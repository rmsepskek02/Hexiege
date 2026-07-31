# Plan — 연구소(Research) 기반 유닛 강화(업그레이드) 시스템

> **⚠️ 2026-07-23 확정값 갱신:** 초기 계획의 밸런스 수치(K=20, 방어 레벨당 +4/Lv5=20, 자연회복 0.5~2.5, 비용 830/연구시간 20~90초)는 **밸런스 최종 확정 + 전투 스탯 ×10 스케일 개편**으로 갱신되었다. 확정값: **K=120 · 방어 0/8/16/24/32/40 · 자연회복 3~15 HP/s · 표준 트랙 비용 1,000(80/120/180/260/360) · 연구 시간 15/25/35/50/70초 · 유닛별 고정 정수 공격력 증가폭 · 힐량 트랙(200→280, 100→140)**, 모든 전투 수치 ×10. 아래 "확정 결정·밸런스 수치" 섹션을 확정값으로 갱신했다. 단일 진실 소스: `BalanceReview.md`, 구현 계약: `GameSystemRules/GameSystemRules_Upgrade.md`.

## 이 계획이 무엇이고 왜 이렇게 하는가 (자연어 설명)

이 문서는 Research.md에서 조사한 "연구소 기반 유닛 강화 시스템"을 **어떤 순서로, 어느 파일을, 어떤 근거로** 만들지 정리한 구현 계획이다. 아직 코드는 작성하지 않으며, 이 계획을 사용자가 승인한 뒤에야 game-programmer 에이전트가 실제 구현([5] 단계)에 들어간다.

큰 그림은 이렇다. 플레이어가 연구소를 짓고 골드·시간을 들여 "우리 팀 근접 유닛 공격력 Lv3" 같은 연구를 완료하면, **그 팀의 해당 유닛 전부가 그 순간부터 강해진다.** 이를 무겁게(유닛마다 스탯 재계산 후 네트워크 재동기화) 구현하지 않고, **"기본 스탯 × 팀 연구 배율"을 데미지·이동을 실제로 쓰는 순간에 곱하는 방식((B) 방식)** 으로 구현한다. 서버는 팀별 연구 레벨만 들고 있으면 되고, 이미 전장에 있는 유닛도 자동으로 소급 강화된다.

여기에 이번 작업에서 처음 도입되는 **방어력** 스탯이 더해진다. 방어력은 받는 피해를 비율로 줄여 주며(공식은 아래), 모든 유닛이 0에서 시작해 연구로만 올린다. 마지막으로 초월 종족 전용 **자연회복**(모든 초월 유닛이 매초 체력을 조금씩 회복)이 기존 힐/도트 인프라를 재사용해 얹힌다.

계획의 순서는 "안전한 데이터 토대(방어력 필드·업그레이드 상태·그룹 매핑) → 전투 공식 수정 → 실시간 배율 적용 → 자연회복 → 연구소 UI/네트워크 → AI"다. 각 항목이 어떤 게임 규칙에 근거하는지 함께 표기했다.

> **⚠️ 세부 기술 설계·최종 확정은 [5] 구현 단계에서 game-programmer가 담당한다.** 이 Plan은 접근 방향·파일 범위·근거·위험을 확정하는 문서이며, 클래스/메서드 시그니처, 조회 지점의 정확한 위치, 캐싱 전략 등 세부 구현은 구현 에이전트가 결정한다.

---

## ⚠️ 최상단 고지 — 기존 로직 변경 범위 (기존 로직 제거 규칙 대비)

WORKFLOW.md [4] "기존 로직 제거 규칙"에 따라 문서 최상단에 명시한다.

- **이 작업에는 "기존 로직 제거"가 없다.** 대부분 신규 코드 추가다.
- **기존 로직 변경은 데미지 공식 in-place 수정**이다: `UnitCombatUseCase`·`TowerCombatUseCase`의 최종 데미지 적용(직격·스플래시·타워→유닛)을 `TakeDamage(공격력)` → `TakeDamage(방어력 감쇄 적용값)`으로 **수정**한다. **DoT 틱값은 감쇄 미적용이므로 변경 없음**(2026-07-23 확정). 여기에 Tank/CannonCart 건물 2배(항목 10)는 최종 데미지 지점에 신규 분기를 얹는다(신규 추가).
- 이 변경은 **제거가 아니라 in-place 수정**이며, **방어력이 0이면 결과가 기존과 정확히 동일**하다(`방어력/(방어력+K)=0` → 감쇄 0% → `Max(1, Round(공격력×1))=공격력`). 즉 연구 이전 상태의 모든 전투는 회귀 없이 그대로 동작한다(하위호환).
- 하위호환이 보장되므로 "비활성화(주석) 우선" 대상이 아니라 안전한 수정으로 처리한다. 단, 데미지 공식은 전 전투에 영향을 주므로 [6] 사용자 실기에서 직격·스플래시·타워 경로의 회귀 여부를 반드시 확인한다.

---

## 확정 결정·밸런스 수치 (game-design-lead 확정 — 구현 근거, 2026-07-23 최종 확정값)

- **전투 스탯 ×10 스케일**: 유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값(2→20/s, 5→50/s), 힐량(20→200, 10→100, 1→10/s) 전부 ×10. HP·공격력 동일 배율로 **TTK 불변**. 사거리·이동속도·쿨다운·생산/연구 시간·모든 골드 비용·채굴 수입·비율은 불변. 개별 값 권위 소스 `StatsReference.md`.
- 공격력: 레벨당 **Round(공격력×8%)의 고정 정수 등차**, Lv1~5, Lv5 ≈ ×1.40(유닛별 증가폭은 `StatsReference.md` 공격력 표). 이동속도: 배율 ×1.000~×1.320(Lv5 +32%).
- 방어력: 전 유닛 기본 0, Lv0~5 = **0/8/16/24/32/40**, Lv5 → 감쇄율 25%(실효 HP +33%).
- 자연회복: 고정 HP/s, Lv0~5 = **0/3.0/6.0/9.0/12.0/15.0**(레벨당 +3.0).
- 데미지 공식: `데미지 = Max(1, Round(공격력 × (1 − 방어력/(방어력+K))))`, **K=120**, floor 1, 감쇄율 하드캡 60~65% 코드 포함.
- 힐량 트랙: BloomFairy 힐 200→280(+16/Lv), TorrentSpirit 아군 힐 100→140(= 물 공격력×0.5).
- 비용(스탯 1종 Lv1~5, 기본 그룹): **80/120/180/260/360(합 1,000)**. 연구시간: **15/25/35/50/70초**.
- 종족 비대칭: 효과 동일, **비용만 배율** — 초월 동물 ×2.0, 자연회복 ×2.5, 초월 식물 포함 그 외 ×1.0. 인간 탈것 ×0.85는 미채택.
- 연구소 건물: 건설비 200골드(불변), HP는 ×10(Human/Spirit 1000·Trans 1500), 스테이지 업그레이드 없음.
- 연구 취소 환불: 진행 중 연구 파괴 취소 시 **투입 골드 100% 환불**, 완료 레벨 비용은 환불 대상 아님.
- **확정 쟁점(BalanceReview §F — 3건 전부 확정, 2026-07-23):** **F-1** = Lv0 Tank vs Fox/RabbitTrickster 원샷 관계 **유지**(Fox/Rabbit HP 미변경 — 코스트 기반 상성, TTK 불변). **F-2** = 방어력 표기 **0/8/16/24/32/40 · K=120** 확정(대안 80~400/K=1200 미채택, 감쇄율 동일). **F-3** = 초월 식물 그룹 비용 배율 **×1.0(합 1,000)** 확정(대안 ×0.7 미채택).

---

## 구현 항목 (각 항목에 GameSystemRules·아키텍처 근거 명시)

### 항목 0. 전투 스탯 ×10 스케일 개편 — config 에셋 재조정 (Infrastructure / 데이터)
**무엇을**: 전 유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값, 힐량을 **×10으로 재조정**한다. 이는 코드가 아니라 **ScriptableObject config 에셋의 값을 재설정**하는 데이터 작업이다. 대상 에셋(실제 프로젝트 확인 경로):
- **유닛 스탯 config** — `Assets/_Project/Resources/Config/UnitStatsConfig.asset`(스키마: `UnitStatsConfig.cs` / `UnitStatEntry`). `maxHp`·`attackPower`를 ×10. 신규 방어력 필드(`defense`, 기본 0 → 항목 1에서 스키마 추가)도 여기서 관리(전 유닛 Lv0=0).
- **건물 스탯 config** — `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`(스키마: `BuildingStatsConfig.cs` / `BuildingTypeEntry`). `humanMaxHp`·`spiritMaxHp`·`transcendenceMaxHp`(전 건물 HP)와 타워 공격력 `humanAttackPower`·`spiritAttackPower`·`transcendenceAttackPower`를 ×10.
- **특수공격 config** — `Assets/_Project/Resources/Config/SpecialAttackConfig.asset`(스키마: `SpecialAttackConfig.cs`). DoT 틱값 `_blastDotPerSecond`(MushroomBomber 2→20), `_infernoDotPerSecond`(InfernoSpirit 5→50)를 ×10. 힐값 `_bloomHealAmount`(BloomFairy 20→200), `_waveHeal`(TorrentSpirit 아군 힐 10→100)를 ×10. **스플래시·부채꼴·파도 비율은 불변**(`_quakeSplashRatio` 0.5 유지, `_sweepReach`/`_sweepArcHalfAngle`/`_waveWidth`/`_waveLength`/`_blastRadius`/`_quakeRadius` 등 반경·각도·시간값 전부 유지).
- **MistShrine(HealShrine) 힐 → 설계값 10 HP/s(범위 3)**: 코드 조사 결과 **HealShrine 힐은 현재 미구현**이다(전용 config 필드도 힐 로직도 없음, `BuildingType.HealShrine=6` 열거형만 존재). 따라서 **기존 에셋 ×10 재조정 대상이 아니다.** 10 HP/s는 `StatsReference.md`/`BalanceReview.md`에 있는 설계 스펙이며, 향후 HealShrine 힐 기능을 구현할 때 **신규 config 필드(예: `SpecialAttackConfig` 또는 건물 힐 전용 필드)에 10 HP/s(이미 ×10 반영된 확정값)로 신설**한다. (이번 ×10 config 재설정 범위에는 포함되지 않음.)

**불변(×10 대상 아님)**: 사거리·감지거리(타일)·이동속도(칸/초)·공격 쿨다운·생산/연구 시간·**모든 골드 비용**(생산비·건설비·업그레이드비·연구비)·채굴 수입(10골드/초)·스플래시 및 힐 **비율**(Quake 스플래시 50%, TorrentSpirit 힐=공격력×0.5, Tank/CannonCart 건물 2배 등). → 위 config에서 `attackRange`/`detectRange`/`moveSpeed`/`attackCooldown`/`productionTime`/`goldCost`/`populationCost`/`upgradeCost` 및 각종 반경·비율 필드는 **손대지 않는다**.

**근거**:
- `BalanceReview.md` §A(유닛 HP·공격력)·§A-4(DoT·스플래시·힐)·§B(건물·타워)와 `GameSystemRules_Upgrade.md` 규칙 1(×10 스케일 대상·불변 대상).
- HP·공격력을 **동일 배율**로 키우므로 **TTK 불변**(상성·매치업 불변, `BalanceReview.md` 부록 TTK 표).
- 개별 ×10 값의 권위 소스는 `StatsReference.md`(×10 반영은 [12] 문서 단계).

**주의(아키텍처 교훈, `.claude/MEMORY.md`)**: **Inspector(ScriptableObject) 값이 코드 기본값보다 우선**한다. 코드의 폴백 기본값(예: `SpecialAttackConfig`의 `_infernoDotPerSecond = 5f`)만 바꿔서는 실제 런타임에 반영되지 않으며, **`.asset` 파일의 값을 실제로 재설정해야 한다.** 재설정 방식(Editor 1회성 스크립트 vs 수동 편집)은 [5]에서 확정하며, 대량·일괄 재설정이므로 **WORKFLOW [5-2] Inspector 에디터 스크립트**(메뉴 `Hexiege/...` 형태)가 유력하다.

### 항목 1. 신규 방어력 스탯 추가 (Domain / Infrastructure)
**무엇을**: `UnitStats.StatValues`(Domain/Unit/UnitStats.cs)에 `Defense` 필드 추가(기본 0), 조회 API `GetDefense`(가칭) 신설. `UnitData`(Domain/Unit/UnitData.cs)에 `Defense` 신규 필드(기본 0) 추가. 필요 시 `UnitStatsConfig`(Infrastructure)에 방어력 항목(전 유닛 0이므로 폴백 0 처리 가능). **또한 `BuildingData`(Domain/Building/BuildingData.cs)에도 Defense 필드(기본 0) 추가** — 감쇄 공식이 유닛·건물에 **통일 적용**되도록(별도 `target is UnitData` 분기 불필요, qa #6 해소). 단 **건물 방어력은 업그레이드 트랙이 없어 항상 0(실질 무감쇄)** 이며, 건물 방어 트랙은 이번 범위 밖(향후 확장 보류). (코드 확인: 현재 `BuildingData.cs`에 Defense 필드 없음.)
**근거**:
- 확정 시스템 프레임 3(방어력 = 신규 스탯, 전 유닛 기본 0) + 사용자 결정(건물 Defense 필드 공통 0·트랙 없음·보류).
- `GameSystemRules_Upgrade.md` 규칙 5("건물 방어력 = 0 고정, 트랙 없음(향후 확장 보류)").
- 아키텍처 제약(`.claude/MEMORY.md`): Defense 값 주입은 UnitStatsConfig(Infrastructure) → `UnitStats.Initialize` 경로로, Domain이 Infrastructure를 직접 참조하지 않는 기존 패턴 유지.
**참고**: 기본값 0이라 UnitStatsConfig에 필드를 넣지 않고 조회 폴백 0으로 갈지, 명시 필드로 둘지는 [5] 확정.

### 항목 2. 데미지 공식 수정 — 방어력 감쇄 일괄 삽입 (Application) — *유일한 기존 로직 변경*
**무엇을**: `UnitCombatUseCase`·`TowerCombatUseCase`의 최종 데미지 적용 지점에 비율 감쇄식을 일괄 삽입한다.
- 직격: `ApplyDamageToVictim`(UnitCombatUseCase.cs:1090~1095) — `target.TakeDamage(attacker.AttackPower)`에 감쇄 적용.
- 스플래시: `ApplyQuakeSplash`(1338~1349) 등 `ApplyFixedDamageToVictim` 경유 임의 수치 피해.
- **타워→유닛 (신규 포함, qa Major 해소):** `TowerCombatUseCase.ExecuteTowerAttack`(TowerCombatUseCase.cs:200~206)의 `target.TakeDamage(damage)`(현재 감쇄 없음). 타워→유닛은 유닛 방어 감쇄 대상이므로 직격과 동일 헬퍼를 적용한다.
- **DoT: 방어력 감쇄 미적용** (2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음). `ApplyBlastDot`/`ApplyInfernoDot`(규칙 40·42) 틱 피해에는 방어력 감쇄·공격력 배율 **둘 다 미적용**(고정 틱값 유지). → DoT 삽입 지점 이슈(qa Moderate)도 소멸.
감쇄 계산은 순수 함수 헬퍼(Domain 권장)로 두어 직격·스플래시·타워 지점이 동일 공식을 쓰게 한다. floor 1·하드캡 60~65% 포함.
**근거**:
- 확정 시스템 프레임 3(감쇄식·K=120·floor 1·하드캡) + 사용자 확정(방어 감쇄는 **직격·스플래시·타워→유닛에만** 적용, **DoT 미적용**).
- `GameSystemRules_Units.md` 규칙 44(방어력 감쇄 — 직격·스플래시·타워→유닛 일괄, DoT 미적용) / 규칙 16(범위 공격은 데미지 계산 방식의 차이) / 규칙 18(데미지는 서버 타이머 권위) / 규칙 40·42(DoT 틱·유닛별 값 분리 — 감쇄 대상 아님).
- `GameSystemRules_Buildings.md` 방어 타워 규칙 9(서버 권위 데미지) — 타워→유닛 데미지도 동일 감쇄 대상(포함 확정).
**하위호환**: 방어력 0이면 기존과 동일(최상단 고지 참조).

### 항목 3. 팀별 업그레이드 상태 UseCase (Application 신규)
**무엇을**: 신규 `UnitUpgradeUseCase`(가칭). 팀별 트랙 레벨을 `Dictionary<(TeamId, UpgradeGroup, UpgradeStat), int level>`로 보관하고, 레벨을 실제 효과값으로 변환하는 조회 API를 제공한다. 트랙은 **레벨(0~5)만** 저장하고, 효과값은 조회 시점에 산출한다.
- **공격력** — "그룹 균일 배율"이 아니라 **유닛별 고정 정수 증가치**다. 그룹 트랙은 레벨만 보관하고, 각 유닛의 증가치 = `Round(기본공격력 × 8%) × 레벨`(유닛별로 상이). 예: Sniper +14/Lv, Tank +24/Lv, Assault +1/Lv(`BalanceReview.md` §C-1 증가폭 표). 조회 API는 `(유닛 기본공격력, 레벨) → 증가치` 또는 `(유닛, 팀) → 유효 공격력`을 반환하도록 설계.
- **이동속도** — 전 유닛 공통 **배율**. 레벨→×1.000/×1.064/×1.128/×1.192/×1.256/×1.320(Lv5 +32%, `BalanceReview.md` §C-3).
- **방어력** — 전 유닛 공통, 레벨→**0 / 8 / 16 / 24 / 32 / 40**(`BalanceReview.md` §C-2). 감쇄 상수 K=120은 항목 2 헬퍼가 사용.
- **자연회복** — 초월 공용 1트랙, 레벨→**0 / 3.0 / 6.0 / 9.0 / 12.0 / 15.0 HP/s**(레벨당 +3.0, `BalanceReview.md` §D). 항목 7이 사용.

레벨 상승·비용/시간 조회(§규칙 10 표)·진행 상태 질의도 담당.
**근거**:
- 확정 시스템 프레임 2(팀 배율 실시간 적용).
- **기존 선례**: `ResourceUseCase._incomeMultipliers` + `SetIncomeMultiplier`(ResourceUseCase.cs:41·76, `GameSystemRules_AI.md` 규칙 34) — 팀별 배율 레이어를 Application에 두는 검증된 패턴.
- 아키텍처 제약(`.claude/MEMORY.md`): Application은 Unity.Netcode 직접 참조 금지 → 상태 보관·조회는 순수 Application, 네트워크 동기화는 항목 6에서 Infrastructure가 담당(인터페이스는 Application 선언·Infrastructure 구현, 의존성 역전).

### 항목 4. 유닛 → 그룹 매핑 헬퍼 (Domain)
**무엇을**: `UnitType → UpgradeGroup`(인간 근접/원거리/탈것, 정령 불/물/땅, 초월 동물/식물) 정적 헬퍼. 초월계 여부 판정(자연회복 대상) 포함.
**근거**:
- 확정 시스템 프레임 5(유닛→그룹 매핑 표).
- **기존 패턴**: `BuildingTypeHelper`(BuildingType.cs 주석 — 생산 여부·단계·다음 단계 조회를 헬퍼로 분리) — 동일하게 순수 Domain 정적 헬퍼로 매핑.

### 항목 5. (B) 실시간 배율 적용 — 전투·이동 사용 지점 (Application)
**무엇을**: 데미지·이동을 실제로 쓰는 지점에서 팀 연구 효과를 적용한다. 두 스탯의 적용 방식이 **다르다**는 점에 주의한다.
- **공격력 = 유닛별 고정 정수 증가치**: `유효 공격력 = 기본 공격력 + (Round(기본 공격력 × 8%) × 레벨)`. 그룹 균일 배율이 아니라 유닛마다 증가폭이 다르다(§항목 3). 데미지 계산 시 이 유효 공격력을 사용. 적용 대상은 **공격력을 직접 쓰는 피해**(직격·스플래시·TorrentSpirit 파도). 고정 DoT 틱값에는 **미적용**(유지).
- **이동속도 = 배율**: `유효 이동속도 = 기본 이동속도 × 이동배율(Lv0 ×1.000 ~ Lv5 ×1.320)`. A* 타일 이동·전투 이동(동일 스탯) 사용 지점에 적용.
- **힐 = 그룹 공격력 트랙 레벨 직접 조회 (신규 로직, qa Major 해소)**: BloomFairy 힐·TorrentSpirit 아군 힐도 연구로 스케일하되, **`AttackPower` 필드 경로가 아니라 해당 그룹의 공격력 트랙 레벨을 직접 조회하는 별도 신규 로직**으로 힐량을 산출한다. 코드 확인: 현재 힐은 `SpecialAttackConfig` **고정 상수**(`_bloomHealAmount`·`_waveHeal`)이며 `AttackPower`를 참조하지 않는다(qa 확인). 특히 BloomFairy는 순수 힐러(공격 없음)라 AttackPower 경로가 원천 불가. → BloomFairy = 초월 **식물** 공격력 트랙(200→280), TorrentSpirit = **물** 공격력 트랙(아군 힐 = 물 공격력×0.5, 100→140)을 조회.
유닛 스냅샷 필드(`AttackPower`/`MoveSpeed`)는 **변경하지 않는다** → 소급 강화 자동 성립.
**근거**:
- 확정 시스템 프레임 2((B) 방식, 재계산·재동기화 불필요) + `BalanceReview.md` §C-1(유닛별 고정 정수 공격력 증가폭)·§C-3(이동속도 배율)·§C-4(힐량 트랙)·§C-1 주의(고정 DoT 미반영).
- `GameSystemRules_Units.md` 규칙 5(A*·전투 이동 동일 이동 속도 스탯) — 이동 배율을 두 이동 모두에 동일 적용.
- `GameSystemRules_Upgrade.md` 규칙 6(힐량 — 그룹 공격력 트랙 레벨 직접 조회하는 신규 로직으로 스케일).
- Research.md 3)(AttackPower·MoveSpeed는 읽기전용 스냅샷) — 스냅샷 불변 + 사용 지점 배율로 (B) 성립.
**참고**: 매 계산 조회 vs 이동 시작 시 조회(성능·소급성 절충)는 [5] 확정.

### 항목 6. 팀별 트랙 레벨·진행 타이머 서버 권위 동기화 (Infrastructure)
**무엇을**: 팀별 트랙 레벨과 진행 중 연구 타이머를 서버 권위로 관리·동기화. 연구 요청 ServerRpc(연구소 건물 대상 트랙 지정) → 서버가 골드 검증·차감·트랙 잠금·타이머 시작 → 완료 시 레벨 반영. 연구소 파괴 시 진행 중 연구 취소·투입 골드 100% 환불(완료분 유지).
- **동기화 재정의 (qa Major 해소 — 기존 "비공개"를 구체화):** 완료된 업그레이드 레벨(효과)은 **양 클라이언트 모두에 동기화되어 양쪽에 적용**된다(상대의 강화된 유닛이 내 화면에서도 올바른 데미지를 내야 하므로). 반면 **진행 중인 연구(어떤 트랙·타이머)는 소유 플레이어에게만 표시**(상대는 진행 상태를 모름). 즉 "비공개 = 진행 중 연구 UI만 비공개, 완료 효과는 양쪽 공개·적용".
- **구현 방향:** 연구 완료 시 서버가 해당 팀 트랙 레벨을 **양 클라 브로드캐스트**(`NetworkResourceSync`가 `ReadPermission=Everyone`으로 양쪽에 전파하는 선례와 동일 계열, 코드 확인). 진행 중 상태는 소유 클라 대상으로 전송(타겟 `ClientRpcParams` — `NetworkBuildingController.BuildFailedClientRpc`가 요청 클라에게만 보내는 패턴 계열, 코드 확인).
**근거**:
- 확정 시스템 프레임 7(진행 중 트랙 잠금·숨김·복수 연구소 병렬·파괴 취소) + 프레임 8(서버 권위) + 사용자 확정(완료 효과 양 클라 적용, 진행 UI만 비공개).
- `GameSystemRules_Upgrade.md` 규칙 8·9(비공개 = 진행 UI만, 완료 레벨 양 클라 브로드캐스트).
- `GameSystemRules_Buildings.md` 규칙 5(생산 큐 골드 차감분 전액 환불) — 연구 취소 100% 환불이 이 "차감분 전액 환불" 원리와 동일. 규칙 4(철거 50% 환불)와는 구분(연구 취소는 100%).
- 아키텍처 제약: NetworkBehaviour는 Infrastructure에만 / RPC 접미사 `ServerRpc`·`ClientRpc` 필수 / `NetworkBuildingController` 패턴(건물 RPC 소유처) 재사용.
- `BuildingType.Research = 4` 이미 존재(BuildingType.cs:37) → RPC int 순서 변경 없음(안전).

### 항목 7. 자연회복 HoT (Application, 기존 HoT 인프라 재사용)
**무엇을**: 초월계 전 유닛에 조건 없는 상시 HoT(고정 HP/s). 레벨별 회복량 Lv0~5 = **0 / 3.0 / 6.0 / 9.0 / 12.0 / 15.0 HP/s**(레벨당 +3.0, `BalanceReview.md` §D). 최대 HP 미변경, `UnitData.Heal`로 MaxHp 클램프(풀피 유닛은 회복량 0으로 자연 무동작). 자연회복 트랙 레벨 0이면 무동작.
- **힐과 별개 독립 채널 (qa Critical 해소):** 자연회복(초월 상시 HoT)과 BloomFairy 힐은 **서로 독립적으로 동시 적용**되어야 하며 한쪽이 다른 쪽을 덮어쓰면 안 된다. 코드 확인: 현재 HoT 시스템은 `AddOrRefreshTimedEffect`가 대상별 효과를 `(TargetId, Kind)` 키로 관리하고 힐은 `TimedEffectKind.Heal` 버킷 하나뿐이라, 자연회복을 같은 Heal 버킷에 넣으면 BloomFairy 힐과 **서로 덮어써 한쪽이 소멸**한다(qa 확인). → 자연회복은 `_activeTimedEffects`의 Heal 버킷과 **분리된 독립 채널**(신규 `TimedEffectKind` 또는 별도 자료구조)로 구현해 BloomFairy 힐과 **상호 간섭을 금지**한다.
**근거**:
- 확정 시스템 프레임 6(상시 HoT·초월 전 유닛·고정 HP/s·기존 틱 인프라 재사용) + 결정(자연회복 초월 공용 1트랙) + 사용자 확정(힐과 별개 독립 채널).
- `GameSystemRules_Upgrade.md` 규칙 7(자연회복 — 힐과 별개 채널, 상호 간섭 금지).
- `GameSystemRules_Units.md` 규칙 30(힐 서브시스템 `UnitData.Heal`+`OnEntityHealed`+`NetworkHealthSync` 힐 동기화) / 규칙 34(HoT/DoT 공용 시스템, 서버 권위) / 규칙 40(1초 간격 discrete 틱, 이중 틱 금지) — 자연회복을 이 인프라 위에 얹는다.
- 서버 틱 진입점: 싱글=`GameBootstrapper.Update`(`!IsNetworkMode`), 멀티=`NetworkCombatController`(IsServer). **이중 틱 금지**.
**참고**: "상시"이므로 레코드 만료 없는 상시 틱 vs 무한 HoT 부여 방식은 [5] 확정. 풀피 유닛은 클램프로 자연 무동작.

### 항목 8. 연구소 UI (Presentation)
**무엇을**: 연구 패널(생산 패널 패턴). 트랙별 버튼·현재 레벨·다음 레벨 비용/시간 표시, 연구 착수, 진행 중 트랙 UI 숨김(팀 잠금), 골드/타이머 표시. 비용 텍스트 색상은 골드 대비 재평가.
**근거**:
- 확정 시스템 프레임 7(진행 중 트랙 숨김·연구 시간 표시).
- `GameSystemRules_UI.md` 생산 패널 UI 규칙(팝업 열기/닫기, 큐/버튼 입력) + 공통 UI 규칙 7·14(비용 텍스트 색상: 골드 부족 빨강/충분 흰색, 골드 변경 시 재평가) + 규칙 1·2·4·5(Canvas Scaler·앵커·SafeArea·CanvasGroup 숨김) — 연구 패널도 이 공통 규칙 준수.
**참고**: 진행 중 트랙 "숨김"의 정확한 UI 표현(숨김 vs 비활성+타이머)은 [5]에서 프레임 7 문구에 맞춰 확정.

### 항목 9. AI 시나리오 — 연구 착수 스텝 (방향만)
**무엇을**: 각 종족 시나리오 Phase 3~4에 연구소 착수 + 우선 트랙 스텝 추가. Human A=근접 공격력, Spirit A=불 공격력, Trans A=동물 공격력, Trans B=자연회복 등 **방향만**. 세부 delaySeconds 개편은 범위 밖.
**근거**:
- 확정 시스템 프레임 8(AI도 업그레이드 사용) + game-design-lead AI 방향.
- `GameSystemRules_AI.md`(빌드오더 Phase 1~4 구조·actionType) 및 종족별 시나리오 문서(`GameSystemRules_AI_Scenario_Human/Spirit/Transcendence.md`) — 기존 빌드오더 테이블에 연구 스텝을 얹는다.
**참고**: actionType 신설 여부·정확한 배치는 game-programmer + game-design-lead 협의로 [5]에서 확정.

### 항목 10. Tank·CannonCart 건물 대상 2배 데미지 (Application, 신규 구현 — qa 전제 오류 해소)
**무엇을**: Tank·CannonCart가 **건물 대상 데미지 2배**를 준다. 코드 확인 결과 이 "건물 2배" 배율은 **현재 코드 어디에도 미구현**(qa 확인 — `StatsReference.md` 비고에만 존재, 코드엔 배율 없음)이라 이번에 신규 구현하는 항목이다.
- **적용 순서:** 건물은 방어력 0이라 감쇄가 무의미하므로 **건물 대상 데미지 = 공격력 × 2**(유닛 대상은 ×1 + 유닛 방어 감쇄, 항목 2). 최종 데미지 적용 지점에서 "대상이 건물이고 공격자가 Tank/CannonCart면 2배" 분기를 둔다.
- **적용 위치(개략):** `UnitCombatUseCase`의 최종 데미지 적용 지점(직격 `ApplyDamageToVictim` 계열 `target.TakeDamage` — UnitCombatUseCase.cs:1095). 대상이 건물(`BuildingData`)인지 판정 후 공격자 UnitType이 Tank/CannonCart면 ×2.
**근거**:
- `StatsReference.md`(Tank·CannonCart "건물에 2배 대미지" 비고 — 구현 예정) + `BalanceReview.md` 원칙 2(Tank/CannonCart 건물 2배 비율 불변) + 사용자 확정(신규 구현 필요).
- `GameSystemRules_Units.md` 규칙 44(방어력 감쇄는 유닛 대상 — 건물은 방어 0이라 감쇄 무의미, 2배만 적용).
**참고**: 정확한 분기 지점(직격만인지 스플래시·파도 공성 포함인지)은 [5]에서 game-programmer가 확정. 현재 건물 공성은 직격·특수 경로별로 분산되어 있어 2배 분기 삽입점 확정 필요.

---

## 위험 요소

1. **데미지 공식이 전 전투에 영향** — 방어력 0 하위호환으로 회귀 위험 완화(항목 2). 그래도 직격·스플래시·타워→유닛 데미지 경로에서 회귀 여부 실기 확인 필요(DoT는 감쇄 미적용이라 이 공식 변경의 영향 밖). Tank/CannonCart 건물 2배 분기(항목 10)도 건물 공성 회귀 확인.
2. **(B) 배율 스레딩의 레이어 준수** — 전투·이동 사용 지점에 팀 배율을 Application 계층 규칙(Netcode 미참조) 위반 없이 전달해야 함. 조회 시점(매 계산 vs 시작 시)에 따라 소급성·성능이 달라짐.
3. **성능** — 자연회복 매초 틱 + 매 데미지·이동마다 배율 조회가 다수 유닛 환경에서 부하가 될 수 있음. 조회 캐싱/딕셔너리 접근 최소화 [5]에서 고려.
4. **멀티플레이 상태 동기화** — 팀별 트랙 레벨·진행 중 연구 타이머의 서버 권위 동기화, 파괴 시 취소·환불의 정확한 서버 처리(항목 6). **완료 레벨은 양 클라 브로드캐스트(양쪽 효과 적용), 진행 중 연구 상태만 소유 클라 한정(진행 UI 비공개)** — 두 채널 분리 처리 필요.
5. **BuildingType 안전** — `Research` 이미 존재(값 4) → RPC int 순서 변경 없음(위험 없음, 확인 완료).
6. **×10 config 재설정 누락/불일치(항목 0)** — Inspector(ScriptableObject) 값이 코드 기본값보다 우선하므로, 코드만 고치고 `.asset`을 안 고치면 런타임에 구 수치(1배)로 동작한다. 유닛·건물·특수공격 3개 config를 누락·오타 없이 일괄 ×10 해야 하며, **비율/반경/시간/골드 필드를 실수로 건드리지 않아야** 한다. 대량 편집이라 WORKFLOW **[5-2] Inspector 에디터 스크립트**로 일괄 처리하는 것이 안전(수동 편집은 누락 위험). ×10 후 실기에서 TTK 불변(상성 유지)을 반드시 확인.

---

## 아키텍처 제약 체크리스트 (`.claude/MEMORY.md`)
- [ ] Application은 Unity.Netcode 직접 참조 금지 → 업그레이드 상태는 Application, 네트워크 동기화는 Infrastructure(의존성 역전, 인터페이스는 Application 선언).
- [ ] NetworkBehaviour는 Infrastructure에만(연구 RPC).
- [ ] RPC 메서드명 `ServerRpc`/`ClientRpc` 접미사 필수.
- [ ] GameBootstrapper가 유일 조합 루트(신규 UseCase·Config 배선은 여기서).
- [ ] 서버 권위: 골드 차감·데미지 판정·연구 완료는 서버.
- [ ] Domain은 Core 미참조(방어력 필드·그룹 매핑은 순수 C#).

---

## 예상 변경/추가 파일 (개략 — [5]에서 최종 확정)
> 정확한 파일 목록·클래스명은 game-programmer가 구현 시 확정. 아래는 범위 파악용 개략치.

**[신규 예상]**
- `Scripts/Application/UseCases/UnitUpgradeUseCase.cs`(가칭) — 팀별 트랙 레벨·배율/방어/회복 조회
- `Scripts/Domain/Unit/UpgradeGroup.cs` + `UnitType→UpgradeGroup` 매핑 헬퍼(가칭)
- 연구 요청 RPC / 팀별 트랙 동기화 — `Scripts/Infrastructure/...`(NetworkBuildingController 패턴)
- 연구 패널 UI — `Scripts/Presentation/...`(생산 패널 패턴)

**[수정 예상 — config 에셋 (항목 0 ×10 스케일, 데이터 재설정)]**
- `Assets/_Project/Resources/Config/UnitStatsConfig.asset` — 전 유닛 `maxHp`·`attackPower` ×10 (+ 방어력 필드 추가 시 전 유닛 0)
- `Assets/_Project/Resources/Config/BuildingStatsConfig.asset` — 전 건물 HP(종족 3필드) ×10, 타워 공격력(종족 3필드) ×10
- `Assets/_Project/Resources/Config/SpecialAttackConfig.asset` — DoT 틱값(`_blastDotPerSecond` 2→20, `_infernoDotPerSecond` 5→50) ×10, 힐값(`_bloomHealAmount` 20→200, `_waveHeal` 10→100) ×10, **비율/반경/시간 필드는 불변**
- (미구현 기능) MistShrine(HealShrine) 힐 — 현재 config·로직 없음(열거형만 존재). 기존 에셋 ×10 대상 아님. HealShrine 힐 구현 시 신규 config 필드에 설계값 10 HP/s(확정값)로 신설
> ⚠️ 위 config 재설정은 Inspector(ScriptableObject) 값 변경이므로 **WORKFLOW [5-2] Inspector 에디터 스크립트**(메뉴 `Hexiege/...` 1회성)가 필요할 수 있다. 코드 폴백 기본값만 바꾸면 반영되지 않음(항목 0 주의 참조).

**[수정 예상 — 코드]**
- `Scripts/Domain/Unit/UnitStats.cs` — StatValues에 Defense 필드 + GetDefense
- `Scripts/Domain/Unit/UnitData.cs` — Defense 필드
- `Scripts/Domain/Building/BuildingData.cs` — Defense 필드(항목 1, 기본 0·업그레이드 트랙 없음·보류, 감쇄 공식 통일 적용용)
- `Scripts/Infrastructure/Config/UnitStatsConfig.cs` — `UnitStatEntry`에 방어력 필드(`defense`) 스키마 추가(항목 1, 기본 0)
- `Scripts/Application/UseCases/UnitCombatUseCase.cs` — 데미지 감쇄식 삽입(직격·스플래시 — **DoT 제외**) + 유효 공격력(유닛별 증가치) 반영 + 힐 그룹 공격력 트랙 조회(항목 5) + Tank/CannonCart 건물 2배 분기(항목 10)
- `Scripts/Application/UseCases/TowerCombatUseCase.cs` — 타워→유닛 데미지(`ExecuteTowerAttack`)에 방어력 감쇄식 적용(항목 2, qa Major 해소)
- 이동 속도 사용 지점(전투/이동 UseCase) — 이동배율 적용
- `GameBootstrapper` — 신규 UseCase·Config 배선
- AI 시나리오 관련(방향만) + `GameSystemRules_AI_Scenario_*.md`
- 관련 GameSystemRules 문서(신규 시스템 규칙 반영은 [12] 문서 반영 단계에서)

**[문서]**
- 본 task 폴더 `Research.md` / `Plan.md`

---

## 참고 문서
- **확정값 단일 진실 소스**: `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`(old→new 전수 대조) / **구현 계약**: `GameSystemRules/GameSystemRules_Upgrade.md`
- `CLAUDE.md`, `Assets/_Project/Docs/WORKFLOW.md`(특히 [5-2] Inspector 에디터 스크립트), `.claude/MEMORY.md`(Inspector 우선 교훈), `AGENTS.md`
- `GameSystemRules.md`(인덱스) / `GameSystemRules_Units.md`(규칙 5·16·18·30·34·40·42) / `GameSystemRules_Buildings.md`(규칙 4·5·9) / `GameSystemRules_UI.md`(공통·생산 패널) / `GameSystemRules_AI.md` + 종족별 시나리오 문서

---

## 완료 결과 (실제 구현 — 기능·멀티플레이 실기 PASS, 2026-07-31)

> WORKFLOW [12] 문서 반영. 히스토리 보존 원칙에 따라 위 계획 본문은 그대로 두고, 실제 구현에서 계획과 달라진 점만 아래에 기록한다. Testcase.md는 사용자 지시로 미작성.

### 계획대로 구현된 항목
- **항목 0~7·10 전부 구현·멀티 실기 PASS.** 방어력 필드/감쇄 공식(K=120, floor 1, 하드캡 65%), 유닛별 고정 정수 공격력 증가, 이동속도 배율, 힐량 트랙(그룹 공격력 트랙 조회), 초월 자연회복(별개 채널), Tank/CannonCart 건물 2배, ×10 config, (B) 팀 배율 소급 강화, 서버 권위 네트워크 동기화 모두 계획대로.
- **신규 파일(확정)**: `Domain/Combat/DamageCalculator.cs`(계획엔 "Domain 순수 함수 헬퍼"로만 표기 → 별도 클래스 `DamageCalculator.ApplyDefense`로 확정), `Domain/Unit/UpgradeGroup.cs`(+`UpgradeGroupHelper`), `Application/UseCases/UnitUpgradeUseCase.cs`, `Infrastructure/Network/NetworkUpgradeController.cs`.
- **최종 데미지 수렴 헬퍼**: `UnitCombatUseCase.ComputeFinalDamage(attacker, target, rawDamage)`(private static)로 방어 감쇄 + Tank/CannonCart 건물 2배를 한 곳에 모음(계획 항목 2·10을 단일 헬퍼로 통합). 삽입 지점은 계획대로 `ApplyDamageToVictim`(직격)·`ApplyFixedDamageToVictim`(스플래시/파도)·`TowerCombatUseCase.ExecuteTowerAttack`(타워→유닛). DoT는 미경유(무감쇄) — 계획대로.

### 계획과 달라진 점 (정정)
- **⭐ 연구 패널 UI (항목 8) — "생산 패널 패턴" → "매트릭스 + BuildingPanelBase 2-레이어"로 확정 변경.** 계획은 "생산 패널 패턴, 트랙별 버튼"이었으나, 실제 구현은 **`ResearchPanelUI : BuildingPanelBase`**(공통 헤더·닫기·**철거+환불** 상속) + 본문 2-레이어 — **매트릭스**(그룹 행 × 공/방/속 열; 인간·정령 3×3, 초월 2×3 + 자연회복 전체폭 버튼 1개; 셀=Lv X/5+눈금+비용, `ResearchMatrixView`/`ResearchCellView`) ↔ **진행 게이지**(이름·Lv X→X+1·게이지·남은시간·취소, `ResearchProgressView`)로 확정. **연구소 단위 전환**(연구 중=진행 레이어, 유휴=매트릭스), 진행 중 트랙 잠금(팀 중복 방지), 배경 탭 닫기(Popup 규칙 8·9). 아이콘 대신 텍스트 라벨. 최종 설계는 `GameSystemRules_Upgrade.md` 규칙 13에 등재.
- **멀티플레이 완료 처리 서비스 스폰 레이스 버그 발견·수정**: `NetworkUpgradeController`가 `OnNetworkSpawn` 시점에 `IGameServices`가 아직 미등록일 수 있어(스폰 레이스), 사용 시점 지연 재조회 `ResolveServices()`로 복구하도록 구현. (계획엔 없던 실기 발견 버그 — qa-tester MEMORY에도 기록.)
- **AI 시나리오 연구 착수(항목 9)**: 코드(`AIOpponentController`/`BuildOrderStep` 관련)는 반영됐으나 **실기 미검증**(후속 보류). 계획대로 "방향만" 범위였고, 실기 확인은 별도 작업.

### 후속 보류 (미완/미검증 — 별도 작업, 과대 표기 금지)
① 연구 패널 UI 레이아웃 다듬기(현재 에디터 스크립트 자동생성 골격 — 사용자가 Unity에서 직접 다듬을 예정) ② 매트릭스 헤더 아이콘(공/방/속·그룹) 에셋 ③ AI 시나리오 연구 사용 실기 검증 ④ MistShrine(HealShrine) 힐 미구현(항목 0에서 이번 범위 밖으로 확정된 대로 유지) ⑤ 싱글플레이 자연회복 실기 미검증(코드상 정상 예상). ×10 적용·배선·디버그에 쓰였던 에디터·디버그 스크립트는 config `.asset` 커밋·씬 배선 반영 후 역할 종료로 제거됨.
