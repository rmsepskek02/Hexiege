# QA-Fix 반복 로그 — 공격 진입 멱등 인계와 대미지 위치 증거

## Round 1 — 2026-09-13 05:18

### [QA] 발견된 문제

- BUG-001: [MULTI-1, MULTI-2, MULTI-5] — 근거리 유닛이 공격 사거리 진입 경계에서 공격 화면 인계 확인을 받지 못해 공격하지 않는 현상
  - 심각도: Critical
  - 실기 증상: 사용자는 근거리 유닛들이 모두 공격하지 않았다고 확인했다. 대미지 텍스트 위치는 정상이라고 별도로 확인했다.
  - 같은 경기 식별값: `993718220d45194886d85f22bd4e8cade81e2992baf80a5152b7127a2fd69a3e`
  - 관련 파일: `Assets/_Project/Scripts/Application/Events/GameEvents.cs:1033`
  - 관련 파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs:5129`, `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs:5144`
  - 관련 파일: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs:2436`, `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs:2472`
  - 관련 파일: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs:645`, `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs:1317`
  - Host 실패 근거: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7331` — 공격 화면 인계 실패 87회, 보류 423프레임, 상세 64건, 초과 23건.
  - Host 최종 판정: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7336` — 이동 권위 관측 최종 `FAIL`.
  - 공격 종류 범위: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7342` — 실제 공격 수행 증거 4/25종.
  - 실패 타입 산출 방법: 같은 세션의 상세 실패 64줄에서 유닛 식별값을 추출한 뒤, 같은 Host 로그의 서버 생산 완료 줄에 기록된 종류와 대조했다. 대조 누락은 0건이다.
  - 실패 타입 집계: LittleKnight 14건, EmberSpirit 14건, TideSpirit 14건, DustSpirit 12건, FlameSpirit 6건, SpearMan 4건.
  - 대표 매핑 근거:
    - LittleKnight: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6688`, `:6722`
    - EmberSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6695`, `:6723`
    - TideSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6713`, `:6728`
    - DustSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6740`, `:6798`
    - FlameSpirit: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6815`, `:6835`
    - SpearMan: `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6964`, `:7043`
  - 성공 경로 분리: Pistoleer와 Assault의 공격 시작은 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:6806`, `:6849`에서 확인된다. StreamSpirit은 `:6903`에서 공격 준비 완료가 확인된다. SpearMan도 `:7003`에서 공격 시작 1건이 확인되므로, 실패 타입에 포함됐다는 사실을 해당 종류 전체의 상시 실패로 확대하지 않는다.
  - 원인 분석: 이동 후보 위치에서는 공격 가능 타겟을 확인하지만, 공격 진입 알림은 유닛 식별값만 전달해 후보 타겟을 버린다. 수신 측은 아직 이동 위치가 확정되기 전의 현재 위치로 사거리 내 타겟을 다시 검색한다. 작은 사거리의 근거리 유닛은 이 재검색에서 타겟을 얻지 못해 동기 인계 확인이 돌아오지 않고, 공격 진입이 보류된다.
  - 자동 검증 경계: 사전 self-validation PASS는 위 실제 멀티플레이 경계를 통과했다는 증거가 아니며, 이번 사용자 실기 FAIL을 무효화하지 않는다.

### 정상 축 분리

- 피해 결과 전달: Host `serverResults=2298`는 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7338`, Client `clientResultAccepted=2298`, 거부 0은 `Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt:132619`에서 일치한다.
- 필수 표시 수렴: Host는 `expectedVisual=2036`, `presentationEmits=2036`을 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7339`에, Client는 같은 수치를 `Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt:132630`에 기록했다.
- 표시 위치: Host는 표본 1,830건, 불일치 0건을 `Assets/_Project/Docs/_Logs/_editor/2026-09-13/RuntimeLog.txt:7341`에, Client는 표본 1,752건, 불일치 0건을 `Assets/_Project/Docs/_Logs/2026-09-13/05_19_logcat/RuntimeLog_device.txt:132648`에 기록했다. 사용자 육안도 대미지 텍스트 위치 정상으로 확인했다.
- 판정: 피해 결과의 전송과 대미지 표시 위치는 PASS 축이다. 공격하지 못한 근거리 유닛의 결과는 애초에 생성되지 않으므로 이 수렴 수치로 공격 진입을 PASS 처리할 수 없다.

### [DEV] 수정 내용

- BUG-001: 수정 미착수. 이 Round는 사용자 실기 FAIL과 저장 로그 및 현재 호출 경계의 원인만 기록했으며, 코드 수정·새 테스트·Unity 조작은 수행하지 않았다.

### Round 1 판정

- **FAIL / OPEN** — BUG-001 수정 및 사용자 멀티플레이 재시험 전까지 작업 미완료.
