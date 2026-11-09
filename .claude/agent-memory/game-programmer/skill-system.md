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
  `PresentationContractSchemaRevision` 6 → 7 and the fixture line `== 7` *(2026-11-09 note: now 8 / `== 8` — §2)*. No `IGameServices` method was added (the
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
