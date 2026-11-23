# 뜯고 녹여라! (가제: Scrap to Gold)

커플 2인 팀의 첫 스팀(PC) 상업 게임 프로젝트 저장소입니다.  
**나사 하나씩 풀던 1평 공방에서 시작해, 전자석·유압 파쇄기·황금 용광로로 외계 우주선까지 통째로 으깨 금괴를 뽑아내는 밝고 아기자기한 2D 자동화 인크리멘탈 게임**을 만듭니다.

---

## 프로젝트 한눈에 보기
- **목표:** 6개월 내 PC(Steam) 정식 출시 (2026.11 ~ 2027.05) · 초기 매출 **\$1,000 달성**
- **장르 & 가격:** 2D 쇼트 액티브 자동화 인크리멘탈 (2.5~4시간 완결형 · \$4.99~\$5.99)
- **엔진 & 협업:** Godot 4.3+ (GDScript) · AI 코딩 & AI 아트(스팀 투명 공개) · GitHub Desktop 협업
- **역할 분담 (DRI):**
  - **본인 (Systems & Build Lead):** 코어 루프, 밸런스(`game/data/*.json`), AI 코딩(`game/scripts/`), 스팀 빌드
  - **여자친구 (Visual, Brand & Growth Lead):** 코지 공방 화풍·UI·캡슐 아트(`game/assets/`), 스토어·마케팅·일정 관리

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
