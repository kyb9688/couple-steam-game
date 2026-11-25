using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        Phase1_TrashRemoval,
        Phase2_FloorMopping,
        TreasureDecision,
        MindMapUpgrade,
        SettingsMenu,
        RoomCompleted
    }

    public enum TreasureRarity { Common, Rare, Legendary }

    [System.Serializable]
    public class DiscoveredTreasure
    {
        public string itemName;
        public int value;
        public TreasureRarity rarity;
        public bool isMentionedByClient;
        public float baseSuspicionGain;
        public BuriedTreasureObject sourceObj;
    }

    public class FloatingWorldText
    {
        public Vector3 worldPos;
        public string text;
        public Color color;
        public float age;
        public float duration;
    }

    /// <summary>
    /// TrashCleaner3D v0.8 중앙 매니저:
    /// - 클릭 연타 제거 & 3단계 도구 진화(맨손 → 집게/빗자루 → 진공청소기) + 연속 흡입 피버 게이지
    /// - 쓰레기 지층 발굴(BuriedTreasureObject) + 의뢰인 코믹 힌트 + 이원화 재화($ / ★신뢰도 / ♦암시장)
    /// - 확정 발각 100% 의심 게이지(억까 배제) + 30/60/90% 자연 세탁 + 100% 강제 퇴거 정산(게임오버 없음)
    /// - 핵심 룰 변형 기믹 4종:
    ///   1) 의심 80~95% '아드레날린(스릴)' 구간 (처리속도·수익 ×2 / ×2.5)
    ///   2) 동종 연속 5개 자동 압축 (봉투 0칸 소모 + 수익 ×2)
    ///   3) 80% 조기 정산([K]) & 막판 10% 잭팟(수익 ×4 / ×5)
    ///   4) 봉투 150% 과적 도박 (이속↓·벽 충돌 시 폭발, 이중 바닥 가방으로 방어)
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public InstancedTrashSwarm trashSwarm;
        public Transform playerSpawnPoint;
        public Transform truckBedGoal;
        public GameObject[] vehicleStageObjects; // 0:쓰레기통, 1:리어카, 2:다마스, 3:1톤 트럭
        public Transform truckStackedParent;
        public Light ceilingLight;
        public ProceduralSfx sfx;
        public GameObject thrownBagPrefab;

        [Header("Room 01 Config")]
        public int baseCleanReward = 450;
        public string clientHintText = "와이프 몰래 매트리스 옆에 숨긴 비상금 있는데 나오면 꼭 알려주세요 ㅎㅎ";

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        // ===== 이원화 재화 3축 =====
        public int Money { get; private set; } = 0;            // $ 현금 (기본 도구·운반·생존)
        public int TrustTokens { get; private set; } = 0;      // ★ 신뢰도 토큰 (성실 전달 보상 → VIP·탐지기·이중가방)
        public int BlackMarketPts { get; private set; } = 0;   // ♦ 암시장 포인트 (슈킹 보상 → 감정조작·회피·아드레날린)

        // ===== 사용자 설정 ([ESC]) =====
        public float MouseSensitivity { get; private set; } = 0.14f;
        public float MasterVolume { get; private set; } = 0.85f;

        // ===== 악취 누적 =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; }
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public bool IsTooSuffocatedToWork => StenchStage >= 4 && !IsOutsideVentilating;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.44f : StenchStage == 3 ? 0.58f : StenchStage == 2 ? 0.72f : StenchStage == 1 ? 0.84f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.0f : StenchStage == 3 ? 0.55f : StenchStage == 2 ? 0.75f : 1.0f;
        public float StenchGainPerSec => 3.4f * (1f - LvMask * 0.16f);

        // ===== 의심 게이지 (0~100% 확정 발각 · 80~95% 아드레날린 스릴 구간!) =====
        public float SuspicionPercent { get; private set; } = 0f;
        public bool IsAdrenalineZone => SuspicionPercent >= 80f && SuspicionPercent < 100f;
        public float AdrenalineMult => IsAdrenalineZone ? (HasAdrenalineAmp ? 2.5f : 2.0f) : 1.0f;
        public float AdrenalineSpeedMult => IsAdrenalineZone ? 2.0f : 1.0f;
        private bool _washed30, _washed60, _washed90;

        // ===== 도구 진화 (0:맨손/고무장갑 → 1:집게/빗자루 → 2:진공청소기) =====
        public int ToolTier { get; private set; } = 0;
        public bool HasVacuumUnlocked => ToolTier >= 2;
        public string CurrentToolName => ToolTier == 0 ? "맨손·고무장갑" : ToolTier == 1 ? "집게·빗자루" : "업소용 진공청소기";

        // ===== 연속 흡입 피버 게이지 =====
        public float ContinuousFever01 { get; private set; } = 0f;
        private float _feverDecayDelay = 0f;
        public bool IsFeverHot => ContinuousFever01 >= 0.75f;
        public float FeverRadiusBonus => HasFeverEngine ? ContinuousFever01 * 0.35f : ContinuousFever01 * 0.08f;
        public float FeverSpeedBonus => HasFeverEngine ? ContinuousFever01 * 0.65f : ContinuousFever01 * 0.15f;
        public float FeverPriceMult => (HasFeverEngine && IsFeverHot) ? 1.45f : 1.0f;

        // ===== 동종 연속 압축 (같은 종류 5개 연속 → 봉투 0칸 + 수익 ×2!) =====
        public const int CompressionStreakTarget = 5;
        public int StreakTypeIdx { get; private set; } = -1;
        public string StreakTypeLabel { get; private set; } = "";
        public int StreakCount { get; private set; } = 0;
        public int TotalCompressionsThisRoom { get; private set; } = 0;

        // ===== 봉투 & 150% 과적 도박 =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly float[] FistDmgByLv = { 1.2f, 2.2f, 3.6f, 5.4f, 10.0f };
        private static readonly string[] VehicleNames = { "공공 쓰레기통", "리어카", "다마스", "1톤 트럭" };

        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (ToolTier >= 2 ? 2 : 1);
        public int OverfillCapacity => Mathf.RoundToInt(HandBagCapacity * 1.5f);
        public float BagFillRatio => HandBagCapacity > 0 ? CollectingCount / (float)HandBagCapacity : 0f;
        public bool IsBagFullReady => CollectingCount >= HandBagCapacity;
        public bool IsBagOverfilled => CollectingCount > HandBagCapacity;
        public bool IsBagAtMaxOverfill => CollectingCount >= OverfillCapacity;
        public bool CanTieAndThrowCollectingBag => !HasCarriedBag && (CollectingCount >= HandBagCapacity || (Phase1Cleaned >= Phase1Total && CollectingCount > 0));
        private bool _notified100Percent;

        // 손에 묶어 든 완성 봉지 1개
        public bool HasCarriedBag { get; private set; } = false;
        public int CarriedBagCount { get; private set; } = 0;
        public int CarriedBagValue { get; private set; } = 0;
        public bool CanPickUpDroppedBag => !HasCarriedBag;

        // ===== 연속 투하 콤보 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;
        public float MaxComboWindow => 5.0f + LvComboTime * 2.5f;

        // ===== 3D 로봇청소기 & 적층 & 월드 팝업 =====
        private readonly List<AutoRoombaCleaner> _roombas = new List<AutoRoombaCleaner>();
        private int _stackedBagsInTruck = 0;
        private readonly List<GameObject> _stackedVisuals = new List<GameObject>();
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 스킬트리 레벨 & 특수 해금 =====
        // 1) 좌상 (도구·흡입·피버·자동화 — $)
        public int LvSweepRadius { get; private set; }      // 0~5
        public bool HasFeverEngine { get; private set; }    // 연속 흡입 피버 증폭
        public int LvCrit { get; private set; }             // 0~4
        public int LvChainPop { get; private set; }         // 0~3
        public int LvRoomba { get; private set; }           // 0~3

        // 2) 우상 (운반·과적·투척·경제 — $)
        public int LvBagSize { get; private set; }          // 0~4
        public int LvThrowPower { get; private set; }       // 0~4
        public int LvTrashPrice { get; private set; }       // 0~5
        public int LvTruckSize { get; private set; }        // 0~3
        public int LvComboTime { get; private set; }        // 0~3
        public bool HasTruckMagnet { get; private set; }

        // 3) 좌하 (생존·걸레질 — $ / 성실 루트 특수 해금 — ★)
        public int LvMask { get; private set; }             // 0~5
        public int LvMoveSpeed { get; private set; }        // 0~4
        public bool HasMopUnlocked { get; private set; }
        public int LvScrubPower { get; private set; }       // 0~3
        public bool HasVipContract { get; private set; }    // ★1: VIP 고액 의뢰 (전체 수익·보너스 ×1.5, 막판 10% ×5)
        public bool HasTreasureDetector { get; private set; } // ★1: 지층 보물 X-Ray 탐지기
        public bool HasDoubleBottomBag { get; private set; }  // ★2: 이중 바닥 가방 (150% 과적 벽충돌 폭발 면역!)

        // 4) 우하 (파괴·탐지 — $ / 암시장 특수 해금 — ♦)
        public int LvFistPower { get; private set; }        // 0~4
        public int LvShockwave { get; private set; }        // 0~3
        public bool HasBeaconDetect { get; private set; }
        public int LvAppraisal { get; private set; }        // ♦1/♦2: 장물 진품 감정 확률↑ (0~2)
        public int LvEvasion { get; private set; }          // ♦1: 의심도 회피 확률 +20%/Lv (0~3)
        public bool HasAdrenalineAmp { get; private set; }  // ♦2: 아드레날린 배율 ×2 → ×2.5

        // ===== 파생 스탯 =====
        public float SweepRadius
        {
            get
            {
                float baseR = ToolTier == 0 ? 0.38f + LvSweepRadius * 0.08f
                            : ToolTier == 1 ? 0.85f + LvSweepRadius * 0.14f
                            : 1.65f + LvSweepRadius * 0.22f;
                return baseR * (1f + FeverRadiusBonus);
            }
        }

        public int AutoGrabPerTick
        {
            get
            {
                int baseGrab = ToolTier == 0 ? 1
                             : ToolTier == 1 ? (3 + LvSweepRadius / 2)
                             : (10 + LvSweepRadius * 2);
                if (IsAdrenalineZone) baseGrab = Mathf.CeilToInt(baseGrab * 1.4f);
                return baseGrab;
            }
        }

        public float EffectiveAutoTickInterval
        {
            get
            {
                float baseInt = ToolTier == 0 ? 0.24f : ToolTier == 1 ? 0.14f : 0.055f;
                float workMult = Mathf.Max(0.28f, StenchWorkSpeedMult);
                float totalSpeed = workMult * (1f + FeverSpeedBonus) * AdrenalineSpeedMult;
                return Mathf.Clamp(baseInt / totalSpeed, 0.025f, 0.55f);
            }
        }

        public float CritChance => LvCrit * 0.15f;
        public float ChainChance => LvChainPop * 0.20f;
        public int ChainExtraCount => LvChainPop == 0 ? 0 : (2 + LvChainPop * 2);
        public float EvasionChance => LvEvasion * 0.20f;

        public float MoveSpeedTotalMult
        {
            get
            {
                float m = StenchMoveSpeedMult * (1f + LvMoveSpeed * 0.15f);
                if (IsBagOverfilled) m *= HasDoubleBottomBag ? 0.90f : 0.72f;
                if (IsAdrenalineZone) m *= 1.18f;
                if (HasFeverEngine && IsFeverHot) m *= 1.15f;
                return m;
            }
        }

        public bool IsLast10Jackpot => Phase1Percent >= 90f || (Phase == GamePhase.Phase2_FloorMopping && Phase2Percent >= 50f);
        public float Last10JackpotMult => IsLast10Jackpot ? (HasVipContract ? 5.0f : 4.0f) : 1.0f;
        public float VipMoneyMult => HasVipContract ? 1.5f : 1.0f;

        public float EffectiveTrashPriceMult =>
            (1f + LvTrashPrice * 0.25f) * VipMoneyMult * AdrenalineMult * FeverPriceMult * Last10JackpotMult;

        public float FistDamage =>
            (ToolTier >= 2 ? Mathf.Max(4.2f, FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)]) : FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)])
            * (IsAdrenalineZone ? 1.6f : 1.0f);

        public float ScrubSpeedMult =>
            (HasMopUnlocked ? 2.6f : 1.0f) * (1f + LvScrubPower * 0.45f) * (IsAdrenalineZone ? 1.5f : 1.0f);

        public float ThrowForwardForce => 5.8f + LvThrowPower * 1.95f;
        public float ThrowUpForce => 3.1f + LvThrowPower * 0.48f;
        public string CurrentVehicleName => VehicleNames[Mathf.Clamp(LvTruckSize, 0, VehicleNames.Length - 1)];

        // ===== 진행도 & 80% 조기 정산 =====
        private readonly List<CleanableTarget> _bigPiles = new List<CleanableTarget>();
        private readonly List<CleanableTarget> _phase2Stains = new List<CleanableTarget>();
        private readonly List<BuriedTreasureObject> _buriedTreasures = new List<BuriedTreasureObject>();

        public int Phase1Total => _bigPiles.Count * 10 + (trashSwarm != null ? trashSwarm.TotalCount : 0);
        public int Phase1Cleaned
        {
            get
            {
                int pilesDone = 0;
                foreach (var p in _bigPiles) if (p == null || p.IsCleaned) pilesDone++;
                return pilesDone * 10 + (trashSwarm != null ? trashSwarm.CleanedCount : 0);
            }
        }
        public float Phase1Percent => Phase1Total > 0 ? Mathf.Clamp01(Phase1Cleaned / (float)Phase1Total) * 100f : 100f;
        public int Phase2Total => _phase2Stains.Count;
        public int Phase2Cleaned
        {
            get
            {
                int done = 0;
                foreach (var s in _phase2Stains) if (s == null || s.IsCleaned) done++;
                return done;
            }
        }
        public float Phase2Percent => Phase2Total > 0 ? Mathf.Clamp01(Phase2Cleaned / (float)Phase2Total) * 100f : 100f;
        public float OverallCleanPercent => Phase1Percent * 0.78f + Phase2Percent * 0.22f;

        public bool CanEarlyCheckout =>
            OverallCleanPercent >= 80f &&
            (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping);

        // ===== 지층 보물 & 정산 기록 =====
        public DiscoveredTreasure ActiveTreasure { get; private set; }
        public float TreasureDecisionLockTimer { get; private set; }
        public float TreasureFlash01 { get; private set; }
        public float StealAlertFlash01 { get; private set; }

        public int StolenCount { get; private set; }
        public int ReturnedCount { get; private set; }
        public int StolenCashTotal { get; private set; }
        public string LastStolenItemName { get; private set; } = "";
        public int LastStolenValue { get; private set; } = 0;

        public bool WasEvicted { get; private set; }
        public bool WasEarlyCheckout { get; private set; }
        public int LastClearBonus { get; private set; }
        public int LastConfiscatedValue { get; private set; }
        public int LastStarRating { get; private set; } = 5;

        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;
        private float _pitchCombo;
        private float _pitchResetTimer;

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
            MouseSensitivity = PlayerPrefs.GetFloat("TC3D_MouseSens", 0.14f);
            MasterVolume = PlayerPrefs.GetFloat("TC3D_MasterVol", 0.85f);
            AudioListener.volume = MasterVolume;
        }

        private void Start()
        {
            if (trashSwarm == null) trashSwarm = FindFirstObjectByType<InstancedTrashSwarm>();
            if (player != null) player.mouseSensitivity = MouseSensitivity;
            CollectTargets();
            UpdateAtmosphere();
            UpdateVehicleStageVisuals();
        }

        public void SetMouseSensitivity(float val)
        {
            MouseSensitivity = Mathf.Clamp(val, 0.03f, 0.45f);
            if (player != null) player.mouseSensitivity = MouseSensitivity;
            PlayerPrefs.SetFloat("TC3D_MouseSens", MouseSensitivity);
        }

        public void SetMasterVolume(float val)
        {
            MasterVolume = Mathf.Clamp01(val);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat("TC3D_MasterVol", MasterVolume);
        }

        public Vector3 GetGoalOpeningHalfExtents()
        {
            switch (Mathf.Clamp(LvTruckSize, 0, 3))
            {
                case 0: return new Vector3(0.42f, 0.58f, 0.42f);
                case 1: return new Vector3(0.70f, 0.58f, 0.64f);
                case 2: return new Vector3(0.98f, 0.60f, 0.80f);
                default: return new Vector3(1.32f, 0.65f, 1.10f);
            }
        }

        private void CollectTargets()
        {
            _bigPiles.Clear();
            _phase2Stains.Clear();
            _buriedTreasures.Clear();

            foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
            {
                if (c.kind == CleanableKind.FloorStain) _phase2Stains.Add(c);
                else if (c.kind == CleanableKind.TrashPile) _bigPiles.Add(c);
                else if (c.kind == CleanableKind.SmallTrash) c.gameObject.SetActive(false);
            }

            foreach (var bt in FindObjectsByType<BuriedTreasureObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bt.manager = this;
                _buriedTreasures.Add(bt);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_toastTimer > 0f) { _toastTimer -= dt; if (_toastTimer <= 0f) ToastMessage = ""; }
            if (_pitchResetTimer > 0f) { _pitchResetTimer -= dt; if (_pitchResetTimer <= 0f) _pitchCombo = 0f; }
            if (TreasureFlash01 > 0f) TreasureFlash01 = Mathf.MoveTowards(TreasureFlash01, 0f, dt * 1.4f);
            if (StealAlertFlash01 > 0f) StealAlertFlash01 = Mathf.MoveTowards(StealAlertFlash01, 0f, dt * 1.0f);
            if (CoughKick01 > 0f) CoughKick01 = Mathf.MoveTowards(CoughKick01, 0f, dt * 3.8f);
            if (FreshAirFlash01 > 0f) FreshAirFlash01 = Mathf.MoveTowards(FreshAirFlash01, 0f, dt * 1.8f);

            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].age += dt;
                FloatingTexts[i].worldPos += Vector3.up * (0.65f * dt);
                if (FloatingTexts[i].age >= FloatingTexts[i].duration) FloatingTexts.RemoveAt(i);
            }

            if (Phase == GamePhase.TreasureDecision)
            {
                if (TreasureDecisionLockTimer > 0f) TreasureDecisionLockTimer -= dt;
                return;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            if (ThrowComboTimer > 0f) { ThrowComboTimer -= dt; if (ThrowComboTimer <= 0f) ThrowComboCount = 0; }

            // 연속 흡입 피버 감쇠
            if (_feverDecayDelay > 0f) _feverDecayDelay -= dt;
            else if (ContinuousFever01 > 0f) ContinuousFever01 = Mathf.MoveTowards(ContinuousFever01, 0f, dt * 0.34f);

            // 악취 환기/누적
            if (player != null)
            {
                bool wasOutside = IsOutsideVentilating;
                IsOutsideVentilating = player.transform.position.z > 3.02f;
                if (IsOutsideVentilating)
                {
                    if (!wasOutside && StenchPercent >= 20f) { FreshAirFlash01 = 1f; sfx.PlayChime(); }
                    if (StenchPercent > 0.1f) StenchPercent = Mathf.Max(0f, StenchPercent - 48f * dt);
                }
                else
                {
                    float roomFilth = Mathf.Lerp(1.0f, 0.25f, OverallCleanPercent / 100f);
                    StenchPercent = Mathf.Min(100f, StenchPercent + StenchGainPerSec * roomFilth * dt);
                }
            }

            if (StenchStage >= 2 && !IsOutsideVentilating)
            {
                _coughTimer -= dt;
                if (_coughTimer <= 0f)
                {
                    _coughTimer = StenchStage == 4 ? 1.8f : StenchStage == 3 ? 2.6f : 4.2f;
                    if (StenchStage >= 3) CoughKick01 = StenchStage == 4 ? 1.0f : 0.65f;
                    sfx.PlayScrub();
                }
            }

            // 10% 잔여 투시 테두리
            bool p1Detect = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval && Phase1Percent >= 90f;
            foreach (var p in _bigPiles) if (p != null) p.SetDetectOutlineVisible(p1Detect && !p.IsCleaned);
            bool p2Detect = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping && (Phase2Total - Phase2Cleaned) <= 2;
            foreach (var s in _phase2Stains) if (s != null) s.SetDetectOutlineVisible(p2Detect && !s.IsCleaned);
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.4f)
        {
            FloatingTexts.Add(new FloatingWorldText { worldPos = worldPos, text = text, color = color, age = 0f, duration = duration });
        }

        private void CheckSuspicionCleanMilestones()
        {
            float pct = OverallCleanPercent;
            if (!_washed30 && pct >= 30f) { _washed30 = true; WashSuspicionByMilestone(30); }
            if (!_washed60 && pct >= 60f) { _washed60 = true; WashSuspicionByMilestone(60); }
            if (!_washed90 && pct >= 90f) { _washed90 = true; WashSuspicionByMilestone(90); }
        }

        private void WashSuspicionByMilestone(int pctMark)
        {
            if (SuspicionPercent <= 0.1f) return;
            SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 10f);
            sfx.PlayChime();
            ShowToast($"청소 {pctMark}% · 의심 -10%");
        }

        /// <summary>
        /// v0.8 쓰레기 수거 콜백:
        /// - 연속 흡입 피버 게이지 충전
        /// - 동종 연속 5개 자동 압축 (0칸 소모 + ×2 보너스!)
        /// - 100% 도달 후에도 150% 과적까지 계속 수집 가능!
        /// </summary>
        public void OnInstancedTrashCollected(int itemValue, int typeIdx, string typeLabel)
        {
            int gainedVal = Mathf.Max(1, Mathf.RoundToInt(itemValue * EffectiveTrashPriceMult));
            CollectingCount++;
            CollectingPendingValue += gainedVal;

            // 연속 흡입 피버 충전
            _feverDecayDelay = 1.35f;
            float feverStep = HasFeverEngine ? 0.045f : 0.028f;
            ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + feverStep);

            // 동종 연속 압축 (같은 종류 5개 연속 → 봉투 5칸 즉시 환급 + 수익 ×2!)
            if (typeIdx == StreakTypeIdx)
            {
                StreakCount++;
                if (StreakCount >= CompressionStreakTarget)
                {
                    StreakCount = 0;
                    TotalCompressionsThisRoom++;
                    CollectingCount = Mathf.Max(0, CollectingCount - CompressionStreakTarget);
                    int bonus = gainedVal * CompressionStreakTarget; // 기존 5개 + 보너스 5개 = 총 ×2!
                    CollectingPendingValue += bonus;
                    sfx.PlayChime();
                    if (player != null)
                    {
                        Vector3 fxPos = player.transform.position + player.transform.forward * 1.4f + Vector3.up * 0.9f;
                        SpawnFloatingText(fxPos, $"★ {typeLabel} 압축! 0칸 · +${bonus}", new Color(0.35f, 1f, 0.72f), 1.35f);
                    }
                    ShowToast($"★ {typeLabel} 압축 · 0칸 · ×2");
                }
            }
            else
            {
                StreakTypeIdx = typeIdx;
                StreakTypeLabel = typeLabel;
                StreakCount = 1;
            }

            _pitchResetTimer = 0.35f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.04f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.38f);

            // 100% 달성 알림 (강제 중단 X → 150% 과적 도박 가능!)
            if (!_notified100Percent && CollectingCount >= HandBagCapacity)
            {
                _notified100Percent = true;
                sfx.PlayChime();
                ShowToast($"봉투 100% · [RMB] 투하 / 과적 가능");
            }

            // 150% 과적 한계 도달 시 자동 묶기
            if (CollectingCount >= OverfillCapacity)
            {
                PackageCollectingIntoBag(true);
            }

            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        public void OnRobotAutoCleaned(int eatenCount, Vector3 botPos)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(eatenCount * 3 * EffectiveTrashPriceMult));
            Money += pay;
            SpawnFloatingText(botPos + Vector3.up * 0.38f, $"[BOT] +${pay}", new Color(0.35f, 0.94f, 1f), 1.0f);
            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        /// <summary>
        /// 100~150% 모은 봉투를 즉시 묶어서 투하 ([RMB])
        /// </summary>
        public bool TieAndThrowCollectingBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (!CanTieAndThrowCollectingBag) return false;
            PackageCollectingIntoBag(false);
            ThrowCarriedBag(spawnPos, forwardDir);
            return true;
        }

        private void PackageCollectingIntoBag(bool fromMaxOverfill)
        {
            if (CollectingCount <= 0) return;
            float overRatio = Mathf.Clamp01((CollectingCount - HandBagCapacity) / (float)Mathf.Max(1, OverfillCapacity - HandBagCapacity));
            float overBonusMult = 1f + overRatio * 0.35f; // 150% 과적 성공 시 +35% 보너스!

            int cnt = CollectingCount;
            int val = Mathf.RoundToInt(CollectingPendingValue * overBonusMult);
            CollectingCount = 0;
            CollectingPendingValue = 0;
            _notified100Percent = false;

            if (!HasCarriedBag)
            {
                HasCarriedBag = true;
                CarriedBagCount = cnt;
                CarriedBagValue = val;
                sfx.PlayChime();
                if (fromMaxOverfill) ShowToast($"★ 과적 150% 봉투 · ${val}");
            }
            else if (player != null)
            {
                Vector3 dropPos = player.transform.position + player.transform.forward * 0.85f + Vector3.up * 0.45f;
                SpawnDroppedBagAt(dropPos, player.transform.forward * 1.2f + Vector3.up * 1.0f, cnt, val);
                sfx.PlayChime();
            }
        }

        /// <summary>
        /// 150% 과적 상태에서 벽·가구 충돌 시 봉투 폭발! (이중 바닥 가방 보유 시 면역)
        /// </summary>
        public void OnOverfillWallBump(Vector3 hitPos)
        {
            if (!IsBagOverfilled) return;

            if (HasDoubleBottomBag)
            {
                sfx.PlayPop(1.2f);
                SpawnFloatingText(hitPos + Vector3.up * 0.5f, "★ 이중바닥 방어!", new Color(0.45f, 0.92f, 1f), 1.1f);
                return;
            }

            int keepCount = Mathf.Max(0, HandBagCapacity / 2);
            int spillCount = Mathf.Max(4, CollectingCount - keepCount);
            float keepRatio = CollectingCount > 0 ? keepCount / (float)CollectingCount : 0f;

            CollectingCount = keepCount;
            CollectingPendingValue = Mathf.RoundToInt(CollectingPendingValue * keepRatio);
            _notified100Percent = false;
            CoughKick01 = 0.75f;
            StealAlertFlash01 = 0.55f;
            sfx.PlayAlert();

            if (trashSwarm != null) trashSwarm.SpawnBurstFountain(hitPos, spillCount);
            SpawnFloatingText(hitPos + Vector3.up * 0.6f, $"과적 폭발! -{spillCount}개", new Color(1f, 0.35f, 0.25f), 1.5f);
            ShowToast("과적 폭발 · 벽 충돌 주의");
        }

        public void ApplyShockwaveToNearbyPiles(CleanableTarget centerPile, float baseDamage)
        {
            if (LvShockwave <= 0 || centerPile == null) return;
            float splashMult = LvShockwave == 1 ? 0.45f : LvShockwave == 2 ? 0.75f : 1.0f;
            float radius = 2.6f;

            foreach (var p in _bigPiles)
            {
                if (p == null || p == centerPile || p.IsCleaned) continue;
                if (Vector3.Distance(p.transform.position, centerPile.transform.position) <= radius)
                {
                    if (p.ApplyCleanDamage(baseDamage * splashMult, null)) OnBigPileBurst(p);
                }
            }
        }

        public void OnBigPileBurst(CleanableTarget pile)
        {
            sfx.PlayPop(0.62f);
            if (trashSwarm != null) trashSwarm.SpawnBurstFountain(pile.transform.position, 22);
            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        // ===== 지층 발굴 보물 =====
        public void OnTreasureUncoveredAlert(BuriedTreasureObject obj)
        {
            if (obj == null) return;
            sfx.PlayChime();
            string tag = obj.isMentionedByClient ? "★ 의뢰인 언급 보물 노출!" : "★ 지층 보물 노출!";
            Color col = obj.isMentionedByClient ? new Color(1f, 0.48f, 0.22f) : new Color(1f, 0.88f, 0.28f);
            SpawnFloatingText(obj.transform.position + Vector3.up * 0.45f, tag, col, 2.2f);
            ShowToast($"{tag} ({obj.itemName})");
        }

        public void OnBuriedTreasureExcavated(BuriedTreasureObject obj)
        {
            if (obj == null) return;
            ActiveTreasure = new DiscoveredTreasure
            {
                itemName = obj.itemName,
                value = obj.baseValue,
                rarity = obj.rarity,
                isMentionedByClient = obj.isMentionedByClient,
                baseSuspicionGain = obj.baseSuspicionGain,
                sourceObj = obj
            };
            TreasureFlash01 = 1.0f;
            TreasureDecisionLockTimer = 0.40f;
            _phaseBeforeOverlay = Phase;
            Phase = GamePhase.TreasureDecision;
            UnlockCursor(true);
            sfx.PlayFanfare();
        }

        public void ResolveActiveTreasure(bool steal)
        {
            if (ActiveTreasure == null || TreasureDecisionLockTimer > 0f) return;
            var item = ActiveTreasure;
            ActiveTreasure = null;
            Phase = _phaseBeforeOverlay;
            UnlockCursor(false);

            if (!steal)
            {
                // 성실 전달 루트: ★ 신뢰도 토큰 + 의심 -25% 세탁 + 소액 사례금
                int tokens = item.isMentionedByClient ? 2 : 1;
                int tip = item.isMentionedByClient ? 65 : 30;
                TrustTokens += tokens;
                Money += tip;
                ReturnedCount++;
                SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 25f);
                sfx.PlayChime();
                ShowToast($"전달 · +★{tokens} · 의심 -25% · +${tip}");
                return;
            }

            // 슈킹 루트: ① 장물 감정가(진품/보통/가품) RNG + ② 의심 회피 RNG + 확정 100% 발각
            float roll = Random.value;
            float genuineChance = 0.25f + LvAppraisal * 0.20f; // 25% → 45% → 65%
            float fakeChance = Mathf.Max(0f, 0.25f - LvAppraisal * 0.13f); // 25% → 12% → 0%
            float appraisalMult = 1.0f;
            string gradeLabel = "보통";
            if (roll < genuineChance) { appraisalMult = 1.5f; gradeLabel = "진품 ×1.5"; }
            else if (roll > 1f - fakeChance) { appraisalMult = 0.6f; gradeLabel = "가품 ×0.6"; }

            float mentionMult = item.isMentionedByClient ? 2.0f : 1.0f;
            int cash = Mathf.RoundToInt(item.value * mentionMult * appraisalMult * AdrenalineMult);
            int bmPts = item.isMentionedByClient ? 2 : 1;

            Money += cash;
            BlackMarketPts += bmPts;
            StolenCount++;
            StolenCashTotal += cash;
            LastStolenItemName = item.itemName;
            LastStolenValue = cash;
            sfx.PlaySteal();

            // 의심도 회피 판정 (암시장 스킬)
            bool evaded = EvasionChance > 0f && Random.value < EvasionChance;
            if (evaded)
            {
                ShowToast($"슈킹({gradeLabel}) · +${cash} · +♦{bmPts} · 의심 회피!");
                return;
            }

            float suspGain = item.baseSuspicionGain * mentionMult;
            SuspicionPercent = Mathf.Min(100f, SuspicionPercent + suspGain);
            StealAlertFlash01 = SuspicionPercent >= 80f ? 0.9f : 0.45f;

            if (SuspicionPercent >= 100f)
            {
                TriggerForcedEviction();
            }
            else if (IsAdrenalineZone)
            {
                ShowToast($"★ 아드레날린! ({SuspicionPercent:0}%) · 수익 ×{AdrenalineMult:0.#}");
            }
            else
            {
                ShowToast($"슈킹({gradeLabel}) · +${cash} · +♦{bmPts} · 의심 +{suspGain:0}%");
            }
        }

        /// <summary>
        /// 의심 100% 확정 발각 → 게임오버 금지! 즉시 강제 퇴거 & 정산
        /// (방 클리어 보너스 몰수 + 마지막 슈킹템 1개 압수 / 기본 청소 수익·토큰·스킬트리 100% 유지!)
        /// </summary>
        private void TriggerForcedEviction()
        {
            WasEvicted = true;
            WasEarlyCheckout = false;
            LastClearBonus = 0;
            LastConfiscatedValue = Mathf.Min(Money, LastStolenValue);
            Money -= LastConfiscatedValue;
            LastStarRating = 1;
            sfx.PlayAlert();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        /// <summary>
        /// 80% 이상 달성 시 조기 정산([K] / HUD 영수증 버튼)
        /// </summary>
        public void TriggerEarlyCheckout()
        {
            if (!CanEarlyCheckout) return;
            WasEvicted = false;
            WasEarlyCheckout = true;
            LastConfiscatedValue = 0;
            float ratio = Mathf.Clamp01(OverallCleanPercent / 100f);
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * ratio * 0.85f * VipMoneyMult);
            Money += LastClearBonus;
            LastStarRating = StolenCount == 0 ? 4 : 3;
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ThrowCarriedBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (!HasCarriedBag)
            {
                if (CollectingCount > 0) ShowToast($"봉투 미달 ({CollectingCount}/{HandBagCapacity})");
                return;
            }

            int cnt = CarriedBagCount;
            int val = CarriedBagValue;
            HasCarriedBag = false;
            CarriedBagCount = 0;
            CarriedBagValue = 0;
            sfx.PlayPop(0.65f);

            Vector3 launchVel = forwardDir.normalized * ThrowForwardForce + Vector3.up * ThrowUpForce;
            SpawnDroppedBagAt(spawnPos, launchVel, cnt, val);
        }

        private void SpawnDroppedBagAt(Vector3 pos, Vector3 vel, int count, int value)
        {
            GameObject bagObj = thrownBagPrefab != null
                ? Instantiate(thrownBagPrefab, pos, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bagObj.SetActive(true);
            bagObj.transform.position = pos;

            var rb = bagObj.GetComponent<Rigidbody>();
            if (rb == null) rb = bagObj.AddComponent<Rigidbody>();
            rb.mass = 1.25f;
            rb.linearDamping = 0.15f;

            var proj = bagObj.AddComponent<ThrownTrashBag>();
            proj.Launch(vel, pos, truckBedGoal, this, count, value, HasTruckMagnet);
        }

        public void OnDroppedBagRetrieved(int count, int pendingVal)
        {
            HasCarriedBag = true;
            CarriedBagCount = count;
            CarriedBagValue = pendingVal;
            sfx.PlayPop(1.05f);
            ShowToast($"봉투 쥐기 · ${CarriedBagValue}");
        }

        public void OnBagScored(int count, int bagValue, float distance, Vector3 scoreWorldPos)
        {
            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(6, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = MaxComboWindow;

            int rawBase = Mathf.Max(bagValue, count * 3);
            float comboStep = 0.25f + LvComboTime * 0.10f;
            float comboMult = 1f + (ThrowComboCount - 1) * comboStep;
            int distBonus = distance >= 6.8f ? 35 : distance >= 4.5f ? 15 : 0;
            int vehBonus = LvTruckSize * 20;
            int totalPay = Mathf.Max(1, Mathf.RoundToInt(rawBase * comboMult + distBonus + vehBonus));

            Money += totalPay;
            sfx.PlayChime();

            SpawnStackedBagVisualInTruck();

            string popup = ThrowComboCount >= 2
                ? $"{ThrowComboCount}연속! +${totalPay}"
                : distBonus >= 35 ? $"롱슛! +${totalPay}" : $"+${totalPay}";
            Color popCol = ThrowComboCount >= 2 ? new Color(1f, 0.85f, 0.22f) : new Color(0.45f, 1f, 0.55f);
            SpawnFloatingText(scoreWorldPos + Vector3.up * 0.45f, popup, popCol, 1.8f);

            CheckPhase1Complete();
        }

        private void SpawnStackedBagVisualInTruck()
        {
            if (truckStackedParent == null || thrownBagPrefab == null) return;
            if (_stackedBagsInTruck >= 45) return;

            int idx = _stackedBagsInTruck++;
            Vector3 half = GetGoalOpeningHalfExtents();
            int cols = LvTruckSize == 0 ? 1 : LvTruckSize == 1 ? 2 : 3;
            int perLayer = cols * cols;
            int layer = idx / perLayer;
            int slot = idx % perLayer;

            float rx = cols == 1 ? 0f : Mathf.Lerp(-half.x * 0.55f, half.x * 0.55f, (slot % cols) / (float)(cols - 1));
            float rz = cols == 1 ? 0f : Mathf.Lerp(-half.z * 0.55f, half.z * 0.55f, (slot / cols) / (float)(cols - 1));
            float sy = layer * 0.22f;

            GameObject bag = Instantiate(thrownBagPrefab, truckStackedParent);
            bag.name = $"StackedBag_{idx + 1:00}";
            bag.SetActive(true);
            bag.transform.localPosition = new Vector3(rx + Random.Range(-0.04f, 0.04f), sy, rz + Random.Range(-0.04f, 0.04f));
            bag.transform.localRotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f));
            bag.transform.localScale = Vector3.one * (LvTruckSize == 0 ? 0.72f : 0.92f);
            foreach (var col in bag.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in bag.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            _stackedVisuals.Add(bag);
        }

        private void CheckPhase1Complete()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            if (Phase1Cleaned >= Phase1Total && Phase1Total > 0)
            {
                if (CollectingCount > 0) PackageCollectingIntoBag(false);
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                ShowToast("2단계 · 밀대 바닥 닦기");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(stain.rewardMoney * EffectiveTrashPriceMult));
            Money += pay;
            sfx.PlayChime();
            SpawnFloatingText(stain.transform.position + Vector3.up * 0.42f, $"✦ 반짝! +${pay}", new Color(0.55f, 0.96f, 1f), 1.5f);
            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        public void ToggleMindMap()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.MindMapUpgrade) { Phase = _phaseBeforeOverlay; UnlockCursor(false); }
            else { if (Phase != GamePhase.SettingsMenu) _phaseBeforeOverlay = Phase; Phase = GamePhase.MindMapUpgrade; UnlockCursor(true); }
        }

        public void ToggleSettingsMenu()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.SettingsMenu || Phase == GamePhase.MindMapUpgrade) { Phase = _phaseBeforeOverlay; UnlockCursor(false); }
            else { _phaseBeforeOverlay = Phase; Phase = GamePhase.SettingsMenu; UnlockCursor(true); }
        }

        // ===== v0.8 스킬트리 가격 & 순차 해금 (3재화: $ / ★ / ♦) =====
        public int CostTool => ToolTier == 0 ? 55 : 220;
        public int CostSweep => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvSweepRadius));
        public int CostFever => 95;
        public int CostCrit => Mathf.RoundToInt(40 * Mathf.Pow(1.85f, LvCrit));
        public int CostChain => Mathf.RoundToInt(75 * Mathf.Pow(1.95f, LvChainPop));
        public int CostRoomba => Mathf.RoundToInt(150 * Mathf.Pow(2.0f, LvRoomba));

        public int CostBag => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvBagSize));
        public int CostThrowPower => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvThrowPower));
        public int CostTrashPrice => Mathf.RoundToInt(45 * Mathf.Pow(1.85f, LvTrashPrice));
        public int CostTruck => LvTruckSize == 0 ? 60 : LvTruckSize == 1 ? 150 : 300;
        public int CostComboTime => Mathf.RoundToInt(65 * Mathf.Pow(1.9f, LvComboTime));
        public int CostMagnet => 180;

        public int CostMask => Mathf.RoundToInt(30 * Mathf.Pow(1.75f, LvMask));
        public int CostMoveSpeed => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvMoveSpeed));
        public int CostMop => 70;
        public int CostScrub => Mathf.RoundToInt(90 * Mathf.Pow(1.85f, LvScrubPower));

        // ★ 신뢰도 토큰 (성실 루트 특수 해금)
        public int CostVipTokens => 1;
        public int CostDetectorTokens => 1;
        public int CostDoubleBagTokens => 2;

        // $ 파괴·탐지
        public int CostFist => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostShockwave => Mathf.RoundToInt(70 * Mathf.Pow(1.9f, LvShockwave));
        public int CostBeacon => 60;

        // ♦ 암시장 포인트 (슈킹 루트 특수 해금)
        public int CostAppraisalPts => LvAppraisal == 0 ? 1 : 2;
        public int CostEvasionPts => 1;
        public int CostAdrenalinePts => 2;

        public bool IsNodeUnlocked(string id)
        {
            switch (id)
            {
                // 초반 4개 코어 + 성실/암시장 입구
                case "tool": case "bag": case "mask": case "fist": return true;
                case "vip": case "detector": return true;       // ★ 신뢰도 토큰 획득 즉시 해금
                case "appraisal": case "evasion": return true;  // ♦ 암시장 포인트 획득 즉시 해금

                // Tier 2
                case "sweep": return true;
                case "fever": return ToolTier >= 1;
                case "crit": return LvSweepRadius >= 1;
                case "throw": return LvBagSize >= 1;
                case "price": return LvBagSize >= 1;
                case "speed": return LvMask >= 1;
                case "mop": return LvMask >= 1;
                case "doublebag": return HasVipContract || HasTreasureDetector;
                case "shock": return LvFistPower >= 1;
                case "beacon": return LvFistPower >= 1;
                case "adrenaline": return LvAppraisal >= 1 || LvEvasion >= 1;

                // Tier 3+
                case "chain": return LvCrit >= 1;
                case "roomba": return LvChainPop >= 1 || ToolTier >= 2;
                case "truck": return LvThrowPower >= 1;
                case "combo": return LvTrashPrice >= 1;
                case "magnet": return LvTruckSize >= 2;
                case "scrub": return HasMopUnlocked;
                default: return false;
            }
        }

        public int CountAffordableUpgrades()
        {
            int c = 0;
            if (IsNodeUnlocked("tool") && ToolTier < 2 && Money >= CostTool) c++;
            if (IsNodeUnlocked("sweep") && LvSweepRadius < 5 && Money >= CostSweep) c++;
            if (IsNodeUnlocked("fever") && !HasFeverEngine && Money >= CostFever) c++;
            if (IsNodeUnlocked("crit") && LvCrit < 4 && Money >= CostCrit) c++;
            if (IsNodeUnlocked("chain") && LvChainPop < 3 && Money >= CostChain) c++;
            if (IsNodeUnlocked("roomba") && LvRoomba < 3 && Money >= CostRoomba) c++;

            if (IsNodeUnlocked("bag") && LvBagSize < 4 && Money >= CostBag) c++;
            if (IsNodeUnlocked("throw") && LvThrowPower < 4 && Money >= CostThrowPower) c++;
            if (IsNodeUnlocked("price") && LvTrashPrice < 5 && Money >= CostTrashPrice) c++;
            if (IsNodeUnlocked("truck") && LvTruckSize < 3 && Money >= CostTruck) c++;
            if (IsNodeUnlocked("combo") && LvComboTime < 3 && Money >= CostComboTime) c++;
            if (IsNodeUnlocked("magnet") && !HasTruckMagnet && Money >= CostMagnet) c++;

            if (IsNodeUnlocked("mask") && LvMask < 5 && Money >= CostMask) c++;
            if (IsNodeUnlocked("speed") && LvMoveSpeed < 4 && Money >= CostMoveSpeed) c++;
            if (IsNodeUnlocked("mop") && !HasMopUnlocked && Money >= CostMop) c++;
            if (IsNodeUnlocked("scrub") && LvScrubPower < 3 && Money >= CostScrub) c++;

            if (IsNodeUnlocked("vip") && !HasVipContract && TrustTokens >= CostVipTokens) c++;
            if (IsNodeUnlocked("detector") && !HasTreasureDetector && TrustTokens >= CostDetectorTokens) c++;
            if (IsNodeUnlocked("doublebag") && !HasDoubleBottomBag && TrustTokens >= CostDoubleBagTokens) c++;

            if (IsNodeUnlocked("fist") && LvFistPower < 4 && Money >= CostFist) c++;
            if (IsNodeUnlocked("shock") && LvShockwave < 3 && Money >= CostShockwave) c++;
            if (IsNodeUnlocked("beacon") && !HasBeaconDetect && Money >= CostBeacon) c++;

            if (IsNodeUnlocked("appraisal") && LvAppraisal < 2 && BlackMarketPts >= CostAppraisalPts) c++;
            if (IsNodeUnlocked("evasion") && LvEvasion < 3 && BlackMarketPts >= CostEvasionPts) c++;
            if (IsNodeUnlocked("adrenaline") && !HasAdrenalineAmp && BlackMarketPts >= CostAdrenalinePts) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            if (!IsNodeUnlocked(id)) return false;
            switch (id)
            {
                case "tool":
                    if (ToolTier >= 2 || Money < CostTool) return false;
                    Money -= CostTool; ToolTier++;
                    ShowToast($"도구 진화 → {CurrentToolName}");
                    break;
                case "sweep": if (LvSweepRadius >= 5 || Money < CostSweep) return false; Money -= CostSweep; LvSweepRadius++; break;
                case "fever": if (HasFeverEngine || Money < CostFever) return false; Money -= CostFever; HasFeverEngine = true; break;
                case "crit": if (LvCrit >= 4 || Money < CostCrit) return false; Money -= CostCrit; LvCrit++; break;
                case "chain": if (LvChainPop >= 3 || Money < CostChain) return false; Money -= CostChain; LvChainPop++; break;
                case "roomba":
                    if (LvRoomba >= 3 || Money < CostRoomba) return false;
                    Money -= CostRoomba; LvRoomba++;
                    _roombas.Add(AutoRoombaCleaner.Spawn(this, new Vector3(Random.Range(-1.2f, 1.2f), 0.05f, Random.Range(-0.8f, 1.0f)), _roombas.Count));
                    ShowToast($"로봇청소기 {_roombas.Count}호 투입");
                    break;

                case "bag": if (LvBagSize >= 4 || Money < CostBag) return false; Money -= CostBag; LvBagSize++; break;
                case "throw": if (LvThrowPower >= 4 || Money < CostThrowPower) return false; Money -= CostThrowPower; LvThrowPower++; break;
                case "price": if (LvTrashPrice >= 5 || Money < CostTrashPrice) return false; Money -= CostTrashPrice; LvTrashPrice++; break;
                case "truck":
                    if (LvTruckSize >= 3 || Money < CostTruck) return false;
                    Money -= CostTruck; LvTruckSize++; UpdateVehicleStageVisuals();
                    ShowToast($"수거함 → {CurrentVehicleName}");
                    break;
                case "combo": if (LvComboTime >= 3 || Money < CostComboTime) return false; Money -= CostComboTime; LvComboTime++; break;
                case "magnet": if (HasTruckMagnet || Money < CostMagnet) return false; Money -= CostMagnet; HasTruckMagnet = true; break;

                case "mask": if (LvMask >= 5 || Money < CostMask) return false; Money -= CostMask; LvMask++; break;
                case "speed": if (LvMoveSpeed >= 4 || Money < CostMoveSpeed) return false; Money -= CostMoveSpeed; LvMoveSpeed++; break;
                case "mop": if (HasMopUnlocked || Money < CostMop) return false; Money -= CostMop; HasMopUnlocked = true; break;
                case "scrub": if (LvScrubPower >= 3 || Money < CostScrub) return false; Money -= CostScrub; LvScrubPower++; break;

                // ★ 신뢰도 토큰 해금
                case "vip": if (HasVipContract || TrustTokens < CostVipTokens) return false; TrustTokens -= CostVipTokens; HasVipContract = true; ShowToast("★ VIP 의뢰 해금 · 단가 ×1.5"); break;
                case "detector": if (HasTreasureDetector || TrustTokens < CostDetectorTokens) return false; TrustTokens -= CostDetectorTokens; HasTreasureDetector = true; ShowToast("★ 지층 보물 투시 해금"); break;
                case "doublebag": if (HasDoubleBottomBag || TrustTokens < CostDoubleBagTokens) return false; TrustTokens -= CostDoubleBagTokens; HasDoubleBottomBag = true; ShowToast("★ 이중 바닥 가방 · 과적 폭발 면역"); break;

                case "fist": if (LvFistPower >= 4 || Money < CostFist) return false; Money -= CostFist; LvFistPower++; break;
                case "shock": if (LvShockwave >= 3 || Money < CostShockwave) return false; Money -= CostShockwave; LvShockwave++; break;
                case "beacon": if (HasBeaconDetect || Money < CostBeacon) return false; Money -= CostBeacon; HasBeaconDetect = true; break;

                // ♦ 암시장 포인트 해금
                case "appraisal": if (LvAppraisal >= 2 || BlackMarketPts < CostAppraisalPts) return false; BlackMarketPts -= CostAppraisalPts; LvAppraisal++; break;
                case "evasion": if (LvEvasion >= 3 || BlackMarketPts < CostEvasionPts) return false; BlackMarketPts -= CostEvasionPts; LvEvasion++; break;
                case "adrenaline": if (HasAdrenalineAmp || BlackMarketPts < CostAdrenalinePts) return false; BlackMarketPts -= CostAdrenalinePts; HasAdrenalineAmp = true; break;

                default: return false;
            }
            sfx.PlayChime();
            return true;
        }

        private void UpdateVehicleStageVisuals()
        {
            if (vehicleStageObjects == null || vehicleStageObjects.Length == 0) return;
            int idx = Mathf.Clamp(LvTruckSize, 0, vehicleStageObjects.Length - 1);
            for (int i = 0; i < vehicleStageObjects.Length; i++)
            {
                if (vehicleStageObjects[i] != null) vehicleStageObjects[i].SetActive(i == idx);
            }
        }

        private void FinishRoomComplete()
        {
            WasEvicted = false;
            WasEarlyCheckout = false;
            LastConfiscatedValue = 0;
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * VipMoneyMult);
            Money += LastClearBonus;
            LastStarRating = StolenCount == 0 ? 5 : 4;
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ReplayRoomKeepUpgrades()
        {
            foreach (var p in _bigPiles) if (p != null) p.ResetForNewRun();
            foreach (var s in _phase2Stains) if (s != null) s.ResetForNewRun();
            foreach (var bt in _buriedTreasures) if (bt != null) bt.ResetForNewRun();
            if (trashSwarm != null) trashSwarm.ResetSwarm(InstancedTrashSwarm.InitialSwarmCount);

            foreach (var v in _stackedVisuals) if (v != null) Destroy(v);
            _stackedVisuals.Clear();
            _stackedBagsInTruck = 0;

            foreach (var dropped in FindObjectsByType<ThrownTrashBag>(FindObjectsSortMode.None))
            {
                Destroy(dropped.gameObject);
            }

            ActiveTreasure = null;
            CollectingCount = 0;
            CollectingPendingValue = 0;
            _notified100Percent = false;
            HasCarriedBag = false;
            CarriedBagCount = 0;
            CarriedBagValue = 0;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            ContinuousFever01 = 0f;
            _feverDecayDelay = 0f;
            StreakTypeIdx = -1;
            StreakTypeLabel = "";
            StreakCount = 0;
            TotalCompressionsThisRoom = 0;
            StenchPercent = 0f;
            SuspicionPercent = 0f;
            _washed30 = _washed60 = _washed90 = false;
            StolenCount = 0;
            ReturnedCount = 0;
            StolenCashTotal = 0;
            LastStolenItemName = "";
            LastStolenValue = 0;
            WasEvicted = false;
            WasEarlyCheckout = false;
            LastClearBonus = 0;
            LastConfiscatedValue = 0;
            Phase = GamePhase.Phase1_TrashRemoval;

            UpdateAtmosphere();
            UpdateVehicleStageVisuals();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            UnlockCursor(false);
        }

        private void UpdateAtmosphere()
        {
            float t = Mathf.Clamp01(OverallCleanPercent / 100f);
            RenderSettings.fogDensity = Mathf.Lerp(0.032f, 0.002f, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.18f, 0.16f, 0.12f), new Color(0.84f, 0.87f, 0.89f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.28f, 0.26f, 0.22f), new Color(0.68f, 0.70f, 0.72f), t);
            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 1.0f, 0.98f), t);
                ceilingLight.intensity = Mathf.Lerp(15f, 24f, t);
            }
        }

        public void ShowToast(string msg)
        {
            ToastMessage = msg;
            _toastTimer = 2.8f;
        }

        private static void UnlockCursor(bool unlock)
        {
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
