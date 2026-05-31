# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-05-31
**현재 단계:** ProductionPopup 잠금 유닛 Lock Icon 표시 완료

---

## 전체 구현 현황

### ✅ 완료된 시스템

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 (2026-04-26 갱신) | TileOwnershipService — Phase 0/1/2 모든 이동 방식에서 매 프레임 물리 위치 기반 실시간 점령 |
| 금광 타일 시스템 | ✅ 완료 (2026-04-08 갱신) | HasGoldMine, 채굴소 건설 조건, 건물 배치 시 광산 숨김/파괴 시 재표시+타일 중립 복원 |
| A* 경로탐색 | ✅ 완료 (2026-03-18 갱신) | ClaimedTile 기반 아군 차단 (중간 타일만), 목표 타일 blocked 체크 제거 |
| 카메라 줌 보간 | ✅ 완료 (2026-03-19) | DOTween.To + Ease.OutCubic, _targetZoom 누적, _zoomDuration(0.25f) SerializeField |
| 유닛 이동 (Lerp) | ✅ 완료 | Per-step 가용성 체크, 재탐색 |
| 전투 시스템 | ✅ 완료 | IDamageable, 이동 중 자동 공격 |
| 전투 거리 정밀도 | ✅ 완료 (2026-03-18 갱신) | 월드좌표 기반, Epsilon=0.05f 추가 (인접 경계 부동소수점 오차 방지) |
| 공격 방향 정밀도 | ✅ 완료 (2026-03-07) | 타겟 실제 transform.position 기반 Atan2, 2D 레거시 제거 |
| 공격 쿨다운 시스템 | ✅ 완료 (2026-04-04 갱신) | 유닛별 AttackCooldown=클립 길이 (Assault=0.2s, Pistoleer=2.0s, Sniper=3.0s), elapsed 기반 정확한 감소 |
| 다중 히트 데미지 | ✅ 완료 (2026-04-24) | FlameSpirit 6히트(총 12dmg), LionKnight 2히트(총 18dmg). HitFrameTimes float[] 기반, 싱글=PendingHit 타이머, 멀티=코루틴 N개 병렬 |
| 전투 애니메이션 시스템 (멀티플레이) | ✅ 완료 (2026-04-04) | 3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정, 사이클 동기화 |
| Walk 애니메이션 연속 재생 | ✅ 완료 (2026-03-09) | 매 스텝 0f 리셋 제거 → 이미 Walk 상태이면 클립 유지 |
| 공격 애니메이션-타격 시각 동기화 | ✅ 완료 (2026-03-14) | Animation Event + AnimationEventRelay → scale punch (데미지 타이밍 무변경) |
| 유닛 메시 방향 보정 | ✅ 완료 (2026-04-29 갱신) | 전 유닛 Mesh Y=0, _meshYOffset 제거, 이동 anim offset=0, DirectionAngles={60,120,180,240,300,0} (FlatTop 월드 각도 기준) |
| 유닛 회전 시스템 (RotateTowards 통일) | ✅ 완료 (2026-05-14 개편) | 모든 회전 Quaternion.RotateTowards 통일. 방향 계산 Atan2(현재 월드 위치→목적지) 기반. [SerializeField] _rotationSpeed = 270f Inspector 조정 가능 |
| 공격 후 Walk 복귀 버그 수정 | ✅ 완료 (2026-03-14) | 타겟 소멸 후 이동 재개 시 Play(StateWalk) 명시 호출 (멀티/싱글 공통) |
| 건물 배치 (Castle/Barracks/MiningPost) | ✅ 완료 | 건설 검증, 영토 확장 |
| 자원 시스템 (골드) | ✅ 완료 | 채굴소 수입, 건물/유닛 비용 |
| 인구 시스템 | ✅ 완료 | 타일 수 = 최대 인구 |
| 유닛 생산 (수동/자동) | ✅ 완료 | 큐 최대 3, 롱프레스 자동 |
| 랠리포인트 | ✅ 완료 | 마커 표시, BFS 빈 타일 탐색, 위치/회전 Inspector 조정. 팀별 표시 분리: 각 플레이어 자신의 깃발만 표시 (2026-05-16) |
| 공성 시스템 | ✅ 완료 | 랠리→Castle 방향 자동 진군 |
| 유닛 분산 이동 (혼잡도 기반) | ✅ 완료 (2026-05-15) | CongestionMap + CongestionAwarePathfinder — 타일 혼잡도 가중 A*로 경로 자연 분산. GameConfig에 DecayInterval/CongestionWeight 통합 |
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
| 종족+팀별 초상화 동적 업데이트 (ProductionPanelUI/BuildingPlacementUI) | ✅ 완료 (2026-04-30) | Show() 호출 시 종족+팀에 맞는 스프라이트 교체 및 생산 타입 바인딩 |
| 잠금 유닛 Lock Icon 표시 (ProductionPopup) | ✅ 완료 (2026-05-31) | 초상화 디밍(35% 밝기) + 우측 하단 자물쇠 아이콘 배지. 업그레이드 후 잠금 해제 확인 |

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
| Phase 6++++ | 공격 타이밍 정밀화 (타격 프레임 데미지, 타겟 고정, 쿨다운 통일) | ✅ 완료 (2026-03-27) |
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

#### 로비 종족 선택 UI (2026-04-04~06)
| 항목 | 상태 | 비고 |
|------|------|------|
| RaceId enum (Human/Spirit/Transcendence) | ✅ 완료 | Domain 레이어 |
| LocalPlayerRace / GameRaceContext 정적 홀더 | ✅ 완료 | Infrastructure 레이어 |
| RaceSelectionViewModel (UniRx) | ✅ 완료 | 종족 순환 + LocalPlayerRace 연동 |
| RaceSelectionView (캐러셀 + DOTween) | ✅ 완료 | 3캐릭터 원근감 배치, Walk/Idle CrossFade 1초 |
| BattleMainView BindRace 연동 | ✅ 완료 | BattleRootView에서 ViewModel 생성·주입 |
| CharacterPreview RenderTexture 카메라 | ✅ 완료 | CharacterPreview 레이어 격리 |
| 종족 선택 항상 표시 (화면 전환 무관) | ✅ 완료 | BattleMainView에서 RaceSelectionView 독립 토글 제거 |
| 종족명 자연→초월 변경 | ✅ 완료 | Nature→Transcendence (코드+한글 표시명 모두) |
| Pistoleer Idle 첫 프레임 동결 버그 수정 | ✅ 완료 | Pistoleer.controller Idle m_Speed 0→1 |
| Android URP RenderTexture 잔상 + RenderPass 에러 수정 | ✅ 완료 | RT antiAliasing 2→1, allowMSAA/allowHDR false, backgroundColor alpha 1 |

#### 유닛/건물 스탯 확정 적용 (2026-04-12~13)
| 항목 | 상태 | 비고 |
|------|------|------|
| Spirit/Transcendence 6종 HP/ATK/생산시간/비용 확정 | ✅ 완료 | StatsReference.md 기준 |
| Pistoleer MoveSpeed 1.0→0.5 수정 | ✅ 완료 | |
| Transcendence 건물 HP 종족별 분기 | ✅ 완료 | BuildingStats.GetMaxHp(type, RaceId) 오버로드 |
| 생산 패널 골드 비용 텍스트 표기 | ✅ 완료 | _slot1/2/3CostText (숫자만, G 없음) |
| 건물 배치 팝업 골드 비용 텍스트 표기 | ✅ 완료 | _barracksCostText / _miningPostCostText |
| 싱글/멀티 실기 테스트 | ✅ PASS | TC-SINGLE-01~10, TC-MULTI-01 전체 PASS |

#### 피격 시 부유 HP 텍스트 — World Space (2026-04-12~13, 2026-04-17 World Space 전환)
| 항목 | 상태 | 비고 |
|------|------|------|
| FloatingHpText.cs (DOTween 애니메이션, 오브젝트 풀 반환) | ✅ 완료 | TextMeshPro 3D World Space |
| FloatingHpTextSpawner.cs (이벤트 구독, 풀 관리) | ✅ 완료 | 월드 좌표 직접 사용, 좌표 변환 없음 |
| FloatingHpText 프리팹 (Maplestory Light SDF FloatingHpText Material.mat) | ✅ 완료 | 독립 .mat 파일 사용 (폰트 .asset 오염 방지) |
| 줌 연동 비율 일관성 (scale=1f 고정) | ✅ 완료 (2026-04-17) | 텍스트가 유닛/건물처럼 줌 비례 동작 → 모든 줌에서 유닛 대비 비율 일정 |
| 빌보드 회전 + 좌우 반전 보정 | ✅ 완료 (2026-04-13) | LookRotation(-forward,up) + localScale(-s,s,s) |
| 멀티플레이 클라이언트 표시 (NetworkHealthSync 재발행) | ✅ 완료 | |
| 팀별 텍스트 색상 (Blue=연두, Red=노랑) | ✅ 완료 (2026-04-13) | Inspector SerializedField로 조정 가능 |
| SetupFloatingHpText 에디터 스크립트 자동화 | ✅ 완료 | Hexiege/Setup/FloatingHpText 설정 |
| 싱글플레이 실기 테스트 | ✅ PASS | TC-1~6 전체 PASS, TC-7(멀티) 미확인 |

#### 종족 인게임 적용 (2026-04-07)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | GameRaceContext 기반 (race, team) 튜플 switch |
| BuildingFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | MiningPost 포함 종족별 분기 |
| GameBootstrapper 싱글 GameRaceContext 초기화 | ✅ 완료 | LoadMap() 직전 Set(LocalPlayerRace.Current, 랜덤종족) — Enum.GetValues + Random.Range (2026-04-24 랜덤으로 변경) |
| 오브젝트 이름 실제 프리팹명 반영 | ✅ 완료 | {prefab.name}_{id} 형식 |
| 에디터 자동 연결 스크립트 | ✅ 완료 | Hexiege/Setup/UnitFactory·BuildingFactory 프리팹 연결 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-01~06 전체 통과 |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-01 통과 |

#### UnitType 개편 + 근접 사거리 시스템 (2026-04-10~11)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitType enum 유닛별 독립 식별자로 개편 | ✅ 완료 | Pistoleer=0 ~ LionKnight=8 (9종) |
| UnitFactory List<UnitPrefabEntry> 구조 변경 | ✅ 완료 | 종족별 리스트 (type/blue/red) |
| Spirit/Transcendence UnitStats 추가 | ✅ 완료 | Range/Cooldown/HitFrameTime 확정, HP/ATK 미정 |
| ProductionPanelUI 종족별 버튼 동적 바인딩 | ✅ 완료 | BindButtonUnitTypes(RaceId), 6세트 초상화 필드 |
| 생산 패널/건물 배치 종족별 초상화 스프라이트 연결 | ✅ 완료 (2026-04-13) | Spirit/Transcendence 유닛+건물 초상화 Inspector 연결 완료 (Spirit Blue ManaRift 2026-04-13 제작 완료) |
| 근접 유닛 Castle 방향 Lerp 이동 + 공격 | ✅ 완료 | FindPathToNeighbor + 경로 끝에 Castle 타일 추가 |
| 다중 유닛 Castle 연속 공격 (ClaimedTile 버그 수정) | ✅ 완료 | 마지막 non-walkable 타일 ClaimedTile 설정 생략 |
| SetupUnitFactoryPrefabs 에디터 스크립트 재작성 | ✅ 완료 | List 구조 대응 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-001~006 통과 (003/004 스프라이트 CONDITIONAL PASS) |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-001 통과 |

#### 원거리 유닛 공격 중 회전 추적 (2026-04-11~12)
| 항목 | 상태 | 비고 |
|------|------|------|
| 공격 중 타겟 방향 지속 추적 | ✅ 완료 | Transform 참조 저장 + Update() RotateTowards |
| 멀티플레이 타이밍 방어 (B사망→C전환 고착 버그) | ✅ 완료 | 백업 ID 필드 + Update() 재조회 로직 |
| 타겟 고착성 (현재 타겟 생존 중 교체 방지) | ✅ 완료 | IsCurrentTargetStillValid() 추가, TickCombat 2곳 수정 |
| 타겟 변경 시 부드러운 회전 전환 | ✅ 완료 (2026-04-12) | RotateTowards(270°/s), ChangeTarget() 즉시 스냅 제거 |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-001~007 전체 통과 |

#### 근접유닛 적 감지 사거리 + 추적 회전 개선 (2026-04-19~24)
| 항목 | 상태 | 비고 |
|------|------|------|
| DetectRange / AttackRange 분리 | ✅ 완료 (2026-04-19) | 근접유닛 DetectRange=1.0f(타일), 원거리는 AttackRange와 동일 |
| 하이브리드 이동 시스템 (Phase 0/1/2) | ✅ 완료 (2026-04-19) | Phase 1 월드 직선 추적, Phase 2 타일 스냅 후 A* 재개 |
| Phase 1 추적 중 타겟 방향 회전 개선 | ✅ 완료 (2026-04-24) | CalculateAttackAngle + RotateTowards(270°/s), 이전 타일 방향 고정 문제 해소 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-001~002 통과 (2026-04-24) |

#### 타일 소유권 실시간 감지 — TileOwnershipService (2026-04-26)
| 항목 | 상태 | 비고 |
|------|------|------|
| `TileOwnershipService.cs` 신규 생성 | ✅ 완료 | `Application/Services/` — Pull 모델, 매 프레임 유닛 물리 위치 기반 타일 소유권 결정 |
| HashSet 풀(`Queue<HashSet<TeamId>>`) | ✅ 완료 | 매 프레임 GC 최소화 |
| 점령 규칙 (한 팀/양 팀/없음) | ✅ 완료 | 한 팀만 있을 때만 갱신, 나머지 현 상태 유지 |
| 이벤트 중복 발행 방지 | ✅ 완료 | `GetOwner != claimingTeam` 조건 가드 |
| `HexGrid.GetOwner(HexCoord)` 신규 추가 | ✅ 완료 | `_tiles.TryGetValue` → `tile.Owner` |
| GameBootstrapper Tick 연결 | ✅ 완료 | `(!NetworkContext.IsNetworkActive \|\| NetworkContext.IsNetworkServer)` 가드 |

#### 근접유닛 뒷무빙 5차 개선 (2026-04-26)
| 항목 | 상태 | 비고 |
|------|------|------|
| Phase 1 타겟 사망 시 즉시 다음 적 재선택 | ✅ 완료 | 다음 적 있으면 Phase 1 유지, 없으면 Phase 2 진입 |
| 전투 루프 종료 후 다음 타겟 선택 | ✅ 완료 | 전투 종료 후에도 감지 범위 내 적 재탐색 |
| Phase 2 후방 스냅 방지 | ✅ 완료 | `HexCoord.Distance` 비교로 nearestTile이 finalTarget보다 멀면 현 위치 유지 |
| Phase 2 점유 누수 방지 | ✅ 완료 | `nearestTile == _unitData.Position`이면 `RegisterOccupancyMove` 생략 |

#### 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitStatsConfig ScriptableObject (전투+생산 통합) | ✅ 완료 | `UnitStatEntry` struct, Inspector에서 9종 유닛 수치 편집 |
| BuildingStatsConfig ScriptableObject (건물타입별 종족 묶음) | ✅ 완료 (2026-05-18 갱신) | `BuildingTypeEntry` B방식, 32종 BuildingType × 3종족 전체 항목 채움. `AttackCooldown` (float) 필드 추가 — AutoTower 종족별 쿨다운 적용 (Human/Trans 5.0s, Spirit 3.5s). `BuildingStats.GetAttackCooldown(type, race)` API 신규. |
| UnitStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<UnitType, StatValues>`, `Initialize()` 추가 |
| UnitProductionStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<UnitType, ProductionValues>`, `Initialize()` 추가 |
| BuildingStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<(BuildingType, RaceId), StatValues>`, HP+골드+공격력 통합 |
| BuildingStats.GetGoldCost / GetAttackPower 신규 메서드 | ✅ 완료 | 건물 배치 비용 조회 + 향후 타워 기능 대비 |
| GameBootstrapper Initialize 연결 | ✅ 완료 | `_unitStatsConfig`, `_buildingStatsConfig` SerializedField |
| BuildingPlacementUI GetBuildingCost BuildingStats 연동 | ✅ 완료 | `BuildingStats.GetGoldCost(type, race)` |
| 에디터 자동 생성 스크립트 2종 | ✅ 완료 | `Hexiege/Setup/UnitStatsConfig 생성`, `Hexiege/Setup/BuildingStatsConfig 생성` |
| 에셋 파일 생성 | ✅ 완료 | `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `BuildingStatsConfig.asset` |

#### 공통 UI 규칙 수립 및 Canvas Scaler / SafeArea 적용 (2026-05-25)
| 항목 | 상태 | 비고 |
|------|------|------|
| 공통 UI 규칙 10개 확정 (GameSystemRules.md) | ✅ 완료 | Canvas Scaler, 앵커 기반 레이아웃, SafeArea, CanvasGroup 패턴, 폰트, 골드 부족 UI, 팝업/모달 구분 등 |
| Canvas Scaler 통일 (Rule 1) | ✅ 완료 | SetupCanvasScaler.cs 에디터 스크립트. Game.unity(540×960→1080×1920, 0.5→0) + Lobby.unity Canvas2(0.5→0) |
| SafeAreaFitter.cs 신규 구현 (Rule 4) | ✅ 완료 | Screen.safeArea 기반 anchorMin/anchorMax 정규화. `Presentation/UI/Common/` |
| SafeAreaContainer 씬 구조 적용 (Rule 4) | ✅ 완료 | SetupSafeAreaContainer.cs 에디터 스크립트. Game.unity 7개 UI 이동, Lobby.unity 캔버스별 적용, ToastUI SafeAreaFitter 직접 부착 |
| 전체 UI 규칙 준수 검증 (Rule 5 CanvasGroup 전환) | ✅ 완료 (2026-05-27) | 로비 7개 뷰 SetActive → CanvasGroup 전환. LobbyRootView, BattleMainView, CustomGameView, CustomHostView, CustomJoinView, RandomMatchView, ProfileView. 실기기 TC 전체 PASS (2026-05-27): TC-SINGLE-001~014 PASS, TC-SINGLE-015~016 SKIP(로그인 미구현). 랜덤 매칭 대기화면 BUG(GameObj inactive)→ Inspector에서 직접 수정 완료 |
| 로비 배경 Safe Area 수정 (Rule 4) | ✅ 완료 (2026-05-26) | LobbyRoot Image가 SafeAreaContainer 안에 있어 Safe Area 경계에서 배경이 끊기는 문제. Canvas 직속 자식 LobbyBackground 오브젝트 신규 추가(전체화면 stretch, 남색), LobbyRoot Image 비활성화. FixLobbyBackground.cs 에디터 스크립트로 적용. 실기기 테스트 PASS |

#### 게임 화면 UI TC 전체 실기기 테스트 (2026-05-27)
| 항목 | 상태 | 비고 |
|------|------|------|
| 게임 화면 UI TC 62개 실기기 테스트 | ✅ 완료 | `Assets/_Project/Docs/_Tasks/2026-05-26/game-ui-tc/Testcase.md` 참조 |
| TC-MULTI-END-001 버그 수정 (BUG-001) | ✅ 완료 | 포기 시 호스트 GameEndUI 미표시. `NetworkGameEndController.ForfeitServerRpc`에 `GameEvents.OnGameEnd.OnNext()` 1줄 추가. 근본 원인: ForfeitServerRpc가 GameEndUseCase를 건너뛰어 서버 측 OnGameEnd 미발행 |
| TC-MULTI-END-001 버그 수정 (BUG-002) | ✅ 완료 | 재경기 팝업이 GameEndUI 뒤에 가려짐. Canvas Hierarchy에서 RematchRequestPopup을 SafeAreaContainer 이후 인덱스로 이동 (Inspector). AnimatedPanel.Show()에 SetAsLastSibling() 없음 확인 → 영구 수정 |
| UI 크기/레이아웃 FAIL 항목 (BP-001/002, BAP-001, PRD-001 완료) | ⚠️ 일부 완료, 나머지 별도 작업 예정 | ✅ BP-001/BP-002(건물 배치 패널): 씬 계층 재설계로 해결 (2026-05-29). ✅ BAP-001(비생산 건물 패널): 3x3 그리드 + 런타임 슬롯 제어로 해결 (2026-05-29). ✅ PRD-001(패널 버튼 크기): LayoutElement(prefH=0,flexH=1) Row 적용 + Rallypoint 패딩 수정으로 해결 (2026-05-31). ❌ HUD-007(설정 버튼), SET-004(확인팝업 버튼 색상), SET-007/END-001(UI 크기), MULTI-END-002(재경기 UI 크기) |

#### BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 (2026-05-29)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingActionPanel 3x3 그리드 구조 재구성 | ✅ 완료 | BuildingPlacementUI와 동일한 VLG+HLG 중첩 패턴. 래퍼 오프셋 제거(anchoredPosition=0,sizeDelta=0). CancelButton 위치 BuildingPlacementUI와 통일 |
| 런타임 슬롯 표시/숨김 제어 | ✅ 완료 | BuildingActionPanelUI.cs에 _allSlotButtons/_activeSlotButtons 추가. OnShow() 오버라이드에서 CanvasGroup alpha 제어. BuildingPlacementUI._buttonCanvasGroups 패턴과 동일 |
| HeaderText 앵커 순수 앵커 기반 변환 | ✅ 완료 | anchorMin=(0.096,0.826), anchorMax=(0.867,1.006), pos=(0,0), delta=(0,0) |

---

#### BuildingPlacementUI 씬 계층 재설계 (2026-05-29)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingPlacementUI 씬 계층 전면 재구성 | ✅ 완료 | TC-SINGLE-BP-001/002 FAIL 해결. GridLayoutGroup 제거 → VLG+HLG 중첩 구조(GameSystemRules Rule 2). 3행×3열 그리드 정상 표시. 순수 앵커 기반(anchoredPosition=0, sizeDelta=0) |
| BuildingPlacementUI.cs _buildingGoldIcons 필드 추가 | ✅ 완료 | 버튼 내부 골드 아이콘 Inspector 참조 신규 |
| GameSystemRules.md Rule 2 보완 | ✅ 완료 | Layout Group 반응형 패턴 명세 추가 (VLG+HLG 중첩 + Control Child Size + Force Expand) |
| GameSystemRules 준수 검증 (Rule 2/4/5/6) | ✅ 완료 | BuildingPopup 전체 계층 씬 YAML 직접 파악 + 검증 통과 |
| RebuildBuildingPlacementUI.cs 에디터 스크립트 | ✅ 완료 | Assets/Editor/ — 재구성 재실행 가능한 1회성 스크립트 |

---

#### 설계 규칙 문서 (GameSystemRules.md)
| 항목 | 상태 | 비고 |
|------|------|------|
| 유닛 이동 시스템 규칙 1~8 | ✅ 확정 (2026-05-14 개편) | 이동/전투/회전 규칙 통합, 총 16개 규칙으로 재번호화 |
| 회전 규칙 통합 | ✅ 확정 (2026-05-14) | 규칙 7(A* 이동 중 서서히 회전), 8(재개 시 이동 방향 바라봄), 12(전투 이동 중), 15(공격 중) — 이동/전투 규칙에 통합 |

#### 인증 시스템 설계 규칙 문서 (AuthSystemRules.md)
| 항목 | 상태 | 비고 |
|------|------|------|
| AuthSystemRules.md 작성 | ✅ 완료 (2026-05-23) | Firebase Auth 기반. 로그인 3종(익명/Google Play Games/이메일+비밀번호), Firebase UID → UGS 브릿지, 백엔드 Option A(Firebase 생태계) 확정 |

---

#### 로그인 시스템 C# 구현 (2026-05-24)
| 항목 | 상태 | 비고 |
|------|------|------|
| Firebase SDK v13.11.0 설치 | ✅ 완료 | FirebaseAuth.unitypackage 임포트. FirebaseApp은 v12+에서 각 패키지에 번들됨 (별도 임포트 불필요) |
| Google Play Games Plugin v2.1.0 설치 | ✅ 완료 | GitHub `current-build/` 폴더 내 .unitypackage 임포트. v1은 2026년 5월부터 deprecated |
| EDM4U Android 의존성 해결 | ✅ 완료 | Custom Main Gradle Template + Custom Gradle Properties Template 활성화 (Jetifier 필요). Multidex 불필요 (Min API 25) |
| FirebaseAuthService.cs (Infrastructure) | ✅ 완료 | Firebase SDK 래퍼. 익명/Google/이메일 로그인 API 제공. AuthException + AuthErrorReason enum 정의. SignInWithCredentialAsync 반환값 FirebaseUser로 수정 (SDK 13.x 호환) |
| LoginUseCase.cs (Application) | ✅ 완료 | 로그인 흐름 조율. BridgeToUGSAsync(Firebase UID → UGS) 포함. 현재 SignInWithCustomIdAsync 미지원으로 익명 로그인 임시 사용 (TODO 주석) |
| AccountLinkUseCase.cs (Application) | ✅ 완료 | 익명 → 실계정 연동 흐름 |
| LoginBootstrapper.cs (Bootstrap) | ✅ 완료 | Login.unity 씬 Composition Root. PlayGamesPlatform.Activate() + Firebase 초기화 + DI |
| LoginRootView.cs (Presentation) | ✅ 완료 | 패널 전환 + Back 스택 + Android 뒤로가기. Application.Quit() → UnityEngine.Application.Quit() 수정 (Hexiege.Application 네임스페이스 충돌 방지) |
| LoginSelectView.cs | ✅ 완료 | 로그인 방식 선택 화면 |
| EmailLoginView.cs | ✅ 완료 | 이메일 로그인 화면 |
| SignUpView.cs | ✅ 완료 | 이메일 회원가입 화면 |
| EmailVerifyView.cs | ✅ 완료 | 이메일 인증 대기 화면 |
| PasswordResetView.cs | ✅ 완료 | 비밀번호 재설정 화면 |
| AnonymousWarningPopup.cs | ✅ 완료 | 익명 로그인 경고 팝업 |
| ProfileView.cs (Lobby — 수정) | ✅ 완료 | 계정 정보 표시, Google/이메일 연동 버튼, 로그아웃 UI 구현 |
| UnityServicesInitializer.cs (수정) | ✅ 완료 | 매 초기화 시 항상 SignOut() → SignInAnonymouslyAsync() 수행하여 유효한 토큰 보장. IsSignedIn=true(기기 캐시)이지만 서버 토큰 만료 시 발생하는 UGS 401 버그 수정 (2026-05-24). ※ Login 씬 흐름 완성 시 재검토 필요 |
| 컴파일 에러 전체 해결 | ✅ 완료 | CS0103(AuthException/AuthErrorReason using 누락), CS0029(SignInWithCredentialAsync 반환 타입), CS1061(SignInWithCustomIdAsync 미지원), CS0234(Application.Quit 네임스페이스 충돌) 전체 해결 |
| 기존 UGS 로그인 동작 보존 | ✅ 완료 | Lobby.unity 직접 실행 시 익명 로그인으로 PlayerId 발급 — 멀티플레이 기능 정상 동작. 401 버그 수정 후 커스텀 게임 + 랜덤 매칭 모두 확인 |
| Firebase Console 설정 | ❌ 미완료 | google-services.json, SHA-1 등록, Authentication 방식 활성화 — 추후 진행 |
| GPGS 클라이언트 ID 설정 | ❌ 미완료 | Unity > Window > Google Play Games > Setup 에서 Web Client ID 입력 — 추후 진행 |
| Login.unity 씬 생성 | ❌ 미완료 | UIWireframe.md 기반 UI 배치 + Inspector 연결 — 추후 진행 |
| Firebase UID → UGS Custom ID 브릿지 | ⚠️ 임시 | SignInWithCustomIdAsync 현재 UGS SDK 미지원. 임시로 SignInAnonymouslyAsync 사용. 추후 UGS SDK 업데이트 시 교체 예정 |

---

#### 유닛 이동/전투 시스템 재설계 (2026-05-11~13)
| 항목 | 상태 | 비고 |
|------|------|------|
| 슬롯 시스템 전면 폐기 | ✅ 완료 (2026-05-11) | TileMoveSlotManager / TileOccupancyManager / AttackPositionManager 비활성화(주석 처리). GameBootstrapper 주입 코드 포함 |
| 타일 점유 한도(OccupancySize) 제거 | ✅ 완료 (2026-05-11) | UnitData.ClaimedTile / UnitStats.OccupancySize / UnitMovementUseCase 점유 메서드 비활성화 |
| 근접/원거리 동일 상태 머신 (MoveAlongPathV3) | ✅ 완료 (2026-05-11) | Phase 0(A* Lerp) → 감지 → Phase 1(직선 추격) → 공격 → FindForwardClosestTile → 재개. 겹침 허용 |
| UnitCombatUseCase DetectRange 판정 통합 | ✅ 완료 (2026-05-11) | isMelee 분기 제거, 모든 유닛 DetectRange × TileHeight 통일 |
| 원거리 유닛 DetectRange 수치 분리 | ✅ 완료 (2026-05-11) | UnitStatsConfig Inspector에서 원거리 유닛 DetectRange > AttackRange 설정 |
| 추격 중 건물 변화 시 유닛 멈춤 (BUG-001) | ✅ 완료 (2026-05-12) | `_isInCombatPursuit` 필드 추가, IsInCombat() 추격 단계도 전투 중으로 판정 |
| 전투 종료 후 순간이동 (BUG-002) | ✅ 완료 (2026-05-13) | 즉시 스냅 제거 → 정렬 Lerp(동일 이동 속도)로 교체. 정렬 중 적 감지 시 즉시 전투 재진입 |

---

#### 유닛 생산 실패 피드백 시스템 (2026-05-16)
| 항목 | 상태 | 비고 |
|------|------|------|
| 범용 토스트 UI 시스템 (ToastUI) | ✅ 완료 | DontDestroyOnLoad 독립 Canvas, 큐 방식, 터치 제거, DOTween 페이드아웃 |
| ToastMessageConfig ScriptableObject | ✅ 완료 | Resources/Config — 메시지/노출시간 Inspector 편집 가능 |
| 골드 부족 피드백 — 유닛 생산 비용 텍스트 빨간색 | ✅ 완료 | `_unitCostTexts[i]` 개별 평가 (보유 골드 표시 텍스트는 변경 안 함) |
| 인구 초과 피드백 — HUD 인구수 텍스트 빨간색 | ✅ 완료 | `used >= max` 조건 매 갱신 시 색상 자동 전환 |
| 수동 생산 실패 토스트 3종 | ✅ 완료 | GoldInsufficient / PopulationFull / ProductionQueueFull |
| 자동 생산 자원 부족 시 즉시 취소 | ✅ 완료 | IsCharged=false 항목만 취소, IsCharged=true는 Rule 2 유지 |
| 싱글플레이 실기 테스트 | ✅ PASS (골드부족·큐초과) | 인구초과·자동취소는 코드 검토 완료 |

#### 건물 배치 패널 실패 피드백 + UI 개선 (2026-05-16)
| 항목 | 상태 | 비고 |
|------|------|------|
| 골드 부족 시 건물 비용 텍스트 빨간색 | ✅ 완료 | 팝업 열릴 때 + OnResourceChanged 구독으로 실시간 재평가. 팝업 닫힐 때 흰색 초기화 |
| 골드 부족 시 토스트 메시지 | ✅ 완료 | `ToastKey.GoldInsufficient` 재사용. 팝업 유지(Close 호출 없음). 싱글플레이 분기만 적용 |
| 건물 비용 텍스트 'G' 접미사 제거 | ✅ 완료 | "200G" → "200". 생산 패널과 동일 표기로 통일 |
| ToastUI SetActive 버그 수정 | ✅ 완료 | ClearAll/FinishCurrent의 SetActive(false) 제거 → CanvasGroup.blocksRaycasts+interactable로 대체. 루트 항상 활성 유지 |
| 싱글플레이 실기 테스트 | ✅ PASS | 골드 부족 비용 텍스트 빨간색, 토스트 메시지, 팝업 유지 동작 확인 |

#### 건물 업그레이드 시스템 (2026-05-17~18)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingType enum 26종 확장 | ✅ 완료 | 단일 Barracks 제거 → 종족별 생산라인×단계(1/2/3) |
| BuildingTypeHelper.cs 신설 (Domain) | ✅ 완료 | IsProductionBuilding / GetStage / GetNextStage / CanUpgrade |
| BuildingData.Stage 파생 프로퍼티 | ✅ 완료 | BuildingType에서 도출, 별도 저장 없음 |
| BuildingStats.GetUpgradeCost + GetTotalInvestedCost | ✅ 완료 | 업그레이드 비용 조회 + 누적 투자비 캐시 |
| BuildingStatsConfig.upgradeCost 필드 | ✅ 완료 | 32종 BuildingType Inspector 설정 완료 |
| GameEvents.OnBuildingUpgraded | ✅ 완료 | BuildingUpgradedEvent(OldBuildingId, NewBuilding) |
| BuildingPlacementUseCase.UpgradeBuilding() | ✅ 완료 | 기존 BuildingData 제거 → 다음 단계 BuildingData 생성 |
| BuildingFactory.UpgradeBuildingObject() | ✅ 완료 | 새 GO 먼저 생성 → 기존 GO Destroy (빈 타일 방지) |
| NetworkBuildingController 업그레이드 RPC | ✅ 완료 | RequestUpgradeServerRpc / UpgradeBuildingClientRpc |
| ProductionPanelUI BuildingUnitMapping 구조 | ✅ 완료 | BuildingType별 유닛 라인업 + requiredStage 단계별 잠금 |
| ToastKey.UpgradeRequired + ToastMessageConfig | ✅ 완료 | 잠금 유닛 탭 시 "건물 업그레이드가 필요합니다" 토스트 |
| GameBootstrapper 누적 투자비 캐싱 | ✅ 완료 | 단계별 체인 순회 → BuildingStats._totalInvestedCostCache |
| 신규 3D 에셋 (HumanBarracks, AncientGrove, PrimalSanctuary) | ✅ 완료 | Blue/Red 프리팹 + 머티리얼 |

#### ProductionPopup UI 레이아웃 재구성 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingIconEntry 팀별 Sprite 분리 (blueIcon/redIcon) | ✅ 완료 | GetBuildingIcon(BuildingType, TeamId). Sprite 명명 규칙: bld_{type}_blue/red.png |
| 2유닛 건물 레이아웃 [유닛1][빈슬롯][유닛2] | ✅ 완료 | _unitButtonGroups (List<CanvasGroup>). 가운데 슬롯 alpha=0 숨김, 레이아웃 공간 유지 |
| UpdateButtonPortraits() 2유닛 슬롯 매핑 수정 | ✅ 완료 | 2유닛 시: slot0=list[0], slot2=list[1] (slot1 스킵). 이전 건물 초상화 잔존 버그 수정 |
| HeaderText 건물 이름 동적 표시 | ✅ 완료 | BuildingType.ToString() 기반. Show() 호출 시 갱신 |
| 철거 환불 누적 계산 | ✅ 완료 | 1단계 건설비 + 모든 업그레이드비 합산의 50%. BuildingStats.GetTotalInvestedCost() + GameBootstrapper 캐싱 |
| 2/3단계 건물 랠리 마커 미표시 버그 수정 | ✅ 완료 | ProductionTicker에 OnBuildingUpgraded 구독 추가. 전 종족 테스트 통과 |

#### 건물 철거 시스템 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitProductionUseCase.CancelAllQueue() | ✅ 완료 | 생산 큐 전체 취소 + IsCharged=true 항목 전액 환불 + UnregisterBarracks |
| ProductionPanelUI.OnDemolishButtonClick() | ✅ 완료 | 싱글: CancelAllQueue → AddGold(50%) → DemolishBuilding. 멀티: RequestDemolishServerRpc |
| BuildingPlacementUseCase.DemolishBuilding() | ✅ 완료 | OnEntityDied 발행 → RemoveBuilding 호출 |
| NetworkBuildingController — RequestDemolishServerRpc | ✅ 완료 | 소유권/Castle/존재 검증 후 철거 + DemolishBuildingClientRpc 동기화 |
| BuildingFactory — OnEntityDied 구독 (GO 파괴) | ✅ 완료 | B방식: 구독 1개 + _buildingObjects Dict O(1) 조회로 GO 파괴 |
| BuildingView.cs + MiningEffectView.cs 삭제 | ✅ 완료 | 미사용 코드 제거. BuildingFactory가 GO 파괴 책임 인수 |
| 채굴소(MiningPost) 철거 UI | ✅ 기본 완료 | BuildingActionPanelUI에서 철거 지원. 전용 패널(일시정지 등)은 별도 작업 예정 |

---

#### 비생산 건물 공용 액션 패널 UI (2026-05-18~19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `BuildingPanelBase.cs` 추상 베이스 신규 | ✅ 완료 | ProductionPanelUI / BuildingActionPanelUI 공통 부모. Template Method 패턴. |
| `BuildingActionPanelUI.cs` 신규 | ✅ 완료 | 비생산 건물 클릭 시 공용 팝업 (헤더 + 철거 버튼) |
| `ProductionPanelUI` BuildingPanelBase 상속 리팩토링 | ✅ 완료 | 공통 필드/메서드 베이스 이전. 외부 API 동일 유지 |
| `BuildingTypeHelper.CanShowActionPanel()` 추가 | ✅ 완료 | `!IsProductionBuilding && type != Castle` |
| `InputHandler` 분기 추가 | ✅ 완료 | CanShowActionPanel 분기 + ClosedFrame 체크 |
| `GameBootstrapper` 주입/등록 추가 | ✅ 완료 | UIManager 등록 + 비생산 건물 환불 캐시 루프 |
| `SetupBuildingActionPanelUI.cs` 에디터 스크립트 | ✅ 완료 | 씬 자동 생성 + 필드 배선 + GameBootstrapper 연결 |
| 싱글플레이 실기 테스트 | ✅ PASS | 채굴소/AutoTower 팝업 표시 + 철거 버튼 동작 확인 |

---

#### 인게임 설정 메뉴 + 게임 포기 기능 (2026-05-18~19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `InGameSettingsUI.cs` 신규 | ✅ 완료 | IGameUI 구현. Show() 싱글 일시정지(timeScale=0). Hide() 복원 + ConfirmPopup 닫기 |
| `ConfirmPopup.cs` 신규 | ✅ 완료 | 범용 확인 팝업. BlockingOverlay로 공유 Background 클릭 차단 |
| 포기 흐름 구현 | ✅ 완료 | 싱글: GameEndUseCase.Forfeit() / 멀티: NetworkGameEndController.ForfeitServerRpc |
| `GameEndUseCase.Forfeit()` 신규 | ✅ 완료 | IsGameOver=true + GameEvents.OnGameEnd(TeamId.Red) 발행 |
| `NetworkGameEndController.ForfeitServerRpc` 신규 | ✅ 완료 | RequireOwnership=false. _announced 재사용, AnnounceWinnerClientRpc 재사용 |
| `GameHudUI` 설정 버튼 연결 | ✅ 완료 | _settingsButton, _settingsUI 필드 + OnSettingsClicked() |
| `GameBootstrapper` 등록 | ✅ 완료 | _inGameSettingsUI, _confirmPopup SerializeField + UIManager 등록 |
| `SetupInGameSettingsUI.cs` 에디터 스크립트 | ✅ 완료 | HUD 재배치 + 설정 패널 생성 + 필드 배선 자동화 |
| AnimatedPanel._backgroundOverlay 배선 | ✅ 완료 | [UI]/Background CanvasGroup 연결 → 패널 열릴 때 반투명 배경 표시 |
| 싱글플레이 실기 테스트 | ✅ PASS | 설정 메뉴 열기/닫기, 일시정지, 포기 기능 확인 |

---

#### 코드 리팩토링 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| OnEntityDied 이벤트 분리 | ✅ 완료 | 단일 공용 이벤트 → OnUnitDied + OnBuildingDied 강타입 분리. 구독자 타입 필터(is-캐스팅) 전면 제거. 13개 파일 수정 |

---

#### 코드 리팩토링 전체 완료 (2026-05-24)

7개 그룹 전체 구현 완료. task 문서: `Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/`

| 그룹 | 내용 | 상태 |
|------|------|------|
| **그룹 1** — Slot/Occupancy 시스템 제거 | AttackPositionManager, TileOccupancyManager 등 ~600줄 삭제. 관련 주석/참조 전면 정리 | ✅ 완료 |
| **그룹 2** — Application→Core 의존성 제거 | IHexCoordinateMapper 인터페이스 신규. HexMetricsCoordinateMapper 구현체. Application이 Core HexMetrics 직접 참조 금지 | ✅ 완료 |
| **그룹 2-B** — Infrastructure→Core 의존성 분리 | HexCoordinateMapper 구현체 Infrastructure로 이동. 레이어 경계 완성 | ✅ 완료 |
| **그룹 3** — Presentation→NGO 의존성 제거 (카테고리 A~E) | Presentation 레이어에서 `using Unity.Netcode` 0건. Infrastructure에서 `using Hexiege.Presentation` 0건. ServerRpc 래퍼 메서드 패턴 도입. 11개 파일 수정 | ✅ 완료 |
| **그룹 4** — FindFirstObjectByType 캐시화 | 30+회 매 프레임 호출 → OnNetworkSpawn 시점 1회 캐시로 전환 (~12회 이하로 감소) | ✅ 완료 |
| **그룹 5** — O(n) 탐색 캐시화 | `_unitsByPosition`, `_buildingsByPosition`, `_ownedTileCounts`, `_usedPopulationByTeam` Dictionary 도입. GetUnitAt/GetBuildingAt/CountTilesOwnedBy/GetUsedPopulation → O(1) | ✅ 완료 |
| **그룹 6** — 가독성/유지보수 (15개 sub-task) | enum 명시값, 중복 생성자 제거, 메서드 분해, GameEvents Subject 허브 통일, ToastKey Application 이동, IUnitView 인터페이스, IsNetworkMode→NetworkContext.IsNetworkActive 전면 교체, TODO 토스트 해소 등 15항목 전부 완료 | ✅ 완료 |
| **그룹 7** — GameBootstrapper partial class 분리 | GameBootstrapper.cs / Setup.cs / Map.cs / Network.cs 4파일로 분리 | ✅ 완료 |

**인스펙터 수동 연결 필요** (에디터에서 직접 연결):
- `GameEndUI` → `_networkGameManager` SerializeField에 NetworkGameManager 오브젝트 연결
- `NetworkStatusUI` → `_networkGameManager` SerializeField에 NetworkGameManager 오브젝트 연결

---

#### 버그 수정 및 폴리싱
| 항목 | 상태 |
|------|------|
| 건물 배치 팝업 3행 버튼 가로폭 불일치 | ✅ 완료 (2026-05-19) — Human/Spirit(7개 건물) 시 3행 버튼 1개가 전체 가로폭 채우던 버그. SetActive(false) → CanvasGroup alpha=0 전환으로 HorizontalLayoutGroup 레이아웃 공간 보존. BuildingPlacementUI.cs 수정 |
| 건물 생성/파괴 시 유닛 이동 멈춤 | ✅ 완료 (2026-05-17) — OnPathInvalidated에서 코루틴 즉시 재시작 대신 _pendingPath 예약 방식 도입. 다음 타일 도착 시점에 부드럽게 경로 교체. 앞 타일에 건물이 생긴 경우만 즉시 재시작 (건물 관통 방지). UnitView.cs 단독 수정 |
| 랠리포인트 깃발 상대팀에도 표시되는 버그 | ✅ 완료 (2026-05-16) — RallyPointChangedEvent에 TeamId 추가, ProductionTicker에 팀 필터 추가. 멀티: 각 플레이어 자신의 깃발만 표시. 싱글플레이 영향 없음 |
| 랜덤 매칭 후 캐릭터 잘못 표시 버그 | ✅ 완료 (2026-05-15) — Lobby 씬 CharPreview 오브젝트가 실제 유닛 프리팹 인스턴스(NetworkTransform 포함)여서 Host 캐러셀 위치가 Red 클라이언트로 동기화되던 원인 확정. Unpack Completely + NetworkObject 계열 컴포넌트 5종 제거 |
| 자동생산 반복 순환 시 골드 미소모 (BUG-20) | ✅ 완료 (2026-04-04) — CompleteProduction IsCharged 리셋 누락 수정 |
| Pistoleer Idle 첫 프레임 동결 | ✅ 완료 (2026-04-06) — Pistoleer.controller Idle 상태 m_Speed: 0 → 1 수정 |
| Android 실기기 캐릭터 잔상 + RenderPass 에러 | ✅ 완료 (2026-04-06) — RT antiAliasing 2→1, Camera allowMSAA/allowHDR false, backgroundColor alpha 1 |
| 근접 공격 거리 다듬기 | ✅ 완료 (2026-04-11) — 유닛 vs 유닛 0.35f, 유닛 vs 건물 0.55f (타겟 타입별 분리) |
| 타겟 고정(Target Lock) 데미지 불일치 버그 | ✅ 완료 (2026-04-18) — 멀티플레이에서 애니메이션 타겟(B)과 다른 유닛(C)에게 데미지 적용되던 버그. NetworkCombatController.TickCombat() damageTargetId 분리로 수정 |
| 생산 슬롯 깜빡임 버그 | ✅ 완료 (2026-04-19) — 큐 비어있을 때 자동 등록 시 슬롯1→슬롯0 1프레임 이동. ToggleAutoProduction에서 즉시 TryStartNext 호출로 수정 |
| 랠리포인트 Client 무시 버그 | ✅ 완료 (2026-04-19) — 멀티플레이 Client(Red팀)에서 랠리포인트 설정이 서버에 전달되지 않던 버그. NetworkProductionController에 SetRallyPointServerRpc 추가, ProductionPanelUI에 네트워크 분기 추가 |
| 근접유닛 뒷무빙 현상 | ✅ 완료 (2026-04-26) — Phase 1 타겟 사망 시 무조건 Phase 2 진입으로 후방 스냅 발생. 타겟 사망 즉시 다음 적 재선택 + Phase 2 후방 스냅 방지 + 점유 누수 방지 (UnitView.cs 3곳 수정) |
| Phase 1 중 타일 소유권 미갱신 | ✅ 완료 (2026-04-26) — Phase 1(월드 직선 추적) 중 유닛이 타일을 지나가도 소유권이 갱신되지 않던 구조적 문제. TileOwnershipService(Pull 모델)로 매 프레임 물리 위치 기반 실시간 점령 |

---

### ⚠️ 알려진 미완성/버그 항목

#### 멀티플레이 기능 미구현
| 항목 | 파일 | 비고 |
|------|------|------|
| BuildFailedClientRpc UI 피드백 없음 | NetworkBuildingController | RPC 구조 완성, UI 기획 후 구현 예정 |
| EnqueueFailedClientRpc UI 피드백 없음 | NetworkProductionController | 싱글플레이 피드백 완료(2026-05-16). 멀티플레이 분기(RPC)는 별도 작업 예정 |
| 재접속 실제 구현 없음 | ReconnectionHandler | 30초 대기 후 ForceWin만 |
| 로비 UI 비주얼 폴리싱 | Lobby Views | UI 에셋 제작 완료 (2026-05-30) — 비주얼 폴리싱 작업만 잔여 |

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
| 유닛 AI 상태머신 | 낮음 | Phase 3 |
| 타임라인/서든데스 시스템 | 낮음 | Phase 3 |
| 사운드/BGM | 낮음 | Phase 4 |
| 튜토리얼 | 낮음 | Phase 4 |
| 게임 내 밸런싱 | 중간 | Phase 4 |
| 로그인 시스템 구현 (Login.unity) | 낮음 | Phase 4 |
| Firebase 백엔드 (랭킹/실시간 리더보드/IAP) | 낮음 | Phase 4 |
| 카드 수집 시스템 | 낮음 | Phase 4 |

---

## 기술 스택 현황

| 항목 | 기술 | 버전 |
|------|------|------|
| 게임 엔진 | Unity | 6000.0.x (Unity 6 LTS) |
| 렌더 파이프라인 | URP | Universal Render Pipeline |
| 네트워크 | Netcode for GameObjects | 2.9.2 |
| 멀티플레이 서비스 | Unity Multiplayer Services | 2.0.0 (Lobby+Relay 전용) |
| 인증 (로그인) | Firebase Authentication + Google Play Games Plugin | Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료 (런타임 설정 미완료) |
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
