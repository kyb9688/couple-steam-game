using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.6 강화 트리 효과 종류. 각 노드는 '효과 1종 × 작은 상승폭'만 담당하고,
    /// 매니저가 레벨 합계(V16Sum)를 실제 계산식에 곱/더한다.
    /// → 한 번 찍는 상승폭은 완만하지만, 가지를 따라가면 확실히 체감되는 구조.
    /// </summary>
    public enum V16Effect
    {
        HoldSpeed,    // 흡입 속도(홀드 시간 감소) +%
        Radius,       // 흡입 반경 +%
        GrabCount,    // 동시 흡입 개수 +N
        BinCap,       // 수거통 용량 +%
        Price,        // 정산 단가 +%
        MoveSpeed,    // 이동 속도 +%
        FistPower,    // 큰 봉투 타격력 +%
        MaskFilter,   // 악취 상승 둔화 -%
        OxygenTank,   // 산소 부스트 강화 +%p
        ScrubPower,   // 걸레질 속도 +%
        RoombaSpeed,  // 로봇 이동 속도 +%
        RoombaPickup, // 로봇 수거 주기 -%
        BulkBonus,    // 정산 단가 배수 +N (차량 배수에 가산)
        SettleBonus,  // 정산 금액 +%
        BurstCash,    // 큰 봉투 터짐 현금 보너스 +$
        BlackHole,    // 봉투 터짐 연쇄 흡입 반경 +m
        FeverGain,    // 피버 게이지 획득 +%
        FeverHold     // 피버 유지(감소 둔화) +%
    }

    /// <summary>v1.6 세분화 노드 정의 (기존 28노드에 더해지는 28개 마이크로 노드).</summary>
    public class V16Node
    {
        public string id;
        public string name;      // 노드 카드 짧은 이름
        public string title;     // 상세 제목
        public string desc;      // 효과 설명 (수치는 매니저가 채움)
        public int branch;       // 0: 흡입·도구, 1: 수거·수익, 2: 기동·생존, 3: 파괴·자동화
        public Vector2 pos;      // 트리 좌표(중앙 기준, 화면 y는 +가 아래)
        public int maxLevel;
        public int baseCost;
        public float growth;
        public string prereq;
        public int prereqLevel;
        public bool milestone;
        public V16Effect effect;
        public float perLevel;

        public string EffectText(int level)
        {
            float v = perLevel * Mathf.Max(1, level);
            switch (effect)
            {
                case V16Effect.HoldSpeed: return $"흡입 속도 +{v * 100f:0.#}%";
                case V16Effect.Radius: return $"흡입 반경 +{v * 100f:0.#}%";
                case V16Effect.GrabCount: return $"동시 흡입 +{v:0}개";
                case V16Effect.BinCap: return $"수거통 용량 +{v * 100f:0.#}%";
                case V16Effect.Price: return $"정산 단가 +{v * 100f:0.#}%";
                case V16Effect.MoveSpeed: return $"이동 속도 +{v * 100f:0.#}%";
                case V16Effect.FistPower: return $"봉투 타격력 +{v * 100f:0.#}%";
                case V16Effect.MaskFilter: return $"악취 상승 -{v * 100f:0.#}%";
                case V16Effect.OxygenTank: return $"산소 부스트 +{v * 100f:0.#}%p";
                case V16Effect.ScrubPower: return $"걸레질 속도 +{v * 100f:0.#}%";
                case V16Effect.RoombaSpeed: return $"로봇 이동 +{v * 100f:0.#}%";
                case V16Effect.RoombaPickup: return $"로봇 수거 주기 -{v * 100f:0.#}%";
                case V16Effect.BulkBonus: return $"정산 배수 +{v:0.00}";
                case V16Effect.SettleBonus: return $"정산 금액 +{v * 100f:0.#}%";
                case V16Effect.BurstCash: return $"봉투 터짐 보너스 +${v:0}";
                case V16Effect.BlackHole: return $"연쇄 흡입 반경 +{v:0.00}m";
                case V16Effect.FeverGain: return $"피버 획득 +{v * 100f:0.#}%";
                case V16Effect.FeverHold: return $"피버 유지 +{v * 100f:0.#}%";
                default: return "";
            }
        }
    }

    public static class UpgradeTreeV16
    {
        // 좌표계: 중앙(0,0) 기준. 위(-y)가 흡입·수거, 아래(+y)가 기동·자동화.
        // 기존 28노드가 ±472/±124 안에 있으므로, 새 노드는 바깥(±560~±820)으로 길게 뻗는다.
        public static readonly V16Node[] Nodes =
        {
            // ================= 가지 0: 흡입 · 도구 (좌상, 바깥으로) =================
            new V16Node { id = "grip",   name = "손아귀",   title = "미세 손아귀 (흡입 미세 조정)", desc = "홀드 시간이 아주 조금씩 줄어든다", branch = 0, pos = new Vector2(-96, -14),  maxLevel = 5, baseCost = 10,  growth = 1.55f, prereq = "hold1",  prereqLevel = 1, effect = V16Effect.HoldSpeed, perLevel = 0.015f },
            new V16Node { id = "reach",  name = "손끝 감각", title = "손끝 거리 감각",              desc = "원형 흡입 범위가 조금 넓어진다",  branch = 0, pos = new Vector2(-190, -14), maxLevel = 5, baseCost = 11,  growth = 1.55f, prereq = "rad1",   prereqLevel = 1, effect = V16Effect.Radius,    perLevel = 0.012f },
            new V16Node { id = "focus",  name = "집중",     title = "호흡 집중 (연속 흡입)",        desc = "차징이 빨라져 초반 손맛이 살아난다", branch = 0, pos = new Vector2(-284, -14), maxLevel = 4, baseCost = 120,  growth = 1.80f, prereq = "grip",   prereqLevel = 2, effect = V16Effect.HoldSpeed, perLevel = 0.018f },
            new V16Node { id = "wrist",  name = "손목",     title = "손목 회전 (넓은 쓸이)",        desc = "빗자루 궤적이 조금 더 넓어진다",  branch = 0, pos = new Vector2(-284, -194), maxLevel = 4, baseCost = 238, growth = 1.80f, prereq = "rad2",   prereqLevel = 1, effect = V16Effect.Radius,    perLevel = 0.016f },
            new V16Node { id = "multi",  name = "멀티 락온", title = "멀티 락온 (다중 흡입)",       desc = "한 번에 락온하는 개수가 1개씩 늘어난다", branch = 0, pos = new Vector2(-378, -194), maxLevel = 3, baseCost = 672, growth = 1.95f, prereq = "focus", prereqLevel = 2, milestone = true, effect = V16Effect.GrabCount, perLevel = 1f },
            new V16Node { id = "glove",  name = "장갑 안감", title = "두꺼운 장갑 안감",             desc = "큰 봉투를 때릴 때 데미지가 오른다", branch = 0, pos = new Vector2(-566, -194), maxLevel = 5, baseCost = 364, growth = 1.85f, prereq = "wrist",  prereqLevel = 2, effect = V16Effect.FistPower, perLevel = 0.03f },
            new V16Node { id = "nozzle", name = "노즐 개량", title = "와이드 노즐 개량",             desc = "진공 단계 반경을 더 넓힌다",       branch = 0, pos = new Vector2(-566, -124), maxLevel = 4, baseCost = 1710, growth = 1.86f, prereq = "vacuum", prereqLevel = 1, effect = V16Effect.Radius,    perLevel = 0.020f },
            new V16Node { id = "swirl",  name = "소용돌이", title = "소용돌이 코어 (블랙홀)",       desc = "봉투가 터질 때 연쇄로 빨아들이는 범위가 넓어진다", branch = 0, pos = new Vector2(-566, -54), maxLevel = 3, baseCost = 1805, growth = 1.92f, prereq = "hold3",  prereqLevel = 1, milestone = true, effect = V16Effect.BlackHole, perLevel = 0.18f },
            new V16Node { id = "spark",  name = "불꽃",     title = "불꽃 손끝 (피버 시동)",        desc = "쓰레기를 빨 때 피버 게이지가 더 빨리 찬다", branch = 0, pos = new Vector2(-660, -124), maxLevel = 4, baseCost = 2280, growth = 1.88f, prereq = "swirl",  prereqLevel = 1, effect = V16Effect.FeverGain, perLevel = 0.06f },

            // ================= 가지 1: 수거 · 수익 (우상, 바깥으로) =================
            new V16Node { id = "scale",    name = "저울 보정", title = "낡은 저울 보정",           desc = "정산 단가를 조금 올린다",       branch = 1, pos = new Vector2(96, -14),  maxLevel = 5, baseCost = 10,  growth = 1.55f, prereq = "bin1",   prereqLevel = 1, effect = V16Effect.Price,     perLevel = 0.012f },
            new V16Node { id = "liner",    name = "봉투 두께", title = "두꺼운 수거 봉투",         desc = "수거통 용량이 조금 늘어난다",   branch = 1, pos = new Vector2(96, -194), maxLevel = 5, baseCost = 12,  growth = 1.58f, prereq = "bin1",   prereqLevel = 1, effect = V16Effect.BinCap,    perLevel = 0.015f },
            new V16Node { id = "sort",     name = "분리수거", title = "분리수거 습관",            desc = "정산 단가를 더 올린다",         branch = 1, pos = new Vector2(190, -194), maxLevel = 4, baseCost = 252, growth = 1.82f, prereq = "scale",  prereqLevel = 2, effect = V16Effect.Price,     perLevel = 0.016f },
            new V16Node { id = "wheels",   name = "바퀴",     title = "리어카 바퀴 정비",          desc = "차량 정산 배수가 오른다",       branch = 1, pos = new Vector2(284, -194), maxLevel = 4, baseCost = 480, growth = 1.84f, prereq = "bin2",   prereqLevel = 1, effect = V16Effect.BulkBonus, perLevel = 0.02f },
            new V16Node { id = "receipt",  name = "영수증",   title = "꼼꼼한 정산 영수증",        desc = "정산할 때마다 금액이 조금 더 붙는다", branch = 1, pos = new Vector2(378, -194), maxLevel = 4, baseCost = 672, growth = 1.84f, prereq = "price2", prereqLevel = 1, effect = V16Effect.SettleBonus, perLevel = 0.012f },
            new V16Node { id = "tip",      name = "팁",       title = "동네 입소문 (팁)",          desc = "큰 봉투를 터뜨릴 때 현금 보너스가 붙는다", branch = 1, pos = new Vector2(566, -54), maxLevel = 4, baseCost = 800, growth = 1.84f, prereq = "combo",  prereqLevel = 2, effect = V16Effect.BurstCash, perLevel = 10f },
            new V16Node { id = "cashback", name = "현금영수증", title = "현금영수증 발급",         desc = "정산 단가를 크게 올린다",       branch = 1, pos = new Vector2(566, -194), maxLevel = 3, baseCost = 2850, growth = 1.95f, prereq = "receipt", prereqLevel = 2, milestone = true, effect = V16Effect.Price, perLevel = 0.020f },
            new V16Node { id = "ledger",   name = "장부",     title = "꼼꼼한 장부",              desc = "정산 배수를 더 올린다",         branch = 1, pos = new Vector2(660, -124), maxLevel = 3, baseCost = 2565, growth = 1.92f, prereq = "wheels", prereqLevel = 2, effect = V16Effect.BulkBonus, perLevel = 0.025f },

            // ================= 가지 2: 기동 · 생존 · 걸레 (좌하, 바깥으로) =================
            new V16Node { id = "sole",     name = "밑창",     title = "신발 밑창 보강",           desc = "이동 속도를 조금 올린다",       branch = 2, pos = new Vector2(-96, 194),  maxLevel = 5, baseCost = 10,  growth = 1.55f, prereq = "speed1", prereqLevel = 1, effect = V16Effect.MoveSpeed, perLevel = 0.010f },
            new V16Node { id = "lace",     name = "끈",       title = "단단히 묶은 끈",           desc = "이동 속도를 더 올린다",         branch = 2, pos = new Vector2(-190, 194), maxLevel = 4, baseCost = 120,  growth = 1.80f, prereq = "speed1", prereqLevel = 2, effect = V16Effect.MoveSpeed, perLevel = 0.012f },
            new V16Node { id = "filter",   name = "필터",     title = "방독면 필터 교체",         desc = "악취가 차오르는 속도를 늦춘다", branch = 2, pos = new Vector2(-96, 254),  maxLevel = 5, baseCost = 11,  growth = 1.58f, prereq = "mask1",  prereqLevel = 1, effect = V16Effect.MaskFilter, perLevel = 0.02f },
            new V16Node { id = "oxygen",   name = "산소탱크", title = "산소 탱크 증설",           desc = "산소 부스트(저악취 가속)가 강해진다", branch = 2, pos = new Vector2(-190, 254), maxLevel = 4, baseCost = 308, growth = 1.82f, prereq = "mask1",  prereqLevel = 2, milestone = true, effect = V16Effect.OxygenTank, perLevel = 0.025f },
            new V16Node { id = "knees",    name = "무릎대",   title = "무릎 보호대",              desc = "걸레질 속도를 올린다",          branch = 2, pos = new Vector2(-284, 194), maxLevel = 4, baseCost = 336, growth = 1.84f, prereq = "speed2", prereqLevel = 1, effect = V16Effect.ScrubPower, perLevel = 0.03f },
            new V16Node { id = "soap",     name = "세제",     title = "강력 세제 리필",           desc = "걸레질 속도를 더 올린다",       branch = 2, pos = new Vector2(-378, 254), maxLevel = 3, baseCost = 896, growth = 1.90f, prereq = "scrub",  prereqLevel = 1, effect = V16Effect.ScrubPower, perLevel = 0.04f },
            new V16Node { id = "vent",     name = "환기",     title = "환기구 정비",              desc = "피버가 천천히 식어 유지된다",   branch = 2, pos = new Vector2(-284, 254), maxLevel = 4, baseCost = 480, growth = 1.84f, prereq = "filter", prereqLevel = 3, effect = V16Effect.FeverHold, perLevel = 0.05f },
            new V16Node { id = "calm",     name = "침착",     title = "침착한 호흡",              desc = "피버 유지력을 더 올린다",       branch = 2, pos = new Vector2(-378, 194), maxLevel = 3, baseCost = 992, growth = 1.92f, prereq = "vent",   prereqLevel = 2, effect = V16Effect.FeverHold, perLevel = 0.06f },

            // ================= 가지 3: 파괴 · 로봇 자동화 (우하, 바깥으로) =================
            new V16Node { id = "knuckle",  name = "너클",     title = "단단한 너클",              desc = "큰 봉투 타격력이 오른다",       branch = 3, pos = new Vector2(96, 254),  maxLevel = 5, baseCost = 11,  growth = 1.58f, prereq = "fist1",  prereqLevel = 1, effect = V16Effect.FistPower, perLevel = 0.03f },
            new V16Node { id = "brush",    name = "솔",       title = "로봇 청소 솔 교체",        desc = "로봇 수거 주기가 조금 짧아진다(보조 한계 내)", branch = 3, pos = new Vector2(190, 254), maxLevel = 4, baseCost = 480, growth = 1.84f, prereq = "rspd1", prereqLevel = 1, effect = V16Effect.RoombaPickup, perLevel = 0.015f },
            new V16Node { id = "route",    name = "경로",     title = "효율 경로 학습",           desc = "로봇 이동이 조금 빨라진다",     branch = 3, pos = new Vector2(284, 254), maxLevel = 3, baseCost = 832, growth = 1.90f, prereq = "rspd2", prereqLevel = 1, effect = V16Effect.RoombaSpeed, perLevel = 0.015f },
            new V16Node { id = "hammer",   name = "망치손",   title = "망치 같은 손바닥",         desc = "봉투 타격력이 크게 오른다",     branch = 3, pos = new Vector2(190, 194), maxLevel = 3, baseCost = 896, growth = 1.90f, prereq = "fist2",  prereqLevel = 1, milestone = true, effect = V16Effect.FistPower, perLevel = 0.04f },
            new V16Node { id = "sensor",   name = "센서",     title = "먼지 센서",                desc = "로봇이 쓰레기를 조금 더 자주 문다", branch = 3, pos = new Vector2(378, 194), maxLevel = 3, baseCost = 576, growth = 1.86f, prereq = "roomba", prereqLevel = 2, effect = V16Effect.RoombaPickup, perLevel = 0.01f },
            new V16Node { id = "dock",     name = "도킹",     title = "자동 도킹 충전",           desc = "로봇 이동이 더 빨라진다",       branch = 3, pos = new Vector2(378, 254), maxLevel = 3, baseCost = 1805, growth = 1.92f, prereq = "sensor", prereqLevel = 2, effect = V16Effect.RoombaSpeed, perLevel = 0.02f },
            new V16Node { id = "aura",     name = "후각",     title = "후각 훈련 (피버 시동)",    desc = "피버 게이지가 더 빨리 찬다",    branch = 3, pos = new Vector2(566, 254), maxLevel = 4, baseCost = 1520, growth = 1.86f, prereq = "knuckle", prereqLevel = 3, effect = V16Effect.FeverGain, perLevel = 0.05f },
            new V16Node { id = "overdrive", name = "과부하",  title = "자동화 과부하",            desc = "정산 금액이 조금 더 붙는다",    branch = 3, pos = new Vector2(566, 194), maxLevel = 3, baseCost = 2660, growth = 1.95f, prereq = "dock",    prereqLevel = 2, effect = V16Effect.SettleBonus, perLevel = 0.015f },
        };

        public static V16Node Get(string id)
        {
            for (int i = 0; i < Nodes.Length; i++)
                if (Nodes[i].id == id) return Nodes[i];
            return null;
        }

        public static bool IsV16Node(string id) => Get(id) != null;

        public static int Cost(V16Node node, int level)
        {
            if (node == null) return 999999;
            return Mathf.RoundToInt(node.baseCost * Mathf.Pow(node.growth, Mathf.Clamp(level, 0, node.maxLevel - 1)));
        }
    }
}
