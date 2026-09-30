# EmberSpirit 공격 연결 조사

EmberSpirit이 화면에서 재생하는 공격 동작과 서버가 공격 시간을 읽는 동작이 같은 클립을 사용하도록 확인하는 작업이다. 현재 저장된 타격 표식과 설정은 모두 1.00초이므로 이 문서는 새 타격 시각을 정하지 않는다. 실제 생산 연결에서 확인된 빈틈과, 구현 전에 보존해야 할 경계를 기록한다.

## 조사 범위와 판정

- 2026-09-28 21:05 KST 작업 폴더. 이번 document-manager 단계는 Research/Plan만 작성한다. 코드·에셋 변경, Unity 메뉴·빌드·실기 실행은 하지 않았다.
- [게임 규칙 인덱스](../../../GameSystemRules.md), [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md), [전투 동기화 계약](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), [공격 에셋 매트릭스](../../../Assets/UnitCombatAssetMatrix.md)를 기준으로 실제 직렬화 파일과 생성 경로를 대조했다. 매트릭스의 EmberSpirit 행은 `MeleeContact / Single / TimerImpact / 1 @ 1.00s / 설정 동일 / MigrationRequired`; resolver는 이미 `Supported`다.
- **정적 결론:** marker와 `hitFrameTimes` 사이의 1.00초 불일치는 발견되지 않았다. 다만 실제 Game 씬 `UnitFactory._spiritPrefabs/type: 11`에 `attackTimelineClip` 명시 참조가 없고, EmberSpirit의 정확한 생산 연결을 검사하는 영구 gate도 없다. 현재 선택 코드상 controller 클립 열거 순서에 의존한다. 실제 경기에서 잘못 선택됐다는 증거는 아니다.
- **상태:** 조사·계획 단계 / 구현 전 / 새 Unity 검증·빌드·EmberSpirit 실기 미실행. PASS 또는 Complete가 아니다.

## 실제 production 연결에서 확인한 사실

| 경계 | 디스크에서 직접 확인한 내용 |
|---|---|
| 타입·생산 등록 | `UnitType.EmberSpirit = 11`. [Game.unity](../../../../Scenes/Game.unity)의 실제 `UnitFactory`(script GUID `ffbee3fe087401948ae349db70b1d746`) `_spiritPrefabs/type: 11`은 Blue GUID `65437a9ab49692a44b94f8c5d97a93b4`, Red GUID `aaff65119146e2248a29f23c479cd35f`를 가리킨다. 두 GUID는 각각 [Blue](../../../../Prefabs/Units/Spirit/Unit_EmberSpirit_Blue.prefab)·[Red](../../../../Prefabs/Units/Spirit/Unit_EmberSpirit_Red.prefab) `.meta`와 일치한다. 그 행에는 `attackTimelineClip`이 없다. 같은 씬에는 다른 컴포넌트에도 `type: 11`이 있으므로 숫자만으로 수정 대상을 고르면 안 된다. |
| 프리팹→Controller→Attack motion | 양 팀 프리팹의 Animator Controller GUID는 `c604ed991e03ad24a90acbf0fb938b9d`로 같다. [EmberSpirit.controller](../../../../Animations/Units/EmberSpirit/EmberSpirit.controller)의 `Attack` state는 speed 1, speed parameter 비활성, motion GUID `335daaffd649791409558085159725aa`다. 이 GUID는 [EmberSpirit_Attack.anim](../../../../Animations/Units/EmberSpirit/EmberSpirit_Attack.anim) `.meta`와 일치한다. 양 팀 Animator root motion은 꺼져 있다. YAML 연결 확인이며 Unity 런타임 재생 확인은 아니다. |
| 클립과 설정 | Attack clip은 30fps, 종료 `2.6666667s`, `OnAttackHit` 이벤트 하나 `1.00s`다. [UnitStatsConfig.asset](../../../../Resources/Config/UnitStatsConfig.asset)의 `unitType: 11`은 `attackCooldown: 2.2`, `hitFrameTimes: [1]`이다. 따라서 **타격 offset은 일치하지만 설정 주기 2.2초와 클립 길이 2.6666667초는 다르다.** |
| 실제 생성 코드 | [UnitFactory.cs](../../../../Scripts/Infrastructure/Factories/UnitFactory.cs)는 서버/싱글과 Client 양쪽에서 `GetAttackTimelineClip` 결과로 쿨다운 길이와 marker 시간을 읽는다. 명시 참조가 없으면 controller의 `animationClips` 중 이름에 `Attack`이 포함된 첫 클립을 선택한다. 클립 길이가 양수면 `UnitData.AttackCooldown`을 그 길이로 덮고, EmberSpirit marker가 있으면 `HitFrameTimes`도 클립 값으로 덮는다. 정적 코드상 정상 Attack이 선택될 때 실효 값은 2.6666667초/1.00초다. 어떤 클립이 실제 런타임 목록에서 먼저 오는지는 이번 조사에서 확인하지 않았다. |
| 지원 상태와 영구 gate | [UnitAttackShadowProfileResolver.cs](../../../../Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs)는 EmberSpirit을 `Supported / MeleeContact / Impact 1 / secondary false`로 반환한다. [RunUnitActionSelfValidation.cs](../../../../Scripts/Editor/Combat/RunUnitActionSelfValidation.cs)에는 Dust/Rabbit/Quake 등의 production timeline 검증이 있지만 EmberSpirit 전용 production clip/controller/prefab/config 검증은 없다. 메뉴 자체도 이번에 실행하지 않았다. |

## 선례와 위험 해석

- [Dust Task](../10_54_dustspirit-explicit-attack-clip-binding/Research.md)는 실제 Attack과 유사 이름 클립의 첫 선택 위험을 명시 연결로 제거했다. [Rabbit Task](../10_03_rabbittrickster-explicit-attack-clip-binding/Research.md)는 같은 선택 경계와, 사용자가 별도로 타격 시각을 지정했을 때에만 marker·설정·gate를 함께 바꾼 선례다. EmberSpirit에는 사용자 지정 새 시각이 없다.
- [Quake Task](../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Research.md) 및 `.claude/mistakes/unit-action-correction.md`의 2026-09-14 사건은 문서 수치나 resolver만 보지 말고 실제 `프리팹 → Controller → Attack state → marker → config`를 함께 검증하라고 한다. 반대로 지금 EmberSpirit은 marker/config offset 불일치가 관측되지 않아 Quake처럼 수치를 먼저 교정할 근거가 없다.
- 설정 쿨다운 2.2초와 Attack clip 길이 2.6666667초의 차이는 기록해야 하지만, 생성 경로가 클립 길이로 덮는다는 사실만 확인했다. 이 설정 폴백을 바꿀지, 게임플레이 주기를 재설계할지는 사용자·구현 담당 판단 전까지 미확정이다. **1.00초를 임의 이동하지 않는다.**
- 정적 연결 일치나 새 자동 gate의 PASS는 실제 화면 접촉 프레임, 서버 피해 발생 시각, Host/Client 반복 공격, 타겟 사망 후 전환의 실기 PASS가 아니다. 전체 25종 이관·역할교대·Legacy rollback도 이 Task의 범위가 아니다.

## 2026-09-28 구현·focused 실기 후속 결과

위의 「구현 전」 표기는 21:05 조사 당시 상태다. 이후 실제 Game 씬 UnitFactory의 type 11에 `EmberSpirit_Attack.anim` GUID `335daaffd649791409558085159725aa`를 명시 연결하고, `RunUnitActionSelfValidation`에 EmberSpirit production 연결 gate를 추가했다. `hitFrameTimes=[1.00]`·marker 1.00초·설정 폴백 쿨다운 2.2초는 변경하지 않았다. 런타임 생성 경로는 선택된 클립 길이 2.6666667초를 공격 주기로 사용한다. 이 수치 연결은 타격 프레임의 실측을 뜻하지 않는다.

- Unity `Run Unit Action Self Validation` 및 `Self Validate Unit Root Pose Cross Audit` 메뉴가 21:19~21:20 새 실행에서 모두 PASS, Console error 0이었다. Android Build And Run 시작 뒤 사용자가 빌드 완료·실제 테스트·로그 저장을 보고했다.
- 동일 `sharedSessionKey=5500ceab6c19238787382bbd8fb15cf2e5f7f006c7400747a26329a8482dbbbf`의 Editor Host 로그 `_Logs/_editor/2026-09-28/RuntimeLog.txt`와 Android Client 로그 `_Logs/2026-09-28/21_42_logcat/RuntimeLog_device.txt`에서 두 경기 종료 집계를 대조했다.

| 경기 | 실제 생산 | Host 결과 | Client 결과 | 양 peer 필수 표현 |
|---|---|---|---|---|
| A | EmberSpirit 18, LittleKnight 46 | `serverResults=299`, `resultFailures=0` | `clientResultAccepted=299`, `clientResultRejected=0` | `expectedVisual/presentationEmits=264/264`, failures 0 |
| B | EmberSpirit 34, LittleKnight 18 | `serverResults=231`, `resultFailures=0` | `clientResultAccepted=231`, `clientResultRejected=0` | `expectedVisual/presentationEmits=196/196`, failures 0 |

- EmberSpirit 합계 52기 생산, Client의 EmberSpirit 데이터 재생성·UnitView 초기화도 각 52건 확인했다. 위 결과·표현 수치는 LittleKnight를 포함한 **경기 전체**이며 EmberSpirit 단독 타격 수로 해석하지 않는다.
- 두 경기 모두 양쪽 local ROOT `summary-END`가 `PASS`, errors 0, logDropCount 0이다. 공식 peer 간 Root Pose CrossAudit 분석을 실행했다는 뜻은 아니다. Client 경기 구간 GameLog ERROR/FATAL은 0이나 WARN 188건(일시적 spawn-init 경고 및 종료 시 disconnect 1건)이 있어 「경고 0」으로 쓰지 않는다.
- 사용자가 EmberSpirit 화면 결과를 “육안으로 괜찮았어”라고 수용했다. 따라서 **EmberSpirit 명시 공격 연결의 focused PASS/CLOSED**다. 정확한 animation→권위 피해 subframe 차이, 25종 전체, Host/Client 역할교대, Legacy rollback은 검증 범위 밖이다.
