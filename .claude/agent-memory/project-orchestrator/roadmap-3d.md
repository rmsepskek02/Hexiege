# 3D 전환 + 네트워크 점검 로드맵 (2026-02-27 확정)

## 전제 확정 사항
- 헥스 타일: Meshy.ai 3D 에셋 제작
- 건물(Castle/Barracks/MiningPost): Meshy.ai 3D 에셋 제작
- 카메라: Orthographic + X축 틸트 (각도는 Phase 3에서 테스트)
- 좌표 평면: XZ 평면 (Y=높이)
- 우선순위: 3D 전환 먼저 → 이후 3종족/AI 기능 추가

---

## Phase 0: 네트워크 점검 (3D 전환 전 선행 추천)

### 배경
현재 멀티플레이 Phase 8까지 구현되어 있으나 코드 분석 결과 미완성 항목과 잠재 버그가 발견됨.
3D 전환 이후에도 동일 문제가 잔존하므로, 전환 전 정리하거나 병렬로 진행.

### 점검 항목 목록

#### A. 알려진 버그 (즉시 수정 권장)
1. **InputHandler 유닛 이동 네트워크 분기 누락** [심각도: 상]
   - 위치: `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` L261
   - 증상: 멀티플레이에서 유닛을 탭해 이동 시 자신 화면만 반영, 상대방에게 동기화 안 됨
   - 원인: `_unitMovement.RequestMove()` + `selectedView.MoveTo()` 직접 호출
     NetworkUnitMovementController.RequestMove() 경유 없음
   - 수정 방향: NetworkManager.Singleton?.IsConnectedClient 분기 추가,
     네트워크 모드에서 `_networkMovement.RequestMove(unit, target, _unitFactory, _unitMovement)` 호출
   - 연관 파일: `InputHandler.cs`, 의존성 주입 시 `_networkMovement` 참조 필요

2. **NetworkGameEndController 씬명 하드코딩** [심각도: 중]
   - 위치: `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs` L61
   - 증상: `_lobbySceneName = "SampleScene"` 하드코딩 → 실제 씬명과 다르면 게임 종료 후 씬 로드 실패
   - 수정 방향: 실제 로비 씬 이름으로 Inspector 값 변경 (또는 GameConfig에 씬명 추가)

#### B. UI 피드백 미구현 (TODO 항목)
3. **BuildFailedClientRpc 피드백 없음** [심각도: 하]
   - 위치: `NetworkBuildingController.cs` BuildFailedClientRpc 메서드
   - 증상: 건물 배치 실패 시 사용자 화면에 아무 반응 없음 (로그만 출력)
   - 수정 방향: 간단한 토스트 메시지 또는 버튼 쉐이크 효과 추가

4. **EnqueueFailedClientRpc 피드백 없음** [심각도: 하]
   - 위치: `NetworkProductionController.cs` EnqueueFailedClientRpc 메서드
   - 수정 방향: 동일 패턴

#### C. 기능 미지원 항목
5. **멀티플레이 자동생산 롱프레스 미지원** [심각도: 하]
   - 위치: `ProductionPanelUI.cs` OnPistoleerLongPress
   - 현 상태: "멀티플레이 미지원" 로그 후 return
   - NetworkProductionController에 ToggleAutoServerRpc 이미 구현됨
   - 수정 방향: `_networkProductionController.ToggleAutoServerRpc(barracksId, teamIndex)` 호출로 교체

6. **재접속 실제 흐름 미구현** [심각도: 중]
   - 현 상태: 연결 끊김 → 30초 대기 → ForceWin만 구현
   - 실제 재접속(씬 재진입 후 게임 상태 복원) 흐름 없음
   - 3D 전환 후 별도 Phase로 구현 예정

#### D. 3D 전환 후 새로 생길 네트워크 관련 이슈
7. **ViewConverter Z축 반전 전파**
   - 현재 ViewConverter.ToView(): X/Y 반전 → 3D 전환 후 X/Z 반전으로 변경 필요
   - 영향 범위: HexGridRenderer, BuildingFactory, UnitFactory, UnitView, InputHandler, ProductionTicker
   - 네트워크 환경: 서버(Host=Blue)와 클라이언트(Red) 모두 각자 ViewConverter 적용 → 변경 없음

8. **3D 위치 동기화 (Z 좌표)**
   - NetworkUnitMovementController: 경로는 HexCoord(Q,R)로 전송 → 서버/클라이언트 각자 HexToWorld 변환
   - 3D 전환 후 HexToWorld()가 XZ 평면 반환하도록 변경되면 자동 대응됨 → 별도 수정 불필요

9. **SyncMovementClientRpc 경로 직렬화**
   - 현재: int[] pathQ, pathR 배열 전송 → 3D에서도 동일 방식 사용 가능 (Y좌표 없음, 계산으로 복원)

### Phase 0 예상 수정 파일 수: 3~5개
### 핵심 리스크: InputHandler 수정 시 싱글플레이 경로 훼손 위험

---

## Phase 1: 좌표계 전환 (XY → XZ)

### 목표
헥스 좌표→월드 좌표 변환 공식을 XY 평면에서 XZ 평면으로 전환.
이 Phase 완료 후 게임이 실행 가능하나 타일/건물/유닛이 3D 메시 없이 보이지 않음.

### 수정 파일 (5개)

| 파일 | 변경 내용 | 레이어 |
|------|----------|--------|
| `Assets/_Project/Scripts/Core/HexMetrics.cs` | HexToWorldFlatTop/PointyTop: `y = -row * ...` → `z = row * ...` / `return new Vector3(x, 0, z)` | Core |
| `Assets/_Project/Scripts/Core/ViewConverter.cs` | ToView(): Y 반전 → Z 반전 (X/Z 반전, Y=0 보호) / FlatTopSortingOrder 제거 (3D 불필요) | Core |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | SetCameraStartPositionForTeam(): `startPos.z = 0f` → Z가 깊이축이므로 Y를 틸트에 맞게 조정 / SetupCamera(): size Z→Y 클램프 | Bootstrap |
| `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs` | SetPosition(): Z 보존 (틸트 카메라이므로 Z 고정 유지) / ClampPosition(): y→z 클램프 | Presentation |
| `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` | ScreenToWorldPoint → Physics.Raycast(XZ 평면 교차점) 방식 교체 | Presentation |

### 검증 방법
HexMetrics.HexToWorld() 결과를 로그로 찍어 Z 좌표가 올바른지 확인.

### Phase 1 예상 수정 파일 수: 5개
### 핵심 리스크: HexMetrics.WorldToHex() 역변환도 Z 기반으로 수정 필요 (입력 역변환에 사용)

---

## Phase 2: 렌더링 전환 (Sprite → Mesh)

### 목표
2D SpriteRenderer 기반 렌더링을 3D MeshRenderer 기반으로 전환.
이 Phase 완료 후 3D 메시로 타일/건물/유닛이 렌더링됨.

### 수정 파일 (6개 + 프리팹 재설계)

| 파일 | 변경 내용 | 레이어 |
|------|----------|--------|
| `Assets/_Project/Scripts/Presentation/Grid/HexGridRenderer.cs` | 타일 프리팹에서 SpriteRenderer → MeshRenderer 참조 / sortingOrder 제거 / 3D Mesh 프리팹 사용 | Presentation |
| `Assets/_Project/Scripts/Presentation/Grid/HexTileView.cs` | SpriteRenderer.color → MeshRenderer.material.color / 콜라이더 PolygonCollider2D → MeshCollider 또는 BoxCollider | Presentation |
| `Assets/_Project/Scripts/Infrastructure/Factories/BuildingFactory.cs` | SpriteRenderer sortingOrder 제거 / MeshRenderer 또는 3D 프리팹 사용 / Y 오프셋 → Y 높이로 변경 | Infrastructure |
| `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs` | sortingOrder 제거 / Instantiate 위치 XZ 평면 기준 | Infrastructure |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 전면 재작성: FrameAnimator → Animator / SpriteRenderer.flipX → Y축 rotation / XZ 이동 | Presentation |
| `Assets/_Project/Scripts/Presentation/Unit/FrameAnimator.cs` | 폐기 (3D에서 불필요) | Presentation |

### 프리팹 변경 사항
- HexTile_FlatTop.prefab: SpriteRenderer → MeshRenderer (Meshy.ai FBX 연결)
- HexTile_PointyTop.prefab: 동일
- Castle.prefab: SpriteRenderer → MeshRenderer (Meshy.ai FBX)
- Barracks.prefab: 동일
- MiningPost.prefab: 동일
- Unit_Pistoleer.prefab: SpriteRenderer + FrameAnimator → MeshRenderer + Animator

### UnitAnimationData ScriptableObject 재설계
- 현재: `Sprite[] idleFrames, walkFrames, attackFrames` (방향별)
- 변경: `AnimatorController pistoleerController` + Mixamo 클립 참조

### Phase 2 예상 수정 파일 수: 6개 코드 + 6개 프리팹 + ScriptableObject 재설계
### 핵심 리스크: UnitView 전면 재작성이 가장 위험한 작업 (이동/전투/애니메이션 통합 컴포넌트)

---

## Phase 3: 카메라 전환 및 각도 테스트

### 목표
카메라를 XZ 평면 기준 Orthographic 틸트 방식으로 전환하고,
45도/55도/60도 각도를 테스트 씬에서 비교하여 최적 각도 결정.

### 수정 파일 (2개)

| 파일 | 변경 내용 | 레이어 |
|------|----------|--------|
| `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs` | 팬: ScreenToWorldPoint → XZ 평면 레이캐스트 방식 / ClampPosition(): Y → Z 클램프 / SetPosition(): Z 고정 | Presentation |
| `Assets/_Project/Scripts/Presentation/Input/InputHandler.cs` | 이미 Phase 1에서 레이캐스트로 전환됨 → 각도 변경에 따른 미세 조정 | Presentation |

### 카메라 각도 테스트 씬 구성
- 테스트 씬에 카메라 3개 배치 (45/55/60도)
- 각 카메라에서 동일한 맵 렌더링 비교
- 결정 기준: 헥스 타일 판독성 + 캐릭터 디테일 가시성 + 원근 왜곡 최소화

### 레이캐스트 팬 구현 방향
```csharp
// 기존 ScreenToWorldPoint 방식 → 레이캐스트 방식
Ray ray = _cam.ScreenPointToRay(mousePos);
Plane xzPlane = new Plane(Vector3.up, Vector3.zero); // Y=0 XZ 평면
if (xzPlane.Raycast(ray, out float dist))
{
    Vector3 hitPoint = ray.GetPoint(dist);
    // hitPoint가 XZ 평면의 월드 좌표
}
```

### Phase 3 예상 수정 파일 수: 2개
### 핵심 리스크: Orthographic 카메라 틸트 시 ClampPosition 로직이 복잡해짐
  - 카메라가 X축으로 기울어지면 XZ 클램프 기준이 달라짐

---

## Phase 4: 에셋 연동

### 목표
Meshy.ai에서 제작한 FBX 에셋을 Unity에 임포트하고
프리팹과 연결하여 최종 비주얼 완성.

### 에셋 현황
- 현재 위치: `Assets/Resources/3DModel/`
- 존재 파일: Idle/Walk/Dead/Running FBX 5종 + pistol.controller
- 미제작: 헥스 타일 3D 메시, Castle, Barracks, MiningPost

### 연동 작업 목록
1. Meshy.ai에서 헥스 타일 FBX 익스포트 → Unity 임포트
2. 건물 3종(Castle/Barracks/MiningPost) FBX 임포트
3. FBX Import Settings: Scale Factor, Normals 설정
4. 프리팹 MeshRenderer에 FBX 메시 연결
5. 건물별 팀 색상 머티리얼 분기 (Blue/Red 팀)
6. 유닛 Animator Controller 에 Mixamo 클립 연결 (Idle/Walk/Attack)
7. Mixamo Attack 클립 선정 (사격 애니메이션)

### 에셋-코드 연결 포인트
- BuildingFactory: 팀별 머티리얼 분기 (`data.Team == TeamId.Blue ? blueMat : redMat`)
- UnitView: Animator 파라미터 키값 (예: "IsWalking", "Attack") 코드-에셋 매핑

### Phase 4 예상 작업: Unity 에디터 작업 중심, 코드 수정 최소 (1~2개)
### 핵심 리스크: FBX 스케일 불일치 (HexMetrics.TileWidth=1.0 기준 타일 크기 맞추기)

---

## 병렬 진행 가능 여부

| 작업 | 코드 작업 | 에셋 작업 | 병렬 가능 |
|------|----------|----------|----------|
| Phase 0 네트워크 점검 | O | X | Phase 1과 병렬 가능 |
| Phase 1 좌표계 전환 | O | X | Phase 0과 병렬 가능 |
| Phase 2 렌더링 전환 | O | O(프리팹) | Meshy.ai 에셋 제작과 Phase 1 병렬 가능 |
| Phase 3 카메라 | O | X | Phase 2 완료 후 |
| Phase 4 에셋 연동 | O(최소) | O | Phase 3 완료 후 |

**추천 병렬 작업 조합:**
- 코드 팀: Phase 0 네트워크 점검 + Phase 1 좌표계 전환
- 에셋 팀(Meshy.ai): 헥스 타일 + 건물 3종 제작
- Phase 1 코드 완료 후 Phase 2 시작, 에셋 완료 시 Phase 4 병합

---

## 각 Phase 완료 기준

| Phase | 완료 기준 |
|-------|----------|
| Phase 0 | 멀티플레이 유닛 이동 동기화 확인, UI 피드백 정상 동작 |
| Phase 1 | HexMetrics.HexToWorld() XZ 반환 확인, 타일 좌표 로그 정상 |
| Phase 2 | 3D 메시로 타일/건물/유닛 렌더링, 이동/전투 정상 동작 |
| Phase 3 | 레이캐스트 팬/줌 정상, 카메라 각도 결정 완료 |
| Phase 4 | Meshy.ai 에셋 인게임 적용, 팀별 색상/머티리얼 구분 |
