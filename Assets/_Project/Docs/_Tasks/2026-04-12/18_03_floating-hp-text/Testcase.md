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

---

### [TC-FHT-08] Blue 팀 피격 시 파란색 텍스트 표시 (2026-04-13 추가)

**전제:** 게임이 시작되고 Blue 팀 유닛 또는 건물이 전투 중이다.

**동작:**
1. Red 팀 유닛이 Blue 팀 유닛(또는 건물)을 공격한다.

**기댓값:**
- Blue 팀 유닛 머리 위에 나타나는 부유 텍스트가 파란색으로 표시된다.

**결과:** PASS

---

### [TC-FHT-09] Red 팀 피격 시 빨간색 텍스트 표시 (2026-04-13 추가)

**전제:** 게임이 시작되고 Red 팀 유닛 또는 건물이 전투 중이다.

**동작:**
1. Blue 팀 유닛이 Red 팀 유닛(또는 건물)을 공격한다.

**기댓값:**
- Red 팀 유닛 머리 위에 나타나는 부유 텍스트가 빨간색으로 표시된다.

**결과:** PASS

---

## 실기 테스트 결과 (초기 구현)

**테스트일:** 2026-04-13  
**결과:** TC-FHT-01 ~ TC-FHT-07 전 항목 PASS — 초기 구현 완료

## 실기 테스트 결과 (팀별 색상 추가)

**테스트일:** 2026-04-13  
**결과:** TC-FHT-08, TC-FHT-09 PASS — 연두색/노란색 정상 표시 확인. Inspector SerializedField로 색상 조정 가능하도록 추가 개선.

---

## 정적 분석 결과 — 팀별 색상 (2026-04-13 qa-tester)

### 1. Play() 시그니처 변경 — 기존 호출부 영향 전수 검색

**검색 범위:** `Assets/_Project/Scripts` 전체 `.cs` 파일에서 `hpText.Play(` 패턴 검색

**결과:** 호출부 1개 — `FloatingHpTextSpawner.cs` 201~205라인

```
hpText.Play(
    $"{evt.CurrentHp}",
    localPoint + new Vector2(0f, YOffset * scale),
    scale,
    textColor);   // ← 신규 4번째 인자 Color 전달
```

`Play(string, Vector2, float, Color?)` 시그니처 완전 일치.

`UnitView.cs`에 등장하는 `.Play(` 는 `Animator.Play()` 호출로, `FloatingHpText.Play()`와 무관.

**판정: ✅ 컴파일 영향 없음 — 기존 호출부 없음, 신규 호출부 타입 일치**

---

### 2. using Hexiege.Domain 추가 여부

**검색 결과:** `FloatingHpTextSpawner.cs` 27라인에 `using Hexiege.Domain;` 존재 확인.

`TeamId`는 `Hexiege.Domain` 네임스페이스에 정의된 enum(`TeamId.cs`). `FloatingHpTextSpawner`가 `TeamId`를 switch 패턴에 사용하므로 해당 using이 필수이며, 올바르게 추가되어 있음.

**판정: ✅ 정상**

---

### 3. TeamId switch 패턴 컴파일 가능 여부

**코드 (193~198라인):**
```csharp
TeamId team = evt.Entity.Team;
Color textColor = team switch
{
    TeamId.Blue => new Color(77f / 255f, 128f / 255f, 230f / 255f),
    TeamId.Red  => new Color(230f / 255f, 77f / 255f, 77f / 255f),
    _           => Color.white
};
```

확인 항목:
- `TeamId` enum 멤버: `Neutral = 0`, `Blue = 1`, `Red = 2` — switch 케이스 `Blue`, `Red` 모두 유효한 멤버.
- 기본 케이스(`_`)가 있어 `Neutral` 및 미래 추가 멤버 처리 보장 → 컴파일러 exhaustive 경고 없음.
- C# switch expression 문법 (C# 8.0 이상) — Unity 6는 C# 9.0 지원, 사용 가능.
- `new Color(float, float, float)` 생성자: 유효, RGB를 0~1 범위로 정규화하여 전달.

**판정: ✅ 컴파일 가능, 모든 케이스 처리됨**

---

### 4. null 병합 연산자(color ?? Color.white) 동작 검증

**코드 (FloatingHpText.cs 144라인):**
```csharp
_text.color = color ?? Color.white;
```

- `color`의 타입: `Color?` (Nullable\<Color\>)
- `??` 연산자: 좌측이 `null`이면 우측 값을 사용 → `Color.white` 적용
- `FloatingHpTextSpawner`는 항상 `textColor` (Color, non-nullable)를 전달하므로 `null`이 될 여지 없음
- 단, 다른 호출자가 `color` 인자를 생략하거나 `null`을 명시할 경우에도 흰색으로 안전하게 처리됨

**판정: ✅ 정상 — null 안전, 기존 호환성 유지**

---

### 5. 오브젝트 풀 재사용 시 색상 초기화 여부

**우려 사항:** 이전 Play() 호출에서 설정한 색상이 다음 재사용 시 남아있을 가능성.

**분석:**
- `Play()` 내부 144라인: `_text.color = color ?? Color.white;` — `Play()` 호출 시마다 색상을 덮어씀.
- 풀에서 꺼낸 직후 `Play()` 호출이 선행되므로, 이전 색상이 그대로 노출되는 프레임 없음.
- `ReturnToPool()`(264~267라인): `SetActive(false)` 후 풀 Enqueue → 색상 값을 따로 초기화하지 않지만, 다음 `Play()` 호출 전까지 오브젝트가 비활성 상태이므로 시각적 영향 없음.

**결론:** 색상 잔류가 화면에 노출되는 경우는 없음. `Play()` 최상단에서 색상 설정이 일어나므로 안전.

**판정: ✅ 정상 — 풀 재사용 시 이전 색상 노출 없음**

---

### 종합 판정

| 항목 | 결과 |
|------|------|
| Play() 호출부 전수 검색 — 타입 불일치 없음 | ✅ |
| using Hexiege.Domain 추가 확인 | ✅ |
| TeamId switch 패턴 컴파일 가능 | ✅ |
| null 병합 연산자 동작 정상 | ✅ |
| 오브젝트 풀 재사용 시 색상 초기화 안전 | ✅ |

**정적 분석: PASS — 컴파일 에러 및 런타임 버그 위험 없음. 실기 테스트(TC-FHT-08, TC-FHT-09)로 진행 가능.**

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
