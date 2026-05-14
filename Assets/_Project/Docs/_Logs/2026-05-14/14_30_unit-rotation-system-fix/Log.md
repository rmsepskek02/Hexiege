# Log — 유닛 회전 시스템 수정

## 작업 개요

유닛 회전 시스템의 두 가지 구조적 문제를 수정하고, 관련 규칙을 GameSystemRules.md에 반영했다.

- **문제 1**: 방향 계산이 타일 좌표 차이(FacingDirection.FromCoords)를 사용 → from==to 케이스에서 엉뚱한 방향(NE 기본값) 반환
- **문제 2**: A* 이동과 정렬 단계에서 즉시 스냅 회전 사용 (규칙 7, 8 위반)

---

## 구현 결과

### 수정 1 — 방향 계산 방식 교체

`FacingDirection.FromCoords(from, to)` (타일 좌표 차이) → `CalculateAttackAngle(toPos)` (현재 월드 위치 → 목적지 월드 중심 Atan2)

- A* 이동 타일 전환 시 적용
- 전투 종료 후 정렬(Align) 단계 적용
- from==to 케이스 문제 원천 해소

### 수정 2 — 모든 회전 RotateTowards 통일

- A* Lerp 루프 내 매 프레임 `Quaternion.RotateTowards(현재, 목표, _rotationSpeed * Time.deltaTime)` 적용
- 정렬 Lerp 루프 내 동일하게 적용
- 기존 `ApplyDirection()` 호출부(2곳) 제거

### 수정 3 — 회전 속도 Inspector 노출

```csharp
// 이전
private const float CombatRotationSpeed = 270f;

// 이후
[SerializeField] private float _rotationSpeed = 270f;
```

모든 회전(A* 이동, 정렬, 전투 추격, 공격) 공통 사용.

---

## 런타임 로그 분석 결과 (2026-05-14 18:29~)

테스트 후 RuntimeLog.txt 분석 결과:

| 항목 | 결과 |
|------|------|
| A* 이동 회전 (ROTATION_TARGET_SET) | Blue 0°, Red 180°, 대각선 정확한 Atan2 각도 — **정상** |
| 정렬 이동 타일 방향 (ALIGN_MOVE) | 전부 앞쪽 타일 (z 증가 = Blue FORWARD) — **정상** |
| BACKWARD 라벨 | 진단 코드 버그 — 실제 이동은 모두 앞쪽 |
| 실제 뒤쪽 타일 이동 | **이번 테스트에서 미확인** |

BACKWARD 라벨 버그 원인:
```csharp
// alignView.z < transform.position.z ? "FORWARD" : "BACKWARD"
// Blue팀 앞쪽 방향 = z 증가이므로 조건이 반대임
```
해당 코드는 MovementLogger 전체 삭제와 함께 제거됨.

---

## 관련 규칙 업데이트

`GameSystemRules.md` 개정: 규칙 전체 재번호화(1~16), 이동/전투 관련 규칙에 회전 방식 통합
- 규칙 7: A* 이동 중 서서히 회전
- 규칙 8: A* 재개 시 이동 방향 바라보며 서서히 회전
- 규칙 12: 전투 이동 중 서서히 회전
- 규칙 15: 공격 중 서서히 회전

---

## 수정 파일 목록

| 파일 | 내용 |
|------|------|
| `Presentation/Unit/UnitView.cs` | 방향 계산 교체, RotateTowards 적용, _rotationSpeed SerializeField |
| `Application/Services/MovementLogger.cs` | **삭제** (런타임 로그 진단 완료 후 제거) |
| `Bootstrap/GameBootstrapper.cs` | MovementLogger.SessionStart() 호출 제거 |
| `Application/Services/AttackPositionManager.cs` | MovementLogger.Log() 호출 3개 제거 |
| `Docs/GameSystemRules.md` | 회전 규칙 통합, 전체 재번호화 |
