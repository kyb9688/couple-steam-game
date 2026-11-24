using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.3 HUD + 상단 악취 바(25/50/75/100% 단계별 누적 디버프) +
    /// 작은 정사각형 아이콘 노드 마인드맵(마우스 호버 시 상세 툴팁!) + 우측 발굴 보물함(즉시 슈킹 + 재밌는 해프닝).
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _boxStyle, _titleStyle, _bigTitleStyle, _labelStyle, _centerStyle, _iconNodeStyle, _smallBtnStyle;
        private Texture2D _darkTex, _panelTex, _barBgTex, _greenTex, _yellowTex, _orangeTex, _redTex, _purpleTex, _blueTex, _goldTex, _lineTex, _whiteTex;
        private bool _inited;

        // Hover tooltip state for Mind-Map
        private string _hoverTitle, _hoverDesc, _hoverPrice;
        private bool _hasHover;

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
            _darkTex = MakeTex(new Color(0.07f, 0.08f, 0.11f, 0.86f));
            _panelTex = MakeTex(new Color(0.09f, 0.10f, 0.14f, 0.95f));
            _barBgTex = MakeTex(new Color(0.18f, 0.20f, 0.25f, 0.95f));
            _greenTex = MakeTex(new Color(0.24f, 0.80f, 0.46f, 1f));
            _yellowTex = MakeTex(new Color(0.96f, 0.78f, 0.18f, 1f));
            _orangeTex = MakeTex(new Color(0.95f, 0.48f, 0.14f, 1f));
            _redTex = MakeTex(new Color(0.88f, 0.22f, 0.22f, 1f));
            _purpleTex = MakeTex(new Color(0.68f, 0.24f, 0.90f, 1f));
            _blueTex = MakeTex(new Color(0.20f, 0.56f, 0.94f, 1f));
            _goldTex = MakeTex(new Color(1.0f, 0.84f, 0.22f, 1f));
            _lineTex = MakeTex(new Color(0.60f, 0.66f, 0.78f, 0.72f));
            _whiteTex = MakeTex(Color.white);

            _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkTex }, padding = new RectOffset(10, 10, 8, 8) };
            _titleStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bigTitleStyle = new GUIStyle(_titleStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) } };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _iconNodeStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _smallBtnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
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

            // 1. 악취 디버프 화면 가장자리 초록/보라 안개 (멀미 유발 카메라 회전 없이 시각적 전달!)
            int stStage = manager.StenchStage;
            if (stStage >= 1)
            {
                float alpha = stStage == 1 ? 0.12f : stStage == 2 ? 0.22f : stStage == 3 ? 0.34f : (0.42f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
                Color tint = stStage == 4 ? new Color(0.45f, 0.15f, 0.55f, alpha) : new Color(0.28f, 0.45f, 0.12f, alpha);
                float edge = stStage >= 3 ? 28f : 14f;
                Color prev = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(0, 0, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - edge, sw, edge), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, edge, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - edge, 0, edge, sh), _whiteTex);
                GUI.color = prev;
            }

            // 2. 상단 중앙 「악취 바」 (좌측 악취 아이콘 + 25/50/75/100% 단계 색상 & 누적 디버프 뱃지!)
            DrawTopStenchBar(sw);

            // 3. 좌상단 진행도 · 현금
            GUI.Box(new Rect(16, 16, 260, 96), "", _boxStyle);
            string pLabel = manager.Phase == GamePhase.Phase1_TrashRemoval
                ? $"1단계 쓰레기 ({manager.Phase1Percent:0.0}%)"
                : $"2단계 바닥 닦기 ({manager.Phase2Cleaned}/{manager.Phase2Total})";
            GUI.Label(new Rect(28, 22, 238, 22), pLabel, _titleStyle);
            GUI.Label(new Rect(28, 46, 238, 20), $"보유 현금: ${manager.Money:N0}", _labelStyle);
            DrawBar(new Rect(28, 70, 234, 10),
                (manager.Phase == GamePhase.Phase1_TrashRemoval ? manager.Phase1Percent : manager.Phase2Percent) / 100f,
                manager.Phase == GamePhase.Phase1_TrashRemoval ? _greenTex : _blueTex);
            string equip = manager.Phase == GamePhase.Phase1_TrashRemoval
                ? (manager.HasVacuumUnlocked ? "장비: 진공청소기 (무한 흡입!)" : "장비: 맨손 훑기 · [우클릭] 슛")
                : (manager.HasMopUnlocked ? "장비: T자 대걸레" : "장비: 물티슈 (마우스 흔들기)");
            GUI.Label(new Rect(28, 86, 238, 18), equip, _labelStyle);

            // 4. 우상단 [Tab] 마인드맵 & 보물 알림 + 집주인 눈치
            GUI.Box(new Rect(sw - 276, 16, 260, 110), "", _boxStyle);
            int avail = manager.CountAffordableUpgrades();
            int pend = manager.PendingTreasureCount;
            bool blink = Mathf.Sin(Time.time * 8f) > 0f;
            _titleStyle.normal.textColor = (avail > 0 || pend > 0) && blink ? new Color(0.55f, 1f, 0.65f) : Color.white;
            GUI.Label(new Rect(sw - 264, 22, 240, 22),
                $"[Tab] 스킬트리 {(avail > 0 ? $"({avail}개 가능!)" : "")}", _titleStyle);
            _titleStyle.normal.textColor = Color.white;

            if (pend > 0)
            {
                _labelStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                GUI.Label(new Rect(sw - 264, 46, 240, 20), $"✨ 발굴 보물 {pend}개 대기 중! ([Tab] 정산)", _labelStyle);
                _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }
            else
            {
                GUI.Label(new Rect(sw - 264, 46, 240, 20), "발굴 보물: 없음 (큰 봉투를 터뜨리세요)", _labelStyle);
            }

            string[] eyeNames = { "🟢 평온", "🟡 의아함", "🟠 의심!", "🔴 들통 직전!!" };
            GUI.Label(new Rect(sw - 264, 72, 240, 20), $"집주인 눈치: {eyeNames[manager.SuspicionStage]}", _labelStyle);
            GUI.Label(new Rect(sw - 264, 96, 240, 18), "[F] 손전등 · [H] 도움말", _labelStyle);

            // 5. 즉시 슈킹 해프닝 알림 (집주인 카톡 / 문 앞 이웃 10초 등장)
            if (manager.RemoteWatchTimer > 0f)
            {
                Rect kr = new Rect(sw - 390, 136, 374, 52);
                GUI.DrawTexture(new Rect(kr.x - 2, kr.y - 2, kr.width + 4, kr.height + 4), _redTex);
                GUI.DrawTexture(kr, _darkTex);
                GUI.Label(new Rect(kr.x + 10, kr.y + 6, kr.width - 20, 40), $"{manager.LandlordMessage} ({manager.RemoteWatchTimer:0}s)", _labelStyle);
            }
            if (manager.DoorVisitorTimer > 0f)
            {
                Rect dr = new Rect(sw / 2f - 220, 96, 440, 34);
                GUI.DrawTexture(new Rect(dr.x - 2, dr.y - 2, dr.width + 4, dr.height + 4), _orangeTex);
                GUI.DrawTexture(dr, _darkTex);
                _centerStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);
                GUI.Label(dr, $"🚪 문 앞 집주인 등장! ({manager.DoorVisitorTimer:0.0}초간 트럭 슛 금지!)", _centerStyle);
            }

            // 6. 중앙 시각적 조준점 + 봉투 가득 참 가시성 바 (조준점 바로 아래!)
            if (controller != null)
            {
                Texture2D dot = controller.AimIsPile ? _orangeTex
                    : controller.AimIsSwarmNear ? _greenTex
                    : controller.AimIsStain ? _blueTex : _barBgTex;
                float ds = (controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain) ? 9f : 5f;
                GUI.DrawTexture(new Rect(sw / 2f - ds / 2f, sh / 2f - ds / 2f, ds, ds), dot);

                // 봉투 게이지 (Phase 1 & 청소기 미해금 시, 조준점 바로 아래 선명하게!)
                if (manager.Phase == GamePhase.Phase1_TrashRemoval && !manager.HasVacuumUnlocked && manager.HandBagCount > 0)
                {
                    float fill = Mathf.Clamp01(manager.HandBagCount / (float)manager.HandBagCapacity);
                    DrawBar(new Rect(sw / 2f - 55, sh / 2f + 18, 110, 8), fill, manager.IsHandBagFull ? _goldTex : _greenTex);
                    if (manager.IsHandBagFull)
                    {
                        _centerStyle.normal.textColor = new Color(1f, 0.90f, 0.30f);
                        GUI.Label(new Rect(sw / 2f - 140, sh / 2f + 28, 280, 22), "봉투 가득! 문 밖 트럭에 [우클릭] 슛!", _centerStyle);
                    }
                }
                else if (controller.AimProgress01 > 0.01f)
                {
                    DrawBar(new Rect(sw / 2f - 55, sh / 2f + 18, 110, 8),
                        controller.AimProgress01, controller.AimIsStain ? _blueTex : _orangeTex);
                }

                if (controller.NeedMouseShakeHint)
                {
                    _centerStyle.normal.textColor = new Color(0.65f, 0.92f, 1f);
                    GUI.Label(new Rect(sw / 2f - 130, sh / 2f + 30, 260, 22), "↔ 마우스를 좌우로 흔들어 닦으세요", _centerStyle);
                }
                else if (Cursor.lockState != CursorLockMode.Locked)
                {
                    GUI.Box(new Rect(sw / 2f - 100, sh / 2f + 26, 200, 28), "", _boxStyle);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(sw / 2f - 100, sh / 2f + 29, 200, 22), "화면 클릭 → 조작 시작", _centerStyle);
                }

                if (controller.ShowHelp)
                {
                    GUI.Box(new Rect(16, sh - 116, 390, 100), "", _boxStyle);
                    GUI.Label(new Rect(28, sh - 110, 370, 20), "조작법 ([H] 닫기)", _titleStyle);
                    GUI.Label(new Rect(28, sh - 86, 370, 20), "• 바닥 1,000개 쓰레기: [좌클릭 꾹 + 시선 훑기]로 동전처럼 싹쓸이", _labelStyle);
                    GUI.Label(new Rect(28, sh - 66, 370, 20), "• 봉투 꽉 차면: 열린 문 밖 트럭에 [우클릭] 슛 · 문 밖으로 나가면 환기", _labelStyle);
                    GUI.Label(new Rect(28, sh - 46, 370, 20), "• [Tab]: 아이콘 마인드맵 강화 & 발굴 보물 즉시 보관/슈킹", _labelStyle);
                }
            }

            // 토스트 알림 (하단 중앙)
            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect tr = new Rect(sw / 2f - 300, sh - 48, 600, 34);
                GUI.Box(tr, "", _boxStyle);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(tr, manager.ToastMessage, _centerStyle);
            }
        }

        /// <summary>
        /// 요청하신 상단 악취 바: 좌측 악취 아이콘 + 25/50/75/100% 직관적 색상 단계 & 누적 디버프 표시!
        /// </summary>
        private void DrawTopStenchBar(float sw)
        {
            float barW = 340f, totalW = 400f, h = 62f;
            float x = (sw - totalW) / 2f, y = 14f;
            GUI.Box(new Rect(x, y, totalW, h), "", _boxStyle);

            int stage = manager.StenchStage;
            Texture2D colTex = stage == 0 ? _greenTex : stage == 1 ? _yellowTex : stage == 2 ? _orangeTex : stage == 3 ? _redTex : _purpleTex;

            // 좌측 악취 아이콘 박스
            Rect iconR = new Rect(x + 8, y + 8, 44, 46);
            GUI.DrawTexture(iconR, colTex);
            GUI.DrawTexture(new Rect(iconR.x + 2, iconR.y + 2, iconR.width - 4, iconR.height - 4), _darkTex);
            _centerStyle.normal.textColor = stage >= 3 ? new Color(1f, 0.55f, 0.95f) : Color.white;
            GUI.Label(iconR, "☣\n악취", _centerStyle);

            // 우측 가로 바 + 25 / 50 / 75 / 100% 눈금
            float bx = x + 58, by = y + 12;
            DrawBar(new Rect(bx, by, barW - 10, 16), manager.StenchPercent / 100f, colTex);
            // 25%, 50%, 75% 구간 구분선
            float bw = barW - 10;
            for (int k = 1; k <= 3; k++)
            {
                GUI.DrawTexture(new Rect(bx + bw * (k * 0.25f), by, 2, 16), _darkTex);
            }

            // 단계별 누적 디버프 텍스트 표시
            string status;
            if (manager.IsOutsideVentilating) status = "💨 문 밖 신선한 공기 — 환기 회복 중!";
            else if (stage == 0) status = "쾌적 (25% 미만)";
            else if (stage == 1) status = "25% [이속↓]";
            else if (stage == 2) status = "50% [이속↓ + 손놀림↓]";
            else if (stage == 3) status = "75% [이속↓ + 손놀림↓ + 어질어질] — 문 밖 환기 권장!";
            else status = "100% [극심한 악취! 모든 디버프 강화!] — 밖으로 나가 숨 쉬세요!";

            _labelStyle.normal.textColor = stage >= 3 ? new Color(1f, 0.55f, 0.50f) : Color.white;
            GUI.Label(new Rect(bx, by + 22, bw, 22), status, _labelStyle);
            _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
        }

        /// <summary>
        /// 작은 정사각형 아이콘 노드 마인드맵 (커서 올릴 때만 하단 툴팁 표시!) + 오른쪽 보물 보관/즉시 슈킹 패널
        /// </summary>
        private void DrawIconMindMapOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);
            _hasHover = false;

            float mapCenterX = sw * 0.37f;
            float mapCenterY = sh * 0.45f;

            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 14, sw, 30), $"스킬트리 마인드맵 · 보유 현금: ${manager.Money:N0}   ([Tab] 닫기)", _bigTitleStyle);

            // 작은 아이콘 노드 좌표 (중앙 코어 → 4갈래)
            Vector2 root = new Vector2(mapCenterX, mapCenterY);
            // 좌상: 훑기/주먹/청소기/탐지
            Vector2 nSweep = new Vector2(mapCenterX - 115, mapCenterY - 70);
            Vector2 nVac = new Vector2(mapCenterX - 225, mapCenterY - 125);
            Vector2 nFist = new Vector2(mapCenterX - 115, mapCenterY - 155);
            Vector2 nBeacon = new Vector2(mapCenterX - 225, mapCenterY - 195);
            // 우상: 봉투/트럭/자석
            Vector2 nBag = new Vector2(mapCenterX + 115, mapCenterY - 75);
            Vector2 nTruck = new Vector2(mapCenterX + 220, mapCenterY - 130);
            Vector2 nMagnet = new Vector2(mapCenterX + 220, mapCenterY - 35);
            // 좌하: 마스크/대걸레/세제
            Vector2 nMask = new Vector2(mapCenterX - 115, mapCenterY + 85);
            Vector2 nMop = new Vector2(mapCenterX - 220, mapCenterY + 60);
            Vector2 nScrub = new Vector2(mapCenterX - 220, mapCenterY + 150);
            // 우하: 보물눈/뻔뻔함
            Vector2 nLuck = new Vector2(mapCenterX + 115, mapCenterY + 85);
            Vector2 nPoker = new Vector2(mapCenterX + 220, mapCenterY + 125);

            // 연결선
            DrawLine(root, nSweep, 3f); DrawLine(nSweep, nVac, 2.5f);
            DrawLine(root, nFist, 2.5f); DrawLine(nFist, nBeacon, 2.5f);
            DrawLine(root, nBag, 3f); DrawLine(nBag, nTruck, 2.5f); DrawLine(nTruck, nMagnet, 2.5f);
            DrawLine(root, nMask, 3f); DrawLine(nMask, nMop, 2.5f); DrawLine(nMop, nScrub, 2.5f);
            DrawLine(root, nLuck, 3f); DrawLine(nLuck, nPoker, 2.5f);

            // 중앙 코어 아이콘
            Rect coreR = CenteredRect(root, 64, 64);
            GUI.DrawTexture(coreR, _goldTex);
            GUI.DrawTexture(new Rect(coreR.x + 3, coreR.y + 3, 58, 58), _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(coreR, "🏠\n본부", _centerStyle);

            // 12개 작은 정사각형 아이콘 노드 (호버 시에만 하단 툴팁!)
            DrawIconNode(nSweep, "sweep", "🖐", $"{manager.LvSweepRadius}/5", "줍기 반경 확대", $"한 번에 쓸어담는 반경 확대 (현재 {manager.SweepRadius:0.2}m)", manager.CostSweep, manager.LvSweepRadius >= 5, true, _orangeTex);
            DrawIconNode(nVac, "vacuum", "🌀", manager.HasVacuumUnlocked ? "ON" : "0/1", "진공청소기 해금!", "광역 폭풍 흡입 + 봉투 제한 완전 해제! (줍기 반경 Lv.2 필요)", manager.CostVacuum, manager.HasVacuumUnlocked, manager.LvSweepRadius >= 2, _orangeTex);
            DrawIconNode(nFist, "fist", "👊", $"{manager.LvFistPower}/4", "주먹 힘 강화", $"큰 쓰레기봉투 타격 데미지 증가 (현재 {manager.FistDamage:0.1})", manager.CostFist, manager.LvFistPower >= 4, true, _orangeTex);
            DrawIconNode(nBeacon, "beacon", "✨", manager.HasBeaconDetect ? "ON" : "0/1", "10% 빛기둥 탐지", "남은 쓰레기가 10% 이하일 때 위치에 빛기둥 표시 (주먹 힘 Lv.1 필요)", manager.CostBeacon, manager.HasBeaconDetect, manager.LvFistPower >= 1, _orangeTex);

            DrawIconNode(nBag, "bag", "🛍", $"{manager.LvBagSize}/4", "큰 쓰레기봉투", $"왼손 봉투 적재량 확대 (현재 {manager.HandBagCapacity}개)", manager.CostBag, manager.LvBagSize >= 4, true, _blueTex);
            DrawIconNode(nTruck, "truck", "🚚", $"{manager.LvTruckSize}/3", "트럭 짐칸 확장", "문 밖 트럭 짐칸 확대 + 슛 골인 보너스 증가 (큰 봉투 Lv.1 필요)", manager.CostTruck, manager.LvTruckSize >= 3, manager.LvBagSize >= 1, _blueTex);
            DrawIconNode(nMagnet, "magnet", "🧲", manager.HasTruckMagnet ? "ON" : "0/1", "트럭 자석 압축기", "문 쪽으로 대충 던져도 트럭으로 자동 흡입! (트럭 확장 Lv.1 필요)", manager.CostMagnet, manager.HasTruckMagnet, manager.LvTruckSize >= 1, _blueTex);

            DrawIconNode(nMask, "mask", "😷", $"{manager.LvMask}/5", "마스크·방독면", $"악취 누적 속도 완화 (현재 -{manager.LvMask * 16}%)", manager.CostMask, manager.LvMask >= 5, true, _greenTex);
            DrawIconNode(nMop, "mop", "🧹", manager.HasMopUnlocked ? "ON" : "0/1", "T자 대걸레 구매", "2단계 물티슈 → 2.6배속 시원한 대걸레 해금! (마스크 Lv.1 필요)", manager.CostMop, manager.HasMopUnlocked, manager.LvMask >= 1, _greenTex);
            DrawIconNode(nScrub, "scrub", "🫧", $"{manager.LvScrubPower}/3", "강력 세제", "2단계 얼룩 닦기 속도 대폭 증가 (대걸레 필요)", manager.CostScrub, manager.LvScrubPower >= 3, manager.HasMopUnlocked, _greenTex);

            DrawIconNode(nLuck, "luck", "🎁", $"{manager.LvTreasureLuck}/3", "보물 감별 눈", "큰 봉투에서 보물 발굴 확률 및 가치 +20%/Lv", manager.CostLuck, manager.LvTreasureLuck >= 3, true, _goldTex);
            DrawIconNode(nPoker, "poker", "🎭", $"{manager.LvPokerFace}/4", "뻔뻔한 연기력", "즉시 슈킹 시 집주인 해프닝·의심 확률 대폭 완화 (보물 눈 Lv.1 필요)", manager.CostPoker, manager.LvPokerFace >= 4, manager.LvTreasureLuck >= 1, _goldTex);

            // 하단 중앙 호버 툴팁 카드 (마우스 올렸을 때만 표시!)
            Rect tipBox = new Rect(mapCenterX - 270, sh - 135, 540, 74);
            GUI.Box(tipBox, "", _boxStyle);
            if (_hasHover)
            {
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 8, 360, 24), _hoverTitle, _titleStyle);
                _titleStyle.normal.textColor = new Color(1f, 0.88f, 0.30f);
                GUI.Label(new Rect(tipBox.x + 380, tipBox.y + 8, 146, 24), _hoverPrice, _titleStyle);
                _titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(tipBox.x + 14, tipBox.y + 36, 512, 30), _hoverDesc, _labelStyle);
            }
            else
            {
                _centerStyle.normal.textColor = new Color(0.65f, 0.70f, 0.78f);
                GUI.Label(tipBox, "아이콘 위에 마우스를 올리면 상세 설명과 가격이 표시됩니다 (클릭하여 즉시 강화)", _centerStyle);
            }

            // 우측 패널: 발굴 보물함 (즉시 정직 보관 vs 즉시 슈킹 + 재밌는 해프닝!)
            float rx = sw * 0.68f, rw = sw * 0.30f;
            Rect treasurePanel = new Rect(rx, 64, rw, sh - 135);
            GUI.Box(treasurePanel, "", _boxStyle);
            GUI.Label(new Rect(rx + 14, 74, rw - 28, 26), $"💎 발굴 보물함 (즉시 정산 가능)", _titleStyle);
            GUI.Label(new Rect(rx + 14, 100, rw - 28, 22), "급전이 필요하면 즉시 슈킹! (단, 집주인 해프닝 주의)", _labelStyle);

            float iy = 132f;
            int shown = 0;
            foreach (var item in manager.DiscoveredTreasures)
            {
                if (item.resolved) continue;
                if (iy + 74 > treasurePanel.yMax - 10) break;
                shown++;

                Rect card = new Rect(rx + 12, iy, rw - 24, 68);
                GUI.DrawTexture(card, item.isDesignatedLostItem ? _redTex : item.rarity == TreasureRarity.Legendary ? _goldTex : _barBgTex);
                GUI.DrawTexture(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), _darkTex);

                GUI.Label(new Rect(card.x + 10, card.y + 6, card.width - 20, 22), item.itemName, _labelStyle);
                float btnW = (card.width - 28) / 2f;
                int tip = item.isDesignatedLostItem ? manager.lostItemTip : Mathf.Max(20, item.value / 5);
                if (GUI.Button(new Rect(card.x + 10, card.y + 32, btnW, 28), $"😇 보관 +${tip}", _smallBtnStyle))
                {
                    manager.ResolveTreasure(item, false);
                }
                string stealLabel = item.isDesignatedLostItem ? $"😈 슈킹 +${item.value} (100%!)" : $"😈 슈킹 +${item.value}";
                if (GUI.Button(new Rect(card.x + 18 + btnW, card.y + 32, btnW, 28), stealLabel, _smallBtnStyle))
                {
                    manager.ResolveTreasure(item, true);
                }
                iy += 76f;
            }
            if (shown == 0)
            {
                _centerStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);
                GUI.Label(new Rect(rx + 12, 180, rw - 24, 60), "아직 발굴한 보물이 없습니다.\n방 안의 큰 봉투를 퍽!퍽! 터뜨려 보세요!", _centerStyle);
            }

            // 하단 닫기 버튼
            if (GUI.Button(new Rect(sw / 2f - 140, sh - 52, 280, 38), "▶ 방으로 돌아가기 ([Tab])", _smallBtnStyle))
            {
                manager.ToggleMindMap();
            }
        }

        private void DrawIconNode(Vector2 center, string id, string icon, string lvText, string title, string desc, int cost, bool isMax, bool unlocked, Texture2D accentTex)
        {
            Rect r = CenteredRect(center, 60, 60);
            bool canBuy = unlocked && !isMax && manager.Money >= cost;

            // Hover detection
            if (r.Contains(Event.current.mousePosition))
            {
                _hasHover = true;
                _hoverTitle = $"{icon} {title}  ({lvText})";
                _hoverDesc = desc;
                _hoverPrice = isMax ? "MAX 완료" : !unlocked ? "선행 노드 필요" : $"비용: ${cost:N0}";
            }

            Texture2D frame = isMax ? _goldTex : canBuy ? accentTex : _barBgTex;
            GUI.DrawTexture(r, frame);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 54, 54), _darkTex);

            Color prev = GUI.contentColor;
            GUI.contentColor = isMax ? new Color(1f, 0.9f, 0.4f) : canBuy ? Color.white : new Color(0.45f, 0.48f, 0.54f);
            if (GUI.Button(new Rect(r.x + 4, r.y + 4, 52, 36), icon, _iconNodeStyle))
            {
                if (canBuy) manager.BuyNode(id);
            }
            GUI.contentColor = prev;

            // 작은 레벨 뱃지 (하단)
            _centerStyle.normal.textColor = isMax ? new Color(1f, 0.9f, 0.35f) : canBuy ? new Color(0.6f, 1f, 0.7f) : new Color(0.6f, 0.62f, 0.66f);
            GUI.Label(new Rect(r.x, r.y + 40, 60, 18), lvText, _centerStyle);
        }

        private void DrawRoomCompletedOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            float w = 560, h = 330;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 4, box.y - 4, w + 8, h + 8), manager.LastWasCaught ? _redTex : _goldTex);
            GUI.DrawTexture(box, _panelTex);

            float x = box.x + 24, y = box.y + 22;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 48, 32), $"원룸 100% 완청 완료!   별점: {stars}", _bigTitleStyle);

            string verdict = manager.LastWasCaught
                ? "[발각됨!] 평판 1점 & 보너스 수당 50% 삭감 (업그레이드·장비는 유지!)"
                : manager.StolenCount == 0
                    ? "[정직 완청!] 청소비 전액 + 별점 5점 달성!"
                    : $"[완전범죄 성공!] 장물 ${manager.StolenValue:N0} + 청소비 전액 획득!";
            GUI.Label(new Rect(x, y + 54, w - 48, 28), verdict, _centerStyle);
            GUI.Label(new Rect(x, y + 96, w - 48, 56), $"의뢰인 리뷰: {manager.LastReviewText}", _labelStyle);
            GUI.Label(new Rect(x, y + 164, w - 48, 26), $"현재 보유 현금: ${manager.Money:N0}", _centerStyle);

            if (GUI.Button(new Rect(x + 40, box.y + h - 74, w - 128, 48), "▶ 강화된 상태로 단골 원룸 재발 의뢰 다시 플레이!", _smallBtnStyle))
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
