using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// Apartment Kit의 Scene_01 환경을 현재 열린 플레이어블 씬(예: Room01_Interior_Eunju)으로 가져와
    /// 1인칭 캐릭터로 직접 걸어 다니며 테스트할 수 있게 해주는 도우미 메뉴입니다.
    /// </summary>
    public static class ApartmentImportHelper
    {
        private const string Scene01Path = "Assets/Brick Project Studio/Apartment Kit/Scenes/Scene_01.unity";

        [MenuItem("TrashCleaner/Import Scene_01 Into Current Room")]
        public static void ImportScene01IntoCurrent()
        {
            if (!File.Exists(Path.GetFullPath(Scene01Path)))
            {
                EditorUtility.DisplayDialog(
                    "Apartment Kit 없음",
                    $"먼저 Asset Store에서 Apartment Kit을 Import해 주세요.\n경로: {Scene01Path}",
                    "확인"
                );
                return;
            }

            Scene targetScene = SceneManager.GetActiveScene();

            // 기존 네모 박스 방이 있다면 숨김 (바닥/벽 겹침 방지)
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                if (root.name == "Room01_KoreanStudio")
                {
                    root.SetActive(false);
                }
                else if (root.name == "Imported_Apartment_Scene01")
                {
                    Object.DestroyImmediate(root);
                }
            }

            Scene srcScene = EditorSceneManager.OpenScene(Scene01Path, OpenSceneMode.Additive);
            GameObject container = new GameObject("Imported_Apartment_Scene01");
            SceneManager.MoveGameObjectToScene(container, targetScene);

            int movedCount = 0;
            int colliderAdded = 0;

            foreach (GameObject srcRoot in srcScene.GetRootGameObjects())
            {
                // 카메라/리스너/이벤트시스템은 플레이어와 충돌하므로 제외
                if (srcRoot.GetComponentInChildren<Camera>(true) != null ||
                    srcRoot.GetComponentInChildren<AudioListener>(true) != null)
                {
                    continue;
                }

                SceneManager.MoveGameObjectToScene(srcRoot, targetScene);
                srcRoot.transform.SetParent(container.transform, true);
                movedCount++;

                // 걸어 다닐 수 있도록 Collider가 없는 메쉬에 MeshCollider 자동 추가
                foreach (MeshFilter mf in srcRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (mf.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<MeshCollider>();
                        colliderAdded++;
                    }
                }
            }

            EditorSceneManager.CloseScene(srcScene, true);

            // Apartment_01 위치를 찾아 플레이어를 방 안쪽으로 안내 이동 (있는 경우)
            Transform apt01 = container.transform.Find("Apartment_01");
            if (apt01 == null)
            {
                foreach (Transform t in container.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Apartment_01") { apt01 = t; break; }
                }
            }

            GameObject player = GameObject.Find("Player_FPS") ?? GameObject.Find("Player") ?? GameObject.Find("FirstPersonCleaner");
            if (player != null && apt01 != null)
            {
                Renderer[] rends = apt01.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                    CharacterController cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = new Vector3(b.center.x, b.min.y + 0.35f, b.center.z);
                    if (cc != null) cc.enabled = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(targetScene);
            Selection.activeGameObject = container;

            EditorUtility.DisplayDialog(
                "Scene_01 가져오기 완료!",
                $"Apartment Kit의 Scene_01을 현재 방으로 옮겼습니다.\n- 가져온 그룹: {movedCount}개\n- 자동 충돌(Collider) 추가: {colliderAdded}개\n\n이제 Play(▶)를 눌러 직접 걸어 다녀 보세요!\n(Ctrl + S로 저장하는 것도 잊지 마세요!)",
                "확인"
            );
        }
    }
}
