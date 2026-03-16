# Hexiege 프로젝트 메인 메모리

**최종 갱신:** 2026-03-16

## 프로젝트 개요
- **장르**: 모바일 1v1 실시간 전략 (RTS), 육각형 타일맵 기반 공성전
- **엔진**: Unity 6000.0.x (URP), C# 9.0
- **네트워크**: Netcode for GameObjects 2.9.2 + Unity Multiplayer Services 2.0.0
- **이벤트**: UniRx 7.1.0
- **3D 모델**: Meshy.ai Image-to-3D → Mixamo 애니메이션

## 현재 상태 (2026-03-16 기준)
- 싱글플레이 코어 루프 완성
- 멀티플레이 Phase 1~8 완성
- 로비 씬 분리 MVVM 완료 (Lobby.unity + Game.unity)
- **완료**: 랜덤 매칭 후 게임 씬 전환 안 되는 버그 수정 (2026-03-16)
  - `MatchmakerManager.DetermineIsHostAsync` — `GetHashCode()` 제거 → `GetStableHash()` (polynomial hash) 로 교체
  - `NetworkGameManager.HostGameAsync` — `OnClientConnectedCallback` 등록을 `StartNetworkHost()` 이전으로 이동
- **보류**: 전역 로딩 스크린 구현 (task 문서 작성 완료, 구현 미착수)
- 다음 우선순위: 로딩 스크린 구현, 랜덤 매칭 추가 테스트 (반복 매칭 무작위성 검증)

## 절대 규칙 (CLAUDE.md 핵심)
1. 사용자가 명시적으로 요청한 파일만 생성/수정
2. 승인 후 즉시 구현 금지 → "task 문서 작성할까요?" 먼저 질문
3. 코드/설계 → 전문 에이전트 위임 (game-programmer / game-design-lead)
4. 테스트 완료 후 → "문서/메모리 업데이트를 진행할까요?" 확인
5. git 명령 절대 금지 (git restore 사고로 작업 전체 삭제된 전례 있음)

## 아키텍처 핵심
- **레이어**: Domain ← Application ← Infrastructure / Presentation ← Application
- **Domain → Core 참조 금지** (HexOrientationContext 정적 홀더 패턴 사용)
- **GameBootstrapper** = 유일한 의존성 조합 루트
- **NetworkBehaviour** = Infrastructure 레이어에만 배치
- **Application 레이어**: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 사용
- No Assembly Definitions — 네임스페이스 규약으로만 레이어 경계 관리

## 좌표계
- XZ 평면 (Y=0 바닥, Y=높이)
- HexMetrics.HexToWorld() → Vector3(x, 0f, z)
- ViewConverter: Red팀 좌표 반전 `2*center - pos` (X, Z만 반전, Y 보존)
- 올바른 초기화 순서: ViewConverter.Setup() → LoadMap() (순서 바꾸면 건물 위치 버그)

## 에이전트 메모리 경로
```
.claude/agent-memory/game-programmer/MEMORY.md
.claude/agent-memory/game-design-lead/MEMORY.md
.claude/agent-memory/qa-tester/MEMORY.md
.claude/agent-memory/asset-prompt-crafter/MEMORY.md
.claude/agent-memory/project-orchestrator/MEMORY.md
```

## 주요 문서 경로
- 기획서: `Assets/_Project/Docs/GameDesignDocument.md`
- 기술설계: `Assets/_Project/Docs/TechnicalDesignDocument.md`
- 진행현황: `Assets/_Project/Docs/PROJECT_STATUS.md`
- 로드맵: `Assets/_Project/Docs/ROADMAP.md`
- 작업규칙: `Assets/_Project/Docs/_Tasks/README.md`
