# 스킬 지점 조준 좌표화 + 조준원 오버레이 렌더링 (2026-08-04)

task: `_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`. 코드-only(규칙문서는 document-manager 병렬).

## 무엇을 바꿨나
조준 중심을 **타일 스냅(HexCoord) → 연속 도메인 월드 Vector3**로 바꾸고, 조준원이 3D 지형에
파묻히지 않게 렌더링 기법을 교체.

## 데이터 흐름 (전 계층 HexCoord → Vector3)
1. `Presentation/Input/SkillAimController.cs`: `UpdateAimPoint`에서 `WorldToHex` 스냅·`HexToWorld`
   되돌림 **주석 비활성화**. 연속 `domainWorld`를 `_clampToBounds`로 맵 경계 clamp → `_lastValidDomain`(Vector3).
   표시=`ViewConverter.ToView(clampedDomain)`(맵 안에선 손가락 뷰와 정확히 일치, 밖에선 경계로 clamp).
   `Initialize(cam, camCtrl, Func<Vector3,Vector3> clampToBounds, Func<Vector3,bool> isWithinBounds)`.
   `BeginAim(..., Vector3 fallbackDomain, Action<int,int,Vector3> onConfirm, ...)`.
2. `Presentation/UI/BuildingSkillPanelUI.cs`: `OnAimConfirm(int,int,Vector3)`·`ActivateSkill(...,Vector3,bool)`.
   BeginAim fallback = `HexMetrics.HexToWorld(_currentBuilding.Position)`(using Hexiege.Core 추가).
3. `Application/UseCases/SkillActivationUseCase.cs`: `Activate(int,int,Vector3? aimWorld)`. 생성자
   `HexGrid grid` 파라미터 제거 → `Func<Vector3,bool> isWithinMapBounds` 주입(HasTile 재검증 → 맵 경계 안 점).
   브릿지 `ApplyInstantAreaDamageBridge/ApplyAreaDotBridge(TeamId, Vector3 center, ...)`. `using UnityEngine` 추가.
4. `Application/Skill/SkillActivationContext.cs`: `AimCoord`(HexCoord)→`AimWorld`(Vector3), 델리게이트
   `Action<TeamId,Vector3,...>`. 두 Executor는 `ctx.AimWorld` 사용.
5. `Application/UseCases/UnitCombatUseCase.cs`: `ApplySkill{InstantAreaDamage,AreaDot}(TeamId, Vector3 center, ...)`.
   `Flatten(_mapper.HexToWorld(center))` **주석 비활성화** → `Flatten(center)`(center 이미 도메인 월드).
   `CollectEnemy{Units,Buildings}InRadiusDomain`은 무변경(이미 Vector3+유클리드).
6. `Infrastructure/Network/NetworkSkillController.cs` + `Application/Interfaces/INetworkSkillController.cs`:
   `RequestActivateSkill(...,Vector3 aimWorld,bool)`. ServerRpc `(...,Vector3 aimWorld,bool,ServerRpcParams)`
   — **NGO 2.9.2 Vector3 기본 직렬화**(int q,r 분해 폐지). `Vector3? aim = hasAim ? aimWorld : null`.
7. `Bootstrap/GameBootstrapper.Setup.cs`: SkillActivationUseCase 생성자 2번째 인자에 `aimWorld =>
   HexMetrics.IsWithinMapBounds(aimWorld,_grid.Width,_grid.Height)` 클로저. SkillAimController.Initialize에
   clamp/within 두 클로저. **람다가 현재 `_grid` 읽음 → 맵 재로드에도 최신 크기**.

## 맵 월드 경계 헬퍼 (신규 — Core/HexMetrics.cs)
- `ComputeMapWorldBounds(w,h,out min,out max)`: 테두리 셀 HexToWorld 극값 + 반칸(0.5×TileWidth/Height) = AABB.
- `IsWithinMapBounds(pt,w,h)` / `ClampToMapBounds(pt,w,h)`. 도메인 좌표(Blue) 기준, 뷰 반전 무관.
- HexGrid(Domain)는 Vector3 불가 → Core에 경계 수학, 클로저로 Application 주입(기존 `_isValidTile` 패턴 계승).

## 조준원 렌더링 (지면 데칼)
- 원인: HexTile ProBuilder 실린더(y±0.05)와 조준원(y=0.05) **coplanar → z-fighting**.
- 신규 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader`(`Hexiege/SkillAimOverlay`):
  Transparent + ZWrite Off + **ZTest LEqual + Offset -1,-1** + Cull Off. coplanar 지형 z-fight는 이기고,
  유닛/건물(불투명 MeshRenderer/SkinnedMeshRenderer, 깊이 기록) 뒤엔 정상 가려짐. **ZTest Always 금지**.
- 머티리얼 `Assets/_Project/Materials/SkillAimOverlay.mat`: `SkillSetup_Scene.cs.EnsureOverlayMaterial()`가
  `Shader.Find`로 생성·CreateAsset. `EnsureReticlePart`가 3겹 SpriteRenderer.sharedMaterial 배선 +
  `SkillAimReticle._overlayMaterial`(신규 SerializeField) 배선. `ApplyRenderer`(static→instance)가 런타임 자가배선.
- **씬 재셋업 필요**: `Hexiege/Skill/2. Setup Scene` 재실행해야 머티리얼 생성·배선(좌표 변경은 코드-only 재셋업 불필요).
  잔여 z-fight 시 Inspector `SkillAimReticle._yOffset` 미세 상향으로 튜닝.

## 취소 버그 근본 수정 (실기 Android, 커밋 2e88dfa 1차 → 4e5da5e 근본)
- 증상: 취소 X 위에서 손을 떼도 스킬이 발동되고 쿨다운이 걸림.
- 원인: 손 뗀(release) 프레임에 `SkillAimController`가 포인터 좌표를 라이브로 읽는데, `TryGetPointerScreenPos`의 **마우스 분기가 터치 종료 후 합성 마우스 좌표(0,0)를 "유효"로 반환**해 캐시된 마지막 드래그 좌표 폴백을 가로챔 → (0,0)은 화면 좌하단이라 취소 X를 벗어난 것으로 판정돼 발동.
- 수정: **release 프레임엔 라이브 좌표를 읽지 않고 캐시된 마지막 드래그 좌표(`_lastDragScreenPos`)로만 취소/발동 판정.**
- 교훈: 터치 종료 프레임의 마우스 좌표 폴링은 (0,0) 합성값이 유효처럼 샐 수 있음 → release 판정은 라이브 폴링이 아니라 마지막 유효 드래그 좌표 캐시 기준.

## 쿨다운 안내 토스트 (커밋 4e5da5e)
- 쿨다운 중 스킬 탭을 조용히 무시하던 것을 안내로. 기존 ToastUI(에셋 기반) 재사용: `Application/Events/ToastKey.cs`에 `SkillOnCooldown` 추가 + `Resources/Config/ToastMessageConfig.asset` key:4 "스킬이 쿨다운 중입니다". `BuildingSkillPanelUI`에서 쿨다운 상태면 `ToastUI.Show(ToastKey.SkillOnCooldown)`.

## 주의
- 제거 대상 3곳은 주석 비활성화 → **실기 통과 후 삭제** 대상.
- **상태: 사용자 실기기 테스트 PASS**(좌표화·렌더링·취소버그·토스트 전부). 이전 "컴파일 미검증" 해소. NGO Vector3 RPC·URP unlit CG 셰이더 표준 지원 확인.
- 브랜치 `claude/building-skills-discussion-3v8d5k`: 규칙정정 13bb7c1 · 좌표화+렌더링 9e79a2f · 취소 1차 2e88dfa · 취소 근본+토스트 4e5da5e.
