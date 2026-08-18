# Game Programmer Agent Memory

> 이 파일은 200줄 이내 핵심 요약만 유지한다. 상세 내용은 토픽 파일 참조.

---

## CRITICAL — GIT 명령 절대 금지
- **모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)
- 코드 상태 확인 필요 시 Read/Grep 도구만 사용

## CRITICAL — 레이어 제약 (상세: architecture.md)
- Domain: `using Hexiege.Core` 금지, UnityEngine 참조 금지 → 정적 홀더 패턴(HexOrientationContext 등)
- Application: Unity.Netcode 직접 참조 금지 → NetworkContext 정적 홀더
- NetworkBehaviour / Unity.Netcode: **Infrastructure 레이어 전용** (Presentation/Application 금지)
- Infrastructure→Presentation 직접 호출 금지 → GameEvents(Subject) 이벤트 경유
- GameBootstrapper = 유일한 의존성 조합 루트. Assembly Definition 없음 — 네임스페이스 규약만
- `Hexiege.Application`이 `UnityEngine.Application`을 가림 → `UnityEngine.Application.xxx` 명시 필요

## CRITICAL — NGO API 제약 (상세: network.md)
- ServerRpc/ClientRpc 메서드명은 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON. NetworkObject는 씬 루트에 생성
- RPC 파라미터: 직렬화 가능 타입만. NGO 2.9.x bool? nullable 비교 필수
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- **GO 파괴 전파**: 서버에서 `NetworkObject.Despawn(destroy:true)` 명시 호출. `Destroy(gameObject)`는 NGO 클라 전파 불보장

## CRITICAL — DontDestroyOnLoad (상세: architecture.md / ui-system.md)
- 루트 GameObject에만 작동. 자식 배치 시 씬 전환마다 재생성+즉시파괴 반복
- DontDestroyOnLoad 오브젝트는 생성 씬 하나에만 배치. SetActive(false)면 Awake 미호출→미등록(숨김은 CanvasGroup.alpha=0)

## CRITICAL — 런타임 로그는 RuntimeLogger 경유 (상세: logging.md)
- **raw `Debug.Log` 진단 로그 금지**(Claude가 콘솔 못 읽음). 규칙: `Docs/LogRules.md`. 유틸: `Infrastructure/Debug/RuntimeLogger.cs`(BeginSession/Log/EndSession, 에디터만 파일 저장) — **이 기반 유틸은 유지됨**.
- ⚠️ **로그 작업 착수 전 반드시 `Docs/LogRules.md`를 먼저 확인**(RuntimeLogger 파일 기록·raw Debug.Log 금지). 2026-08-05 스킬 타입 C 작업에서 이를 뒤늦게 준수해 진단 로그를 걷어냄.
- **Application 계층 로깅 어댑터(`IRuntimeLogSink`/`RuntimeLoggerSink`/`SetLogSink`)는 2026-08-05 정리로 삭제됨 — 상시 기능 아님.** Application에서 임시 진단 로깅이 다시 필요하면 그때 한시적으로 sink를 재도입하고, 작업 종료 후 반드시 제거(코드/문서에 상시 기능으로 남기지 말 것). Infra 직접 참조 금지 원칙(의존성 역전)은 재도입 시에도 유효.

---

## 최근 작업 (상세 전체는 work-history.md)

### 랠리포인트 조준 시 BlockingOverlay 잔존 버그 수정 (2026-08-08 ✅ 실기 테스트 PASS · 커밋 `9a19cd5`)
- **증상**: 배럭 팝업 → 랠리포인트 버튼 탭 시 팝업만 사라지고 **반투명 배경(공유 BlockingOverlay)이 잔존** → 맵을 탭하면 오버레이(onTap=`Close`)가 터치를 먼저 먹어 `Close()`→`OnBeforeClose()`에서 `IsSettingRallyPoint=false`+`HideAllRallyMarkers()` → "지정이 그냥 취소된 것처럼" 보임(EventSystem/InputHandler 실행 순서에 따라 설정 실패 또는 깃발 즉시 소멸, 두 경우 모두 사용자 체감 동일).
- **원인**: `ProductionPanelUI.OnRallyPointClick()`이 `_popup?.Hide()`만 호출. 오버레이는 `BuildingPanelBase.Show()`가 켠 것이고 **끄는 곳은 `Close()` 단 하나** → 팝업만 숨기는 경로는 오버레이를 그대로 남긴다.
- **수정(코드 1파일 2줄, 순수 추가)**: `Presentation/UI/ProductionPanelUI.cs` — ① `OnRallyPointClick()`에 `UIManager.Instance?.HideBlockingOverlay();` ② `Close()`를 거치지 않는 `CompleteRallyPointSetting()`에도 동일 호출(참조 카운터 반납). **②를 빠뜨리면 안 됨** — 지금까지는 잔존 버그가 대신 `Close()`를 태워 카운터를 우연히 맞춰주고 있었으므로, ①만 고치면 카운터가 반납되지 않아 다음 팝업의 오버레이가 어긋난다. `HideBlockingOverlay()`는 언더플로 가드(0 미만 방지)+멱등이라 이중 호출 안전.
- ⚠️ **교훈(재사용·2번째 발생)**: `BuildingPanelBase` 계열에서 **조준 모드 진입을 위해 `_popup.Hide()`만 하는 경로는 반드시 `HideBlockingOverlay()`를 짝으로 호출**해야 하고, **`Close()`를 우회해 종료되는 경로는 참조 카운터를 직접 반납**해야 한다. 선례 `BuildingSkillPanelUI.cs:321-328`(주석에 "랠리 패턴"이라 적혀 있었으나 정작 랠리 경로가 미적용이었음). 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5(BlockingOverlay 단일 소유+참조 카운터), 보조 `GameSystemRules_Buildings.md` 랠리포인트 규칙 2(설정 직후 3초 표시 — 이 버그로 위반 중이었고 수정으로 정상화).
- **범위 밖(미수정, 별도 작업 후보)**: `ProductionTicker` 단일 `_autoHideCoroutine`·싱글플레이 팀 필터 부재·재경기 중복 구독/마커 누수·랠리 좌표 검증 부재·멀티 롤백 경로 부재 → 상세는 [gameplay-systems.md](gameplay-systems.md) "랠리포인트 시스템 구조 맵".

### 건물 파괴 시 열린 패널/조준 UI 원복 (2026-08-08 ✅ 실기 테스트 PASS · 커밋 `8c7fa01`)
- **범위**: 건물 패널 4종(생산/건물액션/스킬/연구) 공통 갭 — 파괴된 건물의 패널·조준 UI가 화면에 남던 것을 자동으로 닫음. task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`.
- **구현(코드 1파일)**: `Presentation/UI/BuildingPanelBase.cs`만 수정(자식 4개 패널 무변경). `InitializeBase`에서 `GameEvents.OnBuildingDied` 구독 → 핸들러가 `e.Building!=null && _currentBuilding!=null && e.Building.Id==_currentBuilding.Id`이면 `Close()` 호출. `Close()`가 `OnBeforeClose()`를 경유하므로 `BuildingSkillPanelUI.OnBeforeClose`(조준 취소 `CancelAim`)·`ProductionPanelUI.OnBeforeClose`(랠리 마커 숨김)가 자동 연계 → **베이스에서 `Close()`만 불러도 4종 정리 완료**.
- **판정 기준 = `IsOpen` 아님, `_currentBuilding.Id` 매칭**: 스킬 조준 진입 시 `BuildingSkillPanelUI`가 `_popup.Hide()`를 불러 `IsOpen`(=`_popup.IsVisible`)이 조준 중 false가 됨 → `IsOpen`으로 걸면 조준 중 케이스를 놓침. 닫힌 패널은 `_currentBuilding==null`이라 매칭 자연 실패(오작동 없음).
- **구독 해제 — Plan 대비 변경(`.AddTo(this)`)**: Plan은 `IDisposable`+신설 `OnDestroy` Dispose였으나, ⚠️ **`ResearchPanelUI`가 이미 자체 `OnDestroy`를 선언**해 베이스에 `OnDestroy`를 신설하면 자식이 이를 **은닉(hide)**(자식이 `base.OnDestroy()`를 부르지 않으면 베이스 해제 로직 누락 → 구독 누수 회귀). 프로젝트 관용 패턴(`BuildingFactory`/`UnitFactory`/`HitPresentationQueue`)대로 **`.AddTo(this)`(UniRx)** 로 컴포넌트 수명에 묶어 회피.
- **멀티**: `NetworkCombatController.HandleBuildingDied`(L1027)가 클라에서 `OnBuildingDied` 재발행 → 싱글/호스트/순수 클라 전부 커버(별도 배선 불필요).
- **실기(PASS)**: 스킬 조준 중 시전 건물 파괴 시 조준 원 소멸·입력 잠금 잔존 없음, 생산 건물 파괴 시 랠리 마커 소멸, 연구소 파괴 시 연구 패널 닫힘(기존 연구 취소·환불과 충돌 없음).
- ⚠️ **교훈(재사용)**: MonoBehaviour 베이스에서 이벤트 구독을 해제할 때, 자식이 자체 `OnDestroy`를 가질 수 있으면 신설 `OnDestroy`+Dispose는 은닉으로 누락 위험 → **`.AddTo(this)`가 안전**. 규칙: `GameSystemRules_UI.md` 공통 UI 팝업 규칙 11(건물 팝업 대상 건물 파괴 시 자동 닫힘) 명문화.

### 스킬 시스템 Phase 2 — 타입 C(전역 상태변경: 버프/디버프/CC/힐) (2026-08-04 구현 → 2026-08-05 ✅ 실기+멀티(클라) 테스트 PASS)
- **범위**: 타입 C 실행기 + 상태효과 시스템 + 유효 스탯 접근자에 상태 배율 합성 + CanAttack 게이트 + 빙결 애니 정지 + 둔화 라이브 + 멀티 동기화 + 종족별 플레이스홀더 5슬롯. task Plan §6 Phase 2(하단 "완료 결과"), 규칙 `GameSystemRules_Skills.md` 13·Plan 9-1~9-5.
- **실기 결과(2026-08-05 PASS)**: 공격버프·빙결(이속0+공격봉쇄+만료 복귀)·둔화(0.5 라이브)·회복(HoT)·**무상태 회귀(기존 유닛/연구강화 무변경)** + 순수 클라이언트 재현까지 모두 확인.
- **신규 파일(5)**: Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}.cs`(순수 C#, 상태 목록→공격/이속 배율·CanAttack 순수 계산). Application `Services/StatusEffectSystem.cs`(유닛Id→UnitStatusState 딕셔너리, 서버 권위 Apply/Tick/accessor, 무상태=배율1·CanAttack true 회귀안전)·`Skill/GlobalStatusChangeExecutor.cs`(타입 C, 조준 없음, TargetsAllies→대상팀 결정→ctx.ApplyGlobalStatus).
- **StatusEffectKind enum(정수 고정)**: None0/MoveSpeedMul1/AttackPowerMul2/AttackDisabled3/Freeze4(=이속0+공격불가)/HealOverTime5. SkillDefinition._statusKind(int)와 1:1. **회복(HealOverTime)은 상태 시스템 미저장 → 기존 HoT(ApplyTimedEffect Heal) 재사용**(HP는 NetworkHealthSync가 이미 동기화).
- **상태 배율 합성(확정 9-1·9-4 곱연산)**: `UnitCombatUseCase`에 `_statusSystem` 주입(SetStatusEffectSystem). `EffectiveAttack`=기본×연구×상태(statusMul==1f면 baseAttack 그대로 반환=부동소수 무개입 회귀안전)·`GetUnitMoveSpeedMultiplier`=연구×상태(빙결 시 0)·**신규 게이트 `CanAttack(unit)`**(빙결/기절 false). **상태 없으면 전부 무변경**.
- **공격 게이트 삽입 2곳**: 싱글=`UnitCombatUseCase.TryAttack`(네트워크 가드 직후 `if(!CanAttack)return null`), 멀티=`NetworkCombatController.TickCombat`(`combat.CanAttack(unit)?TryFindTarget:null` — 데미지만 봉쇄, HasEnemyInRange 유지로 전투 자세는 남음).
- **9-5 이동 정지·둔화 라이브(실기 확정)**: UnitView 이동 배율이 **경로 발급 시 1회 캡처**라 배율을 바꿔도 즉시 반영 안 되던 것을 → **이동 코루틴이 매 프레임 유효 이동배율을 다시 읽어 즉시 반영**(경로 재캡처 불필요)으로 전환. **둔화(0.5 부분배율)도 라이브로 적용됨(실기 확인)**. 빙결=배율 0이므로 이동 코루틴이 매 프레임 0을 읽어 정지. **무상태면 배율>0이라 기존과 동일(무변경 보장)**. UnitView 이동 코루틴은 서버에서만 도므로 서버 권위. ⚠️ **미세 잔여**: 전투 종료 후 정렬 Lerp 구간은 여전히 캡처값 사용(라이브 아님) — 필요 시 후속.
- **빙결 애니메이션 정지(2026-08-05)**: 빙결 시 `Animator.speed=0`으로 걷기 프레임을 고정(제자리 걷기 방지). 클라 동기화용 `UnitAnimState.Frozen` + `OnUnitFreezeChanged` 이벤트로 순수 클라이언트에도 정지 프레임 재현.
- **멀티 동기화(NetworkUpgradeController.OnResearchCompleted 패턴 미러)**: `UnitCombatUseCase.OnGlobalStatusApplied` 이벤트(회복 제외 발화) → `NetworkSkillController`(서버)가 `EnsureStatusSubscription`(OnNetworkSpawn+ServerRpc 진입 멱등, 스폰레이스 대비)로 구독 → `StatusAppliedClientRpc((int)team,(int)kind,mag,dur)` 브로드캐스트 → 클라 `if(IsServer)return; combat.ApplySkillGlobalStatus(...,raiseNetworkEvent:false)`로 자기 유닛 재현. **회복은 HP 동기화로 충분해 미브로드캐스트(이중 힐 방지)**. 기능상 클라 상태는 현재 VFX 없어 무효과지만 프레임워크 완비.
- **상태 부여 경로**: 실행기→`ctx.ApplyGlobalStatus(team,kind,mag,dur)`→`SkillActivationUseCase.ApplyGlobalStatusBridge(raise:true)`→`UnitCombatUseCase.ApplySkillGlobalStatus`(대상팀 유닛 순회, HealOverTime→HoT/그외→_statusSystem.Apply). A·B 실행기가 ctx.ApplyInstantAreaDamage 부르는 것과 동일 분담(실행기 thin).
- **상태 틱(이중 틱 금지, 쿨다운 틱과 동일 가드)**: 싱글=GameBootstrapper.Update(`_statusEffectSystem.Tick`, `!IsNetworkMode||!IsNetworkServer`)·멀티서버=NetworkCombatController.TickCombat(TickCooldowns 옆)·멀티클라미러=Update.
- **수정 파일**: SkillExecutorRegistry(C 등록)·SkillActivationContext(globalStatus 델리게이트+ApplyGlobalStatus)·SkillActivationUseCase(ctx 3번째 델리게이트+ApplyGlobalStatusBridge)·UnitCombatUseCase(_statusSystem·이벤트·접근자 합성·CanAttack·ApplySkillGlobalStatus)·NetworkCombatController(게이트+틱)·NetworkSkillController(구독·브로드캐스트·클라재현)·IGameServices(GetStatusEffectSystem)·GameBootstrapper.cs(필드/getter/Update틱)·GameBootstrapper.Setup.cs(StatusEffectSystem 생성·주입)·UnitView(매 프레임 유효 이동배율 재조회+빙결 Animator.speed=0)·editor `SkillSetup_CreateDataAssets`(SkillSpec에 statusKind/magnitude/targetsAllies+SetBool).
- **UI 버튼 균일화(2026-08-05)**: 스킬 슬롯 CostContainer를 `SetActive(false)`(→행 높이 붕괴) 대신 **CanvasGroup alpha=0(HideChildKeepLayout)**로 숨겨 레이아웃 행 높이를 보존(버튼 크기 균일).
- **유지 중인 테스트 스캐폴딩(과대 표기 금지)**: 종족별 플레이스홀더 스킬 **5슬롯** = 1 폭탄(타입 A)/2 빙결(C-CC)/3 공격버프(C-buff)/4 둔화(C-CC)/5 회복(C-heal). 스킬 버튼은 임시 텍스트 라벨. **최종 스킬 기획·아이콘 확정 전까지 유지되는 테스트용**.
- **정리(cleanup) 완료(2026-08-05)**: 개발 중 넣었던 **진단 로그 코드를 LogRules 준수 위해 제거**(raw Debug.Log 금지·RuntimeLogger 파일 기록 원칙) → `IRuntimeLogSink`/`RuntimeLoggerSink`는 **삭제됨**(grep 0건, 상시 기능으로 기재 금지). 로그 파일 `Docs/_Logs/2026-08-04/16_49_skill-status-debug/RuntimeLog_host.txt`는 LogRules대로 보존. 좌표화 때 주석 비활성화했던 코드 3곳도 삭제 완료. **교훈: 로그 작업 착수 전 반드시 `Docs/LogRules.md`(RuntimeLogger 파일 기록·raw Debug.Log 금지)를 먼저 확인할 것.**
- **남은 것(별도 작업)**: ① ✅ 건물 파괴 시 열린 스킬 패널/조준 UI 원복 — **2026-08-08 구현 완료·실기 PASS**(아래 최근 작업 항목 참조) ② 구체 스킬 목록·수치·아이콘(기획) 보류 ③ 둔화 전투종료 정렬 Lerp 잔여(위 9-5).

### 스킬 지점 조준 좌표화 + 조준원 지면 데칼 렌더링 + 취소 버그·토스트 (2026-08-04) — ✅ 실기기 테스트 PASS (상세: skill-aim-coordinate.md)
- 조준 중심 **HexCoord(타일 스냅) → 연속 도메인 월드 Vector3**. **착탄 반경 판정은 원래 연속 원이라 무변경, 중심만 연속화.** 전 계층 시그니처 Vector3화: SkillAimController/BuildingSkillPanelUI/SkillActivationUseCase.Activate(Vector3?)/SkillActivationContext(AimWorld)/UnitCombatUseCase.ApplySkill*(Vector3 center)/INetworkSkillController·NetworkSkillController RPC(**NGO Vector3 기본 직렬화**, int q,r 폐지).
- **맵 경계 헬퍼 신규**: `HexMetrics.{ComputeMapWorldBounds,IsWithinMapBounds,ClampToMapBounds}(pt,w,h)` — 최외곽 타일 중심 극값+반칸 AABB(도메인 좌표). HexGrid(Domain)는 Vector3 불가 → Core 수학+클로저 주입(GameBootstrapper `_grid` 캡처=맵 재로드 대응). 서버 재검증 HasTile→맵경계 안 점.
- **조준원 렌더링**: coplanar z-fight 원인 → 신규 셰이더 `Hexiege/SkillAimOverlay`(`Assets/_Project/Shaders/`) = Transparent+ZWrite Off+**ZTest LEqual+Offset -1,-1**+Cull Off. 지형엔 안 파묻히고 유닛/건물(불투명)엔 가려짐. **ZTest Always 금지**. 머티리얼은 `SkillSetup_Scene.EnsureOverlayMaterial`가 생성·배선, `SkillAimReticle._overlayMaterial`(신규 필드) 런타임 자가배선. **씬 재셋업(Hexiege/Skill/2. Setup Scene) 필요**(머티리얼 생성·배선). 제거 3곳은 주석 비활성화→실기 통과 후 삭제.
- **취소 버그 근본 수정(실기 Android, 커밋 `2e88dfa` 1차→`4e5da5e` 근본)**: 취소 X 위에서 손을 떼도 발동·쿨다운. 원인=손 뗀 프레임에 `TryGetPointerScreenPos` 마우스 분기가 **합성 마우스 좌표(0,0)를 유효로 반환**해 캐시 폴백을 가로챔. 수정=**release 프레임엔 라이브 좌표를 읽지 않고 캐시된 마지막 드래그 좌표(`_lastDragScreenPos`)로만 취소/발동 판정.** 교훈: 터치 종료 프레임에 마우스 좌표 폴링은 (0,0) 합성값이 유효처럼 새어들 수 있으니 release 판정은 라이브 폴링 대신 마지막 유효 드래그 좌표 캐시로.
- **쿨다운 안내 토스트(커밋 `4e5da5e`)**: 쿨다운 중 탭 조용히 무시 → 기존 ToastUI(에셋 기반)에 `ToastKey.SkillOnCooldown`(`Application/Events/ToastKey.cs`) + `Resources/Config/ToastMessageConfig.asset` key:4 "스킬이 쿨다운 중입니다". `BuildingSkillPanelUI`에서 `ToastUI.Show(ToastKey.SkillOnCooldown)`.
- **컴파일/실기 상태**: 좌표화·렌더링·버그수정 전부 **사용자 실기기 테스트 PASS**(이전 "컴파일 미검증" 해소). 규칙 정정 `13bb7c1`·좌표화+렌더링 `9e79a2f`, 브랜치 `claude/building-skills-discussion-3v8d5k`.

### 스킬 건물 시스템 Phase 1 — ✅ 구현 완료·실기기 테스트 PASS (2026-07-31 구현 → 2026-08-04 조준 좌표화 사이클로 컴파일·씬 배선·실기 PASS)
- **범위**: 타입 A(즉발 범위 피해)·B(장판 DoT) + 프레임워크 골격. 타입 C(전역 상태변경)는 enum 멤버만 선언, 실행기 미구현(Phase 2). task `_Tasks/2026-07-28/12_14_skill-building-system-design/`(Plan §6 Phase 1), 규칙 `GameSystemRules_Skills.md`(1~26).
- **신규 파일(19)**: Domain `Skill/SkillMechanicType.cs`(A/B/C, C 선언만)·`Skill/SkillAimType.cs`(Instant/PointTarget). Application `Skill/SkillData.cs`(값객체, Sprite 허용)·`Skill/{ISkillExecutor,SkillActivationContext,InstantAreaDamageExecutor,AreaDotDamageExecutor,SkillExecutorRegistry}.cs`·`Interfaces/{ISkillDataProvider,INetworkSkillController}.cs`·`UseCases/SkillActivationUseCase.cs`. Infrastructure `Config/{SkillDefinition,SkillLoadoutConfig}.cs`(SO)·`Network/NetworkSkillController.cs`. Presentation `UI/{BuildingSkillPanelUI,SkillCooldownOverlay}.cs`·`Input/SkillAimController.cs`·`Effects/SkillAimReticle.cs`.
- **핵심 패턴**: ① 발동 단일 진입점 `SkillActivationUseCase.Activate(buildingId, slot, HexCoord? aim)`(플레이어/AI 공유, §3-8). 글로벌 쿨다운=UseCase `Dictionary<int,float>`(BuildingData 무변경). ② 건물 출처 피해=`UnitCombatUseCase.ApplySkillInstantAreaDamage`(방어 감쇄 O, **Tank 2배 미적용**, 기존 ComputeFinalDamage/ApplyFixedDamageToVictim 무변경 — 신규 `ApplySkillInstantDamageToVictim`)·`ApplySkillAreaDot`(무감쇄 DoT=`ApplyDamageOverTime(source:null)`). ③ **반경 수집은 SpecialAttackContext 헬퍼 재사용 안 함** → 건물엔 UnitData 공격자가 없고 맵 지점 중심은 뷰 좌표가 없어, 도메인월드(`_mapper.HexToWorld`)+TeamId 기준 전용 수집(`CollectEnemy{Units,Buildings}InRadiusDomain`) 신설(양팀 좌표계 정합). ④ 로드아웃=RaceId 키(`SkillLoadoutConfig:ISkillDataProvider`, MagicBuilding 공유라 enum 아닌 종족 분기). ⑤ RPC=`NetworkSkillController`(NetworkUpgradeController 미러, `GameServicesLocator.Current` 지연 해석, q/r int 전송). 발동 성공 시 `SkillActivatedClientRpc` 브로드캐스트로 클라 쿨다운 미러(`StartCooldownLocal`)만 세움(VFX 플레이스홀더). ⑥ 조준=`SkillAimController`(static `IsAiming` 가드 → CameraController.HandlePan/InputHandler.HandleClick 억제). 좌표=랠리와 동일(ScreenToXZPlane→ViewConverter.FromView→WorldToHex). ⑦ **BlockingOverlay 함정**: 스킬 패널 열면 UIManager BlockingOverlay(탭=Close)가 뜸 → 조준은 SkillAimController가 포인터 직접 읽어 오버레이가 release를 먹어 취소됨 → OnSkillPointerDown에서 `UIManager.Instance?.HideBlockingOverlay()` 필수.
- **쿨다운 틱(이중 틱 금지)**: 싱글=GameBootstrapper.Update, 멀티 서버=NetworkCombatController.TickCombat(TickResearch 옆), 멀티 순수클라=GameBootstrapper.Update의 `!IsNetworkServer` 분기(오버레이 미러 감소).
- **수정 파일(전부 additive)**: UnitCombatUseCase(스킬 피해 경로)·IGameServices(`GetSkillActivationUseCase`)·InputHandler(스킬 라우팅 분기+조준 가드+Initialize 인자 2개)·CameraController(`EdgeScroll(screenPos,dt,margin,speed)`+조준 팬 가드)·NetworkCombatController(쿨다운 틱)·GameBootstrapper.cs(필드/getter/Update)·GameBootstrapper.Setup.cs(생성·배선).
- **잔여(사용자 Unity 작업)**: SkillDefinition/SkillLoadoutConfig .asset 저작(×10 스케일!)·GameBootstrapper 신규 SerializeField 6종 배선(_skillLoadoutConfig/_buildingSkillPanelUI/_skillAimController/_networkSkillController)·씬에 NetworkSkillController NetworkObject·스킬 패널/조준점/취소X/쿨다운오버레이 프리팹. **컴파일 미검증(유니티 환경 없음)**. 개별 스킬 목록·수치·타입 C·정식 아트는 범위 밖.
- **버그수정(2026-07-31)**: `BuildingSkillPanelUI`가 `GameBootstrapper.Map.cs` LoadMap의 UIManager 등록 블록(`_uiManager.Register(...)`)에서 누락 → IGameUI(OnGameStarted/OnGameEnded→Close) 미호출로 게임 시작/종료 시 스킬 패널 미닫힘 + 조준 중 종료 시 IsAiming 락 잔존. `_buildingActionPanelUI` 옆에 `Register(_buildingSkillPanelUI)` 추가로 해결. **교훈: BuildingPanelBase(IGameUI) 상속 신규 패널은 반드시 LoadMap UIManager 등록 블록에 추가**(Register는 null-safe/중복방지). **참고: `_researchPanelUI`도 같은 블록에서 누락(선행 연구 task 산출물이라 이번 범위 밖·미수정)**. 쿨다운 상태는 `_skillActivation`이 CreateUseCases에서 매 LoadMap 새로 생성돼 잔여 없음(ClearAll 불필요).
- **에디터 셋업 스크립트 2종 신설(2026-07-31)**: `Assets/_Project/Scripts/Editor/SkillSetup_CreateDataAssets.cs`(메뉴 `Hexiege/Skill/1. Create Skill Data Assets` — 종족 3×[A폭격+B장판] 플레이스홀더 SkillDefinition 6개 + SkillLoadoutConfig 1개를 `Resources/Config/Skills/`에 멱등 생성, SerializedObject로 private 필드 세팅, ×10 수치)·`SkillSetup_Scene.cs`(메뉴 `Hexiege/Skill/2. Setup Scene` — BuildingSkillPanel(AnimatedPanel+헤더/닫기/철거+슬롯5×[아이콘+SkillCooldownOverlay])·SkillAimReticle(월드 Ring)·SkillAimController+하단 X·NetworkSkillController(NetworkObject) 멱등 생성 + GameBootstrapper 4슬롯 배선). 선례=`Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs`(SO+GameBootstrapper 배선)·`CreateProfileStatsFields.cs`(UI 빌드 헬퍼 FindOrCreateChild/EnsureText/EnsureButton/Connect). **주의: 기존 셋업 스크립트 관례는 `Assets/Editor/Setup/`+메뉴 `Hexiege/Setup/`인데 코디네이터 지시로 `Assets/_Project/Scripts/Editor/`+`Hexiege/Skill/`에 배치(둘 다 Editor 폴더라 컴파일 OK, 필요 시 이동)**. 실행·컴파일 미검증(유니티 없음). 쿨다운 Fill Image는 스프라이트 미지정 시 radial이 안 보임(로직은 배선됨) → 사용자 스프라이트 지정 필요(후속: 내장 UISprite/Knob 임시 자동 지정 보완). **부모 배치 교훈(버그수정 2026-07-31)**: Game.unity의 BuildingActionPanel/ProductionPopup/GameEndPanel은 각자 **자체 오버라이드 Canvas(SO 200, overrideSorting)** 를 가진 독립 패널이라 `[UI]` 루트(SO 0)의 형제로 배치됨(`GameSystemRules_CanvasSortingOrder.md`). 신규 패널을 붙일 부모는 `action.GetComponentInParent<Canvas>()`(자기 Canvas 반환→중첩)가 아니라 **`action.transform.parent`([UI] 루트)** 로 잡아 형제로 둘 것. 신규 패널 루트엔 오버라이드 Canvas(SO 200)+GraphicRaycaster 필수(규칙1). 셋업 스크립트 멱등화는 씬 전역 `FindFirstObjectByType<T>`(+이름 폴백)로 기존 인스턴스 찾아 `SetParent(정상부모,false)` 재부모화로 잘못된 잔재 자동 교정. **패널 신규 생성 교훈(2026-07-31 재수정)**: 셋업 스크립트로 UI 패널을 만들 땐 **처음부터 하드코딩으로 그리지 말고 기존 유사 패널을 `Object.Instantiate(srcGo, parent, false)` 복제** → 배경/앵커/AnimatedPanel/Canvas(SO200)/GraphicRaycaster/슬롯그리드 룩을 그대로 물려받는다. Instantiate가 계층 내부 참조(_popup/_headerText/_allSlotButtons 등)를 복제본 자식으로 자동 remap하므로, 복제본의 원본 컴포넌트에서 SerializedObject로 그 참조들을 **읽은 뒤** `DestroyImmediate(원본컴포넌트)`+`AddComponent<신규>()` 하고 읽어둔 참조로 재배선. Game.unity `BuildingActionPanel._allSlotButtons`=9개(0~4 스킬/5 철거=_demolishButton/6~8 예약). 스킬 슬롯엔 Icon Image+SkillCooldownOverlay만 추가, 예약 슬롯은 CanvasGroup alpha0로 숨김. BuildingSkillPanel=BuildingActionPanel 복제로 재작성 완료(하드코딩 조잡 박스 폐기). **스펙 정합(규칙 2·9·10·20)**: Game.unity 액션패널 슬롯 템플릿=`IconImage`+`CostContainer`(GoldIcon/CostText) 2자식 → 스킬 버튼은 아이콘만이므로 `CostContainer`/`Label` SetActive(false) 비활성, 기존 `IconImage` 재사용(런타임 SkillData.Icon로 켜짐). 쿨다운 오버레이=유휴 시 완전 숨김(`SkillCooldownOverlay.SetCooldown` remaining<=0→Hide, Awake에서 text=""·fillAmount=0로 잔여 "999" 표기 제거). 취소 X=`Sprites/UI/Buttons/ui_btn_cancel.png` 이미지 버튼(텍스트 라벨 없음)·평소 SetActive(false) → `SkillAimController.BeginAim`에서 켜고 `EndAim`(발동/취소/CancelAim)에서 끔(`SetCancelButtonVisible`). 플레이스홀더 스킬은 종족당 2개(타입 A·B)만, 타입 C는 Phase 2. **조준 조작=탭 기반 2단계로 재설계(규칙 17~20·20-1, 기존 hold-drag 폐기)**: `SkillAimController.BeginAim(…, HexCoord fallbackCoord, …)`—버튼 탭 시 즉시 조준모드 진입+조준원 기본표시(화면중앙 유효타일→없으면 시전건물타일)+취소버튼 표시, **버튼 자신의 release는 무시**(`_pendingButtonRelease`가 IsPointerPressed()로 그 gesture 소진까지 대기). 그 뒤 **새 화면 press→드래그(`_dragging`)로 조준 이동+엣지스크롤**, release로 발동, 하단 X 위 release로 취소. X 위 hover 시 취소버튼 `localScale` 확대 예고(규칙20-1, `ApplyCancelHover`). 이전 "hold-drag release 즉시발동"은 버튼이 화면하단이라 그 아래 타일 없어 아무것도 안 뜨는 버그였음. 쿨다운 오버레이가 "얇게" 뜨는 건 슬롯 LayoutGroup이 자식을 눌러서 → 오버레이에 `LayoutElement.ignoreLayout=true` + SetStretch로 버튼 전체 덮음. 취소버튼 스프라이트=`ui_btn_cancel.png`(AssetDatabase.LoadAssetAtPath<Sprite>), 셋업 재실행 시 다른 부모의 잔재 DestroyImmediate로 1개 유지+초기 SetActive(false). **UI 폴리시(2026-07-31)**: ① 취소버튼 hover 확대=하드설정→프레임독립 Lerp(`ApplyCancelHover`는 `_cancelTargetScale`만, `TickCancelHover`가 매 프레임 `Vector3.Lerp(cur,target,1-exp(-speed·dt))`, EndAim서 즉시 기준복원). ② 쿨다운 fill 방향=`fillOrigin=Top`+`fillClockwise=false`+`fillAmount=remaining/total` → 어두운 오버레이가 12시부터 **시계방향으로 걷혀 사라짐**(clockwise=true는 반대=버그였음). ③ `SkillAimReticle`=단일 Ring→**3겹 SpriteRenderer(Ring 진한 아래/Fill 반투명 중간/Dot 중앙점)** 재설계: 루트 X90° 눕힘, Inspector 노출 `_fillColor`(기본 붉은반투명 0.8,0.2,0.2,0.25)·`_edgeColor`·`_sizeScale`(Vector2=타원 가로/세로)·`_visualMultiplier`·`_ringThickness`·`_dotScale`. `Show(pos,radius)` 시그니처 유지. 셋업 `BuildReticle`이 Ring/Fill/Dot 자식(내장 Knob 스프라이트)+3필드 배선.

### 연구소 유닛 강화 시스템 — 전체 구현 완료·멀티플레이 실기 PASS (2026-07-31)
- **Phase 2 완료(UseCase·네트워크·UI·자연회복)**: Phase 1(아래 방어력·공식·×10) 위에 나머지 전부 구현·멀티 실기 PASS.
- **`Application/UseCases/UnitUpgradeUseCase.cs`**: 팀별 트랙 레벨 `Dictionary<(TeamId,UpgradeGroup,UnitUpgradeStat),int>` 보관, (B) 사용 지점 조회 API — `GetEffectiveAttack`(=기본+`Round(기본×8%)×레벨`, 유닛별 고정 정수)/`GetMoveSpeedMultiplier`(1.000~1.320)/`GetDefense`(0/8/16/24/32/40)/`GetRegenPerSecond`(3×레벨)/`ScaleByGroupAttack`(힐=그룹 공격력 트랙 조회). 자연회복 트랙은 `RegenCanonicalGroup`로 정규화(팀 단위 1트랙). 비용/시간 테이블 + 그룹 배율(`GetGroupCostMultiplierPercent`: 동물 200/자연회복 250/그외 100). `OnResearchCompleted` 이벤트, `TryGetActiveResearchByBuilding(buildingId)`. 선례 `ResourceUseCase._incomeMultipliers`.
- **`Domain/Unit/UpgradeGroup.cs`**: `UpgradeGroup`(Human Melee/Ranged/Vehicle·Spirit Fire/Water/Earth·Trans Animal/Plant 8) + `UnitUpgradeStat`(Attack/Defense/MoveSpeed/Regen) + `UpgradeGroupHelper`(`GetGroup(UnitType)`·`MaxLevel=5`·`RegenCanonicalGroup`). 순수 Domain(BuildingTypeHelper 패턴).
- **`Infrastructure/Network/NetworkUpgradeController.cs`(NetworkBehaviour)**: 연구 요청 ServerRpc → 서버 골드 검증·차감·트랙 잠금·타이머 → 완료 시 팀 레벨 반영. **완료 레벨 = 양 클라 브로드캐스트(효과 양쪽 적용), 진행 중 = 소유자만**. 파괴 시 진행 연구 취소·100% 환불. **⚠️ 스폰 레이스 버그 수정**: `OnNetworkSpawn` 시점 `IGameServices` 미등록 가능 → 사용 시점 `ResolveServices()`(지연 재조회)로 복구. 서버만 `OnResearchCompleted` 구독.
- **연구 패널 UI(매트릭스 2-레이어)**: `Presentation/UI/ResearchPanelUI.cs`가 **기존 독립 MonoBehaviour → `: BuildingPanelBase` 상속**으로 전환(공통 헤더·닫기·철거+환불 상속, 철거 시 진행 연구도 GameBootstrapper `OnBuildingDied` 구독이 취소·환불). 본문 2-레이어(연구소 단위): 매트릭스(`ResearchMatrixView`/`ResearchCellView`, 인간·정령 3×3·초월 2×3+자연회복 전체폭 1) ↔ 진행 게이지(`ResearchProgressView`, Lv X→X+1·게이지·취소). `UpdateLayerVisibility()`로 전환. buildingId↔진행 매핑: 싱글/호스트=`TryGetActiveResearchByBuilding`, 순수 클라=서버 착수 확정 buildingId 로컬 귀속. 아이콘 대신 텍스트 라벨(헤더 아이콘 후속).
- **배선·디버그**: 초기 셋업/디버그에 쓰였던 에디터·디버그 스크립트(멱등 배선, 업그레이드 핫키)는 역할 종료 후 제거됨 → 배선은 씬에 반영 완료. 수정: GameBootstrapper(신규 UseCase·NetworkUpgradeController 배선·연구소 파괴 구독)·GameEvents·InputHandler·AIOpponentController·BuildOrderStep.
- **후속 보류(과대 표기 금지)**: UI 레이아웃 다듬기(자동생성 골격)·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐(미구현)·싱글 자연회복 실기.

### 연구소 유닛 강화 시스템 Phase 1 — 전투 데이터·공식 기반 (2026-07-25→31 실기 PASS)
- **범위**: 방어력 필드 + 데미지 감쇄 공식 + Tank/CannonCart 건물 2배 + ×10 스케일 config 재조정. 연구 UseCase·팀 배율·자연회복·네트워크·UI·AI는 **Phase 2**(위 "전체 구현 완료" 항목에서 완료). **Phase 1의 "방어력을 팀 연구레벨로 조회" 예정 주의는 Phase 2에서 `UnitUpgradeUseCase.GetDefense` 조회로 실제 연결됨.**
- **신규 순수함수 `DamageCalculator.ApplyDefense(int raw, int defense)`**(`Domain/Combat/DamageCalculator.cs`): `Max(1, Round(raw×(1−def/(def+120))))`, K=120, floor 1, 감쇄율 하드캡 65%(`MaxMitigationRate`). **하위호환 핵심**: `raw<=0`이면 그대로 반환(0공격력 유닛 회귀 방지), `defense<=0`이면 계산 없이 raw 그대로(반올림 개입 0). Domain이라 System.Math만 사용(UnityEngine 금지).
- **최종 데미지 수렴 헬퍼 `UnitCombatUseCase.ComputeFinalDamage(attacker, target, rawDamage)`**(private static): ① target이 BuildingData이고 attacker가 Tank(6)/CannonCart(7)면 ×2, ② target.Defense(UnitData)/BuildingData(0)로 ApplyDefense. **삽입 지점**: `ApplyDamageToVictim`(직격, attacker.AttackPower), `ApplyFixedDamageToVictim`(스플래시/파도, amount). 파도(TorrentSpirit)·QuakeSplash·BattleAxe 휩쓸기 전부 이 두 경로 경유 → 자동 반영. **DoT(`ApplyTimedDamageToUnit`)는 미경유=무감쇄**(규칙 6). 타워는 `TowerCombatUseCase.ExecuteTowerAttack`에서 `DamageCalculator.ApplyDefense(raw, target.Defense)` 직접(2배 무관).
- **방어력 필드**: `UnitStats.StatValues.Defense`(int)+`GetDefense`, `UnitData.Defense`(생성 시 `GetDefense(type)` 스냅샷, IsHealer와 동일 패턴), `BuildingData.Defense => 0`(트랙 없음), `UnitStatEntry.defense`(스키마, 구 .asset은 0 폴백), GameBootstrapper.Setup 매핑 1줄. **Phase 2 주의**: 규칙5는 방어력을 "피격 대상 팀 연구레벨"로 조회한다 — Phase 1 스냅샷 필드는 골격일 뿐, Phase 2에서 ComputeFinalDamage의 defense 조회를 팀 레벨 조회로 교체 예정.
- **×10 config**: 코드 폴백 default 변경(`SpecialAttackConfig` blast2→20/inferno5→50/bloom20→200/wave10→100, GameBootstrapper.Setup 폴백 리터럴 동일). **실제 .asset(UnitStatsConfig·BuildingStatsConfig·SpecialAttackConfig)에 ×10 값이 커밋 반영됨** → 저장소 기본값이 ×10(Inspector 우선). 재조정 내역: UnitStatsConfig maxHp·attackPower ×10, BuildingStatsConfig 종족 HP·공격력 6필드 ×10, SpecialAttackConfig DoT/힐 4필드(blast/inferno/bloom/wave) set. 적용에 쓰였던 셋업 에디터 스크립트는 역할 종료 후 제거됨(재실행 불필요). 방어력/사거리/이동/쿨/골드/비율/반경은 불변.
- task: `_Tasks/2026-07-22/10_08_unit-upgrade-system/`(Plan 항목0·1·2·10, GameSystemRules_Upgrade 규칙1·5·6). 신규 .cs 2개 .meta는 Unity가 재임포트 시 생성.

### QuakeSpirit(대지의 정령, UnitType 15) 착탄형 즉발 AoE (2026-07-20) — 코드 완료(에디터 스크립트·스탯·프리팹·OnAttackHit·VFX·설계문서 후속)
- **핸들러**: `Application/Combat/QuakeAttackBehavior.cs`(ISpecialAttackBehavior, `ReplacesPrimaryAttack=false`). 레지스트리 1줄 등록. 착탄 중심=주 타깃 월드위치, 반경(`_quakeRadius`) 내 **적 유닛+적 건물** 선수집→**주 타깃 제외**→각 대상 즉발 스플래시 `CeilToInt(공격력×_quakeSplashRatio)` 적용. 주 타깃 100%(20)는 기존 ExecuteAttack이 담당(무변경).
- **반경 헬퍼 공용화**: `BlastAttackBehavior.CollectEnemyUnitsInRadius`를 `private static`→`internal static`로만 변경(로직/시그니처 불변 → MushroomBomber 유닛만 수집 동작 완전 무변경, 회귀 없음). QuakeSpirit이 이걸 그대로 재사용(유닛), 건물은 신규 `QuakeAttackBehavior.CollectEnemyBuildingsInRadius(internal static)`. **유닛/건물 버퍼 분리**(`_unitVictims`/`_buildingVictims`) + 주 타깃 제외를 유닛/건물 Id 각각(`as UnitData`/`as BuildingData`)으로 판정(규칙 29 — Id 카운터 달라 값 충돌 방지). 공용 헬퍼는 주 타깃 미제외라 QuakeSpirit은 apply 단계에서 제외.
- **즉발 임의량 피해 경로 신설**: `UnitCombatUseCase.ApplyQuakeSplash(attacker, IDamageable)`(비율 올림 계산) → `ApplyFixedDamageToVictim(attacker, target, int amount, bool immediatePresentation)`(신규, ApplyDamageToVictim의 attackPower 고정 한계 회피 — 사망처리 순서 동일 복제, 기존 헬퍼 무변경). immediatePresentation=false(BattleAxe 휩쓸기와 동일, HitPresentationQueue 단일프레임 분기가 AoE 동시 방출 처리). 건물 피해도 동일 경로(공성 기여).
- **컨텍스트 확장**: `SpecialAttackContext`에 `Buildings`(IReadOnlyDictionary<int,BuildingData>)·`QuakeRadius`·`ApplyQuakeSplash(Action<UnitData,IDamageable>)` 추가. ctor 3인자 추가(호출부=UseCase 1곳만). 튜닝: `SpecialAttackConfig._quakeRadius`(1.0)/`_quakeSplashRatio`(0.5)+getter, GameBootstrapper.Setup float 주입(폴백 1.0/0.5).
- **후속(이번 미포함)**: 에디터 스크립트(RegisterQuakeSpiritPrefabs/생산배선)·UnitStatsConfig(15) 스탯·OnAttackHit 주입·VFX·GameDesignDocument 설계문서. task: `_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/`.
- **에디터 셋업/데이터 배선 완료(2026-07-20, Plan (e))**: ① `UnitStatsConfig.asset` type15 추가(HP250/공20/range0.5/detect1/move0.5/cooldown5/hitFrameTimes[- 1 placeholder]/생산30·400·1, isHealer 없음). ② `SpecialAttackConfig.asset`에 `_quakeRadius:1`/`_quakeSplashRatio:0.5` 명시 추가(코드 폴백값과 동일 — bloom/blast/inferno는 여전히 asset 미기입 코드폴백, quake만 명시). `_specialAttackConfig` 씬 배선 확인됨(asset guid f859831499fb…이 Game.unity 1회 참조, Setup.cs가 quakeRadius/Ratio 읽어 UnitCombatUseCase 주입 line309~321). ③ `Assets/Editor/Setup/RegisterQuakeSpiritPrefabs.cs`(메뉴 `Hexiege/Setup/Register QuakeSpirit Prefabs (Game)`) — UnitFactory `_spiritPrefabs` type15 멱등 등록. **정령 라인은 `_spiritPrefabs`(RaceId.Spirit)** — transcendence 아님. 프리팹 `Prefabs/Units/Spirit/Unit_QuakeSpirit_{Blue,Red}.prefab`. **조사결과: 씬에 type15 이미 올바르게 배선됨**(blue guid d941b882…/red 9582c9d3… 실측일치) → 실행 시 no-op. ④ `Assets/Editor/Setup/WireEarthSpiritProduction.cs`(메뉴 `Hexiege/Setup/Wire Earth Spirit Production (Game)`) — ProductionPanelUI `_buildingUnitMappings` 땅라인 배선. **흙(땅) 라인 = StoneMound(21)/TerraForge(22)/GaeaSanctum(23), 라인업 = DustSpirit(13,req1)/BoulderSpirit(14,req2)/QuakeSpirit(15,req3)**(설계: GameSystemRules_AI_Scenario_Spirit "4. 생산 건물 라인 정의"). **씬 컨벤션: 라인의 3단계 건물 모두 동일 3-유닛 라인업을 blue/red에 노출, requiredStage로 잠금**(Human/Spirit 전 라인 동일 확인). **조사결과: 21/22/23 모두 [13/14/15 req1/2/3] blue/red 이미 배선됨, quake 포트레잇 GUID 실측일치**(blue ca61857f…/red 6b0232f6…) → 실행 시 no-op. ⑤ OnAttackHit: 코드 자동 불가(실측 필요) → 사용자가 `Hexiege/Combat/Inject OnAttackHit Events (From Config)` 실행 시 hitFrameTimes[1]=1.0s placeholder로 Attack 클립에 주입(clip.length≥1.0 조건), 실제 타격프레임 육안 검증 후 조정 필요. VFX는 사용자 제작.

### BloomFairy(꽃요정) 힐러 유닛 + HoT/DoT 공용 시스템 (2026-07-18) — 코드 완료(에디터 배선·QA 잔여)
- **힐러 식별 = 방식 A(역할 플래그) 채택**: `UnitStatsConfig.isHealer` → `UnitStats.StatValues.IsHealer`/`GetIsHealer` → `UnitData.IsHealer`(생성 시 고정). UnitType 하드코딩 회피(향후 지원유닛 확장). 기존 스탯 주입 경로와 정합 — 필드 1개 추가로 충돌 없음.
- **(a) HoT/DoT 공용 시간지속효과 시스템**(`UnitCombatUseCase`): `TimedEffectKind{Heal,Damage}` + `ActiveTimedEffect`(class, TargetId/SourceId/Kind/TotalAmount/Duration/Elapsed/AppliedAmount) + `List _activeTimedEffects` + `ApplyTimedEffect`(대상별·종류별 1레코드, 재부여=리셋) + `TickTimedEffects`(diff 방식: `cumulativeTarget=Round(Total*Elapsed/Duration)` − `AppliedAmount` → 분할오차 없이 정확히 총량 도달, 만료/풀피(HoT)/사망 시 제거) + `CastHeal`(HoT 진입점) + `ApplyTimedDamageToUnit`(DoT 자체 경로 — 이번엔 미호출, 후속 QuakeSpirit·MushroomBomber가 Kind.Damage로 재사용). 파도(`_activeWaves`/TickWaves)와 동일 소유·틱 패턴.
- **서버 틱 진입점**(파도 TickWaves 바로 옆, 이중 틱 금지): 싱글=`GameBootstrapper.Update`(`!IsNetworkMode`), 멀티=`NetworkCombatController.TickCombat`(IsServer). `TickTimedEffects` 추가.
- **(b) 부상 아군 탐색**(`UnitCombatUseCase.FindInjuredAllyToHeal`→`int?`, `HasInjuredAllyInHealRange`): 적 탐색과 팀 필터 반대(`Team==healer.Team && IsAlive && Hp<MaxHp`), **본인 포함**(self 제외 안 함), 아군 유닛만(건물 X). 사거리=`CalculateRangeLimits(isDetect:true)` 재사용(DetectRange×TileHeight). 우선순위=잃은%((MaxHp-Hp)/MaxHp) 최대 → 동률 거리 최소. **기존 적 탐색 메서드 무변경**.
- **(c) 힐러 상태머신**(`UnitView`): `ShouldEngage()` 헬퍼(healer면 HasInjuredAllyInHealRange, 아니면 HasEnemyInDetectRange)로 두 Lerp 감지지점 통일. 감지 블록에서 `if(IsHealer) EnterHealLoopV3() else EnterCombatPursuitV3()+StopCombatAnimation()`. `EnterHealLoopV3`: 재탐색→PlayHealCastAnimation(대상 회전+Attack=힐클립 CrossFade)→HitFrameTimes[0] 대기→CastHeal→쿨다운(AttackCooldown) 대기→반복. `_isInCombatPursuit=true`로 repath 보호. 힐 종료 후 공유 forwardTile 정렬+ResumeFromForwardTileV3로 A* 재개.
- **⚠️ HoT 발동은 애니 이벤트(OnAttackHit)가 아니라 상태머신 HitFrameTimes 타이머로 구동**(기존 데미지와 동일 관례). 이유: OnAttackHit 미주입 시 회복이 아예 안 되는 취약점 회피. OnAttackHit은 힐 이펙트/사운드 연출용으로만 남김.
- **⚠️⚠️ 멀티 핵심 함정 — NetworkCombatController.TickCombat이 모든 유닛을 독립적으로 combat.TryFindTarget에 넣어 적 전투로 몰아넣음.** healer(attackRange 4.0)는 적을 잡아 잘못된 공격 진입 → **쿨다운 감소 직후 `if(unit.IsHealer) continue;` 가드 추가**(쿨다운은 통과시켜야 힐 재시전 정상). 싱글은 TryAttack이 EnterCombatLoopV3에서만 호출돼 healer가 안 타므로 무영향.
- **멀티 힐 애니 동기화**: 신규 `GameEvents.OnUnitHealCastStarted`(int) → NetworkCombatController `OnUnitHealCastStartedHandler`가 `SetUnitAnimState(Attack)`만(ExecuteAttack 없음). 재개는 기존 OnUnitWalkStarted→Walk 재사용.
- **무변경 재사용**: `UnitData.Heal`, `OnEntityHealed`, `NetworkHealthSync`(SyncHealClientRpc), FloatingHpText 치유색. 특수공격 레지스트리 미등록(힐러 전용 경로).
- **에셋**: `Resources/Config/UnitStatsConfig.asset` unitType 27 추가(HP50/공격력0/attackRange4/detectRange4/moveSpeed1/attackCooldown3/hitFrameTimes[1]/생산20·골드150·인구1/isHealer:1). SpecialAttackConfig에 `_bloomHealAmount`(20)·`_bloomHealDuration`(3) 추가→GameBootstrapper.Setup가 float 주입.
- **에디터 잔여**: ① BloomFairy 힐 클립 OnAttackHit 1개 주입(연출용, 실측 hitFrame) ② UnitFactory `_transcendencePrefabs`에 type27(Unit_BloomFairy_Blue/Red) 등록 ③ 생산건물 매핑에 BloomFairy ④ (선택)힐 VFX EffectPreset. ⚠️ attackRange4.0이라 OnAttackHit 주입+힐프리셋 시 tracer 분기(_combatTargetTransform=null) 타므로 힐 VFX는 non-tracer로 구성 또는 OnAttackHit 미주입 유지.
- **에디터 배선 조사 결과(2026-07-18)**: ② UnitFactory `_transcendencePrefabs` type27은 **이미 Game.unity에 올바르게 배선돼 있음**(UnitFactory 인스턴스 guid ffbee3fe…, blue guid f9886e15…=Unit_BloomFairy_Blue / red guid e9897a02…=Unit_BloomFairy_Red 정확 매치, 라인@25776). 멱등 등록/검증 툴 `Assets/Editor/Setup/RegisterBloomFairyPrefabs.cs`(메뉴 `Hexiege/Setup/Register BloomFairy Prefabs (Game)`) 신설 — 실행 시 no-op(이미 올바름) 보고. **함정 주의**: BuildingFactory(guid 40138d8…)도 `_transcendencePrefabs` 필드를 갖고 그 type27=FeralAltar(BuildingType 27) 건물 프리팹을 가리킴(숫자만 겹침, UnitType 아님). 반드시 `FindFirstObjectByType<UnitFactory>`로 타입 특정할 것.
- **③ 생산 라인 = 데이터(씬)**: 유닛 생산 목록 정의처는 `ProductionPanelUI._buildingUnitMappings`(List<BuildingUnitMapping>, Game.unity 씬 직렬화). BuildingType별 blueUnits/redUnits(UnitPortraitEntry{type, portrait, requiredStage})로 라인업 지정, requiredStage로 잠금. 코드 스위치 아님. **식물 라인(SporePatch 30=stage1 / FloralNursery 31=stage2)은 씬에 아직 전혀 미배선**(grep 0건) — MushroomBomber(26)·BloomFairy(27) 둘 다 생산 패널 미노출. 설계 문서(GameSystemRules_AI_Scenario_Transcendence): 라인=[MushroomBomber(req1), BloomFairy(req2)], BloomFairy는 FloralNursery(stage2)에서 해금. 포트레잇 스프라이트는 존재(`Sprites/Units/Transcendence/{BloomFairy,MushroomBomber}/*_portrait_{blue,red}.png`). **【정정 2026-07-19】위 "미배선(grep 0건)"은 이제 outdated** — Game.unity 씬에서 SporePatch(30)·FloralNursery(31) 매핑이 **둘 다 이미 존재하며 각각 [MushroomBomber(26,req1), BloomFairy(27,req2)] blue/red 전부 배선 완료**(포트레잇 GUID 4종 실측 일치: mushroom blue e26b1c05/red 4e890ee2, bloom blue 4f64b197/red 6347c806). UnitFactory `_transcendencePrefabs`도 type26(Unit_MushroomBomber_Blue guid 2f6c4a53/Red ad61e84b)·type27 이미 배선됨. → 아래 두 멱등 셋업 스크립트는 실행 시 **no-op(이미 올바름)** 리포트.
- task: `_Tasks/2026-07-18/03_40_bloomfairy-healer/`.
- **에디터 셋업 스크립트 2종 신설(2026-07-19, MushroomBomber task (f))**: ① `Assets/Editor/Setup/RegisterMushroomBomberPrefabs.cs`(메뉴 `Hexiege/Setup/Register MushroomBomber Prefabs (Game)`) — UnitFactory `_transcendencePrefabs` type26 멱등 등록(RegisterBloomFairyPrefabs 그대로 본뜸, `FindFirstObjectByType<UnitFactory>` 타입 특정, intValue). ② `Assets/Editor/Setup/WireFloraProductionLine.cs`(메뉴 `Hexiege/Setup/Wire Flora Production Line (Game)`) — ProductionPanelUI `_buildingUnitMappings` 멱등 배선(SporePatch→MushroomBomber req1, FloralNursery→BloomFairy req2). 필드명: `_buildingUnitMappings`/`buildingType`/`blueUnits`/`redUnits`/`type`/`portrait`/`requiredStage`. **비파괴**(기존 다른 유닛 엔트리 삭제 안 함), 매핑 없으면 새 항목 추가(InsertArrayElementAtIndex 후 `ClearArray()`로 복제 잔여 제거 필수), enum intValue. 현재 씬은 이미 풀 배선이라 둘 다 no-op 리포트.


### TorrentSpirit 파도형 이동 AoE + 힐 서브시스템 (2026-07-17) ✅ 코드/QA 완료(VFX 튜닝 사용자 진행)
- **special-only**: `ISpecialAttackBehavior.ReplacesPrimaryAttack`=true면 `ExecuteAttack`이 주 타깃 단일 피해 생략, 핸들러만 실행. ⚠️ special-only는 특수 판정이 **건물도 순회**해야 성 파괴(승리조건) 가능 — `TickWaves`가 `_buildingPlacement.Buildings`도 순회(유닛/건물 Id 카운터 달라 hit-set 분리). BUG-002 교훈.
- **서버 권위 이동 파도**: `TorrentAttackBehavior`(+`WaveSpawnRequest`)는 모양/방향만 계산→`SpawnWave`, 전선 전진·판정·효과는 `UnitCombatUseCase.TickWaves`(`ActiveWave`). 월드 직사각형(폭×전방, forward=주 타깃), 닿는 대상 1회. 틱: 싱글=GameBootstrapper(`!IsNetworkMode` 가드)/멀티=NetworkCombatController(IsServer), 이중 틱 금지. 파도 피해는 `EntityDamagedEvent.ImmediatePresentation`으로 HitPresentationQueue 우회 즉시 방출.
- **힐 서브시스템(BloomFairy 공용)**: `UnitData.Heal`(MaxHp 클램프, 죽으면 무동작), `OnEntityHealed`/`EntityHealedEvent`(피격과 분리 채널). **NetworkHealthSync는 HP 감소만 동기화했었음** → 증가(힐) 분기 + `SyncHealClientRpc` 신설, `FloatingHpTextSpawner` 치유 색상 텍스트.
- **HoT 힐 텍스트 = "틱 억제 + 완료 1회"(2026-07-19)**: `EntityHealedEvent`에 `ShowText`(false=텍스트 skip, HP 동기화는 유지)·`HealAmount`(>0이면 "+총량" 표시, 0이면 기존 현재HP 표시) 2필드 추가(5-arg 오버로드 하위호환:true/0). HoT 틱 `ApplyHealToUnit(...,showText:false)`, 즉발/파도는 default(true/0)로 무변경. `ActiveTimedEffect.ActualHealed`(Heal 전후 Hp차 누적, 재부여 리셋)로 완료 시 `PublishHealCompletionText`(target.Heal 재호출 없는 **텍스트 전용** OnEntityHealed 1회). **사망(targetDead)·조기제거 경로는 텍스트 생략**. 멀티: `SyncHealClientRpc`/`SyncUnitHeal`에 showText/healAmount 전파 — HP동기화(diff>0)와 텍스트재발행(showText) **분리**(완료 텍스트는 diff=0이어도 뜸). 핵심: `OnEntityHealed`가 텍스트+HP동기화 겸함 → 플래그로 분리.
- **VfxPoolItem 교훈**: "빈 루트 + 형제 파티클 여러 개"(예: 파도 3종) 프리팹은 `ParticleSystem.Play()`가 형제를 재생 안 해 첫 하나만 보임 → 루트에 PS 없으면 직속 자식 시스템 전부 재생하도록 수정(루트-PS 프리팹 불변). `EffectPreset`은 vfx 프리팹 감싸는 SO → `UnitEffectConfig` attackPreset에 연결.
- 튜닝: `SpecialAttackConfig` waveWidth/Length/TravelTime/Heal(Inspector), 피해=attackPower. 데이터 배선(스탯18/EffectPreset+meta/UnitEffectConfig/OnAttackHit 0.5s 임시/SpecialAttackConfig 파도필드)은 YAML 직접 편집으로 처리. 규칙 28~31. **세션 한도로 game-programmer/document-manager 서브에이전트 반복 실패 → 메인 세션이 검증·버그수정·문서 직접 수행.** task: `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/`.

### 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 아키텍처 (2026-07-17) ✅ 사용자 실기 PASS
- **특수 공격 전략 핸들러 구조(신규 패턴)**: `ISpecialAttackBehavior.Apply(SpecialAttackContext)` + `SpecialAttackContext`(공격자·주 타깃·유닛 목록·재사용 피해 헬퍼·월드 좌표 조회 수단·reach/arc) + `SpecialAttackRegistry`(`UnitType→핸들러`, 현재 BattleAxe→`SweepAttackBehavior`만) + 유닛별 핸들러. 모두 `Scripts/Application/Combat/`. `UnitType` 키 매핑이라 인스펙터 배선 불필요. **신규 특수 유닛 = 핸들러 추가 + 레지스트리 1줄**, `ExecuteAttack` 재수정 불필요.
- **피해 수렴점 단일화**: 싱글/멀티 공통 `UnitCombatUseCase.ExecuteAttack`의 인라인 단일 피해(피해+이벤트+사망 처리)를 `ApplyDamageToVictim` 헬퍼로 추출 → 주 타깃/AoE가 같은 경로 사용(멀티 HP 동기화 일관). 말미에 특수 공격 훅 1줄.
- **휩쓸기 판정 = 월드 좌표 전방 부채꼴(SweepAttackBehavior)**: 초기 "전방 5타일 타일 기준"에서 실기 후 변경. forward=공격자→주 타깃 방향(월드 XZ), 각 적 **XZ 거리 ≤ `sweepReach` AND 각도 ≤ `sweepArcHalfAngle`**이면 피격. Y(UnitYOffset) 무시, 겹친 적(거리≈0) 포함, 아군/사망/공격자/주 타깃 제외, 건물 미대상. 월드 좌표=`IEntityPositionProvider`(서버 권위). 순회 중 사망 컬렉션 변경 회피 위해 대상 선수집 후 일괄 적용.
- **튜닝 SO `SpecialAttackConfig`(Infrastructure/Config)**: `sweepReach`(기본 1.0, 실기값 0.75)·`sweepArcHalfAngle`(기본 120). GameBootstrapper가 SO값을 **float로 UnitCombatUseCase 생성자에 주입**(Application→Infrastructure 역참조 회피, 미연결 시 코드 폴백). 에셋 `Resources/Config/SpecialAttackConfig.asset` + `_specialAttackConfig` 배선 완료. 에디터 툴 `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs`(메뉴 `Hexiege/Setup/Create SpecialAttackConfig Asset (Game)`)로 에셋 생성+배선 멱등 자동화.
- **⚠️ 교훈 — SO 튜닝값은 "에셋 생성 ≠ 씬 배선"**: `SpecialAttackConfig.asset`에 0.75를 넣어도 GameBootstrapper `_specialAttackConfig`에 연결 안 되면 런타임은 폴백(1.0)을 씀 → 값 미반영 함정. 신규 SO 튜닝값은 배선까지 확인(또는 셋업 스크립트로 자동 배선).
- **⚠️ 교훈 — "리치" 2종 구분**: 유닛 `attackRange`(주 타깃 공격/추격, UnitStatsConfig) vs 특수 AoE `sweepReach`(SpecialAttackConfig)는 별개 값. 혼동 주의. 헥스 인접 타일 중심 간 거리 ≈ 0.9~1.0 월드(FlatTop, TileWidth/Height=1.0)라 상대 유닛 사거리(피스톨러 1.0 등)와의 관계 고려 필요.
- **AoE 피격 연출 동시 방출**: `HitPresentationQueue.OnLocalAttackHit`이 공격자 `HitFrameTimes.Length≤1`이면 보류 큐 전부 방출(휩쓸기 N마리 동시 표시), `>1`(LionKnight 2타·FlameSpirit 6타)이면 기존대로 1건(회귀 없음). 타격 프레임 수=`_unitSpawn.GetUnit(attackerId).HitFrameTimes.Length`. **교훈 — HitPresentationQueue는 "타격 프레임 1개=피해 1건" 전제** → AoE(1프레임 N피해)는 `HitFrameTimes.Length` 분기로 해결. 파일 `Presentation/Effects/HitPresentationQueue.cs`.
- **타격 타이밍/스탯**: BattleAxe `hitFrameTimes=1.1667s`(클립 타격모션 종료 프레임 35f/30fps), `OnAttackHit` 애니 이벤트를 `Hexiege/Combat/Inject OnAttackHit Events` 인젝터로 주입(특수 유닛 5종은 클립에 이벤트 없어 F-4 잔여였음 — 나머지 4종은 구현 시 주입 필수). UnitStatsConfig(unitType 5): HP80/공격력15/attackRange 0.75(0.5→조정)/detectRange1/moveSpeed1/attackCooldown3.05/생산20s/골드200/인구1.
- **병합**: main 최신화 병합 완료(폰트 에셋 충돌은 main 버전으로 정리). 규칙 `GameSystemRules_Units.md` 23~27, TDD 0.22.0. task `_Tasks/2026-07-16/18_06_battleaxe-aoe/`.

### 매치메이킹 404 수정 — 호스트 결정을 Lobby CreateOrJoin으로 전환 (A방식) (2026-07-17) 🔵 초기 정상·지속 관찰 중
- **핵심 교훈 (재발 방지)**: **P2P(Relay) 매칭에서 호스트 결정은 `MatchmakerService.GetMatchmakingResultsAsync`(전용 서버/Multiplay용 서버 지향 API — P2P 클라가 호출하면 조회 대상 리소스 없어 404)가 아니라, 모든 플레이어가 같은 `matchId`를 키로 Lobby `CreateOrJoin`(matchId=lobbyId) 원자 선점으로 해야 한다.** 먼저 만든 쪽=호스트, 있으면 참가=클라. 서버 원자 처리로 정확히 한 명만 호스트 → race condition 원천 차단. **매칭 자체(티켓/폴링/MatchId 발급)는 정상이었고 호스트 결정 단계만 404**였다.
- **SDK 시그니처 (com.unity.services.multiplayer@2.0.0)**: `LobbyService.Instance.CreateOrJoinLobbyAsync(string lobbyId, string lobbyName, int maxPlayers, CreateLobbyOptions options = null)`. **matchId를 lobbyId로 직접 사용 가능**(별도 키 매핑 불필요). `options.Data`(MatchIdKey S1 인덱스)는 **"생성" 시에만 반영, "참가" 시 무시**. 공식 문서 기준 확정 — 에디터 컴파일로 최종 확인 권장.
- **RelayJoinCode 공유 타이밍**: 호스트가 CreateOrJoin으로 Lobby 생성 직후 Relay 할당 → `UpdateRelayJoinCodeAsync`로 JoinCode 기록까지 시간차 존재. **`CurrentLobby`는 참가 시점 스냅샷이라 나중에 채워진 JoinCode 미반영** → 클라는 신규 `RefreshCurrentLobbyAsync()`(내부 `GetLobbyAsync`로 재조회)로 최신화하며 최대 15회(~15초) 폴링 대기 후 Relay 참가.
- **변경 파일(3개, Infrastructure/Network)**: `LobbyManager.cs`[추가: `CreateOrJoinLobbyByMatchIdAsync`, `RefreshCurrentLobbyAsync`], `MatchmakerManager.cs`[비활성화 주석: `DetermineIsHostAsync`/`GetStableHash`], `NetworkGameManager.cs`[추가: `StartMatchmadeGameAsync`/`HostMatchmadeGameAsync`/`JoinMatchmadeGameAsync`, `StartMatchmakingAsync` 분기 교체, 구 클라 참가 경로 `JoinByMatchIdAsync`/`JoinGameByIdAsync` 비활성화 주석].
- **비활성화 우선 원칙 준수**: 폐기 로직은 즉시 삭제가 아니라 블록 주석(`/* */`). `LobbyManager.FindLobbyByMatchIdAsync`는 미사용화됐으나 구 경로가 주석 상태라 함께 보존. **간헐(intermittent) 버그라 지속 테스트 중 → 초기 실기 404 없이 정상 확인, 확정 PASS 아님. 최종 삭제는 지속 테스트 확정 후 별도 단계.**
- 브랜치 `claude/matchmaker-404-error-pi9qdn`, 커밋 `a3dbc73`. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`.

### Android AAB 빌드 용량 최적화 (2026-07-15) ✅ main 반영
- **결과**: `codex/asset-size-optimization` 작업으로 Android AAB **190.66 MB → 125.30 MB**(65.36 MB 절감).
- **핵심 효과**: 3D 모델 텍스처 Android import max texture size 조정이 가장 큼. `Assets/_Project/Texture/Buildings/**`, `Assets/_Project/Texture/Units/**`를 `1024 → 512`로 낮춤.
- **정리 범위**: `_Old` 미사용 에셋 디렉터리 7개, normal-map PNG 93개, roughness PNG 84개 정리. 건물/장비 FBX는 mesh compression + blend shape/animation import 비활성화 등 보수적 import 조정.
- **교훈**: 원본 PNG/FBX 파일 크기와 최종 AAB 패킹 크기는 다르다. 빌드 용량은 Android import override와 실제 참조/패킹 결과 기준으로 판단해야 함. TMP Font Atlas 축소는 패킹 기준 절감은 있었지만 최종 AAB 효과가 작아 되돌림.
- **후속**: 기기 QA에서 3D 유닛/건물 텍스처 품질, 팀 색상 변형, emission/공격 이펙트 품질 확인. 상세/롤백 기준은 `Assets/_Project/Docs/AABSizeOptimization.md`.

### 코드 정리 3건 — 죽은 코드 제거 / Animator 상태 의존 제거 / Firebase 게이트 제거 (2026-07-13) ✅ 실기 통과·main 반영
- **StopMovement() 삭제(죽은 코드)**: `UnitView.StopMovement()` 호출 0건(Grep 전수) → 삭제. 주석이 이미 제거된 `OnUnitWalkStopped` 이벤트를 언급하던 불일치도 해소. 런타임 불변. 커밋 `8840798`.
- **Animator 상태 의존 제거(리팩토링, 패턴)**: 전투 종료 후 Walk 재개 3곳(`EnterCombatLoopV3` 멀티서버/싱글, `ResumeFromForwardTileV3`)이 `Animator.GetCurrentAnimatorStateInfo`로 "이미 Walk?"를 판별 → CrossFade 블렌딩 중 **출발 상태 반환**으로 어긋날 잠재 취약점(자체 원칙 "Animator 런타임 상태 의존 금지", `WaitForAttackCycleEnd` 제거 시 확립, 규칙 U-18/U-22와 동일 방향). **해결**: 신규 `_currentAnimStateHash`(마지막 지시한 상태 해시 로컬 추적, 초기값 0≠어떤 해시) + 헬퍼 `ResumeWalkAnimation()`(speed=1 후 `_currentAnimStateHash==StateWalk`면 skip, 아니면 Walk CrossFade). **CrossFade 발생 4곳 전부**(MoveAlongPathV3 Walk시작/StartWalkAnimation/PlayAttackAnimation/StartCombatAnimation)에서 필드 갱신해야 정합 — 한 곳 누락 시 로컬상태-실애니 불일치. 3곳은 `MoveAlongPathV3` 서버 가드 이후라 서버/호스트/싱글 한정, 클라(규칙22 값기반) 무영향. 겉보기 불변. 실기 통과 후 주석 처리 블록 최종 삭제 완료. 커밋 `97adaad`+후속. task `_Tasks/2026-07-13/09_28_anim-resume-state-tracking/`.
- **교훈 — "이미 X 상태인가"는 로컬 논리상태 추적으로 판별**: `GetCurrentAnimatorStateInfo`는 CrossFade 진행 중 출발 상태를 반환하므로 블렌딩 도중 질의 시 오판. 마지막으로 지시한 상태를 필드로 기억할 것. 새 CrossFade 지점 추가 시 필드 갱신도 함께(설계 규약).
- **Firebase 인증 게이트 제거(로그인 무조건 실패 버그)**: main `528c7c6`의 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 게이트가 심볼 미정의 시 **스텁 FirebaseAuthService 컴파일** → 로그인 항상 실패. Firebase Unity SDK는 `.gitignore`로 git 미포함(대형, 로컬 임포트 정책) → 게이트 제거로 실제 Firebase 코드 무조건 컴파일 복원(검증된 `combat-system-visuals`와 동일). 파일: FirebaseAuthService.cs(게이트+스텁 제거), LoginBootstrapper.cs(GPGS 가드 2곳), `Assets/Plugins/Android/mainTemplate.gradle`(firebase-auth 24.1.0/firebase-app-unity 13.11.0/gpgs-plugin-support 2.1.0 등 복원). 사용자 로컬 임포트(Firebase 13.11.0+GPGS 2.1.0) 후 컴파일/테스트 PASS. 커밋 `4fe1cf0`. **교훈**: 로컬 임포트 대형 SDK를 `#if SYMBOL` 게이트로 감싸면 심볼 누락 시 스텁이 조용히 대체돼 기능 무조건 실패 — 게이트 없이 임포트 자체로 존재 판단. 잔여: 에디터 "Firebase 초기화 실패" 런타임 로그(게이트와 무관, 별도).

### 이동/Walk 애니메이션 동기화 (Phase 2 레벨 동기화 + Phase 3 경로 출발점 보정) (2026-07-12) — ✅ 검증 완료(무귀속 유닛 15기→0기, 로그 PASS 2026-07-13)
- **핵심 패턴 — 애니메이션 상태를 NetworkVariable로 단일화(엣지 트리거 RPC → 레벨 동기화)**: 갓 스폰 유닛이 첫 Walk/Attack RPC를 스폰 레이스(구독 전 도착)로 유실하던 근본 결함 해결. `NetworkUnit._animState`(`NetworkVariable<byte>`, enum `UnitAnimState{None,Walk,Attack}`, Read=Everyone/Write=Server, `_unitId`와 동일 관례). 클라 `OnNetworkSpawn`에서 **현재 값 즉시 적용(ApplySpawnAnimState)** + `OnValueChanged` 구독 → 스폰 레이스 구조적 소멸. NGO는 같은 값 재설정 시 미전송이라 애니메이션 중복 가드 불필요.
- **교훈 — 레벨 동기화도 "적용 시점이 컴포넌트 초기화보다 이르면" 무음 실패**: OnNetworkSpawn(=ApplySpawnAnimState)이 UnitView.Initialize(Animator 캐시)보다 먼저 돌면 StartWalkAnimation이 조용히 early-return, 레벨값 그대로라 OnValueChanged 재호출 없어 재시도 부재 → 간헐 애니 누락. **봉합**: Initialize 직후 `ReapplyAnimStateToView()`로 현재값 재적용(멱등 — 레벨 기반이라 재적용 무해, IsSpawned/IsServer 스킵).
- **레이어 연결**: 서버 쓰기=Infrastructure(NetworkCombatController→`SetUnitAnimState(unitId,state)`→`GetComponent<NetworkUnit>().SetAnimState()`). 클라 적용=NetworkUnit이 같은 GO `UnitView`로 `StartWalkAnimation()`/`PlayAttackAnimation()` 직접 호출. **호스트는 OnNetworkSpawn IsServer early-return이라 _animState 미구독**(서버가 Animator 직접 제어 관례).
- **서버 쓰기 지점**: Walk=`OnUnitWalkStartedHandler`, Attack=`OnUnitEnteredCombatHandler`(2경로).
- **Attack CrossFade 책임 분리(핵심 설계 결정, 유지 확정)**: `UnitView.StartCombatAnimation`의 CrossFade를 `!IsNetworkActive || IsNetworkServer`(싱글+호스트)만 실행하도록 가드 → **클라만** NetworkVariable(PlayAttackAnimation)로 이관. StartCombatClientRpc는 유지(타겟 전달=회전추적/원거리 트레이서 조준 `_combatTargetTransform`). 클라 타격 타이밍은 로컬 히트프레임(HitPresentationQueue, 규칙19)이 게이팅하므로 CrossFade 위상 오프셋을 큐가 흡수.
- **⚠️ `_combatAnimationSent` 가드 유지 필수**: 레벨 동기화로 "애니메이션 재전송"엔 불필요해 보이나, 실제로는 `OnUnitEnteredCombatHandler`의 **ExecuteAttack(데미지)**·StartCombatClientRpc 재전송을 게이팅하는 기능 가드. 제거 시 쿨다운 사이클마다 ExecuteAttack 이중 발화→데미지 붕괴.
- **Phase 3 경로 출발점 보정(뒤로 밀림)**: `RequestMove`가 경로를 **도메인 타일(`_unitData.Position`)** 기준 계산 → 유닛이 타일 사이 이동 중(transform이 도메인보다 앞섬)에 새 경로 발급되면 `path[1]`이 실제 위치보다 뒤 → 첫 걸음 역방향. **수정(단일 지점=MoveTo)**: `AlignPathStartToTransform(path)` — 첫 스텝이 최종목적지 기준 XZ 내적<0(역방향)일 때만 발동, `FindForwardClosestTile`로 실제 transform 기준 전방 타일 구해 `ProcessStep` 도메인 정합 후 `RequestMove` 재발급. 정방향은 원본 그대로 → **일반 이동 무변경**. 신규 static 헬퍼 `TileCenterView(HexCoord)→Vector3`.
- **교훈 — 로그 계측(무귀속 유닛 자동 탐지)로 육안 불가 잔여 버그 특정**: `MoveAnimSyncLog`(임시 로거) + `[MOVESYNC-LOG]` 마커로 서버/클라 AnimState 쓰기↔수신 짝, 역방향 WARN을 계측. 검증 통과 후 전량 제거(2026-07-13, 아래).
- **잔여(코드 무수정)**: Phase 3 잔여 역방향 41건은 "최종 목적지 직선 기준 판정"이 정상 우회 경로를 오탐한 것 → 실제 버그 아님, 미수정 유지.
- **[MOVESYNC-LOG] 계측 코드 전량 제거(2026-07-13)**: `MoveAnimSyncLog.cs`+`Debugging` 폴더(+.meta) 삭제. 5개 파일(UnitView/NetworkUnit/NetworkCombatController/UnitFactory/GameEvents) 마커·로그 호출·로그전용 헬퍼(DescribeAnimatorState/LogRepath)·로그전용 지역변수(realDist/flt_*/alg_*/rft_*) 제거. **기능 코드 전부 보존**(AlignPathStartToTransform 보정·AnimState 레벨 동기화·ReapplyAnimStateToView 재적용·_combatAnimationSent·StartCombatClientRpc). 로그 txt는 `_Logs/2026-07-12/07_55_movement-walk-anim-sync/` 영구 보존.
- **엣지 경로 최종 삭제(2026-07-13, 검증 통과 조건 충족)**: `StartWalkAnimationClientRpc` 메서드·호출, UnitView `OnNetworkWalkStarted` 구독, GameEvents `OnNetworkWalkStarted` Subject+`NetworkWalkStartedEvent` struct 전부 삭제(grep 전수 확인=0). **주의**: `OnUnitWalkStarted`(int Subject)는 서버 이동시작→SetUnitAnimState 체인의 발행원이라 유지. StartCombat/ChangeTarget/StopCombatClientRpc는 타겟·회전·전투상태용이라 유지.
- task: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/`. 파일: NetworkUnit.cs, NetworkCombatController.cs, UnitView.cs, GameEvents.cs, UnitFactory.cs.


### 전투 타격 타이밍 동기화 (Phase 1~3 + 수정 1~3) (2026-07-10) — ✅ 검증 완료(4차 로그+실기 PASS, 2026-07-12)
- **타워 발사 VFX(3-1)**: `BuildingEffectConfig.attackPreset`+`GetAttack`, `EffectManager.PlayBuildingAttack(type,pos,rot)`. 재생 트리거는 **HitPresentationQueue의 타워 즉시 방출 경로**(`!AttackerIsUnit` 분기)에 통합. 위치=BuildingFactory 타워GO, 회전=타워→타겟 LookRotation(XZ), 타입=BuildingPlacementUseCase.GetBuilding(id).Type. **이중재생 없음**: 호스트=UseCase 1회, 클라=NetworkHealthSync 재발행 1회 → 각 머신 정확히 1회.
- **원거리 트레이서(3-2)**: `Presentation/Effects/TracerProjectile.cs`(VfxPoolItem 풀링 미러). **핵심 설계**: 큐 무변경, UnitView.OnAttackHit에서 원거리(AttackRange>=1.0f)면 OnLocalAttackHit 발행을 트레이서 착탄 콜백으로 지연 → 비행시간=피격연출 지연 자동 동기화. 사망flush는 트레이서 대기 없이 즉시 방출(착탄 콜백은 빈 큐 discard). 판정 상수 `UnitView.RangedAttackThreshold=1.0f`.
- **HitPresentationQueue 안전망**: ⓐ타임아웃(쿨다운×1.5), ⓑ타겟사망 FlushTarget, ⓒ즉시방출(타워/GO없음), ⓓ공격자소멸 FlushAttacker(수정2·3 — 공격자사망/전투중단 시 잔여 큐 즉시 방출). 이 flush 경로들은 순수 기능이므로 로그 제거와 무관하게 보존.
- **핵심 교훈 — Tick 경과 시간 이월분 이중 계산 버그(수정1)**: 타이머 이월 패턴에서 elapsed에 이월분을 포함시키면서 타이머에도 잔존분을 남기면 쿨다운이 15~25% 조기 소진된다. **실제 경과 시간과 타이머 잔량을 반드시 분리**할 것.
- **핵심 교훈 — 상태 기반 RPC 경쟁 조건**: 클라이언트 Attack 이탈(Walk RPC)과 서버 전송 가드(`_combatAnimationSent`, NetworkCombatController)의 불일치 → Walk 전송 시 가드를 해제하여 봉합. `_combatAnimationSent`는 기능 필드이므로 로그 제거와 무관하게 유지.
- **핵심 교훈 — 로그 계측 검증 방법론**: 타임아웃 방출 WARN을 "상태 불일치 자동 탐지기"로 사용 → 육안 불가능한 버그 2건을 특정. `[TIMING-LOG]` 마커+`CombatTimingLog`(임시 로거)로 계측 후 검증 완료 시 마커 일괄 grep 삭제(2026-07-12 제거 완료, LogRules).
- **잔여 한계(후속 이관)**: 타겟 전환 순간 서버 판정↔애니메이션 상태 틈새로 ~0.5초 지연 표시 2.7% 잔존 → 이동/Walk 동기화 후속 태스크로 이관.
- **[TIMING-LOG] 계측 코드 전량 제거(2026-07-12)**: CombatTimingLog.cs + Debugging 폴더 삭제, 5개 파일(NetworkCombatController/UnitCombatUseCase/UnitView/HitPresentationQueue/EffectManager) 마커 블록 제거. **주의점**: 기능 코드(FlushAttacker flush 로직, EnqueueTime 타임아웃, EffectManager.GetHit 프리셋, _combatAnimationSent)는 로그 위해 도입됐어도 유지. 로그 전용 `reason` string 파라미터는 FlushAttacker에서 제거. EffectManager는 `using Hexiege.Application`도 제거(로그 전용), NetworkCombat/UnitView는 다른 용도로 유지. 로그 txt는 `_Logs/`에 영구 보존.
- **UnitEffectView.cs 삭제(2026-07-12)**: 프리팹/씬/코드 참조 0건 확인 후 제거.

### 인게임/로비 볼륨·프로필 UI 로직 연결 + 음소거 기능 (2026-07-09) ✅
- **음소거 구현(저장값 보존형)**: `AudioManager`에 `SetMuted(bool)`/`IsMuted()`/`ResetAllVolumes()` 추가. PlayerPrefs 키 `"Muted"`(0/1). 뮤트는 **Master 채널만 -80dB(`MutedDb`)로 눌러** 전체 무음(BGM/SFX 논리 볼륨값은 보존). `ApplyVolume`을 `ApplyDb(param,dB)`로 리팩터(무음 -80dB와 볼륨 변환값이 SetFloat 진단 로깅 경로 공유). `SetVolume`에 자동 언뮤트(슬라이더 조작 시 `if(_muted) SetMuted(false)`).
- **VolumeControlBinder(신규, 순수 C#)**: `Presentation/UI/Common/VolumeControlBinder.cs`. 인게임/로비 볼륨 UI 공통 로직(슬라이더3+On/Off/Reset버튼+색상)을 캡슐화. `Bind(Refs)` 구조체 주입, `RefreshFromAudioManager()`로 패널 표시 시 재동기화. On/Off 버튼은 CanvasGroup 상호배타(규칙24), 슬라이더 Fill 색상은 `slider.fillRect`의 Image로 처리(규칙26, UIColorConfig `soundOnColor`/`soundMutedColor`).
- **핵심 교훈 — 프로그램 슬라이더 값 설정은 `SetValueWithoutNotify` 사용**: `slider.value=` 는 onValueChanged 발화 → SetXxxVolume → 자동 언뮤트 부작용. 패널 열 때 값 동기화가 뮤트를 풀어버리는 버그를 막으려면 반드시 `SetValueWithoutNotify`. (기존 View들의 `slider.value=` 패턴을 이걸로 대체)
- **InGameSettingsUI**: `_profileButton`/`_profileSubViewGroup`/`_profileBackButton` 추가(사운드 버튼과 동일 CanvasGroup 열기/닫기, 규칙6, 내부는 빈 토글). ProfileSubView는 Editor 스크립트가 자동 생성. **버그 수정**: `Hide()`가 서브패널을 메인으로 복원하는 부수효과로 닫힐 때 메인 화면이 잠깐 비침 → `Hide()`는 현재 화면 그대로 페이드아웃, 화면 복원은 `Show()`/`Initialize()`의 `ResetToMainView()`로 통합.
- **LobbySettingsView**: 클래스명 유지. Profile 필드/로직 제거. 컴포넌트를 SettingPanel 자식→루트로 이동(탭패널 컨벤션 통일).
- **로비 설정 탭 배선 버그 수정**: 하단 탭바가 "설정" 탭 미인식(클릭 무반응+항상 선택된 것처럼 표시). `LobbyViewModel.LobbyTab` enum에 `Setting` 추가(Profile↔Ranking 사이), `TabBarView._settingTabButton`+바인딩+색상갱신, `LobbyRootView._settingPanel`+CanvasGroup 캐시+`SetPanelVisible` 전환 완성. enum은 이름 비교라 순서 무관(`(int)LobbyTab` 사용처 없음 확인). task: `_Tasks/2026-07-09/09_58_lobby-setting-tab-wiring/`.
- **버그 수정 — VerticalLayoutGroup 형제 크기 불균등**: On/Off(전체소리켜기/전체음소거) 버튼이 서로 다른 슬롯 차지. `MuteToggleSlot` 래퍼로 `Transform.SetParent()` **재부모화(파괴/재생성 없이 fileID 참조 보존)** 하여 완전 겹침. 이후 발견된 높이 불균등(빈 슬롯 선호높이 0)은 `ChildForceExpandHeight`만으론 부족 → `LayoutElement.preferredHeight=0f`/`flexibleHeight=1f` **비율 가중치**로 최종 해결(고정 픽셀 금지, 공통 규칙 2). Editor 스크립트 `FixMuteToggleOverlap_20260709.cs`.
- **Editor 1회성**: `SetupVolumeProfileUI_20260709.cs`. 필드 자동 배선·LobbySettingsView 컴포넌트 이동·UIColorConfig 참조 연결·ProfileSubView 자동 생성. 씬 저장 전 `EditorUtility.SetDirty`+`MarkSceneDirty`+`SaveScene` 필수. **교훈: 이름 기반 자동 매칭 오연결 위험**(`_backButton`이 `OffButton`에 잘못 연결된 사례) → 참조 적으면 수동 배선이 안전.
- task: `_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/`. **사용자 실기 PASS(2026-07-10)** — 슬라이더/뮤트/초기화, 프로필 열기·닫기, 로비 탭 분리·전환, 닫힘 깜빡임 해소, On/Off 버튼 균등화 전부 확인. 커밋 범위 `66c66797..87a1dd6d`.


### 사운드 시스템 실기 버그 3종 수정 (2026-07-08) ✅
- **BUG-1 BGM 겹침 (핵심 교훈)**: `AudioManager.StartCrossfade()`에서 새 전환 요청 시 `StopCoroutine(_crossfadeRoutine)`만으로는 페이드아웃 중이던 AudioSource가 계속 재생되어 이전 BGM이 겹친다. **코루틴 중단 직후 페이드아웃 채널(active가 아닌 채널)을 즉시 `Stop()`(+ volume 0, clip null)해야 함**. GameSystemRules_Sound 규칙 8에 명문화.
- **BUG-2 볼륨 UI 규칙 위반**: 에디터 스크립트(`SetupInGameVolumePanel.cs`/`SetupLobbySettingsTab.cs`)로 생성하는 슬라이더 서브 요소 고정 픽셀값 → 앵커 비율(규칙 2), 전 TMP에 `Maplestory Bold SDF` 폰트 적용(규칙 6). **에디터 스크립트에서 TMP 폰트 지정 후 `EditorUtility.SetDirty()` 필수** — 없으면 씬 저장 시 폰트가 반영되지 않음.
- **BUG-3 SFX 볼륨 미작동**: Exposed Parameter 이름 불일치가 아니었음(3종 정상). `ApplyVolume()`에 `SetFloat` 실패 감지 디버그 로깅 추가로 진단 경로 확보. AudioMixer `SetFloat`은 실패 시 조용히 false 반환하므로 반환값 로깅이 진단에 유효.
- 브랜치 `claude/sound-system-review-itwt0t`. task: `_Tasks/2026-07-07/12_28_sound-system-bugfix/`

### Google 로그인 실기 디버깅 — GPGS signIn (2026-06-27) ✅
- **`Authenticate()` vs `ManuallyAuthenticate()` (GPGS Plugin 2.1.0)**: `Authenticate()`는 내부적으로 `isAuthenticated()`만 호출 → 기존 로그인 세션이 없으면 무조건 `SignInStatus.Canceled` 반환(계정 선택 UI 미표시). 최초 로그인은 반드시 `PlayGamesPlatform.Instance.ManuallyAuthenticate()`(`signIn()` 호출) 사용. `FirebaseAuthService.cs` 수정.
- **SHA-1 3곳 일치 필수**: ① Firebase Console(OAuth 클라이언트, google-services.json) ② Play Console GPGS 사용자 인증 정보(signIn() 검증) ③ **실제 빌드 키스토어** — 세 곳이 모두 일치해야 GPGS `signIn()` 성공. 근본 원인은 실제 `hexiege-release.keystore`가 SHA-1 등록 시 키스토어와 다른 파일이어서 실제 서명 SHA-1이 어디에도 등록되지 않았던 것.
- **실제 서명 SHA-1 확인법**: logcat `PlayGamesServices[SignInAuthenticator]` 태그의 `Cert SHA1 fingerprint`가 APK가 실제 서명에 사용한 SHA-1. 등록된 값과 비교하여 불일치 즉시 진단. SHA-1 불일치 시 `serverAuthCode length=0`(빈 값) → 정합 후 `length=73` 정상 발급.
- 잔여: Firebase 로그인 성공 후 UGS OIDC 브릿지(`SignInWithOpenIdConnectAsync("oidc-firebase")`) `id provider not found` 실패 — UGS Dashboard OIDC 제공자 미등록(별도 이슈, 멀티플레이 제한). task: `_Tasks/2026-06-27/12_26_google-login-debug/`

### 게임포기 로딩 인디케이터 미해제 버그 수정 (2026-06-26) ✅
멀티플레이 포기 시 `OnForfeitConfirmed()`에서 `ShowLoading(true)` 호출 후 씬 전환이 없어 꺼지지 않던 문제. 포기는 씬 전환 없이 GameEndUI만 표시하므로 ShowLoading 호출 자체를 제거. GameSystemRules_UI.md 규칙 L-2에서 "게임 포기(멀티)" 항목도 함께 제거.

### 랜덤 매칭 2회차 실패 — GameEndUI NGM null 참조 (2026-06-25) ✅
GameEndUI `_networkGameManager` Inspector 미연결(null) → ReturnToLobby에서 BackToLobby 미호출 → NetworkManager.Shutdown 없이 씬 전환 → 2번째 매칭 시 IsListening=True로 StartHost 재호출("Cannot start Host while an instance is already running"). 수정: GameEndUI.Initialize()에 `FindFirstObjectByType<NetworkGameManager>()` 자동 탐색 추가(LobbyUI 동일 패턴). DontDestroyOnLoad 오브젝트는 Inspector 연결 불안정 → 자동 탐색 우선. (상세: network.md)

### RuntimeLogger 유틸리티 생성 (2026-06-25) ✅
`Infrastructure/Debug/RuntimeLogger.cs` 신규. BeginSession(folderPath, role)/Log(level, system, className, message, data)/EndSession() API. `#if UNITY_EDITOR` 파일 기록, 항상 Debug.Log 출력(Logcat 대응). task: `_Tasks/2026-06-25/07_25_runtime-logger/`

### Setup.cs 하드코딩 배열 파생 (2026-06-25) ✅
- `GameBootstrapper.Setup.cs` 환불 캐시 초기화의 `stage1Buildings`(9개)/`nonProductionBuildings`(6개) 하드코딩 배열 → `Array.FindAll`+`BuildingTypeHelper.GetStage`/`IsProductionBuilding` 파생. `using System;` 추가. 환불 루프·동작·값 불변. 신규 생산건물은 `_buildingTable` 한 줄로 환불 캐시까지 자동 반영. 안 2(도메인 무변경) 선택. 사용자 PASS. 커밋 `8d74e06`(main). (상세: unit-building.md)

### 코드 구조 개선 Phase 2 (2026-06-25) ✅
- `BuildingTypeHelper`: IsProductionBuilding/GetStage/GetNextStage 3개 switch → 단일 `Dictionary<BuildingType, BuildingMeta>` lookup table. 신규 생산건물은 table 한 줄 추가로 끝. (상세: unit-building.md)
- `GameBootstrapper.Network.cs`: StartNetworkGame HexMetrics 수동 4줄 → `ApplyConfig(FlatTop, oc)` 1줄. ApplyConfig 멱등(멀티서 2회 실행 무해), UnitYOffset 누락 해소. (상세: hex-grid.md)
- 동작 보존 리팩토링 — SINGLE 7 + MULTI 2 전 항목 PASS. 기존 switch/수동4줄은 주석 보존(별도 지시 시 삭제). 브랜치 `claude/code-refactor-phase2-structural`(3838c4d)

### 코드 정리(클린업) Phase 1 (2026-06-23)
약 30개 파일 히스토리성 주석/폐기코드 제거. GameBootstrapper.Setup.cs 환불 캐시 `refundRaces` 지역변수 통합. 런타임 동작 불변. 구조 변경(switch→Dictionary)은 Phase 2 별도.

### 스플래시 로그인 흐름 — skipFade 모드 (2026-06-23) ✅
SplashOverlayView `_skipFadeOnTap` + `SetTapCallback(callback, skipFade=false)`. 자동 로그인 성공 시 FadeOut 없이 즉시 GoToNextScene → 로딩 인디케이터(SO=300)가 커버. 로그인 X는 기존 FadeOut 유지.

### 로그인 팝업 CloseButton 무반응 (2026-06-23) ✅
AnonymousWarningPopup/NetworkErrorPopup에 `_closeButton` 필드+OnCloseButtonClicked()→Hide() 추가. CloseButton GO가 있어도 SerializeField 필드 없으면 Inspector 연결 불가 → 무반응 패턴.

### LoadingIndicator 전수 적용 (2026-06-22~23) ✅
SceneLoader 정적 유틸(씬 전환 단일 진입점) 신규. ShowLoading은 코루틴 외부 동기 실행. Infrastructure→Presentation은 GameEvents(OnNetworkBackToLobby/OnNetworkRematchStarting) 경유. (상세: ui-system.md)

### Canvas SortingOrder + BlockingOverlay 확정 (2026-06-22) ✅
SO 0(HUD)/100(UIManager)/200(패널 Override)/250(ConfirmPopup)/300(LoadingIndicator). UIManager는 루트 GO 배치 필수. ConfirmPopup 독립 Canvas SO=250. (상세: ui-system.md)

---

## 토픽 파일 인덱스

### 신규 분류 (2026-06-23 재구성)
- [architecture.md](architecture.md) — 레이어 구조/제약, 정적 홀더, GameBootstrapper, SO Config 패턴, 에디터 스크립트 패턴, DontDestroyOnLoad
- [network.md](network.md) — NGO API 제약, RPC 래퍼 패턴, GO 파괴 전파, 같은 씬 재로드, 동기화 타이밍, 회전/위치 동기화
- [ui-system.md](ui-system.md) — UIManager, BlockingOverlay, SceneLoader, LoadingIndicator, Canvas SortingOrder, CanvasGroup/레이아웃/팝업/ToastUI 패턴
- [unit-building.md](unit-building.md) — 유닛 이동/전투 V3, 회전, 혼잡도, 다중히트, 건물 배치/철거/업그레이드/환불, 생산 PendingQueue, AutoTower, 랠리포인트
- [hex-grid.md](hex-grid.md) — 헥스 좌표계, HexMetrics, ViewConverter, 타일 소유권, 그리드 렌더링, 패스파인딩, 카메라, URP RT 잔상
- [work-history.md](work-history.md) — 완료 작업 상세 전체 (날짜 역순, 2026-03~06)

### 기존 토픽 (세부 보조 자료)
- [network-infra.md](network-infra.md) — Phase 1~8 상세 (UGS, NGO, 동기화, 팀 할당, 승패)
- [network-todo.md](network-todo.md) — 네트워크 미완성 항목
- [random-matching-bugfix.md](random-matching-bugfix.md) — 2026-03-16 랜덤 매칭 버그
- [unit-stats-and-combat.md](unit-stats-and-combat.md) — 스탯, IEntityPositionProvider, 쿨다운, 클라 시각 동기화
- [combat-fixes.md](combat-fixes.md) — ClaimedTile 공격 위치 보정, UnitView 회전
- [attack-direction-refactor.md](attack-direction-refactor.md) — 공격 방향 리팩터링(2D→3D)
- [rendering-and-animation.md](rendering-and-animation.md) — UnitView 애니메이션, Shader Graph, HexTileView, 팀 프리팹
- [3d-transition.md](3d-transition.md) — XZ 좌표계 전환, Phase별 수정 파일
- [camera-and-view.md](camera-and-view.md) — 카메라 틸트, ViewConverter, 경계 클램프
- [gameplay-systems.md](gameplay-systems.md) — 랠리포인트, 초상화 동적 업데이트

---

## 핵심 패턴 요약

### 팀 매핑
- TeamId: Neutral=0, Blue=1, Red=2. Host→Blue, Client→Red
- TeamAssigner 삭제됨(2026-03-20) — NetworkGameFlow에서 `IsHost ? Blue : Red` 직접 할당

### 유닛 애니메이션
- Animator.Play() 직접 호출(트랜지션 우회). 파라미터 IsDead(bool) 1개만. Root Motion OFF
- **Animator Controller 상태 m_Speed 주의**: 기본값 0이면 첫 프레임 동결. 새 상태 추가 시 m_Speed=1 확인

### 거리 비교
- 월드 거리(float) 대신 `HexCoord.Distance`(도메인 정수) 우선 — ViewConverter 무관, 부동소수점 오차 없음

### 미사용 코드 정리
- 미사용 필드 확인 시 주석 언급만 믿지 말고 코드베이스 전체 Grep 필수
- 비활성화(주석) 우선, 테스트 통과 후 삭제 (WORKFLOW 규칙)
### 2026-07-16 - Profile/Ranking Cloud Save + Leaderboard port

- Added UGS Cloud Save profile layer: `PlayerProfileData`, `LeaderboardEntry`, `IPlayerProfileService`, `ILeaderboardService`, `PlayerProfileUseCase`, `RankingUseCase`, `PlayerProfileService`, `LeaderboardService`.
- Login flow now routes first verified email login through `NicknameSetupView`; Google/email first-login nickname setup uses `PlayerProfileUseCase`.
- Lobby Profile tab now binds nickname code, email/account info, stats, my rank, nickname change popup, refresh, and logout.
- Lobby Ranking tab now uses `RankingView` + `RankRowView` with UGS Leaderboards. Hidden RankingView no longer loads data on lobby entry; `LobbyRootView` calls `RefreshAsync()` only when Ranking tab is shown.
- Runtime UI polish was added for Profile, Ranking, NicknameSetup, and NicknameChangePopup while preserving CanvasGroup-based visibility rules.
- Follow-up: email verification pending state needs explicit email propagation and cancellation/cleanup handling for unverified Firebase users.
### 2026-07-16 - Email verification flow cleanup

- Added explicit email verification origin flow: signup pending vs existing unverified login.
- `EmailVerifyView` must not rely on `OnEnable()` for display email because login panels use `CanvasGroup` show/hide.
- New guarded delete path: `LoginUseCase.DeleteCurrentUnverifiedEmailUserAsync()` -> `FirebaseAuthService.DeleteCurrentUserAsync()`.
- Existing unverified-login back path signs out only; it must not delete the account.

### 2026-07-18 - Email verification auto-login gates complete

- `LoginUseCase.TryAutoLoginAsync()` now returns an explicit auto-login result so unverified email sessions can return to verification instead of entering Lobby.
- `LoginBootstrapper` checks Cloud Save nickname after auto-login success; verified email accounts with no nickname return to `NicknameSetupView`.
- `SplashOverlay.SetTapCallback(skipFade:true)` is only safe for scene transitions. Login-scene panel transitions must use fade out to release the overlay raycast block.

### 2026-07-22 - Unit ActionSequence Tracer A0 Shadow 통과

- 신규 순수 계약 `Application/Combat/Sequencing/UnitActionSequencing.cs`, Editor 검증기 `RunUnitActionSelfValidation.cs`, `NetworkCombatController` SpearMan Shadow 진단을 추가했다. 기존 피해·HP·RPC·VFX는 유일 writer/emitter로 유지한다.
- Editor self-validation PASS. 사용자 멀티 Host는 scheduled/dispatch/unique 204/204/204, missing·duplicate·target mismatch·schedule↔dispatch facing change 0, Windup 240ms 전건 일치다.
- dispatch 지연은 min 0.013ms / avg 9.105ms / p50 8.226ms / p95 19.862ms / max 29.386ms, >16.667ms 27, >33.333ms·50ms 0. Client는 header only다.
- A0 Shadow 계측 게이트만 통과했다. 당시 reducer와 런타임 seam은 후속 범위였으며, 아래 A1에서 순수 reducer까지 완료됐다.

### 2026-07-22 - Unit ActionSequence Tracer A1 순수 계약·stateful reducer 완료

- `Application/Combat/Sequencing/UnitActionContracts.cs`와 `UnitActionSequencer.cs`에 NGO·Animator·씬·피해 writer 비의존 값 계약과 revision/서버 시간 기반 stateful reducer를 구현했다.
- 상태 전이는 AlignToAttack→Windup→Impact→Recovery, commit/cancel/dead, multi-hit 결정·확인, 회차·revision 고갈 원자적 거부를 포함한다.
- C# 9/Application compile PASS, Editor compile PASS, Unity Editor 메뉴 self-validation PASS, reflection `Validate*` 10개 PASS, 최종 Standards/Spec P0~P3 지적 0건이다.
- 런타임 pose/result seam과 피해·RPC·VFX는 미연결이다. 다음 구현은 A2 server-authoritative pose seam shadow이며 기존 writer/emitter를 유지한다.

### 2026-07-27 - Unit ActionSequence Tracer A2 pose seam runtime PASS

- pure `IUnitActionPoseSource`/`UnitActionPoseSample`, `UnitView` read-only adapter와 `NetworkCombatController` 서버/SpearMan one-cycle reducer Shadow를 연결했다. A2는 pose와 reducer 전이만 관측하며 Legacy 피해·HP·RPC·VFX·이동 writer를 바꾸지 않는다.
- 공격자 사망 시 pending observer를 조기 삭제하지 않고 reducer `MarkDead`로 `DeadTerminal` 종료한다. bounded 관측 용량 초과는 무음 삭제 대신 full canonical key의 `capacity-evicted` terminal skip으로 남긴다.
- 사용자 Editor self-validation PASS. Host 18:04:04는 schedule 429 / dispatch 428이며 마지막 1건은 종료 시 Impact 전 in-flight, 완료 회차 누락·중복 0, attacker-dead 2건 terminal, eviction·예외 0이다. Client 18:09:48 `[UAS-POSE]` 0이다.
- 다음 구현은 Tracer B Simulation Root / Visual Root 분리다. A2 PASS를 세 불일치 해결이나 result seam/권위 전환 완료로 확대하지 않는다.

### 2026-07-27 - Unit ActionSequence Tracer B0 migration readiness PASS

- `ValidateUnitCombatSetup`과 `SetupUnitVisualRoots` dry-run은 50개 유닛 프리팹의 구조·정체성·네트워크 root·이동 후보·직렬화 참조·rollback manifest를 읽기만 한다. prefab/scene/runtime mutation API와 에셋 변경은 0이다.
- 기준선은 Human 16 / Spirit 18 / Transcendence 16, VisualRoot 0, create 50 / reuse 0 / direct-child move 58, VFX assigned 16 / null 34 / direct 8 / nested 8이다.
- `LoadPrefabContents`가 생성하는 native bookkeeping 때문에 전체 `SerializedProperty` 해시는 결정적이지 않다. NetworkObject/NetworkTransform의 의미 설정 allowlist만 invariant 형식으로 해시해야 한다.
- 16KB Unity Console 절단을 고려해 runId가 있는 프리팹별 manifest와 종합 SHA-256 `[END]`를 분리한다. 사용자 연속 2회 dry-run은 50/50, errors 0, assetsModified 0, 동일 SHA-256 `1d1043ff2ea440a5f25d24a9e006bca8739ecb514b4e362f8dd6c01504ae1dcd`로 PASS했다.
- 다음은 B1 50개 프리팹 원자적 migration이다. B0는 실제 VisualRoot 생성·writer 전환이나 세 동기화 증상 해결 완료가 아니다.

### 2026-07-27 - Unit ActionSequence Tracer B1 asset migration 적용

- `VisualRootProjector`/`PresentationPoseProvider`로 Simulation pose와 presentation pose를 분리하고 `NetworkUnit`의 클라이언트 Simulation Root 보정을 제거했다. 멀티 `UnitFactory`는 canonical world position을 사용하며 A2 pose observer와 기존 서버 피해 권위는 유지한다.
- 50개 프리팹에 직접 자식 `VisualRoot`와 root projector를 적용했고 migration journal completed를 확인했다. 사용자 재실행은 50개 모두 migrated state `[NO-OP]`, prefab 저장 호출 0이다.
- 현행 validator 계약: root Animator/Renderer 0, 모든 Animator Root Motion off, 각 Animator와 동일 GO의 `AnimationEventRelay` 1개, projector 참조 유효. Collider는 B1 구조·런타임 검증 범위가 아니다.
- 신규 프리팹은 B1 bulk migration을 반복하지 않고 migrated 템플릿과 `NET-ROOT-004`를 사용한다. UnitType/Blue·Red pair/Matrix/validator roster와 VFX baseline을 원자적으로 갱신한다.
- 2026-07-27 당시 rollback failure injection과 Host/Client·Blue/Red runtime smoke는 미완료였다. 후속 rollback 판정은 아래 2026-07-29 항목을 따른다.

### 2026-07-29 - Unit ActionSequence Tracer B1 rollback 검증

- graceful failure injection index 0/24/49는 각각 journal 복구, primary 100파일 검증과 초기 analyzer를 통과했다.
- crash index 24는 강제 종료 시 prefab mismatch 25 / meta mismatch 0 / VisualRoot 25 / projector 25를 만든 뒤 별도 `RecoverCrash` 프로세스에서 primary 50 prefab + 50 meta를 100/100 복구했다.
- primary Unity compile Tundra success, `[UAS-DIAG]` self-validation PASS다.
- 구현 변경 없이 rollback 내구성만 통과했다. Android 1대와 Unity Editor counterpart의 역할교대 Host/Client `[UAS-ROOT-POSE]` runtime smoke와 Blue/Red 교차 감사가 남아 있어 B1 overall은 OPEN이고 B2 진입은 금지한다.

### 2026-07-29 - Unit ActionSequence B1 Collider 진단 가정 제거

- 2026-07-27 B1에서 근거 없이 추가된 Collider 존재·배치 조건을 공통 계약, runtime observer, Editor validator와 self-validation에서 완전히 제거했다. optional/root-only 계약도 남기지 않는다.
- B1 구조 권위는 network component placement, identity VisualRoot, root Animator/Renderer 0, projector/ref, Root Motion off와 Animator별 relay다. 런타임 권위는 서버 single-writer와 client Simulation Root write 금지다.
- 최종 실기는 같은 코드 리비전의 Unity Editor counterpart와 Android Development Build를 역할교대한다. Match A는 Editor Host Blue file + Android Client Red Logcat, Match B는 Android Host Blue Logcat + Editor Client Red file 쌍을 분석한다.
- Windows/Standalone build는 사용하지 않는다. Android 2대는 release E2E·성능·호환성에 권장할 수 있지만 B1 필수는 아니며, 두 경기 cross-audit 전 B2 시작 금지를 유지한다.

### 2026-07-30 - Unit ActionSequence B1 최종 PASS

- 50개 프리팹은 서버 권위와 `Interpolate=true`를 유지하고 `PositionLerpSmoothing=false`로 통일했다. migration·NO-OP·graceful/crash rollback과 validator가 PASS했다.
- Match A(Editor Host/Android Client)와 Match B(Android Host/Editor Client)의 겹치는 stable pose에서 mismatch 0을 확인해 B1을 완료했다. 다음 구현은 B2 서버 이동·SimulationFacing Shadow다.
- 공격 방향, 서버 Impact와 실제 피해 시점, result seam, Snapshot/ImpactResult 전환은 아직 구현·검증하지 않았다.

### 2026-08-03 - Unit ActionSequence B2 이동 Shadow 구현 완료

- pure Application `UnitMovementReducer`는 command/segment scope, revision fail-closed, 10° 진입/15° 이탈 히스테리시스, target-acquire priority와 endpoint `NoIntent` 정규화를 담당한다.
- `UnitMovementShadowObserver` v5는 서버 read-only 비교와 client sentinel만 수행한다. Legacy 이동·SimulationFacing writer와 피해·RPC·VFX 권위는 유지하며 신규 root write는 0이어야 한다.
- Android manifest는 entry-boundary 청크, UTF-8 byte 메타데이터, SHA-256, terminal 예약/preflight를 사용한다. 첫 4종 v4 + 나머지 21종 v5로 25/25종 Blue/Red 누적 증거를 확보했다.
- `legacyMovedWhileShadowAlign`은 B2 오류가 아니라 규칙 v2와 Legacy writer의 분류된 차이다. 다음 구현은 경기 시작 시 고정한 mode로 25종 writer를 한 번에 전환하는 B3이며 유닛별 혼합을 금지한다.
---

## 씬·에셋 실측 경로 및 에디터 셋업 패턴 (2026-08-10 추가)

> MistShrine 구현 사이클에서 실제로 확인한 값들. 위 섹션의 교훈을 대체하지 않고 보완한다.

## 레이어 / 경로
- 코드 루트: `Assets/_Project/Scripts/{Domain,Application,Core,Infrastructure,Presentation,Bootstrap}`
- 에디터 1회성 셋업 스크립트: `Assets/Editor/Setup/` — 네임스페이스 `Hexiege.EditorTools`, asmdef 없음(Assembly-CSharp-Editor)
- 설정 SO 에셋: `Assets/_Project/Resources/Config/*.asset`
- 대상 씬: `Assets/_Project/Scenes/Game.unity` (Login 0 / Lobby 1 / Game 2)

## 자주 쓰는 실제 경로 (확인 완료)
- `SpecialAttackConfig` → `Hexiege.Infrastructure`, 에셋 `Resources/Config/SpecialAttackConfig.asset`
- `UIColorConfig` → **`Hexiege.Infrastructure`** (파일은 `Scripts/Infrastructure/Config/UIColorConfig.cs`)
- 지면 데칼 셰이더 `Hexiege/SkillAimOverlay` → `Assets/_Project/Shaders/SkillAimOverlay.shader`
  (ZTest LEqual + Offset -1,-1 + ZWrite Off + Cull Off. **ZTest Always 금지**)
- 자동모드 테두리 회전 머티리얼 → `Assets/_Project/Materials/UI/mat_ui_rotatingborder.mat`
  (셰이더 `Shaders/UI/RotatingBorderUI.shader`. 에셋 저장값은 _Speed 5 / _Thickness 0.05만.
   _Radius·_Inset은 ProductionPanelUI가 **런타임 인스턴싱으로만** 덮어씀 → 다른 패널이 공유하면 값이 다르다)
- 폰트 `Assets/_Project/Fonts/Maplestory Bold SDF.asset`

## 건물 패널 UI 골격 (씬 실측)
- `BuildingActionPanel` = 3×3 그리드 9슬롯 원본. `_allSlotButtons` 9개, **index 5 = 철거 버튼**
- 슬롯 자식 구조: `Slot1 { IconImage, CostContainer { GoldIcon, CostText } }`, 부모는 `Row0/Row1/Row2`(HorizontalLayoutGroup)
- 패널은 오버라이드 Canvas SortingOrder **200**(`GameSystemRules_CanvasSortingOrder.md`) — 복제하면 자동 충족
- 미사용 슬롯은 **`CanvasGroup.alpha=0`**, `SetActive(false)` 금지(GridLayout 정렬 붕괴)

## 에디터 셋업 스크립트 패턴 (선례: `SkillSetup_Scene.cs`)
- 패널은 하드코딩으로 그리지 말고 **씬의 BuildingActionPanel을 `Object.Instantiate` 복제** →
  복제본의 컴포넌트에서 `SerializedObject`로 remap된 참조를 읽어 새 컴포넌트에 옮겨 배선
- 배선은 `new SerializedObject(target).FindProperty(...)` + `ApplyModifiedProperties()`
- **저장 반영 필수**: `EditorUtility.SetDirty()` + `EditorSceneManager.MarkSceneDirty()` (없으면 씬에 저장 안 됨)
- 멱등: 이름/컴포넌트로 찾아 재사용(FindOrCreateChild). 패널만은 "기존 제거 후 재복제"로 항상 1개 보장
- 이름 기반 탐색은 **타입 탐색 우선 + 이름은 폴백**, 못 찾으면 조용히 넘어가지 말고 경고
  (과거 사고: `_backButton`이 `OffButton`에 오연결)
- 에디터 스크립트의 `Debug.Log` 진행 출력은 허용(LogRules는 **런타임 파일 로그** 규칙)
- 슬롯 위 오버레이 자식은 `LayoutElement.ignoreLayout = true` 필수(안 하면 한 줄로 찌그러짐)
- 비용/라벨 숨김은 `SetActive(false)`가 아니라 CanvasGroup alpha=0 (레이아웃 footprint 유지 → 버튼 크기 균일)
- 쿨다운 fill: `Filled / Radial360 / Origin=Top` + **`fillClockwise = false`**
  (SetCooldown이 fillAmount=remaining/total로 줄이므로, false여야 어둠이 시계방향으로 걷힌다. true는 과거 버그)

## Awake + SetActive 함정 (중요)
`SkillAimReticle` / `MistShrineRangeIndicator`는 `Awake()`에서 `gameObject.SetActive(false)`를 한다.
→ 씬에 **비활성 상태로 저장하면 안 된다.** 비활성 오브젝트는 Awake가 안 돌고,
런타임 `Show()`의 `SetActive(true)` 순간 Awake가 뒤늦게 실행되며 스스로를 다시 꺼 버려 영영 안 보인다.
에디터 셋업 스크립트는 **활성 상태로 남길 것**.

## 진행 중 시스템
- MistShrine 물안개 힐: 런타임 코드 커밋 완료(`be2b396`), 에디터 셋업 = `Assets/Editor/Setup/MistShrineSetup_{Config,Scene}.cs`
  - 메뉴: `Hexiege/MistShrine/1. Apply Config Values` → `2. Setup Scene (Panel, Range, Network)`
- 알려진 기술부채: `ProductionPanelUI`의 `_unitAutoIndicators`(GameObject) ↔ `_unitBorderOverlays`(Image)가
  **같은 BorderOverlay를 이중 배선** — 정리는 별도 작업 예정. 다른 패널은 복제 금지(UI 규칙 14)

---

## Awake 자기 비활성화 함정 · 값의 단일 소스 (2026-08-11 추가)

> MistShrine 보완 수정 사이클에서 확립. 위 섹션들을 대체하지 않고 보완한다.

## 반복되는 함정 (Unity)

### 1. `Awake()`에서 자기 자신을 `SetActive(false)` 하지 말 것
비활성 상태로 씬에 저장된 오브젝트는 `Awake`가 **실행되지 않는다**.
런타임에 `Show()`가 `SetActive(true)`를 부르면 그 순간 `Awake`가 뒤늦게 처음 실행되며
**스스로를 다시 꺼 버려** 영영 보이지 않는다. (`Show()`의 나머지 코드는 돌지만 무의미)

**해결 패턴 (2026-08-11 적용, 두 파일 동일):**
```csharp
private bool _showRequested;               // [SerializeField] 금지 — 순수 런타임 상태
void Awake() { ...; if (!_showRequested) gameObject.SetActive(false); }
public void Show(...) { _showRequested = true; /* 반드시 SetActive보다 먼저 */ if (!gameObject.activeSelf) gameObject.SetActive(true); ... }
public void Hide()    { _showRequested = false; if (gameObject.activeSelf) gameObject.SetActive(false); }
```
적용 파일:
- `Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs`
- `Assets/_Project/Scripts/Presentation/Effects/MistShrineRangeIndicator.cs`

> 씬에 활성으로 저장된 기존 상태에서는 동작이 완전히 동일 → 회귀 없음.
> 에디터 셋업 스크립트가 "반드시 활성으로 저장" 주석으로 우회하고 있었다면 그건 증상 회피일 뿐이다.

### 2. `MonoBehaviour` 베이스의 `OnDestroy` 은닉
자식이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy`가 숨겨진다.
구독 해제는 UniRx `.AddTo(this)` 사용. (`BuildingPanelBase` 상속 패널 전부 해당)

### 3. UI 숨김은 `CanvasGroup.alpha=0`
`SetActive(false)`는 GridLayout 셀 정렬을 무너뜨려 남은 버튼이 앞으로 당겨진다.

---

## 값의 단일 소스(Single Source of Truth) 원칙

**같은 게임 수치를 Presentation의 `[SerializeField]`와 Config 에셋 양쪽에 두지 말 것.**
에디터 셋업 스크립트가 실행 시점에 동기화해 줘도, 밸런싱으로 `.asset`만 고치면 조용히 어긋난다.

- 패턴: UseCase가 Config에서 주입받은 값을 보관하고, UI는 `GetXxx()` 접근자로 **런타임에 조회**한다.
- 사례(2026-08-11): `MistShrinePanelUI._rangeRadius` 제거 → `MistShrineUseCase.GetRadius()` 조회로 전환.
  에디터 셋업 스크립트의 해당 배선 코드(`ConnectFloat(panel, "_rangeRadius", ...)`)도 함께 제거해야 한다.
- UseCase 접근자 스타일은 `GetCooldownRemaining` / `GetCooldownTotal`(블록 바디 + 한국어 XML 주석)을 따른다.

---

## 주요 파일 위치

| 시스템 | 경로 |
|---|---|
| MistShrine 물안개 힐 로직 | `Assets/_Project/Scripts/Application/UseCases/MistShrineUseCase.cs` |
| MistShrine 전용 패널 | `Assets/_Project/Scripts/Presentation/UI/MistShrinePanelUI.cs` |
| MistShrine 범위 원 | `Assets/_Project/Scripts/Presentation/Effects/MistShrineRangeIndicator.cs` |
| 스킬 조준원 | `Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs` |
| MistShrine 에디터 셋업 | `Assets/Editor/Setup/MistShrineSetup_Scene.cs` (메뉴 `Hexiege/MistShrine/2. ...`) |
| 특수 공격/스킬 수치 Config | `Assets/_Project/Scripts/Infrastructure/Config/SpecialAttackConfig.cs` → `Resources/Config/SpecialAttackConfig.asset` |

지면 데칼 셰이더: `Hexiege/SkillAimOverlay` (ZTest LEqual + Offset -1,-1 + ZWrite Off).
**ZTest Always 금지** — 유닛·건물을 뚫고 그려진다.

---

## 프로젝트 규칙 요약(코드 작성 시)
- 레이어: Domain → Application → Core → Infrastructure → Presentation → Bootstrap
- **Domain은 Core를 참조하지 않는다**, **Application은 Infrastructure/Unity.Netcode를 참조하지 않는다**(`NetworkContext` 정적 홀더 사용)
- `GameBootstrapper`가 유일한 조합 루트
- **Inspector 값이 코드 기본값보다 우선**
- 디버깅용 raw `Debug.Log` 금지(`Docs/LogRules.md`) — 다만 배선 누락 경고용 `Debug.LogWarning`은 프로젝트 전반의 확립된 패턴
- 주석은 한국어, 유니티 초급자도 이해할 수준으로 상세히
- 기존 로직 제거는 원칙적으로 "주석 처리 → 실기 검증 후 삭제"(WORKFLOW.md [4])

### 직렬화 필드의 잘못된 저장값을 무력화하는 방법 (2026-08-11)
- **스프라이트 기준 크기는 코드 상수로 두지 말고 `sprite.bounds.size`에서 산출한다.**
  `bounds.size`는 (텍스처 픽셀 ÷ Pixels Per Unit)이라 PPU가 이미 반영된 월드 크기다.
  매 표시마다 다시 읽으면 정식 아트로 스프라이트를 교체해도 자동으로 따라간다(캐시 금지).
- ⚠️ **직렬화 필드의 잘못된 값은 "코드 기본값 변경"으로 고칠 수 없다** — Inspector/씬 값이 코드 기본값을 이긴다.
  저장된 값을 확실히 무력화하려면 **필드 이름을 바꾸고 `[FormerlySerializedAs]`를 일부러 붙이지 않는다.**
  Unity 직렬화는 이름으로 매칭하므로, 옛 이름의 값은 로드 시 버려지고 새 필드는 C# 기본값으로 초기화된다.
  사례: `MistShrineRangeIndicator` / `SkillAimReticle`의 `_baseDiameter` → `_baseDiameterOverride`(기본 0 = 자동).
- 범위/사거리 표시용 도형에서 **비정사각형 스프라이트는 긴 축을 기준 지름으로** 삼는다.
  과대 표시(실제보다 넓어 보임)가 과소 표시보다 치명적이기 때문이다.
