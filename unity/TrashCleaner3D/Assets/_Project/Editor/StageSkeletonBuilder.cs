using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// Stage 2~7의 각 컨셉과 방 사이즈에 맞는 방 뼈대 + 화장실 + 가구·소품 + 닫힌 노란 현관문(방 안쪽으로 열림)을
    /// 원룸(Stage 1) 코드는 전혀 건드리지 않고 독립 씬으로 깔끔하게 생성합니다.
    /// </summary>
    public static class StageSkeletonBuilder
    {
        private const string BaseScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";

        [MenuItem("TrashCleaner/Build Stage Skeleton/★ Build ALL Stage 2-7 & Register Test Scenes")]
        public static void BuildAllAndRegister()
        {
            if (GuardPlayMode()) return;
            BuildAllAndRegisterSilent();

            EditorUtility.DisplayDialog(
                "Stage 2~7 버그 수정 & 일괄 재빌드 완료!",
                "원룸(Stage 1)은 그대로 유지하고,\nStage 2~7의 공중 쓰레기·문 열림 방향·투명 벽·계단 버그를 모두 수정해 재등록했습니다!\n\n이제 Play(▶) 후 7종 스테이지 마인드맵 또는 [F1] ~ [F7]으로 확인해 보세요!",
                "확인"
            );
        }

        /// <summary>배치모드/WebGL 빌드에서 다이얼로그 없이 Stage 2~7 씬을 생성하고 7종 전체를 Build Settings에 등록합니다.</summary>
        public static string[] BuildAllAndRegisterSilent()
        {
            BuildStage2Internal(false);
            BuildStage3Internal(false);
            BuildStage4Internal(false);
            BuildStage5Internal(false);
            BuildStage6Internal(false);
            BuildStage7Internal(false);

            string[] paths = new[]
            {
                "Assets/_Project/Scenes/Room01_PlayablePrototype.unity",
                "Assets/_Project/Scenes/Stage02_Layout_Eunju.unity",
                "Assets/_Project/Scenes/Stage03_Layout_Eunju.unity",
                "Assets/_Project/Scenes/Stage04_Layout_Eunju.unity",
                "Assets/_Project/Scenes/Stage05_Layout_Eunju.unity",
                "Assets/_Project/Scenes/Stage06_Layout_Eunju.unity",
                "Assets/_Project/Scenes/Stage07_Layout_Eunju.unity"
            };
            var list = new List<EditorBuildSettingsScene>();
            foreach (string p in paths)
            {
                if (File.Exists(Path.GetFullPath(p)))
                    list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
            if (File.Exists(Path.GetFullPath(paths[0])))
                EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
            return paths;
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 2 - 택배 중독 오피스텔 (4.2x6.8m)")]
        public static void BuildStage2() { if (!GuardPlayMode()) BuildStage2Internal(true); }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 3 - 치킨집 주방 (5.5x5.0m)")]
        public static void BuildStage3() { if (!GuardPlayMode()) BuildStage3Internal(true); }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 4 - 육아 전쟁 맞벌이 집 (7.2x5.8m)")]
        public static void BuildStage4() { if (!GuardPlayMode()) BuildStage4Internal(true); }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 5 - 스타트업 사무실 (8.5x7.0m)")]
        public static void BuildStage5() { if (!GuardPlayMode()) BuildStage5Internal(true); }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 6 - 시골집 농막 (4.2x3.6m)")]
        public static void BuildStage6() { if (!GuardPlayMode()) BuildStage6Internal(true); }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 7 - 졸부 3층 복층 펜트하우스 (10x8m, 3층)")]
        public static void BuildStage7() { if (!GuardPlayMode()) BuildStage7Internal(true); }

        private static bool GuardPlayMode()
        {
            if (!EditorApplication.isPlaying) return false;
            EditorUtility.DisplayDialog(
                "Play(재생) 중에는 방을 생성할 수 없습니다!",
                "먼저 화면 가운데 위 Play(▶) 버튼을 한 번 눌러 정지(■)한 뒤에\n메뉴를 다시 클릭해 주세요!",
                "확인"
            );
            return true;
        }

        private static void BuildStage2Internal(bool showDialog)
        {
            Transform root = PrepareScene(2, "Stage02_Officetel", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.95f, 0.94f, 0.90f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.78f, 0.70f, 0.56f));
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.75f, 0.75f, 0.75f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.92f, 0.92f, 0.90f));
            Material glass = CreateGlassMat();

            float w = 4.2f, d = 6.8f, h = 2.7f;
            BuildShell(root, w, d, h, 1.10f, wall, floor, ceil);
            CreateBox("Floor_EntranceCorridor", new Vector3(1.10f, 0.012f, 2.25f), new Vector3(1.80f, 0.016f, 2.30f), tile, root, false);
            CreateBox("Corridor_PartitionWall", new Vector3(0.05f, h / 2f, 2.25f), new Vector3(0.20f, h, 2.20f), wall, root, true);
            CreateBox("Large_WindowGlass", new Vector3(0f, 1.45f, -d / 2f + 0.04f), new Vector3(3.2f, 2.1f, 0.04f), glass, root, false);
            AddLight(root, new Vector3(0f, h - 0.25f, 0f), 14f, 14f);

            // 북서쪽 화장실 칸 (두꺼운 벽 0.20m) — 바닥 타일은 Floor_Main(y=0) 위로 2cm 명확히 띄워 Z-fighting 완전 제거!
            CreateBox("Floor_BathroomTile", new Vector3(-1.30f, 0.012f, 2.55f), new Vector3(1.50f, 0.016f, 1.60f), tile, root, false);
            CreateBox("BathWall_South", new Vector3(-1.30f, h / 2f, 1.70f), new Vector3(1.50f, h, 0.20f), wall, root, true);
            CreateBox("BathWall_East", new Vector3(-0.55f, h / 2f, 2.95f), new Vector3(0.20f, h, 0.90f), wall, root, true);

            Transform furn = root.Find("Furniture_Stage02_Eunju");
            PlacePrefabByName("RR_Toilet_01", new Vector3(-1.65f, 0f, 2.85f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(-1.65f, 0f, 2.15f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Bed_Apt_01_01", new Vector3(-1.15f, 0f, -2.10f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Fridge_01", new Vector3(-1.65f, 0f, 0.85f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-1.55f, 0f, -0.25f), new Vector3(0, 90, 0), furn);

            // 문 안쪽 1.5m는 비우고 중앙~안쪽에 택배박스 배치
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.35f, 0f, 0.6f), new Vector3(0, 15, 0), 0.55f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.40f, 0f, -0.1f), new Vector3(0, -25, 0), 0.65f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.35f, 0.55f, 0.2f), new Vector3(0, 40, 0), 0.48f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(0.3f, 0f, -1.0f), new Vector3(0, 10, 0), 0.65f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/pizza_box", new Vector3(0.5f, 0f, -1.9f), new Vector3(0, 30, 0), 0.12f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(1.85f, 2.35f, 3.05f), new Vector3(25, -145, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage02_Layout_Eunju.unity", "Stage 2 오피스텔 (4.2x6.8m)", showDialog);
        }

        private static void BuildStage3Internal(bool showDialog)
        {
            Transform root = PrepareScene(3, "Stage03_ChickenKitchen", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.86f, 0.88f, 0.88f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.68f, 0.70f, 0.70f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.88f, 0.88f, 0.86f));
            Material counter = CreateColorMat("Mat_Kitchen_Steel", new Color(0.72f, 0.75f, 0.78f), 0.7f);

            float w = 5.5f, d = 5.0f, h = 2.8f;
            BuildShell(root, w, d, h, 1.55f, wall, floor, ceil);
            CreateBox("Counter_West", new Vector3(-2.25f, 0.45f, -0.4f), new Vector3(0.80f, 0.90f, 3.6f), counter, root, true);
            CreateBox("Counter_South", new Vector3(-0.3f, 0.45f, -2.00f), new Vector3(3.4f, 0.90f, 0.80f), counter, root, true);
            CreateBox("PickupCounter_Front", new Vector3(-1.05f, 0.50f, 1.50f), new Vector3(2.2f, 1.0f, 0.65f), counter, root, true);
            AddLight(root, new Vector3(0f, h - 0.2f, 0f), 16f, 14f);

            Transform furn = root.Find("Furniture_Stage03_Eunju");
            PlacePrefabByName("Fridge_01", new Vector3(2.15f, 0f, -1.90f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-2.15f, 0f, 1.55f), new Vector3(0, 90, 0), furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pan_01", new Vector3(-2.25f, 0.90f, -0.8f), new Vector3(0, 35, 0), 0.14f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pot_01", new Vector3(-2.25f, 0.90f, 0.2f), new Vector3(0, -20, 0), 0.26f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pot_01", new Vector3(-0.6f, 0.90f, -2.00f), new Vector3(0, 15, 0), 0.28f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.9f, 0f, -0.8f), new Vector3(0, -10, 0), 0.42f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/styrofoam_container", new Vector3(-1.05f, 1.0f, 1.50f), new Vector3(0, 25, 0), 0.15f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(2.4f, 2.45f, 2.1f), new Vector3(25, -135, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage03_Layout_Eunju.unity", "Stage 3 치킨집 주방 (5.5x5.0m)", showDialog);
        }

        private static void BuildStage4Internal(bool showDialog)
        {
            Transform root = PrepareScene(4, "Stage04_FamilyHome", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.96f, 0.94f, 0.88f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.82f, 0.72f, 0.56f));
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.78f, 0.78f, 0.76f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.94f, 0.93f, 0.90f));
            Material mat = CreateColorMat("Mat_BabyPlayMat", new Color(0.86f, 0.82f, 0.68f), 0.1f);

            float w = 7.2f, d = 5.8f, h = 2.7f;
            BuildShell(root, w, d, h, 2.20f, wall, floor, ceil);
            CreateBox("Floor_EntranceTile", new Vector3(2.20f, 0.012f, 2.25f), new Vector3(1.6f, 0.016f, 1.2f), tile, root, false);
            CreateBox("Bedroom_Wall_North", new Vector3(-2.1f, h / 2f, 0.55f), new Vector3(2.9f, h, 0.22f), wall, root, true);
            CreateBox("Bedroom_Wall_East_Part", new Vector3(-0.65f, h / 2f, -1.65f), new Vector3(0.22f, h, 2.4f), wall, root, true);
            CreateBox("LivingRoom_PlayMat", new Vector3(1.3f, 0.012f, -0.5f), new Vector3(2.6f, 0.016f, 2.2f), mat, root, false);
            AddLight(root, new Vector3(1.2f, h - 0.25f, 0f), 16f, 16f);
            AddLight(root, new Vector3(-2.1f, h - 0.25f, -1.0f), 12f, 12f);

            // 북서쪽 화장실 칸
            CreateBox("Floor_BathroomTile", new Vector3(-2.65f, 0.012f, 1.95f), new Vector3(1.80f, 0.016f, 1.80f), tile, root, false);
            CreateBox("BathWall_South", new Vector3(-2.65f, h / 2f, 1.05f), new Vector3(1.80f, h, 0.20f), wall, root, true);
            CreateBox("BathWall_East", new Vector3(-1.75f, h / 2f, 2.30f), new Vector3(0.20f, h, 1.15f), wall, root, true);

            Transform furn = root.Find("Furniture_Stage04_Eunju");
            PlacePrefabByName("RR_Toilet_01", new Vector3(-3.15f, 0f, 2.40f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(-3.15f, 0f, 1.55f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("BathTub_Base_01", new Vector3(-2.25f, 0f, 2.30f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Bed_Apt_02_01", new Vector3(-2.20f, 0f, -1.70f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Fridge_01", new Vector3(0.35f, 0f, 2.40f), new Vector3(0, 180, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-0.65f, 0f, 2.40f), new Vector3(0, 180, 0), furn);
            PlacePrefabByName("BPS_Table_01", new Vector3(2.35f, 0f, -1.95f), new Vector3(0, 0, 0), furn);

            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(2.6f, 0f, 0.5f), new Vector3(0, 20, 0), 0.50f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/water_bottles", new Vector3(0.8f, 0f, -0.4f), new Vector3(0, -15, 0), 0.35f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/all_purpose_cleaner", new Vector3(2.9f, 0f, -2.2f), new Vector3(0, 45, 0), 0.28f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/plastic_broom", new Vector3(0.0f, 0f, 2.25f), new Vector3(10, 0, 8), 1.25f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(3.2f, 2.4f, -2.5f), new Vector3(20, -45, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage04_Layout_Eunju.unity", "Stage 4 육아 전쟁 맞벌이 집 (7.2x5.8m)", showDialog);
        }

        private static void BuildStage5Internal(bool showDialog)
        {
            Transform root = PrepareScene(5, "Stage05_StartupOffice", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.92f, 0.93f, 0.95f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.45f, 0.48f, 0.52f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.92f, 0.92f, 0.94f));
            Material glass = CreateGlassMat();

            float w = 8.5f, d = 7.0f, h = 2.9f;
            BuildShell(root, w, d, h, 2.60f, wall, floor, ceil);
            CreateBox("CEORoom_Glass_North", new Vector3(-2.75f, h / 2f, -0.5f), new Vector3(2.9f, h, 0.20f), glass, root, true);
            CreateBox("CEORoom_Glass_East", new Vector3(-1.30f, h / 2f, -2.45f), new Vector3(0.20f, h, 2.0f), glass, root, true);
            CreateBox("Pantry_Divider", new Vector3(-2.2f, h / 2f, 2.0f), new Vector3(0.22f, h, 2.7f), wall, root, true);
            AddLight(root, new Vector3(1.0f, h - 0.25f, 0f), 18f, 18f);
            AddLight(root, new Vector3(-2.5f, h - 0.25f, -1.5f), 14f, 14f);

            Transform furn = root.Find("Furniture_Stage05_Eunju");
            float[] deskXs = new[] { 0.2f, 1.8f, 3.3f };
            foreach (float dx in deskXs)
            {
                PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(dx, 0f, 0.5f), new Vector3(0, 0, 0), 0.78f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Props/classic_laptop", new Vector3(dx, 0.78f, 0.5f), new Vector3(0, 180, 0), 0.28f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(dx, 0f, -0.8f), new Vector3(0, 180, 0), 0.78f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Props/binder_notebook", new Vector3(dx + 0.25f, 0.78f, -0.8f), new Vector3(0, 25, 0), 0.12f, furn);
            }
            PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(-2.8f, 0f, -2.2f), new Vector3(0, 0, 0), 0.80f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/classic_laptop", new Vector3(-2.8f, 0.80f, -2.2f), new Vector3(0, 15, 0), 0.30f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(3.9f, 2.55f, 3.1f), new Vector3(25, -135, 0), 0.32f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(-3.9f, 2.55f, -3.1f), new Vector3(25, 45, 0), 0.32f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage05_Layout_Eunju.unity", "Stage 5 스타트업 사무실 (8.5x7.0m)", showDialog);
        }

        private static void BuildStage6Internal(bool showDialog)
        {
            Transform root = PrepareScene(6, "Stage06_RuralCabin", out Scene scene);
            Material wood = CreateColorMat("Mat_Cabin_OldWood", new Color(0.52f, 0.40f, 0.28f), 0.12f);
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.58f, 0.48f, 0.36f));
            Material ceil = CreateColorMat("Mat_Cabin_Roof", new Color(0.45f, 0.36f, 0.26f), 0.1f);

            float w = 4.2f, d = 3.6f, h = 2.5f;
            BuildShell(root, w, d, h, 1.10f, wood, floor, ceil);
            CreateBox("Outdoor_WoodenDeck", new Vector3(0.8f, 0.01f, 2.4f), new Vector3(3.2f, 0.04f, 1.2f), floor, root, false);
            AddLight(root, new Vector3(0f, h - 0.25f, 0f), 11f, 10f);

            Transform furn = root.Find("Furniture_Stage06_Eunju");
            PlaceAsset("Assets/_Project/Art/Models/Props/Barrel_01", new Vector3(-1.35f, 0f, -1.05f), new Vector3(0, 20, 0), 0.92f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Barrel_01", new Vector3(-1.30f, 0f, -0.05f), new Vector3(0, -40, 0), 0.92f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.35f, 0f, -1.15f), new Vector3(0, 15, 0), 0.45f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.35f, 0.45f, -1.15f), new Vector3(0, -10, 0), 0.45f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/dustpan", new Vector3(0.3f, 0f, -1.15f), new Vector3(0, 30, 0), 0.22f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/plastic_broom", new Vector3(-1.7f, 0f, 1.1f), new Vector3(8, 0, -8), 1.25f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(1.7f, 2.2f, 1.45f), new Vector3(20, -135, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage06_Layout_Eunju.unity", "Stage 6 시골집 농막 (4.2x3.6m)", showDialog);
        }

        private static void BuildStage7Internal(bool showDialog)
        {
            Transform root = PrepareScene(7, "Stage07_Penthouse_3F", out Scene scene);
            Material wall = CreateColorMat("Mat_Penthouse_CreamMarble", new Color(0.94f, 0.91f, 0.86f), 0.35f);
            Material floor = CreateColorMat("Mat_Penthouse_MarbleFloor", new Color(0.86f, 0.84f, 0.80f), 0.65f);
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.95f, 0.94f, 0.92f));
            Material glass = CreateGlassMat();
            Material goldTrim = CreateColorMat("Mat_Penthouse_Gold", new Color(0.88f, 0.70f, 0.22f), 0.8f);
            Material stairMat = CreateColorMat("Mat_Penthouse_Stairs", new Color(0.32f, 0.25f, 0.20f), 0.4f);

            float w = 10.0f, d = 8.0f, fh = 3.0f, totalH = fh * 3f;

            // 두꺼운 외벽 0.40m
            CreateBox("Floor_1F", new Vector3(0f, -0.20f, 0f), new Vector3(w + 0.8f, 0.4f, d + 0.8f), floor, root, true);
            CreateBox("Ceiling_3F", new Vector3(0f, totalH + 0.20f, 0f), new Vector3(w + 0.8f, 0.4f, d + 0.8f), ceil, root, true);
            CreateBox("Wall_South_Full", new Vector3(0f, totalH / 2f, -d / 2f - 0.20f), new Vector3(w + 0.8f, totalH, 0.4f), wall, root, true);
            CreateBox("Wall_East_Full", new Vector3(w / 2f + 0.20f, totalH / 2f, 0f), new Vector3(0.4f, totalH, d + 0.8f), wall, root, true);
            CreateBox("Wall_West_Full", new Vector3(-w / 2f - 0.20f, totalH / 2f, 0f), new Vector3(0.4f, totalH, d + 0.8f), wall, root, true);

            CreateBox("Wall_North_Left", new Vector3(-1.37f, totalH / 2f, d / 2f + 0.20f), new Vector3(8.06f, totalH, 0.4f), wall, root, true);
            CreateBox("Wall_North_Right", new Vector3(4.57f, totalH / 2f, d / 2f + 0.20f), new Vector3(1.66f, totalH, 0.4f), wall, root, true);
            CreateBox("Wall_North_Top", new Vector3(3.20f, (2.15f + totalH) * 0.5f, d / 2f + 0.20f), new Vector3(1.12f, totalH - 2.15f, 0.4f), wall, root, true);
            SetupClosedEntranceDoor(root, 3.20f, d);

            CreateBox("1F_PartyBar_Counter", new Vector3(3.4f, 0.55f, -1.2f), new Vector3(0.8f, 1.1f, 3.4f), goldTrim, root, true);
            CreateBox("1F_Panorama_Glass", new Vector3(0f, 1.55f, -d / 2f + 0.04f), new Vector3(6.8f, 2.4f, 0.04f), glass, root, false);

            // 2층 바닥 + 부드러운 16칸 계단
            CreateBox("Floor_2F_Main", new Vector3(1.1f, fh, 0f), new Vector3(7.8f, 0.22f, d), floor, root, true);
            CreateBox("Floor_2F_WestLanding", new Vector3(-3.9f, fh, 2.3f), new Vector3(2.2f, 0.22f, 3.4f), floor, root, true);
            CreateBox("2F_Stair_Railing", new Vector3(-2.75f, fh + 0.55f, -1.5f), new Vector3(0.14f, 0.9f, 4.2f), glass, root, true);
            BuildStairs(root, "Stairs_1F_to_2F", new Vector3(-3.9f, 0f, -3.2f), fh + 0.11f, 3.8f, 16, stairMat);

            // 3층 바닥 + 부드러운 16칸 계단 + VIP 금고방
            CreateBox("Floor_3F_Main", new Vector3(-1.1f, fh * 2f, 0f), new Vector3(7.8f, 0.22f, d), floor, root, true);
            CreateBox("Floor_3F_EastLanding", new Vector3(3.9f, fh * 2f, 2.3f), new Vector3(2.2f, 0.22f, 3.4f), floor, root, true);
            CreateBox("3F_Stair_Railing", new Vector3(2.75f, fh * 2f + 0.55f, -1.5f), new Vector3(0.14f, 0.9f, 4.2f), glass, root, true);
            BuildStairs(root, "Stairs_2F_to_3F", new Vector3(3.9f, fh + 0.11f, -3.2f), fh, 3.8f, 16, stairMat);

            CreateBox("3F_VaultRoom_North", new Vector3(-3.4f, fh * 2f + fh / 2f, -1.0f), new Vector3(3.2f, fh, 0.22f), wall, root, true);
            CreateBox("3F_VaultRoom_East", new Vector3(-1.8f, fh * 2f + fh / 2f, -2.9f), new Vector3(0.22f, fh, 2.2f), wall, root, true);

            // 2층 럭셔리 화장실
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.84f, 0.84f, 0.82f));
            CreateBox("2F_BathroomTile", new Vector3(3.65f, fh + 0.12f, 2.70f), new Vector3(2.30f, 0.02f, 2.20f), tile, root, false);
            CreateBox("2F_BathWall_South", new Vector3(3.65f, fh + fh / 2f, 1.60f), new Vector3(2.30f, fh, 0.20f), wall, root, true);
            CreateBox("2F_BathWall_West", new Vector3(2.50f, fh + fh / 2f, 3.05f), new Vector3(0.20f, fh, 1.50f), wall, root, true);

            AddLight(root, new Vector3(0.5f, fh - 0.3f, 0f), 18f, 16f);
            AddLight(root, new Vector3(0.5f, fh * 2f - 0.3f, 0f), 18f, 16f);
            AddLight(root, new Vector3(-1.5f, fh * 3f - 0.3f, -1.0f), 16f, 16f);

            Transform furn = root.Find("Furniture_Stage07_Eunju");
            PlacePrefabByName("BarCabinet_Apt_01_Filled Variant", new Vector3(4.25f, 0f, -1.2f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("BPS_Table_01", new Vector3(0f, 0f, -1.2f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Lamp_Table_Apt_01", new Vector3(0.3f, 0.75f, -1.2f), new Vector3(0, 0, 0), furn);
            for (int i = 0; i < 4; i++)
            {
                PlaceAsset("Assets/_Project/Art/Models/Furniture/bar_chair_round_01", new Vector3(2.4f, 0f, -2.3f + i * 0.75f), new Vector3(0, 90, 0), 0.78f, furn);
            }
            float y2f = fh + 0.11f;
            PlacePrefabByName("RR_Toilet_01", new Vector3(4.35f, y2f, 3.30f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(4.35f, y2f, 2.35f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("BathTub_Base_01", new Vector3(3.20f, y2f, 3.20f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Bed_Apt_02_01", new Vector3(0.2f, y2f, -2.4f), new Vector3(0, 0, 0), furn);

            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(0.5f, fh - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(0.5f, fh * 2f - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(-0.5f, fh * 3f - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Valuables/usd_money", new Vector3(-3.6f, fh * 2f + 0.11f, -2.6f), new Vector3(0, 25, 0), 0.16f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Valuables/pocket_watch", new Vector3(-3.2f, fh * 2f + 0.11f, -2.4f), new Vector3(0, -15, 0), 0.14f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(4.5f, 2.65f, 3.6f), new Vector3(25, -135, 0), 0.34f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(-4.5f, fh * 2f + 2.65f, -0.7f), new Vector3(25, 45, 0), 0.34f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage07_Layout_Eunju.unity", "Stage 7 졸부 3층 복층 펜트하우스 (10x8m, 3층)", showDialog);
        }

        // --- Helpers ---

        private static Transform PrepareScene(int stageNum, string rootName, out Scene scene)
        {
            if (File.Exists(Path.GetFullPath(BaseScenePath)))
                EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
            scene = SceneManager.GetActiveScene();

            // 기존 Stage 1/2 방·공유 현관문·원룸 전용 쓰레기 스웜 전부 깔끔히 비활성화 (공중 쓰레기/겹침 버그 완전 제거!)
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Stage1_OneRoom_Container" ||
                    go.name == "Stage2_Apartment_Container" ||
                    go.name == "Shared_EntranceDoor" ||
                    go.name == "Room01_KoreanStudio" ||
                    go.name == "Imported_Apartment_Scene01" ||
                    go.name == "TrashSwarm_Engine" ||
                    go.name == "Phase1_BigBreakablePiles_Stage1" ||
                    go.name == "Phase1_BigBreakablePiles_Stage2" ||
                    go.name == "Phase2_FloorStains_Stage1" ||
                    go.name == "Phase2_FloorStains_Stage2")
                {
                    go.SetActive(false);
                }
                else if (go.name.StartsWith("Stage0") || go.name == "Studio_Skeleton_Eunju")
                {
                    Object.DestroyImmediate(go);
                }
            }

            GameObject root = new GameObject(rootName);
            new GameObject($"Furniture_Stage0{stageNum}_Eunju").transform.SetParent(root.transform, false);
            return root.transform;
        }

        private static void BuildShell(Transform root, float w, float d, float h, float doorCenterX, Material wall, Material floor, Material ceil)
        {
            CreateBox("Floor_Main", new Vector3(0f, -0.20f, 0f), new Vector3(w + 0.8f, 0.4f, d + 0.8f), floor, root, true);
            CreateBox("Ceiling_Main", new Vector3(0f, h + 0.20f, 0f), new Vector3(w + 0.8f, 0.4f, d + 0.8f), ceil, root, true);
            CreateBox("Wall_South", new Vector3(0f, h / 2f, -d / 2f - 0.20f), new Vector3(w + 0.8f, h, 0.4f), wall, root, true);
            CreateBox("Wall_East", new Vector3(w / 2f + 0.20f, h / 2f, 0f), new Vector3(0.4f, h, d + 0.8f), wall, root, true);
            CreateBox("Wall_West", new Vector3(-w / 2f - 0.20f, h / 2f, 0f), new Vector3(0.4f, h, d + 0.8f), wall, root, true);

            float doorHalf = 0.54f;
            float leftStart = -w / 2f - 0.4f;
            float leftEnd = doorCenterX - doorHalf;
            float leftW = Mathf.Max(0.3f, leftEnd - leftStart);
            CreateBox("Wall_North_Left", new Vector3((leftStart + leftEnd) * 0.5f, h / 2f, d / 2f + 0.20f), new Vector3(leftW, h, 0.4f), wall, root, true);

            float rightStart = doorCenterX + doorHalf;
            float rightEnd = w / 2f + 0.4f;
            float rightW = Mathf.Max(0.3f, rightEnd - rightStart);
            CreateBox("Wall_North_Right", new Vector3((rightStart + rightEnd) * 0.5f, h / 2f, d / 2f + 0.20f), new Vector3(rightW, h, 0.4f), wall, root, true);

            CreateBox("Wall_North_Top", new Vector3(doorCenterX, (2.15f + h) * 0.5f, d / 2f + 0.20f), new Vector3(1.12f, h - 2.15f, 0.4f), wall, root, true);
            SetupClosedEntranceDoor(root, doorCenterX, d);
        }

        private static void SetupClosedEntranceDoor(Transform root, float doorCenterX, float d)
        {
            float doorZ = d / 2f + 0.20f;
            Material doorMat = LoadMat("Assets/_Project/Materials/Mat_Door_MustardYellow.mat", new Color(0.92f, 0.78f, 0.20f));
            Material handleMat = CreateColorMat("Mat_DoorHandle_Dark", new Color(0.20f, 0.20f, 0.22f), 0.7f);

            // Stage 1 원룸과 100% 동일한 힌지 구조: 오른쪽 경첩 + 방 안쪽(-Z)으로 열림! (플레이어를 절대 밀지 않음)
            GameObject hinge = new GameObject("Door_Entrance_Hinge");
            hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(doorCenterX + 0.54f, 0f, doorZ);

            GameObject doorLeaf = CreateBox("Door_Entrance", new Vector3(-0.54f, 1.075f, 0f), new Vector3(1.06f, 2.14f, 0.08f), doorMat, hinge.transform, true);
            CreateBox("Door_Handle_Out", new Vector3(-0.92f, 1.02f, 0.07f), new Vector3(0.14f, 0.04f, 0.07f), handleMat, hinge.transform, false);
            CreateBox("Door_Handle_In", new Vector3(-0.92f, 1.02f, -0.07f), new Vector3(0.14f, 0.04f, 0.07f), handleMat, hinge.transform, false);

            InteractiveDoor idoor = hinge.AddComponent<InteractiveDoor>();
            idoor.hingePivot = hinge.transform;
            idoor.doorCollider = doorLeaf.GetComponent<Collider>();
            idoor.closedAngleY = 0f;
            idoor.openAngleY = -98f;

            GamePrototypeManager mgr = Object.FindFirstObjectByType<GamePrototypeManager>();
            if (mgr != null)
            {
                idoor.manager = mgr;
                mgr.doorThresholdZ = doorZ;
                mgr.stage1Container = root.gameObject;
                mgr.stage2Container = null;
                mgr.trashSwarm = null; // 원룸 전용 쓰레기 스웜 연결 해제
            }

            // 문 앞 안전 바닥 발판: 실내 Floor_Main(y=0)과 절대 겹치지 않도록 문 바깥(z >= doorZ+0.25)에 배치 + 높이 -0.025m 분리!
            Material porchMat = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.55f, 0.55f, 0.55f));
            CreateBox("Entrance_PorchFloor", new Vector3(doorCenterX, -0.225f, doorZ + 3.55f), new Vector3(12.0f, 0.4f, 6.6f), porchMat, root, true);

            GameObject outside = GameObject.Find("Outside_Truck_Area");
            if (outside != null)
            {
                float dz = doorZ - 3.92f;
                float dx = doorCenterX - 2.10f;
                outside.transform.position = new Vector3(dx, 0f, dz);
                foreach (Transform child in outside.transform)
                {
                    if (child.name.StartsWith("Fence_Outside") || child.name == "Door_StepCurb")
                        child.gameObject.SetActive(false);
                    else if (child.name == "Parking_Floor")
                    {
                        // Parking_Floor 윗면을 -0.06m로 내려 실내 바닥·현관 발판과 Z-fighting 원천 차단!
                        Vector3 lp = child.localPosition;
                        child.localPosition = new Vector3(lp.x, -0.36f, lp.z);
                    }
                }
            }

            // 플레이어를 닫힌 노란 현관문 정면 1.25m 앞에 배치!
            Vector3 spawnPos = new Vector3(doorCenterX, 0.12f, doorZ + 1.25f);
            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);
            GameObject player = GameObject.Find("Player_FPS") ?? GameObject.Find("Player");
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(spawnPos, spawnRot);
                if (cc != null) cc.enabled = true;
            }
            if (mgr != null && mgr.playerSpawnPoint != null)
            {
                mgr.playerSpawnPoint.SetPositionAndRotation(spawnPos, spawnRot);
            }
        }

        private static void BuildStairs(Transform root, string name, Vector3 startPos, float height, float runLength, int steps, Material mat)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(root, false);
            float stepH = height / steps;
            float stepD = runLength / steps;
            for (int i = 0; i < steps; i++)
            {
                CreateBox(
                    $"Step_{i + 1:00}",
                    startPos + new Vector3(0f, stepH * (i + 0.5f), stepD * (i + 0.5f)),
                    new Vector3(1.7f, stepH, stepD + 0.05f),
                    mat,
                    group.transform,
                    true
                );
            }
        }

        private static void FinishScene(Scene scene, Transform root, string savePath, string label, bool showDialog)
        {
            EditorSceneManager.SaveScene(scene, savePath);
            Selection.activeGameObject = root.gameObject;
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    $"{label} 완성!",
                    $"{savePath} 에 방과 가구·현관문이 생성되었습니다.\nPlay(▶)로 닫힌 노란 현관문 앞에서 바로 시작해 보세요!",
                    "확인"
                );
            }
        }

        private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool withCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!withCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
            return go;
        }

        private static void AddLight(Transform parent, Vector3 pos, float intensity, float range)
        {
            GameObject go = new GameObject("Room_PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.94f, 0.84f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        private static Material LoadMat(string path, Color fallback)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            return m != null ? m : CreateColorMat("Fallback", fallback, 0.15f);
        }

        private static Material CreateColorMat(string name, Color col, float smoothness)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s) { name = name, color = col };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Material CreateGlassMat()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(0.75f, 0.88f, 0.96f, 0.40f);
            m.SetFloat("_Surface", 1f);
            m.renderQueue = 3000;
            return m;
        }

        private static void PlaceAsset(string folderPath, Vector3 worldPos, Vector3 eulerRot, float targetHeight, Transform parent)
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            if (guids == null || guids.Length == 0) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (prefab == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (inst == null) inst = Object.Instantiate(prefab, parent);
            inst.transform.localEulerAngles = eulerRot;

            Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.01f, b.size.y);
                inst.transform.localScale *= (targetHeight / h);

                rends = inst.GetComponentsInChildren<Renderer>();
                Bounds nb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
                inst.transform.position += worldPos - new Vector3(nb.center.x, nb.min.y, nb.center.z);
            }
            else
            {
                inst.transform.position = worldPos;
            }

            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>();
            }
        }

        private static void PlacePrefabByName(string prefabName, Vector3 worldPos, Vector3 eulerRot, Transform parent)
        {
            string[] guids = AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { "Assets/Brick Project Studio" });
            if (guids == null || guids.Length == 0) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (prefab == null) return;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (inst == null) inst = Object.Instantiate(prefab, parent);
            inst.transform.localEulerAngles = eulerRot;

            Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                inst.transform.position += worldPos - new Vector3(b.center.x, b.min.y, b.center.z);
            }
            else
            {
                inst.transform.position = worldPos;
            }

            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>();
            }
        }
    }
}
