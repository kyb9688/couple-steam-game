#!/usr/bin/env python3
"""
TrashCleaner3D v1.8 강화 트리 데이터 (단일 출처).

v1.7의 8계열(흡입·손 / 수거·정산 / 큰 봉투·타격 / 얼룩·행주질 / 방독면·산소 / 피버·콤보 /
로봇청소기 / 차량·운송) × 4레인 × 6단계 = 192노드를 그대로 두고, v1.8 장비·쓰레기 축을 더한다.

- gear(장비·압축) 4레인 × 6 = 24노드: 원기옥 압축 빔포 해금·성장·정산, 미니 블랙홀(1→3개),
  화이트홀 자동 정산. 코어 '진공청소기' 뒤에 이어지는 3·4단계 장비.
- trash(쓰레기 강화) 4레인 × 6 = 24노드: 캔·병/전자폐기물/대형 폐가전/금괴 같은
  '내구도 있는 고가치 폐기물' 해금·비중, 내구도·파쇄력, 고가치 정산, 물량·밀도(웨이브).

총 마이크로 240 + 코어 28 = 268노드.

gen_upgrade_tree_v18.py 가 이 데이터로 Assets/_Project/Scripts/UpgradeTreeV18.cs 를 생성하고,
balance_sim_v18.py 가 같은 데이터를 그대로 읽어 시뮬레이션한다(수치 불일치 방지).
"""

import math

# 효과 이름 -> (C# enum 이름, 표시 형식, 단위 배율)
# fmt: pct = 100*x%, num = 정수, dec = 소수 2자리, sec = 초
EFFECTS = {
    "hold":       ("HoldSpeed",          "pct", "흡입 속도"),
    "move":       ("MoveSpeed",          "pct", "이동 속도"),
    "radius":     ("Radius",             "pct", "흡입 반경"),
    "grab":       ("GrabCount",          "num", "동시 흡입"),
    "crit":       ("CritChance",         "pct", "치명타 확률"),
    "chain":      ("ChainChance",        "pct", "연쇄 흡입 확률"),
    "bincap":     ("BinCap",             "pct", "수거통 용량"),
    "price":      ("Price",              "pct", "정산 단가"),
    "combowindow":("ComboWindow",        "sec", "콤보 유지"),
    "settle":     ("SettleBonus",        "pct", "정산 금액"),
    "fist":       ("FistPower",          "pct", "봉투 타격력"),
    "fistcrit":   ("FistCritChance",     "pct", "봉투 치명타 확률"),
    "fistcritmul":("FistCritMult",       "pct", "봉투 치명타 배율"),
    "burstfever": ("BurstFever",         "pct", "봉투 피버 충전"),
    "burstcash":  ("BurstCash",          "won", "봉투 현금 보너스"),
    "chainunlock":("ChainUnlock",        "flag", "연쇄 흡입 해금"),
    "chainradius":("ChainRadius",        "dec", "연쇄 흡입 반경"),
    "chaincount": ("ChainCount",         "num", "연쇄 흡입 개수"),
    "scrub":      ("ScrubSpeed",         "pct", "행주질 속도"),
    "scrubrange": ("ScrubRange",         "pct", "행주질 범위"),
    "stainreward":("StainReward",        "pct", "얼룩 정산"),
    "scrubfever": ("ScrubFever",         "pct", "행주질 피버 충전"),
    "maskf":      ("MaskFilter",         "pct", "악취 상승 둔화"),
    "stenchslow": ("StenchSlowResist",   "pct", "악취 둔화 저항"),
    "blur":       ("BlurReduce",         "pct", "악취 시야 흐림 완화"),
    "oxygenunlock":("OxygenUnlock",      "flag", "산소 부스트 해금"),
    "oxygenpower":("OxygenPower",        "pct", "산소 부스트 흡입"),
    "oxygenmove": ("OxygenMove",         "pct", "산소 부스트 이속"),
    "oxygenhold": ("OxygenFeverHold",    "pct", "산소 피버 유지"),
    "fevergain":  ("FeverGain",          "pct", "피버 게이지 획득"),
    "feverhold":  ("FeverHold",          "pct", "피버 유지"),
    "fevermax":   ("FeverMax",           "pct", "피버 배율"),
    "feverprice": ("FeverPrice",         "pct", "피버 정산"),
    "roombaspeed":("RoombaSpeed",        "pct", "로봇 이동 속도"),
    "roombapickup":("RoombaPickup",      "pct", "로봇 수거 주기"),
    "roombadetect":("RoombaDetect",      "pct", "로봇 탐지 반경"),
    "roombabattery":("RoombaBattery",    "pct", "로봇 배터리 가동"),
    "bulk":       ("VehicleBulk",        "pct", "차량 정산 배수"),
    "vehmult":    ("VehicleMult",        "pct", "차량 거래 배수"),
    "bulkthreshold":("BulkThreshold",    "pct", "대량 정산 기준"),
    "bulksettle": ("BulkSettle",         "pct", "대량 정산 보너스"),
    "remotecooldown":("RemoteCooldown",  "pct", "원격 수거 쿨다운"),

    # ===== v1.8 장비·압축(원기옥·블랙홀·화이트홀) =====
    "twograb":    ("TwoHand",             "flag", "양손 줍기"),
    "orbunlock":  ("OrbUnlock",           "flag", "원기옥 해금"),
    "orbpull":    ("OrbPull",             "pct", "원기옥 끌어당김"),
    "orbradius":  ("OrbRadius",           "pct", "원기옥 반경"),
    "orbcharge":  ("OrbCharge",           "pct", "구슬 성장"),
    "orbsize":    ("OrbSize",             "pct", "구슬 크기"),
    "orbcash":    ("OrbCash",             "pct", "원기옥 정산"),
    "orbburst":   ("OrbBurst",            "pct", "터짐 범위"),
    "orbfly":     ("OrbFly",              "pct", "발사 속도"),
    "orbauto":    ("OrbAuto",             "flag", "즉시 발사"),
    "bhunlock":   ("BhUnlock",            "flag", "블랙홀 해금"),
    "bhradius":   ("BhRadius",            "pct", "블랙홀 반경"),
    "bhcount":    ("BhCount",             "num", "블랙홀 개수"),
    "bhspin":     ("BhSpin",              "pct", "블랙홀 회전"),
    "bhduration": ("BhDuration",          "pct", "블랙홀 지속"),
    "whunlock":   ("WhUnlock",            "flag", "화이트홀 정산"),
    "whcash":     ("WhCash",              "pct", "화이트홀 정산"),
    "bhpull":     ("BhPull",              "pct", "블랙홀 흡입력"),
    "whauto":     ("WhAuto",              "flag", "완전 자동 정산"),

    # ===== v1.8 쓰레기 강화(고가치·내구도·물량) =====
    "trashcan":   ("TrashCan",            "flag", "캔·병 해금"),
    "trashewaste":("TrashEwaste",         "flag", "전자폐기물 해금"),
    "trashbig":   ("TrashBig",            "flag", "대형 폐가전 해금"),
    "trashgold":  ("TrashGold",           "flag", "금괴 해금"),
    "trashratio": ("TrashRatio",          "pct", "고가치 비중"),
    "trashdur":   ("TrashDurability",     "pct", "내구도"),
    "heavydmg":   ("HeavyDamage",         "pct", "파쇄력"),
    "heavyvalue": ("HeavyValue",          "pct", "고가치 단가"),
    "sortbonus":  ("SortBonus",           "pct", "분리 보너스"),
    "rarity":     ("RarityBonus",         "pct", "등급 보정"),
    "heavyjack":  ("HeavyJackpot",        "pct", "금괴 잭팟"),
    "density":    ("TrashDensity",        "pct", "쓰레기 밀도"),
    "cluster":    ("TrashCluster",        "pct", "더미 밀집"),
    "wavebonus":  ("WaveBonus",           "num", "웨이브 추가"),
    "megadump":   ("MegaDump",            "flag", "대량 투하"),
}

# 계열: (id, 이름, 색, 코어 진입 노드들, 레인별 기본가)
BRANCHES = [
    ("suction", "흡입·손",       (0.32, 0.82, 0.92), ["hold1", "rad1", "hold1", "rad1"], 200),
    ("bin",     "수거·정산",     (0.36, 0.90, 0.54), ["bin1", "price1", "price1", "bin1"], 200),
    ("bag",     "큰 봉투·타격",  (0.98, 0.74, 0.24), ["fist1", "fist1", "fist2", "fist2"], 220),
    ("stain",   "얼룩·행주질",   (0.34, 0.62, 0.96), ["mop", "mop", "scrub", "scrub"], 220),
    ("oxygen",  "방독면·산소",   (0.40, 0.86, 0.66), ["mask1", "mask1", "mask2", "speed1"], 280),
    ("fever",   "피버·콤보",     (0.96, 0.46, 0.28), ["fever", "hold2", "fever", "fever"], 300),
    ("roomba",  "로봇청소기",    (0.70, 0.52, 0.94), ["roomba", "roomba", "roomba", "rspd1"], 340),
    ("vehicle", "차량·운송",     (0.66, 0.72, 0.80), ["cart", "damas", "truck1t", "damas"], 360),
    ("gear",    "장비·압축",     (0.22, 0.92, 0.86), ["vacuum", "vacuum", "vacuum", "vacuum"], 560),
    ("trash",   "쓰레기 강화",   (0.76, 0.58, 0.26), ["fist1", "fist1", "price1", "bin1"], 500),
]

# 레인: (계열, 레인 이름, 설명, [(효과, perLevel, maxLevel, 이름, 제목꼬리), ...6개], 코어 선행 Lv)
LANES = [
    # ================= B0 흡입·손 =================
    ("suction", "손아귀", "빨아들이는 힘이 붙어 홀드 시간이 짧아진다", [
        ("hold", 0.016, 3, "흡입력", "기본 흡입"),
        ("hold", 0.016, 3, "손아귀", "쥐는 힘"),
        ("hold", 0.018, 3, "호흡 조절", "끊김 없는 흡입"),
        ("hold", 0.018, 3, "연속 빨기", "연속 흡입"),
        ("hold", 0.020, 3, "압축 흡입", "고압 흡입"),
        ("hold", 0.022, 3, "완전 진공", "진공 완성"),
    ], 1),
    ("suction", "흡입 반경", "한 번에 빨아들이는 원이 넓어진다", [
        ("radius", 0.018, 3, "넓은 손", "손바닥 확장"),
        ("radius", 0.018, 3, "노즐 벌리기", "노즐 개량"),
        ("radius", 0.020, 3, "쓸기 궤적", "궤적 확장"),
        ("radius", 0.020, 3, "광역 빨기", "광역 흡입"),
        ("radius", 0.022, 3, "소용돌이 날개", "회오리 확장"),
        ("radius", 0.025, 3, "흡입 돔", "돔형 흡입"),
    ], 1),
    ("suction", "멀티 락온", "동시에 락온하는 쓰레기 개수가 늘어난다", [
        ("twograb", 1.0, 1, "양손 줍기", "양손 모션"),
        ("grab", 1.0, 1, "멀티 락온", "다중 락온"),
        ("grab", 1.0, 1, "손가락 추가", "집게 손가락"),
        ("grab", 1.0, 1, "동시 락온", "동시 포집"),
        ("grab", 1.0, 1, "여럿 움켜쥐기", "다발 포집"),
        ("grab", 1.0, 1, "무리째 락온", "무리 포집"),
    ], 2),
    ("suction", "예민한 감각", "약점을 노려 치명타·연쇄가 터지고, 발놀림도 가벼워진다", [
        ("crit", 0.015, 3, "날카로운 눈", "약점 감지"),
        ("crit", 0.015, 3, "약점 포착", "약점 조준"),
        ("crit", 0.020, 3, "급소 감각", "급소 포착"),
        ("chain", 0.020, 3, "연쇄 촉", "연쇄 감각"),
        ("chain", 0.025, 3, "예민한 촉", "연쇄 확률"),
        ("move", 0.020, 3, "가벼운 발놀림", "발놀림"),
    ], 2),

    # ================= B1 수거·정산 =================
    ("bin", "수거통", "한 번에 담아둘 수 있는 쓰레기 양이 늘어난다", [
        ("bincap", 0.020, 3, "튼튼한 봉투", "수거 봉투"),
        ("bincap", 0.020, 3, "수거통 확장", "용량 확장"),
        ("bincap", 0.022, 3, "이중 봉투", "이중 적재"),
        ("bincap", 0.022, 3, "압축 적재", "압축 적재"),
        ("bincap", 0.025, 3, "대형 수거통", "대형 용량"),
        ("bincap", 0.028, 3, "무한 적재", "적재 완성"),
    ], 1),
    ("bin", "정산 단가", "쓰레기 한 개의 정산 단가가 오른다", [
        ("price", 0.015, 3, "저울 보정", "저울 손질"),
        ("price", 0.015, 3, "분리수거", "분리 정리"),
        ("price", 0.018, 3, "등급 분류", "등급 구분"),
        ("price", 0.018, 3, "고가 매입", "고가 거래"),
        ("price", 0.020, 3, "재활용 거래", "재활용 계약"),
        ("price", 0.022, 3, "프리미엄 정산", "프리미엄"),
    ], 1),
    ("bin", "정산 콤보", "연속 정산 콤보를 오래 유지한다", [
        ("combowindow", 0.4, 3, "정산 콤보", "콤보 시작"),
        ("combowindow", 0.4, 3, "연속 납품", "연속 납품"),
        ("combowindow", 0.5, 3, "단골 손님", "단골 거래"),
        ("combowindow", 0.5, 3, "빠른 정산", "정산 속도"),
        ("combowindow", 0.6, 3, "콤보 유지", "콤보 연장"),
        ("combowindow", 0.7, 3, "정산 리듬", "리듬 완성"),
    ], 2),
    ("bin", "정산 보너스", "정산할 때 금액이 조금 더 붙는다", [
        ("settle", 0.012, 3, "영수증", "영수증 정리"),
        ("settle", 0.012, 3, "꼼꼼한 계산", "계산 정확"),
        ("settle", 0.014, 3, "현금 영수증", "현금 정산"),
        ("settle", 0.014, 3, "세금 공제", "공제 적용"),
        ("settle", 0.016, 3, "추가 수당", "수당 협상"),
        ("settle", 0.018, 3, "정산 보너스", "보너스 정산"),
    ], 2),

    # ================= B2 큰 봉투·타격 =================
    ("bag", "주먹", "큰 봉투를 때릴 때 데미지가 오른다", [
        ("fist", 0.018, 3, "주먹 단련", "기본 타격"),
        ("fist", 0.018, 3, "두꺼운 장갑", "장갑 안감"),
        ("fist", 0.020, 3, "너클", "너클 강화"),
        ("fist", 0.020, 3, "망치 손바닥", "망치 타격"),
        ("fist", 0.022, 3, "철권", "철권 타격"),
        ("fist", 0.025, 3, "분쇄 주먹", "분쇄 타격"),
    ], 1),
    ("bag", "급소", "봉투 약점을 노려 치명타가 터진다", [
        ("fistcrit", 0.015, 3, "급소 보기", "약점 조준"),
        ("fistcrit", 0.015, 3, "약한 봉투 찾기", "약점 탐색"),
        ("fistcrit", 0.018, 3, "치명타 감각", "치명 감각"),
        ("fistcritmul", 0.08, 3, "찢는 손톱", "치명 배율"),
        ("fistcritmul", 0.08, 3, "파열점", "파열 강화"),
        ("fistcritmul", 0.10, 3, "일격", "일격 필살"),
    ], 2),
    ("bag", "터짐", "봉투가 터질 때 피버와 현금이 터진다", [
        ("burstfever", 0.08, 1, "터지는 맛", "피버 충전 해금"),
        ("burstcash", 6.0, 3, "파열 충전", "현금 보너스 해금"),
        ("burstfever", 0.03, 2, "봉투 피버", "피버 충전 증가"),
        ("burstcash", 10.0, 3, "현금 폭발", "현금 보너스 증가"),
        ("burstfever", 0.03, 2, "터짐 보너스", "피버 충전 강화"),
        ("burstcash", 14.0, 3, "대박 터짐", "현금 보너스 강화"),
    ], 1),
    ("bag", "연쇄 흡입", "봉투가 터지면 주변 쓰레기가 빨려 들어온다", [
        ("chainunlock", 1.0, 1, "미니 블랙홀", "연쇄 흡입 해금"),
        ("chainradius", 0.25, 3, "연쇄 흡입", "연쇄 반경"),
        ("chaincount", 2.0, 3, "빨아들이는 구멍", "연쇄 개수"),
        ("chainradius", 0.25, 3, "소용돌이 반경", "반경 확장"),
        ("chaincount", 2.0, 3, "연쇄 개수", "개수 확장"),
        ("chainradius", 0.30, 3, "블랙홀 완성", "블랙홀 완성"),
    ], 2),

    # ================= B3 얼룩·행주질 =================
    ("stain", "행주질 속도", "바닥 얼룩이 더 빨리 지워진다", [
        ("scrub", 0.020, 3, "팔 힘", "기본 행주질"),
        ("scrub", 0.020, 3, "행주질", "행주 손놀림"),
        ("scrub", 0.022, 3, "강력 세제", "세제 강화"),
        ("scrub", 0.022, 3, "빠른 손놀림", "속도 강화"),
        ("scrub", 0.025, 3, "광택 마무리", "마무리 광택"),
        ("scrub", 0.028, 3, "완벽한 행주", "완벽 행주질"),
    ], 1),
    ("stain", "행주질 범위", "한 번에 닦이는 바닥 범위가 넓어진다", [
        ("scrubrange", 0.020, 3, "큰 걸레", "걸레 확장"),
        ("scrubrange", 0.020, 3, "팔 길이", "사거리 확장"),
        ("scrubrange", 0.022, 3, "넓은 행주", "범위 확장"),
        ("scrubrange", 0.022, 3, "바닥 쓸기", "쓸기 확장"),
        ("scrubrange", 0.025, 3, "사각지대 제거", "사각 제거"),
        ("scrubrange", 0.028, 3, "바닥 전체", "전체 범위"),
    ], 1),
    ("stain", "얼룩 정산", "얼룩을 지울 때 받는 금액이 오른다", [
        ("stainreward", 0.020, 3, "얼룩 값", "얼룩 시세"),
        ("stainreward", 0.020, 3, "찌든 때 시세", "찌든 때 값"),
        ("stainreward", 0.022, 3, "곰팡이 제거", "곰팡이 값"),
        ("stainreward", 0.022, 3, "왁스 코팅", "코팅 시세"),
        ("stainreward", 0.025, 3, "광택 보너스", "광택 값"),
        ("stainreward", 0.028, 3, "바닥 복원", "복원 시세"),
    ], 1),
    ("stain", "행주질 피버", "얼룩을 닦을수록 피버가 찬다", [
        ("scrubfever", 0.015, 3, "행주질 흥", "피버 시작"),
        ("scrubfever", 0.015, 3, "리듬 타기", "리듬 충전"),
        ("scrubfever", 0.018, 3, "손맛 피버", "손맛 충전"),
        ("scrubfever", 0.018, 3, "걸레 피버", "걸레 충전"),
        ("scrubfever", 0.020, 3, "열심 피버", "열심 충전"),
        ("scrubfever", 0.022, 3, "광택 피버", "광택 충전"),
    ], 1),

    # ================= B4 방독면·산소 =================
    ("oxygen", "필터", "악취가 차오르는 속도가 느려진다", [
        ("maskf", 0.015, 3, "필터 교체", "기본 필터"),
        ("maskf", 0.015, 3, "활성탄 필터", "활성탄"),
        ("maskf", 0.018, 3, "이중 필터", "이중 여과"),
        ("maskf", 0.018, 3, "방독면 밀폐", "밀폐 강화"),
        ("maskf", 0.020, 3, "정화 카트리지", "정화 강화"),
        ("maskf", 0.022, 3, "완벽 밀폐", "완벽 밀폐"),
    ], 1),
    ("oxygen", "악취 둔화 저항", "악취가 심해도 이동·작업 속도가 덜 떨어진다", [
        ("stenchslow", 0.020, 3, "코 막기", "악취 저항"),
        ("stenchslow", 0.020, 3, "둔감한 후각", "후각 둔화"),
        ("stenchslow", 0.022, 3, "악취 적응", "적응 훈련"),
        ("stenchslow", 0.022, 3, "숨 참기", "숨 참기"),
        ("stenchslow", 0.025, 3, "강한 정신력", "정신력"),
        ("stenchslow", 0.028, 3, "악취 면역", "면역 완성"),
    ], 1),
    ("oxygen", "시야 확보", "악취로 화면이 흐려지는 정도가 줄어든다", [
        ("blur", 0.020, 3, "시야 확보", "시야 유지"),
        ("blur", 0.020, 3, "김 서림 방지", "김 방지"),
        ("blur", 0.022, 3, "렌즈 코팅", "렌즈 코팅"),
        ("blur", 0.022, 3, "선명한 시야", "선명 시야"),
        ("blur", 0.025, 3, "안개 걷기", "안개 제거"),
        ("blur", 0.028, 3, "투명 렌즈", "투명 렌즈"),
    ], 2),
    ("oxygen", "산소 부스트", "깨끗한 공기에서 이동·흡입이 빨라지고 피버가 유지된다", [
        ("oxygenunlock", 1.0, 1, "산소 부스트", "부스트 해금"),
        ("oxygenpower", 0.03, 3, "산소 탱크", "흡입 가속"),
        ("oxygenmove", 0.02, 3, "가속 호흡", "이동 가속"),
        ("oxygenhold", 0.04, 3, "산소 유지", "피버 유지"),
        ("oxygenpower", 0.04, 3, "산소 과급", "흡입 과급"),
        ("oxygenmove", 0.03, 3, "산소 질주", "이동 질주"),
    ], 2),

    # ================= B5 피버·콤보 =================
    ("fever", "피버 게이지", "흡입할 때 피버 게이지가 더 빨리 찬다", [
        ("fevergain", 0.020, 3, "피버 점화", "점화"),
        ("fevergain", 0.020, 3, "흡입 열기", "열기"),
        ("fevergain", 0.022, 3, "연속 게이지", "게이지"),
        ("fevergain", 0.022, 3, "불씨", "불씨"),
        ("fevergain", 0.025, 3, "가속 게이지", "가속"),
        ("fevergain", 0.028, 3, "과열", "과열"),
    ], 1),
    ("fever", "피버 유지", "피버가 천천히 식어 더 오래 간다", [
        ("feverhold", 0.020, 3, "피버 유지", "유지"),
        ("feverhold", 0.020, 3, "열 보존", "보존"),
        ("feverhold", 0.022, 3, "식지 않는 열", "지속"),
        ("feverhold", 0.022, 3, "잔불", "잔불"),
        ("feverhold", 0.025, 3, "온기 유지", "온기"),
        ("feverhold", 0.028, 3, "불꽃 유지", "불꽃"),
    ], 2),
    ("fever", "피버 배율", "피버 중 범위·속도 상승폭이 커진다", [
        ("fevermax", 0.020, 3, "피버 증폭", "증폭"),
        ("fevermax", 0.020, 3, "불꽃 세기", "불꽃"),
        ("fevermax", 0.022, 3, "열폭발", "열폭발"),
        ("fevermax", 0.022, 3, "광란", "광란"),
        ("fevermax", 0.025, 3, "맥시멈 피버", "맥시멈"),
        ("fevermax", 0.028, 3, "불꽃 폭주", "폭주"),
    ], 2),
    ("fever", "피버 정산", "피버 중 정산 단가가 더 오른다", [
        ("feverprice", 0.010, 3, "피버 수익", "수익"),
        ("feverprice", 0.010, 3, "흥정 실력", "흥정"),
        ("feverprice", 0.012, 3, "바쁜 손", "바쁜 손"),
        ("feverprice", 0.012, 3, "열기 프리미엄", "프리미엄"),
        ("feverprice", 0.015, 3, "광란 정산", "광란 정산"),
        ("feverprice", 0.018, 3, "피버 잭팟", "잭팟"),
    ], 2),

    # ================= B6 로봇청소기 =================
    ("roomba", "로봇 주행", "로봇청소기가 더 빨리 움직인다", [
        ("roombaspeed", 0.020, 3, "로봇 모터", "모터"),
        ("roombaspeed", 0.020, 3, "바퀴 정비", "바퀴"),
        ("roombaspeed", 0.022, 3, "고속 주행", "주행"),
        ("roombaspeed", 0.022, 3, "터보 모터", "터보"),
        ("roombaspeed", 0.025, 3, "경량 바퀴", "경량"),
        ("roombaspeed", 0.028, 3, "최고 속도", "최고 속도"),
    ], 1),
    ("roomba", "로봇 수거", "로봇이 쓰레기를 더 자주 문다(항상 보조)", [
        ("roombapickup", 0.010, 3, "흡입 솔", "솔"),
        ("roombapickup", 0.010, 3, "수거 브러시", "브러시"),
        ("roombapickup", 0.012, 3, "집게 팔", "집게"),
        ("roombapickup", 0.012, 3, "빠른 수거", "수거"),
        ("roombapickup", 0.015, 3, "이중 브러시", "이중 솔"),
        ("roombapickup", 0.018, 3, "자동 집게", "자동 집게"),
    ], 1),
    ("roomba", "로봇 탐지", "로봇이 더 멀리 있는 쓰레기를 찾아간다", [
        ("roombadetect", 0.020, 3, "먼지 센서", "센서"),
        ("roombadetect", 0.020, 3, "탐지 범위", "탐지"),
        ("roombadetect", 0.022, 3, "적외선 센서", "적외선"),
        ("roombadetect", 0.022, 3, "넓은 탐지", "넓은 탐지"),
        ("roombadetect", 0.025, 3, "레이더 센서", "레이더"),
        ("roombadetect", 0.028, 3, "전방위 탐지", "전방위"),
    ], 2),
    ("roomba", "로봇 배터리", "배터리가 좋아져 쉬지 않고 돌아다닌다", [
        ("roombabattery", 0.020, 3, "배터리", "기본 배터리"),
        ("roombabattery", 0.020, 3, "대용량 셀", "대용량"),
        ("roombabattery", 0.022, 3, "충전 효율", "충전"),
        ("roombabattery", 0.022, 3, "연속 가동", "연속"),
        ("roombabattery", 0.025, 3, "급속 충전", "급속"),
        ("roombabattery", 0.028, 3, "무한 배터리", "무한"),
    ], 1),

    # ================= B7 차량·운송 =================
    ("vehicle", "차량 정산", "차량 정산 배수가 오른다", [
        ("bulk", 0.010, 3, "리어카 보강", "보강"),
        ("bulk", 0.010, 3, "짐칸 확장", "짐칸"),
        ("bulk", 0.012, 3, "차량 단가", "단가"),
        ("bulk", 0.012, 3, "화물 정산", "화물"),
        ("bulk", 0.015, 3, "대량 계약", "계약"),
        ("bulk", 0.018, 3, "물류 왕복", "물류"),
    ], 1),
    ("vehicle", "거래 배수", "차량 거래 배수가 한 번 더 곱해진다", [
        ("vehmult", 0.010, 3, "정산 배수", "배수"),
        ("vehmult", 0.010, 3, "거래처 계약", "계약"),
        ("vehmult", 0.012, 3, "직거래", "직거래"),
        ("vehmult", 0.012, 3, "도매 납품", "도매"),
        ("vehmult", 0.015, 3, "독점 계약", "독점"),
        ("vehmult", 0.018, 3, "프리미엄 거래", "프리미엄"),
    ], 2),
    ("vehicle", "대량 정산 기준", "수거통이 덜 차도 대량 보너스·원격 정산이 걸린다", [
        ("bulkthreshold", 0.010, 3, "원격 정산", "기준 80%"),
        ("bulkthreshold", 0.010, 3, "조기 정산", "기준 70%"),
        ("bulkthreshold", 0.012, 3, "절반 정산", "기준 60%"),
        ("bulkthreshold", 0.012, 3, "상시 정산", "기준 55%"),
        ("bulkthreshold", 0.015, 3, "자동 정산", "기준 45%"),
        ("bulkthreshold", 0.020, 3, "완전 자동", "기준 35%"),
    ], 2),
    ("vehicle", "원격 수거", "원격 자동수거가 더 자주, 더 큰 보너스로 터진다", [
        ("remotecooldown", 0.05, 3, "배차 단축", "쿨다운 단축"),
        ("bulksettle", 0.010, 3, "자동 흡입", "대량 보너스"),
        ("bulksettle", 0.010, 3, "문 앞 정산", "보너스 증가"),
        ("remotecooldown", 0.05, 3, "원격 수거", "쿨다운 추가"),
        ("bulksettle", 0.012, 3, "무인 정산", "보너스 강화"),
        ("remotecooldown", 0.06, 3, "완전 자동화", "쿨다운 완성"),
    ], 2),

    # ================= v1.8 B8 장비·압축 (원기옥 → 블랙홀 → 화이트홀) =================
    ("gear", "원기옥", "앞 쓰레기를 한 점으로 끌어모아 구슬로 만든다", [
        ("orbunlock", 1.0, 1, "원기옥", "빔포 해금"),
        ("orbpull", 0.05, 5, "끌어당김", "끌어모으기"),
        ("orbradius", 0.06, 5, "구슬 반경", "모으는 원"),
        ("orbcharge", 0.07, 5, "뭉치기", "구슬 성장"),
        ("orbsize", 0.08, 5, "구슬 크기", "한 방 용량"),
        ("orbauto", 1.0, 1, "풀충 발사", "즉시 발사"),
    ], 1),
    ("gear", "원기옥 정산", "구슬이 터질 때 정산이 더 크게 터진다", [
        ("orbcash", 0.02, 3, "구슬 정산", "정산 상승"),
        ("orbburst", 0.03, 3, "터짐 반경", "광역 터짐"),
        ("orbfly", 0.04, 3, "발사 속도", "빠른 발사"),
        ("orbcash", 0.03, 3, "압축 정산", "정산 강화"),
        ("orbburst", 0.04, 3, "충격파", "광역 확장"),
        ("orbcash", 0.04, 3, "대형 정산", "정산 완성"),
    ], 1),
    ("gear", "블랙홀", "작은 구멍이 주변을 소용돌이로 빨아들인다", [
        ("bhunlock", 1.0, 1, "블랙홀", "건 해금"),
        ("bhradius", 0.05, 4, "사건 지평선", "반경 확장"),
        ("bhcount", 1.0, 1, "쌍 블랙홀", "2개 전개"),
        ("bhspin", 0.06, 4, "회전", "소용돌이"),
        ("bhduration", 0.07, 4, "지속", "유지 시간"),
        ("bhcount", 1.0, 1, "삼중 블랙홀", "3개 전개"),
    ], 1),
    ("gear", "화이트홀", "빨아들인 쓰레기를 트럭 위로 토해낸다", [
        ("whunlock", 1.0, 1, "화이트홀", "자동 정산 해금"),
        ("whcash", 0.02, 3, "토해내기", "정산 상승"),
        ("bhpull", 0.06, 3, "중력", "흡입 강화"),
        ("whcash", 0.03, 3, "분출 정산", "정산 강화"),
        ("bhpull", 0.07, 3, "특이점", "흡입 완성"),
        ("whauto", 1.0, 1, "무한 분출", "자동 정산 완성"),
    ], 1),

    # ================= v1.8 B9 쓰레기 강화 (고가치·내구도·물량) =================
    ("trash", "고가치 폐기물", "묵직하고 값나가는 쓰레기가 섞여 나온다", [
        ("trashcan", 1.0, 1, "캔·병", "고가치 해금"),
        ("trashratio", 0.02, 5, "고가치 비중", "비중 상향"),
        ("trashewaste", 1.0, 1, "전자폐기물", "전자 해금"),
        ("trashratio", 0.025, 5, "폐가전 비중", "비중 강화"),
        ("trashbig", 1.0, 1, "대형 폐가전", "대형 해금"),
        ("trashgold", 1.0, 1, "금괴", "금괴 해금"),
    ], 1),
    ("trash", "내구도", "두꺼운 쓰레기는 여러 번 깎아야 터진다", [
        ("trashdur", 0.10, 5, "두꺼운 재질", "내구도 상향"),
        ("heavydmg", 0.06, 5, "파쇄력", "깎는 힘"),
        ("trashdur", 0.12, 5, "철갑", "내구도 강화"),
        ("heavydmg", 0.08, 5, "분쇄", "깎는 힘 강화"),
        ("trashdur", 0.14, 5, "합금", "내구도 완성"),
        ("heavydmg", 0.10, 5, "완전 분쇄", "분쇄 완성"),
    ], 1),
    ("trash", "고가치 정산", "고가치 폐기물의 정산 단가가 오른다", [
        ("heavyvalue", 0.02, 3, "고가치 단가", "단가 상승"),
        ("sortbonus", 0.02, 3, "분리 보너스", "분리 정산"),
        ("heavyvalue", 0.025, 3, "재활용 단가", "단가 강화"),
        ("rarity", 0.025, 3, "등급 보정", "희귀 보정"),
        ("heavyvalue", 0.03, 3, "프리미엄", "단가 완성"),
        ("heavyjack", 0.05, 3, "금괴 잭팟", "잭팟"),
    ], 1),
    ("trash", "물량·밀도", "더미가 두꺼워지고 웨이브가 늘어난다", [
        ("density", 0.03, 5, "물량", "밀도 상승"),
        ("cluster", 0.04, 5, "더미 밀집", "더미 뭉침"),
        ("density", 0.04, 5, "추가 물량", "밀도 강화"),
        ("wavebonus", 1.0, 2, "추가 웨이브", "웨이브 +1"),
        ("density", 0.05, 5, "대량 물량", "밀도 완성"),
        ("megadump", 1.0, 1, "대량 투하", "마지막 폭탄"),
    ], 1),
]

# 단계별 가격 배수(0단계는 진입 장벽을 낮춰 첫 구매 간격이 벌어지지 않게 한다)
TIER_COST_MULT = (0.22, 0.90, 3.00, 9.00, 26.00, 70.00)
MAXLEVEL_COST_GROWTH = 1.30  # 한 노드 안에서 레벨업 가격 배수
RADIUS_START = 620
RADIUS_STEP = 104
LANE_LATERAL = (-165, -55, 55, 165)

# 계열별 각도(화면 좌표, y가 아래로 +) — 8방향으로 뻗는다
BRANCH_ANGLES = {
    "suction": -126, "bin": -54, "bag": -90, "stain": 162,
    "oxygen": 126, "fever": 90, "roomba": 54, "vehicle": -18,
    "gear": 18, "trash": 198,
}


# v1.8: 레인 첫 노드가 코어가 아니라 다른 장비 노드를 선행으로 요구하는 경우(블랙홀·화이트홀·원기옥 정산).
EXTRA_PREREQ = {
    "gear_1_0": ("gear_0_0", 1),   # 원기옥 정산 ← 원기옥 해금
    "gear_2_0": ("gear_0_4", 1),   # 블랙홀 ← 구슬 크기 Lv.1
    "gear_3_0": ("gear_2_5", 1),   # 화이트홀 ← 삼중 블랙홀 Lv.1
}

# 레인 첫 노드가 코어를 요구하는 레벨(기본값 1). 후반 장비는 더 깊은 코어를 요구한다.
LANE_CORE_LV = {
    "gear_1_0": 2,   # 원기옥 정산 ← 진공청소기 Lv.2(코어 '해금'은 1레벨)
}

# v1.8: 장비/고가치 해금 노드는 '한 번만 사는 큰 관문'이라 티어 가격 배수를 쓰지 않고 고정가로 둔다.
FIXED_COST = {
    "gear_0_0": 18000,    # 원기옥 빔포 (2단계 진공청소기 다음 급의 관문)
    "gear_1_0": 9000,     # 원기옥 정산 가지 입구
    "gear_2_0": 160000,   # 블랙홀 건 (최종 장비)
    "gear_3_0": 1500000,  # 화이트홀 정산
    "trash_0_0": 2000,    # 캔·병 해금
    "trash_0_2": 30000,   # 전자폐기물 해금
    "trash_0_4": 200000,  # 대형 폐가전 해금
    "trash_0_5": 800000,  # 금괴 해금
    "trash_3_5": 300000,  # 대량 투하
}


def first_prereq(branch_id, lane_slot, deposit):
    """레인 첫 노드의 (선행 id, 필요 레벨)."""
    nid = f"{branch_id}_{lane_slot}_0"
    if nid in EXTRA_PREREQ:
        return EXTRA_PREREQ[nid]
    return (deposit, LANE_CORE_LV.get(nid, 1))


def build_nodes():
    """노드 리스트(dict) 생성. 좌표·가격·선행조건·효과가 모두 여기서 결정된다."""
    branch_index = {b[0]: i for i, b in enumerate(BRANCHES)}
    nodes = []
    lane_counter = {b[0]: 0 for b in BRANCHES}

    for branch_id, lane_name, lane_desc, tiers, core_lv in LANES:
        b_idx = branch_index[branch_id]
        lane_slot = lane_counter[branch_id]
        lane_counter[branch_id] += 1
        branch = BRANCHES[b_idx]
        base_cost = branch[4]
        angle = math.radians(BRANCH_ANGLES[branch_id])
        deposit = branch[3][lane_slot]
        prev_id = None
        prev_level = 1
        for tier_i, (eff, per_level, max_lv, name, title_tail) in enumerate(tiers):
            r = RADIUS_START + RADIUS_STEP * tier_i
            lateral = LANE_LATERAL[lane_slot] * (1.0 + 0.18 * tier_i)
            x = math.cos(angle) * r - math.sin(angle) * lateral
            y = math.sin(angle) * r + math.cos(angle) * lateral
            # v1.7: 초반 두 단계는 레벨을 5까지 열어 두어, 언제나 '싼 구매거리'가 남아 있게 한다
            #       (30~90초 구매 리듬 유지 · 후반에는 비싼 노드를 모으는 구간이 생긴다).
            if max_lv > 1 and tier_i <= 1:
                max_lv = 5
            node_id = f"{branch_id}_{lane_slot}_{tier_i}"
            fixed = node_id in FIXED_COST
            cost = FIXED_COST[node_id] if fixed else int(round(base_cost * TIER_COST_MULT[tier_i]))
            if fixed:
                max_lv = 1
            nodes.append(dict(
                id=node_id,
                name=name,
                title=f"{name} · {title_tail}",
                desc=lane_desc,
                branch=b_idx,
                branch_id=branch_id,
                lane=lane_slot,
                tier=tier_i,
                pos=(int(round(x)), int(round(y))),
                max_level=max_lv,
                base_cost=cost,
                growth=1.0 if fixed else MAXLEVEL_COST_GROWTH,
                prereq=prev_id if prev_id else first_prereq(branch_id, lane_slot, deposit)[0],
                prereq_level=prev_level if prev_id else first_prereq(branch_id, lane_slot, deposit)[1],
                milestone=(tier_i == 5),
                effect=eff,
                per_level=per_level,
            ))
            prev_id = node_id
            prev_level = 1
    return nodes


NODES = build_nodes()

EFFECT_ORDER = list(EFFECTS.keys())


def effect_enum(eff):
    return EFFECTS[eff][0]


def total_cost():
    total = 0
    for n in NODES:
        for lv in range(n["max_level"]):
            total += int(round(n["base_cost"] * (n["growth"] ** lv)))
    return total


if __name__ == "__main__":
    print(f"신규 노드 {len(NODES)}개 (코어 28개 포함 시 {len(NODES)+28}개)")
    print(f"신규 노드 전 레벨 비용 합계: ${total_cost():,}")
    ids = [n["id"] for n in NODES]
    assert len(set(ids)) == len(ids), "중복 id"
    names = [n["name"] for n in NODES]
    print(f"이름 중복: {len(names)-len(set(names))}건")
    for b in BRANCHES:
        print(f"  {b[1]}: {sum(1 for n in NODES if n['branch_id']==b[0])}노드")
