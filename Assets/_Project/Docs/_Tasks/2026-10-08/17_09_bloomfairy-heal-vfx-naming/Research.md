# Research — BloomFairy 힐 이펙트 이름 정리 (attack → heal)

꽃요정의 치료가 실제로 적용된 아군에게 만든 힐 이펙트를 붙이고, 회복이 이어지는 동안 유지하다 끝나면 자연스럽게 사라지도록 연결하는 것이 목적이다. 치료량과 타이밍은 바꾸지 않고 서버의 실제 치료 상태를 기준으로 모든 화면에서 같은 대상을 표시한다. 현재는 연결 방법을 문서로 정리한 단계이며, 실제 연결 작업은 구현 승인을 기다린다.

## 최종 구현·검증 인계 — native PASS / 실기 pending (2026-10-09 19:39 추가)

**연동 구현 및 Main의 새 Unity Action·Root self-validation PASS. 실제 멀티/기기 visual은 미검증이며 Task 전체 완료·실기 PASS가 아니다. Main 빌드 시도1회, 준비 시작(JDK checking 관찰); 완료・성공・설치는 미확인이고 사용자 담당이다.** 아래 과거 “승인 대기/FAIL/수정 중/native pending/Root 미실행/빌드0”은 당시 이력으로 보존한다. 현재 상태는 이 요약만 우선한다.

- **실제 native 증거(Main):** latest Editor DLL19:35:39, 최종 fixture SHA `8D020E86403F2DB7D6E4758CBD92149EBC98E0D82036668B270F7E638CFF3BD4`가 적용된 상태에서 19:36 Action 실행. stress 종료 뒤 전체 `UAS-DIAG` PASS line1678501, C3 PASS line1678534, Torrent production PASS line1678448. 새 Root selfValidate PASS line1678545. 이 번호는 Main 인계의 로그 line anchor로 보존하며 문서 담당의 독립 UI 실행/로그 재측정은 아니다. self-validation PASS는 실제 경기의 ROOT/C3·멀티/기기 visual 수용과 구분한다.
- **구현 결과:** 서버 실제 recipient HoT 수락·갱신·종료 provenance를 latest NV 상태로 전달하고 sole presenter가 all5PS loop를 표현한다. 동일 HoT refresh는 restart 없이 유지하며 actual stop 후 natural drain·lease 회수, late-ready/current 복원·lifecycle 종료와 bounded diagnostics를 연결했다. 기존 치료량·타이밍·단일 gameplay writer를 바꾸지 않는 연동이다. 실제 게임 화면의 대상 추종·회복 종료 잔상·멀티 일치는 사용자 실기 확인 대기다.
- **fixture 교정의 최종 순서:** 강제 Simulate가 paused state를 만든 것이 이번 drain fixture 실패 원인으로 확인됐다. 최종 fixture-only 수정은 Simulate 뒤 `StopEmitting`을 NoClear로 reassert하여 native stopped state를 복구하고, 그 후 count0 assertion을 수행한다. 아래 19:33의 “count0 검사 먼저”는 중간 설명 이력이며 이 최종 순서로 정정한다. production `VfxPoolItem` hash·기존 predicate는 그대로이며 production drain 원인/결함 수정으로 일반화하지 않는다.
- **최종 integrity(Main 실측):** 구현 source18 hash match 및 보호6 hash unchanged. assertion 파일은 `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`, SHA는 위 최종 값이다. 19:39 전후 정적 재실행 인계에서도 runtime/source 변경은 없다.
- **정적 컴파일:** programmer 정적4 errors0 보고에 이어 **Main 직접 정적4 재실행 exit0** 확인(19:39 전후). Release에는 기존 CS0168 2건·CS0219 1건, 총 warnings3가 남았으며 CS0618/CS0414는 suppressed다. warnings0·무경고 검증으로 쓰지 않는다.
- **검증 자료와 한계:** `Temp/Bloom.Runtime.rsp/.dll`, `Temp/Bloom.Editor.rsp/.dll`, `Temp/Bloom.Dev.rsp/.dll`, `Temp/Bloom.Release.rsp/.dll` 및 같은 stem의 `.pdb/.ref.dll`이 있다. 문서 담당은 파일명 존재를 확인했으며 실행 결과는 programmer/Main 인계다. Temp 자료는 임시 compile artifact이지 저장된 기기 경기 증거가 아니다. Dev/Release는 **57 Editor refs를 사용하는 define-switch 검사**로 real player/native build가 아니다. 순수 identity CLR 회귀 PASS도 particles·NGO·PlayerLoop·실제 게임 검증을 대신하지 않는다. native 메뉴 PASS는 위 별도 Main 실행 증거로만 인정한다.
- **빌드 인계(이번 추가):** Main이 Unity `File → Build And Run` element212를 1회 클릭했다. immediate capture에서 JDK checking progress를 관찰했으며 build preparation started까지 확인됐다. 사용자 지시에 따라 그 뒤 UI/log/process/build 결과 polling은 하지 않았다. **시도1·준비 시작만 확인**, 완료·성공·설치·실기 결과는 미확인이고 사용자 담당이다. 이번 문서 담당도 결과를 추가 확인하지 않는다. Scene/assets/source 수정 없음, latest source/protected hash unchanged는 prebuild 시점의 Main 인계이며 빌드 후 재측정 주장으로 확장하지 않는다.
- **UI PASS 시각 추가(Main):** 실제 Console Action/C3 PASS19:37:14, Root PASS19:38:13. 기존 로그 anchors1678501/1678534/1678448/1678545와 같은 검증 증거이며 추가 경기나 별도 재검증 횟수로 세지 않는다.
- **빌드 전 이력 보존:** 이전 최신 요약은 “빌드 시도는 아직0이고 문서 검사 후 Main이 수행할 예정이다”, “문서/checker0 뒤 Main의 빌드 시도 예정이며 지금은 시도0·완료/설치/실기 결과 없음”이었다. 이번 클릭 인계로 시도 상태만 갱신하며 native self-validation PASS·실기 미검증 경계는 유지한다. 전역 완료·실기 PASS·메모리/Stats/규칙/AssetMatrix 변경은 하지 않는다.

### 최종 구현 인벤토리 — 18파일

Main 직접 검사출력의 실제 최종 manifest다. 각 경로는 아래 공통 절대 prefix를 붙여 해석한다: `D:/Projects/Hexiege/Hexiege/Assets/_Project/`. 10기존+4신규+4신규 meta 총18파일이며 문서3파일과 별도다. 문서 담당의 독립 hash 재측정이 아니라 사용자 제공 Main path/hash 원문 인계다.

| 실제 파일 경로(prefix 아래) | 최종 SHA256 |
|---|---|
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | `8A1540FDF65AFE55D67A4242D90954163129CA4ABFBEF53B5EFB9A784DCFFB5D` |
| `Scripts/Infrastructure/Network/NetworkCombatController.cs` | `6058D4CE9105A3AFC5D59C435EB900C2F5D2F9DB481DE761FEFADC7D325A5DA6` |
| `Scripts/Infrastructure/Network/NetworkUnit.cs` | `DCB3501E08DDE38049C9E17F69BFA752ED02D86A1430D45E2D87A81050D87417` |
| `Scripts/Bootstrap/GameBootstrapper.Map.cs` | `BB9D6162524FFE75BB7D1D41CBF8058CF7FF4279854E584F141D92E9A8250880` |
| `Scripts/Presentation/Effects/EffectManager.cs` | `061BF6054F73E8E757709785A26EB10D4A2B0AB31467D731F6C0B5F6ED0D9130` |
| `Scripts/Presentation/Effects/VfxPoolItem.cs` | `8C1F6CE2FFE3352F48C910EDBB743816BE8345FEF057C7B2583376C91948AE98` |
| `Scripts/Presentation/Effects/UnitEffectConfig.cs` | `6A62B3452D3A08C21689844691C45A6F0255793A9B8F398C241A4C5A06D8F4FC` |
| `Resources/Config/UnitEffectConfig.asset` | `3F68AA8D31059E4C635A9C5937D6EE8B432124FA5BF57756E32DC373D81EF58E` |
| `Scripts/Application/Combat/UnitHealCastDiagnostics.cs` | `04A574E309DF14015EFB7F208DACB283AE636B0D943C749A70B6021A3F6F5DD5` |
| `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `8D020E86403F2DB7D6E4758CBD92149EBC98E0D82036668B270F7E638CFF3BD4` |
| `Scripts/Application/Combat/BloomHealEffectPresentation.cs` | `99888EA2C953590DF333C2DA304564BA2EFBED0B7D8DD91136B6ADD8166AF213` |
| `Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs` | `28291C9C805829796CB820943683A365CA2BC0CCC8BD8C5740C3B0931A042F3A` |
| `Scripts/Presentation/Effects/BloomHealEffectPresenter.cs` | `A2CC66E62310DAC8C5DB7BB6DB3BECF3EC0CA494CCADCB5254ACCF2C684CA4D8` |
| `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset` | `3C91D5A2BE027ADDEFA242A025CB0A4326196ED4299374FA4CC424E5D7FDFBF4` |
| `Scripts/Application/Combat/BloomHealEffectPresentation.cs.meta` | `4ED8AE47A4F9E574B478132FD827885EA04786CF7AB1908EBB5B1EC4A5C3B4CC` |
| `Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs.meta` | `F29D578A56407563CC7299EF9A827426EAD91C5E9B08DE69E0469EDA67EF76EA` |
| `Scripts/Presentation/Effects/BloomHealEffectPresenter.cs.meta` | `FFEB3C2427DF1C94F5F840CDC14474880C17D2ECDB58B7BB925B55931F16DE3C` |
| `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset.meta` | `7987CF6C5E1769AB88B284B34536982754E0DA31CC73518DD42E19ED6C042F65` |

### 정적 compiler 및 실제 artifact 경로

Main 직접 재실행 compiler: `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe`.

각 `v=Runtime,Editor,Dev,Release`에 대해 인계된 인수는 `@Temp/Bloom.{v}.rsp -out:Temp/Bloom.{v}.dll -refout:Temp/Bloom.{v}.ref.dll /nowarn:0618,0414`이며 Main은 각각 exit0을 확인했다. 경로는 repo root 기준이다. rsp/ref 및 define-switch 한계는 위 요약대로 유지하며 실행 exit0은 player 빌드나 실기 수용 증거가 아니다. Release 기존 warnings3를 보존한다.

## 이전 진행 이력 — 원문 보존

이하 시각별 “최신/현재/우선” 및 미확정 가설·pending 표기는 당시 기록이다. 현재 판정은 위 최종 요약을 읽으며, 이전 FAIL을 삭제하거나 소급 PASS로 바꾸지 않는다.

## 최신 진행 요약 — 19:33 fixture-only 수정 / 새 native 검증 대기 (2026-10-09)

**Main Refresh 후 native compile 진행 중. fixture-only 수정과 정적4 exit0은 보고됐지만, 수정 후 Action/Root·빌드 최종 증거는 아직 없다. native 전체 PASS·실기 수용·Task 완료가 아니며 최종 인계 대기다.** 아래 시각별 기록의 “최신/현재/우선”은 당시 기준으로 보존하고, 현재 상태는 이 요약이 우선한다.

- **19:33 측정:** 5PS Before는 paused=false / stopped=false / playing=true / count1, AfterSimulate는 paused=true / stopped=false / playing=false / count0 / IsAlive=true였다. 강제 Simulate 뒤의 paused 상태 관찰이며 production 원인이나 자연 경과에서의 particle drain 결함을 확정하는 증거가 아니다.
- **fixture-only 수정 보고:** Simulate 후 먼저 `count0 && !isEmitting`을 검사한 다음 `StopEmitting`을 NoClear로 다시 적용하여 native stopped state를 복구했다. 기존 predicate와 production `VfxPoolItem` hash는 이번 fixture 수정에서 불변이다. production predicate 완화·Clear 기반 성공 처리·게임 동작 수정으로 기록하지 않는다.
- **정적 및 hash 인계:** Runtime/Editor × Dev/Release 정적4 exit0, source18 Main hash match, 보호6 hash unchanged 보고. assertion 파일 `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` 최종 SHA256은 `8D020E86403F2DB7D6E4758CBD92149EBC98E0D82036668B270F7E638CFF3BD4`다. 문서 담당의 독립 컴파일/hash 측정이 아니다.
- **static artifact 한계:** 이번 인계에는 정적4 exit0 결과와 위 assertion SHA가 있으나 상세 compiler 명령·전체 출력·artifact 경로 및 나머지17개 개별 path/hash 쌍은 아직 제공되지 않았다. 순수 identity CLR PASS는 이전 인계 증거로 유지하되 이번 fixture 수정의 native 재검증을 대신하지 않는다. DLL 로드·Action/Root PASS·particle 화면·게임 runtime·빌드 성공을 정적 결과로 추론하지 않는다.
- **실패 이력:** 19:06 최초 FAIL, 19:16 이전 assembly 재실행 FAIL, 19:24 최신 loaded source의 시작 조건 통과/drain·회수 fixture1357 FAIL, 19:30 반환0·Ended=false·Pending1·SnapshotEq=true는 그대로 보존한다. 19:33 fixture 수정 후 새 native 결과와 별도다. 빌드 시도0·Root 미실행·실기 미검증을 유지한다.

### source18 인벤토리 인계 범위

Main의 실제 source18 hash 일치 보고를 기록한다. 아래는 기존 계획의 10기존+4신규+4신규 meta를 경로별로 펼친 **18파일 대조 목록**이다(prefix `Assets/_Project/`). Main이 전달한 총수와 일치하지만, 전체 실제 변경 명세·18개 개별 path/hash 원문 인계 전에는 후보 목록을 독립 확인된 최종 manifest로 승격하지 않는다. 이번 fixture-only 수정 파일은 assertion 파일1개이며 production VfxPoolItem은 이번 수정에서 불변이다.

1. `Scripts/Application/UseCases/UnitCombatUseCase.cs`
2. `Scripts/Infrastructure/Network/NetworkCombatController.cs`
3. `Scripts/Infrastructure/Network/NetworkUnit.cs`
4. `Scripts/Bootstrap/GameBootstrapper.Map.cs`
5. `Scripts/Presentation/Effects/EffectManager.cs`
6. `Scripts/Presentation/Effects/VfxPoolItem.cs`
7. `Scripts/Presentation/Effects/UnitEffectConfig.cs`
8. `Resources/Config/UnitEffectConfig.asset`
9. `Scripts/Application/Combat/UnitHealCastDiagnostics.cs`
10. `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
11. `Scripts/Application/Combat/BloomHealEffectPresentation.cs`
12. `Scripts/Application/Combat/BloomHealEffectPresentation.cs.meta`
13. `Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs`
14. `Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs.meta`
15. `Scripts/Presentation/Effects/BloomHealEffectPresenter.cs`
16. `Scripts/Presentation/Effects/BloomHealEffectPresenter.cs.meta`
17. `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset`
18. `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset.meta`

## 19:24 최신 loaded source 검증 · 시작 조건 통과 / drain·회수 FAIL (2026-10-09)

**최신 상태: Refresh 후 새 source가 로드된 실제 Action 검증 FAIL, programmer 구현 수정 중, 최종 인계 대기.** 19:16의 이전 assembly 재실행과 구분한다. 아래 compile/새 검증 pending 문구는 당시 이력이며 현재는 이 절이 우선한다.

- **Main 진행 증거:** Refresh 후 native Editor DLL 시각19:23:41, 실제 Action 실행19:24:50에서 새 source 로드 확인. 문서 담당의 독립 재측정이 아니라 Main 인계 증거다.
- **조건별 결과:** `All5PS lease starts`는 통과했다. 이어 `All5PS drain/return once retains terminal key` fixture1357에서 새 FAIL이 발생했다. 시작 조건의 통과는 이 조건 한정이며 전체 Action/native PASS가 아니다.
- **실패의 세대 구분:** 이번은 최신 loaded source의 실제 실패다. 19:16 stack1310/disk1333·이전 Editor DLL18:56:38에 근거한 이전 assembly FAIL과 혼동하거나 이번 FAIL까지 stale assembly로 제외하지 않는다. 19:06·19:16 실패 기록도 그대로 보존한다.
- Main이 programmer에게 재위임했고 구현 수정 중이다. drain/회수·terminal key 조건 실패의 상세 원인이나 교정 완료는 아직 인계되지 않았다. 시작 조건 통과만으로 이전 `Awake`/`_ps` 초기화 가설의 원인을 확정하지 않는다.
- native 전체 PASS 없음, Root 미실행, 빌드 시도0, 실기 미검증, Task 미완료다. 최종 소스 hash·compile artifacts·수정 후 최신 loaded source 재검증 결과의 추가 인계를 기다린다. 정적/CLR PASS와 게임 particle·runtime 수용은 계속 별개다.

### 19:30 latest probe 추가 — terminal 키 일치 / 반환 집계 pending

Main의 19:30 추가 인계상 **반환0, Ended=false, Pending=1, SnapshotEq=true**다. terminal 키는 일치하지만 반환·종료 집계는 아직 pending이라는 근거이며, 전체 drain/return 검증 통과나 효과 종료 완료로 승격하지 않는다.

- 5PS stop 후 `emit=false, count=1`; 강제 `Simulate(30)` 후 `count=0`이지만 `IsAlive=true`였다. 이 count를 PS 개수로 해석하지 않으며, particle count0을 drain 완료·회수 성공과 동일시하지 않는다.
- 이 관찰은 probe/fixture 경계의 증거다. 강제 Simulate 영향과 production 동작을 구별하는 중이며 **production 원인 확정은 아니다**. terminal 키 오류가 확인된 것으로도 쓰지 않는다.
- Main이 programmer에게 재위임했다. 원인·수정 결과·최종 검증 증거는 추가 인계 대기이며, native 전체 PASS 없음·Root 미실행·빌드 시도0·실기 미검증·Task 미완료를 유지한다. 기존 실패 이력과 미확정 초기화 가설의 경계도 보존한다.

## 19:16 두 번째 Action FAIL · 이전 assembly 재실행 / 새 native 검증 대기 (2026-10-09)

**[19:24 후속 정정 — 원문 보존]** 아래 “최신 상태”와 pending/우선 표기는 19:16 기준이다. 현재는 위 19:24 최신 loaded source의 drain·회수 FAIL / 구현 수정 중 기록이 우선한다.

**최신 상태: Main Assets Refresh 실행 후 native compile 진행 중, 새 assembly의 Action 검증 결과 pending. native PASS·빌드 시도·Task 완료 증거는 아직 없다.** 아래 19:06 실패와 수정 중 기록은 보존하며, 진행 상태는 이 절이 우선한다.

- Main 인계상 19:16 두 번째 Action 실행도 FAIL했으나 **이전 assembly 재실행**이었다. 근거는 실행 stack assertion 위치1310과 disk의 현재 assertion 위치1333 차이, Editor DLL 시각18:56:38이다. 문서 담당의 독립 UI/DLL 재측정이 아니라 Main의 확인 인계를 기록한다.
- 두 번째 FAIL을 숨기거나 PASS로 바꾸지 않는다. 다만 수정된 disk 소스가 새 assembly로 로드된 뒤 재검증에 실패했다는 증거로 사용하지 않는다. 19:06 최초 FAIL, 19:16 이전 assembly FAIL, Refresh 후 새 compile 진행을 서로 다른 단계로 구분한다.
- Main이 Assets Refresh를 실행했고 native compile이 진행 중이다. Refresh 실행·컴파일 진행은 컴파일 완료/로드 완료/새 gate PASS가 아니다. 최종 소스 hash·상세 compile artifacts와 새 assembly 검증 증거의 추가 인계를 기다린다.
- `Awake` 미호출 / `_ps` counter0 초기화 가설의 확정 여부는 여전히 별개다. 이전 assembly 재실행 확인만으로 최초 실패 원인이나 실제 게임 particle 재생 결함을 확정하지 않는다.
- native PASS 없음, 빌드 시도0, Root 미실행, 실기 미검증을 유지한다. 이번은 기존 Research/Plan/VFXSFXList 3문서 진행·실패 기록만이며 코드·씬·UI·build·Git·메모리·전역상태 변경은 없다.

## 구현 중간 인계 · 실제 Unity FAIL / 수정 중 (2026-10-09 19:06)

**[19:16 후속 정정 — 원문 보존]** 아래 “현재 상태”와 “이 절이 우선”은 19:06 기준이다. 최신 진행 상태는 위 19:16 절의 Refresh 후 native compile/새 검증 pending이 우선한다.

**현재 상태: 연동코드·설정 구현 보고 수신, 실제 Unity native 검증 FAIL, programmer 수정 중. Task 완료·native PASS·실기 수용은 아니다.** 아래 승인 대기·구현 보고 대기·게임 미연결 문구는 해당 단계의 이력이며, 현재 상태는 이 절이 우선한다.

- **Programmer 보고:** 소스 구현, Runtime/Editor × Dev/Release 정적 컴파일 4종 errors0, 순수 identity CLR 회귀 PASS, 보호 hash 6개 일치. 이는 programmer 인계 결과이며 문서 담당의 독립 재실행 결과가 아니다. 정적 컴파일·순수 CLR PASS는 실제 ParticleSystem 재생이나 게임 runtime PASS를 뜻하지 않는다. 최종 소스 hash와 자세한 compile artifacts는 추가 인계 대기다.
- **현재 설정 연결 확인:** `UnitEffectConfig.asset`의 `healPreset` GUID `e248f616d9334275a75e6a08405219ac` → `EffectPreset_BloomFairy_Heal.asset`의 `_vfxPrefab` GUID `ff068a5e9b80f3043a0517ebb1cfb00f`. 이 연결을 근거로 목록의 최신 상태를 “연동코드·설정 구현됨, 자동 검증 보완 중, 실기 미검증”으로 갱신한다. 설정 연결 자체는 화면 재생·수명·네트워크 수용 증거가 아니다.
- **Main 실제 Unity 관찰:** 19:06:03/04 `Hexiege → Combat → RunUnitActionSelfValidation` 실행에서 새 `ValidateBloomHoTEffectBoundary`의 `All5PS lease starts` 조건이 FAIL했다(인계 위치 1310). `PSPlayRecursive PS5/lease1` 로그는 있었으나 require 조건은 실패했다. 이 로그만으로 5PS/lease native 검증 PASS를 주장하지 않는다.
- **원인 미확정:** Main의 `Awake` 미호출 / `_ps` counter0 초기화 가설은 아직 가설이다. 확정 원인이나 교정 완료로 쓰지 않으며, 이 추정만으로 mistakes 새 사건을 기록하지 않는다. Main이 programmer에게 수정 재위임했고 수정 결과·새 native 재검증 인계를 기다린다. 최초 FAIL은 이후 결과와 별도로 보존한다.
- **남은 경계:** 빌드 시도0, Root 검증 아직 미실행, native PASS 없음, 실기 미검증. 빌드 시도는 Main만, 빌드 결과·기기 확인은 사용자 담당이다. 승인과 구현 보고를 검증/빌드 완료로 승격하지 않는다.
- **이번 문서 변경 범위:** 기존 Research.md·Plan.md·VFXSFXList.md 3파일이다. 이전 기술 보완/승인 상태 기록2파일과 초기 방법B 실제 변경3파일은 각 단계의 범위로 보존한다. 이번 문서 담당은 source/씬/UI/build/Git/TC/QA 작업을 하지 않고 전역상태·ROADMAP·MEMORY·AssetMatrix·Stats·규칙·mistakes·baseline을 변경하지 않는다.

## 현재 승인 및 진행 상태 (2026-10-09 추가)

**[19:06 후속 정정 — 원문 보존]** 이 절은 구현 중간 보고 수신 전 단계의 기록이다. 이 절의 “우선” 및 보고 전 목록 유지 지시는 당시 기준이며, 현재는 위 “구현 중간 인계 · 실제 Unity FAIL / 수정 중” 절이 우선한다.

사용자가 Main이 검토·공유한 Plan 9절에 **“좋아 작업 진행하고 빌드시도까지 해줘”**라고 명시 승인했다. Main 인계상 기존 programmer가 승인된 계획의 실제 구현을 진행 중이다. **현재 상태는 승인 후 구현 중이며, 구현 보고·검증 인계 대기**다.

- 위 목적 설명의 “구현 승인을 기다린다”와 아래 구현 승인 대기/승인 전 구현 금지 문구는 승인 전 단계의 이력으로 보존한다. 현재 구현 승인 상태는 이 절이 우선한다. 계획의 치료량·타이밍·서버 권위 및 보호 범위는 그대로 유지한다.
- 빌드 시도는 **Main만 수행**, 빌드 결과·기기 확인은 **사용자 담당**이다. 승인 사실을 빌드 실행·성공·설치·실기 관찰 증거로 바꾸지 않는다. 현재 이 문서에 인계된 빌드 시도/완료 결과는 없다.
- 실제 구현 파일·구현 결과·컴파일/기존 gate 증거는 추가 인계를 받은 뒤 기록한다. 인계 전 연동 완료·검증 PASS·실기 수용·Task 완료를 표기하지 않는다.
- VFXSFXList의 “제작됨·게임 미연결”은 실제 구현 보고 전에는 변경하지 않는다. 초기 방법B 전체 변경3파일과 이번 승인/구현 중 상태 기록2파일을 구분한다.
- 이번 문서 담당 수정은 기존 Research.md/Plan.md만이다. 문서 담당의 source/에셋/씬/Git/UI/build/TC/QA 작업은 없으며 새 문서·전역상태·메모리·Stats·규칙·baseline도 변경하지 않는다. 이는 programmer의 별도 구현 진행을 부정하는 source0 주장과 구분한다.



## 7. Programmer 기술 인계와 후속 경계 확정 권고 (2026-10-09)

**현재 상태: Main 기술계획 검토 수용, 사용자 source 구현 승인 대기.** 아래는 기존 naming 범위 밖/인계 대기 기록을 현재 계획 상태에 한해 대체한다. 과거 조사·측정은 보존한다. 실제 연동 구현·컴파일·실기·수용 PASS가 아니다. 초기 방법B 목록 정리와 범위 확장에서 Research.md·Plan.md·`Assets/_Project/Docs/Assets/VFXSFXList.md` 총3파일을 실제 변경했다. 그 이후 현재 기술 보완·최종 문구 보정 단계는 이 Research와 Plan 두 문서만 변경하며, 앞선 목록 변경을 취소하거나 없었던 것으로 보지 않는다. VFXSFXList의 제작됨·게임 미연결 문구는 유지한다.

### 7-1. 현상과 후보를 구분한 기술 인계

- Programmer 조사상 production에는 대상 HoT의 수락/갱신/종료를 표현에 전달하는 lifecycle 계약이 없다. 기존 `AddOrRefreshTimedEffect` / `TickTimedEffects`의 실제 writer 결과를 관찰하는 연결이 필요하다. `NetworkHealthSync`의 HP/ShowText 또는 caster `HealCastState`로 recipient HoT 수명을 추론하지 않는다.
- 추천 후보는 실제 서버/싱글 HoT 수락→Bloom 전용 표현 상태→`NetworkCombatController`→recipient `NetworkUnit`의 독립 atomic NetworkVariable current state→`BloomHealEffectPresenter` 단일 emitter→`EffectManager/VfxPoolItem`이다. 기존 힐 시전 sequence와 대상 효과 instance는 별개다.
- clip4초/OnAttackHit1초, 발동 후 cooldown3초, 기존 SeparateHeal/N/A 분류를 보존한다. 새 시전 sequence의 애니메이션 재생·힐러 승인 정책은 독립·불변이다. **재시작 금지는 동일 HoT refresh의 해당 VFX owner에만 적용**한다.
- authored prefab은 루트1+자식4=5PS, loop/Local/stopAction None이며 미연결이다. disk `useUnscaledTime: 0`도 5개 전부 확인했다. 기존 `ParticleSystemCount`의 root1 값은 recursive5PS 재생 증거가 아니다.

### 7-2. 후속 세 항목 — 추가 게임플레이 결정 없이 적용 가능한 권고

Programmer 후속은 pause/disconnect/target remove에 현재 규칙·소스 충돌이 없다고 보고했다. 아래는 게임플레이 정책 변경이 아니라 표현 경계의 구체 권고이며 **구현 완료 증거는 아니다**.

| 항목 | 계획에 반영할 권고 | 읽기 전용 근거 |
|---|---|---|
| Pause / clock | scaled HoT가 pause된 동안 active 유지. NGO/로컬 표현 timer로 expire하지 않는다. pause를 이유로 기존 FullHP/death 종료 처리를 차단하지 않는다. | `UnitCombatUseCase.cs:2449`의 Heal clock 유효성/진행, 동일 파일2415의 `Time.timeAsDouble` 표본 |
| Actual disconnect / map end | 로컬 visual/pending/subscription만 정리. 서버 HP/HoT 종료 역발행 금지. 단순 packet delay와 실제 연결/맵 종료를 구별한다. | `NetworkCombatController.cs:2534` 경기 종료의 combat stop; `NetworkUnit.cs:940` despawn callback/subscription 정리 |
| Target removal / tail | manager container가 마지막 유효 presentation world position/rotation/scale을 동결한다. 대상 재조회·재사용 ID 추종 금지. 5PS 방출 정지→남은 입자 자연소멸→회수1회. 최초 valid pose가 없으면 원점 spawn 없이 `NotStarted/TargetPoseUnavailable`. | `EffectManager.cs:487` manager container 인스턴스 생성; `VfxPoolItem.cs:298` AnyAlive/반환 경로 |

위 소스 경로의 prefix는 `Assets/_Project/Scripts/`이며 source 행 번호는 2026-10-09 조사 기준이다. 기존 container/AnyAlive 코드는 재사용 후보 근거이지 Bloom 전용 정지 lease 구현의 존재 증거가 아니다.

- Renderer 비활성/화면 밖/particle0은 서버 효과 종료 증거가 아니다. 대상 death/disable/despawn의 **local attachment 종료**와 **server effect 종료**는 사유/로그를 분리한다.
- container가 유지되는 disconnect는 StopEmitting/drain 경로를 사용한다. scene/container teardown으로 기다릴 수 없는 경우만 Clear와 `TeardownClear`를 구별해 기록하며 성공적인 자연소멸 종료로 표기하지 않는다.
- 5PS가 scaled time이므로 pause 중 drain도 멈추는 것은 정상이다. 임의 timeout clear, unscaled 설정 변경, tail2/3초 고정은 하지 않는다. AnyAlive는 **stop된 lease에서만** 회수 판단에 사용하며 active 중 particle gap에서 회수하지 않는다.
- writer scaled clock과 NGO clock 간 혼합 비교는 금지한다. 권위 Active/Stop로 수명을 관리하고 남은 시간·publish 시각은 clock domain을 명시한 metadata로만 취급한다. 이 방식에는 임의 clock bridge가 필요하지 않으며 교차 clock 수명 계산을 새 정책으로 넣지 않는다.

### 7-3. 근거와 증거 한계

원문 대조: Units 규칙34의 Application 서버/싱글 단일 writer·동종1개·refresh reset·FullHP/death/expiry 종료; combat synchronization NET-AUTH-001/002의 서버 결과 권위, NET-ROOT-001~003의 visual/simulation 분리, NET-TIME-001/003~005의 권위 시각·역순·late join 경계, NET-DELIVERY-TIMED의 별도 효과 instance; Sound 규칙15의 SFX 부재 주석 허용; LogRules 1.4/1.7/1.9/1.14의 keyed 형식·Dev stripping·예외 분류·전이 로그/비중복 원칙이다. 구체 연결/후보 파일/검증 예산은 Plan 9절에 기록한다.

Pause/disconnect/tail 처리 권고는 후속으로 구체화됐으므로 이전 미확정 표기를 현 단계의 정책 미결로 재사용하지 않는다. 남은 gate는 Main의 구체 Plan 검토와 사용자 source 승인, 이후 승인 범위에서 실제 구현/검증이다. 소스0변경·빌드0·실기0, 새 TC/QA/validator 없음.


## 현재 확장 범위 · 승인 이력 (2026-10-09)

사용자 “그 문서에 vfx연동 작업을 반영하여 작업을 진행하면 좋겠어”에 따라 **기존 naming Task에 BloomFairy 대상 부착 HoT VFX 연동을 포함**한다. 아래 “연결은 이번 범위 밖”/“문서1개뿐”은 당시 단계의 이력으로 보존하며 현재 범위는 이 절이 우선한다. 방법B B-1~B-5 목록 정리는 이번에 실제 반영한다. **아직 VFX 연결 구현/검증·실기 수용 PASS는 아니다.**

지금 변경 허용 파일은 이 Task Research.md/Plan.md와 `Assets/_Project/Docs/Assets/VFXSFXList.md` 정확3개다. programmer는 read-only 구체 연결 조사 중이며, 기술 인계→구체 Plan 보강→Main plan review/사용자 source 승인 전에는 source·에셋·씬을 변경하지 않는다. 새 Task/TC/QA, Git/UI/build, 규칙/Stats/상태/메모리/baseline 변경 없음.

### 과거 클립 조사 정정 — 기존 기록 유지

**[2026-10-09 정정 — 원문 보존]** 아래 과거 “클립 이벤트0건/OnAttackHit이 불리지 않는다” 조사와 현재 disk/source는 다르다. 현재 `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` tail은 `m_Events` 1개, `time: 1`, `functionName: OnAttackHit`이다. `UnitFactory.GetHitFrameTimes`는 이벤트 함수/시간을 읽고, `UnitView.OnAttackHit`는 `IsHealer`→`_healPresentation.ConsumeMarker()`의 별도 힐 marker 경로를 가진다. 이벤트 존재가 곧 대상 부착 loop VFX 연결이나 실제 재생/수용 증거는 아니다. 이번에 이벤트를 추가·교정한 것이 아니며 과거 미주입 상태를 현재 실패로 재사용하지 않는다.

Main 현disk 확인 SHA256: Attack clip `EBD6EC7A908BE84181482B5BDCF5FFAF9CA3CFECC791EBAA28E07666290A7DF4`, authored heal prefab `982033E7B2E82627F2BBDA6B38AFF91249AAE35C1A91758FC56B67E5EF8F6B66`. Game/Login/Stats baseline unchanged도 Main 인계 증거다. 이 단계의 새 에셋 저장/변경 주장으로 바꾸지 않는다.

### 승인된 연동 원칙 — 구체 코드 선택은 인계 후 확정

- 서버 HoT의 **실제 시작·갱신·종료 lifecycle**을 표현의 근거로 삼는다. 클립 marker마다 대상 loop를 만들거나 독립3초 타이머로 서버 상태를 대신하지 않는다. 대상별 동종1개, 갱신=reset, 만료/풀피/사망 종료 계약 유지(`GameSystemRules_Units.md` 규칙34).
- 치료받는 대상 추종 loop를 사용하고, **HoT refresh에서는 해당 힐 VFX owner와 입자 재생의 연속성을 유지하며 이 VFX를 재시작하지 않는다.** 이는 기존 시전의 새로운 sequence에 따른 애니메이션 재생이나 힐러 승인 정책을 제한하지 않는다 — 두 정책은 독립·불변이다. 정상 완료는 StopEmitting→남은 입자 drain→pool return; 대상 death/disable/despawn/gameend 및 pooled target 재사용에서도 오래된 owner/callback이 새 대상을 건드리지 않아야 한다.
- exact single-owner 및 멀티 late initialization·중복·역순 안전, session/lifetime/key/revision 경계와 종료 뒤 재활성화 방지의 구체 방식은 programmer 조사 보고로 확정한다. 기존 VFX pool의 루트+자식5시스템 재생/정지·drain/회수 경로도 검토한다(`GameSystemRules_Units.md` 규칙31). attackPreset에 loop를 연결하지 않는다.
- HP single writer, 실제 HoT timer/amount/duration·갱신 정책, 회전/Attack clip·marker/cooldown/Stats/기존 데이터 정책은 불변이다. 힐러 예외·힐 텍스트 계약도 유지(`GameSystemRules_Units.md` 규칙32·34·36·37). 다른 유닛/자연회복/다른 Heal 효과를 Bloom VFX로 오인하지 않는다.
- SFX는 현재 미제작. `GameSystemRules_Sound.md` 규칙15의 “한쪽만 재생 시 나머지를 주석으로 명시” 허용에 따라 SFX 없음 주석을 기술 계획에 포함하고 보류한다. 1회/루프 선택은 이 단계 선행조건이 아니며 임의 결정/제작하지 않는다.
- 판독 가능한 bounded proof logs를 기존 GameLog Dev 경로로 계획한다: exact owner/lifetime/key, start/refresh/stop 사유, duplicate/stale/late-init 판단, drain/pool return, overflow/drop/unknown을 구별하고 총량·문자열 byte budget·Release 제거/비간섭을 구체 Plan에서 정한다. per-frame 문자열 로그나 조용한 누락을 정상0건으로 해석하지 않는다. 로그 hook 성공과 실제 입자/pixel·멀티 수용은 별개다.

### 이번 실제 문서 반영 및 다음 gate

VFX/SFX 공격 표에서 Bloom 행 각1개를 특수 효과 절로 이동해 각각24행으로 정리하고, 사망 표는 그대로 유지한다. heal 명칭과 VFX “프리팹 제작됨·게임 미연결”, SFX “미제작·1회/루프 미정(현재 결정 불필요)”를 기록한다. 기존 절/결정 순서와 조사 이력은 삭제하지 않는다. 후속은 **programmer 기술 인계 대기**이며, source 후보/연결점·pool/네트워크 소유권·검증 예산은 아직 미확정이다.

---

## 이 작업은 무엇이고 왜 하는가

꽃요정(BloomFairy)은 적을 때리지 않고 아군을 치료하는 유닛이다. 그런데 이펙트 목록 문서에는 꽃요정의 이펙트가
다른 유닛과 똑같이 「공격(attack)」이라는 이름으로 적혀 있다(`vfx_bloomfairy_attack.prefab`, `sfx_bloomfairy_attack.wav`).

사용자는 지금 Unity AI 로 꽃요정의 치료 이펙트를 직접 만들려고 한다. 이 이펙트는 공격처럼 「한 번 번쩍하고 끝나는」 것이
아니라, **치료를 받는 아군 몸에 붙어서 치료가 이어지는 동안 계속 반복되는** 상태 이펙트다. 만들기 전에 파일 이름부터
정해야 하므로, 사용자는 **이펙트 에셋(VFX·SFX)에서는 꽃요정의 「attack」을 전부 「heal」로 부르기로** 결정했다.

다만 **애니메이션은 이름을 바꾸지 않는다.** 게임 코드가 모든 유닛의 공격 동작을 「Attack」이라는 공통 이름으로 찾기 때문에,
꽃요정만 이름을 바꾸면 치료 동작의 타이밍을 못 찾게 된다. 이 문서는 이름이 어디에 적혀 있는지, 무엇은 바꿔도 되고 무엇은
바꾸면 안 되는지를 실제 파일을 열어 확인한 결과다. 이펙트를 유닛에 실제로 연결하는 일은 **이번 범위가 아니며**,
이펙트가 만들어진 뒤 별도 작업으로 한다.

> **[2026-10-09 정정]** 위 문단은 이펙트를 「만들려고 한다」고 적었지만, 그 뒤 사용자가 Unity AI 로 **VFX 프리팹을 이미 만들었다**
> (`Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab`, 처음부터 새 이름 `heal` 로 저장됨 — 6절). 소리(SFX)는 아직 없다.
> 이펙트는 **아직 어떤 유닛에도 연결되지 않았고**, 연결은 여전히 이번 범위 밖의 후속 작업이다. 행을 어디에 둘지는 사용자가
> **방법 B(「유닛 특수 효과」 칸을 새로 만들어 이동)** 로 정했다(Plan.md 3절). 원문은 이력으로 그대로 둔다.

---

## 1. 이름이 적힌 곳 — 전수 조사

`bloomfairy_attack` / `bloomfairy_heal` 을 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체에서 찾았다(2026-10-08 실측).

| 위치 | 행 | 현재 내용 | 비고 |
|---|---|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 46 | `` `vfx_bloomfairy_attack.prefab` `` · 비고 「꽃잎 힐」 | Ⅰ. VFX → 2. 제작 예정 VFX → **「유닛 공격 이펙트」** 표 |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 257 | `` `sfx_bloomfairy_attack.wav` `` · 비고 「힐 차임음」 | Ⅱ. SFX → 2. 제작 예정 SFX → **「유닛 공격 사운드」** 표 |

- **이 2건 외에는 0건**이다. 코드(`.cs`)·설정 에셋(`.asset`)·프리팹·씬 어디에도 이 파일 이름은 없다. `_Tasks/`·`_Logs/` 에도 0건이다.
- **실제 파일은 둘 다 존재하지 않는다.**
  - `Assets/_Project/Prefabs/VFX/Units/` 에 있는 프리팹 9개 중 꽃요정 것은 없다.
  - `Assets/_Project/Audio/SFX/Units/` 에는 `sfx_pistoleer_attack.wav` · `sfx_tank_attack.wav` · `sfx_unit_death.wav` 3개뿐이다.
  - → **이름을 바꿔도 끊어지는 참조(파일·GUID·코드 문자열)가 없다.** 바꾸는 것은 문서 2행뿐이다.
  > **[2026-10-09 정정 — 원문은 위에 그대로 둔다]** 「실제 파일은 둘 다 존재하지 않는다」는 **VFX 쪽은 더 이상 사실이 아니다.**
  > `Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab`(+ `.meta`)이 생겼다(실측, 상세는 6절). SFX(`sfx_bloomfairy_heal.wav`)는 여전히 없다
  > (`Audio/SFX/Units/` 는 위와 같은 `.wav` 3개 — 재실측).
  > **이름 변경이 안전하다는 결론은 그대로다. 다만 근거가 바뀌었다** — 「파일이 없어서」가 아니라 아래 두 가지 때문이다.
  > ① **옛 이름 `bloomfairy_attack` 을 참조하는 곳이 문서 2행 외에 없다**(`.cs`·`.asset`·`.prefab`·`.unity`·`.md` 재검색, `_Tasks/` 제외 — `VFXSFXList.md` 1개 파일뿐).
  > ② **새 프리팹은 처음부터 새 이름 `vfx_bloomfairy_heal` 로 저장됐다** — 바꿀 파일 이름도, 끊길 GUID 도 없다. 문서 쪽 이름이 실물에 맞춰지는 것이다.
- 같은 문서의 사망 이펙트 행(76행 `vfx_bloomfairy_death.prefab`, 287행 `sfx_bloomfairy_death.wav`)은 「attack」이 아니므로 대상이 아니다.

### `VFXSFXGuide.md` — 변경 불필요 (확인 완료)

| 행 | 내용 | 판정 |
|---|---|---|
| 302 | `\| BloomFairy \| Transcendence \| 힐 \| petal heal, bloom glow \|` (VFX 「유닛 공격 이펙트」 키워드 표) | 파일 이름이 없다. 키워드도 이미 「heal」이다 → 변경 불필요 |
| 493 | `\| BloomFairy \| Transcendence \| gentle heal chime, flower bloom \|` (SFX 「유닛 공격 사운드」 키워드 표) | 같은 이유로 변경 불필요 |

- 같은 문서의 「파일 명명 규칙」(69~110행)은 `vfx_[에셋명]_[이벤트].prefab` · `sfx_[에셋명]_[이벤트].wav` 이다.
  「이벤트」 자리에 `heal` 을 넣는 것은 이 규칙 안에 들어간다 — **명명 규칙을 고칠 필요는 없다.**

---

## 2. 바꾸면 안 되는 것 — 애니메이션과 코드의 「Attack」

모든 유닛이 공격 동작을 같은 이름으로 찾는다. 꽃요정의 치료 동작도 이 공통 경로를 탄다(실측).

| 무엇 | 위치 | 「Attack」을 어떻게 쓰는가 |
|---|---|---|
| 애니메이션 클립 | `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` (9행 `m_Name: BloomFairy_Attack`) | 아래 코드들이 클립 이름에 「Attack」이 **포함**됐는지로 찾는다 |
| Animator 상태 | `Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller` (10행 `m_Name: Attack`) — 이 상태의 모션(23행)이 위 `.anim` 의 GUID `b934af54…` 를 가리킨다 | 상태 이름 「Attack」의 해시로 재생한다 |
| 공격 클립 길이 조회 | `Scripts/Infrastructure/Factories/UnitFactory.cs:461` | `clip.name.Contains("Attack")` |
| 타격 프레임(`HitFrameTimes`) 추출 | `Scripts/Infrastructure/Factories/UnitFactory.cs:496` | `clip.name.Contains("Attack")` — 꽃요정의 **치료 발동 시점**이 여기서 정해진다(규칙 32) |
| 상태 재생 | `Scripts/Presentation/Unit/UnitView.cs:54` | `Animator.StringToHash("Attack")` |
| 멀티 애니 상태 적용 | `Scripts/Infrastructure/Network/NetworkUnit.cs:332` | `case UnitAnimState.Attack:` → `PlayAttackAnimation()` (enum 멤버 — 문자열은 아니지만 같은 상태로 이어진다) |
| 에디터 이벤트 주입기 | `Scripts/Editor/CombatHitEventInjector.cs:260` | `clip.name.Contains("Attack")` |
| 에디터 이벤트 검증기 | `Scripts/Editor/CombatHitEventValidator.cs:176` | `clip.name.Contains("Attack")` |

- 클립 이름을 `BloomFairy_Heal` 로 바꾸면 `UnitFactory.cs:496` 이 꽃요정 클립을 못 찾아 타격 프레임이 폴백값으로 떨어지고,
  `UnitView.cs:54` 의 상태 해시도 맞지 않아 치료 동작이 재생되지 않는다. **그래서 애니메이션 쪽은 「Attack」을 유지한다**(사용자 결정).
- 코드 식별자(`AttackCooldown`, `OnAttackHit`, `UnitAnimState.Attack`, `PlayUnitAttack`, `PlayUnitAttackSfx` 등)와,
  규칙 문서에서 이 식별자를 인용한 문장(예: `GameSystemRules_Units.md` 규칙 17·27·32·36)도 **그대로 둔다** — 코드와 문서가 같은 이름을 가리켜야 하기 때문이다.

---

## 3. 이 이펙트가 기존 유닛 이펙트와 다른 점

### 3-1. 치료는 「시간 지속 효과」다 — 그래서 반복 재생이 필요하다

| 사실 | 근거 |
|---|---|
| 꽃요정의 치료는 **아군 1명**에게 거는 지속 회복(HoT)이다 | `GameSystemRules_Units.md` 규칙 33 · `StatsReference.md` 92행 「아군 단일 지정 힐 200 HP/3초」 |
| 지속시간 기본 3초 — `SpecialAttackConfig._bloomHealDuration`(Inspector 조정 가능) | `Scripts/Infrastructure/Config/SpecialAttackConfig.cs:77` · `Scripts/Bootstrap/GameBootstrapper.Setup.cs:316` |
| **대상 사망 / 풀피 도달 / 지속 만료** 시 효과가 끝난다 → 3초를 다 채우지 않고 **일찍 끝날 수 있다** | `GameSystemRules_Units.md` 규칙 34 「종료 처리」 |
| 같은 대상에 다시 걸면 **남은 시간이 리셋**된다(중첩 없음) → 3초보다 **길게 이어질 수 있다** | `GameSystemRules_Units.md` 규칙 34 「갱신 = 리셋」 |
| 실제 치료 주기는 4.0초(발동 준비 1.0초 + 발동 후 쿨다운 3.0초) | `GameSystemRules_Units.md` 규칙 36 |

→ 효과의 길이가 **매번 다르다.** 정해진 길이로 한 번 재생하고 사라지는 이펙트로는 맞출 수 없으므로,
**반복(loop) 재생하다가 코드가 시작·정지시키는** 구조여야 한다.

### 3-2. 기존 유닛 VFX 프리팹 실측 (`Assets/_Project/Prefabs/VFX/Units/`, 9개)

| 프리팹 | 파티클 시스템 수 | looping | 재생 길이(초) | stopAction |
|---|---|---|---|---|
| `vfx_cannon_attack` | 4 | 전부 OFF | 0.35 | None 3 · Destroy 1 |
| `vfx_pistoleer_attack` | 4 | 전부 OFF | 0.35 | None 3 · Destroy 1 |
| `vfx_streamspirit_attack` | 4 | 전부 OFF | 0.35 / 0.5 | None 3 · Destroy 1 |
| `vfx_tank_attack` | 4 | 전부 OFF | 0.6 | None 3 · Destroy 1 |
| `vfx_quakespirit_attack` | 3 | 전부 OFF | 0.5 | None 3 |
| `vfx_torrentspirit_attack` | 5 | 전부 OFF | 0.5 | None 5 |
| `vfx_unit_death` | 3 | 전부 OFF | 0.8 | None 2 · Destroy 1 |
| `vfx_foxmagician_charge` | 2 | 전부 OFF | 2 | None 1 · Destroy 1 |
| `vfx_infernospirit_charge` | 1 | 전부 OFF | 1 | Destroy 1 |

> **[2026-10-09 추가]** 위 표는 2026-10-08 시점의 9개다. 지금 이 폴더에는 꽃요정 힐 프리팹이 더해져 **10개**다 — 힐 프리팹 실측은 6절에 따로 적는다(아래 비교 문장들은 기존 9개에 대한 것이다).

- 9개 전부 **looping OFF** 다. 공격 이펙트는 0.35~0.6초, 사망 0.8초, 충전(charge) 이펙트 1~2초.
- **시뮬레이션 공간은 9개 프리팹의 파티클 시스템 30개가 전부 Local**(`moveWithTransform: 0`)이다.
  > ⚠️ **인계 내용 정정**: 인계 메모에는 꽃요정 힐 이펙트가 「Local 시뮬레이션 공간이라는 점에서도 기존과 다르다」고 적혀 있었으나,
  > 실측하면 **기존 유닛 VFX 도 전부 Local** 이다. 기존과 실제로 다른 점은 **반복 재생(loop ON)** 과 **자동 소멸 없음(stopAction None)** 두 가지다.
- 여러 파티클 시스템을 형제로 둔 프리팹은 풀 아이템이 직속 자식을 각각 재생해야 한다 — `GameSystemRules_Units.md` 규칙 31. 4층 구성인 힐 이펙트도 이 구조에 해당한다.

### 3-3. 합의된 이펙트 설계 (참고용 — 생성 프롬프트 원문은 대화에 있다)

- 파일: `vfx_bloomfairy_heal.prefab` (저장 위치는 `VFXSFXGuide.md` 「폴더 구조」에 따라 `Assets/_Project/Prefabs/VFX/Units/`)
- 4층 구성: `Bloom_Ring`(바닥의 초록 꽃 원) · `Rising_Petals`(옅은 파스텔 분홍 꽃잎 상승) · `Sparkle_Motes`(초록 반짝임) · `Soft_Aura`(희미한 초록 오라)
- 은은한 톤, **Loop ON**, **Stop Action None**, 약 0.2초 페이드인, 끝날 때는 방출만 멈춰 남은 입자가 자연히 사라지게, Simulation Space Local
- **붙는 대상은 치료를 받는 아군**이다(치료하는 꽃요정이 아니다).
- **[2026-10-09 추가]** 이 설계대로 만든 실물이 생겼다 — 설계와의 대조는 6절.

---

## 4. 나중에 연결할 때 알아야 할 것 (이번 범위 밖 — 기록만)

이펙트를 유닛에 연결하는 일은 이펙트가 만들어진 뒤 별도 작업이다. 그때 필요한 사실을 실측해 둔다.
**[2026-10-09 정정]** VFX 프리팹은 이제 만들어졌다(6절). 연결은 여전히 별도 후속 작업이며, 아래 사실은 그대로 유효하다.

1. **`EffectManager` 에는 「대상에 붙여 반복 재생하고 나중에 끄는」 API 가 없다.**
   `Scripts/Presentation/Effects/EffectManager.cs` 의 공개 재생 메서드는 `PlayUnitAttack`(174행) · `PlayUnitDeath`(184행) · `PlayUnitHit`(197행) ·
   `PlayBuildingDestroy`(213행) · `PlayBuildingUpgrade`(224행) · `PlayBuildingAttack`(239행) · `PlayUi`(253행) · `PlayTracer`(275행)이고,
   모두 「위치에서 한 번 재생」 방식이다. 정지(Stop)·부착(대상 Transform 추종) 메서드는 없다. → **연결 작업에서 신설이 필요하다.**
2. **이 힐 이펙트를 `UnitEffectConfig` 의 꽃요정 `attackPreset` 칸에 넣으면 안 된다.**
   - `Assets/_Project/Resources/Config/UnitEffectConfig.asset` 136~139행: `unitType: 27`(BloomFairy, `Scripts/Domain/Unit/UnitType.cs:49`)의 `attackPreset` 은 비어 있다(`deathPreset` 만 연결됨).
   - `attackPreset` 은 `UnitView.OnAttackHit()`(`Scripts/Presentation/Unit/UnitView.cs:1865~1882`)이 `PlayUnitAttack` 으로 **치료하는 꽃요정 자신의 위치에서 한 번** 재생하는 칸이다.
     힐 이펙트(대상에 붙어 반복)와 재생 위치·방식이 모두 다르다.
   - `GameSystemRules_Units.md` 규칙 31 은 「유닛 공격 VFX는 … `attackPreset` 에 연결한다」고 적고 있다. 힐 이펙트 행이 「유닛 공격 이펙트」 표에 남아 있으면
     이 문장대로 `attackPreset` 에 연결될 여지가 있다 — Plan.md 의 미결 사항(표 위치)과 관련된다.
     **[2026-10-09 갱신]** 미결 사항은 **방법 B(「유닛 특수 효과」 절로 이동)** 로 결정됐다(Plan.md 3절). 2026-10-09 재실측: `UnitEffectConfig.asset` 136~140행 BloomFairy 항목은 그대로다(`attackPreset` 비어 있음, `deathPreset` 만 연결).
3. **`BloomFairy_Attack.anim` 에는 Animation Event 가 하나도 없다**(60755행 `m_Events: []`, `OnAttackHit` 0건 — 실측).
   - 따라서 현재 꽃요정에게는 `OnAttackHit()` 가 불리지 않고, 그 안의 `PlayUnitAttack` / `PlayUnitAttackSfx` 도 꽃요정 몫으로는 실행되지 않는다.
   - 치료 자체는 `HitFrameTimes` 타이머로 동작하므로 영향이 없다(규칙 32, `_Tasks/2026-07-18/03_40_bloomfairy-healer/Plan.md` 110행 「`OnAttackHit` 미주입이어도 회복은 정상 동작」).
   - ⚠️ `GameSystemRules_Units.md` 규칙 27 의 「BloomFairy 는 … `OnAttackHit`은 힐 연출 전용으로 처리 완료」라는 문장이 **이벤트가 주입됐다는 뜻인지, 「연출 전용으로 쓰기로 정했다」는 뜻인지**는
     문장만으로 확정할 수 없다. 실측은 「미주입」이다. 이번 범위에서는 규칙 문서를 고치지 않고 사실만 기록한다(연결 작업에서 확인할 것).
4. **SFX 짝 규칙**: VFX 와 SFX 는 같은 메서드에서 연달아 호출해야 한다 — `GameSystemRules_Sound.md` 규칙 15. 연결 작업에서
   힐 이펙트를 켜는 지점에 `sfx_bloomfairy_heal` 호출(또는 「SFX 없음」 주석)이 함께 와야 한다. **SFX 가 한 번 울리는 소리인지 반복음인지는 아직 정해지지 않았다.**

---

## 5. 작업 중 발견한 부가 이슈 (이번 범위 밖 — 보고만)

| # | 내용 | 실측 |
|---|---|---|
| 1 | `VFXSFXList.md` 「1. 완성된 VFX」(10~12행)가 「현재 없음」인데, `Prefabs/VFX/Units/` 에는 프리팹 9개가 있다 **[2026-10-09: 꽃요정 힐 프리팹이 더해져 10개]** | 위 3-2 표 · 6절 |
| 2 | `VFXSFXList.md` 「1. 완성된 SFX」(221~223행)가 「현재 없음」인데, `Audio/SFX/Units/` 에는 `.wav` 3개가 있다 | 1절 |
| 3 | `GameSystemRules_Units.md` 규칙 27 의 BloomFairy 「처리 완료」 서술과 클립 실측(이벤트 0건)의 관계가 불명확하다 | 4절 3번 |

→ 셋 다 이번 작업(이름 변경)과 무관하므로 고치지 않는다(CLAUDE.md 규칙 6). 필요하면 별도 작업으로 제안한다.

---

## 6. [2026-10-09 추가] 만들어진 힐 이펙트 프리팹 실측

사용자가 Unity AI 로 꽃요정 힐 이펙트를 만들어 올렸다. 이 절은 그 결과물을 직접 열어 확인한 내용이다.
요약하면 **합의한 설계(3-3절)대로 만들어졌고, 아직 게임 어디에도 연결되지 않았다.**

※ 근거 구분(CLAUDE.md 규칙 10): 아래 파일·설정값은 **2026-10-09 에 직접 실측**했다. 커밋 `e84e99f`(「힐 vfx 제작」, main)는
**호출 세션이 전달한 값**이다 — git 명령 금지(CLAUDE.md 규칙 5)로 이 문서에서 재검증하지 않았다.

### 6-1. 파일

| 종류 | 경로 |
|---|---|
| VFX 프리팹 | `Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab` (+ `.meta`) |
| 텍스처 | `Assets/_Project/Art/VFX/BloomFairy/vfx_bloom_ring.png` · `vfx_petal.png` (+ 각 `.meta`) |
| 머티리얼 | `Assets/_Project/Materials/VFX/Mat_VFX_BloomRing.mat` · `Mat_VFX_Petal.mat` (+ 각 `.meta`) — 두 머티리얼의 GUID 가 프리팹 안에서 각각 참조된다 |
| SFX | **없음** — `sfx_bloomfairy_heal.wav` 는 아직 만들어지지 않았다 |

### 6-2. 구조와 재생 설정 (프리팹 YAML 실측)

루트 `vfx_bloomfairy_heal` 아래 직속 자식 4개(`Bloom_Ring` · `Rising_Petals` · `Sparkle_Motes` · `Soft_Aura`)가 있다 — 3-3절 설계의 4층과 이름이 일치한다.
파티클 시스템은 **5개**다: 자식 4개 + **루트 자신에도 1개**가 붙어 있다.

| 오브젝트 | 재생 길이(초) | looping | stopAction | 시뮬레이션 공간 | maxNumParticles |
|---|---|---|---|---|---|
| `vfx_bloomfairy_heal` (루트) | 2 | ON | None | Local | 1 |
| `Bloom_Ring` | **3** | ON | None | Local | 2 |
| `Rising_Petals` | 2 | ON | None | Local | 6 |
| `Sparkle_Motes` | 2 | ON | None | Local | 8 |
| `Soft_Aura` | 2 | ON | None | Local | 2 |

- 5개 전부 **looping ON(`looping: 1`) · stopAction None(`stopAction: 0`) · Local(`moveWithTransform: 0`)** — 3-3절 설계(Loop ON · Stop Action None · Local)와 일치한다.
  3-2절 기존 9개(전부 looping OFF)와 다른 점이 설계 의도대로 들어갔다.
- 입자 상한이 1~8개로 작다 — 「은은한 톤」 설계와 맞는다(화면에 보이는 모습은 실기에서 확인할 일이며 이 실측은 설정값까지만이다).
- ⚠️ **연결 작업에서 확인할 사실(판단은 하지 않음):** `GameSystemRules_Units.md` 규칙 31 은 여러 파티클 시스템을 가진 프리팹을 풀 아이템이 **직속 자식을 각각** 재생한다고 정한다.
  이 프리팹은 **루트에도 파티클 시스템이 있다.** 루트의 것까지 재생·정지 대상에 들어가는지는 연결 작업에서 그 재생 경로를 읽어 확인해야 한다.

### 6-3. 아직 연결되지 않았다 (2026-10-09 실측)

- `bloomfairy_heal` 이라는 문자열은 `.cs`·`.asset`·`.unity`·`.prefab` 중 **프리팹 자기 자신에만** 있다(루트 이름). 코드·설정 에셋·씬 0건.
- 프리팹 `.meta` 의 GUID(`ff068a5e…`)를 `.asset`·`.unity`·`.prefab`·`.cs`·`.controller` 에서 찾으면 **프리팹 자신 외 0건**이다 — 어디에서도 끌어다 쓰지 않는다.
- `UnitEffectConfig.asset` 의 BloomFairy(`unitType: 27`) 항목은 4절 2번 그대로다(`attackPreset` 비어 있음).
- → 연결은 여전히 **이번 범위 밖 후속 작업**이다(Plan.md 5절). 이번 작업이 하는 일은 `VFXSFXList.md` 의 이름·위치를 이 실물에 맞추는 것뿐이다.
