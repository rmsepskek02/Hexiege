# Plan: 카메라 줌 부드러운 보간 (DOTween)

**날짜:** 2026-03-19
**관련 Research:** [research.md](research.md)

---

## 설계 결정

- **DOTween `DOTween.To()`** 방식 채택 (Ease.OutCubic)
- 입력 감지 시 `_targetZoom` 갱신 → 기존 Tween Kill → 새 Tween 시작
- `_zoomDuration` SerializeField (default=0.25f) — Inspector 조정 가능
- `ClampPosition()` 수정 불필요 (매 프레임 orthographicSize 읽으므로 자동 대응)

---

## 수정 파일

| 파일 | 수정 내용 |
|------|----------|
| `Presentation/Camera/CameraController.cs` | 필드 추가 + HandleZoom() 수정 |

---

## 구현 스펙

### 추가할 필드

```csharp
// Inspector
[Header("줌 보간")]
[SerializeField] private float _zoomDuration = 0.25f;

// 내부 상태
private float _targetZoom;
private Tweener _zoomTween;
```

### Awake() 수정

```csharp
private void Awake()
{
    _cam = GetComponent<Camera>();
    _targetZoom = _cam.orthographicSize; // 현재 값으로 초기화
}
```

### HandleZoom() 수정 (핵심)

기존:
```csharp
if (Mathf.Abs(zoomDelta) > 0.001f)
{
    _cam.orthographicSize = Mathf.Clamp(
        _cam.orthographicSize + zoomDelta,
        zoomMin, zoomMax);
}
```

수정 후:
```csharp
if (Mathf.Abs(zoomDelta) > 0.001f)
{
    _targetZoom = Mathf.Clamp(_targetZoom + zoomDelta, zoomMin, zoomMax);
    _zoomTween?.Kill();
    _zoomTween = DOTween.To(
        () => _cam.orthographicSize,
        x => _cam.orthographicSize = x,
        _targetZoom,
        _zoomDuration
    ).SetEase(Ease.OutCubic);
}
```

### using 추가

```csharp
using DG.Tweening;
```

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| DOTween 미설치 | DG.Tweening 참조 오류 | 이미 프로젝트에 DOTween 사용 중 (UnitView 등) — 문제없음 |
| Tween 누수 | OnDestroy에서 Kill 필요 | OnDestroy 추가 또는 SetLink(gameObject) 사용 |
| ClampPosition 충돌 | Tween 중 orthographicSize가 범위 초과 | _targetZoom을 Clamp로 설정하므로 Tween 도달값 안전 |

---

## 테스트 체크리스트

- [x] 마우스 스크롤: 줌인/줌아웃 부드럽게 보간되는지 확인 ✅
- [x] 연속 스크롤: 빠르게 여러 번 스크롤 시 목표값 누적 후 자연스럽게 이동 ✅
- [x] 핀치 줌 (모바일/에뮬레이터): 핀치 시 부드러운 보간 ✅
- [x] 줌 경계: min/max에서 더 이상 줌되지 않음 ✅
- [x] ClampPosition: 줌 중 카메라 위치 경계 정상 동작 ✅
- [x] 팬과 동시: 줌 중 팬해도 충돌 없음 ✅
