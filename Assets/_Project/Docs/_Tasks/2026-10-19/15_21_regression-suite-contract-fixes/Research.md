# 회귀 검증의 Bloom·Fox 계약 불일치 조사 — Research

회귀 검증에서 Bloom 힐 이펙트와 FoxMagician marker 검사 오류가 보고됐다. 실제 게임 동작을 바꾸기 전에, 검사에 남은 옛 프리팹 가정과 회전값 비교 방식 때문에 실패한 것인지 구분한다. 조사 중 main에 다른 작업이 통합돼 Bloom 수정이 이미 들어왔으므로, 현재 남은 Fox 검사만 좁혀 고치는 것이 목적이다.

> **현재 단계 (2026-10-19):** 사용자 전체 suite PASS 수용 후 승인된 Fox 옛 단언 주석 정리와 최종 검토를 완료했다(§6). 사용자 native 메뉴 실행과 에이전트 직접 실행·기기 플레이·전체 공격 시각/HP 수용은 구분한다. 게시 상태는 Git과 메인 최종 보고로 별도 확인한다. §1~§3은 수정 전 근거다.

## 1. 조사 중 main 변경과 범위 확정

| 시점 | 근거와 해석 |
|---|---|
| 최초 main 조사 `b55dfff6` | 메인 인계에서 Bloom runtime/fixture의 활성 5개 고정 검사와 실제 PS 4개가 충돌했다. 초기 다른 브랜치의 DLL/source 불일치는 그때의 관찰이며 현재 main 오류 원인을 stale DLL로 단정하는 근거가 아니다. |
| 현재 main `1c87fcb4` | 외부에서 `Merge codex/ui-improvements`가 반영됐고 `148ae369`의 VFX 후속 작업도 main에 합류했다는 메인 확인을 받았다. 문서 담당도 현재 브랜치 main·HEAD와 실제 Bloom 수정 내용을 재확인했다. 이 작업에서 merge나 소스 변경을 수행한 것은 아니다. |
| 현행 수정 대상 | Bloom 변경은 이미 있으므로 보존한다. Fox의 기존 `ValidateFoxMagicianMarkerCallbacks` 한 함수만 교정하는 계획으로 좁힌다. 타워 추적·recipient fit 등 함께 들어온 외부 구현을 되돌리거나 별도로 가져오지 않는다. |

## 2. 원본과 수정 전 코드 확인

[게임 규칙 인덱스](../../../GameSystemRules.md)를 읽고 [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md)의 「전투 연출 동기화 규칙」 규칙 17과 FoxMagician typed marker 계약, 「특수 공격 시스템 규칙」 규칙 34를 확인했다.

### 2-1. Bloom — 최초 충돌과 현재 해결 상태를 구분

- 실제 프리팹 `Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab`에는 ParticleSystem 직렬화 블록이 4개다. root와 하위 authored 시스템이며 규칙 34도 전 파티클 시스템 재생·개별 loop/scaled/None/Local/burst 0 계약을 따른다. 개수를 5나 4로 고정하는 규칙은 아니다.
- 현재 `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`의 `PlayBloomLoop`에서는 옛 `Length != 5` 거부가 R-1 주석으로 비활성화됐고 `Length == 0` 거부와 개별 시스템 검사가 활성이다. 기존 RecipientFit 경로도 이미 있다.
- 현재 `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`의 `ValidateBloomHoTEffectBoundary`는 `authored.Length >= 1`과 root PS 존재를 검사한다. 재생 후 lease 유효·`BloomRecursiveSystemCount == authored.Length`·root count 1을 확인한다. 옛 5개 단언은 R-3 주석으로 남아 있다.
- 따라서 최초 main의 활성 5개 가정 문제를 현재도 미수정이라고 쓰지 않는다. 현재 Bloom runtime/검증기를 추가 수정하거나 4개 고정으로 바꿀 필요가 없다는 것이 현행 범위 판단이다. 실제 최신 suite 실행 성공은 아직 확인하지 않았다.

### 2-2. Fox — Quaternion 비교가 불변값을 다르다고 판단하는 fixture 문제

- 같은 검증 파일의 `ValidateFoxMagicianMarkerCallbacks`는 비활성 GO에 UnitView를 붙이고 reflection으로 유닛/lease를 설정한다. 회전 자동 속성은 별도 초기화하지 않아 기본 Quaternion `(0,0,0,0)`일 수 있다.
- 현재 단언은 impact callback 뒤 `RemainingMarkerCount == 1 && view.AttackPhaseLastEmissionRotation == before`를 함께 검사한다. 이 비교가 실제로 남아 있음을 직접 읽었다.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 `AttackPhaseLastEmissionRotation`은 기본값 initializer가 없는 자동 속성이고, 해당 속성 대입은 emitter 뒤의 `spawnRot` 기록이다. Fox impact 분기는 charge lease 소비·emitter 기록보다 앞에서 return한다. 이는 이번 fixture 실패가 게임 emitter 재실행을 직접 보여주는 것이 아니라는 정적 근거다.
- 메인은 실제 UnityEngine.CoreModule DLL을 참조한 격리 CLR에서 zero Quaternion의 `==`는 false, `Equals`는 true이며 identity의 두 비교는 true임을 독립 실행으로 확인했다. 임시 `.utmp/fox-quaternion/fox.csproj`·`Program.cs`도 읽어 입력과 참조를 확인했다. 메인의 `dotnet run --project .utmp/fox-quaternion/fox.csproj --no-build --verbosity quiet` 결과는 exit 0 인계값이며 문서 담당이 재실행한 것은 아니다.

metadata 불변만으로 모든 emitter 호출이 없음을 증명한다고 과장하지 않는다. 현행 조기 return의 정적 근거와 fixture 비교 오류를 함께 구분해 적는다. 격리 Quaternion 비교는 Unity native 메뉴·전체 suite·실제 경기 PASS가 아니다.

## 3. 교정 방향과 보존 경계

Fox fixture에서 charge lease 불변 단언과 emitter metadata 불변 단언을 분리한다. scope·target·position·rotation snapshot을 비교하되 회전은 각 성분의 정확한 동일성을 검사하는 `Quaternion.Equals`를 사용한다. zero 기본값과 유효 회전 조건을 모두 확인하고 기존 Host/Client impact 불변 → charge 1회 소비 → 중복 charge 무소비 경로를 유지한다. 상세 변경은 [Plan](Plan.md)을 따른다.

production UnitView의 회전 초기값을 identity로 바꾸거나 새 계측 API를 추가하지 않는다. Bloom·HP·권위 timer·marker 시각·실제 VFX 재생 경로도 바꾸지 않는다. 새 TC·QA·로그·메모리·상시 문서를 생성/수정하지 않는다. 외부 로그·meta·동시 작업은 보존한다.

## 4. 미확인 사항

- 최초 오류가 발생한 실행 세대와 현재 main 세대는 다르다. 현재 Bloom 수정이 보인다는 사실은 최신 전체 suite가 통과했다는 뜻이 아니다.
- Fox fixture의 zero·identity 보강과 별도 컴파일은 완료했고(§5), 이후 사용자 suite PASS 보고는 §6에서 구분한다.
- 사용자 native suite 수용을 실제 경기·시각·역할별 VFX/HP 수용이나 에이전트 직접 실행으로 확대하지 않는다.

## 5. 승인 구현·별도 검증 결과

수정 파일은 `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`, 함수는 `ValidateFoxMagicianMarkerCallbacks` 하나다. 실제 코드를 읽어 zero·identity 각각의 Host/Client 조건, fixture 전용 회전 seed, impact 전 lease·scope·target·position·rotation snapshot, 분리 단언과 `Equals` 비교를 확인했다. 기존 charge 1회 소비·중복 무소비·SOLO/HP 검증과 finally 복원을 유지했다. 구현 인계 당시 옛 복합 Require는 사용자 suite 수용 전 주석으로 보존했다.

메인은 전체 diff에서 함수 밖 변경이 없고 구현 인계 당시 SHA가 `80D2D60008E28ED3A0751288C10BD832A55F37DED85AA2B84C3F9E9F8877DE6D`로 일치함을 독립 확인했다. programmer도 함수 밖 exact text 동일성을 확인했다. Bloom·UnitView·production API·기타 동시 작업은 변경하지 않았다. 수용 후 주석 정리 세대는 §6에서 구분한다.

| 확인 | 최종 근거와 경계 |
|---|---|
| Runtime 별도 컴파일 | exit 0·오류 0·경고 23 |
| Editor 별도 컴파일 | exit 0·오류 0·경고 0, 메인이 실물 로그의 ExitCode 0 확인 |
| 실제 Unity DLL 격리 CLR 비교 | exit 0. zero/identity에서 복사 Equals=True·성분 변이 Equals=False, zero의 `==`는 False |
| native·전체 suite·빌드·QA·사용자 재실행 | 구현 인계 당시 NOT RUN. 이후 사용자 전체 suite PASS는 §6이며 에이전트 native·빌드·QA 실행으로 바꾸지 않음 |

처음 기존 rsp의 낡은 source 목록 오류를 발견했으나 최종 scratch rsp는 현행 source 집합으로 갱신했다. 메인이 각 rsp의 실제 입력을 현재 Runtime 309·Editor 8 source 전수 집합과 독립 대조해 양쪽 차집합 0을 확인했고, Runtime/Editor 로그의 ExitCode 0·오류 0·경고 23/0도 직접 재계수했다. 최초 시도 실패를 현재 성공과 같은 세대로 합치지 않는다. 메인은 comparison 실물 로그도 확인했다. 이는 메인의 직접 검토 인계이며 문서 담당이 컴파일을 재실행한 것은 아니다.

임시 근거는 `.utmp/fox-quaternion/{runtime,editor,comparison}.log`와 `{rally,rally-editor}.rsp`다. scratch 산출물이며 영구 보존 근거 링크가 아니다. 이 최초 구현·별도 검사 단계에서는 게시 승인이 없었고 문서 담당도 source·git·메모리·상시 문서·외부 meta를 변경하지 않았다. 이후 사용자 수용과 문서·메모리 갱신 승인은 §6을 따른다.

## 6. 사용자 전체 suite PASS 수용과 최종 정리

사용자가 「모두 PASS 확인」이라고 보고하고, 문서·메모리 반영 후 push하도록 승인했다. 기존 Unity native 전체 회귀 suite가 Fox 검사에서 멈추던 문제는 이 사용자 재실행 결과로 수용·종결한다. 정확한 실행 시각·Host 배치·장치별 플레이·계측 로그를 확인한 것은 아니며 전체 공격 표현·HP·시각 동등성까지 수용하지 않는다. 에이전트는 native suite를 직접 실행하지 않았다.

승인 Plan에 따라 Fox 옛 복합 Require 3줄과 정리 대기 설명 1줄만 삭제했고 zero 비교의 유용한 설명은 유지했다. 메인은 정리 전후 Editor/Player의 전체 Roslyn 토큰·정확한 문자열 동일성과 trivia 불변/숫자·문자열 변이 검출 PASS 2행을 독립 확인했다. 함수 밖 diff는 없고 다른 VFX 비활성 블록은 보존했다.

정리 후 최종 검증기 SHA256은 `C774CECC4052C80A8B056A0BFDABB166DCFB9ECEB923E1011B87B6DCEAC5632E`로 메인이 독립 대조했다. 최신 Editor 8 source 입력 재컴파일은 exit 0·오류 0·경고 0이다. Runtime source는 바뀌지 않아 기존 309 입력 컴파일 통과 근거를 유지했으며 정리 후 Runtime 재실행은 없었다. 메인의 소스 공백 검사는 exit 0이다.

임시 근거는 `.utmp/fox-quaternion/cleanup-syntax.log`·`cleanup-editor.log`다. 원문 로그·최종 해시는 메인 직접 검토 인계이며 문서 담당이 재실행한 것은 아니다. 주석 정리의 활성 동작 무변경 근거와 정리 전 사용자 전체 suite PASS를 구분하고, 에이전트 native 실행이나 정리 후 새 사용자 실행으로 확대하지 않는다. 구현·수용·승인된 정리 범위를 종결하며 게시 상태는 Git과 메인 최종 보고로 별도 확인한다.
