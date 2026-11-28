using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// TrashCleaner3D v1.8 UI:
    /// - 무설명 UX: 긴 설명 문장 대신 짧은 키 배지([F]·[E]·[M]) · 아이콘 · 수치(+3% · ×1.2) · 색상 펄스로 안내
    /// - 통합 강화소([E]) 내부 268노드 10계열 강화 트리 + 휠 줌·드래그 팬·[R]·뷰포트 컬링
    /// - 수거통이 가득 차면 테두리 색 펄스 + 차량 방향 컴패스(➤ 방향 · m) 표시
    /// - 차량 칩: 적재 제한·배차 대기 없이 정산 배수(×1.0~×1.8)와 편의성 해금(마당/문앞/80% 원격)만 표시
    /// - Stage 2 4방(거실·주방·안방·화장실) 실시간 클리어 체크리스트 + Before→After 점등 플래시
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private enum PostRoundTab { Receipt, CashUpgrades }
        private PostRoundTab _postTab = PostRoundTab.Receipt;
        private GamePhase _lastObservedPhase = GamePhase.Phase1_TrashRemoval;

        private float _pileHpDisplay = 1f;
        private float _pileHpTrail = 1f;

        private bool _stylesReady;
        private Camera _cam;
        private Font _krFont;
        private Texture2D _whiteTex;
        private Texture2D _dimTex;
        private Texture2D _vignetteTex;
        private Texture2D[] _stageThumbs;

        private GUIStyle _inkLeft;
        private GUIStyle _inkBold;
        private GUIStyle _inkCenter;
        private GUIStyle _inkRight;
        private GUIStyle _inkSmall;
        private GUIStyle _inkWrap;
        private GUIStyle _inkTinyRight;

        private GUIStyle _lightLeft;
        private GUIStyle _lightBold;
        private GUIStyle _lightCenter;
        private GUIStyle _lightRight;
        private GUIStyle _lightSmall;
        private GUIStyle _lightBigCenter;

        private GUIStyle _worldPopStyle;
        private GUIStyle _worldPopMoneyStyle;
        private GUIStyle _moneyBadgeStyle;
        private GUIStyle _moneyPendingStyle;
        private GUIStyle _kioskHeaderStyle;
        private GUIStyle _nodeTitleStyle;
        private GUIStyle _nodeSubStyle;
        private GUIStyle _btnStyle;

        private struct TreeNodeDef
        {
            public string id, shortName, fullTitle, statDesc, unlockHint, parentId, desc;
            public Vector2 offset;
            public int level, maxLevel, cost, branch;
            public bool unlocked, affordable, isMilestone, isCore;
            public Color accent;
        }

        private void Awake()
        {
            _whiteTex = MakeTex(Color.white);
            _dimTex = MakeTex(new Color(0.075f, 0.065f, 0.055f, 0.86f));
            _vignetteTex = MakeVignetteTex(64);
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _krFont = Resources.Load<Font>("Fonts/KRFont");
            }
            if (_krFont == null)
            {
                try
                {
                    _krFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "NanumGothic", "Arial" }, 14);
                }
                catch
                {
                    _krFont = null;
                }
            }
            if (_krFont == null) _krFont = Resources.Load<Font>("Fonts/KRFont");

            _stageThumbs = new Texture2D[CampaignProgressStore.TotalStages];
            for (int i = 0; i < CampaignProgressStore.TotalStages; i++)
            {
                _stageThumbs[i] = Resources.Load<Texture2D>(CampaignProgressStore.Stages[i].thumbResourcePath);
            }
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static Texture2D MakeVignetteTex(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            float inv = 1f / (sz - 1);
            for (int y = 0; y < sz; y++)
            {
                float ny = (y * inv) * 2f - 1f;
                for (int x = 0; x < sz; x++)
                {
                    float nx = (x * inv) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(0.42f, 1.18f, r);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            return t;
        }

        private GUIStyle MakeLabelStyle(int size, FontStyle fs, TextAnchor align, Color col, bool wrap = false)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                font = _krFont != null ? _krFont : GUI.skin.font,
                fontSize = size,
                fontStyle = fs,
                alignment = align,
                wordWrap = wrap,
                clipping = wrap ? TextClipping.Clip : TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            s.normal.textColor = col;
            return s;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            Color ink = new Color(0.185f, 0.150f, 0.115f);
            Color white = Color.white;

            _inkLeft = MakeLabelStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            _inkBold = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            _inkCenter = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            _inkRight = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleRight, ink);
            _inkSmall = MakeLabelStyle(12, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            _inkWrap = MakeLabelStyle(12, FontStyle.Normal, TextAnchor.UpperLeft, ink, true);
            _inkTinyRight = MakeLabelStyle(11, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.42f, 0.33f, 0.20f));

            _lightLeft = MakeLabelStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, white);
            _lightBold = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _lightCenter = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _lightRight = MakeLabelStyle(13, FontStyle.Bold, TextAnchor.MiddleRight, white);
            _lightSmall = MakeLabelStyle(12, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _lightBigCenter = MakeLabelStyle(20, FontStyle.Bold, TextAnchor.MiddleCenter, white);

            _worldPopStyle = MakeLabelStyle(17, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _worldPopMoneyStyle = MakeLabelStyle(22, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _moneyBadgeStyle = MakeLabelStyle(26, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.94f, 0.62f));
            _moneyPendingStyle = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.55f, 1f, 0.72f));
            _kioskHeaderStyle = MakeLabelStyle(17, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _nodeTitleStyle = MakeLabelStyle(12, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _nodeSubStyle = MakeLabelStyle(11, FontStyle.Bold, TextAnchor.MiddleCenter, white);

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                font = _krFont != null ? _krFont : GUI.skin.font,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 2, 2)
            };
        }

        private void OnGUI()
        {
            if (manager == null) return;
            EnsureStyles();
            if (_cam == null) _cam = Camera.main;

            if (manager.Phase != _lastObservedPhase)
            {
                if (manager.Phase == GamePhase.RoomCompleted) _postTab = PostRoundTab.Receipt;
                _lastObservedPhase = manager.Phase;
            }

            float w = Screen.width;
            float h = Screen.height;

            DrawScreenOverlays(w, h);
            DrawFloatingWorldPopups(w, h);

            if (manager.Phase == GamePhase.StageMindMap) DrawStageMindMap(w, h);
            else if (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                DrawHud(w, h);
                DrawCrosshairAndPrompt(w, h);
            }
            else if (manager.Phase == GamePhase.TreasureDecision)
            {
                DrawHud(w, h);
                DrawTreasureDecisionModal(w, h);
            }
            else if (manager.Phase == GamePhase.MindMapUpgrade) DrawUpgradeKioskTree(w, h, false);
            else if (manager.Phase == GamePhase.SettingsMenu) DrawSettingsMenu(w, h);
            else if (manager.Phase == GamePhase.RoomCompleted) DrawPostRoundHub(w, h);
        }

        private void DrawScreenOverlays(float w, float h)
        {
            float blur = manager.StenchBlur01;
            if (blur > 0.01f && _vignetteTex != null)
            {
                float pulse = 0.90f + 0.10f * Mathf.Sin(Time.time * 1.8f);
                Color veil = manager.StenchStage >= 3
                    ? new Color(0.56f, 0.44f, 0.24f, 0.52f * blur * pulse)
                    : new Color(0.58f, 0.50f, 0.30f, 0.32f * blur * pulse);
                GUI.color = veil;
                GUI.DrawTexture(new Rect(0, 0, w, h), _vignetteTex);
                GUI.color = Color.white;
            }

            if (!manager.IsOutsideVentilating && manager.StenchPercent >= 75f && _vignetteTex != null)
            {
                float t = Mathf.Clamp01((manager.StenchPercent - 75f) / 25f);
                Color edge = manager.StenchStage >= 3
                    ? new Color(0.50f, 0.14f, 0.44f, Mathf.Lerp(0.18f, 0.50f, t))
                    : new Color(0.38f, 0.46f, 0.12f, Mathf.Lerp(0.12f, 0.38f, t));
                GUI.color = edge;
                GUI.DrawTexture(new Rect(0, 0, w, h), _vignetteTex);
                GUI.color = Color.white;
            }

            if (manager.TreasureFlash01 > 0.01f)
            {
                DrawRect(new Rect(0, 0, w, h), new Color(1.0f, 0.92f, 0.45f, manager.TreasureFlash01 * 0.26f));
            }

            if (manager.StealAlertFlash01 > 0.01f && _vignetteTex != null)
            {
                GUI.color = new Color(0.95f, 0.16f, 0.14f, manager.StealAlertFlash01 * 0.48f);
                GUI.DrawTexture(new Rect(0, 0, w, h), _vignetteTex);
                GUI.color = Color.white;
            }

            if (manager.FreshAirFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(0.65f, 0.92f, 1f, manager.FreshAirFlash01 * 0.12f));
            if (manager.RoomClearFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(1.0f, 0.98f, 0.84f, manager.RoomClearFlash01 * 0.24f));
        }

        private void DrawBorderGlow(float w, float h, Color c, float bw)
        {
            DrawRect(new Rect(0, 0, w, bw), c);
            DrawRect(new Rect(0, h - bw, w, bw), c);
            DrawRect(new Rect(0, bw, bw, h - bw * 2), c);
            DrawRect(new Rect(w - bw, bw, bw, h - bw * 2), c);
        }

        private void DrawFloatingWorldPopups(float w, float h)
        {
            if (_cam == null) return;
            foreach (var ft in manager.FloatingTexts)
            {
                Vector3 sp = _cam.WorldToScreenPoint(ft.worldPos);
                if (sp.z <= 0.1f) continue;
                float sx = Mathf.Round(sp.x), sy = Mathf.Round(h - sp.y);
                float alpha = Mathf.Clamp01(1f - (ft.age / ft.duration) * (ft.age / ft.duration));
                Color c = ft.color; c.a = alpha;

                bool moneyPop = ft.text.IndexOf('$') >= 0;
                GUIStyle st = moneyPop ? _worldPopMoneyStyle : _worldPopStyle;
                float bw = moneyPop ? 420f : 320f;
                float bh = moneyPop ? 34f : 28f;

                st.normal.textColor = new Color(0.09f, 0.075f, 0.055f, alpha * 0.92f);
                GUI.Label(new Rect(sx - bw * 0.5f + 1, sy - bh * 0.5f, bw, bh), ft.text, st);
                GUI.Label(new Rect(sx - bw * 0.5f - 1, sy - bh * 0.5f, bw, bh), ft.text, st);
                GUI.Label(new Rect(sx - bw * 0.5f, sy - bh * 0.5f - 1, bw, bh), ft.text, st);
                GUI.Label(new Rect(sx - bw * 0.5f, sy - bh * 0.5f + 1, bw, bh), ft.text, st);
                st.normal.textColor = c;
                GUI.Label(new Rect(sx - bw * 0.5f, sy - bh * 0.5f, bw, bh), ft.text, st);
            }
        }

        private void DrawHud(float w, float h)
        {
            float topY = Mathf.Round(Mathf.Max(20f, h * 0.030f));
            float botSafe = Mathf.Round(Mathf.Max(28f, h * 0.045f));
            float sideX = Mathf.Round(Mathf.Max(20f, w * 0.025f));

            Color glassBg = new Color(0.07f, 0.09f, 0.13f, 0.78f);
            Color glassLine = new Color(1f, 1f, 1f, 0.16f);

            // ===== 1. 좌상단: 미니멀 다크 글래스 현금 + 슬림 진행 바 =====
            float cardW = 216f;
            int shownMoney = Mathf.RoundToInt(manager.DisplayMoney);
            Rect topCard = new Rect(sideX, topY, cardW, 74f);
            DrawBorderedBox(topCard, glassBg, glassLine, 1);

            GUI.color = new Color(0.98f, 0.88f, 0.42f);
            GUI.Label(new Rect(topCard.x + 12, topCard.y + 6, 130, 30), FormatCash(shownMoney), _moneyBadgeStyle);
            if (manager.CollectingPendingValue > 0)
            {
                GUI.color = new Color(0.45f, 0.94f, 0.68f);
                GUI.Label(new Rect(topCard.x + 110, topCard.y + 12, 94, 20), $"+{FormatCash(manager.CollectingPendingValue)}", _moneyPendingStyle);
            }
            GUI.color = Color.white;

            Rect progBg = new Rect(topCard.x + 12, topCard.y + 44, cardW - 62, 8);
            DrawRect(progBg, new Color(1f, 1f, 1f, 0.14f));
            Color progFill = manager.Phase == GamePhase.Phase2_FloorMopping ? new Color(0.30f, 0.72f, 0.98f) : new Color(0.28f, 0.86f, 0.54f);
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * (manager.OverallCleanPercent / 100f), progBg.height), progFill);
            GUI.color = new Color(0.90f, 0.93f, 0.98f);
            GUI.Label(new Rect(topCard.xMax - 46, topCard.y + 38, 36, 20), $"{manager.OverallCleanPercent:0}%", _lightRight);

            int availUpgrades = manager.CountAffordableCashUpgrades();
            float leftNextY = topCard.yMax + 6f;
            if (availUpgrades > 0)
            {
                float p = 0.55f + 0.45f * Mathf.Sin(Time.time * 4.5f);
                Rect tabChip = new Rect(sideX, leftNextY, 108f, 24f);
                DrawBorderedBox(tabChip, new Color(0.14f, 0.12f, 0.06f, 0.86f), Color.Lerp(new Color(0.82f, 0.66f, 0.22f), new Color(1f, 0.92f, 0.45f), p), 1);
                GUI.color = new Color(1f, 0.92f, 0.48f);
                GUI.Label(tabChip, $"[Tab] +{availUpgrades}", _nodeSubStyle);
                GUI.color = Color.white;
                leftNextY += 28f;
            }
            if (manager.CanEarlyCheckout)
            {
                Rect kChip = new Rect(sideX, leftNextY, 108f, 24f);
                DrawBorderedBox(kChip, new Color(0.24f, 0.08f, 0.08f, 0.86f), new Color(0.94f, 0.36f, 0.32f), 1);
                GUI.color = new Color(1f, 0.72f, 0.68f);
                GUI.Label(kChip, "[K] 조기 정산", _nodeSubStyle);
                GUI.color = Color.white;
                leftNextY += 28f;
            }
            if (manager.TrustBuffTimer > 0.2f)
            {
                Rect bChip = new Rect(sideX, leftNextY, 128f, 22f);
                DrawBorderedBox(bChip, new Color(0.06f, 0.22f, 0.20f, 0.84f), new Color(0.32f, 0.94f, 0.76f), 1);
                GUI.color = new Color(0.60f, 1f, 0.86f);
                GUI.Label(bChip, $"감사 버프 {manager.TrustBuffTimer:0}s", _nodeSubStyle);
                GUI.color = Color.white;
            }

            // ===== 2. 상단 중앙: 악취 1% 이상일 때만 슬림 4단계 바 표시 =====
            float barW = Mathf.Round(Mathf.Min(280f, w * 0.30f));
            float barX = Mathf.Round((w - barW) * 0.5f);
            float topCenterY = topY;

            if (manager.StenchPercent >= 1f)
            {
                Rect tc = new Rect(barX, topCenterY, barW, 26f);
                DrawBorderedBox(tc, glassBg, glassLine, 1);

                int st = manager.StenchStage;
                Color stCol = manager.IsOutsideVentilating ? new Color(0.38f, 0.84f, 1f) :
                    st == 0 ? new Color(0.34f, 0.86f, 0.48f) :
                    st == 1 ? new Color(0.96f, 0.84f, 0.22f) :
                    st == 2 ? new Color(0.98f, 0.46f, 0.16f) : new Color(0.86f, 0.24f, 0.90f);

                float cellStartX = tc.x + 10f;
                float trackW = barW - 20f;
                float[] segStarts = { 0f, 75f, 90f, 99.5f };
                float[] segEnds   = { 75f, 90f, 99.5f, 100f };
                float[] segRatios = { 0.54f, 0.20f, 0.16f, 0.10f };
                float curX = cellStartX;
                for (int i = 0; i < 4; i++)
                {
                    float cw = Mathf.Floor((trackW - 9f) * segRatios[i]);
                    Rect cell = new Rect(curX, tc.y + 8, cw, 10);
                    DrawRect(cell, new Color(1f, 1f, 1f, 0.14f));
                    float seg = Mathf.Clamp01((manager.StenchPercent - segStarts[i]) / Mathf.Max(0.5f, segEnds[i] - segStarts[i]));
                    if (seg > 0.001f) DrawRect(new Rect(cell.x, cell.y, cell.width * seg, cell.height), stCol);
                    curX += cw + 3f;
                }
                topCenterY = tc.yMax + 6f;
            }

            if (manager.SuspicionPercent > 0.5f)
            {
                Rect suspR = new Rect(barX + Mathf.Round((barW - 130f) * 0.5f), topCenterY, 130f, 22f);
                bool highSusp = manager.SuspicionPercent >= 50f;
                DrawBorderedBox(suspR, new Color(0.22f, 0.06f, 0.08f, 0.86f), highSusp ? new Color(0.96f, 0.28f, 0.24f) : new Color(0.82f, 0.52f, 0.24f), 1);
                GUI.color = highSusp ? new Color(1f, 0.55f, 0.52f) : new Color(1f, 0.84f, 0.56f);
                GUI.Label(suspR, $"발각 위험 {manager.SuspicionPercent:0}%", _nodeSubStyle);
                GUI.color = Color.white;
            }

            // ===== 3. 우상단: 의뢰 메모 [H] (기본 접힘 → 미니멀 칩) =====
            bool showMemo = controller != null && controller.ShowHelp;
            if (!showMemo)
            {
                Rect chip = new Rect(w - sideX - 84, topY, 84, 26);
                DrawBorderedBox(chip, glassBg, glassLine, 1);
                GUI.color = new Color(0.86f, 0.90f, 0.96f);
                GUI.Label(chip, "[H] 메모", _nodeSubStyle);
                GUI.color = Color.white;
            }
            else
            {
                Rect postItRect = new Rect(w - sideX - 246, topY, 246, 74);
                DrawBorderedBox(postItRect, glassBg, new Color(0.96f, 0.82f, 0.34f, 0.55f), 1);
                GUI.color = new Color(1f, 0.90f, 0.45f);
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 6, 150, 18), "의뢰 메모 [H]", _lightSmall);
                GUI.color = new Color(0.90f, 0.93f, 0.97f);
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 26, postItRect.width - 20, 22), manager.CurrentClientHint, _lightSmall);
                string lostStatus = manager.FoundClientLostItem ? "● 반환 완료 (5성)"
                                  : manager.StoleDesignatedItem ? "! 분실물 슈킹됨"
                                  : "쓰레기 속 탐색 중";
                GUI.color = manager.FoundClientLostItem ? new Color(0.40f, 0.94f, 0.65f)
                          : manager.StoleDesignatedItem ? new Color(1f, 0.46f, 0.42f)
                          : new Color(0.70f, 0.76f, 0.86f);
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 48, postItRect.width - 20, 18), lostStatus, _nodeSubStyle);
                GUI.color = Color.white;
            }

            // ===== 4. 하단 중앙: 미니멀 다크 수거통 게이지 + [1][2] 슬롯 =====
            DrawBottomBinAndVehicleBar(w, h, botSafe);
            DrawVehicleCompass(w, h, botSafe);
        }

        /// <summary>
        /// [v1.8] 화면 아래 작은 컴퍼스 — 수거 차량(트럭) 방향과 거리만 보여 준다.
        /// 수거통이 70% 이상 차면 더 진하게/가득 차면 펄스 — 글 없이 동선을 알려 준다.
        /// </summary>
        private void DrawVehicleCompass(float w, float h, float botSafe)
        {
            if (manager.truckBedGoal == null || _cam == null) return;
            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping) return;
            Vector3 tp = manager.truckBedGoal.position;
            Vector3 d = tp - _cam.transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.6f) return;
            Vector3 f = _cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
            float ang = Vector3.SignedAngle(f.normalized, d.normalized, Vector3.up);

            bool full = manager.IsBinFull;
            float fill = manager.BinFill01;
            if (!full && fill < 0.70f) return;   // 거의 빌 때는 안 보여 준다(화면 정리)

            float cx = Mathf.Round(w * 0.5f);
            float cy = Mathf.Round(h - botSafe - 82f);
            Color accent = full ? Color.Lerp(new Color(0.96f, 0.42f, 0.20f), new Color(1f, 0.86f, 0.42f), 0.5f + 0.5f * Mathf.Sin(Time.time * 7f))
                               : new Color(0.46f, 0.74f, 0.44f);
            Rect chip = new Rect(cx - 56f, cy - 13f, 112f, 26f);
            DrawPaperCard(chip, accent, 1);

            Matrix4x4 prev = GUI.matrix;
            Color prevCol = GUI.color;
            GUI.color = accent;
            GUIUtility.RotateAroundPivot(ang, new Vector2(cx - 30f, cy));
            GUI.Label(new Rect(cx - 46f, cy - 11f, 32f, 22f), "▶", _nodeSubStyle);
            GUI.matrix = prev;
            GUI.color = prevCol;
            GUI.color = new Color(0.26f, 0.21f, 0.15f);
            GUI.Label(new Rect(cx - 10f, cy - 11f, 62f, 22f), $"차량 {dist:0}m", _nodeSubStyle);
            GUI.color = Color.white;
        }

        private void DrawBottomBinAndVehicleBar(float w, float h, float botSafe)
        {
            // [v2.2] 미니멀 다크 글래스 수거통 바 + [1][2] 슬롯 (불필요한 긴 차량 텍스트 바 & 중앙 FULL 배너 삭제!)
            float bw = 280f;
            float bx = Mathf.Round((w - bw) * 0.5f);
            float by = Mathf.Round(h - botSafe - 42f);

            Color glassBg = new Color(0.07f, 0.09f, 0.13f, 0.80f);
            bool inZone = controller != null && controller.SettlementZoneActive;
            bool full = manager.IsBinFull;
            Color bd = inZone ? new Color(0.96f, 0.82f, 0.28f)
                     : full ? Color.Lerp(new Color(0.90f, 0.26f, 0.20f), new Color(1f, 0.56f, 0.34f), 0.5f + 0.5f * Mathf.Sin(Time.time * 7f))
                     : new Color(1f, 1f, 1f, 0.16f);

            Rect card = new Rect(bx, by, bw, 40f);
            DrawBorderedBox(card, glassBg, bd, (inZone || full) ? 2 : 1);

            GUI.color = full ? new Color(1f, 0.54f, 0.44f) : new Color(0.90f, 0.93f, 0.98f);
            GUI.Label(new Rect(card.x + 12, card.y + 4, 120, 20), $"{manager.CollectingCount} / {manager.BinCapacity}", _lightSmall);
            if (manager.CollectingCount > 0)
            {
                GUI.color = inZone ? new Color(1f, 0.88f, 0.36f) : new Color(0.55f, 0.94f, 0.72f);
                string rightStr = inZone ? $"[RMB] {FormatCash(manager.CollectingPendingValue)}" : FormatCash(manager.CollectingPendingValue);
                GUI.Label(new Rect(card.x + bw - 140, card.y + 4, 128, 20), rightStr, _lightRight);
            }
            GUI.color = Color.white;

            Rect track = new Rect(card.x + 12, card.y + 26, bw - 24, 7);
            DrawRect(track, new Color(1f, 1f, 1f, 0.14f));
            Color fillC = full ? new Color(0.94f, 0.32f, 0.24f) : new Color(0.32f, 0.74f, 0.94f);
            DrawRect(new Rect(track.x, track.y, track.width * manager.BinFill01, track.height), fillC);

            // [1] / [2] 슬림 슬롯
            bool isMop = manager.IsMopSlot;
            Rect slot1 = new Rect(bx + bw + 8f, by, 44f, 40f);
            Rect slot2 = new Rect(bx + bw + 56f, by, 44f, 40f);
            DrawBorderedBox(slot1, glassBg, !isMop ? new Color(0.98f, 0.80f, 0.28f) : new Color(1f, 1f, 1f, 0.14f), !isMop ? 2 : 1);
            GUI.color = !isMop ? new Color(1f, 0.90f, 0.45f) : new Color(0.60f, 0.65f, 0.72f);
            GUI.Label(slot1, "1", _lightCenter);

            DrawBorderedBox(slot2, glassBg, isMop ? new Color(0.32f, 0.78f, 0.98f) : new Color(1f, 1f, 1f, 0.14f), isMop ? 2 : 1);
            GUI.color = isMop ? new Color(0.45f, 0.88f, 1f) : new Color(0.60f, 0.65f, 0.72f);
            GUI.Label(slot2, "2", _lightCenter);
            GUI.color = Color.white;
        }

        private void DrawCrosshairAndPrompt(float w, float h)
        {
            if (controller == null) return;
            float cx = Mathf.Round(w * 0.5f), cy = Mathf.Round(h * 0.5f);

            if (GamePrototypeManager.UiCursorFree)
            {
                Rect uiR = new Rect(cx - 170, cy - 22, 340, 40);
                DrawPaperCard(uiR, new Color(0.24f, 0.52f, 0.80f), 2);
                GUI.color = new Color(0.10f, 0.24f, 0.44f);
                GUI.Label(uiR, "[Alt] 하단 테스트 버튼 선택 ON", _lightCenter);
                GUI.color = Color.white;
                return;
            }

            if (Application.platform == RuntimePlatform.WebGLPlayer && Cursor.lockState != CursorLockMode.Locked)
            {
                Rect lockR = new Rect(cx - 130, cy - 20, 260, 40);
                DrawPaperCard(lockR, new Color(0.76f, 0.52f, 0.14f), 2);
                GUI.Label(lockR, "▶ 화면 클릭 · 1인칭 시작", _inkCenter);
                return;
            }

            bool hasTarget = controller.AimDoor != null || controller.AimTreasure != null || controller.AimCctv != null ||
                             controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain;
            Color dotCol = hasTarget ? new Color(0.24f, 0.58f, 0.36f) : new Color(0.20f, 0.17f, 0.13f, 0.92f);
            float outer = hasTarget ? 9f : 7f;
            DrawRect(new Rect(cx - outer * 0.5f, cy - outer * 0.5f, outer, outer), new Color(1f, 0.985f, 0.95f, 0.78f));
            float sz = hasTarget ? 5f : 3f;
            DrawRect(new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz), dotCol);

            if (controller.AimIsPile)
            {
                float hp = Mathf.Clamp01(controller.AimPileHp01);
                _pileHpDisplay = Mathf.MoveTowards(_pileHpDisplay, hp, Time.deltaTime * 3.2f);
                if (hp > _pileHpDisplay) _pileHpDisplay = hp;
                _pileHpTrail = Mathf.Max(hp, Mathf.MoveTowards(_pileHpTrail, hp, Time.deltaTime * 0.85f));

                Rect pb = new Rect(cx - 44, cy + 14, 88, 9);
                DrawPaperCard(pb, new Color(0.55f, 0.47f, 0.34f), 1);
                Rect inner = new Rect(pb.x + 1, pb.y + 1, pb.width - 2, pb.height - 2);
                DrawRect(inner, new Color(0.24f, 0.20f, 0.16f, 0.55f));
                if (_pileHpTrail > 0.005f)
                    DrawRect(new Rect(inner.x, inner.y, inner.width * _pileHpTrail, inner.height), new Color(0.98f, 0.94f, 0.80f, 0.55f));
                Color hpC = hp > 0.6f ? new Color(0.36f, 0.74f, 0.42f) : hp > 0.3f ? new Color(0.94f, 0.72f, 0.20f) : new Color(0.90f, 0.32f, 0.22f);
                if (_pileHpDisplay > 0.005f)
                    DrawRect(new Rect(inner.x, inner.y, inner.width * _pileHpDisplay, inner.height), hpC);
            }
            else
            {
                _pileHpDisplay = 1f;
                _pileHpTrail = 1f;
            }

            if (!controller.AimIsPile && controller.AimProgress01 > 0.01f && controller.AimProgress01 < 0.999f)
            {
                Rect pb = new Rect(cx - 34, cy + 14, 68, 7);
                DrawPaperCard(pb, new Color(0.55f, 0.47f, 0.34f), 1);
                DrawRect(new Rect(pb.x + 1, pb.y + 1, (pb.width - 2) * controller.AimProgress01, pb.height - 2), new Color(0.92f, 0.74f, 0.18f));
            }

            string prompt = null;
            float promptW = 330f;
            if (controller.AimTreasure != null)
            {
                var tr = controller.AimTreasure;
                promptW = 470f;
                promptW = 360f;
                prompt = tr.isDesignatedLostItem
                    ? $"★ [F / 클릭] 의뢰인 분실물 '{tr.itemName}' 확인!"
                    : $"★ [F / 클릭] 숨겨진 보물 '{tr.itemName}' 확인!";
            }
            else if (controller.AimCctv != null && manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                promptW = 360f;
                prompt = "[LMB 꾹] CCTV 거품 분사 무력화 (25초)";
            }
            else if (controller.SettlementZoneActive && manager.CollectingCount > 0)
            {
                prompt = $"[RMB] {manager.CollectingCount}개 · ${manager.CollectingPendingValue:N0} ×{manager.VehicleEffectiveSettleMult:0.00}";
            }
            else if (controller.SettlementZoneActive && manager.CollectingCount <= 0)
            {
                prompt = $"{manager.CurrentVehicleName} · 비어있음";
            }
            else if (controller.AimDoor != null)
            {
                prompt = controller.AimDoor.IsOpen ? "[F] 현관문 닫기" : "[F] 현관문 열기";
            }
            else if (controller.NeedMouseShakeHint)
            {
                prompt = "누르기 + 마우스 좌우 흔들기";
            }
            else if (controller.AimIsPile)
            {
                prompt = "[LMB 클릭] 큰 봉투/박스 타격!";
            }
            else if (manager.Phase == GamePhase.Phase1_TrashRemoval && manager.Mode == GearMode.Orb && !controller.OrbCharging)
            {
                prompt = "[LMB 꾹] 원기옥 모으기";
            }
            else if (manager.Phase == GamePhase.Phase1_TrashRemoval && manager.Mode == GearMode.BlackHole && !controller.BlackHoleDeployed)
            {
                prompt = $"[LMB 꾹] 블랙홀 ×{manager.BlackHoleCount} 전개";
            }
            else if (manager.IsBinFull)
            {
                prompt = manager.FillBinTag;
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Rect pr = new Rect(cx - promptW * 0.5f, cy + 28, promptW, 30);
                DrawPaperCard(pr, controller.AimTreasure != null ? new Color(0.95f, 0.72f, 0.16f) : new Color(0.52f, 0.44f, 0.32f), controller.AimTreasure != null ? 2 : 1);
                GUI.color = new Color(0.22f, 0.17f, 0.12f);
                GUI.Label(pr, prompt, _lightCenter);
                GUI.color = Color.white;
            }

            if (controller.KioskActive)
            {
                float fy = string.IsNullOrEmpty(prompt) ? cy + 32f : cy + 64f;
                Rect fp = new Rect(cx - 225f, fy, 450f, 50f);
                DrawPaperCard(fp, controller.KioskColor, 2);

                GUI.color = Color.Lerp(controller.KioskColor, new Color(0.24f, 0.18f, 0.10f), 0.55f);
                GUI.Label(new Rect(fp.x + 10f, fp.y + 5f, fp.width - 20f, 22f), controller.KioskTitle, _lightBold);
                GUI.color = new Color(0.32f, 0.28f, 0.22f);
                GUI.Label(new Rect(fp.x + 10f, fp.y + 26f, fp.width - 20f, 20f), controller.KioskDetail, _lightSmall);
                GUI.color = Color.white;
            }
        }

        // ===== v1.9 [Tab] 강화 트리 · 완벽한 클리핑 + 고대비 슬레이트 + 정예 108노드 10계열 =====
        private const float TreeZoomMin = 0.28f;
        private const float TreeZoomMax = 1.45f;
        private const float TreeZoomDefault = 0.90f;
        private float _treeZoom = TreeZoomDefault;
        private Vector2 _treePan;
        private bool _treeDragging;
        private bool _treeDragged;
        private int _selectedBranch = -1;
        private bool _showAllLockedNodes = false;

        private static Color BranchColor(int branch)
        {
            if (branch < 0 || branch >= UpgradeTreeV18.Branches.Length)
                return new Color(0.96f, 0.80f, 0.36f);
            return UpgradeTreeV18.Branches[branch].color;
        }

        private struct TreeMeta
        {
            public string id, shortName, fullTitle, statDesc, unlockHint, parentId, desc;
            public Vector2 offset;
            public int maxLevel, branch;
            public bool isMilestone, isCore;
            public Color accent;
        }

        private List<TreeMeta> _treeMeta;
        private readonly List<TreeNodeDef> _treeNodesBuffer = new List<TreeNodeDef>(128);

        private void ResetTreeView()
        {
            _treeZoom = TreeZoomDefault;
            _treePan = Vector2.zero;
            _selectedBranch = -1;
        }

        private void FocusBranch(int branchIdx, Rect view)
        {
            if (_selectedBranch == branchIdx)
            {
                _selectedBranch = -1;
                _treePan = Vector2.zero;
                return;
            }
            _selectedBranch = branchIdx;
            if (branchIdx >= 0 && branchIdx < UpgradeTreeV18.Branches.Length)
            {
                float rad = UpgradeTreeV18.Branches[branchIdx].angle * Mathf.Deg2Rad;
                _treeZoom = 0.84f;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                bool wide = Mathf.Abs(dir.x) > 0.82f;
                _treePan = -dir * ((wide ? 540f : 350f) * _treeZoom);
                ClampTreePan(view);
            }
        }

        private static Dictionary<string, Texture2D> _pixelIcons;

        private static Texture2D MakePixelIcon(string[] rows)
        {
            int h = rows.Length;
            int w = rows[0].Length;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                string row = rows[h - 1 - y];
                for (int x = 0; x < w; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    tex.SetPixel(x, y, c == '#' ? Color.white : c == 'o' ? new Color(1f, 1f, 1f, 0.55f) : clear);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D GetNodeIcon(string id, int branch)
        {
            if (_pixelIcons == null)
            {
                _pixelIcons = new Dictionary<string, Texture2D>
                {
                    ["hand"] = MakePixelIcon(new[] {
                        "....##..##......",
                        "...##.##.##.....",
                        "...##.##.##.#...",
                        "...##.##.####...",
                        "...##########...",
                        "..###########...",
                        "..##########....",
                        "...########.....",
                        "....######......",
                        "....######......",
                        "....######......",
                        ".....####......."
                    }),
                    ["speed"] = MakePixelIcon(new[] {
                        "........###.....",
                        ".......###......",
                        "......###.......",
                        ".....###........",
                        "....########....",
                        "........###.....",
                        ".......###......",
                        "......###.......",
                        ".....###........",
                        "....###.........",
                        "...##...........",
                        "................"
                    }),
                    ["radius"] = MakePixelIcon(new[] {
                        "....########....",
                        "..##........##..",
                        ".#....####....#.",
                        ".#..##....##..#.",
                        "#..#...##...#..#",
                        "#..#..####..#..#",
                        "#..#..####..#..#",
                        "#..#...##...#..#",
                        ".#..##....##..#.",
                        ".#....####....#.",
                        "..##........##..",
                        "....########...."
                    }),
                    ["fist"] = MakePixelIcon(new[] {
                        "...##.##.##.....",
                        "..###########...",
                        "..###########...",
                        "..###########...",
                        "..###########...",
                        "...#########....",
                        "....#######.....",
                        "....#######.....",
                        ".....#####......",
                        ".....#####......",
                        "................",
                        "................"
                    }),
                    ["bag"] = MakePixelIcon(new[] {
                        ".....######.....",
                        "....##....##....",
                        "...##########...",
                        "..############..",
                        "..##..####..##..",
                        "..############..",
                        "..############..",
                        "..############..",
                        "..############..",
                        "...##########...",
                        "....########....",
                        "................"
                    }),
                    ["dollar"] = MakePixelIcon(new[] {
                        ".......##.......",
                        "....########....",
                        "...##..##..##...",
                        "...##..##.......",
                        "....########....",
                        ".......##..##...",
                        ".......##..##...",
                        "...##..##..##...",
                        "....########....",
                        ".......##.......",
                        "................",
                        "................"
                    }),
                    ["truck"] = MakePixelIcon(new[] {
                        "................",
                        "..########......",
                        "..########.###..",
                        "..########.#..#.",
                        "..#############.",
                        "..#############.",
                        "..#############.",
                        "...##.......##..",
                        "..####.....####.",
                        "...##.......##..",
                        "................",
                        "................"
                    }),
                    ["cyclone"] = MakePixelIcon(new[] {
                        "..############..",
                        ".##############.",
                        "....########....",
                        "...##########...",
                        ".....######.....",
                        "....########....",
                        "......####......",
                        ".....######.....",
                        ".......##.......",
                        "......###.......",
                        ".......#........",
                        "................"
                    }),
                    ["robot"] = MakePixelIcon(new[] {
                        ".......##.......",
                        "....########....",
                        "...##########...",
                        "..###..##..###..",
                        "..############..",
                        "..###.####.###..",
                        "...##########...",
                        "....########....",
                        "...##......##...",
                        "................",
                        "................",
                        "................"
                    }),
                    ["flame"] = MakePixelIcon(new[] {
                        ".......##.......",
                        "......####......",
                        ".....#####......",
                        "....###.###.....",
                        "...###...###....",
                        "...###.##.###...",
                        "..####.##.####..",
                        "..############..",
                        "...##########...",
                        "....########....",
                        ".....######.....",
                        "................"
                    }),
                    ["mask"] = MakePixelIcon(new[] {
                        "....########....",
                        "..############..",
                        ".###..####..###.",
                        ".###..####..###.",
                        ".##############.",
                        "..############..",
                        "...###.##.###...",
                        "..###..##..###..",
                        "..##...##...##..",
                        "......####......",
                        "................",
                        "................"
                    }),
                    ["mop"] = MakePixelIcon(new[] {
                        ".......##.......",
                        ".......##.......",
                        ".......##.......",
                        ".......##.......",
                        ".......##.......",
                        ".....######.....",
                        "...##########...",
                        "..##.##.##.##...",
                        "..##.##.##.##...",
                        "..##.##.##.##...",
                        "................",
                        "................"
                    }),
                    ["gem"] = MakePixelIcon(new[] {
                        "....########....",
                        "...##########...",
                        "..###.####.###..",
                        ".##############.",
                        "..############..",
                        "...##########...",
                        "....########....",
                        ".....######.....",
                        "......####......",
                        ".......##.......",
                        "................",
                        "................"
                    }),
                    ["boot"] = MakePixelIcon(new[] {
                        "....#####.......",
                        "....#####.......",
                        "....#####.......",
                        "....#####.......",
                        "....########....",
                        "....##########..",
                        "....###########.",
                        "....###########.",
                        "...############.",
                        "................",
                        "................",
                        "................"
                    })
                };
            }

            if (id.StartsWith("hold") || id.StartsWith("suction_0_0") || id.StartsWith("suction_0_2")) return _pixelIcons["speed"];
            if (id.StartsWith("rad") || id.StartsWith("suction_0_1") || id.StartsWith("suction_0_3")) return _pixelIcons["radius"];
            if (id == "broom" || id == "vacuum" || id.StartsWith("suction")) return _pixelIcons["hand"];
            if (id.StartsWith("fist") || id.StartsWith("bag")) return _pixelIcons["fist"];
            if (id.StartsWith("bin")) return _pixelIcons["bag"];
            if (id.StartsWith("price") || id.StartsWith("combo")) return _pixelIcons["dollar"];
            if (id == "cart" || id == "damas" || id == "truck1t" || id.StartsWith("vehicle")) return _pixelIcons["truck"];
            if (id.StartsWith("gear")) return _pixelIcons["cyclone"];
            if (id.StartsWith("roomba") || id.StartsWith("rspd")) return _pixelIcons["robot"];
            if (id.StartsWith("fever")) return _pixelIcons["flame"];
            if (id.StartsWith("speed")) return _pixelIcons["boot"];
            if (id.StartsWith("mask") || id.StartsWith("oxygen")) return _pixelIcons["mask"];
            if (id == "mop" || id == "scrub" || id.StartsWith("stain")) return _pixelIcons["mop"];
            if (id.StartsWith("trash")) return _pixelIcons["gem"];
            return branch switch
            {
                0 => _pixelIcons["hand"], 1 => _pixelIcons["fist"], 2 => _pixelIcons["bag"],
                3 => _pixelIcons["truck"], 4 => _pixelIcons["cyclone"], 5 => _pixelIcons["robot"],
                6 => _pixelIcons["flame"], 7 => _pixelIcons["mask"], 8 => _pixelIcons["mop"],
                _ => _pixelIcons["gem"]
            };
        }

        private void DrawOrthogonalBranch(Vector2 a, Vector2 b, Color col, float width)
        {
            if (Mathf.Abs(a.x - b.x) < 2f || Mathf.Abs(a.y - b.y) < 2f)
            {
                DrawLine(a, b, col, width);
                return;
            }
            // 레퍼런스 스타일 깔끔한 직각 L자 가지!
            Vector2 corner = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y)
                ? new Vector2(b.x, a.y)
                : new Vector2(a.x, b.y);
            DrawLine(a, corner, col, width);
            DrawLine(corner, b, col, width);
        }

        private void DrawUpgradeKioskTree(float w, float h, bool embeddedInHub)
        {
            if (!embeddedInHub) GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topPad = embeddedInHub ? 76f : 18f;
            float botPad = embeddedInHub ? 70f : 18f;
            float pw = Mathf.Min(w - 28f, 1220f);
            float ph = Mathf.Min(h - topPad - botPad, 720f);
            Rect panel = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round(topPad + (h - topPad - botPad - ph) * 0.5f), pw, ph);

            // [v2.2] 레퍼런스 스타일 초미니멀 다크 트리 패널 (긴 설명/헤더/10개 탭 바 전부 제거!)
            DrawBorderedBox(panel, new Color(0.055f, 0.068f, 0.095f, 0.985f), new Color(1f, 1f, 1f, 0.16f), 2);

            // 상단 미니멀 헤더: 좌측 '강화', 우측 '$돈' + 닫기 'X'만!
            Rect hdr = new Rect(panel.x + 14, panel.y + 10, panel.width - 28, 38);
            GUI.color = new Color(0.94f, 0.96f, 1f);
            GUI.Label(new Rect(hdr.x + 4, hdr.y + 4, 140, 30), "강화", _kioskHeaderStyle);

            float closeW = embeddedInHub ? 0f : 38f;
            Rect cashR = new Rect(hdr.xMax - closeW - 188f, hdr.y + 2, 180f, 34f);
            DrawBorderedBox(cashR, new Color(0.05f, 0.18f, 0.12f, 0.95f), new Color(0.96f, 0.84f, 0.36f, 0.75f), 1);
            GUI.color = new Color(1f, 0.92f, 0.50f);
            GUI.Label(new Rect(cashR.x + 10, cashR.y + 3, 160, 28), FormatCash(manager.Money), _moneyBadgeStyle);
            GUI.color = Color.white;

            if (!embeddedInHub)
            {
                Rect closeR = new Rect(hdr.xMax - 36f, hdr.y + 2, 34f, 34f);
                DrawBorderedBox(closeR, new Color(0.16f, 0.12f, 0.14f, 0.95f), new Color(1f, 1f, 1f, 0.25f), 1);
                GUI.color = new Color(0.95f, 0.95f, 0.98f);
                GUI.Label(closeR, "X", _lightCenter);
                GUI.color = Color.white;
                if (GUI.Button(closeR, GUIContent.none, GUIStyle.none)) manager.CloseUpgradeKiosk();
            }

            // 전체 뷰포트 캔버스 (화면의 90% 이상을 시원하게 트리에 할애!)
            Rect view = new Rect(panel.x + 12, hdr.yMax + 8f, panel.width - 24, panel.yMax - hdr.yMax - 20f);
            DrawBorderedBox(view, new Color(0.042f, 0.052f, 0.075f, 0.99f), new Color(1f, 1f, 1f, 0.10f), 1);

            Event e = Event.current;
            Vector2 mPos = e.mousePosition;
            bool mouseInView = view.Contains(mPos);
            Vector2 localMouse = mPos - view.position;
            Vector2 viewLocalCenter = new Vector2(view.width * 0.5f, view.height * 0.5f);
            float scale = _treeZoom;
            Vector2 localCenter = viewLocalCenter + _treePan;
            float nodeSz = Mathf.Round(52f * scale);

            var nodes = BuildTreeNodes();
            var idToIdx = new Dictionary<string, int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++) idToIdx[nodes[i].id] = i;

            bool[] visible = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.unlocked || n.level > 0 || string.IsNullOrEmpty(n.parentId)) visible[i] = true;
                else if (idToIdx.TryGetValue(n.parentId, out int pIdx) && (nodes[pIdx].unlocked || nodes[pIdx].level > 0)) visible[i] = true;
            }

            int hoveredIdx = -1;
            if (mouseInView)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!visible[i]) continue;
                    Rect r = NodeRectOf(nodes[i], localCenter, scale, nodeSz, nodeSz);
                    if (r.Contains(localMouse)) { hoveredIdx = i; break; }
                }
            }

            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.R || e.keyCode == KeyCode.Home)) { ResetTreeView(); e.Use(); }
            else if (e.type == EventType.ScrollWheel && mouseInView)
            {
                float old = _treeZoom;
                float next = Mathf.Clamp(old * (e.delta.y > 0f ? 0.88f : 1.12f), TreeZoomMin, TreeZoomMax);
                if (!Mathf.Approximately(next, old))
                {
                    Vector2 treePoint = (localMouse - viewLocalCenter - _treePan) / old;
                    _treePan = localMouse - viewLocalCenter - treePoint * next;
                    _treeZoom = next;
                    ClampTreePan(view);
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDown && mouseInView && e.button == 0 && hoveredIdx >= 0)
            {
                var hv = nodes[hoveredIdx];
                if (hv.unlocked && hv.level < hv.maxLevel && manager.Money >= hv.cost) manager.BuyNode(hv.id);
                _treeDragged = false;
                _treeDragging = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDown && mouseInView && (e.button == 1 || e.button == 2 || e.button == 0))
            {
                _treeDragging = true;
                _treeDragged = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _treeDragging)
            {
                _treePan += e.delta;
                if (e.delta.sqrMagnitude > 0.01f) _treeDragged = true;
                ClampTreePan(view);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _treeDragging)
            {
                _treeDragging = false;
                e.Use();
            }

            scale = _treeZoom;
            localCenter = viewLocalCenter + _treePan;
            nodeSz = Mathf.Round(52f * scale);
            float affordPulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 5.2f);
            string hoveredId = hoveredIdx >= 0 ? nodes[hoveredIdx].id : null;
            string hoveredParentId = hoveredIdx >= 0 ? nodes[hoveredIdx].parentId : null;

            Rect viewLocalBounds = new Rect(0f, 0f, view.width, view.height);
            GUI.BeginGroup(view);

            // 미세한 도트/격자 배경 (레퍼런스 감성)
            float gridStep = Mathf.Max(28f, 76f * scale);
            for (float gx = Mathf.Repeat(_treePan.x + view.width * 0.5f, gridStep); gx < view.width; gx += gridStep)
                DrawRect(new Rect(gx, 0f, 1f, view.height), new Color(1f, 1f, 1f, 0.04f));
            for (float gy = Mathf.Repeat(_treePan.y + view.height * 0.5f, gridStep); gy < view.height; gy += gridStep)
                DrawRect(new Rect(0f, gy, view.width, 1f), new Color(1f, 1f, 1f, 0.04f));

            // 1) 깔끔한 직각(L자) 연결 가지
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!visible[i]) continue;
                var n = nodes[i];
                Vector2 nPos = localCenter + n.offset * scale;
                Vector2 pPos = localCenter;
                if (!string.IsNullOrEmpty(n.parentId))
                {
                    if (!idToIdx.TryGetValue(n.parentId, out int pIdx) || !visible[pIdx]) continue;
                    pPos = localCenter + nodes[pIdx].offset * scale;
                }

                bool isLink = hoveredId != null && (n.id == hoveredId || n.parentId == hoveredId);
                Color lineCol = isLink ? new Color(1f, 0.90f, 0.34f, 1f)
                              : n.level > 0 ? new Color(n.accent.r, n.accent.g, n.accent.b, 0.85f)
                              : n.unlocked ? new Color(n.accent.r, n.accent.g, n.accent.b, 0.50f)
                              : new Color(0.35f, 0.40f, 0.50f, 0.30f);
                float lineW = isLink ? 3.5f : n.level > 0 ? 2.6f : 1.8f;
                DrawOrthogonalBranch(pPos, nPos, lineCol, lineW);
            }

            // 2) 정사각형 픽셀아트 아이콘 노드 (글자 0개! 아이콘 + 하단 레벨 점만!)
            TreeNodeDef? hovered = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!visible[i]) continue;
                var n = nodes[i];
                Rect r = NodeRectOf(n, localCenter, scale, nodeSz, nodeSz);
                if (!viewLocalBounds.Overlaps(r)) continue;

                bool isLineage = hoveredId != null && (n.id == hoveredId || n.id == hoveredParentId || n.parentId == hoveredId);
                bool isMax = n.level >= n.maxLevel;

                Color bg = !n.unlocked ? new Color(0.08f, 0.10f, 0.14f, 0.96f)
                         : isMax ? Color.Lerp(new Color(0.08f, 0.24f, 0.16f), n.accent, 0.22f)
                         : n.affordable ? Color.Lerp(new Color(0.18f, 0.15f, 0.07f), new Color(0.32f, 0.25f, 0.09f), affordPulse)
                         : Color.Lerp(new Color(0.10f, 0.12f, 0.18f), n.accent, 0.16f);

                Color bd = isLineage ? new Color(1f, 0.92f, 0.34f)
                         : !n.unlocked ? new Color(0.24f, 0.28f, 0.36f)
                         : isMax ? new Color(0.34f, 0.95f, 0.60f)
                         : n.affordable ? Color.Lerp(n.accent, new Color(1f, 0.90f, 0.30f), affordPulse)
                         : n.accent;

                DrawBorderedBox(r, bg, bd, (isLineage || n.affordable) ? 3 : n.isMilestone ? 2 : 1);

                // 중앙 픽셀아트 아이콘
                Texture2D icon = GetNodeIcon(n.id, n.branch);
                if (icon != null && nodeSz >= 18f)
                {
                    float pad = Mathf.Round(nodeSz * 0.18f);
                    Rect ir = new Rect(r.x + pad, r.y + pad - 2f, r.width - pad * 2f, r.height - pad * 2f);
                    GUI.color = !n.unlocked ? new Color(0.42f, 0.48f, 0.58f, 0.72f)
                              : isMax ? new Color(0.55f, 1f, 0.72f)
                              : n.affordable ? Color.Lerp(n.accent, Color.white, 0.45f)
                              : n.accent;
                    GUI.DrawTexture(ir, icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }

                // 하단 작은 레벨 점 (글자 없이 레벨 시각화!)
                if (nodeSz >= 30f && n.maxLevel > 1)
                {
                    int dots = Mathf.Min(n.maxLevel, 5);
                    float dotSz = Mathf.Max(3f, Mathf.Round(4.5f * scale));
                    float gap = dotSz + 2.5f;
                    float totalW = dots * dotSz + (dots - 1) * 2.5f;
                    float dx = r.x + (r.width - totalW) * 0.5f;
                    float dy = r.yMax - dotSz - 4f;
                    for (int d = 0; d < dots; d++)
                    {
                        Color dc = d < n.level ? new Color(0.45f, 1f, 0.65f) : new Color(1f, 1f, 1f, 0.22f);
                        DrawRect(new Rect(dx + d * gap, dy, dotSz, dotSz), dc);
                    }
                }

                if (hoveredIdx == i) hovered = n;
            }

            // 3) 중앙 시작 코어 슬롯
            float coreSz = Mathf.Round(56f * scale);
            Rect coreR = new Rect(localCenter.x - coreSz * 0.5f, localCenter.y - coreSz * 0.5f, coreSz, coreSz);
            if (viewLocalBounds.Overlaps(coreR))
            {
                DrawBorderedBox(coreR, new Color(0.18f, 0.15f, 0.08f), new Color(1f, 0.86f, 0.34f), 2);
                Texture2D cIcon = GetNodeIcon("price1", 2);
                if (cIcon != null)
                {
                    float cp = Mathf.Round(coreSz * 0.20f);
                    GUI.color = new Color(1f, 0.90f, 0.42f);
                    GUI.DrawTexture(new Rect(coreR.x + cp, coreR.y + cp, coreR.width - cp * 2f, coreR.height - cp * 2f), cIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }

            GUI.EndGroup();

            // 4) [v2.2] 마우스 호버 시에만 노드 옆에 깔끔한 플로팅 툴팁 카드 표시 (레퍼런스 사진 4번 스타일!)
            if (hovered.HasValue)
            {
                var hv = hovered.Value;
                bool isMax = hv.level >= hv.maxLevel;
                float tw = 276f;
                float th = 114f;
                float tx = mPos.x + 22f;
                float ty = mPos.y - 20f;
                if (tx + tw > panel.xMax - 12f) tx = mPos.x - tw - 22f;
                if (ty + th > panel.yMax - 12f) ty = panel.yMax - th - 12f;
                if (ty < view.y + 8f) ty = view.y + 8f;

                Rect tip = new Rect(Mathf.Round(tx), Mathf.Round(ty), tw, th);
                DrawBorderedBox(tip, new Color(0.06f, 0.075f, 0.11f, 0.97f), hv.accent, 2);

                GUI.color = Color.white;
                GUI.Label(new Rect(tip.x + 12, tip.y + 8, tw - 68, 22), hv.fullTitle, _lightBold);
                GUI.color = isMax ? new Color(0.45f, 1f, 0.65f) : new Color(0.75f, 0.80f, 0.90f);
                GUI.Label(new Rect(tip.xMax - 56, tip.y + 8, 44, 22), $"{hv.level}/{hv.maxLevel}", _lightRight);

                string detail;
                if (hv.isCore) detail = CoreDesc(hv.id);
                else
                {
                    var vn = UpgradeTreeV18.Get(hv.id);
                    detail = vn != null ? vn.desc : "";
                }
                GUI.color = new Color(0.84f, 0.88f, 0.94f);
                GUI.Label(new Rect(tip.x + 12, tip.y + 34, tw - 24, 38), detail, _inkWrap);

                // 하단 가격/상태 스트립
                Rect foot = new Rect(tip.x + 10, tip.yMax - 32, tw - 20, 24);
                Color fBg = !hv.unlocked ? new Color(0.16f, 0.18f, 0.22f)
                          : isMax ? new Color(0.10f, 0.28f, 0.18f)
                          : hv.affordable ? new Color(0.28f, 0.22f, 0.07f)
                          : new Color(0.26f, 0.12f, 0.12f);
                DrawBorderedBox(foot, fBg, new Color(1f, 1f, 1f, 0.12f), 1);
                GUI.color = !hv.unlocked ? new Color(0.70f, 0.75f, 0.82f)
                          : isMax ? new Color(0.48f, 1f, 0.68f)
                          : hv.affordable ? new Color(1f, 0.92f, 0.45f)
                          : new Color(1f, 0.56f, 0.52f);
                string fTxt = !hv.unlocked ? hv.unlockHint
                            : isMax ? "MAX"
                            : FormatCash(hv.cost);
                GUI.Label(foot, fTxt, _lightCenter);
                GUI.color = Color.white;
            }
        }

        private static Rect NodeRectOf(TreeNodeDef n, Vector2 center, float scale, float nW, float nH)
        {
            return new Rect(
                Mathf.Round(center.x + n.offset.x * scale - nW * 0.5f),
                Mathf.Round(center.y + n.offset.y * scale - nH * 0.5f), nW, nH);
        }

        private void ClampTreePan(Rect view)
        {
            // 268노드 트리는 반경 1,300px까지 뻗으므로 줌 배율에 맞춰 이동 한계를 넓힌다.
            float extent = 1720f * _treeZoom;
            float mx = Mathf.Max(view.width * 0.8f, extent);
            float my = Mathf.Max(view.height * 0.8f, extent);
            _treePan = new Vector2(Mathf.Clamp(_treePan.x, -mx, mx), Mathf.Clamp(_treePan.y, -my, my));
        }

        private void EnsureTreeMeta()
        {
            if (_treeMeta != null) return;
            _treeMeta = new List<TreeMeta>(UpgradeTreeV18.TotalNodeCount);

            Color cTool = BranchColor(0), cVeh = BranchColor(2), cSurv = BranchColor(7), cAuto = BranchColor(5);
            Color cCore = new Color(0.95f, 0.80f, 0.36f);

            void Core(string id, string shortName, string fullTitle, string hint, string parentId, Vector2 offset, int maxLv, bool milestone, Color accent)
            {
                _treeMeta.Add(new TreeMeta
                {
                    id = id, shortName = shortName, fullTitle = fullTitle, statDesc = "", desc = "",
                    unlockHint = hint, parentId = parentId, offset = offset,
                    maxLevel = maxLv, branch = -1, isMilestone = milestone, isCore = true,
                    accent = milestone ? cCore : accent
                });
            }

            // ===== [v2.2] 코어 28노드 (76px 깔끔한 직각 격자 배치!) =====
            Core("hold1",  "흡입속도 I",  "흡입 속도 I",     "시작 노드", "", new Vector2(-76, -76), 3, false, cTool);
            Core("rad1",   "흡입반경 I",  "흡입 반경 I",     "선행: 흡입속도 I", "hold1", new Vector2(-152, -76), 3, false, cTool);
            Core("broom",  "빗자루",      "2티어: 빗자루",   "선행: 속도 I & 반경 I", "rad1", new Vector2(-228, -76), 1, true, cTool);
            Core("hold2",  "흡입속도 II", "흡입 속도 II",    "선행: 빗자루", "broom", new Vector2(-304, -76), 4, false, cTool);
            Core("rad2",   "흡입반경 II", "흡입 반경 II",    "선행: 빗자루", "broom", new Vector2(-228, -152), 4, false, cTool);
            Core("fever",  "피버 엔진",   "피버 엔진",       "선행: 흡입속도 II", "hold2", new Vector2(-304, -152), 3, false, cTool);
            Core("vacuum", "진공기",      "3티어: 진공청소기","선행: 속도 II & 반경 II", "hold2", new Vector2(-380, -76), 1, true, cTool);
            Core("hold3",  "흡입속도 III","흡입 속도 III",   "선행: 진공청소기", "vacuum", new Vector2(-380, -152), 3, false, cTool);

            Core("bin1",    "수거통 I",   "수거통 용량 I",   "시작 노드", "", new Vector2(76, -76), 3, false, cVeh);
            Core("price1",  "정산단가 I", "분리수거 단가 I", "선행: 수거통 I", "bin1", new Vector2(76, -152), 3, false, cVeh);
            Core("combo",   "정산콤보",   "연속 정산 콤보",  "선행: 정산단가 I", "price1", new Vector2(152, -152), 3, false, cVeh);
            Core("cart",    "리어카",     "차량 1단계: 리어카","선행: 수거통 I", "bin1", new Vector2(152, -76), 1, true, cVeh);
            Core("bin2",    "수거통 II",  "수거통 용량 II",  "선행: 리어카", "cart", new Vector2(228, -76), 4, false, cVeh);
            Core("price2",  "정산단가 II","분리수거 단가 II","선행: 리어카 & 단가 I", "cart", new Vector2(228, -152), 4, false, cVeh);
            Core("damas",   "다마스",     "차량 2단계: 다마스","선행: 수거통 II & 단가 II", "bin2", new Vector2(304, -76), 1, true, cVeh);
            Core("bin3",    "수거통 III", "수거통 용량 III", "선행: 다마스", "damas", new Vector2(304, -152), 4, false, cVeh);
            Core("truck1t", "1톤트럭",    "차량 3단계: 1톤 트럭","선행: 다마스 & 수거통 III", "damas", new Vector2(380, -76), 1, true, cVeh);

            Core("speed1", "이동속도 I", "운동화 I",         "시작 노드", "", new Vector2(-76, 76), 3, false, cSurv);
            Core("speed2", "이동속도 II","운동화 II",        "선행: 이동속도 I", "speed1", new Vector2(-152, 76), 3, false, cSurv);
            Core("mask1",  "방독면 I",   "방독면 필터 I",    "선행: 이동속도 I", "speed1", new Vector2(-76, 152), 3, false, cSurv);
            Core("mask2",  "방독면 II",  "방독면 필터 II",   "선행: 방독면 I", "mask1", new Vector2(-152, 152), 4, false, cSurv);
            Core("mop",    "대걸레",     "T자 대걸레",       "선행: 방독면 I", "mask1", new Vector2(-228, 152), 1, true, cSurv);
            Core("scrub",  "강력 세제",  "강력 바닥 세제",   "선행: T자 대걸레", "mop", new Vector2(-304, 152), 4, false, cSurv);

            Core("fist1",  "주먹 힘 I",  "봉투 파괴력 I",    "시작 노드", "", new Vector2(76, 76), 3, false, cAuto);
            Core("fist2",  "주먹 힘 II", "봉투 파괴력 II",   "선행: 주먹 힘 I", "fist1", new Vector2(152, 76), 3, false, cAuto);
            Core("roomba", "로봇청소",   "로봇청소기 (최대 3대)","선행: 주먹 힘 I", "fist1", new Vector2(76, 152), 3, true, cAuto);
            Core("rspd1",  "로봇속도 I", "로봇 가속 I",      "선행: 로봇청소기", "roomba", new Vector2(152, 152), 3, false, cAuto);
            Core("rspd2",  "로봇속도 II","로봇 가속 II",     "선행: 로봇 2대 & 속도 I", "rspd1", new Vector2(228, 152), 3, false, cAuto);

            // ===== v1.9 정예 마이크로 노드 80개 =====
            foreach (var v in UpgradeTreeV18.Nodes)
            {
                _treeMeta.Add(new TreeMeta
                {
                    id = v.id,
                    shortName = v.name,
                    fullTitle = v.title,
                    statDesc = "",
                    desc = v.desc,
                    unlockHint = string.IsNullOrEmpty(v.prereq) ? "선행 없음"
                        : $"선행: {NodeDisplayName(v.prereq)} Lv.{v.prereqLevel}",
                    parentId = v.prereq,
                    offset = v.pos,
                    maxLevel = v.maxLevel,
                    branch = v.branch,
                    isMilestone = v.milestone,
                    isCore = false,
                    accent = BranchColor(v.branch)
                });
            }
        }

        private string CoreDesc(string id)
        {
            switch (id)
            {
                case "hold1": case "hold2": case "hold3": return $"현재 홀드 {manager.EffectiveHoldDuration:0.00}초";
                case "rad1": case "rad2": return $"현재 반경 {manager.SweepRadius:0.00}m · 줍기 사거리 {manager.PickupReach:0.00}m · 동시 {manager.AutoGrabPerTick}개";
                case "broom": return "반경·사거리 대폭 확장 · 동시 3개+ 쓸어담기 · 홀드 4% 단축";
                case "vacuum": return "광역 흡입 + 원거리 사거리 확장 · 수거통 용량 ×2!";
                case "fever": return $"Lv.{manager.LvFever}/3 · 범위·속도·수익 증폭";
                case "bin1": case "bin2": case "bin3": return $"현재 최대 {manager.BinCapacity:N0}개 휴대";
                case "price1": case "price2": return $"쓰레기 수익 배율 ×{manager.EffectiveTrashPriceMult:0.00}";
                case "combo": return $"연속 정산 +20%/스택 · 유지 {manager.MaxComboWindow:0.0}초";
                case "cart": return "정산 ×1.20 · 차량 정산 반경 4.2m 확장 (수동 [RMB])";
                case "damas": return "정산 ×1.45 · 정산 반경 6.2m + 마당 진입 시 자동 흡입 정산 해금!";
                case "truck1t": return $"정산 ×1.80 · 정산 반경 9.0m + 방 안 {manager.RemoteSettleFill01 * 100f:0}% 원격 자동수거 해금!";
                case "speed1": case "speed2": return $"이동속도 +{manager.TotalSpeedLv * 3}%";
                case "mask1": case "mask2": return $"악취 상승 -{Mathf.Min(56, manager.TotalMaskLv * 8)}% · 산소 {(manager.HasOxygenBoost ? (manager.OxygenBoostMult - 1f) * 100f : 0f):0}%";
                case "mop": return "2단계 바닥 얼룩 닦기 속도 ×2.4";
                case "scrub": return $"현재 행주질 배율 ×{manager.ScrubSpeedMult:0.00}";
                case "fist1": case "fist2": return $"봉투 타격 {manager.FistDamage:0.0} · 치명 {manager.FistCritChance * 100f:0}%";
                case "roomba": return manager.RoombaCount == 0 ? "구매 시 방 안 자동 수거 로봇 배치!" : $"현재 {manager.RoombaCount}대 · {manager.RoombaEatInterval:0.00}초/개";
                case "rspd1": case "rspd2": return $"로봇 주기 {manager.RoombaEatInterval:0.00}초 · 이동 {manager.RoombaMoveSpeed:0.0}";
                default: return "";
            }
        }

        private List<TreeNodeDef> BuildTreeNodes()
        {
            EnsureTreeMeta();
            _treeNodesBuffer.Clear();
            for (int i = 0; i < _treeMeta.Count; i++)
            {
                var m = _treeMeta[i];
                bool unlocked = manager.IsNodeUnlocked(m.id);
                int lv = manager.GetNodeLevel(m.id);
                int maxLv = manager.GetNodeMaxLevel(m.id);
                int cost = manager.GetNodeCost(m.id);
                bool afford = unlocked && lv < maxLv && manager.Money >= cost;
                _treeNodesBuffer.Add(new TreeNodeDef
                {
                    id = m.id,
                    shortName = m.shortName,
                    fullTitle = m.fullTitle,
                    statDesc = "",
                    desc = m.desc,
                    isCore = m.isCore,
                    unlockHint = m.unlockHint,
                    parentId = m.parentId,
                    offset = m.offset,
                    level = lv,
                    maxLevel = maxLv,
                    cost = cost,
                    branch = m.branch,
                    unlocked = unlocked,
                    affordable = afford,
                    isMilestone = m.isMilestone,
                    accent = m.accent
                });
            }
            return _treeNodesBuffer;
        }

        private static string NodeDisplayName(string id)
        {
            var n = UpgradeTreeV18.Get(id);
            if (n != null) return n.name;
            switch (id)
            {
                case "hold1": return "흡입속도 I"; case "rad1": return "흡입반경 I";
                case "broom": return "빗자루"; case "hold2": return "흡입속도 II";
                case "rad2": return "흡입반경 II"; case "vacuum": return "진공기";
                case "hold3": return "흡입속도 III"; case "fever": return "피버 엔진";
                case "bin1": return "수거통 I"; case "price1": return "정산단가 I";
                case "cart": return "리어카"; case "bin2": return "수거통 II";
                case "price2": return "정산단가 II"; case "damas": return "다마스";
                case "bin3": return "수거통 III"; case "combo": return "정산콤보";
                case "truck1t": return "1톤트럭";
                case "speed1": return "이동속도 I"; case "mask1": return "방독면 I";
                case "mop": return "대걸레"; case "speed2": return "이동속도 II";
                case "mask2": return "방독면 II"; case "scrub": return "강력 세제";
                case "fist1": return "주먹 힘 I"; case "roomba": return "로봇청소기";
                case "rspd1": return "로봇속도 I"; case "fist2": return "주먹 힘 II";
                case "rspd2": return "로봇속도 II";
                default: return id;
            }
        }

        // ===== 클리어 후 정산 허브 ([다시 시작] [다음 단계 ▶] [$ 강화 트리]) =====
        private void DrawPostRoundHub(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topY = Mathf.Round(Mathf.Max(22f, h * 0.032f));
            Rect hdr = new Rect(Mathf.Round(w * 0.5f - 290), topY, 580, 42);
            DrawReceiptBox(hdr);
            GUI.Label(new Rect(hdr.x + 16, hdr.y + 9, 130, 24), $"STAGE {manager.CurrentStage}", _inkBold);
            GUI.color = new Color(0.06f, 0.46f, 0.20f);
            GUI.Label(new Rect(hdr.x + 150, hdr.y + 9, 160, 24), $"현금 ${manager.Money:N0}", _lightBold);
            GUI.color = new Color(0.45f, 0.34f, 0.14f);
            GUI.Label(new Rect(hdr.x + 310, hdr.y + 9, 180, 24), $"{manager.CurrentVehicleName}", _inkRight);
            GUI.color = Color.white;

            if (_postTab != PostRoundTab.Receipt)
            {
                if (GUI.Button(new Rect(hdr.xMax - 76, hdr.y + 7, 68, 28), "영수증", _btnStyle))
                    _postTab = PostRoundTab.Receipt;
            }

            if (_postTab == PostRoundTab.Receipt) DrawReceiptSummaryCard(w, h);
            else DrawUpgradeKioskTree(w, h, true);

            int btnCount = 4;
            float barW = Mathf.Min(780f, w - 40f);
            float btnGap = 10f;
            float btnW = Mathf.Floor((barW - btnGap * (btnCount - 1)) / btnCount);
            float startX = Mathf.Round((w - (btnW * btnCount + btnGap * (btnCount - 1))) * 0.5f);
            float btnY = Mathf.Round(h - 60f);

            int bi = 0;
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), "다시 시작", false, new Color(0.28f, 0.32f, 0.38f), () => manager.ReplayRoomKeepUpgrades());
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), "★ 7종 스테이지 맵", false, new Color(0.22f, 0.48f, 0.80f), () => manager.OpenStageMindMap());
            string nextLabel = manager.CurrentStage < CampaignProgressStore.TotalStages
                ? $"ST.{manager.CurrentStage + 1} {CampaignProgressStore.GetInfo(manager.CurrentStage + 1).shortName} ▶"
                : "★ 전체 캠페인 완료!";
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), nextLabel, false, new Color(0.16f, 0.56f, 0.30f), () =>
            {
                if (manager.CurrentStage < CampaignProgressStore.TotalStages) manager.StartNextStage();
                else manager.OpenStageMindMap();
            });
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), "$ 강화 트리", _postTab == PostRoundTab.CashUpgrades, new Color(0.78f, 0.54f, 0.10f), () => _postTab = PostRoundTab.CashUpgrades);
        }

        // ===== v1.5 Play 시작 즉시 노출 · 7종 캠페인 스테이지 마인드맵 (순차 해금 + 색상 구별 + 썸네일 슬롯) =====
        private void DrawStageMindMap(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float pw = Mathf.Min(w - 28f, 1180f);
            float ph = Mathf.Min(h - 28f, 690f);
            Rect panel = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round((h - ph) * 0.5f), pw, ph);

            // 외곽 카드
            DrawBorderedBox(panel, new Color(0.095f, 0.088f, 0.076f, 0.978f), new Color(0.961f, 0.620f, 0.043f), 3);

            // 상단 헤더 바
            Rect hdr = new Rect(panel.x + 14, panel.y + 12, panel.width - 28, 54);
            DrawReceiptBox(hdr);
            GUI.color = new Color(0.56f, 0.36f, 0.03f);
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 6, 460, 26), "★ TrashCleaner3D · 7단계 캠페인 스테이지 마인드맵", _kioskHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 30, 520, 18),
                $"앞 스테이지를 클리어하면 다음 스테이지가 해금됩니다 · 진행률 {CampaignProgressStore.ClearedStageCount}/{CampaignProgressStore.TotalStages}", _inkSmall);

            // 우측 보유 현금 배지
            Rect cash = new Rect(hdr.xMax - 196, hdr.y + 9, 184, 36);
            DrawBorderedBox(cash, new Color(0.043f, 0.243f, 0.149f), new Color(0.98f, 0.845f, 0.35f), 2);
            GUI.Label(new Rect(cash.x + 10, cash.y + 4, 164, 28), $"${manager.Money:N0}", _moneyBadgeStyle);

            // 상태 색상 범례 + 테스트 토글 바
            Color colLocked    = new Color(0.392f, 0.455f, 0.545f); // #64748B
            Color colAvailable = new Color(0.961f, 0.620f, 0.043f); // #F59E0B
            Color colCleared   = new Color(0.063f, 0.725f, 0.506f); // #10B981

            float legY = hdr.yMax + 8f;
            DrawLegendChip(new Rect(panel.x + 16, legY, 152, 24), colLocked,    "■ 잠김 (미클리어)");
            DrawLegendChip(new Rect(panel.x + 176, legY, 164, 24), colAvailable, "▶ 도전 가능 (클릭)");
            DrawLegendChip(new Rect(panel.x + 348, legY, 174, 24), colCleared,   "● 클리어 (재방문 가능)");

            // 테스트용 전체 잠금해제 토글 & 진행 초기화 버튼
            Rect resetR = new Rect(panel.xMax - 118, legY, 102, 24);
            DrawBorderedBox(resetR, new Color(0.24f, 0.16f, 0.16f), new Color(0.75f, 0.35f, 0.35f), 1);
            GUI.color = new Color(1f, 0.80f, 0.80f);
            GUI.Label(resetR, "진행 초기화", _nodeSubStyle);
            GUI.color = Color.white;
            if (GUI.Button(resetR, GUIContent.none, GUIStyle.none))
            {
                CampaignProgressStore.ResetCampaignProgress();
                manager.ShowToast("캠페인 진행을 초기화했습니다 (Stage 1부터 시작)");
            }

            Rect unlockR = new Rect(resetR.x - 172, legY, 164, 24);
            bool devAll = CampaignProgressStore.DevUnlockAll;
            DrawBorderedBox(unlockR, devAll ? new Color(0.18f, 0.34f, 0.22f) : new Color(0.20f, 0.20f, 0.22f),
                devAll ? colCleared : new Color(0.55f, 0.55f, 0.60f), 1);
            GUI.color = devAll ? new Color(0.65f, 1f, 0.75f) : new Color(0.84f, 0.84f, 0.88f);
            GUI.Label(unlockR, devAll ? "테스트 전체해금: ON" : "테스트 전체해금: OFF", _nodeSubStyle);
            GUI.color = Color.white;
            if (GUI.Button(unlockR, GUIContent.none, GUIStyle.none))
            {
                CampaignProgressStore.DevUnlockAll = !CampaignProgressStore.DevUnlockAll;
                manager.ShowToast(CampaignProgressStore.DevUnlockAll ? "테스트 모드: Stage 1~7 전체 해금 ON" : "정상 순차 해금 모드로 복귀");
            }

            // 7개 노드 좌표 배치 (상단 4개: ST.1~4, 하단 3개: ST.5~7 — 연결 로드맵!)
            float mapTop = legY + 34f;
            float infoH = 62f;
            float mapBottom = panel.yMax - infoH - 18f;
            float mapH = Mathf.Max(280f, mapBottom - mapTop);
            float mapW = panel.width - 36f;

            float gapX = Mathf.Round(Mathf.Clamp(mapW * 0.025f, 14f, 30f));
            float cardW = Mathf.Round(Mathf.Min(256f, (mapW - gapX * 3f) / 4f));
            float gapY = Mathf.Round(Mathf.Clamp(mapH * 0.11f, 22f, 44f));
            float cardH = Mathf.Round(Mathf.Min(182f, (mapH - gapY) * 0.5f));

            float row1TotalW = cardW * 4f + gapX * 3f;
            float row1StartX = Mathf.Round(panel.x + (panel.width - row1TotalW) * 0.5f);
            float row1Y = Mathf.Round(mapTop + (mapH - (cardH * 2f + gapY)) * 0.5f);

            float row2TotalW = cardW * 3f + gapX * 2f;
            float row2StartX = Mathf.Round(panel.x + (panel.width - row2TotalW) * 0.5f);
            float row2Y = Mathf.Round(row1Y + cardH + gapY);

            Rect[] rects = new Rect[7];
            for (int i = 0; i < 4; i++)
                rects[i] = new Rect(row1StartX + i * (cardW + gapX), row1Y, cardW, cardH);
            for (int i = 0; i < 3; i++)
                rects[4 + i] = new Rect(row2StartX + i * (cardW + gapX), row2Y, cardW, cardH);

            // 로드맵 연결선 (ST.1→2→3→4 ──┐ └── ST.5→6→7)
            for (int i = 0; i < 6; i++)
            {
                bool nextUnlocked = CampaignProgressStore.IsStageUnlocked(i + 2);
                bool curCleared = CampaignProgressStore.IsStageCleared(i + 1);
                Color lineCol = curCleared ? colCleared : nextUnlocked ? colAvailable : new Color(0.39f, 0.45f, 0.55f, 0.45f);
                float lw = curCleared || nextUnlocked ? 4f : 2.5f;

                if (i == 3)
                {
                    // ST.4 하단 중앙 → 중간 꺾임선 → ST.5 상단 중앙
                    Vector2 a = new Vector2(rects[3].center.x, rects[3].yMax);
                    Vector2 b = new Vector2(rects[4].center.x, rects[4].y);
                    float midY = (a.y + b.y) * 0.5f;
                    DrawLine(a, new Vector2(a.x, midY), lineCol, lw);
                    DrawLine(new Vector2(a.x, midY), new Vector2(b.x, midY), lineCol, lw);
                    DrawLine(new Vector2(b.x, midY), b, lineCol, lw);
                }
                else
                {
                    Vector2 a = new Vector2(rects[i].xMax, rects[i].center.y);
                    Vector2 b = new Vector2(rects[i + 1].x, rects[i + 1].center.y);
                    DrawLine(a, b, lineCol, lw);
                }
            }

            // 7개 스테이지 카드 렌더
            int hoveredStage = -1;
            Vector2 mPos = Event.current.mousePosition;
            float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * 4.5f);

            for (int i = 0; i < 7; i++)
            {
                int stageNum = i + 1;
                var info = CampaignProgressStore.Stages[i];
                var state = CampaignProgressStore.GetStageState(stageNum);
                Rect r = rects[i];
                bool isHover = r.Contains(mPos);
                if (isHover) hoveredStage = stageNum;

                Color accent = state == StageUnlockState.Cleared ? colCleared
                             : state == StageUnlockState.Available ? colAvailable
                             : colLocked;

                Color cardBg = state == StageUnlockState.Locked
                    ? new Color(0.16f, 0.18f, 0.21f, 0.96f)
                    : state == StageUnlockState.Cleared
                        ? new Color(0.11f, 0.21f, 0.17f, 0.98f)
                        : new Color(0.22f, 0.17f, 0.09f, 0.98f);

                // 진행 가능 노드는 은은한 글로우 외곽
                if (state == StageUnlockState.Available)
                {
                    DrawRect(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(colAvailable.r, colAvailable.g, colAvailable.b, 0.28f * pulse));
                }
                else if (isHover && state != StageUnlockState.Locked)
                {
                    DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(accent.r, accent.g, accent.b, 0.30f));
                }

                DrawBorderedBox(r, cardBg, accent, state == StageUnlockState.Available ? 3 : 2);

                // 상단 썸네일 영역 (커스텀 PNG 썸네일이 있으면 표시, 없으면 방 분위기 컬러 헤더!)
                float thumbH = Mathf.Round(r.height * 0.38f);
                Rect thumbR = new Rect(r.x + 3, r.y + 3, r.width - 6, thumbH);
                Texture2D thumbTex = (_stageThumbs != null && i < _stageThumbs.Length) ? _stageThumbs[i] : null;
                if (thumbTex != null)
                {
                    Color prev = GUI.color;
                    GUI.color = state == StageUnlockState.Locked ? new Color(0.45f, 0.48f, 0.54f, 0.85f) : Color.white;
                    GUI.DrawTexture(thumbR, thumbTex, ScaleMode.ScaleAndCrop);
                    GUI.color = prev;
                }
                else
                {
                    Color bannerCol = state == StageUnlockState.Locked
                        ? new Color(0.24f, 0.27f, 0.32f)
                        : Color.Lerp(info.themeColor, new Color(0.16f, 0.14f, 0.12f), 0.32f);
                    DrawRect(thumbR, bannerCol);
                    DrawRect(new Rect(thumbR.x, thumbR.yMax - 3, thumbR.width, 3), accent);
                }

                // 썸네일 위 좌상단 STAGE 배지 + 우상단 상태 배지
                Rect stBadge = new Rect(thumbR.x + 6, thumbR.y + 5, 68, 20);
                DrawBorderedBox(stBadge, new Color(0.10f, 0.10f, 0.12f, 0.88f), accent, 1);
                GUI.color = Color.white;
                GUI.Label(stBadge, $"STAGE {stageNum}", _nodeSubStyle);

                string statusTag = state == StageUnlockState.Cleared ? $"● {new string('★', Mathf.Max(1, CampaignProgressStore.GetStageStars(stageNum)))}"
                                 : state == StageUnlockState.Available ? "▶ 도전 가능"
                                 : "■ 잠김";
                Rect statBadge = new Rect(thumbR.xMax - 86, thumbR.y + 5, 80, 20);
                DrawBorderedBox(statBadge, accent, new Color(0.10f, 0.10f, 0.10f, 0.6f), 1);
                GUI.color = ReadableOn(accent);
                GUI.Label(statBadge, statusTag, _nodeSubStyle);
                GUI.color = Color.white;

                // 썸네일 하단 태그 (3층 복층 / 튜토리얼 / 최강 악취 등)
                string subFeat = stageNum == 1 ? "튜토리얼 · 기본 원룸"
                               : stageNum == 6 ? "최강 악취 · 방독면 필수"
                               : stageNum == 7 ? "★ FINAL · 3층 복층!"
                               : info.cctvLabel;
                GUI.color = new Color(0.96f, 0.96f, 0.94f, state == StageUnlockState.Locked ? 0.55f : 0.95f);
                GUI.Label(new Rect(thumbR.x + 8, thumbR.yMax - 22, thumbR.width - 16, 18), subFeat, _lightSmall);
                GUI.color = Color.white;

                // 카드 본문: 방 이름 + 규격/쓰레기 수 (잠긴 카드도 높은 대비로 읽히게!)
                float bodyY = thumbR.yMax + 6f;
                GUI.color = state == StageUnlockState.Locked ? new Color(0.84f, 0.87f, 0.92f) : Color.white;
                GUI.Label(new Rect(r.x + 10, bodyY, r.width - 20, 22), info.title, _lightBold);
                GUI.color = state == StageUnlockState.Locked ? new Color(0.72f, 0.76f, 0.82f) : new Color(0.86f, 0.85f, 0.80f);
                GUI.Label(new Rect(r.x + 10, bodyY + 22, r.width - 20, 18), $"{info.sizeLabel}", _lightSmall);
                GUI.Label(new Rect(r.x + 10, bodyY + 40, r.width - 20, 18), $"{info.trashLabel}", _lightSmall);
                GUI.color = Color.white;

                // 하단 클릭 액션 버튼 스트립
                Rect actR = new Rect(r.x + 8, r.yMax - 30, r.width - 16, 23);
                Color actBg = state == StageUnlockState.Locked ? new Color(0.24f, 0.28f, 0.35f) : accent;
                DrawBorderedBox(actR, actBg, new Color(0.12f, 0.12f, 0.12f, 0.65f), 1);
                GUI.color = state == StageUnlockState.Locked ? new Color(0.82f, 0.86f, 0.92f) : ReadableOn(actBg);
                string actLabel = state == StageUnlockState.Locked ? $"ST.{stageNum - 1} 클리어 후 해금"
                                : state == StageUnlockState.Cleared ? "● 클릭하여 재입장"
                                : "▶ 클릭하여 청소 시작";
                GUI.Label(actR, actLabel, _nodeSubStyle);
                GUI.color = Color.white;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    manager.EnterStageFromMindMap(stageNum);
                }
            }

            // 하단 의뢰인 메모 상세 영수증 바
            Rect infoR = new Rect(panel.x + 14, panel.yMax - infoH - 10, panel.width - 28, infoH);
            DrawReceiptBox(infoR);
            int focusStage = hoveredStage > 0 ? hoveredStage : manager.CurrentStage;
            var fInfo = CampaignProgressStore.GetInfo(focusStage);
            var fState = CampaignProgressStore.GetStageState(focusStage);
            string fBadge = fState == StageUnlockState.Cleared ? "[● 클리어 완료]"
                          : fState == StageUnlockState.Available ? "[▶ 입장 가능 · 클릭 시작]"
                          : $"[■ 잠김 · Stage {focusStage - 1} 클리어 필요]";
            GUI.Label(new Rect(infoR.x + 14, infoR.y + 6, infoR.width - 28, 22),
                $"STAGE {fInfo.stageNumber} · {fInfo.title}   {fBadge}   ({fInfo.sizeLabel} · {fInfo.cctvLabel})", _inkBold);
            GUI.Label(new Rect(infoR.x + 14, infoR.y + 30, infoR.width - 28, 24),
                $"의뢰인 메모: \"{fInfo.clientMemo}\"", _inkSmall);

            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect toastR = new Rect(Mathf.Round(w * 0.5f - 220), panel.y - 34, 440, 28);
                if (toastR.y < 8f) toastR.y = hdr.yMax + 6f;
                DrawPaperCard(toastR, colAvailable, 2);
                GUI.color = new Color(0.46f, 0.28f, 0.02f);
                GUI.Label(toastR, manager.ToastMessage, _lightCenter);
                GUI.color = Color.white;
            }
        }

        private void DrawLegendChip(Rect r, Color accent, string label)
        {
            DrawBorderedBox(r, new Color(0.16f, 0.15f, 0.14f, 0.94f), accent, 1);
            DrawRect(new Rect(r.x + 4, r.y + 4, 8, r.height - 8), accent);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 16, r.y, r.width - 18, r.height), label, _lightSmall);
            GUI.color = Color.white;
        }

        private void DrawReceiptSummaryCard(float w, float h)
        {
            float rw = 480f, rh = 395f;
            Rect r = new Rect(Mathf.Round((w - rw) * 0.5f), Mathf.Round((h - rh) * 0.5f - 10f), rw, rh);
            DrawReceiptBox(r);

            string title = manager.WasEarlyCheckout
                ? $"[STAGE {manager.CurrentStage} 80% 조기 정산 영수증]"
                : $"[STAGE {manager.CurrentStage} 청소 완료 영수증]";
            GUI.color = manager.WasCaughtStealing ? new Color(0.76f, 0.14f, 0.14f) : new Color(0.08f, 0.46f, 0.20f);
            GUI.Label(new Rect(r.x + 16, r.y + 16, rw - 32, 28), title, new GUIStyle(_lightCenter) { fontSize = 18 });
            GUI.color = Color.white;

            string stars = new string('★', Mathf.Clamp(manager.LastStarRating, 1, 5));
            GUI.color = manager.LastStarRating >= 4 ? new Color(0.86f, 0.54f, 0.04f) : manager.LastStarRating == 1 ? new Color(0.76f, 0.14f, 0.14f) : new Color(0.55f, 0.45f, 0.15f);
            GUI.Label(new Rect(r.x + 16, r.y + 44, rw - 32, 22), $"의뢰인 평점  {stars} ({manager.LastStarRating}/5)", _lightCenter);
            GUI.color = Color.white;

            GUI.Label(new Rect(r.x + 16, r.y + 68, rw - 32, 18), "--------------------------------------------------", _inkCenter);

            float y = r.y + 92f;
            DrawReceiptRow(r, y, "청소 진행률", $"{manager.OverallCleanPercent:0.0}%"); y += 26;
            DrawReceiptRow(r, y, "현재 장비", manager.CurrentToolName); y += 26;
            DrawReceiptRow(r, y, "수거 차량", manager.CurrentVehicleName); y += 26;
            DrawReceiptRow(r, y, "의뢰인 분실물", manager.FoundClientLostItem ? "● 선반 보관 완료" : manager.StoleDesignatedItem ? "! 분실물 슈킹됨" : "미보관"); y += 26;
            DrawReceiptRow(r, y, "보물 슈킹 / 발각", manager.WasCaughtStealing ? $"발각! (-${manager.LastConfiscatedValue:N0} 벌금)" : manager.StolenCount > 0 ? $"성공 ({manager.StolenCount}건 · 미발각)" : "정직 청소 (0건)"); y += 26;
            DrawReceiptRow(r, y, "완청·정직 보너스", $"+${manager.LastClearBonus:N0}"); y += 26;

            GUI.Label(new Rect(r.x + 16, y + 2, rw - 32, 18), "--------------------------------------------------", _inkCenter);
            GUI.Label(new Rect(r.x + 22, y + 22, rw - 44, 42), manager.LastReviewComment, _inkWrap);

            Rect stamp = new Rect(r.x + 28, r.yMax - 50, rw - 56, 32);
            Color sCol = manager.WasCaughtStealing ? new Color(0.76f, 0.16f, 0.16f) : new Color(0.18f, 0.58f, 0.30f);
            DrawBorderedBox(stamp, manager.WasCaughtStealing ? new Color(0.98f, 0.90f, 0.90f) : new Color(0.92f, 0.96f, 0.92f), sCol, 2);
            GUI.color = sCol;
            GUI.Label(stamp, manager.WasCaughtStealing ? $"슈킹 발각 배상금 -${manager.LastConfiscatedValue:N0} 차감!" : "수익 · 모든 장비 · 강화 100% 유지", _lightCenter);
            GUI.color = Color.white;
        }

        private void DrawTreasureDecisionModal(float w, float h)
        {
            var tr = manager.ActiveTreasure;
            if (tr == null) return;

            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float t = manager.TreasurePopupOpenAge;
            float bounce = t < 0.22f
                ? Mathf.Lerp(0.65f, 1.05f, t / 0.22f)
                : t < 0.34f ? Mathf.Lerp(1.05f, 1.0f, (t - 0.22f) / 0.12f) : 1f;

            bool isLost = tr.isMentionedByClient;
            Color theme = isLost ? new Color(0.24f, 0.88f, 1.0f)
                        : tr.rarity == TreasureRarity.Rare ? new Color(1.0f, 0.78f, 0.18f)
                        : new Color(0.98f, 0.84f, 0.34f);

            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);

            // 1) 뒤쪽 회전 방사형 빛줄기(Sunburst Rays)
            int rayCount = 12;
            float rayLen = Mathf.Min(w, h) * 0.44f * bounce;
            for (int i = 0; i < rayCount; i++)
            {
                float ang = (i / (float)rayCount) * Mathf.PI * 2f + Time.unscaledTime * 0.55f;
                Vector2 end = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rayLen;
                float a = 0.16f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.5f + i);
                DrawLine(center, end, new Color(theme.r, theme.g, theme.b, a), 14f);
            }

            // 2) 주변 반짝이는 별빛 파티클 16개
            for (int i = 0; i < 16; i++)
            {
                float ang = i * 2.39996f + Time.unscaledTime * (0.35f + (i % 3) * 0.12f);
                float rad = (190f + (i % 4) * 42f) * bounce;
                float sx = center.x + Mathf.Cos(ang) * rad;
                float sy = center.y + Mathf.Sin(ang) * rad * 0.72f;
                float sz = 5f + 5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4.2f + i * 1.3f));
                Color sc = (i % 2 == 0) ? new Color(1f, 0.96f, 0.65f, 0.85f) : new Color(theme.r, theme.g, theme.b, 0.80f);
                DrawRect(new Rect(sx - sz * 0.5f, sy - sz * 0.5f, sz, sz), sc);
            }

            // 3) 대형 득템 팝업 카드
            float cw = Mathf.Round(560f * bounce);
            float ch = Mathf.Round(390f * bounce);
            Rect card = new Rect(Mathf.Round(center.x - cw * 0.5f), Mathf.Round(center.y - ch * 0.5f), cw, ch);

            // 외곽 글로우 2중 프레임
            DrawBorderedBox(new Rect(card.x - 6, card.y - 6, card.width + 12, card.height + 12),
                new Color(theme.r, theme.g, theme.b, 0.22f), new Color(theme.r, theme.g, theme.b, 0.65f), 2);
            DrawBorderedBox(card, new Color(0.10f, 0.12f, 0.17f, 0.985f), theme, 4);

            // 상단 등급 배지 리본
            Rect ribbon = new Rect(card.x + 28, card.y - 18, card.width - 56, 36);
            DrawBorderedBox(ribbon, theme, new Color(0.12f, 0.10f, 0.06f), 2);
            GUI.color = ReadableOn(theme);
            string badgeTxt = isLost ? "★ 의뢰인 지정 분실물 발견! ★"
                            : tr.rarity == TreasureRarity.Rare ? "★ S급 희귀 보물 득템! ★"
                            : "★ 숨겨진 귀중품 발견! ★";
            GUI.Label(ribbon, badgeTxt, _lightBigCenter);
            GUI.color = Color.white;

            // 중앙 아이템 쇼케이스 영역
            Rect showcase = new Rect(card.x + 26, card.y + 34, card.width - 52, 168);
            DrawBorderedBox(showcase, new Color(0.14f, 0.17f, 0.24f, 0.96f), new Color(theme.r, theme.g, theme.b, 0.55f), 2);

            // 아이템 이름
            GUI.color = Color.white;
            GUI.Label(new Rect(showcase.x + 14, showcase.y + 16, showcase.width - 28, 34), tr.itemName,
                new GUIStyle(_lightBigCenter) { fontSize = 24 });

            // 예상 감정 시세
            int returnTip = isLost ? Mathf.Max(45, Mathf.RoundToInt(tr.value * 0.55f)) : Mathf.Max(20, Mathf.RoundToInt(tr.value * 0.25f));
            int stealCash = Mathf.Max(50, Mathf.RoundToInt(tr.value * 1.35f));
            GUI.color = new Color(1.0f, 0.90f, 0.42f);
            GUI.Label(new Rect(showcase.x + 14, showcase.y + 58, showcase.width - 28, 30),
                $"감정 시세  {FormatCash(tr.value)}", _lightBigCenter);

            // 설명 / CCTV 경고
            GUI.color = isLost ? new Color(0.45f, 0.94f, 1.0f)
                      : manager.ActiveTreasureWatchedByCctv ? new Color(1.0f, 0.42f, 0.36f)
                      : new Color(0.84f, 0.88f, 0.94f);
            string note = isLost
                ? "의뢰인이 애타게 찾는 분실물! 반환 시 5성 평점 확정 · 슈킹 시 발각 100%!"
                : manager.ActiveTreasureWatchedByCctv
                    ? "! 주의: 현재 CCTV 감시 구역! 슈킹 시 발각 위험 대폭 상승!"
                    : "쓰레기 아래 숨겨져 있던 귀중품입니다. 정직하게 돌려줄까요, 몰래 챙길까요?";
            GUI.Label(new Rect(showcase.x + 18, showcase.y + 100, showcase.width - 36, 48), note, _lightCenter);
            GUI.color = Color.white;

            // 하단 대형 2버튼 ([Q] 반환 vs [E] 슈킹)
            bool locked = manager.TreasureDecisionLockTimer > 0f;
            float btnY = showcase.yMax + 20f;
            float btnW = (card.width - 66f) * 0.5f;
            float btnH = 112f;

            // 왼쪽: [Q] 의뢰인에게 반환 (돈 대신 신뢰도 + 위험 감소 + 감사 버프!)
            Rect qBtn = new Rect(card.x + 26, btnY, btnW, btnH);
            Color qBg = new Color(0.10f, 0.36f, 0.26f, 0.98f);
            Color qBd = new Color(0.32f, 0.94f, 0.64f);
            DrawBorderedBox(qBtn, qBg, qBd, 3);
            GUI.color = new Color(0.68f, 1.0f, 0.82f);
            GUI.Label(new Rect(qBtn.x + 8, qBtn.y + 14, qBtn.width - 16, 28), "[Q] 의뢰인에게 반환", _lightBigCenter);
            GUI.color = Color.white;
            GUI.Label(new Rect(qBtn.x + 8, qBtn.y + 48, qBtn.width - 16, 24), isLost ? "신뢰도 +2 · 흡입 +22% (35s)" : "신뢰도 +1 · 흡입 +22% (20s)", _lightCenter);
            GUI.color = new Color(0.76f, 0.96f, 0.86f);
            GUI.Label(new Rect(qBtn.x + 8, qBtn.y + 74, qBtn.width - 16, 22), isLost ? "발각 위험 -25% · 5성 확보!" : "발각 위험 -15%", _nodeSubStyle);
            GUI.color = Color.white;
            if (!locked && GUI.Button(qBtn, GUIContent.none, GUIStyle.none)) manager.ResolveActiveTreasure(false);

            // 오른쪽: [E] 몰래 슈킹
            Rect eBtn = new Rect(qBtn.xMax + 14, btnY, btnW, btnH);
            Color eBg = isLost ? new Color(0.46f, 0.12f, 0.12f, 0.98f) : new Color(0.42f, 0.28f, 0.08f, 0.98f);
            Color eBd = isLost ? new Color(1.0f, 0.34f, 0.30f) : new Color(1.0f, 0.78f, 0.22f);
            DrawBorderedBox(eBtn, eBg, eBd, 3);
            GUI.color = isLost ? new Color(1.0f, 0.68f, 0.64f) : new Color(1.0f, 0.92f, 0.56f);
            GUI.Label(new Rect(eBtn.x + 8, eBtn.y + 14, eBtn.width - 16, 28), "[E] 몰래 슈킹하기", _lightBigCenter);
            GUI.color = Color.white;
            GUI.Label(new Rect(eBtn.x + 8, eBtn.y + 48, eBtn.width - 16, 24), $"즉시 현금 +{FormatCash(stealCash)}", _lightCenter);
            GUI.color = new Color(1.0f, 0.78f, 0.72f);
            float addSusp = isLost ? 100f : tr.baseSuspicionGain * (manager.ActiveTreasureWatchedByCctv ? 1.85f : 1f);
            GUI.Label(new Rect(eBtn.x + 8, eBtn.y + 74, eBtn.width - 16, 22), isLost ? "주의! 발각 위험 100%" : $"발각 위험 +{addSusp:0}%", _nodeSubStyle);
            GUI.color = Color.white;
            if (!locked && GUI.Button(eBtn, GUIContent.none, GUIStyle.none)) manager.ResolveActiveTreasure(true);
        }

        private void DrawSettingsMenu(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);
            Rect r = new Rect(Mathf.Round((w - 390) * 0.5f), Mathf.Round((h - 310) * 0.5f), 390, 310);
            DrawReceiptBox(r);

            GUI.Label(new Rect(r.x + 16, r.y + 14, 358, 26), "환경 설정 [ESC]", _inkCenter);

            GUI.Label(new Rect(r.x + 22, r.y + 50, 220, 22), $"마우스 감도  {manager.MouseSensitivity:0.00}", _inkBold);
            float ns = GUI.HorizontalSlider(new Rect(r.x + 22, r.y + 76, 346, 18), manager.MouseSensitivity, 0.03f, 0.45f);
            if (!Mathf.Approximately(ns, manager.MouseSensitivity)) manager.SetMouseSensitivity(ns);

            GUI.Label(new Rect(r.x + 22, r.y + 106, 220, 22), $"전체 볼륨  {manager.MasterVolume * 100f:0}%", _inkBold);
            float nv = GUI.HorizontalSlider(new Rect(r.x + 22, r.y + 132, 346, 18), manager.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(nv, manager.MasterVolume)) manager.SetMasterVolume(nv);

            GUI.Label(new Rect(r.x + 22, r.y + 168, 346, 20), "캠페인 스테이지 이동", _inkBold);
            if (GUI.Button(new Rect(r.x + 22, r.y + 194, 346, 36), "★ 7종 스테이지 마인드맵 열기 [M]", _btnStyle))
                manager.OpenStageMindMap();

            if (GUI.Button(new Rect(r.x + 115, r.y + 252, 160, 38), "계속하기 [ESC]", _btnStyle))
                manager.ToggleSettingsMenu();
        }

        private static string FormatCash(long amount)
        {
            long abs = amount < 0 ? -amount : amount;
            string sign = amount < 0 ? "-" : "";
            if (abs >= 1000000000L) return $"{sign}${abs / 1000000000f:0.00}B";
            if (abs >= 1000000L) return $"{sign}${abs / 1000000f:0.00}M";
            if (abs >= 100000L) return $"{sign}${abs / 1000f:0.#}K";
            return $"{sign}${abs:N0}";
        }

        private static Color ReadableOn(Color bg)
        {
            float lum = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            return lum > 0.60f ? new Color(0.22f, 0.17f, 0.10f) : Color.white;
        }

        private void DrawHubButton(Rect r, string label, bool active, Color accent, System.Action onClick)
        {
            if (active)
            {
                DrawBorderedBox(r, accent, new Color(0.24f, 0.19f, 0.13f), 3);
                GUI.color = ReadableOn(accent);
                GUI.Label(r, label, _lightCenter);
                GUI.color = Color.white;
            }
            else
            {
                DrawPaperCard(r, new Color(0.68f, 0.61f, 0.50f), 2);
                GUI.color = new Color(0.28f, 0.23f, 0.17f);
                GUI.Label(r, label, _lightCenter);
                GUI.color = Color.white;
            }
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) onClick?.Invoke();
        }

        private void DrawReceiptRow(Rect r, float y, string left, string right)
        {
            GUI.Label(new Rect(r.x + 24, y, r.width * 0.52f, 24), left, _inkLeft);
            GUI.Label(new Rect(r.x + r.width * 0.42f, y, r.width * 0.58f - 24f, 24), right, _inkRight);
        }

        private void DrawReceiptBox(Rect r)
        {
            DrawRect(new Rect(r.x + 2, r.y + 4, r.width, r.height), new Color(0, 0, 0, 0.16f));
            DrawBorderedBox(r, new Color(0.972f, 0.952f, 0.905f, 0.97f), new Color(0.335f, 0.285f, 0.220f), 2);
            DrawRect(new Rect(r.x + 2, r.y + 2, r.width - 4, 2), new Color(0.855f, 0.815f, 0.735f));
        }

        private void DrawPaperCard(Rect r, Color border, int bw = 1)
        {
            DrawRect(new Rect(r.x + 2, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.18f));
            DrawBorderedBox(r, new Color(0.966f, 0.947f, 0.902f, 0.955f), border, bw);
        }

        private void DrawPostItBox(Rect r)
        {
            DrawRect(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.20f));
            DrawBorderedBox(r, new Color(0.995f, 0.912f, 0.545f, 0.98f), new Color(0.775f, 0.635f, 0.215f), 1);
        }

        private void DrawBorderedBox(Rect r, Color fill, Color border, int bw)
        {
            DrawRect(r, border);
            DrawRect(new Rect(r.x + bw, r.y + bw, r.width - bw * 2, r.height - bw * 2), fill);
        }

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _whiteTex);
            GUI.color = prev;
        }

        private void DrawLine(Vector2 a, Vector2 b, Color c, float width)
        {
            Matrix4x4 prevMtx = GUI.matrix;
            Color prevCol = GUI.color;
            GUI.color = c;
            float angle = Vector3.Angle(b - a, Vector2.right);
            if (b.y < a.y) angle = -angle;
            float len = Vector2.Distance(a, b);
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), _whiteTex);
            GUI.matrix = prevMtx;
            GUI.color = prevCol;
        }
    }
}
