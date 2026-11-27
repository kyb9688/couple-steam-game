using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.6 스테이지별 큰 봉투·바닥 얼룩 배치.
    ///
    /// 배경(버그): Stage 2~7 씬은 Room01 씬을 복사해 만든 탓에, 실제 방(Stage0N_xxx)과는 별개로
    /// 예전 'Stage1_OneRoom_Container'·'Stage2_Apartment_Container'가 통째로 비활성 상태로 남아 있었다.
    /// 그 안의 큰 봉투 20개·얼룩 10개는 씬에는 존재하지만 꺼져 있어서, Stage 2~7은 작은 쓰레기만
    /// 빨아들이는 단조로운 판이 되고 진행률 계산에도 잡히지 않는 잔여물이었다.
    ///
    /// 이 클래스는 그 오브젝트들을 런타임에 공통 루트로 옮겨, 각 스테이지 실제 방 규격
    /// (InstancedTrashSwarm 의 RoomZoneDef) 안에 스테이지 특색에 맞는 개수로 다시 배치한다.
    ///  - ST.2 택배 오피스텔: 택배박스/스티로폼 큰 봉투 비중 대폭 증가
    ///  - ST.3 치킨집 주방: 바닥 얼룩(행주질) 비중 대폭 증가
    ///  - ST.4 맞벌이 부부 집: 방 2구역(거실·안방)에 고르게 분산
    ///  - ST.5 스타트업 사무실: 대량 물량전에 맞춰 봉투 다수
    ///  - ST.6 시골 농막: 좁은 방에 악취와 함께 굵직한 봉투 위주
    ///  - ST.7 3층 펜트하우스: 1F·2F·3F 층별로 분산
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
                case 1: return new Plan { pileCount = 8,  stainCount = 5,  pileHp = 9f,  stainHp = 2.6f, stainReward = 18 };
                case 2: return new Plan { pileCount = 26, stainCount = 4,  pileHp = 10f, stainHp = 2.6f, stainReward = 22 };
                case 3: return new Plan { pileCount = 8,  stainCount = 16, pileHp = 10f, stainHp = 2.1f, stainReward = 24 };
                case 4: return new Plan { pileCount = 16, stainCount = 8,  pileHp = 11f, stainHp = 2.4f, stainReward = 26 };
                case 5: return new Plan { pileCount = 18, stainCount = 8,  pileHp = 12f, stainHp = 2.5f, stainReward = 30 };
                case 6: return new Plan { pileCount = 12, stainCount = 6,  pileHp = 12f, stainHp = 2.5f, stainReward = 30 };
                case 7: return new Plan { pileCount = 24, stainCount = 10, pileHp = 13f, stainHp = 2.6f, stainReward = 34 };
                default: return new Plan { pileCount = 8, stainCount = 5, pileHp = 9f, stainHp = 2.6f, stainReward = 18 };
            }
        }

        private static Transform _root;

        /// <summary>배치된 큰 봉투·얼룩의 공통 부모(매니저 집계용).</summary>
        public static Transform Root => _root;

        /// <summary>
        /// 현재 스테이지 규격으로 큰 봉투/얼룩을 재배치하고 켜고 끈다. 매니저 Start에서
        /// 스웜 스폰(방 규격 확정) 뒤, CollectTargets 전에 호출한다.
        /// </summary>
        public static void Apply(GamePrototypeManager mgr, int stage)
        {
            var all = Object.FindObjectsByType<CleanableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return;

            var piles = new List<CleanableTarget>(40);
            var stains = new List<CleanableTarget>(24);
            foreach (var c in all)
            {
                if (c == null) continue;
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

            int pileUse = Mathf.Min(plan.pileCount, piles.Count);
            int stainUse = Mathf.Min(plan.stainCount, stains.Count);

            // 1) 큰 봉투: 방 구역별로 나눠 배치 (앞쪽일수록 바깥 구역 → 동선 분산)
            var placed = new List<Vector3>(pileUse);
            for (int i = 0; i < piles.Count; i++)
            {
                var c = piles[i];
                bool used = i < pileUse;
                if (used)
                {
                    var room = PickRoom(rooms, i, pileUse, stage);
                    Vector3 p = PickPoint(room, placed, 0.85f, 1.6f, stage);
                    c.ConfigureForLayout(_root, p, plan.pileHp, 0);
                    placed.Add(p);
                }
                else
                {
                    // 쓰지 않는 잔여 소품은 트리 밖으로 빼고 끈다 → 진행률 집계에 끼지 않는다.
                    c.transform.SetParent(null, true);
                    c.gameObject.SetActive(false);
                }
            }

            // 2) 바닥 얼룩: 바닥 높이에 밀착
            placed.Clear();
            for (int i = 0; i < stains.Count; i++)
            {
                var c = stains[i];
                bool used = i < stainUse;
                if (used)
                {
                    var room = PickRoom(rooms, i, stainUse, stage);
                    Vector3 p = PickPoint(room, placed, 0.7f, 1.1f, stage);
                    p.y = room.floorY + 0.012f;
                    c.ConfigureForLayout(_root, p, plan.stainHp, plan.stainReward);
                    placed.Add(p);
                }
                else
                {
                    c.transform.SetParent(null, true);
                    c.gameObject.SetActive(false);
                }
            }
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

            // 방이 여러 개면 라운드로빈으로 고르게 분산 (ST.7은 3개 층을 순서대로)
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
        private static Vector3 AvoidFurniture(Vector3 p, InstancedTrashSwarm.RoomZoneDef room, int stage)
        {
            if (stage != 1) return p;
            if (p.x < -1.95f && p.z < -1.10f) return new Vector3(Mathf.Max(p.x, -1.65f), p.y, Mathf.Max(p.z, -0.95f)); // 침대
            if (p.x > -0.45f && p.x < 1.45f && p.z < -2.55f) p.z = Random.Range(-2.45f, -1.85f);                      // PC 책상
            if (p.x > -1.25f && p.x < 1.35f && p.z > 2.65f) p.z = Random.Range(1.40f, 2.55f);                          // 냉장고·싱크대
            if (p.x > 1.35f && p.x < 2.95f && p.z > 2.55f) p.z = 2.15f;                                                // 현관문 스윙
            p.x = Mathf.Clamp(p.x, room.minX + 0.45f, room.maxX - 0.45f);
            p.z = Mathf.Clamp(p.z, room.minZ + 0.45f, room.maxZ - 0.45f);
            return p;
        }
    }
}
