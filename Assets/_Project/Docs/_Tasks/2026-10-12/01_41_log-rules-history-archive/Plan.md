# LogRules 정리 및 후속 저장 정책 계획

원시 로그를 검증 가능한 archive로 보관한 뒤 main 최종 tree에서 RuntimeLog txt 본문만 추적 해제하되, 로컬 원본·Git 이력과 문서/metadata는 보존한다. 인증·보관 공개범위 결정·보관 검증은 서로 다른 gate이며 아래 영수증은 실행 완료 선언이 아니다.

## 2026-10-12 최종 실제 영수증 — source/main B 원격 동기화 완료

Main 실제 실행 인계: A `b65751c604fabd6b7dd410907ea8f152f51d33f4`에 사용자 전체 작업445파일(nonraw322 + 승인 raw cache 삭제123)을 commit하고 source branch에 push했다. 최신 main9e1c596d를 ort merge한 B **`befdd8ef37f3b064f31d550fd96192c93d379ad2`**는 conflict0·tree(B)=tree(A) true다. 실제 atomic push 성공 후 remote 확인으로 **origin/main과 origin/codex/unit-movement-attack-sync-audit 모두 B**임을 Main이 검증했다. 아래 commit/merge/main 대기·Resources 보류 문장은 당시 이력이며 이 완료 영수증으로 supersede한다.

실제 post-merge compile: Runtime288 exit0/errors0/warnings20, Editor32 exit0/errors0/warnings0, Development288 exit0/errors0/warnings20; Project320 complete·Bee 누락/drift0. Static warnings는 obsolete19 + unused1이며 native/빌드는 재실행하지 않았다. Main fresh 문서 검사0(규칙10/Markdown78/Claude40, Task/Logs 제외)도 인계받았다. 문서 담당의 Git/API/소스/Unity 재검증 결과로 표기하지 않는다.

Private archive ZIP·manifest·verification receipt 업로드 및 전체169 path/CRC/size/SHA 검증은 아래 기존 영수증 그대로 완료. 실제 main committed raw0·Temp DLL tracked0, QA Log 문서10·CrossAuditResult 보존, 원래 Resources의 UnitStats/UnitEffect/Building Config3 존재. 로컬 raw169는 삭제하지 않았고 untracked는 기존 root DLL2만 로컬 보존했다. Git 이력 재작성 없음.

이번 최종 문서 patch는 Research.md·Plan.md·PROJECT_STATUS.md 관련1줄·공용 MEMORY.md 기존 custody 항목1줄, **총4파일**뿐이다. 소스·LogRules·WORKFLOW·다른 과거 Task·baseline은 변경하지 않는다. **영수증 작성 시점에는** 문서 commit C 및 C의 branch/main FF push가 미실행이었다. 자기 SHA 순환을 기록하지 않고 이 문서 반영 상태는 해당 Git commit/remote refs로 확인한다.

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

- 원본 RuntimeLog **169개 / 2,136,923,327 bytes**. Programmer 최종 inventory 인계: 해시 읽기 중 변경0, fingerprint `93fe4221a617ee639aab3086d61e98a4a390178e19ba6ab5c37769c504fa7bbc`. Fingerprint 산식은 이번 인계에 없으므로 Research의 stat TSV 등록 hash와 같은 검사값으로 취급하거나 비교하지 않는다.
- GitHub repo API probe **401**, 사용자 login 갱신 대기. 인증값 출력·저장 없음이라는 Main 인계이며 이 문서에 인증값을 복사하지 않았다.
- **ZIP 미생성 / 업로드·download verification 미실행 / untrack 미실행**. 검증된 업로드와 실제 download 확인 전 untrack gate는 닫혀 있다. 로컬 raw는 그대로 보존한다.
- Main의 **Git 잠금 제거 성공** 인계: lock 내용이 없고 동작 프로세스가 없는 조건을 확인한 뒤 제거했다. 문서 담당이 Git/lock 파일을 읽거나 제거한 결과가 아니며 이전 stale 후보 대기 기록을 supersede한다.
- `Tools/archive_runtime_logs.py`는 **계획·설계뿐, create/구현 없음**. 예정 ZIP은 `Temp/log-archive/2026-10-12_기존로그.zip`; compressed size 실제 확인 뒤 큰 경우에만 독립 parts를 검토한다. 본 문서 작업에서 Tool/Temp/ZIP을 생성하지 않았다.
- Raw txt.meta 및 실제 코드·assets·doc/folder metadata는 §1.10에 따라 Git 보존한다. CrossAuditResult.txt(943 bytes, SHA256 `739F47FF2458AF5EB2AAA9B21B0E90839469D3C63B1DB24A6979CF54977EE7D9`)도 runtime archive/untrack 범위에서 제외하여 Git 유지한다. 전체 txt/_Logs ignore·history rewrite·로컬/remote 삭제 없음.

이번 영수증 직접 수정 범위는 **이 기존 Task Research/Plan2개만**이다. 상태·메모리·규칙·다른 Task를 갱신하지 않고 추가 조사도 중단한다. 아래 이전 단계 기록은 삭제하지 않는다. 최신 문서 검사 결과는 실제 종료 확인 뒤 인계한다.

최종 실제 UTF-8 문서 검사(`-X utf8 Tools/check_docs.py`, session62188): **exit0·문제0**. _Tasks/_Logs 제외 범위를 확인했고 별도 Task2문서 활성 파일 링크0이다. 외부401 차단은 해소되지 않았으며 ZIP/업로드/download 검증/untrack/Tool create는 미실행 상태로 종료한다.



## 2026-10-12 B/C 실행 완료 인계

위 GO 대기/미실행은 최초 계획 시점 이력이다. Main B/C GO 이후 B 원문23블록·규범 보존을 검사한 뒤 C 정책을 문서화했다. 기존 직접커밋/ignore 금지는 명시적 현재 정책 변경으로 H13 원문을 보존했다. 월별 Release 제목은 **로그 보관 YYYY-MM**이며 아래 최초 계획의 이름/part 표기는 최신 LogRules §1.10으로 supersede한다. 기존로그 part suffix는 큰 경우에만 사용하고 독립 ZIP으로 보관한다.

실제 보관 운영 단계 D는 실행하지 않았다. ZIP·Git·upload·delete 및 .gitignore 변경 없음. 기존 추적 파일 처리 권한도 없다. 최초 계획의 정리 문서는 현행 절차이고 실제 운영 승인은 별개다.

### .gitignore 정확 pattern 제안 — 미적용

확정 raw 원본 목록과 사용자 pattern 승인 후에만 적용할 제안이다. 아래 runtime 파일명만 대상으로 하며 Markdown/Task/meta는 제외하지 않는다. 기존 tracked raw의 제거·untracking은 별도 승인 사항이다. 전체 `_Logs/` 또는 전체 `*.txt` 패턴은 쓰지 않는다.

```gitignore
/Assets/_Project/Docs/_Logs/_editor/????-??-??/RuntimeLog.txt
/Assets/_Project/Docs/_Logs/????-??-??/*/RuntimeLog.txt
/Assets/_Project/Docs/_Logs/????-??-??/*/RuntimeLog_device.txt
/Assets/_Project/Docs/_Logs/????-??-??/*/RuntimeLog_host.txt
/Assets/_Project/Docs/_Logs/????-??-??/*/RuntimeLog_client.txt
```

`????-??-??`는 Git glob의 날짜 폴더 위치 제안이며 적용 전 확정 경로 목록이 실제 날짜 디렉터리인지 검토한다. 추가 device 파일명/중첩 경로는 자동 포함하지 않고 별도 manifest 검토한다. Log.md·기타 Markdown·Task·meta는 위 패턴에 매치하지 않으며 Git 유지한다. 검증된 업로드가 생기더라도 자동 로컬/remote 삭제는 허용하지 않는다.

최종 원문 hash·규범 보존·실제 checker 결과 및 manifest는 [Research](Research.md)의 최신 인계에 모은다. 이전 wave/native/빌드 이력은 보존했고 clock code GO만 전달된 상태이며 새 코드 최종 PASS는 없다.


규칙은 규칙으로 남기고, 과거 기록은 문장을 바꾸지 않은 채 별도 이력으로 옮깁니다. 먼저 보존 mapping을 Main이 검토하고 GO한 뒤 문서를 정리합니다. 저장 정책은 그 정리가 검증된 다음 단계이며, 이번에는 아래 계획만 작성합니다.

- 작성: 2026-10-12 01:41 KST.
- 현재 단계: Research/Plan 작성 승인됨, 문서 정리 실행 GO 대기.
- 분류 및 보존 목록: [Research.md](Research.md).
- 제외: oldTasks, Git 명령, raw 로그 재분석, 코드/에셋/빌드/CU, 실제 ZIP 생성·업로드·삭제, TC/QA 및 다른 에이전트 호출.

## 1. 실행 승인 경계

| 단계 | 산출물 | 현재 상태 |
|---|---|---|
| A. 조사·계획 | 이 Task Research/Plan 2개 | 이번 요청의 실행 범위 |
| B. 규범/이력 분리 | LogRules/LogHistory 및 최소 참조 | Main mapping·Plan GO 전 미실행 |
| C. 저장 정책 문서화 | Release ZIP 정책과 좁은 raw 경로 처리안 | B 검증 뒤 진행; 실행 범위 GO 필요 |
| D. 아카이브 운영 | 패키징·업로드 검증·사용자 승인 로컬 정리 | 이번 문서 작성 승인에 포함되지 않음 |

wave client clock 기술 계획은 아직 본 작업에 병합하지 않습니다. 계획이 전달되면 기존 `2026-10-11/21_50_wave-contact-authority-isolation` Task에만 별도 반영합니다.

## 2. Main GO 후 문서 정리 순서

1. 실행 직전 LogRules 변경 여부를 확인하고 Research의 C/R/H/M mapping을 원문 블록별로 확정합니다. 원본 헤더/줄 범위/내용 hash를 이 Task에 기록합니다. 동시 변경이 있으면 기존 조사 내용을 무조건 덮어쓰지 않습니다.
2. 새 `Assets/_Project/Docs/LogHistory.md`에 비권위 이력 안내와 H01–H08 명시적 앵커를 만듭니다. 선정한 이력 블록은 취소선·표·코드·원문 인용·당시 제한까지 verbatim 복사합니다. 요약으로 대체하거나 과거 수치를 고치지 않습니다.
3. 각 복사 블록의 내용 동일성을 확인한 뒤에만 LogRules에서 해당 이력을 분리합니다. 혼합 블록의 C/R은 원래 절에 남깁니다. 원본 내 상대 링크 문자열을 바꾸지 않고 wrapper에 새 위치 기준 활성 근거 링크를 제공합니다. oldTask 파일은 편집하지 않습니다.
4. LogRules 1.1–1.14 번호·제목/기존 앵커와 2절 QA-Fix 계약을 보존합니다. 반복 배경만 간결화하며 의무·예외·적용 범위·필요한 이유를 변경하지 않습니다. 기존 앵커를 제거해야 할 상황이면 진행을 멈추고 mapping을 수정하여 검토받습니다.
5. 특히 1.13을 통째로 이관하지 않습니다. 계층 배치/의존 방향, 단일 owner/종료, sink 모드, 양 peer Role, Lobby 비적용 범위, 자기 로깅 방어와 Editor 잔여 항목의 비예외성은 현행 규범으로 남깁니다.
6. LogRules에서 생성된 LogHistory 앵커로 연결하고, WORKFLOW에는 필요한 교차 참조만 추가합니다. AGENTS에는 LogHistory 인덱스 행만 추가합니다. 원래 workflow, QA/TC 계약, native 검증과 사용자 소유 빌드 완료 경계를 변경하지 않습니다.

## 3. 문서 정리 검증 및 인계

- C/R 목록 전건이 현행 문서에 남았는지, H 전건이 동일 원문으로 보존되었는지 1:1 대조합니다. 블록 개수/원본·목적지 hash와 불명확하여 남긴 항목을 기록합니다.
- 1.3 우선순위·UI 예외, 1.7 lambda, 1.9 네 가지 방어·throttle 경계, 1.10 수동 정리 범위/오늘 제외, 1.13 아키텍처, 1.14 금지 10개와 2절 작성 역할/append 정책을 별도로 확인합니다.
- 이동한 상대 링크와 기존 앵커의 도달 가능성을 확인합니다. 아직 생성하지 않은 미래 파일에는 활성 Markdown 링크를 만들지 않습니다.
- 실제 Python runtime으로 `Tools/check_docs.py`를 읽기 전용 실행합니다. 출력의 검사 범위와 종료 코드를 확인합니다. Task 문서의 상대 링크는 별도로 확인하며 checker만으로 새 Task 내용 검증을 갈음하지 않습니다.
- 완료가 확인되지 않은 검사에는 pending을 기록합니다. 과거 검사 0건을 이번 실행 결과로 재사용하지 않습니다. baseline 직접 수정·임의 갱신은 하지 않습니다.
- 문서 정리가 실제 완료된 후에만 관련 상태/메모리 최소 포인터를 갱신합니다. 새 규칙 사본이나 세션 작업 내용을 메모리 인덱스에 중복 저장하지 않습니다.
- 코드·컴파일·native 실행·빌드 또는 runtime 회복 PASS는 이 문서 검사로 주장하지 않습니다.

## 4. 검증된 정리 이후 Release ZIP 정책 단계

이 절은 승인된 정책 방향을 구체화한 후속안입니다. 정리 단계에서 아래 저장 방식 변경을 몰래 적용하지 않습니다.

### 4.1 보관 단위와 중복 방지

- 월별 Release 식별: `YYYY-MM`. 테스트 raw ZIP: `YYYY-MM-DD_HH_MM_task_round.zip`. 재시도/역할/회차를 manifest로 구별하며 기존 asset을 덮어쓰지 않습니다.
- 기존 raw 로그는 1회 `YYYY-MM-DD_기존로그_partNN.zip`으로 분리합니다. 각 part는 단독 해제 가능한 ZIP이며 분할 압축 스트림이 아닙니다. 과거 날짜별 원래 내부 경로를 유지합니다.
- 내부 경로는 리포지토리 상대 경로 기준을 사용합니다. 캡처 원본을 재포맷/병합하지 않습니다. 계속 append되는 Editor daily 파일은 보관 시점·크기·hash를 고정한 snapshot으로 구별합니다.
- manifest의 원본 경로/hash/범위로 이미 보관한 내용을 식별해 반복 업로드를 방지합니다. 새로운 append 내용과 기존 동일 내용은 구별하여 계획합니다. 현재는 파일 수/용량을 스캔하거나 ZIP을 만들지 않습니다.

### 4.2 검증 metadata와 정리 승인

- ZIP 이름·크기·SHA256, 내부 원본 경로·파일 수·각 파일 크기/hash, 캡처 시간, 알려진 session/role, Task/분석 근거 연결을 기록합니다. 모르는 identity는 추정하지 않습니다.
- Release/asset 다운로드 URL, 검증 날짜, 실제 다운로드 후 ZIP hash 및 내부 파일 목록/개수/내용 hash 일치를 기록합니다. 업로드 응답만으로 보관 성공을 판정하지 않습니다.
- 빌드 상태는 승인/시작 클릭/관측한 단계/완료 확인을 구별합니다. 기존 사용자 소유 완료 정책을 유지하며 자동 완료 감시를 추가하지 않습니다.
- 업로드·다운로드 검증 완료 뒤에도 로컬 raw 정리는 별도의 명시적 승인 대상입니다. 후보 정확한 경로와 보관 대응표를 먼저 제시합니다. 자동 삭제·원격 삭제·아카이브 정리·기존 Release pruning을 도입하지 않습니다.

### 4.3 현행 조항의 명시적 대체와 Git 경계

- 1.10의 선택 raw Git 보존/ignore 금지 및 raw 영구 보존 문구는 현행 규범입니다. 후속 정책에서 해당 조항을 정확히 지목해 검증된 Release 보관으로 대체합니다. 역사적 오기로 처리하지 않습니다.
- Log.md와 Task 문서는 계속 Git 대상입니다. `_Logs` 전체 ignore는 하지 않습니다. 선택 raw만 제외하는 `.gitignore` 경로/패턴은 정확한 허용 목록과 보호 예외를 Main에 제시한 후 GO가 있을 때만 변경합니다.
- 이미 추적된 파일의 Git 처리나 업로드 실행은 이 문서 승인으로 대신하지 않습니다. 별도 권한 없이 Git 명령을 실행하지 않습니다.
- 미선택 Editor disposable 로그의 수동 메뉴 계약은 유지합니다. 보관된 selected 근거를 미선택으로 재분류하여 지우지 않습니다. Device 수집 방식도 저장 정책만으로 변경하지 않습니다.

## 5. 제안 파일 목록

| 시점 | 파일 | 변경 내용 |
|---|---|---|
| 지금 | 이 Task Research.md / Plan.md | 분류·보존 mapping·단계별 계획만 작성 |
| Main GO 후 | `Assets/_Project/Docs/LogHistory.md` | 비권위 안내·출처/활성 링크 wrapper + 이력 원문 |
| Main GO 후 | `Assets/_Project/Docs/LogRules.md` | 현행 규범 보존, 이력 분리, 중복 배경 간결화 |
| Main GO 후 | `Assets/_Project/Docs/WORKFLOW.md` | 필요한 최소 교차 참조만 |
| Main GO 후 | `AGENTS.md` | LogHistory 인덱스만 |
| 실제 완료 후 | 관련 상태/기존 메모리 토픽·인덱스 | 최소 완료 포인터; 이력 삭제/대량 이동 없음 |
| 후속 정책 GO 후 | LogRules 및 필요한 최소 참조, 조건부 `.gitignore` | 저장 정책을 명시적으로 반영; 정확한 경로 승인 전 ignore 편집 없음 |

## 6. 현재 결과

Research/Plan 외에는 변경하지 않았습니다. 이력 이관, 규칙 변경, 저장 정책 적용, ZIP 생성, 업로드, 삭제는 모두 미실행입니다. Main에 규범 보존 mapping을 먼저 인계하고 정리 실행 GO를 기다립니다.
