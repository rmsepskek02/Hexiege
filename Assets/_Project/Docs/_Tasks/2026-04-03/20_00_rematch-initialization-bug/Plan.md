# Plan: 재경기 초기화 버그 수정

작성일: 2026-04-04
상태: 승인 대기

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

`LoadScene()` 호출 **직전에** 서버에서 모든 유닛/건물을 명시적으로 Despawn.

---

## 수정 파일

### `Infrastructure/Network/NetworkGameEndController.cs`

`StartRematch()` 내부에서 `LoadScene()` 호출 전에 정리 단계 추가:

```
StartRematch():
  1. GameBootstrapper를 씬에서 찾기 (FindFirstObjectByType)
  2. UnitFactory.DestroyAllUnits() 호출  ← 서버 측 유닛 명시적 Despawn
  3. BuildingFactory.DestroyAllBuildings() 호출  ← 서버 측 건물 명시적 Despawn
  4. NetworkManager.SceneManager.LoadScene("Game", Single)
```

- `FindFirstObjectByType<GameBootstrapper>()` 패턴은 `NetworkGameFlow.cs`에서 이미 사용 중 — 동일 패턴 적용
- 서버에서 Despawn 시 NGO가 클라이언트에 자동으로 오브젝트 제거 전파
- `DestroyAllUnits()` 내부에서 이미 `networkObject.Despawn(destroy: true)` 처리 — 별도 코드 불필요

---

## 영향 범위

| 파일 | 변경 내용 |
|------|----------|
| `NetworkGameEndController.cs` | `StartRematch()` 내 정리 로직 추가 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| GameBootstrapper가 null인 경우 | null 체크 후 Debug.LogWarning, LoadScene은 그대로 진행 |
| Despawn 후 LoadScene 타이밍 | Despawn은 동기 처리이므로 완료 보장. LoadScene은 이후 호출 |
| 건물(Castle 등)도 Despawn 후 재생성되어야 함 | LoadMap()이 새 씬에서 PlaceCastles()를 다시 실행하므로 정상 |
