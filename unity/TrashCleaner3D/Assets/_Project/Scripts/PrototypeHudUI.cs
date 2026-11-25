using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// TrashCleaner3D v0.8 UI (한국형 원룸 배달 영수증 + 노란 포스트잇 테마):
    /// - 서술형 문구 0% · 개조식 + 레이아웃/도장/게이지 직관 전달!
    /// - 3축 재화($ 현금 / ★ 신뢰도 토큰 / ♦ 암시장 포인트)
    /// - 상단 악취 + 의심 게이지(80~95% ADRENALINE 골드-레드 강조 구간!)
    /// - 하단 봉투 0~100% + 150% 과적 위험 바 + 동종 연속 압축 칩
    /// - 80% 조기 정산([K]) 도장 & 막판 10% ×4/×5 잭팟 도장
    /// - 지층 보물 포스트잇 결정창([Q] 전달 vs [E] 슈킹) & 24노드 이원화 스킬트리
    /// - Unity 6 안전 BMP 기호만 사용 (이모지 폰트 경고 0개)
    /// </summary>
    public class PrototypeHudUI : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public FirstPersonCleanerController controller;

        private bool _stylesReady;
        private bool _hintMinimized;
        private Camera _cam;

        private Texture2D _whiteTex;
        private Texture2D _dimTex;

        private GUIStyle _inkStyle;
        private GUIStyle _inkBold;
        private GUIStyle _inkCenter;
        private GUIStyle _inkSmall;
        private GUIStyle _lightStyle;
        private GUIStyle _lightBold;
        private GUIStyle _lightCenter;
        private GUIStyle _lightSmall;
        private GUIStyle _worldPopStyle;
        private GUIStyle _nodeIconStyle;
        private GUIStyle _nodeSubStyle;

        private struct SkillNodeDef
        {
            public string id, icon, title, shortStat, costTag, parentId;
            public Vector2 offset;
            public int level, maxLevel;
            public bool unlocked, affordable;
            public Color accent;
            public int currencyType; // 0:$, 1:★, 2:♦
        }

        private void Awake()
        {
            _whiteTex = MakeTex(Color.white);
            _dimTex = MakeTex(new Color(0.05f, 0.06f, 0.08f, 0.84f));
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            Color ink = new Color(0.12f, 0.13f, 0.15f);
            _inkStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _inkStyle.normal.textColor = ink;

            _inkBold = new GUIStyle(_inkStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            _inkCenter = new GUIStyle(_inkBold) { alignment = TextAnchor.MiddleCenter };
            _inkSmall = new GUIStyle(_inkStyle) { fontSize = 11 };

            _lightStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _lightStyle.normal.textColor = new Color(0.95f, 0.96f, 0.98f);

            _lightBold = new GUIStyle(_lightStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            _lightCenter = new GUIStyle(_lightBold) { alignment = TextAnchor.MiddleCenter };
            _lightSmall = new GUIStyle(_lightStyle) { fontSize = 11 };

            _worldPopStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _nodeIconStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _nodeSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            if (manager == null) return;
            EnsureStyles();
            if (_cam == null) _cam = Camera.main;

            float w = Screen.width;
            float h = Screen.height;

            DrawScreenOverlays(w, h);
            DrawFloatingWorldPopups(w, h);

            if (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping)
            {
                DrawHud(w, h);
                DrawCrosshairAndPrompt(w, h);
            }
            else if (manager.Phase == GamePhase.TreasureDecision) DrawTreasurePostItModal(w, h);
            else if (manager.Phase == GamePhase.MindMapUpgrade) DrawSkillTreeBoard(w, h);
            else if (manager.Phase == GamePhase.SettingsMenu) DrawSettingsMenu(w, h);
            else if (manager.Phase == GamePhase.RoomCompleted) DrawSettlementReceipt(w, h);
        }

        private void DrawScreenOverlays(float w, float h)
        {
            // 악취 테두리
            if (!manager.IsOutsideVentilating && manager.StenchPercent >= 25f)
            {
                float t = Mathf.Clamp01((manager.StenchPercent - 25f) / 75f);
                float alpha = Mathf.Lerp(0.06f, 0.30f, t);
                Color edge = manager.StenchStage >= 4 ? new Color(0.45f, 0.14f, 0.40f, alpha) : new Color(0.32f, 0.44f, 0.12f, alpha);
                DrawBorderGlow(w, h, edge, Mathf.Lerp(16f, 44f, t));
            }
            // 아드레날린 80~95% 스릴 골드-레드 펄스 테두리!
            if (manager.IsAdrenalineZone)
            {
                float pulse = 0.10f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 6.5f));
                DrawBorderGlow(w, h, new Color(1.0f, 0.36f, 0.14f, pulse), 22f);
            }
            if (manager.FreshAirFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(0.65f, 0.92f, 1f, manager.FreshAirFlash01 * 0.12f));
            if (manager.TreasureFlash01 > 0.01f) DrawRect(new Rect(0, 0, w, h), new Color(1f, 0.88f, 0.3f, manager.TreasureFlash01 * 0.16f));
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
                float sx = sp.x, sy = h - sp.y;
                float alpha = Mathf.Clamp01(1f - (ft.age / ft.duration) * (ft.age / ft.duration));
                Color c = ft.color; c.a = alpha;
                _worldPopStyle.normal.textColor = new Color(0, 0, 0, alpha * 0.8f);
                GUI.Label(new Rect(sx - 139, sy - 13, 280, 28), ft.text, _worldPopStyle);
                _worldPopStyle.normal.textColor = c;
                GUI.Label(new Rect(sx - 140, sy - 14, 280, 28), ft.text, _worldPopStyle);
            }
        }

        private void DrawHud(float w, float h)
        {
            float topY = Mathf.Max(38f, h * 0.062f);
            float botSafe = Mathf.Max(56f, h * 0.088f);
            float sideX = Mathf.Max(34f, w * 0.048f);

            // ===== 1. 좌상단: 배달 영수증 카드 ($ · ★ · ♦ · 진행률 · 80% 조기정산 / 막판 10% 도장) =====
            float cardW = 248f;
            bool showExtraRow = manager.ContinuousFever01 > 0.05f || manager.IsLast10Jackpot || manager.CanEarlyCheckout;
            float cardH = showExtraRow ? 92f : 66f;
            Rect tl = new Rect(sideX, topY, cardW, cardH);
            DrawReceiptBox(tl);

            // 1행: 3축 재화 + 청소 %
            GUI.color = new Color(0.06f, 0.46f, 0.20f);
            GUI.Label(new Rect(tl.x + 10, tl.y + 6, 86, 20), $"${manager.Money:N0}", _lightBold);
            GUI.color = new Color(0.10f, 0.38f, 0.78f);
            GUI.Label(new Rect(tl.x + 92, tl.y + 6, 48, 20), $"★{manager.TrustTokens}", _lightBold);
            GUI.color = new Color(0.76f, 0.12f, 0.32f);
            GUI.Label(new Rect(tl.x + 140, tl.y + 6, 48, 20), $"♦{manager.BlackMarketPts}", _lightBold);
            GUI.color = Color.white;
            GUI.Label(new Rect(tl.x + cardW - 56, tl.y + 6, 48, 20), $"{manager.OverallCleanPercent:0}%", _inkCenter);

            // 2행: 진행률 바 (80% 컷 + 90% 잭팟 구역 표시)
            Rect progBg = new Rect(tl.x + 10, tl.y + 30, cardW - 20, 10);
            DrawRect(progBg, new Color(0.80f, 0.77f, 0.70f));
            // 90~100% 골드 잭팟 구간 배경 표시
            DrawRect(new Rect(progBg.x + progBg.width * 0.90f, progBg.y, progBg.width * 0.10f, progBg.height), new Color(0.96f, 0.78f, 0.20f, 0.55f));
            Color progFill = manager.IsLast10Jackpot ? new Color(0.95f, 0.65f, 0.10f) : (manager.Phase == GamePhase.Phase2_FloorMopping ? new Color(0.22f, 0.62f, 0.90f) : new Color(0.18f, 0.68f, 0.34f));
            DrawRect(new Rect(progBg.x, progBg.y, progBg.width * (manager.OverallCleanPercent / 100f), progBg.height), progFill);
            // 80% 조기 정산 마커 선
            DrawRect(new Rect(progBg.x + progBg.width * 0.80f, progBg.y - 2, 2, progBg.height + 4), new Color(0.75f, 0.20f, 0.18f));

            // 3행: 현재 도구 + 배율 뱃지
            string toolTag = manager.Phase == GamePhase.Phase2_FloorMopping
                ? (manager.HasMopUnlocked ? "도구: T자 대걸레" : "도구: 물티슈")
                : $"도구: {manager.CurrentToolName}";
            GUI.Label(new Rect(tl.x + 10, tl.y + 44, cardW - 20, 18), toolTag, _inkSmall);

            // 4행 (동적): 피버 바 / 막판 10% 잭팟 도장 / 80% 조기 정산 도장
            if (showExtraRow)
            {
                float ry = tl.y + 66f;
                if (manager.CanEarlyCheckout)
                {
                    Rect stampR = new Rect(tl.x + 10, ry - 2, 114, 21);
                    DrawBorderedBox(stampR, new Color(0.96f, 0.90f, 0.88f), new Color(0.78f, 0.18f, 0.18f), 2);
                    GUI.color = new Color(0.78f, 0.14f, 0.14f);
                    GUI.Label(stampR, "[K] 조기 정산", _lightCenter);
                    GUI.color = Color.white;
                }
                else if (manager.ContinuousFever01 > 0.05f)
                {
                    Rect fBg = new Rect(tl.x + 10, ry + 3, 114, 12);
                    DrawRect(fBg, new Color(0.78f, 0.75f, 0.68f));
                    Color fCol = manager.IsFeverHot ? new Color(0.96f, 0.45f, 0.12f) : new Color(0.95f, 0.74f, 0.18f);
                    DrawRect(new Rect(fBg.x, fBg.y, fBg.width * manager.ContinuousFever01, fBg.height), fCol);
                    GUI.Label(new Rect(fBg.x + 4, fBg.y - 2, 108, 16), $"FEVER {manager.ContinuousFever01 * 100f:0}%", _inkSmall);
                }

                if (manager.IsLast10Jackpot)
                {
                    Rect jpR = new Rect(tl.x + 130, ry - 2, 108, 21);
                    DrawBorderedBox(jpR, new Color(1.0f, 0.92f, 0.56f), new Color(0.84f, 0.48f, 0.06f), 2);
                    GUI.color = new Color(0.62f, 0.28f, 0.02f);
                    GUI.Label(jpR, $"★ 막판 ×{manager.Last10JackpotMult:0}", _lightCenter);
                    GUI.color = Color.white;
                }
            }

            // ===== 2. 상단 중앙: 악취 4칸 바 + 의심 게이지 (80~95% ADRENALINE 구간 강조!) =====
            float barW = Mathf.Min(360f, w * 0.40f);
            float barX = (w - barW) * 0.5f;
            Rect tc = new Rect(barX, topY, barW, 64f);
            DrawBorderedBox(tc, new Color(0.12f, 0.13f, 0.16f, 0.90f), manager.IsAdrenalineZone ? new Color(1f, 0.55f, 0.15f) : new Color(0.32f, 0.35f, 0.42f), manager.IsAdrenalineZone ? 2 : 1);

            // 상단: 악취 4칸
            int st = manager.StenchStage;
            Color stCol = manager.IsOutsideVentilating ? new Color(0.38f, 0.84f, 1f) :
                st == 0 ? new Color(0.40f, 0.88f, 0.48f) :
                st == 1 ? new Color(0.95f, 0.85f, 0.25f) :
                st == 2 ? new Color(0.98f, 0.56f, 0.18f) :
                st == 3 ? new Color(0.95f, 0.24f, 0.22f) : new Color(0.82f, 0.28f, 0.96f);

            GUI.color = stCol;
            GUI.Label(new Rect(tc.x + 8, tc.y + 6, 48, 20), "☣ 악취", _lightSmall);
            GUI.color = Color.white;

            float cellStartX = tc.x + 58f;
            float trackW = barW - 142f;
            float cellW = (trackW - 9f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                Rect cell = new Rect(cellStartX + i * (cellW + 3f), tc.y + 9, cellW, 12);
                DrawRect(cell, new Color(0.22f, 0.24f, 0.28f));
                float seg = Mathf.Clamp01((manager.StenchPercent - i * 25f) / 25f);
                if (seg > 0.001f) DrawRect(new Rect(cell.x, cell.y, cell.width * seg, cell.height), stCol);
            }
            string stTag = manager.IsOutsideVentilating ? "환기 중" :
                st == 4 ? "작업불가!" : st == 3 ? "이속↓↓" : st == 2 ? "이속↓" : st == 1 ? "답답함" : "쾌적";
            GUI.color = stCol;
            GUI.Label(new Rect(cellStartX + trackW + 6, tc.y + 6, 74, 20), stTag, _lightSmall);
            GUI.color = Color.white;

            // 하단: 의심 게이지 (0~100% 확정 발각 + 80~95% ADRENALINE 골드-레드 강조!)
            float sy = tc.y + 34f;
            Color suspCol = manager.IsAdrenalineZone ? new Color(1.0f, 0.42f, 0.15f)
                          : manager.SuspicionPercent >= 50f ? new Color(0.98f, 0.72f, 0.22f)
                          : new Color(0.52f, 0.84f, 0.96f);

            GUI.color = suspCol;
            GUI.Label(new Rect(tc.x + 8, sy, 48, 20), "◎ 의심", _lightSmall);
            GUI.color = Color.white;

            Rect suspTrack = new Rect(cellStartX, sy + 3, trackW, 14);
            DrawRect(suspTrack, new Color(0.20f, 0.22f, 0.26f));

            // 80~95% ADRENALINE 강조 구역 배경
            Rect adrZone = new Rect(suspTrack.x + trackW * 0.80f, suspTrack.y - 1, trackW * 0.15f, suspTrack.height + 2);
            DrawRect(adrZone, new Color(1.0f, 0.45f, 0.12f, manager.IsAdrenalineZone ? 0.65f : 0.32f));
            // 100% 확정 퇴거 레드 끝선
            DrawRect(new Rect(suspTrack.x + trackW * 0.95f, suspTrack.y, trackW * 0.05f, suspTrack.height), new Color(0.92f, 0.15f, 0.15f, 0.55f));

            // 실제 의심 바 채움
            float suspFillW = trackW * (manager.SuspicionPercent / 100f);
            if (suspFillW > 0.5f) DrawRect(new Rect(suspTrack.x, suspTrack.y + 1, suspFillW, suspTrack.height - 2), suspCol);

            // 80% 경계 틱
            DrawRect(new Rect(suspTrack.x + trackW * 0.80f, suspTrack.y - 2, 2, suspTrack.height + 4), new Color(1f, 0.85f, 0.25f));

            string suspTag = manager.IsAdrenalineZone
                ? $"스릴 ×{manager.AdrenalineMult:0.#}"
                : $"{manager.SuspicionPercent:0}%";
            GUI.color = suspCol;
            GUI.Label(new Rect(cellStartX + trackW + 6, sy, 74, 20), suspTag, _lightSmall);
            GUI.color = Color.white;

            // ===== 3. 우상단: [Tab] 스킬트리 + 의뢰인 노란 포스트잇 힌트 =====
            int afford = manager.CountAffordableUpgrades();
            Rect trBtn = new Rect(w - sideX - 118, topY, 118, 30);
            DrawReceiptBox(trBtn);
            GUI.color = afford > 0 ? new Color(0.76f, 0.34f, 0.04f) : new Color(0.14f, 0.15f, 0.18f);
            GUI.Label(trBtn, afford > 0 ? $"[Tab] ★+{afford}" : "[Tab] 스킬트리", _lightCenter);
            GUI.color = Color.white;

            // 노란 포스트잇: 의뢰인 코믹 힌트 (간결한 메모 카드, [H]로 접기/펴기)
            _hintMinimized = controller != null && controller.ShowHelp;
            Rect postItRect = _hintMinimized
                ? new Rect(w - sideX - 118, topY + 36, 118, 24)
                : new Rect(w - sideX - 208, topY + 36, 208, 64);
            DrawPostItBox(postItRect);
            if (_hintMinimized)
            {
                GUI.Label(postItRect, "■ 의뢰 메모 [H]", _inkCenter);
            }
            else
            {
                GUI.Label(new Rect(postItRect.x + 8, postItRect.y + 6, postItRect.width - 16, 18), "■ 의뢰인 메모 [H]", _inkBold);
                GUI.Label(new Rect(postItRect.x + 8, postItRect.y + 24, postItRect.width - 16, 36), "매트리스 옆 비상금 · 찾으면 연락!", _inkSmall);
            }

            // ===== 4. 중앙 하단: 봉투(0~100% + 150% 과적) & 동종 연속 압축 칩 =====
            DrawBottomBagAndStreakBar(w, h, botSafe);

            // 짧은 토스트 (중앙 하단 위)
            if (!string.IsNullOrEmpty(manager.ToastMessage))
            {
                Rect toastR = new Rect(w * 0.5f - 175, h - botSafe - 102, 350, 26);
                DrawBorderedBox(toastR, new Color(0.10f, 0.11f, 0.14f, 0.90f), new Color(0.95f, 0.80f, 0.26f), 1);
                GUI.color = new Color(1f, 0.92f, 0.55f);
                GUI.Label(toastR, manager.ToastMessage, _lightCenter);
                GUI.color = Color.white;
            }
        }

        private void DrawBottomBagAndStreakBar(float w, float h, float botSafe)
        {
            float bw = 286f;
            float bx = (w - bw) * 0.5f;
            float by = h - botSafe - 36f;

            // 동종 연속 압축 칩 (2개 이상 쌓이면 바로 위에 표시!)
            if (manager.StreakCount >= 2 && manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                Rect sChip = new Rect(w * 0.5f - 110, by - 26, 220, 22);
                DrawBorderedBox(sChip, new Color(0.08f, 0.26f, 0.20f, 0.92f), new Color(0.32f, 0.96f, 0.65f), 1);
                GUI.color = new Color(0.45f, 1f, 0.72f);
                GUI.Label(sChip, $"◈ {manager.StreakTypeLabel} {manager.StreakCount}/{GamePrototypeManager.CompressionStreakTarget} → 압축(0칸·×2)", _lightCenter);
                GUI.color = Color.white;
            }

            if (manager.HasCarriedBag)
            {
                Rect card = new Rect(bx, by, bw, 36);
                DrawBorderedBox(card, new Color(0.14f, 0.13f, 0.08f, 0.94f), new Color(1f, 0.82f, 0.22f), 2);
                GUI.color = new Color(1f, 0.90f, 0.36f);
                GUI.Label(card, $"▣ 보유 봉지 (${manager.CarriedBagValue}) · [RMB] {manager.CurrentVehicleName}", _lightCenter);
                GUI.color = Color.white;
            }
            else if (manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                bool over = manager.IsBagOverfilled;
                bool ready = manager.IsBagFullReady;
                Color border = over ? new Color(1f, 0.32f, 0.22f) : ready ? new Color(0.36f, 0.94f, 0.52f) : new Color(0.34f, 0.37f, 0.42f);

                Rect card = new Rect(bx, by, bw, 38);
                DrawBorderedBox(card, new Color(0.11f, 0.12f, 0.15f, 0.92f), border, over ? 2 : 1);

                string leftTxt = over
                    ? $"▲ 과적 {manager.BagFillRatio * 100f:0}% ({manager.CollectingCount}/{manager.OverfillCapacity})"
                    : $"▣ {manager.CollectingCount}/{manager.HandBagCapacity}";
                string rightTxt = ready
                    ? $"${manager.CollectingPendingValue} · [RMB] 투하"
                    : $"${manager.CollectingPendingValue}";

                GUI.color = over ? new Color(1f, 0.48f, 0.36f) : ready ? new Color(0.46f, 0.96f, 0.58f) : Color.white;
                GUI.Label(new Rect(card.x + 10, card.y + 4, 155, 18), leftTxt, _lightSmall);
                GUI.Label(new Rect(card.x + bw - 135, card.y + 4, 125, 18), rightTxt, _lightSmall);
                GUI.color = Color.white;

                // 0% ~ 150% 스케일 바 (66.7% 지점이 100% 컷, 나머지가 100~150% 과적 구역!)
                Rect track = new Rect(card.x + 10, card.y + 24, bw - 20, 8);
                DrawRect(track, new Color(0.22f, 0.24f, 0.28f));
                float cutX = track.x + track.width * (2f / 3f);
                // 과적 구역 배경 (붉은 빗금 느낌)
                DrawRect(new Rect(cutX, track.y, track.width * (1f / 3f), track.height), new Color(0.55f, 0.16f, 0.14f, 0.45f));

                float fill01 = Mathf.Clamp01(manager.BagFillRatio / 1.5f);
                Color barCol = over ? new Color(1f, 0.30f, 0.20f) : ready ? new Color(0.36f, 0.92f, 0.52f) : new Color(0.42f, 0.80f, 0.98f);
                DrawRect(new Rect(track.x, track.y, track.width * fill01, track.height), barCol);
                // 100% 경계선
                DrawRect(new Rect(cutX, track.y - 2, 2, track.height + 4), Color.white);
            }
        }

        private void DrawCrosshairAndPrompt(float w, float h)
        {
            if (controller == null) return;
            float cx = w * 0.5f, cy = h * 0.5f;

            bool hasTarget = controller.AimDoor != null || controller.AimDroppedBag != null || controller.AimTreasure != null ||
                             controller.AimIsPile || controller.AimIsSwarmNear || controller.AimIsStain;
            Color dotCol = controller.AimTreasure != null ? new Color(1f, 0.86f, 0.22f)
                         : hasTarget ? new Color(0.42f, 0.98f, 0.58f)
                         : new Color(1f, 1f, 1f, 0.75f);
            float sz = hasTarget ? 6f : 4f;
            DrawRect(new Rect(cx - sz * 0.5f, cy - sz * 0.5f, sz, sz), dotCol);

            // 큰 더미/얼룩 진행 미니 바
            if (controller.AimProgress01 > 0.01f && controller.AimProgress01 < 0.99f)
            {
                Rect pb = new Rect(cx - 28, cy + 12, 56, 5);
                DrawRect(pb, new Color(0, 0, 0, 0.65f));
                DrawRect(new Rect(pb.x, pb.y, pb.width * controller.AimProgress01, pb.height), dotCol);
            }

            // 간결 개조식 조준 프롬프트 (서술형 0%!)
            string prompt = null;
            if (controller.AimTreasure != null)
            {
                string tag = controller.AimTreasure.isMentionedByClient ? "★ 의뢰 언급" : "★ 지층 보물";
                prompt = $"{tag} · {controller.AimTreasure.itemName} · [LMB/F] 발굴";
            }
            else if (controller.AimDoor != null)
            {
                prompt = controller.AimDoor.IsOpen ? "[F] 문 닫기" : "[F] 현관문 열기";
            }
            else if (controller.AimDroppedBag != null && !manager.HasCarriedBag)
            {
                prompt = $"[RMB] 봉투 회수 · ${controller.AimDroppedBag.bagPendingValue}";
            }
            else if (controller.NeedMouseShakeHint)
            {
                prompt = "←→ 마우스 흔들어 닦기";
            }
            else if (manager.IsBagOverfilled)
            {
                prompt = manager.HasDoubleBottomBag
                    ? "과적 중 (이중바닥 보호) · [RMB] 투하"
                    : "▲ 과적 위험 · 벽 충돌 시 폭발 · [RMB] 투하";
            }

            if (!string.IsNullOrEmpty(prompt))
            {
                Rect pr = new Rect(cx - 160, cy + 26, 320, 24);
                DrawBorderedBox(pr, new Color(0.08f, 0.09f, 0.12f, 0.86f), dotCol, 1);
                GUI.Label(pr, prompt, _lightCenter);
            }
        }

        // ===== 노란 포스트잇 지층 보물 결정창 ([Q] 전달 vs [E] 슈킹) =====
        private void DrawTreasurePostItModal(float w, float h)
        {
            var item = manager.ActiveTreasure;
            if (item == null) return;

            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float cw = 430f, ch = 264f;
            Rect card = new Rect((w - cw) * 0.5f, (h - ch) * 0.5f, cw, ch);
            DrawPostItBox(card);

            string badge = item.isMentionedByClient ? "★ 의뢰인 언급 물건 (단가 ×2 · 의심 ×2)" : "★ 지층 발굴 보물";
            GUI.color = item.isMentionedByClient ? new Color(0.76f, 0.12f, 0.10f) : new Color(0.46f, 0.30f, 0.04f);
            GUI.Label(new Rect(card.x + 18, card.y + 16, cw - 36, 22), badge, _lightCenter);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + 18, card.y + 42, cw - 36, 26), item.itemName, new GUIStyle(_inkCenter) { fontSize = 18 });

            // 구분 점선
            GUI.Label(new Rect(card.x + 18, card.y + 70, cw - 36, 18), "--------------------------------------------------", _inkCenter);

            int honestTokens = item.isMentionedByClient ? 2 : 1;
            int honestTip = item.isMentionedByClient ? 65 : 30;
            float mentionMult = item.isMentionedByClient ? 2f : 1f;
            int stealEst = Mathf.RoundToInt(item.value * mentionMult * manager.AdrenalineMult);
            int bmPts = item.isMentionedByClient ? 2 : 1;
            float suspAdd = item.baseSuspicionGain * mentionMult;

            // 좌측: [Q] 전달 (성실 루트)
            Rect qBox = new Rect(card.x + 18, card.y + 96, 190, 146);
            DrawBorderedBox(qBox, new Color(0.92f, 0.96f, 0.92f), new Color(0.16f, 0.58f, 0.30f), 2);
            GUI.color = new Color(0.08f, 0.44f, 0.20f);
            GUI.Label(new Rect(qBox.x + 8, qBox.y + 10, qBox.width - 16, 22), "[Q] 의뢰인 전달", _lightCenter);
            GUI.color = Color.white;
            GUI.Label(new Rect(qBox.x + 12, qBox.y + 40, qBox.width - 24, 62),
                $"• ★ 신뢰도 +{honestTokens}\n• 의심 -25% 세탁\n• 사례금 +${honestTip}", _inkStyle);

            // 우측: [E] 슈킹 (도둑질 루트)
            Rect eBox = new Rect(card.x + cw - 208, card.y + 96, 190, 146);
            DrawBorderedBox(eBox, new Color(0.98f, 0.91f, 0.90f), new Color(0.80f, 0.18f, 0.18f), 2);
            GUI.color = new Color(0.76f, 0.12f, 0.14f);
            GUI.Label(new Rect(eBox.x + 8, eBox.y + 10, eBox.width - 16, 22), "[E] 즉시 슈킹", _lightCenter);
            GUI.color = Color.white;
            string evTag = manager.EvasionChance > 0f ? $" (회피 {manager.EvasionChance * 100f:0}%)" : "";
            GUI.Label(new Rect(eBox.x + 12, eBox.y + 40, eBox.width - 24, 66),
                $"• 현금 ~${stealEst} (감정)\n• ♦ 암시장 +{bmPts}\n• 의심 +{suspAdd:0}%{evTag}", _inkStyle);

            if (manager.TreasureDecisionLockTimer <= 0f)
            {
                if (GUI.Button(new Rect(qBox.x + 16, qBox.y + 108, qBox.width - 32, 28), "전달 확정 [Q]"))
                    manager.ResolveActiveTreasure(false);
                if (GUI.Button(new Rect(eBox.x + 16, eBox.y + 108, eBox.width - 32, 28), "슈킹 확정 [E]"))
                    manager.ResolveActiveTreasure(true);
            }
        }

        // ===== 24노드 이원화 스킬트리 ($ / ★신뢰도 / ♦암시장) =====
        private void DrawSkillTreeBoard(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);

            float topY = Mathf.Max(34f, h * 0.055f);
            float botSafe = Mathf.Max(52f, h * 0.082f);

            // 상단 영수증 헤더
            Rect hdr = new Rect(w * 0.5f - 240, topY, 480, 34);
            DrawReceiptBox(hdr);
            GUI.color = new Color(0.08f, 0.48f, 0.20f);
            GUI.Label(new Rect(hdr.x + 14, hdr.y + 6, 110, 22), $"${manager.Money:N0}", _lightBold);
            GUI.color = new Color(0.10f, 0.38f, 0.78f);
            GUI.Label(new Rect(hdr.x + 126, hdr.y + 6, 95, 22), $"★ 신뢰도 {manager.TrustTokens}", _lightBold);
            GUI.color = new Color(0.76f, 0.12f, 0.32f);
            GUI.Label(new Rect(hdr.x + 226, hdr.y + 6, 115, 22), $"♦ 암시장 {manager.BlackMarketPts}", _lightBold);
            GUI.color = Color.white;
            GUI.Label(new Rect(hdr.x + 350, hdr.y + 6, 120, 22), "[Tab] 닫기", _inkCenter);

            float scale = Mathf.Clamp(Mathf.Min(w / 1120f, (h - topY - botSafe - 70f) / 600f), 0.68f, 1.0f);
            Vector2 center = new Vector2(w * 0.5f, (topY + 42f + h - botSafe - 54f) * 0.5f);

            Color cTool = new Color(0.95f, 0.76f, 0.22f);
            Color cBag = new Color(0.32f, 0.76f, 0.98f);
            Color cSurv = new Color(0.36f, 0.88f, 0.52f);
            Color cTrust = new Color(0.32f, 0.64f, 1.0f);
            Color cFist = new Color(0.96f, 0.56f, 0.24f);
            Color cBlack = new Color(0.94f, 0.24f, 0.46f);

            var nodes = new[]
            {
                // 좌상: 도구 진화 & 흡입 ($)
                Node("tool", "▲", "도구 진화", manager.ToolTier == 0 ? "→ 집게/빗자루 (범위↑·3개)" : manager.ToolTier == 1 ? "→ 진공청소기 (광역 회오리)" : "진공청소기 MAX", $"${manager.CostTool}", "", new Vector2(-115, -75), manager.ToolTier, 2, cTool, 0),
                Node("sweep", "◎", "커서 범위", $"반경 {manager.SweepRadius:0.00}m", $"${manager.CostSweep}", "tool", new Vector2(-215, -75), manager.LvSweepRadius, 5, cTool, 0),
                Node("fever", "✹", "피버 엔진", "연속 흡입 → 범위+35%·속도+65%·×1.45", $"${manager.CostFever}", "tool", new Vector2(-115, -165), manager.HasFeverEngine ? 1 : 0, 1, cTool, 0),
                Node("crit", "✦", "크리티컬", $"확률 {manager.CritChance * 100f:0}% · 수익 ×2", $"${manager.CostCrit}", "sweep", new Vector2(-315, -75), manager.LvCrit, 4, cTool, 0),
                Node("chain", "◈", "연쇄 도미노", $"확률 {manager.ChainChance * 100f:0}% · 주변 +{manager.ChainExtraCount}개", $"${manager.CostChain}", "crit", new Vector2(-415, -75), manager.LvChainPop, 3, cTool, 0),
                Node("roomba", "◉", "로봇청소기", $"자동 수거 {manager.LvRoomba}대", $"${manager.CostRoomba}", "chain", new Vector2(-415, -165), manager.LvRoomba, 3, cTool, 0),

                // 우상: 운반 & 경제 ($)
                Node("bag", "▣", "봉투 용량", $"기본 {manager.HandBagCapacity} / 과적 {manager.OverfillCapacity}", $"${manager.CostBag}", "", new Vector2(115, -75), manager.LvBagSize, 4, cBag, 0),
                Node("throw", "↗", "던지는 힘", $"투척력 Lv.{manager.LvThrowPower}", $"${manager.CostThrowPower}", "bag", new Vector2(215, -75), manager.LvThrowPower, 4, cBag, 0),
                Node("truck", "▶", "수거함 진화", manager.CurrentVehicleName, $"${manager.CostTruck}", "throw", new Vector2(315, -75), manager.LvTruckSize, 3, cBag, 0),
                Node("magnet", "Ω", "자석 압축기", "근처 봉투 자동 흡입", $"${manager.CostMagnet}", "truck", new Vector2(415, -75), manager.HasTruckMagnet ? 1 : 0, 1, cBag, 0),
                Node("price", "◆", "분리수거 단가", $"단가 +{manager.LvTrashPrice * 25}%", $"${manager.CostTrashPrice}", "bag", new Vector2(115, -165), manager.LvTrashPrice, 5, cBag, 0),
                Node("combo", "»", "투하 콤보", $"콤보 유지 {manager.MaxComboWindow:0.0}초", $"${manager.CostComboTime}", "price", new Vector2(215, -165), manager.LvComboTime, 3, cBag, 0),

                // 좌하: 생존/걸레 ($) + 성실 루트 특전 (★)
                Node("mask", "☣", "방독면", $"악취 -{manager.LvMask * 16}%", $"${manager.CostMask}", "", new Vector2(-115, 75), manager.LvMask, 5, cSurv, 0),
                Node("speed", "»", "운동화", $"이동속도 +{manager.LvMoveSpeed * 15}%", $"${manager.CostMoveSpeed}", "mask", new Vector2(-215, 75), manager.LvMoveSpeed, 4, cSurv, 0),
                Node("mop", "┴", "T자 대걸레", "2단계 바닥 닦기 ×2.6", $"${manager.CostMop}", "mask", new Vector2(-115, 165), manager.HasMopUnlocked ? 1 : 0, 1, cSurv, 0),
                Node("scrub", "✧", "강력 세제", $"세정력 +{manager.LvScrubPower * 45}%", $"${manager.CostScrub}", "mop", new Vector2(-215, 165), manager.LvScrubPower, 3, cSurv, 0),

                Node("vip", "★", "VIP 고액 의뢰", "전체 단가·보너스 ×1.5 · 막판 ×5", $"★{manager.CostVipTokens}", "", new Vector2(-320, 75), manager.HasVipContract ? 1 : 0, 1, cTrust, 1),
                Node("detector", "◎", "보물 투시 탐지", "쓰레기 아래 파묻힌 보물 X-Ray", $"★{manager.CostDetectorTokens}", "", new Vector2(-320, 165), manager.HasTreasureDetector ? 1 : 0, 1, cTrust, 1),
                Node("doublebag", "▣", "이중 바닥 가방", "150% 과적 벽 충돌 폭발 면역!", $"★{manager.CostDoubleBagTokens}", "vip", new Vector2(-425, 120), manager.HasDoubleBottomBag ? 1 : 0, 1, cTrust, 1),

                // 우하: 파괴/탐지 ($) + 암시장 특전 (♦)
                Node("fist", "❖", "주먹 힘", $"큰 봉투 타격 {manager.FistDamage:0.0}", $"${manager.CostFist}", "", new Vector2(115, 75), manager.LvFistPower, 4, cFist, 0),
                Node("shock", "✹", "충격파 펀치", $"주변 큰 봉투 동시 타격 Lv.{manager.LvShockwave}", $"${manager.CostShockwave}", "fist", new Vector2(215, 75), manager.LvShockwave, 3, cFist, 0),
                Node("beacon", "◈", "10% 투시", "잔여 10% 쓰레기 황금 테두리", $"${manager.CostBeacon}", "fist", new Vector2(115, 165), manager.HasBeaconDetect ? 1 : 0, 1, cFist, 0),

                Node("appraisal", "♦", "감정가 조작", $"진품(×1.5) 확률 {25 + manager.LvAppraisal * 20}%", $"♦{manager.CostAppraisalPts}", "", new Vector2(320, 75), manager.LvAppraisal, 2, cBlack, 2),
                Node("evasion", "✧", "의심도 회피", $"슈킹 시 의심 무효 {manager.EvasionChance * 100f:0}%", $"♦{manager.CostEvasionPts}", "", new Vector2(320, 165), manager.LvEvasion, 3, cBlack, 2),
                Node("adrenaline", "▲", "아드레날린+", "의심 80~95% 배율 ×2 → ×2.5", $"♦{manager.CostAdrenalinePts}", "appraisal", new Vector2(425, 120), manager.HasAdrenalineAmp ? 1 : 0, 1, cBlack, 2),
            };

            // 연결선 그리기
            foreach (var n in nodes)
            {
                Vector2 pPos = center;
                if (!string.IsNullOrEmpty(n.parentId))
                {
                    foreach (var p in nodes) if (p.id == n.parentId) { pPos = center + p.offset * scale; break; }
                }
                else if (n.currencyType != 0)
                {
                    continue; // ★ / ♦ 독립 특전 브랜치는 깔끔하게 구획 박스로 분리!
                }
                Vector2 nPos = center + n.offset * scale;
                DrawLine(pPos, nPos, n.unlocked ? new Color(n.accent.r, n.accent.g, n.accent.b, 0.55f) : new Color(0.28f, 0.30f, 0.34f, 0.35f), 2f);
            }

            // 성실(★) / 암시장(♦) 구획 프레임
            Rect trustZone = new Rect(center.x - 472 * scale, center.y + 32 * scale, 200 * scale, 178 * scale);
            DrawBorderedBox(trustZone, new Color(0.08f, 0.14f, 0.24f, 0.55f), cTrust, 1);
            GUI.color = cTrust;
            GUI.Label(new Rect(trustZone.x + 6, trustZone.y + 2, trustZone.width - 12, 18), "★ 성실 루트 특전", _lightSmall);

            Rect blackZone = new Rect(center.x + 272 * scale, center.y + 32 * scale, 200 * scale, 178 * scale);
            DrawBorderedBox(blackZone, new Color(0.24f, 0.08f, 0.14f, 0.55f), cBlack, 1);
            GUI.color = cBlack;
            GUI.Label(new Rect(blackZone.x + 6, blackZone.y + 2, blackZone.width - 12, 18), "♦ 암시장 특전", _lightSmall);
            GUI.color = Color.white;

            // 중앙 코어 뱃지
            float coreSz = 48f * scale;
            DrawBorderedBox(new Rect(center.x - coreSz * 0.5f, center.y - coreSz * 0.5f, coreSz, coreSz), new Color(0.20f, 0.18f, 0.12f), cTool, 2);
            GUI.color = cTool;
            GUI.Label(new Rect(center.x - coreSz * 0.5f, center.y - coreSz * 0.5f, coreSz, coreSz), "v0.8", _lightCenter);
            GUI.color = Color.white;

            // 노드 버튼 렌더 + 호버 카드
            SkillNodeDef? hovered = null;
            float nW = 72f * scale, nH = 54f * scale;
            Vector2 mPos = Event.current.mousePosition;

            foreach (var n in nodes)
            {
                Vector2 p = center + n.offset * scale;
                Rect r = new Rect(p.x - nW * 0.5f, p.y - nH * 0.5f, nW, nH);
                bool isMax = n.level >= n.maxLevel;
                Color bg = !n.unlocked ? new Color(0.10f, 0.11f, 0.13f, 0.90f)
                         : isMax ? new Color(0.16f, 0.26f, 0.18f, 0.96f)
                         : n.affordable ? new Color(0.22f, 0.20f, 0.14f, 0.96f)
                         : new Color(0.14f, 0.15f, 0.19f, 0.94f);
                Color bd = !n.unlocked ? new Color(0.25f, 0.27f, 0.30f)
                         : isMax ? new Color(0.38f, 0.92f, 0.52f)
                         : n.affordable ? n.accent
                         : new Color(n.accent.r * 0.55f, n.accent.g * 0.55f, n.accent.b * 0.55f);

                DrawBorderedBox(r, bg, bd, n.affordable ? 2 : 1);

                _nodeIconStyle.normal.textColor = n.unlocked ? (isMax ? new Color(0.45f, 1f, 0.58f) : n.accent) : new Color(0.42f, 0.44f, 0.48f);
                GUI.Label(new Rect(r.x, r.y + 2, r.width, r.height * 0.52f), n.unlocked ? n.icon : "×", _nodeIconStyle);

                _nodeSubStyle.normal.textColor = n.unlocked ? Color.white : new Color(0.50f, 0.52f, 0.56f);
                string sub = !n.unlocked ? "잠김" : isMax ? "MAX" : $"{n.level}/{n.maxLevel} · {n.costTag}";
                GUI.Label(new Rect(r.x, r.y + r.height * 0.52f, r.width, r.height * 0.44f), sub, _nodeSubStyle);

                if (r.Contains(mPos)) hovered = n;
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && n.unlocked && !isMax)
                {
                    manager.BuyNode(n.id);
                }
            }

            // 하단 고정 1줄 개조식 상세 영수증 바
            Rect infoR = new Rect(w * 0.5f - 280, h - botSafe - 44, 560, 40);
            DrawReceiptBox(infoR);
            if (hovered.HasValue)
            {
                var hv = hovered.Value;
                bool isMax = hv.level >= hv.maxLevel;
                string state = !hv.unlocked ? "[잠김 · 선행 필요]" : isMax ? "[MAX]" : $"[클릭 구매 · {hv.costTag}]";
                GUI.Label(new Rect(infoR.x + 12, infoR.y + 4, infoR.width - 24, 18), $"{hv.icon} {hv.title} ({hv.level}/{hv.maxLevel})  {state}", _inkBold);
                GUI.Label(new Rect(infoR.x + 12, infoR.y + 21, infoR.width - 24, 18), $"• {hv.shortStat}", _inkSmall);
            }
            else
            {
                GUI.Label(infoR, "노드에 마우스 올리기 · 좌클릭 즉시 구매 · [Tab] 닫기", _inkCenter);
            }
        }

        private SkillNodeDef Node(string id, string icon, string title, string shortStat, string costTag, string parentId, Vector2 offset, int lv, int maxLv, Color accent, int curType)
        {
            bool unlocked = manager.IsNodeUnlocked(id);
            bool afford = false;
            if (unlocked && lv < maxLv)
            {
                if (curType == 0)
                {
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
                    afford = manager.Money >= c;
                }
                else if (curType == 1)
                {
                    int tc = id == "doublebag" ? manager.CostDoubleBagTokens : 1;
                    afford = manager.TrustTokens >= tc;
                }
                else
                {
                    int bc = id == "appraisal" ? manager.CostAppraisalPts : id == "adrenaline" ? manager.CostAdrenalinePts : manager.CostEvasionPts;
                    afford = manager.BlackMarketPts >= bc;
                }
            }
            return new SkillNodeDef
            {
                id = id, icon = icon, title = title, shortStat = shortStat, costTag = costTag,
                parentId = parentId, offset = offset, level = lv, maxLevel = maxLv,
                unlocked = unlocked, affordable = afford, accent = accent, currencyType = curType
            };
        }

        private void DrawSettingsMenu(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);
            Rect r = new Rect((w - 340) * 0.5f, (h - 230) * 0.5f, 340, 230);
            DrawReceiptBox(r);

            GUI.Label(new Rect(r.x + 16, r.y + 14, 308, 24), "환경 설정 [ESC]", _inkCenter);

            GUI.Label(new Rect(r.x + 20, r.y + 54, 150, 20), $"마우스 감도  {manager.MouseSensitivity:0.00}", _inkBold);
            float ns = GUI.HorizontalSlider(new Rect(r.x + 20, r.y + 78, 300, 18), manager.MouseSensitivity, 0.03f, 0.45f);
            if (!Mathf.Approximately(ns, manager.MouseSensitivity)) manager.SetMouseSensitivity(ns);

            GUI.Label(new Rect(r.x + 20, r.y + 112, 150, 20), $"전체 볼륨  {manager.MasterVolume * 100f:0}%", _inkBold);
            float nv = GUI.HorizontalSlider(new Rect(r.x + 20, r.y + 136, 300, 18), manager.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(nv, manager.MasterVolume)) manager.SetMasterVolume(nv);

            if (GUI.Button(new Rect(r.x + 90, r.y + 178, 160, 34), "계속하기 [ESC]"))
                manager.ToggleSettingsMenu();
        }

        // ===== 열전사 정산 영수증 (완료 / 조기 정산 / 100% 강제 퇴거) =====
        private void DrawSettlementReceipt(float w, float h)
        {
            GUI.DrawTexture(new Rect(0, 0, w, h), _dimTex);
            float rw = 380f, rh = 365f;
            Rect r = new Rect((w - rw) * 0.5f, (h - rh) * 0.5f, rw, rh);
            DrawReceiptBox(r);

            string title = manager.WasEvicted ? "[강제 퇴거 정산 영수증]"
                         : manager.WasEarlyCheckout ? "[80% 조기 정산 영수증]"
                         : "[원룸 청소 완료 영수증]";
            Color titleCol = manager.WasEvicted ? new Color(0.78f, 0.12f, 0.12f) : new Color(0.08f, 0.46f, 0.20f);
            GUI.color = titleCol;
            GUI.Label(new Rect(r.x + 16, r.y + 16, rw - 32, 26), title, new GUIStyle(_lightCenter) { fontSize = 17 });
            GUI.color = Color.white;

            GUI.Label(new Rect(r.x + 16, r.y + 44, rw - 32, 18), "--------------------------------------------", _inkCenter);

            float y = r.y + 68f;
            DrawReceiptRow(r, y, "청소 진행률", $"{manager.OverallCleanPercent:0.0}%"); y += 24;
            DrawReceiptRow(r, y, "동종 연속 압축", $"{manager.TotalCompressionsThisRoom}회"); y += 24;
            DrawReceiptRow(r, y, "보물 전달 / 슈킹", $"{manager.ReturnedCount}건 / {manager.StolenCount}건"); y += 24;
            DrawReceiptRow(r, y, "방 클리어 보너스", manager.WasEvicted ? "$0 (몰수)" : $"+${manager.LastClearBonus:N0}"); y += 24;
            if (manager.WasEvicted)
            {
                GUI.color = new Color(0.78f, 0.14f, 0.14f);
                DrawReceiptRow(r, y, $"압수 ({manager.LastStolenItemName})", $"-${manager.LastConfiscatedValue:N0}");
                GUI.color = Color.white;
                y += 24;
            }

            GUI.Label(new Rect(r.x + 16, y, rw - 32, 18), "--------------------------------------------", _inkCenter);
            y += 22;

            DrawReceiptRow(r, y, "보유 현금 ($)", $"${manager.Money:N0}"); y += 22;
            DrawReceiptRow(r, y, "★ 신뢰도 / ♦ 암시장", $"★{manager.TrustTokens}  ·  ♦{manager.BlackMarketPts}"); y += 28;

            // 안심 도장 (강제 퇴거여도 게임오버 없음!)
            Rect stamp = new Rect(r.x + 28, y, rw - 56, 26);
            DrawBorderedBox(stamp, new Color(0.92f, 0.96f, 0.92f), new Color(0.18f, 0.58f, 0.30f), 1);
            GUI.color = new Color(0.10f, 0.46f, 0.20f);
            GUI.Label(stamp, "기본 수익 · 토큰 · 스킬트리 100% 유지", _lightCenter);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(r.x + 70, r.y + rh - 52, rw - 140, 36), "다음 의뢰 시작 (강화 유지)"))
            {
                manager.ReplayRoomKeepUpgrades();
            }
        }

        private void DrawReceiptRow(Rect r, float y, string left, string right)
        {
            GUI.Label(new Rect(r.x + 22, y, r.width * 0.58f, 20), left, _inkStyle);
            var rightAlign = new GUIStyle(_inkBold) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(r.x + r.width * 0.45f, y, r.width * 0.55f - 22f, 20), right, rightAlign);
        }

        private void DrawReceiptBox(Rect r)
        {
            DrawBorderedBox(r, new Color(0.96f, 0.93f, 0.86f, 0.96f), new Color(0.22f, 0.22f, 0.24f), 2);
            // 상단 톱니 느낌 얇은 장식선
            DrawRect(new Rect(r.x + 2, r.y + 2, r.width - 4, 2), new Color(0.84f, 0.80f, 0.72f));
        }

        private void DrawPostItBox(Rect r)
        {
            DrawRect(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.26f));
            DrawBorderedBox(r, new Color(0.99f, 0.90f, 0.50f, 0.97f), new Color(0.76f, 0.64f, 0.20f), 1);
            // 상단 반투명 테이프 띠
            DrawRect(new Rect(r.x + r.width * 0.36f, r.y - 3, r.width * 0.28f, 7), new Color(0.92f, 0.86f, 0.62f, 0.85f));
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
