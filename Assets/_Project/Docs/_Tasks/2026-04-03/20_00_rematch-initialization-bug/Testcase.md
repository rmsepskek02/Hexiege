# Testcase: 재경기 초기화 버그

작성일: 2026-04-04
상태: ✅ 완료

---

## TC-1: MULTI-재경기 후 유닛 잔존 여부

**전제:** 멀티플레이(Host + Client) 게임이 진행 중이며, 양측 진영에 유닛이 여러 개 배치되어 있음.

**동작:**
1. 게임 종료 후 양측 모두 재경기 버튼을 눌러 동의
2. 재경기 시작 후 새 게임이 로드됨
3. 새 게임 화면에서 이전 게임의 유닛이 남아있는지 확인

**기댓값:**
- 이전 게임의 유닛/건물이 화면에 남아있지 않음
- 새 게임의 초기 상태(유닛 없음, 건물만 존재)로 정상 시작됨

**결과:** PASS (2026-04-04 사용자 실기 테스트)

---

## QA 섹션

### 정적 분석

**수정 파일**: `Infrastructure/Network/NetworkGameEndController.cs` — `StartRematch()`

**수정 내용**:
- `LoadScene()` 직전에 `NetworkManager.SpawnManager.SpawnedObjects` 순회
- `IsSceneObject == false`인 동적 스폰 NetworkObject(유닛, 건물)를 명시적 `Despawn()` 처리
- `SpawnedObjects.Values`를 `List<NetworkObject>`로 복사하여 순회 중 컬렉션 변경 예외 방지
- `IsSpawned == true` / `IsSceneObject == false` nullable bool 비교 처리 (NGO 2.9.x)

**확인된 원인**:
- NGO SceneManager.LoadScene(Single)으로 **같은 씬을 재로드**할 때 동적 스폰 NetworkObject 자동 Despawn 미보장
- `DestroyWithScene = true`도 같은 씬 재로드 시나리오에서는 동작 불보장 (NGO 2.x 이슈 확인)
- 재로드 후 새 `GameBootstrapper`의 `UnitFactory._unitObjects`는 빈 딕셔너리 → `DestroyAllUnits()` 무효

**판정**: PASS
