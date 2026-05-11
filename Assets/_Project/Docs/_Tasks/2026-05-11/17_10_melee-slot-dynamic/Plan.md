# Plan — 근접 유닛 공격 슬롯 동적 배정 시스템

작성일: 2026-05-11  
Research: `_Tasks/2026-05-11/17_10_melee-slot-dynamic/Research.md`  
규칙 근거: `Docs/GameSystemRules.md` 규칙 11, 18

---

## 이 작업이 무엇인지

근접 유닛이 같은 적을 공격할 때 여러 명이 겹쳐 보이는 문제를 해결하기 위해
공격 슬롯 방식을 **고정 6방향 → 동적 접근 각도 등록**으로 전환한다.

추가로, 원거리 유닛(권총병)이 전투 후 이동을 재개할 때 이동 방향이 아닌 잘못된 방향을
바라보는 버그도 함께 수정한다. (적/전투와 무관하게 이동을 재개하면 이동 방향을 바라보도록)

---

## 변경 파일

1. `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs`
2. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
3. `Assets/_Project/Docs/GameSystemRules.md`

---

## 수정 1 — AttackPositionManager.cs 전면 재작성

### 1-1. 상수 교체 (라인 61~82)

**제거:**
```csharp
private const int MaxUnitsPerSlot = 2;
private const int DirectionCount = 6;
private const float DirectionStep = 60f;
private const float SamePositionEpsilon = 0.01f;
```

**추가:**
```csharp
// 한 타겟에 동시에 근접 공격할 수 있는 유닛의 최대 수.
// 이 수 이상이면 신규 유닛의 슬롯 배정을 거부하여 우회를 유도한다.
private const int MaxAttackersPerTarget = 3;

// 새로 접근하는 유닛의 각도가 기존 공격자와 이 값보다 가깝다면 배정을 거부한다.
// 60°로 설정 시 최대 3마리(0°, 120°, 240° 또는 유사 간격)가 서로 겹치지 않고 분산될 수 있다.
private const float MinAngleSeparation = 60f;
```

### 1-2. 데이터 구조 교체 (라인 84~100)

**제거:**
```csharp
private readonly Dictionary<HexCoord, Dictionary<int, Vector3>> _assignments
    = new Dictionary<HexCoord, Dictionary<int, Vector3>>();
private readonly List<Vector3> _candidateBuffer = new List<Vector3>(DirectionCount);
```

**추가:**
```csharp
// 공격자 1명의 등록 정보.
// angleDeg: 이 유닛이 타겟에 접근할 때의 방향 각도(°).
//   계산 방법: Mathf.Atan2(approachVec.x, approachVec.z) * Mathf.Rad2Deg
//   이 각도를 기준으로 신규 유닛과의 최소 간격(MinAngleSeparation)을 검사한다.
private struct AttackerInfo
{
    public int unitId;
    public float angleDeg;
}

// 타겟 타일 좌표별 공격자 목록.
// 최대 MaxAttackersPerTarget명까지 등록할 수 있다.
// Release 시 unitId로 찾아 제거한다.
private readonly Dictionary<HexCoord, List<AttackerInfo>> _attackers
    = new Dictionary<HexCoord, List<AttackerInfo>>();
```

### 1-3. ClaimByApproach 재작성 (라인 143~253)

시그니처는 기존 그대로 유지:
```csharp
public Vector3 ClaimByApproach(HexCoord targetCoord, int unitId,
    Vector3 unitWorldPosDomain, Vector3 approachVecDomain, float contactRadius)
```

반환값 변경:
- 배정 성공 → 슬롯의 뷰 월드 좌표 (기존과 동일)
- 배정 실패(정원 초과 / 각도 충돌) → **`Vector3.zero`** (신규)

새 로직:
```
1. 타겟에 대한 목록 가져오기/생성
2. 같은 unitId가 이미 등록 → 기존 angleDeg로 슬롯 위치 재계산 후 반환 (중복 호출 안전)
3. 목록 크기 >= MaxAttackersPerTarget → 배정 거부, Vector3.zero 반환
4. approach 벡터 정규화 (NormalizeXZ 헬퍼 재사용 가능)
5. 접근 각도 계산: float angleDeg = Mathf.Atan2(approach.x, approach.z) * Mathf.Rad2Deg
6. 기존 모든 AttackerInfo의 angleDeg와 각도 차이 확인
   - 각도 차이 = Mathf.Abs(Mathf.DeltaAngle(angleDeg, info.angleDeg))
     (Mathf.DeltaAngle은 360° 순환 경계를 자동 처리한다)
   - 어느 하나라도 MinAngleSeparation 미만 → 배정 거부, Vector3.zero 반환
7. 배정 성공:
   - new AttackerInfo { unitId = unitId, angleDeg = angleDeg } 를 목록에 추가
   - 슬롯 도메인 위치 = HexMetrics.HexToWorld(targetCoord) + approach * contactRadius
   - ToViewWithUnitYOffset(slotDomainPos) 반환
```

로그 출력 유지:
- MELEE_SLOT_DETAIL: 배정 성공/실패, 접근 각도, 기존 공격자 수, 거부 이유(if any)

### 1-4. ReleaseV2AttackSlot 수정

기존:
```csharp
// _assignments에서 unitId를 키로 제거
```

변경:
```csharp
// _attackers[targetCoord] 목록에서 unitId 항목을 찾아 제거
// 목록이 비면 _attackers에서 키 자체를 제거 (메모리 누수 방지)
```

### 1-5. 파일 상단 클래스 설명 주석 수정 (라인 10~18)

- "6방향(angular)" → "동적 각도 기반 (최대 MaxAttackersPerTarget마리, MinAngleSeparation° 최소 간격)"
- 슬롯 점유 정책 설명도 새 방식에 맞게 업데이트

---

## 수정 2 — UnitView.cs

### 2-1. EnterMeleePursuitV3: Vector3.zero 반환 처리

**위치**: 라인 1327~1330 사이 (첫 번째 ClaimByApproach 호출 직후)

**기존:**
```csharp
Vector3 attackSlotViewPos = _attackPositionManager.ClaimByApproach(
    targetCoord.Value, _unitData.Id, unitDomainPos, approachVecDomain, contactRadius);

_v2AttackSlotTargetCoord = targetCoord;
```

**변경:**
```csharp
Vector3 attackSlotViewPos = _attackPositionManager.ClaimByApproach(
    targetCoord.Value, _unitData.Id, unitDomainPos, approachVecDomain, contactRadius);

// [동적 슬롯 — 2026-05-11] 배정 실패(정원 초과 또는 각도 충돌)
// → 이 타겟은 이미 포화 상태이므로 공격 슬롯을 잡지 않고 A* 이동으로 우회한다.
// 이동 슬롯은 EnterMeleePursuitV3 진입 전에 이미 해제됐으므로 별도 해제 불필요.
if (attackSlotViewPos == Vector3.zero)
{
    MovementLogger.Log(_unitData.Id, "MELEE_SLOT_BYPASS",
        $"reason=ClaimFailed targetCoord={targetCoord.Value} targetId={targetId}");
    yield break;
}

_v2AttackSlotTargetCoord = targetCoord;
```

동일하게, **타겟 재선택(TARGET_SWITCH) 이후 두 번째 ClaimByApproach 호출** (라인 1382~1384) 이후에도 동일 처리를 추가한다:
```csharp
attackSlotViewPos = _attackPositionManager.ClaimByApproach(
    newCoord.Value, _unitData.Id, newUnitDomain, newApproach, contactRadius);
// [동적 슬롯] 재선택 후 배정 실패 → 우회
if (attackSlotViewPos == Vector3.zero)
{
    MovementLogger.Log(_unitData.Id, "MELEE_SLOT_BYPASS",
        $"reason=ClaimFailedAfterTargetSwitch targetCoord={newCoord.Value} targetId={targetId}");
    ReleaseV2AttackSlotIfClaimed(); // 이 시점엔 이전 슬롯이 이미 해제됐으나 안전 호출
    yield break;
}
```

### 2-2. MoveAlongPathV3: EnterStationaryCombatV3 반환 직후 StopCombatAnimation 호출

**위치**: 라인 1033~1034 사이

**기존:**
```csharp
yield return EnterStationaryCombatV3();   // line 1033
_v2InStationaryCombat = false;            // line 1034
```

**변경:**
```csharp
yield return EnterStationaryCombatV3();   // line 1033
// [방향 버그 수정 — 2026-05-11]
// A* 이동 재개 시 ApplyDirection(dir)이 이동 방향으로 회전을 설정하지만,
// _combatTargetTransform이 null이 아닌 동안 Update()가 매 프레임 회전을 덮어써서
// 이동 방향이 아닌 잘못된 방향을 바라보는 버그가 발생한다.
// StopCombatClientRpc는 TickCombat(50ms 주기)으로 지연 전송되어 곧바로 해소되지 않는다.
// 서버에서 StopCombatAnimation()을 직접 호출하면 _combatTargetTransform = null,
// _combatTargetId = -1이 즉시 적용되어 Update()의 회전 간섭이 사라진다.
// 이후 ApplyDirection(dir)이 설정한 이동 방향이 그대로 유지된다.
// 클라이언트 동기화는 기존 StopCombatClientRpc가 그대로 처리. 중복 호출은 내부 null 체크로 안전.
StopCombatAnimation();
_v2InStationaryCombat = false;            // line 1034
```

---

## 수정 3 — GameSystemRules.md 규칙 18

**위치**: 규칙 18 섹션 (라인 154~159)

기존 텍스트를 주석 처리하고 새 규칙을 그 아래에 추가한다.

**변경 전:**
```
공격 슬롯은 적의 월드 좌표를 중심으로 6방향(60° 간격) 각도 기반으로 분산 배치한다.
유닛은 접근 방향과 가장 가까운 각도의 빈 슬롯에 배정된다.
앞쪽 슬롯이 모두 찬 경우 순차적으로 측면, 뒤쪽 슬롯이 배정되어 자연스러운 포위 형태가 된다.
공격 슬롯은 타일 점유 시스템과 완전히 별개이며 A* 경로 탐색에 영향을 주지 않는다.
근접 유닛은 배정된 슬롯 위치로 이동하여 공격하고, 원거리 유닛은 슬롯 위치로 이동하지 않고 현재 자리에서 공격한다. 슬롯 클레임은 후속 유닛 분산 목적으로만 사용한다.
```

**변경 후:**
```
<!-- [2026-05-11 이전 — 6방향 고정 슬롯 방식]
공격 슬롯은 적의 월드 좌표를 중심으로 6방향(60° 간격) 각도 기반으로 분산 배치한다.
유닛은 접근 방향과 가장 가까운 각도의 빈 슬롯에 배정된다.
앞쪽 슬롯이 모두 찬 경우 순차적으로 측면, 뒤쪽 슬롯이 배정되어 자연스러운 포위 형태가 된다.
공격 슬롯은 타일 점유 시스템과 완전히 별개이며 A* 경로 탐색에 영향을 주지 않는다.
근접 유닛은 배정된 슬롯 위치로 이동하여 공격하고, 원거리 유닛은 슬롯 위치로 이동하지 않고 현재 자리에서 공격한다. 슬롯 클레임은 후속 유닛 분산 목적으로만 사용한다.
문제점: 뒤쪽 슬롯(접근 방향 기준 ±90° 초과)은 직선 이동 유닛이 물리적으로 도달할 수 없어
4번째 이후 유닛이 앞에 쌓이는 fallback 겹침 현상이 발생했다. → 2026-05-11 동적 방식으로 전환.
-->

공격 슬롯은 고정 방향이 아닌 각 유닛의 실제 접근 각도를 기반으로 동적으로 등록된다.
- 한 타겟에 동시 공격 가능한 근접 유닛은 최대 3마리다.
- 새로 접근하는 유닛의 접근 각도가 기존 공격 중인 유닛과 60° 미만으로 겹치면 배정이 거부된다.
- 배정이 거부된 유닛은 해당 타겟을 무시하고 A* 이동을 재개하여 우회한다.
  우회 중에 다른 적을 감지하면 그 적과 교전한다.
- 공격 슬롯은 타일 점유 시스템과 완전히 별개이며 A* 경로 탐색에 영향을 주지 않는다.
- 원거리 유닛은 슬롯 위치로 이동하지 않고 현재 자리에서 공격한다.
  슬롯 클레임은 후속 유닛이 충돌 없이 분산되도록 예약하는 목적으로만 사용한다.
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 타겟 재선택(TARGET_SWITCH) 시 기존 unitId 재등록 | ClaimByApproach 1단계에서 같은 unitId 이미 있으면 기존 각도 재사용 → 안전 |
| Vector3.zero 반환 시 이동 슬롯 미해제 가능성 | EnterMeleePursuitV3 진입 시점에 이동 슬롯은 이미 해제됨. yield break 전 별도 해제 불필요 |
| StopCombatAnimation() 서버 측 중복 호출 | StopCombatAnimation() 내부 null 체크 → 중복 호출 무해 |
| Mathf.DeltaAngle 각도 차이 계산 경계 처리 | Mathf.DeltaAngle(a, b)는 -180~180 반환, Abs 적용하면 0~180 → 360° 순환 경계 자동 처리 |
| 공격 슬롯 우회 후 새 타겟 감지 시 각도 90° 초과 문제 | 우회 중에는 A* 이동 재개이므로 별도 EnterMeleePursuitV3 재진입. 새 타겟에 대해 독립적으로 각도 검사 수행 |

---

## 구현 순서

1. `AttackPositionManager.cs` — 상수/구조체/사전 교체 + ClaimByApproach 재작성 + Release 수정
2. `UnitView.cs` — ClaimByApproach Vector3.zero 처리 (2곳) + StopCombatAnimation 즉시 호출 추가
3. `GameSystemRules.md` — 규칙 18 기존 내용 주석 처리 + 새 규칙 추가
