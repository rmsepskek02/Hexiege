# Hexiege - 클라이언트 프로토타입 구현 계획서

**버전:** 1.2.0
**최종 수정일:** 2026-02-15
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
| 2 | 헥사 타일 시스템 | 헥스 그리드 생성(PointyTop 7×17 / FlatTop 10×29), 타일 색상 변경, 클릭 선택이 정확하게 작동하는가? |
| 3 | 유닛 이동 + 방향 전환 | 유닛이 헥스 타일 위에서 A* 경로를 따라 이동하며 방향별 스프라이트가 정확히 전환되는가? |

---

## 🔧 핵심 설계 결정

### 1. 커스텀 스프라이트 기반 헥스 그리드 (Unity Tilemap 사용 안 함)

**이유:**
- 3/4뷰(세미 아이소메트릭) 아트 스타일과 Unity Tilemap의 정육각형 제약이 충돌
- 타일당 개별 SpriteRenderer로 색상/선택/오버레이 처리가 용이
- PointyTop 7×17(119개) / FlatTop 10×29(290개) 타일로 성능 문제 없음
- 향후 타일별 파티클, 애니메이션 추가에 유연

### 2. 커스텀 FrameAnimator (Unity Animator 사용 안 함)

**이유:**
- 1~2프레임 사이클에 Animator 상태머신은 과잉 (.anim 파일, Controller, Transition 등)
- ScriptableObject(`UnitAnimationData`)에 스프라이트 배열 저장
- 드래그 앤 드롭으로 AI 생성 스프라이트 즉시 교체 가능
- ~50줄 코드로 전체 애니메이션 처리

### 3. 방향 시스템 (Orientation별 분리)

#### PointyTop: 3방향 + flipX = 6방향

```
제작 방향          flipX 반전 커버
──────────────────────────────────
NE (↗ 오른쪽 위)  → NW (↖ 왼쪽 위)
E  (→ 오른쪽)     → W  (← 왼쪽)
SE (↘ 오른쪽 아래) → SW (↙ 왼쪽 아래)
```

| 이동 방향 | 아트 방향 | flipX |
|----------|----------|-------|
| NE (q+1, r-1) | NE | false |
| E  (q+1, r+0) | E  | false |
| SE (q+0, r+1) | SE | false |
| SW (q-1, r+1) | SE | true  |
| W  (q-1, r+0) | E  | true  |
| NW (q+0, r-1) | NE | true  |

#### FlatTop: 4방향 + flipX = 6방향

```
제작 방향          flipX 반전 커버
──────────────────────────────────
N  (↑ 위)         → S  (↓ 아래)
NE (↗ 오른쪽 위)  → NW (↖ 왼쪽 위) [flipX]
SE (↘ 오른쪽 아래) → SW (↙ 왼쪽 아래) [flipX]
S  (↓ 아래)       (N의 flipX=false 별도)
```

---

## 🏛️ 아키텍처 구조

기술 설계서(TDD)의 Clean Architecture를 따름:

```
┌──────────────────────────────────────────────────────────┐
│  Presentation Layer                                       │
│  MonoBehaviour: 렌더링, Unity 이벤트 처리                  │
│  ├─ HexTileView          (타일 비주얼 + 클릭)             │
│  ├─ HexGridRenderer      (그리드 전체 렌더링)             │
│  ├─ UnitView             (유닛 이동 + per-step 체크 + ClaimedTile + 전투 + 사망 + OnMoveComplete) │
│  ├─ FrameAnimator        (스프라이트 프레임 순환)           │
│  ├─ BuildingView         (건물 비주얼 + 사망 처리) [MVP]   │
│  ├─ BuildingPlacementUI  (건물 선택 팝업 UI) [MVP]        │
│  ├─ ProductionPanelUI    (배럭 생산 패널 UI + 마커 연동) [MVP2] │
│  ├─ ProductionTicker     (생산 타이머 + 랠리 자동이동 + 마커 관리 + 공성 시스템) [MVP2] │
│  ├─ CameraController     (팬/줌)                         │
│  ├─ InputHandler         (입력 + 건물 배치 + 금광 클릭 + 생산UI + 자동이동) │
│  ├─ GameEndUI            (승리/패배 팝업 + 다시하기) [MVP3] │
│  └─ DebugUI              (디버그 정보)                    │
├──────────────────────────────────────────────────────────┤
│  Application Layer                                        │
│  UseCase + UniRx 이벤트                                   │
│  ├─ GameEvents               (이벤트 허브 + Entity+생산 이벤트) │
│  ├─ GridInteractionUseCase   (타일 선택)                  │
│  ├─ UnitMovementUseCase      (이동 + 타일 점령 + 유닛 우회 + ClaimedTile 차단 + per-step 체크) │
│  ├─ UnitSpawnUseCase         (유닛 생성 + 점유 검증 + 제거) │
│  ├─ UnitCombatUseCase        (전투: IDamageable 대상)     │
│  ├─ BuildingPlacementUseCase (건물 배치 + 영토 확장 + 제거) [MVP] │
│  ├─ ResourceUseCase          (팀별 골드 관리 + 기본/채굴소 수입) [MVP2] │
│  ├─ PopulationUseCase        (인구수 계산) [MVP2]         │
│  ├─ UnitProductionUseCase    (생산 큐/타이머/자동-수동) [MVP2] │
│  └─ GameEndUseCase           (Castle 파괴 → 승패 판정) [MVP3] │
├──────────────────────────────────────────────────────────┤
│  Domain Layer (순수 C#, Unity 독립)                       │
│  ├─ HexCoord             (큐브 좌표 값 객체)              │
│  ├─ HexDirection         (6방향 + 이웃 오프셋)            │
│  ├─ HexGrid              (그리드 데이터)                  │
│  ├─ HexTile              (타일 상태)                     │
│  ├─ HexPathfinder        (A* 경로탐색 + 차단 좌표)       │
│  ├─ FacingDirection      (방향 매핑)                     │
│  ├─ IDamageable          (전투 대상 인터페이스)           │
│  ├─ UnitData             (유닛 상태, IDamageable)        │
│  ├─ UnitStats            (유닛 타입별 기본 스탯)          │
│  ├─ UnitType             (유닛 타입)                     │
│  ├─ TeamId               (팀 열거형)                     │
│  ├─ BuildingType         (건물 타입 열거형) [MVP]         │
│  ├─ BuildingData         (건물 상태, IDamageable) [MVP]   │
│  └─ BuildingStats        (건물 타입별 기본 HP) [MVP]      │
├──────────────────────────────────────────────────────────┤
│  Infrastructure Layer                                     │
│  ├─ OrientationConfig    (Orientation별 그리드 설정 클래스) │
│  ├─ GameConfig           (ScriptableObject 전역 설정)     │
│  ├─ UnitAnimationData    (ScriptableObject 스프라이트)    │
│  ├─ UnitFactory          (유닛 프리팹 팩토리)             │
│  └─ BuildingFactory      (건물 프리팹 팩토리) [MVP]       │
├──────────────────────────────────────────────────────────┤
│  Core Layer (공유 유틸리티)                                │
│  ├─ HexMetrics           (헥스 ↔ 월드 좌표 변환)          │
│  └─ SingletonMonoBehaviour (싱글톤 베이스)                 │
├──────────────────────────────────────────────────────────┤
│  Bootstrap                                                │
│  └─ GameBootstrapper     (씬 진입점, LoadMap, Castle/금광/채굴소 자동 배치) │
└──────────────────────────────────────────────────────────┘
```

---

## 📁 파일 목록

모든 경로는 `Assets/_Project/` 기준.

### Domain Layer (순수 C#) - 16개

| 파일 경로 | 역할 | 단계 |
|----------|------|------|
| `Scripts/Domain/Common/TeamId.cs` | 팀 열거형 (Neutral, Blue, Red) | 프로토타입 |
| `Scripts/Domain/Common/IDamageable.cs` | 전투 대상 인터페이스 (Id, Team, Position, Hp, TakeDamage) | 프로토타입 |
| `Scripts/Domain/Hex/HexCoord.cs` | 큐브 좌표 값 객체 (q, r, s=-q-r) | 프로토타입 |
| `Scripts/Domain/Hex/HexDirection.cs` | 6방향 열거형 + 이웃 좌표 오프셋 | 프로토타입 |
| `Scripts/Domain/Hex/HexTile.cs` | 타일 상태 (소유자, 이동가능 여부, HasGoldMine) | 프로토타입 + **수정** |
| `Scripts/Domain/Hex/HexOrientation.cs` | HexOrientation 열거형 + HexOrientationContext 정적 홀더 | 프로토타입 |
| `Scripts/Domain/Hex/HexGrid.cs` | 그리드 데이터 구조 (Dictionary, orientation 지원) | 프로토타입 |
| `Scripts/Domain/Hex/HexPathfinder.cs` | 헥스 그리드 A* 경로탐색 (blockedCoords 지원) | 프로토타입 |
| `Scripts/Domain/Unit/FacingDirection.cs` | 6방향 → 3아트방향 + flipX 매핑 | 프로토타입 |
| `Scripts/Domain/Unit/UnitType.cs` | 유닛 타입 열거형 | 프로토타입 |
| `Scripts/Domain/Unit/UnitData.cs` | 유닛 상태 (IDamageable 구현, 위치/타입/팀/방향/HP/공격력/사거리/ClaimedTile) | 프로토타입 + **수정** |
| `Scripts/Domain/Unit/UnitStats.cs` | 유닛 타입별 기본 스탯 (MaxHp, AttackPower, AttackRange) | 프로토타입 |
| `Scripts/Domain/Building/BuildingType.cs` | 건물 타입 열거형 (Castle, Barracks, MiningPost) | **MVP** |
| `Scripts/Domain/Building/BuildingData.cs` | 건물 상태 (IDamageable 구현, Id/Type/Team/Position/HP) | **MVP** |
| `Scripts/Domain/Building/BuildingStats.cs` | 건물 타입별 기본 HP (Castle:50, Barracks:30, MiningPost:20) | **MVP** |
| `Scripts/Domain/Building/ProductionState.cs` | 배럭별 생산 상태 (큐, 타이머, 자동/수동, 랠리포인트) | **MVP2** |
| `Scripts/Domain/Unit/UnitProductionStats.cs` | 유닛 타입별 생산 시간/비용/인구 | **MVP2** |

### Core Layer - 2개 (+1 enum)

| 파일 경로 | 역할 |
|----------|------|
| `Scripts/Core/HexMetrics.cs` | 헥스 좌표 ↔ 월드 좌표 변환, 사이징 상수 |
| `Scripts/Core/SingletonMonoBehaviour.cs` | 제네릭 싱글톤 베이스 클래스 |

### Application Layer - 10개

| 파일 경로 | 역할 | 단계 |
|----------|------|------|
| `Scripts/Application/Events/GameEvents.cs` | UniRx Subject 이벤트 허브 (Entity 전투 + GameEnd 이벤트 포함) | 프로토타입 + **수정** |
| `Scripts/Application/UseCases/GridInteractionUseCase.cs` | 타일 선택 처리 | 프로토타입 |
| `Scripts/Application/UseCases/UnitMovementUseCase.cs` | 경로탐색(유닛 Position 우회 + 같은 팀 ClaimedTile 차단) + per-step 가용성 체크(IsTileBlockedBySameTeam) + 이동 + 타일 점령 | 프로토타입 + **수정** |
| `Scripts/Application/UseCases/UnitSpawnUseCase.cs` | 유닛 생성(UnitStats 사용, 점유 검증) + 조회 + 제거 | 프로토타입 + **수정** |
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | IDamageable 기반 전투 (유닛+건물 공격, 사망 데이터 정리) | 프로토타입 + **수정** |
| `Scripts/Application/UseCases/BuildingPlacementUseCase.cs` | 건물 배치 + 영토 확장 + MiningPost 금광 전용(인접 팀 조건) + PlaceMiningPostDirect + 제거(금광 이동불가 유지) | **MVP** + **수정** |
| `Scripts/Application/UseCases/ResourceUseCase.cs` | 팀별 골드 관리 (시작 500, 차감/추가/기본+채굴소 수입) | **MVP2** + **수정** |
| `Scripts/Application/UseCases/PopulationUseCase.cs` | 인구수 계산 (최대=타일, 사용=건물+유닛) | **MVP2** |
| `Scripts/Application/UseCases/UnitProductionUseCase.cs` | 배럭 생산 핵심 로직 (큐/타이머/자동-수동/랠리포인트) | **MVP2** |
| `Scripts/Application/UseCases/GameEndUseCase.cs` | Castle 파괴 감지 → 승패 판정 → OnGameEnd 이벤트 | **MVP3** |

### Infrastructure Layer - 4개

| 파일 경로 | 역할 | 단계 |
|----------|------|------|
| `Scripts/Infrastructure/Config/GameConfig.cs` | 전역 설정 ScriptableObject (OrientationConfig + BuildingYOffset + Economy) | 프로토타입 + **MVP 수정** + **MVP2 수정** |
| `Scripts/Infrastructure/Config/UnitAnimationData.cs` | 방향별 스프라이트 배열 ScriptableObject | 프로토타입 |
| `Scripts/Infrastructure/Factories/UnitFactory.cs` | 유닛 프리팹 인스턴스 생성 + 런타임 의존성 주입 + 전체 제거 | 프로토타입 + **수정** |
| `Scripts/Infrastructure/Factories/BuildingFactory.cs` | 건물 프리팹 인스턴스 생성 + 전체 제거 (맵 전환용) | **MVP** |

### Presentation Layer - 12개

| 파일 경로 | 역할 | 단계 |
|----------|------|------|
| `Scripts/Presentation/Grid/HexTileView.cs` | 타일 비주얼 + 색상 변경 + 선택 | 프로토타입 |
| `Scripts/Presentation/Grid/HexGridRenderer.cs` | HexGrid → GameObject 렌더링 + 금광 오버레이 렌더링 | 프로토타입 + **수정** |
| `Scripts/Presentation/Unit/FrameAnimator.cs` | 스프라이트 프레임 순환 엔진 | 프로토타입 |
| `Scripts/Presentation/Unit/UnitView.cs` | 유닛 이동 코루틴 + per-step 가용성 체크/재탐색 + ClaimedTile 선점/해제 + Lerp 중 전투 + 사망 처리 + OnMoveComplete 콜백 | 프로토타입 + **수정** |
| `Scripts/Presentation/Camera/CameraController.cs` | 카메라 팬/줌 + 경계 제한 | 프로토타입 |
| `Scripts/Presentation/Input/InputHandler.cs` | 입력 처리 + 건물 배치 + 금광 클릭(채굴소 팝업) + T키 자동/수동 이동 토글 | 프로토타입 + **수정** |
| `Scripts/Presentation/Debug/DebugUI.cs` | 화면 디버그 정보 표시 | 프로토타입 |
| `Scripts/Presentation/Building/BuildingView.cs` | 건물 비주얼 + OnEntityDied 구독으로 파괴 처리 | **MVP** + **수정** |
| `Scripts/Presentation/UI/BuildingPlacementUI.cs` | 건물 선택 팝업 UI (배럭/채굴소 조건부 활성, 골드 검증) | **MVP** + **수정** |
| `Scripts/Presentation/UI/GameEndUI.cs` | 승리/패배 팝업 + 다시하기 버튼 (Time.timeScale 제어) | **MVP3** |
| `Scripts/Presentation/UI/ProductionPanelUI.cs` | 배럭 생산 패널 UI (수동 탭/자동 롱프레스, 큐/프로그레스, 마커 표시/숨김 연동) | **MVP2** |
| `Scripts/Presentation/Production/ProductionTicker.cs` | 생산 타이머 브릿지 + 랠리포인트 자동 이동(BFS) + 마커 관리(생성/이동/숨김/파괴) + 공성 시스템(Castle 방향 자동 진군 + 1초 간격 전진) | **MVP2** |

### Bootstrap - 1개

| 파일 경로 | 역할 | 단계 |
|----------|------|------|
| `Scripts/Bootstrap/GameBootstrapper.cs` | 씬 진입점, LoadMap(), 의존성 와이어링, Castle/금광/채굴소 자동 배치, GameEndUseCase 생성 | 프로토타입 + **수정** |

### 에셋 파일

스프라이트는 Gemini AI로 생성 완료됨. 상세 목록은 `AssetProductionGuide.md` 참고.

| 경로 | 용도 |
|------|------|
| `Sprites/Tiles/tile_hex.png` | PointyTop 헥스 타일 스프라이트 (3/4뷰) |
| `Sprites/Tiles/tile_hex_flat.png` | FlatTop 헥스 타일 스프라이트 |
| `Sprites/Units/Pistoleer/` | 권총병 스프라이트 (Idle/Walk/Attack, 3방향) |
| `Sprites/Buildings/` | 건물 + 맵 오브젝트 스프라이트 |
| `Sprites/UI/` | UI 스프라이트 (Buttons/Panels/Bars/Icons/Slots) |
| `Prefabs/HexTile_PointyTop.prefab` | PointyTop 타일 프리팹 (SpriteRenderer + Collider + HexTileView) |
| `Prefabs/HexTile_FlatTop.prefab` | FlatTop 타일 프리팹 (SpriteRenderer + Collider + HexTileView) |
| `Prefabs/Unit_Pistoleer.prefab` | 유닛 프리팹 (SpriteRenderer + UnitView + FrameAnimator) |
| `Resources/Config/GameConfig.asset` | 전역 설정 인스턴스 |
| `Resources/Config/PistoleerAnimData.asset` | 권총병 애니메이션 데이터 인스턴스 |
| `Prefabs/Building_Castle.prefab` | 본기지 프리팹 (SpriteRenderer + BuildingView) | **MVP** |
| `Prefabs/Building_Barracks.prefab` | 배럭 프리팹 (SpriteRenderer + BuildingView) | **MVP** |
| `Prefabs/Building_MiningPost.prefab` | 채굴소 프리팹 (SpriteRenderer + BuildingView) | **MVP** |

**총 파일 수:** 스크립트 47개 (프로토타입 30 + MVP 8 + MVP2 7 + MVP3 2) + 프리팹/SO 8개 + 스프라이트 32개

---

## 📐 구현 순서

### Phase 1: 프로젝트 정리 ✅ 완료
- [x] 스크립트 폴더 구조 생성 (Domain, Application, Infrastructure, Presentation, Bootstrap)
- [x] 스프라이트 폴더 구조 생성 및 에셋 정리 (AssetProductionGuide.md 참고)
- [x] 에셋 명명 규칙 확정 및 전체 파일 리네임 완료
- [ ] `NewMonoBehaviourScript.cs` 삭제 (Phase 2 시작 시 처리)

### Phase 2: Domain 레이어 ✅ 완료
1. `TeamId.cs` - 팀 열거형
2. `HexCoord.cs` - 큐브 좌표 (모든 것의 기반)
3. `HexDirection.cs` - 6방향 + 이웃 오프셋
4. `HexTile.cs` - 타일 상태
5. `HexGrid.cs` - 그리드 생성 (orientation별 even-r/even-q offset → cube 변환)
6. `HexPathfinder.cs` - A* 경로탐색
7. `FacingDirection.cs` - 방향 매핑
8. `UnitType.cs` - 유닛 타입
9. `UnitData.cs` - 유닛 상태

### Phase 3: Core ✅ 완료
1. `HexMetrics.cs` - 좌표 변환
2. `SingletonMonoBehaviour.cs` - 싱글톤

### Phase 4: Application ✅ 완료
1. `GameEvents.cs` - 이벤트 허브
2. `GridInteractionUseCase.cs` - 타일 선택
3. `UnitMovementUseCase.cs` - 이동 + 점령
4. `UnitSpawnUseCase.cs` - 유닛 생성
5. `UnitCombatUseCase.cs` - 전투 (공격/피격/사망)

### Phase 5: Infrastructure ✅ 완료
1. `GameConfig.cs` - 설정 SO (OrientationConfig 중첩 클래스 포함)
2. `UnitAnimationData.cs` - 애니메이션 SO
3. `UnitFactory.cs` - 팩토리 (DestroyAllUnits 포함)

### Phase 6: Presentation - Grid ✅ 완료
1. `HexTileView.cs` - 타일 뷰 (선택 하이라이트 버그 수정 완료)
2. `HexGridRenderer.cs` - 그리드 렌더러 (듀얼 프리팹 지원)

### Phase 7: Presentation - Unit ✅ 완료
1. `FrameAnimator.cs` - 프레임 애니메이터
2. `UnitView.cs` - 유닛 뷰 (이동 + 자동 공격 + 사망 처리)

### Phase 8: Presentation - Camera/Input ✅ 완료
1. `CameraController.cs` - 카메라 제어
2. `InputHandler.cs` - 입력 처리

### Phase 9: Bootstrap + Debug ✅ 완료
1. `GameBootstrapper.cs` - 진입점 (LoadMap 런타임 전환)
2. `DebugUI.cs` - 디버그

### Phase 10: 프리팹 + ScriptableObject 생성 ✅ 완료
- Gemini 스프라이트가 이미 제작 완료되어 플레이스홀더 불필요
- `Prefabs/HexTile_PointyTop.prefab` 생성 (SpriteRenderer + PolygonCollider2D + HexTileView)
- `Prefabs/HexTile_FlatTop.prefab` 생성 (SpriteRenderer + PolygonCollider2D + HexTileView)
- `Prefabs/Unit_Pistoleer.prefab` 생성 (SpriteRenderer + UnitView + FrameAnimator)
- `Resources/Config/GameConfig.asset` 생성 (전역 설정)
- `Resources/Config/PistoleerAnimData.asset` 생성 (실제 스프라이트 연결)

### Phase 11: 통합 테스트 ✅ 완료
- 4가지 목표 검증 완료 (아래 검증 계획 참고)

---

## 🎨 에셋 전략

### 스프라이트 현황

Google Gemini로 프로토타입용 스프라이트 전체 제작 완료. 플레이스홀더 불필요.
상세 목록 및 명명 규칙은 `AssetProductionGuide.md` 참고.

**헥스 타일:**
- `Sprites/Tiles/tile_hex.png` — PointyTop 3/4뷰 육각형
- `Sprites/Tiles/tile_hex_flat.png` — FlatTop 3/4뷰 육각형
- `SpriteRenderer.color`로 팀 색상 적용
- PPU(Pixels Per Unit): 1024

**유닛 스프라이트:** `Sprites/Units/Pistoleer/`
- 3방향 (NE, E, SE) × 3상태 (Idle, Walk, Attack)
- Idle: 방향당 1프레임, Walk E: 2프레임, 나머지 Walk: 1프레임, Attack: 방향당 2프레임
- NW/W/SW는 flipX 반전으로 처리
- 파일명: `pistoleer_{동작}_{방향}_{프레임번호}.png`

### 팀 색상

```
Neutral: RGB(178, 178, 178) - 회색
Blue:    RGB(77, 128, 230)  - 파랑
Red:     RGB(230, 77, 77)   - 빨강
Selected: 기존 색상 × RGB(255, 255, 128) - 노란 틴트
```

---

## 🔄 스프라이트 통합 방법

Gemini AI로 생성한 스프라이트는 이미 프로젝트에 배치 완료. 코드 작성 후 ScriptableObject에 연결만 하면 됨.

### 현재 스프라이트 구조

```
Sprites/Units/Pistoleer/
├── Idle/
│   ├── pistoleer_idle_ne_01.png
│   ├── pistoleer_idle_e_01.png
│   └── pistoleer_idle_se_01.png
├── Walk/
│   ├── pistoleer_walk_ne_01.png
│   ├── pistoleer_walk_e_01.png
│   ├── pistoleer_walk_e_02.png
│   └── pistoleer_walk_se_01.png
├── Attack/
│   ├── pistoleer_attack_ne_01.png
│   ├── pistoleer_attack_ne_02.png
│   ├── pistoleer_attack_e_01.png
│   ├── pistoleer_attack_e_02.png
│   ├── pistoleer_attack_se_01.png
│   └── pistoleer_attack_se_02.png
└── pistoleer_portrait.png
```

**파일명 규칙:** `pistoleer_{동작}_{방향}_{프레임번호}.png` (snake_case, 소문자)

### Unity Import 설정
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 스프라이트 해상도에 맞춰 조정
- Filter Mode: Bilinear (카툰 스타일)
- Compression: None (프로토타입)

### ScriptableObject 연결 (Phase 10에서 수행)
1. `PistoleerAnimData` ScriptableObject를 Inspector에서 열기
2. 각 방향/상태 배열에 위 스프라이트 드래그 앤 드롭
3. 코드 변경 불필요

---

## 🚫 프로토타입 범위

### 포함

| 항목 | 내용 |
|------|------|
| 헥스 그리드 | PointyTop 7×17 / FlatTop 10×29 타일 생성 + 색상 + 선택 |
| 유닛 | 권총병 1종, idle/walk/attack 애니메이션 (death는 프로토타입 범위 외) |
| 이동 | A* 경로탐색, 타일별 이동, 방향 전환 |
| 타일 점령 | 유닛 이동 시 타일 색상 변경 |
| 전투 | 이동 중 매 타일 인접 적(유닛/건물) 자동 공격 (IDamageable), 전투 후 이동 계속, 사망 시 데이터 정리 + GameObject 파괴 |
| 카메라 | 팬(드래그) + 줌(스크롤/핀치) |
| 입력 | 타일 클릭 선택, 유닛 이동 명령, T키 자동/수동 이동 토글 (양팀 Castle 방향 자동 이동) |

### 제외

| 항목 | Phase |
|------|-------|
| ~~건물 시스템 (배럭, 자원, 타워 등)~~ | ~~MVP~~ → **건물 배치 구현 완료 (코드)** |
| ~~자원/생산 시스템~~ | ~~MVP~~ → **생산 시스템 구현 완료 (코드)** |
| ~~승리/패배 조건~~ | ~~MVP~~ → **Castle 파괴 승패 구현 완료 (코드)** |
| 네트워크/멀티플레이어 | Phase 2 |
| UI (디버그 외) | Phase 3 |
| 사운드/BGM | Phase 3 |
| 다중 유닛 타입 | MVP |
| 종족 시스템 | Phase 3 |

---

## ✅ 검증 계획

### 목표 1: AI 스프라이트 애니메이션 ✅ 통과

**검증 항목:**
- [x] 스프라이트 프레임이 설정된 FPS로 정확히 순환
- [x] 상태 전환 (idle → walk → idle)이 즉시 반영
- [x] flipX 반전 시 피벗 포인트가 정확 (중심 기준)
- [x] AI 생성 스프라이트가 헥스 타일 대비 적절한 크기
- [x] 18개 조합 확인 (6방향 × 3상태: idle/walk/attack)

**결과:** 에셋 부족 (FlatTop 4방향 스프라이트 미제작)을 제외하면 정상 동작 확인됨.

### 목표 2: 헥사 타일 시스템 ✅ 통과

**검증 항목:**
- [x] PointyTop 7×17 / FlatTop 10×29 그리드 정상 생성 (빈틈/겹침 없음)
- [x] PointyTop: 홀수 행이 반 칸 오프셋 / FlatTop: 홀수 열이 반 칸 오프셋
- [x] 타일 클릭 시 정확한 타일 선택 (모서리/경계 포함)
- [x] 색상 변경 (Neutral → Blue → Red) 시각적 구분
- [x] `HexCoord.Distance()` 정확도 (인접=1, 2칸=2)

**결과:** 타일 선택 하이라이트 잔존 버그 발견 → 수정 완료 (HexTileView.cs 선택 토글 로직 수정).

### 목표 3: 유닛 이동 + 방향 전환 ✅ 통과

**검증 항목:**
- [x] A* 경로탐색이 유효한 경로 반환
- [x] 유닛이 타일→타일 시각적으로 부드럽게 이동 (Lerp)
- [x] 이동 방향에 따라 정확한 스프라이트로 전환
- [x] flipX 좌우 반전 정확도 (SW/W/NW 방향)
- [x] 이동 시 타일 점령 (색상 변경)
- [x] 6방향 매핑 정확성:
  - NE 이동: NE 스프라이트, flipX=false
  - E 이동: E 스프라이트, flipX=false
  - SE 이동: SE 스프라이트, flipX=false
  - SW 이동: SE 스프라이트, flipX=true
  - W 이동: E 스프라이트, flipX=true
  - NW 이동: NE 스프라이트, flipX=true

**결과:** 모든 방향에서 정상 동작 확인.

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
│   │   참조: HexGrid, CameraController, InputHandler, UnitFactory,
│   │          BuildingFactory, BuildingPlacementUI, GameConfig
│   ├── UnitFactory
│   ├── BuildingFactory [MVP]
│   ├── ProductionTicker [MVP2]
│   └── EventSystem (Input System용)
│
├── [World]
│   ├── HexGrid (빈 오브젝트, 타일 부모)
│   │   컴포넌트: HexGridRenderer
│   ├── Units (빈 오브젝트, 유닛 부모)
│   └── Buildings (빈 오브젝트, 건물 부모) [MVP]
│
├── [Input]
│   └── InputHandler
│       컴포넌트: InputHandler
│
├── [UI] (Canvas, Screen Space - Overlay) [MVP]
│   ├── BuildingPanel (비활성 상태)
│   │   컴포넌트: BuildingPlacementUI
│   │   하위: BarracksButton, MiningPostButton, CancelButton
│   ├── ProductionPopup (비활성 상태) [MVP2]
│   │   컴포넌트: ProductionPanelUI
│   │   하위: Background, ProductionPanel (UnitButtons, QueueSlots, ProgressBar, InfoBar, RallyPointButton)
│   └── GameEndPanel (비활성 상태) [MVP3]
│       컴포넌트: GameEndUI
│       하위: Background, ResultText(TMP), RestartButton
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

### 목표 4: 전투 시스템 (인접 자동 공격) ✅ 통과

**검증 항목:**
- [x] 이동 완료 후 인접 6타일에서 적 유닛 탐색 정상 동작
- [x] 적 발견 시 공격 방향 스프라이트 전환 (flipX 포함)
- [x] Attack 애니메이션 재생 (2프레임 사이클)
- [x] 데미지 적용 정확도 (AttackPower=3, HP 감소 확인)
- [x] 사거리 내 적이 있는 동안 반복 공격
- [x] 적 HP ≤ 0 시 사망 이벤트 발행 + GameObject 파괴
- [x] 사망한 유닛이 UnitSpawnUseCase 목록에서 제거됨

**결과:** 인접 적 공격, 애니메이션, 사망 시 삭제 모두 정상 동작 확인.

---

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 1.2.0 | 2026-02-15 | 금광+자원 시스템: HexTile.HasGoldMine(금광 타일, 이동 불가), BuildingPlacementUseCase MiningPost 금광 전용(인접 팀 조건, PlaceMiningPostDirect 초기용, 파괴 시 금광 이동불가 유지), GameBootstrapper PlaceGoldMines(시작 채굴소 자동 건설, 중립 금광 2개), HexGridRenderer 금광 오버레이, InputHandler 금광 클릭→팝업, ResourceUseCase 기본 수입(0)/채굴소 수입, GameConfig BaseGoldPerSecond. 승리/패배: GameEndUseCase(Castle 파괴 감지→OnGameEnd), GameEndUI(팝업+일시정지+다시하기), GameEvents.OnGameEnd 이벤트 추가 |
| 1.1.0 | 2026-02-15 | 공성 시스템: ProductionTicker 공성 흐름(랠리→Castle→siege 전진, TickSiege 1초 간격), UnitView.OnMoveComplete 콜백 추가, 공성 목록 관리(등록/사망 제거/Castle 인접 제거), ProductionTicker/UnitView 파일 역할 업데이트, PopupClosedFrame(BuildingPlacementUI/ProductionPanelUI) |
| 1.0.0 | 2026-02-15 | 랠리포인트 시스템 개선: RallyPointChangedEvent 이벤트, ProductionTicker 마커 관리(생성/이동/숨김/파괴, 3초 자동 숨김), ProductionPanelUI 마커 연동(Show→표시, Close→숨김), BFS 빈 타일 탐색(FindPathToNearestEmptyTile, maxRange=3), SetRallyPoint 배럭 타일→해제, GameConfig.RallyPointPrefab 추가, 팝업 설정 후 자동 닫힘 |
| 0.9.1 | 2026-02-14 | Per-step 타일 가용성 체크 추가: UnitMovementUseCase.IsTileBlockedBySameTeam() 메서드, MoveAlongPath 각 스텝 전 같은 팀 차단 검증 + 차단 시 재탐색, 아키텍처 다이어그램/파일 역할 업데이트 |
| 0.9.0 | 2026-02-14 | 유닛 이동/전투 시스템 개선: UnitData.ClaimedTile(이동 중 선점, 같은 팀만 차단), UnitMovementUseCase 차단 목록에 같은 팀 ClaimedTile 추가, UnitView.MoveAlongPath Lerp 중 거리 기반 전투 체크로 변경, 타일 중앙 도착=전투 승리=점령 규칙 |
| 0.8.0 | 2026-02-14 | 생산 시스템 구현 반영: Domain 2개(ProductionState, UnitProductionStats), Application 3개(ResourceUseCase, PopulationUseCase, UnitProductionUseCase), Presentation 2개(ProductionPanelUI, ProductionTicker) 추가. 파일 수 38→45. 영토 확장(건물 인접 점령), 경로탐색 전체 유닛 차단, 유닛 스폰 점유 검증, UnitFactory 런타임 의존성 주입, GameConfig 경제 설정, 생산 이벤트 4종, 씬에 ProductionTicker/ProductionPopup 추가 |
| 0.7.0 | 2026-02-13 | 전투 시스템 고도화 반영: IDamageable/UnitStats/BuildingStats 3개 파일 추가(Domain 14→), Entity 기반 이벤트(Attacked/Died), 경로탐색 적 우회, 이동 중 전투, 사망 데이터 정리, T키 자동이동 토글, 파일 수 35→38, 아키텍처 다이어그램 업데이트 |
| 0.6.0 | 2026-02-08 | MVP 건물 배치 시스템 코드 완료 반영: 파일 목록에 건물 7개 파일 추가(Domain 2, Application 1, Infrastructure 1, Presentation 2, Bootstrap 수정), 아키텍처 다이어그램 업데이트, 씬 구성에 Buildings/BuildingFactory/[UI] 추가 |
| 0.5.0 | 2026-02-08 | 프로토타입 완료: Phase 2-11 전체 완료 표시, 검증 4가지 목표 모두 통과, 타일 선택 하이라이트 버그 수정 반영 |
| 0.4.0 | 2026-02-08 | 듀얼 Orientation 지원: OrientationConfig 중첩 클래스, PointyTop(7×17)/FlatTop(10×29) 그리드, 프리팹 분리(HexTile_PointyTop/HexTile_FlatTop), GameBootstrapper.LoadMap() 런타임 맵 전환, UnitFactory.DestroyAllUnits() |
| 0.3.0 | 2026-02-07 | 전투 시스템 추가 반영: UnitCombatUseCase 신규, UnitData 전투 스탯(HP/공격력/사거리), 전투 이벤트(Attack/Died), 이동 후 인접 적 자동 공격, 프로토타입 범위에 전투 포함, 그리드 크기 7×30 현행화 |
| 0.2.0 | 2026-02-02 | Gemini 스프라이트 완료 반영, 에셋 경로/명명 규칙 현행화, 플레이스홀더 전략 제거, death 애니메이션 프로토타입 범위 외 처리 |
| 0.1.0 | 2026-02-01 | 초기 문서 작성 |

---

**문서 끝**
