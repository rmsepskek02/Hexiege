# MistShrine 멀티 검증용 진단 로그 — Plan

## 무엇을 왜 하나요

MistShrine은 싱글플레이로만 검증했고 멀티 4항목은 아직 확인하지 못했습니다. 1차 멀티 테스트는 전용 로그가 없어 판정할 수 없었습니다([Research](Research.md) 1절).
이번에는 **개발용 진단 로그만 추가**해 다시 테스트합니다. **게임 동작(시전·쿨다운·회복·동기화)은 한 줄도 바꾸지 않습니다.**

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
