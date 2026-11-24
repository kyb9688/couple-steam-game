using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// 1,000개 이상의 3D 쓰레기를 렉 0%로 돌리는 GPU Instancing + Zero-GameObject 매니저.
    /// - Awake()에서 Frame 0 즉시 생성 → 문 열자마자 방을 가득 메운 1,000개 쓰레기 산이 바로 보임!
    /// - 커서 아래 가장 가까운 정확히 N개(초반 무조건 1개! 강화로 2→4→7→12→20개)만 선택해
    ///   전용 URP Cull-Front 외곽선 셰이더(WhiteOutlineInstanced)로 선명한 하얀색 테두리 표시!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public struct TrashUnit
        {
            public Vector3 pos;
            public Quaternion rot;
            public float baseScale;
            public float curScale;
            public int typeIdx;
            public byte state; // 0:Idle, 1:Popping, 2:Sucking, 3:Cleaned
            public float timer;
            public Vector3 startPos;
            public Vector3 endPos;
            public int value; // 트럭에 버릴 때 정산될 가치!
        }

        [System.Serializable]
        public class TrashMeshType
        {
            public string label;
            public Mesh mesh;
            public Material material;
            public float normalizedScale = 1f;
            public Vector3 baseEulerOffset = Vector3.zero;
        }

        public List<TrashMeshType> meshTypes = new List<TrashMeshType>();
        public GamePrototypeManager manager;

        private TrashUnit[] _units = new TrashUnit[0];
        private int _unitCount;
        private int _cleanedCount;
        private Transform _suckDestination;

        // Per-frame instancing buffers
        private Matrix4x4[][] _batchMatrices;
        private int[] _batchCounts;
        private RenderParams[] _renderParams;

        // Crisp world-space normal-extruded White Outline pass (NO solid white blob!)
        private Matrix4x4[][] _outlineMatrices;
        private int[] _outlineCounts;
        private Material _outlineMat;
        private RenderParams _outlineRenderParams;

        // Exact indices currently hovered (1 piece at Lv.0, N pieces after upgrade!)
        private readonly List<int> _selectedIndices = new List<int>(64);
        private readonly List<(int idx, float distSq)> _candidates = new List<(int, float)>(128);
        public int HoveredCount => _selectedIndices.Count;

        // Beacon mesh for 10% remaining detect upgrade
        private Mesh _beaconMesh;
        private Material _beaconMat;
        private Matrix4x4[] _beaconMatrices = new Matrix4x4[64];

        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

        private void Awake()
        {
            if (meshTypes.Count > 0 && (_units == null || _units.Length == 0))
            {
                SpawnInitialSwarm(1000);
            }
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (_units == null || _units.Length == 0)
            {
                SpawnInitialSwarm(1000);
            }
            else
            {
                EnsureRenderBuffers();
            }
        }

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _cleanedCount = 0;
            _selectedIndices.Clear();
            EnsureRenderBuffers();
        }

        private void EnsureRenderBuffers()
        {
            int types = Mathf.Max(1, meshTypes.Count);
            _batchMatrices = new Matrix4x4[types][];
            _batchCounts = new int[types];
            _renderParams = new RenderParams[types];

            _outlineMatrices = new Matrix4x4[types][];
            _outlineCounts = new int[types];

            for (int t = 0; t < meshTypes.Count; t++)
            {
                _batchMatrices[t] = new Matrix4x4[1023];
                _outlineMatrices[t] = new Matrix4x4[1023];
                if (meshTypes[t].material != null)
                {
                    meshTypes[t].material.enableInstancing = true;
                    _renderParams[t] = new RenderParams(meshTypes[t].material)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 50f),
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true
                    };
                }
            }

            if (_outlineMat == null)
            {
                Shader sh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _outlineMat = new Material(sh);
                _outlineMat.SetColor("_OutlineColor", Color.white);
                _outlineMat.SetFloat("_OutlineWidth", 0.014f);
                _outlineMat.enableInstancing = true;
                _outlineRenderParams = new RenderParams(_outlineMat)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 50f),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
            }

            if (_beaconMesh == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _beaconMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
                _beaconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _beaconMat.enableInstancing = true;
                _beaconMat.SetColor("_BaseColor", new Color(0.42f, 1.0f, 0.45f, 0.65f));
            }
        }

        /// <summary>
        /// 문 열자마자 한눈에 가득 차 보이도록, 중앙·양옆·매트리스 위까지 입체 산(최대 0.65m 높이) 형태로 1,000개 배치!
        /// </summary>
        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 450);

            // 현관문(x=1.5, z=2.0)을 열었을 때 정면 시선(-Z)에 바로 걸리는 높고 풍성한 쓰레기 산 8곳!
            Vector3[] hubs =
            {
                new Vector3(1.35f, 0f, 0.45f),   // 문 열자마자 바로 정면 앞!
                new Vector3(0.45f, 0f, 0.35f),   // 중앙 정면!
                new Vector3(1.55f, 0f, -0.55f),  // 좌측(동쪽) 벽 산!
                new Vector3(0.95f, 0f, -1.25f),  // 안쪽 정면 산!
                new Vector3(-0.35f, 0f, 0.85f),  // 냉장고 옆 산!
                new Vector3(-0.35f, 0f, -0.45f), // 방 중앙!
                new Vector3(-1.35f, 0f, 0.85f),  // 구석 산!
                new Vector3(-1.30f, 0f, -0.85f)  // 매트리스 위 산!
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                if (i < count * 0.78f)
                {
                    Vector3 h = hubs[i % hubs.Length];
                    Vector2 off = Random.insideUnitCircle * Random.Range(0.08f, 0.88f);
                    float dist01 = Mathf.Clamp01(off.magnitude / 0.88f);
                    // 문 열었을 때 바닥에 납작하게 묻히지 않고 '가득 쌓인' 입체감이 확 살도록 최대 0.58m까지 적층!
                    float yStack = (1f - dist01) * (1f - dist01) * Random.Range(0.06f, 0.58f);
                    pos = new Vector3(
                        Mathf.Clamp(h.x + off.x, -2.10f, 2.10f),
                        0.06f + yStack,
                        Mathf.Clamp(h.z + off.y, -1.78f, 1.65f)
                    );
                }
                else
                {
                    pos = new Vector3(Random.Range(-2.05f, 2.05f), 0.06f, Random.Range(-1.72f, 1.62f));
                }

                // 매트리스 위(-2.18..-0.70, -1.92..0.02)는 매트리스 표면(y=0.30) 위에 얹기
                if (pos.x < -0.70f && pos.z < 0.02f) pos.y += 0.30f;

                // 안쪽으로 열리는 현관문 바로 뒤 최소 회전 공간(x > 1.05 && z > 1.10)만 살짝 비워 문 열릴 때 안 겹치게
                if (pos.x > 1.05f && pos.z > 1.10f) pos.z -= 0.90f;

                AddUnit(pos, pos, false);
            }
        }

        public void SpawnBurstFountain(Vector3 origin, int count)
        {
            if (meshTypes.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.22f, 1.05f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, -2.0f, 2.0f),
                    0.07f,
                    Mathf.Clamp(origin.z + off.y, -1.7f, 1.55f)
                );
                if (land.x < -0.70f && land.z < 0.02f) land.y += 0.30f;
                AddUnit(origin + Vector3.up * 0.35f, land, true);
            }
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation)
        {
            if (_unitCount >= _units.Length) return;
            int tIdx = Random.Range(0, meshTypes.Count);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.86f, 1.20f);
            Quaternion baseOffset = Quaternion.Euler(meshTypes[tIdx].baseEulerOffset);

            _units[_unitCount++] = new TrashUnit
            {
                pos = startPos,
                rot = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f)) * baseOffset,
                baseScale = baseS,
                curScale = baseS,
                typeIdx = tIdx,
                state = popAnimation ? (byte)1 : (byte)0,
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                value = 3 // 개당 $3 (버릴 때 일괄 지급!)
            };
        }

        /// <summary>
        /// 마우스 조준 지점(aimPoint)에서 가장 가까운 정확히 maxSelectCount개(초반 1개!)를 골라 하얀색 테두리 대상으로 지정.
        /// </summary>
        public void UpdateHoverSelection(bool active, Vector3 aimPoint, float searchRadius, int maxSelectCount)
        {
            _selectedIndices.Clear();
            if (!active || maxSelectCount <= 0 || _unitCount == 0) return;

            _candidates.Clear();
            float rSq = searchRadius * searchRadius;

            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - aimPoint;
                d.y *= 0.65f;
                float dSq = d.sqrMagnitude;
                if (dSq <= rSq)
                {
                    _candidates.Add((i, dSq));
                }
            }

            if (_candidates.Count == 0) return;

            if (maxSelectCount == 1)
            {
                // 초반 1개 모드: 가장 가까운 정확히 1개만 빠르게 선택!
                int bestIdx = _candidates[0].idx;
                float bestD = _candidates[0].distSq;
                for (int k = 1; k < _candidates.Count; k++)
                {
                    if (_candidates[k].distSq < bestD)
                    {
                        bestD = _candidates[k].distSq;
                        bestIdx = _candidates[k].idx;
                    }
                }
                _selectedIndices.Add(bestIdx);
            }
            else
            {
                _candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));
                int take = Mathf.Min(maxSelectCount, _candidates.Count);
                for (int k = 0; k < take; k++)
                {
                    _selectedIndices.Add(_candidates[k].idx);
                }
            }
        }

        /// <summary>
        /// 현재 하얀색 테두리로 선택된 정확히 그 쓰레기들(초반 1개 / 업그레이드 시 N개)을 집어서 봉투로 흡입 시작!
        /// </summary>
        public int PickSelectedUnits(Transform suckDest, int maxGrab)
        {
            if (maxGrab <= 0 || _selectedIndices.Count == 0) return 0;
            _suckDestination = suckDest;

            int grabbed = 0;
            for (int k = 0; k < _selectedIndices.Count && grabbed < maxGrab; k++)
            {
                int idx = _selectedIndices[k];
                if (idx >= 0 && idx < _unitCount && _units[idx].state == 0)
                {
                    _units[idx].state = 2; // Sucking!
                    _units[idx].timer = 0f;
                    grabbed++;
                }
            }
            _selectedIndices.Clear();
            return grabbed;
        }

        public void ResetSwarm(int initialCount)
        {
            SpawnInitialSwarm(initialCount);
        }

        private void Update()
        {
            if (_unitCount == 0 || meshTypes.Count == 0 || _batchMatrices == null) return;

            float dt = Time.deltaTime;
            Vector3 destPos = _suckDestination != null ? _suckDestination.position : Vector3.zero;

            for (int t = 0; t < _batchCounts.Length; t++)
            {
                _batchCounts[t] = 0;
                _outlineCounts[t] = 0;
            }

            bool showBeacon = manager != null && manager.HasBeaconDetect &&
                              manager.Phase == GamePhase.Phase1_TrashRemoval &&
                              RemainingCount > 0 && (RemainingCount <= 40 || manager.Phase1Percent >= 90f);
            int beaconCount = 0;

            for (int i = 0; i < _unitCount; i++)
            {
                byte st = _units[i].state;
                if (st == 3) continue;

                if (st == 1)
                {
                    _units[i].timer += dt;
                    float p = Mathf.Clamp01(_units[i].timer / 0.45f);
                    Vector3 cur = Vector3.Lerp(_units[i].startPos, _units[i].endPos, p);
                    cur.y += Mathf.Sin(p * Mathf.PI) * 0.65f;
                    _units[i].pos = cur;
                    if (p >= 1f)
                    {
                        _units[i].pos = _units[i].endPos;
                        _units[i].state = 0;
                    }
                }
                else if (st == 2)
                {
                    _units[i].pos = Vector3.MoveTowards(_units[i].pos, destPos, 14.5f * dt);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 540f * dt, 260f * dt);
                    _units[i].curScale = Mathf.MoveTowards(_units[i].curScale, 0f, _units[i].baseScale * 4.0f * dt);

                    if ((_units[i].pos - destPos).sqrMagnitude < 0.04f || _units[i].curScale <= 0.002f)
                    {
                        _units[i].state = 3;
                        _cleanedCount++;
                        if (manager != null) manager.OnInstancedTrashCollected(_units[i].value);
                        continue;
                    }
                }
                else if (showBeacon && beaconCount < _beaconMatrices.Length && (i % Mathf.Max(1, RemainingCount / 16) == 0))
                {
                    _beaconMatrices[beaconCount++] = Matrix4x4.TRS(
                        _units[i].pos + Vector3.up * 0.9f,
                        Quaternion.identity,
                        new Vector3(0.05f, 0.9f, 0.05f)
                    );
                }

                int tIdx = _units[i].typeIdx;
                int c = _batchCounts[tIdx];
                if (c < 1023)
                {
                    _batchMatrices[tIdx][c] = Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale);
                    _batchCounts[tIdx] = c + 1;
                }
            }

            // 선택된 정확히 N개(초반 1개!)만 전용 Cull-Front 하얀색 외곽선 배치에 추가
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) continue;
                int tIdx = _units[idx].typeIdx;
                int oc = _outlineCounts[tIdx];
                if (oc < 1023)
                {
                    _outlineMatrices[tIdx][oc] = Matrix4x4.TRS(_units[idx].pos, _units[idx].rot, Vector3.one * _units[idx].curScale);
                    _outlineCounts[tIdx] = oc + 1;
                }
            }

            // 1) 일반 3D 쓰레기 GPU Instancing 렌더
            for (int t = 0; t < meshTypes.Count; t++)
            {
                int cnt = _batchCounts[t];
                if (cnt > 0 && meshTypes[t].mesh != null && meshTypes[t].material != null)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], meshTypes[t].mesh, 0, _batchMatrices[t], cnt);
                }
            }

            // 2) 선택된 쓰레기 주위에 선명한 하얀색 외곽 테두리 렌더 (Cull Front → 본체 안 가림!)
            if (_outlineMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int ocnt = _outlineCounts[t];
                    if (ocnt > 0 && meshTypes[t].mesh != null)
                    {
                        Graphics.RenderMeshInstanced(_outlineRenderParams, meshTypes[t].mesh, 0, _outlineMatrices[t], ocnt);
                    }
                }
            }

            // 3) 10% 이하 빛기둥
            if (beaconCount > 0 && _beaconMesh != null && _beaconMat != null)
            {
                var bp = new RenderParams(_beaconMat)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 50f)
                };
                Graphics.RenderMeshInstanced(bp, _beaconMesh, 0, _beaconMatrices, beaconCount);
            }
        }
    }
}
