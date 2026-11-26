using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// TrashCleaner3D v0.9 UI:
    /// - Malgun Gothic 동적 한글 폰트 + RectOffset(0) + TextClipping.Overflow로 글씨 깨짐/잘림 0%!
    /// - 100% 봉투 바 원복 + 1초 HOLD 진동 차징 바 + 동종 압축 칩
    /// - 크고 화려한 보물 발견 팝업 (회전 황금 광선 · 쉬머 배너 · 팝인 애니메이션 · [Q]전달 / [E]슈킹)
    /// - 인게임 [Tab] 현금($) 전용 19노드 스킬트리
    /// - 클리어 후 5버튼 허브: [다시 시작] [다음 단계] [♦ 암시장] [★ 특전] [$ 강화]
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private enum PostRoundTab { Receipt, BlackMarket, TrustPerks, CashUpgrades }
        private PostRoundTab _postTab = PostRoundTab.Receipt;
        private GamePhase _lastObservedPhase = GamePhase.Phase1_TrashRemoval;

        // v1.3 (요청 #8): 큰 봉투 체력바 표시값 — 새 값으로 '차오르지' 않고 현재 체력에서 깎여 내려간다.
        private float _pileHpDisplay = 1f;
        private float _pileHpTrail = 1f;

        private bool _stylesReady;
        private Camera _cam;
        private Font _krFont;
        private Texture2D _whiteTex;
        private Texture2D _dimTex;

        private GUIStyle _inkLeft;
        private GUIStyle _inkBold;
        private GUIStyle _inkCenter;
        private GUIStyle _inkRight;
        private GUIStyle _inkSmall;
        private GUIStyle _inkWrap;

        private GUIStyle _lightLeft;
        private GUIStyle _lightBold;
        private GUIStyle _lightCenter;
        private GUIStyle _lightRight;
        private GUIStyle _lightSmall;
        private GUIStyle _lightBigCenter;
        private GUIStyle _lightTitleCenter;

        private GUIStyle _worldPopStyle;
        private GUIStyle _worldPopMoneyStyle;
        private GUIStyle _moneyBadgeStyle;
        private GUIStyle _moneyPendingStyle;
        private GUIStyle _kioskHeaderStyle;
        private GUIStyle _kioskIconStyle;
        private GUIStyle _nodeIconStyle;
        private GUIStyle _nodeSubStyle;
        private GUIStyle _btnStyle;

        private struct SkillNodeDef
        {
            public string id, icon, title, shortStat, costTag, parentId;
            public Vector2 offset;
            public int level, maxLevel;
            public bool unlocked, affordable;
            public Color accent;
        }

        private void Awake()
        {
            _whiteTex = MakeTex(Color.white);
            _dimTex = MakeTex(new Color(0.075f, 0.065f, 0.055f, 0.86f));
            // WebGL에는 OS 폰트가 없으므로 번들된 Resources/Fonts/KRFont 우선 로드, 데스크톱은 OS 한글 폰트 사용
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
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
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

            Color ink = new Color(0.185f, 0.150f, 0.115f);   // 종이 위 잉크(완전 검정 대신 따뜻한 먹색)
            Color white = Color.white;

            _inkLeft = MakeLabelStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            _inkBold = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            _inkCenter = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            _inkRight = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleRight, ink);
            _inkSmall = MakeLabelStyle(12, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            _inkWrap = MakeLabelStyle(13, FontStyle.Normal, TextAnchor.UpperLeft, ink, true);

            _lightLeft = MakeLabelStyle(13, FontStyle.Normal, TextAnchor.MiddleLeft, white);
            _lightBold = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _lightCenter = MakeLabelStyle(14, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _lightRight = MakeLabelStyle(13, FontStyle.Bold, TextAnchor.MiddleRight, white);
            _lightSmall = MakeLabelStyle(12, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _lightBigCenter = MakeLabelStyle(20, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _lightTitleCenter = MakeLabelStyle(26, FontStyle.Bold, TextAnchor.MiddleCenter, white);

            _worldPopStyle = MakeLabelStyle(17, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            // v1.3 (요청 #3): 돈 팝업은 더 크고 굵게 — 어디서 돈이 들어왔는지 바로 읽힌다.
            _worldPopMoneyStyle = MakeLabelStyle(23, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _moneyBadgeStyle = MakeLabelStyle(27, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.94f, 0.62f));
            _moneyPendingStyle = MakeLabelStyle(15, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.55f, 1f, 0.72f));
            _kioskHeaderStyle = MakeLabelStyle(17, FontStyle.Bold, TextAnchor.MiddleLeft, white);
            _kioskIconStyle = MakeLabelStyle(18, FontStyle.Bold, TextAnchor.MiddleCenter, white);
            _nodeIconStyle = MakeLabelStyle(19, FontStyle.Bold, TextAnchor.MiddleCenter, white);
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

            // 방 클리어 화면에 새로 진입할 때 항상 영수증 탭부터 시작
            if (manager.Phase != _lastObservedPhase)
            {
                if (manager.Phase == GamePhase.RoomCompleted) _postTab = PostRoundTab.Receipt;
                _lastObservedPhase = manager.Phase;
            }

            float w = Screen.width;
            float h = Screen.height;

            DrawScreenOverlays(w, h);
            DrawFloatingWorldPopups(w, h);

            if (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                DrawHud(w, h);
                DrawCrosshairAndPrompt(w, h);
            }
            else if (manager.Phase == GamePhase.TreasureDecision) DrawGrandTreasurePopup(w, h);
            else if (manager.Phase == GamePhase.MindMapUpgrade) DrawUpgradeKiosk(w, h);
            else if (manager.Phase == GamePhase.SettingsMenu) DrawSettingsMenu(w, h);
            else if (manager.Phase == GamePhase.RoomCompleted) DrawPostRoundHub(w, h);
        }

        private void DrawScreenOverlays(float w, float h)
        {
            // v1.3 (요청 #7): 악취가 오를수록 시야가 뿌옇게 가려지는 안개 오버레이.
            //   (작업 자체를 막지 않으므로, 대가를 '화면'으로 체감시킨다)
            float blur = manager.StenchBlur01;
            if (blur > 0.01f)
            {
                float pulse = 0.92f + 0.08f * Mathf.Sin(Time.time * 1.7f);
                Color veil = manager.StenchStage >= 4
                    ? new Color(0.62f, 0.52f, 0.34f, 0.40f * blur * pulse)
                    : manager.StenchStage >= 3
                        ? new Color(0.64f, 0.58f, 0.40f, 0.30f * blur * pulse)
                        : new Color(0.66f, 0.62f, 0.46f, 0.18f * blur * pulse);
                DrawRect(new Rect(0, 0, w, h), veil);

                // 화면 가장자리를 안쪽으로 덮는 2중 테두리 → '눈이 따갑다' 느낌
                if (blur > 0.35f)
                {
                    Color edge = new Color(0.42f, 0.38f, 0.24f, 0.30f * ((blur - 0.35f) / 0.65f));
                    DrawBorderGlow(w, h, edge, Mathf.Lerp(66f, 150f, (blur - 0.35f) / 0.65f));
                }
            }

            if (!manager.IsOutsideVentilating && manager.StenchPercent >= 25f)
            {
                float t = Mathf.Clamp01((manager.StenchPercent - 25f) / 75f);
                float alpha = Mathf.Lerp(0.06f, 0.30f, t);
                Color edge = manager.StenchStage >= 4 ? new Color(0.45f, 0.14f, 0.40f, alpha) : new Color(0.32f, 0.44f, 0.12f, alpha);
                DrawBorderGlow(w, h, edge, Mathf.Lerp(16f, 44f, t));
            }
            if (manager.IsAdrenalineZone)
            {
                float pulse = 0.10f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 6.5f));
                DrawBorderGlow(w, h, new Color(1.0f, 0.36f, 0.14f, pulse), 22f);
            }
            if (manager.FreshAirFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(0.65f, 0.92f, 1f, manager.FreshAirFlash01 * 0.12f));
            if (manager.TreasureFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(1f, 0.88f, 0.3f, manager.TreasureFlash01 * 0.24f));
            if (manager.StealAlertFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(0.95f, 0.16f, 0.14f, manager.StealAlertFlash01 * 0.22f));
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

                // 돈이 걸린 팝업은 한 단계 더 큰 글씨로 (요청 #3)
                bool moneyPop = ft.text.IndexOf('$') >= 0;
                GUIStyle st = moneyPop ? _worldPopMoneyStyle : _worldPopStyle;
                float bw = moneyPop ? 380f : 300f;
                float bh = moneyPop ? 34f : 28f;

                // 밝은 종이 톤 방에서도 읽히도록 먹색 4방향 외곽선 + 밝은 본문
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
            float topY = Mathf.Round(Mathf.Max(38f, h * 0.062f));
            float botSafe = Mathf.Round(Mathf.Max(56f, h * 0.088f));
            float sideX = Mathf.Round(Mathf.Max(34f, w * 0.048f));

            // ===== 1. 좌상단: 보유 현금 배지(요청 #3) + 배달 영수증 카드 =====
            // 돈은 이 게임의 유일한 재화인데 예전엔 영수증 13px 글씨라 묻혔다.
            // 이제 진녹색·금태두리 배지에 27px 굵은 글씨로 따로 세운다.
            float cardW = 264f;
            float badgeH = 46f;
            Rect badge = new Rect(sideX, topY, 220f, badgeH);
            DrawRect(new Rect(badge.x + 2, badge.y + 4, badge.width, badge.height), new Color(0, 0, 0, 0.28f));
            DrawBorderedBox(badge, new Color(0.043f, 0.243f, 0.149f, 0.97f), new Color(0.98f, 0.845f, 0.35f), 3);
            if (manager.PendingCash > 0)
            {
                GUI.Label(new Rect(badge.x + 12, badge.y + 2, 196, 30), $"${manager.Money:N0}", _moneyBadgeStyle);
                GUI.Label(new Rect(badge.x + 12, badge.y + 25, 196, 18), $"+${manager.PendingCash:N0}", _moneyPendingStyle);
            }
            else
            {
                GUI.Label(new Rect(badge.x + 12, badge.y + 6, 196, 34), $"${manager.Money:N0}", _moneyBadgeStyle);
            }

            bool showExtraRow = manager.ContinuousFever01 > 0.05f || manager.IsLast10Jackpot || manager.CanEarlyCheckout;
            float cardH = showExtraRow ? 84f : 60f;
            float cardTop = topY + badgeH + 8f;
            Rect tl = new Rect(sideX, cardTop, cardW, cardH);
            DrawReceiptBox(tl);

            // 1행: 진행률 % + 스테이지 (현금은 위 배지가 전담)
            GUI.Label(new Rect(tl.x + 10, tl.y + 4, 120, 22), $"ST.{manager.CurrentStage}", _inkBold);
            if (CoreLoopConfig.EnableTreasureBranch)
            {
                GUI.color = new Color(0.10f, 0.38f, 0.78f);
                GUI.Label(new Rect(tl.x + 70, tl.y + 4, 50, 22), $"★{manager.TrustTokens}", _inkBold);
                GUI.color = new Color(0.76f, 0.12f, 0.32f);
                GUI.Label(new Rect(tl.x + 118, tl.y + 4, 50, 22), $"♦{manager.BlackMarketPts}", _inkBold);
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(tl.x + cardW - 62, tl.y + 4, 52, 22), $"{manager.OverallCleanPercent:0}%", _inkRight);

            // 2행: 진행률 바 (80% 컷 + 90% 골드 잭팟 구간)
            Rect progBg = new Rect(tl.x + 10, tl.y + 28, cardW - 20, 11);
            DrawRect(progBg, new Color(0.80f, 0.77f, 0.70f));
            DrawRect(new Rect(progBg.x + progBg.width * 0.90f, progBg.y, progBg.width * 0.10f, progBg.height), new Color(0.96f, 0.78f, 0.20f, 0.60f));
            Color progFill = manager.IsLast10Jackpot ? new Color(0.95f, 0.65f, 0.10f) : (manager.Phase == GamePhase.Phase2_FloorMopping ? new Color(0.22f, 0.62f, 0.90f) : new Color(0.18f, 0.68f, 0.34f));
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * (manager.OverallCleanPercent / 100f), progBg.height), progFill);
            DrawRect(new Rect(progBg.x + progBg.width * 0.80f, progBg.y - 2, 2, progBg.height + 4), new Color(0.75f, 0.20f, 0.18f));

            // 3행: 스테이지 + 현재 도구
            string toolTag = manager.Phase == GamePhase.Phase2_FloorMopping
                ? (manager.HasMopUnlocked ? "T자 대걸레" : "물티슈")
                : manager.CurrentToolName;
            GUI.Label(new Rect(tl.x + 10, tl.y + 42, cardW - 20, 22), $"{toolTag}", _inkSmall);

            // 4행 (동적): 피버 / 조기 정산 / 막판 잭팟
            if (showExtraRow)
            {
                float ry = tl.y + 62f;
                if (manager.CanEarlyCheckout)
                {
                    Rect stampR = new Rect(tl.x + 10, ry, 118, 22);
                    DrawBorderedBox(stampR, new Color(0.98f, 0.91f, 0.90f), new Color(0.78f, 0.18f, 0.18f), 2);
                    GUI.color = new Color(0.78f, 0.14f, 0.14f);
                    GUI.Label(stampR, "[K] 조기 정산", _lightCenter);
                    GUI.color = Color.white;
                }
                else if (manager.ContinuousFever01 > 0.05f)
                {
                    Rect fBg = new Rect(tl.x + 10, ry + 2, 118, 18);
                    DrawRect(fBg, new Color(0.78f, 0.75f, 0.68f));
                    Color fCol = manager.IsFeverHot ? new Color(0.96f, 0.45f, 0.12f) : new Color(0.95f, 0.74f, 0.18f);
                    DrawRect(new Rect(fBg.x, fBg.y, fBg.width * manager.ContinuousFever01, fBg.height), fCol);
                    GUI.Label(new Rect(fBg.x + 6, fBg.y, 108, 18), $"FEVER {manager.ContinuousFever01 * 100f:0}%", _inkSmall);
                }

                if (manager.IsLast10Jackpot)
                {
                    Rect jpR = new Rect(tl.x + 136, ry, 118, 22);
                    DrawBorderedBox(jpR, new Color(1.0f, 0.92f, 0.56f), new Color(0.84f, 0.48f, 0.06f), 2);
                    GUI.color = new Color(0.62f, 0.28f, 0.02f);
                    GUI.Label(jpR, $"★ 막판 ×{manager.Last10JackpotMult:0}", _lightCenter);
                    GUI.color = Color.white;
                }
            }

            // ===== 2. 상단 중앙: 악취 4칸 + 의심 게이지(80~95% ADRENALINE) =====
            float barW = Mathf.Round(Mathf.Min(380f, w * 0.40f));
            float barX = Mathf.Round((w - barW) * 0.5f);
            Rect tc = new Rect(barX, topY, barW, CoreLoopConfig.EnableSuspicionBranch ? 70f : 46f);
            DrawPaperCard(tc, manager.IsAdrenalineZone ? new Color(0.86f, 0.45f, 0.13f) : new Color(0.715f, 0.650f, 0.535f), manager.IsAdrenalineZone ? 2 : 1);

            int st = manager.StenchStage;
            Color stCol = manager.IsOutsideVentilating ? new Color(0.38f, 0.84f, 1f) :
                st == 0 ? new Color(0.40f, 0.88f, 0.48f) :
                st == 1 ? new Color(0.95f, 0.85f, 0.25f) :
                st == 2 ? new Color(0.98f, 0.56f, 0.18f) :
                st == 3 ? new Color(0.95f, 0.24f, 0.22f) : new Color(0.82f, 0.28f, 0.96f);

            Color stInk = Color.Lerp(stCol, new Color(0.26f, 0.19f, 0.12f), 0.58f);
            GUI.color = stInk;
            GUI.Label(new Rect(tc.x + 10, tc.y + 8, 54, 22), "☣ 악취", _lightSmall);
            GUI.color = Color.white;

            float cellStartX = tc.x + 68f;
            float trackW = barW - 156f;
            float cellW = (trackW - 9f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect cell = new Rect(cellStartX + i * (cellW + 3f), tc.y + 12, cellW, 13);
                DrawRect(cell, new Color(0.855f, 0.820f, 0.745f));
                float seg = Mathf.Clamp01((manager.StenchPercent - i * 25f) / 25f);
                if (seg > 0.001f) DrawRect(new Rect(cell.x, cell.y, cell.width * seg, cell.height), stCol);
            }
            string stTag = manager.IsOutsideVentilating ? "환기 중" :
                st == 4 ? "시야 흐림!" : st == 3 ? "속도↓↓" : st == 2 ? "속도↓" : st == 1 ? "답답함" : "쾌적";
            GUI.color = stInk;
            GUI.Label(new Rect(cellStartX + trackW + 8, tc.y + 8, 76, 22), stTag, _lightSmall);
            GUI.color = Color.white;

            // 의심 게이지
            if (CoreLoopConfig.EnableSuspicionBranch)
            {
                float sy = tc.y + 38f;
                Color suspCol = manager.IsAdrenalineZone ? new Color(1.0f, 0.42f, 0.15f)
                              : manager.SuspicionPercent >= 50f ? new Color(0.98f, 0.72f, 0.22f)
                              : new Color(0.52f, 0.84f, 0.96f);

                Color suspInk = Color.Lerp(suspCol, new Color(0.26f, 0.19f, 0.12f), 0.5f);
                GUI.color = suspInk;
                GUI.Label(new Rect(tc.x + 10, sy, 54, 22), "◎ 의심", _lightSmall);
                GUI.color = Color.white;

                Rect suspTrack = new Rect(cellStartX, sy + 4, trackW, 14);
                DrawRect(suspTrack, new Color(0.855f, 0.820f, 0.745f));
                DrawRect(new Rect(suspTrack.x + trackW * 0.80f, suspTrack.y - 1, trackW * 0.15f, suspTrack.height + 2), new Color(1.0f, 0.45f, 0.12f, manager.IsAdrenalineZone ? 0.68f : 0.34f));
                DrawRect(new Rect(suspTrack.x + trackW * 0.95f, suspTrack.y, trackW * 0.05f, suspTrack.height), new Color(0.92f, 0.15f, 0.15f, 0.58f));

                float suspFillW = trackW * (manager.SuspicionPercent / 100f);
                if (suspFillW > 0.5f) DrawRect(new Rect(suspTrack.x, suspTrack.y + 1, suspFillW, suspTrack.height - 2), suspCol);
                DrawRect(new Rect(suspTrack.x + trackW * 0.80f, suspTrack.y - 2, 2, suspTrack.height + 4), new Color(1f, 0.85f, 0.25f));

                string suspTag = manager.IsAdrenalineZone ? $"스릴 ×{manager.AdrenalineMult:0.#}" : $"{manager.SuspicionPercent:0}%";
                GUI.color = suspInk;
                GUI.Label(new Rect(cellStartX + trackW + 8, sy, 76, 22), suspTag, _lightSmall);
                GUI.color = Color.white;
            }

            // ===== 3. 우상단: 노란 포스트잇 의뢰 메모 (상시 [Tab] 강화 버튼 삭제 — 요청 #5·#10) =====
            bool hintMin = controller != null && controller.ShowHelp;
            Rect postItRect = hintMin
                ? new Rect(w - sideX - 128, topY, 128, 28)
                : new Rect(w - sideX - 228, topY, 228, 72);
            DrawPostItBox(postItRect);
            if (hintMin)
            {
                GUI.Label(postItRect, "■ 의뢰 메모 [H]", _inkCenter);
            }
            else
            {
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 8, postItRect.width - 20, 22), "■ 의뢰인 메모 [H]", _inkBold);
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 30, postItRect.width - 20, 38), manager.CurrentClientHint, _inkWrap);
            }

            // ===== 4. 중앙 하단: 수거통 게이지 + 트럭 정산 안내 + 같은 쓰레기 연속 보너스 =====
            DrawBottomBinBar(w, h, botSafe);

            // ===== 5. 슈킹 직후 진품/보통/가품 대형 결과 배너 (요청 #4) =====
            if (manager.AppraisalBannerTimer > 0f)
            {
                DrawAppraisalResultBanner(w, h, topY);
            }

            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect toastR = new Rect(Mathf.Round(w * 0.5f - 185), h - botSafe - 118, 370, 30);
                DrawPaperCard(toastR, new Color(0.82f, 0.66f, 0.22f), 1);
                GUI.color = new Color(0.46f, 0.33f, 0.05f);
                GUI.Label(toastR, manager.ToastMessage, _lightCenter);
                GUI.color = Color.white;
            }
        }

        private void DrawAppraisalResultBanner(float w, float h, float topY)
        {
            float alpha = Mathf.Clamp01(manager.AppraisalBannerTimer / 0.5f);
            float pop = Mathf.Lerp(0.86f, 1.0f, Mathf.Clamp01(manager.AppraisalBannerAge / 0.16f));
            float bw = Mathf.Round(490f * pop);
            float bh = Mathf.Round(86f * pop);
            Rect r = new Rect(Mathf.Round((w - bw) * 0.5f), Mathf.Round(topY + 86f), bw, bh);

            Color bg, border, titleCol;
            if (manager.AppraisalGrade == 2) // 진품 대박!
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 10f);
                bg = new Color(0.24f, 0.17f, 0.03f, 0.96f * alpha);
                border = new Color(1.0f, 0.84f, 0.18f, pulse * alpha);
                titleCol = new Color(1.0f, 0.92f, 0.34f, alpha);
            }
            else if (manager.AppraisalGrade == 0) // 가품 판정!
            {
                bg = new Color(0.18f, 0.12f, 0.10f, 0.96f * alpha);
                border = new Color(0.90f, 0.42f, 0.24f, alpha);
                titleCol = new Color(1.0f, 0.56f, 0.36f, alpha);
            }
            else // 보통 감정
            {
                bg = new Color(0.175f, 0.145f, 0.115f, 0.95f * alpha);
                border = new Color(0.86f, 0.80f, 0.66f, alpha);
                titleCol = new Color(0.98f, 0.94f, 0.86f, alpha);
            }

            DrawRect(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), new Color(border.r, border.g, border.b, 0.25f * alpha));
            DrawBorderedBox(r, bg, border, 3);

            GUI.color = titleCol;
            GUI.Label(new Rect(r.x + 12, r.y + 10, r.width - 24, 34), manager.AppraisalBannerTitle, _lightBigCenter);
            GUI.color = new Color(0.95f, 0.96f, 0.98f, alpha);
            GUI.Label(new Rect(r.x + 12, r.y + 48, r.width - 24, 26), manager.AppraisalBannerSub, _lightCenter);
            GUI.color = Color.white;
        }

        private void DrawBottomBinBar(float w, float h, float botSafe)
        {
            float bw = 316f;
            float bx = Mathf.Round((w - bw) * 0.5f);
            float by = Mathf.Round(h - botSafe - 48f);

            // 요청 #2: '압축' 대신 직관적인 [같은 쓰레기 연속 보너스] (동그라미 게이지 ●●●○○ + 효과 명시!)
            if (CoreLoopConfig.EnableStreakCompression)
            {
                if (manager.CompressionBannerTimer > 0f)
                {
                    Rect cBan = new Rect(Mathf.Round(w * 0.5f - 210), by - 34, 420, 28);
                    DrawPaperCard(cBan, new Color(0.30f, 0.64f, 0.44f), 2);
                    GUI.color = new Color(0.12f, 0.40f, 0.24f);
                    GUI.Label(cBan, manager.CompressionBannerText, _lightCenter);
                    GUI.color = Color.white;
                }
                else if (manager.StreakCount >= 2 && manager.Phase == GamePhase.Phase1_TrashRemoval)
                {
                    int target = manager.CompressionStreakTarget;
                    string dots = "";
                    for (int i = 0; i < target; i++) dots += (i < manager.StreakCount ? "●" : "○");
                    Rect sChip = new Rect(Mathf.Round(w * 0.5f - 190), by - 32, 380, 26);
                    DrawPaperCard(sChip, new Color(0.32f, 0.66f, 0.46f), 1);
                    GUI.color = new Color(0.12f, 0.40f, 0.24f);
                    GUI.Label(sChip, $"같은 쓰레기 [{manager.StreakTypeLabel}] {dots}  → 봉투 비움 + ×2", _lightCenter);
                    GUI.color = Color.white;
                }
            }

            // ===== v1.3 (요청 #4·#12): 수거통 게이지 =====
            // 봉투를 만들지 않는다. 담긴 개수만큼 언제든 트럭에서 정산할 수 있다.
            bool inZone = controller != null && controller.SettlementZoneActive;
            bool full = manager.IsBinFull;
            Color bd = inZone ? new Color(0.96f, 0.78f, 0.22f) : full ? new Color(0.86f, 0.42f, 0.24f) : new Color(0.66f, 0.61f, 0.52f);
            Rect card = new Rect(bx, by, bw, 46);
            DrawPaperCard(card, bd, (inZone || full) ? 2 : 1);

            GUI.color = inZone ? new Color(0.52f, 0.34f, 0.02f) : full ? new Color(0.62f, 0.20f, 0.10f) : new Color(0.28f, 0.245f, 0.20f);
            GUI.Label(new Rect(card.x + 12, card.y + 5, 150, 22), $"▣ 수거통 {manager.CollectingCount} / {manager.BinCapacity}", _lightSmall);
            string rightStr = manager.CollectingCount > 0
                ? (inZone 
                    ? $"[RMB] 정산 · ${manager.CollectingPendingValue:N0}"
                    : $"{manager.CollectingPendingValue:N0} · 트럭에서 정산")
                : "비어 있음";
            GUI.Label(new Rect(card.x + bw - 190, card.y + 5, 178, 22), rightStr, _lightRight);
            GUI.color = Color.white;

            Rect track = new Rect(card.x + 12, card.y + 30, bw - 24, 9);
            DrawRect(track, new Color(0.855f, 0.820f, 0.745f));
            float ratio = manager.BinFill01;
            Color fillC = full ? new Color(0.88f, 0.40f, 0.22f) : new Color(0.34f, 0.66f, 0.80f);
            DrawRect(new Rect(track.x, track.y, track.width * ratio, track.height), fillC);
        }

        private void DrawCrosshairAndPrompt(float w, float h)
        {
            if (controller == null) return;
            float cx = Mathf.Round(w * 0.5f), cy = Mathf.Round(h * 0.5f);

            // WebGL 브라우저 포인터 잠금 안내 (클릭 시 1인칭 조작 활성화)
            if (Application.platform == RuntimePlatform.WebGLPlayer && Cursor.lockState != CursorLockMode.Locked)
            {
                Rect lockR = new Rect(cx - 170, cy - 22, 340, 44);
                DrawPaperCard(lockR, new Color(0.76f, 0.52f, 0.14f), 2);
                GUI.Label(lockR, "화면을 클릭하여 1인칭 시점 시작", _inkCenter);
                return;
            }

            bool hasTarget = controller.AimDoor != null ||
                             controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain;
            // 종이 톤 HUD에서도 항상 읽히도록: 밝은 할로우 + 어두운 먹색 점 (조준 시 세이지 그린)
            Color dotCol = hasTarget ? new Color(0.24f, 0.58f, 0.36f) : new Color(0.20f, 0.17f, 0.13f, 0.92f);
            float outer = hasTarget ? 9f : 7f;
            DrawRect(new Rect(cx - outer * 0.5f, cy - outer * 0.5f, outer, outer), new Color(1f, 0.985f, 0.95f, 0.78f));
            float sz = hasTarget ? 5f : 3f;
            DrawRect(new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz), dotCol);

            // ===== 큰 봉투 체력바 (요청 #8) =====
            // 예전에는 매 프레임 '완료율'을 0→현재값으로 다시 채워 그려서, 때릴 때마다
            // 0에서 차오르는 것처럼 보였다. 이제 '남은 체력'을 부드럽게 따라 내려가며,
            // 맞은 순간의 순간 감소분은 흰색 잔상으로 남겨 타격감을 준다.
            if (controller.AimIsPile)
            {
                float hp = Mathf.Clamp01(controller.AimPileHp01);
                _pileHpDisplay = Mathf.MoveTowards(_pileHpDisplay, hp, Time.deltaTime * 3.2f);
                if (hp > _pileHpDisplay) _pileHpDisplay = hp;          // 리셋 없이 항상 '깎이는' 방향만
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

            // 1초 HOLD 진동 차징 바 (바닥 쓰레기 줍기)
            if (!controller.AimIsPile && controller.AimProgress01 > 0.01f && controller.AimProgress01 < 0.999f)
            {
                Rect pb = new Rect(cx - 34, cy + 14, 68, 7);
                DrawPaperCard(pb, new Color(0.55f, 0.47f, 0.34f), 1);
                DrawRect(new Rect(pb.x + 1, pb.y + 1, (pb.width - 2) * controller.AimProgress01, pb.height - 2), new Color(0.92f, 0.74f, 0.18f));
            }

            string prompt = null;
            if (controller.SettlementZoneActive && manager.CollectingCount > 0)
            {
                prompt = $"[RMB] 트럭 정산 · {manager.CollectingCount}개 · ${manager.CollectingPendingValue:N0}";
            }
            else if (controller.SettlementZoneActive && manager.CollectingCount <= 0)
            {
                prompt = "정산 구역 · 수거통이 비어 있음";
            }
            else if (controller.AimDoor != null)
            {
                prompt = controller.AimDoor.IsOpen ? "[F] 문 닫기" : "[F] 현관문 열기";
            }
            else if (controller.NeedMouseShakeHint)
            {
                prompt = "꾹 누르면 기본 닦기 · 흔들면 가속";
            }
            else if (controller.AimIsPile)
            {
                prompt = "[클릭] 큰 봉투 터뜨리기";
            }
            else if (manager.IsBinFull)
            {
                prompt = $"수거통 가득 · 트럭에서 [RMB] 정산";
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Rect pr = new Rect(cx - 140, cy + 28, 280, 28);
                DrawPaperCard(pr, new Color(0.52f, 0.44f, 0.32f), 1);
                GUI.color = new Color(0.26f, 0.21f, 0.15f);
                GUI.Label(pr, prompt, _lightCenter);
                GUI.color = Color.white;
            }

            // v1.3 트럭 옆 '통합 강화소' 안내 (요청 #5·#10)
            if (controller.KioskActive)
            {
                float fy = string.IsNullOrEmpty(prompt) ? cy + 32f : cy + 64f;
                Rect fp = new Rect(cx - 220f, fy, 440f, 50f);
                DrawPaperCard(fp, controller.KioskColor, 2);

                GUI.color = Color.Lerp(controller.KioskColor, new Color(0.24f, 0.18f, 0.10f), 0.55f);
                GUI.Label(new Rect(fp.x + 10f, fp.y + 5f, fp.width - 20f, 22f), controller.KioskTitle, _lightBold);
                GUI.color = new Color(0.32f, 0.28f, 0.22f);
                GUI.Label(new Rect(fp.x + 10f, fp.y + 26f, fp.width - 20f, 20f), controller.KioskDetail, _lightSmall);
                GUI.color = Color.white;
            }
        }

        // ===== 요청 #5: 크고 화려한 보물 자동 발견 팝업 ([Q] 전달 vs [E] 슈킹) =====
        private void DrawGrandTreasurePopup(float w, float h)
        {
            var item = manager.ActiveTreasure;
            if (item == null) return;

            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float now = Time.time;
            float openT = Mathf.Clamp01(manager.TreasurePopupOpenAge / 0.22f);
            float popScale = Mathf.Lerp(0.84f, 1.0f, 1f - (1f - openT) * (1f - openT));

            float cx = w * 0.5f, cy = h * 0.5f;

            // 회전하는 황금 광선 12줄 (화려한 잭팟 연출!)
            Color rayCol = item.isMentionedByClient
                ? new Color(1.0f, 0.42f, 0.18f, 0.14f)
                : new Color(1.0f, 0.84f, 0.24f, 0.14f);
            Matrix4x4 prevMtx = GUI.matrix;
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f + now * 18f;
                GUIUtility.RotateAroundPivot(ang, new Vector2(cx, cy));
                DrawRect(new Rect(cx - 24f, cy - Mathf.Max(w, h) * 0.65f, 48f, Mathf.Max(w, h) * 1.3f), rayCol);
                GUI.matrix = prevMtx;
            }

            float cw = Mathf.Round(Mathf.Min(610f, w * 0.84f) * popScale);
            float ch = Mathf.Round(Mathf.Min(410f, h * 0.78f) * popScale);
            Rect card = new Rect(Mathf.Round(cx - cw * 0.5f), Mathf.Round(cy - ch * 0.5f), cw, ch);

            // 바깥 글로우 프레임 + 이중 골드 테두리
            float glowPulse = 0.65f + 0.35f * Mathf.Sin(now * 6f);
            Color outerGold = item.isMentionedByClient
                ? new Color(1.0f, 0.38f, 0.18f, glowPulse)
                : new Color(1.0f, 0.82f, 0.22f, glowPulse);
            DrawRect(new Rect(card.x - 8, card.y - 8, card.width + 16, card.height + 16), new Color(outerGold.r, outerGold.g, outerGold.b, 0.28f));
            DrawBorderedBox(card, new Color(0.145f, 0.115f, 0.082f, 0.975f), outerGold, 3);
            DrawBorderedBox(new Rect(card.x + 6, card.y + 6, card.width - 12, card.height - 12), new Color(0.185f, 0.145f, 0.100f, 0.985f), new Color(1f, 0.92f, 0.52f, 0.45f), 1);

            // 상단 화려한 배너 + 쉬머 스윕
            Rect banner = new Rect(card.x + 16, card.y + 16, cw - 32, 42);
            Color bannerBg = item.isMentionedByClient ? new Color(0.76f, 0.18f, 0.12f)
                           : item.rarity == TreasureRarity.Legendary ? new Color(0.78f, 0.52f, 0.08f)
                           : new Color(0.18f, 0.46f, 0.76f);
            DrawBorderedBox(banner, bannerBg, new Color(1f, 0.92f, 0.46f), 2);
            float sweepX = Mathf.Repeat(now * 260f, banner.width + 120f) - 60f;
            DrawRect(new Rect(Mathf.Clamp(banner.x + sweepX, banner.x, banner.xMax - 40), banner.y + 2, 40, banner.height - 4), new Color(1f, 1f, 1f, 0.18f));

            string bannerText = item.isMentionedByClient
                ? "★ 의뢰인 언급 핵심 물건 발견! (단가 ×2 · 의심 ×2) ★"
                : item.rarity == TreasureRarity.Legendary
                    ? "★ LEGENDARY 희귀 보물 발견! ★"
                    : "✦ 보물 발견! 처리 방식을 선택하세요 ✦";
            GUI.color = new Color(1f, 0.96f, 0.75f);
            GUI.Label(banner, bannerText, _lightCenter);
            GUI.color = Color.white;

            // 중앙 대형 보물 이름 + 반짝이 별
            string starAnim = Mathf.Sin(now * 8f) > 0 ? "✦" : "★";
            GUI.color = new Color(1f, 0.88f, 0.34f);
            GUI.Label(new Rect(card.x + 20, card.y + 72, cw - 40, 38), $"{starAnim}  {item.itemName}  {starAnim}", _lightTitleCenter);
            GUI.color = Color.white;

            int honestTokens = item.isMentionedByClient ? 2 : 1;
            int honestTip = Mathf.RoundToInt((item.isMentionedByClient ? 65 : 30) * manager.StageRewardMult);
            float mentionMult = item.isMentionedByClient ? 2f : 1f;
            int stealEst = Mathf.RoundToInt(item.value * mentionMult * manager.AdrenalineMult);
            int bmPts = item.isMentionedByClient ? 2 : 1;
            float suspAdd = item.baseSuspicionGain * mentionMult;

            string subBadge = manager.IsAdrenalineZone
                ? $"★ 아드레날린 적용 중! (슈킹 단가 ×{manager.AdrenalineMult:0.#})"
                : $"현재 의심도 {manager.SuspicionPercent:0}% · 100% 도달 시 강제 퇴거 정산";
            GUI.color = new Color(0.84f, 0.845f, 0.82f);
            GUI.Label(new Rect(card.x + 20, card.y + 114, cw - 40, 24), subBadge, _lightCenter);
            GUI.color = Color.white;

            // 좌/우 대형 선택 카드
            float boxW = Mathf.Round((cw - 56f) * 0.5f);
            float boxH = ch - 168f;
            float boxY = card.y + 148f;

            // 좌측: [Q] 성실 전달
            Rect qBox = new Rect(card.x + 20, boxY, boxW, boxH);
            DrawBorderedBox(qBox, new Color(0.105f, 0.215f, 0.160f, 0.97f), new Color(0.36f, 0.90f, 0.58f), 2);
            GUI.color = new Color(0.46f, 1.0f, 0.66f);
            GUI.Label(new Rect(qBox.x + 12, qBox.y + 12, qBox.width - 24, 28), "[Q] 의뢰인에게 전달", _lightBigCenter);
            GUI.color = Color.white;
            GUI.Label(new Rect(qBox.x + 20, qBox.y + 52, qBox.width - 40, 24), $"• ★ 신뢰도 토큰 +{honestTokens} (클리어 후 특전)", _lightLeft);
            GUI.Label(new Rect(qBox.x + 20, qBox.y + 80, qBox.width - 40, 24), "• 의심도 -25% 즉시 세탁", _lightLeft);
            GUI.Label(new Rect(qBox.x + 20, qBox.y + 108, qBox.width - 40, 24), $"• 성실 사례금 +${honestTip}", _lightLeft);

            // 우측: [E] 즉시 슈킹
            Rect eBox = new Rect(card.x + cw - 20 - boxW, boxY, boxW, boxH);
            DrawBorderedBox(eBox, new Color(0.24f, 0.08f, 0.11f, 0.96f), new Color(1.0f, 0.34f, 0.30f), 2);
            GUI.color = new Color(1.0f, 0.48f, 0.42f);
            GUI.Label(new Rect(eBox.x + 12, eBox.y + 12, eBox.width - 24, 28), "[E] 몰래 슈킹", _lightBigCenter);
            GUI.color = Color.white;
            string evTag = manager.EvasionChance > 0f ? $" (회피 {manager.EvasionChance * 100f:0}%)" : "";
            GUI.Label(new Rect(eBox.x + 20, eBox.y + 52, eBox.width - 40, 24), $"• 고액 현금 ~${stealEst} (진품/가품 감정)", _lightLeft);
            GUI.Label(new Rect(eBox.x + 20, eBox.y + 80, eBox.width - 40, 24), $"• ♦ 암시장 포인트 +{bmPts} (클리어 후 상점)", _lightLeft);
            GUI.color = new Color(1f, 0.72f, 0.38f);
            GUI.Label(new Rect(eBox.x + 20, eBox.y + 108, eBox.width - 40, 24), $"• 의심도 +{suspAdd:0}%{evTag}", _lightLeft);
            GUI.color = Color.white;

            if (manager.TreasureDecisionLockTimer <= 0f)
            {
                if (GUI.Button(new Rect(qBox.x + 20, qBox.yMax - 48, qBox.width - 40, 36), "전달 확정 [Q]", _btnStyle))
                    manager.ResolveActiveTreasure(false);
                if (GUI.Button(new Rect(eBox.x + 20, eBox.yMax - 48, eBox.width - 40, 36), "슈킹 확정 [E]", _btnStyle))
                    manager.ResolveActiveTreasure(true);
            }
        }

        // ===== v1.3 트럭 옆 통합 강화소 (요청 #5·#10) =====
        /// <summary>
        /// [E]로 통합 강화소에 들어왔을 때 뜨는 강화 창.
        /// 예전 [Tab] 전역 스킬트리는 삭제되고, 이 창이 유일한 강화 경로다.
        /// 14개 항목을 4열 카드 그리드로 한눈에 보여주고, 카드를 누르면 바로 산다.
        /// </summary>
        private void DrawUpgradeKiosk(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            var station = manager.fieldStation;
            if (station == null)
            {
                GUI.Label(new Rect(0, 0, w, h), "강화소를 찾을 수 없습니다", _lightCenter);
                return;
            }

            float pw = Mathf.Min(w - 60f, 1120f);
            float ph = Mathf.Min(h - 80f, 700f);
            Rect panel = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round((h - ph) * 0.5f), pw, ph);

            // 바깥 판 + 헤더
            DrawBorderedBox(panel, new Color(0.098f, 0.086f, 0.070f, 0.97f), new Color(0.86f, 0.72f, 0.30f), 3);

            Rect hdr = new Rect(panel.x + 14, panel.y + 12, panel.width - 28, 52);
            DrawReceiptBox(hdr);
            GUI.color = new Color(0.62f, 0.45f, 0.04f);
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 6, 420, 26), "통합 강화소 · 트럭 작업대", _kioskHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 30, 420, 18), "카드를 클릭하면 바로 강화 · [ESC] 닫기", _inkSmall);

            // 보유 현금을 이 창에서도 크게
            Rect cash = new Rect(hdr.xMax - 210, hdr.y + 8, 196, 36);
            DrawBorderedBox(cash, new Color(0.043f, 0.243f, 0.149f), new Color(0.98f, 0.845f, 0.35f), 2);
            GUI.Label(new Rect(cash.x + 10, cash.y + 4, 176, 28), $"${manager.Money:N0}", _moneyBadgeStyle);

            int cols = 4;
            int rows = Mathf.CeilToInt(station.ItemCount / (float)cols);
            float gap = 10f;
            float gridX = panel.x + 16f;
            float gridY = hdr.yMax + 12f;
            float gridW = panel.width - 32f;
            float gridH = panel.yMax - 48f - gridY;
            float cw2 = (gridW - gap * (cols - 1)) / cols;
            float ch2 = (gridH - gap * (rows - 1)) / rows;

            for (int i = 0; i < station.ItemCount; i++)
            {
                var item = station.GetItem(i);
                int rIdx = i / cols;
                int cIdx = i % cols;
                Rect card = new Rect(
                    Mathf.Round(gridX + cIdx * (cw2 + gap)),
                    Mathf.Round(gridY + rIdx * (ch2 + gap)),
                    Mathf.Round(cw2), Mathf.Round(ch2));

                Color border = item.maxed ? new Color(0.34f, 0.62f, 0.40f)
                             : item.affordable ? item.color
                             : new Color(0.52f, 0.48f, 0.40f);
                DrawPaperCard(card, border, item.affordable ? 2 : 1);

                // 아이콘 + 제목 + 레벨
                GUI.color = item.maxed ? new Color(0.16f, 0.42f, 0.24f) : new Color(0.22f, 0.18f, 0.13f);
                GUI.Label(new Rect(card.x + 10, card.y + 6, 30, 26), item.icon, _kioskIconStyle);
                GUI.Label(new Rect(card.x + 40, card.y + 6, card.width - 50, 22), item.title, _inkBold);
                GUI.Label(new Rect(card.x + card.width - 66, card.y + 6, 56, 22),
                    item.maxed ? "MAX" : $"{item.level}/{item.maxLevel}", _inkRight);

                // 현재 → 다음 스탯
                GUI.color = new Color(0.32f, 0.28f, 0.22f);
                GUI.Label(new Rect(card.x + 10, card.y + 30, card.width - 20, 20), item.stat, _inkSmall);

                // 구매 스트립
                Rect buy = new Rect(card.x + 8, card.yMax - 30, card.width - 16, 24);
                Color buyBg = item.maxed ? new Color(0.86f, 0.88f, 0.84f)
                            : item.affordable ? item.color
                            : new Color(0.88f, 0.86f, 0.80f);
                DrawBorderedBox(buy, buyBg, new Color(0.30f, 0.26f, 0.18f), 1);
                string buyText = item.maxed ? "강화 완료" : $"+${item.cost:N0}";
                GUI.color = item.maxed ? new Color(0.38f, 0.46f, 0.36f)
                          : item.affordable ? ReadableOn(item.color)
                          : new Color(0.52f, 0.48f, 0.42f);
                GUI.Label(buy, buyText, _lightCenter);
                GUI.color = Color.white;

                if (!item.maxed && GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    station.TryBuy(item.nodeId);
                }
            }

            // 닫기
            Rect closeR = new Rect(panel.xMax - 150, panel.yMax - 36, 134, 26);
            DrawPaperCard(closeR, new Color(0.72f, 0.66f, 0.54f), 1);
            GUI.Label(closeR, "[ESC] 강화소 나가기", _inkCenter);
            if (GUI.Button(closeR, GUIContent.none, GUIStyle.none)) manager.CloseUpgradeKiosk();
        }

        // ===== 인게임 [Tab] & 클리어 후 [$ 강화] 공용: 현금($) 전용 19노드 스킬트리 =====
        private void DrawCashSkillTree(float w, float h, bool embeddedInHub)
        {
            if (!embeddedInHub) GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topY = embeddedInHub ? Mathf.Round(Mathf.Max(92f, h * 0.14f)) : Mathf.Round(Mathf.Max(34f, h * 0.055f));
            float botReserve = embeddedInHub ? 128f : Mathf.Round(Mathf.Max(56f, h * 0.088f));

            if (!embeddedInHub)
            {
                Rect hdr = new Rect(Mathf.Round(w * 0.5f - 190), topY, 380, 38);
                DrawReceiptBox(hdr);
                GUI.color = new Color(0.06f, 0.46f, 0.20f);
                GUI.Label(new Rect(hdr.x + 18, hdr.y + 7, 150, 24), $"보유 현금 ${manager.Money:N0}", _lightBold);
                GUI.color = Color.white;
                GUI.Label(new Rect(hdr.x + 210, hdr.y + 7, 154, 24), "강화 트리 · [Tab] 닫기", _inkRight);
            }

            float availH = h - topY - botReserve - 60f;
            float scale = Mathf.Clamp(Mathf.Min(w / 1060f, availH / 520f), 0.72f, 1.0f);
            Vector2 center = new Vector2(Mathf.Round(w * 0.5f), Mathf.Round(topY + 46f + availH * 0.48f));

            Color cTool = new Color(0.95f, 0.78f, 0.24f);
            Color cBag = new Color(0.32f, 0.78f, 0.98f);
            Color cSurv = new Color(0.36f, 0.90f, 0.54f);
            Color cFist = new Color(0.98f, 0.56f, 0.24f);

            string toolNextHint =
                manager.ToolStep < 3 ? $"현재: {manager.CurrentToolName} · 다음: 1단계 고무장갑 ({manager.ToolStep + 1}/3) · 홀드 {manager.EffectiveHoldDuration:0.00}초" :
                manager.ToolStep == 3 ? "다음: ★ 2단계 [집게·빗자루] 해금 (1/3)" :
                manager.ToolStep < 6 ? $"현재: {manager.CurrentToolName} · 다음: 2단계 ({manager.ToolStep - 2}/3) · 홀드 {manager.EffectiveHoldDuration:0.00}초" :
                manager.ToolStep == 6 ? "다음: ★ 3단계 [진공청소기] 해금 (1/3)" :
                manager.ToolStep < 9 ? $"현재: {manager.CurrentToolName} · 다음: 3단계 ({manager.ToolStep - 5}/3) · 홀드 {manager.EffectiveHoldDuration:0.00}초" :
                "3단계 진공청소기 (3/3) MAX";

            string truckNextHint =
                manager.LvTruckSize == 0 ? "다음: ★ 1단계 [리어카] 해금 (1/3)" :
                manager.LvTruckSize < 3 ? $"현재: {manager.CurrentVehicleName} · 다음: 리어카 ({manager.LvTruckSize + 1}/3)" :
                manager.LvTruckSize == 3 ? "다음: ★ 2단계 [다마스] 해금 (1/3)" :
                manager.LvTruckSize < 6 ? $"현재: {manager.CurrentVehicleName} · 다음: 다마스 ({manager.LvTruckSize - 2}/3)" :
                manager.LvTruckSize == 6 ? "다음: ★ 3단계 [1톤 트럭] 해금 (1/3)" :
                manager.LvTruckSize < 9 ? $"현재: {manager.CurrentVehicleName} · 다음: 1톤 트럭 ({manager.LvTruckSize - 5}/3)" :
                "3단계 1톤 트럭 (3/3) MAX";

            var nodes = new[]
            {
                // 좌상: 도구 다단계 진화 & 흡입
                Node("tool", "▲", "도구 진화", toolNextHint, $"${manager.CostTool}", "", new Vector2(-120, -76), manager.ToolStep, 9, cTool),
                Node("sweep", "◎", "커서 범위", $"원 반경 {manager.SweepRadius:0.00}m · 동시 {manager.AutoGrabPerTick}개", $"${manager.CostSweep}", "tool", new Vector2(-225, -76), manager.LvSweepRadius, 10, cTool),
                Node("fever", "✹", "피버 엔진", $"연속 흡입 → 범위+{manager.LvFever * 8}% · 속도+{manager.LvFever * 14}% · 수익×{1.15f + manager.LvFever * 0.10f:0.00}", $"${manager.CostFever}", "tool", new Vector2(-120, -162), manager.LvFever, 5, cTool),
                Node("crit", "✦", "크리티컬", $"확률 {manager.CritChance * 100f:0}% · 수익 ×2", $"${manager.CostCrit}", "sweep", new Vector2(-330, -76), manager.LvCrit, 10, cTool),
                Node("chain", "◈", "연쇄 도미노", $"확률 {manager.ChainChance * 100f:0}% · 주변 +{manager.ChainExtraCount}개", $"${manager.CostChain}", "crit", new Vector2(-435, -76), manager.LvChainPop, 8, cTool),
                Node("roomba", "◉", "로봇청소기", $"현재 {manager.RoombaCount}대 · {manager.RoombaEatInterval:0.00}초/개 (서브 자동화)", $"${manager.CostRoomba}", "chain", new Vector2(-435, -162), manager.LvRoomba, 2, cTool),

                // 우상: 운반 & 경제
                Node("bag", "▣", "수거통 용량", $"최대 {manager.BinCapacity}개 담기 (언제든 트럭 정산)", $"${manager.CostBag}", "", new Vector2(120, -76), manager.LvBagSize, 10, cBag),
                Node("truck", "▶", "수거함 진화", truckNextHint, $"${manager.CostTruck}", "bag", new Vector2(330, -76), manager.LvTruckSize, 9, cBag),
                Node("combo", "»", "정산 콤보 유지", $"연속 정산 ×1.25/스택 · 유지 {manager.MaxComboWindow:0.0}초", $"${manager.CostComboTime}", "truck", new Vector2(435, -76), manager.LvComboTime, 6, cBag),
                Node("price", "◆", "분리수거 단가", $"단가 +{manager.LvTrashPrice * 15}%", $"${manager.CostTrashPrice}", "bag", new Vector2(120, -162), manager.LvTrashPrice, 10, cBag),


                // 좌하: 생존 & 걸레질
                Node("mask", "☣", "방독면", $"악취 -{Mathf.Min(82, manager.LvMask * 8)}%", $"${manager.CostMask}", "", new Vector2(-120, 76), manager.LvMask, 10, cSurv),
                Node("speed", "»", "운동화", $"이동속도 +{manager.LvMoveSpeed * 8}%", $"${manager.CostMoveSpeed}", "mask", new Vector2(-225, 76), manager.LvMoveSpeed, 8, cSurv),
                Node("mop", "┴", "T자 대걸레", $"2단계 바닥 닦기 ×{1f + manager.LvMop * 0.75f:0.00} ({manager.LvMop}/3)", $"${manager.CostMop}", "mask", new Vector2(-120, 162), manager.LvMop, 3, cSurv),
                Node("scrub", "✧", "강력 세제", $"세정력 +{manager.LvScrubPower * 25}%", $"${manager.CostScrub}", "mop", new Vector2(-225, 162), manager.LvScrubPower, 8, cSurv),

                // 우하: 큰 봉지 파괴 & 10% 투시
                Node("fist", "❖", "주먹 힘", $"큰 봉투 타격 {manager.FistDamage:0.0}", $"${manager.CostFist}", "", new Vector2(120, 76), manager.LvFistPower, 10, cFist),
                Node("shock", "✹", "충격파 펀치", $"주변 큰 봉투 동시 타격 Lv.{manager.LvShockwave}/6", $"${manager.CostShockwave}", "fist", new Vector2(225, 76), manager.LvShockwave, 6, cFist),
                Node("beacon", "◈", "10% 투시", "잔여 10% 쓰레기 황금 테두리", $"${manager.CostBeacon}", "fist", new Vector2(120, 162), manager.HasBeaconDetect ? 1 : 0, 1, cFist),
            };

            foreach (var n in nodes)
            {
                Vector2 pPos = center;
                if (!string.IsNullOrEmpty(n.parentId))
                {
                    foreach (var p in nodes) if (p.id == n.parentId) { pPos = center + p.offset * scale; break; }
                }
                Vector2 nPos = center + n.offset * scale;
                DrawLine(pPos, nPos, n.unlocked ? new Color(n.accent.r, n.accent.g, n.accent.b, 0.58f) : new Color(0.36f, 0.33f, 0.28f, 0.42f), 2f);
            }

            float coreSz = Mathf.Round(52f * scale);
            DrawBorderedBox(new Rect(center.x - coreSz * 0.5f, center.y - coreSz * 0.5f, coreSz, coreSz), new Color(0.225f, 0.190f, 0.140f), cTool, 2);
            GUI.color = cTool;
            GUI.Label(new Rect(center.x - coreSz * 0.5f, center.y - coreSz * 0.5f, coreSz, coreSz), "$ 강화", _lightCenter);
            GUI.color = Color.white;

            SkillNodeDef? hovered = null;
            float nW = Mathf.Round(84f * scale), nH = Mathf.Round(58f * scale);
            Vector2 mPos = Event.current.mousePosition;

            foreach (var n in nodes)
            {
                Vector2 p = center + n.offset * scale;
                Rect r = new Rect(Mathf.Round(p.x - nW * 0.5f), Mathf.Round(p.y - nH * 0.5f), nW, nH);
                bool isMax = n.level >= n.maxLevel;
                Color bg = !n.unlocked ? new Color(0.155f, 0.140f, 0.120f, 0.94f)
                         : isMax ? new Color(0.16f, 0.27f, 0.19f, 0.97f)
                         : n.affordable ? new Color(0.30f, 0.245f, 0.145f, 0.97f)
                         : new Color(0.19f, 0.175f, 0.155f, 0.96f);
                Color bd = !n.unlocked ? new Color(0.25f, 0.27f, 0.30f)
                         : isMax ? new Color(0.38f, 0.94f, 0.52f)
                         : n.affordable ? n.accent
                         : new Color(n.accent.r * 0.55f, n.accent.g * 0.55f, n.accent.b * 0.55f);

                DrawBorderedBox(r, bg, bd, n.affordable ? 2 : 1);

                _nodeIconStyle.normal.textColor = n.unlocked ? (isMax ? new Color(0.45f, 1f, 0.58f) : n.accent) : new Color(0.42f, 0.44f, 0.48f);
                GUI.Label(new Rect(r.x, r.y + 4, r.width, r.height * 0.48f), n.unlocked ? n.icon : "×", _nodeIconStyle);

                _nodeSubStyle.normal.textColor = n.unlocked ? Color.white : new Color(0.54f, 0.56f, 0.60f);
                string sub = !n.unlocked ? "잠김" : isMax ? "MAX" : $"{n.level}/{n.maxLevel} · {n.costTag}";
                GUI.Label(new Rect(r.x, r.y + r.height * 0.52f, r.width, r.height * 0.42f), sub, _nodeSubStyle);

                if (r.Contains(mPos)) hovered = n;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && n.unlocked && !isMax)
                {
                    manager.BuyNode(n.id);
                }
            }

            // 하단 상세 영수증 바 (넉넉한 52px 높이로 글씨 절대 깨짐 없음!)
            float infoY = embeddedInHub ? (h - 122f) : (h - botReserve - 52f);
            Rect infoR = new Rect(Mathf.Round(w * 0.5f - 290), Mathf.Round(infoY), 580, 50);
            DrawReceiptBox(infoR);
            if (hovered.HasValue)
            {
                var hv = hovered.Value;
                bool isMax = hv.level >= hv.maxLevel;
                string state = !hv.unlocked ? "[잠김 · 선행 필요]" : isMax ? "[MAX]" : $"[클릭 구매 · {hv.costTag}]";
                GUI.Label(new Rect(infoR.x + 14, infoR.y + 5, infoR.width - 28, 22), $"{hv.icon} {hv.title} ({hv.level}/{hv.maxLevel})   {state}", _inkBold);
                GUI.Label(new Rect(infoR.x + 14, infoR.y + 26, infoR.width - 28, 20), $"• {hv.shortStat}", _inkSmall);
            }
            else
            {
                GUI.Label(infoR, "노드에 마우스 올리기 · 좌클릭 즉시 구매", _inkCenter);
            }
        }

        private SkillNodeDef Node(string id, string icon, string title, string shortStat, string costTag, string parentId, Vector2 offset, int lv, int maxLv, Color accent)
        {
            bool unlocked = manager.IsNodeUnlocked(id);
            int c = id switch
            {
                "tool" => manager.CostTool, "sweep" => manager.CostSweep, "fever" => manager.CostFever,
                "crit" => manager.CostCrit, "chain" => manager.CostChain, "roomba" => manager.CostRoomba,
                "bag" => manager.CostBag, "throw" => manager.CostThrowPower, "truck" => manager.CostTruck,
                "magnet" => manager.CostMagnet, "price" => manager.CostTrashPrice, "combo" => manager.CostComboTime,
                "mask" => manager.CostMask, "speed" => manager.CostMoveSpeed, "mop" => manager.CostMop,
                "scrub" => manager.CostScrub, "fist" => manager.CostFist, "shock" => manager.CostShockwave,
                "beacon" => manager.CostBeacon, _ => 999999
            };
            bool afford = unlocked && lv < maxLv && manager.Money >= c;
            return new SkillNodeDef
            {
                id = id, icon = icon, title = title, shortStat = shortStat, costTag = costTag,
                parentId = parentId, offset = offset, level = lv, maxLevel = maxLv,
                unlocked = unlocked, affordable = afford, accent = accent
            };
        }

        // ===== 요청 #6: 클리어 후 5버튼 허브 (다시 시작 · 다음 단계 · ♦암시장 · ★특전 · $강화) =====
        private void DrawPostRoundHub(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topY = Mathf.Round(Mathf.Max(30f, h * 0.045f));

            // 상단 통합 재화 영수증 바
            Rect hdr = new Rect(Mathf.Round(w * 0.5f - 290), topY, 580, 42);
            DrawReceiptBox(hdr);
            GUI.Label(new Rect(hdr.x + 16, hdr.y + 9, 100, 24), $"STAGE {manager.CurrentStage}", _inkBold);
            GUI.color = new Color(0.06f, 0.46f, 0.20f);
            GUI.Label(new Rect(hdr.x + 125, hdr.y + 9, 125, 24), $"현금 ${manager.Money:N0}", _lightBold);
            if (CoreLoopConfig.EnableTreasureBranch)
            {
                GUI.color = new Color(0.10f, 0.38f, 0.78f);
                GUI.Label(new Rect(hdr.x + 260, hdr.y + 9, 115, 24), $"★ 신뢰도 {manager.TrustTokens}", _lightBold);
                GUI.color = new Color(0.76f, 0.12f, 0.32f);
                GUI.Label(new Rect(hdr.x + 385, hdr.y + 9, 120, 24), $"♦ 암시장 {manager.BlackMarketPts}", _lightBold);
            }
            else
            {
                GUI.color = new Color(0.62f, 0.48f, 0.10f);
                GUI.Label(new Rect(hdr.x + 260, hdr.y + 9, 240, 24), "무더기 밀기 → 트럭 환전 → 현장 강화", _inkRight);
            }
            GUI.color = Color.white;

            if (_postTab != PostRoundTab.Receipt)
            {
                if (GUI.Button(new Rect(hdr.xMax - 76, hdr.y + 7, 68, 28), "영수증", _btnStyle))
                    _postTab = PostRoundTab.Receipt;
            }

            // 중앙 컨텐츠 (영수증 / ♦암시장 / ★특전 / $강화)
            if (_postTab == PostRoundTab.Receipt) DrawReceiptSummaryCard(w, h);
            else if (_postTab == PostRoundTab.BlackMarket) DrawBlackMarketPanel(w, h);
            else if (_postTab == PostRoundTab.TrustPerks) DrawTrustPerksPanel(w, h);
            else if (_postTab == PostRoundTab.CashUpgrades) DrawCashSkillTree(w, h, true);

            // 하단 허브 바: 코어 루프 모드에서는 [다시 시작][다음 단계 ▶][$ 강화] 3버튼만
            int btnCount = CoreLoopConfig.CoreLoopOnly ? 3 : 5;
            float barW = Mathf.Min(720f, w - 40f);
            float btnGap = 10f;
            float btnW = Mathf.Floor((barW - btnGap * (btnCount - 1)) / btnCount);
            float startX = Mathf.Round((w - (btnW * btnCount + btnGap * (btnCount - 1))) * 0.5f);
            float btnY = Mathf.Round(h - 62f);

            int bi = 0;
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), "다시 시작", false, new Color(0.28f, 0.32f, 0.38f), () => manager.ReplayRoomKeepUpgrades());
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), $"다음 단계 ▶", false, new Color(0.16f, 0.56f, 0.30f), () => manager.StartNextStage());
            if (!CoreLoopConfig.CoreLoopOnly)
            {
                DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), $"♦ 암시장 ({manager.BlackMarketPts})", _postTab == PostRoundTab.BlackMarket, new Color(0.72f, 0.14f, 0.32f), () => _postTab = PostRoundTab.BlackMarket);
                DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), $"★ 특전 ({manager.TrustTokens})", _postTab == PostRoundTab.TrustPerks, new Color(0.16f, 0.44f, 0.80f), () => _postTab = PostRoundTab.TrustPerks);
            }
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), $"$ 강화", _postTab == PostRoundTab.CashUpgrades, new Color(0.78f, 0.54f, 0.10f), () => _postTab = PostRoundTab.CashUpgrades);
        }

        /// <summary>밝은 배경에는 먹색, 어두운 배경에는 흰색 글자를 골라 대비를 지킨다.</summary>
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

        private void DrawReceiptSummaryCard(float w, float h)
        {
            float rw = 420f, rh = 340f;
            Rect r = new Rect(Mathf.Round((w - rw) * 0.5f), Mathf.Round((h - rh) * 0.5f - 6f), rw, rh);
            DrawReceiptBox(r);

            string title = manager.WasEvicted ? "[강제 퇴거 정산 영수증]"
                         : manager.WasEarlyCheckout ? "[80% 조기 정산 영수증]"
                         : $"[STAGE {manager.CurrentStage} 청소 완료 영수증]";
            Color titleCol = manager.WasEvicted ? new Color(0.78f, 0.12f, 0.12f) : new Color(0.08f, 0.46f, 0.20f);
            GUI.color = titleCol;
            GUI.Label(new Rect(r.x + 16, r.y + 18, rw - 32, 28), title, new GUIStyle(_lightCenter) { fontSize = 18 });
            GUI.color = Color.white;

            GUI.Label(new Rect(r.x + 16, r.y + 50, rw - 32, 20), "------------------------------------------------", _inkCenter);

            float y = r.y + 78f;
            DrawReceiptRow(r, y, "청소 진행률", $"{manager.OverallCleanPercent:0.0}%"); y += 26;
            if (CoreLoopConfig.EnableStreakCompression)
            {
                DrawReceiptRow(r, y, "같은 쓰레기 보너스", $"{manager.TotalCompressionsThisRoom}회"); y += 26;
            }
            if (CoreLoopConfig.EnableTreasureBranch)
            {
                DrawReceiptRow(r, y, "보물 전달 / 슈킹", $"{manager.ReturnedCount}건 / {manager.StolenCount}건"); y += 26;
            }
            DrawReceiptRow(r, y, "방 클리어 보너스", manager.WasEvicted ? "$0 (몰수)" : $"+${manager.LastClearBonus:N0}"); y += 26;
            if (manager.WasEvicted)
            {
                GUI.color = new Color(0.78f, 0.12f, 0.12f);
                DrawReceiptRow(r, y, $"압수 ({manager.LastStolenItemName})", $"-${manager.LastConfiscatedValue:N0}", true); y += 26;
                DrawReceiptRow(r, y, "이번 판 ♦ 암시장 포인트", manager.LastForfeitedBmPts > 0 ? $"♦0 (-{manager.LastForfeitedBmPts} 몰수)" : "♦0 (미적립)", true); y += 26;
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(r.x + 16, y + 2, rw - 32, 20), "------------------------------------------------", _inkCenter);
            y += 28;

            Rect stamp = new Rect(r.x + 28, r.yMax - 54, rw - 56, 32);
            DrawBorderedBox(stamp, new Color(0.92f, 0.96f, 0.92f), new Color(0.18f, 0.58f, 0.30f), 2);
            GUI.color = new Color(0.08f, 0.46f, 0.20f);
            GUI.Label(stamp, "기본 수익 · 토큰 · 모든 강화 100% 유지", _lightCenter);
            GUI.color = Color.white;
        }

        private void DrawBlackMarketPanel(float w, float h)
        {
            float pw = 640f, ph = 370f;
            Rect p = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round((h - ph) * 0.5f - 4f), pw, ph);
            Color accent = new Color(0.86f, 0.22f, 0.40f);
            DrawPaperCard(p, accent, 2);

            GUI.color = new Color(0.62f, 0.12f, 0.28f);
            GUI.Label(new Rect(p.x + 20, p.y + 14, pw - 40, 28), $"♦ 암시장 상점 · 보유 ♦{manager.BlackMarketPts}", _lightBigCenter);
            GUI.color = Color.white;

            float cy = p.y + 58f;
            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 66), "♦ 장물 감정 조작", $"진품(×1.5) 확률 {25 + manager.LvAppraisal * 25}% · 가품 제거",
                manager.LvAppraisal >= 2 ? "MAX" : $"♦{manager.CostAppraisalPts} 구매 ({manager.LvAppraisal}/2)",
                manager.LvAppraisal < 2 && manager.BlackMarketPts >= manager.CostAppraisalPts, accent, () => manager.BuyNode("appraisal"));
            cy += 76f;

            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 66), "✧ 의심도 회피", $"슈킹 시 의심도 증가 무효 확률 {manager.EvasionChance * 100f:0}% → +20%",
                manager.LvEvasion >= 3 ? "MAX" : $"♦{manager.CostEvasionPts} 구매 ({manager.LvEvasion}/3)",
                manager.LvEvasion < 3 && manager.BlackMarketPts >= manager.CostEvasionPts, accent, () => manager.BuyNode("evasion"));
            cy += 76f;

            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 66), "▲ 아드레날린 증폭", "의심 80~95% 스릴 구간 수익 배율 ×2 → ×2.5",
                manager.HasAdrenalineAmp ? "보유 중" : $"♦{manager.CostAdrenalinePts} 구매",
                !manager.HasAdrenalineAmp && manager.BlackMarketPts >= manager.CostAdrenalinePts, accent, () => manager.BuyNode("adrenaline"));
            cy += 76f;

            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 66), "$ 암시장 현금 세탁", "암시장 포인트 1개를 현금 $180으로 즉시 환전 (반복 가능)",
                $"♦{manager.CostLaunderPts} → +$180",
                manager.BlackMarketPts >= manager.CostLaunderPts, new Color(0.95f, 0.72f, 0.22f), () => manager.BuyNode("launder"));
        }

        private void DrawTrustPerksPanel(float w, float h)
        {
            float pw = 640f, ph = 330f;
            Rect p = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round((h - ph) * 0.5f - 4f), pw, ph);
            Color accent = new Color(0.24f, 0.56f, 0.92f);
            DrawPaperCard(p, accent, 2);

            GUI.color = new Color(0.12f, 0.36f, 0.68f);
            GUI.Label(new Rect(p.x + 20, p.y + 14, pw - 40, 28), $"★ 성실 특전 상점 · 보유 ★{manager.TrustTokens}", _lightBigCenter);
            GUI.color = Color.white;

            float cy = p.y + 62f;
            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 74), "★ VIP 고액 의뢰 해금", "전체 청소 단가·클리어 보너스 ×1.5 · 막판 10% 잭팟 ×5",
                manager.HasVipContract ? "보유 중" : $"★{manager.CostVipTokens} 해금",
                !manager.HasVipContract && manager.TrustTokens >= manager.CostVipTokens, accent, () => manager.BuyNode("vip"));
            cy += 86f;

            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 74), "◈ 같은 쓰레기 보너스 가속", "같은 종류 연속 보너스 조건 5개 → 3개 (봉투 비움 + 수익 2배!)",
                manager.HasFastCompressor ? "보유 중" : $"★{manager.CostCompressorTokens} 해금",
                !manager.HasFastCompressor && manager.TrustTokens >= manager.CostCompressorTokens, accent, () => manager.BuyNode("compressor"));
            cy += 86f;

            DrawShopItemCard(new Rect(p.x + 20, cy, pw - 40, 74), "✦ 보물 탐지 감각", "보물 자동 발견 확률 ×1.6 · 고가 희귀 보물 등장률 상승",
                manager.HasTreasureSense ? "보유 중" : $"★{manager.CostTreasureSenseTokens} 해금",
                !manager.HasTreasureSense && manager.TrustTokens >= manager.CostTreasureSenseTokens, accent, () => manager.BuyNode("sense"));
        }

        private void DrawShopItemCard(Rect r, string title, string desc, string btnText, bool canBuy, Color accent, System.Action onBuy)
        {
            DrawPaperCard(r, canBuy ? accent : new Color(0.72f, 0.68f, 0.60f), canBuy ? 2 : 1);
            GUI.color = canBuy ? new Color(0.22f, 0.18f, 0.13f) : new Color(0.44f, 0.41f, 0.36f);
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 180, 24), title, _lightBold);
            GUI.color = new Color(0.35f, 0.31f, 0.25f);
            GUI.Label(new Rect(r.x + 16, r.y + 36, r.width - 180, 22), $"• {desc}", _lightSmall);
            GUI.color = Color.white;

            Rect br = new Rect(r.xMax - 148, r.y + (r.height - 36f) * 0.5f, 134, 36);
            DrawBorderedBox(br, canBuy ? accent : new Color(0.82f, 0.79f, 0.72f), canBuy ? new Color(0.26f, 0.20f, 0.14f) : new Color(0.62f, 0.58f, 0.52f), 1);
            if (canBuy)
            {
                GUI.color = ReadableOn(accent);
                GUI.Label(br, btnText, _lightCenter);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(0.48f, 0.44f, 0.38f);
                GUI.Label(br, btnText, _lightCenter);
                GUI.color = Color.white;
            }
            if (canBuy && GUI.Button(br, GUIContent.none, GUIStyle.none)) onBuy?.Invoke();
        }

        private void DrawSettingsMenu(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);
            Rect r = new Rect(Mathf.Round((w - 360) * 0.5f), Mathf.Round((h - 240) * 0.5f), 360, 240);
            DrawReceiptBox(r);

            GUI.Label(new Rect(r.x + 16, r.y + 16, 328, 26), "환경 설정 [ESC]", _inkCenter);

            GUI.Label(new Rect(r.x + 22, r.y + 56, 200, 24), $"마우스 감도  {manager.MouseSensitivity:0.00}", _inkBold);
            float ns = GUI.HorizontalSlider(new Rect(r.x + 22, r.y + 84, 316, 18), manager.MouseSensitivity, 0.03f, 0.45f);
            if (!Mathf.Approximately(ns, manager.MouseSensitivity)) manager.SetMouseSensitivity(ns);

            GUI.Label(new Rect(r.x + 22, r.y + 116, 200, 24), $"전체 볼륨  {manager.MasterVolume * 100f:0}%", _inkBold);
            float nv = GUI.HorizontalSlider(new Rect(r.x + 22, r.y + 144, 316, 18), manager.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(nv, manager.MasterVolume)) manager.SetMasterVolume(nv);

            if (GUI.Button(new Rect(r.x + 100, r.y + 186, 160, 36), "계속하기 [ESC]", _btnStyle))
                manager.ToggleSettingsMenu();
        }

        private void DrawReceiptRow(Rect r, float y, string left, string right, bool lightColorOverride = false)
        {
            GUI.Label(new Rect(r.x + 24, y, r.width * 0.58f, 24), left, lightColorOverride ? _lightLeft : _inkLeft);
            GUI.Label(new Rect(r.x + r.width * 0.42f, y, r.width * 0.58f - 24f, 24), right, lightColorOverride ? _lightRight : _inkRight);
        }

        private void DrawReceiptBox(Rect r)
        {
            DrawRect(new Rect(r.x + 2, r.y + 4, r.width, r.height), new Color(0, 0, 0, 0.16f));
            DrawBorderedBox(r, new Color(0.972f, 0.952f, 0.905f, 0.97f), new Color(0.335f, 0.285f, 0.220f), 2);
            DrawRect(new Rect(r.x + 2, r.y + 2, r.width - 4, 2), new Color(0.855f, 0.815f, 0.735f));
        }

        /// <summary>밝은 크림색 종이 카드 + 얇은 테두리 + 부드러운 그림자 (인게임 HUD 공통 톤)</summary>
        private void DrawPaperCard(Rect r, Color border, int bw = 1)
        {
            DrawRect(new Rect(r.x + 2, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.18f));
            DrawBorderedBox(r, new Color(0.966f, 0.947f, 0.902f, 0.955f), border, bw);
        }

        private void DrawPostItBox(Rect r)
        {
            DrawRect(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.22f));
            DrawBorderedBox(r, new Color(0.995f, 0.912f, 0.545f, 0.98f), new Color(0.775f, 0.635f, 0.215f), 1);
            DrawRect(new Rect(r.x + r.width * 0.36f, r.y - 3, r.width * 0.28f, 7), new Color(0.93f, 0.875f, 0.655f, 0.85f));
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
