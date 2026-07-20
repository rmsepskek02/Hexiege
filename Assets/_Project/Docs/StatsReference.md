# Hexiege — 스탯 레퍼런스

**최종 수정일:** 2026-07-20

이 문서는 사람이 읽는 스탯 미러다. 런타임 수치의 원본은 `Assets/_Project/Resources/Config/UnitStatsConfig.asset`이며, 공격 의미는 `GameSystemRules_Units.md`, 유닛별 에셋·구현 상태는 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`를 따른다. 문서와 런타임 에셋이 다르면 에셋을 무조건 정답으로 간주하지 말고 불일치로 기록해 게임 의도를 확인한다.

---

## 자원 시스템

| 항목 | 값 |
|------|----|
| 시작 골드 | 500 |
| 기본 수입 | 0 골드/초 |
| 채굴소 수입 | 10 골드/초 (채굴소 1개당, 전 팩션 동일) |

---

## 공격 프로필 수치 표기

공격은 다음 축을 별도로 가진다.

- Delivery: MeleeContact / Hitscan / ProjectileImpact / TravelingArea
- TargetScope: Single / Area
- AreaShape: Cone / Circle / Rectangle 등
- Effect: Damage / Heal / Status
- ApplicationSchedule: Instant / MultiImpact / Periodic / ImpactThenPeriodic / ContactOncePerTarget

“착탄형 AoE”처럼 전달 방식과 범위를 합친 과거 용어는 사용하지 않는다. 범위는 같은 타일 소속이 아니라 권위 AimDirection 또는 ImpactPoint 기준 XZ 월드 좌표로 계산한다.

### 사거리

```text
AttackRange ≤ AcquireRange < LoseRange
```

- 현재 표의 `감지 사거리`는 AcquireRange다.
- 공격 사거리와 감지 사거리가 같은 값도 유효하다.
- LoseRange 기본값은 AcquireRange를 월드 단위로 변환한 값에 0.25 world unit을 더한다.
- 공격 판정 공통 `RangeEpsilon = 0.05 world unit`, Legacy 건물 대상 반경은 `0.20 world unit`이다. 타겟 유지용 LoseRange 여유값과 혼용하지 않는다.

### 공격 시간

- `행동 마커 시점`: 권위 AttackTimeline에서 Windup 시작부터 MeleeContact/Hitscan의 결과 Impact 또는 Projectile/TravelingArea의 Launch/Activation까지의 `ActionMarkerOffset`
- Projectile/TravelingArea의 실제 결과 도착 시점(`ResultImpactTime`)은 발사·발동 마커와 별도이며, 비행·이동 판정으로 결정한다.
- `공격 쿨다운`: 일반 유닛은 Windup 커밋부터 다음 Windup 커밋 가능 시점까지의 전체 주기
- Animation Event는 표현·검증 marker이며 타격시점의 권위 원본이 아니다.
- 완성 유닛은 권위 `ActionMarkerOffset`과 실제 Attack state clip marker가 1 animation frame 이내로 일치해야 한다.
- 표의 기존 값과 클립이 다르면 `UnitCombatAssetMatrix.md`에 불일치로 기록한다.

---

## 유닛 — 인간계 (Human)

> **공격 쿨다운 컬럼 표기**: `행동 마커 시점(쿨다운)` — 콜론은 소수점(초 단위). 예: `0:25, 1:15(3:00)` = 권위 결과 Impact 또는 Launch/Activation 마커 0.25초·1.15초, 전체 주기 3.0초. Projectile/TravelingArea의 실제 결과 도착은 별도다. 클립 marker와의 실제 일치 여부는 에셋 매트릭스를 확인한다.

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| 권총병 (Pistoleer) | 30 | 6 | 1.0 | 1.0 | 0.5 | 0:80(2:00) | 5초 | 50 | 1 | |
| 돌격소총병 (Assault) | 40 | 1 | 2.0 | 2.0 | 1 | 0:20(0:20) ⚠️ | 10초 | 100 | 1 | 런타임 UnitStatsConfig 값. 기존 문서 0.17(0.33)과 충돌하며 Attack marker 0.1667s는 30fps 1 frame 경계 — 의도 주기 재확정 필요. |
| 저격총병 (Sniper) | 30 | 18 | 5.0 | 5.0 | 0.25 | 1:73(3:00) | 20초 | 200 | 1 | |
| 근접기사 (LittleKnight) | 35 | 4 | 0.5 | 1.0 | 1 | 0:25, 1:15(3:00) | 5초 | 50 | 1 | 2히트 공격 |
| 창병 (SpearMan) | 50 | 10 | 1.0 | 1.5 | 1 | 0:24(2:00) | 10초 | 100 | 1 | |
| 도끼병 (BattleAxe) | 80 | 15 | 0.75 | 1.0 | 1 | 1:17(3:05) | 20초 | 200 | 1 | MeleeContact · Area/Cone. 설정 1.1667s와 실제 기본 Attack marker 1.02s가 불일치하므로 타임라인 교정 필요. |
| 전차 (Tank) | 100 | 30 | 3.0 | 4.0 | 1 | 0:17(4:00) | 30초 | 400 | 1 | 건물에 2배 대미지 |
| 포격수레 (CannonCart) | 50 | 20 | 3.0 | 4.0 | 1 | 0:17(4:00) | 20초 | 150 | 1 | 건물에 2배 대미지 |

## 유닛 — 정령계 (Spirit)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| EmberSpirit | 35 | 6 | 0.5 | 1.0 | 0.5 | 1:00(2:20) | 5초 | 50 | 1 | |
| FlameSpirit | 35 | 3 | 0.5 | 1 | 2 | 0:20,1:05,1:13,1:20,1:28,2:03(3:00) | 10초 | 100 | 1 | 6히트 공격 |
| InfernoSpirit | 60 | 25 | 4.0 | 4.0 | 1 | 1:15(3:00) | 30초 | 400 | 1 | 목표: 직접 피해 + DoT 5/초×3초. 특수 핸들러 미등록, 설정 1.15s와 Attack marker 0.50s 불일치 — Incomplete. |
| DustSpirit | 40 | 6 | 0.5 | 1.0 | 0.5 | 1:04(3:00) | 5초 | 50 | 1 | |
| BoulderSpirit | 90 | 8 | 0.5 | 1.0 | 0.5 | 1:15(4:00) | 15초 | 100 | 1 | |
| QuakeSpirit | 250 | 20 | 0.5 | 1.0 | 0.5 | 1:20(5:00) | 30초 | 400 | 1 | 목표: MeleeContact GroundImpact · Area/Circle, 중심 100%·주변 50%. **UnitStatsConfig 항목과 Attack marker가 모두 없어 현재 런타임 수치가 이 표와 다를 수 있음 — Critical Incomplete.** |
| TideSpirit | 30 | 7 | 0.5 | 1.0 | 1 | 1:15(3:00) | 5초 | 50 | 1 | |
| StreamSpirit | 30 | 6 | 3.0 | 3.0 | 0.5 | 0:17(1:15) | 10초 | 100 | 1 | |
| TorrentSpirit | 100 | 20 | 3.0 | 3.0 | 0.5 | 0:50(4:00) | 30초 | 400 | 1 | TravelingArea · Area/Rectangle · 적 피해+아군 회복 · ContactOncePerTarget. 현재 서버 전선 로직은 유지하되 ActionSequence·접촉 ID로 이전 필요 — MigrationRequired. |

## 유닛 — 초월계 (Transcendence)

| 유닛 | HP | 공격력 | 공격 사거리 (타일) | 감지 사거리 (타일) | 이동속도 (칸/초) | 공격 쿨다운 (초) | 생산 시간 | 골드 비용 | 인구 | 비고 |
|------|----|--------|--------------------|--------------------|------------------|------------------|-----------|-----------|------|------|
| FoxMagician | 20 | 15 | 3.0 | 3.0 | 0.5 | 2:25(4:00) | 5초 | 70 | 1 | |
| BearGuard | 200 | 6 | 0.5 | 1.0 | 1 | 0:20(1:20) | 40초 | 400 | 1 | |
| LionKnight | 50 | 9 | 0.5 | 1.0 | 2 | 0:22,1:08(3:00) | 15초 | 200 | 1 | 2히트 공격 |
| RhinoBreaker | 60 | 10 | 0.5 | 1.0 | 2 | 1:05(2:00) | 20초 | 200 | 1 | |
| EagleArcher | 35 | 6 | 3.0 | 3.0 | 1 | 0:10(1:00) | 15초 | 150 | 1 | |
| RabbitTrickster | 20 | 6 | 0.5 | 1.0 | 2 | 0:18(2:00) | 5초 | 50 | 1 | |
| MushroomBomber | 40 | 10 | 2.0 | 2.0 | 1 | 1:00(3:00) | 15초 | 200 | 1 | 목표: ProjectileImpact/LockedPoint · Single direct + Area/Circle. 주 타겟 직접 10 + 반경 적 유닛 DoT 2/초×3초. 권위 비행·착탄, ImpactHitRadius, Attack marker, 투사체·폭발 VFX가 없어 **Incomplete**이며 v2 Migration도 필요. |
| BloomFairy | 50 | - | 4.0 | 4.0 | 1 | 1:00(3:00) ⚠️예외 | 20초 | 150 | 1 | 목표: Hitscan cast · Single Heal · Periodic HoT 20/3초. 성공 주기만 Windup 1.0초 + Impact 후 쿨다운 3.0초 = 4.0초. 현재 회복 로직은 있으나 Attack marker가 없어 표현 교정 및 규칙 v2 이전 필요. |

---

## 건물 — 인간계 (Human)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (Castle) | Castle | 200 | - | - |
| 채굴소 (MiningPost) | MiningPost | 100 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 근거리A 1단계 (TrainingCamp) | TrainingCamp | 100 | 100 | 100 |
| 근거리A 2단계 (WarAcademy) | WarAcademy | 200 | - | 200 |
| 근거리A 3단계 (HumanBarracks) | HumanBarracks | 300 | - | - |
| 총기류 1단계 (Gunsmith) | Gunsmith | 100 | 100 | 100 |
| 총기류 2단계 (Armory) | Armory | 200 | - | 200 |
| 총기류 3단계 (WeaponForge) | WeaponForge | 300 | - | - |
| 탈것류 1단계 (Garage) | Garage | 100 | 200 | 300 |
| 탈것류 2단계 (VehicleBay) | VehicleBay | 200 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 자동포탑 (CannonTower) | AutoTower | 50 | 150 | 15 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 비행시설 (FlightFacility) | FlightFacility | 100 | 200 | - | - |
| 기술연구소 (TechnicalLaboratory) | Research | 100 | 200 | - | - |

---

## 건물 — 정령계 (Spirit)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (SpiritNexus) | Castle | 150 | - | - |
| 채굴소 (ManaRift) | MiningPost | 50 | 50 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 불 1단계 (FireSpire) | FireSpire | 50 | 75 | 200 |
| 불 2단계 (BlazeConduit) | BlazeConduit | 100 | - | 400 |
| 불 3단계 (InfernoCore) | InfernoCore | 400 | - | - |
| 물 1단계 (AquaSpring) | AquaSpring | 50 | 75 | 200 |
| 물 2단계 (TidalNexus) | TidalNexus | 100 | - | 400 |
| 물 3단계 (OceanicHeart) | OceanicHeart | 400 | - | - |
| 땅 1단계 (StoneMound) | StoneMound | 50 | 75 | 200 |
| 땅 2단계 (TerraForge) | TerraForge | 100 | - | 400 |
| 땅 3단계 (GaeaSanctum) | GaeaSanctum | 400 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (RuneSpire) | AutoTower | 150 | 200 | 15 | 4.0 | 3.5s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 마법건물 (MagicSpirit) | MagicBuilding | 100 | 200 | - | - |
| 연구건물 (AstronomicalSpirit) | Research | 100 | 200 | - | - |

---

## 건물 — 초월계 (Transcendence)

### 기지 & 채굴소

| 건물 | BuildingType | HP | 건설 비용 | 수입(골드/초) |
|------|-------------|-----|-----------|--------------|
| 본기지 (ElderTree) | Castle | 300 | - | - |
| 채굴소 (FungalNode) | MiningPost | 150 | 100 | 10 |

### 생산 건물

| 건물 | BuildingType | HP | 건설 비용 | 업그레이드 비용 |
|------|-------------|-----|-----------|----------------|
| 동물A 1단계 (PrimalAltar) | PrimalAltar | 150 | 125 | 200 |
| 동물A 2단계 (PrimalDen) | PrimalDen | 300 | - | 300 |
| 동물A 3단계 (PrimalSanctuary) | PrimalSanctuary | 400 | - | - |
| 동물B 1단계 (FeralAltar) | FeralAltar | 150 | 125 | 200 |
| 동물B 2단계 (FeralDen) | FeralDen | 300 | - | 300 |
| 동물B 3단계 (FeralSanctuary) | FeralSanctuary | 400 | - | - |
| 식물 1단계 (SporePatch) | SporePatch | 150 | 125 | 200 |
| 식물 2단계 (FloralNursery) | FloralNursery | 300 | - | - |

### 방어 건물

| 건물 | BuildingType | HP | 건설 비용 | 공격력 | 공격 사거리 | 공격 쿨다운 |
|------|-------------|-----|-----------|--------|------------|------------|
| 방어포탑 (VineTower) | AutoTower | 100 | 175 | 15 | 4.0 | 5.0s |

### 특수 건물

| 건물 | BuildingType | HP | 건설 비용 | 힐량 | 효과 |
|------|-------------|-----|-----------|------|------|
| 힐 건물 (MistShrine) | HealShrine | 50 | 100 | 1 HP/s (범위 3) | - |
| 마법건물 (WillowShrine) | MagicBuilding | 150 | 200 | - | - |
| 연구건물 (AncientGrove) | Research | 150 | 200 | - | - |
