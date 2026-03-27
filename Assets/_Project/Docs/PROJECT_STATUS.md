# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-03-27
**현재 단계:** 이동 전 회전 타이밍 수정 완료 (Rotate-then-Move, _isPreRotating 플래그로 DOTween-LateUpdate 충돌 해소) / 유닛 NGO NetworkObject 전환 + 이동/전투 동기화 완료 / Game UI Lifecycle Framework 완료 / 반투명 배경 오버레이 구조 개선 완료 / 자동/수동 생산 하이브리드 시스템 완성 / 멀티플레이 Phase 8 완료 / 3D 전환 완료 / 팀별 피아식별 프리팹 에셋 완료 / 신규 유닛 2종(Assault/Sniper) 에셋+코드 연동 완료 / 반응형 팝업 UI 완료 / 팀별 초상화 동적 업데이트 완료 / 공격 애니메이션-타격 동기화 완료 / 유닛 회전 DOTween 보간 완료 / 공격 후 Walk 복귀 버그 수정 완료 / 로비 씬 분리 MVVM 구조 완료 / 랜덤 매칭 게임 씬 전환 버그 수정 완료 / 전역 로딩 스크린 완료 / 멀티플레이 로비 복귀 버그 수정 완료 / 재경기 시스템 완료(커스텀+랜덤) / 건물 인근 이동/공격 버그 수정 완료 / 카메라 줌 DOTween 보간 완료 / UI DOTween 애니메이션 프레임워크 완료 / 코드 정리 완료 / 싱글플레이 ViewConverter 버그 수정 완료

---

## 전체 구현 현황

### ✅ 완료된 시스템

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 | 유닛 이동 시 자동 점령 |
| 금광 타일 시스템 | ✅ 완료 | HasGoldMine, 채굴소 건설 조건 |
| A* 경로탐색 | ✅ 완료 (2026-03-18 갱신) | ClaimedTile 기반 아군 차단 (중간 타일만), 목표 타일 blocked 체크 제거 |
| 카메라 줌 보간 | ✅ 완료 (2026-03-19) | DOTween.To + Ease.OutCubic, _targetZoom 누적, _zoomDuration(0.25f) SerializeField |
| 유닛 이동 (Lerp) | ✅ 완료 | Per-step 가용성 체크, 재탐색 |
| 전투 시스템 | ✅ 완료 | IDamageable, 이동 중 자동 공격 |
| 전투 거리 정밀도 | ✅ 완료 (2026-03-18 갱신) | 월드좌표 기반, Epsilon=0.05f 추가 (인접 경계 부동소수점 오차 방지) |
| 공격 방향 정밀도 | ✅ 완료 (2026-03-07) | 타겟 실제 transform.position 기반 Atan2, 2D 레거시 제거 |
| 공격 쿨다운 시스템 | ✅ 완료 (2026-03-07) | 유닛별 AttackCooldown, Attack 클립 길이 자동 설정 |
| Walk 애니메이션 연속 재생 | ✅ 완료 (2026-03-09) | 매 스텝 0f 리셋 제거 → 이미 Walk 상태이면 클립 유지 |
| 공격 애니메이션-타격 시각 동기화 | ✅ 완료 (2026-03-14) | Animation Event + AnimationEventRelay → scale punch (데미지 타이밍 무변경) |
| 유닛 메시 방향 보정 | ✅ 완료 (2026-03-14) | 하위 Mesh 오브젝트 Y 회전 30° / _meshYOffset 공격 방향 전용 / Root Motion OFF |
| 유닛 회전 보간 (DOTween) | ✅ 완료 (2026-03-14) | ApplyDirection + PlayAttackAnimation 모두 DORotate(_rotationDuration).SetEase(Ease.OutQuad) |
| 공격 후 Walk 복귀 버그 수정 | ✅ 완료 (2026-03-14) | 타겟 소멸 후 이동 재개 시 Play(StateWalk) 명시 호출 (멀티/싱글 공통) |
| 건물 배치 (Castle/Barracks/MiningPost) | ✅ 완료 | 건설 검증, 영토 확장 |
| 자원 시스템 (골드) | ✅ 완료 | 채굴소 수입, 건물/유닛 비용 |
| 인구 시스템 | ✅ 완료 | 타일 수 = 최대 인구 |
| 유닛 생산 (수동/자동) | ✅ 완료 | 큐 최대 3, 롱프레스 자동 |
| 랠리포인트 | ✅ 완료 | 마커 표시, BFS 빈 타일 탐색, 위치/회전 Inspector 조정 (GameConfig.RallyMarkerOffset/Euler) |
| 공성 시스템 | ✅ 완료 | 랠리→Castle 방향 자동 진군 |
| 승패 판정 (Castle 파괴) | ✅ 완료 | GameEndUseCase, UI 표시 |

#### 팀별 피아식별 + 신규 유닛 에셋 (2026-03-13)
| 항목 | 상태 | 비고 |
|------|------|------|
| 건물 Blue/Red 프리팹 (Castle, Barracks) | ✅ 완료 (2026-03-14) | BuildingFactory 팀별 분기 |
| 유닛 Pistoleer Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기 |
| 유닛 Assault(돌격소총병) Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기, UnitStats/ProductionStats 정의 |
| 유닛 Sniper(저격총병) Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기, UnitStats/ProductionStats 정의 |
| 초상화 스프라이트 Blue/Red (전 유닛) | ✅ 완료 | UI용 |
| 반응형 팝업 UI (ProductionPopup/BuildingPopup) | ✅ 완료 | 앵커 기반 배치, ResponsivePopupUISetup.cs |
| 팀별 초상화 동적 업데이트 (ProductionPanelUI/BuildingPlacementUI) | ✅ 완료 (2026-03-14) | Show() 호출 시 팀에 맞는 스프라이트 교체 |

#### 3D 전환 (2026-02-27 ~ 2026-03-01)
| 항목 | 상태 |
|------|------|
| XY→XZ 좌표계 전환 | ✅ 완료 |
| Orthographic 55도 틸트 카메라 | ✅ 완료 |
| 카메라 이동 경계(ClampPosition) 줌 연동 | ✅ 완료 |
| 2D Sprite → 3D Mesh 렌더링 | ✅ 완료 |
| sortingOrder 폐기 → Z-buffer | ✅ 완료 |
| Animator (IsDead/Walk/Attack) | ✅ 완료 |
| 헥스 타일 3D (ProBuilder + Shader Graph) | ✅ 완료 |
| 유닛 3D 모델 (Pistoleer, Meshy.ai) | ✅ 완료 |
| 건물 3D 모델 (Castle/Barracks/MiningPost) | ✅ 완료 |
| 금광 타일 3D 오브젝트 (GoldMineTile) | ✅ 완료 |
| 랠리포인트 마커 3D | ✅ 완료 |
| HexTileView 팀 색상 (_BaseColor, Shader Graph) | ✅ 완료 |

#### Game UI Lifecycle Framework (2026-03-24)
| 항목 | 상태 | 비고 |
|------|------|------|
| `IGameUI.cs` 인터페이스 | ✅ 완료 | OnGameStarted/OnGameEnded/OnGamePaused/OnGameResumed, default 빈 구현 |
| `GameUIManager.cs` 매니저 | ✅ 완료 | Register/Initialize, CompositeDisposable 중복 구독 방지, GameEndUI 제외 로직 |
| GameEvents — OnGameStarted/Paused/Resumed 추가 | ✅ 완료 | Subject<Unit> 3개 추가 |
| 기존 UI 4종 IGameUI 구현 | ✅ 완료 | GameHudUI/ProductionPanelUI/BuildingPlacementUI/GameEndUI |
| 멀티플레이 클라이언트 팝업 미닫힘 버그 수정 | ✅ 완료 | NetworkGameEndController.AnnounceWinnerClientRpc에서 NotifyGameEnded() 직접 호출 |

#### UI DOTween 애니메이션 프레임워크 (2026-03-19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `UIAnimator.cs` — static 헬퍼 | ✅ 완료 | PopupShow/Hide, SlideFromBottom/Top, ButtonPunch, CountTo, FlashText, FillTo |
| `AnimatedPanel.cs` — 팝업 컴포넌트 | ✅ 완료 | AnimationType(PopupFade/SlideFromBottom/SlideFromTop), IsVisible, SetUpdate(true), `_backgroundOverlay`(즉시 SetActive) |
| `AnimatedPanelSetup.cs` — Inspector 자동화 에디터 스크립트 | ✅ 완료 | `Hexiege/Setup/Apply AnimatedPanel Setup` 메뉴 |
| GameEndPanel → SlideFromTop | ✅ 완료 | 위에서 아래로 슬라이드 인 |
| ProductionPopup → SlideFromBottom | ✅ 완료 | 하단 슬라이드 업 |
| BuildingPopup → SlideFromBottom | ✅ 완료 | 기존 PopupFade → 변경 (ProductionPanelUI와 일관성 통일) |
| RematchRequestPopup DOFade 수정 | ✅ 완료 | _currentFade 공유 → 3개 별도 Tween / blocksRaycasts 해제 버그 수정 |

#### 전역 로딩 스크린 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| LoadingScreen.cs (싱글턴, DontDestroyOnLoad) | ✅ 완료 | `Presentation/UI/Common/` |
| Lobby 씬 UI 배치 (Canvas SO:100, Background/Spinner/StatusText) | ✅ 완료 | MCP로 생성 |
| 싱글플레이 씬 전환 2초 딜레이 | ✅ 완료 | `LoadSingleplayScene()` async void |
| 커스텀 호스트/참가 씬 전환 시 로딩 스크린 | ✅ 완료 | `LoadGameScene()` 직전 Show() |
| 랜덤 매칭 완료 시 로딩 스크린 | ✅ 완료 | `onMatchFound` 콜백 |
| sceneLoaded 이벤트 자동 Hide() | ✅ 완료 | NGO 씬 전환에도 정상 발동 확인 |

#### 커스텀게임 재경기 시스템 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| `NetworkGameManager.IsRandomMatchmaking` 속성 추가 | ✅ 완료 | 게임 모드 판별용 |
| `NetworkGameEndController` 재경기 RPC 시스템 | ✅ 완료 | Request/Accept/Decline ServerRpc + Notify ClientRpc(targeted) |
| `GameEndUI.SetupRematchButton()` / `RestoreRematchButton()` | ✅ 완료 | 모드별 버튼 분기, 요청 중 상태 관리 |
| `RematchRequestPopup.cs` 신규 생성 | ✅ 완료 | `Presentation/UI/Common/` — 수락/거절/거절알림 팝업 |
| `RematchPopupBuilder.cs` 에디터 스크립트 | ✅ 완료 | `Hexiege/UI/Build Rematch Popup` 메뉴 |
| 레이스 컨디션 처리 (`_rematchRequesterId`) | ✅ 완료 | 양측 동시 요청 → 즉시 재경기 |
| 랜덤매칭 다시하기 버튼 숨김 | ✅ 완료 | `isRandomMatch=true` 시 버튼 비활성화 |
| 싱글플레이 다시하기 동작 유지 | ✅ 완료 | 변경 없음 |

#### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| 버그 원인 파악 | ✅ 완료 | `_lobbySceneName` Inspector 값 "Game"으로 설정된 것이 근본 원인 |
| "로비로" 버튼 독립 처리 | ✅ 완료 | RPC 제거 → 로컬 Shutdown+LoadScene |
| 30초 자동 복귀 카운트다운 | ✅ 완료 | `WaitForSecondsRealtime` (timeScale=0 대응) |
| 싱글/멀티 통합 `ReturnToLobby()` | ✅ 완료 | NetworkContext 분기 제거 |

#### 멀티플레이 (Phase 1~8)
| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 1 | Lobby/Relay/NGO 연결 인프라 | ✅ 완료 |
| Phase 2 | 팀 할당 + 게임 시작 흐름 | ✅ 완료 |
| Phase 3 | 타일/자원 동기화 (NetworkTileSync, NetworkResourceSync) | ✅ 완료 |
| Phase 4 | 건물 배치 동기화 (NetworkBuildingController) | ✅ 완료 |
| Phase 5 | 유닛 생산 동기화 (NetworkProductionController, 자동생산 포함) | ✅ 완료 |
| Phase 6 | 유닛 이동 + 전투 동기화 (NetworkUnitMovementController, NetworkCombatController) | ✅ 완료 |
| Phase 6+ | AI 이동(Siege/랠리) 서버 권위 동기화 (BroadcastServerMove) | ✅ 완료 (2026-03-07) |
| Phase 6++ | 유닛 NGO NetworkObject 전환 (NetworkTransform 위치 동기화, 클라이언트 예측 제거) | ✅ 완료 (2026-03-26) |
| Phase 6+++ | 이동 전 회전 선행 (Rotate-then-Move, _isPreRotating 플래그로 DOTween-LateUpdate 충돌 해소) | ✅ 완료 (2026-03-27) |
| Phase 7 | 승패 판정 동기화 (NetworkGameEndController) | ✅ 완료 |
| Phase 8 | UI/UX 네트워크 대응 (LobbyUI, NetworkStatusUI, ReconnectionHandler) | ✅ 완료 |

#### 팀별 관점 (ViewConverter)
| 항목 | 상태 |
|------|------|
| ViewConverter (Core 레이어) | ✅ 완료 |
| Red팀 좌표 반전 (ToView/FromView) | ✅ 완료 |
| 건물/유닛 위치 반전 | ✅ 완료 |
| 입력 좌표 역변환 | ✅ 완료 |
| ViewConverter 초기화 순서 수정 (Setup→LoadMap) | ✅ 완료 |
| 싱글플레이 ViewConverter LocalPlayerTeam 기반 초기화 | ✅ 완료 (2026-03-20) |

---

### ⚠️ 알려진 미완성/버그 항목

#### 멀티플레이 기능 미구현
| 항목 | 파일 | 비고 |
|------|------|------|
| BuildFailedClientRpc UI 피드백 없음 | NetworkBuildingController | RPC 구조 완성, UI 기획 후 구현 예정 |
| EnqueueFailedClientRpc UI 피드백 없음 | NetworkProductionController | RPC 구조 완성, UI 기획 후 구현 예정 |
| 재접속 실제 구현 없음 | ReconnectionHandler | 30초 대기 후 ForceWin만 |
| 로비 UI 비주얼 폴리싱 | Lobby Views | UI 에셋 제작 후 진행 예정 |

#### GameConfig 코드 기본값 vs Inspector 값
- AnimationFps 필드 제거 완료 (2026-03-09 — 미사용 필드)
- TileHeight 코드 기본값 수정 완료: PointyTop=0.866, FlatTop=0.866
- FlatTop GridHeight 코드 기본값 수정 완료: 20
- CameraZoomDefault 수정 완료: 7

---

### ❌ 미구현 기능

| 기능 | 우선순위 | 관련 Phase |
|------|---------|-----------|
| 3종족 시스템 | 중간 | Phase 3 |
| 방어 타워 (Defense Tower) | 낮음 | Phase 3 |
| 마법 타워 (Magic Tower) | 낮음 | Phase 3 |
| 연구소 (Research Lab) | 낮음 | Phase 3 |
| 건물 업그레이드 시스템 | 낮음 | Phase 3 |
| 유닛 AI 상태머신 | 낮음 | Phase 3 |
| 타임라인/서든데스 시스템 | 낮음 | Phase 3 |
| 사운드/BGM | 낮음 | Phase 4 |
| 튜토리얼 | 낮음 | Phase 4 |
| 게임 내 밸런싱 | 중간 | Phase 4 |
| PlayFab 백엔드 (계정/랭킹/인앱결제) | 낮음 | Phase 4 |
| 카드 수집 시스템 | 낮음 | Phase 4 |

---

## 기술 스택 현황

| 항목 | 기술 | 버전 |
|------|------|------|
| 게임 엔진 | Unity | 6000.0.x (Unity 6 LTS) |
| 렌더 파이프라인 | URP | Universal Render Pipeline |
| 네트워크 | Netcode for GameObjects | 2.9.2 |
| 멀티플레이 서비스 | Unity Multiplayer Services | 2.0.0 (Lobby+Relay+Auth 통합) |
| 이벤트 시스템 | UniRx | 7.1.0 |
| 3D 모델링 도구 | Meshy.ai | Image-to-3D 파이프라인 |
| 애니메이션 | Mixamo + Unity Animator | Mecanim |
| 셰이더 | Shader Graph (SG_HexTile) | Object Space SDF 기반 |

---

## 아키텍처 현황

```
Bootstrap
  └── GameBootstrapper (유일한 composition root)

Domain ← (참조 금지) Core
  ├── HexCoord, HexGrid, HexPathfinder
  ├── UnitData, BuildingData (IDamageable)
  └── HexOrientationContext (정적 홀더)

Application
  ├── UseCases (Combat, Movement, Spawn, Production, ...)
  ├── Interfaces/IEntityPositionProvider (2026-03-02 추가)
  ├── NetworkContext (정적 홀더 — 네트워크 상태)
  └── GameEvents (UniRx Subject 허브)

Core
  ├── HexMetrics (헥스↔월드 좌표 변환, XZ 평면)
  └── ViewConverter (팀별 관점 반전, Red팀만)

Infrastructure
  ├── Config/GameConfig (ScriptableObject)
  ├── Factories/UnitFactory, BuildingFactory
  ├── UnitWorldPositionProvider (IEntityPositionProvider 구현)
  └── Network/ (NetworkXxxController × 8)

Presentation
  ├── Grid/HexTileView, HexGridRenderer
  ├── Unit/UnitView (Lerp + Animator + Register/Unregister)
  ├── Camera/CameraController (XZ 레이캐스트 팬, 55도 틸트)
  ├── Input/InputHandler (XZ 평면 입력)
  ├── UI/ (HUD, 생산 패널, 건물 배치, 게임 종료)
  └── UI/Views/Lobby/ (MVVM — LobbyRootView, TabBarView, BattleRootView + 서브뷰 8종)
       └── UI/ViewModels/ (LobbyViewModel, BattleViewModel — UniRx ReactiveProperty)
```

---

## 에셋 현황

### 3D 모델 (Meshy.ai)
| 에셋 | 경로 | 상태 |
|------|------|------|
| Pistoleer 유닛 (Blue/Red) | Prefabs/Units/Unit_Pistoleer_Blue/Red.prefab | ✅ 완료 |
| Assault 유닛 (Blue/Red) | Prefabs/Units/Unit_Assault_Blue/Red.prefab | ✅ 완료 |
| Sniper 유닛 (Blue/Red) | Prefabs/Units/Unit_Sniper_Blue/Red.prefab | ✅ 완료 |
| Castle (Blue/Red) | Prefabs/Buildings/Building_Castle_Blue/Red.prefab | ✅ 완료 |
| Barracks (Blue/Red) | Prefabs/Buildings/Building_Barracks_Blue/Red.prefab | ✅ 완료 |
| MiningPost | Prefabs/Buildings/Building_MiningPost.prefab | ✅ 완료 |
| GoldMineTile | Prefabs/Buildings/GoldMineTile.prefab | ✅ 완료 |
| RallyPointMarker | Prefabs/Misc/RallyPointMarker.prefab | ✅ 완료 |

### 타일
| 에셋 | 경로 | 상태 |
|------|------|------|
| HexTile (FlatTop) | Prefabs/Tiles/HexTile.prefab | ✅ 완료 (ProBuilder + SG_HexTile) |

### 미제작 에셋
| 에셋 | 용도 |
|------|------|
| 방어타워/마법타워/연구소 3D | 미구현 건물 타입 |
