using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        Phase1_TrashRemoval, // 1단계: 1,000+ 쓰레기 광역 훑기 + 큰 봉투 터뜨리기 → 트럭 슛 (100% 철거)
        Phase2_FloorMopping, // 2단계: 바닥 얼룩 물티슈/대걸레 마우스 흔들어 닦기
        MindMapUpgrade,      // [Tab] 언제든 여는 아이콘형 마인드맵 + 보물 보관/즉시 슈킹
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
        public int riskLevel; // 1~3
        public bool resolved;
        public bool wasStolen;
    }

    /// <summary>
    /// TrashCleaner3D v0.3 중앙 매니저:
    /// - 강제 턴 종료·시간초 삭제! [Tab]으로 언제든 작은 아이콘 마인드맵 강화
    /// - 상단 악취 바(0~100%): 25/50/75/100% 단계별 누적 디버프, 열린 현관문 밖(z>2.0) 나가면 시원하게 회복
    /// - 1,000+ GPU-Instanced 쓰레기 동전줍기식 훑기 + 진공청소기 해방감
    /// - [Tab] 즉시 슈킹 + 즉시 발동하는 재밌는 현장 해프닝(집주인 카톡 추궁 / 문 앞 이웃 10초 등장)
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public InstancedTrashSwarm trashSwarm;
        public Transform playerSpawnPoint;
        public Transform truckBedGoal;
        public Transform truckVisualTransform;
        public Light ceilingLight;
        public ProceduralSfx sfx;
        public GameObject thrownBagPrefab;

        [Header("Room 01 Config")]
        public string roomTitle = "Stage 1 · 반지하 자취 원룸";
        public string clientMemo = "침대 밑 '엄마가 주신 금반지'만 꼭 지켜주세요!";
        public int baseCleanReward = 350;
        public int lostItemTip = 90;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeTab = GamePhase.Phase1_TrashRemoval;

        public int Money { get; private set; } = 0;

        // ===== 악취 누적 (0 ~ 100%, 문 밖 z > 2.0 나가면 2초 만에 회복!) =====
        public float StenchPercent { get; private set; } = 0f;
        public bool IsOutsideVentilating { get; private set; }
        private float _coughTimer = 4f;

        /// <summary>0:쾌적(<25), 1:25%(이속↓), 2:50%(+손놀림↓), 3:75%(+어질/기침), 4:100%(전체 강화!)</summary>
        public int StenchStage => StenchPercent >= 99.5f ? 4 : StenchPercent >= 75f ? 3 : StenchPercent >= 50f ? 2 : StenchPercent >= 25f ? 1 : 0;
        public float StenchMoveSpeedMult => StenchStage == 4 ? 0.65f : StenchStage >= 1 ? 0.85f : 1.0f;
        public float StenchWorkSpeedMult => StenchStage == 4 ? 0.50f : StenchStage >= 2 ? 0.75f : 1.0f;
        public float StenchGainPerSec => 2.8f * (1f - LvMask * 0.16f); // 마스크 Lv.5 시 80% 차단!

        // ===== 왼손 쓰레기봉투 (주울 땐 돈 0! 문 밖 쓰레기 트럭에 버릴 때 모아둔 돈 일괄 정산!) =====
        private static readonly int[] BagCapByLv = { 15, 35, 65, 110, 180 };
        private static readonly int[] PickCountByLv = { 1, 2, 4, 7, 12, 20 };

        public int HandBagCount { get; private set; } = 0;
        public int HandBagPendingValue { get; private set; } = 0;
        public int HandBagCapacity => BagCapByLv[Mathf.Clamp(LvBagSize, 0, BagCapByLv.Length - 1)] * (HasVacuumUnlocked ? 3 : 1);
        public bool IsHandBagFull => HandBagCount >= HandBagCapacity;

        // ===== 아이콘형 마인드맵 12개 노드 =====
        // 주먹·줍기 갈래
        public int LvSweepRadius { get; private set; }   // 0~5 (한 번 클릭 줍기 갯수: 1→2→4→7→12→20개!)
        public int LvFistPower { get; private set; }     // 0~4 (큰 봉투 4방→1방 컷)
        public bool HasVacuumUnlocked { get; private set; } // 진공청소기 (꾹 눌러 연속 흡입 + 봉투 용량 3배!)
        public bool HasBeaconDetect { get; private set; }// 10% 이하 빛기둥 탐지
        // 봉투·트럭 갈래
        public int LvBagSize { get; private set; }       // 0~4 (봉투 용량 15 → 180)
        public int LvTruckSize { get; private set; }     // 0~3 (트럭 짐칸 확대 + 투하 보너스)
        public bool HasTruckMagnet { get; private set; } // 대충 던져도 트럭으로 빨려 들어감
        // 마스크·걸레 갈래
        public int LvMask { get; private set; }          // 0~5 (악취 누적 속도 대폭 완화)
        public bool HasMopUnlocked { get; private set; } // 물티슈 → T자 대걸레
        public int LvScrubPower { get; private set; }    // 0~3 (얼룩 닦기 고속화)
        // 보물·슈킹 갈래
        public int LvTreasureLuck { get; private set; }  // 0~3 (보물 가치 & 발견 확률 업)
        public int LvPokerFace { get; private set; }     // 0~4 (즉시 슈킹 해프닝·의심 확률 완화)

        // Derived Gameplay Stats
        public int PickCountPerClick => PickCountByLv[Mathf.Clamp(LvSweepRadius, 0, PickCountByLv.Length - 1)];
        public float SweepRadius => (HasVacuumUnlocked ? 1.85f : 0.48f) + LvSweepRadius * 0.22f;
        public int SweepPerTick => HasVacuumUnlocked ? Mathf.Max(8, PickCountPerClick) : PickCountPerClick;
        public float FistDamage => (HasVacuumUnlocked ? 3.5f : 1.0f) + LvFistPower * 0.75f;
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.6f : 1.0f) * (1f + LvScrubPower * 0.45f);

        // Progress Tracking (Big Piles + 1,000+ Instanced Swarm + Phase 2 Stains)
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

        // ===== 발굴 보물함 + 즉시 슈킹 & 재밌는 현장 해프닝 =====
        public readonly List<DiscoveredTreasure> DiscoveredTreasures = new List<DiscoveredTreasure>();
        public int PendingTreasureCount
        {
            get
            {
                int c = 0;
                foreach (var t in DiscoveredTreasures) if (!t.resolved) c++;
                return c;
            }
        }

        private float _internalSuspicion;
        public int StolenCount { get; private set; }
        public int StolenValue { get; private set; }
        public bool StoleDesignatedItem { get; private set; }

        // 즉시 슈킹 해프닝 2종!
        public float RemoteWatchTimer { get; private set; }   // 📱 집주인 카톡 추궁 15초 원격 감시
        public string LandlordMessage { get; private set; } = "";
        public float DoorVisitorTimer { get; private set; }   // 🚪 문 앞 집주인/이웃 10초 등장 (슛 금지!)

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
        private int _gachaIndex;
        private bool _designatedFound;

        private static readonly (string name, int val, TreasureRarity rarity)[] TreasurePool =
        {
            ("구겨진 5만 원권 뭉치", 95, TreasureRarity.Common),
            ("무선 이어폰 케이스", 85, TreasureRarity.Common),
            ("중고 휴대용 게임기", 150, TreasureRarity.Rare),
            ("빈티지 금시계", 210, TreasureRarity.Rare),
            ("비상금 봉투", 260, TreasureRarity.Rare),
            ("24K 미니 골드바", 420, TreasureRarity.Legendary)
        };

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
        }

        private void Start()
        {
            if (trashSwarm == null) trashSwarm = FindFirstObjectByType<InstancedTrashSwarm>();
            // 구버전 씬에서 실행되더라도 1,000개 쓰레기가 비지 않도록 런타임 안전망 자동 생성
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
        }

        private void CollectTargets()
        {
            _bigPiles.Clear();
            _phase2Stains.Clear();
            foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
            {
                if (c.kind == CleanableKind.FloorStain) _phase2Stains.Add(c);
                else if (c.kind == CleanableKind.TrashPile) _bigPiles.Add(c);
                else if (c.kind == CleanableKind.SmallTrash) c.gameObject.SetActive(false); // 구버전 잔여 오브젝트 자동 정리
            }
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f) ToastMessage = "";
            }

            if (_pitchResetTimer > 0f)
            {
                _pitchResetTimer -= Time.deltaTime;
                if (_pitchResetTimer <= 0f) _pitchCombo = 0f;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            // 즉시 슈킹 해프닝 타이머
            if (RemoteWatchTimer > 0f)
            {
                RemoteWatchTimer -= Time.deltaTime;
                if (RemoteWatchTimer <= 0f) LandlordMessage = "";
            }
            if (DoorVisitorTimer > 0f)
            {
                DoorVisitorTimer -= Time.deltaTime;
                if (DoorVisitorTimer <= 0f) ShowToast("문 밖 집주인이 돌아갔습니다. 이제 다시 트럭에 슛 하세요!");
            }

            // 악취 바 누적 vs 열린 현관문 밖(z > 2.0f) 환기 회복!
            if (player != null)
            {
                IsOutsideVentilating = player.transform.position.z > 2.0f;
                if (IsOutsideVentilating)
                {
                    if (StenchPercent > 0.1f)
                    {
                        StenchPercent = Mathf.Max(0f, StenchPercent - 48f * Time.deltaTime);
                    }
                }
                else
                {
                    float roomFilth = Mathf.Lerp(1.0f, 0.25f, OverallCleanPercent / 100f);
                    StenchPercent = Mathf.Min(100f, StenchPercent + StenchGainPerSec * roomFilth * Time.deltaTime);
                }
            }

            // 75%+ 어질/숨참 기침 효과음
            if (StenchStage >= 3 && !IsOutsideVentilating)
            {
                _coughTimer -= Time.deltaTime;
                if (_coughTimer <= 0f)
                {
                    _coughTimer = StenchStage == 4 ? 2.6f : 4.5f;
                    sfx.PlayScrub();
                }
            }

            // 10% 이하 탐지 빛기둥 (큰 봉투 & 얼룩)
            bool p1Beacon = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval && Phase1Percent >= 90f;
            foreach (var p in _bigPiles) if (p != null) p.SetBeaconVisible(p1Beacon && !p.IsCleaned);
            bool p2Beacon = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping && (Phase2Total - Phase2Cleaned) <= 2;
            foreach (var s in _phase2Stains) if (s != null) s.SetBeaconVisible(p2Beacon && !s.IsCleaned);
        }

        public void OnInstancedTrashCollected(int itemValue)
        {
            // 주울 때는 돈을 바로 주지 않고 봉투에 담음 → 문 밖 쓰레기 트럭(쓰레기통)에 버릴 때 일괄 정산!
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
            // 큰 봉투를 터뜨려도 즉시 돈 지급 X → 쏟아진 쓰레기를 주워 트럭에 버릴 때 정산!
            sfx.PlayPop(0.68f);

            if (trashSwarm != null)
            {
                trashSwarm.SpawnBurstFountain(pile.transform.position, 20);
            }

            // 보물 발견 (현장 팝업 없이 발견 보물함에 자동 저장)
            float chance = 0.55f + LvTreasureLuck * 0.12f;
            if (Random.value < chance || _gachaIndex == 1)
            {
                DiscoverTreasureSilently();
            }
            _gachaIndex++;

            UpdateAtmosphere();
            CheckPhase1Complete();
        }

        private void DiscoverTreasureSilently()
        {
            DiscoveredTreasure item;
            if (!_designatedFound && _gachaIndex >= 2)
            {
                _designatedFound = true;
                item = new DiscoveredTreasure
                {
                    itemName = "엄마가 주신 금반지 (지정 분실물!)",
                    value = 280,
                    rarity = TreasureRarity.Legendary,
                    isDesignatedLostItem = true,
                    riskLevel = 3
                };
            }
            else
            {
                int maxIdx = Mathf.Min(TreasurePool.Length, 3 + LvTreasureLuck);
                var pick = TreasurePool[Random.Range(0, maxIdx)];
                item = new DiscoveredTreasure
                {
                    itemName = pick.name,
                    value = Mathf.RoundToInt(pick.val * (1f + LvTreasureLuck * 0.2f)),
                    rarity = pick.rarity,
                    isDesignatedLostItem = false,
                    riskLevel = pick.rarity == TreasureRarity.Legendary ? 3 : pick.rarity == TreasureRarity.Rare ? 2 : 1
                };
            }

            DiscoveredTreasures.Add(item);
            sfx.PlayChime();
            ShowToast($"★ 보물 발굴: [{item.itemName}] ([Tab] 보관/슈킹)");
        }

        /// <summary>
        /// [Tab]에서 즉시 보관 or 즉시 슈킹 (즉시 슈킹 시 재밌는 현장 해프닝 확률 발동!)
        /// </summary>
        public void ResolveTreasure(DiscoveredTreasure item, bool steal)
        {
            if (item == null || item.resolved) return;
            item.resolved = true;
            item.wasStolen = steal;

            if (!steal)
            {
                int tip = item.isDesignatedLostItem ? lostItemTip : Mathf.Max(20, item.value / 5);
                Money += tip;
                sfx.PlayChime();
                ShowToast($"[정직 보관] {item.itemName} 반납 완료! 팁 +${tip}");
                return;
            }

            // 즉시 슈킹! 돈은 바로 입금 (성장 쾌감 유지)
            StolenCount++;
            StolenValue += item.value;
            Money += item.value;
            sfx.PlaySteal();

            if (item.isDesignatedLostItem)
            {
                StoleDesignatedItem = true;
                _internalSuspicion = 100f;
                TriggerImmediateStealEvent(true);
                return;
            }

            float addSusp = item.riskLevel * 12f + StolenCount * 5f - LvPokerFace * 4f;
            if (RemoteWatchTimer > 0f) addSusp *= 1.8f; // 원격 감시 중 추가 슈킹 시 의심 급상승!
            _internalSuspicion = Mathf.Clamp(_internalSuspicion + Mathf.Max(5f, addSusp), 0f, 95f);

            // 즉시 디메리트 해프닝 확률 체크 (돈 뺏기 대신 재밌는 실시간 긴장감!)
            float eventChance = Mathf.Clamp(18f + item.riskLevel * 14f + StolenCount * 8f - LvPokerFace * 8f, 10f, 85f);
            if (Random.Range(0f, 100f) < eventChance)
            {
                TriggerImmediateStealEvent(false);
            }
            else
            {
                ShowToast($"[몰래 슈킹 성공!] +${item.value} 즉시 입금! 마인드맵 강화에 쓰세요.");
            }
        }

        private void TriggerImmediateStealEvent(bool severe)
        {
            sfx.PlayAlert();
            if (severe || Random.value < 0.5f)
            {
                RemoteWatchTimer = 15f;
                LandlordMessage = severe
                    ? "[집주인 카톡] \"사장님!! 침대 밑 반지 케이스 알람 떴는데 뭐 하세요?!\""
                    : "[집주인 카톡] \"사장님, 방금 홈캠 모션 감지 떴는데 귀중품 건드리셨나요??\"";
                ShowToast("[경고] 집주인 카톡 추궁! 15초간 원격 감시 발동 (추가 슈킹 주의)");
            }
            else
            {
                DoorVisitorTimer = 10f;
                ShowToast("[똑똑!] 문 앞 복도에 집주인이 10초간 나타났습니다 (트럭 슛 금지)");
            }
        }

        public void ThrowHandBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (HandBagCount <= 0) return;

            if (DoorVisitorTimer > 0f)
            {
                sfx.PlayAlert();
                ShowToast("문 밖에 집주인이 보고 있습니다! 잠시 기다리세요");
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
            rb.mass = 1.2f;

            var proj = bagObj.AddComponent<ThrownTrashBag>();
            proj.Launch(forwardDir.normalized * 8.2f + Vector3.up * 3.4f, spawnPos, truckBedGoal, this, count, pendingVal, HasTruckMagnet);
        }

        public void OnBagScored(int count, int bagValue, float distance, bool directHit)
        {
            // 쓰레기통(트럭)에 버릴 때 비로소 돈이 들어옴!
            int shootBonus = directHit ? (LvTruckSize * 15 + (distance >= 3.8f ? 25 : 10)) : 0;
            int totalPay = Mathf.Max(bagValue, count * 3) + shootBonus;
            Money += totalPay;
            sfx.PlayChime();
            if (directHit)
            {
                ShowToast(distance >= 3.8f
                    ? $"★ 3점 롱 슛 정산! +${totalPay} (보너스 +${shootBonus})"
                    : $"★ 쓰레기 투하 정산! +${totalPay}");
            }
            else
            {
                ShowToast($"쓰레기 수거 정산 +${totalPay} (트럭에 맞추면 보너스!)");
            }
            CheckPhase1Complete();
        }

        private void CheckPhase1Complete()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            // 봉투에 남은 게 있어도 즉시 2단계 걸레질로 전환! (남은 봉투는 2단계 중에도 우클릭으로 던져 정산 가능)
            if (Phase1Cleaned >= Phase1Total && Phase1Total > 0)
            {
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                ShowToast("1단계 쓰레기 100% 완료! 이제 2단계: 좌클릭+마우스 흔들기로 바닥 얼룩을 닦으세요!");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            Money += stain.rewardMoney;
            sfx.PlayChime();
            UpdateAtmosphere();
            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        public void ToggleMindMap()
        {
            if (Phase == GamePhase.RoomCompleted) return;
            if (Phase == GamePhase.MindMapUpgrade)
            {
                Phase = _phaseBeforeTab;
                UnlockCursor(false);
            }
            else
            {
                _phaseBeforeTab = Phase;
                Phase = GamePhase.MindMapUpgrade;
                UnlockCursor(true);
            }
        }

        // ===== 마인드맵 12개 노드 가격 & 구매 =====
        public int CostSweep => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvSweepRadius));
        public int CostFist => Mathf.RoundToInt(35 * Mathf.Pow(1.8f, LvFistPower));
        public int CostVacuum => 280;
        public int CostBeacon => 60;
        public int CostBag => Mathf.RoundToInt(25 * Mathf.Pow(1.85f, LvBagSize));
        public int CostTruck => Mathf.RoundToInt(70 * Mathf.Pow(1.9f, LvTruckSize));
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
            sfx.PlayUpgrade();
            return true;
        }

        private void UpdateTruckVisualScale()
        {
            if (truckVisualTransform == null) return;
            float s = 1f + LvTruckSize * 0.22f;
            truckVisualTransform.localScale = new Vector3(1.8f * s, 0.85f, 1.5f * s);
        }

        private void FinishRoomComplete()
        {
            // 미결정 보물이 남아있으면 자동 정직 반납 처리
            foreach (var t in DiscoveredTreasures)
            {
                if (!t.resolved) ResolveTreasure(t, false);
            }

            float effectiveChance = StoleDesignatedItem ? 100f
                : StolenCount == 0 ? 0f
                : (StolenCount >= 4 ? Mathf.Max(_internalSuspicion, 25f) : Mathf.Clamp(_internalSuspicion, 5f, 95f));

            LastWasCaught = StolenCount > 0 && Random.Range(0f, 100f) < effectiveChance;
            if (LastWasCaught)
            {
                Money += Mathf.RoundToInt(baseCleanReward * 0.5f); // 업글·돈 안 뺏고 보너스 수당만 50% 삭감!
                LastStarRating = 1;
                LastReviewText = StoleDesignatedItem
                    ? "\"방은 새 집 됐는데 엄마 금반지가 없어졌어요! 악덕 업체 신고합니다 ★☆☆☆☆\""
                    : "\"청소는 잘해주셨는데 집 물건이 너무 많이 사라졌네요? 평판 바닥 찍어드립니다 ★☆☆☆☆\"";
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

            DiscoveredTreasures.Clear();
            HandBagCount = 0;
            HandBagPendingValue = 0;
            StenchPercent = 0f;
            StolenCount = 0;
            StolenValue = 0;
            StoleDesignatedItem = false;
            _internalSuspicion = 0f;
            RemoteWatchTimer = 0f;
            DoorVisitorTimer = 0f;
            LandlordMessage = "";
            _gachaIndex = 0;
            _designatedFound = false;
            Phase = GamePhase.Phase1_TrashRemoval;

            UpdateAtmosphere();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            UnlockCursor(false);
            ShowToast("단골 원룸 재발 의뢰! 강화된 장비로 1,000개 쓰레기를 싹쓸이하세요!");
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
