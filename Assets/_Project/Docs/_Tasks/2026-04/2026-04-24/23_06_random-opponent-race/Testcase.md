# Testcase — 싱글플레이 상대방 종족 랜덤 결정

**작업일**: 2026-04-24  

---

## TC 목록

### TC-SINGLE-001: 게임 재시작 시 AI 종족이 달라지는지 확인

**전제:** 싱글플레이 모드가 정상 진입 가능한 상태

**동작:**
1. 싱글플레이로 게임을 시작한다
2. AI(Red 팀)의 Castle, 유닛, 건물 외형을 확인하여 종족을 파악한다
3. 게임을 종료하고 다시 싱글플레이로 게임을 시작한다
4. 다시 AI의 종족을 확인한다
5. 3~4단계를 포함하여 총 3회 이상 반복한다

**기댓값:**
- 3회 중 적어도 한 번은 이전 게임과 다른 종족의 AI가 등장한다
- 매 게임마다 인간, 정령, 초월 중 하나의 외형으로 AI가 표시된다

**결과:** PASS

---

### TC-SINGLE-002: AI가 정령(Spirit) 종족으로 설정될 때 정상 동작 확인

**전제:** AI 종족이 정령으로 결정된 게임에 진입한 상태

**동작:**
1. AI(Red 팀)의 건물과 유닛이 정령 외형으로 표시되는 게임을 찾는다
2. AI 진영의 Castle, 병영, 금광이 정령 계열 외형인지 확인한다
3. AI 유닛이 생산되어 아군 방향으로 이동하는지 확인한다
4. AI 유닛과 전투가 발생할 때 정상적으로 피해 주고받기가 이루어지는지 확인한다

**기댓값:**
- AI 진영의 모든 건물이 정령 계열 외형으로 표시된다
- AI 유닛이 정령 계열 외형으로 생산되고 정상 이동 및 전투를 수행한다

**결과:** PASS

---

### TC-SINGLE-003: AI가 초월(Transcendence) 종족으로 설정될 때 정상 동작 확인

**전제:** AI 종족이 초월로 결정된 게임에 진입한 상태

**동작:**
1. AI(Red 팀)의 건물과 유닛이 초월 외형으로 표시되는 게임을 찾는다
2. AI 진영의 Castle, 병영, 금광이 초월 계열 외형인지 확인한다
3. AI 유닛이 생산되어 아군 방향으로 이동하는지 확인한다
4. AI 유닛과 전투가 발생할 때 정상적으로 피해 주고받기가 이루어지는지 확인한다

**기댓값:**
- AI 진영의 모든 건물이 초월 계열 외형으로 표시된다
- AI 유닛이 초월 계열 외형으로 생산되고 정상 이동 및 전투를 수행한다

**결과:** PASS

---

### TC-SINGLE-004: 플레이어와 AI가 동일한 종족이 되었을 때 정상 동작 확인 (미러 매치)

**전제:** 로비에서 특정 종족을 선택하고 싱글플레이를 시작한 상태

**동작:**
1. 로비에서 인간(또는 특정 종족)을 선택하고 게임을 시작한다
2. AI도 동일한 종족으로 결정될 때까지 반복 진입한다 (확률에 따라 여러 번 필요할 수 있음)
3. 양쪽 모두 동일한 외형의 유닛과 건물이 표시되는지 확인한다
4. 게임이 정상적으로 진행되는지(이동, 전투, 생산 등) 확인한다

**기댓값:**
- 양 팀이 동일한 종족 외형으로 표시된다
- 게임 진행에 오류 없이 정상 동작한다

**결과:** PASS

---

## QA 섹션 (qa-tester 전용)

### 정적 분석 결과 (qa-tester)

**분석 일자**: 2026-04-24

---

#### 1. 변경 코드 직접 확인 (`GameBootstrapper.cs:280-289`)

실제 적용된 코드 확인 완료.

- 283번째 줄 주석에 "Red 팀(AI)은 RaceId에 정의된 모든 종족 중 무작위로 결정한다", "새 종족이 추가되어도 자동으로 후보에 포함됨" 명시
- `if (isNetworkMode)` 의 `else` 분기 내부에만 존재 — 멀티플레이 코드 경로와 완전히 분리됨
- `LoadMap(HexOrientation.FlatTop)` 호출(289번째 줄) 이전에 `GameRaceContext.Set()` 호출 — 기존 선행 조건 유지

#### 2. RaceId enum 값 범위 확인 (`Domain/Common/RaceId.cs`)

- `Human(0)`, `Spirit(1)`, `Transcendence(2)` — 총 3개
- `Enum.GetValues(typeof(RaceId))`는 이 3개를 반환
- `UnityEngine.Random.Range(0, 3)`: 0, 1, 2 반환 (max exclusive) — 배열 경계 안전
- **컴파일 에러 없음, 범위 초과 없음**

#### 3. GameRaceContext.Set() 시그니처 확인 (`Infrastructure/GameRaceContext.cs:50`)

- `public static void Set(RaceId blueRace, RaceId redRace)` — 2개 파라미터
- 호출부: `GameRaceContext.Set(LocalPlayerRace.Current, opponentRace)` — 타입 일치 (`RaceId`, `RaceId`)
- **타입 불일치 없음**

#### 4. [FAIL 발견] BattleViewModel.cs:199 — 중복 덮어쓰기 문제

`Presentation/UI/ViewModels/BattleViewModel.cs`의 `LoadSingleplayScene()` 메서드:

```
// 199번째 줄
GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human);
```

**실행 순서**:
1. 로비에서 싱글플레이 버튼 클릭 → `BattleViewModel.LoadSingleplayScene()` 호출
2. `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` — AI 종족을 **Human으로 고정 설정**
3. `SceneManager.LoadScene("Game")` — Game 씬 전환
4. Game 씬 초기화 → `GameBootstrapper.Awake()` → `GameRaceContext.Set(LocalPlayerRace.Current, opponentRace)` — 랜덤 종족으로 올바르게 재설정

씬 전환 완료 후 GameBootstrapper가 덮어쓰기 때문에 **최종 결과는 랜덤**으로 수렴한다. 그러나 BattleViewModel의 199번째 줄은 이번 작업 전 코드 주석("다음 작업(AI 종족 선택)에서 의미 있는 값으로 변경 예정")이 그대로 남아 있어 수정이 완료됐음을 반영하지 못하고 있다.

- 기능적 오류: 없음 (GameBootstrapper가 최종 값을 설정)
- 코드 일관성 문제: 있음 — BattleViewModel에서 `RaceId.Human` 하드코딩이 잔존, 주석도 미완료 상태
- **심각도**: Minor

#### 5. 멀티플레이 코드 경로 영향 없음 확인

- `NetworkGameFlow.cs:177`: `GameRaceContext.Set((RaceId)blueRace, (RaceId)redRace)` — 서버 수신값으로 설정, 독립적
- `if (isNetworkMode)` 분기로 완전히 격리됨 — 이번 변경과 무관

---

#### TC 판정 요약

| TC ID | 제목 | 판정 | 사유 |
|-------|------|------|------|
| SINGLE-001 | 게임 재시작 시 AI 종족이 달라지는지 확인 | PASS | 실기 확인 완료 |
| SINGLE-002 | AI가 정령(Spirit) 종족으로 설정될 때 정상 동작 확인 | PASS | 실기 확인 완료 |
| SINGLE-003 | AI가 초월(Transcendence) 종족으로 설정될 때 정상 동작 확인 | PASS | 실기 확인 완료 |
| SINGLE-004 | 플레이어와 AI가 동일한 종족이 될 때 정상 동작 확인 (미러 매치) | PASS | 실기 확인 완료 |

---

#### Minor 이슈 — BattleViewModel.cs 잔존 코드

- **파일**: `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs:199`
- **내용**: `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 하드코딩 잔존
- **영향**: 기능 오류 없음 (GameBootstrapper가 최종 덮어씀). 코드 가독성 및 일관성 저하.
- **제안**: 해당 줄을 제거하거나, GameBootstrapper에서 이미 설정하므로 BattleViewModel에서의 선행 설정 자체를 삭제 검토
