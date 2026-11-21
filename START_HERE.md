# 첫 실행 가이드 (START HERE) — 뜯고 녹여라! 프로토타입 v0.1

코딩 경험이 전혀 없어도 **5분 안에 게임을 직접 실행하고 밸런스를 만져볼 수 있도록** 세팅해 두었습니다.

---

## 1. 5분 만에 프로토타입 켜보기 (본인 담당)

1. **Godot 4 무료 다운로드:** https://godotengine.org/download/windows/ 에서 **Standard 버전 (Godot 4.3 이상, .NET 아님)** 압축 파일 다운로드 후 압축 해제 *(설치 없이 exe 바로 실행!)*
2. 실행된 프로젝트 매니저에서 **[가져오기(Import)]** 클릭 → 아래 경로의 `project.godot` 선택:
   - `C:\Users\u34h81\.gemini\antigravity\scratch\couple-steam-game\game\project.godot`
3. 에디터가 열리면 우측 상단 **재생(▶) 버튼** 또는 키보드 **`F5`** 누르기!

---

## 2. 첫 플레이 테스트 체크리스트 (10분 체험)

1. **수동 손맛 체험:** 노란 나사 위에 마우스를 올려 *드르륵* 풀고, 튀어나온 부품을 마우스로 쓸어 담아보세요. *(좌클릭을 누르고 있으면 더 빨라집니다!)*
2. **스킬트리(`Tab`) 첫 자동화:** 돈을 모아 **전동 드라이버 → 자동 컨베이어 → 자동 드라이버 팔 → 초강력 전자석** 순서로 찍어보세요.
3. **핵심 도파민 테스트 (`F9` 디버그 치트 활용 가능!):**
   - `F9`를 눌러 밸런스 테스트 치트 창을 열고, **`유압 파쇄기 ★`**와 **`황금 용광로 ★`**를 켜보세요!
   - 유압 파쇄기가 **콰앙!** 내려찍으며 화면이 흔들리고, 부품이 용광로로 빨려 들어가 **24K 금괴**로 튀어나오는 느낌을 확인해 보세요.
4. **티어 확장 & 엔딩 확인:** T2~T4 공방을 해금하고 상단 **[★ 보스 고물 호출]**로 각 티어 보스와 최종 보스(`추락한 외계 모선`) 엔딩 카드까지 훑어보세요.

---

## 3. 코드 없이 숫자 밸런스 고쳐보기 (본인 강점 구간!)

코드(`.gd`)를 열 필요 없이 아래 3개 JSON 파일을 메모장/VS Code로 열어 숫자만 바꾸고 저장한 뒤 `F5`를 누르면 즉시 반영됩니다.
- `game/data/scraps.json` — 고물 17종의 나사 개수, 해제 시간, 드롭 부품 수량
- `game/data/parts.json` — 부품·주괴 12종의 기본 가격
- `game/data/skills.json` — 스킬 18종의 기본 비용(`base_cost`), 가격 상승 배율(`cost_mult`), 최대 레벨(`max_level`)

---

## 4. 이번 주 여자친구 첫 미션

- [cozy-workshop-art-kit.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/art/cozy-workshop-art-kit.md)를 열고:
  1. **가짜 스크린샷(Fake Screenshot) 1장** 조합해 보기 (30분)
  2. `t1_radio_closed.png` 1장을 뽑아 `game/assets/scraps/`에 넣어 프로토타입에 그림이 자동 적용되는지 함께 확인해 보기!

---

## 프로젝트 전체 문서 지도
- 의도 확정서: [docs/intent/steam-first-game.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/intent/steam-first-game.md)
- 6개월 로드맵 & 2인 협업 가이드: [docs/plan/master-roadmap.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/plan/master-roadmap.md)
- 게임 기획·기술 명세서 v1.1: [docs/spec/scrap-to-gold-spec.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/spec/scrap-to-gold-spec.md)
- 여자친구용 AI 아트 & 브랜딩 키트: [docs/art/cozy-workshop-art-kit.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/docs/art/cozy-workshop-art-kit.md)
- AI 코딩 규칙서: [game/AGENTS.md](file:///c:/Users/u34h81/.gemini/antigravity/scratch/couple-steam-game/game/AGENTS.md)
