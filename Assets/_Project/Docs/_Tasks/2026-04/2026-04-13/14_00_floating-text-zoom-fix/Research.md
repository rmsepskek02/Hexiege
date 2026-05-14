# Research: 부유 텍스트 줌 독립 고정 크기

**작업일:** 2026-04-13  
**관련 이전 작업:** `2026-04-12/18_03_floating-hp-text/`

---

## 문제 설명

현재 부유 텍스트는 카메라 줌 상태에 따라 크기와 Y 오프셋이 변동된다.  
사용자 입장에서는 줌 레벨에 관계없이 항상 일정한 크기와 위치로 보여야 한다.

---

## 현재 구현 분석

### FloatingHpTextSpawner.cs

**경로:** `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

**줌 보정 관련 코드:**

```csharp
// SerializedField
[SerializeField] private float _referenceOrthographicSize = 5f;

// OnEntityDamaged() 내부
float scale = GetZoomScale();
hpText.Play($"{evt.CurrentHp}", localPoint + new Vector2(0f, YOffset * scale), scale, textColor);

// 줌 스케일 계산 메서드
private float GetZoomScale()
{
    if (_mainCamera.orthographic)
        return _referenceOrthographicSize / _mainCamera.orthographicSize;
    return 1f;
}
```

**동작 결과:**

| 줌 상태 | orthographicSize | scale 값 | 텍스트 크기 | Y 오프셋 |
|---------|-----------------|----------|-------------|----------|
| 기본 (기준) | 5 | 1.0 | 기본 | 80px |
| 줌인 | 2 | 2.5 | 2.5배 | 200px |
| 줌아웃 | 7 | 0.71 | 0.71배 | 57px |

→ 줌아웃 시 유닛은 작아지고 텍스트도 작아지지만, 유닛이 축소된 만큼 텍스트가 동일 비율로 줄어들지 않아 **화면에서 상대적으로 크거나 위치가 어색하게 보이는 문제** 발생.

### FloatingHpText.cs

**경로:** `Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs`

`Play()` 에서 `scale` 파라미터를 두 곳에 적용:

```csharp
transform.localScale = Vector3.one * Mathf.Max(scale, 0.1f);     // 텍스트 크기 조정
rt.DOAnchorPosY(anchoredPosition.y + _riseDistance * scale, _duration);  // 이동 거리 조정
```

---

## 위치 변환 흐름 (변경 불필요)

```
월드 좌표 (3D)
    ↓ Camera.WorldToScreenPoint()
스크린 픽셀 좌표 (2D)  ← 줌 상태 반영됨
    ↓ RectTransformUtility.ScreenPointToLocalPointInRectangle()
Canvas 로컬 좌표 (Screen Space - Overlay)
```

`WorldToScreenPoint()`가 이미 카메라 줌을 반영하므로, **텍스트 위치는 줌과 상관없이 유닛의 화면 위치를 정확히 따라간다.**  
별도 위치 보정 불필요.

---

## 해결 방향

줌 보정 로직(`GetZoomScale()`)을 제거하고 `scale = 1f` 고정.

- 텍스트 크기: 항상 프리팹에 설정된 기본 폰트 크기 유지
- Y 오프셋: 항상 `YOffset = 80px` 고정
- 이동 거리: 항상 `_riseDistance = 80px` 고정
- 위치: `WorldToScreenPoint` 변환이 이미 정확하므로 그대로 사용

---

## 영향 범위

| 파일 | 변경 여부 | 내용 |
|------|-----------|------|
| `FloatingHpTextSpawner.cs` | **수정** | `GetZoomScale()` 제거, `_referenceOrthographicSize` 필드 제거, `scale` 고정값 적용 |
| `FloatingHpText.cs` | 없음 | `scale` 파라미터 기본값(1f)이 그대로 동작 |
| `GameBootstrapper.cs` | 없음 | Inspector 연결 변경 없음 |
| `FloatingHpText.prefab` | 없음 | 구조 변경 없음 |

---

## Canvas 타입 확인

현재 Canvas는 **Screen Space - Overlay** 로 확인됨 (`GameBootstrapper.cs:118`, `FloatingHpTextSpawner.cs:48`).  
Overlay Canvas는 카메라와 무관하게 렌더링되므로, 줌 보정을 제거하면 텍스트는 항상 동일한 픽셀 크기로 표시된다.
