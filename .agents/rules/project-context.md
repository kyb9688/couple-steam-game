# Project Context Rule — 뜯고 녹여라! (Scrap to Gold)

이 워크스페이스는 커플 2인(코딩 전무 · 각 주 10시간 · 6개월 완주 목표)의 첫 스팀(PC) 상업 게임 프로젝트입니다.

## 필수 참조 문서
- 전체 맥락 요약: `HANDOFF.md`
- 확정 의도 & Out of Scope: `docs/intent/steam-first-game.md`
- 6개월 로드맵 & 협업 헌장: `docs/plan/master-roadmap.md`
- 확정 기획·기술 명세서 v1.1: `docs/spec/scrap-to-gold-spec.md`
- 코지 공방 AI 아트 & 브랜딩 키트: `docs/art/cozy-workshop-art-kit.md`
- Godot 4.3+ AI 코딩 규칙서: `game/AGENTS.md`

## 핵심 고정 규칙
1. **엔진:** Godot 4.3+ (GDScript, GL Compatibility). Godot 3 문법 절대 금지.
2. **밸런스 분리:** 고물·부품·스킬 수치는 코드가 아니라 `game/data/*.json`에서 관리.
3. **비주얼 톤:** 밝고 아기자기한 코지 공방 (`#3A2B24` 다크 초코 브라운 외곽선, 프레임 애니메이션 0개 원칙 — 코드 트윈/파티클로 연출).
4. **스팀 AI 정책:** AI 아트 활용 + 스팀 AI 라벨 투명 공개.
5. **Anti-Scope 준수:** 자유 배치 공장, 인벤토리 창고, 멀티플레이, 모바일, 3D 절대 제안·추가 금지.
6. **협업 경계:** 코드/데이터(`game/scripts/`, `game/data/`)와 아트 에셋(`game/assets/`)의 경계를 유지해 Git 충돌을 방지한다.
