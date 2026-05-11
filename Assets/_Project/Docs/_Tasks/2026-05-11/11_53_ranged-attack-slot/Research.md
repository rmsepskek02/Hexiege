# Research — 원거리 유닛 전투 지점 겹침 방지

작성일: 2026-05-11  
작업명: ranged-attack-slot

---

## 이 작업이 무엇인지

원거리 유닛(권총병)들이 전투 지점에서 서로 겹치는 문제를 해결한다.

양측 유닛들이 행진하다가 전투 지점에서 만나면, 원거리 유닛은 적을 감지하자마자 그 자리에서 멈추고 공격한다. 이후 5초마다 생성되는 후속 유닛들이 계속 같은 전투 지점에 도착해 멈추면서 여러 유닛이 좁은 공간에 겹치게 된다.

현재 구조에서는 유닛이 전투 중 멈추면 그 위치를 시스템이 추적하지 않아, 나중에 도착하는 유닛이 "이미 누가 있다"는 것을 알 수 없다.

---

## 현재 동작 — 두 가지 문제

### 문제 1: 목적지 타일 위치로 순간이동

파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

`MoveAlongPathV3` 내 Lerp 이동 중 원거리 유닛이 적을 감지하면:

```csharp
// 라인 ~1012
transform.position = toPos;  // 목적지 타일 슬롯 위치로 강제 스냅
```

Lerp를 시작한 직후에 감지하면 거의 한 타일만큼 순간이동하는 것처럼 보인다.

### 문제 2: 전투 중 위치 추적 없음

`EnterStationaryCombatV3` (라인 ~1365)는 공격 슬롯을 클레임하지 않는다.

```csharp
private IEnumerator EnterStationaryCombatV3()
{
    if (!_combatUseCase.HasEnemyInRange(_unitData)) yield break;
    yield return EnterCombatLoopV3();  // 슬롯 클레임 없이 바로 전투 루프
}
```

유닛이 멈춘 위치를 AttackPositionManager가 모르므로, 나중에 도착하는 유닛이 같은 자리에 겹쳐 멈춘다.

---

## 현재 근접 유닛의 분산 방식

`EnterMeleePursuitV3` 참고:

```
1. AttackPositionManager.ClaimByApproach(타겟좌표, 유닛ID, 현재위치, 접근방향, contactRadius)
   → 타겟 주변 12방향(30°씩) 중 현재 접근 방향에 가장 가까운 빈 슬롯 배정
2. 유닛이 그 슬롯 위치로 직진 이동
3. 전투 종료 시 ReleaseV2AttackSlotIfClaimed() 해제
```

AttackPositionManager는 이미 유닛/건물 모두에 대해 이 방식으로 분산을 처리한다.

---

## 영향 파일

| 파일 | 변경 내용 |
|------|----------|
| `Presentation/Unit/UnitView.cs` | 문제 1, 2 모두 수정 |

다른 파일 변경 없음.

---

## 관련 규칙

- GameSystemRules.md 규칙 13: 원거리 유닛은 공격 사거리 내 적 진입 시 현재 위치에서 즉시 공격. 이동 없음.
- GameSystemRules.md 규칙 18: 공격 슬롯은 접근 방향 기반 12방향 분산.
