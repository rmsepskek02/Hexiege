# Research: 종족 선택 UI

**작업 요약:** 로비 전투 탭에 3종족 선택 캐러셀 UI 추가 + 인게임 적용

---

## 1. 현재 종족 시스템 상태

### ⚠️ 종족 개념 없음
현재 프로젝트에는 "Faction" 또는 "Race" 개념이 **전혀 존재하지 않는다.**
- `TeamId.cs`: Blue / Red / Neutral — 팀 색상만 구분
- `UnitType.cs`: Pistoleer / Assault / Sniper — 병종만 구분
- 종족별 스탯 차이, 종족별 건물, 종족별 유닛 외형 분기 없음

**이번 작업은 종족 시스템을 새로 설계하고 추가하는 작업이다.**

---

## 2. 로비 전투 탭 구조

```
BattlePanel (BattleRootView)
└── BattleMainView  ← 종족 선택 UI가 추가될 화면
    ├── [싱글플레이] 버튼
    ├── [커스텀 게임] 버튼
    └── [랜덤 매칭] 버튼
```

**관련 파일:**
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleMainView.cs`
- `Assets/_Project/Scripts/Presentation/UI/ViewModels/BattleViewModel.cs`
- `Assets/_Project/Scripts/Presentation/UI/Views/Lobby/Battle/BattleRootView.cs`

**BattleMainView 특성:**
- `IView<BattleViewModel>` 인터페이스 구현 (MVVM 패턴)
- UniRx `ReactiveProperty` 기반 바인딩
- `BattleScreen.Main`일 때만 `gameObject.SetActive(true)`

---

## 3. 건물 프리팹 현황

```
Assets/_Project/Prefabs/Buildings/
├── [기본 3종]
│   ├── Building_Castle_Blue.prefab
│   ├── Building_Castle_Red.prefab
│   ├── Building_Barracks_Blue.prefab
│   ├── Building_Barracks_Red.prefab
│   └── Building_MiningPost.prefab (중립)
│
└── [신규 6종 × Blue/Red = 12개 — 종족별 에셋]
    ├── Building_ElderTree_Blue/Red.prefab
    ├── Building_FungalNode_Blue/Red.prefab
    ├── Building_HunterPlant_Blue/Red.prefab
    ├── Building_ManaRift_Blue/Red.prefab
    ├── Building_SpiritNexus_Blue/Red.prefab
    └── Building_SummoningAltar_Blue/Red.prefab
```

### ⚠️ 미확정 — 사용자 확인 필요
신규 건물 6종이 어느 종족에 속하는지 매핑이 정해지지 않았다.
예시 (확인 전):
- 피스톨러 종족: Castle, Barracks (기존)
- 불정령 종족: FungalNode, ManaRift, SpiritNexus?
- 여우마법사 종족: ElderTree, HunterPlant, SummoningAltar?

**→ Plan.md 작성 전 사용자가 종족별 건물 매핑을 확정해야 한다.**

---

## 4. 유닛 프리팹 현황

```
Assets/_Project/Prefabs/Units/
├── Unit_Pistoleer_Blue/Red.prefab
├── Unit_Assault_Blue/Red.prefab
└── Unit_Sniper_Blue/Red.prefab
```

**현재 유닛은 3종 모두 단일 외형 (팀 색상만 다름).**

### ⚠️ 미확정 — 사용자 확인 필요
종족별 유닛이 외형적으로 다른가?
- Option A: 각 종족이 동일한 병종(Pistoleer/Assault/Sniper)을 다른 외형으로 사용
  - 예: 불정령 Pistoleer, 여우마법사 Pistoleer 등 → 종족 × 병종 조합 프리팹 필요
- Option B: 각 종족이 고유 캐릭터를 사용하고, 병종 개념은 제거 또는 재정의
- Option C: 현재는 유닛 외형은 동일, 종족 구분은 건물로만 (1차 구현)

**→ 유닛 외형 차이 여부도 사용자 확인 필요.**

---

## 5. 팀-팩토리 시스템 (현재)

### UnitFactory
```
UnitTeamPrefabSet (Blue) → pistoleer / assault / sniper
UnitTeamPrefabSet (Red)  → pistoleer / assault / sniper
팀 × 병종 → 프리팹 선택
```

### BuildingFactory
```
BuildingTeamPrefabSet (Blue) → castle / barracks
BuildingTeamPrefabSet (Red)  → castle / barracks
+ miningPostPrefab (중립)
```
- **신규 건물 6종은 BuildingType enum에 없고 Factory에서도 처리 안 됨**

---

## 6. 팀 전달 메커니즘 (LocalPlayerTeam 패턴)

```csharp
// Infrastructure 레이어 - 정적 홀더
public static class LocalPlayerTeam
{
    public static TeamId Current { get; private set; } = TeamId.Blue;
    public static bool IsAssigned { get; private set; } = false;
    public static void Set(TeamId team) { ... }
    public static void Reset() { ... }
}
```

파일: `Assets/_Project/Scripts/Infrastructure/Network/LocalPlayerTeam.cs`

**이 패턴을 `LocalPlayerFaction`으로 동일하게 적용할 수 있다.**

---

## 7. 멀티플레이 고려사항

### 종족 선택 동기화 문제
멀티플레이에서 두 플레이어가 각자 다른 종족을 선택할 경우:
- 내 유닛/건물: 내 종족에 맞는 프리팹
- 상대방 유닛/건물: 상대방 종족에 맞는 프리팹

상대방 종족 정보를 받아야 올바른 프리팹을 선택할 수 있다.

**선택 방안:**
- A) `NetworkVariable<int>` 로 각 플레이어의 종족 공유 (완전 동기화)
- B) 상대방 유닛/건물은 팀 색상(Red/Blue)으로만 구분하고 외형 동기화 생략 (단순화)

방안 A가 시각적으로 올바르지만 구현 복잡도가 높음.
방안 B는 1차 구현으로 타협 가능.

---

## 8. 영향 범위 요약

| 파일/시스템 | 변경 필요 | 비고 |
|------------|---------|------|
| Domain/Common/FactionType.cs | **신규 생성** | 종족 enum |
| Infrastructure/Network/LocalPlayerFaction.cs | **신규 생성** | 팀 패턴 동일 적용 |
| Infrastructure/Factories/UnitFactory.cs | **수정** | 종족별 프리팹 분기 추가 |
| Infrastructure/Factories/BuildingFactory.cs | **수정** | 종족별 프리팹 + BuildingType 확장 |
| Domain/Building/BuildingType.cs | **수정** | 신규 6종 enum 추가 |
| Presentation/.../BattleMainView.cs | **수정** | 캐러셀 UI 연동 |
| Presentation/.../BattleViewModel.cs | **수정** | 종족 선택 상태 추가 |
| FactionSelectView.cs | **신규 생성** | 캐러셀 UI View |
| Bootstrap/GameBootstrapper.cs | **수정** | 종족 기반 Factory 초기화 |
| Inspector (Lobby.unity) | **Inspector 작업** | 새 UI GameObject 연결 |
| Inspector (Factory Prefabs) | **Inspector 작업** | 종족별 프리팹 슬롯 연결 |

---

## 9. 미확정 항목 (Plan 작성 전 확인 필요)

1. **종족별 건물 매핑**: 신규 6개 건물이 불정령/여우마법사 중 어느 종족인지
2. **유닛 외형 차이**: 종족별로 유닛 외형이 다른가 (현재 존재하는 유닛 프리팹 기준)
3. **멀티플레이 동기화 범위**: 상대방 종족 정보를 동기화할 것인가
4. **종족별 BuildingType**: 기존 Castle/Barracks를 모든 종족이 공유하는가, 종족별 다른 건물로 대체하는가
