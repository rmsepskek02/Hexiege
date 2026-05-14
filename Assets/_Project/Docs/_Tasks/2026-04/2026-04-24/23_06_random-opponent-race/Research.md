# Research — 싱글플레이 상대방 종족 랜덤 결정

**작업일**: 2026-04-24  
**작업자**: Claude

---

## 현재 상태

싱글플레이 시작 시 AI(Red 팀)의 종족이 항상 `Human`으로 고정되어 있다.

### 고정 위치

**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` — 283번째 줄

```csharp
// 싱글플레이: 로비에서 선택한 종족을 Blue 팀에 적용.
// Red 팀은 AI이므로 기본값 Human으로 설정.
GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human);
```

---

## 종족 시스템 구조

### RaceId 열거형 (Domain 레이어)

**파일**: `Assets/_Project/Scripts/Domain/Common/RaceId.cs`

| 값 | 이름 | 설명 |
|----|------|------|
| 0 | Human | 인간 |
| 1 | Spirit | 정령 |
| 2 | Transcendence | 초월 |

### 관련 홀더 클래스

| 파일 | 역할 |
|------|------|
| `Infrastructure/LocalPlayerRace.cs` | 로비에서 선택한 로컬 플레이어 종족 저장 (`Current`, `Set()`, `Reset()`) |
| `Infrastructure/GameRaceContext.cs` | 게임 중 양 팀 종족 저장 (`BlueRace`, `RedRace`, `Set()`) |

### 종족이 사용되는 시점

`GameRaceContext.Set()`은 `LoadMap()` 호출 전에 반드시 설정되어야 한다.  
이후 아래 시스템들이 `GameRaceContext`를 참조하여 종족별 에셋을 로드한다:

- `UnitFactory.CreateUnitObject()` — 종족별 유닛 프리팹 선택
- `BuildingFactory` — 종족별 건물 스탯 적용
- `PlaceCastles()` (GameBootstrapper.cs ~695번째 줄) — Castle 배치 시 `BlueRace` / `RedRace` 전달
- `PlaceGoldMines()` (GameBootstrapper.cs ~757번째 줄) — 금광 배치 시 건물 종족별 HP 설정

---

## 멀티플레이와의 차이

| | 싱글플레이 | 멀티플레이 |
|--|-----------|-----------|
| Blue 종족 | `LocalPlayerRace.Current` (로비 선택) | 네트워크 동기화 |
| Red 종족 | `RaceId.Human` **고정** | 상대방 로비 선택값 |
| 설정 위치 | `GameBootstrapper.cs:283` | `NetworkGameFlow.cs` → `StartGameClientRpc()` |

---

## 수정 범위 분석

### 수정 대상

- `GameBootstrapper.cs:283` — 1줄 변경  
  `RaceId.Human` → `랜덤으로 결정된 종족`

### 랜덤 결정 방법

`System.Enum.GetValues(typeof(RaceId))`로 가능한 종족 전체를 배열로 가져온 뒤 `UnityEngine.Random.Range()`로 인덱스를 선택하는 방법이 가장 안전하다.  
새 종족이 추가될 경우 자동으로 후보에 포함된다.

### 영향 범위

수정은 `GameBootstrapper.cs` 1개 파일, 1줄에 한정된다.  
`GameRaceContext.Set()`의 시그니처(`BlueRace`, `RedRace`) 변경 없음.  
멀티플레이 코드 경로(`NetworkGameFlow.cs`)는 변경 없음.

---

## 부가 이슈

없음.
