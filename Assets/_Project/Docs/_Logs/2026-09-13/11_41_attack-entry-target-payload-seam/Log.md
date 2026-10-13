# QA-Fix 반복 로그 — 공격 진입 타겟 전달 경계 교정

## Round 1 — 2026-09-13 16:17

### [QA] 재검증 결과

- BUG-001: [MULTI-1, MULTI-2] — 공격 진입 시 확정 타겟 유실로 발생했던 근거리 공격 인계 실패
  - 비교 이전 세션: `993718220d45194886d85f22bd4e8cade81e2992baf80a5152b7127a2fd69a3e`
  - 최신 재검증 세션: `abe1fb66044f1aa5efbb99d4420fa901e48c927ebbf3c24ef30b72f181c1f5c1`
  - 사용자 시험 범위: 이전 시험과 동일한 유닛만 시험했다. 나머지 유닛은 의도적으로 시험하지 않았으므로 최신 `coveredUnitTypes=9/25`를 이번 교정의 실패나 미완료 사유로 판정하지 않는다.
  - 이전 실패: LittleKnight, EmberSpirit, TideSpirit, DustSpirit, FlameSpirit, SpearMan 6종에서 Host 공격 화면 인계 실패가 총 87건 발생했다.
  - 최신 성공: 같은 6종 모두 Host에서 승인된 공격 commit이 확인됐다.
    - LittleKnight: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8172`
    - EmberSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8176`
    - TideSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8186`
    - DustSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8431`
    - FlameSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8532`
    - SpearMan: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8598`
  - Host 인계 집계: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8831` — `combatPresentationHandoffFailures=0`, `heldFrames=431`, `stationaryWalkViolations=0`, `failureDetails=0`, `failureEvidenceOverflow=0`, `ignoredServerTargetEvents=0`, `deferredCombatTargetChanges=2`, `droppedLogs=0`.
  - 해석: `heldFrames=431`은 공격 표현 인계를 기다린 프레임의 누적값이며 failure가 아니다. `deferredCombatTargetChanges=2`도 Action 준비 전 공개를 막은 정상 보류이고, 무시된 서버 타겟 이벤트는 0건이다.
  - 판정: **PASS** — 동일 유닛 범위에서 BUG-001의 실패 서명이 `87 → 0`으로 해소됐다.

### 정상 축 회귀 확인

- [MULTI-3] 결과 전달: Host `serverResults=2703`, `resultFailures=0`은 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8838`, Client `clientResultAccepted=2703`, `clientResultRejected=0`은 `Assets/_Project/Docs/_Logs/2026-09-13/16_17_logcat/RuntimeLog_device.txt:91963`에서 확인된다.
- [MULTI-4] 필수 표시: Host와 Client 모두 `expectedVisual=2410`, `presentationEmits=2410`, `failures=0`이다. 근거는 Host `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8839`, Client `Assets/_Project/Docs/_Logs/2026-09-13/16_17_logcat/RuntimeLog_device.txt:91974`다.
- [MULTI-4] 표시 위치: Host `samples=2144`, `mismatches=0`은 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8841`, Client `samples=2036`, `mismatches=0`은 `Assets/_Project/Docs/_Logs/2026-09-13/16_17_logcat/RuntimeLog_device.txt:91992`에서 확인된다.
- Client 공격 표현: `Assets/_Project/Docs/_Logs/2026-09-13/16_17_logcat/RuntimeLog_device.txt:91939` — `clientAttackPresentationStarts=656`, `clientAttackEntryTransportGaps=0`, `clientAttackEntryOrderViolations=0`.
- Host 공격 전체 요약: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:8842` — `coveredUnitTypes=9/25`, `legacyBeforeAligned=0`, `correlationFailures=0`, `productionGateInvalid=0`, `targetMismatches=0`, `dropped=0`.
- 같은 최신 세션에서 GameLog ERROR/FATAL/처리되지 않은 예외는 확인되지 않았다.

### 사용자 육안 결과와 로그 판정의 경계

- 사용자는 Editor Host + Android Client 실기 완료와 이전 시험과 동일한 유닛 구성임을 확인했다.
- 사용자가 이번 실기의 모든 개별 현상을 육안 PASS라고 별도로 선언한 것은 아니다. 따라서 육안 결과를 과장하지 않고, 동일 범위의 저장 로그가 이전 실패 서명의 해소와 정상 축 보존을 입증한 것으로 기록한다.
- 전체 25종 공격 회귀는 별도 통합 회귀 범위이며 현재 BUG-001 교정의 완료 조건이 아니다.

### [DEV] 수정 내용

- `Assets/_Project/Scripts/Application/Events/GameEvents.cs` — 공격 진입 이벤트가 공격자와 확정 타겟 ID·종류를 함께 전달하도록 변경됐다.
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — 최초 공격 진입과 재진입 모두 이미 확정된 타겟을 이벤트에 전달하도록 변경됐다.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` — 공격 진입 수신 시 현재 위치 기반 타겟 재검색을 제거하고 전달된 타겟을 직접 사용하도록 변경됐다.
- 이 섹션은 현재 Task의 기존 구현 기록을 인용한 것이며, 이번 QA 문서 갱신에서 코드를 수정하지 않았다.

### Round 1 판정

- **PASS** — 이전과 동일한 유닛 범위에서 공격 진입 타겟 전달 교정이 확인됐고, 결과 전달과 대미지 표시 위치의 정상 축도 유지됐다.
- `coveredUnitTypes=9/25`는 사용자가 의도한 동일 범위 재시험의 결과이므로 현재 실패가 아니다. 나머지 종류는 별도 통합 회귀에서 다룬다.

