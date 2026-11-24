using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.31 초미니멀 몰입형 HUD + 상단 슬림 악취 바 +
    /// 작은 정사각형 아이콘 노드 마인드맵(호버 툴팁 · 이모지 폰트 경고 0건) + 우측 발굴 보물함(즉시 보관/슈킹).
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _boxStyle, _titleStyle, _bigTitleStyle, _labelStyle, _smallLabelStyle, _centerStyle, _iconNodeStyle, _smallBtnStyle;
        private Texture2D _darkTex, _panelTex, _barBgTex, _greenTex, _yellowTex, _orangeTex, _redTex, _purpleTex, _blueTex, _goldTex, _lineTex, _whiteTex;
        private bool _inited;

        // 호버 툴팁 상태
        private string _hoverTitle, _hoverDesc, _hoverPrice;
        private bool _hasHover;

        // 조작 안내 1회성 표시 플래그 (화면 잡동사니 제거!)
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
            _darkTex = MakeTex(new Color(0.06f, 0.07f, 0.10f, 0.78f));
            _panelTex = MakeTex(new Color(0.08f, 0.09f, 0.13f, 0.94f));
            _barBgTex = MakeTex(new Color(0.18f, 0.20f, 0.25f, 0.92f));
            _greenTex = MakeTex(new Color(0.24f, 0.80f, 0.46f, 1f));
            _yellowTex = MakeTex(new Color(0.96f, 0.78f, 0.18f, 1f));
            _orangeTex = MakeTex(new Color(0.95f, 0.48f, 0.14f, 1f));
            _redTex = MakeTex(new Color(0.88f, 0.22f, 0.22f, 1f));
            _purpleTex = MakeTex(new Color(0.68f, 0.24f, 0.90f, 1f));
            _blueTex = MakeTex(new Color(0.20f, 0.56f, 0.94f, 1f));
            _goldTex = MakeTex(new Color(1.0f, 0.84f, 0.22f, 1f));
            _lineTex = MakeTex(new Color(0.60f, 0.66f, 0.78f, 0.72f));
            _whiteTex = MakeTex(Color.white);

            _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkTex }, padding = new RectOffset(8, 8, 6, 6) };
            _titleStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bigTitleStyle = new GUIStyle(_titleStyle) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) } };
            _smallLabelStyle = new GUIStyle(_labelStyle) { fontSize = 11 };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _iconNodeStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallBtnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
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

            switch (manager.Phase)
            {
                case GamePhase.Phase1_TrashRemoval:
                case GamePhase.Phase2_FloorMopping:
                    DrawPlayingHud();
                    break;
                case GamePhase.MindMapUpgrade:
                    DrawIconMindMapOverlay();
                    break;
                case GamePhase.RoomCompleted:
                    DrawRoomCompletedOverlay();
                    break;
            }
        }

        private void DrawPlayingHud()
        {
            float sw = Screen.width, sh = Screen.height;

            // 1. 악취 디버프 화면 가장자리 초록/보라 안개 (멀미 유발 카메라 흔들림 없음)
            int stStage = manager.StenchStage;
            if (stStage >= 1)
            {
                float alpha = stStage == 1 ? 0.11f : stStage == 2 ? 0.20f : stStage == 3 ? 0.32f : (0.40f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
                Color tint = stStage == 4 ? new Color(0.45f, 0.15f, 0.55f, alpha) : new Color(0.28f, 0.45f, 0.12f, alpha);
                float edge = stStage >= 3 ? 24f : 12f;
                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(0, 0, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - edge, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, edge, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - edge, 0, edge, sh), _whiteTex);
                GUI.color = prev;
            }

            // 2. 상단 중앙: 슬림 악취 바 (좌측 악취 아이콘 + 25/50/75/100% 직관적 색상 단계)
            DrawTopStenchBar(sw);

            // 3. 좌상단: 초미니멀 1줄 (보유 현금 + 청소 진행도 바만!)
            GUI.DrawTexture(new Rect(14, 14, 186, 40), _darkTex);
            _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.32f);
            GUI.Label(new Rect(22, 17, 92, 22), $"${manager.Money:N0}", _titleStyle);
            _titleStyle.normal.textColor = Color.white;

            float pct = manager.Phase == GamePhase.Phase1_TrashRemoval ? manager.Phase1Percent : manager.Phase2Percent;
            string pctLabel = manager.Phase == GamePhase.Phase1_TrashRemoval ? $"{pct:0}%" : $"얼룩 {manager.Phase2Cleaned}/{manager.Phase2Total}";
            _labelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(106, 17, 86, 22), pctLabel, _labelStyle);
            _labelStyle.alignment = TextAnchor.UpperLeft;

            DrawBar(new Rect(22, 43, 170, 5), pct / 100f,
                manager.Phase == GamePhase.Phase1_TrashRemoval ? _greenTex : _blueTex);

            // 4. 우상단: 거대 박스 완전 삭제! 알림이 있을 때만 아주 작은 뱃지 1개만 조용히 표시
            int avail = manager.CountAffordableUpgrades();
            int pend = manager.PendingTreasureCount;
            if (!_hasEverOpenedTab || avail > 0 || pend > 0)
            {
                string badge = pend > 0 ? $"[Tab] ★{pend}" : avail > 0 ? $"[Tab] +{avail}" : "[Tab]";
                float bw = 88f;
                Rect br = new Rect(sw - bw - 14, 14, bw, 28);
                GUI.DrawTexture(br, _darkTex);
                if (pend > 0 || avail > 0)
                {
                    GUI.DrawTexture(new Rect(br.x, br.yMax - 2, br.width, 2), pend > 0 ? _goldTex : _greenTex);
                }
                _centerStyle.normal.textColor = pend > 0 ? new Color(1f, 0.88f, 0.32f) : avail > 0 ? new Color(0.55f, 1f, 0.65f) : new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(br, badge, _centerStyle);
            }

            // 집주인 눈치도 평온(0)일 때는 아예 숨김! 의심 단계일 때만 작은 경고 점 표시
            if (manager.SuspicionStage >= 1)
            {
                Rect sr = new Rect(sw - 102, 48, 88, 22);
                GUI.DrawTexture(sr, _darkTex);
                string[] eyeNames = { "", "● 의아함", "● 의심!", "● 들통직전" };
                _centerStyle.normal.textColor = manager.SuspicionStage == 1 ? new Color(1f, 0.88f, 0.35f)
                    : manager.SuspicionStage == 2 ? new Color(1f, 0.56f, 0.22f) : new Color(1f, 0.32f, 0.32f);
                GUI.Label(sr, eyeNames[manager.SuspicionStage], _centerStyle);
            }

            // 5. 즉시 슈킹 해프닝 알림 (발생했을 때만 표시)
            if (manager.RemoteWatchTimer > 0f)
            {
                Rect kr = new Rect(sw - 380, 76, 366, 46);
                GUI.DrawTexture(new Rect(kr.x - 2, kr.y - 2, kr.width + 4, kr.height + 4), _redTex);
                GUI.DrawTexture(kr, _darkTex);
                GUI.Label(new Rect(kr.x + 10, kr.y + 5, kr.width - 20, 36), $"{manager.LandlordMessage} ({manager.RemoteWatchTimer:0}s)", _smallLabelStyle);
            }
            if (manager.DoorVisitorTimer > 0f)
            {
                Rect dr = new Rect(sw / 2f - 190, 68, 380, 30);
                GUI.DrawTexture(new Rect(dr.x - 2, dr.y - 2, dr.width + 4, dr.height + 4), _orangeTex);
                GUI.DrawTexture(dr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
                GUI.Label(dr, $"[문 앞 인기척] {manager.DoorVisitorTimer:0.0}초간 트럭 슛 금지", _centerStyle);
            }

            // 6. 화면 중앙 조준점 & 얇은 봉투/진행 게이지
            if (controller != null)
            {
                Texture2D dot = controller.AimIsPile ? _orangeTex
                    : controller.AimIsSwarmNear ? _greenTex
                    : controller.AimIsStain ? _blueTex : _barBgTex;
                float ds = (controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain) ? 8f : 5f;
                GUI.DrawTexture(new Rect(sw / 2f - ds / 2f, sh / 2f - ds / 2f, ds, ds), dot);

                if (manager.Phase == GamePhase.Phase1_TrashRemoval && !manager.HasVacuumUnlocked && manager.HandBagCount > 0)
                {
                    float fill = Mathf.Clamp01(manager.HandBagCount / (float)manager.HandBagCapacity);
                    DrawBar(new Rect(sw / 2f - 48, sh / 2f + 16, 96, 6), fill, manager.IsHandBagFull ? _goldTex : _greenTex);
                    if (manager.IsHandBagFull)
                    {
                        _centerStyle.normal.textColor = new Color(1f, 0.90f, 0.30f);
                        GUI.Label(new Rect(sw / 2f - 100, sh / 2f + 24, 200, 20), "가득 참 · 문 밖 [우클릭] 슛", _centerStyle);
                    }
                }
                else if (controller.AimProgress01 > 0.01f)
                {
                    DrawBar(new Rect(sw / 2f - 48, sh / 2f + 16, 96, 6),
                        controller.AimProgress01, controller.AimIsStain ? _blueTex : _orangeTex);
                }

                // 2단계 첫 얼룩 닦을 때만 1회 안내
                if (controller.NeedMouseShakeHint && manager.Phase2Cleaned == 0)
                {
                    _centerStyle.normal.textColor = new Color(0.65f, 0.92f, 1f);
                    GUI.Label(new Rect(sw / 2f - 100, sh / 2f + 26, 200, 20), "←→ 마우스 흔들기", _centerStyle);
                }
                else if (Cursor.lockState != CursorLockMode.Locked)
                {
                    GUI.DrawTexture(new Rect(sw / 2f - 85, sh / 2f + 24, 170, 26), _darkTex);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(sw / 2f - 85, sh / 2f + 26, 170, 22), "화면 클릭 → 시작", _centerStyle);
                }

                // [H] 누른 경우에만 도움말 표시
                if (controller.ShowHelp)
                {
                    GUI.DrawTexture(new Rect(14, sh - 92, 340, 78), _darkTex);
                    GUI.Label(new Rect(24, sh - 86, 320, 18), "[좌클릭 꾹] 바닥 쓰레기 훑기 / 큰 봉투 터뜨리기", _smallLabelStyle);
                    GUI.Label(new Rect(24, sh - 66, 320, 18), "[우클릭] 봉투 트럭 슛 · 문 밖 나가면 악취 환기", _smallLabelStyle);
                    GUI.Label(new Rect(24, sh - 46, 320, 18), "[Tab] 스킬트리 & 보물 정산 · [F] 손전등 · [H] 닫기", _smallLabelStyle);
                }
            }

            // 7. 최초 1회만 슬쩍 뜨고 사라지는 초간단 조작 힌트 (7초 후 완전 소멸)
            if (_introHintTimer > 0f && string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect hr = new Rect(sw / 2f - 230, sh - 40, 460, 26);
                GUI.DrawTexture(hr, _darkTex);
                _centerStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
                GUI.Label(hr, "[좌클릭 꾹] 훑기  ·  [우클릭] 트럭 슛  ·  [Tab] 강화  ·  [H] 도움말", _centerStyle);
            }
            else if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect tr = new Rect(sw / 2f - 240, sh - 42, 480, 28);
                GUI.DrawTexture(tr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(tr, manager.ToastMessage, _centerStyle);
            }
        }

        /// <summary>
        /// 상단 중앙 슬림 악취 바: 좌측 악취 아이콘 + 25/50/75/100% 직관적 색상 단계 (평소엔 글자 없이 깔끔!)
        /// </summary>
        private void DrawTopStenchBar(float sw)
        {
            int stage = manager.StenchStage;
            bool showText = manager.IsOutsideVentilating || stage >= 1;
            float totalW = 310f;
            float h = showText ? 46f : 32f;
            float x = (sw - totalW) / 2f, y = 14f;

            GUI.DrawTexture(new Rect(x, y, totalW, h), _darkTex);
            Texture2D colTex = stage == 0 ? _greenTex : stage == 1 ? _yellowTex : stage == 2 ? _orangeTex : stage == 3 ? _redTex : _purpleTex;

            // 좌측 악취 아이콘 (BMP ☣ — 폰트 경고 0건)
            Rect iconR = new Rect(x + 5, y + 4, 28, h - 8);
            GUI.DrawTexture(iconR, colTex);
            GUI.DrawTexture(new Rect(iconR.x + 2, iconR.y + 2, iconR.width - 4, iconR.height - 4), _darkTex);
            _centerStyle.normal.textColor = stage >= 3 ? new Color(1f, 0.55f, 0.95f) : Color.white;
            GUI.Label(iconR, "☣", _centerStyle);

            // 우측 4칸 색상 게이지 (25/50/75/100%)
            float bx = x + 40, by = y + 9, bw = totalW - 48;
            DrawBar(new Rect(bx, by, bw, 12), manager.StenchPercent / 100f, colTex);
            for (int k = 1; k <= 3; k++)
            {
                GUI.DrawTexture(new Rect(bx + bw * (k * 0.25f), by, 2, 12), _darkTex);
            }

            if (showText)
            {
                string status = manager.IsOutsideVentilating ? "환기 회복 중"
                    : stage == 1 ? "25% 이속↓"
                    : stage == 2 ? "50% 이속↓ · 손놀림↓"
                    : stage == 3 ? "75% 이속↓ · 손↓ · 어질 (문 밖 환기)"
                    : "100% 전체 디버프 강화! 문 밖으로!";
                _smallLabelStyle.normal.textColor = manager.IsOutsideVentilating ? new Color(0.55f, 0.95f, 1f)
                    : stage >= 3 ? new Color(1f, 0.52f, 0.48f) : new Color(0.95f, 0.86f, 0.55f);
                GUI.Label(new Rect(bx, by + 16, bw, 18), status, _smallLabelStyle);
                _smallLabelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }
        }

        /// <summary>
        /// 작은 정사각형 아이콘 노드 마인드맵 (커서 올릴 때만 하단 툴팁!) + 우측 보물 보관/즉시 슈킹
        /// * 모든 아이콘을 안전한 BMP 심볼로 교체해 Unity 콘솔 폰트 경고 0건 보장!
        /// </summary>
        private void DrawIconMindMapOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);
            _hasHover = false;

            float mapCenterX = sw * 0.37f;
            float mapCenterY = sh * 0.45f;

            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 14, sw, 28), $"스킬트리  |  보유 현금 ${manager.Money:N0}   ([Tab] 닫기)", _bigTitleStyle);

            Vector2 root = new Vector2(mapCenterX, mapCenterY);
            Vector2 nSweep = new Vector2(mapCenterX - 110, mapCenterY - 68);
            Vector2 nVac = new Vector2(mapCenterX - 215, mapCenterY - 120);
            Vector2 nFist = new Vector2(mapCenterX - 110, mapCenterY - 150);
            Vector2 nBeacon = new Vector2(mapCenterX - 215, mapCenterY - 190);

            Vector2 nBag = new Vector2(mapCenterX + 110, mapCenterY - 72);
            Vector2 nTruck = new Vector2(mapCenterX + 215, mapCenterY - 125);
            Vector2 nMagnet = new Vector2(mapCenterX + 215, mapCenterY - 32);

            Vector2 nMask = new Vector2(mapCenterX - 110, mapCenterY + 82);
            Vector2 nMop = new Vector2(mapCenterX - 215, mapCenterY + 58);
            Vector2 nScrub = new Vector2(mapCenterX - 215, mapCenterY + 146);

            Vector2 nLuck = new Vector2(mapCenterX + 110, mapCenterY + 82);
            Vector2 nPoker = new Vector2(mapCenterX + 215, mapCenterY + 122);

            DrawLine(root, nSweep, 3f); DrawLine(nSweep, nVac, 2.5f);
            DrawLine(root, nFist, 2.5f); DrawLine(nFist, nBeacon, 2.5f);
            DrawLine(root, nBag, 3f); DrawLine(nBag, nTruck, 2.5f); DrawLine(nTruck, nMagnet, 2.5f);
            DrawLine(root, nMask, 3f); DrawLine(nMask, nMop, 2.5f); DrawLine(nMop, nScrub, 2.5f);
            DrawLine(root, nLuck, 3f); DrawLine(nLuck, nPoker, 2.5f);

            // 중앙 코어
            Rect coreR = CenteredRect(root, 60, 60);
            GUI.DrawTexture(coreR, _goldTex);
            GUI.DrawTexture(new Rect(coreR.x + 3, coreR.y + 3, 54, 54), _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(coreR, "▲\n본부", _centerStyle);

            // 12개 정사각형 아이콘 노드 (BMP 안전 심볼만 사용 → 콘솔 에러/경고 0!)
            DrawIconNode(nSweep, "sweep", "◎", $"{manager.LvSweepRadius}/5", "줍기 반경 확대", $"한 번에 쓸어담는 반경 확대 (현재 {manager.SweepRadius:0.2}m)", manager.CostSweep, manager.LvSweepRadius >= 5, true, _orangeTex);
            DrawIconNode(nVac, "vacuum", "◉", manager.HasVacuumUnlocked ? "ON" : "0/1", "진공청소기 해금", "광역 폭풍 흡입 + 봉투 제한 완전 해제 (줍기 반경 Lv.2 필요)", manager.CostVacuum, manager.HasVacuumUnlocked, manager.LvSweepRadius >= 2, _orangeTex);
            DrawIconNode(nFist, "fist", "◆", $"{manager.LvFistPower}/4", "주먹 힘 강화", $"큰 쓰레기봉투 타격 데미지 증가 (현재 {manager.FistDamage:0.1})", manager.CostFist, manager.LvFistPower >= 4, true, _orangeTex);
            DrawIconNode(nBeacon, "beacon", "✦", manager.HasBeaconDetect ? "ON" : "0/1", "10% 빛기둥 탐지", "남은 쓰레기 10% 이하일 때 위치에 빛기둥 표시 (주먹 힘 Lv.1 필요)", manager.CostBeacon, manager.HasBeaconDetect, manager.LvFistPower >= 1, _orangeTex);

            DrawIconNode(nBag, "bag", "▣", $"{manager.LvBagSize}/4", "큰 쓰레기봉투", $"왼손 봉투 적재량 확대 (현재 {manager.HandBagCapacity}개)", manager.CostBag, manager.LvBagSize >= 4, true, _blueTex);
            DrawIconNode(nTruck, "truck", "▶▶", $"{manager.LvTruckSize}/3", "트럭 짐칸 확장", "문 밖 트럭 짐칸 확대 + 슛 골인 보너스 증가 (큰 봉투 Lv.1 필요)", manager.CostTruck, manager.LvTruckSize >= 3, manager.LvBagSize >= 1, _blueTex);
            DrawIconNode(nMagnet, "magnet", "Ω", manager.HasTruckMagnet ? "ON" : "0/1", "트럭 자석 압축기", "문 쪽으로 대충 던져도 트럭으로 자동 흡입 (트럭 확장 Lv.1 필요)", manager.CostMagnet, manager.HasTruckMagnet, manager.LvTruckSize >= 1, _blueTex);

            DrawIconNode(nMask, "mask", "☣", $"{manager.LvMask}/5", "마스크·방독면", $"악취 누적 속도 완화 (현재 -{manager.LvMask * 16}%)", manager.CostMask, manager.LvMask >= 5, true, _greenTex);
            DrawIconNode(nMop, "mop", "┴", manager.HasMopUnlocked ? "ON" : "0/1", "T자 대걸레 구매", "2단계 물티슈 → 2.6배속 시원한 대걸레 해금 (마스크 Lv.1 필요)", manager.CostMop, manager.HasMopUnlocked, manager.LvMask >= 1, _greenTex);
            DrawIconNode(nScrub, "scrub", "◈", $"{manager.LvScrubPower}/3", "강력 세제", "2단계 얼룩 닦기 속도 대폭 증가 (대걸레 필요)", manager.CostScrub, manager.LvScrubPower >= 3, manager.HasMopUnlocked, _greenTex);

            DrawIconNode(nLuck, "luck", "★", $"{manager.LvTreasureLuck}/3", "보물 감별 눈", "큰 봉투에서 보물 발굴 확률 및 가치 +20%/Lv", manager.CostLuck, manager.LvTreasureLuck >= 3, true, _goldTex);
            DrawIconNode(nPoker, "poker", "♦", $"{manager.LvPokerFace}/4", "뻔뻔한 연기력", "즉시 슈킹 시 집주인 해프닝·의심 확률 대폭 완화 (보물 눈 Lv.1 필요)", manager.CostPoker, manager.LvPokerFace >= 4, manager.LvTreasureLuck >= 1, _goldTex);

            // 하단 중앙 호버 툴팁 카드
            Rect tipBox = new Rect(mapCenterX - 265, sh - 130, 530, 68);
            GUI.DrawTexture(tipBox, _darkTex);
            if (_hasHover)
            {
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 8, 350, 22), _hoverTitle, _titleStyle);
                _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                GUI.Label(new Rect(tipBox.x + 370, tipBox.y + 8, 146, 22), _hoverPrice, _titleStyle);
                _titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 34, 502, 28), _hoverDesc, _labelStyle);
            }
            else
            {
                _centerStyle.normal.textColor = new Color(0.62f, 0.68f, 0.76f);
                GUI.Label(tipBox, "아이콘에 커서를 올리면 설명 표시 · 클릭하여 강화", _centerStyle);
            }

            // 우측 패널: 발굴 보물함 (즉시 보관 vs 즉시 슈킹)
            float rx = sw * 0.68f, rw = sw * 0.30f;
            Rect treasurePanel = new Rect(rx, 60, rw, sh - 126);
            GUI.DrawTexture(treasurePanel, _darkTex);
            GUI.Label(new Rect(rx + 14, 70, rw - 28, 24), "★ 발굴 보물함 (즉시 정산)", _titleStyle);
            GUI.Label(new Rect(rx + 14, 96, rw - 28, 20), "급전 필요 시 즉시 슈킹 (집주인 해프닝 주의)", _smallLabelStyle);

            float iy = 126f;
            int shown = 0;
            foreach (var item in manager.DiscoveredTreasures)
            {
                if (item.resolved) continue;
                if (iy + 72 > treasurePanel.yMax - 8) break;
                shown++;

                Rect card = new Rect(rx + 12, iy, rw - 24, 66);
                GUI.DrawTexture(card, item.isDesignatedLostItem ? _redTex : item.rarity == TreasureRarity.Legendary ? _goldTex : _barBgTex);
                GUI.DrawTexture(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), _panelTex);

                GUI.Label(new Rect(card.x + 10, card.y + 6, card.width - 20, 20), item.itemName, _labelStyle);
                float btnW = (card.width - 28) / 2f;
                int tip = item.isDesignatedLostItem ? manager.lostItemTip : Mathf.Max(20, item.value / 5);
                if (GUI.Button(new Rect(card.x + 10, card.y + 31, btnW, 27), $"보관 +${tip}", _smallBtnStyle))
                {
                    manager.ResolveTreasure(item, false);
                }
                string stealLabel = item.isDesignatedLostItem ? $"슈킹 +${item.value} (100%!)" : $"슈킹 +${item.value}";
                if (GUI.Button(new Rect(card.x + 18 + btnW, card.y + 31, btnW, 27), stealLabel, _smallBtnStyle))
                {
                    manager.ResolveTreasure(item, true);
                }
                iy += 74f;
            }
            if (shown == 0)
            {
                _centerStyle.normal.textColor = new Color(0.58f, 0.63f, 0.70f);
                GUI.Label(new Rect(rx + 12, 180, rw - 24, 52), "발굴된 보물 없음\n큰 봉투를 터뜨리면 여기에 쌓입니다", _centerStyle);
            }

            if (GUI.Button(new Rect(sw / 2f - 120, sh - 48, 240, 34), "돌아가기 ([Tab])", _smallBtnStyle))
            {
                manager.ToggleMindMap();
            }
        }

        private void DrawIconNode(Vector2 center, string id, string icon, string lvText, string title, string desc, int cost, bool isMax, bool unlocked, Texture2D accentTex)
        {
            Rect r = CenteredRect(center, 58, 58);
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
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 52, 52), _darkTex);

            Color prev = GUI.contentColor;
            GUI.contentColor = isMax ? new Color(1f, 0.9f, 0.4f) : canBuy ? Color.white : new Color(0.45f, 0.48f, 0.54f);
            if (GUI.Button(new Rect(r.x + 4, r.y + 4, 50, 34), icon, _iconNodeStyle))
            {
                if (canBuy) manager.BuyNode(id);
            }
            GUI.contentColor = prev;

            _centerStyle.normal.textColor = isMax ? new Color(1f, 0.9f, 0.35f) : canBuy ? new Color(0.6f, 1f, 0.7f) : new Color(0.58f, 0.60f, 0.64f);
            GUI.Label(new Rect(r.x, r.y + 38, 58, 18), lvText, _centerStyle);
        }

        private void DrawRoomCompletedOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            float w = 540, h = 310;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 4, box.y - 4, w + 8, h + 8), manager.LastWasCaught ? _redTex : _goldTex);
            GUI.DrawTexture(box, _panelTex);

            float x = box.x + 24, y = box.y + 22;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 48, 30), $"원룸 100% 완청 완료  |  {stars}", _bigTitleStyle);

            string verdict = manager.LastWasCaught
                ? "[발각됨] 평판 1점 & 보너스 50% 삭감 (업그레이드 유지)"
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
