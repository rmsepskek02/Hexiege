# LogRules 현행 규범·이력 분리 조사

로그 작성 규칙에서 현행 의무·예외와 필요한 근거를 쉽게 찾도록 정리하고, 과거 개정·구현·검증 이력은 별도 문서에 원문 그대로 보존한다. 원시 테스트 로그의 저장 방식은 검증 가능한 월별 Release ZIP 보관으로 정합화하되, 문서의 절차 정의와 실제 업로드·추적 파일 변경·삭제 권한은 구분한다.

## 2026-10-12 12:44:46 main 사용자 self-revert 확인 — 병합 blocker 해제

**첫 commit 전 현재 상태:** Main fresh fetch로 최신 main9e1c596d 및 6a2f2403 대비 tree diff0 확인. Resources 리버트 처리됨·병합 보류 해제·사용자 병합 GO 수신. **실제 source commit/push/merge는 아직 미실행**이며 완료 SHA를 선기록하지 않는다. 이번 사전 반영은 현재 Task2문서만 최소 수정한다. A/B 실제 commit·merge 결과 후 별도 짧은 Task 완료 영수증과 관련 상태1줄을 반영하고 second final commit할 예정이며, 현재 그 단계는 대기다.

Main의 직접 fetch/읽기전용 확인 인계: 사용자 self-revert main **`9e1c596d`(12:44:46)** 확인. 해당 main tree는 `6a2f2403` 대비 **diff0**, 원래 `Assets/_Project/Resources` 위치의88 항목 복원·이동 위치 잔여0·Config3 존재를 확인했다. 이전 `0192443d` Resources 이동 문제에 따른 의도 확인/병합 보류는 이 결과로 **해제**한다. 문서 담당이 Git/소스/asset을 새로 조사하거나 revert한 결과가 아니다.

사용자가 이제 **commit/push/merge 명시적 GO**를 전달했다. 승인과 실제 완료는 구분하며 **source branch commit/push 및 main merge/최종 반영은 현재 결과 대기**다. Main 분기 병합·검증 인계 이후 actual SHA와 실행 결과를 별도 추가한다. Revert의 tree 동일성만으로 branch 병합·새 compile/native/runtime PASS를 선언하지 않는다.

Private archive169 entry 원격 전체 검증·remote receipt 업로드·tracked raw123→0·local169 보존은 완료한 별도 축으로 유지한다. 기존 이름/label/hash/asset ID·검증 시각·보호 hash 인계와 이전 차단 영수증을 삭제하거나 덮어쓰지 않는다. 아래 이전 “Resources 의도 미확인/main merge 보류” 문장은 당시 이력이며 이 최신 blocker 해제로 supersede한다.

이번 직접 수정은 **현재 logarchive Task Research/Plan + 기존 관련 상태3항목**, 총5파일뿐이다. LogRules/WORKFLOW/AGENTS·다른 과거 Task·메모리·baseline·소스/asset은 추가 수정하지 않는다. Git 명령/TCQA/Unity/빌드 실행 없음. 이번 문서 검사 결과는 실제 종료 확인 뒤 추가한다.

최종 실제 UTF-8 check_docs20890 **exit0·문제0**, _Tasks/_Logs 제외 범위 확인 및 별도 Task2문서 파일 링크0. 이번 수정 파일은 이 Research.md/Plan.md와 `Assets/_Project/Docs/PROJECT_STATUS.md`, `Assets/_Project/Docs/ROADMAP.md`, `Assets/_Project/Docs/WORK_HISTORY.md`다. Commit/push/merge actual SHA와 병합 후 검증 결과는 Main 후속 인계 전까지 대기한다.


## 2026-10-12 운영 D 승인·외부 인증 차단 영수증

## 2026-10-12 사용자 최종 승인 — private 로그 전용 보관

## 2026-10-12 private 업로드 성공 — 파일명 mapping / download 검증 대기

## 2026-10-12 최종 원격 검증·index 추적 해제 인계 — main 반영 대기

최종 문서 인계: 실제 UTF-8 check_docs19573 **exit0·문제0**. 규칙10/Markdown78/Claude40 및 _Tasks/_Logs 제외 범위 확인; LogRules·현재 Task2 별도 파일 링크0. Baseline tool 갱신 exit0, document-manager1217→1221행(+4), 파일2→2·감소 없음. 현재 private archive/verification receipt/index123→0/local169 보존 완료만 인정하며 **source branch commit/push는 대기, known Resources move의 사용자 의도 확인 전 main commit/merge는 보류**다. Main 최신 독립 재확인 보호 표기 Game6BB…/Stats0A907…/Wave493F… unchanged는 인계 그대로 기록하며 축약값을 전체 hash/다른 파일 경로로 추정하지 않는다. 문서 담당의 source/runtime 재검사나 Resources 원인 분석·경로 수정 없음. 전체 수정10파일은 Research의 파일명 manifest에 기록했다.

### 전체 수정 파일명 manifest — 최종 문서 인계

이번 private 보관 durable 정책부터 최종 운영 영수증까지 문서 담당이 수정한 **전체10파일**을 아래에 기록한다. 마지막 receipt 단계에서 수정한 것은 현재 Task2 + 상태3파일이며, 규칙/메모리의 앞선 최소 변경까지 합친 목록이다. Baseline은 검사기만 갱신했고 직접 편집하지 않았다. 다른 메모리 정리·상태 재설계·old Task·소스 수정은 없다.

| 번호 | 전체 경로 | 변경 주체/범위 |
|---|---|---|
| 1 | `Assets/_Project/Docs/LogRules.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 2 | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Research.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 3 | `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 4 | `Assets/_Project/Docs/PROJECT_STATUS.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 5 | `Assets/_Project/Docs/ROADMAP.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 6 | `Assets/_Project/Docs/WORK_HISTORY.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 7 | `.claude/MEMORY.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 8 | `.claude/agent-memory/document-manager/MEMORY.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 9 | `.claude/agent-memory/document-manager/doc-conventions.md` | 문서 patch / 최소 영수증·규칙·상태·메모리 |
| 10 | `.claude/agent-memory/_baseline.json` | 검사기 --update-baseline 생성(+4행, 감소 없음) |

별도 Main/programmer 운영 변경: `Tools/archive_runtime_logs.py` 구현·로컬검증, `.gitignore` 승인 RuntimeLog 제외6패턴, 승인 raw txt의 index 추적 해제123개. 문서 담당이 해당 코드/Git 파일을 수정하거나 실행하지 않았다. 169개 원본 전체 경로는 이 Research의 기존 모집단 TSV에 남아 있으며 이는 staged123 목록과 동일하다는 뜻이 아니다. 정확 staged subset을 문서 담당이 Git으로 새 탐색하지 않았다. Raw.meta/CrossAudit/doc·folder meta와 로컬169 원본은 보존한다.

현재 source branch commit/push·main merge/반영은 **미완료/의도 확인 대기**다. Receipt 업로드 완료와 archive/index 검증 완료를 이 단계 완료로 확대하지 않는다. WORKFLOW/AGENTS/LogHistory/게임 규칙·wave Task는 이번 private 운영 최종 인계에서 변경하지 않았다.

최종 실제 UTF-8 check_docs와 Task 파일 링크 검사 결과는 종료 확인 후 아래 추가한다.


**후속 최종 완료 인계:** 별도 remote verification receipt도 private Release에 **업로드 완료**. Asset ID **632550261**, **1,332 bytes**, SHA256 `734b7d948884ca48da71b5d79ff6554b3013fad0f8e6d20ec3e8e4adab46f72d`. 실제 receipt 파일명은 이번 인계에 없으므로 추정하지 않는다. 아래 “별도 receipt 업로드 진행 중/이름·ID 미인계”는 이전 단계이며 이 완료 인계로 supersede한다. Private archive 원격 전체 검증·raw index123→0·local169 보존은 완료지만 **source branch commit/push 및 main merge 완료는 아니다**.

**최신 main 통합 보류:** Main은 origin/main0192443d의 오늘12:21 commit이 `Assets/_Project/Resources` → `Assets/_Project/Prefabs/VFX/Units/Resources` **R100 rename88만**, 전 bytes 일치임을 확인했다. 현 production validator `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs:1379` 및 `:6711`은 이전 Resource 경로라는 Main 확인이다. 문서 담당은 소스/Git을 재검사하거나 경로를 수정하지 않았다. Main은 의도 확인을 async 질문하고 **main merge를 중단·보류**했다. 사용자 승인 아래 지금까지의 source branch 전체 작업을 commit/push로 먼저 안전 보존할 예정이며 아직 실행 완료 인계는 없다. Archive 문서 영수증 완료를 기다리는 단계로 기록한다.


Main actual gate 완료: 지정 private `rmsepskek02/Hexiege-Logs`의 Release410115016/tag `logs-2026-10`에서 ZIP·manifest 실제 download를 완료했다. 안정적인 Release URL은 `https://github.com/rmsepskek02/Hexiege-Logs/releases/tag/logs-2026-10`다. Anonymous repository GET404 확인은 Main 인계이며 문서 담당이 API를 호출하지 않았다.

| 검증 항목 | 실제 최종 인계 |
|---|---|
| ZIP | asset632539446 / `2026-10-12_legacy-logs.zip` / 120,495,020 bytes / SHA256 `74a689e76c43524a941f84f36145066e9ca239a1cceceadbfc6ca3337b5c26d2` |
| manifest | asset632540847 / `2026-10-12_legacy-logs.manifest.json` / 65,377 bytes / SHA256 `aae378ee4ca1688e58e6097d07e059c452ea6a791ae42c5ed3b296d2533c293e` |
| 실제 Python full verification | 169 entry / 2,136,923,327 bytes; path set·CRC·size·SHA256·sourceRecheck 모두true, exit0 |
| verified_at | UTC `2026-10-12T03:30:44.641813+00:00` / KST2026-10-12 12:30:44 |
| 이름/label | 로컬 `2026-10-12_기존로그.zip` ↔ remote `2026-10-12_legacy-logs.zip`, ZIP 한글 label 유지; 이름 변경 후 bytes/hash 불변 |
| 별도 원격 검증 receipt | private Release 추가 업로드 **진행 중**. 파일명·asset ID·완료 결과 아직 미인계 |
| 초기 manifest 상태 | `remote NOT VERIFIED`는 캡처 당시 이력. 초기 artifact를 덮어써 verified로 고치지 않고 별도 최종 verification receipt로 연결 |

### 승인된 좁은 index 추적 해제 — Main 실제 실행

원격 전체 검증 뒤 Main이 manifest의 정확169 경로를 literal pathspec으로 batch20 처리했다. **Git tracked raw123→0**, local raw169 그대로 보존. Staged 삭제123은 전부 승인 RuntimeLog txt 본문이며 other deletion/history rewrite 없다. 문서 담당이 Git이나 로컬 raw 삭제를 실행한 결과가 아니다.

Coder .gitignore 실제 검증은 RuntimeLog169 match / nonraw1933 보존 / negative6 PASS. 실제 인계 SHA256 `a6b5a4fa15b6fdd94e6eb045444ba8dc573424e66e9ca9b1a7d03c7d83c5cdc9`. Raw txt.meta, CrossAuditResult.txt 및 실제 코드/asset/doc/folder metadata는 제외/삭제 범위를 넓히지 않는다. 이전 tracked123·최종 main raw 조건은 서로 다른 단계이며 현재 index0을 원격 main 반영 완료로 승격하지 않는다.

### 최신 Git 전달 경계 — Main 후속 대기

Main 최신 fetch: origin/main `0192443d`에12:21:31 작업1commit 추가, HEAD `ad34bf93` 대비 main1/branch25 diverged. 이전 remote6a2f2403 ancestor 인계는 이전 snapshot이다. **Dirty 전체 작업의 안전 보존 commit → origin/main 내용을 읽고 merge → 필요한 compile/gate 판단**은 Main이 이어 진행할 예정이며 아직 완료가 아니다. 이번 문서도 commit 전이다. Public/source main 최종 반영·commit/push/merge 완료를 기록하지 않는다.

Current source compile320/errors0 및 source unchanged는 이전 Main 준비 인계이지 새 merge 후 검사나 runtime 검증이 아니다. 문서 담당은 source/Git/API/Tool/Unity/빌드/로그 삭제를 실행하거나 재관찰하지 않았다.

### 최소 문서 후속

이번 최종 receipt에서 LogRules의 filename mapping 보강, 현재 Task Research/Plan, 중요 상태3문서·공용/역할 메모리 최소 포인터만 갱신한다. WORKFLOW/AGENTS는 기존 단일 LogRules 참조로 충분하여 추가 변경하지 않는다. 다른 Task·정책·메모리 개편은 없다. 전체 파일명 manifest와 실제 문서 검사 결과를 이 절 아래에 기록한다. 아래 upload/download/untrack 대기는 당시 이력이며 완료된 원격/index 축만 이 최종 인계로 supersede한다.


Main actual receipt: `rmsepskek02/Hexiege-Logs` 생성 및 **private=true 확인**, Release **410115016**, tag `logs-2026-10`. 안정적인 Release 위치: `https://github.com/rmsepskek02/Hexiege-Logs/releases/tag/logs-2026-10`. 이전 public 결정 대기/저장소 생성 미확인 기록은 이 인계로 supersede한다.

ZIP와 외부 manifest **업로드 성공**이 확인됐다. 첫 업로드 timeout 실패는 별도 이전 단계로 보존한다. 현재는 실제 download 진행 중이며 **download 전체 검증·untrack·commit/merge 완료를 주장하지 않는다**.

| 항목 | Main 실제 remote receipt |
|---|---|
| ZIP asset | ID632539446; `2026-10-12_legacy-logs.zip`; 120,495,020 bytes; SHA256 `74a689e76c43524a941f84f36145066e9ca239a1cceceadbfc6ca3337b5c26d2` |
| manifest asset | ID632540847; `2026-10-12_legacy-logs.manifest.json`; 65,377 bytes; SHA256 `aae378ee4ca1688e58e6097d07e059c452ea6a791ae42c5ed3b296d2533c293e` |

### 파일명 정규화의 실제 대응

GitHub 반환 이름에서 한글이 제거되어 ZIP `2026-10-12_.zip`, manifest `2026-10-12_.manifest.json`이 됐고 Main이 remote ASCII 이름을 위와 같이 PATCH했다. 로컬 ZIP 원래 이름 **`2026-10-12_기존로그.zip`** 및 ZIP의 한글 Release label은 유지한다. `legacy-logs`는 “기존로그”의 의미 대응이며 날짜·작업 의미를 버리지 않는다. Manifest 실제 remote 이름은 위 asset 행에 기록하고 로컬 manifest 원래 파일명/label은 확정 receipt가 없으므로 추정하지 않는다.

파일명 변경은 metadata 대응만이며 ZIP/manifest bytes·hash는 위 로컬 완료 인계와 일치한다. 플랫폼 정규화 예외만 [LogRules §1.10](../../../LogRules.md)에 최소 보강했다. Signed/인증값 포함 임시 download URL이나 credential/프로세스 세부정보는 전재하지 않았다. 실제 asset download URL은 안전한 안정 URL receipt를 받은 뒤 추가하며 현재 확인된 위치는 위 Release URL+asset IDs다.

### 현재 gate

**Private 업로드 성공 ≠ remote full verification.** Main의 실제 ZIP·manifest download 및 전체 entry path/count/size/SHA256 검증 결과를 기다린다. 그 gate가 통과하기 전에는 좁은 tracked raw의 untrack/commit·merge를 완료로 기록하지 않는다. 로컬 원본·raw.meta·CrossAudit·Git 이력 보존을 유지한다.

이번 직접 수정은 LogRules §1.10의 정규화 예외 + 현재 Task Research/Plan3파일뿐이다. 다른 정책·WORKFLOW/AGENTS·상태·메모리·소스·.gitignore를 추가 변경하지 않았다. 문서 담당은 Git/API/Tool/ZIP/download/삭제를 실행하지 않았다. 아래 과거 미완료 영수증은 이력으로 보존한다.


사용자가 **`rmsepskek02/Hexiege-Logs` 로그 전용 private 저장소** 보관을 선택했고 Main의 새 저장소 생성·private Release 진행을 승인했다. 이전 public 저장소 보관 선택 대기는 이 결정으로 supersede한다. **정확 대상/매 upload private 확인 → private Release upload → ZIP·manifest 실제 download 전체 검증 → 승인된 좁은 원본 목록 untrack** 순서다. 저장소 생성/원격 upload/download 검증/untrack 완료는 아직 Main receipt 대기이며 승인만으로 완료를 기록하지 않는다.

Durable 계약은 [LogRules §1.10](../../../LogRules.md)에 직접 반영했다. 매 upload `private=true`와 정확 owner/repo 확인, public/익명공개 가능 대상 전송 금지, 월별 `로그 보관 YYYY-MM`, ZIP 이름/manifest·전체 entry 검증, source Git 신규 raw 재추적 방지, 별도 clone 불필요, 소스 doc/meta·CrossAudit 보존, 로컬 원본 보존 및 history rewrite 금지를 유지한다. WORKFLOW는 단일 권위 LogRules 참조가 이미 있으므로 수정하지 않는다. AGENTS의 기존 LogRules 인덱스도 충분하여 추가 이력/규칙 사본을 만들지 않는다.

### 실제 로컬 완료 인계 — 원격 검증과 분리

Main/programmer 인계이며 문서 담당의 Tool 실행/파일 hash 재측정이 아니다.

| 산출물 | 실제 인계 |
|---|---|
| 원본 | RuntimeLog169 / 2,136,923,327 bytes |
| 로컬 ZIP | 120,495,020 bytes; SHA256 `74a689e76c43524a941f84f36145066e9ca239a1cceceadbfc6ca3337b5c26d2` |
| 외부 manifest | 65,377 bytes; SHA256 `aae378ee4ca1688e58e6097d07e059c452ea6a791ae42c5ed3b296d2533c293e` |
| Tool fixture | 12 PASS |
| private upload/download | 아직 최종 receipt 미수신; 로컬 PASS를 원격 검증으로 승격하지 않음 |
| untrack/commit·merge | 아직 최종 receipt 미수신, 원격 전체 검증 전 gate 닫힘 |

이번 문서 직접 수정은 LogRules.md + 이 Task Research/Plan3파일뿐이다. Git/API/코드 실행/씬·assets/로그 삭제·credential 기록은 없다. Main private 보관/download/untrack receipt 후에만 관련 상태·메모리를 최소 후속하고 실제 문서 검사0 및 전체 수정 목록을 인계한다. 아래 인증 차단·Tool/ZIP 미완료 기록은 이전 단계로 보존한다.


## 2026-10-12 인증 회복·공개범위 결정 대기 영수증

Main 최종 인계: **GCM 인증 retry exit0, 실제 repository API200** 확인. 앞선 timeout/401은 이전 실패 단계로 보존하며 현재 인증 gate는 회복됐다. 문서 담당이 인증/API 요청을 실행하거나 인증 원문을 출력·저장한 결과가 아니다.

저장소는 **private=False(PUBLIC)**이고 Release 목록은 없다는 Main 확인이다. 공개 API 재연결200과 실제 repo 인증200은 별도 증거이며, **인증 성공은 원시 로그 공개 업로드 승인과 다르다**.

- 사용자에게 **비공개 보관 또는 민감정보 점검 후 공개 Release** 선택을 async 질문했으며 현재 결정 대기다. 결정 전 raw 공개 Release 업로드 금지.
- Programmer에게 좁은 Tool 구현·로컬 ZIP 검증 GO 전달 완료; 로컬 준비는 계속 가능하나 실제 구현/ZIP 검증 완료 결과는 아직 인계되지 않았다. 이전 Tool 설계만/ZIP 미생성 기록은 당시 단계다.
- **실제 upload·download verification·untrack·commit/merge 미실행**. 공개범위 결정과 보관 검증 전 untrack gate는 계속 닫혀 있으며 로컬 원본/meta·Git 이력·CrossAudit 보존 조건도 유지한다.
- 지금은 이 Task2문서의 현재 영수증만 추가한다. LogRules/WORKFLOW 등 durable 정책, 상태·메모리·코드·Tool/ZIP·.gitignore를 변경하지 않는다. 인증/시크릿 원문·프로세스 세부정보를 전재하지 않았으며 추가 조사도 하지 않는다.

다음은 사용자 공개범위 결정 및 실제 로컬 준비/보관 검증 결과 수신 후 최종 receipt만 반영한다. 아래 이전 차단 영수증은 이력이다.

이번 영수증 후 실제 UTF-8 check_docs37377 **exit0·문제0**, 별도 Task2문서 파일 링크0. 공개범위 결정과 보관 검증은 여전히 대기이며 검사 성공을 upload/untrack 승인으로 읽지 않는다.


사용자가 Git 이력은 보존하고 main 최종 tree의 RuntimeLog txt 본문만 archive 검증 후 추적 해제하는 운영 D 방향을 승인했다. 현재는 외부 인증으로 차단된 준비 단계이며 운영 완료가 아니다.

- 원본 RuntimeLog **169개 / 2,136,923,327 bytes**. Programmer 최종 inventory 인계: 해시 읽기 중 변경0, fingerprint `93fe4221a617ee639aab3086d61e98a4a390178e19ba6ab5c37769c504fa7bbc`. Fingerprint 산식은 이번 인계에 없으므로 기존 stat TSV 등록 hash와 같은 검사값으로 취급하거나 비교하지 않는다.
- GitHub repo API probe **401**, 사용자 login 갱신 대기. 인증값 출력·저장 없음이라는 Main 인계이며 이 문서에 인증값을 복사하지 않았다.
- **ZIP 미생성 / 업로드·download verification 미실행 / untrack 미실행**. 검증된 업로드와 실제 download 확인 전 untrack gate는 닫혀 있다. 로컬 raw는 그대로 보존한다.
- Main의 **Git 잠금 제거 성공** 인계: lock 내용이 없고 동작 프로세스가 없는 조건을 확인한 뒤 제거했다. 문서 담당이 Git/lock 파일을 읽거나 제거한 결과가 아니며 이전 stale 후보 대기 기록을 supersede한다.
- `Tools/archive_runtime_logs.py`는 **계획·설계뿐, create/구현 없음**. 예정 ZIP은 `Temp/log-archive/2026-10-12_기존로그.zip`; compressed size 실제 확인 뒤 큰 경우에만 독립 parts를 검토한다. 본 문서 작업에서 Tool/Temp/ZIP을 생성하지 않았다.
- Raw txt.meta 및 실제 코드·assets·doc/folder metadata는 §1.10에 따라 Git 보존한다. CrossAuditResult.txt(943 bytes, SHA256 `739F47FF2458AF5EB2AAA9B21B0E90839469D3C63B1DB24A6979CF54977EE7D9`)도 runtime archive/untrack 범위에서 제외하여 Git 유지한다. 전체 txt/_Logs ignore·history rewrite·로컬/remote 삭제 없음.

이번 영수증 직접 수정 범위는 **이 기존 Task Research/Plan2개만**이다. 상태·메모리·규칙·다른 Task를 갱신하지 않고 추가 조사도 중단한다. 아래 이전 단계 기록은 삭제하지 않는다. 최신 문서 검사 결과는 실제 종료 확인 뒤 인계한다.

최종 실제 UTF-8 문서 검사(`-X utf8 Tools/check_docs.py`, session62188): **exit0·문제0**. _Tasks/_Logs 제외 범위를 확인했고 별도 Task2문서 활성 파일 링크0이다. 외부401 차단은 해소되지 않았으며 ZIP/업로드/download 검증/untrack/Tool create는 미실행 상태로 종료한다.


## 2026-10-12 운영 D — 준비·검증 후 좁은 추적 해제 계획

사용자는 Git 이력은 남기고 main 최종 tree에는 원시 RuntimeLog **txt 본문**이 남지 않게 진행하는 방향을 승인했다. 로컬 원본은 그대로 보존하며, 검증된 archive가 생기기 전에 추적을 해제하지 않는다. 현재 문서 담당의 실행은 이 기존 Task Research/Plan 갱신과 경로·크기·meta 존재 확인뿐이다. 아래 B/C 결과 및 미실행/권한 대기 문장은 이전 단계 이력이며 새 운영 계획으로 필요한 범위만 supersede한다. Git/ZIP/업로드/삭제/Tool 코드 구현은 실행하지 않았다.

### Main 인계와 현재 blocker

| 항목 | Main 인계 / 증거 경계 |
|---|---|
| 원격/main 관계 | 원격6a2f2403이 HEAD ad34bf93의 ancestor. 문서 담당은 Git을 실행하지 않음 |
| 작업 파일 상태 | dirty93 / untracked278. 기존 사용자 변경이며 일괄 stage/reset/stash/정리 금지 |
| 추적 raw | tracked123 약828MB; branch-main 차이 raw102 약819MB. 현재 디스크169와 같은 모집단이라고 가정하지 않으며 Main이 exact-path set을 대조해야 함 |
| index lock | index.lock10/10 stale 후보, Main은 관련 process 없음 확인. 후보일 뿐 자동 제거 허가/성공이 아님. Main의 재확인·승인 전 index 수정 gate 중단 |
| GitHub 인증 | 기존 credential의 noninteractive repo API probe **401**. 인증값 출력/저장 없었다는 Main 인계. login 갱신은 사용자 async 질문/진행 대기이며 인증/업로드 성공 아님 |
| 진행 가능 범위 | 로컬 준비는 Temp에서 가능. upload→실제 download 검증 전 **untrack 금지**. Commit/merge 승인은 별도 Main 단계이며 현재 archive/untrack 준비 먼저 |

### 로컬 모집단 실제 확인 — 내용 미열람

`Assets/_Project/Docs/_Logs/` 아래 파일 경로·Length·짝 meta 존재만 조사했다. Raw token 검색/내용/헤더 출력은 하지 않았다. 전체 txt170개 2,136,924,270 bytes 중 RuntimeLog 정확 파일명 **169개 2,136,923,327 bytes**(decimal2.136923327GB)가 archive 모집단이다. 각 RuntimeLog의 txt.meta169개 모두 로컬 존재, 합계27,550 bytes. Meta는 archive 원본 txt와 별도 Git 보존 대상이며 숫자를 raw txt 개수에 합산하지 않는다.

| 정확 파일명 | 개수 |
|---|---:|
| RuntimeLog_client.txt | 5 |
| RuntimeLog_device_2.txt | 1 |
| RuntimeLog_device.txt | 114 |
| RuntimeLog_host.txt | 9 |
| RuntimeLog.txt | 40 |

제외·보존: `Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/B3_RoleSwap_2026-08-03_2232/CrossAuditResult.txt` **943 bytes**, 대응 meta 존재. 이것은 RuntimeLog가 아닌 CrossAudit 산출물이며 Git 유지한다. 다른 임의 txt·Markdown·Log.md·Task·소스/실제 assets 및 모든 doc/folder metadata도 제외 대상이 아니다.

과거 중첩 경로의 RuntimeLog_client/host2개는 이전 1단계 폴더 ignore 제안에 매치하지 않으므로 **정확 2경로를 추가**한다. Device_2도 명시적으로 포함한다. 전체 txt 또는 _Logs 전체 제외로 해결하지 않는다.

### Meta/orphan 경계 및 규칙 정합성

Main 추가 확인: “main에서 raw 없음”은 RuntimeLog **txt 본문** 조건이다. §1.10의 metadata Git 유지 정책에 따라 **raw txt.meta도 보존**하며 ignore/untrack/delete하지 않는다. 로컬 txt를 보존하므로 현재 working copy의 짝은 유지한다. 원본 txt 없는 새 checkout에서 meta-only 상태가 생길 수 있는 경계는 정책상 의도된 보존으로 명시하며, Unity 경고/정리 동작을 실행하거나 검증했다고 주장하지 않는다. Orphan 이름만으로 meta를 제거하지 않으며 실제 Unity에서 문제가 관측되면 별도 사용자 판단이 필요하다. Folder meta도 유지한다.

따라서 지금은 LogRules/WORKFLOW 교체가 필요하지 않다. 향후 raw.meta 제거/ignore, CrossAudit txt 제외 또는 main “모든 txt 없음”으로 목표를 넓히려면 현행 §1.10과 충돌하므로 먼저 사용자에게 알려 별도 정책 승인을 받아야 한다. 이번 Plan은 그런 변경을 하지 않는다.

### Gate 및 도구 범위

준비 Tool은 programmer가 설계 중인 **`Tools/archive_runtime_logs.py` 1파일**이며 현재 아직 write 없음이라는 Main 인계다. Tool의 모든 산출물은 `Temp/log-archive/2026-10-12/01_41_log-rules-history-archive/`의 새 run 하위만 사용한다. 코드·asset·docsmetadata·원본 raw·.git 및 .gitignore를 Tool이 변경하지 않는다. Zip/manifest와 고정 snapshot은 Temp만; Programmer 구현 GO는 이 Plan의 좁은 범위를 Main이 확인한 뒤 별도 전달한다.

순서: 모집단169 확정 → Temp immutable snapshot+각 파일 hash/ZIP/로컬 검증 → 인증 복구와 권한 확인 → 월별 **로그 보관2026-10** Release 업로드/실제 download hash·내부169 파일 검증 → Main이 tracked set/정확 ignore/lock gate 확인 → 로컬 원본 보존한 좁은 untrack → final main tree RuntimeLog txt0 및 metadata/CrossAudit/실제 변경 보존 확인. 인증401이나 미검증 업로드가 남으면 untrack gate는 닫혀 있다. History rewrite/원격 prune/삭제는 없다.

### 169개 경로 등록 manifest — stat snapshot, archive 검증 아님

아래 TSV는 순번/원본 경로/정확 bytes/meta 존재를 등록한 조사 모집단이다. **개별 content SHA256·동결·ZIP·upload/download 검증은 아직 없다.** 실행 도구는 이 목록을 재대조하고 변동 시 중단/새 freeze 승인으로 처리한다. 이 TSV의 LF-normalized UTF-8(terminal LF1) SHA256은 `9f10286ea929079179cf403db5231ba4bb2de04b1558749f569d7e7bf9bbdf12`다.

```tsv
id	path	bytes	txt_meta
1	Assets/_Project/Docs/_Logs/_editor/2026-08-31/RuntimeLog.txt	3665967	present
2	Assets/_Project/Docs/_Logs/_editor/2026-09-01/RuntimeLog.txt	320912	present
3	Assets/_Project/Docs/_Logs/_editor/2026-09-02/RuntimeLog.txt	1988	present
4	Assets/_Project/Docs/_Logs/_editor/2026-09-03/RuntimeLog.txt	22031	present
5	Assets/_Project/Docs/_Logs/_editor/2026-09-07/RuntimeLog.txt	1794861	present
6	Assets/_Project/Docs/_Logs/_editor/2026-09-09/RuntimeLog.txt	4340746	present
7	Assets/_Project/Docs/_Logs/_editor/2026-09-10/RuntimeLog.txt	1505656	present
8	Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt	3190456	present
9	Assets/_Project/Docs/_Logs/_editor/2026-09-14/RuntimeLog.txt	270021	present
10	Assets/_Project/Docs/_Logs/_editor/2026-09-19/RuntimeLog.txt	355645	present
11	Assets/_Project/Docs/_Logs/_editor/2026-09-20/RuntimeLog.txt	4088551	present
12	Assets/_Project/Docs/_Logs/_editor/2026-09-21/RuntimeLog.txt	1559577	present
13	Assets/_Project/Docs/_Logs/_editor/2026-09-22/RuntimeLog.txt	2673357	present
14	Assets/_Project/Docs/_Logs/_editor/2026-09-23/RuntimeLog.txt	460989	present
15	Assets/_Project/Docs/_Logs/_editor/2026-09-27/RuntimeLog.txt	1386101	present
16	Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt	1266169	present
17	Assets/_Project/Docs/_Logs/_editor/2026-09-29/RuntimeLog.txt	1227361	present
18	Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt	2254532	present
19	Assets/_Project/Docs/_Logs/_editor/2026-10-01/RuntimeLog.txt	711102	present
20	Assets/_Project/Docs/_Logs/_editor/2026-10-02/RuntimeLog.txt	18799934	present
21	Assets/_Project/Docs/_Logs/_editor/2026-10-03/RuntimeLog.txt	19933829	present
22	Assets/_Project/Docs/_Logs/_editor/2026-10-04/RuntimeLog.txt	12664817	present
23	Assets/_Project/Docs/_Logs/_editor/2026-10-05/RuntimeLog.txt	1374265	present
24	Assets/_Project/Docs/_Logs/_editor/2026-10-07/RuntimeLog.txt	10224802	present
25	Assets/_Project/Docs/_Logs/_editor/2026-10-08/RuntimeLog.txt	99166867	present
26	Assets/_Project/Docs/_Logs/_editor/2026-10-09/RuntimeLog.txt	45762192	present
27	Assets/_Project/Docs/_Logs/_editor/2026-10-10/RuntimeLog.txt	2434722	present
28	Assets/_Project/Docs/_Logs/_editor/2026-10-11/RuntimeLog.txt	41820521	present
29	Assets/_Project/Docs/_Logs/_editor/2026-10-12/RuntimeLog.txt	6680112	present
30	Assets/_Project/Docs/_Logs/2026-04-30/02_29_movement-combat-redesign/RuntimeLog.txt	0	present
31	Assets/_Project/Docs/_Logs/2026-05-06/01_19_movement-slot-ghostfix/RuntimeLog.txt	0	present
32	Assets/_Project/Docs/_Logs/2026-05-07/19_43_movement-combat-full-rewrite/RuntimeLog.txt	0	present
33	Assets/_Project/Docs/_Logs/2026-05-11/23_19_unit-movement-redesign/RuntimeLog.txt	0	present
34	Assets/_Project/Docs/_Logs/2026-05-13/09_57_unit-facing-direction-bug/RuntimeLog.txt	0	present
35	Assets/_Project/Docs/_Logs/2026-05-14/14_30_unit-rotation-system-fix/RuntimeLog.txt	0	present
36	Assets/_Project/Docs/_Logs/2026-05-15/02_57_character-display-bug/RuntimeLog.txt	0	present
37	Assets/_Project/Docs/_Logs/2026-05-31/13_18_panel-button-size-inconsistency/RuntimeLog.txt	25840	present
38	Assets/_Project/Docs/_Logs/2026-05-31/upgrade-diagnostic/RuntimeLog.txt	8711	present
39	Assets/_Project/Docs/_Logs/2026-06-08/01_12_vfxpoolitem-null-check/RuntimeLog.txt	41863	present
40	Assets/_Project/Docs/_Logs/2026-06-08/02_27_networkobject-invalid-destroy/RuntimeLog.txt	3334	present
41	Assets/_Project/Docs/_Logs/2026-06-22/09_32_canvas-sorting-order-fix/RuntimeLog_host.txt	8749	present
42	Assets/_Project/Docs/_Logs/2026-06-25/07_25_matchmaking-debug/RuntimeLog_host.txt	2298	present
43	Assets/_Project/Docs/_Logs/2026-07-09/01_12_combat-hit-timing-sync/RuntimeLog_client.txt	411134	present
44	Assets/_Project/Docs/_Logs/2026-07-09/01_12_combat-hit-timing-sync/RuntimeLog_host.txt	2811510	present
45	Assets/_Project/Docs/_Logs/2026-07-12/07_55_movement-walk-anim-sync/RuntimeLog_client.txt	2302086	present
46	Assets/_Project/Docs/_Logs/2026-07-12/07_55_movement-walk-anim-sync/RuntimeLog_host.txt	2859130	present
47	Assets/_Project/Docs/_Logs/2026-07-20/11_00_quakespirit-aoe-verify/RuntimeLog_client.txt	10658	present
48	Assets/_Project/Docs/_Logs/2026-07-20/11_00_quakespirit-aoe-verify/RuntimeLog_host.txt	117533	present
49	Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/B3_RoleSwap_2026-08-03_2232/RuntimeLog_client.txt	1299574	present
50	Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/B3_RoleSwap_2026-08-03_2232/RuntimeLog_host.txt	2688253	present
51	Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/RuntimeLog_client.txt	2975112	present
52	Assets/_Project/Docs/_Logs/2026-07-22/13_26_unit-action-sequence-implementation/RuntimeLog_host.txt	11749014	present
53	Assets/_Project/Docs/_Logs/2026-08-04/16_49_skill-status-debug/RuntimeLog_host.txt	42295	present
54	Assets/_Project/Docs/_Logs/2026-08-10/14_12_mistshrine-heal-implementation/RuntimeLog_host.txt	55604	present
55	Assets/_Project/Docs/_Logs/2026-08-23/23_10_logcat/RuntimeLog_device.txt	4668601	present
56	Assets/_Project/Docs/_Logs/2026-08-24/12_30_logcat/RuntimeLog_device.txt	1027125	present
57	Assets/_Project/Docs/_Logs/2026-08-24/13_34_logcat/RuntimeLog_device.txt	2102017	present
58	Assets/_Project/Docs/_Logs/2026-08-24/15_16_logcat/RuntimeLog_device.txt	9408596	present
59	Assets/_Project/Docs/_Logs/2026-08-24/17_03_logcat/RuntimeLog_device.txt	1128162	present
60	Assets/_Project/Docs/_Logs/2026-08-24/23_04_logcat/RuntimeLog_device.txt	945431	present
61	Assets/_Project/Docs/_Logs/2026-08-26/11_04_logcat/RuntimeLog_device.txt	1548873	present
62	Assets/_Project/Docs/_Logs/2026-08-26/21_05_logcat/RuntimeLog_device.txt	7851803	present
63	Assets/_Project/Docs/_Logs/2026-08-27/00_28_logcat/RuntimeLog_device.txt	21114585	present
64	Assets/_Project/Docs/_Logs/2026-08-27/11_55_logcat/RuntimeLog_device.txt	12943132	present
65	Assets/_Project/Docs/_Logs/2026-08-27/13_15_logcat/RuntimeLog_device.txt	26948502	present
66	Assets/_Project/Docs/_Logs/2026-08-27/19_17_logcat/RuntimeLog_device.txt	1717646	present
67	Assets/_Project/Docs/_Logs/2026-08-28/23_33_logcat/RuntimeLog_device.txt	7834201	present
68	Assets/_Project/Docs/_Logs/2026-08-29/04_55_logcat/RuntimeLog_device.txt	8500556	present
69	Assets/_Project/Docs/_Logs/2026-08-29/19_48_logcat/RuntimeLog_device.txt	1067783	present
70	Assets/_Project/Docs/_Logs/2026-08-30/16_31_logcat/RuntimeLog_device.txt	1139039	present
71	Assets/_Project/Docs/_Logs/2026-08-30/23_53_logcat/RuntimeLog_device.txt	11714648	present
72	Assets/_Project/Docs/_Logs/2026-08-31/04_09_logcat/RuntimeLog_device.txt	18855879	present
73	Assets/_Project/Docs/_Logs/2026-08-31/12_32_logcat/RuntimeLog_device.txt	8965734	present
74	Assets/_Project/Docs/_Logs/2026-08-31/12_54_logcat/RuntimeLog_device.txt	17391460	present
75	Assets/_Project/Docs/_Logs/2026-08-31/14_27_logcat/RuntimeLog_device.txt	9653704	present
76	Assets/_Project/Docs/_Logs/2026-08-31/19_02_logcat/RuntimeLog_device.txt	11575669	present
77	Assets/_Project/Docs/_Logs/2026-09-01/21_55_logcat/RuntimeLog_device.txt	14075300	present
78	Assets/_Project/Docs/_Logs/2026-09-02/13_07_logcat/RuntimeLog_device.txt	1082627	present
79	Assets/_Project/Docs/_Logs/2026-09-03/03_10_logcat/RuntimeLog_device.txt	54385	present
80	Assets/_Project/Docs/_Logs/2026-09-03/03_26_logcat/RuntimeLog_device.txt	54385	present
81	Assets/_Project/Docs/_Logs/2026-09-07/04_58_logcat/RuntimeLog_device.txt	1071296	present
82	Assets/_Project/Docs/_Logs/2026-09-07/09_43_logcat/RuntimeLog_device.txt	12123117	present
83	Assets/_Project/Docs/_Logs/2026-09-07/09_59_logcat/RuntimeLog_device.txt	12123117	present
84	Assets/_Project/Docs/_Logs/2026-09-07/20_00_logcat/RuntimeLog_device.txt	9793330	present
85	Assets/_Project/Docs/_Logs/2026-09-09/00_43_logcat/RuntimeLog_device.txt	6732514	present
86	Assets/_Project/Docs/_Logs/2026-09-09/10_08_logcat/RuntimeLog_device.txt	1059552	present
87	Assets/_Project/Docs/_Logs/2026-09-09/16_15_logcat/RuntimeLog_device.txt	2263317	present
88	Assets/_Project/Docs/_Logs/2026-09-09/19_17_logcat/RuntimeLog_device.txt	2374468	present
89	Assets/_Project/Docs/_Logs/2026-09-10/11_06_logcat/RuntimeLog_device.txt	13104173	present
90	Assets/_Project/Docs/_Logs/2026-09-10/18_35_logcat/RuntimeLog_device.txt	1012147	present
91	Assets/_Project/Docs/_Logs/2026-09-13/01_12_logcat/RuntimeLog_device.txt	18726505	present
92	Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt	17826615	present
93	Assets/_Project/Docs/_Logs/2026-09-13/16_17_logcat/RuntimeLog_device.txt	12585528	present
94	Assets/_Project/Docs/_Logs/2026-09-13/21_35_logcat/RuntimeLog_device.txt	13001295	present
95	Assets/_Project/Docs/_Logs/2026-09-13/21_41_logcat/RuntimeLog_device.txt	18273472	present
96	Assets/_Project/Docs/_Logs/2026-09-13/22_28_logcat/RuntimeLog_device.txt	21471365	present
97	Assets/_Project/Docs/_Logs/2026-09-14/02_58_logcat/RuntimeLog_device.txt	27403157	present
98	Assets/_Project/Docs/_Logs/2026-09-20/00_04_logcat/RuntimeLog_device.txt	15008568	present
99	Assets/_Project/Docs/_Logs/2026-09-20/13_10_logcat/RuntimeLog_device.txt	503600	present
100	Assets/_Project/Docs/_Logs/2026-09-20/13_44_logcat/RuntimeLog_device.txt	860033	present
101	Assets/_Project/Docs/_Logs/2026-09-20/18_15_logcat/RuntimeLog_device.txt	19575368	present
102	Assets/_Project/Docs/_Logs/2026-09-20/23_50_logcat/RuntimeLog_device.txt	26205756	present
103	Assets/_Project/Docs/_Logs/2026-09-21/13_27_logcat/RuntimeLog_device.txt	10052945	present
104	Assets/_Project/Docs/_Logs/2026-09-21/13_28_logcat/RuntimeLog_device.txt	10052945	present
105	Assets/_Project/Docs/_Logs/2026-09-21/16_07_logcat/RuntimeLog_device.txt	5812937	present
106	Assets/_Project/Docs/_Logs/2026-09-21/21_15_logcat/RuntimeLog_device.txt	3821935	present
107	Assets/_Project/Docs/_Logs/2026-09-21/21_16_logcat/RuntimeLog_device.txt	3821935	present
108	Assets/_Project/Docs/_Logs/2026-09-21/23_01_logcat/RuntimeLog_device.txt	4375814	present
109	Assets/_Project/Docs/_Logs/2026-09-22/04_11_logcat/RuntimeLog_device.txt	1388955	present
110	Assets/_Project/Docs/_Logs/2026-09-22/11_44_logcat/RuntimeLog_device.txt	10248067	present
111	Assets/_Project/Docs/_Logs/2026-09-22/12_40_logcat/RuntimeLog_device.txt	15597069	present
112	Assets/_Project/Docs/_Logs/2026-09-22/17_30_logcat/RuntimeLog_device.txt	12046071	present
113	Assets/_Project/Docs/_Logs/2026-09-22/17_39_logcat/RuntimeLog_device.txt	12046071	present
114	Assets/_Project/Docs/_Logs/2026-09-22/20_43_logcat/RuntimeLog_device.txt	22733389	present
115	Assets/_Project/Docs/_Logs/2026-09-23/09_51_logcat/RuntimeLog_device.txt	11603437	present
116	Assets/_Project/Docs/_Logs/2026-09-27/04_34_logcat/RuntimeLog_device_2.txt	9545447	present
117	Assets/_Project/Docs/_Logs/2026-09-27/04_34_logcat/RuntimeLog_device.txt	9543677	present
118	Assets/_Project/Docs/_Logs/2026-09-27/17_21_logcat/RuntimeLog_device.txt	386042	present
119	Assets/_Project/Docs/_Logs/2026-09-28/03_41_logcat/RuntimeLog_device.txt	12211000	present
120	Assets/_Project/Docs/_Logs/2026-09-28/03_50_logcat/RuntimeLog_device.txt	12212503	present
121	Assets/_Project/Docs/_Logs/2026-09-28/16_09_logcat/RuntimeLog_device.txt	4682571	present
122	Assets/_Project/Docs/_Logs/2026-09-28/20_10_logcat/RuntimeLog_device.txt	3824954	present
123	Assets/_Project/Docs/_Logs/2026-09-28/21_42_logcat/RuntimeLog_device.txt	15261260	present
124	Assets/_Project/Docs/_Logs/2026-09-28/22_25_logcat/RuntimeLog_device.txt	19549478	present
125	Assets/_Project/Docs/_Logs/2026-09-28/22_41_logcat/RuntimeLog_device.txt	19550981	present
126	Assets/_Project/Docs/_Logs/2026-09-29/00_12_logcat/RuntimeLog_device.txt	22509024	present
127	Assets/_Project/Docs/_Logs/2026-09-29/12_47_logcat/RuntimeLog_device.txt	6126350	present
128	Assets/_Project/Docs/_Logs/2026-09-29/18_13_logcat/RuntimeLog_device.txt	6559051	present
129	Assets/_Project/Docs/_Logs/2026-09-29/20_17_logcat/RuntimeLog_device.txt	4085287	present
130	Assets/_Project/Docs/_Logs/2026-09-30/00_24_logcat/RuntimeLog_device.txt	12055481	present
131	Assets/_Project/Docs/_Logs/2026-09-30/00_47_logcat/RuntimeLog_device.txt	18581892	present
132	Assets/_Project/Docs/_Logs/2026-09-30/11_37_logcat/RuntimeLog_device.txt	458520	present
133	Assets/_Project/Docs/_Logs/2026-09-30/13_46_logcat/RuntimeLog_device.txt	5571108	present
134	Assets/_Project/Docs/_Logs/2026-09-30/18_50_logcat/RuntimeLog_device.txt	7936013	present
135	Assets/_Project/Docs/_Logs/2026-10-01/00_08_logcat/RuntimeLog_device.txt	23120517	present
136	Assets/_Project/Docs/_Logs/2026-10-01/00_10_logcat/RuntimeLog_device.txt	23120517	present
137	Assets/_Project/Docs/_Logs/2026-10-01/05_11_logcat/RuntimeLog_device.txt	17768639	present
138	Assets/_Project/Docs/_Logs/2026-10-01/22_13_logcat/RuntimeLog_device.txt	4828836	present
139	Assets/_Project/Docs/_Logs/2026-10-01/22_35_logcat/RuntimeLog_device.txt	15423673	present
140	Assets/_Project/Docs/_Logs/2026-10-01/22_36_logcat/RuntimeLog_device.txt	15423673	present
141	Assets/_Project/Docs/_Logs/2026-10-02/02_09_logcat/RuntimeLog_device.txt	22951482	present
142	Assets/_Project/Docs/_Logs/2026-10-02/13_03_logcat/RuntimeLog_device.txt	20486853	present
143	Assets/_Project/Docs/_Logs/2026-10-02/13_08_logcat/RuntimeLog_device.txt	8905959	present
144	Assets/_Project/Docs/_Logs/2026-10-02/17_05_logcat/RuntimeLog_device.txt	24449289	present
145	Assets/_Project/Docs/_Logs/2026-10-03/01_03_logcat/RuntimeLog_device.txt	16573017	present
146	Assets/_Project/Docs/_Logs/2026-10-03/04_02_logcat/RuntimeLog_device.txt	24580853	present
147	Assets/_Project/Docs/_Logs/2026-10-03/12_39_logcat/RuntimeLog_device.txt	16508034	present
148	Assets/_Project/Docs/_Logs/2026-10-03/18_34_logcat/RuntimeLog_device.txt	19283128	present
149	Assets/_Project/Docs/_Logs/2026-10-03/22_48_logcat/RuntimeLog_device.txt	11440197	present
150	Assets/_Project/Docs/_Logs/2026-10-03/23_04_logcat/RuntimeLog_device.txt	24501524	present
151	Assets/_Project/Docs/_Logs/2026-10-04/00_39_logcat/RuntimeLog_device.txt	39352325	present
152	Assets/_Project/Docs/_Logs/2026-10-04/13_02_logcat/RuntimeLog_device.txt	681514	present
153	Assets/_Project/Docs/_Logs/2026-10-04/15_29_logcat/RuntimeLog_device.txt	20743547	present
154	Assets/_Project/Docs/_Logs/2026-10-04/18_16_logcat/RuntimeLog_device.txt	37012148	present
155	Assets/_Project/Docs/_Logs/2026-10-04/22_56_logcat/RuntimeLog_device.txt	22893864	present
156	Assets/_Project/Docs/_Logs/2026-10-05/03_55_logcat/RuntimeLog_device.txt	2427692	present
157	Assets/_Project/Docs/_Logs/2026-10-07/15_16_logcat/RuntimeLog_device.txt	36503483	present
158	Assets/_Project/Docs/_Logs/2026-10-08/01_26_logcat/RuntimeLog_device.txt	12326801	present
159	Assets/_Project/Docs/_Logs/2026-10-08/11_54_logcat/RuntimeLog_device.txt	75423570	present
160	Assets/_Project/Docs/_Logs/2026-10-08/13_25_logcat/RuntimeLog_device.txt	71292267	present
161	Assets/_Project/Docs/_Logs/2026-10-08/18_52_logcat/RuntimeLog_device.txt	104159106	present
162	Assets/_Project/Docs/_Logs/2026-10-09/00_10_logcat/RuntimeLog_device.txt	59705371	present
163	Assets/_Project/Docs/_Logs/2026-10-09/21_18_logcat/RuntimeLog_device.txt	107960823	present
164	Assets/_Project/Docs/_Logs/2026-10-10/21_43_logcat/RuntimeLog_device.txt	3982352	present
165	Assets/_Project/Docs/_Logs/2026-10-11/00_46_logcat/RuntimeLog_device.txt	35490936	present
166	Assets/_Project/Docs/_Logs/2026-10-11/19_07_logcat/RuntimeLog_device.txt	45340210	present
167	Assets/_Project/Docs/_Logs/2026-10-11/20_28_logcat/RuntimeLog_device.txt	19607166	present
168	Assets/_Project/Docs/_Logs/2026-10-11/23_16_logcat/RuntimeLog_device.txt	35580884	present
169	Assets/_Project/Docs/_Logs/2026-10-12/10_10_logcat/RuntimeLog_device.txt	28981951	present
```

이번 직접 수정 파일은 이 Task Research/Plan2개뿐이다. 후속 정책/운영 상태를 메모리나 전체 status에 확대 전파하지 않는다. 이번 문서 검사는 실제 결과 확인 후 인계한다.


최종 완료: fresh checker43785가 **실제 exit0, 문제0**으로 종료했다. 규칙10/Markdown78/Claude40, _Tasks/_Logs 제외 범위를 확인했다. 아래 재실행 중 기록은 종료 전 단계이며 이 결과로 supersede한다. 원문24/24 hash 일치·규범 보존·별도 Task 링크 검사0 및 growth-only baseline 갱신을 확인했다. B/C 문서화 완료이며 archive 운영/.gitignore 적용/새 코드 최종 PASS는 없다.


### 최종 규범 검사와 파일 manifest

최종 1.1–1.14 원본 header14/14 동일, 원문24/24 동일 hash(H13 포함). §1.13 의존/owner/Role/sink모드, 수동2단계·오늘제외, hook4가드, lambda, 임시 코드 cleanup, 전체 Logs ignore금지 및 규칙의 이번 회차 상태/Task 하드링크 제거를 별도 문자열·섹션 대조했다. 실제 링크 검사: Rules/History/현재 cleanup·wave Task 총6파일의 파일/History anchor 문제0(코드 fence의 WORKFLOW exact 문자열은 활성 링크 검사에서 제외). 첫 정규식 검사가 exact code fence의 예시 링크2개를 실제 Task 링크로 오인했으며, fence 제외 뒤 실제 링크0임을 확인했다.

최종 checker 첫 실행29195 실제 exit0/problem0(규칙10/Markdown78/Claude40); 마지막 규칙 문장3개 정리 뒤 fresh checker43785 재실행 중. 종료 결과는 확인 후 추가한다.

아래는 실제 파일 byte SHA256이며 위 원문 블록 LF-normalized hash와 구별한다. 새 History 및 이 Task2파일, 기존 wave Task2파일, 규칙/인덱스/상태·메모리와 tool-generated baseline만 수정했다. 이 Research 자체는 manifest를 담으므로 순환 self-hash를 넣지 않는다(총15파일, 아래14파일+이 Research). 실제 Git 작업은 하지 않았다.

| 최종 파일 | byte SHA256 |
|---|---|
| `Assets/_Project/Docs/LogRules.md` | `bef2d4eb705ce45a5168b516acbddb126610177ceed5415469e964f4c9ac5ca1` |
| `Assets/_Project/Docs/LogHistory.md` | `e6ca47458f0d7d0269512a7b11847ab134ae64b84eb545f9a08d35ab1984c09c` |
| `Assets/_Project/Docs/WORKFLOW.md` | `82c4152f92146ee68db97dafcee9880ee37566e6898faef1777ab0b681bfd2ad` |
| `AGENTS.md` | `ff34bc9305b78ca52b9e72bd364758ca5dab55d11c74d77c6a13331fcc9acc14` |
| `Assets/_Project/Docs/_Tasks/2026-10-12/01_41_log-rules-history-archive/Plan.md` | `b295d5a1938d0bccd757ea5fa05da4616a1036bcfcb5e435b19fc46bf71cb02e` |
| `Assets/_Project/Docs/_Tasks/2026-10-11/21_50_wave-contact-authority-isolation/Research.md` | `794cdaa3f49d92df71133f8dd7e8c1e067d38358fcfbb5a5217cbd43f0dad2a4` |
| `Assets/_Project/Docs/_Tasks/2026-10-11/21_50_wave-contact-authority-isolation/Plan.md` | `b2b02a5084f522747bd381b8c0158d9a7b1f16dc37e38130472be55598d3b06a` |
| `Assets/_Project/Docs/PROJECT_STATUS.md` | `ad088ce55183a87cf738db06b8d3bae373eaeecf66c9b9c563ee06f052e418df` |
| `Assets/_Project/Docs/ROADMAP.md` | `31c2c4e5084c6cd9f6e27d0b454db0ed155cec5e63e87ac448eb00248c00d247` |
| `Assets/_Project/Docs/WORK_HISTORY.md` | `53afa268f9f4038100415f6f803dcab79a840977a34bff5fe253198af56e2e32` |
| `.claude/MEMORY.md` | `ab6fd12ba472af5bd38ad37a17d3eec97bf7d6150c07a38ae286bcc6dc1bb928` |
| `.claude/agent-memory/document-manager/MEMORY.md` | `eab2cd66d74bbbc2fcc02d5db6de847603e24667451a6df0230f236916dead2a` |
| `.claude/agent-memory/document-manager/doc-conventions.md` | `67beeb9c8367655cb139cb01caa14ae7063cc0c80f17a39061708ac90a4b4933` |
| `.claude/agent-memory/_baseline.json` | `50324690cdc632647773e3e4246226db08bba309275a23abea02252aab5fcf57` |



## 2026-10-12 B 완료·C 정책 문서화 인계

위 최초 계획/미실행 문장은 작성 당시 이력이다. Main mapping/Plan 검토와 B/C GO 후 B를 검증한 다음 C를 문서화했다. Wave는 기존 Task에 별도 반영했고 Main code GO 전달은 코드 최종 PASS와 다르다.

### B 원문·규범 대조

- 원본 LogRules 전체 LF-normalized UTF-8 SHA256: `47f0aee135548f299d28f661f2e44a428870487c23b01a2878ef7a9a28316a69`.
- B History: `dd03904b0cace07d74c489064ec45c4bc798983f77bc053b382d7890cec18aa1`; B LogRules: `3f775764c892bb9365104a3c3931ceada0ada638bc9ac5a83847970803741c5a`.
- 23 원문 블록을 복사한 뒤 각 hash 일치를 확인하고 해당 source 범위만 분리했다. Hash 정의는 CRLF→LF 정규화, UTF-8, 원문 마지막 LF 한 개 포함이다. 플랫폼 newline byte 동일성 주장이 아니라 문장·공백·표·코드 원문 동일성이다.
- 1.1–1.14 전체 절 제목/번호 원본 일치. 특히 1.13의 의존 방향·계층·단일 owner/종료·sink모드·Role·Lobby 비적용·자기 로깅 방어를 남겼다. 1.3 우선순위/UI·catch 예외, 1.7 lambda, 1.9 4가드/1초·32개/Suppressed 하한, 1.10 수동2단계·30/90일·오늘/경로/meta 보호, 1.14 금지10개, 2절 QA-Fix 역할·append 보존.
- B 후 실제 checker session11662: exit0, 문제0. 검사 범위 규칙10/Markdown78/Claude40, _Tasks/_Logs 제외. 별도 Markdown 파일·이력 anchor 검사0.
- 날짜 상태가 섞인 1.13은 H10으로 원문 보존하되 현재 아키텍처를 규칙에 추출했다. 과거 PASS를 현재 실행으로 승격하지 않았다.

| 블록 | 원본 LogRules 행 | 목적지 동일 SHA256 |
|---|---|---|
| H01 | 10–15 | `a734fbe39f2c2e684900ca7fecbcc58c35f5e1118c44536bc44a315321e0baa2` |
| H02 | 19–103 | `4f12cbb7c159e31c41b8b94b254288cdf2fda96eb40491eda60ffdcef7b4f55a` |
| H03a | 191–202 | `3eb6f8e55b4f1de640430d3d6d9be713676a6f9a5282d5af875753e3420625d0` |
| H03b | 245–256 | `f8975a5966d877c45a0854d5af4206c7cac24ad250bc6548037420cbb5cafdea` |
| H03c | 276–280 | `a18be89db61b336386a9729c2fa8b64ecc247c85a4a1de767f203528988b6a85` |
| H04a | 326–332 | `8fca0a8853f48918cdeddb93407be406d5bbe6df8f9a10e96bffa59b7878aba5` |
| H04b | 338–342 | `e41c2834a77c2695635fed12cf9012828231f570be0db80fa8090a577fe96dc9` |
| H04c | 357–362 | `4210916049382fe09fcbda05c9cd64ec07239ac7c63712ad2984d72346f4afb6` |
| H05 | 431–437 | `39abb8f840715f2526d486b674349d42096a544b33e66467b873a73e5fc10ed4` |
| H06 | 541–557 | `4a7ccaf52791983edc47ea59e2784b21e83d783fa6d36f3c4528ec3e332cd8ae` |
| H07a | 577–578 | `57caef4f2f22451fada6cb24fc6e26e347f5755a699d5d320c04cf5a6fe09d8e` |
| H07b | 598–641 | `512b8d0a3d081c2f2b9bb2eded10d7d550736ce06fc7a6256a6f036b3d275815` |
| H07c | 643–655 | `f447a217fe422cb22f84cd232d2ea4edad26c07e20970472c404147cf7ae1a0f` |
| H08a | 670–686 | `d96bd26967b3d8503e2c962895d8a53440fe530b03c46312a4c9b6fba9b4a092` |
| H08b | 706–712 | `0be3a5c8765875937e73396ca25f170d2b839b44d6c8762350643ef74023fcd5` |
| H08c | 759–761 | `641c3f5e9051630b132f3175c2e0c58589efe23e31c9c7ef0db3b06aaaeff30e` |
| H08d | 763–815 | `abd186fbed2e23a338584862ddfbb7c3903558abbc8c77abe2e29ffc7868159b` |
| H09a | 837–841 | `69c796cb9c413d7bcb852f85bc0c7920bfafd6e6b098eb9253380fbcff6cc8b0` |
| H09b | 864–866 | `d1180ae4bd053b988d3190b5b233a94a68bbc1f84899f9b9750af8269f5a5e96` |
| H09c | 879–880 | `4e0ecea92af673d846d79a834ba6fc65832572448a3a5ed306ad389a8d216c19` |
| H10 | 891–1246 | `a7b6ee3f9cdb7c4b1e0eefc8d1c0f51b5f7b1952325204cf67a2aa8dcd3dbc2a` |
| H11 | 1264–1270 | `9bc62208c640159db8769d6a76f2c0290548c47c1934897f6041d189af638074` |
| H12 | 1306–1315 | `45131183bded9bfa4125dbcf21daef04e0ec3184a1de6fea169604c27b1293c2` |

### C의 명시적 규범 변경

B 검증 이후 raw 직접커밋/ignore 금지 방침을 Release ZIP 정책으로 명시적으로 교체했다. 이를 날짜 이력만 옮긴 것으로 주장하지 않는다. 교체된 실제 B 규칙 공유/ignore 블록은 H13으로 verbatim 보존했다. H13 SHA256: `54a93731ce168278cf0658a976460ff64b3f1576de2b0c45fcb7f52c8cde8bbd`.

현행 §1.10은 월별 Release 제목 `로그 보관 YYYY-MM`, 새 ZIP `YYYY-MM-DD_HH_MM_작업명_회차.zip`, 일회성 `YYYY-MM-DD_기존로그.zip`(큰 경우에만 독립ZIP `_partNN`)을 규정한다. 내부경로/hash·개수·download검증·빌드상태·중복포함 방지, 업로드 검증 뒤에도 별도 로컬정리 승인, 자동remote/local삭제 금지를 유지한다. §1.11 임시파일 보존, §1.14#10 및 2절 capture tree의 보존 표현만 §1.10으로 정합화했다. 로그 코드 cleanup 의무·capture 위치·기존 수동 메뉴는 바꾸지 않았다.

Main 중간 검토 후 규칙에서 B/C GO·이번 미실행·ignore 대기 및 진행 Task 하드링크를 제거했다. 규칙은 반복 적용하는 현행 절차/사용자 실행권한만 담는다. 이번 실행 상태는 이 Task가 기록한다.

### WORKFLOW exact 추가 블록

기존 `Log.md 운영 규칙` 참조 뒤에 아래 한 문단만 추가했다. 그 외 workflow/TCQA/native 검증·사용자 빌드완료 경계는 변경하지 않았다.

> 현행 런타임 로그·보관 정책은 [LogRules.md](../../../LogRules.md), 과거 개정·검증 원문은 [LogHistory.md](../../../LogHistory.md)를 참조한다. 이력의 PASS는 새 실행 증거가 아니다.

WORKFLOW 실제 원문 링크 target은 같은 디렉터리 기준 `(LogRules.md)` 및 `(LogHistory.md)`이다. 위 인용의 Task 상대 경로는 도달 가능성을 위한 표시이며 actual 변경 문자열은 다음과 같다:

```text
현행 런타임 로그·보관 정책은 [LogRules.md](LogRules.md), 과거 개정·검증 원문은 [LogHistory.md](LogHistory.md)를 참조한다. 이력의 PASS는 새 실행 증거가 아니다.
```

### 실행 제외 및 최종 검사

실제 ZIP·Git·업로드·삭제·.gitignore 편집·코드/빌드/CU/TCQA 없음. .gitignore 정확 pattern 제안은 Plan 최신 절에만 기록한다. 상태3문서 및 기존 shared/role memory를 최소 포인터로 동기화했다. Baseline 실제 runtime 갱신 exit0: document-manager 1204→1213행(+9), 파일2→2, 감소 없음. 최종 checker 결과는 실제 종료 확인 후 아래 추가한다.


현행 로그 의무를 쉽게 찾도록 정리하고, 과거 구현·검증 기록은 원문 그대로 별도 이력 문서에 보존하려는 작업입니다. 이번 단계에서는 분류와 계획만 작성합니다. Main의 Plan GO 전에는 LogRules, WORKFLOW 또는 이력 원문을 변경하지 않습니다.

- 작성 시각: 2026-10-12 01:41 KST (확인한 UTC 2026-10-11 16:41:45).
- 승인 범위: 이 새 Task의 Research/Plan 작성. 실제 문서 정리는 Main 검토/GO 대기입니다.
- 조사 원본: `Assets/_Project/Docs/LogRules.md` 전체를 연속 구간으로 읽었습니다. 코드·대용량 raw 로그 재감사 또는 최신 구현 상태 검증은 하지 않았습니다.
- 기존 wave Task는 client clock 기술 계획 전달 후 별도로 갱신합니다. 기존 Task, Git, 업로드, 삭제, 빌드, TC/QA는 이번 작업에서 제외합니다.

## 1. 분류 원칙

| 분류 | 판단 기준 | GO 후 처리 |
|---|---|---|
| C: 현행 의무·예외 | 지금 해야 하거나 금지하는 동작, 적용 범위 | LogRules 유지 |
| R: 필요한 근거 | 규범을 잘못 적용하지 않게 하는 이유·반례·증거 경계 | 의미를 보존해 LogRules에 간결하게 유지 |
| H: 날짜별 이력 | 당시 구현 상태, 파일/로그 수, PASS·미실행, 변경 전 상태 | LogHistory에 원문 그대로 이관 |
| M: 혼합 | 한 블록에 C/R/H가 함께 존재 | 문장 단위로 매핑; 규범을 이력으로만 보내지 않음 |

날짜가 붙었다는 이유만으로 이력으로 분류하지 않습니다. 불명확한 문장은 삭제·이관하지 않고 규칙에 남겨 검토합니다. 과거 PASS는 현행 권위나 최신 실행 증거가 아닙니다.

## 2. 절별 규범 보존 mapping

아래 번호 및 기존 제목/헤더 앵커는 유지합니다. 표는 실행 전 보존 목록이며 새 규칙 원본이 아닙니다.

| 원본 절 | LogRules에 남길 C/R | 이력으로 분리 가능한 H |
|---|---|---|
| 1.1 목적 | 디버깅과 운영 관측 목적; 서버 수집은 미래 범위 | 날짜별 구현 완료 주장만 분리 |
| 1.2 두 축 — 심각도와 존속 | Error/Warn/Info와 Ops/Dev/Temp 독립 분류, 운영 필요성 및 예외 사례 | 당시 코드 위치·미처리 상태 |
| 1.3 분류 원칙 | thrower 대신 실제 원인 보유 최종 catch 1회, UI 통지 Dev와 서버 거부 Ops 예외, 삼킨 예외 Ops, 명시된 우선순위·미확정 충돌 경계 | 2026-08-13 적용 3사례와 과거 205건 감사 상태 |
| 1.4 형식 | 시간/LEVEL/System/메시지/key=value, 값의 쉼표 구분 금지, 기능별 System, 헤더 필수·선택 항목과 빈 줄 경계 | 당시 카테고리 개수·Role/폴더 수정 경위·구현 상태 |
| 1.5 이벤트 키 | Ops enum, PascalCase 식별자, 안정적인 사건 이름, Exception/Error 구별 | 키 개수 36→37 등 당시 변경·커밋 |
| 1.6 민감 데이터 | 이메일·토큰·암호 금지, UID/PlayerId 해시, 호출부+facade 2단계 보호, Editor 동일 적용 | 날짜별 적용 개수 |
| 1.7 릴리스 스트리핑 | Dev의 두 Conditional OR, 호출·인자 평가 제거, Ops 비제거, block-body lambda 요구 | 과거 적용·검사 결과 |
| 1.8 싱크 구조 | GameLog/ILogSink, 단일 LogSessionOwner, bootstrap Awake 첫 초기화·멱등성, scene 간 존속, quitting 종료, 개별 sink 예외 격리/무싱크 fallback | 이전 owner·73건 등의 구현 이력 |
| 1.9 예외/엔진 오류 | exception 객체/stack 보존, Exception/Error hook 구별, 4개 재귀 방어, finally 복원, hook 전용 1초/32개 제한과 Suppressed 하한 의미 | 3→4 방어 변경 당시 상태와 실행/미실행 기록 |
| 1.10 Editor 파일 | 일별 append/AutoFlush/세션 헤더/Role, 현행 선택 로그 보존·커밋 조건; 미선택 로그 수동 정리의 2단계 확인·30/90일·오늘/비날짜 제외·절대경로 범위 방어 | 2026-08-17 메뉴 검증 및 당시 크기·파일 수 |
| 1.11 RuntimeLogger | 재사용 파일 writer, Temp 직접 호출 허용과 민감정보 보호, 코드 정리와 파일 보존 분리, 직접 경로 재귀 방어, BeginSession 목적 | 시그니처 변경 당시 상태 |
| 1.12 Device | Android Logcat 수집 순서, 원본 threadtime 보존, 헤더, 덮어쓰기 금지/빈 파일 금지/기기 선택, 현행 저장 경로 | 파일명 변경 날짜별 경위 |
| 1.13 현행 코드 차이 | 아래 아키텍처·범위 규범과 제한을 명시적으로 남김 | 08-17/18/19/20/24 구현 수·상태 표·실행 결과·PASS/미실행 |
| 1.14 금지 | raw Debug.Log, 축 누락, 무의미 메시지, 개인정보, 수준 오용, Ops 키 누락/Conditional, 프레임·틱 스팸, 실제 호출경로 중복, Temp 코드 방치의 10개 금지 | 날짜별 중복 로그 교정 사례 |
| 2. QA-Fix Log.md | Task 대응 경로, QA/DEV 작성 역할, round append/기존 기록 불변, DEV 선기입, 최종 TC 판정, raw 경로·줄 인용 및 템플릿 | 날짜별 파일명 변경 설명만 분리 가능 |

### 1.13에서 놓치면 안 되는 규범

- `Application/Interfaces/ILogSink.cs`, `Application/GameLog.cs`, `Infrastructure/Debug/{ConsoleSink,FileSink,LogSessionOwner}.cs` 배치와 Infrastructure→Application 의존 방향. Application의 Infrastructure 역참조 금지.
- Login/Game bootstrap의 Awake 첫 EnsureInitialized, 단일 멱등 owner, Application.quitting 종료. 씬 OnDestroy에서 세션 종료하지 않음.
- Editor FileSink와 Build ConsoleSink의 모드별 단일 사용으로 이중 콘솔 방지.
- Lobby 직접 진입은 명시된 비적용 범위이며 이번 작업에서 임의 지원하거나 결함 해결로 표기하지 않음.
- Role 기록은 NetworkContext.Set 뒤 양 peer에 적용하는 계약. 예외 안전·목적 fallback 및 append/AutoFlush 계약 유지.
- 로거 자체 hook/console fallback의 재귀 방어 필요성을 보존. 과거 Assets/Editor 잔여 16건은 미적용 당시 상태이지 1.14에 대한 새 예외가 아님.
- 오류 미관측은 hook 자연 실행 증거가 아니며, 미실행·발생 조건 없음·실행 성공을 구별하는 근거를 남김.

## 3. 이관 후보 블록 inventory

대상 문서는 향후 `Assets/_Project/Docs/LogHistory.md`입니다. 아직 생성하지 않았습니다. 아래 ID는 향후 명시적 앵커/출처 wrapper에 사용할 계획입니다.

| ID | 원본 헤더 또는 식별 구간 | 보존 방법 |
|---|---|---|
| H01 | 상단 2026-08-17 파일명 정정 및 개정 날짜 표 | 이전 명칭/취소선/표 원문 보존; 기존 파일·링크 유효성과 미래 명명 규범은 규칙에 유지 |
| H02 | 이 문서를 왜 개정했는가 / 2026-08-13 개정 — 한 축에서 두 축으로 | 당시 구현 상태와 경위 원문 보존; 두 축 필요성은 규칙에 유지 |
| H03 | 1.3 적용 사례 및 1.4 카테고리/Role·폴더 수정 기록 | 원문 표/정정 보존, 실제 catch·헤더 규범은 남김 |
| H04 | 1.5–1.9 날짜별 키/개수/owner/hook 방어 변경 기록 | 규범과 분리된 상태·변경 전 설명을 원문 보존 |
| H05 | 1.10 수동 정리 메뉴 검증 표 및 당시 로그 크기 설명 | 당시 테스트 경계 포함 원문 보존; 수동·경로·날짜 방어는 남김 |
| H06 | 1.11–1.12 시그니처·파일명 변경 경위 | 원문 보존; 현재 API/저장·수집 계약은 남김 |
| H07 | 1.13 2026-08-17/18/19/20/24 기록, 코드 상태 표, 미조치 해결 방안 구현 기록 | PASS·미실행·관측 범위·개수 모두 원문 보존; 아키텍처는 이관 전 추출하여 규칙에 남김 |
| H08 | 1.14 중복 교정 사례 및 2절 파일명 정정 기록 | 원문 보존; 실제 호출경로 판단과 기존 파일 유효성은 남김 |

이관 직전 원본 헤더+실제 줄 범위+블록 hash를 기록해 각 블록의 동일성을 확인합니다. 지금은 줄 범위를 추정하지 않습니다. 원문 안의 상대 링크는 원문을 바꾸지 않고 별도의 활성 링크 안내를 붙여 새 위치에서도 근거에 도달하도록 합니다.

## 4. 후속 Release ZIP 정책과 현행 규칙의 충돌

사용자가 승인한 저장 정책 방향은 문서 정리 검증 이후 단계입니다. 현재 1.10의 선택 raw 로그 Git 보존/ignore 금지 및 raw 영구 보존 표현과 저장 방식 변경이 맞닿습니다. 이를 과거 이력으로 치부하지 않고, 후속 단계에서 명시적으로 대체될 현행 조항으로 매핑해야 합니다. 캡처 방식, 분석 근거 보존, Log.md/Task Git 존속과 미선택 Editor 수동 정리 계약은 별개로 유지합니다.

월별 Release `YYYY-MM`, 테스트별 `YYYY-MM-DD_HH_MM_task_round.zip`, 기존 로그 1회 `YYYY-MM-DD_기존로그_partNN.zip` 독립 ZIP, 내부 상대 경로 보존, 중복 보관 방지, hash·다운로드·파일 수·빌드 상태 metadata를 계획에 포함합니다. 업로드 검증과 로컬 정리 승인은 별도이며 자동 원격/아카이브 삭제는 허용하지 않습니다.

## 5. 변경 파일과 현재 상태

- 지금 생성: 이 Task의 Research.md, Plan.md만.
- Main GO 후 제안: LogRules.md, 새 LogHistory.md, WORKFLOW.md 최소 교차 참조, AGENTS.md 이력 인덱스.
- 정리 실제 완료 후에만: 관련 현재 상태/메모리 최소 포인터. 과거 Task 수정 없음.
- 후속 정책 단계의 `.gitignore`는 정확한 raw 경로/패턴 검토·GO 전까지 제안만 유지. `_Logs` 전체 ignore 금지.
- 실제 이관/규칙 변경/아카이브: 미실행. 문서 검사 결과는 실행 출력 확인 후 별도 인계하며 미완료를 0건으로 표기하지 않음.


## 사용자 직접 보고용 — 원문 이동·현행 정책 변경 exact 인계

현재 디스크 실제 측정: **LogRules 56,980 bytes / 793행**, **LogHistory 98,953 bytes / 971행**. Byte는 FileInfo.Length, 행수는 ReadAllLines 기준이다. 기존 “약129KB”는 이전 개략치이며 정확 측정값으로 소급 표기하지 않는다. 목적 첫 문단을 제목 뒤에 두고 완료 결과는 아래로 배치했다.

### 원문 이동 항목

원본 행·동일 hash는 위 mapping 및 History wrapper가 근거다. H01 명명 정정, H02 개정 표, H03a–c 분류·감사 사례, H04a–c 형식/카테고리/Role 개정, H05 키 개정, H06 owner 변경, H07a–c 엔진오류/hook 변경, H08a–d 상시 기록·경로·용량·수동 메뉴 검증, H09a–c 세션/Logcat 변경, H10 날짜별 코드 상태, H11 중복 호출 당시 사례, H12 파일명 정정을 원문 보존했다. 혼합 블록의 현행 의무는 원래 절에 남겼으며 특히 H10은 §1.13 아키텍처 삭제가 아니다. H13은 승인된 직접커밋/ignore 정책 교체의 이전 원문이다. 모든24블록 같은 hash이며 단순 이력 이동과 현재 정책 변경을 구별한다.

### §1.10 정책 exact 본문

```markdown
### 원시 테스트 로그 공유·보관

원시 테스트 로그는 월별 Release ZIP에 보관합니다. 이전 직접 커밋·ignore 금지 정책 원문은 [LogHistory H13](LogHistory.md#h13)에 보존합니다. 보관 절차의 정의와 실제 ZIP·Git·업로드·삭제 실행 권한은 구분하며, 실행에는 해당 범위의 사용자 승인이 필요합니다.

- 월별 Release 제목은 **`로그 보관 YYYY-MM`**입니다. 새 테스트 ZIP은 **`YYYY-MM-DD_HH_MM_작업명_회차.zip`**으로 구별합니다.
- 기존 로그 일회성 보관은 **`YYYY-MM-DD_기존로그.zip`**입니다. 크기 때문에 나눌 필요가 있을 때만 `_part01`, `_part02` suffix를 붙이며 각 파일은 **독립적으로 해제 가능한 ZIP**입니다. 분할 스트림이 아닙니다.
- 내부 경로는 원래 repository-relative 경로를 보존합니다. 선택한 Editor daily는 캡처 시점의 snapshot임을 기록하며 append 중인 원본의 기록 시점·크기·hash를 구분합니다. 포맷/내용을 재작성하지 않습니다.
- 새 회차는 기존 보관 manifest의 경로·hash와 대조해 이미 보관한 동일 로그를 반복 포함하지 않습니다. 동일 경로의 새 내용은 snapshot 시각/hash로 구별합니다.
- manifest에는 선택 원본 경로, 파일 수·크기·개별 SHA256, ZIP 크기·SHA256, Release/다운로드 위치, 세션·역할(확인된 경우), **빌드 상태와 미확정 경계**를 기록합니다. 빌드 시작을 완료로, 과거 PASS를 새 실행으로 승격하지 않습니다.
- 업로드 완료 응답만으로 보관 성공을 판정하지 않습니다. **실제 다운로드 ZIP hash, 해제/내부 경로·파일 수·개별 hash를 검증**한 뒤 보관 확인을 기록합니다. 로컬 정리는 그 뒤에도 **대상별 사용자 승인**이 필요합니다.
- 로컬·remote archive를 자동/주기 삭제하지 않습니다. 선택하지 않은 소모품 Editor 로그의 기존 사용자 수동 메뉴 계약은 그대로 유지하며, 보관 대상으로 선택된 파일을 검증/승인 없이 그 메뉴로 지우지 않습니다.
- raw 테스트 로그 공유는 검증된 Release ZIP 방식을 사용합니다. **새 raw direct commit을 공유 방법으로 요구하지 않습니다.** 기존 추적 파일의 제거·추적 해제는 별도 사용자 승인이 필요합니다.
- **Log.md·Task 및 문서/meta 근거는 Git에 유지**합니다. `_Logs/` 전체 ignore 금지입니다. raw만 좁게 분리하는 pattern을 적용하며, 적용 전 확정 경로 검토와 사용자 승인을 받습니다. 로그 문서/meta를 광범위한 제외 패턴에 함께 넣지 않습니다.
```

### §1.10 표·§1.11·§1.14#10·2절 exact 변경 줄

```markdown
| **공유** | 선택 raw 테스트 로그는 월별 Release의 독립 ZIP으로 검증·보관한다 (아래) |
| **`.gitignore`** | raw만 좁은 경로로 분리하는 정책; 전체 `_Logs/` 제외 금지. 실제 pattern 적용은 별도 승인 (아래) |
단, `임시` 로그에도 **민감 데이터 규정(1.6)은 그대로 적용된다.** 선택된 원시 증거는 1.10의 검증된 Release 보관 대상으로 남으며, 업로드 검증과 별도 로컬 정리 승인은 구분한다.
10. **`임시` 로그 코드를 남긴 채 작업 종료 금지** — 코드는 제거하되 원시 증거는 **1.10의 검증된 Release 보관 계약**으로 보존한다. 업로드/다운로드 검증과 별도 사용자 승인 전 로컬 원본을 지우지 않는다.
│ ├── HH_MM_[작업명]/                   ← _Tasks와 동일 경로명; raw 보관은 1.10, Log.md는 Git 유지
│ └── HH_mm_logcat/                     ← 실기기 Logcat 캡처 위치; raw 보관은 1.10
```

§1.6의 유출 경로 설명은 “로그 파일은 개발자 간 공유되고, 채팅에 인용되며 Release에 보관된다. 기존 리포지토리 로그도 있어 유출 경로가 여럿이다.”로 정합화했다. 민감 데이터 금지는 그대로다.

### WORKFLOW exact 추가 1문단

```markdown
현행 런타임 로그·보관 정책은 [LogRules.md](LogRules.md), 과거 개정·검증 원문은 [LogHistory.md](LogHistory.md)를 참조한다. 이력의 PASS는 새 실행 증거가 아니다.
```

이 한 문단 외 workflow 변경은 없다. .gitignore·upload·delete 실행은 없다. Code final/native/build 인계 이후 wave/state/programmer memory 후속은 별도 최종 증거를 기다리며 새 코드 PASS를 기록하지 않는다.

목적 첫 문단 보완 후 fresh checker49242 실제 종료 **exit0·문제0**. 범위 규칙10/Markdown78/Claude40, _Tasks/_Logs 제외. 별도 Task 검사: 목적 첫 문단 true, 활성 파일 링크0, §1.10/§1.11/§1.14/2절 및 WORKFLOW exact 인계 항목 존재 확인. 이번 보완은 이 Research만 변경했으므로 기존 LogRules/LogHistory·다른 최종 manifest hash 및 memory baseline은 그대로다.

