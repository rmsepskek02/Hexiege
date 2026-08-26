# 3D 전환 상세 메모

## 확정 사항 (2026-02-27)
- 전환 범위: UI 제외 모든 오브젝트(타일/건물/유닛) 전체 3D
- 카메라 방식: Orthographic + X축 틸트 (각도는 구현 후 테스트로 결정)
- 좌표 평면: XZ 평면(Y=0, Y=높이)으로 전환 — **Phase 1 완료**
- 헥스 타일 및 건물(Castle/Barracks/MiningPost): 3D 전환 완료. 헥스 타일은 ProBuilder + SG_HexTile, 건물 3종은 Meshy.ai Image-to-3D 기반 프리팹으로 연동됨
- 타이밍: 3D 전환 먼저, 이후 3종족/AI 기능 추가

## Phase 1 수정 파일 (XZ 좌표계 전환 — 완료)
- `Assets/_Project/Scripts/Core/HexMetrics.cs` — HexToWorld(): Vector3(x,0,z), WorldToHex(): Z기반 역산
- `Assets/_Project/Scripts/Core/ViewConverter.cs` — ToView(): X,Z 반전 (Y 통과)
- `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs` — XZ 레이캐스트 팬, Z 클램프
- `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` — ScreenToXZPlane() 레이캐스트
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` — SetupCamera Z축 bounds, SetCameraStartPositionForTeam Y높이 유지

## Phase 2 수정 파일 (렌더링 전환 — 완료, 2026-02-27)

### 핵심 변경
- SpriteRenderer → MeshRenderer/Renderer (3D 메시 기반)
- FrameAnimator(커스텀 스프라이트 애니메이션) → Animator(Mecanim) 기반
- sortingOrder 완전 제거 → 3D Z-buffer로 렌더 순서 자동 처리
- UnitAnimationData(Sprite[] ScriptableObject) 의존성 제거 (파일은 보존)
- ViewConverter.FlatTopSortingOrder() 메서드 제거

### 수정 파일 목록
- `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`
  - SpriteRenderer + sortingOrder 코드 블록 제거
  - Tooltip 주석 정리 (SpriteRenderer → Renderer)
- `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
  - SetDependencyReferences(): UnitAnimationData 파라미터 제거
  - `SetDependencyReferences(GameConfig, UnitMovementUseCase, UnitCombatUseCase)` (3개)
  - Tooltip/주석 정리
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
  - SetDependencies(): UnitAnimationData 파라미터 제거
  - `SetDependencies(GameConfig, UnitMovementUseCase, UnitCombatUseCase)` (3개)
  - Animator 기반 상태 전환 (IsWalking, IsDead, Attack trigger)
  - 방향: flipX → Y축 회전 (DirectionAngles 배열)
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - `_pistoleerAnimData` SerializeField 제거
  - SetDependencyReferences 호출에서 animData 인수 제거
- `Assets/_Project/Scripts/Core/ViewConverter.cs`
  - FlatTopSortingOrder() 메서드 제거
- `Assets/_Project/Scripts/Presentation/Building/BuildingView.cs` — 주석만 정리
- `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs` — 주석만 정리
- `Assets/_Project/Scripts/Presentation/Grid/HexTileView.cs` — 주석만 정리 (이미 Renderer 기반)
- `Assets/_Project/Scripts/Domain/Unit/FacingDirection.cs` — FacingInfo 주석 업데이트
- `Assets/_Project/Scripts/Infrastructure/Config/GameConfig.cs` — AnimationFps 주석 정리

### 삭제 파일
- `Assets/_Project/Scripts/Presentation/Unit/FrameAnimator.cs` — 완전 삭제 + .meta 삭제

### 보존 파일 (사용하지 않지만 에셋 참조 때문에 유지)
- `Assets/_Project/Scripts/Infrastructure/Config/UnitAnimationData.cs`
  - ScriptableObject 에셋(.asset)이 존재할 수 있으므로 삭제하면 missing script 에러
  - **[🔴 2026-08-25 정정 — 원문은 그대로 두고 덧붙인다: 이 파일은 현재 리포지토리에 없다.
    삭제 시점과 경위는 확인하지 못했다. `project-orchestrator/MEMORY.md` 는 `FrameAnimator.cs`·
    `UnitAnimationData.cs`·`PistoleerAnimData.asset` 이 삭제됐다고 적고 있어 두 기록이 엇갈린다.]**

## UnitView Animator 파라미터 규격
- IsWalking (bool): 이동 중 여부
- IsDead (bool): 사망 여부
- Attack (trigger): 공격 트리거
- 방향: transform.rotation Y축으로 표현 (Animator 파라미터 불필요)

## HexDirection → Y축 회전 각도 매핑
- NE(0)=30, E(1)=90, SE(2)=150, SW(3)=210, W(4)=270, NW(5)=330
- 3D 탑다운 뷰에서 유닛이 바라보는 방향을 Y축 회전으로 표현

## ViewConverter 3D 호환성 원칙
- XZ 평면에서 반전 공식: `viewPos.z = 2*mapCenter.z - domainPos.z`
- Y축(높이)은 반전하지 않음
- FlatTopSortingOrder() 완전 제거됨

## Phase 3 수정 파일 (카메라 틸트 + UnitView Animator 확인 — 완료, 2026-02-27)

### UnitView.cs — 확인만 (수정 없음)
- Phase 2에서 Animator 연동이 이미 완성되어 있음
- `_animator = GetComponentInChildren<Animator>()` — Initialize()에서 캐시
- `SetAnimatorBool(AnimIsWalking, true/false)` — 이동 시작/완료 시 적용
- `SetAnimatorBool(AnimIsDead, true)` — 사망 시 적용
- `SetAnimatorTrigger(AnimAttack)` — 공격 시 적용
- `ApplyDirection()` — HexDirection → Y축 회전
- MoveAlongPath() — XZ 기반 Lerp + UnitYOffset

### CameraController.cs — 틸트 각도 지원 추가
- `[SerializeField] float _tiltAngle = 55f` — Inspector에서 조정 가능
- `Start()` → `ApplyTilt()` — 카메라 X축 회전 적용
- `ApplyTilt()` — public, 외부에서도 호출 가능 (GameBootstrapper.SetupCamera)
- `TiltAngle` — public property, 외부에서 Z 오프셋 계산에 사용
- `ScreenToXZPlane()` — 기존 Plane.Raycast 기반, 틸트된 카메라에서도 정확히 작동
- 팬 시 Y 고정: `transform.position += new Vector3(diff.x, 0f, diff.z)` 패턴 유지

### GameBootstrapper.cs — 틸트 Z 오프셋 보정 추가
- `SetupCamera()`: 틸트 적용 + Z 오프셋 계산 (`cameraHeight / tan(tiltAngle)`)
- `SetCameraStartPositionForTeam()`: 틸트 Z 오프셋 동일하게 적용
- 공식: `zOffset = cameraHeight / Mathf.Tan(tiltAngle * Mathf.Deg2Rad)`, `startPos.z -= zOffset`

### 틸트 Z 오프셋 원리
- 카메라가 X축으로 틸트되면 화면 중앙이 카메라 직하가 아닌 앞쪽(+Z)을 향함
- 목표 지점이 화면 중앙에 오려면 카메라를 -Z 방향으로 오프셋해야 함
- `zOffset = height / tan(tiltAngle)` — 카메라 높이에서 XZ 평면까지의 수평 거리

## Phase 3 잔여 대상 (미착수)
- 3D 프리팹 제작 및 교체 (Meshy.ai)
- AnimatorController 제작 + 프리팹에 설정
- UnitAnimationData.cs 최종 삭제 (에셋 정리 후)
