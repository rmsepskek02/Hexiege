# game-programmer 에이전트 메모리 — 색인

각 주제의 상세는 아래 파일에. **여기에는 "어디를 보면 되는지"만 짧게 적는다.**

## 프로젝트 규칙 (항상 적용)

- 규칙 단일 소스는 항상 **문서 쪽**이다. 메모리와 문서가 충돌하면 **문서가 옳다.**
  - 로그 → `Assets/_Project/Docs/LogRules.md`
  - 게임 시스템 → `Assets/_Project/Docs/GameSystemRules.md` 및 하위 문서
  - 작업 절차 → `Assets/_Project/Docs/WORKFLOW.md` · 루트 `CLAUDE.md`
- **git 명령 금지**(CLAUDE.md 규칙 5). 읽기 전용 `git show` / `git diff` 로 작업 전후 비교하는 용도까지만.
- **요청 범위 밖은 손대지 않는다**(규칙 6). 눈에 띄어도 **고치지 말고 보고만** 한다.
- **판단이 갈리면 임의로 정하지 않는다**(규칙 12). 잠정 판정 + 근거 주석을 남기고 보고한다.
- 주석은 **유니티 초급 개발자가 읽어도 이해되게** 상세히(규칙 8). "왜"를 적는다.

## 레이어

`Domain → Application → Core → Infrastructure → Presentation → Bootstrap`
Assembly Definitions 없음 — **네임스페이스 규약으로만** 경계를 관리한다.

- `NetworkBehaviour` 는 **Infrastructure 에만.** `Application → Unity.Netcode` 직접 참조 금지
  (`NetworkContext` 정적 홀더를 경유).
- Domain 은 Core 를 참조하지 않는다. 필요하면 Domain 쪽 정적 홀더(`HexOrientationContext` 등).
- 조합 루트는 `GameBootstrapper`. 상세 → `architecture.md`

### ⚠️ 네임스페이스 함정 (CS0234 반복 발생)

`Hexiege.Application` 이 존재하므로 **수식 없는 `Application` 은 `UnityEngine.Application` 이 아니다.**
`UnityEngine.Application.dataPath` / `.persistentDataPath` / `.quitting` 은 **완전 수식 필수.**
`using Hexiege.Application;` 을 새로 추가할 때 그 파일에 수식 없는 `Application.` 이 있는지 반드시 확인할 것.

## 주제별 상세

| 파일 | 내용 |
|---|---|
| `logging.md` | `GameLog`/`ILogSink`/`LogEvent` 구조, 두 축 판정 선례표, `key=value` 표기 규약, 이관 배치 진행 상황, 검증 스크립트 |
| `architecture.md` | 레이어 경계·조합 루트·의존성 역전 패턴 |
| `network.md` · `network-infra.md` | NGO 구성, RPC 흐름, 매칭·Relay·Lobby |
| `hex-grid.md` | 큐브 좌표, PointyTop/FlatTop |
| `unit-building.md` · `unit-stats-and-combat.md` · `gameplay-systems.md` | 유닛·건물·전투·스킬 |
| `ui-system.md` | UI 구조, Canvas SortingOrder |
| `3d-transition.md` · `camera-and-view.md` · `rendering-and-animation.md` | 3D 전환 후 카메라·렌더링·Animator |
| `work-history.md` | 과거 작업 이력(대용량 — 필요할 때만 부분 조회) |
