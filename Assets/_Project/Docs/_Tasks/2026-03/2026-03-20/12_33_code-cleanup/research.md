# Research: 코드 정리 (Code Cleanup)

**날짜**: 2026-03-20
**분석 대상**: `Assets/_Project/Scripts/` 전체 (88개 C# 파일)

---

## 분석 방법

88개 C# 스크립트를 레이어별로 전수 조사. 파일 내용, 참조 관계, 중복 패턴 확인.

---

## 발견 항목

### A. 완전히 미사용되는 파일

#### 1. `Infrastructure/Network/TeamAssigner.cs` (174줄)
- **근거**: MEMORY 기록 및 코드 확인 — "Player Prefab = None" 설정으로 TeamAssigner가 스폰되지 않음
- **대체**: `NetworkGameFlow.WaitForTeamAndSendReady()`에서 `IsHost ? Blue : Red`로 직접 팀 할당
- **결론**: 완전 미사용, 제거 가능

#### 2. `Presentation/UI/Views/Lobby/Shop/ShopView.cs` (~20줄)
- **근거**: MonoBehaviour만 상속, 필드/메서드 없음. 주석: "추후 구현 예정"
- **결론**: 빈 플레이스홀더, 제거 가능

#### 3. `Presentation/UI/Views/Lobby/Profile/ProfileView.cs` (~20줄)
- **근거**: 위와 동일한 빈 파일
- **결론**: 빈 플레이스홀더, 제거 가능

#### 4. `Presentation/UI/Views/Lobby/Ranking/RankingView.cs` (~20줄)
- **근거**: 위와 동일한 빈 파일
- **결론**: 빈 플레이스홀더, 제거 가능

---

### B. 중복 코드

#### 1. 네트워크 모드 판별 로직 4회 반복 (`GameBootstrapper.cs`)
```csharp
bool isNetworkMode = Unity.Netcode.NetworkManager.Singleton != null &&
    (Unity.Netcode.NetworkManager.Singleton.IsHost ||
     Unity.Netcode.NetworkManager.Singleton.IsClient);
```
- **발견 위치**: 라인 227-229, 261-263, 585-587, 618-620
- **결론**: private 헬퍼 메서드로 추출 가능

---

### C. 장문 메서드 (리팩토링 대상)

#### 1. `GameBootstrapper.LoadMap()` (72줄)
- 13단계 초기화 작업을 한 메서드에서 순차 처리
- 그리드 생성, 건물 배치, 타일 동기화, 카메라 설정 등이 혼재
- **제안**: 각 단계 별도 메서드로 분리

#### 2. `GameBootstrapper.StartNetworkGame()` (47줄)
- ViewConverter 설정, LoadMap 호출, 카메라 초기화를 모두 처리
- **제안**: SetupViewConverter(), SetupNetworkCamera() 등으로 분리

#### 3. `CameraController.HandleZoom()` (57줄)
- 마우스 스크롤 / 핀치(2터치) / DOTween 로직 혼재
- **제안**: HandleMouseZoom(), HandlePinchZoom()으로 분리

#### 4. `CameraController.HandlePan()` (72줄)
- 마우스 팬 / 터치 팬 로직 혼재
- **제안**: HandleMousePan(), HandleTouchPan()으로 분리

---

### D. TODO 주석 (미구현 기능)

| 파일 | 라인 | 내용 |
|------|------|------|
| `NetworkBuildingController.cs` | 258 | `// TODO: UI 피드백 — 토스트 메시지, 버튼 흔들기 효과 등` |
| `NetworkProductionController.cs` | 591 | `// TODO: UI 피드백 — 토스트 메시지 등` |

- **결론**: ROADMAP의 "BuildFailed/EnqueueFailed UI 피드백" 작업과 연결. 이번 정리에서는 제외, 별도 작업으로 처리.

---

### E. 플레이스홀더 (미완성 기능)

#### `Presentation/UI/Views/Lobby/Battle/RandomMatchView.cs` (102줄)
- 매칭 대기 화면 UI는 있으나 실제 Matchmaker 연동 미완성
- 주석: "현재 플레이스홀더, 실제 Matchmaker 연동은 추후"
- **결론**: 삭제보다 유지 권장 (UI 구조 있음, 추후 기능 연동 예정)

---

### F. 아키텍처 준수 현황

| 규칙 | 상태 |
|------|------|
| Domain → Core 참조 금지 | ✅ 위반 없음 |
| 레이어 경계 준수 | ✅ 위반 없음 |
| GameBootstrapper 단일 composition root | ✅ 준수 |
| using 지시문 미사용 | ✅ 발견 없음 |

---

## 결론 요약

| 분류 | 항목 | 총 라인 수 |
|------|------|----------|
| 완전 제거 대상 | TeamAssigner + 빈 뷰 3개 | ~234줄 |
| 리팩토링 대상 | GameBootstrapper 메서드 분리, CameraController 입력 분리 | - |
| 중복 제거 | 네트워크 모드 판별 헬퍼 추출 | ~12줄 감소 |
| 보류 (별도 작업) | TODO 주석 2개, RandomMatchView | - |
