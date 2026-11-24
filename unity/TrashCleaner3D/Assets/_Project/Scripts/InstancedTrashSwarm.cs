using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// 1,000개 이상의 3D 쓰레기를 렉 0%로 돌리는 GPU Instancing + Zero-GameObject 매니저.
    /// - 개별 GameObject / Collider / Update() 0개!
    /// - 마우스를 올린 반경 안의 모든 3D 쓰레기 조각에 실시간 하얀색 테두리(Inverted-Hull Outline) 일괄 렌더링!
    /// - 클릭(맨손 한 웅큼 줍기) 및 진공청소기(꾹 눌러 폭풍 흡입) 모두 지원.
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
            public int reward;
        }

        [System.Serializable]
        public class TrashMeshType
        {
            public string label;
            public Mesh mesh;
            public Material material;
            public float normalizedScale = 1f;
        }

        public List<TrashMeshType> meshTypes = new List<TrashMeshType>();
        public GamePrototypeManager manager;

        private TrashUnit[] _units = new TrashUnit[0];
        private int _unitCount;
        private int _cleanedCount;
        private Transform _suckDestination;

        // Per-frame instancing buffers (max 1023 per Graphics.RenderMeshInstanced call)
        private Matrix4x4[][] _batchMatrices;
        private int[] _batchCounts;
        private RenderParams[] _renderParams;

        // White hover outline pass (inverted-hull CullMode.Front!)
        private Matrix4x4[][] _outlineMatrices;
        private int[] _outlineCounts;
        private Material _outlineMat;
        private RenderParams _outlineRenderParams;

        private bool _hoverActive;
        private Vector3 _hoverCenter;
        private float _hoverRadius;
        public int HoveredCount { get; private set; }

        // Beacon mesh for 10% remaining detect upgrade
        private Mesh _beaconMesh;
        private Material _beaconMat;
        private Matrix4x4[] _beaconMatrices = new Matrix4x4[64];

        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _cleanedCount = 0;
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
                _outlineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _outlineMat.SetColor("_BaseColor", Color.white);
                _outlineMat.SetInt("_Cull", (int)CullMode.Front);
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

        public void SetHoverSelection(bool active, Vector3 center, float radius)
        {
            _hoverActive = active;
            _hoverCenter = center;
            _hoverRadius = radius;
        }

        /// <summary>
        /// 방 바닥 전역에 1,000개의 3D 소형 쓰레기를 자연스러운 더미 분포로 생성 (물리 0, 렉 0!)
        /// 밖에서 문을 열었을 때 극적으로 보이도록 현관문 시선 정면(방 중앙·좌우)에 풍성하게 배치.
        /// </summary>
        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 450);

            Vector3[] hubs =
            {
                new Vector3(1.15f, 0f, 0.25f),
                new Vector3(1.55f, 0f, -0.55f),
                new Vector3(1.30f, 0f, -1.25f),
                new Vector3(0.35f, 0f, 0.55f),
                new Vector3(-0.55f, 0f, 1.15f),
                new Vector3(0.25f, 0f, -0.55f),
                new Vector3(-1.10f, 0f, 0.85f)
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                if (i < count * 0.72f)
                {
                    Vector3 h = hubs[i % hubs.Length];
                    Vector2 off = Random.insideUnitCircle * Random.Range(0.15f, 0.95f);
                    float dist01 = Mathf.Clamp01(off.magnitude / 0.95f);
                    float yStack = (1f - dist01) * Random.Range(0.02f, 0.28f);
                    pos = new Vector3(
                        Mathf.Clamp(h.x + off.x, -2.05f, 2.05f),
                        0.03f + yStack,
                        Mathf.Clamp(h.z + off.y, -1.72f, 1.65f)
                    );
                }
                else
                {
                    pos = new Vector3(Random.Range(-1.95f, 1.95f), 0.03f, Random.Range(-1.68f, 1.65f));
                }

                // 매트리스 위(-2.1..-0.70, -1.9..0.02)는 매트리스 상단에 얹기
                if (pos.x < -0.70f && pos.z < 0.02f) pos.y += 0.30f;

                // 안쪽으로 열리는 현관문 바로 앞 회전 반경(x > 1.20 && z > 1.15)만 살짝 비워 문 열릴 때 깔끔하게!
                if (pos.x > 1.18f && pos.z > 1.15f) pos.z -= 0.95f;

                AddUnit(pos, pos, false);
            }
        }

        public void SpawnBurstFountain(Vector3 origin, int count)
        {
            if (meshTypes.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.25f, 1.15f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, -1.95f, 1.95f),
                    0.04f,
                    Mathf.Clamp(origin.z + off.y, -1.65f, 1.60f)
                );
                if (land.x < -0.70f && land.z < 0.02f) land.y += 0.30f;
                AddUnit(origin + Vector3.up * 0.35f, land, true);
            }
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation)
        {
            if (_unitCount >= _units.Length) return;
            int tIdx = Random.Range(0, meshTypes.Count);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.84f, 1.24f);

            _units[_unitCount++] = new TrashUnit
            {
                pos = startPos,
                rot = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)),
                baseScale = baseS,
                curScale = baseS,
                typeIdx = tIdx,
                state = popAnimation ? (byte)1 : (byte)0,
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                reward = 1
            };
        }

        /// <summary>
        /// 하얗게 선택된 반경(aimPoint, radius) 안의 쓰레기를 가까운 순으로 한 웅큼(maxGrab) 주워 흡입 시작!
        /// </summary>
        public int SweepAt(Vector3 aimPoint, float radius, int maxGrab, Transform suckDest)
        {
            if (maxGrab <= 0 || _unitCount == 0) return 0;
            _suckDestination = suckDest;

            float rSq = radius * radius;
            int grabbed = 0;

            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - aimPoint;
                d.y *= 0.45f;
                if (d.sqrMagnitude <= rSq)
                {
                    _units[i].state = 2; // Sucking!
                    _units[i].timer = 0f;
                    grabbed++;
                    if (grabbed >= maxGrab) break;
                }
            }
            return grabbed;
        }

        public bool HasAnyIdleNear(Vector3 aimPoint, float radius)
        {
            float rSq = radius * radius;
            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - aimPoint;
                d.y *= 0.45f;
                if (d.sqrMagnitude <= rSq) return true;
            }
            return false;
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
            float hoverRSq = _hoverRadius * _hoverRadius;
            int hoveredNow = 0;

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
                        if (manager != null) manager.OnInstancedTrashCollected(_units[i].reward);
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

                // 마우스가 올라간 반경 안의 대기 중(state == 0) 쓰레기에 하얀색 테두리(White Outline) 배치!
                if (_hoverActive && st == 0)
                {
                    Vector3 d = _units[i].pos - _hoverCenter;
                    d.y *= 0.45f;
                    if (d.sqrMagnitude <= hoverRSq)
                    {
                        hoveredNow++;
                        int oc = _outlineCounts[tIdx];
                        if (oc < 1023)
                        {
                            _outlineMatrices[tIdx][oc] = Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * (_units[i].curScale * 1.18f));
                            _outlineCounts[tIdx] = oc + 1;
                        }
                    }
                }
            }

            HoveredCount = hoveredNow;

            // 1) 일반 3D 쓰레기 GPU Instancing 렌더 (5 드로우콜)
            for (int t = 0; t < meshTypes.Count; t++)
            {
                int cnt = _batchCounts[t];
                if (cnt > 0 && meshTypes[t].mesh != null && meshTypes[t].material != null)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], meshTypes[t].mesh, 0, _batchMatrices[t], cnt);
                }
            }

            // 2) 마우스 호버 중인 쓰레기만 하얀색 외곽 테두리(Inverted-Hull) 렌더!
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
