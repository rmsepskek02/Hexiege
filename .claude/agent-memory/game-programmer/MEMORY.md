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

---

## 최근 작업 (상세 전체는 work-history.md)

### 닉네임 설정 흐름 통일(C안) + UGS OIDC 브릿지 해결 (2026-07-14) — ✅ 실기 PASS
- **문제**: 이메일 회원가입 직후 닉네임 저장 시 "Access token is missing"(Cloud Save). 원인 ① 가입 시점엔 UGS 세션 없음, ② 실계정이 애초에 UGS 세션을 못 받던 미해결 OIDC 브릿지 이슈.
- **코드 흐름 수정(C안)**: 닉네임 저장은 반드시 "UGS 세션이 있는 첫 로그인 성공 직후"에만 수행. 이메일 가입=닉네임 없이 인증 화면 직행(`SignUpView`), 이메일 인증 후 첫 로그인 성공 시 `IsFirstLogin` 분기로 닉네임(`EmailLoginView`에 분기+`PlayerProfileUseCase` 주입, Google `LoginSelectView.OnGoogleLoginClicked`와 동일 패턴), 닉네임 완료 후 경로 무관 항상 로비(`NicknameSetupView`), `LoginBootstrapper`가 `EmailLoginView`에 UseCase 주입. Google/익명 무변경.
- **⚠️ 교훈 — OIDC 없이는 실계정 Cloud Save 불가**: 코드 흐름만 고쳐선 부족. 실계정(Google/이메일)은 UGS OIDC 브릿지(`SignInWithOpenIdConnectAsync("oidc-firebase", token)`)가 UGS Dashboard 제공자 미등록으로 실패해 access token 자체를 못 받았음. **UGS Dashboard에 OIDC 제공자 등록**(OIDC Name=`firebase`→id `oidc-firebase`, Client ID=`hexiege`, Issuer=`https://securetoken.google.com/hexiege`, Enabled)해야 세션 획득 → 저장 성공. 익명은 `SignInAnonymouslyAsync`라 무관. **계정 귀속 데이터(닉네임 등) 저장은 UGS 세션이 있는 로그인 성공 이후에만.**
- task: `_Tasks/2026-07-14/12_09_nickname-flow-timing-fix/`. 파일: `SignUpView.cs`, `EmailLoginView.cs`, `NicknameSetupView.cs`, `LoginBootstrapper.cs`(+ 필요 시 `LoginRootView.cs`).

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
