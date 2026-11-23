using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// 원클릭으로 Stage 1(반지하 자취 원룸) 핵심 손맛 프로토타입 씬을 자동 생성합니다.
    /// - 한국형 원룸 톤 + 쓰레기봉투 산더미(실제 스케일 정규화) + 그림자 경고 제거
    /// - 1인칭 이동/시점 + 악취 호흡 + 물리 OFF 흡입 + 얼룩 닦기 + 90% 자동 완청
    /// - 보물 슈킹(E)/보관(Q) + CCTV 거품 가리기 + Z/X/C/V 현장 강화 + 트럭 정비소
    /// 메뉴: TrashCleaner > Build Room 01 Playable Prototype
    /// </summary>
    public static class RoomToneTestBuilder
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string MatRoot = "Assets/_Project/Materials";
        private const string ScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";

        [MenuItem("TrashCleaner/Build Room 01 Playable Prototype")]
        public static void BuildRoom01()
        {
            Directory.CreateDirectory(MatRoot);
            Directory.CreateDirectory("Assets/_Project/Scenes");
            AssetDatabase.Refresh();

            // 1. URP Materials (brighter Korean ivory studio wallpaper + stained floor)
            Material matWall = CreateOrUpdatePbrMaterial(
                "Mat_Wall_KoreanIvory",
                $"{ArtRoot}/Textures/Walls/decrepit_wallpaper",
                new Vector2(3f, 1.6f),
                new Color(1.0f, 0.96f, 0.86f),
                0.12f
            );

            Material matFloor = CreateOrUpdatePbrMaterial(
                "Mat_Floor_PlankFlooring",
                $"{ArtRoot}/Textures/Floors/plank_flooring",
                new Vector2(3.2f, 3.2f),
                new Color(0.92f, 0.88f, 0.80f),
                0.18f
            );

            Material matCeiling = CreateSimpleLitMaterial("Mat_Ceiling_Ivory", new Color(0.86f, 0.84f, 0.78f), 0.1f);
            Material matDoor = CreateSimpleLitMaterial("Mat_Door_MustardYellow", new Color(0.78f, 0.62f, 0.16f), 0.25f);
            Material matAppliance = CreateSimpleLitMaterial("Mat_Fridge_Silver", new Color(0.66f, 0.68f, 0.72f), 0.45f);
            Material matMattress = CreateSimpleLitMaterial("Mat_Mattress_BeigeBrown", new Color(0.50f, 0.44f, 0.38f), 0.08f);
            Material matShelf = CreateSimpleLitMaterial("Mat_ReturnShelf_Blue", new Color(0.28f, 0.52f, 0.78f), 0.25f);
            Material matGold = CreateSimpleLitMaterial("Mat_Gold_Treasure", new Color(0.95f, 0.76f, 0.20f), 0.75f);
            Material matCash = CreateSimpleLitMaterial("Mat_Cash_Bundle", new Color(0.86f, 0.76f, 0.35f), 0.15f);
            Material matWhitePlastic = CreateSimpleLitMaterial("Mat_White_Earbuds", new Color(0.92f, 0.94f, 0.96f), 0.55f);
            Material matMoldDecal = CreateTransparentStainMaterial("Mat_Decal_Mold", $"{ArtRoot}/Textures/Decals/mold_stain_01.png");
            Material matSoupDecal = CreateTransparentStainMaterial("Mat_Decal_Soup", $"{ArtRoot}/Textures/Decals/soup_stain_02.png");
            Material matRedCone = CreateTransparentColorMaterial("Mat_CCTV_VisionCone", new Color(1f, 0.12f, 0.12f, 0.16f));

            // 2. New Empty Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.fogDensity = 0.042f;

            // 3. Room 01 Korean Studio Shell (4.8m x 4.0m x 2.6m)
            GameObject roomRoot = new GameObject("Room01_KoreanStudio");
            float w = 4.8f, d = 4.0f, h = 2.6f;

            CreateBox("Floor", new Vector3(0, -0.05f, 0), new Vector3(w, 0.1f, d), matFloor, roomRoot.transform, true);
            CreateBox("Ceiling", new Vector3(0, h + 0.05f, 0), new Vector3(w, 0.1f, d), matCeiling, roomRoot.transform, true);
            CreateBox("Wall_North", new Vector3(0, h / 2f, d / 2f + 0.05f), new Vector3(w, h, 0.1f), matWall, roomRoot.transform, true);
            CreateBox("Wall_South", new Vector3(0, h / 2f, -d / 2f - 0.05f), new Vector3(w, h, 0.1f), matWall, roomRoot.transform, true);
            CreateBox("Wall_East", new Vector3(w / 2f + 0.05f, h / 2f, 0), new Vector3(0.1f, h, d), matWall, roomRoot.transform, true);
            CreateBox("Wall_West", new Vector3(-w / 2f - 0.05f, h / 2f, 0), new Vector3(0.1f, h, d), matWall, roomRoot.transform, true);

            // Mustard Yellow Studio Door + Fridge + Microwave + Bed + Return Shelf
            CreateBox("Door_Entrance", new Vector3(1.55f, 1.05f, d / 2f - 0.02f), new Vector3(0.9f, 2.1f, 0.06f), matDoor, roomRoot.transform, true);
            CreateBox("Fridge", new Vector3(0.65f, 0.72f, d / 2f - 0.35f), new Vector3(0.62f, 1.44f, 0.62f), matAppliance, roomRoot.transform, true);
            CreateBox("Microwave", new Vector3(0.65f, 1.59f, d / 2f - 0.35f), new Vector3(0.48f, 0.30f, 0.38f), matCeiling, roomRoot.transform, true);
            CreateBox("Mattress", new Vector3(-1.45f, 0.15f, -0.95f), new Vector3(1.45f, 0.30f, 1.90f), matMattress, roomRoot.transform, true);

            // Client Return Shelf (의뢰인 보관 선반)
            GameObject shelf = CreateBox("Client_ReturnShelf", new Vector3(-2.15f, 0.90f, 0.85f), new Vector3(0.45f, 0.08f, 1.20f), matShelf, roomRoot.transform, true);
            GameObject shelfAnchor = new GameObject("ReturnShelfAnchor");
            shelfAnchor.transform.SetParent(shelf.transform, false);
            shelfAnchor.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            // 4. Ceiling Light (Shadows = None -> ZERO URP shadow atlas warnings!)
            GameObject lightObj = new GameObject("Ceiling_Light_WarmFluorescent");
            lightObj.transform.position = new Vector3(0f, h - 0.2f, 0f);
            Light ceilingLight = lightObj.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.color = new Color(1.0f, 0.91f, 0.74f);
            ceilingLight.intensity = 11f;
            ceilingLight.range = 9.0f;
            ceilingLight.shadows = LightShadows.None;

            // 5. Post-Processing Volume
            SetupPostProcessing();

            // 6. Floor Stain Decals (8 stains — cleaned with Slot 2 Mop/Spray)
            GameObject stainRoot = new GameObject("Floor_Stains");
            Vector3[] stainPositions = new[]
            {
                new Vector3(-0.25f, 0.008f, 0.15f),
                new Vector3(0.45f, 0.008f, -0.35f),
                new Vector3(1.15f, 0.009f, 0.55f),
                new Vector3(-0.95f, 0.008f, 0.95f),
                new Vector3(0.15f, 0.009f, 1.15f),
                new Vector3(1.45f, 0.008f, -0.85f),
                new Vector3(-0.35f, 0.009f, -1.10f),
                new Vector3(0.95f, 0.008f, -1.35f),
            };
            for (int i = 0; i < stainPositions.Length; i++)
            {
                bool isMold = i % 2 == 0;
                PlaceCleanableStain(
                    isMold ? $"Stain_Mold_{i + 1:00}" : $"Stain_Soup_{i + 1:00}",
                    isMold ? "바닥 곰팡이 얼룩" : "마른 배달국물 얼룩",
                    stainPositions[i],
                    Random.Range(0.85f, 1.25f),
                    isMold ? matMoldDecal : matSoupDecal,
                    stainRoot.transform
                );
            }

            // 7. Load Real CC0 3D Models
            GameObject trashBagAsset = FindModelAsset($"{ArtRoot}/Models/Trash/trashbag");
            GameObject boxAsset = FindModelAsset($"{ArtRoot}/Models/Trash/cardboard_box_01");
            GameObject bottleAsset = FindModelAsset($"{ArtRoot}/Models/Trash/plastic_bottle_gallon");
            GameObject canAsset = FindModelAsset($"{ArtRoot}/Models/Trash/can_rusted");
            GameObject foodCansAsset = FindModelAsset($"{ArtRoot}/Models/Trash/russian_food_cans_01");
            GameObject cigAsset = FindModelAsset($"{ArtRoot}/Models/Trash/cigarette_pack");
            GameObject cctvAsset = FindModelAsset($"{ArtRoot}/Models/Props/security_camera_01");
            GameObject watchAsset = FindModelAsset($"{ArtRoot}/Models/Valuables/pocket_watch");
            GameObject consoleAsset = FindModelAsset($"{ArtRoot}/Models/Valuables/gaming_console");

            // 8. Build Visible Korean Hoarder Trash Mountain (Auto-normalized sizes!)
            GameObject trashRoot = new GameObject("Trash_Mountain");

            // 16 Large Trash Bag Piles (Normalized to ~0.65m~0.90m height, stacked right in front of Fridge, East wall & center!)
            Vector3[] bagPiles = new[]
            {
                // Right/East wall mountain
                new Vector3(1.85f, 0.0f, 0.65f),
                new Vector3(1.80f, 0.0f, 0.05f),
                new Vector3(1.85f, 0.0f, -0.55f),
                new Vector3(1.75f, 0.0f, -1.15f),
                new Vector3(1.82f, 0.52f, 0.35f),
                new Vector3(1.82f, 0.52f, -0.25f),
                new Vector3(1.85f, 1.02f, 0.05f),
                // Mid-floor spill toward camera
                new Vector3(1.22f, 0.0f, 0.50f),
                new Vector3(1.15f, 0.0f, -0.15f),
                new Vector3(1.18f, 0.0f, -0.78f),
                new Vector3(1.25f, 0.48f, 0.15f),
                // North wall beside fridge
                new Vector3(-0.15f, 0.0f, 1.38f),
                new Vector3(-0.78f, 0.0f, 1.42f),
                new Vector3(-0.45f, 0.48f, 1.45f),
                // Center clutter
                new Vector3(0.45f, 0.0f, 0.45f),
                new Vector3(0.35f, 0.0f, -0.45f),
            };
            for (int i = 0; i < bagPiles.Length; i++)
            {
                float targetSize = Random.Range(0.65f, 0.88f);
                GameObject bag = SpawnNormalizedModel(
                    trashBagAsset, $"TrashBagPile_{i + 1:00}", bagPiles[i],
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    targetSize, trashRoot.transform
                );
                if (bag != null)
                {
                    AddCleanable(bag, CleanableKind.TrashPile, "묶인 쓰레기봉투 더미", 1.8f, 12, 2, 2.5f);
                }
            }

            // 10 Cardboard Boxes (Normalized to ~0.42m~0.55m)
            Vector3[] boxPos = new[]
            {
                new Vector3(0.75f, 0f, 0.15f), new Vector3(-0.15f, 0f, 0.72f),
                new Vector3(0.85f, 0f, -0.95f), new Vector3(-0.55f, 0f, 0.25f),
                new Vector3(0.05f, 0f, -0.85f), new Vector3(1.32f, 0f, 1.05f),
                new Vector3(-1.35f, 0f, 1.15f), new Vector3(0.55f, 0f, 0.95f),
                new Vector3(-0.25f, 0f, -0.25f), new Vector3(1.10f, 0f, -1.42f)
            };
            for (int i = 0; i < boxPos.Length; i++)
            {
                GameObject box = SpawnNormalizedModel(
                    boxAsset, $"CardboardBox_{i + 1:00}", boxPos[i],
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    Random.Range(0.40f, 0.55f), trashRoot.transform
                );
                if (box != null) AddCleanable(box, CleanableKind.SmallTrash, "배달·택배 박스", 0.9f, 8, 1, 1.2f);
            }

            // 24 Small Scattered Trash (Bottles, Cans, Food Cans, Cigarettes)
            GameObject[] smallPool = new[] { bottleAsset, canAsset, foodCansAsset, cigAsset };
            string[] smallNames = new[] { "빈 페트병", "찌그러진 캔", "빈 통조림", "빈 담뱃갑" };
            float[] smallSizes = new[] { 0.30f, 0.16f, 0.22f, 0.14f };
            for (int i = 0; i < 24; i++)
            {
                int k = i % smallPool.Length;
                Vector3 pos = new Vector3(Random.Range(-1.45f, 1.75f), 0f, Random.Range(-1.45f, 1.45f));
                // Avoid spawning right on player spawn (-1.3, -1.35)
                if (pos.x < -0.7f && pos.z < -0.5f) pos.x += 1.4f;
                GameObject prop = SpawnNormalizedModel(
                    smallPool[k], $"SmallTrash_{i + 1:00}", pos,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    smallSizes[k], trashRoot.transform
                );
                if (prop != null) AddCleanable(prop, CleanableKind.SmallTrash, smallNames[k], 0.5f, 5, 1, 0.7f);
            }

            // 9. Stage 1 Hidden Treasures (Total $350) + 1 Designated Lost Item (Gold Ring)
            GameObject treasureRoot = new GameObject("Treasures");

            // Designated Lost Item: 엄마가 주신 금반지 (매트리스 옆 바닥)
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "LostItem_MomGoldRing";
            ring.transform.SetParent(treasureRoot.transform, false);
            ring.transform.position = new Vector3(-0.62f, 0.04f, -0.65f);
            ring.transform.localScale = new Vector3(0.09f, 0.025f, 0.09f);
            ring.GetComponent<Renderer>().sharedMaterial = matGold;
            AddTreasure(ring, "엄마가 주신 금반지", 250, true);

            // Hidden Treasure 1: 빈티지 금시계 ($90) — 냉장고 앞 쓰레기봉투 밑
            GameObject watch = SpawnNormalizedModel(watchAsset, "Treasure_GoldWatch", new Vector3(0.45f, 0.02f, 0.52f), Quaternion.Euler(0, 35f, 0), 0.18f, treasureRoot.transform);
            if (watch != null) AddTreasure(watch, "빈티지 금시계", 90, false);

            // Hidden Treasure 2: 중고 휴대용 게임기 ($90) — 중앙 박스 근처
            GameObject console = SpawnNormalizedModel(consoleAsset, "Treasure_GameConsole", new Vector3(-0.18f, 0.02f, 0.78f), Quaternion.Euler(0, -25f, 0), 0.34f, treasureRoot.transform);
            if (console != null) AddTreasure(console, "중고 게임기", 90, false);

            // Hidden Treasure 3: 5만 원권 지폐 뭉치 ($100) — 동쪽 봉투 산더미 밑
            GameObject cash = CreateBox("Treasure_CashStack", new Vector3(1.38f, 0.04f, 0.12f), new Vector3(0.18f, 0.05f, 0.09f), matCash, treasureRoot.transform, false);
            AddTreasure(cash, "5만 원권 지폐 뭉치", 100, false);

            // Hidden Treasure 4: 무선 이어폰 ($70) — 남동쪽 바닥
            GameObject earbuds = CreateBox("Treasure_Earbuds", new Vector3(1.18f, 0.04f, -0.82f), new Vector3(0.10f, 0.06f, 0.09f), matWhitePlastic, treasureRoot.transform, false);
            AddTreasure(earbuds, "무선 이어폰", 70, false);

            // 10. CCTV (테스트용 1대 — 거품 가리기 & 시야각 목격 검증용)
            SecurityCameraController cctvCtrl = BuildCctv(cctvAsset, new Vector3(-2.15f, 2.28f, 1.78f), matRedCone);

            // 11. Spawn Point + 1st-Person Player + Manager + HUD
            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.position = new Vector3(-1.35f, 0.05f, -1.35f);
            spawnObj.transform.rotation = Quaternion.Euler(0f, 38f, 0f);

            FirstPersonCleanerController playerCtrl = CreateFpsPlayer(spawnObj.transform.position, spawnObj.transform.rotation);

            GameObject sysObj = new GameObject("GamePrototypeManager");
            var mgr = sysObj.AddComponent<GamePrototypeManager>();
            mgr.player = playerCtrl;
            mgr.playerSpawnPoint = spawnObj.transform;
            mgr.returnShelfTransform = shelfAnchor.transform;
            mgr.ceilingLight = ceilingLight;
            mgr.cctv = cctvCtrl;
            playerCtrl.manager = mgr;

            var hud = sysObj.AddComponent<PrototypeHudUI>();
            hud.manager = mgr;
            hud.controller = playerCtrl;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashCleaner3D] Room 01 Playable Prototype 씬 생성 완료! Play(▶) 버튼을 눌러 바로 테스트하세요.");
        }

        private static SecurityCameraController BuildCctv(GameObject cctvAsset, Vector3 pos, Material coneMat)
        {
            GameObject root = new GameObject("CCTV_TestUnit");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(22f, 132f, 0f);

            GameObject pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);

            GameObject model = SpawnNormalizedModel(cctvAsset, "CCTV_Mesh", pos, root.transform.rotation, 0.42f, pivot.transform);
            if (model != null)
            {
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
            }
            var hitCol = pivot.AddComponent<BoxCollider>();
            hitCol.size = new Vector3(0.45f, 0.35f, 0.55f);

            // Visual semi-transparent red scan cone
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "VisionCone";
            cone.transform.SetParent(pivot.transform, false);
            cone.transform.localPosition = new Vector3(0f, 0f, 2.3f);
            cone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cone.transform.localScale = new Vector3(1.6f, 2.3f, 1.6f);
            Object.DestroyImmediate(cone.GetComponent<Collider>());
            cone.GetComponent<Renderer>().sharedMaterial = coneMat;

            var ctrl = root.AddComponent<SecurityCameraController>();
            ctrl.pivotTransform = pivot.transform;
            ctrl.visionConeRenderer = cone.GetComponent<Renderer>();
            return ctrl;
        }

        private static FirstPersonCleanerController CreateFpsPlayer(Vector3 startPos, Quaternion startRot)
        {
            GameObject player = new GameObject("Player_FPS");
            player.transform.position = startPos;
            player.transform.rotation = startRot;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.65f;
            cc.radius = 0.26f;
            cc.center = new Vector3(0, 0.825f, 0);

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.50f, 0);
            var cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.fieldOfView = 70f;
            camObj.AddComponent<AudioListener>();
            var urpCam = camObj.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;

            // ONLY real-time shadow light (1 shadow map -> zero URP warnings!)
            GameObject flashObj = new GameObject("Flashlight");
            flashObj.transform.SetParent(camObj.transform, false);
            var flash = flashObj.AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.intensity = 15f;
            flash.range = 10f;
            flash.spotAngle = 62f;
            flash.color = new Color(1f, 0.98f, 0.92f);
            flash.shadows = LightShadows.Soft;
            flash.enabled = true;

            // Simple 1st-person held tool visual (bottom right)
            GameObject toolHolder = new GameObject("HeldTool");
            toolHolder.transform.SetParent(camObj.transform, false);
            toolHolder.transform.localPosition = new Vector3(0.32f, -0.26f, 0.55f);
            GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nozzle.name = "ToolNozzle";
            nozzle.transform.SetParent(toolHolder.transform, false);
            nozzle.transform.localRotation = Quaternion.Euler(78f, -10f, 0f);
            nozzle.transform.localScale = new Vector3(0.08f, 0.24f, 0.08f);
            Object.DestroyImmediate(nozzle.GetComponent<Collider>());
            nozzle.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

            var ctrl = player.AddComponent<FirstPersonCleanerController>();
            ctrl.cameraTransform = camObj.transform;
            ctrl.toolHoldPoint = toolHolder.transform;
            ctrl.flashlight = flash;
            return ctrl;
        }

        private static void AddCleanable(GameObject go, CleanableKind kind, string name, float hp, int reward, int bagCost, float weight)
        {
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                Bounds b = CalculateLocalBounds(go);
                box.center = b.center;
                box.size = Vector3.Max(b.size, Vector3.one * 0.25f);
            }
            var c = go.AddComponent<CleanableTarget>();
            c.kind = kind;
            c.displayName = name;
            c.maxHp = hp;
            c.rewardMoney = reward;
            c.bagCost = bagCost;
            c.cleanWeight = weight;
        }

        private static void AddTreasure(GameObject go, string name, int value, bool isDesignated)
        {
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                Bounds b = CalculateLocalBounds(go);
                box.center = b.center;
                box.size = Vector3.Max(b.size, Vector3.one * 0.22f);
            }
            var t = go.AddComponent<TreasureTarget>();
            t.itemName = name;
            t.value = value;
            t.isDesignatedLostItem = isDesignated;
        }

        private static void PlaceCleanableStain(string name, string displayName, Vector3 pos, float size, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // Make collider slightly thicker so raycast always hits cleanly
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1f, 0.12f);

            var c = go.AddComponent<CleanableTarget>();
            c.kind = CleanableKind.FloorStain;
            c.displayName = displayName;
            c.maxHp = 1.6f;
            c.rewardMoney = 15;
            c.bagCost = 0;
            c.cleanWeight = 1.5f;
        }

        private static Bounds CalculateLocalBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.3f);
            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);
            Vector3 localCenter = go.transform.InverseTransformPoint(world.center);
            Vector3 s = go.transform.lossyScale;
            Vector3 localSize = new Vector3(
                s.x > 0.0001f ? world.size.x / s.x : world.size.x,
                s.y > 0.0001f ? world.size.y / s.y : world.size.y,
                s.z > 0.0001f ? world.size.z / s.z : world.size.z
            );
            return new Bounds(localCenter, localSize);
        }

        /// <summary>
        /// Poly Haven FBX 모델의 실제 바운딩 크기를 측정해 목표 미터(targetMaxDim)로 정확히 정규화하고
        /// 바닥(pos.y)에 딱 맞게 안착 + URP PBR 머티리얼 자동 연결.
        /// </summary>
        private static GameObject SpawnNormalizedModel(GameObject prefab, string name, Vector3 pos, Quaternion rot, float targetMaxDim, Transform parent)
        {
            if (prefab == null) return null;
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.SetParent(parent, false);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // Bind URP PBR Material from sibling textures folder
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            string modelFolder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(modelFolder))
            {
                string texFolder = $"{modelFolder}/textures";
                if (Directory.Exists(texFolder))
                {
                    string folderKey = Path.GetFileName(modelFolder);
                    Material propMat = CreateOrUpdatePbrMaterial($"Mat_Prop_{folderKey}", texFolder, Vector2.one, Color.white, 0.26f);
                    foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = propMat;
                        r.sharedMaterials = mats;
                    }
                }
            }

            // Normalize scale so models are never tiny or huge
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.0001f)
                {
                    float scaleFactor = targetMaxDim / maxDim;
                    inst.transform.localScale = Vector3.one * scaleFactor;
                }
                // Recompute bottom offset to rest cleanly on floor
                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float liftY = -b.min.y;
                inst.transform.position = new Vector3(pos.x, pos.y + liftY, pos.z);
                inst.transform.rotation = rot;
            }
            else
            {
                inst.transform.position = pos;
                inst.transform.rotation = rot;
            }

            return inst;
        }

        private static void SetupPostProcessing()
        {
            GameObject volObj = new GameObject("Global_PostProcess_Volume");
            var volume = volObj.AddComponent<Volume>();
            volume.isGlobal = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string profilePath = $"{MatRoot}/Room01_VolumeProfile.asset";
            AssetDatabase.CreateAsset(profile, profilePath);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.25f);
            colorAdj.contrast.Override(10f);
            colorAdj.saturation.Override(-6f);
            colorAdj.colorFilter.Override(new Color(1.0f, 0.98f, 0.92f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.42f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.25f);

            volume.sharedProfile = profile;
        }

        private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.isStatic = isStatic;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject FindModelAsset(string folder)
        {
            if (!Directory.Exists(folder)) return null;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static Material CreateSimpleLitMaterial(string matName, Color color, float smoothness)
        {
            string path = $"{MatRoot}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateTransparentColorMaterial(string matName, Color color)
        {
            string path = $"{MatRoot}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdatePbrMaterial(string matName, string texFolder, Vector2 tiling, Color tint, float smoothness)
        {
            string path = $"{MatRoot}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            if (Directory.Exists(texFolder))
            {
                string[] files = Directory.GetFiles(texFolder).Where(f => !f.EndsWith(".meta")).ToArray();
                Texture2D diff = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("diff") || f.ToLower().Contains("color")), false);
                Texture2D norm = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("nor_gl") || f.ToLower().Contains("normalgl")), true);
                Texture2D ao = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("_ao_") || f.ToLower().Contains("ambientocclusion")), false);

                if (diff != null) mat.SetTexture("_BaseMap", diff);
                if (norm != null)
                {
                    mat.SetTexture("_BumpMap", norm);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetFloat("_BumpScale", 0.9f);
                }
                if (ao != null)
                {
                    mat.SetTexture("_OcclusionMap", ao);
                    mat.SetFloat("_OcclusionStrength", 0.65f);
                }
            }

            mat.SetColor("_BaseColor", tint);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateTransparentStainMaterial(string matName, string pngPath)
        {
            string path = $"{MatRoot}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (tex != null) mat.SetTexture("_BaseMap", tex);

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D LoadTex(string fullPath, bool isNormal)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            string assetPath = fullPath.Replace("\\", "/");
            if (isNormal)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
