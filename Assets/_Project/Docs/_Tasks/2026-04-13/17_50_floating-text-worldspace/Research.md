# Research: 부유 텍스트 World Space 전환

**작업일:** 2026-04-13  
**관련 이전 작업:** `2026-04-13/14_00_floating-text-zoom-fix/`

---

## 문제 설명

Screen Space Overlay Canvas 기반 구현의 구조적 한계로 두 가지 문제가 발생:

1. **줌에 따른 간격 차이**: Y Offset이 픽셀 단위로 고정 → 줌아웃 시 유닛은 작아지는데 텍스트 간격은 동일 픽셀로 유지 → 상대적으로 멀어 보임
2. **애니메이션 중 위치 어긋남**: 피격 시점에 위치를 1회 계산 후 고정 → 애니메이션 도중 줌 변경 시 유닛 화면 위치는 바뀌지만 텍스트는 기존 픽셀 좌표에 고정

---

## 현재 구현 분석

### 좌표 계산 (FloatingHpTextSpawner.cs)

```
월드 좌표 → WorldToScreenPoint → 스크린 픽셀 → ScreenPointToLocalPointInRectangle → Canvas 로컬 좌표
                 [피격 시 1회]
                       ↓
                 Canvas에 고정 (이후 갱신 없음)
```

→ 줌 변경 시 유닛은 새 화면 위치로 이동하지만 텍스트는 이전 픽셀 좌표에 머물러 어긋남.

### 현재 의존 구조

| 클래스 | Canvas 의존 |
|--------|------------|
| `FloatingHpTextSpawner` | `Canvas _canvas` — 좌표 변환 기준 + 부모 |
| `FloatingHpText` | `RectTransform`, `CanvasGroup` — UI 전용 타입 |
| `GameBootstrapper` | `Canvas _uiCanvas` — Initialize() 시 주입 |

---

## 해결 방향: World Space TextMeshPro 전환

### 핵심 원리

텍스트를 3D 월드 공간에 직접 배치하면:
- 위치가 월드 좌표 기준으로 설정 → 줌이 바뀌어도 유닛과의 세계 단위 거리는 동일
- 애니메이션도 월드 좌표 기준 이동 → 줌 변경의 영향 없음

### 텍스트 크기 보정 필요

World Space 오브젝트는 줌아웃 시 화면에서 작아 보임.  
`scale = referenceOrthographicSize / camera.orthographicSize` 보정으로 항상 동일한 시각적 크기 유지.  
(Overlay에서의 scale 보정과 달리, World Space에서는 이 보정이 자연스럽고 올바름)

### TMP 컴포넌트 변경

| 항목 | 현재 (UGUI) | 변경 후 (World Space) |
|------|------------|----------------------|
| 텍스트 컴포넌트 | `TextMeshProUGUI` | `TextMeshPro` |
| 페이드 제어 | `CanvasGroup.DOFade` | `TextMeshPro.DOFade` (DOTween TMP 확장) |
| 위치 타입 | `RectTransform.anchoredPosition` | `Transform.localPosition` |
| 이동 애니메이션 | `DOAnchorPosY` | `DOLocalMoveY` |
| 부모 설정 | Canvas 자식 | 씬 컨테이너(빈 GameObject) 자식 |

### Y Offset 단위 변경

| | 현재 | 변경 후 |
|-|------|---------|
| 단위 | 픽셀 (80px) | 월드 단위 (기본값 1.5f 예정) |
| 의미 | Canvas 로컬 픽셀 거리 | 3D 공간의 Y축 거리 |

### 카메라 방향 정렬 (Billboard)

World Space 오브젝트는 카메라를 바라보도록 회전해야 화면에서 읽힘.  
이 게임은 Orthographic 카메라 + 고정 틸트 각도 사용 → `Play()` 호출 시 `transform.rotation = Camera.main.transform.rotation` 1회 설정으로 충분.

---

## 영향 범위

| 파일 | 변경 유형 | 내용 |
|------|----------|------|
| `FloatingHpText.cs` | 대폭 수정 | TMP 3D 전환, 좌표계 변경, 크기 보정 추가 |
| `FloatingHpTextSpawner.cs` | 수정 | Canvas 의존 제거, 월드 좌표 직접 사용, 크기 보정 계산 추가 |
| `GameBootstrapper.cs` | 소폭 수정 | Initialize() 파라미터에서 Canvas 제거, 컨테이너 오브젝트 추가 |
| `FloatingHpText.prefab` | 재구성 | TextMeshProUGUI → TextMeshPro, CanvasGroup 제거 |
| `씬(Game.unity)` | Inspector 작업 | FloatingHpTexts 컨테이너 오브젝트 배치, GameBootstrapper 슬롯 재연결 |
