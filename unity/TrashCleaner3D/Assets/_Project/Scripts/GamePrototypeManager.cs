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

    /// <summary>[v1.8] 손에 든 장비 4단계: 맨손 → 진공청소기 → 원기옥 빔포 → 미니 블랙홀 건.
    /// (빗자루는 맨손과 진공 사이의 과도기 도구로, 4단계 장비 계보에는 세지 않는다)</summary>
    public enum GearMode { Hand, Broom, Vacuum, Orb, BlackHole }

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
    /// TrashCleaner3D v1.8 중앙 매니저:
    /// - 1~7 스테이지 청소 + 큰 봉투·바닥 얼룩을 스테이지 규격에 맞게 재배치(StagePropLayout)
    /// - 1:1 GPU-Instanced 트럭 낱개 투입 & 적재
    /// - 수거 차량(쓰레기통→리어카→다마스→1톤 트럭): 정산 단가 배수 + 편의성 해금
    /// - v1.8 강화 트리: 코어 28 + 마이크로 240 = **268노드** (10계열 · 장비·압축·쓰레기 강화 포함)
    /// - '공짜 효과' 전면 제거: 연쇄 흡입·봉투 피버/현금·산소 부스트·콤보·대량 정산 보너스 등은
    ///   전부 강화 노드로 해금해야 작동한다(기본 0/비활성).
    /// - 4시간 캠페인 밸런스: 1회 강화당 1~3% 상승, 스테이지 물량 다중 웨이브(작은 쓰레기 7만+)
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

        [Header("Integrated Upgrade Kiosk (Disabled in v1.9 -> [Tab] Anytime!)")]
        public bool spawnFieldUpgradeStation = false;
        public FieldUpgradeStation fieldStation;

        [Header("Room Config")]
        public int baseCleanReward = 280;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        public bool KioskOpen => Phase == GamePhase.MindMapUpgrade;
        public bool IsStageMindMapOpen => Phase == GamePhase.StageMindMap;
        public int CurrentStage { get; private set; } = 1;
        public float StageRewardMult => 1f + (CurrentStage - 1) * 0.22f;

        /// <summary>[v2.0] 스테이지별 일반 쓰레기 기본 단가(중복 곱연산 제거 → 7스테이지 지수 경제 곡선).</summary>
        public int CurrentStageTrashBaseValue => CurrentStage switch
        {
            1 => 2, 2 => 4, 3 => 8, 4 => 15, 5 => 28, 6 => 50, 7 => 90, _ => 2
        };

        public string CurrentClientHint => CampaignProgressStore.GetInfo(CurrentStage).clientMemo;
        public string CurrentStageTitle => CampaignProgressStore.GetInfo(CurrentStage).title;

        // ===== 재화 & 카운트업 연출 =====
        public int Money { get; private set; } = 0;
        public float DisplayMoney { get; private set; } = 0f;
        public int TrustTokens { get; private set; } = 0;
        public int BlackMarketPts { get; private set; } = 0;

        /// <summary>[v2.3] 숨겨진 테스트용 ']' 키 재화 추가 (UI/토스트/로그 일체 표시 없음).</summary>
        public void AddDebugMoneySilent(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
        }

        // ===== 사용자 설정 ([ESC]) =====
        public float MouseSensitivity { get; private set; } = 0.14f;
        public float MasterVolume { get; private set; } = 0.85f;

        // ===== 악취 & 시야 안개 =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; }
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        // [v2.1] 악취 4단계: 0(쾌적 0~75%) / 1(답답함 75~90%) / 2(위험 90~100%) / 3(한계 100%)
        public int StenchStage => StenchPercent >= 99.5f ? 3 : StenchPercent >= 90f ? 2 : StenchPercent >= 75f ? 1 : 0;
        // [v1.7] 악취 둔화 저항은 강화 노드로만 얻는다(기본 0 = 기존 감속 그대로).
        public float StenchSlowResist01 => Mathf.Clamp(V18Sum(V18Effect.StenchSlowResist), 0f, 0.60f);
        private float ResistSlow(float baseMult) => 1f - (1f - baseMult) * (1f - StenchSlowResist01);
        public float StenchMoveSpeedMult => ResistSlow(StenchStage == 3 ? 0.50f : StenchStage == 2 ? 0.70f : StenchStage == 1 ? 0.86f : 1.0f);
        public float StenchWorkSpeedMult => ResistSlow(StenchStage == 3 ? 0.48f : StenchStage == 2 ? 0.68f : StenchStage == 1 ? 0.85f : 1.0f);
        public float StenchBlur01 => IsOutsideVentilating ? 0f
            : Mathf.Clamp01((StenchPercent - 75f) / 25f) * (1f - Mathf.Clamp(V18Sum(V18Effect.BlurReduce), 0f, 0.60f));
        public float StenchGainPerSec => CoreLoopConfig.EnableStenchGimmick
            ? (CurrentStage == 6 ? 5.1f : 3.2f) * Mathf.Max(0.18f, 1f - TotalMaskLv * 0.08f) * (1f - Mathf.Clamp(V18Sum(V18Effect.MaskFilter), 0f, 0.55f))
            : 0f;

        // ===== v1.7 방독면 '산소 부스트' (기본 잠김 → 산소 부스트 노드로 해금) =====
        // 악취가 낮은(45% 이하 → 25% 이하에서 최대) 상태에서 방독면을 갖추면
        // 이동·흡입이 빨라지고 피버가 천천히 식는다. v1.6의 기본 15%는 삭제하고 노드로만 얻는다.
        public float OxygenZone01 => IsOutsideVentilating ? 1f : Mathf.Clamp01((45f - StenchPercent) / 20f);
        public float OxygenMaskFactor01 => Mathf.Clamp01(TotalMaskLv / 5f);
        public bool HasOxygenBoost => V18Sum(V18Effect.OxygenUnlock) > 0f;
        public float OxygenBoost01 => HasOxygenBoost ? Mathf.Clamp01(OxygenZone01 * OxygenMaskFactor01) : 0f;
        public float OxygenPower01 => V18Sum(V18Effect.OxygenPower);
        public float OxygenSuctionMult => 1f + OxygenPower01 * OxygenBoost01;
        public float OxygenMoveMult => 1f + (OxygenPower01 + V18Sum(V18Effect.OxygenMove)) * OxygenBoost01;
        public float OxygenBoostMult => OxygenMoveMult;
        public float OxygenFeverHold01 => Mathf.Clamp01(V18Sum(V18Effect.OxygenFeverHold) * OxygenBoost01);

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
        public bool HasTwoHand => V18Sum(V18Effect.TwoHand) > 0f;

        public string CurrentToolName =>
            HasBlackHole ? (HasWhiteHole ? "5단계 청소 드론 편대 · 직결 호스" : "5단계 자율 청소 드론 편대") :
            HasOrbGun ? "4단계 산업용 사이클론 청소기" :
            HasVacuum ? $"3단계 진공청소기 (속도 +{LvHold3})" :
            HasBroom ? $"2단계 빗자루 (속도 +{LvHold2})" : "1단계 장갑 맨손";

        // ===== v2.0 장비 5단계 (맨손 → 빗자루 → 진공 → 산업용 사이클론 → 자율 청소 드론) =====
        public bool HasOrbGun => V18Sum(V18Effect.OrbUnlock) > 0f || V18Sum(V18Effect.BhUnlock) > 0f;
        public bool HasBlackHole => V18Sum(V18Effect.BhUnlock) > 0f;
        public bool HasWhiteHole => V18Sum(V18Effect.WhUnlock) > 0f;
        public int GearTierIndex => HasBlackHole ? 3 : HasOrbGun ? 2 : HasVacuum ? 1 : 0;
        /// <summary>[v2.0] HUD 아이콘용 짧은 장비 표기(깨짐 없는 기호).</summary>
        public string ToolBadge => Mode == GearMode.BlackHole ? "★ 청소 드론"
            : Mode == GearMode.Orb ? "★ 사이클론"
            : Mode == GearMode.Vacuum ? "진공청소기"
            : Mode == GearMode.Broom ? "빗자루" : "맨손";
        public GearMode Mode => HasBlackHole ? GearMode.BlackHole : HasOrbGun ? GearMode.Orb
            : HasVacuum ? GearMode.Vacuum : HasBroom ? GearMode.Broom : GearMode.Hand;

        /// <summary>원기옥: 앞의 쓰레기를 한 점으로 끌어모으는 반경(미터).</summary>
        public float OrbPullRadius => 2.1f + V18Sum(V18Effect.OrbRadius) * 4.2f;
        public float OrbGrowRate => 1f + V18Sum(V18Effect.OrbCharge);
        /// <summary>구슬이 담을 수 있는 최대 개수(여기 도달하면 자동 발사).</summary>
        public int OrbCapacity => Mathf.RoundToInt((22f + TotalRadLv * 2f + V18Sum(V18Effect.OrbSize) * 90f) * OrbGrowRate);
        public float OrbCashBonus => V18Sum(V18Effect.OrbCash);
        public float OrbBurstRadius => 1.5f + V18Sum(V18Effect.OrbBurst) * 3.4f;
        public float OrbFlySpeed => 10f + V18Sum(V18Effect.OrbFly) * 16f;
        public bool OrbAutoLaunch => V18Sum(V18Effect.OrbAuto) > 0f;

        /// <summary>블랙홀: 전개 개수(1→3) · 반경 · 회전 · 지속.</summary>
        public int BlackHoleCount => HasBlackHole ? Mathf.Clamp(1 + Mathf.RoundToInt(V18Sum(V18Effect.BhCount)), 1, 3) : 0;
        public float BlackHoleRadius => 1.5f + V18Sum(V18Effect.BhRadius) * 3.4f;
        public float BlackHoleSpin => 1f + V18Sum(V18Effect.BhSpin) * 3.2f;
        public float BlackHoleDuration => 3.2f + V18Sum(V18Effect.BhDuration) * 4.5f;
        public float BlackHolePull => 1f + V18Sum(V18Effect.BhPull) * 2.4f;
        public float WhiteHoleCash => V18Sum(V18Effect.WhCash);
        public bool WhiteHoleAuto => V18Sum(V18Effect.WhAuto) > 0f;

        // ===== v1.8 쓰레기 강화: 내구도 있는 고가치 폐기물 =====
        // (0 = 일반, 1 캔·병, 2 전자폐기물, 3 대형 폐가전, 4 금괴) — hp/가치/등장 가중치
        public static readonly float[] HeavyHp = { 0f, 5.5f, 14f, 34f, 60f };
        public static readonly int[] HeavyBaseValue = { 0, 60, 220, 900, 4000 };
        public bool HeavyCan => V18Sum(V18Effect.TrashCan) > 0f;
        public bool HeavyEwaste => V18Sum(V18Effect.TrashEwaste) > 0f;
        public bool HeavyBig => V18Sum(V18Effect.TrashBig) > 0f;
        public bool HeavyGold => V18Sum(V18Effect.TrashGold) > 0f;
        /// <summary>고가치 비중 노드 합(0~0.6).</summary>
        public float HeavyRatio01 => Mathf.Clamp(V18Sum(V18Effect.TrashRatio), 0f, 0.6f);
        /// <summary>[v1.8] 웨이브마다 추가로 섞이는 고가치 쓰레기 수(일반 물량과 별도).</summary>
        public int HeavyPerWave => HeavyCan ? 4 + Mathf.RoundToInt(HeavyRatio01 * 90f) : 0;
        public float HeavyHpMult => 1f + V18Sum(V18Effect.TrashDurability);
        public float HeavyValueMult => (1f + V18Sum(V18Effect.HeavyValue)) * (1f + V18Sum(V18Effect.SortBonus) + V18Sum(V18Effect.RarityBonus));
        public float HeavyJackpot => V18Sum(V18Effect.HeavyJackpot);
        /// <summary>장비 1회 타격/틱이 고가치 쓰레기에서 깎는 내구도.</summary>
        public float HeavyTickDamage => (Mode == GearMode.BlackHole ? 4.2f : Mode == GearMode.Orb ? 3.6f
            : HasVacuum ? 2.4f : HasBroom ? 1.5f : 1.05f) * (1f + V18Sum(V18Effect.HeavyDamage));
        /// <summary>원기옥 압축이 초당 깎는 내구도.</summary>
        public float OrbDps => 5.5f * OrbGrowRate * (1f + V18Sum(V18Effect.HeavyDamage));
        /// <summary>블랙홀 소용돌이가 초당 깎는 내구도.</summary>
        public float BlackHoleDps => 6.5f * BlackHolePull * (1f + V18Sum(V18Effect.HeavyDamage));

        public bool AnyHeavyUnlocked => HeavyCan;

        /// <summary>스폰 시 고가치 쓰레기 등급을 뽑는다(0 = 일반).</summary>
        public int RollHeavyClass()
        {
            if (!HeavyCan) return 0;
            float total = 0.62f;
            if (HeavyEwaste) total += 0.26f;
            if (HeavyBig) total += 0.09f;
            if (HeavyGold) total += 0.03f;
            float r = Random.value * total;
            if (HeavyGold && r >= total - 0.03f) return 4;
            if (HeavyBig && r >= total - 0.03f - 0.09f) return 3;
            if (HeavyEwaste && r >= 0.62f) return 2;
            return 1;
        }

        public int HeavyValueFor(int cls)
        {
            int b = (cls > 0 && cls < HeavyBaseValue.Length) ? HeavyBaseValue[cls] : 6;
            float v = b * HeavyValueMult;
            if (cls == 4 && HeavyJackpot > 0f && Random.value < 0.25f) v *= 1f + HeavyJackpot;
            return Mathf.Max(2, Mathf.RoundToInt(v));
        }

        public float HeavyHpFor(int cls)
        {
            float b = (cls > 0 && cls < HeavyHp.Length) ? HeavyHp[cls] : 4f;
            return b * HeavyHpMult;
        }

        // ===== v1.8 쓰레기 물량·밀도 =====
        public float TrashDensity01 => Mathf.Clamp(V18Sum(V18Effect.TrashDensity), 0f, 0.95f);
        public int WaveBonusCount => Mathf.Clamp(Mathf.RoundToInt(V18Sum(V18Effect.WaveBonus)), 0, 2);
        public float TrashCluster01 => Mathf.Clamp(V18Sum(V18Effect.TrashCluster), 0f, 0.9f);
        public bool MegaDump => V18Sum(V18Effect.MegaDump) > 0f;

        public int VehicleVisualStage => HasTruck1Ton ? 3 : HasDamas ? 2 : HasCart ? 1 : 0;
        public string CurrentVehicleName =>
            HasTruck1Ton ? "1톤 청소 트럭" :
            HasDamas ? "다마스 (자동흡입)" :
            HasCart ? "리어카" : "공공 쓰레기통";

        // ===== v1.8.1 수거 차량 점진 확장 (리어카부터 자동/무한 사기 방지 → 최종 1톤 트럭에서 원격 자동!) =====
        // 1) 공공 쓰레기통: 차량 앞(2.4m) 수동 [RMB] 정산 · 단가 ×1.00
        // 2) 리어카: 정산 가능 거리 4.2m 확장 (수동 [RMB] 정산) · 단가 ×1.20
        // 3) 다마스: 정산 거리 6.2m + 마당 진입 시 자동 흡입 정산 해금 · 단가 ×1.45
        // 4) 1톤 청소 트럭 (최종): 정산 거리 9.0m + 문 앞/방 안 80% 원격 자동 수거 해금 · 단가 ×1.80
        public float VehicleSettleMultiplier => VehicleVisualStage == 0 ? 1.00f
            : VehicleVisualStage == 1 ? 1.20f
            : VehicleVisualStage == 2 ? 1.45f : 1.80f;
        public float VehicleSettleReach => VehicleVisualStage == 0 ? 2.4f
            : VehicleVisualStage == 1 ? 4.2f
            : VehicleVisualStage == 2 ? 6.2f : 9.0f;
        public bool VehicleHasYardSuction => VehicleVisualStage >= 2;    // 다마스(3단계)부터 마당 자동흡입
        public bool VehicleHasPorchSettle => VehicleVisualStage >= 3;    // 1톤 트럭(최종) 현관 앞 자동정산
        public bool VehicleHasRemoteSettle => VehicleVisualStage >= 3;   // 1톤 트럭(최종) 방 안 80% 원격 자동수거
        public bool HasAutoVehicleSuction => VehicleHasYardSuction;
        // [v1.7] 차량 보조 효과도 전부 노드화: 기본은 차량 단계 배수뿐이고,
        //        대량 정산 보너스(기준 이상 채워 정산하면 추가 수익)·거래 배수·원격 쿨다운은 노드로 해금.
        public float VehicleBulkBonus => V18Sum(V18Effect.VehicleBulk);
        public float VehicleTradeMult => 1f + V18Sum(V18Effect.VehicleMult);
        public float VehicleEffectiveSettleMult => (VehicleSettleMultiplier + VehicleBulkBonus) * VehicleTradeMult;
        public float BulkThreshold01 => Mathf.Clamp(0.80f - V18Sum(V18Effect.BulkThreshold), 0.35f, 0.80f);
        public float BulkSettleBonus => V18Sum(V18Effect.BulkSettle);
        public float RemoteSettleFill01 => BulkThreshold01;
        public float RemoteSettleCooldownSec => Mathf.Max(0.08f, 0.35f * (1f - Mathf.Clamp(V18Sum(V18Effect.RemoteCooldown), 0f, 0.60f)));

        // ===== v1.7 큰 봉투 터짐: 연쇄 흡입·피버 충전·현금 보너스는 노드로만 해금 =====
        public bool HasChainSuck => V18Sum(V18Effect.ChainUnlock) > 0f;
        public float ChainSuckRadius => HasChainSuck ? 1.8f + V18Sum(V18Effect.ChainRadius) : 0f;
        public int ChainSuckMax => HasChainSuck ? 8 + Mathf.RoundToInt(V18Sum(V18Effect.ChainCount) * 2f) : 0;
        public float BurstFeverGain => V18Sum(V18Effect.BurstFever);
        public float BurstCashBonus => V18Sum(V18Effect.BurstCash);

        public string VehiclePerkLabel =>
            VehicleVisualStage == 3 ? $"원격 자동수거 ({RemoteSettleFill01 * 100f:0}%)"
            : VehicleVisualStage == 2 ? $"마당 자동흡입 · 반경 {VehicleSettleReach:0.0}m"
            : VehicleVisualStage == 1 ? $"정산 반경 {VehicleSettleReach:0.0}m"
            : $"차량 앞 {VehicleSettleReach:0.0}m";

        // ===== 연속 흡입 피버 =====
        public float ContinuousFever01 { get; private set; } = 0f;
        private float _feverDecayDelay = 0f;
        public bool HasFeverEngine => LvFever > 0;
        public bool IsFeverHot => ContinuousFever01 >= 0.75f;
        public float FeverMaxMult => 1f + V18Sum(V18Effect.FeverMax);
        public float FeverRadiusBonus => HasFeverEngine ? ContinuousFever01 * (0.04f + LvFever * 0.06f) * FeverMaxMult : 0f;
        public float FeverSpeedBonus => HasFeverEngine ? ContinuousFever01 * (0.08f + LvFever * 0.08f) * FeverMaxMult : 0f;
        public float FeverPriceMult => (LvFever > 0 && IsFeverHot)
            ? 1f + (0.15f + LvFever * 0.08f) * FeverMaxMult + V18Sum(V18Effect.FeverPrice)
            : 1.0f;

        // ===== 수거통 (초반 35칸 → 11단계 최대 1,500칸!) =====
        private static readonly int[] BinCaps = { 35, 50, 70, 95, 130, 180, 250, 340, 480, 680, 980, 1500 };
        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int BinCapacity => Mathf.RoundToInt(BinCaps[Mathf.Clamp(TotalBinLv, 0, BinCaps.Length - 1)] * (HasVacuum ? 2 : 1) * (1f + V18Sum(V18Effect.BinCap)));
        public float BinFill01 => Mathf.Clamp01(CollectingCount / (float)Mathf.Max(1, BinCapacity));
        public bool IsBinFull => CollectingCount >= BinCapacity;
        public bool CanSettle => CollectingCount > 0;

        // ===== [v2.2] 초기 초당 1개(1.00s) → 최종 강화 시 초고속(0.06~0.07s) 정직한 커브 =====
        private static readonly float[] HoldDurCurve = { 1.00f, 0.84f, 0.70f, 0.58f, 0.47f, 0.37f, 0.28f, 0.20f, 0.14f, 0.10f, 0.07f };
        public float EffectiveHoldDuration
        {
            get
            {
                float baseDur = HoldDurCurve[Mathf.Clamp(TotalHoldLv, 0, HoldDurCurve.Length - 1)];
                if (HasBroom) baseDur *= 0.86f;
                if (HasVacuum) baseDur *= 0.78f;
                float workMult = Mathf.Max(0.45f, StenchWorkSpeedMult);
                float totalSpd = workMult * (1f + FeverSpeedBonus + TrustBuffSpeedBonus) * OxygenSuctionMult * (1f + V18Sum(V18Effect.HoldSpeed));
                return Mathf.Clamp(baseDur / totalSpd, 0.06f, 1.5f);
            }
        }

        public float SweepRadius
        {
            get
            {
                float toolBase = HasVacuum ? 0.90f : HasBroom ? 0.50f : 0.28f;
                float r = toolBase + TotalRadLv * 0.08f;
                return r * (1f + FeverRadiusBonus) * (1f + V18Sum(V18Effect.Radius));
            }
        }

        /// <summary>
        /// [v1.9] 수평 거리(XZ) 기준 줍기 사거리! 초반 맨손 2.20m → 최대 6.20m.
        /// (카메라 높이 1.5m 때문의 사각지대 완전 제거!)
        /// </summary>
        public float PickupReach
        {
            get
            {
                float baseReach = 2.20f + TotalRadLv * 0.34f + V18Sum(V18Effect.Radius) * 2.1f;
                if (HasBroom) baseReach += 0.65f;
                if (HasVacuum) baseReach += 1.05f;
                if (HasOrbGun || HasBlackHole) baseReach += 1.20f;
                return Mathf.Clamp(baseReach, 2.20f, 6.20f);
            }
        }

        public bool IsMopSlot => Phase == GamePhase.Phase2_FloorMopping;
        private bool _notifiedTrashDone;

        /// <summary>[v1.8.1 수정 #7] [1] 쓰레기 수거 / [2] 바닥 걸레질 수동 전환 (강제 자동 전환 제거!).</summary>
        public void SetToolSlot(bool mopMode)
        {
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;
            GamePhase next = mopMode ? GamePhase.Phase2_FloorMopping : GamePhase.Phase1_TrashRemoval;
            if (Phase == next) return;
            Phase = next;
            sfx.PlayPop(mopMode ? 1.15f : 0.92f);
            ShowToast(mopMode ? (HasMopUnlocked ? "[2] T자 대걸레" : "[2] 물티슈") : $"[1] {ToolBadge}");
        }

        public int AutoGrabPerTick
        {
            get
            {
                int extra = Mathf.RoundToInt(V18Sum(V18Effect.GrabCount)) + (HasTwoHand ? 1 : 0);
                if (HasVacuum) return 2 + LvRad2 + LvHold3 + extra;
                if (HasBroom) return 2 + LvRad1 / 2 + LvRad2 + extra;
                return 1 + LvRad1 / 3 + extra;
            }
        }

        // [v1.7] 작은 쓰레기 치명타·연쇄는 '예민한 감각' 가지 노드로만 열린다.
        public float CritChance => Mathf.Clamp(V18Sum(V18Effect.CritChance), 0f, 0.45f);
        public float ChainChance => Mathf.Clamp(V18Sum(V18Effect.ChainChance), 0f, 0.45f);
        public int ChainExtraCount => V18Sum(V18Effect.ChainCount) > 0f ? 1 + Mathf.RoundToInt(V18Sum(V18Effect.ChainCount)) : 0;
        // [v2.0] 이동속도 폭주 방지 상한선(최대 1.85배 — 실내 쾌속 + 벽 충돌 방지!)
        public float MoveSpeedTotalMult => Mathf.Clamp(
            StenchMoveSpeedMult * (1f + TotalSpeedLv * 0.05f) * (IsFeverHot && HasFeverEngine ? 1.08f : 1f) * Mathf.Min(1.25f, OxygenMoveMult) * (1f + Mathf.Min(0.35f, V18Sum(V18Effect.MoveSpeed))),
            0.45f, 1.85f);
        // [v2.0] 중복 곱연산 제거 (StageRewardMult 이중 곱 제거! 합연산 중심으로 예측 가능한 7스테이지 경제!)
        public float EffectiveTrashPriceMult => (1f + TotalPriceLv * 0.12f + V18Sum(V18Effect.Price)) * (HasVipContract ? 1.25f : 1f) * Mathf.Min(1.65f, FeverPriceMult);
        public float FistDamage => (1.45f + TotalFistLv * 0.60f + (HasVacuum ? 1.5f : 0f)) * (1f + V18Sum(V18Effect.FistPower));
        public float FistCritChance => Mathf.Clamp(V18Sum(V18Effect.FistCritChance), 0f, 0.60f);
        public float FistCritMult => 1.6f + V18Sum(V18Effect.FistCritMult);

        /// <summary>[v1.7] 큰 봉투 타격 1회의 데미지(치명타 판정 포함).</summary>
        public float RollFistDamage(out bool crit)
        {
            crit = FistCritChance > 0.001f && Random.value < FistCritChance;
            return FistDamage * (crit ? FistCritMult : 1f);
        }

        public float ScrubSpeedMult => (HasMopUnlocked ? 2.4f : 1.0f) * (1f + LvScrubPower * 0.12f)
            * (1f + V18Sum(V18Effect.ScrubSpeed)) * (1f + V18Sum(V18Effect.ScrubRange) * 0.6f);
        public float StainRewardMult => 1f + V18Sum(V18Effect.StainReward);
        public float ScrubFeverGain => V18Sum(V18Effect.ScrubFever);
        public int RoombaCount => LvRoombaCount;
        public float RoombaEatInterval => Mathf.Max(0.45f, (1.00f - TotalRoombaSpdLv * 0.02f)
            * (1f - Mathf.Clamp(V18Sum(V18Effect.RoombaPickup), 0f, 0.35f))
            * (1f - Mathf.Clamp(V18Sum(V18Effect.RoombaBattery), 0f, 0.30f)));
        public float RoombaMoveSpeed => (1.60f + TotalRoombaSpdLv * 0.03f) * (1f + V18Sum(V18Effect.RoombaSpeed));
        public float RoombaDetectRadius => 0.85f + V18Sum(V18Effect.RoombaDetect);

        // ===== 연속 정산 콤보 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;
        public float MaxComboWindow => 6.0f + LvComboTime * 2.5f + V18Sum(V18Effect.ComboWindow);

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

        public int RemainingBigPileCount
        {
            get
            {
                int rem = 0;
                foreach (var p in _activeBigPiles) if (p != null && !p.IsCleaned) rem++;
                return rem;
            }
        }
        public int Phase1Total => _activeBigPiles.Count * 10 + (trashSwarm != null ? trashSwarm.StageTotalItems : 0);
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
        public TreasureTarget ActiveTreasureTarget { get; private set; }
        public bool ActiveTreasureWatchedByCctv { get; private set; }
        public float TrustBuffTimer { get; private set; }
        public float TrustBuffSpeedBonus => TrustBuffTimer > 0f ? 0.22f : 0f;
        private int _pickupTreasureRollCount;
        private int _pickupTreasuresFoundThisStage;
        private bool _designatedDroppedThisStage;
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

        public bool FoundClientLostItem { get; private set; }
        public bool StoleDesignatedItem { get; private set; }
        public bool WasCaughtStealing { get; private set; }
        public int LastStolenValueTotal { get; private set; }
        public string LastReviewComment { get; private set; } = "";
        public int ActiveCctvCount { get; private set; }
        private SecurityCameraController[] _stageCctvs = new SecurityCameraController[0];

        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;
        private float _pitchCombo;
        private float _pitchResetTimer;
        private float _atmoCleanSmoothed;
        private float _autoSuctionCooldown;
        private float _porchSettleCooldown;
        private float _remoteSettleCooldown;
        private float _lidOpenTimer;
        private float _lidOpen01;
        private readonly Transform[] _vehicleLidPivots = new Transform[4];

        // ===== v1.8 세분화 강화 트리 (코어 28 + 마이크로 240 = 268노드) =====
        private readonly Dictionary<string, int> _v17Levels = new Dictionary<string, int>();
        private readonly float[] _v17Sums = new float[(int)V18Effect.Count];
        private Transform _propRoot;
        private InteractiveDoor _entranceDoor;

        /// <summary>v1.7 노드 효과 레벨 합계(구매할 때마다 재계산).</summary>
        public float V18Sum(V18Effect effect)
        {
            int i = (int)effect;
            return (i >= 0 && i < _v17Sums.Length) ? _v17Sums[i] : 0f;
        }

        private void RebuildV18Sums()
        {
            for (int i = 0; i < _v17Sums.Length; i++) _v17Sums[i] = 0f;
            foreach (var kv in _v17Levels)
            {
                var n = UpgradeTreeV18.Get(kv.Key);
                if (n == null || kv.Value <= 0) continue;
                int i = (int)n.effect;
                if (i >= 0 && i < _v17Sums.Length) _v17Sums[i] += n.perLevel * kv.Value;
            }
        }

        private string SerializeV18Nodes()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in _v17Levels)
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            return sb.ToString();
        }

        private void DeserializeV18Nodes(string data)
        {
            _v17Levels.Clear();
            if (!string.IsNullOrEmpty(data))
            {
                var parts = data.Split(',');
                foreach (var p in parts)
                {
                    int c = p.IndexOf(':');
                    if (c <= 0) continue;
                    string id = p.Substring(0, c);
                    if (!int.TryParse(p.Substring(c + 1), out int lv)) continue;
                    var n = UpgradeTreeV18.Get(id);
                    if (n == null) continue;
                    _v17Levels[id] = Mathf.Clamp(lv, 0, n.maxLevel);
                }
            }
            RebuildV18Sums();
        }

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
            MouseSensitivity = PlayerPrefs.GetFloat("TC3D_MouseSens", 0.14f);
            MasterVolume = PlayerPrefs.GetFloat("TC3D_MasterVol", 0.85f);
            AudioListener.volume = MasterVolume;
        }

        private void Start()
        {
            // v1.5.1 진단용: 스테이지 진입 시각을 남겨, 로딩/스폰/첫 렌더 중 어디서 멈추는지 콘솔에서 구분한다.
            Debug.Log($"[TrashCleaner3D] Stage 진입 · {SceneManager.GetActiveScene().name} · t={Time.realtimeSinceStartup:0.00}s");

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
            FixOverlappingFloorHeights();
            UpdateVehicleStageVisuals();
            if (spawnFieldUpgradeStation)
            {
                EnsureFieldUpgradeStation();
            }
            else
            {
                foreach (var ks in FindObjectsByType<FieldUpgradeStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    ks.gameObject.SetActive(false);
            }
            ApplyAtmosphereImmediate();
            EnsureEntranceDoorClosedAtStart();

            StagePropLayout.Apply(this, CurrentStage);
            _propRoot = StagePropLayout.Root;
            CollectTargetsForCurrentStage();
            _stageCctvs = FindObjectsByType<SecurityCameraController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            ActiveCctvCount = _stageCctvs != null ? _stageCctvs.Length : 0;

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
            CampaignProgressStore.SavedV18Nodes = SerializeV18Nodes();
        }

        private void RestoreStateFromCampaignStore()
        {
            // v1.7 세분화 노드는 저장 여부와 무관하게 항상 복원(빈 문자열이면 0레벨)
            DeserializeV18Nodes(CampaignProgressStore.SavedV18Nodes);
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
                _entranceDoor = door;
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

            // v1.6: 큰 봉투·얼룩은 StagePropLayout 이 만든 공통 루트 아래에 모여 있다.
            //   (씬에 남은 비활성 잔여 컨테이너와 무관하게, 실제 배치된 것만 집계)
            if (_propRoot == null)
            {
                GameObject propRootGo = GameObject.Find("StageProps_v16_Root");
                if (propRootGo != null) _propRoot = propRootGo.transform;
            }

            if (_propRoot != null)
            {
                foreach (var c in _propRoot.GetComponentsInChildren<CleanableTarget>(true))
                {
                    if (c.kind == CleanableKind.FloorStain) _activeStains.Add(c);
                    else if (c.kind == CleanableKind.TrashPile) _activeBigPiles.Add(c);
                }
                return;
            }

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

        /// <summary>
        /// [v1.8.1 수정 #2] Stage 2~7에서 실내 Floor_Main(y=0)과 Parking_Floor/Entrance_PorchFloor가
        /// 정확히 같은 높이(y=0)에서 겹쳐 지직거리는 Z-fighting을 런타임에서도 안전하게 분리한다.
        /// </summary>
        private void FixOverlappingFloorHeights()
        {
            GameObject parking = GameObject.Find("Parking_Floor");
            if (parking != null)
            {
                float topY = parking.transform.position.y + parking.transform.lossyScale.y * 0.5f;
                if (topY > -0.04f)
                {
                    Vector3 p = parking.transform.position;
                    p.y -= (topY + 0.06f);
                    parking.transform.position = p;
                }
            }
            GameObject porch = GameObject.Find("Entrance_PorchFloor");
            if (porch != null)
            {
                float topY = porch.transform.position.y + porch.transform.lossyScale.y * 0.5f;
                if (topY > -0.015f)
                {
                    Vector3 p = porch.transform.position;
                    p.y -= (topY + 0.025f);
                    porch.transform.position = p;
                }
            }
        }

        public bool IsPlayerInTruckSettleZone(Vector3 playerPos)
        {
            if (truckBedGoal == null) return false;
            Vector3 d = playerPos - truckBedGoal.position;
            d.y = 0f;
            return d.magnitude <= VehicleSettleReach;
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
            if (_porchSettleCooldown > 0f) _porchSettleCooldown -= dt;
            if (_remoteSettleCooldown > 0f) _remoteSettleCooldown -= dt;
            if (TrustBuffTimer > 0f) TrustBuffTimer = Mathf.Max(0f, TrustBuffTimer - dt);

            // [v2.3] 쓰레기통/차량 투입 시 뚜껑 경첩 열림 → 스르륵 닫힘 애니메이션!
            if (_lidOpenTimer > 0f) _lidOpenTimer = Mathf.Max(0f, _lidOpenTimer - dt);
            float lidTarget = _lidOpenTimer > 0f ? 1f : 0f;
            _lidOpen01 = Mathf.MoveTowards(_lidOpen01, lidTarget, (_lidOpenTimer > 0f ? 7.5f : 3.2f) * dt);
            float lidAngle = -78f * (_lidOpen01 * _lidOpen01 * (3f - 2f * _lidOpen01));
            for (int i = 0; i < _vehicleLidPivots.Length; i++)
            {
                if (_vehicleLidPivots[i] != null)
                    _vehicleLidPivots[i].localRotation = Quaternion.Euler(lidAngle, 0f, 0f);
            }

            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].age += dt;
                FloatingTexts[i].worldPos += Vector3.up * (0.65f * dt);
                if (FloatingTexts[i].age >= FloatingTexts[i].duration) FloatingTexts.RemoveAt(i);
            }

            if (Phase == GamePhase.TreasureDecision)
            {
                TreasurePopupOpenAge += Time.unscaledDeltaTime;
                if (TreasureDecisionLockTimer > 0f) TreasureDecisionLockTimer = Mathf.Max(0f, TreasureDecisionLockTimer - Time.unscaledDeltaTime);
                return;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            if (ThrowComboTimer > 0f) { ThrowComboTimer -= dt; if (ThrowComboTimer <= 0f) ThrowComboCount = 0; }
            if (_feverDecayDelay > 0f) _feverDecayDelay -= dt;
            else if (ContinuousFever01 > 0f)
            {
                // v1.7: 산소 부스트(저악취 + 방독면 노드) + 피버 유지 노드로 피버가 천천히 식는다.
                float hold = Mathf.Clamp(OxygenFeverHold01 + V18Sum(V18Effect.FeverHold), 0f, 0.85f);
                ContinuousFever01 = Mathf.MoveTowards(ContinuousFever01, 0f, dt * 0.28f * (1f - hold));
            }

            // 환기 + 다마스/1톤트럭 마당 진입 시 원거리 자동흡입 정산!
            if (player != null)
            {
                bool wasOutside = IsOutsideVentilating;
                IsOutsideVentilating = player.transform.position.z > doorThresholdZ;
                if (IsOutsideVentilating)
                {
                    if (!wasOutside && StenchPercent >= 20f) FreshAirFlash01 = 1f;
                    if (StenchPercent > 0.1f) StenchPercent = Mathf.Max(0f, StenchPercent - 48f * dt);

                    // v1.6 차량 2단계(리어카)부터: 마당에 발만 내디뎌도 자동 흡입 정산!
                    if (VehicleHasYardSuction && CanSettle && _autoSuctionCooldown <= 0f && player.transform.position.z > doorThresholdZ + 0.45f)
                    {
                        _autoSuctionCooldown = 1.2f;
                        Vector3 lowOrigin = GetLowRightPourOrigin();
                        SettleBinAtTruck(lowOrigin, true);
                    }
                }
                else
                {
                    float roomFilth = Mathf.Lerp(1.0f, 0.25f, OverallCleanPercent / 100f);
                    StenchPercent = Mathf.Min(100f, StenchPercent + StenchGainPerSec * roomFilth * dt);

                    // v1.6 차량 3단계(다마스): 현관 문 앞만 가도 자동 정산!
                    if (VehicleHasPorchSettle && CanSettle && _porchSettleCooldown <= 0f)
                    {
                        Vector3 doorPos = _entranceDoor != null ? _entranceDoor.transform.position : new Vector3(0f, 0f, doorThresholdZ - 0.5f);
                        Vector3 flat = new Vector3(player.transform.position.x - doorPos.x, 0f, player.transform.position.z - doorPos.z);
                        if (flat.magnitude <= 2.4f)
                        {
                            _porchSettleCooldown = 0.9f;
                            SettleBinAtTruck(GetLowRightPourOrigin(), true);
                        }
                    }

                    // v1.7 차량 4단계(1톤 트럭): 방 안에서 수거통이 기준(기본 80%·노드로 하향) 차면 원격 자동 정산
                    if (VehicleHasRemoteSettle && _remoteSettleCooldown <= 0f &&
                        CollectingCount > 0 && BinFill01 >= RemoteSettleFill01)
                    {
                        _remoteSettleCooldown = RemoteSettleCooldownSec;
                        SettleBinAtTruck(GetLowRightPourOrigin(), true, true);
                    }
                }
            }

            if (StenchStage >= 2 && !IsOutsideVentilating)
            {
                _coughTimer -= dt;
                if (_coughTimer <= 0f)
                {
                    _coughTimer = StenchStage >= 3 ? 2.0f : 3.2f;
                    if (StenchStage >= 2) CoughKick01 = StenchStage >= 3 ? 0.85f : 0.45f;
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
                ShowToast($"★ {roomName} +${bonus} ({trashSwarm.ClearedRoomCount}/{trashSwarm.RoomCount})");
            else
                ShowToast($"★ {roomName} +${bonus}");
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.4f)
        {
            FloatingTexts.Add(new FloatingWorldText { worldPos = worldPos, text = text, color = color, age = 0f, duration = duration });
        }

        public void OnInstancedTrashCollected(int itemValue, int typeIdx, string typeLabel)
        {
            // [v2.2] 수거통 용량 엄수 — 절대 최대 개수를 넘지 않는다!
            if (CollectingCount >= BinCapacity) return;

            int gainedVal = Mathf.Max(1, Mathf.RoundToInt(itemValue * EffectiveTrashPriceMult));
            CollectingCount++;
            CollectingPendingValue += gainedVal;

            _feverDecayDelay = 1.75f;
            if (HasFeverEngine)
                ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + 0.045f * (1f + V18Sum(V18Effect.FeverGain)));

            _pitchResetTimer = 0.48f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.06f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.36f);

            // [v2.2] 쓰레기를 줍다가 우연히 숨겨진 보물 / 의뢰인 분실물 발견!
            TryRollPickupTreasure();

            CheckPhase1Complete();
        }

        /// <summary>[v1.9] 수거통이 가득 찼을 때의 한 줄 표시(깨짐 없는 기호).</summary>
        public string FillBinTag =>
            VehicleHasRemoteSettle ? "■ FULL · 원격 정산"
            : VehicleHasPorchSettle ? "■ FULL · 문 앞 정산"
            : VehicleHasYardSuction ? "■ FULL · 마당 정산"
            : "■ FULL · [RMB] 정산";

        /// <summary>
        /// [v1.8] 원기옥·블랙홀처럼 여러 개를 한꺼번에 삼킨 경우의 조용한 집계.
        /// 개별 팝 사운드/팝업을 내지 않고 수거통에 개수·금액만 쌓는다(프레임·소리 부담 억제).
        /// </summary>
        public void OnBulkTrashCollected(int items, int rawValue)
        {
            if (items <= 0) return;
            // [v2.0] 수거통 용량 엄수 — 절대 BinCapacity를 초과하지 않는다!
            int cap = Mathf.Max(1, BinCapacity);
            int space = Mathf.Max(0, cap - CollectingCount);
            if (space <= 0) return;
            int addItems = Mathf.Min(items, space);
            float ratio = addItems / (float)items;
            CollectingCount += addItems;
            CollectingPendingValue += Mathf.Max(1, Mathf.RoundToInt(rawValue * ratio * EffectiveTrashPriceMult));
            _feverDecayDelay = 1.75f;
            ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + 0.004f * addItems);
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
        /// v1.6 정산:
        /// - 보유한 개수 그대로 허리 아래 낮은 아치로 실제 3D 쓰레기가 날아가 적재함에 쌓임
        /// - 적재량 한계/4초 배차 대기 삭제 → 차량은 '정산 단가 배수 + 편의성 해금'으로만 작동
        ///   (공공 ×1.00 / 리어카 ×1.20 / 다마스 ×1.45 / 1톤 ×1.80 + 강화 트리 가산)
        /// - remote=true 는 1톤 트럭의 '방 안 원격 자동수거' — 트럭 위에서 떨어져 쌓이는 연출
        /// </summary>
        public bool SettleBinAtTruck(Vector3 lowPourOrigin, bool triggeredByAutoSuction = false, bool remote = false, float extraMult = 1f)
        {
            if (CollectingCount <= 0) return false;

            int count = CollectingCount;
            int rawBase = Mathf.Max(CollectingPendingValue, count * 2);
            float fill01 = BinFill01;
            CollectingCount = 0;
            CollectingPendingValue = 0;

            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(5, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = MaxComboWindow;

            // [v2.0] 정산 배율 합연산 통합 (10억 달러 6중 곱연산 폭발 원천 차단!)
            float comboAdd = LvComboTime > 0 ? (ThrowComboCount - 1) * 0.08f : 0f;
            float settleAdd = V18Sum(V18Effect.SettleBonus) * 0.5f;
            float bulkAdd = fill01 >= BulkThreshold01 ? BulkSettleBonus * 0.5f : 0f;
            float vehAdd = VehicleBulkBonus * 0.45f + V18Sum(V18Effect.VehicleMult) * 0.45f;
            float extraAdd = Mathf.Max(0f, extraMult - 1f) * 0.5f;
            float finalSettleMult = VehicleSettleMultiplier * (1f + comboAdd + settleAdd + bulkAdd + vehAdd + extraAdd);
            int totalPay = Mathf.Max(1, Mathf.RoundToInt(rawBase * finalSettleMult));

            Money += totalPay;
            _lidOpenTimer = 1.15f;
            sfx.PlayChime();
            if (player != null) player.AddCameraShake(remote ? 0.016f : 0.028f, 0.11f);

            // 1:1 실제 3D 쓰레기 투입 & 적재함 쌓임 (원격일 때는 트럭 위에서 낙하)
            if (truckTrashVisual != null && truckBedGoal != null)
            {
                Vector3 origin = remote
                    ? truckBedGoal.position + Vector3.up * (2.0f + GetGoalOpeningHalfExtents().y)
                    : lowPourOrigin;
                truckTrashVisual.PourExactCount(origin, truckBedGoal, GetGoalOpeningHalfExtents(), count, VehicleVisualStage);
                if (remote) BurstImpactFx.Spawn(truckBedGoal.position + Vector3.up * 0.4f, new Color(0.55f, 0.95f, 1f), 2.4f);
            }

            string prefix = extraMult > 1.02f ? "구슬 정산! " : remote ? "원격 정산! " : triggeredByAutoSuction ? "자동 정산! " : (ThrowComboCount >= 2 ? $"{ThrowComboCount}연속! " : "");
            string multTag = $" ×{(VehicleEffectiveSettleMult * extraMult):0.00}";
            Vector3 popPos = truckBedGoal != null ? truckBedGoal.position + Vector3.up * 0.9f : lowPourOrigin + Vector3.up * 0.5f;
            SpawnFloatingText(popPos, $"{prefix}{count}개 정산 +${totalPay}{multTag}", new Color(0.45f, 1f, 0.55f), 1.9f);

            CheckPhase1Complete();
            return true;
        }

        /// <summary>
        /// v1.7 큰 봉투 터짐:
        ///  파편 파티클 + 충격 링 + 짧은 화면 흔들림 + 경쾌한 "팡!"
        ///  + [연쇄 흡입 노드] 주변 작은 쓰레기 미니 블랙홀
        ///  + [봉투 피버 노드] 피버 게이지 충전
        ///  + [봉투 현금 노드] 현금 보너스 팝업
        ///  → v1.6의 '공짜 피버 +25%·현금 보너스·연쇄 흡입'은 전부 기본 잠김.
        /// </summary>
        public void OnBigPileBurst(CleanableTarget pile)
        {
            Vector3 pos = pile != null ? pile.transform.position : Vector3.zero;

            sfx.PlayBigBurst();
            if (player != null) player.AddCameraShake(0.052f, 0.17f);
            BurstImpactFx.Spawn(pos + Vector3.up * 0.04f, new Color(1f, 0.86f, 0.34f), Mathf.Max(0.8f, ChainSuckRadius + 0.7f));
            SparkleBurst.Spawn(pos, 28, 1.15f, new Color(1f, 0.94f, 0.66f));

            // [노드] 봉투 피버 충전
            float feverGain = BurstFeverGain;
            if (feverGain > 0f)
            {
                ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + feverGain);
                _feverDecayDelay = 2.4f;
            }

            // [노드] 봉투 현금 보너스
            int cash = Mathf.RoundToInt(BurstCashBonus * StageRewardMult);
            if (cash > 0)
            {
                Money += cash;
                SpawnFloatingText(pos + Vector3.up * 1.05f,
                    feverGain > 0f ? $"+${cash} 봉투 보너스 · FEVER +{feverGain * 100f:0}%" : $"+${cash} 봉투 보너스",
                    new Color(1f, 0.90f, 0.42f), 2.1f);
            }

            // [노드] 미니 블랙홀 연쇄 흡입 (주변 작은 쓰레기 → 플레이어 손)
            int chained = 0;
            if (HasChainSuck && trashSwarm != null && player != null)
            {
                Transform hand = player.rightToolPivot != null ? player.rightToolPivot : player.cameraTransform;
                chained = trashSwarm.ChainSuckAround(pos, ChainSuckRadius, hand, ChainSuckMax);
            }
            if (chained > 0)
                SpawnFloatingText(pos + Vector3.up * 0.55f, $"연쇄 흡입 {chained}개!", new Color(0.45f, 0.95f, 1f), 1.3f);

            // 파편(작은 쓰레기) 일부 추가 배출 — 연쇄 흡입과 함께 '와르르' 손맛
            if (trashSwarm != null) trashSwarm.SpawnBurstFountain(pos, 10);

            // [v2.1] 택배박스/큰봉투 언박싱 시 숨겨진 귀중품이 나오면 화려한 대형 득템 팝업 오픈!
            TreasureTarget dropped = StagePropLayout.TrySpawnBoxTreasure(pos, CurrentStage);
            if (dropped != null)
            {
                OpenTreasureDecision(dropped);
            }

            CheckPhase1Complete();
        }

        private void CheckPhase1Complete()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            // [v2.0] 95% 달성 시에도 큰 쓰레기봉지/박스가 1개라도 남아있으면 절대 완청 처리하지 않음!
            if (OverallCleanPercent >= 95.0f && RemainingBigPileCount == 0)
            {
                if (trashSwarm != null)
                {
                    int sw = trashSwarm.SweepRemainingForAutoComplete(out int remVal);
                    if (remVal > 0) Money += Mathf.Max(1, Mathf.RoundToInt(remVal * EffectiveTrashPriceMult));
                    if (sw > 0) ShowToast("★ 95% 달성! 잔여 구역 자동 마무리 완청!");
                }
                FinishRoomComplete();
                return;
            }

            bool p1Done = Phase1Total <= 0 || Phase1Cleaned >= Phase1Total;
            bool p2Done = Phase2Total <= 0 || Phase2Cleaned >= Phase2Total;

            if (p1Done && p2Done)
            {
                FinishRoomComplete();
                return;
            }

            if (p1Done && !p2Done && !_notifiedTrashDone)
            {
                _notifiedTrashDone = true;
                if (CollectingCount > 0)
                {
                    Money += CollectingPendingValue;
                    CollectingCount = 0;
                    CollectingPendingValue = 0;
                }
                sfx.PlayFanfare();
                ShowToast("쓰레기 완료! [2] 걸레로 얼룩 닦기");
            }
        }

        public bool IsPlayerWatchedByCctv(Vector3 worldPos)
        {
            if (_stageCctvs == null) return false;
            for (int i = 0; i < _stageCctvs.Length; i++)
            {
                if (_stageCctvs[i] != null && !_stageCctvs[i].IsBlinded && _stageCctvs[i].CanSeePosition(worldPos))
                    return true;
            }
            return false;
        }

        public void ReturnTreasure(TreasureTarget t)
        {
            if (t == null || t.State != TreasureState.HiddenInRoom) return;
            t.MarkReturned(StagePropLayout.ReturnShelfSlot, StagePropLayout.AllocateReturnShelfSlotIndex());
            if (t.isDesignatedLostItem)
            {
                FoundClientLostItem = true;
                int tip = Mathf.Max(45, Mathf.RoundToInt(t.value * 0.55f));
                Money += tip;
                sfx.PlayChime();
                SpawnFloatingText(t.transform.position + Vector3.up * 0.45f, $"분실물 선반 보관! +${tip}", new Color(0.32f, 0.96f, 1f), 2.2f);
                ShowToast($"★ 의뢰인 분실물 선반 보관! (+팁 ${tip} · 5성 확보)");
            }
            else
            {
                ReturnedCount++;
                int tip = Mathf.Max(20, Mathf.RoundToInt(t.value * 0.25f));
                Money += tip;
                SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 5f);
                sfx.PlayChime();
                SpawnFloatingText(t.transform.position + Vector3.up * 0.45f, $"정직 반환! +${tip}", new Color(0.42f, 0.96f, 0.62f), 1.8f);
                ShowToast($"정직 반환 +${tip} (발각 위험 -5%)");
            }
        }

        public void StealTreasure(TreasureTarget t)
        {
            if (t == null || t.State != TreasureState.HiddenInRoom) return;
            bool watched = IsPlayerWatchedByCctv(t.transform.position) || (player != null && IsPlayerWatchedByCctv(player.transform.position));
            t.MarkStolen();
            int cash = Mathf.Max(50, Mathf.RoundToInt(t.value * 1.35f));
            Money += cash;
            StolenCount++;
            LastStolenValueTotal += cash;
            LastStolenItemName = t.itemName;
            sfx.PlaySteal();

            if (t.isDesignatedLostItem)
            {
                StoleDesignatedItem = true;
                SuspicionPercent = 100f;
                StealAlertFlash01 = 1f;
                SpawnFloatingText(t.transform.position + Vector3.up * 0.50f, $"지정물 슈킹! +${cash}", new Color(1f, 0.35f, 0.28f), 2.2f);
                ShowToast($"! 지정 분실물 슈킹! +${cash} (클리어 시 발각 위험 100%!)");
            }
            else
            {
                float addSusp = t.suspicionGain * (watched ? 1.85f : 1f);
                SuspicionPercent = Mathf.Clamp(SuspicionPercent + addSusp, 0f, 98f);
                StealAlertFlash01 = watched ? 0.95f : 0.65f;
                SpawnFloatingText(t.transform.position + Vector3.up * 0.50f, $"슈킹 +${cash}", new Color(1f, 0.78f, 0.22f), 1.9f);
                ShowToast(watched ? $"! CCTV 목격 슈킹 +${cash} (위험 +{addSusp:0}%)" : $"몰래 슈킹! +${cash} (발각 위험 {SuspicionPercent:0}%)");
            }
        }

        public void ToggleTabUpgradeTree()
        {
            if (Phase == GamePhase.MindMapUpgrade) { CloseUpgradeKiosk(); return; }
            if (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping)
            {
                _phaseBeforeOverlay = Phase;
                Phase = GamePhase.MindMapUpgrade;
                UnlockCursor(true);
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(stain.rewardMoney * EffectiveTrashPriceMult * StainRewardMult));
            Money += pay;
            if (ScrubFeverGain > 0f)
            {
                ContinuousFever01 = Mathf.Clamp01(ContinuousFever01 + ScrubFeverGain);
                _feverDecayDelay = 1.2f;
            }
            sfx.PlayChime();
            SparkleBurst.Spawn(stain.transform.position + Vector3.up * 0.08f, 14, 0.65f, new Color(0.85f, 0.97f, 1f));
            SpawnFloatingText(stain.transform.position + Vector3.up * 0.42f, $"✦ 반짝! +${pay}", new Color(0.55f, 0.96f, 1f), 1.5f);
            CheckPhase1Complete();
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
                ShowToast($"ST.{stageNum - 1} 클리어 필요");
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
            EvaluateStealingAndReview();
            DisplayMoney = Money;
            CampaignProgressStore.MarkStageCleared(CurrentStage, LastStarRating);
            SaveStateToCampaignStore();
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        /// <summary>[v2.2] 쓰레기를 줍다가 우연히 숨겨진 보물 / 의뢰인 분실물 발견!</summary>
        private void TryRollPickupTreasure()
        {
            if (!CoreLoopConfig.EnableTreasureBranch) return;
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            if (_pickupTreasuresFoundThisStage >= 4) return;

            _pickupTreasureRollCount++;
            int stageTotal = trashSwarm != null ? Mathf.Max(100, trashSwarm.StageTotalItems) : 1400;
            int interval = Mathf.Clamp(stageTotal / 8, 22, 420);
            if (_pickupTreasureRollCount < interval) return;

            // 간격 도달 시 12% 확률로 발견(너무 오래 안 나오면 1.6배 간격에서 확정 발견)
            if (_pickupTreasureRollCount < Mathf.RoundToInt(interval * 1.6f) && Random.value > 0.12f) return;

            _pickupTreasureRollCount = 0;
            _pickupTreasuresFoundThisStage++;

            bool spawnDesignated = !_designatedDroppedThisStage && (_pickupTreasuresFoundThisStage == 2 || OverallCleanPercent >= 42f);
            if (spawnDesignated) _designatedDroppedThisStage = true;

            int baseVal = CurrentStage switch { 1 => 140, 2 => 450, 3 => 1400, 4 => 4200, 5 => 14000, 6 => 38000, 7 => 95000, _ => 140 };
            string itemName;
            if (spawnDesignated)
            {
                itemName = CurrentStage switch
                {
                    1 => "의뢰인의 금반지",
                    2 => "군번줄 은색 인식표",
                    3 => "행운의 2달러 지폐",
                    4 => "아이 돌반지",
                    5 => "회사 법인인감 도장",
                    6 => "할아버지 회중시계",
                    7 => "슈퍼카 스마트키",
                    _ => "의뢰인 분실물"
                };
            }
            else
            {
                string[] pool = { "숨겨진 비상금 봉투", "명품 손목시계", "순금 목걸이", "미개봉 무선이어폰", "백화점 상품권 다발", "고급 만년필", "미니 골드바" };
                itemName = pool[Random.Range(0, pool.Length)];
            }

            var dt = new DiscoveredTreasure
            {
                itemName = itemName,
                value = Mathf.RoundToInt(baseVal * (spawnDesignated ? 1.5f : Random.Range(0.95f, 1.45f))),
                rarity = spawnDesignated ? TreasureRarity.Legendary : (Random.value < 0.35f ? TreasureRarity.Rare : TreasureRarity.Common),
                isMentionedByClient = spawnDesignated,
                baseSuspicionGain = spawnDesignated ? 100f : Random.Range(18f, 30f)
            };
            OpenPickupTreasureDecision(dt);
        }

        public void OpenPickupTreasureDecision(DiscoveredTreasure tr)
        {
            if (tr == null) return;
            ActiveTreasureTarget = null;
            ActiveTreasure = tr;
            ActiveTreasureWatchedByCctv = player != null && IsPlayerWatchedByCctv(player.transform.position);
            TreasureDecisionLockTimer = 0.25f;
            TreasurePopupOpenAge = 0f;
            TreasureFlash01 = 1f;
            if (sfx != null) sfx.PlayFanfare();
            _phaseBeforeOverlay = (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping)
                ? Phase : GamePhase.Phase1_TrashRemoval;
            Phase = GamePhase.TreasureDecision;
            UnlockCursor(true);
        }

        /// <summary>[v2.1] 슈킹템/분실물 발견 시 화려한 대형 득템 팝업 오픈!</summary>
        public void OpenTreasureDecision(TreasureTarget target)
        {
            if (target == null || target.State != TreasureState.HiddenInRoom) return;
            ActiveTreasureTarget = target;
            ActiveTreasureWatchedByCctv = IsPlayerWatchedByCctv(target.transform.position) ||
                                          (player != null && IsPlayerWatchedByCctv(player.transform.position));
            TreasureRarity rar = target.isDesignatedLostItem ? TreasureRarity.Legendary
                : target.value >= 800 ? TreasureRarity.Rare : TreasureRarity.Common;
            ActiveTreasure = new DiscoveredTreasure
            {
                itemName = target.itemName,
                value = target.value,
                rarity = rar,
                isMentionedByClient = target.isDesignatedLostItem,
                baseSuspicionGain = target.suspicionGain
            };
            TreasureDecisionLockTimer = 0.25f;
            TreasurePopupOpenAge = 0f;
            TreasureFlash01 = 1f;
            SparkleBurst.Spawn(target.transform.position + Vector3.up * 0.28f, 30, 1.35f,
                target.isDesignatedLostItem ? new Color(0.35f, 0.92f, 1f) : new Color(1f, 0.90f, 0.32f));
            if (sfx != null) sfx.PlayFanfare();
            _phaseBeforeOverlay = (Phase == GamePhase.Phase1_TrashRemoval || Phase == GamePhase.Phase2_FloorMopping)
                ? Phase : GamePhase.Phase1_TrashRemoval;
            Phase = GamePhase.TreasureDecision;
            UnlockCursor(true);
        }

        public void ResolveActiveTreasure(bool steal)
        {
            DiscoveredTreasure tr = ActiveTreasure;
            TreasureTarget t = ActiveTreasureTarget;
            bool watched = ActiveTreasureWatchedByCctv;
            ActiveTreasure = null;
            ActiveTreasureTarget = null;
            ActiveTreasureWatchedByCctv = false;
            Phase = _phaseBeforeOverlay;
            UnlockCursor(false);

            if (tr == null) return;
            if (t != null && t.State == TreasureState.HiddenInRoom)
            {
                if (steal) t.MarkStolen();
                else t.MarkReturned(null, 0);
            }

            if (!steal)
            {
                // [v2.2] 반환 시 돈 대신 비금전 메리트: 신뢰도 + 발각 위험 대폭 감소 + 흡입 속도 감사 버프 + 5성 확보!
                if (tr.isMentionedByClient)
                {
                    FoundClientLostItem = true;
                    TrustTokens += 2;
                    SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 25f);
                    TrustBuffTimer = Mathf.Max(TrustBuffTimer, 35f);
                    ShowToast("★ 분실물 반환! 신뢰도 +2 · 35초 흡입 +22% · 5성 확보");
                }
                else
                {
                    ReturnedCount++;
                    TrustTokens += 1;
                    SuspicionPercent = Mathf.Max(0f, SuspicionPercent - 15f);
                    TrustBuffTimer = Mathf.Max(TrustBuffTimer, 20f);
                    ShowToast("정직 반환! 신뢰도 +1 · 위험 -15% · 20초 흡입 +22%");
                }
            }
            else
            {
                // [v2.2] 슈킹 시 고액 즉시 현금 + 발각 위험 상승!
                int cash = Mathf.Max(80, Mathf.RoundToInt(tr.value * 1.35f));
                Money += cash;
                StolenCount++;
                LastStolenValueTotal += cash;
                LastStolenItemName = tr.itemName;
                if (sfx != null) sfx.PlaySteal();

                if (tr.isMentionedByClient)
                {
                    StoleDesignatedItem = true;
                    SuspicionPercent = 100f;
                    StealAlertFlash01 = 1f;
                    ShowToast($"! 지정 분실물 슈킹 +${cash:N0} (발각 위험 100%)");
                }
                else
                {
                    float addSusp = tr.baseSuspicionGain * (watched ? 1.85f : 1f);
                    SuspicionPercent = Mathf.Clamp(SuspicionPercent + addSusp, 0f, 98f);
                    StealAlertFlash01 = watched ? 0.95f : 0.65f;
                    ShowToast(watched ? $"! CCTV 목격 슈킹 +${cash:N0} (위험 +{addSusp:0}%)" : $"슈킹 +${cash:N0} (발각 위험 {SuspicionPercent:0}%)");
                }
            }
        }

        // ===== v1.9 정예 108노드 강화 트리 가격 & 해금 =====
        public int GetNodeCost(string id)
        {
            var v17 = UpgradeTreeV18.Get(id);
            if (v17 != null) return UpgradeTreeV18.Cost(v17, GetNodeLevel(id));

            switch (id)
            {
                case "hold1": return Mathf.RoundToInt(25 * Mathf.Pow(2.0f, LvHold1));
                case "rad1": return Mathf.RoundToInt(30 * Mathf.Pow(2.0f, LvRad1));
                case "broom": return 850;
                case "hold2": return Mathf.RoundToInt(1100 * Mathf.Pow(2.2f, LvHold2));
                case "rad2": return Mathf.RoundToInt(1400 * Mathf.Pow(2.2f, LvRad2));
                case "vacuum": return 18000;
                case "hold3": return Mathf.RoundToInt(24000 * Mathf.Pow(2.4f, LvHold3));
                case "fever": return Mathf.RoundToInt(1800 * Mathf.Pow(2.3f, LvFever));

                case "bin1": return Mathf.RoundToInt(28 * Mathf.Pow(2.0f, LvBin1));
                case "price1": return Mathf.RoundToInt(35 * Mathf.Pow(2.0f, LvPrice1));
                case "cart": return 650;
                case "bin2": return Mathf.RoundToInt(1200 * Mathf.Pow(2.2f, LvBin2));
                case "price2": return Mathf.RoundToInt(1600 * Mathf.Pow(2.25f, LvPrice2));
                case "damas": return 9500;
                case "bin3": return Mathf.RoundToInt(16000 * Mathf.Pow(2.35f, LvBin3));
                case "combo": return Mathf.RoundToInt(900 * Mathf.Pow(2.25f, LvComboTime));
                case "truck1t": return 120000;

                case "speed1": return Mathf.RoundToInt(30 * Mathf.Pow(2.0f, LvSpeed1));
                case "mask1": return Mathf.RoundToInt(30 * Mathf.Pow(2.0f, LvMask1));
                case "mop": return 350;
                case "speed2": return Mathf.RoundToInt(1300 * Mathf.Pow(2.2f, LvSpeed2));
                case "mask2": return Mathf.RoundToInt(1500 * Mathf.Pow(2.2f, LvMask2));
                case "scrub": return Mathf.RoundToInt(950 * Mathf.Pow(2.2f, LvScrubPower));

                case "fist1": return Mathf.RoundToInt(35 * Mathf.Pow(2.0f, LvFist1));
                case "roomba": return LvRoombaCount == 0 ? 1800 : LvRoombaCount == 1 ? 14000 : 85000;
                case "rspd1": return Mathf.RoundToInt(1600 * Mathf.Pow(2.2f, LvRoombaSpd1));
                case "fist2": return Mathf.RoundToInt(1800 * Mathf.Pow(2.25f, LvFist2));
                case "rspd2": return Mathf.RoundToInt(18000 * Mathf.Pow(2.35f, LvRoombaSpd2));
                default: return 999999;
            }
        }

        public bool IsNodeUnlocked(string id)
        {
            var v17 = UpgradeTreeV18.Get(id);
            if (v17 != null)
            {
                if (string.IsNullOrEmpty(v17.prereq)) return true;
                return GetNodeLevel(v17.prereq) >= v17.prereqLevel;
            }

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

        public int GetNodeLevel(string id)
        {
            var v17 = UpgradeTreeV18.Get(id);
            if (v17 != null) return _v17Levels.TryGetValue(id, out int lv17) ? lv17 : 0;

            return id switch
            {
            "hold1" => LvHold1, "rad1" => LvRad1, "broom" => HasBroom ? 1 : 0, "hold2" => LvHold2, "rad2" => LvRad2, "vacuum" => HasVacuum ? 1 : 0, "hold3" => LvHold3, "fever" => LvFever,
            "bin1" => LvBin1, "price1" => LvPrice1, "cart" => HasCart ? 1 : 0, "bin2" => LvBin2, "price2" => LvPrice2, "damas" => HasDamas ? 1 : 0, "bin3" => LvBin3, "combo" => LvComboTime, "truck1t" => HasTruck1Ton ? 1 : 0,
            "speed1" => LvSpeed1, "mask1" => LvMask1, "mop" => HasMopUnlocked ? 1 : 0, "speed2" => LvSpeed2, "mask2" => LvMask2, "scrub" => LvScrubPower,
            "fist1" => LvFist1, "roomba" => LvRoombaCount, "rspd1" => LvRoombaSpd1, "fist2" => LvFist2, "rspd2" => LvRoombaSpd2,
            _ => 0
            };
        }

        public int GetNodeMaxLevel(string id)
        {
            var v17 = UpgradeTreeV18.Get(id);
            if (v17 != null) return v17.maxLevel;

            return id switch
            {
            "hold1" => 3, "rad1" => 3, "broom" => 1, "hold2" => 4, "rad2" => 4, "vacuum" => 1, "hold3" => 3, "fever" => 3,
            "bin1" => 3, "price1" => 3, "cart" => 1, "bin2" => 4, "price2" => 4, "damas" => 1, "bin3" => 4, "combo" => 3, "truck1t" => 1,
            "speed1" => 3, "mask1" => 3, "mop" => 1, "speed2" => 3, "mask2" => 4, "scrub" => 4,
            "fist1" => 3, "roomba" => 3, "rspd1" => 3, "fist2" => 3, "rspd2" => 3,
            _ => 1
            };
        }

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
            foreach (var node in UpgradeTreeV18.Nodes)
                if (IsNodeUnlocked(node.id) && GetNodeLevel(node.id) < node.maxLevel && Money >= GetNodeCost(node.id)) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            if (!IsNodeUnlocked(id)) return false;
            int lv = GetNodeLevel(id), maxLv = GetNodeMaxLevel(id);
            if (lv >= maxLv) return false;
            int cost = GetNodeCost(id);
            if (Money < cost) return false;

            var v17 = UpgradeTreeV18.Get(id);
            if (v17 != null)
            {
                Money -= cost;
                _v17Levels[id] = lv + 1;
                RebuildV18Sums();
                if (v17.milestone) ShowToast($"★ {v17.title} Lv.{lv + 1} · {v17.EffectText(lv + 1)}");
                sfx.PlayChime();
                SaveStateToCampaignStore();
                return true;
            }

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
                case "cart": HasCart = true; UpdateVehicleStageVisuals(); ShowToast("★ 리어카 해금! (정산 ×1.20 · 마당 입구 자동흡입)"); break;
                case "bin2": LvBin2++; break;
                case "price2": LvPrice2++; break;
                case "damas": HasDamas = true; UpdateVehicleStageVisuals(); ShowToast("★ 다마스 해금! (정산 ×1.45 · 현관 문 앞 자동정산)"); break;
                case "bin3": LvBin3++; break;
                case "combo": LvComboTime++; break;
                case "truck1t": HasTruck1Ton = true; UpdateVehicleStageVisuals(); ShowToast("★ 1톤 트럭 해금! (정산 ×1.80 · 방 안 80% 원격 자동수거)"); break;

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
            {
                if (vehicleStageObjects[i] != null)
                {
                    vehicleStageObjects[i].SetActive(i == idx);
                    EnsureClosedVehicleLid(vehicleStageObjects[i], i);
                }
            }
        }

        /// <summary>[v2.3] 모든 쓰레기통/리어카/다마스/1톤 트럭에 뒷면 경첩 피벗이 달린 닫힌 뚜껑 부착 (투입 시 홱! 열림).</summary>
        private void EnsureClosedVehicleLid(GameObject veh, int stageIdx)
        {
            if (veh == null) return;
            Transform existingPivot = veh.transform.Find("_ClosedLidPivot");
            if (existingPivot != null)
            {
                if (stageIdx >= 0 && stageIdx < _vehicleLidPivots.Length) _vehicleLidPivots[stageIdx] = existingPivot;
                return;
            }
            var renderers = veh.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material lidMat = new Material(lit);
            Color c = stageIdx == 0 ? new Color(0.18f, 0.20f, 0.22f)
                    : stageIdx == 1 ? new Color(0.24f, 0.28f, 0.32f)
                    : new Color(0.20f, 0.24f, 0.28f);
            if (lidMat.HasProperty("_BaseColor")) lidMat.SetColor("_BaseColor", c);
            lidMat.color = c;
            if (lidMat.HasProperty("_Smoothness")) lidMat.SetFloat("_Smoothness", 0.22f);

            float lw = Mathf.Max(0.45f, b.size.x * 0.96f);
            float ld = Mathf.Max(0.45f, b.size.z * 0.96f);

            GameObject pivot = new GameObject("_ClosedLidPivot");
            pivot.transform.SetParent(veh.transform, true);
            pivot.transform.position = new Vector3(b.center.x, b.max.y + 0.03f, b.center.z + ld * 0.5f);
            pivot.transform.rotation = veh.transform.rotation;

            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(lid.GetComponent<Collider>());
            lid.name = "_ClosedLidCover";
            lid.transform.SetParent(pivot.transform, false);
            lid.transform.localPosition = new Vector3(0f, 0f, -ld * 0.5f);
            lid.transform.localScale = new Vector3(lw, 0.08f, ld);
            var mr = lid.GetComponent<Renderer>();
            mr.sharedMaterial = lidMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (stageIdx >= 0 && stageIdx < _vehicleLidPivots.Length)
                _vehicleLidPivots[stageIdx] = pivot.transform;
        }

        private void FinishRoomComplete()
        {
            if (CollectingCount > 0) { Money += CollectingPendingValue; CollectingCount = 0; CollectingPendingValue = 0; }
            WasEvicted = false;
            WasEarlyCheckout = false;
            LastClearBonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult);
            Money += LastClearBonus;
            EvaluateStealingAndReview();
            DisplayMoney = Money;
            CampaignProgressStore.MarkStageCleared(CurrentStage, LastStarRating);
            SaveStateToCampaignStore();
            sfx.PlayFanfare();
            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        private void EvaluateStealingAndReview()
        {
            WasCaughtStealing = StoleDesignatedItem || (StolenCount > 0 && Random.value * 100f < SuspicionPercent);
            if (WasCaughtStealing)
            {
                LastConfiscatedValue = Mathf.Min(Money, Mathf.RoundToInt(LastStolenValueTotal * 1.30f) + (StoleDesignatedItem ? 150 : 0));
                Money = Mathf.Max(0, Money - LastConfiscatedValue);
                LastStarRating = 1;
                LastReviewComment = StoleDesignatedItem
                    ? "\"부탁한 분실물을 챙겨가셨네요? CCTV에 다 찍혔습니다! 배상 청구합니다.\""
                    : "\"청소는 잘해주셨는데 집 귀중품이 사라졌네요. 바로 신고 및 배상 청구합니다.\"";
            }
            else if (StolenCount > 0)
            {
                LastConfiscatedValue = 0;
                LastStarRating = FoundClientLostItem ? 4 : 3;
                LastReviewComment = "\"방은 정말 깨끗해졌는데... 여기 있던 내 비상금이 어디 갔지? 기분 탓인가?\"";
            }
            else
            {
                LastConfiscatedValue = 0;
                int bonus = Mathf.RoundToInt(baseCleanReward * StageRewardMult * (FoundClientLostItem ? 0.35f : 0.15f));
                Money += bonus;
                LastClearBonus += bonus;
                LastStarRating = (FoundClientLostItem || OverallCleanPercent >= 95f) ? 5 : 4;
                LastReviewComment = FoundClientLostItem
                    ? "\"분실물까지 선반에 챙겨주시다니! 완전 새 집이 됐어요 정말 감사합니다!\""
                    : "\"깔끔하고 빠르게 치워주셨어요! 믿고 맡길 수 있는 업체네요!\"";
            }
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
            StagePropLayout.ResetBoxTreasures();
            if (_stageCctvs != null) foreach (var c in _stageCctvs) if (c != null) c.ResetCamera();
            if (trashSwarm != null) trashSwarm.SpawnForStage(CurrentStage);
            if (truckTrashVisual != null) truckTrashVisual.ClearStackedVisuals();

            CollectingCount = 0;
            CollectingPendingValue = 0;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            ContinuousFever01 = 0f;
            StenchPercent = 0f;
            SuspicionPercent = 0f;
            StolenCount = 0;
            ReturnedCount = 0;
            FoundClientLostItem = false;
            StoleDesignatedItem = false;
            WasCaughtStealing = false;
            LastStolenValueTotal = 0;
            LastConfiscatedValue = 0;
            LastReviewComment = "";
            _notifiedTrashDone = false;
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
