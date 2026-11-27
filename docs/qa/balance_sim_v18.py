#!/usr/bin/env python3
"""
TrashCleaner3D v1.8 밸런스 QA 시뮬레이터 (Stage 1~7 × 소비 정책 3종 × 시드 2개 = 6회).

목표
 1) ST.1~7 완주 합계가 약 4시간(210~270분)에 수렴 — 3·4단계 장비(원기옥·블랙홀)가
    방을 순식간에 녹이지 않도록 '내구도 있는 고가치 쓰레기'가 시간을 되받아 준다.
 2) 268노드 트리(코어 28 + 마이크로 240) 전체가 정상 연결(고립·순환·중복 이름 0건)
 3) 끊김 없는 성장: 자주(중간 5~15초) 사는 구간과 비싼 노드를 모으는 구간이 번갈아 나온다.
 4) 3·4단계 장비 해금 시점(스테이지·경과 분)을 표로 보고.

수치 출처(코드와 동일)
 - GamePrototypeManager: HoldDurCurve, BinCaps, EffectiveHoldDuration, SweepRadius, AutoGrabPerTick,
   EffectiveTrashPriceMult, VehicleEffectiveSettleMult, HeavyHp/HeavyBaseValue, HeavyPerWave,
   HeavyHpMult, HeavyValueMult, HeavyTickDamage, OrbDps, BlackHoleDps, TrashDensity, WaveBonus
 - InstancedTrashSwarm: StageWaveCounts, 방 구역(면적·웨이브당 개수)
 - StagePropLayout: 봉투/얼룩 수·체력·보상
 - UpgradeTreeV18: docs/qa/tree_v18.py (C#와 같은 데이터에서 생성)
한계: Unity 에디터가 없어 실제 1인칭 플레이 대신 이산 시뮬레이션으로 근사한다.
      이동·조준 손실은 스테이지별 효율 계수(0.50~0.62)로 모델링한다.
"""

import math
import random
import statistics
import sys

sys.path.insert(0, __file__.rsplit('/', 1)[0])
import tree_v18 as T

STAGE_MULT = lambda s: 1.0 + (s - 1) * 0.12

HOLD_CURVE = [1.00, 0.98, 0.96, 0.94, 0.92, 0.90, 0.88, 0.86, 0.84, 0.82, 0.80]
BIN_CAPS = [35, 50, 70, 95, 130, 180, 250, 340, 480, 680, 980, 1500]

# ===== v1.8 쓰레기 강화 상수 (GamePrototypeManager 와 동일해야 한다) =====
HEAVY_HP = [0.0, 5.5, 14.0, 34.0, 60.0]
HEAVY_VALUE = [0, 60, 220, 900, 4000]
HEAVY_WEIGHT = {1: 0.62, 2: 0.26, 3: 0.09, 4: 0.03}

# (maxLv, base, growth, prereq, prereqLv)
CORE = {
    "hold1": (3, 8, 1.60, None, 0), "rad1": (3, 10, 1.65, "hold1", 1),
    "broom": (1, 325, 1.0, "hold1", 2), "hold2": (4, 190, 1.82, "broom", 1),
    "rad2": (4, 288, 1.84, "broom", 1), "vacuum": (1, 2600, 1.0, "hold2", 3),
    "hold3": (3, 2400, 1.92, "vacuum", 1), "fever": (3, 260, 1.90, "hold2", 1),
    "bin1": (3, 9, 1.62, None, 0), "price1": (3, 12, 1.68, "bin1", 1),
    "cart": (1, 250, 1.0, "bin1", 2), "bin2": (4, 200, 1.82, "cart", 1),
    "price2": (4, 312, 1.84, "cart", 1), "damas": (1, 1500, 1.0, "bin2", 2),
    "bin3": (4, 1760, 1.88, "damas", 1), "combo": (3, 150, 1.85, "price1", 2),
    "truck1t": (1, 8320, 1.0, "damas", 1),
    "speed1": (3, 10, 1.65, None, 0), "mask1": (3, 10, 1.65, "speed1", 1),
    "mop": (1, 110, 1.0, "mask1", 1), "speed2": (3, 220, 1.82, "speed1", 3),
    "mask2": (4, 262, 1.84, "mask1", 3), "scrub": (4, 170, 1.82, "mop", 1),
    "fist1": (3, 11, 1.66, None, 0), "roomba": (3, 0, 1.0, "fist1", 2),
    "rspd1": (3, 250, 1.82, "roomba", 1), "fist2": (3, 260, 1.84, "fist1", 3),
    "rspd2": (3, 1120, 1.88, "roomba", 2),
}
ROOMBA_COST = [280, 820, 2100]
CORE_UNLOCK = {
    "hold1": None, "bin1": None, "speed1": None, "fist1": None,
    "rad1": ("hold1", 1), "broom": ("hold1", 2), "hold2": ("broom", 1), "rad2": ("broom", 1),
    "vacuum": ("hold2", 3), "hold3": ("vacuum", 1), "fever": ("hold2", 1),
    "cart": ("bin1", 2), "bin2": ("cart", 1), "price2": ("cart", 1),
    "damas": ("bin2", 2), "bin3": ("damas", 1), "combo": ("price1", 2), "truck1t": ("damas", 1),
    "mask1": ("speed1", 1), "mop": ("mask1", 1), "speed2": ("speed1", 3),
    "mask2": ("mask1", 3), "scrub": ("mop", 1),
    "roomba": ("fist1", 2), "rspd1": ("roomba", 1), "fist2": ("fist1", 3), "rspd2": ("roomba", 2),
}
CORE_EXTRA = {"vacuum": [("hold2", 3), ("rad2", 2)], "damas": [("bin2", 2), ("price2", 1)],
              "truck1t": [("damas", 1), ("bin3", 2)], "price2": [("cart", 1), ("price1", 2)],
              "combo": [("price1", 2)], "rspd2": [("roomba", 2), ("rspd1", 2)]}

# (waves, [(zone name, minX,maxX,minZ,maxZ, per-wave count), ...])
STAGE_ZONES = {
    1: (1, [("반지하 원룸", -4.35, 4.35, -3.60, 3.55, 960)]),
    2: (4, [("오피스텔 본실", -2.00, 2.00, -3.25, 1.35, 550), ("화장실", -1.95, -0.75, 1.85, 3.25, 40),
            ("현관 복도", 0.35, 1.95, 1.55, 3.25, 85)]),
    3: (6, [("치킨집 주방", -1.70, 2.55, -1.45, 1.10, 975), ("홀·카운터 앞", -0.20, 2.55, 1.00, 2.35, 195)]),
    4: (7, [("거실·놀이매트", -0.55, 3.45, -2.75, 2.35, 2040), ("안방", -3.45, -0.75, -2.75, 0.40, 460),
            ("욕실", -3.45, -1.80, 1.15, 2.80, 170)]),
    5: (6, [("오픈 오피스", -1.20, 4.05, -3.35, 3.30, 3150), ("대표실", -4.10, -1.45, -3.35, -0.65, 540),
            ("탕비실", -4.10, -2.40, 0.75, 3.30, 310)]),
    6: (16, [("시골 농막", -1.95, 1.95, -1.65, 1.65, 1130)]),
    7: (20, [("1층 거실·바", -4.80, 3.00, -3.80, 3.80, 2130), ("2층 파티홀", -2.70, 2.30, -3.80, 3.80, 1400),
            ("2층 서쪽 랜딩", -4.80, -2.90, 0.70, 3.90, 310), ("2층 욕실", 2.50, 4.70, 1.70, 3.80, 180),
            ("3층 VIP홀", -1.70, 2.70, -3.80, 3.80, 1400), ("3층 금고방", -4.80, -2.00, -3.80, -1.10, 1080)]),
}
PROPS = {
    1: dict(piles=8, stains=5, pile_hp=7.0, stain_hp=2.4, stain_reward=18),
    2: dict(piles=30, stains=10, pile_hp=8.0, stain_hp=2.4, stain_reward=22),
    3: dict(piles=20, stains=40, pile_hp=8.0, stain_hp=2.2, stain_reward=24),
    4: dict(piles=36, stains=20, pile_hp=9.0, stain_hp=2.3, stain_reward=26),
    5: dict(piles=45, stains=24, pile_hp=9.0, stain_hp=2.3, stain_reward=30),
    6: dict(piles=30, stains=16, pile_hp=10.0, stain_hp=2.4, stain_reward=30),
    7: dict(piles=60, stains=30, pile_hp=10.0, stain_hp=2.4, stain_reward=34),
}
EFF = {1: 0.62, 2: 0.58, 3: 0.56, 4: 0.54, 5: 0.52, 6: 0.50, 7: 0.50}

V18 = {n["id"]: n for n in T.NODES}
CORE_IDS = list(CORE.keys())

# ===== v1.8 장비 배율(정규화 합 → 배율) =====
ORB_SUM_MAX = 0.25 + 0.30 + 0.35 + 0.40          # orbpull/orbradius/orbcharge/orbsize
BH_SUM_MAX = 0.20 + 0.24 + 0.28 + 0.39 + 2.0     # bhradius/bhspin/bhduration/bhpull/bhcount


def stage_area(stage):
    return sum((z[2] - z[1]) * (z[4] - z[3]) for z in STAGE_ZONES[stage][1])


class Player:
    def __init__(self):
        self.lv = {k: 0 for k in CORE_IDS}
        self.v18 = {k: 0 for k in V18}
        self.money = 0
        self.stage = 1
        self.elapsed = 0.0
        self.unlock_log = {}
        self._memo = {}

    def _c(self, key, fn):
        v = self._memo.get(key)
        if v is None:
            v = fn()
            self._memo[key] = v
        return v

    def invalidate(self):
        self._memo.clear()

    def cost(self, nid):
        if nid in CORE:
            mx, base, g, _, _ = CORE[nid]
            if nid == "roomba":
                return ROOMBA_COST[min(self.lv[nid], 2)]
            return base if mx == 1 else round(base * (g ** self.lv[nid]))
        n = V18[nid]
        return round(n["base_cost"] * (n["growth"] ** self.v18[nid]))

    def maxlv(self, nid):
        return CORE[nid][0] if nid in CORE else V18[nid]["max_level"]

    def level(self, nid):
        return self.lv[nid] if nid in CORE else self.v18[nid]

    def unlocked(self, nid):
        if nid in CORE:
            req = CORE_UNLOCK.get(nid)
            if req and self.level(req[0]) < req[1]:
                return False
            for extra in CORE_EXTRA.get(nid, []):
                if self.level(extra[0]) < extra[1]:
                    return False
            return True
        n = V18[nid]
        return self.level(n["prereq"]) >= n["prereq_level"]

    def buy(self, nid):
        c = self.cost(nid)
        if c > self.money:
            return False
        self.money -= c
        if nid in CORE:
            self.lv[nid] += 1
        else:
            self.v18[nid] += 1
        self.invalidate()
        self._log_unlock(nid)
        return True

    def _log_unlock(self, nid):
        if nid in CORE:
            return
        keys = {"orbunlock": "원기옥", "bhunlock": "블랙홀", "whunlock": "화이트홀",
                "trashcan": "고가치(캔·병)", "trashewaste": "전자폐기물",
                "trashbig": "대형 폐가전", "trashgold": "금괴", "twograb": "양손 줍기"}
        eff = V18[nid]["effect"]
        if eff in keys and keys[eff] not in self.unlock_log:
            self.unlock_log[keys[eff]] = (self.stage, self.elapsed / 60.0)

    def vsum(self, eff):
        return self._c("vsum_" + eff,
                       lambda: sum(V18[k]["per_level"] * lv for k, lv in self.v18.items() if V18[k]["effect"] == eff))

    # ---- 코어 스탯(코드와 동일) ----
    @property
    def total_hold(self): return self.lv["hold1"] + self.lv["hold2"] + self.lv["hold3"]
    @property
    def total_rad(self): return self.lv["rad1"] + self.lv["rad2"]
    @property
    def total_bin(self): return self.lv["bin1"] + self.lv["bin2"] + self.lv["bin3"]
    @property
    def total_price(self): return self.lv["price1"] + self.lv["price2"]
    @property
    def total_speed(self): return self.lv["speed1"] + self.lv["speed2"]
    @property
    def total_mask(self): return self.lv["mask1"] + self.lv["mask2"]
    @property
    def total_fist(self): return self.lv["fist1"] + self.lv["fist2"]
    @property
    def total_roomba_spd(self): return self.lv["rspd1"] + self.lv["rspd2"]
    @property
    def has_vacuum(self): return self.lv["vacuum"] > 0
    @property
    def has_broom(self): return self.lv["broom"] > 0
    @property
    def has_orb(self): return self.vsum("orbunlock") > 0 or self.vsum("bhunlock") > 0
    @property
    def has_bh(self): return self.vsum("bhunlock") > 0
    @property
    def has_wh(self): return self.vsum("whunlock") > 0
    @property
    def tool_tier(self): return 4 if self.has_bh else 3 if self.has_orb else 2 if self.has_vacuum else 1 if self.has_broom else 0
    @property
    def vehicle_stage(self): return 3 if self.lv["truck1t"] else 2 if self.lv["damas"] else 1 if self.lv["cart"] else 0

    @property
    def stench_resist(self): return min(0.60, self.vsum("stenchslow"))

    def resist(self, base):
        return 1 - (1 - base) * (1 - self.stench_resist)

    @property
    def stench_move(self): return self.resist(0.85)
    @property
    def stench_work(self): return self.resist(0.84)
    @property
    def oxygen_zone(self): return 0.60
    @property
    def oxygen_mask(self): return min(1.0, self.total_mask / 5.0)
    @property
    def oxygen_boost(self):
        return self.oxygen_zone * self.oxygen_mask if self.vsum("oxygenunlock") > 0 else 0.0
    @property
    def oxygen_suction(self): return 1 + self.vsum("oxygenpower") * self.oxygen_boost
    @property
    def oxygen_move(self): return 1 + (self.vsum("oxygenpower") + self.vsum("oxygenmove")) * self.oxygen_boost
    @property
    def fever_speed(self): return 0.30 * (0.08 + self.lv["fever"] * 0.10) * (1 + self.vsum("fevermax")) if self.lv["fever"] else 0.0

    @property
    def hold_dur(self):
        base = HOLD_CURVE[min(self.total_hold, len(HOLD_CURVE) - 1)]
        if self.has_broom: base *= 0.96
        if self.has_vacuum: base *= 0.92
        spd = max(0.45, self.stench_work) * (1 + self.fever_speed) * self.oxygen_suction * (1 + self.vsum("hold"))
        return max(0.12, min(1.6, base / spd))

    @property
    def radius(self):
        base = 0.62 if self.has_vacuum else 0.34 if self.has_broom else 0.15
        fever_r = 0.30 * (0.04 + self.lv["fever"] * 0.06) * (1 + self.vsum("fevermax")) if self.lv["fever"] else 0.0
        return (base + self.total_rad * 0.05) * (1 + fever_r) * (1 + self.vsum("radius"))

    @property
    def grab(self):
        extra = round(self.vsum("grab")) + (1 if self.vsum("twograb") > 0 else 0)
        if self.has_vacuum: return 2 + self.lv["rad2"] + self.lv["hold3"] + extra
        if self.has_broom: return 2 + self.lv["rad1"] // 2 + self.lv["rad2"] + extra
        return 1 + self.lv["rad1"] // 3 + extra

    @property
    def bin_cap(self):
        cap = BIN_CAPS[min(self.total_bin, len(BIN_CAPS) - 1)] * (2 if self.has_vacuum else 1)
        return round(cap * (1 + self.vsum("bincap")))

    @property
    def price_mult(self):
        return (1 + self.total_price * 0.04) * STAGE_MULT(self.stage) * (1 + self.vsum("price"))

    @property
    def vehicle_mult(self):
        base = [1.0, 1.20, 1.45, 1.80][self.vehicle_stage]
        return (base + self.vsum("bulk")) * (1 + self.vsum("vehmult"))

    @property
    def bulk_threshold(self): return max(0.35, min(0.80, 0.80 - self.vsum("bulkthreshold")))
    @property
    def bulk_settle(self): return 1 + self.vsum("bulksettle")

    @property
    def move_speed(self):
        return 3.85 * (1 + self.total_speed * 0.03) * self.oxygen_move * (1 + self.vsum("move")) * self.stench_move

    @property
    def fist_damage(self):
        return (1.35 + self.total_fist * 0.10 + (0.5 if self.has_vacuum else 0)) * (1 + self.vsum("fist"))

    @property
    def fist_crit(self):
        chance = min(0.60, self.vsum("fistcrit"))
        return 1 + chance * (1.6 + self.vsum("fistcritmul") - 1)

    @property
    def scrub_mult(self):
        return (2.4 if self.lv["mop"] else 1.0) * (1 + self.lv["scrub"] * 0.12) * (1 + self.vsum("scrub")) * (1 + self.vsum("scrubrange") * 0.6)

    @property
    def roomba_interval(self):
        return max(0.45, (1.0 - self.total_roomba_spd * 0.02) * (1 - min(0.35, self.vsum("roombapickup"))) * (1 - min(0.30, self.vsum("roombabattery"))))

    @property
    def roomba_rate(self):
        if self.lv["roomba"] == 0: return 0.0
        raw = self.lv["roomba"] / self.roomba_interval
        return min(raw, (self.grab / self.hold_dur) * 0.20)

    # ---- v1.8 장비/쓰레기 ----
    @property
    def gear_rate_mult(self):
        orb_sum = self.vsum("orbpull") + self.vsum("orbradius") + self.vsum("orbcharge") + self.vsum("orbsize")
        bh_sum = self.vsum("bhradius") + self.vsum("bhspin") + self.vsum("bhduration") + self.vsum("bhpull") + self.vsum("bhcount")
        if self.has_bh:
            m = 2.05 + 0.65 * min(1.0, bh_sum / BH_SUM_MAX)
            if self.has_wh: m += 0.20
            return m
        if self.has_orb:
            return 1.55 + 0.55 * min(1.0, orb_sum / ORB_SUM_MAX)
        return 1.0

    @property
    def heavy_per_wave(self):
        if self.vsum("trashcan") <= 0: return 0
        return 4 + round(min(0.6, self.vsum("trashratio")) * 90)

    @property
    def heavy_hp_mult(self): return 1 + self.vsum("trashdur")

    @property
    def heavy_value_mult(self):
        return (1 + self.vsum("heavyvalue")) * (1 + self.vsum("sortbonus") + self.vsum("rarity"))

    @property
    def heavy_weights(self):
        w = {1: HEAVY_WEIGHT[1]}
        if self.vsum("trashewaste") > 0: w[2] = HEAVY_WEIGHT[2]
        if self.vsum("trashbig") > 0: w[3] = HEAVY_WEIGHT[3]
        if self.vsum("trashgold") > 0: w[4] = HEAVY_WEIGHT[4]
        s = sum(w.values())
        return {k: v / s for k, v in w.items()}

    @property
    def heavy_hp_avg(self):
        return sum(w * HEAVY_HP[c] for c, w in self.heavy_weights.items()) * self.heavy_hp_mult

    @property
    def heavy_value_avg(self):
        jack = 1.0 + (0.25 * self.vsum("heavyjack") if self.vsum("trashgold") > 0 else 0.0)
        return sum(w * HEAVY_VALUE[c] for c, w in self.heavy_weights.items()) * self.heavy_value_mult * jack

    @property
    def heavy_dps(self):
        if self.has_bh:
            count = 1 + round(self.vsum("bhcount"))
            return 6.5 * (1 + self.vsum("bhpull")) * (1 + self.vsum("heavydmg")) * (1 + 0.5 * (count - 1))
        if self.has_orb:
            return 5.5 * (1 + self.vsum("orbcharge")) * (1 + self.vsum("heavydmg")) * 1.3
        tick = 2.4 if self.has_vacuum else 1.5 if self.has_broom else 1.05
        return tick * (1 + self.vsum("heavydmg")) / max(0.12, self.hold_dur)


# '관문 노드' = 장비 해금·도구/차량 티어 해금처럼 판을 바꾸는 노드.
# 사람은 싼 것만 사지 않고 이런 관문을 모아서 산다(seed 정책에 반영).
GATE_IDS = set(T.FIXED_COST.keys()) | {"broom", "vacuum", "cart", "damas", "truck1t", "mop", "roomba"}

POLICIES = {
    "cheapest": dict(max_buys=3, prefer="cheap"),
    "balanced": dict(max_buys=3, prefer="gate"),
    "longterm": dict(max_buys=2, prefer="afford2x"),
}


def buy_some(p, policy):
    cfg = POLICIES[policy]
    bought = 0
    for _ in range(cfg["max_buys"]):
        cands = []
        for nid in CORE_IDS + list(V18):
            if not p.unlocked(nid) or p.level(nid) >= p.maxlv(nid):
                continue
            c = p.cost(nid)
            if c <= p.money:
                cands.append((c, nid))
        if not cands:
            break
        if cfg["prefer"] == "afford2x":
            rich = [t for t in cands if t[0] * 2 <= p.money]
            c, nid = (max(rich, key=lambda t: t[0]) if rich else min(cands, key=lambda t: t[0]))
        elif cfg["prefer"] == "gate":
            gates = [t for t in cands if t[1] in GATE_IDS or (t[1] in V18 and V18[t[1]]["milestone"])]
            c, nid = (min(gates, key=lambda t: t[0]) if gates else min(cands, key=lambda t: t[0]))
        else:
            c, nid = min(cands, key=lambda t: t[0])
        p.buy(nid)
        bought += 1
    return bought


def simulate_stage(p, stage, policy, seed, dt=0.5, trace=None):
    rng = random.Random(seed * 100 + stage)
    p.stage = stage
    base_waves, zones = STAGE_ZONES[stage]
    density = 1 + min(0.95, p.vsum("density"))
    waves = base_waves + min(2, round(p.vsum("wavebonus")))
    mega = p.vsum("megadump") > 0
    per_wave = sum(round(z[5] * density) for z in zones)
    normal_items = 0
    for w in range(waves):
        normal_items += sum(round(z[5] * density * (1.5 if (mega and w == waves - 1) else 1.0)) for z in zones)
    heavy_items = 0
    if p.heavy_per_wave > 0:
        for w in range(waves):
            heavy_items += round(p.heavy_per_wave * (1.5 if (mega and w == waves - 1) else 1.0))
    items = normal_items + heavy_items
    area = stage_area(stage)
    plan = PROPS[stage]

    normal_left = float(normal_items)
    heavy_left = float(heavy_items)
    visible = float(per_wave)
    wave = 1
    pending = 0.0
    pending_value = 0.0
    income = 0.0
    combo = 0
    combo_timer = 0.0
    buys = 0
    buys_times = []
    next_buy_check = 0.0
    last_buy_t = 0.0
    max_gap = 0.0
    t = 0.0
    heavy_seconds = 0.0

    pile_left = plan["piles"]
    next_pile_at = items / (plan["piles"] + 1) * 1.0
    eff = EFF[stage]
    crit = min(0.45, p.vsum("crit"))
    chain_chance = min(0.45, p.vsum("chain"))
    chain_extra = 1 + round(p.vsum("chaincount")) if p.vsum("chaincount") > 0 else 0

    while (normal_left > 0.001 or heavy_left > 0.001) and t < 4 * 3600:
        # 남은 쓰레기 밀도(마지막 웨이브 뒤에도 바닥에 남은 양 기준) — 웨이브 상한으로 폭주 방지
        density_now = max(0.3, min(normal_left, per_wave) / area)
        sweep = 2 * p.radius * p.move_speed * density_now
        player_rate = (p.grab / p.hold_dur) * p.oxygen_suction
        n_rate = (min(player_rate, sweep) * eff * p.gear_rate_mult + p.roomba_rate)
        n_rate = max(0.05, n_rate)

        heavy_rate = 0.0
        if heavy_left > 0.001:
            per_item = max(0.06, p.heavy_hp_avg / max(0.2, p.heavy_dps))
            heavy_rate = eff / per_item
            heavy_rate = max(0.001, heavy_rate)

        t_n = normal_left / n_rate
        t_h = heavy_left / heavy_rate if heavy_rate > 0 else 0.0
        est = t_n + t_h
        if est <= 0.0001:
            break
        work = normal_left + heavy_left
        rate_eff = work / est
        step = min(dt, est)
        if visible <= 0.001 and wave < waves:
            wave += 1
            visible = float(per_wave)
            continue
        got = min(work, rate_eff * step)
        g_n = got * (normal_left / work) if work > 0 else 0.0
        g_h = got - g_n
        if visible > 0:
            g_n = min(g_n, visible)
        normal_left -= g_n
        heavy_left -= g_h
        visible = max(0.0, visible - g_n)
        heavy_seconds += g_h * (p.heavy_hp_avg / max(0.2, p.heavy_dps)) if g_h > 0 else 0.0
        t += step

        normal_value = 2.0 * p.price_mult * (1 + crit * 1.0) * (1 + chain_chance * chain_extra)
        pending += g_n + g_h
        pending_value += g_n * normal_value + g_h * p.heavy_value_avg * p.price_mult

        # 큰 봉투
        if pile_left > 0 and (items - (normal_left + heavy_left)) >= next_pile_at:
            hits = math.ceil(plan["pile_hp"] / max(0.5, p.fist_damage))
            t += hits * 0.30 + 1.2
            pile_left -= 1
            next_pile_at = items * (plan["piles"] - pile_left + 1) / (plan["piles"] + 1)
            burst = p.vsum("burstcash") * STAGE_MULT(stage)
            if burst > 0:
                p.money += burst
                income += burst
            if p.vsum("chainunlock") > 0 and visible > 0:
                c = min(visible, 8 + 2 * round(p.vsum("chaincount")))
                normal_left = max(0.0, normal_left - c)
                visible -= c
                pending += c
                pending_value += c * normal_value

        # 수거통 정산
        if pending >= p.bin_cap:
            combo = min(5, combo + 1) if combo_timer > 0 else 1
            combo_timer = 6.0 + p.lv["combo"] * 2.5 + p.vsum("combowindow")
            fill = pending / max(1, p.bin_cap)
            bulk = p.bulk_settle if fill >= p.bulk_threshold else 1.0
            cm = (1 + (combo - 1) * 0.2) if p.lv["combo"] > 0 else 1.0
            gain = pending_value * cm * p.vehicle_mult * (1 + p.vsum("settle")) * bulk
            p.money += gain
            income += gain
            pending = 0.0
            pending_value = 0.0
            t += [4.0, 1.4, 0.7, 0.0][p.vehicle_stage]

        if combo_timer > 0:
            combo_timer -= step

        if wave < waves and visible <= max(150, per_wave * 0.10):
            wave += 1
            visible = float(per_wave)

        if t >= next_buy_check:
            next_buy_check = t + 2.0
            p.elapsed += 0  # (스테이지 누적은 run_campaign 에서 더한다)
            n = buy_some(p, policy)
            if n:
                buys += n
                buys_times.append(t)
                if buys > 1:
                    max_gap = max(max_gap, t - last_buy_t)
                last_buy_t = t

    # 2단계: 바닥 얼룩
    stain_power = (0.55 + 2.2) * p.scrub_mult * max(0.45, p.stench_work)
    for _ in range(plan["stains"]):
        t += plan["stain_hp"] / max(0.05, stain_power) + 2.4
    stain_income = plan["stains"] * plan["stain_reward"] * p.price_mult * (1 + p.vsum("stainreward"))
    p.money += stain_income
    income += stain_income

    if pending > 0:
        gain = pending_value * p.vehicle_mult * (1 + p.vsum("settle"))
        if p.vsum("bulksettle") > 0:
            gain *= p.bulk_settle
        p.money += gain
        income += gain
        pending = 0.0
        pending_value = 0.0

    room_bonus = len(zones) * (140 if stage >= 2 else 60) * STAGE_MULT(stage)
    clear_bonus = 280 * STAGE_MULT(stage)
    p.money += room_bonus + clear_bonus
    income += room_bonus + clear_bonus

    row = dict(stage=stage, seconds=t, income=income, buys=buys, max_gap=max_gap,
               end_money=p.money, items=items, normal=normal_items, heavy=heavy_items,
               per_wave=per_wave, waves=waves, area=area, heavy_minutes=heavy_seconds / 60.0,
               gear=p.tool_tier, buy_times=list(buys_times))
    if trace is not None:
        trace.append(row)
    return row


def run_campaign(policy, seed):
    p = Player()
    rows = []
    for st in range(1, 8):
        r = simulate_stage(p, st, policy, seed)
        p.elapsed += r["seconds"]
        rows.append(r)
    return p, rows


def validate_tree():
    errors = []
    ids = set(V18) | set(CORE_IDS)
    for nid in V18:
        seen = set()
        cur = nid
        while cur in V18:
            if cur in seen:
                errors.append(f"순환 참조: {nid}")
                break
            seen.add(cur)
            cur = V18[cur]["prereq"]
        if V18[nid]["prereq"] not in ids:
            errors.append(f"미정의 선행: {nid} → {V18[nid]['prereq']}")
    names = [n["name"] for n in T.NODES]
    dup = len(names) - len(set(names))
    if dup:
        errors.append(f"이름 중복 {dup}건")
    reachable = {"hold1", "bin1", "speed1", "fist1"}
    changed = True
    while changed:
        changed = False
        for nid in V18:
            if nid in reachable:
                continue
            if V18[nid]["prereq"] in reachable:
                reachable.add(nid)
                changed = True
        for nid in CORE_IDS:
            if nid in reachable:
                continue
            req = CORE_UNLOCK.get(nid)
            extra = CORE_EXTRA.get(nid, [])
            ok = (req is None or req[0] in reachable) and all(e[0] in reachable for e in extra)
            if ok:
                reachable.add(nid)
                changed = True
    isolated = [n for n in ids if n not in reachable]
    if isolated:
        errors.append(f"도달 불가 노드 {len(isolated)}개: {isolated[:8]}")
    return errors


def validate_code_sync():
    """C# 코드(스웜 존·웨이브, 매니저 고가치 상수)와 이 시뮬레이터 수치가 같은지 확인한다."""
    import os
    import re
    root = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "TrashCleaner3D",
                        "Assets", "_Project", "Scripts")
    errs = []
    swarm = open(os.path.join(root, "InstancedTrashSwarm.cs"), encoding="utf-8").read()
    mgr = open(os.path.join(root, "GamePrototypeManager.cs"), encoding="utf-8").read()

    m = re.search(r"StageWaveCounts\s*=\s*\{([^}]*)\}", swarm)
    if not m:
        errs.append("StageWaveCounts 를 찾지 못했다")
    else:
        waves = [int(x) for x in m.group(1).split(",")]
        for st in range(1, 8):
            if waves[st] != STAGE_ZONES[st][0]:
                errs.append(f"ST{st} 웨이브 수 C#={waves[st]} 시뮬={STAGE_ZONES[st][0]}")

    stage_blocks = re.split(r"private static readonly RoomZoneDef\[\] Stage(\d)Rooms\s*=", swarm)
    for i in range(1, len(stage_blocks), 2):
        st = int(stage_blocks[i])
        body = stage_blocks[i + 1]
        found = re.findall(
            r"minX =\s*(-?[\d.]+)f, maxX =\s*(-?[\d.]+)f, minZ =\s*(-?[\d.]+)f, maxZ =\s*(-?[\d.]+)f, floorY =\s*(-?[\d.]+)f.*?countToSpawn =\s*(\d+)",
            body, re.S)
        zones = STAGE_ZONES[st][1]
        if len(found) != len(zones):
            errs.append(f"ST{st} 구역 수 C#={len(found)} 시뮬={len(zones)}")
            continue
        for c, z in zip(found, zones):
            cs = (round(float(c[0]), 2), round(float(c[1]), 2), round(float(c[2]), 2), round(float(c[3]), 2),
                  round(float(c[4]), 2), int(c[5]))
            floor_y = 6.11 if z[0].startswith("3층") else 3.11 if z[0].startswith("2층") else 0.0
            zs = (round(float(z[1]), 2), round(float(z[2]), 2), round(float(z[3]), 2),
                  round(float(z[4]), 2), floor_y, int(z[5]))
            if cs != zs:
                errs.append(f"ST{st} '{z[0]}' 구역 수치 불일치 C#={cs} 시뮬={zs}")

    m = re.search(r"HeavyHp\s*=\s*\{([^}]*)\}", mgr)
    if not m:
        errs.append("HeavyHp 를 찾지 못했다")
    else:
        hp = [float(x.replace("f", "")) for x in m.group(1).split(",")]
        if [round(x, 3) for x in hp] != [round(x, 3) for x in HEAVY_HP]:
            errs.append(f"HeavyHp C#={hp} 시뮬={HEAVY_HP}")
    m = re.search(r"HeavyBaseValue\s*=\s*\{([^}]*)\}", mgr)
    if not m:
        errs.append("HeavyBaseValue 를 찾지 못했다")
    else:
        vals = [int(x) for x in m.group(1).split(",")]
        if vals != HEAVY_VALUE:
            errs.append(f"HeavyBaseValue C#={vals} 시뮬={HEAVY_VALUE}")
    if "HeavyCan ? 4 + Mathf.RoundToInt(HeavyRatio01 * 90f) : 0" not in mgr:
        errs.append("HeavyPerWave 공식이 시뮬(4 + ratio*90)과 다르다")
    return errs


def gap_stats(rows):
    ts = sorted(set(round(x, 1) for x in rows))
    g = [ts[i + 1] - ts[i] for i in range(len(ts) - 1)]
    if not g:
        return 0, 0, 0
    g.sort()
    return statistics.median(g), g[int(len(g) * 0.9)], g[-1]


def main():
    print(f"노드 구성: 코어 28 + 마이크로 {len(V18)} = {28 + len(V18)}개")
    print(f"신규 노드 전 레벨 총비용: ${T.total_cost():,}")
    errs = validate_tree()
    print("트리 검증:", "이상 없음 (고립·순환·중복 이름 0)" if not errs else errs)
    sync = validate_code_sync()
    print("C# ↔ 시뮬 수치:", "일치" if not sync else sync)
    errs += sync

    all_runs = []
    for policy in ("cheapest", "balanced", "longterm"):
        for seed in (11, 29):
            p, rows = run_campaign(policy, seed)
            all_runs.append((policy, seed, rows, p))

    base = [r for (pol, sd, r, p) in all_runs if pol == "balanced" and sd == 29][0]
    print(f"\n=== 정책=balanced · seed=29 ===")
    print(f"{'ST':>2} {'웨이브':>6} {'일반':>8} {'고가치':>6} {'클리어':>7} {'중 고가치':>8} {'수익($)':>9} {'구매':>4} {'장비':>4}")
    for r in base:
        print(f"{r['stage']:>2} {r['waves']:>3}×{r['per_wave']:<5} {r['normal']:>8,} {r['heavy']:>6,} "
              f"{r['seconds']/60:>6.1f}분 {r['heavy_minutes']:>7.1f}분 {r['income']:>9,.0f} {r['buys']:>4} {r['gear']:>4}")

    print("\n=== 정책별 캠페인 총 시간 ===")
    totals = []
    for policy, seed, rows, p in all_runs:
        total = sum(r["seconds"] for r in rows) / 60
        totals.append(total)
        allg = []
        for r in rows:
            allg += r["buy_times"]
        med, p90, mx = gap_stats(allg)
        print(f"{policy:<9} seed={seed:<3} 총 {total:6.1f}분 · 구매 {sum(r['buys'] for r in rows):>4}회 "
              f"· 간격 중간 {med:4.0f}초 / p90 {p90:4.0f}초 / 최대 {mx:4.0f}초")

    print(f"\n캠페인 총 시간: {min(totals):.1f} ~ {max(totals):.1f}분 (목표 210~270분)")

    print("\n=== 장비·쓰레기 해금 시점 (balanced · seed 29) ===")
    pb = [p for (pol, sd, r, p) in all_runs if pol == "balanced" and sd == 29][0]
    if not pb.unlock_log:
        print("  (해금 없음)")
    for k, (st, minute) in sorted(pb.unlock_log.items(), key=lambda kv: kv[1][1]):
        print(f"  {k:<14} ST.{st} · {minute:5.1f}분")

    final = max(totals) - min(totals)
    ok = all(210 <= x <= 270 for x in totals) and not errs
    print("\n검증:", "통과" if ok else "재조정 필요")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
