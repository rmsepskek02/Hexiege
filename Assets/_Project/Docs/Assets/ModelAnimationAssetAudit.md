# Model and Animation Asset Audit

Generated during the build asset optimization pass on 2026-07-15.

## Scan Method

- Scanned FBX assets under `Assets/_Project`.
- Scanned standalone `.anim` assets under `Assets/_Project`.
- Extracted Unity GUIDs from `.meta` files.
- Counted GUID references from Unity YAML assets under:
  - `Assets/_Project/Animations`
  - `Assets/_Project/Prefabs`
  - `Assets/_Project/Scenes`
  - `Assets/_Project/Materials`
- Full CSV detail: `Assets/_Project/Docs/Assets/ModelAnimationAssetAudit.csv`.

## Summary

| Category | Result |
|---|---:|
| FBX assets scanned | 100 |
| FBX source size | 757.2 MB |
| `@` animation-source FBX assets | 16 / 3.21 MB |
| Unreferenced `@` FBX assets | 16 / 3.21 MB |
| Unreferenced non-`@` FBX assets | 2 / 22.74 MB |
| Standalone `.anim` assets scanned | 85 / 121.08 MB |
| Unreferenced standalone `.anim` assets | 0 |

## Interpretation

Standalone `.anim` files are currently referenced by Animator controllers, so they are not cleanup candidates at this stage.

The `@` FBX files look like source animation imports. They currently have no direct YAML references, but they may have been used to extract the committed `.anim` clips. Deleting them is likely safe only after confirming the extracted `.anim` clips are the authoritative runtime assets.

The unreferenced non-`@` FBX files are larger and should be visually checked before deletion because they may represent future-use buildings or prefabs not currently wired into scenes.

## User Review Candidates: `@` Animation Source FBX

| Path | Size MB | External refs |
|---|---:|---:|
| `Assets/_Project/Models/Units/Spirit/TorrentSpirit/TorrentSpirit@Breathing Idle.fbx` | 0.35 | 0 |
| `Assets/_Project/Models/Units/Spirit/QuakeSpirit/QuakeSpirit@Jump Attack.fbx` | 0.25 | 0 |
| `Assets/_Project/Models/Units/Transcendence/EagleArcher/EagleArcher@Standing Aim Overdraw.fbx` | 0.24 | 0 |
| `Assets/_Project/Models/Units/Spirit/QuakeSpirit/QuakeSpirit@Mutant Jump Attack.fbx` | 0.24 | 0 |
| `Assets/_Project/Models/Units/Spirit/BoulderSpirit/BoulderSpirit@Zombie Attack.fbx` | 0.23 | 0 |
| `Assets/_Project/Models/Units/Spirit/TorrentSpirit/TorrentSpirit@Standing 2H Magic Attack 05.fbx` | 0.23 | 0 |
| `Assets/_Project/Models/Units/Transcendence/FoxMagician/FoxMagician@Standing 2H Magic Attack 03.fbx` | 0.22 | 0 |
| `Assets/_Project/Models/Units/Spirit/StreamSpirit/StreamSpirit@Standing 2H Magic Attack 01.fbx` | 0.2 | 0 |
| `Assets/_Project/Models/Units/Human/Sniper/Sniper@Gunplay.fbx` | 0.19 | 0 |
| `Assets/_Project/Models/Units/Transcendence/BloomFairy/BloomFairy@Standing 1H Cast Spell 01.fbx` | 0.18 | 0 |
| `Assets/_Project/Models/Units/Transcendence/LionKnight/LionKnight@Sword And Shield Slash.fbx` | 0.16 | 0 |
| `Assets/_Project/Models/Units/Spirit/QuakeSpirit/QuakeSpirit@Drunk Walk Backwards.fbx` | 0.16 | 0 |
| `Assets/_Project/Models/Units/Transcendence/LionKnight/LionKnight@Great Sword Slash.fbx` | 0.15 | 0 |
| `Assets/_Project/Models/Units/Human/Assault/Assault@Walk With Rifle.fbx` | 0.14 | 0 |
| `Assets/_Project/Models/Units/Human/Assault/Assault@Firing Rifle.fbx` | 0.14 | 0 |
| `Assets/_Project/Models/Units/Transcendence/EagleArcher/EagleArcher@Standing Aim Recoil.fbx` | 0.13 | 0 |

## User Review Candidates: Unreferenced Non-`@` FBX

| Path | Size MB | External refs |
|---|---:|---:|
| `Assets/_Project/Models/Buildings/Transcendence/HunterPlant.fbx` | 11.82 | 0 |
| `Assets/_Project/Models/Buildings/Spirit/SummoningAltar.fbx` | 10.92 | 0 |

## Largest Referenced FBX Assets

These are not deletion candidates from this scan, but they dominate model source size.

| Path | Size MB | External refs |
|---|---:|---:|
| `Assets/_Project/Models/Buildings/Transcendence/WillowShrine.fbx` | 12.89 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/VineTower.fbx` | 12.58 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/ElderTree.fbx` | 12.36 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/FungalNode.fbx` | 12.33 | 2 |
| `Assets/_Project/Models/Buildings/Spirit/InfernoCore.fbx` | 12.08 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/PrimalSanctuary.fbx` | 11.83 | 2 |
| `Assets/_Project/Models/Buildings/Human/HumanBarracks.fbx` | 11.77 | 2 |
| `Assets/_Project/Models/Buildings/Spirit/BlazeConduit.fbx` | 11.73 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/PrimalDen.fbx` | 11.59 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/MistShrine.fbx` | 11.57 | 2 |
| `Assets/_Project/Models/Buildings/Human/FlightFacility.fbx` | 11.44 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/FeralSanctuary.fbx` | 11.38 | 2 |
| `Assets/_Project/Models/Buildings/Transcendence/FloralNursery.fbx` | 11.36 | 2 |
| `Assets/_Project/Models/Buildings/Human/WarAcademy.fbx` | 11.28 | 2 |
| `Assets/_Project/Models/Units/Human/Tank/Tank.fbx` | 11.27 | 2 |

## Recommended Next Step

- Do not delete standalone `.anim` files now.
- Review the 16 unreferenced `@` FBX files first; expected gain is small, about 3.21 MB.
- Review the 2 unreferenced non-`@` FBX files separately; expected gain is about 22.74 MB, but gameplay/future-use risk is higher.
- If approved, delete only reviewed FBX files and their `.meta`, then run Unity batchmode validation.
