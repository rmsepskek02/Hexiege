---
name: unit-stats-and-combat
description: 유닛 스탯, 전투 시스템, IEntityPositionProvider, 쿨다운 시스템
type: project
---

# 유닛 스탯 및 전투 시스템

## 유닛 확정 스탯 (2026-03-14)
| 항목 | Pistoleer | Assault | Sniper |
|------|-----------|---------|--------|
| HP | 30 | 50 | 30 |
| AttackPower | 6 | 1 | 10 |
| AttackRange (float) | 1.0 | 2.0 | 5.0 |
| MoveSeconds | 1.0 | 1.0 | 4.0 |
| ProductionTime | 5s | 10s | 15s |
| GoldCost | 50 | 100 | 200 |
| AttackCooldown | ~2.0s | ~0.2s | ~3.0s |
| DPS | 3 | 5 | ~3.3 |

- **AttackCooldown**: `UnitFactory.GetAttackClipLength()` — 클립 길이로 자동 덮어씀
- **UnitStats.cs 코드 기본값은 무관** — UnitFactory 런타임 덮어씀
- **AttackRange**: int → float 변경 (UnitData, UnitStats, UnitSpawnUseCase)

## IEntityPositionProvider (2026-03-07)
- 문제: HexCoord.Distance는 Lerp 완료 후에만 갱신 → 이동 중 최대 0.8초 공격 딜레이
- 해결: UnitFactory/BuildingFactory.GetObject()로 실시간 Transform.position 조회
- 파일: `Application/Interfaces/IEntityPositionProvider.cs`, `Infrastructure/UnitWorldPositionProvider.cs`
- 임계값: `attacker.AttackRange * HexMetrics.TileHeight` (epsilon 없음, +0.1f 제거 — 조기 공격 방지)

## 유닛별 AttackCooldown 시스템 (2026-03-06)
- UnitData.AttackCooldown / AttackCooldownRemaining (float)
- UnitFactory.GetAttackClipLength(Animator): "Attack" 포함 클립 검색
- NetworkCombatController: _attackInterval=0.1f 폴링, AttackCooldownRemaining -= interval
- UnitView.Update(): 싱글플레이에서만 AttackCooldownRemaining -= deltaTime
- MoveAlongPath 차단: HasEnemyInRange() 기반 (쿨다운 무관)

## 개별 이동속도
- UnitData.MoveSeconds (float, readonly) — 타일 1칸 소요 시간
- UnitView.MoveAlongPath: _unitData.MoveSeconds 참조

## 클라이언트 전투 시각 동기화
- 문제: 클라이언트 UnitView Lerp에서 TryAttack() 항상 false → 적 통과
- 해결: HasEnemyInRange() (네트워크 권한 체크 없음)
  - true → Idle + 대기, EntityDiedClientRpc 수신 시 Lerp 재개
- UnitView.StopMovement() public 메서드 추가


## TorrentSpirit bounded evidence (2026-10-01)

- Latest wave/display 32 is a cap; later waves are unobserved. The 11 noHpDelta cases are not proven errors/full-HP contacts or defense-caused zeros. Some coordinator expiry only increments Expired, so zero ERROR/contract failures is not no-loss proof; actual expiry is not established. Overall/exact visual acceptance remain OPEN/C3 Unresolved; compact full-match counters/reason classification/keyed timing samples are proposals only. Source: existing TorrentSpirit 2026-10-01 Task Research/Plan.


## TorrentSpirit diagnostics handoff and test preference (2026-10-02)

- **2026-10-02 final movement diagnosis (main source/raw-log review; no correction implemented):** Confirmed server spatial-state mismatch → Fatal → rejected closure → cancelled follow-up movement. First Editor Host: seven Pistoleer IDs24/28/34/39/46/51/57, two Fatals each=14. Second Android Host: LittleKnight129/131 plus Pistoleer147/150/153/156/162/165/169/172/176/179/185/188/192, two each=30. Strong probable root cause is non-walkable building-rally TerminalContact advancing Root while skipping the domain spatial transition; the next planner requires sampled start=committed tile, so mismatch is Fatal and cleanup cancels completion. Previous successful TerminalContact frame is not logged and no reproduction was run: do not claim complete runtime causal proof. Unit129 committed(8,-2)/Root(9,-2) Fatal and later3.083s stable pose with Move phase remain compatible with local ROOT PASS. User-stopped type is unknown; EmptySameTile zero-distance rejection and wave18 delayed consumption are separate. Goal versus walkable endpoint separation, Root/domain consistency and safe closure are recommendations only, not approved implementation. Previous cause-pending notes are history. Full IDs/log anchors/source boundaries: existing TorrentSpirit Research/Plan latest final movement analysis; overall OPEN/C3 Unresolved and MOVE FAIL.

- **2026-10-02 role-swapped two-match superseding evidence (main exact-key handoff):** Editor Host/Android Client epoch `40c733ed4f786813`: waves147, publish291=259 damage/32 heal, zero60 fullHP. Android Host/Editor Client epoch `491a032ae783db99`: waves176, publish292=263 damage/29 heal, zero118 fullHP. Client receive/both text emitters match291/292 unique contacts with missing/extra/conflict0; Client VFX exact-wave missing/extra0; badZero/duplicate writer and final channel reject/failures/expiry/pending/fault/overflow0. Second Client has two accepted-result consumption delays, residual349.511ms/249.849ms after the existing100ms delay; other290 max21.834ms. No text loss or network nonreceipt attribution; main-loop stall versus clock-update cause and complete timeliness remain unverified. User saw approximate agreement but could not verify exact timing. Overall OPEN/C3 Unresolved; both server MOVE END FAIL (spatialFatalPreflights14/30); Client EVIDENCE and local ROOT PASS do not prove movement progress. Keep prior53-wave and capped-match records as distinct history, retain Dev diagnostics. Full sessions/logs/timing and deferred movement-cause boundary: existing TorrentSpirit Research/Plan latest role-swapped section.

- **2026-10-02 latest runtime evidence (main final analysis, epoch `463c2fb49e473595`):** 53 waves have exact-ID Client receive/VFX-success coverage; 144 published contacts (111 damage/33 heal) match Client receive and both text emitters with zero missing/extra/conflicts. All 32 zero-delta writers are full-HP heals; do not retroactively classify the earlier unknown 11. Channel failures/expiry/reject/pending/helper faults/overflow are zero in this match. Text timing retains 100ms delay plus at most Host37.781/Client31.201ms. Side-entry contacts follow Units29; raw particle geometry/exact VFX contact timing and performance remain unverified, overall OPEN/C3 Unresolved. Earlier pre-runtime/compile-pending notes are dated history. Source: existing TorrentSpirit Research/Plan latest test section; retain Dev diagnostics until closure.

- Explicit user preference: collect detailed causal events every test, repeat tests, correct only evidenced paths, and retain this task's Dev diagnostics until closure; no temporary-log cleanup before tests.
- New WavePresentationDiagnostics removes the 32 observation cap; 512 active-wave/1024-per-wave-target/8192-total-target limits affect observer state only, overflow is explicit and causal events continue. Actual text-emitter success samples server clock immediately, separate from scheduler/presentation clocks; no gameplay writer/HP/hit-set/VFX/clip changes. Client normal/snapshot payload is still undifferentiated (correlate sender); single-fallback server-owner final needs out-of-scope Bootstrap wiring. Error-zero compile-agent handoff is not Unity/build/device/gameplay PASS. Overall and exact visual acceptance remain OPEN/C3 Unresolved. Source: existing TorrentSpirit Task Research/Plan latest 2026-10-02 sections.

- **2026-10-02 validation boundary:** The final compile-agent rerun is Runtime/Development 20 warnings/0 errors, Release 24/0, Editor 0/0; artifacts are under `C:/Users/rmsep/AppData/Local/Temp/hexiege-wave-compile/`. Main's before/after review preserves HP/Publish/RPC retry/VFX movement. The 900-byte chunk code is not an actual-execution byte-limit PASS: maximum-head/Base64 128+ output evidence is missing. New Unity menus/selftest/build/device evidence and single-fallback server-owner final remain open.

- **2026-10-02 final superseding handoff:** Invariant G17/full UTF-8 >900 keyed format-incomplete warning/faults/coverage=false and single simulation-local/network synced-server domains are implemented. Final runtime/development/release/editor.byte-format.compile.log errors0, warnings20/20/24/0. Separate byte-format-bounds.log conservative max699/fallback400 is not main658's fixture and neither proves real Unity/GameLog/device bounds. Earlier944/pending notes are history. Overall OPEN/C3 Unresolved, build not attempted, single-fallback server-owner final/payload distinction still incomplete.
