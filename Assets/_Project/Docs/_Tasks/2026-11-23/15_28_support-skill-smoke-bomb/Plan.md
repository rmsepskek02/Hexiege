# Plan — 지원 스킬 「연막탄」 (인간 FlightFacility 슬롯 4)

## 이 문서는 무엇인가

이 계획대로 하면 다음처럼 된다.

- 인간 스킬 건물(FlightFacility)의 **4번 칸 「연막탄」** 을 누르면 다른 지점 스킬과 같은 **원형 조준**에 들어가고, 손을 떼면 그 자리에 연막이 **6초** 동안 남는다.
- 연막 안에 서 있는 **우리 편 유닛**(건물은 아님)은 적의 **원거리 단일 공격**을 **50%** 확률로 피한다. 연막에 나중에 들어온 유닛도 피하고, 나가면 바로 효과가 풀린다. 연막 안에 있는 **적 유닛은 아무 혜택이 없다.**
- 「원거리 단일 공격」은 날아가는 투사체를 쏘는 유닛 6종(Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher), 총잡이 3종(Pistoleer · Assault · Sniper), 그리고 **방어탑 3종 모두**(인간 대포탑 · 정령 RuneSpire · 초월 VineTower)다. 포자를 범위로 터뜨리는 MushroomBomber는 「단일」이 아니라서 빠진다.
- 피하면 **피해도, 화상도, 피해 숫자도 없다**(「MISS」 글자도 띄우지 않는다). 날아가는 투사체는 **쏘는 순간** 서버가 피할지 정하고 빗나갈 자리(대상 옆 · 조금 뒤)를 정해, 투사체가 처음부터 그 자리로 날아가 땅에 떨어진다. 번개 탑도 대상 옆으로 빗나간 번개를 그린다. 총잡이는 원래 날아가는 그림이 없어서 숫자만 안 뜬다.
- 연막 VFX 프리팹은 아직 없다(VFX 제작은 Unity AI 크레딧 소진으로 멈춰 있다). 칸이 비어 있어도 회피는 동작한다.

숫자(회피 50% · 반경 2 · 지속 6초 · 쿨다운 15)는 모두 **시험용 임시값**이며 밸런싱 단계에서 정한다. 빗나감 정보를 Client에 보내려고 알림 모양이 바뀌므로 접속 호환 번호(스키마)가 **11 → 12**가 된다 — 멀티 테스트는 에디터 · 기기 **두 빌드를 모두 새로 설치**해야 한다.

이 작업은 지금까지의 지원 스킬 중 가장 위험이 크다(상위 기획이 「2차 맨 끝」에 둔 이유 — 투사체 전 경로 · 접속 호환에 닿는다). 그래서 **다섯 단계로 나눠** 한 단계씩 확인하며 간다(4절).

현재 코드 상태와 근거(파일 · 행)는 [Research.md](Research.md)에 있다.

> **현재 상태 (2026-11-23): S-1 · S-2 구현(`9e096e4`) · 1차 테스트(멀티 로그 — 메인 세션 판정) 완료, 사용자 지시 「문서갱신하고 3단계 진행해」로 S-3 착수.** 결과 · 미관측 · 계획과 달라진 점은 [12절](#12-s1-s2-result). 회귀 스위트는 **미보고**(PASS 아님). *(2026-11-23 갱신 — 직전 줄은 아래 그대로)* **현재 상태 (2026-11-23): 2판 — 사용자 승인 대기**(WORKFLOW [4]). 사용자 결정은 1-1절, 메인 세션 기본안(사용자에게 보고 예정 — 확정 아님)은 1-2절, 조사 설계안은 2절(확정 아님), 열린 항목은 3절. *(2026-11-23 2판 — O-4 · O-2 사용자 결정(U-8 · U-9), O-3 · O-5 메인 기본안(M-11 · M-12) 반영: 연막탄은 S-1부터 켜고, 스키마 12는 S-3에서 올린다. 직전 「첫 판 — 사용자 승인 대기」)*

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) 1-1 인간 4, 2절 연막탄 행(133행), 2-1 V7, 3-1 인간 4(210행), 3-2 「2차 끝 회피 판정(E)」(293행), 4절 17 · 18(349~350행), 5절 O-7(378행), 6절 R-11(451행), 위험 「연막탄 RPC · 스키마」(583행). **직전 Task:** [메테오 Task](../../2026-11-18/03_34_support-skill-meteor/Plan.md#14-final-result) · [공습 Task](../../2026-11-17/09_12_support-skill-air-strike/Plan.md#16-final-result).

**표기:** `[사용자 결정]` = 메인 세션이 정리해 전달한 2026-11-23 사용자 결정 · `[메인 기본안]` = 메인 세션이 질문 없이 조사 추천안을 채택한 것(**사용자에게 보고 예정 — 확정 아님**) · `[설계안 — 조사]` = game-programmer 조사 권장(확정 아님) · `[제안 — 문서 담당]` = document-manager가 정리 · 덧붙인 것(결정 아님, 구현 때 프로그래머가 확인 · 보고). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Suite/` = `Assets/Editor/Tools/Combat/`. 행 번호는 HEAD `43dbf22`.

> **기존 로직 제거 · 바뀌는 것 — WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 최상단에 둔다.**
>
> **코드를 지우는 항목은 없다.** 바뀌는 줄은 지우지 않고 **주석 처리로 비활성화**한 뒤, 사용자 테스트 통과 뒤 최종 삭제 여부를 묻는다(지원 스킬 Task 선례).
>
> | 바뀌는 것 | 방식 | 안전 근거 |
> |---|---|---|
> | `UnitCombatUseCase.ApplyAttackDamageCore`(1196~1251) | 대상 생존 확인 뒤 · `ExecuteAttack` 앞에 회피 관문 **덧붙임**(S-2) | 연막 구성원이 없으면(평상시) 확률 0 → 관문을 지나 지금과 같은 경로. 투사체 착탄 중에는 건너뜀(이중 판정 방지) |
> | `UnitAttackShadowCoordinator`(892~918) | 「승인 명중 + writer 회피 → `Evaded`」 갈래 **덧붙임** | 기존 두 갈래(명중 · Miss)와 거절 갈래 무변경. 회피 상태는 이번에 새로 생겨 지금은 오지 않는다 |
> | `AttackDamageApplyStatus` · `ProjectileImpactOutcome` · `StatusEffectKind` | 맨 뒤에 값 **덧붙임**(`Evaded = 6` · `Evaded = 3` · `EvadeChance = 11` — 이름은 제안) | 기존 숫자 값 무변경(정수 직렬화 · 네트워크 전송 값) |
> | `ProjectileImpactPresentation.IsValid`(194~198) | 새 결과 「회피」 허용 — **옛 조건 줄은 주석으로 남긴다** | 명중 · 대상 소실 조건은 그대로 |
> | `UnitCombatUseCase.LaunchProjectile`(579~626) · `TryLaunchTowerProjectile`(253~) · `ProcessProjectileFlight`(628~682) | 발사 순간 판정 · 빗나간 지점 · 회피 착탄 갈래 **덧붙임**(S-3) | 회피가 아니면 지금 줄 그대로(착탄 시각 · 대상 추적 · writer 1회) |
> | `ProjectileLaunchClientRpc`(307~310) · `SendProjectileLaunchRpc`(223~229) · `AttackEffectClientRpc`(80~82) | 인자 **덧붙임**(빗나감 · 빗나간 지점) — 옛 서명은 주석 | RPC 모양 변경 → **스키마 12**로 옛 빌드와의 경기를 시작 거부 |
> | `ProjectilePresentationPresenter` 발사 · `Advance` · `Finish` | 회피면 대상을 따라가지 않는 갈래 **덧붙임** | 회피가 아니면 지금과 같다 |
> | `TowerCombatUseCase.ExecuteTowerAttack` 즉발 분기(417~477) · `VfxPoolItem.TowerBolt`(160~198) | 445행 앞 회피 관문 · 번개 고정 끝점 강제 **덧붙임**(S-4) | 회피가 아니면 지금과 같다 |
> | `PersistentStatusZoneExecutor`(51~60) | 새 상태 종류 통과 **덧붙임** | 기존 두 종류 검사 무변경 |
> | 셋업 도구 `SupportSkillLoadoutSetup.cs` 367~368 연막 줄 · 98 `ExpectedAvailableCount = 13` · 255~268 `PersistentZone`(266 `TargetsAllies = false` 고정) | 새 줄 · **14** · 아군 인자로 교체, **옛 줄은 주석** | 도구를 다시 실행해도 연막이 자리표시로 돌아가지 않게 · 기대 수 불일치로 멈추지 않게 |
> | `Skill_Human_SmokeBomb.asset` 값(Research 11-1) | **값 교체**, GUID 유지 | 로드아웃 참조 그대로. 옛 값은 Research 11-1 표와 셋업 도구 주석 줄로 되돌릴 수 있다 |
> | 스위트 `== 13` **6곳** · Z-T9 「연막 사용 불가」(S-1) · `== 11` **8곳**(S-3)(Research 11-3) | 14 · 연막 자산 계약 검사 · 12로 교체(메시지 포함), 옛 검사 주석 | 스위트가 셋업 도구 · 스키마 상수를 고정해 같은 커밋에서 고쳐야 한다 — 기대 수는 연막을 켜는 S-1(U-8), 스키마는 RPC 모양이 바뀌는 S-3(M-12) |

---

## 0. 근거로 읽은 규칙

WORKFLOW [4]에 따라 `Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고 「스킬」 · 「유닛」 · 「권위 · 시간 · 표현 동기화」 · 「건물」 행의 원본을 열었다. 「스킬 규칙 N」 = `GameSystemRules/GameSystemRules_Skills.md`, 「유닛 규칙 N」 = `GameSystemRules/GameSystemRules_Units.md`, 「탑 규칙 N」 = `GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 N.

| 규칙 | 이 계획과의 관계 |
|---|---|
| 스킬 규칙 28 「지속 상태 구역」 | 연막 구역은 이 장치 그대로 — 진입 적용 · 이탈 해제 · 타일 중심 판정 · 같은 스킬 겹침 1번 · 시전 건물 파괴 뒤에도 유지 · 경기 종료 해제. 「대상: 적 유닛만(지금 쓰는 두 스킬 기준)」 · 「연막탄 … 사용 불가」 → **개정 대상**(11절) |
| 스킬 규칙 13 「타입 C」 · 7 「데이터 주도」 | 새 상태 종류 「회피 확률」(강도 = 확률)을 스킬 자산의 상태 종류 · 강도 칸으로 넣는다 |
| 스킬 규칙 25 「서버에서만 판정·실행」 · 26 | 회피 판정 · 난수는 서버만. Client는 결과(회피 여부 · 빗나간 지점)만 받는다. 상위 R-11 「연막 회피 판정은 서버가 발사 시점에」 |
| 스킬 규칙 3 · 9 · 16 | 쿨다운은 발동 순간(임시 15) · 슬롯 4번 칸(셋업 도구 `h, 3`) · 지점 지정 원형 조준(기존 그대로) |
| 유닛 규칙 19 「피격 표현 큐」 · 20 「원거리 유닛 트레이서」 | 회피는 확정 결과가 「시각 결과 아님」이라 숫자 · 피격 반응이 없다. 투사체 끝점이 대상 대신 빗나간 지점 → **개정 대상** |
| 유닛 규칙 41 InfernoSpirit · 39 MushroomBomber | Inferno 화상은 피해 writer 안에서 걸리므로 회피하면 함께 없다(U-3). Mushroom은 범위 폭발이라 제외(M-1) |
| 유닛 규칙 44 피해 적용 순서 | 회피는 방어 감쇄 · 피해 배율 · 쉴드 **이전**에 판정 — 회피된 공격은 이 순서에 들어가지 않는다 |
| `U-IMPACT-LOCKEDPOINT` | 「발사 시점 목표점 고정」 개념 — 빗나간 지점은 발사 때 고정하고 비행 중 바꾸지 않는다 |
| `NET-DELIVERY-PROJECTILE` · `NET-DELIVERY-HITSCAN` · `NET-TIME-002` | 투사체는 서버가 발사 · 착탄 · 목표 처리를 소유 → 회피도 발사 기록에 싣는다. Hitscan은 서버 Impact에 결과 확정. 화면 지연(순수 Client 0.10초)은 그대로 |
| 탑 규칙 1 · 12 · 14 | 탑은 적 유닛만 공격(회피 대상은 항상 연막 속 아군 유닛), 공격 표현은 프로필(VineTower 행 없음 — Research 9-3), 대포탑 전방 120°(테스트 배치 조건) |
| TDD_Network 「경기 시작 호환 확인」(40행) | 「RPC 모양이 바뀌면 스키마를 올린다」 → **12** |

---

## 1. 결정

### 1-1. 사용자 결정 (2026-11-23) `[사용자 결정]`

| # | 항목 | 결정 | 출처 |
|---|---|---|---|
| U-1 | 방어탑 포함 범위(조사 O-7b) | **모든 방어탑 포함** — 인간 CannonTower · 정령 RuneSpire · 초월 VineTower. RuneSpire 번개 · VineTower도 빗나감 표현 → **탑 공격 표현 RPC 변경 포함** | 사용자 선택(조사 추천 「대포탑만」이 아님) |
| U-2 | 수치(임시, 조사 O-S2 · O-S3) | 회피 **50%** · 반경 **2** · 지속 **6초** · 쿨다운 **15** | 추천 채택 |
| U-3 | 특수 원거리 단일(조사 O-7c) | **InfernoSpirit 화상도 막는다** — 빗나간 공격은 피해 · 화상 모두 없음 | 추천 채택 |
| U-4 | 빗나감 표현(조사 O-S6 · O-S7) | **대상 옆에 떨어진다** — 발사 순간 서버가 빗나간 지점을 고정(대상 반경 + 0.3 옆 · 진행 방향 + 0.5, 좌우는 난수 — 거리 값은 임시), 투사체가 그곳으로 날아가 **착탄 이펙트만**, 숫자 없음 | 추천 채택 |
| U-5 | 스키마 | **11 → 12** | 추천 채택 |
| U-6 | 진행 방식 | **코드 작업 진행** — VFX 제작은 Unity AI 크레딧 소진으로 중단(메테오 VFX 프롬프트는 사용자가 Unity에 옮겨 둠, 「유니티에 옮겨놔서 프롬포트는 괜찮을거같고」) | 원문 「연막탄 작업진행해보자 task 문서작성해줘」 |
| U-8 | 「사용 가능」을 켜는 단계(종전 O-4) | **S-1부터 켠다** — S-1에서 자산 사용 가능 + 셋업 기대 수 14 + 스위트 `== 13` 6곳 → 14 + Z-T9 교체. S-5는 마무리 · 정리만 | 사용자 「1단계부터 켬」 |
| U-9 | 연막 구역 VFX 임시 프리팹(종전 O-2) | **테스트 동안 기존 프리팹을 연막 구역 VFX 칸에 임시 연결, 테스트 뒤 비운다**(메테오 · 공습 선례). 어떤 프리팹인지는 프로그래머가 정해 보고 | 사용자 「임시 프리팹 넣음」 |
| U-7 | 진단 로그 존속 — 이 Task 기본 방침 | **개발용(`GameLog.Dev`)으로 남긴다.** 공습 Task 사용자 결정 「그럼 남겨두고」(2026-11-18)를 기본 방침으로 따른다. 테스트 뒤 사용자가 다르게 정하면 그 결정을 따른다(WORKFLOW [4]) | 메인 세션 전달 |

### 1-2. 메인 세션 기본안 `[메인 기본안]` — 사용자에게 보고 예정 (확정 아님)

메인 세션이 질문하지 않고 조사 추천안을 채택했다. 사용자가 다르게 정하면 그 결정을 따른다.

| # | 항목(조사 번호) | 기본안 | 근거 |
|---|---|---|---|
| M-1 | 원거리 단일 유닛 범위(O-7a) | **투사체 6종(MushroomBomber 제외) + Hitscan 3종** | Mushroom은 착탄 범위 폭발 + 독(유닛 규칙 39)이라 「단일」 아님 — 넣으면 폭발 범위의 다른 유닛까지 보호된다. Hitscan을 빼면 인간 상대에서 총잡이가 연막을 무시한다 |
| M-2 | 연막 판정 기준(O-7d) | **맞는 아군(피격자)이 연막 안**에 있는가 | 기획 「연막 안 아군이 … 회피」(상위 Plan 133행) |
| M-3 | 발사 데이터 전송(O-S1) | **(가) 발사 RPC에 `evaded` + `missPoint` 추가** | 이름과 뜻이 맞고 진단 해석이 쉽다(Research 8-3) |
| M-4 | 연막 안 아군이 쏘는 공격(O-S4) | **불이익 없음** | 기획에 없음 — 넣으면 새 규칙(공격자 기준 판정) |
| M-5 | 연막 안 적 유닛(O-S5) | **혜택 없음** | 아군 구역이라 아군만 구성원(Research 5-1) |
| M-6 | 여러 연막 겹침(O-S8) | **1번만**(스킬 규칙 28) | 연막 스킬이 하나뿐 |
| M-7 | 건물 보호(O-S9) | **유닛만** | 기획 「유닛만」(상위 Plan 76행), 구역 장치가 유닛만 센다 |
| M-8 | 난수(O-S10) | **경기 seed + 공격 열쇠 해시**(SplitMix64), seed는 경기마다 서버가 뽑아 로그에 남긴다 | 순서 독립 · 재계산 가능(Research 10절) |
| M-9 | 핸드셰이크(O-S11) | **스키마 12에 묶는다** | 회피 대상 분류가 다른 빌드끼리 붙지 않게. 분류 토큰을 해시에 넣는 부분은 3절 O-3 |
| M-10 | 투사체 판정 시점(O-S13) | **발사 시점**(상위 R-11 그대로) | 착탄 시점이면 화면이 빗나감을 그릴 시간이 없다(Research 8-2) |
| M-11 | 회피 대상 분류 토큰(종전 O-3) | **핸드셰이크 해시에 포함**(조사 추천) — 분류가 다른 빌드끼리는 경기를 시작하지 않는다 | 투사체 `ManifestToken` 선례(Research 8-3) |
| M-12 | 스키마 12를 올리는 단계(종전 O-5) | **RPC 모양이 바뀌는 S-3과 같은 커밋**에서 상수 11 → 12와 스위트 `== 11` 8곳을 함께 바꾼다. S-4의 탑 RPC 변경도 같은 12 묶음 | 스위트가 상수를 고정 — 같은 커밋이어야 실패하지 않는다 |

---

## 2. 설계안 `[설계안 — 조사]`

| # | 설계안 | 근거 · 비고 |
|---|---|---|
| P-1 | 연막 = **지속 상태 구역(타입 4) + 새 상태 종류 `EvadeChance`(강도 = 확률), 아군 대상** — 새 메커니즘을 만들지 않는다. 실행기 허용 목록 · 구역 효과 합 · `ApplyKind`에 한 갈래씩 | Research 5-2 |
| P-2 | 구역 서비스 접근자 `GetEvadeChance(unitId)` — 효과 없는 평상시 0을 바로 반환. `UnitCombatUseCase`(이미 주입) · **`TowerCombatUseCase`(새 주입 — 조합 루트에서)** 가 읽는다 | Research 5-2 4 |
| P-3 | **회피 판정은 피해 writer 호출 이전** — `IncomingDamage.Apply` 안이 아니다(화상 · 피해 이벤트 누수 · C2 거절) | Research 6-2 |
| P-4 | **Hitscan 관문**: `ApplyAttackDamageCore` 대상 생존 확인 뒤 · `ExecuteAttack` 앞, 공격자가 Hitscan 분류 · 대상이 유닛 · **투사체 착탄 중이 아님**일 때. 회피면 writer 상태 `Evaded`(새 값)를 돌려주고, 코디네이터가 「승인 명중 + 회피 → `AttackImpactOutcome.Evaded`」로 닫는다. RPC 모양 변경 없음 | Research 7절 |
| P-5 | **투사체 발사 판정**: `LaunchProjectile` · `TryLaunchTowerProjectile`에서 대상이 유닛 · 연막 구성원 · 회피 대상 공격자(6종 · 인간 대포탑)면 판정 → 기록에 회피 + 빗나간 지점, 비행 시간은 발사 지점 → 빗나간 지점 거리로 다시 계산, 비행 중 대상 추적 안 함. 착탄 때 회피면 writer를 부르지 않고 결과 「회피」(`ProjectileImpactOutcome.Evaded = 3`) — 「writer 미적용 = 대상 소실」 갈래(670~673)로 보내지 않는다 | Research 8-1 |
| P-6 | 발사 알림(M-3 (가)): 발사 기록 구조체 + `ProjectileLaunchClientRpc` + `SendProjectileLaunchRpc`에 `evaded` · `missPoint` — 늦은 접속 스냅샷도 같은 함수를 지나 함께 실린다. 수신 검사는 회피면 `missPoint` 유한값 요구 | Research 8-3 |
| P-7 | 화면: 회피면 대상을 해석 · 추적하지 않고 끝점 = 빗나간 지점(EagleArcher 화살 첫 방향 포함). `Finish`는 「회피」를 대상 소실처럼 **착탄 이펙트만**, 숫자 방출 없음 | Research 8-2, U-4 |
| P-8 | **즉발 탑 관문**: `ExecuteTowerAttack` 445행 앞. 회피면 피해 · `OnEntityAttacked` · `OnEntityDamaged`를 내지 않고 표현 스냅샷만 낸다(빗나감 표시 + `Impact` = 빗나간 지점, `AppliedDamage` 0). 스냅샷 구조체 · `AttackEffectClientRpc`에 빗나감 칸 → RuneSpire 번개는 빗나감이면 **고정 끝점 갈래 강제**. VineTower는 프로필 행이 없어 화면에 아무것도 없다(Research 9-3) | Research 9절, U-1 |
| P-9 | **결정적 난수**: `MapRandom.Mix64(Combine(seed, key))`, 정수 비교 `roll % 10000 < 확률 × 10000`. 열쇠 = 투사체 `(epoch, id)` · 멀티 Hitscan `AttackResultKey` · 싱글 Hitscan 대체 열쇠 · 즉발 탑 스냅샷 순번(O-10). 판정 함수 하나를 세 경로가 같이 쓴다 | Research 10절, M-8 |
| P-10 | **빗나간 지점 함수 하나**(서버 계산 · 화면 소비 공통): `M = C + n × (r + 0.3) + d × 0.5`, 좌우는 같은 roll의 다른 비트. 표면 지점 함수 옆에 둔다 | Research 8-4, U-4 |
| P-11 | 셋업 도구: `PersistentZone`에 아군 인자(기본 false — 서리 · 수액 무변경) · 연막 새 줄(타입 4 · 지점 · 아군 · `EvadeChance` 0.5 · 6초 · 반경 2 · 쿨 15 · 피해 0) · 기대 수 14 · 주석 문구 | Research 11-2 |
| P-12 | 진단: 새 머리 `[SKILL-EVADE]`(이름 제안 — 조사 자료는 `[SKILL-SMOKE]`, O-12) · 구역은 기존 `[SKILL-ZONE]` · 투사체는 기존 `[PROJECTILE-END]`(v 올림) · `[PROJECTILE-VIEW]` | 6절 |

---

## 3. 열린 항목 — 이 Plan이 정하지 않은 것

| # | 항목 | 지금 상태 · 처리 | 누가 정하나 |
|---|---|---|---|
| O-1 | 연막 속 아군 몸에 붙일 상태 VFX(조사 O-S12) | 칸은 있다(`_statusVfxPrefab`). 상위 Plan 182행 기준 지속 범위 스킬 대상 상태 VFX는 「새로 넣지 않았다」 — VFX 기획 단계에서 정한다. 코드 변경 불필요 | VFX 기획 때 사용자 |
| O-2 | 연막 구역 VFX(V7) 테스트용 임시 프리팹 | **해결 — 넣는다(U-9)**, 테스트 뒤 비운다. 넣은 것 = `vfx_emp_disabled`(GUID `32e68fa52406d994fbdbfd8f8d4fcfbf`, `9e096e4`) *(직전 「연막도 그럴지 — 메인 세션 · 사용자」)* | — |
| O-3 | 회피 대상 분류(어떤 공격자가 회피 대상인가)를 핸드셰이크 해시에 넣을지 | **메인 기본안 M-11 — 넣는다**(보고 예정). *(직전 「메인 기본안(M-9) 문구는 「스키마 12에 묶음」뿐이라 토큰 추가 여부가 확인되지 않았다 — 메인 세션 확인」)* | 사용자 보고 |
| O-4 | 단계별 실기 테스트에서 연막을 쓰려면 「사용 가능」을 언제 켤지 | **해결 — S-1부터 켠다(U-8).** S-3 전까지는 연막이 총잡이 · 투사체 · 탑 일부만 막는 중간 상태가 브랜치에 생긴다(R-13). *(직전 「(가) S-1에서 켠다 / (나) S-5에서 켠다 — 메인 세션 · 사용자」)* | — |
| O-5 | 스키마 상수 12와 스위트 `== 11` 8곳을 바꾸는 단계 | **메인 기본안 M-12 — S-3과 같은 커밋**(보고 예정). S-3 · S-4 멀티 테스트가 핸드셰이크 보호를 받는다. *(직전 「S-3에서 함께 / 메인 세션 예시는 S-5 — 메인 세션」)* | 사용자 보고 |
| O-6 | 비행 중 대상이 죽은 「회피」 투사체의 착탄 결과 | 제안 = 「회피」 그대로(어차피 피해 없음 · 화면 그림 같음 · 진단 구별 쉬움) `[제안 — 문서 담당]`. 「대상 소실」로 보내는 안도 동작상 문제는 없다 | 프로그래머 판단 · 보고 |
| O-7 | 빗나간 지점의 화면 높이(가슴 높이 유지 / 땅) · RuneSpire 빗나간 번개 끝점 높이 | 서버는 바닥 좌표만 안다(Research 8-2 `ServerPoint`). 「대상 옆에 떨어짐」(U-4)에 맞는 높이를 프로그래머가 정해 보고 · 화면 확인 | 프로그래머 → 사용자 육안 |
| O-8 | 빗나간 지점이 맵 밖일 때 맵 안으로 자를지 | 화면 표현만이라 문제는 적다 `[판단 — 조사]` | 프로그래머 판단 · 보고 |
| O-9 | 수치 확정(회피 확률 · 반경 · 지속 · 쿨다운 · 빗나감 거리 0.3 · 0.5) | 밸런싱. 지금 값은 시험용 | 밸런싱 |
| O-10 | 싱글 Hitscan · 즉발 탑의 난수 열쇠 | **싱글 Hitscan 해결(S-2)** — 열쇠 = (공격자 Id, 공격자별 회피 판정 순번)(`SkillEvasionSystem.NextAttackerOrdinalKey`). 즉발 탑 열쇠는 S-4에서 정한다 *(직전 「대체 열쇠 · 탑 열쇠를 프로그래머가 정하고 진단에 남긴다」)* | 탑 몫 — 프로그래머(S-4) |
| O-11 | 경기 seed를 뽑는 방법 | **해결(S-1)** — 맵을 불러올 때마다 `MapRootSeed.Create()`로 새로 뽑는다(재경기 = 새 seed), 판정 줄 · 경기 끝 요약에 `Seed=`. Client는 판정하지 않는다 *(직전 「뽑는 재료는 프로그래머 확정」)* | — |
| O-12 | 진단 머리 이름 | **해결 — `[SKILL-EVADE]`** *(직전 「`[SKILL-EVADE]` / `[SKILL-SMOKE]` — 프로그래머 확정」)* | — |

---

## 4. 구현 단계 (커밋 단위) `[제안 — 문서 담당 정리 · 프로그래머 확인 필요]`

위험이 커서 다섯 단계로 나눈다. 각 단계는 **혼자 컴파일되고 기존 전투를 바꾸지 않은 채** 끝나야 다음으로 간다. 순서 변경은 game-programmer가 판단해 보고한다. 「사용 가능」은 **S-1에서 켜고**(U-8), 스키마 12는 **S-3에서 올린다**(M-12).

| 단계 | 내용 | 근거 규칙 · 결정 | 단독 확인 |
|---|---|---|---|
| **S-1 구역 · 상태 · 켜기** | P-1 · P-2 · P-11 — `EvadeChance` 종류 · 실행기 통과(0~1) · 구역 효과 합 · `GetEvadeChance` · `TowerCombatUseCase` 주입(읽기는 S-4) · 진단 분기, 셋업 도구 아군 인자 · 연막 새 줄, 자산 값(타입 4 · 지점 · 아군 · 0.5 · 6초 · 반경 2 · 쿨 15 · 피해 0) + **사용 가능**, 셋업 기대 수 13 → **14**(옛 줄 주석) · 스위트 `== 13` **6곳** → 14 · Z-T9 「연막 사용 불가」 → 연막 자산 계약 검사(U-8), 연막 구역 VFX 칸에 **임시 프리팹**(U-9). 난수 · 빗나간 지점 **공용 함수**도 여기서 만든다(판정 호출은 아직 없음) | 스킬 규칙 7 · 9 · 13 · 25 · 28, U-2 · U-8 · U-9, M-2 · M-5 · M-6 · M-7 · M-8 | 자가 검증 E-T1 · E-T2 · E-T10 · E-T12(사용 가능 14 · 자산 계약), 서리 · 수액 회귀, 8-1 실기 |
| **S-2 Hitscan 회피** | P-3 · P-4 — 관문(Hitscan 분류 · 대상 유닛 · 투사체 착탄 중 아님) · writer 상태 `Evaded` · 코디네이터 갈래 · 싱글 대체 열쇠. 숫자는 writer를 안 부르니 자연히 생략. `[SKILL-EVADE]` Hitscan 줄 | 유닛 규칙 19 · 44, `NET-DELIVERY-HITSCAN`, M-1 | E-T3 · E-T4 · E-T11 |
| **S-3 서버 권위 투사체 회피** | P-5 · P-6 · P-7 · P-10 — 발사 순간 판정(6종 · 대포탑, Mushroom 제외, 대상 유닛만) · 빗나간 지점 · 비행 시간 재계산 · 착탄 결과 「회피」(writer 미호출 → **화상 차단**) · 수신 검사 · 발사 RPC `evaded` + `missPoint`(늦은 접속 포함) · 화면 비추적 · 착탄 이펙트만 · **스키마 11 → 12 + 스위트 `== 11` 8곳 → 12(같은 커밋 — M-12)** · 회피 대상 분류 토큰 해시 포함(M-11) · `[PROJECTILE-END]` v 올림. 이중 판정 방지(투사체 착탄 중 Hitscan 관문 건너뜀) 재확인 | `NET-DELIVERY-PROJECTILE`, 유닛 규칙 20 · 41, `U-IMPACT-LOCKEDPOINT`, TDD_Network 「경기 시작 호환 확인」, U-3 · U-4 · U-5, M-3 · M-10 · M-11 · M-12 | E-T5~E-T8 · E-T11 · E-T12(스키마 12) |
| **S-4 즉발 탑 회피 · 빗나감 표현** | P-8 — `TowerCombatUseCase` 445행 앞 관문 · 피해 · 이벤트 없음 · 스냅샷 빗나감 칸 · `AttackEffectClientRpc` 인자 · RuneSpire 고정 끝점 강제. VineTower는 관문만(화면 없음). 같은 스키마 12 묶음 | 탑 규칙 1 · 12, U-1 | E-T9 |
| **S-5 마무리 · 정리** | 새 자가 검증 파일 정리 · 진단 줄이 6절 표대로 모두 나오는지 · 전체 회귀. (자산 켜기 · 기대 수 14 · `== 13` 6곳 · Z-T9는 S-1로, 스키마 12 · `== 11` 8곳은 S-3으로 옮겼다 — U-8 · M-12.) 테스트 통과 뒤 임시 프리팹 비우기(U-9) · 주석 블록 최종 삭제는 사용자 확인 뒤 | WORKFLOW [4] | 스위트 · 8-6 전체 실기 |

- `[제안 — 문서 담당]` 진단 로그는 각 단계가 만든 경로와 함께 넣는다. 테스트 항목별 확인 방법(검색어 · 정상 값)은 구현 보고에 적는다(WORKFLOW [4]).
- 새 `.cs` 파일은 `.meta`를 함께 커밋할지 프로그래머가 확인한다(공습 · 메테오 선례 — programmer `skill-system.md` §8 · §9).

---

## 5. 변경 예상 파일 `[제안 — 문서 담당 정리, Research 기준 · 프로그래머 확인 필요]`

| 파일 | 단계 | 바뀌는 것 |
|---|---|---|
| `Scripts/Domain/Status/StatusEffectKind.cs` | S-1 | `EvadeChance = 11` |
| `Scripts/Application/Skill/PersistentStatusZoneExecutor.cs` | S-1 | 새 종류 통과 · 0~1 검사 |
| `Scripts/Application/Services/SkillZoneSystem.cs` · `.Diagnostics.cs` | S-1 | 효과 칸 · `ApplyKind` · `GetEvadeChance` · 진단 표기 |
| 난수 · 빗나간 지점 공용 함수(새 파일 또는 `Projectiles.cs` 곁 — 위치는 프로그래머) | S-1 | P-9 · P-10 |
| `Scripts/Bootstrap/GameBootstrapper*.cs` | S-1 | `TowerCombatUseCase`에 구역 서비스 주입 |
| `Scripts/Application/Combat/AttackDamageApplyStatus.cs` · `UnitCombatUseCase.cs` · `Infrastructure/Network/UnitAttackShadowCoordinator.cs` | S-2 | `Evaded` 상태 · 관문 · 코디네이터 갈래 |
| `Scripts/Application/Combat/ProjectileDeliveryContracts.cs` · `UnitCombatUseCase.Projectiles.cs` | S-3 | 결과 `Evaded` · 발사 기록 칸 · 발사 판정 · 착탄 갈래 · 진단 |
| `Scripts/Infrastructure/Network/NetworkCombatController.Projectiles.cs` · `UnitAttackShadowProfileResolver.cs`(45 · 306~) | S-3 | 발사 RPC 인자 · 스키마 12 · 회피 대상 분류 토큰(M-11) |
| `Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs` | S-3 | 비추적 · 끝점 · `Finish` 갈래 · 표본 줄 |
| `Scripts/Application/UseCases/TowerCombatUseCase.cs` · `Application/Combat/AttackEffectPresentation.cs` · `Infrastructure/Network/NetworkCombatController.cs`(73 · 80~82) · `Presentation/Effects/VfxPoolItem.TowerBolt.cs` · `EffectManager.cs` | S-4 | 탑 관문 · 스냅샷 칸 · RPC · 번개 고정 끝점 |
| `Resources/Config/Skills/Skill_Human_SmokeBomb.asset` | S-1 | 값 · 사용 가능 · 구역 VFX 임시 프리팹(테스트 뒤 비움) |
| `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` | S-1 | `PersistentZone` 아군 인자 · 연막 줄 · 14 · 주석 |
| 스위트 6파일(`== 13`) · `SkillZones.cs` Z-T9 · 새 `Suite/RunUnitActionSelfValidation.SmokeBomb.cs`(이름은 프로그래머) | S-1(이후 단계마다 E-T 추가) | 14 · 연막 계약 · E-T 검사 |
| 스위트 8파일(`== 11`) | S-3 | 12 |

---

## 6. 테스트 항목 ↔ 진단 로그 (WORKFLOW [4] 「테스트 항목 진단 로그 규칙」)

모든 새 줄은 `GameLog.Dev`, `DiagRev=1`, **상태가 바뀔 때만**(LogRules §1.14 금지 8), 존속 = **개발**(U-7). 판정 줄은 **처음 N건만 + 경기 끝 요약 1줄**(투사체 진단 상한 `ProjectileDiagnosticLimit = 96` 선례). 새 머리 `[SKILL-EVADE]` · `Diag=SkillEvade`(O-12). **이벤트 · 키 이름은 제안 — 프로그래머가 구현 때 확정** `[제안 — 문서 담당]`. 구역 생성 · 구성원은 기존 `[SKILL-ZONE]`, 투사체 표본은 기존 `[PROJECTILE-VIEW]`를 그대로 쓴다.

| # | 테스트 항목 | 로그 | 남기는 시점 | 정상 판정 값 |
|---|---|---|---|---|
| E-L1 | 연막 발동 · 구역 생성(쿨 15) | 기존 `[SKILL-VFX] ActivationConfirmed` · `[SKILL-ZONE] ZoneCreated` | 서버 확정 | 슬롯 4번 칸 · `Kind=EvadeChance` · `TargetTeam` = 시전 팀 · `MagMilli=500` · `Radiusmm=2000` · `DurationMs=6000` |
| E-L2 | 아군 유닛만 구성원(적 · 건물 없음), 들어오면 · 나가면 | `[SKILL-ZONE] ZoneMembersChanged` | 구성원 변경 틱 | `Entered` · `Exited`가 시전 팀 유닛 Id만 |
| E-L3 | 총잡이 공격 회피 — 피해 · 숫자 없음 | `[SKILL-EVADE] EvadeRoll` | 판정 1회당(처음 N건) | `Path=Hitscan` · `AttackerType` ∈ Pistoleer/Assault/Sniper · `Chance=500` · `Roll` · `Evaded=1` · `HpBefore` = `HpAfter` |
| E-L4 | (멀티) 회피가 C2 결과 「회피」로 닫힌다 | `[SKILL-EVADE] EvadeResolved`(또는 기존 C2 진단의 결과 칸) | 결과 확정 | `Outcome=Evaded`, 같은 열쇠로 `InvalidInput` 0건 |
| E-L5 | 투사체 회피 — 발사 순간 판정 · 빗나간 지점 | `EvadeRoll` · `EvadeMissPoint` | 발사 | `Path=Projectile` 또는 `TowerShell` · `Key=epoch:id` · `Evaded=1` · 빗나간 지점 ↔ 대상 중심 거리 ≈ 반경 + 0.3(옆) · 비행 시간 |
| E-L6 | 투사체가 빗나간 지점에 떨어지고 숫자 없음 | 기존 `[PROJECTILE-VIEW]` · `[PROJECTILE-END]` | 화면 착탄 · 경기 끝 | `outcome=Evaded` · 숫자 방출 0 · END의 회피 수 칸(새 v) = 서버 회피 수 |
| E-L7 | InfernoSpirit 회피 → 화상 없음 | `EvadeRoll` + 기존 `TimedDamageEffectChannel` | 발사 · 착탄 | Inferno 회피 열쇠에 대해 화상 `Stage=Grant` 0건 |
| E-L8 | 인간 대포탑 포탄 회피 | `EvadeRoll` | 발사 | `Path=TowerShell` |
| E-L9 | RuneSpire 번개가 빗나감(대상 옆) | `EvadeRoll` + 번개 상태 기록(기존 `AttackEffectIdentity`) | 공격 | `Path=TowerInstant` · `Race=Spirit` · 번개 상태 `Untracked(Evaded)` 류(이름은 프로그래머) |
| E-L10 | VineTower 회피 — 숫자 · HP 변화 없음(화면 이펙트는 원래 없음) | `EvadeRoll` | 공격 | `Path=TowerInstant` · `Race=Transcendence` · `Evaded=1` |
| E-L11 | MushroomBomber는 회피 대상 아님 · 연막 밖 · 적 · 건물 대상은 판정 안 함 | `[SKILL-EVADE] EvadeSummary`(경기 끝 1줄) | 경기 끝 | `MushroomRolls=0` · 대상이 연막 밖 / 적 팀 / 건물인 판정 0 |
| E-L12 | 회피 비율 ≈ 50% · 경로별 수 | `EvadeSummary` | 경기 끝 | 경로별 `Rolls` · `Evaded` · 비율(표본이 적으면 넓게 본다) |
| E-L13 | 결정성 — 같은 seed · 열쇠면 같은 결과 | `EvadeSummary` · `EvadeRoll` | 경기 시작 seed · 판정 | `Seed` 1회 기록, 판정 줄의 `Key` · `Roll`이 오프라인 재계산과 같다 |
| E-L14 | 스키마 12 | 기존 경기 시작 호환 로그(`c3-schema=`) | 경기 시작 | `c3-schema=12`, 거부 0 |
| E-L15 | 경계 지연(멀티 0.1초) | `EvadeRoll`의 `ZoneSeq` · 구성원 변경 `ElapsedMs` | — | 경계 근처 판정은 직전 구성원 변경 틱 기준(이상 아님 — R-5) |
| E-L16 | 회귀 스위트 | — | — | Unity Console — 사용자 FAIL 언급이 없으면 PASS(WORKFLOW [6]) |

- **로그로 판정할 수 없는 항목(사용자에게 알린다):** 투사체가 「대상 옆에 떨어져」 보이는지 · 빗나간 지점 높이(O-7) · RuneSpire 빗나간 번개 모양. 총잡이 회피는 원래 날아가는 그림이 없어 **화면에 빗나감이 보이지 않는 것이 정상**이고, VineTower는 공격 이펙트가 원래 없다(Research 9-3) — 둘 다 「숫자가 안 뜬다」만 눈으로 볼 수 있다. 연막 범위는 테스트 동안 임시 프리팹으로 보인다(U-9) — 모양 · 크기는 정식 VFX가 아니다.

---

## 7. 자가 검증 (회귀 스위트 픽스처) `[제안 — 문서 담당 정리 · 프로그래머 확정]`

**픽스처 원칙:** 전역 스탯 표는 비어 있거나 실제 값일 수 있으므로 **빈 표에서 시작해 필요한 행만 고정하고 끝나면 원래 참조로 되돌린다**(공용 메모리 「Regression suite fixtures」 — `SupportFixtureStatsScope`). 확률은 **1.0 · 0.0**으로 결정적 검사, 중간값은 고정 seed + 열쇠로 기대값을 다시 계산한다.

| # | 검사 | 판정 |
|---|---|---|
| E-T1 | 아군 구역 구성원 | 시전 팀 유닛만 확률 0.5, 적 유닛 · 건물 · 구역 밖 0. 구역 없음이면 0(평상시 경로 그대로) |
| E-T2 | 같은 스킬 겹침 · 이탈 · 만료 · 사망 · 경기 종료 | 겹쳐도 0.5(1번), 나가면 · 끝나면 0 |
| E-T3 | Hitscan 회피(확률 1.0) — 싱글 경로 · 멀티 writer 경로 | writer 상태 `Evaded` · HP 그대로 · 피해 이벤트 0 · 피해 숫자 원천 없음 |
| E-T4 | 코디네이터 — 승인 명중 + `Evaded` | `AttackImpactOutcome.Evaded`로 닫힘(불일치 거절 아님). 기존 명중 · Miss 갈래 회귀 |
| E-T5 | 투사체 회피(확률 1.0) | 착탄 때 writer 미호출 · HP 그대로 · 결과 `Evaded` · 지점 = 빗나간 지점(≠ 표면 지점) · 비행 시간 = 거리 ÷ 속도 |
| E-T6 | **이중 판정 없음** | 회피 안 한 투사체 착탄 때 Hitscan 관문이 다시 굴리지 않는다(판정 호출 수 = 발사 수) |
| E-T7 | **화상 차단** | InfernoSpirit 회피 → 화상 기록 0. 회피 안 하면 화상 그대로(유닛 규칙 41 회귀) |
| E-T8 | Mushroom 제외 · 대상 건물 제외 | Mushroom 발사 판정 0, 건물 대상 판정 0, 폭발 · 독 그대로 |
| E-T9 | 즉발 탑(RuneSpire · VineTower) 회피 | 피해 0 · 두 이벤트 0 · 스냅샷 빗나감 표시 + `Impact` = 빗나간 지점 · `AppliedDamage` 0. 대포탑은 E-T5 경로 |
| E-T10 | **난수 해시 결정성 · 경계** | 같은 seed · 열쇠 → 같은 결과, 다른 시스템이 난수를 더 써도 불변. 확률 0 → 항상 명중, 1.0 → 항상 회피, 경계 `roll % 10000 == 확률 × 10000 − 1`은 회피 · `== 확률 × 10000`은 명중 |
| E-T11 | 수신 검사 | 결과 `Evaded = 3` 통과 · 4 이상 거부 · 회피 발사 기록의 `missPoint` NaN 거부 |
| E-T12 | 사용 가능 14 · 연막 자산 계약(S-1) · **스키마 12** · 분류 토큰(S-3) | `ExpectedAvailableCount == 14` 6곳 · 연막 자산(타입 4 · 아군 · `EvadeChance` 0.5 · 6초 · 반경 2 · 쿨 15 · 사용 가능 · 구역 VFX 칸은 비었거나 비반복 아님을 계약 검사) — Z-T9 「사용 불가」 대체 / `== 12` 8곳 · 해시 원문에 회피 분류 토큰 |

---

## 8. 테스트 절차 (단계별 · 짧게) `[제안 — 문서 담당 · 메인 세션이 [6] 안내로 옮긴다]`

게임 규칙 대조(공용 메모리 체크리스트 · Research 15절): 연막을 쓰는 쪽은 **인간**이고, 상대 원거리 공격은 **상대 종족**이 정한다. **AI는 스킬을 쓰지 않고 탑을 짓지 않는다** → 싱글은 AI 원거리 유닛이 내 인간 유닛을 쏘는 장면만, **탑은 멀티(상대가 탑을 짓는다)에서만** 볼 수 있다. 싱글 AI 종족은 무작위다. 건물은 놓는 즉시 생긴다. 로그는 `Hexiege/Logs/Collect Device Logs and Create ZIP`.

연막탄은 S-1부터 켜져 있다(U-8). 테스트 동안 연막 구역 VFX 칸에 임시 프리팹이 있어 연막 범위가 보인다(U-9).

### 8-1. S-1 구역 · 상태
1. `Hexiege/Combat/Run Full Regression Suite` 실행.
2. 싱글 정령으로 서리 감옥 · 수액 늪을 한 번씩 써 그대로 동작하는지 본다(회귀).
3. 싱글 **인간** → FlightFacility를 놓고 **연막탄**(4번 칸)을 내 유닛 무리 위에 쓴다 — 연막(임시 이펙트)이 6초 남는지 본다.
4. 연막 안으로 내 유닛을 넣었다 빼 본다(적 유닛이 들어와도 된다). 이 단계에서는 아직 회피가 없다(정상).
5. 원소 공명은 여전히 조준에 안 들어가는지 본다.

### 8-2. S-2 총잡이 회피
1. 스위트 실행.
2. 회귀: 싱글에서 Pistoleer · Assault · Sniper가 평소처럼 맞히는지 본다(연막 없이).
3. 싱글 인간, 상대 AI가 **인간**일 때 AI 총잡이가 쏘는 내 유닛 위에 연막 → 숫자가 가끔 안 뜨는지 본다(빗나가는 그림은 원래 없다).
4. 멀티(Host 인간 · Client 인간)에서 같은 장면 1번.

### 8-3. S-3 투사체 회피 — **두 빌드 모두 새로 설치**
1. 스위트 실행.
2. 회귀: 싱글에서 Tank · InfernoSpirit · EagleArcher 공격이 평소처럼 날아가 맞는지 본다.
3. 싱글 인간, AI가 **정령**(InfernoSpirit · StreamSpirit) 또는 **인간**(Tank · CannonCart)일 때 맞는 내 유닛 위에 연막 → 일부 투사체가 옆으로 떨어지고 숫자 · 화상이 없는지 본다.
4. 멀티(Host 인간 · Client 인간): Client가 대포탑을 짓고, Host 유닛을 탑 **앞쪽**(전방 120°)에 연막과 함께 세운다 → 포탄이 빗나가는지 양쪽 화면에서 본다.

### 8-4. S-4 즉발 탑 회피
1. 스위트 실행.
2. 멀티(Host 인간 · Client **정령**): Client가 RuneSpire를 짓고, Host 유닛을 연막 안에 넣어 번개가 대상 옆으로 빗나가는지 본다.
3. 멀티(Host 인간 · Client **초월**): Client가 VineTower를 짓고 같은 장면 — 이펙트는 원래 없으니 숫자만 가끔 안 뜨는지 본다.

### 8-5. S-5 마무리
1. 스위트 실행(사용 가능 14 · 스키마 12 검사 포함).
2. 아래 8-6 전체 확인으로 넘어간다.

### 8-6. 전체 확인 (S-5 뒤 한 번)
1. 싱글 인간 1판 — AI 원거리 유닛에게 맞는 내 유닛 위에 연막 2~3번.
2. 멀티 Host 인간 vs Client 정령 · 초월 · 인간 중 고를 수 있는 만큼(탑을 지어 달라고 Client에 부탁).
3. 로그 올리기: 작업명 `support-skill-smoke-bomb`.

---

## 9. 위험

| # | 위험 | 대응 | 근거 |
|---|---|---|---|
| R-1 | **투사체 발사 기록 · 착탄 결과 계약 변경이 7종 + 대포탑 전 경로 · 늦은 접속 · 화면 중복 거부에 닿는다** | 회피가 아니면 지금 줄 그대로(최상단 표) · 발사 RPC 보내는 함수 하나에 칸을 더해 늦은 접속도 함께(P-6) · E-T5 · E-T11 · 기존 서버 투사체 자가 검증 회귀 | Research 8절, 상위 Plan 583행 |
| R-2 | 늦은 접속 스냅샷 경로는 실기로 관측된 적이 없다 | 코드 경로 대조 + 자가 검증만 — 실기 PASS에 넣지 않는다 | 네트워크 TDD 「늦은 접속」 |
| R-3 | 스키마 12 — 두 빌드 재설치 · 스위트 `== 11` 8곳 | S-3 같은 커밋(M-12) · 테스트 안내에 재설치 명시 | Research 11-3 |
| R-4 | `IncomingDamage.Apply` 안에서 끊으면 화상 · 피해 이벤트 누수 + C2 불일치 거절 | P-3(writer 이전) | Research 6-2 · 7-3 |
| R-5 | 멀티 구역 구성원 갱신 0.1초 vs 발사 매 프레임 → 경계 근처 판정 지연(최대 0.1초) | 정상으로 받아들인다(서리 감옥과 같은 성격) — E-L15로 확인만 | Research 5-4 |
| R-6 | 투사체 착탄도 같은 writer를 지나 Hitscan 관문과 이중 판정 | 관문 조건에 「투사체 착탄 중 아님」 · E-T6 | Research 7-3 |
| R-7 | 싱글 피격 표현 큐 FIFO — 회피된 타격 신호가 버려진 뒤 다음 타격 숫자와 엇갈릴 수 있다 | 실기에서 숫자가 한 번씩 밀리지 않는지 본다 | Research 7-4 `[판단]` |
| R-8 | 회피 확률은 서버 결과를 바꾸는 첫 「확률」 전투 요소 — 재현 · 진단은 seed + 열쇠 로그로만 가능 | P-9 열쇠 해시 · E-L13 · E-T10 | Research 10절 |
| R-9 | RuneSpire 번개가 대상을 추적해 빗나감이 안 보인다 | P-8 고정 끝점 강제 · E-L9 | Research 9-2 |
| R-10 | VineTower 빗나감은 화면으로 확인할 수 없다(프로필 행 없음) | 로그 · 숫자로만 판정, 사용자에게 미리 알린다 | Research 9-3 |
| R-11 | 연막 자산에 구역 칸이 직렬화돼 있지 않다 → 셋업 도구 재실행 · 자산 저장 순서 | P-11 · 셋업 도구는 VFX 칸을 쓰지 않는다(254행) | Research 11-1 |
| R-12 | 주석 문자열을 검사하는 소스 게이트가 비활성 줄 삭제 때 깨진다 · 새 `.cs` `.meta` 누락 | 정리 때 스위트 대조 · `.meta` 확인(선례) | programmer `skill-system.md` §8 · §9 |
| R-13 | S-1에서 켜므로(U-8) S-4까지 연막이 일부 경로만 막는 중간 상태가 브랜치에 생긴다 | 브랜치 안에서만 — 전체 PASS 전 병합하지 않는다 | U-8 |
| R-14 | 임시 프리팹을 테스트 뒤 비우지 않거나, 셋업 도구 재실행이 칸을 건드린다 | 테스트 뒤 비움을 S-5 정리 목록에 둔다 · 셋업 도구는 구역 VFX 칸을 쓰지 않는다(254행) | U-9, Research 11-2 |

---

## 10. 바꾸지 않는 것

- 피해 공통 헬퍼 `IncomingDamage.Apply`와 피해 적용 순서(유닛 규칙 44) · 방어 감쇄 · 쉴드 · 받는 피해 배율.
- MushroomBomber 포자 · 독, TorrentSpirit 파도, 근접 · 스플래시 · 지속 피해 틱 · 스킬 피해.
- 서리 감옥 · 수액 늪 동작(셋업 도구 아군 인자 기본 false), 상태 시스템 `StatusEffectSystem`.
- 착탄 시각 계산 · 대상 추적 · writer 1회(회피가 아닌 투사체), 화면 지연(순수 Client 0.10초 · Host/싱글 0초), 「투사체 화면 착탄 대기」 장치.
- 탑 타겟 선택 · 대포탑 전방 120° · 탑 공격 프로필 표(VineTower 행을 새로 만들지 않는다).
- 원형 조준 · 스킬 요청 · 발동 알림 RPC 모양, 구역 구성원 알림 RPC.
- 원소 공명 「사용 불가」, 포격 · 공습 · 메테오 · 다른 스킬.

---

## 11. 테스트 통과 뒤 반영할 문서 `[이번 작업에서는 고치지 않는다]`

| 문서 | 자리(근거 행 — HEAD `43dbf22`) | 바뀔 내용 |
|---|---|---|
| `GameSystemRules/GameSystemRules_Skills.md` | 규칙 28(207행 — 209행 「대상: **적 유닛**만(지금 쓰는 두 스킬 기준 …)」 · 221행 「연막탄도 … 「사용 불가」로 남아 있다 — 회피는 이 규칙에 들어 있지 않다」) | 대상에 아군(연막) · 효과 「회피 확률」 · 회피 대상 공격(M-1 · U-1) · 판정 기준(맞는 아군 — M-2) · 수치(U-2) — 221행 교체. 새 메커니즘 번호는 만들지 않는다(타입 4 유지 — P-1) `[제안 — 반영 때 사용자 확인]` |
| 같은 문서 | 규칙 13(168행~ — 상태 종류의 뜻) · 규칙 7 요소 표 「파라미터」 행(78행~) | 상태 종류 「회피 확률」(강도 = 확률)의 뜻 |
| 같은 문서 | 규칙 25(340행~ — 구역 항목) | 회피 판정 · 난수는 서버만, 투사체는 **발사 시점**(상위 R-11) · Client는 회피 여부 · 빗나간 지점만 받음 · 스키마 12 |
| 같은 문서 | 자연어 설명 19행 「기능이 붙은 것은 **열셋**」 | 열넷 · 「사용 불가」 1개(원소 공명) |
| `GameSystemRules/GameSystemRules_Units.md` | 규칙 19(339행) · 규칙 20(350행) | 회피 결과는 숫자 · 피격 반응 없음 · 투사체 끝점 = 빗나간 지점(트레이서 없는 Hitscan은 그림 없음) |
| 같은 문서 | 규칙 41(580행 — InfernoSpirit) · 규칙 44(619행) | 회피하면 화상 없음 · 회피는 피해 계산 이전 |
| `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | `NET-DELIVERY-PROJECTILE`(268행) · `NET-DELIVERY-HITSCAN`(264행) | 발사 기록에 회피 · 빗나간 지점, 착탄 결과 「회피」 · Hitscan 결과 `Evaded` |
| `GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 | 규칙 12(197행) | 즉발 탑 빗나감 표현(번개 고정 끝점 · 지면 분출 기준점 = 빗나간 지점) |
| `TechnicalDesignDocument_Network.md` | 「서버 권위 투사체 전송 계약」(30행~ — RPC 항목 · 결과 종류) · 「경기 시작 호환 확인」(40행 — 스키마 **11**) | 발사 RPC 인자 · 결과 `Evaded` · 탑 공격 표현 RPC 인자 · 스키마 12 · (O-3) 분류 토큰 |
| `Assets/SkillVFXDesign.md` | V7(145~149행 — 「스킬은 회피 판정이 없어 「사용 불가」다」 · 열린 항목 「회피 대상 범위(기획 Plan O-7)」) | 스킬 사용 가능 · O-7 답 |
| [상위 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) | 2절 연막탄 행(133) · 3-1 인간 4(210) · 3-2 2차 끝(293) · 4절 17 · 18(349~350) · 5절 O-7(378 — 「열림」) · 6절 R-11(451) · 위험 583 | 2차 끝(E) 몫 반영 메모(메테오 · 공습 메모와 같은 모양) |
| `PROJECT_STATUS.md` · `WORK_HISTORY.md` · 공용 / 에이전트 메모리 | 지원 스킬 행 · 사용 가능 14 · 스키마 12 | WORKFLOW [9] · [11] — 공용 메모리 「now 11」 · 「13 available」 류 상태 도장 일괄 점검 |
| 이 Plan · Research | 구현 · 테스트 결과 절, 현재 상태 줄 | — |

---

## 12. S-1 · S-2 구현 · 1차 테스트 결과 (2026-11-23)
<a id="12-s1-s2-result"></a>

### 12-1. 구현 (커밋 `9e096e4` — `git show --stat` 27파일, +2081 / −40)

- 자산 `Skill_Human_SmokeBomb.asset`: 사용 가능 · 타입 4 · 쿨 15 · 6초 · 피해 0 · `EvadeChance`(11) · 0.5 · 아군 · 구역 VFX = 임시 `vfx_emp_disabled`(U-9). 셋업 도구 `ExpectedAvailableCount = 14`(옛 13 · 8 줄 주석). 스위트 `== 13` 6곳 → 14 · Z-T9 교체 · 새 `RunUnitActionSelfValidation.SmokeBomb.cs`(U-8 — S-1로 당김).
- 서버: `StatusEffectKind` · `PersistentStatusZoneExecutor` · `SkillZoneSystem`(+ 진단), 새 `SkillEvasionSystem`(+ `.Diagnostics`) · `Skill/SkillEvasionRoll.cs` · `UnitCombatUseCase.Evasion.cs`, `AttackDamageApplyStatus.Evaded` · `UnitAttackShadowCoordinator` 갈래, 조합 루트(`GameBootstrapper` · `.Setup`) · `NetworkCombatController` 연결.

### 12-2. 프로그래머 판단 · 계획과 달라진 점

| 항목 | 내용 |
|---|---|
| 회피 판정 창구 | 구역 서비스 접근자를 직접 읽지 않고 **새 서비스 `SkillEvasionSystem`** 이 seed · 확률 묻기 · 판정 · 요약을 맡는다(P-2 · P-9의 구현 모양) |
| 싱글 열쇠(O-10) | (공격자 Id, 공격자별 회피 판정 순번) |
| seed(O-11) | 맵 로드마다 `MapRootSeed.Create()` |
| 진단 머리(O-12) | `[SKILL-EVADE]` |
| `TowerCombatUseCase` 주입 | S-1이 아니라 **S-4로** 미뤘다(읽는 곳이 S-4뿐) |
| 다른 회피 스킬과 겹침 | `1 − (1 − a)(1 − b)` 합성으로 구현 — 지금 회피 스킬은 연막 하나라 **해당 경우 없음**(같은 스킬은 규칙 28대로 1번) |
| E-L15(경계 지연) | 진단 생략 — R-5 「정상으로 받아들인다」 그대로 |
| **5절 밖 수정** | `UnitAttackShadowObserver.cs` 두 곳 — writer `Evaded`를 승인 명중과 일치하는 결과로 세고, C2 `Evaded`와 짝으로 본다(옛 줄은 주석). 고치지 않으면 회피가 진단 불일치(FAIL)로 집계된다 |

### 12-3. 1차 테스트 — 로그 `support-skill-smoke-bomb_01` (멀티, 에디터 Host) — 메인 세션 판정

| 항목(6절) | 결과 |
|---|---|
| E-L1 · E-L2 구역 | 연막 2회, `TargetTeam=Red`(시전 팀) · `DurationMs=6000` |
| E-L3 · E-L12 Hitscan 회피 · 비율 | 판정 224회 · 회피 125 — `RatePermille=558`(50% 이항 분포 범위). 상세 62줄 중 회피 34건 HP 불변, 명중은 정상 감소 |
| E-L4 C2 | `C2Evaded=125` · `C2Other=0`, `EvadeResolved` 34건 모두 `Verdict=PASS` |
| 투사체 · 탑 판정 | 0건 — S-3 · S-4 범위 밖이라 정상 |
| 에러 | 0 |

### 12-4. 미관측 (판정에 넣지 않는다)

- **싱글 경로**(싱글 열쇠 `KeySrc` 싱글) — 이번 로그는 멀티뿐.
- **Client 화면 숫자 미표시** — 로그 판정 항목 아님, 사용자 이상 보고 없음(육안 확인을 따로 받지는 않았다).
- **회귀 스위트 — 미보고.** 사용자 「회귀는 나중에 해보고 알려줄게」 — 실행 안 한 것으로 다룬다(WORKFLOW [6] 무언급 PASS 규칙을 쓰지 않는다).
- 임시 프리팹 모양 · 크기(정식 VFX 아님).

### 12-5. 다음

사용자 지시 「문서갱신하고 3단계 진행해」 → **S-3(서버 권위 투사체 회피 · 스키마 12)** 착수. 임시 프리팹 비우기 · 주석 블록 최종 삭제는 S-5 정리(전체 테스트 뒤).

---

## 13. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-23 | S-1 · S-2 결과 — 12절 신설(구현 `9e096e4` · 프로그래머 판단 · 1차 멀티 로그 판정 · 미관측 · 회귀 스위트 미보고), 변경 이력 12 → 13절. O-2 · O-10(싱글 몫) · O-11 · O-12 해결, 현재 상태 줄 갱신 |
| 2026-11-23 | 2판 — 사용자 결정 U-8(O-4 「1단계부터 켬」) · U-9(O-2 「임시 프리팹 넣음」), 메인 기본안 M-11(O-3 분류 토큰 해시 포함) · M-12(O-5 스키마 12 = S-3 같은 커밋) 반영. S-1에 켜기 · 기대 수 14 · `== 13` 6곳 · Z-T9 · 임시 프리팹, S-3에 `== 11` 8곳, S-5는 마무리 · 정리만. 8절 절차 · 5절 파일 표 · 위험 R-13 · R-14 갱신 |
| 2026-11-23 | 첫 판 — 사용자 결정(U-1~U-7) · 메인 세션 기본안(M-1~M-10, 보고 예정) · 조사 설계안(P-1~P-12) · 열린 항목(O-1~O-12) · 다섯 단계(S-1~S-5). 단계 경계에서 생기는 두 물음(「사용 가능」을 켜는 단계 O-4 · 스키마 상수를 올리는 단계 O-5)과 VineTower 프로필 부재(Research 9-3)를 문서 담당이 덧붙였다 |
