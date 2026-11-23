using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 맑은 고딕 OS 동적 폰트를 사용해 한글 깨짐(□□□) 없이
    /// HUD, 현장 퀵 강화 바(Z/X/C/V), 트럭 정비, 방 완청 별점 리뷰를 그립니다.
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private Font _krFont;
        private GUIStyle _boxStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _btnStyle;
        private Texture2D _darkTex;
        private Texture2D _barBgTex;
        private Texture2D _greenTex;
        private Texture2D _yellowTex;
        private Texture2D _redTex;
        private Texture2D _blueTex;
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

            _krFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "Arial" }, 15);
            _darkTex = MakeTex(new Color(0.08f, 0.09f, 0.11f, 0.84f));
            _barBgTex = MakeTex(new Color(0.18f, 0.20f, 0.24f, 0.95f));
            _greenTex = MakeTex(new Color(0.24f, 0.78f, 0.45f, 1f));
            _yellowTex = MakeTex(new Color(0.95f, 0.74f, 0.18f, 1f));
            _redTex = MakeTex(new Color(0.90f, 0.24f, 0.24f, 1f));
            _blueTex = MakeTex(new Color(0.22f, 0.60f, 0.95f, 1f));

            _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkTex }, padding = new RectOffset(12, 12, 10, 10) };
            _titleStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _labelStyle = new GUIStyle(GUI.skin.label) { font = _krFont, fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.92f, 0.93f, 0.95f) } };
            _centerStyle = new GUIStyle(_labelStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _btnStyle = new GUIStyle(GUI.skin.button) { font = _krFont, fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
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

            if (manager.Phase == GamePhase.PlayingInRoom)
            {
                DrawPlayingHud();
            }
            else if (manager.Phase == GamePhase.TruckShop)
            {
                DrawTruckShopOverlay();
            }
            else if (manager.Phase == GamePhase.RoomCompleted)
            {
                DrawRoomCompletedOverlay();
            }
        }

        private void DrawPlayingHud()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // 1. 좌상단 상태 패널 (일차 · 현금 · 청소율 · 악취 호흡 · 가방)
            GUI.Box(new Rect(16, 16, 350, 168), "", _boxStyle);
            GUI.Label(new Rect(28, 24, 330, 26), $"{manager.roomTitle} ({manager.TryCount}일차)", _titleStyle);
            GUI.Label(new Rect(28, 52, 330, 22), $"보유 현금: ${manager.Money:N0}   |   증강: {manager.ActiveAugmentName}", _labelStyle);

            // 청소율 바
            float cleanPct = manager.CleanPercent;
            GUI.Label(new Rect(28, 78, 330, 20), $"청소율: {cleanPct:0.0}% / {manager.TargetCompletePercent:0}% (자동 완청)", _labelStyle);
            DrawBar(new Rect(28, 100, 320, 12), cleanPct / manager.TargetCompletePercent, _blueTex);

            // 악취 호흡 게이지 바
            float breathRatio = Mathf.Clamp01(manager.Breath / manager.MaxBreath);
            Texture2D bTex = breathRatio > 0.45f ? _greenTex : breathRatio > 0.22f ? _yellowTex : _redTex;
            GUI.Label(new Rect(28, 118, 330, 20), $"악취 호흡: {manager.Breath:0.0}초 / {manager.MaxBreath:0}초", _labelStyle);
            DrawBar(new Rect(28, 140, 320, 12), breathRatio, bTex);

            GUI.Label(new Rect(28, 156, 330, 22), $"쓰레기 가방: {manager.BagUsed} / {manager.BagCapacity}   ([R] 트럭 복귀)", _labelStyle);

            // 2. 우상단 의뢰 메모 + 실시간 발각률 + 장비 슬롯
            GUI.Box(new Rect(sw - 346, 16, 330, 158), "", _boxStyle);
            GUI.Label(new Rect(sw - 334, 24, 308, 42), $"의뢰 메모: {manager.clientMemo}", _labelStyle);

            float det = manager.EffectiveDetectionRate;
            Texture2D dTex = det <= 10f ? _greenTex : det < 35f ? _yellowTex : _redTex;
            string detState = det <= 0f ? "정직 (0%)" : det <= 12f ? "안전권" : det < 35f ? "주의" : "위험!";
            GUI.Label(new Rect(sw - 334, 72, 308, 22), $"실시간 예상 발각률: {det:0.0}% ({detState}) | 슈킹 {manager.StolenCount}건", _labelStyle);
            DrawBar(new Rect(sw - 334, 96, 304, 12), det / 100f, dTex);

            string s1 = manager.SuctionTier == 1 ? "맨손" : manager.SuctionTier == 2 ? "집게" : "청소기";
            string s2 = manager.StainTier == 1 ? "걸레" : "세제+대걸레";
            string s3 = manager.MaskTier == 1 ? "노마스크" : manager.MaskTier == 2 ? "마스크" : "방독면";
            int act = controller != null ? controller.ActiveSlot : 1;
            int sug = controller != null ? controller.SuggestedSlot : 0;
            bool blink = Mathf.Sin(Time.time * 12f) > 0f;
            string mark1 = act == 1 ? "▶" : (sug == 1 && blink ? "★" : "");
            string mark2 = act == 2 ? "▶" : (sug == 2 && blink ? "★" : "");
            string mark3 = act == 3 ? "▶" : "";
            GUI.Label(new Rect(sw - 334, 118, 308, 22), $"{mark1}[1]{s1}  {mark2}[2]{s2}  {mark3}[3]{s3}", _labelStyle);
            GUI.Label(new Rect(sw - 334, 142, 308, 22), "[좌클릭] 청소/닦기 · [F] 손전등 · [H] 도움말", _labelStyle);

            // 3. 시각적 조준점 (글자 박스 최소화: 일반 쓰레기·얼룩은 게이지 바만, 보물만 최소 키 뱃지)
            Texture2D dotTex = controller != null && controller.AimIsTreasure ? _yellowTex
                : controller != null && controller.AimIsStain ? _blueTex
                : controller != null && controller.AimIsCleanable ? _greenTex : _barBgTex;
            float dotSize = (controller != null && (controller.AimIsCleanable || controller.AimIsTreasure)) ? 9f : 5f;
            GUI.DrawTexture(new Rect(sw / 2f - dotSize / 2f, sh / 2f - dotSize / 2f, dotSize, dotSize), dotTex);

            if (controller != null)
            {
                // 청소/닦기 진행 중일 때 얇은 바 1줄만 표시 (시야 방해 0%)
                if (controller.AimCleanProgress01 > 0.01f)
                {
                    DrawBar(new Rect(sw / 2f - 65, sh / 2f + 16, 130, 8),
                        controller.AimCleanProgress01,
                        controller.AimIsStain ? _blueTex : _greenTex);
                }

                // 보물·가방꽉참·마우스해제 등 필수 정보만 컴팩트 뱃지로 표시
                if (!string.IsNullOrEmpty(controller.AimTitle))
                {
                    bool hasHint = !string.IsNullOrEmpty(controller.AimHint);
                    float bw = hasHint ? 420 : 180;
                    float bh = hasHint ? 52 : 28;
                    Rect promptRect = new Rect(sw / 2f - bw / 2f, sh / 2f + 28, bw, bh);
                    GUI.Box(promptRect, "", _boxStyle);
                    _centerStyle.normal.textColor = controller.AimIsDanger ? new Color(1f, 0.45f, 0.40f)
                        : controller.AimIsTreasure ? new Color(1f, 0.88f, 0.30f) : Color.white;
                    GUI.Label(new Rect(promptRect.x, promptRect.y + 4, promptRect.width, 22), controller.AimTitle, _centerStyle);
                    if (hasHint)
                    {
                        _centerStyle.normal.textColor = new Color(0.95f, 0.90f, 0.65f);
                        GUI.Label(new Rect(promptRect.x, promptRect.y + 26, promptRect.width, 22), controller.AimHint, _centerStyle);
                    }
                }

                // 도움말은 [H] 키 눌렀을 때만 열림 (기본은 화면 깔끔!)
                if (controller.ShowHelpOverlay)
                {
                    GUI.Box(new Rect(16, sh - 122, 360, 106), "", _boxStyle);
                    GUI.Label(new Rect(28, sh - 114, 340, 22), "조작법 ([H]로 닫기)", _titleStyle);
                    GUI.Label(new Rect(28, sh - 88, 340, 20), "• 쓰레기/얼룩: 조준 후 [좌클릭 꾹] (자동 장비 전환 지원)", _labelStyle);
                    GUI.Label(new Rect(28, sh - 66, 340, 20), "• 보물: [E] 슈킹 / [Q] 선반 보관", _labelStyle);
                    GUI.Label(new Rect(28, sh - 44, 340, 20), "• [1/2/3] 장비 · [Z/X/C/V] 강화 · [R] 트럭 · [F] 손전등", _labelStyle);
                }
            }

            // 4. 토스트 알림 (상단 중앙)
            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect toastRect = new Rect(sw / 2f - 290, 18, 580, 38);
                GUI.Box(toastRect, "", _boxStyle);
                _centerStyle.normal.textColor = new Color(1f, 0.92f, 0.55f);
                GUI.Label(toastRect, manager.ToastMessage, _centerStyle);
            }

            // 5. 우하단 현장 즉시 강화 퀵 바 (Z / X / C / V)
            float qW = 368, qH = 74;
            float qX = sw - qW - 16, qY = sh - qH - 16;
            GUI.Box(new Rect(qX, qY, qW, qH), "", _boxStyle);
            DrawQuickSlot(new Rect(qX + 8, qY + 8, 84, 58), "Z 속도", manager.LvSpeed, manager.CostSpeed);
            DrawQuickSlot(new Rect(qX + 98, qY + 8, 84, 58), "X 범위", manager.LvRange, manager.CostRange);
            DrawQuickSlot(new Rect(qX + 188, qY + 8, 84, 58), "C 호흡", manager.LvBreath, manager.CostBreath);
            DrawQuickSlot(new Rect(qX + 278, qY + 8, 84, 58), "V 가방", manager.LvBag, manager.CostBag);
        }

        private void DrawQuickSlot(Rect r, string title, int lv, int cost)
        {
            bool canBuy = lv < 10 && manager.Money >= cost;
            GUI.DrawTexture(r, canBuy ? _greenTex : _barBgTex);
            Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            GUI.DrawTexture(inner, _darkTex);
            _centerStyle.normal.textColor = canBuy ? new Color(0.6f, 1f, 0.7f) : Color.gray;
            GUI.Label(new Rect(inner.x, inner.y + 4, inner.width, 18), title, _centerStyle);
            _centerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(inner.x, inner.y + 20, inner.width, 16), $"Lv.{lv}", _centerStyle);
            _centerStyle.normal.textColor = canBuy ? new Color(1f, 0.9f, 0.35f) : new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(new Rect(inner.x, inner.y + 36, inner.width, 16), lv >= 10 ? "MAX" : $"${cost}", _centerStyle);
        }

        private void DrawTruckShopOverlay()
        {
            float w = 620, h = 440;
            Rect box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(box, "", _boxStyle);

            float x = box.x + 22, y = box.y + 18;
            GUI.Label(new Rect(x, y, w - 44, 28), $"트럭 정비소 · 현재 청소율 {manager.CleanPercent:0.0}% 유지 중  |  현금 ${manager.Money:N0}", _titleStyle);
            GUI.Label(new Rect(x, y + 32, w - 44, 40), manager.RetreatReasonText, _labelStyle);

            float by = y + 82;
            string s1Text = manager.SuctionTier == 1
                ? "[1번 장비] 집게·봉투 구매 ($100) — 수거 속도 2배"
                : manager.SuctionTier == 2
                    ? "[1번 장비] 가정용 청소기 구매 ($320) — 좌클릭 홀드 연속·광역 흡입!"
                    : "[1번 장비] 가정용 청소기 보유 중 (프로토타입 최고 티어)";
            if (GUI.Button(new Rect(x, by, w - 44, 44), s1Text, _btnStyle)) manager.BuySuctionTier();

            string s2Text = manager.StainTier == 1
                ? "[2번 장비] 세제 스프레이+대걸레 ($220) — 얼룩 2배속 + CCTV 거품 가리기 해금!"
                : "[2번 장비] 세제 스프레이 보유 중 (CCTV 거품 가리기 가능)";
            if (GUI.Button(new Rect(x, by + 54, w - 44, 44), s2Text, _btnStyle)) manager.BuyStainTier();

            string s3Text = manager.MaskTier == 1
                ? "[3번 장비] 일회용 마스크 구매 ($80) — 악취 소모 -30%"
                : manager.MaskTier == 2
                    ? "[3번 장비] 방독면 구매 ($350) — 악취 소모 -65%"
                    : "[3번 장비] 방독면 보유 중 (악취 대폭 감소)";
            if (GUI.Button(new Rect(x, by + 108, w - 44, 44), s3Text, _btnStyle)) manager.BuyMaskTier();

            int stealthCost = 120 + manager.StealthTier * 140;
            string stText = manager.StealthTier < 5
                ? $"[은닉 업글 Lv.{manager.StealthTier}→{manager.StealthTier + 1}] 슈킹 발각률 -5% 영구 감면 (${stealthCost})"
                : "[은닉 업글 MAX] 슈킹 발각률 -25% 감면 완료";
            if (GUI.Button(new Rect(x, by + 162, w - 44, 44), stText, _btnStyle)) manager.BuyStealthTier();

            if (GUI.Button(new Rect(x, box.y + h - 72, w - 44, 52),
                $"▶ {manager.TryCount + 1}일차 방으로 재투입 (호흡·가방 충전 / 청소 진행도 그대로 유지)", _btnStyle))
            {
                manager.ReenterRoomFromTruck();
            }
        }

        private void DrawRoomCompletedOverlay()
        {
            float w = 640, h = 440;
            Rect box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(box, "", _boxStyle);

            float x = box.x + 24, y = box.y + 20;
            string stars = new string('★', manager.LastStarRating) + new string('☆', 5 - manager.LastStarRating);
            GUI.Label(new Rect(x, y, w - 48, 30), $"원룸 완청 완료! (90%+ 자동 정화)   별점: {stars}", _titleStyle);

            string verdict = manager.LastWasCaught
                ? $"[발각됨!] 예상 발각률 {manager.EffectiveDetectionRate:0.0}% — 장물 몰수 + 청소비 50% 벌금!"
                : manager.StolenCount == 0
                    ? "[정직 완청!] 청소비 전액 + 별점 5점 달성!"
                    : $"[완전범죄 성공!] 발각률 {manager.EffectiveDetectionRate:0.0}% 뚫고 장물 ${manager.StolenValue:N0} + 청소비 획득!";

            GUI.Label(new Rect(x, y + 40, w - 48, 28), verdict, _labelStyle);
            GUI.Label(new Rect(x, y + 72, w - 48, 48), $"의뢰인 리뷰: {manager.LastReviewText}", _labelStyle);
            GUI.Label(new Rect(x, y + 126, w - 48, 24), $"총 시도: {manager.TryCount}일차  |  현재 보유 현금: ${manager.Money:N0}", _labelStyle);

            GUI.Label(new Rect(x, y + 168, w - 48, 26), "완청 보상 — 증강 카드 3택 1 (장착 후 재발 의뢰 바로 테스트):", _titleStyle);

            if (GUI.Button(new Rect(x, y + 204, w - 48, 52), "1. [마무리 달인] 방 자동 완청 기준 90% → 85% 단축", _btnStyle))
                manager.SelectDemoAugmentAndReplay(1);
            if (GUI.Button(new Rect(x, y + 266, w - 48, 52), "2. [쾌적한 환기] 청소율 50% 이상일 때 악취 호흡 감소 50% 완화", _btnStyle))
                manager.SelectDemoAugmentAndReplay(2);
            if (GUI.Button(new Rect(x, y + 328, w - 48, 52), "3. [가짜 영수증] 슈킹 발각률 -8% 영구 감면 (싹쓸이 25% 하한 유지)", _btnStyle))
                manager.SelectDemoAugmentAndReplay(3);
        }

        private void DrawBar(Rect r, float fill01, Texture2D fillTex)
        {
            GUI.DrawTexture(r, _barBgTex);
            float w = Mathf.Clamp01(fill01) * r.width;
            if (w > 1f) GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), fillTex);
        }
    }
}
