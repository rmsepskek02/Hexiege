# Android 30 FPS 측정 기반 최적화 계획

실제 Android 앱에서 어느 처리가 프레임을 오래 쓰는지 확인할 수 있도록 측정 구간을 준비하고, 사용자 승인 범위의 반복 할당을 좁게 정리한다. 기능·게임 수치를 유지하면서 변경 전후를 같은 조건에서 비교한다. 코드에서 할당을 줄였다는 사실과 실제 기기의 성능 개선은 구분한다.

> 기존 동작 코드의 폐기·비활성화·최종 삭제는 없다. 초기의 「기준선 전 컬렉션 재사용 금지」는 재개 승인으로 좁은 반복 할당 정리부터 가능하도록 변경했다. 기존 계산·콜백·권위·네트워크 설정·에셋을 유지하며 검색 알고리즘 변경·GPU 품질 하향은 하지 않는다. 재사용의 정확 소유권과 중첩 호출·예외 정리는 전문안을 코드 전에 기록하고 메인이 검토한다.

- 조사 원본: [Research.md](Research.md).
- 재개 기준: clean `work / 7c00f768`, Unity6000.3.5f2, Android30 FPS(약33.3ms/프레임), 최소 기기 미정. 최초572d94af와 아래1.1의 제안은 당시 이력이다.
- 상태(2026-10-26 KST 갱신 시점): Plan1.2의5파일/8marker와 NCC3컬렉션 재사용 및 정적 대조를 마쳤다. 계측만 있는 기준을070ddcaf로 로컬 커밋했고 버퍼/인계 commit·최신 main 통합·게시는 대기다. 다른 환경에서는 [Handoff](Handoff.md)·두patch·manifest와 최종 게시 결과로 실제 source 상태를 확인한다. 실제 Unity 컴파일·전후 캡처는 미실행이며 기능/리팩토링의 남은 계약 검증은 최적화 묶음 뒤 함께 진행한다.

## 1. 보존할 규칙과 첫 범위

[GameSystemRules](../../../GameSystemRules.md) 인덱스를 읽고 아래 원본을 대조했다.

| 작업 | 근거·보존 |
|---|---|
| 전투 시간·탐색 비용 계측 | [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 「이동 규칙」 규칙6의 월드 좌표 사거리 판정 유지 |
| 서버 전투 틱 계측 | [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001·NET-AUTH-002·NET-ROOT-001의 서버 결과/Simulation Root·표현 예약 경계 유지 |
| 계측의 레이어 배치 | [기술 설계](../../../TechnicalDesignDocument.md)의 의존성 방향 추상화와 실제 Application 예외 범위를 검토해 전문 설계로 확정. 새 역방향 의존을 만들지 않음 |

조건부 `DEVELOPMENT_BUILD || UNITY_EDITOR` 계측으로 한정한다. 고정 문자열 marker와 scope 수명으로 동기 메서드의 시간만 측정하며, 코루틴 yield/await를 가로지르는 scope를 만들지 않는다. Application/Domain은 수정하지 않고 Infrastructure/Presentation caller에서만 `Unity.Profiling.ProfilerMarker`를 사용한다. using·static readonly 필드·scope는 모두 같은 guard로 감싸 Release에 계측 SDK 참조와 호출이 남지 않게 한다.

### 1.1 전문 정확안 — 아직 구현하지 않음

아래는2026-10-25의 미구현 초기 제안이다. 당시 전문 설계 `/tmp/hexiege-profiler-markers-20261025/design.json`을 문서 담당이 읽고 기록했으며, 현재 재개 범위는 전문안 확인 뒤 별도로 추가한다. 과거 tmp 경로의 존재나 예전 제안을 현재 구현/GO의 증거로 사용하지 않는다.

| 파일 | 고정 marker·정확 scope |
|---|---|
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) | `Hexiege.NetworkCombat.TickCombat`: TickCombat 입구부터 모든 동기 return/예외까지 Auto scope. `Hexiege.NetworkCombat.TargetQueries`: 기존 공격 대상/감지 질의 두 식만 감싸며 short circuit·평가 순서 유지. 기존 `var attackResult`/`bool hasEnemy`는 블록 밖에 같은 nullable tuple `(int id, bool isUnit)?`/bool로 선언하고 기존 RHS를 원순서대로 대입한다. |
| [ProductionTicker.cs](../../../../Scripts/Presentation/Production/ProductionTicker.cs) | `Hexiege.Economy.TickIncome`: 기존 의존성 nonnull if 안의 TickIncome 호출만 감싸며 서버/클라이언트 가드를 유지한다. |
| [UnitView.Movement.cs](../../../../Scripts/Presentation/Unit/UnitView.Movement.cs) | `Hexiege.UnitView.ShouldEngage`, `Hexiege.UnitView.ShouldEngageAt`: 두 동기 bool 메서드 입구부터 종료까지 scope. 기존 생존/의존성 guard와 힐러 분기를 포함한다. |

TickCombat 전체에는 타워·연구·상태 처리와 중첩 콜백이 포함되므로 공격 검색만의 시간으로 읽지 않는다. TargetQueries는 해당 caller의 두 질의만, UnitView scope는 두 진입 API만 포함하므로 전체 전투 검색의 완전한 호출 횟수나 비용을 뜻하지 않는다. 동적 marker 이름·추가 런타임 로그·ProfilerRecorder·자동 캡처 도구를 만들지 않는다. 계측 전후에는 동일 marker 세대/빌드 옵션을 사용한다.

정적 확인은 계측 guard 블록을 제거하고 질의 블록의 선언/대입을 원문 두 식으로 역복원한 bytes 동일, 기존 meta·다른 source hash 보존이다. 새 tuple 선언/블록은 Release에서도 남지만 원식의 타입/평가/사용 위치를 유지한다. 이 확인은 Unity 컴파일이나 실제 marker 표시 확인을 대신하지 않는다.

### 1.2 재개 정확안 — 고정 marker8개와 전투 틱 컬렉션3개 재사용

2026-10-26 사용자 재개 승인에 따른 전문 정확안을 기록한다. 기존 C#5파일만 변경하고 신규 source/meta·Application/Domain 의존성·에셋·품질·전투/네트워크 수치는 바꾸지 않는다. 아래8개 marker의 using·static readonly 필드·scope는 모두 `UNITY_EDITOR || DEVELOPMENT_BUILD`로 감싼다. 동기 구간만 측정하고 yield/await를 건너지 않는다. 메인 전체 계획 검토/GO 전에 source를 변경하지 않는다.

| 정확 파일 | 고정 이름과 범위 |
|---|---|
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) | `Hexiege.NetworkCombat.TickCombat`: 메서드 입구부터 동기 종료까지, 타워/상태/연구 등 기존 부수 처리 포함. `Hexiege.NetworkCombat.TargetQueries`: 기존 공격 대상/감지 질의 RHS 두 곳만 감싸고 nullable tuple/bool 외부 선언과 short circuit·평가 순서를 유지. `Hexiege.NetworkCombat.TickProjectiles`: 기존 Host/서버 Update의 투사체 tick 호출 구간. 싱글 Bootstrap의 직접 tick은 이 marker 밖이다. |
| [ProductionTicker.cs](../../../../Scripts/Presentation/Production/ProductionTicker.cs) | `Hexiege.Economy.TickIncome`: 기존 nonnull·권위 guard 안의 수입 tick 호출 구간. |
| [UnitView.Movement.cs](../../../../Scripts/Presentation/Unit/UnitView.Movement.cs) | `Hexiege.UnitView.ShouldEngage`, `Hexiege.UnitView.ShouldEngageAt`: 해당 동기 메서드 입구부터 종료까지, 기존 guard/힐러 분기 등 포함. 전체 타겟 검색의 전 호출을 대표하지 않는다. |
| [ProjectilePresentationPresenter.cs](../../../../Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs) | `Hexiege.ProjectileView.Update`: 실제 Update의 입구부터 동기 종료까지. epoch/시계/표현 관리 부수 작업도 포함하며 피해 계산 비용으로 읽지 않는다. |
| [GameBootstrapper.Network.cs](../../../../Scripts/Bootstrap/GameBootstrapper.Network.cs) | `Hexiege.Bootstrap.RepathAllAliveUnits`: 전역 재경로 요청 메서드의 동기 입구부터 종료까지. 모든 개별 유닛/경로 질의의 전체 호출 수를 뜻하지 않는다. |

NCC의 매 전투 틱에서 새로 만드는 세 컬렉션만 controller 인스턴스 소유의 readonly `List<int>`·`HashSet<int>`·`List<int>` scratch와 사용 중 여부 bool로 재사용한다. 버퍼 선택은 타워/상태/스킬 등의 기존 콜백을 모두 실행한 **기존 key snapshot 위치**에서 한다. 공유 버퍼가 비어 있으면 그 호출이 소유권을 잡고, 중첩 호출이 이미 사용 중인 버퍼를 만나면 기존처럼 독립적인 새 세 컬렉션을 사용한다. 콜백 재진입이 바깥 순회 자료를 덮어쓰지 않게 하는 경계다.

선택 뒤 기존 key snapshot 시점에 Clear/AddRange로 복사하고 같은 foreach·판정·제거 본문을 실행한다. `toRemove.Clear()`는 원래 새 제거 목록을 만들던 위치에 두어 순서를 유지한다. 선택과 처리 전체를 try/finally로 감싸 예외에서도 세 버퍼를 Clear하며, 공유 소유자만 사용 중 표시를 해제한다. spawn/despawn 같은 수명 콜백에서 사용 중 버퍼를 강제로 비우지 않는다. 공개 API·대상 선택·snapshot 시점·중복 방지·사망/제거·RPC·권위 시계와 본문 순서는 유지한다.

공유 버퍼의 배열 용량은 controller 수명 동안 남는다. 처음 용량이 늘 때나 중첩 fallback은 할당할 수 있고 HashSet.Clear의 CPU 비용도 남으므로0B/프레임·총 RAM 감소·속도 개선을 약속하지 않는다. 이번 목적은 동일 동작의 반복 할당을 줄이는 것이며 실제 CPU/GC/메모리 비용은 Profiler 전후 비교로 판정한다. 근거는 [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001·NET-AUTH-002의 서버 판정/결과 순서와 [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 「이동 규칙」6의 기존 거리·타겟 판정, U-MOV-REPATH의 재경로 계약 보존이다.

구현은 **marker만 먼저 적용 → 메인이 계측만 있는 변경 전 source snapshot/patch를 보관 → 컬렉션 재사용 적용** 순서다. marker별 전후 시간은 동일 marker가 있는 두 버전으로 비교한다. 원본7c00f768만으로 전체 프레임·GC를 비교할 수는 있지만 marker가 없는 원본을 marker 내부 시간의 기준선으로 사용하지 않는다. 지금 사용자에게 소스를 되돌리거나 캡처하도록 요구하지 않고, 후속 두 빌드 측정 안내 때 같은 계측과 설정을 유지한다.

정적 대조는 marker-only 변환의 원문 복원·guard/8개 고정 문자열·기존meta와 다른 source 보존을 먼저 확인하고, 이어 scratch의 콜백 재진입/예외 소유권·snapshot/제거 순서 보존을 대조한다. 실제 Unity 컴파일·Profiler 표시·Android 성능·기능 PASS와 구분한다.

## 2. 구현 순서

1. 전문 프로그래머의 재개 정확안을1.2에 기록하고 메인이 전체 계획·소유권·보존 근거를 검토한다.
2. 메인 GO 뒤 marker8개만 먼저 적용하고 원문/meta·guard를 대조한다. 메인은 이 계측만 있는 변경 전 snapshot/patch를 보관한다.
3. 같은 코드에 전투 틱 scratch 재사용만 적용하고 콜백 재진입·예외·순회/제거 순서와 다른 기능 보존을 대조한다.
4. 최적화 묶음 뒤 사용자 PC에서 컴파일·기능 테스트를 한 번에 진행한다. 기능 검증은 기존 통합 절차를 사용하고 이번에 같은 일반 동작을 묶음마다 반복 요청하지 않는다.
5. 성능 비교는 같은 marker가 있는 변경 전/후 두 빌드로 후속 진행할 수 있다. 실제 캡처가 없으면 개선량·30 FPS 달성을 미확인으로 유지하며, 다음 큰 탐색/렌더 최적화는 해당 캡처에서 근거를 확인한 뒤 선정한다.

## 3. 사용자 PC의 Profiler 사용과 캡처 절차

**지금 바로 측정할 필요는 없다.** 코드를 준비하는 동안에는 아래 설명을 읽어 두면 되고, 최적화 묶음 뒤 기능 테스트와 성능 캡처를 진행한다. 성능의 전후 비교를 위해서는 변경 전/후 두 빌드가 필요하다. 메인이1.2의 계측만 있는 기준을 보관하므로 사용자가 지금 임의로 소스를 되돌릴 필요는 없다.

Profiler는 앱이 한 프레임을 그리는 동안 CPU가 어떤 함수를 실행했는지, GPU가 그림을 처리하는 데 얼마나 걸렸는지, 새 메모리를 얼마나 할당했는지 보는 도구다. 게임 로그가 「생산/공격이 언제 일어났는지」를 보여 준다면 Profiler는 「그 처리가 프레임에서 시간을 얼마나 썼는지」를 보여 준다. 서로 다른 자료이므로 RuntimeLog만으로 CPU/GPU 병목이나30 FPS를 판정하지 않는다.

### 3.1 Android 앱에 연결하기

1. 사용할 빌드의 소스가 컴파일되는지 확인한다. 오류가 있으면 그 내용을 먼저 전달하고 기기 성능 판정으로 넘어가지 않는다.
2. Android Build Profile에서 **Development Build**와 **Autoconnect Profiler**를 켜고 빌드/설치한다. **Deep Profiling은 끈다.** 전체 함수를 자세히 계측하는 Deep Profiling은 실행 비용을 크게 늘릴 수 있어 첫 비교에는 사용하지 않는다.
3. USB 디버깅이 가능한 Android 기기를 연결하고 앱을 실행한다. Unity에서 **Window > Analysis > Profiler**를 연다.
4. Profiler 상단의 대상이 **Editor가 아니라 Android에서 실행 중인 게임 앱**인지 확인한다. Editor를 측정하면 Inspector·에디터 UI·편집기 작업이 섞인다. 자동 연결되지 않으면 대상 플레이어 목록에서 해당 Android 앱을 선택한다.
5. CPU Usage와 Memory를 우선 켠다. 지원되는 환경에서 GPU/Rendering과 네트워크 통계를 함께 확인한다. 기기·그래픽 API·패키지가 GPU/네트워크 값을 제공하지 않으면 **미지원/미확인**으로 기록한다. 값이 없다는 것은 사용 시간이0이라는 뜻이 아니다.

**이번 NCC 컬렉션 최적화의 성능 비교는 Android Host + Editor Client로 한다.** 줄인 코드는 멀티 서버의 전투 틱에서 실행되므로 Android가 Client이면 해당 변경의 기기 CPU/GC 비용을 측정하지 못한다. Client에 TickCombat marker가 없거나 그 값이0이라고 최적화가 통과한 것으로 해석하지 않는다. 일반 게임 화면에 역할을 추가하는 대신 개발 로그로 실제 역할을 확인한다.

이번 비교에서 목표 FPS·해상도·렌더 품질·네트워크 주기는 새로 바꾸지 않는다. Profiler 연결 여부, Development 옵션, 진단 로그의 종류도 전후에 맞춘다. Development 빌드와 연결/계측 자체에도 비용이 있으므로, 이 결과를 출시본 성능으로 그대로 옮기지 않고 나중에 Release에서도30 FPS 지속과 체감을 따로 확인한다.

### 3.2 무엇을 기록할지

1. 같은 기기를 비슷하게 예열하고 충전 여부·발열·백그라운드 앱 조건을 맞춘다. 맵 로딩/초기 생성 구간과 실제 플레이 구간은 분리한다.
2. **유닛 적음 / 다수 이동 / 밀집 전투**의 세 장면을 각각 관찰한다. 맵·종족·유닛 종류와 대략 수·카메라 위치·역할을 적어 변경 전후에 가능한 한 같은 조건을 만든다. 이번 NCC 재사용 비교에서는 **두 버전 모두 Android Host + Editor Client**를 유지한다. Host와 Client는 수행하는 일이 달라 서로의 값을 전후 개선율로 비교하지 않는다.
3. Profiler의 Record를 켜고 안정된 장면을 약30초 관찰한 뒤 멈춘다. 기록 보관 프레임 상한 때문에30초 전체가 저장된다고 가정하지 않는다. 실제 저장된 프레임 범위를 확인하고, 필요한 길이가 보관되지 않으면 충분한 보관 범위를 설정하거나 짧은 캡처를 여러 번 저장한다.
4. CPU Usage에서 느린 프레임을 선택하고 **Timeline**으로 어느 스레드/처리가 길었는지 본다. **Hierarchy** 검색에 `Hexiege`를 입력하면 이번에 넣은8개 구간을 찾을 수 있다. 해당 장면/역할에서 실행되지 않은 marker는 안 보일 수 있다. 예를 들어 서버 전투 marker가 Client에서 없다고0ms 또는 오류로 판정하지 않는다.
5. Profiler의 Save로 **원본 캡처 파일**을 저장한다. 이름에 변경 전/후와 장면·역할을 구분한다. 느린 프레임의 화면 캡처는 보조 자료로 함께 주면 되지만 원본 Profiler 캡처를 대신하지 않는다.
6. 전달할 때 기기 모델·Android 버전·빌드 리비전/변경 전후 구분·측정 설정·해상도/품질·발열/충전 상태·장면/유닛 수·실제 저장 프레임 범위를 함께 적는다. RuntimeLog와 Profiler 저장 파일은 별도로 보관한다. 기존 로그 ZIP 생성 도구는 RuntimeLog만 골라 담으므로 **Profiler 원본 캡처는 자동 포함되지 않으며 별도로 함께 전달**해야 한다.

할당이 생기는 호출 위치를 더 확인해야 할 때만 **Allocation Call Stacks**를 켜 짧게 추가 캡처한다. 이 옵션을 켠 자료는 기본 비용이 다르므로 옵션을 끈 기준선과 하나의 전후 수치로 섞지 않는다. 첫 단계부터 모든 상세 추적을 켤 필요는 없다.

### 3.3 화면의 숫자를 읽는 방법

| 표시 | 뜻과 볼 점 |
|---|---|
| 프레임 시간 |30 FPS의 프레임 예산은 약33.3ms다. 평균뿐 아니라 반복해서 길어지는 프레임·최악 구간·GC 끊김도 본다. 목표 프레임을 기다리는 시간은 실제 계산 시간과 구분한다. |
| CPU Timeline | 한 프레임에서 메인/렌더/작업 스레드가 일하거나 기다리는 구간을 보여 준다. 긴 막대가 계산인지, 다른 스레드·GPU·프레임 제한을 기다린 것인지 확인한다. |
| Total | 선택한 함수와 그 안에서 호출한 하위 함수까지 포함한 시간이다. TickCombat 안의 TargetQueries처럼 중첩된 구간을 서로 더하면 같은 시간을 중복 계산한다. |
| Self | 하위 함수 시간을 제외한 해당 구간 자체의 시간이다. Total이 크고 Self가 작으면 내부에서 호출한 다른 처리를 더 살펴본다. |
| Calls | 선택한 프레임에 해당 구간이 실행된 횟수다. 유닛이 늘면서 호출 수가 늘었는지, 호출 한 번이 오래 걸리는지를 구분할 때 쓴다. |
| GC Alloc | 선택한 프레임/구간에서 새로 만든 관리 메모리 양이다. **현재 보유 RAM이나 누수 크기가 아니다.** 재사용으로 매 프레임 할당을 줄여도 버퍼 용량을 보유하므로 전체 메모리가 반드시 줄지는 않는다. |
| GPU 시간·대기 | GPU가 처리하는 시간과 CPU가 GPU를 기다리는 흔적을 함께 본다. GPU 대기가 크다는 한 표시만으로 GPU 병목을 확정하지 않는다. 프레임 제한·동기화·다른 스레드 대기도 확인한다. |

CPU와 GPU는 파이프라인에서 겹쳐 일하므로 **CPU시간+GPU시간을 더해33.3ms와 비교하지 않는다.** CPU의 실제 작업, GPU 처리, 동기화/대기와 전체 프레임을 함께 본다. 예를 들어30 FPS 제한 때문에 CPU가 다음 프레임을 기다리는 것은 계산이33ms 걸렸다는 뜻이 아니다. 화면에30 FPS라고 떠도 순간 끊김·입력 지연·지속 발열 문제는 따로 확인한다.

### 3.4 변경 전후 비교와 기능 테스트를 묶는 방법

- marker별 비교는 **기준7c00f768에 같은8marker만 넣은 버전**과 **그 계측에 scratch 재사용까지 넣은 버전**으로 한다. 계측 없는 원본은 전체 프레임/GC 비교에는 사용할 수 있지만 marker 내부 시간의 변경 전 값으로 쓰지 않는다.
- 두 빌드는 같은 기기·장면·역할·설정·예열 조건으로 측정한다. 열이 오른 뒤 자료와 차가운 시작 자료를 한 쌍으로 비교하지 않는다.
- 기능 확인은 최적화 묶음 뒤 기존 [통합 테스트](../20_47_spawn-facing-and-repath-fix/Testcase.md)를 한 번 진행한다. 성능 비교를 위해 두 빌드를 나중에 연속 측정할 수 있으며, 지금 작업 도중 매 변경마다 테스트를 요청하지 않는다.
- 캡처가 아직 없으면 「반복 할당 정리 적용」과 「성능 효과 미측정」으로 기록한다. 실제로 개선됐는지,30 FPS가 유지되는지는 원본 캡처와 Release 확인 뒤 판정한다.

| 묶어서 진행할 확인 | 역할 구성 | 구분하는 이유 |
|---|---|---|
| 기존 통합 기능 검사·통제 메뉴 | Editor Host + Android Client | 기존 Editor 전용 서버 검사 메뉴의 실행 전제다. 이 절차를 변경하지 않는다. |
| 이번 NCC 최적화의 기기 성능 전후 비교 | Android Host + Editor Client, 두 빌드 동일 | 최적화한 서버 틱이 Android에서 실제 실행돼야 기기 CPU/GC 효과를 확인할 수 있다. |

두 구성을 최종 테스트 묶음에서 이어 진행할 수 있다. 이미 수용한 일반 조작을 매번 전부 반복할 필요는 없지만, 서로 다른 역할의 캡처를 같은 경로의 변경 전후 자료로 섞지 않는다.

## 4. 판정표

| 확인 축 | 변경 전 | 변경 후 | 판정 기준 |
|---|---|---|---|
| NCC 재사용의 실제 실행 역할 | 미수집 | 미수집 | 두 버전 모두 Android Host + Editor Client. Android Client의 미실행 marker/GC0은 이 변경의 효과 근거가 아님 |
| CPU 프레임/marker 시간·호출 수 | 미수집 | 미수집 | 동일 조건의 분포와 느린 프레임을 비교 |
| GC Alloc/GC 정지 | 미수집 | 미수집 | 반복 할당과 끊김 원인을 확인 |
| GPU/Rendering | 미수집 | 미수집 | 지원 환경의 GPU 비용과 배치 수 비교 |
| 네트워크 송수신 | 미수집 | 미수집 | 같은 역할·유닛 수에서 전송량과 처리 비용 비교 |
| Material/Texture/재경기 메모리 | 미수집 | 미수집 | 재경기 전후 객체 수/메모리와 수명 확인 |
| Android 30 FPS 지속/기능 회귀 | 미확인 | 미확인 | 실제 기기에서 프레임 예산과 기존 기능 확인 |

사용자 PC는 Unity와 Android USB 측정이 가능하다고 답했다. 현재 작업 환경에서는 Unity·adb·기기 Profiler 접근 수단을 확인하지 못했다. 계측 source 준비와 정적 대조까지 진행하고, 실제 기기 기준선 없이는 최적화 효과를 주장하거나 임의의 전투/렌더 수치를 조정하지 않는다.

## 5. 파일·검사 경계

- 문서: 첫 구현에서는 기존 Research/Plan2개만 갱신했다. 이후 사용자 인계 요청에 따른 같은 Task의4파일/meta 추가와 기존 기능 Testcase 현재 문단 정정은7절이다. 상시 문서·메모리·옛 Research/Plan은 변경하지 않는다.
- source: 현행 정확안은1.2의 기존5파일/8marker와 NCC3scratch다. 메인 GO 뒤 marker-only 기준을 보관하고 재사용을 적용한다. 에셋·씬·quality·VFX·AI 배율·네트워크 설정·Git 쓰기/pull/fetch는 이번 범위에서 하지 않는다.
- 문서 검사: `python3 Tools/check_docs.py`와 새 Task의 상대 Markdown 경로/앵커를 별도로 확인한다. 정적 검사 통과를 Unity 컴파일·Profiler 측정 PASS로 바꾸지 않는다.

## 6. 단계별 적용과 비교 기준 보관(2026-10-26 KST)

첫 단계의5source/8marker 적용과 메인 원문 역복원 대조를 완료했다. 변경 전 계측 기준은`7c00f768`에 **marker-only patch만 더한 source**이며, 이미 해당 커밋에 marker가 있다는 뜻이 아니다. 메인이 `/workspace/scratch/hexiege-optimization-20261026/`에 patch·5source snapshot·manifest를 보관했다. 정확 파일명과 기준의 한계는 [Research7](Research.md#7-계측만-있는-변경-전-기준-보관2026-10-26-kst)에 기록했다.

두 번째 단계의NCC3컬렉션 재사용도 적용했다. 공유 소유자는 key 목록을 Clear/AddRange하고 중첩은 원래 세 생성자를 사용하며, 제거 목록은 원래 생성 위치에서 선택/Clear한다. finally에서 선택된 local만 정리하고 공유 소유자만 표시를 해제한다. 전문 receipt와 메인 독립 대조에서 두 변환 역복원5source의 기준 HEAD 동일·기존meta 보존·선택20개의 문법 오류0·Release 계측 SDK/marker 제외를 확인했다. 실제 Unity 컴파일·기능·Android Profiler 전후 캡처는 미실행이다.

보관한 `buffer-only.patch`는 marker-only 상태 뒤에 적용하는 후속 차이다. 원본7c00f768에 marker-only를 더한 변경 전 빌드와, 여기에 buffer-only까지 더한 변경 후 빌드를 나중에 같은 조건의 Android Host로 비교할 수 있다. QA 최종 정적 검토는 blocker0이며 메인의 기존 foreach3본문 정규화 대조도 동일하다. 반복 할당을 정리한7경로의 source 준비/정적 대조를 실제 Unity 컴파일·성능 개선이나30 FPS 달성으로 판정하지 않는다.

## 7. 다른 환경의 구현·테스트 인계

사용자 요청으로 [Handoff.md](Handoff.md)를 추가해 실제 게시 상태·새 Unity/기기 환경 확인·컴파일/Editor 검사·V01~V10·더 넓은 리팩토링 세부 계약·Profiler 전후 비교·자료 수집을 한 문서에 연결한다. 기존 작업을 덮어쓰지 않도록 별도 clean 기준본에서만 복원하며, 이미 최적화 source를 받았으면 중복 patch를 적용하지 않는다.

같은 폴더에 [marker-only.patch](marker-only.patch), [buffer-only.patch](buffer-only.patch), [HandoffBaseline.json](HandoffBaseline.json) 및meta를 보관한다. 기준 전체 SHA/5파일의각상태hash/patch SHA256/순서/8marker/미실행 결과를 기계적으로 대조할 수 있다. 두patch는 기존 scratch bytes를 그대로 복사하고 신규GUID만 부여한다. 새 helper·검증 코드·제품 기능은 추가하지 않는다.

기능 Testcase의 현재 상태 문단만 게시7c00f768과 이 인계 링크로 갱신한다. 이전 사용자 확인을 유지하고, 그 외 audit Plan5.1의 미확인 세부 계약과 현재 집중 검사/실기의 범위를 구분한다. 인계 파일 작성·source 정적 검토·실제 실행 PASS는 별개다. 문서/상대링크·신규GUID·patch SHA/현재5source hash를 확인하고 Git 게시·기기 실행은 수행하지 않는다.

후속 사용자 게시 승인으로 메인이 marker-only 기준commit070ddcaf를 만들었다. 현재 source와 동일8marker의 변경 전 빌드는 이commit을 우선 사용하며,7c00f768+marker-only patch는 동일한 대안이다. 변경 후는 해당 기준에 buffer-only를 더한다. 버퍼/인계 commit·main5861fcb4 통합·게시의 최종 결과는 메인 실행 후 따로 확인한다.
