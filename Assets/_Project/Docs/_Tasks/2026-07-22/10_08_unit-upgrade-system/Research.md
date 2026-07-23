# Research — 연구소(Research) 기반 유닛 강화(업그레이드) 시스템

> **⚠️ 2026-07-23 확정값 갱신:** 이 문서의 초기 밸런스안(K=20, 방어 레벨당 +4/Lv5=20, 자연회복 0.5~2.5, 비용 830 등)은 이후 **밸런스 최종 확정 + 전투 스탯 ×10 스케일 개편**으로 갱신되었다. 확정값은 **K=120 · 방어 0/8/16/24/32/40 · 자연회복 3~15 HP/s · 표준 트랙 비용 1,000(80/120/180/260/360) · 연구 시간 15/25/35/50/70초**이며, 모든 전투 수치는 ×10 스케일이다. 아래 본문 수치는 확정값으로 갱신했다. 단일 진실 소스: `2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`, 구현 계약: `GameSystemRules/GameSystemRules_Upgrade.md`.

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

지금 Hexiege에는 유닛을 "더 강하게 만드는" 성장 요소가 없다. 경기 내내 유닛의 공격력·이동 속도는 처음 그대로이고, 방어라는 개념 자체가 존재하지 않는다. 이 작업은 플레이어가 **연구소(Research 건물)를 지어 골드와 시간을 들여 자기 팀 유닛을 강화**할 수 있게 하는 신규 시스템을 도입하기 위한 사전 조사(Research) 문서다.

강화할 수 있는 능력은 세 가지다.
- **공격력** — 강화할수록 유닛이 주는 피해가 커진다.
- **방어력(신규)** — 이번에 처음 도입되는 스탯. 강화할수록 받는 피해가 줄어든다. 모든 유닛은 방어력 0에서 시작하며, 오직 연구로만 올릴 수 있다.
- **이동 속도** — 강화할수록 유닛이 빨라진다.

여기에 초월(Transcendence) 종족 전용으로 **자연회복**(모든 초월 유닛이 전투 중에도 매초 체력을 조금씩 회복)이라는 네 번째 트랙이 추가된다.

핵심 설계 결정은 강화 효과가 **이미 전장에 나와 있는 유닛에게도 즉시 소급 적용**된다는 점이다. 즉 "연구를 완료하면 그 팀의 해당 유닛 전부가 그 순간부터 강해진다." 이를 위해 유닛마다 스탯을 다시 계산해 네트워크로 재동기화하는 무거운 로직 대신, **"기본 스탯 × 팀 배율"을 데미지·이동을 실제로 쓰는 그 순간에 곱하는 방식(이하 (B) 방식)** 을 채택했다. 팀별 연구 레벨만 서버가 들고 있으면 되고, 유닛 개개의 스탯을 건드릴 필요가 없다.

이 문서는 **코드를 바꾸지 않는다.** 현재 코드가 어떻게 생겼는지, 무엇을 새로 만들고 무엇을 고쳐야 하는지, 영향 범위와 위험이 어디인지를 파일·라인 근거와 함께 정리한다. 실제 구현 방법과 최종 설계 확정은 다음 단계(Plan.md 승인 후 [5] 구현 단계)에서 game-programmer 에이전트가 담당한다.

---

## 확정된 시스템 프레임 (사용자·game-design-lead 확정 — 변경 금지)

이 프레임은 이미 사용자와 확정된 내용이다. 본 조사는 이 프레임을 전제로 현재 코드를 분석한다.

### 전투 스탯 ×10 스케일 (강화의 전제)
- 유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값(2→20/s, 5→50/s), 힐량(20→200, 10→100, 1→10/s)을 전부 ×10. HP·공격력을 같은 배율로 키워 **TTK 불변**(상성·매치업 불변, 강화 그리드만 규칙적으로 개선). 사거리·이동속도·쿨다운·생산/연구 시간·모든 골드 비용·채굴 수입·비율은 불변.

### 강화 스탯과 효과
- 강화 스탯 3종: **공격력 / 방어력(신규) / 이동속도**. 최대 HP는 강화 대상 아님.
- 효과 적용 방식 = **(B) 기본값 × 팀 배율 실시간 적용** → 이미 생산된 유닛도 연구 완료 즉시 소급 강화. 유닛별 재계산·재동기화 로직 불필요.
- 공격력: 레벨당 **Round(공격력×8%)의 고정 정수 등차**(Lv1~5, Lv5 ≈ ×1.40). ×10 스케일 덕분에 죽은 레벨·불규칙 증가폭 0건. 이동속도: 배율 ×1.000~×1.320(레벨당 +6.4%p, Lv5 +32%).
- 방어력: 신규 스탯, 전 유닛 기본값 0. Lv0~5 = **0/8/16/24/32/40**.

### 방어력 데미지 감쇄 공식 (신규)
```
데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + K))))    // K = 120
```
- 최소 데미지 **1 보장(floor)** 필수.
- 감쇄율 **하드캡 60~65%** 를 미래 확장 안전장치로 코드에 포함(현 Lv5는 25%로 미도달).
- 방어 40 → 감쇄율 40/(40+120) = 25% (실효 HP +33%).
- 방어력 감쇄는 **직격 / 스플래시에만 적용**(타워→유닛 데미지 포함), **DoT 틱값에는 미적용**(2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음).
- 공격력 연구도 고정 수치 DoT(MushroomBomber 20/s, InfernoSpirit 50/s)에는 **반영 안 함**(DoT 틱값 고정). 즉 DoT는 공격력 연구·방어력 연구 어느 쪽에도 영향받지 않는 고정 틱값이다.

### 종족별 트랙 구조
| 종족 | 그룹 × 스탯 | 트랙 수 |
|------|------------|---------|
| 인간계 | 근접 / 원거리 / 탈것 × 공·방·속 | 9 |
| 정령계 | 불 / 물 / 땅 × 공·방·속 | 9 |
| 초월계 | 동물 / 식물 × 공·방·속 (6) + 자연회복(초월 공용 1트랙) | 7 |

### 유닛 → 그룹 매핑
| 종족 | 그룹 | 유닛 |
|------|------|------|
| 인간 | 근접 | LittleKnight, SpearMan, BattleAxe |
| 인간 | 원거리 | Pistoleer, Assault, Sniper |
| 인간 | 탈것 | Tank, CannonCart |
| 정령 | 불 | FlameSpirit, EmberSpirit, InfernoSpirit |
| 정령 | 물 | TideSpirit, StreamSpirit, TorrentSpirit |
| 정령 | 땅 | DustSpirit, BoulderSpirit, QuakeSpirit |
| 초월 | 동물 | BearGuard, FoxMagician, LionKnight, RhinoBreaker, EagleArcher, RabbitTrickster |
| 초월 | 식물 | MushroomBomber, BloomFairy |

### 자연회복 (초월계 전용)
- 조건 없이(전투 중 포함) 상시 지속 HoT, 초월계 전 유닛 대상.
- 최대 HP를 건드리지 않고 **고정 HP/s** 회복. Lv0~5 = 0/3.0/6.0/9.0/12.0/15.0 (레벨당 +3.0, ×10 스케일 기준).
- 기존 "1초 간격 discrete 틱, 서버 권위" HoT/DoT 인프라(BloomFairy 힐 / InfernoSpirit DoT / MushroomBomber DoT) 재사용.

### 힐량 트랙 (공격력 파생)
- BloomFairy 힐(초월 식물 그룹 공격력 트랙을 따름): 200 → 280(증가폭 +16/Lv).
- TorrentSpirit 아군 힐(= 물 공격력 × 0.5): 100 → 140(물 공격력 200→280의 절반).

### 연구소 운영 규칙
- **건물 업그레이드 없음** — 단일 등급 연구소에서 모든 트랙 Lv1~5(최종치)까지 연구 가능. 스테이지 게이트는 추후 확장(이번 도입 안 함).
- 복수 건설 가능(서로 다른 트랙 병렬 연구), 연구 시간 소요.
- 진행 중 트랙은 UI에서 숨김 → 팀당 트랙 단위 잠금(중복 연구 차단).
- 연구소 파괴 시 진행 중 연구 취소(완료분 영구 유지). **비공개 = 진행 중 연구 UI만 비공개** — 진행 중 연구(트랙·타이머)는 소유 플레이어에게만 표시하되, **완료된 업그레이드 레벨(효과)은 양 클라에 동기화되어 양쪽에 적용**된다(2026-07-23 확정, 기존 "전면 비공개" 재정의). 완료 레벨은 양 클라 브로드캐스트(`NetworkResourceSync` `ReadPermission=Everyone` 선례), 진행 상태는 소유 클라 대상(`BuildFailedClientRpc` 타겟 패턴 선례).
- 서버 권위 우선. AI도 시나리오 일부 수정으로 업그레이드 사용.

### 비용·시간 (스탯 1종 Lv1~5, 기본 그룹)
| 레벨 | 골드 | 연구 시간 |
|------|------|-----------|
| Lv1 | 80 | 15초 |
| Lv2 | 120 | 25초 |
| Lv3 | 180 | 35초 |
| Lv4 | 260 | 50초 |
| Lv5 | 360 | 70초 |
| 합계 | 1,000 | — |

- 종족 비대칭 보정: 효과는 동일, **비용만 배율**. 초월 동물 그룹 ×2.0(6유닛 커버), 자연회복 트랙 ×2.5(초월 전 유닛 커버), 초월 식물 포함 그 외 ×1.0. (인간 탈것 ×0.85 대안은 미채택.)
- 연구소 건물: 건설비 200골드(불변), HP는 ×10 스케일(Human/Spirit 1000·Trans 1500).
- 연구 취소 환불: 진행 중 연구가 파괴로 취소 시 **투입 골드 100% 환불**. 완료 레벨 비용은 환불 대상 아님.

---

## 현재 시스템 분석 (파일·라인 근거)

### 1) 건물 열거형 — Research 값 이미 존재 (신규 추가 불필요)
- `Domain/Building/BuildingType.cs:37` — `Research = 4` 이미 명시 부여되어 있음. 주석은 "업그레이드 연구 건물".
- 열거형 int 값이 이미 확정·명시되어 있어 RPC 직렬화(`NetworkBuildingController`가 `(int)BuildingType`로 전송, 파일 상단 주석 참조) **순서 변경이 필요 없다** → 신규 건물 추가로 인한 인덱스 시프트 위험 없음.
- 결론: 연구소는 **건물 타입 신규 추가 없이** 기존 `Research` 값을 그대로 사용한다.

### 2) 유닛 스탯 정적 조회 — Defense 필드 없음 (신규 추가 필요)
- `Domain/Unit/UnitStats.cs` — 정적 클래스. `Dictionary<UnitType, StatValues>`로 **팀 구분 없이** 타입별 기본 스탯 보관(`_data`, 32~61행). `GameBootstrapper`가 `UnitStatsConfig`(Infrastructure)에서 값을 주입해 `Initialize(dict)`로 채운다.
- `StatValues` 구조체(43~58행) 필드: `MaxHp / AttackPower / AttackRange / DetectRange / MoveSpeed / AttackCooldown / HitFrameTimes / IsHealer`. → **`Defense` 필드 없음.** 방어력 도입 시 이 구조체에 신규 필드 추가 필요(기본 0).
- 조회 API는 `GetMaxHp / GetAttackPower / GetAttackRange / GetDetectRange / GetMoveSpeed / GetAttackCooldown / GetHitFrameTimes / GetIsHealer`(99~172행). 방어력 도입 시 `GetDefense`(가칭) 신설 후보. 단, 전 유닛 기본 방어력이 0이므로 UnitStatsConfig에 값을 굳이 넣지 않고 폴백 0으로 처리 가능(세부는 [5]에서 확정).

### 3) 유닛 인스턴스 데이터 — Defense 필드 없음, AttackPower 읽기전용
- `Domain/Unit/UnitData.cs` — `IDamageable` 구현. `MaxHp`(읽기전용), `Hp`(private set), `AttackPower`(읽기전용 int, 58행), `MoveSpeed`(72행) 등 스냅샷 필드. `TakeDamage(int)`(171행), `Heal(int)`(189행) 보유. → **`Defense` 필드 없음.**
- **중요(=(B) 방식과 직결):** `AttackPower`·`MoveSpeed`는 생성 시 `UnitStats`에서 읽어 **읽기전용 스냅샷**으로 고정된다(148~157행). 따라서 (B) 방식(실시간 배율)은 이 스냅샷을 바꾸는 것이 아니라, **데미지·이동을 실제로 쓰는 사용 지점에서 `기본값 × 팀 배율`을 곱해** 적용해야 한다. 유닛 필드 자체는 건드리지 않으므로 소급 적용이 자동 성립한다.
- `Heal(int)`는 이미 존재(MaxHp 클램프, 죽은 유닛 무동작) → 자연회복 HoT가 그대로 재사용 가능.

### 4) 유닛 스폰 — 스냅샷 지점
- `Application/UseCases/UnitSpawnUseCase.cs:89~95`(싱글 `SpawnUnit`) 및 `220~226`(네트워크 `SpawnUnitWithId`) — 두 경로 모두 `UnitStats.GetMaxHp/GetAttackPower/GetAttackRange/GetDetectRange/GetMoveSpeed`를 읽어 `UnitData`에 스냅샷한다.
- (B) 방식에서는 이 스폰 스냅샷을 **변경할 필요가 없다**(강화는 사용 시점 배율로 처리). 방어력만 UnitData에 신규 필드로 실릴 수 있으나, 방어력 기본 0이므로 스폰 스냅샷은 0으로 고정되고 연구 배율은 사용 지점에서 조회된다(세부는 [5]).

### 5) 전투 데미지 적용 지점 — 무감쇄 직격
- `Application/UseCases/UnitCombatUseCase.cs:1095` — `target.TakeDamage(attacker.AttackPower)`. **현재 방어력 감쇄가 전혀 없는 직격**이다. 이 라인이 단일 타깃 피해 수렴점(`ApplyDamageToVictim`)이다.
- 임의 수치 피해 경로 `ApplyFixedDamageToVictim`(1352행~)도 존재 — 스플래시/특수용. QuakeSpirit 스플래시는 `ApplyQuakeSplash`(1338행)에서 `Mathf.CeilToInt(attacker.AttackPower * _quakeSplashRatio)` 계산 후 `ApplyFixedDamageToVictim`로 적용(1343·1349행).
- **타워→유닛 데미지 경로 (qa Major — 감쇄 대상 포함):** `Application/UseCases/TowerCombatUseCase.cs:200` `ExecuteTowerAttack` → `:206` `target.TakeDamage(damage)`(코드 확인). 방어 타워가 유닛에게 주는 직격이며 **현재 감쇄 없음**. 타워→유닛은 유닛 방어 감쇄 대상이므로 이 경로도 동일 감쇄 헬퍼를 삽입해야 한다.
- DoT 경로(`ApplyBlastDot` / `ApplyInfernoDot`, GameSystemRules_Units 규칙 40~42)는 별도 진입점으로 고정 틱값을 적용 — **방어력 감쇄 미적용 대상**(아래).
- → 방어력 감쇄는 **직격·스플래시·타워→유닛 최종 데미지 지점에 일괄 삽입**하고, **DoT 틱값에는 미적용**한다(2026-07-23 확정 — 기존 "DoT에도 적용"을 뒤집음, DoT 삽입 지점 이슈 소멸). 공격력 배율도 "공격력을 쓰는" 직격·스플래시에만 적용하고 고정 DoT 틱값에는 미적용.

### 6) 팀별 배율 레이어의 기존 선례 — ResourceUseCase
- `Application/UseCases/ResourceUseCase.cs:41` — `Dictionary<TeamId, float> _incomeMultipliers`, `76`행 `SetIncomeMultiplier(team, mult)`. AI 난이도별 채굴소 수입 배율을 **팀별로 곱하는 기존 선례**(규칙 34). 기본값 1.0이라 일반 플레이 무영향(60~65행).
- → 업그레이드 상태(팀별 트랙 레벨)도 **동일 패턴으로 Application 계층**에 신규 UseCase로 두는 것이 자연스럽다. `Dictionary<(TeamId, 그룹, 스탯), int level>` + 조회 API. Application 계층이라 Unity.Netcode 미참조(아키텍처 제약 준수).

### 7) HoT/DoT 공용 틱 인프라 — 자연회복 재사용 기반
- GameSystemRules_Units 규칙 34(HoT/DoT 공용 시간 지속 효과, 서버 권위 diff 틱)·규칙 40(DoT 1초 간격 discrete 틱)·규칙 30(힐 서브시스템 `UnitData.Heal` + `OnEntityHealed` + `NetworkHealthSync` 힐 동기화)이 이미 구현·검증됨(BloomFairy 힐 / MushroomBomber DoT / InfernoSpirit DoT).
- 서버 틱 진입점: 싱글=`GameBootstrapper.Update`(`!IsNetworkMode` 가드), 멀티=`NetworkCombatController`(IsServer 가드). **이중 틱 금지**(규칙 34·40).
- → 자연회복은 이 인프라 위에 "초월계 전 유닛 대상 상시 HoT(고정 HP/s)"로 얹을 수 있다. 최대 HP를 안 건드리고 `Heal`로 클램프.
- **⚠️ 자연회복 ↔ 힐 충돌 지점 (qa Critical):** 현재 공용 시스템은 `UnitCombatUseCase.AddOrRefreshTimedEffect`가 효과를 **`(TargetId, Kind)` 키**로 관리하며(코드 확인 — `e.TargetId == target.Id && e.Kind == kind`이면 리셋/덮어쓰기), 힐은 `TimedEffectKind.Heal` 버킷 **하나뿐**이다(BloomFairy 힐이 이 버킷 사용). 자연회복을 같은 Heal 버킷에 넣으면 같은 대상에서 BloomFairy 힐과 **서로 덮어써(갱신=리셋) 한쪽이 소멸**한다. → 자연회복은 Heal 버킷과 **분리된 독립 채널**(신규 `TimedEffectKind` 또는 별도 자료구조)로 구현해야 상호 간섭이 없다(Plan 항목 7).

### 8) 연구소 UI·네트워크의 참조 패턴
- 생산 패널: `GameSystemRules_UI.md` 생산 패널 UI 규칙(팝업 열기/닫기, 큐, 골드 차감 시점, 토스트, 비용 텍스트 색상 규칙 7·14). 연구 패널은 이 패턴을 참고.
- 서버 권위 건물 RPC: `NetworkBuildingController`(Infrastructure, NetworkBehaviour) 패턴 — 건물 관련 RPC의 기존 소유처. 연구 요청도 이 계층에 ServerRpc로 둔다.
- 환불 선례: `GameSystemRules_Buildings.md` 규칙 4(철거 50% 환불)·규칙 5(생산 큐 골드 차감분 전액 환불). 연구 취소 100% 환불은 규칙 5의 "차감분 전액 환불" 원리와 동일.

---

### 9) 전투 스탯 config 에셋 — 현재 1배 스케일 (×10 개편 대상)
전투 수치는 ScriptableObject `.asset` 3개에 보관되며, GameBootstrapper가 게임 시작 시 읽어 Domain/UseCase에 주입한다. 현재는 **1배(구 스케일)** 값이 들어 있어 ×10 개편 시 이 에셋들을 재설정해야 한다.
- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`(스키마 `UnitStatsConfig.cs`/`UnitStatEntry`) — `maxHp`·`attackPower`가 ×10 대상. `attackRange`/`detectRange`/`moveSpeed`/`attackCooldown`/`productionTime`/`goldCost`/`populationCost`는 불변. 방어력 필드는 **현재 없음**(항목 1에서 스키마 추가, 전 유닛 0).
- `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`(스키마 `BuildingStatsConfig.cs`/`BuildingTypeEntry`) — 종족별 HP 3필드(`humanMaxHp`/`spiritMaxHp`/`transcendenceMaxHp`)와 타워 공격력 3필드(`human/spirit/transcendenceAttackPower`)가 ×10 대상. `goldCost`·`upgradeCost`·`attackCooldown`·`attackRange`는 불변.
- `Assets/_Project/Resources/Config/SpecialAttackConfig.asset`(스키마 `SpecialAttackConfig.cs`) — DoT 틱값 `_blastDotPerSecond`(2)·`_infernoDotPerSecond`(5)와 힐값 `_bloomHealAmount`(20)·`_waveHeal`(10)이 ×10 대상. 반경/각도/시간/비율 필드(`_quakeSplashRatio` 0.5 등)는 불변.
- MistShrine(HealShrine) 힐은 현재 **미구현**(전용 config 필드도 힐 로직도 없음, 열거형 `HealShrine=6`만 존재) → 기존 에셋 ×10 대상 아님. 설계값 10 HP/s(범위 3)는 향후 HealShrine 힐 기능 구현 시 신규 config 필드로 추가한다.
- **아키텍처 교훈(`.claude/MEMORY.md`)**: Inspector(ScriptableObject) 값이 코드 기본값보다 우선하므로, ×10은 코드 폴백이 아니라 **`.asset` 파일 자체를 재설정**해야 반영된다. 대량 편집이라 WORKFLOW [5-2] Inspector 에디터 스크립트가 유력.

## 영향 범위 분석

| 레이어 | 영향 | 성격 |
|--------|------|------|
| 데이터(config) | `UnitStatsConfig`/`BuildingStatsConfig`/`SpecialAttackConfig` `.asset` 값 ×10 재설정(HP·공격력·타워·DoT·힐), 비율/사거리/골드/시간 불변 | 데이터 재설정(1배→×10) |
| Domain | `UnitStats.StatValues`에 Defense 필드, 조회 API, `UnitType→UpgradeGroup` 매핑 헬퍼 | 신규 추가 |
| Application | 신규 `UnitUpgradeUseCase`(가칭, 팀별 트랙 레벨 보관·배율/방어/회복 조회), 전투 데미지 공식에 감쇄식·공격 배율 삽입, 이동 속도 사용 지점 배율, 자연회복 HoT 틱 | 신규 + 기존 1지점 수정 |
| Infrastructure | 연구 요청 ServerRpc(NetworkBuildingController 패턴), 팀별 트랙 레벨·진행 타이머 서버 권위 동기화, UnitStatsConfig에 방어력(선택) | 신규 추가 |
| Presentation | 연구 패널 UI(생산 패널 패턴), 진행 중 트랙 숨김, 골드/타이머 표시 | 신규 추가 |
| AI | 각 종족 시나리오 Phase 3~4에 연구 착수 스텝(방향만) | 시나리오 소폭 수정 |

### 전투 전반에 대한 영향 (주의)
- 데미지 공식 변경은 **모든 전투**에 영향을 준다. 다만 방어력 기본값이 전 유닛 0이고 `방어력/(방어력+K)` 항이 0이 되어 감쇄율 0% → `Max(1, Round(공격력 × 1)) = 공격력`이므로 **연구 전에는 기존과 완전히 동일**하다(하위호환). 회귀 위험은 이 하위호환으로 완화되나 실기 검증 필요.

---

## 신규 추가 vs 기존 변경 구분 (기존 로직 제거 규칙 대비)

이 작업은 **대부분 신규 추가**이며, 기존 로직을 제거하는 부분은 없다.

- **유일한 기존 로직 변경**: `UnitCombatUseCase`의 최종 데미지 적용 공식 — `TakeDamage(공격력)` → `TakeDamage(감쇄식 적용값)`. 이는 **제거가 아니라 수정**이며, 방어력 0일 때 결과가 기존과 동일(하위호환)하다. 따라서 "비활성화 우선" 대상이 아니라 안전한 in-place 수정이다. (Plan.md 최상단에 이 사실 명시 예정.)
- 그 외(방어력 필드, 업그레이드 UseCase, 그룹 매핑, 자연회복 HoT, 연구 패널·RPC, AI 스텝)는 전부 신규 코드 추가로 기존 동작을 건드리지 않는다.

---

## 확인 필요·미해결 사항 (구현 전 결정 권장)

1. **방어력 조회 지점**: (B) 방식에서 방어력은 "피격 대상 팀의 연구 레벨"로 결정된다. 데미지 계산 시 대상(`UnitData`)의 팀·그룹으로 방어 레벨을 조회 → 감쇄 적용. 이 조회를 `UnitUpgradeUseCase`가 담당하는지, 감쇄 계산 헬퍼를 어디에 둘지(Domain 순수 함수 권장)는 [5]에서 확정.
2. **이동 속도 배율 스레딩**: 이동 코루틴/속도 사용 지점(A* 타일 이동 + 전투 이동, 규칙 5 동일 스탯)에서 `기본값 × 팀 배율`을 어떻게 레이어 위반 없이 전달할지 — 매 이동 계산 시 조회 vs 이동 시작 시 조회. 성능·소급성 절충 [5]에서 확정.
3. **하드캡 60~65% 적용 위치**: 감쇄율 상한을 공식 안에 clamp로 넣을지, 방어력 상한으로 넣을지. 현재 Lv5(방어 20)는 감쇄 50%로 캡 미도달이나 미래 확장 안전장치로 코드 포함 필요.
4. **자연회복과 HoT 시스템의 상시성**: 기존 HoT는 "효과 부여 → 지속 → 종료" 레코드 방식(규칙 34). 자연회복은 "조건 없이 상시"이므로 레코드 만료가 없는 상시 틱으로 둘지, 초월 유닛 스폰 시 무한 HoT를 부여할지 [5]에서 구조 확정. 풀피 유닛에는 회복량 0(클램프)로 자연 처리.
5. **비대칭 비용 배율의 데이터 위치**: 그룹별 비용 배율(동물 ×2.0, 자연회복 ×2.5)을 상수 테이블/Config 중 어디에 둘지 [5]에서 확정.
6. **탈것 ×0.85**: game-design-lead가 "선택(대안)"으로만 표기 요청 → 이번엔 미적용을 기본으로 하고 Plan에 대안으로만 남긴다.

---

## 참고 문서
- 절대 규칙: `CLAUDE.md`
- 작업 사이클: `Assets/_Project/Docs/WORKFLOW.md`
- 공용 컨텍스트: `.claude/MEMORY.md`, `AGENTS.md`
- 게임 규칙: `GameSystemRules.md`(인덱스), `GameSystemRules_Units.md`(전투·데미지·HoT/DoT 규칙 34·40·42), `GameSystemRules_Buildings.md`(서버권위·환불 규칙 4·5), `GameSystemRules_UI.md`(생산 패널), `GameSystemRules_AI.md`(빌드오더)
