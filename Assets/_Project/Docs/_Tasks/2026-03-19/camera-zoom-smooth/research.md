# Research: 카메라 줌 부드러운 보간 (DOTween)

**날짜:** 2026-03-19

---

## 1. 현재 줌 구현 분석

**파일**: `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs`

### 현재 HandleZoom() 핵심 코드 (라인 204~209)

```csharp
if (Mathf.Abs(zoomDelta) > 0.001f)
{
    _cam.orthographicSize = Mathf.Clamp(
        _cam.orthographicSize + zoomDelta,
        zoomMin, zoomMax);
}
```

**문제**: 입력이 있는 매 프레임마다 `orthographicSize`를 즉시 덮어씀.
- 마우스 스크롤 1 notch = 정규화 후 약 1.0 단위 즉시 적용
- 핀치 입력도 프레임별 diff를 즉시 적용
- 결과: 뚝뚝 끊기는 계단식 줌 변화

---

## 2. DOTween 적용 방식

### 방식 A: 목표값 추적 + 매 입력마다 새 Tween (채택)

```csharp
private float _targetZoom;
private Tweener _zoomTween;

// 입력 감지 시:
_targetZoom = Mathf.Clamp(_targetZoom + zoomDelta, zoomMin, zoomMax);
_zoomTween?.Kill();
_zoomTween = DOTween.To(
    () => _cam.orthographicSize,
    x => _cam.orthographicSize = x,
    _targetZoom,
    _zoomDuration
).SetEase(Ease.OutCubic);
```

**장점**:
- 입력이 올 때마다 목표값 갱신, 이전 Tween 취소 후 새 목표로 부드럽게 전환
- 연속 스크롤 시 목표값이 누적되어 빠르게 줌 가능
- `Ease.OutCubic`: 시작 빠르고 끝에서 감속 → 자연스러운 느낌

### 방식 B: Update에서 Lerp (미채택)

```csharp
_cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetZoom, Time.deltaTime * lerpSpeed);
```

**단점**: 목표값에 점근적으로 접근하여 도달 시간이 불정확, DOTween에 비해 제어성 낮음

---

## 3. ClampPosition과의 연계

`ClampPosition()`은 `Update()`에서 줌/팬 후 매 프레임 호출됨.
DOTween으로 `orthographicSize`가 비동기적으로 변경되어도:
- `ClampPosition()`이 매 프레임 `_cam.orthographicSize`를 읽으므로 자동 대응 ✅
- 별도 수정 불필요

---

## 4. GameConfig 연동

현재 GameConfig에 있는 줌 관련 필드:
- `CameraZoomSpeed` — 스크롤 1 notch당 zoomDelta 계산에 사용
- `CameraZoomMin` / `CameraZoomMax` — 목표값 Clamp에 사용

추가할 Inspector 설정:
- `_zoomDuration` (SerializeField, default=0.25f) — DOTween 지속시간, Inspector에서 조정 가능

---

## 5. 영향 범위

| 파일 | 변경 내용 | 규모 |
|------|----------|------|
| `CameraController.cs` | HandleZoom() 수정, _targetZoom/_zoomTween 필드 추가 | 소 |

- `GameConfig.cs`: 변경 불필요 (zoomDuration은 CameraController SerializeField로 충분)
- `ClampPosition()`: 변경 불필요
- `HandlePan()`: 변경 불필요
