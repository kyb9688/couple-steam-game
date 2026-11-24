using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        Phase1_TrashRemoval, // 1단계: 1,000+ 쓰레기 1개씩(쿨타임 텀!) 클릭 줍기 + 단단한 큰 봉투 터뜨리기 → 수거함 투하
        Phase2_FloorMopping, // 2단계: 마우스 흔들기 전용 바닥 얼룩 걸레질
        TreasureDecision,    // 희귀 보물 발굴 즉시 이펙트 모달 (보관 vs 슈킹 즉시 결정!)
        MindMapUpgrade,      // [Tab] 반응형 아이콘 마인드맵 강화
        SettingsMenu,        // [ESC] 마우스 감도 + 전체 소리 볼륨 설정창
        RoomCompleted        // 방 100% 완청 정산 & 별점 리뷰
    }

    public enum TreasureRarity { Common, Rare, Legendary }

    [System.Serializable]
    public class DiscoveredTreasure
    {
        public string itemName;
        public int value;
        public TreasureRarity rarity;
        public bool isDesignatedLostItem;
        public int riskLevel;
        public int demeritPercent;
        public int extraTrashPenalty; // 적발 시 집주인이 방에 더 던져놓는 묵은 쓰레기 개수!
        public bool resolved;
        public bool wasStolen;
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
    /// TrashCleaner3D v0.5 중앙 매니저:
    /// - 4단계 수거함 진화: 공공 쓰레기통 → 리어카 → 다마스 → 1톤 청소 트럭 (눈에 보이는 투입구 = 실제 골인 판정 100% 일치!)
    /// - 줍기 속도 텀(0.52초 시작) + [줍기 속도] 업그레이드 추가
    /// - 한 번에 봉투 딱 1개만 손에 쥐기 (여러 개 겹쳐 증발 버그 완전 차단!)
    /// - 악취 단계별 줍기 행위 직접 제약 (25% 느려짐 → 50% 갯수 반감 → 75% 헛손질 → 100% 작업 불가! 무조건 숨 쉬러 나가야 함!)
    /// - 불쾌한 트럭 봉쇄/벌금 삭제 → 유쾌한 슈킹 디메리트 (집주인 묵은 쓰레기 추가 투척 + 완청 보너스 배율 하락)
    /// - [ESC] 마우스 감도·볼륨 설정창 (PlayerPrefs 자동 저장)
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
        public string roomTitle = "Stage 1 · 반지하 자취 원룸 (+50% 확장)";
        public int baseCleanReward = 400;
        public int lostItemTip = 95;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        public int Money { get; private set; } = 0;

        // ===== 사용자 설정 ([ESC] 마우스 감도 + 전체 소리 볼륨) =====
        public float MouseSensitivity { get; private set; } = 0.14f;
        public float MasterVolume { get; private set; } = 0.85f;

        // ===== 악취 누적 & 줍기 행위 직접 제약 (+50% 방 북쪽 문턱 z > 3.02f) =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; }
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public bool IsTooSuffocatedToWork => StenchStage >= 4 && !IsOutsideVentilating;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.42f : StenchStage == 3 ? 0.55f : StenchStage == 2 ? 0.68f : StenchStage == 1 ? 0.80f : 1.0f;
        public float StenchPickCooldownMult => StenchStage == 3 ? 2.4f : StenchStage == 2 ? 1.85f : StenchStage == 1 ? 1.35f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.0f : StenchStage == 3 ? 0.45f : StenchStage == 2 ? 0.65f : 1.0f;
        public float StenchNauseaFailChance => StenchStage == 3 ? 0.35f : 0f; // 75%에서 35% 확률로 구역질 헛손질!
        public float StenchGainPerSec => 3.6f * (1f - LvMask * 0.16f);

        // ===== 한 번에 딱 1봉지 보유 규칙 (가득 차야 왼손에 쥐고 던지기 가능!) =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly int[] PickCountByLv = { 1, 2, 4, 7, 12, 20 };
        private static readonly float[] PickCooldownByLv = { 0.52f, 0.38f, 0.26f, 0.16f, 0.08f };
        private static readonly float[] FistDmgByLv = { 1.0f, 2.0f, 3.4f, 5.0f, 10.0f };
        private static readonly string[] VehicleNames = { "공공 쓰레기통", "리어카", "다마스", "1톤 청소 트럭" };

        public int HandBagCount { get; private set; } = 0;
        public int HandBagPendingValue { get; private set; } = 0;
        public bool HoldingDroppedBag { get; private set; } = false; // 실패 봉투를 손에 쥔 상태!
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (HasVacuumUnlocked ? 3 : 1);
        public bool IsHandBagFull => HoldingDroppedBag || HandBagCount >= HandBagCapacity;
        public bool CanThrowHandBag => IsHandBagFull || (Phase1Cleaned >= Phase1Total && HandBagCount > 0);
        // 손이 비어 있어야만 바닥에 떨어진 봉투 1개를 다시 주울 수 있음! (여러 개 겹쳐 증발 완전 차단!)
        public bool CanPickUpDroppedBag => !HoldingDroppedBag && HandBagCount == 0;

        // ===== 5초 이내 연속 투하 콤보 보너스 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;

        // ===== 트럭 짐칸 적층 봉투 =====
        private int _stackedBagsInTruck = 0;
        private readonly List<GameObject> _stackedVisuals = new List<GameObject>();

        // ===== 3D 월드 돈 팝업 =====
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 아이콘형 마인드맵 14개 노드 =====
        public int LvSweepRadius { get; private set; }      // 0~5 (1클릭 줍기 갯수: 1→2→4→7→12→20개)
        public int LvPickSpeed { get; private set; }        // 0~4 (줍기 속도 텀 단축: 0.52s → 0.08s!)
        public int LvFistPower { get; private set; }        // 0~4 (큰 봉투 10방→5→3→2→1방)
        public bool HasVacuumUnlocked { get; private set; } // 진공청소기 (좌클릭 꾹 연속 흡입)
        public bool HasBeaconDetect { get; private set; }   // 10% 이하 황금 투시 테두리 탐지 (기둥 X!)
        public int LvBagSize { get; private set; }          // 0~4 (봉투 용량 15→180)
        public int LvThrowPower { get; private set; }       // 0~4 (쓰레기 던지는 힘 강화)
        public int LvTruckSize { get; private set; }        // 0~3 (쓰레기통 → 리어카 → 다마스 → 1톤 트럭!)
        public bool HasTruckMagnet { get; private set; }    // 수거함 자석 압축기
        public int LvMask { get; private set; }             // 0~5 (악취 누적 속도 완화)
        public bool HasMopUnlocked { get; private set; }    // 물티슈 → T자 대걸레
        public int LvScrubPower { get; private set; }       // 0~3 (얼룩 닦기 고속화)
        public int LvTreasureLuck { get; private set; }     // 0~3 (보물 가치 업)
        public int LvPokerFace { get; private set; }        // 0~4 (슈킹 들킴 확률 완화)

        // Derived Stats
        public int PickCountPerClick
        {
            get
            {
                int baseCnt = PickCountByLv[Mathf.Clamp(LvSweepRadius, 0, PickCountByLv.Length - 1)];
                // 악취 50%+ 시 1클릭 줍는 개수 절반 감소(최소 1개)!
                if (StenchStage >= 2 && !IsOutsideVentilating && baseCnt > 1) return Mathf.Max(1, baseCnt / 2);
                return baseCnt;
            }
        }
        public float EffectivePickCooldown => PickCooldownByLv[Mathf.Clamp(LvPickSpeed, 0, PickCooldownByLv.Length - 1)] * StenchPickCooldownMult;
        public float SweepRadius => (HasVacuumUnlocked ? 1.85f : 0.48f) + LvSweepRadius * 0.22f;
        public int SweepPerTick => HasVacuumUnlocked ? Mathf.Max(8, PickCountPerClick) : PickCountPerClick;
        public float FistDamage => HasVacuumUnlocked ? Mathf.Max(4.0f, FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)]) : FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)];
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.6f : 1.0f) * (1f + LvScrubPower * 0.45f);
        public float ThrowForwardForce => 5.4f + LvThrowPower * 1.9f;
        public float ThrowUpForce => 2.9f + LvThrowPower * 0.48f;
        public string CurrentVehicleName => VehicleNames[Mathf.Clamp(LvTruckSize, 0, VehicleNames.Length - 1)];

        // Progress Tracking
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

        // ===== 희귀 보물(방당 최대 3개) + 유쾌한 슈킹 디메리트 (트럭 봉쇄·벌금 X!) =====
        public DiscoveredTreasure ActiveTreasure { get; private set; }
        public float TreasureDecisionLockTimer { get; private set; }
        public float TreasureFlash01 { get; private set; }
        public float StealAlertFlash01 { get; private set; }

        private int _treasuresSpawnedThisRoom;
        private int _pilesBrokenCount;
        private bool _designatedFound;

        private float _internalSuspicion;
        public int StolenCount { get; private set; }
        public int StolenValue { get; private set; }
        public bool StoleDesignatedItem { get; private set; }
        public float CleanBonusMultiplier { get; private set; } = 1.0f; // 들킬 때마다 완청 보너스 배율 하락 (100%→80%→60%→40%)
        public float LandlordNagTimer { get; private set; }
        public string LandlordMessage { get; private set; } = "";

        public int SuspicionStage => StoleDesignatedItem || _internalSuspicion >= 60f ? 3
            : _internalSuspicion >= 32f ? 2 : _internalSuspicion >= 12f ? 1 : 0;

        // Settlement & Toast
        public bool LastWasCaught { get; private set; }
        public int LastStarRating { get; private set; } = 5;
        public string LastReviewText { get; private set; } = "";
        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;
        private float _pitchCombo;
        private float _pitchResetTimer;

        private static readonly (string name, int val, TreasureRarity rarity)[] TreasurePool =
        {
            ("구겨진 5만 원권 뭉치", 120, TreasureRarity.Common),
            ("중고 휴대용 게임기", 190, TreasureRarity.Rare),
            ("빈티지 금시계", 260, TreasureRarity.Rare),
            ("24K 미니 골드바", 420, TreasureRarity.Legendary)
        };

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

        /// <summary>
        /// 눈에 보이는 수거함 투입구(쓰레기통→리어카→다마스→1톤 트럭) 내부 빈 공간 크기와 100% 일치하는 판정 반환!
        /// </summary>
        public Vector3 GetGoalOpeningHalfExtents()
        {
            switch (Mathf.Clamp(LvTruckSize, 0, 3))
            {
                case 0: return new Vector3(0.42f, 0.58f, 0.42f);  // 공공 쓰레기통 (0.76 x 0.76)
                case 1: return new Vector3(0.70f, 0.58f, 0.64f);  // 리어카 (1.36 x 1.24)
                case 2: return new Vector3(0.98f, 0.60f, 0.80f);  // 다마스 (1.92 x 1.56)
                default: return new Vector3(1.32f, 0.65f, 1.10f); // 1톤 청소 트럭 (2.60 x 2.15)
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

            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastTimer <= 0f) ToastMessage = "";
            }
            if (_pitchResetTimer > 0f)
            {
                _pitchResetTimer -= dt;
                if (_pitchResetTimer <= 0f) _pitchCombo = 0f;
            }
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

            if (ThrowComboTimer > 0f)
            {
                ThrowComboTimer -= dt;
                if (ThrowComboTimer <= 0f) ThrowComboCount = 0;
            }
            if (LandlordNagTimer > 0f)
            {
                LandlordNagTimer -= dt;
                if (LandlordNagTimer <= 0f) LandlordMessage = "";
            }

            // 확장된 방(z = -3.0 ~ +3.0) 기준, 문 밖(z > 3.02f)에서 신선한 공기 환기!
            if (player != null)
            {
                bool wasOutside = IsOutsideVentilating;
                IsOutsideVentilating = player.transform.position.z > 3.02f;
                if (IsOutsideVentilating)
                {
                    if (!wasOutside && StenchPercent >= 20f)
                    {
                        FreshAirFlash01 = 1f;
                        sfx.PlayChime();
                    }
                    if (StenchPercent > 0.1f) StenchPercent = Mathf.Max(0f, StenchPercent - 45f * dt);
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

            // 10% 이하 탐지: 초록/파란 기둥 완전 삭제 → 깔끔한 투시 외곽 테두리 펄스만 표시!
            bool p1Detect = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval && Phase1Percent >= 90f;
            foreach (var p in _bigPiles) if (p != null) p.SetDetectOutlineVisible(p1Detect && !p.IsCleaned);
            bool p2Detect = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping && (Phase2Total - Phase2Cleaned) <= 2;
            foreach (var s in _phase2Stains) if (s != null) s.SetDetectOutlineVisible(p2Detect && !s.IsCleaned);
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.6f)
        {
            FloatingTexts.Add(new FloatingWorldText { worldPos = worldPos, text = text, color = color, age = 0f, duration = duration });
        }

        public void OnInstancedTrashCollected(int itemValue)
        {
            HandBagCount = Mathf.Min(HandBagCapacity, HandBagCount + 1);
            HandBagPendingValue += itemValue;

            _pitchResetTimer = 0.35f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.04f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.38f);

            UpdateAtmosphere();
            CheckPhase1Complete();
        }

        public void OnBigPileBurst(CleanableTarget pile)
        {
            sfx.PlayPop(0.62f);
            _pilesBrokenCount++;

            if (trashSwarm != null)
            {
                trashSwarm.SpawnBurstFountain(pile.transform.position, 20);
            }

            // 방당 최대 3개 희귀 보물!
            if (_treasuresSpawnedThisRoom < 3 && (_pilesBrokenCount == 4 || _pilesBrokenCount == 9 || _pilesBrokenCount == 14))
            {
                TriggerRareTreasureDecision();
            }

            UpdateAtmosphere();
            CheckPhase1Complete();
        }

        private void TriggerRareTreasureDecision()
        {
            _treasuresSpawnedThisRoom++;
            DiscoveredTreasure item;
            if (!_designatedFound && _treasuresSpawnedThisRoom == 2)
            {
                _designatedFound = true;
                item = new DiscoveredTreasure
                {
                    itemName = "엄마가 주신 금반지 (의뢰인 지정 분실물!)",
                    value = 340,
                    rarity = TreasureRarity.Legendary,
                    isDesignatedLostItem = true,
                    riskLevel = 3,
                    demeritPercent = 100,
                    extraTrashPenalty = 65
                };
            }
            else
            {
                int maxIdx = Mathf.Min(TreasurePool.Length, 2 + LvTreasureLuck);
                var pick = TreasurePool[Random.Range(0, maxIdx)];
                int risk = pick.rarity == TreasureRarity.Legendary ? 3 : pick.rarity == TreasureRarity.Rare ? 2 : 1;
                int chance = Mathf.Clamp(35 + risk * 15 - LvPokerFace * 10, 15, 85);
                item = new DiscoveredTreasure
                {
                    itemName = pick.name,
                    value = Mathf.RoundToInt(pick.val * (1f + LvTreasureLuck * 0.22f)),
                    rarity = pick.rarity,
                    isDesignatedLostItem = false,
                    riskLevel = risk,
                    demeritPercent = chance,
                    extraTrashPenalty = 25 + risk * 15
                };
            }

            ActiveTreasure = item;
            TreasureFlash01 = 1.0f;
            TreasureDecisionLockTimer = 0.55f;
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
                int tip = item.isDesignatedLostItem ? lostItemTip : Mathf.Max(35, item.value / 4);
                Money += tip;
                sfx.PlayChime();
                ShowToast($"보관 완료 · +${tip}");
                return;
            }

            StolenCount++;
            StolenValue += item.value;
            Money += item.value;
            sfx.PlaySteal();

            if (item.isDesignatedLostItem)
            {
                StoleDesignatedItem = true;
                _internalSuspicion = 100f;
                ApplyFunStealDemerit(item, true);
                return;
            }

            _internalSuspicion = Mathf.Clamp(_internalSuspicion + Mathf.Max(10f, item.riskLevel * 16f - LvPokerFace * 5f), 0f, 95f);

            if (Random.Range(0, 100) < item.demeritPercent)
            {
                ApplyFunStealDemerit(item, false);
            }
            else
            {
                ShowToast($"슈킹 성공 · +${item.value}");
            }
        }

        private void ApplyFunStealDemerit(DiscoveredTreasure item, bool severe)
        {
            StealAlertFlash01 = 0.85f;
            sfx.PlayAlert();

            CleanBonusMultiplier = Mathf.Max(0.30f, CleanBonusMultiplier - (severe ? 0.35f : 0.20f));
            int extraCount = item.extraTrashPenalty;
            if (trashSwarm != null)
            {
                trashSwarm.SpawnBurstFountain(new Vector3(0.4f, 0.3f, 0.2f), extraCount);
            }

            LandlordNagTimer = 6.0f;
            LandlordMessage = $"적발! 쓰레기 +{extraCount} · 보너스 {CleanBonusMultiplier * 100f:0}%";
            ShowToast($"적발! 쓰레기 +{extraCount} · 보너스 {CleanBonusMultiplier * 100f:0}%");
        }

        public void ThrowHandBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (HandBagCount <= 0) return;

            if (!CanThrowHandBag)
            {
                ShowToast($"봉투 미달 ({HandBagCount}/{HandBagCapacity})");
                return;
            }

            int count = HandBagCount;
            int pendingVal = HandBagPendingValue;
            HandBagCount = 0;
            HandBagPendingValue = 0;
            HoldingDroppedBag = false;
            sfx.PlayPop(0.65f);

            GameObject bagObj = thrownBagPrefab != null
                ? Instantiate(thrownBagPrefab, spawnPos, Quaternion.identity)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bagObj.SetActive(true);
            bagObj.transform.position = spawnPos;

            var rb = bagObj.GetComponent<Rigidbody>();
            if (rb == null) rb = bagObj.AddComponent<Rigidbody>();
            rb.mass = 1.25f;
            rb.linearDamping = 0.15f;

            var proj = bagObj.AddComponent<ThrownTrashBag>();
            Vector3 launchVel = forwardDir.normalized * ThrowForwardForce + Vector3.up * ThrowUpForce;
            proj.Launch(launchVel, spawnPos, truckBedGoal, this, count, pendingVal, HasTruckMagnet);
        }

        /// <summary>
        /// 요청 #7: 한 번에 딱 1봉지만 손에 쥐기! (손이 비어 있을 때만 떨어진 봉투 회수 가능)
        /// </summary>
        public void OnDroppedBagRetrieved(int count, int pendingVal)
        {
            HandBagCount = count;
            HandBagPendingValue = pendingVal;
            HoldingDroppedBag = true;
            sfx.PlayPop(1.05f);
            ShowToast($"봉투 회수 · ${HandBagPendingValue}");
        }

        public void OnBagScored(int count, int bagValue, float distance, Vector3 scoreWorldPos)
        {
            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(5, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = 5.0f;

            int baseVal = Mathf.Max(bagValue, count * 3);
            float comboMult = 1f + (ThrowComboCount - 1) * 0.25f;
            int distBonus = distance >= 6.8f ? 35 : distance >= 4.5f ? 15 : 0;
            int vehBonus = LvTruckSize * 15;
            int totalPay = Mathf.RoundToInt(baseVal * comboMult) + distBonus + vehBonus;

            Money += totalPay;
            sfx.PlayChime();

            SpawnStackedBagVisualInTruck();

            string popup = ThrowComboCount >= 2
                ? $"{ThrowComboCount}연속 콤보! +${totalPay}"
                : distBonus >= 35
                    ? $"롱슛! +${totalPay}"
                    : $"+${totalPay}";
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
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                ShowToast("2단계 · 바닥 닦기");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            Money += stain.rewardMoney;
            sfx.PlayChime();
            SpawnFloatingText(stain.transform.position + Vector3.up * 0.35f, $"+${stain.rewardMoney}", new Color(0.50f, 0.92f, 1f), 1.4f);
            UpdateAtmosphere();
            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        public void ToggleMindMap()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.MindMapUpgrade)
            {
                Phase = _phaseBeforeOverlay;
                UnlockCursor(false);
            }
            else
            {
                if (Phase != GamePhase.SettingsMenu) _phaseBeforeOverlay = Phase;
                Phase = GamePhase.MindMapUpgrade;
                UnlockCursor(true);
            }
        }

        public void ToggleSettingsMenu()
        {
            if (Phase == GamePhase.RoomCompleted || Phase == GamePhase.TreasureDecision) return;
            if (Phase == GamePhase.SettingsMenu)
            {
                Phase = _phaseBeforeOverlay;
                UnlockCursor(false);
            }
            else if (Phase == GamePhase.MindMapUpgrade)
            {
                Phase = _phaseBeforeOverlay;
                UnlockCursor(false);
            }
            else
            {
                _phaseBeforeOverlay = Phase;
                Phase = GamePhase.SettingsMenu;
                UnlockCursor(true);
            }
        }

        // ===== 마인드맵 14개 노드 가격 & 구매 =====
        public int CostSweep => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvSweepRadius));
        public int CostPickSpeed => Mathf.RoundToInt(20 * Mathf.Pow(1.8f, LvPickSpeed));
        public int CostFist => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostVacuum => 280;
        public int CostBeacon => 60;
        public int CostBag => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvBagSize));
        public int CostThrowPower => Mathf.RoundToInt(30 * Mathf.Pow(1.8f, LvThrowPower));
        public int CostTruck => LvTruckSize == 0 ? 55 : LvTruckSize == 1 ? 140 : 280; // 쓰레기통→리어카→다마스→1톤 트럭!
        public int CostMagnet => 160;
        public int CostMask => Mathf.RoundToInt(30 * Mathf.Pow(1.75f, LvMask));
        public int CostMop => 75;
        public int CostScrub => Mathf.RoundToInt(95 * Mathf.Pow(1.85f, LvScrubPower));
        public int CostLuck => Mathf.RoundToInt(60 * Mathf.Pow(1.85f, LvTreasureLuck));
        public int CostPoker => Mathf.RoundToInt(70 * Mathf.Pow(1.85f, LvPokerFace));

        public int CountAffordableUpgrades()
        {
            int c = 0;
            if (LvSweepRadius < 5 && Money >= CostSweep) c++;
            if (LvPickSpeed < 4 && Money >= CostPickSpeed) c++;
            if (LvFistPower < 4 && Money >= CostFist) c++;
            if (LvSweepRadius >= 2 && !HasVacuumUnlocked && Money >= CostVacuum) c++;
            if (LvFistPower >= 1 && !HasBeaconDetect && Money >= CostBeacon) c++;
            if (LvBagSize < 4 && Money >= CostBag) c++;
            if (LvThrowPower < 4 && Money >= CostThrowPower) c++;
            if (LvTruckSize < 3 && Money >= CostTruck) c++;
            if (LvTruckSize >= 1 && !HasTruckMagnet && Money >= CostMagnet) c++;
            if (LvMask < 5 && Money >= CostMask) c++;
            if (LvMask >= 1 && !HasMopUnlocked && Money >= CostMop) c++;
            if (HasMopUnlocked && LvScrubPower < 3 && Money >= CostScrub) c++;
            if (LvTreasureLuck < 3 && Money >= CostLuck) c++;
            if (LvTreasureLuck >= 1 && LvPokerFace < 4 && Money >= CostPoker) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            switch (id)
            {
                case "sweep":
                    if (LvSweepRadius >= 5 || Money < CostSweep) return false;
                    Money -= CostSweep; LvSweepRadius++; break;
                case "speed":
                    if (LvPickSpeed >= 4 || Money < CostPickSpeed) return false;
                    Money -= CostPickSpeed; LvPickSpeed++; break;
                case "fist":
                    if (LvFistPower >= 4 || Money < CostFist) return false;
                    Money -= CostFist; LvFistPower++; break;
                case "vacuum":
                    if (LvSweepRadius < 2 || HasVacuumUnlocked || Money < CostVacuum) return false;
                    Money -= CostVacuum; HasVacuumUnlocked = true; break;
                case "beacon":
                    if (LvFistPower < 1 || HasBeaconDetect || Money < CostBeacon) return false;
                    Money -= CostBeacon; HasBeaconDetect = true; break;
                case "bag":
                    if (LvBagSize >= 4 || Money < CostBag) return false;
                    Money -= CostBag; LvBagSize++; break;
                case "throw":
                    if (LvThrowPower >= 4 || Money < CostThrowPower) return false;
                    Money -= CostThrowPower; LvThrowPower++; break;
                case "truck":
                    if (LvTruckSize >= 3 || Money < CostTruck) return false;
                    Money -= CostTruck; LvTruckSize++; UpdateVehicleStageVisuals();
                    ShowToast($"수거함 → {CurrentVehicleName}");
                    break;
                case "magnet":
                    if (LvTruckSize < 1 || HasTruckMagnet || Money < CostMagnet) return false;
                    Money -= CostMagnet; HasTruckMagnet = true; break;
                case "mask":
                    if (LvMask >= 5 || Money < CostMask) return false;
                    Money -= CostMask; LvMask++; break;
                case "mop":
                    if (LvMask < 1 || HasMopUnlocked || Money < CostMop) return false;
                    Money -= CostMop; HasMopUnlocked = true; break;
                case "scrub":
                    if (!HasMopUnlocked || LvScrubPower >= 3 || Money < CostScrub) return false;
                    Money -= CostScrub; LvScrubPower++; break;
                case "luck":
                    if (LvTreasureLuck >= 3 || Money < CostLuck) return false;
                    Money -= CostLuck; LvTreasureLuck++; break;
                case "poker":
                    if (LvTreasureLuck < 1 || LvPokerFace >= 4 || Money < CostPoker) return false;
                    Money -= CostPoker; LvPokerFace++; break;
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
            float effectiveChance = StoleDesignatedItem ? 100f : Mathf.Clamp(_internalSuspicion, 0f, 95f);
            int finalBonus = Mathf.RoundToInt(baseCleanReward * CleanBonusMultiplier);

            LastWasCaught = StolenCount > 0 && Random.Range(0f, 100f) < effectiveChance;
            if (LastWasCaught)
            {
                Money += Mathf.RoundToInt(finalBonus * 0.6f);
                LastStarRating = 2;
                LastReviewText = StoleDesignatedItem
                    ? "\"방은 새 집 됐는데 엄마 금반지가 없어졌어요! 보너스는 못 드리겠네요 ★★☆☆☆\""
                    : "\"청소는 엄청 깔끔한데 집 물건이 자꾸 사라진 기분... ★★☆☆☆\"";
                sfx.PlayAlert();
            }
            else if (StolenCount == 0)
            {
                Money += finalBonus;
                LastStarRating = 5;
                LastReviewText = "\"넓은 방 쓰레기 1,000개가 싹 사라졌어요!! 물건도 다 지켜주시고 최고예요 ★★★★★\"";
                sfx.PlayFanfare();
            }
            else
            {
                Money += finalBonus;
                LastStarRating = 4;
                LastReviewText = "\"완전 새 집 됐네요! 근데 원래 여기 있던 내 비상금이 어디 갔지...? ★★★★☆\"";
                sfx.PlayFanfare();
            }

            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ReplayRoomKeepUpgrades()
        {
            foreach (var p in _bigPiles) if (p != null) p.ResetForNewRun();
            foreach (var s in _phase2Stains) if (s != null) s.ResetForNewRun();
            if (trashSwarm != null) trashSwarm.ResetSwarm(1000);
            foreach (var v in _stackedVisuals) if (v != null) Destroy(v);
            _stackedVisuals.Clear();
            _stackedBagsInTruck = 0;

            foreach (var dropped in FindObjectsByType<ThrownTrashBag>(FindObjectsSortMode.None))
            {
                Destroy(dropped.gameObject);
            }

            ActiveTreasure = null;
            HandBagCount = 0;
            HandBagPendingValue = 0;
            HoldingDroppedBag = false;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            StenchPercent = 0f;
            StolenCount = 0;
            StolenValue = 0;
            StoleDesignatedItem = false;
            CleanBonusMultiplier = 1.0f;
            _internalSuspicion = 0f;
            LandlordNagTimer = 0f;
            LandlordMessage = "";
            _treasuresSpawnedThisRoom = 0;
            _pilesBrokenCount = 0;
            _designatedFound = false;
            Phase = GamePhase.Phase1_TrashRemoval;

            UpdateAtmosphere();
            UpdateVehicleStageVisuals();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            UnlockCursor(false);
            ShowToast("단골 원룸 재발 의뢰! 강화된 장비로 다시 도전하세요!");
        }

        private void UpdateAtmosphere()
        {
            float t = Mathf.Clamp01(OverallCleanPercent / 100f);
            RenderSettings.fogDensity = Mathf.Lerp(0.032f, 0.003f, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.18f, 0.16f, 0.12f), new Color(0.80f, 0.83f, 0.85f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.28f, 0.26f, 0.22f), new Color(0.62f, 0.63f, 0.64f), t);
            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 0.99f, 0.96f), t);
                ceilingLight.intensity = Mathf.Lerp(15f, 22f, t);
            }
        }

        public void ShowToast(string msg)
        {
            ToastMessage = msg;
            _toastTimer = 3.5f;
        }

        private static void UnlockCursor(bool unlock)
        {
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
