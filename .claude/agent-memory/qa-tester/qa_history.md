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
