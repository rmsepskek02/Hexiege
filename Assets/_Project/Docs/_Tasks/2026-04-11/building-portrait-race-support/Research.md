# Research: 건물 초상화 종족+팀 기반 표시

## 작업 배경
`Assets/_Project/Sprites` 폴더에 정령계(Spirit), 초월계(Transcendence) 건물 UI 이미지가 추가됨.
현재 `BuildingPlacementUI`는 팀(Blue/Red)만 구분하여 건물 초상화를 표시하고, 종족 구분이 없음.
종족에 따라 올바른 건물 이미지(예: Spirit → SummoningAltar, Transcendence → HunterPlant)가 표시되도록 변경이 필요.

---

## 현재 스프라이트 현황

### 유닛 초상화 (전부 완료)
| 종족 | 유닛 | Blue | Red |
|------|------|------|-----|
| Spirit | FlameSpirit | ✅ | ✅ |
| Spirit | EmberSpirit | ✅ | ✅ |
| Spirit | InfernoSpirit | ✅ | ✅ |
| Transcendence | BearGuard | ✅ | ✅ |
| Transcendence | FoxMagician | ✅ | ✅ |
| Transcendence | LionKnight | ✅ | ✅ |

### 건물 초상화
| 종족 | 건물 | Blue | Red |
|------|------|------|-----|
| Human | Castle (bld_castle) | ✅ | ✅ |
| Human | Barracks (bld_barracks) | ✅ | ✅ |
| Human | MiningPost (bld_mining_post) | ✅ (팀 무관) | — |
| Spirit | Castle → SpiritNexus | ✅ | ✅ |
| Spirit | Barracks → SummoningAltar | ✅ | ✅ |
| Spirit | MiningPost → ManaRift | ❌ **없음** | ✅ |
| Transcendence | Castle → ElderTree | ✅ | ✅ |
| Transcendence | Barracks → HunterPlant | ✅ | ✅ |
| Transcendence | MiningPost → FungalNode | ✅ | ✅ |

> ⚠️ ManaRift Blue(`bld_manarift_blue.png`)는 미제작 상태.

---

## 영향 범위

### 변경 필요 파일
- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`

### 변경 불필요 (Inspector 연결만)
- `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
  - `_blueSpiritPortraits`, `_blueTranscendencePortraits`, `_redSpiritPortraits`, `_redTranscendencePortraits` 필드 이미 존재
  - 스프라이트 드래그 연결만 하면 됨

---

## 현재 BuildingPlacementUI 구조 분석

### Inspector 필드 (현재)
```
[BuildingPortraitSet] _bluePortraits  → barracks (Sprite)
[BuildingPortraitSet] _redPortraits   → barracks (Sprite)
[Sprite]              _miningPostPortrait  (팀/종족 무관 단일 이미지)
```

### `BuildingPortraitSet` struct (현재)
```csharp
public struct BuildingPortraitSet
{
    public Sprite barracks;  // 필드 하나뿐, 종족 구분 없음
}
```

### `UpdateButtonPortraits(TeamId team)` (현재)
```
팀(Blue/Red)으로만 set 선택 → barracks/miningPost 이미지 설정
종족(Spirit/Transcendence) 분기 없음
GameRaceContext 미사용
```

---

## ProductionPanelUI 참조 패턴

ProductionPanelUI는 동일 목적(종족+팀 기반 이미지 선택)을 이미 구현해 놓음.
BuildingPlacementUI도 동일한 패턴을 따르면 됨:
- 팀×종족 = 6세트 Inspector 필드
- `GetPortraitSet(TeamId team, RaceId race)` 메서드로 6가지 경우 분기
- `GameRaceContext.BlueRace / RedRace`로 현재 종족 조회

---

## 접근 가능한 컨텍스트

`GameRaceContext`는 `Infrastructure` 레이어의 정적 홀더.
- `GameRaceContext.BlueRace` → Blue 팀 현재 종족
- `GameRaceContext.RedRace` → Red 팀 현재 종족
- `BuildingPlacementUI`는 `Presentation` 레이어 → `Infrastructure` 참조 가능 (이미 `using Hexiege.Infrastructure;` 존재)

---

## Castle 초상화 제외 이유

`BuildingPlacementUI`는 배럭(Barracks)과 채굴소(MiningPost) 버튼만 노출.
Castle은 게임 시작 시 자동 배치되므로 이 UI에 포함하지 않음.
→ Castle 이미지(SpiritNexus, ElderTree)는 이 작업 범위 밖.
