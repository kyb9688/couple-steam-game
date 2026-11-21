# AI 코딩 에이전트 전용 규칙서 — 뜯고 녹여라! (Scrap to Gold)

이 프로젝트는 **코딩 경험이 없는 커플 2인 팀**이 **Godot 4.3+ (GDScript)**와 AI 코딩 에이전트로 개발하는 2D 쇼트 액티브 자동화 인크리멘탈 게임입니다. 모든 코드 수정 시 아래 규칙을 반드시 지키세요.

## 1. 엔진 및 문법 고정 규칙 (Godot 4.3+)
- **Godot 4.x 문법만 사용**합니다. Godot 3.x 구문(`yield`, `rand_range`, `move_and_slide(vel)`, `Tween` 노드, `OS.window_size` 등)을 절대 사용하지 마세요.
- 타이머/연출은 `create_tween()`, 난수는 `randf_range()` / `randi_range()`, 파일 입출력은 `FileAccess`를 사용합니다.
- JSON에서 읽어온 숫자는 Godot에서 `float`로 파싱되므로, 정수 변수에 넣을 때 반드시 `int(...)`로 변환하세요.
- 렌더러는 모든 일반 PC 호환을 위해 `GL Compatibility`를 유지합니다.

## 2. 데이터 주도(Data-Driven) 밸런스 분리 규칙
- 고물 스펙, 부품 가격, 스킬트리 비용/효과 수치를 `.gd` 코드 안에 하드코딩하지 마세요.
- 모든 밸런스 데이터는 `res://data/` 하위 JSON(`scraps.json`, `parts.json`, `skills.json`)에서 읽어와야 합니다.

## 3. 한글 폰트 & UI 안전 규칙
- Godot 4 기본 폰트에는 한글이 없으므로, 모든 UI와 `draw_string()`은 `GameManager.ui_font` (`SystemFont`: Malgun Gothic 등)를 사용합니다.
- 컬러 이모지 대신 깔끔한 한글/영문 텍스트와 단순 기호(`★`, `⚡`, `▶`, `+`, `×`, `$`)를 사용하세요.

## 4. 에셋 교체 친화 구조 (여자친구 협업 경계)
- Phase 1은 도형(Procedural Cozy Style)으로 동작하되, `res://assets/`에 정해진 파일명의 PNG가 추가되면 코드 수정 최소화(또는 자동 로드)로 교체할 수 있게 구조를 유지합니다.
- 프레임 애니메이션을 요구하지 말고, 모든 움직임(탄성, 파쇄, 흔들림, 회전, 파티클)은 코드 트윈과 파티클로 처리합니다.

## 5. 스코프 보호 & 작업 안전 수칙
- `docs/spec/scrap-to-gold-spec.md`의 **Anti-Scope**(자유 배치 건설, 인벤토리 슬롯, 멀티플레이, 모바일 등)를 절대 추가하지 마세요.
- 한 번에 1개 기능만 작게 수정하고, 수정 전 GitHub Desktop 커밋을 권장합니다.
- `F9` 디버그 치트 패널은 개발 완료 전까지 항상 유지해 빠른 밸런스 테스트를 보장하세요.
