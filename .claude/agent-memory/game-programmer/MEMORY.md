# game-programmer 에이전트 메모리 — Hexiege

## 토픽 문서

- **런타임 로그 체계 (`GameLog` / `LogEvent` / sink / 이관 진행 상황 / 판정 선례표)**
  → `logging.md`
  규칙 단일 소스는 `Assets/_Project/Docs/LogRules.md` 이며, **충돌 시 언제나 그 문서가 옳다.**

## 항상 기억할 것

- **네임스페이스 함정:** `Hexiege.Application` 이 존재하므로 수식 없는 `Application` 은
  `UnityEngine.Application` 이 아니다. **완전 수식 필수** (CS0234 발생 이력 3건).
- **`NetworkBehaviour` 는 `Infrastructure` 레이어에만.** `Application → Netcode` 직접 참조 금지
  (`NetworkContext` 정적 홀더 경유).
- **CLAUDE.md 규칙 5 — git 명령 금지.** 사용자가 명시적으로 지시하지 않는 한
  `git show` 같은 읽기 전용 명령도 실행하지 않는다. 작업 전후 비교가 필요하면
  **현재 파일의 중괄호 개폐 균형 + 대상 문장만 치환했다는 사실**로 대체 검증한다.
- **CLAUDE.md 규칙 6 — 요청한 것만.** 작업 중 눈에 띈 개선점은 **고치지 말고 보고만** 한다.
