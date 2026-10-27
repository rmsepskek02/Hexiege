# Hexiege — 네트워크 기술 설계

**문서 역할**
- **목적·질문:** 네트워크 구현 구조·데이터 계약·실패 처리 방법을 설명한다.
- **포함:** Netcode·권위/복제 구조·맵 전송·서버 권위 투사체 전송·시간·이탈 판정·치팅 방어 설계.
- **제외·담당:** 사용자에게 보이는 동작은 [네트워크 규칙](GameSystemRules/GameSystemRules_Network.md)·[경기 운영 규칙](GameSystemRules/GameSystemRules_MatchFlow.md), 실행 결과는 [현황](PROJECT_STATUS.md)·[이력](History/ImplementationVerificationHistory.md).
- **원본·참조:** 상세 네트워크 기술 원본. 공통 구조는 [TDD](TechnicalDesignDocument.md).
- **갱신:** 승인된 계약이나 실제 구현 구조가 바뀌면 해당 기술 절을 갱신한다. 회차별 전송/실기 수치는 이력에 둔다.

공통 계층·개발용 강제 실패 사용법은 [공통 TDD](TechnicalDesignDocument.md), 제품 동작과 실패 화면은 [네트워크 규칙](GameSystemRules/GameSystemRules_Network.md)·[경기 운영](GameSystemRules/GameSystemRules_MatchFlow.md)·[경기 UI](GameSystemRules/GameSystemRules_MatchFlowUI.md)를 따른다. 이 기술 문서는 구현 구조와 현재 데이터 계약을 설명하며 실행·수용 결과를 대신하지 않는다.

<a id="네트워크-설계"></a>

## 🌐 네트워크 설계

### 전투 결과 표현·지속 피해 전달 계약

`TowerCombatUseCase`가 확정한 결과 identity·Impact 위치/시각·Race snapshot을 `GameEvents`와 `NetworkCombatController`가 표현용으로 전달한다. 현재 대상 재조회나 tracer 도착으로 서버 피해를 새로 만들거나 지연하지 않는다.

Recipient poison/burn은 실제 writer의 `(target lifetime, TimedEffectKind.Damage)` 단일 레코드에 identity/revision/poison-burn provenance를 연결한다. network current value·late-init/restoration/shutdown은 single presenter로 전달하며, 동일 owner refresh의 noRestart·overwrite 종료/교체·취소/만료/사망 후 StopEmitting/drain/lease 반환을 구분한다.

위 현행 기술계약의 승인 원문과 당시 진행 단계는 [구현·검증 이력의 TDD 원문](History/ImplementationVerificationHistory.md#tdd-handoff)에 보존했다. preset 키·Mushroom marker와 에셋 연결 사양은 [VFXSFXList.md](Assets/VFXSFXList.md#2026-10-10-확정-연결-계약과-진행-단계), 현재 개별 연결·검증 범위는 해당 목록과 [UnitCombatAssetMatrix.md](Assets/UnitCombatAssetMatrix.md)에서 확인한다. 이 기술계약 요약은 실기 수용이나 전체 migration 완료를 선언하지 않는다.

<a id="server-authoritative-projectile-transport"></a>

### 서버 권위 투사체 전송 계약

투사체 유닛 7종(Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher · MushroomBomber)과 Human CannonTower는 **서버가 발사 · 비행 · 착탄을 소유**한다. 제품 동작은 [동기화 규칙](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) 「7. 전달 방식별 동기화」 `NET-DELIVERY-PROJECTILE`과 [유닛 규칙](GameSystemRules/GameSystemRules_Units.md) 규칙 17 · 19 · 20, 결정 · 회차 기록은 [서버 권위 투사체 Task](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md)가 원본이다. 위 절의 `TowerCombatUseCase` 스냅샷 계약은 투사체가 아닌 타워(Spirit RuneSpire · Transcendence VineTower)의 즉발 경로다.

- **분류:** `ServerProjectileDeliveryPolicy`(`Application/Combat/ProjectileDeliveryContracts.cs`)의 코드 상수 `ProjectileUnitTypes` · `ProjectileTowerRaces`가 단일 소스다(`IsProjectileUnit` · `IsProjectileTower`). Inspector 값이 아닌 이유는 Host와 Client의 분류가 다르면 한쪽은 로컬 트레이서, 다른 쪽은 서버 투사체를 그려 이중 재생이 생기기 때문이다. 이행 기간의 유닛별 스위치는 2026-10-27 P5에서 없어졌다.
- **발사 · 착탄 시각:** 공격의 `OnAttackHit` offset이 서버 발사 시각이다. 발사 때 착탄 시각 = 발사 + (중심 거리 − 대상 반경) ÷ 속도를 **한 번 계산해 고정**하고(`UnitCombatUseCase.Projectiles.cs` 발사 기록 `ImpactTime`), 비행은 매 프레임 진행한다. 속도 · 반경은 서버 전용 `Resources/Config/ProjectileConfig.asset`.
- **단일 writer:** 착탄 때 기존 피해 writer를 한 번 부른다 — 유닛 `ApplyAttackDamageCore`, 타워 `IBuildingProjectileImpactWriter`. 전송 계층(`NetworkCombatController.Projectiles.cs`)은 판정하지 않고 HP를 쓰지 않는다 — Client HP는 기존 `NetworkHealthSync`가 맞춘다.
- **RPC:** 서버 UseCase의 `ProjectileLaunched` · `ProjectileResolved` · `ProjectileLaunchCancelled` 이벤트를 각각 `ProjectileLaunchClientRpc`(발사 기록 1건 — epoch · id · 공격자 · 대상 · 위치 · 반경 · 발사/착탄 시각) · `ProjectileImpactClientRpc`(착탄 결과 1건 — 결과 · 착탄 지점 · 대상 · HP · 피해량 · 시각) · `ProjectileLaunchCancelClientRpc`(발사 전 취소 통보 1건 — 공격자 · 종류 · 사유 · 공격 표현 회차 · 시각)로 그대로 전달한다. 이 셋 앞에 `BeginProjectileEpochClientRpc`(이 경기 UseCase 수명 번호 — 다른 epoch 기록은 버린다)가 붙는다. 같은 `epoch:id` 중복은 화면 소비자(`ProjectilePresentationCoordinator`)가 거부한다.
- **늦은 접속:** Client가 `RequestProjectileSnapshotServerRpc`를 한 번 보내면 서버가 그 Client에게만 epoch(스냅샷 건수 포함) + **지금 비행 중인** 발사 기록을 다시 보낸다. 취소 통보는 스냅샷에 넣지 않는다. 이 경로는 실기로 관측하지 않았다(사용자 결정으로 실기 테스트 생략).
- **표시 시각:** 화면(`ProjectilePresentationPresenter`)은 서버 시계에서 **순수 Client는 0.10초**(`AuthoritativeAttackPresentationCoordinator.PresentationDelaySeconds` 공유), **Host · 싱글은 0초**를 뺀 시각으로 그린다(`NET-TIME-002`). 피해 숫자 · 피격 표현 · 사망타의 사망 표현 · 화상/독 VFX 시작은 그 화면 착탄까지 기다린다(보류 상한 0.3초). 서버 판정 · 도메인 제거 · Despawn은 지연하지 않는다.
- **경기 시작 호환 확인:** Host가 만드는 표현 프로필 해시 원문은 `c3-schema=<스키마>|server-projectile=<분류 토큰>|…유닛 프로필…`이다(`UnitAttackShadowProfileResolver.TryComputePresentationProfileHash`). 분류 토큰 `ManifestToken` = 투사체 유닛 번호 오름차순 + `;tower:` + 투사체 타워 종족 — 지금 `6,7,12,17,21,24,26;tower:human`. 스키마 `PresentationContractSchemaRevision` = **6**. Client 준비 신호의 스키마 · 해시가 다르면 Host가 시작을 거부하고 그 Client를 끊는다(`NetworkGameFlow`). 따라서 분류가 다른 빌드끼리는 경기가 시작되지 않는다. **RPC 모양이 바뀌면 스키마를 올린다**(선례: 4 → 5 스냅샷 건수 인자, 5 → 6 취소 통보 RPC). 늦은 접속 Client가 같은 확인을 거치는지는 코드를 읽지 않았다.
- **진단(개발 빌드 전용):** 경기 종료 때 `[PROJECTILE-END]` 줄을 남긴다 — 서버 `side=server` v=2 · `side=server-tower` v=1 · `side=server-charge` v=2, 화면 `side=view` v=3(2줄). 기록 메서드는 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`라 릴리스 빌드에서 사라지고 판정에 쓰이지 않는다. 칸 설명은 `UnitCombatUseCase.ProjectileDiagnostics.cs` · `ProjectilePresentationPresenter.cs` 머리 주석, 형식 변경 이력은 [Research 20-9](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#20-9-p5-results).

이 절은 구조와 계약 요약이며 실기 수용을 선언하지 않는다 — 회차별 결과는 위 Task와 [현황](PROJECT_STATUS.md). *(2026-10-27 신설 — 서버 권위 투사체 Plan 11-3 P5-H 사용자 승인. 근거: 이 checkout의 코드 머리 주석 · 상수를 다시 읽음)*

<a id="네트워크-동작-규정은-별도-문서로-이관됐다--최상위-원칙--이탈-시-승패-기록--인게임-이탈-처리-2026-10-12"></a>

네트워크 기술은 [Host/Client 제품 일관성·이탈·승패 규칙](GameSystemRules/GameSystemRules_Network.md)의 적용을 받는다. 경기 운영은 [MatchFlow](GameSystemRules/GameSystemRules_MatchFlow.md), 화면은 [MatchFlowUI](GameSystemRules/GameSystemRules_MatchFlowUI.md)가 원본이며 이 문서에는 구조·데이터·복구 방법만 둔다. [이관 당시 역할 판정](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-net-role-migration).

### Netcode for GameObjects

#### 아키텍처
```
클라이언트 A ←→ Unity Relay ←→ 클라이언트 B
                (NAT 관통)
```

**특징**:
- P2P 방식 (Host-Client 모델)
- Host가 서버 역할 (Authoritative)
- Unity Relay로 NAT 관통 자동 처리

#### 동기화 전략

**NetworkVariable (자동 동기화)**:
```csharp
// 자원 (서버 → 클라이언트)
NetworkVariable<int> resources = new NetworkVariable<int>(
    value: 1000,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

**ServerRpc (클라이언트 → 서버)**:
```csharp
[ServerRpc(RequireOwnership = false)]
void SpawnUnitServerRpc(UnitType type, Vector3 position) {
    // 서버에서 검증 + 실행
}
```

**ClientRpc (서버 → 클라이언트)**:
```csharp
[ClientRpc]
void ShowEffectClientRpc(Vector3 position) {
    // 모든 클라이언트에 이펙트 표시
}
```

#### 동기화 대상
| 데이터 | 동기화 방식 | 빈도 |
|--------|------------|------|
| **초기 무작위 맵** | Host 생성·검증 → 공용 `NetworkMapTransfer`의 1KB chunk 전송 → Client SHA-256·semantic 검증/ACK (⚠️ 축약 표기 — 전송 객체의 **수명 규정**은 아래 「`NetworkMapTransfer` 전송 프로토콜」 절이 단일 소스다) | 경기 시작 1회 |
| **건물 건설** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **유닛 생성** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **타일 점령** | NetworkList<TileOwnership> | 변경 시 |
| **자원** | NetworkVariable<int> | 변경 시 |
| **본기지 체력** | NetworkVariable<int> | 변경 시 |
| **유닛 이동·SimulationFacing** | 서버 권위 planner·trajectory/checkpoint·phase 복제. Client reducer/Simulation Root write 금지. 현재 이행·수용은 Matrix 참조 | 연속 |
| **유닛 행동 상태** | 값 기반 행동 상태·pose·애니메이션 계약은 전투 동기화 규칙을 따른다. 현재 구조 이행과 개별 수용은 별개다 | 상태 변경 시 |
| **공격 타격 결과** | 행동 회차·타격 키의 권위 결과와 표현을 분리한다. Legacy FIFO는 전환 경로로 구별한다 | Impact 시 |

맵 전송 수명은 Lobby/Game 씬 객체에 귀속시키지 않는다. persistent가 가리키는 것은 network connection이며 전송 객체의 영구 생존을 뜻하지 않는다. 회차별 동적 전송 객체도 이 수명 계약을 따른다. [이전 축약 표기·구조 인계](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-transfer-01). 유닛 상세는 [동기화 계약](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md), 현재 연결·이행은 [Matrix](Assets/UnitCombatAssetMatrix.md)를 따른다.

#### 무작위 맵 시작 동기화 (확정 설계, 1~3단계 구현 · 잔여 있음)

타일 상태 계약·생성/검증/폴백·전송/해시/씬 전환 게이트 및 재경기 맵의 구현 근거가 있다. 멀티 최초 실패 UI도 이후 반영됐다. 현재 상태·관측 한계는 [Status](PROJECT_STATUS.md), 실제 미완은 [Roadmap](ROADMAP.md)을 따른다. 이 기술 문서에서 과거 단계별 미구현 도장을 누적하지 않는다.

맵이 계산한 InitialGold의 실제 적용, 조각 크기 실측(NGO 실효 페이로드/Relay MTU), 경로 완전 차단 대응은 별도 잔여이며 이번에 새로 확인하거나 닫지 않았다. 생성·전송 구현이 폴백/모든 timeout·복구의 자연 발화 또는 전체 실기 수용을 뜻하지 않는다. [단계 전환·당시 검증 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-map-stages-01).

현재 canonical MapVersion은2이고 바이트 수는315+4N(N=중립 광산 수)이다. 형식 식별은 unknown map format deserialize 차단 전용이며 matchmaking·앱 업데이트·전역 connection compatibility를 대신하지 않는다. GameProtocolVersion/build 호환은 same-version filter·pre-Relay 검사·NGO approval/rejection·reconnect version 검증·update-required UX를 포함하는 별도 작업이다.

- Host 측 맵 준비 조정자가 64-bit root seed를 만들고, 전용 PRNG의 `MapSelection` 스트림으로 `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 최초 1회 선택한 뒤 최종 맵을 생성·검증한다. 실제 `InitialGold`는 광산 수 표에서 결정하며, 설정 파일의 값이 관여하는 갈래는 없다.
- 최대 100회 재시도와 폴백 선택도 Host에서만 수행한다. 재시도는 지형 세부 형태·중립 광산 위치와 장식 placement 활성화 이후의 장식만 바꾸며 선택값을 다시 뽑지 않는다. 최초 구현의 장식 목록은 항상 비어 있다. **경기 선택값이 그대로 유지되는 구간은 시도 0~99까지이며, 폴백 경로에서는 일부 값이 템플릿 값으로 대체된다** — 무엇이 대체되고 무엇이 유지되는지는 아래 「deterministic fallback 정의」가 단일 소스다.
- 로비 씬에 로딩 화면을 표시한 상태에서 최종 맵 데이터, 64-bit root seed, 맵 유형, 광산 수, 시작 광산 방향, 실제 초기 골드, 최종 맵 해시를 전달한다.
- 로딩 UI는 준비 진행 상태만 표시하고 맵 유형·광산 수·초기 골드·seed·맵 미리보기를 노출하지 않는다.
- Client는 전달받은 맵 정의의 해시를 확인하고 준비 완료를 알린다. 양측 해시가 같을 때만 Host가 전투 씬 전환을 시작한다.
- 준비 실패·해시 불일치 때 전투 씬 전환을 차단하고 오류를 기록한다. 최초 멀티는 양쪽 방/연결을 정리해 로비로 복귀하는 M-2, 재경기는 기존 결과 화면을 복원하는 M-3 계약과 아래 복구 상태를 따른다. [이전 축약 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-failure-scope-01).
- 전투 씬의 `LoadMap()`은 독립 추첨하지 않고 로비에서 확정한 맵 정의를 그대로 구성한다.
- 양측 전투 씬 로드가 완료된 뒤 서버가 시뮬레이션을 시작한다.
- 11×21 맵은 231타일이므로 seed만 전달해 양측이 독립 생성하는 방식보다 최종 타일 상태 전체를 전달하는 안정성을 우선한다.
- 싱글플레이는 동일한 생성기를 로컬 권위로 호출한다. 멀티플레이에서는 Host가 생성·확정한 `MapDefinition`의 실제 초기 골드가 권위값이며 Client의 로컬 설정은 사용하지 않는다.

##### `MapDefinition` 정규 데이터 계약

`MapDefinition`은 생성 후보나 재생성 명령이 아니라, 검증을 통과해 전장을 그대로 구성할 수 있는 최종 완성본이다. 멀티플레이에서는 Host가 이 완성본을 만들고 Client가 동일 데이터를 소비한다.

**상위 필드:**

- `MapVersion`(`int`, 현재 지원값 `2`), 64-bit root seed, 맵 유형. [초기 형식 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-field-01).
- 너비 11, 높이 21, FlatTop orientation
- 중립 광산 수, 실제 초기 골드
- 최종 해시

현재 canonical 상위 필드는 제거된 테스트 모드 표식을 포함하지 않는다. 형식 버전2와 SHA 입력은 아래 전송 package 계약을 따른다. [형식 변경·당시 코드 미반영 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-transition-01)은 현재 미구현 표시가 아니다.

**타일 배열:**

- 231개 타일을 row-major 정규 순서로 저장한다.
- 배열 인덱스가 offset 좌표의 단일 표현이다.

```text
index = row * width + col
col   = index % width
row   = index / width
```

- 타일별 필드: **`TileKind` 3상태 단일 필드** — `Normal`(일반 타일) / `NoBuild`(건설 불가 타일) / `Blocked`(막힌 타일). `InitialOwner`와 장식 상태는 포함하지 않는다.

`TileKind`는 Domain 계층의 단일 3상태 필드다. 지형/건설규칙의 모순 조합을 표현하지 못하게 해 같은 게임 의미가 서로 다른 canonical bytes를 만들지 않도록 한다. Domain→Core 역참조는 금지한다. SHA는 Host/Client 비교와 폴백 유효성에 사용하며 직전 경기와 같은 해시를 이유로 재경기 후보를 버리지 않는다. [이전 필드/해시 용도 정정](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-net-tile-kind-history).
- orientation, enum, 종류, 변형, 회전은 정수 enum/index로 표현하며 해시 원본에 float를 포함하지 않는다.

**오브젝트 배치:**

- 성: 위치 + 팀
- 시작 광산: 위치 + 팀
- 중립 광산: 위치
- 장식: 위치 + `typeId` + `materialVariantId` + `scaleStepId` + `rotationStepId`로 구성된 `DecorationDefinition` 목록

`DecorationDefinition`의 네 속성은 모두 고정폭 정수 ID다. 실제 material, scale float, rotation float는 해당 ID가 가리키는 표현 계층의 사전 정의이며 canonical bytes에 직접 넣지 않는다. 장식 목록은 canonical codec과 SHA-256 입력에 포함하고 정규 정렬한다.

성·광산의 정체성, 팀, 위치는 이 오브젝트 배치 목록이 유일한 권위 원본이다. 타일 레코드에 `HasCastle`, `HasGoldMine` 같은 중복 정체성 필드를 두지 않는다. 타일 레코드의 `TileKind`는 정적 지형과 건설 규칙만 표현하며 성·광산의 존재를 중복 인코딩하지 않는다. 런타임 이동 가능 여부는 배치 목록에서 투영한 `MineKind`와 동적 `HasBuilding`을 함께 계산한다. 생성기, 검증기, 네트워크 전달, 전투 씬 구성은 모두 같은 `MapDefinition`을 사용하며 별도 위치 목록을 병행 유지하지 않는다.

**초기 소유권 단일 소스:**

`MapDefinition`은 타일별 `InitialOwner`를 저장·전송·해시하지 않는다. Blue/Red 성 위치와 팀 시작 광산 위치, 그리고 아래 공용 초기 영역 확장 규칙이 유일한 입력이다.

```text
각 팀의 초기 소유 타일 =
    Castle 타일 + Castle 인접 6타일
  ∪ Starting MiningPost 타일 + 그 인접 6타일
```

`InitialMapStateEvaluator`는 이 집합과 초기 건물 점유를 계산하는 순수 규칙이다. 런타임 초기 성/채굴소 배치 및 소유권 적용과 semantic fairness validator가 동일 evaluator를 재사용한다. Host와 Client도 canonical definition의 위치에서 같은 결과를 파생한다.

팀별 즉시 일반 건설 가능 타일 검증은 evaluator가 만든 **고유 집합**에서 다음 순서로 수행한다.

1. 성과 초기 채굴소가 점유한 타일을 제외한다.
2. `TileKind == Normal`, `MineKind == None`, `HasBuilding == false`와 기존 소유권 조건을 적용한다.
3. 중복 좌표를 한 번만 세어 Blue/Red 각각 정확히 10개인지 확인한다.

**canonical binary와 SHA-256:**

권위 직렬화는 canonical binary 한 종류만 사용한다.

1. 스키마에 정의된 필드 순서를 고정한다.
2. 모든 수치는 스키마가 지정한 고정폭 정수로 기록한다.
3. 모든 다중 byte 정수는 **little-endian**으로 기록하고 플랫폼 native byte order를 사용하지 않는다.
4. 타일 231개는 row-major 순서로 기록한다.
5. 성·광산·장식 레코드는 row-major tile index를 첫 키로 정규 정렬한다. 동률 시 성·광산은 종류·팀 ID를, 장식은 `typeId`→`materialVariantId`→`scaleStepId`→`rotationStepId`를 스키마 순서의 후속 키로 사용한다.
6. string, float, 최종 hash 필드 자체는 canonical bytes에서 제외한다.
7. hash 필드를 제외한 canonical bytes 전체에 SHA-256을 계산해 32-byte digest를 만든다.

시각 스케일·재질 변형도 float 값을 직렬화하지 않고 정수 variant ID만 기록한다. JSON 표현은 진단 로그 전용이며 권위 전송, canonical bytes 생성, SHA-256 입력에 사용하지 않는다.

**전송 package:**

```text
mapVersion
canonicalLength
canonicalBytes
sha256Digest[32]
```

package header의 mapVersion은 preflight decoder 선택용이며 canonical MapDefinition.MapVersion과 일치해야 한다. 현재 지원값은2다. 값이 다르거나 미지원이면 deserialize하지 않고 맵 준비 실패로 처리하며 앱/접속 호환 판정에 쓰지 않는다. SHA-256 입력은 canonical bytes만이다. [버전 전환 근거](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-version-package-01).

**`NetworkMapTransfer` 전송 프로토콜:**

최초 경기와 재경기 맵 준비는 persistent network connection에 존재하는 공용 `NetworkMapTransfer`를 사용한다. 전송 수명주기를 Lobby/Game 씬 객체에 귀속시키지 않으며, scene-bound 단일 RPC 호출 하나에 전체 canonical payload를 실어 보내는 방식은 금지한다.

```text
MapPrepareBegin(matchNonce, mapVersion, totalBytes, chunkCount, SHA256)
MapChunk(matchNonce, index, count, data)
MapReady(matchNonce, success, clientSHA, errorCode)
```

- canonical payload의 `totalBytes`는 최대 64KB다. ⚠️ **근거 미확인 — 구현 시 NGO 실측으로 확정한다.**
- 각 `MapChunk.data`는 최대 1KB이며 `index`와 `count`로 전체 위치를 식별한다. ⚠️ **근거 미확인 — 구현 시 NGO 실측으로 확정한다.**

전송 한도의 성능·다중 chunk 실기 근거는 별도 검증 범위다. 작은 기본 맵 payload만으로 쪼개기·재조립·중복 경로의 실기 검증을 완료했다고 보지 않는다. 한도는 NGO 전송 특성에 근거해 판단하며 테스트를 발화시키려고 임의 축소하지 않는다. 이전 Python 크기 계산·한도 미확정 경위는 [근거 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-net-chunk-rationale)에 둔다.
- Client는 같은 `matchNonce`와 `index`의 중복 chunk를 무시한다.
- out-of-order chunk는 수신 순서가 아니라 `index` 순서로 재조립한다.
- 활성 준비 작업의 nonce와 다른 stale begin/chunk/ready 메시지는 상태를 바꾸지 않고 무시한다.
- 선언된 `chunkCount`가 모두 모이고 재조립 길이가 `totalBytes`와 일치한 뒤에만 SHA-256→deserialize→semantic fairness validation을 수행한다.
- 부분 조립 데이터로 hash 검증, deserialize, 맵 구성 또는 준비 ACK를 수행하지 않는다.
- Client는 성공 시 계산한 `clientSHA`와 함께 `MapReady(success=true)`를 보내고, 실패 시 `MapReady(success=false, errorCode)`를 보낸다.
- Host는 활성 nonce의 `success=true`와 SHA 일치를 확인하기 전에 scene transition을 시작하지 않는다.

**timeout/retry state machine:**

- Begin/Chunks는 reliable delivery를 사용한다.
- Host는 package 전송 완료 후 활성 nonce의 `MapReady`를 10초 기다린다.
- timeout 또는 incomplete assembly만 같은 nonce와 같은 canonical package 전체를 1회 재전송한다. 재전송 뒤 두 번째 10초 timeout/incomplete면 terminal failure다.
- unsupported `MapVersion`, `totalBytes`가 한도 초과(현재 값 64KB — ⚠️ 근거 미확인, 위 전송 프로토콜 절 참조), SHA mismatch, deserialize failure, semantic invalid, disconnect는 즉시 terminal failure다. 동일 package resend를 수행하지 않는다.
- terminal failure 경로는 모두 scene transition gate를 닫은 상태로 유지한다.

**복구 상태:**

- 최초 멀티 실패는 loading을 닫고 양쪽이 방/연결을 정리해 로비 메인으로 돌아간 뒤 각자 확인 한 개의 알림을 띄운다. 재시도/재시작 요청 경로는 없다. Host 실패 통보와 통보 미도착의 연결 끊김은 같은 로컬 진입점으로 수렴한다.
- 멀티 재경기 실패는 pending candidate를 폐기하고 old current definition을 유지한다. rematch pending을 reset하고 result UI·기존 actions를 복원하며 자동 복귀 countdown을 전체 길이로 재시작한다.
- 싱글 최초 실패는 loading을 닫고 다시하기/로비로 선택 모달을 표시한다. 싱글 다시하기 실패는 결과 화면과 기존 choices를 복원하고 상태 줄로 알린다. 싱글 자동 로비 복귀 timer는 없다.
- 제목·문구·버튼·Host/Client 동일 화면의 규범은 [경기 흐름 UI](GameSystemRules/GameSystemRules_MatchFlowUI.md), 맵 실패 운영은 [경기 운영](GameSystemRules/GameSystemRules_MatchFlow.md)을 따른다. [이전 Retry/Leave 설계·확정·구현 경위](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-recovery-01)는 현재 절차가 아니다.

**Client 검증 순서:**

1. package 전체 길이, canonicalLength, 지원 `MapVersion`을 확인한다.
2. deserialize 전에 canonical bytes의 SHA-256을 재계산해 전달된 digest와 비교한다.
3. hash가 맞을 때만 canonical bytes를 `MapDefinition`으로 deserialize한다.
4. 231개 타일, 버전 일치, 좌표/목록 정합, 180도 대칭, 도달성, 광산·시작 공간 등 semantic fairness validator를 실행한다.
5. 모든 단계가 성공한 경우에만 `MapReady(success=true, clientSHA)` 준비 ACK를 Host에 전송한다.

Client가 응답 가능한 상태에서 길이/버전/hash/deserialize/semantic 검증이 실패하면 `MapReady(success=false, errorCode)`를 보내고 로비를 유지하며 실패 단계를 로그로 남긴다. timeout·불완전 수신·disconnect는 위 state machine이 처리한다.

##### 생성·검증 실행 모델

초기 구현은 맵 generation과 validation을 main thread synchronous 작업으로 둔다.

1. 로비에서 loading UI를 표시한다.
2. 1 frame yield해 loading UI가 실제 렌더되도록 한다.
3. Host main thread에서 pure C# generator와 validator를 실행한다.
4. 생성 시간과 최종 attempt count를 로그에 기록한다.
5. 검증 성공 뒤 공용 `NetworkMapTransfer` 전송을 시작한다.
6. 전투 씬에서는 수신·검증을 마친 `MapDefinition`의 Unity object/render 적용을 main thread에서 수행한다.

generator와 validator의 도메인 코드는 `UnityEngine` object, scene object, renderer와 그 밖의 Unity API에 의존하지 않는 pure C# 경계로 유지한다. 이 경계는 향후 실행 위치를 바꿀 수 있게 하기 위한 것이며, 최초 구현에 `Task`, worker thread, cancellation token과 관련 수명주기 조정을 선행 도입한다는 뜻이 아니다.

실제 대상 기기 profiling에서 생성·검증 구간의 사용자 체감 hitch가 확인된 경우에만 pure C# 구간을 background로 이전한다. profiling 근거 없이 비동기화하지 않으며, 이전하더라도 Unity object/render 적용은 계속 Game scene main thread에서 수행한다.

##### 결정적 PRNG 및 독립 스트림 계약

맵 생성은 프로젝트 전용 고정 정수 PRNG만 사용한다. PRNG 알고리즘, 정수 폭, 오버플로 규칙, seed 파생 순서를 프로젝트 코드와 테스트 벡터로 고정한다. `System.Random`, `UnityEngine.Random`, 런타임별 문자열 해시, 기본 `GetHashCode()`는 사용하지 않는다.

입력 seed는 64-bit root seed다. `MapVersion`과 root seed를 안정 해시 또는 동일한 고정 정수 파생 함수에 넣어 다음 도메인 seed를 만든다.

| 스트림 | 책임 |
|--------|------|
| `MapSelection` | `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 재시도 전에 1회 선택 |
| `Terrain` | 완전 차단 지형과 건설 불가 구역 |
| `MinePlacement` | 시작 광산과 중립 광산 배치 |
| `Decoration` | 장식 종류, 변형, 회전 |

스트림 이름은 런타임 문자열 해시로 변환하지 않고 스키마에 고정된 정수 stream ID를 사용한다.

최초 구현의 모든 archetype generator와 fallback은 `DecorationDefinition` 빈 목록을 반환하며 `Decoration` PRNG draw를 소비하지 않는다. 그러나 `MapDefinition`, canonical codec/hash, `SymmetricMapBuilder`, `MapDefinitionValidator`는 처음부터 `typeId`, `materialVariantId`, `scaleStepId`, `rotationStepId` 스키마를 처리한다. builder는 회전 대응 위치에 장식 레코드와 180도 대응 `rotationStepId`를 함께 기록하고, validator는 최종 목록의 exact 180° 대응을 독립 검증한다.

장식 에셋과 맵 테마가 확정된 뒤에만 독립 `Decoration` 스트림을 사용해 placement를 활성화한다. 이 후속 추가는 `Decoration` 스트림 내부에서만 draw를 소비하며 같은 `MapVersion`·root seed·attempt의 `Terrain`과 `MinePlacement` 결과를 바꾸지 않는다.

재시도마다 각 도메인 seed와 attempt index를 다시 파생해 `Attempt-0`~`Attempt-99`의 독립 PRNG 상태를 만든다. 즉 하나의 긴 PRNG 상태를 모든 서브시스템과 attempt가 공유하지 않는다.

`InitialGold`는 PRNG 입력이 아니라 `NeutralMineCount`의 순수 lookup 결과다. 선택된 `MapType`, `NeutralMineCount`, `StartingMineSide`와 실제 `InitialGold`는 **attempt 0~99 구간에서 불변인 경기 선택값**이다. **폴백 경로는 이 구간 밖이며, 그 경로에서 어떤 값이 템플릿 값으로 대체되고 어떤 값이 유지되는지는 아래 「deterministic fallback 정의」가 단일 소스다.** attempt는 `Terrain`, 중립 `MinePlacement`, `Decoration`의 세부 결과만 바꿀 수 있다. 검증 실패는 해당 attempt만 폐기하며 `MapSelection`을 다시 실행하지 않는다. 이 경계로 유형·광산 수·A/B별 후보 실패율이 최초 선택 확률을 편향시키는 것을 금지한다.

```text
domainSeed  = Derive(mapVersion, rootSeed, fixedDomainId)
attemptSeed = Derive(domainSeed, attemptIndex)
```

이 구조가 보장해야 하는 성질:

- Decoration의 draw 수나 호출 순서 변경이 Terrain/MinePlacement 결과를 바꾸지 않는다.
- Terrain과 MinePlacement도 서로의 draw 수에 결합되지 않는다.
- Attempt-N의 draw 수가 Attempt-(N+1)의 시작 상태를 바꾸지 않는다.
- 동일 `MapVersion` + root seed는 동일한 맵 선택, 후보 순서, 검증/폴백 결정과 최종 결과를 재현한다.

결정적 재생성은 진단과 테스트를 위한 계약이다. 멀티플레이 경기의 실제 권위는 Host가 생성·검증해 전달한 최종 `MapDefinition`과 hash다. Client는 root seed로 독립 생성한 결과를 권위 데이터로 사용하지 않고 수신 완성본의 hash만 검증한다.

##### `SymmetricMapBuilder` 생성 경계

11×21 archetype generator가 타일 배열을 직접 수정하지 않도록 `SymmetricMapBuilder`를 유일한 후보 생성 변경 경계로 둔다.

```text
SetPair(col, row, state)
    original = (col, row)
    rotated  = 참 180도 회전(col, row)
                 col → 10 - col
                 row → 21 - row      (col 이 짝수일 때)
                 row → 20 - row      (col 이 홀수일 때)
    두 위치에 state와 RotateState(state)를 원자적으로 기록

SetCenter(state)
    center = (5, 10)
    자기대응 가능한 state를 중심에 기록
```

> 🔴 **종전의 `rotated = (10 - col, 20 - row)`(행열 반전)는 폐기됐다 (2026-08-26).** 그것은 회전이 아니다 — FlatTop 은 홀수 열이 반 칸 아래로 시프트되어 있어 두 축 번호를 뒤집으면 **헥스 인접성이 보존되지 않는다.** 큐브 좌표로는 `p → 2C - p`(`C` = offset `(5,10)`)이며, 규칙 정의의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 1이다.
>
> **`SetPair`는 짝 없는 6칸 `(0,0) (2,0) (4,0) (6,0) (8,0) (10,0)`(짝수 열 0행)을 입력으로 받지 않는다.** 그 좌표의 회전 결과는 21행이라 맵 밖이다. 6칸은 생성이 아니라 **고정값(항상 막힌 타일)** 으로 기록한다.

계약:

- `SetPair`는 중심 `(5,10)` 입력을 거부하고 중심은 `SetCenter`로만 기록한다.
- `SetCenter`는 중심 외 좌표를 받지 않는다.
- `TileKind`, 광산, 장식 등 모든 정적 생성 상태는 builder API를 통과한다.
- archetype generator에는 raw mutable tile buffer와 회전 상대 타일 직접 수정 API를 노출하지 않는다.
- `RotateState(state)`는 팀 전용 상태의 Blue↔Red 대응과 장식 rotation의 180도 대응 정수 ID 변환을 포함한다.
- **좌표를 옮기는 연산과 상태를 바꾸는 연산은 이름을 나눠 쓴다** — 좌표는 `RotateCoord(p)`(계산식의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 1), 상태는 `RotateState(state)`(위 항목)다. 2026-08-31 이전에는 **둘 다 같은 이름**이어서 컴파일러는 입력 타입으로 구분해도 읽는 사람은 구분할 수 없었다. 같은 이름으로 되돌리지 않는다.
- 장식 종류·variant·scale/material 대응은 같은 상태 변환 계약 안에서 유지한다.

builder는 실수로 한쪽만 기록하는 구현을 구조적으로 막는 도구다. 보안·완료 판정 경계로 신뢰하지는 않는다. builder가 완성한 `MapDefinition` 후보를 독립 `MapDefinitionValidator`에 넘겨 타일 `TileKind`, 광산, 장식의 exact 180° symmetry를 다시 검사한다. validator는 builder 내부 상태나 “대칭으로 생성되었다”는 플래그를 신뢰하지 않고 최종 정의만 읽는다.

##### archetype generator 알고리즘

> 🔴 **이 절의 대역 수치는 2026-08-26에 참 180도 회전 기준으로 전면 재작성됐다.** 종전 수치는 행열 반전 전제 위에 적혀 있어 회전 대칭이 성립하지 않았다.
> - **유형별 값의 단일 소스는 `GameSystemRules_RandomMap.md` 3장(규칙 4~8)** 이다. 이 절은 그 사양을 구현하는 생성기 알고리즘을 적으며, **값이 바뀌면 규칙 문서를 먼저 고치고 이 절을 맞춘다.**
> - 「높이 단계」 = `행 × 2 + (홀수 열이면 1)`, 범위 1~41, 회전은 `L ↔ 42-L`. 정의는 규칙 문서 7장 용어 정의.
> - ⚠️ **아래 수치의 공정성 근거는 파이썬 시뮬레이션이며 Unity 실기 검증이 아니다**(`_Tasks/2026-08-26/04_35_map-rules-correction/Research.md` 3-0). **구현 후 C# 검증기로 같은 항목을 재측정하기 전까지 「검증 완료」로 표기하지 않는다.**

**`ObstacleOpenGenerator`:** **윗절반 3~9행**은 행별 count 0~4를 각각 20%로 독립 선택하고 count개의 unique column을 균등 선택한다. 0~2행과 그 회전 상대에는 장애물을 두지 않는다. 아랫절반은 별도 추첨 없이 참 180도 회전 투영이며, 결과 대역은 **11~18행**이다(짝수 열 `21-row` · 홀수 열 `20-row` 차이 때문에 상단 대역과 행 번호가 겹치지 않는다). 중앙선(높이 단계 21 = 홀수 열 10행)은 count 0/2/4를 각각 1/3로 선택해 중앙선 내부 rotation pair `(1,10)↔(9,10)` · `(3,10)↔(7,10)` 로 배치하고 고정점 `(5,10)`에는 두지 않는다. 장애물 pair 0개는 attempt reject다. 기대값은 약 30개다. reachability 실패 시 count 감소·이동·repair 없이 attempt 전체를 버린다.

**`CanyonGenerator`:** `W`는 3/5/7 균등 선택이다. 폭은 **열 구간**으로 해석한다(폭 `w` = col `5-(w-1)/2` ~ `5+(w-1)/2`). **윗절반만 생성하고 아랫절반은 참 180도 회전으로 복제한다.** 0~2행은 width 11이다. rows 3~8 중 `(11-W)/2`개 distinct transition row를 균등 선택하고 해당 row를 지날 때 centered contiguous odd width를 2씩 감소시킨다. **3행의 시작 폭은 11**이고 8행에서 `W`에 도달하며 profile은 단조, adjacent decrement≤2다. **높이 단계 18~24**(짝수 열 9~12행 · 홀수 열 9~11행, 두께 3타일 39칸)는 width `W` 로 고정하고 그 열린 타일을 `TileKind.NoBuild`로 둔다. 회전 투영 결과 아래쪽 좁아지는 구간은 **짝수 열 13~18행 · 홀수 열 12~17행**, 아래쪽 width 11 대역은 **짝수 열 19~20행 · 홀수 열 18~20행**이다. 중심 폭 밖은 `TileKind.Blocked`다.

**`OuterGenerator`:** length 5/7/9/11, maxWidth 3/5, 형태(마름모형/타원형/울퉁불퉁형)를 각각 균등 선택한다. 덩어리 대역은 **높이 단계 `21±L`** 이며 실제 행 범위는 `L=5 → 짝수 8~13·홀수 8~12`, `L=7 → 7~14·7~13`, `L=9 → 6~15·6~14`, `L=11 → 5~16·5~15`다. 포함된 각 높이 단계는 col 5 중심의 단일 contiguous odd-width `TileKind.Blocked` 구간이고 **중앙선(단계 21)은 maxWidth**다. upper profile을 참 180도 회전 투영하며 adjacent width delta≤2, connected, no holes를 보장한다. 마름모형은 `1→3→5` monotonic ramp, 타원형은 tapered ends와 max-width plateau, 울퉁불퉁형은 공통 제약 내 임의 odd-width profile이다. **높이 단계 18~24**에서 덩어리 바깥의 열린 외곽 통로는 `TileKind.NoBuild`이며 validator가 left/right width≥3과 connectivity를 검사한다.

**`ThreeLaneGenerator`:** L 5/7/9/11에 대한 분리 대역은 **높이 단계 `21±L`** 이며 실제 행 범위는 `OuterGenerator`와 같은 표를 따른다(`L=5 → 짝수 8~13·홀수 8~12` … `L=11 → 5~16·5~15`). 대역 안은 cols 0~2/4~6/8~10을 `TileKind.NoBuild`, cols 3/7을 `TileKind.Blocked`로 고정한다. 열 구조는 회전(`col → 10-col`)에 그대로 대응하므로 손대지 않는다. 대역 밖은 모두 `TileKind.Normal`이고 즉시 merge하며 transition obstacle이나 맵을 세로로 관통하는 상시 건설 불가 띠는 없다. 대역 내부 paired mine은 mirrored left/right lane에 lane당 최대 1개, middle lane은 odd count의 `(5,10)` singleton만 허용한다. 나머지 pair는 합쳐진 위·아래 구역에 둔다.

##### 중립 광산 canonical orbit sampling

candidate builder는 성, 시작 광산, `InitialMapStateEvaluator`가 산출한 팀별 보호 초기 건설 타일 10개, `TileKind.Blocked`, archetype 금지 zone을 제거한다. 남은 좌표 `p`에서 `(p, RotateCoord(p))`를 만들고 두 row-major index 중 작은 쪽을 canonical representative로 삼아 reversed duplicate를 제거한다. `MinePlacement` stream으로 필요한 distinct pair slot을 center/edge weight 없이 균등 선택한다. odd count의 singleton은 고정 `(5,10)`이다.

- Open/Obstacle: 공통 제외 뒤 모든 pair 허용
- Canyon: **높이 단계 18~24 밖**에만 pair를 둔다(odd count의 `(5,10)` singleton은 예외)
- Outer: **높이 단계 18~24** 안에는 최대 1 pair, 나머지는 위·아래 구역
- ThreeLane: 분리 대역(높이 단계 `21±L`) 내부 pair는 mirrored left/right lane이고 lane당 mine 최대 1개, 나머지는 합쳐진 위·아래 구역

> 유형별 금지 구역의 **단일 소스는 `GameSystemRules_RandomMap.md` 3장(규칙 4~8)의 ④ 항목**이다. 위 목록은 sampler 구현이 참조하는 요약이며, 값이 어긋나면 규칙 문서가 옳다.
- spacing/adjacency filter 없음
- local constraint 위반은 후보별 동일 확률을 보존하는 rejection sampling으로 다시 선택
- global validator 실패는 chosen mine 이동·repair 없이 attempt reject

##### `MapDefinitionValidator` access metric

공정성 거리는 기존 A*에 임의의 mine 인접 타일 하나를 지정해 구하지 않는다. 각 castle에 인접한 `StaticTraversable` 타일 전체를 거리 0의 **성 접근 칸**으로 두고, **대상 광산 덩어리**에 인접한 `StaticTraversable` 타일 전체를 **광산 덩어리 접근 칸**으로 하는 multi-source BFS에서 최초 도달 거리를 사용한다. **재는 단위는 광산 하나가 아니라 광산 덩어리다**(아래 항목). 용어의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 13 「접근 거리」 절이다.

```text
StaticTraversable = TileKind != Blocked
                 && MineKind == None
                 && !Castle
                 && !StartingPost
```

> **광산 덩어리 접근 칸은 광산 하나가 아니라 「광산 덩어리」 단위로 만든다.** 서로 인접한 광산들을 한 덩어리로 묶고, 그 덩어리에 속한 **어느 광산에라도 인접한 StaticTraversable 타일 전체**를 그 덩어리의 접근 칸으로 쓴다. **접근 칸이 하나도 없는 덩어리가 하나라도 있으면 해당 후보를 거부한다.** 아래의 도달 가능성과 cross equality 도 이 덩어리 접근 칸으로 판정하며, 광산이 하나뿐인 덩어리는 결과가 종전과 같다. 판정 규정과 그 근거의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 13이다.

`TileKind.Normal`과 `TileKind.NoBuild`는 모두 traversable이고 runtime general building은 이 정적 metric에서 제외한다. 모든 castle이 모든 neutral 광산 덩어리 접근 칸에 도달해야 하고 Blue/Red 성 접근 칸끼리도 상호 도달해야 한다. center C는 `Access(B,C)==Access(R,C)`, pair A/RotateCoord(A)는 `Access(B,A)==Access(R,RotateCoord(A))`와 `Access(R,A)==Access(B,RotateCoord(A))`를 모두 만족해야 한다. 같은 cross equality를 geometric `HexCoord` 거리로 독립 검사한다.

##### 필수 통로 validator

> 이 절의 이름은 2026-08-26에 **필수 통로 validator** 로 바뀌었다. 종전 이름은 같은 단어가 「두지 않기로 한 것」과 「반드시 검증할 것」을 동시에 가리켜 폐기했다 — 폐기된 표현은 `GameSystemRules_RandomMap.md` 7장 용어 정의 「필수 통로」의 `_Avoid_` 행에만 남겨 둔다.

- mine placement 전 모든 필수 통로의 같은 높이 단계 cross-section: Canyon 중앙 열린 폭≥3, Outer left/right 각각≥3, ThreeLane 각 lane 정확히 3. 해당 열린 타일은 모두 `TileKind.NoBuild`다.
- mine placement 후 같은 통로·같은 높이 단계: mine 0이면 walkable≥3, mine 1이면 walkable≥2, mine≥2는 invalid다.
- 통로별 정의된 start set→end set BFS로 연속성과 unintended disconnect 부재를 확인한다.
- exact symmetry+위 국소 폭+BFS만 검사하며 alternate route 전체를 열거하지 않는다.

##### deterministic fallback 정의

> 🔴 **2026-08-26 전면 교체.** 종전 구조(유형 × 광산 수 × A/B = 50케이스, 시작 광산 좌우 변환)는 폐기했다. 폐기 근거는 두 가지다 — ① **좌우 변환 `(col,row) → (10-col,row)` 은 보호 대상 10타일 집합을 바꿔 검증을 깨뜨린다** ② 폴백은 실전에서 거의 도달하지 않는 경로(attempt 1회 성공률 최저 99.6%)라 50케이스의 제작·검증·유지 비용이 정당화되지 않는다. 기획 계약의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 12다.
>
> **아래 「구성 요소」의 유형별 최대 광산 수와 초기 골드 값은 구현자가 곧바로 쓸 수 있도록 여기에 함께 적는다. 그러나 값의 단일 소스는 규칙 문서이며, 값이 바뀌면 규칙 문서를 먼저 고치고 이 절을 맞춘다**(「archetype generator 알고리즘」 절과 같은 방식이다).

**구성 요소:**

- 맵 유형별 deterministic 고정 템플릿 **1개씩, 총 5개**. 유형×광산 수×A/B 조합은 만들지 않는다.
- 각 템플릿의 중립 광산 수는 **그 유형이 허용하는 최대값**(완전개방 6 / 장애물 6 / 협곡 4 / 외곽 6 / 3갈래 6)으로 고정하고, `InitialGold`는 그 광산 수에서 파생한다(6→200, 4→400).
- **시작 광산 A/B는 템플릿 안에 그대로 고정해 담는다.** `MirrorStartMine` 같은 좌우 대응 변환은 **사용하지 않는다.**
- 템플릿 데이터는 **윗절반(높이 단계 1~20)과 중앙선(단계 21)만 지정**하고, 아랫절반은 `SymmetricMapBuilder`의 참 180도 회전으로 복제한다.
- 최초 구현은 장식 없음(`DecorationDefinition` count 0). 에셋·테마 확정 뒤에는 유형별 고정 exact 180° symmetric decoration set만 허용

fallback builder는 맵 유형을 키로 위 정의를 조립하며 PRNG draw를 전혀 사용하지 않는다. 같은 유형은 항상 같은 지형과 중립 광산 결과를 만든다. **fallback builder의 입력은 `MapType` 하나뿐이다** — 경기 선택 단계의 `StartingMineSide`·`NeutralMineCount`·`InitialGold`는 이 경로에서 입력으로 쓰이지 않고 **템플릿 값으로 대체된다.** 대체된 값은 canonical `MapDefinition`에 그대로 실려 Client에도 전달되고, 규칙 12의 로그 필수 항목(중립 광산 수·시작 광산 방향·실제 초기 골드·폴백 사용 여부)으로 추적된다. **대체되지 않는 것은 `MapType` 하나**이며, 기획 계약의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 12다. 🔴 **2026-09-14 — 이 문장이 종전에 「대체 대상이 아니다」로 함께 묶고 있던 「테스트 모드 표식과 그때의 `InitialGold` 규정」은 제거됐다**(근거의 단일 소스는 같은 문서 규칙 3 의 개정 블록).

조립 결과는 일반 생성 결과와 동일한 `MapDefinition`이며, **전용 완화 규칙 없이 전체 `MapDefinitionValidator`를 통과해야 한다.** 검증 성공 후에만 canonical binary와 SHA-256 package를 만든다. 폴백이 검증에 실패하면 조용히 사용하지 않고 맵 준비 실패로 처리한다. 빌드·에디터 테스트에서 템플릿 5개를 상시 전수 검증한다.

**저장 포맷은 canonical binary이며, 그 선택에는 부수 조건 2가지가 따라붙는다.**

1. **기존 템플릿 bytes·원본 정의와 runtime 폴백 생성·검증 로직을 보존한다.** 2026-10-16 사용자 최소 유지 결정에 따라 독립 템플릿 제작 에디터 도구는 퇴역한다. 포맷 변경 시 재생성 도구를 현행 규격에 맞춰 작성하며 Tools 폴더 보존을 요구하지 않는다(`WORKFLOW.md` [5-2]). 위 runtime 검증·hash·맵 준비 실패 계약은 유지한다.
2. **재생성용 원본(윗절반 지정값 또는 seed)을 별도 보관한다.** 바이너리만 남으면 포맷 변경 시 손으로 다시 만들어야 한다.

base template은 코드 상수 또는 schema 비종속 순수 정의로도 유지해, schema 변경 시 옛 canonical binary에 발이 묶이지 않게 한다.

##### `HexTile` 런타임 상태 계약

`MapDefinition`을 `HexGrid`로 구성할 때 `HexTile` 상태를 다음 세 축으로 분리한다.

| 상태 | 값 | 변경 주체 |
|------|----|-----------|
| `TileKind` | `Normal`(일반), `NoBuild`(건설 불가), `Blocked`(막힘) | 맵 정의. 경기 중 불변 |
| `MineKind` | `None`, `Neutral`, `BlueStart`, `RedStart` | 광산 배치 목록에서 로드 시 투영. 경기 중 불변 |
| `HasBuilding` | bool | 건물 배치·철거·파괴에 따른 동적 상태 |

> 종전에는 지형 축과 건설 규칙 축이 **별도 필드 2개**였으나, 「막혔는데 건설 허용」 같은 **뜻이 없는 조합**이 표현 가능해 의미가 같은 맵이 서로 다른 canonical bytes·해시를 가질 수 있었다. 2026-08-26에 `TileKind` 3상태 단일 필드로 통합했다(위 「`MapDefinition` 정규 데이터 계약」 참조).

`MapDefinition`의 성/광산 배치 목록은 직렬화·해시의 단일 원본이다. `HexTile.MineKind`는 그 목록에서 생성한 런타임 투영이며 별도 네트워크/직렬화 원본으로 병행 유지하지 않는다.

`IsWalkable`은 mutable 저장 필드가 아니라 다음 계산 프로퍼티다.

```text
IsWalkable = TileKind != Blocked
          && MineKind == None
          && !HasBuilding
```

판정 규칙:

```text
일반 건설 = TileKind == Normal
         && MineKind == None
         && !HasBuilding
         && 기존 소유권 조건

MiningPost = TileKind != Blocked
          && MineKind != None
          && !HasBuilding
          && 기존 인접 팀 타일 조건

점령 가능 = TileKind != Blocked
```

`TileKind.Blocked`는 이동·일반/채굴소 건설·점령이 모두 불가능한 영구 지형이다. `HasBuilding`과 독립적이므로 건물 철거 또는 파괴가 막힌 타일을 열지 않는다. `TileKind.NoBuild`는 이동·점령은 허용하고 일반 건설만 막는다(MiningPost는 예외).

**`HexGridRenderer`와 입력 표현:**

- `TileKind.NoBuild` 좌표는 일반 타일과 같은 standard hex mesh와 높이를 생성한다. owner base color도 Neutral/Blue/Red 상태를 그대로 사용한다.
- 타일 표면에는 별도 표현 계층으로 반투명 짙은 회색 diagonal hatch 3개를 overlay한다. selection highlight와 `TileKind.NoBuild` overlay를 독립 상태로 유지해 둘을 동시에 렌더링한다.
- `TileKind.Blocked`도 domain `HexGrid`와 `MapDefinition`의 231개 row-major 레코드에는 남는다. **canonical codec/hash에서 제외하지 않는다.** `MapDefinitionValidator`의 exact 180° 비교에서도 제외하지 않되, **짝 없는 6칸만 아래 예외를 따른다.**

**`TileKind.Blocked` 좌표 계약 — 짝 없는 6칸 예외 (2026-08-26 추가):**

- **① 어느 6칸인가**: `(0,0) (2,0) (4,0) (6,0) (8,0) (10,0)` — **전부 짝수 열 0행**이다. 참 180도 회전은 짝수 열에서 `row → 21 - row` 이므로 0행의 상대는 21행이 되어 맵 밖이다. 즉 **회전 상대가 존재하지 않는다.** 그래서 이 6칸은 항상 `TileKind.Blocked` 고정이며 231칸 중 플레이에 쓰는 칸은 225칸이다.
- **② 대칭 검증**: `MapDefinitionValidator`는 이 6칸을 **대응쌍 비교 대상에서 제외**하되, 그냥 빠뜨리지 않고 **「항상 `TileKind.Blocked` 인가」를 확인하는 고정값 검사로 대체**한다. 제외만 하면 그 6칸이 무엇이든 될 수 있어 검증에 구멍이 생긴다.
- **③ canonical hash**: **배열 231칸과 해시 범위는 그대로 유지한다.** `index = row * 11 + col` 인덱스 규칙도 바뀌지 않는다. 직렬화 범위와 해시 범위를 갈라 놓으면 `TileKind` 통합이 없애려던 「같은 맵이 다른 바이트가 되는 경로」를 다른 자리에 새로 만들게 된다.
- 같은 내용이 `GameSystemRules_RandomMap.md` 규칙 10에도 있다. **두 곳이 어긋나면 규칙 문서가 옳다.**
- `HexGridRenderer`는 `TileKind.Blocked` 좌표에 standard hex tile mesh와 collider를 생성하지 않는다. `TileKind.Normal`·`TileKind.NoBuild`와 다른 높이 geometry도 만들지 않아 결과는 빈 공간이다.
- selection/raycast 진입점은 hit된 배경 또는 하부 collider만 신뢰하지 않는다. 변환된 맵 좌표가 범위 안인지 확인한 뒤 `TileKind != Blocked`인 경우에만 선택 대상으로 반환한다. 따라서 `TileKind.Blocked` 좌표는 이동 명령, 점령, 건설과 selection event를 발생시키지 않는다.
- 추후 obstacle prefab/asset은 `TileKind.Blocked` 좌표의 빈 공간 시각을 대체하는 presentation 확장점이다. domain `TileKind`, collider 기반 선택 금지, 이동/점령/건설 규칙, canonical hash와 exact symmetry 계약은 유지한다. ⚠️ **짝 없는 6칸은 회전 상대가 없으므로, obstacle asset 도입 시 반드시 예외 처리한다** — 그러지 않으면 한쪽에만 보이는 지형 6개가 생긴다.

**`InputHandler`(1·2) + `GridInteractionUseCase`(3~5) 클릭 판정 순서:**

1. 기존 building action 분기
2. `MineKind` 기반 MiningPost 자격 분기
3. `TileKind.Blocked`
4. `TileKind.NoBuild`
5. 일반 타일(`TileKind.Normal`)

- `TileKind.Blocked`/빈 공간은 논리 좌표가 있어도 unselectable이다. `Deselect`로 이전 highlight를 해제하고 `BuildingPlacementUI`를 닫되 토스트와 새 `TileSelected` 이벤트는 발생시키지 않는다.
- 자기 팀 소유 `TileKind.NoBuild`는 정상 `TileSelected`와 highlight를 발생시킨다. 건설 패널은 열지 않고 stale 패널을 닫은 뒤 `ToastKey.BuildingNotAllowed`(`이 타일에는 건설할 수 없습니다`)를 발행한다.
- 중립·적 소유 `TileKind.NoBuild`는 선택·highlight만 수행하고 stale 패널은 닫는다. 건설 의도가 아니므로 토스트는 없다.

> 이 클릭 판정 순서는 `GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 5~8과 **같은 내용**이다. 한쪽만 고치면 조용히 갈라지므로 항상 함께 고친다.
- building action과 광산 분기를 먼저 처리하므로 기존 건물 상호작용과 `MineKind != None`인 광산의 MiningPost 예외가 `TileKind.NoBuild`에 가로막히지 않는다.

**런타임 적용 경계:** 이동·스폰·경로탐색은 계산된 `IsWalkable`, 일반 건설·점령·광산 건설은 위 전용 조건을 각각 사용한다. AI 배치 역시 건설 자격과 이동 가능 여부를 혼동하지 않는다. 구현·미확정 범위는 [현재 상태](PROJECT_STATUS.md)·[AI 규칙](GameSystemRules/GameSystemRules_AI.md)·[로드맵](ROADMAP.md)을 따른다. 1단계 당시 완료/절반/대기 판정과 전수 검색 행번호는 [전환 이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-net-transition-state)에 보존하며 현재 구현 상태로 사용하지 않는다.

##### 재경기 `MapDefinition` 생명주기

기존 재경기 요청·수락·거절 RPC와 Game 씬 재로드 구조를 유지한다. **요청 payload에 맵 조건(mode)을 싣지 않는다 — 재경기 맵은 매번 새로 준비한다.**

요청에는 맵 조건 선택을 싣지 않지만 current definition/package/hash를 씬 재로드 사이에 유지하는 컨텍스트는 필요하다. 동일 폴백이 연속 선택돼도 재추첨으로 감추지 않고 `MapPreparationUsedFallbackTemplate`으로 기록한다. seed를 포함하는 hash의 차이를 지형 차이 보장으로 사용하지 않는다. [이전 SameMap/NewMap·hash 재추첨 설계](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-net-rematch-history).

서버가 관리하는 재경기 컨텍스트는 현재 확정 `MapDefinition` package/hash를 보관한다.

**요청 조정:**

- 요청을 받은 상대 팝업은 요청자를 표시한다. 표시할 맵 조건은 없다.
- 상대는 수락 또는 거절한다.
- pending 요청이 없는 상태에서 서버가 먼저 받은 요청을 현재 제안으로 확정한다.

**재경기 맵 흐름:**

1. Game 종료 상태와 결과 화면을 유지하며 팀·종족·그 밖의 매치 설정은 직전 경기 값으로 고정한다.
2. Host가 새 64-bit root seed를 만들고 맵 관련 값만 새로 준비한다. `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`, 지형 세부 형태, 중립 광산 위치를 다시 선택·생성한다. 새로 뽑힌 `NeutralMineCount`에서 실제 `InitialGold`를 확정하며 Client 로컬 설정은 사용하지 않는다. 장식 placement 활성화 이후에는 장식도 다시 생성하지만 최초 구현은 빈 목록을 유지한다.
3. 기존 current definition은 그대로 보존하고 새 package는 pending 후보로만 둔다.
4. 공용 `NetworkMapTransfer`가 새 package를 chunk로 전달하고 Client가 길이/버전→SHA-256→deserialize→semantic fairness validator를 통과한 뒤 `MapReady(success=true)`로 ACK한다.
5. Host도 최종 후보 검증과 양측 준비 상태를 확인한다.
6. 성공 시 pending 후보를 current definition으로 원자적 교체한다.
7. 교체 후에만 기존 Game 씬 재로드 경로를 실행한다.

생성, 전송, hash, deserialize, semantic 검증, ACK 중 하나라도 실패하면 pending 후보를 폐기한다. 기존 definition과 결과 화면은 유지하며 Game 씬을 재로드하지 않는다.

싱글플레이 재경기도 호출마다 새 root seed로 맵을 준비한다. 로컬 생성·semantic 검증 성공 후 원자적으로 교체한 다음 씬을 재로드한다. GameEndUI→GameBootstrapper.LoadMap→MapRootSeed.Create 흐름의 [기존 구현 관측](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-solo-reload-01)은 새 실행 검증과 구분한다.

재경기 컨텍스트는 Game 씬 재로드 사이에는 유지한다. 로비 복귀, 세션 종료, 연결 종료/끊김 시 current definition, pending 후보, pending 요청을 모두 폐기한다.

<a id="결과-화면-이탈-판정통보-구조-2026-09-16-확정-설계--미구현"></a>

#### 결과 화면 이탈 판정·통보 구조 (단계 6 구현 · 관측 범위별 검증)

정상 퇴장은 나가기 전 서버 통보→상대 브로드캐스트로 즉시 알린다. 무반응은 Host/Client가 각자 결과 화면 전용 하트비트로 판정한다. 서버만 감시하면 Host가 사라졌을 때 남은 Client에 판정 주체가 없으므로 두 경로는 대체가 아니라 보완 관계다. 판정 시 연결을 정리하며 인터넷 도달 확인·실패 사유의 제품 절차는 [네트워크 규칙](GameSystemRules/GameSystemRules_Network.md), 화면·타이머·버튼은 [경기 흐름 UI](GameSystemRules/GameSystemRules_MatchFlowUI.md)를 따른다.

NetworkBehaviour·감시/통보는 Infrastructure/Network에 두고 Presentation에는 Application의 GameEvents를 통해 전달한다. Application은 Unity.Netcode를 직접 참조하지 않는다. RPC 메서드는 ServerRpc/ClientRpc 명명 규약을 지킨다. 과거 예시 이름은 확정 API가 아니며 RTT 표시값은 하트비트 판정을 대신하지 않는다.

| 서로 다른 시계/단계 | 기술 경계 |
|---|---|
| 결과 화면 무반응 판정 | 30초 자체 감시. 이미 정해진 승패를 새로 바꾸지 않고 화면/연결을 정리 |
| 전송 계층 연결 timeout | 설계60초, 결과 판정30초보다 뒤. Game/Lobby 양 씬 Inspector 직렬화 적용 필요 |
| 맵 응답 대기 | 활성 nonce ACK10초+동일 package 재전송1회 후10초. 결과 이탈 시계와 독립 |
| 인게임 이탈 | 전송 끊김 시점 t=60의 별도 판정·승패 작업. 기존30초 유예 제거 계획과 재접속 대기 미확정은 별개 |
| 자동 로비 복귀 countdown | UI 규칙이 정하는 별도 시계. 싱글에는 적용하지 않음 |

두 씬60000 적용·결과 하트비트 구현과 당시 관측은 [원문 근거](History/ImplementationVerificationHistory.md#role-maintenance-20261016-core-network-result-leave-01)에 보존했다. 이 정리는 새 씬/실기 재검증이 아니다. 결과 화면에서 기존 ReconnectionHandler의 죽은 대기가 함께 돌던 관측은 당시 ForceWin의 announced guard로 재승패가 막힌 범위에 한정한다. 인게임 각자 판정/유예 제거·새 ClientId 관련 미확인은 [Roadmap](ROADMAP.md)의 별도 잔여이며 결과화면 구현으로 닫지 않는다.

맵 전송 중 이탈은 기존 Disconnected 즉시 실패와 복구 경로를 쓰며 새 메시지/LogEvent 키를 만들지 않는다. 씬 재로드 중 이탈은 이 설계 범위 밖이고 경기 운영 규칙이 담당한다.

NetworkStatusUI의 씬/프리팹 미배치·RTT 미사용은 과거 감사 관측이며 이번에 재실측하지 않았다. 배치/팝업 필요성은 미확정 후속이고 기존 억제 guard는 향후 배치 시 유효한 예방이므로 이 문서 정리를 코드 삭제 근거로 삼지 않는다. 과거 로그 사본 행수 차이·단계별 정정·관측 등급은 원문에서 확인한다. 현재 구현 요약은 [Status](PROJECT_STATUS.md), 실제 미완은 [Roadmap](ROADMAP.md)으로 분리한다.

#### 치팅 방어
- **서버 검증**: 모든 행동을 서버(Host)에서 검증
- **자원 관리**: 클라이언트는 읽기만 가능
- **유닛 생성**: 인구수/자원 서버 체크
- **타일 점령**: 유닛 위치 서버 관리

---
