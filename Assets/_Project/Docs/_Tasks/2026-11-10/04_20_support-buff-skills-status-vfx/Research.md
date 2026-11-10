# Research — 지원 스킬 1차 강화 4종 + 강화 상태 VFX 경로

## 이 문서는 무엇인가

세 종족의 스킬 건물에는 「맵 전체 아군을 잠시 강하게 만드는」 강화 스킬이 들어 있다. 그중 **지금 있는 기능만으로 만들 수 있는 네 개** — 인간 **전술 지령**(공격력 부분), 정령 **화염의 축복**(공격력), 초월 **숲의 포효**(공격력) · **숲의 순풍**(이동 속도) — 는 슬롯에 이름만 있고 「사용 불가」로 꺼져 있다. 이 작업은 이 네 스킬이 **실제로 효과를 내도록** 하고, 효과를 받는 아군 유닛 몸에 붙는 **강화 상태 이펙트(VFX)** 를 나중에 프리팹만 만들어 스킬마다 칸에 넣으면 **바로 화면에 보이도록** 그 길을 미리 깔아 두는 것이다.

이펙트 그림은 스킬마다 다르다(사용자: 「각 종족별 강화 vfx는 다른 이펙트를 사용할 건데?」). 그래서 **그림은 스킬마다 따로 넣는 칸**에 두고, 「유닛에 붙이고 · 따라다니게 하고 · 끝나면 떼는」 **장치만 네 스킬이 함께 쓴다.** 이펙트 프리팹은 아직 하나도 없으므로, 이번 작업이 끝나도 칸은 비어 있고 효과(공격력 · 이동 속도 증가)만 동작한다. 칸이 비어 있다는 사실은 진단 로그 한 줄로 알린다.

이 문서는 **지금 코드가 어떻게 되어 있는지**(어디를 고쳐야 하고 무엇이 함께 영향을 받는지)를 정리한다. 무엇을 어떻게 만들지와 사용자가 정할 항목은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

**사용자 대화 원문(메인 세션 전달, 2026-11-10):**
1. 「vfx 관련작업중 vfx 제작만 하면 바로 사용해서 적용할수있도록 구현을 먼저 해두고싶은데 어떤게좋을까 ?」 → 메인 세션 추천: 1차 강화 4개 + 상태 VFX 경로
2. 「근데 각 종족별 강화 vfx는 다른이펙트를 사용할건데 ?」 → 메인 세션 답: 그림은 스킬별 칸, 붙이는 장치만 공유
3. 「그 작업으로 진행해보자」 → 이후 「작성해」(이 Research · Plan 작성 승인, Testcase 제외)

**출처 · 표기:** 아래 사실은 game-programmer의 HEAD `624a25a` 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋의 파일을 직접 다시 읽어 행 내용을 대조한 것만 **[확인]** 으로 적었다. `[추정]`은 조사자 스스로 확인하지 못했다고 밝힌 것이다. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-1 · 1-2 · 1-3(슬롯), 2절(스킬별 효과), 2-1(V12 · V13 · V16~V21 · V23), 3-2 「1차 — 기존 기능으로 가능한 것」, 4절 3 · 7 · 19 · 20(열린 물음). **선례:** [EMP Task](../../2026-11-04/03_54_emp-tower-disable/Plan.md#11-implementation-and-user-test)(상태 VFX 루프 · 확인 번호 · 진단 로그 · 셋업 도구 「그 줄만」).

---

## 1. 범위

| 이번에 하는 것 | 이번에 하지 않는 것 |
|---|---|
| 네 스킬 켜기(지속 · 상태 종류 · 강도 · 사용 가능 값) | 전술 지령의 **생산 속도** 부분(새 상태 종류 — 상위 Plan 2-S) |
| 스킬마다 **강화 상태 VFX 칸** + 크기 · 위치 보정 칸 | 강화 상태 VFX 프리팹 제작(VFX 기획 · 제작 단계 — 상위 Plan 3-0) |
| 대상 유닛에 상태 VFX를 붙이고 따라다니게 하고 끝나면 떼는 화면 장치 | 대지의 갑주 · 재생의 뿌리 · 신목의 각성(새 상태 · 건물 대상 — 2-S · 2-B) |
| 즉시 발동 스킬의 **발동 VFX** 재생 길(지금 막혀 있음 — 8절) | 수치 확정(밸런싱 단계 — 상위 Plan 3-3) |
| 테스트 항목별 진단 로그(WORKFLOW [4] 「테스트 항목 진단 로그 규칙」) | 스킬 버튼 라벨 · 아이콘 · 설명 탭(상위 결정 16 — 추후 UI 작업) |

사용자 결정이 필요한 항목은 Plan 「결정 요약」 표(D1~D14)에 모았다.

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 3(건물 글로벌 쿨다운) · 6(종족별 스킬셋) · 7(데이터 주도 — 요소 표) · 13(타입 C 전역 상태변경 — 연구 배율과 곱연산) · 15(즉시 발동) · 25(서버 권위 · 발동 표현 · 「조준이 없는 즉시 발동 스킬의 재생 위치는 아직 정하지 않았다」) · 26(즉시 발동은 건물 생존 · 쿨다운만 재검증)
- `Assets/VFXSFXGuide.md` 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 1(상태 이펙트 파티클 계약) · 2(유닛에 붙는 상태 이펙트의 모양) · 「표현 pose를 저장해 매 프레임 읽는 소비자」(`ProjectNow()` 계약)
- `WORKFLOW.md` [3] · [4](「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」)
- 상위 Plan 1 · 2 · 2-1 · 3-2 · 4 · 5절, EMP Plan 전체

---

## 3. 타입 C 실행 경로 — 지금 어떻게 동작하나

### 3-1. 실행기 `Scripts/Application/Skill/GlobalStatusChangeExecutor.cs`

| 행 | 내용 | 등급 |
|---|---|---|
| 38 | 지속 ≤ 0이면 **조용히 끝** | [확인] |
| 43 | 상태 종류가 `None`이면 **조용히 끝** | [확인] |
| 46 | `TargetsAllies`면 대상 = 시전 팀, 아니면 반대 팀 | [확인] |
| 51 | `ctx.ApplyGlobalStatus(대상 팀, 종류, 강도, 지속)` | [확인] |

### 3-2. 적용 `Scripts/Application/UseCases/UnitCombatUseCase.cs` `ApplySkillGlobalStatus`(2448~2476)

- 그 팀의 **지금 살아 있는 유닛만** 돌며 `_statusSystem.Apply`(2469) — 발동 순간의 목록이다(2451 순회) [확인].
- `raiseNetworkEvent`가 참(서버 권위 발동)일 때만 `OnGlobalStatusApplied` 발행(2474~2475) [확인].

### 3-3. 공격력 `EffectiveAttack`(464~483)

- 유효 공격력 = **기본 × 연구 × 상태**, 마지막에 `RoundToInt`. 상태 배율이 정확히 1이면 기본값 그대로 [확인].
- 서버 투사체는 **발사 순간의 공격력**으로 고정된다(470~471, 저장 위치 `Projectiles.cs` 599) — 버프가 비행 중 끝나도 그 탄은 버프 공격력 [확인 470~471 / 599 전달].
- 이 값이 쓰이는 지점: 1950 · Quake 2270 · 3112 [전달].
- **화상 · 독 지속 피해는 고정값**이라 공격력 버프가 붙지 않는다(1822~1823 · 2236 · 2250) [전달]. 파도 피해가 버프를 받는지는 **미확인**[전달].

### 3-4. 이동 `GetUnitMoveSpeedMultiplier`(493~506)

- 배율 = 연구 × 상태, **상한 없음** [확인]. `StatusEffectKind.cs` 머리 주석은 `MoveSpeedMul`을 「0<m<1 = 둔화」로만 설명한다(m>1을 막지는 않는다) [확인].

### 3-5. 같은 종류가 다시 걸릴 때 `Scripts/Domain/Status/UnitStatusState.cs` 43~53

- 같은 `Kind`가 있으면 그 기록을 **통째로 교체**(강도 · 남은 시간 모두 새 값). 출처(어느 스킬인지)는 구분하지 않는다 [확인].
- 결과: 같은 스킬을 다시 쓰면 남은 시간이 처음부터 다시 시작한다. **다른 출처의 같은 종류가 겹치면 나중 것이 덮는다** — 예: 2-Z의 수액 늪(적 둔화, `MoveSpeedMul`)과 순풍(아군 가속, `MoveSpeedMul`)이 같은 유닛에 걸리면 나중 것만 남는다(상위 Plan 4절 7). 수액 늪은 적에게, 순풍은 아군에게 걸리므로 1차 안에서는 겹치는 사례가 없다 [전달].

### 3-6. 틱 · 정리

- 상태 시간 감소: 싱글 `GameBootstrapper.cs` 753~757, 서버 `NetworkCombatController.cs` 802, 순수 Client의 사본도 같은 Update에서 줄어든다 [전달].
- `RemoveUnit` / `ClearAll` 호출 0건 — 죽은 유닛의 기록은 만료로 사라진다. 유닛 Id가 단조 증가라(`UnitData.cs` 111 · 129 · 177) 재사용 충돌이 없어 무해 [전달].

### 3-7. 발동 순서 `Scripts/Application/UseCases/SkillActivationUseCase.cs`

| 행 | 순서 | 등급 |
|---|---|---|
| 155 | 「사용 가능」이 꺼져 있으면 거부(쿨다운 안 돎) | [확인] |
| 210 | 실행기 호출 | [확인] |
| 213 | 쿨다운 시작 | [확인] |
| 221 | 발동 확정 발행 | [확인] |

→ 실행기가 3-1의 38 · 43행에서 조용히 끝나도 **쿨다운 · 확정은 그대로 진행된다.**

### 3-8. 멀티 동기화 `Scripts/Infrastructure/Network/NetworkSkillController.cs`

- 서버의 `OnGlobalStatusApplied` → `StatusAppliedClientRpc(팀, 종류, 강도, 지속)`(410 · 424) — **팀 단위**이고 어느 스킬인지 · 어느 유닛인지는 싣지 않는다 [확인].
- 수신(426): `IsServer`면 건너뛰고, 순수 Client는 자기 도메인의 그 팀 생존 유닛에 같은 함수를 `raiseNetworkEvent: false`로 다시 적용 [확인].
- 상태 RPC는 발동 확정 RPC보다 **먼저 송신**된다(실행기 210 → 확정 221). reliable 채널에서 같은 순서로 도착한다는 것은 `[추정]`[전달].
- 피해 · HP는 서버가 계산하므로, 공격력 버프 효과(피해 숫자 · HP 감소)는 **양쪽 화면에 그대로 보인다** [전달].

### 3-9. 이동 배율 > 1(순풍)을 막는 곳 — 없음 (상위 Plan 4절 3의 답)

- `UnitView.Movement.cs` 966~967 · 1101~1102 · 1421~1422 · 2404~2405는 **하한만** 둔다. 978의 이동 허용 거리도 같은 배율을 쓴다. 네트워크 쪽 속도 상한 0건 [전달].
- **위험:**
  - 걷기 애니메이션 재생 속도가 이동 배율을 따르지 않아 **발이 미끄러져 보인다** — 연구 배율 1.32에서도 이미 같은 현상 [전달].
  - `UnitView.cs` 2356 진단 표본이 Client 사본의 배율 적용 지연(상태 RPC 도착 전후)을 이상으로 잘못 잡을 수 있다 `[추정]`[전달].
  - NetworkTransform이 빨라진 이동을 매끄럽게 따라가는지는 실기 확인이 필요하다 [전달].

---

## 4. 네 스킬 자산과 셋업 도구

### 4-1. 자산 값 — `Assets/_Project/Resources/Config/Skills/` `[확인]`

`Skill_Human_TacticalOrder.asset`(19~31행) · `Skill_Spirit_FlameBlessing.asset` · `Skill_Trans_ForestRoar.asset` · `Skill_Trans_ForestTailwind.asset` 네 개가 모두 같다.

| 칸 | 값 | 뜻 |
|---|---|---|
| `_isAvailable` | 0 | 사용 불가 |
| `_aimType` | 0 | 즉시 발동(조준 없음) |
| `_mechanic` | 2 | 타입 C |
| `_cooldown` | 15 | |
| `_duration` | **0** | |
| `_statusKind` | **0** | `None` |
| `_magnitude` | **0** | |
| `_targetsAllies` | 1 | 아군 |
| `_activationVfxPrefab` | 없음 | 발동 VFX 없음 |

- **「사용 가능」만 켜면** 실행기가 38 · 43행에서 조용히 끝나고 쿨다운 · 확정은 진행된다(3-7) → **효과 없이 쿨다운만 도는 스킬**이 된다.
- 켜려면 네 칸이 모두 필요하다: 지속 > 0 · 상태 종류(전술 지령 · 화염 · 포효 = 2 `AttackPowerMul`, 순풍 = 1 `MoveSpeedMul`) · 강도 > 0 · 사용 가능.
- **강도는 배율 그대로다**(1.5 = 1.5배, 3-3). 그런데 `SkillDefinition.cs` 94행 툴팁은 「강도(이속 배율·힐량 등, ×10 스케일)」라 배율도 10배로 넣어야 하는 것처럼 읽힌다 — 오해 소지 [확인].
- 참고 — 로드아웃에서 빠진 옛 임시 자산 `Skill_Human_Buff` · `Skill_Spirit_Buff` · `Skill_Trans_Buff`는 모두 타입 2 · 상태 2 · **강도 1.5 · 지속 8 · 쿨다운 15**다 [확인]. (`Skill_Human_Slow`는 상태 1 · 강도 0.5 · 지속 5 · 적 대상 [확인].)

### 4-2. 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` `[확인]`

- 네 스킬은 `InstantPlaceholder(...)`(176행 — 「상태 종류 None · 쿨 15」 자리표시)로 만들어진다: 전술 지령 242 · 화염의 축복 250 · 숲의 포효 265 · 숲의 순풍 267.
- **도구를 다시 실행하면** `WriteValues`가 자산 값을 덮어쓰고, 발동 VFX 칸도 `ResolveActivationVfx`(571)로 다시 정해 이 네 스킬은 **null로 돌아간다.** 따라서 자산 값을 바꾸면 이 네 줄도 함께 새 값으로 바꿔야 되돌아가지 않는다 — EMP 선례(220행 `var emp = new SkillSpec`).
- 이번에 새로 만드는 칸(강화 상태 VFX 등)은 `WriteValues`가 건드리지 않는다 [전달].
- 도구 퇴역은 「지원 스킬 작업 전체가 끝난 뒤」(포격 Task 10-5 · EMP Q10 결정) — 이번 범위 밖.

### 4-3. 화면 쪽

- 즉시 발동 버튼은 조준 없이 바로 발동 요청을 보낸다(`BuildingSkillPanelUI.cs` 365~369) [확인] — 패널 쪽 추가 작업 없음.
- 임시 라벨 `DeriveSkillLabel`(235, `MoveSpeedMul` → 「둔화」 248)이 순풍 버튼을 **「둔화」** 로 보이게 한다 [확인]. 라벨은 추후 UI 작업 범위(상위 결정 16) — 이번엔 **기록만** 한다.
- AI는 스킬을 쓰지 않는다 — 스킬 발동 호출처 검색에 AI 코드가 없다 [확인 — `SkillActivation` · `ActivateSkill` 검색 결과는 패널 · 조준 · 화면 구독자 · 조합 루트뿐]. 그래서 강화 스킬은 **사람이 직접 써야만** 시험된다.

---

## 5. 누가 대상인가 — 새 유닛 · 사망 · 경기 종료 · 재경기

| 상황 | 지금 동작 | 등급 |
|---|---|---|
| 버프 중 **새로 생산된 유닛** | 걸리지 않는다 — 대상은 발동 순간의 목록(2451) | [확인] |
| 「새 유닛에도」로 바꾸려면 | 팀 단위 버프 기록 + 유닛 생성 때 적용하는 길(서버 · Client 모두)이 필요. Client의 유닛 생성은 다른 NetworkObject 경로라 상태 RPC와의 도착 순서가 보장되지 않는다 — 상위 Plan 4절 19 | [전달] |
| 버프 중 **유닛 사망** | 상태 기록은 만료로 사라진다(3-6). 상태 VFX는 사망 이벤트에서 멈춰야 한다 — 선례 `TimedDamageEffectPresenter`, `TowerDisableVfxPresenter.cs` 264 | [전달] |
| **경기 종료** | 선례: `TowerDisableVfxPresenter.cs` 278 — 꼬리 정지 | [전달] |
| **재경기** | `GameBootstrapper.Setup.cs` 409~410에서 새 `StatusEffectSystem`을 만든다. 화면 구독자는 `Initialize`에서 남은 것을 즉시 정지(선례 `TowerDisableVfxPresenter.cs` 120) | [전달] |

---

## 6. 강화 상태 VFX 경로 — 쓸 수 있는 것과 새로 필요한 것

### 6-1. 화상 · 독 경로는 그대로 쓸 수 없다

`TimedDamageEffectPresenter` · `VfxPoolItem.TimedDamage.cs`는 서버의 지속 피해(TimedDamage) 채널에서 오고, **유닛당 표시 1개**라 다른 상태를 얹으면 서로 덮어쓴다 → 상위 Research 2-13 ①의 「상태 VFX 전용 채널」 권장 그대로 [전달].

### 6-2. 다시 쓸 것

| 가져올 것 | 출처 | 등급 |
|---|---|---|
| 루프 시작 · 정지 · 확인 번호(`BeginStatusLoop` / `StopStatusLoop(lease, clear)` / `IsStatusLoopActive(lease)`), 꼬리 정지 · 즉시 정지 | EMP `Scripts/Presentation/Effects/VfxPoolItem.StatusLoop.cs` | [확인 — API 3개 존재] |
| 다시 걸리면 **종료 시각만 연장**(재시작 없음), 확인 번호로 남의 항목을 잘못 멈추지 않기 | `TowerDisableVfxPresenter.cs` 181~225 · 288~312 | [전달] |
| 몸 따라가기 — pose 람다 | `GameBootstrapper.Map.cs` 화상 · 독 구독자 초기화(조사 시 331~336, 이 문서 대조 때 327~338 부근) | [확인 — 람다 존재] |
| 읽기 직전 `VisualRootProjector.ProjectNow()` | `TimedDamageEffectPresenter.cs` 214 · VFXSFXGuide 「표현 pose를 저장해 매 프레임 읽는 소비자」 | [확인] |
| 위치 = pose + 회전 × (보정 + 발밑) | `VfxPoolItem.TimedDamage.cs` 83~89 | [전달] |
| 유닛 크기 표 | `UnitEffectConfig.GetRecipientFit` 262 / `EffectManager.ResolveRecipientFit` 444 | [전달] |

- EMP 루프는 **위치를 갱신하지 않는다**(탑은 움직이지 않음 — `EffectManager.cs` 627~668) → 유닛용으로는 「따라가기」가 새로 필요하다 [전달].

### 6-3. 새로 필요한 것 `[제안 — 프로그래머]`

- `VfxPoolItem`에 **`FollowStatusLoop(lease, pose)`** — EMP 루프 + 화상의 따라가기.
- 새 화면 구독자 **`UnitBuffVfxPresenter`** — 키 = **유닛 Id + 발동 스킬**.
- 스킬 자산에 **`_statusVfxPrefab`** + 크기 배율 · 위치 보정 칸. 선례: `_activationVfxPrefab`(`SkillDefinition.cs` 118 → `SkillData`의 GameObject) — 스킬 규칙 7의 데이터 주도.
  - `EffectPreset`은 Presentation 타입이라 스킬 데이터(Application `SkillData`)가 참조할 수 없다(레이어 방향) [전달].
  - `new SkillData(` 호출은 **한 곳**(`SkillDefinition.cs` 130) [확인 — 전체 검색 1건]. 생성자 인자를 늘려도 고칠 곳이 하나다.
- **종족별 이펙트:** 네 스킬이 모두 한 종족 전용이라 **스킬별 칸만으로** 「종족마다 다른 이펙트」가 충족된다. 대상 종족별 오버라이드(같은 스킬이 받는 유닛의 종족마다 다른 그림)는 **적에게 거는 스킬**에서만 의미가 있다 [전달].
- **크기:** 크기 표 높이 ÷ 1.0으로 균일 배율 + 발밑 기준. 표에 없는 유닛은 배율 1 [전달].
- **프리팹 계약:** 모든 Particle System Looping · Stop Action None · Scaling Mode Hierarchy(VFXSFXGuide 「코드 계약」 1) [확인 — 가이드 표].

### 6-4. 성능

- 풀은 처음 쓸 때 만든다(`EffectManager.cs` 1107~1134), 사전 생성은 쓰지 않는다(140) → **첫 발동 때 대상 수만큼 Instantiate**가 몰려 순간 멈칫할 수 있다 [전달].
- 매 프레임 대상마다 `ProjectNow` + `SetPositionAndRotation` [전달].
- 겹침(오버드로)은 프리팹의 maxParticles로 다룬다 — VFX 제작 기준 [전달].

---

## 7. 상대 화면에 보이게 하려면

- 순수 Client는 **팀 · 종류 · 강도 · 지속**만 받는다(3-8). 화염 · 포효 · 전술은 모두 `AttackPowerMul`이라, 상태 RPC만으로는 **어느 스킬인지 알 수 없다** → 어느 프리팹을 붙일지 모른다 [전달].

| 안 | 방법 | 장점 | 한계 |
|---|---|---|---|
| **본인 화면만** | 시전 팀 = 이 화면의 팀일 때만 표시. 동기화 불필요(이 화면 팀 주입 선례 `GameBootstrapper.Map.cs` 308~310) | 가장 단순 | 상대 강화 상태가 안 보인다 |
| **안 A**(스키마 무변) | 새 구독자가 **발동 확정 · 수신 이벤트**(C-1 두 채널 — `SkillActivationVfxPresenter.cs` 100~101 [확인])를 받아 `FindSkillData(팀, 슬롯)`(`SkillActivationUseCase.cs` 265 [확인])로 상태 VFX · 지속을 **자기 빌드**에서 찾는다. 대상 = 이 화면 도메인의 대상 팀 생존 유닛. Host는 확정만, Client는 수신만 받으므로 중복 없음 | RPC · 스키마 변경 없음. C-1 채널 재사용 | Client는 전송 지연만큼 늦게 시작 · 늦게 끝남(EMP R-7과 같음). 발동 순간 경계에 있던 유닛(막 죽거나 막 생긴)은 양쪽 목록이 다를 수 있다 |
| **안 B** | 서버가 대상 유닛 Id 목록 + 슬롯 + 순번을 전송(EMP `TowersDisabledClientRpc` 선례) | 양쪽 대상이 정확히 같다 | 새 RPC → 스키마 **8 → 9**(`UnitAttackShadowProfileResolver.cs` 33 [확인 — 현재 8], fixture `RunUnitActionSelfValidation.ServerProjectiles.cs` 260) |

---

## 8. 빈 칸일 때 · 즉시 발동의 발동 VFX

- **즉시 발동 스킬은 발동 VFX 칸을 채워도 지금은 재생되지 않는다.** `SkillActivationVfxPresenter`가 조준 없음(`NoAim`)이면 먼저 return한다(134~138 부근) — 규칙 25의 「조준이 없는 즉시 발동 스킬의 재생 위치는 아직 정하지 않았다」가 코드로 그렇게 되어 있다 [확인]. 상위 Plan 2-1의 V16 · V23 · V13 · V12(전술 · 화염 · 포효 · 순풍 발동)는 이 길이 열려야 보인다.
- 조준이 있는데 프리팹이 없으면 `NoPrefab` Warn(144 부근) [확인].
- 제안 `[전달 — 프로그래머]`: 상태 VFX 칸이 비어 있으면 **효과는 그대로 적용**하고, 발동 1건당 화면마다 `BuffVfxSkipped Reason=NoPrefab` Warn 1줄.

---

## 9. 진단 로그 제안 `[제안 — 프로그래머]`

- 접두어 `[SKILL-BUFF]`, `Diag=SkillBuff, DiagRev=1`, `GameLog.Dev`, Conditional(릴리스 빌드 제외), 시간 · 배율은 **밀리 정수**.
- 존속 제안: `개발`(EMP `[EMP-DIAG]` 선례 — 결정은 사용자, WORKFLOW [4]).

| 이벤트 | 남기는 때 | 키 |
|---|---|---|
| `BuffApplied` | 서버(싱글 · Host) 적용 직후 | Sequence, Team, Slot, Kind, MagMilli, DurationMs, TargetCount, RefreshedCount |
| `BuffStatusReceived` | 순수 Client가 상태 RPC를 받았을 때 | (같은 값) |
| `BuffAttackSample` / `BuffMoveSample` | 발동마다 첫 공격 · 첫 이동 1회 | Base, StatusMulMilli, Effective, WorldSpeedmm |
| `BuffExpired` | 만료 요약 1줄 | ExpiredCount, DiedCount, ActualMs |
| `BuffNewUnit` | 버프 중 새 유닛이 생겼을 때 | (D1에 따라) |
| `BuffNoEffect` | 타입 C가 효과 없이 끝났을 때 — **Warn, 0건이어야 정상** | |
| `BuffVfxStarted` | 화면 시작 요약 | Channel, Attached, PosePending, FitMissing, Prefab |
| `BuffVfxExtended` / `BuffVfxStopped` | 연장 / 정지 | Path = Expire / UnitDied / GameEnd / Reinit |
| `BuffVfxSkipped` | 표시 못 함 — Warn | Reason = NoPrefab / NoSkillData. 「본인 화면만」안이면 NotLocalTeam(Info) |

---

## 10. 변경 예상 파일 · 위험 · 자가 검증 후보 `[전달 — 프로그래머]`

### 10-1. 변경 예상 파일

- `Scripts/Infrastructure/Config/SkillDefinition.cs` · `Scripts/Application/Skill/SkillData.cs` — 상태 VFX 칸
- `GlobalStatusChangeExecutor.cs` 또는 `SkillActivationUseCase.cs` — 효과 없음 가드 · 진단
- `UnitCombatUseCase.cs` — 진단(새 유닛 규칙에 따라 적용 경로)
- 새 `Scripts/Presentation/Effects/UnitBuffVfxPresenter.cs`
- `VfxPoolItem.StatusLoop.cs`(따라가기) · `EffectManager.cs`(시작 API)
- `GameBootstrapper.Map.cs`(배선)
- 스킬 자산 4개 · `SupportSkillLoadoutSetup.cs` 네 줄
- 안 B를 고르면: `NetworkSkillController.cs` · 스키마 상수 · fixture

### 10-2. 위험

| 위험 | 설명 |
|---|---|
| 효과 없이 쿨다운만 | 네 칸 중 하나라도 빠지면(4-1) |
| 셋업 도구 재실행 되돌림 | 4-2 |
| 종류만으로 덮어쓰기 | 3-5 — 1차엔 사례 없음, 2-Z부터 |
| 걷기 미끄러짐 | 3-9 |
| Client 진단 오탐 | 3-9 `UnitView.cs` 2356 `[추정]` |
| 첫 발동 멈칫 | 6-4 |
| 뒤집힌 Client에서 `ProjectNow` 누락 시 튐 | 6-2 — VFXSFXGuide 계약 |
| 사망 연출 대역 분리 후 따라가기 | 죽은 유닛의 보이는 몸이 사망 대역으로 넘어간 뒤에도 VFX가 남은 몸을 따라가는 경우 — 사망 즉시 정지로 대응 |

### 10-3. 자가 검증(Suite) 후보 1~9

1. 배율 반올림 · 배율 > 1 · 만료 뒤 복귀
2. 재적용 시 남은 시간 교체
3. 아군 · 생존 유닛만
4. 네 자산 계약(지속 > 0 · 종류 · 강도 > 0 · 사용 가능)
5. 상태 VFX 루프 계약(Looping · Stop Action None · Hierarchy)
6. 확인 번호(lease)
7. 팀 필터
8. 새 유닛 규칙(D1)
9. 스키마 고정값(안 B면 9, 아니면 8 유지)

EMP는 Suite 전용 검사가 없었다 [전달].

---

## 11. 이번 조사에서 확인하지 않은 것

- 파도 피해에 공격력 버프가 붙는지(3-3).
- NGO에서 상태 RPC와 발동 확정 RPC의 도착 순서(3-8 `[추정]`).
- NetworkTransform이 빨라진 이동을 따라가는 모습(3-9 — 실기 필요).
- `UnitView.cs` 2356 진단 오탐 여부(3-9 `[추정]`).
- 첫 발동 Instantiate 멈칫의 실제 크기(6-4 — 실기 필요).

*(2026-11-11 — 구현 · 사용자 테스트 뒤에도 위 다섯 항목에 대한 판정은 전달되지 않았다. 도착 순서는 Client 대상 수가 Host와 10/10 같았다는 것뿐이고 순서를 증명하지 않는다. 테스트 결과 전체는 [Plan 11절](Plan.md#11-implementation-and-user-test).)*

---

## 12. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-10 | 첫 판. 사용자 「작성해」로 작성. game-programmer HEAD `624a25a` 읽기 전용 조사 1~8을 옮기고, document-manager가 같은 커밋의 실행기 · 적용 · 공격력 · 이동 · 덮어쓰기 · 발동 순서 · 동기화 · 네 자산 · 옛 Buff 자산 · 셋업 도구 행 · 화면 구독자 NoAim · 패널 라벨 · 스키마 값 · `new SkillData(` 호출 수 · AI 호출처를 다시 읽어 [확인]으로 표시했다. 코드 · 자산 · 규칙 문서 · 다른 Task 문서 · 메모리 무변경 |
| 2026-11-11 | 11절 끝에 테스트 뒤 메모 1줄(다섯 항목 판정 없음 · Plan 11절 안내). 조사 본문은 그대로 둔다 — 구현 결과는 Plan 11절이 원본 |

---

**무관 관찰(이번 범위 밖, 기록만):** `SkillDefinition.cs` 103~107행(착탄 지연) · 115~116행(발동 VFX)의 주석 「지금(커밋 1·2)은 이 값을 읽는 코드가 아직 없다」는 실제와 다르다 — 착탄 지연 예약과 발동 VFX 재생은 포격 Task에서 이미 연결됐다 [확인 — 주석 원문].
