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
    /// TrashCleaner3D v0.3 원클릭 프로토타입 씬 빌더:
    /// - 1,000+ 3D 쓰레기를 GPU Instancing(드라이콜 5개 미만, 물리 0)으로 배치해 렉 0% 광역 훑기 구현!
    /// - 열린 현관문 + 밖 환기 구역(z > 2.0, 악취 회복) + 쓰레기 트럭 짐칸(로컬 좌표 버그 수정!)
    /// - 흰색/회색/검정 종량제 스타일 큰 봉투 & 택배/스티로폼 박스 12개 (터뜨리면 소형 쓰레기 25개 분수 + 보물 발굴!)
    /// - 2단계 바닥 얼룩 6개 (물티슈 → 대걸레) + 진공청소기 1인칭 비주얼 포함
    /// 메뉴: TrashCleaner > Build Room 01 Playable Prototype
    /// </summary>
    [InitializeOnLoad]
    public static class RoomToneTestBuilder
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string AptKitRoot = "Assets/Brick Project Studio/Apartment Kit/_Prefabs";
        private const string MatRoot = "Assets/_Project/Materials";
        private const string ScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";
        private const int SceneBuildVersion = 42;
        private const string SceneVerPrefKey = "TC3D_BuiltSceneVersion";

        static RoomToneTestBuilder()
        {
            if (Application.isBatchMode) return;
            if (EditorPrefs.GetInt(SceneVerPrefKey, 0) >= SceneBuildVersion) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged += OnPlayModeChangedForAutoBuild;
            }
            else
            {
                EditorApplication.delayCall += TryAutoRebuildOnce;
            }
        }

        private static void OnPlayModeChangedForAutoBuild(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChangedForAutoBuild;
                EditorApplication.delayCall += TryAutoRebuildOnce;
            }
        }

        private static void TryAutoRebuildOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetInt(SceneVerPrefKey, 0) >= SceneBuildVersion) return;
            BuildRoom01();
        }

        [MenuItem("TrashCleaner/Build Room 01 Playable Prototype")]
        public static void BuildRoom01()
        {
            EditorPrefs.SetInt(SceneVerPrefKey, SceneBuildVersion);
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
            Material matTruckBody = CreateSimpleLitMaterial("Mat_Truck_Green", new Color(0.18f, 0.50f, 0.32f), 0.32f);
            Material matTruckGlow = CreateSimpleLitMaterial("Mat_Truck_GoldRim", new Color(0.98f, 0.82f, 0.20f), 0.5f);
            Material matBagBlack = CreateSimpleLitMaterial("Mat_HandBag_Black", new Color(0.16f, 0.17f, 0.19f), 0.35f);
            Material matSkin = CreateSimpleLitMaterial("Mat_Hand_Skin", new Color(0.92f, 0.75f, 0.62f), 0.2f);
            Material matTissue = CreateSimpleLitMaterial("Mat_Tissue_White", new Color(0.96f, 0.96f, 0.98f), 0.08f);
            Material matMopStick = CreateSimpleLitMaterial("Mat_Tool_MopStick", new Color(0.24f, 0.62f, 0.88f), 0.35f);
            Material matMopHead = CreateSimpleLitMaterial("Mat_Tool_MopHead", new Color(0.92f, 0.94f, 0.96f), 0.1f);
            Material matVacuumBody = CreateSimpleLitMaterial("Mat_Tool_VacuumRed", new Color(0.86f, 0.24f, 0.20f), 0.48f);
            Material matVacuumMetal = CreateSimpleLitMaterial("Mat_Tool_VacuumChrome", new Color(0.78f, 0.82f, 0.86f), 0.72f);
            Material matMoldDecal = CreateTransparentStainMaterial("Mat_Decal_Mold", $"{ArtRoot}/Textures/Decals/mold_stain_01.png");
            Material matSoupDecal = CreateTransparentStainMaterial("Mat_Decal_Soup", $"{ArtRoot}/Textures/Decals/soup_stain_02.png");

            // 2. Empty Scene & Mood
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.26f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.fogDensity = 0.032f;

            // 3. Room 01 Shell (+50% 확장: 7.2m x 6.0m x 2.8m) with Doorway at x: 1.55 ~ 2.65
            GameObject roomRoot = new GameObject("Room01_KoreanStudio");
            float w = 7.2f, d = 6.0f, h = 2.8f;

            CreateWorldBox("Floor", new Vector3(0, -0.30f, 0), new Vector3(w + 0.4f, 0.6f, d + 0.4f), matFloor, roomRoot.transform, true);
            CreateWorldBox("Ceiling", new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), matCeiling, roomRoot.transform, true);

            // North Wall split around Doorway (x: 1.55 to 2.65, height: 0 to 2.15)
            CreateWorldBox("Wall_North_Left", new Vector3(-1.125f, h / 2f, d / 2f + 0.15f), new Vector3(5.35f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_North_Right", new Vector3(3.225f, h / 2f, d / 2f + 0.15f), new Vector3(1.15f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_North_Header", new Vector3(2.10f, 2.475f, d / 2f + 0.15f), new Vector3(1.15f, 0.65f, 0.3f), matWall, roomRoot.transform, true);

            // Interactive Closed Mustard Yellow Entrance Door
            Material matDoorHandle = CreateSimpleLitMaterial("Mat_Door_HandleMetal", new Color(0.18f, 0.19f, 0.22f), 0.72f);
            GameObject doorHinge = new GameObject("Door_Interactive_Hinge");
            doorHinge.transform.SetParent(roomRoot.transform, false);
            doorHinge.transform.position = new Vector3(2.64f, 0f, 3.02f);

            GameObject doorLeaf = CreateLocalBox("Door_Leaf", new Vector3(-0.54f, 1.075f, 0f), new Vector3(1.08f, 2.15f, 0.08f), matDoor, doorHinge.transform, false);
            GameObject handleOut = CreateLocalBox("Door_Handle_Out", new Vector3(-0.92f, 1.02f, 0.07f), new Vector3(0.14f, 0.04f, 0.07f), matDoorHandle, doorHinge.transform, false);
            Object.DestroyImmediate(handleOut.GetComponent<Collider>());
            GameObject handleIn = CreateLocalBox("Door_Handle_In", new Vector3(-0.92f, 1.02f, -0.07f), new Vector3(0.14f, 0.04f, 0.07f), matDoorHandle, doorHinge.transform, false);
            Object.DestroyImmediate(handleIn.GetComponent<Collider>());

            var doorCtrl = doorHinge.AddComponent<InteractiveDoor>();
            doorCtrl.hingePivot = doorHinge.transform;
            doorCtrl.doorCollider = doorLeaf.GetComponent<Collider>();
            doorCtrl.closedAngleY = 0f;
            doorCtrl.openAngleY = -98f;

            CreateWorldBox("Wall_South", new Vector3(0, h / 2f, -d / 2f - 0.15f), new Vector3(w + 0.4f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_East", new Vector3(w / 2f + 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_West", new Vector3(-w / 2f - 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);

            // 3.5. Brick Project Studio Apartment Kit 실제 3D 원룸 인테리어 가구 & 소품 배치!
            GameObject furnRoot = new GameObject("ApartmentKit_InteriorDesign");
            furnRoot.transform.SetParent(roomRoot.transform, false);
            BuildApartmentKitInterior(furnRoot.transform, matAppliance, matCeiling, matMattress);

            // 4. Outside Yard (z: 3.0 ~ 16.8) + 4-Stage Evolving Vehicle at z = 10.60
            // * 앞벽 버그 완전 수정: 북쪽 경계 펜스를 쓰레기통 훨씬 뒤(z=16.5)로 이동 → 뒤로 날아가도 벽에 안 막히고 직접 걸어가 회수 가능!
            GameObject outsideRoot = new GameObject("Outside_Truck_Area");
            CreateWorldBox("Parking_Floor", new Vector3(2.1f, -0.30f, 9.9f), new Vector3(11.0f, 0.6f, 13.8f), matOutdoorFloor, outsideRoot.transform, true);

            GameObject stepCurb = CreateWorldBox("Door_StepCurb", new Vector3(2.1f, 0.015f, 3.02f), new Vector3(1.1f, 0.03f, 0.10f), matCeiling, outsideRoot.transform, true);
            Object.DestroyImmediate(stepCurb.GetComponent<Collider>());

            CreateInvisibleFence("Fence_Outside_North", new Vector3(2.1f, 1.2f, 16.5f), new Vector3(11.0f, 2.4f, 0.15f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_West", new Vector3(-3.3f, 1.2f, 9.9f), new Vector3(0.15f, 2.4f, 13.8f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_East", new Vector3(7.5f, 1.2f, 9.9f), new Vector3(0.15f, 2.4f, 13.8f), outsideRoot.transform);

            GameObject porchLightObj = new GameObject("Porch_Light");
            porchLightObj.transform.SetParent(outsideRoot.transform, false);
            porchLightObj.transform.position = new Vector3(2.10f, 2.55f, 4.2f);
            Light porchLight = porchLightObj.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1.0f, 0.94f, 0.80f);
            porchLight.intensity = 11f;
            porchLight.range = 9.0f;
            porchLight.shadows = LightShadows.None;

            GameObject truckRoot = new GameObject("TrashVehicle_Goal");
            truckRoot.transform.SetParent(outsideRoot.transform, false);
            truckRoot.transform.position = new Vector3(2.10f, 0.0f, 10.60f);

            Material matBinBody = CreateSimpleLitMaterial("Mat_Veh_BinDark", new Color(0.24f, 0.28f, 0.30f), 0.35f);
            Material matCartBlue = CreateSimpleLitMaterial("Mat_Veh_CartBlue", new Color(0.18f, 0.45f, 0.78f), 0.38f);
            Material matDamasCream = CreateSimpleLitMaterial("Mat_Veh_DamasCream", new Color(0.92f, 0.90f, 0.82f), 0.45f);
            Material matWheelBlack = CreateSimpleLitMaterial("Mat_Veh_WheelBlack", new Color(0.10f, 0.10f, 0.12f), 0.2f);
            Material matGlass = CreateSimpleLitMaterial("Mat_Veh_Glass", new Color(0.28f, 0.46f, 0.58f), 0.85f);

            // Stage 0: 공공 쓰레기통 (0.76 x 0.86 x 0.76 — 작지만 구멍이 100% 직관적!)
            GameObject stage0 = BuildOpenContainerStage("Stage0_TrashBin", truckRoot.transform, 0.76f, 0.86f, 0.76f, 0.07f, matBinBody, matTruckGlow);
            // Stage 1: 리어카 (1.36 x 0.78 x 1.24 + 바퀴 2개 + 손잡이!)
            GameObject stage1 = BuildOpenContainerStage("Stage1_Handcart", truckRoot.transform, 1.36f, 0.78f, 1.24f, 0.07f, matCartBlue, matTruckGlow);
            AddVehicleWheel(stage1.transform, new Vector3(-0.74f, 0.32f, 0f), 0.62f, matWheelBlack);
            AddVehicleWheel(stage1.transform, new Vector3(0.74f, 0.32f, 0f), 0.62f, matWheelBlack);
            CreateLocalBox("Handle_L", new Vector3(-0.50f, 0.62f, -0.92f), new Vector3(0.06f, 0.06f, 0.62f), matWheelBlack, stage1.transform, false);
            CreateLocalBox("Handle_R", new Vector3(0.50f, 0.62f, -0.92f), new Vector3(0.06f, 0.06f, 0.62f), matWheelBlack, stage1.transform, false);
            stage1.SetActive(false);
            // Stage 2: 한국 다마스 경상용차 (1.92 x 0.84 x 1.56 오픈 짐칸 + 운전석 캐빈 + 바퀴 4개!)
            GameObject stage2 = BuildOpenContainerStage("Stage2_Damas", truckRoot.transform, 1.92f, 0.84f, 1.56f, 0.08f, matDamasCream, matTruckGlow);
            CreateLocalBox("Damas_Cab", new Vector3(0f, 0.86f, 1.22f), new Vector3(1.88f, 1.48f, 0.92f), matDamasCream, stage2.transform, false);
            CreateLocalBox("Damas_Windshield", new Vector3(0f, 1.18f, 0.75f), new Vector3(1.56f, 0.56f, 0.06f), matGlass, stage2.transform, false);
            AddVehicleWheel(stage2.transform, new Vector3(-0.96f, 0.28f, -0.45f), 0.52f, matWheelBlack);
            AddVehicleWheel(stage2.transform, new Vector3(0.96f, 0.28f, -0.45f), 0.52f, matWheelBlack);
            AddVehicleWheel(stage2.transform, new Vector3(-0.96f, 0.28f, 1.20f), 0.52f, matWheelBlack);
            AddVehicleWheel(stage2.transform, new Vector3(0.96f, 0.28f, 1.20f), 0.52f, matWheelBlack);
            stage2.SetActive(false);
            // Stage 3: 1톤 청소 트럭 (2.60 x 0.94 x 2.15 거대 짐칸 + 운전석 캐빈 + 큰 바퀴 4개!)
            GameObject stage3 = BuildOpenContainerStage("Stage3_Truck", truckRoot.transform, 2.60f, 0.94f, 2.15f, 0.09f, matTruckBody, matTruckGlow);
            CreateLocalBox("Truck_Cab", new Vector3(0f, 1.05f, 1.65f), new Vector3(2.45f, 1.85f, 1.15f), matTruckBody, stage3.transform, false);
            CreateLocalBox("Truck_Windshield", new Vector3(0f, 1.45f, 1.06f), new Vector3(2.05f, 0.65f, 0.06f), matGlass, stage3.transform, false);
            AddVehicleWheel(stage3.transform, new Vector3(-1.30f, 0.36f, -0.60f), 0.68f, matWheelBlack);
            AddVehicleWheel(stage3.transform, new Vector3(1.30f, 0.36f, -0.60f), 0.68f, matWheelBlack);
            AddVehicleWheel(stage3.transform, new Vector3(-1.30f, 0.36f, 1.60f), 0.68f, matWheelBlack);
            AddVehicleWheel(stage3.transform, new Vector3(1.30f, 0.36f, 1.60f), 0.68f, matWheelBlack);
            stage3.SetActive(false);

            GameObject[] vehicleStages = new[] { stage0, stage1, stage2, stage3 };

            // 투하한 쓰레기봉투가 실제로 차곡차곡 쌓이는 컨테이너!
            GameObject stackedParent = new GameObject("TruckStackedBags");
            stackedParent.transform.SetParent(truckRoot.transform, false);
            stackedParent.transform.localPosition = new Vector3(0f, 0.22f, 0f);

            GameObject truckLightObj = new GameObject("Truck_BeaconLight");
            truckLightObj.transform.SetParent(truckRoot.transform, false);
            truckLightObj.transform.localPosition = new Vector3(0f, 3.0f, -0.6f);
            Light truckLight = truckLightObj.AddComponent<Light>();
            truckLight.type = LightType.Point;
            truckLight.color = new Color(0.95f, 0.98f, 1.0f);
            truckLight.intensity = 18f;
            truckLight.range = 10.5f;
            truckLight.shadows = LightShadows.None;

            GameObject goalAnchor = new GameObject("GoalCenterAnchor");
            goalAnchor.transform.SetParent(truckRoot.transform, false);
            goalAnchor.transform.localPosition = new Vector3(0f, 0.62f, 0f);

            // 5. Ceiling Light (larger range for +50% bigger room!)
            GameObject lightObj = new GameObject("Ceiling_Light_WarmFluorescent");
            lightObj.transform.position = new Vector3(0f, h - 0.2f, 0f);
            Light ceilingLight = lightObj.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.color = new Color(1.0f, 0.92f, 0.76f);
            ceilingLight.intensity = 16f;
            ceilingLight.range = 13.5f;
            ceilingLight.shadows = LightShadows.None;

            SetupPostProcessing();

            // 6. Phase 2 Floor Stains (8 big glossy stains across the +50% larger room)
            GameObject stainRoot = new GameObject("Phase2_FloorStains");
            Vector3[] stainPositions = new[]
            {
                new Vector3(-0.35f, 0.008f, 0.35f),
                new Vector3(1.35f, 0.008f, -0.55f),
                new Vector3(2.25f, 0.009f, 0.95f),
                new Vector3(-1.55f, 0.008f, 1.45f),
                new Vector3(0.35f, 0.009f, -1.65f),
                new Vector3(1.95f, 0.008f, -1.95f),
                new Vector3(-2.35f, 0.008f, 0.45f),
                new Vector3(0.45f, 0.009f, 1.55f),
            };
            for (int i = 0; i < stainPositions.Length; i++)
            {
                bool isMold = i % 2 == 0;
                PlaceCleanableStain(
                    isMold ? $"Stain_Mold_{i + 1:00}" : $"Stain_Soup_{i + 1:00}",
                    stainPositions[i],
                    Random.Range(1.15f, 1.50f),
                    isMold ? matMoldDecal : matSoupDecal,
                    stainRoot.transform
                );
            }

            // 7. Phase 1 Big Breakable Piles (16개 — 단단한 큰 봉투/박스)
            GameObject whiteBagAsset = FindModelAsset($"{ArtRoot}/Models/Trash/trash_bag_white_broken");
            GameObject grayBagAsset = FindModelAsset($"{ArtRoot}/Models/Trash/trash_bag_gray_modern");
            GameObject blackBagAsset = FindModelAsset($"{ArtRoot}/Models/Trash/trashbag");
            GameObject boxAsset = FindModelAsset($"{ArtRoot}/Models/Trash/cardboard_box_01");
            GameObject styroAsset = FindModelAsset($"{ArtRoot}/Models/Trash/styrofoam_container");

            GameObject[] pileAssets = new[]
            {
                whiteBagAsset ?? blackBagAsset,
                blackBagAsset ?? whiteBagAsset,
                grayBagAsset ?? blackBagAsset,
                boxAsset ?? blackBagAsset,
                styroAsset ?? boxAsset ?? blackBagAsset
            };
            string[] pileNames = new[] { "흰 종량제 쓰레기봉투", "검정 쓰레기봉투", "회색 쓰레기봉투", "단단한 택배박스", "배달 스티로폼 박스" };

            GameObject trashRoot = new GameObject("Phase1_BigBreakablePiles");
            Vector3[] pilePositions = new[]
            {
                new Vector3(2.25f, 0f, 0.85f),
                new Vector3(1.25f, 0f, 0.72f),
                new Vector3(2.35f, 0f, -0.45f),
                new Vector3(2.15f, 0f, -1.35f),
                new Vector3(1.55f, 0f, -0.25f),
                new Vector3(1.45f, 0f, -1.15f),
                new Vector3(0.55f, 0f, 1.15f),
                new Vector3(-0.55f, 0f, 1.45f),
                new Vector3(-1.65f, 0f, 1.40f),
                new Vector3(-0.45f, 0f, 0.45f),
                new Vector3(0.55f, 0f, -0.85f),
                new Vector3(-2.25f, 0f, 0.85f),
                new Vector3(1.25f, 0f, -1.55f),
                new Vector3(0.05f, 0f, -1.45f),
                new Vector3(-1.10f, 0f, -0.55f),
                new Vector3(-2.35f, 0f, 0.10f),
                new Vector3(0.10f, 0f, 1.45f),
                new Vector3(2.45f, 0f, -1.55f),
                new Vector3(-0.85f, 0f, -1.65f),
                new Vector3(-2.15f, 0f, 1.45f),
            };

            for (int i = 0; i < pilePositions.Length; i++)
            {
                int k = i % pileAssets.Length;
                GameObject src = pileAssets[k];
                if (src == null) continue;
                bool isBoxLike = (k == 3 || k == 4);
                float size = isBoxLike ? Random.Range(0.62f, 0.78f) : Random.Range(0.82f, 1.05f);
                GameObject pile = SpawnNormalizedModel(
                    src, $"BreakablePile_{i + 1:00}",
                    pilePositions[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    size, trashRoot.transform
                );
                if (pile != null)
                {
                    var ct = AddCleanable(pile, CleanableKind.TrashPile, pileNames[k], 10.0f, 0, 0, 2.0f);
                    ct.burstSpawnCount = 20;
                    ct.containsTreasureChance = true;
                }
            }

            // 8. 2,250+ GPU-Instanced 3D Trash Swarm
            GameObject swarmObj = new GameObject("TrashSwarm_1000");
            var swarm = swarmObj.AddComponent<InstancedTrashSwarm>();
            PopulateSwarmMeshTypes(swarm);

            // 9. Hidden Runtime Template for Thrown Trash Bag
            GameObject templateRoot = new GameObject("_RuntimeTemplates");
            GameObject thrownBagSrc = whiteBagAsset ?? blackBagAsset;
            GameObject thrownBagTmpl = SpawnNormalizedModel(thrownBagSrc, "Template_ThrownBag", Vector3.zero, Quaternion.identity, 0.44f, templateRoot.transform);
            if (thrownBagTmpl == null)
            {
                thrownBagTmpl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                thrownBagTmpl.name = "Template_ThrownBag";
                thrownBagTmpl.transform.SetParent(templateRoot.transform, false);
                thrownBagTmpl.transform.localScale = Vector3.one * 0.42f;
                thrownBagTmpl.GetComponent<Renderer>().sharedMaterial = matBagBlack;
            }
            thrownBagTmpl.SetActive(false);

            // 10. Player Spawn OUTSIDE facing the closed Mustard Yellow Front Door!
            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.position = new Vector3(2.10f, 0.15f, 4.15f);
            spawnObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            FirstPersonCleanerController playerCtrl = CreateFpsPlayer(
                spawnObj.transform.position, spawnObj.transform.rotation,
                matSkin, matBagBlack, matTissue, matMopStick, matMopHead, matVacuumBody, matVacuumMetal, thrownBagSrc
            );

            // 11. Manager & HUD Wiring
            GameObject sysObj = new GameObject("GamePrototypeManager");
            var mgr = sysObj.AddComponent<GamePrototypeManager>();
            mgr.player = playerCtrl;
            mgr.trashSwarm = swarm;
            mgr.playerSpawnPoint = spawnObj.transform;
            mgr.truckBedGoal = goalAnchor.transform;
            mgr.vehicleStageObjects = vehicleStages;
            mgr.truckStackedParent = stackedParent.transform;
            mgr.ceilingLight = ceilingLight;
            mgr.thrownBagPrefab = thrownBagTmpl;

            swarm.manager = mgr;
            playerCtrl.manager = mgr;

            var hud = sysObj.AddComponent<PrototypeHudUI>();
            hud.manager = mgr;
            hud.controller = playerCtrl;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashCleaner3D v0.9.2] Brick Project Studio Apartment Kit 3D 원룸 인테리어 + v0.9.1 게임플레이 통합 빌드 완료!");
        }

        private static void BuildApartmentKitInterior(Transform parent, Material fallbackAppliance, Material fallbackCeiling, Material fallbackMattress)
        {
            // 요청 #2: 집 안에 꼭 필요한 핵심 5종만 깔끔하게 배치 (냉장고 · 전자레인지 · 싱크대 · 침대 · 책상)!
            // 1) 냉장고 (북쪽, 현관문 왼쪽)
            Bounds fridgeBounds;
            bool hasFridge = PlaceAptPrefab("Furniture/Kitchen/Appliances/Fridge_01.prefab", "Apt_Fridge", new Vector3(0.82f, 0f, 2.52f), 180f, parent, true, true, out fridgeBounds);
            if (!hasFridge)
            {
                GameObject fb = CreateWorldBox("Fridge", new Vector3(0.78f, 0.78f, 2.60f), new Vector3(0.70f, 1.56f, 0.70f), fallbackAppliance, parent, true);
                fridgeBounds = fb.GetComponent<Renderer>().bounds;
            }

            // 2) 전자레인지 (한국형 원룸 국룰: 냉장고 위 깔끔한 3D 전자레인지!)
            BuildRealisticMicrowave(new Vector3(fridgeBounds.center.x, fridgeBounds.max.y, fridgeBounds.center.z), parent);

            // 3) 싱크대 (냉장고 바로 왼쪽)
            PlaceAptPrefab("Furniture/Kitchen/Cabinets/Cabinet_Base_Sink_01.prefab", "Apt_KitchenSink", new Vector3(-0.35f, 0f, 2.58f), 180f, parent, true, true, out _);

            // 4) 침대 (서남쪽 코너)
            bool hasBed = PlaceAptPrefab("Furniture/Bedroom/Bed_Apt_01_01.prefab", "Apt_Bed", new Vector3(-2.35f, 0f, -1.78f), 90f, parent, true, true, out _);
            if (!hasBed)
            {
                CreateWorldBox("Mattress", new Vector3(-2.45f, 0.16f, -1.75f), new Vector3(1.75f, 0.32f, 2.25f), fallbackMattress, parent, true);
            }

            // 5) 책상 (남쪽 벽 PC 책상 풀세트)
            PlaceAptPrefab("Furniture/Living Room/Table_Computer_01_Setup.prefab", "Apt_ComputerDesk", new Vector3(0.45f, 0f, -2.42f), 0f, parent, true, true, out _);
        }

        private static void BuildRealisticMicrowave(Vector3 bottomCenter, Transform parent)
        {
            Material matBody = CreateSimpleLitMaterial("Mat_Micro_Body", new Color(0.86f, 0.87f, 0.90f), 0.52f);
            Material matGlass = CreateSimpleLitMaterial("Mat_Micro_Glass", new Color(0.10f, 0.12f, 0.14f), 0.85f);
            Material matPanel = CreateSimpleLitMaterial("Mat_Micro_Panel", new Color(0.22f, 0.24f, 0.27f), 0.45f);
            Material matLed = CreateSimpleLitMaterial("Mat_Micro_Led", new Color(0.25f, 0.95f, 0.45f), 0.8f);

            GameObject root = new GameObject("Apt_Microwave");
            root.transform.SetParent(parent, false);
            root.transform.position = bottomCenter;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 방 안쪽(남쪽)을 바라봄

            CreateLocalBox("Body", new Vector3(0f, 0.155f, 0f), new Vector3(0.50f, 0.31f, 0.36f), matBody, root.transform, false);
            GameObject win = CreateLocalBox("DoorGlass", new Vector3(-0.06f, 0.155f, 0.182f), new Vector3(0.32f, 0.22f, 0.012f), matGlass, root.transform, false);
            Object.DestroyImmediate(win.GetComponent<Collider>());
            GameObject handle = CreateLocalBox("DoorHandle", new Vector3(0.115f, 0.155f, 0.195f), new Vector3(0.018f, 0.19f, 0.025f), matPanel, root.transform, false);
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            GameObject pad = CreateLocalBox("Keypad", new Vector3(0.185f, 0.155f, 0.182f), new Vector3(0.09f, 0.23f, 0.012f), matPanel, root.transform, false);
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            GameObject led = CreateLocalBox("ClockLed", new Vector3(0.185f, 0.235f, 0.190f), new Vector3(0.06f, 0.025f, 0.008f), matLed, root.transform, false);
            Object.DestroyImmediate(led.GetComponent<Collider>());
        }

        private static bool PlaceAptPrefab(
            string relPath, string objName, Vector3 anchorPos, float yawDeg,
            Transform parent, bool addSimpleBoxCollider, bool clampInsideRoom, out Bounds finalBounds)
        {
            finalBounds = new Bounds(anchorPos, Vector3.one);
            string fullPath = $"{AptKitRoot}/{relPath}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (prefab == null) return false;

            GameObject go = Object.Instantiate(prefab);
            go.name = objName;
            go.transform.SetParent(parent, true);
            go.transform.position = anchorPos;
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb != null) Object.DestroyImmediate(mb);
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(col);
            }

            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                Vector3 shift = new Vector3(
                    anchorPos.x - b.center.x,
                    anchorPos.y - b.min.y,
                    anchorPos.z - b.center.z
                );
                go.transform.position += shift;

                if (clampInsideRoom)
                {
                    b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                    Vector3 clampShift = Vector3.zero;
                    if (b.min.x < -3.54f) clampShift.x = -3.54f - b.min.x;
                    else if (b.max.x > 3.54f) clampShift.x = 3.54f - b.max.x;
                    if (b.min.z < -2.94f) clampShift.z = -2.94f - b.min.z;
                    else if (b.max.z > 2.94f) clampShift.z = 2.94f - b.max.z;
                    if (b.max.x > 1.46f && b.max.z > 1.95f && anchorPos.x < 1.50f)
                    {
                        clampShift.x = Mathf.Min(clampShift.x, 1.44f - b.max.x);
                    }
                    go.transform.position += clampShift;
                }

                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                finalBounds = b;

                if (addSimpleBoxCollider)
                {
                    GameObject colObj = new GameObject("_BoundsCollider");
                    colObj.transform.SetParent(go.transform, true);
                    colObj.transform.position = b.center;
                    colObj.transform.rotation = Quaternion.identity;
                    var bc = colObj.AddComponent<BoxCollider>();
                    bc.size = new Vector3(Mathf.Max(0.15f, b.size.x * 0.94f), Mathf.Max(0.15f, b.size.y * 0.96f), Mathf.Max(0.15f, b.size.z * 0.94f));
                }
            }
            return true;
        }

        private static void CreateBuriedTreasure(
            string objName, string displayName, Vector3 pos, Vector3 scale, Material mat,
            Transform parent, int value, TreasureRarity rarity, bool mentionedByClient, float suspGain)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objName;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // 살짝 여유 있는 발굴 판정 콜라이더
            var col = go.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.size = new Vector3(1.45f, 2.2f, 1.45f);
                col.isTrigger = true;
            }

            var bt = go.AddComponent<BuriedTreasureObject>();
            bt.itemName = displayName;
            bt.baseValue = value;
            bt.rarity = rarity;
            bt.isMentionedByClient = mentionedByClient;
            bt.baseSuspicionGain = suspGain;
            bt.coverThreshold = 5;
            bt.coverCheckRadius = 0.72f;
        }

        private static GameObject BuildOpenContainerStage(string name, Transform parent, float w, float h, float d, float wallT, Material bodyMat, Material rimMat)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;

            // 바닥 + 4면 벽 (내부가 완전히 뚫려 있고, 상단 테두리에 밝은 노란 림을 둘러 투입구가 100% 직관적!)
            CreateLocalBox("Bottom", new Vector3(0f, 0.08f, 0f), new Vector3(w, 0.14f, d), bodyMat, root.transform, false);
            CreateLocalBox("Wall_Front", new Vector3(0f, h * 0.45f, -d / 2f + wallT / 2f), new Vector3(w, h * 0.90f, wallT), bodyMat, root.transform, false);
            CreateLocalBox("Wall_Back", new Vector3(0f, h * 0.50f, d / 2f - wallT / 2f), new Vector3(w, h, wallT), bodyMat, root.transform, false);
            CreateLocalBox("Wall_Left", new Vector3(-w / 2f + wallT / 2f, h * 0.48f, 0f), new Vector3(wallT, h * 0.96f, d), bodyMat, root.transform, false);
            CreateLocalBox("Wall_Right", new Vector3(w / 2f - wallT / 2f, h * 0.48f, 0f), new Vector3(wallT, h * 0.96f, d), bodyMat, root.transform, false);

            // 투입구 상단 노란 림 (어디에 넣어야 하는지 한눈에 직관적!)
            CreateLocalBox("Rim_F", new Vector3(0f, h * 0.92f, -d / 2f + wallT / 2f), new Vector3(w + 0.04f, 0.07f, wallT + 0.03f), rimMat, root.transform, false);
            CreateLocalBox("Rim_B", new Vector3(0f, h + 0.02f, d / 2f - wallT / 2f), new Vector3(w + 0.04f, 0.07f, wallT + 0.03f), rimMat, root.transform, false);
            CreateLocalBox("Rim_L", new Vector3(-w / 2f + wallT / 2f, h * 0.97f, 0f), new Vector3(wallT + 0.03f, 0.07f, d + 0.04f), rimMat, root.transform, false);
            CreateLocalBox("Rim_R", new Vector3(w / 2f - wallT / 2f, h * 0.97f, 0f), new Vector3(wallT + 0.03f, 0.07f, d + 0.04f), rimMat, root.transform, false);
            return root;
        }

        private static void AddVehicleWheel(Transform parent, Vector3 localPos, float diameter, Material wheelMat)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPos;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(diameter, 0.10f, diameter);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.GetComponent<Renderer>().sharedMaterial = wheelMat;
        }

        /// <summary>
        /// 1,000+ GPU Instancing용 깔끔한 3D 소형 쓰레기 5종 세팅
        /// (뾰족하게 깨지던 crushed 피자박스 제거 → Unity Y-up 정방향 캔·담뱃갑·통조림·생수통·구겨진 종이 사용!)
        /// </summary>
        private static void PopulateSwarmMeshTypes(InstancedTrashSwarm swarm)
        {
            // v1.1.2 디자인: 원색 대신 크라프트/무광 알루미늄/반투명 플라스틱/검은 비닐 톤으로 차분하게
            TryAddFallbackFolderType(swarm, "녹슨 캔", $"{ArtRoot}/Models/Trash/can_rusted", 0.16f, new Color(0.62f, 0.44f, 0.32f));
            TryAddFallbackFolderType(swarm, "담뱃갑", $"{ArtRoot}/Models/Trash/cigarette_pack", 0.14f, new Color(0.78f, 0.745f, 0.685f));
            TryAddFallbackFolderType(swarm, "통조림 캔", $"{ArtRoot}/Models/Trash/russian_food_cans_01", 0.20f, new Color(0.720f, 0.700f, 0.620f));
            TryAddFallbackFolderType(swarm, "플라스틱 물통", $"{ArtRoot}/Models/Trash/plastic_bottle_gallon", 0.26f, new Color(0.780f, 0.830f, 0.850f));

            string litterFbxDir = $"{ArtRoot}/Models/Trash/street_litter_sampler/fbx";
            string litterTexDir = $"{ArtRoot}/Models/Trash/street_litter_sampler/textures";
            TryAddInstancedType(swarm, "구겨진 종이",
                $"{litterFbxDir}/SM_PL_Paper_BallMedium_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Paper_BallMedium_crushed_BaseColor.jpg",
                $"{litterTexDir}/T_SM_PL_Paper_BallMedium_crushed_Normal.png",
                new Color(0.82f, 0.795f, 0.745f), 0.15f, 0.10f);

            // 아트 팩이 없으면 단일 큐브 대신, 생활감 있는 로우폴리 쓰레기 5종(캔·택배박스·페트병·비닐봉투·종이)을 코드로 생성
            if (swarm.meshTypes.Count == 0)
            {
                swarm.BuildProceduralTypes();
            }

            // 최후 안전망
            if (swarm.meshTypes.Count == 0)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
                Material fallbackMat = CreateSimpleLitMaterial("Mat_Instanced_FallbackLitter", new Color(0.85f, 0.75f, 0.55f), 0.2f);
                fallbackMat.enableInstancing = true;
                EditorUtility.SetDirty(fallbackMat);
                swarm.meshTypes.Add(new InstancedTrashSwarm.TrashMeshType
                {
                    label = "쓰레기 조각",
                    mesh = cubeMesh,
                    material = fallbackMat,
                    normalizedScale = 0.16f
                });
            }
        }

        private static void TryAddInstancedType(
            InstancedTrashSwarm swarm, string label, string fbxPath,
            string baseColorPath, string normalPath, Color fallbackTint, float targetDim, float smoothness)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return;

            MeshFilter mf = model.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return;

            Mesh mesh = mf.sharedMesh;
            Vector3 sz = mesh.bounds.size;
            float maxDim = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            float normScale = maxDim > 0.0001f ? (targetDim / maxDim) : 1f;

            string safeKey = Path.GetFileNameWithoutExtension(fbxPath);
            string matPath = $"{MatRoot}/Mat_Inst_{safeKey}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            Texture2D diff = LoadTex(baseColorPath, false);
            Texture2D norm = LoadTex(normalPath, true);
            if (diff != null)
            {
                mat.SetTexture("_BaseMap", diff);
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                mat.SetColor("_BaseColor", fallbackTint);
            }
            if (norm != null)
            {
                mat.SetTexture("_BumpMap", norm);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 0.85f);
            }
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            swarm.meshTypes.Add(new InstancedTrashSwarm.TrashMeshType
            {
                label = label,
                mesh = mesh,
                material = mat,
                normalizedScale = normScale
            });
        }

        private static void TryAddFallbackFolderType(InstancedTrashSwarm swarm, string label, string folder, float targetDim, Color tint)
        {
            GameObject model = FindModelAsset(folder);
            if (model == null) return;
            MeshFilter mf = model.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return;

            Mesh mesh = mf.sharedMesh;
            Vector3 sz = mesh.bounds.size;
            float maxDim = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            float normScale = maxDim > 0.0001f ? (targetDim / maxDim) : 1f;

            string key = Path.GetFileName(folder);
            string texDir = Directory.Exists($"{folder}/textures") ? $"{folder}/textures" : folder;
            Material mat = CreateOrUpdatePbrMaterial($"Mat_Inst_{key}", texDir, Vector2.one, Color.white, 0.28f);
            if (mat.GetTexture("_BaseMap") == null) mat.SetColor("_BaseColor", tint);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            swarm.meshTypes.Add(new InstancedTrashSwarm.TrashMeshType
            {
                label = label,
                mesh = mesh,
                material = mat,
                normalizedScale = normScale
            });
        }

        private static FirstPersonCleanerController CreateFpsPlayer(
            Vector3 startPos, Quaternion startRot,
            Material matSkin, Material matBag, Material matTissue,
            Material matMopStick, Material matMopHead,
            Material matVacuumBody, Material matVacuumMetal, GameObject heldBagModelPrefab)
        {
            GameObject player = new GameObject("Player_FPS");
            player.transform.position = startPos;
            player.transform.rotation = startRot;
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.65f;
            cc.radius = 0.26f;
            cc.center = new Vector3(0, 0.825f, 0);
            cc.stepOffset = 0.35f;

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.50f, 0);
            var cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.08f;
            cam.fieldOfView = 72f;
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
            flash.spotAngle = 66f;
            flash.color = new Color(1f, 0.98f, 0.92f);
            flash.shadows = LightShadows.Soft;
            flash.enabled = true;

            // RIGHT HAND PIVOT (Fist / Vacuum / Tissue / Mop — distinctly animated!)
            GameObject rightPivot = new GameObject("RightHandPivot");
            rightPivot.transform.SetParent(camObj.transform, false);
            rightPivot.transform.localPosition = new Vector3(0.32f, -0.26f, 0.52f);

            // 1) 1티어: 장갑 낀 맨손 (어두운 작업장갑 + 주황 커프) — 빗자루(초록)·진공(빨강)과 색이 확실히 갈린다
            Material matGlove = CreateSimpleLitMaterial("Mat_Tool_WorkGlove", new Color(0.19f, 0.21f, 0.26f), 0.16f);
            Material matGloveCuff = CreateSimpleLitMaterial("Mat_Tool_GloveCuff", new Color(0.95f, 0.56f, 0.14f), 0.22f);
            GameObject fist = new GameObject("Visual_FistHand");
            fist.transform.SetParent(rightPivot.transform, false);
            GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            forearm.transform.SetParent(fist.transform, false);
            forearm.transform.localPosition = new Vector3(0.03f, -0.05f, -0.10f);
            forearm.transform.localRotation = Quaternion.Euler(78f, -12f, 0f);
            forearm.transform.localScale = new Vector3(0.085f, 0.18f, 0.085f);
            Object.DestroyImmediate(forearm.GetComponent<Collider>());
            forearm.GetComponent<Renderer>().sharedMaterial = matSkin;

            // 주황 커프 밴드: 손목에서 티어 색을 한 번 더 각인
            GameObject cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuff.transform.SetParent(fist.transform, false);
            cuff.transform.localPosition = new Vector3(0.022f, -0.03f, -0.025f);
            cuff.transform.localRotation = Quaternion.Euler(78f, -12f, 0f);
            cuff.transform.localScale = new Vector3(0.10f, 0.035f, 0.10f);
            Object.DestroyImmediate(cuff.GetComponent<Collider>());
            cuff.GetComponent<Renderer>().sharedMaterial = matGloveCuff;

            GameObject knuckles = GameObject.CreatePrimitive(PrimitiveType.Cube);
            knuckles.transform.SetParent(fist.transform, false);
            knuckles.transform.localPosition = new Vector3(0f, 0.01f, 0.08f);
            knuckles.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            knuckles.transform.localScale = new Vector3(0.115f, 0.105f, 0.12f);
            Object.DestroyImmediate(knuckles.GetComponent<Collider>());
            knuckles.GetComponent<Renderer>().sharedMaterial = matGlove;

            GameObject thumb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thumb.transform.SetParent(fist.transform, false);
            thumb.transform.localPosition = new Vector3(0.055f, 0.015f, 0.055f);
            thumb.transform.localRotation = Quaternion.Euler(18f, -22f, -28f);
            thumb.transform.localScale = new Vector3(0.038f, 0.052f, 0.085f);
            Object.DestroyImmediate(thumb.GetComponent<Collider>());
            thumb.GetComponent<Renderer>().sharedMaterial = matGlove;

            // 1.5) 2티어 도구: 빗자루 (초록 자루 + 크롬 페룰 + 부채꼴 짚 5가닥 — 마른 잎 색)
            Material matBroomGreen = CreateSimpleLitMaterial("Mat_Tool_BroomGreen", new Color(0.24f, 0.72f, 0.42f), 0.35f);
            Material matBristleStraw = CreateSimpleLitMaterial("Mat_Tool_BroomBristle", new Color(0.84f, 0.74f, 0.42f), 0.12f);
            GameObject tongsBroom = new GameObject("Visual_TongsBroom");
            tongsBroom.transform.SetParent(rightPivot.transform, false);
            GameObject bHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bHandle.transform.SetParent(tongsBroom.transform, false);
            bHandle.transform.localPosition = new Vector3(0f, -0.02f, 0.16f);
            bHandle.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            bHandle.transform.localScale = new Vector3(0.036f, 0.34f, 0.036f);
            Object.DestroyImmediate(bHandle.GetComponent<Collider>());
            bHandle.GetComponent<Renderer>().sharedMaterial = matBroomGreen;

            GameObject bHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bHead.transform.SetParent(tongsBroom.transform, false);
            bHead.transform.localPosition = new Vector3(0f, -0.15f, 0.48f);
            bHead.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            bHead.transform.localScale = new Vector3(0.26f, 0.05f, 0.09f);
            Object.DestroyImmediate(bHead.GetComponent<Collider>());
            bHead.GetComponent<Renderer>().sharedMaterial = matBroomGreen;

            // 자루와 머리를 잇는 금속 페룰 링
            GameObject bFerrule = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bFerrule.transform.SetParent(tongsBroom.transform, false);
            bFerrule.transform.localPosition = new Vector3(0f, -0.105f, 0.43f);
            bFerrule.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            bFerrule.transform.localScale = new Vector3(0.055f, 0.022f, 0.055f);
            Object.DestroyImmediate(bFerrule.GetComponent<Collider>());
            bFerrule.GetComponent<Renderer>().sharedMaterial = matVacuumMetal;

            // 짚가닥 5개를 부채꼴로 벌려 빗자루 실루엣을 확실히
            for (int i = 0; i < 5; i++)
            {
                float off = (i - 2) * 0.052f;
                GameObject straw = GameObject.CreatePrimitive(PrimitiveType.Cube);
                straw.name = $"BroomStraw_{i + 1}";
                straw.transform.SetParent(tongsBroom.transform, false);
                straw.transform.localPosition = new Vector3(off, -0.235f, 0.505f + Mathf.Abs(off) * 0.25f);
                straw.transform.localRotation = Quaternion.Euler(14f, off * 240f, off * 160f);
                straw.transform.localScale = new Vector3(0.034f, 0.135f, 0.05f);
                Object.DestroyImmediate(straw.GetComponent<Collider>());
                straw.GetComponent<Renderer>().sharedMaterial = matBristleStraw;
            }
            tongsBroom.SetActive(false);

            // 2) 3티어: 진공청소기 (빨간 몸통 + 크롬 호스 리브 + 시원하게 벌린 노즐 + 하늘색 흡입구)
            Material matVacuumGlow = CreateSimpleLitMaterial("Mat_Tool_VacuumGlow", new Color(0.34f, 0.95f, 1.0f), 0.85f);
            GameObject vacuum = new GameObject("Visual_VacuumNozzle");
            vacuum.transform.SetParent(rightPivot.transform, false);

            GameObject vacBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vacBody.transform.SetParent(vacuum.transform, false);
            vacBody.transform.localPosition = new Vector3(0f, -0.005f, 0.06f);
            vacBody.transform.localScale = new Vector3(0.155f, 0.15f, 0.30f);
            Object.DestroyImmediate(vacBody.GetComponent<Collider>());
            vacBody.GetComponent<Renderer>().sharedMaterial = matVacuumBody;

            // 몸통 크롬 밴드
            GameObject vacBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vacBand.transform.SetParent(vacuum.transform, false);
            vacBand.transform.localPosition = new Vector3(0f, -0.055f, 0.06f);
            vacBand.transform.localScale = new Vector3(0.165f, 0.03f, 0.305f);
            Object.DestroyImmediate(vacBand.GetComponent<Collider>());
            vacBand.GetComponent<Renderer>().sharedMaterial = matVacuumMetal;

            GameObject vacHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vacHandle.transform.SetParent(vacuum.transform, false);
            vacHandle.transform.localPosition = new Vector3(0f, 0.055f, -0.01f);
            vacHandle.transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
            vacHandle.transform.localScale = new Vector3(0.055f, 0.13f, 0.055f);
            Object.DestroyImmediate(vacHandle.GetComponent<Collider>());
            vacHandle.GetComponent<Renderer>().sharedMaterial = matVacuumBody;

            GameObject vacPipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vacPipe.transform.SetParent(vacuum.transform, false);
            vacPipe.transform.localPosition = new Vector3(0f, -0.10f, 0.34f);
            vacPipe.transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
            vacPipe.transform.localScale = new Vector3(0.058f, 0.22f, 0.058f);
            Object.DestroyImmediate(vacPipe.GetComponent<Collider>());
            vacPipe.GetComponent<Renderer>().sharedMaterial = matVacuumMetal;

            // 호스 리브 3개 — 진공청소기 특유의 주름 호스 실루엣
            for (int i = 0; i < 3; i++)
            {
                GameObject rib = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rib.name = $"VacRib_{i + 1}";
                rib.transform.SetParent(vacuum.transform, false);
                rib.transform.localPosition = new Vector3(0f, -0.085f - i * 0.035f, 0.24f + i * 0.095f);
                rib.transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
                rib.transform.localScale = new Vector3(0.076f, 0.016f, 0.076f);
                Object.DestroyImmediate(rib.GetComponent<Collider>());
                rib.GetComponent<Renderer>().sharedMaterial = matVacuumBody;
            }

            GameObject vacNozzle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vacNozzle.transform.SetParent(vacuum.transform, false);
            vacNozzle.transform.localPosition = new Vector3(0f, -0.165f, 0.575f);
            vacNozzle.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            vacNozzle.transform.localScale = new Vector3(0.31f, 0.075f, 0.13f);
            Object.DestroyImmediate(vacNozzle.GetComponent<Collider>());
            vacNozzle.GetComponent<Renderer>().sharedMaterial = matVacuumBody;

            // 노즐 입에 하늘색 발광띠 — 흡입 중 소용돌이가 나오는 지점을 눈으로 표시
            GameObject vacLip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vacLip.transform.SetParent(vacuum.transform, false);
            vacLip.transform.localPosition = new Vector3(0f, -0.196f, 0.615f);
            vacLip.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            vacLip.transform.localScale = new Vector3(0.27f, 0.022f, 0.045f);
            Object.DestroyImmediate(vacLip.GetComponent<Collider>());
            vacLip.GetComponent<Renderer>().sharedMaterial = matVacuumGlow;
            vacuum.SetActive(false);

            // 3) 물티슈 한 장 (Phase 2 초반 — 맨손 문지르기)
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

            // 4) T자 대걸레 (마인드맵 구매 후)
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

            // LEFT HAND PIVOT: 봉투가 완성되거나 떨어진 봉투를 주웠을 때 왼손에 실제 쓰레기봉지를 쥐고 있는 비주얼!
            GameObject leftBagRoot = new GameObject("LeftHand_HeldTrashBag");
            leftBagRoot.transform.SetParent(camObj.transform, false);
            leftBagRoot.transform.localPosition = new Vector3(-0.34f, -0.24f, 0.54f);

            GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftArm.transform.SetParent(leftBagRoot.transform, false);
            leftArm.transform.localPosition = new Vector3(-0.04f, -0.02f, -0.14f);
            leftArm.transform.localRotation = Quaternion.Euler(82f, 14f, 0f);
            leftArm.transform.localScale = new Vector3(0.08f, 0.16f, 0.08f);
            Object.DestroyImmediate(leftArm.GetComponent<Collider>());
            leftArm.GetComponent<Renderer>().sharedMaterial = matSkin;

            GameObject leftGrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftGrip.transform.SetParent(leftBagRoot.transform, false);
            leftGrip.transform.localPosition = new Vector3(0f, 0.02f, 0.02f);
            leftGrip.transform.localScale = new Vector3(0.10f, 0.09f, 0.10f);
            Object.DestroyImmediate(leftGrip.GetComponent<Collider>());
            leftGrip.GetComponent<Renderer>().sharedMaterial = matSkin;

            GameObject bagMeshObj = SpawnNormalizedModel(heldBagModelPrefab, "HeldBag_3D", Vector3.zero, Quaternion.identity, 0.32f, leftBagRoot.transform);
            if (bagMeshObj != null)
            {
                foreach (var c in bagMeshObj.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                bagMeshObj.transform.localPosition = new Vector3(0f, -0.24f, 0.04f);
            }
            else
            {
                GameObject fallbackBag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fallbackBag.transform.SetParent(leftBagRoot.transform, false);
                fallbackBag.transform.localPosition = new Vector3(0f, -0.16f, 0.04f);
                fallbackBag.transform.localScale = new Vector3(0.22f, 0.26f, 0.22f);
                Object.DestroyImmediate(fallbackBag.GetComponent<Collider>());
                fallbackBag.GetComponent<Renderer>().sharedMaterial = matBag;
            }
            leftBagRoot.SetActive(false);

            var ctrl = player.AddComponent<FirstPersonCleanerController>();
            ctrl.cameraTransform = camObj.transform;
            ctrl.rightToolPivot = rightPivot.transform;
            ctrl.leftBagPivot = leftBagRoot.transform;
            ctrl.fistHandVisual = fist;
            ctrl.tongsBroomVisual = tongsBroom;
            ctrl.vacuumVisual = vacuum;
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
            col.size = new Vector3(1f, 1f, 0.08f);

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
                // PolyHaven(/textures 하위)과 Megascans(같은 폴더 직속) 모두 자동 매칭!
                string texFolder = Directory.Exists($"{modelFolder}/textures") ? $"{modelFolder}/textures" : modelFolder;
                string folderKey = Path.GetFileName(modelFolder);
                Material propMat = CreateOrUpdatePbrMaterial($"Mat_Prop_{folderKey}", texFolder, Vector2.one, Color.white, 0.26f);
                if (propMat.GetTexture("_BaseMap") != null)
                {
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
                box.size = Vector3.Max(b.size, Vector3.one * 0.26f);
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
            colorAdj.postExposure.Override(0.28f);
            colorAdj.contrast.Override(10f);
            colorAdj.saturation.Override(-4f);
            colorAdj.colorFilter.Override(new Color(1.0f, 0.98f, 0.92f));

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.42f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.25f);

            volume.sharedProfile = profile;
        }

        private static GameObject CreateWorldBox(string name, Vector3 worldPos, Vector3 scale, Material mat, Transform parent, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.localScale = scale;
            go.isStatic = isStatic;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject CreateLocalBox(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            go.isStatic = isStatic;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void CreateInvisibleFence(string name, Vector3 worldPos, Vector3 size, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.isStatic = true;
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
                string[] files = Directory.GetFiles(texFolder)
                    .Where(f => !f.EndsWith(".meta") && (f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".exr", System.StringComparison.OrdinalIgnoreCase)))
                    .ToArray();

                Texture2D diff = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("basecolor") || f.ToLower().Contains("diff") || f.ToLower().Contains("color")), false);
                Texture2D norm = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("nor_gl") || f.ToLower().Contains("normalgl") || f.ToLower().Contains("normal")), true);
                Texture2D ao = LoadTex(files.FirstOrDefault(f => f.ToLower().Contains("_ao") || f.ToLower().Contains("ambientocclusion")), false);

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
