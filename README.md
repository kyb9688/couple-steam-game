# TrashCleaner3D — 3D 쓰레기집 특수청소 프로젝트 (최신 메인)

> **Unity 6 프로젝트 위치:** **`unity/TrashCleaner3D/`** *(Unity `6000.6.10f1` · Universal 3D URP)*

### 🚀 2인 공유 실행 방법 (여자친구 PC / 본인 PC 공통)
1. **GitHub Desktop**에서 `Fetch origin` → **`Pull origin`** 클릭.
2. **Unity Hub** 열기 → 오른쪽 위 **`추가` → `디스크에서 프로젝트 추가`** → 내 PC의 `couple-steam-game\unity\TrashCleaner3D` 폴더 선택.
3. Unity 에디터가 열리면 상단 메뉴 **`TrashCleaner` → `Build Room 01 Playable Prototype`** 클릭 → 가운데 **Play(`▶`)** 버튼으로 바로 테스트!
   - 조작: `WASD` 이동 · `마우스` 시점 · `좌클릭(꾹)` 바닥 쓰레기 흡입 · `좌클릭(단타)` 큰 봉투 타격 · `우클릭` 봉투 투척/회수/묶기 · `E` 트럭 옆 현장 강화 · `F` 현관문 · `T` 손전등 · `Tab` 강화 트리 · `K` 80% 조기 정산 · `ESC` 설정
4. **충돌 방지 협업 규칙 (필수):**
   - **오빠 (파트 A):** `unity/TrashCleaner3D/Assets/_Project/Scripts/`, `Editor/` 수정
   - **여자친구 (파트 B):** `unity/TrashCleaner3D/Assets/_Project/Scenes/`, `Art/` (방 배치·에셋 추가) 수정  
   *(같은 `.unity` 씬 파일을 두 사람이 동시에 열어서 수정하지 마세요!)*

---

## v1.1 코어 루프 — 무더기 밀기 → 트럭에 와르르 환전 → 트럭 옆 현장 강화 (2026-11-26)

플레이어가 **청소 → 환전 → 강화** 3박자에만 집중하도록 부가 기믹은 꺼두고 손맛·템포·연출만 다듬은 버전입니다.

| 항목 | v0.9.3 | v1.0 | v1.1 (밸런스 보정) |
|---|---|---|---|
| 바닥 쓰레기 줍기 | 1.00초 홀드 | 0.22초 홀드 (도구 강화 시 0.07초) | 동일 |
| 봉투 용량(초반) | 15칸 | 30칸 | **78칸** (맨손 4.5개/s → 약 17~21초 연속 수거) |
| 첫 강화 가격 | $20 | $12~18 | **$12~24** |
| 업그레이드 트리 총액 | — | $13,038 | **$26,065** (1라운드에 43%만 열림 · 진공청소기는 2라운드) |
| 수거 연출 | 직선 이동 | **포물선으로 빨려 들어오며 축소 + 도레미파솔 수거음** | 동일 |
| 트럭 환전 | 즉시 지급 | **다발 연출 + 돈 드르르륵 카운트업** | 카운트업 중 라운드 종료 시 즉시 정산 |
| 강화 장소 | 방 안 [Tab] 트리 | **[Tab] 트리 + 트럭 옆 현장 발판 3개(범위·흡입·용량)** | 발판 위치를 콜라이더 실측으로 안전 배치 |
| 쓰레기 배치 | 방 전체 균등 | **5개 구역 무더기(총 1,200개) → 한 덩어리 밀면 바닥이 드러남** | 동일 |

**v1.1 밸런스 보정 요약** (코드만 보고 수치·동선을 시뮬레이션해 찾은 항목)
- 초반 6.6초마다 트럭으로 뛰던 왕복 템포 → 봉투 1Lv 78칸으로 17~21초 연속 수거, 이후 단계도 8~19초 유지
- **투하 콤보 배율**이 레벨당 +10%씩 올라 최대 ×5.25였던 숨은 폭딜 → 스택당 ×1.25 고정(레벨업은 유지 시간만)
- **거리·차량 보너스**가 봉투당 고정 금액(트럭 9Lv = 봉투당 +$180)이라 봉투를 잘게 나눌수록 이득이던 구조 → 봉투 값 비례(%)로 변경
- 1라운드에 트리 전체가 열리던 문제 → 상위 단계(진공청소기·1톤 트럭·자석) 비용을 올려 1라운드는 40~50%, 2~3라운드에 고티어를 찍도록 조정
- 트럭 옆 현장 강화 존을 `side * 3.0m` 고정 좌표 대신 실제 콜라이더 여유를 재서 배치(울타리 밖·트럭 겹침 방지)

**꺼둔 부가 기믹** — 동종 연속 압축 / 의심 80~95% 아드레날린 구간 / 막판 10% 잭팟 / 보물 [Q]전달·[E]슈킹·신뢰도(★)·암시장(♦) 분기.
코드는 삭제하지 않고 분기만 해두었으므로, `Assets/_Project/Scripts/CoreLoopConfig.cs`의 `CoreLoopOnly = false` 한 줄이면 v0.9.3 규칙이 그대로 되살아납니다.

**밸런스 수치 위치**
- `GamePrototypeManager.cs` — `BagCapByLv`, `ToolStepCosts`, `TruckStepCosts`, `ToolHoldDurs`, `Cost*` 프로퍼티
- `InstancedTrashSwarm.cs` — `InitialSwarmCount`, `AddUnit`의 `value`, `ClusterCenters`
- `FieldUpgradeStation.cs` — 발판 3종 구성(범위 sweep · 흡입 tool · 용량 bag)

---

## 핵심 문서 바로가기 (GitHub에서 클릭해 바로 읽기)

| 문서 | 대상 | 내용 |
|---|---|---|
| **[docs/spec/GAME_DESIGN_v1.0.md](docs/spec/GAME_DESIGN_v1.0.md)** | **둘 다 (최신 확정안)** | **3D 쓰레기집 특수청소 (`TrashCleaner3D`) 게임 기획 확정서 v1.0** |
| **[docs/spec/CONTENT_PLAN_v1.0.md](docs/spec/CONTENT_PLAN_v1.0.md)** | **둘 다 (최신 초안)** | **캠페인 방 6종 · 의뢰 메모 · 증강 카드 16종 · 에셋 체크리스트** |
| **[START_HERE.md](START_HERE.md)** | 둘 다 | Godot 4 첫 실행(5분 컷) 및 밸런스 테스트 방법 |
| **[HANDOFF.md](HANDOFF.md)** | AI 공유 두뇌 | 새 채팅·여자친구 PC AI에 붙여넣어 맥락 100% 복제 |
| **[docs/intent/steam-first-game.md](docs/intent/steam-first-game.md)** | 둘 다 | 프로젝트 목표 · 제약 조건 · 하지 않을 것(Out of Scope) |
| **[docs/plan/master-roadmap.md](docs/plan/master-roadmap.md)** | 둘 다 | 6개월 마스터 로드맵 · 2인 협업 헌장 · 행정 체크리스트 |
| **[docs/spec/scrap-to-gold-spec.md](docs/spec/scrap-to-gold-spec.md)** | 둘 다 | 게임 기획·기술 명세서 v1.1 (코어 루프·스킬트리·구조) |
| **[docs/art/cozy-workshop-art-kit.md](docs/art/cozy-workshop-art-kit.md)** | **여자친구 필독** | 컬러 팔레트 · 복붙용 AI 프롬프트 5종 · 에셋 파일명표 |
| **[docs/plan/github-couple-guide.md](docs/plan/github-couple-guide.md)** | 둘 다 | 코딩 전무 2인 팀용 GitHub Desktop 5분 협업 매뉴얼 |

---

## 충돌 0% 폴더 경계 규칙

- **본인 전용 수정 구역:** `game/scripts/`, `game/scenes/`, `game/data/`
- **여자친구 전용 수정 구역:** `game/assets/` *(이미지 업로드)*, `docs/art/`
- **함께 보는 구역:** `docs/plan/`, `docs/spec/` *(수정 전 카톡 한 줄 공유)*
