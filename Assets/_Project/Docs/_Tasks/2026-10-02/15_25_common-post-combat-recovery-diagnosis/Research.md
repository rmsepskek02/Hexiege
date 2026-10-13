# Common post-combat recovery diagnosis — Research

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

전투가 끝난 뒤 살아 있는 TorrentSpirit가 다시 이동하지 않은 사례를 공통 이동 경로에서 조사합니다. 먼저 최초 복귀 거부부터 Blocked 진입까지의 상태를 자동으로 남기는 진단 계측을 추가합니다. 이번 승인은 원인 증거 수집까지이며, 게임플레이 교정은 증거를 확인한 뒤 별도로 승인받습니다.

현재 Phase A 계측 구현과 정적 컴파일, 실제 변경 코드의 Unity 메뉴 검증은 완료되었습니다. Android 빌드 시도는 서명 비밀번호 사전검사에서 차단되어 시작되지 않았습니다. 새 실기 증거와 정지 원인은 OPEN이며, 이동 교정이나 버그 해결 PASS는 아닙니다.

## 1. Status and scope

- Date: 2026-10-02. Current status: Phase A instrumentation implemented; static compilation and actual changed-code Unity menu verification PASS; Android build attempted but blocked by missing signing passwords before start. Instrumented runtime evidence/cause remain OPEN; Phase B gameplay correction unapproved.
- This task creates Research.md and Plan.md only. No Testcase, QA file, reproduction harness or new validator is requested.
- Shared post-combat recovery is the implementation surface. TorrentSpirit wave assets, damage/heal/presentation contracts and the separate LittleKnight/Pistoleer building-rally Fatal defect are outside scope.
- Approval of Phase A and a subsequent build attempt is not approval of Phase B gameplay correction. Evidence collection completeness is not a bug-fix PASS.

## 2. Runtime evidence

Source: `Assets/_Project/Docs/_Logs/2026-10-02/13_08_logcat/RuntimeLog_device.txt` (Android Host; Editor Client is the peer).

- Shared match correlation: `aab0822e2bf8d0539833764f28f1b9705663e55d984f5f0b79767288cbde53c5`; wave epoch `491a032ae783db99`.
- Line 1055: Blue TorrentSpirit unit125 produced at 13:04:50.233 (logcat envelope 13:04:50.234).
- Line 5365: last observed caster125 wave spawn at 13:05:56.128, wave35, primary unit157.
- Line 5452: unit157 death at 13:05:56.477 (envelope 13:05:56.478).
- Line 5621: 13:05:59.091 `post-combat-corridor-confirmed`, commandRevision1, segmentRevision18, environmentRevision3; `pathStatus=EmptySameTile`, authoritativeStart=simulationRootTile=goal `(1,8)`, currentPath count2, candidate path invalid, corridorEvidence not applicable.
- Main's supplied subsequent-position audit: 146 valid samples through 13:08:26.489 show unit125 alive at unchanged XZ `(0.750,-7.681)`; no further caster125 wave spawns were found. This establishes the observed stationary interval, not the reason for the first corridor rejection.

The `RejectedNoProgressBudget` label at line5621 is explicitly supplied by the confirmed-corridor logging branch. It is not proof that the real progress guard exhausted its budget. Do not merge this incident with the building-rally spatial Fatal counts or with delayed wave text consumption.

## 3. Existing source seams

Source anchors describe the inspected source, not immutable line numbers after implementation.

- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs:2756`: shared `MoveAlongPathV3`; network Clients do not run this movement coroutine.
- `UnitView.cs:2830` area: Blocked retains the coroutine/pose and waits for a pending path or navigation environment revision change; it does not continuously request paths every frame.
- `UnitView.cs:3330` area: a rejected post-combat alignment corridor enters `UnitPostCombatRecoveryPlanner`; existing candidate requests and spatial acceptance determine recovery.
- `UnitView.cs:3435`: `ConfirmedBlocked` marks Blocked and logs the manual `RejectedNoProgressBudget` label.
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs:160`: authoritativeStart==target returns `EmptySameTile` with null path. This typed status must not be silently collapsed into generic unreachable evidence.
- `UnitMovementUseCase.cs:563` / `:583`: `TryFindReachableForwardRejoinTile` already handles the current authoritative goal when walkable. There is an existing same-tile recovery provision; this task must inspect how fallback and corridor recovery consume it, not claim that no fix exists.
- `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs:2250` onward: planner validates its active route, requires recovered paths with at least two points and matching candidate endpoint, stages acceptance/rejection, and confirms Blocked after two complete unchanged-context sweeps.

## 4. Findings and unknowns

- Confirmed: Torrent125 remains stationary after a post-combat confirmed-corridor recovery failure; the logged request is same-tile, not evidence of an unreachable destination by itself.
- Confirmed shared vulnerability: the shared recovery consumer expects a usable multi-point path while the provider can correctly report same-tile with no path. Existing same-tile rejoin handling must be preserved and traced through the actual fallback.
- Not established: the first unsafe corridor's reason, candidate geometry/walkability at that instant, why the existing same-tile handling did not close this episode, and a complete causal chain from that first rejection to the persistent hold.
- A fallback/consumer contract mismatch is a diagnostic possibility, not a selected gameplay fix. Other unit types use the shared path, but this exact runtime failure is not proven for them.
- A valid current goal may still require safe physical center alignment. Same tile does not authorize snapping, ignoring corridor checks or falsely declaring completion.

## 5. Required evidence, not new gameplay queries

Keep bounded recent-state records in memory using values already computed by normal gameplay. On the first post-combat corridor rejection and on first Blocked entry in each recovery episode, automatically emit the correlated recent history and the decisive current state.

Capture unit/lifecycle identity, command, segment, episode, environment revision, frame and explicit clock domain; Root world/domain pose and sampled tile; committed tile and final goal; selected rejoin/candidate and selection source; route signature/count/waypoint and bounded relevant points; provider typed result; planner candidate ordinal/sweep count; real progress-guard counters; corridor status/offending tile/index and walkability evidence actually available from the production decision. Unavailable facts must be explicit, never inferred or obtained by extra A* searches.

Follow the same episode through subsequent unblock/repath acceptance, center/completion, reengagement, death or lifecycle retirement. Retain explicit bounded-capacity, eviction, overflow and missing-history counters so a partial dump cannot appear complete. No per-frame file logging; no navigation, pose, animation, target, HP or callback mutations.

## 6. Authority references

- `Assets/_Project/Docs/GameSystemRules.md` — rule index.
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md` — `U-MOV-PHASE`, `U-MOV-ALIGN`, `U-MOV-REPATH`; rules 2, 3, 4, 8, 9, 10, 11, 13, 16, 22.
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` — `NET-AUTH-001`, `NET-ROOT-001/002`, `NET-ACTION-STATE`, `NET-FACING-001`, `NET-CANCEL-004`, `NET-TIME-001`.
- `Assets/_Project/Docs/LogRules.md` — sections 1.2–1.4, 1.6–1.10, 1.12, 1.14.
- `Assets/_Project/Docs/WORKFLOW.md` — scoped task handoff and verification reporting. Main owns the documentation checker.

## 7. Historical implementation and static compilation handoff (2026-10-02, before final menu/build results)

Initial document-handoff history: "Phase A diagnostic instrumentation approved, not yet implemented in this document handoff." The implementation handoff superseded that initial status; section 1 now shows the final current status. Main reports implementation complete in `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` only. This document update records the supplied implementation results, not an independent code audit or runtime reproduction.

- Schema: `pc-recovery-v1`; shared server-only Editor/Development recorder, with release capture/storage excluded.
- Fixed capacities: 512 tracked views, 32 recent samples per recorder, 256 points per path. Normal captures are decision-only; no extra queries or gameplay changes.
- First failure is captured before the planner runs. Blocked entry has a separate capture, preserving the distinction between the initial rejection and later recovery outcome.
- Provider evidence is an immutable scalar snapshot. Cached spatial/provider evidence retains its own sourceFrame/source time separately from the current capture frame/time, so stale observations cannot masquerade as current decisions.
- Planner source ordinal is unavailable because it is private; do not infer it or claim complete planner-internal coverage. This is an explicit evidence limitation.
- Main's conservative maximum full UTF-8 line estimate is 842 bytes, below the task ceiling of 900 bytes. This is static sizing evidence, not Android runtime logcat completeness proof.

Static compilation results supplied by main (all exit code 0, errors 0):

| Configuration | Warnings |
|---|---:|
| Runtime | 20 |
| Release | 24 |
| Development | 20 |
| Editor | 0 |

Unity was manually refreshed and was undergoing domain reload at this handoff. Actual changed-assembly menu verification is PENDING; Android build has NOT YET been attempted. New device evidence, first unsafe-corridor cause and gameplay correction remain unresolved. Instrumentation implementation/static compilation must not be described as a recovery fix PASS.

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
