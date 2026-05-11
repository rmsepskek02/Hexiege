# Research — 근접 유닛 공격 슬롯 동적 배정 시스템

작성일: 2026-05-11  
작업명: melee-slot-dynamic

---

## 이 작업이 무엇인지

근접 유닛이 같은 적에게 몰릴 때 여전히 겹쳐 보이는 문제를 근본적으로 해결한다.

직전 작업(15_47_melee-slot-6dir)에서 12방향 → 6방향으로 줄였으나 실기 테스트에서 세 가지 문제가 확인됐다.

1. **유닛이 제대로 접근하지 못하는 현상**: 뒤쪽 슬롯에 배정된 유닛이 앞쪽 유닛들에 막혀 목표 지점에 도달하지 못한다.
2. **살아 있는 유닛에게 같은 위치 배정**: 슬롯이 꽉 찬 이후 fallback 공유(MaxUnitsPerSlot=2)로 인해 두 유닛이 동일한 좌표를 목표로 삼아 겹친다.
3. **권총병(원거리 유닛) 방향 버그**: 전투 후 이동을 재개할 때 유닛이 이동 방향이 아닌 잘못된 방향을 바라본다.

이 작업은 고정 6방향 슬롯 방식을 완전히 버리고, 각 유닛이 실제로 접근하는 방향을 그대로 슬롯으로 등록하는 동적 방식으로 전환한다.  
한 타겟에 최대 3마리까지만 붙을 수 있고, 서로 60° 이상 벌어진 방향에서만 접근을 허용한다.  
3마리가 모두 찼거나 각도가 충돌하면 배정을 거부하고 해당 유닛은 A* 이동으로 우회한다.

권총병 방향 버그는 전투 종료 즉시 방향 초기화를 직접 호출하는 방식으로 수정한다.

---

## 현재 문제 상세 분석

### 문제 1 & 2: 후방 슬롯 도달 불가 + 동일 위치 배정

6방향 고정 슬롯에서 앞쪽 슬롯(접근 방향 기준 0°, 60°, -60°)은 직선 이동으로 자연스럽게 도달 가능하다.  
그러나 뒤쪽 슬롯(120°, 180°, -120°)은 이미 앞에 서 있는 유닛들을 뚫고 돌아가야 도달 가능하다.

`EnterMeleePursuitV3`는 A* 없이 직선 이동만 하므로 뒤쪽 슬롯을 향해 이동하다 앞쪽 유닛들에 막혀 멈춘다.  
결과적으로 앞쪽 3개 슬롯만 실제로 사용되고, 4번째 이후 유닛은 이미 점유된 슬롯의 fallback(MaxUnitsPerSlot=2)으로 배정된다.  
→ **동일한 Vector3 위치에 2명이 겹쳐서 배정.**

실기 로그 (2026-05-11) 확인:
```
[UnitID:0] chosenIdx=0  chosenAngle=0.0°   slotPos=(3.75, -7.06)
[UnitID:2] chosenIdx=0  chosenAngle=0.0°   slotPos=(3.75, -7.06)   ← 살아 있는데 겹침
```

### 문제 3: 권총병 전투 후 방향 오류

A* 이동 중 각 타일 스텝이 시작될 때 `ApplyDirection(dir)` (line 897)가 호출되어  
유닛이 이동 방향(from → to)을 바라보도록 회전을 설정한다.

그런데 `UnitView.Update()` (line 256~293)는 `_combatTargetTransform`이 null이 아닌 동안  
매 프레임 유닛을 적 방향으로 회전시켜 `ApplyDirection`이 설정한 회전을 계속 덮어쓴다.

`_combatTargetTransform`은 `StopCombatAnimation()` (line 1892)이 호출될 때 null이 된다.  
그런데 `StopCombatAnimation()`은 `NetworkCombatController.TickCombat()`(50ms 주기)에서  
`StopCombatClientRpc`로 배치 전송된다.

흐름:
```
EnterStationaryCombatV3 종료 → MoveAlongPathV3 이동 재개
→ 새 타일 스텝 시작 → ApplyDirection(dir) 이동 방향으로 회전 설정
→ (동시에) Update()가 _combatTargetTransform을 참조하여 매 프레임 회전 덮어쓰기
→ 50ms 후 TickCombat이 StopCombatClientRpc 발송 → StopCombatAnimation() 호출
→ _combatTargetTransform = null → Update() 회전 간섭 중단
→ 결과: 50ms 동안의 간섭으로 이동 방향도 아닌 잘못된 중간 방향을 바라보게 됨
```

이전 "적 방향"이 아닌 이유: 적이 이미 처치됐거나 이동했고, 50ms 간섭 중 완전히  
적 방향으로 수렴되지 못한 채 중단되어 불특정 중간 방향으로 굳어진다.

**수정 방향**: 이동 재개 시점에 `_combatTargetTransform`을 즉시 초기화하여  
`Update()`의 회전 간섭을 없애면, `ApplyDirection(dir)`이 설정한 이동 방향이  
그대로 유지된다. 적/전투와 무관하게 이동을 재개하면 항상 이동 방향을 바라보게 된다.

---

## 현재 코드 구조

### AttackPositionManager.cs

파일: `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs`

```csharp
// 고정 슬롯 상수
private const int MaxUnitsPerSlot = 2;
private const int DirectionCount = 6;
private const float DirectionStep = 60f;
private const float SamePositionEpsilon = 0.01f;

// 타겟별 슬롯 점유 상태
// 외부 키: 타겟 타일 좌표, 내부 키: 유닛 Id, 값: 도메인 좌표 슬롯 위치
private readonly Dictionary<HexCoord, Dictionary<int, Vector3>> _assignments;

// 후보 위치 재사용 버퍼 (DirectionCount 크기)
private readonly List<Vector3> _candidateBuffer = new List<Vector3>(DirectionCount);
```

`ClaimByApproach` 동작 (line 143):
1. 같은 unitId 이미 등록 → 기존 슬롯 재사용
2. 6방향(0°, 60°, 120°, 180°, 240°, 300°) 후보 중 빈 슬롯 + 접근 방향 각도 차이가 가장 작은 것 선택
3. 빈 슬롯 없으면 fallback — 가장 적게 점유된 슬롯을 MaxUnitsPerSlot(=2)까지 공유

### UnitView.cs

`EnterMeleePursuitV3` (line 1262):
- `ClaimByApproach` 호출(line 1327)로 슬롯 위치 배정
- 배정된 ViewPos를 향해 `transform.position += moveDir.normalized * worldSpeed * Time.deltaTime` 직선 이동

`EnterStationaryCombatV3` 호출부 (line 1033, MoveAlongPathV3 내부):
```csharp
yield return EnterStationaryCombatV3();   // line 1033
_v2InStationaryCombat = false;            // line 1034
// 이 시점에 _combatTargetTransform이 아직 null이 아님 → 방향 버그
```

---

## 새 시스템 설계

### 동적 슬롯 등록 방식

기존: 6개 방향을 미리 고정 → 해당 방향으로 후보 위치 생성 → 가장 적합한 것 배정  
변경: 각 유닛의 실제 접근 각도(Atan2)를 그대로 등록 → 각도 충돌 여부만 검사 → 배정 or 거부

### 새 규칙 (3가지)

1. **최대 3마리**: 한 타겟에 동시 공격 중인 근접 유닛이 3마리이면 배정 거부
2. **60° 최소 간격**: 새로 접근하는 각도가 기존 등록된 모든 각도와 60° 미만으로 겹치면 배정 거부
3. **배정 실패 시 우회**: `Vector3.zero` 반환 → `EnterMeleePursuitV3`에서 `yield break` → A* 이동 재개

### 새 데이터 구조

```csharp
// 공격자 1명의 정보 (유닛 Id + 접근 각도)
private struct AttackerInfo
{
    public int unitId;
    public float angleDeg;
}

// 타겟별 공격자 목록 (최대 MaxAttackersPerTarget명)
private readonly Dictionary<HexCoord, List<AttackerInfo>> _attackers
    = new Dictionary<HexCoord, List<AttackerInfo>>();
```

### ClaimByApproach 새 동작

```
1. unitId 이미 등록 → 기존 angleDeg로 슬롯 위치 재계산 후 반환 (중복 호출 안전)
2. 목록 크기 >= 3(MaxAttackersPerTarget) → 배정 거부, Vector3.zero 반환
3. 접근 각도 계산: Mathf.Atan2(approach.x, approach.z) * Rad2Deg
4. 기존 모든 AttackerInfo와 각도 차이 확인 (360° 순환 고려)
   - 최소 각도 차 < 60°(MinAngleSeparation) → 배정 거부, Vector3.zero 반환
5. 배정 성공:
   - (unitId, angleDeg) 목록에 추가
   - 슬롯 위치 = HexMetrics.HexToWorld(targetCoord) + approachDir × contactRadius
   - ToViewWithUnitYOffset 변환 후 반환
```

### 권총병 방향 버그 수정

`yield return EnterStationaryCombatV3()` (line 1033) 직후, 이동을 재개하기 전에  
서버에서 직접 `StopCombatAnimation()`을 호출한다.

이렇게 하면 `_combatTargetTransform = null`, `_combatTargetId = -1`이 즉시 적용되어  
`Update()`의 회전 간섭이 사라진다. 이후 새 타일 스텝에서 `ApplyDirection(dir)`이 설정하는  
이동 방향이 덮어쓰여지지 않아 유닛이 이동 방향을 정확하게 바라보게 된다.

클라이언트는 기존 `StopCombatClientRpc`(TickCombat 경유)가 그대로 처리하므로 동기화 변경 불필요.  
`StopCombatAnimation()` 내부 null 체크로 중복 호출은 무해하다.

---

## 영향 파일

| 파일 | 변경 내용 |
|------|----------|
| `Application/Services/AttackPositionManager.cs` | 데이터 구조 전면 교체, ClaimByApproach 로직 재작성, Release 로직 수정 |
| `Presentation/Unit/UnitView.cs` | EnterMeleePursuitV3: Vector3.zero 반환 시 yield break 처리; line 1033 이후 StopCombatAnimation() 즉시 호출 |
| `Docs/GameSystemRules.md` | 규칙 18 — 기존 내용 주석 보존, 새 동적 슬롯 규칙 추가 |
