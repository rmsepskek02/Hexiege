# 아키텍처 준수·기능 보존 리팩토링 계획

기존 게임의 동작과 사용자가 이미 확인한 기능을 유지하면서, 코드를 이해하고 고치기 쉬운 구조로 정리한다. 먼저 문서·코드·에셋의 관계와 남은 조사 범위를 기록하고, 근거가 확보된 작은 작업부터 진행한다. 이번 문서는 전체 작업의 순서와 첫 조사 범위를 정하며, 아직 확인하지 않은 코드의 삭제나 재설계를 확정하지 않는다.

> **기존 로직 제거와 삭제 보류:** 실행 로직을 폐기하는 삭제는 현재 범위에 없다. **Plan2.6의 AuthErrorReason/AuthException은 동작 폐기가 아닌 정의 이관이다. 목적지 Application/AuthException.cs에 기존 enum13순서와 예외 body를 먼저 보존·비교한 뒤 원위치의 중복 정의를 옮긴다. old class/shim/주석 dead block은 새로 남기지 않으며, 실기 대기 Legacy 최종 삭제와 구분한다.** WORKFLOW [4]에 따라 제거 후보의 안전 근거를 먼저 적고, 검증 전에는 비활성화(주석 처리)를 기본으로 한다. 최종 삭제는 사용자 테스트 통과 후·문서/메모리 업데이트 전에 해당 승인 범위로 수행한다. ROADMAP의 VFX 옛 로직6곳은 기존 결함의 사용자 테스트 통과 조건을 유지한다. `Legacy`, `Shadow`, `temp`, `Setup`이라는 이름이나 검색 참조0만으로 삭제하지 않는다.

- 기준: 2026-10-23 14:08 KST.
- 조사 장부와 근거: [Research.md](Research.md).
- 현재 승인: Research/Plan 작성·조사 착수에 이어, Plan2.3 공유 후 사용자 「바로진행하자」로 첫 코드 묶음 구현 승인(2026-10-23).
- 후속 상태: Plan2.4 공유 후 사용자가 「이 범위로 구현 진행」으로 명시 승인했다. 기존2파일 주석4곳·불필요 using1행 정리와 메인 독립 정적 대조를 마쳤다. 컴파일·실기는 미실행이다.
- 다음 착수: 사용자 「바로 작업 진행해」는 잔여 리팩토링의 계속 실행 지시다. 메인이 기공유 AI live provider2파일 안을 다음 묶음으로 선정하고 프로그래머 제안을2.5절로 구체화했다. 같은 Task를 사용하며 재승인 질문을 반복하지 않는다. 메인 검토 후 구현 신호를 전달했고 해당2파일 소스 정리·독립 정적 대조를 마쳤다. 컴파일·게임 실행은 미실행이다.
- 다음 착수: 사용자 「계속 진행해」의 잔여 작업 계속 실행 지시 아래 메인이 기공유 인증 계약안을 선정했다. 프로그래머 제안을2.6절로 구체화해 코드 전에 기록하며 메인 전체 검토·구현 신호 이후 소스 정리·독립 정적 대조를 마쳤으며 컴파일·Unity/실기는 미실행이다. 같은 task/승인 질문을 반복하지 않는다.
- 후속 착수: 사용자 「작업 이어서진행하자」의 계속 실행 지시 아래 메인이 AI 값 경계 분리(2.7)→UGS adapter 순서를 선정했다. 현재 브랜치는 `work`이며 「메인」은 조율 담당 에이전트다. AI 전문 제안을 코드 전에 기록하고 UGS 구체안은 별도 조사 후 확정한다.
- 순차 실행: AI2.7 소스/정적 대조·AI 규칙4/33 필수 구조 설명 동기화를 마쳤다. 메인이2.8 계획을 전체 검토하고 AI 검증 뒤 별도 구현 신호를 전달해 UGS 소스 정리·독립 정적 대조와 Auth 구조3행 동기화를 마쳤다. 컴파일·실기는 미실행이다.
- 구체적 코드 수정 계획: 2.3절 승인 범위로 game-programmer 구현 및 메인 독립 정적 대조 완료. 컴파일·실기·빌드는 미실행이다. 이후 별도 묶음은 구체적 파일·보존 계약을 공유하는 절차를 따른다.
- 이번 회차 쓰기 범위: 이 작업 폴더의 Research.md, Plan.md 및 2.3절의 기존 C#3개·신규 C#1개·신규 meta1개, 2.4절의 기존 C#2개 주석4곳·using1행, 2.5절의 AI/Bootstrap 기존 C#2개 및2.6절 인증 기존 C#3개·신규 C#2개·신규 meta2개. 2.7절 기존 AI/Bootstrap C#2개·신규 DTO/meta와 GameSystemRules_AI.md의 규칙4/33 구조 설명 동기화도 추가 범위다. 2.8절의 기존 Login/Bootstrap/Profile C#3개·신규 interface/adapter C#2개·meta2개도 순차 실행 범위다. 허용 상시문서는2.7의 AI규칙4/33과2.8의 AuthSystemRules 구조3행뿐이다. 이후2.9의 현행 TDD/agent/공용메모리 정확 버전·폴더 안내 정정과2.10의 UnitView 단일파일도 선정 범위다. 그 외 Status/Roadmap/다른 상시문서·메모리·기존 타 Task·Git 변경 작업은 범위 밖이며 기존 meta·자산은 변경하지 않는다.

## 1. 단계별 순서와 완료 기준

| 단계 | 작업 | 근거 | 완료 기준 |
|---|---|---|---|
| A · 목록 | 대상 경로를 전수 수집하고 내용 조사 상태 구분 | AGENTS 문서 인덱스, WORKFLOW 검수·수치 절차 | 코드310/Editor8/Python5/상시문서48/기존Task612/Log10의 실제 경로 장부와 제외 이유 존재 |
| B · 계약 | 기능별 규칙·기술·수치·현재상태·과거 결정의 원본 연결 | GameSystemRules 인덱스, 각 문서 머리말, WORKFLOW [11] | 한 행 한 항목 ID로 규칙↔구현↔근거 연결; 미확정·예외 분리 |
| C · 의존성 | Domain/Application부터 실제 타입 의존과 조합 루트 조사 | TDD 「의존성 방향 추상화」, 공용 메모리 「아키텍처 핵심 제약」 | 호출/구체 타입/조건부 경로/예외 결정 대조 후 첫 변경 묶음 확정 |
| D · 소규모 정리 | 보존 계약이 명확한 기능 한 묶음씩 책임·중복·가독성 개선 | 기능별 GameSystemRules 정확한 절/ID와 TDD | 파일별 변경/보존 사항·근거·확인 한계를 기록하고 사용자 승인 범위만 구현 |
| E · 사용 여부 | 삭제 후보의 C#·Unity 직렬화·문자열·Editor·과거 보류 확인 | WORKFLOW [4]/[5-2], UnusedAssetAudit, LogRules | 후보마다 사용 흔적·유지/보류/비활성화 판정과 해제 조건 기록 |
| F · 큰 책임 | 전투·네트워크·View의 상태 소유권과 생명주기 조사 후 분리 | Units/UnitCombatSynchronization/Network/MatchFlow 규칙 및 Network TDD | 기능별 작은 변경으로 나누고 서버 권위·표현 분리·이벤트 순서 보존 |
| G · 문서 반영 | 영향받는 상시문서 전부 검토, 현행 주장 제자리 갱신 | WORKFLOW [7]~[11], 각 문서 역할 | 현재 계약/상태와 과거 원문 구분; 기계검사와 의미 검토 별도 근거 |

A의 목록 확보는 완료했다. B/C는 일부 조사 중이며 D의 첫 묶음인 2.3절은 사용자 승인 후 구현·한정 정적 대조를 마쳤다. 실행 검증과 사용자 수용은 미확인이다. E~G는 미착수다. 향후 신규 파일은 장부에 증분 편입한다. 전체 파일을 목록에 넣은 사실을 내용 감사 완료로 보고하지 않는다.

## 2. 첫 조사 묶음: 계층 의존성 및 낮은 위험 변경 후보

### 2.1 조사할 파일과 이유

| 파일/범위 | 조사 내용 | 현재 판정·연결 |
|---|---|---|
| Domain/**/*.cs, Core/*.cs | Domain의 Unity/Core/외부 계층 참조와 실제 사용; 좌표값/상태 소유권 | namespace/using 검색만으로 위반 확정 금지 |
| Application/**/*.cs 및 Interfaces | 외부 서비스 구체 타입·Unity/NGO 직접 의존·이벤트 DTO의 책임 | 기본 Unity 타입 허용 예외를 TDD와 대조 |
| Application/UseCases/LoginUseCase.cs | 초기 구체 서비스/UGS 사용·헤더 상충을 조사했고 Plan2.6으로 서비스 계약 부분 해소 | AUD-001/AUD-015 · 서비스·오류 타입 정리/정적 대조 완료, UGS 잔여·컴파일/실기 미실행 |
| Application/UseCases/AccountLinkUseCase.cs | 서비스 구체 타입과 결과/예외 전파 | 동일 패턴 후보; 구현·기획 변경 확정 아님 |
| Application/Services/AIOpponentController.cs | Infrastructure import의 실제 타입 사용/역할 | 읽고 참조 그래프·관련 AI 규칙과 대조 |
| Bootstrap/LoginBootstrapper.cs 및 관련 GameBootstrapper partial | 실제 생성·주입·초기화/해제 순서 | 조합 루트 보존; 다른 곳으로 암묵적 수명 이전 금지 |
| Infrastructure 인증 서비스 및 관련 결과·예외 타입 | 추상화 시 노출 계약·동기/비동기·취소·소유권 | 소스/API 전체 확인 전 인터페이스 목록 확정 금지 |
| TDD, AuthSystemRules, AI 규칙, PROJECT_STATUS, ROADMAP와 관련 Task | 의도된 예외인지·과거 승인/수용과 보류가 무엇인지 | 현행 규칙에 코드만 맞춰 문서 변경하지 않음 |

### 2.2 첫 구현 묶음 확정 조건

1. 대상 기능과 관련 규칙의 정확한 조항을 직접 읽고 기재한다.
2. 수정/신규 파일, 호출자, 바뀌는 책임, 보존하는 공개 API/결과/수명/직렬화 연결을 명시한다.
3. 기본 Unity 타입 허용·인증/UGS 소유권·기존 수용을 구분한다.
4. 로그·동작 버그를 같이 발견하면 별도 ID로 분리하고 범위 확장 여부를 메인이 판단한다.
5. 수정 전후 확인 가능한 근거와 실행 불가능한 범위를 적는다.
6. 구체적 Plan 변경표를 사용자에게 공유한 후 승인된 범위만 game-programmer에 위임한다.

첫 구현 후보를 아래 2.3절로 구체화했다. 전체 아키텍처 변경·asmdef 도입·namespace 개명은 이 조사에 자동 포함되지 않는다.

### 2.3 첫 구현 승인 대상: IUnitView의 팩토리 의존 추상화

**문제와 결과:** 조사 시 Application의 IUnitView가 Infrastructure의 UnitFactory/BuildingFactory를 인자로 노출했다. 실제 View는 두 팩토리에서 ID로 GameObject를 조회하는 메서드만 사용한다. 같은 팩토리 객체를 유지하고 API의 타입만 Application 인터페이스로 바꿔 구체 계층 의존을 줄인다. 사용자 「바로진행하자」로 이 절의 구현이 승인됐고 구현·메인 독립 정적 대조를 마쳤다. 컴파일·실기·빌드는 미실행이며 상세 근거는7절에 기록한다.

**근거:** TDD 「의존성 방향 추상화 (Application 인터페이스 패턴)」 112~128행, 공용 메모리 「아키텍처 핵심 제약」의 Application 인터페이스에서 Infrastructure 구체 타입 반환/노출 금지. 게임 동작을 새로 정하는 작업이 아니며, GameSystemRules 인덱스에서 연결한 Units/UnitCombatSynchronization의 기존 타겟·표현·권위 계약을 변경하지 않는다. 상세 동작 규칙 수정은 계획하지 않는다.

아래 기존 소스 행번호는 **구현 전 행번호**다. 현재 소스 위치와 확인 결과는7절에 별도로 기록한다.

| 파일 | 승인한 변경 | 보존·확인 범위 |
|---|---|---|
| Assets/_Project/Scripts/Application/Interfaces/IUnitView.cs | 52~53행 구체 팩토리 인자를 IUnitFactory와 신규 IBuildingObjectProvider로 교체. 헤더15~20행과 인자 주석의 단계적 미완 설명을 실제 API에 맞춰 갱신 | Initialize/SetDependencies 호출 의미·인자 순서·기본 null 값 유지 |
| Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs | 신규 인터페이스. GameObject GetBuildingObject(int buildingId) 한 메서드와 한국어 설명 | 조회만 노출; 생성·파괴 API 추가 안 함. GameObject는 TDD가 허용하는 기본 Unity 타입 패턴 |
| Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs | 34행 선언에 IBuildingObjectProvider 구현 추가. 기존304행 조회 메서드 사용 | 사전 조회·반환 객체·미존재 null 동작 유지. 조회 본문 변경 계획 없음 |
| Assets/_Project/Scripts/Presentation/Unit/UnitView.cs | 205/208행 필드 및1686~1694행 SetDependencies 팩토리 인자 타입 교체 | 기존 객체 그대로 주입, 이벤트 구독·초기화 순서와 조회 본문 유지 |
| 신규 IBuildingObjectProvider.cs.meta | 기존 프로젝트 메타 형식과 중복 없는 새 GUID를 적용하는 안 | 기존 파일의 meta/GUID 변경 없음. Unity의 실제 import 확인은 별도 실행 근거 |

기존 C#3개 수정·신규 C#1개와 신규 meta1개를 구현했다. UnitFactory는 이미 IUnitFactory를 구현한다. UnitFactory.cs296/406행 호출은 같은 객체를 그대로 전달하므로 본문이 실제로 무변경임을 메인이 시작 SHA 대조로 확인했다. 실제 컴파일 전에는 호환성 PASS로 보고하지 않는다. 관련 호출자와 기존 IUnitFactory는 읽기 대조 범위다.

보존할 실제 동작은 UnitView1895/1911행 GetUnitObject, 1896/1912행 GetBuildingObject, Unity destroyed-object null 체크,1898~1901행 현재 position+forward 폴백 및1913행 null Transform 반환이다. MonoBehaviour인 팩토리의 파괴 상태 판정을 C# 인터페이스 null 비교로 바꾸는 위험이 있으므로 기존 조회 결과 GameObject에 대한 Unity null 체크를 유지한다. 팩토리 수명 및 조회 호출 방식의 동작은 별도 코드 검토 대상이다.

메인은 UnitView195~208행 해당 필드에 SerializeField가 없고, Assets의 unity/prefab/asset에서 `m_MethodName: SetDependencies` 검색0을 확인했다. 이는 직렬화 연결 위험을 좁힌 근거이며 모든 동적 호출이 없다는 증명은 아니다. Game.unity26436/26437행 같은 이름의 필드는 GameBootstrapper 컴포넌트의 것으로 View 필드와 구분했다. 피해·타겟 선택·HP·RPC·이벤트·씬·프리팹·config·기존 meta를 바꾸지 않는 범위로 구현한다.

확인은 수정 전후 diff로 타입/주석/구현 선언 외 동작 본문이 유지됐는지, 모든 SetDependencies 호출이 기존 객체를 전달하는지, 새 GUID가 중복되지 않는지, Application API에 해당 구체 Infrastructure 타입이 남지 않는지 대조한다. 현재 컴파일 도구가 확인되지 않았으므로 실제 Unity 컴파일·게임 실행은 미실행 한계로 남긴다. 정적 대조를 컴파일/기능 PASS로 부르지 않는다.

인증 추상화와 AI 의존 정리는 첫 구현 묶음에서 분리한다. 인증은 Infrastructure 정의 AuthException/AuthErrorReason 등 노출 계약까지 포함한 조사가 필요하고, AI는 과거 QA가 예외라고 서술한 기록과 현행 일반원칙을 먼저 정합화해야 한다. 죽은 코드 최종 삭제는 첫 묶음에 포함하지 않는다.

### 2.4 다음 구현 승인안: 타워 스탯 주석과 불필요 Core import 정리

**현재 상태:** 소스 정리·독립 정적 대조 완료, 컴파일·실기 미실행. 사용자 「다음 진행해」로 조사한 이 절의 구체 변경안을 공유했고, 사용자가 「이 범위로 구현 진행」으로 승인했다. 결과는7.3절에 기록한다. 첫 묶음의 사용자 실기 수용은 미확인이다.

**목적:** 현재 방어 타워가 쓰는 스탯을 미사용·미래 용도로 설명하던4주석을 바로잡고, 타입/확장 메서드 사용 근거가 없는 Core import1행을 정리했다. 기존 C#2파일만 수정했으며 신규 코드·meta·수치·동작 변경은 없다. 아래 표의 행번호는 구현 전 계획 기준이며 현재 소스 위치·대조 결과는7.3절로 구분한다.

| 파일 | 제안한 수정 | 근거·보존 사항 |
|---|---|---|
| Assets/_Project/Scripts/Domain/Building/BuildingStats.cs | 현재45/48행 StatValues 공격력/쿨다운 설명과159/170행 getter 설명4곳을 현행 타워 소비에 맞게 수정 | Buildings H2 「방어 타워 시스템」 규칙5 「공격 후 쿨다운」·규칙9 「서버 권위 처리」. TowerCombatUseCase145/231 실제 소비, Bootstrap.Setup155~182 건물값 주입. 필드·API·반환·폴백·수치·순서 무변경 |
| Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs | 현재4행 Hexiege.Core using1행만 제거 | TDD 「의존성 방향 추상화」, Research9.3 Core4타입 본문참조 미발견/확장정의0 전달근거. UnityEngine.Time33행과 모든 실제 동작 본문 유지 |

주석은 방어 타워가 종족별 공격력/쿨다운을 조회한다는 현재 역할을 설명한다. 공격력 getter가 HP를 직접 변경한다거나 Domain이 서버 권위를 판정한다고 적지 않는다. 서버 권위 실행은 TowerCombatUseCase의 책임이다. 수치 사본·성능 효과·새 규칙을 주석에 추가하지 않는다. 정확한 건물 주입은 Setup159/160,170/171,181/182행이며 유닛값83/87행을 근거로 사용하지 않는다.

삭제 범위는 불필요 using1행이다. 실행 로직 제거가 아니며 참조 타입과 Core 확장 메서드 부재를 근거로 한다. 기존 VFX6블록/Legacy/Shadow/다른 임시 도구 삭제는 포함하지 않는다. 해당 using1행 제거는 이번 명시 승인 범위이며 다른 using이나 코드는 정리하지 않는다.

승인 후 확인할 것은 BuildingStats의 문자열 리터럴 보존·주석/공백 제외 토큰 동일, TimedDamageEffectPresentation의 해당 using1행 외 토큰 동일, UnityEngine.Time 및 시계·상태·이벤트·로그 본문 무변경, 기존 meta/config/씬 무변경이다. C# 실행 환경이 없으면 실제 컴파일·게임 실행은 미실행으로 남기고 정적 대조를 기능 PASS로 보고하지 않는다. TC/QA·상시문서·메모리 갱신을 자동 착수하지 않는다.

### 2.5 다음 실행 묶음: AI 종족 조회를 live provider로 주입

**착수 근거와 상태:** 사용자 「바로 작업 진행해」는 남은 리팩토링 계속 실행 지시이며 AI2파일을 직접 이름 지정한 지시는 아니다. 메인이 Research9.5의 기공유 AI live provider 기존2파일 안을 다음 묶음으로 선정하고 프로그래머 구체 제안을 본 절에 기록했다. 코드 전 계획을 메인이 직접 읽어 검토한 뒤 구현 신호를 전달했고 소스 정리·독립 정적 대조를 마쳤다. task 작성/재승인 질문을 반복하지 않는다. 실제 구현 위치와 한계는7.5절에 기록하며 컴파일·게임 실행은 미실행이다.

**근거:** AI H2 「1. 개요」 규칙1/2(싱글플레이·Red 제어), H2 「10. 아키텍처 및 구현 규칙」 규칙28/29/30(Controller UseCase 주입·Config 위치·Bootstrap 초기화), TDD 「의존성 방향 추상화」 및 공용 메모리 계층 제약. TowerCombatUseCase의 종족 delegate 주입 관례와 맞춰 Application의 GameRaceContext 직접 사용 한 곳을 구성 루트로 옮긴다. 난이도 타입의 Infrastructure 의존은 남기며 전체 역참조 해소라고 보고하지 않는다.

| 기존 C# 파일 | 정확한 변경안 | 보존 계약 |
|---|---|---|
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | private readonly Func<RaceId> _aiRaceProvider 필드, ctor difficulty 뒤/optional upgrade 앞 필수 Func<RaceId> aiRaceProvider 추가. `_aiRaceProvider = aiRaceProvider ?? throw new ArgumentNullException(nameof(aiRaceProvider));` 대입. GameRaceForAi의 return을 `_aiRaceProvider()`로 교체 | 기존429/443/483/627행4사용점의 호출 시점 유지. 매 호출 live 값 조회; 생성 시 snapshot 금지. 새 provider null은 명시적 거부·종족 폴백 없음 |
| Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs | 구현 전833행의 유일 생성자 호출에서 difficulty 뒤 `() => GameRaceContext.RedRace,` 인자 추가. 기존 _unitUpgrade 인자 유지 | 정상 caller는 비null 함수 주입. AI 싱글플레이 가드·초기화 순서·시나리오 선택·수입 배율 설정 유지 |

행번호는 구현 전 기준이다. Controller 헤더20~23행에서 DifficultyParams와 AIScenarioConfig를 모두 SO로 설명하던 구절을 실제 DifficultyParams 값 구조체·Domain 시나리오 목록·종족 delegate 주입으로 현행화한다. GameRaceForAi의 직접 Infrastructure 참조를 허용한다고 설명하던 메서드 주석도 새 주입 방식에 맞춘다. DifficultyParams 때문에 Infrastructure using은 유지한다. 기존 예외 QA 기록은 지우지 않는다.

**새 null 계약:** provider를 null로 주면 ArgumentNullException으로 생성이 거부된다. 기존 유일 정상 호출자는 비null 람다를 넘긴다. 해당 필수 인자 추가와 null 거부는 이번 명시적 설계이며, 새로운 fallback을 도입하거나 provider 예외를 조용히 무시하지 않는다. `_aiRaceProvider` 대입은 기존 초기화/구독 전에 두고 기존 구독·실행 순서를 바꾸지 않는다.

**변경 범위와 확인:** 기존 C#2개만 수정하며 신규 파일·meta는 없다. 난이도9값·AI 시나리오·SO/자산·타이밍·이벤트·자원·생산·피해·네트워크·Auth는 변경하지 않는다. 코드 대조에서 허용된 필드/인자/대입/return/호출 인자와 관련 주석 외 토큰이 유지되는지, 유일 caller와 네 조회 사용점·live 람다·기존 upgrade 인자가 유지되는지 확인한다. Compiler·Unity 실행이 불가능하면 미실행으로 남기고 정적 대조를 실제 동작/사용자 수용으로 승격하지 않는다. 첫 두 묶음 실기 미확인·전체 감사 미완·상시문서/메모리/TC/QA 미변경을 유지한다.

### 2.6 다음 실행 묶음: 인증 서비스 계약과 공용 예외 소유권 정리

**착수 근거·상태:** 사용자 「계속 진행해」는 잔여 리팩토링의 계속 실행 지시다. 메인이 Research9.5에서 기공유한 인증 계약안을 다음 묶음으로 선정하고 전문 프로그래머 제안을 본 절에 반영했다. 사용자가 인증7파일을 직접 이름 지정한 것으로 기록하지 않는다. 코드 전에 구체안을 저장해 메인이 검토한 뒤 구현 신호를 전달한다. task 작성/재승인 질문을 반복하지 않는다. 코드 전 구체안을 메인이 전체 읽어 검토한 후 구현 신호를 전달했고 소스 정리·독립 정적 대조를 마쳤다. 컴파일·Unity/실기는 미실행이며 결과는7.7절에 기록한다.

**정의 이관:** 원본 enum/AuthException을 목적지에 먼저 보존·비교한 뒤 원위치 중복 정의를 옮긴다. 실행 로직 제거가 아니며 이전 namespace shim·주석 비활성 old class·dead block을 새로 남기지 않는다. enum 순서13개, Reason, 생성자 인자와 기본값, message/inner 전달 및 Exception 상속 body를 보존한다. 타입 소유 namespace가 Infrastructure→Application으로 달라져 FullName/ToString/스택 표시에는 변화가 있다. 이 표시까지 불변이라고 주장하지 않는다.

**근거:** TDD 「의존성 방향 추상화」와 공용 메모리 Application→Infrastructure 구체 노출 금지. AuthSystemRules H2 「오류 처리」 규칙1/2/3, 「UGS 연결」 규칙1~4, 「계정 연동」 규칙1~6, 「로그아웃」 규칙1~3 및 익명 재실행 보존 계약. 제품 동작 규칙을 새로 정하지 않고 서비스 소비·오류 타입의 소유권을 정리한다.

| 파일 | 구체 변경안 | 보존 사항 |
|---|---|---|
| Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs | 구현 전47행 class에 IAuthenticationService 구현 선언. 현재618행 enum/639행 예외 정의를 Application으로 이관 | 기존38행 Application using 활용. Firebase SDK 초기화·각 인증/연동/토큰/로그아웃·오류 변환 본문·로그 문자열·async 흐름 유지 |
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | _authService field/ctor를 IAuthenticationService로 교체하고 Infrastructure using 제거. 서비스/UGS 헤더 현행화 | 기존 concrete 객체 전달·동일 메서드 호출·catch/LastError·UGS 분기·경고/성공 처리 유지. UGS 직접 의존이 남음을 정확히 설명 |
| Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs | _authService field/ctor를 IAuthenticationService로 교체하고 Infrastructure using 제거. 관련 헤더 현행화 | Google/Email 연동·익명 상태·충돌 결과·오류 catch 흐름 유지 |
| Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs | 신규 서비스 소비 계약. 아래 고유18멤버와 한국어 설명 | 초기화 소유 API IsInitialized/InitializeAsync는 노출하지 않음 |
| Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs.meta | 신규 유일 GUID·프로젝트 기존 형식 | 기존 meta/GUID 변경 없음 |
| Assets/_Project/Scripts/Application/AuthException.cs | 신규 파일에 AuthErrorReason enum과 AuthException class를 Application namespace로 이관 | enum13순서·body 보존. 새 Auth 폴더 없음 |
| Assets/_Project/Scripts/Application/AuthException.cs.meta | 신규 유일 GUID·프로젝트 기존 형식 | 기존 meta/GUID 변경 없음 |

기존 C#3개+신규 C#2개+meta2개 총7파일을 구현했다. 본 절의 기존 소스 행번호는 구현 전 계획 근거이며 현재 위치는7.7절에 별도 기록한다. 다음6속성+12Task멤버 고유18개는 원래 서비스 public 시그니처와 일치함을 메인이 소스 대조했다.

```csharp
bool IsLoggedIn { get; }
bool IsAnonymous { get; }
bool IsEmailVerified { get; }
string FirebaseUID { get; }
string DisplayName { get; }
string Email { get; }
Task<string> SignInAnonymouslyAsync();
Task<string> SignInWithGoogleAsync();
Task<string> SignInWithEmailAsync(string email, string password);
Task SignUpWithEmailAsync(string email, string password, string displayName = null);
Task SendEmailVerificationAsync();
Task<bool> CheckEmailVerifiedAsync();
Task SendPasswordResetEmailAsync(string email);
Task LinkWithGoogleAsync();
Task LinkWithEmailAsync(string email, string password);
Task SignOutAsync();
Task DeleteCurrentUserAsync();
Task<string> GetIdTokenAsync(bool forceRefresh = false);
```

**호출자·표시 영향:** LoginBootstrapper207/208행과 ProfileView177/178행은 같은 FirebaseAuthService concrete 객체를 전달하므로 본문 무변경이 예상된다. 기존 예외/사유 소비9파일(App2/Infra1/LoginView6) 중 Views는 기존 Application using으로 이름을 해석할 수 있어 수정 불필요 예상이며 실제 확인 전 컴파일 PASS로 부르지 않는다. 기존 완전수식 참조/리플렉션/직렬화 검색 미발견은 검색 한계와 함께 기록한다. GameLog433행 GetType().Name의 AuthException 단축명은 유지되지만 ConsoleSink89~91행 LogException은 이관된 예외 객체를 받아 namespace 표시가 바뀔 수 있다. 로그 메시지 문자열·오류 Reason·message/inner 계약은 보존한다.

**확인·한계:** 타입 이관 전후 enum13순서·예외 body/리터럴을 비교하고 기존3파일은 승인한 class 선언/field/ctor/import/정의 이관과 주석 외 실행 토큰·문자열이 유지되는지 확인한다. 계약18멤버·optional 기본값·정상 caller의 동일 객체 전달·GUID 중복 여부·나머지 소비자 변경 여부를 대조한다. 초기화 소유권·sign-in/sign-out/token·UGS 세션·오류 변환/로그 문자열/async 순서·RPC·자산과 기존 사용자 수용을 바꾸지 않는다. UGS import/직접 호출은 남으며 인증 계층 전체 해소가 아니다. 컴파일·실기 미확인과 전체 감사 미완, 상시문서/메모리/TC/QA 미변경을 유지한다.

### 2.7 다음 실행 묶음: 직렬화 AI 설정과 Application 난이도 값 스냅샷 분리

**착수 근거·상태:** 사용자 「작업 이어서진행하자」는 남은 리팩토링의 계속 실행 지시다. 메인이 AI 값 분리→UGS adapter 순서를 선정했고 전문 프로그래머의 AI 구체 제안을 본 절에 기록했다. `work` 브랜치에서 진행하며 「메인」은 조율 담당 에이전트다. 코드 전 계획을 메인이 검토해 구현했고 소스/정적 대조와 규칙4/33 설명 동기화를 마쳤다. 재승인/task 질문을 반복하지 않는다. UGS는 별도2.8절로 구분한다.

**직렬화·소유권 보호:** Infrastructure/Config/AIConfig.cs의 [Serializable] DifficultyParams9float·easy/normal/hard·GetParams와 기존 SO/asset/meta는 그대로 둔다. Controller가 Infrastructure 타입을 모르도록 Bootstrap에서 선택된9값을 Application 불변 값으로 복사한다. 새 DTO는 값을 검증/보정하거나 난이도 기본값을 설정하지 않는다. 규칙5표·시나리오·타이밍·소비·자산은 변경하지 않는다.

**근거와 필수 문서 동기화:** AI H2 「2. 난이도 파라미터 (AIConfig ScriptableObject)」 규칙4/5, H2 「10. 아키텍처 및 구현 규칙」 규칙28/29/30/33/34 및 TDD 의존성 역전. 원래 사용자 목표인 문서·구현 대조의 필수 구조 동기화로 GameSystemRules_AI.md 규칙4/33의 직접 DifficultyParams 전달 설명만 snapshot 경계에 맞춘다. 해당 번호와 행동 요구사항을 유지하며 코드 후 메인 확인 지시를 받아 정확한 조항만 수정한다. 다른 상시문서·메모리는 변경하지 않는다.

| 파일 | 구체 변경안 | 보존·확인 사항 |
|---|---|---|
| Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs | 신규 public readonly struct·9public readonly float(lowerCamel 원래 이름/순서)와9필수 인자 ctor. 각 this.field=field 대입만 수행 | 순수 값 DTO, validation/clamp/default/Unity attributes 없음 |
| Assets/_Project/Scripts/Application/Models/AIDifficultyParameters.cs.meta | 신규 고유 GUID·프로젝트 메타 형식 | 기존 meta/GUID 무변경 |
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | 구현 전57행 field/161행 ctor의 타입을 AIDifficultyParameters로 변경, Infrastructure using 제거·헤더 현행화 | _params 기존7읽기 본문·순서 그대로. live 종족 provider·UseCase·이벤트·자원·생산/피해 로직 유지 |
| Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs | 구현 전833행 생성 직전9named args로 snapshot 생성, 생성자의 aiParams 인자만 difficultyParameters로 변경 | income830/log849 기존 aiParams 유지. 기존 provider/upgrade·초기화 가드·선택 난이도/시나리오 유지 |
| Assets/_Project/Docs/GameSystemRules/GameSystemRules_AI.md | 코드 후 규칙4 제목/본문과 규칙33의 전달 경계를 현행화 | 규칙4 ID 유지, 제목은 「파라미터 구조 (직렬화 설정과 Application 값 스냅샷)」. AIConfig3구조체 보존→GetParams 선택9float 복사→App DTO 주입을 정확히 설명. 규칙5표수치·다른 규칙/행동 요구사항 변경 없음 |

새9필드/생성자 인자 순서는 goldIncomeMultiplier, productionTimeMultiplier, decisionInterval, unitCountThreshold, goldExcessThreshold, buildOrderAccelCooldown, productionActivationCooldown, retryInterval, mineCheckInterval이다. Bootstrap의 `var difficultyParameters = new AIDifficultyParameters(...)`는 동일 이름 named argument로 `aiParams.<동일필드>`를 복사한다. snapshot은 기존 값 복사 의미를 보존하며 새로운 live 난이도 변경 정책을 만들지 않는다.

**미연결 항목 분리:** Controller는9값 중7필드를 읽고 수입 배율은 Bootstrap830행에서 소비한다. productionTimeMultiplier는 코드 전 전체 Scripts에서 AIConfig.cs48행 선언 외 소비 미발견이며 UnitProduction629/692는 기본 생산시간을 사용한다. 이 규칙5·구현 차이는 AUD-017 미해결로 기록하고 이번에 적용·삭제하지 않는다.9값 복사/보존은9동작 소비 증거가 아니다.

**확인:** DTO9float·순서·생성자 단순대입·Bootstrap named args1대1·기존7읽기·income/log/provider/upgrade를 대조하고 AIConfig.cs/실제 asset/meta/규칙5표 무변경을 확인한다. 기존2코드는 허용된 타입/import/header/snapshot 생성/인자 교체 외 동작 토큰·리터럴 유지, 문서 수정은 규칙4/33만인지 별도 diff로 확인한다. 컴파일·Unity/실기 미확인은 그대로 남기고 전체 의미 감사 완료나 생산 배율 연결 완료로 표현하지 않는다. TC/QA·Legacy 최종 삭제는 수행하지 않는다.

### 2.8 다음 순차 실행 묶음: Login의 UGS SDK 접근 adapter

**착수·상태:** 이전 사용자 계속 실행 지시 아래 메인이 AI2.7 뒤 UGS 잔여를 순차 실행하도록 선정했다. 전문 프로그래머의 구체안을 코드 전에 기록한다. AI 소스 검증 후 메인이 본 계획을 전체 검토하고 별도 구현 신호를 전달해 UGS 소스 정리·독립 정적 대조를 마쳤다. 결과는7.12절이며 컴파일·Unity/실기는 미실행이다. 사용자에게7파일을 직접 이름 지정받았다고 기록하지 않고 재승인/task 질문을 반복하지 않는다. 현재 `work` 브랜치이고 「메인」은 조율 담당 에이전트다.

**근거:** TDD 의존성 역전, AuthSystemRules H2 「오류 처리」 규칙3·「로그아웃」 규칙2·「UGS 연결」 규칙1~4. 원래 로그인/UGS 성공·실패·세션/자격 보존 계약은 바꾸지 않는다. 원래 사용자 목표의 문서·구현 동기화에 필요한 정확한 추가 범위로 AuthSystemRules.md의 「레이어별 구성」 Infrastructure/Application 표2행과 「Firebase ↔ UGS 연결 구조」 구현 위치 blockquote1행만 현행화한다. 새 계약·오류 소유권·adapter 실행 책임을 표시하고 해당2표행의 신규/수정이라는 과거 receipt 표시는 제거한다. 행동 규칙·코드 예시·다른 원문은 변경하지 않는다. AI 규칙4/33은2.7 별도 범위다.

| 파일 | 구체 변경안 | 보존·한계 |
|---|---|---|
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | readonly IUgsAuthenticationService _ugsAuthenticationService 필드. 기존 Firebase IAuthenticationService 뒤 필수 ugsAuthenticationService ctor 인자·null guard 추가. 기존 Firebase guard 먼저 유지. SDK using2개 제거·헤더 현행화. Bridge SDK 표현식7개 interface 치환, 명시 로그아웃 SDK 블록 이관 | IsAnonymous snapshot·UID guard·LastUgsBridgeSucceeded·토큰false·provider명·async/catch/로그·성공실패 정책 유지 |
| Assets/_Project/Scripts/Bootstrap/LoginBootstrapper.cs | 구현 전207행 `new LoginUseCase(_authService, new UgsAuthenticationService())` | 같은 Firebase 객체·다른 생성자/초기화 순서 유지. adapter 생성 자체 SDK 동작 없음 |
| Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Profile/ProfileView.cs | 구현 전177행 동일2인자 LoginUseCase 생성 | 같은 Firebase 객체·재초기화/프로필 흐름 유지 |
| Assets/_Project/Scripts/Application/Interfaces/IUgsAuthenticationService.cs 및 meta | 신규 interface8멤버와 고유 GUID meta | SDK 타입·세션 wrapper 노출 없음 |
| Assets/_Project/Scripts/Infrastructure/Auth/UgsAuthenticationService.cs 및 meta | 기존 폴더에 신규 stateless SDK adapter와 고유 GUID meta | 필드·명시적 ctor·cache·async wrapper·catch·fallback·log 없음 |
| Assets/_Project/Docs/AuthSystemRules.md | 코드7파일 외 필수 구조 동기화. 레이어 표의 Infrastructure에는 FirebaseAuthService/UgsAuthenticationService/UnityServicesInitializer, Application에는 LoginUseCase/AccountLinkUseCase/IAuthenticationService/IUgsAuthenticationService/AuthErrorReason/AuthException 표시. 구현 위치1행은 UseCase 조율→UGS 계약/adapter SDK 실행 및 Firebase 계약/구현체 token 발급 경로 설명 | 표2행+blockquote1행만 수정. 기존 인가·익명·세션/UI·UGS 입출력 규칙/코드 예시·다른 원문 유지. 현재 허용 상시문서는 AI규칙과 Auth규칙2개뿐이며 Status/Roadmap/메모리는 변경하지 않음 |

기존 C#3+신규 C#2+신규 meta2=정확7파일이다. 기존 meta는 변경하지 않는다. 계약8멤버는 다음과 같다.

```csharp
bool IsInitialized { get; }
bool IsSignedIn { get; }
string PlayerId { get; }
Task InitializeAsync();
void SignOut();
void SignOutCurrentSession(bool clearCredentials);
Task SignInAnonymouslyAsync();
Task SignInWithOpenIdConnectAsync(string providerName, string idToken);
```

**SDK 접근 보존:** IsInitialized는 UnityServices.State == ServicesInitializationState.Initialized이며 IsSignedIn/PlayerId는 매번 AuthenticationService.Instance를 새로 읽는다. InitializeAsync/두 SignIn은 원래 SDK Task를 그대로 return한다. parameterless SignOut은 AuthenticationService.Instance.SignOut() 기본 자격 보존 호출이다. Bridge의 조건은 !adapter.IsInitialized로 바꾸며 원래7표현식의 순서·SDK Instance getter 평가 횟수/호출 시점을 유지한다. cache·추가 세션 wrapper를 두지 않는다.

**명시 로그아웃 블록 이관:** SignOutCurrentSession에서 원래 Instance를1회 capture한다. clearCredentials && Instance null이면 동일 InvalidOperationException 메시지, Instance가 있고(clearCredentials || IsSignedIn)이면 SignOut(clearCredentials: clearCredentials) 호출이다. 구현 전397~405행 SDK 처리 블록만 새 adapter로 옮기며 관련 legacy comment도 보존한다. UseCase의 leavingAnonymous snapshot·try/catch·익명 실패 시 Firebase 선로그아웃 금지·실계정 실패 경고 후 계속 처리와 Firebase SignOutAsync 호출 순서를 유지한다. 실제 SDK 블록 이관이며 Legacy 최종 삭제나 새로운 자격 삭제 정책 추가가 아니다.

**대안 배제:** 기존 Network.UnityServicesInitializer는 자체 catch/log/콜백·세션 없는 경우 익명 fallback을 추가하므로 Login의 원래 SDK 초기화 직접 호출과 효과가 다르다. 이를 재사용하지 않고 정책 없는 adapter를 선택한다. SDK exception stack에 adapter frame/줄번호 차이가 있을 수 있으므로 기존 로그 문자열·실행 결과 계약 보존을 진단 문자열 전체 동일로 표현하지 않는다.

**확인:** 원래 Bridge의 SDK Instance 실행 접근5개 중 보간 PlayerId 접근은 일반 문자열 마스킹에서 누락될 수 있으므로 보간 표현식을 별도로 포함하고 알려진 실행 치환을 역복원해 비교한다. 원문 문자열 전체 무변경을 주장하지 않는다. 허용 field/ctor/guard/표현식 치환·SDK 블록 이관·caller 새 인자 외 기존 동작 토큰/비보간 리터럴 보존, SDK Instance 접근 횟수/시점·Task 그대로 반환·parameterless/clearCredentials 분리·익명/실계정 catch 정책·로그문자열·UIDguard·tokenfalse/provider·LastBool을 대조한다. interface8멤버와 실제 adapter가 일치하는지, 새 GUID2개 각각 고유·기존 meta/자산/다른 인증·네트워크 소스가 유지되는지 확인한다. exact SDK source 일부가 없으므로 실제 컴파일/Unity/실기 미실행을 명시한다. 상시문서/메모리/TC/QA와 기존 실기 수용을 자동 완료로 바꾸지 않는다.

### 2.9 다음 실행 묶음: 현행 Unity 버전과 폴더 안내 사실 정정

**현재 상태:** 아래 정확한3문서의 사실 정정과 문서 담당 before/diff 대조를 마쳤다. 결과는7.15절에 기록하며 실제 엔진 실행/코드 검증과 구분한다.

사용자 「이외에도 문서간의 충돌있으면 알려주고 이상한점있으면 알려줘 다음 작업들 이어서하자」의 계속 실행 지시 아래 메인이 다음 정확 범위를 선정했다. task/재승인 질문을 반복하지 않는다. 코드·기능·기획·수치·과거 이력을 바꾸지 않는 현행 문서 사실 정정이다.

| 대상 문서 | 정확 정정 범위 | 직접 근거·보존 |
|---|---|---|
| Assets/_Project/Docs/TechnicalDesignDocument.md | 구현 전41/1120행 Unity6000.0.x 두곳→6000.3.5f2 정확값. 폴더 안내104행 Diagnostics→현재 Scripts/Debug 역할, Bootstrap/Debug의 에디터 전용 맵 준비/투영 강제실패 플래그 표시 | ProjectSettings/ProjectVersion.txt1행 실제 버전. root Debug는 UIManager 테스트 핸들러이며 모든 debug 도구가 몰린 폴더로 설명하지 않음. Bootstrap partial×4는 실제 일치하므로 유지 |
| .claude/agents/document-manager.md | 28행 현행 엔진 버전1곳→6000.3.5f2 | NGO2.9.2/C# 및 다른 내용 유지 |
| .claude/MEMORY.md | 284행 현행 엔진 버전1곳→6000.3.5f2 | 다른 메모리/날짜별 이력·기준선은 변경하지 않음 |

버전은 저장소 설정 정확값만 쓰고 릴리스 마케팅/LTS 명칭을 추정해 추가하지 않는다. 루트 프로젝트 README는 현재 파일 목록에서 발견되지 않았고 외부 패키지 안내/옛Task/History를 일괄 치환하지 않는다. TDD 인증232행과 AI634~639행은 현재 추상화를 모두 설명하지 않지만 직접 SDK 호출/잘못된 타입 주입을 단정하는 모순 문장은 아니므로 이번에는 제안만 기록하고 본문을 추가 편집하지 않는다. AUD-002/011과 근거를 Research에 갱신한다. 이번 실행 문서3개와 현재 Task2파일 외에는 변경하지 않는다.

### 2.10 다음 코드 실행 묶음: UnitView 중복 조회 helper와 주석 현행화

**현재 상태:** 지정 단일파일 소스 정리와 메인의 원문 전체 역복원 대조를 마쳤다. 실제 컴파일·Unity/게임 실행은 미실행이며 결과는7.16절에 기록한다. 아래 행번호는 코드 전 계획 기준이다.

사용자 계속 실행 지시 아래 메인이 구체 범위를 공유했고 전문 프로그래머·메인의 원문 대조를 반영해 코드 전에 기록한다. work 브랜치이며 「메인」은 조율 담당 에이전트다. 코드1파일 Assets/_Project/Scripts/Presentation/Unit/UnitView.cs만 수정하고 신규 파일/meta/직렬화·캐시·자산을 추가하지 않는다.

- 구현 전46행의 SetDependencies가 구체 Infrastructure 타입을 받으며 interface에 없다는 오래된 주석1행을 현재 IUnitView 인터페이스 계약으로 의존성을 주입한다는 사실로 정정한다.
- 구현 전1892행 GetTargetWorldPos와1908행 GetTargetTransform에 중복된 `targetIsUnit ? _unitFactory?.GetUnitObject(targetId) : _buildingFactory?.GetBuildingObject(targetId)` 조회를 private GameObject GetTargetObject(int targetId, bool targetIsUnit) helper1개로 추출한다. 원래 두 메서드는 각각 helper를 한 번 호출한다.
- Unity GameObject `obj != null` 의미, world-position의 현재 position+forward 폴백, Transform의 null 반환과 호출 시점은 그대로 둔다. helper가 한 종류 factory만 한 번 조회하고 객체·transform을 캐시하지 않는다.

코드 전 WorldPos caller는8334행1곳, Transform caller는926/1343/1470/1944/1968/6483/7097/7958행8곳이다. 현재8634행 단일 class이며 factory GetObject는 TryGetValue 단순 반환이다. TDD 런타임 유닛 의존성 주입/인터페이스 패턴과 메인이 직접 읽은 권위/Root 경계 NET-AUTH-001/NET-ROOT-001/002를 유지한다. 타겟 선정·서버 권위·SimulationRoot/VisualRoot·이벤트·피해/애니메이션을 변경하지 않는다.

확인은 helper 호출을 기존2삼항 조회로 역복원하고 새 helper/해당 주석을 제외한 문자열 보존 실행 토큰이 원본과 동일한지 대조한다.9caller·getter당 조회1회·Unity null/폴백·meta GUID8b032e7de55b97a44aa94ce3b24f914b 유지 및 코드 전334C# snapshot에서 지정1파일만 변경하는지 확인한다. 실제 컴파일·Unity/실기 미실행을 정적 대조 PASS로 바꾸지 않는다. 생산시간 배율 AUD-017 후속 AI 보류와 기존 Legacy 삭제 보류도 유지한다.

### 2.11 자율 잔여 작업 인덱스와 최종 완료 기준

**승인·조율:** 사용자 「다 진행해 매번 내가 진행하라고 하는것도 불편한데」에 따라 당초 리팩토링/문서 대조 목표의 잔여를 계속 수행한다. task 여부/매 묶음 진행 승인 질문을 반복하지 않는다. 메인이 범위를 조율하고 각 전문 담당이 근거와 구체안을 전달한 뒤 구현 신호로 진행한다. work 브랜치이며 「메인」은 조율 에이전트다. 무제한 신규 기능·게임 정책·밸런스 변경 승인이 아니며 기존 보류를 해제하지 않는다.

| 후속 인덱스 | 영역·담당 | 수행 내용·완료 기준 |
|---|---|---|
| FULL-DA | Domain/Application · game_programmer_ugs | 모든 대상의 내용 읽기 범위·실제 타입/호출/규칙을 기록. namespace scan과 의미 검토를 구분하고 필요한 보존 정리안을 메인에게 전달 |
| FULL-UV | UnitView · game_programmer | 단일 대형 파일의 책임/호출·이벤트/수명·Root/타겟 계약을 실제 읽은 범위로 기록. 단순 줄 수 감소를 목표로 대량 분리하지 않음 |
| FULL-INF | Infra/Bootstrap/Core/Debug/Editor/Python · infra_audit | 런타임/편집기/스크립트 진입점·의존·직렬화·실제 사용/보류 조사. 도구 이름이나 참조0만으로 삭제하지 않음 |
| FULL-PRE | Presentation(UnitView 제외) · presentation_audit | UI/효과/입력/뷰의 상태·구독·생명주기·씬/프리팹 연결을 확인. lexical 후보와 실제 불일치를 구분 |
| FULL-DOC | 현행 Docs/관리 .claude · rules_audit→document_manager | 규칙·설계·수치·현재 상태·이력의 역할과 코드 대조. 확실한 사실 오류를 메인 공유 후 문서 담당이 수정; 승인되지 않은 동작 규칙 변경은 하지 않음 |
| FULL-MERGE | 메인/document_manager | 담당 JSON→영구 조사 장부 취합, 미조사/미완/보류 구분, 범위별 소스/문서 독립 대조·형식 검사·변경 목록 확인 |

임시 취합 자료는 /tmp/hexiege-full-{domain-app,presentation,infra,rules}-audit.json이다. 수신 시 경로 집합과 scope/read_ranges를 비교해 누락을 드러내고, 담당의 full reading/부분 열람/구조 검색을 그대로 구분해 Research16절에 남긴다. 임시 파일만 출처로 두지 않고 관련 원본 경로·판정·검토 위치·한계를 영구 기록한다. 기존 미조사 행을 일괄 완료로 바꾸지 않는다.

**정확한 정적 작업 끝조건:** (1) 시작+증분의 모든 대상 파일에 실제 검토 범위 또는 제외 이유가 있음, (2) 발견 문제마다 단일ID·근거·조치/유지/보류·남은 조건이 있음, (3) 확정된 기능 보존 정리는 메인/전문가 검토 후 적용·원본 대조 완료, (4) 확실한 현행 문서 사실 충돌은 정정하고 코드 변경에 필요한 문서 동기화 완료, (5) 문서검사/링크/변경목록을 확인하고 정적 완료와 실행 미확인을 분리해 보고. 전체 의미 읽기가 끝나지 않았다면 그 범위를 남기고 전체 감사 완료로 보고하지 않는다.

**유지할 보류/미실행:** AUD-017 생산시간 배율은 후속 AI 작업에서 구현한다.6VFX/Legacy 최종 삭제 보류, 기획/밸런스·저장/세션·서버 권위 정책을 이번 승인으로 바꾸지 않는다. Git mutation/native 게임 실행/TC·QA는 자동 수행하지 않는다. 컴파일/실기 미실행은 NOT RUN이며 과거 PASS/기존 사용자 수용을 신규 기능 보존 증거로 확대하지 않는다.

**과거 이력:** 관련 핵심 Task를 현재 규칙·API·근거에서 연결하고 정확한 열람 범위와 직접/전달 구분을 Research5/16.3 및 개별 장부에 유지한다. 전체612과거 Task·10Log 경로 확보를 전체 정독 완료로 부르지 않고 옛 Task/History를 소급 정합화하지 않는다.

**메모리 기준선:** 현재 읽기 전용 측정은 증가3폴더+50행/감소0이었다. `--update-baseline` 증가 전용 갱신 범위를 메인에게 먼저 보고했으며 실행 직전 도구 재측정에서 감소가 있으면 중단한다. 직접 JSON 편집/--reason으로 감소를 승인처럼 만들지 않는다. 메인 실행 신호 후 동일 증가+50/감소0 재측정과 도구 exit0 갱신·postfolders 일치 확인을 마쳤으며 직접 JSON 편집은 하지 않았다.

### 2.12 자율 후속: Infra/Core 설정 설명7파일 정리

사용자 전면 계속 승인 아래 메인이 Infra 전문 담당의 원문 대조안을 선정했다. 코드 전 이 구체안을 기록하고 메인 검토/구현 신호로 진행하며 재승인 질문을 반복하지 않는다. 기존7C#의 설명과 Inspector 속성 문자열8개만 정리한다. GameBootstrapper.OnDestroy는 별도2.16 범위로 분리한다.

| 파일 | 정확 변경 범위 | 근거·보존 |
|---|---|---|
| Core/HexMetricsCoordinateMapper.cs | 49행 X축만 반전한다는 설명→맵 중심 기준 XZ180도 반전·Y높이 유지. 다음 줄은 도메인 좌표로 다루는 기존 의미 보존 | ViewConverter.FromView 실제 변환; 메서드/수치/호출 무변경 |
| Infrastructure/Factories/UnitFactory.cs | 200행 네트워크는 도메인 좌표·싱글은 ViewConverter 적용이라고 설명,205행 Y높이는 반전하지 않으며 높이 오프셋을 적용한다고 설명 | 기존 삼항 분기·Y오프셋 연산·생성 순서 무변경 |
| Infrastructure/Config/BuildingStatsConfig.cs | Header2개의 향후 타워 설명→현재 방어 타워. Tooltip6개는 종족별 공격력이 피해 계산에,쿨다운이 공격 후 대기 시간에 사용된다고 현행화 | 기존8attribute 문자열만 허용 변경. 필드·직렬화명·수치·asset/meta·실행 본문 무변경; Buildings 방어타워 규칙5/9·Setup159/160·170/171·181/182 및 TowerUseCase145/231 근거 |
| Infrastructure/Config/AIScenarioConfig.cs | 23~25행 old Human_A/B/C 삭제 예정이라는3줄을 이전 단일 에셋 교체 이력과 현재 ScenarioBundle.steps 사용으로 정정 | 기존3종족 번들 자산·실제 schema·2026-06-10 Task Plan155~161 삭제완료 기록. 옛 Task/에셋은 변경하지 않음 |
| Infrastructure/Config/ToastMessageConfig.cs | 66행 못찾으면 빈메시지·1초라는 설명을 실제 키이름/표시시간1.5초 폴백으로 정정 | 담당이95~97행 실제 반환과 대조; 폴백 값/실행 본문은 변경하지 않음 |
| Infrastructure/Config/ResourcesMapFallbackTemplateSource.cs | 31~32행 Unity가 bytes만 TextAsset으로 읽는다는 과도한 일반화를 프로젝트 canonical 바이너리 .bytes 관리와 Resources.Load<TextAsset> 설명으로 정정 | 고정경로33행과 실제 파일 형식/로드/오류처리 유지; 새로운 Unity 일반 규칙을 단정하지 않음 |
| Infrastructure/Factories/BuildingFactory.cs | 18행 종족별3건물만이라는 설명을 각 종족 리스트의 건물타입별Blue/Red프리팹 연결로 정정.164행 Y반전 설명은 높이유지·변환후높이오프셋으로 정정 | 리스트/필드·자산/객체생성·좌표계와 연산 순서 무변경 |

수정 전 원문은 담당의 /tmp/hexiege-infra-comment-before.json에 확보했다. 추가3파일 경로도 저장소에서 확인했으며 이 묶음7개 외 GameBootstrapper 수명 정리는2.16으로 분리한다. metadata 문자열8개를 원래대로 역대응한 뒤 주석/공백 제외 실행 토큰·리터럴 동일을 확인하고, 다른 파일/기존meta/asset 변화가 없는지 메인이 대조한다. Inspector 표현은 달라지지만 게임 동작/밸런스를 바꾸지 않는다. 컴파일·실기는 미실행으로 구분하고 AUD-017/6VFX 삭제 보류를 유지한다.

### 2.13 자율 후속: Domain BuildingData/UnitData 현재 상태 설명

메인이 Domain/Application 전문 담당의 제안을 검토해 기존4C# 주석만 정리하도록 선정했다. API·필드·예외·ID카운터·수치·메타·runtime 문자열은 유지하고 주석을 포함한 실행 토큰 불일치를 기능 변경으로 숨기지 않는다.

- Domain/Building/BuildingData.cs 헤더8~12행의 현재 배치만/HP는향후 설명을 Id/Type/Team/Position·파생Stage·최대/현재HP·피해/회복·타워쿨다운의 현재 책임과 생산큐는별도ProductionState라는 설명으로 교체한다. Type XML의 Barracks 예시는 실제 TrainingCamp로 정정한다. 헤더 사용 예시는 실제4인자 `new BuildingData(BuildingType.TrainingCamp, TeamId.Blue, coord, maxHp)`로 정정하며 새 숫자를 발명하지 않는다.
- Domain/Unit/UnitData.cs 헤더8~10행의 프로토타입/전투미추가 설명을 식별자·위치·타입·팀·논리방향, HP/공격·감지사거리/이동속도/공격쿨다운/타격시점, 생성시 힐러여부·기본방어력의 현재 상태로 정정한다. Facing XML은 논리 HexDirection을 이동/전투경로가 갱신하며 화면 회전과 구분한다고 설명해 존재하지 않는 FromHexDirection/flipX 안내를 제거한다. Defense Phase1/2 역사 주석은 유지한다.

- Application/Combat/SpecialAttackRegistry.cs 헤더를 현재 BattleAxe/TorrentSpirit/MushroomBomber/InfernoSpirit/QuakeSpirit5등록으로 정정하고 BloomFairy는 힐러 전용 경로라 여기 등록하지 않는다고 명시한다. ctor끝 HealBehavior TODO/주석예시2행을 같은 역할 경계 설명으로 바꾸며 실제5등록과 피해숫자 주석은 유지한다.
- Application/Combat/ISpecialAttackBehavior.cs 헤더의 적 공격 전략에 힐을 포함하는 설명을 범위/파도/DoT 등으로 정정하고 BloomFairy는 별도 힐러경로(유닛 규칙32)임을 명시한다. 나머지 계약과 false/true 설명은 유지한다. 정확한 선언 경로를 저장소에서 확인했고 새 interface를 만들거나 계약을 바꾸지 않는다.

전문 담당과 메인이 Registry/TryGet·ISpecialAttackBehavior/Units23·32 및 ExecuteAttack1755~1789를 직접 읽어 기존 등록5/힐러 분리 경로를 대조했다. 코드 전 BuildingData/UnitData는 문서 담당도 직접 읽었다. 기존전체141 Domain/Application baseline에서 지정4개 주석 외 나머지137개 SHA동일, 대상4개는 문자열 보존/주석공백제외 실행 토큰 동일을 확인한다. 이는 문서화 사실 정리이며 실제 컴파일·게임 실행·실기 PASS가 아니다. 기존 동작 정책이나 생산시간 배율 후속AI보류를 바꾸지 않는다.

### 2.14 자율 후속: 현행 규칙·수치 설명의 사실 정정

메인이 직접 코드·규칙·기존 수용 원본을 대조해 승인한 정확9문서의 지정 본문만 정정한다. 2026-10-24 KST 계속 작업이며 과거 기록 날짜를 소급 변경하지 않는다. 코드·밸런스·자산·규칙ID·시나리오 고유 숫자·다기기 정책은 변경하지 않는다. 지정9문서의 수정 전 전체 원문/해시를 /tmp/hexiege-plan214-docs-before.json에 먼저 보존했다.

| 문서 | 정확 변경·근거 |
|---|---|
| StatsReference.md | 하드캡60~65%→65%(DamageCalculator MaxMitigationRate0.65f·Upgrade88/Units604). Garage 행 건설200/강화300→100/100(직렬화 BuildingStatsConfig.asset buildingType13의 humanGoldCost100/upgradeCost100). Config meta GUID d7b6bea86b98d354b8d19851830d2678과 Game.unity26430의 _buildingStatsConfig 연결, Setup148~188의 종족별 GoldCost/UpgradeCost 소비를 문서 담당도 직접 확인. 실제 구현 수치 미러의 정정이며 게임 값을 바꾸지 않음 |
| GameSystemRules_BuildingPanelsUI.md | MistShrine 규칙8의 지속 범위 VFX 인지·규칙13의 VFX 지속 인지 근거2문장만 Buildings의 「MistShrine 물안개 힐 시스템」 규칙26 신규 제작 필요와 제작 후 목표로 구분. 별도 지속 UI 없음·아군 패널이 열릴 때만 범위 UI 정책 유지 |
| GameSystemRules_AI.md | 규칙32의 DifficultyLevel을 Domain 소유로 표시하고 Current private set/Set(level) 실제 API 예시로 정정. 규칙35 직접 Current 대입 예시→Set 호출. 실제 LocalPlayerDifficulty/DifficultyLevel 선언 근거 |
| AI_Scenario_Human/Spirit/Transcendence 각 규칙 문서 | 공통 난이도 문장 각1개에서 delaySeconds/골드 수입만이라는 제한을 제거. 공통 AI 규칙의 난이도·종족 시나리오 공통 타이밍 예비 기준을 참조하고 생산시간 배율도 설계 범위로 표시. 생산시간 효과 AUD-017 후속 AI 보류/미구현을 명시하며 시나리오별 표·숫자는 유지 |
| GameSystemRules_Upgrade.md | 규칙13 라벨 항목의 전체 UI 자동생성 골격/전체 다듬기 보류라는 현재 주장을 PROJECT_STATUS 및 2026-10-19/15_41_ui-layout-audit Research의 사용자 수용 범위 참조로 정정. 매트릭스 헤더 아이콘 보류를 유지하고 전체 기기 수용으로 확대하지 않음 |
| GameSystemRules_Sound.md | 규칙6 미결 Splash 노트→현재 Login 내부 SplashOverlay/Login BGM, 별도 Splash 씬은 향후 별도 설계. 규칙17 Debug.LogWarning→AudioManager267~279의 GameLog.Dev.Warn 개발 경고 |
| AuthSystemRules.md | 「세션 및 다기기 동작」 규칙1 두문장만 Firebase Refresh Token의 장기 세션/삭제·비활성화·중요 계정 변경·서버 폐기 무효화 가능성과 ID token 약1시간/SDK 갱신으로 정정하고 공식 manage-sessions 출처 링크 추가. 메인이 https://firebase.google.com/docs/auth/admin/manage-sessions 공식 원문을 독립 취득(/tmp/hexiege-firebase-manage-sessions.html)해30일 비활성 만료 주장과 상충을 확인. 서버30일 정책을 새로 만들거나 규칙2 등 다른 정책을 바꾸지 않음 |

허용 문구별 exact 치환 결과가 현재9파일 전체 원문과 같은지 비교하고 정확 diff를 보존한다. Status/Roadmap/추가 메모리/옛 Task·History는 수정하지 않는다. 실제 compile/Unity/native/실기는 NOT RUN이며 구조 스캔과 직접 의미 읽기 범위, 전체 감사 미완, AUD-017 후속 AI 보류와6VFX 삭제 보류를 유지한다.

### 2.15 자율 후속: VFX 설명·XML 소유 메서드와 불필요 import 정리

메인이 Presentation 전문 담당의 정확 범위를 검토해 선정했다. 기존6C#만 수정하며 새 기능/오디오 정책·VFX 보호블록·필드/수치/asset/meta는 바꾸지 않는다. Sound 규칙1/4/15의 VFX=EffectManager, SFX=AudioManager 분리와 실제 구현에 설명을 맞춘다.

| 파일 | 허용 변경 |
|---|---|
| Presentation/Effects/EffectManager.cs | 활성 IEnumerator/Coroutine가 없고 legacy 주석만 쓰는 System.Collections using1행 제거. 주석6곳의 Coroutine/AudioSource·VFX/SFX통합·SFX pool초기화·UI주로SFX·SFX동시재생 설명을 현재 VFX 전담/Config Dictionary/UI VFX 원점identity 재생으로 정정. 헤더24행 SFX8개정책·legacy SFX블록은 이번 수정에서 제외 |
| Presentation/Effects/EffectPreset.cs | 헤더의 VFX/SFX묶음·한에셋 동시재생 설명을 VFX프리팹/transform설정과 SoundConfig/AudioManager 별도오디오 호출로 정정 |
| Presentation/Effects/UiEffectConfig.cs | 헤더의 주로SFX→VFX. Tooltip1개는 UI VFX preset과AudioManager 별도오디오 설명으로 정정 |
| Presentation/Effects/UnitEffectConfig.cs | Tooltip2개의VFX+SFX→VFX. Initialize 설명 XML블록을 실제 Initialize 앞으로 이동, GetTimedDamage/GetImpact에 실제 서버상태이펙트/착탄 cache조회·미존재null 요약각1줄 추가 |
| Presentation/Effects/BuildingEffectConfig.cs | Tooltip2개의VFX+SFX→VFX |
| Presentation/UI/ViewModels/RaceSelectionViewModel.cs | 담당이 전체읽은 결과 사용타입이System/UniRx/Domain/Infra뿐인 UnityEngine using1행 제거 |

허용 Tooltip5문자열은 Inspector metadata라 변경된다. 허용5문자열을 원문으로 역복원한 뒤 주석/공백 제외 실행 토큰·나머지리터럴 동일을 확인하며 모든 runtime 문자열이 불변이라고 확대하지 않는다. XML 이동은 설명 소유 메서드만 정정하고 메서드 body·serialized 필드는 유지한다. legacy SFX/기존6보호블록은 보존하고 실제compile/native/실기는 미실행으로 구분한다. 메인 계획 검토/신호 후 전문 담당이 구현한다.

### 2.16 자율 후속: Bootstrap 파괴 시 eager 재경로 구독 해제

메인이 GameBootstrapper.OnDestroy 전체와 모든 _eagerRepathSubscriptions 참조,Network의2개GameEvents 구독/Composite AddTo·Map.ClearAll의 기존Dispose/null을 직접 읽어 파괴 경로의 누락을 확인했다. 전문 담당은 메인 계획 검토/신호 후 기존 Bootstrap/GameBootstrapper.cs1파일만 수정한다.

OnDestroy 시작에 이유 설명 주석과 `_eagerRepathSubscriptions?.Dispose();` 다음 `_eagerRepathSubscriptions = null;`을 추가한다. 기존 `_unitCombat?.CloseTorrentLaunches("bootstrap-destroy");`와 `GameServicesLocator.Unregister();`의 상대 순서를 그대로 둔다. Map.ClearAll의 기존 정리·Network 재구독 흐름·다른 UseCase Dispose·파일/세션로그 정리는 변경하지 않는다.

이 묶음은 주석만 변경이 아니라 파괴된Bootstrap에 남는2이벤트 구독의 소유 수명을 닫는 변경이다. 새게임동작 정책·피해/타겟/경로규칙을 추가하지 않는다. 실제 runtime 누수 재현은 아직 없으며 원문참조로 누락을 확인한 예방 정리다. 추가2문장·관련주석 외 원문/호출순서·기존meta·다른파일 무변경을 대조하며 actualcompile/native/실기 NOT RUN을 유지한다. 기존6VFX/Legacy최종삭제와 AUD017 AI생산배율보류는 유지한다.

### 2.17 자율 후속: UnitView 메서드 partial3분리와 기존 source gate 범위 보존

사용자 전면 계속 승인 아래 메인이 전문 담당의 기계적 분리안을 직접 검토해 선정했다. 코드 전 구체안을 기록하며 별도 사용자 재승인 질문을 반복하지 않는다. 기존 UnitView/Editor validator2C#+새 partial3C#/meta3=정확8파일이다. 신규 기능·상태·호출/이벤트·코루틴 소유권을 추가하지 않는다.

| 대상 | 구체 변경·보존 |
|---|---|
| Presentation/Unit/UnitView.cs | 원본8641행 중75메서드/4860행을 아래3파일로 원문 이동. class에partial 수식어만 추가하고 모든 필드/initializer/nested type/6SerializeField/4Unity callback·Initialize/DI/attack gate·기존 MonoScriptGUID/namespace/base/interfaces는 원파일 유지. 예상 원파일 약3781행은 계획치이며 최종파일을 재측정 |
| 새 UnitView.MovementAuthority.cs 및 meta | 권위/회복 관측 메서드31개·원문구간1404행 이동. 필드/초기화는 원파일 유지 |
| 새 UnitView.Movement.cs 및 meta | 이동 메서드26개·원문구간2801행 이동 |
| 새 UnitView.Healing.cs 및 meta | 힐 메서드18개·원문구간655행 이동 |
| Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs | private static string ReadUnitViewSourceForValidation(string unitViewPath) 추가. main File.ReadAllText로StringBuilder 초기화→같은폴더 UnitView.*.cs Directory.GetFiles→Array.Sort(StringComparer.Ordinal)→각 AppendLine/Append(File.ReadAllText)→ToString. 기존 main직독3곳(코드 전2986/7578/7708)와동적inventory779의 UnitView키1곳만 helper로 교체; 다른키는 기존File.ReadAllText 유지 |

실제 partial구간·75signature·해시·callback/필드와 동반소스 검토 범위는 /tmp/hexiege-unitview-partial-proposal.json에 확보했다. 그 최초proposal의 source_gate_followup에 후속과제로 적었던 부분을 메인 최종안에서는 이번8파일에 포함한다. 기존4sourcegate의 Require/Contains/금지API/key와 다른소스 판정은 유지하고 검사 범위를 분리된 동일class 전체로 보존한다. 새로운 TC/QA·메뉴 실행은 하지 않는다.

검증은 이동구간의 원문·순서를 다시 삽입하고 원파일 partial 수식어를 되돌렸을 때 full text 원문동일,75method rawbody동일,필드/initializer순서·6직렬화필드·4callback·Unity가드/brace균형·호출 수·기존 GUID 보존으로 한다. 새 meta3GUID고유/Editor helper의정렬/4진입점/기존 gate조건무변경도 대조한다. partial파일로 source path·줄번호·예외 stack 위치가 바뀔 수 있으므로 진단 위치 불변을 주장하지 않는다. 실제 compile/Unity/native/gameplay는 NOT RUN 경계를 유지한다.6VFX 보호블록은 삭제하지 않고 생산배율 후속 AI 보류를 해제하지 않는다.

### 2.18 자율 후속: 유닛 marker·기존 VFX 수용·Application 책임 설명 정정

메인 검토·실행 신호 아래 정확3문서의 지정 문구만 정정한다. 소스·meta·에셋·밸런스·새 Unity 허용 정책은 변경하지 않는다.

| 대상 | 근거와 구체 변경·보존 |
|---|---|
| GameSystemRules/GameSystemRules_Units.md 규칙42 | InfernoSpirit_Attack.anim93390~93391은1.5초 OnAttackHit1개이고 StatsReference71도1.5다. 잘못 복제된1초 주장을 클립·주기·타격 marker는 StatsReference/감사표 현행 계약 참조로 교체. Mushroom545의1초 문단과 DoT/권위/수명 계약은 보존 |
| Assets/UnitCombatAssetMatrix.md MushroomBomber행 | 실제 Attack.anim71483~71485의1초 OnAttackHit1개로 낡은0개/근거대기 정정. 기존2026-10-13/13_53_vfx-code-followups Research259/278~279의 impact·새 poison prefab 사용자 승인만 반영하고 출처 연결. tracer·공통 시간 경계·권위 착탄/ImpactHitRadius·전체 lifecycle·migration OPEN 유지 |
| TechnicalDesignDocument.md Clean Architecture 그림 | Application의 '(순수 C# + UniRx)'를 '(UseCase·계약)' 책임 설명으로 정정. 기존 예외의 추가·확대나 Domain의 Unity 독립 계약 변경 없음 |

실행 전3전체원문/SHA를 임시 보존하고 정확 치환 예상치와 실제 전체문서 대조·diff·check_docs를 확인한다. 기존 수용을 새 리비전 전체 수용으로 확대하지 않는다.

### 2.19 자율 후속: 생산 UI 초기화·입력·Rx 수명 정리3파일

코드 전 전문 lifecycle 담당의 최종 구체안을 기록한다. 근거는 BuildingPanelsUI 「생산 패널 규칙」1~5의 열기/닫기·탭/롱프레스와 기존 수명 소유권이다. 아래3C# 외 직렬화 에셋·생산 수치·다른 UI 동작은 변경하지 않으며 메인 GO 뒤 코드 담당이 구현한다.

- Presentation/UI/ProductionPanelUI.cs: 서비스 참조는 Initialize마다 갱신. `_uiBindingsInitialized` guard로 rally/upgrade·유닛 EventTrigger·queue 버튼·전역2구독은 컴포넌트 수명1회만 연결한다. 기존 버튼/리스트의 런타임 재대입이 없다는 것은 전문 담당 Scripts/Editor 검색 결과다. ResetUnitPress를 Initialize/OnShow/OnBeforeClose와 신규 private OnDisable에 배치한다. PointerDown/Up·Update·LongPress에는 isActiveAndEnabled/IsOpen/현재 건물 non-null 및 IsAlive(Hp>0) guard를 적용한다. 비활성화 중 Update가 실행되지 않아 남는 눌림도 취소한다.
- Presentation/UI/BuildingPanelBase.cs: InitializeBase의 자체 Close/OnDemolishButtonClick listener만 RemoveListener→AddListener로 중복 방지한다. 다른 담당 listener와 기존 Rx 수명 연결은 유지한다.
- Presentation/Production/ProductionTicker.cs: `_buildingChangeSubs`를 실제 전체 구독 책임에 맞는 readonly `_eventSubscriptions` CompositeDisposable로 정정한다. `_eventSubscriptionsBoundToLifetime` guard로 Composite 자체 AddTo(this)는 컴포넌트 수명1회만 연결하고 SubscribeEvents 시작에서 Clear() 후 기존8구독 모두 AddTo(_eventSubscriptions)로 재바인딩한다. 기존 OnDestroy의 Dispose는 유지하고 readonly 필드 null 대입은 제거한다. 재초기화 시 wrapper 누적을 막고 never-active object의 기존 Rx 파괴 수명도 보존한다.

기존 필드·직렬화·숫자·정상 입력/생산/랠리 정책을 보존한 diff와 허용 변경 대조를 남긴다. 실제 compiler/Unity/native/실기·TC/QA는 NOT RUN이며 재현 없는 중복등록·stale 입력 위험에 대한 예방 정리와 실제 검증을 구분한다.

### 2.20 자율 후속: 결과 화면 도달 확인 뒤 최신 침묵 재검사1파일

전문 담당 제안의 Infrastructure/Network/NetworkGameEndController.cs1파일만 대상이다. MatchFlow 규칙17/Network 최상위 규칙에 따라 Reachable 응답을 기다리는 동안 들어온 정상 상대 신호를 무시하고 연결을 끊지 않도록, 결과 뒤 spawn/listening/leave flags(_resultScreenLeaveJudged/_opponentLeftJudged)를 재검사한다. 최신 Time.realtimeSinceStartup-_lastOpponentSignalRealtime를 다시 계산해30초 이상일 때만 ConfirmOpponentLeftAfterSilence에 전달한다. 회복 신호가 있으면 기존 감시를 계속한다.

tick1/HB3/retry10/timeout30·확인 전 인터넷 도달 순서·로그 정책·승패/재경기 규칙을 유지한다. 코드 전에 기록하고 메인 GO 뒤 구현하며 실제 race 재현/컴파일/Unity/실기는 미실행 경계를 유지한다.

### 2.21 자율 후속: 강제 최초 경기 실패 안내 상수 정정1파일

Assets/Editor/Debug/ForceRematchMapFailureMenu.cs465~471의 FirstRoundExpectation1상수만 대상으로 전문 코드 담당이 메인 GO 뒤 정정한다. MatchFlowUI M-2/Network 최상위 원칙에 맞춰 양쪽 loading 종료·연결/방 즉시 정리·로비 메인 복귀와 동일 알림 모달(제목 알림/본문 경기를 시작할 수 없습니다./확인1개, 확인은 모달만 닫음·배경탭 닫힘 불가), 재시도 없음/새 방 필요를 안내한다. 낡은 Host 재시도/Client 화면 유지 설명만 고치고 실제 실패 트리거·메뉴·예약·시간/설정은 변경하지 않는다. 이 안내 정정을 실패 주입 실행·새 TC/QA 완료로 판정하지 않는다.

## 3. 기능별 조사 분할과 보존할 동작

| 기능 묶음 | 관련 원본 | 보존해야 할 경계 |
|---|---|---|
| 인증·게스트·프로필 | AuthSystemRules, SettingsUI, 관련 수용 Task | Firebase/UGS 정체성 구분, 재실행과 명시 로그아웃 차이, 비동기 소유권 |
| 맵·경기 준비·재경기 | Map/RandomMap/MatchFlow/MatchFlowUI, Network TDD | canonical 형식·해시·seed·팀 관점·실패 복구·씬 준비 gate |
| 이동·타겟·공격·피해 | Units/UnitCombatSynchronization, UnitCombatAssetMatrix | 서버 단일 권위·회차/타격 식별·HP/사망 순서·Legacy rollback |
| VFX·SFX·표현 | Buildings 방어타워, Sound, VFXSFXList/Guide | 피해와 표현 분리·marker 시점·소스/대상 수명·기존 삭제 보류 |
| 건설·생산·연구·스킬 | Buildings/BuildingPanelsUI/Upgrade/Skills, StatsReference | 자원·큐·취소·대상·쿨다운·직렬화된 실제 수치 |
| UI·입력·팝업 | UI/CanvasSortingOrder/SettingsUI/MatchFlowUI, UIGuidelines | foreground modal·입력 차단 소유권·전환 시 gesture 정리·실제 RectTransform 값 |
| AI | AI 공통·종족 시나리오·ROADMAP | 수용된 핵심 동작 유지; 밸런스/시나리오 재설계 보류 |
| 도구·로그 | WORKFLOW [5-2], LogRules, 기존 Editor 정리 Task | 필요한 진입점/내부 의존·진단 보존·원시 로그 보호; Setup 재실행 금지 |

이 표는 조사 라우팅이다. 아직 개별 규칙 번호를 특정하지 않은 기능의 구현 계획으로 사용하지 않는다. 구현 단위마다 정확한 절·ID를 추가한다.

## 4. 죽은 코드 판정 절차

- 먼저 후보의 정의·호출·이벤트 발행/구독·등록/해제·partial 공유 상태를 확인한다.
- `.meta` GUID를 씬·프리팹·asset·controller·clip에서 찾고 UnityEvent/AnimationEvent/SendMessage/문자열·reflection 경로를 확인한다.
- Resources/StreamingAssets·조건부 컴파일·Editor 메뉴·빌드/진단 도구·직접 의존을 확인한다.
- 기존 Task/ROADMAP의 삭제 유예와 사용자 수용 조건을 확인한다.
- 유지/보류/비활성화 후보를 구분하고 안전 근거·영향 범위·복귀 조건을 Plan 최상단과 항목에 적는다.
- 최종 삭제는 이 문서 서두의 승인·사용자 테스트 조건을 충족한 범위에서 진행한다.

에셋 미참조와 코드 미사용은 다른 판정이다. 과거 UnusedAssetAudit 숫자는 현재 참조 상태의 근거로 재사용하지 않는다. 신규 검사 스크립트는 필요성·범위 승인 없이 저장소에 추가하지 않는다.

## 5. 확인 결과를 기록하는 방식

| 확인 종류 | 기록할 것 | 확대 해석 금지 |
|---|---|---|
| 목록/검색 | 경로·패턴·검색 범위·내용 열람 범위 | 검색0=미사용 확정 아님 |
| 코드 대조 | 호출 경로·예외·불변 계약·에셋 값 | 코드 존재=실행 성공 아님 |
| 컴파일 | 실제 명령·리비전·조건부 설정·결과 | 생성 DLL 존재=최신 컴파일 완료 아님 |
| Unity/기기/멀티 | 실제 수행 범위·역할·결과 | 과거 PASS/부분 수용=새 전체 PASS 아님 |
| 문서 검사 | check_docs 실행 범위·진단 | 문제0=구현 의미 일치 아님; 신규 Task는 제외 |

TC/QA는 현재 요청 범위에 포함하지 않는다. 사용자가 명시적으로 요청할 때 WORKFLOW의 해당 절차를 따른다. 일반 문서 정합성·경로·장부 확인은 이번 문서 작성의 마무리 작업으로 수행한다.

## 6. 위험과 대응

| 위험 | 대응 |
|---|---|
| 문서가 코드보다 오래됐거나 의도된 예외가 누락됨 | 규칙·예외 결정·실제 구현을 같이 읽고 상충을 드러냄 |
| 파일 분리로 초기화/구독 해제/상태 소유권이 바뀜 | 호출·생명주기 도표와 보존 항목을 먼저 적고 작은 단위로 적용 |
| 대형 파일 줄 수 감소만 목표가 됨 | 책임 분리·의존 방향·변경 국소성·중복/부수효과 감소로 판단 |
| 직렬화 필드/클래스·meta GUID 변경으로 연결 손실 | 기존 이름·GUID·namespace·partial·씬 연결 보존; 변경 필요 시 별도 계획 |
| 보류 Legacy/진단이 죽은 코드로 오인됨 | Task/ROADMAP 삭제 유예와 현재 실제 의존 확인 |
| 기존 수용을 불필요하게 재개하거나 미관측을 PASS로 승격 | 기존 범위 유지, 새 변경 축만 기록 |
| 문서 장부가 또 다른 사본 규칙이 됨 | 원본 링크와 판정만 기록하고 규칙 본문·상태 영수증 중복 최소화 |

## 7. 실행 기록

| 단계 | 상태 | 근거/다음 작업 |
|---|---|---|
| A | 목록 확보 완료 | Research 1절·7절; 문서 담당 독립 집합 대조 |
| B | 일부 조사 | Research 원본 역할·초기 차이 장부; 전체 의미 대조 미완 |
| C | 초기 조사·첫 변경안 작성 | 메인/game-programmer read-only 조사 결과와 2.3절; 전체 감사는 미완 |
| D 첫 묶음 | 구현·정적 대조 완료 / 컴파일·실기 미실행 | 사용자 「바로진행하자」로 2.3절 승인; 아래 구현 결과. 전체 감사/사용자 수용 완료 아님 |
| E~G | 미착수 | 삭제·전체 감사·실기 수용 완료로 확대하지 않음 |

첫 코드 묶음의 구현 결과와 확인 한계는 아래7.1절에 기록했다. 다음 보고는 전체 리팩토링 완료가 아니라 단계2 조사 결과와2.4절의 다음 구체 승인안을 다룬다. 구현 이후 변경 파일 목록은 코드·에셋·상시문서·작업문서를 구분하고 git diff/작업 이력과 대조한다. 최초 문서 작성 단계의 추가 파일은 Research.md와 Plan.md 두 개이며, 첫 구현 승인으로 2.3절의 코드·신규 meta가 추가 변경 범위가 됐다. 실제 생성·수정 파일과 확인 결과는 아래에 기록했다.

### 7.1 첫 묶음 구현 결과와 확인 한계(2026-10-23)

| 실제 변경 파일 | 구현 결과·현재 소스 위치 |
|---|---|
| Assets/_Project/Scripts/Application/Interfaces/IUnitView.cs | 51~52행 인자를 IUnitFactory/IBuildingObjectProvider로 교체하고 관련 헤더·인자 주석 현행화 |
| Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs | 34행 IBuildingObjectProvider 구현 선언 추가. 기존 조회 메서드 본문 무변경 |
| Assets/_Project/Scripts/Presentation/Unit/UnitView.cs | 205/208행 필드와1688행 인자 타입 교체. 기존 조회·폴백·이벤트·초기화 본문 무변경 |
| Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs | 신규14행 인터페이스와22행 GetBuildingObject(int) 한 메서드 |
| Assets/_Project/Scripts/Application/Interfaces/IBuildingObjectProvider.cs.meta | 신규 GUID 8c3cd82964bd4e939d5d7d036a2ec75d. Assets 전체 meta 검색에서 자기 자신1건 |

메인이 diff 원문과 시작 snapshot 원본 SHA를 확인한 뒤 문자열 리터럴 보존·주석/공백 제외 토큰 대조를 수행했다. 기존 변경3파일에서 승인된 타입 교체와 구현 선언 외 토큰은 동일했고, 시작 C#328개 중 변경3/무변경325를 확인했다. UnitFactory.cs 무변경, 조회 null 전파와 Unity GameObject null 체크, position+forward 폴백, null Transform 반환, 이벤트 구독·초기화 본문 보존도 확인했다. `git diff --check`는0이다. 이 결과는 메인 전달 근거이며 문서 담당은 주요 수정 위치와 신규 파일을 별도 직접 읽었다.

프로젝트 C#은 시작310+신규1=현재311개이며 Task는 기존612+이번 문서2=614개다. Research7.1 증분 장부에서 신규 파일을 추적한다. 기존310개 목록과 구현 전 검사 결과를 지우거나 최종현재 상태로 바꾸지 않는다.

Unity/C# 실행 도구가 확인되지 않아 컴파일·Unity import·기기/멀티·빌드는 실행하지 않았다. 소스 정적 대조를 기능 PASS나 사용자 테스트 완료로 보고하지 않는다. 전체 의존성 감사·죽은 코드 삭제·상시문서·메모리 갱신은 완료하지 않았으며 기존 VFX 삭제 보류도 유지한다. 메인 최종 검사에서 Research 로컬1075링크/Plan1링크 오류0, 시작 장부 모든 범위 누락0, `python3 Tools/check_docs.py` exit0·문제0, `git diff --check`0과 예상7파일 외 변경 없음을 확인했다.

### 7.2 다음 조사 진행(2026-10-23)

사용자 「다음 진행해」 이후 Domain51/Application86의 지정 namespace 텍스트 검색 결과를 Research9절에 기록했다. Domain hit0/Application4파일6행은 한정 검색 결과이며 전체 의존성 PASS가 아니다. 당시 다음 구현 후보는2.4절의 기존2파일 주석4곳·불필요 using1행이었다. 이후 사용자 「이 범위로 구현 진행」으로 명시 승인됐고 소스 정리·정적 대조를 마쳤다. 상세 결과는7.3절에 기록한다. AI/Auth는 별도 조사 결과를 기다린다. 기존 조사·검사 기록은 각 회차의 범위로 보존한다.

AI/Auth 후속 제안은 Research9.5에 조사 근거와 남는 의존성을 기록했다. 인증 고유18멤버·예외 타입 이관을 포함한7파일 안은 이후 사용자 「계속 진행해」의 계속 실행 지시 아래 메인이 다음 묶음으로 선정해2.6절로 구체화했다. 이후 사용자 「바로 작업 진행해」의 계속 실행 지시 아래 메인이 AI live race provider 기존2파일 안을 다음 묶음으로 선정해2.5절에서 구체화했다.2.4절 소규모 정리 결과와 구분한다.

### 7.3 둘째 묶음 구현 결과(2026-10-23)

| 실제 수정 파일 | 결과·현재 소스 위치 |
|---|---|
| Assets/_Project/Scripts/Domain/Building/BuildingStats.cs | 45/48/159/170행 XML 주석4줄 현행화(4삭제·4추가). 방어 타워 피해 계산 조회·공격 후 대기 시간 설정 조회 용도 설명 |
| Assets/_Project/Scripts/Application/Combat/TimedDamageEffectPresentation.cs | 구현 전4행 Core using1줄만 제거. UnityEngine.Time은 이전33행→현재32행으로 내용 유지 |

메인은 수정 전 backup SHA를 확인하고 문자열 리터럴 보존·주석/공백 제외 tokenizer로 BuildingStats 실행 토큰·리터럴 동일을 확인했다. 모든 수정행은 XML 주석이었다. Timed 파일은 원본에서 해당 using1행을 제거한 텍스트와 현재 파일이 정확히 일치했다. 비교 도구의 사본 fixture4개(주석 추가·기존 공백 확대=동일, 숫자 변경·문자열 한 글자 변경=상이)도 확인했으며 `git diff --check`0이다. 문서 담당은 현재 변경 위치를 별도 직접 읽었다.

이번 묶음에서 신규 코드·meta·수치·씬 변경은 없다. 메인의 구현 후 동일 namespace 재검색은 Domain51파일0hit/Application86파일3파일5행(AI31, AccountLink23, Login26/27/29)으로 Timed의 Core hit 해소를 확인했다. Research9.1의 Domain0/Application6행은 구현 전 기록으로 보존한다. 시작 C#328개 중 누적 변경5·무변경323+신규 C#1이며, 이번2파일과 첫 묶음3파일을 구분한다. 컴파일·Unity 실행·실기·빌드는 미실행이며 소스 정리와 한정 정적 대조 완료로만 판정한다. 전체 감사와 AI/Auth 후속 구현, 첫 묶음 사용자 수용, 상시문서/메모리·TC/QA 완료로 확대하지 않는다.

### 7.4 셋째 묶음 착수 기록(2026-10-23)

사용자 「바로 작업 진행해」의 계속 실행 지시 아래 메인이 앞서 공유한 AI live provider 기존2파일 안을 선정했고 프로그래머 제안을2.5절로 구체화했다. 코드 변경 전에 Research10절의 근거·새 null 계약·남는 DifficultyParams 의존성과 함께 메인에게 전달해 직접 검토를 마쳤다. 메인 구현 신호 이후 game-programmer가 구현했고 메인 독립 정적 대조를 마쳤다. 구현 결과·소스 대조·실행 한계는7.5절에 기록한다. 첫 두 묶음의 과거 검사·행번호·실기 미확인 경계는 보존한다.

### 7.5 셋째 묶음 구현 결과(2026-10-23)

| 실제 수정 파일 | 현재 소스 위치·결과 |
|---|---|
| Assets/_Project/Scripts/Application/Services/AIOpponentController.cs | 50행 readonly Func<RaceId>,165행 필수 provider 인자·166행 optional upgrade,173행 null 거부 대입,934행 `_aiRaceProvider()` 반환. 헤더/메서드 주석 현행화 |
| Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs | 843행 `() => GameRaceContext.RedRace,` 주입. 기존 _unitUpgrade 인자·초기화 가드·순서 유지 |

메인은 코드 전329C# snapshot 원본2개 SHA를 확인한 뒤 허용된 field/parameter/assignment/return/lambda만 역치환하고 주석·공백 제외/문자열 리터럴 보존 토큰 대조로 두파일 원문 동일을 확인했다. 생성자에서 provider 호출0, 현재434/448/488/632행4사용점과 호출 시점 유지, 정확한 live static getter 람다·upgrade 인자 유지도 확인했다. 해당 snapshot329개 중 지정2개만 변경·327개 동일·신규 C#0, `git diff --check`0이다. 문서 담당도 주요 소스 위치를 직접 읽었다.

provider null 거부라는 새 생성자 계약을 구현했으며 기존 유일 정상 caller는 비null 람다를 넘긴다. DifficultyParams는 현재57/161행, Infrastructure using은31행에 남는다. 종족 조회 직접 결합만 해소했고 난이도9값·직렬화/시나리오/자산·타이밍·이벤트·자원·생산·피해·네트워크·Auth는 바꾸지 않았다. Research AUD-009 부분 해소와 세부 AUD-014로 연결한다.

컴파일·게임 실행은 미실행이다. 이번 해소는 AI 실행 코드의 GameRaceContext 직접 조회1곳이며 Application raw 검색에 다른 파일의 설명 주석이 남으므로 문자열 잔존0으로 보고하지 않는다. 앞선 두 묶음 실기 수용·전체 감사·AI 전체 역의존 해소·Auth7파일 제안 구현·상시문서/메모리/TC·QA 완료로 확대하지 않는다. 기존 예외 QA 이력과 구현 전 행번호·각 회차 검색/검사 근거는 보존한다.

### 7.6 넷째 묶음 착수 기록(2026-10-23)

사용자 「계속 진행해」 아래 메인이 기공유 인증 계약안을 다음 묶음으로 선정했다. 전문 프로그래머의 정확7파일·계약18멤버·정의 선보존/비교·namespace 표시 영향·남는 UGS 의존을 코드 전2.6절에 반영했다. 메인 전체 검토/구현 신호 이후 소스 정리·독립 정적 대조를 마쳤다. 기존 회차 근거·실기 미확인은 보존하며 최종 소스 결과와 한계는7.7절에 기록한다.

동명 타입 검토: LoginUseCase의 같은 Hexiege.Application namespace 새 IAuthenticationService가 외부 using의 동명 타입보다 우선한다는 언어 규칙을 검토했다. UGS SDK source/DLL이 현재 없어 실제 SDK 동명 interface 선언은 직접 미확인이고 실제 컴파일 근거도 아니다. 기존 UGS AuthenticationService 실행 참조는 무변경이며 alias/rename을 추가하지 않는다. 메인이 계획 전체 읽기 검토를 마친 후 구현 신호를 전달했다.

### 7.7 넷째 묶음 구현 결과(2026-10-23)

| 실제 변경 파일 | 현재 소스 결과 |
|---|---|
| Assets/_Project/Scripts/Infrastructure/Auth/FirebaseAuthService.cs | 47행 class에 IAuthenticationService 구현 선언. 이전 enum/예외는 Application 파일로 이관 |
| Assets/_Project/Scripts/Application/UseCases/LoginUseCase.cs | 63/100행 field·ctor 인터페이스화, Infrastructure using 제거.20행 헤더는 Task 기반 흐름·UGS 직접 후속 의존 설명 |
| Assets/_Project/Scripts/Application/UseCases/AccountLinkUseCase.cs | 49/58행 field·ctor 인터페이스화, Infrastructure using 제거·헤더 현행화 |
| Assets/_Project/Scripts/Application/Interfaces/IAuthenticationService.cs 및 meta | 신규15행 interface·6읽기속성+12Task 계약. GUID `b52ffc5a58464dce9595772ea310c144` |
| Assets/_Project/Scripts/Application/AuthException.cs 및 meta | 신규17행 enum·38행 exception. GUID `71de453ebf0f49df99c37637e340ae58` |

메인이 수정 전 SHA 대조 후 기존 enum/class body 추출과 신규 namespace wrapper 제외 토큰 비교로 enum13순서·Reason/constructor/message/inner/상속 동일을 확인했다. Firebase class는 이관 정의와 interface 구현 선언 외 토큰 동일, UseCase2파일은 지정 field/ctor·using 제거 외 토큰/문자열 동일, interface18멤버 반환·인자·기본값은 기존 public signature와 모두 일치했다. Bootstrap/Profile/로그인 View6개의 소스 해시 무변경 및 GUID2개가 각각 Assets meta 자신1건임도 확인했다. 문서 담당도 주요 소스와 새 정의·계약·meta를 직접 읽었다.

이번329C# snapshot 기준 기존3개 변경·326개 동일+신규2개=331개다. 프로젝트 C#313개/Application88개이며 신규4파일을 Research7.2 증분 장부로 추적한다. `git diff --check`0이다. 일부 임시 비교 artifact SHA는 Login 헤더 최종 변경 전 값이어서 현재 해시로 재사용하지 않는다.

구현 후 지정 namespace 검색은 Domain51파일0hit/Application88파일2파일3행(AI31행 Infrastructure, Login25/26행 UGS)이다. 이전5행 snapshot은 당시 기록으로 보존한다. UGS 직접 호출과 AI DifficultyParams 의존은 남는다. 서비스 async/로그아웃/토큰/오류 변환/세션 보존/로그 문자열은 소스상 유지됐지만 예외 FullName/ToString/스택 namespace 진단 표시는 변한다. 컴파일·Unity/실기는 미실행이고 전체 인증 계층·의존성 감사·기존 사용자 수용의 신규 실기 완료로 확대하지 않는다. Legacy 최종 삭제·TC/QA·상시문서/메모리 갱신은 수행하지 않았다.

### 7.8 다섯째 묶음 코드 전 기록(2026-10-23)

사용자 계속 실행 지시 아래 메인이 선정한 AI 난이도 값 분리의 전문 프로그래머 구체안을2.7절로 기록했다. Infrastructure 직렬화 보호·9값1대1복사·기존7읽기·미연결 생산시간 배율 분리와 AI 규칙4/33의 필수 문서 동기화만 범위에 넣었다. 코드 전 메인 검토·구현 신호 뒤 소스 정리·정적 대조·규칙4/33 동기화를 마쳤다. 결과는7.10절이며 UGS는 별도2.8절로 구분한다. 앞선 묶음의 실제 실행/실기 미확인은 유지한다.

### 7.9 여섯째 묶음 코드 전 기록(2026-10-23)

메인이 기존 계속 실행 지시 범위의 UGS 잔여를 AI2.7 다음으로 선정했고 전문 프로그래머의7파일·8멤버·정책 없는 adapter 제안을2.8절로 기록했다. AI 코드 검증·메인 계획 전체 검토 뒤 별도 구현 신호를 전달했고 소스 정리·독립 정적 대조를 마쳤다. 소스 완료와 실제 컴파일/실기 미실행을 구분해7.12절에 기록한다. 기존 Network initializer를 재사용하지 않는 효과 차이와 SDK 로그아웃 블록 이관·진단 스택 영향도 기록했다.

### 7.10 다섯째 묶음 구현·규칙 설명 동기화 결과(2026-10-23)

신규 AIDifficultyParameters는13행 readonly struct·15~40행 readonly9float·43행9필수 인자 ctor/9단순대입이다. GUID는 c2846d09dbd9404c93926158b072c8fe다. AI56행 field/160행 ctor는 Application DTO를 사용하고 Infrastructure using을 제거했다. Setup833~842행 named args9동명1대1복사·849행 인자 교체 외 income830/log859의 기존 aiParams·live 종족 provider/upgrade·기존7읽기를 유지했다. 문서 담당도 해당 소스를 직접 읽었다.

메인 독립 검토는 코드 전331개 중 지정2개 변경·329개 동일+신규 DTO1=332개다. 허용 import/type/new copy/arg 교체 외 문자열 보존 토큰 두파일 동일, DTO 전체도9readonly fields/9params/9assignments만임을 확인했다. 보호6해시(AIConfig.cs/meta/asset/meta·기존 AI/Setup meta) 동일·신규 GUID 유일·`git diff --check`0이다. 초회 예상 template의 namespace 닫는 brace 누락으로 assertion 실패가 있었으나 예상 template 수정 뒤 실제 비교 전부 통과한 검증 도구 오류이며 production 결함이 아니다.

허용된 GameSystemRules_AI.md 규칙4의 제목/주입 설명과 규칙33의 주입 설명만 실제 snapshot 경계로 현행화했다. AIConfig3직렬화 필드 설명·규칙ID·규칙5수치·다른 행동 요구사항은 유지했다. 신규 DTO/meta는 Research7.3 증분 장부로 추적한다.9값 전달을9행동 소비로 주장하지 않으며 생산시간 배율 미연결 AUD-017은 별도 미해결이다. 컴파일·Unity/실기는 미실행이고 전체 감사/기존 사용자 수용 완료가 아니다.

### 7.11 여섯째 묶음 필수 구조 안내 동기화(2026-10-23)

Plan2.8에 추가 정확 범위를 먼저 기록한 뒤 AuthSystemRules의 레이어 표 Infrastructure/Application2행과 구현 위치 blockquote1행만 현행화했다. 실제 존재하는 계약·공용 오류 소유권·UGS adapter 실행 책임·Firebase token 발급 경로를 표시하고2표행의 과거 receipt 표현을 제거했다. 행동 규칙·코드 예시·다른 원문은 유지한다. 메인의 UGS source 최종 독립 정적 검토도 완료됐으며 그 결과를 컴파일/실기 수용 완료로 확대하지 않는다. AI규칙과 Auth구조 이외 상시문서/메모리는 변경하지 않았다.

### 7.12 여섯째 묶음 구현·독립 정적 대조 결과(2026-10-23)

실제 기존3C#는 LoginUseCase(field63/ctor102/guard105/명시logout397/Bridge471~505), LoginBootstrapper207, ProfileView177이다. 신규 IUgsAuthenticationService15행 계약8멤버와 UgsAuthenticationService19행 stateless 구현 및 각각 meta로 코드7파일이다. 신규 GUID는 `dc6b3c26d2e548f78d8d70821d108e5b`/`758217fe5ff54802804ed9fc6998df8a`이며 Research7.4 증분 장부에 편입했다. 문서 담당도 계약/adapter·주입 위치와 meta를 직접 읽었다.

메인은 코드 전332snapshot 원본3SHA를 맞추고 Login의 허용 field/ctor/guard/import2/SDK표현식7·logout블록을 역복원해 토큰 동일을 확인했다. 보간 PlayerId 원문 표현식도 복원했으므로 원문 문자열 전체 무변경이 아니라 로그 표시식·해시조회 시점과 비보간 literal 보존으로 한정한다. 이관 SDK블록은 raw dedent와 변수명3대응 외 동일하고 legacy comment도 보존했다. adapter 전체 active code가 예상8API/직접Task3/live getter/logout블록과 토큰 동일(no fields/cache/명시ctor/catch/log/fallback), interface전체8API도 예상 토큰과 같았다. caller2는 추가 인자 역복원 뒤 원문 텍스트와 완전히 같았다.

이번332개 중 지정3개 변경·329개 동일+신규2=334개이며 프로젝트 C#316개다. meta GUID2개 각각 Assets 검색 자신1건·기존meta 유지·AI 보호6해시 동일·`git diff --check`0이다. 구현 후 지정 namespace 마스킹 검색은 Domain51파일0hit/Application90파일0hit며 전체 의미·보간식 해석·동적 참조 부재나 컴파일/실기 PASS가 아니다. 이전3행/5행 검색과 각 snapshot은 당시 근거로 보존한다.

메인이 Auth 규칙 before와 현재의 표2행/구현위치1행만 변경됨과 AI 규칙3행 외 나머지 원문 동일을 독립 확인했다. 허용 문서 최종 범위는 Research/Plan+AI/Auth2규칙이며 다른 상시문서/메모리는 변경하지 않았다. 모든 단계 실제 compile/Unity/실기는 미실행, SDK 예외 adapter frame/줄번호 진단 표시 변화 가능, 생산시간 배율 AUD-017 미해결을 유지한다. 소스 의존성 지정 범위와 구조 설명 동기화를 전체 감사·죽은 코드 정리·기존 사용자 수용 완료로 확대하지 않는다.

### 7.13 사용자 승인 설명 정정·구현상태 기록 확인(2026-10-23)

사용자 「그럼 그렇게 수정하고 코드 구현상태는 어떻게 기록되어있는지 확인해서 알려줘」로 ai-scenarios.md의 난이도 문장과 이관 머리말을 정확히 정정했다. 시나리오 대기·골드 수입·생산시간 배율을 설계 범위에 포함하고 대기 시간 쉬움×1.5/어려움×0.7은 조정 예정 예비 기준으로 명시했다. 이전 문장은 날짜 정정에 보존하고 다른 내용은 유지했다. 기존 Task를 이어 사용하며 새 코드 변경은 하지 않는다.

Research14절에 직접 확인한 구현상태 출처를 기록했다. PROJECT_STATUS25행은 포괄 AI 요약, History3669행은 설정 필드 생성 완료,3673행은 수입 배율 적용이며 생산시간 배율 효과의 개별 완료 기록과 구분한다. 현재 설정1.3/1/0.7과 DTO 전달은 있으나 생산시간 계산629/692행과 기본 getter80~81행에서 배율 적용을 확인하지 못했다. AUD-017은 여전히 미해결이고 실제 컴파일/실기·신규 기능 PASS를 부여하지 않는다. 이번 수정은 ai-scenarios.md/Research.md/Plan.md3파일에 한정하며 다른 코드/에셋/규칙표/이력/Status/Roadmap/메모리/기준선은 유지한다.

후속 사용자 결정(2026-10-23): 「그럼 나중에 AI 관련 작업할떄 구현하도록하자」에 따라 AUD-017은 후속 AI 작업에서 구현하고 이번 리팩토링에서는 보류한다. 규칙5의 설계·수치와 효과 미연결·구현 미완을 유지하며 보류를 구현 완료로 판정하지 않는다. 이번 결정 반영은 기존 Research/Plan2파일만 수정한다.

### 7.14 현행 문서·UnitView 다음 묶음 코드 전 기록(2026-10-23)

메인이 현행 버전/폴더 사실 정정3문서(2.9)와 UnitView 조회 중복/주석1코드파일(2.10)을 다음 범위로 선정했다. 코드 전에 구체안을 기록했으며 옛 이력 일괄 치환·추가 TDD 인증/AI 설명 편집·도구 삭제를 포함하지 않는다. 실제 결과는 각 범위의 수정·대조 뒤 별도 기록한다.

### 7.15 현행 버전·폴더 문서 정정 결과(2026-10-23)

TDD41/현재1121행·document-manager agent28행·공용 메모리284행의 엔진 버전을 ProjectVersion.txt와 같은6000.3.5f2로 정정했다. TDD99행 Bootstrap/Debug의 에디터 준비·투영 강제실패 역할을 추가하고105행 Diagnostics를 실제 Debug/UIManager 테스트 핸들러로 바꿨다. before 원문3개와 diff를 직접 읽어 지정 버전4곳·폴더1행교체/1행추가 외 원문 동일을 확인했다. 과거 Task/History·다른 메모리/기준선·TDD 인증/AI설명·코드/폴더/자산은 변경하지 않았다. AUD-002/011의 지정 충돌을 닫되 전체 문서 감사/실기 완료로 확대하지 않는다. 생산시간 배율 AUD-017 후속 AI 보류는 유지한다.

### 7.16 UnitView 조회 helper·주석 정리 결과(2026-10-23)

UnitView.cs46행의 낡은 주석을 현재 interface 계약 설명으로 정정하고1892행에 private GetTargetObject helper1개를 추출했다. 기존 WorldPos1903/Transform1917 getter는 helper를 각1회 호출하며 Unity null 검사·현재 position+forward 폴백·null Transform 반환·캐시 부재를 유지했다. 현재 WorldPos caller8341행1개, Transform caller926/1343/1470/1951/1975/6490/7104/7965행8개를 유지했다.

메인이 새 helper XML/본문 제거·호출2곳의 원삼항식 복원·46행 원문 주석 복원 후 파일 텍스트 전체가 snapshot과 정확히 같음을 확인했다. 이번334C# snapshot에서 UnitView1개만 변경·333개 동일·파일 집합 동일, meta bytes/기존 GUID 동일이다. 문서 담당도 현재 소스를 직접 읽었다. 메인 check_docs exit0·문제0/diffcheck0·work 확인은 해당 형식/소스 범위의 검사이며 실제 compile/native 게임/실기 PASS가 아니다. AUD-019 지정 소스 정리를 닫되 전체 감사·도구 삭제·AUD-017 생산배율 구현 완료로 확대하지 않는다.

### 7.17 자율 잔여 작업 착수·담당 취합(2026-10-23)

사용자 전면 계속 지시를 반영해2.11의6후속 인덱스·담당·정확 끝조건과 기존 미완/보류 경계를 기록했다. 현재 presentation98/infra89의 자료는 inventory/lexical 단계로 의미 읽기 완료가 아니며 DomainApp/rules 자료는 수신 대기다. 기존 장부 상태를 일괄 승격하지 않았다. baseline 증가 전용 도구 갱신은 메인에게 범위를 사전 보고한 뒤 실행 직전 감소0/+50을 재확인하고 --update-baseline exit0으로 반영했다. 취합 자료는 해당 담당이 실제 읽은 범위와 근거를 받은 뒤 반영한다.

### 7.18 다음 확정 정리 묶음·기준선 도구 반영(2026-10-23)

전문 담당 제안을 메인이 검토한2.12 Infra4파일·2.13 Domain2파일·2.14 Stats1문장의 사실 정리안을 기록했다. 지정 변경 외 실행 토큰/리터럴·직렬화·값·자산을 보존하고 코드 담당은 메인 신호로 구현한다. 메모리 기준선은 tool 재측정 감소0/3폴더+50 후 --update-baseline exit0,postfolders 일치 확인을 마쳤다. 직접 JSON 편집·감소 반영은 없으며 새 코드/실기 PASS가 아니다.

### 7.19 자율 코드 정리18파일 최종 결과(2026-10-24 KST)

2.12 Infra/Core7·2.13 Domain/App4·2.15 Presentation6·2.16 Bootstrap1의 코드 정리·메인 독립 대조를 마쳤다. 실제 파일목록/근거는 Research16.6,단일문제ID는 AUD-020~023이다. Infra Inspector8/Effects Tooltip5표시문자열 변경과 OnDestroy실행2문장추가는 무변경이라고 축소하지 않는다. 나머지 지정토큰/리터럴·기존meta·legacy6블록·수치/자산을 보존했다. 신규파일은 없고 actual compile/Unity/native/실기/빌드·TC/QA는 NOT RUN이다.

### 7.20 현행9문서 정정 결과(2026-10-24 KST)

2.14의9문서 지정사실 정정을 완료했다. Stats하드캡/Garage·Mist목표·AI API/3Scenario설계동기화·Upgrade수용범위·Sound Splash/로그·Auth공식세션설명만 바꿨고 Research16.7·AUD-024~032로 연결한다. Garage의Config→Game GUID→Setup소비와 Firebase공식원문도 별도로 직접 확인했다. exact예상치환과9전체원문일치,정확diff artifact 및 check_docs exit0·문제0을 확인했다. 코드/밸런스·규칙ID·고유시나리오수치·다기기정책·Status/Roadmap/추가Memory/옛Task·History는 이번 정정에서 변경하지 않았다. AUD-017효과미구현/후속AI보류와6VFX삭제보류·전체의미감사미완을 유지한다.

### 7.21 UnitView partial 정확8파일 결과(2026-10-24 KST)

2.17 구현·메인 독립검증을 마쳤고 AUD-003에 연결했다. UnitView원8641행의75메서드/4860행을3partial로원문이동해main3781행,MovementAuthority1421행/Movement2818행/Healing672행이다. 필드171순서·직렬화6·callback4·전체157원래메서드와전처리문맥을유지하고 기존4sourcegate는같은class전체를읽도록 범위보존했다. 신규3C#/3meta·기존UnitView/Editor2C#의정확8파일이며 Research7.5신규장부/16.8근거와연결한다.

전문comparison·메인독립artifact로 원문역복원/segment/main/wrapper/meta/GUID3유일·334중기존2변경332동일+신규3=337을확인했다. actualcompile/Unity/native/실기/빌드·TC/QA는 NOT RUN이며 source경로/줄번호·stack표시변화가능성을유지한다. 필드·초기화·이벤트/코루틴소유권·직렬화/게임정책·6VFX보호블록·후속AI보류는바꾸지않았다.

### 7.22 유닛 marker·부분 VFX 수용·Application 설명 정정 결과(2026-10-24 KST)

2.18의 정확3문서 지정사실 정정을 완료하고 Research16.10/AUD-033~035에 연결했다. Inferno1초 오기는 Stats/감사표 현행계약 참조로 교체했고 Mushroom의 실제1초1개와 기존 impact·새poison프리팹 승인만 반영했다. Application 그림은 UseCase·계약 책임설명이다. before원문/SHA·exact예상치환3전체원문일치·정확diff와 check_docs 문제0을 확인했다. 메인 독립 최종 대조도 /tmp/hexiege-plan218-main-independent.json으로3파일각1행변경/기존수용한계보존을확인했다. 코드/meta/에셋/수치·정책은 변경하지 않았다. tracer/권위/수명/전체migration OPEN·후속AI보류/6VFX삭제보류·실제compile/Unity/실기 NOT RUN을 유지한다.

### 7.23 생산 UI·최신 HB·Editor 안내5소스 구현 결과(2026-10-24 KST)

2.19~21을 메인GO뒤 구현했고 Research16.11/AUD-036~039로 연결했다. 생산 UI 수명1회바인딩·닫힘/대상교체/disable 누르기초기화·Ticker8Rx Clear재바인딩/파괴수명·도달probe후최신HB/수명재검사·Editor 최초실패안내상수정정이다. 메인5파일diff직접검토와전문inverseRawEqual5/5·직렬화줄/public surface/숫자상수보존·gitdiffcheck0을정적근거로구분했다. 메인독립artifact의5raw역복원/기존meta5보존·보호3579hash동일·최신HB재계산후Confirm전yield없음도Research16.11에분리기록했다. 실제compiler/Unity/native/runtime재현/TCQA/빌드는NOT RUN이며 새정책/수치/asset/meta편집은없다.

### 7.24 마지막 범위 동기화와 미완 인계(2026-10-24 KST)

Research16.12에4scratch의최신읽기범위합집합재계수·구조스캔/직접읽기/실행검증구분·현재gitstatus전체경로/종류별수를기록했다. 원snapshotSHA를덮어쓰지않고 마지막추가5소스·3문서·기존Task2기록을이전누적변경과구분했다. 전체의미감사/미독범위·AI경제차감/종족보너스/람다Conditional미확정후보·AUD-017후속AI보류·6VFX삭제보류·실기NOT RUN을유지한다. 추가상시문서/메모리/Status/Roadmap/옛TaskHistory수정·신규기획/TCQA/Gitmutation은수행하지않는다.
