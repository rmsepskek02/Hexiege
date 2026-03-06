# Combat Bug Fixes

## ClaimedTile 기반 공격 위치 보정 (2026-03-01)
- 문제: UnitData.Position은 ProcessStep() 완료 시에만 갱신 → Lerp 이동 중 이전 타일 좌표 유지
- ClaimedTile은 이동 목표 타일로, 시각 위치에 더 가까움
- 해결: 사거리/방향 계산 시 `attacker.ClaimedTile ?? attacker.Position` 사용
- 적용 메서드 (UnitCombatUseCase.cs):
  - FindFirstEnemyTarget(): 거리 계산
  - ExecuteAttack(): 공격 방향
  - StartAttack(): 공격 방향 (싱글플레이)
- FindNextTarget은 존재하지 않음 (지시서에 있었으나 실제 코드에 없음)

## UnitView 부드러운 회전 (2026-03-01)
- 기존: ApplyDirection()이 transform.rotation 즉시 설정
- 변경: ApplyDirection()은 _targetYRotation만 저장, Update()에서 MoveTowardsAngle 보간
- _rotationSpeed = 540f (도/초), SerializeField로 Inspector 조정 가능
- Initialize()에서는 즉시 적용 (보간 없이) — 스폰 직후 올바른 방향 보장
