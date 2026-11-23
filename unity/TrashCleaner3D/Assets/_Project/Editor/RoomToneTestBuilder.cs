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
    /// TrashCleaner3D v0.2 원클릭 프로토타입 씬 빌더:
    /// - 열린 현관문 + 바로 밖 쓰레기 트럭 짐칸(농구 슛 골대!)
    /// - 1단계: 단단한 큰 봉투 10개(주먹 4방 퍽! 터뜨리기 → 작은 쓰레기+보물 가챠!) + 작은 쓰레기 14개
    /// - 2단계: 큼직한 바닥 얼룩 6개(물티슈 → 대걸레, 마우스 흔들어 문지르기)
    /// - 턴 종료 후 마인드맵 스킬트리(12노드) + 3.5초 좌/우 본능 슈킹 팝업
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

            // 1. URP Materials
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

            Material matOutdoorFloor = CreateSimpleLitMaterial("Mat_Outdoor_Asphalt", new Color(0.26f, 0.27f, 0.29f), 0.15f);
            Material matCeiling = CreateSimpleLitMaterial("Mat_Ceiling_Ivory", new Color(0.86f, 0.84f, 0.78f), 0.1f);
            Material matDoor = CreateSimpleLitMaterial("Mat_Door_MustardYellow", new Color(0.78f, 0.62f, 0.16f), 0.25f);
            Material matAppliance = CreateSimpleLitMaterial("Mat_Fridge_Silver", new Color(0.66f, 0.68f, 0.72f), 0.45f);
            Material matMattress = CreateSimpleLitMaterial("Mat_Mattress_BeigeBrown", new Color(0.50f, 0.44f, 0.38f), 0.08f);
            Material matTruckBody = CreateSimpleLitMaterial("Mat_Truck_Green", new Color(0.18f, 0.48f, 0.30f), 0.32f);
            Material matTruckGlow = CreateSimpleLitMaterial("Mat_Truck_GoldRim", new Color(0.98f, 0.82f, 0.20f), 0.5f);
            Material matBagBlack = CreateSimpleLitMaterial("Mat_HandBag_Black", new Color(0.16f, 0.17f, 0.19f), 0.35f);
            Material matSkin = CreateSimpleLitMaterial("Mat_Hand_Skin", new Color(0.92f, 0.75f, 0.62f), 0.2f);
            Material matTissue = CreateSimpleLitMaterial("Mat_Tissue_White", new Color(0.96f, 0.96f, 0.98f), 0.08f);
            Material matMopStick = CreateSimpleLitMaterial("Mat_Tool_MopStick", new Color(0.24f, 0.62f, 0.88f), 0.35f);
            Material matMopHead = CreateSimpleLitMaterial("Mat_Tool_MopHead", new Color(0.92f, 0.94f, 0.96f), 0.1f);
            Material matMoldDecal = CreateTransparentStainMaterial("Mat_Decal_Mold", $"{ArtRoot}/Textures/Decals/mold_stain_01.png");
            Material matSoupDecal = CreateTransparentStainMaterial("Mat_Decal_Soup", $"{ArtRoot}/Textures/Decals/soup_stain_02.png");

            // 2. Empty Scene & Mood
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.23f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.fogDensity = 0.038f;

            // 3. Room 01 Shell (4.8m x 4.0m x 2.6m) with OPEN DOORWAY on North-East (x: 0.95 ~ 2.05)
            GameObject roomRoot = new GameObject("Room01_KoreanStudio");
            float w = 4.8f, d = 4.0f, h = 2.6f;

            CreateBox("Floor", new Vector3(0, -0.30f, 0), new Vector3(w + 0.4f, 0.6f, d + 0.4f), matFloor, roomRoot.transform, true);
            CreateBox("Ceiling", new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), matCeiling, roomRoot.transform, true);

            // North Wall split around Open Doorway (x: 0.95 to 2.05, height: 0 to 2.15)
            CreateBox("Wall_North_Left", new Vector3(-0.725f, h / 2f, d / 2f + 0.15f), new Vector3(3.35f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateBox("Wall_North_Right", new Vector3(2.275f, h / 2f, d / 2f + 0.15f), new Vector3(0.45f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateBox("Wall_North_Header", new Vector3(1.50f, 2.375f, d / 2f + 0.15f), new Vector3(1.15f, 0.45f, 0.3f), matWall, roomRoot.transform, true);

            // Swung-Open Mustard Yellow Door beside doorway
            GameObject openDoor = CreateBox("Door_SwungOpen", new Vector3(2.08f, 1.05f, d / 2f + 0.55f), new Vector3(0.08f, 2.1f, 0.92f), matDoor, roomRoot.transform, true);
            openDoor.transform.rotation = Quaternion.Euler(0f, -14f, 0f);

            CreateBox("Wall_South", new Vector3(0, h / 2f, -d / 2f - 0.15f), new Vector3(w + 0.4f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateBox("Wall_East", new Vector3(w / 2f + 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);
            CreateBox("Wall_West", new Vector3(-w / 2f - 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);

            // Basic Korean Studio Furniture
            CreateBox("Fridge", new Vector3(0.35f, 0.72f, d / 2f - 0.35f), new Vector3(0.62f, 1.44f, 0.62f), matAppliance, roomRoot.transform, true);
            CreateBox("Microwave", new Vector3(0.35f, 1.59f, d / 2f - 0.35f), new Vector3(0.48f, 0.30f, 0.38f), matCeiling, roomRoot.transform, true);
            CreateBox("Mattress", new Vector3(-1.45f, 0.15f, -0.95f), new Vector3(1.45f, 0.30f, 1.90f), matMattress, roomRoot.transform, true);

            // 4. Outside Parking Area + Trash Truck Bed (Basketball Shoot Goal right outside the open door!)
            GameObject outsideRoot = new GameObject("Outside_Truck_Area");
            CreateBox("Parking_Floor", new Vector3(1.5f, -0.30f, 4.3f), new Vector3(5.6f, 0.6f, 4.6f), matOutdoorFloor, outsideRoot.transform, true);
            // Invisible safety boundary at doorway threshold so player can shoot from room cleanly without wandering off map
            CreateBox("Door_StepCurb", new Vector3(1.5f, 0.04f, 2.05f), new Vector3(1.1f, 0.08f, 0.1f), matCeiling, outsideRoot.transform, true);

            GameObject truckRoot = new GameObject("TrashTruck_Goal");
            truckRoot.transform.SetParent(outsideRoot.transform, false);
            truckRoot.transform.position = new Vector3(1.50f, 0.0f, 4.35f);

            // Truck Cargo Bed (open-top box container — scales up when LvTruckSize upgraded!)
            GameObject truckBedVisual = new GameObject("TruckBed_Scalable");
            truckBedVisual.transform.SetParent(truckRoot.transform, false);
            truckBedVisual.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            truckBedVisual.transform.localScale = new Vector3(1.8f, 0.85f, 1.5f);

            // Bed base + 4 side walls (open top so bags visibly land inside!)
            CreateBox("Bed_Bottom", new Vector3(0f, -0.42f, 0f), new Vector3(1f, 0.14f, 1f), matTruckBody, truckBedVisual.transform, false);
            CreateBox("Bed_BackWall", new Vector3(0f, 0f, 0.48f), new Vector3(1f, 1f, 0.08f), matTruckBody, truckBedVisual.transform, false);
            CreateBox("Bed_FrontLip", new Vector3(0f, -0.18f, -0.48f), new Vector3(1f, 0.55f, 0.08f), matTruckGlow, truckBedVisual.transform, false);
            CreateBox("Bed_LeftWall", new Vector3(-0.48f, 0f, 0f), new Vector3(0.08f, 1f, 1f), matTruckBody, truckBedVisual.transform, false);
            CreateBox("Bed_RightWall", new Vector3(0.48f, 0f, 0f), new Vector3(0.08f, 1f, 1f), matTruckBody, truckBedVisual.transform, false);

            // Bright spotlight illuminating truck bed so player immediately sees the basketball target!
            GameObject truckLightObj = new GameObject("Truck_BeaconLight");
            truckLightObj.transform.SetParent(truckRoot.transform, false);
            truckLightObj.transform.localPosition = new Vector3(0f, 2.4f, -0.2f);
            Light truckLight = truckLightObj.AddComponent<Light>();
            truckLight.type = LightType.Point;
            truckLight.color = new Color(1f, 0.90f, 0.50f);
            truckLight.intensity = 10f;
            truckLight.range = 6.5f;
            truckLight.shadows = LightShadows.None;

            GameObject goalAnchor = new GameObject("GoalCenterAnchor");
            goalAnchor.transform.SetParent(truckRoot.transform, false);
            goalAnchor.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            // 5. Ceiling Light (No real-time shadows -> zero URP warnings!)
            GameObject lightObj = new GameObject("Ceiling_Light_WarmFluorescent");
            lightObj.transform.position = new Vector3(0f, h - 0.2f, 0f);
            Light ceilingLight = lightObj.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.color = new Color(1.0f, 0.91f, 0.74f);
            ceilingLight.intensity = 11f;
            ceilingLight.range = 9.0f;
            ceilingLight.shadows = LightShadows.None;

            SetupPostProcessing();

            // 6. Phase 2 Floor Stains (6 big glossy stains — unlocked after Phase 1 100%)
            GameObject stainRoot = new GameObject("Phase2_FloorStains");
            Vector3[] stainPositions = new[]
            {
                new Vector3(-0.15f, 0.008f, 0.20f),
                new Vector3(0.85f, 0.008f, -0.35f),
                new Vector3(1.45f, 0.009f, 0.65f),
                new Vector3(-0.95f, 0.008f, 0.95f),
                new Vector3(0.25f, 0.009f, -1.05f),
                new Vector3(1.25f, 0.008f, -1.25f),
            };
            for (int i = 0; i < stainPositions.Length; i++)
            {
                bool isMold = i % 2 == 0;
                PlaceCleanableStain(
                    isMold ? $"Stain_Mold_{i + 1:00}" : $"Stain_Soup_{i + 1:00}",
                    stainPositions[i],
                    Random.Range(1.05f, 1.35f),
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

            // 8. Phase 1 Trash Mountain (10 sturdy breakable piles + 14 loose small trash)
            GameObject trashRoot = new GameObject("Phase1_TrashMountain");

            Vector3[] pilePositions = new[]
            {
                new Vector3(1.80f, 0f, 0.35f),
                new Vector3(1.75f, 0f, -0.35f),
                new Vector3(1.70f, 0f, -1.05f),
                new Vector3(1.15f, 0f, 0.20f),
                new Vector3(1.10f, 0f, -0.55f),
                new Vector3(0.42f, 0f, 0.55f),
                new Vector3(-0.35f, 0f, 1.25f),
                new Vector3(-1.10f, 0f, 1.20f),
                new Vector3(-0.25f, 0f, 0.35f),
                new Vector3(0.45f, 0f, -0.55f),
            };
            for (int i = 0; i < pilePositions.Length; i++)
            {
                bool isBox = i % 3 == 2 && boxAsset != null;
                GameObject src = isBox ? boxAsset : trashBagAsset;
                float size = isBox ? Random.Range(0.55f, 0.68f) : Random.Range(0.70f, 0.88f);
                GameObject pile = SpawnNormalizedModel(
                    src, isBox ? $"BreakableBox_{i + 1:00}" : $"BreakableBag_{i + 1:00}",
                    pilePositions[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    size, trashRoot.transform
                );
                if (pile != null)
                {
                    // 초반 주먹 데미지 1.0 기준 4방 퍽!퍽!퍽!퍽! 터뜨리기!
                    var ct = AddCleanable(pile, CleanableKind.TrashPile, isBox ? "단단한 택배박스" : "묵직한 쓰레기봉투", 4.0f, 10, 0, 2.0f);
                    ct.burstSpawnCount = 2;
                    ct.containsTreasureChance = true;
                }
            }

            // 14 Initial Loose Small Trash
            GameObject[] smallPool = new[] { bottleAsset, canAsset, foodCansAsset, cigAsset };
            float[] smallSizes = new[] { 0.32f, 0.18f, 0.24f, 0.16f };
            for (int i = 0; i < 14; i++)
            {
                int k = i % smallPool.Length;
                Vector3 pos = new Vector3(Random.Range(-1.35f, 1.65f), 0f, Random.Range(-1.30f, 1.35f));
                if (pos.x < -0.65f && pos.z < 0.05f) pos.x += 1.5f;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-0.15f, -1.55f)) < 0.75f) pos.z += 0.9f;
                GameObject prop = SpawnNormalizedModel(
                    smallPool[k], $"SmallTrash_{i + 1:00}", pos,
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    smallSizes[k], trashRoot.transform
                );
                if (prop != null) AddCleanable(prop, CleanableKind.SmallTrash, "소형 쓰레기", 1.0f, 5, 1, 1.0f);
            }

            // 9. Hidden Runtime Spawn Templates (Burst Small Trash + Thrown Bag)
            GameObject templateRoot = new GameObject("_RuntimeTemplates");
            GameObject[] burstTemplates = new GameObject[3];
            for (int k = 0; k < 3; k++)
            {
                GameObject tmpl = SpawnNormalizedModel(smallPool[k], $"Template_Burst_{k}", Vector3.zero, Quaternion.identity, smallSizes[k], templateRoot.transform);
                if (tmpl != null)
                {
                    AddCleanable(tmpl, CleanableKind.SmallTrash, "튀어나온 쓰레기", 1.0f, 6, 1, 1.0f);
                    tmpl.SetActive(false);
                    burstTemplates[k] = tmpl;
                }
            }

            GameObject thrownBagTmpl = SpawnNormalizedModel(trashBagAsset, "Template_ThrownBag", Vector3.zero, Quaternion.identity, 0.42f, templateRoot.transform);
            if (thrownBagTmpl == null)
            {
                thrownBagTmpl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                thrownBagTmpl.name = "Template_ThrownBag";
                thrownBagTmpl.transform.SetParent(templateRoot.transform, false);
                thrownBagTmpl.transform.localScale = Vector3.one * 0.42f;
                thrownBagTmpl.GetComponent<Renderer>().sharedMaterial = matBagBlack;
            }
            thrownBagTmpl.SetActive(false);

            GameObject dynamicParent = new GameObject("Dynamic_BurstTrash");

            // 10. Player Spawn (Clear open spot looking toward trash & open door truck!)
            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.position = new Vector3(-0.15f, 0.15f, -1.55f);
            spawnObj.transform.rotation = Quaternion.Euler(0f, 22f, 0f);

            FirstPersonCleanerController playerCtrl = CreateFpsPlayer(
                spawnObj.transform.position, spawnObj.transform.rotation,
                matSkin, matBagBlack, matTissue, matMopStick, matMopHead
            );

            // 11. Manager & HUD
            GameObject sysObj = new GameObject("GamePrototypeManager");
            var mgr = sysObj.AddComponent<GamePrototypeManager>();
            mgr.player = playerCtrl;
            mgr.playerSpawnPoint = spawnObj.transform;
            mgr.truckBedGoal = goalAnchor.transform;
            mgr.truckVisualTransform = truckBedVisual.transform;
            mgr.ceilingLight = ceilingLight;
            mgr.burstSmallTrashPrefabs = burstTemplates;
            mgr.thrownBagPrefab = thrownBagTmpl;
            mgr.dynamicTrashParent = dynamicParent.transform;
            playerCtrl.manager = mgr;

            var hud = sysObj.AddComponent<PrototypeHudUI>();
            hud.manager = mgr;
            hud.controller = playerCtrl;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashCleaner3D v0.2] Room 01 Playable Prototype 생성 완료! Play(▶)를 눌러 주먹 펀치→봉투 슛→가챠 슈킹→마인드맵 강화를 테스트하세요!");
        }

        private static FirstPersonCleanerController CreateFpsPlayer(
            Vector3 startPos, Quaternion startRot,
            Material matSkin, Material matBag, Material matTissue, Material matMopStick, Material matMopHead)
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

            // Single shadow-casting Flashlight
            GameObject flashObj = new GameObject("Flashlight");
            flashObj.transform.SetParent(camObj.transform, false);
            var flash = flashObj.AddComponent<Light>();
            flash.type = LightType.Spot;
            flash.intensity = 14f;
            flash.range = 10f;
            flash.spotAngle = 64f;
            flash.color = new Color(1f, 0.98f, 0.92f);
            flash.shadows = LightShadows.Soft;
            flash.enabled = true;

            // RIGHT HAND PIVOT (Fist / Tissue / Mop — distinctly animated!)
            GameObject rightPivot = new GameObject("RightHandPivot");
            rightPivot.transform.SetParent(camObj.transform, false);
            rightPivot.transform.localPosition = new Vector3(0.32f, -0.26f, 0.52f);

            // 1) 맨손 주먹 (Phase 1)
            GameObject fist = new GameObject("Visual_FistHand");
            fist.transform.SetParent(rightPivot.transform, false);
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.transform.SetParent(fist.transform, false);
            forearm.transform.localPosition = new Vector3(0.03f, -0.05f, -0.10f);
            forearm.transform.localRotation = Quaternion.Euler(78f, -12f, 0f);
            forearm.transform.localScale = new Vector3(0.085f, 0.18f, 0.085f);
            Object.DestroyImmediate(forearm.GetComponent<Collider>());
            forearm.GetComponent<Renderer>().sharedMaterial = matSkin;

            GameObject knuckles = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knuckles.transform.SetParent(fist.transform, false);
            knuckles.transform.localPosition = new Vector3(0f, 0.01f, 0.08f);
            knuckles.transform.localScale = new Vector3(0.11f, 0.10f, 0.11f);
            Object.DestroyImmediate(knuckles.GetComponent<Collider>());
            knuckles.GetComponent<Renderer>().sharedMaterial = matSkin;

            // 2) 물티슈 한 장 (Phase 2 초반 — 맨손 문지르기)
            GameObject tissue = new GameObject("Visual_WetTissue");
            tissue.transform.SetParent(rightPivot.transform, false);
            GameObject tHand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tHand.transform.SetParent(tissue.transform, false);
            tHand.transform.localPosition = new Vector3(0f, 0.02f, 0.06f);
            tHand.transform.localScale = new Vector3(0.10f, 0.07f, 0.10f);
            Object.DestroyImmediate(tHand.GetComponent<Collider>());
            tHand.GetComponent<Renderer>().sharedMaterial = matSkin;

            GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.transform.SetParent(tissue.transform, false);
            cloth.transform.localPosition = new Vector3(0f, -0.02f, 0.09f);
            cloth.transform.localRotation = Quaternion.Euler(10f, 18f, 0f);
            cloth.transform.localScale = new Vector3(0.16f, 0.02f, 0.16f);
            Object.DestroyImmediate(cloth.GetComponent<Collider>());
            cloth.GetComponent<Renderer>().sharedMaterial = matTissue;
            tissue.SetActive(false);

            // 3) T자 대걸레 (마인드맵 구매 후)
            GameObject mop = new GameObject("Visual_Mop");
            mop.transform.SetParent(rightPivot.transform, false);
            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.transform.SetParent(mop.transform, false);
            stick.transform.localPosition = new Vector3(0f, -0.05f, 0.12f);
            stick.transform.localRotation = Quaternion.Euler(52f, 0f, 0f);
            stick.transform.localScale = new Vector3(0.035f, 0.42f, 0.035f);
            Object.DestroyImmediate(stick.GetComponent<Collider>());
            stick.GetComponent<Renderer>().sharedMaterial = matMopStick;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(mop.transform, false);
            head.transform.localPosition = new Vector3(0f, -0.28f, 0.44f);
            head.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            head.transform.localScale = new Vector3(0.36f, 0.06f, 0.14f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = matMopHead;
            mop.SetActive(false);

            // LEFT HAND PIVOT (Growing Garbage Bag!)
            GameObject leftBag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftBag.name = "LeftHand_GarbageBag";
            leftBag.transform.SetParent(camObj.transform, false);
            leftBag.transform.localPosition = new Vector3(-0.34f, -0.28f, 0.56f);
            leftBag.transform.localScale = Vector3.one * 0.15f;
            Object.DestroyImmediate(leftBag.GetComponent<Collider>());
            leftBag.GetComponent<Renderer>().sharedMaterial = matBag;
            leftBag.SetActive(false);

            var ctrl = player.AddComponent<FirstPersonCleanerController>();
            ctrl.cameraTransform = camObj.transform;
            ctrl.rightToolPivot = rightPivot.transform;
            ctrl.leftBagPivot = leftBag.transform;
            ctrl.fistHandVisual = fist;
            ctrl.tissueVisual = tissue;
            ctrl.mopVisual = mop;
            ctrl.flashlight = flash;
            return ctrl;
        }

        private static CleanableTarget AddCleanable(GameObject go, CleanableKind kind, string name, float hp, int reward, int bagCost, float weight)
        {
            if (go.GetComponent<Collider>() == null)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one * 0.35f;
            }
            var c = go.AddComponent<CleanableTarget>();
            c.kind = kind;
            c.displayName = name;
            c.maxHp = hp;
            c.rewardMoney = reward;
            c.bagCost = bagCost;
            c.cleanWeight = weight;
            return c;
        }

        private static void PlaceCleanableStain(string name, Vector3 pos, float size, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 1f, 0.02f);

            var c = go.AddComponent<CleanableTarget>();
            c.kind = CleanableKind.FloorStain;
            c.displayName = "바닥 오염";
            c.maxHp = 2.4f;
            c.rewardMoney = 18;
            c.bagCost = 0;
            c.cleanWeight = 1.5f;
        }

        private static GameObject SpawnNormalizedModel(GameObject prefab, string name, Vector3 pos, Quaternion rot, float targetMaxDim, Transform parent)
        {
            if (prefab == null) return null;

            GameObject wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = Vector3.zero;
            wrapper.transform.rotation = Quaternion.identity;
            wrapper.transform.localScale = Vector3.one;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (PrefabUtility.IsPartOfPrefabInstance(inst))
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            inst.name = "Visual";
            inst.transform.SetParent(wrapper.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            foreach (var oldCol in inst.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(oldCol);
            }

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

            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.0001f)
                {
                    inst.transform.localScale = Vector3.one * (targetMaxDim / maxDim);
                }

                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

                var box = wrapper.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, b.size.y * 0.5f, 0f);
                box.size = Vector3.Max(b.size, Vector3.one * 0.24f);
            }

            wrapper.transform.position = pos;
            wrapper.transform.rotation = rot;
            return wrapper;
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
            vignette.intensity.Override(0.22f);
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
            mat.SetFloat("_Smoothness", 0.58f);
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
