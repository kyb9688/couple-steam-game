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

            // 3. Room 01 Shell (4.8m x 4.0m x 2.6m) with OPEN DOORWAY on North-East (x: 0.95 ~ 2.05)
            GameObject roomRoot = new GameObject("Room01_KoreanStudio");
            float w = 4.8f, d = 4.0f, h = 2.6f;

            CreateWorldBox("Floor", new Vector3(0, -0.30f, 0), new Vector3(w + 0.4f, 0.6f, d + 0.4f), matFloor, roomRoot.transform, true);
            CreateWorldBox("Ceiling", new Vector3(0, h + 0.15f, 0), new Vector3(w + 0.4f, 0.3f, d + 0.4f), matCeiling, roomRoot.transform, true);

            // North Wall split around Open Doorway (x: 0.95 to 2.05, height: 0 to 2.15)
            CreateWorldBox("Wall_North_Left", new Vector3(-0.725f, h / 2f, d / 2f + 0.15f), new Vector3(3.35f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_North_Right", new Vector3(2.275f, h / 2f, d / 2f + 0.15f), new Vector3(0.45f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_North_Header", new Vector3(1.50f, 2.375f, d / 2f + 0.15f), new Vector3(1.15f, 0.45f, 0.3f), matWall, roomRoot.transform, true);

            // Swung-Open Mustard Yellow Door beside doorway
            GameObject openDoor = CreateWorldBox("Door_SwungOpen", new Vector3(2.12f, 1.05f, d / 2f + 0.56f), new Vector3(0.08f, 2.1f, 0.92f), matDoor, roomRoot.transform, true);
            openDoor.transform.rotation = Quaternion.Euler(0f, -14f, 0f);

            CreateWorldBox("Wall_South", new Vector3(0, h / 2f, -d / 2f - 0.15f), new Vector3(w + 0.4f, h, 0.3f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_East", new Vector3(w / 2f + 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);
            CreateWorldBox("Wall_West", new Vector3(-w / 2f - 0.15f, h / 2f, 0), new Vector3(0.3f, h, d + 0.4f), matWall, roomRoot.transform, true);

            // Basic Korean Studio Furniture
            CreateWorldBox("Fridge", new Vector3(0.35f, 0.72f, d / 2f - 0.35f), new Vector3(0.62f, 1.44f, 0.62f), matAppliance, roomRoot.transform, true);
            CreateWorldBox("Microwave", new Vector3(0.35f, 1.59f, d / 2f - 0.35f), new Vector3(0.48f, 0.30f, 0.38f), matCeiling, roomRoot.transform, true);
            CreateWorldBox("Mattress", new Vector3(-1.45f, 0.15f, -0.95f), new Vector3(1.45f, 0.30f, 1.90f), matMattress, roomRoot.transform, true);

            // 4. Outside Ventilation Porch (z: 2.0 ~ 3.55) + Trash Truck Bed (z: 4.35, STRICTLY OUTSIDE!)
            GameObject outsideRoot = new GameObject("Outside_Truck_Area");
            CreateWorldBox("Parking_Floor", new Vector3(1.5f, -0.30f, 4.3f), new Vector3(5.8f, 0.6f, 4.8f), matOutdoorFloor, outsideRoot.transform, true);

            // 낮은 문턱(콜라이더 제거 → 문 밖 환기 구역으로 걸어 나갈 때 걸림 0%)
            GameObject stepCurb = CreateWorldBox("Door_StepCurb", new Vector3(1.5f, 0.015f, 2.02f), new Vector3(1.1f, 0.03f, 0.10f), matCeiling, outsideRoot.transform, true);
            Object.DestroyImmediate(stepCurb.GetComponent<Collider>());

            // 바깥 환기 구역 안전 투명 펜스 (플레이어가 문 밖 z=2.0~3.45로 나가 숨 쉬고 트럭에 던질 수 있되, 맵 밖 추락 방지!)
            CreateInvisibleFence("Fence_Outside_North", new Vector3(1.5f, 1.2f, 3.52f), new Vector3(4.2f, 2.4f, 0.15f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_West", new Vector3(-0.35f, 1.2f, 2.85f), new Vector3(0.15f, 2.4f, 1.8f), outsideRoot.transform);
            CreateInvisibleFence("Fence_Outside_East", new Vector3(3.35f, 1.2f, 2.85f), new Vector3(0.15f, 2.4f, 1.8f), outsideRoot.transform);

            GameObject truckRoot = new GameObject("TrashTruck_Goal");
            truckRoot.transform.SetParent(outsideRoot.transform, false);
            truckRoot.transform.position = new Vector3(1.50f, 0.0f, 4.40f);

            // Truck Cargo Bed (LOCAL coordinates fixed! Never spawns inside the room!)
            GameObject truckBedVisual = new GameObject("TruckBed_Scalable");
            truckBedVisual.transform.SetParent(truckRoot.transform, false);
            truckBedVisual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            truckBedVisual.transform.localScale = new Vector3(1.85f, 0.85f, 1.50f);

            CreateLocalBox("Bed_Bottom", new Vector3(0f, -0.42f, 0f), new Vector3(1f, 0.14f, 1f), matTruckBody, truckBedVisual.transform, false);
            CreateLocalBox("Bed_BackWall", new Vector3(0f, 0f, 0.48f), new Vector3(1f, 1f, 0.08f), matTruckBody, truckBedVisual.transform, false);
            CreateLocalBox("Bed_FrontLip", new Vector3(0f, -0.18f, -0.48f), new Vector3(1f, 0.55f, 0.08f), matTruckGlow, truckBedVisual.transform, false);
            CreateLocalBox("Bed_LeftWall", new Vector3(-0.48f, 0f, 0f), new Vector3(0.08f, 1f, 1f), matTruckBody, truckBedVisual.transform, false);
            CreateLocalBox("Bed_RightWall", new Vector3(0.48f, 0f, 0f), new Vector3(0.08f, 1f, 1f), matTruckBody, truckBedVisual.transform, false);

            // Bright spotlight illuminating truck bed & outside fresh air zone
            GameObject truckLightObj = new GameObject("Truck_BeaconLight");
            truckLightObj.transform.SetParent(truckRoot.transform, false);
            truckLightObj.transform.localPosition = new Vector3(0f, 2.5f, -0.35f);
            Light truckLight = truckLightObj.AddComponent<Light>();
            truckLight.type = LightType.Point;
            truckLight.color = new Color(0.88f, 0.96f, 1.0f);
            truckLight.intensity = 12f;
            truckLight.range = 7.5f;
            truckLight.shadows = LightShadows.None;

            GameObject goalAnchor = new GameObject("GoalCenterAnchor");
            goalAnchor.transform.SetParent(truckRoot.transform, false);
            goalAnchor.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            // 5. Ceiling Light (No real-time shadows -> zero URP shadow atlas warnings!)
            GameObject lightObj = new GameObject("Ceiling_Light_WarmFluorescent");
            lightObj.transform.position = new Vector3(0f, h - 0.2f, 0f);
            Light ceilingLight = lightObj.AddComponent<Light>();
            ceilingLight.type = LightType.Point;
            ceilingLight.color = new Color(1.0f, 0.92f, 0.76f);
            ceilingLight.intensity = 12f;
            ceilingLight.range = 9.5f;
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

            // 7. Phase 1 Big Breakable Piles (12개: 흰 봉투 / 회색 봉투 / 검정 봉투 / 택배박스 / 스티로폼박스)
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
                new Vector3(1.85f, 0f, 0.45f),
                new Vector3(1.80f, 0f, -0.35f),
                new Vector3(1.70f, 0f, -1.15f),
                new Vector3(1.15f, 0f, 0.25f),
                new Vector3(1.10f, 0f, -0.58f),
                new Vector3(0.45f, 0f, 0.62f),
                new Vector3(-0.35f, 0f, 1.32f),
                new Vector3(-1.12f, 0f, 1.25f),
                new Vector3(-0.28f, 0f, 0.35f),
                new Vector3(0.45f, 0f, -0.58f),
                new Vector3(-1.70f, 0f, 0.72f),
                new Vector3(0.95f, 0f, -1.35f),
            };

            for (int i = 0; i < pilePositions.Length; i++)
            {
                int k = i % pileAssets.Length;
                GameObject src = pileAssets[k];
                if (src == null) continue;
                bool isBoxLike = (k == 3 || k == 4);
                float size = isBoxLike ? Random.Range(0.56f, 0.68f) : Random.Range(0.72f, 0.90f);
                GameObject pile = SpawnNormalizedModel(
                    src, $"BreakablePile_{i + 1:00}",
                    pilePositions[i], Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                    size, trashRoot.transform
                );
                if (pile != null)
                {
                    // 초반 주먹 4방(4.0 HP) → 퍽! 터지면서 소형 쓰레기 25개 분수 + 보물 발굴!
                    var ct = AddCleanable(pile, CleanableKind.TrashPile, pileNames[k], 4.0f, 12, 0, 2.0f);
                    ct.burstSpawnCount = 25;
                    ct.containsTreasureChance = true;
                }
            }

            // 8. 1,000+ GPU-Instanced 3D Trash Swarm (Zero-GameObject, Zero-Lag!)
            GameObject swarmObj = new GameObject("TrashSwarm_1000");
            var swarm = swarmObj.AddComponent<InstancedTrashSwarm>();
            PopulateSwarmMeshTypes(swarm);

            // 9. Hidden Runtime Template for Thrown Trash Bag (Basketball Shoot!)
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

            // 10. Player Spawn (Clear open spot looking toward trash mountain & open door truck!)
            GameObject spawnObj = new GameObject("PlayerSpawnPoint");
            spawnObj.transform.position = new Vector3(-0.15f, 0.15f, -1.55f);
            spawnObj.transform.rotation = Quaternion.Euler(0f, 22f, 0f);

            FirstPersonCleanerController playerCtrl = CreateFpsPlayer(
                spawnObj.transform.position, spawnObj.transform.rotation,
                matSkin, matBagBlack, matTissue, matMopStick, matMopHead, matVacuumBody, matVacuumMetal
            );

            // 11. Manager & HUD Wiring
            GameObject sysObj = new GameObject("GamePrototypeManager");
            var mgr = sysObj.AddComponent<GamePrototypeManager>();
            mgr.player = playerCtrl;
            mgr.trashSwarm = swarm;
            mgr.playerSpawnPoint = spawnObj.transform;
            mgr.truckBedGoal = goalAnchor.transform;
            mgr.truckVisualTransform = truckBedVisual.transform;
            mgr.ceilingLight = ceilingLight;
            mgr.thrownBagPrefab = thrownBagTmpl;

            swarm.manager = mgr;
            playerCtrl.manager = mgr;

            var hud = sysObj.AddComponent<PrototypeHudUI>();
            hud.manager = mgr;
            hud.controller = playerCtrl;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TrashCleaner3D v0.3] Room 01 Playable Prototype 생성 완료! 1,000+ GPU-Instanced 3D 쓰레기 + [Tab] 아이콘 마인드맵 + 상단 악취 바 + 문 밖 환기 + 즉시 슈킹 해프닝 준비 완료!");
        }

        /// <summary>
        /// 1,000+ GPU Instancing용 경량 3D 소형 쓰레기 5종 세팅 (모든 머티리얼 enableInstancing = true)
        /// </summary>
        private static void PopulateSwarmMeshTypes(InstancedTrashSwarm swarm)
        {
            string litterFbxDir = $"{ArtRoot}/Models/Trash/street_litter_sampler/fbx";
            string litterTexDir = $"{ArtRoot}/Models/Trash/street_litter_sampler/textures";

            TryAddInstancedType(swarm, "찌그러진 캔",
                $"{litterFbxDir}/SM_PL_Cans_Soda330_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Cans_Soda330_crushed_BaseColor.jpg",
                $"{litterTexDir}/T_SM_PL_Cans_Soda330_crushed_Normal.png",
                new Color(0.85f, 0.25f, 0.22f), 0.16f, 0.45f);

            TryAddInstancedType(swarm, "테이크아웃 컵",
                $"{litterFbxDir}/SM_PL_Takeout_CoffeeCup_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Takeout_CoffeeCup_crushed_BaseColor.jpg",
                $"{litterTexDir}/T_SM_PL_Takeout_CoffeeCup_crushed_Normal.png",
                new Color(0.92f, 0.88f, 0.80f), 0.20f, 0.25f);

            TryAddInstancedType(swarm, "페트병",
                $"{litterFbxDir}/SM_PL_Bottles_Water500_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Bottles_Water500_crushed_BaseColor.png",
                $"{litterTexDir}/T_SM_PL_Bottles_Water500_crushed_Normal.png",
                new Color(0.75f, 0.88f, 0.96f), 0.23f, 0.60f);

            TryAddInstancedType(swarm, "구겨진 종이",
                $"{litterFbxDir}/SM_PL_Paper_BallMedium_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Paper_BallMedium_crushed_BaseColor.jpg",
                $"{litterTexDir}/T_SM_PL_Paper_BallMedium_crushed_Normal.png",
                new Color(0.88f, 0.86f, 0.78f), 0.14f, 0.10f);

            TryAddInstancedType(swarm, "배달 피자박스",
                $"{litterFbxDir}/SM_PL_Takeout_PizzaBox_crushed.fbx",
                $"{litterTexDir}/T_SM_PL_Takeout_PizzaBox_crushed_BaseColor.jpg",
                $"{litterTexDir}/T_SM_PL_Takeout_PizzaBox_crushed_Normal.png",
                new Color(0.82f, 0.68f, 0.48f), 0.30f, 0.14f);

            // Fallback: street_litter_sampler가 없는 환경이라면 기존 PolyHaven 소형 모델 사용
            if (swarm.meshTypes.Count == 0)
            {
                TryAddFallbackFolderType(swarm, "녹슨 캔", $"{ArtRoot}/Models/Trash/can_rusted", 0.17f, new Color(0.75f, 0.42f, 0.25f));
                TryAddFallbackFolderType(swarm, "담뱃갑", $"{ArtRoot}/Models/Trash/cigarette_pack", 0.14f, new Color(0.85f, 0.82f, 0.75f));
                TryAddFallbackFolderType(swarm, "통조림 캔", $"{ArtRoot}/Models/Trash/russian_food_cans_01", 0.22f, new Color(0.78f, 0.72f, 0.55f));
                TryAddFallbackFolderType(swarm, "플라스틱 병", $"{ArtRoot}/Models/Trash/plastic_bottle_gallon", 0.26f, new Color(0.86f, 0.90f, 0.94f));
            }

            // 최후 안전망 (에셋 폴더가 아예 비어 있을 때만 기본 도형 사용)
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
            flash.spotAngle = 66f;
            flash.color = new Color(1f, 0.98f, 0.92f);
            flash.shadows = LightShadows.Soft;
            flash.enabled = true;

            // RIGHT HAND PIVOT (Fist / Vacuum / Tissue / Mop — distinctly animated!)
            GameObject rightPivot = new GameObject("RightHandPivot");
            rightPivot.transform.SetParent(camObj.transform, false);
            rightPivot.transform.localPosition = new Vector3(0.32f, -0.26f, 0.52f);

            // 1) 맨손 주먹 (Phase 1 초반)
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

            // 2) 진공청소기 노즐 (마인드맵 해금 시 폭풍 흡입!)
            GameObject vacuum = new GameObject("Visual_VacuumNozzle");
            vacuum.transform.SetParent(rightPivot.transform, false);
            GameObject vacHandle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vacHandle.transform.SetParent(vacuum.transform, false);
            vacHandle.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);
            vacHandle.transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
            vacHandle.transform.localScale = new Vector3(0.075f, 0.24f, 0.075f);
            Object.DestroyImmediate(vacHandle.GetComponent<Collider>());
            vacHandle.GetComponent<Renderer>().sharedMaterial = matVacuumBody;

            GameObject vacPipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vacPipe.transform.SetParent(vacuum.transform, false);
            vacPipe.transform.localPosition = new Vector3(0f, -0.08f, 0.34f);
            vacPipe.transform.localRotation = Quaternion.Euler(76f, 0f, 0f);
            vacPipe.transform.localScale = new Vector3(0.052f, 0.22f, 0.052f);
            Object.DestroyImmediate(vacPipe.GetComponent<Collider>());
            vacPipe.GetComponent<Renderer>().sharedMaterial = matVacuumMetal;

            GameObject vacNozzle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vacNozzle.transform.SetParent(vacuum.transform, false);
            vacNozzle.transform.localPosition = new Vector3(0f, -0.14f, 0.56f);
            vacNozzle.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            vacNozzle.transform.localScale = new Vector3(0.24f, 0.06f, 0.10f);
            Object.DestroyImmediate(vacNozzle.GetComponent<Collider>());
            vacNozzle.GetComponent<Renderer>().sharedMaterial = matVacuumBody;
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

            // LEFT HAND PIVOT (Compact Growing Garbage Bag — never blocks center view!)
            GameObject leftBag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leftBag.name = "LeftHand_GarbageBag";
            leftBag.transform.SetParent(camObj.transform, false);
            leftBag.transform.localPosition = new Vector3(-0.34f, -0.32f, 0.52f);
            leftBag.transform.localScale = Vector3.one * 0.12f;
            Object.DestroyImmediate(leftBag.GetComponent<Collider>());
            leftBag.GetComponent<Renderer>().sharedMaterial = matBag;
            leftBag.SetActive(false);

            var ctrl = player.AddComponent<FirstPersonCleanerController>();
            ctrl.cameraTransform = camObj.transform;
            ctrl.rightToolPivot = rightPivot.transform;
            ctrl.leftBagPivot = leftBag.transform;
            ctrl.fistHandVisual = fist;
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
