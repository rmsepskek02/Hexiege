# Plan: 부유 텍스트 World Space 전환

**작업일:** 2026-04-13  
**수정 파일:** `FloatingHpText.cs`, `FloatingHpTextSpawner.cs`, `GameBootstrapper.cs`, `FloatingHpText.prefab`

---

## 구현 목표

부유 텍스트를 Screen Space Overlay Canvas에서 World Space TextMeshPro로 전환하여  
줌 변경에 완전히 독립적인 위치와 일관된 시각적 크기를 확보한다.

---

## [1] FloatingHpText.cs

**경로:** `Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs`

### 컴포넌트 변경

| 항목 | 제거 | 추가 |
|------|------|------|
| 텍스트 | `TextMeshProUGUI _text` | `TextMeshPro _text` |
| 페이드 | `CanvasGroup _canvasGroup` | 제거 (TMP alpha로 직접 제어) |
| using | `using UnityEngine.UI` 확인 필요 | `using TMPro` 유지 |

### Play() 시그니처 변경

```csharp
// 변경 전
public void Play(string text, Vector2 anchoredPosition, float scale = 1f, Color? color = null)

// 변경 후
public void Play(string text, Vector3 worldPosition, float scale = 1f, Color? color = null)
```

### Play() 본문 변경

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| 위치 설정 | `rt.anchoredPosition = anchoredPosition` | `transform.position = worldPosition` |
| 카메라 정렬 | 없음 | `transform.rotation = Quaternion.LookRotation(-Camera.main.transform.forward, Camera.main.transform.up)` |
| 크기 설정 | `transform.localScale = Vector3.one * scale` | 동일 유지 |
| 색상 설정 | `_text.color = color ?? Color.white` | 동일 유지 |
| 알파 초기화 | `_canvasGroup.alpha = 1f` | `_text.alpha = 1f` |
| Y 이동 | `rt.DOAnchorPosY(anchoredPosition.y + _riseDistance * scale, _duration)` | `transform.DOLocalMoveY(transform.localPosition.y + _riseDistance * scale, _duration)` |
| 페이드 | `_canvasGroup.DOFade(0f, _duration)` | `_text.DOFade(0f, _duration)` |
| SetActive 조건 | `gameObject.SetActive(true)` 후 시작 | 동일 유지 |

### Awake() 변경

```csharp
// 변경 전
private void Awake()
{
    if (_canvasGroup != null)
        _canvasGroup.blocksRaycasts = false;
}

// 변경 후
private void Awake()
{
    // World Space TMP는 raycast와 무관 — blocksRaycasts 설정 불필요
}
```

---

## [2] FloatingHpTextSpawner.cs

**경로:** `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

### 필드 변경

| 항목 | 제거 | 추가 |
|------|------|------|
| Canvas 의존 | `Canvas _canvas` | 제거 |
| 컨테이너 | 없음 | `Transform _container` — 텍스트 오브젝트의 부모 Transform |
| 크기 기준값 | 제거되었던 `_referenceOrthographicSize` | 재추가 (`[SerializeField] float`, 기본값 5f) |

### Initialize() 파라미터 변경

```csharp
// 변경 전
public void Initialize(IEntityPositionProvider positionProvider, Canvas canvas, FloatingHpText prefab)

// 변경 후
public void Initialize(IEntityPositionProvider positionProvider, Transform container, FloatingHpText prefab)
```

### OnEntityDamaged() 변경

```csharp
// 변경 전 (Canvas 좌표 변환)
Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
RectTransformUtility.ScreenPointToLocalPointInRectangle(..., out Vector2 localPoint);
float scale = GetZoomScale();
hpText.Play($"{evt.CurrentHp}", localPoint + new Vector2(0f, _yOffset), 1f, textColor);

// 변경 후 (월드 좌표 직접 사용, 스케일 고정)
float scale = 1f;  // 줌 보정 제거 — 유닛/건물과 동일하게 줌 비례 동작
Vector3 spawnPos = worldPos + Vector3.up * _yOffset;
hpText.Play($"{evt.CurrentHp}", spawnPos, scale, textColor);
```

### SetParent 변경

```csharp
// 변경 전
hpText.transform.SetParent(_canvas.transform, false);

// 변경 후
hpText.transform.SetParent(_container, false);
```

### CreateInstance() 변경

```csharp
// 변경 전
FloatingHpText instance = Instantiate(_prefab, _canvas.transform);

// 변경 후
FloatingHpText instance = Instantiate(_prefab, _container);
```

---

## [3] GameBootstrapper.cs

**경로:** `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

### 필드 변경

```csharp
// 제거
[SerializeField] private Canvas _uiCanvas;

// 추가
[Tooltip("부유 텍스트 오브젝트들의 부모 컨테이너 Transform (씬의 FloatingTexts 빈 오브젝트)")]
[SerializeField] private Transform _floatingTextContainer;
```

### Initialize 호출부 변경

```csharp
// 변경 전
_floatingHpTextSpawner.Initialize(_positionProvider, _uiCanvas, _floatingHpTextPrefab);

// 변경 후
_floatingHpTextSpawner.Initialize(_positionProvider, _floatingTextContainer, _floatingHpTextPrefab);
```

> **주의**: `_uiCanvas`는 다른 UI 컴포넌트에서도 사용 중인지 확인 필요. FloatingHpTextSpawner 전용이었다면 필드 제거, 다른 곳에서도 사용 중이라면 유지.

---

## [4] FloatingHpText.prefab 재구성

**기존 구성:**
```
FloatingHpText (RectTransform + CanvasGroup)
  └─ Text (TextMeshProUGUI)
```

**신규 구성:**
```
FloatingHpText (Transform)
  └─ Text (TextMeshPro)
       폰트: Maplestory Light SDF.asset
       폰트 크기: 28
       색상: 흰색
       Alignment: Center
```

→ 프리팹 재생성 필요 (Editor 스크립트 또는 수동).

---

## [5] 씬 Inspector 작업

| 작업 | 내용 |
|------|------|
| `FloatingTexts` 빈 GameObject 배치 | 씬 루트에 빈 오브젝트 생성 (이름: FloatingTexts, Position: (0,0,0)) |
| GameBootstrapper 슬롯 재연결 | `_uiCanvas` 슬롯 제거 → `_floatingTextContainer` 슬롯에 FloatingTexts 연결 |
| `_floatingHpTextPrefab` 슬롯 | 재생성된 프리팹으로 재연결 |

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| `_uiCanvas` 다른 용도 참조 | Canvas 필드가 다른 Init에서도 사용될 경우 제거 불가 | GameBootstrapper 전체 검색 후 판단 |
| TMP DOFade 확장 메서드 | `TextMeshPro.DOFade()`는 DOTween의 TMP 확장 — `using DG.Tweening` 유지 필요 | 컴파일 확인 |
| Billboard 고정 회전 | `Camera.main.transform.rotation` 1회 설정 → 카메라가 회전하면 어긋남 | 이 게임은 카메라 회전 없음 (CameraController 확인 완료) |
| 풀 반환 시 위치 초기화 | `SetActive(false)` 후 위치가 이상한 곳에 남음 | 기능 문제 없음 (비활성 상태에서 렌더되지 않음) |
| Z-fighting/오클루전 | 3D 오브젝트에 가려질 가능성 | TMP 기본 머티리얼은 Overlay 렌더링 아님 → 필요 시 Sorting Layer 설정으로 대응 |

---

## [설계 결정] 줌 보정 제거 — 스케일 고정 (2026-04-17)

**배경:** 초기 구현에서 `scale = orthoSize / referenceSize` 수식을 사용하여 모든 줌 레벨에서 화면 고정 크기(픽셀 일정)를 유지하도록 했음.

**문제:** 줌아웃 시 유닛·건물은 작아지는데 텍스트만 보정 스케일로 커져서, 유닛 대비 텍스트 비율이 어긋나 보임.

**결정:** `scale = 1f` 고정값 사용. 텍스트가 다른 월드 오브젝트들과 동일하게 줌에 비례해 커지고 작아짐 → 유닛 대비 텍스트 비율이 모든 줌에서 일정.

**수정 파일:** `FloatingHpTextSpawner.cs` OnEntityDamaged() 내 scale 계산

---

## [버그 수정] 빌보드 회전 오류 (2026-04-13)

`Camera.main.transform.rotation` 을 그대로 복사하면 텍스트가 카메라와 동일한 방향을 바라보게 되어 카메라에 등을 보임 → 화면에 렌더되지 않는 문제.

```csharp
// 수정 전 (버그)
transform.rotation = Camera.main.transform.rotation;

// 수정 후
transform.rotation = Quaternion.LookRotation(
    -Camera.main.transform.forward,
    Camera.main.transform.up);
```

**수정 파일:** `FloatingHpText.cs` Play() 내 빌보드 정렬 코드

---

## 구현 순서

```
[1] GameBootstrapper.cs — _uiCanvas 사용처 확인 후 _floatingTextContainer 필드 교체
[2] FloatingHpTextSpawner.cs — Canvas 의존 제거, 월드 좌표 직접 사용, 크기 보정 복원
[3] FloatingHpText.cs — TextMeshPro 전환, Play() 시그니처/본문 수정
[4] FloatingHpText.prefab 재구성 (Editor 스크립트)
[5] 씬 Inspector 작업 — 컨테이너 배치, GameBootstrapper 슬롯 재연결
[6] Testcase.md 작성
[7] QA 정적 분석
```
