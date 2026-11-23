using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        PlayingInRoom,
        TruckShop,
        RoomCompleted
    }

    /// <summary>
    /// 원룸 1칸 핵심 손맛 프로토타입 중앙 매니저.
    /// 악취 호흡(철수 후 진행도 유지), 90% 자동 완청, Before->After 조명 전환,
    /// 현장 강화(Z/X/C/V), 장비 3종, 복합 발각률(%) 및 정산 리뷰를 제어합니다.
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public Transform playerSpawnPoint;
        public Transform returnShelfTransform;
        public Light ceilingLight;
        public SecurityCameraController cctv;
        public ProceduralSfx sfx;

        [Header("Room 01 Config")]
        public string roomTitle = "Stage 1 · 반지하 자취 원룸";
        public string clientMemo = "취준하다 방이 이렇게 됐네요... 침대 밑 '엄마가 주신 금반지'만 꼭 선반에 놔주세요!";
        public int baseCleanReward = 200;
        public int lostItemTip = 70;
        public float baseStenchPerSec = 1.0f;
        public float autoCompleteThreshold = 90f;

        // Runtime state
        public GamePhase Phase { get; private set; } = GamePhase.PlayingInRoom;
        public int TryCount { get; private set; } = 1;
        public int Money { get; private set; } = 0;
        public float Breath { get; private set; } = 60f;
        public int BagUsed { get; private set; } = 0;

        // Z / X / C / V Quick Upgrade Levels
        public int LvSpeed { get; private set; }
        public int LvRange { get; private set; }
        public int LvBreath { get; private set; }
        public int LvBag { get; private set; }

        // Equipment Tiers (1 = T1)
        public int SuctionTier { get; private set; } = 1; // 1:맨손, 2:집게·봉투, 3:가정용 청소기
        public int StainTier { get; private set; } = 1;   // 1:마른 걸레, 2:세제 스프레이(CCTV 거품 가리기!)
        public int MaskTier { get; private set; } = 1;    // 1:마스크 없음, 2:일회용 마스크, 3:방독면
        public int StealthTier { get; private set; } = 0; // 은닉 업글 (0~5, 레벨당 -5% 감면)

        // Selected Augment (after room clear demo)
        public string ActiveAugmentName { get; private set; } = "없음";
        public bool HasAugmentAuto85 { get; private set; }
        public bool HasAugmentVentilation { get; private set; }
        public bool HasAugmentFakeReceipt { get; private set; }

        // Clean tracking
        private List<CleanableTarget> _cleanables = new List<CleanableTarget>();
        private List<TreasureTarget> _treasures = new List<TreasureTarget>();
        private float _totalCleanWeight;
        private float _cleanedWeight;
        private int _returnedCount;

        // Stealing tracking
        public int TotalTreasureValue { get; private set; }
        public int StolenValue { get; private set; }
        public int StolenCount { get; private set; }
        public int CctvWitnessCount { get; private set; }
        public bool StoleDesignatedLostItem { get; private set; }
        public bool ReturnedDesignatedLostItem { get; private set; }

        // Last Settlement Results
        public bool LastWasCaught { get; private set; }
        public int LastStarRating { get; private set; } = 5;
        public string LastReviewText { get; private set; } = "";
        public int LastNetProfit { get; private set; }
        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;
        private int _retreatReasonCode; // 0:수동, 1:호흡 한계, 2:가방 가득
        public string RetreatReasonText => _retreatReasonCode == 1
            ? "악취가 너무 심해 숨을 쉴 수 없어 트럭으로 철수했습니다! (치운 구역은 그대로 유지됩니다)"
            : _retreatReasonCode == 2
                ? "쓰레기 가방이 가득 차서 트럭으로 복귀했습니다! (비우고 정비 후 재투입하세요)"
                : "트럭으로 일시 복귀했습니다. 장비 정비 후 이어서 청소할 수 있습니다.";

        // Derived Stats
        public float MaxBreath => 60f + LvBreath * 12f;
        public int BagCapacity => 30 + LvBag * 10;
        public float CleanSpeedMultiplier => (1f + LvSpeed * 0.18f) * (SuctionTier == 1 ? 1.0f : SuctionTier == 2 ? 1.8f : 2.8f);
        public float CleanRange => 2.2f + LvRange * 0.22f;
        public float AreaRadius => 0.45f + LvRange * 0.14f;
        public float CleanPercent => _totalCleanWeight > 0.01f ? Mathf.Clamp01(_cleanedWeight / _totalCleanWeight) * 100f : 0f;
        public float TargetCompletePercent => HasAugmentAuto85 ? 85f : autoCompleteThreshold;
        public float StenchDrainMultiplier
        {
            get
            {
                float maskMult = MaskTier == 1 ? 1.0f : MaskTier == 2 ? 0.70f : 0.35f;
                if (HasAugmentVentilation && CleanPercent >= 50f) maskMult *= 0.5f;
                return maskMult;
            }
        }

        public float EffectiveDetectionRate => ComputeDetectionRate(StolenValue, StolenCount, CctvWitnessCount, StoleDesignatedLostItem);

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
        }

        private void Start()
        {
            CollectSceneObjects();
            Breath = MaxBreath;
            UpdateAtmosphereByCleanPercent();
            ShowToast("1일차 시작! 좌클릭으로 쓰레기를 치우고, Z/X/C/V로 즉시 강화해 보세요.");
        }

        private void CollectSceneObjects()
        {
            _cleanables.Clear();
            _treasures.Clear();
            _totalCleanWeight = 0f;
            _cleanedWeight = 0f;
            TotalTreasureValue = 0;

            foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
            {
                _cleanables.Add(c);
                _totalCleanWeight += c.cleanWeight;
            }

            foreach (var t in FindObjectsByType<TreasureTarget>(FindObjectsSortMode.None))
            {
                _treasures.Add(t);
                if (!t.isDesignatedLostItem) TotalTreasureValue += t.value;
            }
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f) ToastMessage = "";
            }

            if (Phase != GamePhase.PlayingInRoom) return;

            // 악취 호흡 게이지 감소 (치울수록 악취가 아주 미세하게 줄어들며 쾌감 제공)
            float roomFilthFactor = Mathf.Lerp(1.0f, 0.55f, CleanPercent / 100f);
            Breath -= baseStenchPerSec * StenchDrainMultiplier * roomFilthFactor * Time.deltaTime;

            if (Breath <= 0f)
            {
                Breath = 0f;
                EnterTruckShop(1);
            }
        }

        public float ComputeDetectionRate(int stolenVal, int stolenCnt, int witnessCnt, bool stoleDesignated)
        {
            if (stoleDesignated) return 100f;
            if (stolenCnt <= 0 || TotalTreasureValue <= 0) return 0f;

            float ratio = Mathf.Clamp01(stolenVal / (float)TotalTreasureValue);
            float baseVal = Mathf.Pow(ratio, 1.6f) * 75f;
            float countAdd = stolenCnt * 4f;
            float witnessAdd = witnessCnt * 35f;
            float reduction = StealthTier * 5f + (HasAugmentFakeReceipt ? 8f : 0f);

            float raw = baseVal + countAdd + witnessAdd - reduction;

            // 80% 이상 싹쓸이 구간은 절대 25% 미만으로 내려가지 않음
            if (ratio >= 0.80f)
            {
                return Mathf.Clamp(Mathf.Max(raw, 25f), 25f, 100f);
            }
            // 적정 슈킹 구간은 풀업 시 최소 5%까지 완화
            return Mathf.Clamp(raw, 5f, 100f);
        }

        public float PreviewDetectionIfSteal(TreasureTarget target, bool seenByCctv)
        {
            if (target == null) return EffectiveDetectionRate;
            if (target.isDesignatedLostItem) return 100f;
            return ComputeDetectionRate(
                StolenValue + target.value,
                StolenCount + 1,
                CctvWitnessCount + (seenByCctv ? 1 : 0),
                StoleDesignatedLostItem
            );
        }

        public bool CanPickUpMoreTrash(int bagCost)
        {
            return (BagUsed + bagCost) <= BagCapacity;
        }

        public void OnCleanableFinished(CleanableTarget target)
        {
            _cleanedWeight += target.cleanWeight;
            Money += target.rewardMoney;
            if (target.kind != CleanableKind.FloorStain)
            {
                BagUsed = Mathf.Min(BagCapacity, BagUsed + target.bagCost);
            }

            if (target.kind == CleanableKind.FloorStain) sfx.PlayScrub();
            else sfx.PlayPop(Mathf.Lerp(0.85f, 1.35f, CleanPercent / 100f));

            UpdateAtmosphereByCleanPercent();

            if (CleanPercent >= TargetCompletePercent)
            {
                TriggerAutoCompleteRoom();
                return;
            }

            if (BagUsed >= BagCapacity)
            {
                ShowToast("가방이 꽉 찼습니다! [V]로 가방을 늘리거나 [R]로 트럭에 비우고 오세요.");
            }
        }

        public void ReturnTreasure(TreasureTarget target)
        {
            if (target == null || target.State != TreasureState.HiddenInRoom) return;

            Transform slot = returnShelfTransform;
            Vector3 offset = Vector3.right * (_returnedCount * 0.24f - 0.35f);
            _returnedCount++;

            if (slot != null)
            {
                GameObject anchor = new GameObject("ShelfSlot");
                anchor.transform.position = slot.position + offset;
                target.MarkReturned(anchor.transform);
                Destroy(anchor);
            }
            else
            {
                target.MarkReturned(null);
            }

            sfx.PlayChime();
            if (target.isDesignatedLostItem)
            {
                ReturnedDesignatedLostItem = true;
                Money += lostItemTip;
                ShowToast($"지정 분실물 [{target.itemName}] 선반 보관 완료! 즉시 팁 +${lostItemTip}");
            }
            else
            {
                Money += Mathf.RoundToInt(target.value * 0.15f);
                ShowToast($"[{target.itemName}] 의뢰인 선반에 정직 보관! (소액 감사 팁 지급)");
            }
        }

        public void StealTreasure(TreasureTarget target, bool seenByCctv)
        {
            if (target == null || target.State != TreasureState.HiddenInRoom) return;

            target.MarkStolen();
            StolenCount++;
            StolenValue += target.value;
            Money += target.value;

            if (target.isDesignatedLostItem)
            {
                StoleDesignatedLostItem = true;
                sfx.PlayAlert();
                ShowToast($"경고! 지정 분실물 [{target.itemName}] 슈킹 (+${target.value}) — 발각률 100%!");
            }
            else if (seenByCctv)
            {
                CctvWitnessCount++;
                sfx.PlayAlert();
                ShowToast($"CCTV에 찍혔습니다!! [{target.itemName}] +${target.value} (목격 +35%!)");
            }
            else
            {
                sfx.PlaySteal();
                ShowToast($"[{target.itemName}] 몰래 슈킹 성공! +${target.value} (예상 발각률 {EffectiveDetectionRate:0}%)");
            }
        }

        // Quick Upgrades (Z/X/C/V)
        public int GetQuickCost(int baseCost, int level) => Mathf.RoundToInt(baseCost * Mathf.Pow(1.85f, level));
        public int CostSpeed => GetQuickCost(60, LvSpeed);
        public int CostRange => GetQuickCost(80, LvRange);
        public int CostBreath => GetQuickCost(70, LvBreath);
        public int CostBag => GetQuickCost(60, LvBag);

        public bool TryUpgradeSpeed()
        {
            if (LvSpeed >= 10 || Money < CostSpeed) return false;
            Money -= CostSpeed; LvSpeed++; sfx.PlayUpgrade();
            ShowToast($"[Z] 흡입 속도 Lv.{LvSpeed} 강화! ({CleanSpeedMultiplier:0.0}x)");
            return true;
        }

        public bool TryUpgradeRange()
        {
            if (LvRange >= 10 || Money < CostRange) return false;
            Money -= CostRange; LvRange++; sfx.PlayUpgrade();
            ShowToast($"[X] 흡입 범위 Lv.{LvRange} 강화! ({CleanRange:0.1}m)");
            return true;
        }

        public bool TryUpgradeBreath()
        {
            if (LvBreath >= 10 || Money < CostBreath) return false;
            Money -= CostBreath; LvBreath++; Breath = Mathf.Min(MaxBreath, Breath + 15f); sfx.PlayUpgrade();
            ShowToast($"[C] 최대 호흡 Lv.{LvBreath} 강화! (최대 {MaxBreath:0}초)");
            return true;
        }

        public bool TryUpgradeBag()
        {
            if (LvBag >= 10 || Money < CostBag) return false;
            Money -= CostBag; LvBag++; sfx.PlayUpgrade();
            ShowToast($"[V] 가방 용량 Lv.{LvBag} 강화! ({BagCapacity}칸)");
            return true;
        }

        // Truck Shop Tier Upgrades
        public bool BuySuctionTier()
        {
            int cost = SuctionTier == 1 ? 100 : SuctionTier == 2 ? 320 : 999999;
            if (SuctionTier >= 3 || Money < cost) return false;
            Money -= cost; SuctionTier++; sfx.PlayUpgrade();
            return true;
        }

        public bool BuyStainTier()
        {
            int cost = StainTier == 1 ? 220 : 999999;
            if (StainTier >= 2 || Money < cost) return false;
            Money -= cost; StainTier++; sfx.PlayUpgrade();
            return true;
        }

        public bool BuyMaskTier()
        {
            int cost = MaskTier == 1 ? 80 : MaskTier == 2 ? 350 : 999999;
            if (MaskTier >= 3 || Money < cost) return false;
            Money -= cost; MaskTier++; sfx.PlayUpgrade();
            return true;
        }

        public bool BuyStealthTier()
        {
            int cost = 120 + StealthTier * 140;
            if (StealthTier >= 5 || Money < cost) return false;
            Money -= cost; StealthTier++; sfx.PlayUpgrade();
            return true;
        }

        public void EnterTruckShop(int reasonCode)
        {
            if (Phase != GamePhase.PlayingInRoom) return;
            _retreatReasonCode = reasonCode;
            Phase = GamePhase.TruckShop;
            if (reasonCode == 1) sfx.PlayAlert();
            UnlockCursor(true);
        }

        public void ReenterRoomFromTruck()
        {
            TryCount++;
            Breath = MaxBreath;
            BagUsed = 0;
            if (cctv != null) cctv.ResetCamera();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            Phase = GamePhase.PlayingInRoom;
            UnlockCursor(false);
            ShowToast($"{TryCount}일차 재투입! (이전 청소 진행도 {CleanPercent:0}% 유지됨)");
        }

        private void TriggerAutoCompleteRoom()
        {
            // 남은 잔쓰레기 자동 정화 (90% QoL)
            foreach (var c in _cleanables)
            {
                if (!c.IsCleaned) c.AutoCleanInstant();
            }
            _cleanedWeight = _totalCleanWeight;
            UpdateAtmosphereByCleanPercent();

            // 발각 판정
            float det = EffectiveDetectionRate;
            LastWasCaught = StolenCount > 0 && (det >= 100f || Random.Range(0f, 100f) < det);

            if (LastWasCaught)
            {
                int penalty = StolenValue + Mathf.RoundToInt(baseCleanReward * 0.5f);
                Money = Mathf.Max(0, Money - penalty);
                LastNetProfit = Mathf.RoundToInt(baseCleanReward * 0.5f) - StolenValue;
                LastStarRating = 1;
                LastReviewText = StoleDesignatedLostItem
                    ? "\"청소는 잘해주셨는데 침대 밑 엄마 금반지 어디 갔죠?! 바로 경찰 부릅니다.\""
                    : "\"방은 깨끗해졌는데 CCTV 보니까 제 물건 챙기셨네요? 당장 신고합니다.\"";
                sfx.PlayAlert();
            }
            else if (StolenCount == 0)
            {
                Money += baseCleanReward;
                LastNetProfit = baseCleanReward;
                LastStarRating = 5;
                LastReviewText = "\"와 진짜 새 집이 됐어요!! 잃어버렸던 물건도 그대로 찾아주시고 너무 감사합니다 ㅠㅠ\"";
                sfx.PlayFanfare();
            }
            else
            {
                Money += baseCleanReward;
                LastNetProfit = baseCleanReward + StolenValue;
                LastStarRating = 3;
                LastReviewText = "\"방은 진짜 깨끗해졌는데... 원래 여기 있던 내 비상금이랑 물건 어디 갔지? 기분 탓인가?\"";
                sfx.PlayFanfare();
            }

            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void SelectDemoAugmentAndReplay(int augmentChoice)
        {
            if (augmentChoice == 1) { HasAugmentAuto85 = true; ActiveAugmentName = "마무리 달인 (85% 완청)"; }
            else if (augmentChoice == 2) { HasAugmentVentilation = true; ActiveAugmentName = "쾌적한 환기 (50%+ 악취 반감)"; }
            else if (augmentChoice == 3) { HasAugmentFakeReceipt = true; ActiveAugmentName = "가짜 영수증 (발각률 -8%)"; }

            // 방만 리스폰(업그레이드·장비·돈·증강은 유지해 재방문/성장 체감 테스트!)
            foreach (var c in _cleanables) c.ResetForNewRun();
            foreach (var t in _treasures) t.ResetForNewRun();
            if (cctv != null) cctv.ResetCamera();

            _cleanedWeight = 0f;
            _returnedCount = 0;
            StolenValue = 0;
            StolenCount = 0;
            CctvWitnessCount = 0;
            StoleDesignatedLostItem = false;
            ReturnedDesignatedLostItem = false;
            TryCount = 1;
            Breath = MaxBreath;
            BagUsed = 0;

            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }

            UpdateAtmosphereByCleanPercent();
            Phase = GamePhase.PlayingInRoom;
            UnlockCursor(false);
            ShowToast($"증강 [{ActiveAugmentName}] 장착! 단골 원룸 재발 의뢰 시작!");
        }

        private void UpdateAtmosphereByCleanPercent()
        {
            float t = Mathf.Clamp01(CleanPercent / 95f);
            RenderSettings.fogDensity = Mathf.Lerp(0.042f, 0.004f, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.18f, 0.16f, 0.12f), new Color(0.75f, 0.78f, 0.80f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.24f, 0.22f, 0.18f), new Color(0.58f, 0.59f, 0.60f), t);

            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 0.99f, 0.95f), t);
                ceilingLight.intensity = Mathf.Lerp(11f, 18f, t);
            }
        }

        public void ShowToast(string msg)
        {
            ToastMessage = msg;
            _toastTimer = 4.0f;
        }

        private static void UnlockCursor(bool unlock)
        {
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
