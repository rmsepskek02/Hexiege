# 에셋 목록

> 완성된 에셋 현황과 추가 제작이 필요한 에셋을 관리하는 문서
> VFX / SFX 에셋 목록 → [VFXSFXList.md](VFXSFXList.md)
> VFX / SFX 제작 가이드 → [VFXSFXGuide.md](VFXSFXGuide.md)

---

## Ⅰ. 3D 모델 에셋 (3D Model Assets)

### 1. 완성된 3D 에셋

#### 건물 (Buildings)
> **프리팹 설정 기준**: Root Scale (1,1,1) / Mesh Child Rotation Y = 0

> ★ 에셋명과 BuildingType이 다릅니다. 코드에서는 BuildingType 컬럼 기준으로 동작합니다.

| 종족 | 건물명 (에셋명) | BuildingType | 팀 프리팹 | 비고 |
|------|--------------|-------------|---------|------|
| Human | Castle | Castle | Blue / Red | 본기지 |
| Human | MiningPost | MiningPost | Blue / Red | 금광 위 건설 |
| Human | CannonTower | AutoTower ★ | Blue / Red | 방어 타워 |
| Human | FlightFacility | FlightFacility | Blue / Red | 지원 건물 |
| Human | TechnicalLaboratory | Research ★ | Blue / Red | 업그레이드 건물 |
| Human | TrainingCamp | TrainingCamp | Blue / Red | 유닛 생산 건물 (근거리류 1단계) |
| Human | WarAcademy | WarAcademy | Blue / Red | 유닛 생산 건물 (근거리류 2단계) |
| Human | Barracks | HumanBarracks ★ | Blue / Red | 유닛 생산 건물 (근거리류 3단계) |
| Human | Gunsmith | Gunsmith | Blue / Red | 유닛 생산 건물 (총기류 1단계) |
| Human | Armory | Armory | Blue / Red | 유닛 생산 건물 (총기류 2단계) |
| Human | WeaponForge | WeaponForge | Blue / Red | 유닛 생산 건물 (총기류 3단계) |
| Human | Garage | Garage | Blue / Red | 유닛 생산 건물 (탈것류 1단계) |
| Human | VehicleBay | VehicleBay | Blue / Red | 유닛 생산 건물 (탈것류 2단계) |
| Spirit | SpiritNexus | Castle ★ | Blue / Red | 본기지 |
| Spirit | ManaRift | MiningPost ★ | Blue / Red | 금광 위 건설 |
| Spirit | RuneSpire | AutoTower ★ | Blue / Red | 방어 타워 |
| Spirit | MagicSpirit | MagicBuilding ★ | Blue / Red | 스킬 건물 |
| Spirit | AstronomicalSpirit | Research ★ | Blue / Red | 업그레이드 건물 |
| Spirit | FireSpire | FireSpire | Blue / Red | 유닛 생산 건물 (불 속성 1단계) |
| Spirit | BlazeConduit | BlazeConduit | Blue / Red | 유닛 생산 건물 (불 속성 2단계) |
| Spirit | InfernoCore | InfernoCore | Blue / Red | 유닛 생산 건물 (불 속성 3단계) |
| Spirit | AquaSpring | AquaSpring | Blue / Red | 유닛 생산 건물 (물 속성 1단계) |
| Spirit | TidalNexus | TidalNexus | Blue / Red | 유닛 생산 건물 (물 속성 2단계) |
| Spirit | OceanicHeart | OceanicHeart | Blue / Red | 유닛 생산 건물 (물 속성 3단계) |
| Spirit | StoneMound | StoneMound | Blue / Red | 유닛 생산 건물 (땅 속성 1단계) |
| Spirit | TerraForge | TerraForge | Blue / Red | 유닛 생산 건물 (땅 속성 2단계) |
| Spirit | GaeaSanctum | GaeaSanctum | Blue / Red | 유닛 생산 건물 (땅 속성 3단계) |
| Transcendence | ElderTree | Castle ★ | Blue / Red | 본기지 |
| Transcendence | FungalNode | MiningPost ★ | Blue / Red | 금광 위 건설 |
| Transcendence | VineTower | AutoTower ★ | Blue / Red | 방어 타워 |
| Transcendence | MistShrine | HealShrine ★ | Blue / Red | 힐 건물 |
| Transcendence | WillowShrine | MagicBuilding ★ | Blue / Red | 스킬 건물 |
| Transcendence | AncientGrove | Research ★ | Blue / Red | 업그레이드 건물 |
| Transcendence | PrimalAltar | PrimalAltar | Blue / Red | 유닛 생산 건물 (동물A 1단계) |
| Transcendence | PrimalDen | PrimalDen | Blue / Red | 유닛 생산 건물 (동물A 2단계) |
| Transcendence | PrimalSanctuary | PrimalSanctuary | Blue / Red | 유닛 생산 건물 (동물A 3단계) |
| Transcendence | FeralAltar | FeralAltar | Blue / Red | 유닛 생산 건물 (동물B 1단계) |
| Transcendence | FeralDen | FeralDen | Blue / Red | 유닛 생산 건물 (동물B 2단계) |
| Transcendence | FeralSanctuary | FeralSanctuary | Blue / Red | 유닛 생산 건물 (동물B 3단계) |
| Transcendence | SporePatch | SporePatch | Blue / Red | 유닛 생산 건물 (식물 1단계) |
| Transcendence | FloralNursery | FloralNursery | Blue / Red | 유닛 생산 건물 (식물 2단계) |

#### 유닛 (Units)
> **프리팹 설정 기준**: Root Scale (1,1,1) / Mesh Child Rotation Y = 0 

| 종족 | 유닛명 | UnitType | 팀 프리팹 | 비고 |
|------|--------|----------|---------|------|
| Human | Pistoleer | Pistoleer | Blue / Red | 권총병 |
| Human | Assault | Assault | Blue / Red | 돌격소총병 |
| Human | Sniper | Sniper | Blue / Red | 저격총병 |
| Human | LittleKnight | LittleKnight | Blue / Red | 근접 보병 |
| Human | SpearMan | SpearMan | Blue / Red | 창병 |
| Human | BattleAxe | BattleAxe | Blue / Red | 도끼병 |
| Human | Tank | Tank | Blue / Red | 중장갑 포격 전차 |
| Human | CannonCart | CannonCart | Blue / Red | 원거리 포격 수레 |
| Spirit | EmberSpirit | EmberSpirit | Blue / Red | 불정령1 |
| Spirit | FlameSpirit | FlameSpirit | Blue / Red | 불정령2 |
| Spirit | InfernoSpirit | InfernoSpirit | Blue / Red | 불정령3 |
| Spirit | DustSpirit | DustSpirit | Blue / Red | 흙정령1 |
| Spirit | BoulderSpirit | BoulderSpirit | Blue / Red | 흙정령2 |
| Spirit | QuakeSpirit | QuakeSpirit | Blue / Red | 흙정령3 |
| Spirit | TideSpirit | TideSpirit | Blue / Red | 물정령1 |
| Spirit | StreamSpirit | StreamSpirit | Blue / Red | 물정령2 |
| Spirit | TorrentSpirit | TorrentSpirit | Blue / Red | 물정령3 |
| Transcendence | FoxMagician | FoxMagician | Blue / Red | 여우마법사 |
| Transcendence | BearGuard | BearGuard | Blue / Red | 곰탱커 |
| Transcendence | LionKnight | LionKnight | Blue / Red | 사자검사 |
| Transcendence | RhinoBreaker | RhinoBreaker | Blue / Red | 돌진 탱커 |
| Transcendence | EagleArcher | EagleArcher | Blue / Red | 원거리 궁수 |
| Transcendence | RabbitTrickster | RabbitTrickster | Blue / Red | 민첩 근접 |
| Transcendence | MushroomBomber | MushroomBomber | Blue / Red | 범위 폭발 딜러 |
| Transcendence | BloomFairy | BloomFairy | Blue / Red | 힐러 |

#### 기타 오브젝트 (Misc Objects)
| 오브젝트명 | 비고 |
|-----------|------|
| GoldMineTile | 금광 타일 오브젝트 |
| HexTile | 헥스 타일 |
| RallyPointMarker | 집결지 마커 |
| EagleArrow | EagleArcher 화살 무기 서브프리팹 |
| EagleBow | EagleArcher 활 무기 서브프리팹 |
| RabbitSword | RabbitTrickster 검 무기 서브프리팹 |

#### 미사용 / 재분류 예정 (Repurposed)
> 제작은 완료되었으나 원래 역할에서 제외되어 새로운 용도를 검토 중인 에셋

| 종족 | 건물명 | 원래 BuildingType | 팀 프리팹 | 비고 |
|------|--------|-----------------|---------|------|
| Spirit | SummoningAltar | Barracks | Blue / Red | 기존 유닛 생산 건물에서 다른 용도로 재사용 예정 |
| Transcendence | HunterPlant | AncientGrove | Blue / Red | 기존 업그레이드 건물에서 다른 용도로 재사용 예정 |

### 2. 제작 예정 3D 에셋

| 종족 | 유닛명 | 역할/원소 | 비고 |
|------|--------|----------|------|
| Human | KnightRider | 돌격 기병 | 말 기사, 빠른 돌격 |
| Human | WarElephant | 초고체력 탱커 | 전쟁 코끼리, 광역 압박 |
| Spirit | Spark/Storm/Thunder | 전기 (1~3단계) | 미제작 |
| Spirit | Glow/Radiant/Aurora | 빛 (1~3단계) | 미제작 |
| Spirit | Shadow/Void/Abyss | 어둠 (1~3단계) | 미제작 |
| Transcendence | WolfScout | 정찰/러시 | 빠른 이동속도 |
| Transcendence | TigerBlade | 고화력 근접 | 높은 공격력 |
| Transcendence | TurtleShield | 방어형 | 최고 HP, 느린 이동 |

---

## Ⅱ. UI 및 스프라이트 에셋 (UI & Sprite Assets)

### 1. 유닛 초상화 (Portraits)

| 종족 | 유닛명 | 파일명 | 비고 |
|------|--------|--------|------|
| Human | Assault | assault_portrait_blue/red.png | |
| Human | BattleAxe | battleaxe_portrait_blue/red.png | |
| Human | CannonCart | cannoncart_portrait_blue/red.png | |
| Human | LittleKnight | littleknight_portrait_blue/red.png | |
| Human | Pistoleer | pistoleer_portrait_blue/red.png | |
| Human | Sniper | sniper_portrait_blue/red.png | |
| Human | SpearMan | spearman_portrait_blue/red.png | |
| Human | Tank | tank_portrait_blue/red.png | |
| Spirit | BoulderSpirit | boulderspirit_portrait_blue/red.png | |
| Spirit | DustSpirit | dustspirit_portrait_blue/red.png | |
| Spirit | EmberSpirit | emberspirit_portrait_blue/red.png | |
| Spirit | FlameSpirit | flamespirit_portrait_blue/red.png | |
| Spirit | InfernoSpirit | infernospirit_portrait_blue/red.png | |
| Spirit | QuakeSpirit | quakespirit_portrait_blue/red.png | |
| Spirit | StreamSpirit | streamspirit_portrait_blue/red.png | |
| Spirit | TideSpirit | tidespirit_portrait_blue/red.png | |
| Spirit | TorrentSpirit | torrentspirit_portrait_blue/red.png | |
| Transcendence | BearGuard | bearguard_portrait_blue/red.png | |
| Transcendence | BloomFairy | bloomfairy_portrait_blue/red.png | |
| Transcendence | EagleArcher | eaglearcher_portrait_blue/red.png | |
| Transcendence | FoxMagician | foxmagician_portrait_blue/red.png | |
| Transcendence | LionKnight | lionknight_portrait_blue/red.png | |
| Transcendence | MushroomBomber | mushroombomber_portrait_blue/red.png | |
| Transcendence | RabbitTrickster | rabbittrickster_portrait_blue/red.png | |
| Transcendence | RhinoBreaker | rhinobreaker_portrait_blue/red.png | |

### 2. 건물 아이콘 (Building Icons)

> 폴더 경로: `Assets/_Project/Sprites/Buildings/`

#### Human

| 건물명 | 파일명 | 비고 |
|--------|------|
| Armory | bld_armory_blue/red.png | |
| Barracks | bld_barracks_blue/red.png | |
| CannonTower | bld_cannontower_blue/red.png | |
| Castle | bld_castle_blue/red.png | |
| FlightFacility | bld_flightfacility_blue/red.png | |
| Garage | bld_garage_blue/red.png | |
| Gunsmith | bld_gunsmith_blue/red.png | |
| MiningPost | bld_miningpost_blue/red.png | |
| TechnicalLaboratory | bld_technicallaboratory_blue/red.png | |
| TrainingCamp | bld_trainingcamp_blue/red.png | |
| VehicleBay | bld_vehiclebay_blue/red.png | |
| WarAcademy | bld_waracademy_blue/red.png | |
| WeaponForge | bld_weaponforge_blue/red.png | |

#### Spirit

| 건물명 | 파일명 | 비고 |
|--------|------|
| AquaSpring | bld_aquaspring_blue/red.png | |
| AstronomicalSpirit | bld_astronomicalspirit_blue/red.png | |
| BlazeConduit | bld_blazeconduit_blue/red.png | |
| FireSpire | bld_firespire_blue/red.png | |
| GaeaSanctum | bld_gaeasanctum_blue/red.png | |
| MagicSpirit | bld_magicspirit_blue/red.png | |
| ManaRift | bld_manarift_blue/red.png | |
| OceanicHeart | bld_oceanicheart_blue/red.png | |
| RuneSpire | bld_runespire_blue/red.png | |
| SpiritNexus | bld_spiritnexus_blue/red.png | |
| StoneMound | bld_stonemound_blue/red.png | |
| SummoningAltar | bld_summoningaltar_blue/red.png | |
| TerraForge | bld_terraforge_blue/red.png | |

#### Transcendence

| 건물명 | 파일명 | 비고 |
|--------|------|
| ElderTree | bld_eldertree_blue/red.png | |
| FeralAltar | bld_feralaltar_blue/red.png | |
| FeralDen | bld_feralden_blue/red.png | |
| FeralSanctuary | bld_feralsanctuary_blue/red.png | |
| FloralNursery | bld_floralnursery_blue/red.png | |
| FungalNode | bld_fungalnode_blue/red.png | |
| HunterPlant | bld_hunterplant_blue/red.png | |
| MistShrine | bld_mistshrine_blue/red.png | |
| PrimalAltar | bld_primalaltar_blue/red.png | |
| PrimalDen | bld_primalden_blue/red.png | |
| PrimalSanctuary | bld_primalsanctuary_blue/red.png | |
| SporePatch | bld_sporepatch_blue/red.png | |
| VineTower | bld_vinetower_blue/red.png | |
| WillowShrine | bld_willowshrine_blue/red.png | |

#### Misc

| 오브젝트명 | 파일명 | 비고 |
|-----------|------|
| GoldMine | obj_goldmine.png | |

### 3. UI 시스템 요소 (Common UI)

| 분류 | 용도 | 파일명 |
|------|------|--------|
| Bars | HP/진행도 프레임 | ui_bar_alt_frame, ui_bar_hp_frame, ui_bar_progress_frame |
| Buttons | 버튼 배경 프레임 | ui_btn_cancel, ui_btn_gold_normal, ui_btn_primary, ui_btn_secondary |
| Icons — 자원/기능 | 인게임 HUD 아이콘 | ui_icon_gold, ui_icon_population, ui_icon_rallypoint, ui_icon_timer, ui_icon_lock, ui_icon_destroy |
| Icons — 공통 기능 | 설정/종료 아이콘 | ui_icon_settings, ui_icon_quit |
| Icons — TabBar | 로비 탭 바 아이콘 | ui_icon_tab_battle, ui_icon_tab_shop, ui_icon_tab_profile, ui_icon_tab_ranking |
| Icons — 로비 버튼 | 로비 패널 버튼 아이콘 | ui_icon_singleplay, ui_icon_randommatch, ui_icon_customgame, ui_icon_createroom, ui_icon_joinbycode, ui_icon_email, ui_icon_logout, ui_icon_back, ui_icon_cancel |
| Panels | 배경 패널 | ui_panel_dark, ui_panel_light |
| Slots | 슬롯 배경 | ui_slot_bar, ui_slot_icon_dark, ui_slot_icon_light, ui_slot_queue |
| Spinners | 로딩/매칭 대기 스피너 | ui_spinner_hexorb |

### 4. 맵 및 타일 스프라이트 (Tiles)

| 오브젝트명 | 파일명 | 비고 |
|-----------|------|
| HexTile | tile_hex.png | 기본 헥스 타일 |
| HexTile Flat | tile_hex_flat.png | 평면 헥스 타일 |

### 5. UI 프리팹 (UI Prefabs)

| 프리팹명 | 비고 |
|---------|------|
| FloatingHpText | 유닛 피격 시 표시되는 플로팅 데미지 텍스트 |
