# Plan — 유닛 생산 실패 피드백 시스템

> **이 작업이 하려는 것:**
> 유닛 생산 버튼을 눌렀을 때 골드가 없거나 인구수가 가득 차거나 큐가 꽉 찬 경우, 현재는 아무 반응이 없다.
> 이 작업에서는 실패 원인을 즉각적으로 알 수 있도록 골드/인구 텍스트 색상을 빨간색으로 바꾸고 토스트 메시지를 표시한다.
> 자동 생산 중 자원이 부족하면 재시도 없이 즉시 자동 생산을 취소한다.

---

> **GameSystemRules.md 검토 결과:**
> 현재 GameSystemRules.md에는 유닛 이동/전투 규칙만 있으며, 생산 피드백과 관련된 규칙은 없다.
> 이 작업은 UI 피드백 레이어에 한정되므로 기존 시스템 규칙과 충돌 없음.

---

## 구현 목표

| 상황 | UI 효과 | 토스트 메시지 |
|------|---------|-----------|
| 골드 부족 (수동 생산 시도) | 각 유닛 생산 비용 텍스트(`_unitCostTexts`) 개별 빨간색 | O ("골드가 부족합니다") |
| 인구 초과 (수동 생산 시도) | HUD 상단 `_populationText` 빨간색 | O ("인구수가 가득 찼습니다") |
| 큐 3개 초과 | 없음 | O ("생산 대기열이 가득 찼습니다") |
| 자동 생산 중 자원 부족 | 없음 | X (자동 생산 즉시 취소) |

---

## 구현 계획

### [1] 토스트 시스템 신규 구현

**신규 파일:**
- `Assets/_Project/Scripts/Presentation/UI/Common/ToastUI.cs` — 토스트 표시/페이드아웃/터치 제거 컴포넌트
- `Assets/_Project/Scripts/Presentation/UI/Common/ToastSpawner.cs` — 큐 관리 및 싱글턴 접근 진입점
- `Assets/_Project/Resources/Config/ToastMessageConfig.asset` (ScriptableObject) — 메시지 텍스트 및 노출 시간 설정

---

#### 1-1. ToastMessageConfig (ScriptableObject)

메시지 텍스트와 노출 시간을 Inspector에서 직접 편집할 수 있도록 ScriptableObject로 관리.

```
[메시지 목록]
┌──────────────────────────────────────────────────┐
│ Key: GoldInsufficient                            │
│ 메시지: "골드가 부족합니다"                         │
│ 노출 시간: 2.0초                                  │
├──────────────────────────────────────────────────┤
│ Key: PopulationFull                              │
│ 메시지: "인구수가 가득 찼습니다"                    │
│ 노출 시간: 2.0초                                  │
├──────────────────────────────────────────────────┤
│ Key: ProductionQueueFull                         │
│ 메시지: "생산 대기열이 가득 찼습니다"                │
│ 노출 시간: 1.5초                                  │
└──────────────────────────────────────────────────┘
```

- `ToastKey` enum으로 각 메시지를 식별 (코드에 문자열 하드코딩 없음)
- 향후 메시지 추가 시 ScriptableObject에만 항목 추가 → 코드 수정 불필요
- `ToastMessageConfig`는 `Resources/Config/` 경로에 배치 → `Resources.Load()`로 접근

#### 1-2. 호출 방식

```
ToastSpawner.Show(ToastKey.GoldInsufficient)
```
→ ScriptableObject에서 해당 Key의 메시지 텍스트와 노출 시간을 자동으로 가져와 표시

#### 1-3. 동작 규칙

**표시 위치:** 화면 중앙 고정 *(초기 기획은 하단 중앙이었으나 사용자가 인게임에서 화면 중앙으로 변경)*

**스택 방식 큐:**
- 동시에 여러 요청이 오면 큐에 순서대로 쌓아 차례로 표시
- 각 메시지는 설정된 노출 시간 동안 표시 (최소 1초 보장)
- 현재 메시지 노출이 끝나면 자동으로 다음 메시지 표시

**터치 제거:**
- 현재 표시 중인 메시지를 터치하면 해당 메시지만 즉시 제거
- 큐에 다음 메시지가 있으면 곧바로 다음 메시지 표시
- 1초 미만이어도 터치 시 즉시 제거

**애니메이션:**
- 진입: 즉시 표시 (애니메이션 없음)
- 퇴장: DOTween 페이드아웃

**씬 배치:** `[UI] Canvas` 하위에 배치. `GameBootstrapper`에서 `Initialize()` 연결.

---

### [2] ProductionPanelUI 수정

**파일:** [ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs)

**변경 내용:**

#### 2-1. `OnUnitTap()` — 반환값 활용 및 피드백 분기 (line 207~219)

```
현재:
    _production.EnqueueUnit(_currentBarracks.Id, type);  // 반환값 무시

변경:
    bool success = _production.EnqueueUnit(_currentBarracks.Id, type, out ProductionFailReason reason);
    if (!success) HandleProductionFail(reason);
```

> **주의:** `EnqueueUnit()`에 `out ProductionFailReason` 파라미터를 추가하는 방식이 아니라,
> `EnqueueUnit()`의 실패 원인을 UI가 직접 판단하는 방식도 가능.
> 어느 방식을 택할지는 game-programmer 에이전트가 결정.

#### 2-2. `HandleProductionFail()` 신규 메서드 추가

실패 원인별 처리:
- **골드 부족**: 토스트만 표시 (`ToastUI.Show(ToastKey.GoldInsufficient)`) — 비용 텍스트 색상은 `UpdateInfoBar()`가 자동 관리
- **인구 초과**: HUD의 인구 텍스트 빨간색 요청 + `ToastUI.Show(ToastKey.PopulationFull)`
- **큐 초과**: `ToastUI.Show(ToastKey.ProductionQueueFull)`

#### 2-3. `UpdateInfoBar()` — 유닛 생산 비용 텍스트 색상 로직 추가

**`_goldText`(보유 골드 표시)의 색상은 변경하지 않는다.**

대신 각 유닛 버튼의 생산 비용 텍스트(`_unitCostTexts[i]`)를 개별적으로 평가:
- 보유 골드 < 해당 유닛 비용 → 해당 유닛의 비용 텍스트를 **빨간색**
- 보유 골드 >= 해당 유닛 비용 → 해당 유닛의 비용 텍스트를 **흰색** (자동 복구)
- 골드 변경 이벤트(`OnResourceChanged`) 구독 시 갱신 → 실시간으로 모든 유닛 버튼에 반영

---

### [3] GameHudUI 수정

**파일:** [GameHudUI.cs](Assets/_Project/Scripts/Presentation/UI/GameHudUI.cs)

**변경 내용:**

#### 3-1. `UpdateDisplay()` — 인구수 텍스트 색상 조건 추가 (line 152~160)

```
현재:
    _populationText.text = $"{used} / {max}";

변경:
    _populationText.text = $"{used} / {max}";
    _populationText.color = (used >= max) ? Color.red : Color.white;
```

매 프레임 폴링 방식이라 별도 이벤트 구독 불필요. 값 변경 시에만 텍스트를 업데이트하는 기존 캐시 로직에 색상 갱신도 포함.

#### 3-2. 인구 초과 시 ProductionPanelUI에서 HUD 빨간색 트리거 문제

ProductionPanelUI에서 "인구 초과" 실패를 받았을 때 HUD의 `_populationText`를 빨간색으로 만들 방법 필요.
- **방안 A**: GameHudUI.cs 자체가 매 프레임 `used >= max` 여부를 확인해 색상 유지 → ProductionPanelUI가 직접 HUD를 참조할 필요 없음 ✓ **(권장)**
- **방안 B**: 이벤트를 통해 ProductionPanelUI → GameHudUI로 신호 전송

방안 A가 레이어 의존성 없이 깔끔하므로 채택.

---

### [4] UnitProductionUseCase 수정 — 자동 생산 실패 즉시 취소

**파일:** [UnitProductionUseCase.cs](Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs)

**변경 대상:** `TryStartNext()` 내 두 곳

#### 4-1. PendingQueue 미차감 자동 항목 (line 506~521)

```
현재:
    if (!_resource.CanAfford() || !_population.HasPopulation())
    {
        state.PendingQueue.Insert(0, slot);  // 다시 앞에 넣고 대기
        return;
    }

변경:
    if (!_resource.CanAfford() || !_population.HasPopulation())
    {
        // 자원 부족 → 해당 자동 항목 취소 (ToggleAutoProduction 취소 경로 활용)
        CancelAutoSlotOnResourceFail(state, slot);
        return;
    }
```

#### 4-2. AutoTypes 직접 순환 (line 540~554)

```
현재:
    if (!_resource.CanAfford() || !_population.HasPopulation())
    {
        return;  // 조용히 재시도 대기
    }

변경:
    if (!_resource.CanAfford() || !_population.HasPopulation())
    {
        // 자원 부족 → 자동 생산 전체 취소
        CancelAllAutoProduction(state);
        return;
    }
```

#### 4-3. `CancelAutoSlotOnResourceFail()` / `CancelAllAutoProduction()` 신규 private 메서드

- `AutoTypes`에서 해당 타입 제거
- `PendingQueue`에서 관련 미차감 자동 항목 제거
- `OnProductionQueueChanged` 이벤트 발행

> **중요:** IsCharged=true인 자동 항목(이미 골드 차감된 항목)은 Rule 2에 따라 수동으로 이관되어 생산은 계속됨. 취소 대상은 IsCharged=false(미차감) 자동 항목만.

---

## 멀티플레이 적용 범위

이번 작업은 **싱글플레이 + 오프라인 경로**만 적용한다.

- `ProductionPanelUI.OnUnitTap()`의 싱글플레이 분기(`_production.EnqueueUnit()` 직접 호출)에만 피드백 적용
- 멀티플레이 분기(`RequestEnqueueServerRpc`)는 이번 작업 범위 밖 — 별도 작업으로 처리

---

## 위험 요소

| 위험 | 설명 | 대응 |
|------|------|------|
| `TryStartNext()` 자동 취소 로직 | 기존 Rule 2 (IsCharged=true → 수동 이관) 충돌 가능 | IsCharged=false만 취소, IsCharged=true는 Rule 2 그대로 유지 |
| 골드 텍스트 빨간색 복구 타이밍 | 골드 조금 생겨도 빨간색 유지될 수 있음 | `UpdateInfoBar()`에서 매 골드 변경 시 조건 재평가 |
| 토스트 동시 표시 | 빠르게 여러 번 탭 시 토스트 누적 | 스택 큐 방식으로 순서 보장, 각 메시지 최소 1초 노출 후 다음으로 진행 |
| HUD 인구 텍스트 색상 | 매 프레임 `Color` 객체 할당 | 조건이 변경될 때만 설정 (캐시값과 비교) |
