# Hexiege - 작업 로드맵

**최종 수정일:** 2026-05-15  
**현재 단계:** 유닛 이동/전투/회전 시스템 전면 재설계 완료 (슬롯 폐기, MoveAlongPathV3 통일, RotateTowards 통일)
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 | UI 기획 후 진행 | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 에셋 제작 + 비주얼 폴리싱 | 에셋+UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ⬜ 백로그 | 방어/마법 타워 | 기능 | 대 |
| ⬜ 백로그 | 사운드/BGM | 기능 | 중 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | PlayFab 백엔드 | 기능 | 대 |

---

## Phase A — 네트워크 버그 수정

### A-1. BuildFailed/EnqueueFailed UI 피드백 누락
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`
- **증상**: 건물 배치/생산 큐 실패 시 사용자에게 아무 피드백 없음 (서버 로그만 출력)
- **현황**: `BuildFailedClientRpc` / `EnqueueFailedClientRpc` RPC 구조는 완성. 함수 내부에 UI 호출만 추가하면 됨
- **대기 이유**: 전반적인 UI 기획(토스트/팝업 디자인 등)을 먼저 진행한 후 구현 예정

---

## Phase B — 네트워크 미완성 기능

### B-1. 로비 UI 비주얼 폴리싱
- **현황**: MVVM 코드 완료 (2026-03-15). UI 에셋(버튼/패널 스프라이트) 미제작
- **남은 작업**: UI 에셋 제작 후 비주얼 폴리싱

### B-2. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

**✅ 밸런싱 인프라 완료 (2026-04-25)**: `UnitStatsConfig`, `BuildingStatsConfig` ScriptableObject 전환 완료. 코드 수정·재컴파일 없이 Unity Inspector에서 수치 직접 편집 가능.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 30 / 6 / 1.0 | DPS=3, cooldown≈2.0s |
| Assault HP/공격/사거리 | 50 / 1 / 2.0 | DPS=5, cooldown≈0.2s |
| Sniper HP/공격/사거리 | 30 / 10 / 5.0 | DPS≈3.3, cooldown≈3.0s |
| Castle HP | 50 | 게임 시간 조정 |

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
- 건설 후 자동 공격
- 방어 타워: 단일 타겟, 직선 사거리
- 마법 타워: 범위 공격, 마나 자원 추가 필요 가능성

### D-2. 건물 업그레이드 시스템
- Castle/Barracks/MiningPost 레벨업
- 골드 소모 + 생산 시간/수입/HP 증가

### D-3. 유닛 AI 상태머신
**✅ 기본 전투 AI 구현 완료 (2026-05-11)**: 슬롯/점유 시스템 폐기. 근접·원거리 모두 단일 상태 머신(MoveAlongPathV3) 적용. A* 이동 → 적 감지(DetectRange) → 직선 추격(EnterCombatPursuitV3) → 공격 → 재개(Lerp 정렬) 사이클 완성. 겹침 허용, 모든 유닛 동일 규칙 적용.
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

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
