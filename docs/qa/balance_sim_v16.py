#!/usr/bin/env python3
"""
TrashCleaner3D v1.6 밸런스 QA 시뮬레이터 (Stage 1~7 × 정책 3종 × 시드 3개 = 9회).

검증 항목
 1) 초반 1~2분에 성장이 멈추는 구간(정체)이 없는지  → 30초 구간별 구매/처리량
 2) 스테이지마다 강화를 2~3회 이상 찍을 수 있는지
 3) 큰 봉투·얼룩(신규 활성화) + 차량 편의 해금(마당/문앞/80% 원격)이 클리어 시간·수익에 주는 영향

GamePrototypeManager / InstancedTrashSwarm / StagePropLayout / UpgradeTreeV16 의 수치를 그대로 옮겼고,
이동·조준 손실만 '효율 계수'로 근사한다(에디터 없이 코드 수치만으로 검증하는 한계).
"""

import math
import random
from dataclasses import dataclass, field

STAGE_MULT = lambda s: 1.0 + (s - 1) * 0.35
STAGE_ITEMS = {1: 1650, 2: 1800, 3: 2200, 4: 3000, 5: 4200, 6: 2400, 7: 6000}
STAGE_AREA = {1: 9.36 * 7.8, 2: 4.2 * 6.8, 3: 5.5 * 5.0, 4: 7.2 * 5.8, 5: 8.5 * 7.0, 6: 4.2 * 3.6, 7: 10 * 8.0}

PROPS = {
    1: dict(piles=8, stains=5, pile_hp=9.0, stain_hp=2.6, stain_reward=18),
    2: dict(piles=26, stains=4, pile_hp=10.0, stain_hp=2.6, stain_reward=22),
    3: dict(piles=8, stains=16, pile_hp=10.0, stain_hp=2.1, stain_reward=24),
    4: dict(piles=16, stains=8, pile_hp=11.0, stain_hp=2.4, stain_reward=26),
    5: dict(piles=18, stains=8, pile_hp=12.0, stain_hp=2.5, stain_reward=30),
    6: dict(piles=12, stains=6, pile_hp=12.0, stain_hp=2.5, stain_reward=30),
    7: dict(piles=24, stains=10, pile_hp=13.0, stain_hp=2.6, stain_reward=34),
}

HOLD_CURVE = [1.00, 0.84, 0.70, 0.56, 0.42, 0.33, 0.25, 0.19, 0.14, 0.10, 0.07]
BIN_CAPS = [35, 50, 70, 95, 130, 180, 250, 340, 480, 680, 980, 1500]

# (maxLv, base, growth, prereq, prereqLv) — v1.6 튜닝 수치
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

# id, maxLv, base, growth, prereq, prereqLv, effect, perLevel
V16 = [
    ("grip", 5, 10, 1.55, "hold1", 1, "hold", 0.015),
    ("reach", 5, 11, 1.55, "rad1", 1, "radius", 0.012),
    ("focus", 4, 120, 1.80, "grip", 2, "hold", 0.018),
    ("wrist", 4, 238, 1.80, "rad2", 1, "radius", 0.016),
    ("multi", 3, 672, 1.95, "focus", 2, "grab", 1.0),
    ("glove", 5, 364, 1.85, "wrist", 2, "fist", 0.03),
    ("nozzle", 4, 1710, 1.86, "vacuum", 1, "radius", 0.020),
    ("swirl", 3, 1805, 1.92, "hold3", 1, "blackhole", 0.18),
    ("spark", 4, 2280, 1.88, "swirl", 1, "fevergain", 0.06),
    ("scale", 5, 10, 1.55, "bin1", 1, "price", 0.012),
    ("liner", 5, 12, 1.58, "bin1", 1, "bincap", 0.015),
    ("sort", 4, 252, 1.82, "scale", 2, "price", 0.016),
    ("wheels", 4, 480, 1.84, "bin2", 1, "bulk", 0.02),
    ("receipt", 4, 672, 1.84, "price2", 1, "settle", 0.012),
    ("tip", 4, 800, 1.84, "combo", 2, "burst", 10.0),
    ("cashback", 3, 2850, 1.95, "receipt", 2, "price", 0.020),
    ("ledger", 3, 2565, 1.92, "wheels", 2, "bulk", 0.025),
    ("sole", 5, 10, 1.55, "speed1", 1, "move", 0.010),
    ("lace", 4, 120, 1.80, "speed2", 1, "move", 0.012),
    ("filter", 5, 11, 1.58, "mask1", 1, "maskf", 0.02),
    ("oxygen", 4, 308, 1.82, "mask1", 2, "oxygen", 0.025),
    ("knees", 4, 336, 1.84, "speed2", 1, "scrub", 0.03),
    ("soap", 3, 896, 1.90, "scrub", 1, "scrub", 0.04),
    ("vent", 4, 480, 1.84, "filter", 3, "feverhold", 0.05),
    ("calm", 3, 992, 1.92, "vent", 2, "feverhold", 0.06),
    ("knuckle", 5, 11, 1.58, "fist1", 1, "fist", 0.03),
    ("brush", 4, 480, 1.84, "rspd1", 1, "roomba", 0.015),
    ("route", 3, 832, 1.90, "rspd2", 1, "roomba", 0.015),
    ("hammer", 3, 896, 1.90, "fist2", 1, "fist", 0.04),
    ("sensor", 3, 576, 1.86, "roomba", 2, "roomba", 0.01),
    ("dock", 3, 1805, 1.92, "sensor", 2, "roomba", 0.02),
    ("aura", 4, 1520, 1.86, "knuckle", 3, "fevergain", 0.05),
    ("overdrive", 3, 2660, 1.95, "dock", 2, "settle", 0.015),
]
V16_MAP = {v[0]: v for v in V16}
ALL_IDS = list(CORE.keys()) + [v[0] for v in V16]


class Player:
    def __init__(self):
        self.lv = {k: 0 for k in ALL_IDS}
        self.money = 0
        self.stage = 1

    def has(self, nid):
        return self.lv.get(nid, 0) > 0

    def maxlv(self, nid):
        return CORE[nid][0] if nid in CORE else V16_MAP[nid][1]

    def unlocked(self, nid):
        if nid in CORE:
            p, pl = CORE[nid][3], CORE[nid][4]
        else:
            row = V16_MAP[nid]
            p, pl = row[4], row[5]
        return True if p is None else self.lv.get(p, 0) >= pl

    def cost(self, nid):
        if nid == "roomba":
            return ROOMBA_COST[min(self.lv[nid], 2)]
        if nid in CORE:
            mx, base, g, _, _ = CORE[nid]
            return base if mx == 1 else round(base * (g ** self.lv[nid]))
        row = V16_MAP[nid]
        return round(row[2] * (row[3] ** self.lv[nid]))

    def buy(self, nid):
        c = self.cost(nid)
        if c > self.money:
            return False
        self.money -= c
        self.lv[nid] += 1
        return True

    def v16sum(self, effect):
        return sum(row[7] * self.lv[row[0]] for row in V16 if row[6] == effect)

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
    def tool_tier(self): return 2 if self.has("vacuum") else 1 if self.has("broom") else 0

    @property
    def vehicle_stage(self): return 3 if self.has("truck1t") else 2 if self.has("damas") else 1 if self.has("cart") else 0

    @property
    def hold_dur(self):
        base = HOLD_CURVE[min(self.total_hold, len(HOLD_CURVE) - 1)]
        if self.has("broom"): base *= 0.86
        if self.has("vacuum"): base *= 0.75
        fever_bonus = (0.08 + self.lv["fever"] * 0.16) * 0.6 if self.lv["fever"] else 0.0
        spd = (1 + fever_bonus) * (1 + self.v16sum("hold")) * self.oxygen_mult
        return max(0.045, min(1.35, base / spd))

    @property
    def sweep_radius(self):
        base = 0.62 if self.has("vacuum") else 0.34 if self.has("broom") else 0.15
        return (base + self.total_rad * 0.11) * (1 + self.v16sum("radius"))

    @property
    def grab_count(self):
        g = round(self.v16sum("grab"))
        if self.has("vacuum"): return 8 + self.lv["rad2"] * 2 + self.lv["hold3"] * 2 + g
        if self.has("broom"): return 3 + self.lv["rad1"] // 2 + self.lv["rad2"] + g
        return 1 + self.lv["rad1"] // 3 + g

    @property
    def bin_cap(self):
        cap = BIN_CAPS[min(self.total_bin, len(BIN_CAPS) - 1)] * (2 if self.has("vacuum") else 1)
        return round(cap * (1 + self.v16sum("bincap")))

    @property
    def price_mult(self):
        return (1 + self.total_price * 0.18) * STAGE_MULT(self.stage) * (1 + self.v16sum("price"))

    @property
    def oxygen_mult(self): return self.oxygen_mult_at(22.0)

    def oxygen_mult_at(self, stench):
        zone = max(0.0, min(1.0, (45.0 - stench) / 20.0))
        boost = zone * min(1.0, self.total_mask / 5.0)
        return 1 + (0.15 + self.v16sum("oxygen")) * boost

    @property
    def move_speed(self): return 3.85 * (1 + self.total_speed * 0.10) * (1 + self.v16sum("move")) * self.oxygen_mult

    @property
    def fist_damage(self):
        return (1.35 + self.total_fist * 0.95 + (1.6 if self.has("vacuum") else 0)) * (1 + self.v16sum("fist"))

    @property
    def scrub_mult(self):
        return (2.4 if self.has("mop") else 1.0) * (1 + self.lv["scrub"] * 0.38) * (1 + self.v16sum("scrub"))

    @property
    def vehicle_mult(self): return [1.0, 1.2, 1.45, 1.8][self.vehicle_stage] + self.v16sum("bulk")

    @property
    def roomba_interval(self):
        return max(0.48, (1.0 - self.total_roomba_spd * 0.085) * (1 - min(0.30, self.v16sum("roomba"))))

    @property
    def roomba_rate(self):
        if self.lv["roomba"] == 0: return 0.0
        raw = self.lv["roomba"] / self.roomba_interval
        return min(raw, (self.grab_count / self.hold_dur) * 0.25)


@dataclass
class StageResult:
    stage: int
    seconds: float
    money_earned: int
    buys: int
    buys_first_60: int
    buys_first_120: int
    money_at_120: int
    end_money: int
    buys_by_quarter: list = field(default_factory=list)


def buy_policy(player, policy, max_buys=3):
    """정책 3종(모두 실제 플레이어가 쓸 법한 소비 습관): 
       novice = 가장 싼 것부터 2개씩 · balanced = 가장 싼 것부터 3개씩 · longterm = 비싼 노드 우선 2개씩"""
    bought = 0
    limit = 2 if policy in ("novice", "longterm") else 3
    while bought < max(1, min(max_buys, limit)):
        cands = []
        for nid in ALL_IDS:
            if not player.unlocked(nid) or player.lv[nid] >= player.maxlv(nid):
                continue
            c = player.cost(nid)
            if c <= player.money:
                cands.append((c, nid))
        if not cands:
            break
        if policy == "longterm":
            c, nid = max(cands, key=lambda t: t[0])
        else:
            cands.sort()
            c, nid = cands[0]
        player.buy(nid)
        bought += 1
    return bought


def simulate_stage(player, stage, policy, seed, dt=0.5):
    rng = random.Random(seed)
    player.stage = stage
    plan = PROPS[stage]
    items = STAGE_ITEMS[stage]
    area = STAGE_AREA[stage]

    t = 0.0
    trash_clean = 0.0
    pending = 0.0
    pending_value = 0.0
    income_total = 0.0
    combo = 0
    combo_timer = 0.0
    buys = buys60 = buys120 = 0
    money_at_120 = None
    q_buys = [0, 0, 0, 0]
    pile_index = 1
    piles_left = plan["piles"]
    next_pile_at = items / (plan["piles"] + 1)
    last_buy_t = -99.0

    eff = 0.66 if stage <= 3 else 0.60 if stage <= 5 else 0.56 if stage == 6 else 0.52
    oxygen = player.oxygen_mult

    while trash_clean < items and t < 3 * 3600:
        remain = items - trash_clean
        density = max(0.5, remain / area)
        sweep_area = 2 * player.sweep_radius * player.move_speed * oxygen
        player_rate = (player.grab_count / player.hold_dur) * oxygen
        rate = min(player_rate, sweep_area * density) * eff + player.roomba_rate

        step = min(dt, remain / max(0.001, rate))
        got = min(remain, rate * step)
        trash_clean += got
        pending += got
        pending_value += got * 2 * player.price_mult
        t += step

        # 큰 봉투를 만나면 타격 + 연쇄 흡입 (진행 중 정해진 지점마다)
        if piles_left > 0 and trash_clean >= next_pile_at:
            hits = math.ceil(plan["pile_hp"] / max(0.5, player.fist_damage))
            t += hits * 0.30 + 1.2
            piles_left -= 1
            pile_index += 1
            next_pile_at = items * pile_index / (plan["piles"] + 1)
            burst = (20 + stage * 2 + player.v16sum("burst")) * STAGE_MULT(stage)
            player.money += burst
            income_total += burst
            chain = int(min(28, max(10, 10 + player.v16sum("blackhole") * 40)))
            got2 = min(chain, items - trash_clean)
            trash_clean += got2
            pending += got2
            pending_value += got2 * 2 * player.price_mult

            if pending >= player.bin_cap:   # 봉투 직후에도 정산 타이밍
                pass

        # 수거통 정산
        if pending >= player.bin_cap:
            combo = min(5, combo + 1) if combo_timer > 0 else 1
            combo_timer = 6.0
            mult = (1 + (combo - 1) * 0.2) * player.vehicle_mult * (1 + player.v16sum("settle"))
            gain = pending_value * mult
            player.money += gain
            income_total += gain
            pending = 0.0
            pending_value = 0.0
            t += [4.0, 1.4, 0.7, 0.0][player.vehicle_stage]

        if combo_timer > 0:
            combo_timer -= step

        # 2초마다 최대 3개까지 구매 (정책별)
        if t - last_buy_t >= 2.0:
            last_buy_t = t
            n = buy_policy(player, policy)
            if n:
                buys += n
                if t <= 60: buys60 += n
                if t <= 120: buys120 += n
                q_buys[min(3, int(t // 60))] += n
                if money_at_120 is None and t >= 120: money_at_120 = player.money

    # 2단계: 바닥 얼룩
    stain_power = (0.55 + 2.2) * player.scrub_mult
    for _ in range(plan["stains"]):
        t += plan["stain_hp"] / max(0.05, stain_power) + 2.4
    stain_income = plan["stains"] * plan["stain_reward"] * player.price_mult
    player.money += stain_income
    income_total += stain_income

    if pending > 0:
        gain = pending_value * player.vehicle_mult * (1 + player.v16sum("settle"))
        player.money += gain
        income_total += gain
        pending = 0.0
        pending_value = 0.0

    clear_bonus = 280 * STAGE_MULT(stage)
    player.money += clear_bonus
    income_total += clear_bonus
    if money_at_120 is None:
        money_at_120 = player.money

    return StageResult(
        stage=stage, seconds=t, money_earned=int(income_total), buys=buys,
        buys_first_60=buys60, buys_first_120=buys120,
        money_at_120=int(money_at_120), end_money=int(player.money), buys_by_quarter=q_buys,
    )


def run_campaign(policy, seed):
    p = Player()
    return p, [simulate_stage(p, st, policy, seed * 100 + st) for st in range(1, 8)]


def table(rows, title):
    print(f"\n=== {title} ===")
    print(f"{'ST':>2} {'클리어':>7} {'수익($)':>8} {'구매':>4} {'60초':>4} {'120초':>5} {'2분현금':>8} {'분기구매':>12}")
    for r in rows:
        print(f"{r.stage:>2} {r.seconds/60:>6.1f}분 {r.money_earned:>8,} {r.buys:>4} {r.buys_first_60:>4} {r.buys_first_120:>5} {r.money_at_120:>8,} {str(r.buys_by_quarter):>12}")


def trace_stage1(policy="balanced", seed=29, step_report=30):
    """ST1 초반 2~3분을 30초 간격으로 찍어 '정체 구간'이 없는지 확인한다."""
    p, res = run_campaign(policy, seed)
    return res[0]


if __name__ == "__main__":
    runs = []
    for policy in ("novice", "balanced", "longterm"):
        for seed in (11, 29, 47):
            p, res = run_campaign(policy, seed)
            runs.append((policy, seed, res))
            if policy == "balanced" and seed == 29:
                table(res, f"정책={policy} · seed={seed}")

    print("\n=== 9회 요약 ===")
    for policy, seed, res in runs:
        total = sum(r.seconds for r in res) / 60
        min_b120 = min(r.buys_first_120 for r in res)
        late_zero = sum(1 for r in res[3:] if r.buys == 0)
        print(f"{policy:<9} seed={seed:<3} 총 {total:5.1f}분 · 최소 2분구매 {min_b120}회 · ST4~7 무구매 {late_zero}")

    print("\n=== 스테이지별 평균 (9회) ===")
    print(f"{'ST':>2} {'클리어(분)':>9} {'수익($)':>9} {'구매':>5} {'2분구매':>7}")
    for st in range(7):
        secs = [r[st].seconds / 60 for _, _, res in runs for r in [res]]
        money = [r[st].money_earned for _, _, res in runs for r in [res]]
        buys = [r[st].buys for _, _, res in runs for r in [res]]
        b120 = [r[st].buys_first_120 for _, _, res in runs for r in [res]]
        print(f"{st+1:>2} {sum(secs)/len(secs):>9.1f} {sum(money)//len(money):>9,} {sum(buys)/len(buys):>5.1f} {sum(b120)/len(b120):>7.1f}")

    print("\n=== ST1 초반 30초 간격 추적 (balanced) ===")
    p = Player()
    rng = random.Random(1)
    # simulate_stage 를 30초 단위로 쪼개 관찰하기 위한 간이 추적
    player = Player()
    player.stage = 1
    items = STAGE_ITEMS[1]
    plan = PROPS[1]
    area = STAGE_AREA[1]
    t = trash = pending = pv = 0.0
    buys_total = 0
    last_buy = -99.0
    piles = plan["piles"]
    idx = 1
    next_pile = items / (piles + 1)
    eff = 0.66
    next_report = 30.0
    print(f"{'t':>5} {'처리량/초':>8} {'홀드':>6} {'동시':>4} {'현금':>7} {'누적구매':>7}")
    while trash < items and t < 400:
        remain = items - trash
        density = max(0.5, remain / area)
        rate = min(player.grab_count / player.hold_dur,
                   2 * player.sweep_radius * player.move_speed * player.oxygen_mult * density) * eff + player.roomba_rate
        step = min(0.5, remain / max(0.001, rate))
        got = rate * step
        trash += got
        pending += got
        pv += got * 2 * player.price_mult
        t += step
        if piles > 0 and trash >= next_pile:
            import math as _m
            t += _m.ceil(plan["pile_hp"] / max(0.5, player.fist_damage)) * 0.3 + 1.2
            piles -= 1
            idx += 1
            next_pile = items * idx / (plan["piles"] + 1)
            player.money += (20 + 2) * 1.0
        if pending >= player.bin_cap:
            player.money += pv * player.vehicle_mult * (1 + player.v16sum("settle"))
            pending = pv = 0.0
            t += 4.0
        if t - last_buy >= 2.0:
            last_buy = t
            buys_total += buy_policy(player, "balanced")
        if t >= next_report:
            print(f"{t:5.0f} {rate:8.2f} {player.hold_dur:6.2f} {player.grab_count:4d} {player.money:7.0f} {buys_total:7d}")
            next_report += 30.0
            if t >= 300: break
