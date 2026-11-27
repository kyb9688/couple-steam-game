using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.7 스테이지별 큰 봉투·바닥 얼룩 배치.
    ///
    /// 배경: v1.6에서 Stage 2~7 씬에 비활성으로 묻혀 있던 소품을 실제 방 규격에 맞춰 다시 배치했다.
    /// v1.7은 방 사용 면적을 방 끝까지 넓히고(스테이지별 +12~80%), 4시간 캠페인에 맞춰
    /// 큰 봉투·얼룩 개수도 단계적으로 늘린다. 씬에 원본 소품이 모자라면 런타임에 복제해 채운다
    /// (메시·머티리얼을 공유하므로 추가 메모리·드로우콜 부담은 작다).
    ///  - ST.2 택배 오피스텔: 택배박스/스티로폼 큰 봉투 비중 대폭 증가
    ///  - ST.3 치킨집 주방: 바닥 얼룩(행주질) 비중 대폭 증가
    ///  - ST.4 맞벌이 부부 집: 거실·안방·욕실 3구역에 분산
    ///  - ST.5 스타트업 사무실: 대량 물량전에 맞춰 봉투 다수
    ///  - ST.6 시골 농막: 좁은 방에 악취와 함께 굵직한 봉투 위주
    ///  - ST.7 3층 펜트하우스: 1F·2F·3F(랜딩·욕실·금고방 포함) 층별로 분산
    /// </summary>
    public static class StagePropLayout
    {
        public struct Plan
        {
            public int pileCount;
            public int stainCount;
            public float pileHp;
            public float stainHp;
            public int stainReward;
        }

        public static Plan GetPlan(int stage)
        {
            switch (stage)
            {
                case 1: return new Plan { pileCount = 8,  stainCount = 5,  pileHp = 7f,  stainHp = 2.4f, stainReward = 18 };
                case 2: return new Plan { pileCount = 30, stainCount = 10, pileHp = 8f,  stainHp = 2.4f, stainReward = 22 };
                case 3: return new Plan { pileCount = 20, stainCount = 40, pileHp = 8f,  stainHp = 2.2f, stainReward = 24 };
                case 4: return new Plan { pileCount = 36, stainCount = 20, pileHp = 9f,  stainHp = 2.3f, stainReward = 26 };
                case 5: return new Plan { pileCount = 45, stainCount = 24, pileHp = 9f,  stainHp = 2.3f, stainReward = 30 };
                case 6: return new Plan { pileCount = 30, stainCount = 16, pileHp = 10f, stainHp = 2.4f, stainReward = 30 };
                case 7: return new Plan { pileCount = 60, stainCount = 30, pileHp = 10f, stainHp = 2.4f, stainReward = 34 };
                default: return new Plan { pileCount = 8, stainCount = 5, pileHp = 7f, stainHp = 2.4f, stainReward = 18 };
            }
        }

        private static Transform _root;
        private static readonly List<CleanableTarget> _clones = new List<CleanableTarget>(64);

        /// <summary>배치된 큰 봉투·얼룩의 공통 부모(매니저 집계용).</summary>
        public static Transform Root => _root;

        /// <summary>
        /// 현재 스테이지 규격으로 큰 봉투/얼룩을 재배치하고 켜고 끈다. 매니저 Start에서
        /// 스웜 스폰(방 규격 확정) 뒤, CollectTargets 전에 호출한다.
        /// </summary>
        public static void Apply(GamePrototypeManager mgr, int stage)
        {
            ClearClones();

            var all = Object.FindObjectsByType<CleanableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return;

            var piles = new List<CleanableTarget>(48);
            var stains = new List<CleanableTarget>(24);
            foreach (var c in all)
            {
                if (c == null) continue;
                // v1.7: 지난 스테이지에서 복제한 소품(Destroy 예약분)은 다시 세지 않는다(중복 누적 방지).
                if (c.name.Contains("_v17_")) continue;
                // [v1.8.1] 거대 하얀 블록 덩어리로 보이던 스티로폼 큰 봉투는 배치 대상에서 제외하고 숨긴다!
                if (c.kind == CleanableKind.TrashPile && (c.displayName ?? "").Contains("스티로폼"))
                {
                    c.gameObject.SetActive(false);
                    continue;
                }
                if (c.kind == CleanableKind.TrashPile) piles.Add(c);
                else if (c.kind == CleanableKind.FloorStain) stains.Add(c);
            }
            if (piles.Count == 0 && stains.Count == 0) return;

            // 이름 기준 정렬 → 씬/스테이지와 무관하게 항상 같은 선택·같은 배치(재현 가능).
            piles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            stains.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            // ST.2는 택배박스/스티로폼 박스를 앞으로 당겨 '큰 봉투 비중 대폭 증가'를 살린다.
            if (stage == 2 || stage == 5)
            {
                piles.Sort((a, b) =>
                {
                    int sa = BoxScore(a), sb = BoxScore(b);
                    if (sa != sb) return sb.CompareTo(sa);
                    return string.CompareOrdinal(a.name, b.name);
                });
            }

            if (_root == null)
            {
                GameObject rootGo = GameObject.Find("StageProps_v16_Root");
                if (rootGo == null) rootGo = new GameObject("StageProps_v16_Root");
                _root = rootGo.transform;
                _root.position = Vector3.zero;
                _root.rotation = Quaternion.identity;
                _root.localScale = Vector3.one;
            }

            var rooms = InstancedTrashSwarm.RoomsForStage(stage);
            Plan plan = GetPlan(stage);

            // 1) 큰 봉투: 방 구역별로 나눠 배치 (앞쪽일수록 바깥 구역 → 동선 분산)
            var list = ExpandTo(piles, plan.pileCount);
            var placed = new List<Vector3>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                var room = PickRoom(rooms, i, list.Count, stage);
                Vector3 p = PickPoint(room, placed, 0.85f, 1.6f, stage);
                c.ConfigureForLayout(_root, p, plan.pileHp, 0);
                c.gameObject.SetActive(true);
                placed.Add(p);
            }

            // 2) 바닥 얼룩: 바닥 높이에 밀착
            var slist = ExpandTo(stains, plan.stainCount);
            placed.Clear();
            for (int i = 0; i < slist.Count; i++)
            {
                var c = slist[i];
                var room = PickRoom(rooms, i, slist.Count, stage);
                Vector3 p = PickPoint(room, placed, 0.7f, 1.1f, stage);
                p.y = room.floorY + 0.012f;
                c.ConfigureForLayout(_root, p, plan.stainHp, plan.stainReward);
                c.gameObject.SetActive(true);
                placed.Add(p);
            }

            // [v1.8.1] Stage 2~7 장식용 택배박스·피자박스·스티로폼도 전부 공격 가능한 CleanableTarget으로 _root 아래 편입!
            ConvertStaticTrashPropsToAttackable(stage, plan.pileHp, _root);
        }

        /// <summary>[v1.7] 씬 원본이 모자라면 원본을 복제해 목표 개수까지 채운다.</summary>
        private static List<CleanableTarget> ExpandTo(List<CleanableTarget> source, int target)
        {
            var list = new List<CleanableTarget>(Mathf.Max(source.Count, target));
            list.AddRange(source);
            if (target <= 0)
            {
                list.Clear();
                return list;
            }
            if (list.Count > target)
            {
                list.RemoveRange(target, list.Count - target);
                return list;
            }
            int src = 0;
            while (list.Count < target)
            {
                var template = source[src % source.Count];
                src++;
                if (template == null) break;
                // 비활성 템플릿도 복제 가능(복제 후 Awake 전에 ConfigureForLayout → SetActive 순서).
                var clone = Object.Instantiate(template.gameObject);
                clone.name = template.name + "_v17_" + list.Count;
                var comp = clone.GetComponent<CleanableTarget>();
                if (comp == null) { Object.Destroy(clone); break; }
                _clones.Add(comp);
                list.Add(comp);
            }
            return list;
        }

        private static void ClearClones()
        {
            // 스테이지 재진입/재시작 시 이전에 복제한 소품을 정리(중복 누적 방지)
            for (int i = 0; i < _clones.Count; i++)
            {
                if (_clones[i] != null) Object.Destroy(_clones[i].gameObject);
            }
            _clones.Clear();
        }

        private static int BoxScore(CleanableTarget c)
        {
            string n = c.displayName ?? "";
            if (n.Contains("택배")) return 3;
            if (n.Contains("박스")) return 2;
            if (c.name.StartsWith("Pile2")) return 1;   // 오피스텔 컨테이너 유래
            return 0;
        }

        private static InstancedTrashSwarm.RoomZoneDef PickRoom(InstancedTrashSwarm.RoomZoneDef[] rooms, int index, int total, int stage)
        {
            if (rooms == null || rooms.Length == 0)
                return new InstancedTrashSwarm.RoomZoneDef { minX = -3f, maxX = 3f, minZ = -3f, maxZ = 3f, floorY = 0f, center = Vector3.zero };

            if (rooms.Length == 1) return rooms[0];

            // 방이 여러 개면 라운드로빈으로 고르게 분산 (ST.7은 층별 순서대로)
            if (stage == 7)
                return rooms[index % rooms.Length];

            int slot = Mathf.Clamp(index * rooms.Length / Mathf.Max(1, total), 0, rooms.Length - 1);
            return rooms[slot];
        }

        private static Vector3 PickPoint(InstancedTrashSwarm.RoomZoneDef room, List<Vector3> placed, float margin, float minDist, int stage)
        {
            float minX = room.minX + margin, maxX = room.maxX - margin;
            float minZ = room.minZ + margin, maxZ = room.maxZ - margin;
            if (maxX < minX) { minX = room.minX; maxX = room.maxX; }
            if (maxZ < minZ) { minZ = room.minZ; maxZ = room.maxZ; }

            Vector3 best = new Vector3((minX + maxX) * 0.5f, room.floorY + 0.02f, (minZ + maxZ) * 0.5f);
            float bestScore = -1f;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector3 p = new Vector3(Random.Range(minX, maxX), room.floorY + 0.01f, Random.Range(minZ, maxZ));
                p = AvoidFurniture(p, room, stage);
                float nearest = float.MaxValue;
                for (int i = 0; i < placed.Count; i++)
                {
                    Vector3 d = placed[i] - p; d.y = 0f;
                    nearest = Mathf.Min(nearest, d.magnitude);
                }
                if (nearest > bestScore) { bestScore = nearest; best = p; }
                if (nearest >= minDist) return p;
            }
            return AvoidFurniture(best, room, stage);
        }

        /// <summary>
        /// 1단계 원룸은 침대·책상·냉장고·현관문 스윙 자리가 있다. 봉투가 그 안에 박히면
        /// 닿을 수 없어 진행률이 막히므로, InstancedTrashSwarm 이 쓰던 회피 규칙을 그대로 적용한다.
        /// </summary>
        /// <summary>
        /// [v1.8.1] Stage 2~7 가구 폴더에 장식으로 들어가 있던 택배박스·피자박스·스티로폼을
        /// 플레이어가 클릭으로 때려 터뜨릴 수 있는 CleanableTarget으로 자동 변환한다.
        /// </summary>
        private static void ConvertStaticTrashPropsToAttackable(int stage, float pileHp, Transform root)
        {
            if (stage <= 1) return;
            GameObject furn = GameObject.Find($"Furniture_Stage0{stage}_Eunju");
            if (furn == null) return;

            var toConvert = new List<Transform>();
            foreach (Transform child in furn.transform)
            {
                if (child == null || !child.gameObject.activeSelf) continue;
                string lower = child.name.ToLowerInvariant();
                bool isBoxTrash = lower.Contains("cardboard_box") || lower.Contains("pizza_box") ||
                                  lower.Contains("styrofoam") || lower.Contains("water_bottles");
                if (isBoxTrash && child.GetComponent<CleanableTarget>() == null)
                    toConvert.Add(child);
            }

            foreach (var child in toConvert)
            {
                string lower = child.name.ToLowerInvariant();
                var ct = child.gameObject.AddComponent<CleanableTarget>();
                ct.kind = CleanableKind.TrashPile;
                ct.displayName = lower.Contains("cardboard") ? "단단한 택배박스"
                               : lower.Contains("pizza") ? "피자 박스"
                               : "배달 쓰레기 박스";
                ct.maxHp = Mathf.Max(4f, pileHp * 0.75f);
                ct.burstSpawnCount = 10;
                ct.cleanWeight = 1.5f;
                ct.ConfigureForLayout(root, child.position, ct.maxHp, 0);
            }
        }

        private static Vector3 AvoidFurniture(Vector3 p, InstancedTrashSwarm.RoomZoneDef room, int stage)
        {
            if (stage == 1)
            {
                if (p.x < -1.95f && p.z < -1.10f) return new Vector3(Mathf.Max(p.x, -1.65f), p.y, Mathf.Max(p.z, -0.95f)); // 침대
                if (p.x > -0.45f && p.x < 1.45f && p.z < -2.55f) p.z = Random.Range(-2.45f, -1.85f);                      // PC 책상
                if (p.x > -1.25f && p.x < 1.35f && p.z > 2.65f) p.z = Random.Range(1.40f, 2.55f);                          // 냉장고·싱크대
                if (p.x > 1.35f && p.x < 2.95f && p.z > 2.55f) p.z = 2.15f;                                                // 현관문 스윙
            }
            else if (stage == 2)
            {
                // Stage 2 현관문 스윙 공간(x: 0.4..1.8, z > 2.0) 회피
                if (p.x > 0.35f && p.z > 1.95f) p.z = Random.Range(-1.8f, 1.4f);
            }
            else if (stage == 3)
            {
                // Stage 3 치킨집 주방: 현관문(x=1.55, z=2.5) 바로 앞 + 서/남/전면 스틸 카운터 내부 침투 방지!
                if (p.x < -1.45f) p.x = Random.Range(-1.20f, 1.85f);
                if (p.z < -1.25f) p.z = Random.Range(-1.10f, 1.35f);
                if (p.x < 0.35f && p.z > 0.95f) p.z = Random.Range(-0.90f, 0.75f);
                if (p.x > 0.65f && p.z > 1.35f) p.z = Random.Range(-0.80f, 1.15f);
            }
            else
            {
                // Stage 4~7 현관문 안쪽 스윙 1.4m 비움
                if (p.z > room.maxZ - 1.1f && p.x > 0.5f) p.z -= 1.2f;
            }
            p.x = Mathf.Clamp(p.x, room.minX + 0.45f, room.maxX - 0.45f);
            p.z = Mathf.Clamp(p.z, room.minZ + 0.45f, room.maxZ - 0.45f);
            return p;
        }
    }
}
