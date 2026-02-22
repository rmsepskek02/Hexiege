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
- [중요] BuildingFactory/UnitFactory 등 Factory 신규 작성 시 sortingOrder 동적 설정 필수 확인
  - 프리팹 기본 sortingOrder는 정적 고정값 → FlatTop 모드에서 렌더링 레이어 오류 발생
  - 올바른 패턴: FlatTop → `ViewConverter.FlatTopSortingOrder(viewPos) + 기준값`, PointyTop → `coord.R + 기준값`
  - sortingOrder 계층: 타일(0~29) < 금광(+1) < 건물(+50) < 유닛(100)

## 터치 입력 구조
- CameraController: EnhancedTouch 기반, OnEnable/OnDisable에서 Enable/Disable
- 팬 vs 탭 구분: InputHandler ClickThreshold=10px, CameraController는 시작부터 팬 추적
- 에디터 터치 팬 테스트 불가: Touchscreen.current == null 조건으로 차단됨 (의도적)
- 2터치 중 팬 비활성화: activeTouches.Count >= 2 가드
- Android: Mouse.current는 null이 아님(Unity가 터치→가상마우스 생성) → Touchscreen.current 사용

## ViewConverter 방식 (확정, SetTeamView 방식 폐기)
- 카메라 Z축 180° 회전(SetTeamView) 방식은 폐기됨 — 스프라이트 뒤집힘 문제
- 채택 방식: ViewConverter.cs (Core 레이어, 정적 클래스)
  - `ToView(pos) = 2*mapCenter - pos` (Red팀: 맵 중심 기준 반전, 자기 역함수)
  - `FlipDirection(dir) = (dir + 3) % 6` (Red팀 이동 방향 반전)
  - `FromView(viewPos) = ToView(viewPos)` (역변환도 동일 공식)
- [수정됨] 올바른 초기화 순서: StartNetworkGame() → ViewConverter.Setup(isRed, mapCenter) → LoadMap()
  - 이전: LoadMap() 후 Setup → 건물/타일 초기 렌더링에서 반전 누락 버그
  - 수정: Setup 후 LoadMap() → 모든 렌더링이 올바른 반전 위치에서 단일 패스로 처리
- 리셋: LoadMap() 내부 isNetworkMode 분기 → 싱글플레이만 Reset(), 네트워크는 건너뜀

## ViewConverter 테스트 체크리스트
- Blue팀: 타일/유닛/건물이 도메인 좌표 그대로 렌더링됨 (반전 없음)
- Red팀: 타일/유닛/건물이 맵 중심 기준 180° 반전된 위치에 렌더링됨
- Red팀 카메라 시작 위치: ToView()로 변환된 Red Castle 위치를 향해야 함
- 입력(터치/클릭): FromView()로 역변환 후 올바른 HexCoord로 변환되는지 확인
  - Blue: 클릭 위치 = 월드 좌표 그대로 → HexMetrics 변환
  - Red: 클릭 위치 → FromView() → 도메인 좌표 → HexMetrics 변환
- 유닛 이동 방향: Red팀에서 FlipDirection() 적용 여부 확인 (NE↔SW, E↔W, SE↔NW)
- 스프라이트 자체는 뒤집히지 않아야 함 (좌우 flipX는 FacingDirection 로직 그대로)
- 싱글플레이: ViewConverter.IsFlipped = false → 반전 없음
- 맵 재시작 시: ViewConverter.Reset() 후 재Setup 되는지 확인
- [추가] 네트워크 게임 시작 시: 건물(Castle/채굴소)이 Red팀에서 반전된 위치에 나타나는지 확인
- [추가] Red팀 카메라 초기 위치 확인: 반전 후 자기 진영(Red Castle)이 화면 하단에 보이는지
- [추가] Red팀 건물 transform.position 오프셋 버그 수정 확인 (2026-02-22):
  - 증상: Castle/MiningPost의 GameObject 위치가 한 칸 이상 틀어짐 (sortingOrder가 아닌 position 문제)
  - 원인: StartNetworkGame()에서 HexMetrics.Orientation 미설정 상태로 GridCenter() 호출
  - 수정: GameBootstrapper.StartNetworkGame()에 HexMetrics 사전 설정 코드 추가
  - 확인 포인트: Red팀 Castle이 실제 배치 타일(row=1) 위에 정확히 렌더링되는지

## 네트워크 QA 체크리스트
- 건물 배치: 서버 검증 후 양쪽에 동일하게 생성되는지
- 유닛 생산: 서버에서 생산, 양쪽 UnitFactory에 동일 ID로 스폰되는지
- 타일 소유권: BroadcastTileChangeClientRpc로 양쪽 색상 일치하는지
- 골드: NetworkVariable로 클라이언트 자동 동기화되는지
- HP: NetworkHealthSync로 양쪽 HP 일치하는지
- 승패: AnnounceWinnerClientRpc로 양쪽 동일 결과 표시되는지

## 참고 파일
- [patterns.md](patterns.md) — 버그 패턴 상세
