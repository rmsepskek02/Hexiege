# Research — 지원 스킬 2-Z 지속 상태 구역 (서리 감옥 · 수액 늪) + 구역 · 얼음 상태 VFX 칸

## 이 문서는 무엇인가

정령 스킬 건물의 **서리 감옥**과 초월 스킬 건물의 **수액 늪**은 지도 위 한 지점을 골라 쓰는 스킬이다. 쓰면 그 자리 바닥에 얼음(서리 감옥)이나 호박색 수액(수액 늪)이 **몇 초 동안 남는다.** 그동안 그 구역 안에 있는 적 유닛은 얼어붙거나(움직이지도 공격하지도 못함) 크게 느려진다. 구역에 **나중에 걸어 들어온 적도** 똑같이 걸리고, 구역을 **나가거나 구역이 끝나면** 풀린다. 지금 두 스킬은 슬롯에 이름만 있고 「사용 불가」로 꺼져 있다 — 이런 「남아 있는 구역」을 다루는 장치가 게임에 아직 없기 때문이다.

이 작업은 그 장치(**지속 상태 구역**)를 만들어 두 스킬이 실제로 동작하게 하고, 화면에 보일 이펙트 세 가지 — 바닥 얼음(V3) · 바닥 수액(V9) · 얼어붙은 유닛 몸의 얼음(V4) — 를 **나중에 프리팹만 만들어 칸에 넣으면 코드 수정 없이 바로 보이도록** 길을 미리 깔아 두는 것이다. 이펙트 프리팹은 아직 없으므로, 작업이 끝나도 칸은 비어 있고 효과만 동작한다. 인간의 **연막탄**도 같은 「구역」 스킬이지만 그 핵심(원거리 공격 회피)은 다른 큰 기능이라 이번에 만들지 않는다. 다만 나중에 같은 구역 장치 위에 얹을 수 있게 둔다.

이 문서는 **지금 코드가 어떻게 되어 있는지**(어디를 고쳐야 하고 무엇이 함께 영향을 받는지)를 정리한다. 무엇을 어떻게 만들지와 사용자가 정할 항목은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

**사용자 대화 원문(메인 세션 전달):**
1. 「vfx 관련 작업 중 vfx 제작만 하면 바로 사용해서 적용할 수 있도록 구현을 먼저 해두고 싶은데」 → 첫 작업으로 [지원 스킬 1차 강화 Task](../04_20_support-buff-skills-status-vfx/Plan.md#11-implementation-and-user-test) 진행 · 완료(2026-11-11)
2. 다음 작업 목록 제시 뒤 「1번 작업 진행해보자 task 문서 작성진행해」(2026-11-11) — 이 Research · Plan 작성 승인. Testcase 제외.

**출처 · 표기:** 아래 사실은 game-programmer의 HEAD `099e8a1` 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋(브랜치 `claude/vfx-code-followups`)의 파일을 직접 다시 읽어 행 내용을 대조한 것만 **[확인]** 으로 적었다. `[추정]`은 조사자 스스로 확인하지 못했다고 밝힌 것이다. `[문서 담당]`은 document-manager가 덧붙인 관찰이다(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`. 「상위 Plan N행」 = 아래 상위 Task Plan의 행 번호.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-2 · 1-3(슬롯), 2절(스킬별 효과 · 필요 기능 Z), 2-1(V3 · V4 · V9 · V7), 3-2 「2차」 2-Z 행, 4절 5 · 6 · 7, 5절 O-21, 5-1 결정 4 · 10 · 20, 7-2(VFX 기획 요지). **선례:** [EMP Task](../../2026-11-04/03_54_emp-tower-disable/Plan.md#11-implementation-and-user-test)(서버 전용 상태 서비스 · Id 배열 RPC · 스키마 상향 · 상태 VFX 루프), [지원 스킬 1차 강화 Task](../04_20_support-buff-skills-status-vfx/Plan.md#11-implementation-and-user-test)(유닛 상태 VFX 장치 `UnitBuffVfxPresenter` · 임시 테스트 프리팹 D14 · 같은 종류 덮어쓰기 D11).

---

## 1. 범위

| 이번에 하는 것 | 이번에 하지 않는 것 |
|---|---|
| **지속 상태 구역** 장치 — 구역 유지 · 들어온 적에게 적용 · 나가거나 끝나면 해제(서버 판정) | 연막탄의 **회피 판정**(E — 상위 Plan 「2차 끝」). 연막탄은 「사용 불가」 그대로 |
| 서리 감옥(빙결) · 수액 늪(강한 둔화) 켜기 | 이펙트 프리팹 제작(V3 · V4 · V9 — VFX 기획 · 제작 단계, 상위 Plan 3-0) |
| 바닥 구역 VFX 칸(V3 · V9가 들어갈 자리)과 재생 경로 | 서리 감옥 발동 연출 `vfx_frostprison_cast` 제작 여부(O-21 — 열림) |
| 구역 안 유닛의 몸에 붙는 상태 VFX(V4가 들어갈 자리)와 재생 경로 | 수치 확정(밸런싱 단계 — 상위 Plan 3-3) |
| 상대 화면에도 같은 구역 · 같은 유닛에 보이게 하는 동기화 | 제자리 전투 중 빙결 애니메이션 미정지 수정(6절 — 범위 밖 기록) |
| 테스트 항목별 진단 로그(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」) | 스킬 버튼 라벨 · 아이콘 · 설명 탭(상위 결정 16 — 추후 UI 작업) |

사용자 결정이 필요한 항목은 Plan 「결정 요약」 표(Z-1~Z-21)에 모았다.

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 3(건물 글로벌 쿨다운) · 7(데이터 주도 — 요소 표, 「강화 상태 VFX」 행이 「타입 C」로 적혀 있음) · 11(타입 A — 「착탄 전에 시전 건물이 파괴돼도 착탄은 그대로」) · 13(타입 C — 둔화 · 빙결 표현, 곱연산 합성, 「다른 출처의 같은 종류 상태가 겹칠 때는 아직 정하지 않았다(D11 — 지속 상태 구역 작업 때 결정)」) · 16(지점 지정 발동 — 쓰는 타입 목록) · 25(서버 권위 · 발동 표현 · 타입 C 상태 VFX · EMP) · 26(요청 = 조준 좌표 1개) · 27(새 타입은 다음 번호 · 글자 이름 없음 선례) [확인]
- `GameSystemRules/GameSystemRules_Buildings.md` 「MistShrine 물안개 힐 시스템」 규칙 9(범위 이탈 시 즉시 끊김) · 12(시전 건물 파괴 시 물안개 즉시 제거) · 13(겹치면 가장 가까운 하나만) · 25(파괴 · 철거 시 정리 경로) — 유일한 「지속 범위 판정」 선례 [확인 — 제목 행]
- `WORKFLOW.md` [3] · [4](「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」) [확인]
- 상위 Plan 1-2 · 1-3 · 2절 · 2-1 · 3-2 · 4절 5~7 · 5-1 · 7-2, EMP Plan, 1차 강화 Plan 전체 [확인]

---

## 3. 기획 효과 — 상위 Plan 인용

| 항목 | 내용 | 출처 | 등급 |
|---|---|---|---|
| 서리 감옥(정령 슬롯 2) | 지속 범위 · 유닛만 | 상위 Plan 90행 | [확인] |
| 〃 효과 | 「바닥 얼음 이펙트가 있는 동안 구역 안 적 유닛 빙결. 구역에 들어온 적도 빙결, 나가거나 얼음이 끝나면 해제. 타일 상태는 바꾸지 않는다」 · 대상 적 유닛 · 조준 지점 · 필요 기능 **Z + 기존 `Freeze`** · 근거 규칙 「스킬 규칙 13 · 16 (개정 필요)」 | 상위 Plan 136행 | [확인] |
| 〃 결정 10 | 「서리 감옥은 바닥에 얼음 이펙트가 존재하는 동안 효과를 적용하는 지속으로 변경하자」 | 상위 Plan 411행 | [확인] |
| 〃 밸런싱 | 「얼음 구역 유지 시간」 — 수치 없음 | 상위 Plan 211행 | [확인] |
| 수액 늪(초월 슬롯 1) | 「범위 바닥에 호박색 수액, 범위 안 적 강한 둔화. 늪에 들어온 적도 적용, 나가면 해제」 · 적 유닛 · 지점 · **Z + 기존 `MoveSpeedMul`** · 근거 규칙 「스킬 규칙 13 · 16」 | 상위 Plan 140 · 106행 | [확인] |
| 〃 밸런싱 | 「둔화 강도」 — 수치 없음 | 상위 Plan 215행 | [확인] |
| 공통 — 결정 4(O-3) | 「지속 스킬은 나중에 들어오는 유닛도 걸리는 게 맞고, 일회성으로 한 번만 발생하고 사라지는 건 발동 순간 안에 있는 유닛에게만 적용」 · 분류표 「지속 범위: 구역에 들어오는 대상에게도 걸리고, 나가거나 구역이 끝나면 풀린다」 | 상위 Plan 403 · 64행 | [확인] |
| 공통 — 반경 · 지속 | 기획에 숫자 없음. VFX 공통 기준 반경 **R0 = 2.0** | 상위 Plan 516행 | [확인] |
| VFX V3 | `vfx_frostprison_zone` — 구역 · Looping 바닥 얼음 · 경계 링 5.56 · 신규 텍스처 | 상위 Plan 162 · 522행 | [확인] |
| VFX V4 | `vfx_frostprison_frozen` — 상태 · 발밑 피벗 · 가시 결정 5개(반경 0.5 · 높이 1.0) · 유닛당 입자 16개 이하 · 크기 식 `max(키, 반경 × 1.1 / 0.5)`, 상한 `키 × 1.5` | 상위 Plan 163 · 523행 | [확인] |
| VFX V9 | 호박색 수액 바닥 — 구역 · 신규. 기획안 없음 | 상위 Plan 170행 | [확인] |
| 서리 발동 연출 | `vfx_frostprison_cast` 0.9초 원샷 — 기획안 권장, 제작 여부 미정(O-21 열림) | 상위 Plan 388 · 524행 | [확인] |
| 큰 유닛 보정 | 결정 20 「기준 크기로 먼저 만들고 큰 유닛 보정은 코드 단계에서 정한다」, Tank · CannonCart가 상한에 걸림(상위 Research 2-13 ①) | 상위 Plan 419행 · 상위 Research 221~ | [확인 — Plan] / [전달 — Research 행] |
| 연막탄(인간 슬롯 4) | 아군 지속 구역 + 회피(E), 2차 끝 — **이번 범위 밖**, 같은 틀에 얹을 수 있게 | 상위 Plan 133 · 208 · 291행 | [확인] |

- 슬롯 출처 [확인]: 상위 Plan 1-2(정령 2 = 서리 감옥) · 1-3(초월 1 = 수액 늪), 셋업 도구의 0부터 세는 슬롯 값(서리 감옥 `s, 1` · 수액 늪 `t, 0` · 연막탄 `h, 3` — 아래 4절).
- 지속 범위 스킬 대상에게 상태 VFX를 붙일지에 대해 상위 Plan 180행: O-8 원문은 「강화 스킬」만 말하므로 새로 넣지 않았고, **서리 감옥 V4는 첫 판부터 있던 항목**이다 — 수액 늪의 유닛 상태 VFX 항목은 기획에 없다 [확인].

---

## 4. 현재 자산 · 셋업 도구

### 4-1. 자산 `Assets/_Project/Resources/Config/Skills/`

| 자산 | 값 | 등급 |
|---|---|---|
| `Skill_Spirit_FrostPrison.asset` 16~32행 | `_isAvailable: 0` · `_aimType: 1`(지점) · `_cooldown: 10` · `_mechanic: 0`(타입 A 자리표시) · `_radius: 2` · `_duration: 0` · `_totalDamage: 150` · `_statusKind: 0` · `_magnitude: 0` · `_targetsAllies: 0` · `_impactDelay: 0` · `_activationVfxPrefab: {fileID: 0}` · `_vfxReferenceRadius: 2`. `_statusVfx*` 키 없음(기본값 — 1차 강화 자산 `Skill_Human_TacticalOrder`에는 3개 있음) | [확인] |
| `Skill_Trans_SapSwamp.asset` 16~33행 | 같은 자리표시 값 | [확인] |
| `Skill_Human_SmokeBomb.asset` | 같은 자리표시 | [전달] |
| 로드아웃 | 서리 감옥 · 수액 늪이 들어 있다. 옛 `Skill_*_Freeze` · `Skill_Trans_Slow`는 로드아웃에 없다(자산은 보존) | [전달] |
| 프리팹 | `Prefabs/VFX/Skills/`에 `vfx_emp_disabled` · `vfx_emp_pulse` · `vfx_meteor_fall` 3개뿐 — 서리 · 수액 프리팹 없음 | [확인] |

### 4-2. 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`

| 행 | 내용 | 등급 |
|---|---|---|
| 91 | `ExpectedAvailableCount = 6` → 두 스킬을 켜면 **8** | [확인] |
| 163~176 | `PointPlaceholder` 정의 — `IsAvailable = false`, 타입 `InstantAreaDamage`, 쿨다운 10 · 반경 2 · 지속 0 · 피해 150 | [확인] |
| 263 | 연막탄 `PointPlaceholder("Skill_Human_SmokeBomb", h, 3, …)` | [확인] |
| 272 | 서리 감옥 `PointPlaceholder("Skill_Spirit_FrostPrison", s, 1, …)` | [확인] |
| 284 | 수액 늪 `PointPlaceholder("Skill_Trans_SapSwamp", t, 0, …)` | [확인] |

### 4-3. 화면 · 서버 거부

- 사용 불가 스킬은 서버가 쿨다운 전에 거부한다(`Scripts/Application/UseCases/SkillActivationUseCase.cs` 152~157) [전달].
- 새 타입의 패널 임시 라벨은 기본값 「?」(`BuildingSkillPanelUI.cs` `DeriveSkillLabel` 254) — EMP 선례 [전달].

---

## 5. 새 기능 설계 — 지금 장치와 새로 필요한 것

### 5-1. 지금 있는 장치

| 장치 | 동작 | 출처 | 등급 |
|---|---|---|---|
| 타입 B 장판 | 발동 순간 **1회** DoT를 거는 것 — 구역이 아니다 | `UnitCombatUseCase.cs` 2425~2447 | [전달] |
| MistShrine 물안개(유일한 지속 판정 선례) | 매 틱 대상 재수집(510 · 523), 벗어나면 즉시 끊김(Buildings 「MistShrine」 규칙 9), 겹치면 가장 가까운 하나(규칙 13), 시전 건물 파괴 시 제거 `OnShrineDestroyed`(692~698) | `MistShrineUseCase.cs` | 692~698 [확인] / 나머지 [전달] |

### 5-2. 구역 엔티티 `[제안 — 프로그래머]`

- 서버 권위(스킬 규칙 25). 구역 Id(= 발동 순번) · 시전 팀 · 슬롯 · 시전 건물 Id · 중심(도메인 좌표) · 반경 · 남은 / 총 시간 · 효과(종류 · 강도 · 아군 여부) · 구성원 집합 [전달].
- 새 타입 `SkillMechanicType` = **4**(맨 뒤)로 서리 감옥 · 수액 늪 · (나중에) 연막탄을 함께 다룬다 [전달]. 열거형은 정수로 저장되므로 새 멤버는 항상 맨 뒤에 둔다는 주석이 있다(`Scripts/Domain/Skill/SkillMechanicType.cs` 19~20, 지금 마지막 멤버 `AreaTowerDisable` = 3) [확인].

### 5-3. 「구역 안」 판정 기준

- 지금 스킬 범위 판정은 모두 「유닛이 선 **타일 중심**(`_mapper.HexToWorld(unit.Position)`) ↔ 조준점」 유클리드 거리 ≤ 반경이다(`UnitCombatUseCase.cs` 2596~2605) [확인 — 2596행]. EMP(`BuildingStatusEffectSystem.DisableEnemyTowersInRadius`) · 물안개(632)도 같은 방식 [전달].
- `unit.Position`은 서버 이동이 **헥스 경계를 넘을 때** 커밋된다(`UnitView.MovementAuthority.cs` 336 · 819 → `UnitMovementUseCase.cs` 527) [전달]. 그래서 구성원은 **타일 경계를 지날 때만 바뀐다** — EMP R-13과 같은 일관성(모델 가장자리만 원에 걸쳐도 대상이 아니다).

### 5-4. 판정 주기 · 자리

| 모드 | 주기 | 자리 | 등급 |
|---|---|---|---|
| 멀티 서버 | `NetworkCombatController.TickCombat` `_attackInterval` — 씬 값 **0.1초**(`Game.unity` 45954행, 코드 기본값 0.05) | 상태 틱(`NetworkCombatController.cs` 802) **앞** · 유닛 전투 루프 앞 → 같은 틱에 공격 관문 `CanAttack`(886)이 구역 결과를 본다 | 802 · 886 · 씬 값 [확인] |
| 싱글 | `GameBootstrapper.Update` 매 프레임 | 유닛 상태 틱(`GameBootstrapper.cs` 753) 앞, `!IsNetworkMode()` 가드(EMP 765 선례) | 753 · 765 [확인] |

- 권위 틱 폴링 방식이라 비용이 작고, **구역 안에서 막 생산된 유닛도 다음 틱에 자동으로 포함된다** [전달].

### 5-5. 같은 종류 상태가 겹칠 때 (상위 Plan 4절 7 뒤 절반 · 1차 강화 Task D11)

| 사실 | 출처 | 등급 |
|---|---|---|
| `UnitStatusState.ApplyOrRefresh` — 같은 `Kind`가 있으면 그 레코드를 **통째로 교체**(중첩 없음) | `Scripts/Domain/Status/UnitStatusState.cs` 43~57(교체 52) | [확인] |
| 이동 배율 = `Freeze`가 있으면 0(최우선), 아니면 모든 `MoveSpeedMul` 강도의 곱 | 같은 파일 105~117 | [확인] |
| `StatusEffectSystem`에 종류별 · 출처별 제거 API가 없다 — `RemoveUnit` · `ClearAll`뿐이고 호출 0건 | `Scripts/Application/Services/StatusEffectSystem.cs` 163 · 169 | [확인 — grep] |
| 같은 종족끼리 경기 가능 — `GameRaceContext.Reset()`이 양쪽을 `Human`으로 둔다 → **초월 대 초월**에서 상대 순풍(1.3)과 내 수액이 같은 유닛에 실제로 겹칠 수 있다 | `GameRaceContext.cs` 62~63 | [확인 — 기본값] / 같은 종족 선택 가능 [전달] |

세 방식 `[전달 — 프로그래머]`:

| 방식 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **Z1** 짧은 재부여 | 틱마다 구역 구성원에게 짧은 상태(2틱)를 기존 상태 시스템으로 다시 건다 | 코드 최소 | **순풍 소실 버그** — 수액(`MoveSpeedMul`)이 같은 종류인 순풍 레코드를 교체해, 늪을 나간 뒤 순풍이 사라진다. 이탈 해제가 최대 2틱 늦음. 진단 혼선 |
| **Z2** 출처별 기록 | (종류, 출처)로 저장 · 곱 합성 · `Remove(unit, 출처)` | D11의 일반 해결 | Domain 합성 변경이 **1차 강화 4종 전체**에 닿는다. 자가 검증 B-2(`SupportBuffSkills.cs` 123~154) 보존 필요. `StatusAppliedClientRpc`에 슬롯이 없어 출처 키가 (팀, 종류)가 된다 |
| **Z3** 별도 구역 상태 서비스(프로그래머 추천) | 구역 · 구성원을 새 서비스가 들고, `UnitCombatUseCase` 접근자만 「연구 × 상태 × 구역」으로 합성(`GetUnitMoveSpeedMultiplier` 504~520 · `CanAttack` 527~533) | `StatusEffectSystem` · `UnitStatusState` 무변 → **1차 회귀 0**. 이탈 · 종료 즉시 해제. 구역 겹침 규칙을 따로 둘 수 있다. 연막탄 회피 질의도 같은 자리 | 상태를 읽는 곳이 두 군데. 전역 대 전역 같은 종류 겹침(D11 앞 절반)은 미해결 — 지금 그런 조합 없음 |

- 접근자 두 곳의 현재 모습 [확인]: `GetUnitMoveSpeedMultiplier`(504~520)는 연구 배율 × `_statusSystem.GetMoveSpeedMultiplier(unit.Id)` + 강화 진단 표본, `CanAttack`(527~533)은 `_statusSystem.CanAttack(unit.Id)`만 본다.
- 빙결 정지 애니메이션은 **이동 배율 0을 읽어** 켜진다(`UnitView.Movement.cs` 968 `SetFrozenAnimation(multiplier <= MoveFreezeEpsilon)` 등) → Z3 접근자 합성만으로 동작한다 [확인 — 968행] / 결론 [전달]. 유닛 사망 시 구성원에서 뺀다 [전달].

---

## 6. 기존 상태 종류 · 빙결

| 사실 | 출처 | 등급 |
|---|---|---|
| 상태 종류 5종 + None: `MoveSpeedMul` 1 · `AttackPowerMul` 2 · `AttackDisabled` 3 · `Freeze` 4(이동 0 + 공격 불가) · `HealOverTime` 5(상태 시스템에 저장 안 함) | `Scripts/Domain/Status/StatusEffectKind.cs` 30~47 | [확인] |
| 옛 `Skill_Spirit_Freeze.asset` — 타입 C · `_statusKind` 4 · 3초 · 쿨다운 15 · 전역, 로드아웃에서 빠짐. 경로 `ApplySkillGlobalStatus` → `_statusSystem.Apply`(`UnitCombatUseCase.cs` 2461~2498) | — | [전달] |
| 공격 정지: `CanAttack` false → `TryFindTarget`이 대상을 찾지 않는다(멀티 `NetworkCombatController.cs` 886, 싱글 `UnitCombatUseCase.cs` 667). 적이 사거리 안이면 전투 자세는 유지되고 피해만 없다(877~879 주석) | — | 886 [확인] / 나머지 [전달] |
| 이동 정지: 서버 이동 코루틴이 매 프레임 배율을 읽는다(`UnitView.Movement.cs` 48 · 65) | — | [전달] |
| 빙결 애니메이션 `SetFrozenAnimation`(`UnitView.cs` 3482~3496): `Animator.speed = 0`, 공격 연출 닫기("frozen-priority"), `GameEvents.OnUnitFreezeChanged` 발행(3494). 멀티는 서버가 이 이벤트를 받아(`NetworkCombatController.cs` 2676~2702) `UnitAnimState.Frozen`을 복제 | — | [확인] |
| **빙결 표현 공백:** `SetFrozenAnimation`은 이동 · 추격 루프에서만 불린다(`UnitView.Movement.cs` 968 · 1105 · 1426 · 2408 — `MoveAlongPathV3` · `EnterCombatPursuitV3`). 전투 루프(`EnterCombatLoopV3`, 2660~)에는 없다 → **제자리에서 싸우던 유닛이 얼면 애니메이션이 계속 돈다.** V4를 씌우면 눈에 띌 수 있다 | — | 호출 4곳 [확인] / 전투 루프에 없음 [전달] |
| 빙결 직전에 확정된 공격(착탄 대기 중)의 처리 — 미확인. 서버 투사체는 발사 뒤 상태와 무관하게 착탄한다(programmer 메모리 VFX §9) | — | [전달 — 미확인 포함] |

- `[문서 담당]` 빙결은 이동 0이므로 **서리 감옥 안에서 언 적은 스스로 걸어 나갈 수 없다** — 서리 감옥의 「나가면 해제」 장면은 사실상 구역이 끝날 때 · 죽을 때만 생길 것으로 보인다(밀어내는 효과가 있는지는 확인하지 않음). 「걸어 나가면 해제」는 수액 늪에서 본다 — Plan 테스트 절차에 반영. 프로그래머 확인 필요.

---

## 7. 동기화 — Client에 무엇이 필요한가

- Client에 필요한 것: **구역 VFX**(위치 · 반경 · 남은 시간)와 **유닛 상태 VFX**(누가 구역 안인가). 게임 효과는 서버만 판정하므로 Client에 상태 사본은 필요 없다 [전달].
- 지금 RPC로 안 되는 이유: `StatusAppliedClientRpc(teamInt, kindInt, magnitude, duration)`는 **팀 단위**다(`NetworkSkillController.cs` 424 — 상위 Research 2-6) [확인]. 또 **Client에서는 `UnitData.Position`이 이동으로 갱신되지 않는다**(`ProcessStep` · `CommitPreflighted…` 모두 서버 전용, grep 범위 안 다른 경로 없음) [전달] → Client가 도메인 데이터로 구성원을 다시 계산할 수 없다.

| 안 | 구역 VFX | 유닛 V4 | 스키마 | 한계 |
|---|---|---|---|---|
| **N-A** | C-1 발동 채널 재사용 — Client가 팀 · 슬롯으로 자기 빌드 스킬 데이터의 반경 · 지속 · 프리팹을 찾고 로컬 타이머로 끝 | Client가 화면 Transform → `ViewConverter.FromView` → `HexMetrics.WorldToHex`(`Core/HexMetrics.cs` 165)로 스스로 판정 | 8 그대로 | 보간 지연만큼 경계 유닛이 어긋남(1차 강화 D2보다 크다) |
| **N-B**(프로그래머 추천) | C-1 재사용(N-A와 같음) | 서버가 틱마다 **바뀐 것만** 묶어 `ZoneMembershipChangedClientRpc(구역 순번, enteredIds[], exitedIds[])` — EMP `int[]` 선례(`TowersDisabledClientRpc`, 330) | **8 → 9** + fixture 두 곳(`RunUnitActionSelfValidation.ServerProjectiles.cs` 260 · `RunUnitActionSelfValidation.SupportBuffSkills.cs` 260) + `UnitAttackShadowProfileResolver.cs` 33 | 대상은 서버와 정확히 같고 전송 지연만 있다(EMP R-7) |
| **N-C** | 구역 생성 · 종료 RPC 따로 + N-B | N-B와 같음 | 9 | 구역이 **일찍 끝날 수 있을 때만** 필요(시전 건물 파괴 시 구역 소멸로 정할 때 — Plan Z-7) |

- 확인 [확인]: `HexMetrics.WorldToHex` 165행, `TowersDisabledClientRpc(int sequence, float duration, int[] towerIds)` 330행, 스키마 상수 `PresentationContractSchemaRevision = 8`(33행), fixture 두 곳 모두 `== 8` 검사(각 260행).

---

## 8. 구역 VFX 경로

| 사실 · 제안 | 출처 | 등급 |
|---|---|---|
| 발동 VFX는 **1회성**: `PlaySkillActivationVfx` — 회전 항등, 배율 = 반경 ÷ 기준 반경(`ComputeSkillVfxScale` 601), 루프 · 정지 없음 | `EffectManager.cs` 573~591 · 601 | 601 [확인] / 나머지 [전달] |
| 재사용할 루프 장치: `VfxPoolItem.StatusLoop` — `BeginStatusLoop` 확인 번호, `StopStatusLoop(lease, clear)` 꼬리 / 즉시 | `VfxPoolItem.StatusLoop.cs` 58~100 | `BeginStatusLoop` 65행 [확인] / 나머지 [전달] |
| 시작 패턴 선례 `StartTowerStatusVfx`(627~662): `GetOrCreateVfx` → `Play` → `BeginStatusLoop`, 실패 시 반환 | `EffectManager.cs` | 627 [확인] |
| `[제안 — 프로그래머]` `StartSkillZoneVfx(prefab, viewPos, scale, out lease)` / `Stop…` 추가, 풀 항목은 VFX 컨테이너 아래 | — | [전달] |
| 크기 `ComputeSkillVfxScale(skill.Radius, 기준 반경)`. 높이 = 조준점 + `GameConfig.SkillVfxGroundYOffset` **0.06**(`GameConfig.cs` 222), `SkillActivationVfxPresenter` 방식(205~209) | — | 222 [확인] / 205~209 [전달] |
| 위험 `[추정]`: 금지 빗금 오버레이도 높이 0.06이라 겹치면 z-fighting(programmer `vfx-presentation.md` §13) | — | [전달 — 추정] |
| 정지(EMP D8 선례): 만료 · 경기 종료 = 꼬리, 재경기 = 즉시, Client는 로컬 타이머 | — | [전달] |
| 칸: `SkillDefinition` 「발동 VFX」 헤더 뒤 `_zoneVfxPrefab`, 기준 반경은 `_vfxReferenceRadius`(2.0) 공유 또는 따로 `_zoneVfxReferenceRadius`(Plan Z-13). 셋업 도구는 이 칸을 건드리지 않는다(1차 강화 D10 선례) | — | [전달] |
| 루프 계약 검사 `CheckUnitBuffLoopContract`(716) 재사용 — 파티클 1개 이상 · 모두 Looping ON · Stop Action None | `EffectManager.cs` 716~728 | [확인] |

---

## 9. 유닛 상태 VFX(V4) — 지금 장치 `UnitBuffVfxPresenter`가 맞지 않는 점

`Scripts/Presentation/Effects/UnitBuffVfxPresenter.cs`(1차 강화 Task가 만든 장치):

| 구분 | 내용 | 등급 |
|---|---|---|
| 맞지 않는 점 | `skill.Mechanic != GlobalStatusChange`면 빠진다(238). 대상 = 알림 순간 대상 팀 **전원**(269 · 275~), 끝 = `now + Duration` **고정**(271) | [확인] |
| 다시 쓸 수 있는 것 | 기록 열쇠(유닛 Id, 시전 팀, 슬롯)(73~92), `TryStart`(대기 · `ProjectNow` · `StartUnitBuffVfx`)(339~380), `LateUpdate` / `TickEntry` 따라가기 · 사망 · Pose 소실(384~465), 사망 즉시 · 경기 종료 꼬리 · `Reinit`(467~522) | [전달] |
| 넓혀야 할 것 | 명시적 시작 · 정지 진입점(진입 → 시작, 이탈 · 구역 끝 → 정지), 끝 시각 없음(또는 안전용 구역 종료 시각), 같은 스킬 구역 겹침 참조 수 | [전달] |

두 방식 `[전달 — 프로그래머]`:
- **(가)** 공통 부분을 떼어 「발동 구동기」(지금 강화 스킬 경로) + 「구역 구성원 구동기」(새 경로) **두 입력 구조**로 바꾼다 — 중복 없음, 완성도 우선. 직전 작업 코드를 바꾼다.
- **(나)** 별도 `UnitZoneStatusVfxPresenter` 신규 — 기존 무변, 약 200줄 중복.

칸 · 크기 [전달]:
- 칸: `_statusVfxPrefab` / `Scale` / `Offset`을 구역 스킬에도 쓸 수 있다. 헤더 · 툴팁의 「타입 C」 문구는 고쳐야 한다.
- 크기: 지금은 키 ÷ 1.0(+ 수동 배율), clamp [0.1, 10](`UnitEffectConfig` `TryGetRecipientFit` · `UniformScale`). 기획 식(반경 항 · 상한)은 코드에 없다 → 결정 20.

---

## 10. 경기 종료 · 재경기 · 파괴 · 사망 · AI

| 상황 | 사실 · 제안 | 등급 |
|---|---|---|
| 경기 종료 | `OnGameEnd`에서 구역을 닫는다(전부 해제 · 이후 생성 거부), VFX는 꼬리. 선례 `GameBootstrapper.cs` 549~555(착탄 예약 닫기 · EMP `Close`) | 선례 행 [확인] / 제안 [전달] |
| 재경기 | UseCase · 서비스가 맵 로드마다 새로 만들어진다(`GameBootstrapper.Setup.cs` 409 · 441), Presenter `Initialize`가 즉시 정지 | [전달] |
| 유닛 사망 | 구성원에서 빼고 V4 즉시 정지(D8) | [전달] |
| 시전 건물 파괴 | 선례가 갈린다 — 스킬 규칙 11 「착탄 전에 시전 건물이 파괴돼도 착탄은 그대로」 vs 물안개 Buildings 「MistShrine」 규칙 12 · 25 「물안개 즉시 제거」 → Plan Z-7 | 규칙 문장 [확인] |
| AI | 스킬을 쓰지 않는다(상위 Research 2-11, `AIOpponentController.cs`에서 `skill` grep 0건). 다만 **AI 유닛이 얼거나 느려지는 일은 일어난다**(내가 AI 유닛에게 쓴다) | grep 0 [확인] |

---

## 11. 진단 로그 제안 `[제안 — 프로그래머]`

접두어 `[SKILL-ZONE]`, `Diag=SkillZone`, `DiagRev=1`, `GameLog.Dev`(Conditional 2개), **상태가 바뀔 때만** 남긴다(LogRules §1.14 금지 8) [전달].

| 쪽 | 이벤트 | 키 |
|---|---|---|
| 서버 | `ZoneCreated` | `Seq` · `Team` · `Slot` · `Kind` · `MagMilli` · `Radiusmm` · `DurationMs` · `Aimmm` · `InitialMembers` |
| 서버 | `ZoneMembersChanged` | 틱 단위 묶음 `Entered` / `Exited` Ids · `Reason=Walked/Died` |
| 서버 | `ZoneEnded` | `Reason=Expire/GameEnd/CasterDied` · `ActualMs` · `TotalEntered` · `PeakMembers` · `AttackGateBlocks`(횟수만) |
| 서버 | `ZoneOverlap` | 같은 스킬 구역이 처음 겹친 순간 · `Applied` 규칙 |
| 서버 | `ZoneMoveSample` | 유닛 · 구역당 첫 1회 `WorldSpeedmm` · `Mul`(1차 강화 `BuffMoveSample` 선례) — 수액 배율 · 순풍 × 수액 혼합 판정 |
| Client | `ZoneMembershipReceived` | Host와 Ids 대조 |
| 화면 | `ZoneVfxStarted` | `RootYmm` · `AboveTileTop` · `Scale` · `ViewFlipped` · `Channel` |
| 화면 | `ZoneVfxStopped` | `Path` · `Immediate` · `LeaseMatched` |
| 화면 | `ZoneVfxSkipped` | `NoPrefab`(Warn) |
| 화면 | `UnitZoneVfxStarted` / `Stopped` / `Skipped` | `Skipped`는 `NoPrefab` |

- 사용 불가 거부는 기존 `LogActivationRejected(Unavailable)`를 쓴다 [전달].

---

## 12. 변경 예상 파일 · 위험 · 자가 검증 후보 `[전달 — 프로그래머, Z3 + N-B 기준]`

### 12-1. 변경 예상 파일

| 레이어 | 파일 |
|---|---|
| Domain | `SkillMechanicType.cs`(+4) |
| Application | 새 구역 서비스 · 새 실행기, `SkillExecutorRegistry.cs`, `SkillActivationContext.cs`(새 다리는 **선택 인자 맨 뒤** — `SupportBuffSkills.cs` 190이 인자 9개를 위치로 호출), `SkillActivationUseCase.cs`(다리 · `TickZones` · `Close` — 틱 진입을 여기 두면 `IGameServices`(fixture 대역 `RunUnitActionSelfValidation.cs` 76)를 늘리지 않는다), `UnitCombatUseCase.cs` 접근자, `GameEvents.cs`, `SkillData.cs` |
| Infrastructure | `SkillDefinition.cs`, `NetworkSkillController.cs`(RPC), `NetworkCombatController.cs`(802 앞 틱), `UnitAttackShadowProfileResolver.cs` 33 |
| Bootstrap | `GameBootstrapper.cs`(`Update` · `OnGameEnd`), `GameBootstrapper.Setup.cs`(주입 · `OnUnitDied`), `GameBootstrapper.Map.cs`(배선) |
| Presentation | `EffectManager.cs`, 새 구역 VFX Presenter, `UnitBuffVfxPresenter.cs` 또는 새 Presenter |
| Editor · 데이터 | `SupportSkillLoadoutSetup.cs`(91 · 272 · 284), 자가 검증 새 파일 + 스키마 fixture 2곳, 두 자산 |

### 12-2. 위험

- Z1이면 순풍 소실 · 스키마 9면 **두 빌드(에디터 · 기기) 모두 새로 설치** · 제자리 전투 빙결 애니메이션 미정지 · 0.06 z-fighting `[추정]` · 새 타입 라벨 「?」 · 이탈 꼬리와 빠른 재진입 반복.

### 12-3. 자가 검증 후보

타일 이동 진입 · 이탈, 같은 스킬 겹침 1회 적용, 순풍 × 수액 곱(+ 이탈 뒤 순풍 1.3 그대로), 빙결 = 이동 0 · `CanAttack` false · 이탈 / 만료 뒤 정확히 1.0, 종료 후 거부, 사망 정리, 시전 건물 파괴(결정대로), 구역 없을 때 회귀, 스키마 == 9, 셋업 기대 수 8.

---

## 13. 결정 후보 (프로그래머 추천) `[전달]`

Plan 「결정 요약」의 바탕이다. 메인 세션 추천과 근거 · 다른 안은 Plan에 있다.

| # | 프로그래머 추천 |
|---|---|
| Z-1 | Z3 별도 구역 상태 서비스 |
| Z-2 | N-B, 스키마 9 |
| Z-3 | 타일 중심 ↔ 조준점 |
| Z-4 | 권위 틱 |
| Z-5 | 같은 스킬 겹침 1회 적용(스킬 규칙 13, 물안개 규칙 13) |
| Z-6 | 다른 출처 같은 종류는 곱 합성(확정 9-4 곱연산 · 스킬 규칙 13) |
| Z-7 | 시전 건물 파괴 시 구역 유지(스킬 규칙 11 선례; 물안개는 반대) |
| Z-8 | 경기 종료 = 전부 해제 + VFX 꼬리(EMP Q5 · D8) |
| Z-9 | 이탈 V4 = 꼬리(사망만 즉시) |
| Z-10 | 임시 수치 — 서리 반경 2 · 구역 4초 · 쿨다운 15, 수액 반경 2 · 이동 0.4배 · 6초 · 쿨다운 15(옛 Freeze 3초 · 15, 옛 Slow 0.5 · 5초 · 15, EMP 5초 · 10, 버프 8초 · 15; 「강한 둔화」라 0.5보다 강하게) |
| Z-11 | 제자리 전투 빙결 애니메이션 = 범위 밖 기록 |
| Z-12 | V4 큰 유닛 보정 = 현행 키 배율 + 스킬 크기 배율로 시작 |
| Z-13 | `_zoneVfxPrefab` 새 칸, 기준 반경 공유 |
| Z-14 | 유닛 상태 칸 재사용(문구 일반화), 수액 칸은 비워 둠 |
| Z-15 | 서리 발동 연출은 기존 발동 칸으로 충분 |
| Z-16 | 새 타입 하나(= 4) |
| Z-17 | 새 상태 종류 추가 안 함 |
| Z-18 | 테스트 임시 프리팹 — 구역 칸 `vfx_emp_disabled`, 유닛 칸 `vfx_bloomfairy_heal`, 테스트 뒤 비움 |
| Z-19 | 로그 존속 `개발` |
| Z-20 | 두 스킬 사용 가능 + 셋업 두 줄 · 기대 수 6 → 8 |

- 「확정 9-4 곱연산」의 출처: 이력 `History/ImplementationVerificationHistory.md` 2749행이 타입 C 구현을 「유효 스탯 합성(곱연산 · 확정 9-1 · 9-4)」로 적는다 [확인]. 그 「확정 9-4」 원문(2026-07-28 스킬 건물 설계 Task로 보인다)은 이번에 열지 않았다.
- Z-18 임시 프리팹 루프 계약 [확인 — 프리팹 YAML grep만, Unity 실행 아님]: `vfx_emp_disabled`는 파티클 5개 모두 `looping: 1` · `stopAction: 0`(None), `vfx_bloomfairy_heal`(`Prefabs/VFX/Units/`)은 4개 모두 같다 → `CheckUnitBuffLoopContract` 조건을 YAML상으로는 만족한다. 다만 `vfx_emp_disabled`는 **탑 지붕 위 스파크용**(링 반경 0.5 — 상위 Plan 526행)이라 바닥 구역 모양 · 크기와는 다르다 — 경로 확인용일 뿐이다.

---

## 14. 범위 밖 관찰 — 기록만

- `StatusEffectSystem.RemoveUnit` · `ClearAll` 호출 0건(163 · 169) → 죽은 유닛의 상태 기록이 만료까지 남는다 [확인 — grep] / 영향 [전달]. 이번 작업에서 고치지 않는다(CLAUDE.md 규칙 6).

---

## 15. 이번 조사에서 확인하지 않은 것

- Client에서 `UnitData.Position`이 이동으로 갱신되지 않는다는 결론(7절)은 programmer의 grep 범위 안 결론이다 — 문서 담당은 다시 확인하지 않았다 [전달].
- 빙결 직전에 확정된 공격(착탄 대기 중)의 처리(6절).
- 금지 빗금 오버레이와 구역 VFX의 z-fighting(8절) `[추정]`.
- 서리 감옥에서 언 적이 구역을 벗어날 방법이 있는지(밀어내기 등 — 6절 문서 담당 관찰).
- 같은 종족끼리를 로비에서 실제로 고를 수 있는지(기본값만 확인 — 5-5).
- `UnitBuffVfxPresenter.cs`의 73~92 · 339~522행 구조, `EffectManager.cs` 573~591, `VfxPoolItem.StatusLoop.cs` 58~100, `MistShrineUseCase.cs` 510 · 523 · 632, `UnitMovementUseCase.cs` 527 등 `[전달]` 표기 행.
- 「확정 9-4」 원문(13절).

---

## 16. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-11 | 첫 판. 사용자 「1번 작업 진행해보자 task 문서 작성진행해」로 작성. game-programmer 읽기 전용 조사(HEAD `099e8a1`) 1~11절 전부를 옮기고, document-manager가 직접 다시 읽은 행은 [확인]으로 표시. 코드 · 자산 · 규칙 문서 · 다른 Task 문서 · 메모리 무변경 |
