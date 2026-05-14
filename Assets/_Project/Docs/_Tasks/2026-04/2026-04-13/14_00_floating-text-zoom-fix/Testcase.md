# Testcase: 부유 텍스트 줌 독립 고정 크기

**작업일:** 2026-04-13  
**수정 파일:** `FloatingHpTextSpawner.cs`

---

## 테스트 케이스

### TC-1: SINGLE-줌인 상태에서 텍스트 크기 일관성

**전제:** 게임이 시작되고 유닛이 전투 중이다. 카메라가 최대로 줌인된 상태이다.

**동작:**
1. 유닛이 피격되어 텍스트가 나타난다.
2. 텍스트 크기를 관찰한다.

**기댓값:**
- 텍스트가 기본 크기(폰트 크기 28)로 표시된다.
- 이전 구현 대비 줌인 시 텍스트가 더 이상 확대되지 않는다.

**결과:**

---

### TC-2: SINGLE-줌아웃 상태에서 텍스트 크기 일관성

**전제:** 게임이 시작되고 유닛이 전투 중이다. 카메라가 최대로 줌아웃된 상태이다.

**동작:**
1. 유닛이 피격되어 텍스트가 나타난다.
2. 텍스트 크기를 관찰한다.

**기댓값:**
- 텍스트가 기본 크기(폰트 크기 28)로 표시된다.
- 줌인 상태와 동일한 크기로 보인다.

**결과:**

---

### TC-3: SINGLE-줌 변경 중 텍스트 크기 비교

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 기본 줌 상태에서 피격 텍스트를 관찰한다.
2. 카메라를 줌인한 뒤 피격 텍스트를 관찰한다.
3. 카메라를 줌아웃한 뒤 피격 텍스트를 관찰한다.

**기댓값:**
- 세 경우 모두 텍스트 크기가 동일하게 보인다.
- 텍스트가 위로 이동하는 거리도 동일하게 보인다.

**결과:**

---

### TC-4: SINGLE-피격 텍스트 위치가 유닛 위에 올바르게 나타남

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 여러 줌 레벨에서 유닛이 피격되는 것을 관찰한다.

**기댓값:**
- 텍스트가 피격 유닛의 머리 위 위치에 나타난다.
- 줌 레벨에 관계없이 유닛 화면 위치와 텍스트 위치가 일치한다.

**결과:**

---

### TC-5: MULTI-멀티플레이 클라이언트에서 텍스트 크기 일관성

**전제:** 호스트(서버)와 클라이언트가 접속한 상태에서 전투가 발생한다.

**동작:**
1. 클라이언트 화면에서 줌인/줌아웃하면서 피격 텍스트를 관찰한다.

**기댓값:**
- 클라이언트에서도 줌 레벨에 관계없이 텍스트가 동일한 크기로 표시된다.

**결과:**

---

## QA 정적 분석

### FloatingHpTextSpawner.cs 변경 검증

| 항목 | 결과 |
|------|------|
| `_referenceOrthographicSize` 필드 제거됨 | PASS |
| `GetZoomScale()` 메서드 제거됨 | PASS |
| `OnEntityDamaged()`에서 `float scale = GetZoomScale()` 제거됨 | PASS |
| `Play()` 호출부에 `YOffset` 고정값 사용 | PASS |
| `Play()` 호출부에 `scale = 1f` 고정값 사용 | PASS |
| `_mainCamera` 필드 보존됨 (`WorldToScreenPoint` 여전히 사용) | PASS |
| 컴파일 오류 없음 | PASS |

---

## 정적 분석 결과 (qa-tester)

### 분석 대상 파일
- `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`
- `Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs`

### 삭제된 심볼 잔존 참조 전수 검색

프로젝트 전체 `.cs` 파일 대상으로 Grep 검색 수행.

| 검색 패턴 | 검색 범위 | 결과 |
|-----------|-----------|------|
| `GetZoomScale` | `Assets/_Project/Scripts/**/*.cs` | 참조 없음 — PASS |
| `_referenceOrthographicSize` | `Assets/_Project/Scripts/**/*.cs` | 참조 없음 — PASS |

### `_mainCamera` 필드 사용 여부 확인

`FloatingHpTextSpawner.cs` 기준:
- 80번째 줄: `private Camera _mainCamera;` — 필드 선언 보존
- 111번째 줄: `_mainCamera = Camera.main;` — Initialize()에서 캐싱
- 157번째 줄: `if (_mainCamera == null) return;` — null 가드
- 161번째 줄: `_mainCamera.WorldToScreenPoint(worldPos)` — 실제 사용

줌 보정 로직 제거 후에도 `_mainCamera`는 월드→스크린 좌표 변환에 필요하므로 보존이 올바름.

### `Play()` 호출부 인자 검증

`FloatingHpTextSpawner.cs` 191~195번째 줄:
```
hpText.Play(
    $"{evt.CurrentHp}",
    localPoint + new Vector2(0f, YOffset),  // YOffset = 80f 고정
    1f,                                      // scale 고정
    textColor);
```

`FloatingHpText.Play()` 시그니처(`FloatingHpText.cs` 127번째 줄):
```
public void Play(string text, Vector2 anchoredPosition, float scale = 1f, Color? color = null)
```

- 파라미터 개수/타입 일치 — 컴파일 오류 없음.

### 참고: FloatingHpText 내부의 scale 적용 코드

`FloatingHpText.cs` 149번째 줄: `transform.localScale = Vector3.one * Mathf.Max(scale, 0.1f);`
`FloatingHpText.cs` 174번째 줄: `rt.DOAnchorPosY(anchoredPosition.y + _riseDistance * scale, _duration)`

Spawner가 항상 `scale = 1f`를 전달하므로:
- `localScale = Vector3.one * 1f` → 기본 크기 고정
- `_riseDistance * 1f = _riseDistance(80f)` → 이동 거리 고정

줌 레벨과 무관하게 텍스트 크기 및 이동 거리가 항상 동일하게 동작하는 것이 정적 분석으로 확인됨.

### SINGLE TC 정적 판정

| TC | 정적 분석 판정 | 근거 |
|----|--------------|------|
| TC-1 (줌인 텍스트 크기) | CONDITIONAL PASS | scale=1f 고정 코드 확인. 실기 시각 확인 필요 |
| TC-2 (줌아웃 텍스트 크기) | CONDITIONAL PASS | scale=1f 고정 코드 확인. 실기 시각 확인 필요 |
| TC-3 (줌 변경 중 비교) | CONDITIONAL PASS | scale=1f 고정 코드 확인. 실기 3단계 비교 확인 필요 |
| TC-4 (텍스트 위치 유닛 위) | CONDITIONAL PASS | 좌표 변환 경로(월드→스크린→Canvas) 코드 정상. 실기 위치 확인 필요 |
| TC-5 (MULTI 클라이언트) | 에이전트 실기 불가 — 사용자 확인 필요 | 멀티플레이 에디터 단독 실기 불가 |
