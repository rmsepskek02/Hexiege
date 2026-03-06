# 공격 방향 리팩터링 (2026-03-02)

## 요약
2D 레거시 코드 완전 제거 + 공격 방향을 월드벡터 직접 Y 회전으로 단순화.

## 제거된 코드
- `ArtDirection` enum, `FacingInfo` struct (FacingDirection.cs)
- `PointyTopMapping`, `FlatTopMapping` 배열 (FacingDirection.cs)
- `FromHexDirection()` 메서드 (FacingDirection.cs)
- `CalcViewDirection()`, `WorldDeltaToHexDirection()` (UnitCombatUseCase.cs)
- `ProcessStep()` 내 `unit.Facing = dir` 중복 (UnitMovementUseCase.cs)

## 핵심 변경
1. **UnitData.Facing = 항상 도메인 좌표** (싱글/멀티 동일)
2. **TryAttack() 반환값: bool → IDamageable** (null이면 공격 없음)
3. **공격 방향 Y 회전**: HexDirection → DirectionAngles 테이블 대신 타겟 월드벡터 → Atan2 직접 계산
4. **UnitView._meshYOffset**: 메시 자식 로컬 Y 오프셋 보정 (SerializeField, 프리팹별 설정)
5. **ApplyAttackRotation(HexCoord targetHex)**: 공통 헬퍼 (도메인 헥스→뷰 월드→Y 회전)
6. **TriggerAttackAnimation 시그니처**: `(HexDirection, HexCoord targetHex)` 로 변경
7. **PlayAttackAnimationByHex**: 네트워크 경로 전용 코루틴 (FinishAttack 호출 없음)
8. **TriggerAttackAnimationClientRpc**: `(int unitId, int facingInt, int targetQ, int targetR)` 로 확장

## 방향 결정 흐름 (변경 후)
- 싱글플레이: StartAttack → FacingDirection.FromCoords → Facing=도메인 → OnEntityAttacked → PlayAttackAnimation(target!=null) → ApplyAttackRotation(target.Position)
- 멀티플레이 서버: TryAttack → ExecuteAttack → FacingDirection.FromCoords → TriggerAttackAnimationClientRpc(facing, targetQ, targetR)
- 멀티플레이 클라이언트: TriggerAttackAnimationClientRpc → TriggerAttackAnimation(facing, targetHex) → PlayAttackAnimationByHex → ApplyAttackRotation(targetHex)

## 이동 방향 (변경 없음)
- MoveAlongPath: FacingDirection.FromCoords → ViewConverter.FlipDirection → ApplyDirection(DirectionAngles 테이블)

## 확정 설정값 (2026-03-02 테스트 완료)
- Unit_Pistoleer 프리팹: UnitView._meshYOffset = **30** (Inspector에서 설정, 코드 기본값 0f)
- 이유: Unit_Pistoleer_Mesh 자식 오브젝트 LocalY = 30° (Meshy.ai FBX 임포트 오프셋)
- Atan2 계산값에서 30을 빼야 메시가 타겟을 정확히 향함
- 새 유닛 추가 시: 해당 프리팹의 mesh child LocalY 회전값을 _meshYOffset에 동일하게 설정
