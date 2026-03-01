# 네트워크 미완성 항목 (코드 분석 결과, 2026-02-27)

- BuildFailedClientRpc / EnqueueFailedClientRpc: UI 피드백 미구현 (TODO 주석, 로그만)
- InputHandler.cs L261: 멀티플레이 유닛 이동 시 NetworkUnitMovementController 경유 누락
  - 현재: `_unitMovement.RequestMove()` 직접 호출 + `selectedView.MoveTo(path)` 로컬만 처리
  - 필요: `_networkMovement.RequestMove()` 경유하도록 수정 (네트워크 모드 분기 필요)
- ProductionPanelUI 자동생산 롱프레스: 멀티플레이 분기 없음 (TODO 주석, 로그 경고 후 return)
- NetworkGameEndController._lobbySceneName: "SampleScene" 하드코딩 → 실제 씬명 매칭 필요
- 생산 큐 추가 후 클라이언트 UI 즉시 갱신: SyncQueueStateClientRpc가 EnqueueUnit 이후 호출되나
  OnProductionQueueChanged가 서버에서만 발행 → 클라이언트 UI는 ProductionStartedClientRpc까지 대기
