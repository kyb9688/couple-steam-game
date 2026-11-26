using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// Stage 1 '직장인 반지하 원룸' 빈 뼈대(현관 타일, 화장실 칸, 반지하 높은 창문, 메인 바닥/벽)를
    /// Room01_Layout_Eunju 씬에 생성해 주는 인테리어용 도우미입니다.
    /// 기존 플레이어/트럭/청소 시스템은 그대로 유지되어 가구 배치 후 바로 Play 테스트가 가능합니다.
    /// </summary>
    public static class Room01LayoutSkeletonBuilder
    {
        private const string BaseScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";
        private const string LayoutScenePath = "Assets/_Project/Scenes/Room01_Layout_Eunju.unity";

        [MenuItem("TrashCleaner/Build Stage 1 Empty Studio Skeleton (For Interior)")]
        public static void BuildSkeleton()
        {
            if (File.Exists(Path.GetFullPath(BaseScenePath)))
            {
                EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
            }

            Scene scene = SceneManager.GetActiveScene();

            // 기존 기본 네모 방/이전 뼈대/임포트 아파트 정리
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Room01_KoreanStudio" ||
                    root.name == "Imported_Apartment_Scene01")
                {
                    root.SetActive(false);
                }
                else if (root.name == "Studio_Skeleton_Eunju")
                {
                    Object.DestroyImmediate(root);
                }
            }

            GameObject skel = new GameObject("Studio_Skeleton_Eunju");

            // 재질 로드 (기존 프로젝트 재질 재사용, 없으면 기본)
            Material matWall = LoadMat("Assets/_Project/Materials/Mat_Wall_KoreanIvory.mat", new Color(0.95f, 0.91f, 0.80f));
            Material matFloor = LoadMat("Assets/_Project/Materials/Mat_Floor_PlankFlooring.mat", new Color(0.75f, 0.65f, 0.50f));
            Material matCeiling = LoadMat("Assets/_Project/Materials/Mat_Ceiling_Ivory.mat", new Color(0.88f, 0.86f, 0.80f));
            Material matTile = LoadMat("Assets/_Project/Materials/Mat_Floor_DirtyTiles.mat", new Color(0.70f, 0.72f, 0.70f));
            Material matGlass = CreateGlassMat();

            // 방 규격: 5.2m (X: -2.6 ~ +2.6) x 4.2m (Z: -2.1 ~ +2.1) x 2.6m (Y: 0 ~ 2.6)
            float w = 5.2f;
            float d = 4.2f;
            float h = 2.6f;

            // 1. 메인 원룸 바닥 & 천장
            CreateBox("Floor_Main", new Vector3(0f, -0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), matFloor, skel.transform);
            CreateBox("Ceiling_Main", new Vector3(0f, h + 0.15f, 0f), new Vector3(w + 0.4f, 0.3f, d + 0.4f), matCeiling, skel.transform);

            // 2. 현관 타일 바닥 (북동쪽: x 0.9 ~ 2.5, z 0.9 ~ 2.1)
            CreateBox("Floor_EntranceTile", new Vector3(1.70f, 0.005f, 1.50f), new Vector3(1.60f, 0.02f, 1.20f), matTile, skel.transform);

            // 3. 화장실 칸 (북서쪽: x -2.6 ~ -1.0, z 0.5 ~ 2.1)
            CreateBox("Floor_BathroomTile", new Vector3(-1.80f, 0.005f, 1.30f), new Vector3(1.60f, 0.02f, 1.60f), matTile, skel.transform);
            CreateBox("BathWall_South", new Vector3(-1.80f, h / 2f, 0.50f), new Vector3(1.60f, h, 0.12f), matWall, skel.transform);
            // 화장실 동쪽 벽 (문 구멍 z 0.65 ~ 1.45 제외하고 위/양옆)
            CreateBox("BathWall_East_Post", new Vector3(-1.00f, h / 2f, 1.78f), new Vector3(0.12f, h, 0.64f), matWall, skel.transform);
            CreateBox("BathWall_East_Lintel", new Vector3(-1.00f, 2.35f, 0.98f), new Vector3(0.12f, 0.50f, 0.96f), matWall, skel.transform);

            // 4. 동쪽 / 서쪽 외벽
            CreateBox("Wall_East", new Vector3(w / 2f + 0.15f, h / 2f, 0f), new Vector3(0.3f, h, d + 0.4f), matWall, skel.transform);
            CreateBox("Wall_West", new Vector3(-w / 2f - 0.15f, h / 2f, 0f), new Vector3(0.3f, h, d + 0.4f), matWall, skel.transform);

            // 5. 북쪽 벽 (현관문 구멍 x: 1.05 ~ 2.05, y: 0 ~ 2.15 비워둠)
            CreateBox("Wall_North_Left", new Vector3(-0.775f, h / 2f, d / 2f + 0.15f), new Vector3(3.65f, h, 0.3f), matWall, skel.transform);
            CreateBox("Wall_North_Right", new Vector3(2.325f, h / 2f, d / 2f + 0.15f), new Vector3(0.55f, h, 0.3f), matWall, skel.transform);
            CreateBox("Wall_North_Top", new Vector3(1.55f, 2.375f, d / 2f + 0.15f), new Vector3(1.00f, 0.45f, 0.3f), matWall, skel.transform);

            // 6. 남쪽 벽 (반지하 높은 가로 창문 x: -1.5 ~ 0.7, y: 1.55 ~ 2.25)
            CreateBox("Wall_South_Bottom", new Vector3(0f, 0.775f, -d / 2f - 0.15f), new Vector3(w + 0.4f, 1.55f, 0.3f), matWall, skel.transform);
            CreateBox("Wall_South_Top", new Vector3(0f, 2.425f, -d / 2f - 0.15f), new Vector3(w + 0.4f, 0.35f, 0.3f), matWall, skel.transform);
            CreateBox("Wall_South_Left", new Vector3(-2.05f, 1.90f, -d / 2f - 0.15f), new Vector3(1.10f, 0.70f, 0.3f), matWall, skel.transform);
            CreateBox("Wall_South_Right", new Vector3(1.65f, 1.90f, -d / 2f - 0.15f), new Vector3(1.90f, 0.70f, 0.3f), matWall, skel.transform);
            CreateBox("SemiBasement_WindowGlass", new Vector3(-0.40f, 1.90f, -d / 2f - 0.15f), new Vector3(2.20f, 0.70f, 0.04f), matGlass, skel.transform);

            // 7. 가구 배치용 빈 그룹 폴더 (여기에 Apartment Kit 가구를 끌어다 놓으면 깔끔!)
            new GameObject("Furniture_Eunju").transform.SetParent(skel.transform, false);

            // 8. 원룸 천장 조명 (부드러운 형광등)
            GameObject lightObj = new GameObject("Studio_CeilingLight");
            lightObj.transform.SetParent(skel.transform, false);
            lightObj.transform.position = new Vector3(0.2f, h - 0.25f, -0.2f);
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.93f, 0.80f);
            l.intensity = 10f;
            l.range = 10f;
            l.shadows = LightShadows.None;

            // Room01_Layout_Eunju 씬으로 안전하게 저장
            EditorSceneManager.SaveScene(scene, LayoutScenePath);
            Selection.activeGameObject = skel;

            EditorUtility.DisplayDialog(
                "원룸 빈 뼈대 완성!",
                "반지하 원룸 구조(현관·화장실 칸·반지하 창문)가\nRoom01_Layout_Eunju 씬에 생성되었습니다!\n\n이제 [Project > Assets > Brick Project Studio > Apartment Kit > Prefabs]에서\n침대·책상·의자·냉장고·변기 등을 마우스로 끌어다 배치해 보세요!\n(W: 이동 / E: 회전 / R: 크기 / Ctrl+S: 저장)",
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

        private static Material LoadMat(string path, Color fallbackColor)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material fb = new Material(s);
            fb.color = fallbackColor;
            return fb;
        }

        private static Material CreateGlassMat()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(0.75f, 0.88f, 0.96f, 0.45f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.renderQueue = 3000;
            return m;
        }
    }
}
