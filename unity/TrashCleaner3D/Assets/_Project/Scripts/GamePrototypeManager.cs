using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        StageMindMap,
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
    /// TrashCleaner3D v1.4 중앙 매니저 (3시간·6단계 맵 인크리멘탈 청소 설계):
    /// - 1단계: +30% 확장 원룸(9.36m × 7.80m, 1,650개) / 2단계: 4방 아파트(거실·주방·안방·화장실, 8,000개 무렉!)
    /// - 1:1 GPU-Instanced 트럭 낱개 투입 & 적재 (허리 아래 낮은 포물선, 시야 가림 0%)
    /// - 수거 차량(쓰레기통→리어카→다마스→1톤 트럭) 3대 핵심 역할:
    ///   ① 적재 한계(120→350→900→무제한) + 가득 차면 4초 자동 비움 대기
    ///   ② 대량 정산 배율(×1.0→×1.20→×1.45→×1.80)
    ///   ③ 다마스 이상 현관 밖 마당 진입 시 원거리 자동 흡입 정산!
    /// - 통합 강화소([E]) 24노드 분할 나뭇가지 스킬트리 (흡입속도 I/II/III · 반경 I/II · 수거통 I/II/III 등)
    /// - 확실한 Before → After 연출 (방 클리어 조명 찰칵 점등 + 환경/안개 대비 2배 강화!)
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public InstancedTrashSwarm trashSwarm;
        public InstancedTruckTrash truckTrashVisual;
        public Transform playerSpawnPoint;
        public Transform truckBedGoal;
        public GameObject[] vehicleStageObjects; // 0:쓰레기통, 1:리어카, 2:다마스, 3:1톤 트럭
        public Transform truckStackedParent;
        public Light ceilingLight;
        public Light sunLight;
        public ProceduralSfx sfx;
        public GameObject thrownBagPrefab;

        [Header("Multi-Stage Room Containers & Lights")]
        public GameObject stage1Container;
        public GameObject stage2Container;
        public Light[] stage1RoomLights;
        public Light[] stage2RoomLights; // 0:거실, 1:주방, 2:안방, 3:화장실
        public float doorThresholdZ = 3.92f; // +30% 확장 방 현관문 경계 Z

        [Header("Integrated Upgrade Kiosk")]
        public bool spawnFieldUpgradeStation = true;
        public FieldUpgradeStation fieldStation;

        [Header("Room Config")]
        public int baseCleanReward = 280;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        public bool KioskOpen => Phase == GamePhase.MindMapUpgrade;
        public bool IsStageMindMapOpen => Phase == GamePhase.StageMindMap;
        public int CurrentStage { get; private set; } = 1;
        public float StageRewardMult => 1f + (CurrentStage - 1) * 0.35f;

        public string CurrentClientHint => CampaignProgressStore.GetInfo(CurrentStage).clientMemo;
        public string CurrentStageTitle => CampaignProgressStore.GetInfo(CurrentStage).title;

        // ===== 재화 & 카운트업 연출 =====
        public int Money { get; private set; } = 0;
        public float DisplayMoney { get; private set; } = 0f;
        public int TrustTokens { get; private set; } = 0;
        public int BlackMarketPts { get; private set; } = 0;

        // ===== 사용자 설정 ([ESC]) =====
        public float MouseSensitivity { get; private set; } = 0.14f;
        public float MasterVolume { get; private set; } = 0.85f;

        // ===== 악취 & 시야 안개 =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; }
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.45f : StenchStage == 3 ? 0.58f : StenchStage == 2 ? 0.72f : StenchStage == 1 ? 0.85f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.42f : StenchStage == 3 ? 0.52f : StenchStage == 2 ? 0.68f : StenchStage == 1 ? 0.84f : 1.0f;
        public float StenchBlur01 => IsOutsideVentilating ? 0f : Mathf.Clamp01((StenchPercent - 22f) / 78f);
        public float StenchGainPerSec => CoreLoopConfig.EnableStenchGimmick ? (CurrentStage == 6 ? 5.1f : 3.2f) * Mathf.Max(0.18f, 1f - TotalMaskLv * 0.10f) : 0f;

        // ===== 의심 (CoreLoopConfig 제어) =====
        public float SuspicionPercent { get; private set; } = 0f;
        public bool IsAdrenalineZone => CoreLoopConfig.EnableSuspicionBranch && SuspicionPercent >= 80f && SuspicionPercent < 100f;
        public float AdrenalineMult => IsAdrenalineZone ? (HasAdrenalineAmp ? 2.5f : 2.0f) : 1.0f;
        public float AdrenalineSpeedMult => IsAdrenalineZone ? 1.85f : 1.0f;

        // ===== v1.4 분할 나뭇가지 스킬트리 레벨 (24노드 — 3시간·6단계 페이싱!) =====
        // 가지 1: 흡입 · 도구
        public int LvHold1 { get; private set; }         // 0~3 (흡입속도 I)
        public int LvRad1 { get; private set; }          // 0~3 (흡입반경 I)
        public bool HasBroom { get; private set; }       // 0~1 (★ 2티어 빗자루 해금)
        public int LvHold2 { get; private set; }         // 0~4 (흡입속도 II)
        public int LvRad2 { get; private set; }          // 0~4 (흡입반경 II)
        public bool HasVacuum { get; private set; }      // 0~1 (★ 3티어 진공청소기 해금)
        public int LvHold3 { get; private set; }         // 0~3 (흡입속도 III)
        public int LvFever { get; private set; }         // 0~3 (피버 엔진)

        // 가지 2: 수거통 · 차량 · 수익
        public int LvBin1 { get; private set; }          // 0~3 (수거통 I)
        public int LvPrice1 { get; private set; }        // 0~3 (정산단가 I)
        public bool HasCart { get; private set; }        // 0~1 (★ 리어카 해금: 적재 350 · 대량 ×1.20)
        public int LvBin2 { get; private set; }          // 0~4 (수거통 II)
        public int LvPrice2 { get; private set; }        // 0~4 (정산단가 II)
        public bool HasDamas { get; private set; }       // 0~1 (★ 다마스 해금: 적재 900 · 대량 ×1.45 · 마당 자동흡입!)
        public int LvBin3 { get; private set; }          // 0~4 (수거통 III)
        public int LvComboTime { get; private set; }     // 0~3 (연속 정산 콤보)
        public bool HasTruck1Ton { get; private set; }   // 0~1 (★ 1톤 트럭: 적재 무제한 · 대량 ×1.80 · 마당 자동흡입!)

        // 가지 3: 기동 · 생존 · 걸레
        public int LvSpeed1 { get; private set; }        // 0~3 (이동속도 I)
        public int LvMask1 { get; private set; }         // 0~3 (방독면 I)
        public bool HasMopUnlocked { get; private set; } // 0~1 (T자 대걸레 해금)
        public int LvSpeed2 { get; private set; }        // 0~3 (이동속도 II)
        public int LvMask2 { get; private set; }         // 0~4 (방독면 II)
        public int LvScrubPower { get; private set; }    // 0~4 (강력 세제)

        // 가지 4: 파괴 · 자동화
        public int LvFist1 { get; private set; }         // 0~3 (주먹 힘 I)
        public int LvRoombaCount { get; private set; }   // 0~3 (로봇청소기 1~3호 — 시작 0대!)
        public int LvRoombaSpd1 { get; private set; }    // 0~3 (로봇 속도 I)
        public int LvFist2 { get; private set; }         // 0~3 (주먹 힘 II)
        public int LvRoombaSpd2 { get; private set; }    // 0~3 (로봇 속도 II)

        // 호환성 합산 프로퍼티
        public int TotalHoldLv => LvHold1 + LvHold2 + LvHold3;             // 0~10
        public int TotalRadLv => LvRad1 + LvRad2;                          // 0~7
        public int TotalBinLv => LvBin1 + LvBin2 + LvBin3;                 // 0~11
        public int TotalPriceLv => LvPrice1 + LvPrice2;                    // 0~7
        public int TotalSpeedLv => LvSpeed1 + LvSpeed2;                    // 0~6
        public int TotalMaskLv => LvMask1 + LvMask2;                       // 0~7
        public int TotalFistLv => LvFist1 + LvFist2;                       // 0~6
        public int TotalRoombaSpdLv => LvRoombaSpd1 + LvRoombaSpd2;        // 0~6

        public int ToolTier => HasVacuum ? 2 : HasBroom ? 1 : 0;
        public bool HasVacuumUnlocked => HasVacuum;
        public string CurrentToolName =>
            HasVacuum ? $"3티어 진공청소기 (속도 +{LvHold3})" :
            HasBroom ? $"2티어 빗자루 (속도 +{LvHold2})" :
            LvHold1 > 0 ? $"1티어 장갑 맨손 (+{LvHold1})" : "1티어 장갑 맨손";

        public int VehicleVisualStage => HasTruck1Ton ? 3 : HasDamas ? 2 : HasCart ? 1 : 0;
        public string CurrentVehicleName =>
            HasTruck1Ton ? "1톤 청소 트럭" :
            HasDamas ? "다마스 (자동흡입)" :
            HasCart ? "리어카" : "공공 쓰레기통";

        // ===== 요청 #5: 수거 차량 3대 핵심 역할 =====
        public int VehicleMaxLoad => VehicleVisualStage == 0 ? 120 : VehicleVisualStage == 1 ? 350 : VehicleVisualStage == 2 ? 900 : int.MaxValue;
        public bool VehicleHasUnlimitedLoad => VehicleVisualStage >= 3;
        public int VehicleCurrentLoad { get; private set; } = 0;
        public float VehicleDispatchTimer { get; private set; } = 0f;
        public const float VehicleDispatchDuration = 4.0f;
        public bool IsVehicleDispatching => VehicleDispatchTimer > 0f;
        public bool HasAutoVehicleSuction => VehicleVisualStage >= 2; // 다마스 / 1톤 트럭 해금 시 마당 자동흡입 정산!
        public float VehicleMaxBulkMult => VehicleVisualStage == 0 ? 1.00f : VehicleVisualStage == 1 ? 1.20f : VehicleVisualStage == 2 ? 1.45f : 1.80f;

        // ===== 연속 흡입 피버 =====
        public float ContinuousFever01 { get; private set; } = 0f;
        private float _feverDecayDelay = 0f;
        public bool HasFeverEngine => LvFever > 0;
        public bool IsFeverHot => ContinuousFever01 >= 0.75f;
        public float FeverRadiusBonus => ContinuousFever01 * (0.04f + LvFever * 0.10f);
        public float FeverSpeedBonus => ContinuousFever01 * (0.08f + LvFever * 0.16f);
        public float FeverPriceMult => (LvFever > 0 && IsFeverHot) ? (1.15f + LvFever * 0.12f) : 1.0f;

        // ===== 수거통 (초반 35칸 → 11단계 최대 1,500칸!) =====
        private static readonly int[] BinCaps = { 35, 50, 70, 95, 130, 180, 250, 340, 480, 680, 980, 1500 };
        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int BinCapacity => BinCaps[Mathf.Clamp(TotalBinLv, 0, BinCaps.Length - 1)] * (HasVacuum ? 2 : 1);
        public float BinFill01 => Mathf.Clamp01(CollectingCount / (float)Mathf.Max(1, BinCapacity));
        public bool IsBinFull => CollectingCount >= BinCapacity;
        public bool CanSettle => CollectingCount > 0 && !IsVehicleDispatching;

        // ===== 파생 스탯 =====
        private static readonly float[] HoldDurCurve = { 1.00f, 0.84f, 0.70f, 0.56f, 0.42f, 0.33f, 0.25f, 0.19f, 0.14f, 0.10f, 0.07f };
        public float EffectiveHoldDuration
        {
            get
            {
                float baseDur = HoldDurCurve[Mathf.Clamp(TotalHoldLv, 0, HoldDurCurve.Length - 1)];
                if (HasBroom) baseDur *= 0.86f;
                if (HasVacuum) baseDur *= 0.75f;
                float workMult = Mathf.Max(0.45f, StenchWorkSpeedMult);
                float totalSpd = workMult * (1f + FeverSpeedBonus) * AdrenalineSpeedMult;
                return Mathf.Clamp(baseDur / totalSpd, 0.045f, 1.35f);
            }
        }

        public float SweepRadius
        {
            get
            {
                float toolBase = HasVacuum ? 0.62f : HasBroom ? 0.34f : 0.15f;
                float r = toolBase + TotalRadLv * 0.11f;
                return r * (1f + FeverRadiusBonus);
            }
        }

        public int AutoGrabPerTick
        {
            get
            {
                if (HasVacuum) return 8 + LvRad2 * 2 + LvHold3 * 2;
                if (HasBroom) return 3 + LvRad1 / 2 + LvRad2;
                return 1 + LvRad1 / 3;
            }
        }

        public float CritChance => 0f;
        public float ChainChance => 0f;
        public int ChainExtraCount => 0;
        public float MoveSpeedTotalMult => StenchMoveSpeedMult * (1f + TotalSpeedLv * 0.10f) * (IsFeverHot && HasFeverEngine ? 1.12f : 1f);
        public float EffectiveTrashPriceMult => (1f + TotalPriceLv * 0.18f) * StageRewardMult * (HasVipContract ? 1.5f : 1f) * FeverPriceMult;
        public float FistDamage => 1.35f + TotalFistLv * 0.95f + (HasVacuum ? 1.6f : 0f);
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.4f : 1.0f) * (1f + LvScrubPower * 0.38f);
        public int RoombaCount => LvRoombaCount;
        public float RoombaEatInterval => Mathf.Max(0.48f, 1.00f - TotalRoombaSpdLv * 0.085f);
        public float RoombaMoveSpeed => 1.95f + TotalRoombaSpdLv * 0.14f;

        // ===== 연속 정산 콤보 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;
        public float MaxComboWindow => 6.0f + LvComboTime * 2.5f;

        // ===== 특전/암시장 호환 =====
        public bool HasVipContract { get; private set; }
        public bool HasFastCompressor { get; private set; }
        public bool HasTreasureSense { get; private set; }
        public int LvAppraisal { get; private set; }
        public int LvEvasion { get; private set; }
        public bool HasAdrenalineAmp { get; private set; }
        public bool HasBeaconDetect => true;

        // ===== 로봇 & 월드 팝업 =====
        private readonly List<AutoRoombaCleaner> _roombas = new List<AutoRoombaCleaner>();
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 진행도 =====
        private readonly List<CleanableTarget> _bigPiles = new List<CleanableTarget>();
        private readonly List<CleanableTarget> _phase2Stains = new List<CleanableTarget>();
        private readonly List<CleanableTarget> _activeBigPiles = new List<CleanableTarget>();
        private readonly List<CleanableTarget> _activeStains = new List<CleanableTarget>();

        public int Phase1Total => _activeBigPiles.Count * 10 + (trashSwarm != null ? trashSwarm.TotalCount : 0);
        public int Phase1Cleaned
        {
            get
            {
                int done = 0;
                foreach (var p in _activeBigPiles) if (p == null || p.IsCleaned) done++;
                return done * 10 + (trashSwarm != null ? trashSwarm.CleanedCount : 0);
            }
        }
        public float Phase1Percent => Phase1Total > 0 ? Mathf.Clamp01(Phase1Cleaned / (float)Phase1Total) * 100f : 100f;
        public int Phase2Total => _activeStains.Count;
        public int Phase2Cleaned
        {
            get
            {
                int done = 0;
                foreach (var s in _activeStains) if (s == null || s.IsCleaned) done++;
                return done;
            }
        }
        public float Phase2Percent => Phase2Total > 0 ? Mathf.Clamp01(Phase2Cleaned / (float)Phase2Total) * 100f : 0f;

        /// <summary>
        /// 전체 진행률 = 1단계(바닥 쓰레기) 80% + 2단계(얼룩) 20%.
        /// [버그 수정 v1.5.1] 바닥 얼룩이 없는 스테이지(Stage 2~7)에서 2단계 몫 20%를 그대로 더해
        /// 시작하자마자 20%로 보이던 문제를 고쳤다. 실제로 존재하는 단계에만 가중치를 나눠 준다.
        /// </summary>
        public float OverallCleanPercent
        {
            get
            {
                bool hasTrash = Phase1Total > 0;
                bool hasStain = Phase2Total > 0;
                if (!hasTrash && !hasStain) return 100f;

                float w1 = hasTrash ? 0.80f : 0f;
                float w2 = hasStain ? 0.20f : 0f;
                float sum = w1 + w2;
                return (Phase1Percent * w1 + Phase2Percent * w2) / sum;
            }
        }
        public bool CanEarlyCheckout => OverallCleanPercent >= 80f && (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping);

        // ===== 정산/배너 =====
        public DiscoveredTreasure ActiveTreasure { get; private set; }
        public float TreasureDecisionLockTimer { get; private set; }
        public float TreasurePopupOpenAge { get; private set; }
        public float TreasureFlash01 { get; private set; }
        public float StealAlertFlash01 { get; private set; }
        public float RoomClearFlash01 { get; private set; }
        public float AppraisalBannerTimer { get; private set; }
        public int AppraisalGrade { get; private set; }
        public string AppraisalBannerTitle { get; private set; } = "";
        public string AppraisalBannerSub { get; private set; } = "";
        public float CompressionBannerTimer { get; private set; }
        public string CompressionBannerText { get; private set; } = "";
        public int StolenCount { get; private set; }
        public int ReturnedCount { get; private set; }
        public bool WasEvicted { get; private set; }
        public bool WasEarlyCheckout { get; private set; }
        public int LastClearBonus { get; private set; }
        public int LastConfiscatedValue { get; private set; }
        public int LastForfeitedBmPts { get; private set; }
        public string LastStolenItemName { get; private set; } = "";
        public int TotalCompressionsThisRoom { get; private set; }
        public int LastStarRating { get; private set; } = 5;

        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;
        private float _pitchCombo;
        private float _pitchResetTimer;
        private float _atmoCleanSmoothed;
        private float _autoSuctionCooldown;

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
            MouseSensitivity = PlayerPrefs.GetFloat("TC3D_MouseSens", 0.14f);
            MasterVolume = PlayerPrefs.GetFloat("TC3D_MasterVol", 0.85f);
            AudioListener.volume = MasterVolume;
        }

        private void Start()
        {
            // 현재 로드된 씬 이름(Room01 / Stage02~07)으로 스테이지 번호 자동 판별
            UiCursorFree = false;
            CurrentStage = CampaignProgressStore.DetectStageFromActiveScene();
            RestoreStateFromCampaignStore();

            // Stage 2~7 뼈대 씬에서는 Stage 1용 스웜이 비활성화돼 있으므로, 찾아서 활성화 후 해당 스테이지 규격으로 스폰!
            if (trashSwarm == null)
            {
                trashSwarm = FindFirstObjectByType<InstancedTrashSwarm>(FindObjectsInactive.Include);
            }
            if (trashSwarm != null)
            {
                if (!trashSwarm.gameObject.activeSelf) trashSwarm.gameObject.SetActive(true);
                trashSwarm.manager = this;
                trashSwarm.SpawnForStage(CurrentStage);
            }

            if (player != null) player.mouseSensitivity = MouseSensitivity;
            if (truckTrashVisual == null) truckTrashVisual = gameObject.AddComponent<InstancedTruckTrash>();
            if (trashSwarm != null)
            {
                trashSwarm.RoomCleared += OnRoomCleared;
                truckTrashVisual.Init(trashSwarm, truckStackedParent);
            }

            ApplyStageContainerVisibility();
            CollectTargetsForCurrentStage();
            UpdateVehicleStageVisuals();
            if (spawnFieldUpgradeStation) EnsureFieldUpgradeStation();
            ApplyAtmosphereImmediate();
            EnsureEntranceDoorClosedAtStart();

            // 로봇청소기 보유 수만큼 현재 스테이지에 자동 배치
            RespawnOwnedRoombas();

            // Play 시작 시(또는 스테이지 맵 복귀 시) 7종 스테이지 마인드맵 즉시 오픈!
            if (CampaignProgressStore.OpenMindMapOnNextLoad)
            {
                _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;
                Phase = GamePhase.StageMindMap;
                UnlockCursor(true);
            }
            else
            {
                Phase = GamePhase.Phase1_TrashRemoval;
            }
        }

        private void RespawnOwnedRoombas()
        {
            _roombas.Clear();
            for (int i = 0; i < LvRoombaCount; i++)
            {
                _roombas.Add(AutoRoombaCleaner.Spawn(this, new Vector3(Random.Range(-1.2f, 1.2f), 0.05f, Random.Range(-1.2f, 1.2f)), i + 1));
            }
        }

        public void SaveStateToCampaignStore()
        {
            CampaignProgressStore.HasSavedPlayerState = true;
            CampaignProgressStore.SavedMoney = Money;
            CampaignProgressStore.SavedTrustTokens = TrustTokens;
            CampaignProgressStore.SavedBlackMarketPts = BlackMarketPts;
            CampaignProgressStore.LvHold1 = LvHold1;
            CampaignProgressStore.LvRad1 = LvRad1;
            CampaignProgressStore.HasBroom = HasBroom;
            CampaignProgressStore.LvHold2 = LvHold2;
            CampaignProgressStore.LvRad2 = LvRad2;
            CampaignProgressStore.HasVacuum = HasVacuum;
            CampaignProgressStore.LvHold3 = LvHold3;
            CampaignProgressStore.LvFever = LvFever;
            CampaignProgressStore.LvBin1 = LvBin1;
            CampaignProgressStore.LvPrice1 = LvPrice1;
            CampaignProgressStore.HasCart = HasCart;
            CampaignProgressStore.LvBin2 = LvBin2;
            CampaignProgressStore.LvPrice2 = LvPrice2;
            CampaignProgressStore.HasDamas = HasDamas;
            CampaignProgressStore.LvBin3 = LvBin3;
            CampaignProgressStore.LvComboTime = LvComboTime;
            CampaignProgressStore.HasTruck1Ton = HasTruck1Ton;
            CampaignProgressStore.LvSpeed1 = LvSpeed1;
            CampaignProgressStore.LvMask1 = LvMask1;
            CampaignProgressStore.HasMopUnlocked = HasMopUnlocked;
            CampaignProgressStore.LvSpeed2 = LvSpeed2;
            CampaignProgressStore.LvMask2 = LvMask2;
            CampaignProgressStore.LvScrubPower = LvScrubPower;
            CampaignProgressStore.LvFist1 = LvFist1;
            CampaignProgressStore.LvRoombaCount = LvRoombaCount;
            CampaignProgressStore.LvRoombaSpd1 = LvRoombaSpd1;
            CampaignProgressStore.LvFist2 = LvFist2;
            CampaignProgressStore.LvRoombaSpd2 = LvRoombaSpd2;
        }

        private void RestoreStateFromCampaignStore()
        {
            if (!CampaignProgressStore.HasSavedPlayerState) return;
            Money = CampaignProgressStore.SavedMoney;
            DisplayMoney = Money;
            TrustTokens = CampaignProgressStore.SavedTrustTokens;
            BlackMarketPts = CampaignProgressStore.SavedBlackMarketPts;
            LvHold1 = CampaignProgressStore.LvHold1;
            LvRad1 = CampaignProgressStore.LvRad1;
            HasBroom = CampaignProgressStore.HasBroom;
            LvHold2 = CampaignProgressStore.LvHold2;
            LvRad2 = CampaignProgressStore.LvRad2;
            HasVacuum = CampaignProgressStore.HasVacuum;
            LvHold3 = CampaignProgressStore.LvHold3;
            LvFever = CampaignProgressStore.LvFever;
            LvBin1 = CampaignProgressStore.LvBin1;
            LvPrice1 = CampaignProgressStore.LvPrice1;
            HasCart = CampaignProgressStore.HasCart;
            LvBin2 = CampaignProgressStore.LvBin2;
            LvPrice2 = CampaignProgressStore.LvPrice2;
            HasDamas = CampaignProgressStore.HasDamas;
            LvBin3 = CampaignProgressStore.LvBin3;
            LvComboTime = CampaignProgressStore.LvComboTime;
            HasTruck1Ton = CampaignProgressStore.HasTruck1Ton;
            LvSpeed1 = CampaignProgressStore.LvSpeed1;
            LvMask1 = CampaignProgressStore.LvMask1;
            HasMopUnlocked = CampaignProgressStore.HasMopUnlocked;
            LvSpeed2 = CampaignProgressStore.LvSpeed2;
            LvMask2 = CampaignProgressStore.LvMask2;
            LvScrubPower = CampaignProgressStore.LvScrubPower;
            LvFist1 = CampaignProgressStore.LvFist1;
            LvRoombaCount = CampaignProgressStore.LvRoombaCount;
            LvRoombaSpd1 = CampaignProgressStore.LvRoombaSpd1;
            LvFist2 = CampaignProgressStore.LvFist2;
            LvRoombaSpd2 = CampaignProgressStore.LvRoombaSpd2;
        }

        private void EnsureEntranceDoorClosedAtStart()
        {
            var door = FindFirstObjectByType<InteractiveDoor>();
            if (door != null)
            {
                door.manager = this;
                door.ForceCloseImmediate();
                Physics.SyncTransforms(); // 문 각도/콜라이더 즉시 물리 동기화

                // 모든 스테이지(Stage 1~7)에서 무조건 닫힌 노란 현관문 정면 1.25m 앞에서 시작!
                if (player != null)
                {
                    Vector3 doorCenter = door.doorCollider != null
                        ? door.doorCollider.transform.position
                        : door.transform.position;
                    float floorY = door.doorCollider != null
                        ? door.doorCollider.transform.position.y - door.doorCollider.transform.lossyScale.y * 0.5f
                        : 0f;
                    Vector3 frontPos = new Vector3(doorCenter.x, Mathf.Max(0.05f, floorY + 0.08f), doorCenter.z + 1.25f);
                    Quaternion faceDoor = Quaternion.Euler(0f, 180f, 0f);
                    player.TeleportTo(frontPos, faceDoor);
                    if (playerSpawnPoint != null)
                        playerSpawnPoint.SetPositionAndRotation(frontPos, faceDoor);
                    Physics.SyncTransforms();
                }
            }
        }

        private void OnDestroy()
        {
            if (trashSwarm != null) trashSwarm.RoomCleared -= OnRoomCleared;
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
            switch (VehicleVisualStage)
            {
                case 0: return new Vector3(0.42f, 0.58f, 0.42f);
                case 1: return new Vector3(0.70f, 0.58f, 0.64f);
                case 2: return new Vector3(0.98f, 0.60f, 0.80f);
                default: return new Vector3(1.32f, 0.65f, 1.10f);
            }
        }

        private void ApplyStageContainerVisibility()
        {
            // 독립 씬(Stage 2~7)에서는 stage2Container == null이므로 현재 방(stage1Container)을 반드시 활성 유지!
            if (stage2Container != null)
            {
                bool isSt2 = CurrentStage >= 2;
                if (stage1Container != null) stage1Container.SetActive(!isSt2);
                stage2Container.SetActive(isSt2);
            }
            else if (stage1Container != null)
            {
                stage1Container.SetActive(true);
            }

            // 방 조명 초기 'Before' 칙칙한 상태로 리셋
            ResetRoomLightsToDirty();
        }

        private void ResetRoomLightsToDirty()
        {
            Light[] activeLights = CurrentStage >= 2 ? stage2RoomLights : stage1RoomLights;
            if (activeLights != null)
            {
                foreach (var l in activeLights)
                {
                    if (l == null) continue;
                    l.color = new Color(0.95f, 0.78f, 0.54f);
                    l.intensity = 9.5f;
                }
            }
            if (ceilingLight != null)
            {
                ceilingLight.color = new Color(0.95f, 0.78f, 0.54f);
                ceilingLight.intensity = 10f;
            }
        }

        private void CollectTargetsForCurrentStage()
        {
            _activeBigPiles.Clear();
            _activeStains.Clear();
            GameObject activeRoot = (stage2Container != null && CurrentStage >= 2) ? stage2Container : stage1Container;
            if (activeRoot != null)
            {
                foreach (var c in activeRoot.GetComponentsInChildren<CleanableTarget>(true))
                {
                    if (c.kind == CleanableKind.FloorStain) _activeStains.Add(c);
                    else if (c.kind == CleanableKind.TrashPile) _activeBigPiles.Add(c);
                }
            }
            else
            {
                foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
                {
                    if (c.kind == CleanableKind.FloorStain) _activeStains.Add(c);
                    else if (c.kind == CleanableKind.TrashPile) _activeBigPiles.Add(c);
                }
            }
        }

        private void EnsureFieldUpgradeStation()
        {
            if (fieldStation != null) return;
            var existing = FindFirstObjectByType<FieldUpgradeStation>();
            if (existing != null) { fieldStation = existing; fieldStation.Init(this, 0f); return; }

            Vector3 pos = truckBedGoal != null
                ? new Vector3(truckBedGoal.position.x + 3.0f, 0f, truckBedGoal.position.z - 0.8f)
                : new Vector3(5.1f, 0f, 10.6f);
            GameObject go = new GameObject("FieldUpgradeStation_Kiosk");
            go.transform.position = pos;
            fieldStation = go.AddComponent<FieldUpgradeStation>();
            fieldStation.Init(this, -135f);
        }

        public bool IsPlayerInTruckSettleZone(Vector3 playerPos)
        {
            if (truckBedGoal == null) return false;
            Vector3 d = playerPos - truckBedGoal.position;
            d.y = 0f;
            float reach = VehicleVisualStage == 0 ? 2.6f : VehicleVisualStage == 1 ? 3.2f : 4.2f;
            return d.magnitude <= reach;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 돈 드르르륵 카운트업
            if (Mathf.Abs(DisplayMoney - Money) > 0.5f)
            {
                float spd = Mathf.Max(80f, Mathf.Abs(Money - DisplayMoney) * 5.2f);
                DisplayMoney = Mathf.MoveTowards(DisplayMoney, Money, spd * dt);
            }
            else DisplayMoney = Money;

            if (_toastTimer > 0f) { _toastTimer -= dt; if (_toastTimer <= 0f) ToastMessage = ""; }
            if (_pitchResetTimer > 0f) { _pitchResetTimer -= dt; if (_pitchResetTimer <= 0f) _pitchCombo = 0f; }
            if (TreasureFlash01 > 0f) TreasureFlash01 = Mathf.MoveTowards(TreasureFlash01, 0f, dt * 1.2f);
            if (StealAlertFlash01 > 0f) StealAlertFlash01 = Mathf.MoveTowards(StealAlertFlash01, 0f, dt * 1.2f);
            if (RoomClearFlash01 > 0f) RoomClearFlash01 = Mathf.MoveTowards(RoomClearFlash01, 0f, dt * 1.5f);
            if (CoughKick01 > 0f) CoughKick01 = Mathf.MoveTowards(CoughKick01, 0f, dt * 3.8f);
            if (FreshAirFlash01 > 0f) FreshAirFlash01 = Mathf.MoveTowards(FreshAirFlash01, 0f, dt * 1.8f);
            if (_autoSuctionCooldown > 0f) _autoSuctionCooldown -= dt;

            // 차량 꽉 참 → 4초 자동 수거차 비움 대기
            if (VehicleDispatchTimer > 0f)
            {
                VehicleDispatchTimer -= dt;
                if (VehicleDispatchTimer <= 0f)
                {
                    VehicleDispatchTimer = 0f;
                    VehicleCurrentLoad = 0;
                    if (truckTrashVisual != null) truckTrashVisual.ClearStackedVisuals();
                    sfx.PlayChime();
                    ShowToast($"{CurrentVehicleName} 비움 완료!");
                }
            }

            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].age += dt;
                FloatingTexts[i].worldPos += Vector3.up * (0.65f * dt);
                if (FloatingTexts[i].age >= FloatingTexts[i].duration) FloatingTexts.RemoveAt(i);
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            if (ThrowComboTimer > 0f) { ThrowComboTimer -= dt; if (ThrowComboTimer <= 0f) ThrowComboCount = 0; }
            if (_feverDecayDelay > 0f) _feverDecayDelay -= dt;
            else if (ContinuousFever01 > 0f) ContinuousFever01 = Mathf.MoveTowards(ContinuousFever01, 0f, dt * 0.28f);

            // 환기 + 다마스/1톤트럭 마당 진입 시 원거리 자동흡입 정산!
            if (player != null)
            {
                bool wasOutside = IsOutsideVentilating;
                IsOutsideVentilating = player.transform.position.z > doorThresholdZ;
                if (IsOutsideVentilating)
                {
                    if (!wasOutside && StenchPercent >= 20f) { FreshAirFlash01 = 1f; sfx.PlayChime(); }
                    if (StenchPercent > 0.1f) StenchPercent = Mathf.Max(0f, StenchPercent - 48f * dt);

                    // 요청 #5: 다마스/1톤 트럭 해금 시 마당에 발만 내디뎌도 자동 흡입 정산!
                    if (HasAutoVehicleSuction && CanSettle && _autoSuctionCooldown <= 0f && player.transform.position.z > doorThresholdZ + 0.45f)
                    {
                        _autoSuctionCooldown = 1.6f;
                        Vector3 lowOrigin = GetLowRightPourOrigin();
                        SettleBinAtTruck(lowOrigin, true);
                    }
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
                    if (StenchStage >= 3) CoughKick01 = StenchStage == 4 ? 0.9f : 0.55f;
                    sfx.PlayScrub();
                }
            }

            // 확실한 Before → After 부드러운 보간!
            UpdateAtmosphereSmooth(dt);
        }

        public Vector3 GetLowRightPourOrigin()
        {
            if (player != null && player.cameraTransform != null)
            {
                Transform cam = player.cameraTransform;
                return cam.position - cam.up * 0.45f + cam.right * 0.34f + cam.forward * 0.42f;
            }
            return player != null ? player.transform.position + Vector3.up * 0.95f : Vector3.zero;
        }

        /// <summary>
        /// 방 단위 클리어 연출 (Before → After 조명 찰칵 점등 + 스파클 + 보너스!)
        /// </summary>
        private void OnRoomCleared(int roomIdx, string roomName, Vector3 roomCenter)
        {
            int bonus = CurrentStage >= 2 ? Mathf.RoundToInt(140 * StageRewardMult) : 60;
            Money += bonus;
            RoomClearFlash01 = 0.85f;
            sfx.PlayFanfare();
            if (player != null) player.AddCameraShake(0.032f, 0.14f);

            // 해당 방 천장등 어두운 황토 → 환한 백색등으로 "찰칵!" 점등!
            Light[] activeLights = CurrentStage >= 2 ? stage2RoomLights : stage1RoomLights;
            if (activeLights != null && roomIdx >= 0 && roomIdx < activeLights.Length && activeLights[roomIdx] != null)
            {
                activeLights[roomIdx].color = new Color(1.0f, 0.99f, 0.96f);
                activeLights[roomIdx].intensity = 34f;
            }

            SparkleBurst.Spawn(roomCenter + Vector3.up * 0.25f, 22, 1.45f, new Color(1.0f, 0.95f, 0.65f));
            SpawnFloatingText(roomCenter + Vector3.up * 1.1f, $"★ {roomName} 청소 완료! +${bonus}", new Color(1f, 0.94f, 0.42f), 2.5f);

            if (trashSwarm != null && trashSwarm.RoomCount > 1)
                ShowToast($"★ [{roomName}] 클리어! ({trashSwarm.ClearedRoomCount}/{trashSwarm.RoomCount}) +${bonus}");
            else
                ShowToast($"★ [{roomName}] 바닥 쓰레기 완료! +${bonus}");
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.4f)
        {
            FloatingTexts.Add(new FloatingWorldText { worldPos = worldPos, text = text, color = color, age = 0f, duration = duration });
        }

        public void OnInstancedTrashCollected(int itemValue, int typeIdx, string typeLabel)
        {
            int gainedVal = Mathf.Max(1, Mathf.RoundToInt(itemValue * EffectiveTrashPriceMult));
            CollectingCount++;
            CollectingPendingValue += gainedVal;

            _feverDecayDelay = 1.75f;
            ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + (HasFeverEngine ? 0.045f : 0.025f));

            _pitchResetTimer = 0.48f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.06f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.36f);

            if (CollectingCount == BinCapacity)
            {
                sfx.PlayChime();
                ShowToast(HasAutoVehicleSuction ? "수거통 가득 · 문 밖 마당에서 자동 정산!" : "수거통 가득 · 밖 수거함에서 [RMB] 정산");
            }

            CheckPhase1Complete();
        }

        public void OnRobotAutoCleaned(int eatenCount, Vector3 botPos)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(eatenCount * 2 * EffectiveTrashPriceMult));
            Money += pay;
            SpawnFloatingText(botPos + Vector3.up * 0.38f, $"[BOT] +${pay}", new Color(0.35f, 0.94f, 1f), 1.0f);
            CheckPhase1Complete();
        }

        /// <summary>
        /// v1.4 트럭 1:1 정산:
        /// - 보유한 개수 그대로(50개→50개) 허리 아래에서 낮은 아치로 실제 3D 쓰레기가 날아가 적재함에 쌓임!
        /// - 차량 적재 한계(120/350/900/∞) + 대량 정산 배율(×1.0~×1.80) 적용!
        /// </summary>
        public bool SettleBinAtTruck(Vector3 lowPourOrigin, bool triggeredByAutoSuction = false)
        {
            if (CollectingCount <= 0) return false;
            if (IsVehicleDispatching)
            {
                ShowToast($"{CurrentVehicleName} 비우는 중... ({VehicleDispatchTimer:0.0}초)");
                return false;
            }

            int count = CollectingCount;
            int rawBase = Mathf.Max(CollectingPendingValue, count * 2);
            CollectingCount = 0;
            CollectingPendingValue = 0;

            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(5, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = MaxComboWindow;

            float comboMult = 1f + (ThrowComboCount - 1) * 0.20f;
            // 요청 #5: 한 번에 많이 넣을수록 대량 정산 보너스 (차량 등급별 상한!)
            float bulkRatio = Mathf.Clamp01(count / (VehicleVisualStage == 1 ? 35f : VehicleVisualStage == 2 ? 70f : 120f));
            float bulkMult = Mathf.Lerp(1.0f, VehicleMaxBulkMult, bulkRatio);
            int totalPay = Mathf.Max(1, Mathf.RoundToInt(rawBase * comboMult * bulkMult));

            Money += totalPay;
            sfx.PlayChime();
            if (player != null) player.AddCameraShake(0.028f, 0.11f);

            // 요청 #1·#2·#6: 1:1 실제 3D 쓰레기 허리 아래 낮은 아치 투입 & 적재함 쌓임!
            if (truckTrashVisual != null && truckBedGoal != null)
            {
                truckTrashVisual.PourExactCount(lowPourOrigin, truckBedGoal, GetGoalOpeningHalfExtents(), count, VehicleVisualStage);
            }

            // 요청 #5: 차량 적재 한계 체크 (1톤 트럭은 무제한!)
            if (!VehicleHasUnlimitedLoad)
            {
                VehicleCurrentLoad += count;
                if (VehicleCurrentLoad >= VehicleMaxLoad)
                {
                    VehicleCurrentLoad = VehicleMaxLoad;
                    VehicleDispatchTimer = VehicleDispatchDuration;
                    ShowToast($"{CurrentVehicleName} 가득 참! 자동 수거차 비움 ({VehicleDispatchDuration:0}초)");
                }
            }

            string prefix = triggeredByAutoSuction ? "자동흡입! " : (ThrowComboCount >= 2 ? $"{ThrowComboCount}연속! " : "");
            string bulkTag = bulkMult > 1.08f ? $" (×{bulkMult:0.00})" : "";
            Vector3 popPos = truckBedGoal != null ? truckBedGoal.position + Vector3.up * 0.9f : lowPourOrigin + Vector3.up * 0.5f;
            SpawnFloatingText(popPos, $"{prefix}{count}개 정산 +${totalPay}{bulkTag}", new Color(0.45f, 1f, 0.55f), 1.9f);

            CheckPhase1Complete();
            return true;
        }

        public void OnBigPileBurst(CleanableTarget pile)
        {
            sfx.PlayPop(0.62f);
            if (player != null) player.AddCameraShake(0.042f, 0.14f);
            if (trashSwarm != null) trashSwarm.SpawnBurstFountain(pile.transform.position, 22);
            CheckPhase1Complete();
        }

        private void CheckPhase1Complete()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            if (Phase1Cleaned >= Phase1Total && Phase1Total > 0)
            {
                if (CollectingCount > 0)
                {
                    Money += CollectingPendingValue;
                    CollectingCount = 0;
                    CollectingPendingValue = 0;
                }
                if (Phase2Total <= 0)
                {
                    FinishRoomComplete();
                    return;
                }
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                ShowToast("2단계 · 바닥 얼룩 닦기!");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(stain.rewardMoney * EffectiveTrashPriceMult));
            Money += pay;
            sfx.PlayChime();
            SparkleBurst.Spawn(stain.transform.position + Vector3.up * 0.08f, 14, 0.65f, new Color(0.85f, 0.97f, 1f));
            SpawnFloatingText(stain.transform.position + Vector3.up * 0.42f, $"✦ 반짝! +${pay}", new Color(0.55f, 0.96f, 1f), 1.5f);
            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        public void OpenFieldUpgradeKiosk()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;
            _phaseBeforeOverlay = Phase;
            Phase = GamePhase.MindMapUpgrade;
            UnlockCursor(true);
        }

        public void CloseUpgradeKiosk()
        {
            if (Phase != GamePhase.MindMapUpgrade) return;
            Phase = _phaseBeforeOverlay;
            UnlockCursor(false);
        }

        public void ToggleSettingsMenu()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision || Phase == GamePhase.StageMindMap) return;
            if (Phase == GamePhase.SettingsMenu || Phase == GamePhase.MindMapUpgrade) { Phase = _phaseBeforeOverlay; UnlockCursor(false); }
            else { _phaseBeforeOverlay = Phase; Phase = GamePhase.SettingsMenu; UnlockCursor(true); }
        }

        public void OpenStageMindMap()
        {
            SaveStateToCampaignStore();
            if (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping)
                _phaseBeforeOverlay = Phase;
            Phase = GamePhase.StageMindMap;
            UnlockCursor(true);
        }

        public void CloseStageMindMap()
        {
            if (Phase != GamePhase.StageMindMap) return;
            CampaignProgressStore.OpenMindMapOnNextLoad = false;
            Phase = _phaseBeforeOverlay == GamePhase.Phase2_FloorMopping
                ? GamePhase.Phase2_FloorMopping
                : GamePhase.Phase1_TrashRemoval;
            UnlockCursor(false);
        }

        public bool EnterStageFromMindMap(int stageNum)
        {
            stageNum = Mathf.Clamp(stageNum, 1, CampaignProgressStore.TotalStages);
            if (!CampaignProgressStore.IsStageUnlocked(stageNum))
            {
                ShowToast($"Stage {stageNum - 1}을 먼저 클리어해야 해금됩니다!");
                return false;
            }

            var info = CampaignProgressStore.GetInfo(stageNum);
            SaveStateToCampaignStore();
            CampaignProgressStore.OpenMindMapOnNextLoad = false;

            if (SceneManager.GetActiveScene().name == info.sceneName)
            {
                CurrentStage = stageNum;
                Phase = GamePhase.Phase1_TrashRemoval;
                UnlockCursor(false);
                ShowToast($"▶ STAGE {stageNum} · {info.title}");
                return true;
            }

            if (!Application.CanStreamedLevelBeLoaded(info.sceneName))
            {
                ShowToast($"Stage {stageNum} 씬({info.sceneName})이 빌드에 미등록 상태입니다!");
                return false;
            }

            SceneManager.LoadScene(info.sceneName);
            return true;
        }

        public void TriggerEarlyCheckout()
        {
            if (!CanEarlyCheckout) return;
            if (CollectingCount > 0) { Money += CollectingPendingValue; CollectingCount = 0; CollectingPendingValue = 0; }
            WasEvicted = false;
            WasEarlyCheckout = true;
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult * (OverallCleanPercent / 100f) * 0.85f);
            Money += LastClearBonus;
            DisplayMoney = Money;
            LastStarRating = OverallCleanPercent >= 95f ? 5 : OverallCleanPercent >= 88f ? 4 : 3;
            CampaignProgressStore.MarkStageCleared(CurrentStage, LastStarRating);
            SaveStateToCampaignStore();
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ResolveActiveTreasure(bool steal) { ActiveTreasure = null; Phase = _phaseBeforeOverlay; UnlockCursor(false); }

        // ===== v1.4 분할 나뭇가지 스킬트리 가격 & 선행 조건 (3시간·6단계 페이싱!) =====
        public int GetNodeCost(string id)
        {
            switch (id)
            {
                // 가지 1: 흡입 · 도구
                case "hold1": return Mathf.RoundToInt(18 * Mathf.Pow(1.75f, LvHold1));        // 18, 32, 55
                case "rad1": return Mathf.RoundToInt(22 * Mathf.Pow(1.80f, LvRad1));          // 22, 40, 71
                case "broom": return 150;                                                      // ★ 2티어 빗자루
                case "hold2": return Mathf.RoundToInt(140 * Mathf.Pow(1.70f, LvHold2));       // 140, 238, 405, 688
                case "rad2": return Mathf.RoundToInt(165 * Mathf.Pow(1.72f, LvRad2));         // 165, 284, 488, 840
                case "vacuum": return 1350;                                                    // ★ 3티어 진공청소기 (2단계 중후반 목표!)
                case "hold3": return Mathf.RoundToInt(950 * Mathf.Pow(1.80f, LvHold3));       // 950, 1710, 3078
                case "fever": return Mathf.RoundToInt(220 * Mathf.Pow(1.90f, LvFever));       // 220, 418, 794

                // 가지 2: 수거통 · 차량 · 수익
                case "bin1": return Mathf.RoundToInt(20 * Mathf.Pow(1.75f, LvBin1));          // 20, 35, 61
                case "price1": return Mathf.RoundToInt(28 * Mathf.Pow(1.80f, LvPrice1));      // 28, 50, 91
                case "cart": return 130;                                                       // ★ 리어카 (적재 350 · ×1.20)
                case "bin2": return Mathf.RoundToInt(145 * Mathf.Pow(1.70f, LvBin2));         // 145..712
                case "price2": return Mathf.RoundToInt(180 * Mathf.Pow(1.75f, LvPrice2));     // 180..965
                case "damas": return 680;                                                      // ★ 다마스 (적재 900 · ×1.45 · 마당 자동흡입!)
                case "bin3": return Mathf.RoundToInt(780 * Mathf.Pow(1.72f, LvBin3));         // 780..3970
                case "combo": return Mathf.RoundToInt(120 * Mathf.Pow(1.85f, LvComboTime));   // 120, 222, 411
                case "truck1t": return 2200;                                                   // ★ 1톤 트럭 (무제한 · ×1.80 · 자동흡입!)

                // 가지 3: 기동 · 생존 · 걸레
                case "speed1": return Mathf.RoundToInt(24 * Mathf.Pow(1.75f, LvSpeed1));      // 24, 42, 74
                case "mask1": return Mathf.RoundToInt(22 * Mathf.Pow(1.72f, LvMask1));        // 22, 38, 65
                case "mop": return 95;                                                         // ★ T자 대걸레
                case "speed2": return Mathf.RoundToInt(190 * Mathf.Pow(1.80f, LvSpeed2));     // 190, 342, 616
                case "mask2": return Mathf.RoundToInt(160 * Mathf.Pow(1.75f, LvMask2));       // 160..858
                case "scrub": return Mathf.RoundToInt(130 * Mathf.Pow(1.75f, LvScrubPower));  // 130..697

                // 가지 4: 파괴 · 자동화
                case "fist1": return Mathf.RoundToInt(25 * Mathf.Pow(1.75f, LvFist1));        // 25, 44, 77
                case "roomba": return LvRoombaCount == 0 ? 280 : LvRoombaCount == 1 ? 820 : 2100;
                case "rspd1": return Mathf.RoundToInt(160 * Mathf.Pow(1.75f, LvRoombaSpd1));
                case "fist2": return Mathf.RoundToInt(210 * Mathf.Pow(1.80f, LvFist2));
                case "rspd2": return Mathf.RoundToInt(650 * Mathf.Pow(1.80f, LvRoombaSpd2));
                default: return 999999;
            }
        }

        public bool IsNodeUnlocked(string id)
        {
            switch (id)
            {
                // 4방향 루트 시작 노드
                case "hold1": case "bin1": case "speed1": case "fist1": return true;

                // 가지 1 (흡입·도구)
                case "rad1": return LvHold1 >= 1;
                case "broom": return LvHold1 >= 2 && LvRad1 >= 1;
                case "hold2": return HasBroom;
                case "rad2": return HasBroom;
                case "fever": return LvHold2 >= 1;
                case "vacuum": return LvHold2 >= 3 && LvRad2 >= 2;
                case "hold3": return HasVacuum;

                // 가지 2 (수거통·차량·수익)
                case "price1": return LvBin1 >= 1;
                case "cart": return LvBin1 >= 2;
                case "bin2": return HasCart;
                case "price2": return HasCart && LvPrice1 >= 2;
                case "combo": return LvPrice1 >= 2;
                case "damas": return LvBin2 >= 2 && LvPrice2 >= 1;
                case "bin3": return HasDamas;
                case "truck1t": return HasDamas && LvBin3 >= 2;

                // 가지 3 (기동·생존·걸레)
                case "mask1": return LvSpeed1 >= 1;
                case "mop": return LvMask1 >= 1;
                case "speed2": return LvSpeed1 >= 3;
                case "mask2": return LvMask1 >= 3;
                case "scrub": return HasMopUnlocked;

                // 가지 4 (파괴·자동화)
                case "roomba": return LvFist1 >= 2;
                case "rspd1": return LvRoombaCount >= 1;
                case "fist2": return LvFist1 >= 3;
                case "rspd2": return LvRoombaCount >= 2 && LvRoombaSpd1 >= 2;
                default: return false;
            }
        }

        public int GetNodeLevel(string id) => id switch
        {
            "hold1" => LvHold1, "rad1" => LvRad1, "broom" => HasBroom ? 1 : 0, "hold2" => LvHold2, "rad2" => LvRad2, "vacuum" => HasVacuum ? 1 : 0, "hold3" => LvHold3, "fever" => LvFever,
            "bin1" => LvBin1, "price1" => LvPrice1, "cart" => HasCart ? 1 : 0, "bin2" => LvBin2, "price2" => LvPrice2, "damas" => HasDamas ? 1 : 0, "bin3" => LvBin3, "combo" => LvComboTime, "truck1t" => HasTruck1Ton ? 1 : 0,
            "speed1" => LvSpeed1, "mask1" => LvMask1, "mop" => HasMopUnlocked ? 1 : 0, "speed2" => LvSpeed2, "mask2" => LvMask2, "scrub" => LvScrubPower,
            "fist1" => LvFist1, "roomba" => LvRoombaCount, "rspd1" => LvRoombaSpd1, "fist2" => LvFist2, "rspd2" => LvRoombaSpd2,
            _ => 0
        };

        public int GetNodeMaxLevel(string id) => id switch
        {
            "hold1" => 3, "rad1" => 3, "broom" => 1, "hold2" => 4, "rad2" => 4, "vacuum" => 1, "hold3" => 3, "fever" => 3,
            "bin1" => 3, "price1" => 3, "cart" => 1, "bin2" => 4, "price2" => 4, "damas" => 1, "bin3" => 4, "combo" => 3, "truck1t" => 1,
            "speed1" => 3, "mask1" => 3, "mop" => 1, "speed2" => 3, "mask2" => 4, "scrub" => 4,
            "fist1" => 3, "roomba" => 3, "rspd1" => 3, "fist2" => 3, "rspd2" => 3,
            _ => 1
        };

        public int CountAffordableCashUpgrades()
        {
            string[] all = {
                "hold1","rad1","broom","hold2","rad2","vacuum","hold3","fever",
                "bin1","price1","cart","bin2","price2","damas","bin3","combo","truck1t",
                "speed1","mask1","mop","speed2","mask2","scrub",
                "fist1","roomba","rspd1","fist2","rspd2"
            };
            int c = 0;
            foreach (var id in all)
                if (IsNodeUnlocked(id) && GetNodeLevel(id) < GetNodeMaxLevel(id) && Money >= GetNodeCost(id)) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            if (!IsNodeUnlocked(id)) return false;
            int lv = GetNodeLevel(id), maxLv = GetNodeMaxLevel(id);
            if (lv >= maxLv) return false;
            int cost = GetNodeCost(id);
            if (Money < cost) return false;

            Money -= cost;
            switch (id)
            {
                case "hold1": LvHold1++; break;
                case "rad1": LvRad1++; break;
                case "broom": HasBroom = true; ShowToast("★ 2티어 빗자루 해금!"); break;
                case "hold2": LvHold2++; break;
                case "rad2": LvRad2++; break;
                case "vacuum": HasVacuum = true; ShowToast("★ 3티어 진공청소기 해금!"); break;
                case "hold3": LvHold3++; break;
                case "fever": LvFever++; break;

                case "bin1": LvBin1++; break;
                case "price1": LvPrice1++; break;
                case "cart": HasCart = true; UpdateVehicleStageVisuals(); ShowToast("★ 리어카 해금! (적재 350 · 대량 ×1.20)"); break;
                case "bin2": LvBin2++; break;
                case "price2": LvPrice2++; break;
                case "damas": HasDamas = true; UpdateVehicleStageVisuals(); ShowToast("★ 다마스 해금! (마당 자동흡입 정산!)"); break;
                case "bin3": LvBin3++; break;
                case "combo": LvComboTime++; break;
                case "truck1t": HasTruck1Ton = true; UpdateVehicleStageVisuals(); ShowToast("★ 1톤 트럭 해금! (적재 무제한 · 대량 ×1.80)"); break;

                case "speed1": LvSpeed1++; break;
                case "mask1": LvMask1++; break;
                case "mop": HasMopUnlocked = true; ShowToast("★ T자 대걸레 해금!"); break;
                case "speed2": LvSpeed2++; break;
                case "mask2": LvMask2++; break;
                case "scrub": LvScrubPower++; break;

                case "fist1": LvFist1++; break;
                case "roomba":
                    LvRoombaCount++;
                    _roombas.Add(AutoRoombaCleaner.Spawn(this, new Vector3(Random.Range(-1.5f, 1.5f), 0.05f, Random.Range(-1.5f, 1.5f)), _roombas.Count));
                    ShowToast($"로봇청소기 {_roombas.Count}호 투입!");
                    break;
                case "rspd1": LvRoombaSpd1++; break;
                case "fist2": LvFist2++; break;
                case "rspd2": LvRoombaSpd2++; break;
                default: return false;
            }
            sfx.PlayChime();
            SaveStateToCampaignStore();
            return true;
        }

        private void UpdateVehicleStageVisuals()
        {
            if (vehicleStageObjects == null || vehicleStageObjects.Length == 0) return;
            int idx = Mathf.Clamp(VehicleVisualStage, 0, vehicleStageObjects.Length - 1);
            for (int i = 0; i < vehicleStageObjects.Length; i++)
                if (vehicleStageObjects[i] != null) vehicleStageObjects[i].SetActive(i == idx);
        }

        private void FinishRoomComplete()
        {
            if (CollectingCount > 0) { Money += CollectingPendingValue; CollectingCount = 0; CollectingPendingValue = 0; }
            WasEvicted = false;
            WasEarlyCheckout = false;
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult);
            Money += LastClearBonus;
            DisplayMoney = Money;
            LastStarRating = 5;
            CampaignProgressStore.MarkStageCleared(CurrentStage, LastStarRating);
            SaveStateToCampaignStore();
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void StartNextStage()
        {
            int next = Mathf.Min(CampaignProgressStore.TotalStages, CurrentStage + 1);
            if (!EnterStageFromMindMap(next))
            {
                OpenStageMindMap();
            }
        }

        public void JumpToStage(int stage)
        {
            stage = Mathf.Clamp(stage, 1, CampaignProgressStore.TotalStages);
            var info = CampaignProgressStore.GetInfo(stage);
            SaveStateToCampaignStore();
            CampaignProgressStore.OpenMindMapOnNextLoad = false;
            if (Application.CanStreamedLevelBeLoaded(info.sceneName))
            {
                SceneManager.LoadScene(info.sceneName);
            }
            else
            {
                CurrentStage = stage;
                ResetRoomForRun();
            }
        }

        public void ReplayRoomKeepUpgrades()
        {
            ResetRoomForRun();
            ShowToast($"STAGE {CurrentStage} 다시 시작");
        }

        private void ResetRoomForRun()
        {
            ApplyStageContainerVisibility();
            CollectTargetsForCurrentStage();
            foreach (var p in _activeBigPiles) if (p != null) p.ResetForNewRun();
            foreach (var s in _activeStains) if (s != null) s.ResetForNewRun();
            if (trashSwarm != null) trashSwarm.SpawnForStage(CurrentStage);
            if (truckTrashVisual != null) truckTrashVisual.ClearStackedVisuals();

            CollectingCount = 0;
            CollectingPendingValue = 0;
            VehicleCurrentLoad = 0;
            VehicleDispatchTimer = 0f;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            ContinuousFever01 = 0f;
            StenchPercent = 0f;
            SuspicionPercent = 0f;
            WasEvicted = false;
            WasEarlyCheckout = false;
            LastClearBonus = 0;
            Phase = GamePhase.Phase1_TrashRemoval;

            ApplyAtmosphereImmediate();
            UpdateVehicleStageVisuals();
            if (player != null && playerSpawnPoint != null)
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            UnlockCursor(false);
        }

        private void ApplyAtmosphereImmediate()
        {
            _atmoCleanSmoothed = Mathf.Clamp01(OverallCleanPercent / 100f);
            ApplyAtmosphereValue(_atmoCleanSmoothed);
        }

        private void UpdateAtmosphereSmooth(float dt)
        {
            float target = Mathf.Clamp01(OverallCleanPercent / 100f);
            _atmoCleanSmoothed = Mathf.MoveTowards(_atmoCleanSmoothed, target, dt * 0.45f);
            ApplyAtmosphereValue(_atmoCleanSmoothed);
        }

        /// <summary>
        /// 요청 #4(역제안): 확실한 Before → After 대비!
        /// 시작(0%)은 탁하고 어두운 황토 안개 → 청소할수록 맑은 햇살 백색등으로 극적 변화!
        /// </summary>
        private void ApplyAtmosphereValue(float t)
        {
            float stenchFog = StenchBlur01 * 0.042f;
            RenderSettings.fogDensity = Mathf.Lerp(0.044f, 0.0015f, t) + stenchFog;
            RenderSettings.fogColor = Color.Lerp(new Color(0.16f, 0.13f, 0.09f), new Color(0.92f, 0.94f, 0.96f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.20f, 0.17f, 0.13f), new Color(0.78f, 0.80f, 0.82f), t);

            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(0.94f, 0.76f, 0.50f), new Color(1.0f, 1.0f, 0.99f), t);
                ceilingLight.intensity = Mathf.Lerp(10f, 26f, t);
            }
            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(0.45f, 1.45f, t);
                sunLight.color = Color.Lerp(new Color(0.92f, 0.80f, 0.62f), new Color(1.0f, 0.98f, 0.94f), t);
            }
        }

        public void ShowToast(string msg)
        {
            ToastMessage = msg;
            _toastTimer = 2.8f;
        }

        /// <summary>
        /// [Tab]/[Alt]로 하단 스테이지 퀵 전환 바를 마우스로 누를 수 있게 커서를 풀어 둔 상태.
        /// true인 동안 1인칭 컨트롤러는 커서를 자동으로 다시 잠그지 않는다(포인터 락 상태에서는
        /// 마우스가 화면 중앙에 고정되어 하단 버튼이 눌리지 않던 문제 해결).
        /// </summary>
        public static bool UiCursorFree { get; set; }

        private static void UnlockCursor(bool unlock)
        {
            if (!unlock) UiCursorFree = false;
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
