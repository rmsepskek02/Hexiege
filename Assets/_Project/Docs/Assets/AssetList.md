# 에셋 목록

> 완성된 에셋 현황과 추가 제작이 필요한 에셋을 관리하는 문서

---

## 완성된 에셋

### 건물

> **프리팹 설정 기준**: Root Scale (1,1,1) / Mesh Child Rotation Y = 0

| 종족 | 건물명 | BuildingType | 팀 프리팹 | FBX Scale Factor | 비고 |
|------|--------|-------------|---------|-----------------|------|
| Human | Castle | Castle | Blue / Red | 50 | 본기지 |
| Human | Barracks | Barracks | Blue / Red | 40 | 유닛 생산 건물 |
| Human | MiningPost | MiningPost | 팀 공용 | 35 | 금광 위 건설 |
| Spirit | SpiritNexus | Castle | Blue / Red | 30 | 본기지 |
| Spirit | SummoningAltar | Barracks | Blue / Red | 30 | 유닛 생산 건물 |
| Spirit | ManaRift | MiningPost | Blue / Red | 40 | 금광 위 건설 |
| Transcendence | ElderTree | Castle | Blue / Red | 40 | 본기지 |
| Transcendence | HunterPlant | Barracks | Blue / Red | 40 | 유닛 생산 건물 |
| Transcendence | FungalNode | MiningPost | Blue / Red | 35 | 금광 위 건설 |

### 유닛

> **프리팹 설정 기준**: Root Scale (1,1,1) / Mesh Child Rotation Y = 30 / UnitView _meshYOffset = 30

| 종족 | 유닛명 | UnitType | 팀 프리팹 | FBX Scale Factor | 비고 |
|------|--------|----------|---------|-----------------|------|
| Human | Pistoleer | Pistoleer | Blue / Red | 0.25 | 권총병 |
| Human | Assault | Assault | Blue / Red | 0.25 | 돌격소총병 |
| Human | Sniper | Sniper | Blue / Red | 0.25 | 저격총병 |
| Spirit | EmberSpirit | Pistoleer | Blue / Red | 0.25 | 불정령1 |
| Spirit | FlameSpirit | Assault | Blue / Red | 0.35 | 불정령2 |
| Spirit | InfernoSpirit | Sniper | Blue / Red | 0.5 | 불정령3 |
| Transcendence | FoxMagician | Pistoleer | Blue / Red | 0.25 | 여우마법사 |
| Transcendence | BearGuard | Assault | Blue / Red | 0.4 | 곰탱커 |
| Transcendence | LionKnight | Sniper | Blue / Red | 0.25 | 사자검사 |

### 기타 오브젝트

| 오브젝트명 | FBX Scale Factor | 비고 |
|-----------|-----------------|------|
| GoldMineTile | 30 | 금광 타일 오브젝트 |
| HexTile | - | 헥스 타일 |
| RallyPointMarker | 25 | 집결지 마커 |

---

## 제작 예정 에셋

### 건물

| 종족 | 건물명 | 역할 | 비고 |
|------|--------|------|------|
| Human | WatchTower | 방어 타워 | 자동 원거리 공격 |
| Human | Alchemist | 마법 건물 | 액티브 스킬 사용 |
| Human | Forge | 업그레이드 건물 | 전역 유닛/마법 강화 |
| Spirit | RuneSpire | 방어 타워 | 자동 원거리 공격 |
| Spirit | SpiritSanctum | 마법 건물 | 액티브 스킬 사용 |
| Spirit | ArcaneVault | 업그레이드 건물 | 전역 유닛/마법 강화 |
| Transcendence | VineTower | 방어 타워 | 덩굴로 적 공격 |
| Transcendence | MistShrine | 힐 건물 | 주변 아군 체력 회복 (물 안개 효과) |
| Transcendence | WillowShrine | 마법 건물 | 액티브 스킬 사용 |
| Transcendence | AncientGrove | 업그레이드 건물 | 전역 유닛/마법 강화 |

### 유닛

#### 정령계 — 원소별 3단계

| 종족 | 유닛명 | 단계 | 원소 |
|------|--------|------|------|
| Spirit | TideSpirit | 1단계 | 물 |
| Spirit | StreamSpirit | 2단계 | 물 |
| Spirit | TorrentSpirit | 3단계 | 물 |
| Spirit | SparkSpirit | 1단계 | 전기 |
| Spirit | StormSpirit | 2단계 | 전기 |
| Spirit | ThunderSpirit | 3단계 | 전기 |
| Spirit | DustSpirit | 1단계 | 흙 |
| Spirit | BoulderSpirit | 2단계 | 흙 |
| Spirit | QuakeSpirit | 3단계 | 흙 |
| Spirit | GlowSpirit | 1단계 | 빛 |
| Spirit | RadiantSpirit | 2단계 | 빛 |
| Spirit | AuroraSpirit | 3단계 | 빛 |
| Spirit | ShadowSpirit | 1단계 | 어둠 |
| Spirit | VoidSpirit | 2단계 | 어둠 |
| Spirit | AbyssSpirit | 3단계 | 어둠 |

#### 초월계 — 동물 유닛

| 종족 | 유닛명 | 역할 | 비고 |
|------|--------|------|------|
| Transcendence | WolfScout | 정찰/러시 | 빠른 이동속도 |
| Transcendence | RhinoBreaker | 돌진 탱커 | 높은 HP, 돌진 공격 |
| Transcendence | EagleArcher | 원거리 | 긴 사거리 |
| Transcendence | TigerBlade | 고화력 근접 | 높은 공격력 |
| Transcendence | TurtleShield | 방어형 | 최고 HP, 느린 이동 |
| Transcendence | RabbitTrickster | 민첩 근접 | 단검, 빠른 속도 |

#### 초월계 — 식물 유닛

| 종족 | 유닛명 | 역할 | 비고 |
|------|--------|------|------|
| Transcendence | VineCrawler | 근접/재생 | 느리지만 체력 재생 |
| Transcendence | MushroomBomber | 범위 딜 | 독/폭발 공격 |
| Transcendence | BloomFairy | 힐러 | 아군 체력 회복 |

#### 인간계 — 근거리 유닛

| 종족 | 유닛명 | 역할 | 비고 |
|------|--------|------|------|
| Human | IronSwordsman | 근접 | 검, 기본 근접 딜러 |
| Human | SpearGuard | 근접 | 창, 긴 근접 사거리 |
| Human | BattleAxe | 근접 | 도끼, 느리지만 광역 |

#### 인간계 — 탈것 유닛

| 종족 | 유닛명 | 역할 | 비고 |
|------|--------|------|------|
| Human | KnightRider | 돌격 기병 | 말 기사, 빠른 돌격 |
| Human | CannonCart | 원거리 포격 | 대포 수레, 범위 공격 |
| Human | WarElephant | 초고체력 탱커 | 전쟁 코끼리, 광역 압박 |
