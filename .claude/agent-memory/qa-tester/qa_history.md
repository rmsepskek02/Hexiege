# QA 완료 이력 상세 (MEMORY.md 이관본)

## 자동/수동 생산 시스템 QA (2026-03-23 완료)

### 최종 판정: PASS (FIX-1~7, FIX-9~10 실기 통과. FIX-8 FAIL → FIX-10으로 재테스트 후 PASS)

### 핵심 검증 포인트 (생산 시스템 작업 시 재사용)
- **버튼 탭 취소**: 환불 없음 확인 (ToggleAutoProduction 취소 경로에 AddGold 없어야 함)
- **슬롯1 탭 취소 후 슬롯2**: null이어야 함 (autoCount==1 + manualCount==1 케이스)
- **자동 3개 등록**: 슬롯2 즉시 골드 차감 확인 (CanAutoEntryShowInSlot AutoIndex 제외 여부)
- **수동 추가 시 슬롯 이관**: IsCharged=true 자동 항목이 ManualQueue에 이관되는지
- **슬롯 직접 취소(X 버튼)**: IsCharged=true이면 환불, false이면 환불 없음

### 생산 시스템 정적 분석 시 필수 확인 파일
1. `UnitProductionUseCase.cs` — ToggleAutoProduction (등록/취소 분기), CanAutoEntryShowInSlot
2. `ProductionPanelUI.cs` — UpdateQueueSlots (슬롯2 로직 집중), isNormalAutoState 판단
3. `ProductionState.cs` — AutoEntry, AutoEntries

### 알려진 취약 지점
- `isNormalAutoState` 판단 조건: `AutoTypeAt(AutoIndex % autoCount) == CurrentProducing` — autoCount=0이면 접근 오류 가능
- 혼용 상태(ManualQueue + AutoEntries 동시)에서 슬롯 렌더링이 복잡 — 경계 케이스 테스트 필수

---

## UI DOTween 애니메이션 QA (Phase 1+2 완료 + 실기 테스트 완료, 2026-03-19)

### 최종 판정: PASS (전 TC 통과 — 정적 분석 + 실기 테스트 모두)

### Phase 1 발견 버그 → Phase 2 수정 완료
1. **[수정됨] RematchRequestPopup `_currentFade` 공유** → `_overlayFade`/`_requestFade`/`_declinedFade` 3개 변수 + `ref Tween` 패턴
2. **[수정됨] Hide() blocksRaycasts 미해제** → FadeOut OnComplete에서 `cg.blocksRaycasts = false` 명시

### 잔존 Minor
- AnimatedPanel EnsureInitialized(): SetActive(false) 미호출 — 씬 배치 시 비활성 설정하면 무해
- AnimatedPanelSetup.cs L37/L75 주석: 구버전 "PopupFade" 기재 — 실제 코드는 SlideFromTop/SlideFromBottom으로 올바름

### 확인된 UIAnimator 패턴 (표준)
- SlideInFromTop: `pos.y = +offsetY` 시작 → `DOAnchorPosY(0f)`. SetUpdate(true) 적용
- SlideOutToTop: `DOAnchorPosY(+offsetY)` 후 SetActive(false). SetUpdate(true) 적용
- RematchRequestPopup: `ref Tween` 패턴으로 패널별 독립 Tween 관리
- `_currentSeq?.Kill()` — Show/Hide 전환 전 진행 중 Tween 정리
- `ClosedFrame = Time.frameCount` — 같은 프레임 클릭 통과 방지

---

## 카메라 줌 보간 QA (2026-03-19 완료)

### 수정 내용
- `CameraController.HandleZoom()`: 즉시 orthographicSize 적용 → DOTween.To Ease.OutCubic 보간

### 테스트 결과
- [x] 마우스 스크롤 줌인/줌아웃 부드럽게 보간
- [x] 연속 스크롤 목표값 누적 후 자연스러운 이동
- [x] 줌 경계(min/max) 정상 작동
- [x] ClampPosition 줌 중 경계 정상 동작
- [x] 팬과 동시 사용 충돌 없음
- [ ] 핀치 줌 실제 모바일 디바이스 미테스트

---

## 재경기 시스템 QA (2026-03-17 커스텀 / 2026-03-18 랜덤 통합)

### 구현 내용
- `NetworkGameManager.IsRandomMatchmaking` (bool) — 모드 판별
- `NetworkGameEndController`: RequestRematchServerRpc, AcceptRematchServerRpc, DeclineRematchServerRpc + targeted ClientRpc 2개
- `RematchRequestPopup.cs`: `_overlay`(항상 Active 루트) + `_requestPanel` + `_declinedPanel`
- `GameEndUI.SetupRematchButton()` / `RestoreRematchButton()`
- 2026-03-18: isRandomMatch 분기 제거 → 랜덤/커스텀 모두 동일 재경기 흐름

### 테스트 결과
- [x] 커스텀게임 — 다시하기 버튼 표시, 요청 중 상태, 상대 팝업, 수락/재경기, 거절/알림+버튼원복
- [x] 랜덤매칭 — 다시하기 버튼 표시, 재경기 흐름 정상 동작
- [x] 싱글플레이 — 다시하기 동작 변경 없음
- [ ] 동시 클릭 레이스 컨디션 미테스트

### 알려진 취약 지점
- **RematchRequestPopup 루트 Active 필수**: FindFirstObjectByType은 비활성 오브젝트 탐색 불가 → 루트가 비활성이면 팝업 표시 안 됨 (2026-03-17 버그로 확인)
- targeted ClientRpc는 ClientRpcParams.Send.TargetClientIds 배열 사용 — NGO 2.9.2에서 정상 동작 확인

---

## 멀티플레이 로비 복귀 QA (2026-03-17 완료)

### 구현 내용
- `NetworkGameEndController.cs`: RPC 로비 복귀 메서드 4개 제거 (로컬 처리로 변경)
- `GameEndUI.cs`: `ReturnToLobby()` (Shutdown+LoadScene("Lobby")), `CountdownCoroutine()` (30초 WaitForSecondsRealtime)
- Inspector 연결 필요: `_countdownText` (TextMeshProUGUI)

### 테스트 체크리스트
- [x] "로비로" 버튼 클릭 시 클릭한 플레이어만 Lobby로 이동 (상대방은 독립 처리)
- [x] 30초 카운트다운 텍스트가 매 초 업데이트됨
- [x] 30초 후 자동으로 Lobby로 이동
- [x] "다시하기" 클릭 시 카운트다운 중지
- [x] 싱글플레이 로비 복귀 정상 동작

### 알려진 취약 지점
- `_countdownText` Inspector 미연결 시 카운트다운 텍스트 미표시 (null 체크로 안전 처리 — 복귀 동작은 정상)
- `Time.timeScale`이 0인 상태에서도 `WaitForSecondsRealtime` 사용으로 정상 동작

---

## 전역 로딩 스크린 QA (2026-03-17 완료)

### 구현 내용
- `LoadingScreen.cs`: 싱글턴, DontDestroyOnLoad, CanvasGroup 페이드 인/아웃
- `BattleViewModel.LoadSingleplayScene()`: async void + `await Task.Delay(2000)` + Show/Hide
- 커스텀/랜덤매칭: `LoadGameScene()` 직전 Show(), `sceneLoaded` 이벤트 자동 Hide()
- Lobby 씬 LoadingScreen 오브젝트에 SerializeField 3개 Inspector 연결 필요: `_canvasGroup`, `_spinner`, `_statusText`

### 테스트 결과
- [x] 싱글플레이 — 로딩 스크린 2초 표시 후 씬 전환 확인
- [x] 커스텀 호스트/참가 — 로딩 스크린 표시 확인
- [x] 랜덤 매칭 완료 시 로딩 스크린 표시 확인
- [x] 게임 씬 진입 후 자동 Hide() 확인 (NGO sceneLoaded 정상 발동)
- [ ] 에러 발생 시 로딩 스크린 숨김 확인 (미완료)
- [ ] 매칭 취소 시 미표시 확인 (미완료)
- [ ] 반복 매칭 시 중복 인스턴스 없음 확인 (미완료)

### 알려진 취약 지점
- LoadingScreen Inspector SerializeField 연결 안 된 경우 null 참조 방어 필요 (`?.` 연산자 사용 중)
- 싱글플레이 `await Task.Delay(2000)` 중 앱 종료/씬 전환 취소 시나리오 미검증

---

## 랜덤 매칭 QA (2026-03-16 버그 수정)

### 수정 내용
- `MatchmakerManager.DetermineIsHostAsync`: `GetHashCode()` 크로스-프로세스 비결정성 → `GetStableHash()` polynomial hash로 교체
- `NetworkGameManager.HostGameAsync`: `OnClientConnectedCallback` 등록 순서 수정 (StartNetworkHost 이전으로)

### 테스트 체크리스트
- [x] 두 기기에서 동시에 랜덤 매칭 → 한 쪽 Host, 다른 쪽 Client로 역할 분리 확인
- [x] Game 씬으로 양쪽 정상 전환 확인
- [ ] 반복 매칭 테스트 — Host/Client 역할이 번갈아 바뀌는지 확인 (미완료)
- [ ] 취소 후 재매칭 — 정상 동작 확인 (미완료)

### 알려진 취약 지점
- `GetStableHash()`는 polynomial hash (seed=17, multiplier=31) — 극히 드물게 두 MatchId가 동일 hostIndex 산출 가능
- `OnClientConnectedCallback` 등록 전 Client가 순간적으로 접속하는 레이스 컨디션은 현실적으로 발생 불가

---

## 종족 인게임 적용 QA (2026-04-07 완료)

### 최종 판정: PASS (SINGLE-01~06, MULTI-01 실기 통과)

### 정적 분석 발견 사항
- `_bluePrefabs` / `_redPrefabs` 필드명이 BuildingFactory에도 동일하게 존재 → Grep 시 클래스명 함께 확인 필수
- Inspector 필드 재연결이 필요한 작업은 에디터 설정 스크립트(`Hexiege/Setup/...`) 실행 여부 반드시 확인
- BattleViewModel + GameBootstrapper 양쪽에서 GameRaceContext.Set() 중복 호출 — 동작 오류 없음 (Minor 잔존)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/Testcase.md`

---

## 전투 거리 정밀도 QA (2026-03-02 수정, QA 완료)

### IEntityPositionProvider 기반 월드좌표 거리 체크
- 수정 파일: `UnitCombatUseCase.cs`, `UnitView.cs`, `UnitFactory.cs`, `GameBootstrapper.cs`
- 신규 파일: `Application/Interfaces/IEntityPositionProvider.cs`, `Infrastructure/UnitWorldPositionProvider.cs`
- AttackRange=1 기준 maxDist = 1 × 0.866 + 0.1f = 0.966 world units

### [Critical] tileWorldDist 하드코딩 주의 (미수정)
- GameBootstrapper.cs 474번 줄: `_positionProvider, 0.866f` — 하드코딩
- Inspector 값 변경 시 사거리 판정 오류 재발 → `_config.FlatTop.TileHeight` 사용 권장

### [Warning] FindFirstEnemyTarget 알려진 취약점
- `_unitSpawn.Units.Values` 직접 순회 중 RemoveUnit 호출 시 InvalidOperationException 가능
- 유닛(월드거리²) vs 건물(헥스거리²) 혼합 최근접 비교 — 단위 불일치 (단일 타겟만 있을 때는 무관)

### [Warning] NetworkContext 설정 타이밍 위험
- `UnitView.SetDependencies()` 호출 시점에 NetworkContext.IsNetworkActive가 false이면 싱글플레이 이벤트 구독 등록됨
- NetworkCombatController.OnNetworkSpawn()이 SetDependencies 이전에 실행되어야 안전

---

## 공격 방향 정밀도 QA (2026-03-02 리팩터링 완료)

### 변경 사항
- `FacingDirection.cs`: ArtDirection/FacingInfo/FromHexDirection 제거 (2D 레거시 완전 제거)
- `UnitCombatUseCase.TryAttack()`: bool → IDamageable 반환형 변경
- `UnitView`: ApplyAttackRotation(HexCoord) 추가 — 타겟 월드벡터 → Atan2 → Y 회전 직접 계산
- `UnitView._meshYOffset`: SerializeField (Pistoleer 프리팹에서 30으로 설정 필요)

### Inspector 설정 필수 사항
- **Unit_Pistoleer 프리팹**: UnitView 컴포넌트 → `_meshYOffset = 30`
- 다른 유닛 추가 시: 각 프리팹에서 mesh child 로컬 Y 회전 확인 후 _meshYOffset 설정

### 근본 원인 (해결됨)
- 이전 CalcViewDirection이 타겟의 Lerp 중 transform.position 사용
- 섹터 경계(30°, 90°, 150°...)가 이웃 헥스 방향과 일치 → 작은 각도 변화로 방향 뒤집힘
- 해결: 도메인 헥스 좌표 → HexMetrics.HexToWorldUnit → 월드벡터 → Atan2 직접 계산

---

# MEMORY.md 2차 이관본 (2026-08-24) — QA 기록 18건

> 아래 18건은 `qa-tester/MEMORY.md` 에 쌓여 있던 **지나간 QA 기록**이다(2026-04-07 ~ 2026-07-20).
> `MEMORY.md` 는 매 작업마다 읽는 파일이라 「매번 필요한 규칙·체크리스트」만 남기고 이력은 여기로 옮겼다
> (근거: `.claude/MEMORY.md` 「🔴 에이전트 메모리 갱신 규칙」 4).
> **본문은 한 글자도 고치지 않은 원문 그대로이며 날짜 역순으로 정렬했다.**
> ⚠️ 「종족 인게임 적용 QA (2026-04-07 완료)」는 이 파일 위쪽에 이미 같은 내용이 있다(소제목만 「정적 분석 발견 사항」 ↔ 「핵심 발견 사항」으로 다름).
> 어느 쪽이 틀렸다고 확인된 바 없어 **지우지 않고 양쪽 다 남긴다.**

### 2026-07-20 - InfernoSpirit(지옥불 정령) 단일 대상 DoT QA (정적 분석, PASS)
- 검증 범위: InfernoAttackBehavior(신규), SpecialAttackRegistry 등록, SpecialAttackContext.ApplyInfernoDot 델리게이트,
  UnitCombatUseCase(_infernoDotPerSecond/_infernoDotDuration/ApplyInfernoDot), SpecialAttackConfig(inferno 필드),
  GameBootstrapper.Setup.cs 주입. 이슈 0건 — 전 항목 PASS.
- **MushroomBomber 원형에서 "AoE→단일 대상"으로 축소할 때의 정합성 패턴(향후 QuakeSpirit 등 재확인 기준)**:
  BlastAttackBehavior는 반경 수집(`CollectEnemyUnitsInRadius`) 후 각 대상에 `ctx.ApplyDot` 호출, InfernoAttackBehavior는
  반경 수집 코드 자체를 없애고 `ctx.PrimaryTarget as UnitData` 캐스팅 1회로 대체 — 구조적으로 안전(반경 로직 잔존 없음 확인 완료).
- **유닛별 DoT 값 분리 검증 패턴**: 같은 `TimedEffectKind.Damage`를 공유하는 두 유닛(MushroomBomber 2/3, InfernoSpirit 5/3)이
  "같은 파이프라인, 다른 값"을 안전하게 공유하는 근거 — `ApplyDamageOverTime(source, target, perSecond, duration, tickInterval)`가
  호출 시점 인자로 totalAmount/tickAmount를 계산해 레코드에 저장하므로 전역 필드 공유가 없다. 델리게이트도
  `ApplyBlastDot`/`ApplyInfernoDot`으로 완전히 분리(같은 시그니처 `Action<UnitData,UnitData>`를 공유 델리게이트 1개로
  합치지 않고 SpecialAttackContext에 별도 필드 `_applyDot`/`_applyInfernoDot`로 이원화) — 값 회귀 위험을 코드 구조로 원천 차단.
  향후 세 번째 DoT 유닛이 추가되면 이 "값별 델리게이트 분리" 패턴이 유지되는지 확인할 것(공용 델리게이트에 유닛별 값을
  런타임 파라미터로 넘기는 방식으로 리팩터링되면 그 시점에 값 혼입 여부를 재검증해야 함).
- **건물 제외 확인 근거**: `ctx.PrimaryTarget as UnitData`가 건물(BuildingData)이면 null → 캐스팅 실패로 자연 제외
  (별도 `is BuildingData` 분기 불필요, 데이터 흐름상 자동 보장 — MushroomBomber의 "Units만 순회" 방식과 다른 축소판 구현이지만
  결과는 동일하게 안전).
- **사망 시 DoT 스킵 근거**: `UnitData.IsAlive`가 `Hp > 0` 계산 프로퍼티라 `ApplyDamageToVictim`(직접 25) 직후 즉시 반영됨 —
  타이밍 갭 없이 `!victim.IsAlive` 가드가 정확히 작동.
- **단일 히트 프레임 확인**: `UnitStatsConfig.asset`(unitType 12) `hitFrameTimes: [1.15]` 1개뿐 + `attackCooldown: 3` →
  공격 사이클당 `ExecuteAttack` 1회만 호출되므로 DoT 갱신(리셋)이 3초 주기당 1회만 발생, 총량(15) 도달 전 재갱신 없음(설계 의도와 일치).
- **이중 틱 가드 재확인**: `TickTimedEffects` 호출부가 싱글(`GameBootstrapper.cs:443`, `!IsNetworkMode` 가드)·멀티
  (`NetworkCombatController.cs:300`, `IsServer` 가드 이후) 단 두 곳뿐 — InfernoSpirit 추가로 신규 호출부 생기지 않음(공용 로직 재사용).
- **레이어 규칙 재확인**: `InfernoAttackBehavior.cs`는 `using Hexiege.Domain;`만 사용(Infrastructure 미참조),
  `UnitCombatUseCase.cs`도 `using Hexiege.Domain;`만(SO는 GameBootstrapper가 float로 변환해 생성자 인자로 주입).
- task: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/` (파트 1 DoT만 — 공격 방향 버그는 별도 보류 범위)

### 2026-07-19 - MushroomBomber(버섯폭격기) 착탄형 DoT QA (정적 분석, PASS)
- 검증 범위: BlastAttackBehavior(신규), SpecialAttackRegistry/Context, UnitCombatUseCase의 DoT 초단위 틱 모드
  (ActiveTimedEffect.TickInterval/TickAccumulator, TickDiscreteDamageEffect, ApplyOneDamageTick), SpecialAttackConfig
  blast 필드, GameBootstrapper.Setup.cs 주입, UnitStatsConfig(26), 에디터 스크립트 2종. 이슈 0건 — 전 항목 PASS.
- **DoT 초단위 틱 시스템의 정확성 보장 메커니즘(향후 InfernoSpirit/QuakeSpirit도 재사용 — 반드시 재확인)**:
  `TickInterval>0`(discrete DoT) vs `TickInterval<=0`(연속 HoT diff)로 완전히 분기(같은 `TickTimedEffects` 루프
  안에서 `if (effect.TickInterval > 0.0001f)`로 조기 분기 후 `continue`) — 힐 경로 회귀 없음.
  discrete 틱은 "따라잡기 while 루프"(저프레임 대응) + "만료 시 잔여 1틱 강제 정산"(고프레임/누락 대응) 이중 안전망으로
  프레임레이트 무관 총량 정확 보장. `ApplyOneDamageTick`이 `Min(TickAmount, remaining)`로 클램프하므로
  총량 초과 불가능 — 이 두 안전망 존재 여부를 신규 DoT 유닛 QA 시 항상 확인.
- **DoT 텍스트가 OnAttackHit 애니 이벤트 미주입과 무관하게 정상 동작하는 이유**: `ApplyTimedDamageToUnit`이
  DoT 매틱마다 `EntityDamagedEvent(immediatePresentation: true)`를 발행 → `HitPresentationQueue.OnEntityDamaged`가
  `evt.ImmediatePresentation` 체크로 큐잉을 완전히 우회하고 즉시 `Emit()`(규칙 26 확장, 파도와 동일 경로).
  따라서 "매초 텍스트" 요구사항은 Attack 클립의 `OnAttackHit` 이벤트 주입 여부와 **무관**하게 항상 충족된다
  (직접 단일 피해 쪽만 OnAttackHit 타이밍에 의존 — 미주입 시 타임아웃 안전망(쿨다운×1.5)까지 지연되나 이는
  "연출 타이밍" 문제이지 DoT 로직 문제 아님. 향후 이런 착탄형 유닛 QA 시 "OnAttackHit 미주입 = 직접타격 텍스트만 지연,
  DoT/파도류 즉시연출 텍스트는 영향 없음"을 구분해서 판단할 것).
- **에디터 스크립트 필드명 검증 패턴 확인(기존 패턴 재확인)**: `RegisterMushroomBomberPrefabs.cs`/`WireFloraProductionLine.cs`
  모두 `SerializedProperty.FindPropertyRelative` 대상 필드명(`type`/`blue`/`red`, `buildingType`/`blueUnits`/`redUnits`/
  `type`/`portrait`/`requiredStage`)이 실제 struct 정의(`UnitFactory.UnitPrefabEntry`, `ProductionPanelUI.
  BuildingUnitMapping`/`UnitPortraitEntry`)와 정확히 일치함을 grep으로 대조 완료. 멱등성(이미 있으면 no-op, 값
  다르면 보정)도 코드상 확인됨 — 다만 Unity 에디터 부재로 실제 메뉴 실행/씬 반영 여부는 미검증(에디터 작업 영역,
  task 지시상 범위 외).
- **직접 10 + DoT 이중 적용/사망 시 배제 로직**: `ExecuteAttack`이 `ApplyDamageToVictim`(직접, 죽으면 `_unitSpawn`에서
  즉시 제거) 실행 후 `special.Apply(ctx)` 호출 — `BlastAttackBehavior`는 `ctx.Units.Values`(생존자만)를 순회하므로
  직접타격으로 죽은 주 타깃은 자연스럽게 DoT 수집에서 제외됨(별도 방어 코드 불필요, 데이터 흐름상 자동 보장).
- task: `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/`

### 2026-07-18 - Email verification flow QA result

- User confirmed PASS for signup email display, signup cancel popup, Firebase unverified user deletion, continue verification staying on screen, relaunch from verification returning to verification, and relaunch from nickname setup returning to nickname setup.
- Regression focus if revisited: verified complete button path, existing unverified-login back sign-out path, and long-term stale unverified account cleanup remain policy/test follow-ups rather than current blockers.

### 2026-07-17 - TorrentSpirit 파도 AoE + 힐 서브시스템 QA (정적 분석, CONDITIONAL PASS)
- 로직/이벤트/서버권위/레이어 규칙: 전부 PASS (TickWaves 이중 틱 없음, 힐 멀티 동기화 정상, Domain 순수성 유지).
- **BUG-001급 발견 — "코드는 맞는데 데이터가 안 채워짐" 패턴**: UnitStatsConfig.asset에 unitType 18 항목 없음(범용 폴백로 동작) +
  TorrentSpirit_Attack.anim에 OnAttackHit 이벤트 미주입(규칙 27) + UnitEffectConfig.asset의 attackPreset이 fileID 0(고아 VFX 프리팹).
  신규 유닛 QA 시 항상 이 3종 데이터 배선을 grep으로 직접 확인할 것 — 상세 검사 루틴은 patterns.md 참조.
- **구조적 버그 발견**: ReplacesPrimaryAttack=true(special-only) 유닛은 주 타깃이 건물이면 그 공격 사이클에 피해가 전혀 발생하지 않음
  (주 타깃 단일피해 스킵 + AoE가 유닛만 순회). Castle 파괴가 승리조건인데 해당 유닛은 건물을 절대 못 부숨 — Major.
- 상세: patterns.md "세션: 2026-07-17" 참조. task: `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/`

### 2026-07-16 - Profile/Ranking Cloud UI QA notes

- User confirmed email sign-up reaches nickname setup after verification path changes.
- User confirmed Profile tab click path and ProfileView enable path with temporary debug logs; debug logs were removed afterward.
- Ranking/Profile/NicknameChangePopup UI is functionally visible; final fine layout tuning remains manual Inspector work.
- Regression focus for next pass: Unity console compile errors, Profile tab refresh, Ranking tab refresh/empty state, nickname change validation, and email verification abandonment.

### 2026-07-16 - Email verification flow cleanup QA

- Required checks:
  - signup verification screen shows the typed email instead of the placeholder.
  - signup verification back -> confirm -> Firebase unverified user deleted.
  - signup verification back -> continue verification -> stays on screen.
  - existing unverified email login back -> Firebase sign-out and previous login panel, account remains.
  - verified first login still routes through nickname setup.

## BlockingOverlay UIManager 통합 QA (2026-06-21) — CONDITIONAL PASS

### 종합 판정: CONDITIONAL PASS (Inspector 연결 미확인)

### 핵심 구현 패턴 (이후 유사 작업 참조)
- UIManager 단일 소유: `_blockingOverlay(CanvasGroup)` + `_blockingOverlayButton(Button)`
- 참조 카운터(`_blockingOverlayRefCount`): 중첩 Show 지원, HideOverlay가 0이 될 때만 실제 숨김
- 두 모드: Modal(onTap=null) vs Popup(onTap=콜백), Button.onClick으로 분기
- RematchRequestPopup 전용 패턴: `_overlayShown` bool로 ShowOverlayOnce/HideOverlayOnce — 팝업 전환 시 중복 +1 방지

### 발견된 잠재 문제 (Minor)
- BUG-001: `BuildingPlacementUI.Show()`에서 `_popup?.Show()` 이후 `UIManager.Instance?.ShowBlockingOverlay(Close)` 호출 순서 — 팝업이 먼저 나타난 직후 오버레이 표시. `InGameSettingsUI.Show()`는 반대 순서(오버레이 먼저). 기능 차이는 없으나 순서 불일치.
- BUG-002: `BuildingPanelBase.Show()`도 `_popup?.Show()` 이후 `ShowBlockingOverlay` 호출. `ConfirmPopup.Show()`와 `InGameSettingsUI.Show()`와 순서 불일치.
- BUG-003: `SharedBackgroundButton.cs` 파일 자체는 테스트 통과 전이므로 아직 존재(정상). 그러나 Lobby씬/Game씬 프리팹에서 SharedBackgroundButton 컴포넌트가 완전히 제거됐는지는 Inspector 레벨에서만 확인 가능.
- `BuildingPlacementUI.cs` line 468 주석이 과거 SharedBackgroundButton 언급 — 코드 주석 일관성 Minor 이슈.

### ShowBlockingOverlay 호출 순서 패턴 (확인 필요)
- 권장 순서: ShowBlockingOverlay 먼저 → 팝업 패널 Show. (오버레이가 먼저 표시돼야 팝업 아래에 정확히 배치됨)
- ConfirmPopup, InGameSettingsUI: 오버레이 먼저 호출 (올바름)
- BuildingPlacementUI, BuildingPanelBase: 팝업 Show 먼저 → 오버레이 나중 (Minor 불일치)

### 에디터 스크립트 주의점
- `MigrateBlockingOverlayToUIManager.cs`: Login.unity만 BlockingOverlay 생성, Game.unity는 RematchOverlay RaycastTarget만 비활성화
- `SetSiblingIndex(0)`: BlockingOverlay를 SafeAreaContainer보다 Hierarchy 위에 배치 → 팝업보다 뒤에 렌더링 (올바름)

### task 문서
분석 대상: `Assets/_Project/Docs/_Tasks/` 내 해당 날짜 폴더

## 전역 UI 시스템 QA (2026-06-18) ✅ 완료

### 종합 판정: PASS (수정 후)

### 발견 및 수정된 버그
- **[Major, 수정완료] 규칙 1 위반**: `SetupUIManagerInScene.cs` `CanvasScaler.matchWidthOrHeight = 1f` → `0f` (가로 기준). UIManager Canvas / SplashOverlay Canvas 모두 해당.
- **[Major, 수정완료] 규칙 4 위반**: SplashOverlay에 SafeAreaContainer 없어 StatusText/TapToStartText가 노치 영역에 가려질 수 있음 → SafeAreaContainer + SafeAreaFitter 추가, 텍스트를 그 안으로 이동. Background는 전체화면 요소이므로 SafeAreaContainer 밖(SplashOverlay 직속) 유지.
- **[Major, 수정완료] 규칙 6 위반**: `SetupUIManagerInScene.cs`에서 에디터 스크립트로 생성하는 TextMeshProUGUI(StatusText/TapToStartText)에 Maplestory Light SDF 폰트 미적용 → `AssetDatabase.LoadAssetAtPath<TMP_FontAsset>` 로드 후 명시 적용.

### Minor (미수정)
- `LoginBootstrapper.cs:116` 주석에서 `(규칙 5)` 참조 부정확 — null-safe 패턴 설명인데 CanvasGroup 규칙 번호 기재됨. 기능 무영향.
- `BattleViewModel.cs:222` `Task.Delay(2000)` 고정 2초 지연 — 씬 로딩 완료 타이밍과 무관한 하드코딩. 이번 작업 범위 외.

### 핵심 교훈
- 에디터 스크립트로 UI를 생성할 때 **폰트(규칙 6)** 와 **SafeAreaContainer(규칙 4)** 는 반드시 명시 설정
- `matchWidthOrHeight`는 프로젝트 규칙 상 **0f(가로 기준)** 고정

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-16/12_25_global-ui-system/Testcase.md`

## Login UI CanvasGroup 전환 + NetworkErrorPopup 분리 QA (2026-06-11)

### 종합 판정: CONDITIONAL PASS

### 핵심 발견 사항
- **[Rule 5 준수 확인]** `LoginRootView.cs` 전체에서 `GameObject.SetActive()` 패널 전환 호출 없음. `ShowGroup()`/`HideGroup()`으로 alpha/blocksRaycasts/interactable 3속성 모두 처리. 완전 준수.
- **[Inspector 공유 이슈 해소]** `_confirmPopup`(종료 팝업)과 `_networkErrorPopup`이 별도 오브젝트로 분리. LoginUiSetup 에디터 스크립트로 자동 생성/연결.
- **[Minor Bug-1]** `LoginUiSetup.cs`: `SafeAreaContainer` 미발견 시 NetworkErrorPopup이 씬 루트에 생성된 채 저장됨 — `LogWarning`만 출력, `DisplayDialog` 미사용 (186-190행).
- **[Minor Bug-2]** `ShowNetworkErrorPopup()`이 `cancelLabel: string.Empty` 전달하지만 `ConfirmPopup.Show()`는 취소 버튼을 숨기지 않아 빈 버튼 노출 가능성 (`ConfirmPopup.cs` 175-179행).
- **[LoginBootstrapper 호환]** CanvasGroup 타입 변경(GameObject → CanvasGroup)에 대해 LoginBootstrapper가 패널 슬롯 직접 참조 없음 — 영향 없음.
- **[ConfirmPopup Show() 시그니처]** 5개 인자 모두 일치. LoginRootView 양쪽 호출부 모두 정상.

### ConfirmPopup 재사용 패턴 주의
- `cancelLabel: string.Empty` 전달 시 취소 버튼이 빈 텍스트로 노출됨 (버튼 숨김 로직 없음)
- 단독 확인 버튼만 필요한 팝업은 취소 버튼을 Inspector에서 비활성화하거나, ConfirmPopup에 `cancelLabel` 빈 문자열 시 버튼 숨김 로직 추가 필요

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-11/00_31_login-ui-canvasgroup-popup-fix/Testcase.md`

## Login UI 완성도 QA 정적 분석 (2026-06-11)

### 종합 판정: FAIL (CanvasGroup Rule 5 위반)

### 핵심 발견 사항
- **[Rule 5 위반]** LoginRootView.SetActivePanel() / HideAll()에서 5개 패널 전환에 `SetActive()` 직접 사용 → CanvasGroup 패턴으로 교체 필요
- **[Inspector 공유 이슈]** `_confirmPopup`(종료 팝업) 과 `_networkErrorPopup`이 동일한 ConfirmPopup 오브젝트(fileID: 422375806) 참조 → 두 팝업이 동시에 호출되면 Show() 재호출로 메시지/콜백 덮어쓰기 발생 가능 (Major)
- **[Safe Area Rule 4]** Background가 Canvas 직속 자식으로 SafeAreaContainer 밖에 배치됨 → PASS
- **[Inspector 연결]** LoginBootstrapper 7개 View 슬롯 전부 연결됨, `_loadingIndicator` 연결됨
- `_headerText: {fileID: 0}` — null 허용(코드에서 Optional 처리됨), 문제 없음
- AnonymousWarningPopup의 `_blockingOverlay.SetActive()` — Lobby 씬 별도 점검 예정 항목. `_panel.gameObject.SetActive(true)` 는 이미 제거됨(2026-06-15).

### Login 씬 계층 구조 (확인 완료)
- Canvas → [Background (Canvas 직속), SafeAreaContainer (Canvas 직속)]
- SafeAreaContainer → [LoginRootView, ConfirmPopup, LoadingIndicator, AnonymousWarningPopup]
- LoginRootView → [LoginSelectPanel, EmailLoginPanel, SignUpPanel, EmailVerifyPanel, PasswordResetPanel]

### task 문서
정적 분석만 수행 (플레이모드 실기 불가 — Firebase 미설정)

## AI 시나리오 ScriptableObject 개편 QA (2026-06-10 → 2차 완료 2026-06-11)

### 종합 판정: CONDITIONAL PASS (정적 분석 전항목 PASS, 실기 대기)

### 1차 QA (2026-06-10) 핵심 발견 사항
- BuildOrderStep/AIActionType이 Infrastructure에 있어 Application→Infrastructure 직접 의존 발생
  → 2차 작업에서 Domain/AI 레이어로 이동하여 해소
- DifficultyLevel도 Infrastructure에 있어 BuildOrderStep.GetDelaySeconds()로 인한 연쇄 위반
  → DifficultyLevel도 Domain으로 함께 이동하여 해소

### 2차 QA (2026-06-11) 핵심 확인 사항
- BuildOrderStep, AIActionType, DifficultyLevel 모두 Hexiege.Domain 네임스페이스로 이동 완료
- 세 타입 모두 UnityEngine 의존 없음 ([Serializable]은 System.Serializable — 허용)
- LoadScenarioBundleForRace(): 3종족 switch 완비, null/빈배열 방어 코드 존재
- 레거시 Human_A/B/C.asset 실물 삭제 완료 (Glob 결과 0건)
- 에셋 YAML: 3종족 9개 시나리오 모두 actionType(0~2), phaseIndex(0~3) 범위 내
- NOTE-001(Minor): AIScenarioConfig.cs 주석/XML doc에 구버전 경로 예시 잔존 — 기능 무영향

### AI 시나리오 현행 구조
- 종족별 단일 에셋: Human/Spirit/Transcendence 각 1개
- 각 에셋에 3개 ScenarioBundle 내장
- Human: Rush/Tech/Balance, Spirit: Inferno/Torrent/Quake, Transcendence: Rush/Flora/Beast
- 종족 결정: GameRaceContext.RedRace (AI는 항상 Red팀)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-06-10/01_06_ai-scenario-scriptableobject-restructure/`

## 사운드 시스템 QA 정적 분석 (2026-06-10) — PASS

task 문서: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/`

정적 분석 결과: 전체 PASS (FAIL 0건)
- 규칙 1~22 전체 준수 확인
- 아키텍처 의존성 방향 준수 (SoundConfig=Infrastructure, AudioManager=Presentation)
- VFX+SFX 쌍 호출 3곳 모두 확인 (UnitView×2, NetworkUnit×1)
- BGM 크로스페이드 로직, SFX 풀, 볼륨 dB 변환 정상

QA 수정 사항 (3건):
- ReturnSfxAfterPlay: WaitForSeconds → WaitForSecondsRealtime (timeScale=0 대응)
- Initialize() 재호출 시 SFX 풀 중복 생성 방지 코드 추가
- 무음 전환 후 _activeBgmSource = fadeIn으로 상태 명확화

실기 테스트: 완료 (2026-07-08) — 실기에서 버그 3종 발견 및 수정 (아래 참조)
TC 문서: `Assets/_Project/Docs/_Tasks/2026-06-10/09_28_sound-system/Testcase.md`

### 실기 버그 3종 (2026-07-08 수정 완료)
- **BUG-1 BGM 씬 전환 시 겹침**: `StartCrossfade()`가 `StopCoroutine`만 하고 페이드아웃 중이던 AudioSource를 `Stop()`하지 않아 이전 BGM이 계속 재생. → stale 채널 즉시 Stop으로 수정. **사운드/오디오 QA 시 크로스페이드 중단 경로에서 이전 채널의 AudioSource가 확실히 정지되는지 확인할 것.**
- **BUG-2 볼륨 UI 규칙 위반**: 에디터 스크립트 생성 슬라이더의 고정 픽셀값(규칙 2 위반) + TMP 폰트 미지정(규칙 6 위반). → 앵커 비율 + Maplestory Bold SDF + `EditorUtility.SetDirty()`. **에디터 스크립트로 UI 생성하는 작업 QA 시 규칙 2(앵커)/규칙 6(폰트) + SetDirty 호출 여부 점검(전역 UI 시스템 QA 2026-06-18과 동일 패턴).**
- **BUG-3 SFX 볼륨 미작동**: Exposed Parameter 이름은 정상이었음(추정 오류). `SetFloat` 실패 감지 로깅 추가. **AudioMixer.SetFloat은 실패 시 조용히 false 반환 → 반환값 확인 로직 유무 점검.**
- task: `_Tasks/2026-07-07/12_28_sound-system-bugfix/`

## 게임 화면 UI TC 작성 (2026-05-26) — 실기 테스트 대기 중

### 총 62개 TC 작성 완료 (실기 미완)

### 커버 범위
- GameHudUI (7개): 골드/인구수/킬카운트 표시, 공성 게이지, 설정 버튼
- BuildingPlacementUI (9개): 패널 표시/숨김, 건설 비용 갱신, 실패 피드백
- ProductionPanelUI (22개): 수동/자동 생산, 큐 조작, 업그레이드, 랠리포인트
- BuildingActionPanelUI (5개): 채굴소/타워 액션 패널
- InGameSettingsUI (9개): 일시정지, 포기 확인, 설정 패널
- GameEndUI (7개): 승패 표시, 재경기, 로비 복귀
- 공통 (3개): 레이아웃, Safe Area

### 정적 분석 발견 사항
- `ProductionPanelUI.UpdateUpgradeButton()`에서 `_upgradeButton.gameObject.SetActive(...)` 1건 — Minor, 기능 영향 없음, Rule 5 일관성 정리 권장

### task 문서
`Assets/_Project/Docs/_Tasks/2026-05-26/game-ui-tc/Testcase.md`

## 로비 SetActive→CanvasGroup 전환 실기 테스트 (2026-05-25~27) ✅ 완료

### 판정: TC-SINGLE-001~014 전체 PASS / TC-SINGLE-015~016 SKIP (로그인 미구현)

### 핵심 발견 사항
- 로비 7개 뷰 모두 `CanvasGroup.alpha/blocksRaycasts/interactable` 패턴으로 전환 완료 + 실기 통과
- `ProductionPanelUI.UpdateUpgradeButton()`에 `SetActive` 1건 잔존 (인게임 UI, 로비 작업 범위 밖, Minor — 향후 일관성 정리 권장)
- TC-SINGLE-009/010 (방 만들기 에러 메시지): 초기 TC가 잘못된 전제(코드 입력란/확인 버튼 존재)로 작성됨 → 실제 구현(네트워크 오류 시 ErrorMessage 표시, StartHosting 재시도 시 초기화) 기준으로 수정 후 통과
- TC-SINGLE-015/016 (프로필 로그인): Firebase Auth 구현은 존재하나 프로젝트에서 미사용 상태 → 추후 로그인 기능 활성화 시 재테스트

### task 문서
`Assets/_Project/Docs/_Tasks/2026-05-25/ui-rules-inspection/Testcase.md`

## 이동/전투 재설계 QA (2026-04-30 정적 분석 완료, 실기 미완)

### 정적 분석 판정
- BUG-001 (Critical, FAIL): GameBootstrapper.SetupProduction()에서 TileMoveSlotManager를 UnitFactory에 미전달 → UnitFactory.CreateUnitObject()에서도 UnitView에 미전달. 슬롯 분산 전체 불작동.
- BUG-002 (Major, FAIL): ResumeFromForwardTile()에서 _unitData.Position은 forwardTile로 갱신되지만 transform.position은 공격 슬롯 위치 그대로 → RunTileTraversal 재진입 시 Lerp 출발점 불일치로 순간이동 발생.
- BUG-003 (Major, CONDITIONAL PASS): FindForwardAvailable 대기 루프 — 전방 타일 모두 가득 찬 교착 상황에서 무한 대기. 설계 의도(대기)와 일치하지만 교착 가능성은 실기 확인 필요.
- BUG-004 (Minor, CONDITIONAL PASS): ClaimByApproach fallback 무제한 — MaxUnitsPerSlot 실제 차단 미구현. 대규모 전투 실기 확인 후 정책 결정.

### 핵심 패턴 — 신규 매니저 추가 시 3곳 와이어링 필수
1. GameBootstrapper.CreateUseCases() 에서 인스턴스 생성
2. GameBootstrapper.SetupProduction() 에서 UnitFactory.SetDependencyReferences() 호출에 인자 추가
3. UnitFactory.CreateUnitObject() / InitializeUnitView() 에서 unitView.SetDependencies() 호출에 인자 추가

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-30/02_29_movement-combat-redesign/Testcase.md`
`Assets/_Project/Docs/_Logs/2026-04-30/02_29_movement-combat-redesign/Log.md`

## 생산 패널 전면 재작성 QA (2026-04-19) ✅ 실기 완료

### 최종 판정: PASS (TC-001~018 전체)

### 핵심 발견 사항
- 구 구조(ManualQueue/AutoEntries/AutoEntry/AutoIndex/CurrentProducingIsAuto/isNormalAutoState) 모두 제거됨.
- IsAutoMode 읽기 전용 프로퍼티 (`AutoTypes.Count > 0`) — 직접 대입 시도 없음.
- SyncQueueStateClientRpc 파라미터 모두 int/bool 기본 타입 — NGO 직렬화 정상.
- CancelAutoTypeIfNeeded 추가: 슬롯 클릭으로 자동 항목 취소 시 AutoTypes에서도 제거 + Rule 2 처리.
- wasAuto 캡처는 state.CurrentIsAuto=false 초기화 이전 라인에서 반드시 수행해야 함.

### 생산 큐 새 구조 핵심 (이후 QA 재검증 시 기준)
- QueueSlot { Type, IsAuto, IsCharged } — 단일 구조체
- PendingQueue[0]=슬롯1, PendingQueue[1]=슬롯2 불변식
- IsCharged=false: 자동 항목 대기 중 (골드 미차감), ChargeVisibleSlots가 슬롯 진입 시 차감
- ChargeVisibleSlots 호출 시점: TryStartNext 직후, CancelQueueAt(1/2) 직후, CompleteProduction 직후
- 슬롯 클릭 취소: 자동 항목이면 CancelAutoTypeIfNeeded도 호출

### 미해결 이슈
- ~~TC-008 관련: 큐 비어있을 때 자동 등록 시 슬롯1에 1프레임 깜빡임~~ → **수정 완료 (2026-04-19)** `ToggleAutoProduction`에서 `!CurrentProducing.HasValue`이면 즉시 `TryStartNext` 호출로 해결

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-19/production-panel-rewrite/Testcase.md`

## 스탯 적용 + 부유 HP 텍스트 QA (2026-04-12~13 완료)

### 최종 판정: PASS (전 항목 실기 통과)

### 핵심 발견 사항
- `SyncHealthClientRpc` 클라이언트에서 `GameEvents.OnEntityDamaged` 미발행 → FloatingHpTextSpawner 미반응 버그 발견. NetworkHealthSync에서 TakeDamage 후 이벤트 재발행으로 수정.
- `Camera.main` 매 이벤트 탐색 비용 → Initialize()에서 캐싱으로 수정.
- `_prefab` null 체크 누락 → Initialize() 진입부 null 가드 추가.
- `ReturnToPool`에서 SetActive(false) 중복 (FloatingHpText OnComplete에서도 수행) — 기능 영향 없음.
- BuildingStats.GetMaxHp 단일 파라미터 버전이 Human으로 위임 — 하위 호환 정상.
- Application 레이어에 GameRaceContext 직접 참조 없음 확인.

### 체크리스트 추가 항목
- [ ] 멀티플레이 전투 중 클라이언트 FloatingHpText 표시 여부 (NetworkHealthSync 재발행 확인)
- [ ] BuildingStats.GetMaxHp 호출부 RaceId 미전달 누락 없는지 신규 건물 추가 시 확인

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-12/06_42_stats-apply/Testcase.md`
`Assets/_Project/Docs/_Tasks/2026-04-12/18_03_floating-hp-text/Testcase.md`

## 종족 인게임 적용 QA (2026-04-07 완료)

### 최종 판정: PASS (SINGLE-01~06, MULTI-01 실기 통과)

### 핵심 발견 사항
- `_bluePrefabs` / `_redPrefabs` 필드명이 BuildingFactory에도 동일하게 존재 → Grep 시 클래스명 함께 확인 필수
- Inspector 필드 재연결이 필요한 작업은 에디터 설정 스크립트(`Hexiege/Setup/...`) 실행 여부 반드시 확인
- BattleViewModel + GameBootstrapper 양쪽에서 GameRaceContext.Set() 중복 호출 — 동작 오류 없음 (Minor 잔존)

### task 문서
`Assets/_Project/Docs/_Tasks/2026-04-07/09_00_faction-ingame-apply/Testcase.md`
