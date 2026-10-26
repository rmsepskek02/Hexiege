# Android 30 FPS 성능 측정과 최적화 조사

게임이 실제 Android 기기에서 안정적으로 동작하도록 처리 시간과 메모리 할당을 확인한다. 사용자의 후속 승인으로 반복 할당의 좁은 정리와 측정 구간 준비를 먼저 진행하고, 같은 조건의 변경 전후 자료로 효과를 판정한다. 코드에서 보이는 개선 후보를 실제 최대 병목이나 성능 향상으로 단정하지 않는다.

- 시작: 2026-10-25 19:44 KST. 생성 전 `TZ=Asia/Seoul date`로 확인했다.
- 목표: 사용자 지정 최소 기기 없음, Android 30 FPS(프레임 예산 약 33.3ms).
- 최초 기준 소스는`572d94af`였으며, 재개 시점의 clean 기준은`work / 7c00f768`이다. 기능 검증 준비까지 푸시된 상태와 실제 사용자 기기 테스트 완료는 구분한다.
- 현재 상태(2026-10-26 KST 게시 확인): 최적화 코드 기준ff8bdf95를 원격main/work 양쪽에 게시했고 계측만 있는 변경 전 기준070ddcaf도 게시 이력에 포함됐다. 최신 main5861fcb4는3c1ac957로 병합됐다. 새 환경은 MAIN에서 최적화 source와 인계 자료를 바로 받을 수 있다. 최종 문서 HEAD와 코드 기준은 구분하며 [Handoff](Handoff.md)·[manifest](HandoffBaseline.json)를 따른다. 실제 Unity 컴파일·기기 캡처·개선량·30 FPS 달성은 미확인이다.
- 현재 범위: [Plan.md](Plan.md)의 제한 계측·반복 할당 정리와 Profiler/통합 검증 인계. 기존 기능 Testcase는 게시 상태와 인계 링크의 현재 문단만 정정한다. 상시 규칙·제품 에셋·전투/네트워크 수치·VFX·AI 생산시간 배율은 변경하지 않는다.

## 1. 승인과 환경

사용자는 「Profiler를 활용하면서 최적화도 같이 진행해보자」로 측정과 최적화를 요청했고 기능 문제 우선 교정 동안 보류했다가, 후속 「좋아 그럼 그렇게 진행」으로 재개했다. 이전 문서 작성·자율 진행 승인을 이어 사용하며 별도 Testcase 문서는 만들지 않는다. 이번에는 반복 할당의 정적 근거가 있는 좁은 정리를 먼저 진행할 수 있지만, 효과는 같은 계측·같은 기기 조건의 전후 캡처 뒤 판정한다.

메인 확인에 따르면 현재 실행 환경의 PATH와 확인한 대표 설치 경로에는 Unity·adb·dotnet이 없고 연결된 도구에서도 Unity/Profiler 접근 수단이 발견되지 않았다. 이는 로컬 사용자 PC에서 측정할 수 없다는 뜻이 아니다. 사용자는 PC에서 Unity와 Android 기기를 사용할 수 있다고 답했다. 이 환경의 정적 대조를 컴파일이나 기기 측정으로 기록하지 않는다.

## 2. 직접 확인한 현재 코드와 판정

| 근거 | 현재 확인 | 판정·보존할 것 |
|---|---|---|
| [UnitCombatUseCase.cs](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) 감지/공격 검색 | 사거리 검색 본체가 적 유닛·건물 목록을 순회한다. 공개 판정 API는 생존/입력 검사를 먼저 한다. | 반복 호출 비용의 측정 후보. 전체 순회가 실제 최대 병목이라는 실행 근거는 없다. 월드 좌표 거리와 대상 선택을 유지한다. |
| [NetworkCombatController.cs](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) TickCombat | 서비스 가드 뒤 서버 전투와 예약·상태 처리를 수행한다. | 전투 틱 시간/GC 측정 후보. 권위·사망 이벤트·RPC 순서와 주기를 유지한다. |
| [ResourceUseCase.cs](../../../../Scripts/Application/UseCases/ResourceUseCase.cs) TickIncome | 기본/채굴 수입 처리와 팀별 누적기를 사용한다. | 경제 처리 비용의 측정 후보. 분리된 누적기·수치·호출 순서를 유지한다. |
| [TechnicalDesignDocument.md](../../../TechnicalDesignDocument.md) 타겟 성능 | 기존 문서에는 모바일 60 FPS 목표가 있다. | 이번 작업 기준은 최신 사용자 지정 30 FPS다. 기존 문서의 과거 목표와 실제 기기의 달성 수치를 혼동하지 않는다. |

초기 계측 제안은 [Plan1.1](Plan.md#11-전문-정확안--아직-구현하지-않음)에 당시 기록으로 보존한다. 재개 시점에는 main의 서버 권위 투사체 경로가 통합돼 있으므로 원래 제안만으로 현재 비용을 모두 설명하지 않는다. 현행 [Plan1.2](Plan.md#12-재개-정확안--고정-marker8개와-전투-틱-컬렉션3개-재사용)에 전문 고정8marker/5파일과 scratch 소유권·중첩/예외 정리를 코드 전에 기록했고 메인 GO 뒤 계측부터 적용했다.

## 3. 측정에서 분리할 축

- Editor·Android Development·Android Release 결과를 별도 기록한다. Editor와 Development의 개발 로그·관측기 비용을 Release 성능으로 일반화하지 않는다.
- CPU·GPU·GC 할당·네트워크를 각각 구분한다. 프레임이 33.3ms를 넘는다는 사실만으로 전투 CPU가 원인이라고 판단하지 않는다.
- Profiler 연결과 계측 자체에도 비용이 있으므로 변경 전후에는 동일한 계측/연결 설정을 사용한다. Release 장시간 구동 결과는 별도 확인이다.
- VFX·서버 투사체의 main 통합은 현재 기준에 포함된다. 이 최적화에서 VFX 정책·에셋을 바꾸거나 Shadow 이름의 실제 production 흐름을 진단 전용으로 보고 끄지 않는다.
- AI 생산시간 배율은 사용자 지정 후속 AI 작업으로 남긴다.

## 4. 실행 증거 상태

| 축 | 현재 결과 |
|---|---|
| 코드/문서 정적 조사 | 위에 명시한 범위 확인 |
| 계측·좁은 할당 정리 |5파일/8marker와NCC3scratch 적용·전문/메인 정적 대조 완료. marker-only 기준과후속patch 보관 |
| Unity 컴파일·Android 빌드 | 미실행 |
| Android Profiler 기준선 | 미수집 |
| 최적화 적용·변경 후 캡처 | 좁은 반복 할당 정리 적용, 실제 기기 캡처 미수집·효과 미측정 |
| 30 FPS·메모리·장시간 안정성 | 미확인 |

새 자료가 오면 리비전·기기·Host/Client 역할·빌드 종류·측정 설정·장면 조건과 함께 이 표를 갱신한다. 이전 [리팩토링 기록](../../2026-10-23/14_08_architecture-refactoring-audit/Plan.md)의 정적 검증을 이번 성능 PASS로 재사용하지 않는다.

## 5. 후속 기능 테스트 로그 검토와의 구분

사용자가 일반 테스트에서 「특별히 문제 없어 보임」이라고 관찰했지만, 싱글에서 생산 건물을 둘러싸 생산이 대기하다 주변 건물을 파괴한 뒤 생산되면 유닛 방향이 어긋나는 현상을 별도로 신고했다. 후속 답변은 Pistoleer 한 번 관찰·이동 중에도 지속이며 다른 유닛/팀 색상/정확 시각은 미확인이다. 기능 검토는 기존 리팩토링 Task에서 이어 기록한다. 이번에 받은 runtime 로그는 Unity Profiler 캡처가 아니므로 CPU/GPU/GC 기준선이나 30 FPS 달성 자료로 사용하지 않는다. 이 신고를 먼저 분석하는 동안 위 계측은 코드 전 계획 상태로 유지한다. 초기 점진 회전만으로 지속 증상을 설명하지 못하며, 추가 소스 검토에서도 상시 dual-writer 충돌 근거는 확인되지 않았다. 원인·회귀 리비전·수정안은 아직 확정하지 않았다.

## 6. 최적화 재개와 비교 기준(2026-10-26 KST)

앞5절의 계측 보류는 당시 기능 교정 우선 지시의 기록이다. 후속 재개 승인에서는 좁은 반복 할당 정리와 Profiler marker를 준비하고, 기능 테스트는 최적화 묶음 뒤 한 번에 진행한다. 지금 코딩 중에 사용자에게 즉시 캡처하거나 현재 작업을 되돌리도록 요구하지 않는다. 나중에 같은 조건의 변경 전/후 두 빌드를 측정해도 된다.

| 초기 제안 | 재개 후 적용할 경계 |
|---|---|
| 기준선을 받기 전에는 컬렉션 재사용을 하지 않음 | 사용자 승인에 따라 정적 근거가 있는 좁은 재사용을 먼저 구현할 수 있음. 실제 병목·개선량은 캡처 전 미확정 |
| 미통합 VFX 브랜치 대기 | 현재 기준7c00f768에는 main VFX/서버 투사체가 포함됨. 이를 최적화나 전기능 실기 PASS로 읽지 않음 |
| 변경 전 원본과 변경 후 자료 비교 | marker별 시간 비교는 **같은 marker를 넣은 원본 코드**와 재사용 코드에서 수행. marker 없는7c00f768은 전체 프레임/GC 비교 자료로는 가능하나 marker 내부 시간의 직접 비교 기준은 아님 |

재사용 컬렉션은 controller가 살아 있는 동안 확보한 용량을 유지한다. 반복 할당을 줄여도 총 RAM이 반드시 줄지는 않으며, 초기 용량 증가와 중첩 호출의 별도 버퍼는 할당할 수 있다. HashSet.Clear도 용량에 따른 CPU 비용이 있으므로0B/프레임·CPU 개선·메모리 감소를 미리 약속하지 않는다. 기기 모델이 정해지지 않았으므로 첫 측정 기기를 기록하고 Android30 FPS의 지속 여부를 그 기기 범위에서 평가한다.

## 7. 계측만 있는 변경 전 기준 보관(2026-10-26 KST)

메인의 Plan1.2 전체 검토/GO 뒤 기존5source에 고정8marker만 먼저 적용했다. 메인이 각 파일의 계측 변환을 역복원한 bytes가 기준 HEAD와 같고 이름8개가 유일함을 확인했다. 문서 담당은 아래 manifest도 직접 읽어 기준 전체 SHA와5개 파일 목록을 대조했다. 실제 Unity 컴파일·marker 표시·기기 시간 측정은 아직 아니다.

변경 전 계측 기준은 **Git 커밋 자체가 아니라** 기준`7c00f768d5c9f0ebb9583e93e4f6a4a45397f306`에 marker-only patch를 더한 source 상태다. 메인이 `/workspace/scratch/hexiege-optimization-20261026/`에 `marker-only.patch`, `marker-only-source.tar.gz`, `marker-only-manifest.json`, `marker-only/`를 보관했다. 이 기준에는 컬렉션 재사용이 없어 나중에 동일 marker의 전후 비교 빌드를 준비할 수 있다. 사용자가 지금 임의로 체크아웃하거나 현재 작업을 되돌릴 필요는 없다.

이후 NCC3컬렉션 재사용을 별도 단계로 적용했다. 전문 receipt `/tmp/hexiege-profiler-first-batch-20261026/static-review.json`을 문서 담당도 읽어5source의 marker 역복원, scratch를 되돌리면 marker-only와 동일함, 기존meta5 보존, 소유권8항목·고정8이름을 확인했다. 구현은 공유 소유자일 때 key 목록을 Clear/AddRange하고 중첩 호출은 원래 세 생성자를 사용한다. 제거 목록 선택/Clear는 기존 늦은 생성 위치를 유지하며 finally에서 선택된 local만 정리하고 공유 소유자만 표시를 해제한다.

메인의 독립 대조도 두 변환을 역복원한5source가 기준 HEAD와 같고,5source의4조건 선택20개 문법 오류0·Release 계측 SDK/marker 참조 없음·기존meta 불변임을 확인했다. QA 최종 정적 검토는 blocker0이며 메인의 기존 foreach3본문 정규화 대조도 동일하다. 이는 실제 Unity 컴파일이나 callback 재진입의 실행 PASS가 아니다. 실제 Unity·Profiler·Android 실행은 미실행이고0B/프레임·총 RAM 감소·FPS 개선을 확인하지 않았다.

이번 NCC3버퍼는 멀티 서버 전투 틱의 변경이므로 기기 성능 전후 비교는 **두 버전 모두 Android Host + Editor Client**여야 한다. Android Client 캡처에 TickCombat marker가 없거나0이어도 해당 최적화가 실행된 근거가 아니다. 기존 통합 기능 검사는 Editor 전용 서버 메뉴 때문에 **Editor Host + Android Client**를 유지하며, 두 역할 구성을 나중에 한 테스트 묶음으로 진행하되 같은 성능 비교 자료로 섞지 않는다.

메인은 같은 보관 폴더에 후속 `buffer-only.patch`도 보관했다. 재현 순서는 기준7c00f768에 marker-only patch, 이어 buffer-only patch이며 두 patch를 한 단계로 혼동하지 않는다. 첫 최적화 구현 마감 당시 source5+기존 Task2의7경로가 미커밋·미푸시였고 신규 meta·에셋·상시 문서·기존 기능 테스트 문서를 추가 수정하지 않았다. 이후 사용자 인계 요청의 파일 추가는8절이다. 사용자 기기 확인은 최적화 묶음 뒤 한 번에 진행하며 source 준비/정적 대조를 Android 성능·기능 수용으로 바꾸지 않는다.

## 8. 다른 환경을 위한 이식 가능한 인계 자료(2026-10-26 KST)

사용자가 다른 환경에서 최적화와 리팩토링 관련 남은 테스트까지 함께 이어갈 문서를 요청했다. 기존 Task 안에 [Handoff.md](Handoff.md), [marker-only.patch](marker-only.patch), [buffer-only.patch](buffer-only.patch), [HandoffBaseline.json](HandoffBaseline.json)과 각meta를 추가했다. 두patch는 scratch 원본과 bytes/SHA256가 같고 manifest에는 기준 전체 SHA·source5개의 원본/계측만/최적화 hash·고정8marker·적용 순서·미실행 상태를 담았다. 이 자료를 받아야 이전 환경의`/tmp`나`/workspace/scratch`가 없어도 별도 clean 기준 프로젝트에서 복원할 수 있다.

현재 최적화가 아직 게시되지 않았음을 명시하고, source를 이미 받은 경우에는 optimized hash를 확인해 patch 중복 적용을 막는다. 게시된7c00f768만 받은 경우에는 별도 기준본 두 개에서 변경 전=marker-only, 변경 후=marker-only 다음buffer-only로 구성한다. 활성 작업 트리를 reset/clean/역patch로 덮어쓰지 않는다. 인계 ZIP은 프로젝트 전체가 아니라11개 문서/patch/meta 파일 묶음이며 실제 생성/전달은 메인 최종 결과를 따른다.

Handoff는 기존 사용자 컴파일·일반 플레이 수용과 싱글 이번 미재현을 유지한다. 기존 V01~V10 메뉴/실기뿐 아니라 리팩토링 Plan5.1의 인증·AI값·반올림·인구·영토·수입·partial/ILPP 세부 증거도 별도 장부로 연결했다. 기존 집중 검사 성공을 이 전체 장부의 PASS로 확대하지 않는다. 기존 기능 Testcase의 현재 상태 문단만7c00f768 게시와 미게시 최적화 인계 링크로 정정하며, 옛 Research/Plan·전역 문서는 변경하지 않는다.

이번 인계 작성은 새 실행 증거를 만들지 않았다. Unity/ADB/Computer Use의 새 환경 준비 여부부터 확인하고, 기능 검사는Editor Host+Android Client, 이번 NCC 성능 비교는Android Host+Editor Client로 분리한다. 실제 데이터가 오기 전에는 최신 컴파일·실기·성능 개선을 PASS로 기록하지 않는다.

후속 게시 완료로 변경 전 계측 기준은`070ddcafa368dcfb94fb76cb3218000a6cacc474`, 변경 후 코드 기준은`ff8bdf9542f468f02788ff9b582f601785234fde`다. 메인이 원격main/work의ff8bdf95 일치를 확인했고 최신 main5861fcb4 문서도3c1ac957 병합으로 들어왔다. 두 코드 기준의5source는 각각 manifest의 marker-only/optimized hash와 같다. 새 환경에서 MAIN의 최적화 source에 patch를 중복 적용하지 않는다.7c00f768+두patch는 복원 대안으로 유지하고 이후 최종 문서 commit의SHA는 실제 Git HEAD로 확인한다.
