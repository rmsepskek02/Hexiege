# Plan — QuakeSpirit(대지의 정령) 착탄형 즉발 AoE 구현 + 특수 유닛 설계문서 기록

## 이 작업이 무엇인지 (자연어 설명)

특수 유닛 5종의 마지막 QuakeSpirit을 구현합니다. **착탄형 즉발 범위 딜러**로, 공격이 착탄하면
**주 타깃 1마리에 100%(20)**, **착탄 반경(월드, 인접 1칸) 안의 다른 적 유닛·적 건물에 50%(10, 올림)** 를
한 번에(즉발) 줍니다. 주 타깃은 100%만 받습니다.

구현은 기존 자산을 크게 재사용합니다.
- **판정**: MushroomBomber의 월드 원형 반경 헬퍼(규칙 38) 재사용 — 단 QuakeSpirit은 **건물도 스플래시**라 반경 내
  적 건물 순회를 추가.
- **즉발 AoE 적용·연출**: BattleAxe 방식(규칙 24/26) 재사용 — 선수집 후 일괄 `ApplyDamageToVictim`, 단일 타격
  프레임 연출 동시 방출. (DoT 아님 → 규칙 40 미사용.)
- **직접 100%**: 기존 `ExecuteAttack` 단일 피해(`ReplacesPrimaryAttack=false`, 건물 공성 포함).

추가로 **특수 유닛 6종의 능력 설계를 `GameDesignDocument.md`에 기록**합니다(QuakeSpirit + 이전 5종 소급).

관련 규칙(`GameSystemRules_Units.md`): **규칙 23**(전략 핸들러) · **24**(월드 좌표 판정) · **25**(튜닝 파라미터) ·
**26**(AoE 연출 동시 방출) · **27**(OnAttackHit 주입) · **29**(건물 순회 교훈) · **38**(원형 반경 헬퍼). 기본 규칙 16(아군 무피해)·18(서버 권위) 전제.

---

## 확정된 설계 결정 (사용자, 2026-07-20)
| # | 결정 |
|---|------|
| 1 | **즉발 피해**(DoT 아님) |
| 2 | 주 타깃 1마리 = **100%(20)만**(스플래시 제외) |
| 3 | 스플래시 50%(10) = 착탄 반경 내 **적 유닛 + 적 건물**(주 타깃 제외). **건물도 영향** |
| 4 | 스플래시 데미지 **올림**(`CeilToInt`) |
| 5 | 판정 = **월드 좌표 원형 반경**(규칙 38 재사용), 반경 = 전용 `_quakeRadius`(기본 1.0=인접 1칸, blastRadius와 분리) |
| 6 | 아군 무피해, 서버 권위. VFX 사용자 제작. OnAttackHit 주입·스탯·생산 배선 = 에디터 스크립트 |

---

## 구현 항목

### (a) `QuakeAttackBehavior` 신규 핸들러 (규칙 23) [핵심]
- `Application/Combat/QuakeAttackBehavior.cs`. `ISpecialAttackBehavior`, `ReplacesPrimaryAttack=false`.
- `SpecialAttackRegistry`에 `_behaviors[UnitType.QuakeSpirit] = new QuakeAttackBehavior();` 1줄.
- `Apply(SpecialAttackContext)`:
  1. 착탄 중심 = 주 타깃 월드 위치(`IEntityPositionProvider`, 서버 권위).
  2. 반경 내 **적 유닛** 수집(규칙 38 헬퍼 재사용) — **주 타깃 제외**.
  3. 반경 내 **적 건물** 수집(신규 순회, 아래 (b)).
  4. 수집한 유닛·건물에 **50% 즉발 피해**(공격력×0.5, `CeilToInt`) 적용(`ApplyDamageToVictim` 계열, 선수집 후 일괄).
     스플래시 피해값은 컨텍스트로 주입되는 quake 튜닝(비율/반경) 사용.
- BattleAxe 핸들러와 유사하되 **중심 = 주 타깃 위치**(착탄형), **arc 없음**, **건물 포함**, **주 타깃 제외**.

### (b) 반경 내 건물 순회 추가 (규칙 29 교훈)
- 기존 `CollectEnemyUnitsInRadius`(규칙 38)는 유닛만 수집. **적 건물**을 반경으로 수집하는 경로 추가.
  - 방식 A(권장): 헬퍼를 유닛/건물 각각 수집하도록 공용화(또는 건물용 `CollectEnemyBuildingsInRadius` 병행 static).
  - **유닛 hit-set과 건물 hit-set 분리**(유닛 Id·건물 Id 카운터가 달라 값 충돌 가능 — 규칙 29 교훈).
- 건물 월드 위치도 `IEntityPositionProvider`(또는 건물 위치 조회 수단)로 서버 권위 조회.

### (c) 직접 100% = 주 타깃 단일 피해 (기존 경로)
- `ExecuteAttack`의 단일 피해(공격력 20)가 주 타깃(유닛/건물)에 100% 적용(무변경). 이후 특수 훅이 `QuakeAttackBehavior` 호출.
- **주 타깃은 (b)/(a) 스플래시에서 제외** → 100%만. 다른 대상만 50%.
- 주 타깃이 직접 100%에 죽어 제거됐어도 스플래시는 나머지 대상에 정상 적용.

### (d) 튜닝 파라미터 (SpecialAttackConfig, 규칙 25)
- `_quakeRadius`(기본 1.0=인접 1칸) + `_quakeSplashRatio`(기본 0.5) + getter. GameBootstrapper가 float 주입(기존 패턴), 코드 폴백(1.0/0.5).
- ⚠️ 에셋≠배선(규칙 25): `_specialAttackConfig` 연결 확인. 미연결 시 폴백.

### (e) 데이터·에셋 배선 (에디터 스크립트)
| 대상 | 작업 |
|------|------|
| `UnitStatsConfig.asset` | QuakeSpirit(15): HP250/공격20/range0.5/detect1.0/move0.5/cooldown5.0/hitFrameTimes/생산30·400·1 |
| UnitFactory 프리팹 등록 | type 15(정령 라인) 등록 |
| 생산 배선 | 흙(대지) 정령 라인 생산 건물에 QuakeSpirit(단계 확인) |
| Attack 클립 OnAttackHit | hitFrameTimes 실측 후 주입(규칙 27, 마지막 잔여 유닛) |
| UnitEffectConfig / VFX | 사용자 VFX 제작 후 프리셋 연결(이번엔 사용자) |
- 위 스탯·프리팹·생산 배선은 **에디터 셋업 스크립트**(RegisterBloomFairyPrefabs/WireFloraProductionLine 패턴)로 자동화.

### (f) 특수 유닛 설계문서 기록 (GameDesignDocument.md)
- "🪖 유닛 시스템"에 **특수 유닛 능력 설계** 신설 — 6종(BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/InfernoSpirit/QuakeSpirit)
  각 능력·판정 형태·수치를 자연어 설계로. GameSystemRules_Units(구현 규칙)와 역할 분리. → document-manager.

---

## 영향 범위 / 파일

| 파일/영역 | 변경 | 구분 |
|-----------|------|------|
| `Application/Combat/QuakeAttackBehavior.cs` | 착탄 즉발 2단계 핸들러 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | 등록 1줄 | 수정 |
| `Application/Combat/BlastAttackBehavior.cs`/공용 | 반경 헬퍼 건물 포함 확장 | 수정 |
| `Application/UseCases/UnitCombatUseCase.cs` | 스플래시 즉발 진입점, quake 주입 | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | `_quakeRadius`/`_quakeSplashRatio` | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | quake 주입 | 수정 |
| `Assets/Editor/Setup/RegisterQuakeSpiritPrefabs.cs` 등 | 스탯·프리팹·생산 배선 | 신규(에디터) |
| `GameDesignDocument.md` | 특수 유닛 6종 설계 | 문서 |

**무변경 재사용**: 특수공격 전략 구조, 즉발 AoE/연출(규칙 24/26), `ExecuteAttack`/`ApplyDamageToVictim`/피해·사망·데미지 텍스트, BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/InfernoSpirit 로직(회귀 방지).

---

## 위험 요소 / 주의
1. **건물 스플래시** — QuakeSpirit만 건물 포함. 유닛/건물 **hit-set 분리**(규칙 29). 아군 건물 무피해.
2. **주 타깃 제외** — 주 타깃은 100%만, 스플래시 이중 적용 금지.
3. **올림 비율** — 50% = `CeilToInt(공격력×0.5)`. 공격력 20→10.
4. **즉발/연출** — DoT 아님. 단일 타격 프레임 AoE 연출 동시 방출(규칙 26).
5. **에셋≠배선**(규칙 25) — quake 값·`_specialAttackConfig`·스탯·생산 런타임 반영 확인.
6. **회귀** — 헬퍼 공용화 시 MushroomBomber(유닛만) 동작 무변경 확인. 기존 특수 유닛 5종·BattleAxe AoE 회귀 없게.
7. **OnAttackHit 1개만**(규칙 27).

---

## 검증 (QA 포인트, TC 별도 작성 불요)
- 주 타깃 1마리 100%(20)만. 반경 내 다른 적 유닛·적 건물 50%(10, 올림). 주 타깃 스플래시 제외.
- 건물 스플래시 적용(공성 기여). 아군 유닛·건물 무피해.
- 즉발(한 프레임), DoT 아님. 연출 동시 방출.
- 반경 = quakeRadius(월드), 인접 1칸. 주 타깃 사망해도 스플래시 정상.
- 멀티: 서버 권위, 클라 HP·데미지 텍스트 동기화.
- MushroomBomber(유닛만 DoT)·BattleAxe(유닛만 즉발)·기타 유닛 회귀 없음.
- 생산: 대지 정령 라인에서 QuakeSpirit 생산 노출.

---

## 에이전트 위임 (CLAUDE.md 규칙 3)
- 코드 구현(핸들러·건물 순회·스플래시·튜닝) + 에디터 스크립트 → **game-programmer**.
- 특수 유닛 6종 설계문서 기록 → **document-manager**.
- 구현 후 검증 → **qa-tester**(위 포인트, TC 문서 없이).
- 설계 판단 충돌 시 → 사용자 확인(규칙 12).

## 실제 구현 결과와 남은 조건 (2026-07-22 재감사)

- Legacy 피해 로직은 반영됐다. 주 타깃 20, 반경 내 다른 적 유닛·적 건물 10이 적용되며 Host/Client HP 로그에서 결과를 확인했다.
- QuakeSpirit UnitStatsConfig도 등록됐다. 다만 `hitFrameTimes=1.00`은 실측 marker가 아닌 placeholder다.
- 기본 Attack 클립에 `OnAttackHit`이 없어 직접 피해 표현이 타임아웃 안전망까지 지연될 수 있다. 따라서 “연출 동시 방출”과 완전한 시각 동기화는 PASS가 아니다.
- 현재 착탄 중심은 실행 시점의 타깃 현재 위치이며 권위 `ImpactPoint`, `AttackSequenceId + HitIndex`, 결과 순번이 없다. 규칙 v2 이전과 지연·지터 멀티 QA 전에는 Complete로 판정하지 않는다.
- Inferno/Quake를 포함한 특수 피해 의미는 보존하되, 전 유닛 공격 방향 문제는 별도 v2 작업으로 다룬다.
