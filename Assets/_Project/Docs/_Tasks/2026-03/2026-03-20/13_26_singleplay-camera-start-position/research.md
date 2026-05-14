# Research: 싱글플레이 ViewConverter 미초기화 버그

**날짜**: 2026-03-20
**증상**: 싱글플레이에서 내 진영이 화면 하단이 아닌 상단에 보임

---

## 설계 원칙

- **Blue팀**: Blue Castle → 화면 하단
- **Red팀**: Red Castle → ViewConverter 반전 → 화면 하단
- **공통**: 항상 "내 진영 = 화면 하단, 적 진영 = 화면 상단"

---

## 멀티플레이 vs 싱글플레이 비교

### 멀티플레이 (정상)
```
StartNetworkGame(localTeam)
  1. ViewConverter.Setup(isRed, mapCenter)  ← LocalPlayerTeam 기반 관점 설정
  2. LoadMap()                              ← 설정된 관점으로 렌더링
  3. SetCameraStartPositionForTeam(localTeam, oc)
```

### 싱글플레이 (버그)
```
LoadMap()
  1. ViewConverter.Reset()   ← 항상 Blue 관점 강제 (LocalPlayerTeam 무시) ❌
  2. 렌더링 ...
  3. SetupCamera()           ← 맵 중앙 고정, 팀 기준 배치 없음 ❌
```

---

## 버그 원인

`ViewConverter.Reset()`이 `LocalPlayerTeam.Current`를 무시하고 항상 Blue 관점으로 고정.

- 사용자가 **Red팀**이면 → ViewConverter 반전이 없어 Red Castle이 화면 **상단**에 표시 ❌
- 사용자가 **Blue팀**이면 → 우연히 맞음 ✅

---

## 수정 방향

싱글플레이도 멀티플레이와 동일하게:
1. `ViewConverter.Setup(isRed, mapCenter)` — LocalPlayerTeam 기반 관점 설정
2. `SetCameraStartPositionForTeam(LocalPlayerTeam.Current, oc)` — 내 진영 기준 카메라 배치

ViewConverter.Setup은 LoadMap() 내 렌더링(PlaceCastles, RenderGrid) **전에** 설정되어야 함.
→ `ApplyConfig()` 직후 호출 (HexMetrics 설정 완료 후 mapCenter 계산 가능).
