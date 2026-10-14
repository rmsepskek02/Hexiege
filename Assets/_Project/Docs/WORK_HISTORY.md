# Hexiege - 작업 이력

이 문서는 주요 변경을 날짜별로 찾아가는 이력 인덱스다. 현재 상태는 [PROJECT_STATUS](PROJECT_STATUS.md), 남은 작업은 [ROADMAP](ROADMAP.md)을 기준으로 한다.

날짜별 인계·fixture·native·빌드 시도 원문은 [구현·검증 상세 이력](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다. native PASS와 빌드 클릭은 실기 시각 수용이나 빌드 완료가 아니다.

### 2026-10-11 23_16 wave 저장 경기 — 표현 clock OPEN

상세 원문은 [이관 기록](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다.

### 2026-10-11 22:22 wave native PASS와 빌드 시도 — 이전 대기 supersede

상세 원문은 [이관 기록](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다.

### 2026-10-11 21_50 wave contact 표현 예외 격리 — 구현·정적 인계

상세 원문은 [이관 기록](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다.

### 2026-10-11 20_28 MushroomBomber·BloomFairy 최종 로그 근거

상세 원문은 [이관 기록](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다.

### 2026-10-11 00_46 최종 근거 (Main/programmer 인계)

상세 원문은 [이관 기록](History/ImplementationVerificationHistory.md#work-history-handoff)에 보존한다.

## 마일스톤 이력

| 날짜 | 마일스톤 |
|------|---------|
| 2026-10-13 | 멀티 최초 경기 맵 준비 실패 안내(규칙 M-2) [상세 원문](History/ImplementationVerificationHistory.md#milestone-3e8fb934896bb20e) |
| 2026-10-04 | BloomFairy §18 최종 자동 gate/빌드 시도 인계 동기화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0f2c538f07439ea8) |
| 2026-10-04 | BloomFairy §18 최종 코드/정적 인계 문서화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-f00e1ac0a080e387) |
| 2026-10-04 | BloomFairy §18 UnitView 코드 반영 인계 문서화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-5591c357ddf2e01d) |
| 2026-10-04 | BloomFairy 새 두 run·Movie_002 분석 문서 보강 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-639c407b76e22775) |
| 2026-10-03 | 01_03 사용자 경기 분석 문서 반영(추가코드교정 없음). EditorHost/AndroidClient sameSession81ef6ada…2c25a·epoch4c5fc1a2f46db585;421예약=415발사+6gameend취소,415wave/VFX exact일치·… [상세 원문](History/ImplementationVerificationHistory.md#milestone-c3b21a8e9913ffef) |
| 2026-10-03 | 공통 복구·Torrent 승인 발사 교정 구현 및 통합 인계(실기 OPEN). 중앙 서버/싱글 예약 cap1024·service epoch/단조 key/객체 identity·current/last-valid original aim·현재 caster origin·생성 전… [상세 원문](History/ImplementationVerificationHistory.md#milestone-fd96086cfe110eec) |
| 2026-10-02 | 17_05 공통 전투 후 복귀 실기 분석 반영(교정 아님). Torrent24 약366.5초 Blocked 후 재개·Sagan 정지 위치499표본; 유효 sideways hop4→4를 strict-closer center 필터가 제외하고 반복경로 거부 뒤 Blocked… [상세 원문](History/ImplementationVerificationHistory.md#milestone-859bcc089539eea4) |
| 2026-10-02 | 역할교대 이동 최종 분석 반영(코드 교정 아님). 서버 공간 상태 불일치→Fatal→종료 거부→후속 이동 취소 확정. Editor Host Pistoleer7×2=14, Android Host LittleKnight2/Pistoleer13×2=30. 건물 랠리 non-… [상세 원문](History/ImplementationVerificationHistory.md#milestone-852573904747773e) |
| 2026-10-02 | TorrentSpirit 역할교대 두 경기 파도 분석 반영(이동 원인 분석 대기). Editor Host/Android Client wave147·publish291(피해259/회복32), Android Host/Editor Client wave176·publish29… [상세 원문](History/ImplementationVerificationHistory.md#milestone-87cf73e0893efe5c) |
| 2026-10-02 | TorrentSpirit 최신 실기 분석 반영 [상세 원문](History/ImplementationVerificationHistory.md#milestone-3e4c793b242010db) |
| 2026-10-02 | TorrentSpirit byte/clock 로그 최종 구현·정적 재컴파일 확정 인계(게임 PASS 아님). invariant G17·full UTF-8 >900 keyed format-incomplete warning/faults/coverage=false 적용. 마… [상세 원문](History/ImplementationVerificationHistory.md#milestone-d19a21028efada8a) |
| 2026-10-02 | TorrentSpirit 전 경기 사건 Dev 로그 구현 인계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-27d7abd7e87de52a) |
| 2026-10-01 (2차) | **B-7 테스트 관련 작업이 「규칙 문서 ↔ 구현」 전수 점검으로 번졌고, 그 결과 6분류 중 분류 5(코드 주석이 거짓)의 일부가 처리됐다** (**커밋 해시는 적지 않는다** — 규칙 5 로 git 을 실행할 수 없어 확인할 수단이 없다). **[쉬운 말로]** 「멀티플레이에서 지도 만들기가 실패했을 때 화면에 무엇을 띄울 것인가」를 조사하다가 **규정은 있는데 그 기능이 프로그램에 아예 없다**는 것이 드러났습니다. 왜 3주 동안 아무도 몰랐는지를 파고들자 **적혀 있지 않았던 것이 아니라 읽히지 않았던 것**임이 밝혀져, 결국 **규칙 105개를 하나씩 코드와 맞춰 보는 전수 점검**으로 번졌습니다. 그 결과를 사용자가 정한 **여섯 갈래**로 묶었고, 이번 회차에는 그중 **프로그램 안의 설명글이 사실과 반대인 자리**를 실제로 고쳤습니다. **① 어디서 시작했나** — B-7 = **멀티플레이 맵 투영 실패 UI** 건이며 task 폴더는 `_Tasks/2026-09-30/15_01_multi-map-projection-failure/` 다. 그 조사(`Research.md` §7-1)에서 🔴 **규칙 M-2(멀티 최초 경기 맵 준비 실패 UI)가 규정만 있고 구현이 0줄**이라는 것이 드러났다. **② 왜 번졌나 — 원인 추적의 결론이 처음 짐작과 달랐다.** 사용자 지시로 원인을 파고든 결과 두 가지가 나왔다. ⓐ **점검 수단의 범위**: `Tools/check_docs.py` 의 검사 7종이 전부 **「문서 ↔ 문서」**이고 **`.cs` 를 읽는 자리가 0건**이며, `WORKFLOW.md` [11] ② 가 **「작업 내용 기준」**이라 **이번 회차가 건드린 범위만** 보게 되어 있었다. 🔴 **그러나 이것을 「검사기가 문제였다」로 읽지 않는다** — 검사기는 자기 7종을 정확히 수행했고 **그것이 막고 있는 사고는 실재한다**(`.claude/mistakes.md` 2026-08-17 두 건이 검사 [6]·[7] 의 출처다). **틀린 것은 그 0건을 범위보다 넓은 보증으로 전달한 보고 방식**이다. ⓑ 🔴 **M-2 가 3주를 버틴 진짜 이유는 「추적 누락」이 아니었다** — `ROADMAP.md` **204행** 안에 그 항목이 **「팝업 생김새·문구·버튼만 남았다」까지 정확히 적혀 있었고**, 한 칸 안에 잔여 항목 다섯 개가 괄호·취소선·갱신 블록과 뒤섞여 **읽히지 않았다.** (단일 소스는 전수 점검 `Research.md` **§1**) **③ 무엇을 했나** — 사용자 지시로 **규칙 105개 전수 대조**(`GameSystemRules_UI.md` 86 + `GameSystemRules_RandomMap.md` 19)를 수행해 조사 문서 1개가 나왔고, 결과를 **사용자가 정한 6분류**로 정리했다(그 문서 **§12**). **④ 이번 회차에 실제로 처리된 것** — **분류 5 의 코드 주석 6자리(8파일)**를 고쳤다. 🔴 **동작 코드는 0줄 바뀌지 않았다**(주석·문자열 리터럴을 걷어낸 코드 본문을 변경 전 상태와 대조해 8/8 동일 — **메인 세션이 전달한 값**이며 규칙 5 로 내가 재검증하지 못했다). 그 과정에서 **같은 거짓이 더 있는 자리 3곳과 새 거짓 1곳**이 드러나 분류 5 가 **6 → 10건**이 됐고, 6자리 중 **3항목은 「부분 완료」**다. 🔴 **과대 표기 금지 — 끝난 것은 분류 5 의 일부뿐이다.** 분류 1 · 2 · 3 · 4 · 6 은 **한 자리도 손대지 않았고**, **실기·화면 확인은 0건**이며 **Unity 에디터 컴파일도 확인되지 않았다.** 🔴 **「문서가 거짓」으로 추가하라고 받은 두 건은 직접 실측에서 서지 않아 분류 4 에 넣지 않았다** — 하나는 방향이 반대(문서가 맞고 코드 주석이 낡았다)여서 분류 5 로 옮겼고, 하나는 **이미 그 표에 있던 항목**이었다. 경위는 전수 점검 `Research.md` **§12-5-1**. **⑤ 남은 작업의 추적** — 분류별로 `ROADMAP.md` 우선순위 요약 표에 **행 7개**를 세웠다. 🔴 **한 칸에 몰아넣지 않고 분류마다 갈라 짧게 적었다** — M-2 가 묻힌 이유가 **「읽기 어려운 한 칸」**이었으므로 같은 함정을 되풀이하지 않기 위한 것이다. **단일 소스** — 조사 결과·분류·개수는 `_Tasks/2026-10-01/05_19_rules-implementation-audit/Research.md`(§12), 현재 상태의 경계는 `PROJECT_STATUS.md`, 남은 작업은 `ROADMAP.md` 다. **이 표에 그 내용을 옮겨 적지 않는다.** |
| 2026-10-01 | 팝업 로그 커버리지 + 「잡은 적 없는 몫을 놓는」 결함 수정이 실기로 확인됐다 [상세 원문](History/ImplementationVerificationHistory.md#milestone-fe2e0cde4e4eb337) |
| 2026-10-01 | **TorrentSpirit 두 경기 실기 근거 문서 반영(작업 완료 선언 아님):** 관측/display 17→32·HP 변화 45→121·Client heal 4→36. 32 cap 이후 미관측·noHpDelta 11 원인 불명. 두 local ROOT PASS·ERROR 0 및 최신 공통 C3 83/83/83·failures 0은 wave 전용 결과/누락0/migration 완료 아님. 정확 시각 수용 OPEN·전체 OPEN/C3 Unresolved, busy·메뉴/빌드 과거 이력 보존. 원본 VFX·70/30초 발사·4초 주기 유지, 추가 계측은 구현 미승인 제안. 기존 TorrentSpirit Research/Plan 최신 절 참조. |
| 2026-09-30 | 「경기 종료 후 UI」 마무리 회차 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9311030539c1b56) |
| 2026-09-30 | SpearMan 0:25 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-a1ad88aa498f30c1) |
| 2026-09-30 | SpearMan 0:25 생산 타임라인 저장·Unity 자동 검사 두 메뉴 PASS / focused 실기 OPEN. Attack 단일 marker와 type 4 설정을 25/30초(0.8333333초)로 맞추고 cooldown 2초, Game type 4 명시 클립 … [상세 원문](History/ImplementationVerificationHistory.md#milestone-72b5064d83285727) |
| 2026-09-30 | LionKnight 두 타격 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-3319e3d37fcfa1f7) |
| 2026-09-30 | LionKnight 두 타격 설정 저장 및 첫 실기 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1a6842ceac95b683) |
| 2026-09-30 | BearGuard 0:13 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-ba34c68c61ab7fed) |
| 2026-09-29 (2차) | **싱글 맵 실패 UI 후속 7건 — 투영 단계까지 범위 확대(H) · 씬 자리표시 문자열 2자리(I·L) · 주석 문구 사본 제거(J) · 라벨 단일 소스(K-1) · 상태 줄 완전 일원화(K-2) · 싱글 투영 전용 강제 실패 수단(M) 구현 · 실기 검증 완료** (🔴 **같은 날 두 번째 회차다** — 바로 아래 2026-09-29 행이 단계 10 본체이고 이 행은 그것이 남긴 후속 항목이다. **커밋 해시는 적지 않는다** — 규칙 5 로 git 을 실행할 수 없어 확인할 수단이 없다). **[쉬운 말로]** 혼자 하는 게임에서 **지도를 만들었지만 화면에 그리지 못한 경우**에도 이제 안내가 뜬다. 종전에는 「만드는」 단계의 실패만 안내가 있었고 「그리는」 단계는 **조용히 빈 화면**이 됐다. 그 화면을 실제로 볼 수 있는 **전용 스위치**도 함께 만들어져 실기에서 두 번 떴다 — 🔴 **종전 스위치로는 구조적으로 닿을 수 없던 자리다**(만드는 단계가 실패하면 그리는 단계는 아예 불리지 않는다). 나머지는 뒤처리다 — 씬에 남아 있던 개발용 글자 두 자리를 비우고, 같은 글자가 코드 여러 곳에 흩어져 있던 것을 한 자리로 모으고, 화면 한 줄에 글씨를 쓰는 창구를 완전히 하나로 만들었다. **✅ 코드·씬 적용 (7건)** — **H**: 투영 함수가 성공/실패를 돌려주게 되고 **싱글 호출부만** 그 값을 읽어 **기존 통보 함수**를 부른다(🔴 **멀티 호출부 본문 변경 0줄 · 새 채널·새 계약·새 로그 이벤트 0개**) · **I·L**: `Game.unity` 의 상태 줄과 결과 문구 자리 텍스트를 빈 문자열로(각각 1줄 · 배선 무변경 · 파일 총 행수 53,260 유지 · 🔴 **되돌리기 값은 둘 다 `124124`**) · **J**: 리포지토리 전체 히트 **4 → 3**(남은 3건은 전부 값을 담은 자리) · **K-1**: 「다시하기」·「로비로」의 값 자리가 **각 2곳 → 1곳**(🔴 **실패 문구 3곳은 합치지 않았다 — 마침표 차이가 사양이고 세 번째는 멀티 범위 밖이다**) · **K-2**: setter 호출부 **5곳 · 위젯 직접 대입 0곳** · **M**: 메뉴 항목 **8개**, 「현재 설정 확인」·「해제」가 **저장소 넷** 공용. **✅ 실기 PASS (로그 직접 관측)** — 🔴 **투영 단계에 실제로 도달해 실패로 끝났다 2회**(준비 성공 직후 같은 판에서: 23:29:25.704 → .706 → .707 / 23:30:08.071 → .072 → .072, 최초 경기와 다시하기 두 경로) · **K-2 가 건드린 두 자리가 정상 발화**(재경기 준비 진입 23:28:43.573 → .574 / 이탈 재시작 23:29:00.182 → .183) · **멀티 회귀 증상 0건**(평시 60초 · 이탈 30초 · 수락 시 정지) · **싱글 타이머 없음 유지**(카운트다운 시작 0건 · 만료 복귀 0건 · 로비 복귀는 전부 버튼) · 오류 집계 = 실기기 `[ERROR]` **0건**·`[WARN]` 2건, 에디터 `[ERROR]` **2건(강제 실패가 의도적으로 남긴 것뿐)**·`[WARN]` 3건. **✅ 직전 회차의 「Unity 에디터 컴파일 미확인」이 해소됐다** — 새 도구 파일의 `.cs.meta` 가 실재하고(임포트됨) 그 코드가 실기에서 2회 발화했다(컴파일 성공). ⚠️ **과대 표기 금지 — 아직 완료가 아닌 것**: 🔴 **진짜 투영 실패는 발화 0건**(강제 실패로만 봤으므로 발생 빈도·심각도 미확정) · **투영 단계 전체의 실패 지점 전수 미계수** **[✅ 2026-09-30 갱신 포인터 — 🔴 왼쪽 표기는 2026-09-29 (2차) 시점의 기록이라 지우지 않는다(`.claude/MEMORY.md` B-7). 「전수 미계수」는 더 이상 참이 아니다 — 전수 계수가 끝났다. 🔴 단일 소스는 `_Tasks/2026-09-29/16_16_post-game-ui-close-out/Research.md` **§4** 이며 수치를 여기에 옮겨 적지 않는다. ⚠️ 바로 앞 조각(「진짜 투영 실패는 발화 0건」)은 함께 닫히지 않는다 — 「세었다」와 「실제로 난 적이 없다」는 다른 사실이다]** · **컴파일 경고 0건 미확인** · **싱글 투영 실패는 에디터에서만 확인**(실기기는 멀티 구간만) · **로그에 자리가 없어 사용자 화면 확인에만 근거하는 4가지**(모달이 뜬 모습 · 결과 화면 되살아남과 상태 줄 한 줄 · 승리/패배 글씨 · 씬 두 자리가 빈 칸) · 종전 그대로인 **매 초 setter 호출의 성능·깜빡임 미측정 · 모달 z-order 미확인**. ※ **근거 구분(규칙 10)** — **내가 직접 실측한 것**: 로그 시각·건수(에디터 23시대 구간 · 실기기) · grep 개수(문구 4→3 · 라벨 각 1 · setter 5 · 직접 대입 0) · 씬 실측(`m_text` 2자리 빈 값 · 총 53,260행) · `.cs.meta` 실재와 guid. **전달값(재측정 불가)**: 사용자 승인·발언 인용 · 사용자 화면 확인 보고 · 코드 diff 규모. **내가 실행할 수 없는 것**: git · Unity 컴파일 · 런타임 실행. **단일 소스** — 사양은 `GameSystemRules/GameSystemRules_UI.md` 규칙 M-4 끝의 **2026-09-29 (2차)·(3차) 블록**, 도구 사용 절차는 `TechnicalDesignDocument.md` 「개발용 강제 실패 플래그 (에디터 전용)」 절의 **⑤·⑥ 소절**, 구현·실기 기록은 `_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md` **§12 · §15 · §8-1**, 검증 경계는 `PROJECT_STATUS.md` 의 「✅ 구현 완료 · 실기 검증 (2026-09-29 (2차))」 문단이다. |
| 2026-09-29 | 경기 종료 후 UI·프로세스 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1fefe68e7110ff3d) |
| 2026-09-29 | BearGuard 0:13 Unity 두 self-validation PASS·Android 빌드 시작 화면 확인 / 실기 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-0ef0b24052f3d73b) |
| 2026-09-29 | BearGuard 0:13 생산 타격 설정·명시 연결 구현 / Unity 검증·실기 대기 [상세 원문](History/ImplementationVerificationHistory.md#milestone-c3ae42bb416a07ac) |
| 2026-09-29 | TideSpirit 1:15 공격 시각 focused PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-7ee7e8dc4cfc7fa6) |
| 2026-09-29 | TideSpirit 1:15 생산 설정·명시 연결 구현 / Unity 두 메뉴 PASS · 실기 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-b12b1a3fea3762bd) |
| 2026-09-29 | BoulderSpirit 1:10 공격 시각 focused PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-1e58dac98240305b) |
| 2026-09-29 | BoulderSpirit 1:10 Unity 재검증 PASS·Android 빌드 시작 관찰 [상세 원문](History/ImplementationVerificationHistory.md#milestone-5b4dcda15281c399) |
| 2026-09-29 | BoulderSpirit 1:10 저장값 동기화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0356a038573d3757) |
| 2026-09-29 | BoulderSpirit 1.50초 교정 후 focused 화면 수용 / 별도 LittleKnight MOVE-AUTH FAIL [상세 원문](History/ImplementationVerificationHistory.md#milestone-e957bced01f0bc9b) |
| 2026-09-28 | EmberSpirit 명시 Attack 연결 focused PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-f444c11a2f621684) |
| 2026-09-28 | RabbitTrickster 명시 Attack 연결·0:20 타이밍 focused PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-d46d26d7bcb7cca7) |
| 2026-09-28 | Fox 피해 표시 지연 focused 사용자 수용 / Dust focused 로그 CONDITIONAL PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-58876dfa5687a20e) |
| 2026-09-27 | 경기 종료 후 UI·프로세스 [상세 원문](History/ImplementationVerificationHistory.md#milestone-8ad175733f176b55) |
| 2026-09-24 | 경기 종료 후 UI·프로세스 [상세 원문](History/ImplementationVerificationHistory.md#milestone-681f7d536f25f19c) |
| 2026-09-22 | 경기 종료 후 UI·프로세스 [상세 원문](History/ImplementationVerificationHistory.md#milestone-66f5e991995721f2) |
| 2026-09-22 | InfernoSpirit 이동·방향·VFX focused 교정 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-9d59b64f1b5dfc2e) |
| 2026-09-21 | 경기 종료 후 UI·프로세스 [상세 원문](History/ImplementationVerificationHistory.md#milestone-9d34baf316ec5584) |
| 2026-09-20 | BattleAxe production 타격 시점 교정·focused Android 로그 CONDITIONAL PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-c0aaa53474361a6b) |
| 2026-09-15 | 맵 테스트 모드 삭제(T1~T3) + 재경기 랜덤 맵(A~D) [상세 원문](History/ImplementationVerificationHistory.md#milestone-97491641f9c00a6b) |
| 2026-09-14 | 재경기 맵 규칙 개정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-b10aa4a554d6b3ae) |
| 2026-09-14 | 초기 골드 5000 의 정체 확정 · 맵 테스트 모드 삭제 결정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0a201ccc4bcb7c80) |
| 2026-09-14 | 무작위 맵 3단계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-762ed57de971537b) |
| 2026-09-14 | QuakeSpirit focused Android Host/Client 실기 로그 CONDITIONAL PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-84f6170b1c99d06b) |
| 2026-09-14 | QuakeSpirit production marker/config 정렬·Supported 승격 자동 게이트 CONDITIONAL PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-b1d608aa5910d8bd) |
| 2026-09-14 | RhinoBreaker 실제 Attack marker 교정 추가 실기·로그 CONDITIONAL PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-6b7eb98b9bc38d4b) |
| 2026-09-13 | 공격 진입 production typed target payload seam 교정·동일 범위 실기 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-3e94577c11f97512) |
| 2026-09-09 | 무작위 맵 2단계 결과의 멀티플레이 실기 검증 (처음) · 부분 검증 / 테스트용 임시 seed 1 → 11 [🔴 2026-09-14 갱신 [상세 원문](History/ImplementationVerificationHistory.md#milestone-64f551c38a0be888) |
| 2026-09-08 | 무작위 맵 2단계 K [상세 원문](History/ImplementationVerificationHistory.md#milestone-ff628e859bcbfc3b) |
| 2026-09-08 | 무작위 맵 2단계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-8b83e99a9cc2c632) |
| 2026-09-08 | 토스트 중복 표시 수정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-8cabba420f9d09d9) |
| 2026-09-02 | C3 v6 exact-scope 표현 수명·복구 의미 교정 및 Unity 자동 gate PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-2e198b89107d24b0) |
| 2026-09-01 | C3 v5 동일 계약 멀티 실기 FAIL 및 C3-only 원인 분리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-b13d6ef1e21286b1) |
| 2026-08-30 | C2 공격 연속성·사망 future-hit 취소 8종 집중 멀티 회귀 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-cd60fb8b16f2e73a) |
| 2026-08-28 | Tracer C2 통합 실기 FAIL 및 원인 분리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-909470064f9f2cc5) |
| 2026-08-27 | Tracer C2 Snapshot·ImpactResult Shadow 구현·Editor 게이트 PASS / 멀티 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-347ef1b9c54b3023) |
| 2026-08-27 | Tracer C1 연속 Attack·독립 회차 결속 집중 멀티 PASS + observer v5 오탐 교정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-ef315767e7d449a7) |
| 2026-08-26 | Tracer C1 v3 독립 공격 회차 상관 교정·자동 검증 PASS / 멀티 실기 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-c37ecba6546db594) |
| 2026-08-26 | Tracer C1 1차 멀티 실기 FAIL 및 2차 교정 착수 [상세 원문](History/ImplementationVerificationHistory.md#milestone-130f947708900f65) |
| 2026-08-26 | Tracer C1 Legacy 공격 타겟·방향 교정 구현·자동 검증 PASS / 멀티 실기 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-6b1f929fbfa8fe2c) |
| 2026-08-25 | RootCrossAudit Android-safe compact terminal 최종 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-f8d4456868ef5b1e) |
| 2026-08-24 | B3 v10 중심 경로·건물 안전 Chase·전방 중심 복귀 집중 멀티 실기 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-63cede23cc58ab58) |
| 2026-08-24 | B3 v9 회전 복제 계측 PASS · 역할교대 실기 MOVE terminal FAIL / 전체 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-be72737cce4a4f82) |
| 2026-08-23 | RootCrossAudit false-INCONCLUSIVE 진단기 교정 · 공식 재감사 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-3ef2918686f166e2) |
| 2026-08-23 | B3 v9 최종 통합 정적 코드 게이트 PASS · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-ba455951f237e77f) |
| 2026-08-23 | B3 v9 pre-merge self-validation PASS · main 통합 진행 중 · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-b82dde7fd9deeeac) |
| 2026-08-23 | B3 v9 typed 공간 후보/preflight pre-merge 코드 게이트 · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-01c325a72a872a08) |
| 2026-08-20 | B3 v8 경로 checkpoint/공간 타일 분리 코드 게이트 PASS · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-0e3ed9947d184731) |
| 2026-08-18 | B3 재경로·제자리 걷기 목표 단위 교정 구현 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7fbaaa52a7a8462e) |
| 2026-08-18 | B3 유한 재탐색 실기 회귀 진단 및 규칙 재개방 [상세 원문](History/ImplementationVerificationHistory.md#milestone-904a390fe1c87582) |
| 2026-08-18 | B3 최신 main 통합 및 Android Host 유한 재탐색 교정 구현 [상세 원문](History/ImplementationVerificationHistory.md#milestone-198730a7d614cf1c) |
| 2026-08-18 | B3 Android Host 경기 정지 진단 및 최신 main 통합 기준 확정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-24339bd2143512b1) |
| 2026-08-19 | 게임 종료 · 서버 종료 시점의 전투 뒷정리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a249c79b11d00912) |
| 2026-09-03 | 무작위 맵 1단계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a5bc99c0905a3668) |
| 2026-08-24 | 네트워크 종료 시점 가드 8곳의 실기 검증 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a34b43345be84a72) |
| 2026-08-24 | 죽은 윈도우 경로 마지막 잔존 제거 + QA 기록 완전 중복 해소 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1a84ba47615600e4) |
| 2026-08-24 | 에이전트 메모리 3개를 「인덱스 + 토픽」 구조로 분산 [상세 원문](History/ImplementationVerificationHistory.md#milestone-4917947b7dd6feb5) |
| 2026-08-21 | 전제 정정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-641c110f6b0d0435) |
| 2026-08-20 | 네트워크 종료 시점 가드 전수 보강 8곳 [상세 원문](History/ImplementationVerificationHistory.md#milestone-bfeed869492c6763) |
| 2026-08-20 | 에이전트 메모리 손실 사고 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7fdecd60d93d193b) |
| 2026-08-19 | 게임 종료 · 서버 종료 시점의 전투 뒷정리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-f7d5685dae1fd192) |
| 2026-08-19 | 전역 로그 훅의 Error 수집 확대 + 전투 컨트롤러 RPC 가드 + 연구 흐름 로그 보강 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0916935c30ea17e4) |
| 2026-08-18 | 로그 체계(GameLog) 전환 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7c091b94b90ae75d) |
| 2026-08-17 | 로그 체계(GameLog) 전환 [상세 원문](History/ImplementationVerificationHistory.md#milestone-ee9e038a7c34f4ec) |
| 2026-08-12 | MistShrine(HealShrine) 물안개 힐 시스템 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-87e292342feeb2d4) |
| 2026-08-10 | MistShrine(HealShrine) 물안개 힐 재설계 기획 확정 + 방어 타워 표기 오류 정정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-136c4776060cbfb0) |
| 2026-08-08 | 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 완료 (실기 테스트 PASS · 커밋 9a19cd5) [상세 원문](History/ImplementationVerificationHistory.md#milestone-5c6b8f38dd62449c) |
| 2026-08-08 | 건물 파괴 시 열린 패널/조준 UI 원복 구현 완료 (실기 테스트 PASS · 커밋 8c7fa01) [상세 원문](History/ImplementationVerificationHistory.md#milestone-efef73fd490d807e) |
| 2026-08-05 | 스킬 시스템 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 구현 완료 (실기+멀티(클라) 테스트 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-56187617ca745427) |
| 2026-08-04 | 스킬 건물 시스템 Phase 1 구현 완료 (실기기 테스트 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-b898e915aa61598b) |
| 2026-07-31 | 연구소 유닛 강화 시스템 + 전투 스탯 ×10 스케일 개편 + 연구 패널 UI 구현 완료 (기능·멀티플레이 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-62d3c683a2e80148) |
| 2026-07-20 | QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료 (특수 유닛 5종 마지막 = 5종 전량 + InfernoSpirit DoT까지 전부 완료, QA PASS·멀티플레이 실기 로그 검증 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-71da2aa006a17b47) |
| 2026-07-20 | InfernoSpirit(지옥불 정령) 단일 대상 지속 피해(DoT) 구현 완료 (특수 유닛 5종 중 5번째 = 전 5종 완료, QA PASS·사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-fd11c55cc62b431d) |
| 2026-08-04 | Unit ActionSequence Tracer B3 연속 이동 구현 및 Editor 조건부 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-e5072c1cff7e9aaa) |
| 2026-08-03 | Unit ActionSequence Tracer B2 서버 이동·SimulationFacing Shadow 25/25종 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-eb63f651320b4c2e) |
| 2026-07-29 | Unit ActionSequence Tracer B1 양방향 이동 pose 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-fd111e9a8b28caba) |
| 2026-07-29 | Unit ActionSequence B1 잘못된 Collider 진단 가정 제거 [상세 원문](History/ImplementationVerificationHistory.md#milestone-6c2e8d3c60929225) |
| 2026-07-29 | Unit ActionSequence Tracer B1 rollback 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-0322b6b163c65a44) |
| 2026-07-27 | Unit ActionSequence Tracer B1 asset migration·Presentation seam 적용 [상세 원문](History/ImplementationVerificationHistory.md#milestone-473e37d3a4c6c263) |
| 2026-07-27 | Unit ActionSequence Tracer B0 Visual Root migration readiness PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-33a08529f84433f9) |
| 2026-07-27 | Unit ActionSequence Tracer A2 server-authoritative pose seam shadow 구현·멀티플레이 런타임 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-079393e95c89efd1) |
| 2026-07-22 | Unit ActionSequence Tracer A1 Pure Application 계약·stateful reducer 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7f93b9916af2b7b1) |
| 2026-07-22 | Unit ActionSequence Tracer A0 Shadow Melee Sequence 구현·사용자 멀티 계측 통과 [상세 원문](History/ImplementationVerificationHistory.md#milestone-73b3538feee0decb) |
| 2026-07-22 | 최신 main 반영 후 유닛 전투 문서 재감사 — InfernoSpirit 직접 25 + 유닛 전용 DoT 5/초×3초의 사용자 실기 결과와 QuakeSpirit 직접 20 + 주변 적 유닛·건물 10의 Host/Client 피해 로그를 반영했다. 두 구현을 Legacy 기능 확인으로 분류하고, Quake `OnAttackHit` 누락·1.00초 placeholder·최대 7.5초 표현 지연 가능성, Inferno marker 0.50/설정 1.15 불일치, 권위 AttackSequence/ImpactPoint 부재와 Quake 별도 피해 writer를 v2 차단 항목으로 기록했다. 코드·에셋은 변경하지 않았다. |
| 2026-07-20 | InfernoSpirit Legacy 특수 공격 반영 — 주 타깃 직접 25 후 적 유닛에만 DoT 5/초×3초(총 15), 건물은 직접 피해만 적용. 사용자 실기 범위에서 기능 확인. 공격 방향 공통 수정은 사용자 결정으로 별도 보류했다. |
| 2026-07-20 | QuakeSpirit Legacy 즉발 AoE 반영 — 주 타깃 20, 반경 내 다른 적 유닛·적 건물 10(50% 올림)을 서버에서 적용하고 Host/Client HP 로그를 확인. 기본 Attack marker와 규칙 v2 sequence/ImpactPoint는 미완료로 유지했다. |
| 2026-07-20 | 멀티플레이 유닛 이동·공격 동기화 규칙 v2 일괄 개정(문서/설계만, 런타임 변경 없음) [상세 원문](History/ImplementationVerificationHistory.md#milestone-5690f7ca4959e3c4) |
| 2026-07-20 | FlatTop 11×21 무작위 대전 맵 구현 계약 문서 동기화 완료(미구현) [상세 원문](History/ImplementationVerificationHistory.md#milestone-11aeb70840fcc8d4) |
| 2026-07-19 | FlatTop 11×21 무작위 대전 맵 기획·규칙 문서화 완료(구현 예정) — 다섯 맵 유형 동일 20%, 모든 생성 요소·장식 exact 180° 대칭, 시작 공간 팀별 고유 10타일, 유형별 중립 광산 수와 초기 골드, 국소 건설 불가 구역/완전 차단 지형, 결정적 seed·100회 재시도·동일 조건 폴백·로그·검증 기준 확정. 중앙 단독 광산과 180도 대응 광산 쌍의 공정성 규칙을 분리해 `GameSystemRules_Map.md` 정정, `GameSystemRules_RandomMap.md`·루트 `CONTEXT.md` 신규 작성. PointyTop 제외. task: `_Tasks/2026-07-19/15_31_random-battle-map-design/`. |
| 2026-07-19 | MushroomBomber(버섯폭격기) 착탄형 범위 DoT 딜러 구현 완료 (특수 유닛 5종 중 4번째, QA PASS·사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-aaff2b80704781de) |
| 2026-07-18 | BloomFairy(꽃요정) 힐러 + HoT/DoT 공용 시간 지속 효과 시스템 구현 완료 (특수 유닛 5종 중 3번째, 사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-fdd518f4092fa904) |
| 2026-07-17 | TorrentSpirit(물의 상급 정령) 파도형 이동 AoE + 힐 서브시스템 구현 완료 (특수 유닛 5종 중 2번째). 도끼병에서 만든 전략 핸들러 구조 위에 3가지 신규 요소를 얹음. [special-only 모델] ISpecialAttackBehavior.Rep… [상세 원문](History/ImplementationVerificationHistory.md#milestone-1286190bb160ca7a) |
| 2026-07-17 | 도끼병(BattleAxe) 휩쓸기형 AoE 구현 완료 (특수 유닛 5종 중 첫 구현, 사용자 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c922bb891dc3ae0e) |
| 2026-07-17 | 매치메이킹 404 수정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-377188e4f82e6688) |
| 2026-07-15 | Android AAB 빌드 용량 최적화 완료 (main 반영) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c52d481ae51e537d) |
| 2026-07-13 | 코드 정리 3건 완료 (실기 통과, main 반영) [상세 원문](History/ImplementationVerificationHistory.md#milestone-0c80ccbc2a885d50) |
| 2026-07-13 | 이동/Walk 애니메이션 동기화 완료 (레벨 동기화 전환, 실기/로그 검증 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-2a8070298cc90271) |
| 2026-07-12 | 전투 타격 타이밍 동기화 완료 (실기/로그 검증 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c146c8b45575643f) |
| 2026-07-09 | 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 완료 (2026-07-10 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-b976ba1378670d94) |
| 2026-07-08 | 사운드 시스템 실기 버그 3종 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2b7306a3df00fc38) |
| 2026-06-27 | Google 로그인 실기 디버깅 완료 (로그인 성공) [상세 원문](History/ImplementationVerificationHistory.md#milestone-7550a7415e58609a) |
| 2026-06-26 | 방 만들기 취소 후 텍스트 잔류 버그 수정 완료 — 커스텀 게임 방 만들기를 취소한 뒤 다시 CustomHostPanel에 진입하면 이전 세션의 LobbyCode(방 코드), ConnectedPlayers(접속 인원), ErrorMessage(오류 메시지) 값이 그대로 남아 어색하게 표시되던 문제. `BattleViewModel.CancelHosting()` 메서드에 세 값 초기화 코드 3줄 추가(`LobbyCode.Value = ""`, `ConnectedPlayers.Value = 0`, `ErrorMessage.Value = ""`)하여 취소 시점에 패널 상태를 깨끗이 비움. 수정 파일: `Presentation/UI/ViewModels/BattleViewModel.cs`. main 머지 완료. |
| 2026-06-26 | 게임포기 시 로딩 인디케이터 미해제 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-ebd5e656612c50e9) |
| 2026-06-26 | IUnitFactory 인터페이스 도입 완료 (Bootstrap 의존성 제거 리팩토링) [상세 원문](History/ImplementationVerificationHistory.md#milestone-0c52d0adea14090b) |
| 2026-06-25 | 랜덤 매칭 2회차 실패 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-6437b7e96d17018a) |
| 2026-06-25 | RuntimeLogger 유틸리티 생성 완료 — `Infrastructure/Debug/RuntimeLogger.cs` 신규. `BeginSession(folderPath, role)` / `Log(level, system, className, message, data)` / `EndSession()` API. `#if UNITY_EDITOR`에서 파일 기록, 항상 `Debug.Log` 출력(실기기 Logcat 대응). task: `_Tasks/2026-06-25/07_25_runtime-logger/`. |
| 2026-06-25 | GameBootstrapper.Setup.cs 하드코딩 배열 파생 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2447e604bf04ba0e) |
| 2026-06-25 | 코드 구조 개선 Phase 2 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2a0d3d4ced2b82a5) |
| 2026-06-23 | document-manager 에이전트 신규 생성 — 프로젝트 전체 문서(CLAUDE.md, AGENTS.md, WORKFLOW.md, 설계 문서, 메모리 파일, Task 문서 등) 통합 관리 전담. `.claude/agents/document-manager.md` + `.claude/agent-memory/document-manager/MEMORY.md` 생성. AGENTS.md 에이전트 역할 표·메모리 경로 표·업데이트 체크리스트 항목 인덱싱 완료. |
| 2026-06-23 | 코드 정리(클린업) Phase 1 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1d7b8e656b0aac34) |
| 2026-06-23 | 스플래시 화면 로그인 흐름 개선 완료 — 로그인된 상태 재실행 시 스플래시 → "Tap to Start" → 탭 → 로비 직접 이동(FadeOut 없음). `SplashOverlayView`에 `_skipFadeOnTap` 필드 + `SetTapCallback(callback, skipFade=false)` 파라미터 추가. `OnPointerClick` skipFade 분기로 즉시 콜백 실행. `LoginBootstrapper` 자동 로그인 성공 분기에서 `skipFade:true` 적용. 로그인 X 기존 흐름(FadeOut → 로그인 화면 노출) 변화 없음. |
| 2026-06-23 | 로그인 팝업 CloseButton 무반응 수정 완료 — AnonymousWarningPopup / NetworkErrorPopup 두 팝업에 `_closeButton` SerializeField 추가 + Inspector 연결. CloseButton GO가 씬에 활성화 상태로 존재했으나 코드 필드 누락으로 클릭 무반응이던 버그 해결. |
| 2026-06-23 | LoadingIndicator 전수 적용 완료 — SceneLoader 정적 유틸리티 신규, 모든 씬 전환(로그아웃/포기/로비복귀/재경기/연결끊김) ShowLoading 커버. UIManager.LoadSceneWithDelay 텍스트 지연 버그 수정(ShowLoading 코루틴 외부로 이동). AnonymousWarningPopup 초기 메시지 누락 수정("로그인 중..."). NetworkGameManager Infrastructure→Presentation 레이어 위반 수정(GameEvents.OnNetworkBackToLobby 이벤트 경유). GameSystemRules_UI.md 로딩 규칙 L-1~L-4 신설. |
| 2026-06-22 | ConfirmPopup z-order 버그 수정 완료 — InGameSettings Panel(Canvas SO=200)이 ConfirmPopup 위에 렌더링되던 문제. ConfirmPopup.prefab 루트에 Canvas(Override Sorting=true, SO=250) + GraphicRaycaster 추가(Inspector 직접 작업). Canvas SortingOrder 전체 구조 확정(SO=0/100/200/250/300) 및 GameSystemRules_CanvasSortingOrder.md 신규 작성. Fix_AddCanvasToConfirmPopup.cs 에디터 스크립트 작성(LoadPrefabContents 환경에서 SerializedObject 방식 필수). |
| 2026-06-22 | BlockingOverlay 씬 전환 버그 수정 완료 — DontDestroyOnLoad 루트 GO 제약. Canvas SO 구조(0/100/200/300) 수립. RuntimeLogWriter [DEBUG-TEMP] 코드 제거. |
| 2026-06-20 | LogRules.md 작성 완료 — 런타임 로그 + QA-Fix 로그 규칙 통합. 실기기 Logcat 공유 방식 확정. WORKFLOW.md Log.md 섹션 이동. |
| 2026-02 이전 | 싱글플레이 코어 루프 완성 (헥스, 전투, 건물, 생산, 승패) |
| 2026-02 | 멀티플레이 Phase 1~8 완성 |
| 2026-02-27~03-01 | 2D→3D 전환 완료 (XZ 좌표계, 55도 카메라, 3D 모델) |
| 2026-03-02 | 전투 거리 정밀도 버그 수정 (IEntityPositionProvider) |
| 2026-03-02 | GameConfig 정리 (AnimationFps 제거, TileHeight 수정) |
| 2026-03-07 | 공격 방향 Transform 기반 구현 완료 (UnitView._meshYOffset=30f, Atan2 기반 방향 계산) |
| 2026-03-07 | 유닛별 AttackCooldown 시스템 완료 (UnitData.AttackCooldown/AttackCooldownRemaining) |
| 2026-03-01 | 생산 큐 클라이언트 UI 즉시 갱신 수정 (OnProductionQueueChanged → SyncQueueStateClientRpc) |
| 2026-03-07 | 자동생산 멀티플레이 지원 완료 (ToggleAutoServerRpc + AutoProductionChangedClientRpc) |
| 2026-03-07 | Siege/AI 이동 서버 권위 동기화 완료 (BroadcastServerMove + BroadcastMoveClientRpc, 클라이언트 화면 불일치 수정) |
| 2026-03-13 | 팀별 피아식별 프리팹 에셋 완료 (Castle/Barracks/Pistoleer/Assault/Sniper Blue+Red) |
| 2026-03-13 | 반응형 팝업 UI 완료 (ProductionPopup/BuildingPopup 앵커 기반 배치) |
| 2026-03-14 | 팀별 초상화 동적 업데이트 완료 (ProductionPanelUI/BuildingPlacementUI — Show() 시 팀별 스프라이트 교체) |
| 2026-03-14 | 전투 범위 임계값 수정 (UnitCombatUseCase epsilon 제거 → 타일 중심 간 정확한 거리 기준) |
| 2026-03-14 | 팀별 프리팹 코드 연동 완료 (UnitFactory 팀+타입별, BuildingFactory 팀별 분기) |
| 2026-03-14 | Assault/Sniper 코드 연동 완료 (UnitType enum, UnitStats, UnitProductionStats, ProductionPanelUI 생산 버튼) |
| 2026-03-14 | 공격 애니메이션-타격 시각 동기화 완료 (AnimationEventRelay + Animation Event + scale punch) |
| 2026-03-14 | 유닛 스탯 재조정 (ATK: Pistoleer=6, Assault=1, Sniper=10 / cooldown=클립 길이 기준 DPS 산출) |
| 2026-03-14 | 유닛 메시 방향 보정 완료 (하위 Mesh Y=30°, Root Motion OFF, _meshYOffset=공격 전용) |
| 2026-04-29 | 유닛 메시 방향 보정 전면 통일 — 전 유닛(9종) Mesh Y=0, _meshYOffset 코드 제거, 이동 anim offset=0, DirectionAngles={60,120,180,240,300,0} (FlatTop 실제 월드 각도로 재계산) |
| 2026-03-14 | 유닛 회전 DOTween 보간 완료 (이동/공격 모두 DORotate + Ease.OutQuad, _rotationDuration SerializeField) |
| 2026-03-14 | 공격 후 Walk 복귀 버그 수정 (타겟 소멸 시 Play(StateWalk) 명시 호출, 멀티/싱글 공통) |
| 2026-03-15 | 로비 씬 분리 MVVM 완료 (Lobby/Game 씬 분리, UniRx 기반 MVVM, 탭 4개 + 전투 서브화면 5개, 씬 빌드 완료) |
| 2026-03-15 | 멀티플레이 게임 시작 버그 수정 (Host OnClientConnectedCallback 누락 → HandleClientConnected 추가) |
| 2026-03-16 | 랜덤 매칭 게임 씬 전환 버그 수정 (GetHashCode 크로스-프로세스 비결정성 → GetStableHash polynomial hash 교체) |
| 2026-03-17 | 전역 로딩 스크린 완료 (LoadingScreen.cs 싱글턴, Lobby 씬 UI, 씬 전환 자동 Hide) |
| 2026-03-17 | 멀티플레이 로비 복귀 버그 수정 (Inspector _lobbySceneName 값 오류 발견, 로컬 독립 처리로 설계 변경, 30초 자동 복귀 타이머) |
| 2026-03-17 | 커스텀게임 재경기 시스템 완료 (양측 동의 재경기, RematchRequestPopup 신규, 레이스 컨디션 처리) |
| 2026-03-18 | 랜덤매칭 재경기 지원 완료 (커스텀게임과 동일 흐름) |
| 2026-03-18 | 건물 인근 타일 이동/공격 불가 버그 수정 (HexPathfinder goal blocked 체크 제거, Epsilon=0.05f 추가) |
| 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic) |
| 2026-03-19 | UI DOTween 애니메이션 프레임워크 완료 (UIAnimator + AnimatedPanel, SlideFromTop/Bottom, blocksRaycasts 버그 수정) |
| 2026-03-20 | 코드 정리 완료 (TeamAssigner.cs 삭제, 주석 정리, IsNetworkMode() 헬퍼 추출) |
| 2026-03-20 | 싱글플레이 ViewConverter 초기화 버그 수정 (Reset() → LocalPlayerTeam 기반 Setup, ApplyConfig() 직후 호출) |
| 2026-03-23 | 자동/수동 생산 하이브리드 시스템 완성 (전역 규칙 5가지, IsCharged 기반 골드 차감, BUG-01~13 수정) |
| 2026-03-24 | Game UI Lifecycle Framework 완료 (IGameUI 인터페이스 + GameUIManager, 멀티플레이 클라이언트 팝업 버그 수정) |
| 2026-03-26 | 유닛 NGO NetworkObject 전환 완료 (NetworkTransform 위치 동기화, 클라이언트 예측 제거) |
| 2026-03-27 | 이동 전 회전 선행 완료 (Rotate-then-Move, _isPreRotating 플래그) |
| 2026-03-27 | 공격 타이밍 정밀화 완료 (타격 프레임 데미지, 타겟 고정, 쿨다운 통일) |
| 2026-04-04 | 전투 애니메이션 시스템 재정비 완료 (3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정) |
| 2026-04-04 | 자동생산 BUG-20 수정 (CompleteProduction IsCharged 리셋 누락) |
| 2026-04-06 | 로비 종족 선택 UI 완료 (RaceId enum 3종족, 캐러셀 방식, RenderTexture 캐릭터 미리보기, DOTween 전환) |
| 2026-04-06 | 종족명 자연→초월 변경 (Nature→Transcendence enum 전체 rename) |
| 2026-04-06 | Pistoleer Idle 애니메이션 버그 수정 (Pistoleer.controller Idle m_Speed 0→1) |
| 2026-04-06 | Android URP RenderTexture 잔상 + RenderPass 에러 수정 (RT antiAliasing 2→1, allowMSAA/allowHDR false) |
| 2026-04-07 | 종족 인게임 적용 완료 (UnitFactory/BuildingFactory 종족별 6세트 분기, 에디터 자동 연결 스크립트) |
| 2026-04-11 | UnitType 개편 + 근접 사거리 시스템 완료 (9종 독립 enum, FindPathToNeighbor, ClaimedTile non-walkable 예외) |
| 2026-04-11 | 근접 공격 거리 다듬기 완료 (MeleeContactDist=0.3f, BuildingDetectionRadius=0.2f, 타겟 타입별 분리) |
| 2026-04-11~12 | 원거리 유닛 공격 중 회전 추적 완료 (Transform 참조, RotateTowards 270°/s, 타겟 고착성, 멀티 실기 MULTI-001~007 PASS) |
| 2026-04-12 | 종족+팀별 건물/유닛 초상화 완료 (BuildingRacePortraitSet 6세트, Inspector 연결) |
| 2026-04-13 | Spirit Blue ManaRift 초상화 스프라이트 제작 완료 (초상화 세트 전 종목 완성) |
| 2026-04-13 | 유닛/건물 스탯 확정 적용 완료 (Spirit/Transcendence 6종 HP/ATK/생산시간/비용, Transcendence 건물 HP 종족별 분기) |
| 2026-04-13 | 피격 시 부유 HP 텍스트 완료 (FloatingHpText DOTween, 오브젝트 풀, 줌 스케일링, 멀티 클라이언트 표시) |
| 2026-04-13 | 부유 텍스트 팀별 색상 완료 (Blue=연두, Red=노랑, Inspector 조정 가능) |
| 2026-04-18 | 타겟 고정(Target Lock) 데미지 불일치 버그 수정 — 멀티플레이에서 바라보는 타겟(B)이 아닌 가까운 유닛(C)에게 데미지가 적용되던 버그. NetworkCombatController.TickCombat() damageTargetId 지역 변수 분리로 수정. 멀티 실기 PASS |
| 2026-04-19 | 유닛 생산 패널 전면 재작성 완료 — PendingQueue 단일 큐 구조(QueueSlot), 자동/수동 통합, CancelAutoTypeIfNeeded 헬퍼 추가. 싱글 실기 TC-001~018 전체 PASS |
| 2026-04-19 | 생산 슬롯 깜빡임 버그 수정 — 큐 비어있을 때 자동 등록 시 슬롯1→슬롯0 1프레임 이동 버그. ToggleAutoProduction에서 !CurrentProducing.HasValue이면 즉시 TryStartNext 호출로 수정 |
| 2026-04-30 | 종족/팀 초상화 및 생산 연동 정비 — UI 스킨 로직 제거, 종족별 Unit/Building Entries 리스트 기반 동적 바인딩 및 프리팹 생성 일치화 완료. 인스펙터 구조 단순화. |
| 2026-05-14 | 유닛 회전 시스템 전면 개편 완료 — 방향 계산 Atan2 통일 (FacingDirection.FromCoords 제거), A*/정렬/추격 모든 단계 RotateTowards 적용, _rotationSpeed 단일 Inspector 필드로 통일. MovementLogger.cs 삭제. |
| 2026-05-15 | 랜덤 매칭 후 캐릭터 잘못 표시 버그 수정 — Lobby 씬 CharPreview 오브젝트가 실제 유닛 프리팹(NetworkTransform 자동 추가됨) 인스턴스여서 Host의 캐러셀 위치가 Red 클라이언트로 동기화되던 원인 확정. Unpack Completely 후 UnitView/AnimationEventRelay/NetworkUnit/NetworkTransform/NetworkObject 5종 제거. |
| 2026-05-15 | 혼잡도 기반 유닛 분산 시스템(v2) 완료 — 모든 유닛이 세로 줄지어 이동하는 현상 개선. 타일별 혼잡도 누적(CongestionMap) + 혼잡도 가중 A*(CongestionAwarePathfinder) 도입. CastleApproachManager(v1) 폐기 삭제. 설정값(DecayInterval/CongestionWeight)은 GameConfig에 통합. 사용자 테스트 PASS. |
| 2026-05-16 | 랠리포인트 깃발 팀별 표시 분리 완료 — 클라이언트 설정 시 호스트에도 깃발이 표시되던 버그 수정. RallyPointChangedEvent에 TeamId 추가, ProductionTicker에 로컬 팀 필터 추가. 싱글플레이 영향 없음. |
| 2026-05-16 | 유닛 생산 실패 피드백 시스템 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d98b855f18ec15ac) |
| 2026-05-16 | 건물 배치 패널 실패 피드백 + UI 개선 완료 — 골드 부족 시 건물 비용 텍스트 빨간색(OnResourceChanged 실시간 갱신) + 토스트 메시지 + 팝업 유지. 비용 텍스트 'G' 접미사 제거(숫자만 표시). ToastUI SetActive 버그 수정(ClearAll/FinishCurrent의 SetActive 제거 → blocksRaycasts+interactable로 대체). |
| 2026-05-17 | 건물 생성/파괴 시 유닛 이동 멈춤 수정 완료 — 경로 재계산 시 코루틴을 즉시 재시작하지 않고 _pendingPath 예약 방식으로 교체. 앞 타일이 막힌 경우에만 즉시 재시작. UnitView.cs 단독 수정. |
| 2026-05-17 | 자동 생산 Rule 20 슬롯0 확장 완료 — 슬롯0에서 수동 A 생산 중 A 자동등록 시 슬롯1에 중복 추가 없이 슬롯0을 자동으로 전환(CurrentIsAuto=true). 완료 후 자동 순환 자연 시작. GameSystemRules.md 규칙 20 문구 업데이트. |
| 2026-05-17~18 | 건물 업그레이드 시스템 완성 — BuildingType enum 26종 확장(단일 Barracks → 종족별 생산라인×3단계). BuildingTypeHelper.cs 신설(Domain: IsProductionBuilding/GetStage/GetNextStage/CanUpgrade). BuildingData.Stage 파생 프로퍼티. BuildingStats.GetUpgradeCost() + _totalInvestedCostCache(누적 투자비). GameEvents.OnBuildingUpgraded 이벤트. BuildingPlacementUseCase.UpgradeBuilding(). BuildingFactory.UpgradeBuildingObject(). NetworkBuildingController RequestUpgradeServerRpc/UpgradeBuildingClientRpc. ProductionPanelUI BuildingUnitMapping + requiredStage 단계별 잠금 + UpgradeButton + ToastKey.UpgradeRequired. GameBootstrapper 누적 투자비 캐싱. 신규 3D 에셋(HumanBarracks2/3, AncientGrove1/2/3, PrimalSanctuary1/2/3) Blue/Red 완료. |
| 2026-05-18 | 건물 스탯 전체 확정 + BuildingStatsConfig 완성 — StatsReference.md 기준으로 3종족 전체 건물(기지/채굴소/배럭/방어포탑/특수) HP·비용·공격력·업그레이드비용·힐량 확정. BuildingStatsConfig.asset 3개 항목 → 32개 BuildingType 전체 채움. BuildingTypeEntry에 종족별 AttackCooldown(float) 필드 추가, BuildingStats.GetAttackCooldown() API 신규. AutoTower 쿨다운 적용(Human/Trans 5.0s, Spirit 3.5s). MistShrine 힐량 1 HP/s 범위 3 확정. |
| 2026-05-18 | ProductionPopup UI 레이아웃 재구성 완료 — BuildingIconEntry 팀별 Sprite 분리(blueIcon/redIcon), 2유닛 건물 [유닛1][빈슬롯][유닛2] 레이아웃(CanvasGroup alpha=0 방식), UpdateButtonPortraits() 2유닛 슬롯 매핑 수정, HeaderText 건물 이름 동적 표시, 철거 환불 누적 계산(GameBootstrapper 초기화 시 체인 순회 캐싱 → BuildingStats.GetTotalInvestedCost()). |
| 2026-05-18 | 2/3단계 건물 랠리 마커 미표시 버그 수정 — ProductionTicker가 OnBuildingUpgraded를 구독하지 않아 업그레이드된 건물이 UnitProductionUseCase._states에 미등록되는 문제. OnBuildingUpgraded 핸들러 추가: UnregisterBarracks(OldBuildingId) + RegisterBarracks(NewBuilding). 전 종족(Human/Spirit/Transcendence) 테스트 통과. |
| 2026-05-18 | 건물 철거 시스템 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-b8d068c65503455b) |
| 2026-05-18 | OnEntityDied 이벤트 분리 리팩토링 완료 — 단일 공용 이벤트(OnEntityDied/EntityDiedEvent) 삭제 → OnUnitDied(UnitDiedEvent) + OnBuildingDied(BuildingDiedEvent) 강타입 이벤트로 분리. 발행 4곳·구독 9곳 전면 교체(13개 파일). 구독자의 is-캐스팅 타입 필터 전면 제거. NetworkCombatController 서버 구독 단일→2개 분리. RPC 시그니처(EntityDiedClientRpc)는 호환성 유지. |
| 2026-05-18~19 | 비생산 건물 공용 액션 패널 UI 완료 — BuildingPanelBase 추상 베이스(Template Method 패턴) 신규. BuildingActionPanelUI(비생산 건물 팝업 + 철거 버튼) 신규. ProductionPanelUI BuildingPanelBase 상속 리팩토링. InputHandler CanShowActionPanel 분기 추가. GameBootstrapper 비생산 건물 환불 캐시 루프 추가. SetupBuildingActionPanelUI 에디터 스크립트 신규. |
| 2026-05-19 | 인게임 설정 메뉴 + 게임 포기 기능 완료 — InGameSettingsUI(IGameUI, 싱글 일시정지 timeScale=0, SharedBackground 등록) + ConfirmPopup(범용 확인 팝업, BlockingOverlay 공유 Background 차단) + GameEndUseCase.Forfeit()(싱글 포기) + NetworkGameEndController.ForfeitServerRpc(RequireOwnership=false, AnnounceWinnerClientRpc 재사용) + SetupInGameSettingsUI 에디터 스크립트(HUD 재배치 + 패널 생성 + 배선). AnimatedPanel._backgroundOverlay 배선 추가. |
| 2026-05-23 | AuthSystemRules.md 작성 완료 — Firebase Auth 기반 로그인 시스템 설계 규칙 확정. 로그인 방식 3종(익명/Google Play Games/이메일+비밀번호), Firebase UID → UGS 브릿지(SignInWithCustomIdAsync), 백엔드 Option A(Firebase 생태계: Firestore 실시간 리더보드 + Google Play Billing IAP) 확정. Login.unity 별도 씬으로 분리 예정. |
| 2026-05-24 | 로그인 시스템 C# 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-9e58370baf7e9556) |
| 2026-05-24 | UGS 401 Unauthorized 버그 수정 — Lobby/Relay API 호출 시 HTTP 401 에러로 매칭 실패. 원인: IsSignedIn=true(기기 캐시)이지만 서버 토큰이 만료된 상태에서 재로그인을 건너뛰어 만료된 토큰으로 UGS API 호출. UnityServicesInitializer.InitializeAsync()를 항상 SignOut() → SignInAnonymouslyAsync() 순으로 수정하여 매 초기화 시 유효한 토큰 보장. 커스텀 게임 + 랜덤 매칭 모두 정상 동작 확인. |
| 2026-05-25 | 공통 UI 규칙 수립 및 CanvasGroup Rule 5 전환 완료 — 공통 UI 규칙 10개 확정(GameSystemRules.md). 로비 7개 뷰(LobbyRootView, BattleMainView, CustomGameView, CustomHostView, CustomJoinView, RandomMatchView, ProfileView) SetActive → CanvasGroup(alpha/blocksRaycasts/interactable) 전환. 랜덤 매칭 대기화면 GameObj inactive 버그 Inspector 직접 수정. |
| 2026-05-26 | 로비 배경 Safe Area 수정 완료 — LobbyRoot Image가 SafeAreaContainer 안에 위치해 Safe Area 경계에서 배경이 끊기는 버그 수정. Canvas 직속 자식 LobbyBackground 오브젝트 신규 추가(전체화면 stretch, 남색), LobbyRoot Image 비활성화. FixLobbyBackground.cs 에디터 스크립트로 적용. 실기기 테스트 PASS. |
| 2026-05-27 | 로비 CanvasGroup Rule 5 전환 실기 테스트 완료 — TC-SINGLE-001~014 전체 PASS. TC-SINGLE-001(기본 탭 표시), TC-SINGLE-002~005(탭 전환), TC-SINGLE-006~008(커스텀 게임 흐름), TC-SINGLE-009~010(에러 메시지 표시/사라짐), TC-SINGLE-011(방 참가), TC-SINGLE-012~014(랜덤 매칭). TC-SINGLE-015~016(로그인 섹션 전환)은 로그인 미구현으로 SKIP. |
| 2026-05-29 | BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 완료 — TC-SINGLE-BAP-001 FAIL(패널 높이, X버튼 위치 불일치) 해결. BuildingPlacementUI와 동일한 3x3 VLG+HLG 그리드 구조. 래퍼 anchoredPosition/sizeDelta 오프셋 제거. CancelButton 위치 통일. BuildingActionPanelUI.cs에 _allSlotButtons/_activeSlotButtons 필드 추가, OnShow() 오버라이드에서 런타임 CanvasGroup alpha 제어(BuildingPlacementUI._buttonCanvasGroups 패턴 동일). HeaderText 앵커 순수 앵커 기반 변환. |
| 2026-05-30 | 로비 UI 에셋 제작 완료 — TabBar 아이콘 4종(Battle/Shop/Profile/Ranking), 공통 아이콘 2종(Settings/Quit), 로비 버튼 아이콘 9종(SinglePlay/RandomMatch/CustomGame/CreateRoom/JoinByCode/Email/Logout/Back/Cancel), 버튼 배경 2종(Primary/Secondary), 랜덤 매칭 스피너 1종(HexOrb). 총 18종 에셋. 저장 경로: Assets/_Project/Sprites/UI/. AssetList.md 업데이트 완료. |
| 2026-05-29 | BuildingPlacementUI 씬 계층 재설계 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-121ea22f139df73f) |
| 2026-05-31 | 건물 업그레이드 생산 상태 처리 오류 수정 완료 — 업그레이드 시 생산 중 골드 환불 누락 + 랠리포인트 초기화 버그 2건 수정. ProductionTicker.OnBuildingUpgraded()에서 UnregisterBarracks → CancelAllQueue 교체(환불 포함), CancelAllQueue 호출 전 RallyPoint 저장 후 RegisterBarracks 완료 시점에 SetRallyPoint로 복원. 수정 파일: ProductionTicker.cs 1개. 실기 테스트 PASS(골드 환불 확인, 랠리포인트 유지 확인). |
| 2026-06-01 | 방어 타워(AutoTower) 공격 기능 구현 완료 — TowerCombatUseCase 신규(Application 레이어). 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 타겟 선택: 월드 좌표 기준 가장 가까운 적 유닛. 배치 즉시 공격 가능(쿨다운 0 초기값). 멀티플레이 서버 권위 처리(NetworkCombatController.TickCombat에 타워 Tick 추가). 실기 테스트 PASS. |
| 2026-06-05 | 자동생산 재등록 슬롯 중복/누락 버그 구조 개선 — CurrentIsAuto를 수동 필드에서 파생 계산 getter로 전환(IsAutoMode와 동일 패턴). AutoTypes에서 타입 제거 시 getter가 자동으로 false 반환 → UnregisterAutoType·DisableAutoMode 수동 reset 불필요. RegisterAutoType에 PendingQueue.Count==0 조건 추가로 큐에 다른 항목 있을 때 슬롯3 추가 허용. GameSystemRules 규칙 20 보완. TC-01~06 정적분석+실기 전체 PASS. |
| 2026-06-08 | 전체 유닛 사망 VFX 적용 완료 — EffectPreset_Unit_Death_Common.asset 신규 생성(vfx_unit_death.prefab + 사망 SFX). SetUnitDeathVfxAll 에디터 스크립트로 UnitEffectConfig 전체 24종 유닛 deathPreset 일괄 연결. 기존 EffectPreset_Pistoleer_Death.asset 삭제. 코드 변경 없음(에셋 작업만). |
| 2026-06-05 | 자동생산 완료 사이클 슬롯2 깜빡임 버그 수정 — 자동생산 완료 시 재순환 항목이 슬롯2에 1프레임 표시되다가 사라지는 버그. CompleteProduction에서 ChargeVisibleSlots+OnProductionQueueChanged 직접 발행 제거 → TryStartNext 즉시 호출로 대체. 2026-04-19 AddNewAutoSlot 수정과 동일 패턴(완료 사이클 경로가 미처 처리되지 않았던 것). 실기 테스트 PASS. |
| 2026-06-02 | Human CannonTower 초기 방향 설정 완료 — BuildingFactory.GetInitialRotation() 신규. "내 진영 vs 상대 진영" 기준으로 회전 결정(Blue/Red 팀 색깔 기준 아님). ViewConverter.IsFlipped로 로컬 팀 판별 → 상대 포탑 Y180도, 내 포탑 기본값. 실기 테스트 PASS. |
| 2026-06-02 | UnitStatsConfig 미사용 필드 제거 + 스탯 정비 완료 — AttackKind enum/Kind 필드/GetAttackKind() 제거(2026-05-11 비활성화 코드 삭제). occupancySize 제거(TileOccupancyManager 미구현). 유닛 9종 attackCooldown/hitFrameTimes를 StatsReference.md 기준으로 정비. StatsReference.md 업그레이드 비용 표기 출발점 기준으로 통일. ApplyStatsReference.cs 에디터 스크립트로 일괄 적용. |
| 2026-06-05 | BuildingView Missing Script 정리 완료 — Spirit/Transcendence 건물 프리팹 8개에서 삭제된 BuildingView 컴포넌트 참조 제거. Editor 스크립트 1회 실행으로 일괄 처리. |
| 2026-06-05 | 신규 유닛 프리팹 컴포넌트 부착 에디터 스크립트 완료 — Human 5종·Spirit 6종·Transcendence 5종 × Blue/Red 총 32개 프리팹에 UnitView/AnimationEventRelay/NetworkUnit 등 컴포넌트 자동 부착. Assets/Editor/Setup/SetupNewUnitPrefabs.cs. 실기 테스트 예정. |
| 2026-06-06 | NetworkGameManager 고아 필드 + Game씬 중복 NGM 제거 완료 — GameBootstrapper._networkGameManager SerializeField(코드 사용처 없는 고아 필드) 제거. Game.unity에 중복 배치된 NGM GameObject 제거(DontDestroyOnLoad 구조상 Lobby씬 NGM이 유지되므로 불필요). 싱글플레이+멀티플레이 실기 PASS. 콘솔 DontDestroyOnLoad 경고 제거. |
| 2026-06-08 | 멀티플레이 유닛 사망 GO 미파괴 + 이펙트 미재생 버그 수정 (NGO Despawn 패턴 정립) [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9e0b4cd0a5f502f) |
| 2026-06-08 | 유닛 VFX 디테일 개선 3종 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9d1fcf0f00f7cd0) |
| 2026-06-10 | 전 종족 AI 빌드오더 시나리오 문서 완료 — Human 시나리오 골드 수지 검토 추가(A=900g/B=1,200g/C=1,400g, 5,300g 대비 최대 26% 확인). Spirit 3개 시나리오(Spirit-Inferno/Spirit-Torrent/Spirit-Quake) 신규 작성: 각 원소 라인 메인 집중형, 총 건물 비용 1,450g. Transcendence 3개 시나리오(Trans-Rush/Trans-Flora/Trans-Beast) 신규 작성: 초반 물량/균형/고테크 분화, 총 건물 비용 1,150~1,450g. 3종족 9개 시나리오 전원 5,300g 경제 범위 내 검증 완료. GameSystemRules_AI.md에 Spirit/Transcendence 건물-유닛 매핑 및 시나리오 참조 섹션 추가. |
| 2026-06-10 | AI 시나리오 ScriptableObject 3종족 개편 완료 — 종족별 단일 에셋(3시나리오 내장), Domain 레이어 아키텍처 정리(DifficultyLevel/BuildOrderStep/AIActionType), 종족 분기 로드 구현 |
| 2026-06-10 | 사운드 시스템 코드 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2e63e82f37cf7085) |
| 2026-06-18 | 전역 UI 시스템 도입 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1d781d78610b2c36) |
| 2026-06-15 | Lobby.unity 규칙 전수 재점검 및 추가 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-24f8e1ed783bcd5e) |
| 2026-06-22 | ProfileView 로그아웃 버튼 UI 추가 완료 — `AddLogoutButtonToProfileView.cs` 에디터 스크립트. LogoutButton GO(Button+Image+TextMeshProUGUI "로그아웃", Maplestory Bold SDF) 생성 + `ProfileView._logoutButton` Inspector 자동 연결. 코드 변경 없음(ProfileView._logoutButton + OnLogoutClicked()는 기존 구현 완료 상태). Firebase 익명 로그인 후 로비 프로필 탭에서 로그아웃 → Login 씬 복귀 동작 확인. |
| 2026-06-22 | Lobby 탭 패널 CanvasGroup 에디터 사전 부착 완료 — `SetupLobbyPanelCanvasGroups.cs` 에디터 스크립트(`Hexiege/Setup/Lobby 패널 CanvasGroup 설정`). 비활성 상태였던 ShopPanel/ProfilePanel/RankingPanel 3개 활성화 + 4개 패널 모두 CanvasGroup 부착(BattlePanel alpha=1, 나머지 alpha=0). `LobbyRootView.Awake()` EnsureCanvasGroup() → GetComponent<CanvasGroup>() 교체, EnsureCanvasGroup() 헬퍼 삭제. 런타임 AddComponent 방식 폐기(Rule 5 원칙: 컴포넌트는 에디터에서 미리 부착). 실기 테스트 PASS. |
| 2026-06-22 | LoadingIndicator 최소 표시 시간 1초 보장 — `UIManager.ShowLoading(false)` 호출 시 표시 후 1초 미만이면 `WaitForSecondsRealtime`으로 지연 후 숨김. `_loadingMinDuration` SerializeField(기본 1f) 추가. 모든 호출부(LoginBootstrapper, EmailLoginView, BattleViewModel 등) 자동 적용. 실기 테스트 PASS. |
| 2026-06-22 | LoadingIndicator 독립 Canvas(300) 추가 — AnonymousWarningPopup(SortingOrder=200)이 LoadingIndicator를 가리는 문제 해결. `LoginUiSetup.cs` 에디터 스크립트에 `Hexiege/Setup/Login UI — LoadingIndicator 독립 Canvas 추가` 메뉴 항목 추가. LoadingIndicator에 독립 Canvas(SortingOrder=300, overrideSorting=true) + GraphicRaycaster 자동 부착. |
| 2026-06-22 | ConfirmPopup + NetworkErrorPopup 씬 버그 수정 — ① ConfirmPopup 루트 CanvasGroup 씬 프리팹 인스턴스 오버라이드(alpha=0, interactable=0) 수정 → 팝업 표시 및 버튼 클릭 정상화. ② NetworkErrorPopup에 남아있던 ConfirmPopup 컴포넌트 교체 → NetworkErrorPopup.cs로 교체, `_panel` 슬롯 연결. ③ LoginRootView + LoginBootstrapper의 `_networkErrorPopup` 슬롯 연결. 테스트 버튼을 통한 전체 흐름(ConfirmPopup → 확인 → NetworkErrorPopup) 정상 동작 확인. |
| 2026-06-10 | Firebase-UGS OIDC Bridge 구현 완료 — `SignInWithOpenIdConnectAsync("oidc-firebase")` 도입. 실계정(Google/이메일) 로그인 시 Firebase ID Token → UGS OIDC 연결로 기기 변경 후에도 PlayerId 일관성 보장. 익명 계정은 기존 UGS 익명 로그인 유지. UnityServicesInitializer OIDC 세션 덮어쓰기 방지 로직 적용. BridgeToUGSAsync Task<bool> 전환으로 UGS 미연결 상태 감지 가능. UGS 데이터 플랫폼(Cloud Save/Leaderboard/Economy) 채택 확정. Firebase Console + UGS Dashboard 설정 완료 후 실기 테스트 예정. |
| 2026-06-22 | BlockingOverlay 씬 전환 후 미표시 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d2698fee035d8188) |
| 2026-07-16 | 로비 프로필/랭킹 클라우드 연동 1차 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2cd83c4f8c4440e1) |
| 2026-07-18 | 이메일 인증 플로우 보정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-9d4c5b354eedeb1f) |

## 2026-10-09 Assault bounded clock-history readiness 추가 기록

이전 source 미구현/Unity 메뉴 대기는 당시 이력이며 현재는 승인4 source 구현 및 diagnostic gate 완료다. [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md](_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md)의 “Clock-history 최종 파일 인벤토리 (2026-10-09)”에 전체 source4·Temp38·문서/기준값 목록과 정확한 근거를 연결했다. Main 실제 Unity Action13:41:11→13:41:36 overall/C2/C3 PASS, Root13:44:44 PASS; errors0/warnings27. static4 errors0, Main CLR3 exit0. Main frozen4 manifest hash 및 protected Game/Login/Stats/Assault anim baseline 동일 확인; 빌드/씬 저장 없음.

실제00_10 Expired4/source-marker/PhaseClockRegression/실기기 오류/timing/pixel/성능/NGO native query count는 OPEN. 다음은 사용자 새 Development 빌드→경기 종료→로비 복귀→저장(EndSession dump); 강제 종료/종료 전 reset 시 누락 가능. 구현은 원인 분석용 numeric ring/frozen history이지 오류 해결 판정이 아니다.
