# Plan: 부유 텍스트 줌 독립 고정 크기

**작업일:** 2026-04-13  
**수정 파일:** `FloatingHpTextSpawner.cs` (1개)

---

## 구현 목표

카메라 줌 상태에 관계없이 부유 텍스트가 항상 일정한 크기와 Y 오프셋으로 표시되도록 줌 보정 로직을 제거한다.

---

## 변경 내용: FloatingHpTextSpawner.cs

**경로:** `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

### 제거 항목

1. `[Header("줌 스케일링")]` 어노테이션
2. `[Tooltip(...)]` + `[SerializeField] private float _referenceOrthographicSize` 필드
3. `OnEntityDamaged()` 내의 `float scale = GetZoomScale();` 호출
4. `GetZoomScale()` 메서드 전체

### 수정 항목

**`OnEntityDamaged()` — Play() 호출부:**

```
// 변경 전
float scale = GetZoomScale();
hpText.Play($"{evt.CurrentHp}", localPoint + new Vector2(0f, YOffset * scale), scale, textColor);

// 변경 후
hpText.Play($"{evt.CurrentHp}", localPoint + new Vector2(0f, YOffset), 1f, textColor);
```

- `YOffset * scale` → `YOffset` (고정 80px)
- `scale` → `1f` (고정 기본 크기)

---

## [추가 작업] YOffset Inspector 노출

### 변경 파일: FloatingHpTextSpawner.cs

`YOffset`을 코드 상수(`const`)에서 Inspector 조정 가능한 `SerializedField`로 변경한다.

```
// 변경 전
private const float YOffset = 80f;

// 변경 후
[Tooltip("피격 오브젝트 머리 위쪽 시작 오프셋 (픽셀 단위). 클수록 텍스트가 더 높은 위치에서 시작됨.")]
[SerializeField] private float _yOffset = 80f;
```

`OnEntityDamaged()` 내 `YOffset` → `_yOffset` 참조 변경.

---

## FloatingHpText.cs — 변경 없음

`Play(scale: 1f)`가 전달되면:
- `transform.localScale = Vector3.one * 1f` → 기본 크기 유지
- `_riseDistance * 1f` → 고정 80px 이동

`scale` 파라미터 자체는 유지 (기본값 1f). 코드 변경 불필요.

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| Inspector _referenceOrthographicSize 잔존 | 필드 제거 후 Inspector에 표시되던 슬롯이 사라짐 | 기능에 영향 없음. Unity가 serialize된 필드를 제거하면 조용히 무시함 |
| 텍스트 크기 체감 변화 | 이전엔 기준 줌에서만 기본 크기였으나, 이제 모든 줌에서 동일 크기 | 의도된 변경 — 사용자 요구사항 |

---

## 구현 순서

```
[1] FloatingHpTextSpawner.cs 수정
      - _referenceOrthographicSize 필드 + Header/Tooltip 제거
      - OnEntityDamaged()에서 scale 계산 제거 + Play() 호출부 수정
      - GetZoomScale() 메서드 제거
[2] Testcase.md 작성
[3] QA 정적 분석
```
