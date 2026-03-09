# Hexiege - 작업 로드맵

**최종 수정일:** 2026-03-09
**현재 단계:** 멀티플레이 Phase 8 완료 / 3D 전환 완료 / Walk 애니메이션 수정 완료 / TechnicalDesignDocument 현행화 완료 → 밸런싱 및 추가 유닛 작업 예정

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 | UI 기획 후 진행 | 소 |
| ~~🟡 중간~~ | ~~TechnicalDesignDocument.md 3D 업데이트~~ | ✅ 완료 (2026-03-09) | - |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 추가 유닛 타입 | 기능 | 대 |
| 🟢 낮음 | 멀티플레이 로비 UI 완성 | 기능 | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ⬜ 백로그 | 3종족 시스템 | 기능 | 대 |
| ⬜ 백로그 | 방어/마법 타워 | 기능 | 대 |
| ⬜ 백로그 | 사운드/BGM | 기능 | 중 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | PlayFab 백엔드 | 기능 | 대 |

---

## Phase A — 네트워크 버그 수정 (긴급)

현재 멀티플레이에서 발생하는 알려진 버그/미완성 항목들.

### A-1. BuildFailed/EnqueueFailed UI 피드백 누락
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`
- **증상**: 건물 배치/생산 큐 실패 시 사용자에게 아무 피드백 없음 (서버 로그만 출력)
- **현황**: `BuildFailedClientRpc` / `EnqueueFailedClientRpc` RPC 구조는 완성. 함수 내부에 UI 호출만 추가하면 됨
- **대기 이유**: 전반적인 UI 기획(토스트/팝업 디자인 등)을 먼저 진행한 후 구현 예정

---

## Phase B — 네트워크 미완성 기능

### B-2. 멀티플레이 로비 UI 완성
- **파일**: `Assets/_Project/Scripts/Presentation/UI/LobbyUI.cs`
- **현황**: 기본 Host/Join 기능만 구현
- **추가 필요**: 방 목록, 방 생성 옵션, 대기 화면, 플레이어 연결 상태 표시

### B-3. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP | 10 | 전투 지속시간 조정 |
| Pistoleer 공격력 | 3 | DPS vs Castle HP 비율 |
| Pistoleer 생산 시간 | 5초 | 생산량 vs 수입 균형 |
| Castle HP | 50 | 게임 시간 조정 |

### C-2. 추가 유닛 타입
- 현재 Pistoleer(권총병) 1종만 존재
- 최소 2종 추가 권장 (근접, 원거리 혹은 방어/공격 역할 구분)
- 에셋 파이프라인: Meshy.ai Image-to-3D → Mixamo Rig → Unity Animator
- 구현 순서: game-design-lead(설계) → asset-prompt-crafter(모델) → game-programmer(코드)

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 3종족 시스템
- 각 종족마다 고유 유닛/건물/패시브 차별화
- 매칭 화면에서 종족 선택

### D-2. 방어/마법 타워
- 건설 후 자동 공격
- 방어 타워: 단일 타겟, 직선 사거리
- 마법 타워: 범위 공격, 마나 자원 추가 필요 가능성

### D-3. 건물 업그레이드 시스템
- Castle/Barracks/MiningPost 레벨업
- 골드 소모 + 생산 시간/수입/HP 증가

### D-4. 유닛 AI 상태머신
- 현재: 이동 중 인접 적 자동 공격 (하드코딩)
- 목표: Idle → Patrol → Chase → Attack → Retreat 상태 전환

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. PlayFab 백엔드
- 계정 시스템 (로그인/회원가입)
- 랭킹 (승/패 기록)
- 인앱결제 (스킨/종족 언락)

---

## 문서 관리 워크플로우

새 작업 시작 전 반드시:

1. `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/research.md` 작성
   - 관련 코드 파악, 영향 범위, 현재 상태 정리
2. `Assets/_Project/Docs/_Tasks/YYYY-MM-DD/HH_MM_[작업명]/plan.md` 작성
   - 구현 접근법, 파일별 변경 내용, 위험 요소
3. 사용자 승인 후 구현 시작

---

## 완료된 마일스톤

| 날짜 | 마일스톤 |
|------|---------|
| 2026-02 이전 | 싱글플레이 코어 루프 완성 (헥스, 전투, 건물, 생산, 승패) |
| 2026-02 | 멀티플레이 Phase 1~8 완성 |
| 2026-02-27~03-01 | 2D→3D 전환 완료 (XZ 좌표계, 55도 카메라, 3D 모델) |
| 2026-03-02 | 전투 거리 정밀도 버그 수정 (IEntityPositionProvider) |
| 2026-03-02 | GameConfig 정리 (AnimationFps 제거, TileHeight 수정) |
| 2026-03-07 | 공격 방향 Transform 기반 구현 완료 (UnitView._meshYOffset=30f, Atan2 기반 방향 계산) |
| 2026-03-07 | 유닛별 AttackCooldown 시스템 완료 (UnitData.AttackCooldown/AttackCooldownRemaining) |
| 2026-03-01 | 생산 큐 클라이언트 UI 즉시 갱신 수정 (OnProductionQueueChanged → SyncQueueStateClientRpc) |
| 2026-03-07 | 자동생산 멀티플레이 지원 완료 (ToggleAutoServerRpc + AutoProductionChangedClientRpc) |
| 2026-03-07 | Siege/AI 이동 서버 권위 동기화 완료 (BroadcastServerMove + BroadcastMoveClientRpc, 클라이언트 화면 불일치 수정) |
