# QA Tester Memory — Hexiege

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)

## 랜덤 매칭 QA 항목 (2026-03-16 버그 수정)

### 수정 내용
- `MatchmakerManager.DetermineIsHostAsync`: `GetHashCode()` 크로스-프로세스 비결정성 → `GetStableHash()` polynomial hash로 교체
- `NetworkGameManager.HostGameAsync`: `OnClientConnectedCallback` 등록 순서 수정 (StartNetworkHost 이전으로)

### 테스트 체크리스트
- [x] 두 기기에서 동시에 랜덤 매칭 → 한 쪽 Host, 다른 쪽 Client로 역할 분리 확인
- [x] Game 씬으로 양쪽 정상 전환 확인
- [ ] **반복 매칭 테스트** — Host/Client 역할이 번갈아 바뀌는지 확인 (무작위성 검증, 미완료)
- [ ] **취소 후 재매칭** — 정상 동작 확인 (미완료)

### 알려진 취약 지점
- `GetStableHash()`는 polynomial hash (seed=17, multiplier=31) — 극히 드물게 두 MatchId가 동일 hostIndex 산출 가능하지만 무시 가능 수준 (UUID 공간)
- `OnClientConnectedCallback` 등록 전 Client가 순간적으로 접속하는 레이스 컨디션은 현실적으로 발생 불가 (Client는 Relay 참가 + NGO 핸드셰이크에 수 초 소요)

---

## 아키텍처 패턴 (확인된 사항)
- Presentation이 Infrastructure(LocalPlayerTeam) 직접 참조: 정적 홀더 패턴으로 허용 범위
- Assembly Definition 없음 — 물리적 경계 없음, 네임스페이스 규약으로만 관리
- CameraController에 `using Hexiege.Infrastructure` 선언 필요 (GameConfig 사용 목적, 정상)

## 반복 확인 필요 항목
- 신규 UseCase/Manager 추가 시 GameBootstrapper 와이어링 누락 여부
- LocalPlayerTeam.Current 기본값 = Blue → 싱글플레이 동작 항상 확인
- 팀 기반 로직 변경 시 StartAutoMove의 하드코딩 Blue/Red 확인
- ViewConverter.IsFlipped 상태가 올바르게 초기화/리셋 되는지 확인

## 알려진 취약 지점
- FindObjectsByType 사용처: InputHandler.StartAutoMove, InputHandler.HandleClick
  → 유닛 수 증가 시 성능 취약. 캐시 최적화 대상으로 마킹.
- IsPointerOverUI의 Debug.Log — 매 클릭마다 콘솔 출력, 프로덕션 전 제거 필요
- **3D 전환 후**: 신규 Factory 작성 시 Z-depth 기반 배치 확인 (sortingOrder 미사용)
  - 올바른 렌더 순서: 타일(Y=0) < 건물(Y=높이) < 유닛 (카메라에서 멀→가까운 순)

## 터치 입력 구조
- CameraController: EnhancedTouch 기반, OnEnable/OnDisable에서 Enable/Disable
- 팬 vs 탭 구분: InputHandler ClickThreshold=10px, CameraController는 시작부터 팬 추적
- 에디터 터치 팬 테스트 불가: Touchscreen.current == null 조건으로 차단됨 (의도적)
- 2터치 중 팬 비활성화: activeTouches.Count >= 2 가드
- Android: Mouse.current는 null이 아님(Unity가 터치→가상마우스 생성) → Touchscreen.current 사용
- **3D 전환 후**: 입력은 XZ 평면 레이캐스트 방식 (ScreenToXZPlane() 헬퍼)

## ViewConverter 방식 (확정, SetTeamView 방식 폐기)
- 카메라 Z축 180° 회전(SetTeamView) 방식은 폐기됨 — 3D 메시 뒤집힘 문제
- 채택 방식: ViewConverter.cs (Core 레이어, 정적 클래스)
  - `ToView(pos) = 2*mapCenter - pos` (Red팀: 맵 중심 기준 반전, 자기 역함수)
  - `FlipDirection(dir) = (dir + 3) % 6` (Red팀 이동 방향 반전)
  - `FromView(viewPos) = ToView(viewPos)` (역변환도 동일 공식)
  - **3D 전환 후**: Z축 반전 (`2*center.z - pos.z`), Y(높이)는 보존
- [수정됨] 올바른 초기화 순서: StartNetworkGame() → ViewConverter.Setup(isRed, mapCenter) → LoadMap()
- 리셋: LoadMap() 내부 isNetworkMode 분기 → 싱글플레이만 Reset(), 네트워크는 건너뜀

## ViewConverter 테스트 체크리스트
- Blue팀: 타일/유닛/건물이 도메인 좌표 그대로 렌더링됨 (반전 없음)
- Red팀: 타일/유닛/건물이 맵 중심 기준 180° 반전된 위치에 렌더링됨
- Red팀 카메라 시작 위치: ToView()로 변환된 Red Castle 위치를 향해야 함
- 입력(터치/클릭): FromView()로 역변환 후 올바른 HexCoord로 변환되는지 확인
- 유닛 이동 방향: Red팀에서 FlipDirection() 적용 여부 확인 (NE↔SW, E↔W, SE↔NW)
- **3D**: 메시 자체는 뒤집히지 않아야 함 (Y축 회전만 변경)
- 싱글플레이: ViewConverter.IsFlipped = false → 반전 없음
- [추가] 네트워크 게임 시작 시: 건물(Castle/채굴소)이 Red팀에서 반전된 위치에 나타나는지 확인
- [추가] Red팀 건물 transform.position 오프셋 버그 수정 확인 (2026-02-22):
  - 수정: GameBootstrapper.StartNetworkGame()에 HexMetrics 사전 설정 코드 추가

## 네트워크 미완성 항목 QA 체크리스트 (코드 분석, 2026-02-27)
- [ ] BuildFailedClientRpc UI 피드백 없음: 건물 배치 실패 시 사용자 알림 없음
- [ ] EnqueueFailedClientRpc UI 피드백 없음: 유닛 생산 큐 추가 실패 시 동일 문제
- [ ] InputHandler 유닛 이동 네트워크 분기 누락: 멀티플레이에서 탭 이동 시 상대방 화면에 동기화 안 됨
- [ ] 자동생산 멀티플레이 미지원: 롱프레스 시 UI 반응 없음
- [ ] 생산 큐 클라이언트 UI 지연: ProductionStartedClientRpc 받기 전까지 UI 업데이트 없음
- [x] NetworkGameEndController._lobbySceneName 하드코딩 수정: "SampleScene" → "Game" (수정 완료)

## 네트워크 QA 체크리스트
- 건물 배치: 서버 검증 후 양쪽에 동일하게 생성되는지
- 유닛 생산: 서버에서 생산, 양쪽 UnitFactory에 동일 ID로 스폰되는지
- 타일 소유권: BroadcastTileChangeClientRpc로 양쪽 색상 일치하는지
- 골드: NetworkVariable로 클라이언트 자동 동기화되는지
- HP: NetworkHealthSync로 양쪽 HP 일치하는지
- 승패: AnnounceWinnerClientRpc로 양쪽 동일 결과 표시되는지

## 전투 거리 정밀도 QA 항목 (2026-03-02 수정, QA 완료)

### IEntityPositionProvider 기반 월드좌표 거리 체크
- 수정 파일: `UnitCombatUseCase.cs`, `UnitView.cs`, `UnitFactory.cs`, `GameBootstrapper.cs`
- 신규 파일: `Application/Interfaces/IEntityPositionProvider.cs`, `Infrastructure/UnitWorldPositionProvider.cs`
- AttackRange=1 기준 maxDist = 1 × 0.866 + 0.1f = 0.966 world units
- 테스트: 유닛이 시각적으로 인접했을 때(1타일) 공격 발동, 2타일 거리에서는 미발동 확인
- Fallback 테스트: provider 미등록 유닛은 헥스 좌표 기반 체크로 전환되는지 확인
- 멀티플레이 테스트: UnitView.OnDestroy()에서 Unregister 정상 호출 여부 (Dead 유닛 남은 Transform 참조 방지)

### [Critical] tileWorldDist 하드코딩 주의 (미수정)
- GameBootstrapper.cs 474번 줄: `_positionProvider, 0.866f` — 하드코딩
- GameConfig.FlatTop.TileHeight Inspector 값이 0.866이면 문제 없음
- Inspector 값 변경 시 사거리 판정 오류 재발 → `_config.FlatTop.TileHeight` 사용 권장
- 확인 필요: `Assets/_Project/Resources/Config/GameConfig.asset` FlatTop.TileHeight 값

### [Warning] FindFirstEnemyTarget 알려진 취약점
- `_unitSpawn.Units.Values` 직접 순회 중 RemoveUnit 호출 시 InvalidOperationException 가능
  → 향후 List<UnitData> 복사본 기반으로 변경 권장
- 유닛(월드거리²) vs 건물(헥스거리²) 혼합 최근접 비교 — 단위 불일치
  → 단일 타겟만 있을 때는 무관, 다중 타겟 최근접 선택 시 부정확 가능

### [Warning] NetworkContext 설정 타이밍 위험
- `UnitView.SetDependencies()` 호출 시점에 NetworkContext.IsNetworkActive가 false이면
  멀티플레이임에도 싱글플레이 이벤트 구독(OnEntityAttacked)이 등록됨
- NetworkCombatController.OnNetworkSpawn()이 SetDependencies 이전에 실행되어야 안전
- NGO Enable Scene Management=ON 환경에서는 씬 로드 시 자동 스폰 → 일반적으로 안전하나
  타이밍 테스트 필요

### GameConfig 정리 (2026-03-02)
- AnimationFps 필드 제거됨 — 참조 코드 잔재 없음 확인
- TileHeight 코드 기본값 수정: PointyTop 0.82→0.866, FlatTop 0.36→0.866
- FlatTop GridHeight 코드 기본값: 29→20
- CameraZoomDefault: 5→7

## 2D→3D 전환 QA 항목 (2026-02-27 전환 완료)

### 좌표 평면 전환 (XY→XZ) 검증
- HexMetrics.HexToWorld() 결과가 XZ 평면(Y=0)에 올바르게 배치되는지
- InputHandler 레이캐스트가 XZ Plane과 정확히 교차하는지 (잘못 설정 시 클릭 위치 틀어짐)
- ViewConverter.ToView()에서 Z 반전 + Y 보호가 올바르게 적용되는지
  - 검증: Blue팀 ToView(pos).y == pos.y (Y 불변), ToView(pos).z = 2*center.z - pos.z

### 카메라 검증 (Orthographic + 55도 틸트)
- 화면 경계 클램프: XZ 평면 기준으로 카메라가 맵 밖으로 나가지 않는지
- 핀치 줌: orthographicSize 변경 시 시야가 올바르게 변하는지
- 팬: XZ Plane 레이캐스트 팬에서 손가락 아래 맵이 고정되는지 (드래그 느낌)
- 카메라 시작 위치: 55도 틸트 Z오프셋 보정으로 타겟이 화면 중앙에 오는지

### 3D 유닛/건물 검증
- Animator 상태 동기화: 멀티플레이에서 양측 애니메이션 상태 일치 여부
- FlipDirection → Y축 회전 매핑: Red팀 방향 반전이 3D 캐릭터 Y축 회전으로 올바르게 적용되는지
  - HexDirection 0~5 → 각도 매핑 테이블 기준 검증 (E=0°, NE=60°, NW=120°, W=180°, SW=240°, SE=300°)
- FBX Import 스케일 검증: HexMetrics.TileWidth=1.0 기준 캐릭터 크기 비율
- 렌더링 깊이: Z-depth 기준 타일/건물/유닛 레이어링 올바른지 (sortingOrder 미사용)
- Attack 애니메이션 타이밍: Mixamo 클립 길이와 전투 로직 TriggerAttackAnimation() 호환성

## 공격 방향 정밀도 QA 항목 (2026-03-02 리팩터링 완료)

### 변경 사항
- `FacingDirection.cs`: ArtDirection/FacingInfo/FromHexDirection 제거 (2D 레거시 완전 제거)
- `UnitCombatUseCase.TryAttack()`: bool → IDamageable 반환형 변경
- `UnitView`: ApplyAttackRotation(HexCoord) 추가 — 타겟 월드벡터 → Atan2 → Y 회전 직접 계산
- `UnitView._meshYOffset`: SerializeField (Pistoleer 프리팹에서 30으로 설정 필요)
- `NetworkCombatController.TriggerAttackAnimationClientRpc`: targetQ/targetR 파라미터 추가

### Inspector 설정 필수 사항
- **Unit_Pistoleer 프리팹**: UnitView 컴포넌트 → `_meshYOffset = 30` (모델 import 오프셋 보정)
- 다른 유닛 추가 시: 각 프리팹에서 mesh child 로컬 Y 회전 확인 후 _meshYOffset 설정

### QA 체크리스트
- [x] 싱글플레이: 유닛이 인접 적 유닛 공격 시 공격 방향이 정확한지 (어떤 방향에서도) ✅
- [x] 싱글플레이: 유닛이 인접 건물 공격 시 공격 방향이 정확한지 ✅
- [x] 싱글플레이: 타겟이 이동 중일 때 공격 방향이 안정적인지 (Lerp 영향 없음) ✅
- [x] 싱글플레이: 이동 방향은 변경 없이 정상 동작하는지 (DirectionAngles 그대로) ✅
- [x] 멀티플레이: 공격 방향 정상 동작 확인 (_meshYOffset=30 적용) ✅
- [x] 멀티플레이: Red팀 공격 방향이 정확한지 (FlipDirection 적용 확인) ✅ (2026-03-02 확인)
- [x] _meshYOffset 설정 확인: Pistoleer 프리팹 UnitView._meshYOffset = 30 ✅

### 근본 원인 (해결됨)
- 이전 CalcViewDirection이 타겟의 Lerp 중 transform.position을 사용
- 섹터 경계(30°, 90°, 150°...)가 정확히 이웃 헥스 방향과 일치 → 작은 각도 변화로 방향 뒤집힘
- 해결: 도메인 헥스 좌표(타겟.Position) → HexMetrics.HexToWorldUnit → 월드벡터 → Atan2 직접 계산

## 참고 파일
- [patterns.md](patterns.md) — 버그 패턴 상세
