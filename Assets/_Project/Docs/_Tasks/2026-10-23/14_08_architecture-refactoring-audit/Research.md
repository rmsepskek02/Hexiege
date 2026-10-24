# 아키텍처 준수·기능 보존 리팩토링 조사

기존 게임이 하던 일을 유지하면서 코드의 책임과 의존 관계를 명확히 하고, 실제로 쓰이지 않는 코드를 안전하게 정리하기 위한 조사다. 문서가 요구하는 동작과 실제 코드·씬·프리팹이 만드는 동작을 함께 비교한다. 많은 파일을 놓치지 않도록 전체 목록과 조사 상태를 이 문서에서 추적한다.

- 시작: 2026-10-23 14:08 KST.
- 사용자 승인: task 문서 작성·조사 착수에 이어, Plan2.3 공유 후 사용자의 「바로진행하자」로 IUnitView 팩토리 의존 추상화 첫 묶음 구현 승인(2026-10-23).
- 진행 상태: **전체 목록 확보 / 일부 계약·차이 조사 / 확정 묶음 소스 정리·정적 대조 완료 / 직전 푸시 `1a69402c` 후 사용자 컴파일 오류 없음 보고 / 전체 의미 감사·게임 실기 미완**. 최신 검증 축은16.13절이며 이전 회차의 컴파일 NOT RUN은 당시 에이전트 실행 기록으로 보존한다.
- 후속 승인: 사용자 「다음 진행해」로 다음 조사와 기존 Task 후속 기록 진행(2026-10-23). 단계2 조사 결과와 Plan2.4 공유 후 사용자가 「이 범위로 구현 진행」으로 명시 승인했고, 기존2파일 정리·메인 정적 대조를 마쳤다. 컴파일·실기는 미실행이다.
- 다음 착수: 사용자 「바로 작업 진행해」는 남은 리팩토링을 계속 실행하라는 지시다. 메인이 기공유한 AI live 종족 조회 주입2파일 안을 다음 묶음으로 선정했고 프로그래머 제안을 Plan2.5로 코드 전에 구체화·검토한 뒤 구현 신호를 전달했다. 해당2파일의 소스 정리·메인 독립 정적 대조를 마쳤으며 컴파일·게임 실행은 미실행이다(2026-10-23).
- 다음 착수: 사용자 「계속 진행해」의 잔여 리팩토링 계속 실행 지시 아래 메인이 기공유 인증 계약안을 다음 묶음으로 선정했다. 프로그래머 제안을 Plan2.6으로 코드 전에 기록했으며 메인 직접 검토·구현 신호 이후 소스 정리·독립 정적 대조를 마쳤다. 컴파일·Unity/실기는 미실행이다(2026-10-23).
- 후속 착수: 사용자 「작업 이어서진행하자」의 계속 실행 지시 아래 메인이 남은 AI 난이도 값 분리→UGS adapter를 순서로 선정했다. 전문 프로그래머 제안 중 AI 구체안을 Plan2.7에 코드 전에 기록했다. UGS2.8도 소스 정리·메인 독립 정적 대조를 마쳤으며 현재 브랜치는 `work`이며 이 문서의 「메인」은 브랜치 main이 아닌 조율 담당 에이전트를 뜻한다.
- 순차 진행: AI2.7 소스 정리·메인 독립 검토 후 AI 규칙4/33을 실제 snapshot 경계에 맞춰 동기화했다. UGS2.8은 메인 전체 계획 검토·AI 검증 후 별도 구현 신호를 전달해 소스 정리·독립 정적 대조와 Auth 구조3행 동기화를 마쳤다. 두 묶음의 컴파일/실기 완료를 뜻하지 않는다.
- 자율 진행 승인: 사용자 「다 진행해 매번 내가 진행하라고 하는것도 불편한데」로 기존 목표의 잔여 조사·기능 보존 리팩토링·확정 문서 사실 정정을 계속 진행한다. task 작성/매 묶음 재승인 질문을 반복하지 않는다. 전면 정적 조사 범위·담당·완료 기준은 Plan2.11/Research16절이며 기존 AI 생산배율/6VFX 삭제 보류와 동작 정책 경계를 유지한다.
- 계획: [Plan.md](Plan.md). 이 문서의 경로 목록은 파일 존재를 확인한 것이며 전체 내용을 읽었다는 뜻이 아니다.

## 1. 조사 범위와 집계 기준

저장소 루트에서 Python pathlib 재귀 목록을 만들고, 메인이 만든 임시 목록과 문서 담당이 다시 만든 목록의 집합을 비교했다. 아래 수치는 이번 Research/Plan 생성 전 기준이다. 생성 후 `_Tasks` Markdown은 614개이며 기존 작업 문서는 612개다. 같은 파일이 운영 지침과 상시문서에 중복 포함되므로 범주를 더해 전체 고유 파일 수로 표현하지 않는다.

| 범위 | 파일 수 | 현재 확보한 근거 |
|---|---:|---|
| Assets/_Project/Scripts/**/*.cs | 310 | 전 경로·계층·파일 크기 목록; 일부 의존 관계만 확인 |
| Assets/Editor/**/*.cs | 8 | 전 경로; 현재 메뉴 정책과 일부 대조 |
| Tools/**/*.py 및 루트 *.py | 5 | 전 경로; 일부 헤더·읽기 경로 확인 |
| Docs Markdown 중 _Tasks/_Logs 제외 | 48 | 전 경로; 아래 장부에 일부 열람 범위 구분 |
| 기존 _Tasks Markdown | 612 | 전 경로; 일부 과거 작업만 범위를 정해 열람 |
| _Logs Markdown | 10 | 전 경로; 런타임 txt 전체 조사는 별도 |
| 핵심 운영 지침 | 4 | AGENTS·CLAUDE·공용 메모리·WORKFLOW |
| 관리 대상 .claude Markdown | 41 | skills/plugins/worktrees 제외; 전문 내용 조사 완료 아님 |
| ProjectSettings 전체 파일 | 32 | 전 경로; ProjectVersion 값 확인 |
| Packages manifest/lock | 2 | 경로 확보; 실제 버전은 관련 항목별 확인 |

프로젝트 C# 계층별 수는 Domain 51, Application 85, Infrastructure 63, Presentation 99, Bootstrap 7, Core 4, Debug 1이다. 프로젝트 코드 폴더에서 `.asmdef`는 발견되지 않았다. `Assets/Packages/ai.meshy/.../MeshyAssembly.asmdef`는 프로젝트 계층의 assembly가 아니다. 새로운 assembly 분리는 현재 구현 항목으로 승인하지 않았다.

에셋은 `Assets/_Project` 아래 `.unity` 4, `.prefab` 159, `.asset` 52, `.controller` 25, `.anim` 66개를 경로 탐색으로 집계했다. **참조 그래프나 값의 정상 여부를 검사한 결과는 아니다.** `.meta` GUID, material/model 내 clip, Resources/StreamingAssets, 외부 패키지 자산도 개별 기능 조사 시 함께 확인한다. 외부 제공 C#은 Assets 전체 328개 중 프로젝트310·Editor8을 제외한 10개이며 DOTween 모듈과 Meshy Editor 코드다. 프로젝트 리팩토링 변경 대상과 구분해 유지한다.

## 2. 상태 기록과 누락 방지 방법

| 상태 | 뜻 | 다음 단계 |
|---|---|---|
| 목록 확보 · 내용 미조사 | 경로를 찾았으며 내용을 판정하지 않음 | 기능 담당과 관련 규칙 연결 |
| 일부 열람 | 명시한 절·범위만 읽음 | 나머지 계약·호출·에셋·이력 확인 |
| 조사 완료 | 해당 범위의 동작·참조·규칙·예외 근거를 기록 | 일치/수정/보류 판정 |
| 변경 필요 | 재현 또는 문서·구현 근거로 변경 항목 확정 | Plan의 파일별 변경표 작성 |
| 보류 | 기존 사용자 결정 또는 근거 부족 | 해제 조건과 원본 연결 |
| 검증 완료 | 명시한 코드 리비전·범위의 확인 수행 | 실행 종류와 사용자 수용을 구분 |

문제는 한 행에 하나씩 기록하고 ID를 유지한다. 같은 기능이 여러 문서에 다른 이름으로 등장하면 동일 항목으로 연결하되 규칙·기술·수치·상태·과거 기록을 합쳐 쓰지 않는다. 코드 장부와 문서 장부에서 같은 항목 ID를 역으로 찾을 수 있게 한다. 미조사 파일 수와 미해결 항목 수는 서로 다른 지표로 관리한다.

## 3. 문서 원본과 보존 계약

| 주장 종류 | 먼저 확인할 원본 | 비교 대상 |
|---|---|---|
| 제품 동작 | GameSystemRules 인덱스가 가리키는 해당 규칙; 인증은 AuthSystemRules | 실행 경로·씬·프리팹 값 |
| 계층·의존성·기술 | TechnicalDesignDocument 및 Network 상세; 공용 메모리 제약 | namespace·실제 타입·구성 루트 |
| 수치·단위·잠정값 | StatsReference | Config asset과 실제 소비 지점 |
| 연결·개별 수용 | UnitCombatAssetMatrix / VFXSFXList | GUID·clip·event·runtime 경로 |
| 현재 구현/수용 한계 | PROJECT_STATUS | 직접 근거가 연결된 Task/History |
| 미완·보류 | ROADMAP | 최신 사용자 결정·해제 조건 |
| 과거 경위 | Task/Log/History | 당시 범위로 읽고 현행 규칙과 구분 |

과거의 PASS는 새 리비전의 실행 증거가 아니다. 코드 존재·정적 검사·컴파일·Unity 실행·기기/멀티 실행·사용자 수용을 별도로 기록한다. 기존 수용을 취소하거나 과거 미검증 항목을 새 반복 필수관문으로 만들지 않는다. 순수한 구조 정리 중 새 사양·밸런스·전적 정책·프로토콜을 결정하지 않는다.

## 4. 초기 차이와 위험 장부

| ID | 직접 확인한 사실 또는 전달 범위 | 판정 | 연결·다음 조치 |
|---|---|---|---|
| AUD-001 | 초기 LoginUseCase의 구체 인증 서비스 의존과 허용 단정 헤더를 확인. Plan2.6으로 서비스 소비를 IAuthenticationService로 바꾸고 오류 정의를 Application으로 이관했으며 헤더를 실제 UGS 직접 후속 범위에 맞춤 | 서비스/오류 계약에 이어 UGS 직접 의존도 AUD-018 범위로 해소·정적 대조 완료; 전체 의미 감사/컴파일/실기 미완 | 세부 AUD-015 및 Research11.5/Plan7.7. TDD 계층 계약 정합화 범위이며 전체 인증/계층 감사 완료 아님 |
| AUD-002 | ProjectVersion6000.3.5f2와 달랐던 현행 TDD2곳·document-manager agent1곳·공용 메모리1곳 버전을 정확값으로 정정 | Plan2.9 지정 문서 사실 정정 완료; 실행/SDK 재검증 아님 | 과거 Task/History 및 릴리스 마케팅 명칭은 일괄 변경하지 않음. Research15.1/Plan7.15 |
| AUD-003 | 초기8,634행 측정 뒤 지정75메서드를3partial로 기계적 이동하고 main3,781행/기존4sourcegate의 class전체범위를 보존 | Plan2.17 소스 분리·메인 독립 raw대조 완료; actualcompile/실기 NOT RUN | 필드171순서/직렬화6/Unity callback4/157메서드원문 보존·신규6파일. Research7.5/16.8·Plan7.21 |
| AUD-004 | ROADMAP 전투·유닛 후속과 공용 메모리는 VFX 비활성 블록6곳 최종 삭제를 번개 사망 시 끝점 결함 사용자 테스트 이후로 유예 | 기존 삭제 보류 유지 | 2026-10-13/13_53_vfx-code-followups Plan 13-4 원본 확인 후 별도 범위로 관리; 이번 조사에서 삭제 금지 |
| AUD-005 | UnitRecipientFitMeasureTool의 현행 VFXSFXGuide 유지 설명과 VfxConnections301~303/330~331/337의 직접 측정/기준 호출 확인 | 현재 내부 의존 근거 확인·유지; 불필요/삭제 대상 판정 아님 | WORKFLOW 최소 진입점의 실제 내부 의존으로 구분. Research15절; 새 메뉴 실행/실기 검증 아님 |
| AUD-006 | 루트 parse_popup_temp.py는 Game.unity를 열어 RectTransform/MonoBehaviour 등을 해석하는 코드가 있음 | 헤더/일부 구현 열람; 미사용 미판정 | 실제 호출·최근 작업 사용을 확인. temp 이름은 삭제 근거가 아님 |
| AUD-007 | 기존 리팩토링·회귀수정·규칙감사·문서 역할정비 Task가 존재 | 기존 작업 경로 확인; 전체 대조 미완 | 아래 과거 작업 큐로 연결해 중복 작업/실패 재발 방지 |
| AUD-008 | 구현 전 IUnitView52~53행의 구체 팩토리 노출을 현재51~52행 IUnitFactory/IBuildingObjectProvider로 교체. UnitView 필드·인자 타입과 BuildingFactory 구현 선언도 변경 | 첫 묶음 구현·정적 대조 완료; 컴파일·실기 미실행 | 기존 조회·폴백·주입·이벤트 본문 보존 대조. Plan2.3/7절과 아래 증분 장부 연결. 전체 의존성 감사 완료는 아님 |
| AUD-009 | 초기 AI의 DifficultyParams/GameRaceContext 직접 참조를 과거 QA가 예외로 기록. Plan2.5로 종족 조회를 주입했고 Plan2.7로 Controller 난이도 타입을 Application 값으로 분리. 기존 직렬화 DifficultyParams는 Infrastructure에 유지 | 종족 조회·Controller 난이도 타입 결합의 지정 범위 해소; 전체 의미 감사/실기 완료 아님. 과거 QA는 사용자 정책 승인 단독 근거 아님 | 신규 세부 AUD-014와 Research10.4/Plan7.5 연결. 기존 QA 이력·난이도 직렬화 경계 보존 |
| AUD-010 | 프로그래머가 수행해 메인에게 전달한 Domain51/Application85 파일 댓글·문자열 제외 토큰 조사: Domain 금지 토큰0, Application 외부 토큰 후보5파일(IUnitView/LoginUseCase/AccountLinkUseCase/AIOpponentController/TimedDamageEffectPresentation) | 전달된 한정 검색 결과; 컴파일/전체 의존 PASS 아님. 메인은 일부 파일의 원본 대조 수행 | TimedDamageEffectPresentation의 Core import는 본문 타입 사용 미발견 후보. 실제 타입·조건부 경로·full qualification을 추가 대조 |
| AUD-011 | 실제 없는 root Diagnostics 안내를 Debug/UIManager 테스트 핸들러로 정정하고 Bootstrap/Debug의 에디터 맵 준비·투영 실패 플래그를 표시 | Plan2.9 폴더 안내 정정 완료; 코드/폴더 이동 없음 | 다른 Infrastructure/Presentation Debug 책임과 구분. Bootstrap partial4개 유지. Research15.1/Plan7.15 |
| AUD-012 | BuildingStats.cs45/48/159/170행 주석4곳을 현재 방어 타워의 피해 계산·공격 후 대기 시간 조회 용도로 현행화 | 소스 정리·정적 대조 완료; 컴파일·실기 미실행 | Buildings 「방어 타워 시스템」 규칙5/9, TowerCombatUseCase145/231, Setup155~182에 근거. 실행 토큰·리터럴 동일 확인; Research9.8/Plan7.3 |
| AUD-013 | TimedDamageEffectPresentation.cs 구현 전4행의 불필요 Core import1행 제거. UnityEngine.Time은 구현 전33행→현재32행에 그대로 존재 | 소스 정리·정적 대조 완료; 컴파일·실기 미실행 | 해당 using1행 제거 외 원문 정확 일치 확인. 시간·채널·이벤트·상태·로그 본문 유지; 전체 순수C# 전환 아님 |
| AUD-014 | AI 종족 조회를 필수 Func<RaceId> 주입으로 교체. Bootstrap의 live RedRace 람다와 기존4조회 사용점 유지 | Plan2.5 소스 정리·독립 정적 대조 완료; 컴파일/게임 실행 미실행 | 새 provider null 거부 계약, DifficultyParams 의존 유지. AUD-009의 부분 해소로 추적; 전체 AI/Auth 완료 아님 |
| AUD-015 | FirebaseAuthService가 신규 Application IAuthenticationService를 구현하고 Login/AccountLink field·ctor를 인터페이스로 변경. enum13순서와 AuthException body를 Application/AuthException.cs로 이관 | Plan2.6 소스 정리·메인 독립 정적 대조 완료; 실제 컴파일/실기 미실행 | 계약6읽기속성+12Task 일치·동일 concrete caller 유지. namespace 진단 표시 변화·UGS 직접 의존 잔여; AUD-001 부분 해소 |
| AUD-016 | Controller의 난이도 소비 타입을 Application.AIDifficultyParameters 불변9값으로 바꾸고 Bootstrap에서1대1 복사. 기존 직렬화 DifficultyParams/자산은 유지 | Plan2.7 소스 정리·메인 독립 정적 대조·AI 규칙4/33 구조 설명 동기화 완료; 컴파일/실기 미실행 | Research12.4/Plan7.10. 실제7읽기/Red수입 소비 유지. AUD-017 생산시간 배율 미연결은 별도 미해결 |
| AUD-017 | AI 규칙5는 productionTimeMultiplier 효과를 기재하지만 전체 Scripts 검색에서 코드 전 선언 AIConfig.cs48행1곳 외 소비 미발견. UnitProduction629/692는 기본 생산시간 조회 | 사용자 결정: 후속 AI 작업에서 구현, 이번 리팩토링에서는 보류. 효과 미연결·구현 미완 유지 | 이번 DTO에9값을 모두 담아도9동작 소비 증거가 아님. 규칙5 설계·수치는 유지하고 생산시간 배율 적용·삭제는 이번에 수행하지 않음 |
| AUD-018 | LoginUseCase의 UGS SDK 접근을 IUgsAuthenticationService/stateless adapter로 분리하고 명시 로그아웃 SDK 블록을 이관 | Plan2.8 소스 정리·메인 독립 정적 대조·Auth 구조3행 동기화 완료. 지정 namespace 검색 Domain51/App90 hit0; 전체 의미 감사/컴파일/실기 PASS 아님 | getter 평가 시점·직접 반환Task3·세션 정책·legacy comment 보존. 보간 치환 역복원 확인·예외 stack frame 변화 가능. Research13.6/Plan7.12 |
| AUD-019 | UnitView46행 interface 계약 주석 현행화. 중복 factory 조회2곳을 현재1892행 GetTargetObject helper로 추출 | Plan2.10 단일파일 소스 정리·메인 원문 전체 역복원 대조 완료; 실제 컴파일/Unity·게임 미실행 | 기존 getter1903/1917행과9caller·lookup1회·Unity null/폴백·meta유지. Research15.2/Plan7.16 |

| AUD-020 | Infra/Core 7파일의 좌표변환·방어타워 Inspector·시나리오 번들·토스트 폴백·.bytes 설명 충돌 | Plan2.12 설명 정리·메인 독립 정적 대조 완료; 실제 컴파일/실기 미실행 | Inspector8문자열 외 실행 토큰/리터럴 보존. 16.6절 |
| AUD-021 | BuildingData/UnitData의 현재 상태와 특수 공격 Registry/계약의 Bloom 힐러 분리 설명 | Plan2.13 지정4파일 주석 정리·메인 독립 정적 대조 완료; 실제 컴파일/실기 미실행 | 141파일 중 지정4주석 변경/137raw 동일·전체 토큰/리터럴 동일. 16.6절 |
| AUD-022 | Effects5파일의 VFX/SFX 책임·Tooltip/XML 설명과 RaceSelectionViewModel 불필요 import | Plan2.15 지정6파일 정리·메인 독립 정적 대조 완료; 실제 컴파일/실기 미실행 | Tooltip5문자열은 허용 변경·legacy6블록 보존. 16.6절 |
| AUD-023 | GameBootstrapper OnDestroy에 eager 재경로2이벤트 구독 해제가 없었음 | Plan2.16 Dispose/null 정리 추가·메인 전체 raw 역복원 대조 완료; 실제 컴파일/실기 미실행 | 예방 수명 정리이며 runtime 누수 재현 아님. 16.6절 |
| AUD-024 | Stats 감쇄율60~65%와 실제 고정65%의 미러 불일치 | Plan2.14 문서 정정 완료; 실제 컴파일/실기 미실행 | DamageCalculator/Upgrade/Units와 일치·밸런스 변경 없음. 16.7절 |
| AUD-025 | Stats Garage200/300과 씬 연결 직렬화100/100 불일치 | Plan2.14 문서 미러100/100 정정 완료; 실제 컴파일/실기 미실행 | Config→Game GUID→Setup 소비 직접 확인. 16.7절 |
| AUD-026 | MistShrine 미제작 VFX를 현재 시각 인지 근거로 단정 | Plan2.14 현재 미제작/제작 후 목표로2문장 정정 완료; 실제 컴파일/실기 미실행 | 별도 지속UI없음·아군패널 범위 정책 유지. 16.7절 |
| AUD-027 | AI 규칙32/35 public Current setter 예시가 실제 private setter/Set과 불일치 | Plan2.14 API 예시·Domain enum 귀속 정정 완료; 실제 컴파일/실기 미실행 | 실제 API/동작 변경 없음. 16.7절 |
| AUD-028 | 종족 시나리오3문서의 난이도는 delay/수입만이라는 설명에 승인 생산배율 설계 누락 | Plan2.14 각1문장 공통 규칙 참조로 정정 완료; 실제 컴파일/실기 미실행 | AUD-017 실제 효과 후속AI보류·시나리오 숫자 유지. 16.7절 |
| AUD-029 | Upgrade의 전체 UI 자동생성 골격·전체 다듬기 보류 주장이 최신 수용 범위와 충돌 | Plan2.14 현재 Status/수용Task 원본 참조로 정정 완료; 실제 컴파일/실기 미실행 | 헤더 아이콘 보류·전체기기 검증 미완 유지. 16.7절 |
| AUD-030 | Sound의 Splash 씬 위치 미결 상태가 Login 내부 Overlay 구성과 불일치 | Plan2.14 Login SplashOverlay/LoginBGM 설명 정정 완료; 실제 컴파일/실기 미실행 | 향후 별도씬은 별도 설계·동작 변경 없음. 16.7절 |
| AUD-031 | Sound 믹서 미연결 Debug.LogWarning 설명과 실제 GameLog.Dev.Warn 불일치 | Plan2.14 실제 개발 경고 API로 정정 완료; 실제 컴파일/실기 미실행 | 로그 코드/분류 변경 없음. 16.7절 |
| AUD-032 | Auth Refresh Token30일 비활성 만료 주장이 Firebase 공식 세션 문서와 상충 | Plan2.14 장기Refresh/무효화조건·ID token 약1시간으로 정정 완료; 실제 컴파일/실기 미실행 | 공식출처 직접 확인·서버30일정책/다기기규칙 변경 없음. 16.7절 |
| AUD-033 | Units규칙42 Inferno1초 marker 주장이 실제1.5초/Stats와 불일치 | Plan2.18 현행 Stats/감사표 참조로 정정; 실제 컴파일/실기 미실행 | Mushroom1초·권위/DoT 계약 유지. 16.10절 |
| AUD-034 | Matrix Mushroom0개/marker대기·새VFX전부미수용 주장이 실제marker와 기존부분승인에 뒤처짐 | Plan2.18 실제1초1개·기존impact/새poison프리팹 승인만 정정 | tracer·권위/ImpactHitRadius·전체lifecycle/migration OPEN 유지. 16.10절 |
| AUD-035 | TDD Application그림 순수C# 단정이 현재 허용 예외와 불일치 | Plan2.18 UseCase·계약 책임 설명으로 정정 | 새Unity허용정책·Domain계약 변경 없음. 16.10절 |

| AUD-036 | 생산 UI 재초기화 때 버튼/전역 구독 중복 및 Ticker8구독 소유권 분산 | Plan2.19 지정3파일 구현·정적 대조 완료; 실제 컴파일/runtime 재현 NOT RUN | 수명1회바인딩·전체Composite Clear재바인딩/파괴해제. 16.11절 |
| AUD-037 | 패널 닫힘/건물 대상 교체/disable 뒤 stale 누르기가 남을 위험 | Plan2.19 ProductionPanelUI reset·live guard 구현/정적 확인 | 예방 위험 정리이며 실제 오생산 재현/수정실기 PASS 아님. 16.11절 |
| AUD-038 | 결과 도달 probe 대기 동안 갱신된 HB를 재검사하지 않고 옛침묵값으로 이탈판정 | Plan2.20 최신수명flags/HB 재검사 구현·정적 확인 | tick1/HB3/retry10/timeout30 유지; 실제race 재현 NOT RUN. 16.11절 |
| AUD-039 | Editor 최초실패 안내상수가 Host재시도/Client화면유지라는 옛사양을 출력 | Plan2.21 M-2 양쪽로비즉시복귀/확인1개로 상수정정 | 실제실패주입/메뉴예약/설정 변경없음·메뉴실행 NOT RUN. 16.11절 |

초기 코드 검색의 `using` 히트는 실제 타입 사용·수명·승인된 예외를 확정하지 않는다. 전체 위반 건수나 죽은 코드 건수는 아직 산출하지 않았다.

## 5. 과거 작업 연결 큐

| 기존 작업 | 이번에 확인한 범위 | 이번 작업에 반영할 내용 |
|---|---|---|
| 2026-05-19/10_46_code-refactoring/Plan.md | 메인 전달: 처음100행 일부 열람 | 당시 6그룹·보존·삭제계획은 현재 적용 여부를 재확인; 목록 재실행 금지 |
| 2026-05-31/19_42_refactoring-regression-fix/Research.md | 메인 전달: 처음155행 일부 열람 | 옛 FindFirst 캐시 회귀 기록; 현재 같은 결함이 있다는 뜻 아님 |
| 2026-10-01/05_19_rules-implementation-audit/Research.md | 메인 전달: 처음100행 일부 열람 | 한 셀 다중항목·명칭불일치·같은 기능 다른행·편중갱신으로 근거가 안 읽힌 교훈. 한 행 한 문제와 양방향 연결 적용 |
| 2026-10-16/14_25_docs-role-maintenance/Research.md | 메인 전달: 1~75행 일부 열람 | 현행 문서 역할 변경의 근거를 연결; 전체 재독 완료 아님 |

추가 연결: 2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Testcase.md159~165/182행은 문서 담당도 직접 읽었다. 2026-05-19/10_46_code-refactoring/Plan.md551~579행의 IUnitView 단계적 도입 기록은 프로그래머 전달 범위이며 전체 재독 완료가 아니다.

전체 기존 Task·Log 경로는 아래 장부에 남긴다. 기능별 현행 원본에서 연결된 관련 과거 문서를 우선 읽고, 미조사 이력은 숨기거나 조사 완료로 바꾸지 않는다. 역사 문서의 전체 정독 완료와 현행 구현 감사 완료는 별개다.

## 6. 실행·검사 근거 및 한계

- 문서 생성 전 메인이 `python3 Tools/check_docs.py`를 실행해 exit0·문제0을 확인했다고 전달했다. 신규 Task는 검사 제외이므로 이 결과는 이 Research/Plan 본문의 사실·링크 정합성을 보증하지 않는다.
- 기존 memory baseline 증가 advisory(game-programmer +27행, qa +22행)는 메인 전달 값이며 이번 문서2개 범위 밖 기존 상태다. baseline 파일은 변경하지 않는다.
- Unity/dotnet/mcs/mono 실행 파일은 메인 초기 PATH 조사에서 확인되지 않았다. 이것은 가능한 모든 설치 경로를 조사한 결과나 실행 PASS가 아니다.
- 초기 조사·계획 작성 단계에서는 Research/Plan 두 파일만 변경했다. 이후 사용자 승인으로 Plan2.3의 기존 C#3개·신규 C#1개·신규 meta1개 구현 범위가 추가됐다. 에셋·패키지·상시문서·메모리·기존 Task 변경과 TC/QA·Unity 메뉴·빌드·로그 보관/업로드/삭제는 승인 범위에 포함하지 않는다.
- 첫 구현 전 문서 작성 단계에서 메인이 `python3 Tools/check_docs.py`를 재실행해 exit0·문제0을 확인했다. 기존 memory baseline +49행 advisory는 변화 없으며 baseline 파일은 변경하지 않았다.
- 첫 구현 전 신규 Task에 대해서는 메인이 검사기의 markdown_links/anchors 함수를 활용한 별도 검사로 Research 로컬1073링크/Plan1링크의 대상 존재와 앵커오류0을 확인했다. 이는 링크 검사 결과이며 문서의 의미·구현 일치를 보증하지 않는다.
- 메인이 시작 inventory와 대조해 runtime310/editor8/상시문서48/기존Task612/Log10/Python5/운영지침4/설정32/package2/관리 .claude41의 장부 목록누락0을 확인했다. **첫 구현 전 검사**에서 시작 기준 Assets 전체 C#328개 SHA256 변화0도 확인했다. 이는 구현 후 코드 무변경 주장으로 재사용하지 않는다.

### 6.1 첫 구현 이후 메인 독립 검토(2026-10-23)

- 메인이 git diff를 직접 읽고 git show 원본 SHA가 시작 snapshot과 일치함을 확인한 뒤, 문자열 리터럴을 보존하고 주석·공백을 제외한 토큰을 대조했다. 기존 변경3파일은 승인된 타입 교체·class 인터페이스 구현 선언을 제외한 토큰이 동일했다. 이는 정적 본문 보존 근거이며 컴파일/실행 PASS가 아니다.
- 시작 Assets 전체 C#328개 중 변경3개, 무변경325개다. UnitFactory.cs도 무변경이며 기존296/406행의 호출은 같은 팩토리 객체를 전달한다. 신규 IBuildingObjectProvider.cs1개로 프로젝트 스크립트는 시작310→현재311개다. 기존 Task612+이번 문서2=현재614개다.
- 메인은 조회의 null 전파(?.), 반환 GameObject의 Unity null 체크, position+forward 폴백, null Transform 반환과 이벤트 구독·초기화 본문이 유지됨을 확인했다.
- 신규 meta GUID는 `8c3cd82964bd4e939d5d7d036a2ec75d`이며, Assets 전체 meta 검색에서 신규 파일 자신1건만 확인했다. `git diff --check`는0이었다.
- 문서 담당도 새 인터페이스14/22행, 신규 meta, IUnitView51~52행, UnitView205/208/1688행의 실제 타입을 직접 읽었다. 메인 토큰 대조·해시 수치는 전달 근거로 구분한다.
- Unity/C# 실행 도구를 확인하지 못해 실제 컴파일·Unity import·실기·빌드는 미실행이다. 전체 의존성 감사·죽은 코드 정리·사용자 테스트 완료로 확대하지 않는다. 상시문서/메모리 갱신은 별도 단계이며 이번에는 수행하지 않는다.
- 증분 반영 후 메인이 최종 검사해 Research 로컬1075링크/Plan1링크 오류0, 시작 장부 모든 범위 누락0, `python3 Tools/check_docs.py` exit0·문제0, `git diff --check`0과 예상7파일 외 변경 없음을 확인했다. 앞선 구현 전 링크 수치는 당시 검사 기록으로 보존한다.

## 7. 파일별 조사 장부

아래 목록은 시작 기준의 경로를 빠짐없이 보존한다. 각 경로를 없애지 않고 상태와 항목 ID를 갱신한다. 작업 중 추가 파일은 별도 증분 행으로 편입하고 기준일·이유를 기록한다.

<details>
<summary>프로젝트 C# — 310개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Assets/_Project/Scripts/Application/Combat/AttackDamageApplyStatus.cs](<../../../../Scripts/Application/Combat/AttackDamageApplyStatus.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs](<../../../../Scripts/Application/Combat/AttackEffectPresentation.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/BlastAttackBehavior.cs](<../../../../Scripts/Application/Combat/BlastAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/BloomHealEffectPresentation.cs](<../../../../Scripts/Application/Combat/BloomHealEffectPresentation.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/ISpecialAttackBehavior.cs](<../../../../Scripts/Application/Combat/ISpecialAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 · Plan2.13 지정정리/정적 대조 완료(AUD-021); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Application/Combat/InfernoAttackBehavior.cs](<../../../../Scripts/Application/Combat/InfernoAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs](<../../../../Scripts/Application/Combat/PistoleerTimingDiagnostics.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/QuakeAttackBehavior.cs](<../../../../Scripts/Application/Combat/QuakeAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/AuthoritativeAttackPresentationCoordinator.cs](<../../../../Scripts/Application/Combat/Sequencing/AuthoritativeAttackPresentationCoordinator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionContracts.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitActionContracts.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionPoseContracts.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitActionPoseContracts.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitActionSequencer.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitActionSequencing.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitActionSequencing.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackShadowProfile.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitAttackShadowProfile.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackStartGate.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitAttackStartGate.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitHealCastPresentation.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs](<../../../../Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/SpecialAttackContext.cs](<../../../../Scripts/Application/Combat/SpecialAttackContext.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/SpecialAttackRegistry.cs](<../../../../Scripts/Application/Combat/SpecialAttackRegistry.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 · Plan2.13 지정정리/정적 대조 완료(AUD-021); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Application/Combat/SweepAttackBehavior.cs](<../../../../Scripts/Application/Combat/SweepAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs](<../../../../Scripts/Application/Combat/TimedDamageEffectPresentation.cs>) | Plan2.4 using1행 정리·정적 대조 완료 · AUD-013 · 컴파일/실기 미실행·전체 파일 의미 감사 아님 |
| [Assets/_Project/Scripts/Application/Combat/TorrentAttackBehavior.cs](<../../../../Scripts/Application/Combat/TorrentAttackBehavior.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/UnitHealCastDiagnostics.cs](<../../../../Scripts/Application/Combat/UnitHealCastDiagnostics.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/WavePresentationContracts.cs](<../../../../Scripts/Application/Combat/WavePresentationContracts.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/WavePresentationCoordinator.cs](<../../../../Scripts/Application/Combat/WavePresentationCoordinator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/WavePresentationDiagnostics.cs](<../../../../Scripts/Application/Combat/WavePresentationDiagnostics.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Combat/WaveSpawnRequest.cs](<../../../../Scripts/Application/Combat/WaveSpawnRequest.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Events/GameEvents.cs](<../../../../Scripts/Application/Events/GameEvents.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Events/ToastKey.cs](<../../../../Scripts/Application/Events/ToastKey.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/GameLog.cs](<../../../../Scripts/Application/GameLog.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IEntityPositionProvider.cs](<../../../../Scripts/Application/Interfaces/IEntityPositionProvider.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IForfeitService.cs](<../../../../Scripts/Application/Interfaces/IForfeitService.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IGameServices.cs](<../../../../Scripts/Application/Interfaces/IGameServices.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IHexCoordinateMapper.cs](<../../../../Scripts/Application/Interfaces/IHexCoordinateMapper.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/ILeaderboardService.cs](<../../../../Scripts/Application/Interfaces/ILeaderboardService.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs](<../../../../Scripts/Application/Interfaces/ILogSink.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IMapFallbackTemplateSource.cs](<../../../../Scripts/Application/Interfaces/IMapFallbackTemplateSource.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/INetworkMistShrineController.cs](<../../../../Scripts/Application/Interfaces/INetworkMistShrineController.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/INetworkSkillController.cs](<../../../../Scripts/Application/Interfaces/INetworkSkillController.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IPlayerProfileService.cs](<../../../../Scripts/Application/Interfaces/IPlayerProfileService.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/ISkillDataProvider.cs](<../../../../Scripts/Application/Interfaces/ISkillDataProvider.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Interfaces/IUnitFactory.cs](<../../../../Scripts/Application/Interfaces/IUnitFactory.cs>) | 일부 열람 · Plan2.3 관련 타입/조회/주입 범위 · AUD-008 |
| [Assets/_Project/Scripts/Application/Interfaces/IUnitView.cs](<../../../../Scripts/Application/Interfaces/IUnitView.cs>) | Plan2.3 변경 범위 구현/정적 대조 완료 · AUD-008 · 전체 파일 의미 감사·실행 완료 아님 |
| [Assets/_Project/Scripts/Application/MapHandoff.cs](<../../../../Scripts/Application/MapHandoff.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Models/LeaderboardEntry.cs](<../../../../Scripts/Application/Models/LeaderboardEntry.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Models/PlayerProfileData.cs](<../../../../Scripts/Application/Models/PlayerProfileData.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/NetworkContext.cs](<../../../../Scripts/Application/NetworkContext.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/AIOpponentController.cs](<../../../../Scripts/Application/Services/AIOpponentController.cs>) | Plan2.5/2.7 관련 범위 소스/정적 대조 완료 · AUD-014/016 · 전체 파일 의미 감사·컴파일/실기 미완 |
| [Assets/_Project/Scripts/Application/Services/CongestionAwarePathfinder.cs](<../../../../Scripts/Application/Services/CongestionAwarePathfinder.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/CongestionMap.cs](<../../../../Scripts/Application/Services/CongestionMap.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/FlowFieldService.cs](<../../../../Scripts/Application/Services/FlowFieldService.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/GameServicesLocator.cs](<../../../../Scripts/Application/Services/GameServicesLocator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/StatusEffectSystem.cs](<../../../../Scripts/Application/Services/StatusEffectSystem.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Services/TileOwnershipService.cs](<../../../../Scripts/Application/Services/TileOwnershipService.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/AreaDotDamageExecutor.cs](<../../../../Scripts/Application/Skill/AreaDotDamageExecutor.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/GlobalStatusChangeExecutor.cs](<../../../../Scripts/Application/Skill/GlobalStatusChangeExecutor.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/ISkillExecutor.cs](<../../../../Scripts/Application/Skill/ISkillExecutor.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/InstantAreaDamageExecutor.cs](<../../../../Scripts/Application/Skill/InstantAreaDamageExecutor.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/SkillActivationContext.cs](<../../../../Scripts/Application/Skill/SkillActivationContext.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/SkillData.cs](<../../../../Scripts/Application/Skill/SkillData.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/Skill/SkillExecutorRegistry.cs](<../../../../Scripts/Application/Skill/SkillExecutorRegistry.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs](<../../../../Scripts/Application/UseCases/AccountLinkUseCase.cs>) | Plan2.6 서비스/오류 계약 정리 범위 구현·정적 대조 완료 · AUD-015 · 전체 의미 감사/컴파일/실기 미완 |
| [Assets/_Project/Scripts/Application/UseCases/BuildingPlacementUseCase.cs](<../../../../Scripts/Application/UseCases/BuildingPlacementUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/GameEndUseCase.cs](<../../../../Scripts/Application/UseCases/GameEndUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/GridInteractionUseCase.cs](<../../../../Scripts/Application/UseCases/GridInteractionUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs](<../../../../Scripts/Application/UseCases/LoginUseCase.cs>) | Plan2.6 인증 계약/2.8 UGS 접근 범위 소스·독립 정적 대조 완료 · AUD-015/018 · 전체 의미 감사/컴파일/실기 미완 |
| [Assets/_Project/Scripts/Application/UseCases/MapPreparationUseCase.cs](<../../../../Scripts/Application/UseCases/MapPreparationUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/MapProjectionUseCase.cs](<../../../../Scripts/Application/UseCases/MapProjectionUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/MapVerificationUseCase.cs](<../../../../Scripts/Application/UseCases/MapVerificationUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/MistShrineUseCase.cs](<../../../../Scripts/Application/UseCases/MistShrineUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/PlayerProfileUseCase.cs](<../../../../Scripts/Application/UseCases/PlayerProfileUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/PopulationUseCase.cs](<../../../../Scripts/Application/UseCases/PopulationUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/RankingUseCase.cs](<../../../../Scripts/Application/UseCases/RankingUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/ResourceUseCase.cs](<../../../../Scripts/Application/UseCases/ResourceUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs](<../../../../Scripts/Application/UseCases/SkillActivationUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs](<../../../../Scripts/Application/UseCases/TowerCombatUseCase.cs>) | 일부 열람 · 129~152/220~242행 · AUD-012 |
| [Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.TorrentLaunch.cs](<../../../../Scripts/Application/UseCases/UnitCombatUseCase.TorrentLaunch.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs](<../../../../Scripts/Application/UseCases/UnitCombatUseCase.WavePresentation.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs](<../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs](<../../../../Scripts/Application/UseCases/UnitMovementUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitProductionUseCase.cs](<../../../../Scripts/Application/UseCases/UnitProductionUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitSpawnUseCase.cs](<../../../../Scripts/Application/UseCases/UnitSpawnUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Application/UseCases/UnitUpgradeUseCase.cs](<../../../../Scripts/Application/UseCases/UnitUpgradeUseCase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Bootstrap/Debug/ForcedMapPreparationFailure.cs](<../../../../Scripts/Bootstrap/Debug/ForcedMapPreparationFailure.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Bootstrap/Debug/ForcedMapProjectionFailure.cs](<../../../../Scripts/Bootstrap/Debug/ForcedMapProjectionFailure.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Map.cs](<../../../../Scripts/Bootstrap/GameBootstrapper.Map.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Network.cs](<../../../../Scripts/Bootstrap/GameBootstrapper.Network.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs](<../../../../Scripts/Bootstrap/GameBootstrapper.Setup.cs>) | Plan2.5/2.7 관련 범위 소스/정적 대조 완료 · AUD-014/016 · 전체 파일 의미 감사·컴파일/실기 미완 |
| [Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs](<../../../../Scripts/Bootstrap/GameBootstrapper.cs>) | 목록 확보 · 내용 미조사 · Plan2.16 지정정리/정적 대조 완료(AUD-023); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs](<../../../../Scripts/Bootstrap/LoginBootstrapper.cs>) | Plan2.8 LoginUseCase UGS adapter 생성자 주입 범위 소스·정적 대조 완료 · AUD-018 · 전체 파일 의미 감사/실기 미완 |
| [Assets/_Project/Scripts/Core/HexMetrics.cs](<../../../../Scripts/Core/HexMetrics.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Core/HexMetricsCoordinateMapper.cs](<../../../../Scripts/Core/HexMetricsCoordinateMapper.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Core/SingletonMonoBehaviour.cs](<../../../../Scripts/Core/SingletonMonoBehaviour.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Core/ViewConverter.cs](<../../../../Scripts/Core/ViewConverter.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Debug/UIManagerTestButtonHandler.cs](<../../../../Scripts/Debug/UIManagerTestButtonHandler.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Domain/AI/BuildOrderStep.cs](<../../../../Scripts/Domain/AI/BuildOrderStep.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/AI/DifficultyLevel.cs](<../../../../Scripts/Domain/AI/DifficultyLevel.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Building/BuildingData.cs](<../../../../Scripts/Domain/Building/BuildingData.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 · Plan2.13 지정정리/정적 대조 완료(AUD-021); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Domain/Building/BuildingStats.cs](<../../../../Scripts/Domain/Building/BuildingStats.cs>) | Plan2.4 주석4곳 정리·정적 대조 완료 · AUD-012 · 실행토큰/리터럴 보존·전체 의미 감사/실기 완료 아님 |
| [Assets/_Project/Scripts/Domain/Building/BuildingType.cs](<../../../../Scripts/Domain/Building/BuildingType.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Building/BuildingTypeHelper.cs](<../../../../Scripts/Domain/Building/BuildingTypeHelper.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Building/ProductionState.cs](<../../../../Scripts/Domain/Building/ProductionState.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Combat/DamageCalculator.cs](<../../../../Scripts/Domain/Combat/DamageCalculator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Common/IDamageable.cs](<../../../../Scripts/Domain/Common/IDamageable.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Common/RaceId.cs](<../../../../Scripts/Domain/Common/RaceId.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Common/TeamId.cs](<../../../../Scripts/Domain/Common/TeamId.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexCoord.cs](<../../../../Scripts/Domain/Hex/HexCoord.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexDirection.cs](<../../../../Scripts/Domain/Hex/HexDirection.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexFlowField.cs](<../../../../Scripts/Domain/Hex/HexFlowField.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexGrid.cs](<../../../../Scripts/Domain/Hex/HexGrid.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexOrientation.cs](<../../../../Scripts/Domain/Hex/HexOrientation.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs](<../../../../Scripts/Domain/Hex/HexPathfinder.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/HexTile.cs](<../../../../Scripts/Domain/Hex/HexTile.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/MineKind.cs](<../../../../Scripts/Domain/Hex/MineKind.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Hex/TileKind.cs](<../../../../Scripts/Domain/Hex/TileKind.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/DecorationDefinition.cs](<../../../../Scripts/Domain/Map/DecorationDefinition.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/CanyonGenerator.cs](<../../../../Scripts/Domain/Map/Generators/CanyonGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/IMapArchetypeGenerator.cs](<../../../../Scripts/Domain/Map/Generators/IMapArchetypeGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/MapArchetypeGeneratorBase.cs](<../../../../Scripts/Domain/Map/Generators/MapArchetypeGeneratorBase.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/MapBandTable.cs](<../../../../Scripts/Domain/Map/Generators/MapBandTable.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/NeutralMineSampler.cs](<../../../../Scripts/Domain/Map/Generators/NeutralMineSampler.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/ObstacleOpenGenerator.cs](<../../../../Scripts/Domain/Map/Generators/ObstacleOpenGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/OpenGenerator.cs](<../../../../Scripts/Domain/Map/Generators/OpenGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/OuterGenerator.cs](<../../../../Scripts/Domain/Map/Generators/OuterGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/Generators/ThreeLaneGenerator.cs](<../../../../Scripts/Domain/Map/Generators/ThreeLaneGenerator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/InitialMapStateEvaluator.cs](<../../../../Scripts/Domain/Map/InitialMapStateEvaluator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapDefinition.cs](<../../../../Scripts/Domain/Map/MapDefinition.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapDefinitionCodec.cs](<../../../../Scripts/Domain/Map/MapDefinitionCodec.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapDefinitionValidator.cs](<../../../../Scripts/Domain/Map/MapDefinitionValidator.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapFallbackTemplateFactory.cs](<../../../../Scripts/Domain/Map/MapFallbackTemplateFactory.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapRandom.cs](<../../../../Scripts/Domain/Map/MapRandom.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapRandomStreams.cs](<../../../../Scripts/Domain/Map/MapRandomStreams.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapRootSeed.cs](<../../../../Scripts/Domain/Map/MapRootSeed.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/MapType.cs](<../../../../Scripts/Domain/Map/MapType.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Map/SymmetricMapBuilder.cs](<../../../../Scripts/Domain/Map/SymmetricMapBuilder.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Skill/SkillAimType.cs](<../../../../Scripts/Domain/Skill/SkillAimType.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Skill/SkillMechanicType.cs](<../../../../Scripts/Domain/Skill/SkillMechanicType.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Status/StatusEffect.cs](<../../../../Scripts/Domain/Status/StatusEffect.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Status/StatusEffectKind.cs](<../../../../Scripts/Domain/Status/StatusEffectKind.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Status/UnitStatusState.cs](<../../../../Scripts/Domain/Status/UnitStatusState.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Unit/FacingDirection.cs](<../../../../Scripts/Domain/Unit/FacingDirection.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Unit/UnitData.cs](<../../../../Scripts/Domain/Unit/UnitData.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 · Plan2.13 지정정리/정적 대조 완료(AUD-021); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs](<../../../../Scripts/Domain/Unit/UnitProductionStats.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Unit/UnitStats.cs](<../../../../Scripts/Domain/Unit/UnitStats.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Unit/UnitType.cs](<../../../../Scripts/Domain/Unit/UnitType.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Domain/Unit/UpgradeGroup.cs](<../../../../Scripts/Domain/Unit/UpgradeGroup.cs>) | 토큰 조사(전달) · 의미·참조 전체 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs](<../../../../Scripts/Infrastructure/Auth/FirebaseAuthService.cs>) | Plan2.6 서비스/오류 계약 정리 범위 구현·정적 대조 완료 · AUD-015 · 전체 의미 감사/컴파일/실기 미완 |
| [Assets/_Project/Scripts/Infrastructure/Cloud/InternetReachabilityProbe.cs](<../../../../Scripts/Infrastructure/Cloud/InternetReachabilityProbe.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Cloud/LeaderboardService.cs](<../../../../Scripts/Infrastructure/Cloud/LeaderboardService.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Cloud/PlayerProfileService.cs](<../../../../Scripts/Infrastructure/Cloud/PlayerProfileService.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/AIConfig.cs](<../../../../Scripts/Infrastructure/Config/AIConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/AIScenarioConfig.cs](<../../../../Scripts/Infrastructure/Config/AIScenarioConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/Config/BuildingStatsConfig.cs](<../../../../Scripts/Infrastructure/Config/BuildingStatsConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/Config/GameConfig.cs](<../../../../Scripts/Infrastructure/Config/GameConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/ResourcesMapFallbackTemplateSource.cs](<../../../../Scripts/Infrastructure/Config/ResourcesMapFallbackTemplateSource.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/Config/SkillDefinition.cs](<../../../../Scripts/Infrastructure/Config/SkillDefinition.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/SkillLoadoutConfig.cs](<../../../../Scripts/Infrastructure/Config/SkillLoadoutConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/SoundConfig.cs](<../../../../Scripts/Infrastructure/Config/SoundConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/SpecialAttackConfig.cs](<../../../../Scripts/Infrastructure/Config/SpecialAttackConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/ToastMessageConfig.cs](<../../../../Scripts/Infrastructure/Config/ToastMessageConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/Config/UIColorConfig.cs](<../../../../Scripts/Infrastructure/Config/UIColorConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Config/UnitStatsConfig.cs](<../../../../Scripts/Infrastructure/Config/UnitStatsConfig.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/ConsoleSink.cs](<../../../../Scripts/Infrastructure/Debug/ConsoleSink.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/FileSink.cs](<../../../../Scripts/Infrastructure/Debug/FileSink.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/ForcedAcceptSendFailure.cs](<../../../../Scripts/Infrastructure/Debug/ForcedAcceptSendFailure.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/ForcedMapTransferFailure.cs](<../../../../Scripts/Infrastructure/Debug/ForcedMapTransferFailure.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/LogSessionOwner.cs](<../../../../Scripts/Infrastructure/Debug/LogSessionOwner.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs](<../../../../Scripts/Infrastructure/Debug/RuntimeLogger.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs](<../../../../Scripts/Infrastructure/Factories/BuildingFactory.cs>) | Plan2.3 변경 범위 구현/정적 대조 완료 · AUD-008 · 전체 파일 의미 감사·실행 완료 아님 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs](<../../../../Scripts/Infrastructure/Factories/UnitFactory.cs>) | 목록 확보 · 내용 미조사 · Plan2.12 지정정리/정적 대조 완료(AUD-020); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Infrastructure/GameRaceContext.cs](<../../../../Scripts/Infrastructure/GameRaceContext.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/LocalPlayerDifficulty.cs](<../../../../Scripts/Infrastructure/LocalPlayerDifficulty.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/LocalPlayerRace.cs](<../../../../Scripts/Infrastructure/LocalPlayerRace.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/LobbyManager.cs](<../../../../Scripts/Infrastructure/Network/LobbyManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/LocalPlayerTeam.cs](<../../../../Scripts/Infrastructure/Network/LocalPlayerTeam.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/MapChunkAssembler.cs](<../../../../Scripts/Infrastructure/Network/MapChunkAssembler.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/MatchmakerManager.cs](<../../../../Scripts/Infrastructure/Network/MatchmakerManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs](<../../../../Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs](<../../../../Scripts/Infrastructure/Network/NetworkBloomHealEffectState.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkBuildingController.cs](<../../../../Scripts/Infrastructure/Network/NetworkBuildingController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.Waves.cs](<../../../../Scripts/Infrastructure/Network/NetworkCombatController.Waves.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs](<../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs](<../../../../Scripts/Infrastructure/Network/NetworkGameEndController.cs>) | 목록 확보 · 내용 미조사 · Plan2.20 probe뒤최신HB/수명재검사 구현·정적대조 완료 · AUD-038 · 전체감사/실기 미완 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs](<../../../../Scripts/Infrastructure/Network/NetworkGameFlow.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs](<../../../../Scripts/Infrastructure/Network/NetworkGameManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs](<../../../../Scripts/Infrastructure/Network/NetworkHealthSync.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkMapTransfer.cs](<../../../../Scripts/Infrastructure/Network/NetworkMapTransfer.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkMistShrineController.cs](<../../../../Scripts/Infrastructure/Network/NetworkMistShrineController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs](<../../../../Scripts/Infrastructure/Network/NetworkProductionController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkResourceSync.cs](<../../../../Scripts/Infrastructure/Network/NetworkResourceSync.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs](<../../../../Scripts/Infrastructure/Network/NetworkSkillController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkTileSync.cs](<../../../../Scripts/Infrastructure/Network/NetworkTileSync.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs](<../../../../Scripts/Infrastructure/Network/NetworkTimedDamageEffectState.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs](<../../../../Scripts/Infrastructure/Network/NetworkUnit.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs](<../../../../Scripts/Infrastructure/Network/NetworkUnitActionShadowState.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitHealCastState.cs](<../../../../Scripts/Infrastructure/Network/NetworkUnitHealCastState.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkUnitMovementController.cs](<../../../../Scripts/Infrastructure/Network/NetworkUnitMovementController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs](<../../../../Scripts/Infrastructure/Network/NetworkUpgradeController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs](<../../../../Scripts/Infrastructure/Network/ReconnectionHandler.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/RelayManager.cs](<../../../../Scripts/Infrastructure/Network/RelayManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/TileOwnershipData.cs](<../../../../Scripts/Infrastructure/Network/TileOwnershipData.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs](<../../../../Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs](<../../../../Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs](<../../../../Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitMovementAuthorityObserver.cs](<../../../../Scripts/Infrastructure/Network/UnitMovementAuthorityObserver.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitMovementShadowObserver.cs](<../../../../Scripts/Infrastructure/Network/UnitMovementShadowObserver.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnitRootPoseConsistencyObserver.cs](<../../../../Scripts/Infrastructure/Network/UnitRootPoseConsistencyObserver.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/Network/UnityServicesInitializer.cs](<../../../../Scripts/Infrastructure/Network/UnityServicesInitializer.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Infrastructure/UnitWorldPositionProvider.cs](<../../../../Scripts/Infrastructure/UnitWorldPositionProvider.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs](<../../../../Scripts/Presentation/Audio/AudioManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Camera/CameraController.cs](<../../../../Scripts/Presentation/Camera/CameraController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Debug/DebugUI.cs](<../../../../Scripts/Presentation/Debug/DebugUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/BloomHealEffectPresenter.cs](<../../../../Scripts/Presentation/Effects/BloomHealEffectPresenter.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs](<../../../../Scripts/Presentation/Effects/BuildingEffectConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs](<../../../../Scripts/Presentation/Effects/EffectManager.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs](<../../../../Scripts/Presentation/Effects/EffectPreset.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs](<../../../../Scripts/Presentation/Effects/HitPresentationQueue.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/HitReactionPunch.cs](<../../../../Scripts/Presentation/Effects/HitReactionPunch.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/MistShrineRangeIndicator.cs](<../../../../Scripts/Presentation/Effects/MistShrineRangeIndicator.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs](<../../../../Scripts/Presentation/Effects/SkillAimReticle.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs](<../../../../Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/TracerProjectile.cs](<../../../../Scripts/Presentation/Effects/TracerProjectile.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/UiEffectConfig.cs](<../../../../Scripts/Presentation/Effects/UiEffectConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/Effects/UnitAttackResultPresentationShadow.cs](<../../../../Scripts/Presentation/Effects/UnitAttackResultPresentationShadow.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs](<../../../../Scripts/Presentation/Effects/UnitEffectConfig.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs](<../../../../Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs](<../../../../Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs](<../../../../Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs](<../../../../Scripts/Presentation/Effects/VfxPoolItem.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/WavePresentationPresenter.cs](<../../../../Scripts/Presentation/Effects/WavePresentationPresenter.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Effects/WaveVisualCalibration.cs](<../../../../Scripts/Presentation/Effects/WaveVisualCalibration.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs](<../../../../Scripts/Presentation/Grid/HexGridRenderer.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Grid/HexTileView.cs](<../../../../Scripts/Presentation/Grid/HexTileView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Input/InputHandler.cs](<../../../../Scripts/Presentation/Input/InputHandler.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Input/SkillAimController.cs](<../../../../Scripts/Presentation/Input/SkillAimController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs](<../../../../Scripts/Presentation/Production/ProductionTicker.cs>) | 목록 확보 · 내용 미조사 · Plan2.19 전체8Rx소유권/수명1회연결 구현·정적대조 완료 · AUD-036 · 전체감사/실기 미완 |
| [Assets/_Project/Scripts/Presentation/UI/BuildingActionPanelUI.cs](<../../../../Scripts/Presentation/UI/BuildingActionPanelUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs](<../../../../Scripts/Presentation/UI/BuildingPanelBase.cs>) | 목록 확보 · 내용 미조사 · Plan2.19 자체2listener 중복방지 구현·정적대조 완료 · AUD-036 · 전체감사/실기 미완 |
| [Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs](<../../../../Scripts/Presentation/UI/BuildingPlacementUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs](<../../../../Scripts/Presentation/UI/BuildingSkillPanelUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs](<../../../../Scripts/Presentation/UI/Common/AnimatedPanel.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs](<../../../../Scripts/Presentation/UI/Common/FloatingHpText.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/LoadingScreen.cs](<../../../../Scripts/Presentation/UI/Common/LoadingScreen.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/RematchRequestPopup.cs](<../../../../Scripts/Presentation/UI/Common/RematchRequestPopup.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/SafeAreaFitter.cs](<../../../../Scripts/Presentation/UI/Common/SafeAreaFitter.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/SharedBackgroundButton.cs](<../../../../Scripts/Presentation/UI/Common/SharedBackgroundButton.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/SpinnerRotator.cs](<../../../../Scripts/Presentation/UI/Common/SpinnerRotator.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/ToastUI.cs](<../../../../Scripts/Presentation/UI/Common/ToastUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs](<../../../../Scripts/Presentation/UI/Common/UIAnimator.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Common/VolumeControlBinder.cs](<../../../../Scripts/Presentation/UI/Common/VolumeControlBinder.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ConfirmPopup.cs](<../../../../Scripts/Presentation/UI/ConfirmPopup.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ContinuousInputField.cs](<../../../../Scripts/Presentation/UI/ContinuousInputField.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Core/IGameUI.cs](<../../../../Scripts/Presentation/UI/Core/IGameUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Core/IUIManager.cs](<../../../../Scripts/Presentation/UI/Core/IUIManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Core/IView.cs](<../../../../Scripts/Presentation/UI/Core/IView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs](<../../../../Scripts/Presentation/UI/FloatingHpTextSpawner.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/GameEndUI.cs](<../../../../Scripts/Presentation/UI/GameEndUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/GameHudUI.cs](<../../../../Scripts/Presentation/UI/GameHudUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/GameUIManager.cs](<../../../../Scripts/Presentation/UI/GameUIManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/InGameProfileSummaryView.cs](<../../../../Scripts/Presentation/UI/InGameProfileSummaryView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs](<../../../../Scripts/Presentation/UI/InGameSettingsUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/InputFormController.cs](<../../../../Scripts/Presentation/UI/InputFormController.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/InputFormPointerRelay.cs](<../../../../Scripts/Presentation/UI/InputFormPointerRelay.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/LobbySettingsView.cs](<../../../../Scripts/Presentation/UI/LobbySettingsView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs](<../../../../Scripts/Presentation/UI/LobbyUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/MistShrinePanelUI.cs](<../../../../Scripts/Presentation/UI/MistShrinePanelUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/MobileKeyboardAvoidance.cs](<../../../../Scripts/Presentation/UI/MobileKeyboardAvoidance.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/NetworkStatusUI.cs](<../../../../Scripts/Presentation/UI/NetworkStatusUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs](<../../../../Scripts/Presentation/UI/ProductionPanelUI.cs>) | 목록 확보 · 내용 미조사 · Plan2.19 중복바인딩/누르기수명 지정구현·정적대조 완료 · AUD-036/037 · 전체감사/실기 미완 |
| [Assets/_Project/Scripts/Presentation/UI/ResearchCellView.cs](<../../../../Scripts/Presentation/UI/ResearchCellView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ResearchMatrixView.cs](<../../../../Scripts/Presentation/UI/ResearchMatrixView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ResearchPanelUI.cs](<../../../../Scripts/Presentation/UI/ResearchPanelUI.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ResearchProgressView.cs](<../../../../Scripts/Presentation/UI/ResearchProgressView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/SceneLoader.cs](<../../../../Scripts/Presentation/UI/SceneLoader.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/SkillCooldownOverlay.cs](<../../../../Scripts/Presentation/UI/SkillCooldownOverlay.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/SplashOverlayView.cs](<../../../../Scripts/Presentation/UI/SplashOverlayView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/UIManager.cs](<../../../../Scripts/Presentation/UI/UIManager.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs](<../../../../Scripts/Presentation/UI/ViewModels/BattleViewModel.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ViewModels/LobbyViewModel.cs](<../../../../Scripts/Presentation/UI/ViewModels/LobbyViewModel.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/ViewModels/RaceSelectionViewModel.cs](<../../../../Scripts/Presentation/UI/ViewModels/RaceSelectionViewModel.cs>) | 목록 확보 · 내용 미조사 · Plan2.15 지정정리/정적 대조 완료(AUD-022); 전체 의미 감사·컴파일/실기 완료 아님 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleMainView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/BattleMainView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/CustomGameView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/CustomGameView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/CustomHostView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/CustomHostView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/CustomJoinView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/CustomJoinView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/DifficultySelectView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/RandomMatchView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Battle/RandomMatchView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Profile/NicknameChangePopup.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs>) | Plan2.8 LoginUseCase UGS adapter 생성자 주입 범위 소스·정적 대조 완료 · AUD-018 · 전체 파일 의미 감사/실기 미완 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Ranking/RankRowView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Shop/ShopView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/Shop/ShopView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Lobby/TabBarView.cs](<../../../../Scripts/Presentation/UI/Views/Lobby/TabBarView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs](<../../../../Scripts/Presentation/UI/Views/Login/AnonymousWarningPopup.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailLoginView.cs](<../../../../Scripts/Presentation/UI/Views/Login/EmailLoginView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/EmailVerifyView.cs](<../../../../Scripts/Presentation/UI/Views/Login/EmailVerifyView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginRootView.cs](<../../../../Scripts/Presentation/UI/Views/Login/LoginRootView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/LoginSelectView.cs](<../../../../Scripts/Presentation/UI/Views/Login/LoginSelectView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/NetworkErrorPopup.cs](<../../../../Scripts/Presentation/UI/Views/Login/NetworkErrorPopup.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs](<../../../../Scripts/Presentation/UI/Views/Login/NicknameSetupView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/PasswordResetView.cs](<../../../../Scripts/Presentation/UI/Views/Login/PasswordResetView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/UI/Views/Login/SignUpView.cs](<../../../../Scripts/Presentation/UI/Views/Login/SignUpView.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs](<../../../../Scripts/Presentation/Unit/AnimationEventRelay.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Unit/PresentationPoseProvider.cs](<../../../../Scripts/Presentation/Unit/PresentationPoseProvider.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.cs](<../../../../Scripts/Presentation/Unit/UnitView.cs>) | Plan2.3/2.10 지정 interface·조회helper 범위 소스/독립 원문 대조 완료 · AUD-008/019 · 전체 의미 감사/컴파일/실기 미완 · Plan2.17/AUD-003 partial·4sourcegate범위 보존·raw대조 완료, 실행 NOT RUN |
| [Assets/_Project/Scripts/Presentation/Unit/VisualRootProjector.cs](<../../../../Scripts/Presentation/Unit/VisualRootProjector.cs>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>Editor C# — 8개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Assets/Editor/Debug/ForceRematchMapFailureMenu.cs](<../../../../../Editor/Debug/ForceRematchMapFailureMenu.cs>) | 목록 확보 · 내용 미조사 · Plan2.21 FirstRoundExpectation1상수 정정·정적대조 완료 · AUD-039 · 주입실행/전체감사 미완 |
| [Assets/Editor/Tools/Combat/RunAuthoritativeAttackPresentationValidation.cs](<../../../../../Editor/Tools/Combat/RunAuthoritativeAttackPresentationValidation.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs](<../../../../../Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs](<../../../../../Editor/Tools/Combat/RunUnitActionSelfValidation.cs>) | 목록 확보 · 내용 미조사 · Plan2.17/AUD-003 partial·4sourcegate범위 보존·raw대조 완료, 실행 NOT RUN |
| [Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs](<../../../../../Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/Editor/Tools/LogcatCapture.cs](<../../../../../Editor/Tools/LogcatCapture.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/Editor/Tools/TorrentWaveProductionValidation.cs](<../../../../../Editor/Tools/TorrentWaveProductionValidation.cs>) | 목록 확보 · 내용 미조사 |
| [Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs](<../../../../../Editor/Tools/UnitRecipientFitMeasureTool.cs>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>Python 도구 — 5개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Tools/analyze_pistoleer_timing.py](<../../../../../../Tools/analyze_pistoleer_timing.py>) | 일부 열람 · 1~50행 · 실행 안 함 |
| [Tools/archive_runtime_logs.py](<../../../../../../Tools/archive_runtime_logs.py>) | 일부 열람 · 1~75행 · 실행 안 함 |
| [Tools/check_docs.py](<../../../../../../Tools/check_docs.py>) | 메인 헤더·검사 실행 확인 · 실행 결과는6절 |
| [Tools/tests/test_check_docs.py](<../../../../../../Tools/tests/test_check_docs.py>) | 목록 확보 · 내용 미조사 |
| [parse_popup_temp.py](<../../../../../../parse_popup_temp.py>) | 일부 열람 · 1~45행 · AUD-006 |

</details>

<details>
<summary>상시 참조 문서 — 48개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Assets/_Project/Docs/AABSizeOptimization.md](<../../../AABSizeOptimization.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/3DAssetCreationGuide.md](<../../../Assets/3DAssetCreationGuide.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/AssetList.md](<../../../Assets/AssetList.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/CommonAssetGuide.md](<../../../Assets/CommonAssetGuide.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/UIAssetGuide.md](<../../../Assets/UIAssetGuide.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md](<../../../Assets/UnitCombatAssetMatrix.md>) | 목록 확보 · 내용 미조사 · Plan2.18 Mushroom행 지정 사실 정정/exact대조 완료(AUD-034); 전체 의미 감사 아님 |
| [Assets/_Project/Docs/Assets/VFXSFXGuide.md](<../../../Assets/VFXSFXGuide.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/Assets/VFXSFXList.md](<../../../Assets/VFXSFXList.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/AuthSystemRules.md](<../../../AuthSystemRules.md>) | 인증/오류/UGS 관련 절 일부 열람 · Plan2.8 구조 표2행/구현위치1행 동기화 · 다른 행동 원문 동일·전체 의미 감사 아님 · Plan2.14 지정본문 정정/exact대조 완료(AUD-032); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameDesignDocument.md](<../../../GameDesignDocument.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules.md](<../../../GameSystemRules.md>) | 인덱스 열람 · 세부 규칙은 별도 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md](<../../../GameSystemRules/GameSystemRules_AI.md>) | 규칙4/5/28/29/30/33/34 일부 열람 · Plan2.7 규칙4/33의 값 전달 구조 설명만 동기화 · 전체 규칙 의미 감사 아님 · Plan2.14 지정본문 정정/exact대조 완료(AUD-027); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md](<../../../GameSystemRules/GameSystemRules_AI_Scenario_Human.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-028); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md](<../../../GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-028); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md](<../../../GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-028); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_BuildingPanelsUI.md](<../../../GameSystemRules/GameSystemRules_BuildingPanelsUI.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-026); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md](<../../../GameSystemRules/GameSystemRules_Buildings.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_CanvasSortingOrder.md](<../../../GameSystemRules/GameSystemRules_CanvasSortingOrder.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Map.md](<../../../GameSystemRules/GameSystemRules_Map.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlow.md](<../../../GameSystemRules/GameSystemRules_MatchFlow.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_MatchFlowUI.md](<../../../GameSystemRules/GameSystemRules_MatchFlowUI.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Network.md](<../../../GameSystemRules/GameSystemRules_Network.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md](<../../../GameSystemRules/GameSystemRules_RandomMap.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_SettingsUI.md](<../../../GameSystemRules/GameSystemRules_SettingsUI.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md](<../../../GameSystemRules/GameSystemRules_Skills.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md](<../../../GameSystemRules/GameSystemRules_Sound.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-030/031); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md](<../../../GameSystemRules/GameSystemRules_UI.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md](<../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md](<../../../GameSystemRules/GameSystemRules_Units.md>) | 목록 확보 · 내용 미조사 · Plan2.18 규칙42 지정 사실 정정/exact대조 완료(AUD-033); 전체 의미 감사 아님 |
| [Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md](<../../../GameSystemRules/GameSystemRules_Upgrade.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-029); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/History/ImplementationVerificationHistory.md](<../../../History/ImplementationVerificationHistory.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/LogHistory.md](<../../../LogHistory.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/LogRules.md](<../../../LogRules.md>) | 일부 열람 · 문서 역할/두 축 |
| [Assets/_Project/Docs/PROJECT_STATUS.md](<../../../PROJECT_STATUS.md>) | 일부 열람 · 현재 상태/검증 한계 |
| [Assets/_Project/Docs/ROADMAP.md](<../../../ROADMAP.md>) | 일부 열람 · 보류/후속 · AUD-004/007 |
| [Assets/_Project/Docs/Skills/SKILLS_GUIDE.md](<../../../Skills/SKILLS_GUIDE.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/StatsReference.md](<../../../StatsReference.md>) | 목록 확보 · 내용 미조사 · Plan2.14 지정본문 정정/exact대조 완료(AUD-024/025); 전체 의미 감사 완료 아님 |
| [Assets/_Project/Docs/TechnicalDesignDocument.md](<../../../TechnicalDesignDocument.md>) | 일부 열람 · 기술 스택/계층/의존성 · AUD-001/002 · Plan2.18 Application그림 책임 설명 정정/exact대조 완료(AUD-035); 전체 의미 감사 아님 |
| [Assets/_Project/Docs/TechnicalDesignDocument_Network.md](<../../../TechnicalDesignDocument_Network.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/UIGuidelines.md](<../../../UIGuidelines.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/UnusedAssetAudit.md](<../../../UnusedAssetAudit.md>) | 일부 열람 · Scan Method/Important Interpretation |
| [Assets/_Project/Docs/WORKFLOW.md](<../../../WORKFLOW.md>) | 운영 절차 열람 · 개별 기능 검증 아님 |
| [Assets/_Project/Docs/WORK_HISTORY.md](<../../../WORK_HISTORY.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_Canyon.md](<../../../_Reference/MapTemplateSource/MapTemplateSource_Canyon.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_FullyOpen.md](<../../../_Reference/MapTemplateSource/MapTemplateSource_FullyOpen.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_ObstacleOpen.md](<../../../_Reference/MapTemplateSource/MapTemplateSource_ObstacleOpen.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_Outer.md](<../../../_Reference/MapTemplateSource/MapTemplateSource_Outer.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_ThreeLane.md](<../../../_Reference/MapTemplateSource/MapTemplateSource_ThreeLane.md>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>핵심 운영 지침 — 4개</summary>

| 파일 | 조사 상태 |
|---|---|
| [AGENTS.md](<../../../../../../AGENTS.md>) | 문서 인덱스 열람 |
| [CLAUDE.md](<../../../../../../CLAUDE.md>) | 지침 열람 |
| [.claude/MEMORY.md](<../../../../../../.claude/MEMORY.md>) | 공용 제약/수용/보호 지침 열람 |
| [Assets/_Project/Docs/WORKFLOW.md](<../../../WORKFLOW.md>) | 운영 절차 열람 · 개별 기능 검증 아님 |

</details>

<details>
<summary>프로젝트 설정 — 32개</summary>

| 파일 | 조사 상태 |
|---|---|
| [ProjectSettings/AndroidResolverDependencies.xml](<../../../../../../ProjectSettings/AndroidResolverDependencies.xml>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/AudioManager.asset](<../../../../../../ProjectSettings/AudioManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/ClusterInputManager.asset](<../../../../../../ProjectSettings/ClusterInputManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/DynamicsManager.asset](<../../../../../../ProjectSettings/DynamicsManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/EditorBuildSettings.asset](<../../../../../../ProjectSettings/EditorBuildSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/EditorSettings.asset](<../../../../../../ProjectSettings/EditorSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/GooglePlayGameSettings.txt](<../../../../../../ProjectSettings/GooglePlayGameSettings.txt>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/GraphicsSettings.asset](<../../../../../../ProjectSettings/GraphicsSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/InputManager.asset](<../../../../../../ProjectSettings/InputManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/McpUnitySettings.json](<../../../../../../ProjectSettings/McpUnitySettings.json>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/MemorySettings.asset](<../../../../../../ProjectSettings/MemorySettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/MultiplayerManager.asset](<../../../../../../ProjectSettings/MultiplayerManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/NavMeshAreas.asset](<../../../../../../ProjectSettings/NavMeshAreas.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/PackageManagerSettings.asset](<../../../../../../ProjectSettings/PackageManagerSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/Packages/com.unity.ai.assistant/Settings.json](<../../../../../../ProjectSettings/Packages/com.unity.ai.assistant/Settings.json>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/Packages/com.unity.dedicated-server/MultiplayerRolesSettings.asset](<../../../../../../ProjectSettings/Packages/com.unity.dedicated-server/MultiplayerRolesSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/Packages/com.unity.probuilder/Settings.json](<../../../../../../ProjectSettings/Packages/com.unity.probuilder/Settings.json>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/Physics2DSettings.asset](<../../../../../../ProjectSettings/Physics2DSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/PresetManager.asset](<../../../../../../ProjectSettings/PresetManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/ProjectSettings.asset](<../../../../../../ProjectSettings/ProjectSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/ProjectVersion.txt](<../../../../../../ProjectSettings/ProjectVersion.txt>) | 값 확인 · AUD-002 |
| [ProjectSettings/QualitySettings.asset](<../../../../../../ProjectSettings/QualitySettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/SceneTemplateSettings.json](<../../../../../../ProjectSettings/SceneTemplateSettings.json>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/ShaderGraphSettings.asset](<../../../../../../ProjectSettings/ShaderGraphSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/TagManager.asset](<../../../../../../ProjectSettings/TagManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/TimeManager.asset](<../../../../../../ProjectSettings/TimeManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/TimelineSettings.asset](<../../../../../../ProjectSettings/TimelineSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/URPProjectSettings.asset](<../../../../../../ProjectSettings/URPProjectSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/UnityConnectSettings.asset](<../../../../../../ProjectSettings/UnityConnectSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/VFXManager.asset](<../../../../../../ProjectSettings/VFXManager.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/VersionControlSettings.asset](<../../../../../../ProjectSettings/VersionControlSettings.asset>) | 목록 확보 · 내용 미조사 |
| [ProjectSettings/XRSettings.asset](<../../../../../../ProjectSettings/XRSettings.asset>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>패키지 manifest/lock — 2개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Packages/manifest.json](<../../../../../../Packages/manifest.json>) | 목록 확보 · 내용 미조사 |
| [Packages/packages-lock.json](<../../../../../../Packages/packages-lock.json>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>기존 Task 문서 — 612개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-02/21_40_animation-bugs/plan.md](<../../2026-03/2026-03-02/21_40_animation-bugs/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-02/21_40_animation-bugs/research.md](<../../2026-03/2026-03-02/21_40_animation-bugs/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-06/14_57_attack-direction-accuracy/plan.md](<../../2026-03/2026-03-06/14_57_attack-direction-accuracy/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-06/14_57_attack-direction-accuracy/research.md](<../../2026-03/2026-03-06/14_57_attack-direction-accuracy/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-06/23_54_attack-cooldown-animation/plan.md](<../../2026-03/2026-03-06/23_54_attack-cooldown-animation/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-06/23_54_attack-cooldown-animation/research.md](<../../2026-03/2026-03-06/23_54_attack-cooldown-animation/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/00_22_attack-direction-transform/plan.md](<../../2026-03/2026-03-07/00_22_attack-direction-transform/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/00_22_attack-direction-transform/research.md](<../../2026-03/2026-03-07/00_22_attack-direction-transform/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/01_19_combat-world-position/plan.md](<../../2026-03/2026-03-07/01_19_combat-world-position/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/01_19_combat-world-position/research.md](<../../2026-03/2026-03-07/01_19_combat-world-position/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/12_02_attack-animation-loop/plan.md](<../../2026-03/2026-03-07/12_02_attack-animation-loop/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/12_02_attack-animation-loop/research.md](<../../2026-03/2026-03-07/12_02_attack-animation-loop/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/22_38_siege-movement-network-sync/plan.md](<../../2026-03/2026-03-07/22_38_siege-movement-network-sync/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/22_38_siege-movement-network-sync/research.md](<../../2026-03/2026-03-07/22_38_siege-movement-network-sync/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/23_50_client-movement-tile-conflict/plan.md](<../../2026-03/2026-03-07/23_50_client-movement-tile-conflict/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-07/23_50_client-movement-tile-conflict/research.md](<../../2026-03/2026-03-07/23_50_client-movement-tile-conflict/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/00_30_client-movement-root-cause-fix/plan.md](<../../2026-03/2026-03-08/00_30_client-movement-root-cause-fix/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/00_30_client-movement-root-cause-fix/research.md](<../../2026-03/2026-03-08/00_30_client-movement-root-cause-fix/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/02_00_movement-sync-full-server-authority/plan.md](<../../2026-03/2026-03-08/02_00_movement-sync-full-server-authority/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/02_00_movement-sync-full-server-authority/research.md](<../../2026-03/2026-03-08/02_00_movement-sync-full-server-authority/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/16_09_combat-pause-sync-fix/plan.md](<../../2026-03/2026-03-08/16_09_combat-pause-sync-fix/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-08/16_09_combat-pause-sync-fix/research.md](<../../2026-03/2026-03-08/16_09_combat-pause-sync-fix/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-09/01_51_movement-sync-root-fix/plan.md](<../../2026-03/2026-03-09/01_51_movement-sync-root-fix/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-09/01_51_movement-sync-root-fix/research.md](<../../2026-03/2026-03-09/01_51_movement-sync-root-fix/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-09/15_33_responsive-popup-ui/plan.md](<../../2026-03/2026-03-09/15_33_responsive-popup-ui/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-09/15_33_responsive-popup-ui/research.md](<../../2026-03/2026-03-09/15_33_responsive-popup-ui/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/14_00_attack-animation-sync/plan.md](<../../2026-03/2026-03-14/14_00_attack-animation-sync/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/14_00_attack-animation-sync/research.md](<../../2026-03/2026-03-14/14_00_attack-animation-sync/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/15_00_unit-rotation-smooth/plan.md](<../../2026-03/2026-03-14/15_00_unit-rotation-smooth/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/15_00_unit-rotation-smooth/research.md](<../../2026-03/2026-03-14/15_00_unit-rotation-smooth/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/16_00_lobby-scene-separation/plan.md](<../../2026-03/2026-03-14/16_00_lobby-scene-separation/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/16_00_lobby-scene-separation/research.md](<../../2026-03/2026-03-14/16_00_lobby-scene-separation/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-14/16_00_lobby-scene-separation/todo.md](<../../2026-03/2026-03-14/16_00_lobby-scene-separation/todo.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-15/lobby-multiplayer-game-start-fix/plan.md](<../../2026-03/2026-03-15/lobby-multiplayer-game-start-fix/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-15/lobby-multiplayer-game-start-fix/research.md](<../../2026-03/2026-03-15/lobby-multiplayer-game-start-fix/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-15/random-matchmaking/plan.md](<../../2026-03/2026-03-15/random-matchmaking/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-15/random-matchmaking/research.md](<../../2026-03/2026-03-15/random-matchmaking/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-16/loading-screen/plan.md](<../../2026-03/2026-03-16/loading-screen/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-16/loading-screen/research.md](<../../2026-03/2026-03-16/loading-screen/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-16/random-matchmaking-game-start-bug/plan.md](<../../2026-03/2026-03-16/random-matchmaking-game-start-bug/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-16/random-matchmaking-game-start-bug/research.md](<../../2026-03/2026-03-16/random-matchmaking-game-start-bug/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/16_20_back-to-lobby-bug/plan.md](<../../2026-03/2026-03-17/16_20_back-to-lobby-bug/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/16_20_back-to-lobby-bug/research.md](<../../2026-03/2026-03-17/16_20_back-to-lobby-bug/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/18_45_custom-game-rematch/plan.md](<../../2026-03/2026-03-17/18_45_custom-game-rematch/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/18_45_custom-game-rematch/research.md](<../../2026-03/2026-03-17/18_45_custom-game-rematch/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/building-adjacent-movement-fix/plan.md](<../../2026-03/2026-03-17/building-adjacent-movement-fix/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-17/building-adjacent-movement-fix/research.md](<../../2026-03/2026-03-17/building-adjacent-movement-fix/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-19/camera-zoom-smooth/plan.md](<../../2026-03/2026-03-19/camera-zoom-smooth/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-19/camera-zoom-smooth/research.md](<../../2026-03/2026-03-19/camera-zoom-smooth/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-19/ui-dotween-animation/plan.md](<../../2026-03/2026-03-19/ui-dotween-animation/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-19/ui-dotween-animation/research.md](<../../2026-03/2026-03-19/ui-dotween-animation/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-19/ui-dotween-animation/testcase.md](<../../2026-03/2026-03-19/ui-dotween-animation/testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/12_33_code-cleanup/plan.md](<../../2026-03/2026-03-20/12_33_code-cleanup/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/12_33_code-cleanup/research.md](<../../2026-03/2026-03-20/12_33_code-cleanup/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/13_26_singleplay-camera-start-position/plan.md](<../../2026-03/2026-03-20/13_26_singleplay-camera-start-position/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/13_26_singleplay-camera-start-position/research.md](<../../2026-03/2026-03-20/13_26_singleplay-camera-start-position/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/plan.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/research.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/testcase.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/testcasefix.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/testcasefix.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/testcasemulti.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/testcasemulti.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/testcaserule.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/testcaserule.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-20/17_06_auto-production-ui/testcaserulefix.md](<../../2026-03/2026-03-20/17_06_auto-production-ui/testcaserulefix.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-23/10_00_background-overlay-fix/Plan.md](<../../2026-03/2026-03-23/10_00_background-overlay-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-23/10_00_background-overlay-fix/Research.md](<../../2026-03/2026-03-23/10_00_background-overlay-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Plan.md](<../../2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Research.md](<../../2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Testcase.md](<../../2026-03/2026-03-24/10_00_game-ui-lifecycle-framework/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-24/14_00_new-faction-assets/GeminiGuide.md](<../../2026-03/2026-03-24/14_00_new-faction-assets/GeminiGuide.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-25/06_28_faction-selection-ui/Plan.md](<../../2026-03/2026-03-25/06_28_faction-selection-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-25/06_28_faction-selection-ui/Research.md](<../../2026-03/2026-03-25/06_28_faction-selection-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-25/14_00_ui-team-portrait-gemini-guide/Plan.md](<../../2026-03/2026-03-25/14_00_ui-team-portrait-gemini-guide/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-25/14_00_ui-team-portrait-gemini-guide/Research.md](<../../2026-03/2026-03-25/14_00_ui-team-portrait-gemini-guide/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-26/15_08_network-unit-combat-sync/Plan.md](<../../2026-03/2026-03-26/15_08_network-unit-combat-sync/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-26/15_08_network-unit-combat-sync/Research.md](<../../2026-03/2026-03-26/15_08_network-unit-combat-sync/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/10_00_rotation-timing-fix/Plan.md](<../../2026-03/2026-03-27/10_00_rotation-timing-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/10_00_rotation-timing-fix/Research.md](<../../2026-03/2026-03-27/10_00_rotation-timing-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/10_00_rotation-timing-fix/Testcase.md](<../../2026-03/2026-03-27/10_00_rotation-timing-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/11_00_attack-timing-precision/Plan.md](<../../2026-03/2026-03-27/11_00_attack-timing-precision/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/11_00_attack-timing-precision/Plan_target-lock.md](<../../2026-03/2026-03-27/11_00_attack-timing-precision/Plan_target-lock.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/11_00_attack-timing-precision/Research.md](<../../2026-03/2026-03-27/11_00_attack-timing-precision/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/11_00_attack-timing-precision/Research_target-lock.md](<../../2026-03/2026-03-27/11_00_attack-timing-precision/Research_target-lock.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/11_00_attack-timing-precision/Testcase.md](<../../2026-03/2026-03-27/11_00_attack-timing-precision/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/12_00_animation-transition-smooth/Plan.md](<../../2026-03/2026-03-27/12_00_animation-transition-smooth/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-27/12_00_animation-transition-smooth/Research.md](<../../2026-03/2026-03-27/12_00_animation-transition-smooth/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-28/10_00_walk-attack-rotation-fix/Plan.md](<../../2026-03/2026-03-28/10_00_walk-attack-rotation-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-28/10_00_walk-attack-rotation-fix/Research.md](<../../2026-03/2026-03-28/10_00_walk-attack-rotation-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-28/11_00_walk-attack-consistency-fix/Plan.md](<../../2026-03/2026-03-28/11_00_walk-attack-consistency-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-28/11_00_walk-attack-consistency-fix/Research.md](<../../2026-03/2026-03-28/11_00_walk-attack-consistency-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/14_00_initialize-overwrites-turn-to-face/Plan.md](<../../2026-03/2026-03-29/14_00_initialize-overwrites-turn-to-face/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/14_00_initialize-overwrites-turn-to-face/Research.md](<../../2026-03/2026-03-29/14_00_initialize-overwrites-turn-to-face/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/15_00_deferred-red-client-bugs/BugReport.md](<../../2026-03/2026-03-29/15_00_deferred-red-client-bugs/BugReport.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Plan.md](<../../2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Research.md](<../../2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Testcase.md](<../../2026-03/2026-03-29/16_00_red-client-rotation-walk-bugs/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Plan.md](<../../2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Research.md](<../../2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Testcase.md](<../../2026-03/2026-03-29/17_00_rotation-sync-via-networktransform/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-30/10_00_combat-animation-redesign/Plan.md](<../../2026-03/2026-03-30/10_00_combat-animation-redesign/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-03/2026-03-30/10_00_combat-animation-redesign/Research.md](<../../2026-03/2026-03-30/10_00_combat-animation-redesign/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-03/10_00_combat-animation-overhaul/Plan.md](<../../2026-04/2026-04-03/10_00_combat-animation-overhaul/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-03/20_00_rematch-initialization-bug/Plan.md](<../../2026-04/2026-04-03/20_00_rematch-initialization-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-03/20_00_rematch-initialization-bug/Research.md](<../../2026-04/2026-04-03/20_00_rematch-initialization-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-03/20_00_rematch-initialization-bug/Testcase.md](<../../2026-04/2026-04-03/20_00_rematch-initialization-bug/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/15_00_auto-production-gold-bug/Plan.md](<../../2026-04/2026-04-04/15_00_auto-production-gold-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/15_00_auto-production-gold-bug/Research.md](<../../2026-04/2026-04-04/15_00_auto-production-gold-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/15_00_auto-production-gold-bug/Testcase.md](<../../2026-04/2026-04-04/15_00_auto-production-gold-bug/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/21_00_race-selection-ui/Plan.md](<../../2026-04/2026-04-04/21_00_race-selection-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/21_00_race-selection-ui/Research.md](<../../2026-04/2026-04-04/21_00_race-selection-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-04/21_00_race-selection-ui/Testcase.md](<../../2026-04/2026-04-04/21_00_race-selection-ui/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/00_23_attack-animation-root-motion/Plan.md](<../../2026-04/2026-04-07/00_23_attack-animation-root-motion/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/00_23_attack-animation-root-motion/Research.md](<../../2026-04/2026-04-07/00_23_attack-animation-root-motion/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/09_00_faction-ingame-apply/Plan.md](<../../2026-04/2026-04-07/09_00_faction-ingame-apply/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/09_00_faction-ingame-apply/Research.md](<../../2026-04/2026-04-07/09_00_faction-ingame-apply/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/09_00_faction-ingame-apply/Testcase.md](<../../2026-04/2026-04-07/09_00_faction-ingame-apply/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/23_45_goldmine-hide/Plan.md](<../../2026-04/2026-04-07/23_45_goldmine-hide/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/23_45_goldmine-hide/Research.md](<../../2026-04/2026-04-07/23_45_goldmine-hide/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-07/23_45_goldmine-hide/Testcase.md](<../../2026-04/2026-04-07/23_45_goldmine-hide/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-10/16_09_melee-unit-attack-range/Plan.md](<../../2026-04/2026-04-10/16_09_melee-unit-attack-range/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-10/16_09_melee-unit-attack-range/Research.md](<../../2026-04/2026-04-10/16_09_melee-unit-attack-range/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-10/16_09_melee-unit-attack-range/Testcase.md](<../../2026-04/2026-04-10/16_09_melee-unit-attack-range/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/building-portrait-race-support/Plan.md](<../../2026-04/2026-04-11/building-portrait-race-support/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/building-portrait-race-support/Research.md](<../../2026-04/2026-04-11/building-portrait-race-support/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/building-portrait-race-support/Testcase.md](<../../2026-04/2026-04-11/building-portrait-race-support/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/melee-attack-distance/Plan.md](<../../2026-04/2026-04-11/melee-attack-distance/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/melee-attack-distance/Research.md](<../../2026-04/2026-04-11/melee-attack-distance/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/melee-attack-distance/Testcase.md](<../../2026-04/2026-04-11/melee-attack-distance/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/ranged-unit-rotation-tracking/Plan.md](<../../2026-04/2026-04-11/ranged-unit-rotation-tracking/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/ranged-unit-rotation-tracking/Research.md](<../../2026-04/2026-04-11/ranged-unit-rotation-tracking/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-11/ranged-unit-rotation-tracking/Testcase.md](<../../2026-04/2026-04-11/ranged-unit-rotation-tracking/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/01_31_smooth-rotation-on-target-change/Plan.md](<../../2026-04/2026-04-12/01_31_smooth-rotation-on-target-change/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/01_31_smooth-rotation-on-target-change/Research.md](<../../2026-04/2026-04-12/01_31_smooth-rotation-on-target-change/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/06_42_stats-apply/Plan.md](<../../2026-04/2026-04-12/06_42_stats-apply/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/06_42_stats-apply/Research.md](<../../2026-04/2026-04-12/06_42_stats-apply/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/06_42_stats-apply/Testcase.md](<../../2026-04/2026-04-12/06_42_stats-apply/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/18_03_floating-hp-text/Plan.md](<../../2026-04/2026-04-12/18_03_floating-hp-text/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/18_03_floating-hp-text/Research.md](<../../2026-04/2026-04-12/18_03_floating-hp-text/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-12/18_03_floating-hp-text/Testcase.md](<../../2026-04/2026-04-12/18_03_floating-hp-text/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/14_00_floating-text-zoom-fix/Plan.md](<../../2026-04/2026-04-13/14_00_floating-text-zoom-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/14_00_floating-text-zoom-fix/Research.md](<../../2026-04/2026-04-13/14_00_floating-text-zoom-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/14_00_floating-text-zoom-fix/Testcase.md](<../../2026-04/2026-04-13/14_00_floating-text-zoom-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/17_50_floating-text-worldspace/Plan.md](<../../2026-04/2026-04-13/17_50_floating-text-worldspace/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/17_50_floating-text-worldspace/Research.md](<../../2026-04/2026-04-13/17_50_floating-text-worldspace/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-13/17_50_floating-text-worldspace/Testcase.md](<../../2026-04/2026-04-13/17_50_floating-text-worldspace/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-17/22_29_target-lock-damage-bug/Plan.md](<../../2026-04/2026-04-17/22_29_target-lock-damage-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-17/22_29_target-lock-damage-bug/Research.md](<../../2026-04/2026-04-17/22_29_target-lock-damage-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-17/22_29_target-lock-damage-bug/Testcase.md](<../../2026-04/2026-04-17/22_29_target-lock-damage-bug/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug-fix/Plan.md](<../../2026-04/2026-04-18/auto-production-queue-bug-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug-fix/Research.md](<../../2026-04/2026-04-18/auto-production-queue-bug-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug-fix/Testcase.md](<../../2026-04/2026-04-18/auto-production-queue-bug-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug/Plan.md](<../../2026-04/2026-04-18/auto-production-queue-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug/Research.md](<../../2026-04/2026-04-18/auto-production-queue-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-18/auto-production-queue-bug/Testcase.md](<../../2026-04/2026-04-18/auto-production-queue-bug/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/17_49_production-slot-flicker/Plan.md](<../../2026-04/2026-04-19/17_49_production-slot-flicker/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/17_49_production-slot-flicker/Research.md](<../../2026-04/2026-04-19/17_49_production-slot-flicker/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/18_54_rally-point-ignored/Plan.md](<../../2026-04/2026-04-19/18_54_rally-point-ignored/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/18_54_rally-point-ignored/Research.md](<../../2026-04/2026-04-19/18_54_rally-point-ignored/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/20_26_melee-detect-range/Plan.md](<../../2026-04/2026-04-19/20_26_melee-detect-range/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/20_26_melee-detect-range/Research.md](<../../2026-04/2026-04-19/20_26_melee-detect-range/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/production-panel-rewrite/Plan.md](<../../2026-04/2026-04-19/production-panel-rewrite/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/production-panel-rewrite/Research.md](<../../2026-04/2026-04-19/production-panel-rewrite/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-19/production-panel-rewrite/Testcase.md](<../../2026-04/2026-04-19/production-panel-rewrite/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/15_45_melee-pursuit-rotation/Plan.md](<../../2026-04/2026-04-24/15_45_melee-pursuit-rotation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/15_45_melee-pursuit-rotation/Research.md](<../../2026-04/2026-04-24/15_45_melee-pursuit-rotation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/15_45_melee-pursuit-rotation/Testcase.md](<../../2026-04/2026-04-24/15_45_melee-pursuit-rotation/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/16_31_multi-hit-damage/Plan.md](<../../2026-04/2026-04-24/16_31_multi-hit-damage/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/16_31_multi-hit-damage/Research.md](<../../2026-04/2026-04-24/16_31_multi-hit-damage/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/23_06_random-opponent-race/Plan.md](<../../2026-04/2026-04-24/23_06_random-opponent-race/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/23_06_random-opponent-race/Research.md](<../../2026-04/2026-04-24/23_06_random-opponent-race/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-24/23_06_random-opponent-race/Testcase.md](<../../2026-04/2026-04-24/23_06_random-opponent-race/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-25/01_35_unit-stats-scriptableobject/Plan.md](<../../2026-04/2026-04-25/01_35_unit-stats-scriptableobject/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-25/01_35_unit-stats-scriptableobject/Research.md](<../../2026-04/2026-04-25/01_35_unit-stats-scriptableobject/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-25/10_05_pathfinding-improvement/Plan.md](<../../2026-04/2026-04-25/10_05_pathfinding-improvement/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-25/10_05_pathfinding-improvement/Research.md](<../../2026-04/2026-04-25/10_05_pathfinding-improvement/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-25/10_05_pathfinding-improvement/Testcase.md](<../../2026-04/2026-04-25/10_05_pathfinding-improvement/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/11_00_occupancy-from-fix/Plan.md](<../../2026-04/2026-04-26/11_00_occupancy-from-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/11_00_occupancy-from-fix/Research.md](<../../2026-04/2026-04-26/11_00_occupancy-from-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/11_00_occupancy-from-fix/Testcase.md](<../../2026-04/2026-04-26/11_00_occupancy-from-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/15_00_phase1-target-reselect/Plan.md](<../../2026-04/2026-04-26/15_00_phase1-target-reselect/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/15_00_phase1-target-reselect/Research.md](<../../2026-04/2026-04-26/15_00_phase1-target-reselect/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/17_00_tile-ownership-detection/Plan.md](<../../2026-04/2026-04-26/17_00_tile-ownership-detection/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/17_00_tile-ownership-detection/Research.md](<../../2026-04/2026-04-26/17_00_tile-ownership-detection/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/18_30_melee-spread/Plan.md](<../../2026-04/2026-04-26/18_30_melee-spread/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-26/18_30_melee-spread/Research.md](<../../2026-04/2026-04-26/18_30_melee-spread/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-27/01_17_phase2-backward-snap-fix/Plan.md](<../../2026-04/2026-04-27/01_17_phase2-backward-snap-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-27/01_17_phase2-backward-snap-fix/Research.md](<../../2026-04/2026-04-27/01_17_phase2-backward-snap-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-28/14_00_new-asset-game-integration/Plan.md](<../../2026-04/2026-04-28/14_00_new-asset-game-integration/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-28/14_00_new-asset-game-integration/Research.md](<../../2026-04/2026-04-28/14_00_new-asset-game-integration/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-28/14_30_mesh-offset-cleanup/Plan.md](<../../2026-04/2026-04-28/14_30_mesh-offset-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-28/14_30_mesh-offset-cleanup/Research.md](<../../2026-04/2026-04-28/14_30_mesh-offset-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-28/14_30_mesh-offset-cleanup/Testcase.md](<../../2026-04/2026-04-28/14_30_mesh-offset-cleanup/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-30/02_29_movement-combat-redesign/Plan.md](<../../2026-04/2026-04-30/02_29_movement-combat-redesign/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-30/02_29_movement-combat-redesign/Research.md](<../../2026-04/2026-04-30/02_29_movement-combat-redesign/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-04/2026-04-30/02_29_movement-combat-redesign/Testcase.md](<../../2026-04/2026-04-30/02_29_movement-combat-redesign/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/01_19_movement-slot-ghostfix/Plan.md](<../../2026-05-06/01_19_movement-slot-ghostfix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/01_19_movement-slot-ghostfix/Research.md](<../../2026-05-06/01_19_movement-slot-ghostfix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/10_00_ranged-combat-rule-fix/Plan.md](<../../2026-05-06/10_00_ranged-combat-rule-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/10_00_ranged-combat-rule-fix/Research.md](<../../2026-05-06/10_00_ranged-combat-rule-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/19_43_movement-combat-full-rewrite/Plan.md](<../../2026-05-06/19_43_movement-combat-full-rewrite/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-06/19_43_movement-combat-full-rewrite/Research.md](<../../2026-05-06/19_43_movement-combat-full-rewrite/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/10_49_slot-ratio-inspector/Plan.md](<../../2026-05-11/10_49_slot-ratio-inspector/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/10_49_slot-ratio-inspector/Research.md](<../../2026-05-11/10_49_slot-ratio-inspector/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/11_53_ranged-attack-slot/Plan.md](<../../2026-05-11/11_53_ranged-attack-slot/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/11_53_ranged-attack-slot/Research.md](<../../2026-05-11/11_53_ranged-attack-slot/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/15_47_melee-slot-6dir/Plan.md](<../../2026-05-11/15_47_melee-slot-6dir/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/15_47_melee-slot-6dir/Research.md](<../../2026-05-11/15_47_melee-slot-6dir/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/17_10_melee-slot-dynamic/Plan.md](<../../2026-05-11/17_10_melee-slot-dynamic/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/17_10_melee-slot-dynamic/Research.md](<../../2026-05-11/17_10_melee-slot-dynamic/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/23_19_unit-movement-redesign/Plan.md](<../../2026-05-11/23_19_unit-movement-redesign/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-11/23_19_unit-movement-redesign/Research.md](<../../2026-05-11/23_19_unit-movement-redesign/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-13/09_57_unit-facing-direction-bug/Plan.md](<../../2026-05-13/09_57_unit-facing-direction-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-13/09_57_unit-facing-direction-bug/Research.md](<../../2026-05-13/09_57_unit-facing-direction-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-14/14_30_unit-rotation-system-fix/Plan.md](<../../2026-05-14/14_30_unit-rotation-system-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-14/14_30_unit-rotation-system-fix/Research.md](<../../2026-05-14/14_30_unit-rotation-system-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/02_57_character-display-bug/Plan.md](<../../2026-05-15/02_57_character-display-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/02_57_character-display-bug/Research.md](<../../2026-05-15/02_57_character-display-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/03_11_castle-approach-spread/Plan.md](<../../2026-05-15/03_11_castle-approach-spread/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/03_11_castle-approach-spread/Research.md](<../../2026-05-15/03_11_castle-approach-spread/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/17_29_congestion-based-spread/Plan.md](<../../2026-05-15/17_29_congestion-based-spread/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-15/17_29_congestion-based-spread/Research.md](<../../2026-05-15/17_29_congestion-based-spread/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/14_45_dead-code-cleanup/Plan.md](<../../2026-05-16/14_45_dead-code-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/14_45_dead-code-cleanup/Research.md](<../../2026-05-16/14_45_dead-code-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/15_09_production-fail-feedback/Plan.md](<../../2026-05-16/15_09_production-fail-feedback/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/15_09_production-fail-feedback/Research.md](<../../2026-05-16/15_09_production-fail-feedback/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/15_30_rally-point-flag-visibility/Plan.md](<../../2026-05-16/15_30_rally-point-flag-visibility/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/15_30_rally-point-flag-visibility/Research.md](<../../2026-05-16/15_30_rally-point-flag-visibility/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/17_00_building-repath-freeze-fix/Plan.md](<../../2026-05-16/17_00_building-repath-freeze-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/17_00_building-repath-freeze-fix/Research.md](<../../2026-05-16/17_00_building-repath-freeze-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/20_10_building-placement-fail-feedback/Plan.md](<../../2026-05-16/20_10_building-placement-fail-feedback/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/20_10_building-placement-fail-feedback/Research.md](<../../2026-05-16/20_10_building-placement-fail-feedback/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/21_30_building-cost-g-removal/Plan.md](<../../2026-05-16/21_30_building-cost-g-removal/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-16/21_30_building-cost-g-removal/Research.md](<../../2026-05-16/21_30_building-cost-g-removal/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/00_21_production-rule20-slot0-extension/Plan.md](<../../2026-05-17/00_21_production-rule20-slot0-extension/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/00_21_production-rule20-slot0-extension/Research.md](<../../2026-05-17/00_21_production-rule20-slot0-extension/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/02_16_building-upgrade-system/Plan.md](<../../2026-05-17/02_16_building-upgrade-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/02_16_building-upgrade-system/Research.md](<../../2026-05-17/02_16_building-upgrade-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/15_30_production-popup-ui-layout/Plan.md](<../../2026-05-17/15_30_production-popup-ui-layout/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/15_30_production-popup-ui-layout/Research.md](<../../2026-05-17/15_30_production-popup-ui-layout/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/18_15_building-demolish/Plan.md](<../../2026-05-17/18_15_building-demolish/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-17/18_15_building-demolish/Research.md](<../../2026-05-17/18_15_building-demolish/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/15_00_entity-died-event-split/Plan.md](<../../2026-05-18/15_00_entity-died-event-split/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/15_00_entity-died-event-split/Research.md](<../../2026-05-18/15_00_entity-died-event-split/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/17_00_building-action-panel-ui/Plan.md](<../../2026-05-18/17_00_building-action-panel-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/17_00_building-action-panel-ui/Research.md](<../../2026-05-18/17_00_building-action-panel-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/23_36_ingame-settings-forfeit/Plan.md](<../../2026-05-18/23_36_ingame-settings-forfeit/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-18/23_36_ingame-settings-forfeit/Research.md](<../../2026-05-18/23_36_ingame-settings-forfeit/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/00_00_production-popup-responsive-ui/Plan.md](<../../2026-05-19/00_00_production-popup-responsive-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/00_00_production-popup-responsive-ui/Research.md](<../../2026-05-19/00_00_production-popup-responsive-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/Plan.md](<../../2026-05-19/10_46_code-refactoring/Plan.md>) | 메인 전달 · 1~100행 / 프로그래머 전달551~579행 일부 열람 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/Research.md](<../../2026-05-19/10_46_code-refactoring/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/Testcase.md](<../../2026-05-19/10_46_code-refactoring/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/14_00_building-slot-layout-fix/Plan.md](<../../2026-05-19/14_00_building-slot-layout-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-19/14_00_building-slot-layout-fix/Research.md](<../../2026-05-19/14_00_building-slot-layout-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-24/10_00_login-system/Plan.md](<../../2026-05-24/10_00_login-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-24/10_00_login-system/Research.md](<../../2026-05-24/10_00_login-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-24/10_00_login-system/ThirdPartySetup.md](<../../2026-05-24/10_00_login-system/ThirdPartySetup.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-24/10_00_login-system/UIWireframe.md](<../../2026-05-24/10_00_login-system/UIWireframe.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/01_49_gpgs-gameinfo-compile-fix/Plan.md](<../../2026-05-25/01_49_gpgs-gameinfo-compile-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/01_49_gpgs-gameinfo-compile-fix/Research.md](<../../2026-05-25/01_49_gpgs-gameinfo-compile-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/04_55_canvas-scaler-safearea/Plan.md](<../../2026-05-25/04_55_canvas-scaler-safearea/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/04_55_canvas-scaler-safearea/Research.md](<../../2026-05-25/04_55_canvas-scaler-safearea/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/04_55_canvas-scaler-safearea/Testcase.md](<../../2026-05-25/04_55_canvas-scaler-safearea/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/ui-rules-inspection/Plan.md](<../../2026-05-25/ui-rules-inspection/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/ui-rules-inspection/Research.md](<../../2026-05-25/ui-rules-inspection/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-25/ui-rules-inspection/Testcase.md](<../../2026-05-25/ui-rules-inspection/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-26/game-ui-tc/Testcase.md](<../../2026-05-26/game-ui-tc/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-26/safe-area-lobby-bg/Plan.md](<../../2026-05-26/safe-area-lobby-bg/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-26/safe-area-lobby-bg/Research.md](<../../2026-05-26/safe-area-lobby-bg/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-27/16_52_game-end-ui-bugfix/Plan.md](<../../2026-05-27/16_52_game-end-ui-bugfix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-27/16_52_game-end-ui-bugfix/Research.md](<../../2026-05-27/16_52_game-end-ui-bugfix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-27/17_39_ui-color-layout-fix/Plan.md](<../../2026-05-27/17_39_ui-color-layout-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-27/17_39_ui-color-layout-fix/Research.md](<../../2026-05-27/17_39_ui-color-layout-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-27/17_39_ui-color-layout-fix/Testcase.md](<../../2026-05-27/17_39_ui-color-layout-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-28/09_00_game-ui-rules-fix/Plan.md](<../../2026-05-28/09_00_game-ui-rules-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-28/09_00_game-ui-rules-fix/Research.md](<../../2026-05-28/09_00_game-ui-rules-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-29/building-action-panel-rebuild/Plan.md](<../../2026-05-29/building-action-panel-rebuild/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-29/building-action-panel-rebuild/Research.md](<../../2026-05-29/building-action-panel-rebuild/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/Plan.md](<../../2026-05-29/building-placement-ui-rebuild/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-29/building-placement-ui-rebuild/Research.md](<../../2026-05-29/building-placement-ui-rebuild/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/01_39_production-popup-rebuild/Plan.md](<../../2026-05-30/01_39_production-popup-rebuild/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/01_39_production-popup-rebuild/Research.md](<../../2026-05-30/01_39_production-popup-rebuild/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/12_24_lobby-ui-rule-compliance/Plan.md](<../../2026-05-30/12_24_lobby-ui-rule-compliance/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/12_24_lobby-ui-rule-compliance/Research.md](<../../2026-05-30/12_24_lobby-ui-rule-compliance/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/15_48_production-popup-rule-compliance/Plan.md](<../../2026-05-30/15_48_production-popup-rule-compliance/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-30/15_48_production-popup-rule-compliance/Research.md](<../../2026-05-30/15_48_production-popup-rule-compliance/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/09_32_production-panel-portrait-auto-assign/Plan.md](<../../2026-05-31/09_32_production-panel-portrait-auto-assign/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/09_32_production-panel-portrait-auto-assign/Research.md](<../../2026-05-31/09_32_production-panel-portrait-auto-assign/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/13_18_panel-button-size-inconsistency/Plan.md](<../../2026-05-31/13_18_panel-button-size-inconsistency/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/13_18_panel-button-size-inconsistency/Research.md](<../../2026-05-31/13_18_panel-button-size-inconsistency/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_42_refactoring-regression-fix/Plan.md](<../../2026-05-31/19_42_refactoring-regression-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_42_refactoring-regression-fix/Research.md](<../../2026-05-31/19_42_refactoring-regression-fix/Research.md>) | 메인 전달 · 1~155행 일부 열람 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_42_refactoring-regression-fix/Testcase.md](<../../2026-05-31/19_42_refactoring-regression-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_48_production-lock-icon/Plan.md](<../../2026-05-31/19_48_production-lock-icon/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_48_production-lock-icon/Research.md](<../../2026-05-31/19_48_production-lock-icon/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/19_48_production-lock-icon/Testcase.md](<../../2026-05-31/19_48_production-lock-icon/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/21_03_upgrade-production-state-fix/Plan.md](<../../2026-05-31/21_03_upgrade-production-state-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-05-31/21_03_upgrade-production-state-fix/Research.md](<../../2026-05-31/21_03_upgrade-production-state-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-01/05_01_defense-tower-implementation/Plan.md](<../../2026-06-01/05_01_defense-tower-implementation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-01/05_01_defense-tower-implementation/Research.md](<../../2026-06-01/05_01_defense-tower-implementation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-02/00_17_cannon-tower-facing-direction/Plan.md](<../../2026-06-02/00_17_cannon-tower-facing-direction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-02/00_17_cannon-tower-facing-direction/Research.md](<../../2026-06-02/00_17_cannon-tower-facing-direction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/10_59_auto-production-cycle-flicker/Plan.md](<../../2026-06-05/10_59_auto-production-cycle-flicker/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/10_59_auto-production-cycle-flicker/Research.md](<../../2026-06-05/10_59_auto-production-cycle-flicker/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/10_59_auto-production-cycle-flicker/Testcase.md](<../../2026-06-05/10_59_auto-production-cycle-flicker/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/11_31_auto-unregister-currentisauto-fix/Plan.md](<../../2026-06-05/11_31_auto-unregister-currentisauto-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/11_31_auto-unregister-currentisauto-fix/Research.md](<../../2026-06-05/11_31_auto-unregister-currentisauto-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/11_31_auto-unregister-currentisauto-fix/Testcase.md](<../../2026-06-05/11_31_auto-unregister-currentisauto-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/22_14_unit-prefab-component-setup/Plan.md](<../../2026-06-05/22_14_unit-prefab-component-setup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/22_14_unit-prefab-component-setup/Research.md](<../../2026-06-05/22_14_unit-prefab-component-setup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/22_42_missing-buildingview-script/Plan.md](<../../2026-06-05/22_42_missing-buildingview-script/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/22_42_missing-buildingview-script/Research.md](<../../2026-06-05/22_42_missing-buildingview-script/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/23_05_network-game-manager-dontdestroy/Plan.md](<../../2026-06-05/23_05_network-game-manager-dontdestroy/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-05/23_05_network-game-manager-dontdestroy/Research.md](<../../2026-06-05/23_05_network-game-manager-dontdestroy/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-06/00_08_networkgamemanager-cleanup/Plan.md](<../../2026-06-06/00_08_networkgamemanager-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-06/00_08_networkgamemanager-cleanup/Research.md](<../../2026-06-06/00_08_networkgamemanager-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-07/16_40_ai-system-implementation/Plan.md](<../../2026-06-07/16_40_ai-system-implementation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-07/16_40_ai-system-implementation/Research.md](<../../2026-06-07/16_40_ai-system-implementation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-07/23_56_pistoleer-attack-vfx-pool/Plan.md](<../../2026-06-07/23_56_pistoleer-attack-vfx-pool/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-07/23_56_pistoleer-attack-vfx-pool/Research.md](<../../2026-06-07/23_56_pistoleer-attack-vfx-pool/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/00_20_effect-manager-system/Plan.md](<../../2026-06-08/00_20_effect-manager-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/00_20_effect-manager-system/Research.md](<../../2026-06-08/00_20_effect-manager-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/01_12_vfxpoolitem-null-check/Plan.md](<../../2026-06-08/01_12_vfxpoolitem-null-check/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/01_12_vfxpoolitem-null-check/Research.md](<../../2026-06-08/01_12_vfxpoolitem-null-check/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/02_27_networkobject-invalid-destroy/Plan.md](<../../2026-06-08/02_27_networkobject-invalid-destroy/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/02_27_networkobject-invalid-destroy/Research.md](<../../2026-06-08/02_27_networkobject-invalid-destroy/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/14_44_vfx-scaling-mode-fix/Plan.md](<../../2026-06-08/14_44_vfx-scaling-mode-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/14_44_vfx-scaling-mode-fix/Research.md](<../../2026-06-08/14_44_vfx-scaling-mode-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/19_08_unit-death-vfx/Plan.md](<../../2026-06-08/19_08_unit-death-vfx/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-08/19_08_unit-death-vfx/Research.md](<../../2026-06-08/19_08_unit-death-vfx/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Plan.md](<../../2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Research.md](<../../2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Testcase.md](<../../2026-06-10/01_06_ai-scenario-scriptableobject-restructure/Testcase.md>) | 159~165/182행 일부 열람 · AUD-009 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/Plan.md](<../../2026-06-10/09_28_sound-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/Research.md](<../../2026-06-10/09_28_sound-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/Testcase.md](<../../2026-06-10/09_28_sound-system/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/10_20_oidc-bridge-ugs-data/Plan.md](<../../2026-06-10/10_20_oidc-bridge-ugs-data/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/10_20_oidc-bridge-ugs-data/Research.md](<../../2026-06-10/10_20_oidc-bridge-ugs-data/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/10_20_oidc-bridge-ugs-data/Testcase.md](<../../2026-06-10/10_20_oidc-bridge-ugs-data/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/23_32_sound-inspector-setup/Plan.md](<../../2026-06-10/23_32_sound-inspector-setup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-10/23_32_sound-inspector-setup/Research.md](<../../2026-06-10/23_32_sound-inspector-setup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Plan.md](<../../2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Research.md](<../../2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Testcase.md](<../../2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-12/09_13_animatedpanel-canvasgroup-refactor/Plan.md](<../../2026-06-12/09_13_animatedpanel-canvasgroup-refactor/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-12/09_13_animatedpanel-canvasgroup-refactor/Research.md](<../../2026-06-12/09_13_animatedpanel-canvasgroup-refactor/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-15/18_12_lobby-rule-violations/Plan.md](<../../2026-06-15/18_12_lobby-rule-violations/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-15/18_12_lobby-rule-violations/Research.md](<../../2026-06-15/18_12_lobby-rule-violations/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/Plan.md](<../../2026-06-16/12_25_global-ui-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/Research.md](<../../2026-06-16/12_25_global-ui-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/Testcase.md](<../../2026-06-16/12_25_global-ui-system/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-19/15_05_networkerrorpopup-structure-align/Plan.md](<../../2026-06-19/15_05_networkerrorpopup-structure-align/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-19/15_05_networkerrorpopup-structure-align/Research.md](<../../2026-06-19/15_05_networkerrorpopup-structure-align/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-20/23_55_login-debug/Plan.md](<../../2026-06-20/23_55_login-debug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-20/23_55_login-debug/Research.md](<../../2026-06-20/23_55_login-debug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-21/07_30_blocking-overlay-canvasgroup-fix/Plan.md](<../../2026-06-21/07_30_blocking-overlay-canvasgroup-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-21/07_30_blocking-overlay-canvasgroup-fix/Research.md](<../../2026-06-21/07_30_blocking-overlay-canvasgroup-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/04_34_lobby-profile-logout-button/Plan.md](<../../2026-06-22/04_34_lobby-profile-logout-button/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/04_34_lobby-profile-logout-button/Research.md](<../../2026-06-22/04_34_lobby-profile-logout-button/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/05_50_loading-indicator-min-duration/Plan.md](<../../2026-06-22/05_50_loading-indicator-min-duration/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/05_50_loading-indicator-min-duration/Research.md](<../../2026-06-22/05_50_loading-indicator-min-duration/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/05_59_lobby-canvasgroup-preattach/Plan.md](<../../2026-06-22/05_59_lobby-canvasgroup-preattach/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/05_59_lobby-canvasgroup-preattach/Research.md](<../../2026-06-22/05_59_lobby-canvasgroup-preattach/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/09_32_canvas-sorting-order-fix/Plan.md](<../../2026-06-22/09_32_canvas-sorting-order-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/09_32_canvas-sorting-order-fix/Research.md](<../../2026-06-22/09_32_canvas-sorting-order-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/15_09_ingame-settings-confirm-popup-zorder/Plan.md](<../../2026-06-22/15_09_ingame-settings-confirm-popup-zorder/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/15_09_ingame-settings-confirm-popup-zorder/Research.md](<../../2026-06-22/15_09_ingame-settings-confirm-popup-zorder/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/16_37_loading-indicator-full-coverage/Plan.md](<../../2026-06-22/16_37_loading-indicator-full-coverage/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/16_37_loading-indicator-full-coverage/Research.md](<../../2026-06-22/16_37_loading-indicator-full-coverage/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-22/16_37_loading-indicator-full-coverage/Testcase.md](<../../2026-06-22/16_37_loading-indicator-full-coverage/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/00_00_코드정리-클린업/Plan.md](<../../2026-06-23/00_00_코드정리-클린업/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/00_00_코드정리-클린업/Research.md](<../../2026-06-23/00_00_코드정리-클린업/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/03_03_ngm-architecture-fix/Plan.md](<../../2026-06-23/03_03_ngm-architecture-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/03_03_ngm-architecture-fix/Research.md](<../../2026-06-23/03_03_ngm-architecture-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/04_49_cancel-button-fix/Plan.md](<../../2026-06-23/04_49_cancel-button-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/04_49_cancel-button-fix/Research.md](<../../2026-06-23/04_49_cancel-button-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/07_20_splash-login-flow/Plan.md](<../../2026-06-23/07_20_splash-login-flow/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/07_20_splash-login-flow/Research.md](<../../2026-06-23/07_20_splash-login-flow/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/15_37_구조개선-Phase2/Plan.md](<../../2026-06-23/15_37_구조개선-Phase2/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/15_37_구조개선-Phase2/Research.md](<../../2026-06-23/15_37_구조개선-Phase2/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-23/15_37_구조개선-Phase2/Testcase.md](<../../2026-06-23/15_37_구조개선-Phase2/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/07_23_하드코딩배열-파생/Plan.md](<../../2026-06-25/07_23_하드코딩배열-파생/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/07_23_하드코딩배열-파생/Research.md](<../../2026-06-25/07_23_하드코딩배열-파생/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/07_25_runtime-logger/Plan.md](<../../2026-06-25/07_25_runtime-logger/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/07_25_runtime-logger/Research.md](<../../2026-06-25/07_25_runtime-logger/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/08_23_Infrastructure-Bootstrap-의존성/Plan.md](<../../2026-06-25/08_23_Infrastructure-Bootstrap-의존성/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-25/08_23_Infrastructure-Bootstrap-의존성/Research.md](<../../2026-06-25/08_23_Infrastructure-Bootstrap-의존성/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-26/02_16_forfeit-loading-indicator-stuck/Plan.md](<../../2026-06-26/02_16_forfeit-loading-indicator-stuck/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-26/02_16_forfeit-loading-indicator-stuck/Research.md](<../../2026-06-26/02_16_forfeit-loading-indicator-stuck/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-06-27/12_26_google-login-debug/Research.md](<../../2026-06-27/12_26_google-login-debug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-07/12_28_sound-system-bugfix/Plan.md](<../../2026-07-07/12_28_sound-system-bugfix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-07/12_28_sound-system-bugfix/Research.md](<../../2026-07-07/12_28_sound-system-bugfix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/01_12_combat-hit-timing-sync/Plan.md](<../../2026-07-09/01_12_combat-hit-timing-sync/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/01_12_combat-hit-timing-sync/Research.md](<../../2026-07-09/01_12_combat-hit-timing-sync/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/Plan.md](<../../2026-07-09/06_09_ingame-lobby-volume-profile-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/Research.md](<../../2026-07-09/06_09_ingame-lobby-volume-profile-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/09_58_lobby-setting-tab-wiring/Plan.md](<../../2026-07-09/09_58_lobby-setting-tab-wiring/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-09/09_58_lobby-setting-tab-wiring/Research.md](<../../2026-07-09/09_58_lobby-setting-tab-wiring/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-12/07_55_movement-walk-anim-sync/Plan.md](<../../2026-07-12/07_55_movement-walk-anim-sync/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-12/07_55_movement-walk-anim-sync/Research.md](<../../2026-07-12/07_55_movement-walk-anim-sync/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-13/09_28_anim-resume-state-tracking/Plan.md](<../../2026-07-13/09_28_anim-resume-state-tracking/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-13/09_28_anim-resume-state-tracking/Research.md](<../../2026-07-13/09_28_anim-resume-state-tracking/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Plan.md](<../../2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Research.md](<../../2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Testcase.md](<../../2026-07-16/12_10_main-profile-cloudsave-leaderboard-port/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/14_20_email-verification-flow-cleanup/Plan.md](<../../2026-07-16/14_20_email-verification-flow-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/14_20_email-verification-flow-cleanup/Research.md](<../../2026-07-16/14_20_email-verification-flow-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/14_20_email-verification-flow-cleanup/Testcase.md](<../../2026-07-16/14_20_email-verification-flow-cleanup/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/18_06_battleaxe-aoe/Plan.md](<../../2026-07-16/18_06_battleaxe-aoe/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/18_06_battleaxe-aoe/Research.md](<../../2026-07-16/18_06_battleaxe-aoe/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/Plan.md](<../../2026-07-16/19_09_matchmaker-404-host-determination/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/Research.md](<../../2026-07-16/19_09_matchmaker-404-host-determination/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/Plan.md](<../../2026-07-17/12_59_torrentspirit-wave-aoe/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/Research.md](<../../2026-07-17/12_59_torrentspirit-wave-aoe/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-18/03_40_bloomfairy-healer/Plan.md](<../../2026-07-18/03_40_bloomfairy-healer/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-18/03_40_bloomfairy-healer/Research.md](<../../2026-07-18/03_40_bloomfairy-healer/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-18/21_38_spirit-attack-vfx-tuning/Plan.md](<../../2026-07-18/21_38_spirit-attack-vfx-tuning/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-18/21_38_spirit-attack-vfx-tuning/Research.md](<../../2026-07-18/21_38_spirit-attack-vfx-tuning/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/Plan.md](<../../2026-07-19/01_42_mushroombomber-impact-dot/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/Research.md](<../../2026-07-19/01_42_mushroombomber-impact-dot/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/10_49_neutral-goldmine-fairness/Plan.md](<../../2026-07-19/10_49_neutral-goldmine-fairness/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/10_49_neutral-goldmine-fairness/Research.md](<../../2026-07-19/10_49_neutral-goldmine-fairness/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/15_31_random-battle-map-design/Plan.md](<../../2026-07-19/15_31_random-battle-map-design/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-19/15_31_random-battle-map-design/Research.md](<../../2026-07-19/15_31_random-battle-map-design/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md](<../../2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Research.md](<../../2026-07-20/03_22_infernospirit-dot-and-attack-facing/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/Plan.md](<../../2026-07-20/10_24_quakespirit-impact-aoe/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/Research.md](<../../2026-07-20/10_24_quakespirit-impact-aoe/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/14_49_unit-combat-rules-correction/Plan.md](<../../2026-07-20/14_49_unit-combat-rules-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-20/14_49_unit-combat-rules-correction/Research.md](<../../2026-07-20/14_49_unit-combat-rules-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md](<../../2026-07-22/10_08_unit-upgrade-system/BalanceReview.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-22/10_08_unit-upgrade-system/Plan.md](<../../2026-07-22/10_08_unit-upgrade-system/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-22/10_08_unit-upgrade-system/Research.md](<../../2026-07-22/10_08_unit-upgrade-system/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-22/13_26_unit-action-sequence-implementation/Plan.md](<../../2026-07-22/13_26_unit-action-sequence-implementation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-22/13_26_unit-action-sequence-implementation/Research.md](<../../2026-07-22/13_26_unit-action-sequence-implementation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-28/12_14_skill-building-system-design/Plan.md](<../../2026-07-28/12_14_skill-building-system-design/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-07-28/12_14_skill-building-system-design/Research.md](<../../2026-07-28/12_14_skill-building-system-design/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/Plan.md](<../../2026-08-04/04_46_skill-aim-coordinate-based/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/Research.md](<../../2026-08-04/04_46_skill-aim-coordinate-based/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-08/07_40_building-death-ui-restore/Plan.md](<../../2026-08-08/07_40_building-death-ui-restore/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-08/07_40_building-death-ui-restore/Research.md](<../../2026-08-08/07_40_building-death-ui-restore/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/Plan.md](<../../2026-08-08/13_33_rally-point-blocking-overlay-bug/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/Research.md](<../../2026-08-08/13_33_rally-point-blocking-overlay-bug/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/Plan.md](<../../2026-08-10/09_34_mistshrine-heal-redesign/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/Research.md](<../../2026-08-10/09_34_mistshrine-heal-redesign/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md](<../../2026-08-10/14_12_mistshrine-heal-implementation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Research.md](<../../2026-08-10/14_12_mistshrine-heal-implementation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/Plan.md](<../../2026-08-13/05_05_production-auto-indicator-dedup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-13/05_05_production-auto-indicator-dedup/Research.md](<../../2026-08-13/05_05_production-auto-indicator-dedup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-13/07_13_network-auth-log-cleanup/LogAudit.md](<../../2026-08-13/07_13_network-auth-log-cleanup/LogAudit.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-13/07_13_network-auth-log-cleanup/Plan.md](<../../2026-08-13/07_13_network-auth-log-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-13/07_13_network-auth-log-cleanup/Research.md](<../../2026-08-13/07_13_network-auth-log-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-17/11_07_scene-independent-logging/Plan.md](<../../2026-08-17/11_07_scene-independent-logging/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-17/17_19_remaining-layers-log-migration/Plan.md](<../../2026-08-17/17_19_remaining-layers-log-migration/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-18/04_02_upgrade-subscription-fix/Plan.md](<../../2026-08-18/04_02_upgrade-subscription-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-18/20_07_log-coverage-and-rpc-guard/Plan.md](<../../2026-08-18/20_07_log-coverage-and-rpc-guard/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-19/10_16_combat-shutdown-cleanup/Plan.md](<../../2026-08-19/10_16_combat-shutdown-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-20/12_24_network-guard-sweep/Plan.md](<../../2026-08-20/12_24_network-guard-sweep/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-21/15_36_memory-integrity-checks/Plan.md](<../../2026-08-21/15_36_memory-integrity-checks/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-21/15_36_memory-integrity-checks/Research.md](<../../2026-08-21/15_36_memory-integrity-checks/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-24/17_46_b3-center-path-chase-rejoin-correction/Plan.md](<../../2026-08-24/17_46_b3-center-path-chase-rejoin-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-24/17_46_b3-center-path-chase-rejoin-correction/Research.md](<../../2026-08-24/17_46_b3-center-path-chase-rejoin-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-25/00_53_tracer-c-attack-target-facing-shadow/Plan.md](<../../2026-08-25/00_53_tracer-c-attack-target-facing-shadow/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-25/00_53_tracer-c-attack-target-facing-shadow/Research.md](<../../2026-08-25/00_53_tracer-c-attack-target-facing-shadow/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-25/02_31_document-review-consistency/Plan.md](<../../2026-08-25/02_31_document-review-consistency/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-25/02_31_document-review-consistency/Research.md](<../../2026-08-25/02_31_document-review-consistency/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-26/04_35_map-rules-correction/Plan.md](<../../2026-08-26/04_35_map-rules-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-26/04_35_map-rules-correction/Research.md](<../../2026-08-26/04_35_map-rules-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-26/18_02_c1-legacy-attack-alignment-correction/Plan.md](<../../2026-08-26/18_02_c1-legacy-attack-alignment-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-26/18_02_c1-legacy-attack-alignment-correction/Research.md](<../../2026-08-26/18_02_c1-legacy-attack-alignment-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/02_30_c1-attack-continuity-sequence-correction/Plan.md](<../../2026-08-27/02_30_c1-attack-continuity-sequence-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/02_30_c1-attack-continuity-sequence-correction/Research.md](<../../2026-08-27/02_30_c1-attack-continuity-sequence-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/17_39_c2-attack-snapshot-impact-result-shadow/Plan.md](<../../2026-08-27/17_39_c2-attack-snapshot-impact-result-shadow/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/17_39_c2-attack-snapshot-impact-result-shadow/Research.md](<../../2026-08-27/17_39_c2-attack-snapshot-impact-result-shadow/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/23_13_c2-b3-combat-transition-correction/Plan.md](<../../2026-08-27/23_13_c2-b3-combat-transition-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-27/23_13_c2-b3-combat-transition-correction/Research.md](<../../2026-08-27/23_13_c2-b3-combat-transition-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-30/19_16_c3-authoritative-result-presentation-shadow/Plan.md](<../../2026-08-30/19_16_c3-authoritative-result-presentation-shadow/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-30/19_16_c3-authoritative-result-presentation-shadow/Research.md](<../../2026-08-30/19_16_c3-authoritative-result-presentation-shadow/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/17_09_c3-continuous-attack-scope-correction/Plan.md](<../../2026-08-31/17_09_c3-continuous-attack-scope-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/17_09_c3-continuous-attack-scope-correction/Research.md](<../../2026-08-31/17_09_c3-continuous-attack-scope-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/19_40_c3-one-shot-impact-authorization/Plan.md](<../../2026-08-31/19_40_c3-one-shot-impact-authorization/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/19_40_c3-one-shot-impact-authorization/Research.md](<../../2026-08-31/19_40_c3-one-shot-impact-authorization/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/23_30_b3-c3-runtime-failure-correction/Plan.md](<../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-08-31/23_30_b3-c3-runtime-failure-correction/Research.md](<../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-01/07_07_map-docs-out-of-scope-findings/Plan.md](<../../2026-09-01/07_07_map-docs-out-of-scope-findings/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-01/07_07_map-docs-out-of-scope-findings/Research.md](<../../2026-09-01/07_07_map-docs-out-of-scope-findings/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/Plan.md](<../../2026-09-01/19_49_random-map-phase1-tilekind/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-01/19_49_random-map-phase1-tilekind/Research.md](<../../2026-09-01/19_49_random-map-phase1-tilekind/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-02/15_29_c3-authoritative-presentation-coordinator/Plan.md](<../../2026-09-02/15_29_c3-authoritative-presentation-coordinator/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-02/15_29_c3-authoritative-presentation-coordinator/Research.md](<../../2026-09-02/15_29_c3-authoritative-presentation-coordinator/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-03/03_14_random-map-phase2-generator/Plan.md](<../../2026-09-03/03_14_random-map-phase2-generator/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-03/03_14_random-map-phase2-generator/Research.md](<../../2026-09-03/03_14_random-map-phase2-generator/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Plan.md](<../../2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Research.md](<../../2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-07/20_24_c3-authoritative-presentation-token-correction/Plan.md](<../../2026-09-07/20_24_c3-authoritative-presentation-token-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-07/20_24_c3-authoritative-presentation-token-correction/Research.md](<../../2026-09-07/20_24_c3-authoritative-presentation-token-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-08/17_29_random-map-phase3-multiplayer/Plan.md](<../../2026-09-08/17_29_random-map-phase3-multiplayer/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-08/17_29_random-map-phase3-multiplayer/Research.md](<../../2026-09-08/17_29_random-map-phase3-multiplayer/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-09/01_54_c3-self-contained-impact-presentation/Plan.md](<../../2026-09-09/01_54_c3-self-contained-impact-presentation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-09/01_54_c3-self-contained-impact-presentation/Research.md](<../../2026-09-09/01_54_c3-self-contained-impact-presentation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-09/16_49_attack-entry-atomic-handoff/Plan.md](<../../2026-09-09/16_49_attack-entry-atomic-handoff/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-09/16_49_attack-entry-atomic-handoff/Research.md](<../../2026-09-09/16_49_attack-entry-atomic-handoff/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Plan.md](<../../2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Research.md](<../../2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Testcase.md](<../../2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/11_41_attack-entry-target-payload-seam/Plan.md](<../../2026-09-13/11_41_attack-entry-target-payload-seam/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/11_41_attack-entry-target-payload-seam/Research.md](<../../2026-09-13/11_41_attack-entry-target-payload-seam/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/11_41_attack-entry-target-payload-seam/Testcase.md](<../../2026-09-13/11_41_attack-entry-target-payload-seam/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/20_34_rhino-breaker-impact-marker-correction/Plan.md](<../../2026-09-13/20_34_rhino-breaker-impact-marker-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/20_34_rhino-breaker-impact-marker-correction/Research.md](<../../2026-09-13/20_34_rhino-breaker-impact-marker-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-13/20_34_rhino-breaker-impact-marker-correction/Testcase.md](<../../2026-09-13/20_34_rhino-breaker-impact-marker-correction/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-14/00_56_quake-spirit-impact-marker-correction/Plan.md](<../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-14/00_56_quake-spirit-impact-marker-correction/Research.md](<../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-14/00_56_quake-spirit-impact-marker-correction/Testcase.md](<../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Testcase.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md](<../../2026-09-14/12_17_rematch-map-selection/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-14/12_17_rematch-map-selection/Research.md](<../../2026-09-14/12_17_rematch-map-selection/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-15/15_19_leftover-cleanup/Plan.md](<../../2026-09-15/15_19_leftover-cleanup/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-15/15_19_leftover-cleanup/Research.md](<../../2026-09-15/15_19_leftover-cleanup/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-16/06_27_post-game-leave-ui/Plan.md](<../../2026-09-16/06_27_post-game-leave-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-16/06_27_post-game-leave-ui/Research.md](<../../2026-09-16/06_27_post-game-leave-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-19/23_06_battleaxe-impact-marker-correction/Plan.md](<../../2026-09-19/23_06_battleaxe-impact-marker-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-19/23_06_battleaxe-impact-marker-correction/Research.md](<../../2026-09-19/23_06_battleaxe-impact-marker-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-20/04_10_infernospirit-impact-timeline-correction/Plan.md](<../../2026-09-20/04_10_infernospirit-impact-timeline-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-20/04_10_infernospirit-impact-timeline-correction/Research.md](<../../2026-09-20/04_10_infernospirit-impact-timeline-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-20/16_34_infernospirit-attack-facing-evidence/Plan.md](<../../2026-09-20/16_34_infernospirit-attack-facing-evidence/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-20/16_34_infernospirit-attack-facing-evidence/Research.md](<../../2026-09-20/16_34_infernospirit-attack-facing-evidence/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-21/02_42_infernospirit-movement-jump-correction/Plan.md](<../../2026-09-21/02_42_infernospirit-movement-jump-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-21/02_42_infernospirit-movement-jump-correction/Research.md](<../../2026-09-21/02_42_infernospirit-movement-jump-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-22/12_13_streamspirit-production-timeline-correction/Plan.md](<../../2026-09-22/12_13_streamspirit-production-timeline-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-22/12_13_streamspirit-production-timeline-correction/Research.md](<../../2026-09-22/12_13_streamspirit-production-timeline-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md](<../../2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-22/19_48_foxmagician-production-timeline-correction/Research.md](<../../2026-09-22/19_48_foxmagician-production-timeline-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Plan.md](<../../2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Research.md](<../../2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Plan.md](<../../2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Research.md](<../../2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md](<../../2026-09-28/11_50_single-map-failure-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/11_50_single-map-failure-ui/Research.md](<../../2026-09-28/11_50_single-map-failure-ui/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/21_05_ember-spirit-attack-correction/Plan.md](<../../2026-09-28/21_05_ember-spirit-attack-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/21_05_ember-spirit-attack-correction/Research.md](<../../2026-09-28/21_05_ember-spirit-attack-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/21_55_flamespirit-multihit-attack-correction/Plan.md](<../../2026-09-28/21_55_flamespirit-multihit-attack-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-28/21_55_flamespirit-multihit-attack-correction/Research.md](<../../2026-09-28/21_55_flamespirit-multihit-attack-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/03_13_boulderspirit-attack-correction/Plan.md](<../../2026-09-29/03_13_boulderspirit-attack-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/03_13_boulderspirit-attack-correction/Research.md](<../../2026-09-29/03_13_boulderspirit-attack-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/16_16_post-game-ui-close-out/Plan.md](<../../2026-09-29/16_16_post-game-ui-close-out/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/16_16_post-game-ui-close-out/Research.md](<../../2026-09-29/16_16_post-game-ui-close-out/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/19_09_tidespirit-attack-correction/Plan.md](<../../2026-09-29/19_09_tidespirit-attack-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/19_09_tidespirit-attack-correction/Research.md](<../../2026-09-29/19_09_tidespirit-attack-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/Plan.md](<../../2026-09-29/23_28_bearguard-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-29/23_28_bearguard-attack-timing-correction/Research.md](<../../2026-09-29/23_28_bearguard-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/Plan.md](<../../2026-09-30/01_21_lionknight-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/Research.md](<../../2026-09-30/01_21_lionknight-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/12_37_attack-presentation-expiry-diagnosis/Plan.md](<../../2026-09-30/12_37_attack-presentation-expiry-diagnosis/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/12_37_attack-presentation-expiry-diagnosis/Research.md](<../../2026-09-30/12_37_attack-presentation-expiry-diagnosis/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/15_01_multi-map-projection-failure/Plan.md](<../../2026-09-30/15_01_multi-map-projection-failure/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/15_01_multi-map-projection-failure/Research.md](<../../2026-09-30/15_01_multi-map-projection-failure/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/15_01_popup-log-coverage/Plan.md](<../../2026-09-30/15_01_popup-log-coverage/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/18_02_spearman-attack-timing-correction/Plan.md](<../../2026-09-30/18_02_spearman-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/18_02_spearman-attack-timing-correction/Research.md](<../../2026-09-30/18_02_spearman-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/23_19_torrentspirit-attack-timing-correction/Plan.md](<../../2026-09-30/23_19_torrentspirit-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-09-30/23_19_torrentspirit-attack-timing-correction/Research.md](<../../2026-09-30/23_19_torrentspirit-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Plan.md](<../../2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Research.md](<../../2026-10-01/01_00_torrentspirit-wave-contact-diagnosis/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-01/05_19_rules-implementation-audit/Plan.md](<../../2026-10-01/05_19_rules-implementation-audit/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-01/05_19_rules-implementation-audit/Research.md](<../../2026-10-01/05_19_rules-implementation-audit/Research.md>) | 메인 전달 · 1~100행 일부 열람 |
| [Assets/_Project/Docs/_Tasks/2026-10-02/15_25_common-post-combat-recovery-diagnosis/Plan.md](<../../2026-10-02/15_25_common-post-combat-recovery-diagnosis/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-02/15_25_common-post-combat-recovery-diagnosis/Research.md](<../../2026-10-02/15_25_common-post-combat-recovery-diagnosis/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Plan.md](<../../2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Research.md](<../../2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md](<../../2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md](<../../2026-10-03/03_28_bloomfairy-heal-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md](<../../2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md](<../../2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Plan.md](<../../2026-10-08/10_55_assault-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md](<../../2026-10-08/10_55_assault-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md](<../../2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md](<../../2026-10-08/17_09_bloomfairy-heal-vfx-naming/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md](<../../2026-10-09/08_12_mushroombomber-vfx-split/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Research.md](<../../2026-10-09/08_12_mushroombomber-vfx-split/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md](<../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md](<../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-10/23_30_sniper-attack-timing-correction/Plan.md](<../../2026-10-10/23_30_sniper-attack-timing-correction/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-10/23_30_sniper-attack-timing-correction/Research.md](<../../2026-10-10/23_30_sniper-attack-timing-correction/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-11/21_50_wave-contact-authority-isolation/Plan.md](<../../2026-10-11/21_50_wave-contact-authority-isolation/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-11/21_50_wave-contact-authority-isolation/Research.md](<../../2026-10-11/21_50_wave-contact-authority-isolation/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md](<../../2026-10-12/01_41_log-rules-history-archive/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Research.md](<../../2026-10-12/01_41_log-rules-history-archive/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-12/07_08_multi-first-map-failure-ui/Plan.md](<../../2026-10-12/07_08_multi-first-map-failure-ui/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-12/15_28_network-rules-migration/Plan.md](<../../2026-10-12/15_28_network-rules-migration/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/10_37_rally-point-fix/Plan.md](<../../2026-10-13/10_37_rally-point-fix/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/10_37_rally-point-fix/Research.md](<../../2026-10-13/10_37_rally-point-fix/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md](<../../2026-10-13/13_53_vfx-code-followups/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md](<../../2026-10-13/13_53_vfx-code-followups/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/19_03_main-docs-organization/Plan.md](<../../2026-10-13/19_03_main-docs-organization/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/19_03_main-docs-organization/Research.md](<../../2026-10-13/19_03_main-docs-organization/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/22_53_docs-checker-maintenance/Plan.md](<../../2026-10-13/22_53_docs-checker-maintenance/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-13/22_53_docs-checker-maintenance/Research.md](<../../2026-10-13/22_53_docs-checker-maintenance/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Plan.md](<../../2026-10-14/10_04_post-merge-unit-regression-audit/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-14/10_04_post-merge-unit-regression-audit/Research.md](<../../2026-10-14/10_04_post-merge-unit-regression-audit/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-15/23_18_editor-script-organization/Plan.md](<../../2026-10-15/23_18_editor-script-organization/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-15/23_18_editor-script-organization/Research.md](<../../2026-10-15/23_18_editor-script-organization/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-16/14_25_docs-role-maintenance/Plan.md](<../../2026-10-16/14_25_docs-role-maintenance/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-16/14_25_docs-role-maintenance/Research.md](<../../2026-10-16/14_25_docs-role-maintenance/Research.md>) | 메인 전달 · 1~75행 일부 열람 |
| [Assets/_Project/Docs/_Tasks/2026-10-17/04_13_ui-input-field-improvements/Plan.md](<../../2026-10-17/04_13_ui-input-field-improvements/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-17/04_13_ui-input-field-improvements/Research.md](<../../2026-10-17/04_13_ui-input-field-improvements/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/04_39_game-end-modal-input/Plan.md](<../../2026-10-19/04_39_game-end-modal-input/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/04_39_game-end-modal-input/Research.md](<../../2026-10-19/04_39_game-end-modal-input/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/15_21_regression-suite-contract-fixes/Plan.md](<../../2026-10-19/15_21_regression-suite-contract-fixes/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/15_21_regression-suite-contract-fixes/Research.md](<../../2026-10-19/15_21_regression-suite-contract-fixes/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/15_41_ui-layout-audit/Plan.md](<../../2026-10-19/15_41_ui-layout-audit/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md](<../../2026-10-19/15_41_ui-layout-audit/Research.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Plan.md](<../../2026-10-20/12_19_matchmaking-environment-diagnosis/Plan.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md](<../../2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>기존 Log 문서 — 10개</summary>

| 파일 | 조사 상태 |
|---|---|
| [Assets/_Project/Docs/_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md](<../../../_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-05-06/01_19_movement-slot-ghostfix/Log.md](<../../../_Logs/2026-05-06/01_19_movement-slot-ghostfix/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-05-11/23_19_unit-movement-redesign/Log.md](<../../../_Logs/2026-05-11/23_19_unit-movement-redesign/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-05-14/14_30_unit-rotation-system-fix/Log.md](<../../../_Logs/2026-05-14/14_30_unit-rotation-system-fix/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/Log.md](<../../../_Logs/2026-07-22/13_26_unit-action-sequence-implementation/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-08-27/02_30_c1-attack-continuity-sequence-correction/Log.md](<../../../_Logs/2026-08-27/02_30_c1-attack-continuity-sequence-correction/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-08-31/12_54_c3-presentation-shadow-correction/Log.md](<../../../_Logs/2026-08-31/12_54_c3-presentation-shadow-correction/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Log.md](<../../../_Logs/2026-09-07/05_37_assault-action-handoff-c3-lifecycle-correction/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Log.md](<../../../_Logs/2026-09-13/04_07_attack-entry-idempotent-handoff-spatial-proof/Log.md>) | 목록 확보 · 내용 미조사 |
| [Assets/_Project/Docs/_Logs/2026-09-13/11_41_attack-entry-target-payload-seam/Log.md](<../../../_Logs/2026-09-13/11_41_attack-entry-target-payload-seam/Log.md>) | 목록 확보 · 내용 미조사 |

</details>

<details>
<summary>관리 대상 .claude 문서 — 41개</summary>

외부 skills/plugins/worktrees는 관리 범위에서 제외한다. .agents/skills 등의 외부 운영 스킬은 게임 현행 규범으로 자동 적용하지 않는다.

| 파일 | 조사 상태 |
|---|---|
| [.claude/MEMORY.md](<../../../../../../.claude/MEMORY.md>) | 공용 지침·수용 경계 열람 |
| [.claude/agent-memory/asset-prompt-crafter/MEMORY.md](<../../../../../../.claude/agent-memory/asset-prompt-crafter/MEMORY.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/document-manager/MEMORY.md](<../../../../../../.claude/agent-memory/document-manager/MEMORY.md>) | 일부 열람 · 문서판정/과거 기록/Task 관련 인덱스 |
| [.claude/agent-memory/document-manager/doc-conventions.md](<../../../../../../.claude/agent-memory/document-manager/doc-conventions.md>) | 일부 열람 · Recipient-effect lifecycle 및 관련 검색 |
| [.claude/agent-memory/game-design-lead/MEMORY.md](<../../../../../../.claude/agent-memory/game-design-lead/MEMORY.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-design-lead/ai-scenarios.md](<../../../../../../.claude/agent-memory/game-design-lead/ai-scenarios.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-design-lead/core-systems.md](<../../../../../../.claude/agent-memory/game-design-lead/core-systems.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/3d-transition.md](<../../../../../../.claude/agent-memory/game-programmer/3d-transition.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/MEMORY.md](<../../../../../../.claude/agent-memory/game-programmer/MEMORY.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/architecture.md](<../../../../../../.claude/agent-memory/game-programmer/architecture.md>) | 일부 열람 · 35~48행 · AUD-009 |
| [.claude/agent-memory/game-programmer/attack-direction-refactor.md](<../../../../../../.claude/agent-memory/game-programmer/attack-direction-refactor.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/camera-and-view.md](<../../../../../../.claude/agent-memory/game-programmer/camera-and-view.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/check-docs-tool.md](<../../../../../../.claude/agent-memory/game-programmer/check-docs-tool.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/combat-fixes.md](<../../../../../../.claude/agent-memory/game-programmer/combat-fixes.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/gameplay-systems.md](<../../../../../../.claude/agent-memory/game-programmer/gameplay-systems.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/hex-grid.md](<../../../../../../.claude/agent-memory/game-programmer/hex-grid.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/logging.md](<../../../../../../.claude/agent-memory/game-programmer/logging.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/network-infra.md](<../../../../../../.claude/agent-memory/game-programmer/network-infra.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/network-todo.md](<../../../../../../.claude/agent-memory/game-programmer/network-todo.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/network.md](<../../../../../../.claude/agent-memory/game-programmer/network.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/random-matching-bugfix.md](<../../../../../../.claude/agent-memory/game-programmer/random-matching-bugfix.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/rendering-and-animation.md](<../../../../../../.claude/agent-memory/game-programmer/rendering-and-animation.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/skill-aim-coordinate.md](<../../../../../../.claude/agent-memory/game-programmer/skill-aim-coordinate.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/ui-system.md](<../../../../../../.claude/agent-memory/game-programmer/ui-system.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/unit-building.md](<../../../../../../.claude/agent-memory/game-programmer/unit-building.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/unit-stats-and-combat.md](<../../../../../../.claude/agent-memory/game-programmer/unit-stats-and-combat.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/vfx-presentation.md](<../../../../../../.claude/agent-memory/game-programmer/vfx-presentation.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/game-programmer/work-history.md](<../../../../../../.claude/agent-memory/game-programmer/work-history.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/project-orchestrator/MEMORY.md](<../../../../../../.claude/agent-memory/project-orchestrator/MEMORY.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/project-orchestrator/project-history.md](<../../../../../../.claude/agent-memory/project-orchestrator/project-history.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/project-orchestrator/roadmap-3d.md](<../../../../../../.claude/agent-memory/project-orchestrator/roadmap-3d.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/qa-tester/MEMORY.md](<../../../../../../.claude/agent-memory/qa-tester/MEMORY.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/qa-tester/patterns.md](<../../../../../../.claude/agent-memory/qa-tester/patterns.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agent-memory/qa-tester/qa_history.md](<../../../../../../.claude/agent-memory/qa-tester/qa_history.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agents/asset-prompt-crafter.md](<../../../../../../.claude/agents/asset-prompt-crafter.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agents/document-manager.md](<../../../../../../.claude/agents/document-manager.md>) | 담당 지침 열람 |
| [.claude/agents/game-design-lead.md](<../../../../../../.claude/agents/game-design-lead.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agents/game-programmer.md](<../../../../../../.claude/agents/game-programmer.md>) | 목록 확보 · 내용 미조사 |
| [.claude/agents/qa-tester.md](<../../../../../../.claude/agents/qa-tester.md>) | 목록 확보 · 내용 미조사 |
| [.claude/mistakes.md](<../../../../../../.claude/mistakes.md>) | 메인 전달 · 목차와 관련 항목 일부 열람 |
| [.claude/mistakes/unit-action-correction.md](<../../../../../../.claude/mistakes/unit-action-correction.md>) | 목록 확보 · 내용 미조사 |

</details>

### 7.1 첫 구현 증분 장부(2026-10-23)

시작 목록310개는 그대로 보존한다. 신규 C#1개·meta1개를 아래에 편입해 현재 프로젝트 C#311개를 추적한다. 기존 파일3개 변경은 시작 장부의 해당 행에서 AUD-008로 연결한다.

| 파일 | 증분 이유·현재 상태 |
|---|---|
| [Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs](<../../../../Scripts/Application/Interfaces/IBuildingObjectProvider.cs>) | Plan2.3 승인에 따른 신규 생성 · AUD-008 · 구현/정적 대조 완료, 컴파일·실기 미실행 |
| [Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs.meta](<../../../../Scripts/Application/Interfaces/IBuildingObjectProvider.cs.meta>) | Plan2.3 승인에 따른 신규 생성 · AUD-008 · 구현/정적 대조 완료, 컴파일·실기 미실행 |

### 7.2 넷째 묶음 신규 파일 증분(2026-10-23)

시작 장부와 첫 증분을 보존하고 인증 계약 C#2개·meta2개를 추가한다. 이번 묶음 후 프로젝트 C#313개, Application88개, Assets 전체 C#331개다. 이는 현재 집계이며 시작310/85/328개 및 이전 회차 snapshot과 구분한다.

| 신규 파일 | 생성 근거·상태 |
|---|---|
| [Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs](<../../../../Scripts/Application/Interfaces/IAuthenticationService.cs>) | Plan2.6 · AUD-015 · 소스 정리·정적 대조 완료, 컴파일/실기 미실행 |
| [Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs.meta](<../../../../Scripts/Application/Interfaces/IAuthenticationService.cs.meta>) | Plan2.6 · AUD-015 · 소스 정리·정적 대조 완료, 컴파일/실기 미실행 · GUID b52ffc5a58464dce9595772ea310c144 |
| [Assets/_Project/Scripts/Application/AuthException.cs](<../../../../Scripts/Application/AuthException.cs>) | Plan2.6 · AUD-015 · 소스 정리·정적 대조 완료, 컴파일/실기 미실행 |
| [Assets/_Project/Scripts/Application/AuthException.cs.meta](<../../../../Scripts/Application/AuthException.cs.meta>) | Plan2.6 · AUD-015 · 소스 정리·정적 대조 완료, 컴파일/실기 미실행 · GUID 71de453ebf0f49df99c37637e340ae58 |

### 7.3 다섯째 묶음 신규 파일 증분(2026-10-23)

시작 목록과 이전 증분은 보존한다. Plan2.7로 DTO C#1개/meta1개를 추가했으며 이 묶음의 코드 전331개 C# snapshot과 신규1개를 구분한다.

| 신규 파일 | 근거·상태 |
|---|---|
| [Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs](<../../../../Scripts/Application/Models/AIDifficultyParameters.cs>) | Plan2.7 · AUD-016 · 소스/정적 대조 완료, 컴파일/실기 미실행 |
| [Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs.meta](<../../../../Scripts/Application/Models/AIDifficultyParameters.cs.meta>) | Plan2.7 · AUD-016 · 소스/정적 대조 완료, 컴파일/실기 미실행 · GUID c2846d09dbd9404c93926158b072c8fe |

### 7.4 여섯째 묶음 신규 파일 증분(2026-10-23)

Plan2.8의 계약/adapter C#2개·meta2개를 추가하고 이전 장부를 보존한다. 현재 실측은 프로젝트 C#316개/Assets 전체334개이며 이번 코드 전332개 snapshot과 구분한다.

| 신규 파일 | 근거·상태 |
|---|---|
| [Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs](<../../../../Scripts/Application/Interfaces/IUgsAuthenticationService.cs>) | Plan2.8 · AUD-018 · 소스/독립 정적 대조 완료, 컴파일/실기 미실행 |
| [Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs.meta](<../../../../Scripts/Application/Interfaces/IUgsAuthenticationService.cs.meta>) | Plan2.8 · AUD-018 · 소스/독립 정적 대조 완료, 컴파일/실기 미실행 · GUID dc6b3c26d2e548f78d8d70821d108e5b |
| [Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs](<../../../../Scripts/Infrastructure/Auth/UgsAuthenticationService.cs>) | Plan2.8 · AUD-018 · 소스/독립 정적 대조 완료, 컴파일/실기 미실행 |
| [Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs.meta](<../../../../Scripts/Infrastructure/Auth/UgsAuthenticationService.cs.meta>) | Plan2.8 · AUD-018 · 소스/독립 정적 대조 완료, 컴파일/실기 미실행 · GUID 758217fe5ff54802804ed9fc6998df8a |

### 7.5 UnitView partial 신규6파일 증분(2026-10-24 KST)

Plan2.17/AUD-003으로3C#/3meta를 추가한다. 시작목록과 기존증분은 보존한다. Assets전체 C#334개 snapshot의 기존2개변경·332개동일+신규3개=337개이며 프로젝트 Scripts는319개다. 신규GUID는 Assets전체meta에서 각자1건이며 기존UnitView MonoScript GUID8b032e7de55b97a44aa94ce3b24f914b는 유지했다.

| 신규 파일 | 근거·상태 |
|---|---|
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.MovementAuthority.cs](<../../../../Scripts/Presentation/Unit/UnitView.MovementAuthority.cs>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.MovementAuthority.cs.meta](<../../../../Scripts/Presentation/Unit/UnitView.MovementAuthority.cs.meta>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN · GUID 34f2f7a445f146739617334a18f4d383 |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.Movement.cs](<../../../../Scripts/Presentation/Unit/UnitView.Movement.cs>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.Movement.cs.meta](<../../../../Scripts/Presentation/Unit/UnitView.Movement.cs.meta>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN · GUID 54af2cbe64204edbad39831787322535 |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.Healing.cs](<../../../../Scripts/Presentation/Unit/UnitView.Healing.cs>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN |
| [Assets/_Project/Scripts/Presentation/Unit/UnitView.Healing.cs.meta](<../../../../Scripts/Presentation/Unit/UnitView.Healing.cs.meta>) | Plan2.17 · AUD-003 · 원문이동/독립정적대조 완료·compile/실기 NOT RUN · GUID 11ce2d3c8feb4789bc36d4a2b89234a9 |

## 8. 현재 결론과 다음 조사

시작 기준 전체 경로 목록은 확보했으나 310개 프로젝트 스크립트 및 문서의 기능 단위 의미 검토는 완료되지 않았다. Domain/Application 초기 의존성 조사를 메인/프로그래머가 수행했고, 첫 작업 묶음 IUnitView 팩토리 추상화를 Plan2.3에 구체화했다. 이후 사용자의 「바로진행하자」로 해당 첫 묶음이 승인돼 구현과 한정 정적 대조를 마쳤다. 컴파일·기기·게임 실행과 사용자 수용은 미확인이다. 전체 의미 감사는 계속 필요하며 대형 파일 분할이나 삭제를 먼저 확정하지 않는다. 구현으로 생성한 파일은 시작 목록을 보존한 채 아래 증분 장부로 추가했다. 구현 후 근거는6절의 별도 범위에 기록한다.

## 9. 단계2 조사: 첫 묶음 이후 의존성 재검색과 다음 소규모 정리안

### 9.1 범위·방법·한계(2026-10-23)

**아래9.1~9.3의 검색 수와 행번호는 Plan2.4 구현 전 조사 시점의 기록이다.** Application4파일6행 검색 결과를 using 제거 후 현재 결과로 재사용하지 않는다. 구현 후 소스 정리 결과는9.8절로 구분한다.

사용자 「다음 진행해」 이후 프로그래머가 현재 Domain51개/Application86개를 재검색했고, 문서 담당은 임시 산출물 `/tmp/hexiege-layer-audit-next.json`의 범위·검색식·행별 결과·한계를 읽었다. 산출물 생성 시각은2026-10-23 05:23:56 UTC(14:23:56 KST)다. 임시 파일의 영구 보존에 의존하지 않도록 핵심 방법과 결과를 아래에 남긴다. 첫 묶음 신규 Application 인터페이스1개로 Application은 시작85→현재86개다.

검색은 `Assets/_Project/Scripts`에서 block/line 주석·일반/축자 문자열 리터럴을 제외한 텍스트에 대해 수행했다. Domain에서는 Hexiege.Application/Infrastructure/Presentation/Bootstrap/Core 및 UnityEngine, Unity.Netcode, Firebase, Unity.Services 토큰을 찾고, Application에서는 Hexiege.Infrastructure/Presentation/Bootstrap/Core 및 Unity.Netcode, Firebase, Unity.Services 토큰을 찾았다. 경계·하위 namespace를 포함한 토큰 검색이며 C# compiler/symbol resolver가 아니다. 보간 문자열 안 표현식을 의미 해석하지 않으므로 실제 의존 누락 가능성이 있다. 전체 파일의 호출·에셋·기능 계약을 읽은 감사로 승격하지 않는다.

메인이 같은 방법을 직접 재실행해 Domain51/Application86 파일 수와 각 행 hit가 산출물과 완전히 일치함(Domain0/Application6행)을 확인했다고 전달했다. 이 재현은 검색 결과의 재현이며 전체 의미 감사가 아니다.

아래는 산출물에 기록된 정규식 메타데이터의 JSON 표현 그대로다. 역슬래시 escaping을 포함한 기록 형식이며, 재실행 때 실제 regex escaping을 확인한다. 후속 담당은 scope·literal 제외 처리·토큰 검색을 함께 재현하고 결과를 이 행별 장부와 대조한다.

```json
{
  "scope": "Assets/_Project/Scripts",
  "comment_literal_pattern": "/\\*.*?\\*/|//[^\\n]*|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"",
  "patterns": {
    "Domain": "\\b(?:Hexiege\\.(?:Application|Infrastructure|Presentation|Bootstrap|Core)|UnityEngine|Unity\\.Netcode|Firebase|Unity\\.Services)\\b",
    "Application": "\\b(?:Hexiege\\.(?:Infrastructure|Presentation|Bootstrap|Core)|Unity\\.Netcode|Firebase|Unity\\.Services)\\b"
  }
}
```

| 범위 | 검색 결과 | 한계 |
|---|---|---|
| Domain51 | 지정 금지 namespace 토큰0 | 전체 의존성/컴파일/실행 PASS 아님 |
| Application86 | 4파일6행 | actual type·full qualification·alias·확장 메서드·조건부 경로의 의미 판단은 별도 |
| IUnitView | 첫 묶음의 구체 Infrastructure 타입 노출 해소 확인 | 첫 묶음 컴파일/실기 수용은 여전히 미확인 |

| 파일 | 재검색 hit 행·내용 | 연결 |
|---|---|---|
| Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs | 4행 Hexiege.Core import | AUD-013 / Plan2.4 |
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | 31행 Hexiege.Infrastructure import | AUD-009 / 예외·수명·타입 조사 후 판단 |
| Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs | 23행 Hexiege.Infrastructure import | AUD-001 관련 인증 추상화 후속 |
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | 26행 Unity.Services.Authentication,27행 Unity.Services.Core,29행 Hexiege.Infrastructure import | AUD-001 / 토큰3행을 독립 버그3개로 세지 않음 |

### 9.2 AUD-012: 방어 타워 스탯 주석과 실제 소비

문서 담당이 BuildingStats.cs33~54/159~175행과 TowerCombatUseCase129~152/220~242행, Bootstrap.Setup140~188행을 직접 읽었다. BuildingStats의 공격력·공격 쿨다운 필드와 getter의4주석이 현재 사용과 맞지 않는다. 실제로 TowerCombatUseCase145행은 공격 뒤 GetAttackCooldown을 읽고,231행은 GetAttackPower를 읽어 방어력 계산과 피해 적용으로 전달한다.

건물 Config값 주입은 Bootstrap.Setup155~182행이며 Human159/160,Spirit170/171,Transcendence181/182행이 각 건물 공격력/쿨다운이다. **83/87행 유닛 값은 이 건물 근거로 인용하지 않는다.** Buildings의 정확한 H2 「방어 타워 시스템」에서 규칙5 「공격 후 쿨다운」과 규칙9 「서버 권위 처리」를 직접 읽었다. 전자는 공격 후 해당 건물 쿨다운을 사용하고, 후자는 타겟/사거리/피해를 서버에서 처리하는 동작이다. 새 타워 기능을 추가하는 계획이 아니라 이미 구현된 용도를 주석에 정확히 설명하는 후보이며 수치·처리 순서는 변경하지 않는다.

### 9.3 AUD-013: 필요하지 않은 Core import 후보

TimedDamageEffectPresentation.cs1~80행에서 Core import와 UnityEngine.Time.timeAsDouble 실제 사용을 직접 확인했다. 프로그래머는 Core4파일의 선언 타입 HexMetrics/HexMetricsCoordinateMapper/SingletonMonoBehaviour/ViewConverter를 수집해 해당 파일 본문 참조를 찾지 못했고, Core 확장 메서드 정의0을 전달했다. 메인도 Core4타입 본문 사용0 및 Core 확장 정의0을 직접 재검색해 확인했다고 전달했다. 이는 소스 대조에 의한 불필요 using 후보이며 실제 컴파일 결과가 아니다. UnityEngine.Time 호출과 Clock의 의미를 그대로 유지한다. import를 지웠다는 사실로 채널의 순수C#화나 VFX/피해 동기화 문제가 해결됐다고 주장하지 않는다.

### 9.4 다음 작업과 미완 범위

Plan2.4의 기존 C#2파일 주석4곳·using1행 정리는 사용자 「이 범위로 구현 진행」으로 명시 승인됐고 소스 정리·정적 대조를 마쳤다. 컴파일·실기 미실행이다. AI/Auth는 별도 설계 조사 중으로 이번 소규모 변경에 섞지 않는다. 모든 기존 장부의 미조사를 조사 완료로 일괄 승격하지 않는다. 단계2에서 실제 내용 대조한 BuildingStats/TowerCombatUseCase/Bootstrap.Setup/TimedDamageEffectPresentation 범위만 부분 열람으로 기록한다. 첫 묶음과 새 후보 모두 사용자 테스트 완료로 보고하지 않는다.

### 9.5 AI/Auth 후속 조사 제안과 현재 승인 경계

다음은 프로그래머 전달 조사이며 Plan2.4의2파일 구현 범위에 포함하지 않는다. 메인은 인증 계약 집계 artifact를 확인했다. 별도 구체 계획 공유·승인 전에 구현하지 않는다.

- 인증의 직접 소비 계약은 Login의6속성+10Task 멤버16개와 AccountLink의 Google/Email 연동2개로 고유18개다. AccountLink의 IsAnonymous는 기존6속성에 포함돼 중복 가산하지 않는다. LoginUseCase.cs78~93행 속성,168/198/229/266/269/287/307/342/371/418/507행 호출과 AccountLinkUseCase.cs76/90/134/146행이 소비 위치다.
- AuthErrorReason/AuthException은 Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs618/639행에 정의돼 있어 서비스 interface만 추가하면 예외 타입 노출이 남는다. 제안은 신규 IAuthenticationService와 Application/Auth/AuthException(enum 포함), FirebaseAuthService 구현/타입 이관, Login/AccountLink 의존 타입 교체다. 기존 C#3개+신규 C#2개+meta2개, 총7파일 예상이며 정확 파일명·최종 범위는 후속 계획에서 확정한다. 사용9파일(Infra1/Application2/LoginView6)은 사용처 범위이며 전부 수정한다는 뜻이 아니다.
- LoginUseCase.cs26/27행 UGS import와399/481~515행 직접 사용은 위 서비스 추상화 뒤에도 남는다. 인증 추상화 일부를 전체 Application 외부의존 해소로 보고하지 않는다.
- AI의 최소 제안은 AIOpponentController 생성자에 Func<RaceId> live provider를 주입하고 Bootstrap.Setup.cs833행 생성 지점에서 전달하는 기존2파일 안이다. GameRaceForAi930~932행의 실제 종족 최신값 조회 의미를 유지해야 하며 초기값 snapshot으로 바꾸지 않는다.
- DifficultyParams 의존은 위 AI 최소안 뒤에도 남는다. AIConfig.cs의9개 직렬화 필드와 AI 규칙4/29/33~34를 포함한 별도 검토가 필요하다는 프로그래머 전달 범위다. 이 조항들은 문서 담당이 여기서 직접 의미 대조 완료한 근거로 쓰지 않는다. 과거 예외 QA 기록은 사용자 정책 승인 단독 증거가 아니다.

첫 묶음은 코드 정적 대조 완료·실기 미확인, 둘째 묶음은 주석4곳+using1행의 소스 정리·정적 대조 완료·컴파일/실기 미실행이다. 전체 기능·의존성 감사와 죽은 코드 정리는 계속 미완이다.

### 9.6 단계2 문서 작성 후 메인 확인(2026-10-23)

메인은 후속 문서 반영 후 Research 로컬1075링크/Plan1링크 오류0, 시작 장부 목록누락0, `python3 Tools/check_docs.py` exit0·문제0을 확인했다고 전달했다. 지정 namespace 검색도 Domain0/Application6행으로 재현 일치했다. 단계2에서는 새 코드 변경 없이 기존 첫 묶음의 diff를 유지했다. Plan2.4는 사용자에게 공유하고 구체 승인 요청을 보냈으며 명시적 구현 승인 대기다. 이 확인은 후속 문서·검색 범위의 결과로서 첫 묶음이나 새 후보의 컴파일·실기·사용자 수용 완료를 뜻하지 않는다. 앞선 첫 묶음 검사 기록은 당시 범위의 원문으로 보존한다.

### 9.7 다음 묶음 구현 승인(2026-10-23)

9.6절의 구현 전 문서 검사·승인 요청 이후 사용자가 「이 범위로 구현 진행」으로 Plan2.4를 명시 승인했다. game-programmer가 기존 BuildingStats.cs4주석과 TimedDamageEffectPresentation.cs Core using1행을 정리 중이다. 최종 소스 대조·검사 결과는 메인 확인 뒤 별도 기록한다. 첫 묶음 실기 수용 미확인과 AI/Auth 후속안의 미승인 상태는 유지한다.

### 9.8 둘째 묶음 구현·독립 정적 대조(2026-10-23)

사용자 「이 범위로 구현 진행」 승인으로 Plan2.4의 기존 C#2파일을 정리했다. BuildingStats.cs45/48/159/170행의 XML 주석4줄을 바꿨다(4줄 삭제·4줄 추가). 필드/getter가 방어 타워의 공격 피해 계산과 공격 후 대기 시간 설정에 사용된다는 현재 용도를 설명한다. 필드·반환·폴백·수치·실행 순서는 바꾸지 않았다.

TimedDamageEffectPresentation.cs는 구현 전4행의 Core using1줄만 삭제했다. UnityEngine.Time.timeAsDouble은 구현 전33행에서 현재32행으로 이동했으며 내용은 유지됐다. 새 코드·meta는 생성하지 않았다. 문서 담당도 위4주석·import 제거 후 파일 처음 부분과 현재 Time32행을 직접 읽었다.

메인은 수정 전 backup SHA를 검증한 뒤 문자열 리터럴을 보존하고 주석·공백을 제외한 tokenizer로 BuildingStats의 실행 토큰/리터럴 동일을 확인했다. 모든 수정 행은 XML 주석이었다. Timed 파일은 원본에서 `using Hexiege.Core;`와 그 줄바꿈1개를 제거한 텍스트가 현재 파일과 정확히 일치함을 확인했다. 비교 수단 자체도 사본 fixture4개로 검증했다: 주석 추가=동일, 기존 공백 확대=동일, 숫자 변경=상이, 문자열 한 글자 변경=상이. `git diff --check`0이다. 메인의 독립 대조 수치는 전달 근거이며 컴파일·실기 증거가 아니다.

메인은 구현 후 동일 마스킹·patterns로 재검색했다. Domain51파일은0hit, Application86파일은3파일5행(AIOpponentController.cs31행, AccountLinkUseCase.cs23행, LoginUseCase.cs26/27/29행)이며 Timed의 Core hit가 해소됐다. 문서 담당도 `/tmp/hexiege-plan24-post-scan.json`의 파일 수·행별 결과를 읽었다.9.1의0/6은 구현 전 조사 기록으로 보존한다. 시작 Assets C#328개 중 누적 변경5개·무변경323개와 신규 C#1개도 메인이 재확인했다. 이번2파일 변경과 첫 묶음3파일을 합한 누적5를 구분한다. 컴파일·Unity 실기·빌드는 미실행이며 첫 묶음 수용이나 전체 감사·AI/Auth 후속 구현·죽은 코드 전체 정리 완료로 확대하지 않는다. 상시문서·메모리·TC/QA·기존 타 Task는 변경하지 않았다.

## 10. 셋째 묶음 착수: AI 종족 조회의 구성 루트 주입

### 10.1 실행 지시와 현재 범위(2026-10-23)

사용자의 「바로 작업 진행해」는 당초 남은 리팩토링을 계속 실행하라는 지시이며 사용자가 AI2파일을 이름으로 지정한 것은 아니다. 메인이9.5절에서 공유한 AI live provider 기존2파일 안을 다음 실행 묶음으로 선정하고 프로그래머의 구체 제안을 Plan2.5에 반영했다. 동일 Task를 이어서 사용하고 task 작성 여부나 재승인을 다시 묻지 않는다. 코드 전에 정확한 인자·null 계약·보존 동작을 기록했고 메인 직접 검토 후 전문 프로그래머에게 구현 신호를 전달했다. 소스 정리·메인 독립 정적 대조를 마쳤고 결과는10.4절에 기록한다. 컴파일·게임 실행은 미실행이다.

문서 담당이 AIOpponentController1~85/126~185/917~941행, Bootstrap.Setup820~847행을 직접 읽었다. 직접 정적 종족 조회는 GameRaceForAi930~932행이며,429/443/483/627행의 네 사용점이 현재 호출 때마다 이 메서드를 통해 비용·건설/업그레이드·채굴소의 종족을 얻는다. 초기화 때 종족값을 저장하는 snapshot으로 바꾸지 않는다. 생성자 호출은 메인 전체 검색에서 Setup833행1곳임을 확인했다고 전달했고 문서 담당도 그 호출을 직접 읽었다.

### 10.2 규칙·예외·남는 의존성

AI 원본의 H2 「1. 개요」 규칙1/2를 직접 읽었다: 싱글플레이 전용이며 Red만 제어한다. H2 「10. 아키텍처 및 구현 규칙」 규칙28은 Controller를 Application에 두고 UseCase를 주입해 상태를 바꾸도록 한다. 규칙29는 Config SO를 Infrastructure에 두는 위치 규칙이고,30은 Bootstrap.Setup InitializeAI와 싱글플레이 초기화 순서를 정한다. 이 조항들이 모든 Infrastructure 구체 참조를 새로 허용하거나 이번에 난이도 타입을 이동하라고 정하는 것은 아니다.

TDD의 의존성 역전·공용 메모리의 계층 제약과, 기존 TowerCombatUseCase59/81/87/286~287행의 Func<TeamId,RaceId> 주입 관례를 참고한다. AI는 Func<RaceId>를 주입받고 Bootstrap에서 GameRaceContext.RedRace를 읽는 함수를 제공한다. 종족값·초기화 순서·싱글/멀티 가드·UseCase의 상태 변경 책임은 유지한다.

이 묶음 이후에도 DifficultyParams는 Infrastructure 정의의 값 구조체이며 using Hexiege.Infrastructure가 필요하다.9개 난이도 값·SO/시나리오 자산·AIConfig 직렬화 계약은 이동하지 않는다. 기존2026-06-10 QA의 의도적 예외 기록은 보존한다. AI의 직접 GameRaceContext 사용 한 곳을 제거하는 범위이며 모든 Application 역의존이나 AI/Auth 후속을 끝낸다는 뜻이 아니다.

### 10.3 새 생성자 계약과 위험

생성자 difficulty 뒤·optional upgrade 앞에 필수 Func<RaceId> aiRaceProvider를 추가한다. `_aiRaceProvider = aiRaceProvider ?? throw new ArgumentNullException(nameof(aiRaceProvider));`로 null 주입을 거부하고 별도 종족 폴백은 두지 않는다. 이는 새 공개 인자의 명시적 null 거부 계약이며, 유일한 정상 호출자는 항상 비null 람다를 넘겨 기존 정상 동작에 영향이 없도록 한다. provider 예외를 새로 삼키지 않는다. 기존 메서드의 매 호출 읽기와 네 호출 시점을 유지한다.

신규 파일·meta·난이도/시나리오·시간·이벤트·자원·생산·피해·네트워크·Auth 변경은 없다. 첫 두 묶음의 컴파일·실기·사용자 수용은 미확인 상태를 유지한다. 실제 소스 대조 및 실행 한계는 구현 뒤 별도 결과로 기록한다.

### 10.4 셋째 묶음 구현·메인 독립 정적 대조(2026-10-23)

| 수정 파일 | 현재 소스 위치와 결과 |
|---|---|
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | 50행 readonly Func 필드,165행 필수 aiRaceProvider 인자(166행 upgrade는 optional 유지),173행 ArgumentNullException null 거부 대입,934행 provider 반환. 헤더·메서드 설명 현행화 |
| Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs | 843행 비null `() => GameRaceContext.RedRace,` 인자 추가; 기존 upgrade 전달 유지 |

문서 담당도 위 필드·생성자·null guard·return·live 람다와 헤더를 직접 읽었다. 기존4사용점은 현재434/448/488/632행이며 코드/호출 시점을 유지했다. 생성자에서 provider를 호출하지 않고 이후4사용 때마다 GameRaceForAi를 통해 delegate를 호출한다. 정확한 static getter 람다가 현재 RedRace를 읽으므로 초기값 snapshot으로 바꾸지 않았다. DifficultyParams는 현재57/161행, Infrastructure using은31행에 남으며 AI의 모든 외부의존을 해소한 것은 아니다.

메인은 코드 전 전체329C# snapshot의 원본2개 SHA를 확인했다. 수정 후 허용한 field/parameter/assignment/return/lambda만 역치환하고 주석·공백을 제외하되 문자열 리터럴을 보존한 토큰을 대조해 두 파일이 원문과 완전히 일치함을 확인했다. 생성 시 provider 호출0, 기존4사용점, 비null live 람다와 upgrade 인자 유지도 직접 대조했다. 이번 snapshot329개 중 지정2개 변경·나머지327개 동일이며 신규 C#0이다. 이전 묶음 누적과 이 회차 snapshot 숫자를 혼합하지 않는다. `git diff --check`0이다. 위 SHA·토큰·수치는 메인 전달 근거다.

새 null 인자는 거부하고 정상 유일 호출자는 항상 비null 람다를 넘긴다. 유일 생성 지점은 Setup833행이고 주입 인자는843행이다. 이번 해소는 AI 실행 코드의 GameRaceContext 직접 조회1곳으로 한정한다. Application raw 검색에는 TowerCombatUseCase30/57행과 SkillActivationUseCase49행의 설명 주석이 남으므로 문자열 잔존0으로 표현하지 않는다. 기존 난이도9값·시나리오/자산·시간·이벤트·자원·생산·피해·네트워크·Auth는 변경하지 않았다. 기존 예외 QA 이력은 그대로 보존했다. 컴파일·게임 실행은 미실행이며 앞선 두 묶음의 실기 수용도 미확인이다. 전체 감사·DifficultyParams 분리·Auth7파일 제안 구현·상시문서/메모리 갱신 완료로 확대하지 않는다.

## 11. 넷째 묶음 코드 전 조사: 인증 서비스 계약과 공용 예외 정의 이관

### 11.1 착수 근거·범위(2026-10-23)

사용자 「계속 진행해」는 남은 리팩토링의 계속 실행 지시다. 사용자가 인증7파일을 직접 이름 지정한 것은 아니며 메인이 앞서 공유한 인증 계약안을 다음 실행 묶음으로 선정했다. 전문 프로그래머의 구체 제안을 Plan2.6에 기록하고 메인 검토 뒤 구현 신호를 전달한다. 동일 Task를 이어서 사용하고 task 여부/같은 승인 질문을 반복하지 않는다. 코드 전 계획을 메인이 전체 읽어 검토한 후 프로그래머에게 구현 신호를 전달했다. 해당7파일의 소스 정리·메인 독립 정적 대조를 마쳤다. 컴파일·Unity/실기는 미실행이며 인증 계층 전체 완료로 표시하지 않는다.

변경 제안은 기존 FirebaseAuthService/LoginUseCase/AccountLinkUseCase C#3파일, 신규 Application/Interfaces/IAuthenticationService.cs와 meta, 신규 Application/AuthException.cs와 meta로 총7파일이다. 새 Auth 폴더는 만들지 않는다. 서비스 초기화(IsInitialized/InitializeAsync)는 구성 루트 소유로 계약에 노출하지 않고, UseCase가 소비하는6읽기속성+12Task멤버 고유18개만 노출한다. 메서드의 인자·반환·optional 기본값은 현재 FirebaseAuthService와 맞춘다.

### 11.2 직접 확인한 규칙·소비자·표시 영향

문서 담당이 AuthSystemRules의 H2 「오류 처리」 규칙1/2/3, 「UGS 연결」 규칙1~4, 「계정 연동」 규칙1~6, 「로그아웃」 규칙1~3과 익명/재실행 보존 문구를 직접 읽었다. 네트워크/서비스 오류 안내, Firebase 성공 후 UGS 실패를 로그인 실패로 바꾸지 않는 계약, OIDC/익명 로그인 분기·세션 보존, 익명 연동 충돌 시 기존 계정 유지, 명시 로그아웃과 재실행 차이를 보존한다. TDD 계층 의존 역전에 맞춰 서비스 소비 API와 Firebase SDK 예외 변환 결과를 Application에 둔다.

`/tmp/hexiege-auth-contract-consumers.json`을 직접 읽었다. 예외/사유 사용은 기존 Application2/Infrastructure1/Login View6의 고유9파일이다: AccountLinkUseCase, LoginUseCase, FirebaseAuthService, AnonymousWarningPopup, EmailLoginView, EmailVerifyView, LoginSelectView, PasswordResetView, SignUpView. 이는 소비 범위이며9파일 전부 수정한다는 뜻이 아니다. Views는 기존 Application import로 이름을 찾을 수 있어 본문 수정 불필요가 예상되며 실제 소스 대조/컴파일과 구분한다.

기존 FirebaseAuthService.cs38행에 Application import가 있고47행 class 선언은 인터페이스 구현을 추가할 위치다. enum618행과 exception639행 정의를 직접 읽었고 순서13개와 Reason/constructor/message/inner/base 연결을 그대로 이관하는 계획이다. 기존 생성자 호출은 LoginBootstrapper207/208행, ProfileView177/178행에 같은 concrete 객체를 전달한다. 문서 담당도 해당 호출을 직접 읽었다. 기존 완전수식 예외/리플렉션/직렬화 검색은 메인·프로그래머가 미발견으로 전달했으며 존재하지 않는다는 증명은 아니다.

GameLog.cs433행은 exception.GetType().Name을 사용해 AuthException 단축명이 유지된다. ConsoleSink89~91행은 Debug.LogException에 예외 객체를 직접 전달한다. 따라서 namespace 이동으로 FullName·ToString·스택의 타입 표시가 달라질 수 있으며 모든 로그 출력이 완전히 같다고 주장하지 않는다. 문서 담당이 두 실제 로그 경로를 직접 읽었다. 기존 로그 문자열과 오류 변환/비동기 흐름은 변경하지 않는다.

### 11.3 정의 이관과 남는 후속

기존 enum과 exception body를 신규 Application/AuthException.cs에 먼저 보존하고 비교한 뒤 원위치의 중복 정의를 옮긴다. 동작을 폐기하는 삭제가 아니라 계층 소유권을 옮기는 정의 이관이다. 이전 namespace의 shim·주석 비활성 old class·dead block을 새로 남기지 않는다. 실기 대기 Legacy 최종 삭제 규칙과 혼동하지 않고 목적지 선보존·본문 비교 근거를 남긴다.

LoginUseCase의 UGS 직접 import/호출은 여전히 남는다. 헤더에서 직접 외부 의존이 아키텍처상 전부 허용된다고 단정하던 설명은 현재 직접 참조와 별도 후속이라는 사실로 정리한다. 이번 묶음은 인증 서비스/공용 오류 타입의 구체 의존을 줄이는 범위이며 모든 인증 계층 의존·UGS 리팩토링·계정 정책 변경 완료가 아니다. 첫 세 묶음의 컴파일·실기·사용자 수용 미확인은 유지한다.

### 11.4 메인 검토와 동명 타입 확인 한계(2026-10-23)

메인은 Plan2.6을 전체 읽어 검토한 뒤 프로그래머에게 구현 신호를 전달했다. LoginUseCase가 Hexiege.Application namespace에 있으므로 새 같은 namespace의 IAuthenticationService가 using으로 들여온 외부 동명 타입보다 이름 해석에 우선한다는 언어 규칙을 프로그래머가 검토했다. 다만 현재 workspace에는 해당 UGS SDK source/DLL이 없어 실제 SDK에 동명 interface 선언이 있는지 직접 확인하지 못했다. 존재 여부를 단정하거나 실제 컴파일 증거로 쓰지 않는다. FirebaseAuthService는 기존 Application using을 활용하고 UGS using은 없다. 기존 UGS AuthenticationService(I 접두사 없음) 실행 참조는 바꾸지 않으며 별도 alias/rename을 추가하지 않는다.

메인은 인증 원본의 자동로그인/익명/계정연동/로그아웃/오류/UGS 절과 Bootstrap/Profile/InGame 호출자를 직접 읽고 기존 완전수식/typeof/직렬화 자산 참조 검색 미발견도 재확인했다고 전달했다. 이는 검색·언어 규칙에 따른 검토이며 실제 compile/실기나 모든 동적 사용 부재를 증명하지 않는다. 최종 소스 결과와 확인 한계는11.5절에 별도로 기록한다.

### 11.5 넷째 묶음 구현·메인 독립 정적 대조(2026-10-23)

아래는 현재 소스 위치다.11.1~11.4의 원래 enum618/exception639행과 과거 검색 수치는 코드 전 근거로 보존한다.

| 실제 변경 파일 | 현재 소스 결과 |
|---|---|
| Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs | 47행 IAuthenticationService 구현 선언. 기존 enum/예외 정의는 Application 파일로 이관 |
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | 63행 field·100행 ctor를 IAuthenticationService로 변경하고 Infrastructure using 제거.20행 헤더는 Task 기반 흐름·UGS 직접 후속 의존을 정확히 설명 |
| Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs | 49행 field·58행 ctor를 IAuthenticationService로 변경하고 Infrastructure using 제거; 관련 헤더 현행화 |
| Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs 및 meta | 신규15행 interface,6읽기속성+12Task 고유18멤버. 초기화 API 미노출 |
| Assets/_Project/Scripts/Application/AuthException.cs 및 meta | 신규17행 enum·38행 class. 기존 enum13순서·Reason/constructor/message/inner/Exception body 이관 |

문서 담당도 현재 interface 전체18멤버·예외 정의·2개 meta·UseCase field/ctor·Firebase class 선언과 Login 헤더를 직접 읽었다. 신규 interface meta GUID는 `b52ffc5a58464dce9595772ea310c144`, 예외 meta GUID는 `71de453ebf0f49df99c37637e340ae58`이다. 메인은 Assets 전체 meta 검색에서 각 GUID가 신규 자신1건씩임을 확인했다.

메인은 수정 전 SHA를 대조하고 기존 enum/class 범위를 추출해 신규 Application namespace wrapper를 제외한 토큰과 비교했다. enum13순서·Reason·constructor·message/inner·상속 body가 완전히 일치했다. Firebase class 전체는 이전 정의를 제외하고 interface 구현 선언 추가 외 토큰이 동일했고, UseCase2파일은 field/ctor 타입과 Infrastructure using 제거 외 실행 토큰·문자열이 동일했다. interface18멤버의 반환·인자·optional 기본값이 기존 public signature와 모두 일치함도 확인했다. 소비 View6개와 Bootstrap/Profile 호출자 소스 해시 무변경을 확인했으나 이는 새 타입 호환의 소스 검토이며 실제 컴파일이 아니다.

이번 코드 전329개 C# snapshot 중 기존3개 변경·326개 동일에 신규2개를 더해331개다. 프로젝트 C#313/Application88개이며 `git diff --check`0이다. 이전 회차 숫자를 이 회차 baseline/누적과 섞지 않는다. `/tmp/hexiege-auth-contract-comparison.json`의 일부 SHA는 Login 헤더1주석 최종 변경 전 값이므로 현재 파일 해시로 승격하지 않는다. 위 최종 비교는 메인의 현재 소스 독립 검토 전달 근거로 기록한다.

구현 후 메인이 같은 지정 namespace 검색을 재실행했고 문서 담당도 `/tmp/hexiege-plan26-post-scan.json`을 직접 읽었다. Domain51파일0hit, Application88파일은2파일3행(AIOpponentController.cs31행 Infrastructure import, LoginUseCase.cs25/26행 UGS import)이다. 앞선3파일5행 검색은 그 당시 snapshot으로 보존한다. UGS 직접 의존과 AI DifficultyParams는 남으며 전체 인증 계층이나 의존성 감사 완료가 아니다.

sign-in/sign-out/token·세션 보존·UGS async 흐름·오류 변환·기존 로그 문자열은 원문 소스 대조에서 유지됐다. 다만 예외 namespace가 Application으로 이동해 FullName/ToString/스택 진단 표시는 바뀐다. 기존 GameLog 단축 AuthException명 유지와 ConsoleSink의 namespace 표시 가능성을 구분한다. 컴파일·Unity 실행·실기는 미실행이고 기존 사용자 수용을 신규 실기 완료로 확대하지 않는다. Legacy 최종 삭제·TC/QA·상시문서/메모리 갱신은 수행하지 않았다.

## 12. 다섯째 묶음 코드 전 조사: AI 난이도 설정의 Application 값 경계

### 12.1 착수·규칙·직렬화 보호(2026-10-23)

사용자 「작업 이어서진행하자」의 남은 리팩토링 계속 실행 지시 아래 메인이 AI 난이도 값 분리 후 UGS adapter 순서를 선정했다. 구체 AI안은 전문 프로그래머 제안과 메인 검토를 거쳐 Plan2.7에 기록한다. 현재 브랜치는 `work`다. 문서의 「메인」은 조율 담당 에이전트이고 main 브랜치에서 작업한다는 뜻이 아니다. 재승인/task 여부 질문을 반복하지 않는다. 이 절의 앞선 내용은 코드 전 근거이며 이후2.7 소스·문서 동기화를 마쳤다. UGS 구체안과 구현 상태는13절로 구분한다.

AI 규칙의 H2 「2. 난이도 파라미터 (AIConfig ScriptableObject)」 규칙4/5, H2 「10. 아키텍처 및 구현 규칙」 규칙28/29/30/33/34를 직접 읽었다. AIConfig의 easy/normal/hard 직렬화 구조체와9개 float, 선택 난이도, Controller UseCase 책임, Infrastructure SO 위치, Bootstrap 초기화/싱글 가드, Red 수입1회 적용을 보존한다. 규칙4/33의 현재 직접 DifficultyParams 주입 설명은 새 값 스냅샷 경계를 정확히 표현해야 하므로 **원래 문서·구현 대조 목표에 필요한 구조 동기화로 GameSystemRules_AI.md 이 두 조항만 범위를 확장한다.** 규칙 번호·규칙5표의 수치와 행동 요구사항은 변경하지 않는다. 문서 본문은 코드 구현 후 메인 확인 지시를 받아 수정한다.

Infrastructure/Config/AIConfig.cs의 [Serializable] top-level DifficultyParams와9public float를 그대로 두고 Application/Models/AIDifficultyParameters.cs에 새 불변 순수 값 구조체를 만든다. 기존 Application Models의 namespace 관례는 Hexiege.Application이다.9개 readonly float의 이름·순서는 원래와 같고 생성자는9필수 float를 그대로 대입하며 검증·clamp·기본값·Unity 속성을 추가하지 않는다. Bootstrap은 선택한 기존 aiParams9값을 named argument로1대1 복사한다.

### 12.2 실제 소비와 별도 미연결 항목

문서 담당이 Controller의 _params 소비7행을 직접 검색했다: 구현 전317 retryInterval,607 mineCheckInterval,693 decisionInterval,715 unitCountThreshold,733 productionActivationCooldown,750 goldExcessThreshold,765 buildOrderAccelCooldown. 이7읽기와 타이밍·분기·동작은 그대로 둔다. goldIncomeMultiplier는 Bootstrap.Setup830행의 ResourceUseCase 초기 설정에서 사용하며849행 로그도 기존 aiParams를 유지한다.

productionTimeMultiplier는 전체 Scripts 검색에서 AIConfig.cs48행 선언1곳 외 사용을 찾지 못했다. 문서 담당도 UnitProductionUseCase629/692행이 UnitProductionStats.GetProductionTime을 RequiredTime으로 넣는 부분을 직접 읽었다. AI 규칙5의 생산시간 배율 설명과 현재 소비의 차이는 AUD-017로 별도 미해결 관리한다. 이번에 배율을 연결·삭제하거나 규칙5 수치·효과 요구사항을 바꾸지 않는다.9값을 DTO로 복사하는 것은 값 전달 보존이지9효과 실행 증거가 아니다.

### 12.3 구현·문서 범위

기존 Controller의 field57/ctor161 타입과 Infrastructure using/헤더를 정리하고 Bootstrap833행 생성 직전에 새9값 snapshot을 만든다. 생성자에는 기존 aiParams 위치만 새 값을 전달하며 live 종족 provider/upgrade, income830/log849, 설정 선택·시나리오·자산·가드는 유지한다. 기존2C#+신규 DTO/meta4파일과 GameSystemRules_AI.md 구조 설명2조항이 범위다. 신규 meta는 기존 GUID와 중복 없는 값을 사용한다. 기존 AIConfig.cs·SO/직렬화 자산·규칙5표·다른 상시문서/메모리는 변경하지 않는다. 앞선2.3~2.6 묶음의 실제 컴파일·실기 수용 미확인과 전체 감사 미완은 유지한다.

## 13. 여섯째 묶음 코드 전 조사: 로그인 UGS adapter

### 13.1 순차 착수·근거(2026-10-23)

사용자 「작업 이어서진행하자」의 잔여 리팩토링 계속 실행 지시 아래 메인이 기공유 UGS 잔여를 AI2.7 이후 실행 묶음으로 선정했다. 전문 프로그래머의 구체 제안을 Plan2.8에 기록한다. AI2.7 소스 검토 후 메인이2.8 계획을 전체 읽어 검토하고 별도 구현 신호를 전달해 UGS 소스 정리·독립 정적 대조를 마쳤다. `work` 브랜치이고 「메인」은 조율 담당 에이전트다. 재승인/task 질문을 반복하지 않는다. 소스 정리 완료로 한정해 기록하며 실제 컴파일/실기 완료로 확대하지 않는다.

AuthSystemRules H2 「오류 처리」 규칙3, 「로그아웃」 규칙2, 「UGS 연결」 규칙1~4를 직접 읽었다. Firebase 성공 후 UGS 실패 시 로그인 성공·기능 제한 안내 유지, 실계정 OIDC/익명 분기·세션 보존, 명시 익명 로그아웃의 자격 정리와 실계정 보존을 유지한다. Login의 Firebase UID 선행 검사·IsAnonymous snapshot·try/catch·LastUgsBridgeSucceeded·로그 문자열·oidc-firebase·토큰 forceRefresh=false를 변경하지 않는다.

### 13.2 실제 경로와 기존 initializer 미사용 이유

문서 담당이 현재 LoginUseCase63/100행의 의존·생성자,382~431행 로그아웃,468~535행 UGS 브릿지와 LoginBootstrapper207행/ProfileView177행 생성자를 직접 읽었다. 새 IUgsAuthenticationService는 Firebase IAuthenticationService 뒤 필수 인자로 주입하고 기존 Firebase null guard를 먼저 유지한다. 두 caller는 같은 Firebase 객체와 새 stateless UgsAuthenticationService 객체를 넘긴다. adapter 객체 생성 자체에는 SDK 접근·세션/초기화 효과가 없다.

기존 Infrastructure/Network/UnityServicesInitializer.InitializeAsync는 자체 try/catch·로그·콜백 및 기존 세션 보존/없는 세션 익명 폴백이 있어 Login의 원래 UnityServices.InitializeAsync 직접 호출과 효과가 다르다. 이를 재사용해 중복을 줄이는 방식은 순서·실패/로그 정책을 바꿀 수 있어 채택하지 않는다. 문서 담당이 해당 구현을 직접 읽었다. 이번 adapter는 정책 없이 원래 SDK 접근을 감싸는 범위다.

### 13.3 상태 조회·Task·로그아웃 블록 보존

8멤버 계약은 IsInitialized/IsSignedIn/PlayerId getter, InitializeAsync, parameterless SignOut, SignOutCurrentSession(bool clearCredentials), SignInAnonymouslyAsync, SignInWithOpenIdConnectAsync(providerName,idToken)다. adapter는 필드·명시적 생성자·캐시·async wrapper·catch·fallback·log를 두지 않는다. IsInitialized는 UnityServices.State 비교이고 인증 getter는 매 호출 SDK Instance를 새로 조회한다.3Task 함수는 원래 SDK Task를 그대로 반환한다.

Bridge의 SDK 표현식7개는 같은 순서로 interface 접근으로 바꾼다. getter 평가 횟수와 호출 시점을 캐시/세션 wrapper로 바꾸지 않는다. parameterless SignOut은 원래 SDK 기본 자격 보존 호출을 그대로 수행한다.

명시 로그아웃의 SignOutCurrentSession은 원래 SDK Instance를1회 capture하고, clearCredentials이면서 Instance null이면 동일 InvalidOperationException 메시지를 던지고, Instance가 있고(clearCredentials 또는 IsSignedIn)이면 SignOut(clearCredentials: clearCredentials)을 호출한다. 구현 전397~405행의 SDK 처리 블록과 관련 legacy comment만 이관하며 UseCase의 익명 상태 snapshot·예외 처리·익명 실패 시 Firebase 로그아웃 금지/실계정 실패 경고 후 계속 처리 정책은 유지한다. 새 clear-credentials 정책·캐시·SDK 타입 노출을 추가하지 않는다.

기존 로그 문자열은 유지하지만 SDK 예외 스택에 adapter frame과 줄번호 차이가 추가될 수 있다. 동일 문자열/동작 정책을 진단 스택 문자열 완전 동일로 확대하지 않는다. SDK 실제 선언 일부가 현재 workspace에 없어 실제 컴파일 PASS로 보고하지 않는다. 정확한7코드파일 계획과 확인 범위는 Plan2.8에 기록하며 원래 문서·구현 동기화 목표에 필요한 AuthSystemRules의 구조 안내3행만 추가 범위로 정했다. AI규칙4/33과 이번 Auth구조3행 외 다른 상시문서·행동 규범은 변경하지 않는다.

### 12.4 다섯째 묶음 구현·정적 대조·필수 규칙 설명 동기화(2026-10-23)

| 실제 변경 파일 | 현재 위치·결과 |
|---|---|
| Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs 및 meta | 13행 public readonly struct,15~40행9readonly float,43행 ctor9필수 인자·각 this.field=field 단순 대입. GUID c2846d09dbd9404c93926158b072c8fe |
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | 56행 field/160행 ctor가 Application DTO 타입. Infrastructure using 제거·헤더 현행화. 기존 _params7읽기 유지 |
| Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs | 833~842행9named args 동명1대1 복사,849행 Controller 인자 교체. income830/log859는 기존 aiParams, 종족 live provider/upgrade도 그대로 |
| Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md | 규칙4 제목·주입 설명 및 규칙33 주입 설명을 선택 설정→9float 불변 snapshot→Controller 경계로 동기화. 기존 easy/normal/hard 설명·ID·규칙5수치·다른 행동 요구사항 유지 |

문서 담당이 신규 DTO 전체·meta, Controller field/ctor·헤더와 Bootstrap9값 복사를 직접 읽고 허용된 규칙4/33의 정확한 문장만 수정했다. 메인은 코드 전331C# snapshot 중 지정2개 변경·329개 동일+신규 DTO1개=332개를 확인했다. 허용 import/type/9값복사/인자 교체 외 문자열 보존 토큰은 두파일 모두 원본과 동일했고 DTO 전체 토큰도9readonly fields·9params·9assignments 이외 없음을 확인했다. 기존 AIConfig.cs/meta·AIConfig.asset/meta·기존 AI/Setup meta의 보호6해시 동일, 새 GUID의 Assets meta 유일 및 `git diff --check`0도 메인 전달 근거다.

메인 검증기 초회 예상 template에서 namespace 닫는 brace를 빠뜨려 assertion이 실패했으나 예상 template을 바로잡은 뒤 실제 비교는 모두 통과했다. 이는 검증 도구의 예상 텍스트 오류이며 production 소스 결함이나 실행 실패로 해석하지 않는다. 실제 컴파일·Unity/실기는 미실행이다.

9값 복사는9행동 소비 증거가 아니다. Controller7읽기·Bootstrap Red수입1회 소비를 유지했고 생산시간 배율 미연결 AUD-017은 미해결이다. AIConfig 직렬화·자산·규칙5표·다른 규칙/시나리오/타이밍/이벤트·동작을 바꾸지 않았다. 허용된 AI 규칙4/33 외 상시문서·메모리·TC/QA·기존 타 Task를 변경하지 않았으며 전체 의미 감사/실기 수용 완료로 확대하지 않는다.

### 13.4 계획 전체 검토와 SDK 표현식 비교 한계(2026-10-23)

메인은 AI 소스 검증 후 Plan2.8을 전체 읽어 검토하고 UGS 프로그래머에게 별도 구현 신호를 전달했다. 이후 UGS 소스 정리·독립 정적 대조를 마쳤고 최종 근거는13.6절에 구분한다. 코드 전 원래 Bridge의 AuthenticationService.Instance 실행 접근은5개지만 일반 문자열을 통째로 가리는 검색은 보간 문자열 안의 PlayerId 접근1개를 가려4개로 보일 수 있다. 따라서 알려진 실행 표현식의 정확한 치환을 역으로 복원해 비교하고 보간 표현식까지 따로 확인한다. SDK 치환으로 보간 표현식이 바뀌므로 원문 문자열 전체 무변경이라고 주장하지 않는다. 로그 메시지 의미/비보간 literal과 실행 getter 평가·순서의 보존을 구분한다.

### 13.5 필수 인증 구조 안내 동기화(2026-10-23)

UGS 계약/adapter 구현 파일을 문서 담당이 직접 읽었고 메인의 최종 독립 정적 검토도 완료됐다. 소스 정리/대조 완료와 실제 컴파일·실기 미실행을 구분한다. 원래 사용자 목표의 문서·구현 동기화에 따라 메인이 추가한 정확한 범위를 Plan2.8에 먼저 기록한 뒤 AuthSystemRules.md3행만 수정했다.

「레이어별 구성」 Infrastructure 표행에 FirebaseAuthService/UgsAuthenticationService/UnityServicesInitializer, Application 표행에 LoginUseCase/AccountLinkUseCase/IAuthenticationService/IUgsAuthenticationService/AuthErrorReason/AuthException을 표시했다. 해당2행의 신규/수정 같은 과거 receipt 표시는 제거했다. 「Firebase ↔ UGS 연결 구조」 구현 위치 blockquote1행은 LoginUseCase 조율→IUgsAuthenticationService/Infrastructure UgsAuthenticationService의 SDK 실행, IAuthenticationService/Infrastructure FirebaseAuthService.GetIdTokenAsync(false)의 token 발급을 정확히 설명한다.

기존 인가·익명·세션 보존·UI·UGS 입출력 규칙·코드 예시와 다른 원문은 변경하지 않았다. 허용 상시문서는 AI2.7의 규칙4/33과 Auth2.8의 구조3행으로 한정하며 Status/Roadmap/다른 상시문서·메모리는 수정하지 않는다. 코드 검토 결과와 실제 실행 한계는 메인 최종 근거를 받아13.6절에 별도 기록한다.

### 13.6 여섯째 묶음 구현·독립 정적 대조 결과(2026-10-23)

| 실제 변경 파일 | 현재 소스 결과 |
|---|---|
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | 63행 UGS 계약 field,102행 필수2인자 ctor·105행 UGS null guard.397행 명시 세션 종료 위임.471/474/479/481/488/498/505행 Bridge 표현식을 계약 호출로 교체·SDK imports2 제거/헤더 현행화 |
| Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs | 207행 동일 Firebase 객체와 `new UgsAuthenticationService()` 주입 |
| Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs | 177행 동일 Firebase 객체와 새 stateless adapter 주입 |
| Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs 및 meta | 신규15행 interface8멤버. GUID `dc6b3c26d2e548f78d8d70821d108e5b` |
| Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs 및 meta | 신규19행 class,22~34행 live SDK 상태·원래 Task 반환/기본 SignOut,41행 명시 logout 블록 이관. GUID `758217fe5ff54802804ed9fc6998df8a` |

문서 담당도 계약/adapter 전체·현재 주입 위치와2개 meta를 직접 읽었다. 메인은 코드 전332C# snapshot과 원본3SHA를 대조했다. Login의 허용 field/ctor/guard/import2/SDK표현식7·명시 logout SDK 블록만 역복원하자 원본 토큰과 같았으며 보간 PlayerId 표현식도 원래 형태로 복원해 확인했다. 일반 문자열 마스킹만으로 실행 표현식이 누락될 수 있어 원문 문자열 전체 무변경 주장은 하지 않는다. 로그 표시식 형태·해시 조회 시점·실행 정책 보존을 구분한다.

SDK logout 블록은 원문 들여쓰기 제거와 leavingAnonymous→clearCredentials의3개 이름 대응 외 raw body가 동일했고 기존 legacy comment를 보존했다. 신규 adapter 전체 active code는 예상8API·직접Task3·상태 live getter·명시 logout 블록과 토큰이 완전히 같았다. 필드/cache/명시적 ctor/catch/log/fallback은 추가하지 않았다. 신규 interface8멤버 전체도 예상 토큰과 같았다. caller2는 새 UGS 인자를 역복원한 뒤 원문 텍스트와 완전히 같았다.

이번332개 snapshot 중 기존3개 변경·329개 동일+신규2개=334개이며 프로젝트 C#316개다. 새 meta GUID2개는 각각 Assets meta 자신1건만이고 기존 meta는 유지했다. `git diff --check`0이며 앞선 AI 보호6해시도 여전히 동일하다. 위 독립 비교·해시·수치는 메인 전달 근거다. 이전 snapshot 숫자/문서 링크 검사 결과를 현재 결과로 재사용하지 않는다.

메인이 같은 지정 namespace 검색을 재실행했고 문서 담당도 `/tmp/hexiege-plan28-post-scan.json`을 읽었다. Domain51파일0hit/Application90파일0hit다. 이 결과는 지정 토큰·마스킹 검색 범위의 완료이며 전체 의미·보간식 해석·동적 참조 부재나 실제 컴파일 보증이 아니다. 기존 각 회차의 DifficultyParams/UGS 잔여 기록은 당시 상태로 보존하되 현행 지정 범위의 해소는 AUD-016/018로 구분한다.

Auth 규칙은 메인도 before 원문과 독립 비교해 레이어 표2행·구현위치1행 외 전부 동일함을 확인했다. AI 규칙도 메인이 앞서 정확3행 변경 외 원문 동일을 확인했다. 현재 문서 변경은 Research/Plan과 AI/Auth 규칙2파일뿐이다. 모든 단계 실제 컴파일·Unity/실기는 미실행이며 SDK adapter frame/줄번호 등 예외 진단 표시는 달라질 수 있다. 생산시간 배율 AUD-017은 미해결이고 전체 규칙·기능·죽은 코드 감사·사용자 수용 완료로 확대하지 않는다. 다른 상시문서/메모리·TC/QA·Legacy 최종 삭제는 수행하지 않았다.

## 14. 사용자 승인 AI 설명 정정과 구현상태 기록 확인(2026-10-23)

사용자 「그럼 그렇게 수정하고 코드 구현상태는 어떻게 기록되어있는지 확인해서 알려줘」에 따라 .claude/agent-memory/game-design-lead/ai-scenarios.md의 난이도 설계 설명만 수정했다. 시나리오 대기 시간·골드 수입 배율·유닛 생산 시간 배율을 포함하고, 쉬움 대기×1.5/어려움×0.7은 GameSystemRules_AI.md107행과 같은 조정 예정 예비 기준으로 명시했다. 이관 원문이라는 경위는 유지하되 머리말의 현재 무변경 주장은 승인 정정 범위를 밝히도록 바꾸고 이전 난이도 문장을 짧은 날짜 정정에 보존했다. 나머지 내용은 유지한다.

이번 코드 구현상태 출처는 문서 담당도 직접 읽었다.

| 출처 | 기록된 상태와 해석 |
|---|---|
| PROJECT_STATUS.md25행 AI 상태 | 난이도·빌드오더/반응·생산/배치·핵심 흐름의 포괄적 구현/관측 요약. 생산시간 배율 효과의 개별 구현·실기 PASS 기록은 아님 |
| History/ImplementationVerificationHistory.md3669행 | AIConfig/DifficultyParams 설정 필드 생성 완료 기록에 productionTimeMultiplier 포함. 설정 구조 존재와 효과 적용을 구분해야 함 |
| 같은 History3673행 | ResourceUseCase.SetIncomeMultiplier의 Red 수입 배율 실제 적용을 명시. 생산시간 배율 적용 기록은 아님 |
| 이 Task의 AUD-017, Research12.2/12.4, Plan2.7/7.10 | 생산시간 배율 미연결을 미해결로 관리하며9값 DTO 전달을9행동 소비로 확대하지 않음 |

현재 Scripts의 정확 식별자 검색은 AIConfig.cs48행 선언, Bootstrap.Setup835행 DTO 전달, AIDifficultyParameters.cs19/45/55행의 선언·인자·대입뿐이다. AIConfig.asset18/28/38행에는1.3/1/0.7이 설정돼 있다. UnitProductionUseCase629/692행의 RequiredTime은 UnitProductionStats.GetProductionTime을 쓰며, 해당 getter80~81행은 기본 ProductionTime/폴백5초를 반환하고 생산 배율을 곱하지 않는다. 문서 담당이 해당 소스·에셋 위치를 직접 확인했다. 따라서 **설정 필드·값 전달은 구현돼 있으나 생산시간 배율의 실제 효과 적용은 확인되지 않았고 AUD-017은 여전히 미해결**이다.

이번 변경은 ai-scenarios 설명과 현재 Research/Plan 기록3파일뿐이다. 코드·에셋·규칙5표·옛 Task·History·Status·Roadmap·다른 메모리·기준선은 수정하지 않았다. 문서 표현 충돌 해소를 실제 코드 적용이나 새 컴파일/실기 PASS로 기록하지 않는다.

후속 사용자 결정(2026-10-23): 「그럼 나중에 AI 관련 작업할떄 구현하도록하자」에 따라 AUD-017의 생산시간 배율 효과 연결은 후속 AI 작업에서 구현하고 이번 리팩토링에서는 보류한다. 규칙5 설계·수치는 유지하며 코드 효과 미연결·구현 미완 상태를 닫지 않는다. 이 결정 기록은 현재 Research/Plan2파일만 갱신하며 다른 코드·메모리·문서·기준선은 변경하지 않는다.

## 15. 현행 문서 충돌 조사와 다음 범위 선정(2026-10-23)

사용자 「이외에도 문서간의 충돌있으면 알려주고 이상한점있으면 알려줘 다음 작업들 이어서하자」에 따라 현행 설정·TDD·agent 안내를 읽었다. ProjectVersion.txt1행은6000.3.5f2이며 TDD41/1120행·document-manager agent28행·공용 메모리284행의6000.0.x와 다르다. 역사Task/History를 제외한 관리 문서의 버전 검색에서 이4현행 위치를 확인했다. 루트 프로젝트 README는 파일 목록에서 발견되지 않았고 외부 SDK/패키지 안내는 프로젝트 규범과 구분한다. 메인이 선정한3문서 정확 정정 범위는 Plan2.9다.

TDD104행 Diagnostics 폴더는 실제 root Scripts에 없고 Debug/UIManagerTestButtonHandler와 Bootstrap/Debug의 에디터 맵 준비·투영 실패 플래그가 존재한다. Infrastructure/Debug 등 다른 디버그 책임도 있으므로 root Debug가 모든 도구라는 설명은 피한다. Bootstrap GameBootstrapper partial4개는 실제 일치하고 NGO2.9.2는 manifest와 일치해 변경하지 않는다.

추가 검토한 TDD232행 인증 구현 경계와634~639행 AI 계층 안내는 최신 계약·snapshot 설명이 빠진 부분이지만 현재 원문이 잘못된 직접 SDK 호출이나 DifficultyParams 직접 주입을 단정하지는 않는다. 이를 신규 문서 충돌로 확정하지 않고 후속 명료화 제안으로 구분하며 이번 정정에는 포함하지 않는다.

AUD-005 측정 도구는 VFXSFXGuide의 현행 유지 설명과 VfxConnections.cs301~303/330~331/337행의 실제 측정/기준 호출을 직접 읽어 내부 의존을 확인했다. UIManagerTestButtonHandler는 Login.unity7537행의 GUID9119a3e2063be1f44974ffc13636281e가 실제 컴포넌트와 연결돼 있다. 임시·도구라는 이름만으로 삭제할 근거가 없으며 삭제하지 않는다.

UnitView46행의 오래된 interface 제외 주석과1892/1908행 중복 삼항 조회를 직접 읽었다. 메인·프로그래머의9caller 및 factory 단순 조회 대조를 Plan2.10의 code-before 계획에 기록했다. 다음 수정은 이 한 파일의 helper 추출·주석 정리이며 타겟/권위/Root/캐시 정책을 바꾸지 않는다. 코드 전 snapshot과 실제 수정 후 결과는 구분한다. AUD-017은 사용자 결정대로 후속 AI 작업에서 구현, 이번 리팩토링 보류를 유지한다.

### 15.1 현행 버전·폴더 안내 정정 결과(2026-10-23)

Plan2.9를 먼저 기록한 뒤 메인이 확정한3문서의 현행 사실만 정정했다. TDD41/현재1121행의 Unity 버전2곳과 document-manager agent28행/공용 메모리284행의1곳씩을 ProjectVersion.txt의6000.3.5f2로 맞췄다. LTS 등 릴리스 명칭을 추정해 추가하지 않았다. TDD99행에 Bootstrap/Debug의 에디터 맵 준비·투영 실패 플래그를 표시하고105행의 실제 없는 Diagnostics를 Debug/UIManager 테스트 핸들러 안내로 바꿨다. root Debug가 모든 진단 도구를 소유한다고 쓰지 않았다.

문서 담당은 `/tmp/hexiege-plan29-docs-before.json`의 수정 전3원문과 현재 파일을 diff로 비교해 TDD의 버전2행·폴더안내1행교체/1행추가, agent/공용 메모리의 버전각1행 외 변화 없음을 확인했다. 다른 과거 기록·인증/AI TDD 내용·NGO/C#·Bootstrap partial수·기준선은 변경하지 않았다. 이번 문서 사실 정정을 코드 기능/컴파일/실기 PASS로 보고하지 않는다. 코드2.10의 실제 수정 결과는 별도로 기록한다.

### 15.2 UnitView 중복 조회·주석 정리 결과(2026-10-23)

UnitView.cs46행 SetDependencies의 구체 타입/interface 제외 설명을 현재 Application 인터페이스·IUnitView 계약 주입에 맞췄다. 현재1892행 GetTargetObject(int,bool) helper는 타겟 종류에 맞는 factory를1회 조회한다. 기존 getter는1903행 GetTargetWorldPos/1917행 GetTargetTransform으로 이동했고 각각 helper를1회 부른다. Unity GameObject `obj != null`, 현재 position+forward 폴백, Transform/null 반환과 호출 시점·캐시 부재는 유지했다. 문서 담당도 변경 소스와9caller 위치를 직접 읽었다.

현재 WorldPos caller는8341행1곳, Transform caller는926/1343/1470/1951/1975/6490/7104/7965행8곳이다. 메인은 코드 전 snapshot과 비교해 신규 helper의 XML 설명/본문을 정확히 제거하고 호출2곳을 원래 삼항 조회로,46행 주석을 원문으로 역복원했을 때 **파일 원문 텍스트 전체가 정확히 동일**함을 확인했다. 토큰 비교보다 강한 이 한정 raw 비교 결과와 코드334개 중 지정 UnitView1개만 변경·333개 동일/파일 집합 동일, meta bytes 동일을 메인에게 전달받았다. GUID8b032e7de55b97a44aa94ce3b24f914b는 유지됐다.

메인도2.9의3문서 정정을 독립 원문 diff로 확인했고 현재 work 브랜치, `python3 Tools/check_docs.py` exit0·문제0 및 `git diff --check`0을 확인했다고 전달했다. 현행 Docs에서 Tasks/History/Logs를 제외한 범위와 document-manager 설정의6000.0.x 잔여0도 검색 확인했다. 이 검사는 해당 문서 사실·형식과 소스 보존 범위이며 실제 컴파일/native 게임/실기 실행은 미실행이다. 측정 도구·Login 참조 임시 handler는 삭제하지 않았고 AUD-017은 후속 AI 작업 구현 보류를 유지한다.

## 16. 사용자 전면 승인에 따른 자율 잔여 작업 관리(2026-10-23)

### 16.1 승인 범위·작업 책임

사용자 「다 진행해 매번 내가 진행하라고 하는것도 불편한데」를 당초 아키텍처 준수·기능 보존·가독성/생산성·죽은 코드 및 문서 충돌 정리의 잔여 작업을 계속 수행하라는 승인으로 적용한다. 무제한 기능 추가/밸런스·실행 정책 변경 승인이 아니다. task 여부·매 묶음 진행 승인 질문을 반복하지 않는다. 사용자의 후속 AI 구현 보류(AUD-017)와6VFX 최종 삭제 보류는 유지한다. 담당자는 제안·원본 근거를 메인 조율 에이전트에게 전달하고 메인 범위 확정/구현 신호 후 전문 코드 담당이 구현한다. 문서는 document_manager 한 담당이 취합·수정해 동시 쓰기 충돌을 피한다.

| 영역 | 조사/구현 담당 | 기록 자료와 현재 확인 범위 |
|---|---|---|
| Domain/Application | game_programmer_ugs | /tmp/hexiege-full-domain-app-audit.json 수신 대기. 이전 namespace 검색과 일부 계약 변경을 전체 의미 읽기로 승격하지 않음 |
| UnitView | game_programmer | 앞선 인터페이스/조회 helper 지정 범위만 검토 완료. 전체 파일 의미 감사는 별도 범위 |
| Infrastructure/Bootstrap/Core/Debug/Editor/Python | infra_audit | /tmp/hexiege-full-infra-audit.json 현재89파일 경로·행수·SHA inventory, read_ranges 비어 있으며 content review pending |
| Presentation(UnitView 제외) | presentation_audit | /tmp/hexiege-full-presentation-audit.json 현재98파일 inventory/lexical triage. 샘플 read_ranges 비어 있고 semantic reading pending |
| 현행 Docs·관리 .claude 문서 대조 | rules_audit | /tmp/hexiege-full-rules-audit.json 수신 대기. 확실한 사실 오류 제안은 메인 공유 후 document_manager 수정 |
| 취합·현재 문서/코드 동기화 | document_manager / 메인 | 현재 Task 장부와 원본 문서·코드 직접 대조. source/문서 주장과 담당 전달 근거를 구분 |

위 임시 자료는 도착 시점별 snapshot이고 경로·scope/read_ranges·이슈·한계를 이 Research로 옮겨 영구 기록한다. 파일 목록이 있다는 것과 내용을 읽었다는 것은 다르다. 아직 도착하지 않은 자료나 empty read_ranges를 전체 감사 완료로 표시하지 않는다. 기존 장부의 미조사는 실제 확인 범위와 담당 근거를 받은 행만 갱신한다.

### 16.2 정확한 종료 조건과 미완 경계

Plan2.11에 영역별 끝조건을 정했다. 시작 목록과 증분에 있는 모든 대상은 실제 내용 검토 범위 또는 명시 제외 이유를 갖고, 발견 항목은 한 ID/한 문제로 원본 위치·판정·수정/유지/보류와 근거를 연결해야 한다. lexical scan만 끝낸 파일을 의미 검토 완료로 세지 않는다. 임시 자료가 없어져도 조사 위치·범위·한계를 추적할 수 있어야 한다.

안전 근거가 확정된 기능 보존 정리는 구현하고 동작/기획 선택이 필요한 차이는 원문·이유·보류 조건을 기록한다. Git 변경·native 게임 실행·TC/QA·정책/밸런스 변경·보류 Legacy 최종 삭제를 자동 수행하지 않는다. 실제 실행을 할 수 없었던 범위는 NOT RUN이며 과거 PASS/사용자 수용을 신규 근거로 쓰지 않는다. 정적 조사 완료·확정 정리 완료·미완/보류·실행 미확인을 따로 보고한다. 전체 파일을 읽지 못한 상태에서 전체 의미 감사 완료로 닫지 않는다.

### 16.3 과거 Task 연결과 실제 열람 범위

기존7절의612개 과거 Task/10개 Log 시작 경로 목록을 유지한다. 관련 핵심 Task를 원본 규칙/현행 상태/사용 API에서 역으로 연결하고 다음을 기록한다: 경로·정확한 절/행 범위·직접 읽기/담당 전달·현재 계약·예외·삭제 조건에 준 영향.5절에 이미 기록한2026-05-19 리팩토링 Plan1~100 및551~579행,2026-05-31 회귀 Research1~155행,2026-10-01 감사 Research1~100행,2026-10-16 역할 정비 Research1~75행과2026-06-10 AI Testcase159~165/182행은 그 부분만 확인한 기록으로 유지한다. 전체 과거 원문 재열람/검증 완료로 승격하지 않는다. 새 관련 이력은 실제 읽은 범위만 추가한다. 옛 Task/History를 현재 상태에 맞춰 일괄 수정하지 않는다.

### 16.4 메모리 보호 기준선 사전 확인

공용 메모리의 기준선 관리 규칙과 check_docs의 collect_memory_files/diff_baseline_folders/update_baseline 구현을 읽었다. 읽기 전용 사전 측정은 game-design-lead338→339(+1), game-programmer8980→9007(+27), qa1432→1454(+22), 나머지 동일로 감소0/파일수 변화0·합+50이었다. 실행 범위를 메인에게 먼저 보고해 증가 전용 도구 실행 신호를 받았다. 도구의 collect_memory_files/diff_baseline_folders로 실행 직전 같은3폴더+50·감소0을 재확인한 뒤 `python3 Tools/check_docs.py --update-baseline`을 실행해 exit0으로 반영했다. 도구 측정 폴더와 갱신값이 정확히 일치함을 다시 확인했으며 --reason이나 직접 JSON 편집으로 우회하지 않았다. 과거 작업의 baseline advisory 수치는 당시 검사 기록으로 보존한다.

### 16.5 다음 확정 범위·자료 수신(2026-10-23)

메인은 Infra4파일 설명/Inspector문구(Plan2.12), Domain BuildingData/UnitData2파일 주석(2.13), StatsReference 하드캡1문장(2.14)을 전문 담당 제안과 직접 원문 대조로 선정했다. 구체안을 코드/문서 수정 전에 기록했고 각 코드 담당은 메인 구현 신호를 기다린다. Stats는 규칙/코드0.65와 맞추는 현재 문서 사실 정정이며 새 밸런스 결정이 아니다. 임시 측정 도구/강제실패·기존Legacy를 이름만으로 제거하지 않는다.

Domain/App 자료 /tmp/hexiege-full-domain-app-audit.json도 수신했다.141파일의 구조 검색·using/type/preprocessor/candidate comment 기록이 있으며 actualCompile은 NOT RUN이다. 샘플 readRanges는 비어 있어 전체 의미 정독 완료로 승격하지 않는다. Infra/Presentation의 inventory/lexical snapshot도 같은 한계를 유지한다. 핵심 과거AI Task의 실제 열람 범위에2026-06-10 Plan155~161행(이전3에셋 삭제완료 원문)을 추가하며 옛Task 원문은 수정하지 않는다.

기준선은 감소0/지정3폴더+50을 도구 기준으로 재측정하고 메인 실행 신호 후 --update-baseline exit0으로 증가만 반영했다. 파일수 변화·감소·--reason·직접JSON편집은 없다. 이 갱신은 메모리 손실 감시 기준 반영이며 게임기능/실행 검증이 아니다.

### 16.6 확정 코드 정리 묶음 결과(2026-10-24 KST)

Plan2.12/2.13/2.15/2.16의 변경안은 코드 전에 기록했고 메인 신호 뒤 각 전문 담당이 구현했다. 문서 담당은 실제 diff의 지정 설명·XML/using·OnDestroy 추가를 직접 읽었고 메인은 별도의 원문 대조를 완료했다. 과거16.5/Plan7.18의4Infra/2Domain 계획 기록은 당시 단계로 보존하며 최종 범위는7/4/6/1파일이다.

| 계획·문제ID | 실제 수정 파일(Assets/_Project/Scripts/ 기준) | 근거·검증 |
|---|---|---|
| 2.12·AUD-020 | Core/HexMetricsCoordinateMapper.cs; Infrastructure/Factories/UnitFactory.cs·BuildingFactory.cs; Infrastructure/Config/BuildingStatsConfig.cs·AIScenarioConfig.cs·ToastMessageConfig.cs·ResourcesMapFallbackTemplateSource.cs | 좌표XZ/Y·건물프리팹목록·타워소비·현재번들·토스트1.5초·프로젝트bytes 설명. /tmp/hexiege-infra-comment-comparison.json의7파일 exact_expected_edit/reverse_raw_equal/허용Inspector8외토큰일치. meta 유지 |
| 2.13·AUD-021 | Domain/Building/BuildingData.cs; Domain/Unit/UnitData.cs; Application/Combat/SpecialAttackRegistry.cs·ISpecialAttackBehavior.cs | 현재 상태·4인자예시·논리방향·실제5공격등록/Bloom별도힐러 설명. /tmp/hexiege-plan213-comparison.json: 전체141문자열보존토큰 동일,지정4주석변경·137원문동일,meta 유지 |
| 2.15·AUD-022 | Presentation/Effects/EffectManager.cs·EffectPreset.cs·UiEffectConfig.cs·UnitEffectConfig.cs·BuildingEffectConfig.cs; Presentation/UI/ViewModels/RaceSelectionViewModel.cs | VFX전담/오디오별도 설명·XML 소유위치·import2제거. /tmp/hexiege-effects-explanation-comparison.json: Tooltip5예외 역복원 후 토큰/나머지리터럴 동일,legacy6원문블록 보존·meta 유지 |
| 2.16·AUD-023 | Bootstrap/GameBootstrapper.cs | OnDestroy 첫부분에 이유주석/Dispose/null 추가. /tmp/hexiege-bootstrap-destroy-comparison.json: 추가3행 제거시fullraw동일·기존 CloseTorrentLaunches→Unregister순서/meta유지. 실제 누수 재현 없는 예방적 수명 정리 |

이번 묶음은 신규파일 없이 기존18C#의 지정 변경이다. Inspector 문자열13개(Infra8+Effects5)는 표시 변경이며 무변경이라고 보고하지 않는다. OnDestroy는 주석만 정리가 아니라 실행2문장의 수명 처리 추가다. 어떤 비교도 실제 컴파일/native/실기 PASS가 아니며 compile/Unity/게임/빌드·TC/QA는 NOT RUN이다. 기존 사용자 수용을 새 리비전 수용으로 확대하지 않고 AUD-017 후속AI보류·6VFX 삭제보류를 유지한다.

### 16.7 현행9문서 지정 사실 정정 결과(2026-10-24 KST)

메인 직접 근거 대조·승인 범위를 Plan2.14에 먼저 확정하고 /tmp/hexiege-plan214-docs-before.json에9전체원문/SHA를 보존한 뒤 지정본문만 정정했다. 문서 담당은 actual Config·Game 씬 GUID 연결·Setup 소비, LocalPlayerDifficulty/Domain enum·AudioManager 경고,현행Status·수용Task 참조,공식Firebase 원문을 직접 읽었다.

- StatsReference: 감쇄하드캡65%와 Garage100/100을 실제 구현 미러에 맞춤(AUD-024/025). Config GUID d7b6bea86b98d354b8d19851830d2678→Game.unity26430→Setup148~188 소비를 확인했으며 코드/직렬화 수치는 변경하지 않음.
- BuildingPanelsUI: MistShrine 규칙8/13의2문장을 미제작VFX/제작후목표로 구분(AUD-026). 별도지속UI없음·패널이열린아군만범위 정책 유지.
- AI: 규칙32/35의Domain enum/private setter/Set API 예시(AUD-027). Human/Spirit/Transcendence 각1문장의 설계범위를 공통난이도/예비타이밍 참조로 맞춤(AUD-028). 생산시간효과 AUD-017은 후속AI보류/미구현이며 고유시나리오숫자는 유지.
- Upgrade: 규칙13의라벨항목을 현행Status/기존UI수용Task로 연결(AUD-029). 헤더아이콘보류·전체기기/장문/SafeArea미검증을 유지.
- Sound: 규칙6 Login내부SplashOverlay/LoginBGM 및 향후별도설계(AUD-030),규칙17 GameLog.Dev.Warn 개발경고(AUD-031).
- Auth: 「세션 및 다기기 동작」규칙1 두문장만 장기Refresh/삭제·비활성화·중요계정변경·서버폐기 무효화 가능성과 약1시간ID token/SDK갱신으로 정정(AUD-032). https://firebase.google.com/docs/auth/admin/manage-sessions 공식 원문(/tmp/hexiege-firebase-manage-sessions.html)을 메인과 문서담당이 독립 확인. 서버30일정책을 추가하거나 규칙2 등 다기기정책을 변경하지 않음.

/tmp/hexiege-plan214-docs-comparison.json은 지정exact치환을 적용한 예상 원문과 실제9파일 전체일치를 확인했고 /tmp/hexiege-plan214-docs.diff에 정확diff를 남겼다. 규칙ID/시나리오표·숫자·수용범위/다른본문은 유지한다. python3 Tools/check_docs.py exit0·문제0(/tmp/hexiege-docs-finish-check.log)은 참조·형식검사이며 의미전체감사·compile/runtime증거가 아니다. Status/Roadmap/추가Memory/옛Task·History는 이번 범위에서 수정하지 않았다. 당시 이전 묶음의 제한된 문서범위 기록은 과거 단계로 보존한다.

### 16.8 UnitView partial 분리·source gate 범위 보존 결과(2026-10-24 KST)

Plan2.17 코드를 메인 계획 검토·신호 뒤 전문 담당이 구현했고 메인 독립 대조도 완료했다. 문서 담당은 실제 main/partial3의 class·DI/helper 위치와 Editor helper/4진입점,신규meta 및 두 비교artifact를 직접 읽었다. 파일별 근거는 /tmp/hexiege-unitview-partial-snapshot/comparison.json·manifest.json·method-comparison.json 및 메인 독립 /tmp/hexiege-unitview-main-independent.json이다.

| 실제 파일 | 결과 |
|---|---|
| Presentation/Unit/UnitView.cs | 기존8641행→3781행. 원래75메서드/4860행을 이동하고partial수식어만추가. main에는필드171순서/initializer/nested type/SerializeField6·Unity callback4·DI·attackgate·기존namespace/base/interface를유지 |
| 신규 UnitView.MovementAuthority.cs/meta | 31메서드·원문1404행이동,wrapper포함1421행. GUID34f2f7a445f146739617334a18f4d383 |
| 신규 UnitView.Movement.cs/meta | 26메서드·원문2801행이동,wrapper포함2818행. GUID54af2cbe64204edbad39831787322535 |
| 신규 UnitView.Healing.cs/meta | 18메서드·원문655행이동,wrapper포함672행. GUID11ce2d3c8feb4789bc36d4a2b89234a9 |
| Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs | main원문+같은폴더UnitView.*.cs를Ordinal정렬·개행연결하는helper추가. 기존3직독/동적inventory의UnitView키1곳을helper로교체. 다른키읽기/Require·Contains·금지API·기존gate판정은유지 |

전문 담당은 전체157원래메서드raw동일·75이동·필드순서/직렬화/4callback·메서드전처리문맥/lexical괄호균형·Editor추가를역복원한fullraw동일/4sourcegate판정동일을 확인했다. 메인은 별도segment원문동일·main정확제거+partial만추가·wrapper·기존meta bytes·신GUID3유일 및 snapshot334중기존2변경/332동일+신규3=337을 독립 확인했다. 새meta3개를 포함해 코드변경은정확8파일이며 신규6파일은7.5증분에 편입했다.

소스경로/줄번호·예외stack진단위치는 달라질 수 있다. 이 결과는 원문이동·정적구조검증이며 actualcompile/Unity/native/게임/빌드·TC/QA는 NOT RUN이다. 전체의미감사·실기수용·글로벌Legacy/VFX삭제완료로확대하지않는다. AUD-017후속AI보류/6VFX삭제보류는 유지한다.

### 16.9 다음 문서 사실 정정·생산 수명·최신 HB 계획 기록(2026-10-24 KST)

문서 담당이 Units규칙42/Stats71/실제Inferno1.5초·Mushroom1초 OnAttackHit1개, Matrix117과 기존 VFX Task Research259/278~279의 impact·새poison프리팹 사용자 승인 원문, TDD Application 그림을 직접 확인했다. Plan2.18 정확3문서 정정은 메인 검토·실행 신호를 받았다. Mushroom1초 문단·권위/lifecycle/전체migration OPEN을 유지하며 숫자·에셋·새 Unity 예외 정책을 바꾸지 않는다.

전문 lifecycle 담당의 생산 UI3파일/Network1파일/Editor안내상수1파일 구체안을 Plan2.19~21에 코드 전에 기록했다. 문서 담당도 ProductionPanelUI Initialize/Show/Close/입력구간, BuildingPanelBase listener·Rx 소유설명, ProductionTicker 수명구간, Network 도달 확인 결과 뒤 기존 침묵값으로 Confirm하는 분기를 직접 읽었다. 런타임 버튼 재대입 없음 검색과 최종 readonlyComposite/AddTo(this)1회·Clear/OnDestroyDispose 방식은 전문 담당 전달 근거로 구분한다. FirstRoundExpectation465~471의 실제 옛 Host재시도/Client미변경 문자열도 직접 읽었다. 각 구현은 메인 GO와 뒤이은 diff/범위 검증을 기다리며 이번 계획 기록만으로 완료·실기 PASS로 승격하지 않는다.

### 16.10 유닛 marker·부분 수용·Application 그림 정확3문서 정정 결과(2026-10-24 KST)

Plan2.18 기록·메인 실행 신호 뒤 정확3문서만 지정 치환했다. AUD-033 Units규칙42는 Inferno의 잘못된1초 복제 대신 Stats/감사표 현행 타격 계약을 참조한다. AUD-034 Matrix Mushroom행은 저장1초 OnAttackHit1개/3초와 기존 impact·새poison프리팹 사용자 승인만 반영했다. AUD-035 TDD그림은 Application을 UseCase·계약 책임으로 설명한다. 실제Config unitType12의hitFrameTimes1.5/cooldown3(162~170행),unitType26의1/cooldown3(345~353행)와 Domain enum12/26도 직접 대조했다.

/tmp/hexiege-plan218-docs-before.json은 수정전 전체원문/SHA, -comparison.json은3문서 exact예상치환과 실제전체원문 일치, -docs.diff는 정확diff다. python3 Tools/check_docs.py 문제0(/tmp/hexiege-plan218-check.log)이며 메인의 독립 최종 대조도 완료했다. 문서 담당은 /tmp/hexiege-plan218-main-independent.json의 exactOneLinePerFile/acceptanceLimitsRetained=true를 직접 읽었다. 메인은 실제3파일각1행만변경·Matrix앞5분류/Behavior필드불변·Incomplete/MigrationRequired와권위착탄/ImpactHitRadius/전체lifecycle미확정유지·TDD책임1행exact정정을 확인했다. Mushroom 규칙의 기존1초 문단·피해/HP권위·수명·tracer/공통시간경계·ImpactHitRadius/전체migration OPEN을 보존했다. 기존 부분승인을 새 리비전 전체수용으로 확대하지 않는다. 코드/meta/에셋/수치/새Unity허용정책·Status/Roadmap/추가Memory/옛Task·History 변경은 없고 실제compiler/Unity/native/실기·TC/QA는 NOT RUN이다.

### 16.11 생산 UI·결과 HB·Editor 안내 정확5소스 결과(2026-10-24 KST)

Plan2.19~21을 코드 전에 기록하고 메인 GO 뒤 전문 lifecycle 담당이 구현했다. 문서 담당은 실제 diff 전부를 직접 읽었고 메인도5파일 diff 전체를 독립 읽어 구체범위 일치를 확인했다고 전달했다. AUD-036~039는 위 단일문제ID로 연결하며 이번 추가 상시문서 수정은 없다.

| 실제 파일 | 구현·전문 정적 검증 |
|---|---|
| Presentation/UI/ProductionPanelUI.cs | 1122→1166행. 서비스참조 갱신 뒤 수명1회 bindingguard, Initialize/Show/Close/OnDisable reset, down/up/Update/longpress 현재열린살아있는건물 guard. AUD-036/037 |
| Presentation/UI/BuildingPanelBase.cs | 402→409행. 자체 Close/OnDemolishButtonClick listener만 Remove/Add. AUD-036 |
| Presentation/Production/ProductionTicker.cs | 884→889행. readonly Composite·수명AddTo(this)1회·Clear후8구독 재바인딩·OnDestroyDispose 유지. AUD-036 |
| Infrastructure/Network/NetworkGameEndController.cs | 1672→1683행. Reachable뒤spawn/listening/leaveflags와 최신HB 침묵30초를 재검사한 뒤확정. AUD-038 |
| Assets/Editor/Debug/ForceRematchMapFailureMenu.cs | 508→506행. FirstRoundExpectation1상수만 M-2 양쪽로비복귀·동일알림/확인1개로 정정. AUD-039 |

/tmp/hexiege_lifecycle_finish/manifest.json·0~4원문·changes.json·verification.json·diff.patch·static_checks.json이 코드전원문/SHA·정확허용변경·역복원 근거다. 문서 담당은 verification5파일 각inverseRawEqual/SerializeField줄보존true 및 static_checks의기존public surface/숫자상수보존·8구독/수명1회·최신HB검사true를 읽었다. 전문 담당의 exact역복원·git diff --check0을 정적근거로 기록하며 메인의 독립 /tmp/hexiege-lifecycle-main-independent.json도 직접 읽었다: rawInverse5=true·existingMeta5Same=true·기존meta/config/scene/prefab/anim3579개 protectedChanged빈목록, latestSignal재계산→Confirm사이에다른yield없음=true다. 이3579개는 메인 비교snapshot의 보호모집단이며 전체프로젝트 의미/실행검증이 아니다. 실제compiler/Unity/native/runtime재현·TC/QA/빌드는 NOT RUN이다. 예방수명정리·명백한옛안내정정이 전체리팩토링/실기PASS는아니다.

### 16.12 최종 범위 장부·미완 경계 동기화(2026-10-24 KST)

아래 coverage는 최신4 scratch JSON의 readRanges/read_ranges/semantic_read_ranges를 파일별 합집합으로 재계수한 값이다. 구조·후보검색은 각 inventory 전체에서 실행됐지만 직접 의미읽기 또는 실행검증과 같지 않다. 행수/SHA는 각 scratch 최초snapshot 기준이며 그 값을 현재파일로 덮어쓰지 않았다. 이번 지정변경 이후의diff검토·정적대조는16.6~11과 개별artifact의 별도근거이며 옛범위 전체의최신리비전정독 완료로 승격하지 않는다.

| scratch 범위 | inventory | 직접읽기 범위 있음 | snapshot 전문 행범위 충족 | 부분범위 | 직접범위 없음 | 읽은 행 합집합 / snapshot 전체행 |
|---|---:|---:|---:|---:|---:|---:|
| Domain/Application | 141 | 132 | 130 | 2 | 9 | 27941 / 44199 |
| Presentation | 98 | 52 | 50 | 2 | 46 | 18768 / 32310 |
| Infra/Core/Bootstrap/Editor | 89 | 40 | 30 | 10 | 49 | 12011 / 58322 |
| 문서·규칙/관련설정 | 89 | 38 | 36 | 2 | 51 | 10740 / 48028 |

재계수 상세는 /tmp/hexiege-final-coverage-recount.json이다. 전문행범위충족은 해당담당이 기록한 직접읽기범위의전체덮음이며 전체정책/모든참조/실행의문제없음 판정이 아니다. 코드·문서·설정 inventory는 서로 다른 모집단이라 중복을 제거하지 않고 총계로합쳐 전체프로젝트완료율을만들지 않는다.

미완·보류: 전체의미감사·미독범위/관련이력추적·실제compile/Unity/native/빌드/실기/TCQA는 미완 또는 NOT RUN이다. AUD-017 AI생산시간배율효과는 후속AI작업보류,6VFX legacy삭제는 보류다. AI경제의차감누락후보·종족보너스(Human생산+10%/TransHP+15%)의기준과현스탯포함여부·람다/Conditional 호출설명은 미확정후보로 유지하며 원인/설계실패로단정하거나임의구현·밸런스수정하지않았다. scratch의옛proposed/awaiting상태는당시발견단계로보존하고 실제이번완료범위는이Task의개별AUD/결과절로판정한다.

현재 작업트리 변경 목록은 아래 `git status --porcelain --untracked-files=all` 읽기전용 결과를 그자리에서 집계했다. HEAD대비변경 전체이며 전부이번회차작성물이라는뜻이아니다. 시작전사용자미커밋과이번Task누적변경의정확귀속은최초status증거없이는추정하지않는다. 메인은 최초작업전status저장artifact가확인되지않았다고전달했다. /tmp/hexiege-autonomous-base.json은2.10후/2.12~17전wholehash이고 /tmp/hexiege-refactoring-inventory.json의최초C#328hash도Git귀속증거는아니다. 이번마지막추가구현5소스(16.11)와직전3문서(16.10),기존Research/Plan갱신은 명시확정범위다. 이전누적수정/메모리/기준선도 목록에포함하며 기존변경을취소하거나Gitmutation하지않았다.

총 71경로: 문서/메모리17 / baseline1 / C#44 / meta9. 추적파일수정51,미추적20. 원시목록 /tmp/hexiege-final-status-porcelain.txt.

| Git상태 | 파일 | 마지막추가구현구분 |
|---|---|---|
|  M | `.claude/MEMORY.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `.claude/agent-memory/_baseline.json` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `.claude/agent-memory/game-design-lead/ai-scenarios.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `.claude/agents/document-manager.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/Editor/Debug/ForceRematchMapFailureMenu.cs` | 2.19~21 추가소스 |
|  M | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` | 2.18 지정문서추가정정 |
|  M | `Assets/_Project/Docs/AuthSystemRules.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Human.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_BuildingPanelsUI.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` | 2.18 지정문서추가정정 |
|  M | `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/StatsReference.md` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Docs/TechnicalDesignDocument.md` | 2.18 지정문서추가정정 |
|  M | `Assets/_Project/Scripts/Application/Combat/ISpecialAttackBehavior.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/Combat/SpecialAttackRegistry.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/Interfaces/IUnitView.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/Services/AIOpponentController.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Core/HexMetricsCoordinateMapper.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Domain/Building/BuildingData.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Domain/Unit/UnitData.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Config/AIScenarioConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Config/BuildingStatsConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Config/ResourcesMapFallbackTemplateSource.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Config/ToastMessageConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs` | 2.19~21 추가소스 |
|  M | `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Effects/UiEffectConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Production/ProductionTicker.cs` | 2.19~21 추가소스 |
|  M | `Assets/_Project/Scripts/Presentation/UI/BuildingPanelBase.cs` | 2.19~21 추가소스 |
|  M | `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs` | 2.19~21 추가소스 |
|  M | `Assets/_Project/Scripts/Presentation/UI/ViewModels/RaceSelectionViewModel.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
|  M | `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Plan.md` | 현재Task기록 |
| ?? | `Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Research.md` | 현재Task기록 |
| ?? | `Assets/_Project/Scripts/Application/AuthException.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/AuthException.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.Healing.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.Healing.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.Movement.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.Movement.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.MovementAuthority.cs` | 이전누적/현재변경 목록; 이번마지막추가아님 |
| ?? | `Assets/_Project/Scripts/Presentation/Unit/UnitView.MovementAuthority.cs.meta` | 이전누적/현재변경 목록; 이번마지막추가아님 |

### 16.13 직전 푸시 후 사용자 컴파일 확인과 잔여 감사 계속(2026-10-24 KST)

사용자 「컴파일 에러없으니 다음진행하자」는 직전71경로 커밋·푸시 `1a69402ce039c4fd1c34373babe048ed37e36e4f` 이후의 컴파일 오류 없음 보고다. 플랫폼·컴파일 로그·전체 게임 실기 결과는 제공되지 않았으므로 이 보고를 게임 동작·모든 기기/멀티·빌드 성공·전체 작업 수용으로 확대하지 않는다. 앞선 각 절의 compile/Unity/native/게임/빌드 NOT RUN은 당시 에이전트가 실행하지 않았다는 증거로 보존하며 이번 사용자 보고와 구분한다.

| 확인 축 | 최신 근거와 한계 |
|---|---|
| 사용자 컴파일 확인 | 직전 푸시 묶음 후 오류 없음 보고; 플랫폼·로그 미제공 |
| 에이전트 컴파일·Unity/native 실행 | 새 실행 없음; 과거 NOT RUN 기록 보존 |
| 전체 게임 실기·사용자 수용 | 이번 보고로 확인되지 않음 |
| 저장소 상태 | 후속 착수 시 직접 확인: `work`, HEAD `1a69402ce039c4fd1c34373babe048ed37e36e4f`, clean. 이 확인 기록으로 Research/Plan만 수정하며 이후 추가 커밋·푸시는 아직 하지 않음 |
| 잔여 감사 | 소스 inventory 최신337개, 전체 의미 감사 미완.16.12의 coverage·71경로 표는 직전 회차 시점 기록이며 현재 작업트리 목록으로 재사용하지 않음 |

기존 전면 진행·Task 기록 승인을 유지해 잔여 본문 감사를 계속한다. 새 구체 후보는 전문 담당 근거를 메인이 검토한 뒤 Plan2.22 이하에 코드 전에 기록하고 메인 구현 신호 순서로 진행한다. 미확정 후보의 일괄 완료 승격이나 새 기획·밸런스 판단은 하지 않는다. AUD-017 생산시간 배율은 후속 AI 작업 보류,6VFX legacy 삭제 보류를 유지한다. 이번 컴파일 확인은 새 Task/TCQA·Status/Roadmap/메모리 전파·옛 Task/History 수정의 근거로 사용하지 않는다.

### 16.14 검사기 과거 한계 토픽의 현재 상태 안내(2026-10-24 KST)

잔여 문서 직접읽기에서 `.claude/agent-memory/game-programmer/check-docs-tool.md`의8월25일33규칙 미검출/UNRESOLVED 원문을 확인했다. 메인은 이를 새 결함으로 재개하지 않고 원문을 보존한 현재 상태 note1곳만 승인했으며 Plan2.23에 먼저 기록한 뒤 해당 heading 직후 English note를 추가했다. 현행 parser935~970은 모든 규칙 문서를 등록하고823~829의ID/정의 parser는 숫자·소번호·문자ID를 지원한다. 메인/문서 담당이 각각 독립 함수호출로 Map5/RandomMap19/Upgrade13 정의를 확인했다. 현행 범위 원본은 WORKFLOW [11]과 매 실행 출력이며 이 note는 게임 동작·의미 감사 완료를 뜻하지 않는다.

원문 `/tmp/hexiege-plan223-topic-before.md` SHA `0daa1272b941cd66b03f3e626c41500114a536072df74d0b9efbfc716f856f3f`를 보존했다. 정확 note삽입1곳을 제거한 전체raw가 원문과 동일함과 기존 heading/anchor·과거본문 보존을 `/tmp/hexiege-plan223-comparison.json`으로 확인했다.126→128행이며 다른 토픽은 수정하지 않았다. 기준선은 실행 직전 도구에서 감소0/game-programmer만+2행·파일21개동일을 확인하고 `--update-baseline` exit0으로 증가만 반영했다. `/tmp/hexiege-plan223-baseline-comparison.json`도 동일delta를 확인했다. 직접 baseline JSON편집·감소/사유반영은 없다. 사용자 컴파일 보고와 무관한 문서 사실 안내이며 새컴파일/Unity/native/게임/빌드·TCQA 실행은 없다.

### 16.15 경로·서비스·운영 로그·프로필4소스 주석 정정 결과(2026-10-24 KST)

메인이 Plan2.22 전체를 읽고 구현 신호를 전달한 뒤 전문 담당이 지정4C# 주석만 정정했다. 실제 파일은 Domain/Hex/HexPathfinder.cs, Application/Interfaces/IGameServices.cs, Application/Interfaces/ILogSink.cs, Presentation/UI/InGameSettingsUI.cs다. 문서 담당은 전체 diff와 `/tmp/hexiege-plan222-comparison.json`을 직접 읽어 계획의 낡은 설명 교체와 조건부 프로필 설명을 확인했다. 정확 oldspan은 Hex20/47~48, IGame17~18, ILog68~71/524~525/549/575/594/615/638, UI456~460이다. 코드 전 전달 근사행번호를 실제원문범위로 정정했으며 동작 범위 변경이 아니다. ILog678→676/UI580→578행, 나머지2파일 행수동일이다.

전문 artifact의4파일 각각 tokens_equal/literals_equal/all_changed_lines_comments/reverse_full_raw_identical=true와 source/meta690개 중 허용4개만변경·나머지686동일을 확인했다. scanner 사본검증도 주석/기존공백추가=동일, 숫자변경=토큰상이, 문자열변경=토큰/리터럴상이로 전달됐다. 정확 전후SHA·span은 comparison JSON, 원문 diff는 `/tmp/hexiege-plan222-source.diff`, 재검증기는 `/tmp/hexiege-plan222-verify.py`다. 이는 전문 담당의 정적 대조 근거이며 메인의 독립 검토 결과와 구분한다. runtime/API·Inspector/metadata문자열·수치·수명·서버writer/seed/canonical/PRNG는 변경하지 않았다. 새컴파일/Unity/native/게임/빌드·TCQA는 미실행이며 직전1a69402c 사용자컴파일보고로 이번소스를검증했다고기록하지않는다.

메인의 독립 `/tmp/hexiege-plan222-main-independent.json`도 문서 담당이 직접 읽었다. 변경 diff행이 모두 `//`주석이고 전체행주석을 제외한 나머지 원바이트가4파일모두 동일함을 별도 방식으로 확인했다. 메인337C# snapshot 중 정확4파일변경/333동일·신규없음이다. 이는 전문 토큰 scanner와 독립적인 원문 비교 근거이며 실행증거는 아니다. 메인의 check_docs 문제0/diffcheck0도 이번 정적 확인으로 구분한다.

### 16.16 TDD 초기 자원 실제 소비 설명2곳 정정(2026-10-24 KST)

메인이 Plan2.24 전체를 읽고 구현 신호를 전달한 뒤 TechnicalDesignDocument.md927/1066행만 정확 계획 문구로 정정했다. ResourceUseCase 표는 초기화/차감/추가/조회 책임으로, 경제 문단은 GameConfig.StartingGold의 현재 소비와 맵 InitialGold 실제 연결 후속을 구분했다. 문서 담당도 Setup457·ResourceUseCase43~48·GameConfig125·ROADMAP 경제 후속·Status 무작위 맵 한계와 실제2행 diff를 직접 읽었다. 경제 수치·규칙·기획/밸런스·코드·에셋·메모리·옛Task/History는 수정하지 않았다.

`/tmp/hexiege-plan224-docs-before.json`과 `-tdd-before.raw`에 수정전1202행 전체원문/SHA `763544b0201d2ea3fc5694004d98b0c7cfd772e221053b56d3cb1c967ce8ee0f`를 보존했다. `/tmp/hexiege-plan224-comparison.json`은 정확2치환/변경물리행927·1066/역복원fullraw동일/기존3링크동일/전후1202행을 확인하며 정확diff는 `-docs.diff`다. check_docs 문제0(`/tmp/hexiege-plan224-check.log`)·git diff --check0을 확인했다. 메인 독립 최종 검토는 별도 수신 후 기록하며 새컴파일/Unity/native/게임/빌드·TCQA 실행은 없다. 직전1a69402c 사용자컴파일보고와 이번 문서 정적 검증을 구분한다.

메인 독립 `/tmp/hexiege-plan224-main-independent.json`을 수신해 직접 읽었다. 정확2치환 외 원문동일·모든Markdown링크보존·변경행927/1066·1202행을 별도 확인한 정적 근거이며 새실행증거가 아니다.

### 16.17 폴백 재생성 안내5문서 정정 단계(2026-10-24 KST)

Plan2.26 전체를 메인이 검토하고 문서 동기화GO를 전달한 뒤 `_Reference/MapTemplateSource/MapTemplateSource_{Canyon,FullyOpen,ObstacleOpen,Outer,ThreeLane}.md`5개의 「다시 만드는 법」2행만 정확 계획 문단1행으로 교체했다. 삭제된 메뉴가 현재 없고 재생성 시 Factory를 직접 호출할 도구를 별도 준비한다는 사실 안내다. 문서 담당은5문서 전문·생성문구794~796·self-validation1038~1085를 직접 읽었다. 자체검사 소스는 hash/seed/date/동일 입력의 문서결정성과 확장자를 검사하며 옛 메뉴 문자열을 gate로 검사하지 않는다. 소스를 읽었을 뿐 자체검사는 실행하지 않았다.

`/tmp/hexiege-plan226-docs-before.json`은5전체원문/SHA를 보존하며 `-docs-comparison.json`은5개 각정확문단치환·역복원fullraw동일·나머지raw동일·53→52행을 확인한다. 정확diff는 `-docs.diff`, check_docs문제0는 `/tmp/hexiege-plan226-check.log`이며 diffcheck0이다. 코드1파일은 별도 전문 담당GO/구현·메인최종검토 대기이며 문서 담당은 코드에 쓰지 않았다. 수치/seed/바이트/해시/생성일/링크/기존meta·생성로직은 문서 정정 범위에서 바뀌지 않았다. 메뉴복원·신규도구·재생성·compile/Unity/native/게임/빌드·TCQA는 실행하지 않았다.

코드 전문 구현과 메인 독립 검토도 완료됐다. 문서 담당은 Factory794~796의 최종3Append와 `/tmp/hexiege-plan226-code-comparison.json`·`/tmp/hexiege-plan226-main-independent.json`을 직접 읽었다. 전문 증거는 허용3literal역복원fullraw동일·Append103개보존·기존meta3268/맵binary5보호이며, 메인 독립은 정확3literal외원문동일·Append3내용추출과5문서실제문단일치·맵생성로직무변경이다. 문단을2행에서1행으로 합쳐 출력 newline escape는55→54로 줄었으며 다른 출력은 보존했다. 실제Factory실행·바이너리재생성·컴파일/게임실기 증거가 아니다.

### 16.18 광산 표현·배치 UI 구독 수명2소스 정리 결과(2026-10-24 KST)

메인 Plan2.25 전체 검토/GO 뒤 전문 담당이 HexGridRenderer81의private bool·SubscribeGoldMineEvents523의중복guard·544의두구독뒤true설정과 BuildingPlacementUI151의OnDestroy Dispose/null만 추가했다. 문서 담당은 실제추가범위와 `/tmp/hexiege-plan225-comparison.json`·메인독립 `/tmp/hexiege-plan225-main-independent.json`을 직접 읽었다. renderer기존2callback/AddTo(this)/ClearGrid·배치UI Show/Close/overlay/색상은그대로이며OnDestroy에서Close/UIManager를호출하지않는다.

코드전 `/tmp/hexiege-plan225-before/`·`/tmp/hexiege-plan225-baseline.json`이전체원문/SHA근거다. 전문검증은2raw역복원동일·public/직렬화/meta불변·보호meta/serialized/shader3805개변경0이며C#337중내2파일+별도승인Factory1파일변경/추가삭제0이다. 메인독립은별도방식으로2파일기존원문전부보존/삽입만·flag두구독뒤·guard1·OnDestroyDispose/null만확인했다. 두수명문제는소스경로근거이며실기재현/새compile/Unity/native/빌드/게임·TCQA는미실행이다.

### 16.19 에셋 역할의 현재 맵 규격 원본 안내(2026-10-24 KST)

메인 Plan2.27 전체 검토/GO 뒤 `.claude/agents/asset-prompt-crafter.md`49행의현재10×29사본만RandomMap원본안내로정정했다. 실제역할행은표준backtickcode span이며Plan의정확문구도동일하다. `/tmp/hexiege-plan227-before.json`은수정전120행원문/SHA, `-comparison.json`은정확1행교체/전체raw역복원동일/120행보존을확인한다. agents는기준선memory-root밖이라baseline무영향이며기준선갱신/JSON쓰기는없다. check_docs문제0(`/tmp/hexiege-plan227-check.log`)·diffcheck0이다. 메인독립 `/tmp/hexiege-plan227-main-independent.json`을직접읽어exact_one_line_replacement/all_other_raw_unchanged=true를확인했다. 새맵수치/코드/다른메모리/정책변경·컴파일/게임실기·TCQA는없다.

### 16.20 잔여 직접읽기의 후속 후보와 보류 경계(2026-10-24 KST)

아래는 전문 담당의 직접읽기 보고에서 나온 후속 후보이며 별도 코드 전 계획·메인 구현 신호가 없는 항목이다. 이번 기록은 구현 완료나 실기 재현 판정이 아니다. 원보고 `/tmp/hexiege-network-observer-resume-read.json`, `/tmp/hexiege-editor-resume-read.json`, `/tmp/hexiege-validator-a-read.json`, `/tmp/hexiege-validator-b-read.json`의 최초 SHA/행범위는 그대로 보존한다. 문서 담당의 일부 위치 재열람을 전체 파일 정독으로 계수하지 않는다.

| 후보 | 직접읽기 근거·추가 확인 범위 | 현재 처리 |
|---|---|---|
| Coordinator 예약 순서 queue와 private 저장값 | 전문 보고: `_reservationOrder`는 예약마다 Enqueue하나 live dictionary가512이하이면 TrimReservations에서 dequeue하지 않고 완료 삭제가 queue를 정리하지 않음. 문서 담당도486~501/1145~1168을 재열람. Token/LegacyScheduledAt/ShadowCommitAt 저장만 있다는 보고는 repository 검색 근거와 함께 원artifact에 있음 | 게임 중 메모리/성능 문제 재현은 없음. queue 수명 정리와 private 값 삭제는 추가 설계·구현하지 않음 |
| Coordinator/Observer 주석 위치·현행 역할 | Coordinator15의반환값 writer 사용금지와 실제 writer authorization 경계의불일치, Observer113의summary가다음ExpiryClockHistory에붙은상태를전문보고와문서담당부분재열람으로확인 | Coordinator현행역할주석은2.32에서정정(16.24). Observer주석이동/partial분리는보류이며source gate의단일파일읽기의존성을함께계획해야함 |
| Logcat 파일명 충돌 상한 | LogcatCapture1348~1362의MakeNonOverwritingPath는base/2~999모두존재하면기존999경로를반환하고284/312에서WriteAllText함. 문서담당이해당본문/호출을재확인 | 정적희귀경계후보이며실제덮어쓰기재현없음. 새충돌/실패정책이나코드수정없음 |
| Logcat 운영 안내·검사기 제외 설명 | LogcatCapture106의과거54KB근거로현재비용단정은LogRules현행「당시크기로현재부담/삭제필요성단정금지」와불일치.731의커밋로그삭제안내도현행정책대조후보. check_docs213의어느깊이설명과259의첫폴더판정은문서담당도재열람 | 54KB일반화/검사기첫폴더설명은2.32에서정정(16.24).731안내는보류. 수동정리/보관/제외범위정책은변경없음 |
| Editor 내부정리·fixture 후속 | RootPose의EndLine/SetInvariant미사용보고는repository검색추가필요. undefined numeric movementPhase허용은별도행동검토후보. UnitAction의XML위치2곳·기존manifest rejection helper중복·C3실패경로cleanup후보는각validator artifact의정확범위참조 | UnitAction XML2곳은2.32에서실제인접본문설명으로정정(16.24). 나머지는보류/미실행. 기존Bloom R-3 legacy assertion과실제호출이있는BloomHealRetimingValidation은삭제후보가아님 |

이후 승인된2.28~32의결과는별도결과절에서기록하며이보류표로해당완료범위를재개하지않는다. AUD-017생산시간배율후속AI보류와6VFX legacy삭제보류를유지한다. 신규도구/정책/밸런스/이력수정·컴파일/Unity/native/게임/빌드·TCQA·Git변경은없다.

### 16.21 이동·표현·힐 주석, Bloom fixture와 연구RPC5소스 결과(2026-10-24 KST)

Plan2.28~30은코드전에기록됐고메인전체검토/GO뒤전문담당이정확5소스를구현했다. 문서담당은 `/tmp/hexiege_code_finish_20261024/source.diff` 전체와receipt/preservation/lexical_preservation JSON을직접읽었다.2.28은UnitMovementContracts의중심소비뒤방향적용설명1곳·UnitAttackResultPresentationShadow의현행표현소유설명2곳·FloatingHpTextSpawner의별도Bloom힐VFX설명1곳이다.3소스실행토큰은리터럴보존비교에서모두동일하며scanner control은주석/공백동일·숫자/리터럴변경상이로확인했다.

2.29는RunUnitActionSelfValidation의BloomClient상태를Set(false,true)로,finally복원을Set(netServer,netActive)로정확2인수치환했다.2.30은NetworkUpgradeController에실제live/Research/expectedTeam을검사하는private static IsOwnedLiveResearchBuilding helper와연구시작/수동취소2guard를삽입했다. 시작실패는기존「연구 불가」통지를재사용하고취소실패는return한다. 기존sender팀검증이후/차감·취소환불이전에가드가위치한다. 파괴시환불별도경로는보존했고실제RPC재현이나fixture실행은없다.

전문담당before/after전체원문·before_manifest·allowed_replacements·receipt는5파일모두허용치환역복원fullraw동일을확인한다. preservation은Assets/Packages/ProjectSettings의5491파일모집단에서정확5대상만변경/나머지동일을확인하며문서/메모리전체모집단또는프로젝트의미검증이아니다. 메인의 `/tmp/hexiege-plan228-230-main-independent.json`도직접읽었다:5diff독립검토/각reverse_raw_equal=true이며메인은주석3본문원바이트동일·fixture정확2인수치환·연구helper/guard삽입만과mutation전위치를별도로확인했다고전달했다. public API/직렬화/meta/자산불변은해당정적비교근거다. 새compile/Unity/native/게임/빌드·TCQA는NOT RUN이며직전1a69402c사용자컴파일보고를이번5파일에적용하지않는다.

### 16.22 생산RPC4곳의 실제 소유권·음수 슬롯 검증 결과(2026-10-24 KST)

Plan2.31을코드전에기록한뒤메인이전문검토/GO했고전문담당이NetworkProductionController.cs1파일만구현했다. 문서담당은 `/tmp/hexiege_plan231_20261024/source.diff` 전체와receipt를직접읽었다. Enqueue는기존state null검사뒤/비용계산전에실제state.Team검증과기존「팀 불일치」통지,Cancel은mutation전에state null/team/slotIndex<0검증,Rally/Toggle은mutation전에state null/team검증을추가했다. Toggle의성공후GetState재조회는유지하고기존var선언만대입으로바꿨다. 정상소유자흐름·로그/문자열·public/API/직렬화·기존다른변경은보존한다.

before.cs/after.cs·allowed_replacements·before_manifest·receipt는허용5치환역복원fullraw동일과Assets/Packages/ProjectSettings5491파일중해당1개만변경을확인한다. 전SHA `edeabfcfa8fc3382d22e21bedc7f70f742724d4a7d7b490b6f2ac2d19c60da65`, 후SHA `3e02c8269d9a812fa6bb7a16560e9641106adaa3eb6e491297cb7fdf26162ac3`다. meta/자산불변은이보호모집단의정적대조이며실제RPC/예외재현·compile/Unity/native/게임/빌드·TCQA는NOT RUN이다. 메인독립`/tmp/hexiege-plan231-main-independent.json`을직접읽어정확4삽입/1선언변경역복원raw동일·4guard가mutation앞·음수인덱스가UseCase뺄셈전에거부됨을확인했다. slotIndex상한/enum/종족/유닛종류/팀배정/환불정책은추가하지않았고AUD-017후속AI보류·6VFX삭제보류를유지한다.

### 16.23 마감 범위 장부: 현행 소스 읽기·문서 사실 대조·실행의 구분(2026-10-24 KST)

16.12는직전회차의당시coverage기록이다. 이번재개에서메인과각전문담당이잔여본문을읽고후속변경diff를분리해취합했다. 아래수치는각scratch의실제필드/범위를직접읽어기록했으며원snapshot SHA/행수/ranges는덮어쓰지않았다. 모든소스의한시점원본을동시에정독했다거나모든정책·참조·실행을검증했다는뜻은아니다.

| 범위 | 직접읽기 근거와 한계 |
|---|---|
| Domain/Application141파일 | `/tmp/hexiege-full-domain-app-audit.json`의누적readRanges/coverageSummary는141전문·44199장부행을기록. 원SHA와후속변경diff근거를분리하며미사용검색만으로삭제하지않음 |
| Presentation98파일 | `/tmp/hexiege-full-presentation-audit.json`의continuation_summary는잔여31파일8961행을추가해98파일전문근거를취합. 원summary67/부분1과원SHA는보존. InGameSettingsUI는원본과다른읽기리비전으로분리됨 |
| Infra/Core/Bootstrap/Editor/Tools89파일 | `/tmp/hexiege-full-infra-audit.json`은특정리비전별전문근거89파일58342행. 원리비전88파일47758행과원Action의미독10564행은그대로보존하며Action은별도c81d7e5c리비전10631행전문을읽음. 후속현재변경13파일의diff검토는그때의별도증거이며이후2.32도개별artifact로보충 |
| 현재경로집합 | `/tmp/hexiege-audit-close-path-delta.json`: 기존장부141+98+89=328경로와별도UnitView본체/3partial4경로를합쳐332. 프로젝트327C#+Tools재귀4Python+루트1Python이며Assets337C#중vendor10은프로젝트소스범위밖. 추가Interface/DTO/Auth경로누락0은경로대조판정. UnitView4는16.8/Plan7.21의원문·partial매핑·메인독립대조를참조하며새전문읽기량을이중계수하지않음 |
| 현행문서원본/미러/참조45와역할5 | `/tmp/hexiege-docs-kind-coverage-ledger.json`은각원snapshot범위전문45파일13774행/역할5파일652행을기록. `/tmp/hexiege-docs-close-current-revision-delta.json`은이50파일중원SHA와현SHA가다른18경로를구분하며해당승인변경/diff근거는본Task각결과절을따름 |
| 혼합메모리/이력39파일 | 같은종류장부에서혼합메모리31중전문범위17/직접합집합1930행,이력·마일스톤3부분167행,폐지역할3/사건기록2는장부상직접읽기0. 이는원장부당시범위이며이번운영규칙/관련부분재열람을전체재독으로합치지않음. 광범위이력/메모리미독은보존하며현행45+역할5의직접읽기미완으로혼동하지않음 |

읽기는확정구체후보를선별한근거이며전체의미PASS·모든기능보존·게임실기수용과다르다. 구현완료는각Plan2.22이하의허용변경과독립정적대조범위에서만판정한다. 최근사용자의컴파일오류없음보고는1a69402c직전푸시묶음이며그뒤수정의컴파일/Unity/native/게임/빌드·TCQA는NOT RUN이다. 새Task/TC·상태/로드맵/메모리/이력전파·커밋/푸시는수행하지않는다. AUD-017후속AI보류·6VFX삭제보류와16.20의미승인구조/동작후보를유지하며끝없는추가후보탐색없이마지막2.32뒤문서검사/인계로정리한다.

### 16.24 마지막7파일 사실 설명 정정 결과(2026-10-24 KST)

Plan2.32전체/정확proposal을메인이읽고GO한뒤전문담당이7파일14치환을구현하고소스를동결했다. 문서담당도 `/tmp/hexiege_plan232_20261024/source.diff` 전체와receipt를직접읽었다. Coordinator/NCC의현행권위·identity/scope·시계·provisional/실제공격분리설명,SkillData C필드소비설명,Logcat과거54KB일반화제거,check_docs첫폴더제외설명,Validator인접실제HoT/Sniper검사설명을정정했다. SkillDefinition은기존Tooltip1문자열을현재C실행기사용설명으로바꿨다. Inspector안내표시가실제로바뀌므로모든관측동작동일이라고주장하지않는다.

before/after전체원문/SHA·allowed_replacements·before_manifest·receipt는7개모두역복원fullraw동일,5C#주석파일리터럴보존토큰동일,Python주석1개AST동일,Tooltip1문자열외토큰동일을확인한다. scanner control4개도예상결과다. 보호모집단은Assets/Packages/ProjectSettings/Tools에서prose .md와__pycache__를제외한4823파일이며정확7대상만변경/나머지동일이다. 앞선5491/3579/3805/3617모집단과선택범위가달라같은모집단수로혼용하지않는다. NCC는앞선3392행전문읽기와최종3393행SHA `d87b3353e8a2f5239bb94c2b17f4343fec6f4c646d12aeecd1b465f3f5de570f`의변경XML재독을별도 `NetworkCombatController.final-read.json`으로연결한다. 새컴파일/Unity/native/게임/빌드·TCQA는없고Python AST파싱/소스비교만수행했다. 메인독립`/tmp/hexiege-refactoring-final-main.json`도직접읽어정확14치환외원문동일/5C#주석본문raw동일/PythonAST동일/Tooltip1표시문자열만변경을확인했다. 새구조/로직/보안확장·queue/미사용값삭제·정책/밸런스/이력수정은없다.

### 16.25 이번 자동 선정 묶음 마감·현재 변경 경로 인계(2026-10-24 KST)

이번 마감은 검토 대상 경로 누락 해소와 선정된 Plan2.22~32 구현·문서 정정의 인계다. 모든 역사·모든 주장·모든 게임 기능 검증 또는 전체 리팩토링 종료라고 판정하지 않는다. 변경 유형은 사실 설명/안내 정정, UI/구독 수명 정리, 기존 Bloom fixture 상태 정정, 연구/생산 RPC의 실제 대상 소유권 검증과 음수 취소 인덱스 거부다. 각 구현 전 계획과 메인 GO·실제 diff·독립 정적 근거는16.14~24에 연결했다.

메인 최종 `/tmp/hexiege-refactoring-final-main.json`을 문서 담당도 직접 읽었다. 체크포인트 대비 Assets C#337개 집합 추가/삭제0·변경18개다. `/tmp/hexiege-autonomous-base.json`의 기존 파일 중 meta/asset/prefab/unity/anim/controller/overrideController/bytes를 선택한3617개는 SHA가 모두 동일하다. 이는 해당 suffix 보호 모집단 비교이며 다른 회차의3579/3805/4823/5491개 모집단과 합치지 않는다. 최종 소스 동결 뒤 새 후보 탐색이나 마이크로 정리를 계속하지 않았다.

현재 `work`의 HEAD `1a69402ce039c4fd1c34373babe048ed37e36e4f` 대비 변경은 아래 정확30경로다. 메인 최종 status artifact와 문서 담당의 `/tmp/hexiege-docs-close-status.txt`를 대조했다. 전부 기존 추적 파일의 unstaged 수정(` M`)이고 staged/추가/삭제 경로는 없다. 유형별 C#18/Python1/문서·메모리·역할10/기준선JSON1이다. 이번 후속 변경은 미커밋·미푸시 상태이며 이 작업에서 Git 변경 명령을 실행하지 않았다.

| 현재 변경 경로 | 변경 유형 |
|---|---|
| `.claude/agent-memory/_baseline.json` | 기준선 증가 반영 |
| `.claude/agent-memory/game-programmer/check-docs-tool.md` | 과거 기록 보존·현재 안내 |
| `.claude/agents/asset-prompt-crafter.md` | 현재 맵 원본 안내 |
| `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` | fixture 인수·사실 주석 |
| `Assets/Editor/Tools/LogcatCapture.cs` | 사실 주석 |
| `Assets/_Project/Docs/TechnicalDesignDocument.md` | 초기 자원 소비 설명 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_Canyon.md` | 재생성 안내 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_FullyOpen.md` | 재생성 안내 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_ObstacleOpen.md` | 재생성 안내 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_Outer.md` | 재생성 안내 |
| `Assets/_Project/Docs/_Reference/MapTemplateSource/MapTemplateSource_ThreeLane.md` | 재생성 안내 |
| `Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Plan.md` | 기존 Task 계획·결과 |
| `Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Research.md` | 기존 Task 근거·범위 |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Application/Interfaces/IGameServices.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Application/Interfaces/ILogSink.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Application/Skill/SkillData.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Domain/Hex/HexPathfinder.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Domain/Map/MapFallbackTemplateFactory.cs` | 생성 문서 안내 문자열 |
| `Assets/_Project/Scripts/Infrastructure/Config/SkillDefinition.cs` | Inspector Tooltip |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` | RPC 실제 소유권·음수 슬롯 검증 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs` | RPC 실제 연구소 검증 |
| `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs` | 중복 구독 방지 |
| `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs` | 구독 수명 정리 |
| `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs` | 사실 주석 |
| `Assets/_Project/Scripts/Presentation/UI/InGameSettingsUI.cs` | 사실 주석 |
| `Tools/check_docs.py` | 사실 주석 |

남은 경계는16.20의 예약 queue/private 저장값·Observer 주석/분리·RootPose/fixture 내부 정리·Logcat999 충돌 및 미결 운영 안내 후보다. 별도 계획/구현 없이 기록만 유지한다. AUD-017 AI 생산시간 배율은 사용자 결정대로 후속 AI 작업 보류,6VFX legacy 최종 삭제도 보류다. 새 정책/밸런스·미승인 구조/보안 확장을 하지 않았다.

확인된 축은 직접읽기·경로 대조·허용 변경 역복원/토큰/AST/보호 SHA와 문서 정합성이다. 사용자 컴파일 보고는 직전1a69402c 묶음 범위이며 이번 후속30경로의 새 컴파일/Unity/native/게임/빌드·TCQA는 NOT RUN이다. 컴파일·실기 결과를 확인하지 않은 채 전체 기능 보존 PASS나 전체 사용자 수용으로 승격하지 않는다. 새 Task/Testcase·상태/로드맵/메모리/이력 전파·커밋/푸시는 수행하지 않았다.

최종 `python3 Tools/check_docs.py`는 문제0, `git diff --check`는0이다. 검사기가 제외하는 이 Task2파일의 상대 Markdown 링크1092개는 별도 파일 존재 확인에서 누락0이며 앵커 포함 링크0이다(`/tmp/hexiege-docs-close-task-links.json`). 링크 존재와 검사0은 모든 내용의 의미/구현 일치를 보증하지 않는다.

### 16.26 기존30파일 푸시 영수증과 다음2파일 코드 전 조사(2026-10-25 KST)

사용자 「푸시하고 다음 작업 이어서」에 따라 메인이 기존30파일을 `e1588fabd3f4b583b3729d3021533957570cb345` (`refactor: tighten request ownership and lifecycle cleanup`)으로 커밋하고 origin/work에 푸시했다. 문서 담당은 local HEAD/commit stat의 정확30파일을 직접 확인했다. 메인은 원격 refs/heads/work가 동일 SHA이고 푸시 직후 status clean임을 확인해 전달했다. 원격 근거는 메인 전달값이며 문서 담당이 원격 조회를 재실행하지 않았다. 브랜치: [work](https://github.com/rmsepskek02/Hexiege/tree/work). 16.25/Plan7.35의 미커밋·미푸시는 당시 기록으로 보존하며 이번 영수증으로 후속 상태를 구분한다.

이번 e1588fab에 대한 새 사용자 컴파일 성공 보고는 없다. 기존 「컴파일 에러없으니 다음진행하자」는 직전1a69402c 묶음의 사용자 보고다. 컴파일/Unity/native/게임/빌드·TCQA는 새로 수행하지 않았으며 푸시 성공이 전체 의미 감사·모든 기능 보존·실기 PASS를 뜻하지 않는다. 기존332프로젝트 경로 감사 완료는16.23의 리비전별 직접읽기와 이후 변경 diff 근거의 범위로 유지한다. AUD-017 후속 AI 생산배율과 legacy VFX6 최종 삭제 보류도 유지한다.

다음은 기존16.20/16.25에 남긴 후보 중 정확2파일만 선정한다. 전문 담당의 제안과 현재 소스를 직접 대조했다. LogcatCapture1348의 MakeNonOverwritingPath는 기본 이름과 `_2`…`_999`가 모두 있으면 기존 `_999`를 반환하며, SaveDevice의284행 호출은 경로 준비 try 안에 있고286~296행 catch가312행 쓰기 전에 return한다. Plan2.33은 기존 상한과 정상 이름을 보존하고 점유 시 IOException으로 기존 catch를 거치도록 하며 해당 안내문을 정정한다. 원자적 생성/쓰기 race 보장이나 상한 확장은 포함하지 않는다.

RunUnitActionSelfValidation5975~5990의 oversizedRejected 블록과6324행 기존 RequireB2ManifestRejected는 동일 null reflection target/같은 entries 객체를 한 인자 배열로 전달하고 동일 TargetInvocationException/InnerException InvalidOperationException 필터만 처리한다. 정상반환 시 Require 실패와 다른 예외 전파도 동일하다. Plan2.34는 동일 실패 문구를 넘기는 기존 helper 한 호출로만 치환한다. `maximumChunks + 1`,580자 payload와 주변 순서는 유지한다. 이 행번호는 e1588fab 소스 기준이며 실행 증거가 아니다.

Plan2.33/2.34를 소스 변경 전에 기록했으며 메인이 두 계획 전체를 직접 읽고 전문 담당에게 GO를 전달했다. 메인 검토로2.33의 기본 이름 미점유 조건을 명확히 하고2.34는 실패 stack trace의 helper frame 추가/소스 줄 위치 변화 가능성을 명시했다. assert 조건·실패 문구·예외 전파 보존과 진단 위치의 동일성은 구분한다. 새 후보·대형 감사 확장, 원시 로그 생성/삭제/업로드, TC 작성/QA·컴파일/Unity 실행, 새 Task·상시상태/메모리 전파는 하지 않는다. 이 문서 기록 뒤 다음 묶음의 추가 커밋/푸시는 메인 별도 지시 전 수행하지 않는다.

코드 전 문서검사: `python3 Tools/check_docs.py` exit0·문제0, `git diff --check` exit0을 확인했다. `_Tasks/`는 검사기 제외이므로 별도 상대 Markdown 링크1092개를 다시 수집해 파일 누락0/앵커 포함0을 확인했다(`/tmp/hexiege-next-docs-task-links.json`). 외부 URL은 조회하지 않았다. 이 검사와 위 소스 분기 대조는 링크 존재·문서 정합성·정적 흐름 확인이며 실제 예외/fixture 실행이나 기능 PASS가 아니다. 구현 후 변경·독립 검토 근거는 별도 후속 절에 기록한다.

### 16.27 다음2파일 구현과 메인 독립 정적 대조(2026-10-25 KST)

Plan2.33/2.34 코드 전 기록과 메인 전체 검토/GO 뒤 전문 담당이 정확2파일을 구현하고 소스를 동결했다. 문서 담당은 두 파일의 전체 diff와 `/tmp/hexiege_plan233_234_20261024/manifest.json`·`static-review.json`, 메인의 `/tmp/hexiege-next-main-independent.json`을 직접 읽었다. LogcatCapture는 기존 loop 뒤의 점유 guard/IOException과 경로 준비 실패 안내2span만 변경했다. 기존 상한/정상 이름 선택/경로 표시/원인 표시/쓰기 전 catch return은 유지했다. Combat은 oversized manifest 중복 블록을 동일 인자/실패 문구의 기존 RequireB2ManifestRejected 호출 하나로 치환했다. 기존 helper 본문·fixture 크기/580자 payload/주변 순서는 유지했다.

전문 근거는 before/after 전체원문·SHA와 허용 치환 역복원 full raw 동일, 각 소스 meta 동일을 포함한다. 메인은 e1588fab의 원문을 독립적으로 읽고 Logcat guard+안내2문자열 역복원 raw 동일/기존 catch가 쓰기 전에 return하는 순서, Combat의 정확1블록 치환과 기존 helper를 원래 인자로 확장한 리터럴 보존 토큰 동일을 확인했다. 소스 분기 대조에서 기본 이름 미점유/기본 점유 뒤 `_2` 미점유/`_999` 미점유/전부 점유를 구분했으며 실제 파일 생성이나 예외 실행은 하지 않았다. File.Exists와 쓰기는 원자적이지 않으므로 검사 뒤 동시 생성 경쟁은 남는다. helper 호출은 실패 stack trace frame/소스 줄 위치에 영향을 줄 수 있으며 모든 관측 출력 동일로 확대하지 않는다.

현재 추가 변경은 C#2와 기존 Task2의 정확4경로이며 신규 파일은 없다. HEAD는 e1588fab이고 이 추가분은 미커밋·미푸시다. 원시 로그 생성/삭제/업로드·캡처/정리 메뉴·새 후보/대형 감사 확장·상시문서/메모리 전파는 하지 않았다. 컴파일/fixture/Unity/native/게임/빌드·TCQA는 NOT RUN이다. 직전1a69402c 사용자 컴파일 보고를 이번 소스의 실행 검증으로 재사용하지 않는다. AUD-017 후속 AI 생산배율과 legacy VFX6 최종 삭제 보류를 유지한다.

구현 결과 문서 마감 검사에서도 `python3 Tools/check_docs.py` exit0·문제0과 `git diff --check` exit0을 확인했다. 검사 제외인 이 Task2파일의 상대 Markdown 링크1092개는 별도 확인에서 파일 누락0/앵커 포함0이다(`/tmp/hexiege-next-docs-task-links.json`). 이 결과는 문서 정합성·파일 존재와 정적 소스 대조의 범위이며 전체 의미/게임기능 PASS가 아니다.

### 16.28 예약 순서 queue 후속 후보 조사 진행(2026-10-25 KST)

사용자의 계속 진행 승인 아래 메인은 최신 상태 질문에 직전 인계 후 배경 실행하지 않았음을 설명하고, 기존 후보 중 UnitAttackShadowCoordinator의 예약 순서 queue와 private 저장값을 후속 조사 대상으로 좁혔다. 새 전체 감사나 구현 완료로 기록하지 않는다. 기존2.33/2.34의 C#2+Task2 추가4경로는 보존하며 현재 문서 담당의 쓰기 범위는 이 Task의 Research/Plan뿐이다.

문서 담당이 현재 소스의 ReservationKey·삽입·Clear·TrimReservations·퇴역·공격자 제거 경로를 직접 읽었다. `_reservationOrder`에는 예약 삽입마다 키가 추가되지만 TrimReservations의 dequeue는 live dictionary 수가512를 넘을 때만 실행된다. RetireReservationIfComplete와 RemoveReservations는 dictionary 항목만 제거하므로 정상 완료/공격자 제거가 반복되고 live 수가 상한 이하이면 queue에 stale 키가 남을 수 있다. 이는 소스상 누적 가능성의 근거이며 실제 런타임 메모리 증가를 측정한 결과가 아니다.

ReservationKey는 AttackerInstanceId와 token 값으로 구성된다. 따라서 단순 ContainsKey 기반 압축은 동일 키가 재삽입되는 경우 과거 queue 항목과 새 live 예약의 세대를 구별하지 못한다. 동일 AttackerInstanceId 재등록·token 재사용 가능성과 기존 FIFO 퇴역 순서의 의미를 전문 담당이 검토 중이다. live 상한512·취소 마스크/tombstone·정규 identity/권위 정책을 보존하는 구체안과 메인 선정이 확정되기 전에는 Plan2.35를 구현안으로 확정하지 않는다. private 저장값도 참조 검색만으로 삭제하지 않고 WORKFLOW [4]의 비활성화·사용자 테스트·최종 삭제 경계를 유지한다.

이번 절은 조사 중간 기록이며 코드 변경·테스트·TCQA·컴파일/Unity/native/게임/빌드 실행은 없다. 기존1a69402c 사용자 컴파일 보고를 e1588fab 및 그 뒤 추가분의 검증으로 확대하지 않는다. AUD-017 후속 AI 생산배율과 legacy VFX6 삭제 보류, 상시문서/메모리/Status/Roadmap/옛 Task·History 미편집을 유지한다. 구체안 수신 뒤 코드 전 계획→메인 전체 검토/GO→전문 구현 순서로 이어간다.

후속 전문 검토와 메인 판정으로 **이번 queue compaction 구현은 보류**한다. 원본 경로는 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowCoordinator.cs`다. 문서 담당도 ObserveIntent182~188/Prepare378~386의 새 Entry 생성, ScheduleLegacyAttack473~499의 유한 시각 검증·token 증가·예약 대입/Enqueue, Retire1058~1062의 예약/Entry 제거를 직접 읽었다. 새 Entry의 LastLegacyToken은0부터 시작하므로 Retire 후 동일 AttackerInstanceId를 재등록하면 `(instance, token1)`을 재사용할 수 있다. 현재 키 존재만 확인하는 압축은 재사용 키의 과거/현재 세대를 구별하지 못하며 중복 키를 모두 남기면 제안된1024개 상한도 보장되지 않는다.

전문 담당이 제시하고 메인이 검토한 반례는 다음과 같다. queue 앞에 퇴역한 `(A,1)`이 있고 뒤에 다른 공격자들의 live 예약512개가 있을 때, 먼저 stale `(A,1)`을 압축으로 지운 뒤 A를 같은 instance로 재등록하여 token1을 예약하면 live 수가513이 된다. 기존 TrimReservations는 앞의 옛 `(A,1)`을 dequeue하여 새 A 예약을 제거하지만, 압축 후에는 다른 공격자의 첫 live 예약을 제거한다. 따라서 현재 stale인 키만 지우는 방식도 미래 키 재사용 때 기존 퇴역 동작을 바꿀 수 있다. HashSet 중복 제거나 generation을 새로 부여하는 방안도 기존 alias 동작/수명 계약을 수정하므로 이번 기능 보존 범위에서 채택하지 않는다. 이는 기존 ABA 동작의 보존을 위한 보류 판단이며, 기존 queue가 bounded이거나 모든 동작이 안전하다는 판정이 아니다.

실제 caller와 공개 API의 경계도 구분한다. `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`235행의 static counter와2378~2388행의 증가 발급,2786~2799행의 사망 mapping 제거는 문서 담당이 직접 읽었다. 이 caller의 현재 경로는 증가하는 instance를 발급하지만 Coordinator의 동일 instance 재등록을 금지하는 공개 계약과 같지는 않다. 전문 담당이 수행한 Assets/Editor·Tools·Scripts 및 추적 형식 검색에서는 queue/private 필드의 외부 직접 참조를 찾지 못했고, 기존 fixture는 로컬 Coordinator/public API를 사용하며 Retire 후 재등록 사례가 없다고 전달했다. 이 검색/fixture 범위는 전문 담당 전달 근거이며 문서 담당이 전부 재검색하거나 실행한 결과가 아니다.

LegacyReservation의 Token·LegacyScheduledAt·ShadowCommitAt은 전문 검색상 선언/초기화 외 읽기 없는 저장값 후보이며 문서 담당도 해당 선언/초기화를 직접 확인했다. 그러나 세 값의 최종 삭제는 WORKFLOW [4] gate 전 보류한다. LegacyScheduledAt의 원래 입력에 대한 IsFinite 검증, Entry.LastLegacyToken 및 ReservationKey.Token은 삭제 대상이 아니다. 원코드는 유지하며 새 수명 계약 채택/ABA 수정 승인 질문을 추가하지 않는다. 다음 별도 읽기 후보는 RootPoseCrossAudit의 EndLine 저장과 SetInvariant 인자지만 아직 구현 대상으로 선정하지 않았고 Plan2.35도 미작성이다.

### 16.29 RootPose 내부 비소비 데이터 코드 전 계획(2026-10-25 KST)

16.28 이후 메인이 별도 RootPose 후보를 선정하여 Plan2.35를 코드 전에 작성했다. 대상은 `Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs`의 EndLine 선언/대입과 두 SetInvariant overload의 미사용 field 매개변수/호출 literal인자, 정확6span이다. 문서 담당이1561~1567 호출·1592대입·1696~1728메서드·2268/2281 private 증거 타입/필드를 직접 읽었다. 두 본문은 field를 읽지 않으며 EndLine은 전문 검색상 선언/대입 외 소비가 없다. 인자 제거 대상은 부작용 없는 문자열 literal이고 실제 ReadValue/ReadLong 파싱 키는 보존한다.

소유 타입은 private sealed·비Serializable 내부 증거 객체이며 전문 담당의 source/config/Unity serializer 형식 검색상 외부 반사·직렬화 소비 참조도 찾지 못했다. 전체 검색은 전문 전달 근거이고 문서 담당 직접 읽기와 구분한다. 기능 분기/판정/파싱/예외/로그 제거가 아니라 소비 없는 저장/전달만 정리하는 한정안이며 queue compaction·Coordinator private3값 보류는 유지한다. 정확 원문/SHA·6span 역복원 full raw 동일·메서드 본문2개 raw 동일·실제 파싱 키2개/기존 meta 불변을 확인할 계획이다. 메인 전체 계획 검토/GO 전에는 구현하지 않으며 현재 코드/컴파일/fixture/Unity/native/게임/빌드·TCQA 실행은 없다.

### 16.30 RootPose 비소비 데이터 정리 결과·이번 추가분 인계(2026-10-25 KST)

Plan2.35의 코드 전 기록과 메인 전체 검토/GO 뒤 전문 담당이 RootPose 감사기 정확1파일6span만 구현하고 소스를 동결했다. 문서 담당도 전체 diff와 `/tmp/hexiege_plan235_20261024/static-review.json`, 메인 독립 `/tmp/hexiege-plan235-main-independent.json`을 직접 읽었다. EndLine 선언/대입과 두 SetInvariant overload의 사용하지 않는 field 매개변수/호출 literal인자만 제거했으며 메서드 본문2개·실제 ReadValue/ReadLong 파싱 키2문장·BEGIN/END 집계·ParseErrorCount·판정/예외/로그·공용 메뉴/API는 유지했다. 소비 없는 private 내부 저장/전달 정리이며 행동 있는 기존 기능의 폐기나 Coordinator private3값 삭제 승인으로 확대하지 않는다.

전문 artifact에는 수정 전 원문·SHA와 역복원 원문을 보존했다. source SHA는 `f8ea2089020d713a46c9f31a560916e8234b8fc7109f39a6969ce70626973d37`에서 `7ac1aa9cca040f8a4220e8004ddfc955267144b04ec325bc24a18daf947e47c4`로 바뀌었으며 정확6span 치환/역복원 full raw 동일·메서드 본문2개 raw 동일·실제 파싱 키 보존·기존 Root meta 및 지정 보호 파일 SHA 동일을 확인했다. 첫 정적 비교는 generic sharedSessionKey ReadValue 식이 파일 전체1회라는 잘못된 검증 가정 때문에 중단됐으나 실제로 다른 parser를 포함해2회였다. 정확 Parse local 선언으로 식별해 재비교했으며 이 과정의 추가 소스 수정이나 실행 테스트는 없었다. 검증 가정 정정과 소스 결함을 구분한다.

메인 독립 결과는 e1588fab 원문에 대한 정확6치환 외 full raw 동일·두 본문 완전 동일·두 실제 파싱 키문장 원문 유지·queue 무변경을 별도로 확인한다. 현재 변경은 아래 정확5경로이며 기존 파일의 추가분은 미커밋·미푸시다. 앞선2.33/2.34의 수정도 보존했다.

| 현재 변경 경로 | 구분 |
|---|---|
|`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`|2.34 helper 재사용|
|`Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs`|2.35 비소비 저장/인자 정리|
|`Assets/Editor/Tools/LogcatCapture.cs`|2.33 이름 소진 guard/안내|
|`Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Plan.md`|기존 Task 계획/결과|
|`Assets/_Project/Docs/_Tasks/2026-10-23/14_08_architecture-refactoring-audit/Research.md`|기존 Task 조사/근거|

queue compaction은16.28의 동일 instance/token 재사용 반례 때문에 보류하며 bounded 완료로 판정하지 않는다. Coordinator private3값·AUD-017 후속 AI 생산배율·legacy VFX6 최종 삭제 보류도 유지한다. 추가 후보 탐색·상시문서/메모리/Status/Roadmap/옛 Task·History 편집·Git 변경은 하지 않는다. 기존1a69402c 사용자 컴파일 보고는 과거 묶음 범위이며 e1588fab 및 이번 추가분의 새 컴파일/fixture/Unity/native/로그 분석/게임/빌드·TCQA는 NOT RUN이다. 메인 check_docs 문제0/diffcheck0은 정적 문서/형식 근거이며 전체 의미나 실행 PASS가 아니다.

결과 기록 뒤 문서 담당도 `python3 Tools/check_docs.py` exit0·문제0 및 `git diff --check` exit0을 확인했다. 검사 제외 Task2의 별도 상대 Markdown 링크1092개는 파일 누락0/앵커 포함0이다(`/tmp/hexiege-queue-root-docs-task-links.json`). 외부 URL 조회나 전체 의미/게임 기능 검증은 하지 않았다.
