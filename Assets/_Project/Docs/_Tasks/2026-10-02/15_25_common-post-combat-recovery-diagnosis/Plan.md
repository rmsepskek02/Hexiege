# Common post-combat recovery diagnosis — Plan

## Latest analysis — 17_05 Android Host reproduction (2026-10-02)

최신 실기에서 Torrent24의 약366.5초 정지 후 재개와 공통 복귀 경로의 문제를 확인했습니다. 출력된 진단 기록은 완전하지만, 실제 fallback 결정과 후속 capture에는 공백이 있어 원인 완전 증명·교정 PASS는 아닙니다. 아래 교정 방향과 공격 발사 보존 정책은 아직 구현되지 않은 제안이며, 이전 경기·빌드·파도 보정 근거는 이력으로 보존합니다.

The latest runtime evidence supersedes earlier runtime-pending wording only for this reproduction; all prior evidence and build-attempt history remain preserved below. Phase A instrumentation is implemented, but capture completeness and gameplay cause are separate verdicts. Shared movement and TorrentSpirit Unresolved/Legacy status remain OPEN; no gameplay correction or new permanent rule is implemented/approved. Earlier wave-calibration evidence is not revoked merely because a separate shared movement defect exists.

Source: `Assets/_Project/Docs/_Logs/2026-10-02/17_05_logcat/RuntimeLog_device.txt`, Android Host; recorder session `f927a8c0c889dd4c1857bfd955d06ed9a518149240022f37fe30e6d0b099aff5`. Aggregate counts and the stationary-sample audit below are main's verified final handoff; Sagan verified the 499 stationary-position samples. They are not a count of spatial diagnostic records or duplicates.

### Unit24 long Blocked episode

- TorrentSpirit24 entered Blocked at 16:55:16.886 (raw line3781), then resumed BlockedRepath Align at 17:01:23.421 and Move at 17:01:23.460: approximately366.5 seconds held. Sagan checked499 stationary-position samples during the interval. This was not a permanent stop.
- Provider Success route: `6;16|7;16|8;15|9;14|9;13|9;12`. Its first hop keeps hex distance to the goal at4→4. Capture reports centerAvailableFalse, no spatial stage, and accepted counters0.
- Strong source inference, not a fully runtime-recorded guard decision: the strict-closer forward-center filter excludes a valid sideways first hop. No-center fallback requests the route via SpatialRecoverable; an unchanged route is rejected as RejectedRepeatedPath and the caller marks navigationBlocked. The blocked loop waits for an external pending-path/view-environment event and does not perform target detection there.
- Provider environment12 and View environment0 are separate revision domains. Their numerical difference does not prove broken wiring.
- At 17:01:33.590, after resumption, unit24 had an EmptySameTile completion rejection. This is a separate later outcome, not the initial long-Blocked cause.

### Recovered corridor failures and separate consistency failures

- All61 first-corridor failures have61 planner-accepted, blocked-route-installed and pursuit-target-selected transitions, across11 unit instances. These are recovered episodes, not61 permanent stops.
- Unit45 illustrates a stale-Lerp acquisition seam: acquireTrue with Root `6;14`, candidate `5;15`, OutsideLogicalPath. Source review finds reducer alignment does not update alignElapsed while the acquisition branch validates the old Lerp candidate. This is a shared-code defect/inference, not a Torrent-only special-movement feature.
- Four Fatal preflight lines belong to TorrentSpirit18/39, initial+cleanup for each, with committed `1;16` versus Root `0;17`. Keep this consistency issue separate from unit24's Blocked cause.
- Shared code applies beyond TorrentSpirit, but these runtime cases do not prove the same exact failure in another unit type.

### Recorder completeness versus causal coverage

- Main reassembled4137 chunks into3453 logical records: incomplete0. All62 dumps are complete and report zero diagnostic loss/fault counters.
- This establishes emitted-chunk/dump completeness only. A blocked-only episode with episodeTriggeredFalse hides later captures; the fallback guard decision itself is not logged. Private planner source ordinal remains unavailable. Do not claim complete causal proof, resolved behavior or bug PASS.
- The earlier conservative842-byte static bound remains static evidence; no new blanket runtime byte-bound conclusion is inferred from the count audit.

### Proposed next directions — not implemented

Shared movement proposals: separate valid-route recovery from direct-center selection so a safe detour's sideways hop is not treated as no progress; remove the stale-Lerp candidate source at target acquisition. Preserve safety/authority contracts, with no tolerance widening, snapping, Client writer or retry-budget increase. Evidence-trigger/later-capture and fallback-decision gaps also need an explicit approved implementation step, not silent promotion of the existing recorder to complete causal coverage.

Current Torrent launch behavior: `UnitCombatUseCase.cs:833` checks a live primary before ExecuteAttack, so missing/dead primary before creation skips launch. An alive primary leaving range ALONE is not explicitly cancelled by this launch writer. An already created wave is independent of primary lifetime and applies server-authoritative contact damage/heal.

The user's desired launch-after-started direction is a proposed design only: a server-approved attack launch reservation could retain valid authorized aim independently of primary survival and create exactly one server wave, whose contacts affect eligible alive enemies/allies. Snapshot-point versus last-authorized-aim policy remains a decision needed; do not claim an approved fixed-start direction. Provisional animation is not a server commit. Preserve existing attacker-death/game-end cancellation while that policy remains pending design. No fake Client VFX damage and no retargeting a dead primary.

Target-death/range-loss committed-launch VFX plus server-contact damage/heal instrumentation/behavior requested in addition is NOT implemented in this update. Phase B/launch-policy changes need explicit approval. No code/assets/build/new Task/TC/QA changes are made by this analysis update.

전투 후 복귀가 처음 거부되는 순간의 근거를 확보하는 계획입니다. Phase A 계측 구현·정적 컴파일과 실제 변경 코드의 Unity 메뉴 검증은 완료되었습니다. 승인된 Android 빌드 시도는 서명 비밀번호 사전검사에서 차단되어 시작되지 않았습니다. 실기 증거와 정지 원인은 OPEN이며, 실제 이동 교정은 새 증거와 별도 승인 없이는 진행하지 않습니다.

## 1. Approval and boundaries

- [x] Phase A implementation: bounded server-only Editor/Development recent-state evidence, automatic first-rejection and Blocked-entry capture, and correlated lifecycle follow-up. Instrumented runtime acceptance remains pending.
- No gameplay correction, new reproduction harness, new validator, Testcase.md or QA file now. User play instructions remain in this Plan.
- Do not change wave assets/contracts, retry limits, safety tolerances, movement writers, target selection or callbacks. Defer the LittleKnight/Pistoleer building-rally defect entirely.
- Main handles documentation checking. Implementation/build are subsequent responsibilities, not actions completed by document-manager.

## 2. Phase A implementation items

### A1. Read-only episode recorder

- [x] Add a shared recent-state recorder at existing post-combat movement seams, not a Torrent-only branch. Scope it to the authoritative network server in `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- [x] Correlate unit plus lifecycle identity, commandRevision, segmentRevision, recoveryEpisode, environmentRevision and existing non-secret match/run correlation. Never add authentication/session credentials.
- [x] Use fixed finite ring, tracked-unit and active-episode capacities. Report chosen capacities in implementation handoff; diagnostic eviction must not retire gameplay intent or reset gameplay progress guards.
- [x] Record existing observations in memory without per-frame file writes. Prefer decision/transition samples; any repeated unchanged-state samples remain bounded and memory-only. Diagnostics must not introduce extra pathfinding, corridor probes or world mutations.
- [x] Release builds must omit ring storage, capture work, argument construction and output, not merely hide output.

References: Units `U-MOV-PHASE`, rules 3, 9, 10, 16; synchronization `NET-AUTH-001`, `NET-ROOT-001/002`; LogRules 1.3, 1.6, 1.7, 1.14.

### A2. First rejection and Blocked entry

- [x] Automatically trigger a bounded recent-history dump on the FIRST post-combat corridor rejection per episode, before later provider requests overwrite the first reason. Trigger a distinct Blocked-entry dump once when the episode enters Blocked.
- [x] Include Root world/domain position, sampled tile, committed tile, final goal, center distance; selected rejoin/candidate/source; path signature/count/index and bounded points; provider typed status; planner candidate ordinal, sweeps and context changes; actual guard counters and decision origin. Implementation limitation: private planner source ordinal is explicitly unavailable, not reconstructed.
- [x] Include the original corridor status/offending tile/index, available tile/walkability facts and staging/acceptance result. Record `unavailable` with reason for facts not already computed. Do not perform extra A* or safety queries merely to fill diagnostic fields.
- [x] Preserve the distinction between `EmptySameTile`, invalid provider input, unreachable candidate, repeated path, frame-budget defer, real no-progress exhaustion and planner-confirmed Blocked. A manual diagnostic label must never impersonate a guard decision.
- [x] Preserve existing production behavior and the existing same-tile forward-rejoin provision. The evidence should reveal whether fallback/consumer handling differs, not implement an assumed fix.

References: Units `U-MOV-ALIGN` item 11, `U-MOV-REPATH` items 1–7, rules 2, 4, 11, 13; synchronization `NET-FACING-001`, `NET-ACTION-STATE`; LogRules 1.3, 1.4, 1.14.

### A3. Bounded lifecycle evidence and output

- [x] Retain episode correlation through unblock/repath acceptance, progression/center arrival, completion, reengagement, death, cancellation and lifecycle retirement; report an open episode as open at shutdown rather than fabricate completion.
- [x] Expose capture/dump counts, capacity, history truncation, overflow, eviction and unfinished episodes in bounded summaries. Mark incomplete evidence explicitly; absence of a captured failure with dropped history is not evidence of successful recovery.
- [x] Emit through existing `GameLog.Dev` and existing sinks: Info for capture/transition evidence, Warn for diagnostic incompleteness. Preserve existing operational failures; no raw Debug.Log, additional sink or global hook changes.
- [x] Use keyed bounded chunks with episode/event/part/parts identity. Adopt a task output ceiling of 900 UTF-8 bytes per full line including standard prefix; verify actual emitted formatting, not character counts. This is a task engineering limit, not a newly invented permanent rule. Main reports a conservative full-line maximum of 842 UTF-8 bytes; Android runtime capture verification remains pending.
- [x] Reject oversized/incomplete formatting visibly with a short keyed bounded fallback. Never silently truncate decisive fields or recurse while reporting failure. Keep diagnostic counters independent of movement state.
- [ ] Retain Dev diagnostics until task closure; preserve collected logs under existing LogRules paths.

References: Units `U-MOV-REPATH` items 6–9, rules 13, 22; synchronization `NET-ACTION-STATE`, `NET-CANCEL-004`, `NET-TIME-001`; LogRules 1.2–1.4, 1.7–1.10, 1.12, 1.14.

## 3. Integration sanity and build handoff

- [x] Compile the changed runtime and Editor code through the ordinary project workflow; check Editor/Development conditional paths and release exclusion. Do not create a validator or reproduction harness for this task. Static compile handoff: runtime/release/development/editor exit0, errors0; warnings20/24/20/0 respectively. Actual Unity refresh/domain reload completed at 16:04:11; subsequent changed-assembly menu verification PASS, as recorded below.
- [x] Run existing menus `Hexiege > Combat > Run Unit Action Self Validation` and `Hexiege > Combat > Diagnostics > Self Validate Unit Root Pose Cross Audit` as integration sanity checks. Main reports actual changed-assembly PASS at 16:05:41 (all subsections) and 16:07:03 after refresh/domain reload completed16:04:11. GUI/latest Editor.log evidence; Unity Console errors0. These menus do not prove that Torrent125 recovered or that a gameplay defect was corrected.
- [x] Inspect fixed capacities, output UTF-8 bounds and observer-only behavior within ordinary implementation verification. No new TC/QA artifact.
- [x] User-approved Android build attempt: File > Build And Run clicked approximately16:08; signing-password preflight blocked the attempt with a Unity modal. BUILD NOT STARTED; no completion claim or completion monitoring. User handles signing passwords locally and retries. No signing/configuration changes or password search.

References: Units rules 2, 3, 9, 10, 22; synchronization `NET-AUTH-001`, `NET-ROOT-001/002`; LogRules 1.7, 1.10, 1.12, 1.14; WORKFLOW verification reporting.

## 4. User ordinary-play evidence collection

- [ ] Clear the Android logcat buffer through `Hexiege > Logcat > 1. 버퍼 비우기` before the match, then save with `Hexiege > Logcat > 2. 파일로 저장` afterward. Preserve the Editor daily RuntimeLog.txt and match role/correlation evidence.
- [ ] Play ordinary TorrentSpirit combat, allow a target to die, and observe whether the unit returns to its movement objective, reengages, waits or stays stopped. Include ordinary melee and ranged units using the shared recovery path; no forced harness or special debug state setup now.
- [ ] If stopped, note unit type/team and approximate on-screen time; correlate exact unit/command/segment/episode in logs rather than infer unit identity from a stationary pose alone.
- [ ] Check first rejection, candidates/provider statuses, Blocked entry and eventual exit/lifecycle evidence. If no rejection occurs, report no recurrence in that run, not bug PASS. Missing first-reason history, overflow or eviction makes causal evidence incomplete.
- [ ] Keep separate findings for instrumentation completeness, observed movement behavior, and unresolved cause. Neither local ROOT PASS nor compile/menu success proves normal objective progress.

References: Units `U-MOV-ALIGN`, `U-MOV-REPATH`, rules 8, 9, 11, 13, 16; synchronization `NET-ACTION-STATE`, `NET-FACING-001`; LogRules 1.10, 1.12, 1.14.

## 5. Phase B — conditional, not currently approved

After reviewing captured first-rejection evidence, request explicit approval for the smallest supported shared correction. Possible branches include preserving typed same-tile outcomes through fallback/consumer seams, safe center-alignment versus actual completion, staged route/corridor acceptance consistency, or correct waiting/unblock lifecycle handling. Existing same-tile rejoin behavior must be accounted for before selecting a change.

Do not snap Root, add a Client writer, bypass corridor/walkability validation, increase retry budgets, or declare a merely empty same-tile path unreachable. After cause evidence and approval, define targeted state-seam regression coverage for the selected correction plus ordinary Torrent/melee/ranged play. That later coverage is not authorization to add a harness/validator/TC now.

References: Units `U-MOV-ALIGN`, `U-MOV-REPATH`, rules 2, 3, 4, 9, 11, 13; synchronization `NET-ROOT-001/002`, `NET-FACING-001`, `NET-ACTION-STATE`.

## 6. Completion reporting

Phase A handoff must list changed implementation files, chosen capacities, trigger/episode ownership, output limits, observed compile/menu results, build-attempt state and unresolved evidence gaps. Report instrumentation ready separately from gameplay recovery. Current verdict remains OPEN; no correction or fix PASS is claimed.

## 7. Historical implementation handoff (2026-10-02, before final menu/build results)

Initial document-handoff wording retained as history: "아래 항목은 구현·검증 인계이며 아직 완료 결과가 아닙니다." The introduction and section 3 now show the final current results. Phase A implementation is reported complete by main in UnitView.cs only; schema `pc-recovery-v1`, 512 tracked views / 32 samples / 256 points per path. First-failure capture precedes planner execution; Blocked entry is recorded separately. Normal captures are decision-only, provider scalars are immutable, and cached spatial/provider sourceFrame/time remain distinct from current capture time. No extra queries or gameplay changes; release capture/storage excluded.

Checked implementation items above reflect main's completion handoff, not new runtime acceptance. Private planner source ordinal remains unavailable. Conservative full-line UTF-8 maximum842 bytes is static evidence only. Dev diagnostics remain retained; collection and gameplay verdicts remain OPEN.

Unity manual refresh/domain reload is in progress. Actual menu results on the changed assembly remain pending; Android build is not yet attempted. Keep section 3 menu/build items and section 4 user-play items unchecked until actual results arrive. Phase B remains unapproved; no bug-fix PASS.

## 8. Final integration verification and blocked build attempt (2026-10-02)

This final handoff supersedes earlier menu/build-pending statements in sections 1 and 7, which are retained as dated handoff history. Main supplied the following actual changed-code Unity results, supported by GUI observations and exact latest Editor.log entries:

- Manual Assets Refresh and domain reload finished at 16:04:11.
- Run Unit Action Self Validation: PASS, all subsections, at 16:05:41.
- Self Validate Unit Root Pose Cross Audit: PASS at 16:07:03.
- Unity showed existing compiler warnings20 followed by validation warnings5; Unity Console errors0. These are distinct from the configuration-specific static compile warning counts above.
- These results are integration sanity evidence only, not proof of recovery, first-rejection cause or bug correction.

At approximately 16:08, main clicked File > Build And Run using the existing Development Build / AAB / Samsung settings. Unity displayed "Can not sign the application" and "Unable to sign the application; please provide passwords!" with an OK control. Signing-password preflight blocked the attempt: BUILD NOT STARTED. This is not a failed completion of an already running build, and no build completion is claimed.

No signing/configuration changes or password search were performed. The Unity dialog is left for the user to handle locally; the user can provide signing passwords locally and retry. No completion monitoring is performed.

Phase A instrumentation and static/actual Unity integration checks are complete; the build attempt is recorded as blocked before start. Actual instrumented device-play evidence remains pending, including first unsafe-corridor cause and runtime capture completeness. Private planner source ordinal remains unavailable; the conservative842-byte sizing result is not Android runtime proof. Gameplay bug remains OPEN; Phase B correction remains unapproved.
