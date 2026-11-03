# Game Programmer — Audio / SFX playback

> Topic file linked from `MEMORY.md` 「토픽 파일 인덱스」. Older sound-system entries (2026-07 bug fixes, mute)
> still live in `MEMORY.md` itself under their dated headings.

## SFX concurrency rework (2026-11-02~03, unit-attack SFX tiering task stage 1)

Source: `Assets/_Project/Docs/_Tasks/2026-11-02/13_39_unit-attack-sfx-tiering/Plan.md` §6 (spec) and §17 (result).
Rule: `GameSystemRules_Sound.md` rule 13 (rewritten 2026-11-02).

- **Entry point:** `AudioManager.PlaySfxClip(AudioClip clip, float volume = 1f, SfxPriority priority = SfxPriority.Normal)`.
  `SfxPriority` is a top-level enum in `Hexiege.Presentation` (`AudioManager.cs`): `Normal = 0`, `High = 1`.
  Only `PlayUnitAttackSfx` passes a non-default value — it reads `SoundConfig.IsUnitAttackPriority(type)`
  (new bool `UnitSoundEntry.attackIsPriority`; the data decides who is "special", never a unit list in code — rule 3).
  Death / building / UI SFX use the default `Normal`. `PlayTowerAttackSfx(BuildingType, RaceId)` (tower task
  `15_48_…`, 2026-11-03) calls this entry with `SfxPriority.High` always — details in
  [vfx-presentation.md §12](vfx-presentation.md#12-tower-attack-profile--presentation-chosen-by-attack-kind-not-race-2026-11-03).
  *(2026-11-03 updated — old: "The tower-attack task (`15_48_…`) is planned to call this entry with `SfxPriority.High` always.")*
- **Order of checks:** clip null → same-clip rules (min interval, max same clip — `High` obeys them too) →
  global limit (full: `Normal` dropped; `High` evicts the earliest-started `Normal` with a short fade; all-`High`
  active → new `High` dropped) → start.
- **Inspector knobs (AudioManager, scene `Login.unity`):** `_maxConcurrentSfx` 16 · `_maxSameClipConcurrent` 3 ·
  `_minSameClipIntervalSec` 0.05 · `_evictFadeOutSec` 0.03 (initial value, not tuned by listening yet).
  🔴 The scene value wins over the code default: changing the default to 16 did nothing until the scene value
  was changed (one-time menu, 2026-11-03).
- **Eviction pitfall (design note in the Plan §6-2):** each play has a return coroutine that frees the slot after
  `clip length + 0.1 s`. An evicted play's coroutine must not free a slot or stop a source that was already reused —
  each play carries an id and the coroutine checks it before returning.
- **Dev log:** `RecordSfxDecision` — one Info summary per ≥1 s (`IntervalDrop`, `SameClipDrop`, `GlobalDrop`,
  `AllPriorityDrop`, `Evicted`, `Active`, `Max`), `[Conditional("UNITY_EDITOR")]` + `DEVELOPMENT_BUILD`.
  Counts are minimums (the last burst is only printed when the next decision arrives).
- **Verification state (2026-11-03):** user test PASS for scope (menu run, LittleKnight playback, regression suite
  PASS by the user rule). Editor log showed only `DroppedSameClipInterval` with `Max=16`. **Same-clip-3, global
  limit and eviction paths have not run in the field** — the big-battle listening test is deferred by the user until
  more SFX exist. No unit has `attackIsPriority` on yet (no special-unit clip is connected).
- **Deferred deletions:** commented old code (six `[비활성화 2026-11-02 …]` blocks in `AudioManager.cs`), the four
  disabled AudioSources on VFX prefabs (`vfx_pistoleer_attack` · `vfx_streamspirit_attack` · `vfx_tank_attack` ·
  `vfx_unit_death`) and the one-time menu are removed only after the big-battle test passes.

## One-time menu `SfxTieringOneTimeSetup` (2026-11-03)

- `Assets/Editor/Tools/SfxTieringOneTimeSetup.cs`, menu `Hexiege/Setup/SFX Tiering - Apply One-time Asset Changes`.
  Disables the 4 VFX AudioSources, clears missing references in `SoundConfig.asset`, sets the scene limit to 16,
  and re-serializes two `EffectPreset` assets with `AssetDatabase.ForceReserializeAssets` to drop the stale
  `_sfxClip` lines (fields already commented out in code cannot be cleared through `SerializedObject`).
  Side effect seen: re-serialization also wrote 9 current-code fields with default values into those assets.
- First created under `Assets/Editor/Setup/`; Unity did not import it there (not selectable, not compiled) and it
  worked after the move to `Assets/Editor/Tools/`. Cause unconfirmed — see [architecture.md](architecture.md)
  「배치 관례와 저장 반영」.

## SFX normalizer length warning (2026-11-02, `ee7049cd`)

- `Assets/Editor/Tools/SfxNormalizeCore.cs` `TryGetAttackLengthLimit(fileName, …)`: the length cap is found from the
  **file name prefix** (bundle names from Plan §2-5, e.g. `sfx_gun_light_attack`); any other name containing
  `_attack` gets the maximum cap 1.0 s; other files are not checked. Warn only, never trim. Not yet run on real
  files (deferred to the sample re-listening step so EmberSpirit's file is not overwritten).
