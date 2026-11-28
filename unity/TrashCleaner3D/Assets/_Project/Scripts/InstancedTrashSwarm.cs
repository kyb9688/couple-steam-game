using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.9 10만 개 무렉 GPU/CPU 아키텍처 (WebGL 60fps 대응):
    /// 1) 4종 저폴리 프록시 메쉬 자동 전환(개당 12~24 삼각형) + Shadow Off → 드로우콜·오버드로우 25배 경량화
    /// 2) 표면 노출 쉘(Surface Crust, 셀당 상위 44개) + 심층 매몰 스택(Buried Stack):
    ///    100,000개 전량 데이터를 보유하되 GPU에는 겉표면 ~7,000개만 노출. 위를 빨아들이면 아래층이 실시간 솟아오름!
    /// 3) O(1) Swap-Back 공간 섹터 청크 + 카메라 절두체(Frustum) & 층간(1F/2F/3F) 컬링 → 전체 순회(RebuildStaticChunks) 0ms!
    /// 4) 3D 시선 레이 직접 타겟팅(TryGetRayTrashHit) → 더미 위 쓰레기를 봐도 바닥에 꽂히지 않고 정확히 락온!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public static readonly int[] StageWaveCounts = { 0, 1, 1, 1, 1, 1, 1, 1 };

        public int StageTotalItems { get; private set; }
        public int WaveIndex { get; private set; }
        public int WaveCount { get; private set; }
        public bool AllWavesSpawned => WaveIndex >= WaveCount;
        private const int SpawnCapacityMargin = 1600;
        // [v2.0] 셀 숨김 팝인(리스폰 착시) 완전 제거 — 전량 처음부터 노출하고 섹터+절두체+저폴리로 60fps 유지!
        private const int ExposedMaxPerCell = 5000;
        private const int MaxDynamicFlyingVisuals = 420;
        private const float SectorSizeXZ = 3.5f;

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
            public int gridKey;
            public bool inActiveList;
            public bool isExposed;
            public int sectorIdx;
            public int sectorSlot;
            public byte heavyClass;   // 0 일반, 1 캔·병, 2 전자, 3 대형, 4 금고
            public float hp;
            public float hpMax;
            public float orbitAngle;
            public byte orbitIdx;
        }

        private class CellBucket
        {
            public readonly List<int> exposed = new List<int>(48);
            public readonly List<int> buried = new List<int>(64);
        }

        private class RenderSector
        {
            public int key;
            public Vector3 center;
            public Bounds bounds;
            public List<Matrix4x4[]>[] chunks;
            public List<int[]>[] unitIdChunks;
            public int[] counts;
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
        private int _spawnedCount;
        private int _cleanedCount;
        private readonly List<int> _freeSlots = new List<int>(4096);
        private int _currentStageLayout = 1;
        private Transform _suckDestination;
        private Transform _robotSuckDest;

        // 3D 층분리 Spatial Hash Grid (1m XZ × 층)
        private readonly Dictionary<int, CellBucket> _grid = new Dictionary<int, CellBucket>(1024);
        private readonly List<int> _activeIndices = new List<int>(512);

        // O(1) Swap-Back 공간 섹터 청크 + 절두체 컬링
        private readonly List<RenderSector> _sectors = new List<RenderSector>(64);
        private readonly Dictionary<int, int> _sectorMap = new Dictionary<int, int>(64);
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private List<Matrix4x4[]>[] _dynChunks;
        private int[] _dynCounts;
        private RenderParams[] _renderParams;

        private Matrix4x4[][] _outlineMatrices;
        private int[] _outlineCounts;
        private Material _outlineMat;
        private RenderParams _outlineRenderParams;

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

        public bool SpawnInProgress { get; private set; }
        private const float SpawnFrameBudgetSeconds = 0.012f;
        private Coroutine _spawnRoutine;
        private bool _proxiesApplied;

        public int HoveredCount => _selectedIndices.Count;
        public float MaxSelectedCharge01 { get; private set; }
        public int TotalCount => _unitCount;
        public int SpawnedCount => _spawnedCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _spawnedCount - _cleanedCount);

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

        // Stage 1: 반지하 원룸 (1,400개)
        private static readonly RoomZoneDef[] Stage1Rooms =
        {
            new RoomZoneDef { name = "반지하 원룸", minX = -4.35f, maxX = 4.35f, minZ = -3.60f, maxZ = 3.55f, floorY = 0f, center = new Vector3(0f, 0f, 0f), countToSpawn = 1400 }
        };

        // Stage 2: 택배 중독 오피스텔 (4,500개)
        private static readonly RoomZoneDef[] Stage2Rooms =
        {
            new RoomZoneDef { name = "오피스텔 본실", minX = -1.95f, maxX =  1.95f, minZ = -3.20f, maxZ = 1.35f, floorY = 0f, center = new Vector3(0f, 0f, -0.95f), countToSpawn = 3500 },
            new RoomZoneDef { name = "화장실",        minX = -1.90f, maxX = -0.75f, minZ =  1.85f, maxZ = 3.20f, floorY = 0f, center = new Vector3(-1.35f, 0f, 2.55f), countToSpawn = 350 },
            new RoomZoneDef { name = "현관 복도",     minX =  0.25f, maxX =  1.90f, minZ =  1.50f, maxZ = 3.00f, floorY = 0f, center = new Vector3(1.10f, 0f, 2.25f), countToSpawn = 650 }
        };

        // Stage 3: 치킨집 주방 (12,000개)
        private static readonly RoomZoneDef[] Stage3Rooms =
        {
            new RoomZoneDef { name = "치킨집 주방",  minX = -1.65f, maxX = 2.50f, minZ = -1.40f, maxZ = 1.05f, floorY = 0f, center = new Vector3(0.42f, 0f, -0.18f), countToSpawn = 9600 },
            new RoomZoneDef { name = "홀·카운터 앞", minX =  0.55f, maxX = 2.50f, minZ =  1.05f, maxZ = 2.15f, floorY = 0f, center = new Vector3(1.45f, 0f, 1.55f),  countToSpawn = 2400 }
        };

        // Stage 4: 육아 전쟁 맞벌이 집 (26,000개)
        private static readonly RoomZoneDef[] Stage4Rooms =
        {
            new RoomZoneDef { name = "거실·놀이매트", minX = -0.50f, maxX =  3.40f, minZ = -2.70f, maxZ = 2.25f, floorY = 0f, center = new Vector3(1.45f, 0f, -0.20f), countToSpawn = 18500 },
            new RoomZoneDef { name = "안방",          minX = -3.40f, maxX = -0.80f, minZ = -2.70f, maxZ = 0.35f, floorY = 0f, center = new Vector3(-2.10f, 0f, -1.18f), countToSpawn = 5500 },
            new RoomZoneDef { name = "욕실",          minX = -3.40f, maxX = -1.85f, minZ =  1.20f, maxZ = 2.75f, floorY = 0f, center = new Vector3(-2.60f, 0f, 1.98f), countToSpawn = 2000 }
        };

        // Stage 5: 야근 지옥 스타트업 사무실 (48,000개)
        private static readonly RoomZoneDef[] Stage5Rooms =
        {
            new RoomZoneDef { name = "오픈 오피스", minX = -1.15f, maxX =  4.00f, minZ = -3.30f, maxZ =  3.20f, floorY = 0f, center = new Vector3(1.42f, 0f, -0.02f), countToSpawn = 36000 },
            new RoomZoneDef { name = "대표실",      minX = -4.05f, maxX = -1.50f, minZ = -3.30f, maxZ = -0.70f, floorY = 0f, center = new Vector3(-2.78f, 0f, -2.00f), countToSpawn = 7800 },
            new RoomZoneDef { name = "탕비실",      minX = -4.05f, maxX = -2.45f, minZ =  0.80f, maxZ =  3.20f, floorY = 0f, center = new Vector3(-3.25f, 0f, 2.02f), countToSpawn = 4200 }
        };

        // Stage 6: 시골집 농막 (35,000개 · 초고밀도 + 최강 악취)
        private static readonly RoomZoneDef[] Stage6Rooms =
        {
            new RoomZoneDef { name = "시골 농막", minX = -1.90f, maxX = 1.90f, minZ = -1.60f, maxZ = 1.55f, floorY = 0f, center = new Vector3(0f, 0f, 0f), countToSpawn = 35000 }
        };

        // Stage 7: 졸부 파티광 3층 복층 펜트하우스 (정확히 100,000개!)
        private static readonly RoomZoneDef[] Stage7Rooms =
        {
            new RoomZoneDef { name = "1층 거실·바",   minX = -4.80f, maxX =  3.00f, minZ = -3.80f, maxZ = 3.80f, floorY = 0.00f, center = new Vector3(-0.90f, 0.00f, 0.00f), countToSpawn = 38000 },
            new RoomZoneDef { name = "2층 파티홀",    minX = -2.70f, maxX =  2.30f, minZ = -3.80f, maxZ = 3.80f, floorY = 3.11f, center = new Vector3(-0.20f, 3.11f, 0.00f), countToSpawn = 22000 },
            new RoomZoneDef { name = "2층 서쪽 랜딩", minX = -4.80f, maxX = -2.90f, minZ =  0.70f, maxZ = 3.90f, floorY = 3.11f, center = new Vector3(-3.85f, 3.11f, 2.30f), countToSpawn = 5000 },
            new RoomZoneDef { name = "2층 욕실",      minX =  2.50f, maxX =  4.70f, minZ =  1.70f, maxZ = 3.80f, floorY = 3.11f, center = new Vector3(3.60f, 3.11f, 2.75f), countToSpawn = 3000 },
            new RoomZoneDef { name = "3층 VIP홀",     minX = -1.70f, maxX =  2.70f, minZ = -3.80f, maxZ = 3.80f, floorY = 6.11f, center = new Vector3(0.50f, 6.11f, 0.00f), countToSpawn = 18000 },
            new RoomZoneDef { name = "3층 금고방",    minX = -4.80f, maxX = -2.00f, minZ = -3.80f, maxZ = -1.10f, floorY = 6.11f, center = new Vector3(-3.40f, 6.11f, -2.45f), countToSpawn = 14000 }
        };

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

        private static int FloorBandForY(float y) => Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / 2.8f), 0, 3);
        private static int PackCell(int cx, int cz, int band) => ((band & 7) << 22) | (((cx + 512) & 0x3FF) << 11) | ((cz + 512) & 0x3FF);
        private static int CellKeyForPos(Vector3 p) => PackCell(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z), FloorBandForY(p.y));
        private static int PackSector(int sx, int sz, int band) => ((band & 7) << 22) | (((sx + 256) & 0x3FF) << 11) | ((sz + 256) & 0x3FF);
        private static int SectorKeyForPos(Vector3 p) => PackSector(Mathf.FloorToInt(p.x / SectorSizeXZ), Mathf.FloorToInt(p.z / SectorSizeXZ), FloorBandForY(p.y));

        public bool TryGetPrimaryTargetPos(out Vector3 worldPos)
        {
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx >= 0 && idx < _unitCount && _units[idx].state == 0)
                {
                    worldPos = _units[idx].pos;
                    return true;
                }
            }
            worldPos = Vector3.zero;
            return false;
        }

        private void Awake() { }

        private IEnumerator Start()
        {
            if (meshTypes.Count == 0) BuildProceduralTypes();
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            EnsureLowPolySwarmMeshes();
            EnsureRenderBuffers();

            yield return null;

            if (!SpawnInProgress && (_units == null || _unitCount == 0))
                SpawnForStage(manager != null ? manager.CurrentStage : 1);
        }

        /// <summary>
        /// [v1.9] 고폴리 FBX 메쉬(90삼각형 초과)를 초경량 12~24삼각형 입체 프록시로 교체해 WebGL 60fps 보장!
        /// </summary>
        private void EnsureLowPolySwarmMeshes()
        {
            if (_proxiesApplied) return;
            _proxiesApplied = true;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Mesh lowCan = BuildOctaPrism(8, 0.48f, 1.0f, true);
            Mesh lowBottle = BuildOctaPrism(8, 0.42f, 1.25f, true);
            Mesh lowBox = BuildCompactBox();
            Mesh lowPaper = BuildIcoLowPoly();

            Color[] smallColors =
            {
                new Color(0.68f, 0.42f, 0.28f), // 녹슨 캔
                new Color(0.84f, 0.78f, 0.66f), // 담뱃갑
                new Color(0.32f, 0.54f, 0.68f), // 통조림 캔
                new Color(0.76f, 0.88f, 0.94f), // 생수통
                new Color(0.88f, 0.84f, 0.76f), // 구겨진 종이
            };

            for (int i = 0; i < meshTypes.Count && i < 5; i++)
            {
                var mt = meshTypes[i];
                if (mt == null) continue;
                int tris = mt.mesh != null ? mt.mesh.triangles.Length / 3 : 9999;
                if (tris > 90)
                {
                    Mesh replacement = i == 1 ? lowBox : (i == 3 ? lowBottle : (i == 4 ? lowPaper : lowCan));
                    mt.mesh = replacement;
                    float targetDim = i == 1 ? 0.14f : i == 3 ? 0.22f : i == 4 ? 0.13f : 0.16f;
                    mt.normalizedScale = targetDim;
                    mt.baseEulerOffset = i == 0 || i == 2 ? new Vector3(0f, 0f, 84f) : Vector3.zero;
                    if (lit != null)
                    {
                        Material pm = new Material(lit);
                        Color col = smallColors[i % smallColors.Length];
                        if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", col);
                        pm.color = col;
                        if (pm.HasProperty("_Smoothness")) pm.SetFloat("_Smoothness", i == 3 ? 0.55f : 0.22f);
                        pm.enableInstancing = true;
                        mt.material = pm;
                    }
                }
            }
        }

        private static Mesh BuildCompactBox()
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh src = b.GetComponent<MeshFilter>().sharedMesh;
            Mesh copy = Instantiate(src);
            Destroy(b);
            Vector3[] verts = copy.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3(verts[i].x, verts[i].y * 0.62f, verts[i].z * 0.80f);
            copy.vertices = verts;
            copy.RecalculateNormals();
            copy.RecalculateBounds();
            return copy;
        }

        private static Mesh BuildOctaPrism(int sides, float radius, float height, bool taperedTop)
        {
            Mesh m = new Mesh();
            int ringVerts = sides;
            Vector3[] verts = new Vector3[ringVerts * 2 + 2];
            int[] tris = new int[sides * 12];
            float hy = height * 0.5f;
            float topR = taperedTop ? radius * 0.68f : radius;
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i] = new Vector3(c * radius, -hy, s * radius);
                verts[sides + i] = new Vector3(c * topR, hy, s * topR);
            }
            verts[sides * 2] = new Vector3(0f, -hy, 0f);
            verts[sides * 2 + 1] = new Vector3(0f, hy, 0f);
            int ti = 0;
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                tris[ti++] = i; tris[ti++] = sides + i; tris[ti++] = sides + ni;
                tris[ti++] = i; tris[ti++] = sides + ni; tris[ti++] = ni;
                tris[ti++] = sides * 2; tris[ti++] = i; tris[ti++] = ni;
                tris[ti++] = sides * 2 + 1; tris[ti++] = sides + ni; tris[ti++] = sides + i;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildIcoLowPoly()
        {
            Mesh m = new Mesh();
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] v = {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1,-t, 0), new Vector3(1,-t, 0),
                new Vector3(0,-1, t), new Vector3(0, 1, t), new Vector3(0,-1,-t), new Vector3(0, 1,-t),
                new Vector3(t, 0,-1), new Vector3(t, 0, 1), new Vector3(-t, 0,-1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * (0.45f + (i % 3) * 0.06f);
            m.vertices = v;
            m.triangles = new[] {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void EnsureHeavyMeshTypes()
        {
            if (meshTypes.Count >= 9) return;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (lit == null) return;
            Mesh box = BuildCompactBox();
            Mesh cyl = BuildOctaPrism(8, 0.45f, 1.0f, false);

            while (meshTypes.Count < 5)
                AddProc(lit, "쓰레기 조각", box, new Color(0.55f, 0.52f, 0.46f), 0.2f, 0.16f, Vector3.zero);
            AddProc(lit, "고철 캔", cyl, new Color(0.42f, 0.44f, 0.50f), 0.72f, 0.20f, new Vector3(8f, 0f, 74f));
            AddProc(lit, "전자폐기물", box, new Color(0.24f, 0.26f, 0.30f), 0.58f, 0.24f, new Vector3(8f, 24f, 4f));
            AddProc(lit, "소형 폐가전", box, new Color(0.36f, 0.38f, 0.42f), 0.52f, 0.30f, new Vector3(0f, 12f, 0f));
            AddProc(lit, "금괴", box, new Color(0.98f, 0.80f, 0.24f), 0.92f, 0.22f, new Vector3(24f, 18f, 0f));
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
            _sectors.Clear();
            _sectorMap.Clear();
            _activeIndices.Clear();
            _selectedIndices.Clear();
            _selectedSet.Clear();
            _lockedIndices.Clear();
            MaxSelectedCharge01 = 0f;
            EnsureRenderBuffers();
        }

        private void EnsureRenderBuffers()
        {
            int types = Mathf.Max(1, meshTypes.Count);
            if (_dynChunks == null || _dynChunks.Length != types)
            {
                _dynChunks = new List<Matrix4x4[]>[types];
                _dynCounts = new int[types];
                _renderParams = new RenderParams[types];
                _outlineMatrices = new Matrix4x4[types][];
                _outlineCounts = new int[types];

                for (int t = 0; t < types; t++)
                {
                    _dynChunks[t] = new List<Matrix4x4[]> { new Matrix4x4[1023] };
                    _outlineMatrices[t] = new Matrix4x4[1023];
                }
            }

            for (int t = 0; t < meshTypes.Count; t++)
            {
                if (meshTypes[t].material != null)
                {
                    // [v2.1] 쓰레기 더미 눈부심 방지: 과한 정반사(Smoothness) 억제 + 밝은 흰색 알베도 살짝 톤다운 (중복 적용 방지)
                    Material mat = meshTypes[t].material;
                    if (!mat.name.EndsWith("_Matte"))
                    {
                        mat = new Material(mat);
                        mat.name = meshTypes[t].material.name + "_Matte";
                        mat.enableInstancing = true;
                        if (mat.HasProperty("_Smoothness"))
                            mat.SetFloat("_Smoothness", Mathf.Min(mat.GetFloat("_Smoothness"), 0.14f));
                        if (mat.HasProperty("_Metallic"))
                            mat.SetFloat("_Metallic", Mathf.Min(mat.GetFloat("_Metallic"), 0.18f));
                        if (mat.HasProperty("_BaseColor"))
                        {
                            Color bc = mat.GetColor("_BaseColor");
                            mat.SetColor("_BaseColor", new Color(bc.r * 0.78f, bc.g * 0.78f, bc.b * 0.76f, bc.a));
                        }
                        else
                        {
                            Color c = mat.color;
                            mat.color = new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.76f, c.a);
                        }
                        meshTypes[t].material = mat;
                    }
                    _renderParams[t] = new RenderParams(mat)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 80f),
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false
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
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 80f),
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
            }

            if (_auraRingMesh == null)
            {
                _auraRingMesh = BuildRingMesh(40, 0.86f, 1.0f);
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

            Mesh cyl = BuildOctaPrism(8, 0.45f, 1.0f, true);
            Mesh box = BuildCompactBox();
            Mesh sph = BuildIcoLowPoly();

            AddProc(lit, "찌그러진 캔", cyl, new Color(0.65f, 0.56f, 0.48f), 0.52f, 0.14f, new Vector3(0f, 0f, 84f));
            AddProc(lit, "택배 박스", box, new Color(0.60f, 0.45f, 0.30f), 0.12f, 0.22f, Vector3.zero);
            AddProc(lit, "페트병", cyl, new Color(0.72f, 0.80f, 0.84f), 0.60f, 0.18f, new Vector3(86f, 0f, 0f));
            AddProc(lit, "비닐봉투", sph, new Color(0.18f, 0.18f, 0.20f), 0.36f, 0.18f, Vector3.zero);
            AddProc(lit, "구겨진 종이", sph, new Color(0.84f, 0.80f, 0.74f), 0.10f, 0.13f, Vector3.zero);

            AddProc(lit, "고철 캔", cyl, new Color(0.42f, 0.44f, 0.50f), 0.72f, 0.20f, new Vector3(8f, 0f, 74f));
            AddProc(lit, "전자폐기물", box, new Color(0.24f, 0.26f, 0.30f), 0.58f, 0.24f, new Vector3(8f, 24f, 4f));
            AddProc(lit, "소형 폐가전", box, new Color(0.36f, 0.38f, 0.42f), 0.52f, 0.30f, new Vector3(0f, 12f, 0f));
            AddProc(lit, "금괴", box, new Color(0.98f, 0.80f, 0.24f), 0.92f, 0.22f, new Vector3(24f, 18f, 0f));
        }

        private void AddProc(Shader lit, string label, Mesh mesh, Color tint, float smooth, float targetDim, Vector3 euler)
        {
            Material mat = new Material(lit);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            mat.color = tint;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            mat.enableInstancing = true;
            meshTypes.Add(new TrashMeshType { label = label, mesh = mesh, material = mat, normalizedScale = targetDim, baseEulerOffset = euler });
        }

        // ===================== [v1.9] O(1) Swap-Back 섹터 & 표면 쉘 관리 =====================

        private RenderSector GetOrCreateSector(int sKey, Vector3 samplePos, out int sIdx)
        {
            if (_sectorMap.TryGetValue(sKey, out sIdx))
                return _sectors[sIdx];

            int types = Mathf.Max(1, meshTypes.Count);
            float sxCenter = (Mathf.FloorToInt(samplePos.x / SectorSizeXZ) + 0.5f) * SectorSizeXZ;
            float szCenter = (Mathf.FloorToInt(samplePos.z / SectorSizeXZ) + 0.5f) * SectorSizeXZ;
            float bandY = FloorBandForY(samplePos.y) * 3.05f;
            Vector3 c = new Vector3(sxCenter, bandY + 0.75f, szCenter);

            var sec = new RenderSector
            {
                key = sKey,
                center = c,
                bounds = new Bounds(c, new Vector3(SectorSizeXZ + 1.2f, 2.8f, SectorSizeXZ + 1.2f)),
                chunks = new List<Matrix4x4[]>[types],
                unitIdChunks = new List<int[]>[types],
                counts = new int[types]
            };
            for (int t = 0; t < types; t++)
            {
                sec.chunks[t] = new List<Matrix4x4[]>(2);
                sec.unitIdChunks[t] = new List<int[]>(2);
            }
            sIdx = _sectors.Count;
            _sectors.Add(sec);
            _sectorMap[sKey] = sIdx;
            return sec;
        }

        private void AddToSector(int idx)
        {
            if (_units[idx].sectorIdx >= 0) return;
            int sKey = SectorKeyForPos(_units[idx].pos);
            RenderSector sec = GetOrCreateSector(sKey, _units[idx].pos, out int sIdx);
            int t = _units[idx].typeIdx;
            if (t < 0 || t >= sec.counts.Length) return;

            int flat = sec.counts[t]++;
            int cIdx = flat / 1023;
            int lIdx = flat % 1023;
            while (sec.chunks[t].Count <= cIdx)
            {
                sec.chunks[t].Add(new Matrix4x4[1023]);
                sec.unitIdChunks[t].Add(new int[1023]);
            }
            sec.chunks[t][cIdx][lIdx] = Matrix4x4.TRS(_units[idx].pos, _units[idx].rot, Vector3.one * _units[idx].curScale);
            sec.unitIdChunks[t][cIdx][lIdx] = idx;
            _units[idx].sectorIdx = sIdx;
            _units[idx].sectorSlot = flat;
            _units[idx].isExposed = true;
        }

        private void RemoveFromSector(int idx)
        {
            int sIdx = _units[idx].sectorIdx;
            if (sIdx < 0 || sIdx >= _sectors.Count) return;

            RenderSector sec = _sectors[sIdx];
            int t = _units[idx].typeIdx;
            int slot = _units[idx].sectorSlot;
            int last = --sec.counts[t];

            if (slot != last && last >= 0)
            {
                int dstC = slot / 1023, dstL = slot % 1023;
                int srcC = last / 1023, srcL = last % 1023;
                sec.chunks[t][dstC][dstL] = sec.chunks[t][srcC][srcL];
                int movedUnit = sec.unitIdChunks[t][srcC][srcL];
                sec.unitIdChunks[t][dstC][dstL] = movedUnit;
                _units[movedUnit].sectorSlot = slot;
            }
            _units[idx].sectorIdx = -1;
            _units[idx].sectorSlot = -1;
            _units[idx].isExposed = false;
        }

        private void RemoveFromCellAndPromote(int idx)
        {
            Vector3 removedPos = _units[idx].pos;
            byte roomIdx = _units[idx].roomIdx;
            float floorY = (_rooms != null && roomIdx < _rooms.Length) ? _rooms[roomIdx].floorY : 0f;

            RemoveFromSector(idx);
            int key = _units[idx].gridKey;
            if (!_grid.TryGetValue(key, out CellBucket bucket)) return;

            if (!bucket.exposed.Remove(idx))
                bucket.buried.Remove(idx);

            // [v2.0] 아래 쓰레기를 빼면 바로 위에 얹혀있던 쓰레기가 자연스럽게 아래로 툭 내려앉음!
            int settled = 0;
            List<int> exp = bucket.exposed;
            for (int i = 0; i < exp.Count && settled < 10; i++)
            {
                int u = exp[i];
                if (u < 0 || u >= _unitCount || _units[u].state != 0) continue;
                Vector3 p = _units[u].pos;
                if (p.y <= removedPos.y + 0.03f) continue;
                float dx = p.x - removedPos.x;
                float dz = p.z - removedPos.z;
                if (dx * dx + dz * dz > 0.11f) continue;

                p.y = Mathf.Max(floorY + 0.045f, p.y - 0.12f);
                _units[u].pos = p;
                UpdateUnitMatrixInSector(u);
                settled++;
            }

            if (bucket.exposed.Count == 0 && bucket.buried.Count == 0)
                _grid.Remove(key);
        }

        private void UpdateUnitMatrixInSector(int idx)
        {
            int sIdx = _units[idx].sectorIdx;
            int slot = _units[idx].sectorSlot;
            if (sIdx < 0 || sIdx >= _sectors.Count || slot < 0) return;
            RenderSector sec = _sectors[sIdx];
            int t = _units[idx].typeIdx;
            if (t < 0 || t >= sec.chunks.Length) return;
            int cIdx = slot / 1023;
            int lIdx = slot % 1023;
            if (cIdx < sec.chunks[t].Count)
            {
                sec.chunks[t][cIdx][lIdx] = Matrix4x4.TRS(_units[idx].pos, _units[idx].rot, Vector3.one * _units[idx].curScale);
            }
        }

        private void RebuildExposedShellsForGrid()
        {
            foreach (var kv in _grid)
            {
                CellBucket bucket = kv.Value;
                if (bucket.exposed.Count <= ExposedMaxPerCell) continue;

                // 높이가 큰(y 높은) 쓰레기부터 겉표면(exposed)에 남기고, 낮은 것은 buried 스택으로 내린다!
                bucket.exposed.Sort((a, b) => _units[a].pos.y.CompareTo(_units[b].pos.y));
                int cut = bucket.exposed.Count - ExposedMaxPerCell;
                for (int i = 0; i < cut; i++)
                    bucket.buried.Add(bucket.exposed[i]);
                bucket.exposed.RemoveRange(0, cut);
            }

            // 노출된 표면 유닛만 공간 섹터 GPU 버퍼에 등록!
            foreach (var kv in _grid)
            {
                List<int> exp = kv.Value.exposed;
                for (int i = 0; i < exp.Count; i++)
                    AddToSector(exp[i]);
            }
        }

        public void SpawnForStage(int stage)
        {
            if (meshTypes.Count == 0) BuildProceduralTypes();
            EnsureLowPolySwarmMeshes();
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

            _densityMult = 1f + (manager != null ? manager.TrashDensity01 : 0f);
            _megaDump = false;
            WaveCount = 1;
            _waveCountFixed = 1;
            _perWaveCount = 0;
            for (int r = 0; r < _rooms.Length; r++) _perWaveCount += RoomWaveCount(_rooms[r], 0);
            StageTotalItems = _perWaveCount;
            int heavyPerWave = manager != null ? manager.HeavyPerWave : 0;
            if (heavyPerWave > 0) StageTotalItems += heavyPerWave;
            WaveIndex = 0;

            InitializeCapacity(_perWaveCount + heavyPerWave + SpawnCapacityMargin);
            SpawnInProgress = true;

            if (!isActiveAndEnabled)
            {
                IEnumerator it = CoSpawnWave(false, 0);
                while (it.MoveNext()) { }
                SpawnInProgress = false;
                return;
            }

            Debug.Log($"[TrashCleaner3D v1.9] Stage {_currentStageLayout} 스폰 시작: {StageTotalItems:N0}개 (표면 쉘 + 섹터 컬링) · t={Time.realtimeSinceStartup:0.00}s");
            _spawnRoutine = StartCoroutine(CoSpawnWave(true, 0));
        }

        private int WaveForIndex(int waveIndex)
        {
            int sum = 0;
            for (int r = 0; r < _rooms.Length; r++) sum += RoomWaveCount(_rooms[r], waveIndex);
            return sum;
        }

        private int RoomWaveCount(RoomZoneDef room, int waveIndex)
        {
            float scale = _densityMult;
            return Mathf.Max(1, Mathf.RoundToInt(room.countToSpawn * scale));
        }

        private IEnumerator CoSpawnWave(bool frameSpread, int waveIndex)
        {
            float stageStart = Time.realtimeSinceStartup;
            int baseTrashVal = manager != null ? manager.CurrentStageTrashBaseValue : 2;
            float maxStackH = _currentStageLayout >= 7 ? 1.32f : _currentStageLayout >= 5 ? 1.05f : _currentStageLayout >= 3 ? 0.85f : 0.65f;

            for (int r = 0; r < _rooms.Length; r++)
            {
                RoomZoneDef room = _rooms[r];
                int n = RoomWaveCount(room, waveIndex);
                byte roomIdx = (byte)r;
                float baseY = room.floorY;

                Vector3[] mounds =
                {
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.28f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.30f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.72f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.35f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.42f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.72f)),
                    new Vector3(Mathf.Lerp(room.minX, room.maxX, 0.68f), baseY, Mathf.Lerp(room.minZ, room.maxZ, 0.74f))
                };

                float spanMin = Mathf.Min(room.maxX - room.minX, room.maxZ - room.minZ);
                float maxRad = Mathf.Clamp(spanMin * 0.38f, 0.9f, 2.2f);

                int i = 0;
                while (i < n)
                {
                    float frameStart = Time.realtimeSinceStartup;
                    int batch = 0;
                    do
                    {
                        SpawnOneUnit(room, i, n, baseY, maxRad, maxStackH, mounds, roomIdx, baseTrashVal);
                        i++;
                        batch++;
                    }
                    while (i < n && (batch < 240 || (frameSpread && Time.realtimeSinceStartup - frameStart < SpawnFrameBudgetSeconds)));

                    if (frameSpread) yield return null;
                }
            }

            if (manager != null)
            {
                int heavyN = manager.HeavyPerWave;
                for (int h = 0; h < heavyN; h++)
                {
                    int cls = manager.RollHeavyClass();
                    if (cls <= 0) cls = 1;
                    SpawnHeavyUnit(_rooms[h % _rooms.Length], (byte)(h % _rooms.Length), cls);
                }
            }

            RebuildExposedShellsForGrid();
            SpawnInProgress = false;
            _spawnRoutine = null;
            WaveIndex = waveIndex + 1;

            Debug.Log($"[TrashCleaner3D v1.9] Stage {_currentStageLayout} 스폰 완료: 총 {_unitCount:N0}개 ({(Time.realtimeSinceStartup - stageStart) * 1000f:0}ms, 섹터 {_sectors.Count}개)");
        }

        private void SpawnOneUnit(RoomZoneDef room, int i, int n, float baseY, float maxRad, float maxStackH, Vector3[] mounds, byte roomIdx, int baseVal)
        {
            Vector3 pos;
            if (i < n * 0.74f)
            {
                Vector3 m = mounds[i % mounds.Length];
                Vector2 off = Random.insideUnitCircle * Random.Range(0.06f, maxRad);
                float dist01 = Mathf.Clamp01(off.magnitude / maxRad);
                float yStack = (1f - dist01) * (1f - dist01) * Random.Range(0.05f, maxStackH);
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
                    baseY + 0.055f + Random.Range(0f, 0.16f),
                    Random.Range(room.minZ, room.maxZ)
                );
            }

            // [v1.9] 모든 스테이지 현관문 스윙 안전구역(안쪽 반경 1.7m 비워 시야·진입 확보!) + 가구 회피
            if (_currentStageLayout == 1)
            {
                if (pos.x > 1.25f && pos.x < 3.05f && pos.z > 2.15f) pos.z = Random.Range(-1.2f, 2.0f);
                if (pos.x < -1.95f && pos.z < -1.10f) pos.y += 0.56f;
                if (pos.x > -2.85f && pos.x < -0.10f && pos.z > 2.65f) pos.z = Random.Range(1.20f, 2.50f);
                if (pos.x > -0.45f && pos.x < 1.45f && pos.z < -2.65f) pos.z = Random.Range(-2.55f, -1.35f);
            }
            else if (_currentStageLayout == 2 && pos.x > 0.25f && pos.z > 1.85f)
            {
                pos.z = Random.Range(-1.60f, 1.55f);
            }
            else if (_currentStageLayout == 3)
            {
                if (pos.x > 0.65f && pos.z > 1.05f) pos.z = Random.Range(-1.10f, 0.90f);
            }
            else if (pos.y < 0.8f && pos.z > room.maxZ - 1.35f && pos.x > 0.3f)
            {
                pos.z -= 1.45f;
            }

            int smallTypes = Mathf.Min(5, meshTypes.Count);
            int tIdx = (i < n * 0.45f) ? ((i / 6) % smallTypes) : Random.Range(0, smallTypes);
            AddUnit(pos, pos, false, tIdx, roomIdx, 0, 0f, baseVal, deferSector: true);
        }

        private void SpawnHeavyUnit(RoomZoneDef room, byte roomIdx, int heavyClass)
        {
            if (heavyClass <= 0 || manager == null) return;
            float margin = 0.5f;
            Vector3 pos = new Vector3(
                Random.Range(room.minX + margin, Mathf.Max(room.minX + margin, room.maxX - margin)),
                room.floorY + 0.14f,
                Random.Range(room.minZ + margin, Mathf.Max(room.minZ + margin, room.maxZ - margin)));
            if (_currentStageLayout == 1)
            {
                if (pos.x < -1.95f && pos.z < -1.10f) pos.y += 0.56f;
                if (pos.x > 1.25f && pos.x < 3.05f && pos.z > 2.15f) pos.z -= 1.55f;
            }
            int typeIdx = Mathf.Clamp(4 + heavyClass, 0, meshTypes.Count - 1);
            float hp = manager.HeavyHpFor(heavyClass);
            AddUnit(pos, pos, false, typeIdx, roomIdx, heavyClass, hp, manager.HeavyValueFor(heavyClass), deferSector: true);
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
            int baseVal = manager != null ? manager.CurrentStageTrashBaseValue : 2;
            int smallTypeCount = Mathf.Min(5, meshTypes.Count);

            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length && _freeSlots.Count == 0) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.28f, 1.45f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, room.minX, room.maxX),
                    room.floorY + 0.06f,
                    Mathf.Clamp(origin.z + off.y, room.minZ, room.maxZ)
                );
                AddUnit(origin + Vector3.up * 0.35f, land, true, Random.Range(0, smallTypeCount), rIdx, 0, 0f, baseVal, deferSector: false);
            }
        }

        private byte FindRoomIndexForPos(Vector3 p)
        {
            for (int r = 0; r < _rooms.Length; r++)
            {
                if (Mathf.Abs(p.y - _rooms[r].floorY) < 2.1f &&
                    p.x >= _rooms[r].minX - 0.5f && p.x <= _rooms[r].maxX + 0.5f &&
                    p.z >= _rooms[r].minZ - 0.5f && p.z <= _rooms[r].maxZ + 0.5f)
                    return (byte)r;
            }
            return 0;
        }

        private int AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation, int tIdx, byte roomIdx,
            int heavyClass = 0, float heavyHp = 0f, int valueOverride = 2, bool deferSector = false)
        {
            tIdx = Mathf.Clamp(tIdx, 0, meshTypes.Count - 1);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.86f, 1.22f);
            Quaternion baseOffset = Quaternion.Euler(meshTypes[tIdx].baseEulerOffset);
            Quaternion rot = Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(0f, 360f), Random.Range(-16f, 16f)) * baseOffset;

            int idx;
            if (_freeSlots.Count > 0)
            {
                idx = _freeSlots[_freeSlots.Count - 1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
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
                suckDur = 0.22f,
                arcHeight = 0.42f,
                roomIdx = roomIdx,
                gridKey = cellKey,
                inActiveList = popAnimation,
                isExposed = false,
                sectorIdx = -1,
                sectorSlot = -1,
                heavyClass = (byte)Mathf.Clamp(heavyClass, 0, 4),
                hp = heavyHp,
                hpMax = heavyHp
            };

            if (!_grid.TryGetValue(cellKey, out var bucket))
            {
                bucket = new CellBucket();
                _grid[cellKey] = bucket;
            }

            if (popAnimation)
            {
                _activeIndices.Add(idx);
                bucket.exposed.Add(idx);
            }
            else if (deferSector)
            {
                bucket.exposed.Add(idx);
            }
            else
            {
                if (bucket.exposed.Count < ExposedMaxPerCell)
                {
                    bucket.exposed.Add(idx);
                    AddToSector(idx);
                }
                else
                {
                    bucket.buried.Add(idx);
                }
            }

            if (roomIdx < _roomRemaining.Length)
            {
                _roomRemaining[roomIdx]++;
                _roomInitial[roomIdx]++;
            }
            return idx;
        }

        private void ActivateUnitForCharge(int idx)
        {
            if (!_units[idx].inActiveList)
            {
                RemoveFromSector(idx);
                _units[idx].inActiveList = true;
                _activeIndices.Add(idx);
            }
        }

        private void NotifyUnitCleaned(byte rIdx)
        {
            if (rIdx >= _roomRemaining.Length) return;
            if (_roomRemaining[rIdx] > 0) _roomRemaining[rIdx]--;
            if (_roomRemaining[rIdx] == 0 && !_roomClearedFlags[rIdx] && AllWavesSpawned)
            {
                _roomClearedFlags[rIdx] = true;
                ClearedRoomCount++;
                RoomCleared?.Invoke(rIdx, _rooms[rIdx].name, _rooms[rIdx].center);
            }
        }

        /// <summary>
        /// [v1.9] 95% 달성 자동 완청 시 남아 있는 쓰레기 5%를 즉시 수거/정리 처리한다.
        /// </summary>
        public int SweepRemainingForAutoComplete(out int totalValue)
        {
            totalValue = 0;
            int count = 0;
            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state == 3) continue;
                totalValue += _units[i].value;
                count++;
                _units[i].state = 3;
                _units[i].inActiveList = false;
                if (_units[i].sectorIdx >= 0) RemoveFromSector(i);
            }
            _cleanedCount += count;
            _activeIndices.Clear();
            _grid.Clear();
            for (int r = 0; r < _roomRemaining.Length; r++)
            {
                _roomRemaining[r] = 0;
                _roomClearedFlags[r] = true;
            }
            ClearedRoomCount = _rooms.Length;
            return count;
        }

        public void SetCursorRing(bool visible, Vector3 center, float radius)
        {
            _cursorRingVisible = visible;
            _cursorRingCenter = center;
            _cursorRingRadius = radius;
        }

        /// <summary>
        /// [v1.9] 3D 시선 레이(Ray)가 더미 위 쓰레기를 직접 스치면 바닥 y=0까지 뚫고 가지 않고 그 쓰레기 높이에 바로 락온!
        /// </summary>
        public bool TryGetRayTrashHit(Ray aimRay, float maxHorizReach, float hitRadius, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (_unitCount == 0) return false;

            float bestAlong = float.MaxValue;
            float hitRSq = hitRadius * hitRadius;
            float horizSqMax = maxHorizReach * maxHorizReach;
            float maxRayLen = maxHorizReach + 1.85f;
            int lastKey = int.MinValue;

            for (float t = 0.25f; t <= maxRayLen; t += 0.24f)
            {
                Vector3 sample = aimRay.origin + aimRay.direction * t;
                Vector2 flatSample = new Vector2(sample.x - aimRay.origin.x, sample.z - aimRay.origin.z);
                if (flatSample.sqrMagnitude > horizSqMax + 0.15f) break;

                int band = FloorBandForY(sample.y);
                int cx = Mathf.FloorToInt(sample.x);
                int cz = Mathf.FloorToInt(sample.z);
                int cKey = PackCell(cx, cz, band);
                if (cKey == lastKey) continue;
                lastKey = cKey;

                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oz = -1; oz <= 1; oz++)
                    {
                        if (!_grid.TryGetValue(PackCell(cx + ox, cz + oz, band), out CellBucket bucket)) continue;
                        List<int> list = bucket.exposed;
                        for (int i = 0; i < list.Count; i++)
                        {
                            int idx = list[i];
                            if (_units[idx].state != 0) continue;
                            Vector3 uPos = _units[idx].pos;
                            Vector3 to = uPos - aimRay.origin;
                            Vector2 flatU = new Vector2(to.x, to.z);
                            if (flatU.sqrMagnitude > horizSqMax) continue;

                            float along = Vector3.Dot(to, aimRay.direction);
                            if (along < 0.15f || along >= bestAlong) continue;
                            Vector3 closest = aimRay.origin + aimRay.direction * along;
                            if ((uPos - closest).sqrMagnitude <= hitRSq)
                            {
                                bestAlong = along;
                                hitPoint = uPos;
                            }
                        }
                    }
                }
                if (bestAlong < float.MaxValue) return true;
            }
            return false;
        }

        public void UpdateHoverSelection(bool active, bool isHolding, Vector3 aimPoint, float cursorRadius, int maxSelectCount)
        {
            Camera cam = Camera.main;
            Vector3 cPos = cam != null ? cam.transform.position : aimPoint + Vector3.up * 1.5f;
            Vector3 cFwd = cam != null ? cam.transform.forward : Vector3.down;
            UpdateHoverSelection(active, isHolding, aimPoint, cursorRadius, maxSelectCount, cPos, cFwd);
        }

        public void UpdateHoverSelection(bool active, bool isHolding, Vector3 aimPoint, float cursorRadius, int maxSelectCount, Vector3 camPos, Vector3 camFwd)
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

            if (!active || maxSelectCount <= 0 || _unitCount == 0 || cursorRadius <= 0.01f)
            {
                MaxSelectedCharge01 = 0f;
                return;
            }

            float rSq = cursorRadius * cursorRadius;
            int band = FloorBandForY(aimPoint.y);

            if (isHolding && _lockedIndices.Count > 0)
            {
                for (int i = _lockedIndices.Count - 1; i >= 0; i--)
                {
                    int idx = _lockedIndices[i];
                    if (idx < 0 || idx >= _unitCount || _units[idx].state != 0)
                    {
                        _lockedIndices.RemoveAt(i);
                        continue;
                    }
                    if (Mathf.Abs(_units[idx].pos.y - aimPoint.y) > 1.50f)
                    {
                        _lockedIndices.RemoveAt(i);
                        continue;
                    }
                    Vector3 d = _units[idx].pos - aimPoint;
                    d.y = 0f;
                    if (d.sqrMagnitude > rSq)
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

            if (_selectedIndices.Count < maxSelectCount)
            {
                _candidates.Clear();
                int minCx = Mathf.FloorToInt(aimPoint.x - cursorRadius);
                int maxCx = Mathf.FloorToInt(aimPoint.x + cursorRadius);
                int minCz = Mathf.FloorToInt(aimPoint.z - cursorRadius);
                int maxCz = Mathf.FloorToInt(aimPoint.z + cursorRadius);
                Vector3 fwdN = camFwd.sqrMagnitude > 0.001f ? camFwd.normalized : Vector3.down;

                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cz = minCz; cz <= maxCz; cz++)
                    {
                        if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                        List<int> list = bucket.exposed;
                        for (int b = 0; b < list.Count; b++)
                        {
                            int idx = list[b];
                            if (_units[idx].state != 0 || _selectedSet.Contains(idx)) continue;
                            Vector3 uPos = _units[idx].pos;
                            if (Mathf.Abs(uPos.y - aimPoint.y) > 1.50f) continue;

                            Vector3 dFlat = uPos - aimPoint;
                            dFlat.y = 0f;
                            if (dFlat.sqrMagnitude > rSq) continue;

                            Vector3 toUnit = uPos - camPos;
                            float along = Vector3.Dot(toUnit, fwdN);
                            if (along < 0.05f) continue;
                            Vector3 closestOnRay = camPos + fwdN * along;
                            float rayPerpSq = (uPos - closestOnRay).sqrMagnitude;
                            float topHeight = Mathf.Max(0f, uPos.y - aimPoint.y);
                            float score = rayPerpSq * 2.8f + along * 0.42f - topHeight * 0.35f;
                            _candidates.Add((idx, score));
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

        /// <summary>[v1.9] 단타 클릭(lmbDown) 시 즉시 차징 보너스를 줘서 톡!톡! 줍기 손맛 강화.</summary>
        public void QuickChargeSelectedOnClick(float boost01 = 0.65f)
        {
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx >= 0 && idx < _unitCount && _units[idx].state == 0)
                    _units[idx].holdCharge = Mathf.Min(1f, _units[idx].holdCharge + boost01);
            }
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

                if (manager != null && manager.ToolTier >= 2) ActivateUnitForCharge(idx);
                _units[idx].holdCharge = Mathf.Min(1f, _units[idx].holdCharge + dt * rate);
                if (_units[idx].holdCharge > maxC) maxC = _units[idx].holdCharge;

                if (_units[idx].holdCharge >= 1f)
                {
                    if (_units[idx].hp > 0f)
                    {
                        _units[idx].hp -= heavyDamage;
                        _units[idx].holdCharge = 0.50f;
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

            if (picked > 0 && chainChance > 0f && chainExtraCount > 0 && Random.value < chainChance)
            {
                int chained = ChainSuckAround(fxOrigin, 1.45f, suckDest, chainExtraCount);
                if (chained > 0) didChain = true;
            }

            MaxSelectedCharge01 = picked > 0 ? 0f : maxC;
            return picked;
        }

        public int ChainSuckAround(Vector3 origin, float radius, Transform dest, int maxCount)
        {
            if (_unitCount == 0 || maxCount <= 0 || radius <= 0.05f) return 0;
            if (dest != null) _suckDestination = dest;

            int picked = 0;
            int minCx = Mathf.FloorToInt(origin.x - radius);
            int maxCx = Mathf.FloorToInt(origin.x + radius);
            int minCz = Mathf.FloorToInt(origin.z - radius);
            int maxCz = Mathf.FloorToInt(origin.z + radius);
            int band = FloorBandForY(origin.y);
            float rSq = radius * radius;

            for (int cx = minCx; cx <= maxCx && picked < maxCount; cx++)
            {
                for (int cz = minCz; cz <= maxCz && picked < maxCount; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                    List<int> list = bucket.exposed;
                    for (int b = list.Count - 1; b >= 0 && picked < maxCount; b--)
                    {
                        int idx = list[b];
                        if (_units[idx].state != 0 || _units[idx].hp > 0f) continue;
                        Vector3 d = _units[idx].pos - origin;
                        if (Mathf.Abs(d.y) > 1.50f) continue;
                        d.y = 0f;
                        if (d.sqrMagnitude > rSq) continue;

                        float dist01 = Mathf.Clamp01(Mathf.Sqrt(d.sqrMagnitude) / Mathf.Max(0.2f, radius));
                        StartUnitSuckDelayed(idx, 2,
                            Random.Range(0f, 0.04f + dist01 * 0.14f),
                            Mathf.Lerp(0.26f, 0.44f, dist01),
                            Mathf.Lerp(1.55f, 0.85f, dist01));
                        picked++;
                    }
                }
            }
            return picked;
        }

        // ===================== [v1.9] 원기옥 압축 · 미니 블랙홀 대용량 고속 처리 =====================
        private Vector3 _orbCenter;
        private float _orbRadius = 2f;
        private float _orbDps = 5f;
        private bool _orbActive;
        private int _orbAbsorbedCount;
        private int _orbAbsorbedValue;
        private int _vortexAbsorbedCount;
        private int _vortexAbsorbedValue;

        public void SetOrbTarget(Vector3 center, float radius, float dps)
        {
            _orbCenter = center;
            _orbRadius = Mathf.Max(0.4f, radius);
            _orbDps = Mathf.Max(0.5f, dps);
            _orbActive = true;
        }

        public void ClearOrbTarget() => _orbActive = false;

        public int PullToOrb(int maxNew)
        {
            if (!_orbActive || maxNew <= 0 || _unitCount == 0) return 0;
            // [v2.0] 수거통 용량 엄수 — 가득 차면 즉시 흡입 중단!
            int binSpace = manager != null ? Mathf.Max(0, manager.BinCapacity - manager.CollectingCount) : maxNew;
            if (binSpace <= 0) return 0;
            int budget = Mathf.Min(maxNew, binSpace);
            int started = 0;
            float rSq = _orbRadius * _orbRadius;
            int band = FloorBandForY(_orbCenter.y);
            int minCx = Mathf.FloorToInt(_orbCenter.x - _orbRadius), maxCx = Mathf.FloorToInt(_orbCenter.x + _orbRadius);
            int minCz = Mathf.FloorToInt(_orbCenter.z - _orbRadius), maxCz = Mathf.FloorToInt(_orbCenter.z + _orbRadius);

            for (int cx = minCx; cx <= maxCx && started < budget; cx++)
            {
                for (int cz = minCz; cz <= maxCz && started < budget; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                    List<int> list = bucket.exposed;
                    for (int b = list.Count - 1; b >= 0 && started < budget; b--)
                    {
                        int idx = list[b];
                        if (_units[idx].state != 0) continue;
                        Vector3 d = _units[idx].pos - _orbCenter;
                        if (d.sqrMagnitude > rSq) continue;

                        RemoveFromCellAndPromote(idx);
                        if (_activeIndices.Count >= MaxDynamicFlyingVisuals && _units[idx].hp <= 0f)
                        {
                            _units[idx].state = 3;
                            _units[idx].inActiveList = false;
                            FinishAbsorb(idx, true);
                        }
                        else
                        {
                            _units[idx].state = 5;
                            _units[idx].timer = 0f;
                            _units[idx].holdCharge = 0f;
                            _units[idx].startPos = _units[idx].pos;
                            if (!_units[idx].inActiveList) { _units[idx].inActiveList = true; _activeIndices.Add(idx); }
                        }
                        started++;
                    }
                }
            }
            return started;
        }

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

        public void ClearBlackHoles() => _bhCount = 0;

        public int BlackHoleTick(int maxNew)
        {
            if (_bhCount <= 0 || maxNew <= 0 || _unitCount == 0) return 0;
            // [v2.0] 수거통 용량 엄수 — 절대 용량을 초과해 쌓이지 않는다!
            int binSpace = manager != null ? Mathf.Max(0, manager.BinCapacity - manager.CollectingCount) : maxNew;
            if (binSpace <= 0) return 0;
            int budget = Mathf.Min(maxNew, binSpace);
            int started = 0;
            float rSq = _bhRadius * _bhRadius;

            for (int h = 0; h < _bhCount && started < budget; h++)
            {
                Vector3 c = _bhCenters[h];
                int band = FloorBandForY(c.y);
                int minCx = Mathf.FloorToInt(c.x - _bhRadius), maxCx = Mathf.FloorToInt(c.x + _bhRadius);
                int minCz = Mathf.FloorToInt(c.z - _bhRadius), maxCz = Mathf.FloorToInt(c.z + _bhRadius);
                for (int cx = minCx; cx <= maxCx && started < budget; cx++)
                {
                    for (int cz = minCz; cz <= maxCz && started < budget; cz++)
                    {
                        if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                        List<int> list = bucket.exposed;
                        for (int b = list.Count - 1; b >= 0 && started < budget; b--)
                        {
                            int idx = list[b];
                            if (_units[idx].state != 0) continue;
                            Vector3 d = _units[idx].pos - c;
                            if (d.sqrMagnitude > rSq) continue;

                            RemoveFromCellAndPromote(idx);
                            if (_activeIndices.Count >= MaxDynamicFlyingVisuals && _units[idx].hp <= 0f)
                            {
                                _units[idx].state = 3;
                                _units[idx].inActiveList = false;
                                FinishAbsorb(idx, false);
                            }
                            else
                            {
                                _units[idx].state = 6;
                                _units[idx].timer = 0f;
                                _units[idx].suckDur = Random.Range(0.85f, 1.35f);
                                _units[idx].orbitIdx = (byte)h;
                                _units[idx].orbitAngle = Mathf.Atan2(d.z, d.x);
                                _units[idx].startPos = _units[idx].pos;
                                if (!_units[idx].inActiveList) { _units[idx].inActiveList = true; _activeIndices.Add(idx); }
                            }
                            started++;
                        }
                    }
                }
            }
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
            RemoveFromCellAndPromote(idx);
            if (_activeIndices.Count >= MaxDynamicFlyingVisuals)
            {
                CompleteImmediate(idx, newState);
                return;
            }
            _units[idx].state = newState;
            _units[idx].timer = -Mathf.Max(0f, delay);
            _units[idx].holdCharge = 0f;
            _units[idx].startPos = _units[idx].pos;
            _units[idx].isCritBonus = false;
            _units[idx].suckDur = Mathf.Max(0.12f, suckDur);
            _units[idx].arcHeight = arcHeight;
            if (!_units[idx].inActiveList) { _units[idx].inActiveList = true; _activeIndices.Add(idx); }
        }

        private void StartUnitSuck(int idx, byte newState, Vector3 destPos, bool critBonus)
        {
            RemoveFromCellAndPromote(idx);
            _units[idx].isCritBonus = critBonus;
            if (_activeIndices.Count >= MaxDynamicFlyingVisuals)
            {
                CompleteImmediate(idx, newState);
                return;
            }
            _units[idx].state = newState;
            _units[idx].timer = 0f;
            _units[idx].holdCharge = 0f;
            _units[idx].startPos = _units[idx].pos;
            float dist = Vector3.Distance(_units[idx].pos, destPos);
            float spd = (manager != null && manager.ToolTier >= 2) ? 15.5f : 13.0f;
            _units[idx].suckDur = Mathf.Clamp(dist / spd, 0.14f, 0.30f);
            _units[idx].arcHeight = Mathf.Clamp(0.24f + dist * 0.10f, 0.24f, 0.65f);
            if (!_units[idx].inActiveList) { _units[idx].inActiveList = true; _activeIndices.Add(idx); }
        }

        private void CompleteImmediate(int idx, byte st)
        {
            _units[idx].state = 3;
            _units[idx].inActiveList = false;
            _cleanedCount++;
            _freeSlots.Add(idx);
            NotifyUnitCleaned(_units[idx].roomIdx);
            if (st == 2 && manager != null)
            {
                int val = _units[idx].isCritBonus ? _units[idx].value * 2 : _units[idx].value;
                string label = _units[idx].typeIdx < meshTypes.Count ? meshTypes[_units[idx].typeIdx].label : "쓰레기";
                manager.OnInstancedTrashCollected(val, _units[idx].typeIdx, label);
            }
        }

        public bool TryFindNearestIdlePos(Vector3 fromPos, out Vector3 foundPos, float searchRadius = 3f)
        {
            foundPos = Vector3.zero;
            float best = float.MaxValue;
            int bestIdx = -1;
            int band = FloorBandForY(fromPos.y);
            int minCx = Mathf.FloorToInt(fromPos.x - searchRadius), maxCx = Mathf.FloorToInt(fromPos.x + searchRadius);
            int minCz = Mathf.FloorToInt(fromPos.z - searchRadius), maxCz = Mathf.FloorToInt(fromPos.z + searchRadius);
            float searchSq = searchRadius * searchRadius;

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                    List<int> list = bucket.exposed;
                    for (int b = 0; b < list.Count; b++)
                    {
                        int i = list[b];
                        if (_units[i].state != 0 || _units[i].hp > 0f) continue;
                        float dSq = (_units[i].pos - fromPos).sqrMagnitude;
                        if (dSq > searchSq) continue;
                        if (dSq < best) { best = dSq; bestIdx = i; }
                    }
                }
            }
            if (bestIdx >= 0) { foundPos = _units[bestIdx].pos; return true; }

            int step = Mathf.Max(1, _unitCount / 180);
            for (int i = 0; i < _unitCount; i += step)
            {
                if (_units[i].state != 0 || FloorBandForY(_units[i].pos.y) != band) continue;
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
            int band = FloorBandForY(robotPos.y);
            int minCx = Mathf.FloorToInt(robotPos.x - radius), maxCx = Mathf.FloorToInt(robotPos.x + radius);
            int minCz = Mathf.FloorToInt(robotPos.z - radius), maxCz = Mathf.FloorToInt(robotPos.z + radius);

            for (int cx = minCx; cx <= maxCx && eaten < maxEat; cx++)
            {
                for (int cz = minCz; cz <= maxCz && eaten < maxEat; cz++)
                {
                    if (!_grid.TryGetValue(PackCell(cx, cz, band), out CellBucket bucket)) continue;
                    List<int> list = bucket.exposed;
                    for (int b = list.Count - 1; b >= 0 && eaten < maxEat; b--)
                    {
                        int i = list[b];
                        if (_units[i].state != 0 || _units[i].hp > 0f) continue;
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
            return eaten;
        }

        private static void PushDynChunk(List<Matrix4x4[]>[] chunks, int[] counts, int tIdx, Matrix4x4 mtx)
        {
            int idx = counts[tIdx]++;
            int cIdx = idx / 1023;
            int lIdx = idx % 1023;
            while (chunks[tIdx].Count <= cIdx) chunks[tIdx].Add(new Matrix4x4[1023]);
            chunks[tIdx][cIdx][lIdx] = mtx;
        }

        private void Update()
        {
            if (_unitCount == 0 || meshTypes.Count == 0 || _dynChunks == null) return;

            float dt = Time.deltaTime;
            float now = Time.time;
            Vector3 playerDest = _suckDestination != null ? _suckDestination.position : Vector3.zero;
            Vector3 botDest = _robotSuckDest != null ? _robotSuckDest.position : Vector3.zero;

            for (int t = 0; t < _dynCounts.Length; t++)
            {
                _dynCounts[t] = 0;
                _outlineCounts[t] = 0;
            }

            // 1) 동적(진동·팝핑·흡입 중) 유닛만 업데이트!
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
                        _units[i].holdCharge = Mathf.MoveTowards(_units[i].holdCharge, 0f, dt * 2.8f);
                    }
                    float ch = _units[i].holdCharge;
                    if (ch <= 0.005f)
                    {
                        _units[i].holdCharge = 0f;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        AddToSector(i);
                        continue;
                    }

                    bool vacuumVibe = manager != null && manager.ToolTier >= 2;
                    Matrix4x4 mtx;
                    if (vacuumVibe)
                    {
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
                        mtx = Matrix4x4.TRS(rPos, rRot, Vector3.one * (_units[i].curScale * (1f + ch * 0.14f)));
                    }
                    else
                    {
                        mtx = Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale);
                    }
                    PushDynChunk(_dynChunks, _dynCounts, _units[i].typeIdx, mtx);

                    if (_selectedSet.Contains(i))
                    {
                        int oc = _outlineCounts[_units[i].typeIdx];
                        if (oc < 1023) { _outlineMatrices[_units[i].typeIdx][oc] = mtx; _outlineCounts[_units[i].typeIdx] = oc + 1; }
                    }
                }
                else if (st == 1)
                {
                    _units[i].timer += dt;
                    float p = Mathf.Clamp01(_units[i].timer / 0.42f);
                    Vector3 cur = Vector3.Lerp(_units[i].startPos, _units[i].endPos, p);
                    cur.y += Mathf.Sin(p * Mathf.PI) * 0.60f;
                    _units[i].pos = cur;
                    if (p >= 1f)
                    {
                        _units[i].pos = _units[i].endPos;
                        _units[i].state = 0;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        int newKey = CellKeyForPos(_units[i].pos);
                        _units[i].gridKey = newKey;
                        if (!_grid.TryGetValue(newKey, out CellBucket bkt)) { bkt = new CellBucket(); _grid[newKey] = bkt; }
                        if (!bkt.exposed.Contains(i)) bkt.exposed.Add(i);
                        AddToSector(i);
                    }
                    else
                    {
                        PushDynChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(cur, _units[i].rot, Vector3.one * _units[i].curScale));
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
                        _activeIndices.RemoveAt(k);
                        CompleteImmediate(i, st);
                    }
                    else
                    {
                        PushDynChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(cur, _units[i].rot, Vector3.one * _units[i].curScale));
                    }
                }
                else if (st == 5)
                {
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
                        _units[i].pos = Vector3.MoveTowards(_units[i].pos, center, 12.5f * dt);
                    }
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 520f * dt, 240f * dt);
                    dist = (_units[i].pos - center).magnitude;
                    if (dist < 0.32f || (dist < 0.75f && _units[i].hp <= 0f))
                    {
                        _units[i].state = 3;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        FinishAbsorb(i, true);
                        continue;
                    }
                    PushDynChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale));
                }
                else if (st == 6)
                {
                    _units[i].timer += dt;
                    float t = Mathf.Clamp01(_units[i].timer / Mathf.Max(0.35f, _units[i].suckDur));
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
                    if (t >= 1f || (_units[i].hp <= 0f && t >= 0.62f))
                    {
                        _units[i].state = 3;
                        _units[i].inActiveList = false;
                        _activeIndices.RemoveAt(k);
                        FinishAbsorb(i, false);
                        continue;
                    }
                    PushDynChunk(_dynChunks, _dynCounts, _units[i].typeIdx, Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale));
                }
            }

            // 2) 호버 선택된 정적 노출 유닛 하얀 외곽선 수집
            for (int k = 0; k < _selectedIndices.Count; k++)
            {
                int idx = _selectedIndices[k];
                if (idx < 0 || idx >= _unitCount || _units[idx].state != 0 || _units[idx].inActiveList) continue;
                int tIdx = _units[idx].typeIdx;
                int oc = _outlineCounts[tIdx];
                if (oc < 1023)
                {
                    _outlineMatrices[tIdx][oc] = Matrix4x4.TRS(_units[idx].pos, _units[idx].rot, Vector3.one * _units[idx].curScale);
                    _outlineCounts[tIdx] = oc + 1;
                }
            }

            // 3) 절두체 + 층간 컬링으로 보이는 공간 섹터만 초고속 GPU Instanced 렌더!
            Camera mainCam = Camera.main;
            bool hasFrustum = mainCam != null;
            float camY = hasFrustum ? mainCam.transform.position.y : 1.5f;
            if (hasFrustum) GeometryUtility.CalculateFrustumPlanes(mainCam, _frustumPlanes);

            for (int s = 0; s < _sectors.Count; s++)
            {
                RenderSector sec = _sectors[s];
                if (hasFrustum)
                {
                    if (_currentStageLayout == 7 && Mathf.Abs(sec.center.y - camY) > 3.6f) continue;
                    if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, sec.bounds)) continue;
                }
                RenderChunked(sec.chunks, sec.counts, _renderParams);
            }

            // 4) 동적 유닛 GPU Instanced 렌더!
            RenderChunked(_dynChunks, _dynCounts, _renderParams);

            // 5) 호버 하얀 테두리
            if (_outlineMat != null)
            {
                for (int t = 0; t < meshTypes.Count; t++)
                {
                    int ocnt = _outlineCounts[t];
                    if (ocnt > 0 && meshTypes[t].mesh != null)
                        Graphics.RenderMeshInstanced(_outlineRenderParams, meshTypes[t].mesh, 0, _outlineMatrices[t], ocnt);
                }
            }

            // 6) 바닥 커서 링
            if (_auraRingMesh != null)
            {
                if (_cursorRingVisible && _cursorRingMat != null && _cursorRingRadius > 0.04f)
                {
                    Color c = _isHoldingNow ? new Color(1.0f, 0.88f, 0.28f, 0.82f) : new Color(1.0f, 0.96f, 0.86f, 0.45f);
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
                    Graphics.RenderMeshInstanced(rps[t], meshTypes[t].mesh, 0, chunks[t][c], 1023);
                if (rem > 0)
                    Graphics.RenderMeshInstanced(rps[t], meshTypes[t].mesh, 0, chunks[t][full], rem);
            }
        }
    }
}
