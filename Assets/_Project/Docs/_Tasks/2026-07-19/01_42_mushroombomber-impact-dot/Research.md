# Research — MushroomBomber(버섯폭격기) 착탄형 범위 딜러 구현

## 이 작업이 무엇인지 (자연어 설명)

MushroomBomber는 초월계 식물 라인의 **착탄형 범위 딜러**입니다. 지금까지 만든 특수 유닛들과 또 다른
공격 방식으로, **폭탄(포자)을 적에게 던져 떨어진 자리에서 폭발**시킵니다.

폭발이 일어나면 두 가지가 동시에 발생합니다.
1. **직접 피해**: 폭탄을 맞은 **딱 1마리(주 타깃)** 에게 10의 즉시 피해.
2. **지속 피해(DoT)**: 폭발 지점 주변(월드 반경 = "인접 1칸" 거리) 안의 **적 유닛 전원**에게 3초에 걸쳐
   초당 2씩 총 6의 지속 피해. 이 DoT는 **1초마다 뚝뚝** 들어가며, 맞을 때마다 남은 체력이 텍스트로 뜹니다.

주 타깃은 폭발 중심이므로 **직접 10 + DoT를 함께** 받고, 근처에 겹쳐 있거나 서 있는 다른 적 유닛은
**DoT만** 받습니다. **건물은 직접 피해만** 받고 DoT는 받지 않지만, 폭탄을 건물에 던졌더라도
**주변 적 유닛에게는 DoT가 정상 적용**됩니다. 아군은 어떤 피해도 받지 않습니다(규칙 16).

핵심 신규 작업은 두 가지입니다.
- **착탄형 특수 공격**: 폭탄이 날아가 착탄하는 순간(서버 권위 타이밍), 월드 좌표 반경 안의 적 유닛을
  찾아 DoT를 부여하는 특수 핸들러(BattleAxe 휩쓸기와 같은 전략 핸들러 구조, 규칙 23).
- **DoT "초 단위 틱" 모드**: BloomFairy에서 만든 HoT/DoT 공용 시스템을 재사용하되, 힐과 달리
  **1초 간격으로 discrete하게 피해를 적용**하고 매초 데미지 텍스트를 띄우는 모드를 추가.

직접 10 피해는 **기존 단일 타깃 공격 경로**(`ExecuteAttack`)를 그대로 쓰고, DoT AoE만 특수 핸들러로
얹습니다(BattleAxe식 `ReplacesPrimaryAttack=false`). 그래서 건물 공성(직접 10)도 자연히 됩니다.

---

## 대상 유닛 스펙 (StatsReference.md 기준)

| 항목 | 값 |
|------|----|
| UnitType | `MushroomBomber` (enum 값 26, 초월계 · "범위 폭발 딜러") |
| HP | 40 |
| 공격력(직접) | **10** (착탄 대상 1마리) |
| 공격 사거리 / 감지 사거리 | 2.0 / 2.0 |
| 이동 속도 | 1 |
| 쿨다운 | 1:00(3:00) — 착탄 발동 1.0s / 전체 주기 3.0s (BloomFairy 예외와 달리 표준: 주기 = 쿨다운) |
| 생산 / 골드 / 인구 | 15초 / 200 / 1 |
| 특수 | 착탄형 AoE — 착탄 대상 1마리 10 직접 + 착탄 지점 반경 내 적 유닛 DoT 2/초 × 3초 |
| 생산 건물 | **SporePatch(식물 1단계, BuildingType 30)** 에서 생산 |

### 확정된 설계 결정 (사용자, 2026-07-19)

1. **판정 = 월드 좌표 기준**(BattleAxe 휩쓸기와 동일 방식). 단, **반경 값은 "인접 1칸"에 해당하는
   타일 거리값**을 사용(= 헥스 인접 타일 간 월드 거리). **Inspector(SpecialAttackConfig)에서 튜닝**.
2. **직접 피해 대상 = 반드시 1마리**(주 타깃). 같은 지점에 여럿 겹쳐 있어도 직접 10은 1마리에게만,
   나머지는 DoT만.
3. **착탄 대상도 DoT 포함** — 주 타깃 = **직접 10 + DoT 둘 다**. 나머지 반경 내 적 유닛 = DoT만.
4. **DoT 대상 = 적 유닛만**. **건물은 DoT 제외**(건물은 직접 착탄 피해만).
   - ⚠️ 단, 주 타깃이 **건물이어도** 폭발 반경 내 **적 유닛에게는 DoT 적용**(특수 핸들러는 주 타깃
     종류와 무관하게 실행 — BattleAxe와 동일).
5. **아군 무피해** — 직접·DoT 모두(규칙 16).
6. **DoT 갱신 = 중첩 없음(리셋)** — 같은 유닛에 DoT가 겹치면 3초·총량 리셋(공용 시스템 규칙 34).
7. **DoT 적용 방식 (힐과 다름)**: **1초 간격 discrete 적용**. 초당 피해량을 **매초 1번씩** 반영하고,
   맞을 때마다 **남은 체력을 데미지 텍스트로 표시**. 소수점은 **올림**.
   - (참고: BloomFairy HoT는 "프레임마다 부드럽게 + 완료 시 텍스트 1회"였음 → DoT는 정반대로 "초 단위
     뚝뚝 + 매초 텍스트".)
8. **모든 피해 계산 = 서버 권위**(착탄·DoT 모두, 규칙 18). VFX(폭탄 투사체·폭발)는 **사용자가 별도 제작**.
9. **에디터 작업(프리팹 등록·생산 라인 배선)은 에디터 스크립트로** 작성.

---

## 현재 코드 구조 (파악 결과)

### 1. 특수 공격 전략 핸들러 구조 (규칙 23) — 재사용
- `Scripts/Application/Combat/`의 `ISpecialAttackBehavior`(`Apply(SpecialAttackContext)` + `ReplacesPrimaryAttack`),
  `SpecialAttackContext`, `SpecialAttackRegistry`(`UnitType → 핸들러`).
- **등록 현황**: `BattleAxe → SweepAttackBehavior`, `TorrentSpirit → TorrentAttackBehavior`. 주석에
  자리 예약됨: `_behaviors[UnitType.MushroomBomber] = new BlastAttackBehavior();`.
- `UnitCombatUseCase.ExecuteAttack`이 단일 타깃 피해 직후 특수 훅 1줄(레지스트리 조회 → `Apply`)만 호출.
- **MushroomBomber 관련**: `BlastAttackBehavior`(신규 핸들러) + 레지스트리 1줄. `ReplacesPrimaryAttack=false`
  (직접 10은 주 타깃 단일 피해로, DoT AoE만 핸들러). BattleAxe 패턴과 거의 동일하되 효과가 "DoT 부여".

### 2. 착탄형 월드 좌표 반경 판정 (신규, QuakeSpirit과 공유 여지)
- BattleAxe는 "전방 부채꼴"(reach + arc). MushroomBomber는 **착탄 지점 중심 원형 반경**(arc 없음).
- 판정: 착탄 지점(주 타깃 월드 위치)에서 **XZ 평면 거리 ≤ 반경**인 적 유닛(월드 좌표는
  `IEntityPositionProvider` 서버 권위, 규칙 6/24와 동일 소스). 반경 = "인접 1칸" 타일 거리(Inspector).
- **QuakeSpirit도 착탄형**(중심 100%/인접 50%)이라 향후 이 원형 반경 판정을 재사용 가능. (구체 공유 형태는 Plan.)

### 3. HoT/DoT 공용 시간 지속 효과 시스템 (규칙 34, BloomFairy에서 구축) — DoT 확장 필요
- `UnitCombatUseCase`의 `ActiveTimedEffect` / `_activeTimedEffects` / `ApplyTimedEffect` / `TickTimedEffects` /
  `TimedEffectKind{Heal,Damage}`. `ApplyTimedEffect(...Damage...)`로 DoT를 얹을 자리는 이미 있음.
- ⚠️ **현재 틱은 "프레임마다 diff" 방식**(힐용, 부드러움). MushroomBomber DoT는 **"1초 간격 discrete"**
  가 필요 → **DoT용 초 단위 틱 모드**를 추가해야 함(누적 시간이 1초를 넘을 때마다 그 초의 피해 적용 + 텍스트).
- Damage 적용 경로: 기존 피해 헬퍼(`ApplyDamageToVictim`/`ApplyTimedDamageToUnit` 계열) + `OnEntityDamaged` →
  기존 **데미지 텍스트(현재 HP 표시)** 가 그대로 매초 남은 체력을 보여줌(별도 텍스트 작업 불필요).
- 갱신 규칙(대상별 동종 1레코드 리셋)은 기존 그대로 적용.
- 서버 틱 진입점: 싱글=`GameBootstrapper.Update`(`!IsNetworkMode`) / 멀티=`NetworkCombatController`(IsServer).
  이미 `TickTimedEffects`가 두 곳에서 호출됨 — DoT 초 단위 틱도 같은 진입점.

### 4. 데미지 텍스트 = 재사용 (현재 HP 표시)
- `FloatingHpTextSpawner.ShowDamage`가 이미 **피격 후 현재 HP**를 표시(`$"{evt.CurrentHp}"`).
  DoT 매초 틱이 `OnEntityDamaged`를 발행하면 매초 남은 체력이 자연히 텍스트로 뜬다 → 신규 텍스트 작업 없음.

### 5. 착탄 타이밍·연출 (규칙 18/22/26/27)
- 원거리 유닛은 `OnAttackHit`에서 연출 전용 트레이서(발사→비행→착탄)를 재생하고 착탄 시점에 피격 연출을 방출.
- 데미지·DoT 발동은 서버 권위 타이머(`HitFrameTimes`), 파티클 위치 종속 금지(규칙 18). VFX는 사용자 제작.
- MushroomBomber Attack 클립 `OnAttackHit` 미주입(규칙 27 잔여 3종) → 구현 시 `hitFrameTimes` 확정 후 주입.

### 6. 생산 라인 배선 = 씬 데이터 (에디터 스크립트로 처리)
- 생산 목록은 `ProductionPanelUI._buildingUnitMappings`(씬 직렬화). `BuildingUnitMapping`의
  `blueUnits`/`redUnits` = `List<UnitPortraitEntry>{ type, portrait, requiredStage }`.
- **식물 라인 미배선**(Game.unity에 SporePatch/FloralNursery 매핑 0건). 이번에 배선:
  - **SporePatch(30, 1단계)** → MushroomBomber(requiredStage 1)
  - **FloralNursery(31, 2단계)** → BloomFairy(requiredStage 2) — BloomFairy 생산 노출도 이때 완성.
- 프리팹 등록: UnitFactory `_transcendencePrefabs`에 type 26(MushroomBomber) 추가(BloomFairy `RegisterBloomFairyPrefabs.cs` 패턴).

---

## 영향 범위 (예상)

| 파일/영역 | 예상 변경 | 구분 |
|-----------|-----------|------|
| `Application/Combat/BlastAttackBehavior.cs` | 착탄형 DoT 특수 핸들러 신설 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | MushroomBomber 등록 1줄 | 수정 |
| `Application/UseCases/UnitCombatUseCase.cs` | DoT 초 단위 틱 모드 추가(`TickTimedEffects`/`ActiveTimedEffect`), 착탄 반경 적 유닛 수집 + `ApplyTimedEffect(Damage)` | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | 착탄 반경·DoT(초당/지속) 튜닝 필드 추가 | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | MushroomBomber DoT 튜닝값 주입(필요 시) | 수정 |
| `UnitStatsConfig.asset` | MushroomBomber(26) 스탯 입력 | 에셋 |
| 힐 애니 클립 / `OnAttackHit` | `hitFrameTimes` 확정 + 주입(규칙 27) | 에셋 |
| UnitFactory 프리팹 등록 (에디터 스크립트) | type 26 등록 | 에디터 스크립트 |
| 생산 라인 배선 (에디터 스크립트) | SporePatch→MushroomBomber, FloralNursery→BloomFairy | 에디터 스크립트 |

**무변경 재사용**: 특수공격 전략 구조(규칙 23), HoT/DoT 공용 시스템 뼈대(규칙 34), 데미지 텍스트(현재 HP),
`ApplyDamageToVictim`/피해 이벤트/사망 처리, 기존 적 탐색·파도·힐 로직(회귀 방지).

---

## 현재 상태 (구현 전제)
- `UnitType.MushroomBomber` = 26 등록됨. 프리팹 `Unit_MushroomBomber_Blue/Red` + 포트레잇 존재.
- **UnitStatsConfig에 26 미입력** → 폴백값 사용 중.
- 특수공격 레지스트리 MushroomBomber 미등록(주석 자리만).
- HoT/DoT 공용 시스템에 **DoT 초 단위 틱 모드 없음**(힐용 프레임 diff만).
- 생산 라인(SporePatch/FloralNursery) 미배선. `OnAttackHit` 미주입.

---

## 핵심 난이도 / Plan에서 결정할 항목
1. **착탄 반경 판정 형태**: BattleAxe 부채꼴 재사용 vs 원형 반경 신규. QuakeSpirit 공유를 위한 헬퍼 추출 범위.
2. **DoT 초 단위 틱 모드**: `ActiveTimedEffect`에 틱 간격/누적 시간 필드, 힐(프레임 diff)과 DoT(1초 discrete)의
   분기, 매초 `OnEntityDamaged` 발행(현재 HP 텍스트), 올림 처리, 사망 시 처리.
3. **직접 10 + DoT 동시 부여**: 주 타깃(직접) 경로와 DoT AoE(핸들러) 경로가 주 타깃에 둘 다 적용되도록.
4. **주 타깃 종류 무관 실행**: 주 타깃이 건물이어도 DoT AoE는 적 유닛 대상으로 실행(핸들러가 건물 여부와 독립).
5. **튜닝 파라미터**: 착탄 반경·DoT 초당/지속을 SpecialAttackConfig에. (에셋≠배선 교훈 — 배선 확인.)
6. **에디터 스크립트**: 프리팹 등록 + 식물 라인(2건) 생산 배선 멱등 스크립트.
