using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 테스트 기간 동안 Play 모드에서 [F1]~[F7] 키(또는 ESC로 커서 해제 시 버튼 클릭)로
    /// Stage 1~7 방을 즉시 이동해 볼 수 있는 테스트 스위처입니다.
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
            "F1 원룸",
            "F2 오피스텔",
            "F3 치킨집",
            "F4 육아집",
            "F5 사무실",
            "F6 농막",
            "F7 3층펜트"
        };

        private static StageQuickTestSwitcher _instance;
        private string _notice = "";
        private float _noticeTimer;
        private Font _krFont;
        private GUIStyle _btnStyle;
        private GUIStyle _activeBtnStyle;
        private GUIStyle _noticeStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("StageQuickTestSwitcher");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StageQuickTestSwitcher>();
        }

        private void Awake()
        {
            _krFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "NanumGothic", "Segoe UI" }, 13);
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
                _notice = $"Stage {index + 1} 미등록! 상단 [TrashCleaner > Build Stage Skeleton > ★ Build ALL Stage 2-7] 1회 클릭 필요";
                _noticeTimer = 4.5f;
                return;
            }
            SceneManager.LoadScene(sceneName);
        }

        private void EnsureStyles()
        {
            if (_btnStyle != null) return;
            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                font = _krFont,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            _btnStyle.normal.textColor = new Color(0.92f, 0.94f, 0.97f);

            _activeBtnStyle = new GUIStyle(_btnStyle)
            {
                fontStyle = FontStyle.Bold
            };
            _activeBtnStyle.normal.textColor = new Color(1.0f, 0.85f, 0.25f);

            _noticeStyle = new GUIStyle(GUI.skin.box)
            {
                font = _krFont,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            _noticeStyle.normal.textColor = new Color(1.0f, 0.90f, 0.45f);
        }

        private void OnGUI()
        {
            EnsureStyles();
            string current = SceneManager.GetActiveScene().name;

            float x = 12f;
            float y = Screen.height - 34f;
            float btnW = 86f;
            float gap = 4f;

            for (int i = 0; i < StageLabels.Length; i++)
            {
                bool isCur = current == StageSceneNames[i];
                if (GUI.Button(new Rect(x + i * (btnW + gap), y, btnW, 24f), StageLabels[i], isCur ? _activeBtnStyle : _btnStyle))
                {
                    LoadStage(i);
                }
            }

            if (_noticeTimer > 0f && !string.IsNullOrEmpty(_notice))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 340f, 70f, 680f, 32f), _notice, _noticeStyle);
            }
        }
    }
}
