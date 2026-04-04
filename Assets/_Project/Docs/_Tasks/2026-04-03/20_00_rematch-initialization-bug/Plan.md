# Plan: 재경기 초기화 버그 수정

작성일: 2026-04-04
상태: ✅ 구현 완료 + 테스트 통과 (2026-04-04)

---

## 목표

재경기(Rematch) 시 이전 게임의 유닛/건물 GameObject가 씬에 잔존하는 버그 수정.

---

## 원인 요약

`StartRematch()` → `LoadScene("Game", Single)` 호출 전에
유닛/건물 NetworkObject를 명시적으로 Despawn하지 않음.

새 `GameBootstrapper` 인스턴스의 `DestroyAllUnits()`는 빈 딕셔너리로 실행되어 아무 효과 없음.
NGO의 같은 씬 재로드 시 동적 NetworkObject 자동 Despawn 미보장.

---

## 수정 방향

`LoadScene()` 호출 **직전에** NGO `SpawnManager.SpawnedObjects`를 순회하여
동적 스폰 NetworkObject를 모두 명시적으로 Despawn.

### 왜 SpawnManager 방식인가

| 비교 항목 | GameBootstrapper 경유 | SpawnManager 직접 순회 |
|----------|----------------------|----------------------|
| 의존성 | GameBootstrapper 탐색 필요 | NGO 내부 트래커 직접 사용 |
| 범위 | 팩토리에 등록된 것만 | NGO가 추적하는 모든 동적 NetworkObject |
| 확장성 | 새 동적 오브젝트 추가 시 누락 가능 | 자동 포함 |

`DestroyWithScene = true` 방식은 **같은 씬 재로드 시 동작 불보장** — NGO 2.x 이슈 확인됨.

---

## 수정 파일

### 1. `Infrastructure/Network/NetworkGameEndController.cs`

`StartRematch()` 내부 수정:

```
StartRematch():
  1. SpawnManager.SpawnedObjects 복사본 순회
     - IsSceneObject = true인 씬 배치 오브젝트(NetworkGameFlow, NetworkCombatController 등) 제외
     - 나머지 동적 스폰 NetworkObject(유닛, 건물) → Despawn()
  2. NetworkManager.SceneManager.LoadScene("Game", Single)
```

### 2. 유닛 프리팹 Inspector (6종)

`Active Scene Synchronization` → **체크 해제 (원래 상태로 복원)**
- 이번 수정은 코드에서 명시적으로 처리하므로 이 설정에 의존하지 않음
- 체크 유지 시 씬 변경 시 유닛이 active 씬을 따라 이동하는 부작용 발생 가능

---

## 영향 범위

| 파일 | 변경 내용 |
|------|----------|
| `NetworkGameEndController.cs` | `StartRematch()` 내 정리 로직 추가 |
| 유닛 프리팹 6종 Inspector | `Active Scene Synchronization` 체크 해제 복원 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| Despawn 중 컬렉션 변경 | SpawnedObjects의 복사본(`new List<>`)으로 순회 |
| 씬 NetworkObject 실수로 Despawn | `netObj.IsSceneObject` 체크로 제외 |
| Despawn 후 LoadScene 타이밍 | Despawn은 동기 처리 완료 후 LoadScene 호출 |
| 건물(Castle 등) 재생성 | 새 씬의 LoadMap() → PlaceCastles() 가 재실행하므로 정상 |
