# Game Programmer — 네트워크 (NGO)

NGO 2.9.2 API 제약, RPC 패턴, 멀티플레이 동기화 패턴.
(상세 Phase 1~8 / 미완성 항목은 [network-infra.md], [network-todo.md], [random-matching-bugfix.md] 참조)

---

## NGO API 제약 (CRITICAL)

- ServerRpc/ClientRpc 메서드명: 반드시 `ServerRpc`/`ClientRpc`로 끝나야 함
- NGO 2.9.2, Enable Scene Management = ON
- NetworkBehaviour는 씬에 NetworkObject로 배치해야 RPC 작동
- RPC 파라미터: 직렬화 가능 타입만 (INetworkSerializable 또는 기본 타입/enum)
- NGO NetworkObject 부모 제약: 씬 루트에 생성 (일반 GameObject 하위 불가)
- 클라이언트 전용 분기: `NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer`
- NGO 2.9.x bool? (nullable) 비교: `IsSpawned == true` / `IsSceneObject == false` 방식 필수

---

## GO 파괴 전파 (CRITICAL)

- **`NetworkObject.Despawn(destroy: true)`**: 서버에서만 호출 가능
- `Destroy(gameObject)`는 NGO 클라이언트 전파 불보장 → 반드시 Despawn 명시 호출 (2026-06-08 확인)
- **유닛 사망 패턴**: NetworkCombatController.OnUnitDied()에서 EntityDiedClientRpc 발행 후 `UnitFactory.GetUnitObject(unitId)`로 GO 조회 → `NetworkObject.Despawn(true)`. 클라이언트는 `NetworkUnit.OnNetworkDespawn()`에서 이펙트 재생. UnitView(Presentation)는 Unity.Netcode 직접 참조 금지 — NetworkContext 홀더 패턴만

---

## 같은 씬 재로드 (재경기)

- `DestroyWithScene = true`는 같은 씬 재로드 시 동작 불보장
- `Active Scene Synchronization`은 씬 전환용 — 같은 씬 재로드와 무관
- 같은 씬 재로드 전 동적 NetworkObject 명시 Despawn 필수:
  - `SpawnedObjects.Values`를 `List<NetworkObject>` 복사본으로 순회 (Despawn 중 컬렉션 변경 방지)
  - `IsSceneObject == false`만 Despawn (씬 배치 오브젝트 자동 제외)

---

## RPC 래퍼 패턴 (레이어 분리)

Presentation에서 *ServerRpc 직접 호출 금지 → Infrastructure 컨트롤러에 래퍼 메서드:
- NetworkBuildingController: RequestBuild / RequestDemolish / RequestUpgrade
- NetworkProductionController: RequestEnqueue / RequestCancelSlot / RequestSetRallyPoint / RequestToggleAuto
- NetworkGameEndController: RequestForfeit / RequestRematch (IForfeitService 구현)

ServerRpc 검증 패턴: 소유권(팀) + 대상 존재 + (Castle 아님) + 골드 재검증 → 처리 → ClientRpc. ClientRpc 첫줄 `if (IsServer) return;` (호스트 이중 처리 방지).

---

## 동기화 타이밍

- NetworkSync 스폰 시 HexGrid/ResourceUseCase null 가능 → null 방어 필수
- ResourceUseCase 생성자는 OnResourceChanged 미발행 → SyncInitialGold() 필요
- ViewConverter.Setup()은 LoadMap() 이전 호출
- 클라이언트 등록 타이밍: WaitForUnitId 폴링 + ApplyStartWalkWithRetry (또는 OnValueChanged 콜백)

---

## 회전/위치 동기화 (유닛, 최종 2026-03-29)

- 위치: NGO NetworkTransform (서버 position → 클라 자동 보간)
- 회전: NetworkTransform SyncRotAngleY=true (서버 즉시 스냅 → 클라 보간)
- Walk/공격/사망: ClientRpc (이벤트 기반)
- Red 클라 보정: NetworkUnit.LateUpdate() (위치 반전 + Y축 +180°)
- **이중 보간 금지**: 서버 DORotate(0.3) + NetworkTransform(0.1) = ~1초 딜레이. 서버 즉시 스냅하면 NetworkTransform 보간만 적용

---

## 멀티플레이 이벤트 재발행

- NetworkHealthSync.SyncUnitHealth/SyncBuildingHealth: TakeDamage 후 `GameEvents.OnEntityDamaged.OnNext()` 재발행 (클라에서 FloatingHpTextSpawner 반응). diff>0인 경우만 (중복 방지)
- 클라이언트는 GameEvents.OnGameEnd 미발행 설계 → AnnounceWinnerClientRpc에서 직접 처리/재발행

---

## 멀티플레이 TC 주의

- 멀티 TC는 에디터(Host)+빌드(Client) 구성 필요 → QA 에이전트 단독 실기 불가. 해당 TC에 "에이전트 실기 불가 — 사용자 확인 필요" 표기 후 정적 분석만
- 공격 VFX는 OnAttackHit() Animation Event 기반 (GameEvents.OnEntityAttacked는 서버 전용이라 멀티 클라 미도달)

---

## 멀티 데미지/타겟 주의

- Target Lock: 애니메이션 타겟(`_unitCombatTargets`)과 데미지 타겟(`targetId`)은 항상 일치. IsCurrentTargetStillValid로 애니 유지 시 데미지도 같은 타겟에
- 싱글플레이 데미지는 네트워크 시 완전 차단 (HOST 이중 데미지 방지)
