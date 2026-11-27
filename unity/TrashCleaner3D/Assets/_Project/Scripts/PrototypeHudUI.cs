using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// TrashCleaner3D v1.4 UI:
    /// - WebGL 번들 한글 폰트(Resources/Fonts/KRFont) + 데스크톱 맑은 고딕 자동 폴백
    /// - 통합 강화소([E]) 내부 28노드 4방향 나뭇가지 분할 강화 트리 (흡입속도 I/II/III, 빗자루, 진공기, 리어카, 다마스, 1톤 트럭)
    /// - 차량 적재량 게이지 / 4초 비움 대기 바 / 다마스·트럭 마당 자동흡입 배지
    /// - Stage 2 4방(거실·주방·안방·화장실) 실시간 클리어 체크리스트 + Before→After 점등 플래시
    /// - WebGL 포인터 잠금 클릭 안내 + [ESC] 설정(Stage 1/2 빠른 전환 포함)
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
            public string id, shortName, fullTitle, statDesc, unlockHint, parentId;
            public Vector2 offset;
            public int level, maxLevel, cost;
            public bool unlocked, affordable, isMilestone;
            public Color accent;
        }

        private void Awake()
        {
            _whiteTex = MakeTex(Color.white);
            _dimTex = MakeTex(new Color(0.075f, 0.065f, 0.055f, 0.86f));
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
            else if (manager.Phase == GamePhase.MindMapUpgrade) DrawUpgradeKioskTree(w, h, false);
            else if (manager.Phase == GamePhase.SettingsMenu) DrawSettingsMenu(w, h);
            else if (manager.Phase == GamePhase.RoomCompleted) DrawPostRoundHub(w, h);
        }

        private void DrawScreenOverlays(float w, float h)
        {
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

            if (manager.FreshAirFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(0.65f, 0.92f, 1f, manager.FreshAirFlash01 * 0.12f));
            if (manager.RoomClearFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(1.0f, 0.98f, 0.84f, manager.RoomClearFlash01 * 0.26f));
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
            float topY = Mathf.Round(Mathf.Max(28f, h * 0.045f));
            float botSafe = Mathf.Round(Mathf.Max(42f, h * 0.068f));
            float sideX = Mathf.Round(Mathf.Max(26f, w * 0.036f));

            // ===== 1. 좌상단: 보유 현금 배지 + 진행 영수증 카드 =====
            float cardW = 272f;
            float badgeH = 44f;
            Rect badge = new Rect(sideX, topY, 224f, badgeH);
            DrawRect(new Rect(badge.x + 2, badge.y + 4, badge.width, badge.height), new Color(0, 0, 0, 0.28f));
            DrawBorderedBox(badge, new Color(0.043f, 0.243f, 0.149f, 0.97f), new Color(0.98f, 0.845f, 0.35f), 3);

            int shownMoney = Mathf.RoundToInt(manager.DisplayMoney);
            if (manager.CollectingPendingValue > 0)
            {
                GUI.Label(new Rect(badge.x + 12, badge.y + 1, 200, 28), $"${shownMoney:N0}", _moneyBadgeStyle);
                GUI.Label(new Rect(badge.x + 12, badge.y + 24, 200, 18), $"대기 +${manager.CollectingPendingValue:N0}", _moneyPendingStyle);
            }
            else
            {
                GUI.Label(new Rect(badge.x + 12, badge.y + 5, 200, 34), $"${shownMoney:N0}", _moneyBadgeStyle);
            }

            bool showExtraRow = manager.ContinuousFever01 > 0.05f || manager.CanEarlyCheckout;
            float cardH = showExtraRow ? 88f : 68f;
            float cardTop = topY + badgeH + 8f;
            Rect tl = new Rect(sideX, cardTop, cardW, cardH);
            DrawReceiptBox(tl);

            var stInfo = CampaignProgressStore.GetInfo(manager.CurrentStage);
            string stageLabel = $"ST.{manager.CurrentStage} {stInfo.shortName}";
            GUI.Label(new Rect(tl.x + 10, tl.y + 4, 185, 22), stageLabel, _inkBold);
            GUI.Label(new Rect(tl.x + cardW - 66, tl.y + 4, 56, 22), $"{manager.OverallCleanPercent:0}%", _inkRight);

            Rect progBg = new Rect(tl.x + 10, tl.y + 28, cardW - 20, 11);
            DrawRect(progBg, new Color(0.80f, 0.77f, 0.70f));
            Color progFill = manager.Phase == GamePhase.Phase2_FloorMopping ? new Color(0.22f, 0.62f, 0.90f) : new Color(0.18f, 0.68f, 0.34f);
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * (manager.OverallCleanPercent / 100f), progBg.height), progFill);
            DrawRect(new Rect(progBg.x + progBg.width * 0.80f, progBg.y - 2, 2, progBg.height + 4), new Color(0.75f, 0.20f, 0.18f));

            string toolTag = manager.Phase == GamePhase.Phase2_FloorMopping
                ? (manager.HasMopUnlocked ? "T자 대걸레" : "물티슈")
                : manager.CurrentToolName;
            GUI.Label(new Rect(tl.x + 10, tl.y + 42, cardW - 20, 22), toolTag, _inkSmall);

            if (showExtraRow)
            {
                float ry = tl.y + 62f;
                if (manager.CanEarlyCheckout)
                {
                    Rect stampR = new Rect(tl.x + 10, ry, 122, 22);
                    DrawBorderedBox(stampR, new Color(0.98f, 0.91f, 0.90f), new Color(0.78f, 0.18f, 0.18f), 2);
                    GUI.color = new Color(0.78f, 0.14f, 0.14f);
                    GUI.Label(stampR, "[K] 조기 정산", _lightCenter);
                    GUI.color = Color.white;
                }
                else if (manager.ContinuousFever01 > 0.05f)
                {
                    Rect fBg = new Rect(tl.x + 10, ry + 2, 130, 18);
                    DrawRect(fBg, new Color(0.78f, 0.75f, 0.68f));
                    Color fCol = manager.IsFeverHot ? new Color(0.96f, 0.45f, 0.12f) : new Color(0.95f, 0.74f, 0.18f);
                    DrawRect(new Rect(fBg.x, fBg.y, fBg.width * manager.ContinuousFever01, fBg.height), fCol);
                    GUI.Label(new Rect(fBg.x + 6, fBg.y, 118, 18), $"FEVER {manager.ContinuousFever01 * 100f:0}%", _inkSmall);
                }
            }

            // ===== 1.5 Stage 2 방별(거실·주방·안방·화장실) 실시간 청소 체크리스트 =====
            if (manager.trashSwarm != null && manager.trashSwarm.RoomCount > 1)
            {
                int rc = manager.trashSwarm.RoomCount;
                float listY = tl.yMax + 8f;
                float listH = 26f + rc * 20f;
                Rect rmBox = new Rect(sideX, listY, cardW, listH);
                DrawPaperCard(rmBox, new Color(0.68f, 0.62f, 0.50f), 1);
                GUI.Label(new Rect(rmBox.x + 10, rmBox.y + 4, cardW - 20, 20),
                    $"방별 청소 ({manager.trashSwarm.ClearedRoomCount}/{rc})", _inkBold);

                for (int i = 0; i < rc; i++)
                {
                    bool done = manager.trashSwarm.IsRoomCleared(i);
                    int rem = manager.trashSwarm.GetRoomRemaining(i);
                    string rName = manager.trashSwarm.GetRoomName(i);
                    float y = rmBox.y + 24f + i * 20f;
                    GUI.color = done ? new Color(0.12f, 0.52f, 0.24f) : new Color(0.24f, 0.20f, 0.15f);
                    GUI.Label(new Rect(rmBox.x + 12, y, 120, 18), done ? $"✓ {rName}" : $"○ {rName}", _lightSmall);
                    GUI.Label(new Rect(rmBox.x + cardW - 110, y, 98, 18), done ? "완료!" : $"남음 {rem:N0}", _lightRight);
                    GUI.color = Color.white;
                }
            }

            // ===== 2. 상단 중앙: 악취 게이지 =====
            float barW = Mathf.Round(Mathf.Min(380f, w * 0.40f));
            float barX = Mathf.Round((w - barW) * 0.5f);
            Rect tc = new Rect(barX, topY, barW, 44f);
            DrawPaperCard(tc, new Color(0.715f, 0.650f, 0.535f), 1);

            int st = manager.StenchStage;
            Color stCol = manager.IsOutsideVentilating ? new Color(0.38f, 0.84f, 1f) :
                st == 0 ? new Color(0.40f, 0.88f, 0.48f) :
                st == 1 ? new Color(0.95f, 0.85f, 0.25f) :
                st == 2 ? new Color(0.98f, 0.56f, 0.18f) :
                st == 3 ? new Color(0.95f, 0.24f, 0.22f) : new Color(0.82f, 0.28f, 0.96f);

            Color stInk = Color.Lerp(stCol, new Color(0.26f, 0.19f, 0.12f), 0.58f);
            GUI.color = stInk;
            GUI.Label(new Rect(tc.x + 10, tc.y + 10, 54, 22), "☣ 악취", _lightSmall);
            GUI.color = Color.white;

            float cellStartX = tc.x + 68f;
            float trackW = barW - 156f;
            float cellW = (trackW - 9f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect cell = new Rect(cellStartX + i * (cellW + 3f), tc.y + 15, cellW, 13);
                DrawRect(cell, new Color(0.855f, 0.820f, 0.745f));
                float seg = Mathf.Clamp01((manager.StenchPercent - i * 25f) / 25f);
                if (seg > 0.001f) DrawRect(new Rect(cell.x, cell.y, cell.width * seg, cell.height), stCol);
            }
            string stTag = manager.IsOutsideVentilating ? "환기 중" :
                st == 4 ? "시야 흐림!" : st == 3 ? "속도↓↓" : st == 2 ? "속도↓" : st == 1 ? "답답함" : "쾌적";
            GUI.color = stInk;
            GUI.Label(new Rect(cellStartX + trackW + 8, tc.y + 10, 76, 22), stTag, _lightSmall);
            GUI.color = Color.white;

            // ===== 3. 우상단: 의뢰 메모 포스트잇 =====
            bool hintMin = controller != null && controller.ShowHelp;
            Rect postItRect = hintMin
                ? new Rect(w - sideX - 134, topY, 134, 28)
                : new Rect(w - sideX - 268, topY, 268, 76);
            DrawPostItBox(postItRect);
            if (hintMin)
            {
                GUI.Label(postItRect, "■ 의뢰 메모 [H]", _inkCenter);
            }
            else
            {
                // [버그 수정 v1.5.1] 14px 볼드 한 줄로 쓰면 "[ESC]"가 노란 메모 박스 오른쪽으로 삐져나왔다.
                //  → 제목(좌) / 단축키(우) 두 조각으로 나누고, 단축키는 11px 전용 스타일로 줄여 박스 안에 맞췄다.
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 6, 128, 20), "■ 의뢰 메모 [H]", _inkBold);
                GUI.Label(new Rect(postItRect.x + 136, postItRect.y + 7, postItRect.width - 146, 18), "[M] 맵 · [ESC] 설정", _inkTinyRight);
                GUI.Label(new Rect(postItRect.x + 10, postItRect.y + 28, postItRect.width - 20, 44), manager.CurrentClientHint, _inkWrap);
            }

            // ===== 4. 중앙 하단: 수거 차량 적재 칩 + 수거통 게이지 =====
            DrawBottomBinAndVehicleBar(w, h, botSafe);

            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect toastR = new Rect(Mathf.Round(w * 0.5f - 210), h - botSafe - 132, 420, 30);
                DrawPaperCard(toastR, new Color(0.82f, 0.66f, 0.22f), 2);
                GUI.color = new Color(0.46f, 0.30f, 0.04f);
                GUI.Label(toastR, manager.ToastMessage, _lightCenter);
                GUI.color = Color.white;
            }
        }

        private void DrawBottomBinAndVehicleBar(float w, float h, float botSafe)
        {
            float bw = 356f;
            float bx = Mathf.Round((w - bw) * 0.5f);
            float by = Mathf.Round(h - botSafe - 48f);

            // 1) 수거 차량 상태 칩 (적재 한계 / 비움 대기 / 자동흡입 / 대량 정산 배율)
            Rect vehR = new Rect(bx, by - 32f, bw, 26f);
            if (manager.IsVehicleDispatching)
            {
                DrawPaperCard(vehR, new Color(0.88f, 0.45f, 0.14f), 2);
                float ratio = 1f - Mathf.Clamp01(manager.VehicleDispatchTimer / GamePrototypeManager.VehicleDispatchDuration);
                DrawRect(new Rect(vehR.x + 2, vehR.y + 2, (vehR.width - 4) * ratio, vehR.height - 4), new Color(0.98f, 0.78f, 0.28f, 0.45f));
                GUI.color = new Color(0.62f, 0.24f, 0.04f);
                GUI.Label(vehR, $"▶ {manager.CurrentVehicleName} 비우는 중... ({manager.VehicleDispatchTimer:0.0}초)", _lightCenter);
                GUI.color = Color.white;
            }
            else if (manager.VehicleHasUnlimitedLoad)
            {
                DrawPaperCard(vehR, new Color(0.22f, 0.64f, 0.36f), 2);
                GUI.color = new Color(0.10f, 0.42f, 0.20f);
                GUI.Label(vehR, $"★ 1톤 트럭 · 적재 무제한 · 마당 자동흡입 · 대량 ×{manager.VehicleMaxBulkMult:0.00}", _lightCenter);
                GUI.color = Color.white;
            }
            else
            {
                float loadRatio = Mathf.Clamp01(manager.VehicleCurrentLoad / (float)Mathf.Max(1, manager.VehicleMaxLoad));
                Color vBd = loadRatio >= 0.85f ? new Color(0.86f, 0.36f, 0.18f)
                          : manager.HasAutoVehicleSuction ? new Color(0.24f, 0.58f, 0.86f)
                          : new Color(0.68f, 0.62f, 0.52f);
                DrawPaperCard(vehR, vBd, 1);
                DrawRect(new Rect(vehR.x + 2, vehR.y + vehR.height - 5, (vehR.width - 4) * loadRatio, 3),
                    loadRatio >= 0.85f ? new Color(0.90f, 0.35f, 0.20f) : new Color(0.32f, 0.66f, 0.48f));
                string extra = manager.HasAutoVehicleSuction
                    ? $" · 자동흡입 · ×{manager.VehicleMaxBulkMult:0.00}"
                    : (manager.VehicleVisualStage >= 1 ? $" · 대량 ×{manager.VehicleMaxBulkMult:0.00}" : "");
                GUI.color = new Color(0.24f, 0.20f, 0.15f);
                GUI.Label(vehR, $"■ {manager.CurrentVehicleName} 적재 {manager.VehicleCurrentLoad}/{manager.VehicleMaxLoad}{extra}", _lightCenter);
                GUI.color = Color.white;
            }

            // 2) 수거통 게이지
            bool inZone = controller != null && controller.SettlementZoneActive;
            bool full = manager.IsBinFull;
            Color bd = inZone ? new Color(0.96f, 0.78f, 0.22f) : full ? new Color(0.86f, 0.42f, 0.24f) : new Color(0.66f, 0.61f, 0.52f);
            Rect card = new Rect(bx, by, bw, 46);
            DrawPaperCard(card, bd, (inZone || full) ? 2 : 1);

            GUI.color = inZone ? new Color(0.52f, 0.34f, 0.02f) : full ? new Color(0.62f, 0.20f, 0.10f) : new Color(0.28f, 0.245f, 0.20f);
            GUI.Label(new Rect(card.x + 12, card.y + 5, 160, 22), $"▣ 수거통 {manager.CollectingCount} / {manager.BinCapacity}", _lightSmall);
            string rightStr = manager.CollectingCount > 0
                ? (inZone
                    ? $"[RMB] 정산 · ${manager.CollectingPendingValue:N0}"
                    : manager.HasAutoVehicleSuction
                        ? $"${manager.CollectingPendingValue:N0} · 마당 자동정산"
                        : $"${manager.CollectingPendingValue:N0} · 밖에서 정산")
                : "비어 있음";
            GUI.Label(new Rect(card.x + bw - 205, card.y + 5, 193, 22), rightStr, _lightRight);
            GUI.color = Color.white;

            Rect track = new Rect(card.x + 12, card.y + 30, bw - 24, 9);
            DrawRect(track, new Color(0.855f, 0.820f, 0.745f));
            Color fillC = full ? new Color(0.88f, 0.40f, 0.22f) : new Color(0.34f, 0.66f, 0.80f);
            DrawRect(new Rect(track.x, track.y, track.width * manager.BinFill01, track.height), fillC);
        }

        private void DrawCrosshairAndPrompt(float w, float h)
        {
            if (controller == null) return;
            float cx = Mathf.Round(w * 0.5f), cy = Mathf.Round(h * 0.5f);

            // [버그 수정 v1.5.1] [Tab]으로 커서를 풀어 둔 'UI 커서 모드' 안내 (하단 스테이지 버튼 클릭용)
            if (GamePrototypeManager.UiCursorFree)
            {
                Rect uiR = new Rect(cx - 210, cy - 22, 420, 44);
                DrawPaperCard(uiR, new Color(0.24f, 0.52f, 0.80f), 2);
                GUI.color = new Color(0.10f, 0.24f, 0.44f);
                GUI.Label(uiR, "커서 모드 · 하단 스테이지 버튼 클릭 · [Tab] 1인칭 복귀", _lightCenter);
                GUI.color = Color.white;
                return;
            }

            // WebGL 포인터 락 클릭 안내
            if (Application.platform == RuntimePlatform.WebGLPlayer && Cursor.lockState != CursorLockMode.Locked)
            {
                Rect lockR = new Rect(cx - 175, cy - 22, 350, 44);
                DrawPaperCard(lockR, new Color(0.76f, 0.52f, 0.14f), 2);
                GUI.Label(lockR, "화면을 클릭하여 1인칭 시점 시작", _inkCenter);
                return;
            }

            bool hasTarget = controller.AimDoor != null ||
                             controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain;
            Color dotCol = hasTarget ? new Color(0.24f, 0.58f, 0.36f) : new Color(0.20f, 0.17f, 0.13f, 0.92f);
            float outer = hasTarget ? 9f : 7f;
            DrawRect(new Rect(cx - outer * 0.5f, cy - outer * 0.5f, outer, outer), new Color(1f, 0.985f, 0.95f, 0.78f));
            float sz = hasTarget ? 5f : 3f;
            DrawRect(new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz), dotCol);

            // 큰 봉투 체력바
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

            // 바닥 쓰레기 HOLD 차징 바
            if (!controller.AimIsPile && controller.AimProgress01 > 0.01f && controller.AimProgress01 < 0.999f)
            {
                Rect pb = new Rect(cx - 34, cy + 14, 68, 7);
                DrawPaperCard(pb, new Color(0.55f, 0.47f, 0.34f), 1);
                DrawRect(new Rect(pb.x + 1, pb.y + 1, (pb.width - 2) * controller.AimProgress01, pb.height - 2), new Color(0.92f, 0.74f, 0.18f));
            }

            string prompt = null;
            if (controller.SettlementZoneActive && manager.CollectingCount > 0)
            {
                prompt = manager.IsVehicleDispatching
                    ? $"{manager.CurrentVehicleName} 비우는 중 ({manager.VehicleDispatchTimer:0.0}초)"
                    : $"[RMB] {manager.CollectingCount}개 낱개 투입 · ${manager.CollectingPendingValue:N0}";
            }
            else if (controller.SettlementZoneActive && manager.CollectingCount <= 0)
            {
                prompt = $"{manager.CurrentVehicleName} 앞 · 수거통 비어 있음";
            }
            else if (controller.AimDoor != null)
            {
                prompt = controller.AimDoor.IsOpen ? "[F] 현관문 열림" : "[F] 현관문 열기";
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
                prompt = manager.HasAutoVehicleSuction
                    ? "수거통 가득 · 문 밖 마당에서 자동 정산!"
                    : "수거통 가득 · 밖 수거함에서 [RMB] 정산";
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Rect pr = new Rect(cx - 165, cy + 28, 330, 28);
                DrawPaperCard(pr, new Color(0.52f, 0.44f, 0.32f), 1);
                GUI.color = new Color(0.26f, 0.21f, 0.15f);
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

        // ===== v1.4 통합 강화소([E]) · 28노드 4방향 나뭇가지 분할 강화 트리 =====
        private void DrawUpgradeKioskTree(float w, float h, bool embeddedInHub)
        {
            if (!embeddedInHub) GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topPad = embeddedInHub ? 80f : 22f;
            float botPad = embeddedInHub ? 74f : 22f;
            float pw = Mathf.Min(w - 32f, 1140f);
            float ph = Mathf.Min(h - topPad - botPad, 660f);
            Rect panel = new Rect(Mathf.Round((w - pw) * 0.5f), Mathf.Round(topPad + (h - topPad - botPad - ph) * 0.5f), pw, ph);

            DrawBorderedBox(panel, new Color(0.10f, 0.088f, 0.072f, 0.975f), new Color(0.86f, 0.72f, 0.30f), 3);

            // 헤더
            Rect hdr = new Rect(panel.x + 14, panel.y + 12, panel.width - 28, 48);
            DrawReceiptBox(hdr);
            GUI.color = new Color(0.58f, 0.40f, 0.04f);
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 5, 420, 24), "통합 강화소 · 4방향 나뭇가지 강화 트리", _kioskHeaderStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 27, 460, 18), "마우스로 상세 확인 · 좌클릭 즉시 강화 · [E] / [ESC] 닫기", _inkSmall);

            Rect cash = new Rect(hdr.xMax - 204, hdr.y + 6, 192, 36);
            DrawBorderedBox(cash, new Color(0.043f, 0.243f, 0.149f), new Color(0.98f, 0.845f, 0.35f), 2);
            GUI.Label(new Rect(cash.x + 10, cash.y + 4, 172, 28), $"${manager.Money:N0}", _moneyBadgeStyle);

            // 트리 영역 계산 (좌표: X -520..+520, Y -165..+165)
            float treeTop = hdr.yMax + 10f;
            float infoH = 54f;
            float treeBottom = panel.yMax - infoH - 18f;
            float treeH = Mathf.Max(220f, treeBottom - treeTop);
            float treeW = panel.width - 28f;

            float scale = Mathf.Clamp(Mathf.Min(treeW / 1060f, treeH / 360f), 0.62f, 1.0f);
            Vector2 center = new Vector2(Mathf.Round(panel.x + panel.width * 0.5f), Mathf.Round(treeTop + treeH * 0.5f));

            Color cTool = new Color(0.96f, 0.78f, 0.24f);  // 좌상: 흡입·도구
            Color cVeh  = new Color(0.32f, 0.78f, 0.98f);  // 우상: 수거통·차량·단가
            Color cSurv = new Color(0.36f, 0.90f, 0.54f);  // 좌하: 이동·방독·걸레
            Color cAuto = new Color(0.98f, 0.56f, 0.24f);  // 우하: 주먹·로봇청소기

            // 4개 구역 라벨
            GUI.color = new Color(cTool.r, cTool.g, cTool.b, 0.75f);
            GUI.Label(new Rect(panel.x + 22, treeTop + 2, 180, 18), "▲ 흡입 · 도구 진화", _lightSmall);
            GUI.color = new Color(cVeh.r, cVeh.g, cVeh.b, 0.75f);
            GUI.Label(new Rect(panel.xMax - 202, treeTop + 2, 180, 18), "▣ 수거통 · 차량 · 수익", _lightRight);
            GUI.color = new Color(cSurv.r, cSurv.g, cSurv.b, 0.75f);
            GUI.Label(new Rect(panel.x + 22, treeBottom - 20, 180, 18), "☣ 이동 · 생존 · 걸레", _lightSmall);
            GUI.color = new Color(cAuto.r, cAuto.g, cAuto.b, 0.75f);
            GUI.Label(new Rect(panel.xMax - 202, treeBottom - 20, 180, 18), "❖ 파괴 · 로봇 자동화", _lightRight);
            GUI.color = Color.white;

            var nodes = new[]
            {
                // ===== 가지 1: 흡입 · 도구 (8노드) =====
                BuildNode("hold1",  "흡입속도 I",  "흡입 속도 I (기초)",     $"현재 홀드 {manager.EffectiveHoldDuration:0.00}초 · 1티어 장갑 가속", "시작 노드", "", new Vector2(-96, -54), false, cTool),
                BuildNode("rad1",   "흡입반경 I",  "흡입 반경 I (기초)",     $"현재 반경 {manager.SweepRadius:0.00}m · 동시 {manager.AutoGrabPerTick}개", "선행: 흡입속도 I Lv.1", "hold1", new Vector2(-190, -54), false, cTool),
                BuildNode("broom",  "★ 빗자루",   "2티어 도구: 빗자루 해금", "반경 대폭 확장 · 동시 3개+ 쓸어담기 · 홀드 14% 단축", "선행: 속도 I Lv.2 & 반경 I Lv.1", "rad1", new Vector2(-284, -54), true, cTool),
                BuildNode("hold2",  "흡입속도 II", "흡입 속도 II (중급)",    $"현재 홀드 {manager.EffectiveHoldDuration:0.00}초 · 빗자루 고속화", "선행: 빗자루 해금", "broom", new Vector2(-378, -54), false, cTool),
                BuildNode("rad2",   "흡입반경 II", "흡입 반경 II (중급)",    $"현재 반경 {manager.SweepRadius:0.00}m · 동시 {manager.AutoGrabPerTick}개", "선행: 빗자루 해금", "broom", new Vector2(-284, -124), false, cTool),
                BuildNode("fever",  "피버 엔진",   "연속 흡입 피버 엔진",    $"연속 흡입 시 범위·속도·수익 증폭 (Lv.{manager.LvFever}/3)", "선행: 흡입속도 II Lv.1", "hold2", new Vector2(-378, -124), false, cTool),
                BuildNode("vacuum", "★ 진공기",   "3티어 도구: 진공청소기",  "광역 8개+ 고속 흡입 · 수거통 용량 ×2!", "선행: 속도 II Lv.3 & 반경 II Lv.2", "hold2", new Vector2(-472, -54), true, cTool),
                BuildNode("hold3",  "흡입속도 III","흡입 속도 III (마스터)", $"현재 홀드 {manager.EffectiveHoldDuration:0.00}초 · 최속 0.05초대!", "선행: 진공청소기 해금", "vacuum", new Vector2(-472, -124), false, cTool),

                // ===== 가지 2: 수거통 · 차량 · 수익 (9노드) =====
                BuildNode("bin1",    "수거통 I",   "수거통 용량 I (기초)",   $"현재 최대 {manager.BinCapacity}개 휴대", "시작 노드", "", new Vector2(96, -54), false, cVeh),
                BuildNode("price1",  "정산단가 I", "분리수거 단가 I",        $"쓰레기 수익 배율 ×{manager.EffectiveTrashPriceMult:0.00}", "선행: 수거통 I Lv.1", "bin1", new Vector2(96, -124), false, cVeh),
                BuildNode("combo",   "정산콤보",   "연속 정산 콤보 유지",    $"연속 정산 시 +20%/스택 · 유지 {manager.MaxComboWindow:0.0}초", "선행: 정산단가 I Lv.2", "price1", new Vector2(190, -124), false, cVeh),
                BuildNode("cart",    "★ 리어카",   "차량 1단계: 리어카",     "차량 적재 120→350 · 대량 정산 보너스 최대 ×1.20!", "선행: 수거통 I Lv.2", "bin1", new Vector2(190, -54), true, cVeh),
                BuildNode("bin2",    "수거통 II",  "수거통 용량 II (중급)",  $"현재 최대 {manager.BinCapacity}개 휴대", "선행: 리어카 해금", "cart", new Vector2(284, -54), false, cVeh),
                BuildNode("price2",  "정산단가 II","분리수거 단가 II",       $"쓰레기 수익 배율 ×{manager.EffectiveTrashPriceMult:0.00}", "선행: 리어카 & 단가 I Lv.2", "cart", new Vector2(284, -124), false, cVeh),
                BuildNode("damas",   "★ 다마스",   "차량 2단계: 다마스",     "적재 900 · 대량 ×1.45 · 문 밖 마당 진입 시 원거리 자동흡입 정산!", "선행: 수거통 II Lv.2 & 단가 II Lv.1", "bin2", new Vector2(378, -54), true, cVeh),
                BuildNode("bin3",    "수거통 III", "수거통 용량 III (대형)", $"현재 최대 {manager.BinCapacity}개 휴대", "선행: 다마스 해금", "damas", new Vector2(378, -124), false, cVeh),
                BuildNode("truck1t", "★ 1톤트럭",  "차량 3단계: 1톤 트럭",   "적재 무제한(비움 대기 0초) · 대량 ×1.80 · 마당 자동흡입!", "선행: 다마스 & 수거통 III Lv.2", "damas", new Vector2(472, -54), true, cVeh),

                // ===== 가지 3: 기동 · 생존 · 걸레 (6노드) =====
                BuildNode("speed1", "이동속도 I", "운동화 I (기동력)",       $"이동속도 +{manager.TotalSpeedLv * 10}%", "시작 노드", "", new Vector2(-96, 54), false, cSurv),
                BuildNode("speed2", "이동속도 II","운동화 II (스프린트)",    $"이동속도 +{manager.TotalSpeedLv * 10}% (왕복 단축)", "선행: 이동속도 I Lv.3", "speed1", new Vector2(-190, 54), false, cSurv),
                BuildNode("mask1",  "방독면 I",   "방독면 필터 I",           $"악취 상승 -{Mathf.Min(70, manager.TotalMaskLv * 10)}%", "선행: 이동속도 I Lv.1", "speed1", new Vector2(-96, 124), false, cSurv),
                BuildNode("mask2",  "방독면 II",  "방독면 필터 II (고급)",   $"악취 상승 -{Mathf.Min(70, manager.TotalMaskLv * 10)}%", "선행: 방독면 I Lv.3", "mask1", new Vector2(-190, 124), false, cSurv),
                BuildNode("mop",    "★ 대걸레",   "T자 대걸레 해금",         "2단계 바닥 얼룩 닦기 속도 ×2.4!", "선행: 방독면 I Lv.1", "mask1", new Vector2(-284, 124), true, cSurv),
                BuildNode("scrub",  "강력 세제",  "강력 바닥 세제",          $"현재 걸레질 배율 ×{manager.ScrubSpeedMult:0.00}", "선행: T자 대걸레 해금", "mop", new Vector2(-378, 124), false, cSurv),

                // ===== 가지 4: 파괴 · 자동화 (5노드) =====
                BuildNode("fist1",  "주먹 힘 I",  "큰 봉투 파괴력 I",        $"큰 봉투/박스 타격 {manager.FistDamage:0.0}", "시작 노드", "", new Vector2(96, 54), false, cAuto),
                BuildNode("fist2",  "주먹 힘 II", "큰 봉투 파괴력 II",       $"큰 봉투/박스 타격 {manager.FistDamage:0.0}", "선행: 주먹 힘 I Lv.3", "fist1", new Vector2(190, 54), false, cAuto),
                BuildNode("roomba", "★ 로봇청소", "로봇청소기 투입 (최대 3대)", manager.RoombaCount == 0 ? "구매 시 방 안 자동 수거 로봇 배치!" : $"현재 {manager.RoombaCount}대 가동 중 ({manager.RoombaEatInterval:0.00}초/개)", "선행: 주먹 힘 I Lv.2", "fist1", new Vector2(96, 124), true, cAuto),
                BuildNode("rspd1",  "로봇속도 I", "로봇청소기 가속 I",       $"로봇 수거 주기 {manager.RoombaEatInterval:0.00}초", "선행: 로봇청소기 1대", "roomba", new Vector2(190, 124), false, cAuto),
                BuildNode("rspd2",  "로봇속도 II","로봇청소기 가속 II",      $"로봇 수거 주기 {manager.RoombaEatInterval:0.00}초", "선행: 로봇 2대 & 속도 I Lv.2", "rspd1", new Vector2(284, 124), false, cAuto),
            };

            // 연결선 그리기
            foreach (var n in nodes)
            {
                Vector2 pPos = center;
                if (!string.IsNullOrEmpty(n.parentId))
                {
                    foreach (var p in nodes) if (p.id == n.parentId) { pPos = center + p.offset * scale; break; }
                }
                Vector2 nPos = center + n.offset * scale;
                Color lineCol = n.unlocked
                    ? new Color(n.accent.r, n.accent.g, n.accent.b, 0.72f)
                    : new Color(0.38f, 0.35f, 0.30f, 0.42f);
                DrawLine(pPos, nPos, lineCol, n.unlocked ? 2.5f : 1.5f);
            }

            // 중앙 코어 노드
            float coreSz = Mathf.Round(56f * scale);
            Rect coreR = new Rect(center.x - coreSz * 0.5f, center.y - coreSz * 0.5f, coreSz, coreSz);
            DrawBorderedBox(coreR, new Color(0.24f, 0.195f, 0.135f), cTool, 2);
            GUI.color = cTool;
            GUI.Label(coreR, "$ 강화", _lightCenter);
            GUI.color = Color.white;

            // 28개 노드 카드 렌더링
            TreeNodeDef? hovered = null;
            float nW = Mathf.Round(84f * scale);
            float nH = Mathf.Round(52f * scale);
            Vector2 mPos = Event.current.mousePosition;

            foreach (var n in nodes)
            {
                Vector2 p = center + n.offset * scale;
                Rect r = new Rect(Mathf.Round(p.x - nW * 0.5f), Mathf.Round(p.y - nH * 0.5f), nW, nH);
                bool isMax = n.level >= n.maxLevel;

                Color bg = !n.unlocked ? new Color(0.15f, 0.14f, 0.125f, 0.94f)
                         : isMax ? new Color(0.14f, 0.28f, 0.18f, 0.98f)
                         : n.affordable ? (n.isMilestone ? new Color(0.36f, 0.27f, 0.10f, 0.98f) : new Color(0.29f, 0.24f, 0.145f, 0.97f))
                         : new Color(0.19f, 0.175f, 0.155f, 0.96f);

                Color bd = !n.unlocked ? new Color(0.28f, 0.28f, 0.30f)
                         : isMax ? new Color(0.40f, 0.95f, 0.55f)
                         : n.affordable ? n.accent
                         : new Color(n.accent.r * 0.50f, n.accent.g * 0.50f, n.accent.b * 0.50f);

                DrawBorderedBox(r, bg, bd, (n.affordable || n.isMilestone) ? 2 : 1);

                _nodeTitleStyle.fontSize = scale < 0.8f ? 10 : 12;
                _nodeSubStyle.fontSize = scale < 0.8f ? 9 : 11;

                _nodeTitleStyle.normal.textColor = !n.unlocked ? new Color(0.52f, 0.52f, 0.54f)
                    : isMax ? new Color(0.55f, 1.0f, 0.65f)
                    : n.isMilestone ? new Color(1.0f, 0.92f, 0.45f)
                    : Color.white;
                GUI.Label(new Rect(r.x + 2, r.y + 4, r.width - 4, r.height * 0.48f), n.shortName, _nodeTitleStyle);

                _nodeSubStyle.normal.textColor = !n.unlocked ? new Color(0.46f, 0.46f, 0.50f)
                    : isMax ? new Color(0.55f, 1.0f, 0.65f)
                    : n.affordable ? n.accent
                    : new Color(0.75f, 0.72f, 0.66f);

                string sub = !n.unlocked ? "잠김"
                           : isMax ? "MAX"
                           : $"{n.level}/{n.maxLevel} · ${n.cost:N0}";
                GUI.Label(new Rect(r.x + 2, r.y + r.height * 0.50f, r.width - 4, r.height * 0.44f), sub, _nodeSubStyle);

                if (r.Contains(mPos)) hovered = n;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && n.unlocked && !isMax)
                {
                    manager.BuyNode(n.id);
                }
            }

            // 하단 상세 영수증 바 + 닫기 버튼
            float closeW = embeddedInHub ? 0f : 146f;
            Rect infoR = new Rect(panel.x + 14, panel.yMax - infoH - 10, panel.width - 28 - (embeddedInHub ? 0f : closeW + 10f), infoH);
            DrawReceiptBox(infoR);
            if (hovered.HasValue)
            {
                var hv = hovered.Value;
                bool isMax = hv.level >= hv.maxLevel;
                string state = !hv.unlocked ? $"[잠김 · {hv.unlockHint}]"
                             : isMax ? "[강화 완료 MAX]"
                             : hv.affordable ? $"[클릭 즉시 구매 · ${hv.cost:N0}]"
                             : $"[현금 부족 · ${hv.cost:N0}]";
                GUI.Label(new Rect(infoR.x + 14, infoR.y + 5, infoR.width - 28, 22), $"{hv.fullTitle} ({hv.level}/{hv.maxLevel})   {state}", _inkBold);
                GUI.Label(new Rect(infoR.x + 14, infoR.y + 28, infoR.width - 28, 20), $"• {hv.statDesc}", _inkSmall);
            }
            else
            {
                GUI.Label(infoR, "노드에 마우스를 올리면 효과와 선행 조건을 확인할 수 있습니다 · 좌클릭 즉시 강화", _inkCenter);
            }

            if (!embeddedInHub)
            {
                Rect closeR = new Rect(panel.xMax - 14 - closeW, infoR.y, closeW, infoH);
                DrawPaperCard(closeR, new Color(0.76f, 0.62f, 0.34f), 2);
                GUI.Label(closeR, "[E / ESC] 나가기", _inkCenter);
                if (GUI.Button(closeR, GUIContent.none, GUIStyle.none)) manager.CloseUpgradeKiosk();
            }
        }

        private TreeNodeDef BuildNode(string id, string shortName, string fullTitle, string statDesc, string unlockHint, string parentId, Vector2 offset, bool isMilestone, Color accent)
        {
            bool unlocked = manager.IsNodeUnlocked(id);
            int lv = manager.GetNodeLevel(id);
            int maxLv = manager.GetNodeMaxLevel(id);
            int cost = manager.GetNodeCost(id);
            bool afford = unlocked && lv < maxLv && manager.Money >= cost;
            return new TreeNodeDef
            {
                id = id,
                shortName = shortName,
                fullTitle = fullTitle,
                statDesc = statDesc,
                unlockHint = unlockHint,
                parentId = parentId,
                offset = offset,
                level = lv,
                maxLevel = maxLv,
                cost = cost,
                unlocked = unlocked,
                affordable = afford,
                isMilestone = isMilestone,
                accent = accent
            };
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
            DrawHubButton(new Rect(startX + (btnW + btnGap) * bi++, btnY, btnW, 44), "◈ 7종 스테이지 맵", false, new Color(0.22f, 0.48f, 0.80f), () => manager.OpenStageMindMap());
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
            DrawLegendChip(new Rect(panel.x + 348, legY, 174, 24), colCleared,   "✓ 클리어 (재방문 가능)");

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

                string statusTag = state == StageUnlockState.Cleared ? $"✓ {new string('★', Mathf.Max(1, CampaignProgressStore.GetStageStars(stageNum)))}"
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

                // 카드 본문: 방 이름 + 규격/쓰레기 수
                float bodyY = thumbR.yMax + 6f;
                GUI.color = state == StageUnlockState.Locked ? new Color(0.62f, 0.66f, 0.72f) : Color.white;
                GUI.Label(new Rect(r.x + 10, bodyY, r.width - 20, 22), info.title, _lightBold);
                GUI.color = state == StageUnlockState.Locked ? new Color(0.50f, 0.54f, 0.60f) : new Color(0.84f, 0.83f, 0.78f);
                GUI.Label(new Rect(r.x + 10, bodyY + 22, r.width - 20, 18), $"{info.sizeLabel}", _lightSmall);
                GUI.Label(new Rect(r.x + 10, bodyY + 40, r.width - 20, 18), $"{info.trashLabel}", _lightSmall);
                GUI.color = Color.white;

                // 하단 클릭 액션 버튼 스트립
                Rect actR = new Rect(r.x + 8, r.yMax - 30, r.width - 16, 23);
                Color actBg = state == StageUnlockState.Locked ? new Color(0.22f, 0.25f, 0.30f) : accent;
                DrawBorderedBox(actR, actBg, new Color(0.12f, 0.12f, 0.12f, 0.65f), 1);
                GUI.color = state == StageUnlockState.Locked ? new Color(0.64f, 0.68f, 0.74f) : ReadableOn(actBg);
                string actLabel = state == StageUnlockState.Locked ? $"ST.{stageNum - 1} 클리어 후 해금"
                                : state == StageUnlockState.Cleared ? "✓ 클릭하여 재입장"
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
            string fBadge = fState == StageUnlockState.Cleared ? "[✓ 클리어 완료]"
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
            float rw = 440f, rh = 310f;
            Rect r = new Rect(Mathf.Round((w - rw) * 0.5f), Mathf.Round((h - rh) * 0.5f - 6f), rw, rh);
            DrawReceiptBox(r);

            string title = manager.WasEarlyCheckout
                ? $"[STAGE {manager.CurrentStage} 80% 조기 정산 영수증]"
                : $"[STAGE {manager.CurrentStage} 청소 완료 영수증]";
            GUI.color = new Color(0.08f, 0.46f, 0.20f);
            GUI.Label(new Rect(r.x + 16, r.y + 18, rw - 32, 28), title, new GUIStyle(_lightCenter) { fontSize = 18 });
            GUI.color = Color.white;

            GUI.Label(new Rect(r.x + 16, r.y + 50, rw - 32, 20), "------------------------------------------------", _inkCenter);

            float y = r.y + 78f;
            DrawReceiptRow(r, y, "청소 진행률", $"{manager.OverallCleanPercent:0.0}%"); y += 28;
            DrawReceiptRow(r, y, "현재 장비", manager.CurrentToolName); y += 28;
            DrawReceiptRow(r, y, "수거 차량", manager.CurrentVehicleName); y += 28;
            DrawReceiptRow(r, y, "방 클리어 보너스", $"+${manager.LastClearBonus:N0}"); y += 28;

            GUI.Label(new Rect(r.x + 16, y + 4, rw - 32, 20), "------------------------------------------------", _inkCenter);

            Rect stamp = new Rect(r.x + 28, r.yMax - 54, rw - 56, 32);
            DrawBorderedBox(stamp, new Color(0.92f, 0.96f, 0.92f), new Color(0.18f, 0.58f, 0.30f), 2);
            GUI.color = new Color(0.08f, 0.46f, 0.20f);
            GUI.Label(stamp, "수익 · 모든 장비 · 강화 100% 유지", _lightCenter);
            GUI.color = Color.white;
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

            // 7종 스테이지 마인드맵 바로 열기
            GUI.Label(new Rect(r.x + 22, r.y + 168, 346, 20), "캠페인 스테이지 이동", _inkBold);
            if (GUI.Button(new Rect(r.x + 22, r.y + 194, 346, 36), "◈ 7종 스테이지 마인드맵 열기 [M]", _btnStyle))
                manager.OpenStageMindMap();

            if (GUI.Button(new Rect(r.x + 115, r.y + 252, 160, 38), "계속하기 [ESC]", _btnStyle))
                manager.ToggleSettingsMenu();
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
