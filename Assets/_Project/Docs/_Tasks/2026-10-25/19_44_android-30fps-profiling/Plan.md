# Android 30 FPS 측정 기반 최적화 계획

실제 기기에서 어느 처리가 프레임을 오래 쓰는지 확인할 수 있도록 측정 구간을 준비한다. 변경 전 자료를 먼저 모으고, 확인된 비용만 줄인 뒤 같은 조건에서 다시 비교한다. 이번 첫 단계에는 기능이나 게임 수치를 바꾸는 최적화를 포함하지 않는다.

> 기존 동작 코드의 폐기·비활성화·최종 삭제는 없다. 첫 구현은 개발용 계측만으로 한정하며 기존 계산·콜백·권위·네트워크 설정·에셋을 유지한다. 기준선 수집 전 컬렉션 재사용·검색 알고리즘 변경·GPU 품질 하향은 하지 않는다.

- 조사 원본: [Research.md](Research.md).
- 기준: `work / 572d94af`, Unity 6000.3.5f2, Android 30 FPS(약 33.3ms/프레임), 최소 기기 미정.
- 상태: 사용자 결정으로 최적화/계측 구현을 보류했다. 아래3파일/5marker는 미구현 코드 전 제안으로 보존하고, 먼저 [기능 교정 Task](../20_47_spawn-facing-and-repath-fix/Plan.md)를 진행한다. 기준선 측정은 아직 없다.

## 1. 보존할 규칙과 첫 범위

[GameSystemRules](../../../GameSystemRules.md) 인덱스를 읽고 아래 원본을 대조했다.

| 작업 | 근거·보존 |
|---|---|
| 전투 시간·탐색 비용 계측 | [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 「이동 규칙」 규칙6의 월드 좌표 사거리 판정 유지 |
| 서버 전투 틱 계측 | [전투 동기화](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001·NET-AUTH-002·NET-ROOT-001의 서버 결과/Simulation Root·표현 예약 경계 유지 |
| 계측의 레이어 배치 | [기술 설계](../../../TechnicalDesignDocument.md)의 의존성 방향 추상화와 실제 Application 예외 범위를 검토해 전문 설계로 확정. 새 역방향 의존을 만들지 않음 |

조건부 `DEVELOPMENT_BUILD || UNITY_EDITOR` 계측으로 한정한다. 고정 문자열 marker와 scope 수명으로 동기 메서드의 시간만 측정하며, 코루틴 yield/await를 가로지르는 scope를 만들지 않는다. Application/Domain은 수정하지 않고 Infrastructure/Presentation caller에서만 `Unity.Profiling.ProfilerMarker`를 사용한다. using·static readonly 필드·scope는 모두 같은 guard로 감싸 Release에 계측 SDK 참조와 호출이 남지 않게 한다.

### 1.1 전문 정확안 — 아직 구현하지 않음

전문 설계 `/tmp/hexiege-profiler-markers-20261025/design.json`을 문서 담당이 직접 읽었다. 아래는 메인의 전체 계획 검토/GO 후에만 적용할 안이며, 지금은 후속 테스트 로그 분석을 우선한다.

| 파일 | 고정 marker·정확 scope |
|---|---|
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) | `Hexiege.NetworkCombat.TickCombat`: TickCombat 입구부터 모든 동기 return/예외까지 Auto scope. `Hexiege.NetworkCombat.TargetQueries`: 기존 공격 대상/감지 질의 두 식만 감싸며 short circuit·평가 순서 유지. 기존 `var attackResult`/`bool hasEnemy`는 블록 밖에 같은 nullable tuple `(int id, bool isUnit)?`/bool로 선언하고 기존 RHS를 원순서대로 대입한다. |
| [ProductionTicker.cs](../../../../Scripts/Presentation/Production/ProductionTicker.cs) | `Hexiege.Economy.TickIncome`: 기존 의존성 nonnull if 안의 TickIncome 호출만 감싸며 서버/클라이언트 가드를 유지한다. |
| [UnitView.Movement.cs](../../../../Scripts/Presentation/Unit/UnitView.Movement.cs) | `Hexiege.UnitView.ShouldEngage`, `Hexiege.UnitView.ShouldEngageAt`: 두 동기 bool 메서드 입구부터 종료까지 scope. 기존 생존/의존성 guard와 힐러 분기를 포함한다. |

TickCombat 전체에는 타워·연구·상태 처리와 중첩 콜백이 포함되므로 공격 검색만의 시간으로 읽지 않는다. TargetQueries는 해당 caller의 두 질의만, UnitView scope는 두 진입 API만 포함하므로 전체 전투 검색의 완전한 호출 횟수나 비용을 뜻하지 않는다. 동적 marker 이름·추가 런타임 로그·ProfilerRecorder·자동 캡처 도구를 만들지 않는다. 계측 전후에는 동일 marker 세대/빌드 옵션을 사용한다.

정적 확인은 계측 guard 블록을 제거하고 질의 블록의 선언/대입을 원문 두 식으로 역복원한 bytes 동일, 기존 meta·다른 source hash 보존이다. 새 tuple 선언/블록은 Release에서도 남지만 원식의 타입/평가/사용 위치를 유지한다. 이 확인은 Unity 컴파일이나 실제 marker 표시 확인을 대신하지 않는다.

## 2. 구현 순서

1. 전문 프로그래머의 3파일/5marker 제안과 레이어 근거를 위 1.1에 기록했다. 현재 코드 GO 전이다.
2. 문서 담당이 이 계획에 파일·marker·scope/전처리 범위를 확정하고 메인이 원문과 전체 계획을 읽는다.
3. 메인 GO 뒤 계측만 구현하고 source/meta/API·기존 동작의 허용 차이를 정적으로 대조한다.
4. 사용자 PC에서 최신 source를 컴파일하고 Android Development 기준선을 수집한다.
5. 캡처의 CPU/GPU/GC/네트워크 비용을 판정한 뒤 첫 실제 최적화 범위를 이 계획에 추가한다. 변경 전후 같은 계측과 장면으로 비교한다.

## 3. 사용자 PC의 최초 캡처 절차

1. 최신 계측 source에서 컴파일 오류가 없는지 먼저 확인한다. 오류가 있으면 기기 측정으로 넘어가지 않는다.
2. Android Build Profile에서 **Development Build**와 **Autoconnect Profiler**를 켠다. **Deep Profiling은 끈다.** 이 단계에서 목표 FPS·해상도·렌더 품질·네트워크 주기를 새로 변경하지 않는다.
3. USB로 Android 기기를 연결해 빌드/실행하고 `Window > Analysis > Profiler`를 연다. Profiler의 대상 플레이어가 Editor가 아닌 실행 중 Android 앱인지 확인한다. 자동 연결되지 않으면 대상 플레이어 목록에서 선택한다.
4. CPU Usage와 Memory를 우선 기록한다. 가능한 환경에서는 GPU와 Rendering, 설치된 패키지가 지원하는 네트워크 통계도 함께 확인한다. GPU/네트워크 모듈이 제공되지 않는 환경을 정상 측정치0으로 기록하지 않는다.
5. 맵 로딩이 끝나고 화면·유닛 수가 안정된 구간에서 기록한다. **유닛 적음 / 다수 이동 / 밀집 전투**를 나눠 각각 연속 약30초를 저장한다. 같은 맵·종족·유닛 구성/수·카메라·품질·화면 주사율·Host/Client 역할을 적고 변경 전후 비교 시 맞춘다.
6. CPU Timeline/Hierarchy에서 고정 marker의 Total/Self 시간·Calls와 GC Alloc을 확인한다. Allocation Call Stacks는 할당 원인을 좁힐 때 별도 짧은 캡처에서만 켜고 기본 캡처와 섞지 않는다.
7. Profiler의 저장 기능으로 각 캡처를 저장한다. 기기 모델/Android 버전·빌드 리비전·싱글/Host/Client·해상도/품질·충전 여부/발열 상태·대략 유닛 수와 해당 장면을 함께 전달한다.

기기는 캡처 전에 비슷하게 예열하고 전후 측정의 발열·충전·백그라운드 앱 조건을 맞춘다. 서로 다른 기기나 Host/Client 역할의 수치를 한 쌍의 전후 개선율로 계산하지 않는다. 화면 30 FPS 표시만으로 순간 끊김·GC·입력 안정성을 통과시키지 않는다.

## 4. 판정표

| 확인 축 | 변경 전 | 변경 후 | 판정 기준 |
|---|---|---|---|
| CPU 프레임/marker 시간·호출 수 | 미수집 | 미수집 | 동일 조건의 분포와 느린 프레임을 비교 |
| GC Alloc/GC 정지 | 미수집 | 미수집 | 반복 할당과 끊김 원인을 확인 |
| GPU/Rendering | 미수집 | 미수집 | 지원 환경의 GPU 비용과 배치 수 비교 |
| 네트워크 송수신 | 미수집 | 미수집 | 같은 역할·유닛 수에서 전송량과 처리 비용 비교 |
| Material/Texture/재경기 메모리 | 미수집 | 미수집 | 재경기 전후 객체 수/메모리와 수명 확인 |
| Android 30 FPS 지속/기능 회귀 | 미확인 | 미확인 | 실제 기기에서 프레임 예산과 기존 기능 확인 |

사용자 PC는 Unity와 Android USB 측정이 가능하다고 답했다. 현재 작업 환경에서는 Unity·adb·기기 Profiler 접근 수단을 확인하지 못했다. 계측 source 준비와 정적 대조까지 진행하고, 실제 기기 기준선 없이는 최적화 효과를 주장하거나 임의의 전투/렌더 수치를 조정하지 않는다.

## 5. 파일·검사 경계

- 신규 문서: 이 Research/Plan과 Unity 인식용 새 일자·작업 폴더/문서 meta. 별도 Testcase·상시 문서·메모리·옛 Task는 변경하지 않는다.
- source: 1.1의 전문 정확안은 제안 상태이며 메인 GO 전 변경 없음. 에셋·씬·quality·VFX·AI 배율·네트워크 설정·Git 쓰기/pull/fetch는 이번 첫 단계에서 하지 않는다.
- 문서 검사: `python3 Tools/check_docs.py`와 새 Task의 상대 Markdown 경로/앵커를 별도로 확인한다. 정적 검사 통과를 Unity 컴파일·Profiler 측정 PASS로 바꾸지 않는다.
