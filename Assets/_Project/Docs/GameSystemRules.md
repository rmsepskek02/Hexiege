# Game System Rules — 인덱스

구현 시 따라야 하는 게임 시스템별 규칙 모음.
아이디어나 기획 의도가 아닌, 실제 코드로 구현할 때 기준이 되는 구체적인 규칙을 기록한다.

세부 규칙은 아래 파일에 있다. Plan.md 작성 전 관련 파일을 반드시 읽는다.

---

## 파일 목록

| 파일 | 포함 시스템 |
|------|------------|
| [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) | 공통 UI 규칙, 생산 패널 UI, 건물 배치 패널 UI, 인게임 설정 메뉴 |
| [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md) | 유닛 이동 시스템, 전투 진입, 전투 연계 |
| [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 랠리포인트 시스템, 건물 철거 시스템, 방어 타워 시스템 |
| [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md) | AI 난이도 시스템, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘 |
| [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md) | Human 종족 AI 빌드오더 시나리오 A/B/C |

---

## 시스템별 빠른 참조

### UI 관련 작업
→ [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md)
- Canvas Scaler, 앵커 기반 배치, Safe Area, CanvasGroup 숨김/표시
- 폰트, 골드 부족 표시, 팝업/모달 타입 구분
- 생산 패널: 큐 구조, 골드 차감 시점, 자동 생산, 토스트 메시지
- 건물 배치 패널: 비용 표시, 실패 피드백
- 인게임 설정 메뉴: 일시정지, 포기 처리

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
