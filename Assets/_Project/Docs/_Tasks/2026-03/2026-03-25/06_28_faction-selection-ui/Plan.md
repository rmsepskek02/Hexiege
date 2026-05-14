# Plan: 종족 선택 UI

**목표:** 로비 전투 탭에 3종족 캐러셀 선택 UI 구현 + 인게임 유닛/건물에 종족 적용

---

## ✅ 확정된 설계 결정사항 (2026-03-25)

| # | 항목 | 결정 내용 |
|---|------|---------|
| Q1 | **종족별 건물 매핑** | 초월계(여우마법사): ElderTree, FungalNode, HunterPlant / 정령계(불정령): ManaRift, SpiritNexus, SummoningAltar |
| Q2 | **종족별 유닛 외형** | 각 종족별 고유 외형 있음. 일부 유닛은 한 팀 색상만 존재(추후 추가 예정) |
| Q3 | **멀티플레이 종족 동기화** | 게임 시작 시 상대방 종족 동기화 필요 (Option A 채택) |

### 종족 목록
| 종족 | 영문 ID | 건물 (고유) | 대표 캐릭터 |
|------|--------|-----------|------------|
| 피스톨러 | `Pistoleer` | Castle, Barracks (기존) | 피스톨러 캐릭터 |
| 불정령 | `FireSpirit` | ManaRift, SpiritNexus, SummoningAltar | 불정령 캐릭터 |
| 여우마법사 | `FoxMage` | ElderTree, FungalNode, HunterPlant | 여우마법사 캐릭터 |

---

## 구현 단계

### Step 1: Domain — FactionType enum 추가

**파일:** `Assets/_Project/Scripts/Domain/Common/FactionType.cs` (신규)

```
FactionType {
    Pistoleer = 0,    // 피스톨러 종족
    FireSpirit = 1,   // 불정령 종족
    FoxMage = 2       // 여우마법사 종족
}
```

Domain 레이어 — 순수 C#, 의존성 없음.

---

### Step 2: Infrastructure — LocalPlayerFaction 정적 홀더 추가

**파일:** `Assets/_Project/Scripts/Infrastructure/Network/LocalPlayerFaction.cs` (신규)

`LocalPlayerTeam`과 동일한 패턴:
- `Current`: 현재 선택된 종족 (기본값: `FactionType.Pistoleer`)
- `Set(FactionType faction)`: Lobby에서 선택 시 호출
- `Reset()`: 씬 전환/연결 해제 시 초기화

**로비에서 직접 저장 → 씬 전환 후 GameBootstrapper가 읽어 Factory에 전달**

---

### Step 3: Domain — BuildingType enum 확장

**파일:** `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` (수정)

기존 Castle / Barracks / MiningPost에 종족별 건물 6종 추가.
*(Q1 확정 후 정확한 enum 값 결정)*

예시:
```
// 기존
Castle, Barracks, MiningPost

// 불정령 종족 (Q1 확인 후)
FireSpiritCastle?, FireSpiritBarracks?

// 여우마법사 종족 (Q1 확인 후)
FoxMageCastle?, FoxMageBarracks?
```

---

### Step 4: Infrastructure — Factory 수정

#### UnitFactory 수정
**파일:** `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

종족별 외형 확정: `FactionType × TeamId × UnitType` 조합 → 프리팹 딕셔너리 또는 중첩 구조체
단, 일부 유닛은 한 팀 색상만 있으므로 null 허용 + 폴백(fallback) 처리 필요.

#### BuildingFactory 수정
**파일:** `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs`

- `BuildingTeamPrefabSet`에 종족별 건물 슬롯 추가
- GameBootstrapper에서 `LocalPlayerFaction.Current` 기반으로 올바른 프리팹 세트 주입
- MiningPost는 종족 무관 유지

---

### Step 5: Presentation — 종족 선택 UI (캐러셀)

#### 5-1. FactionSelectView.cs 신규 생성
**파일:** `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/FactionSelectView.cs`

UI 구성:
```
[FactionSelectView]
├── [◀] 왼쪽 화살표 버튼
├── [대표 캐릭터 이미지] (중앙)
├── [종족 이름 텍스트]
└── [▶] 오른쪽 화살표 버튼
```

동작:
- 화살표 버튼 클릭 → 현재 선택 종족 변경 (0 → 1 → 2 → 0 순환)
- `LocalPlayerFaction.Set()` 호출로 선택 즉시 저장
- 종족별 대표 이미지는 Inspector에서 `Sprite[]`로 할당

#### 5-2. BattleViewModel 수정
**파일:** `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs`

- `ReactiveProperty<FactionType> SelectedFaction` 추가
- CmdSelectPrevFaction / CmdSelectNextFaction Subject 추가
- SelectedFaction 변경 시 → `LocalPlayerFaction.Set()` 호출

#### 5-3. BattleMainView 수정
**파일:** `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleMainView.cs`

- `FactionSelectView` 참조 추가 (`[SerializeField]`)
- `Bind()` 시 FactionSelectView에 ViewModel 연결

---

### Step 6: Bootstrap — GameBootstrapper 수정

**파일:** `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

- `LoadMap()` 또는 `StartNetworkGame()` 에서 `LocalPlayerFaction.Current` 읽기
- Factory 초기화 시 현재 종족에 맞는 프리팹 세트 전달

---

### Step 7: 멀티플레이 동기화 (Q3 결과에 따라)

**Option A 채택 (완전 동기화):**
- `NetworkGameFlow.cs` 에 `NetworkVariable<int>` 2개 추가 (Host 종족, Client 종족)
- 게임 씬 진입 직후 각 클라이언트가 자신의 종족을 서버에 전달 (ServerRpc)
- GameBootstrapper가 양쪽 종족 정보를 받아 Factory 초기화
- 상대방 팀의 유닛/건물은 상대방 종족 + 상대방 팀 색상으로 생성

---

### Step 8: Inspector 작업

| 대상 | 작업 내용 |
|------|---------|
| Lobby.unity - BattleMainView GameObject | FactionSelectView 하위 GameObject 추가, 버튼/이미지/텍스트 연결 |
| FactionSelectView Inspector | 종족별 대표 캐릭터 Sprite 3종 슬롯 연결 |
| GameBootstrapper Inspector | 종족별 Building/Unit 프리팹 슬롯 연결 |

---

## 구현 순서 (의존성 기반)

```
[병렬 가능]
Step 1 (FactionType) + Step 2 (LocalPlayerFaction)
        ↓
[순차]
Step 3 (BuildingType 확장) — Q1 확정 후
        ↓
[병렬 가능]
Step 4 (Factory 수정) + Step 5 (UI 작성)
        ↓
Step 6 (GameBootstrapper 수정)
        ↓
Step 7 (멀티플레이 동기화) — Q3 결정 후
        ↓
Step 8 (Inspector 작업) — 전 단계 코드 완료 후
```

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| BuildingType 확장 시 기존 switch문 누락 | 모든 BuildingType switch 위치 검색 후 수정 |
| Factory Inspector 슬롯 증가로 기존 참조 깨짐 | Inspector 스크립트로 자동 할당 또는 단계적 수정 |
| 멀티플레이 종족 미동기화 시 상대방 건물 외형 오류 | Option B로 시작해 문제 확인 후 A 전환 |
| Domain 레이어에서 Core 참조 금지 | FactionType은 순수 enum — using 없음 |

---

## 에이전트 위임 계획

| 단계 | 담당 에이전트 |
|------|-------------|
| Step 1~3 (Domain/Infrastructure 추가) | game-programmer |
| Step 4~6 (Factory + UI + Bootstrap) | game-programmer |
| Step 7 (멀티플레이 동기화) | game-programmer |
| Step 8 (Inspector 작업) | 1회성 에디터 스크립트 → 사용자 실행 |
| 구현 후 검증 | qa-tester |
