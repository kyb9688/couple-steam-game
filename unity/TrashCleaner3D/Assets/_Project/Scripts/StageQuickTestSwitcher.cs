using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TrashCleaner3D
{
    /// <summary>
    /// 테스트 기간 스테이지 즉시 이동 도우미 (v1.5.1 개편):
    /// - 1인칭 포인터 락 중에는 하단에 '눌리지 않는 버튼'을 그리지 않고 안내 문구만 띄운다.
    ///   (포인터 락 상태에서는 마우스가 화면 중앙에 고정되어 하단 버튼이 클릭으로 먹히지 않던 문제)
    /// - [Tab] 또는 [Alt] = 커서 모드 토글 → 커서가 풀리면 하단 스테이지 버튼을 실제로 클릭할 수 있다.
    /// - [F1]~[F7] = Stage 1~7 즉시 이동 (데스크톱/에디터. 브라우저는 F5 새로고침 등과 겹칠 수 있음)
    /// - 커서 모드에서는 숫자 [1]~[7]도 Stage 1~7 즉시 이동으로 동작한다 (브라우저에서 F키가 막혀도 OK).
    /// - [M] = 7종 스테이지 마인드맵 (1인칭 컨트롤러가 처리)
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
            "1 원룸",
            "2 오피스텔",
            "3 치킨집",
            "4 육아집",
            "5 사무실",
            "6 농막",
            "7 3층펜트"
        };

        private static StageQuickTestSwitcher _instance;
        private string _notice = "";
        private float _noticeTimer;
        private Font _krFont;
        private GUIStyle _btnStyle;
        private GUIStyle _activeBtnStyle;
        private GUIStyle _noticeStyle;
        private GUIStyle _hintStyle;

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
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                _krFont = Resources.Load<Font>("Fonts/KRFont");
            if (_krFont == null)
            {
                try { _krFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "NanumGothic", "Segoe UI" }, 13); }
                catch { _krFont = null; }
            }
            if (_krFont == null) _krFont = Resources.Load<Font>("Fonts/KRFont");
        }

        private void Update()
        {
            if (_noticeTimer > 0f) _noticeTimer -= Time.deltaTime;

            bool cursorFree = GamePrototypeManager.UiCursorFree;
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
                else if (cursorFree)
                {
                    // 브라우저(WebGL)에서 F1/F5 등이 브라우저 기능과 겹칠 때를 위한 대체 키
                    if (kb.digit1Key.wasPressedThisFrame) targetIdx = 0;
                    else if (kb.digit2Key.wasPressedThisFrame) targetIdx = 1;
                    else if (kb.digit3Key.wasPressedThisFrame) targetIdx = 2;
                    else if (kb.digit4Key.wasPressedThisFrame) targetIdx = 3;
                    else if (kb.digit5Key.wasPressedThisFrame) targetIdx = 4;
                    else if (kb.digit6Key.wasPressedThisFrame) targetIdx = 5;
                    else if (kb.digit7Key.wasPressedThisFrame) targetIdx = 6;
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.F1)) targetIdx = 0;
            else if (Input.GetKeyDown(KeyCode.F2)) targetIdx = 1;
            else if (Input.GetKeyDown(KeyCode.F3)) targetIdx = 2;
            else if (Input.GetKeyDown(KeyCode.F4)) targetIdx = 3;
            else if (Input.GetKeyDown(KeyCode.F5)) targetIdx = 4;
            else if (Input.GetKeyDown(KeyCode.F6)) targetIdx = 5;
            else if (Input.GetKeyDown(KeyCode.F7)) targetIdx = 6;
            else if (cursorFree)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) targetIdx = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2)) targetIdx = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3)) targetIdx = 2;
                else if (Input.GetKeyDown(KeyCode.Alpha4)) targetIdx = 3;
                else if (Input.GetKeyDown(KeyCode.Alpha5)) targetIdx = 4;
                else if (Input.GetKeyDown(KeyCode.Alpha6)) targetIdx = 5;
                else if (Input.GetKeyDown(KeyCode.Alpha7)) targetIdx = 6;
            }
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
            var mgr = FindFirstObjectByType<GamePrototypeManager>();
            if (mgr != null) mgr.SaveStateToCampaignStore();
            CampaignProgressStore.OpenMindMapOnNextLoad = false;
            // 씬을 옮긴 뒤에는 다시 1인칭 포인터 락으로 시작한다(WebGL은 첫 클릭에서 잠금).
            GamePrototypeManager.UiCursorFree = false;
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

            _hintStyle = new GUIStyle(GUI.skin.box)
            {
                font = _krFont,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 2, 2)
            };
            _hintStyle.normal.textColor = new Color(0.90f, 0.93f, 0.97f);
        }

        private void OnGUI()
        {
            // 스테이지 마인드맵 / 강화 트리 / 클리어 허브가 열려 있을 때는 하단 테스트 바를 숨겨 겹침 방지!
            var mgr = FindFirstObjectByType<GamePrototypeManager>();
            if (mgr != null && mgr.Phase != GamePhase.Phase1_TrashRemoval && mgr.Phase != GamePhase.Phase2_FloorMopping)
                return;

            EnsureStyles();
            string current = SceneManager.GetActiveScene().name;
            bool cursorFree = GamePrototypeManager.UiCursorFree;

            float y = Screen.height - 34f;

            if (!cursorFree)
            {
                // 1인칭 포인터 락 중: 눌리지 않는 버튼을 그리지 않고, 무엇을 눌러야 하는지 안내만 한다.
                float hintW = Mathf.Round(Mathf.Min(360f, Screen.width - 24f));
                GUI.Box(new Rect(12f, y, hintW, 24f),
                    "[M] ▣   [Tab] ⌖   [F1~F7]", _hintStyle);
            }
            else
            {
                float x = 12f;
                float btnW = 80f;
                float gap = 3f;

                if (GUI.Button(new Rect(x, y, 92f, 24f), "[M] 스테이지 맵", _activeBtnStyle))
                {
                    if (mgr != null) mgr.OpenStageMindMap();
                }
                x += 96f;

                for (int i = 0; i < StageLabels.Length; i++)
                {
                    bool isCur = current == StageSceneNames[i];
                    if (GUI.Button(new Rect(x + i * (btnW + gap), y, btnW, 24f), StageLabels[i], isCur ? _activeBtnStyle : _btnStyle))
                    {
                        LoadStage(i);
                    }
                }

                float hintX = x + StageLabels.Length * (btnW + gap) + 6f;
                if (hintX < Screen.width - 120f)
                    GUI.Box(new Rect(hintX, y, 96f, 24f), "[Tab] ⌖", _hintStyle);
            }

            if (_noticeTimer > 0f && !string.IsNullOrEmpty(_notice))
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 340f, 70f, 680f, 32f), _notice, _noticeStyle);
            }
        }
    }
}
