# 랠리포인트 깃발 — 팀 간 표시 누수 현상 분석

이 문서는 멀티플레이에서 클라이언트가 랠리포인트를 설정했을 때,  
설정한 클라이언트뿐만 아니라 호스트 화면에도 랠리포인트 깃발이 나타나는  
현상의 원인을 분석합니다.

개발 의도는 **"랠리포인트 깃발은 설정한 플레이어 화면에서만 보여야 한다"**는 것이었습니다.  
그러나 현재 구조에서는 호스트가 네트워크 요청을 처리하는 과정에서  
깃발 표시 신호가 호스트 측에서도 발생하여, 양쪽 화면에 모두 깃발이 표시됩니다.

---

## 현재 동작 흐름

클라이언트가 랠리포인트를 설정할 때 일어나는 일:

```
[클라이언트] ProductionPanelUI.CompleteRallyPointSetting() 호출
│
├─ [A] SetRallyPointServerRpc() 호출 → 호스트로 네트워크 전송
│       ↓
│   [호스트] SetRallyPointServerRpc 수신
│       → production.SetRallyPoint() 실행
│       → UnitProductionUseCase.SetRallyPoint()
│       → GameEvents.OnRallyPointChanged 발생 (호스트 측)  ← ❌ 버그
│       → 호스트 ProductionTicker → 깃발 표시
│
└─ [B] _production.SetRallyPoint() 로컬 호출
        → UnitProductionUseCase.SetRallyPoint()
        → GameEvents.OnRallyPointChanged 발생 (클라이언트 측)  ← ✅ 정상
        → 클라이언트 ProductionTicker → 깃발 표시
```

---

## 핵심 원인 3가지

### 원인 1. `OnRallyPointChanged` 이벤트에 팀 정보 없음

`RallyPointChangedEvent` 구조체에는 배럭 ID와 좌표만 있고, 어느 팀 배럭인지 알 수 없습니다.  
그래서 이벤트를 받은 쪽에서 "내 팀 것인지" 판단할 방법이 없습니다.

**파일:** `Assets/_Project/Scripts/Application/Events/GameEvents.cs:268-279`

```csharp
public readonly struct RallyPointChangedEvent
{
    public readonly int BarracksId;
    public readonly HexCoord? Coord;
    // 팀 정보 없음 ← 원인
}
```

### 원인 2. `ProductionTicker`가 모든 팀의 이벤트를 무조건 처리

`ProductionTicker.OnRallyPointChanged()`는 이벤트가 오면 어느 팀 배럭인지,  
현재 플레이어가 그 팀인지 확인하지 않고 무조건 깃발을 생성합니다.

**파일:** `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs:324-339`

### 원인 3. 호스트 RPC 핸들러가 상태 저장과 UI 이벤트를 분리하지 않음

호스트가 RPC를 수신하면 `production.SetRallyPoint()`를 호출합니다.  
이 함수는 `ProductionState`에 랠리 좌표를 저장하는 것과 동시에  
`OnRallyPointChanged` 이벤트도 발생시킵니다.  
호스트는 데이터만 저장하면 되지만, 깃발 표시 신호까지 함께 나옵니다.

**파일:** `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs:735`

---

## 코드 주석 vs 실제 동작 불일치

`NetworkProductionController.cs:684-685` 주석:
> "화면에 표시되는 랠리 마커(OnRallyPointChanged)는 클라이언트 로컬에서  
> `_production.SetRallyPoint()`를 함께 호출하여 표시만 갱신함."

→ **의도:** 깃발은 설정한 클라이언트만 표시  
→ **현실:** 호스트도 `SetRallyPoint()` 실행 → 호스트에서도 이벤트 발생 → 호스트에도 깃발 표시

---

## 영향 범위

| 파일 | 변경 필요 | 이유 |
|------|-----------|------|
| `GameEvents.cs` | ✅ 필요 | `RallyPointChangedEvent`에 팀 필드 추가 |
| `UnitProductionUseCase.cs` | ✅ 필요 | 이벤트 생성 시 팀 정보 전달 |
| `ProductionTicker.cs` | ✅ 필요 | 팀 필터링 로직 추가 |
| `NetworkProductionController.cs` | ❌ 불필요 | 서버 상태 저장은 그대로 유지 |
| `ProductionPanelUI.cs` | ❌ 불필요 | 클라이언트 로컬 호출은 의도된 동작 |

---

## 참고: `ProductionState.Team` 이미 존재

`ProductionState`에는 이미 `Team` 필드가 있습니다.  
`UnitProductionUseCase.SetRallyPoint()`에서 `state.Team`에 즉시 접근 가능합니다.

**파일:** `Assets/_Project/Scripts/Domain/Building/ProductionState.cs:80`
