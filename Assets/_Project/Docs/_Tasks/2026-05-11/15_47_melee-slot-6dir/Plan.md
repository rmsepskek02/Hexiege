# Plan — 공격 슬롯 12방향 → 6방향 변경

작성일: 2026-05-11  
Research: `_Tasks/2026-05-11/15_47_melee-slot-6dir/Research.md`  
규칙 근거: `Docs/GameSystemRules.md` 규칙 18

---

## 이 작업이 무엇인지

유닛이 적 주변에 몰릴 때 서로 겹쳐 보이는 문제를 개선하기 위해,  
공격 슬롯 방향 수를 12개(30° 간격)에서 6개(60° 간격)로 줄인다.  
슬롯 수가 줄면 유닛 간 간격이 2배 넓어져 시각적으로 분산된 느낌이 강해진다.

---

## 변경 파일

1. `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs`
2. `Assets/_Project/Docs/GameSystemRules.md`

---

## 수정 1 — AttackPositionManager.cs 상수 변경

**위치**: 파일 상단 상수 정의부 (라인 ~78~82)

**변경 전:**
```csharp
private const int DirectionCount = 12;
private const float DirectionStep = 30f;
```

**변경 후:**
```csharp
private const int DirectionCount = 6;
private const float DirectionStep = 60f;
```

**연동되는 주석도 함께 수정:**
- "12방향(30°씩)" → "6방향(60°씩)"
- "_candidateBuffer 초기 용량 12는 DirectionCount에 맞춤" → 6
- "12방향 사이 최소 거리(약 0.15f 이상)" → "6방향 사이 최소 거리(약 0.30f 이상)"
- 파일 최상단 클래스 설명 주석 "12방향(angular)" → "6방향(angular)"

`_candidateBuffer = new List<Vector3>(DirectionCount)` 는 상수를 참조하므로 코드 변경 불필요.

---

## 수정 2 — GameSystemRules.md 규칙 18 수정

**변경 전:**
```
공격 슬롯은 적의 월드 좌표를 중심으로 12방향(30° 간격) 각도 기반으로 분산 배치한다.
```

**변경 후:**
```
공격 슬롯은 적의 월드 좌표를 중심으로 6방향(60° 간격) 각도 기반으로 분산 배치한다.
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 최대 수용 유닛 수 감소 (24명 → 12명) | MaxUnitsPerSlot=2 fallback이 있으므로 12명 초과 시 가장 적게 배정된 슬롯에 공유. 게임 특성상 한 타겟에 12명 이상 동시 공격은 발생 드물어 실질 문제 없음 |
| SamePositionEpsilon(0.01f) 유효성 | 6방향 슬롯 간 거리 0.30 >> 0.01 — 다른 슬롯을 같은 슬롯으로 오인할 위험 없음 |

---

## 구현 순서

1. `AttackPositionManager.cs` — DirectionCount/DirectionStep 상수 + 관련 주석 수정
2. `GameSystemRules.md` — 규칙 18 수치 수정
