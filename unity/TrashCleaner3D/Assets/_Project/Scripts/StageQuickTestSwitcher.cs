using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 테스트 기간 동안 Play 모드에서 [F1]~[F7] 키로 Stage 1~7 방을 즉시 이동해 볼 수 있는 테스트 유틸리티입니다.
    /// (EditorBuildSettings에 등록된 Stage 씬으로 전환)
    /// </summary>
    public class StageQuickTestSwitcher : MonoBehaviour
    {
        private static readonly string[] StageSceneNames =
        {
            "Room01_PlayablePrototype",
            "Stage02_Layout_Eunju",
            "Stage03_Layout_Eunju",
            "Stage04_Layout_Eunju",
            "Stage05_Layout_Eunju",
            "Stage06_Layout_Eunju",
            "Stage07_Layout_Eunju"
        };

        private static readonly string[] StageLabels =
        {
            "F1: 원룸",
            "F2: 오피스텔",
            "F3: 치킨집",
            "F4: 육아집",
            "F5: 사무실",
            "F6: 농막",
            "F7: 3층펜트"
        };

        private static StageQuickTestSwitcher _instance;
        private string _notice = "";
        private float _noticeTimer;
        private GUIStyle _barStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("StageQuickTestSwitcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StageQuickTestSwitcher>();
        }

        private void Update()
        {
            if (_noticeTimer > 0f) _noticeTimer -= Time.deltaTime;

            int targetIdx = -1;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) targetIdx = 0;
                else if (kb.f2Key.wasPressedThisFrame) targetIdx = 1;
                else if (kb.f3Key.wasPressedThisFrame) targetIdx = 2;
                else if (kb.f4Key.wasPressedThisFrame) targetIdx = 3;
                else if (kb.f5Key.wasPressedThisFrame) targetIdx = 4;
                else if (kb.f6Key.wasPressedThisFrame) targetIdx = 5;
                else if (kb.f7Key.wasPressedThisFrame) targetIdx = 6;
            }
#else
            if (Input.GetKeyDown(KeyCode.F1)) targetIdx = 0;
            else if (Input.GetKeyDown(KeyCode.F2)) targetIdx = 1;
            else if (Input.GetKeyDown(KeyCode.F3)) targetIdx = 2;
            else if (Input.GetKeyDown(KeyCode.F4)) targetIdx = 3;
            else if (Input.GetKeyDown(KeyCode.F5)) targetIdx = 4;
            else if (Input.GetKeyDown(KeyCode.F6)) targetIdx = 5;
            else if (Input.GetKeyDown(KeyCode.F7)) targetIdx = 6;
#endif
            if (targetIdx >= 0) LoadStage(targetIdx);
        }

        private void LoadStage(int index)
        {
            string sceneName = StageSceneNames[index];
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                _notice = $"Stage {index + 1} 미등록! 메뉴 [TrashCleaner > Build Stage Skeleton > ★ Build ALL Stage 2-7] 1회 클릭 필요";
                _noticeTimer = 4.5f;
                return;
            }
            SceneManager.LoadScene(sceneName);
        }

        private void OnGUI()
        {
            if (_barStyle == null)
            {
                _barStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };
                _barStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);
            }

            string current = SceneManager.GetActiveScene().name;
            string barText = "[테스트 이동] " + string.Join(" | ", StageLabels);
            GUI.Box(new Rect(12f, Screen.height - 34f, 620f, 24f), barText, _barStyle);

            if (_noticeTimer > 0f && !string.IsNullOrEmpty(_notice))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 320f, 70f, 640f, 30f), _notice, _barStyle);
            }
        }
    }
}
