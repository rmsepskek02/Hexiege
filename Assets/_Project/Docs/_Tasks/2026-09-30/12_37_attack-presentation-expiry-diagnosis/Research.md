# 공격 결과 표현 만료와 렉 원인 분리 — Research

LionKnight와 LittleKnight가 함께 나온 한 경기에서 서버의 공격 결과는 모두 전달됐지만 Editor Client의 결과 표현 묶음 7개가 만료됐다. 사용자는 심한 렉을 느꼈으나 기존 로그에는 프레임 시간과 묶음의 발행·수신 시각이 없어, 렉이 만료를 일으켰는지조차 아직 알 수 없다. 아래 조사는 **구현 전 기록**으로 관찰된 사실과 계측의 질문을 구분한다. 사용자가 승인한 같은 작업의 문서 작성 → 계측 구현 → Unity 검증 → Android 빌드 시도 실적은 문서 끝에 별도로 기록한다.

## 근거와 현재 판정

- 이전 작업: `Assets/_Project/Docs/_Tasks/2026-09-30/01_21_lionknight-attack-timing-correction/{Research,Plan}.md`. 저장된 LionKnight Attack 표식·type 22 설정은 30fps `0:18 = 0.6초`, `1:13 = 1.4333333초`다. 그 교정의 화면 접촉 시각과 C3 통과는 이 수치만으로 증명되지 않는다.
- 같은 경기 키 `a2d9c80e143ab3ee03081f3925c936873e23978929e15528ec0b9b2a4c622225`, Android Host 로그 `Assets/_Project/Docs/_Logs/2026-09-30/11_37_logcat/RuntimeLog_device.txt`, Editor Client 로그 `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt`. 범위는 LionKnight/LittleKnight 2/25종이다.
- Host C2 결과 224·실패 0, C3 ready 224·실패 0·묶음 224/224 방출·필수 표현 199/199. Client C2 수락 224·거부 0, C3 ready 217·실패 7·묶음 224 중 217 방출·필수 표현 192/192는 **ready된 결과 범위만** 덮는다. 양쪽 local ROOT PASS는 공식 CrossAudit Analyze PASS가 아니다.
- Client 7건은 `coordinator-failure status=Expired`; 기록된 `observed-intended`는 약 0.527~1.192초로 현재 0.5초 허용 경계를 넘었다. 여기서 `intended`는 `ImpactServerTime + 0.10초`의 **표현 due 시각**이다. 발행→수신 전송 지연을 직접 뜻하지 않는다. 첫 실패의 `attackerInstance=1`은 LionKnight `unitId=2`와 연결되지만 나머지 6건의 유형은 입증되지 않았다.
- 사용자는 큰 육안 문제는 보지 못했고 심한 렉을 보고했다. 로그에 FPS·frame-time·발행/수신/판정 세 시점의 연결 자료가 없으므로 렉 원인, 렉↔만료 인과, 정확한 타격 접촉은 미확정이다. 이전 LionKnight 작업은 Client C3 실패 때문에 **FAIL/OPEN**으로 유지한다.

## 읽기 전용 코드 조사: 현재 시간 경로

`AuthoritativeAttackPresentationCoordinator.cs`는 완료 묶음을 보관하고 `Tick(now)`에서 `due = ImpactServerTime + 0.10`을 계산한다. `now < due`이면 기다리고, `now > due + 0.50`이면 `Expired`, 그 외에 필요한 schedule/묶음 조건이 맞으면 `Ready`로 판정한다. 묶음 안 결과가 모두 준비돼야 방출한다. `0.50`은 현재 코드의 만료 경계이며 이번 조사에서 조정할 값이 아니다.

`NetworkCombatController.cs`의 서버 발행 경로는 완성 묶음을 만든 후 서버 시간과 로컬 monotonic 시간을 읽어 Bridge에 넘기고 Reliable `AttackPresentationCompletedBundleClientRpc`를 보낸다. Host는 RPC 수신 경로를 건너뛰며, Client는 수신 시각의 추정 서버 시간과 로컬 monotonic 시간을 Bridge에 전달한다. `UnitAttackResultPresentationShadow.cs` Bridge는 수신/결과 관찰 때 서버↔로컬 clock anchor를 갱신하고, Presentation `Update`는 `Time.realtimeSinceStartupAsDouble`로 `TickCoordinator`를 호출한다. 따라서 Client의 판정 시각은 마지막 anchor와 로컬 시간 경과에서 추정된다. 다른 결과 관찰도 anchor를 바꿀 수 있으므로 원인 분석에는 **anchor 출처/갱신 시각**이 필요하다.

Host·Client의 로컬 monotonic 시각을 직접 빼서 네트워크 지연이라고 할 수 없다. 서버가 전송한 동기화 서버 시간과 Client의 서버 시간 추정치도 clock-offset 불확실성을 포함하므로, 동일 clock-domain 구간(각 peer의 로컬 발행·수신→판정)과 cross-peer 추정 구간을 별도 표기해야 한다. 현재 로그는 이 분해에 필요한 값을 제공하지 않는다.

## 검증 전 가설과 구분 신호

| 미확정 가설 | 후속 경기에서 필요한 구분 신호 |
|---|---|
| 묶음 구성/서버 발행이 due 이후에 늦음 | 각 exact result key의 `ImpactServerTime`, due, 서버 완성·발행 시각 비교. 발행 전 지연이면 Client 네트워크 원인으로 귀속하지 않음. |
| 발행 이후 전송·RPC 처리 지연 | 서버 발행 시간과 Client RPC 진입 시간을 서버 시간축의 **추정치/오차**로 비교하고, Client RPC 진입→Bridge 관찰은 동일 로컬 clock으로 비교. 통신과 Client 메인 스레드 큐 대기는 추가 자료 없이 분리하지 않음. |
| Client 메인 스레드 정지/긴 프레임 | Client 수신·Coordinator 판정 직전의 로컬 `Update` 간격, 최대 frame gap과 실패의 같은 시간창 겹침 확인. 단순 육안 렉 보고만으로 판정하지 않음. |
| 서버 시간 추정/anchor 편차 | 수신 때의 추정 서버 시간, anchor source/age, 판정 때 추정 서버 시간, 동일 로컬 경과와 서버 기록을 대조. 수신 시점부터 이미 만료였는지와 수신 후 추정 시간이 튀었는지 분리. |
| 0.5초 정책 경계가 관찰 지연을 만료로 처리 | 앞 네 구간을 측정한 뒤에만 정책의 적합성을 별도 판단. 기준 완화나 만료 재생으로 실패를 숨기지 않음. |

## 규칙과 범위

`GameSystemRules.md`의 유닛 규칙 인덱스를 따라 `GameSystemRules_Units.md` **규칙 17~19**를 확인했다. Attack clip 이벤트는 표현 타격점/순서의 근거이고, 피해는 서버 타이머가 적용하며, 확인된 결과의 표현은 exact key와 최대 1회 원칙을 지킨다. `GameSystemRules_UnitCombatSynchronization.md` **NET-TIME-002/003**은 동기 서버 시간에 기반한 표현 지연과 0.50초 age/catch-up 경계를, **NET-PRESENT-003**은 Schedule·Result·Marker 분리 및 완성 AoE 묶음과 exact-key 방출을 요구한다. 후속 계측도 이 계약의 관찰자일 뿐 애니메이션, 피해 writer, 타이머, 만료 경계나 재생 정책을 바꾸지 않는다. 계측 결과가 나오기 전에는 위 가설 중 어느 것도 원인으로 확정하지 않는다.

## 2026-09-30 구현 후 상태 — 원인 미확정

위 시간 경로·가설은 **구현 전 조사 이력**이다. 이후 원인 분리용 계측이 코드 5곳에 추가됐다. `NetworkCombatController.cs`는 완료 묶음 Reliable RPC에 진단용 서버 발행 시각과 서버 로컬 시각을 동봉하고 Client RPC 진입 시각을 수집한다. Application `UnitAttackResultPresentationShadow.cs` Bridge는 clock anchor의 출처·revision을 노출한다. `UnitAttackShadowObserver.cs`는 exact-key arrival 최대 512개와 최근 frame gap 128개를 보관하고, Client `Expired` 시 `coordinator-expiry-timing-A/B`로 제한된 상세 정보(최대 16건)를 기록하며 overflow를 집계한다. Presentation `UnitAttackResultPresentationShadow.cs`는 프레임 숫자만 전달하고, Editor `RunAuthoritativeAttackPresentationValidation.cs`에는 만료 경계 분류의 결정적 검증이 추가됐다. 코드상 기존 `0.10/0.50` 판정, 피해·공격·애니메이션 경로 및 Reliable 전송은 변경 대상이 아니다. 구현 파일별 상태는 같은 task의 Plan 끝에 적는다.

제공된 Unity 실행 결과는 2026-09-30 **13:02:10 Unit Action**, **13:02:53 ROOT**, **13:04:05 Authoritative Attack Presentation** self-validation 모두 PASS, Console 오류 0이다. `File > Build And Run` 클릭 후 **Detect Java Development Kit (JDK) / Checking Java Development Kit** 진행 화면까지 관찰됐고, 그 이후 빌드 완료 추적은 사용자 담당이다. 따라서 **Android 빌드 완료·설치·변경 후 실기**는 확인되지 않았다. 이전 경기의 Client C3 7 `Expired`와 LionKnight task **FAIL/OPEN**은 유지된다. 새 계측 로그가 아직 없으므로 지연 구간·렉 원인·만료와의 인과 또는 근본 수정 성공은 주장하지 않는다.

## 2026-09-30 새 실기 증거 — 무만료 관찰, 원인 미확정

위 구현 직후 상태 문단은 **13:46 실기 전 기록**으로 보존한다. 새 동일 경기 키 `a81345042552685a831405817424d1a4b7e2260e56708f4e6f8febb7c3aacb5b`는 **13:43:15~13:45:47 Editor Host / Android Client**다. 근거는 Host `Assets/_Project/Docs/_Logs/_editor/2026-09-30/RuntimeLog.txt`, Client `Assets/_Project/Docs/_Logs/2026-09-30/13_46_logcat/RuntimeLog_device.txt`의 같은 키 BEGIN/END 및 사용자 육안 보고다. Host 생산은 LionKnight 8기·LittleKnight 22기, **2/25종**이며 아래 수치는 경기 전체 집계다.

| 경계 | Editor Host | Android Client |
|---|---:|---:|
| C3 일정 / 결과 / ready / pending / 실패 | 226 / 216 / 216 / 0 / 0 | 226 / 216 / 216 / 0 / 0 |
| 완결 묶음 / 방출 | 216 / 216 | 216 / 216 |
| C2 결과 | 서버 216, 실패 0 | 수락 216, 거부 0 |
| 필수 표현 | 188 / 188 | 188 / 188 |
| 표현 공간 불일치 | 0 | 0 |
| local ROOT | PASS, 오류 0 | PASS, 오류 0 |

Client `expiry-diagnostic-summary`는 **recorded=216, overflow=0, pending=0**이다. 이번 세션에는 `Expired`가 없어 실패 시에만 출력되는 `coordinator-expiry-timing-A/B` 상세도 없다. Host MOVE는 64,671프레임, adapter failure·stationary Walk 0, 공간 전이 계획/commit 326/326이다. Client 이동 복제 276샘플·invalid 0, attack-entry transport gap/order violation 0이다. Host WARN 1건은 경기 종료 시 재접속 대기 로그이고 Android Unity ERROR/FATAL은 0으로 보고됐다. 사용자는 큰 시각적 문제를 보지 못했다고 했으나 이번 런의 과거와 같은 심한 렉 여부는 평가되지 않았다.

**판정: 이번 2/25 경기에서는 Client 만료 0건을 관찰했지만 task는 FAIL/OPEN 유지.** 이전 `a2d9c80e…c622225`의 Client `Expired` 7건은 기록으로 남는다. 그 뒤 추가된 것은 원인 분리용 진단뿐이고 이번에는 실패가 재현되지 않아 publish/receive/frame-gap/clock-skew 중 어느 경로였는지 판별할 A/B 상세가 없다. 무만료 한 경기와 육안 수용을 원인 규명·근본 수정·정확한 접촉 타이밍·공식 CrossAudit 또는 25종 migration PASS로 확대하지 않는다.

## 2026-09-30 판정 범위 분리

사용자 수용에 따라 최신 `a8134504…c3aacb5b` 경기의 LionKnight 두 타격 focused 실기는 **PASS/CLOSED**다(근거와 범위는 LionKnight task 최신 절). 이 판정은 이 **C3 만료 원인 진단 task의 OPEN**을 닫지 않는다. 첫 `a2d9…c622225`의 Client `Expired` 7건은 미해결이며, 이번에는 재현되지 않아 원인 구분용 실패 상세가 없다. 위 FAIL/OPEN 표기는 이 진단 과제와 이전 판정 이력으로 읽고, LionKnight focused 현행 판정에 적용하지 않는다.
