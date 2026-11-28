using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.9 강화 효과 enum (정예 80 마이크로 + 코어 28 = 108노드).
    /// 1회 강화당 +8%~+20% 고체감 상승으로 매 클릭마다 확실히 강해지는 인크리멘탈 도파민 설계!
    /// </summary>
    public enum V18Effect
    {
        HoldSpeed = 0,
        MoveSpeed = 1,
        Radius = 2,
        GrabCount = 3,
        CritChance = 4,
        ChainChance = 5,
        BinCap = 6,
        Price = 7,
        ComboWindow = 8,
        SettleBonus = 9,
        FistPower = 10,
        FistCritChance = 11,
        FistCritMult = 12,
        BurstFever = 13,
        BurstCash = 14,
        ChainUnlock = 15,
        ChainRadius = 16,
        ChainCount = 17,
        ScrubSpeed = 18,
        ScrubRange = 19,
        StainReward = 20,
        ScrubFever = 21,
        MaskFilter = 22,
        StenchSlowResist = 23,
        BlurReduce = 24,
        OxygenUnlock = 25,
        OxygenPower = 26,
        OxygenMove = 27,
        OxygenFeverHold = 28,
        FeverGain = 29,
        FeverHold = 30,
        FeverMax = 31,
        FeverPrice = 32,
        RoombaSpeed = 33,
        RoombaPickup = 34,
        RoombaDetect = 35,
        RoombaBattery = 36,
        VehicleBulk = 37,
        VehicleMult = 38,
        BulkThreshold = 39,
        BulkSettle = 40,
        RemoteCooldown = 41,
        TwoHand = 42,
        OrbUnlock = 43,
        OrbPull = 44,
        OrbRadius = 45,
        OrbCharge = 46,
        OrbSize = 47,
        OrbCash = 48,
        OrbBurst = 49,
        OrbFly = 50,
        OrbAuto = 51,
        BhUnlock = 52,
        BhRadius = 53,
        BhCount = 54,
        BhSpin = 55,
        BhDuration = 56,
        WhUnlock = 57,
        WhCash = 58,
        BhPull = 59,
        WhAuto = 60,
        TrashCan = 61,
        TrashEwaste = 62,
        TrashBig = 63,
        TrashGold = 64,
        TrashRatio = 65,
        TrashDurability = 66,
        HeavyDamage = 67,
        HeavyValue = 68,
        SortBonus = 69,
        RarityBonus = 70,
        HeavyJackpot = 71,
        TrashDensity = 72,
        TrashCluster = 73,
        WaveBonus = 74,
        MegaDump = 75,
        Count = 76
    }

    public class V18Node
    {
        public string id;
        public string name;
        public string title;
        public string desc;
        public int branch;
        public int lane;
        public int tier;
        public Vector2 pos;
        public int maxLevel;
        public int baseCost;
        public float growth;
        public string prereq;
        public int prereqLevel;
        public bool milestone;
        public V18Effect effect;
        public float perLevel;

        public string EffectText(int level)
        {
            float v = perLevel * Mathf.Max(1, level);
            switch (effect)
            {
                case V18Effect.HoldSpeed: return $"흡입 속도 +{v * 100f:0.#}%";
                case V18Effect.MoveSpeed: return $"이동 속도 +{v * 100f:0.#}%";
                case V18Effect.Radius: return $"흡입 반경·사거리 +{v * 100f:0.#}%";
                case V18Effect.GrabCount: return $"동시 흡입 +{v:0}개";
                case V18Effect.CritChance: return $"치명타 확률 +{v * 100f:0.#}%";
                case V18Effect.ChainChance: return $"연쇄 흡입 확률 +{v * 100f:0.#}%";
                case V18Effect.BinCap: return $"수거통 용량 +{v * 100f:0.#}%";
                case V18Effect.Price: return $"정산 단가 +{v * 100f:0.#}%";
                case V18Effect.ComboWindow: return $"콤보 유지 +{v:0.0}초";
                case V18Effect.SettleBonus: return $"정산 보너스 +{v * 100f:0.#}%";
                case V18Effect.FistPower: return $"봉투·박스 파괴력 +{v * 100f:0.#}%";
                case V18Effect.FistCritChance: return $"봉투 치명타 확률 +{v * 100f:0.#}%";
                case V18Effect.FistCritMult: return $"봉투 치명타 배율 +{v * 100f:0.#}%";
                case V18Effect.BurstFever: return $"봉투 피버 충전 +{v * 100f:0.#}%";
                case V18Effect.BurstCash: return $"봉투 보너스 +${v:0}";
                case V18Effect.ChainUnlock: return level > 0 ? "봉투 파열 연쇄 흡입 해금!" : "연쇄 흡입 잠김";
                case V18Effect.ChainRadius: return $"연쇄 흡입 반경 +{v:0.0}m";
                case V18Effect.ChainCount: return $"연쇄 흡입 +{v:0}개";
                case V18Effect.ScrubSpeed: return $"걸레·세척 속도 +{v * 100f:0.#}%";
                case V18Effect.ScrubRange: return $"걸레 범위 +{v * 100f:0.#}%";
                case V18Effect.StainReward: return $"얼룩 정산 +{v * 100f:0.#}%";
                case V18Effect.ScrubFever: return $"걸레 피버 충전 +{v * 100f:0.#}%";
                case V18Effect.MaskFilter: return $"악취 상승 둔화 +{v * 100f:0.#}%";
                case V18Effect.StenchSlowResist: return $"악취 이속 둔화 저항 +{v * 100f:0.#}%";
                case V18Effect.BlurReduce: return $"시야 흐림 완화 +{v * 100f:0.#}%";
                case V18Effect.OxygenUnlock: return level > 0 ? "저악취 산소 부스트 해금!" : "산소 부스트 잠김";
                case V18Effect.OxygenPower: return $"산소 흡입 가속 +{v * 100f:0.#}%";
                case V18Effect.OxygenMove: return $"산소 이동 가속 +{v * 100f:0.#}%";
                case V18Effect.OxygenFeverHold: return $"산소 피버 유지 +{v * 100f:0.#}%";
                case V18Effect.FeverGain: return $"피버 충전 +{v * 100f:0.#}%";
                case V18Effect.FeverHold: return $"피버 지속 +{v * 100f:0.#}%";
                case V18Effect.FeverMax: return $"피버 배율 +{v * 100f:0.#}%";
                case V18Effect.FeverPrice: return $"피버 정산 +{v * 100f:0.#}%";
                case V18Effect.RoombaSpeed: return $"로봇 이동 속도 +{v * 100f:0.#}%";
                case V18Effect.RoombaPickup: return $"로봇 수거 가속 +{v * 100f:0.#}%";
                case V18Effect.RoombaDetect: return $"로봇 탐지 +{v:0.0}m";
                case V18Effect.RoombaBattery: return $"로봇 효율 +{v * 100f:0.#}%";
                case V18Effect.VehicleBulk: return $"차량 정산 배수 +{v * 100f:0.#}%";
                case V18Effect.VehicleMult: return $"차량 거래 배수 +{v * 100f:0.#}%";
                case V18Effect.BulkThreshold: return $"원격 기준 -{v * 100f:0.#}%";
                case V18Effect.BulkSettle: return $"대량 정산 +{v * 100f:0.#}%";
                case V18Effect.RemoteCooldown: return $"원격 쿨다운 -{v * 100f:0.#}%";
                case V18Effect.TwoHand: return level > 0 ? "양손 교차 줍기 해금 (+1개)!" : "양손 줍기 잠김";
                case V18Effect.OrbUnlock: return level > 0 ? "4단계 장비: 산업용 사이클론 청소기 해금!" : "사이클론 잠김";
                case V18Effect.OrbPull: return $"사이클론 흡입력 +{v * 100f:0.#}%";
                case V18Effect.OrbRadius: return $"사이클론 반경 +{v * 100f:0.#}%";
                case V18Effect.OrbCharge: return $"사이클론 출력 +{v * 100f:0.#}%";
                case V18Effect.OrbSize: return $"기류 확장 +{v * 100f:0.#}%";
                case V18Effect.OrbCash: return $"사이클론 정산 +{v * 100f:0.#}%";
                case V18Effect.OrbBurst: return $"터빈 충격 +{v * 100f:0.#}%";
                case V18Effect.OrbFly: return $"회오리 가속 +{v * 100f:0.#}%";
                case V18Effect.OrbAuto: return level > 0 ? "사이클론 터보 과급 해금!" : "터보 과급 잠김";
                case V18Effect.BhUnlock: return level > 0 ? "5단계 장비: 자율 청소 드론 편대 해금!" : "청소 드론 잠김";
                case V18Effect.BhRadius: return $"드론 탐지 반경 +{v * 100f:0.#}%";
                case V18Effect.BhCount: return $"동시 전개 드론 +{v:0}대";
                case V18Effect.BhSpin: return $"드론 선회 속도 +{v * 100f:0.#}%";
                case V18Effect.BhDuration: return $"드론 배터리 +{v * 100f:0.#}%";
                case V18Effect.WhUnlock: return level > 0 ? "트럭 직결 메가 호스 해금!" : "직결 호스 잠김";
                case V18Effect.WhCash: return $"직결 호스 정산 +{v * 100f:0.#}%";
                case V18Effect.BhPull: return $"드론 흡입력 +{v * 100f:0.#}%";
                case V18Effect.WhAuto: return level > 0 ? "초고속 직결 자동 정산!" : "초고속 직결 잠김";
                case V18Effect.TrashCan: return level > 0 ? "고가치 캔·병 폐기물 해금!" : "캔·병 잠김";
                case V18Effect.TrashEwaste: return level > 0 ? "고가치 전자폐기물 해금!" : "전자폐기물 잠김";
                case V18Effect.TrashBig: return level > 0 ? "초고가 대형 폐가전 해금!" : "대형 폐가전 잠김";
                case V18Effect.TrashGold: return level > 0 ? "숨겨진 금괴 폐기물 해금!" : "금괴 잠김";
                case V18Effect.TrashRatio: return $"고가치 등장 +{v * 100f:0.#}%";
                case V18Effect.TrashDurability: return $"내구물 비중 +{v * 100f:0.#}%";
                case V18Effect.HeavyDamage: return $"파쇄력 +{v * 100f:0.#}%";
                case V18Effect.HeavyValue: return $"고가치 단가 +{v * 100f:0.#}%";
                case V18Effect.SortBonus: return $"분리수거 보너스 +{v * 100f:0.#}%";
                case V18Effect.RarityBonus: return $"등급 보정 +{v * 100f:0.#}%";
                case V18Effect.HeavyJackpot: return $"금괴 잭팟 +{v * 100f:0.#}%";
                case V18Effect.TrashDensity: return $"추가 물량 +{v * 100f:0.#}%";
                case V18Effect.TrashCluster: return $"더미 밀집 +{v * 100f:0.#}%";
                case V18Effect.WaveBonus: return $"보너스 투입 +{v:0}";
                case V18Effect.MegaDump: return level > 0 ? "메가 투하 해금!" : "대량 투하 잠김";
                default: return "";
            }
        }
    }

    public struct BranchDef
    {
        public string id;
        public string label;
        public Color color;
        public int angle;
    }

    /// <summary>
    /// v1.9 정예 108노드 강화 트리 (코어 28 + 고체감 정예 마이크로 80).
    /// - 10계열 × 2레인 × 4티어 = 80노드
    /// - 코어 영역([-560..560, -145..145]) 바깥에 배치해 절대 겹치지 않는다.
    /// </summary>
    public static class UpgradeTreeV18
    {
        public const int MicroNodeCount = 80;
        public const int CoreNodeCount = 28;
        public const int TotalNodeCount = MicroNodeCount + CoreNodeCount;
        public const int BranchCount = 10;

        // [v2.3] 중앙 기준 4방향 특색별 명확한 구역 배치!
        // - 왼쪽(-X, 155~205°): 청소기·흡입·사이클론·드론·피버
        // - 오른쪽(+X, -25~+25°): 걸레·얼룩·방독면·산소·기동
        // - 아래쪽(+Y, 62~118°): 쓰레기통 용량·단가·차량·고가치 보물
        // - 위쪽(-Y, -118~-62°): 로봇청소기·주먹 파괴·봉투 연쇄
        public static readonly BranchDef[] Branches =
        {
            new BranchDef { id = "suction", label = "흡입·손",       color = new Color(0.320f, 0.840f, 0.940f), angle = 180 }, // 왼쪽 중앙
            new BranchDef { id = "bag",     label = "봉투·파괴",     color = new Color(0.980f, 0.740f, 0.240f), angle = -112 }, // 위쪽 좌
            new BranchDef { id = "bin",     label = "수거·정산",     color = new Color(0.360f, 0.900f, 0.540f), angle = 90 },  // 아래쪽 중앙
            new BranchDef { id = "vehicle", label = "차량·운송",     color = new Color(0.680f, 0.760f, 0.880f), angle = 64 },  // 아래쪽 우
            new BranchDef { id = "gear",    label = "사이클론·드론", color = new Color(0.220f, 0.960f, 0.860f), angle = 154 }, // 왼쪽 위
            new BranchDef { id = "roomba",  label = "로봇청소기",    color = new Color(0.720f, 0.560f, 0.960f), angle = -68 }, // 위쪽 우
            new BranchDef { id = "fever",   label = "피버·콤보",     color = new Color(0.980f, 0.480f, 0.280f), angle = 206 }, // 왼쪽 아래
            new BranchDef { id = "oxygen",  label = "방독면·산소",   color = new Color(0.400f, 0.880f, 0.680f), angle = -18 }, // 오른쪽 위
            new BranchDef { id = "stain",   label = "얼룩·걸레",     color = new Color(0.340f, 0.640f, 0.980f), angle = 18 },  // 오른쪽 아래
            new BranchDef { id = "trash",   label = "고가치·보물",   color = new Color(0.900f, 0.680f, 0.260f), angle = 116 }, // 아래쪽 좌
        };

        private static Vector2 RingPos(int branchIdx, int lane, int tier)
        {
            // [v2.3] 4방향 특색 구역 격자(76px) 직각 가지 배치
            float baseDeg = Branches[branchIdx].angle;
            bool horiz = Mathf.Abs(Mathf.Cos(baseDeg * Mathf.Deg2Rad)) > 0.72f;
            float r0 = horiz ? 456f : 340f;
            float rStep = 84f;
            float spreadDeg = 7.6f;
            float deg = baseDeg + (lane == 0 ? -spreadDeg : +spreadDeg);
            float rad = deg * Mathf.Deg2Rad;
            float r = r0 + tier * rStep;
            float gx = Mathf.Round((Mathf.Cos(rad) * r) / 76f) * 76f;
            float gy = Mathf.Round((Mathf.Sin(rad) * r) / 76f) * 76f;
            return new Vector2(gx, gy);
        }

        private static V18Node N(string id, string name, string title, string desc,
            int branch, int lane, int tier, int maxLv, int cost, float growth,
            string prereq, int prereqLv, bool milestone, V18Effect eff, float perLv)
        {
            return new V18Node
            {
                id = id, name = name, title = title, desc = desc,
                branch = branch, lane = lane, tier = tier,
                pos = RingPos(branch, lane, tier),
                maxLevel = maxLv, baseCost = cost, growth = growth,
                prereq = prereq, prereqLevel = prereqLv,
                milestone = milestone, effect = eff, perLevel = perLv
            };
        }

        public static readonly V18Node[] Nodes =
        {
            // 0: 흡입·손 (파워·범위/양손·치명)
            N("suction_0_0", "손놀림 I",    "손놀림 I · 흡입 가속",   "흡입 속도 +14%",           0, 0, 0, 3, 180,     1.85f, "hold1", 1, false, V18Effect.HoldSpeed, 0.14f),
            N("suction_0_1", "넓은 링 I",   "넓은 링 I · 범위/사거리", "흡입 반경·사거리 +15%",    0, 0, 1, 3, 4500,    2.00f, "suction_0_0", 1, false, V18Effect.Radius, 0.15f),
            N("suction_0_2", "터보 흡입",   "터보 흡입 · 고속 빨아들임","흡입 속도 +18%",           0, 0, 2, 3, 75000,   2.15f, "suction_0_1", 1, false, V18Effect.HoldSpeed, 0.18f),
            N("suction_0_3", "회오리 돔",   "회오리 돔 · 극대 반경",   "흡입 반경·사거리 +20%",    0, 0, 3, 3, 1200000, 2.25f, "suction_0_2", 1, true,  V18Effect.Radius, 0.20f),

            N("suction_1_0", "양손 줍기",   "★ 양손 교차 줍기",       "왼손+오른손 동시 +1개",    0, 1, 0, 1, 320,     1.00f, "rad1", 1, false, V18Effect.TwoHand, 1.0f),
            N("suction_1_1", "멀티 집기",   "멀티 집기 · 동시 수거",   "동시 흡입 +2개",           0, 1, 1, 3, 6200,    2.05f, "suction_1_0", 1, false, V18Effect.GrabCount, 2.0f),
            N("suction_1_2", "황금 손맛",   "황금 손맛 · 2배 치명타",  "수거 치명타 +12%",         0, 1, 2, 3, 95000,   2.15f, "suction_1_1", 1, false, V18Effect.CritChance, 0.12f),
            N("suction_1_3", "연쇄 폭풍",   "★ 연쇄 폭풍 흡입",       "주변 쓰레기 연쇄 흡입!",   0, 1, 3, 3, 1600000, 2.25f, "suction_1_2", 1, true,  V18Effect.ChainChance, 0.15f),

            // 1: 봉투·파괴
            N("bag_0_0", "너클 글러브",     "너클 글러브 · 파괴력",    "봉투·박스 파괴력 +22%",    1, 0, 0, 3, 240,     1.85f, "fist1", 1, false, V18Effect.FistPower, 0.22f),
            N("bag_0_1", "급소 일격",       "급소 일격 · 봉투 치명타", "봉투 치명타 +18%",         1, 0, 1, 3, 5200,    2.00f, "bag_0_0", 1, false, V18Effect.FistCritChance, 0.18f),
            N("bag_0_2", "중량 해머",       "중량 해머 · 치명 배율",   "치명타 배율 +45%",         1, 0, 2, 3, 85000,   2.15f, "bag_0_1", 1, false, V18Effect.FistCritMult, 0.45f),
            N("bag_0_3", "분쇄 철권",       "★ 분쇄 철권 · 초파괴",    "봉투·박스 파괴력 +35%",    1, 0, 3, 3, 1400000, 2.25f, "bag_0_2", 1, true,  V18Effect.FistPower, 0.35f),

            N("bag_1_0", "터지는 쾌감",     "터지는 쾌감 · 피버 충전", "봉투 파열 시 피버 +18%",   1, 1, 0, 3, 450,     1.90f, "fist1", 2, false, V18Effect.BurstFever, 0.18f),
            N("bag_1_1", "연쇄 흡입파",     "★ 연쇄 흡입파 해금",      "봉투 터질 때 주변 자동흡입",1, 1, 1, 1, 6800,   1.00f, "bag_1_0", 1, false, V18Effect.ChainUnlock, 1.0f),
            N("bag_1_2", "연쇄 범위",       "연쇄 범위 · 대량 흡입",   "파열 연쇄 반경 +0.9m",     1, 1, 2, 3, 110000,  2.15f, "bag_1_1", 1, false, V18Effect.ChainRadius, 0.9f),
            N("bag_1_3", "잭팟 봉투",       "★ 잭팟 봉투 · 현금폭발",  "봉투 파열 시 +$250",       1, 1, 3, 3, 1800000, 2.25f, "bag_1_2", 1, true,  V18Effect.BurstCash, 250.0f),

            // 2: 수거·정산
            N("bin_0_0", "압축 적재 I",     "압축 적재 I · 용량",      "수거통 용량 +18%",         2, 0, 0, 3, 220,     1.85f, "bin1", 1, false, V18Effect.BinCap, 0.18f),
            N("bin_0_1", "초고압 압축",     "초고압 압축 · 대용량",    "수거통 용량 +24%",         2, 0, 1, 3, 5800,    2.05f, "bin_0_0", 1, false, V18Effect.BinCap, 0.24f),
            N("bin_0_2", "만재 보너스",     "만재 보너스 · 대량 정산", "만재 정산 +16%",           2, 0, 2, 3, 98000,   2.15f, "bin_0_1", 1, false, V18Effect.BulkSettle, 0.16f),
            N("bin_0_3", "대형 포대",       "★ 초대형 포대 · 용량 완성","수거통 용량 +32%",        2, 0, 3, 3, 1900000, 2.30f, "bin_0_2", 1, true,  V18Effect.BinCap, 0.32f),

            N("bin_1_0", "분리수거 I",      "분리수거 I · 단가 상승",  "쓰레기 정산 단가 +14%",    2, 1, 0, 3, 260,     1.90f, "price1", 1, false, V18Effect.Price, 0.14f),
            N("bin_1_1", "단골 계약",       "단골 계약 · 영수증 가산", "정산 총액 +14%",           2, 1, 1, 3, 6800,    2.05f, "bin_1_0", 1, false, V18Effect.SettleBonus, 0.14f),
            N("bin_1_2", "프리미엄 매입",   "프리미엄 매입 · 단가",    "쓰레기 정산 단가 +18%",    2, 1, 2, 3, 125000,  2.20f, "bin_1_1", 1, false, V18Effect.Price, 0.18f),
            N("bin_1_3", "V.I.P 정산",      "★ V.I.P 정산 · 황금단가", "정산 총액 +22%",           2, 1, 3, 3, 2400000, 2.30f, "bin_1_2", 1, true,  V18Effect.SettleBonus, 0.22f),

            // 3: 차량·운송
            N("vehicle_0_0", "차량 적재 강화","차량 정산 배수 I",       "차량 정산 배수 +15%",      3, 0, 0, 3, 1400,    1.95f, "cart", 1, false, V18Effect.VehicleBulk, 0.15f),
            N("vehicle_0_1", "특수 폐기 계약","차량 거래 배수 I",       "차량 거래 배수 +16%",      3, 0, 1, 3, 22000,   2.10f, "vehicle_0_0", 1, false, V18Effect.VehicleMult, 0.16f),
            N("vehicle_0_2", "물류 대기업",  "차량 정산 배수 II",       "차량 정산 배수 +22%",      3, 0, 2, 3, 280000,  2.20f, "vehicle_0_1", 1, false, V18Effect.VehicleBulk, 0.22f),
            N("vehicle_0_3", "광역 트럭 계약","★ 광역 트럭 마스터",    "차량 거래 배수 +28%",      3, 0, 3, 3, 3800000, 2.30f, "vehicle_0_2", 1, true,  V18Effect.VehicleMult, 0.28f),

            N("vehicle_1_0", "빠른 상차",    "원격 기준 하향",          "원격 자동수거 기준 -12%",  3, 1, 0, 3, 12000,   2.00f, "damas", 1, false, V18Effect.BulkThreshold, 0.12f),
            N("vehicle_1_1", "고속 리프트",  "원격 쿨다운 단축",        "원격 수거 쿨다운 -18%",    3, 1, 1, 3, 85000,   2.15f, "vehicle_1_0", 1, false, V18Effect.RemoteCooldown, 0.18f),
            N("vehicle_1_2", "풀로드 보너스","대량 상차 보너스",        "만재 정산 +20%",           3, 1, 2, 3, 480000,  2.20f, "vehicle_1_1", 1, false, V18Effect.BulkSettle, 0.20f),
            N("vehicle_1_3", "초고속 물류",  "★ 초고속 물류 완성",     "원격 수거 쿨다운 -22%",    3, 1, 3, 3, 5200000, 2.30f, "vehicle_1_2", 1, true,  V18Effect.RemoteCooldown, 0.22f),

            // 4: 사이클론·드론 (4·5티어 최종 특수청소 장비 계보!)
            N("gear_0_0", "★ 사이클론 청소기","★ 4단계: 산업용 사이클론 해금", "강력 회오리 기류로 광역 흡입!", 4, 0, 0, 1, 650000,   1.00f, "vacuum", 1, true,  V18Effect.OrbUnlock, 1.0f),
            N("gear_0_1", "광역 회오리",     "사이클론 반경 확대",           "사이클론 반경 +20% 확장",       4, 0, 1, 3, 1100000,  2.10f, "gear_0_0", 1, false, V18Effect.OrbRadius, 0.20f),
            N("gear_0_2", "고압 터빈",       "사이클론 흡입 출력",           "사이클론 흡입력 +35% 강화",     4, 0, 2, 3, 2400000,  2.15f, "gear_0_1", 1, false, V18Effect.OrbCharge, 0.35f),
            N("gear_0_3", "터보 과급기",     "★ 사이클론 터보 과급 완성",   "초고속 회오리 과급 모드!",      4, 0, 3, 1, 5500000,  1.00f, "gear_0_2", 1, true,  V18Effect.OrbAuto, 1.0f),

            N("gear_1_0", "★ 청소 드론 편대","★ 5단계: 자율 청소 드론 해금", "청소 드론 1대가 주변 자동 흡입!",4, 1, 0, 1, 6500000,  1.00f, "gear_0_1", 1, true,  V18Effect.BhUnlock, 1.0f),
            N("gear_1_1", "광역 드론 센서",  "드론 탐지 반경 확대",          "드론 흡입 반경 +25% 확장",      4, 1, 1, 3, 9500000,  2.10f, "gear_1_0", 1, false, V18Effect.BhRadius, 0.25f),
            N("gear_1_2", "삼중 드론 편대",  "★ 다중 드론 편대 (+2대)",      "드론 최대 3대 동시 전개!",      4, 1, 2, 2, 18000000, 2.20f, "gear_1_1", 1, false, V18Effect.BhCount, 1.0f),
            N("gear_1_3", "★ 직결 메가 호스","★ 최종기: 트럭 직결 호스 정산","수거통 75% 시 트럭 직결 정산!", 4, 1, 3, 1, 45000000, 1.00f, "gear_1_2", 1, true,  V18Effect.WhUnlock, 1.0f),

            // 5: 로봇청소기
            N("roomba_0_0", "고속 모터 I",    "로봇 주행 가속 I",         "로봇 이동 속도 +20%",       5, 0, 0, 3, 2400,    1.95f, "roomba", 1, false, V18Effect.RoombaSpeed, 0.20f),
            N("roomba_0_1", "레이저 라이다",  "로봇 탐지 확장",           "로봇 탐지 반경 +0.6m",      5, 0, 1, 3, 18000,   2.10f, "roomba_0_0", 1, false, V18Effect.RoombaDetect, 0.60f),
            N("roomba_0_2", "터보 흡입팬",    "로봇 수거 가속",           "로봇 수거 주기 -14%",       5, 0, 2, 3, 180000,  2.20f, "roomba_0_1", 1, false, V18Effect.RoombaPickup, 0.14f),
            N("roomba_0_3", "인공지능 AI",    "★ AI 초고속 로봇",         "로봇 이동 속도 +30%",       5, 0, 3, 3, 2200000, 2.25f, "roomba_0_2", 1, true,  V18Effect.RoombaSpeed, 0.30f),

            N("roomba_1_0", "광역 흡입구",    "로봇 수거량 확장",         "로봇 배터리 효율 +12%",     5, 1, 0, 3, 2800,    1.95f, "roomba", 1, false, V18Effect.RoombaBattery, 0.12f),
            N("roomba_1_1", "배터리 팩",      "대용량 배터리",            "로봇 수거 주기 -12%",       5, 1, 1, 3, 24000,   2.10f, "roomba_1_0", 1, false, V18Effect.RoombaPickup, 0.12f),
            N("roomba_1_2", "초음파 센서",    "장거리 센서",              "로봇 탐지 반경 +0.8m",      5, 1, 2, 3, 240000,  2.20f, "roomba_1_1", 1, false, V18Effect.RoombaDetect, 0.80f),
            N("roomba_1_3", "핵융합 셀",      "★ 무한 동력 로봇",         "로봇 효율 +16%",            5, 1, 3, 3, 2800000, 2.25f, "roomba_1_2", 1, true,  V18Effect.RoombaBattery, 0.16f),

            // 6: 피버·콤보
            N("fever_0_0", "아드레날린",      "피버 게이지 가속",         "피버 게이지 충전 +25%",     6, 0, 0, 3, 2200,    1.95f, "fever", 1, false, V18Effect.FeverGain, 0.25f),
            N("fever_0_1", "지속 호흡",       "피버 유지 연장",           "피버 감소 완화 +22%",       6, 0, 1, 3, 19000,   2.10f, "fever_0_0", 1, false, V18Effect.FeverHold, 0.22f),
            N("fever_0_2", "한계 돌파",       "피버 배율 증폭",           "피버 보너스 배율 +25%",     6, 0, 2, 3, 190000,  2.20f, "fever_0_1", 1, false, V18Effect.FeverMax, 0.25f),
            N("fever_0_3", "황금 피버",       "★ 황금 피버 정산",         "피버 중 정산 단가 +25%",    6, 0, 3, 3, 2500000, 2.25f, "fever_0_2", 1, true,  V18Effect.FeverPrice, 0.25f),

            N("fever_1_0", "콤보 타이머",     "정산 콤보 연장 I",         "정산 콤보 유지 +2.0초",     6, 1, 0, 3, 1600,    1.95f, "combo", 1, false, V18Effect.ComboWindow, 2.0f),
            N("fever_1_1", "연속 리듬",       "정산 콤보 보너스",         "정산 총액 +14%",            6, 1, 1, 3, 16000,   2.10f, "fever_1_0", 1, false, V18Effect.SettleBonus, 0.14f),
            N("fever_1_2", "콤보 마스터",     "정산 콤보 연장 II",        "정산 콤보 유지 +3.0초",     6, 1, 2, 3, 165000,  2.20f, "fever_1_1", 1, false, V18Effect.ComboWindow, 3.0f),
            N("fever_1_3", "광란의 정산",     "★ 광란 피버·콤보",         "피버 배율 +25%",            6, 1, 3, 3, 2300000, 2.25f, "fever_1_2", 1, true,  V18Effect.FeverMax, 0.25f),

            // 7: 방독면·산소
            N("oxygen_0_0", "활성탄 필터",    "악취 상승 둔화",           "악취 상승 속도 -15%",       7, 0, 0, 3, 320,     1.90f, "mask1", 1, false, V18Effect.MaskFilter, 0.15f),
            N("oxygen_0_1", "강철 폐활량",    "악취 둔화 저항",           "악취 이동감속 저항 +18%",   7, 0, 1, 3, 6500,    2.05f, "oxygen_0_0", 1, false, V18Effect.StenchSlowResist, 0.18f),
            N("oxygen_0_2", "김서림 방지",    "시야 흐림 제거",           "악취 시야흐림 완화 +20%",   7, 0, 2, 3, 88000,   2.15f, "oxygen_0_1", 1, false, V18Effect.BlurReduce, 0.20f),
            N("oxygen_0_3", "특수 방독면",    "★ 완전 방독 완료",         "악취 상승 둔화 -18%",       7, 0, 3, 3, 1350000, 2.25f, "oxygen_0_2", 1, true,  V18Effect.MaskFilter, 0.18f),

            N("oxygen_1_0", "★ 산소 부스트",  "★ 저악취 산소 부스트 해금","쾌적할 때 작업·이동 가속!", 7, 1, 0, 1, 750,     1.00f, "mask1", 2, false, V18Effect.OxygenUnlock, 1.0f),
            N("oxygen_1_1", "신선한 호흡",    "산소 흡입 가속",           "산소 부스트 흡입 +25%",     7, 1, 1, 3, 9500,    2.05f, "oxygen_1_0", 1, false, V18Effect.OxygenPower, 0.25f),
            N("oxygen_1_2", "질주 호흡",      "산소 쾌적 기동",           "산소 부스트 이속 +10%",     7, 1, 2, 3, 115000,  2.15f, "oxygen_1_1", 1, false, V18Effect.OxygenMove, 0.10f),
            N("oxygen_1_3", "오존 부스터",    "★ 오존 피버 유지",         "산소 피버 유지 +25%",       7, 1, 3, 3, 1650000, 2.25f, "oxygen_1_2", 1, true,  V18Effect.OxygenFeverHold, 0.25f),

            // 8: 얼룩·걸레
            N("stain_0_0", "기름 분해제",     "기름때 세척 가속",         "걸레 세척 속도 +20%",       8, 0, 0, 3, 550,     1.90f, "mop", 1, false, V18Effect.ScrubSpeed, 0.20f),
            N("stain_0_1", "와이드 패드",     "걸레 면적 확장",           "걸레 닦기 범위 +22%",       8, 0, 1, 3, 8800,    2.05f, "stain_0_0", 1, false, V18Effect.ScrubRange, 0.22f),
            N("stain_0_2", "고압 스팀",       "초고속 스팀 세척",         "걸레 세척 속도 +28%",       8, 0, 2, 3, 98000,   2.15f, "stain_0_1", 1, false, V18Effect.ScrubSpeed, 0.28f),
            N("stain_0_3", "광택 코팅",       "★ 완벽 광택 마무리",      "얼룩 정산 단가 +40%",       8, 0, 3, 3, 1450000, 2.25f, "stain_0_2", 1, true,  V18Effect.StainReward, 0.40f),

            N("stain_1_0", "향기 탈취",       "걸레 피버 충전",           "얼룩 제거 시 피버 +18%",    8, 1, 0, 3, 720,     1.90f, "mop", 1, false, V18Effect.ScrubFever, 0.18f),
            N("stain_1_1", "초극세사",        "세정력 보너스",            "얼룩 정산 단가 +24%",       8, 1, 1, 3, 11500,   2.05f, "stain_1_0", 1, false, V18Effect.StainReward, 0.24f),
            N("stain_1_2", "회전 브러시",     "광역 브러시",              "걸레 닦기 범위 +26%",       8, 1, 2, 3, 130000,  2.15f, "stain_1_1", 1, false, V18Effect.ScrubRange, 0.26f),
            N("stain_1_3", "미백 코팅",       "★ 눈부신 클린 광택",      "걸레 세척 속도 +35%",       8, 1, 3, 3, 1750000, 2.25f, "stain_1_2", 1, true,  V18Effect.ScrubSpeed, 0.35f),

            // 9: 고가치·물량·보물
            N("trash_0_0", "★ 캔·병 폐기물",   "고가치 캔·병 해금",        "값비싼 고철 캔·병 섞임!",   9, 0, 0, 1, 4500,     1.00f, "fist1", 1, false, V18Effect.TrashCan, 1.0f),
            N("trash_0_1", "★ 전자폐기물",     "전자폐기물 해금",          "고가 전자부품 쓰레기 추가!",9, 0, 1, 1, 65000,    1.00f, "trash_0_0", 1, false, V18Effect.TrashEwaste, 1.0f),
            N("trash_0_2", "★ 대형 폐가전",    "대형 폐가전 해금",         "고단가 폐가전 등장!",       9, 0, 2, 1, 680000,   1.00f, "trash_0_1", 1, false, V18Effect.TrashBig, 1.0f),
            N("trash_0_3", "★ 금괴 폐기물",    "★ 전설 금괴 폐기물 해금",  "초고액 금괴가 쓰레기에!",   9, 0, 3, 1, 8500000,  1.00f, "trash_0_2", 1, true,  V18Effect.TrashGold, 1.0f),

            N("trash_1_0", "중장비 파쇄",     "내구 쓰레기 파쇄력",       "고가치 내구도 파쇄 +25%",   9, 1, 0, 3, 2800,     1.95f, "price1", 1, false, V18Effect.HeavyDamage, 0.25f),
            N("trash_1_1", "고가치 감정",     "고가치 정산 단가",         "고가치 단가 +25%",          9, 1, 1, 3, 32000,    2.10f, "trash_1_0", 1, false, V18Effect.HeavyValue, 0.25f),
            N("trash_1_2", "보물 매장량↑",    "고가치 비중 확장",         "고가치 등장 비중 +15%",     9, 1, 2, 3, 360000,   2.20f, "trash_1_1", 1, false, V18Effect.TrashRatio, 0.15f),
            N("trash_1_3", "금괴 잭팟",       "★ 금괴 잭팟 대박",        "금괴 수거 시 +100% 잭팟!",  9, 1, 3, 3, 6200000,  2.30f, "trash_1_2", 1, true,  V18Effect.HeavyJackpot, 1.00f),
        };

        private static Dictionary<string, V18Node> _index;

        private static void BuildIndex()
        {
            _index = new Dictionary<string, V18Node>(Nodes.Length);
            for (int i = 0; i < Nodes.Length; i++) _index[Nodes[i].id] = Nodes[i];
        }

        public static V18Node Get(string id)
        {
            if (_index == null) BuildIndex();
            V18Node n;
            return (id != null && _index.TryGetValue(id, out n)) ? n : null;
        }

        public static bool IsV18Node(string id) => Get(id) != null;

        public static int Cost(V18Node node, int level)
        {
            if (node == null) return 999999;
            return Mathf.RoundToInt(node.baseCost * Mathf.Pow(node.growth, Mathf.Clamp(level, 0, node.maxLevel - 1)));
        }

        public static int TotalCostAllLevels()
        {
            int sum = 0;
            for (int i = 0; i < Nodes.Length; i++)
                for (int lv = 0; lv < Nodes[i].maxLevel; lv++)
                    sum += Cost(Nodes[i], lv);
            return sum;
        }
    }
}
