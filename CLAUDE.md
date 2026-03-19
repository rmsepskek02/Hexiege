# Hexiege 프로젝트 절대 규칙

이 파일의 규칙은 **예외 없이** 모든 상황에서 적용됩니다.
"간단하니까", "자연스러운 다음 단계니까" 등의 이유로 생략 불가.

---

## 1. 승인 없이 파일 수정/생성 절대 금지

- 사용자가 명시적으로 요청한 파일만 생성/수정
- 요청하지 않은 파일을 "도움이 될 것 같아서" 추가 생성 금지
- **예시 위반**: research.md만 요청했는데 plan.md까지 생성

## 2. 승인 후 즉시 구현 금지

사용자가 "진행해" 등으로 승인 → **반드시 먼저 질문**:
> "task 문서(research.md / plan.md) 작성을 진행할까요?"

질문 없이 바로 Edit/Write/Agent 호출 절대 금지.

## 3. 코드/설계 작업은 반드시 전문 에이전트에게 위임

직접 코드를 수정하거나 설계를 결정하지 않음:
- 코드 구현/버그 수정 → **game-programmer** 에이전트
- 기획/밸런스 결정 → **game-design-lead** 에이전트
- 구현 후 검증 → **qa-tester** 에이전트
- 복합 작업 조율 → **project-orchestrator** 에이전트

## 4. 테스트 완료 후 반드시 업데이트 확인

사용자가 테스트 완료를 알리면 → **반드시 먼저 질문**:
> "문서/메모리 업데이트를 진행할까요?"

사용자가 다음 작업으로 넘어가려 해도 이 확인을 먼저 함.

## 5. git 명령 절대 금지

`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등
**사용자가 명시적으로 직접 언급하지 않는 한 어떤 git 명령도 실행 불가**.

이유: 2026-03-03, git restore 무단 실행으로 커밋되지 않은 작업 전체 삭제 → 복구 불가.

## 6. 작업 범위 초과 금지

요청된 작업의 범위를 넘어서 추가 개선/리팩토링/기능 추가 금지.
요청한 것만 정확하게 수행.
추가 개선 및 리팩토링은 문서 작성 전 사용자에게 제안하는것 권장.

## 7. 완성도 우선 원칙

**작업량보다 완성됐을 때의 완성도가 항상 우선**한다.

- 기존 코드를 많이 바꾸더라도 더 효율적인 구조가 있다면 그 방법을 채택
- "기존 코드와 맞지 않아서", "작업량이 많아서" 등의 이유로 더 나은 설계를 포기 금지
- 단, 더 나은 방법을 제안할 때는 반드시 이유와 장단점을 명확히 설명하고 사용자 승인 후 진행
- 이 원칙은 UI, 아키텍처, 알고리즘 등 모든 개발 영역에 적용

## 8. 주석은 상세하게 작성
- 유니티 초급 개발자도 쉽게 이해할 수 있는 주석 작성.

## 9. 코드 작업 완료 후 TestCase 문서로 작성
- _Task 폴더 내 해당 작업 폴더에 research/plan.md 와 같이 testcase 문서도 작성

---

## 전체 작업 사이클 요약

```
[1] 사용자 요청 → 계획 설명 (파일 수정 없음)
[2] 승인 → "task 문서 작성할까요?" 질문 먼저
[3] research.md 작성 (요청 시에만)
[4] plan.md 작성 → 공유 → 승인 후에만 구현
[5] 에이전트 위임 → 구현
[6] 사용자 테스트
[7] "문서/메모리 업데이트를 진행할까요?" 반드시 확인  ← 사용자가 다음 작업으로 넘어가려 해도 이 확인 먼저
[8] plan.md 테스트 체크리스트 업데이트
[9] 사용자 MEMORY.md 업데이트 (C:\Users\rmsep\.claude\projects\...\memory\)
[10] PROJECT_STATUS.md 업데이트
[11] ROADMAP.md 업데이트
[12] 에이전트 메모리 업데이트 ← 자주 누락 주의
    - game-programmer: .claude/agent-memory/game-programmer/MEMORY.md
    - project-orchestrator: .claude/agent-memory/project-orchestrator/MEMORY.md
    - qa-tester: .claude/agent-memory/qa-tester/MEMORY.md
    - game-design-lead: .claude/agent-memory/game-design-lead/MEMORY.md
```

**[7]~[12] 중 하나라도 빠지면 사이클 미완료. 순서대로 모두 수행.**

상세 규칙: `Assets/_Project/Docs/_Tasks/README.md` 참조.
