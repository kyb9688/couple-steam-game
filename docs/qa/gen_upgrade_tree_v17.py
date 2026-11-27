#!/usr/bin/env python3
"""tree_v17.py 데이터로 Unity C# 강화 트리(UpgradeTreeV17.cs)를 생성한다."""
import os
import tree_v17 as T

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "TrashCleaner3D",
                   "Assets", "_Project", "Scripts", "UpgradeTreeV17.cs")

FMT = {
    "pct": ('return $"{{label}} +{{v * 100f:0.#}}%";', "pct"),
    "num": ('return $"{{label}} +{{v:0}}개";', "num"),
    "sec": ('return $"{{label}} +{{v:0.0}}초";', "sec"),
    "won": ('return $"{{label}} +${{v:0}}";', "won"),
    "dec": ('return $"{{label}} +{{v:0.00}}m";', "dec"),
    "flag": ('return level > 0 ? $"{{label}} 해금" : $"{{label}} 잠김";', "flag"),
}

LABEL = {
    "hold": "흡입 속도", "move": "이동 속도", "radius": "흡입 반경", "grab": "동시 흡입", "crit": "치명타 확률",
    "chain": "연쇄 흡입 확률", "bincap": "수거통 용량", "price": "정산 단가", "combowindow": "콤보 유지",
    "settle": "정산 금액", "fist": "봉투 타격력", "fistcrit": "봉투 치명타 확률",
    "fistcritmul": "봉투 치명타 배율", "burstfever": "봉투 피버 충전", "burstcash": "봉투 현금 보너스",
    "chainunlock": "연쇄 흡입", "chainradius": "연쇄 흡입 반경", "chaincount": "연쇄 흡입 개수",
    "scrub": "행주질 속도", "scrubrange": "행주질 범위", "stainreward": "얼룩 정산",
    "scrubfever": "행주질 피버 충전", "maskf": "악취 상승 둔화", "stenchslow": "악취 둔화 저항",
    "blur": "악취 시야 흐림 완화", "oxygenunlock": "산소 부스트", "oxygenpower": "산소 부스트 흡입",
    "oxygenmove": "산소 부스트 이속", "oxygenhold": "산소 피버 유지", "fevergain": "피버 게이지 획득",
    "feverhold": "피버 유지", "fevermax": "피버 배율", "feverprice": "피버 정산",
    "roombaspeed": "로봇 이동 속도", "roombapickup": "로봇 수거 주기", "roombadetect": "로봇 탐지 반경",
    "roombabattery": "로봇 배터리 가동", "bulk": "차량 정산 배수", "vehmult": "차량 거래 배수",
    "bulkthreshold": "대량 정산 기준", "bulksettle": "대량 정산 보너스", "remotecooldown": "원격 수거 쿨다운",
}


def cs_effect_texts():
    out = []
    for eff in T.EFFECT_ORDER:
        enum_name = T.EFFECTS[eff][0]
        kind = T.EFFECTS[eff][1]
        label = LABEL[eff]
        if kind == "pct":
            out.append(f'                case V17Effect.{enum_name}: return $"{label} +{{v * 100f:0.#}}%";')
        elif kind == "num":
            out.append(f'                case V17Effect.{enum_name}: return $"{label} +{{v:0}}";')
        elif kind == "sec":
            out.append(f'                case V17Effect.{enum_name}: return $"{label} +{{v:0.0}}초";')
        elif kind == "won":
            out.append(f'                case V17Effect.{enum_name}: return $"{label} +${{v:0}}";')
        elif kind == "dec":
            out.append(f'                case V17Effect.{enum_name}: return $"{label} +{{v:0.00}}m";')
        elif kind == "flag":
            out.append(f'                case V17Effect.{enum_name}: return level > 0 ? "{label} 해금" : "{label} 잠김";')
    return "\n".join(out)


def main():
    enums = ",\n        ".join(
        f"{T.EFFECTS[e][0]} = {i}" for i, e in enumerate(T.EFFECT_ORDER))
    enums += f",\n        Count = {len(T.EFFECT_ORDER)}"

    rows = []
    for n in T.NODES:
        rows.append(
            '            new V17Node {{ id = "{id}", name = "{name}", title = "{title}", desc = "{desc}", '
            'branch = {branch}, lane = {lane}, tier = {tier}, pos = new Vector2({x}f, {y}f), '
            'maxLevel = {max_level}, baseCost = {base_cost}, growth = {growth}f, '
            'prereq = "{prereq}", prereqLevel = {prereq_level}, milestone = {milestone}, '
            'effect = V17Effect.{effect}, perLevel = {per_level}f }},'.format(
                id=n["id"], name=n["name"], title=n["title"], desc=n["desc"], branch=n["branch"],
                lane=n["lane"], tier=n["tier"], x=n["pos"][0], y=n["pos"][1],
                max_level=n["max_level"], base_cost=n["base_cost"], growth=n["growth"],
                prereq=n["prereq"], prereq_level=n["prereq_level"],
                milestone="true" if n["milestone"] else "false",
                effect=T.effect_enum(n["effect"]), per_level=n["per_level"]))

    branch_meta = "\n".join(
        '            new BranchDef { id = "%s", label = "%s", color = new Color(%.3ff, %.3ff, %.3ff), angle = %d },'
        % (b[0], b[1], b[2][0], b[2][1], b[2][2], T.BRANCH_ANGLES[b[0]])
        for b in T.BRANCHES)

    body = f'''using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{{
    /// <summary>
    /// v1.7 강화 효과 41종. 모든 부가 효과는 '기본 0/비활성'이고, 이 트리의 노드로만 켜지고 올라간다.
    /// 1회 강화당 상승폭은 1~3% 단위(피버·연쇄 같은 해금형은 1레벨)로 제한해 4시간 캠페인 동안 완만히 성장한다.
    /// </summary>
    public enum V17Effect
    {{
        {enums}
    }}

    /// <summary>v1.7 세분화 노드 정의(효과 1종 × 작은 상승폭).</summary>
    public class V17Node
    {{
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
        public V17Effect effect;
        public float perLevel;

        public string EffectText(int level)
        {{
            float v = perLevel * Mathf.Max(1, level);
            switch (effect)
            {{
{cs_effect_texts()}
                default: return "";
            }}
        }}
    }}

    public struct BranchDef
    {{
        public string id;
        public string label;
        public Color color;
        public int angle;        // 화면 기준 각도(도). y가 아래로 +
    }}

    /// <summary>
    /// v1.7 강화 트리: 8계열 × 4가지 × 6단계 = 192개 마이크로 노드.
    /// 기존 코어 28노드(도구 3티어·차량 3단계·대걸레·로봇 등)와 합쳐 220노드.
    /// 데이터는 docs/qa/tree_v17.py 에서 생성된다(수치 단일 출처).
    /// </summary>
    public static class UpgradeTreeV17
    {{
        public const int MicroNodeCount = {len(T.NODES)};
        public const int CoreNodeCount = 28;
        public const int TotalNodeCount = MicroNodeCount + CoreNodeCount;
        public const int BranchCount = {len(T.BRANCHES)};

        public static readonly BranchDef[] Branches =
        {{
{branch_meta}
        }};

        public static readonly V17Node[] Nodes =
        {{
{chr(10).join(rows)}
        }};

        private static Dictionary<string, V17Node> _index;

        private static void BuildIndex()
        {{
            _index = new Dictionary<string, V17Node>(Nodes.Length);
            for (int i = 0; i < Nodes.Length; i++) _index[Nodes[i].id] = Nodes[i];
        }}

        public static V17Node Get(string id)
        {{
            if (_index == null) BuildIndex();
            V17Node n;
            return (id != null && _index.TryGetValue(id, out n)) ? n : null;
        }}

        public static bool IsV17Node(string id) => Get(id) != null;

        public static int Cost(V17Node node, int level)
        {{
            if (node == null) return 999999;
            return Mathf.RoundToInt(node.baseCost * Mathf.Pow(node.growth, Mathf.Clamp(level, 0, node.maxLevel - 1)));
        }}

        /// <summary>신규 192노드를 전부 만렙까지 올리는 총비용(밸런스 QA·HUD 표시용).</summary>
        public static int TotalCostAllLevels()
        {{
            int sum = 0;
            for (int i = 0; i < Nodes.Length; i++)
                for (int lv = 0; lv < Nodes[i].maxLevel; lv++)
                    sum += Cost(Nodes[i], lv);
            return sum;
        }}
    }}
}}
'''
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(body)
    print(f"wrote {os.path.normpath(OUT)} ({len(body)} bytes, {len(T.NODES)} nodes)")


if __name__ == "__main__":
    main()
