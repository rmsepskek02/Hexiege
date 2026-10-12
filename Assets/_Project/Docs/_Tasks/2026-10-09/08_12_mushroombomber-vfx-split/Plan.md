# Plan — 버섯폭격기(MushroomBomber) 이펙트를 3개로 나누기 (attack → tracer · impact · poison)

## 현재 source freeze 인계 — 2026-10-10

서버가 적용한 공격·지속 피해와 화면 이펙트의 소유권을 연결하는 변경이 현재 소스 snapshot으로 인계됐다. 아래는 구현 보고·정적 검사까지의 결과이며 게임 화면과 실제 멀티플레이 수용은 아직 확인하지 않았다.

**최신 판정 — 2026-10-10 09:53:38 정적 보고 / source freeze:** programmer 현재 snapshot44파일 freeze 인계 수신. Runtime/Editor/Development/Release 정적4경로 exit0 보고이며 native 실행 PASS가 아니다. Main SourceHash44 재확인 완료: Files44/Mismatches0/ChangedPaths=null(tool39f796), 현재 파일 SHA256 대조 불일치0. Main 보호6파일 baseline 일치 인계는 별도이며 Bloom clip baseline도 Main 확인됐다(원 JSON의 baseline unavailable 표기보다 후속 인계가 우선). **Action/ROOT 두 메뉴 미실행(UI activation2 timeout)·빌드 미시도·설치/실기/visual 수용 미확인·전체 OPEN.** 기존 메뉴 확장은 UnitAction 및 AuthoritativeAttackPresentation에 연결됐지만 fixture는 실행 전이고 Root 기존 gate 자체는 변경하지 않았다. 이전 승인·수정 중·static 대기 절은 당시 이력이다.

### 구현 freeze 기술 내용 (programmer 보고, native 미검증)

- Mushroom: single writer-owned immutable `AttackEffectPresentation`이 remaining-flight tracer와 due impact를 함께 소유한다. local marker의 기존 Legacy HP 신호를 유지하며 committedSchedule/Supported/observer-clock에 의존하지 않는다. tracer callback은 오브젝트 반환/표현 종료 only로 HP/impact queue를 생성·방출·지연하지 않는다. 동일 immutable attack identity와 due snapshot으로 impact 단일 재생, 기존 3초 clip/cycle·Stats 유지 및 marker1초다.
- Recipient: 실제 `(target lifetime, Damage)` 단일 gameplay record가 sole owner. poison/burn provenance·attack identity를 current value로 복제, 동일 owner refresh noRestart·overwrite old emission 종료교체. NetworkUnit Host baseline·late Client 현재값 복원·service closure·fresh-match restoration을 연결했다. natural tail 반환과 forced teardown 분류를 분리한다.
- Tower: 실제 writer에서 source/impact/time/race와 actual appliedDamage/resultHp를 capture하고 publication 예외를 HP/death/remove 흐름에서 격리했다. Human 별도 preset은 Tank prefab 재사용·Tank 값 불변, Spirit RuneSpire, Transcendence 미지정/no fallback. 서버 즉시 피해·주기·범위 불변.
- Live-clock/실패 집계: Combat/Network 진단, recoverable faults Warn, bounded key cap은 add 전 검사. missing tracer/manager 및 clock deadline 실패도 counters에 포함, local queue aggregate END는 detail cap과 분리하고 failure/overflow/pending이면 unknown을 남긴다. attack detail budget은 local match 초기화 때 reset한다. 구현 보고일 뿐 실패 경계 fixture 전부 실행/통과를 뜻하지 않는다.

### Asset tuning과 gameplay 분리

impact 모든 PS는 nonlooping / StopAction None이다. 아래 duration+lifetimeMax는 초기 정상 tail 설정을 설명하며 최대 약0.8초다. native 입자 drain·실제 렌더 픽셀 종료시간 수용 증거가 아니다. 실제 서버 DoT3초·HP·틱·스탯·cycle·범위는 변경하지 않았다.

| PS | duration (s) | lifetimeMax (s) |
|---|---:|---:|
| Poison_Ring | 0.2 | 0.45 |
| Spore_Burst | 0.2 | 0.55 |
| Poison_Cloud | 0.15 | 0.65 |
| root | 0.15 | 0.55 |
| Toxic_Bubbles | 0.2 | 0.55 |

root emission=false. emission window<=0.5 및 초기 정상 tail<=약0.8 검증은 준비된 fixture 범위이며 실행 PASS 미확인이다. 과거 3초 독구름·지면 damage-zone 사양은 superseded다.

### 검증 축 및 미완 범위

정적 실행 보고 command: `Temp/expiry-clock-history/Validate.ps1`, Runtime/Editor/Development/Release 각각 exit0(09:53:38 programmer 보고). 읽은 script는 Unity Bee Runtime/Editor rsp 기반으로 source를 재수집하며 Development/Release는 UNITY_EDITOR/DEVELOPMENT_BUILD define 전환이다. 이 검사와 `Temp/expiry-clock-history/{Runtime,Editor,Development,Release}.{rsp,dll,pdb,log}`, `references.sha256`, `unity-version.txt`은 정적 compiler/dependency artefact이고 실제 Player/native build·NGO wire·입자/visual 증명은 아니다. 이 문서 담당이 컴파일을 재실행한 결과가 아니며 warnings0을 주장하지 않는다.

**coveragePrepared — 코드에 연결·준비됨 / 미실행:**

1. 기존 UnitAction·AuthoritativeAttackPresentation 확장(새 menu/validator 없음); Root gate 불변.
2. race selector 및 Human 별도 preset/Tank 공유, nonloop impact emission/tail·StopAction None, canonical Mushroom marker1/cycle3·Stats agreement.
3. same Damage refresh/overwrite, revision 증가 전 exact stale Stop, recipient duplicate/retired enum guards.
4. Host current baseline, late Client current-value restore, service closure/fresh-match restore.
5. recursive PS start·live-particle 조기 반환 거부, natural simulation particles0 뒤 test-only StopEmitting reassert 및 once-only return.
6. actual Unresolved Mushroom writer event→tracer, callback HP/impact 방출 없음, immutable due impact·duplicate snapshot·actual attack identity→Damage record.
7. Tower actual appliedDamage/resultHp correlation 및 presentation subscriber exception과 death/remove 격리.

**coverageNotYetAsserted — 자동 assertion 미완 / 수용 미확인:**

- dedupe/current/tombstone/identity 전체 capacity 경계.
- future/deadline/NaN clock 실패의 terminal counting.
- Legacy/Shadow/ResultPresentation mode별 fixture matrix.
- forced Disable/Destroy teardown 분류.
- 전체 wire serializer round-trip 및 실제 multiplayer delivery.

따라서 static exit0 또는 fixture 코드 존재를 위 범위 완료/native PASS로 확대하지 않는다. 이전 Client C3 FAIL64의 fix도 이번 범위/완료 판정이 아니다.

### 실제 변경 manifest44 — 추가24 / 수정20 (인계 원문 경로)

programmer `Temp/vfx-connection-handoff/source-freeze.json` 전문을 읽어 아래 내용을 문서에 보존했다. hash는 programmer freeze snapshot 값이며 Main 현재파일 Get-FileHash SHA256 재검증도 완료됐다(Files44/Mismatches0/ChangedPaths=null, tool39f796). Temp 파일에만 의존하지 않는다. 신규6 source+meta6+presets6+meta6=추가24, 기존 source16+config2+clip1+impact1=수정20이다. 이는 이 문서 편집 파일 목록이 아니라 programmer 구현 write manifest다.

#### 추가

| 경로 | 구분 |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs` | 추가 |
| `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs` | 추가 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs` | 추가 |
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` | 추가 |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | 추가 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | 추가 |
| `Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs.meta` | 추가 |
| `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs.meta` | 추가 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs.meta` | 추가 |
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs.meta` | 추가 |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs.meta` | 추가 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Tracer.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Tracer.asset.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Impact.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Impact.asset.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Poison.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Poison.asset.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset.meta` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset` | 추가 |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset.meta` | 추가 |

#### 수정

| 경로 | 구분 |
|---|---|
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | 수정 |
| `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` | 수정 |
| `Assets/_Project/Scripts/Application/Events/GameEvents.cs` | 수정 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs` | 수정 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 수정 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs` | 수정 |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/TracerProjectile.cs` | 수정 |
| `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` | 수정 |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 수정 |
| `Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | 수정 |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | 수정 |
| `Assets/_Project/Resources/Config/BuildingEffectConfig.asset` | 수정 |
| `Assets/_Project/Animations/Units/MushroomBomber/MushroomBomber_Attack.anim` | 수정 |
| `Assets/_Project/Prefabs/VFX/Units/vfx_mushroombomber_impact.prefab` | 수정 |

SHA25644 및 보호6 전체 경로/hash는 같은 Task의 [Research](Research.md)에 보존한다. Root gate·새 native 결과는 없으며 실행 결과를 받은 뒤 outcome을 추가한다.

## 현재 구현 준비 Plan — 2026-10-10 Main 검토 통과 기술계약

**재개 중간 근거 — 2026-10-10 Main 인계:** 사용량 초기화 후 submission `01a1233b-895e-7530-aade-f82827243652`로 재개했다. programmer 09:36 소스 수정 및 09:42 `RunUnitActionSelfValidation.cs` 수정 진행을 Main이 확인했다. 이는 최종 구현 freeze·전체 inventory·정적 결과 인계가 아니다. native UI는 `sky.list_apps/list_windows`에서 Unity window67396 확인, 최초 `activate_window` timeout 후 returned window 재선택·`get_window` 복구 및 재시도도 timeout이었다. Main은 computer-use 복구 규정에 따라 native 입력을 중단했다. **이번 두 메뉴 미실행 / 빌드 미시도 / 완료·설치·runtime 미확인**이며, 게임 소스 결함이나 native gate FAIL로 분류하지 않는다. 최종 구현·static 결과는 programmer freeze 회신 후 별도 기록한다.

목록의 세 이펙트를 실제 공격과 독 효과에 연결하되, 피해 판정은 기존 서버가 그대로 담당한다. 착탄은 짧은 포자 burst로 보이고 독은 실제 독 효과를 받는 대상에게 붙는다. **아래 이전 문서 전용 Plan·3초 지면 독구름은 이력이며 이 절이 우선한다.** 기존 gameplay 로직 제거 계획 없음; 검증 전 Legacy 경로를 최종 삭제하지 않는다.

### 확정 구현 계약 및 근거

| 변경 | 구현 계약 | 근거 |
|---|---|---|
| source marker | 기존 3초 clip에 1초 `OnAttackHit` 1개만 추가. clip 길이·cycle·Stats/UnitStats 불변 | 사용자 승인·Main/programmer 인계; GameSystemRules_Units.md 규칙 17·18·20·27 |
| tracer/impact | tracer callback 종료 only. 같은 attack exact identity의 impact 단일 재생; 로컬 도착/충돌로 피해 생성·지연 또는 피격 큐 방출 금지 | GameSystemRules_Units.md 규칙 19·20·26; GameSystemRules_UnitCombatSynchronization.md NET-AUTH-001/002·NET-PRESENT-003 |
| impact prefab | 짧은 포자 확산 burst/fade, StopAction None. 3초 독구름·지면 damage zone 제거; 정확 burst 초 수는 임의 확정하지 않음 | 사용자 확정 사양; GameSystemRules_Units.md 규칙 38~40 |
| recipient state | 실제 writer `(target lifetime, TimedEffectKind.Damage)` 단일 레코드의 effect identity/revision/poison-burn provenance. 같은 owner refresh noRestart; overwrite는 이전 표현 종료·교체, 독립 stack 금지 | GameSystemRules_Units.md 규칙 34·40·42; GameSystemRules_UnitCombatSynchronization.md NET-DELIVERY-TIMED |
| 복제/표현 | network current value, late-init 현재값 복원, restoration/shutdown terminal 보호 → presenter 대상 추종. 취소·만료·사망 후 StopEmitting/drain/lease 반환과 강제 teardown 구별 | 위 효과/권위 규칙 및 Main 검토 통과 기술 인계 |
| migration | Unresolved Legacy 유지, C3 Supported 승격 없음. 기존 HP·DoT 3초·주기·범위 및 30fps 타이밍 권위 불변 | 사용자 범위·NET-AUTH-001/002 |

### Main/programmer 후보 파일과 단계

공통 연결 후보(확정 인계 목록, 실제 변경 inventory는 구현 후 별도 기록): `GameEvents`, `UnitCombatUseCase`, `TowerCombatUseCase`, `NetworkCombatController`, `NetworkUnit`, `GameBootstrapper.Setup/Map`, `UnitView`, `UnitEffectConfig`, `BuildingEffectConfig`, `EffectManager`, `VfxPoolItem`, `TracerProjectile`, `HitPresentationQueue`. 신규 후보: `AttackEffectPresentation`, `TimedDamageEffectPresentation`, `NetworkTimedDamageEffectState`, `TimedDamageEffectPresenter`. config 2개·Mushroom clip·impact prefab·presets 연결 및 기존 `UnitAction`/`AuthoritativeAttackValidation` 확장을 Main/programmer가 담당한다. 이 문서 담당은 MD/meta만 편집한다.

1. 기존 writer/정규 결과 identity와 표현 ownership을 보존해 provenance 및 전달을 보강한다.
2. marker·tracer·burst·recipient state/preset을 연결하고 중복 emitter·ID 재사용·overwrite·late-ready·terminal 수명을 점검한다.
3. 기존 자동 검증 확장 및 정적/native 결과를 별도로 인계한다. 신규 QA/TC/validator 작성은 이 문서 작업 범위가 아니다.
4. Main 구현 허가 후 구현 진행. 빌드 시도는 승인돼 있으나 실행 여부/즉시 관찰만 인계받고 완료 감시는 하지 않는다. 사용자 실기·visual 수용은 별도 대기한다.

**초안 상태:** 초기 기술 Plan Main 검토 통과 인계 반영 완료 / Main 구현 허가 전달·구현 진행 중(사용자/Main 인계). 구현 완료·새 자동 gate·이번 빌드 시도·runtime 수용 결과 없음. [Inferno·타워 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md)과 공통 후보를 공유하되 기존 피해 정책을 바꾸지 않는다.

## 이 작업은 무엇이고 왜 하는가

이펙트 목록 문서(`VFXSFXList.md`)에 버섯폭격기 공격 이펙트가 **한 줄**(「포자 투척 폭발」)로 적혀 있는데, 사용자가 Unity AI 로 만들고 있는 실제 이펙트는
**세 개**다 — 날아가는 포자 폭탄, 떨어진 자리의 폭발과 3초짜리 독구름, 그리고 독에 걸린 적의 몸에 붙어 반복되는 독 표시.
이 Plan 은 목록 문서의 그 한 줄을 세 줄로 바꾸는 방법을 정한다.

- 폭탄과 폭발은 **공격할 때마다 한 번씩** 나오는 이펙트라 지금 자리(「유닛 공격 이펙트」 표)에 둔다.
- 독 표시는 **적의 몸에 붙어 독이 이어지는 동안 반복되는** 이펙트라, 꽃요정 치료 이펙트와 같은 **「유닛 특수 효과」** 칸에 둔다(사용자 결정).
- 그 「유닛 특수 효과」 칸은 꽃요정 작업이 만들기로 했는데 아직 만들어지지 않았다. 어느 작업이 먼저 반영되든 **결과가 똑같아지도록** 두 경우의 절차를 모두 적는다.
- 소리(SFX)는 **이번에 바꾸지 않는다**(사용자 결정 — 추후 사운드 작업에서 다시 본다).

바꾸는 파일은 **문서 1개**(`VFXSFXList.md`)뿐이다. 이 이름을 쓰는 코드·에셋이 없고 프리팹도 아직 없어서(Research.md 1절) 깨지는 곳이 없다.
이펙트를 게임에 실제로 연결하는 일은 코드가 필요하므로 **이번 범위 밖**이다(5절).

---

## ⚠️ 기존 로직 제거 — 없음

이번 작업은 문서 표의 행을 바꾸고 더하는 것이며 **코드·로직을 제거하거나 비활성화하는 항목이 없다**(WORKFLOW.md [4] 「기존 로직 제거 규칙」 해당 없음).
「유닛 공격 이펙트」 표에서 옛 행 1개가 빠지지만, 이는 **문서 행의 교체**(1행 → 2행)이지 로직 제거가 아니다.

---

## 1. 근거 규칙

WORKFLOW.md [4] 에 따라 각 항목의 근거를 적는다. **에셋 파일 이름과 목록 문서의 분류를 정하는 규칙은 `GameSystemRules` 에 없다** — 이름 형식은
`VFXSFXGuide.md` 「파일 명명 규칙」이 정하고, 분류는 사용자 결정과 문서 선례를 따른다. 그래서 아래 표는 「직접 근거」와 「관련 GameSystemRules」를 갈라 적는다.

| 항목 | 직접 근거 | 관련 GameSystemRules |
|---|---|---|
| 투사체 행 `vfx_mushroombomber_tracer.prefab` | 사용자 결정 + `VFXSFXGuide.md` 「파일 명명 규칙」 `vfx_[에셋명]_[이벤트].prefab`(`tracer` 는 「이벤트」 자리) | `GameSystemRules_Units.md` 규칙 20 — 원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체를 재생하고 착탄 시 피격 표현을 방출한다(버섯폭격기 사거리 2.0 은 원거리 분기 — Research.md 3-1). 규칙 40 「서버 권위」 항목 — 폭탄 투사체·폭발 VFX 는 사용자가 따로 만든다 |
| 착탄 행 `vfx_mushroombomber_impact.prefab` | 같은 사용자 결정 + 같은 명명 규칙 | `GameSystemRules_Units.md` 규칙 38(착탄 중심 = 주 타깃 위치, 월드 원형 반경 `blastRadius`) · 규칙 40(DoT 3초 · 1초 틱, 폭발 VFX 는 사용자 제작) — 비고의 「반경 1.0 · 3.0초」가 이 두 규칙의 현재 값에 맞춘 것이다 |
| 두 행을 「유닛 공격 이펙트」 표에 둠 | 사용자 결정 — 둘 다 공격 1회당 1회 나오는 이펙트 | **해당 규칙 없음**(목록 문서의 분류는 GameSystemRules 가 정하지 않는다) |
| 독 상태 행 `vfx_mushroombomber_poison.prefab` | 같은 사용자 결정 + 같은 명명 규칙 | `GameSystemRules_Units.md` 규칙 34(종료 처리 — 만료·대상 사망 / 갱신 = 리셋) · 규칙 40(DoT 갱신 = 리셋) · 규칙 39(DoT 는 적 유닛만 — 건물에는 붙지 않는다) |
| 독 상태 행을 「유닛 특수 효과 이펙트」 절에 둠 | **사용자 결정** — 대상에 붙는 상태 이펙트이며 꽃요정 치료 이펙트와 같은 부류. 절의 형태는 꽃요정 작업 Plan 2절 B-2 를 따른다 | 분류 자체를 정하는 규칙은 **없음**. 공격 표에 두면 `GameSystemRules_Units.md` 규칙 31 의 「유닛 공격 VFX 는 `attackPreset` 에 연결한다」는 문장대로 잘못 연결될 여지가 있다(꽃요정 작업 Plan 1절과 같은 이유) |
| (절이 없을 때) 「유닛 특수 효과 이펙트」 절 신설 | 꽃요정 작업 Plan 2절 **B-2** 의 정의(위치 · 제목 · 머리 안내문 · 열)를 그대로 따른다 — 두 작업이 같은 결과로 수렴하게 하기 위해서다 | **해당 규칙 없음** |
| SFX 행 변경 없음 | 사용자 결정(추후 사운드 작업에서 재검토) | `GameSystemRules_Sound.md` 규칙 15(VFX + SFX 쌍 호출)는 **호출 위치**를 정하는 규칙이라 목록 문서의 SFX 행 이름·개수를 정하지 않는다. 쌍 호출은 연결 작업에서 지킨다(5절 6번) |

---

## 2. 변경 항목

대상 파일: `Assets/_Project/Docs/Assets/VFXSFXList.md` **한 개뿐**.
위치는 **행 번호가 아니라 절 제목으로 찾는다**(꽃요정 작업이 먼저 반영되면 행 번호가 달라진다). 아래 행 번호는 2026-10-09 실측 기준이다.

### 비고 첫머리의 상태 표시 — 세 행 공통

구현하는 순간 `Assets/_Project/Prefabs/VFX/Units/` 에 그 파일이 있는지 **실측해서** 둘 중 하나를 붙인다(꽃요정 작업 Plan 2절 B-2 의 선례 — 「있는 것」과 「동작하는 것」을 갈라 적는다).

| 실측 결과 | 비고 첫머리 |
|---|---|
| 파일 없음 | `**미제작**` |
| 파일 있음 | `**프리팹 제작됨(YYYY-MM-DD 확인) · 게임 미연결**` — 날짜는 확인한 날 |

- 「게임 미연결」이 사실인지도 함께 확인한다: 프리팹 `.meta` 의 GUID 가 `.asset`·`.unity`·`.prefab`(자기 자신 제외)·`.cs` 에서 0건이어야 한다. 0건이 아니면 그 문구를 쓰지 않고 멈춰서 보고한다.
- 아래 문구의 `{상태}` 자리가 이 표시다.

### 항목 1. 「유닛 공격 이펙트」 표 — 1행을 2행으로 교체

- 찾을 곳: `#### 유닛 공격 이펙트` 표의 버섯폭격기 행(실측 45행)
- 변경 전(삭제):

```
| Transcendence | MushroomBomber | `vfx_mushroombomber_attack.prefab` | 포자 투척 폭발 |
```

- 변경 후(같은 자리에 2행 — 투사체가 먼저, 착탄이 다음):

```
| Transcendence | MushroomBomber | `vfx_mushroombomber_tracer.prefab` | {상태} — 날아가는 포자 폭탄(갈색 포자 구 + 보라 독가루 꼬리). 발사 지점에서 착탄 지점까지 비행하는 동안 루프 재생 |
| Transcendence | MushroomBomber | `vfx_mushroombomber_impact.prefab` | {상태} — 착탄 지점 폭발, 1회 재생. 갈색 포자 파열(0~0.5초) + 보라 독구름·지면 원·독 기포가 반경 1.0 에 3.0초(현재 독 피해 반경·지속시간과 맞춤) |
```

- 두 행의 순서는 재생 순서(던짐 → 착탄)를 따른다. 표의 다른 행·표 머리는 건드리지 않는다.
- 변경 후 이 표의 행 수: 꽃요정 행이 아직 이 표에 있으면 **26행**(Transcendence 9), 이미 빠졌으면 **25행**(Transcendence 8).
- 비고에 **다른 두 파일의 이름을 적지 않는다** — 7절의 「각 1건」 확인이 성립하도록 하기 위해서다. 옛 이름도 적지 않는다(7절 「0건」 확인).

### 항목 2. 「유닛 특수 효과 이펙트」 절 — 독 상태 행 추가

#### 2-0. 어느 경우인지 판별

`VFXSFXList.md` 에서 `#### 유닛 특수 효과 이펙트` 를 찾는다.

| 결과 | 경우 | 할 일 |
|---|---|---|
| 1건 | **(a) 꽃요정 작업이 먼저 반영됨** | 2-A |
| 0건 | **(b) 아직 없음** (2026-10-09 실측 상태) | 2-B |
| 2건 이상 | 비정상 | 고치지 말고 멈춰서 보고한다 |

두 경우 모두 **최종 모습은 2-C 와 같아야 한다.**

#### 2-A. 절이 이미 있을 때

1. 머리 안내 인용문(꽃요정 Plan B-2 의 `>` 3줄)의 **마지막 줄 다음에** 아래 2-C 의 버섯폭격기 안내 2줄을 덧붙인다. 꽃요정 줄은 고치지 않는다.
2. 표에서 꽃요정 행 **위에** 버섯폭격기 행을 넣는다. 다른 모든 유닛 표가 유닛 종류 순서(`MushroomBomber = 26` → `BloomFairy = 27`, `Scripts/Domain/Unit/UnitType.cs:48~49`)로 버섯폭격기를 꽃요정 앞에 두기 때문이다.
3. 꽃요정 줄·행이 꽃요정 Plan B-2 문구와 다르면(사용자가 직접 고쳐 썼을 수 있다) **맞추려고 고치지 않는다.** 버섯폭격기 줄·행만 넣고, 다른 점을 보고한다.

#### 2-B. 절이 아직 없을 때

1. 위치: `#### 유닛 사망 이펙트` 표의 마지막 행(꽃요정 사망 행) 다음, `#### 건물 파괴 이펙트` **앞** — 꽃요정 Plan B-2 와 같은 자리다.
2. 제목·머리 안내문 3줄·표 머리는 **꽃요정 Plan B-2 의 문구를 한 글자도 바꾸지 않고** 쓴다(아래 2-C 의 앞부분). 그 뒤에 버섯폭격기 안내 2줄과 버섯폭격기 행을 넣는다.
3. 꽃요정 행은 **넣지 않는다** — 그 행의 이동(공격 표에서 빼기 · 이 표에 넣기)은 꽃요정 작업의 일이다(사용자가 직접 한다).
4. 결과: 꽃요정 작업은 B-2 에서 **절을 새로 만들지 않고 꽃요정 행만** 버섯폭격기 행 아래에 넣으면 된다(B-1 · B-3 · B-4 는 그대로).
5. ⚠️ 그 사이에는 머리 안내문이 아직 표에 없는 꽃요정을 설명하는 상태가 된다. 두 작업이 같은 결과로 만나기 위해 감수하는 과도 상태다 — 꽃요정 작업이 반영되면 해소된다.

#### 2-C. 최종 모습 (두 경우 공통)

```
#### 유닛 특수 효과 이펙트

> BloomFairy 치료는 아군 1명에게 거는 **지속 회복(HoT)**이다 — 규칙: [`GameSystemRules/GameSystemRules_Units.md`](../GameSystemRules/GameSystemRules_Units.md) **규칙 34**.
> 공격 순간 1회 재생되는 「유닛 공격 이펙트」와 달리 **치료받는 아군에 붙어 회복이 이어지는 동안 루프 재생**되므로 이 절에 따로 둔다.
> ⚠️ 꽃요정의 `attackPreset` 칸(`GameSystemRules_Units.md` 규칙 31)에 연결하지 않는다 — 그 칸은 치료하는 꽃요정 **자신의 위치에서 1회** 재생한다.
> MushroomBomber 독 상태 이펙트는 착탄 반경 안에서 **지속 피해**(DoT)를 받는 **적 유닛 각각에 붙어** 피해가 이어지는 동안 루프 재생된다 — 규칙: `GameSystemRules_Units.md` 규칙 34 · 규칙 40. 착탄 지점의 폭발·독구름(1회 재생)은 「유닛 공격 이펙트」 표에 있다.
> ⚠️ 버섯폭격기의 `attackPreset`·`hitPreset` 칸에 연결하지 않는다 — `hitPreset` 은 **맞은 유닛의 종류**로 고르는 칸이라 버섯폭격기가 **맞았을 때** 재생된다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | MushroomBomber | `vfx_mushroombomber_poison.prefab` | {상태} — 독 피해를 받는 적 유닛 각각에 붙어 루프 재생(은은한 보라 기포·독 방울·오라). DoT 만료·대상 사망 시 방출 정지(남은 입자는 자연 소멸), 재부여 시 끊지 않고 유지(시간 리셋). 건물에는 붙지 않는다 |
| Transcendence | BloomFairy | (꽃요정 작업이 넣는 행) |
```

- 앞의 3줄과 꽃요정 행은 **꽃요정 Plan B-2 의 것**이다. 마지막 행 `(꽃요정 작업이 넣는 행)` 은 위치를 보이기 위한 자리 표시이며 **문서에 이 글자를 쓰지 않는다.**
- 버섯폭격기 안내 2줄의 근거: 규칙 34(종료 처리 · 갱신 = 리셋) · 규칙 40(DoT 갱신 = 리셋) · Research.md 3-1(`hitPreset` 은 맞은 유닛 종류로 고른다 — `HitPresentationQueue.cs:373`).
- 비고의 「방출 정지(남은 입자는 자연 소멸)」는 사용자가 수락한 생성 프롬프트의 합의 사항이다 — 약 0.2초 페이드인, 정지 시 방출만 멈추고 남은 입자는 자연히 사라진다(Research.md 5절). 꽃요정 치료 이펙트(꽃요정 Research.md 3-3)와 같은 방식이다.
- `check_docs.py` 대비: 규칙 번호 뒤에 「」로 제목처럼 보이는 말을 붙이지 않았다(검사 `[5]`). 규칙 번호와 같은 줄에 `GameSystemRules_Units.md` 를 적어 어느 문서의 규칙인지 특정되게 했다. 링크는 꽃요정 줄의 기존 상대 경로만 쓴다(검사 `[2]`).

---

## 3. 변경하지 않는 것 (이유 포함)

| 대상 | 이유 |
|---|---|
| `VFXSFXList.md` 「유닛 공격 사운드」 표의 `sfx_mushroombomber_attack.wav` 행(실측 256행)과 다른 모든 SFX 행 | 사용자 결정 — 추후 사운드 작업에서 재검토. 「유닛 특수 효과 사운드」 절도 이번에 만들지 않는다(그 절은 꽃요정 Plan B-4 가 만든다) |
| `VFXSFXList.md` 「유닛 사망 이펙트」 표의 버섯폭격기 행(실측 75행, `vfx_mushroombomber_death.prefab`) | 「attack」이 아니며 이번 분할과 무관하다 |
| `VFXSFXGuide.md` 301행(VFX 키워드 표 · 「독/폭발 투척 / spore explosion, poison cloud」) · 492행(SFX 키워드 표) | **파일 이름이 없는 키워드 행**이고, 키워드가 착탄 폭발·독구름과 이미 맞는다(Research.md 1절). 꽃요정 작업이 키워드 행을 그대로 둔 판단과 같다 |
| `VFXSFXGuide.md` 「파일 명명 규칙」 | `tracer` · `impact` · `poison` 모두 규칙 안이다(Research.md 1절) |
| 애니메이션 `MushroomBomber_Attack.anim` · `MushroomBomber.controller` 의 `Attack` 상태 | 모든 유닛이 공격 동작을 「Attack」이라는 공통 이름으로 찾는다(꽃요정 작업 Research.md 2절과 같은 경로). 문서 작업이며 범위 밖이다 |
| 코드 식별자(`attackPreset` · `tracerPreset` · `hitPreset` · `OnAttackHit` · `PlayUnitAttack` · `PlayTracer` 등)와 이를 인용한 규칙 문서 문장 | 코드와 문서가 같은 이름을 가리켜야 한다. 코드는 범위 밖이다(CLAUDE.md 규칙 6) |
| `GameSystemRules_Units.md` 규칙 27(「MushroomBomber 클립에 `OnAttackHit` 1개 주입 완료」) | 실측(이벤트 0건)과 어긋나지만 이번 범위 밖이다 — 사실만 Research.md 4절·7절에 기록했다 |
| 꽃요정 작업 문서(`_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md` · `Research.md`) | 이 작업의 대상이 아니다. 그 Plan 7절의 행 수 기대값이 달라지는 점은 6절 「꽃요정 작업에 전할 것」으로 보고만 한다 |

---

## 4. 이번 작업이 하는 일의 경계

- 이번에 하는 일은 **목록 문서에 파일 이름 3개를 올바른 칸에 적는 것**까지다.
- 프리팹 제작은 사용자가 한다. 이 작업은 프리팹을 **열기만 하고 만들거나 고치지 않는다**(2절 상태 표시를 위한 존재 확인만).

---

## 5. 범위 밖 — 후속 작업으로 남기는 것

이번에는 **아무것도 게임에 연결하지 않는다.** 연결에 필요한 사실은 Research.md 3·4절에 실측해 두었다.

1. **버섯폭격기 공격 클립에 `OnAttackHit` 이벤트 주입** — 지금은 0건이라 공격 VFX·SFX·트레이서가 버섯폭격기 몫으로 전혀 실행되지 않는다(Research.md 4절). 다른 연결이 모두 이것에 달려 있다. 규칙 27 서술과 실측의 불일치도 이때 정리한다.
2. **착탄 지점 폭발 재생 수단** — 지금은 착탄 위치에서 버섯폭격기 전용 이펙트를 재생할 칸·메서드가 없다(`hitPreset` 은 맞은 유닛 종류로 고르는 칸이라 쓸 수 없다 — Research.md 3-1). 트레이서 착탄 콜백에 이어 붙이는 방법 등은 그 작업에서 정한다.
3. **독 상태 이펙트 붙이기 / 갱신 / 떼기** — 「대상에 붙여 반복 재생 + 정지」 API, 그리고 DoT 부여·갱신·종료(만료·사망)를 알리는 신호가 필요하다. 지금은 둘 다 없고, 멀티에서 클라이언트가 DoT 시작·종료를 알 수단도 없다(Research.md 3-1). 꽃요정 작업 Plan 5절 1·2번(같은 API)과 함께 설계하는 것이 자연스럽다.
4. **포물선(던지는) 궤적** — 트레이서는 직선 비행만 한다. 코드가 필요하며 사용자가 나중으로 미뤘다(Research.md 3-2). 착탄 순간 투사체가 바로 꺼져 꼬리 입자도 함께 사라지는 점도 그때 함께 본다.
5. **`UnitEffectConfig` 에 프리셋 연결** — 투사체는 `tracerPreset` 에. 착탄·독 상태는 2·3번에서 정해지는 자리에. ⚠️ 독 상태 이펙트는 `attackPreset`·`hitPreset` 에 넣지 않는다(2-C 안내문).
6. **SFX** — `sfx_mushroombomber_attack.wav` 행의 재검토(나눌지, 이름을 바꿀지)와, 연결 지점마다의 VFX + SFX 쌍 호출(`GameSystemRules_Sound.md` 규칙 15 — SFX 가 없으면 주석으로 명시).

---

## 6. 위험 요소

| 위험 | 대책 |
|---|---|
| 꽃요정 작업과 순서가 엇갈려 절이 두 번 생기거나 모양이 달라진다 | 2-0 판별을 **먼저** 하고, 2-B 에서는 꽃요정 Plan B-2 문구를 그대로 쓴다. 2건 이상이면 멈춘다. 확인 절차(7절)에 절 제목 1건 확인을 넣었다 |
| 꽃요정 작업의 확인 기대값이 틀어진다 | **꽃요정 작업에 전할 것**: ① 그 Plan 7절의 「유닛 공격 이펙트」 표 **24행**은, 이번 작업이 먼저 반영됐다면 **25행**(Transcendence 8)이 된다(「유닛 공격 사운드」는 24행 그대로). ② 이번 작업이 먼저 절을 만들었다면 그 작업의 B-2 는 꽃요정 행만 넣는다. ③ 그 Plan 의 행 번호(76·78행 등)는 1칸 이상 밀려 있다 — 절 제목으로 찾는다. 이 Plan 은 그 문서를 고치지 않는다 |
| 착탄 이펙트의 「반경 1.0 · 3.0초」가 나중에 실제 값과 어긋난다 | `blastRadius` · `blastDotDuration` 은 Inspector 에서 바꿀 수 있고(규칙 25·40) 프리팹은 따라가지 않는다. 비고에 「현재 … 맞춤」이라고 적어 이 값이 **그 시점 기준**임을 드러냈다. 값이 바뀌면 프리팹을 다시 맞춰야 한다 |
| 독구름을 「구름 안에 있으면 피해」로 오해한다 | 실제 판정은 착탄 순간 한 번 모은 대상에만 DoT 가 붙는다(Research.md 2-1). 이번 문서 문구는 「착탄 반경 안에서 DoT 를 받는 적 유닛 각각에 붙는다」로 적어 판정 기준을 착탄 순간에 둔다. 연출을 어떻게 보이게 할지는 사용자 판단이다 |
| 행 교체 중 행이 사라지거나 두 번 들어간다 / 옛 이름이 남는다 | 7절의 이름별 건수 확인 |
| 상태 표시를 실측 없이 적는다 | 2절 「상태 표시」 표대로 **구현 순간에** 폴더를 실측하고 GUID 미참조까지 확인한 뒤 적는다 |
| 새 안내문이 `check_docs.py` 에 걸린다 | 2-C 의 「check_docs.py 대비」대로 썼다. 그래도 걸리면 **참조하는 쪽 문구**를 고친다(규칙 번호는 바꾸지 않는다) |

---

## 7. 구현 후 확인 절차

| 확인 | 검색 범위 | 기대값 |
|---|---|---|
| 옛 이름 `vfx_mushroombomber_attack` | `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체, **`_Tasks/`·`_Logs/` 제외** | **0건** (검색어는 `vfx_` 까지 포함한다 — `mushroombomber_attack` 만으로 찾으면 그대로 두는 SFX 행이 잡힌다) |
| `vfx_mushroombomber_tracer.prefab` (확장자까지) | `Assets/_Project/Docs/Assets/VFXSFXList.md` | **1건** — 「유닛 공격 이펙트」 표 안 |
| `vfx_mushroombomber_impact.prefab` | 같은 문서 | **1건** — 「유닛 공격 이펙트」 표 안, tracer 행 바로 다음 |
| `vfx_mushroombomber_poison.prefab` | 같은 문서 | **1건** — 「유닛 특수 효과 이펙트」 표 안 |
| `sfx_mushroombomber_attack.wav` | 같은 문서 | **1건** — 변경 전과 같은 「유닛 공격 사운드」 표 안, 비고 「포자 폭발음」 그대로 |
| 절 제목 `#### 유닛 특수 효과 이펙트` | 같은 문서 | **1건** — 「유닛 사망 이펙트」 표 바로 다음 · 「건물 파괴 이펙트」 앞 |
| 「유닛 공격 이펙트」 표의 행 수 | 같은 문서 | 꽃요정 행이 아직 있으면 **26행**, 빠졌으면 **25행** |
| 「유닛 특수 효과 이펙트」 표의 행 수 | 같은 문서 | (b) 경우 **1행**, (a) 경우 **2행**(버섯폭격기 → 꽃요정 순) |

그다음 리포지토리 루트에서 `python3 Tools/check_docs.py` → **0건** 확인(WORKFLOW.md [11]). 2026-10-09 작업 전 기준선도 **0건**이다(실측).
- `VFXSFXList.md` 는 `Docs/` 하위라 검사 범위 안이다. 이 Plan/Research 는 `_Tasks/` 라 **검사 범위 밖**이며, 여기에 적은 규칙 번호·경로는 손으로 대조했다.
- 코드·씬 변경이 없으므로 실기 테스트 대상이 없다.

---

## 8. 변경 예정 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 항목 1(「유닛 공격 이펙트」 표 1행 → 2행) · 항목 2(「유닛 특수 효과 이펙트」 절에 1행 + 안내 2줄 — 절이 없으면 꽃요정 Plan B-2 대로 절 신설 포함) |

작업 문서(이번에 작성): `Assets/_Project/Docs/_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Research.md` · `Plan.md`
