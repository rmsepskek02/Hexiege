# Research — 유닛 생산 실패 피드백 시스템

> **이 작업의 목적:**
> 유닛 생산이 실패했을 때(골드 부족, 인구수 초과, 큐 가득 참) 플레이어가 아무 반응 없이 묵묵히 실패하는 현재 상태를 개선한다.
> 실패 원인을 즉각적으로 시각/텍스트로 알려줘서 플레이어가 "왜 생산이 안 되지?"라는 혼란을 겪지 않도록 한다.

---

## 1. 현재 생산 실패 처리 방식

### 1-1. 수동 생산 (버튼 탭)

**파일:** [UnitProductionUseCase.cs](Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs) — `EnqueueUnit()` (line 130~154)

조건 체크 순서:
1. 큐 3개 초과 → `return false`
2. 골드 부족 (`CanAfford`) → `return false`
3. 인구 초과 (`HasPopulation`) → `return false`

**문제점:**
[ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs) `OnUnitTap()` (line 207~219)에서 `EnqueueUnit()`의 반환값 `bool`을 **완전히 무시**한다.
```
_production.EnqueueUnit(_currentBarracks.Id, type);  // false 반환해도 아무것도 안 함
```
플레이어는 버튼을 눌러도 아무 반응이 없어서 왜 생산이 안 되는지 알 수 없다.

### 1-2. 자동 생산 실패

**파일:** `UnitProductionUseCase.cs` — `TryStartNext()` (line 497~573)

- **PendingQueue의 미차감 자동 항목** (line 511~517): 자원 부족 시 큐 앞에 다시 넣고 다음 Tick 재시도 → 영구 대기
- **AutoTypes 직접 순환** (line 550~554): 자원 부족 시 조용히 `return` → 다음 Tick 재시도 → 골드가 생길 때까지 계속 재시도

현재 구조에서 자동 생산은 "자원이 생기면 언젠가 생산된다"는 방식인데, 사용자 요구에 따라 **자원 부족 시 자동 생산을 즉시 취소**하도록 변경 예정.

---

## 2. UI 구성 파악

### 2-1. ProductionPanelUI — 생산 패널

**파일:** [ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs)

| 필드 | 타입 | 역할 |
|------|------|------|
| `_goldText` | `TextMeshProUGUI` | 생산 패널 내 골드 표시 (line 55) |
| `_populationText` | `TextMeshProUGUI` | 생산 패널 내 인구 표시 (line 56) |

**현재 갱신 시점:** `UpdateInfoBar()` (line 284~289)
- `GameEvents.OnResourceChanged` 이벤트 구독 → 골드 변경 시 자동 갱신 (line 104)
- `GameEvents.OnProductionQueueChanged` 구독 → 큐 변경 시 갱신 (line 103)

**텍스트 색상 변경 관련:**
- 현재 색상 변경 로직 전혀 없음 — 단순히 `.text` 값만 바꿈
- `TextMeshProUGUI.color` 프로퍼티로 빨간색 전환 가능

### 2-2. GameHudUI — 상단 HUD

**파일:** [GameHudUI.cs](Assets/_Project/Scripts/Presentation/UI/GameHudUI.cs)

| 필드 | 타입 | 역할 |
|------|------|------|
| `_goldText` | `TextMeshProUGUI` | 상단 HUD 골드 텍스트 (line 43) |
| `_populationText` | `TextMeshProUGUI` | 상단 HUD 인구수 텍스트 (line 46) |

**현재 갱신 방식:** `Update()` → `UpdateDisplay()` 매 프레임 폴링 (line 129~133)
- 값이 바뀐 경우에만 텍스트 업데이트 (캐시값 비교 방식)
- 인구수 텍스트는 `$"{used} / {max}"` 형식 (line 160)

**인구수 텍스트 빨간색 전환 요건:**
- `used >= max` 일 때 → 빨간색
- `used < max` 일 때 → 기본 흰색 복구

현재 인구 초과 시 색상 변경 로직 없음.

---

## 3. 토스트 메시지 시스템

**현재 상태: 토스트 시스템 없음**

프로젝트 내 Toast/Notification 관련 파일 전혀 없음. 신규 구현 필요.

**참고 가능한 기존 UI 패턴:**
- [FloatingHpText.cs](Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs) — 데미지 숫자가 떠오르는 방식
- [FloatingHpTextSpawner.cs](Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs) — 위 컴포넌트의 스포너
- [AnimatedPanel.cs](Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs) — DOTween 기반 패널 애니메이션

**토스트 구현 방향:**
- 화면 중앙 하단에 짧은 문자열을 표시했다가 페이드 아웃
- DOTween으로 구현 (프로젝트 기존 스타일과 일치)
- `GameUIManager`에 등록 가능한 MonoBehaviour로 작성

---

## 4. 멀티플레이 고려 사항

**파일:** `ProductionPanelUI.cs` — `OnUnitTap()` (line 207~219)

멀티플레이 모드에서는 `_networkProductionController.RequestEnqueueServerRpc()`를 호출하고 **서버에서 실제 처리**한다.
- 클라이언트 측에서는 실패 여부를 즉시 알 수 없음 (서버 응답 대기 구조)
- 현재 단일 실패 피드백 구현은 **싱글플레이 경로**(`_production.EnqueueUnit()`)에만 해당

멀티플레이 피드백은 별도 작업이 필요할 수 있으므로, 이번 작업은 **싱글플레이 + 오프라인 경로 우선** 처리.

> 단, 멀티플레이에서도 버튼 탭 시 클라이언트 측에서 **미리 조건 체크**를 하여 피드백을 줄 수 있는 구조로 설계 가능 (서버에는 그대로 RPC 전송, 클라이언트는 체크 결과로만 피드백).

---

## 5. 생산 실패 상황별 정리

| 상황 | 감지 위치 | 현재 처리 | 원하는 처리 |
|------|-----------|-----------|-----------|
| 골드 부족 (수동) | `EnqueueUnit()` line 139 | `return false` (UI 무시) | `_goldText` 빨간색 + 토스트 |
| 인구 초과 (수동) | `EnqueueUnit()` line 142 | `return false` (UI 무시) | HUD 인구 텍스트 빨간색 + 토스트 |
| 큐 3개 초과 | `EnqueueUnit()` line 134 | `return false` (UI 무시) | 토스트만 |
| 골드/인구 부족 (자동) | `TryStartNext()` line 511~554 | 조용히 재시도 | 자동 생산 즉시 취소 (토스트 없음) |

---

## 6. 관련 파일 목록

| 파일 | 수정 여부 | 이유 |
|------|-----------|------|
| [ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs) | **수정** | `EnqueueUnit()` 반환값 처리 + 골드 텍스트 색상 변경 + 토스트 호출 |
| [GameHudUI.cs](Assets/_Project/Scripts/Presentation/UI/GameHudUI.cs) | **수정** | 인구수 초과 시 텍스트 빨간색 전환 로직 추가 |
| [UnitProductionUseCase.cs](Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs) | **수정** | 자동 생산 실패 시 즉시 취소 로직 변경 |
| Toast UI 신규 파일 | **신규 생성** | 프로젝트에 토스트 시스템 없어 신규 구현 필요 |
