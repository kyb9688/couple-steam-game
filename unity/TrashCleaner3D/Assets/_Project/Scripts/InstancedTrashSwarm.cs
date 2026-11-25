using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.9 GPU-Instanced 2,250+ 쓰레기 매니저:
    /// - 1초 HOLD 진동 줍기(맨손 1.0s → 집게/빗자루 0.40s → 진공청소기 0.10s):
    ///   [LMB] 꾹 누르는 동안 원 안의 쓰레기가 부르르 진동하며 떠오르다가, 차징 완료 시 쏙 빨려 들어옴!
    /// - 바닥 커서 원형 오라(ZTest Always)가 쓰레기 더미 위에 항상 선명하게 표시!
    /// - 동종 연속 압축(typeIdx 전달) + 로봇청소기 자동 수거 지원.
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
            public byte state;       // 0:Idle, 1:Popping, 2:SuckingToPlayer, 3:Cleaned, 4:SuckingToRobot
            public float timer;
            public Vector3 startPos;
            public Vector3 endPos;
            public int value;
            public bool isCritBonus;
            public float holdCharge; // 0~1: 꾹 누르는 동안 진동 차징!
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
        private Transform _robotSuckDest;

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

        // 커서 원형 오라 (쓰레기 위에 항상 선명히 보이는 오버레이 링!)
        private Mesh _auraRingMesh;
        private Material _auraRingMat;
        private bool _auraVisible;
        private bool _isHoldingNow;
        private Vector3 _auraCenter;
        private float _auraRadius;

        private readonly List<int> _selectedIndices = new List<int>(64);
        private readonly HashSet<int> _selectedSet = new HashSet<int>();
        private readonly List<int> _lockedIndices = new List<int>(64);
        private readonly List<(int idx, float distSq)> _candidates = new List<(int, float)>(256);

        public int HoveredCount => _selectedIndices.Count;
        public float MaxSelectedCharge01 { get; private set; }
        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

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
            if (_units == null || _units.Length == 0) SpawnInitialSwarm(InitialSwarmCount);
            else EnsureRenderBuffers();
        }

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _cleanedCount = 0;
            _selectedIndices.Clear();
            _selectedSet.Clear();
            _lockedIndices.Clear();
            MaxSelectedCharge01 = 0f;
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

            if (_auraRingMesh == null)
            {
                _auraRingMesh = BuildRingMesh(48, 0.86f, 1.0f);
                Shader ringSh = Shader.Find("TrashCleaner3D/CursorRingOverlay") ?? Shader.Find("Universal Render Pipeline/Unlit");
                _auraRingMat = new Material(ringSh);
                if (ringSh.name.Contains("Unlit"))
                {
                    _auraRingMat.SetFloat("_Surface", 1f);
                    _auraRingMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    _auraRingMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    _auraRingMat.SetFloat("_ZWrite", 0f);
                    _auraRingMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _auraRingMat.renderQueue = (int)RenderQueue.Transparent + 10;
                }
            }
        }

        private static Mesh BuildRingMesh(int segments, float innerR, float outerR)
        {
            Mesh m = new Mesh();
            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);

                int ni = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }

        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 900);

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

                int preferredType = (i < count * 0.55f) ? ((i / 6) % meshTypes.Count) : Random.Range(0, meshTypes.Count);
                AddUnit(pos, pos, false, preferredType);
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
                AddUnit(origin + Vector3.up * 0.35f, land, true, Random.Range(0, meshTypes.Count));
            }
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation, int tIdx)
        {
            if (_unitCount >= _units.Length) return;
            tIdx = Mathf.Clamp(tIdx, 0, meshTypes.Count - 1);
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
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                value = 2,
                isCritBonus = false,
                holdCharge = 0f
            };
        }

        public void UpdateHoverSelection(bool active, bool isHolding, Vector3 aimPoint, float cursorRadius, int maxSelectCount)
        {
            _selectedIndices.Clear();
            _selectedSet.Clear();
            _auraVisible = active;
            _auraCenter = aimPoint;
            _auraRadius = cursorRadius;
            if (!active || !isHolding)
            {
                _isHoldingNow = false;
                _lockedIndices.Clear();
            }

            if (!active || maxSelectCount <= 0 || _unitCount == 0)
            {
                MaxSelectedCharge01 = 0f;
                return;
            }

            float rSq = cursorRadius * cursorRadius;
            // 홀드 중에는 기존 락온 대상이 원을 벗어나지 않으면(약간의 여유 히스테리시스 ×1.12) 끝까지 유지!
            if (isHolding && _lockedIndices.Count > 0)
            {
                float keepRSq = (cursorRadius * 1.12f) * (cursorRadius * 1.12f);
                for (int i = _lockedIndices.Count - 1; i >= 0; i--)
                {
                    int idx = _lockedIndices[i];
                    if (idx < 0 || idx >= _unitCount || _units[idx].state != 0)
                    {
                        _lockedIndices.RemoveAt(i);
                        continue;
                    }
                    Vector3 d = _units[idx].pos - aimPoint;
                    d.y *= 0.65f;
                    if (d.sqrMagnitude > keepRSq)
                    {
                        _lockedIndices.RemoveAt(i);
                        continue;
                    }
                    if (_selectedIndices.Count < maxSelectCount && _selectedSet.Add(idx))
                    {
                        _selectedIndices.Add(idx);
                    }
                }
            }

            // 남은 슬롯이 있으면 원 안의 가장 가까운 후보로 채움
            if (_selectedIndices.Count < maxSelectCount)
            {
                _candidates.Clear();
                for (int i = 0; i < _unitCount; i++)
                {
                    if (_units[i].state != 0 || _selectedSet.Contains(i)) continue;
                    Vector3 d = _units[i].pos - aimPoint;
                    d.y *= 0.65f;
                    float dSq = d.sqrMagnitude;
                    if (dSq <= rSq) _candidates.Add((i, dSq));
                }

                if (_candidates.Count > 0)
                {
                    _candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));
                    int need = Mathf.Min(maxSelectCount - _selectedIndices.Count, _candidates.Count);
                    for (int k = 0; k < need; k++)
                    {
                        int idx = _candidates[k].idx;
                        _selectedIndices.Add(idx);
                        _selectedSet.Add(idx);
                        if (isHolding) _lockedIndices.Add(idx);
                    }
                }
            }

            float maxC = 0f;
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                float c = _units[_selectedIndices[k]].holdCharge;
                if (c > maxC) maxC = c;
            }
            MaxSelectedCharge01 = maxC;
        }

        /// <summary>
        /// v0.9 1초 HOLD 진동 줍기:
        /// - holdActive=true 동안 원 안의 선택된 쓰레기의 holdCharge가 0 → 1로 올라가며 격렬히 진동!
        /// - 1.0(맨손 1.0초 → 집게 0.40초 → 진공 0.10초)에 도달하면 쏙 빨려 들어감!
        /// </summary>
        public int TickHoldChargeAndPull(
            bool holdActive, float dt, float holdDurationSeconds,
            Transform suckDest, float critChance, float chainChance, int chainExtraCount,
            out bool didCrit, out bool didChain, out Vector3 fxOrigin)
        {
            didCrit = false;
            didChain = false;
            fxOrigin = Vector3.zero;
            _isHoldingNow = holdActive && _selectedIndices.Count > 0;

            if (!_isHoldingNow || _unitCount == 0) return 0;

            _suckDestination = suckDest;
            float rate = 1f / Mathf.Max(0.04f, holdDurationSeconds);
            int picked = 0;
            float maxC = 0f;

            bool rolledCrit = critChance > 0f && Random.value < critChance;

            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) continue;

                _units[idx].holdCharge = Mathf.Min(1f, _units[idx].holdCharge + dt * rate);
                if (_units[idx].holdCharge > maxC) maxC = _units[idx].holdCharge;

                if (_units[idx].holdCharge >= 1f)
                {
                    if (picked == 0) fxOrigin = _units[idx].pos;
                    _units[idx].state = 2;
                    _units[idx].timer = 0f;
                    _units[idx].holdCharge = 0f;
                    _units[idx].isCritBonus = rolledCrit;
                    picked++;
                    if (rolledCrit) didCrit = true;
                }
            }

            MaxSelectedCharge01 = picked > 0 ? 0f : maxC;

            if (picked > 0 && chainChance > 0f && chainExtraCount > 0 && Random.value < chainChance)
            {
                float chainRSq = 1.15f * 1.15f;
                int chained = 0;
                for (int i = 0; i < _unitCount && chained < chainExtraCount; i++)
                {
                    if (_units[i].state != 0) continue;
                    if ((_units[i].pos - fxOrigin).sqrMagnitude <= chainRSq)
                    {
                        _units[i].state = 2;
                        _units[i].timer = 0f;
                        _units[i].holdCharge = 0f;
                        _units[i].isCritBonus = false;
                        chained++;
                        picked++;
                    }
                }
                if (chained > 0) didChain = true;
            }

            return picked;
        }

        public bool TryFindNearestIdlePos(Vector3 fromPos, out Vector3 foundPos)
        {
            foundPos = Vector3.zero;
            float best = float.MaxValue;
            int bestIdx = -1;
            int step = Mathf.Max(1, _unitCount / 220);
            for (int i = 0; i < _unitCount; i += step)
            {
                if (_units[i].state != 0) continue;
                float dSq = (_units[i].pos - fromPos).sqrMagnitude;
                if (dSq < best) { best = dSq; bestIdx = i; }
            }
            if (bestIdx >= 0) { foundPos = _units[bestIdx].pos; return true; }
            return false;
        }

        public int SuckNearestForRobot(Vector3 robotPos, float radius, Transform robotTrans, int maxEat)
        {
            _robotSuckDest = robotTrans;
            float rSq = radius * radius;
            int eaten = 0;
            for (int i = 0; i < _unitCount && eaten < maxEat; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - robotPos;
                d.y *= 0.4f;
                if (d.sqrMagnitude <= rSq)
                {
                    _units[i].state = 4;
                    _units[i].timer = 0f;
                    _units[i].holdCharge = 0f;
                    eaten++;
                }
            }
            return eaten;
        }

        public void ResetSwarm(int initialCount)
        {
            SpawnInitialSwarm(initialCount);
        }

        private void Update()
        {
            if (_unitCount == 0 || meshTypes.Count == 0 || _batchMatrices == null) return;

            float dt = Time.deltaTime;
            float now = Time.time;
            Vector3 playerDest = _suckDestination != null ? _suckDestination.position : Vector3.zero;
            Vector3 botDest = _robotSuckDest != null ? _robotSuckDest.position : Vector3.zero;

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
                float pulse = 0.55f + 0.40f * Mathf.Abs(Mathf.Sin(now * 4.5f));
                _detectMat.SetColor("_OutlineColor", new Color(1.0f, 0.90f, 0.32f, pulse));
            }

            for (int i = 0; i < _unitCount; i++)
            {
                byte st = _units[i].state;
                if (st == 3) continue;

                // 선택에서 벗어났거나 홀드를 떼면 진동 차징 부드럽게 원복
                if (st == 0 && _units[i].holdCharge > 0f && (!_isHoldingNow || !_selectedSet.Contains(i)))
                {
                    _units[i].holdCharge = Mathf.MoveTowards(_units[i].holdCharge, 0f, dt * 2.4f);
                }

                if (st == 1)
                {
                    _units[i].timer += dt;
                    float p = Mathf.Clamp01(_units[i].timer / 0.45f);
                    Vector3 cur = Vector3.Lerp(_units[i].startPos, _units[i].endPos, p);
                    cur.y += Mathf.Sin(p * Mathf.PI) * 0.65f;
                    _units[i].pos = cur;
                    if (p >= 1f) { _units[i].pos = _units[i].endPos; _units[i].state = 0; }
                }
                else if (st == 2 || st == 4)
                {
                    Vector3 dest = st == 4 ? botDest : playerDest;
                    float suckSpd = (manager != null && manager.ToolTier >= 2) ? 18.5f : 15.5f;
                    _units[i].pos = Vector3.MoveTowards(_units[i].pos, dest, suckSpd * dt);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 680f * dt, 300f * dt);
                    _units[i].curScale = Mathf.MoveTowards(_units[i].curScale, 0f, _units[i].baseScale * 4.8f * dt);

                    if ((_units[i].pos - dest).sqrMagnitude < 0.04f || _units[i].curScale <= 0.002f)
                    {
                        _units[i].state = 3;
                        _cleanedCount++;
                        if (st == 2 && manager != null)
                        {
                            int val = _units[i].isCritBonus ? _units[i].value * 2 : _units[i].value;
                            string label = _units[i].typeIdx < meshTypes.Count ? meshTypes[_units[i].typeIdx].label : "쓰레기";
                            manager.OnInstancedTrashCollected(val, _units[i].typeIdx, label);
                        }
                        continue;
                    }
                }

                // 홀드 진동 오프셋 계산! (부르르 흔들리며 살짝 부양 + 살짝 커지는 탄성감!)
                Vector3 renderPos = _units[i].pos;
                Quaternion renderRot = _units[i].rot;
                float renderScale = _units[i].curScale;
                float ch = _units[i].holdCharge;
                if (st == 0 && ch > 0.01f)
                {
                    float freq = 52f + ch * 38f;
                    float amp = 0.012f + ch * 0.032f;
                    float seed = i * 1.73f;
                    renderPos += new Vector3(
                        Mathf.Sin(now * freq + seed) * amp,
                        ch * 0.09f + Mathf.Abs(Mathf.Sin(now * freq * 0.8f + seed)) * (0.025f * ch),
                        Mathf.Cos(now * freq * 1.13f + seed) * amp
                    );
                    renderRot = renderRot * Quaternion.Euler(
                        Mathf.Sin(now * freq * 0.9f + seed) * (14f * ch),
                        Mathf.Cos(now * freq * 0.7f + seed) * (18f * ch),
                        Mathf.Sin(now * freq * 1.05f + seed) * (14f * ch)
                    );
                    renderScale *= (1f + ch * 0.14f);
                }

                int tIdx = _units[i].typeIdx;
                int c = _batchCounts[tIdx];
                if (c < 1023)
                {
                    Matrix4x4 mtx = Matrix4x4.TRS(renderPos, renderRot, Vector3.one * renderScale);
                    _batchMatrices[tIdx][c] = mtx;
                    _batchCounts[tIdx] = c + 1;

                    if (showDetectOutline && st == 0)
                    {
                        int dc = _detectCounts[tIdx];
                        if (dc < 1023) { _detectMatrices[tIdx][dc] = mtx; _detectCounts[tIdx] = dc + 1; }
                    }

                    if (st == 0 && _selectedSet.Contains(i))
                    {
                        int oc = _outlineCounts[tIdx];
                        if (oc < 1023) { _outlineMatrices[tIdx][oc] = mtx; _outlineCounts[tIdx] = oc + 1; }
                    }
                }
            }

            // 1) 일반 3D 쓰레기 렌더
            for (int t = 0; t < meshTypes.Count; t++)
            {
                int cnt = _batchCounts[t];
                if (cnt > 0 && meshTypes[t].mesh != null && meshTypes[t].material != null)
                    Graphics.RenderMeshInstanced(_renderParams[t], meshTypes[t].mesh, 0, _batchMatrices[t], cnt);
            }

            // 2) 10% 투시 황금 테두리
            if (showDetectOutline && _detectMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int dcnt = _detectCounts[t];
                    if (dcnt > 0 && meshTypes[t].mesh != null)
                        Graphics.RenderMeshInstanced(_detectRenderParams, meshTypes[t].mesh, 0, _detectMatrices[t], dcnt);
                }
            }

            // 3) 호버/진동 하얀 테두리
            if (_outlineMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int ocnt = _outlineCounts[t];
                    if (ocnt > 0 && meshTypes[t].mesh != null)
                        Graphics.RenderMeshInstanced(_outlineRenderParams, meshTypes[t].mesh, 0, _outlineMatrices[t], ocnt);
                }
            }

            // 4) 커서 원형 오라 (쓰레기 위에 항상 선명히 표시! 홀드 중에는 채도/크기 펄스!)
            if (_auraVisible && _auraRingMesh != null && _auraRingMat != null && _auraRadius > 0.05f)
            {
                int tier = manager != null ? manager.ToolTier : 0;
                float holdBoost = _isHoldingNow ? (0.22f + MaxSelectedCharge01 * 0.25f) : 0f;
                Color auraCol = tier >= 2
                    ? new Color(0.22f, 0.94f, 1.0f, 0.65f + holdBoost)
                    : tier == 1
                        ? new Color(0.42f, 0.98f, 0.54f, 0.58f + holdBoost)
                        : _isHoldingNow
                            ? new Color(1.0f, 0.90f, 0.32f, 0.72f + holdBoost * 0.5f)
                            : new Color(1.0f, 1.0f, 1.0f, 0.55f);
                _auraRingMat.SetColor("_BaseColor", auraCol);

                float spin = tier >= 2 ? now * 110f : (_isHoldingNow ? now * 55f : 0f);
                float rScale = _auraRadius * (_isHoldingNow ? (1f - MaxSelectedCharge01 * 0.12f) : 1f);
                Vector3 ringPos = new Vector3(_auraCenter.x, Mathf.Max(0.04f, _auraCenter.y + 0.04f), _auraCenter.z);
                Matrix4x4 ringMtx = Matrix4x4.TRS(ringPos, Quaternion.Euler(0f, spin, 0f), new Vector3(rScale, 1f, rScale));
                Graphics.DrawMesh(_auraRingMesh, ringMtx, _auraRingMat, 0);
            }
        }
    }
}
