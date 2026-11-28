using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.9 스테이지별 큰 봉투·바닥 얼룩·보물·분실물 선반·CCTV 배치.
    /// - 스테이지별 내구도 차등화(ST.1 4타 → ST.7 120HP)로 봉투/박스 존재감 및 파쇄 강화 체감 확보
    /// - 모든 스테이지 현관문 안쪽 1.85m 안전구역 확보(문 열자마자 길막 0%)
    /// - 의뢰인 지정 분실물 1종 + 숨겨진 귀중품 + 택배박스 파열 시 확률 언박싱 보물 연동!
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
                case 1: return new Plan { pileCount = 8,  stainCount = 6,  pileHp = 5.5f,  stainHp = 2.2f, stainReward = 22 };
                case 2: return new Plan { pileCount = 26, stainCount = 12, pileHp = 13.0f, stainHp = 2.6f, stainReward = 38 };
                case 3: return new Plan { pileCount = 18, stainCount = 44, pileHp = 24.0f, stainHp = 2.8f, stainReward = 65 };
                case 4: return new Plan { pileCount = 32, stainCount = 22, pileHp = 40.0f, stainHp = 3.2f, stainReward = 110 };
                case 5: return new Plan { pileCount = 38, stainCount = 24, pileHp = 62.0f, stainHp = 3.6f, stainReward = 185 };
                case 6: return new Plan { pileCount = 26, stainCount = 18, pileHp = 85.0f, stainHp = 4.0f, stainReward = 280 };
                case 7: return new Plan { pileCount = 48, stainCount = 32, pileHp = 120.0f,stainHp = 4.5f, stainReward = 450 };
                default: return new Plan { pileCount = 8, stainCount = 6,  pileHp = 5.5f,  stainHp = 2.2f, stainReward = 22 };
            }
        }

        private static Transform _root;
        private static readonly List<CleanableTarget> _clones = new List<CleanableTarget>(64);
        private static readonly List<TreasureTarget> _treasures = new List<TreasureTarget>(16);
        private static GameObject _shelfObj;
        private static int _boxTreasuresSpawned;
        private static int _returnedSlotCounter;

        public static Transform Root => _root;
        public static Transform ReturnShelfSlot { get; private set; }
        public static IReadOnlyList<TreasureTarget> StageTreasures => _treasures;

        public static int AllocateReturnShelfSlotIndex() => _returnedSlotCounter++;

        public static void Apply(GamePrototypeManager mgr, int stage)
        {
            ClearClones();
            ClearTreasures();
            _boxTreasuresSpawned = 0;
            _returnedSlotCounter = 0;

            if (_root == null)
            {
                GameObject rootGo = GameObject.Find("StageProps_v16_Root");
                if (rootGo == null) rootGo = new GameObject("StageProps_v16_Root");
                _root = rootGo.transform;
                _root.position = Vector3.zero;
                _root.rotation = Quaternion.identity;
                _root.localScale = Vector3.one;
            }

            var all = Object.FindObjectsByType<CleanableTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var piles = new List<CleanableTarget>(48);
            var stains = new List<CleanableTarget>(24);
            if (all != null)
            {
                foreach (var c in all)
                {
                    if (c == null) continue;
                    if (c.name.Contains("_v17_")) continue;
                    if (c.kind == CleanableKind.TrashPile && (c.displayName ?? "").Contains("스티로폼"))
                    {
                        c.gameObject.SetActive(false);
                        continue;
                    }
                    if (c.kind == CleanableKind.TrashPile) piles.Add(c);
                    else if (c.kind == CleanableKind.FloorStain) stains.Add(c);
                }
            }

            piles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            stains.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            if (stage == 2 || stage == 5)
            {
                piles.Sort((a, b) =>
                {
                    int sa = BoxScore(a), sb = BoxScore(b);
                    if (sa != sb) return sb.CompareTo(sa);
                    return string.CompareOrdinal(a.name, b.name);
                });
            }

            var rooms = InstancedTrashSwarm.RoomsForStage(stage);
            Plan plan = GetPlan(stage);

            var placed = new List<Vector3>(64);
            if (piles.Count > 0)
            {
                var list = ExpandTo(piles, plan.pileCount);
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    var room = PickRoom(rooms, i, list.Count, stage);
                    Vector3 p = PickPoint(room, placed, 0.85f, 1.55f, stage);
                    c.ConfigureForLayout(_root, p, plan.pileHp, 0);
                    c.gameObject.SetActive(true);
                    placed.Add(p);
                }
            }

            if (stains.Count > 0)
            {
                var slist = ExpandTo(stains, plan.stainCount);
                placed.Clear();
                for (int i = 0; i < slist.Count; i++)
                {
                    var c = slist[i];
                    var room = PickRoom(rooms, i, slist.Count, stage);
                    Vector3 p = PickPoint(room, placed, 0.65f, 0.95f, stage);
                    p.y = room.floorY + 0.014f;
                    c.ConfigureForLayout(_root, p, plan.stainHp, plan.stainReward);
                    // Stage 3 치킨집 주방: 진한 황금 갈색 튀김 기름때/양념 얼룩으로 선명하게 강조!
                    if (stage == 3)
                    {
                        c.displayName = i % 3 == 0 ? "치킨 양념 얼룩" : "튀김 기름때";
                        foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r != null && r.material != null)
                            {
                                Color grease = i % 3 == 0 ? new Color(0.62f, 0.16f, 0.08f, 0.92f) : new Color(0.58f, 0.36f, 0.08f, 0.92f);
                                if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", grease);
                                r.material.color = grease;
                            }
                        }
                    }
                    c.gameObject.SetActive(true);
                    placed.Add(p);
                }
            }

            ConvertStaticTrashPropsToAttackable(stage, plan.pileHp, _root);

            // [v1.9] 분실물 보관 선반 + 의뢰인 분실물 1종 + 숨겨진 귀중품 + CCTV 활성화!
            BuildReturnShelf(mgr, stage, rooms);
            // [v2.2] 보물은 바닥에 눈에 띄게 배치하지 않고, 쓰레기를 줍다가 우연히 발견되어 팝업으로 등장!
            SecurityCameraController.AutoAttachToStageCameras();
        }

        public static void ResetBoxTreasures()
        {
            _boxTreasuresSpawned = 0;
            _returnedSlotCounter = 0;
            for (int i = _treasures.Count - 1; i >= 0; i--)
            {
                var t = _treasures[i];
                if (t == null) { _treasures.RemoveAt(i); continue; }
                if (t.isFromBox)
                {
                    Object.Destroy(t.gameObject);
                    _treasures.RemoveAt(i);
                }
                else
                {
                    t.ResetForNewRun();
                }
            }
        }

        public static TreasureTarget TrySpawnBoxTreasure(Vector3 origin, int stage)
        {
            if (_boxTreasuresSpawned >= 2) return null;
            if (Random.value > 0.28f) return null;

            _boxTreasuresSpawned++;
            int val = Mathf.RoundToInt(GetHiddenBaseValue(stage) * Random.Range(1.05f, 1.45f));
            string[] names = { "미개봉 명품 향수", "새 스마트워치", "현금 봉투", "백화점 상품권 다발", "순금 반지" };
            TreasureVisualKind[] kinds = { TreasureVisualKind.SmartWatch, TreasureVisualKind.SmartWatch, TreasureVisualKind.CashBundle, TreasureVisualKind.LuckyTwoDollar, TreasureVisualKind.GoldRing };
            int k = Random.Range(0, names.Length);
            Vector3 dropPos = new Vector3(origin.x, Mathf.Max(0f, origin.y) + 0.04f, origin.z);
            var t = TreasureTarget.SpawnProcedural(_root, kinds[k], dropPos, names[k], val, false, 18f, fromBox: true);
            _treasures.Add(t);
            return t;
        }

        private static int GetHiddenBaseValue(int stage)
        {
            switch (stage)
            {
                case 1: return 110;
                case 2: return 320;
                case 3: return 820;
                case 4: return 2200;
                case 5: return 5400;
                case 6: return 9500;
                case 7: return 18000;
                default: return 120;
            }
        }

        private static void BuildReturnShelf(GamePrototypeManager mgr, int stage, InstancedTrashSwarm.RoomZoneDef[] rooms)
        {
            // [v2.1] 정체불명 문 옆 박스 제거 — 슈킹템은 발견 시 대형 팝업에서 즉시 반환/슈킹 선택!
            if (_shelfObj != null) Object.Destroy(_shelfObj);
            _shelfObj = null;
            ReturnShelfSlot = null;
        }

        private static void BuildReturnShelf_Unused(GamePrototypeManager mgr, int stage, InstancedTrashSwarm.RoomZoneDef[] rooms)
        {
            if (_shelfObj != null) Object.Destroy(_shelfObj);

            float sx = 2.85f, sz = 3.25f;
            var door = Object.FindFirstObjectByType<InteractiveDoor>();
            if (door != null)
            {
                Vector3 dc = door.doorCollider != null ? door.doorCollider.transform.position : door.transform.position;
                float sideX = stage == 2 ? dc.x + 0.72f : stage == 7 ? dc.x + 1.05f : dc.x + 0.88f;
                float minX = -3.8f, maxX = 4.2f;
                for (int r = 0; r < rooms.Length; r++)
                {
                    if (rooms[r].floorY < 0.5f)
                    {
                        minX = Mathf.Min(minX, rooms[r].minX);
                        maxX = Mathf.Max(maxX, rooms[r].maxX);
                    }
                }
                sx = Mathf.Clamp(sideX, minX + 0.4f, maxX - 0.35f);
                sz = dc.z - 0.58f;
            }

            _shelfObj = new GameObject("ClientReturnShelf_Indoor");
            _shelfObj.transform.SetParent(_root, false);
            _shelfObj.transform.position = new Vector3(sx, 0f, sz);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material wood = new Material(lit);
            Color wc = new Color(0.56f, 0.40f, 0.24f);
            if (wood.HasProperty("_BaseColor")) wood.SetColor("_BaseColor", wc);
            wood.color = wc;

            Material trayMat = new Material(lit);
            Color tc = new Color(0.24f, 0.62f, 0.42f);
            if (trayMat.HasProperty("_BaseColor")) trayMat.SetColor("_BaseColor", tc);
            trayMat.color = tc;

            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(stand.GetComponent<Collider>());
            stand.transform.SetParent(_shelfObj.transform, false);
            stand.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            stand.transform.localScale = new Vector3(0.72f, 0.88f, 0.34f);
            stand.GetComponent<Renderer>().sharedMaterial = wood;

            GameObject tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(tray.GetComponent<Collider>());
            tray.transform.SetParent(_shelfObj.transform, false);
            tray.transform.localPosition = new Vector3(0f, 0.89f, 0f);
            tray.transform.localScale = new Vector3(0.66f, 0.04f, 0.30f);
            tray.GetComponent<Renderer>().sharedMaterial = trayMat;

            GameObject slot = new GameObject("ReturnShelfSlot");
            slot.transform.SetParent(_shelfObj.transform, false);
            slot.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            ReturnShelfSlot = slot.transform;
        }

        private static void SpawnStageTreasures(int stage, InstancedTrashSwarm.RoomZoneDef[] rooms)
        {
            // 1) 의뢰인 지정 분실물 1종 (우상단 메모 위치에 정확히 배치!)
            Vector3 lostPos;
            string lostName;
            TreasureVisualKind lostKind;
            switch (stage)
            {
                case 1: lostPos = new Vector3(-2.65f, 0.02f, -2.05f); lostName = "엄마가 주신 금반지"; lostKind = TreasureVisualKind.GoldRing; break;
                case 2: lostPos = new Vector3(-1.05f, 0.02f, -1.65f); lostName = "강아지 은색 인식표"; lostKind = TreasureVisualKind.DogTag; break;
                case 3: lostPos = new Vector3(-0.35f, 0.02f, 0.85f);  lostName = "개업 기념 2달러 지폐"; lostKind = TreasureVisualKind.LuckyTwoDollar; break;
                case 4: lostPos = new Vector3(1.65f, 0.02f, -0.45f);  lostName = "아기 첫 돌 금반지"; lostKind = TreasureVisualKind.BabyGoldRing; break;
                case 5: lostPos = new Vector3(-2.70f, 0.02f, -1.75f); lostName = "법인 인감 도장"; lostKind = TreasureVisualKind.CorporateSeal; break;
                case 6: lostPos = new Vector3(-1.15f, 0.02f, -0.95f); lostName = "할아버지 회중시계"; lostKind = TreasureVisualKind.PocketWatch; break;
                default: lostPos = new Vector3(-3.30f, 6.13f, -2.40f); lostName = "스포츠카 차 키"; lostKind = TreasureVisualKind.SportsCarKey; break;
            }
            int lostTip = Mathf.RoundToInt(GetHiddenBaseValue(stage) * 1.4f);
            _treasures.Add(TreasureTarget.SpawnProcedural(_root, lostKind, lostPos, lostName, lostTip, designated: true, suspGain: 100f));

            // 2) 쓰레기 산 아래 숨겨진 보물들 (Stage 1: 3개 → Stage 7: 6개)
            int hiddenCount = stage <= 2 ? 3 : stage <= 4 ? 4 : stage <= 6 ? 5 : 6;
            int baseV = GetHiddenBaseValue(stage);
            string[] hiddenNames = { "5만 원권 다발", "고급 스마트워치", "빈티지 회중시계", "24K 골드바", "비자금 현금뭉치", "미개봉 명품 워치" };
            TreasureVisualKind[] hiddenKinds = {
                TreasureVisualKind.CashBundle, TreasureVisualKind.SmartWatch, TreasureVisualKind.PocketWatch,
                TreasureVisualKind.GoldBar, TreasureVisualKind.CashBundle, TreasureVisualKind.SmartWatch
            };
            var placed = new List<Vector3>(hiddenCount) { lostPos };
            for (int i = 0; i < hiddenCount; i++)
            {
                var room = PickRoom(rooms, i + 1, hiddenCount + 1, stage);
                Vector3 p = PickPoint(room, placed, 0.7f, 1.35f, stage);
                p.y = room.floorY + 0.025f;
                placed.Add(p);
                int val = Mathf.RoundToInt(baseV * Random.Range(0.95f, 1.55f));
                _treasures.Add(TreasureTarget.SpawnProcedural(_root, hiddenKinds[i % hiddenKinds.Length], p,
                    hiddenNames[(i + stage) % hiddenNames.Length], val, designated: false, suspGain: Random.Range(16f, 24f)));
            }
        }

        private static List<CleanableTarget> ExpandTo(List<CleanableTarget> source, int target)
        {
            var list = new List<CleanableTarget>(Mathf.Max(source.Count, target));
            list.AddRange(source);
            if (target <= 0 || source.Count == 0) { list.Clear(); return list; }
            if (list.Count > target) { list.RemoveRange(target, list.Count - target); return list; }
            int src = 0;
            while (list.Count < target)
            {
                var template = source[src % source.Count];
                src++;
                if (template == null) break;
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
            for (int i = 0; i < _clones.Count; i++)
                if (_clones[i] != null) Object.Destroy(_clones[i].gameObject);
            _clones.Clear();
        }

        private static void ClearTreasures()
        {
            for (int i = 0; i < _treasures.Count; i++)
                if (_treasures[i] != null) Object.Destroy(_treasures[i].gameObject);
            _treasures.Clear();
        }

        private static int BoxScore(CleanableTarget c)
        {
            string n = c.displayName ?? "";
            if (n.Contains("택배")) return 3;
            if (n.Contains("박스")) return 2;
            if (c.name.StartsWith("Pile2")) return 1;
            return 0;
        }

        private static InstancedTrashSwarm.RoomZoneDef PickRoom(InstancedTrashSwarm.RoomZoneDef[] rooms, int index, int total, int stage)
        {
            if (rooms == null || rooms.Length == 0)
                return new InstancedTrashSwarm.RoomZoneDef { minX = -3f, maxX = 3f, minZ = -3f, maxZ = 3f, floorY = 0f, center = Vector3.zero };
            if (rooms.Length == 1) return rooms[0];
            if (stage == 7) return rooms[index % rooms.Length];
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
                ct.maxHp = Mathf.Max(4f, pileHp * 0.78f);
                ct.burstSpawnCount = 12;
                ct.cleanWeight = 1.5f;
                ct.ConfigureForLayout(root, child.position, ct.maxHp, 0);
            }
        }

        private static Vector3 AvoidFurniture(Vector3 p, InstancedTrashSwarm.RoomZoneDef room, int stage)
        {
            if (stage == 1)
            {
                if (p.x < -1.95f && p.z < -1.10f) return new Vector3(Mathf.Max(p.x, -1.65f), p.y, Mathf.Max(p.z, -0.95f));
                if (p.x > -0.45f && p.x < 1.45f && p.z < -2.55f) p.z = Random.Range(-2.45f, -1.85f);
                if (p.x > -2.85f && p.x < -0.10f && p.z > 2.65f) p.z = Random.Range(1.40f, 2.45f);
                if (p.x > 1.25f && p.x < 3.05f && p.z > 2.05f) p.z = 1.75f; // 현관문 앞 1.85m 비움!
            }
            else if (stage == 2)
            {
                if (p.x > 0.25f && p.z > 1.65f) p.z = Random.Range(-1.8f, 1.3f);
            }
            else if (stage == 3)
            {
                if (p.x < -1.45f) p.x = Random.Range(-1.20f, 1.85f);
                if (p.z < -1.25f) p.z = Random.Range(-1.10f, 1.35f);
                if (p.x < 0.35f && p.z > 0.95f) p.z = Random.Range(-0.90f, 0.75f);
                if (p.x > 0.65f && p.z > 0.85f) p.z = Random.Range(-0.80f, 0.70f); // 현관문 스윙 비움!
            }
            else
            {
                if (room.floorY < 0.8f && p.z > room.maxZ - 1.75f && p.x > 0.2f) p.z -= 1.85f;
            }
            p.x = Mathf.Clamp(p.x, room.minX + 0.45f, room.maxX - 0.45f);
            p.z = Mathf.Clamp(p.z, room.minZ + 0.45f, room.maxZ - 0.45f);
            return p;
        }
    }
}
