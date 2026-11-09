# Research — 인간 지원 스킬 EMP(2-B 건물 대상 상태 + 탑 무력화 VFX)

## 이 문서는 무엇인가

인간 스킬 건물의 3번 칸에는 **EMP**가 들어 있지만, 지금은 「사용 불가」로 꺼져 있어 눌러도 아무 일이 없다. 이번 작업은 EMP를 실제로 동작하게 만드는 것이다.

EMP는 지도의 한 지점을 골라 쓰는 스킬이다. **쓴 바로 그 순간 원 안에 있던 적 방어탑**(인간 대포 타워 · 정령 룬 첨탑 · 초월 덩굴 타워)이 잠시 동안 공격을 하지 못한다. 유닛에게는 아무 영향이 없고, 이미 날아가고 있던 포탄은 그대로 날아가 맞는다. 같은 탑에 다시 쓰면 멈추는 시간이 처음부터 다시 시작된다. 화면에는 두 가지가 보인다 — 쓴 자리에 퍼지는 **전기 고리**(V5)와, 멈춘 탑 위에서 지지직거리는 **합선 · 연기 이펙트**(V6).

지금 게임 코드에는 이것을 만들 자리가 셋 다 없다. ① 건물에 「멈춤」 같은 상태를 걸어 두는 곳, ② 탑이 쏘기 직전에 「지금 쏴도 되나」를 묻는 관문, ③ 탑 위에 계속 붙어 있는 이펙트. 이 문서는 그 세 가지를 만들기 전에 **지금 코드가 어떻게 되어 있는지, 어떤 규칙이 근거인지, 무엇이 위험하고 무엇을 아직 모르는지**를 파일 · 행 단위로 정리한 기록이다. 어떻게 만들지(추천안 · 변경 파일 · 커밋 4단위 · 테스트 · 사용자 확인 Q1~Q12)는 같은 폴더의 [Plan.md](Plan.md)에 있다. **이번 단계는 문서만 만든다** — 코드 · 에셋 · 규칙 문서는 바꾸지 않았다.

> **🔴 먼저 알릴 사실 — 확정 사항과 현재 코드가 어긋난 곳 1건.** 상위 기획에서 정한 「탑 크기 맞춤 프리셋은 `BuildingEffectConfig`의 **`_autoTowerProjectiles` 구조를 따른다**」(상위 Plan 4절 15 덧붙임, 2026-11-02)의 그 칸 `_autoTowerProjectiles`는 **커밋 `d719fa53`(2026-11-03 타워 공격 표현 개편)에서 삭제됐다** `[확인]`. 따를 "모양"(종족 한 줄마다 프리셋을 두는 목록, 초월이라고 비워 두는 분기 없음)은 옛 커밋에 남아 있으므로, **같은 모양의 새 칸을 만드는 안(P1)** 을 제안한다. 삭제된 칸을 되살리는 것이 아니다. 이렇게 이해해도 되는지는 사용자 확인 대상이다 → [Plan 7절](Plan.md) **Q9**. 자세한 근거는 [6-3](#6-3-종족별-프리셋-칸--확정-사항과-어긋난-곳).

> **현재 상태 (2026-11-09):** **완료 — 구현 · 사용자 테스트 PASS.** 결과 · 미관측 · 남은 결정은 [Plan 11절](Plan.md#11-implementation-and-user-test), 이 조사의 9절 「확인하지 않은 것」이 구현 뒤 어떻게 됐는지는 [11절](#11-구현-뒤-확인-2026-11-09). *(2026-11-09 갱신 — 직전 「**사용자 확인 Q1~Q12 대기, 구현 승인 전.** 코드 · 에셋 · 규칙 문서 무변경.」 그 뒤 2026-11-04 사용자 결정(Plan 7-1)과 구현이 있었다)*

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-1 · 2절 EMP 행, 2-1 V5 · V6, 4절 14 · 15(프로그래머 확인 물음), 5절 O-22 · O-23(열림), 5-1 결정 12 · 19 · 22 · 31, 6절 R-4 · R-7 · R-8 · R-11, 7-2 EMP 기획 요지.

**직전 완료 Task(기반):** [지원 스킬 공통 전제 + 포격 지원](../../2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md#10-implementation-and-user-test) — 스킬 데이터의 「사용 가능」 · 착탄 지연 · 발동 VFX · 기준 반경 칸, 사용 불가 거부, 발동 표현 동기화(C-1), 스키마 7, 착탄 예약. EMP는 이 기반 위에 올라간다.

**연결 Task(의존):** [방어탑 3종 크기 측정 메뉴](../../2026-11-02/20_45_tower-bounds-measure-tool/Plan.md) — 메뉴 구현 커밋 `4c2f41c9`(현재 HEAD, `git log` 확인) · **사용자 측정 결과 대기** `[전달 — 메인 세션]`. V6의 종족별 배율 · 높이 값은 측정 뒤 정한다. 그 전에는 **배율 1 · 높이 보정 0**으로 둔다. *(문서 담당 확인: 그 Task Plan 머리의 현재 상태 줄은 아직 「구현은 사용자 승인 대기」(2026-11-02)로 남아 있다 — 이번 범위가 아니라 고치지 않았다.)*

**원자료:** game-programmer 읽기 전용 조사(2026-11-03, 메인 세션 전달). 조사 기준 브랜치 `claude/vfx-code-followups`, HEAD `3ff5ed60`, 작업 트리 깨끗함 `[전달]`.

**표기:** `[확인]` = 문서 담당이 2026-11-04 HEAD `4c2f41c9` 작업 트리에서 그 행을 직접 다시 읽어 확인한 것 · `[전달]` = 프로그래머 조사를 그대로 옮긴 것(문서 담당 미재확인) · `[추정]` = 근거는 있으나 실행으로 확인하지 않은 판단 · `[미확인]` = 이 단계에서 확인할 수 없었던 것 · `[제안 — 프로그래머]` = 프로그래머 추천(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`. `3ff5ed60` → `4c2f41c9` 사이 변경은 `Assets/Editor/Tools/TowerBoundsMeasureTool.cs` 신설 1개뿐이라(`git diff --stat`) 원자료의 `Scripts/` 행 번호는 그대로 유효하다 `[확인]`.

---

## 1. 결정과 범위

| 항목 | 내용 | 출처 |
|---|---|---|
| EMP 분류 · 범위 | **지점 조준 범위 · 일시 타격** — 발동 순간 범위 안의 적 방어탑을 스냅샷으로 정한다(맵 전체 아님) | 상위 Plan 5-1 결정 12 → 결정 22(사용자 「이미 1번으로 제작했으니 그렇게 진행해 보자」) `[확정]` |
| EMP 효과 | 적 방어탑 3종을 잠시 무력화. 이미 날아가는 투사체는 유지. 다시 쓰면 지속 갱신. 대상은 건물만 | 상위 Plan 2절 EMP 행 `[확정]` |
| 실행 묶음 | **2-B 건물 대상 상태** — 건물용 상태 저장 공간이 새로 필요(유닛 Id 키와 분리) | 상위 Plan 3-2 2-B 행 |
| 작업 순서 | 첫 코드 task(C-1 · C-4 + 포격) 다음 = **탑 높이 측정 메뉴 + EMP** | 상위 Plan 5-1 결정 31 `[확정]` |
| VFX 2종 | V5 `vfx_emp_pulse`(발동) · V6 `vfx_emp_disabled`(상태) — 둘 다 제작 · 검수 완료. 화면 가독성은 게임 적용 후 테스트에서 | 상위 Plan 2-1 V5 · V6 `[확정]` |
| 탑 높이 | 측정 메뉴(결정 19)로 잰다 — 측정 결과 대기 | 상위 Plan 5-1 결정 19 · 연결 Task |
| 열린 결정 | **O-22** V5와 V6를 동시에 시작하는가 · **O-23** 다시 가동될 때의 연출을 빼는가 — 둘 다 사용자 미답(기획안 추천: 동시 · 제외) | 상위 Plan 5절 → 이번 Plan Q3 · Q11 |
| 이번 범위 밖 | 스킬 버튼 라벨 · 아이콘 · 설명 탭 | 상위 Plan 5-1 결정 16(추후 UI 작업) → Plan Q8 |
| 이번 범위 밖 | 무력화 시간의 최종 값(밸런싱) — 시험용 임시 값만 정한다 | 상위 Plan 3-1 「무력화 시간」 → Plan Q1 |
| 이번 범위 밖 | 규칙 문서 수정 — 「무력화」 규칙 신설 포함, 구현 뒤 반영 | 메인 세션 지시 → Plan 8절 |

---

## 2. 근거로 읽은 규칙

`Assets/_Project/Docs/GameSystemRules.md`(인덱스)에서 스킬 · 건물 · 사운드 · 전투 동기화 행을 골라 원본을 열었다 `[확인]`. 「스킬 규칙 N」 = `GameSystemRules/GameSystemRules_Skills.md` 규칙 N. 건물 문서는 절마다 번호가 다시 시작하므로 「방어 타워 시스템」 절 이름을 함께 적는다.

| 규칙 | 내용(요지) | EMP와의 관계 |
|---|---|---|
| 스킬 규칙 3 「건물별 글로벌 쿨다운」 | 발동 후 건물 글로벌 쿨다운 | EMP 시전 쿨다운은 기존 그대로(발동 순간) |
| 스킬 규칙 7 「데이터 주도 설계」 | 스킬 = 데이터, 코드는 타입별 실행기만. 요소 표 「타입 — 3종 중 하나」 | 새 실행기 하나 + 반경 · 지속은 자산. **「3종 중 하나」 문구는 새 타입이 생기면 거짓이 된다**(구현 뒤 반영 — Plan 8절) |
| 스킬 「스킬 메커니즘 타입」 머리 · 규칙 11~13 | 실행 방식은 **3종 타입**(A 즉발 범위 피해 · B 장판 · C 전역 상태변경) | 건물 대상 지점 상태는 셋 어디에도 맞지 않는다(3-1 「타입 C로 대신할 수 있나」). 상위 Plan 6절 **R-4** 개정 대상 |
| 스킬 규칙 14 「유틸리티 타입 제외」 | 3종에 속하지 않는 보조 스킬은 제외 | EMP가 「유틸리티」로 읽히지 않게 분류 명시 필요 — 상위 R-7 |
| 스킬 규칙 16 「지점 지정 발동」 | **타입 A · B**가 이 경로 | 새 타입도 지점 지정 — 상위 R-8 |
| 스킬 규칙 25 「서버에서만 판정·실행」 | 발동 · 판정 · 상태변경은 서버만. 착탄 지연은 서버 권위 틱에서만. 발동 표현은 서버 확정에서 나와 각 화면이 로컬 재생 | 스냅샷 · 관문 · 만료 = 서버, V5 · V6 = 각 화면. 「EMP 대상은 발동 순간 스냅샷」 문장 추가 — 상위 R-11 |
| 스킬 규칙 26 「좌표만 전송 + 서버 재검증」 | 클라 → 서버 요청은 조준 좌표 1개 | 요청 RPC 무변경 |
| 스킬 「멀티플레이 / 서버 권위」 머리 문장 | NetworkBehaviour는 Infrastructure에만, RPC 이름 접미사 | 새 RPC는 `NetworkSkillController`에 `…ClientRpc` |
| 「방어 타워 시스템」 규칙 1 「타겟 대상」 | 타워는 적 유닛만 노린다 | EMP는 타워의 대상 규칙을 바꾸지 않는다 |
| 「방어 타워 시스템」 규칙 5 「공격 후 쿨다운」 | 공격 직후 쿨다운. **쿨다운 중에도 타겟 탐색은 계속** | 무력화 중 쿨다운 처리(K1 / K2)와 맞물린다 — 4-2 |
| 「방어 타워 시스템」 규칙 7 「파괴 시 공격 즉시 중단」 | HP 0이면 탐색 · 쿨다운 진행 등 모든 동작 중단 | 탑 파괴 때 V6 즉시 정지의 근거(Plan Q4) |
| 「방어 타워 시스템」 규칙 9 「서버 권위 처리」 | 타겟 · 사거리 · 피해는 서버만, 클라는 표현만 | 관문은 서버 Tick 안 |
| 「방어 타워 시스템」 규칙 11 「Human CannonTower 배치 시 초기 회전」 | 상대 진영 대포탑만 Y 180° | V6 위치 보정의 X · Z 주의 — 6-4 |
| 「방어 타워 시스템」 규칙 12 「타워 발사 연출」 | 공격 표현 = 종족별 타워 공격 프로필. 행이 없으면 재생 없음(VineTower) | 프리셋 칸을 공격 프로필에 넣지 않는 근거(P2 비추천) · 6-5 |
| `GameSystemRules_Sound.md` 규칙 15 「VFX+SFX 쌍 호출 규칙」 | VFX 호출 바로 옆에서 SFX 짝 | EMP SFX를 넣는다면(Plan Q7) |
| `GameSystemRules_UnitCombatSynchronization.md` 「10. 현재 구조에서의 전환 규칙」 5항 | 경기 시작 전 스키마 일치 확인 | 스키마 7 → 8 상향의 근거(Plan Q6) |
| **「무력화」를 정의한 규칙** | **없다** — 건물 문서 「방어 타워 시스템」 절(규칙 1~12)에 탑 상태 효과 규칙이 없다 `[확인]` | 구현 뒤 그 절에 새 번호(13~)로 「무력화 중 공격 불가 · 쿨다운 처리 · 비행 중 투사체 유지」를 추가해야 한다. **이번 작업에서는 규칙 문서를 고치지 않는다**(Plan 8절) |

---

## 3. 현재 코드 — 실행 방식(타입)

### 3-1. 지금 구조

| 구성 | 위치 | 내용 |
|---|---|---|
| 실행 방식 열거형 | `Scripts/Domain/Skill/SkillMechanicType.cs` 24~36행 | A `InstantAreaDamage = 0` · B `AreaDotDamage = 1` · C `GlobalStatusChange = 2` 세 개뿐 `[확인]` |
| 실행기 계약 | `Scripts/Application/Skill/ISkillExecutor.cs` 58~65행 | `Execute(SkillActivationContext)` 하나. 46행 「신규 타입 = 실행기 클래스 추가 + 등록 1줄」 `[전달]` |
| 실행기 등록 | `Scripts/Application/Skill/SkillExecutorRegistry.cs` 104~116행 | 생성자에서 A · B · C 등록. 미등록 타입 → `TryGet` null → 발동 무효(`SkillActivationUseCase.cs` 149~150행) `[전달]` |
| 실행 컨텍스트 | `Scripts/Application/Skill/SkillActivationContext.cs` 177~187 · 200~218행 | 효과 다리 3개(A 즉발 피해 · B 장판 · C 전역 상태)를 생성자 델리게이트로 받는다 `[전달]`. **생성 지점은 `SkillActivationUseCase.cs` 164행 한 곳**(Editor 포함 grep 1건) `[확인]` |
| 데이터 칸 | `Scripts/Infrastructure/Config/SkillDefinition.cs` 74행 `_mechanic` · 78행 `_radius` · 81행 `_duration` | EMP에 필요한 반경 · 지속 칸은 이미 있다 `[전달]` |
| EMP 자산 | `Assets/_Project/Resources/Config/Skills/Skill_Human_Emp.asset` | `_isAvailable: 0` · `_aimType: 1`(지점) · **`_mechanic: 0`(A 자리표시)** · `_radius: 2` · `_duration: 0` · **`_totalDamage: 150`** · `_impactDelay: 0` · `_activationVfxPrefab: {fileID: 0}` · `_vfxReferenceRadius: 2` `[확인]` |
| 타입 C로 대신할 수 있나 | `Scripts/Application/UseCases/UnitCombatUseCase.cs` 2448행~(ApplySkillGlobalStatus) | **유닛 전체 순회**(`foreach _unitSpawn.Units`) — 건물 · 지점 범위가 없다. 쓸 수 없다 `[전달]` |
| 「사용 가능」만 켜면 | 자산이 타입 A · `_totalDamage: 150` | **포격처럼 150 피해**를 준다 — 05_24 Plan 3-5 U1 위험과 같다. 불가 `[확인 — 자산 값]` |

### 3-2. 새 타입을 넣으면 함께 영향받는 곳

| 자리 | 지금 | 영향 |
|---|---|---|
| `Scripts/Presentation/UI/BuildingSkillPanelUI.cs` 239~255행 `DeriveSkillLabel` | A · B · C 외 = `default: return "?"`(254행) `[확인]` | EMP 칸의 임시 라벨이 「폭탄」에서 **「?」** 로 바뀐다. 라벨은 추후 UI 범위(상위 결정 16) → Plan Q8 |
| `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` 18행 · 200행 | 「자산이 이미 있으면 … 값만 아래 표 값으로 다시 덮어쓴다(GUID 유지)」, EMP = `PointPlaceholder("Skill_Human_Emp", …)` `[확인]` | **이 설정 도구를 다시 실행하면 EMP 자산이 자리표시(A · 사용 불가)로 되돌아간다.** 도구 삭제 여부는 05_24 Plan 7-2 「남은 일」에서 미정 → 위험 R-9 · Plan Q10 |
| 회귀 fixture | `new SkillActivationContext` · `new SkillExecutorRegistry`를 Editor에서 만드는 곳 0건 `[확인 — SkillActivationContext]` · `[전달 — Registry]` | 컨텍스트 생성자에 인자를 더해도 fixture는 깨지지 않는다 |

---

## 4. 현재 코드 — 건물 상태 · 탑 관문 · 비행 중 투사체

### 4-1. 상태를 걸어 둘 곳이 없다 — 근거 사실

| 사실 | 근거 |
|---|---|
| 유닛 상태는 **유닛 Id 키** 딕셔너리 | `Scripts/Application/Services/StatusEffectSystem.cs` 36행 `Dictionary<int, UnitStatusState>` `[전달]` |
| 유닛 Id와 건물 Id는 카운터가 달라 **값이 겹칠 수 있다** | `UnitCombatUseCase.cs` 2368행 주석 `[전달]` |
| 유닛 상태 틱은 **클라이언트 미러에서도 돈다** | `Scripts/Bootstrap/GameBootstrapper.cs` 737~741행(`!IsNetworkMode() \|\| !IsNetworkServer` 묶음, 740행 `_statusEffectSystem.Tick`) `[확인 — 740행]` |
| 권위 전용 틱 선례(착탄 예약) | 싱글 `GameBootstrapper.cs` 727~730행, 멀티 `Scripts/Infrastructure/Network/NetworkCombatController.cs` 772행(TickCombat 안) `[전달]` |
| 탑 틱 위치 | 싱글 `GameBootstrapper.cs` 752행 `_towerCombat.Tick(Time.deltaTime)`(750~753행 묶음), 멀티 `NetworkCombatController.cs` 729행 `tower?.Tick(elapsed)` `[확인]` |
| 경기 종료 닫기 선례 | `GameBootstrapper.cs` 544행 `OnGameEnd → ClosePendingImpacts`, `SkillActivationUseCase.cs` 355~359행 `[전달]` |
| 싱글은 경기 종료 뒤에도 탑 틱이 돈다 | 750행 가드가 `!IsNetworkMode()`뿐. 멀티는 `_combatStopped`로 TickCombat 자체가 멈춘다(772행 주석) `[전달]` |
| 같은 종류 상태 재부여 = 새 값으로 **교체**(중첩 없음) | `Scripts/Domain/Status/UnitStatusState.cs` `ApplyOrRefresh` 43~53행 `[전달]` |
| `AttackDisabled = 3`(「공격 불가, 이동은 가능」) 종류가 이미 있다 | `Scripts/Domain/Status/StatusEffectKind.cs` 40~41행 · `UnitStatusState.CanAttack` `[전달]` |
| 상태 시스템 · 스킬 UseCase는 맵을 불러올 때마다 새로 만든다 | `Scripts/Bootstrap/GameBootstrapper.Setup.cs` 409 · 418행 `[전달]` |
| 방어탑은 업그레이드 대상이 아니다 → Id가 바뀌지 않는다 | `Scripts/Domain/Building/BuildingTypeHelper.cs` 139~142행 + 다음 단계 표에 AutoTower 없음(54행 주석) `[전달]` |
| 건물 사망 · 철거는 서버와 클라 모두 `OnBuildingDied`를 낸다 | 서버 `BuildingPlacementUseCase.cs` 329행(철거) · `UnitCombatUseCase.cs` 2008 · 2332 · 2524행(피해), 클라 `NetworkCombatController.cs` 3544행 `[전달]` |
| 건물 범위 판정 선례(타입 A) | `UnitCombatUseCase.cs` 2562~2573행 `CollectEnemyBuildingsInRadiusDomain` — 아군 제외 · **타일 중심**(`HexToWorld(building.Position)`) ↔ 조준점 거리 `[전달]` |

### 4-2. 탑 공격 순서 — `Scripts/Application/UseCases/TowerCombatUseCase.cs` `Tick`

| 행 | 지금 하는 일 |
|---|---|
| 134 | 멀티 클라이언트면 즉시 return(서버 권위) `[확인]` |
| 142~148 | AutoTower · 생존만 순회(148행 「규칙 7: 파괴된 타워는 모든 동작 중단」) `[확인]` |
| 150~153 | 쿨다운 감소(「규칙 5: 쿨다운 중에도 아래 탐색은 계속」) `[확인]` |
| 156 | 쿨다운이 남으면 `continue` `[확인]` |
| 159 | 사거리 안 가장 가까운 적 유닛 탐색 `[확인]` |
| 166 | `ExecuteTowerAttack` — 인간 = 서버 투사체 발사(253~258행), 정령 · 초월 = 즉발 피해(260~320행). 발사 실패면 쿨다운 시작 안 함 `[확인 — 166행]` |
| 170~172 | 공격 직후 쿨다운 리셋 `[확인]` |

- **「공격만 막고 쿨다운은 따로 돈다」 선례:** 유닛 빙결 · 기절은 공격만 막고(`UnitCombatUseCase.cs` 653~654행, `NetworkCombatController.cs` 878행) 쿨다운 틱은 따로 돈다(`UnitCombatUseCase.TickCooldowns` 1079행) `[전달]`.
- **생성자를 바꾸면 회귀 fixture가 깨진다:** `TowerCombatUseCase` 5인자 생성자를 Editor 두 곳이 쓴다 — `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 1913행 · `RunUnitActionSelfValidation.ServerProjectiles.cs` 637행 `[확인]`. 같은 파일 644행이 `Tower.Tick`을 부른다 `[전달]`. 설정자(setter) 선례: `SetUpgradeUseCase`(108~111행) · `SetProjectileLauncher`(118~121행) `[전달]`.
- **`ExecuteTowerAttack`의 이름 · 매개변수는 Editor 검증이 리플렉션으로 부른다**(244~246행 주석) — 바꾸면 안 된다 `[전달]`.

### 4-3. 「이미 날아가는 투사체 유지」

| 탑 | 경로 | EMP 영향 |
|---|---|---|
| 인간 CannonTower | `TryLaunchTowerProjectile`이 **곧바로 비행 중 기록**을 만든다 — 「발사 전 끊김 구간이 없다」(`UnitCombatUseCase.Projectiles.cs` 29행, 253~280행). 착탄 피해 `ApplyTowerProjectileImpact`는 **탑 생존 · 상태를 보지 않는다**(`TowerCombatUseCase.cs` 330 · 342행) `[전달]` | 관문이 Tick의 발사 결정에만 있으면 날던 포탄은 **자동으로 유지**된다. 추가 코드 없음 |
| 정령 RuneSpire · 초월 VineTower | 즉발 피해 — 날아가는 것이 없다 `[전달]` | 해당 없음. RuneSpire 번개 VFX는 이미 맞은 공격의 표현이라 끝까지 재생된다(정상) |

→ 상위 Plan 4절 15의 물음 「서버 투사체인 CannonTower에만 해당하는가」의 답 `[전달]`: **예. 실질적으로 CannonTower만 해당하고, 관문을 Tick 안에 두는 것만으로 지켜진다.**

---

## 5. 현재 코드 — 멀티 동기화 · 스키마

### 5-1. 지금 있는 것

| 구성 | 위치 |
|---|---|
| 발동 표현 RPC `SkillActivatedClientRpc(buildingId, team, slot, hasAim, aim, sequence, impactDelay, cooldown)` | `Scripts/Infrastructure/Network/NetworkSkillController.cs` 242~266행 `[전달]` |
| 서버는 `GameEvents.OnSkillActivationConfirmed` 구독으로 브로드캐스트, 클라는 `IsServer` 가드 뒤 쿨다운 미러 + `OnSkillActivationReceived` 발행 | 같은 파일 94~99 · 210~221 · 246~265행 `[전달]` |
| 타입 C 상태 동기화 RPC(팀 단위) — **늦은 접속 스냅샷 없음** | 같은 파일 277~298행 `[전달]` |
| 배열 인자 ClientRpc 선례 | `NetworkMapTransfer.cs` 1206행 `MapChunkClientRpc(ulong, int, byte[])` `[전달]` |
| `IGameServices`에 메서드 추가 금지(fixture 대역 구현) | 05_24 Plan 2절 · `RunUnitActionSelfValidation.cs` 73행 `[전달]` |
| AI는 스킬을 쓰지 않는다 | `Scripts/` 전체에서 `Activate` 호출은 `NetworkSkillController`와 패널뿐(grep) `[전달]` |
| 클라이언트가 무력화 상태를 알아야 하는 곳 | 탑 공격 판정은 서버만 한다(`TowerCombatUseCase.cs` 134행 `[확인]`) → **화면 VFX뿐** |

### 5-2. 스키마

| 사실 | 근거 |
|---|---|
| 현재 값 **7**, 주석 「6 → 7 스킬 발동 표현 RPC 모양 변경」 | `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 25~29행 `[확인]` |
| 선례: **새 RPC가 생기기만 해도 올렸다** — 「5 → 6. 발사 전 취소 통보 RPC가 새로 생겼다」 | 같은 파일 23~24행 `[확인]` |
| 접속 확인은 이 상수를 그대로 비교 | `Scripts/Infrastructure/Network/NetworkGameFlow.cs` 82~83행 `[전달]` |
| fixture가 `== 7`을 요구 | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs` 259행 `[확인]` |
| 스키마 7은 아직 `main`에 없다 — `56d5ab2a`는 `main` · `origin/main`의 조상이 아니고 `origin/main`의 값은 6 | `git merge-base --is-ancestor` · `git show origin/main:…` `[전달]` |

→ 사용자 기기에 C-1 테스트 빌드(스키마 7, EMP RPC 없음)가 깔려 있다면, 스키마를 올리지 않을 경우 그 빌드와 EMP 빌드를 접속 단계에서 구분하지 못한다. 그때 모르는 RPC를 받은 쪽의 NGO 동작은 `[미확인]`(05_24 Research 3-2와 같은 미확인) → Plan Q6.

---

## 6. 현재 코드 — 화면(탑 지속 VFX · 종족별 프리셋 · 빨강 화면 · 탑 연출)

### 6-1. VFX 자산 — `Assets/_Project/Prefabs/VFX/Skills/`

| 프리팹 | 반복 | 수명 | 비고 |
|---|---|---|---|
| `vfx_emp_pulse.prefab`(V5) | `looping: 0` | `lengthInSec: 1.2` | `scalingMode: 0`(Hierarchy) · Local 공간. 루트 높이 0.05 `[전달]` |
| `vfx_emp_disabled.prefab`(V6) | **`looping: 1` 전부**(루트 + 자식 4: Short_Circuit · Smoke_Wisp · Base_Ring · Spark_Crown) `[확인 — looping 5곳]` | 입자 최대 수명 **0.8초**(루트 · Smoke · Ring 0.8, Short 0.3, Spark 0.35) `[전달]` | Hierarchy · Local, 회전 항등 · 위치 0 `[전달]` |

→ 「정지 후 꼬리 ≤ 0.8초」는 방출만 멈추면 자연히 지켜진다(최대 수명 0.8) `[전달]`.

### 6-2. 탑에 계속 붙는 VFX 경로 — 지금 없는 것과 쓸 수 있는 것

| 사실 | 근거 |
|---|---|
| 건물용 VFX는 1회성뿐(파괴 · 업그레이드) | `Scripts/Presentation/Effects/EffectManager.cs` 501~516행 `[전달]` |
| 지속 VFX는 유닛 전용 채널(화상 · 독 · 힐)뿐 | 같은 파일 343~430행, `VfxPoolItem.TimedDamage.cs` · `VfxPoolItem.cs` 112~262행(힐 lease) `[전달]` |
| 풀 항목은 입자가 모두 사라지면 스스로 반환 | `VfxPoolItem.cs` Update 518~521행 · `Return` 602~631행 `[전달]` |
| 일반 「방출 정지」 공개 API가 없다(힐 `StopBloom` · 화상 `StopTimedDamage` · `StopCasterOwned`만) | `VfxPoolItem*.cs` 공개 메서드 `[전달]` |
| 늦은 정지가 재사용된 항목을 멈추지 않게 하는 lease 선례 | 힐 `_bloomLease`(`VfxPoolItem.cs` 90 · 243~250행) `[전달]` |
| 발동 VFX(V5)는 연결 경로가 이미 있다 — 자산 칸만 채우면 조준점에 재생 | `SkillActivationVfxPresenter.cs` 90~110행, `EffectManager.PlaySkillActivationVfx` 537~555행 `[전달]` |
| **탑 GameObject의 자식으로 VFX를 붙이면 안 된다** — 탑 사망 때 `BuildingFactory`가 GO를 `Destroy`한다(풀 항목도 같이 파괴됨) | `Scripts/Infrastructure/Factories/BuildingFactory.cs` 101~117행(`OnBuildingDied` 구독 → GO Destroy) `[확인]` |
| 탑 GO의 위치는 이미 화면 좌표(`ToView` + `_buildingYOffset`), GO 조회 = `IBuildingObjectProvider.GetBuildingObject(id)` | `BuildingFactory.cs` 163~164 · 304~308행 `[전달]` |
| 탑 프리팹은 정적 메시뿐(Animator · 파티클 0) | `Prefabs/Buildings/{Human,Spirit,Transcendence}/Building_*Tower_*_Blue.prefab` `[전달]` — Red 변형은 열어 보지 않았다(9절) |
| 화면 구독자를 붙이는 자리 선례 | `GameBootstrapper.Map.cs` 271~277행 `[전달]` |
| 탑 종족 해석 람다 선례 | `GameBootstrapper.Setup.cs` 379행 `new TowerCombatUseCase(` … 384~386행 `[확인 — 379행]` |

### 6-3. 종족별 프리셋 칸 — 확정 사항과 어긋난 곳

| 확정 사항 | 현재 코드 | 영향 |
|---|---|---|
| 「EMP 무력화 VFX 종족별 프리셋은 `_autoTowerProjectiles` 구조를 따르고 `_autoTowerAttacks`(초월 = 비움)는 복사하지 않는다」 — 상위 Plan 4절 15 덧붙임 · 상위 Research 2-13 ④(2026-11-02, 개편 전 확인 결과) | **두 칸 모두 삭제됨.** `BuildingEffectConfig.cs` 54~55행 주석 「옛 종족별 타워 프리셋 목록 2개(_autoTowerAttacks · _autoTowerProjectiles)는 아래 _towerAttackProfiles로 옮긴 뒤(사용자 테스트 통과) 삭제했다. 옛 값은 커밋 7368d7f1의 에셋에 남아 있다」 `[확인]`. 삭제 커밋 = **`d719fa53`**(`git log -S` · `git show`에서 `_autoTowerProjectiles` 삭제 행 5개) `[확인]` | 따를 "모양"은 남아 있다 — 옛 `_autoTowerProjectiles` = `List<RaceProjectile>`(76행), 조회는 `entry.race == race`인 줄을 그대로 찾고 초월이라고 null을 돌려주는 분기가 없다(`git show 7368d7f1:…/BuildingEffectConfig.cs` 76 · 83~84 · 93~94행) `[확인]`. **같은 모양의 새 목록 칸**(P1)이면 원안의 의도(VineTower 칸이 빠지지 않음)를 지킨다 `[제안 — 프로그래머]` → Plan Q9 |

- 지금의 `_towerAttackProfiles`(`TowerAttackProfile` 구조체)에 무력화 칸을 더하는 방식(P2)은 **권하지 않는다** `[제안 — 프로그래머]`: 프로필의 「비어 있음」 판정이 세 공격 프리셋 기준이고(`TowerAttackProfile.cs` 109행), **VineTower 행이 없는 것이 「공격 연출 없음」의 뜻**이다(`BuildingEffectConfig.cs` 62~65행 툴팁 `[확인]`, 「방어 타워 시스템」 규칙 12). VineTower용 무력화 칸을 넣으려면 행을 만들어야 해서 공격 연출 판정(`TryGetTowerProfile` · `EffectManager.cs` 609행 `UnsetAttackKind`)과 얽힌다 `[전달]`.
- `EffectPreset`에 **Scale Multiplier · Local Position Offset** 칸이 이미 있다(`EffectPreset.cs` 41 · 47 · 128 · 138행 — `ScaleMultiplier`가 0 벡터면 1) `[전달]` — 원안 문구와 칸 이름이 그대로 맞는다.
- V6 제작 기준: **높이 1.0 · 발밑 링 반경 0.5** `[전달 — 메인 세션]`. 종족별 배율 · 높이 값은 측정 메뉴 결과가 필요하다(연결 Task).

### 6-4. 빨강(뒤집힌) 화면

| 사실 | 근거 |
|---|---|
| 건물은 `ToView`로 화면 좌표에 놓인다 | `BuildingFactory.cs` 163행 `[전달]` |
| **상대 인간 대포탑만 Y 180° 회전**, 정령 · 초월 탑은 회전 없음 | `BuildingFactory.cs` 228~244행 `[전달]`, 「방어 타워 시스템」 규칙 11 `[확인]` |

→ V6를 탑 GO 위치에 **회전 항등**으로 두면 두 화면 모두 같은 모양으로 보인다. 다만 프리셋 위치 보정의 **X · Z를 0이 아닌 값으로 쓰면** 상대 대포탑(180° 회전)에서 모델과 어긋난다 `[추정 — 회전 계산상]` → 위험 R-6.

### 6-5. 탑 공격 연출(`TowerAttackProfile`, `d719fa53`)을 따로 멈춰야 하나

| 연출 | 시작 신호 | EMP 중 |
|---|---|---|
| 정령 번개 · (초월 — 행 없음, 재생 없음) | 서버 즉발 공격의 `OnAttackEffectConfirmed`(`TowerCombatUseCase.cs` 289~292행) `[전달]` | 관문이 공격을 막으면 **신호 자체가 안 나온다** |
| 인간 발사 · 투사체 · 착탄 | 서버 투사체 발사 기록(`ProjectilePresentationPresenter`) `[전달]` | 발사가 없으면 새 연출 없음. 이미 날던 포탄 연출은 계속(4-3과 일치) |
| 탑 상시 움직임 | 탑 프리팹에 Animator · 파티클 없음 `[전달]` | 멈출 것이 없다 |
| 탑 공격 SFX | 발사 VFX 바로 아래 짝(「방어 타워 시스템」 규칙 12 끝 문단 `[확인]`) | 같은 이유로 자동 무음 |

→ 화면 쪽 별도 정지 코드는 필요 없다 `[전달]`. `TowerAttackProfile` · `_towerAttackProfiles` 무변경.

### 6-6. EMP SFX

스킬 SFX 경로가 아직 없다(05_24 Plan 3-6 · Q5 ①에서 포격도 SFX 없이 진행) `[전달]` → Plan Q7.

---

## 7. 이 작업이 만들어야 하는 것(요약)

| 없는 것 | 왜 필요한가 | 추천안(Plan) |
|---|---|---|
| 건물을 대상으로 하는 지점 범위 실행 방식 | 타입 A는 피해, B는 장판, C는 유닛 전체 — 어느 것도 「지점 범위 적 탑에 상태」가 아니다(3-1) | M1 새 타입 · 새 실행기 |
| 건물 Id 키 상태 저장소 | 유닛 Id와 겹칠 수 있고, 유닛 상태 틱은 클라에서도 돈다(4-1) | B1 새 Application 서비스 |
| 탑 공격 직전 관문 | 지금 Tick에는 상태를 묻는 곳이 없다(4-2) | K1 관문 1줄(setter 주입) |
| 대상 탑 목록 알림 | 발동 표현 RPC는 어느 탑이 멈췄는지 싣지 않는다(5-1) | N1 별도 RPC 1개 + 스키마 8(Q6) |
| 탑에 계속 붙는 VFX · 종족별 크기 칸 | 지속 VFX는 유닛 전용, 옛 종족 칸은 삭제됨(6-2 · 6-3) | P1 새 종족 행 목록 + 새 화면 구독자 |

---

## 8. 위험 · 회귀 영향

| # | 위험 | 내용 | 대응 `[제안 — 프로그래머]` |
|---|---|---|---|
| **R-1** | **싱글에서 적 탑을 시험할 수 없다 → 멀티 테스트 필요** | AI 시나리오 3종 모두 방어탑(`buildingType: 2`, AutoTower)을 짓는 단계가 **0건**(`Resources/Config/AIScenarioConfig_{Human,Spirit,Transcendence}.asset` 정확 일치 grep 3파일 모두 0 `[확인]`). 스킬을 쓰는 AI 코드도 없다(5-1) `[전달]` | **EMP의 핵심 동작(탑 정지 · V6 · 갱신 · 파괴 · 종족별 위치)은 멀티(Host / Client)에서만 시험할 수 있다.** 싱글은 「발동 · 펄스 · 아무 일 없음」만 확인. 테스트 계획은 Plan 5절 |
| R-2 | 회귀 fixture | `TowerCombatUseCase` 생성자 · `Tick` 사용 2곳(4-2), 스키마 `== 7`(5-2) | 생성자 무변경(setter), 미주입이면 관문 통과 → fixture 결과 무변경. 스키마를 올리면 fixture 1줄 동시 수정 |
| R-3 | 탑 Tick 성능 | 탑마다 딕셔너리 조회 1회 추가 | 상태가 0개면 즉시 「쏴도 됨」(`StatusEffectSystem` 19~20행 회귀 안전 원칙과 같게) |
| R-4 | 풀 항목 오정지 | 꼬리 중 반환된 항목이 다른 VFX로 재사용된 뒤 늦은 정지가 그것을 멈춤 | lease 확인(6-2 선례) |
| R-5 | 탑 GO 자식으로 붙임 | 탑 파괴 때 풀 항목이 같이 Destroy | 자식으로 붙이지 않음(6-2) |
| R-6 | 상대 대포탑 회전 | 위치 보정 X · Z가 어긋남 | 보정은 Y(높이)만(6-4) |
| R-7 | 클라 종료 시각 오차 | 전송 지연만큼 늦게 시작 · 늦게 끝남(지속 길이는 같다 — 05_24 Plan 3-3 D1과 같은 성질) | N1 허용, 거슬리면 종료 RPC 추가(N2) |
| R-8 | 싱글 경기 종료 뒤 | 상태를 비우면 결과 화면 뒤에서 탑이 다시 쏜다(지금 다른 탑과 같은 동작, 4-1) | 기존 동작 유지. 바꾸려면 범위 밖 제안 → Plan Q5 |
| R-9 | 설정 도구 재실행 | `SupportSkillLoadoutSetup` 재실행 시 EMP 자산이 A · 사용 불가로 되돌아간다(3-2) | Plan Q10 |
| R-10 | 임시 라벨 | EMP 칸이 「?」로 보인다(3-2) | Plan Q8 |
| R-11 | 탑 높이 측정 의존 | 종족별 배율 · 높이 값이 측정 결과를 기다린다 | 우선 배율 1 · 보정 0으로 넣고 측정 뒤 값만 교체 |
| R-12 | 같은 틱 안 순서 | 싱글은 스킬 틱이 탑 틱보다 앞(727 → 752행), 멀티는 탑 틱이 맨 앞(729행) | 무력화 만료 틱을 두 모드 모두 **탑 틱 바로 앞**에 둔다 |
| R-13 | 범위 판정 기준 | 타일 중심 기준이라 탑 모델 가장자리만 원 안이면 안 걸린다 | 타입 A와 같은 기준(의도된 일관성, 4-1 마지막 행) — Plan 시나리오 2 |

**회귀 영향 범위(변경 없음)** `[전달]`: 포격(타입 A · 착탄 예약) · 타입 B · C 실행 경로, 발동 표현 RPC 모양, 탑 공격 프로필 · 탑 SFX, 서버 투사체 착탄, 유닛 상태 시스템 — 추천안은 모두 **덧붙이기**이고 위 경로의 코드를 바꾸지 않는다(컨텍스트 생성자 인자 추가 · 탑 Tick 한 줄 · 틱 호출 두 줄 · 레지스트리 한 줄 제외).

---

## 9. 이번 조사에서 확인하지 않은 것

- NGO에서 같은 NetworkObject의 두 ClientRpc가 보낸 순서대로 도착하는지(N1의 「발동 표현 뒤에 온다」) — `[추정]`, 실기 미확인.
- 모르는 RPC를 받은 구버전 클라의 NGO 동작(스키마를 올리지 않을 때의 위험) — `[미확인]`.
- 클라이언트에서 `GameEvents.OnGameEnd`가 발행되는 지점 — `NetworkGameEndController.cs` 7~17행 머리 주석상 결과 화면은 모든 클라에 뜨지만, 발행 행은 확인하지 않았다 `[미확인]`. V6의 경기 종료 정지가 클라에서 실제로 불리는지는 이 확인에 달렸다.
- V6 프리팹의 실제 화면 크기 · 탑 3종 높이 — 측정 메뉴 결과 대기.
- 탑 프리팹 Red 변형(`*_Red.prefab`)의 메시 차이 — Blue만 열어 봤다 `[전달]`.
- 사용자가 C-1 테스트 빌드(스키마 7)를 기기에 설치해 시험했는지 — 사용자 확인 Q6.

---

## 10. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-04 | 첫 판. game-programmer 읽기 전용 조사(HEAD `3ff5ed60`)를 옮겼다. 문서 담당이 HEAD `4c2f41c9`에서 다시 읽은 행: `BuildingEffectConfig.cs` 54~66 · `TowerCombatUseCase.cs` 130~173 · `UnitAttackShadowProfileResolver.cs` 20~30 · `SkillMechanicType.cs` 24~36 · `Skill_Human_Emp.asset` 값 · `vfx_emp_disabled.prefab` looping · AI 시나리오 3종 grep · fixture 1913 / 637 / 259행 · `SupportSkillLoadoutSetup.cs` 18 / 200행 · `BuildingSkillPanelUI.cs` 254행 · `GameBootstrapper.cs` 740 / 752행 · `NetworkCombatController.cs` 729행 · `_autoTowerProjectiles` 삭제 커밋(`d719fa53`). 원자료와 다르게 나온 곳 없음. 코드 · 에셋 · 규칙 문서 · 다른 Task 문서 · 메모리 무변경 |
| 2026-11-09 | 서두 현재 상태 줄 갱신, 11절 추가(9절 항목의 구현 뒤 상태 · 사실 정정 1건). WORKFLOW [11] 문서 반영 — 사용자 「그렇게 진행하고 나중에 문제 있으면 다시 살펴보자」. 9절 원문은 그대로 둔다 |

## 11. 구현 뒤 확인 (2026-11-09)

구현(`aea11d3a` · `28dfc9a8` · `6c97c6f1` · `d7cce131` · `0d5d43a3`)과 사용자 테스트(로그 2회차 — [Plan 11-4](Plan.md#11-implementation-and-user-test)) 뒤, 9절의 「확인하지 않은 것」이 어떻게 됐는지 적는다. 9절 원문은 조사 당시 기록이라 그대로 둔다. 로그 판정은 메인 세션 전달 **[전달]**이다.

| 9절 항목 | 지금 |
|---|---|
| 같은 NetworkObject 두 ClientRpc의 도착 순서 | **그대로 `[추정]`.** 송신 · 수신 탑 목록이 23회 일치했지만 도착 순서를 잰 기록은 아니다 |
| 모르는 RPC를 받은 구버전 클라 | **해당 없어짐** — 스키마를 8로 올려(Q6) 섞인 빌드는 경기 시작 단계에서 거절된다. NGO 동작 자체는 여전히 `[미확인]` |
| 클라이언트의 `OnGameEnd` 발행 지점 | **동작은 관측됨 [전달]** — 무력화 중 경기 종료 장면에서 양쪽 화면 모두 V6가 `GameEnd` 경로로 멈췄다(로그 1). 발행 행은 이번에도 읽지 않았다 |
| V6 실제 크기 · 탑 3종 높이 | **측정하지 않았다** — 탑 높이 측정 메뉴 미실행. 세 프리셋 모두 배율 1 · 보정 0(`665174df` 자산 [확인]). 사용자 첫 시험 원문 「타워에 적용되는 EMP 효과는 적절하게 적용되는 것 같으나 …」 |
| 탑 프리팹 Red 변형의 메시 차이 | 그대로 미확인 |
| C-1 테스트 빌드(스키마 7) 설치 여부 | 질문 대신 Q6 「8로 올림」으로 정리됐다 |

**조사에 없던 사실 1건 — 스킬 발동 VFX 높이.** 6-1 · 6-2는 V5를 「기존 경로 그대로」로 보았으나, 조준 좌표(Y = 0)에 재생하면 타일 윗면(Y = 0.05, 불투명 · 깊이 기록)에 가려진다는 것은 조사하지 않았다. 사용자 첫 시험에서 「발사 이펙트는 타일에 절반이 잘리는 느낌」으로 드러났고 `0d5d43a3`이 모든 스킬 발동 VFX 루트를 0.06 올려 고쳤다(`GameConfig.SkillVfxGroundYOffset` 주석 [확인]). 같은 원인으로 포격 프리팹 `Impact_Flash`(서브 이미터)도 아래가 잘릴 수 있으나 위치만으로는 올릴 수 없어 `0d5d43a3`에서는 고치지 않았다. 사용자가 「3번 고쳐」(2026-11-09)로 수정을 정했고, 착탄층을 방출 모양 위치로 올린 수정이 `0cd23ea7`에 들어갔다(사용자 육안 테스트 대기 — [Plan 11-6](Plan.md#11-implementation-and-user-test)).
