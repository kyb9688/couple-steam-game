using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.2 시각 중심 HUD + 마인드맵 스킬트리(연결선 포함) + 화려한 3.5초 좌/우 본능 가챠 슈킹 팝업.
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bigTitleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _nodeBtnStyle;
        private Texture2D _darkTex, _panelTex, _barBgTex, _greenTex, _yellowTex, _orangeTex, _redTex, _blueTex, _goldTex, _lineTex, _whiteTex;
        private bool _inited;

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
            _darkTex = MakeTex(new Color(0.07f, 0.08f, 0.10f, 0.86f));
            _panelTex = MakeTex(new Color(0.10f, 0.11f, 0.14f, 0.94f));
            _barBgTex = MakeTex(new Color(0.18f, 0.20f, 0.25f, 0.95f));
            _greenTex = MakeTex(new Color(0.24f, 0.80f, 0.46f, 1f));
            _yellowTex = MakeTex(new Color(0.96f, 0.78f, 0.18f, 1f));
            _orangeTex = MakeTex(new Color(0.95f, 0.48f, 0.14f, 1f));
            _redTex = MakeTex(new Color(0.88f, 0.22f, 0.22f, 1f));
            _blueTex = MakeTex(new Color(0.20f, 0.56f, 0.94f, 1f));
            _goldTex = MakeTex(new Color(1.0f, 0.84f, 0.22f, 1f));
            _lineTex = MakeTex(new Color(0.65f, 0.70f, 0.80f, 0.7f));
            _whiteTex = MakeTex(Color.white);

            _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkTex }, padding = new RectOffset(10, 10, 8, 8) };
            _titleStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _bigTitleStyle = new GUIStyle(_titleStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.92f, 0.94f, 0.96f) } };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _nodeBtnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 12, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
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
                case GamePhase.GachaStealPopup:
                    DrawPlayingHud();
                    DrawGachaInstinctPopup();
                    break;
                case GamePhase.MindMapUpgrade:
                    DrawMindMapOverlay();
                    break;
                case GamePhase.RoomCompleted:
                    DrawRoomCompletedOverlay();
                    break;
            }
        }

        private void DrawPlayingHud()
        {
            float sw = Screen.width, sh = Screen.height;

            // 문 밖 인기척(노크) 3.5초 붉은 테두리 경고!
            if (manager.IsKnockAlertActive)
            {
                float alpha = 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
                Color prev = GUI.color;
                GUI.color = new Color(1f, 0.15f, 0.15f, alpha);
                GUI.DrawTexture(new Rect(0, 0, sw, 10), _whiteTex);
                GUI.DrawTexture(new Rect(0, sh - 10, sw, 10), _whiteTex);
                GUI.DrawTexture(new Rect(0, 0, 10, sh), _whiteTex);
                GUI.DrawTexture(new Rect(sw - 10, 0, 10, sh), _whiteTex);
                GUI.color = prev;

                GUI.Box(new Rect(sw / 2f - 170, 60, 340, 34), "", _boxStyle);
                _centerStyle.normal.textColor = new Color(1f, 0.45f, 0.40f);
                GUI.Label(new Rect(sw / 2f - 170, 64, 340, 26), "똑똑... 문 밖 인기척! (지금 슈킹하면 위험!)", _centerStyle);
            }

            // 좌상단 컴팩트 상태 패널
            GUI.Box(new Rect(16, 16, 330, 152), "", _boxStyle);
            string phaseLabel = manager.Phase == GamePhase.Phase1_TrashRemoval
                ? $"1단계 · 쓰레기 철거 ({manager.Phase1Cleaned}/{manager.Phase1Total})"
                : $"2단계 · 바닥 닦기 ({manager.Phase2Cleaned}/{manager.Phase2Total})";
            GUI.Label(new Rect(28, 22, 308, 24), $"{phaseLabel}  · {manager.TurnCount}턴", _titleStyle);
            GUI.Label(new Rect(28, 48, 308, 20), $"보유 현금: ${manager.Money:N0}", _labelStyle);

            float pct = manager.Phase == GamePhase.Phase1_TrashRemoval ? manager.Phase1Percent : manager.Phase2Percent;
            DrawBar(new Rect(28, 72, 304, 10), pct / 100f, manager.Phase == GamePhase.Phase1_TrashRemoval ? _greenTex : _blueTex);

            // 행동 스태미나 바 (시간 제한 없음!)
            float stRatio = Mathf.Clamp01(manager.Stamina / manager.MaxStamina);
            Texture2D stTex = stRatio > 0.45f ? _greenTex : stRatio > 0.22f ? _yellowTex : _redTex;
            GUI.Label(new Rect(28, 88, 308, 18), $"체력 (행동 시 소모 · 시간제한 없음): {manager.Stamina:0}/{manager.MaxStamina:0}", _labelStyle);
            DrawBar(new Rect(28, 108, 304, 11), stRatio, stTex);

            // 왼손 쓰레기봉투 게이지 (Phase 1) or 현재 닦기 도구 (Phase 2)
            if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                string bagHint = manager.IsHandBagFull
                    ? $"봉투 가득! ({manager.HandBagCount}/{manager.HandBagCapacity}) → 문 밖 트럭에 [우클릭] 슛!"
                    : $"왼손 봉투: {manager.HandBagCount} / {manager.HandBagCapacity}  (트럭에 [우클릭] 투척)";
                _labelStyle.normal.textColor = manager.IsHandBagFull ? new Color(1f, 0.88f, 0.30f) : new Color(0.92f, 0.94f, 0.96f);
                GUI.Label(new Rect(28, 128, 308, 22), bagHint, _labelStyle);
                _labelStyle.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            }
            else
            {
                string toolName = manager.HasMopUnlocked ? "장비: T자 대걸레 (마우스 흔들어 쓱싹!)" : "장비: 물티슈 한 장 (마우스 열심히 흔들기!)";
                GUI.Label(new Rect(28, 128, 308, 22), toolName, _labelStyle);
            }

            // 우상단 직관적 4단계 의심 눈알 (복잡한 % 수식 완전 숨김!)
            GUI.Box(new Rect(sw - 296, 16, 280, 106), "", _boxStyle);
            int st = manager.SuspicionStage;
            string[] stageNames = { "🟢 평온 (안전)", "🟡 의아함 (살짝 찔림)", "🟠 의심! (조심하세요)", "🔴 들통 직전!! (위험)" };
            Texture2D[] stageTex = { _greenTex, _yellowTex, _orangeTex, _redTex };
            GUI.Label(new Rect(sw - 284, 22, 256, 22), $"집주인 눈치: {stageNames[st]}", _titleStyle);
            DrawBar(new Rect(sw - 284, 50, 254, 10), (st + 0.25f) / 3.25f, stageTex[st]);
            GUI.Label(new Rect(sw - 284, 68, 256, 20), $"의뢰: {manager.clientMemo}", _labelStyle);
            GUI.Label(new Rect(sw - 284, 94, 256, 18), "[R] 마인드맵 강화 · [F] 손전등 · [H] 조작법", _labelStyle);

            // 중앙 시각적 조준점 + 얇은 게이지 바만 (글자 설명 박스 없음!)
            if (manager.Phase != GamePhase.GachaStealPopup && controller != null)
            {
                Texture2D dot = controller.AimIsPile ? _orangeTex
                    : controller.AimIsSmallTrash ? _greenTex
                    : controller.AimIsStain ? _blueTex : _barBgTex;
                float ds = (controller.AimIsPile || controller.AimIsSmallTrash || controller.AimIsStain) ? 9f : 5f;
                GUI.DrawTexture(new Rect(sw / 2f - ds / 2f, sh / 2f - ds / 2f, ds, ds), dot);

                if (controller.AimProgress01 > 0.01f)
                {
                    DrawBar(new Rect(sw / 2f - 65, sh / 2f + 16, 130, 8),
                        controller.AimProgress01,
                        controller.AimIsStain ? _blueTex : controller.AimIsPile ? _orangeTex : _greenTex);
                }

                if (controller.NeedMouseShakeHint)
                {
                    _centerStyle.normal.textColor = new Color(0.65f, 0.92f, 1f);
                    GUI.Label(new Rect(sw / 2f - 130, sh / 2f + 28, 260, 22), "↔ 마우스를 좌우로 흔들어 문지르세요", _centerStyle);
                }
                else if (Cursor.lockState != CursorLockMode.Locked)
                {
                    GUI.Box(new Rect(sw / 2f - 105, sh / 2f + 24, 210, 30), "", _boxStyle);
                    _centerStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(sw / 2f - 105, sh / 2f + 28, 210, 22), "화면 클릭 → 조작 시작", _centerStyle);
                }

                if (controller.ShowHelp)
                {
                    GUI.Box(new Rect(16, sh - 118, 370, 102), "", _boxStyle);
                    GUI.Label(new Rect(28, sh - 112, 350, 20), "조작법 ([H] 닫기)", _titleStyle);
                    GUI.Label(new Rect(28, sh - 88, 350, 20), "• 큰 봉투: [좌클릭] 주먹으로 퍽!퍽! 터뜨리기 (보물 가챠!)", _labelStyle);
                    GUI.Label(new Rect(28, sh - 68, 350, 20), "• 작은 쓰레기: [좌클릭] 봉투에 담기 → [우클릭] 밖 트럭에 슛!", _labelStyle);
                    GUI.Label(new Rect(28, sh - 48, 350, 20), "• 2단계 바닥 얼룩: [좌클릭 꾹 + 마우스 흔들기] 문질러 닦기", _labelStyle);
                }
            }

            // 상단 토스트 알림
            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect tr = new Rect(sw / 2f - 290, 16, 580, 36);
                GUI.Box(tr, "", _boxStyle);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(tr, manager.ToastMessage, _centerStyle);
            }
        }

        /// <summary>
        /// 보물 등장 시 등급별 화려한 연출 + 3.5초 좌(😇 파랑 보관) vs 우(😈 황금/빨강 슈킹) 본능 선택!
        /// </summary>
        private void DrawGachaInstinctPopup()
        {
            var offer = manager.CurrentOffer;
            float sw = Screen.width, sh = Screen.height;

            // 등급별 화려한 백드롭
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 12f);
            Color prev = GUI.color;
            GUI.color = offer.rarity == TreasureRarity.Legendary
                ? new Color(1f, 0.80f, 0.15f, 0.22f * pulse)
                : offer.rarity == TreasureRarity.Rare
                    ? new Color(0.95f, 0.65f, 0.15f, 0.16f)
                    : new Color(0.05f, 0.06f, 0.08f, 0.60f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _whiteTex);
            GUI.color = prev;

            float w = 640, h = 340;
            float x = (sw - w) / 2f, y = (sh - h) / 2f;

            // 등급별 화려한 외곽 프레임
            Texture2D borderTex = offer.isDesignatedLostItem ? _redTex
                : offer.rarity == TreasureRarity.Legendary ? _goldTex
                : offer.rarity == TreasureRarity.Rare ? _orangeTex : _blueTex;
            GUI.DrawTexture(new Rect(x - 5, y - 5, w + 10, h + 10), borderTex);
            GUI.DrawTexture(new Rect(x, y, w, h), _panelTex);

            string rarityTag = offer.isDesignatedLostItem ? "🚨 의뢰인 지정 분실물 발견!!"
                : offer.rarity == TreasureRarity.Legendary ? "✨ ★ 전설 보물 발굴!! ★ ✨"
                : offer.rarity == TreasureRarity.Rare ? "✨ 희귀 보물 발견! ✨"
                : "보물 발견!";

            _bigTitleStyle.normal.textColor = offer.rarity == TreasureRarity.Legendary ? new Color(1f, 0.88f, 0.25f) : Color.white;
            GUI.Label(new Rect(x, y + 16, w, 32), rarityTag, _bigTitleStyle);
            _bigTitleStyle.normal.textColor = new Color(1f, 0.95f, 0.75f);
            GUI.Label(new Rect(x, y + 52, w, 34), $"{offer.itemName}", _bigTitleStyle);

            // 3.5초 본능 카운트다운 바
            float tRatio = Mathf.Clamp01(manager.OfferTimer / GamePrototypeManager.OfferDuration);
            DrawBar(new Rect(x + 60, y + 96, w - 120, 12), tRatio, tRatio > 0.35f ? _goldTex : _redTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + 110, w, 20), $"빠른 선택! {manager.OfferTimer:0.0}초", _centerStyle);

            // 왼쪽 (파란 후광 — 정직 보관 [Q]) vs 오른쪽 (붉은/황금 후광 — 몰래 슈킹 [E])
            float halfW = (w - 60) / 2f;
            float btnY = y + 144, btnH = 165;

            // LEFT: HONEST
            GUI.DrawTexture(new Rect(x + 20, btnY, halfW, btnH), _blueTex);
            GUI.DrawTexture(new Rect(x + 23, btnY + 3, halfW - 6, btnH - 6), _darkTex);
            int tip = offer.isDesignatedLostItem ? manager.lostItemTip : Mathf.Max(15, offer.value / 6);
            if (GUI.Button(new Rect(x + 26, btnY + 6, halfW - 12, btnH - 12),
                $"😇 선반 보관 [Q]\n\n안전 · 정직\n팁 +${tip}", _nodeBtnStyle))
            {
                manager.ResolveGachaChoice(false);
            }

            // RIGHT: STEAL
            float rx = x + 40 + halfW;
            GUI.DrawTexture(new Rect(rx, btnY, halfW, btnH), offer.isDesignatedLostItem ? _redTex : _goldTex);
            GUI.DrawTexture(new Rect(rx + 3, btnY + 3, halfW - 6, btnH - 6), _darkTex);
            string eyes = offer.isDesignatedLostItem ? "🚨 100% 발각!" : new string('👁', offer.riskIcons);
            string knockWarn = manager.IsKnockAlertActive ? "\n⚠ 문 밖 인기척!" : "";
            if (GUI.Button(new Rect(rx + 6, btnY + 6, halfW - 12, btnH - 12),
                $"😈 몰래 슈킹 [E]\n\n+${offer.value:N0}\n위험도 {eyes}{knockWarn}", _nodeBtnStyle))
            {
                manager.ResolveGachaChoice(true);
            }
        }

        /// <summary>
        /// 1턴 종료 후 열리는 인크리멘탈 마인드맵 스킬트리 (중앙 뿌리 → 4갈래 실제 연결선!)
        /// </summary>
        private void DrawMindMapOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _panelTex);

            float cx = sw / 2f, cy = sh / 2f - 10f;

            _bigTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, 16, sw, 32), $"트럭 정비소 · 마인드맵 스킬트리  |  보유 현금: ${manager.Money:N0}", _bigTitleStyle);
            _centerStyle.normal.textColor = new Color(0.80f, 0.86f, 0.92f);
            GUI.Label(new Rect(0, 48, sw, 22),
                $"현재 방 진행도 — 1단계 쓰레기 {manager.Phase1Percent:0}%  |  2단계 얼룩 {manager.Phase2Percent:0}% (재투입 시 그대로 유지)", _centerStyle);

            // 마인드맵 노드 좌표 (중앙에서 4갈래로 뻗음)
            Vector2 root = new Vector2(cx, cy);
            // 👊 좌상: 주먹·파괴 갈래
            Vector2 nFist = new Vector2(cx - 190, cy - 85);
            Vector2 nShock = new Vector2(cx - 375, cy - 150);
            Vector2 nBeacon = new Vector2(cx - 375, cy - 30);
            // 🛍️ 우상: 봉투·트럭 갈래
            Vector2 nBag = new Vector2(cx + 190, cy - 85);
            Vector2 nTruck = new Vector2(cx + 375, cy - 140);
            Vector2 nMagnet = new Vector2(cx + 375, cy - 25);
            // 💚 좌하: 체력·걸레 갈래
            Vector2 nStam = new Vector2(cx - 190, cy + 95);
            Vector2 nMop = new Vector2(cx - 375, cy + 75);
            Vector2 nScrub = new Vector2(cx - 375, cy + 185);
            // 😈 우하: 보물·슈킹 갈래
            Vector2 nLuck = new Vector2(cx + 190, cy + 95);
            Vector2 nPoker = new Vector2(cx + 375, cy + 75);
            Vector2 nMarket = new Vector2(cx + 375, cy + 185);

            // 연결선 먼저 그리기
            DrawLine(root, nFist, 3f); DrawLine(nFist, nShock, 2.5f); DrawLine(nFist, nBeacon, 2.5f);
            DrawLine(root, nBag, 3f); DrawLine(nBag, nTruck, 2.5f); DrawLine(nTruck, nMagnet, 2.5f);
            DrawLine(root, nStam, 3f); DrawLine(nStam, nMop, 2.5f); DrawLine(nMop, nScrub, 2.5f);
            DrawLine(root, nLuck, 3f); DrawLine(nLuck, nPoker, 2.5f); DrawLine(nLuck, nMarket, 2.5f);

            // 중앙 코어 노드
            Rect coreR = CenteredRect(root, 136, 62);
            GUI.DrawTexture(coreR, _goldTex);
            GUI.DrawTexture(new Rect(coreR.x + 3, coreR.y + 3, coreR.width - 6, coreR.height - 6), _darkTex);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(coreR, "특수청소 본부\n(4갈래 성장)", _centerStyle);

            // 12개 스킬 노드 버튼
            DrawSkillNode(nFist, "fist", $"👊 주먹 힘 Lv.{manager.LvFistPower}/5\n타격 데미지 UP", manager.CostFist, manager.LvFistPower >= 5, true, _orangeTex);
            DrawSkillNode(nShock, "shock", $"💥 충격파 Lv.{manager.LvShockwave}/3\n주변 봉투 동시 타격", manager.CostShockwave, manager.LvShockwave >= 3, manager.LvFistPower >= 1, _orangeTex);
            DrawSkillNode(nBeacon, "beacon", $"✨ 10% 빛기둥 탐지\n마지막 쓰레기 투시", manager.CostBeacon, manager.HasBeaconDetect, manager.LvFistPower >= 1, _orangeTex);

            DrawSkillNode(nBag, "bag", $"🛍️ 큰 봉투 Lv.{manager.LvBagSize}/4\n용량 {manager.HandBagCapacity}칸", manager.CostBagSize, manager.LvBagSize >= 4, true, _blueTex);
            DrawSkillNode(nTruck, "truck", $"🚚 트럭 확장 Lv.{manager.LvTruckSize}/3\n짐칸 확대+슛 보너스", manager.CostTruckSize, manager.LvTruckSize >= 3, manager.LvBagSize >= 1, _blueTex);
            DrawSkillNode(nMagnet, "magnet", $"🧲 트럭 자석 압축기\n근처 슛 자동 흡입!", manager.CostTruckMagnet, manager.HasTruckMagnet, manager.LvTruckSize >= 1, _blueTex);

            DrawSkillNode(nStam, "stamina", $"💚 체력 증진 Lv.{manager.LvStamina}/5\n최대 {manager.MaxStamina:0}", manager.CostStamina, manager.LvStamina >= 5, true, _greenTex);
            DrawSkillNode(nMop, "mop", $"🧹 대걸레 구매\n물티슈→2.5배속 걸레!", manager.CostMop, manager.HasMopUnlocked, manager.LvStamina >= 1, _greenTex);
            DrawSkillNode(nScrub, "scrub", $"🫧 세제 강화 Lv.{manager.LvScrubPower}/3\n얼룩 닦기 고속화", manager.CostScrub, manager.LvScrubPower >= 3, manager.HasMopUnlocked, _greenTex);

            DrawSkillNode(nLuck, "luck", $"🎁 보물 눈 Lv.{manager.LvTreasureLuck}/3\n가챠 확률·등급 UP", manager.CostLuck, manager.LvTreasureLuck >= 3, true, _goldTex);
            DrawSkillNode(nPoker, "poker", $"🎭 뻔뻔함 Lv.{manager.LvPokerFace}/4\n슈킹 의심도 완화", manager.CostPoker, manager.LvPokerFace >= 4, manager.LvTreasureLuck >= 1, _goldTex);
            DrawSkillNode(nMarket, "market", $"💰 암시장 Lv.{manager.LvBlackMarket}/3\n슈킹 수익 +25%/Lv", manager.CostMarket, manager.LvBlackMarket >= 3, manager.LvTreasureLuck >= 1, _goldTex);

            // 하단 재투입 버튼
            Rect goBtn = new Rect(cx - 210, sh - 74, 420, 52);
            GUI.DrawTexture(new Rect(goBtn.x - 3, goBtn.y - 3, goBtn.width + 6, goBtn.height + 6), _greenTex);
            if (GUI.Button(goBtn, $"▶ {manager.TurnCount + 1}턴 방으로 재투입 (체력 회복 · 청소 진행도 유지)", _nodeBtnStyle))
            {
                manager.ResumeNextTurnFromMindMap();
            }
        }

        private void DrawSkillNode(Vector2 center, string id, string text, int cost, bool isMax, bool unlocked, Texture2D accentTex)
        {
            Rect r = CenteredRect(center, 152, 74);
            bool canBuy = unlocked && !isMax && manager.Money >= cost;

            Texture2D frame = isMax ? _goldTex : canBuy ? accentTex : _barBgTex;
            GUI.DrawTexture(r, frame);
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), _darkTex);

            string footer = isMax ? "[MAX 완료]" : !unlocked ? "[선행 노드 필요]" : $"${cost:N0}";
            Color prev = GUI.contentColor;
            GUI.contentColor = isMax ? new Color(1f, 0.9f, 0.4f) : canBuy ? Color.white : new Color(0.55f, 0.58f, 0.62f);
            if (GUI.Button(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), $"{text}\n{footer}", _nodeBtnStyle))
            {
                if (canBuy) manager.BuyNode(id);
            }
            GUI.contentColor = prev;
        }

        private void DrawRoomCompletedOverlay()
        {
            float sw = Screen.width, sh = Screen.height;
            float w = 560, h = 340;
            Rect box = new Rect((sw - w) / 2f, (sh - h) / 2f, w, h);
            GUI.DrawTexture(new Rect(box.x - 4, box.y - 4, w + 8, h + 8), manager.LastWasCaught ? _redTex : _goldTex);
            GUI.DrawTexture(box, _panelTex);

            float x = box.x + 24, y = box.y + 22;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 48, 32), $"원룸 100% 완청 완료!   별점: {stars}", _bigTitleStyle);

            string verdict = manager.LastWasCaught
                ? $"[발각됨!] 욕심이 과했습니다 — 장물 몰수 + 청소비 50% 벌금!"
                : manager.StolenCount == 0
                    ? "[정직 완청!] 청소비 전액 + 별점 5점 달성!"
                    : $"[완전범죄 성공!] 장물 ${manager.StolenValue:N0} + 청소비 전액 획득!";

            GUI.Label(new Rect(x, y + 54, w - 48, 28), verdict, _centerStyle);
            GUI.Label(new Rect(x, y + 96, w - 48, 56), $"의뢰인 리뷰: {manager.LastReviewText}", _labelStyle);
            GUI.Label(new Rect(x, y + 168, w - 48, 26), $"총 소모 턴: {manager.TurnCount}턴   |   보유 현금: ${manager.Money:N0}", _centerStyle);

            if (GUI.Button(new Rect(x + 40, box.y + h - 78, w - 128, 52),
                "▶ 강화된 상태로 단골 원룸 재발 의뢰 다시 플레이!", _nodeBtnStyle))
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
