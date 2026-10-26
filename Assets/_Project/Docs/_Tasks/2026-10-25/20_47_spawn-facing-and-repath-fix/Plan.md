# 생산 방향·전투 복귀 문제 우선 교정 계획

최적화에 앞서 사용자가 발견한 방향 문제와 저장 로그의 이동 복귀 실패를 분리해서 교정한다. 증거가 부족한 부분은 원인을 가르는 최소 관측부터 준비하고, 확인된 코드 문제만 좁게 수정한다. 이번 문서는 수정 범위·보존할 계약과 검증 순서를 관리한다.

> 1.2는 건물 사망 알림을 유지하고, 경로 캐시 무효화/즉시 재경로 구독 두 곳만 **상태 제거 완료 뒤의 새 이벤트**로 옮기는 교정안이다. 사망 처리·조회·환불·게임 종료 기능을 폐기하지 않는다. 기존 로직의 최종 삭제나 상시 진단/VFX 제거를 확정하지 않으며 메인 전체 계획 검토/GO 전 source는 수정하지 않는다.

- 조사 원본: [Research.md](Research.md).
- 기준: `work / 572d94af`, Unity6000.3.5f2.
- 현재 상태(2026-10-26 KST): 원격 main f367cb52를 병합한 HEAD는562888d2다. 기존 진단 준비16경로를 복원하고 새 투사체가 비행 중일 때 서버 요청 검사를 실행하지 않는 전제만 보강했다. 추가분은 미커밋·미푸시이며 실제 새 Unity 컴파일/실기는 미실행이다. 기존 일반 플레이 수용·싱글 이번 미재현/원인 미확정과 Profiler 보류를 유지한다. 최신 통합 상태는10절/Research12를 따르며 main의 VFX/투사체 통합을 이 진단의 전기능 PASS로 확대하지 않는다.

## 1. 코드 전 분할과 규칙

### 1.1 싱글 생산 대기 해제 후 지속 방향 어긋남

Pistoleer 한 번의 관찰이며 다른 유닛이나 팀 색상은 확인하지 못했다. 처음 생성된 순간만이 아니라 이동 중 지속된다는 사용자 답변을 기준으로 한다. 원인이 미확정이므로 현재 방향을 보정하지 않고 **첫 생산 차단 Pistoleer 한 개체**만 추적하는 개발용 관측을 준비한다. 전문 정확안 `/tmp/hexiege-solo-spawn-facing/design.json`과 baseline을 문서 담당이 읽었다. 이 단계는 기능 수정 완료가 아니라 증거 수집 준비다.

| 정확 변경 파일 | 코드 전 계약 |
|---|---|
| [UnitProductionUseCase.cs](../../../../Scripts/Application/UseCases/UnitProductionUseCase.cs) | `UNITY_EDITOR || DEVELOPMENT_BUILD` 안에 대기 ProductionState 참조·상한을 둔 blocked 시도 수·pending UnitData 참조·생산건물ID·한 번 선정 여부의 private 상태5개만 둔다. 기존 생산 시작 대입 두 곳과 대응 Unregister에서 대기 기록을 초기화한다. 싱글/Pistoleer에서 실제 spawnTile 부재가 확인된 첫 대기만 추적하고, 같은 대기가 실제 SpawnUnit에 성공한 뒤 기존 상태 reset 전에 **동일 UnitData 참조**를 pending으로 넘긴다. 개발용 TryClaim API는 같은 참조를 한 번만 소비하며 건물/시도 정보를 반환한다. 기존 생성/큐/생산시간·실패 반환은 유지한다. |
| [ProductionTicker.cs](../../../../Scripts/Presentation/Production/ProductionTicker.cs) | OnUnitProduced의 실제 UnitView 조회 뒤, 기존 null/IsMoving 가드 앞에 개발용 preflight를 둔다. 싱글·살아있는 같은 UnitData 참조·가용한 View만 claim해 기존 랠리/기본 MoveTo 이전에 BeginBlockedSpawnFacingTrace를 호출한다. 기존 가드는 삭제하지 않으며 View가 없는 경우 임의 개체로 대체하지 않고 이벤트/이동 순서를 유지한다. |
| [UnitView.cs](../../../../Scripts/Presentation/Unit/UnitView.cs) | 개발 조건부 진단 상태를 선정 개체 하나에만 만들고 첫 이동 요청 전 Begin, 기존 개발 LateUpdate에서 관측, OnDisable에서 종료한다. 최대4초 realtime·초당4회·샘플≤16와 Begin/End를 합쳐 **최대18줄**이다. Animator transform과 유효한 Humanoid Hips Transform은 시작 시 캐시한다. pose·회전·Animator·네비게이션은 쓰지 않는다. |

필드/메서드/호출은 모두 같은 개발 guard로 감싸 Release 진단은 남기지 않는다. Domain/GameEvents와 Movement partial·에셋·회전속도·production 통계는 이 단일 관측을 위해 변경하지 않는다. 기록할 값은 Unit/type/team/생산건물/blockedAttempts·unscaled 시간/frame, 실제 이전 프레임 대비 Root XZ 이동량/방향, 이미 있는 desiredTarget의 유효 여부/방향, Root yaw·읽기 전용 ObservedVisualRoot yaw·Animator transform yaw·가용한 raw Hips.forward yaw, Movement/Action 소유권과 intent/Legacy·Reducer/현재 애니 상태다. 새 경로 질의·ProjectNow 호출·pose writer를 추가하지 않는다.

[LogRules.md](../../../LogRules.md)의 기존 줄 길이 예산인 UTF-8 900바이트와 현재 로그 envelope 예산을 유지한다. 총량·기간·줄 길이 정적 확인을 따로 기록하고 새 상시 로그/파일 저장 정책을 만들지 않는다. Hips/raw mesh 축은 보정된 시각적 정면이나 최종 렌더 픽셀의 증거가 아니다. LateUpdate의 값도 최종 프레임 정면과 동일하다고 가정하지 않으며 동종 정상 사례/영상과 함께 해석한다. 한 게임의 첫 대상만 관측하므로 여러 생산건물·재현 지연을 전수 포괄하지 않는다. 실제 이전 위치는 관측하지 않는 프레임에도 갱신해 이동량을 한 프레임 값으로 유지하고, 이동하지 않았으면 방향 가용 여부를 false로 쓴다. payload UTF-8+envelope120이900바이트를 넘지 않게 하며 길이 fallback도18줄 예산에 포함한다. 4초 만료·OnDisable·재초기화 모두 End 한 번으로 닫고 누락·생략을 정상으로 판정하지 않는다.

근거: [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md)의 U-MOV-ALIGN과 이동 규칙45, [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 NET-ROOT-001·NET-ROOT-002. Simulation Root를 임시로 덮어쓰거나 mesh 보정으로 판정 오차를 숨기지 않는다.

### 1.2 멀티 전투 종료 후 복귀 경로 실패

unit54/64의 저장 실패는 `post-combat-no-safe-forward-center`/Unreachable다. 전문 제안과 문서 담당 원문 확인에서 사망 이벤트가 아직 건물/HasBuilding이 남아 있는 시점에 캐시 무효화→즉시 재경로를 유발하고, 실제 제거 후에는 완료 통지가 없어 그 사이 재생성한 차단 캐시가 남을 수 있는 순서를 확인했다. 원시 로그에는 실제 캐시 재생성 키가 없어 이 경기 실패의 완전한 인과 증명과 source 순서 결함을 구분한다. 잘못된 후보를 fail-closed하는 보호나 관측기 verdict를 완화하지 않고 상태 변경 완료 뒤에 재경로를 요청한다.

| 정확 변경 파일 | 코드 전 계약 |
|---|---|
| [GameEvents.cs](../../../../Scripts/Application/Events/GameEvents.cs) | `public readonly struct BuildingRemovedEvent`에 `public readonly BuildingData Building`/동명 생성자를 추가하고 `public static readonly Subject<BuildingRemovedEvent> OnBuildingRemoved`를 추가한다. 기존 Died DTO/Subject/API는 유지한다. 기존 Died XML의 FlowField/Bootstrap 소비자 안내 두 곳만 새 Removed 설명으로 정합화한다. |
| [BuildingPlacementUseCase.cs](../../../../Scripts/Application/UseCases/BuildingPlacementUseCase.cs) | RemoveBuilding의 기존 TryGet/타일 HasBuilding=false/MiningPost 소유권·이벤트/동일 인스턴스 위치 인덱스 제거를 유지한다. `_buildings.Remove(buildingId)` 성공 뒤에만 새 Removed를 한 번 발행하고 true 반환한다. 없는 ID/실제 삭제 실패는 false·발행0이다. Demolish의 Died→Remove 순서는 유지하고 Upgrade/Clear에서 새 이벤트를 발행하지 않는다. |
| [FlowFieldService.cs](../../../../Scripts/Application/Services/FlowFieldService.cs) | Initialize의 Died 구독 한 곳을 Removed로 바꾼다. OnPlaced·InvalidateAll/cache/revision·Dispose/재초기화의 구독 교체는 유지한다. |
| [GameBootstrapper.Network.cs](../../../../Scripts/Bootstrap/GameBootstrapper.Network.cs) | SetupEagerRepathOnBuildingChanges의 Died 구독 한 곳만 Removed로 바꾼다. OnPlaced/서버 가드/살아있는 유닛 처리/이전 Dispose와 새 컨테이너/OnDestroy 해제는 유지한다. |
| [RunUnitActionSelfValidation.cs](../../../../../Editor/Tools/Combat/RunUnitActionSelfValidation.cs) | 기존 blocked-authoritative-start 회귀 인접에 `ValidateB3BuildingRemovalRepathLifecycle`를 추가하고 기존 전체 검사에서 한 번 호출한다. 준비된 fixture와 실제 실행 결과를 분리한다. |

기존 Died 소비자인 전투/네트워크 사망 조회·Factory·GameEnd·환불은 순서를 바꾸지 않는다. FlowField 초기화가 eager 구독보다 앞서는 현재 조합 순서를 유지해 Removed에서 무효화→재계산이 이어지게 한다. 기존 RemoveBuilding의 공개 signature/실패 반환·MiningPost/Blocked 이동 불가 조건·기존 meta는 유지한다. source4+기존 suite1의5파일이며 신규 C#/meta는 없다.

회귀안은 실제 UseCase/FlowField와 이벤트를 사용한다. Died 안에서는 건물 ID/위치 조회와 HasBuilding=true가 유지되고 차단 캐시를 의도적으로 만든다. Remove 완료 콜백에서는 조회 제거/HasBuilding=false와 (5,16)→(6,16) 직접 경로가 확인돼야 한다. 철거·전투와 같은 Died→Remove 단계, 같은 ID/없는 ID 재삭제의 이벤트0, 광산/Blocked의 계속 차단, 실제로 격리된 목표의 Unreachable, Upgrade/Clear의 제거 알림0을 확인한다. fixture의 전역 Subject/FlowField 구독은 finally에서 해제하고 기존 same-tile/invalid-root/egress/budget 회귀는 보존한다. 구버전 RED/교정 후 GREEN은 아직 예상이며 실행하지 않았다.

근거: [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md)의 U-MOV-ALIGN 항목11, U-MOV-REPATH 및 이동 규칙45, [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 NET-AUTH-001·NET-ROOT-001. 위치 스냅 없이 현재 pose에서 안전한 진행을 보장하고, 실제 차단/우회 불가는 기존 WaitingRepath/Blocked 계약으로 남긴다.

### 1.3 관측부족과 Lobby404

frames/samples0 종료는 이번 source 결함의 증명으로 사용하지 않는다. Lobby404는 기존 catch/실패 UI·입력 복구 흐름만 읽기 확인하고 현재 정책을 바꾸지 않는다. 사용자 코드 입력이 잘못됐는지 알 수 없으므로 사용자 원인으로 단정하지 않는다.

두 안을 합친 source 범위는 기존8 C#(1.1의3개+1.2의5개)이며 신규 source/meta는 없다. 기존 미커밋13문서/meta 경로를 보존한 합계21경로가 예상된다. 정확 범위는 구현 후 다시 확인한다. 두 source안 모두 메인의 전체 계획 검토/GO 전에는 적용하지 않는다.

### 1.4 게임 종료 뒤 늦은 전투 진입 차단(2026-10-26 KST, 구현 전 계획)

아래는 메인 GO 이전에 작성한 계획이며 적용 결과와 남은 확인은7절에 기록한다.

re1의 포기 종료 뒤 같은 프레임에서 발생한 두 provisional handoff 실패를 대상으로 한다. 게임이 끝난 상태에서는 새 타겟과 공격 표현을 등록하지 않도록, [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs)의 `OnUnitEnteredCombatHandler` 첫 가드 한 곳에 기존 `_combatStopped`를 추가한다. 정확한 조건은 `if (!IsSpawned || !IsServer || _combatStopped) return;`이며, 종료 뒤에도 UnitView 코루틴의 진입 이벤트가 도착할 수 있어 이를 차단한다는 한국어 설명 두 줄을 붙인다. 메인 검토/GO 전에는 적용하지 않는다.

가드는 서비스·유닛·타겟 조회와 타겟 캐시 변경, `BeginProvisionalAttackPresentation`보다 앞선다. 기존 spawn/server 보호를 유지하고, 경기 진행 중 `_combatStopped=false`인 경로의 타겟·Held·공격 처리에는 손대지 않는다. `OnGameEndHandler`의 true 설정과 `OnNetworkSpawn`/`OnNetworkDespawn`의 false 초기화도 유지한다. Observer 오류를 숨기거나 native owner를 다시 열지 않으며, 이미 확정된 피해·결과·종료 정책도 바꾸지 않는다. 관측된 이벤트 입구의 교정으로 한정하고 별도 atomic ACK 경로 전체의 종료 문제 해결을 주장하지 않는다.

근거는 [전투 동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 「1. 권위 경계」 NET-AUTH-001(공격 회차 생성·취소·종료의 서버 권위)과 NET-PRESENT-003의 미래 표현 허가 종료·확정 결과 보존 계약이다. 새 규칙이나 공격 타이밍·밸런스·VFX 정책을 도입하지 않는다. 기존 C# 한 파일만 변경하며 에셋/meta/원시 로그/Profiler는 유지한다.

정적 검토에서는 정확한 한 가드와 설명 두 줄 외 원문 보존, 초기화·종료 순서 및 기존 meta 보존을 대조한다. 컴파일·Unity·기기 실행은 별도이며 이 환경에서 실행하지 않은 결과를 PASS로 기록하지 않는다. 후속 실기 범위는 멀티 포기/정상 승패 뒤 새 provisional 진입이 없는지와 재경기에서 정상 전투가 다시 시작되는지다. 조건식을 복제하는 전용 테스트는 추가하지 않는다.

## 2. 구현 순서와 경계

1. 전문 담당이 각 문제의 정확 source안과 한계·변경 전 원문/SHA를 제시한다.
2. 문서 담당이 파일·동작·보존·규칙·검증 근거를 기록하고 메인이 원문과 전체 계획을 검토한다.
3. GO 뒤 해당 범위만 구현하고 기존meta·자산·다른 source와 허용 차이를 정적으로 대조한다.
4. 가능한 컴파일/기존 회귀 검사를 수행하고 실제 실행과 정적 대조의 증거를 분리한다. 현재 작업 환경에 Unity가 없어 불가능한 실행을 PASS로 기록하지 않는다.
5. 사용자 PC의 Unity/Android 환경에서 같은 재현을 확인한다. 필요한 자료와 순서는 구현 결과에 맞춰 이 계획에 갱신한다.

기존 리팩토링 문서와 보류 Profiler 초안은 보존한다. 별도 Testcase/상시문서/메모리·원시 로그·Git 쓰기/pull/fetch·네트워크/품질/밸런스 수치 변경은 이번 첫 범위에서 하지 않는다.

## 3. 회귀에서 확인할 것

| 확인 | 기대·한계 |
|---|---|
| 생산 건물 포위→대기→주변 건물 파괴→생산 | 생성 여부·랠리 선택과 이동 Root/보이는 몸체 방향을 분리해 관찰. 같은 프레임/유닛에 연결할 증거 필요 |
| 일반적인 막히지 않은 생산·이동·전투 | 기존 생성 방향·회전속도·소유권·공격 진입 유지 |
| 전투 후 기존 경로 복귀 | 안전한 중심/경로가 있으면 진행, 실제 차단이면 기존 정지/재경로 유지. 기존 unit54/64 유형 회귀와 역할 교대 멀티 확인 |
| 게임 종료·재경기 | 관측/이벤트·진행 이력이 이전 경기로 새지 않음 |
| 기존 자동 검사 | 실제 수정 영향에 맞는 기존 검사를 사용. 실행 전·준비된 시나리오를 PASS로 기록하지 않음 |

## 4. 증거와 문서 검사

구현·source 정적 대조·컴파일·기존 검사·기기 관찰 결과를 따로 기록한다. `python3 Tools/check_docs.py`, 새/기존 Task 링크와 신규 GUID 유일성을 확인하며 source 변경 전 계획을 먼저 완성한다. Profiler 캡처와 성능 개선률은 이번 기능 교정의 검증 결과에 포함하지 않는다.

## 5. 적용 결과와 다음 확인(2026-10-26 KST)

1.2의 건물 제거 완료 후 경로 캐시 갱신 교정을 적용했고 기존 suite에 실제 UseCase 기반 수명 회귀를 추가했다. 1.1은 원인 판정을 위한 개발용 관측만 적용했으며 싱글 방향 오류의 교정 완료가 아니다. 전문 원문/Release/기존meta 정적 결과는 [Research7절](Research.md#7-구현정적-대조-결과2026-10-26-kst)에 기록했다. 메인 독립 대조와 QA 정적 검토는 완료했고 blocker는 없었으나 실제 compile/suite/기기 재현은 아직 없다.

후속은 최신 코드 컴파일→Play 종료 상태에서 기존 `Hexiege > Combat > Run Full Regression Suite` 실행→문제가 없으면 Development 기기/Editor 싱글 재현 순서다. 재현에서는 새 경기의 첫 Pistoleer 생산을 건물로 막아 실제 대기가 생긴 뒤 통로를 열고, 생산 직전부터 이동4초 이상을 같은 화면으로 관찰한다. 가능한 영상과 Editor/기기 원본 로그에서 `[UAS-SPAWN-FACING]` begin/sample/end·유닛/팀/생산건물/시각을 함께 연결한다. 처음 생성된 개체의 어느 쪽이 앞인지 미리 단정하지 않고 이동 방향과 보이는 몸체를 비교한다.

같은 경기에서 이미 첫 대상이 선정됐다면 이후 재현은 새 경기를 사용한다. Factory/View 미준비나 identity 불일치이면 begin 로그가0일 수 있으며, 이 진단이 미초기화도 포착한다고 주장하지 않는다. 신호가 없거나 format-rejected/생략이 있으면 정상으로 판단하지 않고 선정/자료 경계를 확인한다. 그 뒤에는 일반 생산·건물 철거/파괴 후 경로 재개·재경기와 멀티 양쪽 역할의 전투 후 이동 복귀를 확인한다. 새 fixture나 기존 ROOT PASS만으로 전체 기능·기기 PASS를 대신하지 않는다.

당시 최종 범위는 기존8source+문서/meta13=21파일이며 커밋 전 snapshot은 `work / 572d94af` 기준 추가분 미커밋·미푸시 상태였다. 기존meta·원시로그·에셋·네트워크/GPU 수치와 Profiler 보류를 유지했다. 단일 방향 오류의 원인 확인, 멀티 캐시 교정의 실제 경기 효과는 위 재검증 뒤 판정한다.


## 6. 재검토 결과와 남은 한정 항목(2026-10-26 KST)

이 절은 로그 분석 마감 당시의 기록이다. 이후 적용한 한 가드 교정은7절을 따른다.

[Research8절](Research.md#8-re1-멀티-재검토--기존-복귀-실패와-종료-후-진입을-구분2026-10-26-kst)에 re1 두 멀티 run과 중복 prefix를 제외한 추가 SINGLE을 기록했다. 사용자 답변은 이번 싱글에서 방향 오류가 보이지 않았다는 것이며, Unit11의 제한 진단도 초기 회전 후 약0.5초의 두 번째 이동 샘플에서 Root/Visual/Mesh가 이동 방향과 정렬됐다. 이후는 정지 공격·사망이라 지속 이동이나 공격→걷기 전환을 검증한 것으로 확대하지 않는다. 이번 조건의 미재현으로 수용하고 원인 해결 완료·전기능 PASS를 선언하지 않는다.

기존 이동 adapter 실패는 두 Android Host run에서0이고 peer ROOT4종료는 PASS였으나 첫 경기의 MOVE FAIL은 포기 종료 후 같은 frame에서 닫힌 native owner에 전투 진입한 handoff2건이다. `_combatStopped`가 없는 이벤트 입구의 종료 수명 경계를 별도 후속 교정 후보로 남긴다. 정상 진행의 전투/이동 실패 또는 이전 cache 오류가 재발한 것으로 묶지 않는다. 실제 빌드 SHA·별도 컴파일/자동 검사 결과는 아직 미확인이다.

이번에는 기존 Research/Plan2파일만 갱신하며 source/asset/Profiler/원시 로그는 변경하지 않는다. 앞5절의 미커밋21파일·실기 미확인 설명은 당시 구현 마감 snapshot으로 보존하고, 현재 소스 기준은8b9605af와 위 새 관측이다. 추가 측정이나 끝없는 진단 확장을 바로 시작하지 않으며 Profiler 보류를 유지한다.

## 7. 종료 뒤 진입 교정 적용과 남은 확인(2026-10-26 KST)

메인의1.4 검토/GO 뒤 NCC의 이벤트 입구 가드 한 곳과 설명 두 줄을 적용했다. 종료된 경기에서 타겟/표현을 다시 등록하기 전에 반환하며 기존 spawn/server 보호와 정상 경기 처리, 새 경기의 초기화는 유지한다. 기존 C#1+Task2문서가 미커밋·미푸시 상태다. [Research9절](Research.md#9-main-동기화-확인과-종료-후-전투-진입-가드-교정2026-10-26-kst)에 실제 적용 범위와 메인 독립 정적 대조를 기록했다.

main fetch/merge 확인 결과는 추가 변경 없는 `Already up to date.`였고 사용자의 「없으면 패스해」 답변에 따라 진행했다. 충돌을 수동 해결하거나 새 main 변경을 통합한 것은 아니다. Unity 컴파일·suite·새 빌드/기기 재검증은 실행하지 않았다. 후속 실기 확인은 포기/정상 승패 뒤 재진입 실패가 없는지와 재경기에서 정상 전투가 시작되는지이며, 실제 확인 전까지 전체 해결 PASS로 판정하지 않는다. 싱글은 이번 미재현과 원인 미확정의 한계를 유지하고 Profiler 최적화는 계속 보류한다.

## 8. 남은 수정 항목의 통합 검증 준비(2026-10-26 KST, 구현 전 계획)

사용자가 「패스되지않은 항목 정리해서 로그 추가하고 테스트 할수있도록 절차안내해」라고 요청했다. 이미 사용자가 확인한 당시 컴파일·일반 플레이의 수용은 유지하고, 수정별 특수 조건에서 부족한 실행 증거를 채운다. 대상은 건물 제거 후 경로, 종료 뒤 전투 진입, 예약 FIFO, 연구 요청, 생산 요청·음수 취소, 생산 UI 입력·구독, Bootstrap 구독 해제, 결과 화면 최신 통신 재판정, 로그 파일명 소진, 검증 도구 정정의 열 항목이다. 인증·경제 등 전체 리팩토링을 다시 검증하는 범위로 늘리지 않으며, AI 생산배율·VFX 외부 작업·최적화는 제외한다.

### 8.1 Editor 집중 검사 진입점과 실제 경계 검사

메인이 전문 제안을 선정했다. 신규 [RunUnitActionSelfValidation.RefactoringVerification.cs](../../../../../Editor/Tools/Combat/RunUnitActionSelfValidation.RefactoringVerification.cs)와 대응 meta만 추가해 기존 `RunUnitActionSelfValidation` partial에 연결한다. 기존 전체 검사 본문이나 메뉴를 바꾸지 않고 `Hexiege > Combat > Diagnostics > Run Refactoring Verification`을 제공한다. Play 중·Play 전환 대기·활성 네트워크·등록된 경기 서비스가 있으면 실행하지 않고 `NOT_RUN`으로 기록하며 PASS를 출력하지 않는다.

공유 case 실행기는 실제 검사별 시작·결과와 마지막 요약을 `[REFACTOR-VERIFY]`로 기록한다. 로그는 `Generation=refactor-verify-v1`, 매 실행의 새 Run GUID, Case, Status를 포함하며 한 줄 UTF-8 예산900바이트를 지킨다. 이 Generation은 진단 포맷/검사 세대이지 Git SHA 검증이 아니다. 전체 case가 실제 실행되고 성공한 경우만 전체 PASS이며, 실행하지 않은 case나 대상 조건 미충족을 오류0으로 바꾸지 않는다. 결과·로그 출력은 기존 로거 체계를 사용하고 원시 로그 보관 정책을 새로 만들지 않는다. Play를 멈춘 Editor에서는 sink가 아직 열리지 않아 Console fallback만 남을 수 있으므로 새 로그 세션을 강제로 열지 않는다. 사용자는 이번 Run의 Console receipt만 별도 `RefactoringVerification.txt`로 복사·공유하며 전체 Editor.log는 요구하지 않는다.

| 실제 실행 대상 | 확인할 계약과 근거 |
|---|---|
| `UnitAttackShadowCoordinator`의 준비·일정·완료·Retire·Clear와 같은 ID 재등록·상한512 | 실제 공개 동작을 호출하고 살아 있는 예약 dictionary와 LinkedList의1:1 및 정상 FIFO 순서를 대조한다. 조건식을 복제하는 테스트를 만들지 않는다. [전투 동기화 규칙](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 「8. Animation Event와 표현」 NET-PRESENT-003 및 회차 수명·중복 방지 계약을 보존한다. |
| `LogcatCapture`의 실제 파일명 helper | 임시 폴더에서 기본 이름부터999까지 점유한 뒤 실제 helper가 IOException으로 중단하고 기존 파일 bytes를 보존하는지 확인한다. finally에서 이 검사 전용 임시 파일만 정리한다. [LogRules](../../../LogRules.md) §1.12의 덮어쓰기 금지와 §1.10의 원본 보존이 근거이며 사용자 raw는 열거나 지우지 않는다. |
| RootPose의 실제 이동 단계 parser | 정상 정의는 수용하고 정의되지 않은 숫자 및 Invalid는 거부하는지 확인한다. [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) U-MOV-PHASE와 기존 감사기의 증거 분류를 유지한다. |
| 기존 건물 제거 수명 fixture | 기존 UseCase·FlowField·이벤트 기반 검사를 실제 호출한다. 위1.2의 Died 시점 조회 보존, 제거 완료 뒤 경로 갱신, 반복 삭제와 계속 차단되는 지형을 검증한다. 근거는 U-MOV-REPATH와 이동 규칙45다. |
| 기존 Bloom writer·network fixture | 기존 실제 역할 상태 인수와 종료 시 복원을 포함한 검사를 호출한다. NET-AUTH-001의 서버 권위·클라이언트 표현 분리를 검증하고 Bloom/VFX 제품 동작은 변경하지 않는다. |

이 메뉴는 별도 runtime 검증 partial의 `RunRefactoringRuntimeOfflineCases()`도 호출한다. 해당 파일·fixture·개발 로그의 정확 범위는 전문 제안 확인 뒤8.2에 코드 전에 기록한다. 8.1은 준비한 검사의 실행 PASS가 아니며 메인 전체 검토/GO 뒤 구현한다. 신규 meta GUID의 유일성, 기존 코드·meta·에셋·raw 보존과 실제 컴파일/검사 실행 여부를 분리해 남긴다.

### 8.2 실제 실행 경로의 제한 진단과 Editor 전용 통제 검사

메인이 전문 제안의 다음 범위를 선정했다. 기존 C#8파일에 진단을 추가하고 신규 Editor partial [RunUnitActionSelfValidation.VerificationRuntime.cs](../../../../../Editor/Tools/Combat/RunUnitActionSelfValidation.VerificationRuntime.cs)와 meta를 추가한다. 아래 내용은 메인 검토/GO 전의 코드 전 계약이다. 기존 종료 후 진입 가드와 Task 기록을 보존하며 제품 기능·밸런스·네트워크 주기·VFX 정책을 변경하지 않는다.

| 정확 기존 파일 | 허용 진단·보존 범위 |
|---|---|
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) | spawn의 진단 세대/초기화, 종료 receipt, 실제 `_combatStopped` 거부 지점의 누계와 최대4줄을 기록한다. 서비스 조회·타겟·공격 표현 변경보다 앞서 반환하는 기존 교정을 유지한다. |
| [GameBootstrapper.Network.cs](../../../../Scripts/Bootstrap/GameBootstrapper.Network.cs), [GameBootstrapper.cs](../../../../Scripts/Bootstrap/GameBootstrapper.cs) | 재경로 구독 bind와 destroy 시 Dispose/null을 기록한다. 제거 완료 이벤트에서 실제 타일/건물 인덱스 상태 근거를 최대16줄 기록한다. 구독·경로·객체 수명 순서는 바꾸지 않는다. |
| [ProductionPanelUI.cs](../../../../Scripts/Presentation/UI/ProductionPanelUI.cs) | 실제 초기화/최초 bind, 큐·자원 콜백 횟수, 누른 상태 reset의 근거를 최대32줄 기록한다. UI 입력·자동생산 정책은 유지하며 로그를 위해 새 입력을 만들지 않는다. |
| [ProductionTicker.cs](../../../../Scripts/Presentation/Production/ProductionTicker.cs) | 실제 Composite 구독 수의 bind·dispose 근거를 기록한다. 생산 완료·랠리·이벤트 순서를 유지한다. |
| [NetworkProductionController.Requests.cs](../../../../Scripts/Infrastructure/Network/NetworkProductionController.Requests.cs) | 새 소유권/음수 인덱스 guard에서 이유·거부 누계를 기록하고 상세 출력은 최대24줄로 제한한다. 기존 정상 요청 동작·골드·환불·전송을 바꾸지 않는다. |
| [NetworkUpgradeController.cs](../../../../Scripts/Infrastructure/Network/NetworkUpgradeController.cs) | 실제 연구소 guard의 거부 이유·누계를 기록하고 상세는 최대16줄로 제한한다. 정상 연구·취소·파괴 환불과 서버 권위는 유지한다. |
| [NetworkGameEndController.cs](../../../../Scripts/Infrastructure/Network/NetworkGameEndController.cs) | 실제 도달 확인 대기 뒤 최신 신호/수명 변경으로 판정을 중단한 근거를 최대4줄 기록한다. Editor 전용 private `Func<Task<InternetReachabilityResult>>`를 실제 task 선택 위치에만 두어 검사에서 대기 task를 공급한다. 실행 시계·30초 판정·도달 결과 처리·연결 종료 정책은 유지한다. |

진단 상태와 호출은 모두 `UNITY_EDITOR || DEVELOPMENT_BUILD` 안에 둔다. HB task 공급 seam과 통제 검사는 `UNITY_EDITOR` 전용이며 Release나 Android 개발 빌드에 부정 요청을 실행하는 진입점을 제공하지 않는다. 공통 로그는 `[REFACTOR-VERIFY] Generation=refactor-verify-v1`을 사용하고 줄 예산900바이트를 유지한다. 누계를 오류0이나 PASS의 대용으로 삼지 않으며, 실제 이벤트가 없으면 미발생으로 구분한다. 진단 세대는 Git SHA 인증이 아니다.

새 runtime 검증 partial은8.1 집중 메뉴에서 호출할 오프라인 UI 검사를 제공한다. 패널을 실제 초기화 두 번 한 뒤 실제 Subject를 발행해 `UpdateUI`/`UpdateInfoBar` refresh 누계가 한 번만 증가하고, 파괴 뒤 다시 발행해도 누계가 증가하지 않는지 확인한다. Ticker도 실제 초기화 두 번 뒤 Composite.Count가8이며 파괴 뒤 IsDisposed인지 대조한다. 누른 상태에서 비활성화/늦은 손 떼기가 실제 reset·입력 거부 경로를 거쳐 추가 동작을 만들지 않는지도 검사한다. 활성 경기 서비스와 네트워크가 없는 환경에서만 임시 객체를 사용하고 finally로 구독·객체를 정리한다.

오프라인 수명 검사의 전문 후속 구체안에는 Bootstrap의 실제 bind 두 번→구독2개/배치·제거 이벤트 각1회 수신→실제 OnDestroy 정리 본문→이후 이벤트 비전달도 포함한다. 임시 비활성 객체이므로 Unity가 자동으로 OnDestroy를 호출했다고 주장하지 않고 해당 production 정리 본문을 직접 실행한 검사로 기록한다. 공통 메뉴의 추가 case는 `EDITOR-UI-LIFETIME`, `EDITOR-TICKER-LIFETIME`, `EDITOR-BOOTSTRAP-LIFETIME`이다.

멀티 특수 조건은 Editor Host의 실제 전용 테스트 경기에서 다음 메뉴로 실행한다. 임의 RPC를 사용자에게 직접 작성하게 하지 않고 실제 공개 요청과 실제 게임 상태를 통제해 검사한다. 메뉴는 실행 전제를 확인하며 전제가 없으면 `NOT_EXERCISED` 또는 `NOT_RUN`으로 기록한다. case가0개 실행된 경우 전체 PASS를 출력하지 않는다.

| 메뉴 (`Hexiege > Combat > Verification` 아래) | 실제 경계·판정과 격리 |
|---|---|
| `Run Server Request Checks` | 서버가 spawn/listen 중이고 **양 팀의 생산건물에 실제 생산이 진행 중이며 양 팀 연구소에도 실제 연구가 진행 중**이어야 한다. 검사 전에는 전투하지 않고 랠리를 자기 기지 근처로 두며, 생산 대기를 여러 개 준비하고 연구를 마지막에 시작한 뒤 바로 메뉴를 실행한다. 실제 NGO 공개 요청을 호출해 새 guard의 거부 누계 증가와 골드·큐·랠리·자동생산·연구 상태의 전후 불변을 함께 확인한다. 생산 타팀4요청·자기팀 음수취소2건·연구의 타팀/비연구소/없는ID 착수와취소6건인12개 부정 요청을 검사한다. `Time.timeScale=0`의 짧은 검사 트랜잭션을 사용하며 각case 최대3초,12case 전체 최대36초이고 정상 응답에서는 수초 내 끝난다. 첫 실패에서 중단하고 모든 종료·실패에서 원래 시계와 검사 상태를 finally로 복구한다. Play 종료/domain reload에서도 시계를 복원하고 Editor update 구독을 해제한다. 일반 권한 요청의 정상 동작은 사용자 플레이와 기존 로그로 별도 확인한다. |
| `Run Ended Combat Check` | spawn된 서버의 종료된 경기에서 살아 있는 유닛과 타겟이 있어야 한다. 실제 전투 진입 이벤트 handler에 도달시켜 거부 누계가1증가하고 타겟 dictionary·revision·epoch가 바뀌지 않는지 확인한다. 이미 닫힌 native 수명을 열거나 전투를 재개하지 않는다. |
| `Run Result Heartbeat Check` | 실제 결과 화면 감시가 활성 상태여야 한다. 실제 감시 enumerator에 미완료 task를 공급하고 실제 상대 신호 수신을 진행한 뒤 Reachable로 완료해, 최신 신호 재검사에서 연결/이탈 flags와 이벤트가 유지되는지 확인한다. finally에서 공급 seam·필드·enumerator 상태를 복구한다. 오래된 신호를 이탈 확정·연결 종료시키는 별도 분기는 이번 통제 검사에서 실행하지 않는다. |

규칙 근거는 연구의 [Upgrade](../../../GameSystemRules/GameSystemRules_Upgrade.md) 「유닛 강화 시스템 규칙」8·9의 서버 권위/파괴 환불, 생산의 [BuildingPanelsUI](../../../GameSystemRules/GameSystemRules_BuildingPanelsUI.md) 「생산 패널 UI」1~8·20·23·24의 입력·큐·환불·대기 계약, 네트워크의 [MatchFlow](../../../GameSystemRules/GameSystemRules_MatchFlow.md) 규칙17의 최신 응답에 따른 이탈 판정이다. 서버 요청 검증은 [네트워크 TDD](../../../TechnicalDesignDocument_Network.md)의 치팅 방어 계약도 따른다. 건물 제거·종료 뒤 공격·진단 원칙은8.1의 U-MOV-REPATH/NET-AUTH-001/NET-PRESENT-003과 [LogRules](../../../LogRules.md) §1.2·1.6·1.7에 근거한다.

정적 확인에서는 Release에서 진단 상태/호출과 Editor seam이 선택되지 않는지, 새 진입점이 Editor에만 있는지, 원래 수치·RPC/API/직렬화·기존meta가 보존됐는지 대조한다. 준비한 fixture의 source 검토를 실제 Unity 컴파일·NGO ILPP·기기 실행 PASS로 기록하지 않는다. 구현 뒤 [Testcase.md](Testcase.md)에 최신 세대 컴파일→집중 메뉴→싱글→멀티 통제 검사/종료·재경기→로그 수집 순서와 항목별 기대 증거를 기록한다.

전문 후속 구체화에서 서버 요청 검사의 전제를 **실제 생산/연구 진행 중**으로 좁혔다. 취소 대상이 없는 상태의 무변경만 확인하지 않기 위한 강화이며, 실제 진행이 없으면 `NOT_RUN`이다. 초기 제안의3초는12개 전체가 아니라 개별 요청 대기의 상한으로 확정했다. 현재 표와 사용자 절차에 이 전제·총상한을 함께 반영한다.

## 9. 검증 준비 적용 결과와 사용자 실행 인계(2026-10-26 KST)

8절의 기존 runtime8파일·신규 Editor partial2개와 대응meta를 적용하고 [Testcase.md](Testcase.md) 및 meta를 추가했다. 기존 Task2와 합한16경로가 미커밋·미푸시 상태다. 전문 QA의 추가 blocker 없음과 메인 문법/Release 조건부 선택·문서/diff 정적 대조 완료를 확인했으나 실제 Unity 컴파일·집중 검사·NGO/기기 실행은 미실행이다. 새 source 준비를 기능 PASS로 기록하지 않고 Testcase의 항목별 실행·조건·결과를 받아 판정한다.

원격 main2750decd는 메인이 충돌 없이 병합했고 현재 HEAD는e312feac다. 메인 전달의 병합 전후16경로 bytes 보존 결과와 새 main 변경의 한계는 [Research11](Research.md#11-검증-준비-구현과-main-후속-병합2026-10-26-kst)에 기록했다. VFX·AI 생산배율·최적화의 보류와 기존 사용자 확인은 유지한다.


## 10. main 통합 이후의 검증 준비 유지(2026-10-26 KST)

메인이 main `f367cb52`를 병합한 HEAD는`562888d2`이며 기존 검증 준비16경로를 복원했다. 기존 runtime8 진단 patch와 원래 파일 보존, 양쪽 source·문서 충돌의 의미별 결합은 [Research12](Research.md#12-main-vfx투사체-통합과-검증-준비-보존2026-10-26-kst)에 기록했다. 이 통합에는 main의 VFX·서버 투사체 변경이 포함되며9절의 이전 병합 범위와 구분한다.

8.2 서버 요청 검사는 새 main의 권위 투사체/파도가 진행 중이지 않아야 한다. Editor 메뉴의 실제 `CountActiveProjectiles()==0`·`_torrentLaunchCount==0` 읽기 전제만 보강하고, 제품 공격 tick·시간·피해는 변경하지 않는다. 전제가 없으면 검사하지 않고, 사용자 절차는 무전투·자기 기지 랠리·비행 없음 상태에서 진행 중 생산/연구를 준비하도록 유지한다.

메인의 선택 source 문법123개 오류0·기존 진단 patch/비충돌 main 보존 대조·QA 추가 blocker 없음과 문서/diff 검사 결과는 정적 근거다. 실제 Unity 컴파일·NGO/기기 실행은 미실행이며 진단 준비16경로는 미커밋·미푸시다. [Testcase](Testcase.md)에 통합 버전의 새 빌드와 로그 ZIP 생성 메뉴, 자동 포함되지 않는 별도 Console receipt 전달을 반영했다. AI 생산배율·최적화 보류와 기존 사용자 확인은 유지하고 새 VFX 전체 수용을 선언하지 않는다.
