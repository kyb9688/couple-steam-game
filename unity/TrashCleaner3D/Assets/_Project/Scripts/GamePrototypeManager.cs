using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// TrashCleaner3D v1.0 중앙 매니저 — '무더기 밀기 → 트럭에 와르르 환전 → 트럭 옆 현장 강화' 코어 루프:
    /// - 0.22초 HOLD 진동 줍기(맨손 0.22s → 집게/빗자루 0.15s → 진공청소기 0.07s) + 도레미파솔 수거음
    /// - 빨아들인 쓰레기는 수거통에 쌓이고, 트럭 옆 정산 구역에서 우루루 쏟아 환전 (v1.3: 봉투 연출 제거)
    /// - 정산하면 다발 연출 + 돈 드르르륵 카운트업(와르르 환전)
    /// - 트럭 옆 '현장 강화' 발판 3개(범위·흡입·용량)에서 바로 강화 → 방으로 복귀하는 3D 공간 루프
    /// - 부가 기믹(동종 압축 · 의심 스릴 · 막판 잭팟 · 보물 슈킹 분기)은 CoreLoopConfig로 OFF
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
        public Light sunLight;   // 씬에 창가 햇살(Directional)이 있으면 여기에 자동 연결 — 없으면 생략
        public ProceduralSfx sfx;
        public GameObject thrownBagPrefab;

        [Header("v1.0 Core Loop (무더기 밀기 → 트럭 와르르 환전 → 현장 강화)")]
        [Tooltip("트럭 반납 지점 바로 옆에 '현장 강화' 자판기 존을 런타임에 코드로 스폰한다.")]
        public bool spawnFieldUpgradeStation = true;
        public FieldUpgradeStation fieldStation;

        [Header("Room Config")]
        public int baseCleanReward = 260;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        // ===== 스테이지 진행 =====
        public int CurrentStage { get; private set; } = 1;
        public float StageRewardMult => 1f + (CurrentStage - 1) * 0.30f;

        private static readonly (string hint, string itemName, int val)[] StageClientHints =
        {
            ("와이프 몰래 숨긴 비상금 · 나오면 연락!", "남편의 비밀 비상금 봉투", 115),
            ("엄마가 준 순금 반지 · 버리지 마세요!", "어머니의 순금 가락지", 140),
            ("전 남친 명품 시계 · 꼭 돌려주세요!", "스위스 명품 손목시계", 165),
            ("월세 보증금 봉투 · 라면박스 안에 있어요!", "현금 보증금 돈봉투", 195)
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
        // v1.3 (요청 #7): 악취 4단계에서도 '작업 자체'는 절대 막지 않는다. (예전엔 작업 배율 0 = 완전 봉인)
        //   대신 손이 무거워지는 둔화 폭을 단계별로 크게 벌리고, 시야는 화면 안개(StenchBlur01)로 가린다.
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.42f : StenchStage == 3 ? 0.55f : StenchStage == 2 ? 0.70f : StenchStage == 1 ? 0.84f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.40f : StenchStage == 3 ? 0.50f : StenchStage == 2 ? 0.66f : StenchStage == 1 ? 0.83f : 1.0f;
        /// <summary>악취로 시야가 뿌옇게 가려지는 정도(0~1). HUD 안개 오버레이와 포그가 함께 쓴다.</summary>
        public float StenchBlur01 => IsOutsideVentilating ? 0f : Mathf.Clamp01((StenchPercent - 22f) / 78f);
        public float StenchGainPerSec => CoreLoopConfig.EnableStenchGimmick ? 3.4f * Mathf.Max(0.18f, 1f - LvMask * 0.082f) : 0f;

        // ===== 의심 게이지 (0~100% 확정 발각 · 80~95% 아드레날린 구간!) =====
        public float SuspicionPercent { get; private set; } = 0f;
        public bool IsAdrenalineZone => CoreLoopConfig.EnableSuspicionBranch && SuspicionPercent >= 80f && SuspicionPercent < 100f;
        public float AdrenalineMult => IsAdrenalineZone ? (HasAdrenalineAmp ? 2.5f : 2.0f) : 1.0f;
        public float AdrenalineSpeedMult => IsAdrenalineZone ? 1.85f : 1.0f;
        private bool _washed30, _washed60, _washed90;

        // ===== 도구 다단계 진화 (총 9단계: 1단계 고무장갑 1→3 · 2단계 집게/빗자루 1→3 · 3단계 진공청소기 1→3!) =====
        public int ToolStep { get; private set; } = 0; // 0~9
        public int ToolTier => ToolStep >= 7 ? 2 : ToolStep >= 4 ? 1 : 0;
        public bool HasVacuumUnlocked => ToolTier >= 2;
        public string CurrentToolName =>
            ToolStep == 0 ? "맨손 (기본)" :
            ToolStep <= 3 ? $"1단계 고무장갑 ({ToolStep}/3)" :
            ToolStep <= 6 ? $"2단계 집게·빗자루 ({ToolStep - 3}/3)" :
            $"3단계 진공청소기 ({ToolStep - 6}/3)";

        // ===== 연속 흡입 피버 게이지 (Lv.0~5) =====
        public float ContinuousFever01 { get; private set; } = 0f;
        private float _feverDecayDelay = 0f;
        public bool IsFeverHot => ContinuousFever01 >= 0.75f;
        public float FeverRadiusBonus => ContinuousFever01 * (0.06f + LvFever * 0.08f);
        public float FeverSpeedBonus => ContinuousFever01 * (0.10f + LvFever * 0.14f);
        public float FeverPriceMult => (LvFever > 0 && IsFeverHot) ? (1.15f + LvFever * 0.10f) : 1.0f;

        // ===== 동종 연속 압축 (기본 5개 / 특전 해금 시 3개 → 0칸 + ×2!) =====
        public int CompressionStreakTarget => HasFastCompressor ? 3 : 5;
        public int StreakTypeIdx { get; private set; } = -1;
        public string StreakTypeLabel { get; private set; } = "";
        public int StreakCount { get; private set; } = 0;
        public int TotalCompressionsThisRoom { get; private set; } = 0;

        // ===== 수거통 용량 10단계 성장 (BagCapByLv) =====
        // v1.1 밸런스: 초반 30칸은 0.22초 홀드에서 6.6초마다 트럭으로 뛰어야 해 템포가 끊겼다.
        // → 1Lv 78칸(맨손 0.22초 ≈ 4.5개/s → 약 17초 연속 수거)로 시작하고, 홀드가 빨라지는
        //   2·3단계 구간에서는 용량도 함께 커져 각 단계마다 8~19초는 한 번에 쓸어 담게 맞췄다.
        private static readonly int[] BagCapByLv = { 78, 100, 130, 170, 230, 310, 420, 560, 740, 980, 1300 };
        // v1.1 밸런스: 1라운드에 트리 전체가 열리지 않도록 상위 단계(진공청소기 = 8번째 칸)를
        // 2라운드로 미룬다. 1라운드 기대 수입($7,000~11,000)의 40~50%만 트리에 쓰이도록 조정.
        private static readonly int[] ToolStepCosts = { 16, 26, 44, 76, 130, 220, 380, 660, 1100 };
        private static readonly int[] TruckStepCosts = { 24, 40, 66, 110, 180, 300, 520, 900, 1500 };
        // v1.0: 1초 홀드 → 0.22초 경쾌한 템포 (도구를 올릴수록 0.07초까지)
        // v1.3 (요청 #1): 시작은 1.00초 — 한 개 줍는 데 1초라 '느릿한 손맛'이 먼저 오고,
        // 1라운드 끝 무렵(ToolStep 4~5)에 0.25~0.20초로 빨라지며 성장이 체감되고, 풀강에서 0.07초.
        private static readonly float[] ToolHoldDurs = { 1.00f, 0.78f, 0.58f, 0.40f, 0.25f, 0.20f, 0.16f, 0.12f, 0.095f, 0.07f };

        // ===== 수거통(요청 #4·#12) =====
        // 봉투를 만들어 던지던 연출은 없앴다. 빨아들인 쓰레기는 '수거통'에 개수만큼 쌓이고,
        // 트럭 옆 정산 구역에서 [RMB] 한 번이면 담긴 만큼(1개라도) 낱개로 우루루 쏟아져 정산된다.
        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int BinCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (ToolTier >= 2 ? 2 : 1);
        public float BinFill01 => Mathf.Clamp01(CollectingCount / (float)Mathf.Max(1, BinCapacity));
        public bool IsBinFull => CollectingCount >= BinCapacity;
        public bool CanSettle => CollectingCount > 0;   // 1개만 담겨 있어도 정산 가능

        // ===== 슈킹 감정 결과 대형 배너 & 동종 보너스 배너 =====
        public float AppraisalBannerTimer { get; private set; }
        public float AppraisalBannerAge { get; private set; }
        public int AppraisalGrade { get; private set; } // 0:가품, 1:보통, 2:진품
        public string AppraisalBannerTitle { get; private set; } = "";
        public string AppraisalBannerSub { get; private set; } = "";

        public float CompressionBannerTimer { get; private set; }
        public string CompressionBannerText { get; private set; } = "";

        private int _bmPtsEarnedThisRoom;
        public int LastForfeitedBmPts { get; private set; }

        // ===== 연속 투하 콤보 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;
        public float MaxComboWindow => 5.0f + LvComboTime * 1.5f;

        // ===== 3D 로봇청소기 & 적층 & 월드 팝업 =====
        private readonly List<AutoRoombaCleaner> _roombas = new List<AutoRoombaCleaner>();
        private int _stackedBagsInTruck = 0;
        private readonly List<GameObject> _stackedVisuals = new List<GameObject>();
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 인게임 스킬트리 레벨 (저렴한 가격 + 다단계 3~10Lv 성장!) =====
        public int LvSweepRadius { get; private set; }      // 0~10
        public int LvFever { get; private set; }            // 0~5
        public bool HasFeverEngine => LvFever > 0;
        public int LvCrit { get; private set; }             // 0~10
        public int LvChainPop { get; private set; }         // 0~8
        // v1.3.1: 로봇청소기는 '서브'. 시작 0대(바닥에 없음) → 강화소에서 대수를 처음 사면 1대 등장,
        // 이후 같은 항목으로 대수(최대 3대)와 속도만 따로 올린다.
        public int LvRoomba { get; private set; }           // 0~3 (로봇 대수 = 레벨: 0대 → 1대 → 2대 → 3대)
        public int LvRoombaSpeed { get; private set; }      // 0~6 (수거 간격 1.00초 → 0.55초)
        public int RoombaCount => LvRoomba;                 // v1.3.1: 시작 0대 — 첫 구매가 곧 1대
        public float RoombaEatInterval => Mathf.Max(0.55f, 1.00f - LvRoombaSpeed * 0.075f);
        public float RoombaMoveSpeed => 1.85f + LvRoombaSpeed * 0.10f;

        public int LvBagSize { get; private set; }          // 0~10
        public int LvThrowPower { get; private set; }       // 0~8
        public int LvTrashPrice { get; private set; }       // 0~10
        public int LvTruckSize { get; private set; }        // 0~9 (리어카 1→3 · 다마스 1→3 · 1톤트럭 1→3)
        public int VehicleVisualStage => LvTruckSize >= 7 ? 3 : LvTruckSize >= 4 ? 2 : LvTruckSize >= 1 ? 1 : 0;
        public int LvComboTime { get; private set; }        // 0~6
        public int LvMagnet { get; private set; }           // 0~3
        public bool HasTruckMagnet => LvMagnet > 0;

        public int LvMask { get; private set; }             // 0~10
        public int LvMoveSpeed { get; private set; }        // 0~8
        public int LvMop { get; private set; }              // 0~3
        public bool HasMopUnlocked => LvMop > 0;
        public int LvScrubPower { get; private set; }       // 0~8

        public int LvFistPower { get; private set; }        // 0~10
        public int LvShockwave { get; private set; }        // 0~6
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
                // v1.3 (요청 #6): 시작 반경 0.15m = 지름 30cm(50원 동전 느낌). 여기서 강화로만 넓어진다.
                float baseR = 0.15f + ToolStep * 0.080f + LvSweepRadius * 0.042f;
                return baseR * (1f + FeverRadiusBonus);
            }
        }

        public int AutoGrabPerTick
        {
            get
            {
                // v1.0: 0.22초 홀드에서도 '쓸어 담는' 손맛이 나도록 동시 수거량을 끌어올림
                int baseGrab = ToolStep <= 3 ? (1 + LvSweepRadius / 3)
                             : ToolStep <= 6 ? (3 + (ToolStep - 3) + LvSweepRadius / 2)
                             : (8 + (ToolStep - 6) * 2 + LvSweepRadius);
                if (IsAdrenalineZone) baseGrab = Mathf.CeilToInt(baseGrab * 1.35f);
                return baseGrab;
            }
        }

        public float EffectiveHoldDuration
        {
            get
            {
                float baseDur = ToolHoldDurs[Mathf.Clamp(ToolStep, 0, ToolHoldDurs.Length - 1)];
                float workMult = Mathf.Max(0.45f, StenchWorkSpeedMult);
                float totalSpeed = workMult * (1f + FeverSpeedBonus) * AdrenalineSpeedMult;
                return Mathf.Clamp(baseDur / totalSpeed, 0.04f, 1.30f);   // v1.3: 초반 1.00초 홀드가 잘리지 않도록 상한 확대
            }
        }

        public float CritChance => LvCrit * 0.06f;
        public float ChainChance => LvChainPop * 0.08f;
        public int ChainExtraCount => LvChainPop == 0 ? 0 : (1 + LvChainPop);
        public float EvasionChance => LvEvasion * 0.20f;

        public float MoveSpeedTotalMult
        {
            get
            {
                float m = StenchMoveSpeedMult * (1f + LvMoveSpeed * 0.08f);
                if (IsAdrenalineZone) m *= 1.18f;
                if (HasFeverEngine && IsFeverHot) m *= 1.15f;
                return m;
            }
        }

        public bool IsLast10Jackpot => CoreLoopConfig.EnableLast10Jackpot &&
            (Phase1Percent >= 90f || (Phase == GamePhase.Phase2_FloorMopping && Phase2Percent >= 50f));
        public float Last10JackpotMult => IsLast10Jackpot ? (HasVipContract ? 5.0f : 4.0f) : 1.0f;
        public float VipMoneyMult => HasVipContract ? 1.5f : 1.0f;

        public float EffectiveTrashPriceMult =>
            (1f + LvTrashPrice * 0.15f) * StageRewardMult * VipMoneyMult * AdrenalineMult * FeverPriceMult * Last10JackpotMult;

        public float FistDamage =>
            (1.25f + LvFistPower * 0.85f + (ToolTier >= 2 ? 1.5f : 0f))
            * (IsAdrenalineZone ? 1.6f : 1.0f);

        public float ScrubSpeedMult =>
            (1.0f + LvMop * 0.75f) * (1f + LvScrubPower * 0.25f) * (IsAdrenalineZone ? 1.5f : 1.0f);

        public float ThrowForwardForce => 5.8f + LvThrowPower * 1.10f;
        public float ThrowUpForce => 3.1f + LvThrowPower * 0.28f;
        public string CurrentVehicleName =>
            LvTruckSize == 0 ? "공공 쓰레기통" :
            LvTruckSize <= 3 ? $"리어카 ({LvTruckSize}/3)" :
            LvTruckSize <= 6 ? $"다마스 ({LvTruckSize - 3}/3)" :
            $"1톤 트럭 ({LvTruckSize - 6}/3)";

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

        // ===== v1.3 통합 강화소(키오스크) & 트럭 정산 구역 =====
        /// <summary>트럭 옆 통합 강화소 창이 열려 있는지. (엣 Tab 상시 트리는 제거됨)</summary>
        public bool KioskOpen => Phase == GamePhase.MindMapUpgrade;

        /// <summary>트럭 정산 구역 판정 — 적재함 중심에서 개구부 반폭 + 1.15m 이내면 정산 가능.</summary>
        public bool IsNearTruckSettlement(Vector3 worldPos)
        {
            if (truckBedGoal == null) return false;
            Vector3 half = GetGoalOpeningHalfExtents();
            Vector3 d = worldPos - truckBedGoal.position;
            d.y = 0f;
            float rx = half.x + 1.15f;
            float rz = half.z + 1.15f;
            return Mathf.Abs(d.x) <= rx && Mathf.Abs(d.z) <= rz;
        }

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

        // ===== v1.2: 5대 무더기 구역 클리어 보너스 & 현관문 자동 열림 =====
        private const int ClusterClearBonus = 35;      // 구역당 $30~40 선 (경제 밸런스를 해치지 않는 소소한 보너스)
        private InteractiveDoor[] _doors;
        private float _doorAutoOpenTimer;

        private static readonly (string name, int val, TreasureRarity rarity, float susp)[] RandomTreasurePool =
        {
            ("구겨진 5만 원권 뭉치", 65, TreasureRarity.Common, 15f),
            ("중고 휴대용 게임기", 90, TreasureRarity.Rare, 17f),
            ("한정판 피규어 세트", 110, TreasureRarity.Rare, 18f),
            ("빈티지 금시계", 140, TreasureRarity.Rare, 20f),
            ("24K 미니 골드바", 195, TreasureRarity.Legendary, 25f)
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
        private bool _binFullHinted;        // 수거통 가득 안내를 한 번만 띄우기 위한 플래그
        private int _melodyStep;            // 도레미파솔 연속 수거음 계단
        private float _pitchResetTimer;

        // ===== v1.1.2 분위기(더러움 → 깨끗함) 보간 목표값 =====
        private bool _atmoTargetsReady;
        private float _tgtFogDensity = 0.045f;
        private Color _tgtFogColor = new Color(0.17f, 0.15f, 0.12f);
        private Color _tgtAmbientSky = new Color(0.21f, 0.19f, 0.15f);
        private Color _tgtAmbientEquator = new Color(0.15f, 0.14f, 0.12f);
        private Color _tgtAmbientGround = new Color(0.06f, 0.05f, 0.05f);
        private float _tgtAmbientIntensity = 0.9f;
        private Color _tgtCeilingColor = new Color(1f, 0.85f, 0.67f);
        private float _tgtCeilingIntensity = 14f;
        private Color _tgtSunColor = new Color(1f, 0.86f, 0.68f);
        private float _tgtSunIntensity = 0.6f;

        // 트럭에 쏟은 돈이 드르르륵 올라가는 카운트업 상태
        private int _cashPayoutRemaining;
        private int _cashPayoutTotal;
        private float _cashPayoutElapsed;
        private float _cashPayoutDuration = CoreLoopConfig.CashCrunchBaseDuration;

        /// <summary>[수금 중] 아직 카운트업이 끝나지 않은 금액 (HUD 표시용).</summary>
        public int PendingCash => _cashPayoutRemaining;
        public bool IsCrunchingCash => _cashPayoutRemaining > 0;

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
            if (trashSwarm != null)
            {
                trashSwarm.ClusterCleared -= OnTrashClusterCleared;
                trashSwarm.ClusterCleared += OnTrashClusterCleared;
            }
            CacheDoors();
            ScheduleDoorsOpenForRun();
            if (sunLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional) { sunLight = lights[i]; break; }
                }
            }
            UpdateAtmosphere();
            UpdateVehicleStageVisuals();
            if (spawnFieldUpgradeStation) EnsureFieldUpgradeStation();
            EnsureRoombaCount();   // v1.3.1: 시작 0대 — 이 시점엔 아무것도 안 깔린다(첫 구매 때 1대).
        }

        /// <summary>대수 강화에 맞춰 로봇청소기를 필요한 만큼만 바닥에 내려놓는다(요청 #14).
        /// v1.3.1: 대수 0대(시작 상태)면 아무것도 만들지 않고, 대수를 사는 순간 1대씩 내려놓는다.</summary>
        public void EnsureRoombaCount()
        {
            while (_roombas.Count < RoombaCount)
            {
                // 방 안(원룸 바닥)에 내려놓는다 — 마당/문 밖에서 시작하지 않도록.
                Vector3 p = new Vector3(Random.Range(-1.2f, 1.2f), 0.05f, Random.Range(-0.8f, 1.0f));
                _roombas.Add(AutoRoombaCleaner.Spawn(this, p, _roombas.Count));
            }
        }

        /// <summary>
        /// 트럭 반납 지점 바로 옆에 '현장 강화' 존을 런타임 스폰한다.
        /// 씬에 이미 있으면 그것을 쓰고, 없으면 코드로 새로 만든다(씬/프리팹 직렬화 의존 없음).
        /// </summary>
        public void EnsureFieldUpgradeStation()
        {
            if (fieldStation != null)
            {
                if (fieldStation.manager == null) fieldStation.Init(this, fieldStation.transform.eulerAngles.y);
                return;
            }

            fieldStation = FindFirstObjectByType<FieldUpgradeStation>();
            if (fieldStation == null)
            {
                Vector3 pos = ComputeFieldStationPosition();
                GameObject stationObj = new GameObject("FieldUpgradeStation");
                stationObj.transform.position = pos;
                fieldStation = stationObj.AddComponent<FieldUpgradeStation>();
            }

            // 표지판이 트럭을 마주 보도록 배치
            float yaw = 0f;
            if (truckBedGoal != null)
            {
                Vector3 look = truckBedGoal.position - fieldStation.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            }
            fieldStation.Init(this, yaw);
        }

        // ===== 현장 강화 존 배치 =====
        // FieldUpgradeStation(통합 강화소) 실제 크기 — 로컬 X 폭 2.40m × 로컬 Z 깊이 1.60m
        private const float StationHalfWidth = 1.20f;
        private const float StationHalfDepth = 0.80f;
        private const float StationWalkGap = 0.55f;   // 트럭 벽 ↔ 발판 사이 보행 여유

        /// <summary>
        /// 트럭 옆에 '현장 강화' 존을 세울 좌표를 실측해서 고른다.
        /// 예전에는 side * 3.0m 한 점을 고정으로 찍고 x/z만 잘라서, 다른 씬/차량 크기에서는
        /// 3.90m 폭 슬래브가 울타리 밖으로 나가거나 트럭 몸체와 겹칠 수 있었다.
        /// → 이제는 (1) 트럭 몸체를 뺀 좌/우 여유를 실제 콜라이더 레이캐스트로 재서 넓은 쪽을 고르고,
        ///         (2) 트럭 반폭 + 보행 여유 + 슬래브 두께만큼만 띄우고,
        ///         (3) 3.90m 폭이 벽·울타리에 닿지 않도록 수직 방향으로 밀어 넣는다.
        /// </summary>
        private Vector3 ComputeFieldStationPosition()
        {
            Vector3 basePos = truckBedGoal != null ? truckBedGoal.position : new Vector3(2.1f, 0f, 10.6f);
            basePos.y = 0f;

            Vector3 toRoom = playerSpawnPoint != null ? playerSpawnPoint.position - basePos : Vector3.back;
            toRoom.y = 0f;
            if (toRoom.sqrMagnitude < 0.01f) toRoom = Vector3.back;
            toRoom.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toRoom).normalized;

            // 1톤 트럭(최대 단계)의 투입구 반폭 1.40 이상 → 어떤 단계에서도 겹치지 않는다
            float truckHalf = Mathf.Max(1.50f, GetGoalOpeningHalfExtents().x + 0.15f);

            // 좌/우 중 실제 콜라이더(트럭 몸체 제외) 여유가 넓은 쪽
            float freeA = MeasureFreeDistance(basePos, side, truckHalf);
            float freeB = MeasureFreeDistance(basePos, -side, truckHalf);
            Vector3 outDir = freeB > freeA + 0.05f ? -side : side;
            float free = Mathf.Max(freeA, freeB);

            // 표지판이 트럭을 마주 보게 서므로 접근축 방향 두께 = 로컬 Z(1.45)/2
            float ideal = truckHalf + StationWalkGap + StationHalfDepth;
            float maxDist = free - StationHalfDepth - 0.20f;
            float dist = ideal;
            if (maxDist < ideal)
            {
                // 마당이 좁아도 트럭 몸체와는 절대 겹치지 않게 (불가피하면 울타리 쪽이 조금 겹치는 쪽을 택함)
                dist = Mathf.Max(truckHalf + 0.15f, maxDist);
                Debug.LogWarning($"[현장 강화 존] 마당 여유 {free:0.00}m → 발판을 트럭에 붙여 배치합니다.");
            }

            Vector3 pos = basePos + outDir * dist;

            // 3.90m 폭이 좌우 벽/울타리를 넘지 않도록 수직 방향으로 밀어 넣기
            Vector3 perp = Vector3.Cross(Vector3.up, outDir).normalized;
            float freeP = MeasureFreeDistance(pos, perp, 0.40f);
            float freeN = MeasureFreeDistance(pos, -perp, 0.40f);
            float need = StationHalfWidth + 0.15f;
            float lo = need - freeN;   // -perp 쪽이 좁으면 +perp으로 밀기
            float hi = freeP - need;   // +perp 쪽이 좁으면 -perp으로 밀기
            if (lo <= hi)
            {
                pos += perp * Mathf.Clamp(0f, lo, hi);
            }
            else
            {
                Debug.LogWarning("[현장 강화 존] 마당 폭이 3.90m보다 좁아 슬래브 일부가 벽에 닿습니다.");
            }

            pos.y = 0f;
            Debug.Log($"[현장 강화 존] 트럭({basePos.x:0.0}, {basePos.z:0.0}) → 발판({pos.x:0.0}, {pos.z:0.0}) " +
                      $"| 좌우 여유 {freeA:0.0}m / {freeB:0.0}m · 트럭 반폭 {truckHalf:0.00}m");
            return pos;
        }

        /// <summary>origin에서 dir 방향으로 (트리거 제외) 첫 장애물까지의 거리. 6m 안에 없으면 6m.</summary>
        private static float MeasureFreeDistance(Vector3 origin, Vector3 dir, float ignoreWithin)
        {
            const float maxProbe = 6.0f;
            float best = maxProbe;
            RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 0.45f, dir, maxProbe, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance <= ignoreWithin) continue;   // 트럭 자기 몸체·플레이어 무시
                if (hits[i].distance < best) best = hits[i].distance;
            }
            return best;
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
            float sub = (LvTruckSize > 0 ? ((LvTruckSize - 1) % 3) * 0.04f : 0f);
            switch (VehicleVisualStage)
            {
                case 0: return new Vector3(0.42f, 0.58f, 0.42f);
                case 1: return new Vector3(0.70f + sub, 0.58f, 0.64f + sub);
                case 2: return new Vector3(0.98f + sub, 0.60f, 0.80f + sub);
                default: return new Vector3(1.32f + sub, 0.65f, 1.10f + sub);
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
            if (AppraisalBannerTimer > 0f) { AppraisalBannerTimer -= dt; AppraisalBannerAge += dt; }
            if (CompressionBannerTimer > 0f) { CompressionBannerTimer -= dt; }
            if (_pitchResetTimer > 0f) { _pitchResetTimer -= dt; if (_pitchResetTimer <= 0f) _melodyStep = 0; }
            TickCashCrunch(dt);
            TickAtmosphere(dt);
            DrawSettlementZone();   // 트럭 정산 구역 바닥 링
            if (_doorAutoOpenTimer > 0f)
            {
                _doorAutoOpenTimer -= dt;
                if (_doorAutoOpenTimer <= 0f) EnsureDoorsOpenForRun();
            }
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

        // ===== v1.2: 5대 무더기 구역 클리어 피드백 =====
        /// <summary>
        /// 무더기 하나를 다 비웠을 때: 그 자리에 반짝이 + "OO 구역 클리어! +$35" 팝업 + 가벼운 3화음 +
        /// 구역당 $35 소소한 현금 보너스 + 짧은 카메라 임팩트.
        /// (산개된 14%는 집계에서 빠지므로 5대 클러스터만 대상)
        /// </summary>
        private void OnTrashClusterCleared(int clusterIdx, Vector3 center)
        {
            string zoneName = InstancedTrashSwarm.ClusterName(clusterIdx);
            AddCashCrunch(ClusterClearBonus);

            Vector3 fxPos = new Vector3(center.x, Mathf.Max(0.10f, center.y + 0.10f), center.z);
            SparkleBurst.Spawn(fxPos, 22, 0.95f, new Color(0.62f, 1f, 0.84f));
            SpawnFloatingText(fxPos + Vector3.up * 0.85f, $"{zoneName} 구역 클리어! +${ClusterClearBonus}",
                new Color(0.55f, 1f, 0.78f), 2.0f);

            int cleared = trashSwarm != null ? trashSwarm.ClearedClusterCount : 1;
            int total = trashSwarm != null ? trashSwarm.ClusterSlotCount : 5;
            ShowToast($"{zoneName} 구역 클리어! +${ClusterClearBonus} · {cleared}/{total}");
            sfx.PlayZoneClear();
            if (player != null) player.AddCameraShake(0.032f, 0.12f);
        }

        // ===== v1.2: 현관문을 청소 중에는 항상 열어 두어 방↔마당 왕복 동선이 막히지 않게 =====
        private void CacheDoors()
        {
            _doors = FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None);
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null) _doors[i].manager = this;
            }
        }

        private void ScheduleDoorsOpenForRun()
        {
            _doorAutoOpenTimer = 0.9f;   // 문이 열리며 방이 드러나는 연출 + 트럭 왕복 동선 확보
        }

        /// <summary>현관문을 전부 연다. (열려 있으면 그대로 둔다)</summary>
        public void EnsureDoorsOpenForRun()
        {
            if (_doors == null || _doors.Length == 0) CacheDoors();
            if (_doors == null) return;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null && !_doors[i].IsOpen) _doors[i].SetOpen(true, sfx);
            }
        }

        /// <summary>
        /// 트럭에 밀어 넣은 돈을 0.55~1.25초 동안 드르르륵 카운트업한다.
        /// (Money에 바로 넣지 않고 조금씩 푸는 방식 → HUD 숫자가 실제로 올라가는 걸 보여줌)
        /// </summary>
        private void AddCashCrunch(int amount)
        {
            if (amount <= 0) return;
            if (_cashPayoutRemaining <= 0)
            {
                _cashPayoutElapsed = 0f;
                _cashPayoutTotal = 0;
                _cashPayoutDuration = CoreLoopConfig.CashCrunchBaseDuration;
            }

            _cashPayoutRemaining += amount;
            _cashPayoutTotal += amount;
            _cashPayoutDuration = Mathf.Clamp(
                CoreLoopConfig.CashCrunchBaseDuration + _cashPayoutTotal / 900f,
                CoreLoopConfig.CashCrunchBaseDuration,
                CoreLoopConfig.CashCrunchMaxDuration);
        }

        private void TickCashCrunch(float dt)
        {
            if (_cashPayoutRemaining <= 0)
            {
                if (_cashPayoutTotal != 0) _cashPayoutTotal = 0;
                return;
            }

            _cashPayoutElapsed += dt;
            float t = Mathf.Clamp01(_cashPayoutElapsed / Mathf.Max(0.2f, _cashPayoutDuration));
            float eased = t * t * (3f - 2f * t);

            int shouldHavePaid = Mathf.RoundToInt(_cashPayoutTotal * eased);
            int alreadyPaid = _cashPayoutTotal - _cashPayoutRemaining;
            int delta = shouldHavePaid - alreadyPaid;
            if (delta > 0)
            {
                Money += delta;
                _cashPayoutRemaining -= delta;
            }

            if (t >= 1f && _cashPayoutRemaining > 0)
            {
                Money += _cashPayoutRemaining;
                _cashPayoutRemaining = 0;
            }
            if (_cashPayoutRemaining <= 0)
            {
                _cashPayoutRemaining = 0;
                _cashPayoutTotal = 0;
                _cashPayoutElapsed = 0f;
            }
        }

        /// <summary>
        /// 라운드가 끝나는 순간(클리어·조기 정산·강제 퇴거) 아직 드르르륵 카운트업 중인 현금을
        /// 전부 즉시 지급한다. → 영수증에 적힌 금액과 보유 현금이 어긋나 '돈이 증발한 것처럼' 보이는 문제 방지.
        /// </summary>
        public void FlushPendingCash()
        {
            if (_cashPayoutRemaining > 0)
            {
                Money += _cashPayoutRemaining;
                _cashPayoutRemaining = 0;
            }
            _cashPayoutTotal = 0;
            _cashPayoutElapsed = 0f;
        }

        /// <summary>
        /// 라운드가 끝날 때 수거통에 남아 있는 쓰레기 값을 현금으로 정리한다.
        /// (정산을 못 하고 방이 끝나 '돈이 증발'하는 일이 없도록)
        /// </summary>
        private void FlushCollectingIntoCash()
        {
            if (CollectingCount <= 0) return;
            int pay = Mathf.Max(1, Mathf.RoundToInt(CollectingPendingValue * (1f + LvTruckSize * 0.03f)));
            AddCashCrunch(pay);
            CollectingCount = 0;
            CollectingPendingValue = 0;
            _binFullHinted = false;
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
        /// - 수거통이 차면 더 담기지 않고 트럭 정산 안내 (v1.3)
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

            // 동종 연속 압축 (v1.0 코어 루프에서는 꺼둔다 — 봉투 용량으로만 성장 체감)
            if (CoreLoopConfig.EnableStreakCompression)
            {
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
                            SpawnFloatingText(fxPos, $"★ {typeLabel} {targetStreak}연속! 수거통 비움 · +${bonus}", new Color(0.35f, 1f, 0.72f), 1.5f);
                        }
                        CompressionBannerText = $"★ [{typeLabel}] {targetStreak}연속 보너스! · 수거통 -{targetStreak}칸 비움 · 보너스 +${bonus}";
                        CompressionBannerTimer = 2.2f;
                    }
                }
                else
                {
                    StreakTypeIdx = typeIdx;
                    StreakTypeLabel = typeLabel;
                    StreakCount = 1;
                }
            }
            else
            {
                StreakTypeIdx = -1;
                StreakTypeLabel = "";
                StreakCount = 0;
            }

            // 도레미파솔: 연속으로 담을수록 수거음이 한 칸씩 올라간다(한 옥타브 다 오르면 다시 '도').
            _pitchResetTimer = 0.55f;
            _melodyStep = _melodyStep >= CoreLoopConfig.MelodyTopStep ? 0 : _melodyStep + 1;
            sfx.PlayNote(_melodyStep);

            // 수거통 용량이 꽉 차면 더는 담지 않는다. (강제로 봉투를 만드는 단계 없음 — 요청 #4·#12)
            if (CollectingCount >= BinCapacity)
            {
                if (!_binFullHinted)
                {
                    _binFullHinted = true;
                    ShowToast($"수거통 가득 ({CollectingCount}/{BinCapacity}) · 트럭에서 [RMB] 정산");
                }
            }
            else
            {
                _binFullHinted = false;
            }

            // 쓰레기 줍기 시 일정 확률로 보물 자동 팝업!
            TryRollAutomaticTreasure(fromBigPile: false);

            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        public void OnRobotAutoCleaned(int eatenCount, Vector3 botPos)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(eatenCount * 2 * EffectiveTrashPriceMult));
            Money += pay;
            SpawnFloatingText(botPos + Vector3.up * 0.38f, $"[BOT] +${pay}", new Color(0.35f, 0.94f, 1f), 1.0f);
            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            CheckPhase1Complete();
        }

        // ===== v1.3 트럭 정산(요청 #4·#12) =====
        /// <summary>
        /// 트럭 옆 정산 구역에서 [RMB] 한 번 — 수거통에 담긴 개수만큼(1개라도) 낱개 쓰레기를
        /// 트럭 적재함으로 우루루 쏟아 넣고 그 값을 현금으로 환산한다.
        /// 용량이 커도 가득 찰 때까지 기다릴 필요가 없다.
        /// </summary>
        public int SettleCollectingAtTruck(Vector3 fromWorldPos)
        {
            if (CollectingCount <= 0) return 0;

            int count = CollectingCount;
            int value = CollectingPendingValue;
            CollectingCount = 0;
            CollectingPendingValue = 0;
            _binFullHinted = false;

            // 연속 정산 콤보 (예전 '연속 투하'를 그대로 계승 — 빨리 여러 번 정산할수록 배율↑)
            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(6, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = MaxComboWindow;

            float comboMult = 1f + (ThrowComboCount - 1) * 0.25f;
            float vehPct = LvTruckSize * 0.03f;
            int totalPay = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(value, count * 2) * (comboMult + vehPct)));

            AddCashCrunch(totalPay);
            sfx.PlayPour();
            if (player != null) player.AddCameraShake(0.032f, 0.12f);

            // 담긴 개수만큼 낱개가 트럭으로 쏟아진다 (개수가 많으면 다발도 더 많이)
            TruckPourBurst.Spawn(fromWorldPos, truckBedGoal, count, new Color(0.95f, 0.86f, 0.42f));

            int stackCount = Mathf.Clamp(count / 5, 1, 3);
            for (int i = 0; i < stackCount; i++) SpawnStackedBagVisualInTruck();

            string popup = ThrowComboCount >= 2
                ? $"{ThrowComboCount}연속 정산! +${totalPay}"
                : $"와르르! {count}개 정산 +${totalPay}";
            Color popCol = ThrowComboCount >= 2 ? new Color(1f, 0.85f, 0.22f) : new Color(0.45f, 1f, 0.55f);
            Vector3 popPos = truckBedGoal != null ? truckBedGoal.position + Vector3.up * 0.7f : fromWorldPos + Vector3.up * 0.6f;
            SpawnFloatingText(popPos, popup, popCol, 1.8f);
            ShowToast($"정산 완료 · {count}개 → +${totalPay:N0}");

            CheckPhase1Complete();
            return totalPay;
        }

        public void ApplyShockwaveToNearbyPiles(CleanableTarget centerPile, float baseDamage)
        {
            if (LvShockwave <= 0 || centerPile == null) return;
            float splashMult = Mathf.Min(1.1f, 0.20f + LvShockwave * 0.15f);
            float radius = 2.2f + LvShockwave * 0.20f;

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
            if (player != null) player.AddCameraShake(0.045f, 0.14f);   // 큰 봉투가 터지는 묵직한 임팩트
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
            if (!CoreLoopConfig.EnableTreasureBranch) return;   // v1.0: 슈킹/신뢰도·암시장 분기는 꺼둔다
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
            int grade = 1; // 0:가품, 1:보통, 2:진품
            if (roll < genuineChance) { appraisalMult = 1.5f; grade = 2; }
            else if (roll > 1f - fakeChance) { appraisalMult = 0.6f; grade = 0; }

            float mentionMult = item.isMentionedByClient ? 2.0f : 1.0f;
            int cash = Mathf.RoundToInt(item.value * mentionMult * appraisalMult * AdrenalineMult);
            int bmPts = item.isMentionedByClient ? 2 : 1;

            bool evaded = EvasionChance > 0f && Random.value < EvasionChance;
            float suspGain = evaded ? 0f : item.baseSuspicionGain * mentionMult;

            // 요청 #3: 의심 100% 강제 퇴거 시 이번 슈킹 및 이번 판 암시장 포인트 적립 금지/몰수!
            if (SuspicionPercent + suspGain >= 100f)
            {
                SuspicionPercent = 100f;
                StealAlertFlash01 = 1.0f;
                LastStolenItemName = item.itemName;
                LastStolenValue = cash;
                LastForfeitedBmPts = _bmPtsEarnedThisRoom;
                BlackMarketPts = Mathf.Max(0, BlackMarketPts - _bmPtsEarnedThisRoom);
                _bmPtsEarnedThisRoom = 0;
                TriggerForcedEviction();
                return;
            }

            Money += cash;
            BlackMarketPts += bmPts;
            _bmPtsEarnedThisRoom += bmPts;
            StolenCount++;
            StolenCashTotal += cash;
            LastStolenItemName = item.itemName;
            LastStolenValue = cash;
            SuspicionPercent = Mathf.Min(99.9f, SuspicionPercent + suspGain);
            if (suspGain > 0f) StealAlertFlash01 = SuspicionPercent >= 80f ? 0.85f : 0.35f;

            // 요청 #4: 슈킹 직후 진품/보통/가품 가시성 대폭 강화 대형 배너 + 차별화 사운드!
            AppraisalGrade = grade;
            AppraisalBannerTimer = 3.2f;
            AppraisalBannerAge = 0f;
            if (grade == 2)
            {
                AppraisalBannerTitle = "★ 진품 대박 감정! (×1.5) ★";
                TreasureFlash01 = 0.85f;
                sfx.PlayFanfare();
            }
            else if (grade == 0)
            {
                AppraisalBannerTitle = "× 가품 판정... (×0.6)";
                sfx.PlayPop(0.42f);
            }
            else
            {
                AppraisalBannerTitle = "◆ 보통 감정 (×1.0)";
                sfx.PlaySteal();
            }

            string suspStr = evaded ? "의심 회피 성공!" : (IsAdrenalineZone ? $"아드레날린 발동! ({SuspicionPercent:0}%)" : $"의심 +{suspGain:0}%");
            AppraisalBannerSub = $"{item.itemName}  ·  +${cash}  ·  ♦+{bmPts}  ·  {suspStr}";
        }

        private void TriggerForcedEviction()
        {
            FlushCollectingIntoCash();
            FlushPendingCash();
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
            FlushCollectingIntoCash();
            FlushPendingCash();
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

        private void SpawnStackedBagVisualInTruck()
        {
            if (truckStackedParent == null || thrownBagPrefab == null) return;
            if (_stackedBagsInTruck >= 45) return;

            int idx = _stackedBagsInTruck++;
            Vector3 half = GetGoalOpeningHalfExtents();
            int cols = VehicleVisualStage == 0 ? 1 : VehicleVisualStage == 1 ? 2 : 3;
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
            bag.transform.localScale = Vector3.one * (VehicleVisualStage == 0 ? 0.72f : 0.92f);
            foreach (var col in bag.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in bag.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            _stackedVisuals.Add(bag);
        }

        private void CheckPhase1Complete()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            if (Phase1Cleaned >= Phase1Total && Phase1Total > 0)
            {
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
            // v1.1.2 디자인: 얼룩이 사라진 자리에 반짝이 파티클 ("광 냈다"는 시각 보상)
            SparkleBurst.Spawn(stain.transform.position + Vector3.up * 0.05f, 16, 0.62f, new Color(1f, 0.96f, 0.78f));
            UpdateAtmosphere();
            CheckSuspicionCleanMilestones();
            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        /// <summary>
        /// v1.3 (요청 #5·#10): 강화 창은 트럭 옆 통합 강화소(키오스크)에서만 열린다.
        /// [Tab]으로 언제든 열리던 상시 강화 트리는 제거됐다.
        /// </summary>
        public void OpenUpgradeKiosk()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.MindMapUpgrade) return;
            if (Phase != GamePhase.SettingsMenu) _phaseBeforeOverlay = Phase;
            Phase = GamePhase.MindMapUpgrade;
            UnlockCursor(true);
            sfx.PlayChime();
        }

        public void CloseUpgradeKiosk()
        {
            if (Phase != GamePhase.MindMapUpgrade) return;
            Phase = _phaseBeforeOverlay;
            UnlockCursor(false);
        }

        public void ToggleUpgradeKiosk()
        {
            if (Phase == GamePhase.MindMapUpgrade) CloseUpgradeKiosk();
            else OpenUpgradeKiosk();
        }

        public void ToggleSettingsMenu()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.SettingsMenu || Phase == GamePhase.MindMapUpgrade) { Phase = _phaseBeforeOverlay; UnlockCursor(false); }
            else { _phaseBeforeOverlay = Phase; Phase = GamePhase.SettingsMenu; UnlockCursor(true); }
        }

        // ===== 인게임 스킬트리 가격 ($ 현금 전용 — 저렴한 가격 + 다단계 3~10Lv 클릭 손맛!) =====
        public int CostTool => ToolStepCosts[Mathf.Clamp(ToolStep, 0, ToolStepCosts.Length - 1)];
        public int CostSweep => Mathf.RoundToInt(10 * Mathf.Pow(1.45f, LvSweepRadius));
        public int CostFever => Mathf.RoundToInt(40 * Mathf.Pow(1.50f, LvFever));
        public int CostCrit => Mathf.RoundToInt(18 * Mathf.Pow(1.45f, LvCrit));
        public int CostChain => Mathf.RoundToInt(40 * Mathf.Pow(1.50f, LvChainPop));
        public int CostRoomba => Mathf.RoundToInt(140 * Mathf.Pow(1.80f, LvRoomba));
        public int CostRoombaSpeed => Mathf.RoundToInt(70 * Mathf.Pow(1.45f, LvRoombaSpeed));

        public int CostBag => Mathf.RoundToInt(12 * Mathf.Pow(1.42f, LvBagSize));
        public int CostThrowPower => Mathf.RoundToInt(18 * Mathf.Pow(1.42f, LvThrowPower));
        public int CostTrashPrice => Mathf.RoundToInt(24 * Mathf.Pow(1.62f, LvTrashPrice));
        public int CostTruck => TruckStepCosts[Mathf.Clamp(LvTruckSize, 0, TruckStepCosts.Length - 1)];
        public int CostComboTime => Mathf.RoundToInt(40 * Mathf.Pow(1.50f, LvComboTime));
        public int CostMagnet => Mathf.RoundToInt(120 * Mathf.Pow(1.60f, LvMagnet));

        public int CostMask => Mathf.RoundToInt(14 * Mathf.Pow(1.40f, LvMask));
        public int CostMoveSpeed => Mathf.RoundToInt(14 * Mathf.Pow(1.42f, LvMoveSpeed));
        public int CostMop => Mathf.RoundToInt(40 * Mathf.Pow(1.50f, LvMop));
        public int CostScrub => Mathf.RoundToInt(26 * Mathf.Pow(1.45f, LvScrubPower));

        public int CostFist => Mathf.RoundToInt(14 * Mathf.Pow(1.45f, LvFistPower));
        public int CostShockwave => Mathf.RoundToInt(40 * Mathf.Pow(1.48f, LvShockwave));
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
            // v1.3: 강화 경로가 '트럭 옆 통합 강화소' 한 곳으로 모였으므로, 키오스크 진열 항목은
            // 선행 조건 없이 항상 열려 있다(어느 강화를 먼저 살지는 플레이어 선택).
            switch (id)
            {
                case "tool": case "sweep": case "bag": case "mask": case "fist": return true;
                case "fever": return true;
                case "crit": return ToolStep >= 2;
                case "chain": return LvCrit >= 2;
                case "roomba": return true;
                // v1.3.1: 로봇 속도는 로봇을 최소 1대 보유한 뒤에 열린다.
                case "roombaspeed": return LvRoomba >= 1;
                case "throw": return true;
                case "price": return true;
                case "truck": return true;
                case "combo": return true;
                case "magnet": return VehicleVisualStage >= 1;
                case "speed": return true;
                case "mop": return true;
                case "scrub": return true;
                case "shock": return LvFistPower >= 2;
                case "beacon": return LvFistPower >= 1;
                case "vip": case "compressor": case "sense": return true;
                case "appraisal": case "evasion": case "adrenaline": case "launder": return true;
                default: return false;
            }
        }

        public int CountAffordableCashUpgrades()
        {
            int c = 0;
            if (IsNodeUnlocked("tool") && ToolStep < 9 && Money >= CostTool) c++;
            if (IsNodeUnlocked("sweep") && LvSweepRadius < 10 && Money >= CostSweep) c++;
            if (IsNodeUnlocked("fever") && LvFever < 5 && Money >= CostFever) c++;
            if (IsNodeUnlocked("crit") && LvCrit < 10 && Money >= CostCrit) c++;
            if (IsNodeUnlocked("chain") && LvChainPop < 8 && Money >= CostChain) c++;
            if (IsNodeUnlocked("roomba") && LvRoomba < 3 && Money >= CostRoomba) c++;
            if (IsNodeUnlocked("roombaspeed") && LvRoombaSpeed < 6 && Money >= CostRoombaSpeed) c++;

            if (IsNodeUnlocked("bag") && LvBagSize < 10 && Money >= CostBag) c++;
            if (IsNodeUnlocked("throw") && LvThrowPower < 8 && Money >= CostThrowPower) c++;
            if (IsNodeUnlocked("price") && LvTrashPrice < 10 && Money >= CostTrashPrice) c++;
            if (IsNodeUnlocked("truck") && LvTruckSize < 9 && Money >= CostTruck) c++;
            if (IsNodeUnlocked("combo") && LvComboTime < 6 && Money >= CostComboTime) c++;
            if (IsNodeUnlocked("magnet") && LvMagnet < 3 && Money >= CostMagnet) c++;

            if (IsNodeUnlocked("mask") && LvMask < 10 && Money >= CostMask) c++;
            if (IsNodeUnlocked("speed") && LvMoveSpeed < 8 && Money >= CostMoveSpeed) c++;
            if (IsNodeUnlocked("mop") && LvMop < 3 && Money >= CostMop) c++;
            if (IsNodeUnlocked("scrub") && LvScrubPower < 8 && Money >= CostScrub) c++;

            if (IsNodeUnlocked("fist") && LvFistPower < 10 && Money >= CostFist) c++;
            if (IsNodeUnlocked("shock") && LvShockwave < 6 && Money >= CostShockwave) c++;
            if (IsNodeUnlocked("beacon") && !HasBeaconDetect && Money >= CostBeacon) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            if (!IsNodeUnlocked(id)) return false;
            switch (id)
            {
                case "tool":
                    if (ToolStep >= 9 || Money < CostTool) return false;
                    Money -= CostTool; ToolStep++;
                    ShowToast($"도구 강화 → {CurrentToolName}");
                    break;
                case "sweep": if (LvSweepRadius >= 10 || Money < CostSweep) return false; Money -= CostSweep; LvSweepRadius++; break;
                case "fever": if (LvFever >= 5 || Money < CostFever) return false; Money -= CostFever; LvFever++; break;
                case "crit": if (LvCrit >= 10 || Money < CostCrit) return false; Money -= CostCrit; LvCrit++; break;
                case "chain": if (LvChainPop >= 8 || Money < CostChain) return false; Money -= CostChain; LvChainPop++; break;
                case "roomba":
                    // v1.3.1: 대수만 늘린다(시작 0대 — 첫 구매가 1대, 이후 최대 3대). 속도는 아래 별도 항목.
                    if (LvRoomba >= 3 || Money < CostRoomba) return false;
                    Money -= CostRoomba; LvRoomba++;
                    EnsureRoombaCount();
                    ShowToast(RoombaCount <= 1 ? "로봇청소기 첫 1대 배치!" : $"로봇청소기 {RoombaCount}대 배치!");
                    break;
                case "roombaspeed":
                    if (LvRoombaSpeed >= 6 || Money < CostRoombaSpeed) return false;
                    Money -= CostRoombaSpeed; LvRoombaSpeed++;
                    ShowToast($"로봇청소기 속도 강화 · {RoombaEatInterval:0.00}초/개");
                    break;

                case "bag": if (LvBagSize >= 10 || Money < CostBag) return false; Money -= CostBag; LvBagSize++; break;
                case "throw": if (LvThrowPower >= 8 || Money < CostThrowPower) return false; Money -= CostThrowPower; LvThrowPower++; break;
                case "price": if (LvTrashPrice >= 10 || Money < CostTrashPrice) return false; Money -= CostTrashPrice; LvTrashPrice++; break;
                case "truck":
                    if (LvTruckSize >= 9 || Money < CostTruck) return false;
                    Money -= CostTruck; LvTruckSize++; UpdateVehicleStageVisuals();
                    ShowToast($"수거함 → {CurrentVehicleName}");
                    break;
                case "combo": if (LvComboTime >= 6 || Money < CostComboTime) return false; Money -= CostComboTime; LvComboTime++; break;
                case "magnet": if (LvMagnet >= 3 || Money < CostMagnet) return false; Money -= CostMagnet; LvMagnet++; break;

                case "mask": if (LvMask >= 10 || Money < CostMask) return false; Money -= CostMask; LvMask++; break;
                case "speed": if (LvMoveSpeed >= 8 || Money < CostMoveSpeed) return false; Money -= CostMoveSpeed; LvMoveSpeed++; break;
                case "mop": if (LvMop >= 3 || Money < CostMop) return false; Money -= CostMop; LvMop++; break;
                case "scrub": if (LvScrubPower >= 8 || Money < CostScrub) return false; Money -= CostScrub; LvScrubPower++; break;

                case "fist": if (LvFistPower >= 10 || Money < CostFist) return false; Money -= CostFist; LvFistPower++; break;
                case "shock": if (LvShockwave >= 6 || Money < CostShockwave) return false; Money -= CostShockwave; LvShockwave++; break;
                case "beacon": if (HasBeaconDetect || Money < CostBeacon) return false; Money -= CostBeacon; HasBeaconDetect = true; break;

                // 클리어 후 [★ 특전]
                case "vip": if (HasVipContract || TrustTokens < CostVipTokens) return false; TrustTokens -= CostVipTokens; HasVipContract = true; break;
                case "compressor": if (HasFastCompressor || TrustTokens < CostCompressorTokens) return false; TrustTokens -= CostCompressorTokens; HasFastCompressor = true; break;
                case "sense": if (HasTreasureSense || TrustTokens < CostTreasureSenseTokens) return false; TrustTokens -= CostTreasureSenseTokens; HasTreasureSense = true; break;

                // 클리어 후 [♦ 암시장]
                case "appraisal": if (LvAppraisal >= 2 || BlackMarketPts < CostAppraisalPts) return false; BlackMarketPts -= CostAppraisalPts; LvAppraisal++; break;
                case "evasion": if (LvEvasion >= 3 || BlackMarketPts < CostEvasionPts) return false; BlackMarketPts -= CostEvasionPts; LvEvasion++; break;
                case "adrenaline": if (HasAdrenalineAmp || BlackMarketPts < CostAdrenalinePts) return false; BlackMarketPts -= CostAdrenalinePts; HasAdrenalineAmp = true; break;
                case "launder": if (BlackMarketPts < CostLaunderPts) return false; BlackMarketPts -= CostLaunderPts; Money += 180; break;

                default: return false;
            }
            sfx.PlayChime();
            return true;
        }

        private void UpdateVehicleStageVisuals()
        {
            if (vehicleStageObjects == null || vehicleStageObjects.Length == 0) return;
            int idx = Mathf.Clamp(VehicleVisualStage, 0, vehicleStageObjects.Length - 1);
            for (int i = 0; i < vehicleStageObjects.Length; i++)
            {
                if (vehicleStageObjects[i] != null) vehicleStageObjects[i].SetActive(i == idx);
            }
        }

        private void FinishRoomComplete()
        {
            FlushCollectingIntoCash();
            FlushPendingCash();
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

            ActiveTreasure = null;
            CollectingCount = 0;
            CollectingPendingValue = 0;
            _binFullHinted = false;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            ContinuousFever01 = 0f;
            _feverDecayDelay = 0f;
            _melodyStep = 0;
            _pitchResetTimer = 0f;
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
            _bmPtsEarnedThisRoom = 0;
            LastForfeitedBmPts = 0;
            AppraisalBannerTimer = 0f;
            CompressionBannerTimer = 0f;
            Phase = GamePhase.Phase1_TrashRemoval;

            UpdateAtmosphere();
            UpdateVehicleStageVisuals();
            EnsureDoorsOpenForRun();
            ScheduleDoorsOpenForRun();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            UnlockCursor(false);
        }

        private void UpdateAtmosphere()
        {
            // v1.1.2 디자인: 시작은 탁하고 축축한 공기, 청소율이 오를수록 안개가 걷히며 따뜻한 오후 햇살 톤으로.
            // smoothstep이라 초반엔 더러운 톤이 유지되고 후반(50~100%)에 확 밝아진다.
            float raw = Mathf.Clamp01(OverallCleanPercent / 100f);
            float t = raw * raw * (3f - 2f * raw);

            RenderSettings.fog = true;
            _tgtFogDensity = Mathf.Lerp(0.048f, 0.0024f, t);
            _tgtFogColor = Color.Lerp(new Color(0.170f, 0.152f, 0.118f), new Color(0.930f, 0.905f, 0.855f), t);
            _tgtAmbientSky = Color.Lerp(new Color(0.205f, 0.188f, 0.152f), new Color(0.740f, 0.722f, 0.680f), t);
            _tgtAmbientEquator = Color.Lerp(new Color(0.150f, 0.140f, 0.116f), new Color(0.600f, 0.560f, 0.500f), t);
            _tgtAmbientGround = Color.Lerp(new Color(0.058f, 0.054f, 0.045f), new Color(0.330f, 0.292f, 0.242f), t);
            _tgtAmbientIntensity = Mathf.Lerp(0.85f, 1.18f, t);
            _tgtCeilingColor = Color.Lerp(new Color(1.0f, 0.845f, 0.665f), new Color(1.0f, 0.980f, 0.930f), t);
            _tgtCeilingIntensity = Mathf.Lerp(13.5f, 23f, t);
            _tgtSunColor = Color.Lerp(new Color(1.0f, 0.86f, 0.68f), new Color(1.0f, 0.945f, 0.855f), t);
            _tgtSunIntensity = Mathf.Lerp(0.55f, 1.35f, t);
            _atmoTargetsReady = true;
        }

        // 목표 톤으로 매 프레임 부드럽게 끌어당겨, 청소 중에 방 색이 뚝뚝 끊기지 않고 서서히 환해진다.
        private void TickAtmosphere(float dt)
        {
            if (!_atmoTargetsReady) return;
            float k = 1f - Mathf.Exp(-1.7f * dt);

            RenderSettings.fog = true;
            // v1.3 (요청 #7): 악취가 심할수록 안개 밀도도 함께 올라 시야가 뿌옇게 가린다.
            float stenchFog = StenchBlur01 * 0.075f;
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, _tgtFogDensity + stenchFog, k);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, _tgtFogColor, k);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, _tgtAmbientSky, k);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, _tgtAmbientSky, k);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, _tgtAmbientEquator, k);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, _tgtAmbientGround, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, _tgtAmbientIntensity, k);

            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(ceilingLight.color, _tgtCeilingColor, k);
                ceilingLight.intensity = Mathf.Lerp(ceilingLight.intensity, _tgtCeilingIntensity, k);
            }
            if (sunLight != null && sunLight.type == LightType.Directional)
            {
                sunLight.color = Color.Lerp(sunLight.color, _tgtSunColor, k);
                sunLight.intensity = Mathf.Lerp(sunLight.intensity, _tgtSunIntensity, k);
            }
        }

        // ===== v1.3 트럭 정산 구역 바닥 링 (요청 #4·#12) =====
        private Mesh _settleDiscMesh;
        private Mesh _settleRingMesh;
        private Material _settlePadMat;

        private void EnsureSettlementPadVisuals()
        {
            if (_settlePadMat != null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh == null) return;

            _settleDiscMesh = BuildFloorRingMesh(56, 0f, 1f);
            _settleRingMesh = BuildFloorRingMesh(56, 0.94f, 1f);

            _settlePadMat = new Material(sh);
            if (_settlePadMat.HasProperty("_Surface")) _settlePadMat.SetFloat("_Surface", 1f);
            if (_settlePadMat.HasProperty("_SrcBlend")) _settlePadMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_settlePadMat.HasProperty("_DstBlend")) _settlePadMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (_settlePadMat.HasProperty("_ZWrite")) _settlePadMat.SetFloat("_ZWrite", 0f);
            if (_settlePadMat.HasProperty("_Cull")) _settlePadMat.SetFloat("_Cull", (float)CullMode.Off);
            _settlePadMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _settlePadMat.renderQueue = (int)RenderQueue.Transparent + 5;
        }

        /// <summary>플레이어가 어디에 서면 정산되는지 바닥에 금색 링으로 상시 표시한다.</summary>
        private void DrawSettlementZone()
        {
            EnsureSettlementPadVisuals();
            if (_settlePadMat == null || truckBedGoal == null) return;
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            Vector3 half = GetGoalOpeningHalfExtents();
            float radius = Mathf.Min(half.x, half.z) + 1.10f;
            bool ready = CollectingCount > 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (ready ? 4.2f : 1.8f));

            Vector3 p = new Vector3(truckBedGoal.position.x, 0.022f, truckBedGoal.position.z);
            Matrix4x4 m = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(radius, 1f, radius));

            Color discCol = ready ? new Color(1f, 0.86f, 0.32f, 0.055f + 0.05f * pulse)
                                  : new Color(1f, 0.96f, 0.88f, 0.028f + 0.02f * pulse);
            _settlePadMat.SetColor("_BaseColor", discCol);
            if (_settleDiscMesh != null) Graphics.DrawMesh(_settleDiscMesh, m, _settlePadMat, 0);

            Color ringCol = ready ? new Color(1f, 0.84f, 0.28f, 0.55f + 0.35f * pulse)
                                  : new Color(1f, 0.95f, 0.86f, 0.20f + 0.10f * pulse);
            _settlePadMat.SetColor("_BaseColor", ringCol);
            if (_settleRingMesh != null) Graphics.DrawMesh(_settleRingMesh, m, _settlePadMat, 0);
        }

        private static Mesh BuildFloorRingMesh(int segments, float innerR, float outerR)
        {
            Mesh m = new Mesh();
            m.name = "Mesh_SettlePadRing";
            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, 0f, sn * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, sn * outerR);

                int ni = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
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
