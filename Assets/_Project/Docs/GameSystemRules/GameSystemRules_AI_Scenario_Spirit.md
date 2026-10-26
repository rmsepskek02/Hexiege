# AI 빌드오더 시나리오 — Spirit 종족

- **목적·답하는 질문:** Spirit 종족 AI 빌드오더의 기존 선택·진행 내용을 찾는다.
- **포함:** 시나리오 A/B/C, Phase 시점, 건물/유닛 선택·생산 순서와 골드 수지 검토
- **제외·담당 문서:** Phase·반응·가드·배치 공통 동작은 [AI](GameSystemRules_AI.md), 실제 스탯은 [StatsReference](../StatsReference.md), 재논의 작업은 [ROADMAP](../ROADMAP.md)
- **권위원본·주요 참조:** 종족별 시나리오 자료의 원본이며 밸런싱과 함께 별건 재논의 대상; 현행 수치의 확정 권위로 쓰지 않음
- **갱신 조건·방법:** 밸런싱·시나리오 재논의가 승인될 때 해당 선택·표·수지와 확정 범위를 함께 갱신한다. 이번 정리는 역할 안내만 바꾸고 시나리오 본문은 보존한다.

---

## 목차

- [1. 시나리오 선택](#1-시나리오-선택)
- [2. 공통 규칙](#2-공통-규칙)
- [3. Phase 타이밍 기준](#3-phase-타이밍-기준)
- [4. 생산 건물 라인 정의](#4-생산-건물-라인-정의)
- [5. 골드 수지 검토](#5-골드-수지-검토)
- [6. 시나리오 A — Spirit-Inferno (불 집중형)](#6-시나리오-a--spirit-inferno-불-집중형)
- [7. 시나리오 B — Spirit-Torrent (물 집중형)](#7-시나리오-b--spirit-torrent-물-집중형)
- [8. 시나리오 C — Spirit-Quake (땅 집중형)](#8-시나리오-c--spirit-quake-땅-집중형)

---

## 1. 시나리오 선택

게임 시작 시 A / B / C 중 하나를 균등 확률(각 33.3%)로 무작위 선택한다.
선택은 난이도와 무관하며, `GameBootstrapper.InitializeAI()` 호출 시점에 결정된다.

| 시나리오 | 선택 확률 |
|---------|---------|
| A — Spirit-Inferno (불 집중형) | 33.3% |
| B — Spirit-Torrent (물 집중형) | 33.3% |
| C — Spirit-Quake (땅 집중형) | 33.3% |

난이도(쉬움/보통/어려움)의 설계 범위는 [공통 AI 규칙](GameSystemRules_AI.md)의 난이도 파라미터와 「종족 시나리오 공통 타이밍」 예비 기준을 따르며, 시나리오 대기 시간(`delaySeconds`)·골드 수입 배율(`goldIncomeMultiplier`)·생산 시간 배율(`productionTimeMultiplier`)을 포함하되 생산 시간 배율의 효과 적용은 후속 AI 작업으로 보류 중이다.
시나리오 자체는 변경되지 않는다.

> **구현 참고**: `AIScenarioConfig_Spirit.asset` 내에 3개 시나리오 배열을 담고,
> `UnityEngine.Random.Range(0, 3)` 인덱스로 하나를 선택하여 `AIOpponentController`에 주입한다.

---

## 2. 공통 규칙

- 채굴소(ManaRift)는 Phase 2와 Phase 4에 각 1개씩 확보한다.
  단, Build Order 항목에 포함되지 않는다. Phase 진입 시 자동 시작되는 병행 트랙이 독립적으로 처리한다.
  상세 동작은 `GameSystemRules_AI.md` 섹션 6 참조.
- 순차 실행·최소 대기 시간·난이도 지연의 예비 기준은 [AI 공통 규칙](GameSystemRules_AI.md#종족-시나리오-공통-타이밍)을 따른다.

> ⚠️ 아래 시나리오의 delay 수치는 10분 플레이타임 기준 보통 난이도 예비값이다.
> StatsReference.md 스탯(건물 건설 시간, 유닛 비용) 확정 후 실제 경제 시뮬레이션을 통해 재조정한다.

---

## 3. Phase 타이밍 기준

Phase 시간표의 원본은 [AI 공통 타이밍](GameSystemRules_AI.md#종족-시나리오-공통-타이밍)이다. 이 문서의 종족별 Phase 항목·순서·delay는 그대로 유지한다.

---

## 4. 생산 건물 라인 정의

| 라인 | Stage 1 | Stage 2 | Stage 3 |
|------|---------|---------|---------|
| 0 — 불 라인 | FireSpire | BlazeConduit | InfernoCore |
| 1 — 물 라인 | AquaSpring | TidalNexus | OceanicHeart |
| 2 — 땅 라인 | StoneMound | TerraForge | GaeaSanctum |

### 건물-유닛 매핑

| 라인 | requiredStage=1 | requiredStage=2 | requiredStage=3 |
|------|----------------|----------------|----------------|
| 0 — 불 | EmberSpirit | FlameSpirit | InfernoSpirit |
| 1 — 물 | TideSpirit | StreamSpirit | TorrentSpirit |
| 2 — 땅 | DustSpirit | BoulderSpirit | QuakeSpirit |

---

## 5. 골드 수지 검토

> **기존 예비 계산의 적용 범위:** 아래 수입·투자 계산은 작성 당시 고정 시작 골드와 초기 채굴소가 없다는 가정을 보존한 것이다. 현행 [무작위 맵 규칙](GameSystemRules_RandomMap.md) 규칙 2·3의 시작 채굴소 및 중립 광산 수별 초기 골드를 반영하지 않았으므로, 현재 모든 맵·난이도의 경제 성립 근거로 사용하지 않는다. 기존 숫자와 결론은 이 가정 아래의 계산이며 재시뮬레이션은 별도 판단 항목이다.

### Spirit 종족 건물 비용 구조

| 항목 | 비용 |
|------|------|
| 생산 건물 1단계 건설 | 75g (Human 100g보다 저렴) |
| 생산 건물 1→2단계 업그레이드 | 200g |
| 생산 건물 2→3단계 업그레이드 | 400g |
| 채굴소(ManaRift) | 50g |
| **단일 라인 3단계까지 총 비용** | **675g** |

### 시나리오별 건물 총 투자

3개 시나리오 모두 메인 라인 3단계 + 보조 라인 2단계 + Phase 4 보조 라인 3단계 구조로 동일하다.

| 항목 | 비용 |
|------|------|
| 메인 라인 (건설 + 업글 2회) | 675g |
| 보조 라인 1단계 건설 | 75g |
| 보조 라인 2단계 업글 | 200g |
| 보조 라인 3단계 업글 (Phase 4) | 400g |
| 채굴소 2개 | 100g |
| **합계** | **1,450g** |

### 가용 골드 검토

| 시점 | 누적 수입 |
|------|---------|
| Phase 1 종료 (3분) | 500g (시작 골드만, 채굴소 없음) |
| Phase 2 종료 (6분) | +1,800g (ManaRift 1개 × 10g/s × 180s) |
| Phase 3 종료 (9분) | +1,800g 추가 |
| 10분 기준 이론 최대 | 약 **5,300g** |

**결론**: 건물 투자 1,450g은 5,300g의 약 27%. 나머지 ~3,850g은 유닛 생산에 활용 가능하다. ✓

> Spirit 업그레이드 비용(200g + 400g)이 Human(100g + 200g)보다 비싸므로,
> 3단계 업글(400g)은 Phase 3 진입 직후 누적 수입이 충분한 시점(15초 지연)에 배치했다.
> 골드 부족 시 `retryInterval`(10초) 가드 메커니즘이 자동으로 재시도한다.

---

## 6. 시나리오 A — Spirit-Inferno (불 집중형)

**전략**: 불 라인을 Phase 3에서 3단계(InfernoCore)까지 빠르게 완성해 InfernoSpirit(원거리 DoT 고화력)를 조기 확보.
땅 라인(DustSpirit → BoulderSpirit)을 보조 물량으로 운용하다 Phase 4에 GaeaSanctum(3단계)까지 올려 QuakeSpirit(AoE 탱커) 추가.
**10분 목표**: InfernoSpirit(불 3단계) + BoulderSpirit(땅 2단계).
**Phase 4 목표**: QuakeSpirit(땅 3단계) 추가 생산.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | FireSpire | 0 | 5초 |
| 2 | StartProduction | EmberSpirit | 0 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | BlazeConduit | 0 | 20초 |
| 2 | StartProduction | FlameSpirit | 0 | 50초 |
| 3 | PlaceBuilding | StoneMound | 2 | 70초 |
| 4 | StartProduction | DustSpirit | 2 | 90초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | InfernoCore | 0 | 15초 |
| 2 | StartProduction | InfernoSpirit | 0 | 50초 |
| 3 | UpgradeBuilding | TerraForge | 2 | 60초 |
| 4 | StartProduction | BoulderSpirit | 2 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | GaeaSanctum | 2 | 30초 |
| 2 | StartProduction | QuakeSpirit | 2 | 70초 |

---

## 7. 시나리오 B — Spirit-Torrent (물 집중형)

**전략**: 물 라인을 Phase 3에서 3단계(OceanicHeart)까지 완성해 TorrentSpirit(파도형 AoE + 아군 힐)를 확보.
불 라인(EmberSpirit → FlameSpirit)을 보조 물량으로 운용하다 Phase 4에 InfernoCore(3단계)까지 올려 InfernoSpirit(DoT) 추가.
**10분 목표**: TorrentSpirit(물 3단계) + FlameSpirit(불 2단계).
**Phase 4 목표**: InfernoSpirit(불 3단계) 추가로 파도 + DoT 복합 전선 완성.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | AquaSpring | 1 | 5초 |
| 2 | StartProduction | TideSpirit | 1 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | TidalNexus | 1 | 20초 |
| 2 | StartProduction | StreamSpirit | 1 | 50초 |
| 3 | PlaceBuilding | FireSpire | 0 | 70초 |
| 4 | StartProduction | EmberSpirit | 0 | 90초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | OceanicHeart | 1 | 15초 |
| 2 | StartProduction | TorrentSpirit | 1 | 50초 |
| 3 | UpgradeBuilding | BlazeConduit | 0 | 60초 |
| 4 | StartProduction | FlameSpirit | 0 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | InfernoCore | 0 | 30초 |
| 2 | StartProduction | InfernoSpirit | 0 | 65초 |

---

## 8. 시나리오 C — Spirit-Quake (땅 집중형)

**전략**: 땅 라인을 Phase 3에서 3단계(GaeaSanctum)까지 완성해 QuakeSpirit(착탄형 AoE 탱커, HP는 [StatsReference](../StatsReference.md) 참조)를 조기 확보.
물 라인(TideSpirit → StreamSpirit)을 원거리 보조로 운용하다 Phase 4에 OceanicHeart(3단계)까지 올려 TorrentSpirit(힐 파도) 추가.
**10분 목표**: QuakeSpirit(땅 3단계) + StreamSpirit(물 2단계).
**Phase 4 목표**: TorrentSpirit(물 3단계) 추가로 탱커 + AoE 힐 복합 전선 완성.

**Phase 1 (초반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | PlaceBuilding | StoneMound | 2 | 5초 |
| 2 | StartProduction | DustSpirit | 2 | 20초 |

**Phase 2 (중반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | TerraForge | 2 | 20초 |
| 2 | StartProduction | BoulderSpirit | 2 | 50초 |
| 3 | PlaceBuilding | AquaSpring | 1 | 70초 |
| 4 | StartProduction | TideSpirit | 1 | 90초 |

**Phase 3 (중후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | GaeaSanctum | 2 | 15초 |
| 2 | StartProduction | QuakeSpirit | 2 | 50초 |
| 3 | UpgradeBuilding | TidalNexus | 1 | 60초 |
| 4 | StartProduction | StreamSpirit | 1 | 90초 |

**Phase 4 (후반)**

| 순서 | actionType | 대상 | 라인 | delay(보통) |
|------|-----------|------|------|------------|
| 1 | UpgradeBuilding | OceanicHeart | 1 | 30초 |
| 2 | StartProduction | TorrentSpirit | 1 | 65초 |
