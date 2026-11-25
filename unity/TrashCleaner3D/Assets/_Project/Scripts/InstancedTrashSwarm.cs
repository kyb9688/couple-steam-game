using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.0 GPU-Instanced 쓰레기 매니저:
    /// - 0.22초 HOLD 진동 줍기(맨손 0.22s → 집게/빗자루 0.15s → 진공청소기 0.07s):
    ///   [LMB] 꾹 누르는 동안 원 안의 쓰레기가 부르르 진동하며 떠오르다가, 차징 완료 시 포물선을 그리며 빨려 들어옴!
    /// - 방 전체 균등 분산 대신 5개 구역의 큰 무더기로 배치 → 한 덩어리 밀면 바닥이 드러남
    /// - 바닥 커서 원형 오라(ZTest Always)가 쓰레기 더미 위에 항상 선명하게 표시!
    /// - 동종 연속 압축(typeIdx 전달) + 로봇청소기 자동 수거 지원.
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public const int InitialSwarmCount = 1200;

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
            public float suckDur;    // 흡입 연출 1회 재생 시간(초) — 거리 기반으로 짧게
            public float arcHeight;   // 흡입 궤적이 위로 솟는 높이
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
        private Material _cursorRingMat;
        private bool _auraVisible;
        private bool _isHoldingNow;
        private Vector3 _auraCenter;
        private float _auraRadius;

        // 바닥 커서 흡입 반경 링 (쓰레기가 멀어도 항상 보이는 항해용 인디케이터)
        private bool _cursorRingVisible;
        private Vector3 _cursorRingCenter;
        private float _cursorRingRadius = 1f;

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
            if (meshTypes.Count == 0) BuildProceduralTypes();
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
                _auraRingMesh = BuildRingMesh(64, 0.93f, 1.0f);
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
                if (_cursorRingMat == null)
                {
                    _cursorRingMat = new Material(_auraRingMat);
                    _cursorRingMat.name = "Mat_CursorRing";
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

        // ===== v1.1.2 디자인: 아트 팩이 없어도 원룸에 어울리는 생활 쓰레기 5종을 코드로 만든다 =====
        /// <summary>
        /// 찌그러진 캔 · 눌린 택배 박스 · 페트병 · 구겨진 비닐봉투 · 구겨진 종이.
        /// 색은 크라프트/무광 알루미늄/반투명 플라스틱/검은 비닐 톤으로 차분하게 맞춤.
        /// </summary>
        public void BuildProceduralTypes()
        {
            if (meshTypes.Count > 0) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;

            // 1) 찌그러진 알루미늄 캔 · 바닥에 누운 자세
            Mesh can = BuildLatheMesh(
                new[] { -0.058f, -0.026f, 0.020f, 0.058f },
                new[] { 0.030f, 0.034f, 0.031f, 0.033f }, 14, 0.055f);
            AddProceduralType(lit, "찌그러진 캔", can, new Color(0.655f, 0.66f, 0.655f), 0.52f, 0.72f, 0.16f, new Vector3(0f, 0f, 90f));

            // 2) 눌린 택배/피자 박스 · 크라프트지
            Mesh box = BuildWarpedBoxMesh(new Vector3(0.30f, 0.065f, 0.235f), 0.07f);
            AddProceduralType(lit, "택배 박스", box, new Color(0.600f, 0.452f, 0.295f), 0.10f, 0.0f, 0.30f, Vector3.zero);

            // 3) 페트병
            Mesh bottle = BuildLatheMesh(
                new[] { -0.085f, -0.055f, 0.030f, 0.062f, 0.098f },
                new[] { 0.031f, 0.035f, 0.029f, 0.013f, 0.014f }, 14, 0.015f);
            AddProceduralType(lit, "페트병", bottle, new Color(0.720f, 0.795f, 0.830f), 0.62f, 0.05f, 0.23f, new Vector3(90f, 0f, 0f));

            // 4) 구겨진 검은 비닐봉투
            Mesh bag = BuildCrumpledBlobMesh(0.105f, 12, 8, 0.17f, 11);
            AddProceduralType(lit, "비닐봉투", bag, new Color(0.125f, 0.125f, 0.135f), 0.38f, 0.0f, 0.21f, Vector3.zero);

            // 5) 구겨진 종이
            Mesh paper = BuildCrumpledBlobMesh(0.075f, 10, 7, 0.30f, 23);
            AddProceduralType(lit, "구겨진 종이", paper, new Color(0.815f, 0.790f, 0.740f), 0.08f, 0.0f, 0.15f, Vector3.zero);
        }

        private void AddProceduralType(Shader lit, string label, Mesh mesh, Color tint, float smoothness, float metallic, float targetDim, Vector3 euler)
        {
            Material mat = new Material(lit);
            mat.name = $"Mat_Proc_{label}";
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            mat.color = tint;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;

            Vector3 sz = mesh.bounds.size;
            float maxDim = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            meshTypes.Add(new TrashMeshType
            {
                label = label,
                mesh = mesh,
                material = mat,
                normalizedScale = maxDim > 0.0001f ? targetDim / maxDim : 1f,
                baseEulerOffset = euler
            });
        }

        /// <summary>Y축 회전체(캔·병) 메쉬: 링별 반지름을 주면 살짝 찌그러진 원통이 된다.</summary>
        private static Mesh BuildLatheMesh(float[] ringY, float[] ringR, int seg, float wobble)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rings = Mathf.Min(ringY.Length, ringR.Length);
            for (int r = 0; r < rings; r++)
            {
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float rr = ringR[r] * (1f + wobble * Mathf.Sin(a * 3f + r * 1.7f) + wobble * 0.45f * Mathf.Sin(a * 5f - r));
                    verts.Add(new Vector3(Mathf.Cos(a) * rr, ringY[r], Mathf.Sin(a) * rr));
                }
            }
            for (int r = 0; r < rings - 1; r++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int i2 = (i + 1) % seg;
                    int a0 = r * seg + i, a1 = r * seg + i2, b0 = (r + 1) * seg + i, b1 = (r + 1) * seg + i2;
                    tris.Add(a0); tris.Add(b0); tris.Add(b1);
                    tris.Add(a0); tris.Add(b1); tris.Add(a1);
                }
            }
            int c0 = verts.Count; verts.Add(new Vector3(0f, ringY[0], 0f));
            for (int i = 0; i < seg; i++) { tris.Add(c0); tris.Add(i); tris.Add((i + 1) % seg); }
            int c1 = verts.Count; verts.Add(new Vector3(0f, ringY[rings - 1], 0f));
            int baseIdx = (rings - 1) * seg;
            for (int i = 0; i < seg; i++) { tris.Add(c1); tris.Add(baseIdx + (i + 1) % seg); tris.Add(baseIdx + i); }
            return BuildOutwardMesh(verts, tris);
        }

        /// <summary>모서리가 살짝 눌린 상자 메쉬 (택배/피자 박스용).</summary>
        private static Mesh BuildWarpedBoxMesh(Vector3 size, float warp)
        {
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>(8);
            for (int i = 0; i < 8; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = (i & 2) == 0 ? -1f : 1f;
                float sz = (i & 4) == 0 ? -1f : 1f;
                verts.Add(new Vector3(
                    sx * h.x * (1f + (sz > 0f ? warp : -warp * 0.5f)),
                    sy * h.y * (1f + (sx > 0f ? warp * 0.6f : 0f)),
                    sz * h.z));
            }
            var tris = new List<int>
            {
                0, 1, 3, 0, 3, 2,
                4, 6, 7, 4, 7, 5,
                0, 4, 5, 0, 5, 1,
                2, 3, 7, 2, 7, 6,
                0, 2, 6, 0, 6, 4,
                1, 5, 7, 1, 7, 3
            };
            return BuildOutwardMesh(verts, tris);
        }

        /// <summary>구겨진 봉투/종이 메쉬: 납작하게 눌린 방울형에 잔주름을 준다.</summary>
        private static Mesh BuildCrumpledBlobMesh(float radius, int seg, int rings, float crumple, int seed)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float phase = (seed % 17) * 0.37f;
            for (int y = 0; y <= rings; y++)
            {
                float v = y / (float)rings;
                float phi = v * Mathf.PI;
                for (int x = 0; x < seg; x++)
                {
                    float theta = x / (float)seg * Mathf.PI * 2f;
                    float n = 1f + crumple * (0.55f * Mathf.Sin(theta * 3f + phi * 2f + phase) + 0.45f * Mathf.Sin(phi * 4f - theta * 2f));
                    float r = radius * n;
                    verts.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * r, Mathf.Cos(phi) * r * 0.66f, Mathf.Sin(phi) * Mathf.Sin(theta) * r));
                }
            }
            for (int y = 0; y < rings; y++)
            {
                for (int x = 0; x < seg; x++)
                {
                    int x2 = (x + 1) % seg;
                    int a = y * seg + x, b = y * seg + x2, c = (y + 1) * seg + x, d = (y + 1) * seg + x2;
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }
            }
            return BuildOutwardMesh(verts, tris);
        }

        /// <summary>중심에서 바깥을 향하도록 삼각형 감기 순서를 자동 보정해 노멀을 바로잡는다.</summary>
        private static Mesh BuildOutwardMesh(List<Vector3> verts, List<int> tris)
        {
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, centroid) < 0f)
                {
                    int tmp = tris[t + 1];
                    tris[t + 1] = tris[t + 2];
                    tris[t + 2] = tmp;
                }
            }
            Mesh m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>쓸어담으면 바닥이 드러나는 5개 무더기 중심 좌표.</summary>
        private static readonly Vector3[] ClusterCenters =
        {
            new Vector3(2.05f, 0f, 0.85f),
            new Vector3(-1.95f, 0f, 1.25f),
            new Vector3(2.35f, 0f, -1.45f),
            new Vector3(-0.85f, 0f, -0.75f),
            new Vector3(0.55f, 0f, 1.45f)
        };

        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 900);

            // v1.0 '구역이 뚫리는' 무더기: 방 전체 균등 분산 대신 4~5개 주요 구역에 큰 클러스터로 몰아둔다.
            // 한 무더기를 밀어내면 그 자리 바닥이 시원하게 드러난다.
            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                if (i < count * 0.86f)
                {
                    Vector3 h = ClusterCenters[i % ClusterCenters.Length];
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    float dist = Mathf.Sqrt(Random.value) * 1.02f;
                    float dist01 = dist / 1.02f;
                    float yStack = Mathf.Pow(1f - dist01, 1.6f) * Random.Range(0.08f, 0.62f);
                    pos = new Vector3(
                        Mathf.Clamp(h.x + Mathf.Cos(ang) * dist, -3.24f, 3.24f),
                        0.06f + yStack,
                        Mathf.Clamp(h.z + Mathf.Sin(ang) * dist, -2.62f, 2.55f)
                    );
                }
                else
                {
                    pos = new Vector3(Random.Range(-3.22f, 3.22f), 0.06f, Random.Range(-2.65f, 2.55f));
                }

                // 5종 미니멀 원룸 가구(침대·책상·냉장고·전자레인지·싱크대)만 회피해 넓은 바닥에 풍성하게 배치!
                if (pos.x < -1.35f && pos.z < -0.65f) pos.y += 0.56f; // 침대 위
                if (pos.x > -1.05f && pos.x < 1.35f && pos.z > 1.88f) pos.z = Random.Range(0.95f, 1.78f); // 냉장고·싱크대 앞
                if (pos.x > -0.45f && pos.x < 1.35f && pos.z < -1.85f) pos.z = Random.Range(-1.75f, -0.95f); // PC 책상 앞
                if (pos.x > 1.45f && pos.z > 1.90f) pos.z -= 1.05f; // 현관문 스윙 공간 확보

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
                    Mathf.Clamp(origin.x + off.x, -2.60f, 2.68f),
                    0.07f,
                    Mathf.Clamp(origin.z + off.y, -1.78f, 1.72f)
                );
                if (land.x < -1.35f && land.z < -0.65f) land.y += 0.56f;
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
                value = 4,
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
            // 홀드 중에는 기존 락온 대상이 커서 원(수평 XZ 반경) 밖으로 벗어나기 전까지 절대 해제/초기화 X!
            if (isHolding && _lockedIndices.Count > 0)
            {
                float keepRSq = (cursorRadius * 1.15f) * (cursorRadius * 1.15f);
                for (int i = _lockedIndices.Count - 1; i >= 0; i--)
                {
                    int idx = _lockedIndices[i];
                    if (idx < 0 || idx >= _unitCount || _units[idx].state != 0)
                    {
                        _lockedIndices.RemoveAt(i);
                        continue;
                    }
                    Vector3 d = _units[idx].pos - aimPoint;
                    d.y = 0f; // 높이 오차로 락온이 풀리지 않도록 원형 커서와 동일한 순수 수평(XZ) 반경으로 판정!
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
                    d.y = 0f;
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
        /// v1.0 경쾌한 HOLD 진동 줍기:
        /// - holdActive=true 동안 원 안의 선택된 쓰레기의 holdCharge가 0 → 1로 올라가며 격렬히 진동!
        /// - 1.0 도달 즉시(맨손 0.22초 → 집게 0.15초 → 진공 0.07초) 빨려 들어가기 시작!
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
                    Vector3 suckDestPos = _suckDestination != null ? _suckDestination.position : _units[idx].pos;
                    BeginSuck(ref _units[idx], suckDestPos, rolledCrit);
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
                        Vector3 chainDest = _suckDestination != null ? _suckDestination.position : _units[i].pos;
                        BeginSuck(ref _units[i], chainDest, false);
                        chained++;
                        picked++;
                    }
                }
                if (chained > 0) didChain = true;
            }

            return picked;
        }

        /// <summary>선택된 쓰레기를 '빨려 들어오는' 상태로 전환한다(거리 기반 짧은 연출 시간).</summary>
        private void BeginSuck(ref TrashUnit unit, Vector3 dest, bool crit)
        {
            unit.startPos = unit.pos;
            unit.state = 2;
            unit.timer = 0f;
            unit.holdCharge = 0f;
            unit.isCritBonus = crit;
            unit.suckDur = Mathf.Clamp(Vector3.Distance(unit.pos, dest) / 7.5f, 0.16f, 0.34f);
            unit.arcHeight = Random.Range(0.30f, 0.75f);
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
                    _units[i].startPos = _units[i].pos;
                    _units[i].suckDur = 0.30f;
                    _units[i].arcHeight = 0.22f;
                    eaten++;
                }
            }
            return eaten;
        }

        public void ResetSwarm(int initialCount)
        {
            SpawnInitialSwarm(initialCount);
        }

        /// <summary>
        /// 바닥의 흡입 반경(SweepRadius)을 보여주는 커서 링. 쓰레기가 반경 밖에 있어도 항상 보여서
        /// "어디까지 빨려오는지"를 눈으로 바로 알 수 있다. (Phase 1 바닥 조준 시 컨트롤러가 호출)
        /// </summary>
        public void SetCursorRing(bool visible, Vector3 center, float radius)
        {
            _cursorRingVisible = visible;
            _cursorRingCenter = center;
            _cursorRingRadius = Mathf.Max(0.1f, radius);
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
                    // v1.0 빨려 들어오는 연출: 직선 이동 대신 위로 솟았다가 커서/손으로 꽂히는 포물선 + 회전 + 축소
                    Vector3 dest = st == 4 ? botDest : playerDest;
                    _units[i].timer += dt;
                    float suckDur = Mathf.Max(0.05f, _units[i].suckDur);
                    float sp = Mathf.Clamp01(_units[i].timer / suckDur);
                    float spe = sp * sp * (3f - 2f * sp);

                    Vector3 from = _units[i].startPos;
                    Vector3 ctrl = Vector3.Lerp(from, dest, 0.5f) + Vector3.up * _units[i].arcHeight;
                    Vector3 pa = Vector3.Lerp(from, ctrl, spe);
                    Vector3 pb = Vector3.Lerp(ctrl, dest, spe);
                    _units[i].pos = Vector3.Lerp(pa, pb, spe);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 560f * dt, 280f * dt);
                    _units[i].curScale = Mathf.Lerp(_units[i].baseScale, 0f, Mathf.Clamp01(spe * 1.18f));

                    if (sp >= 1f)
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
                    ? new Color(0.52f, 0.90f, 0.82f, 0.62f + holdBoost)
                    : tier == 1
                        ? new Color(0.56f, 0.86f, 0.58f, 0.56f + holdBoost)
                        : _isHoldingNow
                            ? new Color(1.0f, 0.84f, 0.38f, 0.70f + holdBoost * 0.5f)
                            : new Color(1.0f, 0.96f, 0.88f, 0.48f);
                _auraRingMat.SetColor("_BaseColor", auraCol);

                float spin = tier >= 2 ? now * 110f : (_isHoldingNow ? now * 55f : 0f);
                float rScale = _auraRadius * (_isHoldingNow ? (1f - MaxSelectedCharge01 * 0.12f) : 1f);
                Vector3 ringPos = new Vector3(_auraCenter.x, Mathf.Max(0.04f, _auraCenter.y + 0.04f), _auraCenter.z);
                Matrix4x4 ringMtx = Matrix4x4.TRS(ringPos, Quaternion.Euler(0f, spin, 0f), new Vector3(rScale, 1f, rScale));
                Graphics.DrawMesh(_auraRingMesh, ringMtx, _auraRingMat, 0);
            }
            // 5) 바닥 커서 흡입 반경 링 — 쓰레기 호버 오라가 없을 때도 항상 반경을 보여준다.
            if (_cursorRingVisible && !_auraVisible && _auraRingMesh != null && _cursorRingMat != null && _cursorRingRadius > 0.05f)
            {
                bool holding = _isHoldingNow;
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.6f);
                Color ringCol = holding
                    ? new Color(1.0f, 0.84f, 0.38f, 0.72f + 0.18f * pulse)
                    : new Color(1.0f, 0.965f, 0.88f, 0.34f + 0.12f * pulse);
                _cursorRingMat.SetColor("_BaseColor", ringCol);

                Vector3 ringPos = new Vector3(_cursorRingCenter.x, Mathf.Max(0.035f, _cursorRingCenter.y + 0.035f), _cursorRingCenter.z);
                Matrix4x4 ringMtx = Matrix4x4.TRS(ringPos, Quaternion.identity, new Vector3(_cursorRingRadius, 1f, _cursorRingRadius));
                Graphics.DrawMesh(_auraRingMesh, ringMtx, _cursorRingMat, 0);
            }
        }
    }
}
