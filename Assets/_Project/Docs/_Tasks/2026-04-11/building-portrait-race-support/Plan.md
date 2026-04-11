# Plan: 건물 초상화 종족+팀 기반 표시

## 작업 목표
`BuildingPlacementUI`에서 팝업을 열 때 현재 팀과 종족에 맞는 건물 초상화(배럭, 채굴소)를 표시한다.

---

## 변경 파일 목록

| 파일 | 변경 유형 |
|------|-----------|
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | 코드 수정 |
| `BuildingPlacementUI` MonoBehaviour (Inspector) | 스프라이트 연결 |
| `ProductionPanelUI` MonoBehaviour (Inspector) | 스프라이트 연결 (코드 변경 없음) |

---

## BuildingPlacementUI.cs 변경 내용

### 1. `BuildingPortraitSet` struct → `BuildingRacePortraitSet`으로 교체

**기존:**
```csharp
[System.Serializable]
public struct BuildingPortraitSet
{
    public Sprite barracks;
}
```

**변경 후:**
```csharp
/// <summary>
/// 팀×종족 단위의 건물 초상화 스프라이트 세트.
/// barracks: 배럭 건물 버튼 이미지.
/// miningPost: 채굴소 건물 버튼 이미지.
/// (Human의 경우 두 값 동일 이미지 가능)
/// </summary>
[System.Serializable]
public struct BuildingRacePortraitSet
{
    public Sprite barracks;
    public Sprite miningPost;
}
```

---

### 2. Inspector 필드 교체 (6세트 + 불필요 필드 제거)

**기존:**
```csharp
[SerializeField] private BuildingPortraitSet _bluePortraits;
[SerializeField] private BuildingPortraitSet _redPortraits;
[SerializeField] private Sprite _miningPostPortrait;  // 팀/종족 무관 단일 이미지
```

**변경 후 (팀×종족 6세트):**
```csharp
[Header("Building Portraits — 종족+팀별 (팀×종족 = 6세트)")]
[SerializeField] private BuildingRacePortraitSet _blueHumanPortraits;
[SerializeField] private BuildingRacePortraitSet _blueSpiritPortraits;
[SerializeField] private BuildingRacePortraitSet _blueTranscendencePortraits;
[SerializeField] private BuildingRacePortraitSet _redHumanPortraits;
[SerializeField] private BuildingRacePortraitSet _redSpiritPortraits;
[SerializeField] private BuildingRacePortraitSet _redTranscendencePortraits;
```

> `_miningPostPortrait` 단일 필드 삭제.

---

### 3. `UpdateButtonPortraits()` 수정

**기존:**
```csharp
private void UpdateButtonPortraits(TeamId team)
{
    var set = team == TeamId.Blue ? _bluePortraits : _redPortraits;
    if (_barracksButtonPortrait   != null) _barracksButtonPortrait.sprite  = set.barracks;
    if (_miningPostButtonPortrait != null) _miningPostButtonPortrait.sprite = _miningPostPortrait;
}
```

**변경 후:**
```csharp
private void UpdateButtonPortraits(TeamId team)
{
    // 팀에 따라 현재 종족 조회 (GameRaceContext는 게임 시작 시 설정됨)
    RaceId race = team == TeamId.Blue ? GameRaceContext.BlueRace : GameRaceContext.RedRace;
    var set = GetBuildingPortraitSet(team, race);
    if (_barracksButtonPortrait   != null) _barracksButtonPortrait.sprite  = set.barracks;
    if (_miningPostButtonPortrait != null) _miningPostButtonPortrait.sprite = set.miningPost;
}

private BuildingRacePortraitSet GetBuildingPortraitSet(TeamId team, RaceId race)
{
    if (team == TeamId.Blue)
    {
        return race switch
        {
            RaceId.Spirit        => _blueSpiritPortraits,
            RaceId.Transcendence => _blueTranscendencePortraits,
            _                    => _blueHumanPortraits
        };
    }
    else
    {
        return race switch
        {
            RaceId.Spirit        => _redSpiritPortraits,
            RaceId.Transcendence => _redTranscendencePortraits,
            _                    => _redHumanPortraits
        };
    }
}
```

---

## Inspector 연결 작업

### BuildingPlacementUI Inspector (신규 6세트)

| 필드 | barracks | miningPost |
|------|----------|------------|
| `_blueHumanPortraits` | bld_barracks_blue | bld_mining_post |
| `_blueSpiritPortraits` | bld_summoningaltar_blue | ⚠️ **bld_manarift_blue 없음 → 임시 null** |
| `_blueTranscendencePortraits` | bld_hunterplant_blue | bld_fungalnode_blue |
| `_redHumanPortraits` | bld_barracks_red | bld_mining_post |
| `_redSpiritPortraits` | bld_summoningaltar_red | bld_manarift_red |
| `_redTranscendencePortraits` | bld_hunterplant_red | bld_fungalnode_red |

> `bld_manarift_blue.png` 제작 완료 후 `_blueSpiritPortraits.miningPost`에 연결.

### ProductionPanelUI Inspector (기존 필드에 연결)

| 필드 | slot1 | slot2 | slot3 |
|------|-------|-------|-------|
| `_blueSpiritPortraits` | flamespirit_portrait_blue | emberspirit_portrait_blue | infernospirit_portrait_blue |
| `_blueTranscendencePortraits` | bearguard_portrait_blue | foxmagician_portrait_blue | lionknight_portrait_blue |
| `_redSpiritPortraits` | flamespirit_portrait_red | emberspirit_portrait_red | infernospirit_portrait_red |
| `_redTranscendencePortraits` | bearguard_portrait_red | foxmagician_portrait_red | lionknight_portrait_red |

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 기존 Inspector 연결 손실 | `_bluePortraits`, `_redPortraits` 필드 삭제로 기존에 연결된 Human 이미지가 사라짐 → 신규 6세트 필드에 다시 연결 필수 |
| ManaRift Blue 미제작 | `_blueSpiritPortraits.miningPost` 연결 불가 → null 상태로 두면 채굴소 버튼 이미지가 빈 상태로 표시됨 |
| `GameRaceContext` 미설정 시 | 기본값 `RaceId.Human`이므로 Human 이미지로 폴백 — 문제 없음 |

---

## 작업 순서

1. **game-programmer** → `BuildingPlacementUI.cs` 코드 수정 (struct 교체, 필드 추가, UpdateButtonPortraits 수정)
2. Inspector에서 BuildingPlacementUI 신규 6세트 스프라이트 연결
3. Inspector에서 ProductionPanelUI 기존 4세트 스프라이트 연결
4. **qa-tester** → 테스트
