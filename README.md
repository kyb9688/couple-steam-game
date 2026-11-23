# TrashCleaner3D — 3D 쓰레기집 특수청소 프로젝트 (최신 메인)

> **Unity 6 프로젝트 위치:** **`unity/TrashCleaner3D/`** *(Unity `6000.6.10f1` · Universal 3D URP)*

### 🚀 2인 공유 실행 방법 (여자친구 PC / 본인 PC 공통)
1. **GitHub Desktop**에서 `Fetch origin` → **`Pull origin`** 클릭.
2. **Unity Hub** 열기 → 오른쪽 위 **`추가` → `디스크에서 프로젝트 추가`** → 내 PC의 `couple-steam-game\unity\TrashCleaner3D` 폴더 선택.
3. Unity 에디터가 열리면 상단 메뉴 **`TrashCleaner` → `Build Room 01 Playable Prototype`** 클릭 → 가운데 **Play(`▶`)** 버튼으로 바로 테스트!
   - 조작: `WASD` 이동 · `마우스` 시점 · `좌클릭` 청소/닦기/CCTV 거품 · `1/2/3` 장비 전환 · `E` 슈킹 · `Q` 보관 · `Z/X/C/V` 현장 강화 · `F` 손전등 · `R` 트럭 복귀
4. **충돌 방지 협업 규칙 (필수):**
   - **오빠 (파트 A):** `unity/TrashCleaner3D/Assets/_Project/Scripts/`, `Editor/` 수정
   - **여자친구 (파트 B):** `unity/TrashCleaner3D/Assets/_Project/Scenes/`, `Art/` (방 배치·에셋 추가) 수정  
   *(같은 `.unity` 씬 파일을 두 사람이 동시에 열어서 수정하지 마세요!)*

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
