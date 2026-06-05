# Plan — Missing BuildingView Script 경고 제거

## 작업 목적 (자연어 설명)

플레이모드 실행 시 Unity 콘솔에 출력되는 "The referenced script on this Behaviour is missing!" 경고를 없앱니다.
원인은 현재 사용 중인 Spirit/Transcendence 계열 건물 프리팹 8개에 삭제된 `BuildingView` 스크립트의 참조가 남아 있기 때문입니다.
Editor 전용 1회성 스크립트를 작성해 Unity가 직접 해당 컴포넌트를 제거하도록 하고, 작업 완료 후 스크립트를 삭제합니다.

---

## 수정 대상

| 파일 | 종족 | 비고 |
|------|------|------|
| `Buildings/Spirit/Building_ManaRift_Blue.prefab` | Spirit | |
| `Buildings/Spirit/Building_ManaRift_Red.prefab` | Spirit | |
| `Buildings/Spirit/Building_SpiritNexus_Blue.prefab` | Spirit | |
| `Buildings/Spirit/Building_SpiritNexus_Red.prefab` | Spirit | |
| `Buildings/Transcendence/Building_ElderTree_Blue.prefab` | Transcendence | |
| `Buildings/Transcendence/Building_ElderTree_Red.prefab` | Transcendence | |
| `Buildings/Transcendence/Building_FungalNode_Blue.prefab` | Transcendence | |
| `Buildings/Transcendence/Building_FungalNode_Red.prefab` | Transcendence | |

> `_Old` 폴더 9개는 게임에서 사용되지 않으므로 수정하지 않음.

---

## 구현 방법

### Editor 1회성 스크립트 작성

- 경로: `Assets/Editor/RemoveMissingScripts.cs`
- 메뉴: `Hexiege/Setup/Missing Script 제거`
- Unity의 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript()` API를 사용
- 위 8개 프리팹을 직접 로드 → Missing Script 제거 → 저장 → 스크립트 파일 삭제

### 동작 순서

1. 대상 프리팹 8개를 `AssetDatabase.LoadAssetAtPath()`로 로드
2. `PrefabUtility.LoadPrefabContents()`로 프리팹 내용 열기
3. 루트 GameObject와 모든 자식에서 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript()` 실행
4. `PrefabUtility.SaveAsPrefabAsset()`로 저장
5. 완료 후 제거된 컴포넌트 수를 콘솔에 출력

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 프리팹 데이터 손상 | `PrefabUtility` API를 사용하므로 Unity가 직접 처리 — 위험 없음 |
| 실수로 다른 컴포넌트 제거 | `RemoveMonoBehavioursWithMissingScript()`는 **Missing Script만** 제거함 |
| _Old 폴더 영향 | 대상 목록을 하드코딩하므로 _Old 폴더는 건드리지 않음 |

---

## 검증 방법

1. Editor 스크립트 실행 후 Unity 플레이모드 실행
2. 콘솔에 "The referenced script on this Behaviour is missing!" 경고가 사라지면 완료
3. 건물(ElderTree, FungalNode, ManaRift, SpiritNexus)이 정상적으로 화면에 생성되면 완료

---

## 작업 순서

- [ ] 1. Editor 스크립트(`RemoveMissingScripts.cs`) 작성
- [ ] 2. 사용자가 `Hexiege/Setup/Missing Script 제거` 메뉴 실행
- [ ] 3. 플레이모드로 경고 사라졌는지 확인
- [ ] 4. `RemoveMissingScripts.cs` 파일 삭제
