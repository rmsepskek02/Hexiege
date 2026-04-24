# Plan — 싱글플레이 상대방 종족 랜덤 결정

**작업일**: 2026-04-24  
**작업자**: Claude

---

## 목표

싱글플레이 시 AI(Red 팀) 종족이 항상 `Human`으로 고정되는 문제를 수정하여,  
매 게임마다 `Human / Spirit / Transcendence` 중 하나가 무작위로 결정되도록 한다.

---

## 구현 접근법

### 수정 파일

| 파일 | 변경 내용 |
|------|----------|
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 283번째 줄 — AI 종족 고정값을 랜덤 선택으로 교체 |

### 변경 전

```csharp
GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human);
```

### 변경 후

```csharp
// RaceId에 정의된 모든 종족 값을 배열로 가져온다.
// 새 종족이 추가되어도 자동으로 후보에 포함됨.
RaceId[] allRaces = (RaceId[])System.Enum.GetValues(typeof(RaceId));
RaceId opponentRace = allRaces[UnityEngine.Random.Range(0, allRaces.Length)];
GameRaceContext.Set(LocalPlayerRace.Current, opponentRace);
```

---

## 아키텍처 제약

- `GameRaceContext.Set()` 호출은 `LoadMap()` 이전에 반드시 완료되어야 한다. (기존 위치 유지)
- `System.Enum.GetValues()`는 런타임 리플렉션 사용 — 성능 영향 무시 가능 (게임 시작 1회 호출)
- 멀티플레이 코드 경로(`NetworkGameFlow.cs`)는 수정하지 않는다.

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 새 종족 추가 시 랜덤 풀에 자동 포함 — 의도치 않은 종족이 등장할 수 있음 | 현재 3종족 모두 구현 완료 상태이므로 문제 없음. 미완성 종족 추가 시 풀에서 제외하는 처리가 필요할 수 있음 |
| 플레이어와 동일한 종족이 AI로 결정될 수 있음 | 게임 디자인상 허용 (미러 매치) — 별도 제한 불필요 |

---

## 체크리스트

- [ ] `GameBootstrapper.cs:283` 수정
- [ ] 싱글플레이 3회 이상 진행하여 AI 종족이 달라지는지 확인
- [ ] 각 종족으로 AI가 설정되었을 때 유닛/건물이 정상적으로 생성되는지 확인
