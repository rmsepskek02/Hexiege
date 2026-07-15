# Build Asset Optimization Report

Generated during the `codex/build-asset-size-optimization` branch audit.

> 최종 AAB 용량 결과와 적용/롤백 기준은 `AABSizeOptimization.md`를 권위 문서로 본다. 이 문서는 빌드 에셋 최적화 과정의 감사 및 중간 리포트다.

## Applied Scope

- Target roots: `Assets/_Project/Texture`, `Assets/_Project/Sprites`
- Excluded folders: paths containing `/_Old/`
- Optimized PNG import meta files: 670
- Source PNG total in scope: 1,723.28 MB

## Android Import Rules

- 3D material textures: Android override enabled, max texture size `1024`, automatic compressed format.
- UI sprites: Android override enabled, max texture size `1024`, automatic compressed format.
- UI backgrounds and store graphics: Android override enabled, max texture size `2048`, automatic compressed format.
- Crunch compression remains disabled to avoid runtime/decode tradeoffs changing unexpectedly.

## Largest Source PNGs In Scope

| Source size | Path |
|---:|---|
| 9.15 MB | `Assets/_Project/Texture/Buildings/Spirit/SummoningAltar/tex_summoningaltar_blue_base.png` |
| 9.01 MB | `Assets/_Project/Texture/Buildings/Transcendence/ElderTree/tex_eldertree_red_base.png` |
| 8.95 MB | `Assets/_Project/Texture/Buildings/Transcendence/HunterPlant/tex_hunterplant_red_base.png` |
| 8.89 MB | `Assets/_Project/Texture/Buildings/Spirit/ManaRift/tex_manarift_blue_base.png` |
| 8.83 MB | `Assets/_Project/Texture/Buildings/Transcendence/FungalNode/tex_fungalnode_red_base.png` |
| 8.34 MB | `Assets/_Project/Texture/Buildings/Transcendence/VineTower/VineTower/tex_vinetower_normal.png` |
| 8.34 MB | `Assets/_Project/Texture/Buildings/Transcendence/ElderTree/tex_eldertree_normal.png` |
| 8.26 MB | `Assets/_Project/Texture/Units/Human/Sniper/tex_sniper_base_blue.png` |
| 8.26 MB | `Assets/_Project/Texture/Buildings/Transcendence/FungalNode/tex_fungalnode_normal.png` |
| 8.12 MB | `Assets/_Project/Texture/Buildings/Transcendence/HunterPlant/tex_hunterplant_normal.png` |
| 8.09 MB | `Assets/_Project/Texture/Buildings/Transcendence/PrimalSanctuary/tex_primalsanctuary_normal.png` |
| 7.97 MB | `Assets/_Project/Texture/Units/Spirit/QuakeSpirit/tex_quakespirit_normal.png` |
| 7.89 MB | `Assets/_Project/Texture/Buildings/Transcendence/PrimalDen/tex_primalden_normal.png` |
| 7.88 MB | `Assets/_Project/Texture/Buildings/Spirit/BlazeConduit/tex_blazeconduit_normal.png` |
| 7.83 MB | `Assets/_Project/Texture/Buildings/Spirit/InfernoCore/tex_infernocore_normal.png` |
| 7.81 MB | `Assets/_Project/Texture/Buildings/Human/HumanBarracks/tex_humanbarracks _normal.png` |
| 7.74 MB | `Assets/_Project/Texture/Buildings/Human/FlightFacility/tex_flightfacility_normal.png` |
| 7.67 MB | `Assets/_Project/Texture/Units/Human/Pistoleer/tex_pistoleer_base_blue.png` |
| 7.67 MB | `Assets/_Project/Texture/Units/Transcendence/RabbitTrickster/RabbitSword/tex_rabbitsword_normal.png` |
| 7.63 MB | `Assets/_Project/Texture/Units/Transcendence/BearGuard/tex_bearguard_normal.png` |
| 7.62 MB | `Assets/_Project/Texture/Buildings/Human/WarAcademy/tex_waracademy_normal.png` |
| 7.62 MB | `Assets/_Project/Texture/Units/Transcendence/EagleArcher/tex_eaglearcher_normal.png` |
| 7.61 MB | `Assets/_Project/Texture/Buildings/Human/MiningPost/tex_miningpost_normal.png` |
| 7.61 MB | `Assets/_Project/Texture/Buildings/Transcendence/FeralSanctuary/tex_feralsanctuary_normal.png` |
| 7.59 MB | `Assets/_Project/Texture/Units/Human/Assault/tex_assult_base_blue.png` |
| 7.58 MB | `Assets/_Project/Texture/Buildings/Spirit/TerraForge/tex_terraforge_normal.png` |
| 7.57 MB | `Assets/_Project/Texture/Buildings/Transcendence/MistShrine/tex_mistshrine_normal.png` |
| 7.56 MB | `Assets/_Project/Texture/Units/Human/Tank/tex_tank_normal.png` |
| 7.56 MB | `Assets/_Project/Texture/Buildings/Transcendence/WillowShrine/tex_willowshrine_normal.png` |
| 7.55 MB | `Assets/_Project/Texture/Buildings/Human/WeaponForge/tex_weaponforge_normal.png` |
| 7.55 MB | `Assets/_Project/Texture/Buildings/Transcendence/FloralNursery/tex_floralnursery_normal.png` |
| 7.54 MB | `Assets/_Project/Texture/Buildings/Transcendence/FeralDen/tex_feralden_normal.png` |
| 7.53 MB | `Assets/_Project/Texture/Buildings/Transcendence/VineTower/VineTower/tex_vinetower_blue_base.png` |
| 7.50 MB | `Assets/_Project/Texture/Units/Human/Cannon/tex_cannon_normal.png` |
| 7.49 MB | `Assets/_Project/Texture/Buildings/Spirit/SummoningAltar/tex_summoningaltar_normal.png` |
| 7.46 MB | `Assets/_Project/Texture/Buildings/Spirit/SpiritNexusRed/tex_spiritnexus_red_normal.png` |
| 7.45 MB | `Assets/_Project/Texture/Buildings/Spirit/SpiritNexusBlue/tex_spiritnexus_blue_normal.png` |
| 7.44 MB | `Assets/_Project/Texture/Units/Transcendence/EagleArcher/EagleBow/tex_eaglebow_normal.png` |
| 7.43 MB | `Assets/_Project/Texture/Units/Human/Assault/Rifle/tex_rifle_base.png` |
| 7.41 MB | `Assets/_Project/Texture/Units/Spirit/BoulderSpirit/tex_boulderspirit_normal.png` |
