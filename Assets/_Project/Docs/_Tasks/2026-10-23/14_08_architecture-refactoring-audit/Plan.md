# 아키텍처 준수·기능 보존 리팩토링 계획

기존 게임의 동작과 사용자가 이미 확인한 기능을 유지하면서, 코드를 이해하고 고치기 쉬운 구조로 정리한다. 먼저 문서·코드·에셋의 관계와 남은 조사 범위를 기록하고, 근거가 확보된 작은 작업부터 진행한다. 이번 문서는 전체 작업의 순서와 첫 조사 범위를 정하며, 아직 확인하지 않은 코드의 삭제나 재설계를 확정하지 않는다.

> **기존 로직 제거와 삭제 보류:** 실행 로직을 폐기하는 삭제는 현재 범위에 없다. **Plan2.6의 AuthErrorReason/AuthException은 동작 폐기가 아닌 정의 이관이다. 목적지 Application/AuthException.cs에 기존 enum13순서와 예외 body를 먼저 보존·비교한 뒤 원위치의 중복 정의를 옮긴다. old class/shim/주석 dead block은 새로 남기지 않으며, 실기 대기 Legacy 최종 삭제와 구분한다.** WORKFLOW [4]에 따라 제거 후보의 안전 근거를 먼저 적고, 검증 전에는 비활성화(주석 처리)를 기본으로 한다. 최종 삭제는 사용자 테스트 통과 후·문서/메모리 업데이트 전에 해당 승인 범위로 수행한다. ROADMAP의 VFX 옛 로직6곳은 기존 결함의 사용자 테스트 통과 조건을 유지한다. `Legacy`, `Shadow`, `temp`, `Setup`이라는 이름이나 검색 참조0만으로 삭제하지 않는다.

- 기준: 2026-10-23 14:08 KST.
- 조사 장부와 근거: [Research.md](Research.md).
- 최신 확인(2026-10-24 KST): 직전71경로 커밋·푸시 `1a69402c` 후 사용자 「컴파일 에러없으니 다음진행하자」 보고를 컴파일 확인 축으로 기록한다. 플랫폼·로그·전체 게임 실기 증거는 제공되지 않았다. 현재 `work` 브랜치이며 이번 후속 착수 전 작업트리는 clean이다. 이후 추가 커밋·푸시는 아직 하지 않았다. Research16.13/이 문서7.25가 최신 근거이며 아래 과거 미실행 표기는 당시 에이전트 실행 기록이다.
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

### 2.22 자율 후속: 경로·서비스·운영 로그·프로필의 낡은 주석4소스 정정

사용자 계속 진행 승인 아래 메인이 선정한 기존4C#의 주석만 정정한다. 근거는 TDD의 계층/현재 구현 설명, LogRules1.5의 enum 이름·운영/개발 구분·구조화 필드, SettingsUI 「인게임 설정 메뉴」 규칙6의 표시 전용 프로필·뒤로가기 계약이다. 문서 담당이 관련 원문을 직접 읽었으며 코드 전에 아래 범위를 기록한다. 메인 계획 검토·별도 구현 신호 뒤 domain_next 전문 담당이 수정한다.

| 파일(Assets/_Project/Scripts/ 기준) | 정확한 변경 범위와 보존 근거 |
|---|---|
| Domain/Hex/HexPathfinder.cs |20행의187개/11×17 성능 단정을 호출자가 전달한 HexGrid에서 외부 라이브러리 없이 탐색한다는 설명으로 교체.47~48행의 목표 blocked 예외 설명은 실제97행처럼 목표를 포함해 추가 차단 좌표를 제외하고41행 start==goal은 먼저 즉시 반환한다는 설명으로 교체. 경로 알고리즘·분기·순서는 유지 |
| Application/Interfaces/IGameServices.cs |17~18행 Getter10종 고정수를 그리드·UseCase·유닛 팩토리 조회 및 네트워크 게임 시작/시작 여부 책임으로 설명. 본문 getter14개를 확인했으나 새 고정숫자를 넣지 않는다.19행 제외 계약·전체 API 유지 |
| Application/Interfaces/ILogSink.cs |68~71행 최소 예외/209건 미래 이관 설명을 현행 전역 예외·인증·네트워크 세션·클라우드·맵 운영 이벤트와 호출부 구조화 필드 설명으로 교체.524~525행 선언만/향후 F~H 호출 설명은 현행 재전송·전송 결말·수신 검증 사용과 스폰/디스폰 개발 로그 구분으로 교체.549/575/594/615/638행 발생 지점의 미래 호출 괄호만 현행 LogOutcomeOnce·HandleHostTimeout·FailHostRound/FailClientRound·VerifyAndAnswer/MapReadyServerRpc 경로로 정정. 역사 단계·정책 이유·4결말+1재전송 계약은 보존 |
| Presentation/UI/InGameSettingsUI.cs |ShowProfilePanel XML456~460행의 빈 패널/뒤로가기 미배선/복귀 불가5줄만 현재 조회 전용 요약 RefreshAsync 요청과 Initialize에서 BackButton 참조가 있을 때 복귀 핸들러를 연결한다는 조건부 사실로 정정. 실제 Initialize172/211~214와 Show465행을 직접 확인했다. 씬 배선·새 실기 수용을 단정하지 않는다 |

전문 담당 전달 NetworkMapTransfer의909/946~958·1292/1323~1329·1344/1583·1448~1465·1532/1550·1724~1773 경로 중 문서 담당은900~960/1710~1775를 직접 읽어 재전송 Warn과 결말1회 Ops dispatch/키 변환을 확인했다. 나머지 구체 호출 위치는 전문 전달 근거로 구분한다. 초기3파일 before SHA는 HexPathfinder `394d747e461a80fded3c341bed964df4ed8b18f1107c482397acfeefc73f6427`, IGameServices `19ece22e728db9867c4318d3bf987c6050bba048b07859a0b8e31ee5dca1cbaf`, ILogSink `28fab48c75f36b07ee022a7614cdcb9c6fa2a685bd88e8a1222c95b0880b1b15`이며 후속4파일 원문 증거는 구현 담당이 보존한다.

runtime/API·metadata/Inspector 문자열·실행 토큰·리터럴·수치·순서·수명·seed/canonical/PRNG·서버 writer를 모두 보존한다. 주석 정정을 새 컴파일/Unity/native/게임/빌드·TCQA 실행으로 보고하지 않으며16.13의 사용자 컴파일 보고는 직전 푸시 리비전의 별도 근거다. 상시문서·메모리·옛 Task/History·AI보류·6VFX삭제보류는 이4소스 묶음에서 변경하지 않는다.

### 2.23 자율 후속: 문서 검사기 토픽의 과거 한계와 현재 상태 구분

메인이 승인한 정확1토픽 `.claude/agent-memory/game-programmer/check-docs-tool.md`만 대상으로, `Known limitation — 33 rules are invisible to the checker (UNRESOLVED)` heading 직후에 짧은 English current-status note1개를 추가한다.8월25일의 원본문·heading·anchor는 그대로 보존하며 과거 한계를 현재 결함으로 재개하지 않는다. WORKFLOW [11]의 현재 검사 범위와 현행 parse_rule_docs/ID parser가 권위 근거다. 메인과 문서 담당이935~970/823~829를 직접 읽고 독립 함수호출에서 Map5/RandomMap19/Upgrade13 정의와 정의 없는 문서도 등록됨을 확인했다.

추가할 정확 문구는 다음과 같다: **Current status (2026-10-24):** The 2026-08-25 limitation below is historical. The current `parse_rule_docs()` registers every rule document, and `RULE_ID`/`RE_DEFINITION` support numeric, subnumber and letter IDs. The current check includes Map, RandomMap and Upgrade definitions; its latest scope output and `Assets/_Project/Docs/WORKFLOW.md` [11] govern coverage. This supersedes the historical unresolved status only; it does not certify gameplay or implementation correctness.

쓰기 전 원문 snapshot/SHA를 보존하고 정확 note삽입을 역복원한 전체 raw동일·기존 heading/anchor보존을 확인한다. 메모리 보호기준선은 도구 재측정에서 감소0/증가만 확인한 뒤 `python3 Tools/check_docs.py --update-baseline`으로 반영한다. 직접 baseline JSON편집·감소 반영·다른 메모리/Status/Roadmap/옛TaskHistory 수정은 없다. Research/Plan의 이번 계획·결과 기록만 함께 갱신한다. 이 사실 안내를 새 컴파일·게임검증으로 해석하지 않고 직전 푸시의 사용자 컴파일 보고는16.13 별도 축으로 유지한다.

### 2.24 자율 후속: TDD 초기 골드 현재 소비 설명2곳 정정

잔여 TDD 직접읽기에서927행 ResourceUseCase 역할의 `맵 InitialGold 적용`과1066행 `현재 경기 초기 골드는 맵의 InitialGold가 권위`가 현재 소비와 어긋남을 확인했다. 메인도 해당2곳과 Setup457/GameConfig125/ROADMAP56/Status22를 직접 대조해 사실 정정 후보를 선정했다. 정확1문서 `Assets/_Project/Docs/TechnicalDesignDocument.md`의2물리행만 대상으로 아래 문구를 기록하며 메인 전체계획검토·별도 신호 뒤 문서 담당이 수정한다. 근거는 TDD 현재 구조 설명 책임과 ROADMAP의 InitialGold 실제 연결 후속·Status의 생성/전송/실제 소비 구분이다. 규칙·경제 계약을 바꾸는 계획이 아니다.

-927행 표: `팀별 골드 관리 (맵 InitialGold 적용, 차감/추가/조회)`를 `팀별 골드 관리 (초기화, 차감/추가/조회)`로 교체한다.
-1066행 문단: `현재 초기 자원은 GameConfig.StartingGold를 ResourceUseCase에 전달한다. 맵 InitialGold의 생성·전송과 실제 시작 자원 연결은 별개이며 후자는 ROADMAP의 경제 후속 범위다. 별도의 맵 테스트 모드 분기를 두지 않는 계약을 유지하고 경제 설정의 기본값·fallback과 경기 맵 적용을 구분한다.`로 기존 설명2문장만 교체한다. 기존 StatsReference/네트워크 TDD/필드제거 당시 History 링크3개는 그대로 둔다.

실제 ResourceUseCase43~48은 주입받은 startingGold로 Blue/Red를 초기화하고 Setup457은 `_config.StartingGold`를 전달한다. GameConfig125의 getter를 확인했으며 맵 InitialGold로 이 값을 덮어쓰는 경로를 Bootstrap 검색에서 확인하지 못했다. 생성/전송값이 존재함과 실제 자원 소비가 연결됨을 구분한다. 수치·코드·기획/밸런스·규칙·옛Task/History·다른 메모리는 수정하지 않는다. 변경 전 전체 원문/SHA를 보존하고 정확2치환 외 fullraw동일·기존링크보존을 검증한다. 새컴파일/게임실기·전체 의미감사 완료를 주장하지 않는다.

### 2.25 자율 후속: 광산 표현 구독과 건물 배치 UI 파괴 수명2소스 정리

메인이 선정한 `Presentation/Grid/HexGridRenderer.cs`와 `Presentation/UI/BuildingPlacementUI.cs`의 구독 수명만 정리한다. 근거는 GameSystemRules_MatchFlow.md 규칙14의 재경기 맵 교체/기존 흐름 유지, GameSystemRules_UI.md 「공통 UI 규칙」 규칙5의 overlay 단일 소유/점유와 반납 짝, GameSystemRules_BuildingPanelsUI.md 「건물 배치 패널 UI」 규칙2·3의 열린 동안 골드 색상 재평가다. 문서 담당이 해당 원문·현재 renderer 구독/재렌더/딕셔너리 조회와 배치UI Show/Close를 직접 읽었다. 아래는 코드 전 계획이며 메인 전체 검토/별도 GO 뒤 전문 구현한다.

| 파일(Assets/_Project/Scripts/ 기준) | 허용 변경과 보존 |
|---|---|
| Presentation/Grid/HexGridRenderer.cs | private bool `_goldMineEventsSubscribed`1개 추가. SubscribeGoldMineEvents 진입 시 이미 true면 return, 최초 정상 구독 시 true 설정. 기존 OnBuildingPlaced/OnBuildingDied의2 Subscribe/AddTo(this) 본문은 그대로 유지한다. callback은 grid를 캡처하지 않고 현재 `_goldMineObjects`를 조회하므로 renderer수명1회 바인딩으로 이후 재렌더의 새 광산을 처리한다. ClearGrid에서 bool을 초기화하지 않는다 |
| Presentation/UI/BuildingPlacementUI.cs | private OnDestroy1개에서 `_resourceSubscription?.Dispose(); _resourceSubscription = null;`만 수행한다. Close를 호출하지 않는다. 현재 Close는 열림 여부와 관계없이 공유 overlay 점유를 반납하므로 파괴 시 호출하면 타 화면 점유를 깎을 수 있다. Show/Close·색상·overlay·입력/팝업 동작은 그대로 둔다 |

전문 소스추적 전달: 싱글 GameEndUI1263 LoadMap→Bootstrap Map175 ClearAll372~428은 renderer를 파괴하지 않고, RenderGrid189→ClearGrid248은 자식만 파괴/딕셔너리clear, Map343→RenderGoldMines459→497→Subscribe519~538에서 static2구독이 누적된다. Bootstrap79 동일 renderer필드와 GameEvents1044/1055 staticSubject가 수명 근거다. 문서 담당은 renderer248~257/459~538/548~568을 직접 읽고 callback의 현재 딕셔너리 조회를 확인했으며 상위 전체경로는 전문 전달 근거로 구분한다. 배치UI308~311의 static ResourceRx 구독은 Close442~443에서만 해제돼 열린 채 씬 파괴 시 남는 수명 문제다. 실기 재현은 없으며 소스상의 수명 문제와 실행 관측을 구분한다.

쓰기 전2소스 전체원문/SHA snapshot을 보존하고 허용 bool/guard/flag·OnDestroy disposal만 역복원한 fullraw동일, 기존 구독 callback/AddTo(this)·API/직렬화·meta·표현수치/리터럴·overlay/권위 불변을 검증한다. 규칙/수치/자산/메모리/옛TaskHistory 변경이나 새컴파일/Unity/native/게임/빌드·TCQA 실행은 없다. AUD-017후속AI보류와6VFX삭제보류는 유지한다.

### 2.26 자율 후속: 폴백 재생성 참조5문서와 생성문구 정합화

현재 `_Reference/MapTemplateSource/MapTemplateSource_{Canyon,FullyOpen,ObstacleOpen,Outer,ThreeLane}.md`5문서45~46행은 삭제된 Editor 재생성 메뉴를 안내하고 Domain/Map/MapFallbackTemplateFactory.cs794~796의 Append가 같은 문단을 생성한다. 문서 담당이5문서 전문·Factory770~810을 직접 읽었고 Assets/Editor 메뉴/파일 검색에서 해당 Builder와 메뉴를 찾지 못했다. 메인도 해당 위치·메뉴 검색 결과를 독립 확인했다. WORKFLOW [5-2]의 최소 유지 기준과 필요할 때 도구를 다시 작성하는 방침이 근거다. 기존 도구 삭제를 취소하거나 새 도구를 만드는 계획이 아니다.

메인 전체계획검토/별도 GO 뒤 코드1파일은 game-programmer가, 문서5개는 document-manager가 동기화한다.5문서의 「다시 만드는 법」 본문 문단만 다음 정확 문구로 교체하고 Factory의 해당 문단 출력3개 문자열도 같은 본문을 내도록 수정한다:

> 현재 저장소에는 폴백 템플릿 재생성용 Editor 메뉴가 포함되어 있지 않다. 재생성이 필요하면 `MapFallbackTemplateFactory`(Domain, 순수 C#)를 직접 호출하는 도구를 별도로 준비한다. Unity 없이도 이 클래스를 호출할 수 있다.

Factory 해당 Append문 구성은 출력문단 문자열만 변경하고 newline 문맥을 보존한다. 메서드/API·맵 생성/검증·seed/PRNG/canonical·선택/폴백/InitialGold 로직과 기존 .bytes·크기/해시/날짜/기타수치를 보존한다. 문서별 나머지본문·수치·링크·기존meta를 보존하며 기존 메뉴 복원/실행·도구 신규 구현·실제 재생성은 하지 않는다.

쓰기 전6파일 전체원문/SHA snapshot을 보존하고5문서 정확문단치환 역복원fullraw동일, 코드 허용문자열3개 외 fullraw/실행토큰/기존리터럴동일·기존meta/바이너리보존을 확인한다. 새 로그/컴파일/Unity/native/빌드/게임·TCQA 실행 없이 안내 정정의 정적 증거만 기록한다. Research/Plan 외 규칙/수치/자산/메모리/옛TaskHistory·AUD-017후속AI보류/6VFX삭제보류는 변경하지 않는다.

### 2.27 자율 후속: 에셋 프롬프트 역할의 현행 맵 규격 사본1행 정정

`.claude/agents/asset-prompt-crafter.md`49행의 현재 역할 컨텍스트 `- **기본 맵**: FlatTop 10×29`를 정확 ``- **기본 맵**: 대전 맵 규격은 `Assets/_Project/Docs/GameSystemRules/GameSystemRules_RandomMap.md`를 참조한다.``로 교체한다. 문서 담당이 역할 전문·RandomMap 「무작위 맵 생성 규칙」 규칙1의11×21·GameConfig.cs FlatTop 기본11×21을 직접 읽었다. 이 행은 과거 검증 기록이 아닌 현재 제작 컨텍스트이고 기획수치를 바꾸지 않고 원본으로 안내한다. 메인이 후보 타당성을 확인했으며 전체 계획검토/별도 GO 뒤 문서 담당이 정확1행만 수정한다.

쓰기 전 전체원문/SHA snapshot과 정확1치환 역복원fullraw동일을 보존한다. 나머지 역할/에셋조건·메모리·규칙·수치·코드·옛TaskHistory는 변경하지 않는다. agents는 check_docs의 기본 memory-root `.claude/agent-memory` 밖이므로 토픽 메모리 보호기준선의 행수에 영향이 없다. baseline도구실행/JSON수정은 하지 않고 check_docs0/diffcheck0을 확인한다. 새컴파일/Unity/게임/TCQA실행 없이 현행 사실 안내만 정정한다.

### 2.28 자율 후속: 이동 중심·확정 표현 소유·힐 VFX 설명3소스 정정

전문 담당이 선정하고 메인이 계획 기록을 요청한3소스 주석만 정정한다. 문서 담당이 실제 oldspan·본문을 직접 읽었다. 근거는 GameSystemRules_Units.md 「이동 규칙」 규칙2의 중심 도달/소비 뒤 outgoing 선분 회전, GameSystemRules_UnitCombatSynchronization.md NET-FACING-001의 서버 checkpoint/trajectory 권위·NET-PRESENT-003의 정규 결과 키/표현 조정자, 같은 Units 「특수 공격 시스템 규칙」 규칙37의 HoT 종료 시 현재HP텍스트 계약이다. 아래 정확 문구를 코드 전에 기록하고 메인 전체계획검토/별도 GO 뒤 전문 담당이 수정한다.

| 소스(Assets/_Project/Scripts/ 기준) | 정확 허용 주석 교체 |
|---|---|
| Application/Combat/Sequencing/UnitMovementContracts.cs |750행 XML원칙1 `현재 waypoint 가까이에서는 다음 선분을 미리 보아 60도 방향 점프를 완만하게 만든다.`→`현재 waypoint 중심까지 현재 선분을 따라 진행하고, 다음 선분 방향은 그 중심을 소비한 뒤 적용한다.`. 원칙2~4와816~823의실제toWaypoint/desiredTangent본문불변 |
| Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs |821행 `// Comparison-only migration stage. This object has no gameplay or visual emitter callback.`→`// 확정 공격 표현 조정자와 소유권 조회를 공유한다.`. RegisterAuditEligibility992~993 XML2행은 `UnitView 초기화 시 manifest/runtime 계약의 채점 자격을 등록한다. 이 표는` / `read-only bridge 진입 분류와, 확정 표현 emitter 활성 시 OwnsAttacker의 소유권 판정에 쓰인다.` / `HitPresentationQueue는 이 판정으로 Legacy 중복 방출을 건너뛰며,` / `이 등록은 HP 적용을 수행하지 않는다.`의4행으로 교체 |
| Presentation/UI/FloatingHpTextSpawner.cs |ShowHeal XML287~289의힐VFX없음/미래PlayUnitHeal안내3행만 `이 메서드는 회복 HP 텍스트만 표시한다. BloomFairy의 지속 힐 VFX는` / `BloomHealEffectPresenter가 별도 채널 상태를 받아 EffectManager를 통해 재생한다.`의2행으로교체.284~285의현재HP표시문자열/규칙37계약은그대로보존 |

문서 담당은 Shadow825~828의UsesAuthoritativeEmitter+eligibility소유권과 HitPresentationQueue406~414/497의중복Legacy방출return, Bootstrap.Map274~281의Bloompresenter배선·Presenter108의StartBloomEffect·EffectManager383의GetHeal(BloomFairy)을 직접 대조했다. 최초 전달의Floating284~286근사범위는 실제oldspan287~289로 바로잡으며 허용범위를 넓히지 않는다.

쓰기전3전체원문/SHA snapshot을보존하고정확주석치환역복원fullraw동일·리터럴보존본문토큰/해시동일·기존public/API/직렬화/meta/자산/HPauthority/PRNG불변을확인한다. 새수치/규칙/로그·compile/Unity/native/게임/빌드·TCQA실행은없다.

추가정확근거를문서담당도직접대조했다: Units U-MOV-ALIGN 항1·3은현재중심진행/중심소비뒤outgoing선분입력을직접규정한다. 동기화 NET-ACTION-IDEMPOTENT·NET-PRESENT-003의단일writer전환/최대한번표현·NET-AUTH-002의HP즉시수렴과표현예약분리, Units 「특수 공격 시스템 규칙 — 힐러 확장」 규칙34의actual provenance/current-state/단일presenterloop소유도각주석의정확근거다. 새writer승격이나전체migration완료를추가하지않는다.

### 2.29 자율 후속: Bloom Editor fixture의 네트워크 상태 인수2행 정정

`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs`의기존BloomNetworkReadyFixture만수정한다. NetworkContext.Set의실제서명은 `(bool isServer, bool isActive)`이며문서담당도NetworkContext163~166과fixture1655~1726을직접읽었다. 메인이선정한정확2행은Client분기1686의 `NetworkContext.Set(true, false);`→`NetworkContext.Set(false, true);`, finally1719의 `NetworkContext.Set(netActive, netServer);`→`NetworkContext.Set(netServer, netActive);`이다. Host1667의true/true와나머지fixture본문/assertion은그대로둔다.

기존Client를active=false/server=true로잘못설정한표현과종료시원래값복원인수순서를실제서명에맞춘다. 근거는 GameSystemRules_UnitCombatSynchronization.md NET-AUTH-001의서버권위/클라이언트표현분리와동일역할상태를꾸미는기존fixture계약이다. runtime/생산/HP수정이나새fixture/검사진입점추가는아니다. 메인전체계획검토/별도 GO뒤game-programmer가구현한다.

쓰기전전체원문/SHA snapshot을보존하고정확2인수치환역복원fullraw동일·기존assertion/try-finally/서비스복원/meta보존을확인한다. 메뉴/fixture/compile/Unity/native/게임·TCQA는실행하지않고소스정정/정적대조만기록한다.

### 2.30 자율 후속: 연구 시작·취소RPC의 실제 연구소 권위 검증

`Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs`1파일에서sender팀인수검증에실제건물검증을추가한다. GameSystemRules_Upgrade.md규칙8의연구소파괴100%환불/서버권위·규칙9의연구소건물대상RPC·규칙13의건물유지수동취소/buildingId귀속과TechnicalDesignDocument_Network.md 「치팅 방어」의모든행동서버검증이근거다. 문서담당이해당원문과양RPC180~230/260~287을직접읽었다. 메인전체계획검토/별도GO뒤전문담당이구현한다.

private bool helper는 `IGameServices.GetBuildingPlacement()?.GetBuilding(buildingId)`로조회해 `building != null && building.IsAlive && building.Type == BuildingType.Research && building.Team == expectedTeam`만판정한다. RequestResearchServerRpc의기존team==sender검증직후/TryStartResearch골드차감전에helper가false면기존 `SendResearchFailed(senderClientId, "연구 불가")`후return한다. RequestCancelResearchServerRpc의기존team검증직후/CancelResearchByBuilding취소환불전에helper가false면return한다. 기존유효착수/환불/타이머/완료RPC/이벤트순서·수치/enum/API/직렬화는보존한다.

전문담당소스추적전달: NormalInput342~348자기팀/live→Research패널356~363, ResearchPanel175~178 Research확인/191team→268/292currentID+team이다. 파괴는Combat1967/1988·Placement330/333의OnDied→Remove,Setup469~476의권위/Research가드→UseCase355~357 OnLabDestroyed→379~403의원소유팀100%환불별도경로다. 따라서죽은labRPC거부가파괴환불을대신하거나막는설계가아니다. BuildingPlacement286~290 IDlookup/null·BuildingType.Research4공통종족·BuildingData45 Hp>0의IsAlive도전문근거이며실기재현은없다.

쓰기전전체원문/SHA snapshot을보존하고허용helper+2guard역복원fullraw동일·다른C#/meta/자산보존을확인한다. 새로그/실패UI/밸런스정책·규칙/수치/자산/메모리/옛TaskHistory수정·신규도구·compile/Unity/native/게임/빌드·TCQA실행은없다. AUD-017후속AI보류/6VFX삭제보류는유지한다.

### 2.31 자율 후속: 생산 RPC4곳의 실제 생산건물 소유권과 음수 취소 인덱스 검증

`Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs`1파일만수정한다. 전문담당의구체안과현RPC4곳·UnitProductionUseCase의등록/GetState/Enqueue/CancelQueueAt/CancelPendingSlot·ProductionState.Team의get-only정의를문서담당도직접읽었다. 기존teamIndex==expectedTeam검증은요청인수와발신자만대조하므로다른팀barracksId를막는실제상태검증을추가한다. 근거는TechnicalDesignDocument_Network.md「치팅 방어」의모든행동서버검증, GameSystemRules_BuildingPanelsUI.md「생산 패널 UI」규칙6/8/23의기존슬롯과취소환불계약이다. 새팀배정/슬롯/환불정책은추가하지않는다. 메인전체계획검토/별도GO뒤전문담당이구현한다.

| RPC | 정확 허용 변경 |
|---|---|
| RequestEnqueueServerRpc | 기존`var state = production.GetState(barracksId);`와null가드완료뒤/비용계산전에`if (state.Team != expectedTeam) { SendEnqueueFailed(senderClientId, "팀 불일치"); return; }`삽입. 기존실패문구재사용 |
| CancelSlotServerRpc | production null가드뒤/CancelQueueAt전에`var state = production.GetState(barracksId);`와`if (state == null || state.Team != expectedTeam || slotIndex < 0) return;`삽입 |
| SetRallyPointServerRpc | production null가드뒤/HexCoord target생성전에`var state = production.GetState(barracksId);`와`if (state == null || state.Team != expectedTeam) return;`삽입 |
| ToggleAutoServerRpc | production null가드뒤/unitType변환전에같은state조회/null·team가드삽입. 기존성공후`var state = production.GetState(barracksId);`만`state = production.GetState(barracksId);`로교체해동일scope중복선언을막고성공후재조회/알림순서를보존 |

등록된ProductionState는건물.Team을보존하며Enqueue는그state를기준으로차감한다. CancelQueueAt는0이아니면slotIndex-1을pendingIndex로넘기고CancelPendingSlot는상한만검사해음수접근가능성이있다. RPC입구의slotIndex<0은일반음수와int.MinValue의뺄셈전거부를포함하며실제예외재현결과는아니다. slotIndex상한·enum·종족/유닛종류검증은이번범위에추가하지않는다.

쓰기전전체원문/SHA snapshot을보존하고허용4가드/state선언·기존재조회1치환을역복원한fullraw동일과다른소스/meta/자산불변을검증한다. 기존sender/team검증·로그/실패문구·수치/API/직렬화·정상소유생산/취소/랠리/자동생산흐름을보존한다. 새로그/정책/밸런스·규칙/수치/메모리/이력수정·컴파일/Unity/native/게임/빌드·TCQA는없다. AUD-017후속AI보류·6VFX삭제보류를유지한다.

### 2.32 마지막 사실 정정: 기존 주석6파일·Inspector Tooltip1파일

마지막묶음은전문담당이제시한 `/tmp/hexiege_plan232_proposal.json`의정확7파일·14치환만수행한다. artifact는코드전각전체SHA와oldspan/newspan을포함하며그문구외추가후보탐색/수정은하지않는다. 메인전체계획검토/별도GO뒤전문담당이구현한다. 문서담당은proposal전문과Coordinator/NCC의실제승인·취소→writer입력/재진입본문,SkillExecutorRegistry의C등록과GlobalStatusChangeExecutor의4필드소비,check_docs의첫폴더판정·LogRules현행부담일반화금지를직접대조했다.

| 파일 | 허용 정정·근거 |
|---|---|
| Infrastructure/Network/UnitAttackShadowCoordinator.cs | 클래스XML2치환. 서버공격회차와Legacy타격예약의권위판정연결,Impact승인·취소가Legacy단일피해writer실행조건이며snapshot/result는진단·복제·표현에도전달하고직접HP적용은하지않는실제범위로설명. ResolveLegacyWriterAuthorization/취소소비와NCC writer입력근거 |
| Infrastructure/Network/NetworkCombatController.cs | XML3치환. 생성개체identity가공격회차·결과키/네트워크scope에쓰임,fixture우선/NGO/비활성시Unity시계가실제예약·Impact판정에쓰임,재진입provisional표현과TickCombat정지·정렬/timeline뒤실제ExecuteAttack을분리하는현본문으로정정 |
| Infrastructure/Config/SkillDefinition.cs | `_statusKind` Tooltip문자열정확1개를`(C) 상태 종류(정수 코드). GlobalStatusChangeExecutor가 StatusEffectKind로 변환해 사용한다.`로교체. Skills규칙13의기존타입C구현사실안내 |
| Application/Skill/SkillData.cs | 타입C제목/StatusKind/Magnitude/TargetsAllies의Phase1미사용·Phase2예정설명4곳만실행기의현재소비설명으로교체. 필드/API/값·Duration·규칙13참조는보존 |
| Assets/Editor/Tools/LogcatCapture.cs | 과거54KB근거의2행1span을과거측정예시/실제크기는기록량·파일수에따름/현재크기나장기보관비용으로일반화하지않음으로정정. 과거수치54KB와보존기간·기존수동정리안내/삭제/파일충돌로직은보존. LogRules현행계약근거 |
| Tools/check_docs.py | 제외설명1행만`.claude/`상대경로첫폴더이름으로루트직속하위트리를제외한다고정정. 함수본문/CLI/검사범위는보존 |
| Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs | 잘못붙은Bloom/Assault XML2span만각실제HoT채널/presenter/pool검사와Sniper생산클립·30fps1:22/3초검사설명으로교체. fixture본문/assertion/자산/수치/진입점은보존 |

근거규칙은기존UnitCombatSynchronization NET-ACTION-IDEMPOTENT/NET-PRESENT-003/NET-AUTH-002의writer·표현분리,Skills규칙13,LogRules현행보관안내다. 새writer승격·migration완료·정책변경을뜻하지않는다. 쓰기전전체원문/SHA를보존하고14정확치환역복원fullraw동일,주석파일의리터럴보존실행토큰/해시동일과Python AST동일,Tooltip소스는허용문자열1개역치환후본문동일을검증한다. 기존API/직렬화/meta/자산/동작·로그·상수·balance를보존한다. 넓은구조분리/unusedqueue·private값삭제/추가보안확장·새기획/수치/이력수정·컴파일/Unity/native/게임/빌드·TCQA는없다. AUD-017후속AI보류·6VFX삭제보류를유지하고이묶음뒤장부/문서검사/인계로마감한다.

### 2.33 Logcat 이름 상한 소진 시 기존 파일 반환 차단

사용자 「푸시하고 다음 작업 이어서」와 기존 자율 진행 승인 아래, 기존 후보 중 `Assets/Editor/Tools/LogcatCapture.cs` 한 파일만 다음 범위로 선정한다. GameSystemRules 인덱스를 확인했으며 게임 동작을 바꾸는 항목은 없다. 직접 근거는 LogRules §1.12의 덮어쓰기 금지 및 §1.10의 원본 보존 계약이다. 게임 규칙·보관 정책을 변경하지 않는다. 전문 제안과 현재 MakeNonOverwritingPath→경로 준비 catch→File.WriteAllText 순서를 문서 담당이 직접 대조했다. 메인 전체 계획 검토/GO 뒤 전문 담당이 구현한다.

현재 기본 이름과 `_2`부터 `_999`까지 모두 있으면 `index < 1000` 루프가 종료되어 존재하는 `_999` 경로가 반환된다. 기존 루프·상한·정상 이름 순서는 유지하고, 루프 직후 `File.Exists(candidate)`가 참일 때 `IOException("로그 파일 이름이 기본 이름부터 _999까지 모두 사용 중입니다. 기존 파일을 덮어쓰지 않도록 저장을 중단합니다.")`을 던진다. 기존 경로 준비 catch가 쓰기 전에 안내하고 return하도록 한다. catch 첫 문장을 `로그를 저장할 경로를 준비하지 못했습니다.\n\n`로, 마지막 안내를 `폴더 쓰기 권한과 경로 잠금 여부를 확인하세요. 같은 분의 파일 이름이 모두 사용 중이면 다음 분에 다시 저장하세요.`로 정정하며 경로/원인 표시와 return은 보존한다. 이는 이름 소진 실패 동작과 사용자 안내의 변경이다. 확인과 실제 쓰기 사이의 경쟁을 원자적으로 막는 보장은 추가하지 않는다.

쓰기 전 전체 원문/SHA와 정확 old/new span을 보존하고, 허용 guard·안내문 치환만 역복원했을 때 원문 전체 동일을 확인한다. 기본 이름 미점유, 기본 이름 점유, `_999`만 빈 경우 및 상한 모두 점유의 분기와 기존 catch가 쓰기보다 앞서는 순서를 정적으로 대조한다. API/직렬화/meta/자산·로그 헤더/내용/정상 저장 경로는 보존한다. 원시 로그 생성·삭제·업로드, 캡처/정리 메뉴 실행, 상한 확장, 운영 정책 변경, 컴파일/Unity/native/게임/빌드·TCQA는 수행하지 않는다.

### 2.34 oversized manifest 거부 검사의 기존 helper 재사용

2.33과 함께 `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` 한 파일의 oversized manifest 중복 블록만 정리한다. 근거는 GameSystemRules_UnitCombatSynchronization의 NET-AUTH-001/NET-AUTH-002가 정한 권위·표현 경계를 그대로 보존하는 진단 내부 정리 및 WORKFLOW [5-2]의 전투 검사 내부 의존 유지 원칙이다. 이 규칙들이 helper 추출이나 chunk 상한을 새로 요구한다는 뜻은 아니다. 메인 전체 계획 검토/GO 뒤 전문 담당이 구현한다.

현재 `oversizedRejected` 선언→reflection Invoke→한정 catch→Require 블록을 기존 `RequireB2ManifestRejected(buildPayloads, oversizedEntries, "A manifest requiring more than the reserved chunks must fail closed.")` 한 호출로 치환한다. 두 경로 모두 null target으로 동일 List<string> 객체를 object 배열 한 인자로 전달하며, `TargetInvocationException`의 InnerException이 `InvalidOperationException`인 경우만 거부 성공으로 처리한다. 정상 반환 시 동일 실패 문구의 Require 실패, 다른 예외의 전파, 직전/직후 검사 순서를 유지한다. 문서 담당이 현 블록과 기존 helper를 직접 대조했으며 구현 담당도 인자·필터·실패 문구의 동일성을 확인한 뒤에만 치환한다. `maximumChunks + 1` fixture 크기, 580자 payload, 상수·기존 helper 본문·다른 assertion/진입점은 변경하지 않는다. helper 호출로 실패 stack trace에 helper frame이 추가되거나 소스 줄 위치가 달라질 수 있으며, 이를 모든 진단 출력의 완전 동일로 확대하지 않는다.

쓰기 전 전체 원문/SHA·정확 치환 span을 보존하고 해당 호출을 기존 블록으로 역복원한 full raw 동일을 확인한다. 이번 범위는 기존 진단 중복 제거이며 전투 코드/HP/타이밍/수치/API/직렬화/meta/자산을 바꾸지 않는다. 컴파일·fixture 실행·Unity/native/게임/빌드·TCQA는 NOT RUN으로 남긴다. 두 계획 외 새 후보/대형 감사 확장·상태/로드맵/메모리 전파·추가 커밋/푸시는 수행하지 않으며 AUD-017 후속 AI 생산배율 및 legacy VFX6 삭제 보류를 유지한다.

### 2.35 RootPose 감사기의 소비 없는 저장값·인자 정리

메인은 후속 후보 중 `Assets/Editor/Tools/Combat/RunUnitRootPoseCrossAudit.cs` 정확1파일의6span만 다음 코드 범위로 선정했다. 전문 제안과 실제 호출/선언/메서드 본문을 문서 담당도 직접 읽었다. 근거는 GameSystemRules_UnitCombatSynchronization의 NET-ROOT-001/NET-ROOT-002 및 NET-ACTION-IDEMPOTENT가 정한 pose/identity 판정 계약 보존, WORKFLOW [5-2]의 현재 RootPose 검사 진입점 유지다. 이 규칙이 저장값 삭제를 요구한다는 뜻은 아니다. 메인이 이 계획 전체를 읽고 GO한 뒤 전문 담당이 구현한다.

| 수정 span(수정 전 소스 행) | 정확 변경 | 안전 근거·보존 사항 |
|---|---|---|
|1592|`run.EndLine = lineIndex;` 대입1행만 제거|EndLine은 선언/대입 외 읽기가 없고 RHS는 int local의 단순 읽기다. lineIndex loop·BEGIN/END 수·정렬·출력은 유지한다.|
|2281|`public int EndLine;` 선언1행만 제거|소유자 RunEvidence는2268의 private sealed class이며 Serializable/SerializeField가 없는 내부 증거 객체다. public 필드 표기만으로 외부 공개 API가 되지 않으며 반사/직렬화 소비 경로가 없음을 확인한 범위다.|
|1561|첫 SetInvariant 호출의 마지막 `, "sharedSessionKey"` 인자만 제거|제거 표현은 부작용 없는 문자열 literal이다. 직전 ReadValue의 실제 `"sharedSessionKey"` 파싱 키는 유지한다.|
|1563~1567|둘째 SetInvariant 호출의 마지막 `"sessionStartBucket"` 인자와 앞 comma만 제거|같은 literal 전달 제거다. 직전 ReadLong의 실제 `"sessionStartBucket"` 파싱 키는 유지한다.|
|1696~1700|ref string overload의 마지막 `string field` 매개변수와 앞 comma만 제거|field는 본문에서 읽지 않는다. candidate/current 검사와 ParseErrorCount 증가는 본문 원문 그대로 유지한다.|
|1713~1717|ref long? overload의 마지막 `string field` 매개변수와 앞 comma만 제거|같은 미사용 매개변수이며 본문 전체를 보존한다. ref string/ref long? 차이로 overload 구분을 유지한다.|

두 private static overload의 직접 호출은 각1곳이며 전문 담당의 Assets/Editor·Tools·Scripts 및 추적 source/config/Unity serializer 형식 검색에서 해당 내부 멤버를 외부에서 반사/직렬화해 소비하는 참조를 찾지 못했다. 문서 담당은 위 지정 소스를 직접 읽었으며 전체 검색 범위는 전문 전달 근거로 구분한다. 실제 파싱 키·ParseErrorCount·BEGIN/END 집계·role/identity 연결·endpoint/rotation 정렬·임계값·fixture·실패 문구·예외 경로·공용 메뉴/Analyze API는 바꾸지 않는다. 기능 분기·판정·로그 또는 파싱을 제거하는 작업이 아니라 소비 없는 private 저장과 literal 전달만 정리한다. WORKFLOW [4]의 행동 있는 기존 로직 제거/최종 삭제 gate를 우회하는 일반 승인으로 확대하지 않으며, Research16.28의 queue compaction 및 Coordinator private3값 보류를 재개하지 않는다.

수정 전 전체 원문/SHA와 정확6span old/new 치환을 보존하고, 허용 치환만 역복원하여 full raw 동일을 확인한다. SetInvariant 본문2개의 raw 동일·ReadValue/ReadLong 실제 키2개의 불변·외부 source/reflection 소비 참조 재검색·기존 meta 동일 및 정확1파일 diff를 정적으로 대조한다. 기존2.33/2.34 Editor2소스와 Task 기록에 간섭하지 않는다. 컴파일/fixture/Unity/native/로그 분석 실행/게임/빌드·TCQA는 NOT RUN이며 정적 비교를 실행 PASS로 보고하지 않는다. 새 Task/상시문서/메모리/Status/Roadmap/옛 Task·History 편집이나 Git 변경은 하지 않는다.

### 2.36 표현 bundle의 읽기 없는 지역 저장2행 제거

사용자 「푸시하고 다음작업진행」과 기존 전면 자율 진행·Task 기록 승인 아래 `Assets/_Project/Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs`의 TryCreate만 다음 범위로 선정한다. GameSystemRules 인덱스를 확인했으며 GameSystemRules_UnitCombatSynchronization의 NET-AUTH-001/NET-AUTH-002 권위·표현 경계를 그대로 유지하는 내부 정리다. 이 규칙이 지역 변수 삭제를 요구하는 것은 아니다. 전문 제안과 현재 TryCreate 본문·AppliedAttackPresentationFact readonly struct 선언을 문서 담당도 직접 읽었다. 메인의 전체 계획 검토/GO 뒤 코드 전문 담당이 구현한다.

수정 전54행 `AppliedAttackPresentationFact primaryAppliedFact = default;`와75행 `primaryAppliedFact = fact;` 두 행만 제거한다. 파일 전체의 해당 식별자는 선언/대입2곳뿐이라는 전문 검색 결과와 직접 읽은 본문을 근거로 한다. RHS는 이미 읽어 둔 fact local의 struct 복사이며 추가 호출·operator·부작용을 제거하지 않는다. `foundPrimaryAppliedFact` boolean과 중복 primary 거부·결과/HP/위치/팀/타입 비교·fact.IsValid·primary 누락 검증·결과 정렬·네트워크 변환 루프를 모두 보존한다. 실제 기능의 비활성화/폐기가 아닌 소비 없는 지역 저장 정리이며 Coordinator private3값 삭제나 queue compaction의 재개 승인이 아니다.

쓰기 전 전체 원문/SHA·정확2행을 보존하고 허용 제거만 역복원해 full raw 동일을 확인한다. 기존 boolean/검증/반환/API·직렬화/meta/수치/자산/예외/로그를 보존하며 실제 컴파일·Unity/native/fixture/게임/빌드·TCQA는 수행하지 않는다.

### 2.37 Observer 설명을 실제 Observer 선언 앞으로 원문 이동

`Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`의 수정 전112~117행 Tracer C summary6줄만329행 실제 `public static class UnitAttackShadowObserver` 직전으로 원문 이동한다. 현재 이 설명은 ExpiryClockHistory 자체 summary118~121행 앞에 잘못 붙어 있다. 문서 담당도 두 summary와 두 class 경계를 직접 확인했다. 근거는 GameSystemRules_UnitCombatSynchronization의 권위·표현 경계 설명을 현재 구현 주체에 맞추고 WORKFLOW [5-2]의 진단 진입점을 유지하는 사실 정리다. 메인 전체 계획 검토/GO 뒤 코드 전문 담당이 구현한다.

ExpiryClockHistory 자체 summary와 모든 코드·namespace·전처리 경계·attribute·상수/문자열·진단 예산/순서/출력을 보존한다. 같은 namespace/외곽 전처리 범위 안의 주석 이동이며 sourcegate가 검사하는 Decision 문자열과 금지 Animator member 유무를 바꾸지 않는다. 전문 담당은 기존 RunUnitActionSelfValidation의 해당 sourcegate를 직접 읽어 확인했고 문서 담당은 그 내용을 전달 근거로 구분한다. 수정 전 전체 원문/SHA를 보존하고 정확6줄 원문 이동의 역복원 raw 동일·주석 제외 토큰/리터럴 동일·전처리문과 기존 meta 동일을 정적으로 대조한다. 새 sourcegate/진단 정책이나 컴파일·native 실행은 추가하지 않는다.

### 2.38 Editor 로그 정리 확인창의 현행 private 보관 안내2문장 정정

`Assets/Editor/Tools/LogcatCapture.cs`의 ConfirmDeletion에서 수정 전731~732행 AppendLine 문자열2개만 아래로 치환한다. 직접 근거는 LogRules §1.10의 「원시 테스트 로그 공유·보관」이 정한 private Release ZIP 보관·전체 다운로드 검증과 보관 선택 파일의 대상별 사용자 삭제 승인이다. 문서 담당도 현행 조항과 현재 확인창/버튼을 직접 읽었다. 기존 「공유하려고 커밋해 둔 로그」 안내를 현행 정책에 맞추며 정책 자체는 바꾸지 않는다. 메인 전체 계획 검토/GO 뒤 코드 전문 담당이 구현한다.

```text
⚠️ 보관 대상으로 선택한 로그는 private Release ZIP 보관과 전체 다운로드 검증 후에도
   대상별 사용자 삭제 승인이 필요합니다. 조건 충족 여부와 삭제 목록을 확인한 뒤 진행하세요.
```

이는 사용자에게 표시되는 안내 변경이다. 자동 보관 검증·대상별 승인 확인 기능이나 새 삭제 허가 기능을 구현하는 것이 아니다. 실제 삭제 제어조건·30일/90일 선택·대상 폴더/총수 표시·경로 가드·삭제/취소 버튼·기존 정책 및 원시 로그를 보존한다. 쓰기 전 전체 원문/SHA와 정확2문자열을 보존하고 두 문자열만 역복원한 full raw 동일·다른 리터럴/실행 토큰 및 기존 meta 동일을 확인한다. 실제 캡처/저장/삭제/업로드·정리 메뉴 실행·컴파일/Unity/native/게임/빌드·TCQA는 하지 않는다. 이번 정확3파일 외 새 후보/대형 감사·상시문서/메모리/Status/Roadmap/옛 Task·History/Git 변경을 확대하지 않으며 AUD-017 후속 AI 생산배율·legacy VFX6 삭제·queue/Coordinator private3값 보류를 유지한다.

### 2.39 전송 bundle classifier의 소비 없는 private 순서 저장3행 제거

기존 전면 자율 진행 승인 아래 메인이 별도 검토를 요청한 `Assets/_Project/Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs`의 NetworkAttackPresentationBundleClassifier만 후속 범위로 선정한다.2.36~38 완료 기록은 그대로 보존하며 이 계획은 별도 코드 전 단계다. GameSystemRules_UnitCombatSynchronization의 NET-ACTION-IDEMPOTENT가 정한 멱등 표현 계약과 NET-AUTH-001/NET-AUTH-002의 권위 경계를 보존하는 내부 저장 정리다. 이 규칙이 queue 삭제를 요구하는 것은 아니다. 문서 담당도 classifier 전체 본문과 기존 ValidateTransportLongMatchRetention을 직접 읽었다. 메인의 전체 계획 검토/GO 뒤 코드 전문 담당이 구현한다.

2.36 반영 후213행의 private readonly `_order` Queue 선언,245행 `_order.Enqueue(scope);`,252행 `_order.Clear();` 정확3행만 제거한다. 이 class는 nonpartial plain sealed이며 전문 담당의 현재 source/Editor/Tools 검색에서 순서 조회·Dequeue/Peek/Count/enumeration·field escape와 quoted `_order`/nameof 반사 조회를 찾지 못했다. 외부 소비는 NetworkCombatController와 기존 검사의 public 생성/Classify/Clear 경로라는 전문 전달 근거다. 멱등/충돌/상한 정책은 `_accepted`의 TryGetValue→길이/각 payload.Equals 비교, Count→MaximumRememberedBundles 비교, Add(scope,payload.Clone()), Clear에 있으며 해당 본문 전체를 보존한다. 상한32768·scope/hash/키/정렬/RPC/API/직렬화/meta/자산은 변경하지 않는다.

명시적으로 queue 객체/내부 저장 할당과 Enqueue/Clear 관리만 없어지며 OOM 등 자원 실패의 동등성이나 모든 관측의 완전 동일을 주장하지 않는다. 기존 검사는1964묶음 Accepted→옛 payload Duplicate→Clear 후 Accepted를 public 경로로 확인하며 private queue를 참조하지 않는다. 해당 검사는 이번에 실행하지 않는다. 보류된 UnitAttackShadowCoordinator의 예약 FIFO/동일 instance·token 재사용 반례 및 private3값과 별도 타입/상태이며 그 보류를 재개하거나 우회하지 않는다.

쓰기 전 현재 전체 원문/SHA와 정확3행을 별도 보존하고 허용 제거만 역복원한 full raw 동일, `_accepted` 정책 본문/공개 API·상수/문자열·기존 meta 동일 및 정확1source 추가 diff를 정적으로 대조한다. 앞선2.36의 지역 저장2행 제거와2.37/2.38 결과를 보존한다. 컴파일/fixture/Unity/native/게임/빌드·TCQA·로그 작업/삭제 메뉴/Git 변경은 수행하지 않으며 새 파일/대형 감사·상시문서/메모리/옛 Task·History 전파 없이 기록한다. AUD-017 후속 AI 생산배율·legacy VFX6 삭제 보류를 유지한다.

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

### 7.25 직전 푸시 후 컴파일 확인·잔여 감사 계속(2026-10-24 KST)

사용자 「컴파일 에러없으니 다음진행하자」로 직전71경로 커밋·푸시 `1a69402ce039c4fd1c34373babe048ed37e36e4f` 이후 컴파일 오류 없음 보고를 받았다. Research16.13의 검증표를 최신 근거로 삼으며 플랫폼·로그·전체 게임 실기 증거 미제공과 에이전트 새 실행 없음을 구분한다. 과거 각 회차의 NOT RUN은 당시 실행 기록으로 보존한다.

직접 확인한 후속 착수 상태는 `work`/위 HEAD/clean이며 이번 Research/Plan 기록 이후 추가 커밋·푸시는 아직 하지 않았다.7.24/Research16.12의71경로 목록은 직전 회차의 과거 상태다. 최신 소스337개 전체 의미 감사는 미완이며 기존 전면 진행·Task 기록 승인으로 남은 본문을 계속 읽는다. 구체 후보 수신→메인 검토→2.22 이하 코드 전 계획 기록→메인 구현 신호 순서를 지킨다. AUD-017 후속 AI 보류·6VFX 삭제 보류·게임 정책/밸런스를 유지하고 새 Task/TCQA·Status/Roadmap/메모리·옛 Task/History는 이번 컴파일 확인으로 수정하지 않는다.

### 7.26 검사기 토픽 현재 상태 안내1곳 결과(2026-10-24 KST)

메인 승인2.23을 먼저 기록한 뒤 check-docs-tool.md의과거한계heading직후 English note1곳만 추가했다. 과거원문/SHA·정확삽입역복원fullraw동일·기존heading/anchor보존은 Research16.14와 `/tmp/hexiege-plan223-comparison.json`으로 연결한다.126→128행이며 baseline은 도구 직전 감소0/game-programmer+2행·파일수동일 확인 뒤 `--update-baseline` exit0으로 증가만 반영했다. 다른 토픽·직접JSON편집·Status/Roadmap/옛TaskHistory 변경은 없다. 현재parser의실제정의확인과검사범위안내일뿐 새컴파일/게임실기증거가아니며16.13사용자보고를별도유지한다.

### 7.27 4소스 주석 정정 결과(2026-10-24 KST)

2.22의4C#를 메인 전체계획검토/구현신호 뒤 전문 담당이 수정했다. Research16.15의정확파일/oldspan·전후행수와comparison/diff/verify artifact를근거로기록한다. 코드전전달근사행번호는실제span으로정정했다. 문서담당은전체diff/JSON을직접읽었고4파일토큰/리터럴동일·변경행전부주석·전체raw역복원·source/meta690중허용4만변경/나머지686동일을전문검증근거로구분했다. 메인독립검토와새컴파일/게임실기는별도축이며새실행은없다. runtime/API/metadata문자열·수치·정책·자산·AI후속보류/6VFX삭제보류는유지한다.

메인 독립 결과도 수신해 Research16.15에 연결했다. `/tmp/hexiege-plan222-main-independent.json`은4파일변경diff행모두주석·전체행주석제외원바이트동일·337C#중정확4변경333동일/신규없음을 확인한다. 전문 scanner와 별도 방식의 정적 대조이며 새컴파일/게임실기 증거가 아니다.

### 7.28 TDD 초기 자원 설명2곳 정정 결과(2026-10-24 KST)

2.24를 메인 전체계획검토/구현신호 뒤 반영했다. TDD927/1066의 정확2치환만 수행하고 기존3링크·1202행·다른본문을 보존했다. Research16.16과 `/tmp/hexiege-plan224-docs-before.json`/`-tdd-before.raw`/`-comparison.json`/`-docs.diff`가 전체원문/SHA·역복원raw동일 근거다. 현재StartingGold소비와 InitialGold실제연결후속을 구분했으며 check_docs문제0/diffcheck0이다. 메인독립최종검토는별도수신대기이며 코드/수치/규칙/자산/다른메모리/옛TaskHistory 변경·새컴파일/게임실기는없다. AUD-017후속AI보류·6VFX삭제보류를유지한다.

메인 독립 `/tmp/hexiege-plan224-main-independent.json`을 수신해 직접 읽었다. 정확2치환 외 원문동일·모든Markdown링크보존·변경행927/1066·1202행을 별도 확인한 정적 근거이며 새실행증거가 아니다.

### 7.29 폴백 재생성 안내5문서 정정 단계(2026-10-24 KST)

2.26 전체계획 메인검토·문서GO 뒤5참조문서의현행재생성안내문단만교체했다. Research16.17·`/tmp/hexiege-plan226-docs-before.json`/`-docs-comparison.json`/`-docs.diff`에원문/SHA·정확치환·5raw역복원동일·각53→52행과다른본문불변을기록했다. check_docs0/diffcheck0이며Factory1소스는별도전문구현/메인검토대기다. 문서담당코드쓰기·바이너리재생성·메뉴복원·새도구/컴파일/Unity/게임/TCQA실행은없다.

코드 전문 구현과 메인 독립 검토도 완료됐다. 문서 담당은 Factory794~796의 최종3Append와 `/tmp/hexiege-plan226-code-comparison.json`·`/tmp/hexiege-plan226-main-independent.json`을 직접 읽었다. 전문 증거는 허용3literal역복원fullraw동일·Append103개보존·기존meta3268/맵binary5보호이며, 메인 독립은 정확3literal외원문동일·Append3내용추출과5문서실제문단일치·맵생성로직무변경이다. 문단을2행에서1행으로 합쳐 출력 newline escape는55→54로 줄었으며 다른 출력은 보존했다. 실제Factory실행·바이너리재생성·컴파일/게임실기 증거가 아니다.

### 7.30 광산 표현·배치 UI 구독 수명2소스 정리 결과(2026-10-24 KST)

2.25 메인계획검토/GO뒤전문구현과메인독립대조를완료했다. Research16.18·`/tmp/hexiege-plan225-comparison.json`·`-main-independent.json`이2소스삽입만/기존원문보존·역복원fullraw동일·기존callback/AddTo/Close/overlay불변근거다. privatebool/guard/구독뒤flag와OnDestroyDispose/null만추가했고public/직렬화/meta/표현수치/정책은보존했다. compile/Unity/게임·TCQA실행은없다.

### 7.31 에셋 역할의 현행 맵 규격 안내1행 결과(2026-10-24 KST)

2.27 전체계획검토/GO뒤asset-prompt-crafter역할49행만원본규칙안내로정정했다. Research16.19와`/tmp/hexiege-plan227-before.json`/`-comparison.json`에120행원문/SHA·정확1행치환·역복원raw동일을기록했다. baseline영향/갱신없음·check_docs0/diffcheck0이며메인독립`/tmp/hexiege-plan227-main-independent.json`도정확1행치환외나머지원문동일을확인했다. 새수치/코드/정책/다른메모리·컴파일/실기/TCQA변경은없다.

### 7.32 주석3소스·Bloom fixture·연구RPC 구현 결과(2026-10-24 KST)

2.28~30의메인전체검토/GO뒤전문담당이정확5소스를구현했다. Research16.21에실제변경·근거경계를기록했다. `/tmp/hexiege_code_finish_20261024/`의5원문/SHA·허용치환역복원동일·주석3토큰동일/control·보호5491파일중정확5변경과메인의`/tmp/hexiege-plan228-230-main-independent.json`각독립검토/역복원동일을직접읽었다. fixture2인수만정정했고연구RPC는실제live/Research/소유팀검증helper와mutation전2guard만삽입했다. 기존public/API/직렬화/meta/자산·파괴환불경로를보존한다. 실제컴파일/fixture/Unity/native/게임/빌드·TCQA는NOT RUN이며직전사용자컴파일보고의검증범위를확대하지않는다.

### 7.33 생산RPC4곳 소유권·음수슬롯 검증 결과(2026-10-24 KST)

2.31코드전계획/메인전체검토/GO뒤NetworkProductionController.cs1파일의4guard/state조회와Toggle성공후var제거만구현했다. Research16.22와`/tmp/hexiege_plan231_20261024/`에전후전체원문/SHA·정확diff·허용5치환역복원fullraw동일·5491모집단중해당1파일만변경을기록했다. 문서담당도diff/receipt와메인독립`/tmp/hexiege-plan231-main-independent.json`을직접읽었다. 독립결과는정확역복원raw동일/4guard가mutation앞/음수인덱스뺄셈전거부를확인한다. 기존sender팀검증·정상소유흐름·성공후재조회/알림·로그/문자열·API/직렬화/meta/자산보존을유지한다. 새컴파일/Unity/native/게임/빌드·TCQA는NOT RUN이고실제예외/공격재현증거로확대하지않는다.

### 7.34 마지막7파일 사실 설명 정정 결과(2026-10-24 KST)

2.32전체계획/정확proposal메인검토·GO뒤7파일14치환을구현하고소스를동결했다. Research16.24와`/tmp/hexiege_plan232_20261024/`에전후원문/SHA·역복원fullraw동일·5C#주석토큰동일/Python AST동일/Tooltip정확1문자열외토큰동일·4823보호모집단중정확7변경을기록했다. 문서담당도전체diff/receipt와메인독립`/tmp/hexiege-refactoring-final-main.json`을직접읽어정확14치환/주석본문raw/PythonAST/Tooltip표시1문자열만변경을확인했다. InspectorTooltip표시변경을명시하고모든관측동작동일로확대하지않는다. 추가후보탐색/소스수정은끝내고장부·문서검사·인계만마감한다. 컴파일/Unity/native/게임/빌드·TCQA는NOT RUN이다.

### 7.35 선정 묶음 마감과 미검증·보류 인계(2026-10-24 KST)

검토대상경로누락해소와선정된2.22~32의구현/문서정정을마감했다. Research16.23~25가리비전별읽기범위·정확현재30경로/유형·known open·실행한계·미커밋/미푸시상태의상세원본이다. 메인최종독립은Assets C#337집합추가삭제0/체크포인트이후18C#변경,선택보호meta/자산3617개SHA동일을확인한다. 이모집단을다른회차보호수와혼용하지않는다. 현행문서/역할전문읽기와광범위이력/메모리미독,직접읽기와전체의미/모든기능검증을구분한다.

정확후속30경로는C#18/Python1/문서·메모리·역할10/기준선JSON1이며모두기존추적파일unstaged수정이다. 새커밋/푸시는하지않았다. 기존사용자컴파일보고는직전1a69402c묶음이고이번후속새컴파일/Unity/native/게임/빌드·TCQA는NOT RUN이다. 전체리팩토링/모든역사·주장/게임기능완료로승격하지않는다. queue/private값·Logcat희귀충돌등잔여후보는기록만보존하고AUD-017후속AI/6VFX삭제보류를유지한다. 추가소스수정/후보탐색없이인계한다.

최종문서검사문제0/diffcheck0이며검사기제외Task2파일은별도상대Markdown링크1092개파일존재확인누락0/앵커포함0을확인했다. 정적검사나링크존재가모든의미/실행검증은아니다.

### 7.36 기존 묶음 푸시와 다음 코드 전 계획(2026-10-25 KST)

사용자 「푸시하고 다음 작업 이어서」 아래 기존30파일은 `e1588fabd3f4b583b3729d3021533957570cb345`로 커밋되어 origin/work에 푸시됐다. Research16.26이 직접 확인한 local HEAD/30파일 stat와 메인이 전달한 원격 동일 SHA·푸시 직후 clean의 근거 원본이다. 과거16.25/7.35의 미커밋·미푸시 기록은 당시 상태로 보존한다. 기존 사용자 컴파일 보고는1a69402c 범위이며 이번 푸시에 대한 새 컴파일/실기 보고는 없다. 컴파일/Unity/native/게임/빌드·TCQA는 새로 수행하지 않았다.

메인이 기존 후보 중 정확2파일만 다음 범위로 선정했고 전문 제안과 실제 호출/예외 경로를 대조해2.33/2.34를 코드 전에 기록했다. 2.33은 기존999 이름 상한에서 점유된 경로 반환을 막고 기존 catch로 쓰기 전 중단하며 안내를 정정한다. 2.34는 동일 인자/예외 필터/실패 문구를 보존해 oversized manifest 검사를 기존 helper로 합친다. 메인 전체 계획 검토/GO 뒤 전문 담당이 구현하며 이 절은 구현 완료나 실제 실행 PASS가 아니다. 기록 외 신규 후보 확대/정책·게임수치 변경/원시 로그 작업/추가 커밋·푸시는 하지 않는다. AUD-017 후속 AI 생산배율 및 VFX6 legacy 삭제 보류를 유지한다. 이번 문서검사 결과는 Research16.26의 후속 문단에 기록한다.

### 7.37 다음2파일 구현·정적 대조와 추가분 인계(2026-10-25 KST)

2.33/2.34의 메인 검토/GO 뒤 정확2소스 구현과 동결을 마쳤다. Research16.27에 실제 변경과 전문 원문/SHA/역복원 근거 및 메인 독립 대조를 기록했다. 문서 담당도 전체 source diff/manifest/static-review와 `/tmp/hexiege-next-main-independent.json`을 직접 읽었다. Logcat은 상한 점유 guard·안내2span만 변경하며 기존 catch가 쓰기 전에 return한다. Combat은 정확1블록을 기존 helper로 치환하고 원래 인자로 확장했을 때 옛 블록의 리터럴 보존 토큰과 동일하다. File.Exists/쓰기 경쟁의 비원자성과 실패 stack trace helper frame/줄 위치 변화 가능성을 유지한다. 실제 저장/예외/fixture 실행 근거로 확대하지 않는다.

현재 변경은 C#2+기존 Task2의 정확4경로이며 추가분은 미커밋·미푸시다. 컴파일/fixture/Unity/native/게임/빌드·TCQA는 NOT RUN이며 이전1a69402c 사용자 컴파일 보고와 구분한다. 검사 마감은 check_docs 문제0/diffcheck0/별도 Task 상대링크1092개 파일누락0·앵커0이며 전체 의미나 기능 PASS가 아니다. 추가 후보/대형 감사 확대·로그 작업·상시문서/메모리 전파 없이 인계하며 AUD-017 후속 AI 생산배율과 legacy VFX6 삭제 보류를 유지한다.

### 7.38 queue 보류와 RootPose 비소비 데이터 정리 결과(2026-10-25 KST)

Research16.28의 동일 instance/token 재사용 반례를 검토해 queue compaction과 Coordinator private3값 삭제는 보류하고 원코드를 유지했다. 이어 별도 선정한2.35를 코드 전 기록·메인 전체 검토/GO 뒤 전문 담당이 구현했다. Research16.29~30과 `/tmp/hexiege_plan235_20261024/static-review.json`·`/tmp/hexiege-plan235-main-independent.json`이 정확1파일6span 치환/역복원 full raw 동일·두 SetInvariant 본문 raw 동일·실제 파싱 키2문장/기존 meta 유지·queue 무변경 근거다. 문서 담당도 전체 diff/두 JSON을 직접 읽었으며 전문 검증의 generic 키식 횟수 가정 정정은 추가 소스 수정 없이 이뤄졌음을 구분했다.

현재 추가분은 Editor C#3+기존 Task2의 정확5경로이며 미커밋·미푸시다. 공개 API/직렬화 계약·판정/파싱/예외/로그·기존2.33/2.34 변경은 보존했다. 새로운 컴파일/fixture/Unity/native/로그 분석/게임/빌드·TCQA는 NOT RUN이며 기존1a69402c 사용자 보고를 재사용하지 않는다. queue bounded 완료/전체 안전이나 전체 기능 PASS로 확대하지 않고 AUD-017 후속 AI 생산배율·legacy VFX6 삭제 보류를 유지한다. 추가 후보 탐색/상시문서·메모리 전파/Git 변경 없이 문서 검사와 인계를 마감한다.

### 7.39 기존5경로 푸시와 다음3파일 코드 전 계획(2026-10-25 KST)

사용자 「푸시하고 다음작업진행」 아래 기존5경로의 커밋/푸시 `469fda010bb4c5947c1254ef8e7281f7fe588af7`를 Research16.31에 기록했다. 문서 담당의 commit stat 직접 확인과 메인의 원격 동일 SHA·푸시 직후 clean 전달값을 구분하며16.30/7.38의 당시 미커밋 기록을 보존한다. 기존1a69402c 사용자 컴파일 보고를 e1588fab/469fda01 및 이후 세대로 확대하지 않는다. 새 사용자 컴파일 보고와 에이전트 컴파일/Unity/native/실기/빌드·TCQA 실행은 없다.

전문 구체안과 실제 소스/LogRules §1.10을 대조해2.36~38을 코드 전에 기록했다. 정확3파일의 지역 저장2행 제거·summary6줄 원문 이동·확인창2문자열 정정이며 메인 전체 계획 검토/GO 뒤 전문 담당이 구현한다. 실제 primary 검증·Observer 코드/전처리·삭제 제어/기간/목록/버튼·원시 로그/정책은 보존한다. 문구 정정을 자동 보관 검증/새 승인 기능이나 실행 PASS로 확대하지 않는다. AUD-017 후속 AI 생산배율·legacy VFX6 삭제·queue/Coordinator private3값 보류를 유지하며 그 외 범위를 확대하지 않는다. 이번 문서 검사 결과는 Research16.31의 후속 문단에 기록한다.

### 7.40 다음3소스 정리 결과와 미커밋 추가분 인계(2026-10-25 KST)

2.36~38의 코드 전 계획·메인 전체 검토/GO 뒤 정확3소스 구현과 동결을 마쳤다. Research16.32와 `/tmp/hexiege_plan236_238_20261024/static-review.json`·`/tmp/hexiege-plan236-238-main-independent.json`에3source 전체 diff·전후 SHA·허용 치환 역복원 full raw 동일/기존 meta 동일 및 메인 HEAD469fda01 기반 독립 대조를 구분해 기록했다. 문서 담당도 diff/두 artifact를 직접 읽었다. Bundle의 비소비 저장2행·Observer 원문6줄 이동·Logcat 표시2문자열만 바꿨으며 실제 검증/동작/삭제조건·기간/목록/버튼·원시 로그/정책은 유지했다. 안내 정정은 자동 보관 검증이나 새 승인 기능의 구현이 아니다.

현재 추가분은 Source3+기존 Task2의 정확5경로이며 미커밋·미푸시다.469fda01의 기존 푸시와 별개이며 새 사용자 컴파일 보고/에이전트 컴파일·fixture/Unity/native/실기/빌드·TCQA는 없다. 기존1a69402c 보고를 이 세대 검증으로 확대하지 않는다. 로그 작업·삭제 메뉴·새 범위/대형 감사·상시문서/메모리 전파/Git 변경 없이 마감하며 AUD-017 후속 AI 생산배율·legacy VFX6 삭제·queue/Coordinator private3값 보류를 유지한다. 문서 검사 마감은 Research16.32에 기록한다.

### 7.41 classifier 비소비 순서 저장 정리와 최종 추가분 인계(2026-10-25 KST)

2.36~38 완료 기록을 유지한 채 별도2.39 코드 전 계획/메인 전체 검토·GO 뒤 같은 Bundle classifier의 `_order` 선언/Enqueue/Clear3행만 제거했다. Research16.34와 `/tmp/hexiege_plan239_20261024/static-review.json`·`/tmp/hexiege-plan239-main-independent.json`이 직전 소스 기준 정확3행 역복원 full raw 동일·accepted 정책/기존 meta/다른2source 보존 및 HEAD469fda01 기준 누적5행 제거 외 전체원문 동일의 근거다. 문서 담당도 diff/두 artifact를 직접 읽었다. 코드 전 문서의 보류 FIFO 타입명 오기는 UnitAttackShadowCoordinator로 정정했으며 실제 보류 대상 변화는 없다.

상한32768·멱등/충돌/초기화 정책은 유지했고 queue 자원 할당/저장 관리 제거와 OOM 등 자원 실패 동등성 미주장을 명시한다. Source3+기존 Task2 정확5경로 추가분은 미커밋·미푸시이며469fda01 기존 푸시와 구분한다. 새 컴파일/fixture/Unity/native/게임/빌드·TCQA·로그 작업/삭제 메뉴는 NOT RUN이다. 보호 Coordinator FIFO/ABA·private3값·AUD-017 후속 AI 생산배율·legacy VFX6 삭제 보류를 유지한다. 소스 동결·추가 후보/새 범위 없이 문서 검사를 마감하며 최종 결과는 Research16.34에 기록한다.

위 5경로 목록과 미커밋·미푸시 설명은 커밋 전 정적 검증 시점의 기록이다. 메인은 이번 사용자의 푸시 지시 범위에서 후속 완료분도 이어서 커밋·푸시한다. 실제 성공 여부와 원격 SHA는 메인 실행 결과로 확인하며, 이 시점 설명은 새 컴파일이나 실기 검증을 뜻하지 않는다.
