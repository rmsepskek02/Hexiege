# Plan: 코드 정리 (Code Cleanup)

**날짜**: 2026-03-20
**담당**: game-programmer 에이전트

---

## 최종 확정 범위

| 항목 | 결정 | 근거 |
|------|------|------|
| TeamAssigner.cs 삭제 | ✅ 진행 | 코드 참조 없음, NetworkGameFlow로 완전 대체 확인 |
| LocalPlayerTeam.cs 주석 정리 | ✅ 진행 | TeamAssigner 관련 구 주석 제거 |
| NetworkGameFlow.cs 주석 정리 | ✅ 진행 | TeamAssigner 관련 구 주석 제거 |
| IsNetworkMode() 헬퍼 추출 | ✅ 진행 | GameBootstrapper.cs 4곳 중복 제거 |
| ShopView / ProfileView / RankingView | ❌ 유지 | 추후 구현 예정 플레이스홀더 |
| RandomMatchView | ❌ 유지 | View는 완성됨, ViewModel 연동만 미완 |
| 장문 메서드 분리 | ❌ 보류 | 초기화 의존성/입력 버그 위험 |
| TODO 주석 | ❌ 유지 | 로드맵 연결 미구현 기능 |

---

## 작업 상세

### 1. TeamAssigner.cs 삭제

**삭제 파일**:
- `Assets/_Project/Scripts/Infrastructure/Network/TeamAssigner.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/TeamAssigner.cs.meta`

**검증 내용**:
- `TeamAssigner` 타입을 코드로 참조하는 파일 없음 확인 완료
- `LocalPlayerTeam.Set()`은 `NetworkGameFlow.WaitForTeamAndSendReady()`에서 호출됨
- Player Prefab = None이므로 스폰 자체가 발생하지 않음

---

### 2. LocalPlayerTeam.cs 주석 정리

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/LocalPlayerTeam.cs`

**변경 대상**: TeamAssigner를 언급하는 주석들을 NetworkGameFlow 기준으로 교체
- L6: `TeamAssigner가 네트워크에서 팀을 받아오면 여기에 저장` → `NetworkGameFlow에서 팀이 결정되면 여기에 저장`
- L11: `// 팀 설정 (TeamAssigner에서 호출)` → `// 팀 설정 (NetworkGameFlow에서 호출)`
- L26: `TeamAssigner가 서버로부터 팀을 받으면 Set()을 호출해 갱신.` → `NetworkGameFlow에서 팀을 결정하면 Set()을 호출해 갱신.`
- L38: `TeamAssigner가 실제로 팀을 할당했는지 여부.` → `NetworkGameFlow가 실제로 팀을 할당했는지 여부.`
- L47: `TeamAssigner.OnNetworkSpawn() 또는 OnTeamAssigned 이벤트에서 호출.` → `NetworkGameFlow.WaitForTeamAndSendReady()에서 호출.`

---

### 3. NetworkGameFlow.cs 주석 정리

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs`

**변경 대상**:
- L12 (파일 상단 주석): `2. 각 클라이언트: TeamAssigner 준비 대기 후 RequestReadyServerRpc() 호출`
  → `2. 각 클라이언트: IsHost 기반으로 팀 직접 결정 후 RequestReadyServerRpc() 호출`

---

### 4. GameBootstrapper.cs — IsNetworkMode() 헬퍼 추출

**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

**현재 (4곳에서 동일하게 반복)**:
```csharp
bool isNetworkMode = Unity.Netcode.NetworkManager.Singleton != null &&
    (Unity.Netcode.NetworkManager.Singleton.IsHost ||
     Unity.Netcode.NetworkManager.Singleton.IsClient);
```

**추가할 메서드** (private 영역 적절한 위치에):
```csharp
/// <summary>
/// 현재 네트워크 모드(멀티플레이)로 실행 중인지 확인합니다.
/// Host 또는 Client로 연결된 경우 true를 반환합니다.
/// </summary>
private bool IsNetworkMode()
{
    return NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);
}
```

**적용 위치**: 라인 227-229, 261-263, 585-587, 618-620의 중복 블록 → `IsNetworkMode()` 호출로 교체

---

## 테스트 계획

- [ ] Unity 컴파일 오류 없음
- [ ] 싱글플레이 정상 동작
- [ ] 멀티플레이 호스트/클라이언트 연결 및 팀 할당 정상
- [ ] 게임 시작 흐름 정상 (Blue: 하단, Red: 반전 하단)
