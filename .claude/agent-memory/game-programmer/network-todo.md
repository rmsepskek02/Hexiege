# 네트워크 미완성 항목 (코드 분석 결과, 2026-02-27)

- BuildFailedClientRpc / EnqueueFailedClientRpc: UI 피드백 미구현 (RPC 구조 완성, 함수 내부에 UI 호출만 추가하면 됨) → UI 기획 후 구현 예정
- ~~ProductionPanelUI 자동생산 롱프레스: 멀티플레이 분기 없음~~ → 완료 (2026-03-07): ToggleAutoServerRpc + AutoProductionChangedClientRpc 정상 구현 확인
- NetworkGameEndController._lobbySceneName: "SampleScene" 하드코딩 → 실제 씬명 매칭 필요
- ~~생산 큐 추가 후 클라이언트 UI 즉시 갱신: SyncQueueStateClientRpc가 EnqueueUnit 이후 호출되나 OnProductionQueueChanged가 서버에서만 발행 → 클라이언트 UI는 ProductionStartedClientRpc까지 대기~~ → 완료 (2026-03-01)
