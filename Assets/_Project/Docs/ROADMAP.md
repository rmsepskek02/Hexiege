# Hexiege - 작업 로드맵

**최종 수정일:** 2026-05-19  
**현재 단계:** 인게임 설정 메뉴 + 게임 포기 기능 완료 (InGameSettingsUI + ConfirmPopup)
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

### A-1. BuildFailed/EnqueueFailed UI 피드백 (멀티플레이 분기)
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`
- **현황**: 싱글플레이 생산 실패 피드백 완료 (2026-05-16 — ToastUI 범용 시스템 구축 포함)
- **남은 작업**: `EnqueueFailedClientRpc` / `BuildFailedClientRpc` 내부에 `ToastUI.Show()` 호출 추가 (멀티플레이 분기)
- **비고**: ToastUI 시스템이 이미 완성되어 있어 RPC 핸들러에 1~2줄 추가만 필요

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

**✅ 건물 스탯 전체 확정 (2026-05-18)**: StatsReference.md 기준으로 모든 종족 건물 HP/비용/공격력/쿨다운 확정. BuildingStatsConfig.asset 32종 항목 전부 채움.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 30 / 6 / 1.0 | DPS=3, cooldown≈2.0s |
| Assault HP/공격/사거리 | 50 / 1 / 2.0 | DPS=5, cooldown≈0.2s |
| Sniper HP/공격/사거리 | 30 / 10 / 5.0 | DPS≈3.3, cooldown≈3.0s |
| Castle HP (Human/Spirit/Trans) | 200 / 150 / 300 | 확정 — 플레이테스트 후 조정 |
| AutoTower 공격력/쿨다운 (Human) | 15 / 5.0s | 확정 |
| AutoTower 공격력/쿨다운 (Spirit) | 15 / 3.5s | 확정 — 가장 강한 타워 |
| AutoTower 공격력/쿨다운 (Trans) | 15 / 5.0s | 확정 |
| MistShrine 힐량/범위 (Trans) | 1 HP/s / 범위 3 | 확정 — 플레이테스트 후 조정 |

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
- 건설 후 자동 공격
- 방어 타워: 단일 타겟, 직선 사거리
- 마법 타워: 범위 공격, 마나 자원 추가 필요 가능성

### D-2. 건물 업그레이드 시스템
**✅ 완료 (2026-05-17~18)**: 종족별 단계 BuildingType 26종 확장, BuildingPlacementUseCase.UpgradeBuilding(), NetworkBuildingController RPC, ProductionPopup 업그레이드 버튼/아이콘, 철거 환불 누적 계산 완료. 전 종족 테스트 통과.

### D-2-1. 건물 철거 로직 구현
**✅ 완료 (2026-05-18~19)**: BuildingActionPanelUI를 통해 비생산 건물(채굴소/타워 등) 철거 가능. BuildingPanelBase.OnDemolishButtonClick()에 싱글/멀티 분기 공통 구현. 채굴소 전용 패널(일시정지 등)은 별도 작업.

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
