# Research: 유닛 회전 부드럽게 (DOTween)

**날짜:** 2026-03-14
**작업자:** Claude

---

## 현재 상태

### 회전 처리 위치 (`UnitView.cs`)

두 곳에서 유닛 루트의 Y 회전을 설정한다.

#### 1. `ApplyDirection()` — 이동 방향 회전 (line ~212)
```csharp
private void ApplyDirection(int index)
{
    if (index < 0 || index >= DirectionAngles.Length) return;
    transform.rotation = Quaternion.Euler(0f, DirectionAngles[index], 0f);
}
```
- 문제: **즉시 스냅** → 유닛이 방향이 바뀔 때마다 순간 회전("휙휙")
- 호출 빈도: 이동 스텝마다 (유닛이 한 타일씩 이동할 때)

#### 2. `PlayAttackAnimation()` — 공격 방향 회전 (line ~499)
```csharp
float yAngle = CalculateAttackAngle(target.WorldPosition);
transform.DOKill();
transform.rotation = Quaternion.Euler(0f, yAngle, 0f);
```
- 현재: DOKill 후 즉시 스냅 (1차 구현)
- **추가 요청**: 공격 전환 시에도 DOTween 보간 적용
- 이동 회전과 동일한 `_rotationDuration` 또는 별도 `_attackRotationDuration` 사용 가능
- 공격은 타겟 정밀 조준이지만, Atan2 기반 정확한 각도를 이미 계산하므로 보간해도 문제 없음

### DirectionAngles
```csharp
private static readonly float[] DirectionAngles = { 30f, 90f, 150f, 210f, 270f, 330f };
// NE=30°, E=90°, SE=150°, SW=210°, W=270°, NW=330°
```
하위 Mesh 오브젝트 Y=30° 보정이 이 위에 추가로 적용됨.

---

## DOTween 설치 현황

- 프로젝트에 DOTween 이미 설치됨 (이전 작업에서 확인)
- `using DG.Tweening;` 추가 후 즉시 사용 가능
- 주요 API:
  - `transform.DORotate(Vector3 endValue, float duration)` — 목표 각도까지 부드럽게 회전
  - `transform.DOKill()` — 진행 중인 tween 취소 (방향이 빠르게 바뀔 때 누적 방지)

---

## 고려 사항

1. **빠른 연속 방향 변경**: 이동 스텝 간격이 짧으면 tween이 완료 전에 새 방향으로 덮어쓰여야 함 → `DOKill()` 후 새 tween 시작
2. **공격 회전과 이동 회전 충돌**: 공격 중 이동 방향 tween이 공격 각도를 덮어쓰지 않도록 주의 → 공격 시 `DOKill()` 후 즉시 스냅으로 처리
3. **`_rotationDuration` 조정**: Inspector에서 조정 가능하도록 `[SerializeField]` 노출 (기본값 0.15f 예상)
4. **Ease 타입**: 기본 `Ease.Linear` 또는 `Ease.OutQuad`를 사용해 자연스러운 감속 효과

---

## 영향 범위

- 수정 파일: `UnitView.cs` 단 1개
- 신규 의존성: `using DG.Tweening;` 추가 (이미 설치된 패키지)
- 다른 시스템 영향 없음 (회전은 순수 비주얼)
