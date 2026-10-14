# Plan — BloomFairy 힐 이펙트 이름 정리 (attack → heal)

> **[2026-10-13 추가 — 목록 문서 반영 완료 확인 · 아래 본문은 원문 유지]** 이 Plan의 목록 문서 변경(방법 B: B-1~B-5)은 `Assets/_Project/Docs/Assets/VFXSFXList.md`에 **반영 완료** 상태다. 2026-10-13 document-manager가 7절 정밀화 확인표를 다시 실행했다: 옛 이름 `bloomfairy_attack`(`_Tasks/`·`_Logs/` 제외) 0건 · `vfx_bloomfairy_heal.prefab` 1건(「유닛 특수 효과 이펙트」 표) · `sfx_bloomfairy_heal.wav` 1건(「유닛 특수 효과 사운드」 표) · 두 절 제목 각 1건, 각각 「유닛 사망 이펙트/사운드」 바로 다음.
> 이 Plan의 기대값과 다른 점(모두 다른 승인 작업이 만든 차이이며 결함이 아니다): ① 「유닛 공격 이펙트」 표는 **25행**이다 — MushroomBomber 분할 Plan(`_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md` 6절 ①)이 예고한 값. 「유닛 공격 사운드」는 24행 그대로. ② 「유닛 특수 효과 이펙트」 표에는 InfernoSpirit 화상·MushroomBomber 독 행이 꽃요정 행 **앞**에 있다(유닛 종류 순서). ③ 이 절 다음 제목은 「건물 파괴 이펙트」가 아니라 이후 작업이 더한 「Bloom HoT 최신 관측 근거」 등 이력 소절이다. ④ 머리 안내문 3번째 줄은 B-2 확정안과 글자가 다르다(⚠️·규칙 31 언급 없음) — 사용자가 고쳐 썼을 수 있어 MushroomBomber Plan 2-A 3번에 따라 맞추지 않았다. 연결(VFX 재생) 작업은 이 Plan 범위 밖이었고, 이후 2026-10-09 확장 범위에서 구현됐다(위 「최종 구현·검증 인계」).

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



## 9. HoT VFX 구체 기술 계획 — 구현 승인 대기 (2026-10-09)

**Programmer 읽기 전용 조사·후속 권고 반영본 — Main 기술계획 검토 수용, 사용자 source 구현 승인 대기.** 기존 절의 범위 밖/인계 대기 문구는 당시 이력으로 보존한다. 현재는 기존 Task에 연동 계획이 포함되지만 **사용자 source 승인 전 구현 금지**다. 초기 방법B 목록 정리와 범위 확장에서 Research.md·Plan.md·`Assets/_Project/Docs/Assets/VFXSFXList.md` 총3파일을 실제 변경했다. 그 이후 현재 기술 보완·최종 문구 보정 단계는 Research/Plan 두 문서만 변경하며 앞선 목록 변경도 전체 변경 범위에 포함한다. 소스·에셋·씬·규칙·Stats·전역상태·메모리·baseline 변경0, Git/UI/build/실기0, 신규 Task/TC/QA/validator 없음.

### 9-1. 권위 writer와 표현 수명 연결 후보

실제 서버/싱글 HoT 수락 → Bloom 전용 Application 표현 상태 → NetworkCombatController → recipient NetworkUnit 독립 atomic NetworkVariable current state → BloomHealEffectPresenter sole emitter → EffectManager/VfxPoolItem.

- `AddOrRefreshTimedEffect/TickTimedEffects`의 **실제 수락·갱신·제거 결과**를 관찰한다. HP/ShowText와 caster HealCastState는 recipient HoT 수명의 대용이 아니다. HP/HoT list/수학/amount/duration/reset·단일 writer와 틱 순서를 바꾸지 않는다.
- 효과 키는 `MatchGeneration+TargetLifetime+EffectInstanceId`; atomic 값에 `monotonicRevision+Active+BloomHealKind`를 포함한다. instance는 시전 sequence와 독립이다. 종료된 같은 instance를 재활성화하지 않고 종료 후 새 grant는 새 instance다. 낮은 revision·다른 lifetime·중복·역순·ID 재사용 입력을 구별해 차단한다.
- 동일 owner refresh는 no Play/Clear/restart로 연속성을 유지한다. 이는 **해당 recipient HoT VFX owner 한정**이며 새 시전 sequence 애니메이션·기존 힐러 승인 정책은 독립·불변이다.
- 다른 Heal이 generic 동일종류 슬롯을 overwrite하여 Bloom 효과가 끝나는 경우 Bloom visual만 종료한다. 기존 generic overwrite 수학은 변경하지 않는다. non-Bloom Heal/regen/Torrent/DoT가 Bloom VFX를 만들지 않도록 provenance를 분리한다.
- 실제 writer FullHP/Expiry/target death/unavailable 종료에서 Active=false를 게시한다. caster death가 이미 적용된 HoT를 취소하지 않는 기존 writer를 보존한다.
- Host도 동일 presenter만 emitter가 되게 하고 server hook/수신/ready 경로에서 중복 Play를 만들지 않는다. late join/view ready는 latest current를 복구하되 과거 begin burst/sound를 재생하지 않는다. stop-before-ready는 이후 spawn을 차단한다. 같은 프레임 begin/end NetworkVariable coalescing은 current convergence이며 시작 loss로 단정하지 않는다.
- Bootstrap의 PresentationPoseProvider를 재사용해 manager container에서 recipientVisualRoot 표현 pose를 따라간다. SimulationRoot writer 추가·Red 좌표 재변환·씬 저장/수정 금지. NetworkHealthSync/GameEvents/UnitView/EffectPreset는 그대로 유지 가능한 후보다.

### 9-2. Pause / disconnect / target remove — 후속으로 구체화한 표현 경계

추가 게임플레이 결정을 요구하지 않는 programmer 권고다. 아래를 구현 후보 계약으로 반영하되 구현/실기 수용으로 표시하지 않는다.

1. **Pause:** writer scaled `Time.timeAsDouble`가 정지한 동안 HoT active 유지. NGO clock/로컬 timer로 임의 expire 금지. pause 전용 early-return으로 기존 FullHP/death 종료를 가로막지 않는다. 권위 Active/Stop가 수명을 소유한다. NGO와 scaled writer clock을 혼합 비교하지 않고 publish 시각/남은 시간은 clock-domain 명시 metadata에 한정한다.
2. **Actual disconnect/map end:** local visual/pending/subscription만 멱등 정리하고 HP/HoTlist 수정 또는 서버 종료 역발행 금지. packet delay는 actual disconnect가 아니다. container가 남아 있으면 방출 정지 후 자연소멸을 기다린다.
3. **Target remove:** attachment를 닫고 마지막 유효 presentation world position/rotation/scale을 manager container에 동결한다. 이후 대상 재조회/재사용 target 추종 금지. 처음부터 valid pose가 없으면 원점 spawn하지 않고 `NotStarted/TargetPoseUnavailable`를 기록한다.
4. **입자 회수:** poolLeaseGeneration으로 늦은 callback을 격리한다. stop된 같은 lease의 5PS 모두 StopEmitting→drain→Return1회. AnyAlive는 stop된 lease에서만 자연소멸 회수를 판정하며 active 중 입자 gap으로 회수하지 않는다. tail2/3초 고정·임의 timeout clear 금지.
5. **Scaled drain / teardown:** prefab5PS `useUnscaledTime=0` 유지. pause 중 drain 정지는 정상이며 unscaled로 수정하지 않는다. scene/container teardown에서는 Clear→`TeardownClear`로 기록하고 자연소멸/성공 완료로 세지 않는다. teardown은 visual/subscription만 정리한다.
6. **증거 분리:** Renderer 비활성/화면 밖/particle0은 server stop 증거가 아니다. death/disable/despawn의 local attachment 종료와 server effect 종료를 별도 로그/사유로 기록한다.

읽기 전용 source anchors(prefix `Assets/_Project/Scripts/`): `Application/UseCases/UnitCombatUseCase.cs:2449`, `Infrastructure/Network/NetworkCombatController.cs:2534`, `Infrastructure/Network/NetworkUnit.cs:940`, `Presentation/Effects/EffectManager.cs:487`, `Presentation/Effects/VfxPoolItem.cs:298`. 이들은 기존 writer/teardown/container/AnyAlive 근거이며 Bloom 구현 완료 증거가 아니다.

### 9-3. 수정 후보 인벤토리 — 승인 후만 적용

모든 경로 prefix는 `Assets/_Project/`다. 아래 10기존+4신규는 programmer 기술 권고 후보이며 이번에 만들거나 수정하지 않았다.

| 기존 수정 후보 | 책임 | 규칙 근거 |
|---|---|---|
| `Scripts/Application/UseCases/UnitCombatUseCase.cs` | 수락/refresh/실제 제거 관찰, Bloom provenance 전달; 기존 수학 불변 | Units34, NET-AUTH-001/002, NET-DELIVERY-TIMED |
| `Scripts/Infrastructure/Network/NetworkCombatController.cs` | server publication/종료 연결과 Application 구독 정리 | Units34, NET-AUTH-001, NET-TIME-001 |
| `Scripts/Infrastructure/Network/NetworkUnit.cs` | recipient atomic current state/ready/late join/terminal·lifetime 경계 | NET-TIME-004/005, NET-DELIVERY-TIMED |
| `Scripts/Bootstrap/GameBootstrapper.Map.cs` | 싱글/멀티 배선 및 기존 pose provider 주입 | Units34 단일 틱, NET-ROOT-001~003 |
| `Scripts/Presentation/Effects/EffectManager.cs` | manager container 대상 추종/정지 lease API 후보 | Units31/34, NET-ROOT-002 |
| `Scripts/Presentation/Effects/VfxPoolItem.cs` | all5PS/stop lease/drain/Return once/lease generation | Units31/34, NET-ROOT-002 |
| `Scripts/Presentation/Effects/UnitEffectConfig.cs` | 별도 heal preset 참조 후보, attackPreset 유지 | Units31/34, Sound15 |
| `Resources/Config/UnitEffectConfig.asset` | authored heal GUID를 새 heal slot에 연결할 후보 | Units31/34; attackPreset null 불변 |
| `Scripts/Application/Combat/UnitHealCastDiagnostics.cs` | bounded 대상 효과 proof 후보, caster 회차와 구분 | LogRules1.4/1.7/1.14, NET-DELIVERY-TIMED |
| `Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 gate만 확장 | Units31/34/36, NET-AUTH/ROOT/TIME, Sound15 |

| 신규 후보 — 사용자 source 승인 후만 생성, 각각 meta 동반 | 책임/근거 |
|---|---|
| `Scripts/Application/Combat/BloomHealEffectPresentation.cs` | NGO 비의존 Bloom 상태/identity 계약; Units34·NET-DELIVERY-TIMED |
| `Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs` | atomic network payload; NET-TIME-004/005·NET-DELIVERY-TIMED |
| `Scripts/Presentation/Effects/BloomHealEffectPresenter.cs` | sole emitter/current convergence/attachment·lease; NET-AUTH/ROOT/TIME |
| `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset` | authored prefab 참조만; Units31/34·Sound15 |

Application→Infrastructure/Netcode 역참조 없이 계약을 Application에 두고 Bootstrap에서 조합한다. authored VFX·Attack clip·controller·prefab·Stats는 수정하지 않는다. SFX 미제작은 Sound15 허용에 따라 호출점에 부재 주석을 남기는 후보이며 1회/loop 결정·제작은 선행조건이 아니다.

### 9-4. Bounded proof / 예외 비간섭 계획

- run별 details2048+BEGIN/END2, diagnostic owner256을 후보 예산으로 둔다. cap은 **진단만 제한**하며 실제 heal/VFX 횟수를 제한하지 않는다. suppressed counter와 unknown을 남겨 quota 밖을 성공0건으로 오인하지 않는다.
- 전이 사건 Grant/Refresh/Stop/Publish/Receive/Play/StopEmitting/Drain/Return에 exact key/revision/pool lease/reason을 기록한다. stale/duplicate/pending/missingasset/fault/drop/overflow를 각각 구별하며 local attachment close/server stop/TeardownClear/NotStarted도 분리한다.
- formatted 전체 GameLog envelope UTF-8 **1000B 미만**을 preflight하며 fallback도 key를 보존한다. platform prefix/stack/separator는 이 envelope budget 밖이다. 이 숫자는 구현 후보 예산이지 기존 규칙의 고정 숫자나 실제 런타임 측정이 아니다.
- 기존 GameLog Dev 경로·Conditional/compile guards로 Release 진단 문자열 및 진단 state를 제거한다. gameplay identity/state는 진단 state가 아니므로 제거하지 않는다. per-frame 문자열 로그 금지. 각 단계는 서로 다른 사건이지 같은 사건의 중복 기록이 되지 않게 한다.
- 표현/observer exception은 writer/tick에 전파하지 않는다. fault를 bounded failure로 분류하고 무조건 silent catch 금지. 실제 예외의 운영/개발 심각도 분류·최종 catch 기록은 LogRules1.9/1.14를 따르며 운영 예외를 Dev budget/stripping으로 숨기지 않는다.
- 기존 ParticleSystemCount=root1과 recursive5PS 계측을 분리해 all5PS 시작/정지/소멸 증거를 수집한다. 로그 hook 성공은 particle/pixel·성능·멀티 수용 PASS가 아니다.

### 9-5. 기존 self-validation 확장 후보와 남은 승인 gate

새 validator/TC/QA 없이 기존 RunUnitActionSelfValidation에만 다음 후보를 추가한다.

- GUID/preset/config 정합성, Bloom attackPreset null, authored5PS/loop/local/stopActionNone/useUnscaledTime0 불변.
- 동일 refresh noRestart, all5PS stop/drain/Return once, active particle gap 비회수, pause active/drain 유지, FullHP/death 기존 종료 비차단, 마지막 pose 동결·없는 pose NotStarted, teardown 별도 Clear.
- ordering/closed-reactivation/target ID reuse/old pool lease 차단; Host sole emitter·Client late join/view ready/latest current·stop-before-ready·동일 frame current coalescing.
- non-Bloom Heal/regen/Torrent/DoT noBloom; caster death의 이미 부여된 HoT 비취소.
- clip4s/marker1s/발동 후 cooldown3 및 N/A/SeparateHeal 기존 gates 유지; SimulationRoot/HP/HoT math 비간섭.

규칙 원문 확인 범위: Units34·31·36, combatSync NET-AUTH-001/002·NET-ROOT-001~003·NET-TIME-001/003~005·NET-DELIVERY-TIMED 및 SeparateHeal, Sound15, LogRules1.4/1.7/1.9/1.14. 이는 기존 계약 적용 근거이며 새 gameplay 정책을 정한 것이 아니다.

후속 pause/disconnect/targetremove 권고로 이전 기술 미확정3항목을 구체화했다. 남은 것은 **Main 구체 Plan review→사용자 source 구현 승인**이다. 이후 구현·컴파일·기존 gate·사용자 실기/시각 수용은 각각 별도 증거가 필요하다. 이번 문서 checker0은 소스 구현·연동 완료·실기 PASS를 의미하지 않는다.


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

꽃요정(BloomFairy)의 치료 이펙트를 Unity AI 로 만들기 전에, 이펙트 목록 문서(`VFXSFXList.md`)에 적힌 꽃요정 이펙트 이름을
「공격(attack)」에서 「치료(heal)」로 바꾼다. 꽃요정은 공격하지 않는 치료 유닛이고, 만들 이펙트도 공격 섬광이 아니라
**치료받는 아군에게 붙어 치료가 이어지는 동안 반복되는 이펙트**이기 때문이다.

바꾸는 것은 **문서 1개의 2행**(VFX 이름 1개, SFX 이름 1개)뿐이다. 실제 파일은 아직 없고, 코드·에셋 어디에서도 이 이름을
쓰지 않으므로(Research.md 1절) 이름을 바꿔도 깨지는 곳이 없다. 반면 **애니메이션과 코드의 「Attack」은 그대로 둔다** —
게임이 모든 유닛의 공격 동작을 「Attack」이라는 공통 이름으로 찾기 때문이다(Research.md 2절).

결정이 하나 남아 있다. 이름만 바꿔서 지금 자리(「유닛 공격 이펙트」 표)에 둘지, 성격이 다른 이펙트이니 따로 칸을 만들어 옮길지다.
아래 「미결 사항」에 두 방법을 나란히 적었다. **사용자가 고른 뒤에 구현한다.**

> **[2026-10-09 정정 · 결정 반영]** 위 두 문단은 작성 당시(2026-10-08) 그대로 둔다. 그 뒤 바뀐 사실 두 가지 —
> 1. **VFX 프리팹은 이제 실제로 있다.** 사용자가 Unity AI 로 만들어 처음부터 새 이름으로 저장했다
>    (`Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab` — Research.md 6절). 「실제 파일은 아직 없고」는 **VFX 에 대해서는 더 이상 사실이 아니다**(SFX 는 여전히 없다).
>    그래도 이름을 바꿔 깨지는 곳은 없다 — **옛 이름(`bloomfairy_attack`)을 쓰는 곳이 이 문서 2행뿐이고, 새 프리팹은 이미 새 이름을 쓰고 있으며 아직 아무 데도 연결되지 않았기 때문이다.**
>    즉 이번 변경은 이름 목록 문서를 실물에 맞추는 일이다.
> 2. **자리 문제는 결정됐다.** 사용자가 **방법 B** 를 골랐다 — 꽃요정 힐 이펙트·사운드를 공격 표에서 빼서, 새로 만드는 **「유닛 특수 효과」** 칸으로 옮긴다(3절).
>
> 바뀐 내용과 이유는 바로 아래 「계획 변경 이력」에 적는다.

---

## 계획 변경 이력

WORKFLOW.md [4] 「계획 변경 시 변경 이유도 추가 기록」에 따라 적는다. 원래 계획의 문장은 지우지 않고, 바뀐 자리에 `[2026-10-09 …]` 표시를 붙였다.

| 날짜 | 무엇이 바뀌었나 | 이유 |
|---|---|---|
| 2026-10-09 | 3절 미결 사항 → **결정 사항**: 방법 B 채택, 신규 항목명 「유닛 특수 효과」 | 사용자 결정 (2026-10-09) |
| 2026-10-09 | 2절 변경 항목을 방법 B 기준의 구체적 절차(삭제할 행 · 새 절의 위치 · 열 · 안내문 · 비고 문구)로 확정. 6·7·8절도 B 기준으로 갱신 | 위 결정에 따라 「고른 방법을 따른다」로 남겨 둔 자리를 채웠다 |
| 2026-10-09 | 「실제 파일이 없다」에 기댄 서술(서두 · 2절) 정정. VFX 비고 문구를 「미제작」 → 「프리팹 제작됨 · 게임 미연결」로 변경 | 사용자가 VFX 프리팹을 만들어 main 에 올렸다(커밋 `e84e99f` — 호출 세션 전달값). 2026-10-09 실측으로 파일 존재·미연결 확인(Research.md 6절) |
| 2026-10-09 | 7절 확인 절차의 검색 범위·검색어를 정밀화(`_Tasks/` 제외, 확장자까지 포함한 파일명으로 1건 확인) | 이 Plan/Research 자체에 옛 이름이 적혀 있고, SFX 비고가 VFX 이름을 「쌍」으로 인용하므로 원래 검색어로는 0건/1건 판정이 성립하지 않는다 |

---

## ⚠️ 기존 로직 제거 — 없음

이번 작업은 문서의 이름 2개를 바꾸는 것이며 **코드·로직을 제거하거나 비활성화하는 항목이 없다**(WORKFLOW.md [4] 「기존 로직 제거 규칙」 해당 없음).
미결 사항에서 방법 B 를 고르면 표의 행 2개가 다른 표로 **이동**하지만, 이는 문서 행의 위치 이동이지 로직 제거가 아니다.
**[2026-10-09]** 방법 B 로 결정됐다 — 위 문장대로 행 2개의 **이동**이며 로직 제거 항목은 여전히 없다.

---

## 1. 근거 규칙

WORKFLOW.md [4] 에 따라 각 항목의 근거를 적는다. **에셋 파일 이름을 정하는 규칙은 `GameSystemRules` 에 없다** — 이름의 형식은
에셋 가이드 문서가 정한다. 그래서 아래 표는 「직접 근거」와 「관련 규칙」을 구분해 적는다.

| 항목 | 직접 근거 | 관련 GameSystemRules |
|---|---|---|
| VFX 이름 변경 | 사용자 결정(이펙트 에셋에서 꽃요정 attack → heal) + `VFXSFXGuide.md` 「파일 명명 규칙」 `vfx_[에셋명]_[이벤트].prefab` — `heal` 은 「이벤트」 자리에 들어가며 규칙 안이다 | 이름을 정하는 GameSystemRules 규칙은 **없음**. 이 이펙트가 반복 재생이어야 하는 이유는 `GameSystemRules_Units.md` 규칙 34(종료 처리 · 갱신 = 리셋) |
| SFX 이름 변경 | 같은 사용자 결정 + `VFXSFXGuide.md` 「파일 명명 규칙」 `sfx_[에셋명]_[이벤트].wav` | `GameSystemRules_Sound.md` 규칙 15(VFX+SFX 쌍 호출) — 이 규칙은 **호출 위치**를 정하는 규칙이라 이름을 정하지는 않는다. 다만 짝을 이루는 두 에셋의 이름을 같은 이벤트명(`heal`)으로 맞춰 두면 연결 작업에서 짝을 찾기 쉽다 |
| 애니메이션·코드 「Attack」 유지 | 사용자 결정 + 코드 실측(Research.md 2절) | `GameSystemRules_Units.md` 규칙 17(타격 프레임은 Attack 클립의 `OnAttackHit` 이벤트가 단일 출처) · 규칙 32(힐 발동은 `HitFrameTimes` 타이머) |
| (방법 B 를 고를 경우) 별도 칸으로 이동 **[2026-10-09: 방법 B 확정 — 「유닛 특수 효과」 절로 이동]** | 문서 선례: `VFXSFXList.md` 「건물 특수 효과 이펙트」 / 「건물 특수 효과 사운드」 절(반복 재생 이펙트를 별도 칸에 두고 재생 방식을 비고에 적음) | `GameSystemRules_Units.md` 규칙 31 — 「유닛 공격 VFX 는 `attackPreset` 에 연결한다」. 공격 표에 남으면 이 문장대로 잘못 연결될 여지가 있다(Research.md 4절 2번) |

---

## 2. 변경 항목

대상 파일: `Assets/_Project/Docs/Assets/VFXSFXList.md` **한 개뿐**.

### 항목 1. VFX 이름 변경 (46행)

- 변경 전: `` | Transcendence | BloomFairy | `vfx_bloomfairy_attack.prefab` | 꽃잎 힐 | ``
- 변경 후 파일명: `vfx_bloomfairy_heal.prefab`
- 행의 위치와 비고 문구는 **미결 사항에서 고른 방법**을 따른다(아래 3절).

### 항목 2. SFX 이름 변경 (257행)

- 변경 전: `` | Transcendence | BloomFairy | `sfx_bloomfairy_attack.wav` | 힐 차임음 | ``
- 변경 후 파일명: `sfx_bloomfairy_heal.wav`
- 위치는 항목 1 과 **같은 방법**을 따른다(VFX 와 SFX 는 짝이라 한쪽만 옮기지 않는다).

### [2026-10-09 확정] 방법 B 구현 절차

위 항목 1·2 의 「미결 사항에서 고른 방법을 따른다」를 방법 B 로 채운다. 행 번호는 **변경 전 기준**이다(VFX 쪽 삽입 뒤에는 SFX 쪽 행 번호가 밀리므로, 위치는 행 번호가 아니라 **절 제목**으로 찾는다).
새 절은 같은 문서의 선례 「건물 특수 효과 이펙트」(172행) / 「건물 특수 효과 사운드」(383행)를 본뜬다 — H4 제목, 머리 안내 인용문, 그 아래 표, 비고에 재생·정지 조건.

**B-1. VFX — 공격 표에서 빼기**
- `#### 유닛 공격 이펙트` 표에서 46행 `` | Transcendence | BloomFairy | `vfx_bloomfairy_attack.prefab` | 꽃잎 힐 | `` 을 **삭제**한다.
- 삭제 후 이 표는 24행(Human 8 · Spirit 9 · Transcendence 7)이 된다. 표 머리·다른 행은 건드리지 않는다.

**B-2. VFX — 「유닛 특수 효과 이펙트」 절 신설**
- 위치: `#### 유닛 사망 이펙트` 표의 마지막 행(76행, BloomFairy 사망) 다음, `#### 건물 파괴 이펙트`(78행) **앞**. 즉 「Ⅰ. VFX → 2. 제작 예정 VFX」 안에서 유닛 관련 절이 끝나는 자리다.
  - 「1. 완성된 VFX」가 아니라 「2. 제작 예정 VFX」 안에 두는 이유: 이 문서는 이미 존재하는 다른 유닛 VFX 프리팹 9개도 모두 「제작 예정」 표에 두고 있다(Research.md 5절 1번). 「완성된 VFX」 절의 정리는 이번 범위 밖이다.
- 열: 공격·사망 표와 같은 **`종족 | 유닛명 | 파일명 | 비고`** 4열.
- 넣을 내용(문구 확정안):

```
#### 유닛 특수 효과 이펙트

> BloomFairy 치료는 아군 1명에게 거는 **지속 회복(HoT)**이다 — 규칙: [`GameSystemRules/GameSystemRules_Units.md`](../GameSystemRules/GameSystemRules_Units.md) **규칙 34**.
> 공격 순간 1회 재생되는 「유닛 공격 이펙트」와 달리 **치료받는 아군에 붙어 회복이 이어지는 동안 루프 재생**되므로 이 절에 따로 둔다.
> ⚠️ 꽃요정의 `attackPreset` 칸(`GameSystemRules_Units.md` 규칙 31)에 연결하지 않는다 — 그 칸은 치료하는 꽃요정 **자신의 위치에서 1회** 재생한다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | BloomFairy | `vfx_bloomfairy_heal.prefab` | **프리팹 제작됨(2026-10-09 확인) · 게임 미연결** — 치료받는 아군에 붙어 지속 회복 동안 루프 재생. 만료·풀피·대상 사망 시 방출 정지(남은 입자는 자연 소멸), 재부여 시 끊지 않고 유지(시간 리셋) |
```

  - 비고의 재생·정지 조건 근거: `GameSystemRules_Units.md` 규칙 34(종료 3사유 · 갱신 = 리셋), 합의된 설계(Research.md 3-3절 — 끝날 때 방출만 멈춤).
  - 「미제작」 대신 「프리팹 제작됨 · 게임 미연결」로 적는 이유: 프리팹은 있지만(Research.md 6절) 아직 어디에도 연결되지 않아 게임에서는 보이지 않는다. 선례 「건물 특수 효과 이펙트」의 「동작하되 눈에 보이지 않는 상태」처럼, **있는 것과 동작하는 것을 갈라 적는다**(과대 표기 금지).
  - 규칙 번호 뒤에 「」로 규칙 제목처럼 보이는 말을 붙이지 않는다 — `check_docs.py` `[5]`(병기 내용과 규칙 제목 불일치)에 걸릴 수 있다. 규칙 31 의 실제 제목은 「다중 파티클 시스템 VFX 재생」, 규칙 34 는 「HoT/DoT 공용 시간 지속 효과 시스템 (서버 권위 diff 틱)」이다(2026-10-09 실측).

**B-3. SFX — 공격 표에서 빼기**
- `#### 유닛 공격 사운드` 표에서 257행 `` | Transcendence | BloomFairy | `sfx_bloomfairy_attack.wav` | 힐 차임음 | `` 을 **삭제**한다. 삭제 후 이 표도 24행이다.

**B-4. SFX — 「유닛 특수 효과 사운드」 절 신설**
- 위치: `#### 유닛 사망 사운드` 표의 마지막 행(287행, BloomFairy 사망) 다음, `#### 건물 파괴 사운드`(289행) **앞**. VFX 쪽과 대칭이다.
- 열: 같은 4열. 넣을 내용(문구 확정안):

```
#### 유닛 특수 효과 사운드

> 사운드 규칙 15(VFX + SFX 쌍 호출)에 따라 위 **유닛 특수 효과 이펙트**와 1:1로 대응한다.

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Transcendence | BloomFairy | `sfx_bloomfairy_heal.wav` | **미제작** — `vfx_bloomfairy_heal`과 쌍. 1회음/반복음 여부 미정(연결 작업에서 확정) |
```

  - 머리 안내문은 선례 「건물 특수 효과 사운드」 385행 문장을 절 이름만 바꿔 그대로 쓴다(그 문장은 현재 검사 0건으로 통과 중이다).
  - SFX 가 1회음인지 반복음인지는 **정해지지 않았으므로** 비고에 확정해 적지 않고 「미정」으로만 둔다(원래 계획 3절 「미확인」 행 그대로).

**B-5. 건드리지 않는 것**: 두 표의 나머지 행, 사망 표의 BloomFairy 행(76·287행), 「건물 특수 효과」 절, 문서의 다른 모든 절.

---

## 3. ✅ 결정 사항 — 행을 어디에 둘 것인가

> **사용자 결정 (2026-10-09): 방법 B 채택, 신규 항목명 「유닛 특수 효과」**
> - 꽃요정 힐 VFX·SFX 행을 「유닛 공격 이펙트」·「유닛 공격 사운드」 표에서 빼고, 새로 만드는 **「유닛 특수 효과 이펙트」·「유닛 특수 효과 사운드」** 절로 옮긴다.
>   이름은 같은 문서의 「건물 특수 효과 이펙트/사운드」를 본떴다. 구체적인 절차는 2절 「방법 B 구현 절차」(B-1 ~ B-5).
> - 방법 A 는 채택하지 않았다.
>
> 이 절의 원래 제목은 「🔶 미결 사항 — 행을 어디에 둘 것인가 (사용자 결정 필요)」였다. 아래 비교표와 추천 이유는 **결정 근거의 기록으로 원문 그대로** 둔다.
> 다만 비교표 B 열의 비고 예시 문구 「**미제작**」(VFX)은 그 뒤 프리팹이 만들어져 **2절 B-2 의 확정 문구(「프리팹 제작됨 · 게임 미연결」)로 대체**됐다.

| | **방법 A — 이름만 바꾸고 제자리** | **방법 B — 「유닛 특수 효과」 칸을 새로 만들어 이동** |
|---|---|---|
| 파일/문서 변경 | 46행·257행의 파일명만 교체(2행) | ① 46행을 「유닛 공격 이펙트」 표에서 빼고, 「유닛 사망 이펙트」 표 다음에 `#### 유닛 특수 효과 이펙트` 절을 새로 만들어 넣는다 ② 257행도 같은 방식으로 「유닛 사망 사운드」 표 다음 `#### 유닛 특수 효과 사운드` 절로 옮긴다 ③ 두 절 머리에 「반복 재생, 치료받는 아군에 붙음」 안내 1~2줄 |
| 비고 문구 | 「꽃잎 힐」 / 「힐 차임음」 그대로 | 예: VFX 「**미제작** — 치료받는 아군에 붙어 지속 회복 동안 루프 재생. 만료·풀피·대상 사망 시 방출 정지, 재부여 시 유지(시간 리셋)」 / SFX 「**미제작** — `vfx_bloomfairy_heal` 과 쌍」 (문구는 확정 전 예시) |
| 즉시 따라오는 변화 | 없음 | 문서 목차(H4 절)가 2개 늘어난다 |
| 위험 | 「유닛 공격 이펙트」 표에 남아 있어, 연결 작업 때 규칙 31 대로 꽃요정 `attackPreset` 칸에 넣는 잘못된 연결로 이어질 여지가 있다(그 칸은 치료하는 꽃요정 **자신의 위치에서 한 번** 재생한다 — Research.md 4절 2번) | 새 절 이름(「유닛 특수 효과」)은 기존 「건물 특수 효과」를 본뜬 **새 이름**이다. `VFXSFXGuide.md` 의 키워드 표(302·493행)는 여전히 「유닛 공격」 분류 아래 있어 두 문서의 분류가 어긋난다(파일명이 없는 키워드 행이라 기능상 문제는 없음) |
| 얻는 것 | 변경이 가장 작다 | 재생 방식이 다른 이펙트임이 문서에서 바로 보인다. 이미 있는 「건물 특수 효과」 선례(반복 재생 이펙트를 별도 칸 + 비고에 재생·정지 조건)와 같은 형태가 된다 |
| 미확인 | — | SFX 가 한 번 울리는 소리인지 반복음인지는 **아직 정해지지 않았다** — 방법 B 의 SFX 비고에는 이를 확정해 적지 않는다 |

**추천: 방법 B.** 이유 —
1. 이 이펙트는 「공격 순간 한 번 재생」이 아니라 「대상에 붙어 길이가 매번 다른 반복 재생」이다(Research.md 3-1). 공격 표에 두면 표의 다른 24행과 성격이 다른 행이 한 줄 섞인다.
2. 공격 표에 남기면 `GameSystemRules_Units.md` 규칙 31 의 문장이 잘못된 연결(`attackPreset`)을 유도할 수 있다. 연결 작업이 아직 남아 있으므로 지금 칸을 갈라 두는 편이 안전하다.
3. 같은 문서에 「건물 특수 효과 이펙트/사운드」라는 같은 성격의 선례가 이미 있다.

> 방법 A 도 틀린 선택은 아니다 — 이름 결정이라는 이번 작업의 목적은 A 만으로 달성된다. B 의 이점은 **다음 작업(연결)의 오연결 예방**이다.
> 이 두 방법 외의 배치(예: 「보류」 절, 별도 문서)는 검토하지 않았다.

---

## 4. 변경하지 않는 것 (이유 포함)

| 대상 | 이유 |
|---|---|
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy_Attack.anim` (클립 이름) | `UnitFactory.cs:461`·`:496`, `CombatHitEventInjector.cs:260`, `CombatHitEventValidator.cs:176` 이 클립 이름에 「Attack」 포함 여부로 찾는다. 바꾸면 치료 발동 타이밍(`HitFrameTimes`)을 못 찾는다 |
| `Assets/_Project/Animations/Units/BloomFairy/BloomFairy.controller` 의 `Attack` 상태 | `UnitView.cs:54` `Animator.StringToHash("Attack")` 로 재생한다. 바꾸면 치료 동작이 재생되지 않는다 |
| 코드 식별자 `AttackCooldown` · `OnAttackHit` · `UnitAnimState.Attack`(`NetworkUnit.cs:332`) · `PlayUnitAttack` · `PlayUnitAttackSfx` | 전 유닛 공용 경로다. 이번 작업은 문서 작업이며 코드는 범위 밖이다(CLAUDE.md 규칙 6) |
| 위 식별자를 인용한 규칙 문서 문장(`GameSystemRules_Units.md` 규칙 17·27·32·36, `StatsReference.md` 등) | 코드 이름과 같아야 하므로 그대로 둔다 |
| `VFXSFXGuide.md` 302·493행 | 파일 이름이 없는 키워드 행이고 키워드가 이미 「heal」이다(Research.md 1절) |
| `VFXSFXList.md` 76·287행(꽃요정 사망 이펙트/사운드) | 「attack」이 아니다 |
| `VFXSFXGuide.md` 「파일 명명 규칙」 | `heal` 이 규칙 안에 들어가므로 고칠 필요가 없다 |

---

## 5. 범위 밖 — 후속 작업으로 남기는 것

이펙트(`vfx_bloomfairy_heal.prefab`)가 만들어진 뒤 별도 작업에서 다룬다. 이번에는 **아무것도 연결하지 않는다.**
**[2026-10-09 정정]** VFX 프리팹은 이제 만들어졌다(Research.md 6절). 연결은 그대로 **이번 범위 밖**이며 아래 후속 항목도 그대로 유효하며, 6번을 덧붙인다.

1. **`EffectManager` 에 「대상에 붙여 반복 재생 + 정지」 API 신설** — 지금은 위치에서 한 번 재생하는 메서드뿐이다(Research.md 4절 1번).
2. **HoT 시작·갱신·종료에 맞춘 시작/정지 연결** — 종료 3사유(만료·풀피·대상 사망)와 재부여 시 유지 처리(`GameSystemRules_Units.md` 규칙 34). 멀티에서 클라이언트가 HoT 시작/종료를 어떻게 알게 할지도 그때 정한다.
3. **SFX 짝 호출**(`GameSystemRules_Sound.md` 규칙 15)과 SFX 의 1회/반복 여부 결정.
4. **꽃요정 `attackPreset` 칸에 이 이펙트를 넣지 않는다**는 점을 연결 작업 Plan 에 명시(Research.md 4절 2번).
5. (참고) `BloomFairy_Attack.anim` 이벤트 0건과 규칙 27 서술의 관계 확인(Research.md 4절 3번).
6. **[2026-10-09 추가]** 이 프리팹은 자식 4개 외에 **루트에도 파티클 시스템이 1개 있다**. `GameSystemRules_Units.md` 규칙 31(풀 아이템이 직속 자식을 각각 재생)의 재생 경로에서 루트의 것이 시작·정지 대상에 포함되는지 연결 작업에서 확인한다(Research.md 6-2절).

---

## 6. 위험 요소

| 위험 | 대책 |
|---|---|
| 옛 이름이 다른 곳에 남는다 | 변경 후 `bloomfairy_attack` 을 `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 에서 다시 찾아 **0건**을 확인한다(지금은 2건, 둘 다 변경 대상) |
| 표 행 이동(방법 B) 중 행이 사라지거나 두 번 들어간다 | `vfx_bloomfairy_heal` · `sfx_bloomfairy_heal` 이 문서에 **각 1건**인지 확인한다 |
| 코드·애니메이션을 실수로 건드린다 | 대상 파일은 `VFXSFXList.md` 하나뿐이다. 그 외 파일은 열기만 한다 |
| **[2026-10-09 추가]** 이미 만들어진 프리팹·텍스처·머티리얼을 건드린다 | 이번 작업은 문서만 고친다. `vfx_bloomfairy_heal.prefab` 과 같은 커밋의 텍스처·머티리얼(Research.md 6-1절)은 **열기만 하고 수정·이름 변경하지 않는다** — 파일 이름은 이미 새 이름이다 |
| **[2026-10-09 추가]** 새 절의 안내문이 `check_docs.py` 에 걸린다 | B-2 문구는 규칙 번호 뒤에 제목처럼 보이는 「」를 붙이지 않았고, B-4 안내문은 현재 통과 중인 선례 문장을 그대로 썼다. 그래도 걸리면 **참조하는 쪽 문구**를 고친다(규칙 번호는 바꾸지 않는다) |

---

## 7. 구현 후 확인 절차

1. `bloomfairy_attack` 잔존 0건, `bloomfairy_heal` VFX·SFX 각 1건 확인.
2. 리포지토리 루트에서 `python3 Tools/check_docs.py` 실행 → **0건** 확인(WORKFLOW.md [11]). 작업 전 기준선도 0건이다(2026-10-08 실측).
   - `VFXSFXList.md` 는 `Docs/` 하위라 검사 범위 안이다. 이 Plan/Research 는 `_Tasks/` 라 검사 범위 밖이다.
3. 이번 작업은 코드·씬 변경이 없으므로 실기 테스트 대상이 없다.

> **[2026-10-09 정밀화 — 위 1번을 아래처럼 수행한다]** 원래 검색어 그대로면 판정이 성립하지 않는다 —
> 이 Plan/Research 에 옛 이름이 적혀 있고, B-4 의 SFX 비고가 VFX 이름(`vfx_bloomfairy_heal`)을 「쌍」으로 인용하며, 프리팹 자신도 루트 이름으로 `vfx_bloomfairy_heal` 을 갖기 때문이다.
>
> | 확인 | 검색 범위 | 기대값 |
> |---|---|---|
> | 옛 이름 `bloomfairy_attack` | `.md`·`.cs`·`.asset`·`.prefab`·`.unity` 전체, **`_Tasks/`·`_Logs/` 제외** | **0건** |
> | `vfx_bloomfairy_heal.prefab` (확장자까지) | `Assets/_Project/Docs/Assets/VFXSFXList.md` | **1건** — 「유닛 특수 효과 이펙트」 표 안 |
> | `sfx_bloomfairy_heal.wav` (확장자까지) | `Assets/_Project/Docs/Assets/VFXSFXList.md` | **1건** — 「유닛 특수 효과 사운드」 표 안 |
> | 절 제목 `#### 유닛 특수 효과 이펙트` · `#### 유닛 특수 효과 사운드` | 같은 문서 | **각 1건**, 각각 「유닛 사망 이펙트/사운드」 바로 다음 · 「건물 파괴 이펙트/사운드」 앞 |
> | 「유닛 공격 이펙트」·「유닛 공격 사운드」 표의 행 수 | 같은 문서 | **각 24행**(BloomFairy 행 없음) |
>
> 그다음 2번(`python3 Tools/check_docs.py` → **0건**)을 그대로 수행한다. 2026-10-09 작업 전 기준선도 **0건**이다(실측).

---

## 8. 변경 예정 파일

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Docs/Assets/VFXSFXList.md` | 항목 1·2 (방법 A: 2행 교체 / 방법 B: 2행 이동 + 절 2개 신설) **[2026-10-09 확정: 방법 B — 공격 표 2개에서 행 1개씩 삭제 + 「유닛 특수 효과 이펙트」·「유닛 특수 효과 사운드」 절 신설(2절 B-1 ~ B-4)]** |

작업 문서(이번에 작성): `Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md` · `Plan.md`
