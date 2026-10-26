# VFX presentation — contracts, recipient fitting, tower bolt, caster-owned VFX, charge units

Lessons verified against the code on branch `claude/vfx-code-followups` (commit `84b6ff2a`, 2026-10-15)
and confirmed by the user's field tests (3 rounds, 2026-10-13~15). Task record:
`Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/{Research,Plan}.md` (Research 8~10, Plan 10/12/13).
All of this is presentation only — no server damage / target / HP / RPC path changed.

## 1. SkinnedMeshRenderer.BakeMesh output is in the renderer's LOCAL space

- `smr.BakeMesh(mesh, true)` (useScale = true) returns vertices in the renderer Transform's local space with
  the renderer's (and parents') scale already *undone*. To get world points you must multiply by
  `smr.transform.localToWorldMatrix` (position + rotation + **scale**).
- The bug (editor tool `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`, fixed in `84b6ff2a`, old line kept
  commented around line 332): it used `Matrix4x4.TRS(position, rotation, Vector3.one)`. Production unit prefabs put
  the body under `Unit_xxx_Mesh` scaled 0.2~0.3 (Armature lives below it), so skinned heights/radii were recorded
  ~3.3~5x too large (Pistoleer height 1.71 instead of ~0.34). The MeshRenderer path (Tank/CannonCart) already used
  `localToWorldMatrix`, so vehicles looked right — the two paths disagreed silently.
- Guards now in place: tool warns when height > 1.2 m, radius > 0.43 m (half a hex tile ~0.87 m) or height < 0.1 m
  (still writes the row); self-validation `ValidateRecipientFitMeasurement` (`RunUnitActionSelfValidation.VfxConnections.cs`)
  requires Pistoleer 0.2~0.6 m and Tank taller than Pistoleer (cross-checks that both paths share one space).
- **Lesson:** when a tool has two measurement paths (skinned vs static mesh), assert a cross-path relation on real
  prefabs, not just that each path returns "something". A wrong table looked like a prefab/preset problem in the
  field ("poison/heal too big and high") — check the data source before tuning values on top of it.

## 2. Particle contracts are enforced by code — a prefab that violates them never plays

- Bloom heal (`VfxPoolItem.PlayBloomLoop`): rejects only when the prefab has **0** ParticleSystems (old
  `!= 5` check kept commented, R-1). Every system must be loop ON, not unscaled time, Stop Action None, Local
  simulation, burst count 0 — otherwise playback is refused before Play (`BloomHealEffectPresenter` records
  `Fault / RecursivePSContract`; the HoT itself still heals). Logs print the real count (`RecursivePS=<n>`).
- Burn / poison (`VfxPoolItem.PlayTimedDamage`): at least 1 system, each loop ON + Stop Action None, else it throws
  ("Damage loop contract").
- Validator asserts authored count >= 1 and lease count == authored count — never hard-code a particle count
  from one prefab revision (the user removed `Bloom_Ring`, 5 → 4, and the heal VFX silently disappeared).

## 3. Recipient (status VFX) fitting — `UnitEffectConfig` table + presets

- `UnitEffectConfig` `_recipientFits` per UnitType: `radius`, `height`, `footOffset`, `manualScale`.
  Uniform scale = `manualScale` if positive, else `height / RecipientReferenceHeight (1.0)`; clamped; missing row = 1.
- Computed once at play (`PlayTimedDamage`, `PlayBloomLoop`); Follow uses the stored value (no per-frame bounds —
  deterministic, same on Host and Client). Final scale = original × preset `Scale Multiplier` × fit scale;
  position = pose position + pose rotation × (preset `Local Position Offset` + `footOffset`).
- The table is filled only when the user runs `Hexiege/Effects/Measure Unit Recipient Fit` (asset saved on run).
  User then tunes per effect through the presets — confirmed working 2026-10-15; tuning is data work, not code.
  Values at commit `91371e55` (user): re-measured table Pistoleer height 0.342, max height ~0.53, max radius ~0.41,
  `manualScale` 0 everywhere; Heal and Poison `Scale Multiplier` 2, Burn 1; all `Local Position Offset` 0. Tuning continues.
- ⚠️ `manualScale` also changes that unit's RuneSpire chest height (§4) because chest height derives from the same
  uniform scale.

## 4. RuneSpire (Spirit AutoTower) bolt — `VfxPoolItem.TowerBolt.cs`

- Target resolved **once** at play (UnitView + UnitData reference + VisualRootProjector); `LateUpdate` re-reads the
  rendered position; on dead / replaced data / inactive / unreadable pose it freezes at the last endpoint and never
  re-tracks (no ID re-lookup — ID reuse safe). Human tower and Mushroom impact never enter this mode.
- Source = tower + preset offset rotated by **yaw only**, so the offset Y is a world height (RuneSpire preset Y 0.65 =
  crystal). Root aims in **3D** at endpoint; `Bolt` local Z scale = 3D distance (divided by root scale).
  `Impact_Sparks` is a child of `Bolt` at local Z 1 — do not move it separately (distance would apply twice).
- Endpoint = rendered feet + chest height; chest height = `UnitEffectConfig.GetChestHeight` = uniform scale × 1.0 ×
  `ChestHeightRatio` (default 0.6); unit not in table → 0.6 m fallback.
- The first design (4-B: "ignore height, XZ only") matched the written plan but not the user's intent; a preset
  offset cannot fix a horizontal aim. When the user names a visual anchor ("crystal", "chest"), design the aim in 3D.
- **OPEN (2026-10-15):** if the target is already dead when the bolt begins, tracking never starts and the endpoint is
  snapshot Impact + chest height (line 172), not the rendered pose. *(2026-10-15 correction — original said the check
  is `TryReadEndpoint` line 76; that line is the mid-flight death check. Dead-at-begin is rejected earlier — see §4-1.)*
  Ordering fact: `TowerCombatUseCase.ExecuteTowerAttack` reads the impact position (223~226), then applies damage
  (241), then builds/publishes the snapshot (245/248) — so a lethal hit always reaches this branch on server/Host/single
  (Client HP ordering not traced). The user sees a slight endpoint offset only in this case. Suspected
  snapshot-vs-rendered mismatch is an **unverified hypothesis** — log both positions at begin before changing code.
  Using the rendered pose here would touch Buildings 「방어 타워 시스템」 rule 12 ("추적 불가로 시작할 때") — ask the
  user before any rule change.

## 4-1. HANDOFF — bolt endpoint off when the tower hit kills the target (OPEN, pick up in a later task)

Single source: `Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md` §13-4 (ROADMAP 「VFX 후속 ①」).
Symptom (user, 2026-10-15): endpoint slightly off only when this very hit kills the target; living / moving targets are fine.

Verified code facts (line numbers at `84b6ff2a` — re-read before editing):
1. `Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack`: reads source/impact 223~226 (`GetUnitWorldPosition`,
   zero → `HexToWorld` hex centre) → `target.TakeDamage` 241 → snapshot `_effectIdentity.Create` 245 → publish 248.
   Lethal hit ⇒ target already dead when the presentation begins (server/Host/single; Client order not traced).
2. `Presentation/Effects/HitPresentationQueue.cs` `ResolveTowerBoltTarget` (267~): line 278 `!data.IsAlive` →
   `TargetDead`, returns `default` (no View/Data). Called at 246.
3. `Presentation/Effects/VfxPoolItem.TowerBolt.cs` `BeginTowerBolt`: no reference → `Untracked(TargetDead)`,
   endpoint = `snapshotImpact + up × chestHeight` (~172). Living path ~165 uses `VisualRootProjector.PresentationPosition`.
4. `Presentation/Effects/EffectManager.cs` 550 `ResolveBoltChestHeight(boltTarget.Data)` → 560~563: `Data == null` ⇒
   `UnitEffectConfig.FallbackChestHeight` **0.6 m**, not the table value (Pistoleer height 0.342 → table chest ~0.21 m).
5. `Infrastructure/UnitWorldPositionProvider.cs` `GetUnitWorldPosition` (35~) = unit GameObject **root** position, while the
   living endpoint is the VisualRoot projection — different sources. Client flipped view converts at HitPresentationQueue 235~236.

Hypotheses (ALL unverified): H0 fixed 0.6 m chest height lifts the endpoint (strongest code-level candidate, worst on small
units); H1 snapshot root position ≠ rendered pose; H2 impact not at the feet / hex-centre fallback; H3 only in the flipped
(Red / Client) view.

First diagnostics (approval needed, no behaviour change): in the untracked branch log snapshot Impact, rendered positions
read via `s.TargetId` (Projector + Pose, if the View is still active), chest height used vs `GetChestHeight(type)`, reason —
append to the existing `AttackEffectIdentity.Record` outcome (Dev/Editor only). Collect on Host and Client screens, Blue and
Red views, with a one-shot kill. Do not change behaviour until a hypothesis is confirmed.

Candidate fixes: F0 chest height from the snapshot target's UnitType even when dead (rule 12 wording seems unaffected —
it already says "table, else 0.6 m"); F1 use the rendered pose at begin if readable (changes rule 12 「추적 불가로 시작할 때」
— user approval before the rule change); F2 correct the snapshot impact source/flip; F3 extend snapshot/RPC (out of this
task's "do not change" scope — separate design). None changes damage/target/HP.

## 5. Caster-owned attack VFX — EffectPreset 「Stop When Caster Dies」

- `EffectPreset.StopWhenCasterDies` (on for `EffectPreset_FoxMagician_Attack`, `EffectPreset_InfernoSpirit_Attack`).
  `EffectManager` binds the item to the caster (`BeginCasterOwnership(caster, caster.Data)`).
- Two layers (`VfxPoolItem.CasterOwned.cs`): UnitView death event / `OnDisable` → `EffectManager.StopCasterOwnedAttackEffects`
  stops only that caster's items; per-item `Update` safety net stops if the caster view is destroyed, inactive,
  holds different data, or is dead. Stop = stop emitting **and clear** particles, then return to pool.
- Does not apply to death / tracer / impact / tower / status VFX. User-confirmed PASS (2026-10-13, 2026-10-15).
- *(2026-10-19 added)* For charge units the same stop is also called on **attack cancel before launch** — see §8.

## 6. Commented-out old logic still present — delete ONLY after the §4-1 issue passes the user's test (user decision 2026-10-15)

R-1 `VfxPoolItem.cs` ~164 · R-5 `VfxPoolItem.cs` ~210 · R-4 `VfxPoolItem.TimedDamage.cs` ~42 · R-3
`RunUnitActionSelfValidation.cs` ~1385 · R2-X1 `VfxPoolItem.TowerBolt.cs` ~204 · R3 `UnitRecipientFitMeasureTool.cs` ~332
(line numbers as of `84b6ff2a`; search the `[R… 비활성화` / `[버그 — 2026-10-13 주석 처리]` markers).

## 7. Fixture value equality and generation boundaries

- An inactive UnitView fixture can retain default zero Quaternion metadata. Unity Quaternion `==` is orientation comparison and can reject two unchanged zero values; use `Equals` for exact unchanged components, and exercise both zero and valid identity seeds. Do not hide the fixture bug by changing production initialization.
- Assert lease lifetime separately from emission metadata (scope, target, position, rotation), then preserve charge-once/duplicate-no-consumption checks. Metadata stability alone does not prove every emitter was unreachable; retain the production early-return evidence. *(2026-10-19: the Fox hit-marker early return this refers to is now commented out — Fox hit markers launch the tracer; the old fixture asserts are kept commented. See §8.)*
- Re-read the current branch before applying an earlier fix: concurrent main integration may already contain the authored-particle contract. Preserve unrelated recipient-fit/tower work. Refresh scratch compiler inputs against the actual source set before interpreting missing-source failures.
- User full-suite PASS is a native-menu acceptance report, not an agent native run or every device/role/gameplay/HP acceptance. After authorized obsolete-assertion cleanup, compare active tokens with exact literal text and keep that no-behavior-change evidence separate from the earlier user run. Exact generations remain in the existing `2026-10-19/15_21_regression-suite-contract-fixes` Task.

## 8. Charge units — charge at `OnAttackCharge`, launch at `OnAttackHit` (2026-10-19)

Commit `8330b5d9` (+ material fixes `f899238f`, `609ba447`). Record: `Assets/_Project/Docs/_Tasks/2026-10-19/11_09_charge-unit-launch-timing/`
Research §9 / Plan §9. Rule: `GameSystemRules_Units.md` rule 17 「차지 유닛 typed marker 계약」. User acceptance: single + multi
**visual** check only (no log verification, regression-suite result not reported). Presentation only — server damage timing unchanged.

- **Detection = clip event, not a type list.** `UnitView.EnsureAttackChargeMarkerCache`: the single clip whose name ends in
  `_Attack` (more than one distinct → false) has an `OnAttackCharge` event → `_hasAttackChargeMarker`. Computed once per
  `Initialize` (reset there first for pooled reuse), or at the first marker if the Animator was missing. `AnimationClip.events`
  allocates a new array per read — never read it per marker. Do not reuse `CanUseNativeAttackPhase` for this: its clip selection
  sits inside a condition that excludes FoxMagician. Guard against silent regression: validator T-4 pins the set of `_Attack`
  clips with `OnAttackCharge` to {FoxMagician, InfernoSpirit}.
- 🔴 **Lease: a charge marker must only *check*, never consume.** One server commit arms the source lease for
  `HitFrameTimes.Length` = 1 marker. If the charge consumes it, the real hit marker is Suppressed and **no projectile fires —
  only in multiplayer** (single never uses the lease, so single tests cannot catch it). Also note
  `TryAcquirePresentationShadowMarkerScope` silently consumes the **impact** lease too — both had to be removed from the charge
  path. Check-only = `IsArmed && RemainingMarkerCount > 0 && !IsSourceOnly && revision == _lastAttackPresentationRevision
  && revision != _lastChargePresentedRevision` (once-per-revision flag, because the clip loops and an unconsumed lease would let
  the next loop's charge pass again). Impact lease: `IsArmed` is enough — while Armed at least one HitIndex remains (the last
  consume flips it to Consumed) — so no new read API was needed in `UnitAttackPresentationPolicy`.
- **Hit marker of a charge unit:** skip `PlayUnitAttack` (would respawn the charge after launch); skip the Inferno VFX-attempt
  diagnostic there (an intentional skip would be counted as a playback failure); play `PlayUnitAttackSfx` next to `PlayTracer`
  (and in the no-EffectManager branch) instead of at the attack-VFX line. SourceOnly hit → nothing at all (no VFX/SFX/tracer).
  Fox observer records (`RecordFoxMagicianTimeline*`) are limited to `chargeOnly` so the hit marker does not double-record.
- **Cancel stop placement:** `StopOwnedChargeEffects` lives in `RetireAndCloseAttackPresentationScope` and
  `PreserveCommittedSourceMarkerAndCloseTargetImpact` (`ClearCombatTargetTracking` reaches both). 🔴 Exclude reason
  `presentation-revision-replaced` — it is the **normal per-revision commit** boundary; stopping there cuts every healthy charge.
  Single `ChangeTarget` does not stop: it fires when a new attack starts, not on a cancel. Before stopping VFX at any lifecycle
  boundary, check whether the normal path also passes through it.
- **Charge prefab length = charge window** (`OnAttackHit − OnAttackCharge`; Inferno 1.3333 s, Fox 1.8333 s), because the hit marker
  no longer touches the charge. Validator T-5 computes each system's real end = (start delay + last emission + max lifetime) /
  simulation speed (last emission = duration if rate-over-time/distance, else last burst time) and requires ≤ window and within
  1/30 s of it. "Duration + max lifetime" is only a conservative bound for continuous emitters.
- **Commented-out (still in code, deletion not yet authorised):** R-1 Fox hit early return, R-2 Fox type guard in `OnAttackCharge`,
  R-3 charge-path consume, R-4 unconditional attack SFX line; validator V-1~V-3 plus several old Fox fixture asserts.
- Known limits (not bugs of this change): Fox damage number shows before tracer arrival (rule 19 immediate path — option C);
  number-only after a cancel (server damage already scheduled); Client Inferno charge may be skipped when the commit arrives after
  0.1667 s [unverified].
