# Game System Rules — 인덱스

구현 시 따라야 하는 게임 시스템별 규칙 모음.
아이디어나 기획 의도가 아닌, 실제 코드로 구현할 때 기준이 되는 구체적인 규칙을 기록한다.

세부 규칙은 아래 파일에 있다. Plan.md 작성 전 관련 파일을 반드시 읽는다.

---

## 파일 목록

| 파일 | 포함 시스템 |
|------|------------|
| [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) | 공통 UI 규칙, 생산 패널 UI, 건물 배치 패널 UI, 인게임 설정 메뉴, 로비 설정/프로필 UI |
| [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md) | 유닛 이동 시스템, 전투 진입, 전투 연계 |
| [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 랠리포인트 시스템, 건물 철거 시스템, 방어 타워 시스템 |
| [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md) | Canvas SortingOrder 구조, 씬별 Canvas 계층, 전역 UI z-order |
| [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md) | BGM 전환 규칙, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md) | AI 난이도 시스템, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘 |
| [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md) | Human 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md) | Spirit 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md) | Transcendence 종족 AI 빌드오더 시나리오 A/B/C |

---

### 사운드 관련 작업
→ [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md)
- AudioManager 레이어 및 DontDestroyOnLoad 규칙
- BGM 전환 시점 (Login/Lobby/Battle/Victory/Defeat)
- BGM 크로스페이드 방식
- SFX 2D 고정, 동시 재생 한도 8개
- VFX+SFX 쌍 호출 규칙
- 볼륨 채널 (Master/BGM/SFX), PlayerPrefs 저장
- 볼륨 컨트롤 버튼 (전체 소리켜기/음소거/초기화/뒤로), 슬라이더 색상

---

## 시스템별 빠른 참조

### UI 관련 작업
→ [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md)
- Canvas SortingOrder 구조는 [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md)를 함께 확인
- Canvas Scaler, 앵커 기반 배치, Safe Area, CanvasGroup 숨김/표시
- 폰트, 골드 부족 표시, 팝업/모달 타입 구분
- 생산 패널: 큐 구조, 골드 차감 시점, 자동 생산, 토스트 메시지
- 건물 배치 패널: 비용 표시, 실패 피드백
- 인게임 설정 메뉴: 일시정지, 포기 처리, 프로필 서브 패널
- 로비 설정/프로필 UI: ProfilePanel/SettingPanel 탭 분리

### 유닛 관련 작업
→ [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md)
- A* 이동, 공유 타일 상태, 경로 재계산
- 상태 머신 (A* 이동 / 전투 이동 / 공격)
- 감지/공격 사거리, 타겟 선택, AoE

### 건물 관련 작업
→ [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md)
- 랠리포인트 표시/숨김
- 철거 처리, 골드 환불, 연쇄 처리
- 방어 타워: 타겟 선택, 쿨다운, 서버 권위 처리

### AI 시스템 관련 작업
→ [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md)
- 난이도 파라미터 (AIConfig ScriptableObject)
- 빌드오더 스크립트 (Phase 1~4 구조, actionType 정의)
- 반응 시스템 (R1 유닛열세, R2 골드과잉, R3 MiningPost 파괴 감지)
- 건물 배치 로직 (BFS 타일 선택, MiningPost 병행 트랙)
- 가드 메커니즘 (재시도, 생산 취소 1회 한도)

→ [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md)
- Human 종족 시나리오 A (물량형), B (테크형), C (균형형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md)
- Spirit 종족 시나리오 A (Spirit-Inferno 불 집중형), B (Spirit-Torrent 물 집중형), C (Spirit-Quake 땅 집중형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md)
- Transcendence 종족 시나리오 A (Trans-Rush 초반 물량형), B (Trans-Flora 동물A+식물 균형형), C (Trans-Beast 동물 고테크형) 빌드오더 테이블
