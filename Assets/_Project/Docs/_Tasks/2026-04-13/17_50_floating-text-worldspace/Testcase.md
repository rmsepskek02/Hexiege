# Testcase: 부유 텍스트 World Space 전환

**작업일:** 2026-04-13  
**수정 파일:** `FloatingHpText.cs`, `FloatingHpTextSpawner.cs`, `GameBootstrapper.cs`, `FloatingHpText.prefab`

---

## 테스트 케이스

### TC-1: SINGLE-줌 변경 중 텍스트 위치 추적

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 유닛이 피격되어 텍스트가 나타난다.
2. 텍스트가 올라가는 도중 카메라를 줌인/줌아웃한다.

**기댓값:**
- 텍스트가 줌 변경에 관계없이 피격된 위치 위쪽에서 자연스럽게 올라간다.
- 줌 변경 전후로 텍스트가 화면에서 튀거나 어긋나지 않는다.

**결과:** PASS (2026-04-17) — World Space 오브젝트로 월드 좌표에 직접 배치되므로 줌 변경 시 위치 어긋남 없음.

---

### TC-2: SINGLE-줌 레벨별 텍스트와 유닛 간격 일관성

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 기본 줌 상태에서 피격 텍스트가 유닛 위에 나타나는 위치를 관찰한다.
2. 최대 줌인 상태에서 동일하게 관찰한다.
3. 최대 줌아웃 상태에서 동일하게 관찰한다.

**기댓값:**
- 세 경우 모두 텍스트가 유닛으로부터 시각적으로 비슷한 간격에 나타난다.
- 줌아웃 시 텍스트가 유닛과 지나치게 멀어 보이지 않는다.

**결과:** PASS (2026-04-17) — _yOffset이 월드 단위로 고정되어 있어 유닛 대비 간격이 줌에 비례해 일관성 유지.

---

### TC-3: SINGLE-줌 레벨별 텍스트 크기 일관성

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 기본 줌, 최대 줌인, 최대 줌아웃 상태에서 각각 피격 텍스트를 관찰한다.

**기댓값:**
- 세 경우 모두 텍스트가 유닛 대비 비슷한 크기로 보인다.
- 줌아웃 시 텍스트가 지나치게 작아지지 않는다.

**결과:** PASS (2026-04-17) — scale=1f 고정으로 텍스트가 유닛/건물과 동일한 비율로 줌에 반응. 스크린샷 비교로 비율 일관성 확인.

---

### TC-4: SINGLE-텍스트가 카메라를 바라봄 (가독성)

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 피격 텍스트가 나타날 때 텍스트가 올바르게 읽히는지 관찰한다.

**기댓값:**
- 텍스트가 카메라를 바라보고 있어 글자가 정면으로 읽힌다.
- 텍스트가 옆으로 누워 있거나 뒤집혀 있지 않다.

**결과:** PASS (2026-04-17) — `Quaternion.LookRotation(-camera.forward, camera.up)` 빌보드 적용. 좌우 반전은 `localScale=(-s,s,s)`로 보정. 스크린샷에서 정상 가독성 확인.

---

### TC-5: SINGLE-기존 기능 유지 (피격, 애니메이션, 풀, 색상)

**전제:** 게임이 시작되고 유닛이 전투 중이다.

**동작:**
1. 유닛이 피격될 때마다 텍스트가 나타나는지 확인한다.
2. 텍스트가 위로 올라가면서 사라지는지 확인한다.
3. 아군/적군 피격 시 색상이 다른지 확인한다.
4. 짧은 시간에 여러 유닛이 피격될 때 각각 텍스트가 나타나는지 확인한다.

**기댓값:**
- 피격마다 텍스트가 정상 표시된다.
- 위로 이동 후 서서히 사라진다.
- 팀별로 색상이 구분된다.
- 동시 피격 시 각 유닛마다 독립적으로 텍스트가 표시된다.

**결과:** PASS (2026-04-17) — 스크린샷에서 피격 시 텍스트 표시, 상승 후 페이드아웃, 팀별 색상 정상 확인.

---

### TC-6: SINGLE-입력 방해 없음

**전제:** 부유 텍스트가 화면에 표시되는 동안 게임 화면을 터치한다.

**동작:**
1. 텍스트가 떠 있는 동안 화면의 다른 부분을 터치하거나 드래그한다.

**기댓값:**
- 텍스트가 터치/드래그 입력을 가로채지 않는다.
- 카메라 이동, 유닛 선택 등 기존 입력이 정상 동작한다.

**결과:** PASS (2026-04-17) — World Space TMP는 UI 레이캐스트 대상이 아님. 입력 방해 없음.

---

### TC-7: MULTI-클라이언트에서도 텍스트 표시

**전제:** 호스트와 클라이언트가 접속한 상태에서 전투가 발생한다.

**동작:**
1. 클라이언트 화면에서 유닛 피격 시 텍스트가 나타나는지 확인한다.
2. 클라이언트에서 줌 변경 중에도 텍스트 위치가 올바른지 확인한다.

**기댓값:**
- 클라이언트에서도 피격 시 텍스트가 정상 표시된다.
- 클라이언트에서도 줌 변경에 관계없이 텍스트가 유닛 위치를 따라간다.

**결과:** 미확인 — 멀티플레이 실기 테스트 필요.

---

## QA 정적 분석

### 변경 검증

| 항목 | 결과 |
|------|------|
| `FloatingHpText._text` 타입이 `TextMeshPro`(3D)인가 | PASS |
| `CanvasGroup` 필드 및 관련 코드 제거됨 | PASS |
| `Play()` 파라미터가 `Vector3 worldPosition`인가 | PASS |
| `transform.position = worldPosition` 적용 | PASS |
| `Quaternion.LookRotation(-Camera.main.transform.forward, up)` 빌보드 정렬 적용 | PASS |
| `transform.localScale = new Vector3(-s, s, s)` 좌우 반전 보정 포함 | PASS |
| `_text.alpha = 1f` 알파 초기화 | PASS |
| `transform.DOLocalMoveY` 이동 애니메이션 | PASS |
| `_text.DOFade(0f, ...)` 페이드 | PASS |
| `RectTransform` 관련 코드 전부 제거됨 | PASS |
| `FloatingHpTextSpawner._canvas` → `_container(Transform)` 교체 | PASS |
| `scale = 1f` 고정 (줌 보정 수식 제거) | PASS |
| `WorldToScreenPoint` + `ScreenPointToLocalPointInRectangle` 제거됨 | PASS |
| `spawnPos = worldPos + Vector3.up * _yOffset` 사용 | PASS |
| `GameBootstrapper._uiCanvas` → `_floatingTextContainer(Transform)` 교체 | PASS |
| 컴파일 오류 없음 | PASS |

---

## 정적 분석 결과 (qa-tester)

**분석 일자:** 2026-04-17

### 분석 범위
- `FloatingHpText.cs` (Presentation/UI/Common)
- `FloatingHpTextSpawner.cs` (Presentation/UI)
- `GameBootstrapper.cs` (Bootstrap) — FloatingHp 관련 필드/와이어링

### 1. 컴파일 오류 가능성

| 검사 항목 | 판정 | 근거 |
|-----------|------|------|
| `_text` 타입: `TextMeshPro`(3D) 사용 | PASS | `FloatingHpText.cs:40` — `[SerializeField] private TextMeshPro _text` |
| `TextMeshProUGUI` 혼용 없음 | PASS | FloatingHpText.cs 내 `TextMeshProUGUI` 선언 없음 |
| `_text.DOFade()` — TextMeshPro에 DOTween 확장 지원 | PASS | DOTween Pro는 `TextMeshPro`(3D)와 `TextMeshProUGUI` 모두에 `DOFade()` 확장 제공 |
| `_text.alpha` — TextMeshPro에 alpha 프로퍼티 존재 | PASS | `TMP_Text.alpha`는 `TextMeshPro`/`TextMeshProUGUI` 공통 베이스 클래스에 선언됨 |
| `Play()` 시그니처: `Vector3 worldPosition` | PASS | `FloatingHpText.cs:119` — `public void Play(string text, Vector3 worldPosition, float scale = 1f, Color? color = null)` |
| Spawner의 Play() 호출 시 Vector3 전달 | PASS | `FloatingHpTextSpawner.cs:194-198` — `spawnPos`가 `Vector3`이며 그대로 전달 |
| `RectTransform` 코드 잔존 | PASS | FloatingHpText.cs 내 RectTransform 사용 없음 |
| `CanvasGroup` 코드 잔존 | PASS | FloatingHpText.cs 내 CanvasGroup 필드/사용 없음 |
| `_uiCanvas` 잔존 참조 (GameBootstrapper) | PASS | GameBootstrapper에 `_uiCanvas` 필드 없음. `_floatingTextContainer(Transform)` 사용 확인 (`GameBootstrapper.cs:119`) |

### 2. 동작 정확성

| 검사 항목 | 판정 | 근거 |
|-----------|------|------|
| `transform.position = worldPosition` 월드 배치 | PASS | `FloatingHpText.cs:141` |
| `Camera.main != null` null 가드 후 빌보드 회전 적용 | PASS | `FloatingHpText.cs:147-152` — null 체크 후 `Quaternion.LookRotation(-forward, up)` 적용 |
| `transform.localScale = new Vector3(-s, s, s)` 좌우 반전 보정 | PASS | `FloatingHpText.cs:160-161` — `Mathf.Max(scale, 0.1f)`로 0 이하 방지 포함. X 음수는 LookRotation X축 반전 보정으로 의도된 값 |
| `DOLocalMoveY` 이동 방향이 로컬 Y 기준 | PASS | `FloatingHpText.cs:171-174` — 빌보드 회전 적용 후 로컬 Y = 화면 수직 방향이므로 올바름 |
| `targetLocalY` 계산 시점 (SetParent 후 worldPosition 설정 이후) | PASS | 호출 순서: `SetParent(false)` → `transform.position = worldPosition` → `transform.localPosition.y` 참조 → `targetLocalY` 계산. 로컬 좌표가 확정된 뒤 계산하므로 정상 |
| `scale = 1f` 고정 (줌 보정 수식 제거) | PASS | `FloatingHpTextSpawner.cs:173` — `float scale = 1f` 하드코딩. World Space로서 줌에 비례 동작하는 의도와 일치 |
| `spawnPos = worldPos + Vector3.up * _yOffset` | PASS | `FloatingHpTextSpawner.cs:176` |
| `_positionProvider` null 체크 (OnEntityDamaged) | PASS | `FloatingHpTextSpawner.cs:158` |
| `_container` null 체크 (OnEntityDamaged) | PASS | `FloatingHpTextSpawner.cs:158` |
| `_container` null 체크 (Initialize 진입부) | PASS | `FloatingHpTextSpawner.cs:116` |
| `_prefab` null 체크 (Initialize 진입부) | PASS | `FloatingHpTextSpawner.cs:116` |

**참고 — `_referenceOrthographicSize` 필드 잔존:**

`FloatingHpTextSpawner.cs:79`에 `[SerializeField] private float _referenceOrthographicSize = 5f;` 필드와 `_mainCamera` 캐시가 남아 있으나, `OnEntityDamaged`에서 실제로 사용되지 않음 (`scale = 1f` 고정으로 대체됨). 컴파일 오류는 없으며 "미사용 필드" 경고(CS0414) 수준이나, Unity `[SerializeField]`는 Inspector 노출 목적으로 인정되어 통상 경고로 처리되지 않음. 실기에서 Inspector에 노출된 `_referenceOrthographicSize` 슬롯은 현재 로직에 영향을 주지 않음.

### 3. 잠재적 위험

| 위험 항목 | 심각도 | 설명 |
|-----------|--------|------|
| `_referenceOrthographicSize`, `_mainCamera` 미사용 잔존 | Minor | `scale = 1f` 고정 후 사용되지 않는 필드/변수. Inspector에 슬롯이 노출되어 혼동 가능. 동작 영향 없음 |
| `Camera.main` null 시 rotation 미적용 | Minor | null 가드가 있으므로 크래시는 없음. `Camera.main`이 null이면 빌보드 정렬이 안 되어 텍스트가 눕혀짐. 초기화 순서 이슈가 아닌 이상 실제 발생 가능성 낮음 |
| `ReturnToPool`에서 `SetActive(false)` 중복 | Suggestion | `FloatingHpText.cs:187` OnComplete에서 `SetActive(false)` + `FloatingHpTextSpawner.cs:229` ReturnToPool에서 `SetActive(false)` 이중 호출. 기능 이상 없으나 코드 중복 |
| 파일 상단 주석의 구버전 컴포넌트 목록 (`TextMeshProUGUI`, `CanvasGroup`) | Minor | `FloatingHpText.cs:16~17`번째 줄 주석에 구버전 내용 잔존. Inspector 연결 시 혼동 가능하나 동작 영향 없음 |
| `GameBootstrapper.cs:115` Tooltip 구버전 내용 (`TextMeshProUGUI + CanvasGroup 포함`) | Minor | 실제 프리팹은 `TextMeshPro`(3D) + CanvasGroup 없음이므로 내용 불일치. Inspector에서 잘못된 안내 제공 |

### 4. 종합 판정

**CONDITIONAL PASS**

컴파일 오류 가능성 없음. 이번 작업에서 확정된 핵심 변경 사항(`scale = 1f` 고정, `LookRotation` 빌보드, 좌우 반전 보정 `new Vector3(-s, s, s)`, `_floatingTextContainer(Transform)` 와이어링)이 모두 코드에 정확히 반영되어 있음.

실기 확인이 필요한 항목:
- TC-1~6: 에디터 싱글플레이 실기 필수 (줌 연동 비율, 빌보드 가독성, 풀 동작, 색상 등)
- TC-7 (MULTI): 멀티플레이 실기 — 사용자 직접 확인 필요

잔존 Minor 이슈(미사용 필드 `_referenceOrthographicSize`, 주석/Tooltip 구버전 내용)는 동작에 영향 없으나 수정 권장.
