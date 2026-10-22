# Game System Rules — 연구소 기반 유닛 강화(업그레이드) 시스템 + 전투 스탯 ×10 스케일

- **목적·답하는 질문:** 연구소의 팀 단위 유닛 강화와 전투 수치 스케일 계약을 판단한다.
- **포함:** 공/방/속·초월 자연회복, 연구 비용·소급 적용·서버 권위·연구 패널·×10 의미
- **제외·담당 문서:** 현재 스탯 값은 [StatsReference](../StatsReference.md), 피해 감쇄 소비는 [Units](GameSystemRules_Units.md), 현재 상태·후속은 [PROJECT_STATUS](../PROJECT_STATUS.md)·[ROADMAP](../ROADMAP.md)
- **권위원본·주요 참조:** 연구 시스템 규칙·예외 원본; Config 직렬화 값은 코드 기본값보다 우선
- **갱신 조건·방법:** 강화 계약 승인 변경 때 해당 규칙을 갱신한다. 과거 적용·검증·별도 보류를 전체 완료로 합치지 않고 현재 상태와 상세 근거를 연결한다.

연구소 강화·연구 패널과 전투 스탯 ×10의 기능 계약은 아래 규칙을 따른다. 현재 구현·검증 범위는 [PROJECT_STATUS](../PROJECT_STATUS.md), 후속은 [ROADMAP](../ROADMAP.md), 과거 완료·보류 묶음은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-01)다. 개별 PASS를 다른 미검증 범위로 확대하지 않는다.

Config 직렬화 값이 코드 fallback보다 우선한다. ×10은 저장된 기본값이므로 폐지된 일회성 셋업 도구를 실행하지 않는다. 현재 개별 수치는 [StatsReference](../StatsReference.md)를 따른다.

---

## 이 문서가 무엇이고 왜 만들었는가 (자연어 설명)

플레이어는 연구소를 지어 골드·시간을 들여 자기 팀 유닛의 공격력·방어력·이동속도를 강화한다. 초월 종족은 전투 중에도 적용되는 자연회복 트랙을 추가로 가진다. 도입 전 상태는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-02)다.

핵심 결정은 강화 효과가 **이미 전장에 나와 있는 유닛에게도 즉시 소급 적용**된다는 점이다. 이를 위해 유닛마다 스탯을 재계산해 네트워크로 재동기화하는 무거운 방식 대신, **"기본 스탯 × 팀 배율"을 데미지·이동을 실제로 쓰는 순간에 곱하는 방식(이하 (B) 방식)** 을 쓴다. 서버는 팀별 연구 레벨만 들고 있으면 된다.

전투 수치 ×10은 강화 증가폭을 정수 그리드로 정리하는 스케일 계약이다. HP·공격력을 같은 배율로 키워 스케일 자체로 처치 타수·상성·매치업을 바꾸지 않는다. 현재 값은 [StatsReference](../StatsReference.md), 도입 당시 old/new 비교는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-03)와 기존 BalanceReview 기록을 참조한다.

---

## 구현 상태 (2026-07-31 — 기능·멀티플레이 실기 PASS)

### ✅ 구현 완료 (멀티플레이 실기 검증)
연구 강화·×10 스케일의 개별 완료/실기 범위는 [PROJECT_STATUS](../PROJECT_STATUS.md), 당시 구성·검증 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-upgrade-01)를 따른다. 과거 PASS를 싱글 자연회복·AI 연구 사용 등 미검증 범위로 확대하지 않는다.

### ⏳ 후속 / 보류 (별도 작업, 과대 표기 금지)
현재 후속·우선순위는 [ROADMAP](../ROADMAP.md)에서 관리한다. 자연회복과 MistShrine 물안개·BloomFairy 힐은 서로 독립 채널로 중첩 적용한다. MistShrine의 재설계 이전 `1→10/s`는 현행 수치 확정이 아니며 규칙1의 보류와 건물 규칙 원본을 따른다. UI 다듬기·헤더 아이콘·AI 연구·싱글 자연회복·MistShrine 멀티/아트/밸런싱의 과거 상태를 완료로 올리지 않는다.

---

## 유닛 강화 시스템 규칙

**규칙 1. 전투 스탯 ×10 스케일 (강화의 전제)**

×10 대상은 유닛 HP·공격력, 건물/타워 HP·타워 공격력, 고정 DoT와 힐량이다. 현재 개별 값은 [StatsReference](../StatsReference.md)를 참조한다. MistShrine은 물안개 힐로 재설계되어 구체 회복량·지속·쿨다운·반경의 밸런싱 확정이 보류 중이다([건물 규칙16](GameSystemRules_Buildings.md)). HP·힐량의 동일 스케일 원리는 유지하며 재설계 이전 값을 현행 확정 수치로 재사용하지 않는다. 이전 수치 비교·보류 경위는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-04)다.

**×10 하지 않는 것(불변):** 공격/감지 사거리(타일), 이동속도(칸/초), 공격 쿨다운·생산 시간·연구 시간, 모든 골드 비용, 채굴 수입(10골드/초), 스플래시/특수 **비율**(QuakeSpirit 50%, Tank/CannonCart 건물 2배, 부채꼴/원형 반경, TorrentSpirit 힐=공격력×0.5 등).

- HP·공격력을 같은 배율로 키웠으므로 TTK 불변 → 상대 밸런스·컨셉·매치업 불변.
- 개별 ×10 값은 `StatsReference.md`가 권위 소스(구현 완료 — config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)). 이 규칙은 원리·불변 대상만 정의한다.

**규칙 2. 강화 스탯과 트랙 구조**

- 강화 스탯 3종: **공격력 / 방어력(신규) / 이동속도.** 최대 HP는 강화 대상 아님.
- 초월계 전용 4번째 트랙: **자연회복**(초월 공용 1트랙).
- 종족별 트랙 구조:

| 종족 | 그룹 × 스탯 | 트랙 수 |
|------|------------|---------|
| 인간계 | 근접 / 원거리 / 탈것 × 공·방·속 | 9 |
| 정령계 | 불 / 물 / 땅 × 공·방·속 | 9 |
| 초월계 | 동물 / 식물 × 공·방·속 (6) + 자연회복(초월 공용 1) | 7 |

- 각 트랙은 Lv0(연구 전 기본)~Lv5. Lv0 = `StatsReference.md`의 ×10 기본값.

**규칙 3. 유닛 → 그룹 매핑**

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

- 순수 Domain 정적 헬퍼로 매핑한다(초월계 여부 판정 = 자연회복 대상 포함). 기존 `BuildingTypeHelper`와 동일하게 순수 C#.

**규칙 4. (B) 팀 배율 실시간 적용 (소급 강화)**

- 효과 적용 = **기본값 × 팀 연구 배율을 데미지·이동을 실제로 쓰는 사용 지점에서 곱함.** 유닛 스냅샷 필드(`AttackPower`/`MoveSpeed`)는 변경하지 않는다 → 이미 생산된 유닛도 연구 완료 즉시 소급 강화, 재계산·재동기화 로직 불필요.
- 팀별 트랙 레벨은 Application 계층에 보관한다(신규 UseCase). 기존 선례: `ResourceUseCase._incomeMultipliers`/`SetIncomeMultiplier`(팀별 배율 레이어). Application은 Unity.Netcode 미참조(아키텍처 제약) — 네트워크 동기화는 Infrastructure가 담당(의존성 역전, 인터페이스는 Application 선언).
- 공격력 배율: 레벨당 +8% 가산, Lv1~5, Lv5 = ×1.40. 이동속도 배율: `StatsReference.md` §이동속도 트랙(×1.000~×1.320).

**규칙 5. 방어력 데미지 감쇄 공식 (신규)**

```
데미지 = Max(1, Round(공격력 × (1 − 방어력 / (방어력 + 120))))
```

- 방어력 트랙: Lv0~5 = **0 / 8 / 16 / 24 / 32 / 40**, 감쇄 상수 **K = 120**.
- **최소 데미지1 보장(floor) 필수.** 감쇄율 하드캡은65%(`DamageCalculator.MaxMitigationRate=0.65f`)이며 미래 확장의 무한 감쇄를 막는다(현 Lv5는25%로 미도달). 이전 범위 표기는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-08)다.
- **방어력 감쇄는 직격 · 스플래시에만 적용하고, DoT 틱값에는 미적용**한다(2026-07-23 확정 — 기존 "DoT에도 적용" 결정을 뒤집음). 직격·스플래시의 모든 최종 데미지 지점에 일괄 적용하며, **타워→유닛 데미지도 포함**한다(방어 타워가 유닛에 주는 피해도 유닛 방어 감쇄 대상, `GameSystemRules_Units.md` 규칙 44 및 `GameSystemRules_Buildings.md` **방어 타워 시스템** 규칙 9와 상호 참조).
- **하위호환:** Lv0(방어 0)이면 감쇄 0% → `Max(1, Round(공격력×1)) = 공격력`으로 기존과 완전히 동일.
- **건물 방어력 = 0 고정, 트랙 없음(향후 확장 보류):** 건물도 `BuildingData`에 방어력 필드(기본 0)를 두어 감쇄 공식이 유닛·건물에 **통일 적용**되게 한다(별도 `target is UnitData` 분기 불필요). 단 **건물 방어력 업그레이드 트랙은 없어** 건물 방어력은 항상 0(실질 감쇄 0%)이다 — 건물 방어 트랙은 이번 도입 범위 밖(향후 확장 보류).
- 방어력은 **피격 대상 팀의 연구 레벨**로 결정된다(데미지 계산 시 대상 팀·그룹으로 방어 레벨 조회). 감쇄 계산은 순수 함수 헬퍼(Domain 권장)로 두어 모든 지점이 동일 공식을 쓴다.

**규칙 6. 공격력 연구의 적용 범위 (파생 스케일)**

- 공격력 연구 배율은 **공격력을 직접 쓰는 피해(직격·스플래시·파도)에만** 적용한다.
- **고정 DoT 틱값(MushroomBomber 20/s, InfernoSpirit 50/s)에는 반영하지 않는다**(강화와 무관하게 고정). **방어력 감쇄(규칙 5)도 DoT 틱값에는 미적용**이다(2026-07-23 확정 — DoT는 공격력 연구·방어력 연구 어느 쪽에도 영향받지 않는 고정 틱값). 방어력 감쇄는 직격·스플래시에만 적용한다.
- **공격력 파생 스케일:** QuakeSpirit 스플래시(= 공격력 × 0.5), TorrentSpirit 파도(= 공격력)는 공격력을 쓰므로 공격력 연구에 따라 함께 커진다.
- **힐량:** BloomFairy 힐(초월 식물 그룹 공격력 트랙을 따름, 200→280), TorrentSpirit 아군 힐(= 물 공격력 × 0.5, 100→140). `StatsReference.md` §힐량 트랙.
  - 힐 연구 배율은 `AttackPower` 스냅샷이 아니라 해당 그룹의 공격력 트랙 레벨을 조회한다. BloomFairy는 공격 없는 순수 힐러이므로 초월 식물 트랙, TorrentSpirit는 물 트랙을 사용한다. 도입 전 고정 상수·신규 로직 지침은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-05)다.

**규칙 7. 자연회복 (초월계 전용 상시 HoT)**

- 초월계 전 유닛 대상 **조건 없는 상시 HoT**(전투 중 포함). 최대 HP 미변경, `UnitData.Heal`로 MaxHp 클램프(풀피 유닛은 회복량 0으로 자연 무동작).
- 레벨별 고정 HP/s: Lv0~5 = **0 / 3.0 / 6.0 / 9.0 / 12.0 / 15.0**(레벨당 +3.0). 자연회복 트랙 레벨 0이면 무동작.
- 기존 "1초 간격 discrete 틱, 서버 권위" HoT/DoT 인프라(`GameSystemRules_Units.md` 규칙 30·34·40 — BloomFairy 힐 / MushroomBomber·InfernoSpirit DoT) 재사용. 서버 틱 진입점은 싱글=`GameBootstrapper.Update`(`!IsNetworkMode`)·멀티=`NetworkCombatController`(IsServer), **이중 틱 금지**.
- **힐과 별개 채널(상호 간섭 금지):** 자연회복과 BloomFairy 힐은 서로 독립적으로 동시 적용한다. 자연회복을 같은 `(TargetId, Kind)` Heal 버킷에 넣어 한쪽이 덮어써지지 않도록 채널을 분리한다. MistShrine 물안개도 별도 채널이다. 도입 전 자료구조 선택지는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-06)다.

**규칙 8. 연구소 운영**

- **복수 건설 가능** — 서로 다른 트랙을 병렬 연구할 수 있다. 각 연구는 연구 시간이 소요된다.
- **건물 업그레이드 없음** — 단일 등급 연구소에서 모든 트랙 Lv1~5(최종치)까지 연구 가능. 스테이지 게이트는 추후 확장(이번 도입 안 함).
- **진행 중 트랙 숨김 = 팀당 트랙 단위 잠금** — 어떤 트랙을 연구 중이면 그 트랙은 팀 전체 UI에서 숨겨져 중복 연구가 차단된다(복수 연구소가 같은 트랙을 동시에 못 돌린다).
- **연구소 파괴 시 진행 중 연구 취소** — 진행 중이던 연구는 취소되고 **투입 골드 100% 환불**(완료된 레벨 비용은 환불 대상 아님, 완료 레벨은 영구 유지). 환불 원리는 `GameSystemRules_Buildings.md` 건물 철거 시스템 규칙 5(생산 큐 차감분 전액 환불)와 동일(철거 50% 환불인 규칙 4와 구분).
- **비공개 = 진행 중 연구 UI만 비공개** — 상대가 **현재 무엇을 연구 중인지(어떤 트랙·타이머)** 는 소유 플레이어에게만 표시하고 상대에게는 숨긴다. **단, 완료된 업그레이드 레벨(효과)은 양 클라이언트에 모두 동기화되어 양쪽에 적용**된다 — 상대의 강화된 유닛이 내 화면에서도 올바른 데미지·이동을 내야 하기 때문이다. 즉 완료 효과는 양쪽 공개·적용, 진행 상태만 비공개다. 기존 "상대 팀 연구 내용 전면 비공개"를 이 정의로 재정의(2026-07-23).
- **서버 권위** — 골드 검증·차감·트랙 잠금·타이머·완료 레벨 반영은 서버가 판정한다.

**규칙 9. 서버 권위 네트워크 처리**

- 연구 요청은 연구소 건물을 대상으로 트랙을 지정하는 **ServerRpc**로 전달한다(`NetworkBuildingController` 패턴, Infrastructure). 서버가 골드 검증·차감·트랙 잠금·타이머 시작 → 완료 시 팀 레벨 반영.
- **완료 레벨 = 양 클라 브로드캐스트 / 진행 상태 = 소유 클라 한정** (규칙 8 비공개 정의 구현): 연구 **완료** 시 서버는 해당 팀의 트랙 레벨을 **양 클라이언트 모두에 전파**한다(완료 효과가 양쪽에 적용돼야 하므로 — `NetworkResourceSync`가 `ReadPermission=Everyone`으로 자원을 양쪽에 전파하는 선례와 동일 계열). 반면 **진행 중 연구 상태(트랙·타이머)** 는 소유 플레이어에게만 전송한다(타겟 `ClientRpcParams` 지정 — `NetworkBuildingController.BuildFailedClientRpc`가 요청 클라에게만 보내는 패턴 계열).
- 아키텍처 제약: NetworkBehaviour는 Infrastructure에만, RPC 메서드명은 `ServerRpc`/`ClientRpc` 접미사 필수.
- `BuildingType.Research = 4`는 이미 존재(BuildingType.cs) → 연구소는 건물 타입 신규 추가 없이 기존 값을 재사용한다(RPC int 순서 변경 없음).

**규칙 10. 연구 비용·시간**

**표준 트랙 (스탯 1종 Lv1~5, 기본 배율 ×1.0):**

| 레벨 | 골드 | 연구 시간 |
|------|:-:|:-:|
| Lv1 | 80 | 15초 |
| Lv2 | 120 | 25초 |
| Lv3 | 180 | 35초 |
| Lv4 | 260 | 50초 |
| Lv5 | 360 | 70초 |
| **합계** | **1,000** | — |

**그룹 비용 배율 (효과는 동일, 비용만 배율):**

| 그룹 | 배율 | 트랙 합계 골드 | 비고 |
|------|:-:|:-:|------|
| 초월 동물 | ×2.0 | 2,000 | 6유닛 커버 |
| 자연회복(초월 공용) | ×2.5 | 2,500 | 초월 전 유닛 커버 |
| 초월 식물 포함 그 외 전부 | ×1.0 | 1,000 | (초월 식물 그룹 비용 배율 = ×1.0로 확정) |

- 연구 시간은 배율과 무관하게 표준(15/25/35/50/70초) 유지.
- **연구소 건설비 200골드(불변).** 스테이지 업그레이드 없음(단일 등급에서 전 트랙 Lv1~5).
- 그룹별 비용 배율 데이터 위치(상수 테이블/Config)는 구현 단계에서 확정.

**규칙 11. AI 사용 (방향만 — 구현 예정, 별도 task)**

- AI도 각 종족 시나리오 Phase 3~4에 연구소 착수 + 우선 트랙 스텝을 갖는다(방향만): 예 — Human A = 근접 공격력, Spirit A = 불 공격력, Trans A = 동물 공격력, Trans B = 자연회복.
- actionType 신설 여부·정확한 배치·세부 delaySeconds 개편은 **별도 task로 분리**(game-programmer + game-design-lead 협의). 이 문서는 방향만 못박는다.
- 근거 문서: `GameSystemRules_AI.md`(빌드오더 Phase 1~4 구조) 및 종족별 시나리오 문서.

**규칙 12. 확정 쟁점 (BalanceReview §F — 3건 전부 확정, 2026-07-23)**

초기 미확정이던 밸런스 쟁점 3건은 사용자 결정으로 **전부 확정**됐다.

- **F-1 (확정: 유지)** — Lv0 Tank(공격 300)가 Lv0 FoxMagician/RabbitTrickster(HP 200)를 무업그레이드로 원샷하는 관계는 ×10 스케일 전에도 있던 코스트 기반 상성(TTK 불변)이므로 **현 관계 유지**. Fox/Rabbit HP는 변경하지 않는다.
- **F-2 (확정: 8~40 / K=120)** — 방어력 트랙 표기는 **Lv0~5 = 0/8/16/24/32/40, 감쇄 상수 K=120**으로 확정(규칙 5). 대안(80~400 / K=1200)은 미채택 — 감쇄율은 두 표기가 수학적으로 동일하다.
- **F-3 (확정: ×1.0)** — 초월 식물 그룹 비용 배율 = **×1.0(합 1,000)** 확정(규칙 10). 대안(×0.7 / 합 700)은 미채택.

**규칙 13. 연구 패널 UI 최종 설계 (구현 완료 — 매트릭스 · BuildingPanelBase · 2-레이어)**

연구소 클릭 시 뜨는 연구 패널의 최종 구현 설계다(초기 계획의 "생산 패널 패턴"에서 **매트릭스 + 공통 건물 패널 프레임**으로 확정). 코드: `Presentation/UI/ResearchPanelUI.cs`(로직 코어·레이어 오케스트레이션) + `ResearchMatrixView.cs`·`ResearchCellView.cs`(매트릭스) + `ResearchProgressView.cs`(진행 게이지).

- **공통 프레임 = `ResearchPanelUI : BuildingPanelBase`** — 헤더 제목 + 닫기[X]는 비생산 건물 패널·생산 패널의 공통 외곽에 맞추되, 연구소의 **철거 버튼(건설비 환불)은 패널 내부 하단 예약 영역에 배치**하고 다른 건물 패널은 기존 slot6을 유지한다(기존 독립 MonoBehaviour → BuildingPanelBase 전환). 철거 시 진행 중 연구도 취소·환불(GameBootstrapper `OnBuildingDied`→연구소 파괴 구독이 담당, 규칙 8·9).
- **본문 2-레이어 (연구소 단위 전환):** 공통 프레임은 고정, 본문만 두 레이어로 전환한다.
  - **(a) 매트릭스 레이어** — 행 = 현재 종족의 강화 그룹, 열 = 공격력/방어력/이동속도. **인간·정령 = 3×3**(근접·원거리·탈것 / 불·물·땅), **초월 = 2×3(동물·식물) + 자연회복 전체폭 버튼 1개**. 각 셀 = 현재 레벨(Lv X/5 + 눈금) + 다음 레벨 비용. 연구 유휴 상태의 연구소에서 표시.
  - **(b) 진행 게이지 레이어** — 해당 연구소가 연구 중일 때 표시. 트랙 이름 + `Lv X→X+1` + 진행 게이지 + 남은 시간 + [취소](연구만 취소·투입 골드 환불, 건물 유지).
  - 전환은 `Open(building)` 시점 + 상태 변화 시 `UpdateLayerVisibility()`가 결정한다. **연구소 단위** — 연구 중인 연구소는 진행 레이어, 유휴 연구소는 매트릭스 레이어를 본다(복수 연구소 각각 독립).
- **진행 중 트랙 잠금(팀 단위 중복 방지)** — 팀이 어떤 트랙을 연구 중이면 그 트랙 셀은 잠긴다(규칙 8 — 복수 연구소가 같은 트랙 동시 연구 불가).
- **닫기 규칙** — 배경 탭 시 닫힘(Popup 공통 규칙 8·9, `GameSystemRules_UI.md`).
- **라벨 표기** — 현재는 아이콘 대신 텍스트 라벨 사용. **매트릭스 헤더 아이콘(공/방/속·그룹)과 전체 UI 레이아웃 비주얼 다듬기는 후속 보류**(에디터 스크립트 자동생성 골격 상태 — "구현 상태" 섹션 참조).
- **buildingId ↔ 진행 연구 매핑** — 싱글/호스트는 `UnitUpgradeUseCase.TryGetActiveResearchByBuilding(buildingId)`로 조회, 멀티 순수 클라는 서버가 착수 확정 시 내려준 buildingId를 로컬 진행에 귀속해 판정.

---

## 참고 문서

- 스케일 도입 당시 old/new 비교 기록: `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md` (현재 수치는 StatsReference와 실제 Config를 참조). 이전 권위 표기는 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-upgrade-07)다.
- 개별 스탯 값(×10, 구현 완료): `StatsReference.md`
- 조사/계획: `_Tasks/2026-07-22/10_08_unit-upgrade-system/Research.md`, `Plan.md`(하단 "완료 결과" 절에 실제 구현 대비 정정 기록)
- 전투·데미지·HoT/DoT 규칙: `GameSystemRules_Units.md`(규칙 5·16·18·30·34·40·42·44)
- 서버 권위·환불: `GameSystemRules_Buildings.md` 건물 철거 시스템 규칙 4·5 / 방어 타워 시스템 규칙 9
- MistShrine 물안개 힐(자연회복과 별개 효과·중첩 적용, **2026-08-12 구현 완료 / 멀티 미검증**): `GameSystemRules_Buildings.md` **MistShrine 물안개 힐 시스템**
- 연구 패널 UI: 규칙 13(`ResearchPanelUI : BuildingPanelBase` + 매트릭스/진행 2-레이어). 공통 팝업/닫기 규칙은 `GameSystemRules_UI.md`
- AI 빌드오더: `GameSystemRules_AI.md` + 종족별 시나리오 문서
