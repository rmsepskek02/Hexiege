# Skill system — support skills (2026-11-03~)

> Rules: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md` rules 7 · 11 · 25 · 26 win over this note.
> Earlier skill work (Phase 1 / Phase 2 type C / aim coordinates) is in `MEMORY.md` dated sections and
> [skill-aim-coordinate.md](skill-aim-coordinate.md).

## 1. Common foundation + Bombardment (2026-11-03)

Source: `Assets/_Project/Docs/_Tasks/2026-11-03/05_24_support-skill-foundation-bombardment/` Research, Plan §3 · §7 · §10.
Commits `03e5551d` (data fields, availability, panel guard, setup tool) · `56d5ab2a` (C-1) · `c4cbf8ab` (Bombardment);
user asset push `3ff5ed60`. User test PASS (single + multi, user's overall verdict; suite PASS by rule).

- **Data fields live on the skill asset** (`SkillDefinition` → `SkillData`, constructor called only from `ToSkillData`):
  display name · description · `IsAvailable` · `ImpactDelay` · `ActivationVfxPrefab` · `VfxReferenceRadius`.
  `IsAvailable` default true so old assets keep working; the 15 new assets set it false except Bombardment.
- **Unavailable = reject before cooldown.** `SkillActivationUseCase.Activate` returns false right after the slot lookup
  (line ~128), so no executor runs and no cooldown starts. Why it was needed: a placeholder type-A asset on a "no code yet"
  slot would really run the bomb executor and start the cooldown (an air strike slot behaving as a bomb). The panel guard
  (`BuildingSkillPanelUI` PointerDown, line ~317) is only UX — the server check is the real one (rule 25).
- **Two presentation channels, like the attack presentation precedent:** the UseCase publishes
  `GameEvents.OnSkillActivationConfirmed` (server / single) **after** starting the cooldown (the network relay reads
  `GetCooldownTotal` inside that event); `NetworkSkillController` relays it with the new `SkillActivatedClientRpc` shape;
  pure clients (after the kept `if (IsServer) return;`) mirror the cooldown and publish `OnSkillActivationReceived`.
  One screen subscriber (`SkillActivationVfxPresenter`) listens to both — the `IsServer` guard is what stops the Host
  from playing twice. Payload = building id · team · **slot** · has-aim · aim (domain coords) · sequence · impact delay;
  the receiver re-finds VFX/radius from **its own build's** loadout by team + slot, so both builds must match →
  `PresentationContractSchemaRevision` 6 → 7 and the fixture line `== 7` *(2026-11-09 note: now 8 / `== 8` — §2)* *(2026-11-11 note: now 9 / `== 9` — §4)*. No `IGameServices` method was added (the
  regression fixture has a stand-in implementation).
- **Impact reservation is not on the cooldown tick.** `TickCooldowns` also runs on the pure client as a mirror
  (`GameBootstrapper.Update` guard `!IsNetworkMode() || !IsNetworkServer`); putting strikes there would make clients deal
  damage. Separate authoritative method `TickPendingImpacts`: single = `GameBootstrapper.Update` under `!IsNetworkMode()`
  only, multiplayer = `NetworkCombatController.TickCombat` (server, after its entry guards). Do not merge the two guards.
- **Delay is chosen by the bridge, not the executor.** `SelectInstantAreaDamageBridge(delay, sequence)` hands the type-A
  executor either the old instant bridge (delay 0 — unchanged behaviour) or a scheduling lambda. So the delay only applies
  to **type A**; B / C ignore the field. The strike re-uses `ApplySkillInstantAreaDamage` once at expiry → targets are
  whoever is in range **at impact** (tile centres). Caster death does not cancel (CannonTower precedent). Game end:
  `OnGameEnd` → `ClosePendingImpacts()` (closes and clears; re-checked before each due strike because a strike can end
  the game). The UseCase is rebuilt per map load, so no reset is needed.
- **VFX playback:** `EffectManager.PlaySkillActivationVfx(prefab, viewPos, uniformScale)` — no rotation, no offset, root
  scale × (radius ÷ reference radius) via `ComputeSkillVfxScale` (≤ 0 → 1). Prefab particles must be Hierarchy scaling.
  The presenter returns early when `HasAim` is false (no placement rule yet for instant skills) and converts with
  `ViewConverter.ToView`. Wired in `GameBootstrapper.Map.cs` (AddComponent if missing, `Initialize` on each map load —
  it disposes old subscriptions first). No skill SFX yet (user decision); if added, call it right under the VFX call
  (Sound rule 15).
- **Known limits (recorded, not tasks):** multiplayer strike up to one server tick late (scene `_attackInterval` 0.1 s);
  prefab Shell lifetime 0.4 s and data delay 0.4 s are kept separately (no check); temporary slot labels overlap the
  cooldown number (user: leave it for the later icon UI).
- **Meteor (2-D) should reuse the reservation** — it is a type-A strike with a longer delay; what is new there is the
  warning playback speed, the moving body and burn.

## 2. EMP — area tower disable (2026-11-04~09)

Source: `Assets/_Project/Docs/_Tasks/2026-11-04/03_54_emp-tower-disable/` Plan §3 · §7-1 (user decisions Q1~Q12) · §11.
Commits `aea11d3a` (judgment) · `28dfc9a8` (sync, schema 8) · `6c97c6f1` (screen) · `d7cce131` (one-time menus) ·
`0d5d43a3` (skill VFX ground height, pulse prefab, diagnostic logs); user asset commit `665174df`. User test PASS
(main session's log + visual verdict, approved by the user). Rules: Skills 25 · 27, Buildings 「방어 타워 시스템」 13.

- **New type = new executor + one registry line.** `SkillMechanicType.AreaTowerDisable = 3` (appended last — serialized
  assets), `AreaTowerDisableExecutor` only reads radius / duration and calls a context bridge; it ignores `TotalDamage` and
  `ImpactDelay` (the delay bridge exists only for type A). Guard `!(x > 0)` also rejects NaN.
- **Building state is a separate service, not `StatusEffectSystem`.** Unit ids and building ids come from different
  counters and can collide, and the unit status tick also runs as a pure-client mirror. `BuildingStatusEffectSystem`
  (Application, building-id keys, one `StatusEffect` per tower) — `DisableEnemyTowersInRadius` (enemy · alive · AutoTower,
  tile centre ↔ aim ≤ radius, same as type-A building hits), re-apply = replace remaining time, `Remove` on
  `OnBuildingDied`, `Close()` on `OnGameEnd` (clears and refuses later applies). Empty dictionary → `CanAttack` returns
  true without lookup (regression safety). Reusable for the other 2-B skills (building shield / regen).
- **Tower gate:** one line in `TowerCombatUseCase.Tick` **after** the cooldown decrement and **before** target search
  (K1: cooldown keeps ticking). Injected by a **setter** (`SetBuildingStatusSystem`) — changing the constructor breaks two
  regression fixtures; null = always pass. In-flight server shells need no code: the gate is only on the launch decision.
- **Expiry tick is authoritative only and placed right before the tower tick in both modes** (single
  `GameBootstrapper.Update` under `!IsNetworkMode()`, multi `NetworkCombatController.TickCombat` before `tower?.Tick`) so
  "expire → gate" order matches. Never on the client cooldown-mirror tick.
- **Sync = one list RPC, no end RPC, no client mirror.** `OnTowersDisabledConfirmed` (server) →
  `TowersDisabledClientRpc(sequence, duration, towerIds)` → pure client `OnTowersDisabledReceived` (after `IsServer`
  guard). The received payload has no caster team (Neutral) — the presenter resolves race/position from **the tower's own
  team and GameObject**. New RPC ⇒ schema 7 → 8 + fixture `== 8`. Known limit: client starts/ends late by transport delay.
- **Status-loop VFX with a lease** (`VfxPoolItem.StatusLoop.cs`): `BeginStatusLoop()` after `Play`, stop only when the
  lease matches — tail stop (emission off, Update returns the item when particles die, ≤ 0.8 s) for expiry / game end,
  clear stop for tower death. Pool item stays under the VFX container, never a tower child (tower Destroy would take it).
  Re-apply only extends the end time (no restart). Position = tower GO position + preset offset **Y only** (enemy Human
  CannonTower is rotated 180°) · preset scale. Presets: `BuildingEffectConfig._towerDisabledPresets` (race rows).
- **One-time preset menu kept separate from `SupportSkillLoadoutSetup`** — that tool rewrites all 15 skills on every
  run, so later-tuned heights would be reset. The preset menu keeps existing offset/scale on re-run. User decision
  2026-11-09: the preset menu (+ `.meta`) and the 3 commented-out old EMP lines were deleted after confirming the 3 preset GUIDs are wired in `BuildingEffectConfig.asset` and code references are 0. `SupportSkillLoadoutSetup` itself retires after the whole support-skill work.
- **Lessons:** (1) tile top is Y 0.05 and opaque — a VFX root at aim Y 0 is hidden/half-cut → see
  [vfx-presentation.md §13](vfx-presentation.md#13-skill-vfx-ground-height-and-diagnostic-logs-2026-11-09).
  (2) WORKFLOW [4] now requires a diagnostic log for every user test item: log once on each state change, count per-tick
  events (gate blocks) and report the count at expiry; prefix + `Diag=` + `DiagRev=` keys. The user kept them as `개발`.

## 3. Support buff skills (type C ×4) + unit status-VFX path (2026-11-10~11 — user test PASS)

*(2026-11-11 updated — heading was "implemented, user test NOT run"; the line "Uncommitted at writing time; verify the commit
before trusting this section." is replaced by the commit list below.)*

Source: `Assets/_Project/Docs/_Tasks/2026-11-10/04_20_support-buff-skills-status-vfx/` Plan D1~D14 (user 「추천대로 진행하자」)
and Plan §11 (results). Commits `06bf59f` (data) · `e5d7020` (screen) · `fbd3ab0` (diagnostics · suite · assets) · `6d0245c`
(temporary prefab emptied, 8 disabled comment lines deleted). User test PASS 2026-11-11 (2 multi + 1 single match, log verdict
+ user visual; suite PASS by rule).

- **Data:** `SkillDefinition._statusVfxPrefab` / `_statusVfxScale` / `_statusVfxOffset` → `SkillData.StatusVfx*` (optional ctor args,
  scale ≤ 0/NaN → 1). Setup tool does not write these keys. Four assets enabled (D4 temporary: AttackPowerMul 1.5 / MoveSpeedMul 1.3, 8 s, cd 15).
- **Screen:** `UnitBuffVfxPresenter` (key = unit id + caster team + slot) listens to `OnSkillActivationConfirmed`/`Received` (plan A, no RPC,
  schema stays 8), re-finds the skill with `FindSkillData`, targets = alive units of the target team on this screen. Start via
  `EffectManager.StartUnitBuffVfx` (loop contract check cached per prefab, recipient-fit table) → `VfxPoolItem.FollowStatusLoop(lease, pose)`
  after `ProjectNow()`. Stops: expiry/game end tail, death/reinit immediate, re-use extends end time only.
- **Instant-skill activation VFX (D3):** `SkillActivationVfxPresenter` old `NoAim` return commented; no-aim skills play at the caster
  building tile centre + 0.06 (`Anchor=CasterBuilding`), building lookup = 4th optional `Initialize` arg.
- **Diagnostics `[SKILL-BUFF]` (Diag=SkillBuff):** server/client in `UnitCombatUseCase.SkillBuffDiagnostics.cs` (batch per team+kind,
  expiry watched through `StatusEffectSystem.SetDiagnosticTickListener`, `HasEffect` added), `BuffNoEffect` in `SkillActivationUseCase`
  (activation tag gives BuffApplied its sequence/slot), screen lines in the presenter. Suite: `RunUnitActionSelfValidation.SupportBuffSkills.cs`.
- **Setup-tool count trap (fixed in `fbd3ab0`):** `SupportSkillLoadoutSetup.ExpectedAvailableCount` must equal the number of
  available specs in `BuildSpecs` — left at 2 with the four buff lines enabled, `ValidateSpecs` aborts before writing (safe:
  nothing reverts, but the tool cannot be re-run). It is now **6** (`SupportSkillLoadoutSetup.cs` line 91), with the
  completion log text listing the six names. *(2026-11-11 corrected — this line said "is still 2 … until that constant is
  changed with approval", but the same commit `fbd3ab0` that wrote the note already set it to 6.)* **Lesson:** whenever a
  setup-tool spec line flips `available`, change the expected-count constant in the same commit, and re-read the committed
  file before writing a memory note about its state.
- **Testing a slot with no prefab (D14):** put an existing loop prefab that already meets the status-loop contract
  (`vfx_bloomfairy_heal`, scale 2) into one skill's slot for the test, then empty it after the test (`6d0245c`). It proved
  attach · follow (flipped Client too, one late pose resolved with `WaitedMs=0`) · expiry tail stop · death immediate stop
  with `LeaseMatched` = stop count. Game-end / reinit stops stayed unobserved (no buff was active at match end).
- **Kept disabled block:** `SkillActivationVfxPresenter.cs` lines 151~157 (`NoAim` block) and 192 (old position line) stay
  commented until an instant skill gets an activation prefab and the caster-building playback is seen in a test. Deleting
  them breaks suite B-8 (`RunUnitActionSelfValidation.SupportBuffSkills.cs` line 273 `Contains("// if (!p.HasAim)")`) —
  change that check in the same commit.
- **Instant activation VFX scale:** the four buff assets have radius 0, so `ComputeSkillVfxScale` returns 1 (prefab size as made).

## 4. Persistent status zones — Frost Prison · Sap Swamp (2026-11-11 — user test PASS)

*(2026-11-11 updated — heading was "implemented, user test NOT run"; the line "Uncommitted at writing time … verify `git log`"
is replaced by the commit list below.)*

Source: `Assets/_Project/Docs/_Tasks/2026-11-10/16_02_support-skill-persistent-zones/` Plan Z-1~Z-21 (user 「추천대로 진행하자」).
Commits `0de30f7` (zone service · executor · server tick) · `96fbbd1` (membership RPC, schema 9) · `43c7c16` (zone VFX · unit zone
VFX) · `417feea` (assets enabled · suite · temporary prefabs) · `a4a4465` (slot button text); cleanup (temporary prefabs emptied,
stale comments fixed, disabled lines deleted) is uncommitted at writing time — commits are made by the main session.
User test PASS 2026-11-11 (3 logs, multi + single): freeze + attack block, sap 0.4 and exit restore, overlap `Applied=Once`,
zone survives caster-building demolition, tailwind × sap 0.52 / exit 1.3, `GameEnd` close, Client membership = Host, zone VFX and
unit zone VFX start · Expire · UnitDied stop, `SlotLabels` Name 15/15. **Unobserved:** unit zone VFX `Path=Exit` stop, `Reinit`.

- **Zone state is its own service, not `StatusEffectSystem`** (Z-1 Z3): `SkillZoneSystem` (Application, partial + `.Diagnostics.cs`).
  Only `GetUnitMoveSpeedMultiplier` (research × status × zone, zone multiplied only when ≠ 1) and `CanAttack` (zone check first)
  read it. Why: `UnitStatusState.ApplyOrRefresh` replaces the same kind wholesale, so a sap `MoveSpeedMul` would erase a tailwind
  record. No code outside the accessors reads status multipliers directly (R-7 grep, 2026-11-11).
- **Same skill (team + slot) overlap applies once** via a `(unitId, skillKey)` set while rebuilding the per-unit effect cache;
  different skills multiply; Freeze = move 0 + no attack. The cache is rebuilt only when membership changes.
- **Authoritative tick through `SkillActivationUseCase.TickZones`** (no new `IGameServices` getter — fixture stand-in): multi
  `NetworkCombatController.TickCombat` right before the status tick; single `GameBootstrapper.Update` under `!IsNetworkMode()` only.
  `CloseZones` on `OnGameEnd`; `NotifyUnitDied` from a Setup `OnUnitDied` subscription (also tick-detected).
- **Sync = one membership RPC** `ZoneMembershipChangedClientRpc(seq, entered[], exited[])` (schema 8 → 9, two fixtures `== 9`).
  No create / end RPC: create = the activation RPC, end = screen-local timer (caster death keeps the zone — Z-7).
  **First members are announced on the next tick**, so the activation RPC always precedes the membership RPC (R-10).
- **Screen:** `SkillZoneVfxPresenter` (ground loop VFX from `_zoneVfxPrefab`, scale radius ÷ `_vfxReferenceRadius`, +0.06 height).
  V4 = `UnitBuffVfxPresenter` zone driver (`.ZoneDriver.cs`): entries carry `Driver` + `ZoneRefs`; stop groups log under the driver's
  prefix, so `[SKILL-BUFF]` keys are unchanged. Suite B-8 string gates read only the main presenter file — keep them there.
- **Self-validation:** `RunUnitActionSelfValidation.SkillZones.cs` (Z-T1~Z-T10). Headless execution of `UnitCombatUseCase` /
  `SkillZoneSystem` outside Unity fails with `TypeLoadException: Internal call method with non_NULL RVA` (reference UnityEngine
  DLLs) — only the static compile harness works; the suite runs in the Editor.
- **Temporary test prefabs (Z-18):** Frost Prison `_zoneVfxPrefab` = `vfx_emp_disabled`, `_statusVfxPrefab` = `vfx_bloomfairy_heal`
  (scale 2, D14 precedent) were used for the test and **emptied after it** (`{fileID: 0}`, scale 1 — 2026-11-11). Every zone VFX
  slot is empty now: nothing is visible until real prefabs are made. Suite Z-T9 checks a filled slot's loop contract only when it
  is not null, so empty slots pass.
- **Slot button text = skill asset name (unit ⑥, commit `a4a4465`):** `BuildingSkillPanelUI.ResolveSlotLabel`
  shows `SkillData.DisplayName` (trimmed), falls back to `DeriveSkillLabel` only when blank (old commented line deleted after the
  test). Log `[SKILL-PANEL] SlotLabels` (Diag=SkillPanel, `S1..S5 Label/Src/Avail`) once per panel open. All 15 loadout assets
  carry names matching the design Plan table, so no slot falls back. Labels: scene `SkillTestLabel` TMP 24pt, wrap on, no
  auto-size, full-stretch over a ~297-px-wide slot (3 per row). `SkillDefinition._displayName` comment/tooltip and
  `SkillData.DisplayName` doc now say "used as the slot button text, fallback when blank" *(2026-11-11 fixed — they said
  "not shown on screen")*.
- **Disabled-line cleanup after the test (2026-11-11):** deleted only blocks whose replacement ran in the test logs — old 9-arg
  `SkillActivationContext` call, schema `= 8` line, `UnitBuffVfxPresenter` old non-partial declaration and old stop-group
  condition, old slot-label line, and the setup tool's four old lines. **Kept** (replacement not seen running):
  `UnitBuffVfxPresenter` old `LogFault("Follow" …)` (runs only on an exception; Fault 0) and old `LogPendingResolved` line
  (zone pending-resolve not in the reported results); the suite's two old `== 8` schema `Require` lines
  (`RunUnitActionSelfValidation.ServerProjectiles.cs` / `.SupportBuffSkills.cs` — suite run not in the test evidence);
  `SkillDefinition` old header and three old tooltip texts (Inspector text, not checkable by logs — main session to decide).
