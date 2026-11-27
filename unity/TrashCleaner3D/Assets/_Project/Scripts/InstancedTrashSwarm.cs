using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.4 초고속 8,000+ GPU-Instanced 쓰레기 엔진 (WebGL 60fps 대응):
    /// 1) 멀티청크 GPU Instancing(1023개 제한 해제): 8,000개도 드로우콜 ~16개로 렌더!
    /// 2) 정적/동적 분리 + Dirty-Flag 캐시: 멈춰있는 7,990개는 매 프레임 Matrix4x4 재계산 0회!
    /// 3) 2D Spatial Hash Grid (1m 셀): 8,000개 중 커서 반경/로봇 주변 셀만 O(1) 탐색!
    /// 4) 바닥 원형 색 매트 제거 → 실제 방 단위(1단계 원룸 1방 / 2단계 거실·주방·안방·화장실 4방) 클리어 추적!
    /// 5) HOLD 락온 유지(순수 수평 XZ 반경) + 포물선 흡입 연출!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public const int Stage1SwarmCount = 1650;
        public const int Stage2SwarmCount = 8000;

        public struct RoomZoneDef
        {
            public string name;
            public float minX, maxX, minZ, maxZ;
            public float floorY;
            public Vector3 center;
            public int countToSpawn;
        }

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
            public float holdCharge;
            public float suckDur;
            public float arcHeight;
            public byte roomIdx;
            public Matrix4x4 cachedMatrix;
            public int gridKey;
            public bool inActiveList;
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
        private int _currentStageLayout = 1;
        private Transform _suckDestination;
        private Transform _robotSuckDest;

        // 2D Spatial Hash Grid (1.0m cells) -> O(1) lookup even at 8,000+ units!
        private readonly Dictionary<int, List<int>> _grid = new Dictionary<int, List<int>>(512);
        // Active-only update list (only popping / sucking / vibrating units!)
        private readonly List<int> _activeIndices = new List<int>(128);

        // Multi-chunk Static & Dynamic GPU Instancing buffers
        private List<Matrix4x4[]>[] _staticChunks;
        private int[] _staticCounts;
        private bool _staticDirty = true;

        private List<Matrix4x4[]>[] _dynChunks;
        private int[] _dynCounts;

        private RenderParams[] _renderParams;

        private Matrix4x4[][] _outlineMatrices;
        private int[] _outlineCounts;
        private Material _outlineMat;
        private RenderParams _outlineRenderParams;

        private List<Matrix4x4[]>[] _detectChunks;
        private int[] _detectCounts;
        private Material _detectMat;
        private RenderParams _detectRenderParams;

        // Floor cursor ring (y=0 fixed, crisp overlay)
        private Mesh _auraRingMesh;
        private Material _auraRingMat;
        private Material _cursorRingMat;
        private bool _auraVisible;
        private bool _isHoldingNow;
        private Vector3 _auraCenter;
        private float _auraRadius;
        private bool _cursorRingVisible;
        private Vector3 _cursorRingCenter;
        private float _cursorRingRadius = 0.5f;

        private readonly List<int> _selectedIndices = new List<int>(64);
        private readonly HashSet<int> _selectedSet = new HashSet<int>();
        private readonly List<int> _lockedIndices = new List<int>(64);
        private readonly List<(int idx, float distSq)> _candidates = new List<(int, float)>(256);

        /// <summary>스폰(쓰레기 채우기)이 아직 진행 중인지. 프레임 예산 안에서 나눠 채운다.</summary>
        public bool SpawnInProgress { get; private set; }
        /// <summary>한 프레임에 스폰에 쓸 수 있는 최대 시간(초). 60fps 예산 안에서 남는 몫만 사용.</summary>
        private const float SpawnFrameBudgetSeconds = 0.005f;
        private Coroutine _spawnRoutine;

        public int HoveredCount => _selectedIndices.Count;
        public float MaxSelectedCharge01 { get; private set; }
        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

        // ===== 방 단위 클리어 추적 (원룸 1방 / 2단계 거실·주방·안방·화장실 4방) =====
        public event System.Action<int, string, Vector3> RoomCleared;
        private RoomZoneDef[] _rooms = new RoomZoneDef[0];
        private int[] _roomRemaining = new int[0];
        private int[] _roomInitial = new int[0];
        private bool[] _roomClearedFlags = new bool[0];

        public int RoomCount => _rooms.Length;
        public int ClearedRoomCount { get; private set; }

        public string GetRoomName(int idx) => (idx >= 0 && idx < _rooms.Length) ? _rooms[idx].name : "방";
        public int GetRoomRemaining(int idx) => (idx >= 0 && idx < _roomRemaining.Length) ? _roomRemaining[idx] : 0;
        public int GetRoomInitial(int idx) => (idx >= 0 && idx < _roomInitial.Length) ? _roomInitial[idx] : 1;
        public bool IsRoomCleared(int idx) => idx >= 0 && idx < _roomClearedFlags.Length && _roomClearedFlags[idx];
        public Vector3 GetRoomCenter(int idx) => (idx >= 0 && idx < _rooms.Length) ? _rooms[idx].center : Vector3.zero;

        // Stage 1: +30% 확장 원룸 (9.36 x 7.80, x: -4.68..+4.68, z: -3.90..+3.90) — 기존 원룸 그대로 유지!
        private static readonly RoomZoneDef[] Stage1Rooms =
        {
            new RoomZoneDef { name = "반지하 원룸", minX = -4.35f, maxX = 4.35f, minZ = -3.60f, maxZ = 3.55f, floorY = 0f, center = new Vector3(0f, 0f, 0f), countToSpawn = Stage1SwarmCount }
        };

        // Stage 2: 택배 중독 오피스텔 (4.2 x 6.8, 문 x=1.10 z=3.60, 북서 화장실 x:-2.1..-0.55 z:1.7..3.4)
        private static readonly RoomZoneDef[] Stage2Rooms =
        {
            new RoomZoneDef { name = "오피스텔 본실", minX = -1.75f, maxX = 1.75f, minZ = -3.05f, maxZ = 1.45f, floorY = 0f, center = new Vector3(0f, 0f, -0.80f), countToSpawn = 1450 },
            new RoomZoneDef { name = "현관 복도",     minX =  0.30f, maxX = 1.75f, minZ =  1.45f, maxZ = 2.35f, floorY = 0f, center = new Vector3(1.05f, 0f, 1.90f), countToSpawn = 350 }
        };

        // Stage 3: 치킨집 주방 (5.5 x 5.0, 문 x=1.55 z=2.70, 서/남/전면 조리대 회피)
        private static readonly RoomZoneDef[] Stage3Rooms =
        {
            new RoomZoneDef { name = "치킨집 주방", minX = -1.55f, maxX = 2.30f, minZ = -1.40f, maxZ = 1.95f, floorY = 0f, center = new Vector3(0.40f, 0f, 0.25f), countToSpawn = 2200 }
        };

        // Stage 4: 육아 전쟁 맞벌이 집 (7.2 x 5.8, 문 x=2.20 z=3.10, 안방 남서 + 화장실 북서)
        private static readonly RoomZoneDef[] Stage4Rooms =
        {
            new RoomZoneDef { name = "거실·놀이매트", minX = -0.35f, maxX =  3.20f, minZ = -2.55f, maxZ = 2.15f, floorY = 0f, center = new Vector3( 1.40f, 0f, -0.20f), countToSpawn = 1900 },
            new RoomZoneDef { name = "안방",          minX = -3.20f, maxX = -0.95f, minZ = -2.55f, maxZ = 0.25f, floorY = 0f, center = new Vector3(-2.10f, 0f, -1.15f), countToSpawn = 1100 }
        };

        // Stage 5: 야근 지옥 스타트업 사무실 (8.5 x 7.0, 문 x=2.60 z=3.70, 대표실 남서 + 탕비실 북서)
        private static readonly RoomZoneDef[] Stage5Rooms =
        {
            new RoomZoneDef { name = "오픈 오피스", minX = -1.00f, maxX =  3.85f, minZ = -3.15f, maxZ =  2.65f, floorY = 0f, center = new Vector3( 1.45f, 0f, -0.25f), countToSpawn = 2700 },
            new RoomZoneDef { name = "대표실",      minX = -3.85f, maxX = -1.55f, minZ = -3.15f, maxZ = -0.80f, floorY = 0f, center = new Vector3(-2.70f, 0f, -1.95f), countToSpawn = 900 },
            new RoomZoneDef { name = "탕비실",      minX = -3.85f, maxX = -2.45f, minZ =  0.20f, maxZ =  3.10f, floorY = 0f, center = new Vector3(-3.15f, 0f,  1.65f), countToSpawn = 600 }
        };

        // Stage 6: 시골집 농막 (4.2 x 3.6, 문 x=1.10 z=2.00)
        private static readonly RoomZoneDef[] Stage6Rooms =
        {
            new RoomZoneDef { name = "시골 농막", minX = -1.75f, maxX = 1.75f, minZ = -1.45f, maxZ = 1.15f, floorY = 0f, center = new Vector3(0f, 0f, -0.15f), countToSpawn = 2400 }
        };

        // Stage 7: 졸부 파티광 3층 복층 펜트하우스 (10 x 8 x 3층 — 1F y=0, 2F y=3.11, 3F y=6.11!)
        private static readonly RoomZoneDef[] Stage7Rooms =
        {
            new RoomZoneDef { name = "1층 거실·바",     minX = -2.35f, maxX = 4.35f, minZ = -3.55f, maxZ = 2.75f, floorY = 0.00f, center = new Vector3( 1.00f, 0.00f, -0.40f), countToSpawn = 2200 },
            new RoomZoneDef { name = "2층 파티홀",      minX = -2.35f, maxX = 2.20f, minZ = -3.55f, maxZ = 3.45f, floorY = 3.11f, center = new Vector3( 0.00f, 3.11f, -0.05f), countToSpawn = 2000 },
            new RoomZoneDef { name = "3층 VIP 금고방",  minX = -4.55f, maxX = 2.35f, minZ = -3.55f, maxZ = 3.45f, floorY = 6.11f, center = new Vector3(-1.10f, 6.11f, -0.05f), countToSpawn = 1800 }
        };

        private static RoomZoneDef[] GetRoomsForStage(int stage) => stage switch
        {
            1 => Stage1Rooms,
            2 => Stage2Rooms,
            3 => Stage3Rooms,
            4 => Stage4Rooms,
            5 => Stage5Rooms,
            6 => Stage6Rooms,
            7 => Stage7Rooms,
            _ => Stage1Rooms
        };

        private static int PackCell(int cx, int cz) => (cx + 1024) * 4096 + (cz + 1024);
        private static int CellKeyForPos(Vector3 p) => PackCell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));

        private void Awake()
        {
            // [v1.5.1] 여기서 Stage 1 규격(1,650개)을 미리 스폰하지 않는다.
            //  - Stage 2~7 씬에서는 매니저가 활성화하자마자 실제 스테이지 규격으로 다시 스폰하므로,
            //    예전에는 같은 프레임에 1,650개를 만들었다 버리는 이중 작업이 있었다(진입 히치의 한 몫).
            //  - 스폰은 Start(또는 매니저)에서 스테이지당 정확히 한 번만 한다.
        }

        private System.Collections.IEnumerator Start()
        {
            if (meshTypes.Count == 0) BuildProceduralTypes();
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            EnsureRenderBuffers();

            // [v1.5.1] 매니저가 자기 Start에서 스테이지 규격으로 스폰하므로, 그 결정을 기다리기 위해 1프레임 양보한다.
            //  (예전에는 스웜 Awake에서 Stage 1 규격으로 먼저 채운 뒤 바로 버려서 진입 프레임이 두 배로 무거웠다)
            yield return null;

            if (!SpawnInProgress && (_units == null || _unitCount == 0))
                SpawnForStage(manager != null ? manager.CurrentStage : 1);
        }

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _cleanedCount = 0;
            _grid.Clear();
            _activeIndices.Clear();
            _selectedIndices.Clear();
            _selectedSet.Clear();
            _lockedIndices.Clear();
            MaxSelectedCharge01 = 0f;
            _staticDirty = true;
            EnsureRenderBuffers();
        }

        private void EnsureRenderBuffers()
        {
            int types = Mathf.Max(1, meshTypes.Count);
            if (_staticChunks == null || _staticChunks.Length != types)
            {
                _staticChunks = new List<Matrix4x4[]>[types];
                _staticCounts = new int[types];
                _dynChunks = new List<Matrix4x4[]>[types];
                _dynCounts = new int[types];
                _detectChunks = new List<Matrix4x4[]>[types];
                _detectCounts = new int[types];
                _renderParams = new RenderParams[types];
                _outlineMatrices = new Matrix4x4[types][];
                _outlineCounts = new int[types];

                for (int t = 0; t < types; t++)
                {
                    _staticChunks[t] = new List<Matrix4x4[]> { new Matrix4x4[1023] };
                    _dynChunks[t] = new List<Matrix4x4[]> { new Matrix4x4[1023] };
                    _detectChunks[t] = new List<Matrix4x4[]> { new Matrix4x4[1023] };
                    _outlineMatrices[t] = new Matrix4x4[1023];
                }
            }

            for (int t = 0; t < meshTypes.Count; t++)
            {
                if (meshTypes[t].material != null)
                {
                    meshTypes[t].material.enableInstancing = true;
                    _renderParams[t] = new RenderParams(meshTypes[t].material)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
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
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
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
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
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

        public void BuildProceduralTypes()
        {
            if (meshTypes.Count > 0) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;

            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh cyl = c.GetComponent<MeshFilter>().sharedMesh; Destroy(c);
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh box = b.GetComponent<MeshFilter>().sharedMesh; Destroy(b);
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sph = s.GetComponent<MeshFilter>().sharedMesh; Destroy(s);

            AddProc(lit, "찌그러진 캔", cyl, new Color(0.65f, 0.66f, 0.65f), 0.52f, 0.14f, new Vector3(0f, 0f, 90f));
            AddProc(lit, "택배 박스", box, new Color(0.60f, 0.45f, 0.30f), 0.12f, 0.24f, Vector3.zero);
            AddProc(lit, "페트병", cyl, new Color(0.72f, 0.80f, 0.84f), 0.60f, 0.18f, new Vector3(90f, 0f, 0f));
            AddProc(lit, "비닐봉투", sph, new Color(0.14f, 0.14f, 0.16f), 0.36f, 0.19f, Vector3.zero);
            AddProc(lit, "구겨진 종이", sph, new Color(0.82f, 0.79f, 0.74f), 0.10f, 0.13f, Vector3.zero);
        }

        private void AddProc(Shader lit, string label, Mesh mesh, Color tint, float smooth, float targetDim, Vector3 euler)
        {
            Material mat = new Material(lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            mat.color = tint;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            mat.enableInstancing = true;
            Vector3 sz = mesh.bounds.size;
            float maxDim = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            meshTypes.Add(new TrashMeshType { label = label, mesh = mesh, material = mat, normalizedScale = maxDim > 0.001f ? targetDim / maxDim : 1f, baseEulerOffset = euler });
        }

        /// <summary>
        /// 스테이지 1~7 각 방 규격(3층 복층 펜트하우스 층별 높이 포함)에 맞춰 무렉 GPU-Instanced 쓰레기 스폰!
        /// </summary>
        public void SpawnForStage(int stage)
        {
            if (meshTypes.Count == 0) BuildProceduralTypes();
            if (meshTypes.Count == 0) return;

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            _currentStageLayout = Mathf.Clamp(stage, 1, 7);
            _rooms = GetRoomsForStage(_currentStageLayout);
            _roomRemaining = new int[_rooms.Length];
            _roomInitial = new int[_rooms.Length];
            _roomClearedFlags = new bool[_rooms.Length];
            ClearedRoomCount = 0;

            int totalSpawn = 0;
            for (int r = 0; r < _rooms.Length; r++) totalSpawn += _rooms[r].countToSpawn;
            InitializeCapacity(totalSpawn + 900);
            SpawnInProgress = true;

            if (!isActiveAndEnabled)
            {
                // 비활성 상태에서는 코루틴을 돌릴 수 없으므로 즉시 채운다(결과는 기존과 동일).
                IEnumerator it = CoSpawnStage(false);
                while (it.MoveNext()) { }
                return;
            }

            _spawnRoutine = StartCoroutine(CoSpawnStage(true));
        }

        /// <summary>
        /// [v1.5.1] 스폰 히치 완화: 6,000개(Stage 7)를 한 프레임에 몰아 만들던 것을
        /// 프레임 예산(SpawnFrameBudgetSeconds) 단위로 나눠 채운다. 유닛 좌표·개수·난수 순서는
        /// v1.5와 동일한 계산식이라 게임 로직·배치는 그대로 유지된다.
        /// </summary>
        private IEnumerator CoSpawnStage(bool frameSpread)
        {
            float stageStart = Time.realtimeSinceStartup;

            for (int r = 0; r < _rooms.Length; r++)
            {
                RoomZoneDef room = _rooms[r];
                int n = room.countToSpawn;
                byte roomIdx = (byte)r;
                float baseY = room.floorY;

                Vector3[] mounds =
                {
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.30f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.35f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.70f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.40f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.45f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.72f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.65f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.75f))
                };

                float spanMin = Mathf.Min(room.maxX - room.minX, room.maxZ - room.minZ);
                float maxRad = Mathf.Clamp(spanMin * 0.36f, 0.9f, 2.0f);

                int i = 0;
                while (i < n)
                {
                    float frameStart = Time.realtimeSinceStartup;
                    do
                    {
                        SpawnOneUnit(room, i, n, baseY, maxRad, mounds, roomIdx);
                        i++;
                    }
                    while (i < n && frameSpread && Time.realtimeSinceStartup - frameStart < SpawnFrameBudgetSeconds);

                    _staticDirty = true;
                    if (frameSpread) yield return null;
                }
            }

            _staticDirty = true;
            SpawnInProgress = false;
            _spawnRoutine = null;
            Debug.Log($"[TrashCleaner3D] Stage {_currentStageLayout} 스폰 완료: {_unitCount:N0}개 · {(Time.realtimeSinceStartup - stageStart) * 1000f:0}ms");
        }

        private void SpawnOneUnit(RoomZoneDef room, int i, int n, float baseY, float maxRad, Vector3[] mounds, byte roomIdx)
        {
            Vector3 pos;
            if (i < n * 0.72f)
            {
                Vector3 m = mounds[i % mounds.Length];
                Vector2 off = Random.insideUnitCircle * Random.Range(0.08f, maxRad);
                float dist01 = Mathf.Clamp01(off.magnitude / maxRad);
                float yStack = (1f - dist01) * (1f - dist01) * Random.Range(0.05f, _currentStageLayout >= 5 ? 0.78f : 0.62f);
                pos = new Vector3(
                    Mathf.Clamp(m.x + off.x, room.minX, room.maxX),
                    baseY + 0.055f + yStack,
                    Mathf.Clamp(m.z + off.y, room.minZ, room.maxZ)
                );
            }
            else
            {
                pos = new Vector3(
                    Random.Range(room.minX, room.maxX),
                    baseY + 0.055f + Random.Range(0f, 0.14f),
                    Random.Range(room.minZ, room.maxZ)
                );
            }

            // 1단계 원룸 가구 및 현관문 스윙 공간 회피
            if (_currentStageLayout == 1)
            {
                if (pos.x > 1.35f && pos.x < 2.95f && pos.z > 2.60f) pos.z -= 1.15f;
                if (pos.x < -1.95f && pos.z < -1.10f) pos.y += 0.56f; // 침대 위
                if (pos.x > -1.25f && pos.x < 1.35f && pos.z > 2.65f) pos.z = Random.Range(1.40f, 2.55f); // 냉장고·싱크대 앞
                if (pos.x > -0.45f && pos.x < 1.45f && pos.z < -2.65f) pos.z = Random.Range(-2.55f, -1.35f); // PC 책상 앞
            }

            int tIdx = (i < n * 0.45f) ? ((i / 6) % meshTypes.Count) : Random.Range(0, meshTypes.Count);
            AddUnit(pos, pos, false, tIdx, roomIdx);
        }

        public void ResetSwarm(int stageOrCount)
        {
            int st = stageOrCount <= 10 ? Mathf.Max(1, stageOrCount) : (manager != null ? manager.CurrentStage : 1);
            SpawnForStage(st);
        }

        public void SpawnBurstFountain(Vector3 origin, int count)
        {
            if (meshTypes.Count == 0) return;
            byte rIdx = FindRoomIndexForPos(origin);
            RoomZoneDef room = rIdx < _rooms.Length ? _rooms[rIdx] : Stage1Rooms[0];

            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.28f, 1.45f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, room.minX, room.maxX),
                    0.06f,
                    Mathf.Clamp(origin.z + off.y, room.minZ, room.maxZ)
                );
                AddUnit(origin + Vector3.up * 0.35f, land, true, Random.Range(0, meshTypes.Count), rIdx);
            }
            _staticDirty = true;
        }

        private byte FindRoomIndexForPos(Vector3 p)
        {
            for (int r = 0; r < _rooms.Length; r++)
            {
                if (p.x >= _rooms[r].minX - 0.5f && p.x <= _rooms[r].maxX + 0.5f &&
                    p.z >= _rooms[r].minZ - 0.5f && p.z <= _rooms[r].maxZ + 0.5f)
                    return (byte)r;
            }
            return 0;
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation, int tIdx, byte roomIdx)
        {
            if (_unitCount >= _units.Length) return;
            tIdx = Mathf.Clamp(tIdx, 0, meshTypes.Count - 1);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.86f, 1.22f);
            Quaternion baseOffset = Quaternion.Euler(meshTypes[tIdx].baseEulerOffset);
            Quaternion rot = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f)) * baseOffset;

            int idx = _unitCount++;
            int cellKey = CellKeyForPos(endPos);

            _units[idx] = new TrashUnit
            {
                pos = startPos,
                rot = rot,
                baseScale = baseS,
                curScale = baseS,
                typeIdx = tIdx,
                state = popAnimation ? (byte)1 : (byte)0,
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                value = 2,
                isCritBonus = false,
                holdCharge = 0f,
                suckDur = 0.24f,
                arcHeight = 0.45f,
                roomIdx = roomIdx,
                cachedMatrix = Matrix4x4.TRS(startPos, rot, Vector3.one * baseS),
                gridKey = cellKey,
                inActiveList = popAnimation
            };

            if (!_grid.TryGetValue(cellKey, out var bucket))
            {
                bucket = new List<int>(24);
                _grid[cellKey] = bucket;
            }
            bucket.Add(idx);

            if (popAnimation) _activeIndices.Add(idx);

            if (roomIdx < _roomRemaining.Length)
            {
                _roomRemaining[roomIdx]++;
                _roomInitial[roomIdx]++;
            }
        }

        private void ActivateUnit(int idx)
        {
            if (!_units[idx].inActiveList)
            {
                _units[idx].inActiveList = true;
                _activeIndices.Add(idx);
                _staticDirty = true;
            }
        }

        private void NotifyUnitCleaned(byte rIdx)
        {
            if (rIdx >= _roomRemaining.Length) return;
            if (_roomRemaining[rIdx] > 0) _roomRemaining[rIdx]--;
            if (_roomRemaining[rIdx] == 0 && !_roomClearedFlags[rIdx])
            {
                _roomClearedFlags[rIdx] = true;
                ClearedRoomCount++;
                RoomCleared?.Invoke(rIdx, _rooms[rIdx].name, _rooms[rIdx].center);
            }
        }

        public void SetCursorRing(bool visible, Vector3 center, float radius)
        {
            _cursorRingVisible = visible;
            _cursorRingCenter = center;
            _cursorRingRadius = radius;
        }

        /// <summary>
        /// 2D Spatial Hash Grid O(1) 탐색 + HOLD 락온 유지! (8,000개에서도 0.02ms!)
        /// </summary>
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

            // 홀드 중 기존 락온 유지 (순수 수평 XZ 반경 * 1.15 여유)
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
                    d.y = 0f;
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

            // Spatial Hash Grid 주변 셀만 O(1) 스캔!
            if (_selectedIndices.Count < maxSelectCount)
            {
                _candidates.Clear();
                int minCx = Mathf.FloorToInt(aimPoint.x - cursorRadius);
                int maxCx = Mathf.FloorToInt(aimPoint.x + cursorRadius);
                int minCz = Mathf.FloorToInt(aimPoint.z - cursorRadius);
                int maxCz = Mathf.FloorToInt(aimPoint.z + cursorRadius);

                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cz = minCz; cz <= maxCz; cz++)
                    {
                        if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                        for (int b = 0; b < bucket.Count; b++)
                        {
                            int idx = bucket[b];
                            if (_units[idx].state != 0 || _selectedSet.Contains(idx)) continue;
                            if (Mathf.Abs(_units[idx].pos.y - aimPoint.y) > 1.45f) continue;
                            Vector3 d = _units[idx].pos - aimPoint;
                            d.y = 0f;
                            float dSq = d.sqrMagnitude;
                            if (dSq <= rSq) _candidates.Add((idx, dSq));
                        }
                    }
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
            Vector3 destPos = suckDest != null ? suckDest.position : Vector3.zero;
            float rate = 1f / Mathf.Max(0.04f, holdDurationSeconds);
            int picked = 0;
            float maxC = 0f;

            bool rolledCrit = critChance > 0f && Random.value < critChance;

            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0) continue;

                ActivateUnit(idx);
                _units[idx].holdCharge = Mathf.Min(1f, _units[idx].holdCharge + dt * rate);
                if (_units[idx].holdCharge > maxC) maxC = _units[idx].holdCharge;

                if (_units[idx].holdCharge >= 1f)
                {
                    if (picked == 0) fxOrigin = _units[idx].pos;
                    StartUnitSuck(idx, 2, destPos, rolledCrit);
                    picked++;
                    if (rolledCrit) didCrit = true;
                }
            }

            MaxSelectedCharge01 = picked > 0 ? 0f : maxC;
            if (picked > 0) _staticDirty = true;
            return picked;
        }

        private void StartUnitSuck(int idx, byte newState, Vector3 destPos, bool critBonus)
        {
            ActivateUnit(idx);
            _units[idx].state = newState;
            _units[idx].timer = 0f;
            _units[idx].holdCharge = 0f;
            _units[idx].startPos = _units[idx].pos;
            _units[idx].isCritBonus = critBonus;
            float dist = Vector3.Distance(_units[idx].pos, destPos);
            float spd = (manager != null && manager.ToolTier >= 2) ? 14.5f : 12.0f;
            _units[idx].suckDur = Mathf.Clamp(dist / spd, 0.16f, 0.34f);
            _units[idx].arcHeight = Mathf.Clamp(0.28f + dist * 0.12f, 0.28f, 0.72f);
        }

        public bool TryFindNearestIdlePos(Vector3 fromPos, out Vector3 foundPos)
        {
            foundPos = Vector3.zero;
            // 먼저 주변 반경 3m 셀에서 빠르게 탐색
            float best = float.MaxValue;
            int bestIdx = -1;
            int minCx = Mathf.FloorToInt(fromPos.x - 3f), maxCx = Mathf.FloorToInt(fromPos.x + 3f);
            int minCz = Mathf.FloorToInt(fromPos.z - 3f), maxCz = Mathf.FloorToInt(fromPos.z + 3f);
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int i = bucket[b];
                        if (_units[i].state != 0) continue;
                        float dSq = (_units[i].pos - fromPos).sqrMagnitude;
                        if (dSq < best) { best = dSq; bestIdx = i; }
                    }
                }
            }
            if (bestIdx >= 0) { foundPos = _units[bestIdx].pos; return true; }

            // 주변에 없으면 전체 샘플링
            int step = Mathf.Max(1, _unitCount / 180);
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
            int minCx = Mathf.FloorToInt(robotPos.x - radius), maxCx = Mathf.FloorToInt(robotPos.x + radius);
            int minCz = Mathf.FloorToInt(robotPos.z - radius), maxCz = Mathf.FloorToInt(robotPos.z + radius);

            for (int cx = minCx; cx <= maxCx && eaten < maxEat; cx++)
            {
                for (int cz = minCz; cz <= maxCz && eaten < maxEat; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count && eaten < maxEat; b++)
                    {
                        int i = bucket[b];
                        if (_units[i].state != 0) continue;
                        Vector3 d = _units[i].pos - robotPos;
                        d.y *= 0.4f;
                        if (d.sqrMagnitude <= rSq)
                        {
                            StartUnitSuck(i, 4, robotPos, false);
                            eaten++;
                        }
                    }
                }
            }
            if (eaten > 0) _staticDirty = true;
            return eaten;
        }

        private static void PushChunk(List<Matrix4x4[]>[] chunks, int[] counts, int tIdx, Matrix4x4 mtx)
        {
            int idx = counts[tIdx]++;
            int cIdx = idx / 1023;
            int lIdx = idx % 1023;
            while (chunks[tIdx].Count <= cIdx) chunks[tIdx].Add(new Matrix4x4[1023]);
            chunks[tIdx][cIdx][lIdx] = mtx;
        }

        private void RebuildStaticChunks()
        {
            _staticDirty = false;
            for (int t = 0; t < _staticCounts.Length; t++) _staticCounts[t] = 0;
            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0 || _units[i].inActiveList) continue;
                PushChunk(_staticChunks, _staticCounts, _units[i].typeIdx, _units[i].cachedMatrix);
            }
        }

        private void Update()
        {
            if (_unitCount == 0 || meshTypes.Count == 0 || _staticChunks == null) return;

            float dt = Time.deltaTime;
            float now = Time.time;
            Vector3 playerDest = _suckDestination != null ? _suckDestination.position : Vector3.zero;
            Vector3 botDest = _robotSuckDest != null ? _robotSuckDest.position : Vector3.zero;

            for (int t = 0; t < _dynCounts.Length; t++)
            {
                _dynCounts[t] = 0;
                _outlineCounts[t] = 0;
                _detectCounts[t] = 0;
            }

            // 1) 동적(진동/팝핑/흡입 중) 유닛만 업데이트! (8,000개 중 보통 0~30개만 순회!)
            for (int k = _activeIndices.Count - 1; k >= 0; k--)
            {
                int i = _activeIndices[k];
                byte st = _units[i].state;

                if (st == 3)
                {
                    _units[i].inActiveList = false;
                    _activeIndices.RemoveAt(k);
                    continue;
                }

                if (st == 0)
                {
                    if (!_isHoldingNow || !_selectedSet.Contains(i))
                    {
                        _units[i].holdCharge = Mathf.MoveTowards(_units[i].holdCharge, 0f, dt * 2.6f);
                    }
                    float ch = _units[i].holdCharge;
                    if (ch <= 0.005f)
                    {
                        _units[i].holdCharge = 0f;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        _staticDirty = true;
                        continue;
                    }

                    // 진동 연출
                    float freq = 52f + ch * 38f;
                    float amp = 0.012f + ch * 0.032f;
                    float seed = i * 1.73f;
                    Vector3 rPos = _units[i].pos + new Vector3(
                        Mathf.Sin(now * freq + seed) * amp,
                        ch * 0.09f + Mathf.Abs(Mathf.Sin(now * freq * 0.8f + seed)) * (0.025f * ch),
                        Mathf.Cos(now * freq * 1.13f + seed) * amp
                    );
                    Quaternion rRot = _units[i].rot * Quaternion.Euler(
                        Mathf.Sin(now * freq * 0.9f + seed) * (14f * ch),
                        Mathf.Cos(now * freq * 0.7f + seed) * (18f * ch),
                        Mathf.Sin(now * freq * 1.05f + seed) * (14f * ch)
                    );
                    float rScale = _units[i].curScale * (1f + ch * 0.14f);
                    Matrix4x4 mtx = Matrix4x4.TRS(rPos, rRot, Vector3.one * rScale);
                    PushChunk(_dynChunks, _dynCounts, _units[i].typeIdx, mtx);

                    if (_selectedSet.Contains(i))
                    {
                        int oc = _outlineCounts[_units[i].typeIdx];
                        if (oc < 1023) { _outlineMatrices[_units[i].typeIdx][oc] = mtx; _outlineCounts[_units[i].typeIdx] = oc + 1; }
                    }
                }
                else if (st == 1)
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
                        _units[i].cachedMatrix = Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale);
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        _staticDirty = true;
                    }
                    else
                    {
                        PushChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(cur, _units[i].rot, Vector3.one * _units[i].curScale));
                    }
                }
                else if (st == 2 || st == 4)
                {
                    Vector3 dest = st == 4 ? botDest : playerDest;
                    _units[i].timer += dt;
                    float u = Mathf.Clamp01(_units[i].timer / Mathf.Max(0.08f, _units[i].suckDur));
                    float ease = u * u * (3f - 2f * u);
                    Vector3 cur = Vector3.Lerp(_units[i].startPos, dest, ease);
                    cur.y += Mathf.Sin(u * Mathf.PI) * _units[i].arcHeight;
                    _units[i].pos = cur;
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 680f * dt, 300f * dt);
                    _units[i].curScale = _units[i].baseScale * Mathf.Clamp01(1f - u * u * 0.92f);

                    if (u >= 1f)
                    {
                        _units[i].state = 3;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        _cleanedCount++;
                        NotifyUnitCleaned(_units[i].roomIdx);
                        if (st == 2 && manager != null)
                        {
                            int val = _units[i].isCritBonus ? _units[i].value * 2 : _units[i].value;
                            string label = _units[i].typeIdx < meshTypes.Count ? meshTypes[_units[i].typeIdx].label : "쓰레기";
                            manager.OnInstancedTrashCollected(val, _units[i].typeIdx, label);
                        }
                    }
                    else
                    {
                        PushChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(cur, _units[i].rot, Vector3.one * _units[i].curScale));
                    }
                }
            }

            // 정적 청크는 변경이 생겼을 때만 재구축!
            if (_staticDirty) RebuildStaticChunks();

            // 호버 선택된 정적 유닛 하얀 외곽선 수집
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0 || _units[idx].inActiveList) continue;
                int tIdx = _units[idx].typeIdx;
                int oc = _outlineCounts[tIdx];
                if (oc < 1023)
                {
                    _outlineMatrices[tIdx][oc] = _units[idx].cachedMatrix;
                    _outlineCounts[tIdx] = oc + 1;
                }
            }

            // 2) 정적 8,000+ 멀티청크 GPU Instanced 렌더!
            RenderChunked(_staticChunks, _staticCounts, _renderParams);

            // 3) 동적 유닛 GPU Instanced 렌더!
            RenderChunked(_dynChunks, _dynCounts, _renderParams);

            // 4) 호버 하얀 테두리
            if (_outlineMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int ocnt = _outlineCounts[t];
                    if (ocnt > 0 && meshTypes[t].mesh != null)
                        Graphics.RenderMeshInstanced(_outlineRenderParams, meshTypes[t].mesh, 0, _outlineMatrices[t], ocnt);
                }
            }

            // 5) 바닥 커서 링 (다층 바닥 높이 대응: 1F/2F/3F 모두 정확히 밀착!)
            if (_auraRingMesh != null)
            {
                if (_cursorRingVisible && _cursorRingMat != null && _cursorRingRadius > 0.04f)
                {
                    Color c = _isHoldingNow ? new Color(1.0f, 0.88f, 0.28f, 0.78f) : new Color(1.0f, 0.96f, 0.86f, 0.42f);
                    _cursorRingMat.SetColor("_BaseColor", c);
                    Vector3 rPos = new Vector3(_cursorRingCenter.x, _cursorRingCenter.y + 0.03f, _cursorRingCenter.z);
                    Graphics.DrawMesh(_auraRingMesh, Matrix4x4.TRS(rPos, Quaternion.identity, new Vector3(_cursorRingRadius, 1f, _cursorRingRadius)), _cursorRingMat, 0);
                }
                if (_auraVisible && _isHoldingNow && _auraRingMat != null && _auraRadius > 0.04f)
                {
                    int tier = manager != null ? manager.ToolTier : 0;
                    Color ac = tier >= 2 ? new Color(0.28f, 0.94f, 1.0f, 0.72f) : tier == 1 ? new Color(0.45f, 0.96f, 0.56f, 0.65f) : new Color(1.0f, 0.88f, 0.30f, 0.72f);
                    _auraRingMat.SetColor("_BaseColor", ac);
                    float rScale = _auraRadius * (1f - MaxSelectedCharge01 * 0.10f);
                    Vector3 rPos = new Vector3(_auraCenter.x, _auraCenter.y + 0.035f, _auraCenter.z);
                    Graphics.DrawMesh(_auraRingMesh, Matrix4x4.TRS(rPos, Quaternion.Euler(0f, now * 65f, 0f), new Vector3(rScale, 1f, rScale)), _auraRingMat, 0);
                }
            }
        }

        private void RenderChunked(List<Matrix4x4[]>[] chunks, int[] counts, RenderParams[] rps)
        {
            for (int t = 0; t < meshTypes.Count; t++)
            {
                int total = counts[t];
                if (total <= 0 || meshTypes[t].mesh == null || meshTypes[t].material == null) continue;
                int full = total / 1023;
                int rem = total % 1023;
                for (int c = 0; c < full; c++)
                {
                    Graphics.RenderMeshInstanced(rps[t], meshTypes[t].mesh, 0, chunks[t][c], 1023);
                }
                if (rem > 0)
                {
                    Graphics.RenderMeshInstanced(rps[t], meshTypes[t].mesh, 0, chunks[t][full], rem);
                }
            }
        }
    }
}
