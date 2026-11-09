# Plan — 인간 지원 스킬 EMP(2-B 건물 대상 상태 + 탑 무력화 VFX)

## 이 문서는 무엇인가

인간 스킬 건물의 3번 칸 **EMP**를 실제로 쓸 수 있게 만드는 계획이다. 지금 EMP는 「사용 불가」로 꺼져 있다. 이 계획대로 하면 다음처럼 동작한다.

- 플레이어가 지도의 한 지점을 골라 EMP를 쓰면, 그 자리에 **전기 고리 이펙트**(V5)가 퍼진다.
- **쓴 바로 그 순간 원 안에 있던 적 방어탑**(대포 타워 · 룬 첨탑 · 덩굴 타워)이 정해진 시간 동안 **공격을 멈춘다.** 그 탑 위에는 **합선 · 연기 이펙트**(V6)가 계속 보인다. 내 화면과 상대 화면 모두 같은 탑에 보인다.
- 유닛은 아무 영향을 받지 않는다. 이미 날아가던 포탄은 그대로 맞는다. 같은 탑에 다시 쓰면 멈추는 시간이 처음부터 다시 시작된다. 시간이 끝나면 탑은 다시 쏘고, 이펙트는 짧게 사그라진다.

이를 위해 「건물에 상태를 걸어 두는 곳」, 「탑이 쏘기 전 확인하는 관문」, 「탑 위에 계속 붙는 이펙트」를 새로 만든다. 기존 포격 · 다른 스킬 · 탑 공격 연출의 코드는 바꾸지 않고 **덧붙이기만** 한다.

현재 코드 상태와 근거(파일 · 행)는 [Research.md](Research.md)에 있다. 추천안은 game-programmer가 코드를 읽고 낸 것이며, **사용자 결정이 아니다.** 7절의 사용자 확인 Q1~Q12를 정한 뒤, 이 Plan을 사용자가 승인해야 구현을 시작한다(WORKFLOW [4]).

> **현재 상태 (2026-11-09):** **완료 — 구현 4단위 + 스킬 VFX 지면 높이 보정, 사용자 테스트 PASS**(메인 세션의 로그 · 육안 판정을 사용자가 승인 — 「그렇게 진행하고 나중에 문제 있으면 다시 살펴보자」). 미관측 항목 · 남은 사용자 결정은 [11절](#11-implementation-and-user-test). 탑 높이 측정 메뉴는 실행되지 않아 V6는 3종 모두 배율 1 · 보정 0 그대로다. *(2026-11-09 갱신 — 직전 「**구현 승인 — game-programmer 진행 중(커밋 단위별 즉시 커밋).** 사용자 「추천안대로 진행해 보자」(2026-11-04)로 Q1~Q12 추천안을 모두 채택했다 — 7-1 결정 표. 탑 높이 측정은 사용자 결과 대기(그 전엔 배율 1 · 보정 0).」)*

> **🔴 먼저 알릴 사실:** 상위 기획이 정한 「프리셋은 `_autoTowerProjectiles` 구조를 따른다」의 그 칸은 커밋 `d719fa53`에서 **삭제됐다.** 이 계획은 그 칸을 되살리지 않고, **같은 모양(종족 한 줄마다 프리셋)의 새 칸**을 만든다(P1, 3-5). 이렇게 이해해도 되는지 → **Q9**. 근거: [Research 6-3](Research.md#6-3-종족별-프리셋-칸--확정-사항과-어긋난-곳).

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 2절 EMP 행, 5-1 결정 12 · 22(지점 조준 범위 · 일시 타격 · 스냅샷) · 31(다음 = 탑 높이 측정 메뉴 + EMP), 5절 O-22 · O-23(열림 → Q3 · Q11) *(2026-11-04 갱신: 닫힘 — 7-1)*. **기반:** [포격 Task](../../2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md#10-implementation-and-user-test)(스킬 데이터 칸 · 사용 불가 거부 · 발동 표현 동기화 · 스키마 7 · 착탄 예약). **의존:** [탑 크기 측정 메뉴](../../2026-11-02/20_45_tower-bounds-measure-tool/Plan.md)(커밋 `4c2f41c9`, 사용자 측정 결과 대기 — 그 전엔 배율 1 · 보정 0).

**표기:** `[확정]` = 사용자 결정(상위 Plan 5-1) · `[사실]` = Research에서 확인한 것 · `[추천 — 프로그래머]` = game-programmer 추천(결정 아님) · `[제안]` = 결정 아님 · `[사용자 확인]` = 7절에서 사용자가 정할 것. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`.

> **기존 로직 제거 — WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 최상단에 둔다.**
>
> **코드 제거 · 주석 처리는 없다.** 모두 덧붙이기다(Research 8절 「회귀 영향 범위」).
>
> | 바뀌는 것 | 방식 | 안전 근거 |
> |---|---|---|
> | EMP 자산 `Skill_Human_Emp.asset`의 자리표시 값(타입 A · 피해 150 · 사용 불가) | **값 교체** — 새 타입 · 피해 0 · 지속 시간 · 발동 VFX · 사용 가능. **GUID 유지** | 로드아웃 참조가 그대로라 다른 자산은 바뀌지 않는다. 자리표시 값은 이 Plan과 `SupportSkillLoadoutSetup.cs` 200행에 남아 있어 되돌릴 수 있다 |
> | 스킬 패널의 EMP 임시 라벨 「폭탄」 | 코드를 지우지 않는다. 새 타입이 `DeriveSkillLabel`의 `default` 줄로 가서 **「?」** 로 보이게 될 뿐이다 | 라벨은 추후 UI 작업 범위(상위 결정 16). 한 줄 추가 여부 → Q8 |
> | (Q10 승인 시에만) 설정 도구의 EMP 줄 | **값 교체** — 도구를 다시 실행해도 EMP가 자리표시로 되돌아가지 않게 | 도구 퇴역 여부는 별도 확인 |

---

## 0. 근거로 읽은 규칙

WORKFLOW [4]에 따라 `Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고 스킬 · 건물 · 사운드 · 전투 동기화 행의 원본을 열었다. 아래 각 수정 항목의 「근거」 칸은 이 규칙을 가리킨다. 「스킬 규칙 N」 = `GameSystemRules/GameSystemRules_Skills.md` 규칙 N, 「타워 규칙 N」 = `GameSystemRules/GameSystemRules_Buildings.md` **「방어 타워 시스템」** 절 규칙 N(건물 문서는 절마다 번호가 다시 시작한다).

| 규칙 | 이 계획과의 관계 |
|---|---|
| 스킬 규칙 3 「건물별 글로벌 쿨다운」 | EMP 시전 쿨다운은 발동 순간 시작(현행 유지) |
| 스킬 규칙 7 「데이터 주도 설계」 | 새 실행기 하나만 코드, 반경 · 지속 · 발동 VFX는 자산. **요소 표 「타입 — 3종 중 하나」는 구현 뒤 개정 필요 → 8절** |
| 스킬 「스킬 메커니즘 타입」 머리 · 규칙 11~13 | 「3종 타입」 → 새 타입 추가. **구현 뒤 개정 필요(상위 R-4) → 8절** |
| 스킬 규칙 14 「유틸리티 타입 제외」 | EMP가 유틸리티로 읽히지 않게 새 타입으로 분류(상위 R-7 → 8절) |
| 스킬 규칙 16 「지점 지정 발동」 | 새 타입도 지점 지정 경로. 「타입 A · B」 문구 개정 필요(상위 R-8 → 8절) |
| 스킬 규칙 25 「서버에서만 판정·실행」 | 대상 스냅샷 · 관문 · 만료는 서버 권위 틱에서만. V5 · V6는 각 화면 로컬. 「EMP 대상은 발동 순간 스냅샷」 문장 추가(상위 R-11 → 8절) |
| 스킬 규칙 26 「좌표만 전송 + 서버 재검증」 | 요청 RPC(클라 → 서버)는 바꾸지 않는다 |
| 스킬 「멀티플레이 / 서버 권위」 머리 문장 | 새 RPC는 Infrastructure의 `NetworkSkillController`에, 이름은 `…ClientRpc` |
| 타워 규칙 1 「타겟 대상」 | 탑의 대상 규칙은 바꾸지 않는다 |
| 타워 규칙 5 「공격 후 쿨다운」 | 무력화 중 쿨다운 처리(Q2). K1은 문장 그대로 지켜지고, K2면 「무력화 중 쿨다운 정지」를 덧붙여야 한다 |
| 타워 규칙 7 「파괴 시 공격 즉시 중단」 | 무력화 중 탑 파괴 → 상태 제거 · V6 즉시 정지(Q4) |
| 타워 규칙 9 「서버 권위 처리」 | 관문은 서버 Tick 안. 클라는 V6 표현만 |
| 타워 규칙 11 「Human CannonTower 배치 시 초기 회전」 | V6 위치 보정은 Y만(상대 대포탑 180° 회전) |
| 타워 규칙 12 「타워 발사 연출」 | 공격 프로필(`_towerAttackProfiles`)은 바꾸지 않는다 — 무력화 프리셋은 별도 칸(P1). 공격이 막히면 발사 연출 · SFX는 자동으로 안 나온다 |
| `GameSystemRules_Sound.md` 규칙 15 「VFX+SFX 쌍 호출 규칙」 | EMP SFX를 넣는다면 V5 재생 바로 아래(Q7) |
| `GameSystemRules_UnitCombatSynchronization.md` 「10. 현재 구조에서의 전환 규칙」 5항 | 새 RPC → 스키마 7 → 8(Q6) |
| **「무력화」를 정의한 규칙 — 없음** | 「방어 타워 시스템」 절에 새 규칙이 필요하다. **이번 작업에서는 규칙 문서를 고치지 않고, 구현 · 사용자 테스트 뒤 반영한다 → 8절** |

---

## 1. 추천안 한눈에 `[추천 — 프로그래머]`

| 결정할 것 | 추천 | 다른 안 | 자세히 |
|---|---|---|---|
| 실행 방식 | **M1** — 전용 새 타입 `AreaTowerDisable = 3` + 새 실행기 + 컨텍스트 다리 1개 | M2 일반형 「지점 범위 건물 상태」 타입 / M3 타입 A 자리표시에 「사용 가능」만 켬(150 피해 — 불가) | 3-1 · Q12 |
| 상태 저장 위치 | **B1** — 새 Application 서비스 `BuildingStatusEffectSystem`(건물 Id 키) | B2 `BuildingData`에 남은 시간 필드 / B3 `TowerCombatUseCase` 안 딕셔너리 | 3-2 · Q12 |
| 무력화 중 탑 쿨다운 | **K1** — 계속 줄어든다(관문은 쿨다운 감소 뒤 · 탐색 앞) | K2 멈춘다 / K3 해제 순간 가득 채움 | 3-3 · Q2 |
| 클라 알림 | **N1** — 대상 탑 목록 RPC 1개(종료 RPC 없음, 화면이 받은 지속 시간으로 끝냄) | N2 + 종료 RPC / N3 발동 표현 RPC에 목록 추가 / N4 탑마다 RPC | 3-4 · Q12 |
| 스키마 | **S8** — 7 → 8 | S7 7 유지 | 3-4 · Q6 |
| 종족별 V6 프리셋 칸 | **P1** — `BuildingEffectConfig`에 새 종족 행 목록 `_towerDisabledPresets` | P2 `TowerAttackProfile`에 칸 추가(비추천) / P3 스킬 자산에 종족별 칸 | 3-5 · Q9 |

---

## 2. 변경 파일 목록 (추천안 기준)

레이어 방향은 Domain → Application → Core → Infrastructure → Presentation → Bootstrap을 지킨다. **Domain → Core 참조 없음. `IGameServices`에 메서드를 추가하지 않는다**(서버 · 클라 이벤트는 정적 `GameEvents`로 — C-1 선례, fixture 대역 구현 보호) `[추천 — 프로그래머]`.

| # | 파일 | 변경 | 레이어 | 근거 규칙 |
|---|---|---|---|---|
| 1 | `Scripts/Domain/Skill/SkillMechanicType.cs` | 값 `AreaTowerDisable = 3`을 **맨 뒤에** 추가(기존 자산 직렬화 안전) | Domain | 스킬 규칙 7 · 「스킬 메커니즘 타입」 머리 |
| 2 | 새 `Scripts/Application/Skill/AreaTowerDisableExecutor.cs` + `SkillExecutorRegistry.cs` 등록 1줄 | 새 실행기 — 조준점 · 반경 · 지속을 컨텍스트 다리로 넘긴다 | Application | 스킬 규칙 7 · 25 |
| 3 | `Scripts/Application/Skill/SkillActivationContext.cs` | 「범위 안 적 방어탑 무력화」 다리 델리게이트 + 메서드 1개 | Application | 스킬 규칙 25 |
| 4 | `Scripts/Application/UseCases/SkillActivationUseCase.cs` | 컨텍스트 생성(164행)에 새 다리 연결, 서비스 주입 | Application | 스킬 규칙 25 |
| 5 | 새 `Scripts/Application/Services/BuildingStatusEffectSystem.cs` | 건물 Id 키 상태 · `Apply` · `CanAttack` · 권위 틱 · `Remove` · `Close` · 확정 이벤트 발행 | Application | 스킬 규칙 25 · 타워 규칙 7 · 9 |
| 6 | `Scripts/Application/UseCases/TowerCombatUseCase.cs` | 설정자 `SetBuildingStatusSystem` + 156행과 159행 사이 관문 1줄 | Application | 타워 규칙 5 · 9 |
| 7 | `Scripts/Application/Events/GameEvents.cs`(+ 값 객체 새 파일 가능) | 확정 · 수신 채널 2개 | Application | 스킬 규칙 25 |
| 8 | `Scripts/Bootstrap/GameBootstrapper.Setup.cs` · `GameBootstrapper.cs` · `GameBootstrapper.Map.cs` | 서비스 생성 · 주입, 싱글 권위 틱(탑 틱 752행 바로 앞), `OnGameEnd` 닫기, 화면 구독자 AddComponent + Initialize | Bootstrap | 스킬 규칙 25 |
| 9 | `Scripts/Infrastructure/Network/NetworkCombatController.cs` | 멀티 권위 틱(`tower?.Tick` 729행 바로 앞) | Infrastructure | 타워 규칙 9 · 스킬 규칙 25 |
| 10 | `Scripts/Infrastructure/Network/NetworkSkillController.cs` | 확정 구독 → `TowersDisabledClientRpc` → 클라(`IsServer` 가드 뒤) 수신 발행 | Infrastructure | 스킬 「멀티플레이 / 서버 권위」 머리 · 25 |
| 11 | `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` + `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs` 259행 | **(Q6 승인 시)** 스키마 7 → 8, 선례 형식 주석(「EMP 탑 무력화 RPC 신설」), fixture `== 8` | Infrastructure / Editor | 전투 동기화 「10. 현재 구조에서의 전환 규칙」 5항 |
| 12 | 새 `Scripts/Presentation/Effects/VfxPoolItem.StatusLoop.cs` · `EffectManager.cs` · 새 `Scripts/Presentation/Effects/TowerDisableVfxPresenter.cs` | 지속 VFX 시작 · 정지(lease) · 화면 구독 | Presentation | 스킬 규칙 25 · 타워 규칙 7 · 11 |
| 13 | `Scripts/Presentation/Effects/BuildingEffectConfig.cs` + 새 구조체 | `_towerDisabledPresets` 목록 · 조회 메서드 | Presentation | 타워 규칙 12(공격 프로필과 분리) |
| 14 | 자산: `BuildingEffectConfig.asset` 3행 · 새 `EffectPreset_EmpDisabled_{Human,Spirit,Transcendence}.asset` · `Skill_Human_Emp.asset` | 에디터 스크립트로 생성 · 입력, 사용 후 정리(WORKFLOW [5-2]) | Data | 스킬 규칙 7 |
| 15 | (Q10 승인 시) `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` 200행 | EMP 줄 값을 새 값으로 | Editor | — |

- **바꾸지 않는 것** `[사실]`: `TowerAttackProfile` · `_towerAttackProfiles`, `ExecuteTowerAttack`의 이름 · 매개변수(Editor 리플렉션), `TowerCombatUseCase` 생성자, `SkillActivatedClientRpc` 모양, 요청 RPC, 유닛 상태 시스템, 포격 착탄 예약.

---

## 3. 항목별 방법

### 3-1. 실행 방식 — M1 `[추천 — 프로그래머]`

- 새 값 `SkillMechanicType.AreaTowerDisable = 3`, 새 실행기 `AreaTowerDisableExecutor`, 컨텍스트에 다리 1개. 레지스트리 설계 의도(「신규 타입 = 실행기 클래스 추가 + 등록 1줄」)를 그대로 따른다. 기존 A · B · C 경로 무변경.
- 컨텍스트 생성자 인자가 1개 늘지만 호출처는 `SkillActivationUseCase.cs` 164행 한 곳이고 Editor에는 없다 `[사실]`.
- M2(일반형)를 권하지 않는 이유: 다른 2-B 스킬(신목의 각성 건물 쉴드 · 재생의 뿌리 건물 재생)은 **맵 전체 아군**이라 「지점 범위 적 건물」 틀과 맞지 않아 일반화 이득이 거의 없다. 저장소(B1)는 그 스킬들도 다시 쓸 수 있다.

### 3-2. 건물 상태 저장 · 지속 · 갱신 · 만료 — B1 `[추천 — 프로그래머]`

새 Application 서비스 `BuildingStatusEffectSystem`(순수 C#). 건물 Id 키 딕셔너리라 유닛 Id와 겹치지 않는다. 값은 기존 Domain `StatusEffect` + `AttackDisabled` 종류를 재사용하고, EMP는 종류가 하나뿐이라 서비스 안에서 `StatusEffect` 1개만 들어도 충분하다(별도 `BuildingStatusState` 불필요) `[제안 — 프로그래머]`.

| 항목 | 방법 | 근거 |
|---|---|---|
| 대상 스냅샷 시점 | 실행기가 불리는 **발동 순간**(착탄 지연 0) | 결정 22 「발동 순간 범위 안」 `[확정]`, EMP 자산 `_impactDelay: 0` `[사실]`. 「고리가 탑에 닿은 뒤」를 원하면 → Q3 |
| 대상 판정 | 적 팀 · 살아 있는 · `BuildingType.AutoTower`만, **타일 중심 ↔ 조준점 거리 ≤ 반경** | 타입 A 건물 판정과 같은 기준 → 조준 원과 결과가 맞는다 |
| 다시 걸림 | 같은 탑이면 **남은 시간을 새 지속 시간으로 교체**. 다른 탑은 각자 독립 | `UnitStatusState.ApplyOrRefresh` 선례. 스킬이 하나라 「교체」 = 「최대값」과 결과가 같다 |
| 만료 틱 | **권위 전용** — 싱글은 `GameBootstrapper` 탑 틱 바로 앞, 멀티는 `TickCombat`의 `tower?.Tick` 바로 앞 | 두 모드에서 「만료 → 관문 판정」 순서가 같아진다(위험 R-12). 클라 미러 틱 묶음에는 얹지 않는다(스킬 규칙 25 착탄 지연 항목과 같은 이유) |
| 탑 파괴 · 철거 | `OnBuildingDied` 구독으로 그 Id 제거 | 누수 방지(파괴된 탑은 관문 전에 이미 건너뛴다) |
| 경기 종료 | `OnGameEnd` → `Close()`(전부 비우고 이후 `Apply` 거부, 여러 번 불러도 안전) | `ClosePendingImpacts` 선례. 싱글 결과 화면 뒤 탑 재사격은 기존 동작 그대로(R-8) → Q5 |
| 맵 재로드 | Setup에서 서비스를 새로 만든다 | 상태 시스템 선례 |

- 대안(절대 만료 시각 저장)은 틱 위치 문제가 없지만 스킬 코드의 기존 방식(남은 시간 감소)과 달라져 추천하지 않는다.

### 3-3. 탑 공격 관문 · 쿨다운 — K1 `[추천 — 프로그래머]` · Q2

- `TowerCombatUseCase.Tick`의 **156행(쿨다운 남으면 continue) 뒤, 159행(적 탐색) 앞**에 `if (!CanAttack(tower.Id)) continue;` 한 줄.
- 무력화 중에도 쿨다운은 **계속 줄어든다** → 풀리는 순간 쿨다운이 0이면 사거리 안 적에게 바로 쏜다. 유닛 빙결 · 기절 선례와 같고 타워 규칙 5 「쿨다운 중에도 탐색 계속」 문장과 충돌하지 않는다.
- 주입은 **설정자**(`SetBuildingStatusSystem`). 생성자를 바꾸면 회귀 fixture 2곳이 깨진다. 미주입(null)이면 관문 통과 → fixture 결과 무변경.
- **이미 날아가는 포탄은 추가 코드 없이 유지된다** — 관문이 발사 결정에만 있고, 착탄 피해는 탑 상태를 보지 않는다(Research 4-3). 정령 · 초월 탑은 즉발이라 해당 없음.
- K2(쿨다운 정지)는 무력화가 최대 쿨다운 1회분 더 세지고, 타워 규칙 5에 문장을 덧붙여야 한다. K3(해제 때 가득 채움)는 밸런싱 성격이며 해제 순간을 알아야 해서 코드가 늘어난다.

### 3-4. 멀티 동기화 — N1 · S8 `[추천 — 프로그래머]` · Q6 · Q12

- **서버:** 무력화 적용 직후 Application 이벤트 `OnTowersDisabledConfirmed`(시전 팀 · 발동 순번 · 지속 · 탑 Id 목록) 발행 → `NetworkSkillController`가 구독해 `TowersDisabledClientRpc(int sequence, float duration, int[] towerIds)` 1회.
- **클라:** `IsServer` 가드 뒤 `OnTowersDisabledReceived` 발행. **종료 RPC는 없다** — 화면이 받은 지속 시간으로 끝낸다.
- C-1과 같은 「확정 / 수신」 두 채널이라 화면 구독자 하나가 싱글 · Host · Client를 모두 처리한다. 다시 걸리면 같은 RPC가 또 온다. 발동 표현 RPC 모양은 바꾸지 않는다.
- **클라에 상태 미러를 두지 않는다** — 클라가 무력화를 알아야 하는 곳은 화면 V6뿐이다. 화면 구독자가 탑별 종료 시각을 든다(C-1 `SkillActivationVfxPresenter` 선례). 「무력화 중」 UI가 생기면 그때 미러를 둔다.
- **늦은 접속:** 타입 C 상태 동기화와 같이 스냅샷 없음(지속 수 초) `[제안 — 프로그래머]`.
- **스키마 S8:** 7 → 8. 선례(5 → 6 「새 RPC가 생겼다」)와 같고, 사용자 기기에 이미 깔린 C-1 테스트 빌드(스키마 7, EMP RPC 없음)와 섞인 경기를 접속 단계에서 확실히 거절한다. 상수 1 · fixture 1 · 주석.
- 알려진 한계(R-7): 클라는 전송 지연만큼 늦게 시작 · 늦게 끝난다(길이는 같다). 거슬리면 N2(종료 RPC)로 보완.

### 3-5. 화면 — V5 · V6 · 종족별 프리셋 P1

**V5(발동 고리):** 기존 경로 그대로 — EMP 자산의 발동 VFX 칸에 `vfx_emp_pulse`를 넣으면 조준점에 재생된다(C-1). 기준 반경은 자산의 2를 유지.

**V6(탑 무력화) 구성** `[추천 — 프로그래머]`:

| # | 파일 | 할 일 |
|---|---|---|
| a | 새 partial `VfxPoolItem.StatusLoop.cs` | `BeginStatusLoop()` → lease 번호 발급. `StopStatusLoop(lease, clear)` — `clear = false`면 방출만 멈춤(꼬리 ≤ 0.8초 뒤 기존 Update가 자동 반환), `clear = true`면 입자까지 지우고 반환. **lease를 확인해** 이미 반환 · 재사용된 항목을 잘못 멈추지 않는다(힐 선례) |
| b | `EffectManager`에 `StartTowerStatusVfx(RaceId, Vector3 viewPos)` · `StopTowerStatusVfx(item, lease, immediate)` | 프리셋 조회(P1) → 풀에서 꺼냄 → 기존 재생 → lease 시작 |
| c | 새 화면 구독자 `TowerDisableVfxPresenter` | 「확정 / 수신」 두 채널 구독. 탑 Id → (항목, lease, 종료 시각). 새 알림 = 없으면 시작 · 있으면 **종료 시각만 갱신(재시작 안 함 — 끊김 없음)**. 종료 시각 도달 → 꼬리 정지. `OnBuildingDied` → 즉시 정지(Q4). `OnGameEnd` · 재초기화 · 파괴 → 전부 정지(Q5). 붙이는 자리 = `GameBootstrapper.Map.cs` 271~277행 옆 |
| d | 위치 | **탑 GO의 위치**(이미 화면 좌표) + 프리셋 높이 보정, 회전 항등. 부모 = 기존 VFX 컨테이너(**탑의 자식으로 붙이지 않는다** — 탑 파괴 때 같이 Destroy됨). GO가 없으면 타일 좌표를 화면 좌표로 바꿔 대신 쓴다 |

**종족별 프리셋 칸 P1** `[추천 — 프로그래머]` · Q9:

- `BuildingEffectConfig`에 새 목록 `_towerDisabledPresets : List<{ RaceId race; EffectPreset preset; }>` — 삭제된 `_autoTowerProjectiles`와 같은 「종족 행」 모양, 초월이라고 비우는 분기 없음.
- 프리셋 = `EffectPreset` 자산 3개(같은 `vfx_emp_disabled` 프리팹, 종족마다 **Scale Multiplier · Local Position Offset**). 두 칸은 `EffectPreset`에 이미 있다.
- **값:** 측정 메뉴 결과 전에는 **3종 모두 배율 1 · 보정 0**. 측정 결과가 오면 값만 바꾼다(코드 무변경). 제작 기준은 높이 1.0 · 발밑 링 반경 0.5.
- **보정은 Y(높이)만 쓴다** — 상대 인간 대포탑은 180° 회전돼 있어 X · Z 보정이 모델과 어긋난다(타워 규칙 11). X · Z가 꼭 필요하면 탑 회전을 보정에 곱하는 처리가 추가로 필요하다.
- 탑 종족 해석은 조합 루트가 `Func<TeamId, RaceId>`를 주입한다(Presentation이 Infrastructure를 직접 부르지 않게).
- P2(`TowerAttackProfile`에 칸 추가)를 권하지 않는 이유: VineTower 행이 없는 것이 「공격 연출 없음」의 뜻이라(타워 규칙 12), 무력화 칸을 넣으려고 행을 만들면 공격 연출 판정과 얽힌다.

**탑 공격 연출은 따로 멈추지 않는다** `[사실]`: 관문이 공격을 막으면 번개 · 발사 · 탑 SFX 신호가 아예 나오지 않고, 탑 프리팹에는 멈출 움직임이 없다(Research 6-5).

**SFX:** 스킬 SFX 경로가 아직 없다 → Q7. 넣는다면 V5 재생 바로 아래(사운드 규칙 15).

---

## 4. 커밋 분할 — 4단위 `[추천 — 프로그래머]`

| 커밋 | 내용 | 단독 확인 |
|---|---|---|
| 1 | Domain / Application: `AreaTowerDisable = 3` · 실행기 · 컨텍스트 다리 · `BuildingStatusEffectSystem` · 탑 관문(설정자) · 권위 틱 2곳 · `OnBuildingDied` / `OnGameEnd` 정리 · 조합 루트 배선. **EMP 자산은 아직 사용 불가** | 회귀 스위트(관문 미주입 경로 무변경) |
| 2 | 동기화: 확정 / 수신 이벤트 · `TowersDisabledClientRpc` · 스키마(Q6) · fixture | 회귀 스위트 |
| 3 | 화면: `VfxPoolItem.StatusLoop` · `EffectManager` 메서드 · `TowerDisableVfxPresenter` · `BuildingEffectConfig` 새 목록 + `EffectPreset` 3개(에디터 스크립트) | Editor 컴파일 |
| 4 | 자산: EMP `_mechanic = 3` · `_isAvailable = 1` · `_duration`(Q1) · `_totalDamage = 0` · `_activationVfxPrefab = vfx_emp_pulse` (+ 설정 도구 EMP 줄 — Q10) | 5절 시나리오 1~11 |

- 커밋 4 전까지 EMP는 사용 불가로 남아 게임 동작이 바뀌지 않는다.
- 자산 · 프리셋 생성은 WORKFLOW [5-2] 에디터 1회성 스크립트 → 사용자 실행 → 확인 → 정리 절차를 따른다.

---

## 5. 테스트 시나리오

> **🔴 멀티 테스트가 필요하다.** 싱글의 AI는 방어탑을 짓지 않고(AI 시나리오 3종 모두 AutoTower 단계 0건) 스킬도 쓰지 않는다(Research R-1). 따라서 **적 탑이 멈추는지 · V6 · 갱신 · 파괴 · 종족별 위치는 멀티(Host / Client)에서만 시험할 수 있다.** 싱글은 시나리오 1만 본다.

| # | 환경 | 시나리오 | 기대 |
|---|---|---|---|
| 1 | 싱글 | EMP를 빈 땅에 사용 | V5 고리 1.2초, 쿨다운 시작, 피해 없음 |
| 2 | 멀티 Host(파랑, 인간) / Client(빨강) | 적 탑 2개 중 1개만 원 안에 두고 EMP | 원 안 탑만 공격 중지 + V6, 원 밖 탑은 계속 공격. **양쪽 화면 모두** 같은 탑에 V6. 탑 모델 가장자리만 원 안이면 걸리지 않음(타일 중심 기준, R-13) |
| 3 | 멀티 | 적 유닛이 원 안에 있는 상태로 EMP | 유닛은 이동 · 공격 그대로 |
| 4 | 멀티 | 적 대포탑이 포탄을 쏜 직후 EMP | 날던 포탄은 맞고 피해 숫자 표시, 다음 발사 없음 |
| 5 | 멀티 | 무력화 끝 | 지정 시간 뒤 탑이 다시 공격, V6는 0.8초 이내 꼬리 뒤 사라짐(K1이면 사거리 안 적에게 바로 발사) |
| 6 | 멀티 | 무력화 중 같은 탑에 다시 EMP(두 번째 스킬 건물 또는 쿨다운 뒤) | 무력화 시간이 새로 시작, V6 끊김 없음 |
| 7 | 멀티 | 무력화 중 탑 파괴 / 상대가 철거 | V6 사라짐(Q4), 오류 없음 |
| 8 | 멀티 | 상대 종족 3종(인간 · 정령 · 초월) 탑 각각 | 탑마다 V6 위치 · 크기 맞음, 빨강 화면에서도 같은 자리(측정 전에는 배율 1 · 보정 0 기준) |
| 9 | 멀티 | 무력화 중 경기 종료 | 오류 없음, V6 정지(Q5) |
| 10 | 멀티 | 내 탑(아군) 위에 EMP | 아군 탑은 영향 없음 |
| 11 | Editor | `Hexiege/Combat/Run Full Regression Suite` | 실패 없음 |

- 측정 메뉴 결과로 종족별 값을 넣은 뒤에는 시나리오 8을 다시 본다.
- 클라에서 경기 종료 이벤트가 실제로 나는지는 확인하지 않았다(Research 9절) — 시나리오 9의 Client 화면에서 함께 본다.

---

## 6. 위험

전체 표는 [Research 8절](Research.md#8-위험--회귀-영향). 이 계획의 대응만 다시 적는다.

| # | 위험 | 대응 |
|---|---|---|
| R-1 | 싱글에서 적 탑 시험 불가 | 5절 멀티 시나리오 2~10 |
| R-2 | 회귀 fixture | 생성자 무변경(설정자), 스키마를 올리면 fixture 1줄 동시 수정 |
| R-4 · R-5 | 풀 항목 오정지 · 탑 자식으로 붙임 | lease 확인, 자식으로 붙이지 않음(3-5) |
| R-6 | 상대 대포탑 회전 | 보정은 Y만(3-5) |
| R-7 | 클라 종료 시각 오차 | N1 허용, 거슬리면 N2 |
| R-9 | 설정 도구 재실행 시 EMP가 자리표시로 되돌아감 | Q10 |
| R-11 | 측정 결과 의존 | 배율 1 · 보정 0으로 시작, 측정 뒤 값만 교체 |
| R-12 | 같은 틱 순서 | 만료 틱을 두 모드 모두 탑 틱 바로 앞에 |
| — | NGO 두 RPC의 도착 순서 `[추정]` · 구버전 클라의 모르는 RPC 처리 `[미확인]` | S8로 섞인 빌드를 접속 단계에서 거절. 순서는 시나리오 2에서 관찰 |

---

## 7. 사용자 확인 질문

| # | 질문 | 추천 | 관련 |
|---|---|---|---|
| Q1 | 무력화 시간(시험용 임시 값) — 밸런싱 항목이지만 시험하려면 값이 필요하다 | 예: **5초**(임시, 밸런싱에서 확정) | 상위 Plan 3-1 「무력화 시간」 |
| Q2 | 무력화 중 탑 공격 쿨다운 | **K1 계속 줄어듦**(유닛 빙결 · 기절 선례) / K2 멈춤 / K3 해제 때 가득 채움 | 3-3 · 상위 4절 14 |
| Q3 | (O-22) V5 고리와 V6 탑 무력화를 **동시에 시작**하는가 — 「고리가 탑에 닿은 뒤」로 하려면 착탄 지연(예약)을 써야 한다 | **동시**(지연 0, 기획안 추천) | 3-2 · 상위 O-22 |
| Q4 | 무력화 중 탑이 파괴 · 철거되면 V6를 입자까지 즉시 지우는가, 꼬리 0.8초를 남기는가 | **즉시 지움**(탑이 사라지는데 스파크만 남지 않게) | 3-5 c · 타워 규칙 7 |
| Q5 | 경기가 끝날 때 남은 무력화 · V6 처리 | **상태 비움 + V6 꼬리 정지**(싱글 결과 화면 뒤 탑 재사격은 기존 동작 그대로) | 3-2 · R-8 |
| Q6 | 스키마 7 → 8로 올리는가(C-1 테스트 빌드를 기기에 설치한 적이 있는가) | **8로 올림** | 3-4 |
| Q7 | EMP SFX — 이번엔 없음 / 효과음을 받아 추가 | **없음**(포격과 같음) | 3-5 · 사운드 규칙 15 |
| Q8 | EMP 칸 임시 라벨 「?」 그대로 / `"EMP"` 한 줄 추가 | **그대로**(추후 UI 범위 — 상위 결정 16 존중) | 최상단 표 · Research 3-2 |
| Q9 | 프리셋 칸 = 삭제된 `_autoTowerProjectiles`와 같은 「종족 행 목록」 모양의 **새 칸**으로 이해해도 되는가(삭제된 칸을 되살리는 것이 아님) | **예(P1)** | 최상단 알림 · 3-5 · Research 6-3 |
| Q10 | 일회용 설정 도구 `SupportSkillLoadoutSetup.cs`의 EMP 줄을 새 값으로 같이 고칠지, 도구를 퇴역할지 | **이번엔 EMP 줄만 새 값으로**(재실행해도 되돌아가지 않게) — 퇴역은 별도 확인 | R-9 |
| Q11 | (O-23) 탑이 다시 가동될 때의 연출을 제외하는가 | **제외**(기획안 추천) — 코드상 V6 정지만 | 상위 O-23 |
| Q12 | 저장 위치 B1(새 건물 상태 서비스) · 타입 M1(전용 새 타입) · 알림 N1 채택 여부 | **B1 · M1 · N1** | 3-1 · 3-2 · 3-4 |

### 7-1. 사용자 결정 (2026-11-04) `[확정]`

사용자 원문: 「추천안대로 진행해 보자」 — 위 질문 표의 추천안을 Q1~Q12 전부 채택했다. 위 질문 표는 결정 전 기록이라 그대로 둔다.

| # | 결정 | 고른 안 | 반영 위치 |
|---|---|---|---|
| Q1 | 무력화 시간 **5초** — 시험용 임시 값, 최종 값은 밸런싱에서 확정 | 5초(임시) | 4절 커밋 4 `_duration` · 9절 |
| Q2 | 무력화 중 탑 공격 쿨다운은 **계속 줄어든다** | K1 | 3-3 |
| Q3 | V5 고리와 V6 탑 무력화를 **동시에 시작**(착탄 지연 0). 상위 O-22 닫힘 | 동시 | 3-2 · 상위 Plan 5절 O-22 |
| Q4 | 무력화 중 탑이 파괴 · 철거되면 V6를 **입자까지 즉시 지운다** | 즉시 지움 | 3-5 c |
| Q5 | 경기 종료 시 **상태를 비우고 V6를 정지**(싱글 결과 화면 뒤 탑 재사격은 기존 동작 그대로) | 상태 비움 + V6 정지 | 3-2 · 3-5 c |
| Q6 | 스키마 **7 → 8** | S8 | 3-4 · 2절 11번 |
| Q7 | EMP SFX **없음** | 없음 | 3-5 |
| Q8 | EMP 칸 임시 라벨 **「?」 그대로** | 그대로 | 최상단 표 |
| Q9 | 프리셋 칸은 삭제된 칸을 되살리지 않고 **같은 「종족 행 목록」 모양의 새 칸**으로 만든다 | P1 | 최상단 알림 · 3-5 |
| Q10 | 설정 도구 `SupportSkillLoadoutSetup.cs`는 **EMP 줄만 새 값으로** 고친다. 도구 퇴역은 지원 스킬 작업 전체가 끝난 뒤 — 기존 [포격 Task](../../2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md#10-implementation-and-user-test) 10-5 결정 그대로 | EMP 줄만 | 2절 15번 · R-9 |
| Q11 | 탑 재가동 연출은 **제외**(코드상 V6 정지만). 상위 O-23 닫힘 | 제외 | 9절 · 상위 Plan 5절 O-23 |
| Q12 | 저장 위치 · 실행 방식 · 클라 알림 | B1 · M1 · N1 | 3-1 · 3-2 · 3-4 |

---

## 8. 구현 뒤 반영할 규칙 문서 `[제안 — 이번 작업에서는 고치지 않는다]`

구현 · 사용자 테스트 통과 뒤, 결정된 내용대로 아래를 사용자 승인을 받아 반영한다(WORKFLOW [11]). 문안은 그때 만든다.

| 문서 · 자리 | 왜 바뀌어야 하나 |
|---|---|
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 — **새 규칙(13~) 「무력화」 신설** | 지금 탑 상태 효과를 정의한 규칙이 없다. 「무력화 중 공격 불가 · 쿨다운 처리(Q2) · 비행 중 투사체 유지 · 파괴 시 해제」 |
| `GameSystemRules_Skills.md` 「스킬 메커니즘 타입」 머리 · 규칙 11~13 뒤 | 「3종 타입」 → 새 타입 추가(상위 R-4) |
| 같은 문서 규칙 7 요소 표 「타입」 행 | 「3종 중 하나」 문구 |
| 같은 문서 규칙 14 | EMP가 유틸리티가 아님을 분류로 명시(상위 R-7) |
| 같은 문서 규칙 16 | 「타입 A · B」 → 새 타입도 지점 지정(상위 R-8) |
| 같은 문서 규칙 25 | 「EMP 대상은 발동 순간 스냅샷, 무력화 만료는 서버 권위 틱」(상위 R-11) |
| `GameSystemRules_UnitCombatSynchronization.md` 등 스키마 값을 적은 문서 | Q6에서 8로 올리면 |

> **2026-11-09 반영:** 구현 · 사용자 테스트 PASS 뒤 사용자 「그렇게 진행하고 나중에 문제 있으면 다시 살펴보자」(문서/메모리 업데이트 진행)로 위 7행을 모두 반영했다 → [11-7](#11-implementation-and-user-test). 위 표 칸은 그대로 둔다.

---

## 9. 하지 않는 것

- 무력화 시간의 최종 값(밸런싱) · 적 건물 피해 비율.
- 스킬 버튼 라벨 · 아이콘 · 설명 탭(상위 결정 16), 「무력화 중」 탑 UI.
- 탑 재가동 연출(Q11에서 제외가 정해지면). *(2026-11-04 갱신: Q11 **제외로 결정** — 7-1)*
- 늦은 접속 스냅샷.
- 다른 2-B 스킬(신목의 각성 · 재생의 뿌리) — 저장소 B1은 재사용 가능하게 두지만 이번에 만들지 않는다.
- 탑 공격 프로필 · 탑 SFX · 포격 · 타입 B · C 경로 수정.
- 규칙 문서 수정(8절은 구현 뒤).

---

## 10. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-04 | 첫 판. 사용자 지시(2026-11-03) 「진행해」(EMP task 문서 작성 질문에 대한 승인)로 작성. game-programmer 읽기 전용 조사의 추천안 M1 · B1 · K1 · N1 · S8 · P1, 변경 파일 15행, 커밋 4단위, 테스트 시나리오 11개, 사용자 확인 Q1~Q12를 옮겼다. 상태: 사용자 확인 Q1~Q12 대기, 구현 승인 전. 코드 · 에셋 · 규칙 문서 · 다른 Task 문서 · 메모리 무변경 |
| 2026-11-04 | 사용자 결정 기록 — 「추천안대로 진행해 보자」: Q1 5초(임시) · Q2 K1 · Q3 동시(지연 0) · Q4 즉시 지움 · Q5 상태 비움 + V6 정지 · Q6 7 → 8 · Q7 SFX 없음 · Q8 「?」 그대로 · Q9 P1 · Q10 EMP 줄만(퇴역은 작업 전체 완료 후 — 포격 Task 10-5) · Q11 제외 · Q12 B1 · M1 · N1 → 7-1 결정 표 추가(질문 표는 원문 유지). 서두 현재 상태 줄 · 9절 재가동 연출 줄 갱신. 상위 Plan 5절 O-22 · O-23 닫힘 · 7-2 남은 미답 메모. **상태: 구현 승인 — game-programmer 진행 중(커밋 단위별 즉시 커밋).** 탑 높이 측정은 사용자 결과 대기(그 전엔 배율 1 · 보정 0). 규칙 문서 무변경. **경위:** 이 기록은 2026-11-04에 처음 적었으나 커밋 전 2026-11-09 작업 환경 초기화로 유실되어 2026-11-09에 다시 작성했다 |
| 2026-11-09 | 구현 · 사용자 테스트 완료 기록(WORKFLOW [7]~[11]) — 사용자 「그렇게 진행하고 나중에 문제 있으면 다시 살펴보자」(메인 세션 판정 승인 + 문서/메모리 업데이트 진행). 서두 현재 상태 줄 갱신, 8절 표 밑 반영 메모, **11절 추가**(커밋 · 사용자 자산 · 계획과 달라진 점 · 테스트 결과 · 로그 보관 · 남은 위험과 결정 · 규칙/상시 문서 반영 · 다음). 같은 날 사용자 「추천대로 해」 결정(진단 로그 `개발` 유지 · 프리셋 메뉴 삭제 · 옛 EMP 주석 줄 최종 삭제 · 포격 착탄 섬광 수정 · 측정 메뉴 유지)을 11-6에 기록 — 메뉴 · 주석 줄 삭제와 착탄 섬광 수정은 `0cd23ea7`(착탄 섬광은 사용자 육안 테스트 대기). 11-1에 `0cd23ea7` 행 추가. 문서 담당은 코드 · 에셋 무변경 |

<a id="11-implementation-and-user-test"></a>
## 11. 구현 결과 · 사용자 테스트 (2026-11-09)

**한 줄 요약:** EMP를 쓰면 이제 조준한 원 안의 적 방어탑이 **5초 동안 공격을 멈추고**, 그 탑 위에 합선 · 연기 이펙트(V6)가 **양쪽 화면 같은 탑에** 보인다. 같은 탑에 다시 쓰면 멈춤 시간이 새로 시작되고 이펙트는 끊기지 않는다. 시험 중 「발사 이펙트가 타일에 절반 잘린다」는 결함이 나와, **모든 스킬 발동 이펙트를 타일 윗면보다 조금 위(0.06)에서 재생하도록** 고쳤다. 메인 세션이 로그와 사용자 육안 보고로 판정을 제안했고 사용자가 승인했다.

> **출처:** 구현 사실은 game-programmer 보고 **[전달]**이며, [확인] 표시는 document-manager가 이 기록 중 작업 트리(HEAD `665174df`)의 파일을 직접 읽어 대조한 것이다. 테스트 · 로그 판정은 메인 세션 전달 **[전달]**이다 — 에이전트(document-manager)가 화면이나 원시 로그를 직접 본 것이 아니다.

### 11-1. 커밋

| 커밋 | 내용 | 확인 |
|---|---|---|
| `aea11d3a` | 단위 1 판정 — `SkillMechanicType.AreaTowerDisable = 3` · `AreaTowerDisableExecutor` + 레지스트리 등록 · 컨텍스트 다리 · 새 서비스 `BuildingStatusEffectSystem`(건물 Id 키, 걸기 · 묻기 · 권위 틱 · 파괴 시 빼기 · 경기 종료 닫기) · `TowerCombatUseCase` 설정자 주입 + 관문 1줄(쿨다운 감소 뒤 · 적 탐색 앞, K1) · 권위 틱 2곳 · 조합 루트 배선 | 관문 `TowerCombatUseCase.cs` 187행, 싱글 틱 `GameBootstrapper.cs` 760~762행(`!IsNetworkMode()`), 멀티 틱 `NetworkCombatController.cs` 735행, 경기 종료 닫기 `GameBootstrapper.cs` 555행 [확인] |
| `28dfc9a8` | 단위 2 동기화 — 값 객체 `TowersDisabledPresentation`(새 파일) · `GameEvents.OnTowersDisabledConfirmed` / `OnTowersDisabledReceived` · `NetworkSkillController.TowersDisabledClientRpc(발동 순번, 지속, 탑 Id 배열)` · 전투 표현 스키마 7 → 8 · fixture `== 8` | 스키마 `UnitAttackShadowProfileResolver.cs` 33행, fixture `RunUnitActionSelfValidation.ServerProjectiles.cs` 260행, RPC 호출 `NetworkSkillController.cs` 317행 [확인] |
| `6c97c6f1` | 단위 3 화면 — `VfxPoolItem.StatusLoop.cs`(lease · 꼬리 정지 / 즉시 정지) · `EffectManager.StartTowerStatusVfx` / `StopTowerStatusVfx`(프리셋의 위치 보정 **Y만** · 배율) · 화면 구독자 `TowerDisableVfxPresenter` · `BuildingEffectConfig._towerDisabledPresets`(P1) | `EffectManager.cs` 591행 · `BuildingEffectConfig.cs` 208행 · `GameBootstrapper.Map.cs` 292~293행 [확인] |
| `d7cce131` | 단위 4 — 1회성 메뉴 새 파일 `Assets/Editor/Tools/EmpTowerDisabledPresetSetup.cs`(`Hexiege/Setup/Skills/Create EMP Tower Disabled Presets` — 프리셋 3개 + 설정 3줄, 재실행해도 높이 · 크기를 되돌리지 않음) · `SupportSkillLoadoutSetup.cs` EMP 줄 새 값(Q10 — 옛 자리표시 줄은 주석 처리로 남김, 사용 가능 기대 수 1 → 2) | 두 파일 머리 주석 · `SupportSkillLoadoutSetup.cs` 213~231행 [확인] *(2026-11-09 갱신 — 직전 「213~231 · 239~240행」. 239~240행의 주석 처리된 옛 줄은 사용자 결정으로 삭제됐고, `EmpTowerDisabledPresetSetup.cs`도 삭제됐다 — 11-6)* |
| `ff879f56` · `654683ce` | WORKFLOW 문서만 — [4] 「테스트 항목 진단 로그 규칙」(사용자 결정 2026-11-09), [6] 「테스트 로그 올리기」 작업명 · 매달 첫 업로드 | `git show --stat` [확인] |
| `0d5d43a3` | **계획 밖 수정** — 스킬 발동 VFX 지면 높이 보정 `GameConfig.SkillVfxGroundYOffset`(기본 0.06), `vfx_emp_pulse` 프리팹 2곳, 진단 로그 `[EMP-DIAG]`(`Diag=EmpTowerDisable`) · `[SKILL-VFX]`(`Diag=SkillVfx`), `DiagRev=1` — 11-3 | `GameConfig.cs` 202~222행 · `SkillActivationVfxPresenter.cs` 33 · 192행 · `GameBootstrapper.Map.cs` 282행 · 프리팹 diff [확인] |
| `665174df` | **사용자 커밋** — 메뉴 실행 결과 자산(아래 11-2)과 새 코드 · 폴더의 `.meta` | `git show --stat` [확인] |
| `0cd23ea7` | **사용자 결정 뒤 후속(11-6)** — 포격 프리팹 `vfx_flightfacility_bombardment` 착탄층 높이(`Impact_Flash` 방출 모양 켜기 · Sphere 반경 0 · 위치 Y 0.35, `Fireball` · `Smoke` 서브 이미터 방출 모양 위치 Y 0.45) · `EmpTowerDisabledPresetSetup.cs`(+ `.meta`) 삭제 · `SupportSkillLoadoutSetup.cs` 옛 EMP 주석 3줄 삭제 | `git show --stat` · 프리팹 diff 6줄 [확인] |

### 11-2. 사용자 자산 (`665174df`) `[확인]`

- `Skill_Human_Emp.asset`(GUID 유지): 타입 3 · 사용 가능 · 쿨다운 10 · 반경 2 · **지속 5**(Q1 임시 값) · 피해 0 · 착탄 지연 0 · 발동 VFX = `vfx_emp_pulse`(GUID `e246a9e1…`) · 기준 반경 2. 사용 가능 스킬은 이제 포격 지원 · EMP 2개다.
- `BuildingEffectConfig.asset` `_towerDisabledPresets` 3줄: race 0 / 1 / 2 → `EffectPreset_EmpDisabled_Human` / `_Spirit` / `_Transcendence`(GUID 대조 일치). 세 프리셋 모두 `vfx_emp_disabled`(GUID `32e68fa5…`) · 위치 보정 (0, 0, 0) · 배율 1 — **탑 높이 측정 전 값 그대로**(R-11).
- 두 VFX 프리팹을 참조하는 자산은 위 4개뿐이다(GUID 검색).

### 11-3. 계획과 달라진 점

| # | 계획 | 실제 | 영향 |
|---|---|---|---|
| 1 | 4절 — 프리셋 3개는 커밋 3에서 에디터 스크립트로, 자산 값은 커밋 4에서 | 프리셋은 **별도 1회성 메뉴**(`EmpTowerDisabledPresetSetup.cs`, 커밋 4)로 만들었다. `SupportSkillLoadoutSetup`에 합치면 재실행마다 나중에 맞출 탑 높이 · 크기가 되돌아갈 위험이 있어서다(그 파일 머리 주석). 자산은 사용자가 메뉴를 실행해 커밋했다(`665174df`) | 메뉴는 실행 · 확인 뒤 사용자 결정으로 **삭제됨**(11-6) *(2026-11-09 갱신 — 직전 「메뉴 파일 1개가 더 남아 있다 — 삭제는 승인 대기」)* |
| 2 | 계획에 없음 | **스킬 발동 VFX 지면 높이 보정**(`0d5d43a3`). 조준 좌표는 Y = 0인데 타일 윗면은 Y = 0.05(불투명 · 깊이 기록)라 바닥에 눕는 고리 · 빛은 통째로, 카메라를 보는 섬광은 아래 절반이 가려졌다(`GameConfig.cs` 주석). 모든 스킬 발동 VFX 루트에 0.06(= 윗면 0.05 + 여유 0.01)을 더한다 — 포격 VFX도 함께 올라간다. `vfx_emp_pulse`는 `Core_Flash` 위치 Y 0.05 → 0.35, `Static_Sparks` 방출 모양 위치 Y 0.05 → 0.1 | 화면 전용(판정 · 피해 · 동기화 무관). VFXSFXGuide 「코드 계약」 10번의 「오프셋 칸이 없다」가 거짓이 되어 고쳤다(11-7) |
| 3 | 5절 — 로그 확인 언급 없음 | WORKFLOW [4] 새 규칙(사용자 결정 2026-11-09)으로 **테스트 항목마다 진단 로그**를 넣었다 — 상태가 바뀌는 순간에만 1줄, 탑 관문 차단은 매 틱이라 횟수만 센다 | 존속 `개발`로 유지 — 사용자 결정(11-6) |
| 4 | 2절 7번 「값 객체 새 파일 가능」 | `TowersDisabledPresentation.cs` 새 파일. 「수신」 채널은 RPC에 팀이 없어 시전 팀이 Neutral이므로, 화면은 종족 · 위치를 **탑 자신의 팀 · 위치**로 정한다(`TowerDisableVfxPresenter.cs` 머리 주석) | 없음 |

### 11-4. 사용자 테스트 · 로그 판정

**판정: PASS** — 메인 세션의 판정 제안(로그 · 육안)을 사용자가 승인했다(원문 「그렇게 진행하고 나중에 문제 있으면 다시 살펴보자」). 아래 「미관측」 · 「전달된 판정에 없음」 항목은 이 PASS의 범위 밖이다.

첫 시험(사용자 원문): 「타워에 적용되는 EMP 효과는 적절하게 적용되는 것 같으나 발사 이펙트는 타일에 절반이 잘리는 느낌」 → 원인 = 스킬 VFX 루트 Y 0 < 타일 윗면 0.05 → `0d5d43a3`로 수정한 뒤 다시 시험했다.

| 5절 # | 시나리오 | 결과 | 근거 |
|---|---|---|---|
| 1 | 싱글 — 빈 땅에 EMP | 전달된 판정에 싱글 EMP 항목이 따로 없다 | 로그 1의 싱글 기록은 포격 3회(착탄 401~403ms) |
| — | 발동 VFX 높이(수정 확인) | PASS | 로그 1: 사용 표시 VFX 55건 모두 루트 높이 60mm · 타일 윗면 위(`AboveTileTop=True`) |
| 2 | 원 안 탑만 멈춤 · 양쪽 화면 같은 탑 V6 | PASS | 육안: 범위 밖 탑은 무력화 안 됨. 로그: 송신 · 수신 탑 목록 23회 일치 |
| 3 | 원 안 적 유닛 무영향 | 전달된 판정에 없음 | — |
| 4 | 날던 포탄은 맞음 · 다음 발사 없음 | PASS(육안 — 로그 장면 없음) / 다음 발사 없음은 로그 | 육안: 날아가던 포탄 그대로 맞음. 로그: 무력화 중 발사 0(`LaunchesWhileDisabled 0`) |
| 5 | 무력화 끝 | 길이 PASS | 로그: 무력화 5001~5005ms. 만료 뒤 첫 발사 시각(K1)과 V6 꼬리 길이는 전달된 판정에 없다 |
| 6 | 무력화 중 같은 탑에 다시 EMP | PASS | 로그: 시간 갱신, V6 재시작 없음(`Restarted=False`, 최대 적용 4회 · 17.5초) |
| 7 | 무력화 중 탑 파괴 / 철거 | 파괴 PASS | 로그: 탑 파괴 4회 모두 V6 즉시 제거. 철거만의 장면은 전달에 따로 없다 |
| 8 | 상대 종족 3종 탑 | PASS(측정 전 값 기준) | 멀티 3판 — A 에디터 Host / 정령 RuneSpire, B 기기 Host / 인간 CannonTower, C 에디터 Host / 초월 VineTower — 3종 프리셋 찾음(`Preset=Found`). 위치 · 크기는 배율 1 · 보정 0 그대로(측정 메뉴 미실행) |
| 9 | 무력화 중 경기 종료 | PASS | 로그: 양쪽 화면 `GameEnd`로 V6 정지 |
| 10 | 내 탑(아군)에 EMP | PASS | 로그: 아군 탑 무영향 |
| 11 | 회귀 스위트 | **PASS** | WORKFLOW 「[6] 사용자 테스트」 규칙 — 사용자 FAIL 언급 없음. 결과 파일 없음(Console만), 에이전트 미관측 |
| — | 사용 불가 버튼(다른 스킬) | 간접 | 서버 요청이 없으면 로그가 없다 — 발동 슬롯이 0 · 2뿐이었다 |
| — | 멀티 포격(로그 2) | PASS | Host 포격 1회 — Client가 받은 조준 좌표 일치 · 뒤집힌 화면 · 루트 60mm · 착탄 500ms(0.4초 + 서버 틱 0.1초) · 건물 4개 명중 |

**미관측 — 이번 회차:**
- **Client가 쓴 스킬** — 멀티 전 회차 모두 Host가 썼다. 요청 RPC는 이번 작업에서 바뀌지 않았고, 이전 포격 Task는 종합 판정만 있다 → 「이번 회차 미관측」.
- **착탄 전 시전 건물 철거(포격)** — 「실기 미확인(코드 확인)」.

### 11-5. 로그 보관 (LogRules §1.10)

| 회차 | 보관 위치 | 대조 |
|---|---|---|
| 로그 1 | `rmsepskek02/Hexiege-Logs` Release `logs-2026-11` · `2026-11-09_18_43_vfx-test_01` | 메인 세션이 다운로드해 ZIP SHA256 `32af9c29…` · manifest `cce1f07d…` 일치 |
| 로그 2 | 같은 Release · `2026-11-09_19_07_vfx-test2_01` | ZIP `0a2fd671…` · manifest `2e75b2dc…` 일치 |

- 작업명이 안내(`emp-tower-disable`, WORKFLOW [6])와 다른 `vfx-test` / `vfx-test2`로 올라갔다 — 기록만 한다.

### 11-6. 남은 위험 · 결정

**사용자 결정 (2026-11-09, 원문 「추천대로 해」 — 메인 세션 추천안 채택):**

| 항목 | 결정 | 상태 |
|---|---|---|
| 진단 로그 `[EMP-DIAG]` · `[SKILL-VFX]`(`GameLog.Dev`) | **존속 `개발`로 유지**(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」, LogRules §1.2 존속 축) | 코드 변경 없음 |
| `Assets/Editor/Tools/EmpTowerDisabledPresetSetup.cs`(+ `.meta`) | **삭제** — 실행 완료 · 참조 없음(WORKFLOW [5-2] 최소 유지 기준) | **삭제됨**(game-programmer — 프리셋 3개 GUID가 `BuildingEffectConfig.asset`에 연결된 것과 코드 참조 0을 확인한 뒤. `0cd23ea7`) |
| `SupportSkillLoadoutSetup.cs`의 주석 처리된 옛 EMP 자리표시 줄 | **최종 삭제**(WORKFLOW [4] 「기존 로직 제거 규칙」 — 사용자 테스트 통과 뒤). 파일 자체의 퇴역은 기존 결정대로 지원 스킬 작업 전체가 끝난 뒤([포격 Task](../../2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md#10-implementation-and-user-test) 10-5) | **삭제됨**(주석 3줄, 구 239~241행 — `0cd23ea7`) |
| 포격 프리팹 `Impact_Flash`(서브 이미터) 착탄 섬광이 타일에 가려짐 | **고친다** — 사용자 「3번 고쳐」 | **수정 커밋 `0cd23ea7` — 사용자 육안 테스트 대기.** 원인(game-programmer): 포탄(Shell) 착지점 = 루트 0.06이고 착탄층이 모두 포탄 소멸(Death) 서브 이미터라 그 높이에서 생겨, 55° 카메라 빌보드의 아래 끝이 크기 × 약 0.287만큼 낮아 타일에 가려졌다. 수정: `Impact_Flash` 방출 모양 켜기 · Sphere 반경 0 · 위치 Y 0.35, `Fireball` · `Smoke` 방출 모양 위치 Y 0.45 → 아래 끝이 타일 윗면 0.05 위(계산, 크기 0.5~1.5 유지). 그대로: 타이밍 · 7발 · 낙하 반경 1.35 · 1.6초 · 바닥층(`Shock_Ring` · `Scorch` · `Target_Marker`, 0.06 이상) · `Sparks` · `Debris`(중력으로 떨어져 땅에서 사라짐 — 바꾸려면 별도 결정). 미확인: 서브 이미터 방출 모양 위치가 실제로 적용되는지(Unity 동작 이해 — 화면 미확인), 포탄이 한 프레임 더 내려가 사라질 가능성(60fps 최대 약 0.17). 진단 로그: 루트 높이는 기존 `ActivationVfxPlayed`로 판정, 층 높이는 육안 항목(프리팹 고정값 · GPU 깊이라 로그로 못 잰다, 매 틱 로그 금지) |
| 탑 높이 측정 메뉴(`Assets/Editor/Tools/TowerBoundsMeasureTool.cs`) | **유지**(메인 세션 전달 「U-1대로 유지」) — 측정을 하지 않아 V6 배율 1 · 높이 0 그대로 | 변경 없음 |

**관찰 — 범위 밖, 보고만(새 할 일이 아니다):**
- 판 A가 끝날 때 에디터 Host에서 `[UAS-MOVE-AUTH] END verdict=FAIL`(18:38:04). 다른 판은 `EVIDENCE`. 원인은 조사하지 않았다.
- 만료 0.09초 뒤 다시 EMP를 쓴 장면 — 서버는 그 사이 탑이 1회 쐈고, Client의 V6는 이어졌다. 전송 지연(0.1초 미만)으로 생기는 표시 차이이며 R-7(알려진 한계)의 범위다.

**남은 위험(기록):** R-7 Client 시작 · 끝 지연(위 관찰), R-13 타일 중심 판정, 늦은 접속 스냅샷 없음, NGO 두 RPC의 도착 순서 `[추정]` 그대로(목록 일치 23회는 순서를 증명하지 않는다), R-8 싱글 결과 화면 뒤 탑 재사격(기존 동작).

### 11-7. 규칙 · 상시 문서 반영 (2026-11-09)

| 자리 | 반영 |
|---|---|
| `GameSystemRules_Skills.md` 「스킬 메커니즘 타입」 머리 · **새 규칙 27 「지점 범위 적 방어탑 무력화(EMP)」** · 규칙 7 요소 표 「타입」 · 14 · 16 · 25 | 8절 2~6행. 새 타입은 새 번호(상위 Plan 6절 「새 규칙은 27~」). 코드 이름(`AreaTowerDisable`)에 A · B · C 같은 글자 이름이 없어 규칙에도 글자를 붙이지 않았다. 자연어 설명 끝 문단의 「기능이 붙은 것은 포격 지원 하나」가 거짓이 되어 고쳤다 |
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 **새 규칙 13 「무력화」** | 8절 1행 — 공격 불가 · 쿨다운 계속 감소(K1) · 비행 중 투사체 유지 · 파괴 시 해제 · 경기 종료 · 서버 권위 |
| `TechnicalDesignDocument_Network.md` 「경기 시작 호환 확인」 | 8절 7행 — 스키마 7 → 8, 선례에 「7 → 8 EMP 탑 무력화 RPC」 추가. `GameSystemRules_UnitCombatSynchronization.md`에는 스키마 **값**이 적혀 있지 않다(「10.」 5항은 `CombatSchemaRevision` 이름만) — 고칠 곳 없음 |
| `VFXSFXGuide.md` 「코드 계약」 10번 · 새 11번 · `VFXSFXList.md` 「스킬 발동 이펙트」 | 지면 높이 0.06 계약, 탑 상태 VFX(V6) 재생 계약, EMP V5 · V6 연결 행 |
| `PROJECT_STATUS.md` · `WORK_HISTORY.md` | 현황 행 · 완료 행. `ROADMAP.md`는 바꾸지 않았다 — 지시 없는 행을 더하지 않고, 「확정 스킬 15개의 수치는 임시값」 행이 EMP 5초(임시)에도 그대로 맞다 |

### 11-8. 다음 작업

상위 Plan 5-1 **결정 31**이 정한 순서(첫 코드 task → 탑 높이 측정 메뉴 + EMP)는 여기서 끝난다. **다음 코드 task는 아직 정해지지 않았다** — 2차 내부 순서(O-12)가 열려 있고, 1차 나머지(전술 지령 공격력 · 화염의 축복 · 숲의 포효 · 숲의 순풍)와 2-Z · 2-D 등의 순서를 정한 결정 기록이 없다(상위 Plan 3-2). 이 Task가 새로 정한 것은 없다.
