using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// Stage 2~7의 각 컨셉과 방 사이즈에 맞는 빈 방 뼈대(벽·바닥·창문·파티션)를
    /// 기존 프로젝트 에셋만으로 생성해 주는 인테리어용 도우미입니다.
    /// </summary>
    public static class StageSkeletonBuilder
    {
        private const string BaseScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 2 - 택배 중독 오피스텔 (4.2x6.8m)")]
        public static void BuildStage2()
        {
            Transform root = PrepareScene(2, "Stage02_Officetel", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.95f, 0.94f, 0.90f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.78f, 0.70f, 0.56f));
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.75f, 0.75f, 0.75f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.92f, 0.92f, 0.90f));
            Material glass = CreateGlassMat();

            // 긴 복도형 오피스텔: 4.2m x 6.8m x 2.7m (북쪽 문 x: 0.6~1.6, z: +3.4)
            float w = 4.2f, d = 6.8f, h = 2.7f;
            BuildShell(root, w, d, h, 1.10f, wall, floor, ceil);
            // 긴 현관 복도 타일
            CreateBox("Floor_EntranceCorridor", new Vector3(1.10f, 0.005f, 2.25f), new Vector3(1.80f, 0.02f, 2.30f), tile, root);
            // 복도 가벽 (신발장/붙박이장 느낌)
            CreateBox("Corridor_PartitionWall", new Vector3(0.15f, h / 2f, 2.25f), new Vector3(0.12f, h, 2.30f), wall, root);
            // 남쪽 대형 통창 유리
            CreateBox("Large_WindowGlass", new Vector3(0f, 1.45f, -d / 2f + 0.02f), new Vector3(3.2f, 2.1f, 0.04f), glass, root);
            AddLight(root, new Vector3(0f, h - 0.25f, 0f), 12f, 12f);

            // 화장실 칸 (북서쪽: x -2.1 ~ -0.6, z 1.8 ~ 3.4)
            CreateBox("Floor_BathroomTile", new Vector3(-1.35f, 0.008f, 2.60f), new Vector3(1.50f, 0.02f, 1.60f), tile, root);
            CreateBox("BathWall_South", new Vector3(-1.35f, h / 2f, 1.80f), new Vector3(1.50f, h, 0.12f), wall, root);
            CreateBox("BathWall_East", new Vector3(-0.60f, h / 2f, 2.95f), new Vector3(0.12f, h, 0.90f), wall, root);

            // 컨셉 가구·소품 자동 배치 (화장실 + 침대 + 냉장고 + 싱크대 + 택배박스 + 펫캠 CCTV)
            Transform furn = root.Find("Furniture_Stage02_Eunju");
            PlacePrefabByName("RR_Toilet_01", new Vector3(-1.75f, 0f, 2.95f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(-1.75f, 0f, 2.20f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("RR_ToiletPaperDispenser", new Vector3(-1.75f, 0.7f, 2.65f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Bed_Apt_01_01", new Vector3(-1.35f, 0f, -2.10f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Fridge_01", new Vector3(-0.20f, 0f, 2.95f), new Vector3(0, 180, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-1.55f, 0f, 0.90f), new Vector3(0, 90, 0), furn);

            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.1f, 0f, 1.5f), new Vector3(0, 15, 0), 0.55f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.4f, 0f, 0.9f), new Vector3(0, -25, 0), 0.65f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(1.25f, 0.55f, 1.2f), new Vector3(0, 40, 0), 0.48f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(-0.2f, 0f, -0.5f), new Vector3(0, 10, 0), 0.70f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/pizza_box", new Vector3(0.3f, 0f, -1.5f), new Vector3(0, 30, 0), 0.12f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/classic_laptop", new Vector3(-1.5f, 0f, -0.4f), new Vector3(0, 20, 0), 0.28f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(1.85f, 2.35f, 3.1f), new Vector3(25, -145, 0), 0.32f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage02_Layout_Eunju.unity", "Stage 2 오피스텔 (4.2x6.8m)");
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 3 - 치킨집 주방 (5.5x5.0m)")]
        public static void BuildStage3()
        {
            Transform root = PrepareScene(3, "Stage03_ChickenKitchen", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.86f, 0.88f, 0.88f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.68f, 0.70f, 0.70f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.88f, 0.88f, 0.86f));
            Material counter = CreateColorMat("Mat_Kitchen_Steel", new Color(0.72f, 0.75f, 0.78f), 0.7f);

            // 치킨집 주방: 5.5m x 5.0m x 2.8m
            float w = 5.5f, d = 5.0f, h = 2.8f;
            BuildShell(root, w, d, h, 1.55f, wall, floor, ceil);
            // 기본 ㄷ자 조리대/카운터 뼈대 (중앙 통로는 넓게 비움!)
            CreateBox("Counter_West", new Vector3(-2.30f, 0.45f, -0.3f), new Vector3(0.80f, 0.90f, 3.8f), counter, root);
            CreateBox("Counter_South", new Vector3(-0.3f, 0.45f, -2.05f), new Vector3(3.6f, 0.90f, 0.80f), counter, root);
            CreateBox("PickupCounter_Front", new Vector3(-0.9f, 0.50f, 1.65f), new Vector3(2.4f, 1.0f, 0.65f), counter, root);
            AddLight(root, new Vector3(0f, h - 0.2f, 0f), 14f, 12f);

            // 컨셉 가구·소품 자동 배치 (조리대 위 프라이팬·냄비 + 업소용 냉장고 + 싱크대 + 식자재 상자 + CCTV)
            Transform furn = root.Find("Furniture_Stage03_Eunju");
            PlacePrefabByName("Fridge_01", new Vector3(2.15f, 0f, -1.95f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-2.30f, 0f, 1.15f), new Vector3(0, 90, 0), furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pan_01", new Vector3(-2.30f, 0.90f, -0.8f), new Vector3(0, 35, 0), 0.14f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pot_01", new Vector3(-2.30f, 0.90f, 0.2f), new Vector3(0, -20, 0), 0.26f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/brass_pot_01", new Vector3(-0.6f, 0.90f, -2.05f), new Vector3(0, 15, 0), 0.28f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.8f, 0f, -1.8f), new Vector3(0, -10, 0), 0.42f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/styrofoam_container", new Vector3(-0.9f, 1.0f, 1.65f), new Vector3(0, 25, 0), 0.15f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(2.4f, 2.45f, 2.1f), new Vector3(25, -135, 0), 0.32f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage03_Layout_Eunju.unity", "Stage 3 치킨집 주방 (5.5x5.0m)");
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 4 - 육아 전쟁 맞벌이 집 (7.2x5.8m)")]
        public static void BuildStage4()
        {
            Transform root = PrepareScene(4, "Stage04_FamilyHome", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.96f, 0.94f, 0.88f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.82f, 0.72f, 0.56f));
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.78f, 0.78f, 0.76f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.94f, 0.93f, 0.90f));
            Material mat = CreateColorMat("Mat_BabyPlayMat", new Color(0.86f, 0.82f, 0.68f), 0.1f);

            // 2룸 가정집: 7.2m x 5.8m x 2.7m
            float w = 7.2f, d = 5.8f, h = 2.7f;
            BuildShell(root, w, d, h, 2.20f, wall, floor, ceil);
            CreateBox("Floor_EntranceTile", new Vector3(2.20f, 0.005f, 2.30f), new Vector3(1.6f, 0.02f, 1.2f), tile, root);
            // 안방 파티션 (왼쪽 구역 x: -3.6 ~ -0.6, z: -2.9 ~ 0.6)
            CreateBox("Bedroom_Wall_North", new Vector3(-2.1f, h / 2f, 0.6f), new Vector3(3.0f, h, 0.14f), wall, root);
            CreateBox("Bedroom_Wall_East_Part", new Vector3(-0.6f, h / 2f, -1.6f), new Vector3(0.14f, h, 2.6f), wall, root);
            // 거실 유아 놀이매트
            CreateBox("LivingRoom_PlayMat", new Vector3(1.4f, 0.008f, -0.5f), new Vector3(2.6f, 0.015f, 2.2f), mat, root);
            AddLight(root, new Vector3(1.2f, h - 0.25f, 0f), 14f, 14f);
            AddLight(root, new Vector3(-2.1f, h - 0.25f, -1.0f), 10f, 10f);

            // 화장실 칸 (북서쪽: x -3.6 ~ -1.8, z 1.1 ~ 2.9)
            CreateBox("Floor_BathroomTile", new Vector3(-2.70f, 0.008f, 2.00f), new Vector3(1.80f, 0.02f, 1.80f), tile, root);
            CreateBox("BathWall_South", new Vector3(-2.70f, h / 2f, 1.10f), new Vector3(1.80f, h, 0.12f), wall, root);
            CreateBox("BathWall_East", new Vector3(-1.80f, h / 2f, 2.30f), new Vector3(0.12f, h, 1.20f), wall, root);

            // 컨셉 가구·소품 자동 배치 (화장실 + 부부 침대 + 주방 냉장고·싱크대 + 식탁 + 생활 소품)
            Transform furn = root.Find("Furniture_Stage04_Eunju");
            PlacePrefabByName("RR_Toilet_01", new Vector3(-3.20f, 0f, 2.45f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(-3.20f, 0f, 1.60f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("BathTub_Base_01", new Vector3(-2.25f, 0f, 2.35f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Bed_Apt_02_01", new Vector3(-2.25f, 0f, -1.75f), new Vector3(0, 90, 0), furn);
            PlacePrefabByName("Fridge_01", new Vector3(0.35f, 0f, 2.45f), new Vector3(0, 180, 0), furn);
            PlacePrefabByName("Cabinet_Base_Sink_01", new Vector3(-0.65f, 0f, 2.45f), new Vector3(0, 180, 0), furn);
            PlacePrefabByName("BPS_Table_01", new Vector3(2.40f, 0f, -2.00f), new Vector3(0, 0, 0), furn);

            PlaceAsset("Assets/_Project/Art/Models/Trash/cardboard_box_01", new Vector3(2.5f, 0f, 1.4f), new Vector3(0, 20, 0), 0.50f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Trash/water_bottles", new Vector3(0.8f, 0f, -0.4f), new Vector3(0, -15, 0), 0.35f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/all_purpose_cleaner", new Vector3(2.8f, 0f, -2.2f), new Vector3(0, 45, 0), 0.28f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/plastic_broom", new Vector3(-0.3f, 0f, 2.3f), new Vector3(10, 0, 8), 1.25f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(3.2f, 2.4f, -2.5f), new Vector3(20, -45, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage04_Layout_Eunju.unity", "Stage 4 육아 전쟁 맞벌이 집 (7.2x5.8m)");
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 5 - 스타트업 사무실 (8.5x7.0m)")]
        public static void BuildStage5()
        {
            Transform root = PrepareScene(5, "Stage05_StartupOffice", out Scene scene);
            Material wall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.92f, 0.93f, 0.95f));
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.45f, 0.48f, 0.52f));
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.92f, 0.92f, 0.94f));
            Material glass = CreateGlassMat();

            // 스타트업 사무실: 8.5m x 7.0m x 2.9m
            float w = 8.5f, d = 7.0f, h = 2.9f;
            BuildShell(root, w, d, h, 2.60f, wall, floor, ceil);
            // 대표실 유리 파티션 (남서쪽 구석: x -4.25 ~ -1.25, z -3.5 ~ -0.5)
            CreateBox("CEORoom_Glass_North", new Vector3(-2.75f, h / 2f, -0.5f), new Vector3(3.0f, h, 0.06f), glass, root);
            CreateBox("CEORoom_Glass_East", new Vector3(-1.25f, h / 2f, -2.5f), new Vector3(0.06f, h, 2.0f), glass, root);
            // 탕비실 파티션 (북서쪽)
            CreateBox("Pantry_Divider", new Vector3(-2.2f, h / 2f, 2.0f), new Vector3(0.14f, h, 2.8f), wall, root);
            AddLight(root, new Vector3(1.0f, h - 0.25f, 0f), 16f, 16f);
            AddLight(root, new Vector3(-2.5f, h - 0.25f, -1.5f), 12f, 12f);

            // 컨셉 가구·소품 자동 배치 (중앙 사무실 책상 6개 + 노트북 + 서류 바인더 + 대표실 책상 + CCTV 2대)
            Transform furn = root.Find("Furniture_Stage05_Eunju");
            float[] deskXs = new[] { 0.2f, 1.8f, 3.3f };
            foreach (float dx in deskXs)
            {
                PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(dx, 0f, 0.8f), new Vector3(0, 0, 0), 0.78f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Props/classic_laptop", new Vector3(dx, 0.78f, 0.8f), new Vector3(0, 180, 0), 0.28f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(dx, 0f, -0.5f), new Vector3(0, 180, 0), 0.78f, furn);
                PlaceAsset("Assets/_Project/Art/Models/Props/binder_notebook", new Vector3(dx + 0.25f, 0.78f, -0.5f), new Vector3(0, 25, 0), 0.12f, furn);
            }
            // 대표실 책상 + 노트북
            PlaceAsset("Assets/_Project/Art/Models/Furniture/metal_office_desk", new Vector3(-2.8f, 0f, -2.2f), new Vector3(0, 0, 0), 0.80f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/classic_laptop", new Vector3(-2.8f, 0.80f, -2.2f), new Vector3(0, 15, 0), 0.30f, furn);
            // CCTV 2대
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(3.9f, 2.55f, 3.1f), new Vector3(25, -135, 0), 0.32f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(-3.9f, 2.55f, -3.1f), new Vector3(25, 45, 0), 0.32f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage05_Layout_Eunju.unity", "Stage 5 스타트업 사무실 (8.5x7.0m)");
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 6 - 시골집 농막 (4.2x3.6m)")]
        public static void BuildStage6()
        {
            Transform root = PrepareScene(6, "Stage06_RuralCabin", out Scene scene);
            Material wood = CreateColorMat("Mat_Cabin_OldWood", new Color(0.52f, 0.40f, 0.28f), 0.12f);
            Material floor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.58f, 0.48f, 0.36f));
            Material ceil = CreateColorMat("Mat_Cabin_Roof", new Color(0.45f, 0.36f, 0.26f), 0.1f);

            // 아담한 시골 농막: 4.2m x 3.6m x 2.5m
            float w = 4.2f, d = 3.6f, h = 2.5f;
            BuildShell(root, w, d, h, 1.10f, wood, floor, ceil);
            // 처마 앞 야외 작업 발판
            CreateBox("Outdoor_WoodenDeck", new Vector3(0.8f, 0.02f, 2.4f), new Vector3(3.2f, 0.06f, 1.2f), floor, root);
            AddLight(root, new Vector3(0f, h - 0.25f, 0f), 9f, 9f);

            // 컨셉 가구·소품 자동 배치 (드럼통 + 농막 상자 + 빗자루 + 쓰레받기 + 방범 CCTV)
            Transform furn = root.Find("Furniture_Stage06_Eunju");
            PlaceAsset("Assets/_Project/Art/Models/Props/Barrel_01", new Vector3(-1.4f, 0f, -1.1f), new Vector3(0, 20, 0), 0.92f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Barrel_01", new Vector3(-1.3f, 0f, -0.1f), new Vector3(0, -40, 0), 0.92f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.4f, 0f, -1.2f), new Vector3(0, 15, 0), 0.45f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/plastic_crate_01", new Vector3(1.4f, 0.45f, -1.2f), new Vector3(0, -10, 0), 0.45f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/dustpan", new Vector3(0.4f, 0f, -1.2f), new Vector3(0, 30, 0), 0.22f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Tools/plastic_broom", new Vector3(1.7f, 0f, 0.8f), new Vector3(8, 0, -8), 1.25f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_01", new Vector3(1.7f, 2.2f, 1.5f), new Vector3(20, -135, 0), 0.30f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage06_Layout_Eunju.unity", "Stage 6 시골집 농막 (4.2x3.6m)");
        }

        [MenuItem("TrashCleaner/Build Stage Skeleton/Stage 7 - 졸부 3층 복층 펜트하우스 (10x8m, 3층)")]
        public static void BuildStage7()
        {
            Transform root = PrepareScene(7, "Stage07_Penthouse_3F", out Scene scene);
            Material wall = CreateColorMat("Mat_Penthouse_CreamMarble", new Color(0.94f, 0.91f, 0.86f), 0.35f);
            Material floor = CreateColorMat("Mat_Penthouse_MarbleFloor", new Color(0.86f, 0.84f, 0.80f), 0.65f);
            Material ceil = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.95f, 0.94f, 0.92f));
            Material glass = CreateGlassMat();
            Material goldTrim = CreateColorMat("Mat_Penthouse_Gold", new Color(0.88f, 0.70f, 0.22f), 0.8f);
            Material stairMat = CreateColorMat("Mat_Penthouse_Stairs", new Color(0.32f, 0.25f, 0.20f), 0.4f);

            // 3층 복층 럭셔리 펜트하우스: 10.0m x 8.0m, 층고 3.0m x 3층 = 총 9.0m
            float w = 10.0f, d = 8.0f, fh = 3.0f, totalH = fh * 3f;

            // 외벽 4면 (3층 전체 높이, 1층 북쪽 현관문 구멍만 뚫림)
            CreateBox("Floor_1F", new Vector3(0f, -0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), floor, root);
            CreateBox("Ceiling_3F", new Vector3(0f, totalH + 0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), ceil, root);
            CreateBox("Wall_South_Full", new Vector3(0f, totalH / 2f, -d / 2f - 0.15f), new Vector3(w + 0.4f, totalH, 0.3f), wall, root);
            CreateBox("Wall_East_Full", new Vector3(w / 2f + 0.15f, totalH / 2f, 0f), new Vector3(0.3f, totalH, d + 0.4f), wall, root);
            CreateBox("Wall_West_Full", new Vector3(-w / 2f - 0.15f, totalH / 2f, 0f), new Vector3(0.3f, totalH, d + 0.4f), wall, root);

            // 북쪽 벽 (1층 현관문 x: 2.7~3.7, y: 0~2.15 제외하고 3층까지)
            CreateBox("Wall_North_Left", new Vector3(-1.25f, totalH / 2f, d / 2f + 0.15f), new Vector3(7.9f, totalH, 0.3f), wall, root);
            CreateBox("Wall_North_Right", new Vector3(4.45f, totalH / 2f, d / 2f + 0.15f), new Vector3(1.5f, totalH, 0.3f), wall, root);
            CreateBox("Wall_North_AboveDoor", new Vector3(3.20f, (2.15f + totalH) * 0.5f, d / 2f + 0.15f), new Vector3(1.0f, totalH - 2.15f, 0.3f), wall, root);

            // 1층: 바(Bar) 카운터 & 파노라마 창
            CreateBox("1F_PartyBar_Counter", new Vector3(3.4f, 0.55f, -1.2f), new Vector3(0.8f, 1.1f, 3.4f), goldTrim, root);
            CreateBox("1F_Panorama_Glass", new Vector3(0f, 1.55f, -d / 2f + 0.02f), new Vector3(6.8f, 2.4f, 0.04f), glass, root);

            // 2층 바닥 (계단 구멍 x: -4.6 ~ -2.8, z: -3.6 ~ 0.6 비워둠) + 유리 난간
            CreateBox("Floor_2F_Main", new Vector3(1.1f, fh, 0f), new Vector3(7.8f, 0.22f, d), floor, root);
            CreateBox("Floor_2F_WestLanding", new Vector3(-3.9f, fh, 2.3f), new Vector3(2.2f, 0.22f, 3.4f), floor, root);
            CreateBox("2F_Stair_Railing", new Vector3(-2.75f, fh + 0.55f, -1.5f), new Vector3(0.06f, 0.9f, 4.2f), glass, root);
            BuildStairs(root, "Stairs_1F_to_2F", new Vector3(-3.9f, 0f, -3.2f), fh, 3.6f, 12, stairMat);

            // 3층 바닥 (계단 구멍 x: 2.8 ~ 4.6, z: -3.6 ~ 0.6 비워둠) + VIP 금고방
            CreateBox("Floor_3F_Main", new Vector3(-1.1f, fh * 2f, 0f), new Vector3(7.8f, 0.22f, d), floor, root);
            CreateBox("Floor_3F_EastLanding", new Vector3(3.9f, fh * 2f, 2.3f), new Vector3(2.2f, 0.22f, 3.4f), floor, root);
            CreateBox("3F_Stair_Railing", new Vector3(2.75f, fh * 2f + 0.55f, -1.5f), new Vector3(0.06f, 0.9f, 4.2f), glass, root);
            BuildStairs(root, "Stairs_2F_to_3F", new Vector3(3.9f, fh, -3.2f), fh, 3.6f, 12, stairMat);

            // 3층 안쪽 VIP 금고방 파티션 (남서쪽)
            CreateBox("3F_VaultRoom_North", new Vector3(-3.4f, fh * 2f + fh / 2f, -1.0f), new Vector3(3.2f, fh, 0.18f), wall, root);
            CreateBox("3F_VaultRoom_East", new Vector3(-1.8f, fh * 2f + fh / 2f, -2.9f), new Vector3(0.18f, fh, 2.2f), wall, root);

            // 2층 고급 화장실 칸 (북동쪽: x 2.5 ~ 4.8, z 1.6 ~ 3.8)
            Material tile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.84f, 0.84f, 0.82f));
            CreateBox("2F_BathroomTile", new Vector3(3.65f, fh + 0.12f, 2.70f), new Vector3(2.30f, 0.02f, 2.20f), tile, root);
            CreateBox("2F_BathWall_South", new Vector3(3.65f, fh + fh / 2f, 1.60f), new Vector3(2.30f, fh, 0.14f), wall, root);
            CreateBox("2F_BathWall_West", new Vector3(2.50f, fh + fh / 2f, 3.05f), new Vector3(0.14f, fh, 1.50f), wall, root);

            // 1·2·3층 조명
            AddLight(root, new Vector3(0.5f, fh - 0.3f, 0f), 18f, 16f);
            AddLight(root, new Vector3(0.5f, fh * 2f - 0.3f, 0f), 18f, 16f);
            AddLight(root, new Vector3(-1.5f, fh * 3f - 0.3f, -1.0f), 16f, 16f);

            // 컨셉 가구·소품 자동 배치 (1층 바+의자+테이블 / 2층 화장실+침실 / 3층 VIP 보물+샹들리에)
            Transform furn = root.Find("Furniture_Stage07_Eunju");
            PlacePrefabByName("BarCabinet_Apt_01_Filled Variant", new Vector3(4.25f, 0f, -1.2f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("BPS_Table_01", new Vector3(0f, 0f, -1.2f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Lamp_Table_Apt_01", new Vector3(0.3f, 0.75f, -1.2f), new Vector3(0, 0, 0), furn);
            // 2층 화장실 & 마스터 침실
            float y2f = fh + 0.11f;
            PlacePrefabByName("RR_Toilet_01", new Vector3(4.35f, y2f, 3.30f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("RR_Sink_01", new Vector3(4.35f, y2f, 2.35f), new Vector3(0, -90, 0), furn);
            PlacePrefabByName("BathTub_Base_01", new Vector3(3.20f, y2f, 3.20f), new Vector3(0, 0, 0), furn);
            PlacePrefabByName("Bed_Apt_02_01", new Vector3(0.2f, y2f, -2.4f), new Vector3(0, 0, 0), furn);
            for (int i = 0; i < 4; i++)
            {
                PlaceAsset("Assets/_Project/Art/Models/Furniture/bar_chair_round_01", new Vector3(2.4f, 0f, -2.3f + i * 0.75f), new Vector3(0, 90, 0), 0.78f, furn);
            }
            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(0.5f, fh - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(0.5f, fh * 2f - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/Chandelier_01", new Vector3(-0.5f, fh * 3f - 0.85f, 0f), Vector3.zero, 0.75f, furn);
            // 3층 VIP 금고방 보물 (지폐 + 회중시계)
            PlaceAsset("Assets/_Project/Art/Models/Valuables/usd_money", new Vector3(-3.6f, fh * 2f + 0.11f, -2.6f), new Vector3(0, 25, 0), 0.16f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Valuables/pocket_watch", new Vector3(-3.2f, fh * 2f + 0.11f, -2.4f), new Vector3(0, -15, 0), 0.14f, furn);
            // 1층 & 3층 CCTV
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(4.5f, 2.65f, 3.6f), new Vector3(25, -135, 0), 0.34f, furn);
            PlaceAsset("Assets/_Project/Art/Models/Props/security_camera_02", new Vector3(-4.5f, fh * 2f + 2.65f, -0.7f), new Vector3(25, 45, 0), 0.34f, furn);

            FinishScene(scene, root, "Assets/_Project/Scenes/Stage07_Layout_Eunju.unity", "Stage 7 졸부 3층 복층 펜트하우스 (10x8m, 3층)");
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
                    new Vector3(1.7f, stepH, stepD + 0.04f),
                    mat,
                    group.transform
                );
            }
        }

        // --- Helpers ---

        private static Transform PrepareScene(int stageNum, string rootName, out Scene scene)
        {
            if (File.Exists(Path.GetFullPath(BaseScenePath)))
            {
                EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
            }
            scene = SceneManager.GetActiveScene();

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Room01_KoreanStudio" || go.name == "Imported_Apartment_Scene01")
                    go.SetActive(false);
                else if (go.name.StartsWith("Stage0") || go.name == "Studio_Skeleton_Eunju")
                    Object.DestroyImmediate(go);
            }

            GameObject root = new GameObject(rootName);
            new GameObject($"Furniture_Stage0{stageNum}_Eunju").transform.SetParent(root.transform, false);
            return root.transform;
        }

        private static void BuildShell(Transform root, float w, float d, float h, float doorCenterX, Material wall, Material floor, Material ceil)
        {
            CreateBox("Floor_Main", new Vector3(0f, -0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), floor, root);
            CreateBox("Ceiling_Main", new Vector3(0f, h + 0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), ceil, root);

            CreateBox("Wall_South", new Vector3(0f, h / 2f, -d / 2f - 0.15f), new Vector3(w + 0.4f, h, 0.3f), wall, root);
            CreateBox("Wall_East", new Vector3(w / 2f + 0.15f, h / 2f, 0f), new Vector3(0.3f, h, d + 0.4f), wall, root);
            CreateBox("Wall_West", new Vector3(-w / 2f - 0.15f, h / 2f, 0f), new Vector3(0.3f, h, d + 0.4f), wall, root);

            // 북쪽 벽 (문 구멍 1.0m 너비, 높이 2.15m 비워둠)
            float doorHalf = 0.5f;
            float leftStart = -w / 2f - 0.2f;
            float leftEnd = doorCenterX - doorHalf;
            float leftW = Mathf.Max(0.2f, leftEnd - leftStart);
            CreateBox("Wall_North_Left", new Vector3((leftStart + leftEnd) * 0.5f, h / 2f, d / 2f + 0.15f), new Vector3(leftW, h, 0.3f), wall, root);

            float rightStart = doorCenterX + doorHalf;
            float rightEnd = w / 2f + 0.2f;
            float rightW = Mathf.Max(0.2f, rightEnd - rightStart);
            CreateBox("Wall_North_Right", new Vector3((rightStart + rightEnd) * 0.5f, h / 2f, d / 2f + 0.15f), new Vector3(rightW, h, 0.3f), wall, root);

            CreateBox("Wall_North_Top", new Vector3(doorCenterX, (2.15f + h) * 0.5f, d / 2f + 0.15f), new Vector3(1.0f, h - 2.15f, 0.3f), wall, root);
        }

        private static void FinishScene(Scene scene, Transform root, string savePath, string label)
        {
            EditorSceneManager.SaveScene(scene, savePath);
            Selection.activeGameObject = root.gameObject;
            EditorUtility.DisplayDialog(
                $"{label} 뼈대 완성!",
                $"{savePath} 에 방 뼈대가 생성되었습니다.\n이제 [Brick Project Studio > Apartment Kit > Prefabs]에서 가구를 끌어다 배치하고 Ctrl+S로 저장해 보세요!",
                "확인"
            );
        }

        private static GameObject CreateBox(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
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
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
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
                float scale = targetHeight / h;
                inst.transform.localScale *= scale;

                // Recompute bounds after scale so bottom sits cleanly at worldPos.y
                rends = inst.GetComponentsInChildren<Renderer>();
                Bounds nb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
                Vector3 delta = worldPos - new Vector3(nb.center.x, nb.min.y, nb.center.z);
                inst.transform.position += delta;
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
                Vector3 delta = worldPos - new Vector3(b.center.x, b.min.y, b.center.z);
                inst.transform.position += delta;
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
