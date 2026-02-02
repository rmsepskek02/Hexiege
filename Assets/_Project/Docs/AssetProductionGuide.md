# Hexiege - 에셋 제작 현황 및 가이드

**버전:** 0.4.0
**최종 수정일:** 2026-02-02
**작성자:** HANYONGHEE

---

## 📋 목차

1. [에셋 제작 도구](#에셋-제작-도구)
2. [Gemini 프롬프트 작성 가이드](#gemini-프롬프트-작성-가이드)
3. [폴더 구조 및 명명 규칙](#폴더-구조-및-명명-규칙)
4. [제작 완료 에셋](#제작-완료-에셋)
5. [제작 예정 에셋](#제작-예정-에셋)
6. [프로토타입 범위 제외 항목](#프로토타입-범위-제외-항목)

---

## 🛠️ 에셋 제작 도구

- **이미지 생성:** Google Gemini (AI 이미지 생성)
- **스타일:** 2D 캐주얼, 밝고 친근한 느낌, 아이소메트릭 카툰
- **아웃라인:** 두꺼운 검정 외곽선 (thick black outlines)
- **배경:** 투명 또는 단색 배경 (후처리로 제거)

---

## 📝 Gemini 프롬프트 작성 가이드

### 공통 키워드

프롬프트에 항상 포함할 키워드:
- `flat design, mobile game UI, bright colors, rounded corners` (UI 요소)
- `cartoon style, thick black outlines, isometric` (건물/유닛)
- `clean, simple, readable at small sizes` (아이콘류)

### Gemini 사용 시 주의사항

1. **스타일 일관성 유지**
   - 새 에셋 생성 시 기존 에셋 이미지를 참고 이미지로 함께 첨부
   - 같은 세트(건물 3종, 유닛 방향별 등)는 한 세션에서 연속 생성하면 일관성 향상

2. **아이콘/UI 생성 시**
   - 정사각(square format) 또는 가로형(horizontal) 명시
   - `centered composition` 지정
   - 작은 크기에서도 식별 가능하도록 `icon-friendly, minimal details` 추가
   - 결과가 너무 디테일하면 `simplified` 키워드 추가

3. **유닛 스프라이트 생성 시**
   - 방향 명시: NE(오른쪽 위), E(오른쪽), SE(오른쪽 아래) 3방향만 제작
   - 나머지 3방향(NW, W, SW)은 flipX 반전으로 처리
   - 파일명 규칙: `{동작}_{방향}_{프레임번호}.png`
   - 동일한 캐릭터 참고 이미지를 매번 첨부

4. **건물 스프라이트 생성 시**
   - 아이소메트릭 앵글 통일
   - 배경 없이 건물만 생성
   - UI 아이콘 용도로 별도 제작 불필요 (원본 이미지 축소 재사용)

5. **배경 처리**
   - Gemini 생성 이미지는 배경 포함될 수 있음
   - **내부가 어두운 에셋은 흰색 배경으로 생성** (핑크 배경 시 내부 색상도 같이 제거될 수 있음)
   - 밝은 에셋은 핑크 배경 사용 가능
   - 또는 `transparent background, PNG` 키워드 시도

### Claude Code에 프롬프트 작성 요청 시 규칙

Claude Code에게 Gemini용 프롬프트를 작성해달라고 요청할 때, Claude Code는 다음을 반드시 지켜야 한다:

1. **영문 프롬프트 작성** - Gemini에 바로 붙여넣을 수 있는 영문 프롬프트 제공
2. **한글 설명 추가** - 프롬프트의 의도와 핵심 포인트를 한글로 설명
3. **첨부 이미지 안내** - Gemini에 프롬프트와 함께 첨부해야 할 참고 이미지가 무엇인지 명시
   - 예: "기존 배럭 원본 이미지를 함께 첨부하세요"
   - 예: "기존 UI 패널 이미지를 스타일 참고용으로 첨부하세요"

---

## 📂 폴더 구조 및 명명 규칙

### 폴더 구조

모든 경로는 `Assets/_Project/Sprites/` 기준.

```
Sprites/
├── UI/
│   ├── Buttons/        ← 버튼 에셋
│   ├── Panels/         ← 패널 배경
│   ├── Bars/           ← 프로그레스/체력 바 프레임
│   ├── Icons/          ← HUD 아이콘 (골드, 인구수, 타이머 등)
│   └── Slots/          ← 아이콘 슬롯, 생산 큐 슬롯
├── Buildings/          ← 건물 스프라이트
├── Units/
│   └── Pistoleer/      ← 권총병 (유닛 타입별 폴더)
│       ├── Idle/       ← 대기 스프라이트
│       ├── Walk/       ← 이동 스프라이트
│       └── Attack/     ← 공격 스프라이트
└── Tiles/              ← 타일 스프라이트
```

### 명명 규칙

- **전체:** `snake_case`, 소문자 영문, `.png` 확장자
- **UI:** `ui_{카테고리}_{이름}_{상태}.png`
- **건물 (플레이어):** `bld_{이름}.png`
- **오브젝트 (중립/맵):** `obj_{이름}.png`
- **유닛:** `pistoleer_{동작}_{방향}_{프레임번호}.png`
- **타일:** `tile_{이름}.png`
- **방향:** `ne` / `e` / `se` (소문자, 3방향만 제작, NW/W/SW는 flipX)
- **프레임번호:** 01부터 시작 (2자리 패딩)

---

## ✅ 제작 완료 에셋

### UI 기본 요소

| 파일명 | 폴더 | 설명 | 비고 |
|--------|------|------|------|
| `ui_btn_gold_normal.png` | UI/Buttons/ | 골드+베이지 버튼 (보통) | 9-slice 대응, pressed/disabled는 코드 틴트 처리 |
| `ui_panel_light.png` | UI/Panels/ | 나무 프레임 + 양피지 내부 | 정보 표시용, 9-slice 대응 |
| `ui_panel_dark.png` | UI/Panels/ | 나무 프레임 + 어두운 내부 | 생산 패널/하단 패널용, 9-slice 대응 |
| `ui_slot_icon.png` | UI/Slots/ | 돌/양피지 정사각 프레임, V자 노치 | 유닛/건물 아이콘 프레임 |
| `ui_slot_queue.png` | UI/Slots/ | 정사각형, 갈색 테두리 + 어두운 내부 | 생산 대기열 슬롯 |
| `ui_bar_progress_frame.png` | UI/Bars/ | 갈색 나무 테두리 가로형 바 | fill 이미지와 조합 |
| `ui_bar_hp_frame.png` | UI/Bars/ | 다크레드 단색 가로형 바 | fill 이미지와 조합 |
| `ui_bar_alt_frame.png` | UI/Bars/ | 바 프레임 변형 | 다용도 바 프레임 |
| `ui_icon_gold.png` | UI/Icons/ | 골드 코인 아이콘 | HUD 자원 표시 |
| `ui_icon_population.png` | UI/Icons/ | 인구수 표시 아이콘 | HUD 사용 |
| `ui_icon_timer.png` | UI/Icons/ | 금색 알람시계 | HUD 타이머 옆 배치 |
| `ui_icon_rallypoint.png` | UI/Icons/ | 나무 기둥 + 노란 깃발 | 맵 위 이동 목적지 표시 |

### 건물 스프라이트 (3종) - UI 아이콘으로 재사용

| 파일명 | 폴더 | 설명 | 비고 |
|--------|------|------|------|
| `bld_mining_post.png` | Buildings/ | 플레이어 채굴소 (나무 프레임 + 석조 아치 + 도르래 + 금화 광차) | 중립 금광 위에 건설 |
| `bld_castle.png` | Buildings/ | 빨간 원뿔 지붕 + 깃발 + 성벽 + 4개 탑 | 본기지 |
| `bld_barracks.png` | Buildings/ | 나무 건물 + 빨간 지붕 + 교차 검 문양 + 굴뚝 연기 | 병영 |

### 맵 오브젝트

| 파일명 | 폴더 | 설명 | 비고 |
|--------|------|------|------|
| `obj_goldmine.png` | Buildings/ | 중립 금광 (바위 + 금 결정 + 금화) | 맵에 배치, 플레이어가 채굴소 건설 |

### 유닛 스프라이트 - 권총병

| 파일명 | 폴더 | 방향 | 설명 |
|--------|------|------|------|
| `pistoleer_idle_ne_01.png` | Units/Pistoleer/Idle/ | NE | 대기 1프레임 (NW는 flipX) |
| `pistoleer_idle_e_01.png` | Units/Pistoleer/Idle/ | E | 대기 1프레임 (W는 flipX) |
| `pistoleer_idle_se_01.png` | Units/Pistoleer/Idle/ | SE | 대기 1프레임 (SW는 flipX) |
| `pistoleer_walk_ne_01.png` | Units/Pistoleer/Walk/ | NE | 이동 1프레임 (NW는 flipX) |
| `pistoleer_walk_e_01.png` | Units/Pistoleer/Walk/ | E | 이동 (W는 flipX) |
| `pistoleer_walk_e_02.png` | Units/Pistoleer/Walk/ | E | 이동 프레임 2 |
| `pistoleer_walk_se_01.png` | Units/Pistoleer/Walk/ | SE | 이동 1프레임 (SW는 flipX) |
| `pistoleer_attack_ne_01.png` | Units/Pistoleer/Attack/ | NE | 공격 (NW는 flipX) |
| `pistoleer_attack_ne_02.png` | Units/Pistoleer/Attack/ | NE | 공격 프레임 2 |
| `pistoleer_attack_e_01.png` | Units/Pistoleer/Attack/ | E | 공격 (W는 flipX) |
| `pistoleer_attack_e_02.png` | Units/Pistoleer/Attack/ | E | 공격 프레임 2 |
| `pistoleer_attack_se_01.png` | Units/Pistoleer/Attack/ | SE | 공격 (SW는 flipX) |
| `pistoleer_attack_se_02.png` | Units/Pistoleer/Attack/ | SE | 공격 프레임 2 |
| `pistoleer_portrait.png` | Units/Pistoleer/ | - | 초상화 (UI 아이콘용) |

### 타일 스프라이트

| 파일명 | 폴더 | 설명 | 비고 |
|--------|------|------|------|
| `tile_hex.png` | Tiles/ | 3/4뷰 육각형 타일 | SpriteRenderer.color로 팀 색상 적용 |

---

## 📌 제작 예정 에셋

### Gemini로 제작

프로토타입에 필요한 Gemini 제작 에셋은 모두 완료됨.

### 코드로 처리 (에셋 제작 불필요)

| # | 에셋 | 설명 | 처리 방안 |
|---|------|------|----------|
| 1 | **타일 선택 하이라이트** | 선택 시 노란 테두리/글로우 | SpriteRenderer.color 노란 틴트 or 오버레이 |
| 2 | **이동 경로 표시** | 경로 타일 표시 | 경로 타일 반투명 색상 오버레이 (코드 처리) |
| 3 | **프로그레스 바 fill** | 생산 진행률 채우기 | 단색 스프라이트 + Image.fillAmount |
| 4 | **체력 바 fill** | HP 잔량 채우기 | 단색 스프라이트 + Image.fillAmount |
| 5 | **타이머 패널** | 상단 중앙 타이머 배경 | 기존 패널 배경(어두운) 재사용 |

### 프로토타입 이후 (MVP에서 제작)

| 에셋 | Phase |
|------|-------|
| 업그레이드 버튼 아이콘 | MVP |
| 승리/패배 화면 배경 | MVP |
| 자동생산 표시 이펙트 | MVP |
| 추가 유닛 아이콘 (기관총병, 저격총병) | MVP |
| 추가 건물 아이콘 (방어타워, 연구소, 마법타워) | MVP |
| 서든데스 경고 오버레이 | MVP |
| 타일 점유율 게이지 | MVP |

---

## 🚫 프로토타입 범위 제외 항목

다음 항목은 프로토타입에서 구현하지 않으므로 에셋 제작 불필요:

- 서든데스 UI
- 타일 점유율 게이지
- 마법 관련 UI 전체
- 방어타워/연구소 아이콘
- 다중 유닛 타입 (권총병만 사용)
- 종족 선택 UI
- 로비/메뉴 UI
- 사운드/BGM

---

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.4.0 | 2026-02-02 | 실제 파일 현황 반영, 채굴소/금광 분리(bld_/obj_), Idle/Walk 프레임 수 정정, pistoleer_icon 제거, ui_bar_alt_frame 추가 |
| 0.3.0 | 2026-02-02 | 폴더 구조 및 명명 규칙 추가, 전체 파일명 매핑 테이블 작성 |
| 0.2.0 | 2026-02-02 | 프로토타입 에셋 제작 완료, 타이머 아이콘 추가, 제작 예정 목록 정리 |
| 0.1.0 | 2026-02-02 | 초기 문서 작성 |

---

**문서 끝**
