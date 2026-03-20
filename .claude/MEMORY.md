# 에이전트 공용 컨텍스트

> **모든 에이전트는 작업 시작 전 이 파일을 반드시 읽을 것.**

---

## 프로젝트 개요
- 장르: 모바일 1v1 RTS, 헥스 타일맵 기반 공성전 (9:16 세로)
- 엔진: Unity 6000.0.x (URP), C# 9.0, NGO 2.9.2
- 씬: Lobby.unity (Build Index 0), Game.unity (Build Index 1)
- 레이어: Domain → Application → Core → Infrastructure → Presentation → Bootstrap

---

## 아키텍처 핵심 제약 (위반 시 컴파일 오류 또는 런타임 버그)

| 제약 | 내용 |
|------|------|
| Domain → Core 참조 금지 | `using Hexiege.Core` in Domain 파일 불가 → HexOrientationContext 정적 홀더 사용 |
| GameBootstrapper | 유일한 의존성 조합 루트 — 다른 곳에서 직접 의존성 주입 금지 |
| NetworkBehaviour 위치 | Infrastructure 레이어에만 (Presentation/Application 금지) |
| Application → Netcode | Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더 사용 |
| Assembly Definitions | 없음 — 네임스페이스 규약으로만 레이어 경계 관리 |
| NGO RPC 메서드명 | 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함 |
| NGO 설정 | Enable Scene Management = ON 필수 |

---

## 절대 규칙 참조
→ `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/CLAUDE.md`

## 작업 사이클 상세 참조
→ `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/README.md`

---

## 에이전트별 MEMORY.md 경로

| 에이전트 | MEMORY.md 경로 |
|---------|---------------|
| game-programmer | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/game-programmer/MEMORY.md` |
| game-design-lead | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/game-design-lead/MEMORY.md` |
| qa-tester | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/qa-tester/MEMORY.md` |
| asset-prompt-crafter | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/asset-prompt-crafter/MEMORY.md` |
| project-orchestrator | `d:/Dmain/dev/Portfolio/Hexiege/Hexiege/.claude/agent-memory/project-orchestrator/MEMORY.md` |

---

## 주요 문서 경로

| 문서 | 경로 |
|------|------|
| 프로젝트 현황 | `Assets/_Project/Docs/PROJECT_STATUS.md` |
| 로드맵 | `Assets/_Project/Docs/ROADMAP.md` |
| 기획서 | `Assets/_Project/Docs/GameDesignDocument.md` |
| 기술설계 | `Assets/_Project/Docs/TechnicalDesignDocument.md` |
| 작업 사이클 규칙 | `Assets/_Project/Docs/_Tasks/README.md` |

---

## 좌표계 핵심
- XZ 평면 (Y=0 바닥, Y=높이)
- HexMetrics.HexToWorld() → Vector3(x, 0f, z)
- ViewConverter: Red팀 좌표 반전 `2*center - pos` (X, Z만 반전, Y 보존)
- ViewConverter.Setup()은 LoadMap() 내 렌더링 전에 호출 (ApplyConfig() 직후)

---

## 공통 중요 교훈
- Y Scale 0.4 on tile prefabs is INTENTIONAL (등각 효과) — 절대 변경 금지
- Inspector 값이 코드 기본값보다 우선 (ScriptableObject overrides code)
- QA 에이전트 제안 → 반드시 컴파일 확인 후 적용
- Scene NetworkObjects → Despawn/Respawn 시 리셋 → GameBootstrapper flag 사용
- TeamAssigner 삭제됨 (2026-03-20) — NetworkGameFlow.WaitForTeamAndSendReady()에서 팀 직접 할당
