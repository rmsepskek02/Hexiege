# Testcase: 건물 초상화 종족+팀 기반 표시

## 테스트 전 사전 조건

- Inspector 연결 완료 상태 (BuildingPlacementUI 6세트 + ProductionPanelUI 4세트)
- 싱글플레이 기준: 로비에서 종족 선택 후 게임 진입

---

## TC 목록

### TC-SINGLE-001: Human 종족 배럭 초상화 표시

**전제:** 로비에서 Human 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 빈 일반 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 배럭 버튼에 Human 종족의 배럭 이미지(`bld_barracks_blue`)가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: `GameBootstrapper.LoadMap()` 이전에 `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 호출 확인. Show() → UpdateButtonPortraits() → GetBuildingPortraitSet(Blue, Human) → `_blueHumanPortraits.barracks` 경로 정상. `using Hexiege.Infrastructure` 선언 확인.
- 실기: Inspector에서 `_blueHumanPortraits.barracks`에 `bld_barracks_blue` 스프라이트 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-002: Human 종족 채굴소 초상화 표시

**전제:** 로비에서 Human 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 금광 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 채굴소 버튼에 Human 종족의 채굴소 이미지(`bld_mining_post`)가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: GetBuildingPortraitSet(Blue, Human) → `_blueHumanPortraits.miningPost` 경로 정상. sprite = null 대입 시 Image 투명 표시(크래시 없음) 확인.
- 실기: Inspector에서 `_blueHumanPortraits.miningPost`에 `bld_mining_post` 스프라이트 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-003: Spirit 종족 배럭 초상화 표시

**전제:** 로비에서 Spirit 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 빈 일반 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 배럭 버튼에 Spirit 종족의 소환 제단 이미지(`bld_summoningaltar_blue`)가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: GetBuildingPortraitSet(Blue, Spirit) → `_blueSpiritPortraits.barracks` 분기 정상. Spirit 케이스 명시 확인(line 235).
- 실기: Inspector에서 `_blueSpiritPortraits.barracks`에 `bld_summoningaltar_blue` 스프라이트 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-004: Spirit 종족 채굴소 초상화 표시

**전제:** 로비에서 Spirit 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 금광 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 채굴소 버튼에 Spirit 종족의 마나균열 이미지(`bld_manarift_blue`)가 표시된다.
- ⚠️ `bld_manarift_blue` 미제작 상태 → 이미지 없음(빈 상태) 예상.

**결과:** CONDITIONAL PASS
- 정적 분석: GetBuildingPortraitSet(Blue, Spirit) → `_blueSpiritPortraits.miningPost` 경로 정상. sprite = null 대입 시 Image.sprite = null → 투명(빈 상태) 표시, 크래시 없음 확인.
- 실기: Inspector에서 `_blueSpiritPortraits.miningPost` 미연결 시 빈 상태로 표시되는지 사용자 확인 필요.

---

### TC-SINGLE-005: Transcendence 종족 배럭 초상화 표시

**전제:** 로비에서 Transcendence 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 빈 일반 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 배럭 버튼에 Transcendence 종족의 사냥 식물 이미지(`bld_hunterplant_blue`)가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: GetBuildingPortraitSet(Blue, Transcendence) → `_blueTranscendencePortraits.barracks` 분기 정상. Transcendence 케이스 명시 확인(line 236).
- 실기: Inspector에서 `_blueTranscendencePortraits.barracks`에 `bld_hunterplant_blue` 스프라이트 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-006: Transcendence 종족 채굴소 초상화 표시

**전제:** 로비에서 Transcendence 종족을 선택하고 싱글플레이로 게임에 진입한다.

**동작:**
1. 자기 팀 금광 타일을 탭하여 건물 선택 팝업을 연다.

**기댓값:**
- 채굴소 버튼에 Transcendence 종족의 균류 노드 이미지(`bld_fungalnode_blue`)가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: GetBuildingPortraitSet(Blue, Transcendence) → `_blueTranscendencePortraits.miningPost` 경로 정상.
- 실기: Inspector에서 `_blueTranscendencePortraits.miningPost`에 `bld_fungalnode_blue` 스프라이트 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-007: Spirit 종족 유닛 생산 패널 초상화 표시

**전제:** 로비에서 Spirit 종족을 선택하고 싱글플레이로 게임에 진입한다. 배럭을 건설한다.

**동작:**
1. 건설된 배럭을 탭하여 유닛 생산 패널을 연다.

**기댓값:**
- 슬롯1: FlameSpirit 초상화, 슬롯2: EmberSpirit 초상화, 슬롯3: InfernoSpirit 초상화가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: ProductionPanelUI.Show() → GameRaceContext.BlueRace 조회 → BindButtonUnitTypes(Spirit) + UpdateButtonPortraits(Blue, Spirit) → GetPortraitSet(Blue, Spirit) → `_blueSpiritPortraits` 경로 정상 확인.
- 실기: Inspector에서 `_blueSpiritPortraits` 3슬롯 연결 여부 사용자 확인 필요.

---

### TC-SINGLE-008: Transcendence 종족 유닛 생산 패널 초상화 표시

**전제:** 로비에서 Transcendence 종족을 선택하고 싱글플레이로 게임에 진입한다. 배럭을 건설한다.

**동작:**
1. 건설된 배럭을 탭하여 유닛 생산 패널을 연다.

**기댓값:**
- 슬롯1: BearGuard 초상화, 슬롯2: FoxMagician 초상화, 슬롯3: LionKnight 초상화가 표시된다.

**결과:** CONDITIONAL PASS
- 정적 분석: ProductionPanelUI.Show() → GameRaceContext.BlueRace 조회 → BindButtonUnitTypes(Transcendence) + UpdateButtonPortraits(Blue, Transcendence) → GetPortraitSet(Blue, Transcendence) → `_blueTranscendencePortraits` 경로 정상 확인.
- 실기: Inspector에서 `_blueTranscendencePortraits` 3슬롯 연결 여부 사용자 확인 필요.

---

## QA 섹션 (qa-tester 전용)

### 정적 분석 대상 파일
- `Assets/_Project/Scripts/Presentation/UI/BuildingPlacementUI.cs`
- `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`

### 확인 포인트
1. `BuildingRacePortraitSet` struct의 `barracks`, `miningPost` 필드 직렬화 여부
2. `UpdateButtonPortraits()`: `GameRaceContext.BlueRace`/`RedRace` 호출 시 null 위험 없음 (정적 홀더이므로 항상 유효)
3. `GetBuildingPortraitSet()`: 6가지 분기 누락 없음 확인
4. `miningPost` sprite가 null인 경우(bld_manarift_blue 미연결) → sprite = null 대입 → Image가 투명 표시 — 크래시 없음 확인
5. 기존 `_bluePortraits`, `_redPortraits`, `_miningPostPortrait` 필드 삭제로 인한 컴파일 오류 없음 확인
6. `using Hexiege.Infrastructure`가 BuildingPlacementUI.cs에 포함되어 있어 `GameRaceContext` 참조 가능 확인

---

## 정적 분석 결과 (qa-tester)

분석일: 2026-04-12

### 1. Grep 전수 검색 결과

**`BuildingPortraitSet` (구 타입명) 잔존 참조 검색**
- 검색 범위: `Assets/_Project/Scripts/**/*.cs`
- 결과: 잔존 없음. `BuildingPlacementUI.cs`에는 `BuildingRacePortraitSet`(신규)만 존재. 컴파일 오류 없음.

**`_bluePortraits`, `_redPortraits`, `_miningPostPortrait` (삭제된 필드) 잔존 검색**
- 검색 범위: `Assets/_Project/Scripts/**/*.cs`
- 결과: 잔존 없음. 참조 완전 제거 확인.

### 2. 확인 포인트별 판정

| 번호 | 확인 항목 | 판정 | 근거 |
|------|----------|------|------|
| 1 | `BuildingRacePortraitSet.barracks`, `miningPost` 필드에 `[System.Serializable]` + `public` 선언 | PASS | BuildingPlacementUI.cs line 93-101. struct에 `[System.Serializable]` 부착, 두 필드 모두 `public Sprite` |
| 2 | `GameRaceContext.BlueRace`/`RedRace` null 위험 없음 | PASS | GameRaceContext.cs line 35-38. `static` 프로퍼티, 기본값 `RaceId.Human` 초기화. null 반환 불가 |
| 3 | `GetBuildingPortraitSet()` 6분기 누락 없음 | PASS | BuildingPlacementUI.cs line 231-249. Blue×{Spirit, Transcendence, default(Human)}, Red×{Spirit, Transcendence, default(Human)} 6경우 전부 커버 |
| 4 | `miningPost` sprite = null 대입 시 크래시 없음 | PASS | line 221: null 체크(`_miningPostButtonPortrait != null`) 후 `.sprite = set.miningPost` 대입. Unity의 Image.sprite에 null 대입은 투명 표시, 예외 없음 |
| 5 | 기존 삭제 필드 참조 잔존 없음 | PASS | Grep 전수 검색 결과 0건 |
| 6 | `using Hexiege.Infrastructure` 포함 여부 | PASS | BuildingPlacementUI.cs line 34: `using Hexiege.Infrastructure;` 확인 |

### 3. 초기화 순서 검증 (싱글플레이)

`GameBootstrapper.cs` line 266: `GameRaceContext.Set(LocalPlayerRace.Current, RaceId.Human)` 이후 `LoadMap()` 호출.
BuildingPlacementUI.Show()는 플레이어가 타일을 탭하는 런타임 시점에 실행되므로, GameRaceContext는 항상 설정된 상태임. 순서 오류 없음.

### 4. ProductionPanelUI 관련 (TC-SINGLE-007, 008)

ProductionPanelUI.cs line 279-284: `Show()` 내에서 `GameRaceContext`로 종족 조회 후 `BindButtonUnitTypes(race)` + `UpdateButtonPortraits(team, race)` 호출. GetPortraitSet()의 6분기 구조가 BuildingPlacementUI와 동일 패턴으로 구현됨. 정적 분석상 오류 없음.

### 5. 종합 판정

컴파일 오류 위험 없음. 로직 분기 누락 없음. null 크래시 위험 없음.
모든 TC는 Inspector 스프라이트 연결 여부에 따라 최종 동작이 결정되므로 **CONDITIONAL PASS** 판정.
에이전트 실기 불가 — 사용자 확인 필요 (Inspector 6세트 스프라이트 연결 상태).
