# Android 30 FPS 성능 측정과 최적화 조사

게임이 실제 Android 기기에서 안정적으로 동작하도록 먼저 느린 구간과 메모리 할당을 측정한다. 코드에서 보이는 개선 후보를 실제 병목으로 단정하지 않고, 같은 조건의 변경 전후 자료를 비교해 필요한 부분부터 최적화한다. 이번 첫 단계는 기준선 측정을 준비하는 작업이다.

- 시작: 2026-10-25 19:44 KST. 생성 전 `TZ=Asia/Seoul date`로 확인했다.
- 목표: 사용자 지정 최소 기기 없음, Android 30 FPS(프레임 예산 약 33.3ms).
- 기준 소스: `work`, `572d94af28cb8ee1f8142c49bea31a6f72e33fc5`. 직전 리팩토링의 구현 종료와 최신 컴파일·기기 검증은 별개다.
- 현재 상태: 사용자 「최적화 우선 보류, 이런 문제들부터 바로잡자」에 따라 보류. caller3파일/marker5개는 코드 전 제안이며 GO·구현·Profiler 기준선 수집은 없다. 기능 문제는 [별도 Task](../20_47_spawn-facing-and-repath-fix/Research.md)에서 우선 교정한다. 30 FPS 목표는 유지하되 성능 개선률/달성은 미확인이다.
- 현재 범위: [Plan.md](Plan.md)의 기준선 절차와 개발 빌드용 계측 준비. 기존 리팩토링 Task 원문, 상시 규칙·에셋·네트워크 수치·VFX·AI 생산시간 배율은 변경하지 않는다.

## 1. 승인과 환경

사용자는 「Profiler를 활용하면서 최적화도 같이 진행해보자」로 측정과 최적화 진행을 요청했다. 이전 문서 작성·자율 진행 승인을 이어 사용하며 별도 Testcase 문서는 만들지 않는다. 실제 최적화는 기준선 자료를 확인한 뒤 작은 변경부터 비교한다.

메인 확인에 따르면 현재 실행 환경의 PATH와 확인한 대표 설치 경로에는 Unity·adb·dotnet이 없고 연결된 도구에서도 Unity/Profiler 접근 수단이 발견되지 않았다. 이는 로컬 사용자 PC에서 측정할 수 없다는 뜻이 아니다. 사용자는 PC에서 Unity와 Android 기기를 사용할 수 있다고 답했다. 이 환경의 정적 대조를 컴파일이나 기기 측정으로 기록하지 않는다.

## 2. 직접 확인한 현재 코드와 판정

| 근거 | 현재 확인 | 판정·보존할 것 |
|---|---|---|
| [UnitCombatUseCase.cs](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) 감지/공격 검색 | 사거리 검색 본체가 적 유닛·건물 목록을 순회한다. 공개 판정 API는 생존/입력 검사를 먼저 한다. | 반복 호출 비용의 측정 후보. 전체 순회가 실제 최대 병목이라는 실행 근거는 없다. 월드 좌표 거리와 대상 선택을 유지한다. |
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) TickCombat | 서비스 가드 뒤 서버 전투와 예약·상태 처리를 수행한다. | 전투 틱 시간/GC 측정 후보. 권위·사망 이벤트·RPC 순서와 주기를 유지한다. |
| [ResourceUseCase.cs](../../../../Scripts/Application/UseCases/ResourceUseCase.cs) TickIncome | 기본/채굴 수입 처리와 팀별 누적기를 사용한다. | 경제 처리 비용의 측정 후보. 분리된 누적기·수치·호출 순서를 유지한다. |
| [TechnicalDesignDocument.md](../../../TechnicalDesignDocument.md) 타겟 성능 | 기존 문서에는 모바일 60 FPS 목표가 있다. | 이번 작업 기준은 최신 사용자 지정 30 FPS다. 기존 문서의 과거 목표와 실제 기기의 달성 수치를 혼동하지 않는다. |

계측의 레이어 배치·고정 marker 이름·정확한 source 범위는 [Plan.md](Plan.md) 1.1의 전문안에 기록했다. Application/Domain을 바꾸지 않는 caller 3파일/5marker 제안이다. 메인 코드 GO는 아직 없으며 source를 바꾸지 않았다.

## 3. 측정에서 분리할 축

- Editor·Android Development·Android Release 결과를 별도 기록한다. Editor와 Development의 개발 로그·관측기 비용을 Release 성능으로 일반화하지 않는다.
- CPU·GPU·GC 할당·네트워크를 각각 구분한다. 프레임이 33.3ms를 넘는다는 사실만으로 전투 CPU가 원인이라고 판단하지 않는다.
- Profiler 연결과 계측 자체에도 비용이 있으므로 변경 전후에는 동일한 계측/연결 설정을 사용한다. Release 장시간 구동 결과는 별도 확인이다.
- VFX는 다른 브랜치 작업 완료 후 통합할 별도 범위다. Shadow 이름의 현재 production 흐름을 진단 전용으로 보고 끄지 않는다.
- AI 생산시간 배율은 사용자 지정 후속 AI 작업으로 남긴다.

## 4. 실행 증거 상태

| 축 | 현재 결과 |
|---|---|
| 코드/문서 정적 조사 | 위에 명시한 범위 확인 |
| 계측 source 구현 | caller 3파일/5marker 전문안 기록, 메인 GO 전·미구현 |
| Unity 컴파일·Android 빌드 | 미실행 |
| Android Profiler 기준선 | 미수집 |
| 최적화 적용·변경 후 캡처 | 미실행 |
| 30 FPS·메모리·장시간 안정성 | 미확인 |

새 자료가 오면 리비전·기기·Host/Client 역할·빌드 종류·측정 설정·장면 조건과 함께 이 표를 갱신한다. 이전 [리팩토링 기록](../../2026-10-23/14_08_architecture-refactoring-audit/Plan.md)의 정적 검증을 이번 성능 PASS로 재사용하지 않는다.

## 5. 후속 기능 테스트 로그 검토와의 구분

사용자가 일반 테스트에서 「특별히 문제 없어 보임」이라고 관찰했지만, 싱글에서 생산 건물을 둘러싸 생산이 대기하다 주변 건물을 파괴한 뒤 생산되면 유닛 방향이 어긋나는 현상을 별도로 신고했다. 후속 답변은 Pistoleer 한 번 관찰·이동 중에도 지속이며 다른 유닛/팀 색상/정확 시각은 미확인이다. 기능 검토는 기존 리팩토링 Task에서 이어 기록한다. 이번에 받은 runtime 로그는 Unity Profiler 캡처가 아니므로 CPU/GPU/GC 기준선이나 30 FPS 달성 자료로 사용하지 않는다. 이 신고를 먼저 분석하는 동안 위 계측은 코드 전 계획 상태로 유지한다. 초기 점진 회전만으로 지속 증상을 설명하지 못하며, 추가 소스 검토에서도 상시 dual-writer 충돌 근거는 확인되지 않았다. 원인·회귀 리비전·수정안은 아직 확정하지 않았다.
