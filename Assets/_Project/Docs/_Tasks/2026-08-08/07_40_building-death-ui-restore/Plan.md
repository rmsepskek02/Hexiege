# Plan — 건물 파괴 시 열린 패널/조준 UI 원복

## 이 작업이 무엇인지 (자연어 설명)

건물 패널이 열려 있거나(생산/스킬/연구/액션 패널) 스킬 조준 중인 상태에서 **그 건물이 파괴되면**, 지금은 죽은 건물의 패널·조준 UI가 화면에 그대로 남습니다. 실제 발동·철거 같은 동작은 서버 재검증으로 막히므로 게임 상태는 안전하지만, **UI만 원복되지 않는** 시각적 버그입니다.

이 작업은 **파괴된 건물이 현재 표시/조준 중인 건물이면 그 패널을 닫고(스킬이면 조준까지 취소해) 평상시 맵 화면으로 되돌립니다.** 건물 패널 4종(생산/액션/스킬/연구)은 모두 공통 베이스 `BuildingPanelBase`를 상속하므로, **베이스 한 곳에서 사망 이벤트를 구독**하면 4개 패널 전부가 한 번에 커버됩니다. 이번엔 문서만 작성하며, 구현은 승인 후 별도로 진행합니다.

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW [4] 규칙 — 최상단 명시)

**이 작업은 기존 로직을 제거하지 않는다.** 새 구독/핸들러/`OnDestroy` 해제를 **추가**할 뿐이며, 기존 `Show()`/`Close()`/`OnBeforeClose()`/각 패널 정리 로직은 그대로 유지·재사용한다. 따라서 "비활성화(주석 처리) 우선" 절차가 적용될 삭제 대상이 없다.

---

## 대상 4개 패널 (모두 `BuildingPanelBase` 상속)

`ProductionPanelUI`(L35) · `BuildingActionPanelUI`(L34) · `BuildingSkillPanelUI`(L41) · `ResearchPanelUI`(L43).
→ 베이스 1곳 처리로 4종 전부 커버.

---

## 설계 (근거 규칙과 함께)

### 접근: 베이스에서 `OnBuildingDied` 구독 + 현재 건물 Id 매칭 시 `Close()`

1. **구독 등록** — `BuildingPanelBase.InitializeBase(...)`(L129~145, 자식 Initialize에서 1회 호출) 안에서 `GameEvents.OnBuildingDied`를 구독한다.

2. **핸들러** — 대략 다음 판정으로 처리한다(자연어):
   > 죽은 건물이 있고(`e.Building != null`), 이 패널이 현재 어떤 건물을 다루는 중이며(`_currentBuilding != null`), 그 두 건물의 Id가 같으면 → `Close()`를 호출한다.

   즉 `if (e.Building != null && _currentBuilding != null && e.Building.Id == _currentBuilding.Id) Close();`

3. **판정 기준은 `IsOpen`이 아니라 현재 건물 매칭** — 스킬 패널은 조준 진입 시 `_popup.Hide()`를 호출해(`BuildingSkillPanelUI` L322) `IsOpen`(L106)이 조준 중 **false**가 된다. `IsOpen`으로 걸면 조준 중 케이스를 놓치므로, **`_currentBuilding.Id` 매칭**으로 판정한다(Research "중요 특성" 참조). 패널이 닫혀 있으면 `_currentBuilding == null`이라 매칭이 자연 실패해 오작동 없음.

4. **조준 취소는 자동 연계** — `Close()`는 맨 처음 `OnBeforeClose()`를 호출한다(L199). `BuildingSkillPanelUI.OnBeforeClose`(L260~263)가 `_skillAimController.CancelAim()`을 부르므로, 베이스 핸들러가 `Close()`만 불러도 스킬 조준이 취소된다. 별도 조준 취소 코드를 베이스에 넣지 않는다. 마찬가지로 `ProductionPanelUI.OnBeforeClose`(랠리 마커 숨김)도 자동 연계된다.

5. **구독 해제(누수 방지)** — `BuildingPanelBase`에 현재 `OnDestroy`가 없다. 구독을 저장할 `IDisposable`(또는 `CompositeDisposable`) 필드를 두고, **`OnDestroy`를 신설해 해제**한다. `InitializeBase`가 자식별로 1회만 호출되도록 **중복 구독 가드**(이미 구독돼 있으면 재구독하지 않음)를 둔다.

### 근거 규칙 매핑

| 설계 항목 | 근거 |
|-----------|------|
| 배럭 파괴 시 마커/패널 정리 | `GameSystemRules_Buildings.md` 랠리포인트 규칙 2 "배럭 파괴 → 랠리포인트 및 마커 제거", "팝업 닫힘 → 모든 마커 숨김" |
| 스킬 건물 파괴 시 열린 스킬 패널/조준 원복 | `GameSystemRules_Skills.md` 규칙 17~20 및 문서 서두 "건물 파괴 시 열린 스킬 패널/조준 UI 원복도 별도 후속 작업으로 남아 있다"(이 작업이 그 후속) |
| 서버 권위 유지(발동/철거는 서버 재검증) | `GameSystemRules_Skills.md` 규칙 25·26, `GameSystemRules_Buildings.md` 방어 타워 규칙 9 — 본 작업은 **UI 원복만** 담당, 게임 상태 판정은 서버 권위 그대로 |
| BlockingOverlay 단일 소유 / 팝업 닫기 | `GameSystemRules_UI.md` 공통 규칙 5(BlockingOverlay는 UIManager 단일 소유), 규칙 9(팝업 배경 탭 닫기) — `Close()`가 `HideBlockingOverlay()`를 부르므로 오버레이도 함께 정리됨 |

---

## 파일별 변경 내용

### 수정: `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs` (유일한 코드 변경)

- **필드 추가**: 사망 이벤트 구독을 담을 `IDisposable`(예: `_buildingDiedSub`) + 중복 구독 방지 플래그.
- **`InitializeBase`(L129~145)**: 메서드 말미에 `GameEvents.OnBuildingDied` 구독 등록(중복 가드 포함).
- **핸들러 메서드 신설**: `e.Building.Id == _currentBuilding.Id`일 때 `Close()` 호출(위 설계 2·3).
- **`OnDestroy` 신설**: 구독 해제(누수 방지).
- 주석은 유니티 초급자도 이해할 수 있게 상세히(CLAUDE.md 규칙 8): "왜 `IsOpen`이 아니라 현재 건물 Id로 판정하는가(조준 중 팝업 숨김)", "왜 `Close()`만 불러도 조준이 취소되는가(OnBeforeClose 연계)".

### 변경 없음: 각 패널 4종

- `ProductionPanelUI` / `BuildingActionPanelUI` / `BuildingSkillPanelUI` / `ResearchPanelUI` — 기존 `OnBeforeClose`를 그대로 사용. 코드 수정 불필요.
- 특히 `BuildingSkillPanelUI.OnBeforeClose`(조준 취소)와 `ProductionPanelUI.OnBeforeClose`(랠리 마커)는 베이스 `Close()`를 통해 자동 호출된다.

---

## 위험 / 아키텍처 제약 / 조사 필요

### 아키텍처 제약 (준수 확인)

- **레이어 방향**: `BuildingPanelBase`(Presentation) → `GameEvents.OnBuildingDied`(Application) 구독은 정방향으로 허용. Application → Infrastructure 역행 아님. 위반 없음.

### 위험 요소

1. **중복 `Close()` 호출** — 이미 닫힌 패널(`_currentBuilding == null`)에는 매칭이 실패해 재호출되지 않는다. 또 `Close()` 내부는 null-safe(`_popup?.Hide()`, `UIManager.Instance?.HideBlockingOverlay()`). 다만 같은 프레임 다중 발행 등 극단 케이스에서 `ClosedFrame`(L113) 갱신이 겹칠 수 있으므로, 핸들러는 "현재 건물 매칭 시에만" 닫도록 좁혀 재진입을 최소화한다.
2. **구독 시점/해제 정합** — `InitializeBase`가 자식마다 1회 호출임을 전제로 하되, 방어적으로 중복 구독 가드를 둔다. `OnDestroy`에서 반드시 해제(씬 재로드/재경기 반복 시 누수 방지).
3. **`InitializeBase` 미호출 패널** — 4종 모두 Initialize에서 `InitializeBase`를 호출함을 Research에서 확인(스킬 패널 L108 등). 구독을 `InitializeBase`에 두면 초기화된 패널만 구독하므로 안전하다.

### 조사/실기 확인 필요 (구현·QA 단계)

- **멀티 클라 발행 여부 → 확인 완료(위험 해소)**: `NetworkCombatController.HandleBuildingDied`(L1027)가 클라에서 `OnBuildingDied`를 재발행하므로 베이스 구독만으로 싱글/호스트/순수 클라 모두 커버. 추가 despawn 연결 불필요.
- **실기 확인 대상**: (a) 스킬 조준 중 시전 건물이 파괴되면 조준 원이 사라지고 입력 잠금이 남지 않는지, (b) 상대가 내 배럭을 부술 때 생산 패널이 닫히고 랠리 마커가 사라지는지, (c) 연구소 파괴 시 연구 패널이 닫히되 기존 연구 취소·환불(`GameBootstrapper.Setup` L417)과 충돌 없는지.

---

## 변경 예정 파일 목록

```
[수정]
- Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs

[변경 없음 — 기존 OnBeforeClose 재사용]
- Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs
- Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs
- Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs
- Assets/_Project/Scripts/Presentation/UI/ResearchPanelUI.cs

[작업 문서]
- Assets/_Project/Docs/_Tasks/2026-08-08/07_40_building-death-ui-restore/Research.md
- Assets/_Project/Docs/_Tasks/2026-08-08/07_40_building-death-ui-restore/Plan.md
```

> 구현은 `game-programmer` 에이전트에 위임하며, **사용자 승인 후에만** 시작한다(WORKFLOW [4]).

---

## 구현 결과 (2026-08-08, 실기 테스트 PASS · 커밋 `8c7fa01`)

계획대로 **`BuildingPanelBase.cs` 1개 파일만 수정**했고(자식 4개 패널 무변경), 파괴된 건물이 현재 표시/조준 중인 건물이면 베이스가 `Close()`를 호출해 각 패널 `OnBeforeClose`로 스킬 조준 취소·랠리 마커 숨김이 자동 연계되는 것을 실기로 확인했다. **생산/건물액션/스킬/연구 4개 패널 전부 커버**되고, 멀티(순수 클라 포함)도 `NetworkCombatController.HandleBuildingDied`의 로컬 재발행으로 정상 동작.

### 계획 대비 달라진 점 — 구독 해제를 `OnDestroy`+Dispose 대신 `.AddTo(this)`(UniRx)로 변경

- **원래 계획(위 설계 5·파일별 변경)**: `IDisposable` 필드 + 신설 `OnDestroy`에서 Dispose.
- **실제 구현**: 구독을 **`.AddTo(this)`(UniRx)** 로 컴포넌트 수명에 묶어 자동 해제.
- **변경 이유(회귀 회피)**: `ResearchPanelUI`가 **이미 자체 `OnDestroy`를 선언**하고 있어, 베이스에 `OnDestroy`를 신설하면 C# 메서드 **은닉(hide)** 이 발생한다(자식 `OnDestroy`가 베이스 `OnDestroy`를 가려 `base.OnDestroy()` 호출이 없으면 베이스 해제 로직이 실행되지 않음 → 구독 누수 회귀). 이를 피하려 프로젝트에 이미 쓰이는 관용 패턴(`BuildingFactory`/`UnitFactory`/`HitPresentationQueue`의 `.AddTo(this)`)을 채택했다.
- **판정 기준은 계획대로 `IsOpen`이 아닌 `_currentBuilding.Id` 매칭**(스킬 조준 중엔 `_popup.Hide()`로 `IsOpen=false`이므로) — 무변경.

### 실기 확인
- 스킬 조준 중 시전 건물 파괴 → 조준 원 사라짐·입력 잠금 잔존 없음. 생산 건물 파괴 → 생산 패널 닫힘·랠리 마커 사라짐. 연구소 파괴 → 연구 패널 닫힘(기존 연구 취소·환불과 충돌 없음). 모두 PASS.

### 교훈
- **MonoBehaviour 베이스에서 자식이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy`가 은닉(hide)되어 베이스 해제 로직이 누락될 수 있다.** 베이스에서 이벤트 구독을 해제할 때는 신설 `OnDestroy`보다 **`.AddTo(this)`(UniRx)** 로 컴포넌트 수명에 묶는 것이 안전하다.
