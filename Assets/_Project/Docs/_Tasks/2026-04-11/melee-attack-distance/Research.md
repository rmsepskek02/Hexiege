# Research: 근접 공격 거리 다듬기

**날짜**: 2026-04-11

---

## 문제 정의

근접 유닛(AttackRange = 0.5)이 적 유닛을 공격할 때 두 유닛 사이에 눈에 띄는 여백이 생겨 어색함.
건물 공격 시에는 건물 메시가 크기 때문에 현재 거리(0.483f)에서 시각적으로 자연스럽게 보임.

## 원인 분석

### 현재 maxDist 계산
```
maxDist = AttackRange × TileHeight + Epsilon
        = 0.5 × 0.866 + 0.05
        = 0.483f  (모든 타겟 동일)
```

`maxDist`는 공격자 `transform.position`과 타겟 `transform.position` (타일 중심) 간 거리 기준.
프리팹 Scale과 무관 — 시각적 메시 크기는 판정에 영향 없음.

| 타겟 | 0.483f 시점 시각 | 결과 |
|------|-----------------|------|
| 건물(Castle) | 건물 메시가 커서 이미 메시에 닿아 보임 | 자연스러움 ✅ |
| 유닛 | 유닛 메시가 작아 두 모델 사이 여백 존재 | 어색함 ❌ |

## 수정 대상 파일 및 위치

`Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`

### 수정 지점 1 — `FindFirstEnemyTarget` (line 272)
```csharp
float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
// 유닛 루프 / 건물 루프 동일한 maxDist 사용
```
근접 유닛(range < 1.0)인 경우 타겟 타입별로 다른 maxDist 필요.

### 수정 지점 2 — `IsTargetInRange` (line 405)
```csharp
float maxDist = attacker.AttackRange * HexMetrics.TileHeight + Epsilon;
```
`FindFirstEnemyTarget`과 동일 로직 — 동일하게 수정 필요.

### 변경 불필요 — `FindFirstEnemyTargetByHexCoord` (HexCoord 폴백)
HexCoord.Distance는 정수 반환 → range < 1.0은 이미 `threshold = max(1, CeilToInt(0.5)) = 1`로 보정.
이번 수정(float 거리 기반)과 독립적이므로 변경 불필요.

## 확정된 수치 (사용자 결정)

| 항목 | 값 |
|------|-----|
| 근접 유닛 유닛 타겟 판정 거리 | `0.3f` |
| 근접 유닛 건물 타겟 추가 반경 | `0.2f` |
| 근접 유닛 건물 타겟 최종 거리 | `0.3f + 0.2f + Epsilon = 0.55f` |
| 원거리 유닛(range ≥ 1.0) | 기존 `AttackRange × TileHeight + Epsilon` 유지 |

건물 `+0.2f`의 의미: 건물 타일 중심에서 0.2f 바깥까지 감지 거리를 확장.
건물 메시가 크기 때문에 0.2f 일찍 감지해도 시각적으로 건물에 닿아 보임.
