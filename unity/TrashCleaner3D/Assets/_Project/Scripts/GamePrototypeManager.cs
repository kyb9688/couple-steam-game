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
    /// TrashCleaner3D v0.9 중앙 매니저:
    /// - 1초 HOLD 진동 줍기(맨손 1.0s → 집게/빗자루 0.40s → 진공청소기 0.10s)
    /// - 쓰레기 봉투 100% 정직한 원복 (150% 과적/벽 충돌 폭발 완전 제거!)
    /// - 큰 봉투 터뜨리기 / 바닥 쓰레기 줍기 시 일정 확률로 보물 자동 발견 팝업!
    /// - 한 판 종료 후 5버튼 허브(다시 시작 · 다음 단계 · ♦암시장 · ★특전 · $강화) + 스테이지 진행!
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

        [Header("Room Config")]
        public int baseCleanReward = 450;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        // ===== 스테이지 진행 =====
        public int CurrentStage { get; private set; } = 1;
        public float StageRewardMult => 1f + (CurrentStage - 1) * 0.30f;

        private static readonly (string hint, string itemName, int val)[] StageClientHints =
        {
            ("와이프 몰래 숨긴 비상금 · 나오면 연락!", "남편의 비밀 비상금 봉투", 220),
            ("엄마가 준 순금 반지 · 버리지 마세요!", "어머니의 순금 가락지", 260),
            ("전 남친 명품 시계 · 꼭 돌려주세요!", "스위스 명품 손목시계", 300),
            ("월세 보증금 봉투 · 라면박스 안에 있어요!", "현금 보증금 돈봉투", 350)
        };

        public string CurrentClientHint => StageClientHints[(CurrentStage - 1) % StageClientHints.Length].hint;
        private (string hint, string itemName, int val) CurrentMentionedSpec => StageClientHints[(CurrentStage - 1) % StageClientHints.Length];

        // ===== 3축 재화 ($ / ★ / ♦) =====
        public int Money { get; private set; } = 0;            // $ 현금 (인게임 스킬트리 & 클리어 후 강화)
        public int TrustTokens { get; private set; } = 0;      // ★ 신뢰도 토큰 (클리어 후 [특전] 전용)
        public int BlackMarketPts { get; private set; } = 0;   // ♦ 암시장 포인트 (클리어 후 [암시장] 전용)

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
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.0f : StenchStage == 3 ? 0.58f : StenchStage == 2 ? 0.78f : 1.0f;
        public float StenchGainPerSec => 3.4f * (1f - LvMask * 0.16f);

        // ===== 의심 게이지 (0~100% 확정 발각 · 80~95% 아드레날린 구간!) =====
        public float SuspicionPercent { get; private set; } = 0f;
        public bool IsAdrenalineZone => SuspicionPercent >= 80f && SuspicionPercent < 100f;
        public float AdrenalineMult => IsAdrenalineZone ? (HasAdrenalineAmp ? 2.5f : 2.0f) : 1.0f;
        public float AdrenalineSpeedMult => IsAdrenalineZone ? 1.85f : 1.0f;
        private bool _washed30, _washed60, _washed90;

        // ===== 도구 진화 (0:맨손 1.0s HOLD → 1:집게/빗자루 0.40s → 2:진공청소기 0.10s) =====
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

        // ===== 동종 연속 압축 (기본 5개 / 특전 해금 시 3개 → 0칸 + ×2!) =====
        public int CompressionStreakTarget => HasFastCompressor ? 3 : 5;
        public int StreakTypeIdx { get; private set; } = -1;
        public string StreakTypeLabel { get; private set; } = "";
        public int StreakCount { get; private set; } = 0;
        public int TotalCompressionsThisRoom { get; private set; } = 0;

        // ===== 봉투 100% 정직 원복 (과적/폭발 완전 제거!) =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly float[] FistDmgByLv = { 1.4f, 2.4f, 3.8f, 5.6f, 10.0f };
        private static readonly string[] VehicleNames = { "공공 쓰레기통", "리어카", "다마스", "1톤 트럭" };

        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (ToolTier >= 2 ? 2 : 1);

        // 손에 쥔 완성 봉지 1개
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

        // ===== 인게임 스킬트리 레벨 (현금 $ 전용 19노드!) =====
        public int LvSweepRadius { get; private set; }      // 0~5
        public bool HasFeverEngine { get; private set; }
        public int LvCrit { get; private set; }             // 0~4
        public int LvChainPop { get; private set; }         // 0~3
        public int LvRoomba { get; private set; }           // 0~3

        public int LvBagSize { get; private set; }          // 0~4
        public int LvThrowPower { get; private set; }       // 0~4
        public int LvTrashPrice { get; private set; }       // 0~5
        public int LvTruckSize { get; private set; }        // 0~3
        public int LvComboTime { get; private set; }        // 0~3
        public bool HasTruckMagnet { get; private set; }

        public int LvMask { get; private set; }             // 0~5
        public int LvMoveSpeed { get; private set; }        // 0~4
        public bool HasMopUnlocked { get; private set; }
        public int LvScrubPower { get; private set; }       // 0~3

        public int LvFistPower { get; private set; }        // 0~4
        public int LvShockwave { get; private set; }        // 0~3
        public bool HasBeaconDetect { get; private set; }

        // ===== 클리어 후 [★ 특전] (성실 신뢰도 토큰 전용) =====
        public bool HasVipContract { get; private set; }    // ★1: 전체 수익·보너스 ×1.5, 막판 10% ×5
        public bool HasFastCompressor { get; private set; } // ★1: 동종 압축 5개 → 3개!
        public bool HasTreasureSense { get; private set; }  // ★2: 보물 발견 확률 ×1.6 & 고가 보물 확률↑

        // ===== 클리어 후 [♦ 암시장] (암시장 포인트 전용) =====
        public int LvAppraisal { get; private set; }        // ♦1/♦2: 장물 진품(×1.5) 확률↑ (0~2)
        public int LvEvasion { get; private set; }          // ♦1: 의심도 회피 +20%/Lv (0~3)
        public bool HasAdrenalineAmp { get; private set; }  // ♦2: 의심 80~95% 배율 ×2 → ×2.5

        // ===== 파생 스탯 =====
        public float SweepRadius
        {
            get
            {
                float baseR = ToolTier == 0 ? 0.36f + LvSweepRadius * 0.08f
                            : ToolTier == 1 ? 0.85f + LvSweepRadius * 0.14f
                            : 1.65f + LvSweepRadius * 0.22f;
                return baseR * (1f + FeverRadiusBonus);
            }
        }

        public int AutoGrabPerTick
        {
            get
            {
                int baseGrab = ToolTier == 0 ? (1 + LvSweepRadius / 3)
                             : ToolTier == 1 ? (3 + LvSweepRadius)
                             : (10 + LvSweepRadius * 2);
                if (IsAdrenalineZone) baseGrab = Mathf.CeilToInt(baseGrab * 1.35f);
                return baseGrab;
            }
        }

        /// <summary>
        /// 1단계 맨손: 1.0초 HOLD 진동 후 쏙! → 2단계 집게/빗자루: 0.40초 → 3단계 진공청소기: 0.10초
        /// </summary>
        public float EffectiveHoldDuration
        {
            get
            {
                float baseDur = ToolTier == 0 ? 1.00f : ToolTier == 1 ? 0.40f : 0.10f;
                float workMult = Mathf.Max(0.35f, StenchWorkSpeedMult);
                float totalSpeed = workMult * (1f + FeverSpeedBonus) * AdrenalineSpeedMult;
                return Mathf.Clamp(baseDur / totalSpeed, 0.05f, 1.8f);
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
                if (IsAdrenalineZone) m *= 1.18f;
                if (HasFeverEngine && IsFeverHot) m *= 1.15f;
                return m;
            }
        }

        public bool IsLast10Jackpot => Phase1Percent >= 90f || (Phase == GamePhase.Phase2_FloorMopping && Phase2Percent >= 50f);
        public float Last10JackpotMult => IsLast10Jackpot ? (HasVipContract ? 5.0f : 4.0f) : 1.0f;
        public float VipMoneyMult => HasVipContract ? 1.5f : 1.0f;

        public float EffectiveTrashPriceMult =>
            (1f + LvTrashPrice * 0.25f) * StageRewardMult * VipMoneyMult * AdrenalineMult * FeverPriceMult * Last10JackpotMult;

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

        // ===== 자동 보물 발견 시스템 =====
        public DiscoveredTreasure ActiveTreasure { get; private set; }
        public float TreasureDecisionLockTimer { get; private set; }
        public float TreasurePopupOpenAge { get; private set; }
        public float TreasureFlash01 { get; private set; }
        public float StealAlertFlash01 { get; private set; }

        private int _treasuresFoundThisRoom;
        private int _pilesBrokenThisRoom;
        private bool _mentionedFoundThisRoom;
        private float _treasureCooldown;

        private static readonly (string name, int val, TreasureRarity rarity, float susp)[] RandomTreasurePool =
        {
            ("구겨진 5만 원권 뭉치", 130, TreasureRarity.Common, 14f),
            ("중고 휴대용 게임기", 175, TreasureRarity.Rare, 16f),
            ("한정판 피규어 세트", 210, TreasureRarity.Rare, 17f),
            ("빈티지 금시계", 260, TreasureRarity.Rare, 19f),
            ("24K 미니 골드바", 380, TreasureRarity.Legendary, 24f)
        };

        // ===== 정산 기록 =====
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
            foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
            {
                if (c.kind == CleanableKind.FloorStain) _phase2Stains.Add(c);
                else if (c.kind == CleanableKind.TrashPile) _bigPiles.Add(c);
                else if (c.kind == CleanableKind.SmallTrash) c.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_toastTimer > 0f) { _toastTimer -= dt; if (_toastTimer <= 0f) ToastMessage = ""; }
            if (_pitchResetTimer > 0f) { _pitchResetTimer -= dt; if (_pitchResetTimer <= 0f) _pitchCombo = 0f; }
            if (_treasureCooldown > 0f) _treasureCooldown -= dt;
            if (TreasureFlash01 > 0f) TreasureFlash01 = Mathf.MoveTowards(TreasureFlash01, 0f, dt * 1.1f);
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
                TreasurePopupOpenAge += dt;
                if (TreasureDecisionLockTimer > 0f) TreasureDecisionLockTimer -= dt;
                return;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            if (ThrowComboTimer > 0f) { ThrowComboTimer -= dt; if (ThrowComboTimer <= 0f) ThrowComboCount = 0; }

            if (_feverDecayDelay > 0f) _feverDecayDelay -= dt;
            else if (ContinuousFever01 > 0f) ContinuousFever01 = Mathf.MoveTowards(ContinuousFever01, 0f, dt * 0.30f);

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
        /// 바닥 쓰레기 수거 콜백:
        /// - 100% 차면 즉시 깔끔하게 봉투 완성 (과적/벽충돌 폭발 없음!)
        /// - 동종 연속 압축 (5개 / 특전 3개 → 0칸 + ×2)
        /// - 일정 확률로 보물 자동 발견 팝업!
        /// </summary>
        public void OnInstancedTrashCollected(int itemValue, int typeIdx, string typeLabel)
        {
            int gainedVal = Mathf.Max(1, Mathf.RoundToInt(itemValue * EffectiveTrashPriceMult));
            CollectingCount++;
            CollectingPendingValue += gainedVal;

            _feverDecayDelay = 1.65f;
            float feverStep = HasFeverEngine ? 0.055f : 0.035f;
            ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + feverStep);

            // 동종 연속 압축
            int targetStreak = CompressionStreakTarget;
            if (typeIdx == StreakTypeIdx)
            {
                StreakCount++;
                if (StreakCount >= targetStreak)
                {
                    StreakCount = 0;
                    TotalCompressionsThisRoom++;
                    CollectingCount = Mathf.Max(0, CollectingCount - targetStreak);
                    int bonus = gainedVal * targetStreak;
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

            _pitchResetTimer = 0.45f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.05f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.38f);

            // 100% 도달 시 즉시 완성 봉투로 묶기!
            if (CollectingCount >= HandBagCapacity)
            {
                PackageCollectingIntoBag();
            }

            // 쓰레기 줍기 시 일정 확률로 보물 자동 팝업!
            TryRollAutomaticTreasure(fromBigPile: false);

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

        private void PackageCollectingIntoBag()
        {
            if (CollectingCount <= 0) return;
            int cnt = CollectingCount;
            int val = CollectingPendingValue;
            CollectingCount = 0;
            CollectingPendingValue = 0;

            if (!HasCarriedBag)
            {
                HasCarriedBag = true;
                CarriedBagCount = cnt;
                CarriedBagValue = val;
                sfx.PlayChime();
                ShowToast($"봉투 완성 (${val}) · [RMB] 투하");
            }
            else if (player != null)
            {
                Vector3 dropPos = player.transform.position + player.transform.forward * 0.85f + Vector3.up * 0.45f;
                SpawnDroppedBagAt(dropPos, player.transform.forward * 1.2f + Vector3.up * 1.0f, cnt, val);
                sfx.PlayChime();
            }
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
            _pilesBrokenThisRoom++;
            if (trashSwarm != null) trashSwarm.SpawnBurstFountain(pile.transform.position, 22);

            // 큰 봉투 터뜨릴 때 높은 확률로 보물 자동 발견!
            TryRollAutomaticTreasure(fromBigPile: true);

            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        private void TryRollAutomaticTreasure(bool fromBigPile)
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            int maxTreasures = HasTreasureSense ? 6 : 5;
            if (_treasuresFoundThisRoom >= maxTreasures) return;
            if (_treasureCooldown > 0f) return;

            bool trigger = false;
            if (fromBigPile)
            {
                float chance = HasTreasureSense ? 0.46f : 0.32f;
                if (_pilesBrokenThisRoom == 3 && _treasuresFoundThisRoom == 0) trigger = true; // 첫 발견 보장!
                else if (Random.value < chance) trigger = true;
            }
            else
            {
                float chance = HasTreasureSense ? 0.018f : 0.011f;
                if (Random.value < chance) trigger = true;
            }

            if (!trigger) return;

            _treasuresFoundThisRoom++;
            _treasureCooldown = 9.0f;

            DiscoveredTreasure item;
            // 2번째 보물은 확정적으로 의뢰인 언급 물건 등장!
            if (!_mentionedFoundThisRoom && (_treasuresFoundThisRoom == 2 || (_pilesBrokenThisRoom >= 6 && Random.value < 0.5f)))
            {
                _mentionedFoundThisRoom = true;
                var spec = CurrentMentionedSpec;
                item = new DiscoveredTreasure
                {
                    itemName = spec.itemName,
                    value = Mathf.RoundToInt(spec.val * StageRewardMult),
                    rarity = TreasureRarity.Legendary,
                    isMentionedByClient = true,
                    baseSuspicionGain = 24f
                };
            }
            else
            {
                int minIdx = HasTreasureSense ? 1 : 0;
                var pick = RandomTreasurePool[Random.Range(minIdx, RandomTreasurePool.Length)];
                item = new DiscoveredTreasure
                {
                    itemName = pick.name,
                    value = Mathf.RoundToInt(pick.val * StageRewardMult),
                    rarity = pick.rarity,
                    isMentionedByClient = false,
                    baseSuspicionGain = pick.susp
                };
            }

            ActiveTreasure = item;
            TreasureFlash01 = 1.0f;
            TreasureDecisionLockTimer = 0.45f;
            TreasurePopupOpenAge = 0f;
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
                int tokens = item.isMentionedByClient ? 2 : 1;
                int tip = Mathf.RoundToInt((item.isMentionedByClient ? 65 : 30) * StageRewardMult);
                TrustTokens += tokens;
                Money += tip;
                ReturnedCount++;
                SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 25f);
                sfx.PlayChime();
                ShowToast($"전달 · +★{tokens} · 의심 -25% · +${tip}");
                return;
            }

            float roll = Random.value;
            float genuineChance = 0.25f + LvAppraisal * 0.25f; // 25% → 50% → 75%
            float fakeChance = Mathf.Max(0f, 0.25f - LvAppraisal * 0.13f);
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

            bool evaded = EvasionChance > 0f && Random.value < EvasionChance;
            if (evaded)
            {
                ShowToast($"슈킹({gradeLabel}) · +${cash} · +♦{bmPts} · 의심 회피!");
                return;
            }

            float suspGain = item.baseSuspicionGain * mentionMult;
            SuspicionPercent = Mathf.Min(100f, SuspicionPercent + suspGain);
            StealAlertFlash01 = SuspicionPercent >= 80f ? 0.9f : 0.45f;

            if (SuspicionPercent >= 100f) TriggerForcedEviction();
            else if (IsAdrenalineZone) ShowToast($"★ 아드레날린! ({SuspicionPercent:0}%) · 수익 ×{AdrenalineMult:0.#}");
            else ShowToast($"슈킹({gradeLabel}) · +${cash} · +♦{bmPts} · 의심 +{suspGain:0}%");
        }

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

        public void TriggerEarlyCheckout()
        {
            if (!CanEarlyCheckout) return;
            WasEvicted = false;
            WasEarlyCheckout = true;
            LastConfiscatedValue = 0;
            float ratio = Mathf.Clamp01(OverallCleanPercent / 100f);
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult * ratio * 0.85f * VipMoneyMult);
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
                if (CollectingCount > 0) PackageCollectingIntoBag();
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

        // ===== 인게임 스킬트리 가격 ($ 현금 전용) =====
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

        public int CostFist => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostShockwave => Mathf.RoundToInt(70 * Mathf.Pow(1.9f, LvShockwave));
        public int CostBeacon => 60;

        // ===== 클리어 후 [★ 특전] & [♦ 암시장] 가격 =====
        public int CostVipTokens => 1;
        public int CostCompressorTokens => 1;
        public int CostTreasureSenseTokens => 2;

        public int CostAppraisalPts => LvAppraisal == 0 ? 1 : 2;
        public int CostEvasionPts => 1;
        public int CostAdrenalinePts => 2;
        public int CostLaunderPts => 1;

        public bool IsNodeUnlocked(string id)
        {
            switch (id)
            {
                case "tool": case "sweep": case "bag": case "mask": case "fist": return true;
                case "fever": return ToolTier >= 1;
                case "crit": return LvSweepRadius >= 1;
                case "chain": return LvCrit >= 1;
                case "roomba": return LvChainPop >= 1 || ToolTier >= 2;
                case "throw": return LvBagSize >= 1;
                case "price": return LvBagSize >= 1;
                case "truck": return LvThrowPower >= 1;
                case "combo": return LvTrashPrice >= 1;
                case "magnet": return LvTruckSize >= 2;
                case "speed": return LvMask >= 1;
                case "mop": return LvMask >= 1;
                case "scrub": return HasMopUnlocked;
                case "shock": return LvFistPower >= 1;
                case "beacon": return LvFistPower >= 1;
                // 클리어 후 전용 특전/암시장
                case "vip": case "compressor": case "sense": return true;
                case "appraisal": case "evasion": case "adrenaline": case "launder": return true;
                default: return false;
            }
        }

        public int CountAffordableCashUpgrades()
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

            if (IsNodeUnlocked("fist") && LvFistPower < 4 && Money >= CostFist) c++;
            if (IsNodeUnlocked("shock") && LvShockwave < 3 && Money >= CostShockwave) c++;
            if (IsNodeUnlocked("beacon") && !HasBeaconDetect && Money >= CostBeacon) c++;
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

                case "fist": if (LvFistPower >= 4 || Money < CostFist) return false; Money -= CostFist; LvFistPower++; break;
                case "shock": if (LvShockwave >= 3 || Money < CostShockwave) return false; Money -= CostShockwave; LvShockwave++; break;
                case "beacon": if (HasBeaconDetect || Money < CostBeacon) return false; Money -= CostBeacon; HasBeaconDetect = true; break;

                // 클리어 후 [★ 특전]
                case "vip": if (HasVipContract || TrustTokens < CostVipTokens) return false; TrustTokens -= CostVipTokens; HasVipContract = true; break;
                case "compressor": if (HasFastCompressor || TrustTokens < CostCompressorTokens) return false; TrustTokens -= CostCompressorTokens; HasFastCompressor = true; break;
                case "sense": if (HasTreasureSense || TrustTokens < CostTreasureSenseTokens) return false; TrustTokens -= CostTreasureSenseTokens; HasTreasureSense = true; break;

                // 클리어 후 [♦ 암시장]
                case "appraisal": if (LvAppraisal >= 2 || BlackMarketPts < CostAppraisalPts) return false; BlackMarketPts -= CostAppraisalPts; LvAppraisal++; break;
                case "evasion": if (LvEvasion >= 3 || BlackMarketPts < CostEvasionPts) return false; BlackMarketPts -= CostEvasionPts; LvEvasion++; break;
                case "adrenaline": if (HasAdrenalineAmp || BlackMarketPts < CostAdrenalinePts) return false; BlackMarketPts -= CostAdrenalinePts; HasAdrenalineAmp = true; break;
                case "launder": if (BlackMarketPts < CostLaunderPts) return false; BlackMarketPts -= CostLaunderPts; Money += 250; break;

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
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult * VipMoneyMult);
            Money += LastClearBonus;
            LastStarRating = StolenCount == 0 ? 5 : 4;
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void StartNextStage()
        {
            CurrentStage++;
            ResetRoomForRun();
            ShowToast($"STAGE {CurrentStage} 시작 · 수익 ×{StageRewardMult:0.0}");
        }

        public void ReplayRoomKeepUpgrades()
        {
            ResetRoomForRun();
            ShowToast($"STAGE {CurrentStage} 다시 시작");
        }

        private void ResetRoomForRun()
        {
            foreach (var p in _bigPiles) if (p != null) p.ResetForNewRun();
            foreach (var s in _phase2Stains) if (s != null) s.ResetForNewRun();
            int spawnCount = Mathf.Min(2900, InstancedTrashSwarm.InitialSwarmCount + (CurrentStage - 1) * 180);
            if (trashSwarm != null) trashSwarm.ResetSwarm(spawnCount);

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
            _treasuresFoundThisRoom = 0;
            _pilesBrokenThisRoom = 0;
            _mentionedFoundThisRoom = false;
            _treasureCooldown = 0f;
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
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 0.99f, 0.96f), t);
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
