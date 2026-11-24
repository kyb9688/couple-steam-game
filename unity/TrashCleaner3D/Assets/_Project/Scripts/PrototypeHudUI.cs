using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.4 초미니멀 몰입형 HUD + 3D 월드 돈 팝업 + 강력한 악취/적발 화면 이펙트 +
    /// 희귀 보물 즉시 결정 황금 이펙트 모달(글자 잘림 0%) + 해상도 반응형 13노드 마인드맵(끝 잘림 0%).
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _titleStyle, _bigTitleStyle, _labelStyle, _smallLabelStyle, _centerStyle, _iconNodeStyle, _smallBtnStyle, _worldPopStyle, _worldPopShadowStyle;
        private Texture2D _darkTex, _panelTex, _barBgTex, _greenTex, _yellowTex, _orangeTex, _redTex, _purpleTex, _blueTex, _cyanTex, _goldTex, _lineTex, _whiteTex;
        private bool _inited;

        private string _hoverTitle, _hoverDesc, _hoverPrice;
        private bool _hasHover;
        private bool _hasEverOpenedTab;
        private float _introHintTimer = 7f;

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
            _panelTex = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.95f));
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
            _bigTitleStyle = new GUIStyle(_titleStyle) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) } };
            _smallLabelStyle = new GUIStyle(_labelStyle) { fontSize = 11 };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _iconNodeStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallBtnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            _worldPopStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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
            if (_introHintTimer > 0f && manager != null &&
                (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping))
            {
                _introHintTimer -= Time.deltaTime;
            }
            if (manager != null && manager.Phase == GamePhase.MindMapUpgrade)
            {
                _hasEverOpenedTab = true;
            }
        }

        private void OnGUI()
        {
            if (manager == null) return;
            InitStyles();

            // 3D 월드 돈 팝업 (항상 플레이 화면 위에 표시!)
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
                case GamePhase.RoomCompleted:
                    DrawRoomCompletedOverlay();
                    break;
            }

            // 화면 전체 플래시 이펙트 (황금 보물 발굴 / 빨간 슈킹 적발 사이렌 / 청록 신선한 공기)
            DrawScreenFlashEffects();
        }

        private void DrawScreenFlashEffects()
        {
            float sw = Screen.width, sh = Screen.height;
            Color prev = GUI.color;

            if (manager.TreasureFlash01 > 0.01f)
            {
                GUI.color = new Color(1f, 0.88f, 0.30f, manager.TreasureFlash01 * 0.42f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
            }
            if (manager.StealAlertFlash01 > 0.01f)
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
                GUI.color = new Color(0.95f, 0.10f, 0.10f, manager.StealAlertFlash01 * 0.42f * pulse);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
            }
            if (manager.FreshAirFlash01 > 0.01f)
            {
                GUI.color = new Color(0.35f, 0.92f, 1.0f, manager.FreshAirFlash01 * 0.24f);
                float e = 24f;
                GUI.DrawTexture(new Rect(0, 0, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - e, sw, e), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, e, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - e, 0, e, sh), _whiteTex);
            }
            GUI.color = prev;
        }

        /// <summary>
        /// 요청 #8: 쓰레기를 트럭에 넣은 바로 그 3D 월드 위치에 돈 팝업 띄우기!
        /// </summary>
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
                float gx = sp.x;
                float gy = sh - sp.y;

                Rect r = new Rect(gx - 130f, gy - 20f, 260f, 36f);
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

            // 1. 요청 #7: 체감이 확실한 강력한 악취 독가스 화면 가장자리 이펙트!
            int stStage = manager.StenchStage;
            if (stStage >= 1 && !manager.IsOutsideVentilating)
            {
                float pulse = 0.72f + 0.28f * Mathf.Sin(Time.time * (stStage >= 3 ? 6.5f : 3.0f));
                float alpha = (stStage == 1 ? 0.18f : stStage == 2 ? 0.30f : stStage == 3 ? 0.46f : 0.62f) * pulse;
                Color tint = stStage == 4 ? new Color(0.50f, 0.10f, 0.62f, alpha) : new Color(0.26f, 0.50f, 0.08f, alpha);
                float edge = stStage == 1 ? 18f : stStage == 2 ? 32f : stStage == 3 ? 52f : 76f;

                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(0, 0, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - edge, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, edge, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - edge, 0, edge, sh), _whiteTex);
                // 75%+ 전체 화면에 살짝 독가스 안개 필터 씌우기
                if (stStage >= 3)
                {
                    GUI.color = new Color(tint.r, tint.g, tint.b, stStage == 4 ? 0.16f : 0.08f);
                    GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
                }
                GUI.color = prev;
            }

            // 2. 상단 중앙: 슬림 악취 바
            DrawTopStenchBar(sw);

            // 3. 좌상단: 초미니멀 1줄 (보유 현금 + 청소 진행도)
            GUI.DrawTexture(new Rect(14, 14, 186, 40), _darkTex);
            _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.32f);
            GUI.Label(new Rect(22, 17, 94, 22), $"${manager.Money:N0}", _titleStyle);
            _titleStyle.normal.textColor = Color.white;

            float pct = manager.Phase == GamePhase.Phase1_TrashRemoval ? manager.Phase1Percent : manager.Phase2Percent;
            string pctLabel = manager.Phase == GamePhase.Phase1_TrashRemoval ? $"{pct:0}%" : $"얼룩 {manager.Phase2Cleaned}/{manager.Phase2Total}";
            _labelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(106, 17, 86, 22), pctLabel, _labelStyle);
            _labelStyle.alignment = TextAnchor.UpperLeft;

            DrawBar(new Rect(22, 43, 170, 5), pct / 100f,
                manager.Phase == GamePhase.Phase1_TrashRemoval ? _greenTex : _blueTex);

            // 요청 #6: 5초 연속 투하 콤보 진행 중일 때만 좌상단 아래에 작은 콤보 타이머 표시
            if (manager.ThrowComboTimer > 0f && manager.ThrowComboCount >= 1)
            {
                Rect cr = new Rect(14, 60, 186, 28);
                GUI.DrawTexture(cr, _darkTex);
                float nextBonus = manager.ThrowComboCount * 25f;
                _smallLabelStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                GUI.Label(new Rect(22, 62, 170, 16), $"연속 투하 x{manager.ThrowComboCount} (다음 +{nextBonus:0}% · {manager.ThrowComboTimer:0.0}s)", _smallLabelStyle);
                _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
                DrawBar(new Rect(22, 80, 170, 4), manager.ThrowComboTimer / 5.0f, _goldTex);
            }

            // 4. 우상단: 알림 있을 때만 아주 작은 [Tab] 뱃지 1개
            int avail = manager.CountAffordableUpgrades();
            if (!_hasEverOpenedTab || avail > 0)
            {
                string badge = avail > 0 ? $"[Tab] +{avail}" : "[Tab]";
                Rect br = new Rect(sw - 96, 14, 82, 28);
                GUI.DrawTexture(br, _darkTex);
                if (avail > 0) GUI.DrawTexture(new Rect(br.x, br.yMax - 2, br.width, 2), _greenTex);
                _centerStyle.normal.textColor = avail > 0 ? new Color(0.55f, 1f, 0.65f) : new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(br, badge, _centerStyle);
            }

            if (manager.SuspicionStage >= 1)
            {
                Rect sr = new Rect(sw - 102, 48, 88, 22);
                GUI.DrawTexture(sr, _darkTex);
                string[] eyeNames = { "", "● 의아함", "● 의심!", "● 들통직전" };
                _centerStyle.normal.textColor = manager.SuspicionStage == 1 ? new Color(1f, 0.88f, 0.35f)
                    : manager.SuspicionStage == 2 ? new Color(1f, 0.56f, 0.22f) : new Color(1f, 0.32f, 0.32f);
                GUI.Label(sr, eyeNames[manager.SuspicionStage], _centerStyle);
            }

            // 5. 슈킹 디메리트 실시간 발동 배너 (확실히 눈에 띄게!)
            if (manager.RemoteWatchTimer > 0f)
            {
                Rect kr = new Rect(sw / 2f - 220, 68, 440, 34);
                GUI.DrawTexture(new Rect(kr.x - 2, kr.y - 2, kr.width + 4, kr.height + 4), _redTex);
                GUI.DrawTexture(kr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.45f, 0.45f);
                GUI.Label(kr, $"{manager.LandlordMessage} ({manager.RemoteWatchTimer:0}s)", _centerStyle);
            }
            if (manager.DoorVisitorTimer > 0f)
            {
                Rect dr = new Rect(sw / 2f - 210, manager.RemoteWatchTimer > 0f ? 108 : 68, 420, 34);
                GUI.DrawTexture(new Rect(dr.x - 2, dr.y - 2, dr.width + 4, dr.height + 4), _redTex);
                GUI.DrawTexture(dr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.82f, 0.35f);
                GUI.Label(dr, $"[집주인 현장 감시] 트럭 봉쇄! ({manager.DoorVisitorTimer:0.0}초간 투하 불가)", _centerStyle);
            }

            // 6. 화면 중앙 조준점 & 컨텍스트 프롬프트
            if (controller != null)
            {
                Texture2D dot = controller.AimDoor != null || controller.AimDroppedBag != null ? _whiteTex
                    : controller.AimIsPile ? _orangeTex
                    : controller.AimIsSwarmNear ? _greenTex
                    : controller.AimIsStain ? _blueTex : _barBgTex;
                float ds = (controller.AimDoor != null || controller.AimDroppedBag != null || controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain) ? 8f : 5f;
                GUI.DrawTexture(new Rect(sw / 2f - ds / 2f, sh / 2f - ds / 2f, ds, ds), dot);

                if (controller.AimDroppedBag != null)
                {
                    Rect rp = new Rect(sw / 2f - 105, sh / 2f + 18, 210, 26);
                    GUI.DrawTexture(rp, _darkTex);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(rp, "[좌클릭 / F] 떨어진 봉투 줍기", _centerStyle);
                }
                else if (controller.AimDoor != null)
                {
                    Rect dp = new Rect(sw / 2f - 66, sh / 2f + 18, 132, 26);
                    GUI.DrawTexture(dp, _darkTex);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(dp, controller.AimDoor.IsOpen ? "[F] 문 닫기" : "[F] 문 열기", _centerStyle);
                }
                else if (controller.AimProgress01 > 0.01f && (controller.AimIsPile || controller.AimIsStain))
                {
                    DrawBar(new Rect(sw / 2f - 52, sh / 2f + 16, 104, 7),
                        controller.AimProgress01, controller.AimIsStain ? _blueTex : _orangeTex);
                }
                else if (manager.HandBagCount > 0)
                {
                    float fill = Mathf.Clamp01(manager.HandBagCount / (float)manager.HandBagCapacity);
                    DrawBar(new Rect(sw / 2f - 48, sh / 2f + 16, 96, 6), fill, manager.CanThrowHandBag ? _goldTex : _greenTex);
                    if (manager.CanThrowHandBag)
                    {
                        _centerStyle.normal.textColor = new Color(1f, 0.90f, 0.30f);
                        GUI.Label(new Rect(sw / 2f - 140, sh / 2f + 24, 280, 20), $"가득 참 (${manager.HandBagPendingValue}) · 밖으로 나가 트럭에 [우클릭]", _centerStyle);
                    }
                }

                // 2단계 마우스 흔들기 강제 안내 (가만히 누르고 있을 때만 표시!)
                if (controller.NeedMouseShakeHint)
                {
                    _centerStyle.normal.textColor = new Color(0.60f, 0.94f, 1f);
                    GUI.Label(new Rect(sw / 2f - 125, sh / 2f + 28, 250, 22), "←→ 마우스를 흔들어야 닦입니다!", _centerStyle);
                }
                else if (Cursor.lockState != CursorLockMode.Locked)
                {
                    GUI.DrawTexture(new Rect(sw / 2f - 85, sh / 2f + 24, 170, 26), _darkTex);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(sw / 2f - 85, sh / 2f + 26, 170, 22), "화면 클릭 → 시작", _centerStyle);
                }

                if (controller.ShowHelp)
                {
                    GUI.DrawTexture(new Rect(14, sh - 92, 360, 78), _darkTex);
                    GUI.Label(new Rect(24, sh - 86, 340, 18), "[F] 문 열기 · [좌클릭] 쓰레기 줍기 / 떨어진 봉투 회수", _smallLabelStyle);
                    GUI.Label(new Rect(24, sh - 66, 340, 18), "[우클릭] 가득 찬 봉투 트럭 투하 (5초 연속 콤보!)", _smallLabelStyle);
                    GUI.Label(new Rect(24, sh - 46, 340, 18), "[Tab] 스킬트리 · [T] 손전등 · [H] 도움말 닫기", _smallLabelStyle);
                }
            }

            if (_introHintTimer > 0f && string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect hr = new Rect(sw / 2f - 235, sh - 40, 470, 26);
                GUI.DrawTexture(hr, _darkTex);
                _centerStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
                GUI.Label(hr, "[F] 문 열기  ·  [좌클릭] 줍기  ·  [우클릭] 트럭 투하  ·  [Tab] 강화", _centerStyle);
            }
            else if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect tr = new Rect(sw / 2f - 250, sh - 42, 500, 28);
                GUI.DrawTexture(tr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(tr, manager.ToastMessage, _centerStyle);
            }
        }

        private void DrawTopStenchBar(float sw)
        {
            int stage = manager.StenchStage;
            bool showText = manager.IsOutsideVentilating || stage >= 1;
            float totalW = 326f;
            float h = showText ? 48f : 32f;
            float x = (sw - totalW) / 2f, y = 14f;

            GUI.DrawTexture(new Rect(x, y, totalW, h), _darkTex);
            Texture2D colTex = manager.IsOutsideVentilating ? _cyanTex
                : stage == 0 ? _greenTex : stage == 1 ? _yellowTex : stage == 2 ? _orangeTex : stage == 3 ? _redTex : _purpleTex;

            Rect iconR = new Rect(x + 5, y + 4, 28, h - 8);
            GUI.DrawTexture(iconR, colTex);
            GUI.DrawTexture(new Rect(iconR.x + 2, iconR.y + 2, iconR.width - 4, iconR.height - 4), _darkTex);
            _centerStyle.normal.textColor = stage >= 3 && !manager.IsOutsideVentilating ? new Color(1f, 0.50f, 0.95f) : Color.white;
            GUI.Label(iconR, "☣", _centerStyle);

            float bx = x + 40, by = y + 9, bw = totalW - 48;
            DrawBar(new Rect(bx, by, bw, 12), manager.StenchPercent / 100f, colTex);
            for (int k = 1; k <= 3; k++)
            {
                GUI.DrawTexture(new Rect(bx + bw * (k * 0.25f), by, 2, 12), _darkTex);
            }

            if (showText)
            {
                string status = manager.IsOutsideVentilating ? "신선한 공기! 악취 빠르게 회복 중"
                    : stage == 1 ? "25% [이속 -25%]"
                    : stage == 2 ? "50% [이속 -38% · 손놀림 -35%]"
                    : stage == 3 ? "75% [이속 -50% · 기침! 문 밖 환기]"
                    : "100% [숨 막힘! 이속 -65% · 밖으로!]";
                _smallLabelStyle.normal.textColor = manager.IsOutsideVentilating ? new Color(0.50f, 0.96f, 1f)
                    : stage >= 3 ? new Color(1f, 0.48f, 0.45f) : new Color(0.95f, 0.86f, 0.55f);
                GUI.Label(new Rect(bx, by + 17, bw, 18), status, _smallLabelStyle);
                _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }
        }

        /// <summary>
        /// 요청 #1 & #11: 희귀 보물 발굴 시 화려한 황금 이펙트 + 즉시 보관/슈킹 결정 (버튼 높이 58px → 잘림 0%!)
        /// + 슈킹 디메리트(발각 확률 · 벌금 · 트럭 봉쇄/속도 저하)를 눈으로 명확히 표기!
        /// </summary>
        private void DrawTreasureDecisionOverlay()
        {
            var item = manager.ActiveTreasure;
            if (item == null) return;

            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);

            float w = Mathf.Min(560f, sw - 40f);
            float h = Mathf.Min(320f, sh - 40f);
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);

            // 반짝이는 황금/빨강 외곽 테두리 이펙트!
            float glowPad = 4f + 2f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
            Texture2D border = item.isDesignatedLostItem ? _redTex : _goldTex;
            GUI.DrawTexture(new Rect(box.x - glowPad, box.y - glowPad, w + glowPad * 2f, h + glowPad * 2f), border);
            GUI.DrawTexture(box, _darkTex);

            float pad = 22f;
            _bigTitleStyle.normal.textColor = item.isDesignatedLostItem ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.88f, 0.30f);
            GUI.Label(new Rect(box.x + pad, box.y + 18, w - pad * 2, 30),
                item.isDesignatedLostItem ? "★ 의뢰인 지정 분실물 발견! ★" : "★ 희귀 보물 발견! ★", _bigTitleStyle);

            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(box.x + pad, box.y + 56, w - pad * 2, 28), item.itemName, _titleStyle);
            _titleStyle.alignment = TextAnchor.UpperLeft;

            // 디메리트 상세 안내 박스
            Rect infoR = new Rect(box.x + pad, box.y + 94, w - pad * 2, 74);
            GUI.DrawTexture(infoR, _panelTex);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = new Color(1f, 0.68f, 0.52f);
            string demeritDesc = item.isDesignatedLostItem
                ? $"슈킹 시 100% 적발! 즉시 벌금 -${item.fineAmount} + 15초 트럭 봉쇄 + 최종 청소비 50% 삭감!"
                : $"슈킹 적발 확률 {item.demeritPercent}%  |  적발 시 즉시 벌금 -${item.fineAmount}\n+ 15초 트럭 봉쇄 또는 20초 작업속도 -35% 디버프!";
            GUI.Label(infoR, demeritDesc, _labelStyle);
            _labelStyle.alignment = TextAnchor.UpperLeft;
            _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);

            bool locked = manager.TreasureDecisionLockTimer > 0f;
            float btnW = (w - pad * 2 - 16f) / 2f;
            float btnY = box.y + h - 104f;
            float btnH = 62f;

            int keepTip = item.isDesignatedLostItem ? manager.lostItemTip : Mathf.Max(35, item.value / 4);

            GUI.enabled = !locked;
            // 좌측: 정직 보관 버튼 (넉넉한 62px 높이 → 글자 잘림 절대 없음!)
            Rect keepR = new Rect(box.x + pad, btnY, btnW, btnH);
            GUI.DrawTexture(new Rect(keepR.x - 2, keepR.y - 2, keepR.width + 4, keepR.height + 4), _blueTex);
            if (GUI.Button(keepR, $"[Q] 정직 보관  +${keepTip}\n안전 · 평판 유지", _smallBtnStyle))
            {
                manager.ResolveActiveTreasure(false);
            }

            // 우측: 몰래 슈킹 버튼
            Rect stealR = new Rect(box.x + pad + btnW + 16f, btnY, btnW, btnH);
            GUI.DrawTexture(new Rect(stealR.x - 2, stealR.y - 2, stealR.width + 4, stealR.height + 4), item.isDesignatedLostItem ? _redTex : _goldTex);
            if (GUI.Button(stealR, $"[E] 몰래 슈킹  +${item.value}\n적발 확률 {item.demeritPercent}%", _smallBtnStyle))
            {
                manager.ResolveActiveTreasure(true);
            }
            GUI.enabled = true;

            if (locked)
            {
                _centerStyle.normal.textColor = new Color(0.75f, 0.80f, 0.88f);
                GUI.Label(new Rect(box.x, box.y + h - 32f, w, 22), "실수 방지 잠금 해제 중...", _centerStyle);
            }
        }

        /// <summary>
        /// 요청 #10 & #13: 해상도 반응형 13노드 마인드맵 (던지는 힘 노드 추가 · 어떤 창 크기에서도 절대 안 잘림!)
        /// </summary>
        private void DrawIconMindMapOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);
            _hasHover = false;

            // 해상도에 맞춰 자동 축소/배치 (상·하·좌·우 잘림 원천 차단!)
            float s = Mathf.Clamp(Mathf.Min((sw - 40f) / 760f, (sh - 170f) / 410f), 0.58f, 1.05f);
            float nodeSz = Mathf.Round(56f * s);
            float cx = sw * 0.50f;
            float cy = 54f + (sh - 180f) * 0.50f;

            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 12, sw, 28), $"스킬트리  |  보유 현금 ${manager.Money:N0}   ([Tab] 닫기)", _bigTitleStyle);

            Vector2 P(float dx, float dy) => new Vector2(cx + dx * s, cy + dy * s);

            Vector2 root = P(0, 0);
            // 좌상: 줍기/주먹/청소기/탐지
            Vector2 nSweep = P(-120, -68);
            Vector2 nVac = P(-245, -108);
            Vector2 nFist = P(-120, -148);
            Vector2 nBeacon = P(-245, -172);
            // 우상: 봉투/던지는 힘/트럭/자석
            Vector2 nBag = P(120, -68);
            Vector2 nThrow = P(120, -150);
            Vector2 nTruck = P(245, -110);
            Vector2 nMagnet = P(245, -24);
            // 좌하: 마스크/대걸레/세제
            Vector2 nMask = P(-120, 78);
            Vector2 nMop = P(-245, 58);
            Vector2 nScrub = P(-245, 138);
            // 우하: 보물눈/뻔뻔함
            Vector2 nLuck = P(120, 78);
            Vector2 nPoker = P(245, 114);

            DrawLine(root, nSweep, 3f); DrawLine(nSweep, nVac, 2.5f);
            DrawLine(root, nFist, 2.5f); DrawLine(nFist, nBeacon, 2.5f);
            DrawLine(root, nBag, 3f); DrawLine(nBag, nThrow, 2.5f); DrawLine(nBag, nTruck, 2.5f); DrawLine(nTruck, nMagnet, 2.5f);
            DrawLine(root, nMask, 3f); DrawLine(nMask, nMop, 2.5f); DrawLine(nMop, nScrub, 2.5f);
            DrawLine(root, nLuck, 3f); DrawLine(nLuck, nPoker, 2.5f);

            Rect coreR = CenteredRect(root, nodeSz, nodeSz);
            GUI.DrawTexture(coreR, _goldTex);
            GUI.DrawTexture(new Rect(coreR.x + 3, coreR.y + 3, nodeSz - 6, nodeSz - 6), _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(coreR, "▲\n본부", _centerStyle);

            // 13개 노드 (던지는 힘 포함!)
            DrawIconNode(nSweep, nodeSz, "sweep", "◎", $"{manager.LvSweepRadius}/5", "한 번에 줍기 증가", $"클릭 1회당 줍는 쓰레기 개수 증가 (현재 {manager.PickCountPerClick}개 → 최대 20개)", manager.CostSweep, manager.LvSweepRadius >= 5, true, _orangeTex);
            DrawIconNode(nVac, nodeSz, "vacuum", "◉", manager.HasVacuumUnlocked ? "ON" : "0/1", "진공청소기 해금", "좌클릭 꾹 연속 흡입 + 봉투 용량 3배! (한 번에 줍기 Lv.2 필요)", manager.CostVacuum, manager.HasVacuumUnlocked, manager.LvSweepRadius >= 2, _orangeTex);
            DrawIconNode(nFist, nodeSz, "fist", "◆", $"{manager.LvFistPower}/4", "주먹 힘 강화", $"단단한 큰 봉투 타격력 강화 (현재 1타 {manager.FistDamage:0.0} / 봉투 HP 10)", manager.CostFist, manager.LvFistPower >= 4, true, _orangeTex);
            DrawIconNode(nBeacon, nodeSz, "beacon", "✦", manager.HasBeaconDetect ? "ON" : "0/1", "10% 빛기둥 탐지", "남은 쓰레기 10% 이하일 때 빛기둥 표시 (주먹 힘 Lv.1 필요)", manager.CostBeacon, manager.HasBeaconDetect, manager.LvFistPower >= 1, _orangeTex);

            DrawIconNode(nBag, nodeSz, "bag", "▣", $"{manager.LvBagSize}/4", "큰 쓰레기봉투", $"봉투 적재량 확대 (현재 {manager.HandBagCapacity}개)", manager.CostBag, manager.LvBagSize >= 4, true, _blueTex);
            DrawIconNode(nThrow, nodeSz, "throw", "↗", $"{manager.LvThrowPower}/4", "쓰레기 던지는 힘", $"봉투 투척 비거리 대폭 상승! (현재 투척력 {manager.ThrowForwardForce:0.0})", manager.CostThrowPower, manager.LvThrowPower >= 4, true, _blueTex);
            DrawIconNode(nTruck, nodeSz, "truck", "▶▶", $"{manager.LvTruckSize}/3", "트럭 투입구 확장", "트럭 짐칸 넓어짐 + 투하 보너스 증가 (큰 봉투 Lv.1 필요)", manager.CostTruck, manager.LvTruckSize >= 3, manager.LvBagSize >= 1, _blueTex);
            DrawIconNode(nMagnet, nodeSz, "magnet", "Ω", manager.HasTruckMagnet ? "ON" : "0/1", "트럭 자석 압축기", "트럭 근처로 던지면 자동으로 쏙 빨려 들어감 (트럭 확장 Lv.1 필요)", manager.CostMagnet, manager.HasTruckMagnet, manager.LvTruckSize >= 1, _blueTex);

            DrawIconNode(nMask, nodeSz, "mask", "☣", $"{manager.LvMask}/5", "마스크·방독면", $"강력한 악취 누적 속도 완화 (현재 -{manager.LvMask * 16}%)", manager.CostMask, manager.LvMask >= 5, true, _greenTex);
            DrawIconNode(nMop, nodeSz, "mop", "┴", manager.HasMopUnlocked ? "ON" : "0/1", "T자 대걸레 구매", "2단계 물티슈 → 2.6배속 대걸레 해금 (마스크 Lv.1 필요)", manager.CostMop, manager.HasMopUnlocked, manager.LvMask >= 1, _greenTex);
            DrawIconNode(nScrub, nodeSz, "scrub", "◈", $"{manager.LvScrubPower}/3", "강력 세제", "2단계 얼룩 닦기 속도 대폭 증가 (대걸레 필요)", manager.CostScrub, manager.LvScrubPower >= 3, manager.HasMopUnlocked, _greenTex);

            DrawIconNode(nLuck, nodeSz, "luck", "★", $"{manager.LvTreasureLuck}/3", "보물 감별 눈", "희귀 보물 가치 +22%/Lv 증가", manager.CostLuck, manager.LvTreasureLuck >= 3, true, _goldTex);
            DrawIconNode(nPoker, nodeSz, "poker", "♦", $"{manager.LvPokerFace}/4", "뻔뻔한 연기력", "슈킹 시 집주인 적발·벌금 확률 -10%/Lv 완화 (보물 눈 Lv.1 필요)", manager.CostPoker, manager.LvPokerFace >= 4, manager.LvTreasureLuck >= 1, _goldTex);

            // 하단 중앙 호버 툴팁 카드
            float tipW = Mathf.Min(560f, sw - 40f);
            Rect tipBox = new Rect((sw - tipW) / 2f, sh - 118f, tipW, 64f);
            GUI.DrawTexture(tipBox, _darkTex);
            if (_hasHover)
            {
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 6, tipW - 140, 22), _hoverTitle, _titleStyle);
                _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                _titleStyle.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(tipBox.x + tipW - 130, tipBox.y + 6, 116, 22), _hoverPrice, _titleStyle);
                _titleStyle.alignment = TextAnchor.UpperLeft;
                _titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 32, tipW - 28, 28), _hoverDesc, _labelStyle);
            }
            else
            {
                _centerStyle.normal.textColor = new Color(0.62f, 0.68f, 0.76f);
                GUI.Label(tipBox, "아이콘 위에 커서를 올리면 상세 설명 표시 · 클릭하여 강화", _centerStyle);
            }

            if (GUI.Button(new Rect(sw / 2f - 115, sh - 44, 230, 32), "돌아가기 ([Tab])", _smallBtnStyle))
            {
                manager.ToggleMindMap();
            }
        }

        private void DrawIconNode(Vector2 center, float size, string id, string icon, string lvText, string title, string desc, int cost, bool isMax, bool unlocked, Texture2D accentTex)
        {
            Rect r = CenteredRect(center, size, size);
            bool canBuy = unlocked && !isMax && manager.Money >= cost;

            if (r.Contains(Event.current.mousePosition))
            {
                _hasHover = true;
                _hoverTitle = $"{title} ({lvText})";
                _hoverDesc = desc;
                _hoverPrice = isMax ? "MAX" : !unlocked ? "선행 필요" : $"${cost:N0}";
            }

            Texture2D frame = isMax ? _goldTex : canBuy ? accentTex : _barBgTex;
            GUI.DrawTexture(r, frame);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, size - 6, size - 6), _darkTex);

            Color prev = GUI.contentColor;
            GUI.contentColor = isMax ? new Color(1f, 0.9f, 0.4f) : canBuy ? Color.white : new Color(0.45f, 0.48f, 0.54f);
            float btnH = Mathf.Max(22f, size * 0.58f);
            if (GUI.Button(new Rect(r.x + 4, r.y + 4, size - 8, btnH), icon, _iconNodeStyle))
            {
                if (canBuy) manager.BuyNode(id);
            }
            GUI.contentColor = prev;

            _centerStyle.normal.textColor = isMax ? new Color(1f, 0.9f, 0.35f) : canBuy ? new Color(0.6f, 1f, 0.7f) : new Color(0.58f, 0.60f, 0.64f);
            GUI.Label(new Rect(r.x, r.y + size - 19f, size, 17f), lvText, _centerStyle);
        }

        private void DrawRoomCompletedOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            float w = Mathf.Min(540f, sw - 40f), h = 310f;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 4, box.y - 4, w + 8, h + 8), manager.LastWasCaught ? _redTex : _goldTex);
            GUI.DrawTexture(box, _panelTex);

            float x = box.x + 24, y = box.y + 22;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 48, 30), $"원룸 100% 완청 완료  |  {stars}", _bigTitleStyle);

            string verdict = manager.LastWasCaught
                ? "[발각됨] 평판 1점 & 최종 청소비 50% 삭감 (업그레이드 유지)"
                : manager.StolenCount == 0
                    ? "[정직 완청] 청소비 전액 + 별점 5점!"
                    : $"[완전범죄 성공] 장물 ${manager.StolenValue:N0} + 청소비 전액!";
            GUI.Label(new Rect(x, y + 52, w - 48, 26), verdict, _centerStyle);
            GUI.Label(new Rect(x, y + 90, w - 48, 54), $"의뢰인 리뷰: {manager.LastReviewText}", _labelStyle);
            GUI.Label(new Rect(x, y + 156, w - 48, 24), $"보유 현금: ${manager.Money:N0}", _centerStyle);

            if (GUI.Button(new Rect(x + 40, box.y + h - 66, w - 128, 44), "강화된 상태로 다시 플레이", _smallBtnStyle))
            {
                manager.ReplayRoomKeepUpgrades();
            }
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
