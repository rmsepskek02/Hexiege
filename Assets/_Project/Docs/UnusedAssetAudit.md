# Unused Asset Audit

Generated during the build asset optimization pass.

## Scan Method

- Scanned PNG assets under `Assets/_Project/Texture` and `Assets/_Project/Sprites`.
- Extracted Unity GUIDs from `.meta` files.
- Checked GUID references from Unity YAML assets under `Assets/_Project`:
  - scenes, prefabs, materials, ScriptableObjects, controllers, sprite atlases, animations, playable assets
- Excluded documentation from the reference scan.
- Checked `Resources` and `StreamingAssets` separately because those can be included in builds without scene references.

## Findings

| Category | Result |
|---|---:|
| PNG assets scanned | 699 |
| Source PNG size scanned | 1,792.54 MB |
| Referenced PNGs | 416 / 1,073.16 MB |
| Unreferenced PNGs | 283 / 719.38 MB |
| Large media in `Resources` / `StreamingAssets` | 0 files |
| `_Old` directories found | 7 |
| `_Old` directory source size | 235.68 MB |

## Important Interpretation

Unreferenced assets outside `Resources`, `StreamingAssets`, Addressables, and scene/prefab/material references are usually not packed into Android builds. Removing them mainly reduces repository/project size, while import setting changes reduce Android build size.

## Cleanup Applied

- Repointed `Assets/_Project/Materials/Buildings/mat_miningpost.mat` away from `_Old/MiningPost1` textures:
  - `_BaseMap` / `_MainTex` now use `Texture/Buildings/Human/MiningPost/tex_miningpost_blue_base.png`.
  - `_MetallicGlossMap` now uses `Texture/Buildings/Human/MiningPost/tex_miningpost_metallic.png`.
- Verified no outside references remain to assets under `_Old` directories.
- Deleted 7 `_Old` directories under:
  - `Assets/_Project/Materials/Buildings/_Old`
  - `Assets/_Project/Models/Buildings/_Old`
  - `Assets/_Project/Models/Units/_Old`
  - `Assets/_Project/Prefabs/Buildings/_Old`
  - `Assets/_Project/Prefabs/Units/Spirit/_/_Old`
  - `Assets/_Project/Sprites/Buildings/_Old`
  - `Assets/_Project/Texture/Buildings/_Old`

## Next Candidates

The remaining largest unreferenced PNGs are mostly normal-map textures. They should not be bulk-deleted without visual/material QA because the current materials may intentionally omit them, or they may be source assets reserved for future material setup.
## Follow-up Decision: Secondary Textures and Animation Assets

Decision after user review:

- `metallic` textures: keep for now.
- `normal` textures under `Assets/_Project/Texture`: remove from the project.
- `roughness` textures under `Assets/_Project/Texture`: remove from the project.
- Model files used only as animation sources: user review required before cleanup.
- Animation clips not used by current controllers: user review required before cleanup.
- Assets that are unused now but may be useful later should be archived outside the Unity project instead of kept in `Assets`.

Applied cleanup:

- Removed 93 normal-map PNG textures and their `.meta` files from `Assets/_Project/Texture`.
- Cleared the remaining Pistoleer material `_BumpMap` references before deleting `tex_pistoleer_normal.png`.
- Removed 84 roughness PNG textures and their `.meta` files from `Assets/_Project/Texture`; no external material/prefab/scene references were found before deletion.

