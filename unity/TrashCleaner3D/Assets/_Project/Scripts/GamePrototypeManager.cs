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
        public bool isDesignatedLostItem;
        public int riskLevel;
        public int demeritPercent;
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
    /// TrashCleaner3D v0.6 중앙 매니저:
    /// - 바닥 쓰레기 수집 카운터(CollectingCount)와 손에 쥔 완성 봉지(CarriedBag, [F]로 줍기) 완전 분리!
    /// - 답답한 클릭 쿨타임 제거 → 눌어붙은 쓰레기 떼어내기(3→2→1클릭, 손악력 업그레이드)!
    /// - 티어별 순차 해금 스킬트리 (Tier 1 → Tier 2 → Tier 3 → Tier 4)
    /// - 진짜 경제 슈킹 디메리트 (적발 시 쓰레기 투하 단가 -20% & 상점 강화비 +15%!)
    /// - 2,250+ 쓰레기 물량 + 4단계 수거함 진화 + [ESC] 감도/볼륨 설정
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
        public string roomTitle = "Stage 1 · 반지하 자취 원룸";
        public int baseCleanReward = 450;
        public int lostItemTip = 95;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        public int Money { get; private set; } = 0;

        // ===== 사용자 설정 ([ESC]) =====
        public float MouseSensitivity { get; private set; } = 0.14f;
        public float MasterVolume { get; private set; } = 0.85f;

        // ===== 악취 누적 & 줍기 제약 =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; }
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public bool IsTooSuffocatedToWork => StenchStage >= 4 && !IsOutsideVentilating;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.42f : StenchStage == 3 ? 0.55f : StenchStage == 2 ? 0.68f : StenchStage == 1 ? 0.80f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.0f : StenchStage == 3 ? 0.48f : StenchStage == 2 ? 0.70f : 1.0f;
        public float StenchNauseaFailChance => StenchStage == 3 ? 0.35f : 0f;
        public float StenchGainPerSec => 3.6f * (1f - LvMask * 0.16f);

        // ===== 시스템 A: 바닥 쓰레기 수집 카운터 (Collecting) =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly int[] PickCountByLv = { 1, 2, 4, 7, 12, 20 };
        private static readonly float[] FistDmgByLv = { 1.0f, 2.0f, 3.4f, 5.0f, 10.0f };
        private static readonly string[] VehicleNames = { "공공 쓰레기통", "리어카", "다마스", "1톤 청소 트럭" };

        public int CollectingCount { get; private set; } = 0;
        public int CollectingPendingValue { get; private set; } = 0;
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (HasVacuumUnlocked ? 3 : 1);

        // ===== 시스템 B: 손에 쥔 완성 쓰레기봉지 1개 (CarriedBag — [F]로 줍기, [RMB]로 투하!) =====
        public bool HasCarriedBag { get; private set; } = false;
        public int CarriedBagCount { get; private set; } = 0;
        public int CarriedBagValue { get; private set; } = 0;

        // 수집 카운터(1/15 등)와 완전히 독립! 손에 완성 봉지를 안 들고 있으면 언제든 [F]로 떨어진 봉지 1개 들기 가능!
        public bool CanPickUpDroppedBag => !HasCarriedBag;
        public bool CanThrowCarriedBag => HasCarriedBag;

        // ===== 5초 연속 투하 콤보 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;

        // ===== 트럭 적층 봉투 =====
        private int _stackedBagsInTruck = 0;
        private readonly List<GameObject> _stackedVisuals = new List<GameObject>();

        // ===== 3D 월드 돈 팝업 =====
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 순차 해금 14노드 스킬트리 =====
        // Tier 1 (기본 해금)
        public int LvSweepRadius { get; private set; }      // 0~5 (1클릭 줍기 갯수: 1→2→4→7→12→20개)
        public int LvFistPower { get; private set; }        // 0~4 (큰 봉투 10방→5→3→2→1방)
        public int LvBagSize { get; private set; }          // 0~4 (봉투 용량 15→180)
        public int LvMask { get; private set; }             // 0~5 (악취 완화)
        // Tier 2 (Tier 1 Lv.1 선행)
        public int LvPeelPower { get; private set; }        // 0~2 (떼어내기 힘: 3클릭→2클릭→1클릭 컷!) ← sweep Lv.1
        public int LvThrowPower { get; private set; }       // 0~4 (던지는 힘) ← bag Lv.1
        public bool HasBeaconDetect { get; private set; }   // 10% 투시 테두리 ← fist Lv.1
        public int LvTreasureLuck { get; private set; }     // 0~3 (보물 눈) ← fist Lv.1
        public bool HasMopUnlocked { get; private set; }    // T자 대걸레 ← mask Lv.1
        // Tier 3 (Tier 2 선행)
        public int LvTruckSize { get; private set; }        // 0~3 (쓰레기통→리어카→다마스→트럭) ← throw Lv.1
        public int LvScrubPower { get; private set; }       // 0~3 (강력 세제) ← mop
        public int LvPokerFace { get; private set; }        // 0~4 (연기력) ← luck Lv.1
        // Tier 4 (엔드게임)
        public bool HasVacuumUnlocked { get; private set; } // 진공청소기 ← peel Lv.2 & sweep Lv.2
        public bool HasTruckMagnet { get; private set; }    // 자석 압축기 ← truck Lv.2 (다마스)

        // Derived Stats
        public int PickCountPerClick
        {
            get
            {
                int baseCnt = PickCountByLv[Mathf.Clamp(LvSweepRadius, 0, PickCountByLv.Length - 1)];
                if (StenchStage >= 2 && !IsOutsideVentilating && baseCnt > 1) return Mathf.Max(1, baseCnt / 2);
                return baseCnt;
            }
        }
        // 요청 #5: 3클릭 → 2클릭 → 1클릭 (악취 25%+ 시 떼어내기 힘 1 감소해 자연스럽게 느려짐!)
        public int EffectivePeelDamage
        {
            get
            {
                if (HasVacuumUnlocked) return 3;
                int baseDmg = 1 + LvPeelPower; // 1(3클릭), 2(2클릭), 3(1클릭)
                if (StenchStage >= 1 && !IsOutsideVentilating && baseDmg > 1) return baseDmg - 1;
                return baseDmg;
            }
        }
        public int ClicksNeededToPick => EffectivePeelDamage >= 3 ? 1 : EffectivePeelDamage == 2 ? 2 : 3;
        public float SweepRadius => (HasVacuumUnlocked ? 1.85f : 0.48f) + LvSweepRadius * 0.22f;
        public int SweepPerTick => HasVacuumUnlocked ? Mathf.Max(8, PickCountPerClick) : PickCountPerClick;
        public float FistDamage => HasVacuumUnlocked ? Mathf.Max(4.0f, FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)]) : FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)];
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.6f : 1.0f) * (1f + LvScrubPower * 0.45f);
        public float ThrowForwardForce => 5.6f + LvThrowPower * 1.95f;
        public float ThrowUpForce => 3.0f + LvThrowPower * 0.48f;
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

        // ===== 희귀 보물(방당 최대 3개) + 진짜 경제 슈킹 디메리트 (투하 단가↓ & 상점 물가↑!) =====
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

        // 요청 #4: 진짜 경제적 디메리트 (돈 더 주는 역효과 0%!)
        public float TrashPayMultiplier { get; private set; } = 1.0f;   // 적발 시 -20% (최저 40%)
        public float ShopPriceMultiplier { get; private set; } = 1.0f;  // 적발 시 +15% (최대 200%)
        public float CleanBonusMultiplier { get; private set; } = 1.0f; // 적발 시 -25% (최저 30%)
        public float LandlordNagTimer { get; private set; }
        public string LandlordMessage { get; private set; } = "";

        public int SuspicionStage => StoleDesignatedItem || _internalSuspicion >= 60f ? 3
            : _internalSuspicion >= 32f ? 2 : _internalSuspicion >= 12f ? 1 : 0;

        // Settlement & Toast
        public bool LastWasCaught { get; private set; }
        public int LastStarRating { get; private set; } = 5;
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

            bool p1Detect = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval && Phase1Percent >= 90f;
            foreach (var p in _bigPiles) if (p != null) p.SetDetectOutlineVisible(p1Detect && !p.IsCleaned);
            bool p2Detect = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping && (Phase2Total - Phase2Cleaned) <= 2;
            foreach (var s in _phase2Stains) if (s != null) s.SetDetectOutlineVisible(p2Detect && !s.IsCleaned);
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.6f)
        {
            FloatingTexts.Add(new FloatingWorldText { worldPos = worldPos, text = text, color = color, age = 0f, duration = duration });
        }

        /// <summary>
        /// 바닥 쓰레기를 주워 CollectingCount를 채우고, 가득 차면 완성된 봉지(CarriedBag)로 포장!
        /// 이미 손에 다른 봉지를 들고 있으면 발 앞에 완성 봉지를 툭 떨어뜨림!
        /// </summary>
        public void OnInstancedTrashCollected(int itemValue)
        {
            CollectingCount++;
            CollectingPendingValue += itemValue;

            _pitchResetTimer = 0.35f;
            _pitchCombo = Mathf.Min(1.65f, _pitchCombo + 0.04f);
            sfx.PlayPop(0.88f + _pitchCombo * 0.38f);

            if (CollectingCount >= HandBagCapacity)
            {
                PackageCollectingIntoBag();
            }

            UpdateAtmosphere();
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
                ShowToast($"봉투 완성 · ${val}");
            }
            else if (player != null)
            {
                // 이미 손에 완성 봉지 1개를 들고 있다면 발 앞에 새 완성 봉지 생성 (나중에 [F]로 주워 5초 콤보 투하 가능!)
                Vector3 dropPos = player.transform.position + player.transform.forward * 0.85f + Vector3.up * 0.45f;
                SpawnDroppedBagAt(dropPos, player.transform.forward * 1.4f + Vector3.up * 1.2f, cnt, val);
                sfx.PlayChime();
                ShowToast($"완성 봉투 바닥 배치 · [F] 회수");
            }
        }

        public void OnBigPileBurst(CleanableTarget pile)
        {
            sfx.PlayPop(0.62f);
            _pilesBrokenCount++;

            if (trashSwarm != null)
            {
                trashSwarm.SpawnBurstFountain(pile.transform.position, 22);
            }

            if (_treasuresSpawnedThisRoom < 3 && (_pilesBrokenCount == 5 || _pilesBrokenCount == 11 || _pilesBrokenCount == 17))
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
                    itemName = "엄마가 주신 금반지 (지정 분실물)",
                    value = 340,
                    rarity = TreasureRarity.Legendary,
                    isDesignatedLostItem = true,
                    riskLevel = 3,
                    demeritPercent = 100
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
                    demeritPercent = chance
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
                ShowToast($"보관 · +${tip}");
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
                ApplyEconomicStealDemerit(true);
                return;
            }

            _internalSuspicion = Mathf.Clamp(_internalSuspicion + Mathf.Max(10f, item.riskLevel * 16f - LvPokerFace * 5f), 0f, 95f);

            if (Random.Range(0, 100) < item.demeritPercent)
            {
                ApplyEconomicStealDemerit(false);
            }
            else
            {
                ShowToast($"슈킹 성공 · +${item.value}");
            }
        }

        /// <summary>
        /// 요청 #4: 진짜 경제 디메리트! (쓰레기 더 주는 메리트 역효과 제거!)
        /// 적발 시 악평으로 인해 남은 방 동안: 투하 수익 -20% · 상점 강화비 +15% · 완청 보너스 -25%!
        /// </summary>
        private void ApplyEconomicStealDemerit(bool severe)
        {
            StealAlertFlash01 = 0.85f;
            sfx.PlayAlert();

            TrashPayMultiplier = Mathf.Max(0.40f, TrashPayMultiplier - (severe ? 0.35f : 0.20f));
            ShopPriceMultiplier = Mathf.Min(2.00f, ShopPriceMultiplier + (severe ? 0.25f : 0.15f));
            CleanBonusMultiplier = Mathf.Max(0.25f, CleanBonusMultiplier - (severe ? 0.40f : 0.25f));

            LandlordNagTimer = 6.0f;
            LandlordMessage = $"악평 적발 · 수익 {TrashPayMultiplier * 100f:0}% · 강화비 {ShopPriceMultiplier * 100f:0}%";
            ShowToast(LandlordMessage);
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

        /// <summary>
        /// 요청 #2: [F] 키로 바닥의 완성 봉지를 손(CarriedBag)에 들기 (CollectingCount와 완전 독립!)
        /// </summary>
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
            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(5, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = 5.0f;

            int rawBase = Mathf.Max(bagValue, count * 3);
            float comboMult = 1f + (ThrowComboCount - 1) * 0.25f;
            int distBonus = distance >= 6.8f ? 35 : distance >= 4.5f ? 15 : 0;
            int vehBonus = LvTruckSize * 15;
            int totalPay = Mathf.Max(1, Mathf.RoundToInt((rawBase * comboMult + distBonus + vehBonus) * TrashPayMultiplier));

            Money += totalPay;
            sfx.PlayChime();

            SpawnStackedBagVisualInTruck();

            string popup = ThrowComboCount >= 2
                ? $"{ThrowComboCount}연속! +${totalPay}"
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
                // 마지막 덜 찬 수집분이 있으면 자동으로 봉투로 묶어줌!
                if (CollectingCount > 0) PackageCollectingIntoBag();
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                ShowToast("2단계 · 바닥 닦기");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            int pay = Mathf.Max(1, Mathf.RoundToInt(stain.rewardMoney * TrashPayMultiplier));
            Money += pay;
            sfx.PlayChime();
            SpawnFloatingText(stain.transform.position + Vector3.up * 0.35f, $"+${pay}", new Color(0.50f, 0.92f, 1f), 1.4f);
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
            if (Phase == GamePhase.SettingsMenu || Phase == GamePhase.MindMapUpgrade)
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

        // ===== 순차 해금 조건 & 상점 물가(ShopPriceMultiplier) 반영 가격 =====
        private int P(float raw) => Mathf.RoundToInt(raw * ShopPriceMultiplier);

        public int CostSweep => P(25 * Mathf.Pow(1.85f, LvSweepRadius));
        public int CostPeel => P(35 * Mathf.Pow(2.2f, LvPeelPower));
        public int CostFist => P(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostVacuum => P(280);
        public int CostBeacon => P(60);
        public int CostBag => P(25 * Mathf.Pow(1.85f, LvBagSize));
        public int CostThrowPower => P(35 * Mathf.Pow(1.8f, LvThrowPower));
        public int CostTruck => P(LvTruckSize == 0 ? 60 : LvTruckSize == 1 ? 150 : 300);
        public int CostMagnet => P(180);
        public int CostMask => P(30 * Mathf.Pow(1.75f, LvMask));
        public int CostMop => P(75);
        public int CostScrub => P(95 * Mathf.Pow(1.85f, LvScrubPower));
        public int CostLuck => P(60 * Mathf.Pow(1.85f, LvTreasureLuck));
        public int CostPoker => P(70 * Mathf.Pow(1.85f, LvPokerFace));

        // 요청 #3: 엄격한 티어별 순차 해금 조건!
        public bool IsNodeUnlocked(string id)
        {
            switch (id)
            {
                // Tier 1 (초반 4대 기본)
                case "sweep":
                case "fist":
                case "bag":
                case "mask":
                    return true;
                // Tier 2 (Tier 1 Lv.1 선행 필수!)
                case "peel": return LvSweepRadius >= 1;
                case "throw": return LvBagSize >= 1;
                case "beacon": return LvFistPower >= 1;
                case "luck": return LvFistPower >= 1;
                case "mop": return LvMask >= 1;
                // Tier 3 (Tier 2 선행 필수!)
                case "truck": return LvThrowPower >= 1;
                case "scrub": return HasMopUnlocked;
                case "poker": return LvTreasureLuck >= 1;
                // Tier 4 (엔드게임 최종 선행!)
                case "vacuum": return LvPeelPower >= 2 && LvSweepRadius >= 2;
                case "magnet": return LvTruckSize >= 2;
                default: return false;
            }
        }

        public int CountAffordableUpgrades()
        {
            int c = 0;
            if (IsNodeUnlocked("sweep") && LvSweepRadius < 5 && Money >= CostSweep) c++;
            if (IsNodeUnlocked("peel") && LvPeelPower < 2 && Money >= CostPeel) c++;
            if (IsNodeUnlocked("fist") && LvFistPower < 4 && Money >= CostFist) c++;
            if (IsNodeUnlocked("vacuum") && !HasVacuumUnlocked && Money >= CostVacuum) c++;
            if (IsNodeUnlocked("beacon") && !HasBeaconDetect && Money >= CostBeacon) c++;
            if (IsNodeUnlocked("bag") && LvBagSize < 4 && Money >= CostBag) c++;
            if (IsNodeUnlocked("throw") && LvThrowPower < 4 && Money >= CostThrowPower) c++;
            if (IsNodeUnlocked("truck") && LvTruckSize < 3 && Money >= CostTruck) c++;
            if (IsNodeUnlocked("magnet") && !HasTruckMagnet && Money >= CostMagnet) c++;
            if (IsNodeUnlocked("mask") && LvMask < 5 && Money >= CostMask) c++;
            if (IsNodeUnlocked("mop") && !HasMopUnlocked && Money >= CostMop) c++;
            if (IsNodeUnlocked("scrub") && LvScrubPower < 3 && Money >= CostScrub) c++;
            if (IsNodeUnlocked("luck") && LvTreasureLuck < 3 && Money >= CostLuck) c++;
            if (IsNodeUnlocked("poker") && LvPokerFace < 4 && Money >= CostPoker) c++;
            return c;
        }

        public bool BuyNode(string id)
        {
            if (!IsNodeUnlocked(id)) return false;
            switch (id)
            {
                case "sweep":
                    if (LvSweepRadius >= 5 || Money < CostSweep) return false;
                    Money -= CostSweep; LvSweepRadius++; break;
                case "peel":
                    if (LvPeelPower >= 2 || Money < CostPeel) return false;
                    Money -= CostPeel; LvPeelPower++; break;
                case "fist":
                    if (LvFistPower >= 4 || Money < CostFist) return false;
                    Money -= CostFist; LvFistPower++; break;
                case "vacuum":
                    if (HasVacuumUnlocked || Money < CostVacuum) return false;
                    Money -= CostVacuum; HasVacuumUnlocked = true; break;
                case "beacon":
                    if (HasBeaconDetect || Money < CostBeacon) return false;
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
                    if (HasTruckMagnet || Money < CostMagnet) return false;
                    Money -= CostMagnet; HasTruckMagnet = true; break;
                case "mask":
                    if (LvMask >= 5 || Money < CostMask) return false;
                    Money -= CostMask; LvMask++; break;
                case "mop":
                    if (HasMopUnlocked || Money < CostMop) return false;
                    Money -= CostMop; HasMopUnlocked = true; break;
                case "scrub":
                    if (LvScrubPower >= 3 || Money < CostScrub) return false;
                    Money -= CostScrub; LvScrubPower++; break;
                case "luck":
                    if (LvTreasureLuck >= 3 || Money < CostLuck) return false;
                    Money -= CostLuck; LvTreasureLuck++; break;
                case "poker":
                    if (LvPokerFace >= 4 || Money < CostPoker) return false;
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
                sfx.PlayAlert();
            }
            else if (StolenCount == 0)
            {
                Money += finalBonus;
                LastStarRating = 5;
                sfx.PlayFanfare();
            }
            else
            {
                Money += finalBonus;
                LastStarRating = 4;
                sfx.PlayFanfare();
            }

            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ReplayRoomKeepUpgrades()
        {
            foreach (var p in _bigPiles) if (p != null) p.ResetForNewRun();
            foreach (var s in _phase2Stains) if (s != null) s.ResetForNewRun();
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
            HasCarriedBag = false;
            CarriedBagCount = 0;
            CarriedBagValue = 0;
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            StenchPercent = 0f;
            StolenCount = 0;
            StolenValue = 0;
            StoleDesignatedItem = false;
            TrashPayMultiplier = 1.0f;
            ShopPriceMultiplier = 1.0f;
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
            _toastTimer = 3.0f;
        }

        private static void UnlockCursor(bool unlock)
        {
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
