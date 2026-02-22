# Project Orchestrator Memory — Hexiege

## 프로젝트 현재 상태 (2026-02-22)
- 싱글플레이 코어 루프 완성 (헥스 그리드, 전투, 건물, 생산, 승패)
- 멀티플레이 Phase 1~8 완성 (Lobby/Relay/NGO, 건물/생산/이동/전투/HUD/재접속)
- ViewConverter 타이밍 버그 수정 완료 (Setup → LoadMap 순서 교정)
- BuildingFactory sortingOrder 버그 수정 완료 (FlatTop 모드 동적 sortingOrder 설정)
- Red팀 건물 position 버그 수정 완료 (2026-02-22)
  - 원인: GameBootstrapper.StartNetworkGame()에서 HexMetrics.Orientation 미설정 상태로 GridCenter() 호출
  - 수정: GridCenter() 호출 전 HexMetrics 사전 설정 (Orientation=FlatTop, TileWidth, TileHeight)
  - 수정 파일: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- 다음 단계: 실기기 테스트 / 3종족 유닛 시스템 기획

## 사용 가능한 에이전트

| 에이전트 | 역할 | MEMORY 경로 |
|---------|------|-------------|
| game-programmer | 코드 구현, 버그 수정, 아키텍처 적용 | `.claude/agent-memory/game-programmer/MEMORY.md` |
| game-design-lead | 게임플레이 설계, 밸런스, 기획 결정 | `.claude/agent-memory/game-design-lead/MEMORY.md` |
| qa-tester | 구현 검증, 버그 체크리스트, 패턴 분석 | `.claude/agent-memory/qa-tester/MEMORY.md` |
| asset-prompt-crafter | AI 스프라이트 프롬프트 생성 | `.claude/agent-memory/asset-prompt-crafter/MEMORY.md` |
| project-orchestrator | 작업 분해, 위임, 조율 (본인) | `.claude/agent-memory/project-orchestrator/MEMORY.md` |

## 작업 유형별 위임 패턴

| 작업 유형 | 담당 에이전트 | 비고 |
|----------|-------------|------|
| 새 기능 코드 구현 | game-programmer | 아키텍처 규칙 컨텍스트 전달 필수 |
| 버그 수정 | game-programmer | 관련 파일 경로 + 증상 전달 |
| 게임 설계 결정 | game-design-lead | 현재 구현 상태 컨텍스트 전달 |
| 구현 후 검증 | qa-tester | 변경된 파일 목록 + 예상 동작 전달 |
| 스프라이트 제작 | asset-prompt-crafter | 아트 스타일 컨텍스트 전달 |
| 복합 기능 (설계+구현+검증) | 순차: design-lead → programmer → qa-tester | |

## 위임 시 필수 컨텍스트 항목
모든 위임 시 반드시 포함:
1. 관련 파일 경로 (절대 경로, `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/Assets/...`)
2. Clean Architecture 레이어 규칙 (Domain이 Core 참조 불가 등)
3. 현재 프로젝트 상태 요약
4. 해당 에이전트 MEMORY.md 경로

## 작업 완료 후 메모리 업데이트 체크리스트
- [ ] game-programmer MEMORY.md: 새 파일/클래스/API 매핑 추가
- [ ] qa-tester MEMORY.md: 새 취약 지점, 테스트 체크리스트 항목 추가
- [ ] game-design-lead MEMORY.md: 구현 완료 항목 이동, 미결 항목 갱신
- [ ] 메인 MEMORY.md (C:/Users/rmsep/.claude/...): 아키텍처 결정사항 반영
- [ ] project-orchestrator MEMORY.md: 현재 상태 요약 갱신

## 사용자 워크플로우 선호사항
- 사용자는 총괄(project-orchestrator)에게 요청 → 총괄이 각 에이전트에게 분배
- 복잡한 작업(2개 이상 파일, 새 기능, 설계 결정)은 먼저 계획 제시 후 실행
- 한국어로 소통
- 에이전트 완료 후 반드시 메모리 업데이트

## 프로젝트 핵심 제약
- 스크립트: `Assets/_Project/Scripts/` 아래 레이어별 폴더
- No Assembly Definitions — 네임스페이스 규약으로만 레이어 경계 관리
- Domain → Core 참조 금지 (HexOrientationContext 정적 홀더 패턴 사용)
- GameBootstrapper = 유일한 의존성 조합 루트
- ViewConverter: Red팀 좌표 반전, 스프라이트 뒤집기 없음
  - 올바른 초기화 순서: ViewConverter.Setup() → LoadMap() (LoadMap 내부에서 ToView 호출하므로)
  - Setup() 전에 HexMetrics.Orientation과 TileWidth/TileHeight를 반드시 사전 설정 (GridCenter 정확성 필수)
  - LoadMap() 내 Reset()은 싱글플레이 경로(isNetworkMode=false)에서만 실행
- 멀티플레이: NGO 2.9.2, Enable Scene Management = ON
- sortingOrder 계층 (FlatTop): 타일(0~29) < 금광(+1) < 건물(+50) < 유닛(100고정)
  - Factory 신규 작성 시 반드시 동적 sortingOrder 설정 필수
