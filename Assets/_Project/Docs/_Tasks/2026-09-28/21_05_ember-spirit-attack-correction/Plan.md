# EmberSpirit 공격 연결 교정 계획

EmberSpirit의 공격 동작을 화면과 서버가 같은 생산 클립에서 읽도록 고정하고, 나중에 연결이 어긋나면 빌드 전에 드러나게 한다. 현재 타격 표식과 설정의 1.00초는 서로 일치하므로 이번 계획은 이를 임의로 조정하지 않는다. 실제 수정은 메인 검토와 구현 담당 단계에서 관측된 불일치에 한정한다.

**기존 로직 제거:** 없음. 기존 서버 피해 writer, Client 표현, resolver의 `Supported` 상태 및 다른 유닛 폴백 경로를 유지한다.

**계획 상태:** Research/Plan 작성만 완료. 구현·Unity 검증·빌드·새 EmberSpirit 실기 전이며 PASS/Complete 아님.

## 수정 항목과 규칙 근거

1. **실제 Game 씬의 EmberSpirit 공격 클립 명시 연결.** 후보는 [Game.unity](../../../../Scenes/Game.unity)의 실제 `UnitFactory`(`ffbee3fe087401948ae349db70b1d746`) `_spiritPrefabs/type: 11` 행 **한 곳**이다. Blue/Red GUID를 그대로 두고 `attackTimelineClip`에 `EmberSpirit_Attack.anim` GUID `335daaffd649791409558085159725aa`를 넣는 것이 현재 관측에 맞는 최소 수정이다. 단, 구현 시 실제 씬/프리팹/Controller/Attack motion GUID가 이번 조사와 같을 때만 적용하고 다르면 중지해 원인을 재조사한다. 명시 클립이 있으면 기존 [UnitFactory.cs](../../../../Scripts/Infrastructure/Factories/UnitFactory.cs)의 두 생성 경로가 같은 클립으로 주기와 marker를 읽으므로 공통 런타임 코드 수정은 현재 근거상 필요하지 않다. 근거: [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 17·18 및 `U-ATK-TIMELINE`, [동기화 계약](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) `NET-PRESENT-002`.
2. **EmberSpirit 전용 생산 연결 회귀 gate.** 후보는 [RunUnitActionSelfValidation.cs](../../../../Scripts/Editor/Combat/RunUnitActionSelfValidation.cs)이다. 기존 메뉴의 검사 흐름에 실제 Game 씬 `UnitFactory` 행 하나, 양 팀 프리팹/Controller, `Base Layer/Attack` motion speed 1, 정확한 클립 GUID, 30fps·길이 `2.6666667s`, `OnAttackHit` 한 개 `1.00s`, 설정 type 11 한 행의 `hitFrameTimes=[1.00]`, 두 생성 경로의 동일 선택 seam을 함께 검사하도록 한다. 명시 클립과 실제 motion이 다르면 조용히 이름 기반으로 대체하지 않고 실패시킨다. EmberSpirit은 현재 별도 `Attack2/Attack3`가 확인되지 않았으므로 Dust/Rabbit의 alternate-first fixture를 그대로 요구하지 않는다. 필요하면 다른 클립이 먼저 열거되는 fixture를 통해 명시 참조 우선만 검증한다. 직렬화 enum 비교는 `enumValueIndex`가 아니라 `(int)UnitType.EmberSpirit`과 `intValue`를 사용한다. 근거: 유닛 규칙 17·19·22, 동기화 계약 `NET-PRESENT-001`·`NET-PRESENT-002`·`NET-PRESENT-003`, Quake 선례의 fail-closed production gate.
3. **수치 변경은 조건부로 별도 판단.** [UnitStatsConfig.asset](../../../../Resources/Config/UnitStatsConfig.asset)의 `hitFrameTimes=[1.00]`과 Attack marker `1.00s`는 현 상태에서 변경 후보가 아니다. 설정 `attackCooldown: 2.2`와 클립 길이 `2.6666667s`는 불일치 관측으로 gate/보고에 명시한다. 클립이 선택되면 현재 `UnitFactory`가 실효 쿨다운을 클립 길이로 덮는다. 폴백 값 2.2를 정렬할지, 의도한 공격 주기를 바꿀지는 추정하지 않는다. 사용자나 설계 근거로 의도를 확정한 뒤에만 해당 에셋·gate 기대값 변경을 별도 결정한다. 근거: 유닛 규칙 17·18·36 및 `U-ATK-TIMELINE`, 동기화 계약 `NET-PRESENT-002`.

## 빌드 전 게이트와 인계

- 구현 담당이 위 연결과 영구 검사를 적용한 뒤 Runtime/Editor 컴파일 오류가 없는지 확인한다. 실제 씬의 **정확한** UnitFactory 등록과 양 팀 GUID, Controller/Attack motion/marker/config가 한 검증에서 수렴해야 한다. resolver의 `Supported / MeleeContact / Impact 1 / secondary false`와 다른 유닛의 기존 회귀도 유지한다.
- Unity 메뉴 `Hexiege/Combat/Run Unit Action Self Validation`과 `Hexiege/Combat/Diagnostics/Self Validate Unit Root Pose Cross Audit`의 **새 실행 결과 모두 PASS**를 확인한다. 검사 실패나 오류가 있으면 빌드를 시작하지 않는다. 문서 작성 단계에서는 어느 메뉴도 실행하지 않았다.
- 두 메뉴 PASS 후 사용자 승인된 범위에서 Android 빌드를 **시작**한다. 빌드 완료 확인은 사용자 담당이다. 메뉴 PASS/빌드 시작만으로 EmberSpirit 실기 PASS를 선언하지 않는다.
- 사용자 새 빌드에서 실제 EmberSpirit 생산, Blue/Red 공격 반복, 화면 접촉과 피해 표시의 체감 시각, 타겟 사망·전환, Editor Host/Android Client 결과·필수 표현을 확인해야 focused 결론을 낼 수 있다. 수치를 바꾸려면 사용자 관찰 또는 계측 근거를 먼저 남긴다.

## 범위와 위험

- 현재 **필요성이 확인된 구현 후보는 Game 씬의 명시 참조와 영구 검증 코드**다. 프리팹·Controller·Attack clip·marker·resolver·공통 피해/표현 코드의 변경은 조사상 요구되지 않는다. 직렬화 설정 쿨다운 차이는 미해결 관측이며 근거 없이 수정하지 않는다.
- Game 씬에는 `type: 11`이 여러 컴포넌트에 있다. 실제 `UnitFactory._spiritPrefabs`와 양 팀 프리팹 GUID를 동시에 확인하지 않고 숫자만 검색해 바꾸면 오연결된다. 잘못된 명시 참조는 서버·Client 양쪽의 주기와 marker를 함께 오염시킬 수 있다.
- 기존 `Supported` 분류를 새 승격으로 취급하지 않는다. 정적 gate는 생산 연결 이탈을 잡는 수단이지 실기 결과나 전체 migration 완료의 대체가 아니다. 25종 전체, 역할교대, Legacy rollback, 타격 시각 재튜닝은 이번 focused 작업에서 제외한다.
- 사용자 지시상 Testcase/Log/현황 문서/메모리 및 코드·에셋 파일은 이 document-manager 단계에서 생성·수정하지 않는다. 문서 정합성 검사와 Unity 자동 검증·빌드도 이 단계에서 실행하지 않는다.

## 완료 결과 — 2026-09-28 focused PASS/CLOSED

위 계획 상태와 마지막 범위 문장은 **초기 Research/Plan 작성 단계의 이력**이다. 후속 구현에서는 실제 Game 씬 type 11 `attackTimelineClip`을 GUID `335daaffd649791409558085159725aa`로 고정하고 EmberSpirit production gate를 영구 Unit Action self-validation에 추가했다. 기존 marker·config 1.00초, 폴백 쿨다운 2.2초, 클립 길이 2.6666667초는 유지한다. Unity 두 메뉴의 21:19~21:20 PASS·Console error 0 뒤 Build And Run이 시작됐고, 사용자가 빌드 완료와 실기를 보고했다.

동일 세션 `5500ceab6c19238787382bbd8fb15cf2e5f7f006c7400747a26329a8482dbbbf`의 Editor Host/Android Client 두 경기에서 EmberSpirit 18+34=52기, 대조군 LittleKnight 46+18=64기가 생산됐다. 경기 A Host 299 결과/실패 0 ↔ Client 299 수락/거부 0, 양 peer 필수 표현 264/264·실패 0; 경기 B는 231/0 ↔ 231/0, 양 peer 196/196·실패 0이다. Client EmberSpirit 데이터 재생성·UnitView 초기화 각 52건, 두 경기 양 peer local ROOT PASS/errors·drop 0이다. Client GameLog ERROR/FATAL 0, WARN 188건은 일시적 spawn-init 및 종료 disconnect 1건으로 별도 기록한다. 원문은 `_Logs/_editor/2026-09-28/RuntimeLog.txt`와 `_Logs/2026-09-28/21_42_logcat/RuntimeLog_device.txt`다.

사용자의 “육안으로 괜찮았어” 확인을 합쳐 **EmberSpirit 명시 연결 focused PASS/CLOSED**로 판정한다. 집계는 LittleKnight 포함 경기 전체이며 정확한 animation→피해 subframe offset은 미계측이다. 공식 cross-audit, 25종 전체·역할교대·Legacy rollback은 미실행/미완이고 이번 판정에 포함하지 않는다. 새 Testcase는 요청받지 않아 만들지 않았다. 다음 유닛은 사용자가 선택하기 전까지 확정하지 않는다.
