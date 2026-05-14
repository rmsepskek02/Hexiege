# Plan — 유닛 회전 시스템 수정

## 이 작업으로 무엇이 달라지는가

유닛이 어디서 무엇을 하든 — A* 이동 중이든, 전투 후 이동 재개 중이든 — 항상 자신이 가려는 방향을 바라보며 부드럽게 회전하게 됩니다.
회전 속도는 Unity Inspector에서 직접 조정할 수 있습니다.
또한, 전투 후 실제로 뒤쪽으로 이동하는 현상이 있는지 정확히 파악할 수 있는 런타임 로그가 추가됩니다.

---

## 수정 항목

### 수정 1. 방향 계산 방식 교체 (규칙 8 근거)

**현재:** 출발 타일 좌표 → 도착 타일 좌표 차이로 방향을 구한 뒤, 6방향 중 하나로 맞춤
**변경:** 현재 유닛의 월드 위치 → 목적지 월드 중심까지의 벡터로 Atan2 계산

적용 위치:
- A* 이동 회전 (UnitView.cs:832~835)
- 전투 종료 후 정렬 회전 (UnitView.cs:953~969)

이 변경으로 from==to 케이스(NE 쓰레기값) 문제가 원천 해소됩니다.

---

### 수정 2. 모든 회전을 서서히 회전으로 통일 (규칙 7, 8 근거)

**현재:** A* 이동, 정렬 → 즉시 스냅 / 전투 추격, 공격 → RotateTowards
**변경:** 모든 상태에서 `Quaternion.RotateTowards` 사용

적용 위치:
- `ApplyDirection` 제거 또는 즉시 스냅 호출부를 `RotateTowards` 목표 설정으로 교체
- 단, `RotateTowards`는 코루틴 안에서 매 프레임 호출해야 하므로, A* 이동 Lerp 루프 내부에서 매 프레임 적용

구체적 동작:
- A* Lerp 루프: 매 프레임 목표 방향을 계산하고 `RotateTowards`로 서서히 회전
- 정렬 Lerp 루프: 마찬가지로 매 프레임 목표 방향 계산 후 `RotateTowards`

---

### 수정 3. 회전 속도 Inspector 노출 (규칙 7, 8, 12, 15 근거)

**현재:** `private const float CombatRotationSpeed = 270f`
**변경:** `[SerializeField] private float _rotationSpeed = 270f`

A* 이동, 정렬, 전투 추격, 공격 중 — 모두 동일한 `_rotationSpeed` 사용

---

### 추가. 런타임 로그 신규 추가

뒤쪽 이동 여부를 정밀하게 파악하기 위해 두 개의 로그 태그를 추가합니다.

**로그 출력 파일 (새 문서)**
```
Assets/_Project/Docs/_Logs/2026-05-14/14_30_unit-rotation-system-fix/RuntimeLog.txt
```
MovementLogger의 로그 파일 경로 상수를 위 경로로 변경한다.
기존 RuntimeLog.txt는 건드리지 않는다.

**ROTATION_TARGET_SET**
- 기록 시점: 회전 목표가 새로 설정될 때 (A* 타일 전환, 정렬 시작, 추격 시작)
- 기록 내용: 현재 월드 위치, 목표 월드 위치, 계산된 각도(도), 상태 구분(AST/ALIGN/PURSUIT)

**ALIGN_MOVE**
- 기록 시점: 정렬 Lerp 시작 시
- 기록 내용:
  - 출발 월드 위치 (유닛의 현재 world position)
  - forwardTile 헥스 좌표
  - forwardTile 월드 중심 위치
  - 이동 벡터 방향 (출발 → forwardTile 월드 중심)
  - 이동 벡터의 Z축 부호 판정 (FORWARD / BACKWARD — Blue팀 기준 Z 감소=앞)

---

## 수정 파일

| 파일 | 수정 내용 |
|------|----------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 회전 계산 방식, RotateTowards 통일, rotationSpeed SerializeField, 로그 추가 |

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| NetworkTransform 충돌 | 멀티플레이 클라이언트에서는 rotation을 직접 쓰지 않도록 기존 가드(IsNetworkServer 체크) 유지 필수 |
| 기존 ApplyDirection 호출부 | ApplyDirection을 제거하거나 내부를 바꾸면 다른 호출부도 영향 → 호출처 전체 확인 필요 |
| 회전이 느릴 때 타일 전환 타이밍 | rotationSpeed가 너무 낮으면 유닛이 이전 방향을 바라보며 이동할 수 있음 → 기본값 270f 유지 |

---

## 규칙 근거

| 수정 항목 | 근거 규칙 |
|----------|----------|
| 방향 계산 월드 벡터 | 규칙 8 — 재개 시 이동 방향 정면으로 |
| 모든 회전 서서히 | 규칙 7 — A* 이동 중 서서히 회전 / 규칙 8 — 재개 시 서서히 회전 / 규칙 12 — 전투 이동 중 서서히 / 규칙 15 — 공격 중 서서히 |
| rotationSpeed Inspector | 규칙 7, 8, 12, 15 모두 해당 — 속도 조정 가능해야 함 |
