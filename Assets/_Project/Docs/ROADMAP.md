# Hexiege - 작업 로드맵

**최종 수정일:** 2026-06-23
**현재 단계:** 로그인 팝업 CloseButton 무반응 버그 수정 완료 — 사운드/AI Inspector 작업 + 신규 유닛 프리팹 실기 테스트 예정
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| ✅ 완료 | 코드 리팩토링 7개 그룹 전체 | 아키텍처 | 대 |
| ✅ 완료 | 로그인 시스템 C# 구현 (Firebase Auth + GPGS) | 기능 | 중 |
| ✅ 완료 | 게임 화면 UI TC 62개 실기기 테스트 + END UI 버그 수정 | QA/버그 | 중 |
| ✅ 완료 | BuildingPlacementUI 씬 계층 재설계 (BP-001/BP-002 해결) | UI | 중 |
| ✅ 완료 | 패널 버튼 크기 불일치 수정 (PRD-001, BAP-001) — LayoutElement 균등화 | UI | 소 |
| 🔵 구현완료 | AI 시스템 — 코드 완료. Inspector 연결 + 실기 테스트 대기 | 기능 | 대 |
| 🔴 높음 | AI 시스템 — Inspector 작업 (AIConfig/Scenario에셋 생성, DifficultySelectView GO 배치) | UI/기능 | 소 |
| 🔴 높음 | 신규 유닛 프리팹 실기 테스트 + 후속 작업 (Animation Event 부착, UnitFactory 등록, StatsReference 스탯 확정) | 기능 | 대 |
| 🔴 높음 | 게임 화면 UI 크기/레이아웃 수정 잔여 (HUD-007, SET-004, SET-007/END-001, MULTI-END-002 — 5항목) | UI | 소 |
| ✅ 완료 | 전역 UI 시스템 (UIManager + SplashOverlay) | UI | 중 |
| 🔴 높음 | Login.unity 씬 로그인 UI 조립 + Firebase Console 설정 | UI/인프라 | 소 |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 (멀티) | UI | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 비주얼 폴리싱 (에셋 제작 완료 2026-05-30) | UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ✅ 완료 | 방어 타워(AutoTower) 공격 기능 | 기능 | 대 |
| 🔵 구현완료 | 사운드 시스템 — 코드 완료. AudioMixer Inspector 작업 + 실기 테스트 대기. 로비 볼륨 패널 미구현(별도 작업) | 기능 | 중 |
| 🔴 높음 | 사운드 시스템 — Inspector 작업 (AudioMixer 에셋 생성, Login.unity AudioManager 배치, SoundConfig 에셋 생성) | 기능/Inspector | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

---

## Phase A — 네트워크 버그 수정

### ✅ A-1. BuildFailed/EnqueueFailed UI 피드백 (멀티플레이 분기) — 완료 (2026-05-24)
- **완료 내용**: `GameEvents.OnToastRequested` Subject 패턴 도입. NetworkBuildingController / NetworkProductionController RPC 핸들러에서 Subject 발행 → ToastUI 구독. Presentation이 Infrastructure를 직접 참조하지 않는 구조 완성.
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`, `GameEvents.cs`, `ToastKey.cs`

---

## Phase B — 네트워크 미완성 기능

### B-1. 로비 UI 비주얼 폴리싱
- **현황**: MVVM 코드 완료 (2026-03-15). UI 에셋 제작 완료 (2026-05-30) — 아이콘 13종(탭/기능/로비버튼), 버튼 배경 2종(Primary/Secondary), 스피너 1종(HexOrb)
- **남은 작업**: 제작된 에셋을 로비 씬 Inspector에 연결하여 비주얼 폴리싱 진행

### B-2. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-0. 싱글플레이 AI 시스템
**🔵 코드 구현 완료 (2026-06-07)**: LocalPlayerDifficulty / AIConfig / AIScenarioConfig ScriptableObject / AIOpponentController(빌드오더+반응시스템+BFS) / GameBootstrapper 연동 / 로비 난이도 선택 UI(DifficultySelectView) 전체 완료. 3종족 시나리오 에셋 개편 완료 (2026-06-10): Human/Spirit/Transcendence 각 1개 에셋 × 3시나리오, Domain 레이어 아키텍처 정리.

**남은 작업**:
1. 싱글플레이 실기 테스트 (AI 동작, 난이도 선택, 3종족 시나리오 무작위 동작 확인)

---

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
**✅ 방어 타워 완료 (2026-06-01~02)**: TowerCombatUseCase 신규 구현. 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 서버 권위 처리. Human CannonTower 초기 방향(내 진영 0도/상대 진영 180도) 구현 완료.
- 마법 타워: 범위 공격, 마나 자원 추가 필요 가능성 — 미구현

### D-2. 건물 업그레이드 시스템
**✅ 완료 (2026-05-17~18)**: 종족별 단계 BuildingType 26종 확장, BuildingPlacementUseCase.UpgradeBuilding(), NetworkBuildingController RPC, ProductionPopup 업그레이드 버튼/아이콘, 철거 환불 누적 계산 완료. 전 종족 테스트 통과.

### D-2-1. 건물 철거 로직 구현
**✅ 완료 (2026-05-18~19)**: BuildingActionPanelUI를 통해 비생산 건물(채굴소/타워 등) 철거 가능. BuildingPanelBase.OnDemolishButtonClick()에 싱글/멀티 분기 공통 구현. 채굴소 전용 패널(일시정지 등)은 별도 작업.

### D-3. 유닛 AI 상태머신
**✅ 기본 전투 AI 구현 완료 (2026-05-11)**: 슬롯/점유 시스템 폐기. 근접·원거리 모두 단일 상태 머신(MoveAlongPathV3) 적용. A* 이동 → 적 감지(DetectRange) → 직선 추격(EnterCombatPursuitV3) → 공격 → 재개(Lerp 정렬) 사이클 완성. 겹침 허용, 모든 유닛 동일 규칙 적용.
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

### D-4. 신규 유닛 프리팹 완성 (16종)
**🔧 에디터 스크립트 완료 (2026-06-05)**: Human 5종(LittleKnight/SpearMan/BattleAxe/Tank/CannonCart)·Spirit 6종(DustSpirit/BoulderSpirit/QuakeSpirit/TideSpirit/StreamSpirit/TorrentSpirit)·Transcendence 5종(RabbitTrickster/RhinoBreaker 등) × Blue/Red 총 32개 프리팹 자동 컴포넌트 부착. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`

**남은 작업**:
1. 실기 테스트 — 프리팹 컴포넌트 부착 정상 동작 확인
2. Animation Event 부착 (각 유닛 공격 애니메이션 타이밍)
3. UnitFactory 종족별 리스트에 신규 16종 등록
4. StatsReference.md 스탯 확정 후 UnitStatsConfig Inspector 입력

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. Firebase 백엔드
- 실시간 글로벌 리더보드 (Firestore onSnapshot)
- 승/패 기록 저장 (Firestore)
- Android 인앱결제 (Google Play Billing — 스킨)
- Firebase Functions (경기 결과 처리, IAP 영수증 검증)

### E-4. 로그인 시스템 구현 (Login.unity)
- Login.unity 씬 신규 생성 (Build Index 분리)
- LoginUI (익명/Google Play Games/이메일+비밀번호 선택 화면)
- ProfileView 계정 연동 탭 구현 (익명 → 실계정 전환)
- AuthSystemRules.md 기준 구현
