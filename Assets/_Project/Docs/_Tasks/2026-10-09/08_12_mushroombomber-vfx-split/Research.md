# Research — 버섯폭격기(MushroomBomber) 이펙트를 3개로 나누기 (attack → tracer · impact · poison)

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

| 경로 | SHA256 (programmer freeze) |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs` | `F6D0FBBABE6FEB008BA34A0D148701E24FCE0C6A5B55348B72127F5BDE55D3F1` |
| `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs` | `E79EDDC4A84B1B2510CB3C90972C607604D66605EF707094AAB0F640BD10352E` |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs` | `BBE44864C313B39C8641770C0D025139CCC8F7396FFD730A9ABD5714DABEA977` |
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` | `124B0710C136293AC04F1B5568C71CA4B6572EC3B44BED907F8678DC69454840` |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | `8CFE9DBAF09B7F76EA3AC23B304D5AB67D57CE1B436757FF7BCA41A7C5C389CF` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | `8DCA1D37B070467C1DA4959E64E504D0CF3178C22CE5E40C6EDB07DD026A7465` |
| `Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs.meta` | `7D449F3711814B3BCB065D091A2DDA717C92394424D8EF8CF427F9BC1A18A10A` |
| `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs.meta` | `C46721494E0219D2BDA0DDBCF928F1F3FD7AEC66340F3E067959CD165749B59A` |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs.meta` | `B6E137698E3C7BBD23EB0B62A432FFEAF9D3F8E019310B2889E3D4A11B5242A7` |
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs.meta` | `F701280227C857CEE9F3A0445335E1B73B757410F0A15AD40714A46897CCE9F7` |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs.meta` | `53F1BB7A1ED588E3029BC5CCF02D02FEB6C3E68B3186F57327966A93396A56AE` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs.meta` | `2A99E4C824E0EFB899FA62DDED851B534F9D85440779E7B3979849DD4C9E3D17` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Tracer.asset` | `962A0C45A6F94E90FC619C4E7E598E79C04FE1F5CCF2FD9CDA15081BE9C669A6` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Tracer.asset.meta` | `C769767334D1D362CABCA330CF9C478327CE25FA5F73AB2CA1B8721E142546BE` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Impact.asset` | `0DC595297BC46C484C5ABE91D1D4F94FC9F9720964D788F8EB18945302E2CD47` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Impact.asset.meta` | `CF11C1EC40AEED51EE10FA0FE548BDB8AF3ACAEBE9F06A3FC3CC04D1BC6DF670` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Poison.asset` | `ED9A4BC81DE98607555355609906C9A499C590584061E45E88E9E711A5E13467` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_MushroomBomber_Poison.asset.meta` | `4534FA9687C45B16F351EBC86B368B332B866EA1B8A66CB8C4571AD4BBDA3A81` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset` | `C123D7F1B4063A877136D47EC7F7585B0C3E98E301C04CEB818F15DB344D5D6E` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Burn.asset.meta` | `2A85A6842D08A50166AA4E3D45FE0B7C4F12FB2BD963A18321FB6F3BADD7A72C` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset` | `B680ADA397876645A50B93ECF32B32A1E5A7E8D9EDBC937EF3780BC4DE00E977` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset.meta` | `2A062D4440640B87D7EBBD7585A1FF33A04C01FA5D5BA4C231100C1C2602542B` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset` | `6EEF21A73E58419343B7462E7C4E9E8846709CD2BAD90F52BBD23E9638D39030` |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset.meta` | `8C73F9D0D8912D3026AF15ABB617738E2099B2B380F3959C9FEB2F87782BCB51` |

#### 수정

| 경로 | SHA256 (programmer freeze) |
|---|---|
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | `95F2DCD1CF7AE357C3646B0512C3CC5E3F662A057851682A030C31AB2AF5A6BD` |
| `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` | `B45B246139CDB544F0A30625C1FC20929793525E89A5A72CCCBBEFE71762854D` |
| `Assets/_Project/Scripts/Application/Events/GameEvents.cs` | `F70F7984F31DFB10039A1AEC740BF40AB172CDC555C0B717E8AF2973E0AFB130` |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs` | `6D0112628DDC6C3D19B0B4C2ABE3BBED00C25AE56DA6F1CAE680166AEA72A930` |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | `BF6B1C510A009498F772B5D056DAED811B0E8B8B4CD3CCA0D507ABD82230B94A` |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `44D92FACD89D2005DDE79D41F93D1452BB449F2218283D44766B9FE29C4662B3` |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs` | `F6A23233835BE70A4EA083CA706DD1CB83B5F3F8635144DB8D11D4E5F922CAD4` |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` | `B7117358CE318BA7D16DC04342B11D318AC2AB22F5A9DCE12AFDA375964C238D` |
| `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | `DFC9943007E972ED4433513F8947A70160A3C8A72A98E6F00AECCA8F3F29CAD2` |
| `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs` | `CFFF377C6B16A3F828320203F341CD3F6FDE8F1CECD451B0B36A0A0DAF57F1A6` |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | `93D8ACEB4EC987F218758255999FB5B0AD58FDAF042E89E5C4BA1FDF316EAB11` |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` | `528ABC8A52FD3EC6E434D794296FA27D46C0418CE4C86D6513202C76F842BEF4` |
| `Assets/_Project/Scripts/Presentation/Effects/TracerProjectile.cs` | `75ED6E16AE2FABFB546244043750B2065EC1FA16EBEF5FDA4C0CB1E041868CE8` |
| `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` | `A30E53861A5B4FCB1C42D04CC1ACFEED371A9C1EE04074277499F7C857431F5E` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `0DCE1FC75DA9E7C1E0C359A7481CEFBA2826C00F64BD8A63E31FE7A93D923DB2` |
| `Assets/_Project/Scripts/Editor/Combat/RunAuthoritativeAttackPresentationValidation.cs` | `87C5015A85595749D68D78F24BB496A50DEF9F5FCC946F74D07AC79D4F14BD3F` |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` | `5CBF74A9378FE2E8CC55B2DD571C5D7DA4A929AA5E563CA7B1F6DCB324B8FBEB` |
| `Assets/_Project/Resources/Config/BuildingEffectConfig.asset` | `AA16D69F0E05F61ED4E4CD86379D70D122317EC4BE9D312A15E6DD7F31C5CE18` |
| `Assets/_Project/Animations/Units/MushroomBomber/MushroomBomber_Attack.anim` | `82C65AE65F8D1776C3B34063DAD06C6F34DBF9829AF874FEB74A812DCBAA6A69` |
| `Assets/_Project/Prefabs/VFX/Units/vfx_mushroombomber_impact.prefab` | `C68060BF7C79510D19C791D0A725C4EF33F5C87A943E5518EFF9E6D9096D5C07` |

### 보호6파일 — Main baseline 일치 인계

| 경로 | snapshot SHA256 |
|---|---|
| `Assets/_Project/Scenes/Game.unity` | `A5310D1545A0080AFB8D5149EAAC95E0B1D2649318D0F9F973CC206D5BB42966` |
| `Assets/_Project/Scenes/Login.unity` | `10BF4C2C9EFB79C4B36E7601D9B8CF8AF4031D05FE00ED1A308A71744F0BB474` |
| `Assets/_Project/Resources/Config/UnitStatsConfig.asset` | `3906969C3A8618BCFF774FC33B252990B8899F7A1CC1B77B0668AB58F26D98EF` |
| `Assets/_Project/Resources/Config/SpecialAttackConfig.asset` | `70D53758F4E8DD39EF6DBB6B0FB3B766295C7D733240203223DA3481710BBE86` |
| `Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab` | `496CD9F76DE25AD9888A9A87E9A7537E06035D9386C3F83A256E0261E3FE72BB` |
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` | `EBD6EC7A908BE84181482B5BDCF5FFAF9CA3CFECC791EBAA28E07666290A7DF4` |

JSON은 BloomFairy clip을 baseline unavailable/current hash only로 적었으나 Main 후속 인계는 Bloom 도메인 baseline 포함 모두 일치다. 이 후속 근거를 반영하며 문서 담당의 독립 baseline 재측정 주장이나 다른 파일 전체 무변경 주장으로 확대하지 않는다.

## 현재 작업 확장 — 2026-10-10 확정 사양·기술 인계

**재개 중간 근거 — 2026-10-10 Main 인계:** 사용량 초기화 후 submission `01a1233b-895e-7530-aade-f82827243652`로 재개했다. programmer 09:36 소스 수정 및 09:42 `RunUnitActionSelfValidation.cs` 수정 진행을 Main이 확인했다. 이는 최종 구현 freeze·전체 inventory·정적 결과 인계가 아니다. native UI는 `sky.list_apps/list_windows`에서 Unity window67396 확인, 최초 `activate_window` timeout 후 returned window 재선택·`get_window` 복구 및 재시도도 timeout이었다. Main은 computer-use 복구 규정에 따라 native 입력을 중단했다. **이번 두 메뉴 미실행 / 빌드 미시도 / 완료·설치·runtime 미확인**이며, 게임 소스 결함이나 native gate FAIL로 분류하지 않는다. 최종 구현·static 결과는 programmer freeze 회신 후 별도 기록한다.

**단계 갱신:** Main은 Task4 실파일·기술 Plan 검토를 완료하고 programmer에게 구현 허가를 전달했다. 구현 진행 중이며 최종 source inventory·새 정적/native gate·build/runtime 결과는 인계 대기다.

포자 폭탄의 비행, 짧은 착탄 확산, 독에 걸린 대상의 표시를 실제 게임에 연결하는 작업으로 확장한다. 착탄 뒤 3초 남는 독구름은 피해 영역으로 오해되므로 **짧은 포자 확산 burst**로 교정한다. 서버 DoT 3초와 기존 피해·HP writer·대상 수집·범위·틱은 그대로 유지한다. 아래 2026-10-09 목록 전용 작업과 독구름 사양은 당시 이력이며 이 절이 현재 지시다.

- 실물 tracer/impact/poison 제작과 병합 후 GUID 참조0은 [VFX 목록](../../../Assets/VFXSFXList.md)의 2026-10-09 실측을 따른다. 연결·실기 수용 완료가 아니다.
- Main/programmer 초기 기술 인계: 현재 Mushroom clip은 3초/event 없음. **1초 OnAttackHit 1개만 추가**, clip 길이·cycle·Stats/UnitStats 값은 불변이다. `Unresolved / Legacy` 유지, C3 Supported 승격 없음.
- tracer 도착 callback은 표현 종료만 담당한다. impact는 같은 attack exact identity로 단일 재생하며 피해나 피격 큐를 도착까지 지연하지 않는다.
- recipient는 실제 writer의 `(target lifetime, TimedEffectKind.Damage)` 단일 레코드 identity/revision 및 poison/burn provenance를 따른다. 같은 owner refresh는 restart하지 않는다. poison↔burn/다른 Damage overwrite는 기존 정책대로 이전 표현을 종료·교체하며 독립 stack을 추가하지 않는다.
- native 구현·자동 gate·빌드 시도·runtime/visual 수용 결과는 아직 인계받지 않았다. 아래 source/API 부재·이벤트 감사는 과거 시점 기록이지 최신 구현 판정이 아니다.

연관 작업: [Inferno burn·종족별 타워 Research](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md).

## 이 작업은 무엇이고 왜 하는가

버섯폭격기는 포자 폭탄을 적에게 던지는 유닛이다. 폭탄이 떨어진 자리에서 터지고, 그 주변에 있던 적들은 3초 동안 1초마다
독 피해를 입는다. 그런데 이펙트 목록 문서(`VFXSFXList.md`)에는 이 모든 것이 **이펙트 하나**(`vfx_mushroombomber_attack.prefab`, 비고 「포자 투척 폭발」)로 적혀 있다.

사용자는 지금 Unity AI 로 버섯폭격기 이펙트를 직접 만들고 있는데, 실제로 화면에 보여야 하는 것은 성격이 다른 **세 가지**다.

1. **날아가는 폭탄** — 버섯폭격기 손에서 적에게 날아가는 포자 덩어리 (`vfx_mushroombomber_tracer.prefab`)
2. **떨어진 자리의 폭발과 독구름** — 착탄 순간 터지고 3초 동안 남는 보라색 독구름 (`vfx_mushroombomber_impact.prefab`)
3. **독에 걸린 적의 몸에 붙는 표시** — 독 피해를 받는 동안 그 적에게 붙어 반복되는 이펙트 (`vfx_mushroombomber_poison.prefab`)

그래서 목록 문서의 한 줄을 세 줄로 바꾸려 한다. **프리팹은 사용자가 만들고, 이번 작업은 목록 문서만 맞춘다.**
이 문서는 그 전에 실제 파일을 열어 확인한 사실 — 버섯폭격기 공격이 실제로 어떻게 동작하는지, 지금 코드에 이 세 이펙트를 꽂을 자리가 있는지,
그리고 같은 문서를 고치는 꽃요정(BloomFairy) 작업과 어떻게 맞물리는지 — 를 정리한 것이다. 이펙트를 게임에 실제로 연결하는 일은 **이번 범위가 아니다.**

※ 근거 구분(CLAUDE.md 규칙 10): 아래 파일·행·설정값은 **2026-10-09 에 직접 실측**했다. 프리팹 사양(5절)은 호출 세션이 전달한 합의 내용의 요약이며,
생성 프롬프트 원문은 대화에 있다.

---

## 1. 지금 이름이 적힌 곳

`mushroombomber` 를 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체에서 찾았다(`_Tasks/`·`_Logs/` 제외).

| 위치 | 행 | 현재 내용 | 소속 |
|---|---|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 45 | `` `vfx_mushroombomber_attack.prefab` `` · 비고 「포자 투척 폭발」 | Ⅰ. VFX → 2. 제작 예정 VFX → **「유닛 공격 이펙트」** 표 — **이번 변경 대상** |
| 같은 문서 | 75 | `` `vfx_mushroombomber_death.prefab` `` | 「유닛 사망 이펙트」 표 — 대상 아님 |
| 같은 문서 | 256 | `` `sfx_mushroombomber_attack.wav` `` · 비고 「포자 폭발음」 | 「유닛 공격 사운드」 표 — **사용자 결정으로 이번엔 그대로 둔다**(추후 사운드 작업에서 재검토) |
| 같은 문서 | 286 | `` `sfx_mushroombomber_death.wav` `` | 「유닛 사망 사운드」 표 — 대상 아님 |
| `Assets/_Project/Docs/Assets/AssetList.md` | 154 | `mushroombomber_portrait_blue/red.png` | 초상화 — 이펙트와 무관 |

- **`vfx_mushroombomber_attack` 은 `VFXSFXList.md` 45행 1곳뿐이다.** 코드·설정 에셋·프리팹·씬 어디에서도 이 이름을 쓰지 않는다.
- **버섯폭격기 VFX 프리팹은 아직 하나도 없다.** `Assets/_Project/Prefabs/VFX/Units/` 의 프리팹은 10개(`vfx_bloomfairy_heal` · `vfx_cannon_attack` · `vfx_foxmagician_charge` · `vfx_infernospirit_charge` · `vfx_pistoleer_attack` · `vfx_quakespirit_attack` · `vfx_streamspirit_attack` · `vfx_tank_attack` · `vfx_torrentspirit_attack` · `vfx_unit_death`)이고 버섯폭격기 것은 없다.
- → **이름을 바꾸거나 나눠도 끊어지는 참조(파일·GUID·코드 문자열)가 없다.** 바꾸는 것은 문서 1행이다.

### `VFXSFXGuide.md` — 변경 불필요

| 행 | 내용 | 판정 |
|---|---|---|
| 301 | `\| MushroomBomber \| Transcendence \| 독/폭발 투척 \| spore explosion, poison cloud \|` (VFX 「1. 유닛 공격 이펙트」 키워드 표) | **파일 이름이 없는 키워드 행**이다. 키워드(포자 폭발 · 독구름)는 이번에 나누는 이펙트 중 착탄 폭발과 그대로 맞는다. 꽃요정 작업의 같은 판단(그 작업 Research.md 1절)과 같은 이유로 고치지 않는다 |
| 492 | `\| MushroomBomber \| Transcendence \| spore throw, toxic pop \|` (SFX 키워드 표) | SFX 는 이번 범위 밖이다 |

- 같은 문서 「파일 명명 규칙」(69~110행)은 `vfx_[에셋명]_[이벤트].prefab` 이다. `tracer` · `impact` · `poison` 은 모두 「이벤트」 자리에 들어가므로 **명명 규칙 안이다.**
  「attack」이 아닌 이벤트명은 이미 선례가 있다 — 실물 프리팹 `vfx_foxmagician_charge` · `vfx_infernospirit_charge`, 목록 문서의 `vfx_mistshrine_mist_cast` · `vfx_mistshrine_mist_loop`.

---

## 2. 버섯폭격기 공격은 실제로 어떻게 동작하는가

| 사실 | 값 | 근거 |
|---|---|---|
| 사거리 | 2.0 | `Assets/_Project/Docs/StatsReference.md` 91행 |
| 주 타깃 1마리 직접 피해 | **100** (건물이 주 타깃이어도 들어감 — 공성) | `StatsReference.md` 91행 · `GameSystemRules_Units.md` 규칙 39 |
| 독 지속 피해(DoT) | **초당 20 × 3초**(총 60) | `StatsReference.md` 91행 · `Assets/_Project/Resources/Config/SpecialAttackConfig.asset` 24~25행(`_blastDotPerSecond: 20`, `_blastDotDuration: 3`) |
| DoT 대상 | 착탄 중심에서 **월드 XZ 평면 거리 ≤ `blastRadius`** 인 **적 유닛 전원**(주 타깃 포함) | `GameSystemRules_Units.md` 규칙 38 |
| `blastRadius` | **1.0**(인접 1칸 거리) | `SpecialAttackConfig.asset` 23행(`_blastRadius: 1`) · `Scripts/Infrastructure/Config/SpecialAttackConfig.cs:84` |
| 착탄 중심 | **주 타깃의 월드 위치** | `GameSystemRules_Units.md` 규칙 38 |
| 틱 방식 | **1초 간격 discrete**(뚝뚝), 매초 남은 체력 데미지 텍스트 | 규칙 40 · `Scripts/Application/UseCases/UnitCombatUseCase.cs:122`(`BlastDotTickInterval = 1.0f`) |
| 건물 | DoT 없음(직접 피해만) | 규칙 38·39 |
| 아군 | 직접·DoT 모두 무피해 | 규칙 39(규칙 16) |
| 다시 맞으면 | 남은 시간·총량을 **리셋**(겹쳐 쌓이지 않음) | 규칙 34 「갱신 = 리셋」 · 규칙 40 |
| DoT 가 끝나는 때 | **지속 만료** 또는 **대상 사망** | 규칙 34 「종료 처리」(풀피 종료는 HoT 전용) |

- `SpecialAttackConfig.asset` 이 실제로 쓰이는 값인지 확인했다 — 이 에셋의 GUID(`f8598314…`)가 `Assets/_Project/Scenes/Game.unity` 에서 참조된다.
- ⚠️ **규칙 문서의 숫자를 읽을 때 주의**: `GameSystemRules_Units.md` 규칙 38~40 본문은 「직접 10」「초당 2(총 6)」로 적혀 있다. 이는 전투 스탯 ×10 이전 값이며,
  `GameSystemRules_Upgrade.md` 규칙 1 이 「DoT 틱값(MushroomBomber 2→20/s)」을 포함해 ×10 을 정하고 **개별 값은 `StatsReference.md` 가 권위 소스**라고 적는다. 그래서 위 표는 100 · 20 을 쓴다. 반경 1.0 은 ×10 대상이 아니다(같은 규칙 1 「불변」).

### 2-1. 알아둘 사실 — 독구름은 「보이는 것」이고 피해 판정은 착탄 순간 한 번이다

`Scripts/Application/Combat/BlastAttackBehavior.cs` 의 `Apply`(66행~)는 착탄 순간 반경 안의 적 유닛을 **한 번 모아**(85행 `CollectEnemyUnitsInRadius`)
각자에게 DoT 를 건다(92행 `ctx.ApplyDot`). 그 뒤로는 **각 유닛에 붙은 DoT 가 따로 돈다.**

- 그래서 착탄 뒤 독구름 **밖으로 걸어 나간 적도 3초 동안 계속 피해를 받고**, 착탄 뒤 구름 **안으로 걸어 들어온 적은 피해를 받지 않는다.**
- 즉 착탄 이펙트의 독구름(3초, 반경 1.0)은 **피해 범위를 시각적으로 암시하는 연출**이고, 실제로 누가 독에 걸렸는지는 **3번 독 상태 이펙트(적 몸에 붙는 것)** 가 보여 준다.
  두 이펙트를 나누는 이유가 여기에 있다. (이 사실은 판단이 아니라 기록이다 — 연출을 어떻게 보이게 할지는 사용자가 정한다.)

---

## 3. 지금 코드에 있는 이펙트 「칸」과 세 이펙트의 관계

### 3-1. 유닛 이펙트 칸은 4개다

`Scripts/Presentation/Effects/UnitEffectConfig.cs` 의 `UnitEffectEntry`(37~54행)에 유닛 종류마다 칸이 4개 있다.

| 칸 | 언제·어디서 재생되나 | 세 이펙트에 쓸 수 있나 |
|---|---|---|
| `attackPreset`(43행) | 공격 애니메이션의 `OnAttackHit` 이벤트 순간, **공격하는 유닛의 `VfxSpawnPoint`(없으면 유닛 위치)에서 한 번** — `Scripts/Presentation/Unit/UnitView.cs:1879~1881` → `EffectManager.PlayUnitAttack`(`Scripts/Presentation/Effects/EffectManager.cs:174`) | 세 이펙트 어느 것도 이 위치에서 재생되지 않는다(발사 순간의 손 쪽 섬광 같은 것이 들어가는 칸이다) |
| `tracerPreset`(53행) | 같은 `OnAttackHit` 순간, 사거리 ≥ 1.0 인 유닛이면(`UnitView.cs:76` `RangedAttackThreshold = 1.0f`, 1897행) **발사 지점 → 발사 순간의 타깃 위치로 직선 비행** — `UnitView.cs:1908` → `EffectManager.PlayTracer`(275~291행) → `TracerProjectile` | **1번 투사체가 들어갈 칸이다.** 버섯폭격기 사거리 2.0 은 이 분기를 탄다 |
| `hitPreset`(49행) | 피격 연출 방출 시 **맞은 유닛의 위치에서 한 번**. 단, 칸을 고르는 키가 **맞은 유닛의 종류**다 — `Scripts/Presentation/Effects/HitPresentationQueue.cs:369~374`(`PlayUnitHit(targetUnit.Type, …)`) → `EffectManager.cs:197~201` | **버섯폭격기 전용 폭발을 담을 수 없다.** 여기에 넣으면 「버섯폭격기가 때렸을 때」가 아니라 「버섯폭격기가 **맞았을 때**」 재생된다 |
| `deathPreset`(46행) | 사망 시 한 번 | 무관 |

- **착탄 지점에서 폭발을 재생하는 칸·메서드가 없다.** `EffectManager` 의 공개 재생 메서드는 `PlayUnitAttack`(174) · `PlayUnitDeath`(184) · `PlayUnitHit`(197) · `PlayBuildingDestroy`(213) · `PlayBuildingUpgrade`(224) · `PlayBuildingAttack`(239) · `PlayUi`(253) · `PlayTracer`(275)뿐이다.
  트레이서의 착탄 콜백(`onArrive`)은 지금 「피격 연출 방출 신호」 하나만 보낸다(`UnitView.cs:1908~1909`).
- **대상에 붙여 반복 재생하다가 끄는 메서드가 없다.** 위 8개는 모두 「위치에서 한 번」이고 정지·부착(대상 추종) 메서드는 없다(꽃요정 작업 Research.md 4절 1번과 같은 사실 — 재확인).
  또 풀링된 VFX 는 **파티클이 전부 사라져야 풀로 돌아간다**(`Scripts/Presentation/Effects/VfxPoolItem.cs:183~195` `AnyAlive()`) — 반복(loop) 재생 이펙트를 기존 재생 경로에 넣으면 영영 돌아오지 않는다.
- **DoT 가 걸리고·갱신되고·끝나는 순간을 알리는 이벤트가 없다.** `Scripts/Application/Events/GameEvents.cs` 의 공개 정적 이벤트 42개 중 DoT·시간 지속 효과용은 0개다(회복 쪽 `OnEntityHealed` 766행 · `OnUnitHealCastStarted` 920행만 있다).
  멀티에서도 DoT 를 알리는 RPC 는 없고(`Scripts/Infrastructure/Network/*.cs` 검색), 클라이언트는 HP·데미지 텍스트만 동기화로 받는다(규칙 40 「서버 권위」).
  → 3번 독 상태 이펙트를 켜고 끄려면 **연결 작업에서 신호부터 새로 만들어야 한다.**

### 3-2. 트레이서의 두 가지 성질 (연결 작업에서 알아야 할 사실)

`Scripts/Presentation/Effects/TracerProjectile.cs`:

1. **직선 비행만 한다** — 매 프레임 `Vector3.Lerp(_start, _target, t)`(155행). 포물선(던지는 궤적)은 없다. 포물선은 코드가 필요하며 **사용자가 나중으로 미뤘다.**
2. **착탄하면 그 자리에서 바로 꺼진다** — `Arrive()`(169~190행)가 콜백을 부른 뒤 곧바로 `gameObject.SetActive(false)`(183행)하고 풀로 돌려보낸다.
   오브젝트를 끄면 그 아래 파티클도 함께 사라지므로, 투사체의 **월드 공간 꼬리 입자도 착탄 순간 같이 사라진다.**
   꼬리가 남아 흩어지길 원한다면 연결 작업에서 다룰 일이다(판단은 하지 않음 — 사실 기록).

- 현재 설정 에셋 `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 의 25개 항목 **전부 `tracerPreset` 이 비어 있다**(실측 25/25). 즉 이 설정 기준으로는 트레이서 칸을 쓰는 유닛이 아직 없다.
  이 에셋이 실제로 쓰이는 것인지도 확인했다 — 에셋 GUID 가 `Game.unity` 에서 참조된다.

---

## 4. 지금 버섯폭격기에게는 공격 이펙트가 하나도 재생되지 않는다

| 확인 | 실측 |
|---|---|
| 설정 에셋의 버섯폭격기 항목 | `UnitEffectConfig.asset` 131~135행 `unitType: 26`(`Scripts/Domain/Unit/UnitType.cs:48` `MushroomBomber = 26`) — `attackPreset` · `hitPreset` · `tracerPreset` **비어 있음**, `deathPreset` 만 공용 사망 프리셋(`EffectPreset_Unit_Death_Common.asset`) |
| 공격 애니메이션의 이벤트 | `Assets/_Project/Animations/Units/MushroomBomber/MushroomBomber_Attack.anim` 71483행 **`m_Events: []`** — `OnAttackHit` 0건 |
| 그 클립이 실제 공격 상태에 쓰이는가 | 예 — `MushroomBomber.controller` 83행 `m_Name: Attack` 상태의 모션(96행)이 이 클립의 GUID(`8b7e1085…`)를 가리킨다 |

- `OnAttackHit` 이 불리지 않으므로 `UnitView.OnAttackHit()`(1865~1919행) 안의 공격 VFX·SFX·트레이서 발사가 버섯폭격기 몫으로는 **실행되지 않는다.** 칸에 프리셋을 넣어도 지금 상태로는 보이지 않는다.
- 피해 자체는 서버 타이머(`HitFrameTimes`)로 들어가므로 영향이 없다(규칙 18).
- ⚠️ **규칙 문서와 실측이 어긋난다.** `GameSystemRules_Units.md` 규칙 27 은 「MushroomBomber는 클립에 `OnAttackHit` **1개** 주입 완료(2026-07-19, 규칙 38~40)」라고 적는다.
  실측은 0건이다. 그 뒤에 이벤트가 지워진 것인지, 다른 이유인지는 이 문서에서 확정할 수 없다(git 명령 금지 — CLAUDE.md 규칙 5). **이번 범위에서는 규칙 문서를 고치지 않고 사실만 기록한다**(7절).

---

## 5. 합의된 프리팹 사양 (요약 — 생성 프롬프트 원문은 대화에 있다)

저장 위치는 `VFXSFXGuide.md` 「폴더 구조」에 따라 `Assets/_Project/Prefabs/VFX/Units/`.

| 파일 | 무엇 | 재생 | Stop Action | 시뮬레이션 공간 | 구성 |
|---|---|---|---|---|---|
| `vfx_mushroombomber_tracer.prefab` | 날아가는 포자 폭탄 — 지름 약 0.25 의 둥근 갈색 포자 구 + 보라 독가루 꼬리 | **반복(loop)** — 비행 동안 | None | 몸체 Local / 꼬리 World | — |
| `vfx_mushroombomber_impact.prefab` | 착탄 폭발 — 갈색 포자 파열(0~0.5초) + 보라 독구름 · 지면 원 · 독 기포가 **월드 반경 1.0** 에 **정확히 3.0초**(DoT 지속과 동일), 2.5→3.0초 사이 페이드아웃 | **1회** | Destroy | — | `Spore_Burst` / `Poison_Cloud` / `Poison_Ring` / `Toxic_Bubbles` |
| `vfx_mushroombomber_poison.prefab` | 독 상태 표시 — DoT 를 받는 적 유닛 각각에 붙음, 은은한 톤, 약 0.2초 페이드인. **정지 시 방출만 멈추고 남은 입자는 자연히 사라진다**(꽃요정 치료 이펙트와 같은 방식) | **반복(loop)** — 코드가 켜고 끔 | None | Local | `Poison_Bubbles` / `Toxic_Drips` / `Poison_Aura` |

- 착탄 이펙트의 반경 1.0 · 3.0초는 **지금의** `blastRadius`(1.0) · `blastDotDuration`(3) 값에 맞춘 것이다. 이 두 값은 Inspector 에서 바꿀 수 있으므로(규칙 25·40), 값이 바뀌면 프리팹이 저절로 따라가지 않는다(Plan.md 위험 요소).
- 독 상태 이펙트는 꽃요정 치료 이펙트와 같은 부류다 — **대상에 붙어, 길이가 매번 다른 반복 재생**(DoT 는 만료·사망으로 끝나고 재부여 시 리셋되어 길어질 수 있다 — 2절).
  사용자가 이 이펙트를 「유닛 공격 이펙트」가 아니라 **「유닛 특수 효과」** 절에 두기로 정했다.

---

## 6. 「유닛 특수 효과」 절과의 선후 관계 (꽃요정 작업)

3번 독 상태 이펙트가 들어갈 「유닛 특수 효과 이펙트」 절은 **아직 없다.** 그 절은 꽃요정 작업의 Plan 이 정의한다 —
`Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md` 2절 **B-2**(VFX 절 신설: 위치 · 제목 · 머리 안내문 3줄 · 4열 표)와 **B-4**(SFX 절 신설). 사용자는 그 문서 변경을 **직접** 하겠다고 했고 순서는 정해지지 않았다.

2026-10-09 실측(이 문서 작성 시점):

| 확인 | 결과 |
|---|---|
| `VFXSFXList.md` 의 `#### 유닛 특수 효과 이펙트` | **0건** — 절 없음 |
| `VFXSFXList.md` 의 `#### 유닛 특수 효과 사운드` | **0건** |
| 꽃요정 VFX 행의 위치 | 아직 「유닛 공격 이펙트」 표 46행(`` `vfx_bloomfairy_attack.prefab` ``) — 꽃요정 문서 변경은 아직 안 됐다 |
| 「유닛 공격 이펙트」 표의 행 수 | **25행**(Human 8 · Spirit 9 · Transcendence 8) |

두 작업이 서로에게 주는 영향:

1. **어느 쪽이 먼저든 마지막 모습이 같아야 한다.** 그래서 이번 Plan 은 「절이 이미 있으면 행만 넣고, 없으면 꽃요정 Plan B-2 그대로 만든다」는 두 경우를 모두 정한다(Plan.md 2절).
2. **꽃요정 Plan 7절의 확인 기대값 하나가 달라진다.** 그 Plan 은 꽃요정 행을 뺀 뒤 「유닛 공격 이펙트」 표가 **24행**(Transcendence 7)이 된다고 적었다.
   이번 작업은 그 표에서 1행을 2행으로 바꾸므로, 이번 작업이 먼저 반영되면 꽃요정 작업 후 그 표는 **25행**(Transcendence 8)이 된다. 「유닛 공격 사운드」 표는 이번에 건드리지 않으므로 24행 그대로다.
3. **꽃요정 Plan 이 적은 행 번호가 1칸씩 밀린다**(예: B-2 의 「76행」「78행」). 그 Plan 도 「위치는 행 번호가 아니라 절 제목으로 찾는다」고 적어 두었으므로 절차 자체는 그대로 쓸 수 있다.
4. 이번 작업이 먼저 절을 만들면, 꽃요정 작업은 B-2 에서 **절을 만들지 않고 꽃요정 행만 넣으면 된다.**

---

## 7. 작업 중 발견한 부가 이슈 (이번 범위 밖 — 보고만)

| # | 내용 | 실측 |
|---|---|---|
| 1 | `GameSystemRules_Units.md` 규칙 27 의 「MushroomBomber 클립에 `OnAttackHit` 1개 주입 완료」와 클립 실측(이벤트 0건)이 어긋난다. 꽃요정의 같은 부류 불일치(꽃요정 작업 Research.md 5절 3번)와 함께 확인할 거리다 | 4절 |
| 2 | `VFXSFXList.md` 「1. 완성된 VFX」(10~12행)가 「현재 없음」인데 `Prefabs/VFX/Units/` 에는 프리팹이 10개 있다 — 꽃요정 작업 Research.md 5절 1번에서 이미 보고된 것과 같은 사안이다 | 1절 |

→ 둘 다 이번 작업(목록 문서의 이름 나누기)과 무관하므로 고치지 않는다(CLAUDE.md 규칙 6). 필요하면 별도 작업으로 제안한다.
