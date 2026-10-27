# Hexiege - 작업 이력

**문서 역할**
- **목적·질문:** 주요 완료 작업을 날짜와 근거로 찾아가는 마일스톤 인덱스다.
- **포함:** 완료 시점·핵심 결과·당시 확인 범위와 상세 링크.
- **제외·담당:** 현재 판정은 [현황](PROJECT_STATUS.md), 남은 일은 [ROADMAP](ROADMAP.md), 상세 실행은 [이력](History/ImplementationVerificationHistory.md)·기존 Task/Log.
- **원본·참조:** 완료 마일스톤의 이력 인덱스이며 과거 PASS를 현재 전체 수용으로 해석하지 않는다.
- **갱신:** 주요 작업이 완료될 때 짧은 행과 근거 링크를 추가한다. 기존 고유 원문·정정 경위는 보존하고 긴 재인계는 상세 이력으로 연결한다.

## 마일스톤 이력

아래는 해당 시점에 끝난 구현·문서화·분석 단계의 이력이다. 당시 OPEN/실기 대기는 현재 상태가 아니며 최신 수용은 [Status](PROJECT_STATUS.md)·[Matrix](Assets/UnitCombatAssetMatrix.md)를 따른다. 긴 실행 통계·커밋별 과정은 [직전 본문 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory)에 보존했다.

| 날짜 | 마일스톤 |
|---|---|
| 2026-10-27 | 서버 권위 투사체 마무리 정리 P5 코드 · 문서 완료(사용자 확인 대기): 결정 P5-A ~ P5-J 추천안 채택 → 닿지 않는 코드 · 주석 블록 삭제(`9cefe80d`), 이행 스위치를 투사체 분류 질문으로 교체 — handshake 토큰 값 · 스키마 6 유지(`b73c0993`), 이행 전용 진단 칸 삭제 · 진단 형식 버전 상향(`d77b8e68`). 규칙 문서 상태 표시 · 적용 범위 문구(Buildings 규칙 12) · 네트워크 TDD 서버 투사체 절 반영. 정적 확인만 — 컴파일 · 회귀 스위트 · 멀티 실기는 사용자 확인 대기. [근거](<_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#11-9-p5-execution-log>) |
| 2026-10-27 | 서버 권위 투사체 화살 도달 거리 후속 완료 · main 반영: EagleArcher 화살이 대상에 약간 못 미쳐 사라지던 문제를 화살 프리팹 자식 위치 조정(수정안 (c), `33ce4257` — 데이터만, 피해 시각 불변)으로 고쳐 13:45 멀티 실기 로그 정상(scoped)·사용자 「잘 수정된 것 같고」 → 사용자 테스트 PASS(회귀 스위트는 무언급 PASS 기준). 단계 E 전체가 `origin/main`에 fast-forward(`33ce4257`). 스위치 정리(P5)와 후속 후보 R-A1~R-A3은 남음. [근거](<_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-10-arrow-reach-followup>) |
| 2026-10-27 | 서버 권위 투사체 단계 E 완료: EagleArcher(팀별 화살 tracer Blue · Red — 사용자 메뉴 실행 에셋 `be9b6e1a`, 착탄 이펙트 없음)와 MushroomBomber(옛 서버 writer 스냅샷 발행 끔, Client 독 VFX 시작을 화면 착탄까지 보류)가 서버 투사체로 전환(`5b8fb82e`, 스위치 `6,7,12,17,21,24,26;tower:human` — 투사체 유닛 7종 전원). 회귀 스위트 1차 FAIL은 화상 v2 프리팹의 검사 재현 한계로 검사 코드만 수정(`a5aa781d`) → 재실행 오류 없음. 사용자 육안 「잘 작업된 것으로 보여」·실기 로그 scoped → **단계 E 사용자 테스트 PASS**. 화살 도달 거리 후속·스위치 정리(P5)는 남음. [근거](<_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-stage-e-field-check-20261027>) |
| 2026-10-27 | 상태 이펙트 화상 v2·튐 수정 완료: 몸통 폭 맞춤(`eb660f70`)·카메라 쪽 당김(`7f787d21`) 뒤 사용자가 화상을 v2로 다시 만들어 연결(`5dd22db7`, 앞 두 옵션은 꺼짐), 뒤집힌 Client에서 화상·독이 회전 중 옆으로 튀던 문제를 따라가기 직전 바로잡기로 수정(`ec621589`). 04:15 멀티 실기 사용자 「특별히 문제 발견되지 않음」·로그 정상(scoped) → 사용자 테스트 PASS(회귀 스위트는 같은 날 정한 무언급 PASS 기준), `origin/main` 병합 `0a47f37f`(병합 뒤 Unity 컴파일 미확인). 가시성 강화는 ROADMAP 후속. [근거](<_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md#13-튐-수정-뒤-멀티-실기-확인과-작업-완료-2026-10-27-0415-로그>) |
| 2026-10-26 | 서버 권위 투사체 중간 단계: 단계 D(`a0a23423` InfernoSpirit·FoxMagician)와 D-2(`fb77548c` Client 사망 표현·화상 VFX를 화면 착탄까지 보류, 기기 파티클 오류 수정)가 서버 투사체로 동작하고, D-2까지 main 병합·원격 반영(`f367cb52`). 실기 로그 scoped·사용자 육안 정상 → **단계 D·D-2 사용자 테스트 PASS**(2026-10-27 정정 — 회귀 스위트는 사용자 FAIL 언급 없음 → PASS 간주, BearGuard 화상 크기 U-20은 화상 v2 Task로 종결, 늦은 접속은 사용자 결정으로 실기 테스트 생략. 종전 「회귀 스위트 미보고라 단계 D 사용자 테스트 PASS가 아니다 — BearGuard 화상 VFX 크기 결정 대기」), EagleArcher·MushroomBomber 남음. `NET-TIME-002` 문장 정리는 같은 날 사용자 결정으로 현재 동작에 맞춰 반영(종전 「… 개정 승인 대기」). [근거](<_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#17-stage-d2-death-hold-check-20261026>) |
| 2026-10-23 | UI후속 구현/기수용 항목에 이어 최종 랭킹10행 높이·배치 사용자 정상 확인 완료. UI39파일 de2a424b와 통합기록69b5e5c4 로컬main FF 완료·push 없음. 빌드 완료/설치/실행의 에이전트 직접 관측과 구분. [구현과 수용 범위](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023) |
| 2026-10-21 | 랜덤 매칭 정상 진입・상대 인터넷 단절 시 Host 30초 실패 복구・복구 후 재매칭 교정 사용자 수용 완료. 인증/취소 경쟁 등 별도 미관측과 원 HTTP 429 미확정은 보존. [근거](_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md#13-랜덤-매칭-세-단계-사용자-수용-완료--2026-10-21) |
| 2026-10-21 | 서버 권위 투사체 중간 단계: 단계 A~C(`d09cc4a9` StreamSpirit · `639ac1aa` Tank·CannonCart · `64b743c8` 사망타 숫자 · `b819dc51` 인간 대포 타워)가 서버 발사 → 착탄 때 피해로 전환. 사용자 육안 확인·회귀 스위트 PASS(Console)·실기 로그 scoped 범위이며 작업 전체 완료가 아님 — 단계 D(InfernoSpirit·FoxMagician) 진행 중, EagleArcher·MushroomBomber 남음. [근거](<_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#15-stage-c-human-cannon-tower-check-20261021>) |
| 2026-10-21 | 로그 보관 ZIP 생성 에디터 도구(`Hexiege > Logs > Create Log Archive ZIP`, 업로드는 사용자) 사용자 실행 ZIP 3회·메인 세션 다운로드 대조 확인. 이후 KST 날짜 기준 변경(`fd4c04cb`)은 실행 확인 기록 없음. [근거](<_Tasks/2026-10-20/12_02_log-archive-zip-tool/Plan.md#8-실행-기록>) |
| 2026-10-20 | 익명 명시 로그아웃 자격 정리・매칭 실패 회차 복구 교정 뒤 원래 새 빌드 기기↔Editor 조합의 매칭 성공을 사용자 수용. 다른 회귀 실기는 별도. [근거](_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md#11-원래-실패-조합의-사용자-매칭-수용) |
| 2026-10-19 | 차지 유닛(InfernoSpirit·FoxMagician) 차지→발사 시점 분리(클립 `OnAttackCharge` = 차지 VFX·lease 확인만, `OnAttackHit` = 투사체+발사음, 취소 시 차지 정지)·Fox tracer/impact 연결·Inferno 불티 재질 교체를 싱글·멀티 사용자 시각 확인 범위에서 수용. 서버 피해 시점 무변경, 로그 검증 없음·회귀 스위트 미보고. [근거](<_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-implementation-result>) |
| 2026-10-19 | QuakeSpirit 공격 VFX 재제작(6갈래 바닥 균열·흙먼지 퍼프·파편, 기존 공격 슬롯) 사용자 승인. 보이는 범위 약 1.6 > 피해 반경 1.0은 사용자 선택이며 피해 규칙 무변경. [근거](<_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md#13-quakespirit-attack-vfx-rework>) |
| 2026-10-19 | Fox marker fixture의 기본 Quaternion 비교 교정 후 전체 회귀 suite 사용자 PASS 수용. production·Bloom 무변경, 기기 플레이/공격 시각·HP 수용과 구분. [근거](<_Tasks/2026-10-19/15_21_regression-suite-contract-fixes/Research.md#6-사용자-전체-suite-pass-수용과-최종-정리>) |
| 2026-10-19 | 결과 화면의 공용 배경·게임 입력 차단/복원과 중첩·재경기·이탈·강제 실패 대응을 안내된 6항목 사용자 정상 확인 범위에서 수용. [근거](<_Tasks/2026-10-19/04_39_game-end-modal-input/Research.md#7-사용자-6항목-정상-확인수용과-후속-정리>) |
| 2026-10-18 | 사용자 승인으로 PR 없이 랠리 작업 `af5ac268`을 로컬 main에 fast-forward 통합. 원격 push 미실행. [근거](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-10-pr-없는-로컬-main-통합-완료-2026-10-18-메인-수행-인계>) |
| 2026-10-18 | 랠리 사용자 수용 후 승인된 비활성·미참조 코드 정리와 별도 검증 완료. 활성 토큰 동일성 확인과 신규 실기·main 병합은 구분. [근거](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-9-불필요-코드-정리-완료별도-검증-programmer메인-전달값>) |
| 2026-10-18 | AI 랠리 5번도 사용자 싱글 정상 확인 범위에서 수용. 불필요 코드 정리·main 병합 준비는 승인 후 진행 단계로 구분. [근거](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-7-ai-5번-사용자-싱글-수용과-코드-정리main-병합-준비>) |
| 2026-10-18 | 이동 도중 랠리 목적지 건설 후속 수정의 싱글·멀티 정상 동작을 사용자 확인 범위에서 수용·종결하고 사양 문서를 동기화. AI 5번·전체 경로 차단 대응은 별도. [근거](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-6-후속-수정의-사용자-싱글멀티-확인과-수용-2026-10-18>) |
| 2026-10-18 | 랠리 최초 수정 `9aa921f9`의 사용자 싱글 1·2·3·4·6 정상 관찰·멀티 확인 보고 기록. AI는 미확인, 이동 중 건설 문제는 당시 후속 OPEN(후속 수용은 위 행). [근거](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-2026-10-18-사용자-실기-보고와-후속-조사-범위>) |
| 2026-10-15 | VFX 코드 후속(2026-10-13~15) — Bloom 힐 개수 계약·RuneSpire 번개 크리스털→가슴 조준·상태 이펙트 크기 맞춤·차지 사망 정지·측정 도구 스케일 버그, 사용자 실기 확인. 번개 사망 시 끝점 어긋남 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-1437a37849308e14) |
| 2026-10-15 | SOLO 공통 이동 복귀 사용자2경기·runtime 분석의 focused 수용. 전25종/멀티/global 수용·native 메뉴 PASS와 구분하며 광산·랠리는 제외. [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-1) |
| 2026-10-15 | 공통 이동 복귀의 공간 정합·endpoint 중복 방지 구현과 controlled 검증. 이후 수용은 같은 날짜 수용 행을 따른다. [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-2) |
| 2026-10-15 | 원래 승인 교정의 에셋·코드 반영 감사 및 입증된 누락 복원 종결. Fox 원래2:25와 기존 SOLO clock/pursuit를 보존하며 새 전체 실기 수용은 아님. [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-solo-3) |
| 2026-10-13 | 멀티 최초 경기 맵 준비 실패 안내(규칙 M-2) [상세 원문](History/ImplementationVerificationHistory.md#milestone-3e8fb934896bb20e) |
| 2026-10-04 | BloomFairy §18 최종 자동 gate/빌드 시도 인계 동기화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0f2c538f07439ea8) |
| 2026-10-04 | BloomFairy §18 최종 코드/정적 인계 문서화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-f00e1ac0a080e387) |
| 2026-10-04 | BloomFairy §18 UnitView 코드 반영 인계 문서화 [상세 원문](History/ImplementationVerificationHistory.md#milestone-5591c357ddf2e01d) |
| 2026-10-04 | BloomFairy 새 두 run·Movie_002 분석 문서 보강 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-639c407b76e22775) |
| 2026-10-03 | 01_03 사용자 경기 분석 문서 반영(추가코드교정 없음) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-03 | 공통 복구·Torrent 승인 발사 교정 구현 및 통합 인계(실기 OPEN) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-02 | 17_05 공통 전투 후 복귀 실기 분석 반영(교정 아님) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-02 | 역할교대 이동 최종 분석 반영(코드 교정 아님) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-02 | TorrentSpirit 역할교대 두 경기 파도 분석 반영(이동 원인 분석 대기) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-02 | TorrentSpirit 최신 실기 분석 반영 [상세 원문](History/ImplementationVerificationHistory.md#milestone-3e4c793b242010db) |
| 2026-10-02 | TorrentSpirit byte/clock 로그 최종 구현·정적 재컴파일 확정 인계(게임 PASS 아님) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-10-02 | TorrentSpirit 전 경기 사건 Dev 로그 구현 인계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-27d7abd7e87de52a) |
| 2026-10-01 (2차) | 규칙↔구현 전수 점검 중 코드 설명 정정 일부 완료. 다른 분류·부분 완료·실기 미확인 경계는 원문을 따른다. [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-audit-01) |
| 2026-10-01 | 팝업 로그 커버리지 + 「잡은 적 없는 몫을 놓는」 결함 수정이 실기로 확인됐다 [상세 원문](History/ImplementationVerificationHistory.md#milestone-fe2e0cde4e4eb337) |
| 2026-10-01 | TorrentSpirit 두 경기 실기 근거 문서 반영(작업 완료 선언 아님): 관측/display 17→32·HP 변화 45→121·Client heal 4→36 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-09-30 | 「경기 종료 후 UI」 마무리 회차 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9311030539c1b56) |
| 2026-09-30 | SpearMan 0:25 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-a1ad88aa498f30c1) |
| 2026-09-30 | SpearMan 0:25 생산 타임라인 저장·Unity 자동 검사 두 메뉴 PASS / focused 실기 OPEN [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-09-30 | LionKnight 두 타격 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-3319e3d37fcfa1f7) |
| 2026-09-30 | LionKnight 두 타격 설정 저장 및 첫 실기 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1a6842ceac95b683) |
| 2026-09-30 | BearGuard 0:13 focused 실기 PASS/CLOSED [상세 원문](History/ImplementationVerificationHistory.md#milestone-ba34c68c61ab7fed) |
| 2026-09-29 (2차) | 싱글 맵 실패 UI 후속의 투영·라벨·상태 줄·강제 실패 수단 구현 및 당시 실기 검증. 남은 멀티 판단·미관측 범위는 원문 참조. [원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-ui-01) |
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
| 2026-09-03 | 무작위 맵 1단계 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a5bc99c0905a3668) |
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
| 2026-08-24 | 네트워크 종료 시점 가드 8곳의 실기 검증 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a34b43345be84a72) |
| 2026-08-24 | 죽은 윈도우 경로 마지막 잔존 제거 + QA 기록 완전 중복 해소 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1a84ba47615600e4) |
| 2026-08-24 | 에이전트 메모리 3개를 「인덱스 + 토픽」 구조로 분산 [상세 원문](History/ImplementationVerificationHistory.md#milestone-4917947b7dd6feb5) |
| 2026-08-23 | RootCrossAudit false-INCONCLUSIVE 진단기 교정 · 공식 재감사 OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-3ef2918686f166e2) |
| 2026-08-23 | B3 v9 최종 통합 정적 코드 게이트 PASS · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-ba455951f237e77f) |
| 2026-08-23 | B3 v9 pre-merge self-validation PASS · main 통합 진행 중 · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-b82dde7fd9deeeac) |
| 2026-08-23 | B3 v9 typed 공간 후보/preflight pre-merge 코드 게이트 · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-01c325a72a872a08) |
| 2026-08-21 | 전제 정정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-641c110f6b0d0435) |
| 2026-08-20 | B3 v8 경로 checkpoint/공간 타일 분리 코드 게이트 PASS · 전체 FAIL/OPEN [상세 원문](History/ImplementationVerificationHistory.md#milestone-0e3ed9947d184731) |
| 2026-08-20 | 네트워크 종료 시점 가드 전수 보강 8곳 [상세 원문](History/ImplementationVerificationHistory.md#milestone-bfeed869492c6763) |
| 2026-08-20 | 에이전트 메모리 손실 사고 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7fdecd60d93d193b) |
| 2026-08-19 | 게임 종료 · 서버 종료 시점의 전투 뒷정리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-a249c79b11d00912) |
| 2026-08-19 | 게임 종료 · 서버 종료 시점의 전투 뒷정리 [상세 원문](History/ImplementationVerificationHistory.md#milestone-f7d5685dae1fd192) |
| 2026-08-19 | 전역 로그 훅의 Error 수집 확대 + 전투 컨트롤러 RPC 가드 + 연구 흐름 로그 보강 [상세 원문](History/ImplementationVerificationHistory.md#milestone-0916935c30ea17e4) |
| 2026-08-18 | B3 재경로·제자리 걷기 목표 단위 교정 구현 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7fbaaa52a7a8462e) |
| 2026-08-18 | B3 유한 재탐색 실기 회귀 진단 및 규칙 재개방 [상세 원문](History/ImplementationVerificationHistory.md#milestone-904a390fe1c87582) |
| 2026-08-18 | B3 최신 main 통합 및 Android Host 유한 재탐색 교정 구현 [상세 원문](History/ImplementationVerificationHistory.md#milestone-198730a7d614cf1c) |
| 2026-08-18 | B3 Android Host 경기 정지 진단 및 최신 main 통합 기준 확정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-24339bd2143512b1) |
| 2026-08-18 | 로그 체계(GameLog) 전환 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7c091b94b90ae75d) |
| 2026-08-17 | 로그 체계(GameLog) 전환 [상세 원문](History/ImplementationVerificationHistory.md#milestone-ee9e038a7c34f4ec) |
| 2026-08-12 | MistShrine(HealShrine) 물안개 힐 시스템 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-87e292342feeb2d4) |
| 2026-08-10 | MistShrine(HealShrine) 물안개 힐 재설계 기획 확정 + 방어 타워 표기 오류 정정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-136c4776060cbfb0) |
| 2026-08-08 | 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 완료 (실기 테스트 PASS · 커밋 9a19cd5) [상세 원문](History/ImplementationVerificationHistory.md#milestone-5c6b8f38dd62449c) |
| 2026-08-08 | 건물 파괴 시 열린 패널/조준 UI 원복 구현 완료 (실기 테스트 PASS · 커밋 8c7fa01) [상세 원문](History/ImplementationVerificationHistory.md#milestone-efef73fd490d807e) |
| 2026-08-05 | 스킬 시스템 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 구현 완료 (실기+멀티(클라) 테스트 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-56187617ca745427) |
| 2026-08-04 | 스킬 건물 시스템 Phase 1 구현 완료 (실기기 테스트 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-b898e915aa61598b) |
| 2026-08-04 | Unit ActionSequence Tracer B3 연속 이동 구현 및 Editor 조건부 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-e5072c1cff7e9aaa) |
| 2026-08-03 | Unit ActionSequence Tracer B2 서버 이동·SimulationFacing Shadow 25/25종 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-eb63f651320b4c2e) |
| 2026-07-31 | 연구소 유닛 강화 시스템 + 전투 스탯 ×10 스케일 개편 + 연구 패널 UI 구현 완료 (기능·멀티플레이 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-62d3c683a2e80148) |
| 2026-07-29 | Unit ActionSequence Tracer B1 양방향 이동 pose 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-fd111e9a8b28caba) |
| 2026-07-29 | Unit ActionSequence B1 잘못된 Collider 진단 가정 제거 [상세 원문](History/ImplementationVerificationHistory.md#milestone-6c2e8d3c60929225) |
| 2026-07-29 | Unit ActionSequence Tracer B1 rollback 검증 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-0322b6b163c65a44) |
| 2026-07-27 | Unit ActionSequence Tracer B1 asset migration·Presentation seam 적용 [상세 원문](History/ImplementationVerificationHistory.md#milestone-473e37d3a4c6c263) |
| 2026-07-27 | Unit ActionSequence Tracer B0 Visual Root migration readiness PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-33a08529f84433f9) |
| 2026-07-27 | Unit ActionSequence Tracer A2 server-authoritative pose seam shadow 구현·멀티플레이 런타임 PASS [상세 원문](History/ImplementationVerificationHistory.md#milestone-079393e95c89efd1) |
| 2026-07-22 | Unit ActionSequence Tracer A1 Pure Application 계약·stateful reducer 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-7f93b9916af2b7b1) |
| 2026-07-22 | Unit ActionSequence Tracer A0 Shadow Melee Sequence 구현·사용자 멀티 계측 통과 [상세 원문](History/ImplementationVerificationHistory.md#milestone-73b3538feee0decb) |
| 2026-07-22 | 최신 main 반영 후 유닛 전투 문서 재감사 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-07-20 | QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료 (특수 유닛 5종 마지막 = 5종 전량 + InfernoSpirit DoT까지 전부 완료, QA PASS·멀티플레이 실기 로그 검증 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-71da2aa006a17b47) |
| 2026-07-20 | InfernoSpirit(지옥불 정령) 단일 대상 지속 피해(DoT) 구현 완료 (특수 유닛 5종 중 5번째 = 전 5종 완료, QA PASS·사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-fd11c55cc62b431d) |
| 2026-07-20 | InfernoSpirit Legacy 특수 공격 반영 — 주 타깃 직접 25 후 적 유닛에만 DoT 5/초×3초(총 15), 건물은 직접 피해만 적용. 사용자 실기 범위에서 기능 확인. 공격 방향 공통 수정은 사용자 결정으로 별도 보류했다. |
| 2026-07-20 | QuakeSpirit Legacy 즉발 AoE 반영 — 주 타깃 20, 반경 내 다른 적 유닛·적 건물 10(50% 올림)을 서버에서 적용하고 Host/Client HP 로그를 확인. 기본 Attack marker와 규칙 v2 sequence/ImpactPoint는 미완료로 유지했다. |
| 2026-07-20 | 멀티플레이 유닛 이동·공격 동기화 규칙 v2 일괄 개정(문서/설계만, 런타임 변경 없음) [상세 원문](History/ImplementationVerificationHistory.md#milestone-5690f7ca4959e3c4) |
| 2026-07-20 | FlatTop 11×21 무작위 대전 맵 구현 계약 문서 동기화 완료(미구현) [상세 원문](History/ImplementationVerificationHistory.md#milestone-11aeb70840fcc8d4) |
| 2026-07-19 | FlatTop 11×21 무작위 대전 맵 기획·규칙 문서화 완료(구현 예정) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-07-19 | MushroomBomber(버섯폭격기) 착탄형 범위 DoT 딜러 구현 완료 (특수 유닛 5종 중 4번째, QA PASS·사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-aaff2b80704781de) |
| 2026-07-18 | BloomFairy(꽃요정) 힐러 + HoT/DoT 공용 시간 지속 효과 시스템 구현 완료 (특수 유닛 5종 중 3번째, 사용자 실기 완료) [상세 원문](History/ImplementationVerificationHistory.md#milestone-fdd518f4092fa904) |
| 2026-07-18 | 이메일 인증 플로우 보정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-9d4c5b354eedeb1f) |
| 2026-07-17 | TorrentSpirit(물의 상급 정령) 파도형 이동 AoE + 힐 서브시스템 구현 완료 (특수 유닛 5종 중 2번째) [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-07-17 | 도끼병(BattleAxe) 휩쓸기형 AoE 구현 완료 (특수 유닛 5종 중 첫 구현, 사용자 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c922bb891dc3ae0e) |
| 2026-07-17 | 매치메이킹 404 수정 [상세 원문](History/ImplementationVerificationHistory.md#milestone-377188e4f82e6688) |
| 2026-07-16 | 로비 프로필/랭킹 클라우드 연동 1차 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2cd83c4f8c4440e1) |
| 2026-07-15 | Android AAB 빌드 용량 최적화 완료 (main 반영) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c52d481ae51e537d) |
| 2026-07-13 | 코드 정리 3건 완료 (실기 통과, main 반영) [상세 원문](History/ImplementationVerificationHistory.md#milestone-0c80ccbc2a885d50) |
| 2026-07-13 | 이동/Walk 애니메이션 동기화 완료 (레벨 동기화 전환, 실기/로그 검증 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-2a8070298cc90271) |
| 2026-07-12 | 전투 타격 타이밍 동기화 완료 (실기/로그 검증 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-c146c8b45575643f) |
| 2026-07-09 | 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 완료 (2026-07-10 실기 PASS) [상세 원문](History/ImplementationVerificationHistory.md#milestone-b976ba1378670d94) |
| 2026-07-08 | 사운드 시스템 실기 버그 3종 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2b7306a3df00fc38) |
| 2026-06-27 | Google 로그인 실기 디버깅 완료 (로그인 성공) [상세 원문](History/ImplementationVerificationHistory.md#milestone-7550a7415e58609a) |
| 2026-06-26 | 방 만들기 취소 후 텍스트 잔류 버그 수정 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-26 | 게임포기 시 로딩 인디케이터 미해제 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-ebd5e656612c50e9) |
| 2026-06-26 | IUnitFactory 인터페이스 도입 완료 (Bootstrap 의존성 제거 리팩토링) [상세 원문](History/ImplementationVerificationHistory.md#milestone-0c52d0adea14090b) |
| 2026-06-25 | 랜덤 매칭 2회차 실패 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-6437b7e96d17018a) |
| 2026-06-25 | RuntimeLogger 유틸리티 생성 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-25 | GameBootstrapper.Setup.cs 하드코딩 배열 파생 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2447e604bf04ba0e) |
| 2026-06-25 | 코드 구조 개선 Phase 2 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2a0d3d4ced2b82a5) |
| 2026-06-23 | document-manager 에이전트 신규 생성 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-23 | 코드 정리(클린업) Phase 1 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1d7b8e656b0aac34) |
| 2026-06-23 | 스플래시 화면 로그인 흐름 개선 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-23 | 로그인 팝업 CloseButton 무반응 수정 완료 — AnonymousWarningPopup / NetworkErrorPopup 두 팝업에 `_closeButton` SerializeField 추가 + Inspector 연결. CloseButton GO가 씬에 활성화 상태로 존재했으나 코드 필드 누락으로 클릭 무반응이던 버그 해결. |
| 2026-06-23 | LoadingIndicator 전수 적용 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | ConfirmPopup z-order 버그 수정 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | BlockingOverlay 씬 전환 버그 수정 완료 — DontDestroyOnLoad 루트 GO 제약. Canvas SO 구조(0/100/200/300) 수립. RuntimeLogWriter [DEBUG-TEMP] 코드 제거. |
| 2026-06-22 | ProfileView 로그아웃 버튼 UI 추가 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | Lobby 탭 패널 CanvasGroup 에디터 사전 부착 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | LoadingIndicator 최소 표시 시간 1초 보장 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | LoadingIndicator 독립 Canvas(300) 추가 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | ConfirmPopup + NetworkErrorPopup 씬 버그 수정 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-22 | BlockingOverlay 씬 전환 후 미표시 버그 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d2698fee035d8188) |
| 2026-06-20 | LogRules.md 작성 완료 — 런타임 로그 + QA-Fix 로그 규칙 통합. 실기기 Logcat 공유 방식 확정. WORKFLOW.md Log.md 섹션 이동. |
| 2026-06-18 | 전역 UI 시스템 도입 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-1d781d78610b2c36) |
| 2026-06-15 | Lobby.unity 규칙 전수 재점검 및 추가 수정 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-24f8e1ed783bcd5e) |
| 2026-06-10 | 전 종족 AI 빌드오더 시나리오 문서 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-10 | AI 시나리오 ScriptableObject 3종족 개편 완료 — 종족별 단일 에셋(3시나리오 내장), Domain 레이어 아키텍처 정리(DifficultyLevel/BuildOrderStep/AIActionType), 종족 분기 로드 구현 |
| 2026-06-10 | 사운드 시스템 코드 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-2e63e82f37cf7085) |
| 2026-06-10 | Firebase-UGS OIDC Bridge 구현 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-08 | 전체 유닛 사망 VFX 적용 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-08 | 멀티플레이 유닛 사망 GO 미파괴 + 이펙트 미재생 버그 수정 (NGO Despawn 패턴 정립) [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9e0b4cd0a5f502f) |
| 2026-06-08 | 유닛 VFX 디테일 개선 3종 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d9d1fcf0f00f7cd0) |
| 2026-06-06 | NetworkGameManager 고아 필드 + Game씬 중복 NGM 제거 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-05 | 자동생산 재등록 슬롯 중복/누락 버그 구조 개선 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-05 | 자동생산 완료 사이클 슬롯2 깜빡임 버그 수정 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-05 | BuildingView Missing Script 정리 완료 — Spirit/Transcendence 건물 프리팹 8개에서 삭제된 BuildingView 컴포넌트 참조 제거. Editor 스크립트 1회 실행으로 일괄 처리. |
| 2026-06-05 | 신규 유닛 프리팹 컴포넌트 부착 에디터 스크립트 완료 — Human 5종·Spirit 6종·Transcendence 5종 × Blue/Red 총 32개 프리팹에 UnitView/AnimationEventRelay/NetworkUnit 등 컴포넌트 자동 부착. Assets/Editor/Setup/SetupNewUnitPrefabs.cs. 실기 테스트 예정. |
| 2026-06-02 | Human CannonTower 초기 방향 설정 완료 — BuildingFactory.GetInitialRotation() 신규. "내 진영 vs 상대 진영" 기준으로 회전 결정(Blue/Red 팀 색깔 기준 아님). ViewConverter.IsFlipped로 로컬 팀 판별 → 상대 포탑 Y180도, 내 포탑 기본값. 실기 테스트 PASS. |
| 2026-06-02 | UnitStatsConfig 미사용 필드 제거 + 스탯 정비 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-06-01 | 방어 타워(AutoTower) 공격 기능 구현 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-31 | 건물 업그레이드 생산 상태 처리 오류 수정 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-30 | 로비 UI 에셋 제작 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-29 | BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-29 | BuildingPlacementUI 씬 계층 재설계 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-121ea22f139df73f) |
| 2026-05-27 | 로비 CanvasGroup Rule 5 전환 실기 테스트 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-26 | 로비 배경 Safe Area 수정 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-25 | 공통 UI 규칙 수립 및 CanvasGroup Rule 5 전환 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-24 | 로그인 시스템 C# 구현 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-9e58370baf7e9556) |
| 2026-05-24 | UGS 401 Unauthorized 버그 수정 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-23 | AuthSystemRules.md 작성 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-19 | 인게임 설정 메뉴 + 게임 포기 기능 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-18 | 건물 스탯 전체 확정 + BuildingStatsConfig 완성 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-18 | ProductionPopup UI 레이아웃 재구성 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-18 | 2/3단계 건물 랠리 마커 미표시 버그 수정 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-18 | 건물 철거 시스템 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-b8d068c65503455b) |
| 2026-05-18 | OnEntityDied 이벤트 분리 리팩토링 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-18~19 | 비생산 건물 공용 액션 패널 UI 완료 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-17 | 건물 생성/파괴 시 유닛 이동 멈춤 수정 완료 — 경로 재계산 시 코루틴을 즉시 재시작하지 않고 _pendingPath 예약 방식으로 교체. 앞 타일이 막힌 경우에만 즉시 재시작. UnitView.cs 단독 수정. |
| 2026-05-17 | 자동 생산 Rule 20 슬롯0 확장 완료 — 슬롯0에서 수동 A 생산 중 A 자동등록 시 슬롯1에 중복 추가 없이 슬롯0을 자동으로 전환(CurrentIsAuto=true). 완료 후 자동 순환 자연 시작. GameSystemRules.md 규칙 20 문구 업데이트. |
| 2026-05-17~18 | 건물 업그레이드 시스템 완성 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-16 | 랠리포인트 깃발 팀별 표시 분리 완료 — 클라이언트 설정 시 호스트에도 깃발이 표시되던 버그 수정. RallyPointChangedEvent에 TeamId 추가, ProductionTicker에 로컬 팀 필터 추가. 싱글플레이 영향 없음. |
| 2026-05-16 | 유닛 생산 실패 피드백 시스템 완료 [상세 원문](History/ImplementationVerificationHistory.md#milestone-d98b855f18ec15ac) |
| 2026-05-16 | 건물 배치 패널 실패 피드백 + UI 개선 완료 — 골드 부족 시 건물 비용 텍스트 빨간색(OnResourceChanged 실시간 갱신) + 토스트 메시지 + 팝업 유지. 비용 텍스트 'G' 접미사 제거(숫자만 표시). ToastUI SetActive 버그 수정(ClearAll/FinishCurrent의 SetActive 제거 → blocksRaycasts+interactable로 대체). |
| 2026-05-15 | 랜덤 매칭 후 캐릭터 잘못 표시 버그 수정 [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-workhistory) |
| 2026-05-15 | 혼잡도 기반 유닛 분산 시스템(v2) 완료 — 모든 유닛이 세로 줄지어 이동하는 현상 개선. 타일별 혼잡도 누적(CongestionMap) + 혼잡도 가중 A*(CongestionAwarePathfinder) 도입. CastleApproachManager(v1) 폐기 삭제. 설정값(DecayInterval/CongestionWeight)은 GameConfig에 통합. 사용자 테스트 PASS. |
| 2026-05-14 | 유닛 회전 시스템 전면 개편 완료 — 방향 계산 Atan2 통일 (FacingDirection.FromCoords 제거), A*/정렬/추격 모든 단계 RotateTowards 적용, _rotationSpeed 단일 Inspector 필드로 통일. MovementLogger.cs 삭제. |
| 2026-04-30 | 종족/팀 초상화 및 생산 연동 정비 — UI 스킨 로직 제거, 종족별 Unit/Building Entries 리스트 기반 동적 바인딩 및 프리팹 생성 일치화 완료. 인스펙터 구조 단순화. |
| 2026-04-29 | 유닛 메시 방향 보정 전면 통일 — 전 유닛(9종) Mesh Y=0, _meshYOffset 코드 제거, 이동 anim offset=0, DirectionAngles={60,120,180,240,300,0} (FlatTop 실제 월드 각도로 재계산) |
| 2026-04-19 | 유닛 생산 패널 전면 재작성 완료 — PendingQueue 단일 큐 구조(QueueSlot), 자동/수동 통합, CancelAutoTypeIfNeeded 헬퍼 추가. 싱글 실기 TC-001~018 전체 PASS |
| 2026-04-19 | 생산 슬롯 깜빡임 버그 수정 — 큐 비어있을 때 자동 등록 시 슬롯1→슬롯0 1프레임 이동 버그. ToggleAutoProduction에서 !CurrentProducing.HasValue이면 즉시 TryStartNext 호출로 수정 |
| 2026-04-18 | 타겟 고정(Target Lock) 데미지 불일치 버그 수정 — 멀티플레이에서 바라보는 타겟(B)이 아닌 가까운 유닛(C)에게 데미지가 적용되던 버그. NetworkCombatController.TickCombat() damageTargetId 지역 변수 분리로 수정. 멀티 실기 PASS |
| 2026-04-13 | Spirit Blue ManaRift 초상화 스프라이트 제작 완료 (초상화 세트 전 종목 완성) |
| 2026-04-13 | 유닛/건물 스탯 확정 적용 완료 (Spirit/Transcendence 6종 HP/ATK/생산시간/비용, Transcendence 건물 HP 종족별 분기) |
| 2026-04-13 | 피격 시 부유 HP 텍스트 완료 (FloatingHpText DOTween, 오브젝트 풀, 줌 스케일링, 멀티 클라이언트 표시) |
| 2026-04-13 | 부유 텍스트 팀별 색상 완료 (Blue=연두, Red=노랑, Inspector 조정 가능) |
| 2026-04-12 | 종족+팀별 건물/유닛 초상화 완료 (BuildingRacePortraitSet 6세트, Inspector 연결) |
| 2026-04-11 | UnitType 개편 + 근접 사거리 시스템 완료 (9종 독립 enum, FindPathToNeighbor, ClaimedTile non-walkable 예외) |
| 2026-04-11 | 근접 공격 거리 다듬기 완료 (MeleeContactDist=0.3f, BuildingDetectionRadius=0.2f, 타겟 타입별 분리) |
| 2026-04-11~12 | 원거리 유닛 공격 중 회전 추적 완료 (Transform 참조, RotateTowards 270°/s, 타겟 고착성, 멀티 실기 MULTI-001~007 PASS) |
| 2026-04-07 | 종족 인게임 적용 완료 (UnitFactory/BuildingFactory 종족별 6세트 분기, 에디터 자동 연결 스크립트) |
| 2026-04-06 | 로비 종족 선택 UI 완료 (RaceId enum 3종족, 캐러셀 방식, RenderTexture 캐릭터 미리보기, DOTween 전환) |
| 2026-04-06 | 종족명 자연→초월 변경 (Nature→Transcendence enum 전체 rename) |
| 2026-04-06 | Pistoleer Idle 애니메이션 버그 수정 (Pistoleer.controller Idle m_Speed 0→1) |
| 2026-04-06 | Android URP RenderTexture 잔상 + RenderPass 에러 수정 (RT antiAliasing 2→1, allowMSAA/allowHDR false) |
| 2026-04-04 | 전투 애니메이션 시스템 재정비 완료 (3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정) |
| 2026-04-04 | 자동생산 BUG-20 수정 (CompleteProduction IsCharged 리셋 누락) |
| 2026-03-27 | 이동 전 회전 선행 완료 (Rotate-then-Move, _isPreRotating 플래그) |
| 2026-03-27 | 공격 타이밍 정밀화 완료 (타격 프레임 데미지, 타겟 고정, 쿨다운 통일) |
| 2026-03-26 | 유닛 NGO NetworkObject 전환 완료 (NetworkTransform 위치 동기화, 클라이언트 예측 제거) |
| 2026-03-24 | Game UI Lifecycle Framework 완료 (IGameUI 인터페이스 + GameUIManager, 멀티플레이 클라이언트 팝업 버그 수정) |
| 2026-03-23 | 자동/수동 생산 하이브리드 시스템 완성 (전역 규칙 5가지, IsCharged 기반 골드 차감, BUG-01~13 수정) |
| 2026-03-20 | 코드 정리 완료 (TeamAssigner.cs 삭제, 주석 정리, IsNetworkMode() 헬퍼 추출) |
| 2026-03-20 | 싱글플레이 ViewConverter 초기화 버그 수정 (Reset() → LocalPlayerTeam 기반 Setup, ApplyConfig() 직후 호출) |
| 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic) |
| 2026-03-19 | UI DOTween 애니메이션 프레임워크 완료 (UIAnimator + AnimatedPanel, SlideFromTop/Bottom, blocksRaycasts 버그 수정) |
| 2026-03-18 | 랜덤매칭 재경기 지원 완료 (커스텀게임과 동일 흐름) |
| 2026-03-18 | 건물 인근 타일 이동/공격 불가 버그 수정 (HexPathfinder goal blocked 체크 제거, Epsilon=0.05f 추가) |
| 2026-03-17 | 전역 로딩 스크린 완료 (LoadingScreen.cs 싱글턴, Lobby 씬 UI, 씬 전환 자동 Hide) |
| 2026-03-17 | 멀티플레이 로비 복귀 버그 수정 (Inspector _lobbySceneName 값 오류 발견, 로컬 독립 처리로 설계 변경, 30초 자동 복귀 타이머) |
| 2026-03-17 | 커스텀게임 재경기 시스템 완료 (양측 동의 재경기, RematchRequestPopup 신규, 레이스 컨디션 처리) |
| 2026-03-16 | 랜덤 매칭 게임 씬 전환 버그 수정 (GetHashCode 크로스-프로세스 비결정성 → GetStableHash polynomial hash 교체) |
| 2026-03-15 | 로비 씬 분리 MVVM 완료 (Lobby/Game 씬 분리, UniRx 기반 MVVM, 탭 4개 + 전투 서브화면 5개, 씬 빌드 완료) |
| 2026-03-15 | 멀티플레이 게임 시작 버그 수정 (Host OnClientConnectedCallback 누락 → HandleClientConnected 추가) |
| 2026-03-14 | 팀별 초상화 동적 업데이트 완료 (ProductionPanelUI/BuildingPlacementUI — Show() 시 팀별 스프라이트 교체) |
| 2026-03-14 | 전투 범위 임계값 수정 (UnitCombatUseCase epsilon 제거 → 타일 중심 간 정확한 거리 기준) |
| 2026-03-14 | 팀별 프리팹 코드 연동 완료 (UnitFactory 팀+타입별, BuildingFactory 팀별 분기) |
| 2026-03-14 | Assault/Sniper 코드 연동 완료 (UnitType enum, UnitStats, UnitProductionStats, ProductionPanelUI 생산 버튼) |
| 2026-03-14 | 공격 애니메이션-타격 시각 동기화 완료 (AnimationEventRelay + Animation Event + scale punch) |
| 2026-03-14 | 유닛 스탯 재조정 (ATK: Pistoleer=6, Assault=1, Sniper=10 / cooldown=클립 길이 기준 DPS 산출) |
| 2026-03-14 | 유닛 메시 방향 보정 완료 (하위 Mesh Y=30°, Root Motion OFF, _meshYOffset=공격 전용) |
| 2026-03-14 | 유닛 회전 DOTween 보간 완료 (이동/공격 모두 DORotate + Ease.OutQuad, _rotationDuration SerializeField) |
| 2026-03-14 | 공격 후 Walk 복귀 버그 수정 (타겟 소멸 시 Play(StateWalk) 명시 호출, 멀티/싱글 공통) |
| 2026-03-13 | 팀별 피아식별 프리팹 에셋 완료 (Castle/Barracks/Pistoleer/Assault/Sniper Blue+Red) |
| 2026-03-13 | 반응형 팝업 UI 완료 (ProductionPopup/BuildingPopup 앵커 기반 배치) |
| 2026-03-07 | 공격 방향 Transform 기반 구현 완료 (UnitView._meshYOffset=30f, Atan2 기반 방향 계산) |
| 2026-03-07 | 유닛별 AttackCooldown 시스템 완료 (UnitData.AttackCooldown/AttackCooldownRemaining) |
| 2026-03-07 | 자동생산 멀티플레이 지원 완료 (ToggleAutoServerRpc + AutoProductionChangedClientRpc) |
| 2026-03-07 | Siege/AI 이동 서버 권위 동기화 완료 (BroadcastServerMove + BroadcastMoveClientRpc, 클라이언트 화면 불일치 수정) |
| 2026-03-02 | 전투 거리 정밀도 버그 수정 (IEntityPositionProvider) |
| 2026-03-02 | GameConfig 정리 (AnimationFps 제거, TileHeight 수정) |
| 2026-03-01 | 생산 큐 클라이언트 UI 즉시 갱신 수정 (OnProductionQueueChanged → SyncQueueStateClientRpc) |
| 2026-02-27~03-01 | 2D→3D 전환 완료 (XZ 좌표계, 55도 카메라, 3D 모델) |
| 2026-02 이전 | 싱글플레이 코어 루프 완성 (헥스, 전투, 건물, 생산, 승패) |
| 2026-02 | 멀티플레이 Phase 1~8 완성 |

## 상세 관측·인계 찾기

| 기록 | 범위 |
|---|---|
| 2026-10-11 wave·Mushroom/Bloom 관측/인계 | [기존 원문](History/ImplementationVerificationHistory.md#work-history-handoff). 전달·native·빌드 시도·시각/원인 OPEN을 각각 구분 |
| 2026-10-09 Assault clock-history 준비 | [당시 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-work-assault-01). 자동 gate는 실제 경기·정밀 시각 수용과 별개 |

현재 작업 상태를 이곳에서 다시 판정하지 않으며, 같은 완료 인계를 다른 주요 문서에 복제하지 않는다.

<!-- 기존 문서 참조의 호환 앵커 -->
<a id="2026-10-09-assault-bounded-clock-history-readiness-추가-기록"></a>
<a id="2026-10-11-00_46-최종-근거-mainprogrammer-인계"></a>
<a id="2026-10-11-20_28-mushroombomberbloomfairy-최종-로그-근거"></a>
<a id="2026-10-11-21_50-wave-contact-표현-예외-격리--구현정적-인계"></a>
<a id="2026-10-11-2222-wave-native-pass와-빌드-시도--이전-대기-supersede"></a>
<a id="2026-10-11-23_16-wave-저장-경기--표현-clock-open"></a>
