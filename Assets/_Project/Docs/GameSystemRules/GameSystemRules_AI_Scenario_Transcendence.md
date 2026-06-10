# AI 빌드오더 시나리오 — Transcendence 종족

Transcendence 종족 AI가 사용하는 빌드오더 시나리오를 정의하는 문서이다.
Phase 구조, 반응 시스템, 건물 배치 로직 등 공통 구현 규칙은 `GameSystemRules_AI.md`를 참조한다.

---

## 목차

- [1. 시나리오 선택](#1-시나리오-선택)
- [2. 공통 규칙](#2-공통-규칙)
- [3. Phase 타이밍 기준](#3-phase-타이밍-기준)
- [4. 생산 건물 라인 정의](#4-생산-건물-라인-정의)
- [5. 골드 수지 검토](#5-골드-수지-검토)
- [6. 시나리오 A — Trans-Rush (초반 물량형)](#6-시나리오-a--trans-rush-초반-물량형)
- [7. 시나리오 B — Trans-Flora (동물A + 식물 균형형)](#7-시나리오-b--trans-flora-동물a--식물-균형형)
- [8. 시나리오 C — Trans-Beast (동물 고테크형)](#8-시나리오-c--trans-beast-동물-고테크형)

---

## 1. 시나리오 선택

게임 시작 시 A / B / C 중 하나를 균등 확률(각 33.3%)로 무작위 선택한다.
선택은 난이도와 무관하며, `GameBootstrapper.InitializeAI()` 호출 시점에 결정된다.

| 시나리오 | 선택 확률 |
|---------|---------|
| A — Trans-Rush (초반 물량형) | 33.3% |
| B — Trans-Flora (동물A + 식물 균형형) | 33.3% |
| C — Trans-Beast (동물 고테크형) | 33.3% |

난이도(쉬움/보통/어려움)는 선택된 시나리오의 `delaySeconds` 배율과 `goldIncomeMultiplier`로만 반영된다.
시나리오 자체는 변경되지 않는다.

> **구현 참고**: `AIScenarioConfig_Transcendence.asset` 내에 3개 시나리오 배열을 담고,
> `UnityEngine.Random.Range(0, 3)` 인덱스로 하나를 선택하여 `AIOpponentController`에 주입한다.

---

## 2. 공통 규칙

- 채굴소(FungalNode)는 Phase 2와 Phase 4에 각 1개씩 확보한다.
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

## 4. 생산 건물 라인 정의

| 라인 | Stage 1 | Stage 2 | Stage 3 |
|------|---------|---------|---------|
| 0 — 동물A 라인 | PrimalAltar | PrimalDen | PrimalSanctuary |
| 1 — 동물B 라인 | FeralAltar | FeralDen | FeralSanctuary |
| 2 — 식물 라인 | SporePatch | FloralNursery | — (2단계까지) |

### 건물-유닛 매핑

| 라인 | requiredStage=1 | requiredStage=2 | requiredStage=3 |
|------|----------------|----------------|----------------|
| 0 — 동물A | RabbitTrickster | LionKnight | BearGuard |
| 1 — 동물B | FoxMagician | EagleArcher | RhinoBreaker |
| 2 — 식물 | MushroomBomber | BloomFairy | — |

> 식물 라인은 2단계(FloralNursery)가 최상위다.
> BloomFairy는 공격 불가 힐 전용 유닛이므로 독립 주력 라인이 아닌 보조 운용을 전제한다.

---

## 5. 골드 수지 검토

### Transcendence 종족 건물 비용 구조

| 항목 | 비용 |
|------|------|
| 생산 건물 1단계 건설 | 125g (Human 100g, Spirit 75g보다 고가) |
| 생산 건물 1→2단계 업그레이드 | 200g |
| 생산 건물 2→3단계 업그레이드 | 300g |
| 채굴소(FungalNode) | 100g (Human/Spirit 50g보다 고가) |
| **단일 라인 3단계까지 총 비용** | **625g** |

### 시나리오별 건물 총 투자

| 시나리오 | 건물 투자 합계 |
|---------|-------------|
| A — Trans-Rush | 1,150g |
| B — Trans-Flora | 1,275g |
| C — Trans-Beast | 1,450g |

### 가용 골드 검토

| 시점 | 누적 수입 |
|------|---------|
| Phase 1 종료 (3분) | 500g (시작 골드만, 채굴소 없음) |
| Phase 2 종료 (6분) | +1,800g (FungalNode 1개 × 10g/s × 180s) |
| Phase 3 종료 (9분) | +1,800g 추가 |
| 10분 기준 이론 최대 | 약 **5,300g** |

**결론**: 최고 비용인 Trans-Beast(1,450g)도 5,300g 대비 27%로 유닛 생산 여유가 충분하다. ✓

> Transcendence 건물 1단계 건설이 125g으로 비싸 Phase 2에 두 번째 라인을 여는 타이밍이 중요하다.
> 골드 부족 시 `retryInterval`(10초) 가드 메커니즘이 자동으로 재시도한다.

---

## 6. 시나리오 A — Trans-Rush (초반 물량형)

**전략**: Phase 1~2에 동물A(RabbitTrickster) + 동물B(FoxMagician) 두 라인을 동시 개설해 값싼 1단계 유닛으로 조기 압박.
Phase 3에 양 라인을 2단계로 동시 업그레이드해 LionKnight + EagleArcher 전환.
Phase 4에 동물B 라인을 3단계(RhinoBreaker)까지 완성.
**10분 목표**: LionKnight(동물A 2단계) + RhinoBreaker(동물B 3단계) 동시 운용.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | PrimalAltar | 0 | 5초 |
| 2 | StartProduction | RabbitTrickster | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | FeralAltar | 1 | 15초 |
| 2 | StartProduction | FoxMagician | 1 | 35초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | PrimalDen | 0 | 10초 |
| 2 | StartProduction | LionKnight | 0 | 35초 |
| 3 | UpgradeBuilding | FeralDen | 1 | 50초 |
| 4 | StartProduction | EagleArcher | 1 | 75초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | FeralSanctuary | 1 | 20초 |
| 2 | StartProduction | RhinoBreaker | 1 | 55초 |

---

## 7. 시나리오 B — Trans-Flora (동물A + 식물 균형형)

**전략**: 동물A 라인을 Phase 3에서 3단계(PrimalSanctuary)까지 완성해 BearGuard(HP 200 탱커)를 확보.
Phase 2에 식물 라인(SporePatch)을 개설하고 Phase 3에 FloralNursery로 업그레이드해 BloomFairy(힐 서포트) 추가.
BearGuard의 높은 체력과 BloomFairy의 힐이 맞물려 후반 전선을 안정적으로 유지.
Phase 4에 동물B 라인(FoxMagician)을 추가 개설해 원거리 화력 보강.
**10분 목표**: BearGuard(동물A 3단계) + BloomFairy(식물 2단계) + FoxMagician(동물B 1단계).

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | PrimalAltar | 0 | 5초 |
| 2 | StartProduction | RabbitTrickster | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | PrimalDen | 0 | 20초 |
| 2 | StartProduction | LionKnight | 0 | 45초 |
| 3 | PlaceBuilding | SporePatch | 2 | 70초 |
| 4 | StartProduction | MushroomBomber | 2 | 90초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | PrimalSanctuary | 0 | 10초 |
| 2 | StartProduction | BearGuard | 0 | 55초 |
| 3 | UpgradeBuilding | FloralNursery | 2 | 65초 |
| 4 | StartProduction | BloomFairy | 2 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | FeralAltar | 1 | 15초 |
| 2 | StartProduction | FoxMagician | 1 | 35초 |

---

## 8. 시나리오 C — Trans-Beast (동물 고테크형)

**전략**: 동물B 라인을 우선 3단계(FeralSanctuary)까지 완성해 RhinoBreaker를 먼저 확보.
동물A 라인은 Phase 2에 개설해 Phase 4에서 3단계(PrimalSanctuary)로 완성, BearGuard 추가.
두 동물 라인 모두 3단계에 도달하는 것이 최종 목표. 가장 고비용이나 후반 최강 조합.
**Phase 4 목표**: RhinoBreaker(동물B 3단계) + BearGuard(동물A 3단계) 동시 운용.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | FeralAltar | 1 | 5초 |
| 2 | StartProduction | FoxMagician | 1 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | FeralDen | 1 | 20초 |
| 2 | StartProduction | EagleArcher | 1 | 45초 |
| 3 | PlaceBuilding | PrimalAltar | 0 | 70초 |
| 4 | StartProduction | RabbitTrickster | 0 | 90초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | FeralSanctuary | 1 | 10초 |
| 2 | StartProduction | RhinoBreaker | 1 | 40초 |
| 3 | UpgradeBuilding | PrimalDen | 0 | 60초 |
| 4 | StartProduction | LionKnight | 0 | 85초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | PrimalSanctuary | 0 | 15초 |
| 2 | StartProduction | BearGuard | 0 | 60초 |
