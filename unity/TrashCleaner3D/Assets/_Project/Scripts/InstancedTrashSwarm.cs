using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.7 초고속 GPU-Instanced 쓰레기 엔진 (WebGL 60fps 대응):
    /// 1) 멀티청크 GPU Instancing(1023개 제한 해제): 웨이브 6,500개도 드로우콜 ~35개로 렌더!
    /// 2) 정적/동적 분리 + Dirty-Flag 캐시(0.18초 스로틀): 멈춰있는 수천 개는 매 프레임 재계산 0회!
    /// 3) 2D Spatial Hash Grid (1m 셀): 6,500개 중 커서 반경/로봇 주변 셀만 O(1) 탐색!
    /// 4) [v1.7] 청소된 슬롯 재사용(_freeSlots) → 누적 13만 개 물량에도 배열·순회 비용은 동시 생존 수만큼만.
    /// 4) 바닥 원형 색 매트 제거 → 실제 방 단위(1단계 원룸 1방 / 2단계 거실·주방·안방·화장실 4방) 클리어 추적!
    /// 5) HOLD 락온 유지(순수 수평 XZ 반경) + 포물선 흡입 연출!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public const int Stage1SwarmCount = 960;
        public const int Stage2SwarmCount = 2700;

        // ===== v1.7 : 스테이지별 총 쓰레기 물량과 다중 웨이브 =====
        // 한 번에 화면에 뿌리는 양(웨이브)은 1,200~5,000개로 제한해 WebGL 프레임 예산을 지키고,
        // 웨이브를 계속 공급해 4시간 캠페인에 맞는 물량(총 7.7만 개)을 만든다.
        // countToSpawn 은 '웨이브 1회당' 개수다.
        public static readonly int[] StageWaveCounts = { 0, 1, 4, 6, 7, 6, 16, 20 };

        public int StageTotalItems { get; private set; }
        public int WaveIndex { get; private set; }
        public int WaveCount { get; private set; }
        public bool AllWavesSpawned => WaveIndex >= WaveCount;
        private const int SpawnCapacityMargin = 1400;

        // [v1.8] 스테이지 진입 시 고정되는 웨이브당 개수·웨이브 수(강화 '물량·밀도' 노드 반영)
        private int _perWaveCount;
        private int _waveCountFixed;
        private float _densityMult = 1f;
        private bool _megaDump;
        private readonly Vector3[] _bhCenters = new Vector3[3];
        private int _bhCount;
        private float _bhRadius;
        private float _bhSpin;
        private float _bhDps;

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
            public byte state;       // 0:Idle, 1:Popping, 2:SuckingToPlayer, 3:Cleaned, 4:SuckingToRobot, 5:ToOrb, 6:OrbitBlackHole
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
            public byte heavyClass;   // [v1.8] 0 일반, 1 캔·병, 2 전자, 3 대형, 4 금고
            public float hp;          // [v1.8] 남은 내구도(일반은 0)
            public float hpMax;
            public float orbitAngle;  // [v1.8] 블랙홀 궤도 각도
            public byte orbitIdx;     // [v1.8] 어느 블랙홀 중심을 도는지
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
        private int _unitCount;          // 배열 하이워터마크(순회 범위)
        private int _spawnedCount;       // 누적 스폰 수(웨이브 포함 진행률 계산용)
        private int _cleanedCount;       // 누적 청소 수
        private readonly List<int> _freeSlots = new List<int>(4096);   // 청소된 슬롯 재사용(메모리·순회 비용 억제)
        private int _currentStageLayout = 1;
        private Transform _suckDestination;
        private Transform _robotSuckDest;

        // 2D Spatial Hash Grid (1.0m cells) -> O(1) lookup even at 6,500+ units!
        private readonly Dictionary<int, List<int>> _grid = new Dictionary<int, List<int>>(512);
        // Active-only update list (only popping / sucking / vibrating units!)
        private readonly List<int> _activeIndices = new List<int>(128);

        // Multi-chunk Static & Dynamic GPU Instancing buffers
        private List<Matrix4x4[]>[] _staticChunks;
        private int[] _staticCounts;
        private bool _staticDirty = true;
        private float _staticRebuildTimer;
        private const float StaticRebuildInterval = 0.18f;   // v1.7: 3만 개 시대의 정적 청크 재구축 스로틀

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
        public int SpawnedCount => _spawnedCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _spawnedCount - _cleanedCount);

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

        // Stage 1: +30% 확장 원룸 (9.36 x 7.80) — 튜토리얼은 그대로
        private static readonly RoomZoneDef[] Stage1Rooms =
        {
            new RoomZoneDef { name = "반지하 원룸", minX = -4.35f, maxX = 4.35f, minZ = -3.60f, maxZ = 3.55f, floorY = 0f, center = new Vector3(0f, 0f, 0f), countToSpawn = 960 }
        };

        // Stage 2: 택배 중독 오피스텔 (4.2 x 6.8) — 방 끝까지 + 화장실·현관 복도 (사용 면적 +31%)
        private static readonly RoomZoneDef[] Stage2Rooms =
        {
            new RoomZoneDef { name = "오피스텔 본실", minX = -2.00f, maxX =  2.00f, minZ = -3.25f, maxZ = 1.35f, floorY = 0f, center = new Vector3(0f, 0f, -0.95f), countToSpawn = 550 },
            new RoomZoneDef { name = "화장실",        minX = -1.95f, maxX = -0.75f, minZ =  1.85f, maxZ = 3.25f, floorY = 0f, center = new Vector3(-1.35f, 0f, 2.55f), countToSpawn = 40 },
            new RoomZoneDef { name = "현관 복도",     minX =  0.35f, maxX =  1.95f, minZ =  1.55f, maxZ = 3.25f, floorY = 0f, center = new Vector3(1.15f, 0f, 2.40f), countToSpawn = 85 }
        };

        // Stage 3: 치킨집 주방 (5.5 x 5.0) — 주방 바닥 끝까지 + 홀/카운터 앞 (사용 면적 +12%)
        private static readonly RoomZoneDef[] Stage3Rooms =
        {
            new RoomZoneDef { name = "치킨집 주방", minX = -1.70f, maxX = 2.55f, minZ = -1.45f, maxZ = 1.10f, floorY = 0f, center = new Vector3(0.42f, 0f, -0.18f), countToSpawn = 975 },
            new RoomZoneDef { name = "홀·카운터 앞", minX = -0.20f, maxX = 2.55f, minZ =  1.00f, maxZ = 2.35f, floorY = 0f, center = new Vector3(1.20f, 0f, 1.68f), countToSpawn = 195 }
        };

        // Stage 4: 육아 전쟁 맞벌이 집 (7.2 x 5.8) — 거실·안방 끝까지 + 욕실 (사용 면적 +80%)
        private static readonly RoomZoneDef[] Stage4Rooms =
        {
            new RoomZoneDef { name = "거실·놀이매트", minX = -0.55f, maxX =  3.45f, minZ = -2.75f, maxZ = 2.35f, floorY = 0f, center = new Vector3(1.45f, 0f, -0.20f), countToSpawn = 2040 },
            new RoomZoneDef { name = "안방",          minX = -3.45f, maxX = -0.75f, minZ = -2.75f, maxZ = 0.40f, floorY = 0f, center = new Vector3(-2.10f, 0f, -1.18f), countToSpawn = 460 },
            new RoomZoneDef { name = "욕실",          minX = -3.45f, maxX = -1.80f, minZ =  1.15f, maxZ = 2.80f, floorY = 0f, center = new Vector3(-2.60f, 0f, 1.98f), countToSpawn = 170 }
        };

        // Stage 5: 야근 지옥 스타트업 사무실 (8.5 x 7.0) — 오픈 오피스 끝까지 + 대표실·탕비실 (사용 면적 +48%)
        private static readonly RoomZoneDef[] Stage5Rooms =
        {
            new RoomZoneDef { name = "오픈 오피스", minX = -1.20f, maxX =  4.05f, minZ = -3.35f, maxZ =  3.30f, floorY = 0f, center = new Vector3(1.42f, 0f, -0.02f), countToSpawn = 3150 },
            new RoomZoneDef { name = "대표실",      minX = -4.10f, maxX = -1.45f, minZ = -3.35f, maxZ = -0.65f, floorY = 0f, center = new Vector3(-2.78f, 0f, -2.00f), countToSpawn = 540 },
            new RoomZoneDef { name = "탕비실",      minX = -4.10f, maxX = -2.40f, minZ =  0.75f, maxZ =  3.30f, floorY = 0f, center = new Vector3(-3.25f, 0f, 2.02f), countToSpawn = 310 }
        };

        // Stage 6: 시골집 농막 (4.2 x 3.6) — 좁은 방을 끝까지 (사용 면적 +27%) · 최강 악취
        private static readonly RoomZoneDef[] Stage6Rooms =
        {
            new RoomZoneDef { name = "시골 농막", minX = -1.95f, maxX = 1.95f, minZ = -1.65f, maxZ = 1.65f, floorY = 0f, center = new Vector3(0f, 0f, 0f), countToSpawn = 1130 }
        };

        // Stage 7: 졸부 파티광 3층 복층 펜트하우스 (10 x 8 x 3층) — 층별 바닥 전체 + 2층 욕실·3층 금고방 (사용 면적 +55%)
        private static readonly RoomZoneDef[] Stage7Rooms =
        {
            new RoomZoneDef { name = "1층 거실·바",     minX = -4.80f, maxX =  3.00f, minZ = -3.80f, maxZ = 3.80f, floorY = 0.00f, center = new Vector3(-0.90f, 0.00f, 0.00f), countToSpawn = 2130 },
            new RoomZoneDef { name = "2층 파티홀",      minX = -2.70f, maxX =  2.30f, minZ = -3.80f, maxZ = 3.80f, floorY = 3.11f, center = new Vector3(-0.20f, 3.11f, 0.00f), countToSpawn = 1400 },
            new RoomZoneDef { name = "2층 서쪽 랜딩",   minX = -4.80f, maxX = -2.90f, minZ =  0.70f, maxZ = 3.90f, floorY = 3.11f, center = new Vector3(-3.85f, 3.11f, 2.30f), countToSpawn = 310 },
            new RoomZoneDef { name = "2층 욕실",        minX =  2.50f, maxX =  4.70f, minZ =  1.70f, maxZ = 3.80f, floorY = 3.11f, center = new Vector3(3.60f, 3.11f, 2.75f), countToSpawn = 180 },
            new RoomZoneDef { name = "3층 VIP홀",       minX = -1.70f, maxX =  2.70f, minZ = -3.80f, maxZ = 3.80f, floorY = 6.11f, center = new Vector3(0.50f, 6.11f, 0.00f), countToSpawn = 1400 },
            new RoomZoneDef { name = "3층 금고방",      minX = -4.80f, maxX = -2.00f, minZ = -3.80f, maxZ = -1.10f, floorY = 6.11f, center = new Vector3(-3.40f, 6.11f, -2.45f), countToSpawn = 1080 }
        };

        /// <summary>v1.6: 스테이지별 실제 플레이 가능 바닥 구역(다른 시스템의 소품 배치에도 사용).</summary>
        public static RoomZoneDef[] RoomsForStage(int stage) => GetRoomsForStage(stage);

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
            // [v1.5.1] 여기서 Stage 1 규격을 미리 스폰하지 않는다.
            //  - Stage 2~7 씬에서는 매니저가 활성화하자마자 실제 스테이지 규격으로 다시 스폰하므로,
            //    예전에는 같은 프레임에 Stage 1 물량을 만들었다 버리는 이중 작업이 있었다(진입 히치의 한 몫).
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

        /// <summary>
        /// [v1.8] 씬에 커스텀 쓰레기 타입이 4개 이하로 들어 있어도 고가치 4종(인덱스 5~8)이
        /// 반드시 존재하도록 절차 메시로 빈 칸을 채운다 — 고가치가 일반 쓰레기처럼 보이지 않게.
        /// </summary>
        private void EnsureHeavyMeshTypes()
        {
            if (meshTypes.Count >= 9) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh cyl = c.GetComponent<MeshFilter>().sharedMesh; Destroy(c);
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh box = b.GetComponent<MeshFilter>().sharedMesh; Destroy(b);

            while (meshTypes.Count < 5)
                AddProc(lit, "쓰레기 조각", box, new Color(0.55f, 0.52f, 0.46f), 0.2f, 0.16f, Vector3.zero);
            AddProc(lit, "고철 캔", cyl, new Color(0.42f, 0.44f, 0.50f), 0.72f, 0.26f, new Vector3(8f, 0f, 74f));
            AddProc(lit, "전자폐기물", box, new Color(0.22f, 0.24f, 0.28f), 0.55f, 0.40f, new Vector3(8f, 24f, 4f));
            AddProc(lit, "대형 폐가전", box, new Color(0.74f, 0.74f, 0.70f), 0.42f, 0.72f, new Vector3(0f, 12f, 0f));
            AddProc(lit, "금괴", box, new Color(0.98f, 0.80f, 0.24f), 0.92f, 0.34f, new Vector3(24f, 18f, 0f));
            EnsureRenderBuffers();
        }

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _spawnedCount = 0;
            _cleanedCount = 0;
            _freeSlots.Clear();
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

            // [v1.8] 내구도 있는 고가치 폐기물 4종 (인덱스 5~8 = heavyClass 1~4)
            AddProc(lit, "고철 캔", cyl, new Color(0.42f, 0.44f, 0.50f), 0.72f, 0.26f, new Vector3(8f, 0f, 74f));
            AddProc(lit, "전자폐기물", box, new Color(0.22f, 0.24f, 0.28f), 0.55f, 0.40f, new Vector3(8f, 24f, 4f));
            AddProc(lit, "대형 폐가전", box, new Color(0.74f, 0.74f, 0.70f), 0.42f, 0.72f, new Vector3(0f, 12f, 0f));
            AddProc(lit, "금괴", box, new Color(0.98f, 0.80f, 0.24f), 0.92f, 0.34f, new Vector3(24f, 18f, 0f));
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
            EnsureHeavyMeshTypes();
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

            // [v1.8] 강화 '물량·밀도' 노드가 스테이지 진입 시점의 웨이브 크기·웨이브 수를 늘린다.
            _densityMult = 1f + (manager != null ? manager.TrashDensity01 : 0f);
            _megaDump = manager != null && manager.MegaDump;
            int baseWaves = (_currentStageLayout < StageWaveCounts.Length && StageWaveCounts[_currentStageLayout] > 0)
                ? StageWaveCounts[_currentStageLayout] : 1;
            WaveCount = baseWaves + (manager != null ? manager.WaveBonusCount : 0);
            _waveCountFixed = WaveCount;
            _perWaveCount = 0;
            for (int r = 0; r < _rooms.Length; r++) _perWaveCount += RoomWaveCount(_rooms[r], 0);
            StageTotalItems = 0;
            for (int w = 0; w < WaveCount; w++) StageTotalItems += WaveForIndex(w);
            int heavyPerWave = manager != null ? manager.HeavyPerWave : 0;
            if (heavyPerWave > 0)
            {
                for (int w = 0; w < WaveCount; w++)
                    StageTotalItems += Mathf.RoundToInt(heavyPerWave * ((_megaDump && w >= _waveCountFixed - 1) ? 1.5f : 1f));
            }
            WaveIndex = 0;

            // [v1.7] 웨이브마다 청소된 슬롯을 재사용하므로, 필요한 배열은 '웨이» 1회분 + 여유'면 충분하다.
            // [v1.8] 고가치는 일반보다 오래 살아남으므로 그만큼 여유를 더 둔다.
            int maxWave = Mathf.Max(_perWaveCount, WaveForIndex(WaveCount - 1));
            int heavySlack = 0;
            if (manager != null && manager.HeavyPerWave > 0)
                heavySlack = manager.HeavyPerWave * Mathf.Min(WaveCount, 6) + 120;
            InitializeCapacity(maxWave + heavySlack + SpawnCapacityMargin);
            SpawnInProgress = true;

            if (!isActiveAndEnabled)
            {
                // 비활성 상태에서는 코루틴을 돌릴 수 없으므로 즉시 채운다(결과는 기존과 동일).
                for (int w = 0; w < WaveCount; w++)
                {
                    IEnumerator it = CoSpawnWave(false, w);
                    while (it.MoveNext()) { }
                }
                SpawnInProgress = false;
                return;
            }

            Debug.Log($"[TrashCleaner3D] Stage {_currentStageLayout} 스폰 시작: 웨이브당 약 {_perWaveCount:N0}개 × {WaveCount} = {StageTotalItems:N0}개 · t={Time.realtimeSinceStartup:0.00}s");
            _spawnRoutine = StartCoroutine(CoSpawnWave(true, 0));
        }

        /// <summary>[v1.8] 웨이브별 실제 개수(밀도·추가 웨이브·마지막 대량 투하 반영).</summary>
        private int WaveForIndex(int waveIndex)
        {
            int sum = 0;
            for (int r = 0; r < _rooms.Length; r++) sum += RoomWaveCount(_rooms[r], waveIndex);
            return sum;
        }

        private int RoomWaveCount(RoomZoneDef room, int waveIndex)
        {
            float scale = _densityMult;
            if (_megaDump && waveIndex >= _waveCountFixed - 1) scale *= 1.5f;
            return Mathf.Max(1, Mathf.RoundToInt(room.countToSpawn * scale));
        }

        /// <summary>
        /// [v1.7] 웨이브 단위 스폰. 4시간 캠페인 물량(총 7.7만 개)을 한 번에 만들지 않고,
        /// 프레임 예산(SpawnFrameBudgetSeconds) · 웨이브 상한(1,200~5,000개) 안에서 나누어 채운다.
        /// 유닛 좌표·개수·난수 순서는 기존 계산식과 같다.
        /// </summary>
        private IEnumerator CoSpawnWave(bool frameSpread, int waveIndex)
        {
            float stageStart = Time.realtimeSinceStartup;

            for (int r = 0; r < _rooms.Length; r++)
            {
                RoomZoneDef room = _rooms[r];
                int n = RoomWaveCount(room, waveIndex);
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
            WaveIndex = waveIndex + 1;

            // [v1.8] 웨이브마다 고가치 폐기물을 별도로 흩어 놓는다(묵직하게 깎는 손맛).
            if (manager != null)
            {
                int heavyN = manager.HeavyPerWave;
                if (_megaDump && waveIndex >= _waveCountFixed - 1) heavyN = Mathf.RoundToInt(heavyN * 1.5f);
                for (int h = 0; h < heavyN; h++)
                {
                    int cls = manager.RollHeavyClass();
                    if (cls <= 0) cls = 1;
                    SpawnHeavyUnit(_rooms[h % _rooms.Length], (byte)(h % _rooms.Length), cls);
                }
                if (heavyN > 0) _staticDirty = true;
            }

            Debug.Log($"[TrashCleaner3D] Stage {_currentStageLayout} 웨이브 {WaveIndex}/{WaveCount} 스폰 완료: 현재 {_unitCount:N0}개 · {(Time.realtimeSinceStartup - stageStart) * 1000f:0}ms · t={Time.realtimeSinceStartup:0.00}s");
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

            int tIdx = (i < n * 0.45f) ? ((i / 6) % Mathf.Min(5, meshTypes.Count)) : Random.Range(0, Mathf.Min(5, meshTypes.Count));
            AddUnit(pos, pos, false, tIdx, roomIdx);
        }

        /// <summary>[v1.8] 웨이브에 섞이는 '내구도 있는 고가치 쓰레기' 스폰.
        /// 방 바닥에 흩어 놓고, 장비로 몇 번 더 깎아야 빨려 들어간다.</summary>
        private void SpawnHeavyUnit(RoomZoneDef room, byte roomIdx, int heavyClass)
        {
            if (heavyClass <= 0 || manager == null) return;
            float margin = 0.5f;
            Vector3 pos = new Vector3(
                Random.Range(room.minX + margin, Mathf.Max(room.minX + margin, room.maxX - margin)),
                room.floorY + 0.10f,
                Random.Range(room.minZ + margin, Mathf.Max(room.minZ + margin, room.maxZ - margin)));
            if (_currentStageLayout == 1)
            {
                if (pos.x < -1.95f && pos.z < -1.10f) pos.y += 0.56f;
                if (pos.x > 1.35f && pos.x < 2.95f && pos.z > 2.60f) pos.z -= 1.15f;
            }
            int typeIdx = Mathf.Clamp(4 + heavyClass, 0, meshTypes.Count - 1);
            float hp = manager.HeavyHpFor(heavyClass);
            AddUnit(pos, pos, false, typeIdx, roomIdx, heavyClass, hp, manager.HeavyValueFor(heavyClass));
            _staticDirty = true;
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

        private int AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation, int tIdx, byte roomIdx,
            int heavyClass = 0, float heavyHp = 0f, int valueOverride = 2)
        {
            tIdx = Mathf.Clamp(tIdx, 0, meshTypes.Count - 1);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.86f, 1.22f);
            Quaternion baseOffset = Quaternion.Euler(meshTypes[tIdx].baseEulerOffset);
            Quaternion rot = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f)) * baseOffset;

            int idx;
            if (_freeSlots.Count > 0)
            {
                // [v1.7] 청소로 비워진 슬롯 재사용 → 13만 개 누적 물량에도 배열·순회 비용은 '동시 생존 수'만큼만 든다.
                idx = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                RemoveFromGrid(idx);
            }
            else
            {
                if (_unitCount >= _units.Length) return -1;
                idx = _unitCount++;
            }
            _spawnedCount++;
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
                value = valueOverride,
                isCritBonus = false,
                holdCharge = 0f,
                suckDur = 0.24f,
                arcHeight = 0.45f,
                roomIdx = roomIdx,
                cachedMatrix = Matrix4x4.TRS(startPos, rot, Vector3.one * baseS),
                gridKey = cellKey,
                inActiveList = popAnimation,
                heavyClass = (byte)Mathf.Clamp(heavyClass, 0, 4),
                hp = heavyHp,
                hpMax = heavyHp
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
            return idx;
        }

        private void RemoveFromGrid(int idx)
        {
            int key = _units[idx].gridKey;
            if (_grid.TryGetValue(key, out var bucket))
            {
                bucket.Remove(idx);
                if (bucket.Count == 0) _grid.Remove(key);
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
            // v1.7: 아직 웨이브가 남아 있으면 방 '클리어' 연출을 띄우지 않는다(뒤에서 또 쏟아진다).
            if (_roomRemaining[rIdx] == 0 && !_roomClearedFlags[rIdx] && AllWavesSpawned)
            {
                _roomClearedFlags[rIdx] = true;
                ClearedRoomCount++;
                RoomCleared?.Invoke(rIdx, _rooms[rIdx].name, _rooms[rIdx].center);            }
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
            float heavyDamage = manager != null ? manager.HeavyTickDamage : 1f;
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
                    // [v1.8] 내구도 있는 고가치 쓰레기: 한 번에 빨려가지 않고 틱마다 깎인다.
                    if (_units[idx].hp > 0f)
                    {
                        _units[idx].hp -= heavyDamage;
                        _units[idx].holdCharge = 0.55f;
                        fxOrigin = _units[idx].pos;
                        if (_units[idx].hp <= 0f)
                        {
                            StartUnitSuck(idx, 2, destPos, false);
                            picked++;
                        }
                        continue;
                    }

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

        /// <summary>
        /// v1.6 큰 봉투가 터질 때의 '미니 블랙홀 연쇄 흡입'.
        /// 반경 안의 작은 쓰레기들이 공중으로 튀어 오르며(포물선 아치) 플레이어 손으로 연쇄로 빨려 들어간다.
        /// 시작 시점을 조금씩 어긋나게(delay) 두어 한꺼번에 사라지지 않고 '와르르' 빨려오는 느낌을 만든다.
        /// </summary>
        public int ChainSuckAround(Vector3 origin, float radius, Transform dest, int maxCount)
        {
            if (_unitCount == 0 || maxCount <= 0 || radius <= 0.05f) return 0;
            if (dest != null) _suckDestination = dest;

            int picked = 0;
            int minCx = Mathf.FloorToInt(origin.x - radius);
            int maxCx = Mathf.FloorToInt(origin.x + radius);
            int minCz = Mathf.FloorToInt(origin.z - radius);
            int maxCz = Mathf.FloorToInt(origin.z + radius);
            float rSq = radius * radius;

            for (int cx = minCx; cx <= maxCx && picked < maxCount; cx++)
            {
                for (int cz = minCz; cz <= maxCz && picked < maxCount; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count && picked < maxCount; b++)
                    {
                        int idx = bucket[b];
                        if (_units[idx].state != 0) continue;
                        if (_units[idx].hp > 0f) continue;   // [v1.8] 내구도 있는 고가치는 연쇄로 빨려가지 않는다
                        Vector3 d = _units[idx].pos - origin;
                        if (Mathf.Abs(d.y) > 1.35f) continue;
                        d.y = 0f;
                        if (d.sqrMagnitude > rSq) continue;

                        float dist01 = Mathf.Clamp01(Mathf.Sqrt(d.sqrMagnitude) / Mathf.Max(0.2f, radius));
                        StartUnitSuckDelayed(
                            idx, 2,
                            Random.Range(0f, 0.05f + dist01 * 0.16f),
                            Mathf.Lerp(0.30f, 0.52f, dist01),
                            Mathf.Lerp(1.75f, 0.95f, dist01));
                        picked++;
                    }
                }
            }

            if (picked > 0) _staticDirty = true;
            return picked;
        }

        // ===================== [v1.8] 원기옥 압축 · 미니 블랙홀 =====================
        private Vector3 _orbCenter;
        private float _orbRadius = 2f;
        private float _orbDps = 5f;
        private bool _orbActive;
        private int _orbAbsorbedCount;
        private int _orbAbsorbedValue;
        private int _vortexAbsorbedCount;
        private int _vortexAbsorbedValue;

        /// <summary>[v1.8] 원기옥이 매 프레임 부르는 설정 — 한 점으로 끌어모을 중심·반경·압축 데미지.</summary>
        public void SetOrbTarget(Vector3 center, float radius, float dps)
        {
            _orbCenter = center;
            _orbRadius = Mathf.Max(0.4f, radius);
            _orbDps = Mathf.Max(0.5f, dps);
            _orbActive = true;
        }

        public void ClearOrbTarget()
        {
            _orbActive = false;
        }

        /// <summary>반경 안의 멀쩡한 쓰레기를 구슬로 끌어당기기 시작한다(최대 maxNew개).</summary>
        public int PullToOrb(int maxNew)
        {
            if (!_orbActive || maxNew <= 0 || _unitCount == 0) return 0;
            int started = 0;
            float rSq = _orbRadius * _orbRadius;
            int minCx = Mathf.FloorToInt(_orbCenter.x - _orbRadius), maxCx = Mathf.FloorToInt(_orbCenter.x + _orbRadius);
            int minCz = Mathf.FloorToInt(_orbCenter.z - _orbRadius), maxCz = Mathf.FloorToInt(_orbCenter.z + _orbRadius);
            for (int cx = minCx; cx <= maxCx && started < maxNew; cx++)
            {
                for (int cz = minCz; cz <= maxCz && started < maxNew; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count && started < maxNew; b++)
                    {
                        int idx = bucket[b];
                        if (_units[idx].state != 0) continue;
                        Vector3 d = _units[idx].pos - _orbCenter;
                        if (d.sqrMagnitude > rSq) continue;
                        ActivateUnit(idx);
                        _units[idx].state = 5;
                        _units[idx].timer = 0f;
                        _units[idx].holdCharge = 0f;
                        _units[idx].startPos = _units[idx].pos;
                        started++;
                    }
                }
            }
            if (started > 0) _staticDirty = true;
            return started;
        }

        /// <summary>구슬이 삼킨 개수·가치를 가져가고 비운다(매 프레임 호출).</summary>
        public int ConsumeOrbAbsorbed(out int value)
        {
            int c = _orbAbsorbedCount; value = _orbAbsorbedValue;
            _orbAbsorbedCount = 0; _orbAbsorbedValue = 0;
            return c;
        }

        public void SetBlackHoles(Vector3[] centers, int count, float radius, float spin, float dps)
        {
            _bhCount = Mathf.Clamp(count, 0, 3);
            for (int i = 0; i < _bhCount && i < centers.Length; i++) _bhCenters[i] = centers[i];
            _bhRadius = Mathf.Max(0.5f, radius);
            _bhSpin = Mathf.Max(0.2f, spin);
            _bhDps = Mathf.Max(0.5f, dps);
        }

        public void ClearBlackHoles()
        {
            _bhCount = 0;
        }

        /// <summary>블랙홀 반경 안의 쓰레기를 소용돌이 궤도로 끌어들인다.</summary>
        public int BlackHoleTick(int maxNew)
        {
            if (_bhCount <= 0 || maxNew <= 0 || _unitCount == 0) return 0;
            int started = 0;
            float rSq = _bhRadius * _bhRadius;
            for (int h = 0; h < _bhCount && started < maxNew; h++)
            {
                Vector3 c = _bhCenters[h];
                int minCx = Mathf.FloorToInt(c.x - _bhRadius), maxCx = Mathf.FloorToInt(c.x + _bhRadius);
                int minCz = Mathf.FloorToInt(c.z - _bhRadius), maxCz = Mathf.FloorToInt(c.z + _bhRadius);
                for (int cx = minCx; cx <= maxCx && started < maxNew; cx++)
                {
                    for (int cz = minCz; cz <= maxCz && started < maxNew; cz++)
                    {
                        if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                        for (int b = 0; b < bucket.Count && started < maxNew; b++)
                        {
                            int idx = bucket[b];
                            if (_units[idx].state != 0) continue;
                            Vector3 d = _units[idx].pos - c;
                            if (d.sqrMagnitude > rSq) continue;
                            ActivateUnit(idx);
                            _units[idx].state = 6;
                            _units[idx].timer = 0f;
                            _units[idx].suckDur = Random.Range(1.1f, 1.7f);
                            _units[idx].orbitIdx = (byte)h;
                            _units[idx].orbitAngle = Mathf.Atan2(d.z, d.x);
                            _units[idx].startPos = _units[idx].pos;
                            started++;
                        }
                    }
                }
            }
            if (started > 0) _staticDirty = true;
            return started;
        }

        public int ConsumeVortexAbsorbed(out int value)
        {
            int c = _vortexAbsorbedCount; value = _vortexAbsorbedValue;
            _vortexAbsorbedCount = 0; _vortexAbsorbedValue = 0;
            return c;
        }

        private void FinishAbsorb(int idx, bool orb)
        {
            _cleanedCount++;
            _freeSlots.Add(idx);
            NotifyUnitCleaned(_units[idx].roomIdx);
            if (orb) { _orbAbsorbedCount++; _orbAbsorbedValue += _units[idx].value; }
            else { _vortexAbsorbedCount++; _vortexAbsorbedValue += _units[idx].value; }
        }

        private void StartUnitSuckDelayed(int idx, byte newState, float delay, float suckDur, float arcHeight)
        {
            ActivateUnit(idx);
            _units[idx].state = newState;
            _units[idx].timer = -Mathf.Max(0f, delay);   // 음수 타이머 = 지연 후 출발
            _units[idx].holdCharge = 0f;
            _units[idx].startPos = _units[idx].pos;
            _units[idx].isCritBonus = false;
            _units[idx].suckDur = Mathf.Max(0.12f, suckDur);
            _units[idx].arcHeight = arcHeight;
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

        public bool TryFindNearestIdlePos(Vector3 fromPos, out Vector3 foundPos, float searchRadius = 3f)
        {
            foundPos = Vector3.zero;
            // 먼저 주변 반경 셀에서 빠르게 탐색
            float best = float.MaxValue;
            int bestIdx = -1;
            int minCx = Mathf.FloorToInt(fromPos.x - searchRadius), maxCx = Mathf.FloorToInt(fromPos.x + searchRadius);
            int minCz = Mathf.FloorToInt(fromPos.z - searchRadius), maxCz = Mathf.FloorToInt(fromPos.z + searchRadius);
            float searchSq = searchRadius * searchRadius;
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz), out var bucket)) continue;
                    for (int b = 0; b < bucket.Count; b++)
                    {
                        int i = bucket[b];
                        if (_units[i].state != 0) continue;
                        if (_units[i].hp > 0f) continue;   // [v1.8] 로봇은 고가치 쓰레기를 노리지 않는다
                        float dSq = (_units[i].pos - fromPos).sqrMagnitude;
                        if (dSq > searchSq) continue;
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
                        if (_units[i].hp > 0f) continue;   // [v1.8] 로봇은 고가치 쓰레기를 삼키지 않는다
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
                        _freeSlots.Add(i);   // 슬롯 재사용 대기열
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
                else if (st == 5)
                {
                    // [v1.8] 원기옥으로 끌려가는 중 — 내구도가 남아 있으면 표면에서 버티며 깎인다.
                    Vector3 center = _orbCenter;
                    Vector3 to = center - _units[i].pos;
                    float dist = to.magnitude;
                    float hp = _units[i].hp;
                    if (hp > 0f)
                    {
                        hp -= _orbDps * dt;
                        _units[i].hp = hp;
                        if (dist > 0.75f)
                            _units[i].pos += to / Mathf.Max(0.001f, dist) * Mathf.Min(dist - 0.75f, 5.5f * dt);
                        if (hp <= 0f) _units[i].pos = to.sqrMagnitude > 0.0001f ? _units[i].pos : center - Vector3.forward * 0.5f;
                    }
                    else
                    {
                        float pull = _units[i].heavyClass > 0 ? 8.5f : 11.5f;
                        _units[i].pos = Vector3.MoveTowards(_units[i].pos, center, pull * dt);
                    }
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 520f * dt, 240f * dt);
                    dist = (_units[i].pos - center).magnitude;
                    if (dist < 0.30f || (dist < 0.75f && _units[i].hp <= 0f))
                    {
                        _units[i].state = 3;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        FinishAbsorb(i, true);
                        continue;
                    }
                    PushChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale));
                }
                else if (st == 6)
                {
                    // [v1.8] 블랙홀 소용돌이 궤도 — 돌다가 중심으로 빨려 들어간다.
                    _units[i].timer += dt;
                    float t = Mathf.Clamp01(_units[i].timer / Mathf.Max(0.4f, _units[i].suckDur));
                    Vector3 c = _bhCenters[Mathf.Clamp((int)_units[i].orbitIdx, 0, 2)];
                    float orbitR = Mathf.Lerp(_bhRadius * 0.82f, 0.06f, t * t);
                    _units[i].orbitAngle += _bhSpin * dt * (1.7f - t);
                    float y = c.y + 0.30f * (1f - t);
                    _units[i].pos = new Vector3(c.x + Mathf.Cos(_units[i].orbitAngle) * orbitR, y, c.z + Mathf.Sin(_units[i].orbitAngle) * orbitR);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(130f * dt * _bhSpin, 300f * dt, 110f * dt);
                    if (_units[i].hp > 0f)
                    {
                        _units[i].hp -= _bhDps * dt;
                        if (_units[i].hp <= 0f) _units[i].suckDur = Mathf.Max(0.35f, _units[i].timer + 0.25f);
                    }
                    bool absorbed = t >= 1f || (_units[i].hp <= 0f && t >= 0.62f);
                    if (absorbed)
                    {
                        _units[i].state = 3;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        FinishAbsorb(i, false);
                        continue;
                    }
                    PushChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale));
                }
            }

            // [v1.8] 웨이브가 남아 있고 현재 웨이브를 거의 다 비웠다면 다음 더미를 공급한다.
            if (!AllWavesSpawned && _spawnRoutine == null && _unitCount > 0)
            {
                int perWave = WaveForIndex(WaveIndex > 0 ? WaveIndex - 1 : 0);
                int trigger = Mathf.Max(150, Mathf.RoundToInt(perWave * 0.10f));
                if (RemainingCount <= trigger)
                {
                    _spawnRoutine = StartCoroutine(CoSpawnWave(true, WaveIndex));
                    if (manager != null)
                        manager.ShowToast($"더미 붕괴 {WaveIndex + 1}/{WaveCount}");
                }
            }

            // 정적 청크는 변경이 생겼을 때만 재구축! (v1.7: 3만 개 규모 → 0.18초 스로틀)
            _staticRebuildTimer -= dt;
            if (_staticDirty && (_staticRebuildTimer <= 0f || _activeIndices.Count == 0))
            {
                _staticRebuildTimer = StaticRebuildInterval;
                RebuildStaticChunks();
            }

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
