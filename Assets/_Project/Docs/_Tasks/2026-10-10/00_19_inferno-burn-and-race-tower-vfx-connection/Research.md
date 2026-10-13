# Research — Inferno burn 및 종족별 방어 타워 VFX 연결

> **Human ONLY 최종 적용(Main 실제2파일 검토 완료):** Human 별도 preset의 _vfxPrefab ONLY → Assault(type1)의 실제 production VFX: EffectPreset_Pistoleer_Attack → vfx_pistoleer_attack, GUID badc7c96fdc06564fb9f9af71d1ddf68/root5594113295059828660. 기존 fixture는 Tank/Cannon=actual Tank, Assault/Human=actual Assault 및 Human 별도 preset을 검사한다. Editor static exit0/errors0/warnings0, 문서 담당 현재2해시도 인계와 일치. Assault 기존 preset·Tank/Cannon 참조 불변(Main/programmer); 다른 필드·VFX 실물·gameplay/Stats 변경 없음. 새 native/UI/build/Git 미실행·실기/pixel 미검증. Inferno는 사용자 clip/event 변경 보고만 유지하며 inspection/hash·Stats 동기화 검증 없음. burn fitting·Spirit endpoint·Tank/Cannon diagnostics deferred/미해결. 아래 대기/계획 및 이전 native는 단계별 이력이다.

### Human ONLY 실제2파일 manifest

- Assets/_Project/Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset — SHA256 FE1CB10BC3BC8D3E904E5F0B14B472A9E3C6CFDB0071AA9A18AF28EFC10792BA
- Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs — SHA256 2465795E387399EF9F65299E87471F32791A8ACCCE93F9477BBC0DEA055D3F9B

Assault56 Ready/fail0는 앞선21_43 경기의 제한된 clock/result 근거다. source 교집합51·종료5(버그 아님)·source-only1 unresolved 및 이전64/50 원인·timing/pixel OPEN 유지. 이번 Human 변경을 Assault clock/source의 새 수정으로 주장하지 않는다.

## HumanTower ONLY — 승인 계획 ready / 구현 인계 대기

**Main Plan review GO / 실제 참조 확인:** Assault(type1)의 UnitEffectConfig `60d8…`는 `EffectPreset_Pistoleer_Attack` → 실제 `vfx_pistoleer_attack`을 사용한다(Main 조회 인계). 선택 기준은 이름이 아니라 이 실제 production identity: GUID `badc7c96fdc06564fb9f9af71d1ddf68`, root fileID `5594113295059828660`이다. Human 별도 preset prefab 참조 ONLY + 기존 VfxConnections fixture 두 파일 구현을 Main이 승인했다. 실제 적용·최종 manifest/정적 결과 인계는 대기이며 native/build는 요청·실행되지 않았다. 기존 `vfx_assault_attack` 이름의 asset를 자동 선택하지 않는다. 모든 다른 보류 범위는 아래와 같이 유지한다.

**최신 HumanTower ONLY 사용자 승인:** Human 별도 tower preset의 prefab 참조만 기존 Assault actual VFX로 교체한다. Tank/CannonCart는 Tank VFX 및 shared Cannon preset 유지. Human preset 이름/SFX/offset/scale/나머지 값·VFX 실물·피해/HP/주기/범위/Stats 불변. 실제 programmer 경로/identity 조회·Main 검토·구현 인계 대기, 새 정적/native PASS나 빌드 주장 없음. Inferno는 사용자 clip/event 변경 보고만 기록하며 확인/hash 조회하지 않는다; 새 값·Stats 동기화 미확인. burn recipient fitting·Spirit original-target endpoint·Tank/Cannon diagnostics는 보류/미해결. 이전 세대상 Tank 계약은 당시 이력이다.

1. Programmer 실제 조회로 Human 별도 preset과 Assault production VFX의 정확한 path/identity를 Main에 인계한다. 이름만으로 참조를 가정하지 않는다.
2. Main 검토 후 기존 Human preset asset의 prefab 참조 ONLY 교체한다. **VFX 실물 prefab/particle는 수정하지 않는다.** 새 preset은 만들지 않는다.
3. 기존 VfxConnections fixture에서 Tank=Tank / CannonCart=Tank / HumanTower=Assault actual identity 및 Human 별도 preset을 검사한다. lifecycle/config 준비·실제 tracer/HP/death/remove assertions는 유지한다.
4. Programmer 실제 manifest/검증 근거 후 적용 상태를 추가한다. 새 Task/TC·다른 구현·빌드는 요청되지 않았다.

## 21_43 최종 로그·소스 인계 — Main/programmer 확인 / visual OPEN

- **동일 경기:** 위 match/sharedSession의 Android Host / Editor Client이며 아래 수치는 Main/programmer 최종 read-only 인계다. 앞선 부분 집계는 이력으로 보존한다.
- **Assault:** 양쪽 C2/C3 results56 / Ready56, fail/reject/pending0; Client Receive56 전부 Assault. Client VFXStarted52와 exact 결과 교집합51, result-only5(unit0/7/15/30/35)는 attacker death/despawn 종료에 연결되므로 버그5건으로 세지 않는다. source-only1(unit20, instance7, seq3)은 unresolved다. 실제 marker5/30초·cycle10/30초 확인. clockInvalid1은 GameEnd transport closure(pending0), 이번 경기 Expired 없음. 이는 이번 경기 범위이며 이전 original64/remaining50 원인이나 timing/pixel을 닫지 않는다.
- **Inferno 실제 binding:** 양쪽 prefab/controller가 실제0.5초 marker clip에 연결, Stats0.5초/cycle3초 확인. StatsReference `1:15=1.5s`와의 모순은 강한 조기 타격 후보이나 native Start↔HP 정밀 측정은 없다. 현행0.5 확인과 향후 의도1.5 제안은 구분한다. source attack Host raw132/started122/suppressed10, Client131/120/11, playbackFailure0, overflowTrue라 완전 source coverage/visual PASS가 아니다.
- **Burn:** Host Play/Drain/Return 각각32, Client 각각31; Host StopBeforeReady4/staleClosedReject27. Publish40/Receive38 차이는 current-value merge 가능성이 있어 loss2로 세지 않는다. fixed scale1/no recipient bounds fitting 소스 확인은 사용자 크기 관찰과 양립하며 pixel·정확한 원인 수용은 미확정이다.
- **Tank/CannonCart:** current actual Tank 참조/해시 일치. 해당 Legacy 타입별 per-attack source emitter diagnostics가 없어 간헐 누락 원인은 UNKNOWN; Assault C3 성공을 이 두 타입 VFX 성공으로 확대하지 않는다.
- **Tower:** Human22+Spirit8=30, Host publish / Client receive / play exact identities missing/extra0. 현행 source는 방향만 사용하며 actual target transform 추적/거리 refit 없음. 사용자에게 길이가 잘 보였던 관찰을 부정하지 않되 dynamic code fitting의 증거로 쓰지 않는다. 모든 particle/pixel 수용은 미확정이다.
- **Main 후속 제안만(미적용):** 사용자 의도에 따라 Inferno1.5초 여부 확인; burn recipient fitting; Tank/Cannon production boundary diagnostics; Spirit endpoint original identity의 visual-only 추적; Human은 별도 tower preset 참조만 Assault로 변경. 현재 세대상 Tank 참조 계약 유지, 규칙/spec 변경 및 gameplay·소스·에셋·빌드 작업 없음. 기존 Task 안에서 근거/승인 검토를 이어가며 새 Task/TC는 만들지 않는다.

## 2026-10-10 21_43 실기 후속 — 부분 근거 / 분석 OPEN

- **경기 식별(Main 인계):** match `f226c344-2531-47db-8ce1-bc3818fa2eb3`, device Host / Editor Client, sharedSession `febbd6c5502bd937d1b8dd788b73ff8f2cf1959155bbf672262c5d7583e47680`. 저장 로그: `Assets/_Project/Docs/_Logs/2026-10-10/21_43_logcat/RuntimeLog_device.txt` 및 `Assets/_Project/Docs/_Logs/_editor/2026-10-10/RuntimeLog.txt`.
- **사용자 관찰:** Inferno 피해가 공격보다 일찍 보임; burn 크기/모양이 recipient에 따라 너무 크거나 어색함; Tank/CannonCart 효과가 간헐적으로 빠져 보임. 이는 사용자 시각 관찰이며 측정된 타격 오차·누락 건수·원인 확정이 아니다. Assault도 테스트 대상이나 이 단계의 Assault 단독 판정은 없다.
- **후속 요청(미적용):** SpiritTower 표현 수명 동안 원래 맞은 이동 유닛을 endpoint가 추적; HumanTower는 향후 Assault VFX 사용. 현재 Tank/CannonCart/HumanTower actual Tank asset 공유 계약은 그대로다. 새 요청을 현행 사양/구현으로 바꾸지 않으며 규칙·코드·에셋 수정, 새 Task/TC는 없다.
- **Main read-only 소스 확인:** `VfxPoolItem.TimedDamage` line30은 original scale × preset multiplier 고정, Follow line39–40은 position/rotation만 갱신; Burn preset offset0/scale1로 recipient bounds fitting 없음. `EffectManager.PlayAttackEffect` lines405–425는 source/impact vectors만 받으며 target reference 없음. 이는 현재 소스 제한이며 모든 관찰 증상의 runtime 원인을 증명하지 않는다.
- **Inferno timing 미확정:** disk clip marker0.5(line93390)와 StatsReference의 `1:15=1.5s`가 다르다는 Main 확인까지다. 실제 binding/runtime 조사 중이므로 live1.5s나 잘못된 실제 타격값이라고 단정하지 않는다.
- **Editor 부분 로그(Main 확인):** END C3 results56 / ready56 / released56 / fail0. coordinator clockInvalid1, coverageUnknownTrue; Inferno raw131 / started120 / suppressed11, overflowTrue. 타입별 최종 상관관계 전의 subset이며 전체 VFX 완전성·timing/pixel PASS 또는 Tank/Cannon 누락 반증이 아니다.
- **현재 경계:** 앞선16:42/16:43 native PASS는 self-validation 이력이지 새 경기 수용 판정이 아니다. original64 입력 부족·remaining50 원인 및 새로운 타입별 상관관계·clock coverage·시각 원인은 OPEN. 사용자 실기 사실만으로 이전 빌드 시도의 완료/설치 또는 실행 artifact identity를 새로 확정하지 않는다. Main/programmer 최종 로그 근거 후 추가한다.

> **2026-10-10 최종 구현/native·빌드 시도 인계(Main):** UnitAction16:42:39 ALL terminal PASS(C2/C3 ProductionTimeline·baseUAS·VfxConnections 전 경계·Assault canonical current fixture), ROOT16:43:22 PASS, 동일 final fixture generation, Console errors0/warnings30. Tank/CannonCart/Human 실제 Tank identity 연결 및 두 fixture-only lifecycle/Stats 초기화 RED는 해소됐지만 날짜별 실패 이력은 보존한다. final fixture SHA256 `EB64059B75A20B83A5E4A16DD9A9423467B30F7AD2D3456995DBD7B3006B3E5B`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1`. post-gates Main source-only11 Assault/protected 대조 불일치0·final2해시 exact match, 새 fixture 보완에 Production/Stats 변경 없음. **Build And Run16:43:57 KST(07:43:57 UTC) File 메뉴 클릭1회**, 즉시 Checking prerequisites/Starting Android build만 관찰하고 UI/log/process monitoring 전부 중단했다. 완료·설치·실행/runtime/visual 미확인, scene save/Git 없음. 이 native PASS는 기존 self-validation 범위다. original64/remaining50 원인·Assault 실기 timing/pixel·전체 migration은 OPEN이며 counterfactual14 Ready/50 Expired를 실기 해결로 쓰지 않는다. 아래 pending/RED/빌드 미시도는 당시 이력이다.

> **두 번째 fixture 보완 적용·Main 실제 소스 검토 완료:** actual config AutoTower/Spirit row로 temporary data 초기화·lethal preflight를 적용하고 finally 첫 단계에서 EXACT 기존 `_data` 참조를 복원한다. throwing subscriber/death/remove assertion은 유지, Production/Stats asset 변경 없음(Main/programmer 인계). 현재 Editor compile exit0/errors0/warnings0. 현행 fixture SHA256 `EB64059B75A20B83A5E4A16DD9A9423467B30F7AD2D3456995DBD7B3006B3E5B`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1` 불변; 문서 담당 현재2해시도 일치. 이전93B11D…/1C196A…는 단계별 이력이다. fresh native 결과는 대기,16:32 RED→lifecycle 보완→16:37 tracer/impact/HP 경계 통과·후속 nullStats fixture FAIL→두 번째 보완 적용을 구분한다. ROOT16:33 PASS 별도·build 미시도·runtime/visual OPEN이며 전체 PASS로 쓰지 않는다.

> **최신 native — Main16:37:26 인계:** fresh UnitAction에서 이전 missing manager/tracer 실패는 해소됐고 실제 tracer·impact/HP assertions 통과, 해당 END failures0을 확인했다. 이후 VfxConnections line256 `Expression subscriber exception cannot prevent death/remove workflow` assertion에서 새 FAIL이다. tower fixture의 attack applied0/HP1(nullStats 컨텍스트)은 관찰값이며 원인 확정이나 production death/remove 결함 증명이 아니다. programmer read-only 진단 대기, build 미시도·runtime/visual OPEN.16:32 RED→fixture lifecycle 보완→16:37 이전 경계 통과/후속 경계 FAIL을 분리하고 전체 PASS로 쓰지 않는다. ROOT16:33:29 PASS는 독립 이전 결과다. 아래 기존 대기/실패 문단은 당시 이력이다.

## 최신 구현 인계 — 2026-10-10 Main 실제2파일 검토 완료

**16:32 RED 원인·최소 fixture 교정 적용(Main 실제6행 patch 검토):** EffectManager Instance는 private Awake에서만 등록되고 ExecuteInEditMode가 없다. fixture AddComponent/Initialize가 lifecycle을 누락했으며 tracer asset 참조 존재·event 전달은 확인됐다. 같은 existing VfxConnections 파일에 actual Awake 호출 + Require Instance==manager + tracer nonnull preflight를 적용했고 Main이 실제6행 patch를 검토했다. 최종 SHA256 `93B11DB06A043E3B86E8002BB1991F05016718F124BECABBA11AE22BC6AF9878`, Cannon preset SHA256 `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1` 불변. 기존 actual tracer-play/HP assertion은 약화하지 않고 production 소스/asset 변경은 없다. 현재 Editor compile exit0/errors0/warnings0; Runtime 불변·이전 static4 성공은 별도다. 교정 후 새 native 결과는 인계 대기이며 Action16:32:17 RED는 보존한다. ROOT16:33:29 PASS(Main UIstatus/Editor.log)와 source2 frozen/protection match는 별도, build 미시도·runtime/visual OPEN.

> **최신 native — Main16:32:17 인계:** fresh UnitAction FAIL. Tank/CannonCart/Human exact loaded Tank identity 및 별도 Human assertions는 먼저 통과했고, 이후 `RunUnitActionSelfValidation.VfxConnections.cs`의 `ValidateWriterSnapshotVfx` line219에서 `Actual Unresolved writer event reaches actual tracer play, no committedSchedule dependency.` assertion이 실패했다. 이전 Tank identity 실패로 분류하지 않는다. 원인·수정은 Main/programmer read-only 진단 대기이며 fresh ROOT16:33:29 PASS(Main UIstatus+Editor.log), source2 frozen/protection match(Main 인계), build 미시도·runtime/visual OPEN이다. 아래 static/hash 성공은 이 native FAIL과 별도다.

세 대상의 실제 공격 VFX 연결 변경이 구현돼 Main 검토를 마쳤다. 아래 구현·정적 검사 근거와 아직 대기 중인 새 native/build/화면 수용은 서로 다른 단계다. 아래 기존 승인/인계 대기 문단은 이전 단계 이력으로 보존한다.

**아래 표는 lifecycle 보완 이전 최초2파일 인계 이력이다.** Cannon preset hash는 유지되며 fixture의 현행 최종 hash는 위93B11D… 보완 인계가 우선한다. 이전1C196A… hash를 현행으로 재사용하지 않는다.

| 최초 수정 파일 (추가 파일 없음, 후속 fixture hash 위 참조) | 당시 실제 변경 | 당시 SHA256 |
|---|---|---|
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Cannon_Attack.asset` | `_vfxPrefab`만 `Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab` actual root로 변경. 이름·SFX·offset·다른 필드 보존, 새 preset/실물 VFX 변경 없음 | `61A7A3F0F77525CE17050EF2648EEB4F361B39A012FC112B7A7F4B9374E4B8F1` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | loaded Tank path와 Tank/CannonCart/Human production lookup의 세 actual identity 일치 및 Human 별도 preset을 명시적으로 검사. 기존 fixture 확장, 새 메뉴/validator/TC 없음 | `1C196A354B3601B4EA00038D3AC307900DD6DC4B3A971EDD52850CAED03879FF` |

- 근거 구분: 실제2파일 manifest/변경 범위·정적 결과는 Main/programmer 인계이며 문서 담당도 현재 SHA2562개를 직접 재확인해 모두 일치했다. fixture 코드를 읽어 actual loaded prefab identity와 별도 Human assertion 연결을 확인했지만 Unity fixture 실행은 수행하지 않았다.
- programmer static4 Runtime/Editor/Development/Release exit0·errors0, warnings20/0/20/24. 이전 Replay/Dev/Release CLR3 모두 exit0는 Assault 근거이며 이번 VFX native 또는 visual PASS가 아니다.
- Assault5·보호6·clip 불변은 Main/programmer 인계. HP/피해·서버즉발·주기·범위·Stats·marker 불변, Spirit RuneSpire·Transcendence 미지정/no fallback·Type+Race 유지. shared preset의 이름은 실제 선택 prefab의 증거가 아니다.
- 새 native/build 결과는 Main UI 인계 대기다. 이전 Action15:47:57 FAIL은 교정 전 actual Tank identity mismatch, ROOT15:52:38 PASS는 독립 이전 결과로 보존한다. 교정 후 전체 PASS/빌드 시도·완료·설치·runtime/visual 수용은 주장하지 않는다.
- 다음 문서 반영은 실제 새 native/build·사용자 화면 수용 근거 수신 후이며 원64/remaining50 지연 원인과 Assault 전체 timing/runtime OPEN을 닫지 않는다.

## 최신 사용자 선택 확정 — Tank·CannonCart·HumanTower 공유 (2026-10-10)

**Main 기술 Plan 승인 추가:** existing shared `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_Cannon_Attack.asset`의 `_vfxPrefab` 참조만 `vfx_tank_attack` actual root로 변경한다. preset 이름·SFX·offset·나머지 값 보존, 새 preset 없음. Human 별도 preset을 유지하고 기존 fixture에서 Tank/CannonCart/Human3 identity를 명시적으로 확인한다. 이름은 actual selected asset의 증거가 아니다. 구현 적용/manifest 및 native/build/visual 결과는 programmer/Main 후속 인계 전 미확인.

사용자는 세 공격 표현 모두 기존 `vfx_tank_attack`을 사용하도록 승인했다. 화면 품질은 후속 실기에서 확인한다. Human 선택 대기는 해소됐지만 아직 이 선택의 실제 구현 manifest/새 native/build 결과를 받지 않았으므로 **사양 확정·구현 인계 대기·visual OPEN**이다. 아래 기존44 freeze와 실패/보호 기록은 당시 이력이다.

- 변경 전 실제 Tank type6는 `UnitEffectConfig.asset`→`EffectPreset_Cannon_Attack.asset`→`vfx_cannon_attack`였으며 UnitView/EffectManager override 없음. 새 Human tower는 `EffectPreset_HumanAutoTower_Attack.asset`→`vfx_tank_attack`여서15:47:57 기존 fixture가 거부했다. ROOT15:52:38 독립 PASS는 그 실패를 상쇄하지 않는다.
- 최신 승인 범위: Tank와 CannonCart의 공격 **설정 참조**를 함께 `vfx_tank_attack`으로 정합화하고 Human tower는 동일 prefab의 별도 tower preset을 유지한다. 기존 “Tank preset/값 불변” 보호는 이 공격 VFX 참조 변경에 한해서 superseded다. VFX prefab/재질/입자/텍스처 등 실물 자체는 변경하지 않는다.
- `BuildingType.AutoTower` 단일 및 Type+Race 선택, Spirit RuneSpire, Transcendence 미지정/no fallback은 유지한다. 서버 피해·HP 단일writer·즉발 타워 피해·쿨다운·사거리·Stats·클립·marker·Assault canonical clock은 변경하지 않는다.
- 정책 근거: Buildings 방어 타워 규칙9·12, Units 전투 연출 규칙17~21, NET-PRESENT-003의 표현/피해 분리. 새 결정은 표현 에셋 identity 선택이지 공격 권위/시점 변경이 아니다.
- programmer 구현 후 실제 수정 파일·GUID/full hash·세 production 참조와 별도 Human preset 확인을 기록한다. 기존 VFX44 snapshot은 과거 baseline이며 새 manifest 없이 changed/unchanged 개수를 추정하지 않는다. 새 static/native/build 사실은 Main 결과 수신 후 기록한다. 새 Task/TC/asset 제작·UI·Git 없음.

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

불 지속 피해를 받는 적에게 불꽃 표시를 붙이고, 각 종족의 타워가 자기 종족에 맞는 발사 이펙트를 사용하도록 연결한다. 피해·체력·공격 주기와 사거리는 바꾸지 않는다. 제작된 프리팹이 존재하는 것과 실제 게임 연결·검증·사용자 시각 수용을 따로 기록한다.

## 현재 근거와 확정 사양 (2026-10-10)

**재개 중간 근거 — 2026-10-10 Main 인계:** 사용량 초기화 후 submission `01a1233b-895e-7530-aade-f82827243652`로 재개했다. programmer 09:36 소스 수정 및 09:42 `RunUnitActionSelfValidation.cs` 수정 진행을 Main이 확인했다. 이는 최종 구현 freeze·전체 inventory·정적 결과 인계가 아니다. native UI는 `sky.list_apps/list_windows`에서 Unity window67396 확인, 최초 `activate_window` timeout 후 returned window 재선택·`get_window` 복구 및 재시도도 timeout이었다. Main은 computer-use 복구 규정에 따라 native 입력을 중단했다. **이번 두 메뉴 미실행 / 빌드 미시도 / 완료·설치·runtime 미확인**이며, 게임 소스 결함이나 native gate FAIL로 분류하지 않는다. 최종 구현·static 결과는 programmer freeze 회신 후 별도 기록한다.

Task 시간은 생성 준비 시 실측 UTC 2026-10-09 15:19:23 → Asia/Seoul 2026-10-10 00:19:23이다.

- Inferno burn/RuneSpire attack 실물·GUID 미연결 실측은 [VFX 목록](../../../Assets/VFXSFXList.md)의 2026-10-09 기록에 있다. 기존 Inferno source attack focused PASS는 새로운 burn 수용 근거가 아니다.
- 타워는 `BuildingType.AutoTower` 하나다. 선택 키는 **Type + Race**. Human은 `Assets/_Project/Prefabs/VFX/Units/vfx_tank_attack.prefab`을 별도 타워 preset으로 재사용하되 Tank 값은 불변. Spirit은 RuneSpire VFX, Transcendence는 미지정이며 타 종족 fallback 금지다.
- 문서 담당 읽기 실측: 기존 `Scripts/Presentation/Effects/BuildingEffectConfig.cs`는 BuildingType-only dictionary/GetAttack이고 Race 키가 없다. Tank prefab meta GUID는 `0a9fb5876c31b9b4493bccb1c54e4cfa`다. 이 확인은 실제 연결·native 재생 증명이 아니다.
- Main/programmer 인계: 기존 tower payload에 권위 impact/race/id가 부족하다. `TowerCombatUseCase`의 확정 snapshot과 `GameEvents`/`NetworkCombatController` 표현 전달을 보강해 동일 결과 identity·Impact 위치/시각을 연결한다. 현재 target 재조회나 tracer 도착으로 피해를 지연하지 않는다.
- burn은 실제 writer의 Damage 단일 레코드와 effect identity/revision/provenance를 따른다. poison↔burn/다른 Damage overwrite는 기존 단일 효과 정책대로 이전 표현 종료·교체, 독립 stack 금지다. 동일 owner refresh noRestart, late-init/restoration/shutdown 보호가 필요하다.

## 범위와 미완 상태

**2026-10-10 단계 갱신:** Main 기술 검토·실파일 확인 완료, programmer 구현 허가 전달·구현 진행 중. 아래 초안 단계 기록에서 진전했으며 최종 구현/native/build/runtime 결과는 여전히 인계 대기다.

확정 사양 및 초기 기술계약 Main 검토 통과 인계까지다. 구현·새 정적/native 자동 gate·이번 build attempt·runtime/visual 결과는 아직 없다. 피해/HP writer·기간·주기·범위·clip/Stats/30fps 권위와 Unresolved Legacy 분류를 유지하며 C3 Supported 승격은 하지 않는다. 구체 후보와 단계는 [Plan](Plan.md), Mushroom 확장은 [기존 Research](../../2026-10-09/08_12_mushroombomber-vfx-split/Research.md)를 따른다.
