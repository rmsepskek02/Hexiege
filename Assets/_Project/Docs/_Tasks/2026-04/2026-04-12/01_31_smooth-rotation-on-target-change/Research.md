# Research: 타겟 변경 시 회전 부드럽게 전환

**날짜**: 2026-04-12

---

## 문제 요약

원거리 유닛이 타겟을 변경할 때 회전이 즉시 스냅되어 부자연스럽다.
예: A가 동쪽을 바라보며 B를 공격 중에 B가 사망하고 서쪽의 C로 타겟이 바뀌면 회전이 순간적으로 반전된다.

---

## 현재 동작 분석

### 회전이 설정되는 시점

| 메서드 | 파일 | 동작 |
|--------|------|------|
| `StartCombatAnimation()` (726~749행) | `UnitView.cs` | 공격 시작 시 타겟 방향으로 **즉시 스냅** |
| `ChangeTarget()` (761~775행) | `UnitView.cs` | 타겟 변경 시 새 타겟 방향으로 **즉시 스냅** |
| `Update()` (170~199행) | `UnitView.cs` | 매 프레임 타겟 방향으로 **즉시 스냅** |

세 메서드 모두 `transform.rotation = Quaternion.Euler(0f, angle, 0f)` 형태의 즉시 스냅을 사용한다.

### 부자연스러운 원인

타겟이 교체될 때:
1. `ChangeTarget()` → 새 타겟 방향으로 즉시 스냅 (한 프레임에 180° 회전도 가능)
2. `Update()` → 이후 매 프레임 새 타겟 추적

스냅 자체가 한 프레임에 발생하므로 눈에 띄게 부자연스럽다.

---

## 과거 이력: DORotate 방식이 폐기된 이유

2026-03-29 NGO 전환 작업 당시 `DORotate`(보간)를 사용한 회전이 폐기되었다.

**폐기 이유**:
- 서버 DORotate(0.3초 애니메이션) + NetworkTransform 보간(약 0.1초) = 시각적으로 ~1초 딜레이 발생
- 서버가 이미 0.3초에 걸쳐 회전을 진행하는 동안 클라이언트는 그 진행 중인 값을 다시 보간하여 표시
- 두 보간이 겹쳐서 딜레이가 누적됨

**현재 방식의 장점**:
- 서버에서 즉시 스냅 → NetworkTransform이 클라이언트에 보간 전달
- 딜레이 = NetworkTransform 동기화 딜레이(~1틱) 뿐

---

## 개선 접근법 분석

### 방법 A: `Quaternion.RotateTowards` (프레임별 점진 회전)

`Update()`에서 즉시 스냅 대신 매 프레임 지정 속도로 목표 방향에 가까워짐.

```
현재: transform.rotation = Quaternion.Euler(0f, angle, 0f)
변경: transform.rotation = Quaternion.RotateTowards(현재회전, 목표회전, 속도 * deltaTime)
```

**DORotate와의 차이 (중요)**:
- DORotate: 서버가 0.3초짜리 독립 애니메이션 실행 → 클라이언트가 그 '진행 중인 값'을 NetworkTransform으로 다시 보간 → 이중 보간 → ~1초 딜레이
- RotateTowards: 서버가 매 프레임 조금씩 직접 rotation 값 갱신 → NetworkTransform이 그 값을 그대로 동기화 → 딜레이 = NetworkTransform 1틱(정상)

**RotateTowards가 안전한 이유**:
NetworkTransform은 rotation 값을 틱 주기로 전송한다. 서버에서 RotateTowards로 매 프레임 조금씩 바뀐 값을 NetworkTransform이 받아 클라이언트에 전달하면, 클라이언트는 수신된 값 사이를 보간할 뿐이다. 서버와 클라이언트의 표시 값 차이는 최대 1틱(~33ms)으로 기존과 동일하다.

**속도 설정**:
- 너무 느림(90°/s): 빠른 타겟 전환 시 회전이 늦게 도착해 시각적 불일치
- 너무 빠름(720°/s): 스냅과 사실상 동일, 개선 효과 없음
- 적정 범위: 270°~360°/s — 타겟 방향 전환(최대 180°)이 0.5~0.67초 내 완료

### 방법 B: ChangeTarget()에서만 부드럽게 + Update()는 즉시 스냅 유지

ChangeTarget() 호출 시 플래그를 세우고, Update()에서 해당 플래그 동안만 RotateTowards 사용.

- 구현 복잡도 증가 (플래그, 임계각도 판단 필요)
- 타겟이 빠르게 이동할 때 Update()가 즉시 스냅이면 떨림 가능성
- **채택하지 않음**

---

## 결론

`Update()`의 즉시 스냅을 `Quaternion.RotateTowards`로 교체(방법 A)하는 것이 가장 단순하고 안전하다.

- `ChangeTarget()` 내 1회 스냅 제거 — Update()가 부드럽게 처리하므로 불필요
- `StartCombatAnimation()` 내 1회 스냅은 유지 — 전투 최초 진입 시 즉시 방향 전환이 자연스러움

### 변경 파일

| 파일 | 변경 내용 |
|------|---------|
| `Presentation/Unit/UnitView.cs` | Update() RotateTowards 교체, ChangeTarget() 즉시 스냅 제거 |
