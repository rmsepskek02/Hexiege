# QA Tester Memory — Hexiege

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

## 참고 파일
- [patterns.md](patterns.md) — 버그 패턴 상세
