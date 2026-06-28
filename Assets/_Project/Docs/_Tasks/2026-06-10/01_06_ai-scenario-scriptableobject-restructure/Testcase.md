# Testcase — AI 시나리오 ScriptableObject 구조 개편

## 개요

이 테스트 문서는 AI 시나리오 에셋 구조를 종족별 단일 에셋(3개 시나리오 내장)으로 개편한 작업을 검증한다.
변경 전에는 Human_A/B/C 에셋 3개가 별도로 존재했으나, 변경 후에는 Human/Spirit/Transcendence
각 종족별 에셋 1개에 3개 시나리오가 묶여 있다. AI가 게임 시작 시 올바른 종족의 에셋을 로드하고
3개 중 하나를 무작위로 선택해 빌드오더를 수행하는지를 검증한다.

---

## 테스트 케이스 목록

### SINGLE-1: Human 종족 AI의 시나리오 무작위 선택

**전제:** 싱글플레이 모드에서 AI 팀(Red) 종족이 Human으로 설정되어 있고, AI가 활성화된 상태이다.

**동작:**
1. 싱글플레이 게임을 시작한다.
2. 게임 시작 직후 Unity 콘솔 로그를 확인한다.

**기댓값:**
- 콘솔에 "AI 초기화 완료"와 함께 선택된 시나리오 이름이 출력된다.
- 선택된 시나리오 이름은 "Human-Rush", "Human-Tech", "Human-Balance" 세 가지 중 하나이다.
- 게임을 여러 번 시작하면 세 시나리오 중 임의의 것이 선택된다(고정 패턴 없음).

**결과:**

---

### SINGLE-2: Human AI의 Phase별 빌드오더 수행

**전제:** SINGLE-1 조건과 동일하게 Human AI가 활성화된 싱글플레이 게임이 시작된 상태이다.

**동작:**
1. 게임을 시작하고 AI가 행동하는 것을 충분한 시간 동안 관찰한다.
2. Phase 1(초반): AI가 첫 번째 생산 건물을 배치하는지 확인한다.
3. Phase 2(중반): 이전 Phase 완료 후 AI가 추가 건물 배치 또는 업그레이드를 진행하는지 확인한다.
4. 각 Phase에서 AI가 유닛 생산을 시작하는지 확인한다.

**기댓값:**
- AI는 Phase 순서대로 빌드오더를 실행한다(Phase 1 완료 후 Phase 2 진행).
- 각 Phase 안에서 건물 배치 → 유닛 생산 시작 순서가 지켜진다.
- 빌드오더 실행 시마다 해당 Phase와 액션 내용이 로그에 남는다.

**결과:**

---

### SINGLE-3: Spirit 종족 AI의 시나리오 에셋 사용

**전제:** 싱글플레이 모드에서 AI 팀(Red) 종족이 Spirit으로 설정되어 있고, AI가 활성화된 상태이다.

**동작:**
1. Red 팀 종족을 Spirit으로 설정하고 싱글플레이 게임을 시작한다.
2. 게임 시작 직후 Unity 콘솔 로그를 확인한다.

**기댓값:**
- 콘솔에 "AI 초기화 완료"와 함께 선택된 시나리오 이름이 출력된다.
- 선택된 시나리오 이름은 "Spirit-Inferno", "Spirit-Torrent", "Spirit-Quake" 세 가지 중 하나이다.
- Human 시나리오(Human-Rush 등)가 사용되지 않는다.

**결과:**

---

### SINGLE-4: Transcendence 종족 AI의 시나리오 에셋 사용

**전제:** 싱글플레이 모드에서 AI 팀(Red) 종족이 Transcendence로 설정되어 있고, AI가 활성화된 상태이다.

**동작:**
1. Red 팀 종족을 Transcendence로 설정하고 싱글플레이 게임을 시작한다.
2. 게임 시작 직후 Unity 콘솔 로그를 확인한다.

**기댓값:**
- 콘솔에 "AI 초기화 완료"와 함께 선택된 시나리오 이름이 출력된다.
- 선택된 시나리오 이름은 "Trans-Rush", "Trans-Flora", "Trans-Beast" 세 가지 중 하나이다.
- Human이나 Spirit 시나리오가 사용되지 않는다.

**결과:**

---

### SINGLE-5: 레거시 에셋 없이 AI 정상 초기화

**전제:** 프로젝트에 구버전 Human_A/Human_B/Human_C 에셋이 삭제된 상태이고, 신규 Human.asset만 존재한다. AI가 활성화된 싱글플레이 상태이다.

**동작:**
1. 싱글플레이 게임을 시작한다.
2. 콘솔에 에셋 로드 실패 관련 에러가 없는지 확인한다.
3. AI가 정상적으로 행동하는지(건물 배치, 유닛 생산) 관찰한다.

**기댓값:**
- 구버전 에셋 관련 에러 로그가 없다.
- AI가 정상적으로 초기화되고 빌드오더를 수행한다.
- 게임 진행에 지장이 없다.

**결과:**

---

### MULTI-1: 멀티플레이 중 AI 비활성화 유지

에이전트 실기 불가 — 사용자 확인 필요

**전제:** 멀티플레이 모드(Host + Client 구성)에서 두 플레이어가 접속한 상태이다.

**동작:**
1. Host와 Client가 각각 접속하여 멀티플레이 게임을 시작한다.
2. 게임 시작 후 AI 관련 로그 및 AI 행동(건물 자동 배치 등)이 있는지 확인한다.

**기댓값:**
- 멀티플레이 게임에서 AI 컨트롤러가 생성되지 않는다.
- Red 팀이 자동으로 건물을 배치하거나 유닛을 생산하는 행동이 없다.

**결과:**

---

## 정적 분석 결과 (qa-tester)

### 분석 일자: 2026-06-11

### STEP 1 — Grep 전수 검색 결과

| 검색 패턴 | 결과 | 판정 |
|-----------|------|------|
| `.Steps\b` (구버전 프로퍼티) | 매칭 없음 | PASS |
| `_steps` (레거시 필드) | 주석 내 언급만 존재 (AIScenarioConfig.cs 23행 — 레거시 설명 코멘트) | PASS |
| `LoadRandomHumanScenario` (레거시 메서드) | 매칭 없음 | PASS |
| `AIScenarioConfig_Human_A\|B\|C` | 주석·XML doc 내 언급만 존재 (코드 경로로 사용되지 않음) | PASS |
| `Human_A\|B\|C` (하드코딩 경로) | AIScenarioConfig.cs 22~23행 주석, 51~52행 XML doc 내 예시 표기만 존재 | PASS |
| `Infrastructure.DifficultyLevel\|BuildOrderStep\|AIActionType` | 매칭 없음 | PASS |

비고: AIScenarioConfig.cs 62행 XML doc에 "AIScenarioConfig_Human_A.asset 등"이라는 구버전 경로 설명이 남아있고, 51~52행 Tooltip에 "Human_A" 예시가 있으나 실행 코드 경로가 아닌 주석/doc이므로 기능 영향 없음. Minor 수준 문서 정리 대상.

---

### STEP 2 — 핵심 파일 분석

#### AIScenarioConfig.cs (Infrastructure/Config)
- `_steps` 필드: 완전히 제거됨. `ScenarioBundle.steps` 필드로 대체됨.
- `scenarios` 배열: `List<ScenarioBundle>`으로 정의됨 (76행).
- `AIActionType`, `BuildOrderStep` 타입 정의: 파일에 없음. `using Hexiege.Domain`으로 참조.
- PASS

#### BuildOrderStep.cs (Domain/AI)
- 네임스페이스: `Hexiege.Domain` — 올바름.
- `using System` 만 사용. UnityEngine 의존 없음 — PASS.
- `[Serializable]` 유지 (Inspector 직렬화에 필요, System.Serializable이므로 Unity 의존 아님) — 올바름.
- `AIActionType` enum: PlaceBuilding(0), UpgradeBuilding(1), StartProduction(2) 정의됨.
- PASS

#### DifficultyLevel.cs (Domain/AI)
- 네임스페이스: `Hexiege.Domain` — 올바름.
- using 없음. UnityEngine 의존 없음 — PASS.
- PASS

#### AIOpponentController.cs (Application/Services)
- `using Hexiege.Domain` + `using Hexiege.Infrastructure` 사용.
- Infrastructure 참조: `DifficultyParams`(Infrastructure struct), `GameRaceContext`(Infrastructure static class) 직접 참조 존재.
- 파일 상단 주석에 "기존 LoginUseCase도 Application에서 Infrastructure를 참조하는 선례가 있다"고 명시되어 있어 의도된 설계임을 주석으로 설명하고 있음.
- 생성자 시그니처: `IReadOnlyList<BuildOrderStep>` + `string scenarioName` 수신 — 신규 설계 적용 확인.
- `AIScenarioConfig`를 직접 받지 않음 — 올바름.
- PASS (아키텍처 의도적 예외 — 주석에 근거 명시됨)

#### GameBootstrapper.Setup.cs (Bootstrap)
- `LoadScenarioBundleForRace()` 신규 메서드 존재 (636~681행).
- switch 분기: `RaceId.Human`, `RaceId.Spirit`, `RaceId.Transcendence` 3종족 모두 커버 + `default` 경고 분기 존재 (643~659행).
- 종족 결정 기준: `GameRaceContext.RedRace` 사용 (639행) — AI는 Red팀이므로 올바름.
- null 방어: `config == null` 체크 (663~667행), `config.scenarios == null || config.scenarios.Count == 0` 체크 (669~673행) 존재.
- 레거시 `LoadRandomHumanScenario` 메서드: 완전 삭제됨.
- `_aiController?.Dispose()` 재경기 안전성 처리 있음 (566~568행).
- PASS

---

### STEP 3 — 아키텍처 검증

| 검증 항목 | 결과 |
|-----------|------|
| `AIOpponentController`(Application)에서 `Hexiege.Infrastructure` 직접 참조 | `DifficultyParams`, `GameRaceContext` 참조 존재. 설계 의도 주석 있음(파일 상단 + `GameRaceForAi()` 주석). 허용된 예외. |
| `BuildOrderStep` / `AIActionType` / `DifficultyLevel` 이 Hexiege.Domain에만 정의 | 세 타입 모두 `Hexiege.Domain` 네임스페이스에만 정의됨. Infrastructure/Application에 중복 정의 없음. PASS |
| Domain 레이어 UnityEngine 의존 없음 | `BuildOrderStep.cs`: `using System`만 사용. `DifficultyLevel.cs`: using 없음. PASS |

---

### STEP 4 — 에셋 YAML 검증

#### AIScenarioConfig_Human.asset
- `_steps` 필드: 없음 — PASS
- `scenarios` 배열: 존재 (3개: Human-Rush, Human-Tech, Human-Balance) — PASS
- 시나리오 이름: Human-Rush / Human-Tech / Human-Balance — PASS
- actionType 범위 확인:
  - Human-Rush: 0, 2, 0, 2, 1, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
  - Human-Tech: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 0, 2, 1, 2 → 모두 0~2 범위 내 — PASS
  - Human-Balance: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 1, 2, 0, 2, 1, 2 → 모두 0~2 범위 내 — PASS
- phaseIndex 범위 확인:
  - Human-Rush: 0, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3 → 모두 0~3 범위 내 — PASS
  - Human-Tech: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3 → 모두 0~3 범위 내 — PASS
  - Human-Balance: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3 → 모두 0~3 범위 내 — PASS

#### AIScenarioConfig_Spirit.asset
- `_steps` 필드: 없음 — PASS
- `scenarios` 배열: 존재 (3개: Spirit-Inferno, Spirit-Torrent, Spirit-Quake) — PASS
- 시나리오 이름: Spirit-Inferno / Spirit-Torrent / Spirit-Quake — PASS
- actionType 범위 확인:
  - Spirit-Inferno: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
  - Spirit-Torrent: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
  - Spirit-Quake: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
- phaseIndex 범위 확인:
  - Spirit-Inferno: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS
  - Spirit-Torrent: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS
  - Spirit-Quake: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS

#### AIScenarioConfig_Transcendence.asset
- `_steps` 필드: 없음 — PASS
- `scenarios` 배열: 존재 (3개: Trans-Rush, Trans-Flora, Trans-Beast) — PASS
- 시나리오 이름: Trans-Rush / Trans-Flora / Trans-Beast — PASS
- actionType 범위 확인:
  - Trans-Rush: 0, 2, 0, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
  - Trans-Flora: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 0, 2 → 모두 0~2 범위 내 — PASS
  - Trans-Beast: 0, 2, 1, 2, 0, 2, 1, 2, 1, 2, 1, 2 → 모두 0~2 범위 내 — PASS
- phaseIndex 범위 확인:
  - Trans-Rush: 0, 0, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS
  - Trans-Flora: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS
  - Trans-Beast: 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 → 모두 0~3 범위 내 — PASS

---

### STEP 5 — 기능 로직 검증

| 검증 항목 | 결과 |
|-----------|------|
| `LoadScenarioBundleForRace()` switch가 3종족 모두 커버 | Human / Spirit / Transcendence + default 경고 분기 모두 존재 (GameBootstrapper.Setup.cs 643~659행). PASS |
| 종족 분기 기준이 `GameRaceContext.RedRace` | 639행: `RaceId aiRace = GameRaceContext.RedRace;` 확인. PASS |
| null/빈 배열 방어 코드 | `config == null` 체크(663행), `scenarios == null || Count == 0` 체크(669행) 존재. PASS |
| 레거시 에셋 파일 실물 삭제 확인 | `AIScenarioConfig_Human_A/B/C.asset` Glob 검색 결과 0건 — 삭제 확인. PASS |

---

### 발견된 이슈 목록

| ID | 심각도 | 설명 | 위치 | 판정 영향 |
|----|--------|------|------|-----------|
| NOTE-001 | Minor (문서) | AIScenarioConfig.cs 62행 XML doc에 구버전 에셋 경로("AIScenarioConfig_Human_A.asset 등") 잔존. 51~52행 Tooltip에 "Human_A" 예시 잔존. 기능에는 무영향이나 신규 개발자 혼란 야기 가능. | AIScenarioConfig.cs 51~52, 62행 | 기능 PASS — 문서 정리 권장 |

---

### 종합 판정

| TC | 정적 분석 | 실기 |
|----|-----------|------|
| SINGLE-1 | CONDITIONAL PASS | 사용자 실기 확인 필요 |
| SINGLE-2 | CONDITIONAL PASS | 사용자 실기 확인 필요 |
| SINGLE-3 | CONDITIONAL PASS | 사용자 실기 확인 필요 |
| SINGLE-4 | CONDITIONAL PASS | 사용자 실기 확인 필요 |
| SINGLE-5 | PASS (레거시 에셋 삭제 확인) | 사용자 실기 확인 필요 |
| MULTI-1 | 에이전트 실기 불가 — 사용자 확인 필요 | 사용자 실기 확인 필요 |

**정적 분석 최종 판정: CONDITIONAL PASS**

코드 구조, 아키텍처, 에셋 데이터 모두 설계 의도에 맞게 구현되어 있다.
레거시 필드/메서드/에셋이 실행 경로에서 완전히 제거되었고, 세 종족 모두 올바른 에셋 경로로 분기된다.
실기 테스트(사용자가 Unity Editor에서 직접 게임을 실행하여 콘솔 로그 및 AI 행동 확인)가 완료되어야 최종 PASS 판정을 내릴 수 있다.
