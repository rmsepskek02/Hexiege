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
  `PresentationContractSchemaRevision` 6 → 7 and the fixture line `== 7`. No `IGameServices` method was added (the
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
