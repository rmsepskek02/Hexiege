---
name: unit-stats-and-combat
description: 유닛 스탯, 전투 시스템, IEntityPositionProvider, 쿨다운 시스템
type: project
---

# 유닛 스탯 및 전투 시스템

## 유닛 확정 스탯 (2026-03-14)
| 항목 | Pistoleer | Assault | Sniper |
|------|-----------|---------|--------|
| HP | 30 | 50 | 30 |
| AttackPower | 6 | 1 | 10 |
| AttackRange (float) | 1.0 | 2.0 | 5.0 |
| MoveSeconds | 1.0 | 1.0 | 4.0 |
| ProductionTime | 5s | 10s | 15s |
| GoldCost | 50 | 100 | 200 |
| AttackCooldown | ~2.0s | ~0.2s | ~3.0s |
| DPS | 3 | 5 | ~3.3 |

- **AttackCooldown**: `UnitFactory.GetAttackClipLength()` — 클립 길이로 자동 덮어씀
- **UnitStats.cs 코드 기본값은 무관** — UnitFactory 런타임 덮어씀
- **AttackRange**: int → float 변경 (UnitData, UnitStats, UnitSpawnUseCase)

## IEntityPositionProvider (2026-03-07)
- 문제: HexCoord.Distance는 Lerp 완료 후에만 갱신 → 이동 중 최대 0.8초 공격 딜레이
- 해결: UnitFactory/BuildingFactory.GetObject()로 실시간 Transform.position 조회
- 파일: `Application/Interfaces/IEntityPositionProvider.cs`, `Infrastructure/UnitWorldPositionProvider.cs`
- 임계값: `attacker.AttackRange * HexMetrics.TileHeight` (epsilon 없음, +0.1f 제거 — 조기 공격 방지)

## 유닛별 AttackCooldown 시스템 (2026-03-06)
- UnitData.AttackCooldown / AttackCooldownRemaining (float)
- UnitFactory.GetAttackClipLength(Animator): "Attack" 포함 클립 검색
- NetworkCombatController: _attackInterval=0.1f 폴링, AttackCooldownRemaining -= interval
- UnitView.Update(): 싱글플레이에서만 AttackCooldownRemaining -= deltaTime
- MoveAlongPath 차단: HasEnemyInRange() 기반 (쿨다운 무관)

## 개별 이동속도
- UnitData.MoveSeconds (float, readonly) — 타일 1칸 소요 시간
- UnitView.MoveAlongPath: _unitData.MoveSeconds 참조

## 클라이언트 전투 시각 동기화
- 문제: 클라이언트 UnitView Lerp에서 TryAttack() 항상 false → 적 통과
- 해결: HasEnemyInRange() (네트워크 권한 체크 없음)
  - true → Idle + 대기, EntityDiedClientRpc 수신 시 Lerp 재개
- UnitView.StopMovement() public 메서드 추가
