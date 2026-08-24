# 싱글플레이 AI 시스템 (2026-06-10 완료) — game-design-lead 이관본

> 이 파일은 `game-design-lead/MEMORY.md` 에서 옮겨 온 **확정 설계 상세**다(2026-08-24 분리).
> `MEMORY.md` 는 매 작업마다 읽는 파일이라 「매번 필요한 것 + 인덱스」만 남기고,
> 분량이 큰 상세 설계는 여기로 옮겼다(근거: `.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」 4).
> **본문은 한 글자도 고치지 않은 원문 그대로다 — 삭제가 아니라 이동이다.**

> 다루는 범위: AI 시나리오 ScriptableObject 에셋 / 시나리오 구조 / 3종족(Human·Spirit·Transcendence)
> 시나리오와 생산 건물 라인 표.

---


### AI 시나리오 ScriptableObject 에셋 (2026-06-10 완료)
- 3종족 시나리오가 각 1파일에 3시나리오 내장된 형태로 완성됨 (종족당 단일 에셋).
- 파일명: `AIScenarioConfig_Human.asset` / `AIScenarioConfig_Spirit.asset` / `AIScenarioConfig_Transcendence.asset`
- 경로: `Resources/Config/`. 게임 시작 시 `GameRaceContext.RedRace`로 종족 판별 후 해당 에셋에서 무작위 시나리오 1개 선택.
- 레거시 Human_A/B/C 개별 에셋은 삭제됨.

### AI 시나리오 구조
- Phase 4단계: Phase 1(0~3분) / Phase 2(3~6분) / Phase 3(6~9분) / Phase 4(9분~종료)
- 시나리오 선택: 게임 시작 시 A/B/C 중 균등 확률(33.3%) 랜덤. `UnityEngine.Random.Range(0, 3)` 인덱스 사용.
- 난이도는 `delaySeconds` 배율(쉬움 ×1.5 / 어려움 ×0.7)과 `goldIncomeMultiplier`로만 반영.
- 채굴소(MiningPost 계열)는 Phase 2와 Phase 4에 병행 트랙으로 자동 처리.
- 골드 경제: 시작 500g + 채굴소 10g/s × 180s × 2 = 10분 기준 이론 최대 약 5,300g.

### Human 종족 시나리오 (완료)
- **건물 비용**: 1단계 100g / 2단계 업글 100g / 3단계 업글 200g / MiningPost 50g
- **시나리오 A — 물량형**: TrainingCamp→Gunsmith 각 3단계. 건물 총 비용 900g.
- **시나리오 B — 테크형**: 근거리A 3단계 + 총기 2단계 + 탈것 2단계. 총 1,200g.
- **시나리오 C — 균형형**: 근거리A 3단계 + 총기 3단계 + 탈것 1단계(+추가시간 2단계). 총 1,400g.

### Human 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 근거리A | TrainingCamp | WarAcademy | HumanBarracks | LittleKnight | SpearMan | BattleAxe |
| 1 — 총기류 | Gunsmith | Armory | WeaponForge | Pistoleer | Assault | Sniper |
| 2 — 탈것류 | Garage | VehicleBay | — | CannonCart | Tank | — |

### Spirit 종족 시나리오 (완료)
- **건물 비용**: 1단계 75g / 2단계 업글 200g / 3단계 업글 400g / ManaRift 50g
- 단일 라인 3단계 총 비용 675g. 3단계 업글(400g)이 비싸므로 Phase 3 진입 직후(15초 지연) 배치.
- 3개 시나리오 모두 동일 구조: 메인 라인 3단계 + 보조 라인 2단계(Phase 3) → 보조 라인 3단계(Phase 4). 총 1,450g.
- **시나리오 A — Spirit-Inferno**: 불 메인(InfernoSpirit) + 땅 보조(QuakeSpirit Phase 4).
- **시나리오 B — Spirit-Torrent**: 물 메인(TorrentSpirit) + 불 보조(InfernoSpirit Phase 4).
- **시나리오 C — Spirit-Quake**: 땅 메인(QuakeSpirit) + 물 보조(TorrentSpirit Phase 4).

### Spirit 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 불 | FireSpire | BlazeConduit | InfernoCore | EmberSpirit | FlameSpirit | InfernoSpirit |
| 1 — 물 | AquaSpring | TidalNexus | OceanicHeart | TideSpirit | StreamSpirit | TorrentSpirit |
| 2 — 땅 | StoneMound | TerraForge | GaeaSanctum | DustSpirit | BoulderSpirit | QuakeSpirit |

### Transcendence 종족 시나리오 (완료)
- **건물 비용**: 1단계 125g / 2단계 업글 200g / 3단계 업글 300g / FungalNode 100g
- 단일 라인 3단계 총 비용 625g. 1단계 건설(125g)이 타 종족보다 비싸 초반 멀티 라인 오픈 타이밍이 중요.
- **시나리오 A — Trans-Rush**: 동물A + 동물B 두 라인 모두 2단계(Phase 3), 동물B 3단계(Phase 4). 총 1,150g.
- **시나리오 B — Trans-Flora**: 동물A 3단계(BearGuard) + 식물 2단계(BloomFairy) + 동물B 1단계(FoxMagician, Phase 4). 총 1,275g.
- **시나리오 C — Trans-Beast**: 동물B 3단계(LionKnight) 우선 → 동물A 3단계(BearGuard, Phase 4). 총 1,450g.

### Transcendence 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 동물A | PrimalAltar | PrimalDen | PrimalSanctuary | RabbitTrickster | RhinoBreaker | BearGuard |
| 1 — 동물B | FeralAltar | FeralDen | FeralSanctuary | FoxMagician | EagleArcher | LionKnight |
| 2 — 식물 | SporePatch | FloralNursery | — | MushroomBomber | BloomFairy | — |

> 식물 라인은 2단계(FloralNursery)가 최상위. BloomFairy는 공격 불가 힐 전용 유닛.
