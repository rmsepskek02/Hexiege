# MistShrine 멀티 검증용 진단 로그 — Plan

## 무엇을 왜 하나요

MistShrine은 싱글플레이로만 검증했고 멀티 4항목은 아직 확인하지 못했습니다. 1차 멀티 테스트는 전용 로그가 없어 판정할 수 없었습니다([Research](Research.md) 1절).
이번에는 **개발용 진단 로그만 추가**해 다시 테스트합니다. **게임 동작(시전·쿨다운·회복·동기화)은 한 줄도 바꾸지 않습니다.**

> **상태: 종결 (2026-11-25).** 멀티 검증 4항목 **PASS**. 테스트 중 찾은 「물안개 1개가 11회 회복」 결함은 이 Task에서 고치지 않고 다음 작업으로 넘겼다(사용자 결정). 결과·판정 → [6절](#6-multi-result).

- 기존 로직 제거·비활성화: **0건**.
- 근거 규칙: `GameSystemRules_Buildings.md` 「MistShrine 물안개 힐 시스템」 규칙 21·22·24, 원 task R3·R12([Plan](../../2026-08-10/14_12_mistshrine-heal-implementation/Plan.md) 10-3절). 로그 작성은 [LogRules](../../../LogRules.md), WORKFLOW [4] 「테스트 항목 진단 로그 규칙」.

## 1. 공통 형식

- `GameLog.Dev.Info`(이상은 `Dev.Warn`) · 메시지 `[MIST-DIAG] <Event>` · 첫 키 `Diag=MistShrine, DiagRev=1, Event=…, Role=Single|Host|Client`.
- 문자열 조립은 `[Conditional("UNITY_EDITOR")]`+`[Conditional("DEVELOPMENT_BUILD")]` 헬퍼 안, 람다는 블록 본문. 시간은 정수 ms.
- 매 틱·매 회복 줄 금지 — 상태가 바뀔 때와 물안개당·경기당 요약만. 한 줄 900바이트 이하. 존속 `개발`(제안).
- 대조용 순번: Host `CastNo`·`ToggleNo`, Client `RecvNo`·`AutoRecvNo`(신전별). RPC 인자는 늘리지 않는다.

## 2. 로그 지점 (경로 접두 `Assets/_Project/Scripts/`)

| 위치 | 역할 | Event · 핵심 키 | 찍는 조건 |
|---|---|---|---|
| `Presentation/UI/MistShrinePanelUI.cs` `HandleTap`·`HandleToggleAuto` | 누른 쪽 | `UseInput` `Action=Cast\|ToggleAuto\|IgnoredCooldown, Path=Rpc\|Direct` | 입력 1회 |
| `Infrastructure/Network/NetworkMistShrineController.cs` `ActivateMistServerRpc`·`ToggleAutoServerRpc` | Host | `CastRequest`·`ToggleRequest` `Sender, Result=Accepted\|Reject*` | 요청 1회 |
| `Application/UseCases/MistShrineUseCase.cs` `Activate` | Host/Single | `CastResult` `Origin=Manual\|Auto, Result=Ok\|Reject*, CastNo, CdMs` | 호출 1회 |
| 컨트롤러 `OnMistActivatedOnServer` / `MistActivatedClientRpc` | Host / Client | `ActivateSent` `CastNo` / `ActivateReceived` `RecvNo, PrevMirrorMs` | 송신·수신 1회 |
| UseCase `TickCooldowns` 만료 | Host·Client | `CooldownExpired` `CdMs, ElapsedMs` | 만료 1회 |
| 컨트롤러 `ToggleAutoServerRpc` / `AutoModeChangedClientRpc` | Host / Client | `AutoToggled` `On, ToggleNo` / `AutoModeReceived` `On, AutoRecvNo, PrevLocal` | 토글 1회 |
| UseCase `ApplyHealToUnitEntity`·`ApplyHealToBuildingEntity` | Host/Single | `HealSample` `Target, Base, Amplified, HpBefore, HpAfter` | 물안개당 대상별 첫 1회(상한 16) |
| UseCase `TickMists` | Host/Single | `DoubleTick` `Frame, Calls`(Warn) | 같은 프레임 2회 통과 첫 1회 |
| UseCase 물안개 종료(만료·파괴·재시전·정리·경기 종료) | Host/Single | `MistEnded` `Reason, Ticks, MinGapMs, MaxGapMs, SumDtMs, WallMs, BuildingHeals, DupTargetHeals` | 물안개 1회 |
| `Infrastructure/Network/NetworkHealthSync.cs` `SyncBuildingHeal` | Client | `BuildingHealReceived` `HealerId, ServerHp, HpAfter, Check` · 불일치·건물 없음은 Warn | 건물별 첫 1회 |
| 경기 종료(`OnGameEnd` 구독) | Host·Client | `SyncSummary` Host `BuildingHealSent` / Client `BuildingHealRecv, BuildingMismatch, AutoOn` | 경기 1회 |

## 3. 검증 항목 ↔ 로그 ↔ 판정

| 항목 | 로그 | 정상 판정 |
|---|---|---|
| ① Client 시전 서버 재검증·쿨다운 동일 감소 (규칙 21·22) | `UseInput`·`CastRequest`·`CastResult`·`ActivateSent`·`ActivateReceived`·`CooldownExpired` | Client 탭 수 = `Accepted` = `Ok` = `Sent` = `Received`, `CastNo↔RecvNo` 1:1. 양쪽 같은 회차 `ElapsedMs` 차 ≤ 약 150ms. `Reject*` 0건은 「미발생」(PASS 아님) |
| ② 건물 HP 회복 Client 동기화 (규칙 24) | `HealSample Target=Building`·`BuildingHealReceived`·`SyncSummary` | Host `BuildingHealSent` = Client `BuildingHealRecv`, `BuildingMismatch=0`, `Check=Match` |
| ③ Host 회복 2배 없음 (R3) | `DoubleTick`·`MistEnded`·`HealSample` | `DoubleTick` 0줄, `DupTargetHeals=0`, `SumDtMs ≈ WallMs`, `Gap ≈ 1000ms`, `Base=10`(2배면 20) |
| ④ 자동 토글 양쪽 브로드캐스트 (R12) | `AutoToggled`·`AutoModeReceived`·`CastResult Origin=Auto` | 신전별 `On` 순서열 Host = Client, `PrevLocal≠On`, 자동 시전 간격 ≈ `CdMs` |

## 4. 자가 검증

- 컴파일 성공, `grep -rn "Diag=MistShrine" Assets/_Project/Scripts`로 위 지점 전부 존재 확인.
- 판정 필드(`Remaining`·`TickAccum`·`TextAccum`·쿨다운 사전)와 RPC 시그니처 diff 0 — 진단 전용 카운터만 추가.
- 연막탄 미커밋 파일은 건드리지 않는다.

## 5. 테스트 절차 (멀티)

1. Editor=Host, 기기=Client로 접속, Client 초월·(가능하면) Host 초월.
2. Client 초월이 MistShrine을 짓고 손상된 아군 건물 근처에서 **수동 시전 1회**.
3. Client가 **자동 토글 켜기 → 자동 시전 1회 이상 확인 → 끄기**.
4. 가능하면 Host 초월도 수동 시전 1회.
5. 경기 종료 후 `Hexiege/Logs/Collect Device Logs and Create ZIP`, 이름 `mistshrine-multi`.

<a id="6-multi-result"></a>
## 6. 결과·판정·종결 (2026-11-25)

멀티 테스트 3회차 중 유효한 2회차(멀티 3판)로 4항목을 판정했다. 판정은 메인 세션이 `[MIST-DIAG]` 로그로 했다. 진단 커밋 `091d11b`.

### 6-1. 테스트 회차

원시 로그는 비공개 저장소 Hexiege-Logs의 Release `logs-2026-11`에 있다(저장소에 넣지 않는다 — [LogRules](../../../LogRules.md) §1.10).

| 회차 | 로그 이름 | 구성 | 판정에 사용 |
|---|---|---|---|
| 1 | `2026-11-25_00_10_mistshrine-multi_01` | — | **무효.** 사용자가 풀을 받지 않은 빌드로 테스트했다(사용자 확인: 「풀을 안받고 테스트했었네」). `[MIST-DIAG]` 0줄, 경기 종료 `SyncSummary` 없음. 연막탄 진단(DiagRev=2)은 있어 `569a58b` 시점 빌드로 추정 |
| 2 | `2026-11-25_00_41_mistshrine-multi1_01` | 멀티 2판, Android = Host(Blue), 에디터 = Client(Red), 양쪽 초월. SHA 일치 | 사용 |
| 3 | `2026-11-25_00_55_mistshrine-multi4_01` | 멀티 1판, 같은 구성. SHA 일치 | 사용 |

**계획과 다른 점:** 5절 절차는 Editor = Host · 기기 = Client였으나 실제 두 유효 회차는 Android = Host · 에디터 = Client였다. 에디터 Host 구성은 이번에 관측하지 않았다.

### 6-2. 판정 (회차 2 + 3)

| 항목 | 판정 | 근거 |
|---|---|---|
| ① Client 시전 서버 재검증 · 쿨다운 동일 감소 (Buildings 「MistShrine 물안개 힐 시스템」 규칙 21 · 22) | **PASS** | 회차 2: 입력 8건 → 서버 요청 전부 `Accepted`(Client 입력 4 = 서버 `Sender=1` 요청 4), `CastResult Ok` 12 = `ActivateSent` 12 = Client `ActivateReceived` 12, 건물별 `CastNo` ↔ `RecvNo` 1:1, `CooldownExpired` `ElapsedMs` 차 최대 약 90ms. 회차 3: Client 입력 8 = 서버 요청 8, 발동 7 = 수신 7 |
| ② 건물 HP 회복 Client 동기화 (같은 섹션 규칙 24) | **PASS** | 회차 2: 1판 Host `BuildingHealSent` 20 = Client `Recv` 20 = `MistEnded BuildingHeals` 합 20, 2판 62 = 62 = 62. 회차 3: 87 = 87 = 87. `BuildingMissing` 0, `BuildingMismatch` 0, `BuildingHealReceived Check` 전부 `Match` |
| ③ Host 회복량 2배 아님 — 이중 틱 없음 (원 task R3) | **PASS** | 물안개 19개 `DoubleTick` 경고 0, `DoubleTickFrames` 0, `DupTargetHeals` 0, 틱 간격 904~1072ms, `HealSample` 전부 `Dup=False` · `Check=Match`. 회복 증폭(재생의 뿌리, 받는 회복 1.5배): 회차 3에서 `AmpMulMilli=1500` · `Amplified=15` · `Gained=15` 2건 `Match` — 1.5배가 정확히 한 번 곱해짐 |
| ④ 자동 모드 토글 브로드캐스트 (원 task R12) | **PASS** | 켜기: Host `AutoToggled On` = Client `AutoModeReceived` 1:1(`PrevLocal=False`), `Origin=Auto` 재시전 간격 20.0~20.09s, 경기 종료 `AutoOn` 양쪽 동일. 끄기(회차 3): 자동 중 탭 → Host `AutoToggled On=False` → Client `AutoModeReceived On=False`(`PrevLocal=True`), 이후 자동 재시전 없음 |
| 회귀 스위트 | PASS (사용자 무언급 간주) | WORKFLOW 「[6] 사용자 테스트」 규칙 — 사용자가 이번 사이클에서 따로 언급하지 않았다. 결과 파일 없음(Console에만 나오고 RuntimeLog에 자체 검증 줄이 남지 않는다), 에이전트가 직접 확인한 것이 아니다 |

- **서버 거절(`Reject*`)은 0건 = 미발생.** 3절은 「`Reject*` 0건은 미발생(PASS 아님)」으로 정해 두었다. 정상 플레이에서는 쿨다운 중 탭을 패널이 먼저 막아(`MistShrinePanelUI.cs` `HandleTap` — 쿨다운 중이면 return) 요청이 서버까지 가지 않으므로 서버 거절은 정상 플레이로 생기지 않는 경로다. 사용자와 협의해 추가 테스트 없이 「정상 플레이로 발생하지 않는 경로」로 기록했다(사용자: 「쿨다운중 탭은 테스트해야하는 사항이야?」 → 생략 합의). ① PASS는 이 경로를 포함하지 않는다.
- **추가 관찰(정상):** 신전 파괴 시 물안개 즉시 종료(`Reason=ShrineDestroyed` 2건), 경기 종료 시 정리(`Reason=GameEnd` 1건).

### 6-3. 관찰 사항 (버그 아님 — 기록만)

- 신전이 파괴된 뒤에도 **Client의 쿨다운 미러 타이머는 계속 돌아** `CooldownExpired` 1줄을 남긴다(회차 2 신전 5: 39:01 파괴 → Client 39:04 만료 줄 / 회차 3 신전 10 · 11 · 8 동일). 서버는 재시전하지 않고 동기화 수치는 모두 일치한다 — 표시용 타이머만 남는 현상이다.

### 6-4. 발견된 결함 → 다음 작업 (이 Task에서 고치지 않음)

- **물안개 1개가 11회 회복했다** — 회차 3 신전 15(`PhaseMs=923`, `FirstTickMs=100`, `Ticks=11`, `SumDtMs=10080`). 관찰한 물안개 31개 중 1개.
- **원인(코드 근거 `Assets/_Project/Scripts/Application/UseCases/MistShrineUseCase.cs` `TickMists`, 2026-11-25 문서 갱신 때 486~531행 재확인):** 위상 정렬(Buildings 「MistShrine 물안개 힐 시스템」 규칙 8-1-a)로 첫 틱이 시작 직후(약 0.08s)에 오면 11번째 틱 예정 시각이 10.08s가 된다. 서버 처리 단위(약 0.1s)에서 그 프레임이 「틱 발화」와 「남은 시간 ≤ 0 소멸」을 함께 처리하는데, 발화 판정(1단계)이 소멸 판정(3단계)보다 먼저라 지속시간을 넘긴 틱이 한 번 더 들어간다 → 대상당 100이 아니라 110.
- **사용자 결정(원문):** 「원인 파악하고 기획의도한대로 초당 1회 10포 지속 총 10회 발생으로 알맞게 적용되도록 수정하는게 맞는거같다 우선 이 작업을 하기전에 진행된 사항들과 태스트 결과들을 문서갱신하고 메인에 푸시병합 후에 task 문서 작성해서 작업진행하자」.
- → 기획 의도 확정: 물안개 1회 = 1초마다 10 회복, 총 10회. [ROADMAP](../../../ROADMAP.md) 「기획·경제·콘텐츠·UI」에 다음 작업으로 등록했다. 수정은 새 Task 문서로 진행한다.

### 6-5. 진단 로그 존속

- `[MIST-DIAG]`는 **유지**한다(존속 `개발`). 1절의 「존속 `개발`(제안)」이 사용자 이전 결정 「그럼 남겨두고」(개발 진단 로그 유지)로 확정됐다.

### 6-6. 반영한 문서

- 원 task [Plan §10-3](../../2026-08-10/14_12_mistshrine-heal-implementation/Plan.md) 4항목에 결과를 덧붙였다(원문 유지).
- 상시 문서: PROJECT_STATUS 「MistShrine」 행, ROADMAP(멀티 미검증 행을 닫고 다음 작업 행 등록), WORK_HISTORY 2026-11-25 행, 「멀티 미검증」 상태 표기가 남아 있던 Buildings 「MistShrine 물안개 힐 시스템」 머리 안내 · Upgrade 관련 규칙 안내 · StatsReference · VFXSFXList.
- Buildings 「MistShrine 물안개 힐 시스템」 규칙 본문은 바꾸지 않았다 — 규칙 6(「지속 시간 동안 매초 회복」)이 이미 지속시간을 넘긴 회복을 허용하지 않으며, 횟수를 명시하는 조항을 더할지는 수정 Task에서 정한다.
- WORKFLOW [8] Testcase.md: 이 Task에는 Testcase.md가 없어 해당 없음(만들지 않았다).
