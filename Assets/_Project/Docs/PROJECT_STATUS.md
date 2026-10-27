# Hexiege - 프로젝트 진행 현황

**문서 역할**
- **목적·질문:** 시스템이 현재 어디까지 구현·확인됐는지 요약한다.
- **포함:** 구현·검증 범위·알려진 한계와 상세 근거 링크.
- **제외·담당:** 미완 우선순위는 [ROADMAP](ROADMAP.md), 개별 연결·수용은 [Matrix](Assets/UnitCombatAssetMatrix.md), 사양은 [규칙](GameSystemRules.md), 실행 과정은 [이력](History/ImplementationVerificationHistory.md).
- **원본·참조:** 시스템 현재 상태의 요약 원본. 개별 사실의 증거는 연결된 기존 Task/Log·전문 원본을 따른다.
- **갱신:** 실제 구현·검증·수용 축에 변화가 있을 때 해당 상태를 제자리 갱신한다. 날짜별 경기 receipt를 누적하지 않는다.

## 현재 시스템 상태

상태는 구현·검증·사용자 수용 근거의 확인 범위를 기준으로 읽는다. 초기 구현 완료를 최신 전투 전체 수용으로 해석하지 않는다.

| 시스템 | 구현·확인된 범위 | 현재 한계·참조 |
|---|---|---|
| 랠리포인트 | 최초·후속 수정 싱글·멀티 및 AI 5번 싱글 사용자 수용, 코드 정리·별도 검증 후 main 통합·원격 push 완료 | 통합 후 신규 실기·역할별·장치별·정밀 로그 계약 및 전체 경로 차단 대응으로 확대하지 않음. [통합·원격 반영 결과](<_Tasks/2026-10-13/10_37_rally-point-fix/Research.md#9-10-pr-없는-로컬-main-통합-완료-2026-10-18-메인-수행-인계>) |
| 코어 경기 | 헥스 맵·건설/생산·자원·점령·전투·승패 및 싱글/멀티 기본 흐름 구현 기록 | 상세 동작은 게임 규칙, 남은 범위는 ROADMAP |
| 유닛 행동·전투 | 서버 권위·행동/타격 결과·표현 구조 및 기존 개별 교정. 원래 승인 교정의 에셋/코드 반영 감사와 입증된 누락 복원은 종결 | 전체25종·역할교대·Legacy rollback·정밀 시각/구조 이행은 개별 수용과 별도. [Matrix](Assets/UnitCombatAssetMatrix.md) |
| SOLO 이동 복귀 | 사용자 두 경기와 runtime 분석의 focused 수용으로 해당 복귀 범위 종결 | 광산·랠리는 제외. 초월8종 한 경기의 전달/힐 시간 관측을 멀티·전체 시각/global PASS로 확대하지 않음 |
| 특수 공격·힐 | BattleAxe sweep, Torrent 파도, Bloom 힐/HoT, Mushroom 착탄 DoT, Inferno DoT, Quake 즉발 AoE 구현 근거 | 기존 사용자 개별 수용 유지. 새 VFX·정밀 시각·wave clock/Invalid 및 미관측 회복은 Matrix 현행 목록 |
| 서버 권위 투사체 (2026-10-20~27, 전 대상 사용자 테스트 PASS — 정리 P5 대기) | 공통 기반(서버 발사 → 매 프레임 비행 → 착탄 때 피해) 위에 단계 A(`d09cc4a9` StreamSpirit) · B(`639ac1aa` Tank · CannonCart, 의심 1 수정) · B-2(`64b743c8` 사망타 숫자) · C(`b819dc51` 인간 대포 타워 — 포구 발사, Red 반전) · D(`a0a23423` InfernoSpirit · FoxMagician — 차지 중 끊기면 서버 발사 취소) · D-2(`fb77548c` 투사체 사망타의 사망 표현·화상 VFX를 화면 착탄까지 보류, 착탄 파티클 커브 모드 수정) · E(`5b8fb82e` EagleArcher 팀별 화살 tracer · MushroomBomber 옛 스냅샷 발행 끔 · 독 VFX 시작도 화면 착탄까지 보류, 화살 에셋 `be9b6e1a`)가 서버 투사체로 동작한다(스위치 `6,7,12,17,21,24,26;tower:human` — 투사체 유닛 7종 전원). *(2026-10-27 갱신: 종전 스위치 `6,7,12,17,21;tower:human`, 단계 E 없음)* 단계 A~C는 실기 로그 정상(scoped)·사용자 육안 확인·회귀 스위트 PASS(2026-10-21 사용자 Console 확인). 단계 D·D-2는 실기 로그 정상(scoped)·사용자 육안 정상(2026-10-26) → 사용자 테스트 PASS(2026-10-27). 단계 E와 화살 도달 거리 조정까지 `origin/main`에 반영됨(fast-forward `33ce4257`, 2026-10-27). *(2026-10-27 갱신 — 종전 「단계 D-2까지 main에 병합·원격 반영됨(`f367cb52`, 2026-10-26).」)* RuneSpire는 즉발 유지 | scoped — 경기 전체 PASS 아님. 회귀 스위트 PASS는 Console 확인이라 RuntimeLog에 남지 않음. Tank·CannonCart·StreamSpirit의 로그 회귀는 단계 B-2까지(단계 C·D 경기에 미등장). 포구 좌표 직접 로그 없음, 싱글·비행 중 타워 파괴·기기 Host(단계 D) 실기 미관측. 늦은 접속은 실기로 본 적이 없고, 사용자 결정 2026-10-27 「테스트하기 어려우니 넘어가도록 하자」로 실기 테스트를 생략한다(PASS를 막는 사유 아님). 타워 겹사격 헛방은 사용자 결정 Q15대로의 결과(관찰). **단계 D·D-2는 사용자 테스트 PASS(2026-10-27) — 근거: 사용자 육안 정상, 로그 정상(scoped), 회귀 스위트는 사용자 FAIL 언급 없음 → PASS 간주(WORKFLOW 「[6] 사용자 테스트」, 결과 파일 없음), BearGuard 화상 VFX 문제(서버 투사체 Plan U-20)는 별건 [상태 이펙트 Task](_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md) 화상 v2로 해결돼 U-20 종결.** *(2026-10-27 정정 — 사용자 결정 「인페르노·여우 작업 완료한 거 아니야? 뭐 더 확인해야 해?」. 직전 표기 「단계 D·D-2는 사용자 테스트 PASS로 기록하지 않았다 — … U-20 행은 … 아직 「결정 대기」로 남아 있어 종결 확인이 필요하다」, 「늦은 접속 … 실기 미관측」)* *(2026-10-27 갱신: 종전 「단계 D·D-2는 사용자 테스트 PASS 아님 — 회귀 스위트 결과 미보고, BearGuard 화상 VFX가 잘 안 보임(크기 수정안 사용자 결정 대기, 원인은 화면 미확인 가설).」)* 규칙 `NET-TIME-002` 문장 정리(사망 표현·화상 VFX 맞춤, 「HP바」 추후 추가 예정 표기, 서버 투사체 호스트·싱글 지연 0초)는 사용자 결정으로 반영됨(2026-10-26 — 현재 동작에 맞춤, 코드 무변경). *(2026-10-26 갱신: 종전 「… 개정안 사용자 승인 대기」)* **단계 E는 사용자 테스트 PASS(2026-10-27) — 근거: 사용자 육안 「잘 작업된 것으로 보여」, 실기 로그 정상(scoped, 에디터 Host · 기기 Client 1경기), 회귀 스위트는 1차 FAIL(화상 v2 프리팹의 검사 재현 한계 — 게임 코드 무관) → `a5aa781d` 검사 코드 수정 → 재실행 오류 없음.** 화살이 대상에 약간 못 미쳐 사라지던 도달 거리 후속은 화살 프리팹 자식 위치 조정(수정안 (c) `33ce4257` — 데이터만, 피해 시각 불변) 뒤 실기 로그 정상(scoped)·사용자 「잘 수정된 것 같고」 → 사용자 테스트 PASS(2026-10-27). 남은 일: 스위치 · 주석 블록 정리 P5(사용자 지시 대기), 진단 중 드러난 후속 후보 R-A1~R-A3(기록만 — Plan 10-11). *(2026-10-27 갱신 — 종전 「남은 일: 화살이 대상에 약간 못 미쳐 사라지는 도달 거리 후속(진행 중), 스위치 · 주석 블록 정리 P5(사용자 지시 대기).」)* 단계 E 싱글 · 기기 Host 실기는 전달되지 않음. *(2026-10-27 갱신 — 종전 「남은 대상 EagleArcher · MushroomBomber.」, 그 전 2026-10-26 「단계 D 구현 진행 중 — 미완료(2026-10-21)」)* 속도·반경은 서버 설정 `ProjectileConfig.asset`(Plan의 `UnitStatsConfig`와 다른 자리). 규칙 문서의 개정 표시는 유닛별 구현 상태로 갱신됨(2026-10-21). 인간 타워 전방 120° 제한은 기록만(ROADMAP). [Research 15절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#15-stage-c-human-cannon-tower-check-20261021) · [16절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#16-stage-d-charge-unit-check-20261026) · [17절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#17-stage-d2-death-hold-check-20261026) · [19절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-stage-e-field-check-20261027) · [19-10 화살 도달 거리](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-10-arrow-reach-followup) |
| VFX 표현 후속 (브랜치 `claude/vfx-code-followups`, 2026-10-13~19) | 사용자 실기 3회차까지: BloomFairy 힐 PS 4개 재생, 차지 VFX(FoxMagician·InfernoSpirit) 시전자 사망 시 정지, RuneSpire 번개 크리스털 시작·살아 있는 대상 가슴 높이 조준, 측정 도구 스케일 버그 수정 뒤 화상·독·힐 크기·높이의 프리셋 조정, 버섯 착탄·새 독 프리팹 승인. QuakeSpirit 공격 VFX 재제작(6갈래 균열·흙먼지, 기존 공격 슬롯) 사용자 승인 2026-10-19 — 보이는 범위가 피해 반경 1.0보다 넓은 것은 사용자 선택([Research 13절](_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md#13-quakespirit-attack-vfx-rework)). 차지 유닛(InfernoSpirit·FoxMagician) 차지→발사 시점 분리(차지 = `OnAttackCharge` 차지 VFX만, 발사 = `OnAttackHit` 투사체+발사음, 취소 시 차지 즉시 정지)·Fox 마법탄 tracer/impact 연결·Inferno 불티 재질 교체 — 싱글·멀티 사용자 시각 확인 2026-10-19([별건 Task 9절](_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-implementation-result)). 서버 피해·타겟·HP·RPC 불변(표현 전용) | ⚠️ 완료 아님: 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋남(OPEN, 원인 미검증) · 상태 이펙트 값 미세 조정은 사용자 데이터 작업 진행 중(현재 값은 커밋 `91371e55`: 재측정 표 + Heal·Poison Scale Multiplier 2) · 주석 비활성화한 옛 로직 6곳은 번개 사망 시 결함이 테스트를 통과한 뒤에만 최종 삭제(2026-10-15 사용자 결정). *(2026-10-15 정정: 첫 판의 「재측정 표·프리셋 값 미커밋」은 `91371e55`로 해소)* · 차지 유닛: 로그 기반 검증 없음·회귀 스위트 결과 미보고, Fox 피해 숫자가 투사체 도착보다 먼저 뜸·차지 중 취소 시 숫자만 뜰 수 있음(둘 다 C 방식에서 다룸) *(2026-10-26: 서버 투사체 단계 D `a0a23423`에서 Fox 즉시 표시 분기를 비활성화했고(RM-5), 단계 D 기기 로그에서 Fox 피해 숫자 `foxPrimary` projectileKeyed 126 · immediate 0 — Fox 숫자 선행은 재현되지 않음([Research 16-4](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#16-4-conclusion)). 차지 중 취소는 서버가 발사 자체를 취소해 피해가 없지만(Research 16-7 수지), 「취소 뒤 숫자만」 장면을 직접 센 로그 항목은 없음. D-2 Research 17절에는 Fox 숫자 항목이 따로 없음)*, Client Inferno 차지 간헐 누락 가능(추정) — [Research 9-4](_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-4-알려진-한계-유지). · StreamSpirit 물폭탄·Tank/CannonCart 포탄 tracer·impact는 2026-10-19 제작·연결됐고(R5 임시 도착 착탄 — [Research 15절](_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md#15-r5-interim-impact-hookup)), 이 세 유닛은 지금 서버 권위 투사체 경로(위 행)로 재생된다. 참조 0건 VFX 에셋 11개는 2026-10-19 삭제(`344dfa42`). *(2026-10-21 추가)* 단일 소스: [Research 10절](_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) · [Plan 13절](_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md) |
| 상태 이펙트 화상 v2·튐 수정 (2026-10-26~27, 같은 브랜치) | 화상 이펙트를 사용자가 다시 만든 v2(몸 둘레 작은 불꽃 다수, `vfx_infernospirit_burn_v2.prefab`)로 교체(`5dd22db7`). 그 전 단계의 몸통 폭 맞춤(`eb660f70`)·카메라 쪽 당김(`7f787d21`)은 프리셋 옵션으로 남기고 화상에서 꺼 둠. 뒤집힌 Client에서 화상·독이 유닛 회전 중 옆으로 튀던 문제를 따라가기 직전 보이는 몸 위치 바로잡기로 수정(`ec621589`). 04:15 멀티 실기 사용자 「특별히 문제 발견되지 않음」·로그 정상(scoped) → **사용자 테스트 PASS(2026-10-27, 회귀 스위트는 무언급 PASS 간주)**, `origin/main` 병합 `0a47f37f`. 서버 판정·피해·DoT·RPC 불변(표현 전용) | 사용자 「세 이펙트 모두 눈에 띄는 느낌은 아님, 지금 상태로도 사용 가능」 → 가시성 강화는 ROADMAP 후속. 병합 뒤 Unity 컴파일 미확인. 힐 presenter는 같은 우회 패턴이 남아 있으나 튐 보고 없음. 옛 화상 프리팹은 롤백용 보존(참조 0). [Research 13절](_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md#13-튐-수정-뒤-멀티-실기-확인과-작업-완료-2026-10-27-0415-로그) |
| 무작위 맵 | 생성·검증·폴백·전송·해시·재경기 및 최초 멀티 실패 UI의 구현/관측 근거. 매 경기 새 맵 | InitialGold 실제 적용·조각 크기 실측·차단 대응 및 자연 실패/복구 관측은 별도. [맵 규칙](GameSystemRules/GameSystemRules_RandomMap.md) |
| 건물·스킬·연구 | 생산/업그레이드/철거와 싱글·기존 실패 피드백, 스킬 타입A/B/C, 연구 트랙·×10 스케일·연구 패널의 구현/기존 실기 근거 | 멀티 BuildFailed/EnqueueFailedClientRpc의 UI 피드백은 후속. 새 스킬 목록·밸런스·연구 UI/AI 연구 실기 등 보류는 별도. 값은 [Stats](StatsReference.md) |
| MistShrine | 아군 유닛·건물 대상 물안개 힐 구현, 기존 에디터 싱글 확인 | 멀티 미검증·밸런싱 미확정. 현재 임시값을 확정값으로 인용하지 않음 |
| AI | 난이도·빌드오더/반응·생산/배치와 핵심 흐름 구현·관측 근거 | 반응 트리거/무작위 시나리오 세부 검증과 밸런싱+3시나리오 재논의는 별개 후속 |
| UI·로비 | lifecycle·DOTween·전역 로딩·SafeArea/CanvasGroup·건물 패널·설정/음소거·종족/초상화 및 경기 흐름 화면 구현. 결과 화면 공용 배경·입력 차단/복원은 6항목 사용자 정상 확인으로 수용하고 대체 주석 정리·별도 검사 완료 | 에이전트 native 실행·정밀 로그/장치별 측정과 구분. [확인 범위](<_Tasks/2026-10-19/04_39_game-end-modal-input/Research.md#7-사용자-6항목-정상-확인수용과-후속-정리>). 기존 레이아웃·CanvasGroup 예외 결정은 ROADMAP |
| 인증·프로필 | Login/Google/이메일 흐름의 기존 확인을 유지. 로비/인게임 표시 전용 프로필과 엄격 조회 기반 게스트 최초 닉네임 연결 구현. 프로필 표시/레이아웃과 게스트 재실행 닉네임 유지 사용자 확인 | 명시logout새guest새이름은 미보고이며 UI완료 필수관문 아님. 서버 승패 기록·IAP/Functions 수용으로 확대하지 않음. [Auth](AuthSystemRules.md)·[수용 범위](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023) |
| UI 레이아웃 후속 | 입력/폰트·프로필·Development페이징·연구 제목/철거/생산형진행/취소와 최종10행 높이 교정 구현 완료. 프로필/재실행닉/철거/취소/실기페이지에 이어 최종 랭킹 높이·배치도 사용자 정상 확인 완료 | UI39파일 de2a424b와 통합기록69b5e5c4의 로컬main FF 완료·원격push 없음. Build And Run 완료/설치/실행의 에이전트 직접 관측이나 전체기기·장문·SafeArea 증명으로 확대하지 않음. [완료 구분](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023) |
| 익명 로그아웃·매칭 교정 | 명시적 익명 로그아웃 자격 정리와 매칭 회차 복구 구현. 2026-10-21 정상 진입・상대 단절 시 Host 30초 복구・복구 후 재매칭 사용자 수용 완료 | 게스트 자동복원 로그와 이후 닉네임 재실행 유지 사용자 확인은 분리해 수용. 원 HTTP429/전적 상세 보존/실계정 진행도/익명 이전 진행도/취소경쟁·역할교환은 별도이며 UI·매칭 수용의 반복 관문 아님. [매칭 근거](_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md#13-랜덤-매칭-세-단계-사용자-수용-완료--2026-10-21)·[UI 확인](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023) |
| 사운드 | AudioManager·BGM/SFX·볼륨/음소거 연결의 구현 기록, 기존 볼륨/음소거 UI 실기 확인 | BGM/SFX Inspector별 배선·제작/실기·수용 잔여는 에셋 목록. Victory/Defeat 분리는 Sound 규칙의 선행조건이 있는 추후 기획 |
| 로그 | GameLog/FileSink·에디터 파일·Device 캡처·수동 정리와 기존 이관/실기 근거. 로그 보관 ZIP 생성 에디터 도구(`Hexiege > Logs > Create Log Archive ZIP`, 업로드는 사용자) — 사용자 실행으로 ZIP 생성·메인 세션 다운로드 대조 OK(Task Plan §8의 3회, 이후 서버 투사체 단계 B-2·C 로그 보관에도 같은 도구 manifest 사용 — 2026-10-21) | 현행 규범은 [LogRules](LogRules.md). ZIP 도구의 KST 날짜 기준 변경(`fd4c04cb`)은 실행 확인 기록 없음([Task Plan §8](_Tasks/2026-10-20/12_02_log-archive-zip-tool/Plan.md)). Lobby 직접 진입은 사용자 범위 제외. 자연 엔진 오류 훅 발화의 미확인과 FileSink 경로 중복 정리는 ROADMAP |
| 에셋·플랫폼 | 팀별 표현·3D 전환/자산 적용 및 AAB 최적화의 기록 | 보유/제작/연결은 에셋 목록. 과거 용량은 당시 빌드 값이며 현재 크기 보증이 아님 |
| 개발 도구 | 필수 Editor7소스/17메뉴 보존, 불필요28소스/메타 최종 삭제·main 반영. Fox fixture 비교 교정 후 전체 회귀 suite 사용자 PASS 수용 | 에이전트 native 실행·기기 플레이/전체 공격 시각·HP 수용과 구분. [회귀 수용 근거](<_Tasks/2026-10-19/15_21_regression-suite-contract-fixes/Research.md#6-사용자-전체-suite-pass-수용과-최종-정리>). 사용법은 WORKFLOW/TDD/LogRules, 문서 검사 범위는 WORKFLOW [11] |

## 현재 검증과 보류 경계

- RhinoBreaker·QuakeSpirit·BattleAxe·DustSpirit·LittleKnight·EagleArcher의 기존 사용자 수용을 유지한다. MigrationRequired·과거 미주입/대기 도장 때문에 자동 재시험하지 않는다.
- Assault/Sniper/Inferno의 focused 수용, wave의 한 경기 전달/표시 관측과 원인/정밀 시각·자연 pending 회복·전체 migration은 다른 축이다. 실제 잔여는 [Matrix 현행 목록](Assets/UnitCombatAssetMatrix.md)에서 한 번만 관리한다.
- 구현·정적 검사·native 메뉴·기기/멀티·사용자 수용·빌드 시작/완료는 서로 다른 확인 범위다.
- 밸런싱+AI 시나리오는 추후 별건이며 추가 기획은 낮은 우선으로 보류한다. 마법 타워·상태머신 확장·서든데스·튜토리얼·카드/IAP 등 미완 기획을 구현 완료로 옮기지 않는다.

## 상세 원본과 작업 목록

| 확인할 내용 | 담당 원본 |
|---|---|
| 현재 값·단위·Eagle 생산 클립 선택 경계 | [StatsReference](StatsReference.md) |
| 아키텍처·기술 스택·계층/구현 패턴 | [TDD](TechnicalDesignDocument.md)·[네트워크 TDD](TechnicalDesignDocument_Network.md) |
| 보유 에셋·제작/연결·개별 수용 | [AssetList](Assets/AssetList.md)·[VFXSFXList](Assets/VFXSFXList.md)·[Matrix](Assets/UnitCombatAssetMatrix.md) |
| 실제 미완·우선순위·선행/미결 | [ROADMAP](ROADMAP.md) |
| 완료 시점·과거 경위와 검증 영수증 | [WORK_HISTORY](WORK_HISTORY.md)·[구현 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-status) |

<!-- 기존 문서 참조의 호환 앵커 -->
<a id="--후속-이메일-인증-플로우-보정은-2026-07-18-완료됨-인증-대기-화면-이메일-표시-가입-취소계정-삭제-정책-앱-재실행-자동-로그인-게이트-닉네임-미설정-lobby-우회-차단을-반영했다"></a>
<a id="-p0-재오픈--서버-권위-유닛-actionsequence"></a>
<a id="-무작위-맵--본체-구현검증-완료-적용-후속-남음"></a>
<a id="-미구현-기능"></a>
<a id="-알려진-미완성버그-항목"></a>
<a id="-완료된-시스템"></a>
<a id="2026-07-16-추가-완료-로비-프로필랭킹-클라우드-연동"></a>
<a id="2026-07-18-완료-이메일-인증-플로우-보정"></a>
<a id="2026-10-09-assault-bounded-clock-history-readiness-추가-기록"></a>
<a id="3d-모델-meshyai"></a>
<a id="3d-전환-2026-02-27--2026-03-01"></a>
<a id="bloomfairy꽃요정-힐러--hotdot-공용-시스템-2026-07-18--특수-유닛-5종-중-3번째"></a>
<a id="buildingactionpanelui-씬-계층-재설계--런타임-슬롯-제어-2026-05-29"></a>
<a id="buildingplacementui-씬-계층-재설계-2026-05-29"></a>
<a id="flattop-1121-무작위-대전-맵-2026-07-19-기획-확정"></a>
<a id="game-ui-lifecycle-framework-2026-03-24"></a>
<a id="gameconfig-코드-기본값-vs-inspector-값"></a>
<a id="google-로그인-실기-디버깅-2026-06-2728"></a>
<a id="infernospirit지옥불-정령-단일-대상-dot-2026-07-20--특수-유닛-5종-중-5번째"></a>
<a id="lobby-ui-전체-규칙-준수-수정-2026-05-30"></a>
<a id="lobby-씬-ui-규칙-재점검-및-추가-수정-2026-06-15"></a>
<a id="lobby-패널-canvasgroup-사전-부착--profileview-로그아웃-버튼-2026-06-22"></a>
<a id="mistshrine-물안개-힐-시스템--구현-완료--에디터-싱글플레이-실기-검증-완료-2026-08-12--멀티-미검증"></a>
<a id="mushroombomber버섯폭격기-착탄형-범위-dot-2026-07-19--특수-유닛-5종-중-4번째"></a>
<a id="productionpopup-ui-레이아웃-재구성-2026-05-18"></a>
<a id="quakespirit대지의-정령-착탄형-즉발-aoe-2026-07-20--특수-유닛-5종-전량-완료"></a>
<a id="ui-dotween-애니메이션-프레임워크-2026-03-19"></a>
<a id="unittype-개편--근접-사거리-시스템-2026-04-1011"></a>
<a id="개발-도구--에이전트-인프라"></a>
<a id="건물-배치-패널-실패-피드백--ui-개선-2026-05-16"></a>
<a id="건물-업그레이드-시스템-2026-05-1718"></a>
<a id="건물-철거-시스템-2026-05-18"></a>
<a id="게임-화면-ui-tc-전체-실기기-테스트-2026-05-27"></a>
<a id="게임포기-로딩-인디케이터-미해제-버그-수정-2026-06-26"></a>
<a id="경기-전-원래-교정-반영-감사-종결-2026-10-15"></a>
<a id="공통-ui-규칙-수립-및-canvas-scaler--safearea-적용-2026-05-25"></a>
<a id="근접유닛-뒷무빙-5차-개선-2026-04-26"></a>
<a id="근접유닛-적-감지-사거리--추적-회전-개선-2026-04-1924"></a>
<a id="기술-스택-현황"></a>
<a id="도끼병battleaxe-휩쓸기형-aoe--특수-공격-아키텍처-2026-07-17"></a>
<a id="랜덤-매칭-2회차-실패-버그-수정--runtimelogger-2026-06-25"></a>
<a id="로그-체계gamelog-전환--미완-항목-2026-08-17-기준"></a>
<a id="로그인-시스템-c-구현-2026-05-24"></a>
<a id="로비-종족-선택-ui-2026-04-0406"></a>
<a id="매치메이킹-404-수정--호스트-결정-lobby-createorjoin-전환-2026-07-17"></a>
<a id="멀티플레이-phase-18"></a>
<a id="멀티플레이-기능-미구현"></a>
<a id="멀티플레이-로비-복귀-버그-수정-2026-03-17"></a>
<a id="미제작-에셋"></a>
<a id="버그-수정-및-폴리싱"></a>
<a id="비생산-건물-공용-액션-패널-ui-2026-05-1819"></a>
<a id="사용자-싱글-경기-관측-2026-10-15"></a>
<a id="설계-규칙-문서-gamesystemrulesmd"></a>
<a id="스킬-건물-시스템-phase-1-타입-ab--조준ui좌표화렌더링버그수정-phase-2-타입-c-전역-상태변경--구현-완료--실기기-테스트-pass-phase-1-2026-08-04-phase-2-2026-08-05-실기멀티클라-pass"></a>
<a id="싱글플레이-ai-시스템-2026-06-07"></a>
<a id="아키텍처-현황"></a>
<a id="에셋-현황"></a>
<a id="연구소-유닛-강화-시스템--전투-스탯-10-스케일--연구-패널-ui--구현-완료--멀티플레이-실기-pass-2026-07-31"></a>
<a id="원거리-유닛-공격-중-회전-추적-2026-04-1112"></a>
<a id="유닛-생산-실패-피드백-시스템-2026-05-16"></a>
<a id="유닛-이동전투-시스템-재설계-2026-05-1113"></a>
<a id="유닛건물-스탯-scriptableobject-전환-2026-04-25"></a>
<a id="유닛건물-스탯-확정-적용-2026-04-1213"></a>
<a id="인게임-설정-메뉴--게임-포기-기능-2026-05-1819"></a>
<a id="인게임로비-볼륨음소거프로필-버튼-ui-로직-연결-2026-07-09-실기-pass"></a>
<a id="인증-시스템-설계-규칙-문서-authsystemrulesmd"></a>
<a id="전역-로딩-스크린-2026-03-17"></a>
<a id="전체-구현-현황"></a>
<a id="종족-인게임-적용-2026-04-07"></a>
<a id="커스텀게임-재경기-시스템-2026-03-17"></a>
<a id="코드-리팩토링-2026-05-18"></a>
<a id="코드-리팩토링-전체-완료-2026-05-24"></a>
<a id="코드-정리-3건--죽은-코드-제거--animator-상태-의존-제거--firebase-게이트-제거-2026-07-13"></a>
<a id="코어-게임플레이"></a>
<a id="타일"></a>
<a id="타일-소유권-실시간-감지--tileownershipservice-2026-04-26"></a>
<a id="팀별-관점-viewconverter"></a>
<a id="팀별-피아식별--신규-유닛-에셋-2026-03-13"></a>
<a id="플랫폼--빌드-최적화-2026-07-15"></a>
<a id="피격-시-부유-hp-텍스트--world-space-2026-04-1213-2026-04-17-world-space-전환"></a>
<a id="현행-진행-경계-2026-10-13-문서-정리"></a>
