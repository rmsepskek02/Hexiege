# AI 빌드오더 시나리오 — Human 종족

Human 종족 AI가 사용하는 빌드오더 시나리오를 정의하는 문서이다.
Phase 구조, 반응 시스템, 건물 배치 로직 등 공통 구현 규칙은 `GameSystemRules_AI.md`를 참조한다.

---

## 목차

- [1. 시나리오 선택](#1-시나리오-선택)
- [2. 공통 규칙](#2-공통-규칙)
- [3. Phase 타이밍 기준](#3-phase-타이밍-기준)
- [4. 시나리오 A — 물량형 (Mass)](#4-시나리오-a--물량형-mass)
- [5. 시나리오 B — 테크형 (Tech)](#5-시나리오-b--테크형-tech)
- [6. 시나리오 C — 균형형 (Balanced)](#6-시나리오-c--균형형-balanced)

---

## 1. 시나리오 선택

게임 시작 시 A / B / C 중 하나를 균등 확률(각 33.3%)로 무작위 선택한다.
선택은 난이도와 무관하며, `GameBootstrapper.InitializeAI()` 호출 시점에 결정된다.

| 시나리오 | 선택 확률 |
|---------|---------|
| A — 물량형 (Mass) | 33.3% |
| B — 테크형 (Tech) | 33.3% |
| C — 균형형 (Balanced) | 33.3% |

난이도(쉬움/보통/어려움)는 선택된 시나리오의 `delaySeconds` 배율과 `goldIncomeMultiplier`로만 반영된다.
시나리오 자체는 변경되지 않는다.

> **구현 참고**: `AIScenarioConfig_Human.asset` 내에 3개 시나리오 배열을 담고,
> `UnityEngine.Random.Range(0, 3)` 인덱스로 하나를 선택하여 `AIOpponentController`에 주입한다.

---

## 2. 공통 규칙

- 채굴소(MiningPost)는 Phase 2와 Phase 4에 각 1개씩 확보한다.
  단, Build Order 항목에 포함되지 않는다. Phase 진입 시 자동 시작되는 병행 트랙이 독립적으로 처리한다.
  상세 동작은 `GameSystemRules_AI.md` 섹션 6 참조.
- 각 Phase 내 항목은 순서대로 실행되며, 앞 항목이 완료되지 않으면 다음 항목은 실행되지 않는다.
- `delaySeconds`는 해당 Phase 시작 후 해당 항목까지의 최소 대기 시간(보통 난이도 기준)이다.
- 쉬움: delay × 1.5 / 어려움: delay × 0.7 기준으로 조정 예정.

> ⚠️ 아래 시나리오의 delay 수치는 10분 플레이타임 기준 보통 난이도 예비값이다.
> StatsReference.md 스탯(건물 건설 시간, 유닛 비용) 확정 후 실제 경제 시뮬레이션을 통해 재조정한다.

---

## 3. Phase 타이밍 기준

| Phase | 시간 |
|-------|------|
| Phase 1 | 0~3분 |
| Phase 2 | 3~6분 |
| Phase 3 | 6~9분 |
| Phase 4 | 9분~종료 (최대 12분) |

---

## 4. 시나리오 A — 물량형 (Mass)

**전략**: 업그레이드보다 생산 라인 확장 우선. 1~2단계 유닛을 대량 생산해 물량으로 압도.
**최종 목표**: 근거리 3단계(BattleAxe) + 총기 3단계(Sniper). 탈것 없음.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | TrainingCamp | 0 | 5초 |
| 2 | StartProduction | LittleKnight | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | Gunsmith | 1 | 20초 |
| 2 | StartProduction | Pistoleer | 1 | 35초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | WarAcademy | 0 | 5초 |
| 2 | StartProduction | SpearMan | 0 | 30초 |
| 3 | UpgradeBuilding | Armory | 1 | 60초 |
| 4 | StartProduction | Assault | 1 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | HumanBarracks | 0 | 15초 |
| 2 | StartProduction | BattleAxe | 0 | 45초 |
| 3 | UpgradeBuilding | WeaponForge | 1 | 50초 |
| 4 | StartProduction | Sniper | 1 | 80초 |

---

## 5. 시나리오 B — 테크형 (Tech)

**전략**: 빠른 업그레이드로 고급 유닛 조기 확보. Phase 4에 탈것 2단계(Tank) 진입.
**최종 목표**: 근거리 3단계(BattleAxe) + 총기 2단계(Assault) + 탈것 2단계(Tank).

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | TrainingCamp | 0 | 5초 |
| 2 | StartProduction | LittleKnight | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | WarAcademy | 0 | 20초 |
| 2 | StartProduction | SpearMan | 0 | 45초 |
| 3 | PlaceBuilding | Gunsmith | 1 | 60초 |
| 4 | StartProduction | Pistoleer | 1 | 75초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | HumanBarracks | 0 | 5초 |
| 2 | StartProduction | BattleAxe | 0 | 35초 |
| 3 | UpgradeBuilding | Armory | 1 | 60초 |
| 4 | StartProduction | Assault | 1 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | Garage | 2 | 15초 |
| 2 | StartProduction | CannonCart | 2 | 30초 |
| 3 | UpgradeBuilding | VehicleBay | 2 | 60초 |
| 4 | StartProduction | Tank | 2 | 90초 |

---

## 6. 시나리오 C — 균형형 (Balanced)

**전략**: 근거리+총기 업그레이드를 병행하고 Phase 4에 총기 최상급 + 탈것 라인 개설. 추가시간에 탈것 2단계 완성.
**10분 목표**: 근거리 3단계(BattleAxe) + 총기 3단계(Sniper) + 탈것 1단계(CannonCart).
**추가시간 목표**: 탈것 2단계(Tank) 완성.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | TrainingCamp | 0 | 5초 |
| 2 | StartProduction | LittleKnight | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | WarAcademy | 0 | 20초 |
| 2 | StartProduction | SpearMan | 0 | 45초 |
| 3 | PlaceBuilding | Gunsmith | 1 | 60초 |
| 4 | StartProduction | Pistoleer | 1 | 75초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | HumanBarracks | 0 | 5초 |
| 2 | StartProduction | BattleAxe | 0 | 35초 |
| 3 | UpgradeBuilding | Armory | 1 | 60초 |
| 4 | StartProduction | Assault | 1 | 90초 |

**Phase 4 (후반 + 추가시간)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | WeaponForge | 1 | 15초 |
| 2 | StartProduction | Sniper | 1 | 45초 |
| 3 | PlaceBuilding | Garage | 2 | 50초 |
| 4 | StartProduction | CannonCart | 2 | 65초 |
| 5 | UpgradeBuilding | VehicleBay | 2 | 120초 ※ |
| 6 | StartProduction | Tank | 2 | 150초 ※ |

※ delay 120초 이후 항목은 추가시간(10~12분) 구간에 해당한다.
