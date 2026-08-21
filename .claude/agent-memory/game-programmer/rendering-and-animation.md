---
name: rendering-and-animation
description: 3D 렌더링, UnitView 애니메이션, Shader Graph, HexTileView 팀 색상 시스템
type: project
---

# 렌더링 및 애니메이션 시스템

## UnitView 애니메이션 (2026-03-07 확정)
- **Animator Controller 파라미터**: `IsDead`(bool) 1개만 — IsWalking/Attack trigger 제거됨
- **스테이트**: Walk(기본/루프), Attack, Dead — 이름 정확히 일치 필요
- **트랜지션**: `Any State → Dead (IsDead=true)` 만 유지
- **Animator.Play() 직접 호출 방식** (트랜지션 우회):
  - Walk 시작: `_animator.Play(StateWalk, 0, 0f)` + `speed=1f` (이미 Walk면 Play 스킵)
  - Walk 정지(Idle): `speed=0f` (현재 프레임 고정)
  - 공격: `Play(StateAttack, 0, 0f)` → `yield return null` → clipLen 읽기 → WaitForSeconds
  - 사망: `speed=1f` + `SetBool(AnimIsDead, true)`
- **Idle 애니메이션 없음**: Walk speed=0으로 정지 표현

## Walk 애니메이션 연속 재생 (2026-03-09 수정)
- 문제: MoveAlongPath 매 스텝 시 Play(StateWalk,0,0f) → normalizedTime 리셋 → 반복 끊김
- 수정: Walk 상태 여부 체크 후 조건부 Play

## Animation Event 타격 반응 (2026-03-14)
- `AnimationEventRelay.cs` — Mesh 자식에 부착, `OnAttackHit` → UnitView.OnAttackHit()
- HitReactionCoroutine: scale×0.85 → 0.05초 → 원복
- Event 위치: Pistoleer=0.833s, Assault=0.1s, Sniper=2.0s
- Root Motion: 반드시 OFF (ON이면 경로/방향 버그)

## 유닛 메시 방향 보정 (2026-03-14)
- 이동 방향: Mesh 자식 Y 회전 30° (Pistoleer/Assault/Sniper 공통)
- 공격 방향: `_meshYOffset` SerializeField — CalculateAttackAngle Atan2에서 차감

## HexTileView 팀 색상 (2026-03-01)
- `material.color` → `material.SetColor("_BaseColor", X)` (SG_HexTile Shader Graph용)
- 셰이더 이름 기반 재질 탐색: `shader.name.Contains("SG_HexTile")` 루프

## 헥스 타일 (3D ProBuilder + Shader Graph)
- ProBuilder Cylinder: Sides=6, Height=0.1, 두 Submesh (top/side)
- SG_HexTile: Object Space Position 기반 HexBorder Custom Function
  - HLSL: `float d = max(p.y, p.x * 0.866 + p.y * 0.5); Border = step(0.433 - BorderSize, d);`
  - BorderSize=0.02
- mat_tile_top: SG_HexTile, #BCBCBC / mat_tile_side: #3A3A3A

## 팀별 피아식별 프리팹 (2026-03-14)
- 유닛: `Prefabs/Units/Unit_{Type}_{Blue|Red}.prefab` (6개)
- 건물: `Prefabs/Buildings/Building_{Type}_{Blue|Red}.prefab` (4개)
- 초상화: `Sprites/Units/{Type}/{type}_portrait_{blue|red}.png`
- UnitFactory: `UnitTeamPrefabSet` struct / BuildingFactory: `BuildingTeamPrefabSet` struct
- ProductionPanelUI: `UpdateButtonPortraits(TeamId)` — 팀별 초상화 동적 교체

## 범위/사거리 표시 스프라이트의 기준 크기 (2026-08-11 / 2026-08-21 복구 — 유일본)

> 2026-08-17 `675203ae` 로 `MEMORY.md` 에서 소실. `sprite.bounds.size` 산출 규칙은
> 다른 문서에 남아 있지 않아 여기로 복구한다.
> (짝을 이루던 `[FormerlySerializedAs]` 무력화 기법과 `_baseDiameterOverride` 사례는
>  `Assets/_Project/Docs/WORK_HISTORY.md` 에 보존돼 있어 중복 복구하지 않는다.)

- **스프라이트 기준 지름을 코드 상수로 두지 말고 `sprite.bounds.size` 에서 산출한다.**
  `bounds.size` = (텍스처 픽셀 ÷ Pixels Per Unit) 이라 **PPU가 이미 반영된 월드 크기**다.
- **매 표시마다 다시 읽는다(캐시 금지).** 그래야 정식 아트로 스프라이트를 교체해도 크기가 자동으로 따라간다.
- **비정사각형 스프라이트는 긴 축을 기준 지름으로 삼는다.** 과대 표시(실제보다 넓어 보임)가
  과소 표시보다 치명적이기 때문이다(플레이어가 사거리를 잘못 판단한다).
- 적용 대상: `SkillAimReticle` · `MistShrineRangeIndicator`
