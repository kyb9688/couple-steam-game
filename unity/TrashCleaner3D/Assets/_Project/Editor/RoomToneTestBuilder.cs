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
        private const int SceneBuildVersion = 45;
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
            RenderSettings.ambientLight = new Color(0.20f, 0.17f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.13f, 0.09f);
            RenderSettings.fogDensity = 0.044f;

            Material matTileFloor = CreateSimpleLitMaterial("Mat_Floor_BathroomTile", new Color(0.82f, 0.86f, 0.88f), 0.42f);
            Material matPartitionWall = CreateSimpleLitMaterial("Mat_Wall_Partition", new Color(0.95f, 0.93f, 0.88f), 0.10f);
            Material matDoorHandle = CreateSimpleLitMaterial("Mat_Door_HandleMetal", new Color(0.18f, 0.19f, 0.22f), 0.72f);

            // 공유 북쪽 현관문 (z = 3.92, x: 1.55..2.65) — 1단계 원룸과 2단계 4방 아파트가 동일한 현관·마당 공유!
            const float doorZ = 3.92f;
            GameObject sharedDoorRoot = new GameObject("Shared_EntranceDoor");
            GameObject doorHinge = new GameObject("Door_Interactive_Hinge");
            doorHinge.transform.SetParent(sharedDoorRoot.transform, false);
            doorHinge.transform.position = new Vector3(2.64f, 0f, doorZ);

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

            // ===== 3A. STAGE 1: +30% 확장 원룸 (9.36m × 7.80m × 2.8m, x:-4.68..+4.68, z:-3.90..+3.90) =====
            GameObject stage1Container = new GameObject("Stage1_OneRoom_Container");
            float w1 = 9.36f, d1 = 7.80f, h1 = 2.80f;
            CreateWorldBox("Floor", new Vector3(0f, -0.30f, 0f), new Vector3(w1 + 0.4f, 0.6f, d1 + 0.4f), matFloor, stage1Container.transform, true);
            CreateWorldBox("Ceiling", new Vector3(0f, h1 + 0.15f, 0f), new Vector3(w1 + 0.4f, 0.3f, d1 + 0.4f), matCeiling, stage1Container.transform, true);

            // 북쪽 벽 (현관문 x: 1.55 ~ 2.65 비움)
            CreateWorldBox("Wall_North_Left", new Vector3(-1.665f, h1 / 2f, d1 / 2f + 0.15f), new Vector3(6.43f, h1, 0.3f), matWall, stage1Container.transform, true);
            CreateWorldBox("Wall_North_Right", new Vector3(3.765f, h1 / 2f, d1 / 2f + 0.15f), new Vector3(2.23f, h1, 0.3f), matWall, stage1Container.transform, true);
            CreateWorldBox("Wall_North_Header", new Vector3(2.10f, 2.475f, d1 / 2f + 0.15f), new Vector3(1.15f, 0.65f, 0.3f), matWall, stage1Container.transform, true);

            CreateWorldBox("Wall_South", new Vector3(0f, h1 / 2f, -d1 / 2f - 0.15f), new Vector3(w1 + 0.4f, h1, 0.3f), matWall, stage1Container.transform, true);
            CreateWorldBox("Wall_East", new Vector3(w1 / 2f + 0.15f, h1 / 2f, 0f), new Vector3(0.3f, h1, d1 + 0.4f), matWall, stage1Container.transform, true);
            CreateWorldBox("Wall_West", new Vector3(-w1 / 2f - 0.15f, h1 / 2f, 0f), new Vector3(0.3f, h1, d1 + 0.4f), matWall, stage1Container.transform, true);

            GameObject furnRoot1 = new GameObject("ApartmentKit_Stage1_Interior");
            furnRoot1.transform.SetParent(stage1Container.transform, false);
            BuildStage1OneRoomInterior(furnRoot1.transform, matAppliance, matMattress);

            Light s1CeilingLight = CreateRoomCeilingLight("Stage1_CeilingLight", new Vector3(0f, h1 - 0.2f, 0f), 16.5f, stage1Container.transform);
            Light[] stage1RoomLights = new[] { s1CeilingLight };

            // ===== 3B. STAGE 2: 4방 가정집 아파트 (거실·주방·안방·화장실, 15.4m × 12.8m × 2.9m, 8,000개!) =====
            GameObject stage2Container = new GameObject("Stage2_Apartment_Container");
            float w2 = 15.40f, d2 = 12.80f, h2 = 2.90f;
            float z2Center = -2.50f; // z: -8.90 .. +3.90 (북벽이 Stage 1과 정확히 z=3.90 일치!)
            CreateWorldBox("Floor_Apt", new Vector3(0f, -0.30f, z2Center), new Vector3(w2 + 0.4f, 0.6f, d2 + 0.4f), matFloor, stage2Container.transform, true);
            GameObject bathTile = CreateWorldBox("Floor_BathroomTile", new Vector3(4.60f, 0.002f, -5.55f), new Vector3(6.15f, 0.006f, 6.65f), matTileFloor, stage2Container.transform, true);
            Object.DestroyImmediate(bathTile.GetComponent<Collider>());
            CreateWorldBox("Ceiling_Apt", new Vector3(0f, h2 + 0.15f, z2Center), new Vector3(w2 + 0.4f, 0.3f, d2 + 0.4f), matCeiling, stage2Container.transform, true);

            // 외벽 (북벽 현관문 x: 1.55 ~ 2.65 공유)
            CreateWorldBox("Wall2_North_Left", new Vector3(-3.175f, h2 / 2f, 3.90f + 0.15f), new Vector3(9.45f, h2, 0.3f), matWall, stage2Container.transform, true);
            CreateWorldBox("Wall2_North_Right", new Vector3(5.275f, h2 / 2f, 3.90f + 0.15f), new Vector3(5.25f, h2, 0.3f), matWall, stage2Container.transform, true);
            CreateWorldBox("Wall2_North_Header", new Vector3(2.10f, 2.525f, 3.90f + 0.15f), new Vector3(1.15f, 0.75f, 0.3f), matWall, stage2Container.transform, true);
            CreateWorldBox("Wall2_South", new Vector3(0f, h2 / 2f, -8.90f - 0.15f), new Vector3(w2 + 0.4f, h2, 0.3f), matWall, stage2Container.transform, true);
            CreateWorldBox("Wall2_East", new Vector3(w2 / 2f + 0.15f, h2 / 2f, z2Center), new Vector3(0.3f, h2, d2 + 0.4f), matWall, stage2Container.transform, true);
            CreateWorldBox("Wall2_West", new Vector3(-w2 / 2f - 0.15f, h2 / 2f, z2Center), new Vector3(0.3f, h2, d2 + 0.4f), matWall, stage2Container.transform, true);

            // 내부 방 칸막이 벽 (1.6m~1.8m 넓은 통로 확보!)
            // A) 거실 ↔ 주방 (x = -0.20, z: -2.20..+3.90, 통로 z: -0.40..+1.40)
            CreateWorldBox("Part_LivingKitchen_N", new Vector3(-0.20f, h2 / 2f, 2.65f), new Vector3(0.22f, h2, 2.50f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_LivingKitchen_S", new Vector3(-0.20f, h2 / 2f, -1.30f), new Vector3(0.22f, h2, 1.80f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_LivingKitchen_Lintel", new Vector3(-0.20f, 2.60f, 0.50f), new Vector3(0.22f, 0.60f, 1.80f), matPartitionWall, stage2Container.transform, true);

            // B) 북쪽(거실/주방) ↔ 남쪽(안방/화장실) (z = -2.20, 안방 통로 x:-2.20..-0.40, 화장실 통로 x:+3.60..+5.20)
            CreateWorldBox("Part_Mid_W", new Vector3(-4.95f, h2 / 2f, -2.20f), new Vector3(5.50f, h2, 0.22f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_Mid_BedLintel", new Vector3(-1.30f, 2.60f, -2.20f), new Vector3(1.80f, 0.60f, 0.22f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_Mid_Center", new Vector3(1.60f, h2 / 2f, -2.20f), new Vector3(4.00f, h2, 0.22f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_Mid_BathLintel", new Vector3(4.40f, 2.60f, -2.20f), new Vector3(1.60f, 0.60f, 0.22f), matPartitionWall, stage2Container.transform, true);
            CreateWorldBox("Part_Mid_E", new Vector3(6.45f, h2 / 2f, -2.20f), new Vector3(2.50f, h2, 0.22f), matPartitionWall, stage2Container.transform, true);

            // C) 안방 ↔ 화장실 (x = +1.50, z: -8.90..-2.20)
            CreateWorldBox("Part_BedBath", new Vector3(1.50f, h2 / 2f, -5.55f), new Vector3(0.22f, h2, 6.70f), matPartitionWall, stage2Container.transform, true);

            GameObject furnRoot2 = new GameObject("ApartmentKit_Stage2_Interior");
            furnRoot2.transform.SetParent(stage2Container.transform, false);
            BuildStage2ApartmentInterior(furnRoot2.transform, matAppliance, matMattress);

            // 2단계 방별 천장 조명 4개 (순서 엄수: 0:거실, 1:주방, 2:안방, 3:화장실)
            Light[] stage2RoomLights = new[]
            {
                CreateRoomCeilingLight("Stage2_Light_Living",   new Vector3( 3.70f, h2 - 0.20f,  0.80f), 14.0f, stage2Container.transform),
                CreateRoomCeilingLight("Stage2_Light_Kitchen",  new Vector3(-3.90f, h2 - 0.20f,  0.80f), 14.0f, stage2Container.transform),
                CreateRoomCeilingLight("Stage2_Light_Bedroom",  new Vector3(-3.05f, h2 - 0.20f, -5.50f), 14.0f, stage2Container.transform),
                CreateRoomCeilingLight("Stage2_Light_Bathroom", new Vector3( 4.55f, h2 - 0.20f, -5.50f), 13.0f, stage2Container.transform),
            };

            // 4. Outside Yard (z: 3.90 ~ 18.10) + 4-Stage Evolving Vehicle at z = 11.40
            GameObject outsideRoot = new GameObject("Outside_Truck_Area");
            CreateWorldBox("Parking_Floor", new Vector3(2.1f, -0.30f, 11.0f), new Vector3(16.0f, 0.6f, 14.2f), matOutdoorFloor, outsideRoot.transform, true);

            GameObject stepCurb = CreateWorldBox("Door_StepCurb", new Vector3(2.1f, 0.015f, doorZ), new Vector3(1.1f, 0.03f, 0.10f), matCeiling, outsideRoot.transform, true);
            Object.DestroyImmediate(stepCurb.GetComponent<Collider>());

            CreateInvisibleFence("Fence_Outside_North", new Vector3(2.1f, 1.2f, 18.0f), new Vector3(16.0f, 2.4f, 0.15f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_West", new Vector3(-5.8f, 1.2f, 11.0f), new Vector3(0.15f, 2.4f, 14.2f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_East", new Vector3(10.0f, 1.2f, 11.0f), new Vector3(0.15f, 2.4f, 14.2f), outsideRoot.transform);

            GameObject porchLightObj = new GameObject("Porch_Light");
            porchLightObj.transform.SetParent(outsideRoot.transform, false);
            porchLightObj.transform.position = new Vector3(2.10f, 2.55f, 5.1f);
            Light porchLight = porchLightObj.AddComponent<Light>();
            porchLight.type = LightType.Point;
            porchLight.color = new Color(1.0f, 0.94f, 0.80f);
            porchLight.intensity = 12f;
            porchLight.range = 10.0f;
            porchLight.shadows = LightShadows.None;

            GameObject truckRoot = new GameObject("TrashVehicle_Goal");
            truckRoot.transform.SetParent(outsideRoot.transform, false);
            truckRoot.transform.position = new Vector3(2.10f, 0.0f, 11.40f);

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

            // 5. Directional Sunlight (Before → After 햇살 변화 연출용)
            GameObject sunObj = new GameObject("Directional_SunLight");
            sunObj.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light sunLight = sunObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(0.92f, 0.80f, 0.62f);
            sunLight.intensity = 0.45f;
            sunLight.shadows = LightShadows.None;

            SetupPostProcessing();

            // 6A. Stage 1 Phase 2 Floor Stains (8개 — Stage 1 컨테이너 소속!)
            GameObject stainRoot1 = new GameObject("Phase2_FloorStains_Stage1");
            stainRoot1.transform.SetParent(stage1Container.transform, false);
            Vector3[] stainPos1 = new[]
            {
                new Vector3(-0.55f, 0.008f,  0.45f),
                new Vector3( 1.85f, 0.008f, -0.75f),
                new Vector3( 3.10f, 0.009f,  1.25f),
                new Vector3(-2.15f, 0.008f,  1.95f),
                new Vector3( 0.45f, 0.009f, -2.10f),
                new Vector3( 2.85f, 0.008f, -2.55f),
                new Vector3(-3.20f, 0.008f,  0.55f),
                new Vector3( 0.65f, 0.009f,  2.15f),
            };
            for (int i = 0; i < stainPos1.Length; i++)
            {
                bool isMold = i % 2 == 0;
                PlaceCleanableStain(
                    isMold ? $"Stain1_Mold_{i + 1:00}" : $"Stain1_Soup_{i + 1:00}",
                    stainPos1[i],
                    Random.Range(1.25f, 1.65f),
                    isMold ? matMoldDecal : matSoupDecal,
                    stainRoot1.transform
                );
            }

            // 6B. Stage 2 Phase 2 Floor Stains (10개 — 거실·주방·안방·화장실 4방 분산!)
            GameObject stainRoot2 = new GameObject("Phase2_FloorStains_Stage2");
            stainRoot2.transform.SetParent(stage2Container.transform, false);
            Vector3[] stainPos2 = new[]
            {
                // 거실 (3)
                new Vector3( 2.40f, 0.008f,  1.40f),
                new Vector3( 4.60f, 0.009f, -0.35f),
                new Vector3( 1.45f, 0.008f, -0.85f),
                // 주방 (3)
                new Vector3(-2.60f, 0.009f,  1.65f),
                new Vector3(-5.20f, 0.008f,  1.20f),
                new Vector3(-3.80f, 0.009f, -0.80f),
                // 안방 (2)
                new Vector3(-2.20f, 0.008f, -4.60f),
                new Vector3(-4.40f, 0.009f, -6.20f),
                // 화장실 (2)
                new Vector3( 3.60f, 0.008f, -4.40f),
                new Vector3( 5.20f, 0.009f, -6.20f),
            };
            for (int i = 0; i < stainPos2.Length; i++)
            {
                bool isMold = i % 2 == 0;
                PlaceCleanableStain(
                    isMold ? $"Stain2_Mold_{i + 1:00}" : $"Stain2_Soup_{i + 1:00}",
                    stainPos2[i],
                    Random.Range(1.30f, 1.75f),
                    isMold ? matMoldDecal : matSoupDecal,
                    stainRoot2.transform
                );
            }

            // 7. Phase 1 Big Breakable Piles (Stage 1: 14개, Stage 2: 20개)
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

            GameObject trashRoot1 = new GameObject("Phase1_BigBreakablePiles_Stage1");
            trashRoot1.transform.SetParent(stage1Container.transform, false);
            Vector3[] pilePos1 = new[]
            {
                new Vector3( 2.85f, 0f,  1.25f),
                new Vector3( 1.65f, 0f,  0.95f),
                new Vector3( 3.10f, 0f, -0.65f),
                new Vector3( 2.75f, 0f, -1.95f),
                new Vector3( 1.25f, 0f, -0.45f),
                new Vector3( 0.35f, 0f,  1.55f),
                new Vector3(-1.15f, 0f,  1.95f),
                new Vector3(-2.65f, 0f,  1.85f),
                new Vector3(-0.85f, 0f,  0.35f),
                new Vector3( 0.55f, 0f, -1.45f),
                new Vector3(-3.15f, 0f,  0.65f),
                new Vector3(-1.65f, 0f, -0.95f),
                new Vector3(-3.35f, 0f, -0.45f),
                new Vector3( 3.45f, 0f, -2.65f),
            };
            SpawnBigPilesAtPositions(pilePos1, pileAssets, pileNames, trashRoot1.transform, "Pile1");

            GameObject trashRoot2 = new GameObject("Phase1_BigBreakablePiles_Stage2");
            trashRoot2.transform.SetParent(stage2Container.transform, false);
            Vector3[] pilePos2 = new[]
            {
                // 거실 (5)
                new Vector3( 2.80f, 0f,  2.10f),
                new Vector3( 4.90f, 0f,  1.50f),
                new Vector3( 1.30f, 0f,  0.50f),
                new Vector3( 3.50f, 0f, -0.90f),
                new Vector3( 5.40f, 0f, -1.10f),
                // 주방 (5)
                new Vector3(-1.80f, 0f,  2.20f),
                new Vector3(-4.20f, 0f,  1.80f),
                new Vector3(-6.20f, 0f,  1.10f),
                new Vector3(-2.40f, 0f, -0.70f),
                new Vector3(-5.50f, 0f, -1.00f),
                // 안방 (6)
                new Vector3(-1.20f, 0f, -3.90f),
                new Vector3(-3.60f, 0f, -4.10f),
                new Vector3(-5.80f, 0f, -4.40f),
                new Vector3(-0.60f, 0f, -6.50f),
                new Vector3(-3.40f, 0f, -6.90f),
                new Vector3(-1.80f, 0f, -7.40f),
                // 화장실 (4)
                new Vector3( 3.10f, 0f, -3.80f),
                new Vector3( 5.60f, 0f, -3.70f),
                new Vector3( 3.80f, 0f, -5.80f),
                new Vector3( 5.10f, 0f, -6.60f),
            };
            SpawnBigPilesAtPositions(pilePos2, pileAssets, pileNames, trashRoot2.transform, "Pile2");

            // 2단계 컨테이너는 1단계 시작 시 비활성화!
            stage2Container.SetActive(false);

            // 8. GPU-Instanced 3D Trash Swarm (1단계 1,650개 / 2단계 8,000개 무렉!)
            GameObject swarmObj = new GameObject("TrashSwarm_Engine");
            var swarm = swarmObj.AddComponent<InstancedTrashSwarm>();
            PopulateSwarmMeshTypes(swarm);

            // 9. Hidden Runtime Template for Thrown Trash Bag (레거시 참조 안전망)
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

            // 10. Player Spawn OUTSIDE facing the Entrance Doorway!
            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.position = new Vector3(2.10f, 0.15f, 5.10f);
            spawnObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            FirstPersonCleanerController playerCtrl = CreateFpsPlayer(
                spawnObj.transform.position, spawnObj.transform.rotation,
                matSkin, matTissue, matMopStick, matMopHead, matVacuumBody, matVacuumMetal
            );

            // 11. Manager, Truck Visual & HUD Wiring
            GameObject sysObj = new GameObject("GamePrototypeManager");
            var mgr = sysObj.AddComponent<GamePrototypeManager>();
            var truckVisual = sysObj.AddComponent<InstancedTruckTrash>();
            mgr.player = playerCtrl;
            mgr.trashSwarm = swarm;
            mgr.truckTrashVisual = truckVisual;
            mgr.playerSpawnPoint = spawnObj.transform;
            mgr.truckBedGoal = goalAnchor.transform;
            mgr.vehicleStageObjects = vehicleStages;
            mgr.truckStackedParent = stackedParent.transform;
            mgr.ceilingLight = s1CeilingLight;
            mgr.sunLight = sunLight;
            mgr.thrownBagPrefab = thrownBagTmpl;
            mgr.stage1Container = stage1Container;
            mgr.stage2Container = stage2Container;
            mgr.stage1RoomLights = stage1RoomLights;
            mgr.stage2RoomLights = stage2RoomLights;
            mgr.doorThresholdZ = doorZ;

            doorCtrl.manager = mgr;
            swarm.manager = mgr;
            playerCtrl.manager = mgr;

            var hud = sysObj.AddComponent<PrototypeHudUI>();
            hud.manager = mgr;
            hud.controller = playerCtrl;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashCleaner3D v1.4] Stage 1(+30% 원룸 1,650개) + Stage 2(4방 아파트 8,000개 무렉) + 1:1 트럭 적재 씬 빌드 완료!");
        }

        private static Light CreateRoomCeilingLight(string name, Vector3 pos, float range, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.95f, 0.78f, 0.54f);
            l.intensity = 9.5f;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        private static void SpawnBigPilesAtPositions(Vector3[] positions, GameObject[] pileAssets, string[] pileNames, Transform parent, string prefix)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                int k = i % pileAssets.Length;
                GameObject src = pileAssets[k];
                if (src == null) continue;
                bool isBoxLike = (k == 3 || k == 4);
                float size = isBoxLike ? Random.Range(0.62f, 0.78f) : Random.Range(0.82f, 1.05f);
                GameObject pile = SpawnNormalizedModel(
                    src, $"{prefix}_{i + 1:00}",
                    positions[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    size, parent
                );
                if (pile != null)
                {
                    var ct = AddCleanable(pile, CleanableKind.TrashPile, pileNames[k], 10.0f, 0, 0, 2.0f);
                    ct.burstSpawnCount = 22;
                    ct.containsTreasureChance = true;
                }
            }
        }

        private static void BuildStage1OneRoomInterior(Transform parent, Material fallbackAppliance, Material fallbackMattress)
        {
            // +30% 확장 원룸 (x: -4.68..+4.68, z: -3.90..+3.90) 핵심 가구 5종 (냉장고·전자레인지·싱크대·침대·책상)
            Bounds fridgeBounds;
            bool hasFridge = PlaceAptPrefab("Furniture/Kitchen/Appliances/Fridge_01.prefab", "Apt_Fridge",
                new Vector3(0.75f, 0f, 3.35f), 180f, parent, true, -4.55f, 1.42f, -3.78f, 3.78f, out fridgeBounds);
            if (!hasFridge)
            {
                GameObject fb = CreateWorldBox("Fridge", new Vector3(0.75f, 0.78f, 3.40f), new Vector3(0.70f, 1.56f, 0.70f), fallbackAppliance, parent, true);
                fridgeBounds = fb.GetComponent<Renderer>().bounds;
            }
            BuildRealisticMicrowave(new Vector3(fridgeBounds.center.x, fridgeBounds.max.y, fridgeBounds.center.z), parent);

            PlaceAptPrefab("Furniture/Kitchen/Cabinets/Cabinet_Base_Sink_01.prefab", "Apt_KitchenSink",
                new Vector3(-0.55f, 0f, 3.40f), 180f, parent, true, -4.55f, 1.42f, -3.78f, 3.78f, out _);

            bool hasBed = PlaceAptPrefab("Furniture/Bedroom/Bed_Apt_01_01.prefab", "Apt_Bed",
                new Vector3(-3.35f, 0f, -2.55f), 90f, parent, true, -4.55f, 4.55f, -3.78f, 3.78f, out _);
            if (!hasBed)
            {
                CreateWorldBox("Mattress", new Vector3(-3.35f, 0.16f, -2.55f), new Vector3(1.75f, 0.32f, 2.25f), fallbackMattress, parent, true);
            }

            PlaceAptPrefab("Furniture/Living Room/Table_Computer_01_Setup.prefab", "Apt_ComputerDesk",
                new Vector3(0.65f, 0f, -3.30f), 0f, parent, true, -4.55f, 4.55f, -3.78f, 3.78f, out _);
        }

        private static void BuildStage2ApartmentInterior(Transform parent, Material fallbackAppliance, Material fallbackMattress)
        {
            // 1) 거실 (x: -0.05..+7.55, z: -2.05..+3.78)
            PlaceAptPrefab("Furniture/Living Room/Sofa_Apt_01.prefab", "St2_Living_Sofa",
                new Vector3(6.75f, 0f, 0.85f), -90f, parent, true, 0.15f, 7.55f, -2.02f, 3.75f, out _);
            PlaceAptPrefab("Furniture/Living Room/Table_Media_01.prefab", "St2_Living_TVStand",
                new Vector3(5.10f, 0f, 3.38f), 180f, parent, true, 2.80f, 7.55f, -2.02f, 3.75f, out _);

            // 2) 주방 (x: -7.55..-0.35, z: -2.05..+3.78)
            Bounds fridge2;
            bool hasF2 = PlaceAptPrefab("Furniture/Kitchen/Appliances/Fridge_01.prefab", "St2_Kitchen_Fridge",
                new Vector3(-6.85f, 0f, 3.25f), 180f, parent, true, -7.55f, -0.42f, -2.02f, 3.75f, out fridge2);
            if (!hasF2)
            {
                GameObject fb = CreateWorldBox("St2_Fridge", new Vector3(-6.85f, 0.78f, 3.30f), new Vector3(0.70f, 1.56f, 0.70f), fallbackAppliance, parent, true);
                fridge2 = fb.GetComponent<Renderer>().bounds;
            }
            BuildRealisticMicrowave(new Vector3(fridge2.center.x, fridge2.max.y, fridge2.center.z), parent);

            PlaceAptPrefab("Furniture/Kitchen/Cabinets/Cabinet_Base_Sink_01.prefab", "St2_Kitchen_Sink",
                new Vector3(-5.15f, 0f, 3.38f), 180f, parent, true, -7.55f, -0.42f, -2.02f, 3.75f, out _);
            PlaceAptPrefab("Furniture/Kitchen/Appliances/Stove_01.prefab", "St2_Kitchen_Stove",
                new Vector3(-3.65f, 0f, 3.35f), 180f, parent, true, -7.55f, -0.42f, -2.02f, 3.75f, out _);

            // 3) 안방 (x: -7.55..+1.35, z: -8.75..-2.35)
            bool hasBed2 = PlaceAptPrefab("Furniture/Bedroom/Bed_Apt_01_01.prefab", "St2_Bedroom_Bed",
                new Vector3(-6.05f, 0f, -7.20f), 90f, parent, true, -7.55f, 1.32f, -8.75f, -2.42f, out _);
            if (!hasBed2)
            {
                CreateWorldBox("St2_Mattress", new Vector3(-6.05f, 0.16f, -7.20f), new Vector3(1.75f, 0.32f, 2.25f), fallbackMattress, parent, true);
            }
            PlaceAptPrefab("Furniture/Living Room/Table_Computer_01_Setup.prefab", "St2_Bedroom_Desk",
                new Vector3(-2.55f, 0f, -8.25f), 0f, parent, true, -7.55f, 1.32f, -8.75f, -2.42f, out _);
            PlaceAptPrefab("Furniture/Bedroom/Dresser Closet_01.prefab", "St2_Bedroom_Closet",
                new Vector3(-7.10f, 0f, -3.70f), 90f, parent, true, -7.55f, 1.32f, -8.75f, -2.42f, out _);

            // 4) 화장실 (x: +1.65..+7.55, z: -8.75..-2.35)
            PlaceAptPrefab("Furniture/Bathroom/BathTub_Base_01.prefab", "St2_Bath_Tub",
                new Vector3(6.65f, 0f, -7.30f), -90f, parent, true, 1.70f, 7.55f, -8.75f, -2.42f, out _);
            PlaceAptPrefab("Furniture/Bathroom/Toilet_Apt_01.prefab", "St2_Bath_Toilet",
                new Vector3(2.45f, 0f, -8.25f), 0f, parent, true, 1.70f, 7.55f, -8.75f, -2.42f, out _);
            PlaceAptPrefab("Furniture/Bathroom/BR_Vanity_01.prefab", "St2_Bath_Vanity",
                new Vector3(4.35f, 0f, -8.30f), 0f, parent, true, 1.70f, 7.55f, -8.75f, -2.42f, out _);
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
            Transform parent, bool addSimpleBoxCollider,
            float minX, float maxX, float minZ, float maxZ, out Bounds finalBounds)
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

                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                Vector3 clampShift = Vector3.zero;
                if (b.min.x < minX) clampShift.x = minX - b.min.x;
                else if (b.max.x > maxX) clampShift.x = maxX - b.max.x;
                if (b.min.z < minZ) clampShift.z = minZ - b.min.z;
                else if (b.max.z > maxZ) clampShift.z = maxZ - b.max.z;
                go.transform.position += clampShift;

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
            Material matSkin, Material matTissue,
            Material matMopStick, Material matMopHead,
            Material matVacuumBody, Material matVacuumMetal)
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

            // 1) 1티어: 장갑 낀 맨손 — v1.3 (요청 #2)
            //    회색 직육면체 대신 손바닥·엄지·접힌 손가락 마디가 구분되는 장갑 손 실루엣.
            Material matGlove = CreateSimpleLitMaterial("Mat_Tool_WorkGlove", new Color(0.19f, 0.21f, 0.26f), 0.16f);
            Material matGloveCuff = CreateSimpleLitMaterial("Mat_Tool_GloveCuff", new Color(0.95f, 0.56f, 0.14f), 0.22f);
            GameObject fist = new GameObject("Visual_FistHand");
            fist.transform.SetParent(rightPivot.transform, false);
            GloveHandBuilder.BuildFist(fist.transform, matGlove, matGloveCuff, matSkin);

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

            // v1.3 (요청 #4): 왼손 봉투 비주얼은 삭제됐다. 수거통은 HUD 게이지로만 표시된다.

            var ctrl = player.AddComponent<FirstPersonCleanerController>();
            ctrl.cameraTransform = camObj.transform;
            ctrl.rightToolPivot = rightPivot.transform;
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
