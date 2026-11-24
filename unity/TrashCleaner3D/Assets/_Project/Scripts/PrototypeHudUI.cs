using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.7 안전 여백(상·하·좌·우 잘림 0%!) 개조식 HUD + 22노드 반응형 스팀 인크리멘탈 마인드맵!
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _titleStyle, _bigTitleStyle, _labelStyle, _smallLabelStyle, _centerStyle, _iconNodeStyle, _smallBtnStyle, _worldPopStyle, _worldPopShadowStyle;
        private Texture2D _darkTex, _panelTex, _barBgTex, _greenTex, _yellowTex, _orangeTex, _redTex, _purpleTex, _blueTex, _cyanTex, _goldTex, _lineTex, _whiteTex;
        private bool _inited;

        private string _hoverTitle, _hoverStat, _hoverPrice;
        private bool _hasHover;
        private bool _hasEverOpenedTab;

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (controller == null) controller = FindFirstObjectByType<FirstPersonCleanerController>();
        }

        private void InitStyles()
        {
            if (_inited) return;
            _inited = true;

            _krFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "Arial" }, 14);
            _darkTex = MakeTex(new Color(0.06f, 0.07f, 0.10f, 0.80f));
            _panelTex = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.94f));
            _barBgTex = MakeTex(new Color(0.18f, 0.20f, 0.25f, 0.92f));
            _greenTex = MakeTex(new Color(0.24f, 0.82f, 0.46f, 1f));
            _yellowTex = MakeTex(new Color(0.96f, 0.78f, 0.18f, 1f));
            _orangeTex = MakeTex(new Color(0.95f, 0.48f, 0.14f, 1f));
            _redTex = MakeTex(new Color(0.90f, 0.20f, 0.20f, 1f));
            _purpleTex = MakeTex(new Color(0.68f, 0.22f, 0.92f, 1f));
            _blueTex = MakeTex(new Color(0.20f, 0.56f, 0.94f, 1f));
            _cyanTex = MakeTex(new Color(0.32f, 0.90f, 0.98f, 1f));
            _goldTex = MakeTex(new Color(1.0f, 0.84f, 0.22f, 1f));
            _lineTex = MakeTex(new Color(0.60f, 0.66f, 0.78f, 0.72f));
            _whiteTex = MakeTex(Color.white);

            _titleStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bigTitleStyle = new GUIStyle(_titleStyle) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) } };
            _smallLabelStyle = new GUIStyle(_labelStyle) { fontSize = 11 };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _iconNodeStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallBtnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _worldPopStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _worldPopShadowStyle = new GUIStyle(_worldPopStyle) { normal = { textColor = new Color(0f, 0f, 0f, 0.85f) } };
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        private void Update()
        {
            if (manager != null && manager.Phase == GamePhase.MindMapUpgrade) _hasEverOpenedTab = true;
        }

        private void OnGUI()
        {
            if (manager == null) return;
            InitStyles();

            DrawFloatingWorldTexts();

            switch (manager.Phase)
            {
                case GamePhase.Phase1_TrashRemoval:
                case GamePhase.Phase2_FloorMopping:
                    DrawPlayingHud();
                    break;
                case GamePhase.TreasureDecision:
                    DrawTreasureDecisionOverlay();
                    break;
                case GamePhase.MindMapUpgrade:
                    DrawIconMindMapOverlay();
                    break;
                case GamePhase.SettingsMenu:
                    DrawSettingsOverlay();
                    break;
                case GamePhase.RoomCompleted:
                    DrawRoomCompletedOverlay();
                    break;
            }

            DrawScreenFlashEffects();
        }

        private void DrawScreenFlashEffects()
        {
            float sw = Screen.width, sh = Screen.height;
            Color prev = GUI.color;

            if (manager.IsFeverActive)
            {
                float fp = 0.22f + 0.14f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
                GUI.color = new Color(1f, 0.85f, 0.18f, fp);
                float e = 14f;
                GUI.DrawTexture(new Rect(0, 0, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - e, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, e, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - e, 0, e, sh), _whiteTex);
            }
            if (manager.TreasureFlash01 > 0.01f)
            {
                GUI.color = new Color(1f, 0.88f, 0.30f, manager.TreasureFlash01 * 0.42f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
            }
            if (manager.StealAlertFlash01 > 0.01f)
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 12f));
                GUI.color = new Color(0.95f, 0.15f, 0.15f, manager.StealAlertFlash01 * 0.34f * pulse);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
            }
            if (manager.FreshAirFlash01 > 0.01f)
            {
                GUI.color = new Color(0.35f, 0.92f, 1.0f, manager.FreshAirFlash01 * 0.24f);
                float e = 20f;
                GUI.DrawTexture(new Rect(0, 0, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - e, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, e, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - e, 0, e, sh), _whiteTex);
            }
            GUI.color = prev;
        }

        private void DrawFloatingWorldTexts()
        {
            if (manager.FloatingTexts.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            float sh = Screen.height;
            Color prev = GUI.color;
            foreach (var ft in manager.FloatingTexts)
            {
                Vector3 sp = cam.WorldToScreenPoint(ft.worldPos);
                if (sp.z <= 0.1f) continue;

                float alpha = 1f - Mathf.Clamp01((ft.age / ft.duration) * (ft.age / ft.duration));
                Rect r = new Rect(sp.x - 130f, (sh - sp.y) - 20f, 260f, 34f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), ft.text, _worldPopShadowStyle);
                _worldPopStyle.normal.textColor = new Color(ft.color.r, ft.color.g, ft.color.b, alpha);
                GUI.Label(r, ft.text, _worldPopStyle);
            }
            GUI.color = prev;
        }

        private void DrawPlayingHud()
        {
            float sw = Screen.width, sh = Screen.height;
            // 요청 c: Unity Game창 1.1x 확대 상태에서도 상·하·좌·우 절대 안 잘리는 안전 여백!
            float topY = Mathf.Max(36f, sh * 0.06f);
            float botSafe = Mathf.Max(54f, sh * 0.085f);
            float sideX = Mathf.Max(32f, sw * 0.048f);

            // 1. 악취 독가스 화면 가장자리
            int stStage = manager.StenchStage;
            if (stStage >= 1 && !manager.IsOutsideVentilating)
            {
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * (stStage >= 3 ? 6.5f : 3.0f));
                float alpha = (stStage == 1 ? 0.16f : stStage == 2 ? 0.28f : stStage == 3 ? 0.44f : 0.62f) * pulse;
                Color tint = stStage == 4 ? new Color(0.50f, 0.10f, 0.62f, alpha) : new Color(0.26f, 0.50f, 0.08f, alpha);
                float edge = stStage == 1 ? 16f : stStage == 2 ? 28f : stStage == 3 ? 46f : 68f;

                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(0, 0, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - edge, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, edge, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - edge, 0, edge, sh), _whiteTex);
                if (stStage >= 3)
                {
                    GUI.color = new Color(tint.r, tint.g, tint.b, stStage == 4 ? 0.18f : 0.08f);
                    GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
                }
                GUI.color = prev;
            }

            // 2. 상단 중앙: 안전 여백 악취 바
            DrawTopStenchBar(sw, topY);

            if (manager.IsTooSuffocatedToWork)
            {
                Rect sr = new Rect(sw / 2f - 125, topY + 52, 250, 26);
                GUI.DrawTexture(new Rect(sr.x - 2, sr.y - 2, sr.width + 4, sr.height + 4), _purpleTex);
                GUI.DrawTexture(sr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.55f, 0.98f);
                GUI.Label(sr, "☣ 작업 불가 · 문 밖 환기", _centerStyle);
            }
            else if (manager.LandlordNagTimer > 0f)
            {
                Rect nr = new Rect(sw / 2f - 175, topY + 52, 350, 26);
                GUI.DrawTexture(new Rect(nr.x - 2, nr.y - 2, nr.width + 4, nr.height + 4), _redTex);
                GUI.DrawTexture(nr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.55f, 0.50f);
                GUI.Label(nr, manager.LandlordMessage, _centerStyle);
            }

            // 3. 좌상단: 금액 + 진행도 1줄 (안전 여백!)
            GUI.DrawTexture(new Rect(sideX, topY, 174, 38), _darkTex);
            _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.32f);
            GUI.Label(new Rect(sideX + 8, topY + 2, 92, 22), $"${manager.Money:N0}", _titleStyle);
            _titleStyle.normal.textColor = Color.white;

            float pct = manager.Phase == GamePhase.Phase1_TrashRemoval ? manager.Phase1Percent : manager.Phase2Percent;
            string pctLabel = manager.Phase == GamePhase.Phase1_TrashRemoval ? $"{pct:0}%" : $"{manager.Phase2Cleaned}/{manager.Phase2Total}";
            _labelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(sideX + 90, topY + 2, 76, 22), pctLabel, _labelStyle);
            _labelStyle.alignment = TextAnchor.UpperLeft;

            DrawBar(new Rect(sideX + 8, topY + 27, 158, 5), pct / 100f,
                manager.Phase == GamePhase.Phase1_TrashRemoval ? _greenTex : _blueTex);

            float leftSubY = topY + 44f;
            if (manager.HasFeverEngine)
            {
                Rect fr = new Rect(sideX, leftSubY, 174, 22);
                GUI.DrawTexture(fr, _darkTex);
                _smallLabelStyle.normal.textColor = manager.IsFeverActive ? new Color(1f, 0.88f, 0.25f) : new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(new Rect(sideX + 8, leftSubY + 1, 158, 14),
                    manager.IsFeverActive ? $"FEVER {manager.FeverTimer:0.0}s" : $"FEVER {manager.FeverCharge01 * 100f:0}%", _smallLabelStyle);
                DrawBar(new Rect(sideX + 8, leftSubY + 16, 158, 4),
                    manager.IsFeverActive ? manager.FeverTimer / 8.0f : manager.FeverCharge01, _goldTex);
                leftSubY += 26f;
            }
            if (manager.ThrowComboTimer > 0f && manager.ThrowComboCount >= 1)
            {
                Rect cr = new Rect(sideX, leftSubY, 174, 24);
                GUI.DrawTexture(cr, _darkTex);
                _smallLabelStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                GUI.Label(new Rect(sideX + 8, leftSubY + 1, 158, 15), $"COMBO x{manager.ThrowComboCount} · {manager.ThrowComboTimer:0.0}s", _smallLabelStyle);
                DrawBar(new Rect(sideX + 8, leftSubY + 17, 158, 4), manager.ThrowComboTimer / manager.MaxComboWindow, _goldTex);
            }
            _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);

            // 4. 우상단: [Tab] 뱃지 (안전 여백!)
            int avail = manager.CountAffordableUpgrades();
            if (!_hasEverOpenedTab || avail > 0)
            {
                string badge = avail > 0 ? $"[Tab] +{avail}" : "[Tab]";
                Rect br = new Rect(sw - sideX - 82, topY, 82, 26);
                GUI.DrawTexture(br, _darkTex);
                if (avail > 0) GUI.DrawTexture(new Rect(br.x, br.yMax - 2, br.width, 2), _greenTex);
                _centerStyle.normal.textColor = avail > 0 ? new Color(0.55f, 1f, 0.65f) : new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(br, badge, _centerStyle);
            }

            if (manager.TrashPayMultiplier < 0.99f)
            {
                Rect pr = new Rect(sw - sideX - 114, topY + 32, 114, 22);
                GUI.DrawTexture(pr, _darkTex);
                _smallLabelStyle.alignment = TextAnchor.MiddleCenter;
                _smallLabelStyle.normal.textColor = new Color(1f, 0.45f, 0.45f);
                GUI.Label(pr, $"수익 {manager.TrashPayMultiplier * 100f:0}% · 물가 {manager.ShopPriceMultiplier * 100f:0}%", _smallLabelStyle);
                _smallLabelStyle.alignment = TextAnchor.UpperLeft;
                _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }

            // 5. 중앙 조준점
            if (controller != null)
            {
                Texture2D dot = controller.AimDoor != null || controller.AimDroppedBag != null ? _whiteTex
                    : controller.AimIsPile ? _orangeTex
                    : controller.AimIsSwarmNear ? _greenTex
                    : controller.AimIsStain ? _blueTex : _barBgTex;
                float ds = (controller.AimDoor != null || controller.AimDroppedBag != null || controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain) ? 8f : 5f;
                GUI.DrawTexture(new Rect(sw / 2f - ds / 2f, sh / 2f - ds / 2f, ds, ds), dot);

                if (controller.AimDroppedBag != null && !manager.HasCarriedBag)
                {
                    DrawKeyChip(sw / 2f, sh / 2f + 26, "[RMB] 봉지 들기");
                }
                else if (controller.AimDoor != null)
                {
                    DrawKeyChip(sw / 2f, sh / 2f + 26, controller.AimDoor.IsOpen ? "[F] 닫기" : "[F] 열기");
                }
                else if (controller.AimProgress01 > 0.01f)
                {
                    Texture2D barCol = controller.AimIsStain ? _blueTex : controller.AimIsPile ? _orangeTex : _greenTex;
                    DrawBar(new Rect(sw / 2f - 48, sh / 2f + 16, 96, 7), controller.AimProgress01, barCol);
                }

                if (controller.NeedMouseShakeHint) DrawKeyChip(sw / 2f, sh / 2f + 34, "← →");

                if (controller.ShowHelp)
                {
                    GUI.DrawTexture(new Rect(sideX, sh - botSafe - 72, 260, 72), _darkTex);
                    GUI.Label(new Rect(sideX + 8, sh - botSafe - 66, 244, 18), "[F] 문   [Space] 점프   [T] 손전등", _smallLabelStyle);
                    GUI.Label(new Rect(sideX + 8, sh - botSafe - 46, 244, 18), "[LMB] 줍기·타격   [RMB] 봉지 들기·투하", _smallLabelStyle);
                    GUI.Label(new Rect(sideX + 8, sh - botSafe - 26, 244, 18), "[Tab] 스킬트리   [ESC] 감도·소리 설정", _smallLabelStyle);
                }
            }

            // 6. 하단 중앙 안전 여백 「보유 봉지 + 수집 바」 (절대 안 잘림 + 1줄 고정!)
            DrawBottomBagBar(sw, sh, botSafe);

            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                float ty = sh - botSafe - (manager.HasCarriedBag ? 98f : 68f);
                Rect tr = new Rect(sw / 2f - 170, ty, 340, 24);
                GUI.DrawTexture(tr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(tr, manager.ToastMessage, _centerStyle);
            }
        }

        private void DrawKeyChip(float centerX, float centerY, string text)
        {
            float w = Mathf.Max(74f, text.Length * 11f + 22f);
            Rect r = new Rect(centerX - w / 2f, centerY - 12f, w, 24f);
            GUI.DrawTexture(r, _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(r, text, _centerStyle);
        }

        private void DrawBottomBagBar(float sw, float sh, float botSafe)
        {
            float bw = 310f;
            float bx = (sw - bw) / 2f;
            float baseBottomY = sh - botSafe;

            if (manager.HasCarriedBag)
            {
                Rect hr = new Rect(bx, baseBottomY - 68f, bw, 28f);
                GUI.DrawTexture(new Rect(hr.x - 2, hr.y - 2, hr.width + 4, hr.height + 4), _goldTex);
                GUI.DrawTexture(hr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.32f);
                GUI.Label(hr, $"▣ 보유 봉지 ${manager.CarriedBagValue}  ·  [RMB] 투하", _centerStyle);
            }

            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.CollectingCount == 0) return;

            float bh = 34f;
            float by = baseBottomY - bh;
            GUI.DrawTexture(new Rect(bx, by, bw, bh), _darkTex);

            float fill = Mathf.Clamp01(manager.CollectingCount / (float)manager.HandBagCapacity);
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            _labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(bx + 10, by + 2, 140, 18), $"수집 {manager.CollectingCount}/{manager.HandBagCapacity}", _labelStyle);

            _labelStyle.alignment = TextAnchor.MiddleRight;
            _labelStyle.normal.textColor = new Color(0.65f, 0.92f, 0.72f);
            GUI.Label(new Rect(bx + bw - 140, by + 2, 130, 18), $"${manager.CollectingPendingValue}", _labelStyle);

            _labelStyle.alignment = TextAnchor.UpperLeft;
            _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);

            DrawBar(new Rect(bx + 10, by + 22, bw - 20, 7), fill, _greenTex);
        }

        private void DrawTopStenchBar(float sw, float topY)
        {
            int stage = manager.StenchStage;
            bool showChips = manager.IsOutsideVentilating || stage >= 1;
            float totalW = 290f;
            float h = showChips ? 44f : 30f;
            float x = (sw - totalW) / 2f;

            GUI.DrawTexture(new Rect(x, topY, totalW, h), _darkTex);
            Texture2D colTex = manager.IsOutsideVentilating ? _cyanTex
                : stage == 0 ? _greenTex : stage == 1 ? _yellowTex : stage == 2 ? _orangeTex : stage == 3 ? _redTex : _purpleTex;

            Rect iconR = new Rect(x + 4, topY + 4, 26, h - 8);
            GUI.DrawTexture(iconR, colTex);
            GUI.DrawTexture(new Rect(iconR.x + 2, iconR.y + 2, iconR.width - 4, iconR.height - 4), _darkTex);
            _centerStyle.normal.textColor = stage >= 3 && !manager.IsOutsideVentilating ? new Color(1f, 0.50f, 0.95f) : Color.white;
            GUI.Label(iconR, "☣", _centerStyle);

            float bx = x + 36, by = topY + 8, bw = totalW - 44;
            DrawBar(new Rect(bx, by, bw, 12), manager.StenchPercent / 100f, colTex);
            for (int k = 1; k <= 3; k++) GUI.DrawTexture(new Rect(bx + bw * (k * 0.25f), by, 2, 12), _darkTex);

            if (showChips)
            {
                string tag = manager.IsOutsideVentilating ? "환기"
                    : stage == 1 ? "이속↓ · 떼기↓"
                    : stage == 2 ? "줍기×0.5 · 이속↓↓"
                    : stage == 3 ? "헛손질 35% · 이속↓↓↓"
                    : "작업 불가";
                _smallLabelStyle.normal.textColor = manager.IsOutsideVentilating ? new Color(0.50f, 0.96f, 1f)
                    : stage >= 3 ? new Color(1f, 0.48f, 0.45f) : new Color(0.95f, 0.86f, 0.55f);
                GUI.Label(new Rect(bx, by + 15, bw, 16), tag, _smallLabelStyle);
                _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }
        }

        private void DrawSettingsOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);

            float w = Mathf.Min(360f, sw - 60f), h = 236f;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 2, box.y - 2, w + 4, h + 4), _blueTex);
            GUI.DrawTexture(box, _darkTex);

            float pad = 24f;
            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(box.x, box.y + 14, w, 26), "설정", _bigTitleStyle);

            GUI.Label(new Rect(box.x + pad, box.y + 56, w - pad * 2, 20), $"마우스 속도   {manager.MouseSensitivity * 100f:0}%", _titleStyle);
            float newSens = GUI.HorizontalSlider(new Rect(box.x + pad, box.y + 82, w - pad * 2, 20), manager.MouseSensitivity, 0.03f, 0.40f);
            if (!Mathf.Approximately(newSens, manager.MouseSensitivity)) manager.SetMouseSensitivity(newSens);

            GUI.Label(new Rect(box.x + pad, box.y + 116, w - pad * 2, 20), $"소리   {manager.MasterVolume * 100f:0}%", _titleStyle);
            float newVol = GUI.HorizontalSlider(new Rect(box.x + pad, box.y + 142, w - pad * 2, 20), manager.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(newVol, manager.MasterVolume)) manager.SetMasterVolume(newVol);

            if (GUI.Button(new Rect(box.x + w / 2f - 90, box.y + h - 50, 180, 34), "계속 [ESC]", _smallBtnStyle))
            {
                manager.ToggleSettingsMenu();
            }
        }

        private void DrawTreasureDecisionOverlay()
        {
            var item = manager.ActiveTreasure;
            if (item == null) return;

            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);

            float w = Mathf.Min(490f, sw - 60f);
            float h = 216f;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);

            float glowPad = 4f + 2f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
            Texture2D border = item.isDesignatedLostItem ? _redTex : _goldTex;
            GUI.DrawTexture(new Rect(box.x - glowPad, box.y - glowPad, w + glowPad * 2f, h + glowPad * 2f), border);
            GUI.DrawTexture(box, _darkTex);

            float pad = 20f;
            _bigTitleStyle.normal.textColor = item.isDesignatedLostItem ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.88f, 0.30f);
            GUI.Label(new Rect(box.x + pad, box.y + 16, w - pad * 2, 28), $"★ {item.itemName}", _bigTitleStyle);

            bool locked = manager.TreasureDecisionLockTimer > 0f;
            float btnW = (w - pad * 2 - 14f) / 2f;
            float btnY = box.y + 62f;
            float btnH = 116f;
            int keepTip = item.isDesignatedLostItem ? manager.lostItemTip : Mathf.Max(35, item.value / 4);

            GUI.enabled = !locked;
            Rect keepR = new Rect(box.x + pad, btnY, btnW, btnH);
            GUI.DrawTexture(new Rect(keepR.x - 2, keepR.y - 2, keepR.width + 4, keepR.height + 4), _blueTex);
            if (GUI.Button(keepR, $"[Q] 보관\n+${keepTip}\n\n단가·물가 유지", _smallBtnStyle))
                manager.ResolveActiveTreasure(false);

            Rect stealR = new Rect(box.x + pad + btnW + 14f, btnY, btnW, btnH);
            GUI.DrawTexture(new Rect(stealR.x - 2, stealR.y - 2, stealR.width + 4, stealR.height + 4), item.isDesignatedLostItem ? _redTex : _goldTex);
            string riskLine = item.isDesignatedLostItem ? "적발 100% · 수익 -35%\n강화비 +25%" : $"적발 {item.demeritPercent}% · 수익 -20%\n강화비 +15%";
            if (GUI.Button(stealR, $"[E] 슈킹\n+${item.value}\n{riskLine}", _smallBtnStyle))
                manager.ResolveActiveTreasure(true);
            GUI.enabled = true;

            if (locked)
                DrawBar(new Rect(box.x + pad, box.y + h - 22f, w - pad * 2, 6), 1f - (manager.TreasureDecisionLockTimer / 0.55f), border);
        }

        /// <summary>
        /// 요청 b & c: 안전 여백 적용 22노드 스팀 인크리멘탈 순차 마인드맵 (1.1x 확대 창에서도 상하좌우 0% 잘림!)
        /// </summary>
        private void DrawIconMindMapOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);
            _hasHover = false;

            float topSafe = Mathf.Max(42f, sh * 0.065f);
            float botSafe = Mathf.Max(58f, sh * 0.09f);
            float usableH = sh - topSafe - botSafe - 115f;
            float usableW = sw - Mathf.Max(80f, sw * 0.10f);

            float s = Mathf.Clamp(Mathf.Min(usableW / 860f, usableH / 420f), 0.48f, 0.96f);
            float nodeSz = Mathf.Round(50f * s);
            float cx = sw * 0.50f;
            float cy = topSafe + 34f + usableH * 0.50f;

            string priceTag = manager.ShopPriceMultiplier > 1.01f ? $"   ·   물가 {manager.ShopPriceMultiplier * 100f:0}%" : "";
            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, topSafe, sw, 26), $"${manager.Money:N0}{priceTag}   ·   [Tab]", _bigTitleStyle);

            Vector2 P(float dx, float dy) => new Vector2(cx + dx * s, cy + dy * s);

            Vector2 root = P(0, 0);

            // 🟠 좌상 — 채집 · 크리 · 연쇄 · 자동화 (6노드)
            Vector2 nSweep = P(-115, -52);
            Vector2 nPeel = P(-225, -108);
            Vector2 nVac = P(-340, -142);
            Vector2 nCrit = P(-225, -24);
            Vector2 nChain = P(-335, -36);
            Vector2 nRoomba = P(-420, -100);

            // 🔵 우상 — 운반 · 투척 · 경제 (6노드)
            Vector2 nBag = P(115, -52);
            Vector2 nThrow = P(225, -112);
            Vector2 nTruck = P(335, -124);
            Vector2 nMagnet = P(425, -72);
            Vector2 nPrice = P(225, -24);
            Vector2 nCombo = P(335, -14);

            // 🟢 좌하 — 생존 · 기동 · 피버 · 걸레질 (5노드)
            Vector2 nMask = P(-115, 72);
            Vector2 nSpeed = P(-225, 58);
            Vector2 nFever = P(-340, 58);
            Vector2 nMop = P(-225, 138);
            Vector2 nScrub = P(-340, 138);

            // 🟡 우하 — 파괴 · 충격파 · 탐지 · 보물 · 슈킹 (5노드)
            Vector2 nFist = P(115, 72);
            Vector2 nShock = P(225, 54);
            Vector2 nBeacon = P(335, 54);
            Vector2 nLuck = P(225, 138);
            Vector2 nPoker = P(340, 138);

            // 22노드 순차 연결선 (교차선 0개!)
            DrawLine(root, nSweep, 3f); DrawLine(nSweep, nPeel, 2.2f); DrawLine(nPeel, nVac, 2.2f);
            DrawLine(nSweep, nCrit, 2.2f); DrawLine(nCrit, nChain, 2.2f); DrawLine(nChain, nRoomba, 2.2f);

            DrawLine(root, nBag, 3f); DrawLine(nBag, nThrow, 2.2f); DrawLine(nThrow, nTruck, 2.2f); DrawLine(nTruck, nMagnet, 2.2f);
            DrawLine(nBag, nPrice, 2.2f); DrawLine(nPrice, nCombo, 2.2f);

            DrawLine(root, nMask, 3f); DrawLine(nMask, nSpeed, 2.2f); DrawLine(nSpeed, nFever, 2.2f);
            DrawLine(nMask, nMop, 2.2f); DrawLine(nMop, nScrub, 2.2f);

            DrawLine(root, nFist, 3f); DrawLine(nFist, nShock, 2.2f); DrawLine(nFist, nBeacon, 2.2f);
            DrawLine(nFist, nLuck, 2.2f); DrawLine(nLuck, nPoker, 2.2f);

            Rect coreR = CenteredRect(root, nodeSz, nodeSz);
            GUI.DrawTexture(coreR, _goldTex);
            GUI.DrawTexture(new Rect(coreR.x + 3, coreR.y + 3, nodeSz - 6, nodeSz - 6), _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(coreR, "▲", _centerStyle);

            // 🟠 좌상 6노드
            DrawIconNode(nSweep, nodeSz, "sweep", "◎", $"{manager.LvSweepRadius}/5", "한 번에 줍기", $"현재 {manager.PickCountPerClick}개  ·  최대 20개", manager.CostSweep, manager.LvSweepRadius >= 5, manager.IsNodeUnlocked("sweep"), _orangeTex);
            DrawIconNode(nPeel, nodeSz, "peel", "⚡", $"{manager.LvPeelPower}/2", "손악력", $"현재 {manager.ClicksNeededToPick}클릭 → 1클릭  (선행: 줍기 Lv.1)", manager.CostPeel, manager.LvPeelPower >= 2, manager.IsNodeUnlocked("peel"), _orangeTex);
            DrawIconNode(nVac, nodeSz, "vacuum", "◉", manager.HasVacuumUnlocked ? "ON" : "0/1", "진공청소기", "연속 흡입 · 용량 ×3  (선행: 손악력 MAX · 줍기 Lv.2)", manager.CostVacuum, manager.HasVacuumUnlocked, manager.IsNodeUnlocked("vacuum"), _orangeTex);
            DrawIconNode(nCrit, nodeSz, "crit", "✦", $"{manager.LvCrit}/4", "크리티컬 손길", $"1방 즉사 · 수익 ×2  ({manager.CritChance * 100f:0}% · 선행: 줍기 Lv.1)", manager.CostCrit, manager.LvCrit >= 4, manager.IsNodeUnlocked("crit"), _orangeTex);
            DrawIconNode(nChain, nodeSz, "chain", "❖", $"{manager.LvChainPop}/3", "연쇄 도미노", $"주변 +{manager.ChainExtraCount}개 연쇄 수거  ({manager.ChainChance * 100f:0}% · 선행: 크리 Lv.1)", manager.CostChain, manager.LvChainPop >= 3, manager.IsNodeUnlocked("chain"), _orangeTex);
            DrawIconNode(nRoomba, nodeSz, "roomba", "●", $"{manager.LvRoomba}/3", "로봇청소기", $"3D 자동 수거 펫 {manager.LvRoomba}대  (선행: 연쇄 Lv.1)", manager.CostRoomba, manager.LvRoomba >= 3, manager.IsNodeUnlocked("roomba"), _cyanTex);

            // 🔵 우상 6노드
            DrawIconNode(nBag, nodeSz, "bag", "▣", $"{manager.LvBagSize}/4", "봉투 용량", $"현재 {manager.HandBagCapacity}개", manager.CostBag, manager.LvBagSize >= 4, manager.IsNodeUnlocked("bag"), _blueTex);
            DrawIconNode(nThrow, nodeSz, "throw", "↗", $"{manager.LvThrowPower}/4", "던지는 힘", $"투척력 {manager.ThrowForwardForce:0.0}  (선행: 봉투 Lv.1)", manager.CostThrowPower, manager.LvThrowPower >= 4, manager.IsNodeUnlocked("throw"), _blueTex);
            string nextVeh = manager.LvTruckSize == 0 ? "리어카" : manager.LvTruckSize == 1 ? "다마스" : manager.LvTruckSize == 2 ? "1톤 트럭" : "MAX";
            DrawIconNode(nTruck, nodeSz, "truck", "▶▶", $"{manager.LvTruckSize}/3", "수거함 진화", $"{manager.CurrentVehicleName} → {nextVeh}  (선행: 던지기 Lv.1)", manager.CostTruck, manager.LvTruckSize >= 3, manager.IsNodeUnlocked("truck"), _blueTex);
            DrawIconNode(nMagnet, nodeSz, "magnet", "Ω", manager.HasTruckMagnet ? "ON" : "0/1", "자석 압축기", "투입구 자동 흡입  (선행: 다마스)", manager.CostMagnet, manager.HasTruckMagnet, manager.IsNodeUnlocked("magnet"), _blueTex);
            DrawIconNode(nPrice, nodeSz, "price", "$", $"{manager.LvTrashPrice}/5", "분리수거 단가", $"쓰레기 가치 +{manager.LvTrashPrice * 25}%  (선행: 봉투 Lv.1)", manager.CostTrashPrice, manager.LvTrashPrice >= 5, manager.IsNodeUnlocked("price"), _blueTex);
            DrawIconNode(nCombo, nodeSz, "combo", "◷", $"{manager.LvComboTime}/3", "콤보 연장", $"유지 {manager.MaxComboWindow:0.0}s · 배율 +10%/Lv  (선행: 단가 Lv.1)", manager.CostComboTime, manager.LvComboTime >= 3, manager.IsNodeUnlocked("combo"), _blueTex);

            // 🟢 좌하 5노드
            DrawIconNode(nMask, nodeSz, "mask", "☣", $"{manager.LvMask}/5", "마스크", $"악취 누적 -{manager.LvMask * 16}%", manager.CostMask, manager.LvMask >= 5, manager.IsNodeUnlocked("mask"), _greenTex);
            DrawIconNode(nSpeed, nodeSz, "speed", "»", $"{manager.LvMoveSpeed}/4", "운동화", $"이동속도 +{manager.LvMoveSpeed * 15}%  (선행: 마스크 Lv.1)", manager.CostMoveSpeed, manager.LvMoveSpeed >= 4, manager.IsNodeUnlocked("speed"), _greenTex);
            DrawIconNode(nFever, nodeSz, "fever", "★", manager.HasFeverEngine ? "ON" : "0/1", "피버 엔진", "8s 광란: 1클릭·이속↑·수익×1.5  (선행: 운동화 Lv.1)", manager.CostFever, manager.HasFeverEngine, manager.IsNodeUnlocked("fever"), _goldTex);
            DrawIconNode(nMop, nodeSz, "mop", "┴", manager.HasMopUnlocked ? "ON" : "0/1", "T자 대걸레", "닦기 속도 ×2.6  (선행: 마스크 Lv.1)", manager.CostMop, manager.HasMopUnlocked, manager.IsNodeUnlocked("mop"), _greenTex);
            DrawIconNode(nScrub, nodeSz, "scrub", "◈", $"{manager.LvScrubPower}/3", "강력 세제", "닦기 속도 +45%/Lv  (선행: 대걸레)", manager.CostScrub, manager.LvScrubPower >= 3, manager.IsNodeUnlocked("scrub"), _greenTex);

            // 🟡 우하 5노드
            DrawIconNode(nFist, nodeSz, "fist", "◆", $"{manager.LvFistPower}/4", "주먹 힘", $"타격력 {manager.FistDamage:0.0}  ·  봉투 HP 10", manager.CostFist, manager.LvFistPower >= 4, manager.IsNodeUnlocked("fist"), _orangeTex);
            DrawIconNode(nShock, nodeSz, "shock", "✹", $"{manager.LvShockwave}/3", "충격파 펀치", $"주변 큰 봉투 동시 타격  (선행: 주먹 Lv.1)", manager.CostShockwave, manager.LvShockwave >= 3, manager.IsNodeUnlocked("shock"), _orangeTex);
            DrawIconNode(nBeacon, nodeSz, "beacon", "✧", manager.HasBeaconDetect ? "ON" : "0/1", "10% 투시 테두리", "잔여 10% 외곽 표시  (선행: 주먹 Lv.1)", manager.CostBeacon, manager.HasBeaconDetect, manager.IsNodeUnlocked("beacon"), _goldTex);
            DrawIconNode(nLuck, nodeSz, "luck", "★", $"{manager.LvTreasureLuck}/3", "보물 눈", "보물 가치 +25%/Lv  (선행: 주먹 Lv.1)", manager.CostLuck, manager.LvTreasureLuck >= 3, manager.IsNodeUnlocked("luck"), _goldTex);
            DrawIconNode(nPoker, nodeSz, "poker", "♦", $"{manager.LvPokerFace}/4", "연기력", "적발 확률 -10%/Lv  (선행: 보물 눈 Lv.1)", manager.CostPoker, manager.LvPokerFace >= 4, manager.IsNodeUnlocked("poker"), _goldTex);

            if (_hasHover)
            {
                float tipW = Mathf.Min(450f, sw - 60f);
                Rect tipBox = new Rect((sw - tipW) / 2f, sh - botSafe - 92f, tipW, 52f);
                GUI.DrawTexture(tipBox, _darkTex);
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 5, tipW - 120, 20), _hoverTitle, _titleStyle);
                _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                _titleStyle.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(tipBox.x + tipW - 110, tipBox.y + 5, 96, 20), _hoverPrice, _titleStyle);
                _titleStyle.alignment = TextAnchor.UpperLeft;
                _titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 27, tipW - 28, 20), _hoverStat, _labelStyle);
            }

            if (GUI.Button(new Rect(sw / 2f - 85, sh - botSafe - 32f, 170, 30), "닫기 [Tab]", _smallBtnStyle))
            {
                manager.ToggleMindMap();
            }
        }

        private void DrawIconNode(Vector2 center, float size, string id, string icon, string lvText, string title, string stat, int cost, bool isMax, bool unlocked, Texture2D accentTex)
        {
            Rect r = CenteredRect(center, size, size);
            bool canBuy = unlocked && !isMax && manager.Money >= cost;

            if (r.Contains(Event.current.mousePosition))
            {
                _hasHover = true;
                _hoverTitle = $"{title}  {lvText}";
                _hoverStat = stat;
                _hoverPrice = isMax ? "MAX" : !unlocked ? "잠김" : $"${cost:N0}";
            }

            Texture2D frame = isMax ? _goldTex : canBuy ? accentTex : _barBgTex;
            GUI.DrawTexture(r, frame);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, size - 6, size - 6), _darkTex);

            Color prev = GUI.contentColor;
            GUI.contentColor = isMax ? new Color(1f, 0.9f, 0.4f) : canBuy ? Color.white : new Color(0.42f, 0.45f, 0.50f);
            float btnH = Mathf.Max(20f, size * 0.56f);
            if (GUI.Button(new Rect(r.x + 4, r.y + 4, size - 8, btnH), unlocked ? icon : "×", _iconNodeStyle))
            {
                if (canBuy) manager.BuyNode(id);
            }
            GUI.contentColor = prev;

            _centerStyle.normal.textColor = isMax ? new Color(1f, 0.9f, 0.35f) : canBuy ? new Color(0.6f, 1f, 0.7f) : new Color(0.55f, 0.57f, 0.61f);
            GUI.Label(new Rect(r.x, r.y + size - 18f, size, 16f), unlocked ? lvText : "잠김", _centerStyle);
        }

        private void DrawRoomCompletedOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            float w = Mathf.Min(440f, sw - 60f), h = 240f;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 3, box.y - 3, w + 6, h + 6), manager.LastWasCaught ? _redTex : _goldTex);
            GUI.DrawTexture(box, _panelTex);

            float x = box.x + 22, y = box.y + 20;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 44, 28), $"완청 완료   {stars}", _bigTitleStyle);

            string summary = manager.StolenCount == 0
                ? $"정직 완청  ·  보너스 {manager.CleanBonusMultiplier * 100f:0}%"
                : manager.LastWasCaught
                    ? $"슈킹 발각  ·  장물 ${manager.StolenValue:N0}  ·  수익 {manager.TrashPayMultiplier * 100f:0}%"
                    : $"완전범죄  ·  장물 ${manager.StolenValue:N0}";
            GUI.Label(new Rect(x, y + 54, w - 44, 24), summary, _centerStyle);
            GUI.Label(new Rect(x, y + 92, w - 44, 26), $"보유 현금   ${manager.Money:N0}", _bigTitleStyle);

            if (GUI.Button(new Rect(x + 40, box.y + h - 58, w - 124, 40), "다시 플레이", _smallBtnStyle))
                manager.ReplayRoomKeepUpgrades();
        }

        private void DrawLine(Vector2 a, Vector2 b, float width)
        {
            Matrix4x4 saved = GUI.matrix;
            Vector2 d = b - a;
            float len = d.magnitude;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), _lineTex);
            GUI.matrix = saved;
        }

        private static Rect CenteredRect(Vector2 c, float w, float h) => new Rect(c.x - w / 2f, c.y - h / 2f, w, h);

        private void DrawBar(Rect r, float fill01, Texture2D fillTex)
        {
            GUI.DrawTexture(r, _barBgTex);
            float w = Mathf.Clamp01(fill01) * r.width;
            if (w > 1f) GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), fillTex);
        }
    }
}
