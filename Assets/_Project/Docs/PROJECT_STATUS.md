# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-03-07
**현재 단계:** 멀티플레이 Phase 8 완료 / 3D 전환 완료 / 공격 방향 Transform 기반 복구 완료 / 유닛별 AttackCooldown 완료

---

## 전체 구현 현황

### ✅ 완료된 시스템

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 | 유닛 이동 시 자동 점령 |
| 금광 타일 시스템 | ✅ 완료 | HasGoldMine, 채굴소 건설 조건 |
| A* 경로탐색 | ✅ 완료 | ClaimedTile 기반 아군 차단, 적군 투과 |
| 유닛 이동 (Lerp) | ✅ 완료 | Per-step 가용성 체크, 재탐색 |
| 전투 시스템 | ✅ 완료 | IDamageable, 이동 중 자동 공격 |
| 전투 거리 정밀도 | ✅ 완료 (2026-03-02) | 월드좌표 기반 (IEntityPositionProvider) |
| 공격 방향 정밀도 | ✅ 완료 (2026-03-07) | 타겟 실제 transform.position 기반 Atan2, 2D 레거시 제거 |
| 공격 쿨다운 시스템 | ✅ 완료 (2026-03-07) | 유닛별 AttackCooldown, Attack 클립 길이 자동 설정 |
| 건물 배치 (Castle/Barracks/MiningPost) | ✅ 완료 | 건설 검증, 영토 확장 |
| 자원 시스템 (골드) | ✅ 완료 | 채굴소 수입, 건물/유닛 비용 |
| 인구 시스템 | ✅ 완료 | 타일 수 = 최대 인구 |
| 유닛 생산 (수동/자동) | ✅ 완료 | 큐 최대 3, 롱프레스 자동 |
| 랠리포인트 | ✅ 완료 | 마커 표시, BFS 빈 타일 탐색, 위치/회전 Inspector 조정 (GameConfig.RallyMarkerOffset/Euler) |
| 공성 시스템 | ✅ 완료 | 랠리→Castle 방향 자동 진군 |
| 승패 판정 (Castle 파괴) | ✅ 완료 | GameEndUseCase, UI 표시 |

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

#### 멀티플레이 (Phase 1~8)
| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 1 | Lobby/Relay/NGO 연결 인프라 | ✅ 완료 |
| Phase 2 | 팀 할당 + 게임 시작 흐름 | ✅ 완료 |
| Phase 3 | 타일/자원 동기화 (NetworkTileSync, NetworkResourceSync) | ✅ 완료 |
| Phase 4 | 건물 배치 동기화 (NetworkBuildingController) | ✅ 완료 |
| Phase 5 | 유닛 생산 동기화 (NetworkProductionController) | ✅ 완료 |
| Phase 6 | 유닛 이동 + 전투 동기화 (NetworkUnitMovementController, NetworkCombatController) | ✅ 완료 |
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

---

### ⚠️ 알려진 미완성/버그 항목

#### 멀티플레이 기능 미구현
| 항목 | 파일 | 비고 |
|------|------|------|
| BuildFailedClientRpc UI 피드백 없음 | NetworkBuildingController | 현재 로그만 출력 |
| EnqueueFailedClientRpc UI 피드백 없음 | NetworkProductionController | 현재 로그만 출력 |
| InputHandler 이동 네트워크 분기 누락 | InputHandler.cs L261 부근 | 탭 이동이 상대방 화면에 미동기화 |
| 자동생산 멀티플레이 미지원 | ProductionPanelUI | 롱프레스 시 로그 경고만 |
| 생산 큐 클라이언트 UI 지연 | NetworkProductionController | 큐 추가 즉시 전파 안됨 |
| 재접속 실제 구현 없음 | ReconnectionHandler | 30초 대기 후 ForceWin만 |
| 멀티플레이 로비 UI 미완성 | LobbyUI | 기본 기능만 |

#### GameConfig 코드 기본값 vs Inspector 값
- AnimationFps 필드 제거됨 (2026-03-02)
- TileHeight 코드 기본값 수정 완료: PointyTop=0.866, FlatTop=0.866
- FlatTop GridHeight 코드 기본값 수정 완료: 20
- CameraZoomDefault 수정 완료: 7

---

### ❌ 미구현 기능

| 기능 | 우선순위 | 관련 Phase |
|------|---------|-----------|
| 3종족 시스템 | 중간 | Phase 3 |
| 추가 유닛 타입 (현재 Pistoleer 1종) | 중간 | Phase 3 |
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
  └── UI/ (HUD, 생산 패널, 건물 배치, 게임 종료)
```

---

## 에셋 현황

### 3D 모델 (Meshy.ai)
| 에셋 | 경로 | 상태 |
|------|------|------|
| Pistoleer 유닛 | Models/Units/Pistoleer/ | ✅ 완료 (Walk/Attack/Dead 애니메이션) |
| Castle | Models/Buildings/Castle/ | ✅ 완료 |
| Barracks | Models/Buildings/Barracks/ | ✅ 완료 |
| MiningPost | Models/Buildings/MiningPost/ | ✅ 완료 |
| GoldMineTile | Prefabs/Misc/GoldMineTile.prefab | ✅ 완료 |
| RallyPointMarker | Prefabs/Misc/RallyPointMarker.prefab | ✅ 완료 |

### 타일
| 에셋 | 경로 | 상태 |
|------|------|------|
| HexTile (FlatTop) | Prefabs/Tiles/HexTile.prefab | ✅ 완료 (ProBuilder + SG_HexTile) |

### 미제작 에셋
| 에셋 | 용도 |
|------|------|
| 추가 유닛 모델 | 3종족/다양한 유닛 타입 |
| 방어타워/마법타워/연구소 3D | 미구현 건물 타입 |
