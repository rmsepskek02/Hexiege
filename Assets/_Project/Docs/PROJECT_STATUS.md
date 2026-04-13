# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-04-13
**현재 단계:** 원거리 유닛 공격 중 회전 추적 + 타겟 고착성 + 부드러운 회전 전환 완료 (멀티플레이 실기 MULTI-001~007 전체 PASS) (근접 유닛 Castle 방향 접근 공격, 다중 유닛 연속 공격, 종족별 생산 패널 동적 바인딩) (건물 배치 시 숨김, 파괴 시 재표시+타일 중립 복원) / 종족 인게임 적용 완료 / 로비 종족 선택 UI(캐러셀 방식) 완료 / 종족 이름 자연→초월 변경 / Pistoleer Idle 애니메이션 버그 수정 / 전투 애니메이션 시스템 전면 재정비 완료 (6가지 규칙 확정, 3-신호 RPC, TickCombat 타이밍 정확도, 경쟁 조건 버그 수정) / 재경기 초기화 버그 수정 완료 (NGO 동적 NetworkObject 명시적 Despawn) / 공격 타이밍 정밀화 완료 / 이동 전 회전 타이밍 수정 완료 / 유닛 NGO NetworkObject 전환 + 이동/전투 동기화 완료 / Game UI Lifecycle Framework 완료 / 반투명 배경 오버레이 구조 개선 완료 / 자동/수동 생산 하이브리드 시스템 완성 / 멀티플레이 Phase 8 완료 / 3D 전환 완료 / 팀별 피아식별 프리팹 에셋 완료 / 신규 유닛 2종(Assault/Sniper) 에셋+코드 연동 완료 / 반응형 팝업 UI 완료 / 팀별 초상화 동적 업데이트 완료 / 공격 애니메이션-타격 동기화 완료 / 로비 씬 분리 MVVM 구조 완료 / 재경기 시스템 완료(커스텀+랜덤) / 건물 인근 이동/공격 버그 수정 완료 / 카메라 줌 DOTween 보간 완료 / UI DOTween 애니메이션 프레임워크 완료

---

## 전체 구현 현황

### ✅ 완료된 시스템

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 | 유닛 이동 시 자동 점령 |
| 금광 타일 시스템 | ✅ 완료 (2026-04-08 갱신) | HasGoldMine, 채굴소 건설 조건, 건물 배치 시 광산 숨김/파괴 시 재표시+타일 중립 복원 |
| A* 경로탐색 | ✅ 완료 (2026-03-18 갱신) | ClaimedTile 기반 아군 차단 (중간 타일만), 목표 타일 blocked 체크 제거 |
| 카메라 줌 보간 | ✅ 완료 (2026-03-19) | DOTween.To + Ease.OutCubic, _targetZoom 누적, _zoomDuration(0.25f) SerializeField |
| 유닛 이동 (Lerp) | ✅ 완료 | Per-step 가용성 체크, 재탐색 |
| 전투 시스템 | ✅ 완료 | IDamageable, 이동 중 자동 공격 |
| 전투 거리 정밀도 | ✅ 완료 (2026-03-18 갱신) | 월드좌표 기반, Epsilon=0.05f 추가 (인접 경계 부동소수점 오차 방지) |
| 공격 방향 정밀도 | ✅ 완료 (2026-03-07) | 타겟 실제 transform.position 기반 Atan2, 2D 레거시 제거 |
| 공격 쿨다운 시스템 | ✅ 완료 (2026-04-04 갱신) | 유닛별 AttackCooldown=클립 길이 (Assault=0.2s, Pistoleer=2.0s, Sniper=3.0s), elapsed 기반 정확한 감소 |
| 전투 애니메이션 시스템 (멀티플레이) | ✅ 완료 (2026-04-04) | 3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정, 사이클 동기화 |
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
| 종족+팀별 초상화 동적 업데이트 (ProductionPanelUI/BuildingPlacementUI) | ✅ 완료 (2026-04-12) | Show() 호출 시 종족+팀에 맞는 스프라이트 교체 (BuildingRacePortraitSet 6세트, Spirit miningPost Blue 미연결) |

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

#### 피격 시 부유 HP 텍스트 (2026-04-12~13)
| 항목 | 상태 | 비고 |
|------|------|------|
| FloatingHpText.cs (DOTween 애니메이션, 오브젝트 풀 반환) | ✅ 완료 | |
| FloatingHpTextSpawner.cs (이벤트 구독, 좌표 변환, 풀 관리) | ✅ 완료 | |
| FloatingHpText 프리팹 (Maplestory Light SDF, TMP + CanvasGroup) | ✅ 완료 | |
| 줌 기반 크기/위치 스케일링 | ✅ 완료 | orthographicSize 기준, Inspector 조정 가능 |
| 멀티플레이 클라이언트 표시 (NetworkHealthSync 재발행) | ✅ 완료 | |
| 입력 방해 없음 (blocksRaycasts=false, RaycastTarget=OFF) | ✅ 완료 | |
| SetupFloatingHpText 에디터 스크립트 자동화 | ✅ 완료 | Hexiege/Setup/FloatingHpText 설정 |
| 싱글/멀티 실기 테스트 | ✅ PASS | TC-FHT-01~07 전체 PASS |

#### 종족 인게임 적용 (2026-04-07)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | GameRaceContext 기반 (race, team) 튜플 switch |
| BuildingFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | MiningPost 포함 종족별 분기 |
| GameBootstrapper 싱글 GameRaceContext 초기화 | ✅ 완료 | LoadMap() 직전 Set(LocalPlayerRace.Current, Human) |
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
| 생산 패널/건물 배치 종족별 초상화 스프라이트 연결 | ✅ 완료 (2026-04-12) | Spirit/Transcendence 유닛+건물 초상화 Inspector 연결 완료 (Spirit Blue ManaRift 미제작 제외) |
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

#### 버그 수정 및 폴리싱
| 항목 | 상태 |
|------|------|
| 자동생산 반복 순환 시 골드 미소모 (BUG-20) | ✅ 완료 (2026-04-04) — CompleteProduction IsCharged 리셋 누락 수정 |
| Pistoleer Idle 첫 프레임 동결 | ✅ 완료 (2026-04-06) — Pistoleer.controller Idle 상태 m_Speed: 0 → 1 수정 |
| Android 실기기 캐릭터 잔상 + RenderPass 에러 | ✅ 완료 (2026-04-06) — RT antiAliasing 2→1, Camera allowMSAA/allowHDR false, backgroundColor alpha 1 |
| 근접 공격 거리 다듬기 | ✅ 완료 (2026-04-11) — 유닛 vs 유닛 0.35f, 유닛 vs 건물 0.55f (타겟 타입별 분리) |

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
