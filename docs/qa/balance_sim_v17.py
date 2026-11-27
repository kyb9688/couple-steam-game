#!/usr/bin/env python3
"""
TrashCleaner3D v1.7 밸런스 QA 시뮬레이터 (Stage 1~7 × 소비 정책 2종 × 시드 2개 = 8회).

목표
 1) ST.1~7 완주 합계가 약 4시간(210~270분)에 수렴
 2) 220노드 트리 전체가 정상 연결(고립 노드·순환 참조 없음)
 3) 끊김 없는 성장: 매 30~90초 안에 노드를 1개 이상 구매 가능
 4) 스테이지별 예상 클리어 시간·물량 표 산출

수치 출처(코드와 동일)
 - GamePrototypeManager: HoldDurCurve, BinCaps, EffectiveHoldDuration, SweepRadius, AutoGrabPerTick,
   EffectiveTrashPriceMult, VehicleEffectiveSettleMult, BulkThreshold01/SettleBonus, Roomba*, FistDamage
 - InstancedTrashSwarm: StageWaveCounts, 방 구역(면적·웨이브당 개수)
 - StagePropLayout: 봉투/얼룩 수·체력·보상
 - UpgradeTreeV17: docs/qa/tree_v17.py (C#와 같은 데이터에서 생성)
한계: Unity 에디터가 없어 실제 1인칭 플레이 대신 이산 시뮬레이션으로 근사한다.
      이동·조준 손실은 스테이지별 효율 계수(0.50~0.62)로 모델링한다.
"""

import math
import random
import statistics
import sys

sys.path.insert(0, __file__.rsplit('/', 1)[0])
import tree_v17 as T

STAGE_MULT = lambda s: 1.0 + (s - 1) * 0.12

HOLD_CURVE = [1.00, 0.98, 0.96, 0.94, 0.92, 0.90, 0.88, 0.86, 0.84, 0.82, 0.80]
BIN_CAPS = [35, 50, 70, 95, 130, 180, 250, 340, 480, 680, 980, 1500]

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
# IsNodeUnlocked 의 코어 분기와 동일 (id -> (prereq id, lv) 또는 루트)
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
# 실제 코드의 복합 조건(단일 prereq 로 근사한 부분 포함)
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
    6: (9, [("시골 농막", -1.95, 1.95, -1.65, 1.65, 1130)]),
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

V17 = {n["id"]: n for n in T.NODES}
CORE_IDS = list(CORE.keys())


def stage_area(stage):
    return sum((z[2] - z[1]) * (z[4] - z[3]) for z in STAGE_ZONES[stage][1])


class Player:
    def __init__(self):
        self.lv = {k: 0 for k in CORE_IDS}
        self.v17 = {k: 0 for k in V17}
        self.money = 0
        self.stage = 1
        self._memo = {}

    def _c(self, key, fn):
        v = self._memo.get(key)
        if v is None:
            v = fn()
            self._memo[key] = v
        return v

    def invalidate(self):
        self._memo.clear()

    # ---- 비용/해금 ----
    def cost(self, nid):
        if nid in CORE:
            mx, base, g, _, _ = CORE[nid]
            if nid == "roomba":
                return ROOMBA_COST[min(self.lv[nid], 2)]
            return base if mx == 1 else round(base * (g ** self.lv[nid]))
        n = V17[nid]
        return round(n["base_cost"] * (n["growth"] ** self.v17[nid]))

    def maxlv(self, nid):
        return CORE[nid][0] if nid in CORE else V17[nid]["max_level"]

    def level(self, nid):
        return self.lv[nid] if nid in CORE else self.v17[nid]

    def unlocked(self, nid):
        if nid in CORE:
            req = CORE_UNLOCK.get(nid)
            if req and self.level(req[0]) < req[1]:
                return False
            for extra in CORE_EXTRA.get(nid, []):
                if self.level(extra[0]) < extra[1]:
                    return False
            return True
        n = V17[nid]
        return self.level(n["prereq"]) >= n["prereq_level"]

    def buy(self, nid):
        c = self.cost(nid)
        if c > self.money:
            return False
        self.money -= c
        if nid in CORE:
            self.lv[nid] += 1
        else:
            self.v17[nid] += 1
        self.invalidate()
        return True

    def vsum(self, eff):
        return self._c("vsum_" + eff,
                       lambda: sum(V17[k]["per_level"] * lv for k, lv in self.v17.items() if V17[k]["effect"] == eff))

    # ---- 스탯(코드와 동일) ----
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
    def tool_tier(self): return 2 if self.has_vacuum else 1 if self.has_broom else 0
    @property
    def vehicle_stage(self): return 3 if self.lv["truck1t"] else 2 if self.lv["damas"] else 1 if self.lv["cart"] else 0

    @property
    def stench_resist(self): return min(0.60, self.vsum("stenchslow"))

    def resist(self, base):
        return 1 - (1 - base) * (1 - self.stench_resist)

    @property
    def stench_move(self): return self.resist(0.85)   # 평균 악취 30~40% (1단계) 가정
    @property
    def stench_work(self): return self.resist(0.84)

    @property
    def oxygen_zone(self): return 0.60   # 45% 이하 구간에서 평균 60% (환기 루프)
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
        extra = round(self.vsum("grab"))
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


POLICIES = {
    "cheapest": dict(max_buys=3, prefer="cheap"),
    "balanced": dict(max_buys=3, prefer="cheap"),
    "longterm": dict(max_buys=2, prefer="afford2x"),
}


def buy_some(p, policy):
    cfg = POLICIES[policy]
    bought = 0
    for _ in range(cfg["max_buys"]):
        cands = []
        for nid in CORE_IDS + list(V17):
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
        else:
            c, nid = min(cands, key=lambda t: t[0])
        p.buy(nid)
        bought += 1
    return bought


def simulate_stage(p, stage, policy, seed, dt=0.5, trace=None):
    rng = random.Random(seed * 100 + stage)
    p.stage = stage
    waves, zones = STAGE_ZONES[stage]
    per_wave = sum(z[5] for z in zones)
    items = per_wave * waves
    area = stage_area(stage)
    plan = PROPS[stage]

    t = 0.0
    cleaned = 0.0
    visible = float(per_wave)         # 현재 웨이브에 남은(화면에 보이는) 쓰레기
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

    pile_left = plan["piles"]
    next_pile_at = items / (plan["piles"] + 1) * 1.0
    eff = EFF[stage]
    crit = min(0.45, p.vsum("crit"))
    chain_chance = min(0.45, p.vsum("chain"))
    chain_extra = 1 + round(p.vsum("chaincount")) if p.vsum("chaincount") > 0 else 0

    while cleaned < items and t < 4 * 3600:
        remain = items - cleaned
        density = max(0.3, visible / area)
        sweep = 2 * p.radius * p.move_speed * density
        player_rate = (p.grab / p.hold_dur) * p.oxygen_suction
        rate = min(player_rate, sweep) * eff + p.roomba_rate
        rate = max(0.05, rate)

        if visible <= 0.001:
            # 웨이브가 남았는데 화면이 비었다면 즉시 다음 더미 공급(정상 흐름에서는 발생하지 않음)
            if wave < waves:
                wave += 1
                visible = float(per_wave)
                continue
            break
        step = min(dt, visible / rate, remain / rate)
        got = min(remain, visible, rate * step)
        value_per_item = 2.0 * p.price_mult * (1 + crit * 1.0) * (1 + chain_chance * chain_extra)
        picked = got * (1 + chain_chance * chain_extra)
        cleaned += got
        visible = max(0.0, visible - got)
        pending += got
        pending_value += got * value_per_item
        t += step

        # 큰 봉투
        if pile_left > 0 and cleaned >= next_pile_at:
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
                cleaned += c
                visible -= c
                pending += c
                pending_value += c * value_per_item

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
            # 왕복 비용(차량 단계): 쓰레기통 4.0초 / 리어카 1.4 / 다마스 0.7 / 1톤 0
            t += [4.0, 1.4, 0.7, 0.0][p.vehicle_stage]

        if combo_timer > 0:
            combo_timer -= step

        # 웨이브 공급
        if wave < waves and visible <= max(150, per_wave * 0.10):
            wave += 1
            visible = float(per_wave)

        # 구매(2초마다 검사)
        if t >= next_buy_check:
            next_buy_check = t + 2.0
            n = buy_some(p, policy)
            if n:
                buys += n
                buys_times.append(t)   # 구매가 일어난 '시점'만 기록(한 번에 2~3개 구매해도 1회)
                if buys > 1:
                    gap = t - last_buy_t
                    max_gap = max(max_gap, gap)
                last_buy_t = t

    # 2단계: 바닥 얼룩
    stain_power = (0.55 + 2.2) * p.scrub_mult * max(0.45, p.stench_work)
    for _ in range(plan["stains"]):
        t += plan["stain_hp"] / max(0.05, stain_power) + 2.4
        if p.vsum("scrubfever") > 0:
            pass
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

    if trace is not None:
        trace.append(dict(stage=stage, seconds=t, income=income, buys=buys,
                          max_gap=max_gap, end_money=p.money, items=items,
                          per_wave=per_wave, waves=waves, area=area))
    return dict(stage=stage, seconds=t, income=income, buys=buys, max_gap=max_gap,
                end_money=p.money, items=items, per_wave=per_wave, waves=waves, area=area,
                buy_times=list(buys_times))


def run_campaign(policy, seed):
    p = Player()
    rows = []
    for st in range(1, 8):
        rows.append(simulate_stage(p, st, policy, seed))
    return p, rows


def validate_tree():
    """고립 노드(선행이 영원히 불가)·순환 참조·미정의 선행 검사."""
    errors = []
    ids = set(V17) | set(CORE_IDS)
    # 순환 참조
    for nid in V17:
        seen = set()
        cur = nid
        while cur in V17:
            if cur in seen:
                errors.append(f"순환 참조: {nid}")
                break
            seen.add(cur)
            cur = V17[cur]["prereq"]
    # 선행이 존재하지 않는 노드
    for nid in V17:
        if V17[nid]["prereq"] not in ids:
            errors.append(f"미정의 선행: {nid} → {V17[nid]['prereq']}")
    # 도달 가능성: 코어 루트에서 BFS
    reachable = {"hold1", "bin1", "speed1", "fist1"}
    changed = True
    while changed:
        changed = False
        for nid in V17:
            if nid in reachable:
                continue
            if V17[nid]["prereq"] in reachable:
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


def gap_stats(rows):
    ts = sorted(set(round(x, 1) for x in rows))
    g = [ts[i + 1] - ts[i] for i in range(len(ts) - 1)]
    if not g:
        return 0, 0, 0
    g.sort()
    return statistics.median(g), g[int(len(g) * 0.9)], g[-1]


def main():
    print(f"노드 구성: 코어 28 + 마이크로 {len(V17)} = {28 + len(V17)}개")
    print(f"신규 노드 전 레벨 총비용: ${T.total_cost():,}")
    errs = validate_tree()
    print("트리 검증:", "이상 없음 (고립 노드·순환 참조 0)" if not errs else errs)

    all_runs = []
    for policy in ("cheapest", "balanced", "longterm"):
        for seed in (11, 29):
            p, rows = run_campaign(policy, seed)
            all_runs.append((policy, seed, rows))
            if policy == "balanced" and seed == 29:
                print(f"\n=== 정책={policy} · seed={seed} ===")
                print(f"{'ST':>2} {'웨이브':>6} {'총물량':>7} {'면적':>6} {'클리어':>7} {'수익($)':>9} {'구매':>4} {'최대구매간격':>8}")
                for r in rows:
                    print(f"{r['stage']:>2} {r['waves']:>3}×{r['per_wave']:<4} {r['items']:>7,} {r['area']:>6.0f} "
                          f"{r['seconds']/60:>6.1f}분 {r['income']:>9,} {r['buys']:>4} {r['max_gap']:>7.0f}초")

    print("\n=== 정책별 캠페인 총 시간 ===")
    for policy, seed, rows in all_runs:
        total = sum(r["seconds"] for r in rows) / 60
        allg = []
        for r in rows:
            allg += r["buy_times"]
        med, p90, mx = gap_stats(allg)
        print(f"{policy:<9} seed={seed:<3} 총 {total:6.1f}분 · 구매 {sum(r['buys'] for r in rows):>4}회 "
              f"· 간격 중간 {med:4.0f}초 / p90 {p90:4.0f}초 / 최대 {mx:4.0f}초")

    print("\n=== 스테이지별 (balanced · seed 29) ===")
    print(f"{'ST':>2} {'웨이브':>7} {'총물량':>8} {'면적':>6} {'클리어(분)':>10} {'수익($)':>10} {'구매':>5}")
    base_rows = [rows for pol, sd, rows in all_runs if pol == "balanced" and sd == 29][0]
    for r in base_rows:
        print(f"{r['stage']:>2} {r['waves']:>3}×{r['per_wave']:<5} {r['items']:>8,} {r['area']:>6.0f} "
              f"{r['seconds']/60:>9.1f} {r['income']:>10,.0f} {r['buys']:>5}")

    totals = [sum(r["seconds"] for r in rows) / 60 for _, _, rows in all_runs]
    print(f"\n캠페인 총 시간: {min(totals):.1f} ~ {max(totals):.1f}분 (목표 210~270분)")
    print("검증:", "통과" if all(210 <= x <= 270 for x in totals) and not errs else "재조정 필요")


if __name__ == "__main__":
    main()
