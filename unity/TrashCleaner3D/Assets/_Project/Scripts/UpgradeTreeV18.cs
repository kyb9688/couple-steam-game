using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.8 강화 효과 77종(v1.7 41 + 장비·쓰레기 36). 모든 부가 효과는 '기본 0/비활성'이고, 이 트리의 노드로만 켜지고 올라간다.
    /// 1회 강화당 상승폭은 1~3% 단위(피버·연쇄 같은 해금형은 1레벨)로 제한해 4시간 캠페인 동안 완만히 성장한다.
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

    /// <summary>v1.7 세분화 노드 정의(효과 1종 × 작은 상승폭).</summary>
    public class V18Node
    {
        public string id;
        public string name;      // 노드 카드 짧은 이름
        public string title;     // 상세 제목
        public string desc;      // 효과 설명(수치는 EffectText가 채움)
        public int branch;       // 0~7 계열
        public int lane;         // 계열 안 가지 번호(0~3)
        public int tier;         // 가지 안 단계(0~5)
        public Vector2 pos;      // 트리 좌표(중앙 기준, 화면 y는 +가 아래)
        public int maxLevel;
        public int baseCost;
        public float growth;
        public string prereq;
        public int prereqLevel;
        public bool milestone;   // 가지의 마지막 노드(강조 표시)
        public V18Effect effect;
        public float perLevel;

        public string EffectText(int level)
        {
            float v = perLevel * Mathf.Max(1, level);
            switch (effect)
            {
                case V18Effect.HoldSpeed: return $"흡입 속도 +{v * 100f:0.#}%";
                case V18Effect.MoveSpeed: return $"이동 속도 +{v * 100f:0.#}%";
                case V18Effect.Radius: return $"흡입 반경 +{v * 100f:0.#}%";
                case V18Effect.GrabCount: return $"동시 흡입 +{v:0}";
                case V18Effect.CritChance: return $"치명타 확률 +{v * 100f:0.#}%";
                case V18Effect.ChainChance: return $"연쇄 흡입 확률 +{v * 100f:0.#}%";
                case V18Effect.BinCap: return $"수거통 용량 +{v * 100f:0.#}%";
                case V18Effect.Price: return $"정산 단가 +{v * 100f:0.#}%";
                case V18Effect.ComboWindow: return $"콤보 유지 +{v:0.0}초";
                case V18Effect.SettleBonus: return $"정산 금액 +{v * 100f:0.#}%";
                case V18Effect.FistPower: return $"봉투 타격력 +{v * 100f:0.#}%";
                case V18Effect.FistCritChance: return $"봉투 치명타 확률 +{v * 100f:0.#}%";
                case V18Effect.FistCritMult: return $"봉투 치명타 배율 +{v * 100f:0.#}%";
                case V18Effect.BurstFever: return $"봉투 피버 충전 +{v * 100f:0.#}%";
                case V18Effect.BurstCash: return $"봉투 현금 보너스 +${v:0}";
                case V18Effect.ChainUnlock: return level > 0 ? "연쇄 흡입 해금" : "연쇄 흡입 잠김";
                case V18Effect.ChainRadius: return $"연쇄 흡입 반경 +{v:0.00}m";
                case V18Effect.ChainCount: return $"연쇄 흡입 개수 +{v:0}";
                case V18Effect.ScrubSpeed: return $"행주질 속도 +{v * 100f:0.#}%";
                case V18Effect.ScrubRange: return $"행주질 범위 +{v * 100f:0.#}%";
                case V18Effect.StainReward: return $"얼룩 정산 +{v * 100f:0.#}%";
                case V18Effect.ScrubFever: return $"행주질 피버 충전 +{v * 100f:0.#}%";
                case V18Effect.MaskFilter: return $"악취 상승 둔화 +{v * 100f:0.#}%";
                case V18Effect.StenchSlowResist: return $"악취 둔화 저항 +{v * 100f:0.#}%";
                case V18Effect.BlurReduce: return $"악취 시야 흐림 완화 +{v * 100f:0.#}%";
                case V18Effect.OxygenUnlock: return level > 0 ? "산소 부스트 해금" : "산소 부스트 잠김";
                case V18Effect.OxygenPower: return $"산소 부스트 흡입 +{v * 100f:0.#}%";
                case V18Effect.OxygenMove: return $"산소 부스트 이속 +{v * 100f:0.#}%";
                case V18Effect.OxygenFeverHold: return $"산소 피버 유지 +{v * 100f:0.#}%";
                case V18Effect.FeverGain: return $"피버 게이지 획득 +{v * 100f:0.#}%";
                case V18Effect.FeverHold: return $"피버 유지 +{v * 100f:0.#}%";
                case V18Effect.FeverMax: return $"피버 배율 +{v * 100f:0.#}%";
                case V18Effect.FeverPrice: return $"피버 정산 +{v * 100f:0.#}%";
                case V18Effect.RoombaSpeed: return $"로봇 이동 속도 +{v * 100f:0.#}%";
                case V18Effect.RoombaPickup: return $"로봇 수거 주기 +{v * 100f:0.#}%";
                case V18Effect.RoombaDetect: return $"로봇 탐지 반경 +{v * 100f:0.#}%";
                case V18Effect.RoombaBattery: return $"로봇 배터리 가동 +{v * 100f:0.#}%";
                case V18Effect.VehicleBulk: return $"차량 정산 배수 +{v * 100f:0.#}%";
                case V18Effect.VehicleMult: return $"차량 거래 배수 +{v * 100f:0.#}%";
                case V18Effect.BulkThreshold: return $"대량 정산 기준 +{v * 100f:0.#}%";
                case V18Effect.BulkSettle: return $"대량 정산 보너스 +{v * 100f:0.#}%";
                case V18Effect.RemoteCooldown: return $"원격 수거 쿨다운 +{v * 100f:0.#}%";
                case V18Effect.TwoHand: return level > 0 ? "양손 줍기 해금" : "양손 줍기 잠김";
                case V18Effect.OrbUnlock: return level > 0 ? "원기옥 해금" : "원기옥 잠김";
                case V18Effect.OrbPull: return $"원기옥 끌어당김 +{v * 100f:0.#}%";
                case V18Effect.OrbRadius: return $"원기옥 반경 +{v * 100f:0.#}%";
                case V18Effect.OrbCharge: return $"구슬 성장 +{v * 100f:0.#}%";
                case V18Effect.OrbSize: return $"구슬 크기 +{v * 100f:0.#}%";
                case V18Effect.OrbCash: return $"원기옥 정산 +{v * 100f:0.#}%";
                case V18Effect.OrbBurst: return $"터짐 범위 +{v * 100f:0.#}%";
                case V18Effect.OrbFly: return $"발사 속도 +{v * 100f:0.#}%";
                case V18Effect.OrbAuto: return level > 0 ? "즉시 발사 해금" : "즉시 발사 잠김";
                case V18Effect.BhUnlock: return level > 0 ? "블랙홀 해금" : "블랙홀 잠김";
                case V18Effect.BhRadius: return $"블랙홀 반경 +{v * 100f:0.#}%";
                case V18Effect.BhCount: return $"블랙홀 개수 +{v:0}";
                case V18Effect.BhSpin: return $"블랙홀 회전 +{v * 100f:0.#}%";
                case V18Effect.BhDuration: return $"블랙홀 지속 +{v * 100f:0.#}%";
                case V18Effect.WhUnlock: return level > 0 ? "화이트홀 해금" : "화이트홀 잠김";
                case V18Effect.WhCash: return $"화이트홀 정산 +{v * 100f:0.#}%";
                case V18Effect.BhPull: return $"블랙홀 흡입력 +{v * 100f:0.#}%";
                case V18Effect.WhAuto: return level > 0 ? "완전 자동 정산 해금" : "완전 자동 정산 잠김";
                case V18Effect.TrashCan: return level > 0 ? "캔·병 해금" : "캔·병 잠김";
                case V18Effect.TrashEwaste: return level > 0 ? "전자폐기물 해금" : "전자폐기물 잠김";
                case V18Effect.TrashBig: return level > 0 ? "대형 폐가전 해금" : "대형 폐가전 잠김";
                case V18Effect.TrashGold: return level > 0 ? "금괴 해금" : "금괴 잠김";
                case V18Effect.TrashRatio: return $"고가치 비중 +{v * 100f:0.#}%";
                case V18Effect.TrashDurability: return $"내구도 +{v * 100f:0.#}%";
                case V18Effect.HeavyDamage: return $"파쇄력 +{v * 100f:0.#}%";
                case V18Effect.HeavyValue: return $"고가치 단가 +{v * 100f:0.#}%";
                case V18Effect.SortBonus: return $"분리 보너스 +{v * 100f:0.#}%";
                case V18Effect.RarityBonus: return $"등급 보정 +{v * 100f:0.#}%";
                case V18Effect.HeavyJackpot: return $"금괴 잭팟 +{v * 100f:0.#}%";
                case V18Effect.TrashDensity: return $"쓰레기 밀도 +{v * 100f:0.#}%";
                case V18Effect.TrashCluster: return $"더미 밀집 +{v * 100f:0.#}%";
                case V18Effect.WaveBonus: return $"웨이브 추가 +{v:0}";
                case V18Effect.MegaDump: return level > 0 ? "대량 투하 해금" : "대량 투하 잠김";
                default: return "";
            }
        }
    }

    public struct BranchDef
    {
        public string id;
        public string label;
        public Color color;
        public int angle;        // 화면 기준 각도(도). y가 아래로 +
    }

    /// <summary>
    /// v1.8 강화 트리: 10계열 × 4가지 × 6단계 = 240개 마이크로 노드.
    /// 기존 코어 28노드(도구 3티어·차량 3단계·대걸레·로봇 등)와 합쳐 268노드.
    /// v1.8 신규: 장비·압축(원기옥·블랙홀·화이트홀) 24 + 쓰레기 강화(고가치·내구도·물량) 24.
    /// 데이터는 docs/qa/tree_v18.py 에서 생성된다(수치 단일 출처).
    /// </summary>
    public static class UpgradeTreeV18
    {
        public const int MicroNodeCount = 240;
        public const int CoreNodeCount = 28;
        public const int TotalNodeCount = MicroNodeCount + CoreNodeCount;
        public const int BranchCount = 10;

        public static readonly BranchDef[] Branches =
        {
            new BranchDef { id = "suction", label = "흡입·손", color = new Color(0.320f, 0.820f, 0.920f), angle = -126 },
            new BranchDef { id = "bin", label = "수거·정산", color = new Color(0.360f, 0.900f, 0.540f), angle = -54 },
            new BranchDef { id = "bag", label = "큰 봉투·타격", color = new Color(0.980f, 0.740f, 0.240f), angle = -90 },
            new BranchDef { id = "stain", label = "얼룩·행주질", color = new Color(0.340f, 0.620f, 0.960f), angle = 162 },
            new BranchDef { id = "oxygen", label = "방독면·산소", color = new Color(0.400f, 0.860f, 0.660f), angle = 126 },
            new BranchDef { id = "fever", label = "피버·콤보", color = new Color(0.960f, 0.460f, 0.280f), angle = 90 },
            new BranchDef { id = "roomba", label = "로봇청소기", color = new Color(0.700f, 0.520f, 0.940f), angle = 54 },
            new BranchDef { id = "vehicle", label = "차량·운송", color = new Color(0.660f, 0.720f, 0.800f), angle = -18 },
            new BranchDef { id = "gear", label = "장비·압축", color = new Color(0.220f, 0.920f, 0.860f), angle = 18 },
            new BranchDef { id = "trash", label = "쓰레기 강화", color = new Color(0.760f, 0.580f, 0.260f), angle = 198 },
        };

        public static readonly V18Node[] Nodes =
        {
            new V18Node { id = "suction_0_0", name = "흡입력", title = "흡입력 · 기본 흡입", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 0, pos = new Vector2(-498f, -405f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "hold1", prereqLevel = 1, milestone = false, effect = V18Effect.HoldSpeed, perLevel = 0.016f },
            new V18Node { id = "suction_0_1", name = "손아귀", title = "손아귀 · 쥐는 힘", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 1, pos = new Vector2(-583f, -471f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "suction_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.HoldSpeed, perLevel = 0.016f },
            new V18Node { id = "suction_0_2", name = "호흡 조절", title = "호흡 조절 · 끊김 없는 흡입", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 2, pos = new Vector2(-668f, -538f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "suction_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.HoldSpeed, perLevel = 0.018f },
            new V18Node { id = "suction_0_3", name = "연속 빨기", title = "연속 빨기 · 연속 흡입", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 3, pos = new Vector2(-753f, -605f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "suction_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.HoldSpeed, perLevel = 0.018f },
            new V18Node { id = "suction_0_4", name = "압축 흡입", title = "압축 흡입 · 고압 흡입", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 4, pos = new Vector2(-839f, -671f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "suction_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.HoldSpeed, perLevel = 0.02f },
            new V18Node { id = "suction_0_5", name = "완전 진공", title = "완전 진공 · 진공 완성", desc = "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", branch = 0, lane = 0, tier = 5, pos = new Vector2(-924f, -738f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "suction_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.HoldSpeed, perLevel = 0.022f },
            new V18Node { id = "suction_1_0", name = "넓은 손", title = "넓은 손 · 손바닥 확장", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 0, pos = new Vector2(-409f, -469f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "rad1", prereqLevel = 1, milestone = false, effect = V18Effect.Radius, perLevel = 0.018f },
            new V18Node { id = "suction_1_1", name = "노즐 벌리기", title = "노즐 벌리기 · 노즐 개량", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 1, pos = new Vector2(-478f, -548f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "suction_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.Radius, perLevel = 0.018f },
            new V18Node { id = "suction_1_2", name = "쓸기 궤적", title = "쓸기 궤적 · 궤적 확장", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 2, pos = new Vector2(-547f, -626f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "suction_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.Radius, perLevel = 0.02f },
            new V18Node { id = "suction_1_3", name = "광역 빨기", title = "광역 빨기 · 광역 흡입", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 3, pos = new Vector2(-616f, -704f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "suction_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.Radius, perLevel = 0.02f },
            new V18Node { id = "suction_1_4", name = "소용돌이 날개", title = "소용돌이 날개 · 회오리 확장", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 4, pos = new Vector2(-685f, -783f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "suction_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.Radius, perLevel = 0.022f },
            new V18Node { id = "suction_1_5", name = "흡입 돔", title = "흡입 돔 · 돔형 흡입", desc = "한 번에 빨아들이는 원이 넓어진다", branch = 0, lane = 1, tier = 5, pos = new Vector2(-755f, -861f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "suction_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.Radius, perLevel = 0.025f },
            new V18Node { id = "suction_2_0", name = "양손 줍기", title = "양손 줍기 · 양손 모션", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 0, pos = new Vector2(-320f, -534f), maxLevel = 1, baseCost = 44, growth = 1.3f, prereq = "hold1", prereqLevel = 1, milestone = false, effect = V18Effect.TwoHand, perLevel = 1.0f },
            new V18Node { id = "suction_2_1", name = "멀티 락온", title = "멀티 락온 · 다중 락온", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 1, pos = new Vector2(-373f, -624f), maxLevel = 1, baseCost = 180, growth = 1.3f, prereq = "suction_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.GrabCount, perLevel = 1.0f },
            new V18Node { id = "suction_2_2", name = "손가락 추가", title = "손가락 추가 · 집게 손가락", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 2, pos = new Vector2(-426f, -714f), maxLevel = 1, baseCost = 600, growth = 1.3f, prereq = "suction_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.GrabCount, perLevel = 1.0f },
            new V18Node { id = "suction_2_3", name = "동시 락온", title = "동시 락온 · 동시 포집", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 3, pos = new Vector2(-479f, -804f), maxLevel = 1, baseCost = 1800, growth = 1.3f, prereq = "suction_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.GrabCount, perLevel = 1.0f },
            new V18Node { id = "suction_2_4", name = "여럿 움켜쥐기", title = "여럿 움켜쥐기 · 다발 포집", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 4, pos = new Vector2(-532f, -894f), maxLevel = 1, baseCost = 5200, growth = 1.3f, prereq = "suction_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.GrabCount, perLevel = 1.0f },
            new V18Node { id = "suction_2_5", name = "무리째 락온", title = "무리째 락온 · 무리 포집", desc = "동시에 락온하는 쓰레기 개수가 늘어난다", branch = 0, lane = 2, tier = 5, pos = new Vector2(-586f, -984f), maxLevel = 1, baseCost = 14000, growth = 1.3f, prereq = "suction_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.GrabCount, perLevel = 1.0f },
            new V18Node { id = "suction_3_0", name = "날카로운 눈", title = "날카로운 눈 · 약점 감지", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 0, pos = new Vector2(-231f, -599f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "rad1", prereqLevel = 1, milestone = false, effect = V18Effect.CritChance, perLevel = 0.015f },
            new V18Node { id = "suction_3_1", name = "약점 포착", title = "약점 포착 · 약점 조준", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 1, pos = new Vector2(-268f, -700f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "suction_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.CritChance, perLevel = 0.015f },
            new V18Node { id = "suction_3_2", name = "급소 감각", title = "급소 감각 · 급소 포착", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 2, pos = new Vector2(-305f, -802f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "suction_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.CritChance, perLevel = 0.02f },
            new V18Node { id = "suction_3_3", name = "연쇄 촉", title = "연쇄 촉 · 연쇄 감각", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 3, pos = new Vector2(-342f, -903f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "suction_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.ChainChance, perLevel = 0.02f },
            new V18Node { id = "suction_3_4", name = "예민한 촉", title = "예민한 촉 · 연쇄 확률", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 4, pos = new Vector2(-379f, -1005f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "suction_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.ChainChance, perLevel = 0.025f },
            new V18Node { id = "suction_3_5", name = "가벼운 발놀림", title = "가벼운 발놀림 · 발놀림", desc = "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", branch = 0, lane = 3, tier = 5, pos = new Vector2(-416f, -1107f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "suction_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.MoveSpeed, perLevel = 0.02f },
            new V18Node { id = "bin_0_0", name = "튼튼한 봉투", title = "튼튼한 봉투 · 수거 봉투", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 0, pos = new Vector2(231f, -599f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "bin1", prereqLevel = 1, milestone = false, effect = V18Effect.BinCap, perLevel = 0.02f },
            new V18Node { id = "bin_0_1", name = "수거통 확장", title = "수거통 확장 · 용량 확장", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 1, pos = new Vector2(268f, -700f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "bin_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.BinCap, perLevel = 0.02f },
            new V18Node { id = "bin_0_2", name = "이중 봉투", title = "이중 봉투 · 이중 적재", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 2, pos = new Vector2(305f, -802f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "bin_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.BinCap, perLevel = 0.022f },
            new V18Node { id = "bin_0_3", name = "압축 적재", title = "압축 적재 · 압축 적재", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 3, pos = new Vector2(342f, -903f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "bin_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.BinCap, perLevel = 0.022f },
            new V18Node { id = "bin_0_4", name = "대형 수거통", title = "대형 수거통 · 대형 용량", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 4, pos = new Vector2(379f, -1005f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "bin_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.BinCap, perLevel = 0.025f },
            new V18Node { id = "bin_0_5", name = "무한 적재", title = "무한 적재 · 적재 완성", desc = "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", branch = 1, lane = 0, tier = 5, pos = new Vector2(416f, -1107f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "bin_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.BinCap, perLevel = 0.028f },
            new V18Node { id = "bin_1_0", name = "저울 보정", title = "저울 보정 · 저울 손질", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 0, pos = new Vector2(320f, -534f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "price1", prereqLevel = 1, milestone = false, effect = V18Effect.Price, perLevel = 0.015f },
            new V18Node { id = "bin_1_1", name = "분리수거", title = "분리수거 · 분리 정리", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 1, pos = new Vector2(373f, -624f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "bin_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.Price, perLevel = 0.015f },
            new V18Node { id = "bin_1_2", name = "등급 분류", title = "등급 분류 · 등급 구분", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 2, pos = new Vector2(426f, -714f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "bin_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.Price, perLevel = 0.018f },
            new V18Node { id = "bin_1_3", name = "고가 매입", title = "고가 매입 · 고가 거래", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 3, pos = new Vector2(479f, -804f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "bin_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.Price, perLevel = 0.018f },
            new V18Node { id = "bin_1_4", name = "재활용 거래", title = "재활용 거래 · 재활용 계약", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 4, pos = new Vector2(532f, -894f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "bin_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.Price, perLevel = 0.02f },
            new V18Node { id = "bin_1_5", name = "프리미엄 정산", title = "프리미엄 정산 · 프리미엄", desc = "쓰레기 한 개의 정산 단가가 오른다", branch = 1, lane = 1, tier = 5, pos = new Vector2(586f, -984f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "bin_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.Price, perLevel = 0.022f },
            new V18Node { id = "bin_2_0", name = "정산 콤보", title = "정산 콤보 · 콤보 시작", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 0, pos = new Vector2(409f, -469f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "price1", prereqLevel = 1, milestone = false, effect = V18Effect.ComboWindow, perLevel = 0.4f },
            new V18Node { id = "bin_2_1", name = "연속 납품", title = "연속 납품 · 연속 납품", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 1, pos = new Vector2(478f, -548f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "bin_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.ComboWindow, perLevel = 0.4f },
            new V18Node { id = "bin_2_2", name = "단골 손님", title = "단골 손님 · 단골 거래", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 2, pos = new Vector2(547f, -626f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "bin_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.ComboWindow, perLevel = 0.5f },
            new V18Node { id = "bin_2_3", name = "빠른 정산", title = "빠른 정산 · 정산 속도", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 3, pos = new Vector2(616f, -704f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "bin_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.ComboWindow, perLevel = 0.5f },
            new V18Node { id = "bin_2_4", name = "콤보 유지", title = "콤보 유지 · 콤보 연장", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 4, pos = new Vector2(685f, -783f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "bin_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.ComboWindow, perLevel = 0.6f },
            new V18Node { id = "bin_2_5", name = "정산 리듬", title = "정산 리듬 · 리듬 완성", desc = "연속 정산 콤보를 오래 유지한다", branch = 1, lane = 2, tier = 5, pos = new Vector2(755f, -861f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "bin_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.ComboWindow, perLevel = 0.7f },
            new V18Node { id = "bin_3_0", name = "영수증", title = "영수증 · 영수증 정리", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 0, pos = new Vector2(498f, -405f), maxLevel = 5, baseCost = 44, growth = 1.3f, prereq = "bin1", prereqLevel = 1, milestone = false, effect = V18Effect.SettleBonus, perLevel = 0.012f },
            new V18Node { id = "bin_3_1", name = "꼼꼼한 계산", title = "꼼꼼한 계산 · 계산 정확", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 1, pos = new Vector2(583f, -471f), maxLevel = 5, baseCost = 180, growth = 1.3f, prereq = "bin_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.SettleBonus, perLevel = 0.012f },
            new V18Node { id = "bin_3_2", name = "현금 영수증", title = "현금 영수증 · 현금 정산", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 2, pos = new Vector2(668f, -538f), maxLevel = 3, baseCost = 600, growth = 1.3f, prereq = "bin_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.SettleBonus, perLevel = 0.014f },
            new V18Node { id = "bin_3_3", name = "세금 공제", title = "세금 공제 · 공제 적용", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 3, pos = new Vector2(753f, -605f), maxLevel = 3, baseCost = 1800, growth = 1.3f, prereq = "bin_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.SettleBonus, perLevel = 0.014f },
            new V18Node { id = "bin_3_4", name = "추가 수당", title = "추가 수당 · 수당 협상", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 4, pos = new Vector2(839f, -671f), maxLevel = 3, baseCost = 5200, growth = 1.3f, prereq = "bin_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.SettleBonus, perLevel = 0.016f },
            new V18Node { id = "bin_3_5", name = "정산 보너스", title = "정산 보너스 · 보너스 정산", desc = "정산할 때 금액이 조금 더 붙는다", branch = 1, lane = 3, tier = 5, pos = new Vector2(924f, -738f), maxLevel = 3, baseCost = 14000, growth = 1.3f, prereq = "bin_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.SettleBonus, perLevel = 0.018f },
            new V18Node { id = "bag_0_0", name = "주먹 단련", title = "주먹 단련 · 기본 타격", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 0, pos = new Vector2(-165f, -620f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "fist1", prereqLevel = 1, milestone = false, effect = V18Effect.FistPower, perLevel = 0.018f },
            new V18Node { id = "bag_0_1", name = "두꺼운 장갑", title = "두꺼운 장갑 · 장갑 안감", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 1, pos = new Vector2(-195f, -724f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "bag_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.FistPower, perLevel = 0.018f },
            new V18Node { id = "bag_0_2", name = "너클", title = "너클 · 너클 강화", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 2, pos = new Vector2(-224f, -828f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "bag_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.FistPower, perLevel = 0.02f },
            new V18Node { id = "bag_0_3", name = "망치 손바닥", title = "망치 손바닥 · 망치 타격", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 3, pos = new Vector2(-254f, -932f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "bag_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.FistPower, perLevel = 0.02f },
            new V18Node { id = "bag_0_4", name = "철권", title = "철권 · 철권 타격", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 4, pos = new Vector2(-284f, -1036f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "bag_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.FistPower, perLevel = 0.022f },
            new V18Node { id = "bag_0_5", name = "분쇄 주먹", title = "분쇄 주먹 · 분쇄 타격", desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 2, lane = 0, tier = 5, pos = new Vector2(-313f, -1140f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "bag_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.FistPower, perLevel = 0.025f },
            new V18Node { id = "bag_1_0", name = "급소 보기", title = "급소 보기 · 약점 조준", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 0, pos = new Vector2(-55f, -620f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "fist1", prereqLevel = 1, milestone = false, effect = V18Effect.FistCritChance, perLevel = 0.015f },
            new V18Node { id = "bag_1_1", name = "약한 봉투 찾기", title = "약한 봉투 찾기 · 약점 탐색", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 1, pos = new Vector2(-65f, -724f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "bag_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.FistCritChance, perLevel = 0.015f },
            new V18Node { id = "bag_1_2", name = "치명타 감각", title = "치명타 감각 · 치명 감각", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 2, pos = new Vector2(-75f, -828f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "bag_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.FistCritChance, perLevel = 0.018f },
            new V18Node { id = "bag_1_3", name = "찢는 손톱", title = "찢는 손톱 · 치명 배율", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 3, pos = new Vector2(-85f, -932f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "bag_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.FistCritMult, perLevel = 0.08f },
            new V18Node { id = "bag_1_4", name = "파열점", title = "파열점 · 파열 강화", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 4, pos = new Vector2(-95f, -1036f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "bag_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.FistCritMult, perLevel = 0.08f },
            new V18Node { id = "bag_1_5", name = "일격", title = "일격 · 일격 필살", desc = "봉투 약점을 노려 치명타가 터진다", branch = 2, lane = 1, tier = 5, pos = new Vector2(-104f, -1140f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "bag_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.FistCritMult, perLevel = 0.1f },
            new V18Node { id = "bag_2_0", name = "터지는 맛", title = "터지는 맛 · 피버 충전 해금", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 0, pos = new Vector2(55f, -620f), maxLevel = 1, baseCost = 48, growth = 1.3f, prereq = "fist2", prereqLevel = 1, milestone = false, effect = V18Effect.BurstFever, perLevel = 0.08f },
            new V18Node { id = "bag_2_1", name = "파열 충전", title = "파열 충전 · 현금 보너스 해금", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 1, pos = new Vector2(65f, -724f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "bag_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.BurstCash, perLevel = 6.0f },
            new V18Node { id = "bag_2_2", name = "봉투 피버", title = "봉투 피버 · 피버 충전 증가", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 2, pos = new Vector2(75f, -828f), maxLevel = 2, baseCost = 660, growth = 1.3f, prereq = "bag_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.BurstFever, perLevel = 0.03f },
            new V18Node { id = "bag_2_3", name = "현금 폭발", title = "현금 폭발 · 현금 보너스 증가", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 3, pos = new Vector2(85f, -932f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "bag_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.BurstCash, perLevel = 10.0f },
            new V18Node { id = "bag_2_4", name = "터짐 보너스", title = "터짐 보너스 · 피버 충전 강화", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 4, pos = new Vector2(95f, -1036f), maxLevel = 2, baseCost = 5720, growth = 1.3f, prereq = "bag_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.BurstFever, perLevel = 0.03f },
            new V18Node { id = "bag_2_5", name = "대박 터짐", title = "대박 터짐 · 현금 보너스 강화", desc = "봉투가 터질 때 피버와 현금이 터진다", branch = 2, lane = 2, tier = 5, pos = new Vector2(105f, -1140f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "bag_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.BurstCash, perLevel = 14.0f },
            new V18Node { id = "bag_3_0", name = "미니 블랙홀", title = "미니 블랙홀 · 연쇄 흡입 해금", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 0, pos = new Vector2(165f, -620f), maxLevel = 1, baseCost = 48, growth = 1.3f, prereq = "fist2", prereqLevel = 1, milestone = false, effect = V18Effect.ChainUnlock, perLevel = 1.0f },
            new V18Node { id = "bag_3_1", name = "연쇄 흡입", title = "연쇄 흡입 · 연쇄 반경", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 1, pos = new Vector2(195f, -724f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "bag_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.ChainRadius, perLevel = 0.25f },
            new V18Node { id = "bag_3_2", name = "빨아들이는 구멍", title = "빨아들이는 구멍 · 연쇄 개수", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 2, pos = new Vector2(224f, -828f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "bag_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.ChainCount, perLevel = 2.0f },
            new V18Node { id = "bag_3_3", name = "소용돌이 반경", title = "소용돌이 반경 · 반경 확장", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 3, pos = new Vector2(254f, -932f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "bag_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.ChainRadius, perLevel = 0.25f },
            new V18Node { id = "bag_3_4", name = "연쇄 개수", title = "연쇄 개수 · 개수 확장", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 4, pos = new Vector2(284f, -1036f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "bag_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.ChainCount, perLevel = 2.0f },
            new V18Node { id = "bag_3_5", name = "블랙홀 완성", title = "블랙홀 완성 · 블랙홀 완성", desc = "봉투가 터지면 주변 쓰레기가 빨려 들어온다", branch = 2, lane = 3, tier = 5, pos = new Vector2(314f, -1140f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "bag_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.ChainRadius, perLevel = 0.3f },
            new V18Node { id = "stain_0_0", name = "팔 힘", title = "팔 힘 · 기본 행주질", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 0, pos = new Vector2(-539f, 349f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "mop", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubSpeed, perLevel = 0.02f },
            new V18Node { id = "stain_0_1", name = "행주질", title = "행주질 · 행주 손놀림", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 1, pos = new Vector2(-628f, 409f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "stain_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubSpeed, perLevel = 0.02f },
            new V18Node { id = "stain_0_2", name = "강력 세제", title = "강력 세제 · 세제 강화", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 2, pos = new Vector2(-718f, 469f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "stain_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubSpeed, perLevel = 0.022f },
            new V18Node { id = "stain_0_3", name = "빠른 손놀림", title = "빠른 손놀림 · 속도 강화", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 3, pos = new Vector2(-808f, 530f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "stain_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubSpeed, perLevel = 0.022f },
            new V18Node { id = "stain_0_4", name = "광택 마무리", title = "광택 마무리 · 마무리 광택", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 4, pos = new Vector2(-898f, 590f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "stain_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubSpeed, perLevel = 0.025f },
            new V18Node { id = "stain_0_5", name = "완벽한 행주", title = "완벽한 행주 · 완벽 행주질", desc = "바닥 얼룩이 더 빨리 지워진다", branch = 3, lane = 0, tier = 5, pos = new Vector2(-987f, 650f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "stain_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.ScrubSpeed, perLevel = 0.028f },
            new V18Node { id = "stain_1_0", name = "큰 걸레", title = "큰 걸레 · 걸레 확장", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 0, pos = new Vector2(-573f, 244f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "mop", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubRange, perLevel = 0.02f },
            new V18Node { id = "stain_1_1", name = "팔 길이", title = "팔 길이 · 사거리 확장", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 1, pos = new Vector2(-669f, 285f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "stain_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubRange, perLevel = 0.02f },
            new V18Node { id = "stain_1_2", name = "넓은 행주", title = "넓은 행주 · 범위 확장", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 2, pos = new Vector2(-764f, 327f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "stain_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubRange, perLevel = 0.022f },
            new V18Node { id = "stain_1_3", name = "바닥 쓸기", title = "바닥 쓸기 · 쓸기 확장", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 3, pos = new Vector2(-860f, 369f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "stain_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubRange, perLevel = 0.022f },
            new V18Node { id = "stain_1_4", name = "사각지대 제거", title = "사각지대 제거 · 사각 제거", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 4, pos = new Vector2(-956f, 410f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "stain_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubRange, perLevel = 0.025f },
            new V18Node { id = "stain_1_5", name = "바닥 전체", title = "바닥 전체 · 전체 범위", desc = "한 번에 닦이는 바닥 범위가 넓어진다", branch = 3, lane = 1, tier = 5, pos = new Vector2(-1052f, 452f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "stain_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.ScrubRange, perLevel = 0.028f },
            new V18Node { id = "stain_2_0", name = "얼룩 값", title = "얼룩 값 · 얼룩 시세", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 0, pos = new Vector2(-607f, 139f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "scrub", prereqLevel = 1, milestone = false, effect = V18Effect.StainReward, perLevel = 0.02f },
            new V18Node { id = "stain_2_1", name = "찌든 때 시세", title = "찌든 때 시세 · 찌든 때 값", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 1, pos = new Vector2(-709f, 162f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "stain_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.StainReward, perLevel = 0.02f },
            new V18Node { id = "stain_2_2", name = "곰팡이 제거", title = "곰팡이 제거 · 곰팡이 값", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 2, pos = new Vector2(-811f, 185f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "stain_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.StainReward, perLevel = 0.022f },
            new V18Node { id = "stain_2_3", name = "왁스 코팅", title = "왁스 코팅 · 코팅 시세", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 3, pos = new Vector2(-913f, 207f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "stain_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.StainReward, perLevel = 0.022f },
            new V18Node { id = "stain_2_4", name = "광택 보너스", title = "광택 보너스 · 광택 값", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 4, pos = new Vector2(-1015f, 230f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "stain_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.StainReward, perLevel = 0.025f },
            new V18Node { id = "stain_2_5", name = "바닥 복원", title = "바닥 복원 · 복원 시세", desc = "얼룩을 지울 때 받는 금액이 오른다", branch = 3, lane = 2, tier = 5, pos = new Vector2(-1116f, 253f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "stain_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.StainReward, perLevel = 0.028f },
            new V18Node { id = "stain_3_0", name = "행주질 흥", title = "행주질 흥 · 피버 시작", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 0, pos = new Vector2(-641f, 35f), maxLevel = 5, baseCost = 48, growth = 1.3f, prereq = "scrub", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubFever, perLevel = 0.015f },
            new V18Node { id = "stain_3_1", name = "리듬 타기", title = "리듬 타기 · 리듬 충전", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 1, pos = new Vector2(-749f, 39f), maxLevel = 5, baseCost = 198, growth = 1.3f, prereq = "stain_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubFever, perLevel = 0.015f },
            new V18Node { id = "stain_3_2", name = "손맛 피버", title = "손맛 피버 · 손맛 충전", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 2, pos = new Vector2(-857f, 42f), maxLevel = 3, baseCost = 660, growth = 1.3f, prereq = "stain_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubFever, perLevel = 0.018f },
            new V18Node { id = "stain_3_3", name = "걸레 피버", title = "걸레 피버 · 걸레 충전", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 3, pos = new Vector2(-965f, 46f), maxLevel = 3, baseCost = 1980, growth = 1.3f, prereq = "stain_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubFever, perLevel = 0.018f },
            new V18Node { id = "stain_3_4", name = "열심 피버", title = "열심 피버 · 열심 충전", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 4, pos = new Vector2(-1073f, 50f), maxLevel = 3, baseCost = 5720, growth = 1.3f, prereq = "stain_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.ScrubFever, perLevel = 0.02f },
            new V18Node { id = "stain_3_5", name = "광택 피버", title = "광택 피버 · 광택 충전", desc = "얼룩을 닦을수록 피버가 찬다", branch = 3, lane = 3, tier = 5, pos = new Vector2(-1181f, 54f), maxLevel = 3, baseCost = 15400, growth = 1.3f, prereq = "stain_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.ScrubFever, perLevel = 0.022f },
            new V18Node { id = "oxygen_0_0", name = "필터 교체", title = "필터 교체 · 기본 필터", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 0, pos = new Vector2(-231f, 599f), maxLevel = 5, baseCost = 62, growth = 1.3f, prereq = "mask1", prereqLevel = 1, milestone = false, effect = V18Effect.MaskFilter, perLevel = 0.015f },
            new V18Node { id = "oxygen_0_1", name = "활성탄 필터", title = "활성탄 필터 · 활성탄", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 1, pos = new Vector2(-268f, 700f), maxLevel = 5, baseCost = 252, growth = 1.3f, prereq = "oxygen_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.MaskFilter, perLevel = 0.015f },
            new V18Node { id = "oxygen_0_2", name = "이중 필터", title = "이중 필터 · 이중 여과", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 2, pos = new Vector2(-305f, 802f), maxLevel = 3, baseCost = 840, growth = 1.3f, prereq = "oxygen_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.MaskFilter, perLevel = 0.018f },
            new V18Node { id = "oxygen_0_3", name = "방독면 밀폐", title = "방독면 밀폐 · 밀폐 강화", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 3, pos = new Vector2(-342f, 903f), maxLevel = 3, baseCost = 2520, growth = 1.3f, prereq = "oxygen_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.MaskFilter, perLevel = 0.018f },
            new V18Node { id = "oxygen_0_4", name = "정화 카트리지", title = "정화 카트리지 · 정화 강화", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 4, pos = new Vector2(-379f, 1005f), maxLevel = 3, baseCost = 7280, growth = 1.3f, prereq = "oxygen_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.MaskFilter, perLevel = 0.02f },
            new V18Node { id = "oxygen_0_5", name = "완벽 밀폐", title = "완벽 밀폐 · 완벽 밀폐", desc = "악취가 차오르는 속도가 느려진다", branch = 4, lane = 0, tier = 5, pos = new Vector2(-416f, 1107f), maxLevel = 3, baseCost = 19600, growth = 1.3f, prereq = "oxygen_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.MaskFilter, perLevel = 0.022f },
            new V18Node { id = "oxygen_1_0", name = "코 막기", title = "코 막기 · 악취 저항", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 0, pos = new Vector2(-320f, 534f), maxLevel = 5, baseCost = 62, growth = 1.3f, prereq = "mask1", prereqLevel = 1, milestone = false, effect = V18Effect.StenchSlowResist, perLevel = 0.02f },
            new V18Node { id = "oxygen_1_1", name = "둔감한 후각", title = "둔감한 후각 · 후각 둔화", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 1, pos = new Vector2(-373f, 624f), maxLevel = 5, baseCost = 252, growth = 1.3f, prereq = "oxygen_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.StenchSlowResist, perLevel = 0.02f },
            new V18Node { id = "oxygen_1_2", name = "악취 적응", title = "악취 적응 · 적응 훈련", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 2, pos = new Vector2(-426f, 714f), maxLevel = 3, baseCost = 840, growth = 1.3f, prereq = "oxygen_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.StenchSlowResist, perLevel = 0.022f },
            new V18Node { id = "oxygen_1_3", name = "숨 참기", title = "숨 참기 · 숨 참기", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 3, pos = new Vector2(-479f, 804f), maxLevel = 3, baseCost = 2520, growth = 1.3f, prereq = "oxygen_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.StenchSlowResist, perLevel = 0.022f },
            new V18Node { id = "oxygen_1_4", name = "강한 정신력", title = "강한 정신력 · 정신력", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 4, pos = new Vector2(-532f, 894f), maxLevel = 3, baseCost = 7280, growth = 1.3f, prereq = "oxygen_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.StenchSlowResist, perLevel = 0.025f },
            new V18Node { id = "oxygen_1_5", name = "악취 면역", title = "악취 면역 · 면역 완성", desc = "악취가 심해도 이동·작업 속도가 덜 떨어진다", branch = 4, lane = 1, tier = 5, pos = new Vector2(-586f, 984f), maxLevel = 3, baseCost = 19600, growth = 1.3f, prereq = "oxygen_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.StenchSlowResist, perLevel = 0.028f },
            new V18Node { id = "oxygen_2_0", name = "시야 확보", title = "시야 확보 · 시야 유지", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 0, pos = new Vector2(-409f, 469f), maxLevel = 5, baseCost = 62, growth = 1.3f, prereq = "mask2", prereqLevel = 1, milestone = false, effect = V18Effect.BlurReduce, perLevel = 0.02f },
            new V18Node { id = "oxygen_2_1", name = "김 서림 방지", title = "김 서림 방지 · 김 방지", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 1, pos = new Vector2(-478f, 548f), maxLevel = 5, baseCost = 252, growth = 1.3f, prereq = "oxygen_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.BlurReduce, perLevel = 0.02f },
            new V18Node { id = "oxygen_2_2", name = "렌즈 코팅", title = "렌즈 코팅 · 렌즈 코팅", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 2, pos = new Vector2(-547f, 626f), maxLevel = 3, baseCost = 840, growth = 1.3f, prereq = "oxygen_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.BlurReduce, perLevel = 0.022f },
            new V18Node { id = "oxygen_2_3", name = "선명한 시야", title = "선명한 시야 · 선명 시야", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 3, pos = new Vector2(-616f, 704f), maxLevel = 3, baseCost = 2520, growth = 1.3f, prereq = "oxygen_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.BlurReduce, perLevel = 0.022f },
            new V18Node { id = "oxygen_2_4", name = "안개 걷기", title = "안개 걷기 · 안개 제거", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 4, pos = new Vector2(-685f, 783f), maxLevel = 3, baseCost = 7280, growth = 1.3f, prereq = "oxygen_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.BlurReduce, perLevel = 0.025f },
            new V18Node { id = "oxygen_2_5", name = "투명 렌즈", title = "투명 렌즈 · 투명 렌즈", desc = "악취로 화면이 흐려지는 정도가 줄어든다", branch = 4, lane = 2, tier = 5, pos = new Vector2(-755f, 861f), maxLevel = 3, baseCost = 19600, growth = 1.3f, prereq = "oxygen_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.BlurReduce, perLevel = 0.028f },
            new V18Node { id = "oxygen_3_0", name = "산소 부스트", title = "산소 부스트 · 부스트 해금", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 0, pos = new Vector2(-498f, 405f), maxLevel = 1, baseCost = 62, growth = 1.3f, prereq = "speed1", prereqLevel = 1, milestone = false, effect = V18Effect.OxygenUnlock, perLevel = 1.0f },
            new V18Node { id = "oxygen_3_1", name = "산소 탱크", title = "산소 탱크 · 흡입 가속", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 1, pos = new Vector2(-583f, 471f), maxLevel = 5, baseCost = 252, growth = 1.3f, prereq = "oxygen_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.OxygenPower, perLevel = 0.03f },
            new V18Node { id = "oxygen_3_2", name = "가속 호흡", title = "가속 호흡 · 이동 가속", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 2, pos = new Vector2(-668f, 538f), maxLevel = 3, baseCost = 840, growth = 1.3f, prereq = "oxygen_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.OxygenMove, perLevel = 0.02f },
            new V18Node { id = "oxygen_3_3", name = "산소 유지", title = "산소 유지 · 피버 유지", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 3, pos = new Vector2(-753f, 605f), maxLevel = 3, baseCost = 2520, growth = 1.3f, prereq = "oxygen_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.OxygenFeverHold, perLevel = 0.04f },
            new V18Node { id = "oxygen_3_4", name = "산소 과급", title = "산소 과급 · 흡입 과급", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 4, pos = new Vector2(-839f, 671f), maxLevel = 3, baseCost = 7280, growth = 1.3f, prereq = "oxygen_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.OxygenPower, perLevel = 0.04f },
            new V18Node { id = "oxygen_3_5", name = "산소 질주", title = "산소 질주 · 이동 질주", desc = "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", branch = 4, lane = 3, tier = 5, pos = new Vector2(-924f, 738f), maxLevel = 3, baseCost = 19600, growth = 1.3f, prereq = "oxygen_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.OxygenMove, perLevel = 0.03f },
            new V18Node { id = "fever_0_0", name = "피버 점화", title = "피버 점화 · 점화", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 0, pos = new Vector2(165f, 620f), maxLevel = 5, baseCost = 66, growth = 1.3f, prereq = "fever", prereqLevel = 1, milestone = false, effect = V18Effect.FeverGain, perLevel = 0.02f },
            new V18Node { id = "fever_0_1", name = "흡입 열기", title = "흡입 열기 · 열기", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 1, pos = new Vector2(195f, 724f), maxLevel = 5, baseCost = 270, growth = 1.3f, prereq = "fever_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.FeverGain, perLevel = 0.02f },
            new V18Node { id = "fever_0_2", name = "연속 게이지", title = "연속 게이지 · 게이지", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 2, pos = new Vector2(224f, 828f), maxLevel = 3, baseCost = 900, growth = 1.3f, prereq = "fever_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.FeverGain, perLevel = 0.022f },
            new V18Node { id = "fever_0_3", name = "불씨", title = "불씨 · 불씨", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 3, pos = new Vector2(254f, 932f), maxLevel = 3, baseCost = 2700, growth = 1.3f, prereq = "fever_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.FeverGain, perLevel = 0.022f },
            new V18Node { id = "fever_0_4", name = "가속 게이지", title = "가속 게이지 · 가속", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 4, pos = new Vector2(284f, 1036f), maxLevel = 3, baseCost = 7800, growth = 1.3f, prereq = "fever_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.FeverGain, perLevel = 0.025f },
            new V18Node { id = "fever_0_5", name = "과열", title = "과열 · 과열", desc = "흡입할 때 피버 게이지가 더 빨리 찬다", branch = 5, lane = 0, tier = 5, pos = new Vector2(314f, 1140f), maxLevel = 3, baseCost = 21000, growth = 1.3f, prereq = "fever_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.FeverGain, perLevel = 0.028f },
            new V18Node { id = "fever_1_0", name = "피버 유지", title = "피버 유지 · 유지", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 0, pos = new Vector2(55f, 620f), maxLevel = 5, baseCost = 66, growth = 1.3f, prereq = "hold2", prereqLevel = 1, milestone = false, effect = V18Effect.FeverHold, perLevel = 0.02f },
            new V18Node { id = "fever_1_1", name = "열 보존", title = "열 보존 · 보존", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 1, pos = new Vector2(65f, 724f), maxLevel = 5, baseCost = 270, growth = 1.3f, prereq = "fever_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.FeverHold, perLevel = 0.02f },
            new V18Node { id = "fever_1_2", name = "식지 않는 열", title = "식지 않는 열 · 지속", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 2, pos = new Vector2(75f, 828f), maxLevel = 3, baseCost = 900, growth = 1.3f, prereq = "fever_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.FeverHold, perLevel = 0.022f },
            new V18Node { id = "fever_1_3", name = "잔불", title = "잔불 · 잔불", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 3, pos = new Vector2(85f, 932f), maxLevel = 3, baseCost = 2700, growth = 1.3f, prereq = "fever_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.FeverHold, perLevel = 0.022f },
            new V18Node { id = "fever_1_4", name = "온기 유지", title = "온기 유지 · 온기", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 4, pos = new Vector2(95f, 1036f), maxLevel = 3, baseCost = 7800, growth = 1.3f, prereq = "fever_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.FeverHold, perLevel = 0.025f },
            new V18Node { id = "fever_1_5", name = "불꽃 유지", title = "불꽃 유지 · 불꽃", desc = "피버가 천천히 식어 더 오래 간다", branch = 5, lane = 1, tier = 5, pos = new Vector2(105f, 1140f), maxLevel = 3, baseCost = 21000, growth = 1.3f, prereq = "fever_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.FeverHold, perLevel = 0.028f },
            new V18Node { id = "fever_2_0", name = "피버 증폭", title = "피버 증폭 · 증폭", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 0, pos = new Vector2(-55f, 620f), maxLevel = 5, baseCost = 66, growth = 1.3f, prereq = "fever", prereqLevel = 1, milestone = false, effect = V18Effect.FeverMax, perLevel = 0.02f },
            new V18Node { id = "fever_2_1", name = "불꽃 세기", title = "불꽃 세기 · 불꽃", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 1, pos = new Vector2(-65f, 724f), maxLevel = 5, baseCost = 270, growth = 1.3f, prereq = "fever_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.FeverMax, perLevel = 0.02f },
            new V18Node { id = "fever_2_2", name = "열폭발", title = "열폭발 · 열폭발", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 2, pos = new Vector2(-75f, 828f), maxLevel = 3, baseCost = 900, growth = 1.3f, prereq = "fever_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.FeverMax, perLevel = 0.022f },
            new V18Node { id = "fever_2_3", name = "광란", title = "광란 · 광란", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 3, pos = new Vector2(-85f, 932f), maxLevel = 3, baseCost = 2700, growth = 1.3f, prereq = "fever_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.FeverMax, perLevel = 0.022f },
            new V18Node { id = "fever_2_4", name = "맥시멈 피버", title = "맥시멈 피버 · 맥시멈", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 4, pos = new Vector2(-95f, 1036f), maxLevel = 3, baseCost = 7800, growth = 1.3f, prereq = "fever_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.FeverMax, perLevel = 0.025f },
            new V18Node { id = "fever_2_5", name = "불꽃 폭주", title = "불꽃 폭주 · 폭주", desc = "피버 중 범위·속도 상승폭이 커진다", branch = 5, lane = 2, tier = 5, pos = new Vector2(-104f, 1140f), maxLevel = 3, baseCost = 21000, growth = 1.3f, prereq = "fever_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.FeverMax, perLevel = 0.028f },
            new V18Node { id = "fever_3_0", name = "피버 수익", title = "피버 수익 · 수익", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 0, pos = new Vector2(-165f, 620f), maxLevel = 5, baseCost = 66, growth = 1.3f, prereq = "fever", prereqLevel = 1, milestone = false, effect = V18Effect.FeverPrice, perLevel = 0.01f },
            new V18Node { id = "fever_3_1", name = "흥정 실력", title = "흥정 실력 · 흥정", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 1, pos = new Vector2(-195f, 724f), maxLevel = 5, baseCost = 270, growth = 1.3f, prereq = "fever_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.FeverPrice, perLevel = 0.01f },
            new V18Node { id = "fever_3_2", name = "바쁜 손", title = "바쁜 손 · 바쁜 손", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 2, pos = new Vector2(-224f, 828f), maxLevel = 3, baseCost = 900, growth = 1.3f, prereq = "fever_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.FeverPrice, perLevel = 0.012f },
            new V18Node { id = "fever_3_3", name = "열기 프리미엄", title = "열기 프리미엄 · 프리미엄", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 3, pos = new Vector2(-254f, 932f), maxLevel = 3, baseCost = 2700, growth = 1.3f, prereq = "fever_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.FeverPrice, perLevel = 0.012f },
            new V18Node { id = "fever_3_4", name = "광란 정산", title = "광란 정산 · 광란 정산", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 4, pos = new Vector2(-284f, 1036f), maxLevel = 3, baseCost = 7800, growth = 1.3f, prereq = "fever_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.FeverPrice, perLevel = 0.015f },
            new V18Node { id = "fever_3_5", name = "피버 잭팟", title = "피버 잭팟 · 잭팟", desc = "피버 중 정산 단가가 더 오른다", branch = 5, lane = 3, tier = 5, pos = new Vector2(-313f, 1140f), maxLevel = 3, baseCost = 21000, growth = 1.3f, prereq = "fever_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.FeverPrice, perLevel = 0.018f },
            new V18Node { id = "roomba_0_0", name = "로봇 모터", title = "로봇 모터 · 모터", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 0, pos = new Vector2(498f, 405f), maxLevel = 5, baseCost = 75, growth = 1.3f, prereq = "roomba", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaSpeed, perLevel = 0.02f },
            new V18Node { id = "roomba_0_1", name = "바퀴 정비", title = "바퀴 정비 · 바퀴", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 1, pos = new Vector2(583f, 471f), maxLevel = 5, baseCost = 306, growth = 1.3f, prereq = "roomba_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaSpeed, perLevel = 0.02f },
            new V18Node { id = "roomba_0_2", name = "고속 주행", title = "고속 주행 · 주행", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 2, pos = new Vector2(668f, 538f), maxLevel = 3, baseCost = 1020, growth = 1.3f, prereq = "roomba_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaSpeed, perLevel = 0.022f },
            new V18Node { id = "roomba_0_3", name = "터보 모터", title = "터보 모터 · 터보", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 3, pos = new Vector2(753f, 605f), maxLevel = 3, baseCost = 3060, growth = 1.3f, prereq = "roomba_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaSpeed, perLevel = 0.022f },
            new V18Node { id = "roomba_0_4", name = "경량 바퀴", title = "경량 바퀴 · 경량", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 4, pos = new Vector2(839f, 671f), maxLevel = 3, baseCost = 8840, growth = 1.3f, prereq = "roomba_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaSpeed, perLevel = 0.025f },
            new V18Node { id = "roomba_0_5", name = "최고 속도", title = "최고 속도 · 최고 속도", desc = "로봇청소기가 더 빨리 움직인다", branch = 6, lane = 0, tier = 5, pos = new Vector2(924f, 738f), maxLevel = 3, baseCost = 23800, growth = 1.3f, prereq = "roomba_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.RoombaSpeed, perLevel = 0.028f },
            new V18Node { id = "roomba_1_0", name = "흡입 솔", title = "흡입 솔 · 솔", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 0, pos = new Vector2(409f, 469f), maxLevel = 5, baseCost = 75, growth = 1.3f, prereq = "roomba", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaPickup, perLevel = 0.01f },
            new V18Node { id = "roomba_1_1", name = "수거 브러시", title = "수거 브러시 · 브러시", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 1, pos = new Vector2(478f, 548f), maxLevel = 5, baseCost = 306, growth = 1.3f, prereq = "roomba_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaPickup, perLevel = 0.01f },
            new V18Node { id = "roomba_1_2", name = "집게 팔", title = "집게 팔 · 집게", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 2, pos = new Vector2(547f, 626f), maxLevel = 3, baseCost = 1020, growth = 1.3f, prereq = "roomba_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaPickup, perLevel = 0.012f },
            new V18Node { id = "roomba_1_3", name = "빠른 수거", title = "빠른 수거 · 수거", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 3, pos = new Vector2(616f, 704f), maxLevel = 3, baseCost = 3060, growth = 1.3f, prereq = "roomba_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaPickup, perLevel = 0.012f },
            new V18Node { id = "roomba_1_4", name = "이중 브러시", title = "이중 브러시 · 이중 솔", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 4, pos = new Vector2(685f, 783f), maxLevel = 3, baseCost = 8840, growth = 1.3f, prereq = "roomba_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaPickup, perLevel = 0.015f },
            new V18Node { id = "roomba_1_5", name = "자동 집게", title = "자동 집게 · 자동 집게", desc = "로봇이 쓰레기를 더 자주 문다(항상 보조)", branch = 6, lane = 1, tier = 5, pos = new Vector2(755f, 861f), maxLevel = 3, baseCost = 23800, growth = 1.3f, prereq = "roomba_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.RoombaPickup, perLevel = 0.018f },
            new V18Node { id = "roomba_2_0", name = "먼지 센서", title = "먼지 센서 · 센서", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 0, pos = new Vector2(320f, 534f), maxLevel = 5, baseCost = 75, growth = 1.3f, prereq = "roomba", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaDetect, perLevel = 0.02f },
            new V18Node { id = "roomba_2_1", name = "탐지 범위", title = "탐지 범위 · 탐지", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 1, pos = new Vector2(373f, 624f), maxLevel = 5, baseCost = 306, growth = 1.3f, prereq = "roomba_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaDetect, perLevel = 0.02f },
            new V18Node { id = "roomba_2_2", name = "적외선 센서", title = "적외선 센서 · 적외선", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 2, pos = new Vector2(426f, 714f), maxLevel = 3, baseCost = 1020, growth = 1.3f, prereq = "roomba_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaDetect, perLevel = 0.022f },
            new V18Node { id = "roomba_2_3", name = "넓은 탐지", title = "넓은 탐지 · 넓은 탐지", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 3, pos = new Vector2(479f, 804f), maxLevel = 3, baseCost = 3060, growth = 1.3f, prereq = "roomba_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaDetect, perLevel = 0.022f },
            new V18Node { id = "roomba_2_4", name = "레이더 센서", title = "레이더 센서 · 레이더", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 4, pos = new Vector2(532f, 894f), maxLevel = 3, baseCost = 8840, growth = 1.3f, prereq = "roomba_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaDetect, perLevel = 0.025f },
            new V18Node { id = "roomba_2_5", name = "전방위 탐지", title = "전방위 탐지 · 전방위", desc = "로봇이 더 멀리 있는 쓰레기를 찾아간다", branch = 6, lane = 2, tier = 5, pos = new Vector2(586f, 984f), maxLevel = 3, baseCost = 23800, growth = 1.3f, prereq = "roomba_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.RoombaDetect, perLevel = 0.028f },
            new V18Node { id = "roomba_3_0", name = "배터리", title = "배터리 · 기본 배터리", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 0, pos = new Vector2(231f, 599f), maxLevel = 5, baseCost = 75, growth = 1.3f, prereq = "rspd1", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaBattery, perLevel = 0.02f },
            new V18Node { id = "roomba_3_1", name = "대용량 셀", title = "대용량 셀 · 대용량", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 1, pos = new Vector2(268f, 700f), maxLevel = 5, baseCost = 306, growth = 1.3f, prereq = "roomba_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaBattery, perLevel = 0.02f },
            new V18Node { id = "roomba_3_2", name = "충전 효율", title = "충전 효율 · 충전", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 2, pos = new Vector2(305f, 802f), maxLevel = 3, baseCost = 1020, growth = 1.3f, prereq = "roomba_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaBattery, perLevel = 0.022f },
            new V18Node { id = "roomba_3_3", name = "연속 가동", title = "연속 가동 · 연속", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 3, pos = new Vector2(342f, 903f), maxLevel = 3, baseCost = 3060, growth = 1.3f, prereq = "roomba_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaBattery, perLevel = 0.022f },
            new V18Node { id = "roomba_3_4", name = "급속 충전", title = "급속 충전 · 급속", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 4, pos = new Vector2(379f, 1005f), maxLevel = 3, baseCost = 8840, growth = 1.3f, prereq = "roomba_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.RoombaBattery, perLevel = 0.025f },
            new V18Node { id = "roomba_3_5", name = "무한 배터리", title = "무한 배터리 · 무한", desc = "배터리가 좋아져 쉬지 않고 돌아다닌다", branch = 6, lane = 3, tier = 5, pos = new Vector2(416f, 1107f), maxLevel = 3, baseCost = 23800, growth = 1.3f, prereq = "roomba_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.RoombaBattery, perLevel = 0.028f },
            new V18Node { id = "vehicle_0_0", name = "리어카 보강", title = "리어카 보강 · 보강", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 0, pos = new Vector2(539f, -349f), maxLevel = 5, baseCost = 79, growth = 1.3f, prereq = "cart", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleBulk, perLevel = 0.01f },
            new V18Node { id = "vehicle_0_1", name = "짐칸 확장", title = "짐칸 확장 · 짐칸", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 1, pos = new Vector2(628f, -409f), maxLevel = 5, baseCost = 324, growth = 1.3f, prereq = "vehicle_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleBulk, perLevel = 0.01f },
            new V18Node { id = "vehicle_0_2", name = "차량 단가", title = "차량 단가 · 단가", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 2, pos = new Vector2(718f, -469f), maxLevel = 3, baseCost = 1080, growth = 1.3f, prereq = "vehicle_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleBulk, perLevel = 0.012f },
            new V18Node { id = "vehicle_0_3", name = "화물 정산", title = "화물 정산 · 화물", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 3, pos = new Vector2(808f, -530f), maxLevel = 3, baseCost = 3240, growth = 1.3f, prereq = "vehicle_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleBulk, perLevel = 0.012f },
            new V18Node { id = "vehicle_0_4", name = "대량 계약", title = "대량 계약 · 계약", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 4, pos = new Vector2(898f, -590f), maxLevel = 3, baseCost = 9360, growth = 1.3f, prereq = "vehicle_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleBulk, perLevel = 0.015f },
            new V18Node { id = "vehicle_0_5", name = "물류 왕복", title = "물류 왕복 · 물류", desc = "차량 정산 배수가 오른다", branch = 7, lane = 0, tier = 5, pos = new Vector2(987f, -650f), maxLevel = 3, baseCost = 25200, growth = 1.3f, prereq = "vehicle_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.VehicleBulk, perLevel = 0.018f },
            new V18Node { id = "vehicle_1_0", name = "정산 배수", title = "정산 배수 · 배수", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 0, pos = new Vector2(573f, -244f), maxLevel = 5, baseCost = 79, growth = 1.3f, prereq = "damas", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleMult, perLevel = 0.01f },
            new V18Node { id = "vehicle_1_1", name = "거래처 계약", title = "거래처 계약 · 계약", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 1, pos = new Vector2(669f, -285f), maxLevel = 5, baseCost = 324, growth = 1.3f, prereq = "vehicle_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleMult, perLevel = 0.01f },
            new V18Node { id = "vehicle_1_2", name = "직거래", title = "직거래 · 직거래", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 2, pos = new Vector2(764f, -327f), maxLevel = 3, baseCost = 1080, growth = 1.3f, prereq = "vehicle_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleMult, perLevel = 0.012f },
            new V18Node { id = "vehicle_1_3", name = "도매 납품", title = "도매 납품 · 도매", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 3, pos = new Vector2(860f, -369f), maxLevel = 3, baseCost = 3240, growth = 1.3f, prereq = "vehicle_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleMult, perLevel = 0.012f },
            new V18Node { id = "vehicle_1_4", name = "독점 계약", title = "독점 계약 · 독점", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 4, pos = new Vector2(956f, -410f), maxLevel = 3, baseCost = 9360, growth = 1.3f, prereq = "vehicle_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.VehicleMult, perLevel = 0.015f },
            new V18Node { id = "vehicle_1_5", name = "프리미엄 거래", title = "프리미엄 거래 · 프리미엄", desc = "차량 거래 배수가 한 번 더 곱해진다", branch = 7, lane = 1, tier = 5, pos = new Vector2(1052f, -452f), maxLevel = 3, baseCost = 25200, growth = 1.3f, prereq = "vehicle_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.VehicleMult, perLevel = 0.018f },
            new V18Node { id = "vehicle_2_0", name = "원격 정산", title = "원격 정산 · 기준 80%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 0, pos = new Vector2(607f, -139f), maxLevel = 5, baseCost = 79, growth = 1.3f, prereq = "truck1t", prereqLevel = 1, milestone = false, effect = V18Effect.BulkThreshold, perLevel = 0.01f },
            new V18Node { id = "vehicle_2_1", name = "조기 정산", title = "조기 정산 · 기준 70%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 1, pos = new Vector2(709f, -162f), maxLevel = 5, baseCost = 324, growth = 1.3f, prereq = "vehicle_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.BulkThreshold, perLevel = 0.01f },
            new V18Node { id = "vehicle_2_2", name = "절반 정산", title = "절반 정산 · 기준 60%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 2, pos = new Vector2(811f, -185f), maxLevel = 3, baseCost = 1080, growth = 1.3f, prereq = "vehicle_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.BulkThreshold, perLevel = 0.012f },
            new V18Node { id = "vehicle_2_3", name = "상시 정산", title = "상시 정산 · 기준 55%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 3, pos = new Vector2(913f, -207f), maxLevel = 3, baseCost = 3240, growth = 1.3f, prereq = "vehicle_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.BulkThreshold, perLevel = 0.012f },
            new V18Node { id = "vehicle_2_4", name = "자동 정산", title = "자동 정산 · 기준 45%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 4, pos = new Vector2(1015f, -230f), maxLevel = 3, baseCost = 9360, growth = 1.3f, prereq = "vehicle_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.BulkThreshold, perLevel = 0.015f },
            new V18Node { id = "vehicle_2_5", name = "완전 자동", title = "완전 자동 · 기준 35%", desc = "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", branch = 7, lane = 2, tier = 5, pos = new Vector2(1116f, -253f), maxLevel = 3, baseCost = 25200, growth = 1.3f, prereq = "vehicle_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.BulkThreshold, perLevel = 0.02f },
            new V18Node { id = "vehicle_3_0", name = "배차 단축", title = "배차 단축 · 쿨다운 단축", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 0, pos = new Vector2(641f, -35f), maxLevel = 5, baseCost = 79, growth = 1.3f, prereq = "damas", prereqLevel = 1, milestone = false, effect = V18Effect.RemoteCooldown, perLevel = 0.05f },
            new V18Node { id = "vehicle_3_1", name = "자동 흡입", title = "자동 흡입 · 대량 보너스", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 1, pos = new Vector2(749f, -39f), maxLevel = 5, baseCost = 324, growth = 1.3f, prereq = "vehicle_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.BulkSettle, perLevel = 0.01f },
            new V18Node { id = "vehicle_3_2", name = "문 앞 정산", title = "문 앞 정산 · 보너스 증가", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 2, pos = new Vector2(857f, -42f), maxLevel = 3, baseCost = 1080, growth = 1.3f, prereq = "vehicle_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.BulkSettle, perLevel = 0.01f },
            new V18Node { id = "vehicle_3_3", name = "원격 수거", title = "원격 수거 · 쿨다운 추가", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 3, pos = new Vector2(965f, -46f), maxLevel = 3, baseCost = 3240, growth = 1.3f, prereq = "vehicle_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.RemoteCooldown, perLevel = 0.05f },
            new V18Node { id = "vehicle_3_4", name = "무인 정산", title = "무인 정산 · 보너스 강화", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 4, pos = new Vector2(1073f, -50f), maxLevel = 3, baseCost = 9360, growth = 1.3f, prereq = "vehicle_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.BulkSettle, perLevel = 0.012f },
            new V18Node { id = "vehicle_3_5", name = "완전 자동화", title = "완전 자동화 · 쿨다운 완성", desc = "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", branch = 7, lane = 3, tier = 5, pos = new Vector2(1181f, -54f), maxLevel = 3, baseCost = 25200, growth = 1.3f, prereq = "vehicle_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.RemoteCooldown, perLevel = 0.06f },
            new V18Node { id = "gear_0_0", name = "원기옥", title = "원기옥 · 빔포 해금", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 0, pos = new Vector2(641f, 35f), maxLevel = 1, baseCost = 18000, growth = 1.0f, prereq = "vacuum", prereqLevel = 1, milestone = false, effect = V18Effect.OrbUnlock, perLevel = 1.0f },
            new V18Node { id = "gear_0_1", name = "끌어당김", title = "끌어당김 · 끌어모으기", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 1, pos = new Vector2(749f, 39f), maxLevel = 5, baseCost = 504, growth = 1.3f, prereq = "gear_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.OrbPull, perLevel = 0.05f },
            new V18Node { id = "gear_0_2", name = "구슬 반경", title = "구슬 반경 · 모으는 원", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 2, pos = new Vector2(857f, 42f), maxLevel = 5, baseCost = 1680, growth = 1.3f, prereq = "gear_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.OrbRadius, perLevel = 0.06f },
            new V18Node { id = "gear_0_3", name = "뭉치기", title = "뭉치기 · 구슬 성장", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 3, pos = new Vector2(965f, 46f), maxLevel = 5, baseCost = 5040, growth = 1.3f, prereq = "gear_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.OrbCharge, perLevel = 0.07f },
            new V18Node { id = "gear_0_4", name = "구슬 크기", title = "구슬 크기 · 한 방 용량", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 4, pos = new Vector2(1073f, 50f), maxLevel = 5, baseCost = 14560, growth = 1.3f, prereq = "gear_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.OrbSize, perLevel = 0.08f },
            new V18Node { id = "gear_0_5", name = "풀충 발사", title = "풀충 발사 · 즉시 발사", desc = "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", branch = 8, lane = 0, tier = 5, pos = new Vector2(1181f, 54f), maxLevel = 1, baseCost = 39200, growth = 1.3f, prereq = "gear_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.OrbAuto, perLevel = 1.0f },
            new V18Node { id = "gear_1_0", name = "구슬 정산", title = "구슬 정산 · 정산 상승", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 0, pos = new Vector2(607f, 139f), maxLevel = 1, baseCost = 9000, growth = 1.0f, prereq = "gear_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.OrbCash, perLevel = 0.02f },
            new V18Node { id = "gear_1_1", name = "터짐 반경", title = "터짐 반경 · 광역 터짐", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 1, pos = new Vector2(709f, 162f), maxLevel = 5, baseCost = 504, growth = 1.3f, prereq = "gear_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.OrbBurst, perLevel = 0.03f },
            new V18Node { id = "gear_1_2", name = "발사 속도", title = "발사 속도 · 빠른 발사", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 2, pos = new Vector2(811f, 185f), maxLevel = 3, baseCost = 1680, growth = 1.3f, prereq = "gear_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.OrbFly, perLevel = 0.04f },
            new V18Node { id = "gear_1_3", name = "압축 정산", title = "압축 정산 · 정산 강화", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 3, pos = new Vector2(913f, 207f), maxLevel = 3, baseCost = 5040, growth = 1.3f, prereq = "gear_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.OrbCash, perLevel = 0.03f },
            new V18Node { id = "gear_1_4", name = "충격파", title = "충격파 · 광역 확장", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 4, pos = new Vector2(1015f, 230f), maxLevel = 3, baseCost = 14560, growth = 1.3f, prereq = "gear_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.OrbBurst, perLevel = 0.04f },
            new V18Node { id = "gear_1_5", name = "대형 정산", title = "대형 정산 · 정산 완성", desc = "구슬이 터질 때 정산이 더 크게 터진다", branch = 8, lane = 1, tier = 5, pos = new Vector2(1116f, 253f), maxLevel = 3, baseCost = 39200, growth = 1.3f, prereq = "gear_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.OrbCash, perLevel = 0.04f },
            new V18Node { id = "gear_2_0", name = "블랙홀", title = "블랙홀 · 건 해금", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 0, pos = new Vector2(573f, 244f), maxLevel = 1, baseCost = 160000, growth = 1.0f, prereq = "gear_0_4", prereqLevel = 1, milestone = false, effect = V18Effect.BhUnlock, perLevel = 1.0f },
            new V18Node { id = "gear_2_1", name = "사건 지평선", title = "사건 지평선 · 반경 확장", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 1, pos = new Vector2(669f, 285f), maxLevel = 5, baseCost = 504, growth = 1.3f, prereq = "gear_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.BhRadius, perLevel = 0.05f },
            new V18Node { id = "gear_2_2", name = "쌍 블랙홀", title = "쌍 블랙홀 · 2개 전개", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 2, pos = new Vector2(764f, 327f), maxLevel = 1, baseCost = 1680, growth = 1.3f, prereq = "gear_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.BhCount, perLevel = 1.0f },
            new V18Node { id = "gear_2_3", name = "회전", title = "회전 · 소용돌이", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 3, pos = new Vector2(860f, 369f), maxLevel = 4, baseCost = 5040, growth = 1.3f, prereq = "gear_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.BhSpin, perLevel = 0.06f },
            new V18Node { id = "gear_2_4", name = "지속", title = "지속 · 유지 시간", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 4, pos = new Vector2(956f, 410f), maxLevel = 4, baseCost = 14560, growth = 1.3f, prereq = "gear_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.BhDuration, perLevel = 0.07f },
            new V18Node { id = "gear_2_5", name = "삼중 블랙홀", title = "삼중 블랙홀 · 3개 전개", desc = "작은 구멍이 주변을 소용돌이로 빨아들인다", branch = 8, lane = 2, tier = 5, pos = new Vector2(1052f, 452f), maxLevel = 1, baseCost = 39200, growth = 1.3f, prereq = "gear_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.BhCount, perLevel = 1.0f },
            new V18Node { id = "gear_3_0", name = "화이트홀", title = "화이트홀 · 자동 정산 해금", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 0, pos = new Vector2(539f, 349f), maxLevel = 1, baseCost = 1500000, growth = 1.0f, prereq = "gear_2_5", prereqLevel = 1, milestone = false, effect = V18Effect.WhUnlock, perLevel = 1.0f },
            new V18Node { id = "gear_3_1", name = "토해내기", title = "토해내기 · 정산 상승", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 1, pos = new Vector2(628f, 409f), maxLevel = 5, baseCost = 504, growth = 1.3f, prereq = "gear_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.WhCash, perLevel = 0.02f },
            new V18Node { id = "gear_3_2", name = "중력", title = "중력 · 흡입 강화", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 2, pos = new Vector2(718f, 469f), maxLevel = 3, baseCost = 1680, growth = 1.3f, prereq = "gear_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.BhPull, perLevel = 0.06f },
            new V18Node { id = "gear_3_3", name = "분출 정산", title = "분출 정산 · 정산 강화", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 3, pos = new Vector2(808f, 530f), maxLevel = 3, baseCost = 5040, growth = 1.3f, prereq = "gear_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.WhCash, perLevel = 0.03f },
            new V18Node { id = "gear_3_4", name = "특이점", title = "특이점 · 흡입 완성", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 4, pos = new Vector2(898f, 590f), maxLevel = 3, baseCost = 14560, growth = 1.3f, prereq = "gear_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.BhPull, perLevel = 0.07f },
            new V18Node { id = "gear_3_5", name = "무한 분출", title = "무한 분출 · 자동 정산 완성", desc = "빨아들인 쓰레기를 트럭 위로 토해낸다", branch = 8, lane = 3, tier = 5, pos = new Vector2(987f, 650f), maxLevel = 1, baseCost = 39200, growth = 1.3f, prereq = "gear_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.WhAuto, perLevel = 1.0f },
            new V18Node { id = "trash_0_0", name = "캔·병", title = "캔·병 · 고가치 해금", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 0, pos = new Vector2(-641f, -35f), maxLevel = 1, baseCost = 2000, growth = 1.0f, prereq = "fist1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashCan, perLevel = 1.0f },
            new V18Node { id = "trash_0_1", name = "고가치 비중", title = "고가치 비중 · 비중 상향", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 1, pos = new Vector2(-749f, -39f), maxLevel = 5, baseCost = 450, growth = 1.3f, prereq = "trash_0_0", prereqLevel = 1, milestone = false, effect = V18Effect.TrashRatio, perLevel = 0.02f },
            new V18Node { id = "trash_0_2", name = "전자폐기물", title = "전자폐기물 · 전자 해금", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 2, pos = new Vector2(-857f, -42f), maxLevel = 1, baseCost = 30000, growth = 1.0f, prereq = "trash_0_1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashEwaste, perLevel = 1.0f },
            new V18Node { id = "trash_0_3", name = "폐가전 비중", title = "폐가전 비중 · 비중 강화", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 3, pos = new Vector2(-965f, -46f), maxLevel = 5, baseCost = 4500, growth = 1.3f, prereq = "trash_0_2", prereqLevel = 1, milestone = false, effect = V18Effect.TrashRatio, perLevel = 0.025f },
            new V18Node { id = "trash_0_4", name = "대형 폐가전", title = "대형 폐가전 · 대형 해금", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 4, pos = new Vector2(-1073f, -50f), maxLevel = 1, baseCost = 200000, growth = 1.0f, prereq = "trash_0_3", prereqLevel = 1, milestone = false, effect = V18Effect.TrashBig, perLevel = 1.0f },
            new V18Node { id = "trash_0_5", name = "금괴", title = "금괴 · 금괴 해금", desc = "묵직하고 값나가는 쓰레기가 섞여 나온다", branch = 9, lane = 0, tier = 5, pos = new Vector2(-1181f, -54f), maxLevel = 1, baseCost = 800000, growth = 1.0f, prereq = "trash_0_4", prereqLevel = 1, milestone = true, effect = V18Effect.TrashGold, perLevel = 1.0f },
            new V18Node { id = "trash_1_0", name = "두꺼운 재질", title = "두꺼운 재질 · 내구도 상향", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 0, pos = new Vector2(-607f, -139f), maxLevel = 5, baseCost = 110, growth = 1.3f, prereq = "fist1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDurability, perLevel = 0.1f },
            new V18Node { id = "trash_1_1", name = "파쇄력", title = "파쇄력 · 깎는 힘", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 1, pos = new Vector2(-709f, -162f), maxLevel = 5, baseCost = 450, growth = 1.3f, prereq = "trash_1_0", prereqLevel = 1, milestone = false, effect = V18Effect.HeavyDamage, perLevel = 0.06f },
            new V18Node { id = "trash_1_2", name = "철갑", title = "철갑 · 내구도 강화", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 2, pos = new Vector2(-811f, -185f), maxLevel = 5, baseCost = 1500, growth = 1.3f, prereq = "trash_1_1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDurability, perLevel = 0.12f },
            new V18Node { id = "trash_1_3", name = "분쇄", title = "분쇄 · 깎는 힘 강화", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 3, pos = new Vector2(-913f, -207f), maxLevel = 5, baseCost = 4500, growth = 1.3f, prereq = "trash_1_2", prereqLevel = 1, milestone = false, effect = V18Effect.HeavyDamage, perLevel = 0.08f },
            new V18Node { id = "trash_1_4", name = "합금", title = "합금 · 내구도 완성", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 4, pos = new Vector2(-1015f, -230f), maxLevel = 5, baseCost = 13000, growth = 1.3f, prereq = "trash_1_3", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDurability, perLevel = 0.14f },
            new V18Node { id = "trash_1_5", name = "완전 분쇄", title = "완전 분쇄 · 분쇄 완성", desc = "두꺼운 쓰레기는 여러 번 깎아야 터진다", branch = 9, lane = 1, tier = 5, pos = new Vector2(-1116f, -253f), maxLevel = 5, baseCost = 35000, growth = 1.3f, prereq = "trash_1_4", prereqLevel = 1, milestone = true, effect = V18Effect.HeavyDamage, perLevel = 0.1f },
            new V18Node { id = "trash_2_0", name = "고가치 단가", title = "고가치 단가 · 단가 상승", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 0, pos = new Vector2(-573f, -244f), maxLevel = 5, baseCost = 110, growth = 1.3f, prereq = "price1", prereqLevel = 1, milestone = false, effect = V18Effect.HeavyValue, perLevel = 0.02f },
            new V18Node { id = "trash_2_1", name = "분리 보너스", title = "분리 보너스 · 분리 정산", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 1, pos = new Vector2(-669f, -285f), maxLevel = 5, baseCost = 450, growth = 1.3f, prereq = "trash_2_0", prereqLevel = 1, milestone = false, effect = V18Effect.SortBonus, perLevel = 0.02f },
            new V18Node { id = "trash_2_2", name = "재활용 단가", title = "재활용 단가 · 단가 강화", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 2, pos = new Vector2(-764f, -327f), maxLevel = 3, baseCost = 1500, growth = 1.3f, prereq = "trash_2_1", prereqLevel = 1, milestone = false, effect = V18Effect.HeavyValue, perLevel = 0.025f },
            new V18Node { id = "trash_2_3", name = "등급 보정", title = "등급 보정 · 희귀 보정", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 3, pos = new Vector2(-860f, -369f), maxLevel = 3, baseCost = 4500, growth = 1.3f, prereq = "trash_2_2", prereqLevel = 1, milestone = false, effect = V18Effect.RarityBonus, perLevel = 0.025f },
            new V18Node { id = "trash_2_4", name = "프리미엄", title = "프리미엄 · 단가 완성", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 4, pos = new Vector2(-956f, -410f), maxLevel = 3, baseCost = 13000, growth = 1.3f, prereq = "trash_2_3", prereqLevel = 1, milestone = false, effect = V18Effect.HeavyValue, perLevel = 0.03f },
            new V18Node { id = "trash_2_5", name = "금괴 잭팟", title = "금괴 잭팟 · 잭팟", desc = "고가치 폐기물의 정산 단가가 오른다", branch = 9, lane = 2, tier = 5, pos = new Vector2(-1052f, -452f), maxLevel = 3, baseCost = 35000, growth = 1.3f, prereq = "trash_2_4", prereqLevel = 1, milestone = true, effect = V18Effect.HeavyJackpot, perLevel = 0.05f },
            new V18Node { id = "trash_3_0", name = "물량", title = "물량 · 밀도 상승", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 0, pos = new Vector2(-539f, -349f), maxLevel = 5, baseCost = 110, growth = 1.3f, prereq = "bin1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDensity, perLevel = 0.03f },
            new V18Node { id = "trash_3_1", name = "더미 밀집", title = "더미 밀집 · 더미 뭉침", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 1, pos = new Vector2(-628f, -409f), maxLevel = 5, baseCost = 450, growth = 1.3f, prereq = "trash_3_0", prereqLevel = 1, milestone = false, effect = V18Effect.TrashCluster, perLevel = 0.04f },
            new V18Node { id = "trash_3_2", name = "추가 물량", title = "추가 물량 · 밀도 강화", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 2, pos = new Vector2(-718f, -469f), maxLevel = 5, baseCost = 1500, growth = 1.3f, prereq = "trash_3_1", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDensity, perLevel = 0.04f },
            new V18Node { id = "trash_3_3", name = "추가 웨이브", title = "추가 웨이브 · 웨이브 +1", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 3, pos = new Vector2(-808f, -530f), maxLevel = 2, baseCost = 4500, growth = 1.3f, prereq = "trash_3_2", prereqLevel = 1, milestone = false, effect = V18Effect.WaveBonus, perLevel = 1.0f },
            new V18Node { id = "trash_3_4", name = "대량 물량", title = "대량 물량 · 밀도 완성", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 4, pos = new Vector2(-898f, -590f), maxLevel = 5, baseCost = 13000, growth = 1.3f, prereq = "trash_3_3", prereqLevel = 1, milestone = false, effect = V18Effect.TrashDensity, perLevel = 0.05f },
            new V18Node { id = "trash_3_5", name = "대량 투하", title = "대량 투하 · 마지막 폭탄", desc = "더미가 두꺼워지고 웨이브가 늘어난다", branch = 9, lane = 3, tier = 5, pos = new Vector2(-987f, -650f), maxLevel = 1, baseCost = 300000, growth = 1.0f, prereq = "trash_3_4", prereqLevel = 1, milestone = true, effect = V18Effect.MegaDump, perLevel = 1.0f },
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

        /// <summary>신규 240노드를 전부 만렙까지 올리는 총비용(밸런스 QA·HUD 표시용).</summary>
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
