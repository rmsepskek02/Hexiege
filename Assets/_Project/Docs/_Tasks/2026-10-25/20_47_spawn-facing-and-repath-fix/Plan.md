# 생산 방향·전투 복귀 문제 우선 교정 계획

최적화에 앞서 사용자가 발견한 방향 문제와 저장 로그의 이동 복귀 실패를 분리해서 교정한다. 증거가 부족한 부분은 원인을 가르는 최소 관측부터 준비하고, 확인된 코드 문제만 좁게 수정한다. 이번 문서는 수정 범위·보존할 계약과 검증 순서를 관리한다.

> 1.2는 건물 사망 알림을 유지하고, 경로 캐시 무효화/즉시 재경로 구독 두 곳만 **상태 제거 완료 뒤의 새 이벤트**로 옮기는 교정안이다. 사망 처리·조회·환불·게임 종료 기능을 폐기하지 않는다. 기존 로직의 최종 삭제나 상시 진단/VFX 제거를 확정하지 않으며 메인 전체 계획 검토/GO 전 source는 수정하지 않는다.

- 조사 원본: [Research.md](Research.md).
- 기준: `work / 572d94af`, Unity6000.3.5f2.
- 현재 상태(2026-10-26 KST): 메인 GO 뒤1.1 진단3source/1.2 교정·기존회귀5source를 적용·전문 정적 대조했다. 메인 독립 대조·QA 정적 검토도 마쳤지만 Unity·기기 확인은 미실행이며 Profiler 최적화는 계속 보류다.

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

최종 범위는 기존8source+문서/meta13=21파일이며 현재 `work / 572d94af` 이후 추가분은 미커밋·미푸시다. 기존meta·원시로그·에셋·네트워크/GPU 수치와 Profiler 보류를 유지했다. 단일 방향 오류의 원인 확인, 멀티 캐시 교정의 실제 경기 효과는 위 재검증 뒤 판정한다.
