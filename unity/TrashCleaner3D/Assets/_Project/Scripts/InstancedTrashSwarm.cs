using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.6 2,250+ GPU Instancing 쓰레기 매니저 (+50% 확장 방 면적 2.25배에 맞춘 압도적 물량, 5드로우콜 렉 0%!):
    /// - 답답한 클릭 쿨타임 대신 '바닥에 눌어붙은 쓰레기 떼어내기(초반 3클릭→2클릭→1클릭)' 방식 적용!
    /// - 매 클릭마다 즉각 들썩임+소리 반응(입력 씹힘 0%) + 10% 이하 황금 투시 테두리 유지!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public const int InitialSwarmCount = 2250;

        public struct TrashUnit
        {
            public Vector3 pos;
            public Quaternion rot;
            public float baseScale;
            public float curScale;
            public int typeIdx;
            public byte state;     // 0:Idle, 1:Popping, 2:Sucking, 3:Cleaned
            public byte hp;        // 바닥에 눌어붙은 강도 (기본 3 → 떼어내기 힘 강화 시 3→2→1클릭 컷!)
            public float hopTimer; // 떼어내기 클릭 시 톡! 들썩이는 피드백 애니메이션
            public float timer;
            public Vector3 startPos;
            public Vector3 endPos;
            public int value;
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

        private Matrix4x4[][] _batchMatrices;
        private int[] _batchCounts;
        private RenderParams[] _renderParams;

        private Matrix4x4[][] _outlineMatrices;
        private int[] _outlineCounts;
        private Material _outlineMat;
        private RenderParams _outlineRenderParams;

        private Matrix4x4[][] _detectMatrices;
        private int[] _detectCounts;
        private Material _detectMat;
        private RenderParams _detectRenderParams;

        private readonly List<int> _selectedIndices = new List<int>(64);
        private readonly List<(int idx, float distSq)> _candidates = new List<(int, float)>(128);
        public int HoveredCount => _selectedIndices.Count;

        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

        public float SelectedPeelProgress01
        {
            get
            {
                if (_selectedIndices.Count == 0) return -1f;
                int idx = _selectedIndices[0];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) return -1f;
                if (_units[idx].hp >= 3) return -1f;
                return Mathf.Clamp01((3 - _units[idx].hp) / 3f);
            }
        }

        private void Awake()
        {
            if (meshTypes.Count > 0 && (_units == null || _units.Length == 0))
            {
                SpawnInitialSwarm(InitialSwarmCount);
            }
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (_units == null || _units.Length == 0)
            {
                SpawnInitialSwarm(InitialSwarmCount);
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

            _detectMatrices = new Matrix4x4[types][];
            _detectCounts = new int[types];

            for (int t = 0; t < meshTypes.Count; t++)
            {
                _batchMatrices[t] = new Matrix4x4[1023];
                _outlineMatrices[t] = new Matrix4x4[1023];
                _detectMatrices[t] = new Matrix4x4[1023];
                if (meshTypes[t].material != null)
                {
                    meshTypes[t].material.enableInstancing = true;
                    _renderParams[t] = new RenderParams(meshTypes[t].material)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 60f),
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
                _outlineMat.SetFloat("_OutlineWidth", 0.015f);
                _outlineMat.enableInstancing = true;
                _outlineRenderParams = new RenderParams(_outlineMat)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 60f),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
            }

            if (_detectMat == null)
            {
                Shader sh = Shader.Find("TrashCleaner3D/DetectOutlineXRay") ?? Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _detectMat = new Material(sh);
                _detectMat.SetColor("_OutlineColor", new Color(1.0f, 0.92f, 0.35f, 0.85f));
                _detectMat.SetFloat("_OutlineWidth", 0.016f);
                _detectMat.enableInstancing = true;
                _detectRenderParams = new RenderParams(_detectMat)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 60f),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
            }
        }

        /// <summary>
        /// 확장된 7.2m x 6.0m 방 전체를 빈틈없이 뒤덮는 2,250개 입체 쓰레기 산 생성!
        /// </summary>
        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 750);

            Vector3[] hubs =
            {
                new Vector3(1.95f, 0f, 0.95f),
                new Vector3(0.65f, 0f, 0.65f),
                new Vector3(2.60f, 0f, -0.65f),
                new Vector3(1.65f, 0f, -1.95f),
                new Vector3(-0.55f, 0f, 1.55f),
                new Vector3(-0.55f, 0f, -0.65f),
                new Vector3(-2.35f, 0f, 1.55f),
                new Vector3(-2.30f, 0f, -1.55f),
                new Vector3(0.20f, 0f, -2.15f),
                new Vector3(-2.65f, 0f, 0.25f)
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                if (i < count * 0.78f)
                {
                    Vector3 h = hubs[i % hubs.Length];
                    Vector2 off = Random.insideUnitCircle * Random.Range(0.08f, 1.38f);
                    float dist01 = Mathf.Clamp01(off.magnitude / 1.38f);
                    float yStack = (1f - dist01) * (1f - dist01) * Random.Range(0.06f, 0.72f);
                    pos = new Vector3(
                        Mathf.Clamp(h.x + off.x, -3.28f, 3.28f),
                        0.06f + yStack,
                        Mathf.Clamp(h.z + off.y, -2.70f, 2.60f)
                    );
                }
                else
                {
                    pos = new Vector3(Random.Range(-3.22f, 3.22f), 0.06f, Random.Range(-2.65f, 2.55f));
                }

                if (pos.x < -1.55f && pos.z < -0.55f) pos.y += 0.32f;
                if (pos.x > 1.50f && pos.z > 1.90f) pos.z -= 1.05f;

                AddUnit(pos, pos, false);
            }
        }

        public void SpawnBurstFountain(Vector3 origin, int count)
        {
            if (meshTypes.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.25f, 1.35f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, -3.15f, 3.15f),
                    0.07f,
                    Mathf.Clamp(origin.z + off.y, -2.60f, 2.45f)
                );
                if (land.x < -1.55f && land.z < -0.55f) land.y += 0.32f;
                AddUnit(origin + Vector3.up * 0.35f, land, true);
            }
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation)
        {
            if (_unitCount >= _units.Length) return;
            int tIdx = Random.Range(0, meshTypes.Count);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.86f, 1.22f);
            Quaternion baseOffset = Quaternion.Euler(meshTypes[tIdx].baseEulerOffset);

            _units[_unitCount++] = new TrashUnit
            {
                pos = startPos,
                rot = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f)) * baseOffset,
                baseScale = baseS,
                curScale = baseS,
                typeIdx = tIdx,
                state = popAnimation ? (byte)1 : (byte)0,
                hp = 3, // 바닥에 눌어붙은 강도 3 (초반 3클릭 톡톡톡! → 강화 시 2클릭 → 1클릭 컷!)
                hopTimer = 0f,
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                value = 3
            };
        }

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
                if (dSq <= rSq) _candidates.Add((i, dSq));
            }

            if (_candidates.Count == 0) return;

            if (maxSelectCount == 1)
            {
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
                for (int k = 0; k < take; k++) _selectedIndices.Add(_candidates[k].idx);
            }
        }

        /// <summary>
        /// 요청 #5: 답답한 쿨타임 대신 즉각 반응하는 '떼어내기 클릭'!
        /// 매 클릭마다 hp가 깎이며 톡! 들썩이고, hp가 0이 되면 봉투로 쏙 흡입됨!
        /// </summary>
        public void HitOrPickSelectedUnits(Transform suckDest, int maxTargets, int pickDamage, out int peeledCount, out int pickedCount)
        {
            peeledCount = 0;
            pickedCount = 0;
            if (maxTargets <= 0 || _selectedIndices.Count == 0) return;
            _suckDestination = suckDest;

            int processed = 0;
            for (int k = 0; k < _selectedIndices.Count && processed < maxTargets; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) continue;
                processed++;

                if (_units[idx].hp > pickDamage)
                {
                    _units[idx].hp = (byte)(_units[idx].hp - pickDamage);
                    _units[idx].hopTimer = 0.14f; // 톡! 들썩이며 떼어지는 손맛!
                    peeledCount++;
                }
                else
                {
                    _units[idx].hp = 0;
                    _units[idx].state = 2; // Sucking!
                    _units[idx].timer = 0f;
                    pickedCount++;
                }
            }
            if (pickedCount > 0) _selectedIndices.Clear();
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
                _detectCounts[t] = 0;
            }

            bool showDetectOutline = manager != null && manager.HasBeaconDetect &&
                                     manager.Phase == GamePhase.Phase1_TrashRemoval &&
                                     RemainingCount > 0 && (RemainingCount <= 80 || manager.Phase1Percent >= 90f);

            if (showDetectOutline && _detectMat != null)
            {
                float pulse = 0.55f + 0.40f * Mathf.Abs(Mathf.Sin(Time.time * 4.5f));
                _detectMat.SetColor("_OutlineColor", new Color(1.0f, 0.90f, 0.32f, pulse));
            }

            for (int i = 0; i < _unitCount; i++)
            {
                byte st = _units[i].state;
                if (st == 3) continue;

                float hopY = 0f;
                if (_units[i].hopTimer > 0f)
                {
                    _units[i].hopTimer = Mathf.Max(0f, _units[i].hopTimer - dt);
                    hopY = Mathf.Sin((_units[i].hopTimer / 0.14f) * Mathf.PI) * 0.11f;
                }

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
                    _units[i].pos = Vector3.MoveTowards(_units[i].pos, destPos, 15.0f * dt);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 540f * dt, 260f * dt);
                    _units[i].curScale = Mathf.MoveTowards(_units[i].curScale, 0f, _units[i].baseScale * 4.2f * dt);

                    if ((_units[i].pos - destPos).sqrMagnitude < 0.04f || _units[i].curScale <= 0.002f)
                    {
                        _units[i].state = 3;
                        _cleanedCount++;
                        if (manager != null) manager.OnInstancedTrashCollected(_units[i].value);
                        continue;
                    }
                }

                int tIdx = _units[i].typeIdx;
                int c = _batchCounts[tIdx];
                if (c < 1023)
                {
                    Vector3 drawPos = hopY > 0f ? _units[i].pos + Vector3.up * hopY : _units[i].pos;
                    Matrix4x4 mtx = Matrix4x4.TRS(drawPos, _units[i].rot, Vector3.one * _units[i].curScale);
                    _batchMatrices[tIdx][c] = mtx;
                    _batchCounts[tIdx] = c + 1;

                    if (showDetectOutline && st == 0)
                    {
                        int dc = _detectCounts[tIdx];
                        if (dc < 1023)
                        {
                            _detectMatrices[tIdx][dc] = mtx;
                            _detectCounts[tIdx] = dc + 1;
                        }
                    }
                }
            }

            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) continue;
                int tIdx = _units[idx].typeIdx;
                int oc = _outlineCounts[tIdx];
                if (oc < 1023)
                {
                    float hopY = _units[idx].hopTimer > 0f ? Mathf.Sin((_units[idx].hopTimer / 0.14f) * Mathf.PI) * 0.11f : 0f;
                    _outlineMatrices[tIdx][oc] = Matrix4x4.TRS(_units[idx].pos + Vector3.up * hopY, _units[idx].rot, Vector3.one * _units[idx].curScale);
                    _outlineCounts[tIdx] = oc + 1;
                }
            }

            for (int t = 0; t < meshTypes.Count; t++)
            {
                int cnt = _batchCounts[t];
                if (cnt > 0 && meshTypes[t].mesh != null && meshTypes[t].material != null)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], meshTypes[t].mesh, 0, _batchMatrices[t], cnt);
                }
            }

            if (showDetectOutline && _detectMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int dcnt = _detectCounts[t];
                    if (dcnt > 0 && meshTypes[t].mesh != null)
                    {
                        Graphics.RenderMeshInstanced(_detectRenderParams, meshTypes[t].mesh, 0, _detectMatrices[t], dcnt);
                    }
                }
            }

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
        }
    }
}
