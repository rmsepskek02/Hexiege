# Game System Rules — 경기 운영

- **목적·답하는 질문:** 최초 경기·재경기의 맵 준비와 실패·이탈 복구 흐름을 판단한다.
- **포함:** 최종 맵 전송, 재경기 선택, timeout·재전송·실패 복구, 결과 화면·재로드 중 이탈 계약
- **제외·담당 문서:** 생성 사양은 [RandomMap](GameSystemRules_RandomMap.md), 화면은 [MatchFlowUI](GameSystemRules_MatchFlowUI.md), 승패·역할 원칙은 [Network](GameSystemRules_Network.md), 데이터 구조는 [네트워크 TDD](../TechnicalDesignDocument_Network.md)
- **권위원본·주요 참조:** 경기 운영 규칙 원본; 기존 RandomMap 6장 제목·번호는 유지
- **갱신 조건·방법:** 운영 동작 승인 변경 때 현재 계약·예외·필요 이유를 갱신한다. 단계별 코드/실기 원문은 History/Task, 현재 미완 범위는 [ROADMAP](../ROADMAP.md)에 연결한다.

생성 사양·용어는 [RandomMap](GameSystemRules_RandomMap.md)이 원본이다. 이 문서의 운영 규칙은14·16~19이며 마지막 생성 규칙 참조 절은 이전 번호 연결 안내다. 분리 당시 문맥 설명은 [이전 원문](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-round2-rules-matchflow-01)다.

## 무작위 맵 생성 규칙

### 6장. 네트워크 — 만든 맵을 어떻게 전달하고 재경기에서 어떻게 고르는가

**규칙 16. 멀티플레이 맵 전송과 실패 복구**

- Host는 검증된 최종 맵 전체 데이터와 seed·유형·중립 광산 수·시작 광산 방향·실제 초기 골드를 Client에 전달한다. 테스트 모드 표식은 전달하지 않는다.
- Client는 전달된 최종 맵을 그대로 로드하며 별도로 추첨·재생성·폴백하지 않는다. 최종 해시가 같을 때만 전투 씬 전환을 시작한다.
- 준비 실패·해시 불일치에서는 전투 씬으로 가지 않고 불일치를 기록한다. 최초 멀티는 양쪽이 방을 나가 로비 메인으로, 재경기는 기존 결과 화면·맵 정의를 유지한다.
- 전투 씬은 확정 맵만 구성하며 양측 씬 로드 완료 후 시뮬레이션을 시작한다. 최초/재경기는 씬에 종속되지 않는 공용 전송 경로를 사용한다.
- 모든 조각이 모여 선언 크기가 일치한 뒤 해시→역직렬화→공정성 검증 순서로 처리한다. 부분 데이터를 검증·구성에 쓰지 않는다.
- MapVersion은 canonical 형식 식별·미지원 형식 차단용이며 불일치는 준비 실패다. matchmaking·앱 업데이트·전역 호환 판정에 쓰지 않는다. 형식/조각/중복·역순 재조립은 [네트워크 TDD](../TechnicalDesignDocument_Network.md)가 담당한다.
- 조각·전체 한도는 RPC 실효 payload/Relay MTU 근거로 정하며 근거 없는 잠정값을 임의 다른 숫자로 바꾸지 않는다. 현재 미완 실측은 [ROADMAP](../ROADMAP.md), 초기 실패화면 한정·테스트 모드 제거·한도 근거 당시 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-01)다.

### 매칭 성립 후 최초 접속 준비 상한

랜덤 매칭이 Found가 되어 `게임에 접속하는 중...` 로딩을 켜는 순간부터 최초 Game 씬 전환을 시작하는 순간까지의 접속 준비는 최대 30초다. 상대가 아직 접속하지 않았거나 실패 통보를 전달할 수 없어도 각자의 해당 구간을 이 상한 안에 종료한다. 실패를 먼저 확인하면 30초를 기다리지 않고 즉시 로딩을 해제하고 해당 회차의 연결/방을 정리하며 [경기 흐름 UI](GameSystemRules_MatchFlowUI.md) 「공통 UI 규칙」 규칙 M-2의 실패 팝업/복귀를 적용한다. 상대에게 전달 가능한 경로가 있으면 실패를 알리되, 미전달을 이유로 로컬 복구를 늦추지 않는다.

이 상한은 매칭 상대를 찾는 티켓 대기 시간이나 아래 맵 package 응답 대기/재전송 시간, 전투 중 이탈/재경기/결과 화면 타이머와 다르다. 기존 각 시간과 규칙 번호는 변경하지 않는다. 씬 전환 시작 뒤에는 기존 맵/씬 gate와 경기 운영 계약을 따른다.

### timeout, 재전송, 실패 복구

- Host는 활성 준비 작업 응답을10초 기다린다. timeout 또는 조각 불완전 수신에만 같은 작업 전체 데이터를1회 재전송하며 두 번째10초에도 준비되지 않으면 실패한다.
- 이10초는 맵 package 준비 응답 대기이며 연결 자체의 전송 계층 timeout과 다른 수명이다. 전송 응답 대기는 전송 성공 시간, 연결 timeout은 결과화면 이탈 판정이 먼저 나야 한다는 별도 근거로 조정하며 서로 따라 바꾸지 않는다. 전송 계층 값·설정 위치는 [네트워크 TDD](../TechnicalDesignDocument_Network.md)를 따른다.
- 미지원 MapVersion·용량 초과·해시 불일치·역직렬화/공정성 실패·연결 끊김은 즉시 실패하고 재전송하지 않는다. 어떤 실패에서도 전투 씬으로 가지 않으며 seed·유형·내부 code는 로그에만 둔다.
- 최초 멀티 실패는 loading을 닫고 양쪽이 즉시 방을 나가며 재시도하지 않는다. 화면·문구·버튼은 [MatchFlowUI](GameSystemRules_MatchFlowUI.md) M-2를 따른다.
- 재경기 실패는 이전 MapDefinition을 유지하고 rematch pending을 초기화한 뒤 결과 UI·전체 길이 자동 복귀 countdown·선택지를 복원한다. 다시하기 활성 예외는 같은 UI 원본 D-2를 따른다.
- 싱글 최초/재경기 실패의 화면·재시도·timer 제외는 같은 UI 원본 M-4다. 실패 UI 확정과 과거 미정·두 버튼/연결유지 폐기·시계 구분 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-02), 현재 구현·실기 범위는 [PROJECT_STATUS](../PROJECT_STATUS.md)를 따른다.

---

**규칙 14. 재경기 맵 선택**

기존 재경기의 요청·수락·거절과 Game 씬 재로드 흐름은 유지한다. **재경기는 맵 조건을 고르지 않는다 — 매 판 새 맵이 무작위로 결정된다.** 재경기가 성립하면 Host가 새 `MapDefinition`을 생성·검증해 교체한다.

상대방의 재경기 팝업은 모달 (Modal) 타입이므로 배경을 탭해도 닫히지 않는다. 팝업 타입 구분의 단일 소스는 `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 8 이며, 이 팝업이 Modal 모드라는 지정은 같은 문서 「공통 UI 규칙」 규칙 5 의 반투명 배경 오버레이 단일 소유 패턴 절이다.

같은 맵/새 맵 조건·mode 선택·조건 표시 UI는 제공하지 않는다. 양쪽이 각자 재경기를 요청하면 상호 동의로 성립할 수 있다. 직전 맵과 우연히 같은 전장이 나와도 재추첨하지 않는다. canonical hash에는 seed가 포함돼 지형 동일성 검사가 아니며 고정 폴백의 사용을 숨기는 재시도로 쓰지 않는다. 폴백 사용은 기존 로그로 드러낸다. 옛 조건 선택·hash 충돌 기각·코드 실측 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-03)다.

### 재경기 맵

- Game 종료 결과 화면을 유지한 상태에서 Host가 새 맵을 생성한다.
- 팀, 종족과 그 밖의 매치 설정은 직전 경기 값을 그대로 유지한다.
- 새 64-bit root seed를 만든 뒤 맵 관련 값만 다시 선택한다. `MapType`, 허용 범위의 `NeutralMineCount`, `StartingMineSide`를 새로 추첨하고, 지형 세부 형태·중립 광산 위치를 새 seed에서 생성한다. Host는 **새로 뽑은 중립 광산 수에서 규칙 3 의 표대로** 실제 초기 골드를 확정해 새 canonical 정의에 기록한다. 장식 placement 활성화 이후에는 장식도 새로 생성하지만 최초 구현은 빈 목록을 유지한다.
- 새 package를 공용 `NetworkMapTransfer`로 Client에 전송하고 SHA-256 및 semantic fairness 검증과 `MapReady(success=true)` ACK를 완료한다.
- 검증 완료 전까지 기존 `MapDefinition`을 현재 값으로 유지한다.
- 양측 검증 성공 시 새 정의를 원자적으로 현재 값으로 교체한 뒤 Game 씬을 재로드한다.
- 생성·전송·검증이 실패하면 교체하거나 씬을 재로드하지 않고 기존 결과 화면과 기존 맵 정의를 유지한다.

싱글 재경기도 매 판 새 맵을 생성하고 semantic 검증 성공 뒤 교체·재로드한다. 초기 골드는 [RandomMap](GameSystemRules_RandomMap.md) 규칙3의 광산 수 표를 따른다. 현재 확정 맵 정의는 Game 씬 재로드 사이에 유지하고 로비 복귀·연결 종료 때 폐기한다. 기존 맵 handoff가 재로드를 건너가기 위해 필요한 수명 규정이며 재경기 제안이 없어졌다는 이유로 함께 제거하지 않는다. 이 수명 문단 보존에 남긴 사용자 확인 요청은 원문에 보존하며 이번 정리를 별도 새 승인으로 간주하지 않는다. 초기골드 개정·싱글 실제호출·수명 문언 보존 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-04)다.

---

**규칙 17. 결과 화면에서의 상대 이탈 — 판정과 통보**

결과 화면에서 상대 이탈을 판정하고 통보하는 계약이다. 승패는 이미 확정됐으므로 여기서의 이탈은 승패를 바꾸지 않는다.

| 갈래 | 처리 |
|---|---|
|정상 퇴장|로비 버튼으로 나가기 전에 통보하고 받은 쪽은 즉시 반영|
|무반응 이탈|통보 없이30초간 응답이 없으면 아래 절차로 판정|

이탈을 확정하면 연결도 종료해 늦은 응답으로 화면 상태가 뒤집히지 않게 한다. 전송 계층 연결 timeout은 이 자체 이탈 판정보다 길어야 하며 설정값은 [네트워크 TDD](../TechnicalDesignDocument_Network.md)가 담당한다. UI는 [MatchFlowUI](GameSystemRules_MatchFlowUI.md), 승패는 [Network](GameSystemRules_Network.md) 규칙1을 따른다.

#### ① 판정 주체 — Host 와 Client 가 **각자 자기 쪽에서** 판정한다 (서버 전용 금지)

양쪽이 같은 자체 감시를 돌린다. 서버만 판정해 RPC로 알리면 Host 사망 때 남은 Client에는 감시·통보 주체가 함께 사라져 [네트워크 최상위 원칙](GameSystemRules_Network.md)을 위반한다. 정상 퇴장 즉시 통보는 자체 감시가 대신하지 않으며 두 경로는 보완 관계다.

#### ② 판정 **전에** 「내 인터넷이 되는가」를 먼저 확인한다

30초간 상대 신호가 없으면 상대와 무관한 인터넷 도달을 확인한다. 도달 가능하면 상대 이탈 확정→연결 종료→화면 반영, 불가능하면 내 회선 문제로 보고 연결을 끊지 않고 복구를 기다린다. 확인 대상·판정의 남은 한계는 [Network](GameSystemRules_Network.md) 규칙1이 원본이다. 오판은 단순 문구 오류가 아니라 복구 가능한 연결을 스스로 끝내는 결과이므로 도달 확인을 생략하지 않는다. 인게임에서는 승패 기록에, 결과 화면에서는 연결·UI 정리에 같은 확인 절차를 재사용한다.

<a id="③-응답을-무엇으로-재는가--순서만-확정됐고-수단은-미확정이다"></a>

#### ③ 「응답」을 무엇으로 재는가 — **결과 화면 전용 하트비트 채택**

현재 수단은 결과 화면 전용 하트비트다. 이전 RTT 우선 후보를 현행 채택으로 읽지 않는다. 하트비트/도달 확인의 기술 구조는 [네트워크 TDD](../TechnicalDesignDocument_Network.md), 관측 등급은 [PROJECT_STATUS](../PROJECT_STATUS.md)를 따른다. 이30초는 규칙16의 맵 전송 응답10초와 다른 시계다. 초기 설계·RTT 후보·착수조건·후속 구현/관측 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-05)다.

---

**규칙 18. 맵 전송 중 상대 이탈 — 기존 실패 경로를 그대로 쓴다**

**[쉬운 말로]** 재경기 맵을 주고받는 도중에 상대가 나가면, **그것을 위한 새 처리를 만들지 않는다.** 규칙 16 이 이미 「연결이 끊기면 즉시 실패」라고 정해 두었으므로 **그 길을 그대로 탄다.** 달라지는 것은 **화면에 뜨는 문구 하나뿐**이다. (2026-09-16 사용자 확정)

- 🔴 **새 이벤트도, 새 로그 키도 만들지 않는다.** 맵 전송 중 이탈은 규칙 16 의 *"연결 끊김은 **즉시 실패**하며 같은 데이터를 재전송하지 않는다"* 에 이미 해당한다. **같은 사건에 키를 따로 만들면 실패율 집계가 두 벌로 쪼개진다.**
- **실패 후의 상태 복원도 규칙 16 그대로다** — 재경기라면 「재경기 맵 준비 실패」 조항(이전 `MapDefinition` 유지 · rematch pending 초기화 · 결과 UI 와 전체 길이의 자동 로비 복귀 countdown · 결과 화면의 기존 선택지 복원), 최초 경기라면 같은 절의 최초 실패 조항을 따른다. **이 규칙이 다시 정하지 않는다.**
- 🔴 **가르는 것은 화면 문구 하나뿐이다.** 실패 사유가 **연결 끊김(`Disconnected`)** 인 경우에만 「상대가 나갔다」는 뜻의 문구를 쓰고, 그 밖의 실패 사유(미지원 버전 · 용량 초과 · 해시 불일치 · 역직렬화 실패 · 공정성 검증 실패 · timeout)는 **규칙 16 의 기존 실패 문구를 그대로 쓴다.**
  - **왜 문구만 가르는가**: 플레이어에게 「맵 준비가 실패했다」와 「상대가 나갔다」는 **할 수 있는 일이 다르다.** 앞쪽은 다시 시도할 여지가 있고 뒤쪽은 없다. 그런데 **되돌리는 절차 자체는 두 경우가 완전히 같으므로**, 절차를 복제하지 않고 **표시만 가른다.**
  - **실제 문구는 이 규칙이 정하지 않는다** — 단일 소스는 `GameSystemRules_UI.md` 「공통 UI 규칙」이다.
- ⚠️ **이 규칙은 이미 있는 처리를 확인해 규칙으로 못 박은 것이지 새 동작이 아니다.** 전송 경로가 연결 끊김을 감지해 그 회차를 실패로 닫는 배선은 이미 존재한다(`ROADMAP.md` 「경기 종료 후 UI·프로세스」 행의 ✅ 항목 ⓐ). **새로 정한 것은 「사유에 따라 문구를 가른다」 한 줄뿐**이다.

연결 끊김인지 여부만 결과 화면에 전달하고 기존 실패 채널을 재사용한다. 모르는 사유를 상대 이탈로 단정하지 않는다. UI 문구·개행은 [MatchFlowUI](GameSystemRules_MatchFlowUI.md) D-7, Application 사유 분류·RPC 표현·레이어 경계는 [네트워크 TDD](../TechnicalDesignDocument_Network.md)가 담당한다. 과거 채널 실측·0건 표기·도구 재현 경위는 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-matchflow-06), 현재 개별 관측은 [PROJECT_STATUS](../PROJECT_STATUS.md)를 따른다. 과거 미검증을 현재 상태로 유지하거나 강제 실패를 자연 단절로 확대하지 않는다.

---

**규칙 19. 씬 재로드 중 이탈 — 이번 설계의 범위 밖 (근거와 함께 기록한다)**

**[쉬운 말로]** 재경기가 성립해 **전투 씬을 다시 불러오는 그 짧은 순간**에 상대가 나가는 경우는 **이번에 다루지 않기로 정했다.** 없는 문제라서가 아니라 **다뤄 봐야 할 수 있는 일이 없기 때문**이며, 나중에 「빠뜨린 것 아닌가」로 다시 올라오지 않도록 근거를 함께 적어 둔다. (2026-09-16 사용자 확정)

**범위 밖으로 정한 근거 2가지:**

1. **구간이 짧다.** 이 구간은 **맵 전송과 양측 검증이 모두 끝난 뒤**에 시작한다. 즉 규칙 16 의 전송·해시 대조·씬 전환 게이트를 전부 통과한 다음의 **0.2초 남짓**이며, 이탈이 그 창에 정확히 들어올 확률이 매우 낮다.
2. 🔴 **되돌릴 결과 화면이 이미 없다.** 다른 이탈 처리의 결론은 전부 **「결과 화면으로 되돌린다」**인데(규칙 16·17·18), 이 구간에서는 결과 화면이 이미 내려가 있다. **되돌릴 대상이 없으므로 같은 처리를 적용할 수 없고, 이 구간만을 위한 별도 복원 화면을 만드는 것은 이번 설계의 범위가 아니다.**

- 🔴 **이것은 미해결 결함이 아니라 사용자가 범위 밖으로 확정한 항목이다.** 「검증 누락」이나 「빠뜨린 경로」로 읽지 않는다.
- ⚠️ **다만 「아무 일도 일어나지 않는다」고 적지 않는다** — 이 구간에서 이탈했을 때 실제로 어떤 화면이 되는지는 **확인된 적이 없다.** 확인하지 않았다는 사실 자체를 여기 남긴다(`CLAUDE.md` 규칙 10).
- 이 구간이 다시 문제가 되는 조건은 **씬 재로드 구간이 길어지는 변경**이다. 그때 이 규칙의 근거 1번이 깨지므로 **범위를 다시 판단한다.**

---

## 생성 규칙 참조

이 절은 생성 규칙의 원본 위치를 안내한다. 경기 운영 규칙은 위 규칙 14·16~19이고, 아래 제목은 생성 규칙의 사본이 아니다.

**규칙 1. 공통 맵 사양**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 2. 시작 영역**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 3. 중립 광산 수와 초기 골드**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 4. 완전개방형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 5. 장애물 개방형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 6. 협곡형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 7. 외곽형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 8. 3갈래형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 9. 건설 불가 구역**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 10. 완전 차단 지형**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 11. 건물로 경로가 완전히 막혔을 때의 향후 동작**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 12. seed, 재시도, 폴백, 로그**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 13. 생성 완료 검증**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.

**규칙 15. 장식 단계**

생성·검증 원본: [GameSystemRules_RandomMap.md](GameSystemRules_RandomMap.md#무작위-맵-생성-규칙). 본문을 이 문서에 복제하지 않는다.
