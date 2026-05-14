# Research: 유닛/건물 스탯 적용 및 UI 표기

**작업일:** 2026-04-12  
**기준 문서:** `Assets/_Project/Docs/StatsReference.md`

---

## 1. 현재 스탯 시스템 구조

### 1.1 데이터 파일 위치

| 파일 | 역할 | 경로 |
|------|------|------|
| `UnitStats.cs` | 전투 스탯 (HP, 공격력, 사거리, 이동속도, 쿨다운, 타격 프레임) | `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs` |
| `UnitProductionStats.cs` | 생산 스탯 (생산 시간, 골드 비용, 인구) | `Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs` |
| `BuildingStats.cs` | 건물 HP | `Assets/_Project/Scripts/Domain/Building/BuildingStats.cs` |
| `BuildingType.cs` | 건물 타입 enum | `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` |

### 1.2 스탯 적용 흐름
- `UnitSpawnUseCase`가 유닛 생성 시 `UnitStats.*` 메서드를 호출하여 `UnitData`에 값을 주입
- `BuildingPlacementUseCase`가 건물 생성 시 `BuildingStats.GetMaxHp()`를 호출하여 `BuildingData`에 주입
- 생산 비용/시간은 `UnitProductionStats.*`에서 조회

---

## 2. 현재 코드 값 vs StatsReference.md 기준값 비교

### 2.1 유닛 전투 스탯 (UnitStats.cs)

#### 인간계 (Human)

| 유닛 | 항목 | 현재 코드 값 | StatsReference 값 | 일치 여부 |
|------|------|-------------|-------------------|----------|
| Pistoleer | HP | 30 | 30 | ✅ |
| Pistoleer | 공격력 | 6 | 6 | ✅ |
| Pistoleer | 사거리 | 1.0 | 1.0 | ✅ |
| Pistoleer | 이동속도 | **1.0** | **0.5** | ❌ 불일치 |
| Pistoleer | 공격 쿨다운 | 2.0 | 2.0 | ✅ |
| Assault | HP | 50 | 50 | ✅ |
| Assault | 공격력 | 1 | 1 | ✅ |
| Assault | 사거리 | 2.0 | 2.0 | ✅ |
| Assault | 이동속도 | 1.0 | 1.0 | ✅ |
| Assault | 공격 쿨다운 | 0.2 | 0.2 | ✅ |
| Sniper | HP | 30 | 30 | ✅ |
| Sniper | 공격력 | 10 | 10 | ✅ |
| Sniper | 사거리 | 5.0 | 5.0 | ✅ |
| Sniper | 이동속도 | 0.25 | 0.25 | ✅ |
| Sniper | 공격 쿨다운 | 3.0 | 3.0 | ✅ |

#### 정령계 (Spirit) — 플레이스홀더 적용 필요

| 유닛 | 항목 | 현재 코드 값 | StatsReference 값 | 일치 여부 |
|------|------|-------------|-------------------|----------|
| EmberSpirit | HP | **30** | **30** | ✅ |
| EmberSpirit | 공격력 | **5** | **5** | ✅ |
| EmberSpirit | 사거리 | 0.5 | 0.5 | ✅ |
| EmberSpirit | 이동속도 | 0.5 | 0.5 | ✅ |
| EmberSpirit | 공격 쿨다운 | 2.33 | 2.33 (2:20) | ✅ |
| FlameSpirit | HP | **30** | **50** | ❌ 불일치 |
| FlameSpirit | 공격력 | **5** | **2** | ❌ 불일치 |
| FlameSpirit | 사거리 | 0.5 | 0.5 | ✅ |
| FlameSpirit | 이동속도 | 2.0 | 2.0 | ✅ |
| FlameSpirit | 공격 쿨다운 | 3.0 | 3.0 (3:00) | ✅ |
| InfernoSpirit | HP | **30** | **100** | ❌ 불일치 |
| InfernoSpirit | 공격력 | **5** | **25** | ❌ 불일치 |
| InfernoSpirit | 사거리 | 4.0 | 4.0 | ✅ |
| InfernoSpirit | 이동속도 | 1.0 | 1.0 | ✅ |
| InfernoSpirit | 공격 쿨다운 | 3.0 | 3.0 (3:00) | ✅ |

#### 초월계 (Transcendence) — 플레이스홀더 적용 필요

| 유닛 | 항목 | 현재 코드 값 | StatsReference 값 | 일치 여부 |
|------|------|-------------|-------------------|----------|
| BearGuard | HP | **30** | **200** | ❌ 불일치 |
| BearGuard | 공격력 | **5** | **10** | ❌ 불일치 |
| BearGuard | 사거리 | 0.5 | 0.5 | ✅ |
| BearGuard | 이동속도 | 1.0 | 1.0 | ✅ |
| BearGuard | 공격 쿨다운 | 1.33 | 1.33 (1:20) | ✅ |
| FoxMagician | HP | **30** | **20** | ❌ 불일치 |
| FoxMagician | 공격력 | **5** | **8** | ❌ 불일치 |
| FoxMagician | 사거리 | 3.0 | 3.0 | ✅ |
| FoxMagician | 이동속도 | 0.5 | 0.5 | ✅ |
| FoxMagician | 공격 쿨다운 | 4.0 | 4.0 (4:00) | ✅ |
| LionKnight | HP | **30** | **50** | ❌ 불일치 |
| LionKnight | 공격력 | **5** | **9** | ❌ 불일치 |
| LionKnight | 사거리 | 0.5 | 0.5 | ✅ |
| LionKnight | 이동속도 | 2.0 | 2.0 | ✅ |
| LionKnight | 공격 쿨다운 | 2.33 | 2.33 (2:20 첫 히트 기준, 클립 총 3:00) | ✅ |

### 2.2 유닛 생산 스탯 (UnitProductionStats.cs)

| 유닛 | 항목 | 현재 코드 값 | StatsReference 값 | 일치 여부 |
|------|------|-------------|-------------------|----------|
| Pistoleer | 생산 시간 | 5 | 5 | ✅ |
| Pistoleer | 골드 비용 | 50 | 50 | ✅ |
| Assault | 생산 시간 | 10 | 10 | ✅ |
| Assault | 골드 비용 | 100 | 100 | ✅ |
| Sniper | 생산 시간 | 15 | 15 | ✅ |
| Sniper | 골드 비용 | 200 | 200 | ✅ |
| EmberSpirit | 생산 시간 | **10** | **5** | ❌ 불일치 |
| EmberSpirit | 골드 비용 | **100** | **50** | ❌ 불일치 |
| FlameSpirit | 생산 시간 | **5** | **15** | ❌ 불일치 |
| FlameSpirit | 골드 비용 | **50** | **200** | ❌ 불일치 |
| InfernoSpirit | 생산 시간 | **15** | **30** | ❌ 불일치 |
| InfernoSpirit | 골드 비용 | **200** | **500** | ❌ 불일치 |
| BearGuard | 생산 시간 | **5** | **25** | ❌ 불일치 |
| BearGuard | 골드 비용 | **50** | **400** | ❌ 불일치 |
| FoxMagician | 생산 시간 | **10** | **5** | ❌ 불일치 |
| FoxMagician | 골드 비용 | **100** | **50** | ❌ 불일치 |
| LionKnight | 생산 시간 | **15** | **15** | ✅ |
| LionKnight | 골드 비용 | **200** | **200** | ✅ |

> **메모:** Spirit/Transcendence 유닛 순서가 플레이스홀더 적용 시 인간계 유닛 순서(5초/10초/15초)와 동일하게 설정됨.
> 실제 StatsReference.md의 정렬 순서는 "저렴한 유닛 → 비싼 유닛" 기준이므로 순서가 역전되어 있음.

### 2.3 건물 스탯 (BuildingStats.cs)

#### 현재 BuildingType enum

```
Castle, Barracks, MiningPost
```

종족별 건물 구분 없음 — 인간계/정령계/초월계 모두 동일한 BuildingType으로 처리.

#### HP 비교

| BuildingType | 현재 코드 값 | Human | Spirit | Transcendence |
|--------------|-------------|-------|--------|--------------|
| Castle | 100 | 100 ✅ | 100 ✅ | **200** ❌ |
| Barracks | 30 | 30 ✅ | 30 ✅ | **50** ❌ |
| MiningPost | 20 | 20 ✅ | 20 ✅ | **40** ❌ |

> **핵심 이슈:** 초월계 건물 HP가 Human/Spirit과 다름.
> 현재 `BuildingStats.GetMaxHp(BuildingType)`는 종족 정보를 받지 않으므로
> 초월계 건물에 다른 HP를 적용하려면 **메서드 시그니처 변경**이 필요.
> `BuildingData`는 `TeamId`를 알고 있고, `GameRaceContext`로 `RaceId`를 조회 가능.

---

## 3. UI 현황

### 3.1 ProductionPanelUI.cs

현재 SerializedField 필드:
- `_goldText` — 플레이어 총 골드
- `_populationText` — 플레이어 총 인구
- `_pistoleerButton`, `_assaultButton`, `_sniperButton` — 유닛 버튼
- `_pistoleerButtonPortrait`, `_assaultButtonPortrait`, `_sniperButtonPortrait` — 초상화 이미지
- `_pistoleerAutoIndicator`, `_assaultAutoIndicator`, `_sniperAutoIndicator` — 자동 생산 표시
- `_queueSlotImages[3]` — 큐 슬롯
- `_progressFill` — 생산 진행률 바

**현재 미구현 UI 표기:**
- 유닛별 골드 비용 텍스트 (CostText — 코드에 SerializedField 없음)
- 유닛 HP / 공격력 / 사거리 / 생산 시간 표기

### 3.2 BuildingPlacementUI.cs

현재 SerializedField 필드:
- `_barracksButton`, `_miningPostButton`, `_cancelButton`
- `_barracksButtonPortrait`, `_miningPostButtonPortrait`
- 6세트 종족별 초상화 세트

**현재 미구현 UI 표기:**
- 건물별 HP 텍스트
- 건물별 건설 비용 텍스트

---

## 4. 다히트 공격 유닛 현황

### 4.1 현재 상태

`UnitStats.cs` 주석(128-129행):
> "다중 히트 유닛(FlameSpirit 6히트, LionKnight 4히트)은 첫 번째 히트 프레임만 적용.  
> 다중 히트 구현은 별도 작업에서 처리 예정."

**LionKnight 히트 수 불일치:**
- `UnitStats.cs` 주석: 4히트
- `StatsReference.md`: 2히트 (2히트 공격)

→ 이번 작업에서 **StatsReference.md 기준(2히트)**으로 주석을 수정하고, 실제 구현은 첫 1히트만 처리.

### 4.2 타격 프레임 값 (HitFrameTime) — 현재 코드 기준

이미 StatsReference.md의 첫 번째 타격 프레임 기준으로 설정되어 있음 (변경 불필요):
- FlameSpirit: 0.667초 (0:20 첫 히트)
- EmberSpirit: 1.000초 (1:00)
- InfernoSpirit: 1.250초 (1:15)
- BearGuard: 0.667초 (0:20)
- FoxMagician: 2.417초 (2:25)
- LionKnight: 0.250초 (0:15 첫 히트)

---

## 5. 작업 범위 요약

### 파일별 변경 필요 항목

| 파일 | 변경 내용 | 영향 범위 |
|------|-----------|----------|
| `UnitStats.cs` | HP/공격력 플레이스홀더 → 실제 값, Pistoleer 이동속도 수정, LionKnight 주석 수정 | 전투 스탯 전체 |
| `UnitProductionStats.cs` | Spirit/Transcendence 생산 시간/비용 수정 | 생산 큐, UI 비용 표시 |
| `BuildingStats.cs` | 종족별 HP 지원 추가 (RaceId 파라미터 or GameRaceContext 조회) | 건물 생성 흐름 |
| `BuildingPlacementUseCase.cs` | BuildingStats.GetMaxHp() 호출부 수정 (RaceId 전달) | 건물 HP 적용 |
| `ProductionPanelUI.cs` | 유닛별 비용/HP/공격력/사거리 표기 추가 | UI 레이아웃 |
| `BuildingPlacementUI.cs` | 건물별 HP/비용 표기 추가 | UI 레이아웃 |

### UI 유니티 에디터 작업 필요
- ProductionPanel 프리팹에 CostText, StatText 오브젝트 추가
- BuildingPlacementPanel 프리팹에 HP/Cost 텍스트 오브젝트 추가

---

## 6. 다히트 공격 — 이번 작업 스코프 외 기록

> **작업 제외 항목 — 별도 작업으로 처리:**

| 유닛 | 히트 수 | 타격 프레임 시간 (StatsReference 기준) |
|------|---------|--------------------------------------|
| FlameSpirit | 6히트 | 0:20, 1:05, 1:13, 1:20, 1:28, 2:03 |
| LionKnight | 2히트 | 0:22, 1:08 |

이번 작업에서는 **첫 번째 타격 프레임(0:20 / 0:22)** 시점에 1회 데미지만 적용.  
다중 히트 구현은 별도 작업 태스크로 분리.
