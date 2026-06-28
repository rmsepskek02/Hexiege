# Plan: 인게임 설정 - 게임포기 확인팝업 가림(Z-order) 버그 수정

## 1. 한눈에 보는 설명 (자연어)

게임 중 설정 창에서 "게임포기"를 누르면 "정말 포기하시겠습니까?"라는 확인 창
(ConfirmPopup)이 떠야 합니다. 그런데 지금은 이 확인 창이 설정 창보다 **뒤쪽에 그려져서**
설정 창에 가려 보이지 않습니다. 사용자는 포기 버튼을 눌렀는데 아무 일도 안 일어난 것처럼
느끼게 됩니다.

원인은 단순합니다. 화면에 겹쳐 그려지는 UI는 "그리는 순서 번호(SortingOrder)"가 클수록
앞(위)에 그려지는데, **설정 창 본체는 자기만의 번호 200을 가진 반면, 확인 창은 자기 번호가
없어서 부모(UIManager)의 번호 100을 그대로 따릅니다.** 그래서 100짜리 확인 창이 200짜리
설정 창 뒤에 깔려 안 보이는 것입니다.

이 작업은 확인 창에게 **자기만의 그리기 순서 번호 250**을 부여해서, 설정 창(200)을 포함한
모든 일반 패널보다 항상 앞에 그려지도록 고치는 것입니다. 확인 창은 모든 패널 공통으로
사용되는 모달이므로, 이 수정 한 번이면 다른 패널 위에서도 동일하게 잘 동작합니다.

---

## 2. 채택안 및 기각안 요약

- **채택 (A안)**: `ConfirmPopup.prefab`에 독립 Canvas Override(`SortingOrder=250`) +
  GraphicRaycaster 추가.
- **기각 (B안)**: UIManager Canvas 자체의 SortingOrder를 상향.
  - 기각 이유: UIManager Canvas를 올리면 그 안에 함께 소속된 **BlockingOverlay(반투명
    배경)** 도 같이 올라가서, 패널(SO=200)보다 위에 그려져 패널 자체를 덮어버리는 새 문제가
    발생함 (Research.md 5절, 부가 이슈 2번 참조).

### 수정 후 SortingOrder 구조

```
SO=0   → [UI] Canvas (HUD)
SO=100 → UIManager Canvas (BlockingOverlay — 패널 뒤 반투명 배경)
SO=200 → 각 패널 Canvas Override (InGameSettings 본체 "Panel" 등)
SO=250 → ConfirmPopup 독립 Canvas Override  ← 신규
SO=300 → LoadingIndicator 독립 Canvas
```

- ConfirmPopup(250)이 BlockingOverlay(100) 및 모든 패널(200)보다 위, LoadingIndicator(300)
  보다 아래에 위치하므로 z-order 일관성이 유지됨.

---

## 3. 근거 규칙 (GameSystemRules_UI.md)

| 수정 항목 | 근거 규칙 | 내용 |
|---|---|---|
| ConfirmPopup이 설정 창보다 위에 그려져야 함 | 인게임 설정 메뉴 규칙 3 (포기 확인 팝업) | "설정 메뉴에서 포기 버튼을 탭하면 확인 팝업(ConfirmPopup)이 표시된다." → 표시되려면 가려지지 않아야 함 |
| ConfirmPopup은 배경 탭으로 닫히지 않는 모달 | 공통 UI 규칙 8·9 (팝업 타입 구분 / 배경 탭) | ConfirmPopup은 모달(Modal)로, 확인/취소 버튼으로만 닫힘. Canvas 추가가 이 동작을 바꾸지 않음 |
| GraphicRaycaster 함께 추가 | 공통 UI 규칙 5 (BlockingOverlay 단일 소유) + Unity 제약 | 독립 Canvas를 가진 UI 요소는 자체 GraphicRaycaster가 있어야 버튼 입력을 받음. Canvas만 추가하고 Raycaster가 없으면 확인/취소 버튼 클릭이 동작하지 않음 |
| BlockingOverlay는 UIManager가 단일 소유 — 손대지 않음 | 공통 UI 규칙 4·5 (Safe Area / 단일 소유 패턴) | ConfirmPopup에 개별 오버레이를 추가하지 않음. 이번 수정은 SortingOrder만 부여하며 오버레이 소유 구조는 그대로 유지 |

---

## 4. 구현 방법

### 4-1. 방식 결정: ConfirmPopup.prefab 직접 수정

ConfirmPopup의 **루트 GameObject**에 다음 두 컴포넌트를 추가한다.

1. **Canvas** (`u!223`)
   - `m_OverrideSorting: 1`
   - `m_SortingOrder: 250`
2. **GraphicRaycaster** (`u!114`, Canvas 동작용)

추가 방식 판단:
- ConfirmPopup.prefab은 단일 프리팹이고 추가할 컴포넌트가 2개로 단순하다.
  **에디터 1회성 스크립트(Editor 메뉴)** 로 추가하는 방식을 채택한다.
  - 이유: 프리팹의 YAML을 손으로 편집하면 fileID 충돌·GUID 누락 위험이 있고,
    Canvas/GraphicRaycaster의 직렬화 필드를 Unity가 자동으로 올바르게 채워주도록 하는 것이
    안전하다 (CLAUDE.md 규칙 7 완성도 우선).
  - WORKFLOW [5-2] "Inspector 작업이 필요한 경우 Editor 1회성 스크립트 작성 → 사용자 실행
    요청" 절차를 따른다.
- 스크립트는 `Hexiege/Fix/Add Canvas To ConfirmPopup` 형태의 메뉴 경로로 작성하고,
  프리팹 루트에 Canvas(OverrideSorting=1, SortingOrder=250) + GraphicRaycaster가 없을 때만
  추가하도록(멱등) 구현한다. 실행 후 프리팹을 저장한다.
- 1회성 스크립트는 실행·검증 완료 후 삭제해도 무방하다.

### 4-2. 코드 변경 여부

- `UIManager.cs`, `InGameSettingsUI.cs`, `ConfirmPopup.cs` **코드 수정 없음**.
  - 호출 흐름(`ShowConfirm` → `_confirmPopup.Show`)은 그대로이며, SortingOrder는 순수
    프리팹 컴포넌트 설정으로만 해결되기 때문.
  - 단, 구현 단계에서 `ConfirmPopup.cs`가 자체적으로 BlockingOverlay를 추가 호출하는지
    1차 확인한다(Research 부가 이슈 2). 만약 추가 호출이 없다면(현재 정황상 InGameSettingsUI가
    이미 오버레이를 켠 상태에서 모달만 띄움) 코드 변경 불필요. 추가 호출이 있어도 오버레이는
    SO=100이라 ConfirmPopup(250) 아래에 그려지므로 가림 문제는 발생하지 않음.

---

## 5. 수정할 파일 목록

```
[수정]
- Assets/_Project/Prefabs/UI/ConfirmPopup.prefab
    → 루트에 Canvas(OverrideSorting=1, SortingOrder=250) + GraphicRaycaster 추가

[추가 — 1회성, 검증 후 삭제 가능]
- Assets/_Project/Scripts/Editor/Fix_AddCanvasToConfirmPopup.cs
    → 위 컴포넌트를 프리팹에 추가하는 에디터 메뉴 스크립트

[신규 — 문서]
- Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md
    → 전역 SortingOrder 대역 규칙 및 씬별 Canvas 구조 문서 신규 작성

[수정 — 문서]
- Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md
    → GameSystemRules_CanvasSortingOrder.md 참조 링크 추가
```

### 5-1. GameSystemRules_UI.md 참조 링크 추가

`GameSystemRules_UI.md`에 Canvas SortingOrder 관련 참조 링크를 추가한다.
신규 작성한 `GameSystemRules_CanvasSortingOrder.md`로 연결하여, UI 문서를 보는
사람이 SortingOrder 구조 및 씬별 Canvas 목록을 쉽게 찾아갈 수 있도록 한다.

---

- `UIManager.cs` / `InGameSettingsUI.cs` / `ConfirmPopup.cs` : **변경 없음** (4-2 참조)
- 씬 파일(`Login.unity` / `Game.unity`) : **변경 없음** (ConfirmPopup은 프리팹 인스턴스라
  프리팹 수정이 인스턴스에 반영됨. 단, 씬 인스턴스에 오버라이드가 걸려 있는지는 검증 항목에서 확인)

---

## 6. 검증 항목 (테스트 방법)

### 6-1. 정적 검증

1. `ConfirmPopup.prefab` 루트에 Canvas 컴포넌트가 존재하고 `m_OverrideSorting: 1`,
   `m_SortingOrder: 250` 인지 확인.
2. `ConfirmPopup.prefab` 루트에 GraphicRaycaster가 존재하는지 확인.
3. `Login.unity`의 ConfirmPopup 인스턴스에 SortingOrder를 덮어쓰는 프리팹 오버라이드가
   걸려 있지 않은지 확인(있으면 250이 무력화될 수 있음 → 오버라이드 제거 필요).
4. `GameSystemRules_CanvasSortingOrder.md`의 씬별 Canvas 표가 실제 씬
   (Login / Lobby / Game)의 Canvas SortingOrder 값과 일치하는지 확인.

### 6-2. 실기 검증 (사용자 테스트)

1. 게임 진입 → 우상단 설정 버튼 탭 → 인게임 설정 창이 뜬다.
2. "게임포기" 버튼 탭 → **확인 팝업(ConfirmPopup)이 설정 창 위에 정상적으로 보인다.**
   (기존 버그: 설정 창 뒤에 가려 안 보임)
3. 확인 팝업의 "포기" / "취소" 버튼이 **정상적으로 클릭된다** (GraphicRaycaster 정상 동작 확인).
4. 확인 팝업 배경(설정 창 영역)을 탭해도 확인 팝업이 닫히지 않는다 (모달 규칙 9 유지).
5. "취소" 탭 시 확인 팝업만 닫히고 설정 창은 그대로 남아 있다.
6. BlockingOverlay(반투명 배경)가 설정 창이나 확인 창을 부자연스럽게 덮지 않는지 확인
   (오버레이 SO=100 < 확인창 SO=250 이므로 정상이어야 함).
7. 다른 패널(생산/건물 배치) 위에서 확인 팝업이 뜨는 경로가 있다면 동일하게 위에 뜨는지 확인.

---

## 7. 위험 요소

- **씬 인스턴스 오버라이드**: ConfirmPopup이 씬에 인스턴스로 배치되어 있고, 그 인스턴스에
  Canvas 관련 프리팹 오버라이드가 있으면 프리팹 SO=250이 적용되지 않을 수 있음 → 6-1의 3번
  검증으로 확인 후, 오버라이드 발견 시 제거.
- **GraphicRaycaster 누락 시 입력 불가**: Canvas만 추가하고 Raycaster를 빠뜨리면 확인/취소
  버튼이 안 눌림 → 반드시 함께 추가(4-1).
- **LoadingIndicator(SO=300)와의 순서**: 250 < 300 이므로 로딩 인디케이터가 확인창 위에
  그려짐(의도된 동작). 변경 없음.

---

> 본 Plan은 사용자 승인 후에만 구현을 시작한다 (WORKFLOW [4]).
> 코드/프리팹 수정은 game-programmer 에이전트에 위임한다 (CLAUDE.md 규칙 3).
