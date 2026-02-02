# Hexiege - 클라이언트 프로토타입 구현 계획서

**버전:** 0.1.0
**최종 수정일:** 2026-02-01
**작성자:** HANYONGHEE

---

## 📋 목차

1. [목표](#목표)
2. [핵심 설계 결정](#핵심-설계-결정)
3. [아키텍처 구조](#아키텍처-구조)
4. [파일 목록](#파일-목록)
5. [구현 순서](#구현-순서)
6. [에셋 전략](#에셋-전략)
7. [AI 스프라이트 통합 방법](#ai-스프라이트-통합-방법)
8. [프로토타입 범위](#프로토타입-범위)
9. [검증 계획](#검증-계획)
10. [씬 구성](#씬-구성)

---

## 🎯 목표

AI 에셋 유료 투자 전에 3가지 기술 검증:

| # | 검증 항목 | 핵심 질문 |
|---|----------|----------|
| 1 | AI 스프라이트 애니메이션 | AI로 생성한 프레임별 스프라이트가 Unity에서 자연스러운 애니메이션으로 동작하는가? |
| 2 | 헥사 타일 시스템 | 11×17 헥스 그리드 생성, 타일 색상 변경, 클릭 선택이 정확하게 작동하는가? |
| 3 | 유닛 이동 + 방향 전환 | 유닛이 헥스 타일 위에서 A* 경로를 따라 이동하며 방향별 스프라이트가 정확히 전환되는가? |

---

## 🔧 핵심 설계 결정

### 1. 커스텀 스프라이트 기반 헥스 그리드 (Unity Tilemap 사용 안 함)

**이유:**
- 3/4뷰(세미 아이소메트릭) 아트 스타일과 Unity Tilemap의 정육각형 제약이 충돌
- 타일당 개별 SpriteRenderer로 색상/선택/오버레이 처리가 용이
- 187개 타일(11×17)로 성능 문제 없음
- 향후 타일별 파티클, 애니메이션 추가에 유연

### 2. 커스텀 FrameAnimator (Unity Animator 사용 안 함)

**이유:**
- 1~2프레임 사이클에 Animator 상태머신은 과잉 (.anim 파일, Controller, Transition 등)
- ScriptableObject(`UnitAnimationData`)에 스프라이트 배열 저장
- 드래그 앤 드롭으로 AI 생성 스프라이트 즉시 교체 가능
- ~50줄 코드로 전체 애니메이션 처리

### 3. 3방향 + flipX = 6방향 시스템

```
제작 방향          flipX 반전 커버
──────────────────────────────────
NE (↗ 오른쪽 위)  → NW (↖ 왼쪽 위)
E  (→ 오른쪽)     → W  (← 왼쪽)
SE (↘ 오른쪽 아래) → SW (↙ 왼쪽 아래)
```

헥스 6방향과 아트 방향 매핑:

| 이동 방향 | 아트 방향 | flipX |
|----------|----------|-------|
| NE (q+1, r-1) | NE | false |
| E  (q+1, r+0) | E  | false |
| SE (q+0, r+1) | SE | false |
| SW (q-1, r+1) | SE | true  |
| W  (q-1, r+0) | E  | true  |
| NW (q+0, r-1) | NE | true  |

---

## 🏛️ 아키텍처 구조

기술 설계서(TDD)의 Clean Architecture를 따름:

```
┌──────────────────────────────────────────────────────────┐
│  Presentation Layer                                       │
│  MonoBehaviour: 렌더링, Unity 이벤트 처리                  │
│  ├─ HexTileView          (타일 비주얼 + 클릭)             │
│  ├─ HexGridRenderer      (그리드 전체 렌더링)             │
│  ├─ UnitView             (유닛 스프라이트 + 이동 비주얼)    │
│  ├─ FrameAnimator        (스프라이트 프레임 순환)           │
│  ├─ CameraController     (팬/줌)                         │
│  ├─ InputHandler         (입력 처리)                      │
│  └─ DebugUI              (디버그 정보)                    │
├──────────────────────────────────────────────────────────┤
│  Application Layer                                        │
│  UseCase + UniRx 이벤트                                   │
│  ├─ GameEvents               (이벤트 허브)                │
│  ├─ GridInteractionUseCase   (타일 선택)                  │
│  ├─ UnitMovementUseCase      (이동 + 타일 점령)           │
│  └─ UnitSpawnUseCase         (유닛 생성)                  │
├──────────────────────────────────────────────────────────┤
│  Domain Layer (순수 C#, Unity 독립)                       │
│  ├─ HexCoord             (큐브 좌표 값 객체)              │
│  ├─ HexDirection         (6방향 + 이웃 오프셋)            │
│  ├─ HexGrid              (그리드 데이터)                  │
│  ├─ HexTile              (타일 상태)                     │
│  ├─ HexPathfinder        (A* 경로탐색)                   │
│  ├─ FacingDirection      (방향 매핑)                     │
│  ├─ UnitData             (유닛 상태)                     │
│  ├─ UnitType             (유닛 타입)                     │
│  └─ TeamId               (팀 열거형)                     │
├──────────────────────────────────────────────────────────┤
│  Infrastructure Layer                                     │
│  ├─ GameConfig           (ScriptableObject 전역 설정)     │
│  ├─ UnitAnimationData    (ScriptableObject 스프라이트)    │
│  └─ UnitFactory          (유닛 프리팹 팩토리)             │
├──────────────────────────────────────────────────────────┤
│  Core Layer (공유 유틸리티)                                │
│  ├─ HexMetrics           (헥스 ↔ 월드 좌표 변환)          │
│  └─ SingletonMonoBehaviour (싱글톤 베이스)                 │
├──────────────────────────────────────────────────────────┤
│  Bootstrap                                                │
│  └─ GameBootstrapper     (씬 진입점, 전체 와이어링)        │
└──────────────────────────────────────────────────────────┘
```

---

## 📁 파일 목록

모든 경로는 `Assets/_Project/` 기준.

### Domain Layer (순수 C#) - 9개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Domain/Common/TeamId.cs` | 팀 열거형 (Neutral, Blue, Red) |
| `Scripts/Domain/Hex/HexCoord.cs` | 큐브 좌표 값 객체 (q, r, s=-q-r) |
| `Scripts/Domain/Hex/HexDirection.cs` | 6방향 열거형 + 이웃 좌표 오프셋 |
| `Scripts/Domain/Hex/HexTile.cs` | 타일 상태 (소유자, 이동가능 여부) |
| `Scripts/Domain/Hex/HexGrid.cs` | 그리드 데이터 구조 (Dictionary) |
| `Scripts/Domain/Hex/HexPathfinder.cs` | 헥스 그리드 A* 경로탐색 |
| `Scripts/Domain/Unit/FacingDirection.cs` | 6방향 → 3아트방향 + flipX 매핑 |
| `Scripts/Domain/Unit/UnitType.cs` | 유닛 타입 열거형 |
| `Scripts/Domain/Unit/UnitData.cs` | 유닛 상태 (위치, 타입, 팀, 방향) |

### Core Layer - 2개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Core/HexMetrics.cs` | 헥스 좌표 ↔ 월드 좌표 변환, 사이징 상수 |
| `Scripts/Core/SingletonMonoBehaviour.cs` | 제네릭 싱글톤 베이스 클래스 |

### Application Layer - 4개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Application/Events/GameEvents.cs` | UniRx Subject 이벤트 허브 |
| `Scripts/Application/UseCases/GridInteractionUseCase.cs` | 타일 선택 처리 |
| `Scripts/Application/UseCases/UnitMovementUseCase.cs` | 경로탐색 + 이동 + 타일 점령 |
| `Scripts/Application/UseCases/UnitSpawnUseCase.cs` | 유닛 생성 관리 |

### Infrastructure Layer - 3개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Infrastructure/Config/GameConfig.cs` | 전역 설정 ScriptableObject |
| `Scripts/Infrastructure/Config/UnitAnimationData.cs` | 방향별 스프라이트 배열 ScriptableObject |
| `Scripts/Infrastructure/Factories/UnitFactory.cs` | 유닛 프리팹 인스턴스 생성 |

### Presentation Layer - 7개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Presentation/Grid/HexTileView.cs` | 타일 비주얼 + 색상 변경 + 선택 |
| `Scripts/Presentation/Grid/HexGridRenderer.cs` | HexGrid → GameObject 렌더링 |
| `Scripts/Presentation/Unit/FrameAnimator.cs` | 스프라이트 프레임 순환 엔진 |
| `Scripts/Presentation/Unit/UnitView.cs` | 유닛 비주얼 + 이동 코루틴 + 방향 전환 |
| `Scripts/Presentation/Camera/CameraController.cs` | 카메라 팬/줌 + 경계 제한 |
| `Scripts/Presentation/Input/InputHandler.cs` | 마우스/터치 입력 → UseCase 연결 |
| `Scripts/Presentation/Debug/DebugUI.cs` | 화면 디버그 정보 표시 |

### Bootstrap - 1개

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Bootstrap/GameBootstrapper.cs` | 씬 진입점, 의존성 와이어링, 테스트 유닛 스폰 |

### 에셋 파일

| 경로 | 용도 |
|------|------|
| `Sprites/Placeholder/` | 플레이스홀더 헥스 타일 + 유닛 스프라이트 |
| `Sprites/AI_Generated/Pistoleer/` | DeeVid AI 생성 스프라이트 저장소 |
| `Prefabs/HexTile.prefab` | 타일 프리팹 (SpriteRenderer + Collider + HexTileView) |
| `Prefabs/Unit_Pistoleer.prefab` | 유닛 프리팹 (SpriteRenderer + UnitView + FrameAnimator) |
| `Resources/Config/GameConfig.asset` | 전역 설정 인스턴스 |
| `Resources/Config/PistoleerAnimData.asset` | 권총병 애니메이션 데이터 인스턴스 |

**총 파일 수:** 스크립트 26개 + 에셋 6개

---

## 📐 구현 순서

### Phase 1: 프로젝트 정리
- `NewMonoBehaviourScript.cs` 삭제
- 위 폴더 구조 생성

### Phase 2: Domain 레이어
1. `TeamId.cs` - 팀 열거형
2. `HexCoord.cs` - 큐브 좌표 (모든 것의 기반)
3. `HexDirection.cs` - 6방향 + 이웃 오프셋
4. `HexTile.cs` - 타일 상태
5. `HexGrid.cs` - 11×17 그리드 생성 (even-r offset → cube 변환)
6. `HexPathfinder.cs` - A* 경로탐색
7. `FacingDirection.cs` - 방향 매핑
8. `UnitType.cs` - 유닛 타입
9. `UnitData.cs` - 유닛 상태

### Phase 3: Core
1. `HexMetrics.cs` - 좌표 변환
2. `SingletonMonoBehaviour.cs` - 싱글톤

### Phase 4: Application
1. `GameEvents.cs` - 이벤트 허브
2. `GridInteractionUseCase.cs` - 타일 선택
3. `UnitMovementUseCase.cs` - 이동 + 점령
4. `UnitSpawnUseCase.cs` - 유닛 생성

### Phase 5: Infrastructure
1. `GameConfig.cs` - 설정 SO
2. `UnitAnimationData.cs` - 애니메이션 SO
3. `UnitFactory.cs` - 팩토리

### Phase 6: Presentation - Grid
1. `HexTileView.cs` - 타일 뷰
2. `HexGridRenderer.cs` - 그리드 렌더러

### Phase 7: Presentation - Unit
1. `FrameAnimator.cs` - 프레임 애니메이터
2. `UnitView.cs` - 유닛 뷰

### Phase 8: Presentation - Camera/Input
1. `CameraController.cs` - 카메라 제어
2. `InputHandler.cs` - 입력 처리

### Phase 9: Bootstrap + Debug
1. `GameBootstrapper.cs` - 진입점
2. `DebugUI.cs` - 디버그

### Phase 10: 플레이스홀더 에셋
- 헥스 타일 스프라이트 (128×96px 흰색 육각형)
- 유닛 스프라이트 (64×96px 방향 표시 캡슐)
- 프리팹 생성 + ScriptableObject 인스턴스 생성

### Phase 11: 통합 테스트
- 3가지 목표 검증 (아래 검증 계획 참고)

---

## 🎨 에셋 전략

### 플레이스홀더 에셋

기술 검증용 임시 에셋. AI 스프라이트 완성 전까지 사용.

**헥스 타일:**
- 128×96px PNG (가로가 넓은 3/4뷰 육각형)
- 흰색 채우기 → `SpriteRenderer.color`로 팀 색상 적용
- PPU(Pixels Per Unit): 128

**유닛 스프라이트:**
- 64×96px PNG, 방향별 3장 (NE, E, SE)
- 화살표로 방향 표시된 캡슐 형태
- PPU: 64

### 팀 색상

```
Neutral: RGB(178, 178, 178) - 회색
Blue:    RGB(77, 128, 230)  - 파랑
Red:     RGB(230, 77, 77)   - 빨강
Selected: 기존 색상 × RGB(255, 255, 128) - 노란 틴트
```

---

## 🔄 AI 스프라이트 통합 방법

DeeVid AI로 생성한 스프라이트를 프로젝트에 적용하는 절차:

### Step 1: 스프라이트 파일 배치

```
Sprites/AI_Generated/Pistoleer/
├── idle_NE_01.png
├── idle_E_01.png
├── idle_SE_01.png
├── walk_NE_01.png
├── walk_NE_02.png
├── walk_E_01.png
├── walk_E_02.png
├── walk_SE_01.png
├── walk_SE_02.png
├── attack_NE_01.png
├── attack_NE_02.png
├── attack_E_01.png
├── attack_E_02.png
├── attack_SE_01.png
├── attack_SE_02.png
├── death_NE_01.png
├── death_E_01.png
└── death_SE_01.png
```

**파일명 규칙:** `{동작}_{방향}_{프레임번호}.png`

### Step 2: Unity Import 설정
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 아트 해상도에 맞춰 조정 (128 또는 256)
- Filter Mode: Bilinear (카툰 스타일)
- Compression: None (프로토타입)

### Step 3: ScriptableObject에 연결
1. `PistoleerAnimData` ScriptableObject를 Inspector에서 열기
2. 각 방향/상태 배열에 스프라이트 드래그 앤 드롭
3. 코드 변경 불필요

### Step 4: 검증
- Play 모드 진입
- 유닛이 새 스프라이트로 표시되는지 확인
- 타일 클릭하여 이동 시 방향 전환 확인
- flipX 반전이 자연스러운지 확인

### 기존 DeeVid 생성물 매핑

| DeeVid 결과물 | 방향 매핑 |
|-------------|----------|
| 뒷모습 (첫 번째 생성) | NE (↗ 오른쪽 위) |
| 앞모습 (세 번째 생성) | SE (↘ 오른쪽 아래) → flipX하면 SW |

---

## 🚫 프로토타입 범위

### 포함

| 항목 | 내용 |
|------|------|
| 헥스 그리드 | 11×17 타일 생성 + 색상 + 선택 |
| 유닛 | 권총병 1종, idle/walk/attack/death 애니메이션 |
| 이동 | A* 경로탐색, 타일별 이동, 방향 전환 |
| 타일 점령 | 유닛 이동 시 타일 색상 변경 |
| 카메라 | 팬(드래그) + 줌(스크롤/핀치) |
| 입력 | 타일 클릭 선택, 유닛 이동 명령 |

### 제외

| 항목 | Phase |
|------|-------|
| 건물 시스템 (배럭, 자원, 타워 등) | MVP |
| 자원/생산 시스템 | MVP |
| 전투 (데미지/HP/사망) | MVP |
| 승리/패배 조건 | MVP |
| 네트워크/멀티플레이어 | Phase 2 |
| UI (디버그 외) | Phase 3 |
| 사운드/BGM | Phase 3 |
| 다중 유닛 타입 | MVP |
| 종족 시스템 | Phase 3 |

---

## ✅ 검증 계획

### 목표 1: AI 스프라이트 애니메이션

**검증 항목:**
- [ ] 스프라이트 프레임이 설정된 FPS로 정확히 순환
- [ ] 상태 전환 (idle → walk → idle)이 즉시 반영
- [ ] flipX 반전 시 피벗 포인트가 정확 (중심 기준)
- [ ] AI 생성 스프라이트가 헥스 타일 대비 적절한 크기
- [ ] 24개 조합 확인 (6방향 × 4상태)

**통과 기준:**
- 플레이스홀더 애니메이션이 시각적으로 정상 동작
- AI 스프라이트를 ScriptableObject에 넣으면 즉시 반영
- walk 2프레임이 "걷는 느낌"을 전달

### 목표 2: 헥사 타일 시스템

**검증 항목:**
- [ ] 11×17 그리드 정상 생성 (187개 타일, 빈틈/겹침 없음)
- [ ] 홀수 행이 반 칸 오프셋
- [ ] 타일 클릭 시 정확한 타일 선택 (모서리/경계 포함)
- [ ] 색상 변경 (Neutral → Blue → Red) 시각적 구분
- [ ] `HexCoord.Distance()` 정확도 (인접=1, 2칸=2)

**통과 기준:**
- 그리드가 시각적으로 정합한 육각형 맵으로 보임
- 어떤 타일을 클릭해도 정확한 타일이 선택됨
- 팀 색상이 명확히 구분됨

### 목표 3: 유닛 이동 + 방향 전환

**검증 항목:**
- [ ] A* 경로탐색이 유효한 경로 반환
- [ ] 유닛이 타일→타일 시각적으로 부드럽게 이동 (Lerp)
- [ ] 이동 방향에 따라 정확한 스프라이트로 전환
- [ ] flipX 좌우 반전 정확도 (SW/W/NW 방향)
- [ ] 이동 시 타일 점령 (색상 변경)
- [ ] 6방향 매핑 정확성:
  - NE 이동: NE 스프라이트, flipX=false
  - E 이동: E 스프라이트, flipX=false
  - SE 이동: SE 스프라이트, flipX=false
  - SW 이동: SE 스프라이트, flipX=true
  - W 이동: E 스프라이트, flipX=true
  - NW 이동: NE 스프라이트, flipX=true

**통과 기준:**
- 경로탐색이 인접 타일만 거치는 유효한 경로 반환
- 유닛 이동 시 방향 전환이 자연스러움
- 모든 6방향에서 스프라이트+flipX 조합이 정확

---

## 🎮 씬 구성

### SampleScene 오브젝트 계층

```
SampleScene
├── Main Camera
│   컴포넌트: Camera (Orthographic, Size 8), CameraController
│   Position: (0, 0, -10)
│   Background: #1a1a2e
│
├── [Managers]
│   ├── GameBootstrapper
│   │   컴포넌트: GameBootstrapper
│   │   참조: HexGrid, CameraController, InputHandler, UnitFactory, GameConfig
│   └── EventSystem (Input System용)
│
├── [World]
│   ├── HexGrid (빈 오브젝트, 타일 부모)
│   │   컴포넌트: HexGridRenderer
│   └── Units (빈 오브젝트, 유닛 부모)
│
├── [Input]
│   └── InputHandler
│       컴포넌트: InputHandler
│
└── [Debug]
    └── DebugUI
        컴포넌트: DebugUI
```

### 카메라 설정
- Projection: Orthographic
- Orthographic Size: 8 (기본 줌)
- 줌 범위: 3 ~ 12
- 정렬: TransparencySortMode.CustomAxis (0, 1, 0)

### 정렬 레이어
- Background (order 0): 헥스 타일
- Units (order 1): 유닛 스프라이트 (Y축 기준 자동 정렬)

---

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.1.0 | 2026-02-01 | 초기 문서 작성 |

---

**문서 끝**
