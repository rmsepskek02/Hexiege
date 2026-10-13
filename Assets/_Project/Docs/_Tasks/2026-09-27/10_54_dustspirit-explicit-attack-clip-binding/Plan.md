# DustSpirit 공격 클립 명시 연결 계획

DustSpirit의 두 공격 애니메이션 가운데 실제 `Attack` 상태가 쓰는 클립을 유닛 등록 정보에 지정한다. 그러면 서버·싱글·Client가 같은 클립에서 공격 주기와 타격 표식을 읽어, 클립 목록의 순서가 바뀌어도 결과가 흔들리지 않는다.

**상태:** 구현·자동 검증 PASS / 두 경기 Dust 49기 focused 로그 확인 / CONDITIONAL PASS·OPEN. 명시적 사용자 육안 최종 수용 전 Complete 아님. 아래 재테스트 결과가 최신이며 이전 실기 대기는 이력이다.

## 예정 구현 — 코드·씬 3파일

1. `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`: `UnitPrefabEntry`에 선택적 `AnimationClip attackTimelineClip`을 추가한다. production 단일 선택 seam에서 해당 항목의 명시 참조가 있으면 그 클립 하나를 선택하고, **쿨다운 길이와 `OnAttackHit` 추출 모두 같은 선택 결과**를 서버/싱글·Client 생성 경로에 전달한다. 명시 참조가 없는 기존 항목은 종전의 첫 `Attack` 선택/marker 부재 시 설정 폴백을 보존한다. FoxMagician의 marker로 피해 설정을 덮어쓰지 않는 예외도 보존한다. 근거: `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17·18, `U-ATK-TIMELINE`; `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002`.
2. `Assets/_Project/Scenes/Game.unity`: 실제 `UnitFactory._spiritPrefabs`의 **DustSpirit(type 13) 항목 하나**에 `DustSpirit_Attack.anim` GUID `961cafd21c1fa13429c74aa65a3a6345`를 명시한다. Blue/Red 프리팹과 controller, `Attack2` motion은 바꾸지 않는다. 근거: 규칙 17의 실제 Attack 클립 단일 출처 및 `NET-PRESENT-002`의 검증된 타임라인.
3. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`: 기존 self-validation에 작은 DustSpirit production gate를 추가한다. 실제 씬의 type 13 항목 명시 참조가 양 팀 프리팹 controller의 `Base Layer/Attack` motion과 일치하고, 같은 production 추출 경로에서 길이 3초·marker `[1.04]`인지 검사한다. 별도 fixture에서 `Attack2`를 먼저 열거해도 명시 `Attack`이 이기는지 확인한다. 실패는 fail-closed로 보고한다. 근거: `U-ATK-TIMELINE`, `NET-PRESENT-002`; `GameSystemRules_Units.md` 규칙 19·22의 결과 표현/애니 상태 회귀 방지.

## 보존·위험·인계

- 명시 참조가 잘못되거나 실제 Animator `Attack` motion과 다르면 조용한 폴백으로 숨기지 않고 검증 실패로 드러내야 한다. 미지정 항목은 기존 방식 그대로다.
- `Attack2`가 현재 실제 경기에서 선택됐다는 판정은 하지 않는다. 순서 재배치 fixture는 취약성의 회귀 검증이지 과거 오선택 실기 증거가 아니다.
- 피해 권위·피격 표현·NetworkVariable 애니메이션 상태(규칙 18·19·22), Fox 예외, 공격 클립/Controller/프리팹 에셋, projectile/tracer는 이번 구현에서 변경하지 않는다.
- 구현 후 Runtime/Editor 컴파일과 Unit Action self-validation 결과를 확인하고, 실기 판정 전에는 Task PASS/종료나 `Supported` 승격을 선언하지 않는다. Project status/roadmap/history·asset matrix·memory 갱신은 구현 결과 확인 뒤 별도 단계에서 판단한다.

## 구현 결과와 검증 — 2026-09-27

- 계획한 3파일을 수정했다. DustSpirit의 기존 Supported 분류는 그대로이며 명시 참조 하나에서 주기·marker를 추출한다. 프리팹·애니메이션·스탯 수치는 변경하지 않았다.
- Unity 재컴파일 후 `Run Unit Action Self Validation` PASS. Game 씬의 실제 등록과 양 팀 Attack motion, 설정 3초/[1.04], Attack2-first 실패 형태 및 명시 참조의 순서 독립성을 확인했다.
- `Self Validate Unit Root Pose Cross Audit` PASS. Unity Console error 0건. 기존 경고는 남아 있으며 경고 0을 주장하지 않는다.
- 새 기기 실기는 사용자 담당이다. 실제 생산한 DustSpirit의 연속 공격 간격·타격 순간·타겟 사망 후 이동/다음 공격을 확인하고 Host/Client 로그를 저장한다. 자동 PASS를 실기 완료로 확대하지 않는다.
- 코드 위임 에이전트가 생산 코드·씬 수정 뒤 사용량 제한으로 중단돼, 메인 세션이 나머지 검증 구현과 Unity 메뉴 실행을 이어받았다.

## 빌드 인계

두 메뉴 PASS 뒤 Unity `File → Build And Run`을 실행했다. 직후 화면에 `Checking prerequisites / Starting Android build`가 표시됐다. 사용자 지시에 따라 이후 빌드 진행·완료는 확인하지 않았다. 문서 정합성 검사는 문제 0건이다.

## 변경 파일

- 코드·씬: `UnitFactory.cs`, `RunUnitActionSelfValidation.cs`, `Assets/_Project/Scenes/Game.unity`.
- 상시 문서: `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`, `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`, `Assets/_Project/Docs/PROJECT_STATUS.md`, `Assets/_Project/Docs/ROADMAP.md`.
- 작업 문서: 이 폴더의 `Research.md`, `Plan.md` 신규 작성 및 갱신. Unity가 신규 작업 폴더·문서의 `.meta`를 생성했다.

## 2026-09-28 사용자 재테스트 결과 반영

원본은 `Assets/_Project/Docs/_Logs/2026-09-28/03_50_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`다. Editor Host / Android Client 동일 `sharedSessionKey=be8b171a5d2d58c113c4702b8ea5d8d054bf5855322f2ad45c097796bd94d4f5`의 **두 경기**를 runId/startedAt으로 분리했다. `03_41_logcat`은 겹치는 gameplay 캡처이므로 합산하지 않았다. Host attack runId는 `0e16018819d949288f14f378c63f6d83` / `bebba109c44540b7bf81caa3a49c4958`, Client는 `fc947d78f77d4d27bb90a2bcf0462472` / `74702837e689471c960b66edb37c34ab`이다.

**DustSpirit: focused runtime evidence 확인 / CONDITIONAL PASS·OPEN.** 실제 생산 Dust 49기/Fox 10기이며 이번 실제 생산 타입 중 Supported는 Dust뿐이다. 서버 결과 84+166=250과 Client 수락 250이 일치하고 각 peer 필수 표현은 83+153=236/236, 결과·표현 실패/중복/공간 mismatch/recovery 0이다. Host 이동 52,714+94,799=147,513 frame에서 gate/handoff/stationaryWalk/errors 0이며 이 이동 집계는 경기 전체이지 Dust 전용 집계는 아니다. 각 경기 양 peer local ROOT PASS지만 공식 CrossAudit 실행 결과는 아니다. 초기화 지연 59건은 59건 완료, retry failure 0이다. 게임 로그 ERROR/FATAL/실제 Exception 발생 없음(스택의 Exception 매개변수 문자열은 제외). Dust 공격 접촉·대상 전환 등에 대한 명시적 사용자 육안 수용은 없어 Complete/최종 PASS로 닫지 않는다. 이전 17_21 경기의 Dust 부재 판단은 당시 사실로 보존하며 이번 두 경기에서 해소됐다.

**Fox 표시 지연 focused 교정: 사용자 수용 / 현행 타이밍 유지.** 양 peer에서 received/emitted 61쌍씩이 모두 같은 frame, immediate=True, dispatch=text-played다. localTime 차이의 최대는 Host 7.097ms / Client 2.445ms, 평균은 1.07182ms / 1.71633ms다. 이는 큐 수신→텍스트 생성 호출 성공 구간이며 VFX 종료→동일 공격 피해 표시의 exact 측정이 아니다. VFX 실제 시작은 Host 14+65=79, Client 14+64=78, 실패 0이다. 미완료는 Host 1/Client 1이고 원인은 미확정이다. 종료 disconnect 경고를 그 원인으로 단정하지 않는다. 사용자는 약간 이른 느낌은 있지만 현재 Fox 타이밍을 명시적으로 수용했으며 재튜닝은 향후 발사체 구현 때로 미룬다. VFX 1.00초/피해 2.25초를 유지한다. 이번 표시 지연 focused 범위는 수용됐으나 완벽한 화면 일치, 권위 projectile/tracer, 전체 migration·25종·역할교대·rollback 완료로 확대하지 않는다. MigrationRequired / Unresolved / LegacyFallback 유지.

이번 변경은 승인된 결과 문서 반영만이며 코드·TC·새 Task·빌드 실행은 하지 않았다. 다음 유닛은 별도 조사·선정 대상으로 남기며 이 문서 갱신이 구현 착수 승인은 아니다.
