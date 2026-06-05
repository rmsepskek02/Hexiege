# Research — Missing BuildingView Script 경고 제거

## 작업 목적 (자연어 설명)

게임을 플레이모드로 실행하면 Unity 콘솔에 "The referenced script on this Behaviour is missing!" 경고가 여러 건 출력됩니다.
이 경고는 게임 동작에 직접 영향을 주지는 않지만, 콘솔을 오염시켜 실제 버그 로그를 찾기 어렵게 만들고 프리팹 상태가 불완전함을 나타냅니다.
원인은 과거에 프리팹에 붙어 있던 `BuildingView` 스크립트가 삭제됐지만, 프리팹 파일 내부의 참조가 정리되지 않아 발생하는 것입니다.
이 작업은 해당 누락 참조를 모든 영향 프리팹에서 제거하여 경고를 없애는 것을 목표로 합니다.

---

## 원인 분석

### 누락된 스크립트

| 항목 | 내용 |
|------|------|
| 스크립트 이름 | `Hexiege.Presentation.BuildingView` |
| 참조 GUID | `c178b6f3e086351409b946635cbfae71` |
| 파일 존재 여부 | **없음** (`Assets/_Project/Scripts/` 내 어디에도 `BuildingView.cs` 없음) |

### 발생 경로 (콜스택)

```
BuildingFactory.CreateBuildingObject()
  → Instantiate(prefab, ...)
    → Unity가 프리팹 컴포넌트를 복원하려고 했으나
      c178b6f3e086351409b946635cbfae71 GUID에 해당하는 스크립트가 없음
        → "The referenced script on this Behaviour is missing!" 경고 출력
```

### BuildingView 제거 경위

[BuildingFactory.cs](Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs) 파일 주석에 명시:
> "별도의 BuildingView 컴포넌트 없이 BuildingFactory가 단일 책임으로 GO 생명주기를 관리한다."

즉, `BuildingView`는 **의도적으로 제거된 스크립트**이며 다시 복원할 필요 없음.
프리팹에서 참조 정리만 하면 됨.

---

## 영향 범위

### 현재 사용 중인 프리팹 (8개) — 반드시 수정 필요

**Spirit 계열**
- `Assets/_Project/Prefabs/Buildings/Spirit/Building_ManaRift_Blue.prefab`
- `Assets/_Project/Prefabs/Buildings/Spirit/Building_ManaRift_Red.prefab`
- `Assets/_Project/Prefabs/Buildings/Spirit/Building_SpiritNexus_Blue.prefab`
- `Assets/_Project/Prefabs/Buildings/Spirit/Building_SpiritNexus_Red.prefab`

**Transcendence 계열**
- `Assets/_Project/Prefabs/Buildings/Transcendence/Building_ElderTree_Blue.prefab`
- `Assets/_Project/Prefabs/Buildings/Transcendence/Building_ElderTree_Red.prefab`
- `Assets/_Project/Prefabs/Buildings/Transcendence/Building_FungalNode_Blue.prefab`
- `Assets/_Project/Prefabs/Buildings/Transcendence/Building_FungalNode_Red.prefab`

### _Old 폴더 프리팹 (9개) — 현재 게임에서 사용되지 않음

- `Building_SummoningAltar_Blue/Red.prefab`
- `Building_MiningPost.prefab`
- `Building_HunterPlant_Blue/Red.prefab`
- `Building_Barracks_Blue1/Red1.prefab`
- `Building_Castle_Blue1/Red1.prefab`

> `_Old` 폴더는 아카이브 목적으로 보관 중이므로 수정 여부는 사용자 판단.

### 영향 없는 프리팹

Human 계열 전체 + 다수의 Spirit/Transcendence 신규 프리팹 → `BuildingView` 참조 없음 (정상)

---

## 수정 방법

프리팹 파일의 `MonoBehaviour` 블록 중 해당 GUID를 참조하는 항목을 제거하면 됨.

### 프리팹 내 누락 참조 구조 (예시: Building_ElderTree_Red.prefab)

```yaml
--- !u!114 &2187975772124476828
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: c178b6f3e086351409b946635cbfae71, type: 3}
  m_EditorClassIdentifier: Assembly-CSharp::Hexiege.Presentation.BuildingView
  # ↑ 이 블록 전체 제거 + 루트 GameObject의 m_Component 목록에서 해당 component 참조도 제거
```

### 제거 방식 (2가지 선택지)

| 방식 | 설명 | 장단점 |
|------|------|--------|
| **A. Editor 스크립트** | 메뉴 항목으로 일괄 처리 | 안전하고 실수 없음, Unity가 직접 처리 |
| **B. 텍스트 직접 수정** | prefab 파일을 텍스트로 열어 해당 블록 제거 | 빠르지만 수동 작업 오류 위험 |

→ **Editor 스크립트(A) 권장**: Unity가 프리팹 직렬화를 정상적으로 처리하므로 더 안전

---

## 현재 상태 요약

| 항목 | 상태 |
|------|------|
| 경고 발생 원인 | 확인 완료 (`BuildingView` 스크립트 참조 잔존) |
| 영향 프리팹 수 | 현재 사용 8개 + _Old 9개 = 총 17개 |
| 스크립트 복원 필요 여부 | 불필요 (BuildingFactory가 대체 역할 수행 중) |
| 게임 동작 영향 | 없음 (경고만 출력, 기능 오작동 없음) |
| 수정 방법 | Editor 스크립트로 Missing Script 일괄 제거 |
