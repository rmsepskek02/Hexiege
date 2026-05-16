# 랠리포인트 깃발 — 팀별 표시 분리 수정 계획

이 작업은 멀티플레이에서 랠리포인트 깃발이 설정한 플레이어 화면에서만 보이도록 수정합니다.

클라이언트가 랠리포인트를 설정하면 호스트 화면에도 깃발이 표시되는 버그를 고칩니다.  
수정 후에는 각 플레이어가 자신이 설정한 랠리포인트 깃발만 볼 수 있게 됩니다.

---

## GameSystemRules.md 검토

GameSystemRules.md의 규칙은 이동/전투 시스템에 집중되어 있으며, 랠리포인트 UI 표시에 대한 직접 규칙은 없습니다.  
이 수정은 멀티플레이 아키텍처 원칙을 따릅니다:  
- **서버(호스트):** 게임 상태를 관리하는 단일 권위자 (ProductionState 저장)  
- **클라이언트:** UI 표시 담당 (깃발 생성/숨김)

이 원칙에 따라 "깃발 표시"는 설정한 플레이어(클라이언트)의 책임으로 두고,  
호스트는 상태 저장만 담당하는 구조로 수정합니다.

---

## 수정 접근법: 이벤트에 팀 정보 추가 + 화면 담당이 필터링

**핵심 원칙:**  
어느 팀의 랠리포인트가 변경되었는지를 이벤트 자체에 담고,  
화면을 담당하는 `ProductionTicker`가 "내 팀 것만" 처리하도록 한다.

이 방법을 선택한 이유:
- `ProductionState.Team`이 이미 있어 추가 조회 없이 바로 사용 가능
- 이벤트 데이터가 자기 완결적이 됨 (이벤트만 봐도 어느 팀 것인지 알 수 있음)
- 싱글플레이에는 영향 없음 (NetworkManager가 없으면 필터링 건너뜀)
- 게임 로직(서버 상태 저장, 유닛 스폰 랠리 이동)은 전혀 건드리지 않음

---

## 파일별 수정 내용

### 1. `Assets/_Project/Scripts/Application/Events/GameEvents.cs`

**변경 위치:** `RallyPointChangedEvent` 구조체 (268~279번째 줄)  
**변경 내용:** `TeamId Team` 필드 및 생성자 파라미터 추가

```csharp
// 변경 전
public readonly struct RallyPointChangedEvent
{
    public readonly int BarracksId;
    public readonly HexCoord? Coord;

    public RallyPointChangedEvent(int barracksId, HexCoord? coord)
    {
        BarracksId = barracksId;
        Coord = coord;
    }
}

// 변경 후
public readonly struct RallyPointChangedEvent
{
    public readonly int BarracksId;
    public readonly HexCoord? Coord;
    /// <summary> 랠리포인트를 설정한 배럭의 소속 팀. 화면 표시 필터링에 사용. </summary>
    public readonly TeamId Team;

    public RallyPointChangedEvent(int barracksId, HexCoord? coord, TeamId team)
    {
        BarracksId = barracksId;
        Coord = coord;
        Team = team;
    }
}
```

---

### 2. `Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs`

**변경 위치:** `SetRallyPoint()` 434번째 줄, `ClearRallyPoint()` 443번째 줄  
**변경 내용:** 이벤트 생성 시 `state.Team` 전달

```csharp
// SetRallyPoint 내부 (변경 전)
GameEvents.OnRallyPointChanged.OnNext(
    new RallyPointChangedEvent(barracksId, target));

// SetRallyPoint 내부 (변경 후)
GameEvents.OnRallyPointChanged.OnNext(
    new RallyPointChangedEvent(barracksId, target, state.Team));

// ClearRallyPoint 내부 (변경 전)
GameEvents.OnRallyPointChanged.OnNext(
    new RallyPointChangedEvent(barracksId, null));

// ClearRallyPoint 내부 (변경 후)
GameEvents.OnRallyPointChanged.OnNext(
    new RallyPointChangedEvent(barracksId, null, state.Team));
```

---

### 3. `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs`

**변경 위치:** `OnRallyPointChanged()` 324번째 줄  
**변경 내용:** 멀티플레이에서 로컬 플레이어 팀 이벤트만 처리하는 필터 추가

```csharp
// 변경 전
private void OnRallyPointChanged(RallyPointChangedEvent e)
{
    if (e.Coord.HasValue)
    {
        CreateOrMoveMarker(e.BarracksId, e.Coord.Value);
        ShowMarkerTemporary(e.BarracksId);
    }
    else
    {
        DestroyMarker(e.BarracksId);
    }
}

// 변경 후
private void OnRallyPointChanged(RallyPointChangedEvent e)
{
    // 멀티플레이 중이면 로컬 플레이어의 팀 이벤트만 처리.
    // 호스트(서버) = Blue팀, 클라이언트(비서버) = Red팀.
    // 싱글플레이에서는 NetworkManager가 없으므로 필터 건너뜀.
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        TeamId localTeam = NetworkManager.Singleton.IsServer ? TeamId.Blue : TeamId.Red;
        if (e.Team != localTeam) return;
    }

    if (e.Coord.HasValue)
    {
        CreateOrMoveMarker(e.BarracksId, e.Coord.Value);
        ShowMarkerTemporary(e.BarracksId);
    }
    else
    {
        DestroyMarker(e.BarracksId);
    }
}
```

---

## 수정하지 않는 파일

| 파일 | 이유 |
|------|------|
| `NetworkProductionController.cs` | 호스트 RPC 핸들러는 그대로 유지. 서버가 `state.RallyPoint`를 저장해야 유닛 생산 후 랠리 이동이 동작한다. |
| `ProductionPanelUI.cs` | 클라이언트 로컬 `_production.SetRallyPoint()` 호출은 의도된 동작. 변경 불필요. |

---

## 위험 요소 검토

| 항목 | 위험도 | 이유 |
|------|--------|------|
| 싱글플레이 영향 | 없음 | NetworkManager가 null이면 필터링 건너뜀, 기존 동작 그대로 |
| 유닛 생산 후 랠리 이동 | 없음 | 이벤트 처리만 변경, `state.RallyPoint` 저장은 그대로 |
| 호스트가 자신의 깃발 못 보는 문제 | 없음 | 호스트가 Blue팀 배럭 랠리 설정 시 → Blue팀 이벤트 → 필터 통과 → 표시됨 |
| 클라이언트가 자신의 깃발 못 보는 문제 | 없음 | 클라이언트 로컬 `SetRallyPoint()` 호출 → Red팀 이벤트 → 필터 통과 → 표시됨 |

---

## 검증 시나리오

1. **[호스트] 자신의 배럭에 랠리포인트 설정** → 호스트 화면에만 깃발 표시, 클라이언트 화면에는 미표시
2. **[클라이언트] 자신의 배럭에 랠리포인트 설정** → 클라이언트 화면에만 깃발 표시, 호스트 화면에는 미표시
3. **싱글플레이** → 기존과 동일하게 깃발 표시
