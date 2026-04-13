# Testcase: 피격 시 부유 텍스트 (Floating HP Text)

**작업일:** 2026-04-12  
**구현 파일:** `FloatingHpText.cs`, `FloatingHpTextSpawner.cs`  
**Inspector 설정 필요:** FloatingHpText 프리팹 생성 + GameBootstrapper 연결

---

## QA 정적 분석 결과

### FloatingHpText.cs

| 항목 | 결과 |
|------|------|
| `Awake()`에서 `_canvasGroup.blocksRaycasts = false` 설정 | ✅ |
| `Play()` 호출 시 기존 시퀀스 Kill 처리 | ✅ |
| `anchoredPosition` 초기 설정 후 Y 이동 | ✅ |
| `OnComplete` 시 `SetActive(false)` + 콜백 호출 | ✅ |
| `OnDestroy`에서 시퀀스 정리 | ✅ |
| null 체크 (`_text`, `_canvasGroup`, `rt`) | ✅ |

### FloatingHpTextSpawner.cs

| 항목 | 결과 |
|------|------|
| `Initialize()` 호출 필요 구조 (GameBootstrapper에서 주입) | ✅ |
| 초기 풀 크기 10개 사전 생성 | ✅ |
| `AddTo(this)` 구독 자동 해제 | ✅ |
| `worldPos == Vector3.zero` 폴백 처리 | ✅ |
| `Camera.main == null` 폴백 처리 | ✅ |
| `ScreenPointToLocalPointInRectangle` camera=null (Overlay Canvas) | ✅ |
| `SetParent(_canvas.transform, false)` | ✅ |

---

## Inspector 설정 체크리스트 (테스트 전 완료 필요)

```
[x] 1. FloatingHpText 프리팹 생성
        - UI 오브젝트(RectTransform) 생성
        - CanvasGroup 컴포넌트 추가
        - 자식에 TextMeshProUGUI 추가 (폰트: Maplestory Light SDF.asset, 크기: 28, 색상: 흰색)
        - TextMeshProUGUI.RaycastTarget = OFF
        - FloatingHpText 스크립트 추가
        - Inspector에서 _text, _canvasGroup 연결
        - Assets/_Project/Prefabs/UI/ 에 저장 (SetupFloatingHpText 에디터 스크립트로 자동 생성)

[x] 2. 씬에 FloatingHpTextSpawner GameObject 추가
        - 빈 GameObject 생성 (이름: FloatingHpTextSpawner)
        - FloatingHpTextSpawner 스크립트 추가

[x] 3. GameBootstrapper Inspector 연결
        - _floatingHpTextSpawner → 씬의 FloatingHpTextSpawner 오브젝트
        - _floatingHpTextPrefab  → FloatingHpText 프리팹
        - _uiCanvas             → 씬의 [UI] Canvas
```

---

## 테스트 케이스

### [TC-FHT-01] 유닛 피격 시 텍스트 표시

**시나리오:** 적 유닛이 아군 유닛을 공격한다.  
**전제조건:** 게임이 시작되고 유닛이 전투 중이다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 피격 직후 HP 텍스트가 나타나는가 | 피격 유닛 위쪽에 남은 HP 수치 표시 | PASS |
| 2 | 텍스트 내용이 남은 HP인가 | `EntityDamagedEvent.CurrentHp` 값 표시 | PASS |
| 3 | 텍스트 위치가 피격 유닛 머리 위인가 | 유닛 오브젝트 위쪽(Y+80px)에 위치 | PASS |

---

### [TC-FHT-02] 부유 텍스트 애니메이션

**시나리오:** TC-FHT-01 연속으로 관찰.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 텍스트가 위로 이동하는가 | 나타난 위치에서 위로 80px 이동 | PASS |
| 2 | 이동 중 서서히 사라지는가 | alpha 1→0 페이드아웃 동시 진행 | PASS |
| 3 | 총 애니메이션 시간이 약 1.2초인가 | 1.2초 후 완전히 사라짐 | PASS |
| 4 | 이동 속도가 처음에 빠르고 끝에서 느린가 | OutCubic 이징 적용 확인 | PASS |

---

### [TC-FHT-03] 건물 피격 시 텍스트 표시

**시나리오:** 유닛이 건물을 공격한다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 피격 직후 HP 텍스트가 나타나는가 | 피격 건물 위쪽에 남은 HP 수치 표시 | PASS |
| 2 | 텍스트 위치가 건물 위인가 | 건물 오브젝트 위쪽에 위치 | PASS |

---

### [TC-FHT-04] 연속 피격 (풀 재사용)

**시나리오:** 짧은 시간 안에 여러 유닛이 동시에 피격된다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 각 피격 유닛마다 별도 텍스트가 표시되는가 | 피격 수만큼 독립된 텍스트 동시 표시 | PASS |
| 2 | 10개 초과 동시 피격 시 정상 동작하는가 | 추가 Instantiate로 처리, 에러 없음 | PASS |
| 3 | 사라진 텍스트가 다음 피격에 재사용되는가 | 새 Instantiate 없이 풀에서 재사용 | PASS |

---

### [TC-FHT-05] 입력 방해 없음

**시나리오:** 부유 텍스트가 화면에 표시되는 동안 UI 조작을 시도한다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 텍스트가 떠있는 동안 버튼이 눌리는가 | 텍스트 영역 위 버튼/터치 정상 동작 | PASS |
| 2 | 텍스트 위치에서 터치 이벤트가 통과되는가 | RaycastTarget=OFF, blocksRaycasts=false 확인 | PASS |

---

### [TC-FHT-06] 클라이언트 측 텍스트 표시 (멀티플레이)

**시나리오:** 호스트(서버)와 클라이언트가 접속한 상태에서 전투가 발생한다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 서버(호스트) 화면에 텍스트가 표시되는가 | 피격 시 부유 텍스트 정상 표시 | PASS |
| 2 | 클라이언트 화면에도 텍스트가 표시되는가 | NetworkHealthSync 재발행으로 정상 표시 | PASS |
| 3 | 클라이언트에서 표시되는 HP 값이 서버와 동일한가 | 서버 권위 값(`serverHp`) 기준으로 동일 | PASS |
| 4 | 클라이언트에서 중복 텍스트가 표시되지 않는가 | diff>0 조건으로 이미 동기화된 경우 미표시 | PASS |

---

### [TC-FHT-07] 유닛 사망 시 텍스트 처리

**시나리오:** HP가 0 이하가 되어 유닛이 사망하는 순간 피격 이벤트가 발행된다.

| # | 확인 항목 | 기대 결과 | 실제 결과 |
|---|-----------|-----------|-----------|
| 1 | 사망 직전 마지막 타격에 텍스트가 표시되는가 | `CurrentHp=0` 텍스트 정상 표시 | PASS |
| 2 | 사망 처리 후 NullReferenceException이 없는가 | 에러 없음 (worldPos==0 폴백 동작) | PASS |

---

## 실기 테스트 결과

**테스트일:** 2026-04-13  
**결과:** 전 항목 PASS — 작업 완료

---

## 버그 보고 양식

```
[BUG-FHT-XX]
제목:
재현 단계:
기대 결과:
실제 결과:
발생 빈도:
스크린샷/로그:
```
