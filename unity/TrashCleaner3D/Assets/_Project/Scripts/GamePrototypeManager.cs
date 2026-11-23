using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    public enum GamePhase
    {
        Phase1_TrashRemoval, // 1단계: 큰 봉투 퍽! 뜯기 → 작은 쓰레기 봉투에 담아 트럭 슛 (100% 철거)
        Phase2_FloorMopping, // 2단계: 쓰레기 100% 후 바닥 얼룩 물티슈/대걸레 문질러 닦기
        MindMapUpgrade,      // 턴 종료(스태미나 소진) → 마인드맵 스킬트리 강화
        GachaStealPopup,     // 보물 발견! 화려한 가챠 연출 + 3.5초 좌(보관) vs 우(슈킹) 본능 선택
        RoomCompleted        // 방 완청 결과 & 별점 리뷰
    }

    public enum TreasureRarity { Common, Rare, Legendary }

    [System.Serializable]
    public struct GachaTreasureOffer
    {
        public string itemName;
        public int value;
        public TreasureRarity rarity;
        public bool isDesignatedLostItem;
        public int riskIcons; // 1~3 직관적 눈알 아이콘
    }

    /// <summary>
    /// TrashCleaner3D v0.2 핵심 게임 매니저.
    /// - 시간 타이머 대신 '행동 스태미나' 기반 짧은 턴 (스트레스 없이 25~35초마다 마인드맵 도파민!)
    /// - 1단계 쓰레기 100% 철거(10% 이하 빛기둥 탐지) → 2단계 바닥 걸레질 피날레
    /// - 큰 봉투 퍽! 터뜨리기 → 작은 쓰레기 스폰 + 보물 가챠 좌/우 3.5초 본능 선택
    /// - 직관적 4단계 의심 눈알 + 문 밖 인기척 긴장감 이벤트
    /// </summary>
    public class GamePrototypeManager : MonoBehaviour
    {
        [Header("Scene References")]
        public FirstPersonCleanerController player;
        public Transform playerSpawnPoint;
        public Transform truckBedGoal;
        public Transform truckVisualTransform;
        public Light ceilingLight;
        public ProceduralSfx sfx;
        public GameObject[] burstSmallTrashPrefabs;
        public GameObject thrownBagPrefab;
        public Transform dynamicTrashParent;

        [Header("Room 01 Config")]
        public string roomTitle = "Stage 1 · 반지하 자취 원룸";
        public string clientMemo = "침대 밑 '엄마가 주신 금반지'만 꼭 지켜주세요!";
        public int baseCleanReward = 220;
        public int lostItemTip = 65;

        public GamePhase Phase { get; private set; } = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforePopup = GamePhase.Phase1_TrashRemoval;
        private GamePhase _phaseBeforeMindMap = GamePhase.Phase1_TrashRemoval;

        public int TurnCount { get; private set; } = 1;
        public int Money { get; private set; } = 0;

        // 행동 스태미나 (시간 제한 스트레스 없이, 행동할 때만 소모 → 짧은 턴 도파민!)
        public float Stamina { get; private set; } = 24f;
        public float MaxStamina => 24f + LvStamina * 12f;

        // 왼손 쓰레기봉투 적재량 (꽉 차면 트럭으로 슛!)
        public int HandBagCount { get; private set; } = 0;
        public int HandBagCapacity => 6 + LvBagSize * 3;
        public bool IsHandBagFull => HandBagCount >= HandBagCapacity;

        // ===== 마인드맵 12개 노드 레벨 =====
        // 👊 주먹·파괴 갈래
        public int LvFistPower { get; private set; }     // 0~5 (기본 1.0 → 4방 컷에서 1방 컷으로!)
        public int LvShockwave { get; private set; }     // 0~3 (터뜨릴 때 주변 봉투 연쇄 충격)
        public bool HasBeaconDetect { get; private set; }// 10% 이하 빛기둥 탐지
        // 🛍️ 봉투·트럭 갈래
        public int LvBagSize { get; private set; }       // 0~4 (봉투 용량 6 → 18)
        public int LvTruckSize { get; private set; }     // 0~3 (트럭 짐칸 확대 + 골인 보너스)
        public bool HasTruckMagnet { get; private set; } // 대충 던져도 트럭으로 빨려 들어감
        // 💚 체력·닦기 갈래
        public int LvStamina { get; private set; }       // 0~5 (최대 스태미나 24 → 84)
        public bool HasMopUnlocked { get; private set; } // 물티슈 → 시원한 T자 대걸레 구매!
        public int LvScrubPower { get; private set; }    // 0~3 (얼룩 닦기 속도 업)
        // 😈 보물·슈킹 갈래
        public int LvTreasureLuck { get; private set; }  // 0~3 (보물 등장 확률 & 가치 업)
        public int LvPokerFace { get; private set; }     // 0~4 (의심도 상승 완화)
        public int LvBlackMarket { get; private set; }   // 0~3 (슈킹 보물 금액 +25%/Lv)

        // Derived combat/clean stats
        public float FistDamage => 1.0f + LvFistPower * 0.65f;
        public float ScrubSpeedMult => (HasMopUnlocked ? 2.5f : 1.0f) * (1f + LvScrubPower * 0.45f);
        public float TreasureValueMult => 1f + LvBlackMarket * 0.25f;

        // Phase 1 & Phase 2 Progress Tracking
        private readonly List<CleanableTarget> _phase1Items = new List<CleanableTarget>();
        private readonly List<CleanableTarget> _phase2Stains = new List<CleanableTarget>();
        private readonly List<GameObject> _spawnedBurstObjects = new List<GameObject>();

        public int Phase1Total { get; private set; }
        public int Phase1Cleaned { get; private set; }
        public int Phase2Total { get; private set; }
        public int Phase2Cleaned { get; private set; }

        public float Phase1Percent => Phase1Total > 0 ? (Phase1Cleaned / (float)Phase1Total) * 100f : 100f;
        public float Phase2Percent => Phase2Total > 0 ? (Phase2Cleaned / (float)Phase2Total) * 100f : 100f;
        public float OverallCleanPercent => Phase1Percent * 0.72f + Phase2Percent * 0.28f;

        // Gacha Instinct Steal State (3.5s countdown!)
        public GachaTreasureOffer CurrentOffer { get; private set; }
        public float OfferTimer { get; private set; }
        public const float OfferDuration = 3.5f;
        private int _gachaRollIndex;
        private bool _designatedOffered;

        // 직관적 의심도 (내부 0~100, 화면엔 숫자 숨기고 4단계 눈알 표시!)
        private float _internalSuspicion;
        public int StolenCount { get; private set; }
        public int StolenValue { get; private set; }
        public bool StoleDesignatedItem { get; private set; }

        // 문 밖 인기척(노크/발소리) 3.5초 긴장감 이벤트
        public bool IsKnockAlertActive => _knockAlertTimer > 0f;
        private float _knockAlertTimer;
        private float _nextKnockCooldown = 16f;

        // Settlement
        public bool LastWasCaught { get; private set; }
        public int LastStarRating { get; private set; } = 5;
        public string LastReviewText { get; private set; } = "";
        public string ToastMessage { get; private set; } = "";
        private float _toastTimer;

        /// <summary>
        /// 0: 평온(초록), 1: 의아함(노랑), 2: 의심!(주황), 3: 들통 직전!!(빨강)
        /// </summary>
        public int SuspicionStage
        {
            get
            {
                if (StoleDesignatedItem || _internalSuspicion >= 60f) return 3;
                if (_internalSuspicion >= 32f) return 2;
                if (_internalSuspicion >= 12f) return 1;
                return 0;
            }
        }

        private static readonly (string name, int val, TreasureRarity rarity)[] TreasurePool =
        {
            ("구겨진 5만 원권 뭉치", 85, TreasureRarity.Common),
            ("무선 이어폰 케이스", 75, TreasureRarity.Common),
            ("중고 휴대용 게임기", 130, TreasureRarity.Rare),
            ("빈티지 금시계", 180, TreasureRarity.Rare),
            ("숨겨둔 비상금 봉투", 240, TreasureRarity.Rare),
            ("24K 미니 골드바", 380, TreasureRarity.Legendary)
        };

        private void Awake()
        {
            if (sfx == null) sfx = gameObject.AddComponent<ProceduralSfx>();
        }

        private void Start()
        {
            CollectInitialTargets();
            Stamina = MaxStamina;
            UpdateAtmosphere();
            UpdateTruckVisualScale();
            ShowToast("1단계: 큰 봉투를 주먹으로 퍽!퍽! 터뜨리고, 봉투를 채워 문 밖 트럭에 슛 하세요!");
        }

        private void CollectInitialTargets()
        {
            _phase1Items.Clear();
            _phase2Stains.Clear();
            foreach (var c in FindObjectsByType<CleanableTarget>(FindObjectsSortMode.None))
            {
                if (c.kind == CleanableKind.FloorStain) _phase2Stains.Add(c);
                else _phase1Items.Add(c);
            }
            RecountProgress();
        }

        private void RecountProgress()
        {
            Phase1Total = _phase1Items.Count;
            Phase1Cleaned = 0;
            int p1Remaining = 0;
            foreach (var c in _phase1Items)
            {
                if (c == null || c.IsCleaned) Phase1Cleaned++;
                else p1Remaining++;
            }

            Phase2Total = _phase2Stains.Count;
            Phase2Cleaned = 0;
            int p2Remaining = 0;
            foreach (var s in _phase2Stains)
            {
                if (s == null || s.IsCleaned) Phase2Cleaned++;
                else p2Remaining++;
            }

            // 10% 이하(또는 3개 이하) 탐지 빛기둥 업그레이드 자동 발동!
            bool showP1Beacon = HasBeaconDetect && Phase == GamePhase.Phase1_TrashRemoval &&
                                p1Remaining > 0 && (p1Remaining <= 3 || Phase1Percent >= 90f);
            foreach (var c in _phase1Items)
            {
                if (c != null) c.SetBeaconVisible(showP1Beacon && !c.IsCleaned);
            }

            bool showP2Beacon = HasBeaconDetect && Phase == GamePhase.Phase2_FloorMopping &&
                                p2Remaining > 0 && p2Remaining <= 2;
            foreach (var s in _phase2Stains)
            {
                if (s != null) s.SetBeaconVisible(showP2Beacon && !s.IsCleaned);
            }
        }

        private void Update()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f) ToastMessage = "";
            }

            // 가챠 3.5초 본능 선택 타이머
            if (Phase == GamePhase.GachaStealPopup)
            {
                OfferTimer -= Time.deltaTime;
                if (OfferTimer <= 0f)
                {
                    ResolveGachaChoice(steal: false);
                }
                return;
            }

            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;

            // 문 밖 인기척 긴장감 이벤트 (신호등식 3.5초 경고)
            if (_knockAlertTimer > 0f)
            {
                _knockAlertTimer -= Time.deltaTime;
            }
            else
            {
                _nextKnockCooldown -= Time.deltaTime;
                if (_nextKnockCooldown <= 0f)
                {
                    _knockAlertTimer = 3.5f;
                    _nextKnockCooldown = Random.Range(18f, 28f);
                    sfx.PlayAlert();
                }
            }
        }

        public void ConsumeStamina(float amount)
        {
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;
            Stamina -= amount;
            if (Stamina <= 0f)
            {
                Stamina = 0f;
                OpenMindMap("체력이 바닥났습니다! 번 돈으로 마인드맵을 강화하고 바로 재투입하세요!");
            }
        }

        public void OnTrashPileBurst(CleanableTarget pile)
        {
            Money += pile.rewardMoney;
            sfx.PlayPop(0.75f);

            // 주변 봉투 충격파 업그레이드
            if (LvShockwave > 0)
            {
                float radius = 1.1f + LvShockwave * 0.45f;
                foreach (var other in _phase1Items)
                {
                    if (other != null && other != pile && !other.IsCleaned && other.kind == CleanableKind.TrashPile)
                    {
                        if (Vector3.Distance(pile.transform.position, other.transform.position) <= radius)
                        {
                            if (other.ApplyCleanDamage(FistDamage * 0.45f * LvShockwave, null))
                            {
                                OnTrashPileBurst(other);
                            }
                        }
                    }
                }
            }

            // 작은 쓰레기 2~3개 통통 튀어나오기 (0.45초 포물선 후 물리 OFF!)
            SpawnBurstSmallTrash(pile.transform.position, pile.burstSpawnCount);

            RecountProgress();
            UpdateAtmosphere();

            // 보물 가챠 판정 (초반에도 재밌게 자주 터지도록 40% + 행운 업글당 +12%!)
            float chance = 0.42f + LvTreasureLuck * 0.12f;
            if (pile.containsTreasureChance && (Random.value < chance || _gachaRollIndex == 1))
            {
                TriggerGachaOffer();
            }
            _gachaRollIndex++;

            CheckPhase1Completion();
        }

        private void SpawnBurstSmallTrash(Vector3 origin, int count)
        {
            if (burstSmallTrashPrefabs == null || burstSmallTrashPrefabs.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                GameObject src = burstSmallTrashPrefabs[Random.Range(0, burstSmallTrashPrefabs.Length)];
                if (src == null) continue;
                GameObject clone = Instantiate(src, origin + Vector3.up * 0.25f, Quaternion.Euler(0, Random.Range(0, 360f), 0), dynamicTrashParent);
                clone.SetActive(true);
                var ct = clone.GetComponent<CleanableTarget>();
                if (ct != null)
                {
                    // 방 경계(-2.0..2.0, -1.65..1.65) 안으로만 착지시켜 100% 도달 보장!
                    Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(0.45f, 0.95f);
                    Vector3 land = new Vector3(
                        Mathf.Clamp(origin.x + offset.x, -1.85f, 1.85f),
                        0.02f,
                        Mathf.Clamp(origin.z + offset.y, -1.55f, 1.55f)
                    );
                    ct.StartPopOut(origin + Vector3.up * 0.35f, land);
                    _phase1Items.Add(ct);
                }
                _spawnedBurstObjects.Add(clone);
            }
        }

        public void OnSmallTrashPicked(CleanableTarget item)
        {
            HandBagCount = Mathf.Min(HandBagCapacity, HandBagCount + item.bagCost);
            Money += item.rewardMoney;
            sfx.PlayPop(Mathf.Lerp(0.9f, 1.45f, HandBagCount / (float)HandBagCapacity));
            RecountProgress();
            UpdateAtmosphere();

            if (IsHandBagFull)
            {
                ShowToast("봉투 꽉 참! 열린 현관문 밖 트럭 짐칸으로 [우클릭] 농구 슛!");
            }

            CheckPhase1Completion();
        }

        public void ThrowHandBag(Vector3 spawnPos, Vector3 forwardDir)
        {
            if (HandBagCount <= 0) return;

            int count = HandBagCount;
            HandBagCount = 0;
            sfx.PlayPop(0.65f);

            GameObject bagObj;
            if (thrownBagPrefab != null)
            {
                bagObj = Instantiate(thrownBagPrefab, spawnPos, Quaternion.identity);
                bagObj.SetActive(true);
            }
            else
            {
                bagObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bagObj.transform.position = spawnPos;
                bagObj.transform.localScale = Vector3.one * 0.36f;
            }

            var rb = bagObj.GetComponent<Rigidbody>();
            if (rb == null) rb = bagObj.AddComponent<Rigidbody>();
            rb.mass = 1.2f;

            var proj = bagObj.AddComponent<ThrownTrashBag>();
            Vector3 launchVel = forwardDir.normalized * 7.8f + Vector3.up * 3.4f;
            proj.Launch(launchVel, spawnPos, truckBedGoal, this, count, HasTruckMagnet);
        }

        public void OnBagScored(int count, float distance, bool directHit)
        {
            int basePay = count * 5;
            int truckBonus = LvTruckSize * 12;
            int longShot = (directHit && distance >= 3.8f) ? 25 : (directHit ? 12 : 0);
            int total = basePay + truckBonus + longShot;
            Money += total;

            if (directHit)
            {
                sfx.PlayChime();
                string label = distance >= 3.8f ? "3점 롱 슛 골인!!" : "나이스 슛 골인!";
                ShowToast($"{label} 트럭 수거 완료 +${total}");
            }
            else
            {
                ShowToast($"트럭 자동 회수 +${total}");
            }

            CheckPhase1Completion();
        }

        private void CheckPhase1Completion()
        {
            if (Phase != GamePhase.Phase1_TrashRemoval) return;
            if (Phase1Cleaned >= Phase1Total && Phase1Total > 0 && HandBagCount == 0)
            {
                Phase = GamePhase.Phase2_FloorMopping;
                sfx.PlayFanfare();
                RecountProgress();
                ShowToast("1단계 쓰레기 100% 철거 완료! 이제 2단계: 바닥 얼룩을 마우스로 쓱쓱 문질러 닦으세요!");
            }
            else if (Phase1Cleaned >= Phase1Total && HandBagCount > 0)
            {
                ShowToast("마지막 봉투를 현관문 밖 트럭에 던져 넣으면 2단계 걸레질로 넘어갑니다!");
            }
        }

        public void OnStainCleaned(CleanableTarget stain)
        {
            Money += stain.rewardMoney;
            sfx.PlayChime();
            RecountProgress();
            UpdateAtmosphere();

            if (Phase == GamePhase.Phase2_FloorMopping && Phase2Cleaned >= Phase2Total && Phase2Total > 0)
            {
                FinishRoomComplete();
            }
        }

        private void TriggerGachaOffer()
        {
            _phaseBeforePopup = Phase;
            Phase = GamePhase.GachaStealPopup;
            OfferTimer = OfferDuration;
            UnlockCursor(true);

            // 3번째 보물쯤에 의뢰인 지정 분실물(엄마 금반지) 한 번 출현시켜 양심 밀당!
            if (!_designatedOffered && _gachaRollIndex >= 2)
            {
                _designatedOffered = true;
                CurrentOffer = new GachaTreasureOffer
                {
                    itemName = "엄마가 주신 금반지 (의뢰인 분실물!)",
                    value = 260,
                    rarity = TreasureRarity.Legendary,
                    isDesignatedLostItem = true,
                    riskIcons = 3
                };
                sfx.PlayFanfare();
                return;
            }

            int maxIdx = Mathf.Min(TreasurePool.Length, 3 + LvTreasureLuck);
            var pick = TreasurePool[Random.Range(0, maxIdx)];
            int finalVal = Mathf.RoundToInt(pick.val * TreasureValueMult);
            int risk = pick.rarity == TreasureRarity.Legendary ? 3 : pick.rarity == TreasureRarity.Rare ? 2 : 1;

            CurrentOffer = new GachaTreasureOffer
            {
                itemName = pick.name,
                value = finalVal,
                rarity = pick.rarity,
                isDesignatedLostItem = false,
                riskIcons = risk
            };

            if (pick.rarity == TreasureRarity.Legendary) sfx.PlayFanfare();
            else sfx.PlayChime();
        }

        public void ResolveGachaChoice(bool steal)
        {
            if (Phase != GamePhase.GachaStealPopup) return;

            if (!steal)
            {
                int tip = CurrentOffer.isDesignatedLostItem ? lostItemTip : Mathf.Max(15, CurrentOffer.value / 6);
                Money += tip;
                sfx.PlayChime();
                ShowToast($"[정직 보관] {CurrentOffer.itemName} 선반에 올려둠! 팁 +${tip}");
            }
            else
            {
                StolenCount++;
                StolenValue += CurrentOffer.value;
                Money += CurrentOffer.value;

                if (CurrentOffer.isDesignatedLostItem)
                {
                    StoleDesignatedItem = true;
                    _internalSuspicion = 100f;
                    sfx.PlayAlert();
                    ShowToast($"의뢰인 지정 분실물 슈킹!! +${CurrentOffer.value} (집주인이 무조건 눈치챕니다!)");
                }
                else
                {
                    float baseAdd = CurrentOffer.riskIcons * 11f + StolenCount * 4.5f;
                    float knockBonus = IsKnockAlertActive ? 22f : 0f;
                    float pokerCut = LvPokerFace * 4.5f;
                    _internalSuspicion = Mathf.Clamp(_internalSuspicion + Mathf.Max(5f, baseAdd + knockBonus - pokerCut), 0f, 95f);
                    sfx.PlaySteal();
                    ShowToast(IsKnockAlertActive
                        ? $"문 밖 인기척 중에 슈킹! +${CurrentOffer.value} (의심도 급상승!!)"
                        : $"몰래 슈킹 성공! +${CurrentOffer.value}");
                }
            }

            Phase = _phaseBeforePopup;
            UnlockCursor(false);
        }

        public void OpenMindMap(string reasonToast = "")
        {
            if (Phase != GamePhase.Phase1_TrashRemoval && Phase != GamePhase.Phase2_FloorMopping) return;
            _phaseBeforeMindMap = Phase;
            Phase = GamePhase.MindMapUpgrade;
            UnlockCursor(true);
            if (!string.IsNullOrEmpty(reasonToast)) ShowToast(reasonToast);
        }

        public void ResumeNextTurnFromMindMap()
        {
            TurnCount++;
            Stamina = MaxStamina;
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            Phase = _phaseBeforeMindMap;
            RecountProgress();
            UnlockCursor(false);
            ShowToast($"{TurnCount}턴 투입! (이전 청소 진행도 그대로 유지)");
        }

        // ===== 마인드맵 노드 구매 함수 (12종) =====
        public int CostFist => Mathf.RoundToInt(25 * Mathf.Pow(1.75f, LvFistPower));
        public int CostShockwave => Mathf.RoundToInt(90 * Mathf.Pow(2.0f, LvShockwave));
        public int CostBeacon => 60;
        public int CostBagSize => Mathf.RoundToInt(30 * Mathf.Pow(1.8f, LvBagSize));
        public int CostTruckSize => Mathf.RoundToInt(70 * Mathf.Pow(1.9f, LvTruckSize));
        public int CostTruckMagnet => 180;
        public int CostStamina => Mathf.RoundToInt(25 * Mathf.Pow(1.75f, LvStamina));
        public int CostMop => 75;
        public int CostScrub => Mathf.RoundToInt(110 * Mathf.Pow(1.85f, LvScrubPower));
        public int CostLuck => Mathf.RoundToInt(55 * Mathf.Pow(1.85f, LvTreasureLuck));
        public int CostPoker => Mathf.RoundToInt(80 * Mathf.Pow(1.85f, LvPokerFace));
        public int CostMarket => Mathf.RoundToInt(130 * Mathf.Pow(1.9f, LvBlackMarket));

        public bool BuyNode(string id)
        {
            switch (id)
            {
                case "fist":
                    if (LvFistPower >= 5 || Money < CostFist) return false;
                    Money -= CostFist; LvFistPower++; break;
                case "shock":
                    if (LvFistPower < 1 || LvShockwave >= 3 || Money < CostShockwave) return false;
                    Money -= CostShockwave; LvShockwave++; break;
                case "beacon":
                    if (LvFistPower < 1 || HasBeaconDetect || Money < CostBeacon) return false;
                    Money -= CostBeacon; HasBeaconDetect = true; break;
                case "bag":
                    if (LvBagSize >= 4 || Money < CostBagSize) return false;
                    Money -= CostBagSize; LvBagSize++; break;
                case "truck":
                    if (LvBagSize < 1 || LvTruckSize >= 3 || Money < CostTruckSize) return false;
                    Money -= CostTruckSize; LvTruckSize++; UpdateTruckVisualScale(); break;
                case "magnet":
                    if (LvTruckSize < 1 || HasTruckMagnet || Money < CostTruckMagnet) return false;
                    Money -= CostTruckMagnet; HasTruckMagnet = true; break;
                case "stamina":
                    if (LvStamina >= 5 || Money < CostStamina) return false;
                    Money -= CostStamina; LvStamina++; break;
                case "mop":
                    if (LvStamina < 1 || HasMopUnlocked || Money < CostMop) return false;
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
                case "market":
                    if (LvTreasureLuck < 1 || LvBlackMarket >= 3 || Money < CostMarket) return false;
                    Money -= CostMarket; LvBlackMarket++; break;
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
            float effectiveChance = StoleDesignatedItem ? 100f
                : StolenCount == 0 ? 0f
                : (StolenCount >= 4 ? Mathf.Max(_internalSuspicion, 25f) : Mathf.Clamp(_internalSuspicion, 5f, 95f));

            LastWasCaught = StolenCount > 0 && Random.Range(0f, 100f) < effectiveChance;

            if (LastWasCaught)
            {
                int penalty = StolenValue + Mathf.RoundToInt(baseCleanReward * 0.5f);
                Money = Mathf.Max(0, Money - penalty);
                LastStarRating = 1;
                LastReviewText = StoleDesignatedItem
                    ? "\"방은 새 집 됐는데 엄마 유품 금반지 어디 갔어요?! 바로 경찰서 갑니다.\""
                    : "\"청소는 잘해주셨는데 집 물건이 너무 많이 사라졌네요? 당장 신고합니다.\"";
                sfx.PlayAlert();
            }
            else if (StolenCount == 0)
            {
                Money += baseCleanReward;
                LastStarRating = 5;
                LastReviewText = "\"와 진짜 새 집이 됐어요!! 물건도 다 지켜주시고 최고예요 ㅠㅠ\"";
                sfx.PlayFanfare();
            }
            else
            {
                Money += baseCleanReward;
                LastStarRating = 3;
                LastReviewText = "\"방이 엄청 깨끗해졌네요! 근데 원래 여기 있던 내 비상금이 어디 갔지...?\"";
                sfx.PlayFanfare();
            }

            Phase = GamePhase.RoomCompleted;
            UnlockCursor(true);
        }

        public void ReplayRoomKeepUpgrades()
        {
            foreach (var obj in _spawnedBurstObjects)
            {
                if (obj != null) Destroy(obj);
            }
            _spawnedBurstObjects.Clear();

            _phase1Items.RemoveAll(c => c == null);
            foreach (var c in _phase1Items) c.ResetForNewRun();
            foreach (var s in _phase2Stains) s.ResetForNewRun();

            HandBagCount = 0;
            StolenCount = 0;
            StolenValue = 0;
            StoleDesignatedItem = false;
            _internalSuspicion = 0f;
            _gachaRollIndex = 0;
            _designatedOffered = false;
            TurnCount = 1;
            Stamina = MaxStamina;
            Phase = GamePhase.Phase1_TrashRemoval;

            RecountProgress();
            UpdateAtmosphere();
            if (player != null && playerSpawnPoint != null)
            {
                player.TeleportTo(playerSpawnPoint.position, playerSpawnPoint.rotation);
            }
            UnlockCursor(false);
            ShowToast("단골 원룸 재발 의뢰 시작! 강화된 능력으로 시원하게 쓸어버리세요!");
        }

        private void UpdateAtmosphere()
        {
            float t = Mathf.Clamp01(OverallCleanPercent / 100f);
            RenderSettings.fogDensity = Mathf.Lerp(0.038f, 0.003f, t);
            RenderSettings.fogColor = Color.Lerp(new Color(0.18f, 0.16f, 0.12f), new Color(0.80f, 0.83f, 0.85f), t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.25f, 0.23f, 0.19f), new Color(0.62f, 0.63f, 0.64f), t);
            if (ceilingLight != null)
            {
                ceilingLight.color = Color.Lerp(new Color(1.0f, 0.91f, 0.74f), new Color(1.0f, 0.99f, 0.96f), t);
                ceilingLight.intensity = Mathf.Lerp(11f, 19f, t);
            }
        }

        public void ShowToast(string msg)
        {
            ToastMessage = msg;
            _toastTimer = 3.6f;
        }

        private static void UnlockCursor(bool unlock)
        {
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }
    }
}
