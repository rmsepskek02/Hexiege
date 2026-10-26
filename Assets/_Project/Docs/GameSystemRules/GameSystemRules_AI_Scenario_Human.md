# AI 빌드오더 시나리오 — Human 종족

- **목적·답하는 질문:** Human 종족 AI 빌드오더의 기존 선택·진행 내용을 찾는다.
- **포함:** 시나리오 A/B/C, Phase 시점, 건물/유닛 선택·생산 순서와 골드 수지 검토
- **제외·담당 문서:** Phase·반응·가드·배치 공통 동작은 [AI](GameSystemRules_AI.md), 실제 스탯은 [StatsReference](../StatsReference.md), 재논의 작업은 [ROADMAP](../ROADMAP.md)
- **권위원본·주요 참조:** 종족별 시나리오 자료의 원본이며 밸런싱과 함께 별건 재논의 대상; 현행 수치의 확정 권위로 쓰지 않음
- **갱신 조건·방법:** 밸런싱·시나리오 재논의가 승인될 때 해당 선택·표·수지와 확정 범위를 함께 갱신한다. 이번 정리는 역할 안내만 바꾸고 시나리오 본문은 보존한다.

---

## 목차

- [1. 시나리오 선택](#1-시나리오-선택)
- [2. 공통 규칙](#2-공통-규칙)
- [3. Phase 타이밍 기준](#3-phase-타이밍-기준)
- [4. 골드 수지 검토](#4-골드-수지-검토)
- [5. 시나리오 A — 물량형 (Mass)](#5-시나리오-a--물량형-mass)
- [6. 시나리오 B — 테크형 (Tech)](#6-시나리오-b--테크형-tech)
- [7. 시나리오 C — 균형형 (Balanced)](#7-시나리오-c--균형형-balanced)

---

## 1. 시나리오 선택

게임 시작 시 A / B / C 중 하나를 균등 확률(각 33.3%)로 무작위 선택한다.
선택은 난이도와 무관하며, `GameBootstrapper.InitializeAI()` 호출 시점에 결정된다.

| 시나리오 | 선택 확률 |
|---------|---------|
| A — 물량형 (Mass) | 33.3% |
| B — 테크형 (Tech) | 33.3% |
| C — 균형형 (Balanced) | 33.3% |

난이도(쉬움/보통/어려움)의 설계 범위는 [공통 AI 규칙](GameSystemRules_AI.md)의 난이도 파라미터와 「종족 시나리오 공통 타이밍」 예비 기준을 따르며, 시나리오 대기 시간(`delaySeconds`)·골드 수입 배율(`goldIncomeMultiplier`)·생산 시간 배율(`productionTimeMultiplier`)을 포함하되 생산 시간 배율의 효과 적용은 후속 AI 작업으로 보류 중이다.
시나리오 자체는 변경되지 않는다.

> **구현 참고**: `AIScenarioConfig_Human.asset` 내에 3개 시나리오 배열을 담고,
> `UnityEngine.Random.Range(0, 3)` 인덱스로 하나를 선택하여 `AIOpponentController`에 주입한다.

---

## 2. 공통 규칙

- 채굴소(MiningPost)는 Phase 2와 Phase 4에 각 1개씩 확보한다.
  단, Build Order 항목에 포함되지 않는다. Phase 진입 시 자동 시작되는 병행 트랙이 독립적으로 처리한다.
  상세 동작은 `GameSystemRules_AI.md` 섹션 6 참조.
- 순차 실행·최소 대기 시간·난이도 지연의 예비 기준은 [AI 공통 규칙](GameSystemRules_AI.md#종족-시나리오-공통-타이밍)을 따른다.

> ⚠️ 아래 시나리오의 delay 수치는 10분 플레이타임 기준 보통 난이도 예비값이다.
> StatsReference.md 스탯(건물 건설 시간, 유닛 비용) 확정 후 실제 경제 시뮬레이션을 통해 재조정한다.

---

## 3. Phase 타이밍 기준

Phase 시간표의 원본은 [AI 공통 타이밍](GameSystemRules_AI.md#종족-시나리오-공통-타이밍)이다. 이 문서의 종족별 Phase 항목·순서·delay는 그대로 유지한다.

---

## 4. 골드 수지 검토

> **기존 예비 계산의 적용 범위:** 아래 수입·투자 계산은 작성 당시 고정 시작 골드와 초기 채굴소가 없다는 가정을 보존한 것이다. 현행 [무작위 맵 규칙](GameSystemRules_RandomMap.md) 규칙 2·3의 시작 채굴소 및 중립 광산 수별 초기 골드를 반영하지 않았으므로, 현재 모든 맵·난이도의 경제 성립 근거로 사용하지 않는다. 기존 숫자와 결론은 이 가정 아래의 계산이며 재시뮬레이션은 별도 판단 항목이다.

### Human 종족 건물 비용 구조

| 항목 | 비용 |
|------|------|
| 생산 건물 1단계 건설 | 100g |
| 생산 건물 1→2단계 업그레이드 | 100g |
| 생산 건물 2→3단계 업그레이드 | 200g |
| 채굴소(MiningPost) | 50g |
| **단일 라인 3단계까지 총 비용** | **400g** |

### 시나리오별 건물 총 투자

| 시나리오 | 건물 투자 합계 | 비고 |
|---------|-------------|------|
| A — 물량형 | 900g | 근거리A 3단계 + 총기류 3단계 + MiningPost 2개 |
| B — 테크형 | 1,200g | 근거리A 3단계 + 총기류 2단계 + 탈것류 2단계 + MiningPost 2개 |
| C — 균형형 | 1,400g | 근거리A 3단계 + 총기류 3단계 + 탈것류 2단계 + MiningPost 2개 |

### 가용 골드 검토

| 시점 | 누적 수입 |
|------|---------|
| Phase 1 종료 (3분) | 500g (시작 골드만, 채굴소 없음) |
| Phase 2 종료 (6분) | +1,800g (MiningPost 1개 × 10g/s × 180s) |
| Phase 3 종료 (9분) | +1,800g 추가 |
| 10분 기준 이론 최대 | 약 **5,300g** |

**결론**: 최고 비용인 균형형(1,400g)도 5,300g 대비 26%로 유닛 생산 여유가 충분하다. ✓
시나리오 C의 추가시간 VehicleBay 업그레이드(300g, delay 120초)는 11분 시점(누적 ~6,500g)에 실행되므로 문제없다. ✓

---

## 5. 시나리오 A — 물량형 (Mass)

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

## 6. 시나리오 B — 테크형 (Tech)

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

## 7. 시나리오 C — 균형형 (Balanced)

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
