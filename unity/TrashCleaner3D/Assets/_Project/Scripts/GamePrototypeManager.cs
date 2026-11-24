using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        Phase1_TrashRemoval, // 1단계: 1,000+ 쓰레기 1개씩 클릭 줍기 + 단단한 큰 봉투 터뜨리기 → 먼 트럭에 투하
        Phase2_FloorMopping, // 2단계: 마우스 흔들기 전용 바닥 얼룩 걸레질
        TreasureDecision,    // 희귀 보물 발굴 즉시 이펙트 모달 (보관 vs 슈킹 즉시 결정!)
        MindMapUpgrade,      // [Tab] 반응형 아이콘 마인드맵 강화
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
        public int riskLevel;       // 1~3
        public int demeritPercent;  // 발각/해프닝 확률 (%)
        public int fineAmount;      // 적발 시 즉시 벌금
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
    /// TrashCleaner3D v0.4 중앙 매니저:
    /// - 희귀 보물(방당 최대 3개) 발굴 시 화려한 이펙트 + 즉시 보관/슈킹 결정 & 확실한 체감 디메리트(트럭 봉쇄·벌금·속도 저하)
    /// - 단단한 큰 봉투(초반 10방) · 먼 트럭(7.2m) · 실패 봉투 재회수 · 5초 이내 연속 콤보 보너스
    /// - 봉투 100% 가득 차야 왼손에 나타나고 던지기 가능 · 던지는 힘 강화 노드 추가 · 트럭 짐칸에 실제 봉투 적층!
    /// - 체감되는 강력한 악취 디버프(이속 -65%, 시야 압박, 기침 반동, 독가스 맥동) & 3D 월드 돈 팝업!
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public InstancedTrashSwarm trashSwarm;
        public Transform playerSpawnPoint;
        public Transform truckBedGoal;
        public Transform truckVisualTransform;
        public Transform truckStackedParent;
        public GameObject truckBlockedBarrier;
        public Light ceilingLight;
        public ProceduralSfx sfx;
        public GameObject thrownBagPrefab;

        [Header("Room 01 Config")]
        public string roomTitle = "Stage 1 · 반지하 자취 원룸";
        public string clientMemo = "침대 밑 '엄마가 주신 금반지'만 꼭 지켜주세요!";
        public int baseCleanReward = 350;
        public int lostItemTip = 95;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeOverlay = GamePhase.Phase1_TrashRemoval;

        public int Money { get; private set; } = 0;

        // ===== 강력한 악취 누적 (0~100%, 문 밖 z > 2.0 나가면 빠르게 회복!) =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        public float CoughKick01 { get; private set; } // 기침 시 1회성 수직 반동 이펙트 (멀미 X)
        public float FreshAirFlash01 { get; private set; }
        private float _coughTimer = 3.0f;

        /// <summary>0:쾌적(<25), 1:25%(이속-25%), 2:50%(이속-38%·손-35%), 3:75%(이속-50%·기침·시야압박), 4:100%(이속-65%·손-70%!)</summary>
        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.35f : StenchStage == 3 ? 0.50f : StenchStage == 2 ? 0.62f : StenchStage == 1 ? 0.75f : 1.0f;
        public float StenchWorkSpeedMult => (StenchStage == 4 ? 0.30f : StenchStage == 3 ? 0.48f : StenchStage == 2 ? 0.65f : 1.0f) * (RemoteWatchTimer > 0f ? 0.65f : 1.0f);
        public float StenchGainPerSec => 3.4f * (1f - LvMask * 0.16f);

        // ===== 왼손 쓰레기봉투 (가득 차야 왼손에 나타나고 던지기 가능! 트럭에 넣을 때 정산!) =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly int[] PickCountByLv = { 1, 2, 4, 7, 12, 20 };
        private static readonly float[] FistDmgByLv = { 1.0f, 2.0f, 3.4f, 5.0f, 10.0f };

        public int HandBagCount { get; private set; } = 0;
        public int HandBagPendingValue { get; private set; } = 0;
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (HasVacuumUnlocked ? 3 : 1);
        public bool IsHandBagFull => HandBagCount >= HandBagCapacity;
        // 가득 찼거나, 방 쓰레기가 100% 끝난 마지막 잔여 봉투일 때만 던지기 허용!
        public bool CanThrowHandBag => IsHandBagFull || (Phase1Cleaned >= Phase1Total && HandBagCount > 0);

        // ===== 5초 이내 연속 투하 콤보 보너스 =====
        public int ThrowComboCount { get; private set; } = 0;
        public float ThrowComboTimer { get; private set; } = 0f;

        // ===== 트럭 짐칸 적층 봉투 카운트 =====
        private int _stackedBagsInTruck = 0;
        private readonly List<GameObject> _stackedVisuals = new List<GameObject>();

        // ===== 3D 월드 돈 팝업 =====
        public readonly List<FloatingWorldText> FloatingTexts = new List<FloatingWorldText>();

        // ===== 아이콘형 마인드맵 13개 노드 =====
        public int LvSweepRadius { get; private set; }      // 0~5 (1클릭 줍기 갯수: 1→2→4→7→12→20개)
        public int LvFistPower { get; private set; }        // 0~4 (큰 봉투 10방→5→3→2→1방)
        public bool HasVacuumUnlocked { get; private set; } // 진공청소기 (좌클릭 꾹 연속 흡입 + 용량 3배)
        public bool HasBeaconDetect { get; private set; }   // 10% 이하 빛기둥 탐지
        public int LvBagSize { get; private set; }          // 0~4 (봉투 용량 15→180)
        public int LvThrowPower { get; private set; }       // 0~4 (쓰레기 던지는 힘 강화!)
        public int LvTruckSize { get; private set; }        // 0~3 (트럭 투입구 확장 + 보너스)
        public bool HasTruckMagnet { get; private set; }    // 트럭 자석 압축기
        public int LvMask { get; private set; }             // 0~5 (악취 누적 속도 완화)
        public bool HasMopUnlocked { get; private set; }    // 물티슈 → T자 대걸레
        public int LvScrubPower { get; private set; }       // 0~3 (얼룩 닦기 고속화)
        public int LvTreasureLuck { get; private set; }     // 0~3 (보물 가치 업)
        public int LvPokerFace { get; private set; }        // 0~4 (슈킹 디메리트 발각 확률 완화)

        // Derived Stats
        public int PickCountPerClick => PickCountByLv[Mathf.Clamp(LvSweepRadius, 0, PickCountByLv.Length - 1)];
        public float SweepRadius => (HasVacuumUnlocked ? 1.85f : 0.48f) + LvSweepRadius * 0.22f;
        public int SweepPerTick => HasVacuumUnlocked ? Mathf.Max(8, PickCountPerClick) : PickCountPerClick;
        public float FistDamage => HasVacuumUnlocked ? Mathf.Max(4.0f, FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)]) : FistDmgByLv[Mathf.Clamp(LvFistPower, 0, 4)];
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.6f : 1.0f) * (1f + LvScrubPower * 0.45f);
        public float ThrowForwardForce => 5.2f + LvThrowPower * 1.85f;
        public float ThrowUpForce => 2.8f + LvThrowPower * 0.45f;

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

        // ===== 희귀 보물 즉시 결정(방당 최대 3개!) + 체감되는 슈킹 디메리트 =====
        public DiscoveredTreasure ActiveTreasure { get; private set; }
        public float TreasureDecisionLockTimer { get; private set; } // 연타 실수 방지 0.55초 보호!
        public float TreasureFlash01 { get; private set; }           // 황금 발견 플래시 이펙트
        public float StealAlertFlash01 { get; private set; }         // 슈킹 적발 빨간 사이렌 플래시!

        private int _treasuresSpawnedThisRoom;
        private int _pilesBrokenCount;
        private bool _designatedFound;

        private float _internalSuspicion;
        public int StolenCount { get; private set; }
        public int StolenValue { get; private set; }
        public bool StoleDesignatedItem { get; private set; }

        // 체감되는 슈킹 디메리트 2종!
        public float RemoteWatchTimer { get; private set; }   // 20초 홈캠 감시 (작업속도 -35% + 즉석 벌금!)
        public string LandlordMessage { get; private set; } = "";
        public float DoorVisitorTimer { get; private set; }   // 15초 집주인 출동 (트럭 빨간 배리어 봉쇄 + 투하 불가 + 벌금!)

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
        }

        private void Start()
        {
            if (trashSwarm == null) trashSwarm = FindFirstObjectByType<InstancedTrashSwarm>();
            if (trashSwarm == null)
            {
                GameObject swObj = new GameObject("TrashSwarm_1000_AutoFallback");
                trashSwarm = swObj.AddComponent<InstancedTrashSwarm>();
                trashSwarm.manager = this;
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
                Material fbMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fbMat.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.62f));
                fbMat.enableInstancing = true;
                trashSwarm.meshTypes.Add(new InstancedTrashSwarm.TrashMeshType
                {
                    label = "쓰레기",
                    mesh = cubeMesh,
                    material = fbMat,
                    normalizedScale = 0.16f
                });
                trashSwarm.SpawnInitialSwarm(1000);
            }
            CollectTargets();
            UpdateAtmosphere();
            UpdateTruckVisualScale();
            if (truckBlockedBarrier != null) truckBlockedBarrier.SetActive(false);
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
            if (StealAlertFlash01 > 0f) StealAlertFlash01 = Mathf.MoveTowards(StealAlertFlash01, 0f, dt * 0.9f);
            if (CoughKick01 > 0f) CoughKick01 = Mathf.MoveTowards(CoughKick01, 0f, dt * 3.8f);
            if (FreshAirFlash01 > 0f) FreshAirFlash01 = Mathf.MoveTowards(FreshAirFlash01, 0f, dt * 1.8f);

            // 월드 돈 팝업 수명 갱신
            for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            {
                FloatingTexts[i].age += dt;
                FloatingTexts[i].worldPos += Vector3.up * (0.65f * dt);
                if (FloatingTexts[i].age >= FloatingTexts[i].duration)
                {
                    FloatingTexts.RemoveAt(i);
                }
            }

            if (Phase == GamePhase.TreasureDecision)
            {
                if (TreasureDecisionLockTimer > 0f) TreasureDecisionLockTimer -= dt;
                return;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            // 5초 연속 투하 콤보 타이머
            if (ThrowComboTimer > 0f)
            {
                ThrowComboTimer -= dt;
                if (ThrowComboTimer <= 0f) ThrowComboCount = 0;
            }

            // 슈킹 디메리트 타이머
            if (RemoteWatchTimer > 0f)
            {
                RemoteWatchTimer -= dt;
                if (RemoteWatchTimer <= 0f) LandlordMessage = "";
            }
            if (DoorVisitorTimer > 0f)
            {
                DoorVisitorTimer -= dt;
                if (DoorVisitorTimer <= 0f)
                {
                    if (truckBlockedBarrier != null) truckBlockedBarrier.SetActive(false);
                    ShowToast("집주인이 돌아갔습니다! 이제 트럭 봉쇄 해제!");
                }
            }

            // 강력한 악취 누적 vs 문 밖(z > 2.0) 신선한 공기 회복!
            if (player != null)
            {
                bool wasOutside = IsOutsideVentilating;
                IsOutsideVentilating = player.transform.position.z > 2.0f;
                if (IsOutsideVentilating)
                {
                    if (!wasOutside && StenchPercent >= 20f)
                    {
                        FreshAirFlash01 = 1f;
                        sfx.PlayChime();
                    }
                    if (StenchPercent > 0.1f)
                    {
                        StenchPercent = Mathf.Max(0f, StenchPercent - 45f * dt);
                    }
                }
                else
                {
                    float roomFilth = Mathf.Lerp(1.0f, 0.25f, OverallCleanPercent / 100f);
                    StenchPercent = Mathf.Min(100f, StenchPercent + StenchGainPerSec * roomFilth * dt);
                }
            }

            // 50%+ 호흡 곤란 / 75%+ 기침 반동 이펙트!
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

            // 10% 이하 탐지 빛기둥
            bool p1Beacon = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval && Phase1Percent >= 90f;
            foreach (var p in _bigPiles) if (p != null) p.SetBeaconVisible(p1Beacon && !p.IsCleaned);
            bool p2Beacon = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping && (Phase2Total - Phase2Cleaned) <= 2;
            foreach (var s in _phase2Stains) if (s != null) s.SetBeaconVisible(p2Beacon && !s.IsCleaned);
        }

        public void SpawnFloatingText(Vector3 worldPos, string text, Color color, float duration = 1.6f)
        {
            FloatingTexts.Add(new FloatingWorldText
            {
                worldPos = worldPos,
                text = text,
                color = color,
                age = 0f,
                duration = duration
            });
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

            // 보물 개수를 방당 딱 3개로 희귀하게 압축! (4번째, 8번째, 12번째 큰 봉투에서 확정/고확률 등장!)
            if (_treasuresSpawnedThisRoom < 3 && (_pilesBrokenCount == 4 || _pilesBrokenCount == 8 || _pilesBrokenCount == 12))
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
                    value = 320,
                    rarity = TreasureRarity.Legendary,
                    isDesignatedLostItem = true,
                    riskLevel = 3,
                    demeritPercent = 100,
                    fineAmount = 90
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
                    fineAmount = 40 + risk * 15
                };
            }

            ActiveTreasure = item;
            TreasureFlash01 = 1.0f;
            TreasureDecisionLockTimer = 0.55f; // 클릭 연타 실수 방지 보호!
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
                ShowToast($"[정직 보관] {item.itemName} 반납 완료! 팁 +${tip}");
                return;
            }

            // 즉시 슈킹! 돈은 즉시 입금되지만, 확실하고 눈에 띄는 디메리트 확률 판정!
            StolenCount++;
            StolenValue += item.value;
            Money += item.value;
            sfx.PlaySteal();

            if (item.isDesignatedLostItem)
            {
                StoleDesignatedItem = true;
                _internalSuspicion = 100f;
                ApplyStealDemerit(item, true);
                return;
            }

            _internalSuspicion = Mathf.Clamp(_internalSuspicion + Mathf.Max(10f, item.riskLevel * 16f - LvPokerFace * 5f), 0f, 95f);

            if (Random.Range(0, 100) < item.demeritPercent)
            {
                ApplyStealDemerit(item, false);
            }
            else
            {
                ShowToast($"[슈킹 성공!!] 들키지 않고 +${item.value} 즉시 획득!");
            }
        }

        private void ApplyStealDemerit(DiscoveredTreasure item, bool severe)
        {
            StealAlertFlash01 = 1.0f;
            sfx.PlayAlert();

            int fine = Mathf.Min(Money, item.fineAmount);
            Money -= fine;

            if (severe || Random.value < 0.5f)
            {
                // 디메리트 A: 15초간 집주인 현장 출동 → 트럭에 빨간 배리어 봉쇄(투하 불가!) + 벌금!
                DoorVisitorTimer = 15f;
                if (truckBlockedBarrier != null) truckBlockedBarrier.SetActive(true);
                ShowToast($"[슈킹 적발!] 벌금 -${fine} + 15초간 집주인 감시로 트럭 봉쇄!");
            }
            else
            {
                // 디메리트 B: 20초간 홈캠 원격 감시 → 작업속도 -35% + 즉석 벌금!
                RemoteWatchTimer = 20f;
                LandlordMessage = $"[홈캠 적발!] 벌금 -${fine} · 20초간 집주인 감시 (작업속도 -35%!)";
                ShowToast($"[홈캠 적발!] 벌금 -${fine} + 20초간 작업속도 -35% 디버프!");
            }
        }

        public void ThrowHandBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (HandBagCount <= 0) return;

            if (!CanThrowHandBag)
            {
                ShowToast($"봉투를 가득 채워야 던질 수 있습니다 ({HandBagCount}/{HandBagCapacity})");
                return;
            }

            if (DoorVisitorTimer > 0f)
            {
                sfx.PlayAlert();
                StealAlertFlash01 = 0.5f;
                ShowToast($"[트럭 봉쇄 중!] 집주인이 보고 있어 {DoorVisitorTimer:0}초간 버릴 수 없습니다!");
                return;
            }

            int count = HandBagCount;
            int pendingVal = HandBagPendingValue;
            HandBagCount = 0;
            HandBagPendingValue = 0;
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
        /// 던지기 실패해 바닥에 떨어진 쓰레기봉지를 다시 주워 손에 들기!
        /// </summary>
        public void OnDroppedBagRetrieved(int count, int pendingVal)
        {
            HandBagCount += count;
            HandBagPendingValue += pendingVal;
            sfx.PlayPop(1.05f);
            ShowToast($"떨어진 봉투 회수! 다시 트럭을 향해 [우클릭] 던지세요 (${HandBagPendingValue})");
        }

        /// <summary>
        /// 트럭 골인 시: 5초 연속 콤보 계산 + 3D 투하 위치에 돈 팝업 + 트럭 짐칸 안에 실제 3D 봉투 쌓기!
        /// </summary>
        public void OnBagScored(int count, int bagValue, float distance, Vector3 scoreWorldPos)
        {
            if (ThrowComboTimer > 0f) ThrowComboCount = Mathf.Min(5, ThrowComboCount + 1);
            else ThrowComboCount = 1;
            ThrowComboTimer = 5.0f;

            int baseVal = Mathf.Max(bagValue, count * 3);
            float comboMult = 1f + (ThrowComboCount - 1) * 0.25f; // 1x, 1.25x, 1.5x, 1.75x, 2.0x!
            int distBonus = distance >= 6.5f ? 35 : distance >= 4.2f ? 15 : 0;
            int truckBonus = LvTruckSize * 15;
            int totalPay = Mathf.RoundToInt(baseVal * comboMult) + distBonus + truckBonus;

            Money += totalPay;
            sfx.PlayChime();

            // 트럭 짐칸에 실제 봉투 쌓기!
            SpawnStackedBagVisualInTruck();

            // 투하한 바로 그 3D 위치에 직관적인 돈 팝업 띄우기!
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
            if (_stackedBagsInTruck >= 45) return; // 최대 45개까지 깔끔하게 적층

            int idx = _stackedBagsInTruck++;
            int layer = idx / 4; // 한 층에 2x2 배치로 차곡차곡 쌓임!
            int slot = idx % 4;
            float sx = (slot % 2 == 0 ? -0.24f : 0.24f) + Random.Range(-0.06f, 0.06f);
            float sz = (slot < 2 ? -0.22f : 0.22f) + Random.Range(-0.06f, 0.06f);
            float sy = layer * 0.24f;

            GameObject bag = Instantiate(thrownBagPrefab, truckStackedParent);
            bag.name = $"StackedBag_{idx + 1:00}";
            bag.SetActive(true);
            bag.transform.localPosition = new Vector3(sx, sy, sz);
            bag.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            bag.transform.localScale = Vector3.one * 0.92f;
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
                ShowToast("1단계 쓰레기 100% 완료! 이제 2단계: 좌클릭한 채 마우스를 흔들어 얼룩을 닦으세요!");
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
                _phaseBeforeOverlay = Phase;
                Phase = GamePhase.MindMapUpgrade;
                UnlockCursor(true);
            }
        }

        // ===== 마인드맵 13개 노드 가격 & 구매 =====
        public int CostSweep => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvSweepRadius));
        public int CostFist => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostVacuum => 280;
        public int CostBeacon => 60;
        public int CostBag => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvBagSize));
        public int CostThrowPower => Mathf.RoundToInt(30 * Mathf.Pow(1.8f, LvThrowPower));
        public int CostTruck => Mathf.RoundToInt(65 * Mathf.Pow(1.9f, LvTruckSize));
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
            if (LvFistPower < 4 && Money >= CostFist) c++;
            if (LvSweepRadius >= 2 && !HasVacuumUnlocked && Money >= CostVacuum) c++;
            if (LvFistPower >= 1 && !HasBeaconDetect && Money >= CostBeacon) c++;
            if (LvBagSize < 4 && Money >= CostBag) c++;
            if (LvThrowPower < 4 && Money >= CostThrowPower) c++;
            if (LvBagSize >= 1 && LvTruckSize < 3 && Money >= CostTruck) c++;
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
                    if (LvBagSize < 1 || LvTruckSize >= 3 || Money < CostTruck) return false;
                    Money -= CostTruck; LvTruckSize++; UpdateTruckVisualScale(); break;
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

        private void UpdateTruckVisualScale()
        {
            if (truckVisualTransform == null) return;
            float w = 1.18f + LvTruckSize * 0.45f;
            float d = 1.08f + LvTruckSize * 0.36f;
            truckVisualTransform.localScale = new Vector3(w, 0.92f, d);
        }

        private void FinishRoomComplete()
        {
            float effectiveChance = StoleDesignatedItem ? 100f : Mathf.Clamp(_internalSuspicion, 0f, 95f);

            LastWasCaught = StolenCount > 0 && Random.Range(0f, 100f) < effectiveChance;
            if (LastWasCaught)
            {
                Money += Mathf.RoundToInt(baseCleanReward * 0.5f);
                LastStarRating = 1;
                LastReviewText = StoleDesignatedItem
                    ? "\"방은 새 집 됐는데 엄마 금반지가 없어졌어요! 악덕 업체 신고합니다 ★☆☆☆☆\""
                    : "\"청소는 잘해주셨는데 집 물건이 사라졌네요? 평판 바닥 찍어드립니다 ★☆☆☆☆\"";
                sfx.PlayAlert();
            }
            else if (StolenCount == 0)
            {
                Money += baseCleanReward;
                LastStarRating = 5;
                LastReviewText = "\"와 쓰레기 1,000개가 싹 사라졌어요!! 물건도 다 지켜주시고 최고예요 ★★★★★\"";
                sfx.PlayFanfare();
            }
            else
            {
                Money += baseCleanReward;
                LastStarRating = 3;
                LastReviewText = "\"완전 새 집 됐네요! 근데 원래 여기 있던 내 비상금이 어디 갔지...? ★★★☆☆\"";
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
            ThrowComboCount = 0;
            ThrowComboTimer = 0f;
            StenchPercent = 0f;
            StolenCount = 0;
            StolenValue = 0;
            StoleDesignatedItem = false;
            _internalSuspicion = 0f;
            RemoteWatchTimer = 0f;
            DoorVisitorTimer = 0f;
            if (truckBlockedBarrier != null) truckBlockedBarrier.SetActive(false);
            LandlordMessage = "";
            _treasuresSpawnedThisRoom = 0;
            _pilesBrokenCount = 0;
            _designatedFound = false;
            Phase = GamePhase.Phase1_TrashRemoval;

            UpdateAtmosphere();
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
            RenderSettings.fogDensity = Mathf.Lerp(0.036f, 0.003f, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.18f, 0.16f, 0.12f), new Color(0.80f, 0.83f, 0.85f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.26f, 0.24f, 0.20f), new Color(0.62f, 0.63f, 0.64f), t);
            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 0.99f, 0.96f), t);
                ceilingLight.intensity = Mathf.Lerp(11f, 19f, t);
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
