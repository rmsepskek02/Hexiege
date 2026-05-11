# Research — 공격 슬롯 12방향 → 6방향 변경

작성일: 2026-05-11  
작업명: melee-slot-6dir

---

## 이 작업이 무엇인지

근접/원거리 유닛이 같은 적 주변에 여러 명 모였을 때 서로 겹쳐 보이는 문제를 개선한다.  
현재 12방향(30° 간격) 슬롯 시스템을 6방향(60° 간격)으로 변경하여 유닛 간 물리적 간격을 넓힌다.

---

## 현재 구조

파일: `Assets/_Project/Scripts/Application/Services/AttackPositionManager.cs`

```csharp
private const int DirectionCount = 12;   // 30°씩 12방향
private const float DirectionStep = 30f;
```

슬롯은 타겟 중심에서 반경 `contactRadius`(유닛=0.3f, 건물=0.5f) 원 위에 배치된다.  
인접 슬롯 간 실제 거리 계산:

```
거리 = 2 × contactRadius × sin(DirectionStep / 2)
     = 2 × 0.30 × sin(15°)
     = 2 × 0.30 × 0.259
     ≈ 0.155
```

---

## 로그에서 확인된 문제

HOST 실기 로그 (`2026-05-11 15:29:16` 세션) 기준:

```
[UnitID:0] chosenIdx=0  chosenAngle=0.0°   slotPos=(3.75, -7.06)
[UnitID:2] chosenIdx=1  chosenAngle=30.0°  slotPos=(3.90, -7.10)
```

두 슬롯 간 거리: `sqrt(0.15² + 0.04²) ≈ 0.155`

유닛 모델의 시각적 크기(약 0.3~0.5)에 비해 슬롯 간격이 너무 좁아 겹쳐 보인다.

---

## 6방향 변경 시 효과

```csharp
private const int DirectionCount = 6;    // 60°씩 6방향
private const float DirectionStep = 60f;
```

인접 슬롯 간 실제 거리:
```
거리 = 2 × 0.30 × sin(30°)
     = 2 × 0.30 × 0.5
     = 0.30
```

현재 0.155 → **0.30으로 약 2배 개선**.

| 항목 | 12방향 | 6방향 |
|------|--------|-------|
| 슬롯 수 | 12개 | 6개 |
| 인접 슬롯 간격 | 0.155 | 0.30 |
| 최대 수용 유닛 (MaxUnitsPerSlot=2) | 24명 | 12명 |
| 시각적 분산 | 겹쳐 보임 | 구분 가능 |

---

## 영향 파일

| 파일 | 변경 내용 |
|------|----------|
| `Application/Services/AttackPositionManager.cs` | DirectionCount 12→6, DirectionStep 30→60, 관련 주석 수정 |
| `Docs/GameSystemRules.md` | 규칙 18 "12방향(30° 간격)" → "6방향(60° 간격)" |

---

## 관련 규칙

- GameSystemRules.md 규칙 18: 공격 슬롯 배치 — 현재 "12방향(30° 간격)" → 이번 작업으로 변경 대상
