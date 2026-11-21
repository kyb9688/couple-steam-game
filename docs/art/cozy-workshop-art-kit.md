# 여자친구용 '코지 공방' AI 아트 & 브랜딩 키트 (v1.0)

- **대상 게임:** 뜯고 녹여라! (가제: Scrap to Gold) — [scrap-to-gold-spec.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/spec/scrap-to-gold-spec.md)
- **목표:** 그림(드로잉) 실력 없이도 **AI 이미지 생성 + 디자인/브랜딩 감각(색감·배치·타이포·통일감)**만으로 스팀에서 눈에 띄는 **'밝고 아기자기한 코지 공방'** 비주얼을 뽑아내는 실전 매뉴얼

---

## 1. 확정 브랜드 컬러 팔레트 (피그마·캔바 복붙용)

순수 검정(`#000000`) 대신 **다크 초코 브라운(`#3A2B24`)**을 외곽선과 텍스트에 쓰면, AI로 뽑은 오브젝트와 UI가 한 브랜드처럼 따뜻하고 고급스럽게 묶입니다.

| 용도 | 컬러명 | HEX 코드 | 사용 규칙 |
|---|---|---|---|
| **외곽선 · 기본 글자** | 다크 초코 브라운 | `#3A2B24` | 모든 오브젝트 외곽선, 버튼 테두리, 본문 텍스트 |
| **UI 카드 · 패널** | 크림 페이퍼 | `#FFF8EC` | 스킬트리 패널, 상단/하단 HUD 바, 안내 카드 |
| **T1 벽면** | 햇살 바닐라 | `#FFF6E6` | 1단계 공방 상단 배경 |
| **T1 작업대** | 따뜻한 우드 | `#E5C39C` | 작업대 나무 표면 |
| **중앙 작업 매트** | 세이지 민트 | `#98C1A8` | 고물·부품이 올라가는 중앙 매트 *(저채도로 오브젝트 강조)* |
| **핵심 포인트 1** | 머스타드 골드 | `#FFD166` | 나사, 스킬트리 강조, 24K 금괴, 피버 테두리 |
| **핵심 포인트 2** | 코랄 오렌지 | `#FF7B54` | 유압 파쇄기, 보스 배지, 고물 포인트 컬러 |
| **핵심 포인트 3** | 스카이 민트 | `#4EA8DE` / `#52B788` | 로봇 팔, 전자석, 현금 증가(`+`) 컬러 |

---

## 2. 화풍 100% 통일 AI 프롬프트 마스터 템플릿 (복붙용)

**절대 원칙:** Midjourney / Google AI Studio(Imagen·Gemini) / ChatGPT 이미지 생성 어디를 쓰든, **앞부분의 `[공통 화풍 고정문]`을 한 글자도 바꾸지 말고 그대로 복사**해 `[대상]`만 바꿔 생성하세요! 그래야 95장이 전부 한 사람이 그린 것처럼 보입니다.

### ① 고물 오브젝트용 (17종)
> **복사 프롬프트 (영문 권장 — AI 화풍 일관성이 훨씬 높음):**
> ```text
> Cozy cute 2D indie game asset illustration of [a vintage retro red cassette radio], front-top slightly angled view, chunky rounded toy-like proportions, thick smooth dark brown outline (#3A2B24), flat cel-shading with soft warm highlights, vibrant pastel-vivid colors, sticker art style, clean isolated object centered on a pure solid white background, no text, no shadow on floor, no hands, game sprite style
> ```
- **`[대괄호]` 교체 예시 (T1 동네 공방 5종):**
  1. `a vintage retro coral-red cassette radio` (`t1_radio_closed.png`)
  2. `a cute retro pastel mint two-slice toaster` (`t1_toaster_closed.png`)
  3. `a chunky vintage cream mechanical keyboard with colorful keycaps` (`t1_keyboard_closed.png`)
  4. `a cute retro mint table fan with rounded cage` (`t1_fan_closed.png`)
  5. `a bulky retro wooden-frame CRT television with glowing cyan screen, boss item` (`t1_boss_tv_closed.png`)
- **뚜껑 열린 버전(`_open.png`) 만들기 팁:** 온전한 이미지가 마음에 들면, 이미지 편집(Image-to-Image / Gemini 편집)에 그 이미지를 넣고 `"Keep the exact same style and outline. Pop open the casing to reveal cute glowing copper coils, green circuit board, and tiny gears inside."`라고 지시하면 형태가 100% 일치합니다!

### ② 부품 & 주괴 아이콘용 (12종)
> ```text
> Cozy cute 2D indie game icon of [a gleaming stacked 24K gold ingot bar with tiny sparkles], chunky simplified silhouette, thick smooth dark brown outline (#3A2B24), clean cel-shading, bright readable colors even at small size, sticker icon style, centered on pure solid white background, no text, no drop shadow
> ```

### ③ 고정 슬롯 기계용 (5종 — 파츠 분리 팁)
> ```text
> Cozy cute 2D indie game machine sprite of [a chunky coral-orange and yellow hydraulic crusher press head with yellow-black cute hazard stripes], front view, rounded industrial-toy design, thick smooth dark brown outline (#3A2B24), flat cel-shading, isolated on pure solid white background, no text, no background clutter
> ```
- **움직이는 기계 팁:** 유압 파쇄기처럼 움직이는 기계는 **"기계 몸체"와 "내려찍는 철판(헤드)"**을 따로 만들거나 헤드 1장만 뽑아주면, 위아래 내려찍는 동작과 흔들림은 Godot 코드가 알아서 처리합니다!

### ④ 공방 배경 4단계 (T1~T4)
> ```text
> Cozy bright 2D indie game workshop background, flat front-top view of a warm wooden workbench with a large empty muted sage-green cutting mat in the center, sunlit pastel cream pegboard wall at top third with blurred cute tools, clean stylized 2D illustration, soft dark brown outlines, low-contrast calm surface in the center so foreground game objects pop out clearly, 16:9 aspect ratio, no characters, no text
> ```
- **중요 포인트:** 중앙 작업 매트 영역(`empty muted sage-green cutting mat in the center`)이 **비어 있고 차분해야** 그 위에 올라오는 고물과 부품이 묻히지 않습니다!

### ⑤ 스팀 메인 캡슐 아트 (클릭률의 절반!)
> **1단계 (AI로 중앙 일러스트 생성):**
> ```text
> Eye-catching Steam game capsule key art, cozy vibrant 2D illustration style with thick dark brown outlines. Center focus: a chunky cute coral-and-yellow hydraulic press smashing a retro robot and vintage TV in a bright sunny workshop, showering gleaming 24K gold bars, glowing blue microchips, copper coils, and golden brass screws outward in dynamic motion. High contrast, joyful satisfying energy, leave clean empty space on the upper-left for game logo text, no text in image, 16:9 composition
> ```
> **2단계 (여자친구 캔바/피그마 마감):** AI가 만든 그림 좌상단 여백에 **굵고 둥근 한글/영문 타이틀 로고** (예: `SCRAP TO GOLD` / `뜯고 녹여라!`)를 직접 얹고, 외곽선+그림자로 **231×87px 초소형 썸네일에서도 글자가 1초 만에 읽히는지** 축소 확인!

---

## 3. 'AI 티' 100% 지우는 여자친구 5분 후가공 루틴

AI 생성 이미지를 그대로 게임에 넣으면 배경 잔여물과 색감 차이 때문에 어설퍼 보입니다. 아래 **4단계(장당 2~3분)**만 거치면 상업 인디게임 퀄리티로 확 바뀝니다.

1. **배경 투명화 (누끼):** 캔바/포토샵/무료 배경 제거 툴로 흰 배경 제거 → 테두리에 하얀 픽셀 찌꺼기가 남았는지 어두운 배경을 깔아 10초 확인
2. **색온도 통일 (가장 중요!):** 모든 오브젝트에 **따뜻함(Warmth) +5 ~ +10, 채도 통일**을 똑같이 적용해 "같은 조명 아래 있는 물건"처럼 맞추기
3. **여백(Padding) & 해상도 맞추기:**
   - 고물 오브젝트: 가로 기준 `400~512px` 투명 PNG (상하좌우 여백 최소)
   - 부품·주괴 아이콘: `128×128px` 정사각형 투명 PNG
   - 공장 배경: `1280×720px` (또는 2배 `2560×1440px`) PNG
4. **지정 파일명으로 `game/assets/`에 넣기:** 아래 파일명 그대로 저장하면 끝! *(코드 수정 불필요)*

---

## 4. 코드 충돌 0% 에셋 파일명 체크리스트

> [!TIP]
> **지금 당장 해볼 수 있는 마법 테스트!**
> 아래 목록의 첫 번째 파일인 `t1_radio_closed.png`를 아무 AI 툴로 1장 뽑아 배경 제거 후 `couple-steam-game/game/assets/scraps/t1_radio_closed.png`에 넣으면, **프로토타입(F5)을 켰을 때 도형 라디오가 여자친구가 만든 코지 라디오 그림으로 자동 교체됩니다!**

### `game/assets/scraps/` (고물 — Phase 2까지 우선 T1 5종만 먼저!)
- [ ] `t1_radio_closed.png` / `t1_radio_open.png` (카세트 라디오)
- [ ] `t1_toaster_closed.png` / `t1_toaster_open.png` (레트로 토스터)
- [ ] `t1_keyboard_closed.png` / `t1_keyboard_open.png` (기계식 키보드)
- [ ] `t1_fan_closed.png` / `t1_fan_open.png` (민트 선풍기)
- [ ] `t1_boss_tv_closed.png` / `t1_boss_tv_open.png` (보스: 브라운관 TV)
- *(T2~T4 12종은 Phase 3에 동일 규칙: `t2_washer`, `t2_vending`, `t2_scooter`, `t2_boss_car`, `t3_server`, `t3_excavator`, `t3_drone`, `t3_boss_mech`, `t4_satellite`, `t4_rocket`, `t4_ufo`, `t4_boss_mothership`)*

### `game/assets/parts/` (`128×128px` PNG)
- [ ] `screw.png` · `iron_plate.png` · `copper_wire.png` · `pcb.png` · `battery.png` · `gear.png` · `gold_chip.png` · `energy_core.png`
- [ ] `copper_ingot.png` · `silver_ingot.png` · `gold_ingot.png` · `cosmic_ingot.png`

### `game/assets/backgrounds/` (`1280×720px` PNG)
- [ ] `bg_t1.png` (햇살 1평 공방) · `bg_t2.png` (파스텔 창고) · `bg_t3.png` (토이·로봇 팩토리) · `bg_t4.png` (구름 위 우주 공방)

---

## 5. 이번 주 여자친구 첫 미션: '가짜 스크린샷(Fake Screenshot)' 1장 만들기 (30분)

게임을 다 만들기 전에, **"이 게임이 완성되면 스팀에서 얼마나 예뻐 보일까?"**를 미리 확인하는 Phase 0 핵심 미션입니다.

1. 위 **④ 배경 프롬프트**로 `햇살 드는 1평 공방` 배경 1장 생성
2. 위 **① 고물 프롬프트**로 `카세트 라디오` 1장 + **② 부품 프롬프트**로 `구리 코일`, `24K 금괴` 2장 생성
3. 캔바 또는 피그마에서 **1280×720 캔버스** 위에:
   - 배경 깔기 → 중앙에 라디오 올리기 → 주변에 나사와 금괴 흩뿌리기 → 상단에 크림색 HUD(`$1,250`, `[스킬트리]`) 얹기!
4. 이 1장을 두 분이 같이 보고 **"스팀에서 이 스크린샷 보면 클릭하고 싶다!"**가 나오면 **Phase 0 비주얼 통과 확정**입니다!
