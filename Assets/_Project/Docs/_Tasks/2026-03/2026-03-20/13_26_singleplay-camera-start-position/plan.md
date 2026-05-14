# Plan: 싱글플레이 ViewConverter 초기화 버그 수정

**날짜**: 2026-03-20
**담당**: game-programmer 에이전트

---

## 수정 목표

싱글플레이에서 `ViewConverter`를 `LocalPlayerTeam.Current` 기반으로 올바르게 초기화한다.
카메라 초기 위치는 기존과 동일하게 맵 중앙 유지.

---

## 수정 대상

**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

---

## 수정 — ViewConverter 초기화 방식 변경

**위치**: `LoadMap()` 시작 부분, 기존 `ViewConverter.Reset()` 제거 후 `ApplyConfig()` 직후에 추가

### 변경 전
```csharp
bool isNetworkMode = IsNetworkMode();
if (!isNetworkMode)
{
    ViewConverter.Reset();   // LocalPlayerTeam 무시하고 항상 Blue 고정
}

// ...
// 2. 설정 적용
ApplyConfig(orientation, oc);
```

### 변경 후
```csharp
bool isNetworkMode = IsNetworkMode();

// ...
// 2. 설정 적용
ApplyConfig(orientation, oc);

// 싱글플레이: LocalPlayerTeam 기반으로 ViewConverter 초기화.
// ApplyConfig() 이후에 호출해야 HexMetrics가 준비되어 GridCenter 계산이 정확함.
// 멀티플레이는 StartNetworkGame()에서 LoadMap() 전에 이미 설정하므로 여기서는 건너뜀.
if (!isNetworkMode)
{
    Vector3 mapCenter = HexMetrics.GridCenter(oc.GridWidth, oc.GridHeight);
    bool isRed = (LocalPlayerTeam.Current == TeamId.Red);
    ViewConverter.Setup(isRed, mapCenter);
}
```

---

## 영향 범위

| 경로 | 변경 전 | 변경 후 |
|------|---------|---------|
| 싱글플레이 Blue팀 | ViewConverter Blue 고정 | ViewConverter Blue ✅ (동일) |
| 싱글플레이 Red팀 | ViewConverter Blue 강제 → Red Castle 상단 ❌ | ViewConverter Red 반전, Red Castle 화면 하단 ✅ |
| 싱글플레이 카메라 | 맵 중앙 | 맵 중앙 유지 ✅ |
| 멀티플레이 전체 | 영향 없음 (`isNetworkMode=true`) | 동일 ✅ |

---

## 테스트 계획

- [ ] 싱글플레이 관점 변환 정상 동작 확인
- [ ] 싱글플레이 카메라 초기 위치 맵 중앙 유지 확인
- [ ] 멀티플레이 Blue/Red 정상 (영향 없음 확인)
