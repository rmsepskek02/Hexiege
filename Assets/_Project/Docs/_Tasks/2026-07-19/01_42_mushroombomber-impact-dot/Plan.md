# Plan — MushroomBomber(버섯폭격기) 착탄형 범위 딜러 구현

## 이 작업이 무엇인지 (자연어 설명)

MushroomBomber는 **폭탄을 던져 착탄 지점에서 폭발**하는 범위 딜러입니다. 폭발은 두 가지 피해를 줍니다.
- **직접 10**: 폭탄 맞은 1마리(주 타깃)에게 즉시.
- **DoT(지속 피해)**: 폭발 반경 안 적 유닛 전원에게 3초간 초당 2씩(총 6), **1초마다 뚝뚝** 들어가고
  맞을 때마다 **남은 체력이 텍스트로** 뜹니다.

구현의 핵심은 3가지입니다.
1. **착탄형 특수 핸들러**(`BlastAttackBehavior`) — 착탄 시점에 월드 좌표 반경 안 적 유닛을 찾아 DoT 부여.
   BattleAxe 휩쓸기와 같은 전략 핸들러 구조(규칙 23)를 그대로 쓰되, 판정이 "전방 부채꼴" 대신 "원형 반경"입니다.
2. **DoT 초 단위 틱 모드** — BloomFairy에서 만든 HoT/DoT 공용 시스템(규칙 34)에, 힐과 달리 **1초 간격으로
   discrete하게 피해를 넣고 매초 데미지 텍스트**를 띄우는 모드를 추가합니다.
3. **직접 10 = 주 타깃 단일 피해** — 기존 `ExecuteAttack` 단일 피해를 그대로 쓰고(그래서 건물 공성도 자연히
   됨), DoT AoE만 특수 핸들러로 얹습니다(BattleAxe식 `ReplacesPrimaryAttack=false`).

관련 규칙(`GameSystemRules_Units.md`): **규칙 23**(전략 핸들러) · **24/25**(월드 좌표 판정·튜닝 파라미터) ·
**26**(AoE 연출 동시 방출) · **27**(OnAttackHit 주입) · **34**(HoT/DoT 공용 시스템). 기본 규칙 16(아군 무피해)·18(서버 권위 타이밍) 전제.

---

## 확정된 설계 결정 (사용자, 2026-07-19) — 재확인

| # | 결정 |
|---|------|
| 1 | 판정 = **월드 좌표 원형 반경**. 반경 = "인접 1칸" 타일 거리(Inspector/SpecialAttackConfig 튜닝) |
| 2 | 직접 피해 = **주 타깃 1마리**만 10. 나머지는 DoT만 |
| 3 | **주 타깃도 DoT 포함**(직접 10 + DoT). 반경 내 다른 적 유닛 = DoT만 |
| 4 | DoT 대상 = **적 유닛만**. **건물 DoT 제외**(건물은 직접 착탄만). 단 주 타깃이 건물이어도 **주변 적 유닛엔 DoT** |
| 5 | **아군 무피해**(직접·DoT 모두, 규칙 16) |
| 6 | DoT **중첩 없음·갱신(리셋)** — 대상별 동종 1레코드(규칙 34) |
| 7 | DoT = **1초 간격 discrete**, 매초 남은 체력 데미지 텍스트, 소수점 **올림** |
| 8 | 모든 피해 **서버 권위**(규칙 18). VFX(투사체·폭발)는 사용자 별도 제작 |
| 9 | 에디터 작업(프리팹 등록·생산 라인 배선)은 **에디터 스크립트로** |

---

## 구현 항목

### (a) 착탄형 특수 핸들러 `BlastAttackBehavior` (규칙 23) [신규]

- `Scripts/Application/Combat/BlastAttackBehavior.cs` 신설. `ISpecialAttackBehavior` 구현,
  `ReplacesPrimaryAttack = false`(직접 10은 주 타깃 단일 피해가 담당, 핸들러는 DoT AoE만).
- `SpecialAttackRegistry`에 `_behaviors[UnitType.MushroomBomber] = new BlastAttackBehavior();` 1줄.
- `Apply(SpecialAttackContext)` 동작:
  1. **착탄 중심** = 주 타깃 월드 위치(`IEntityPositionProvider`, 서버 권위).
  2. 중심에서 **XZ 평면 거리 ≤ 착탄 반경**인 **적 유닛**을 수집(아군·사망·건물 제외). 주 타깃 포함(거리 0).
     - 규칙 24처럼 **먼저 리스트로 수집 후 일괄** 적용(순회 중 사망에 의한 컬렉션 변경 회피).
  3. 각 대상에 **DoT 부여** = `ApplyTimedEffect(caster, target, Damage, 초당피해, 지속, 틱간격=1s)` (b 시스템 호출).
- 판정 형태: BattleAxe(reach+arc)의 **arc 없는 원형 반경** 버전. 공통 부분(월드 거리 수집)을 재사용 가능하면
  헬퍼로 뽑되, **QuakeSpirit(향후 착탄형)** 도 쓸 수 있게 과도한 결합 없이. (원형 반경 수집 헬퍼 정도가 적정 — 최종 판단은 구현자.)

### (b) DoT "초 단위 틱" 모드 — HoT/DoT 공용 시스템 확장 (규칙 34)

- `ActiveTimedEffect`에 **틱 간격**(예: `TickInterval`, DoT=1.0s) + **다음 틱까지 누적 시간** 필드 추가.
  힐(HoT)은 기존 "프레임마다 diff"를 유지, DoT는 **누적 시간이 틱 간격(1s)을 넘을 때마다 그 틱의 피해 적용**.
- **틱당 피해량** = 초당 피해 × 틱 간격, **소수점 올림**(`Mathf.CeilToInt`). MushroomBomber = ceil(2×1)=2.
  총량이 정확히 맞도록(2/초×3초 = 3틱×2 = 6) 마지막 틱 잔량 정산(올림으로 인한 초과는 총 지속·총량 상한으로 클램프).
- 각 틱 피해는 **기존 피해 경로**(`ApplyTimedDamageToUnit`/`ApplyDamageToVictim` 계열)로 적용 →
  `OnEntityDamaged` 발행(`ImmediatePresentation` 적절히) → **데미지 텍스트가 매초 남은 체력 표시**(규칙 34 재사용).
  힐처럼 텍스트를 억제하지 않는다(요구 7: 매초 표시).
- **갱신(리셋)**: 같은 대상에 DoT 재부여 시 남은 시간·틱 누적 리셋(규칙 34 기존 규칙).
- **사망 처리**: 틱 피해로 대상 사망 시 기존 `ApplyDamageToVictim` 사망 경로(이벤트/제거) 그대로, 레코드 제거.
- **서버 틱 진입점**: 기존 `TickTimedEffects` 그대로(싱글 `!IsNetworkMode` / 멀티 IsServer). 이중 틱 금지.

### (c) 직접 10 = 주 타깃 단일 피해 (기존 경로)

- `ExecuteAttack`의 단일 타깃 피해(`ApplyDamageToVictim`)가 주 타깃(유닛/건물)에 **10**을 적용(공격력=10, UnitStatsConfig).
- 이후 특수 훅이 `BlastAttackBehavior.Apply` 호출(규칙 23). **주 타깃이 건물이어도 핸들러는 실행**되어 주변 적 유닛에 DoT.
- 주 타깃이 유닛이면 직접 10을 받고, 착탄 반경에 포함되므로 DoT도 함께 받음(결정 3) — 직접 경로와 DoT 경로가 별개로 적용.

### (d) 튜닝 파라미터 (SpecialAttackConfig, 규칙 25)

- 신규 필드: `_blastRadius`(착탄 반경, 기본 = "인접 1칸" 타일 월드 거리) / `_blastDotPerSecond`(2) / `_blastDotDuration`(3) + getter.
- GameBootstrapper가 시작 시 SO 값을 읽어 핸들러/유스케이스에 **float 주입**(Application이 Infra SO 직접 참조 금지, 규칙 25).
- ⚠️ **에셋 생성 ≠ 씬 배선**(규칙 25 교훈): 값 넣어도 GameBootstrapper `_specialAttackConfig` 연결 확인. 미연결 시 코드 폴백.

### (e) 데이터·에셋 배선

| 대상 | 작업 |
|------|------|
| `UnitStatsConfig.asset` | MushroomBomber(26): HP40 / 공격력 10 / 사거리 2.0 / 감지 2.0 / 이동 1 / 쿨다운 3.0 / hitFrameTimes / 생산15·골드200·인구1 (BloomFairy와 달리 `isHealer` 없음) |
| 힐 애니 → 공격 클립 `OnAttackHit` | `hitFrameTimes` 실측(착탄 발동 프레임) 후 `CombatHitEventInjector` 주입(규칙 27, **1개만**) — 연출/타이밍용. ⚠️ 사거리 2.0(≥1.0)이라 트레이서(원거리) 분기 — 착탄 연출과 정합 확인 |
| VFX | 폭탄 투사체·폭발 = **사용자 별도 제작**(EffectPreset/UnitEffectConfig 연결도 사용자 또는 후속) |

### (f) 에디터 스크립트 (규칙 결정 9)

- **프리팹 등록**: UnitFactory `_transcendencePrefabs`에 type 26(Unit_MushroomBomber_Blue/Red) 멱등 등록
  (`RegisterBloomFairyPrefabs.cs` 패턴). 메뉴 `Hexiege/Setup/Register MushroomBomber Prefabs (Game)`.
- **생산 라인 배선**: `ProductionPanelUI._buildingUnitMappings`에 식물 라인 2건 멱등 배선.
  - SporePatch(30) → MushroomBomber(requiredStage 1)
  - FloralNursery(31) → BloomFairy(requiredStage 2)  ← BloomFairy 생산 노출도 이때 완성
  - 포트레잇 스프라이트 연결(MushroomBomber/BloomFairy 포트레잇 존재). 메뉴 `Hexiege/Setup/Wire Flora Production Line (Game)`.
- 두 스크립트 모두 멱등(재실행 안전), 씬 자동 열기/저장, `CreateSpecialAttackConfigAsset.cs` 스타일.

---

## 영향 범위 / 파일

| 파일/영역 | 변경 | 구분 |
|-----------|------|------|
| `Application/Combat/BlastAttackBehavior.cs` | 착탄형 DoT 핸들러 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | 등록 1줄 | 수정 |
| `Application/UseCases/UnitCombatUseCase.cs` | DoT 초 단위 틱 모드, 착탄 반경 수집 | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | blast 반경·DoT 튜닝 필드 | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | blast 튜닝값 주입 | 수정 |
| `UnitStatsConfig.asset` | 26 스탯 | 에셋 |
| 공격 클립 `OnAttackHit` | 주입(규칙 27) | 에셋 |
| `Assets/Editor/Setup/RegisterMushroomBomberPrefabs.cs` | 프리팹 등록 | 신규(에디터) |
| `Assets/Editor/Setup/WireFloraProductionLine.cs` | 식물 라인 생산 배선 | 신규(에디터) |

**무변경 재사용**: 특수공격 전략 구조·`ExecuteAttack` 훅(규칙 23), HoT/DoT 뼈대(규칙 34),
`ApplyDamageToVictim`/피해 이벤트/사망 처리, 데미지 텍스트(현재 HP), 기존 적 탐색·파도·힐(회귀 방지).

---

## 위험 요소 / 주의
1. **DoT 초 단위 틱 vs 힐 프레임 diff 혼선** — 한 시스템에 두 모드. `TickInterval`로 분기, 힐 경로 회귀 없게(HoT 무변경 확인).
2. **올림 초과** — ceil로 틱당 피해가 커져 총량이 6을 넘지 않게 총 지속/총량 상한 클램프.
3. **매초 텍스트 = 데미지 텍스트 재사용** — HoT처럼 억제하지 말 것(요구 7). 파도/일반 데미지 텍스트 회귀 없게.
4. **주 타깃 건물 시 DoT AoE 실행** — 핸들러가 주 타깃 종류와 독립 실행(결정 4). BattleAxe처럼 AoE는 항상 실행.
5. **직접+DoT 이중 적용** — 주 타깃이 직접 10과 DoT를 둘 다(결정 3). 별개 경로라 자연 성립하되 중복 사망처리 주의.
6. **이중 틱 금지** — 싱글/멀티 가드 유지(규칙 29/34).
7. **에셋 ≠ 배선**(규칙 25) — SpecialAttackConfig 값·생산 배선 런타임 반영까지 확인.
8. **OnAttackHit 1개만**(규칙 27) — 착탄 중복 방지.

---

## 검증 (QA 포인트, TC 별도 작성 불요)
- 착탄 대상 1마리 직접 10 + DoT, 반경 내 다른 적 유닛 DoT만, 아군·건물 DoT 제외.
- 건물을 주 타깃으로 해도 주변 적 유닛 DoT 적용(+건물 직접 10 = 공성).
- DoT 매초 1회 discrete, 매초 남은 체력 텍스트, 올림, 총량 6(2×3) 정확·초과 없음.
- DoT 갱신(리셋)·중첩 없음. DoT로 사망 시 정상 사망 처리.
- 멀티: 서버만 틱, 클라 HP·데미지 텍스트 동기화(이중 적용 없음).
- 힐(HoT)·파도·일반 공격/데미지 텍스트 회귀 없음.
- 생산: SporePatch에서 MushroomBomber, FloralNursery에서 BloomFairy 생산 노출(단계 잠금 정상).

---

## 에이전트 위임 (CLAUDE.md 규칙 3)
- 코드 구현(핸들러·DoT 틱 모드·튜닝·주입) + 에디터 스크립트 → **game-programmer**.
- 구현 후 검증 → **qa-tester**(위 포인트, TC 문서 없이).
- 설계 판단 충돌 시 → 사용자 확인(규칙 12).

## 남은 특수 유닛(참고)
MushroomBomber 이후 잔여: **QuakeSpirit**(착탄형 — 이 작업의 원형 반경 판정 재사용 가능), **InfernoSpirit**(DoT — 이 작업의 DoT 초 단위 틱 재사용). 착탄형·DoT를 재사용 가능하게 두면 후속 비용↓.
