# Hexiege - 기술 설계서 (Technical Design Document)

**버전:** 0.43.2
**최종 수정일:** 2026-08-12
**작성자:** HANYONGHEE

> **2026-08-12:** **MistShrine 물안개 힐 구현 완료 — 에디터 싱글플레이 실기 검증 완료 / ⚠️ 멀티플레이 미검증.** 신규 구성 요소: Application `MistShrineUseCase`(물안개 인스턴스 목록 + 물안개별 독립 누적기 + 매 틱 대상 재수집. **HoT/DoT 시간 지속 효과 목록을 사용하지 않아** 자연회복·BloomFairy 힐과의 채널 충돌이 구조적으로 차단된다) · Application `INetworkMistShrineController` / Infrastructure `NetworkMistShrineController`(`Request → ServerRpc(팀 검증) → ClientRpc`, `ResolveServices()` 지연 재조회) · `NetworkHealthSync` **건물 힐 전용 RPC 신설**(기존 유닛 힐 RPC 시그니처 무변경) · Domain `BuildingData.Heal(int)`(프로젝트 최초의 건물 회복 경로) · Presentation `MistShrinePanelUI` / `MistShrineRangeIndicator`. **`PopupClosedFrame` 패턴의 기존 결손 보정** — `InputHandler`의 `ClosedFrame` 가드에 연구 패널·스킬 패널이 누락돼 있던 것을 신규 MistShrine 패널과 함께 등록했다(모든 팝업이 가드에 들어와야 패턴이 성립한다). **⚠️ 멀티 고유 경로(건물 HP 동기화·클라 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱)는 실행된 적이 없다.** 상세: `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`.
> **2026-08-10:** 건물 타입 주석 오류 정정 — `AutoTower`×3종족의 Transcendence는 **VineTower**다(이전 "Trans=MistShrine"은 오류). MistShrine은 방어 타워가 아니라 별도 힐 건물 `HealShrine`(= 6)이며, 물안개 지속 힐로 재설계 확정되었다(**당시 기준 기획 확정 / 구현 미착수 — 2026-08-12 구현 완료**). 규칙: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템.

---

## 📋 목차

1. [기술 스택](#기술-스택)
2. [프로젝트 아키텍처](#프로젝트-아키텍처)
3. [네트워크 설계](#네트워크-설계)
4. [백엔드 설계](#백엔드-설계)
5. [데이터베이스 스키마](#데이터베이스-스키마)
6. [디자인 패턴](#디자인-패턴)
7. [육각형 그리드 시스템](#육각형-그리드-시스템)
8. [AI 시스템](#ai-시스템)
9. [성능 최적화](#성능-최적화)
10. [개발 환경](#개발-환경)

---

## 🛠️ 기술 스택

### 핵심 기술
| 항목 | 기술 | 버전 |
|------|------|------|
| **게임 엔진** | Unity | 6000.0.x (Unity 6 LTS) |
| **렌더 파이프라인** | URP | Universal Render Pipeline |
| **네트워크** | Netcode for GameObjects | 2.9.2 |
| **전송 레이어** | Unity Transport (UTP) | - |
| **NAT 관통** | Unity Relay | - |
| **매칭** | Unity Lobby | - |
| **인증** | Firebase Authentication + Google Play Games Plugin | Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료 (런타임 설정 미완료) |
| **경로찾기** | 커스텀 A* (HexPathfinder) | 자체 구현 |
| **백엔드** | Firebase (Firestore + Functions + Google Play Billing) | - |
| **이벤트 시스템** | UniRx | 7.1.0 |
| **애니메이션** | Animator (Mecanim) | Walk/Attack/Dead 상태 기반 |
| **모바일 입력** | Lean Touch+ / Unity Input System | - |

### 개발 언어
- **C# 9.0** (Unity 6)

### 개발 도구
- **IDE**: Visual Studio 2022 / Rider
- **버전 관리**: Git + GitHub
- **빌드**: Unity Cloud Build (선택)
- **분석**: Firebase Analytics (선택)

---

## 🏛️ 프로젝트 아키텍처

### Clean Architecture 구조

```
┌─────────────────────────────────────┐
│      Presentation Layer             │  ← MonoBehaviours, UI, Input
│  (Unity 의존성)                     │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│      Application Layer              │  ← Use Cases, Business Logic
│  (순수 C# + UniRx)                  │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│         Domain Layer                │  ← Entities, Value Objects
│  (순수 C#, Unity 독립)              │
└─────────────────────────────────────┘
              ↓ ↑
┌─────────────────────────────────────┐
│    Infrastructure Layer             │  ← Network, DB, External APIs
│  (Netcode, PlayFab, 외부 연동)      │
└─────────────────────────────────────┘
```

### 폴더 구조
```
Assets/
└── _Project/
    ├── Scripts/
    │   ├── Bootstrap/           # Composition Root (GameBootstrapper partial × 4, LoginBootstrapper)
    │   ├── Domain/              # 순수 C# 엔티티
    │   ├── Application/         # Use Cases, GameEvents, NetworkContext, Interfaces
    │   ├── Infrastructure/      # 외부 연동, Network Controllers, Factories
    │   ├── Presentation/        # Unity UI/View, UnitView, CameraController
    │   ├── Core/                # 공통 유틸리티 (HexMetrics, ViewConverter)
    │   └── Diagnostics/         # 진단/디버그 전용 도구
    ├── Prefabs/
    ├── Materials/
    ├── Scenes/
    └── Resources/
```

### 의존성 방향 추상화 (Application 인터페이스 패턴)

Clean Architecture에서는 **안쪽 레이어가 바깥쪽 레이어를 알면 안 된다**(역방향 의존 금지).
이를 지키기 위해, 바깥 레이어가 안쪽에 무언가를 제공해야 할 때는
**안쪽 레이어에 인터페이스를 선언하고 바깥 레이어가 그 인터페이스를 구현**한다(의존성 역전).

본 프로젝트에서 이 패턴을 적용한 대표 사례:

| 인터페이스 (선언 위치) | 구현체 (위치) | 해소한 역방향 의존 |
|----------------------|--------------|------------------|
| `IGameServices` (Application) | `GameBootstrapper` (Bootstrap) | Infrastructure/Network → Bootstrap 직접 참조 제거. NetworkXxx가 `FindFirstObjectByType<GameBootstrapper>()` 대신 `IGameServices`로 UseCase에 접근 |
| `IUnitFactory` (Application) | `UnitFactory` (Infrastructure) | `IGameServices.GetUnitFactory()`가 구체 클래스 `UnitFactory`(Infrastructure)를 반환하면 생기는 Application → Infrastructure 역방향 의존 제거. 인터페이스를 반환하도록 변경 |
| `IEntityPositionProvider` (Application) | (Infrastructure 구현) | 전투 거리 판정 시 엔티티 실제 위치 조회 추상화 |
| `IForfeitService` (Application) | (Infrastructure/Network 구현) | 게임 포기 처리 추상화 |

- **위치 규칙**: 이런 추상화 인터페이스는 `Scripts/Application/Interfaces/`에 둔다.
- **참조 허용 범위**: Application 레이어는 Domain/Application 타입 참조 가능, `Unity.Netcode` 직접 참조 금지(NetworkContext 정적 홀더 사용). 단 `UnityEngine.GameObject` 등 기본 Unity 타입은 `IUnitFactory`처럼 필요 시 허용.
- **`IUnitFactory` 멤버**: `GetUnitObject(int)` / `RegisterUnitObject(int, GameObject)` / `InitializeUnitView(UnitData)` — Infrastructure/Network 계층(`NetworkUnit`, `NetworkProductionController`, `NetworkCombatController`, `NetworkUnitMovementController`)이 `IGameServices.GetUnitFactory()`를 통해 `IUnitFactory` 타입으로 접근한다.

### ViewConverter 시스템 (Core 레이어)

멀티플레이 팀별 관점 처리를 위한 좌표 변환 시스템.

- **위치**: `Scripts/Core/ViewConverter.cs`
- **역할**: 서버/도메인 좌표계(Blue 기준 단일)를 Red 클라이언트 뷰 좌표로 반전
- **반전 공식**: `Flip(pos) = 2 * mapCenter - pos` (맵 중심 기준 180° 반전)
- **제공 API**: `IsFlipped`, `ToView()`, `FromView()`, `FlipDirection()`
- **특징**: 스프라이트/메시 자체는 뒤집히지 않음 — 위치(Position)만 반전
- **입력 역변환**: `ScreenToWorldPoint` 결과도 `FromView()`로 역변환 필요
- **방향 반전**: 유닛 FacingDirection도 Red팀에서 FlipDirection() 적용 (NE↔SW, E↔W, SE↔NW)

---

## 🌐 네트워크 설계

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
| **초기 무작위 맵** | Host 생성·검증 → persistent `NetworkMapTransfer`의 1KB chunk 전송 → Client SHA-256·semantic 검증/ACK | 경기 시작 1회 |
| **건물 건설** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **유닛 생성** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **타일 점령** | NetworkList<TileOwnership> | 변경 시 |
| **자원** | NetworkVariable<int> | 변경 시 |
| **본기지 체력** | NetworkVariable<int> | 변경 시 |
| **유닛 이동** | 클라이언트 예측 (AI 동일 로직) | - |

#### 무작위 맵 시작 동기화 (확정 설계, 미구현)

> 범위 경계: 이번 기능에는 canonical binary 형식 식별용 임시 `MapVersion`(`int`, 초기값 `1`)만 둔다. 이는 unknown map format deserialize 차단 전용이며 matchmaking, 앱 업데이트, 전역 connection compatibility 책임이 없다. 전역 `GameProtocolVersion`/build compatibility는 matchmaking same-version filter, custom lobby pre-Relay 검사, NGO connection approval/rejection, reconnect version validation, update-required UX를 포함한 별도 중요 작업이다.

- Host 측 맵 준비 조정자가 64-bit root seed를 만들고, 전용 PRNG의 `MapSelection` 스트림으로 `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 최초 1회 선택한 뒤 최종 맵을 생성·검증한다. 정상 모드의 `InitialGold`는 광산 수 표에서 결정하고, `GameConfig.MapTestModeEnabled=true`이면 광산 수와 무관하게 실제 `InitialGold=TestStartingGold(5000)`을 사용한다.
- 최대 100회 재시도와 같은 선택값의 폴백 선택도 Host에서만 수행한다. 재시도는 지형 세부 형태·중립 광산 위치와 장식 placement 활성화 이후의 장식만 바꾸며 선택값을 다시 뽑지 않는다. 최초 구현의 장식 목록은 항상 비어 있다.
- 로비 씬에 로딩 화면을 표시한 상태에서 최종 맵 데이터, 64-bit root seed, 맵 유형, 광산 수, 시작 광산 방향, 테스트 모드 표식(고정폭 0/1), 실제 초기 골드, 최종 맵 해시를 전달한다.
- 로딩 UI는 준비 진행 상태만 표시하고 맵 유형·광산 수·초기 골드·seed·맵 미리보기를 노출하지 않는다.
- Client는 전달받은 맵 정의의 해시를 확인하고 준비 완료를 알린다. 양측 해시가 같을 때만 Host가 전투 씬 전환을 시작한다.
- 준비 실패나 해시 불일치 시 전투 씬으로 이동하지 않고 기존 로비를 유지하며 오류 로그를 남긴다.
- 전투 씬의 `LoadMap()`은 독립 추첨하지 않고 로비에서 확정한 맵 정의를 그대로 구성한다.
- 양측 전투 씬 로드가 완료된 뒤 서버가 시뮬레이션을 시작한다.
- 11×21 맵은 231타일이므로 seed만 전달해 양측이 독립 생성하는 방식보다 최종 타일 상태 전체를 전달하는 안정성을 우선한다.
- 싱글플레이는 동일한 생성기를 로컬 권위로 호출하고 로컬 `GameConfig`의 테스트 모드를 적용한다. 멀티플레이에서는 Host의 테스트 모드 표식과 실제 초기 골드가 권위값이며 Client의 로컬 설정은 무시한다.

##### `MapDefinition` 정규 데이터 계약

`MapDefinition`은 생성 후보나 재생성 명령이 아니라, 검증을 통과해 전장을 그대로 구성할 수 있는 최종 완성본이다. 멀티플레이에서는 Host가 이 완성본을 만들고 Client가 동일 데이터를 소비한다.

**상위 필드:**

- `MapVersion`(`int`, 초기값 `1`), 64-bit root seed, 맵 유형
- 너비 11, 높이 21, FlatTop orientation
- 중립 광산 수, 테스트 모드 표식(고정폭 0/1), 실제 초기 골드
- 최종 해시

**타일 배열:**

- 231개 타일을 row-major 정규 순서로 저장한다.
- 배열 인덱스가 offset 좌표의 단일 표현이다.

```text
index = row * width + col
col   = index % width
row   = index / width
```

- 타일별 필드: `TerrainKind`(`Open`/`Blocked`), `BuildRule`(`Allowed`/`NoBuild`). `InitialOwner`와 장식 상태는 포함하지 않는다.
- orientation, enum, 종류, 변형, 회전은 정수 enum/index로 표현하며 해시 원본에 float를 포함하지 않는다.

**오브젝트 배치:**

- 성: 위치 + 팀
- 시작 광산: 위치 + 팀
- 중립 광산: 위치
- 장식: 위치 + `typeId` + `materialVariantId` + `scaleStepId` + `rotationStepId`로 구성된 `DecorationDefinition` 목록

`DecorationDefinition`의 네 속성은 모두 고정폭 정수 ID다. 실제 material, scale float, rotation float는 해당 ID가 가리키는 표현 계층의 사전 정의이며 canonical bytes에 직접 넣지 않는다. 장식 목록은 canonical codec과 SHA-256 입력에 포함하고 정규 정렬한다.

성·광산의 정체성, 팀, 위치는 이 오브젝트 배치 목록이 유일한 권위 원본이다. 타일 레코드에 `HasCastle`, `HasGoldMine` 같은 중복 정체성 필드를 두지 않는다. 타일 레코드의 `TerrainKind`/`BuildRule`은 정적 지형과 건설 규칙만 표현하며 성·광산의 존재를 중복 인코딩하지 않는다. 런타임 이동 가능 여부는 배치 목록에서 투영한 `MineKind`와 동적 `HasBuilding`을 함께 계산한다. 생성기, 검증기, 네트워크 전달, 전투 씬 구성은 모두 같은 `MapDefinition`을 사용하며 별도 위치 목록을 병행 유지하지 않는다.

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
2. `TerrainKind == Open`, `BuildRule == Allowed`, `MineKind == None`, `HasBuilding == false`와 기존 소유권 조건을 적용한다.
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

package header의 `mapVersion`은 preflight decoder 선택용이며 canonical `MapDefinition.MapVersion`과 일치해야 한다. 현재 지원 값은 `1`이다. 값이 다르거나 미지원이면 canonical bytes를 deserialize하지 않고 map preparation을 실패 처리한다. 이 값은 앱/접속 호환성 판정에 사용하지 않는다. SHA-256의 입력은 canonical bytes만이다.

**`NetworkMapTransfer` 전송 프로토콜:**

최초 경기와 `RematchMapMode.NewMap`은 persistent network connection에 존재하는 공용 `NetworkMapTransfer`를 사용한다. 전송 수명주기를 Lobby/Game 씬 객체에 귀속시키지 않으며, scene-bound 단일 RPC 호출 하나에 전체 canonical payload를 실어 보내는 방식은 금지한다.

```text
MapPrepareBegin(matchNonce, mapVersion, totalBytes, chunkCount, SHA256)
MapChunk(matchNonce, index, count, data)
MapReady(matchNonce, success, clientSHA, errorCode)
```

- canonical payload의 `totalBytes`는 최대 64KB다.
- 각 `MapChunk.data`는 최대 1KB이며 `index`와 `count`로 전체 위치를 식별한다.
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
- unsupported `MapVersion`, `totalBytes>64KB`, SHA mismatch, deserialize failure, semantic invalid, disconnect는 즉시 terminal failure다. 동일 package resend를 수행하지 않는다.
- terminal failure 경로는 모두 scene transition gate를 닫은 상태로 유지한다.

**복구 상태:**

- 최초 멀티 준비 실패: loading hide, lobby와 connection 유지, generic `맵 준비에 실패했습니다` modal, `Retry`/`Leave Match` actions. 양 player의 Retry request를 Host가 idempotency key로 한 번만 수락하고 새 64-bit root seed부터 selection/generation/transfer를 재시작한다.
- NewMap 실패: pending candidate 폐기, old current definition 유지, rematch pending state reset, result UI 복원, auto-return countdown을 full duration으로 reset, SameMap/NewMap/Lobby actions 재활성화.
- single 최초 실패: Retry/Lobby. single NewMap 실패: 기존 rematch result choices 복원.
- UI payload에는 internal error code, seed, MapType을 포함하지 않는다. 상세 값은 진단 로그 전용이다.

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

`InitialGold`는 PRNG 입력이 아니다. 정상 모드에서는 `NeutralMineCount`의 순수 lookup 결과이고, 테스트 모드에서는 `TestStartingGold(5000)`이다. 선택된 `MapType`, `NeutralMineCount`, `StartingMineSide`, `MapTestModeEnabled` 표식, 실제 `InitialGold`는 attempt 0~99와 fallback이 모두 공유하는 불변 경기 선택값이다. attempt는 `Terrain`, 중립 `MinePlacement`, `Decoration`의 세부 결과만 바꿀 수 있다. 검증 실패는 해당 attempt만 폐기하며 `MapSelection`을 다시 실행하지 않는다. 이 경계로 유형·광산 수·A/B별 후보 실패율이 최초 선택 확률을 편향시키는 것을 금지한다.

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
    rotated  = (10 - col, 20 - row)
    두 위치에 state와 Rotate180(state)를 원자적으로 기록

SetCenter(state)
    center = (5, 10)
    자기대응 가능한 state를 중심에 기록
```

계약:

- `SetPair`는 중심 `(5,10)` 입력을 거부하고 중심은 `SetCenter`로만 기록한다.
- `SetCenter`는 중심 외 좌표를 받지 않는다.
- Terrain, `BuildRule`, 광산, 장식 등 모든 정적 생성 상태는 builder API를 통과한다.
- archetype generator에는 raw mutable tile buffer와 회전 상대 타일 직접 수정 API를 노출하지 않는다.
- `Rotate180(state)`는 팀 전용 상태의 Blue↔Red 대응과 장식 rotation의 180도 대응 정수 ID 변환을 포함한다.
- 장식 종류·variant·scale/material 대응은 같은 상태 변환 계약 안에서 유지한다.

builder는 실수로 한쪽만 기록하는 구현을 구조적으로 막는 도구다. 보안·완료 판정 경계로 신뢰하지는 않는다. builder가 완성한 `MapDefinition` 후보를 독립 `MapDefinitionValidator`에 넘겨 타일, 지형, `BuildRule`, 광산, 장식의 exact 180° symmetry를 다시 검사한다. validator는 builder 내부 상태나 “대칭으로 생성되었다”는 플래그를 신뢰하지 않고 최종 정의만 읽는다.

##### archetype generator 알고리즘

**`ObstacleOpenGenerator`:** 3~9행은 행별 count 0~4를 각각 20%로 독립 선택하고 count개의 unique column을 균등 선택한다. 11~17행은 exact 180° projection이다. 10행은 count 0/2/4를 각각 1/3로 선택해 내부 rotation pair로 배치한다. 장애물 pair 0개는 attempt reject다. raw 기대값은 `7×2×2 + 2 = 30`개/231타일이다. reachability 실패 시 count 감소·이동·repair 없이 attempt 전체를 버린다.

**`CanyonGenerator`:** rows 0~2와 18~20은 width 11 Open이다. `W`는 3/5/7 균등 선택이다. rows 3~8 중 `(11-W)/2`개 distinct transition row를 균등 선택하고 해당 row를 지날 때 centered contiguous odd width를 2씩 감소시킨다. profile은 단조이고 adjacent decrement≤2다. rows 9~11은 width W `Open+NoBuild`, rows 12~17은 exact 180° projection이며 profile 밖은 Blocked다.

**`OuterGenerator`:** length 5/7/9/11, maxWidth 3/5, shape Diamond/Oval/Irregular을 각각 균등 선택한다. 모든 포함 row는 col 5 중심의 단일 contiguous odd-width Blocked segment이고 row 10은 maxWidth다. upper profile을 180° 투영하며 adjacent width delta≤2, connected, no holes를 보장한다. Diamond는 1→3→5 monotonic ramp, Oval은 tapered ends와 max-width plateau, Irregular은 공통 제약 내 임의 odd-width profile이다. rows 9~11의 mass 바깥 Open route는 NoBuild이며 validator가 left/right width≥3과 connectivity를 검사한다.

**`ThreeLaneGenerator`:** L 5/7/9/11에 대한 band는 8~12/7~13/6~14/5~15다. band row는 cols 0~2/4~6/8~10을 `Open+NoBuild`, cols 3/7을 Blocked로 고정한다. band 밖은 모두 `Open+Allowed`이고 즉시 merge하며 transition obstacle이나 longitudinal protected corridor는 없다. band 내부 paired mine은 mirrored left/right lane에 lane당 최대 1개, middle lane은 odd count의 `(5,10)` singleton만 허용한다. 나머지 pair는 merged upper/lower zone에 둔다.

##### 중립 광산 canonical orbit sampling

candidate builder는 성, 시작 광산, `InitialMapStateEvaluator`가 산출한 팀별 보호 초기 건설 타일 10개, Blocked, archetype 금지 zone을 제거한다. 남은 좌표 `p`에서 `(p, Rotate180(p))`를 만들고 두 row-major index 중 작은 쪽을 canonical representative로 삼아 reversed duplicate를 제거한다. `MinePlacement` stream으로 필요한 distinct pair slot을 center/edge weight 없이 균등 선택한다. odd count의 singleton은 고정 `(5,10)`이다.

- Open/Obstacle: 공통 제외 뒤 모든 pair 허용
- Canyon: rows 9~11 밖 widening zone
- Outer: rows 9~11 최대 1 pair, 나머지 upper/lower zone
- ThreeLane: band 내부 pair는 mirrored left/right lane이고 lane당 mine 최대 1개, 나머지는 merged zone
- spacing/adjacency filter 없음
- local constraint 위반은 후보별 동일 확률을 보존하는 rejection sampling으로 다시 선택
- global validator 실패는 chosen mine 이동·repair 없이 attempt reject

##### `MapDefinitionValidator` access metric

공정성 거리는 기존 A*에 임의의 mine 인접 타일 하나를 지정해 구하지 않는다. 각 castle의 statically walkable neighbor 전체를 distance 0 source set, target mine의 statically walkable neighbor 전체를 target set으로 하는 multi-source BFS에서 최초 target 도달 거리를 사용한다.

```text
StaticTraversable = TerrainKind == Open
                 && MineKind == None
                 && !Castle
                 && !StartingPost
```

`BuildRule.Allowed`와 `NoBuild`는 모두 traversable이고 runtime general building은 이 정적 metric에서 제외한다. 모든 castle이 모든 neutral mine access set에 도달해야 하고 Blue/Red castle source region도 상호 도달해야 한다. center C는 `Access(B,C)==Access(R,C)`, pair A/R180(A)는 `Access(B,A)==Access(R,R180(A))`와 `Access(R,A)==Access(B,R180(A))`를 모두 만족해야 한다. 같은 cross equality를 geometric `HexCoord` 거리로 독립 검사한다.

##### 보호 corridor validator

- mine placement 전 모든 보호 row cross-section: Canyon 중앙 Open≥3, Outer left/right 각각≥3, ThreeLane 각 lane 정확히3. 해당 Open은 모두 NoBuild다.
- mine placement 후 corridor+row: mine 0이면 walkable≥3, mine 1이면 walkable≥2, mine≥2는 invalid다.
- corridor별 정의된 start set→end set BFS로 연속성과 unintended disconnect 부재를 확인한다.
- exact symmetry+위 국소 폭+BFS만 검사하며 alternate route 전체를 열거하지 않는다.

##### deterministic fallback 정의

fallback은 유형×중립 광산 수 조합마다 canonical binary 또는 Unity asset 완성본을 저장하는 구조가 아니다.

**구성 요소:**

- 맵 유형별 deterministic base template 1개(총 5개)
- 각 유형이 허용하는 중립 광산 수별 prevalidated symmetric mine slot 조합
- 최초 구현은 장식 없음(`DecorationDefinition` count 0). 에셋·테마 확정 뒤에는 유형별 고정 exact 180° symmetric decoration set만 허용

fallback builder는 맵 유형과 중립 광산 수를 키로 위 정의를 조립하며 PRNG draw를 전혀 사용하지 않는다. 같은 type+mine count는 항상 같은 지형과 중립 광산 결과를 만든다.

fallback builder는 경기 선택 단계의 `StartingMineSide`를 별도 불변 입력으로 받고 새로 추첨하거나 변경하지 않는다. A/B별 완성 맵을 복제 저장하지 않으며, 기준 시작 광산 쌍에 대해 B일 때 시작 광산 좌표에만 좌우 대응 함수 `MirrorStartMine(col,row) = (10-col,row)`를 적용한다. 이는 전체 맵의 180도 대칭 함수와 별개이며 base terrain과 중립 mine slot은 변환하지 않는다.

조립 결과는 일반 생성 결과와 동일한 `MapDefinition`이며, 전용 완화 규칙 없이 전체 `MapDefinitionValidator`를 통과해야 한다. 검증 성공 후에만 canonical binary와 SHA-256 package를 만든다.

base template과 mine slot은 코드 상수 또는 schema 비종속 순수 정의로 유지한다. 과거 schema 버전의 canonical binary fallback asset을 저장하지 않으므로 schema 변경 시 fallback binary migration이 필요하지 않아야 한다.

##### `HexTile` 런타임 상태 계약

`MapDefinition`을 `HexGrid`로 구성할 때 `HexTile` 상태를 다음 네 축으로 분리한다.

| 상태 | 값 | 변경 주체 |
|------|----|-----------|
| `TerrainKind` | `Open`, `Blocked` | 맵 정의. 경기 중 불변 |
| `BuildRule` | `Allowed`, `NoBuild` | 맵 정의. 경기 중 불변 |
| `MineKind` | `None`, `Neutral`, `BlueStart`, `RedStart` | 광산 배치 목록에서 로드 시 투영. 경기 중 불변 |
| `HasBuilding` | bool | 건물 배치·철거·파괴에 따른 동적 상태 |

`MapDefinition`의 성/광산 배치 목록은 직렬화·해시의 단일 원본이다. `HexTile.MineKind`는 그 목록에서 생성한 런타임 투영이며 별도 네트워크/직렬화 원본으로 병행 유지하지 않는다.

`IsWalkable`은 mutable 저장 필드가 아니라 다음 계산 프로퍼티다.

```text
IsWalkable = TerrainKind == Open
          && MineKind == None
          && !HasBuilding
```

판정 규칙:

```text
일반 건설 = TerrainKind == Open
         && BuildRule == Allowed
         && MineKind == None
         && !HasBuilding
         && 기존 소유권 조건

MiningPost = MineKind != None
          && !HasBuilding
          && 기존 인접 팀 타일 조건

점령 가능 = TerrainKind == Open
```

`Blocked`는 이동·일반/채굴소 건설·점령이 모두 불가능한 영구 지형이다. `HasBuilding`과 독립적이므로 건물 철거 또는 파괴가 `Blocked`를 열지 않는다. `NoBuild`는 열린 타일에서 이동·점령은 허용하고 일반 건설만 막는다.

**`HexGridRenderer`와 입력 표현:**

- `BuildRule.NoBuild`인 `Open` 좌표는 일반 타일과 같은 standard hex mesh와 높이를 생성한다. owner base color도 Neutral/Blue/Red 상태를 그대로 사용한다.
- 타일 표면에는 별도 표현 계층으로 반투명 짙은 회색 diagonal hatch 3개를 overlay한다. selection highlight와 NoBuild overlay를 독립 상태로 유지해 둘을 동시에 렌더링한다.
- `TerrainKind.Blocked`도 domain `HexGrid`와 `MapDefinition`의 231개 row-major 레코드에는 남는다. canonical codec/hash와 `MapDefinitionValidator`의 exact 180° 비교에서도 제외하지 않는다.
- `HexGridRenderer`는 blocked 좌표에 standard hex tile mesh와 collider를 생성하지 않는다. Open/NoBuild와 다른 높이 geometry도 만들지 않아 결과는 빈 공간이다.
- selection/raycast 진입점은 hit된 배경 또는 하부 collider만 신뢰하지 않는다. 변환된 맵 좌표가 범위 안인지 확인한 뒤 `TerrainKind == Open`인 경우에만 선택 대상으로 반환한다. 따라서 blocked 좌표는 이동 명령, 점령, 건설과 selection event를 발생시키지 않는다.
- 추후 obstacle prefab/asset은 blocked 좌표의 빈 공간 시각을 대체하는 presentation 확장점이다. domain `TerrainKind`, collider 기반 선택 금지, 이동/점령/건설 규칙, canonical hash와 exact symmetry 계약은 유지한다.

**`GridInteractionUseCase` 클릭 판정 순서:**

1. 기존 building action 분기
2. `MineKind` 기반 MiningPost 자격 분기
3. `TerrainKind.Blocked`
4. `BuildRule.NoBuild`
5. 일반 Open 타일

- Blocked/빈 공간은 논리 좌표가 있어도 unselectable이다. `Deselect`로 이전 highlight를 해제하고 `BuildingPlacementUI`를 닫되 토스트와 새 `TileSelected` 이벤트는 발생시키지 않는다.
- 자기 팀 소유 `Open+NoBuild`는 정상 `TileSelected`와 highlight를 발생시킨다. 건설 패널은 열지 않고 stale 패널을 닫은 뒤 `ToastKey.BuildingNotAllowed`(`이 타일에는 건설할 수 없습니다`)를 발행한다.
- 중립·적 소유 `Open+NoBuild`는 선택·highlight만 수행하고 stale 패널은 닫는다. 건설 의도가 아니므로 토스트는 없다.
- building action과 광산 분기를 먼저 처리하므로 기존 건물 상호작용과 `MineKind != None`인 광산의 MiningPost 예외가 NoBuild에 가로막히지 않는다.

**기존 코드 전환 요구:**

- mutable `HexTile.IsWalkable` 필드를 제거하거나 setter 없는 계산 프로퍼티로 전환한다.
- 광산 배치의 `HasGoldMine`/`IsWalkable=false` 이중 대입은 `MineKind` 설정으로 교체한다.
- 건물 배치 시 `IsWalkable=false` 대신 `HasBuilding=true`를 설정한다.
- 건물 철거/파괴 시 `IsWalkable=true` 복구 대신 `HasBuilding=false`만 설정한다.
- 이동, 스폰, 경로탐색은 계산된 `IsWalkable`을 읽고, 건설·점령은 위의 전용 조건을 사용한다.

##### 재경기 `MapDefinition` 생명주기

기존 재경기 요청·수락·거절 RPC와 Game 씬 재로드 구조를 유지하되, 요청 payload에 다음 enum을 추가한다.

```text
RematchMapMode.SameMap
RematchMapMode.NewMap
```

서버가 관리하는 재경기 컨텍스트는 현재 확정 `MapDefinition` package/hash와 현재 pending 요청자/mode를 보관한다.

**요청 조정:**

- 요청을 받은 상대 팝업은 요청자와 `RematchMapMode` 조건을 표시한다.
- 상대는 표시된 조건을 기준으로 수락 또는 거절한다.
- pending 요청이 없는 상태에서 서버가 먼저 받은 요청을 현재 제안으로 확정한다.
- 양측의 서로 다른 mode 요청이 교차해도 자동 시작하거나 mode를 합의된 것으로 간주하지 않는다.
- 뒤에 도착한 다른 mode 요청은 선접수 제안을 덮어쓰지 않으며, 해당 플레이어에게 선접수 조건을 제시해 명시적 응답을 받는다.

**SameMap 흐름:**

1. 현재 컨텍스트의 canonical package와 hash를 그대로 유지한다.
2. 새 seed, 생성, 정의 교체를 수행하지 않는다.
3. 수락 완료 후 기존 `NetworkSceneManager.LoadScene(Game)` 재경기 경로로 Game 씬을 재로드한다.

**NewMap 흐름:**

1. Game 종료 상태와 결과 화면을 유지하며 팀·종족·그 밖의 매치 설정은 직전 경기 값으로 고정한다.
2. Host가 새 64-bit root seed를 만들고 맵 관련 값만 새로 준비한다. `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`, 지형 세부 형태, 중립 광산 위치를 다시 선택·생성한다. 이 준비 시점의 Host `MapTestModeEnabled`를 적용해 실제 `InitialGold`를 확정하며 Client 로컬 설정은 사용하지 않는다. 장식 placement 활성화 이후에는 장식도 다시 생성하지만 최초 구현은 빈 목록을 유지한다.
3. 완성 후보의 canonical hash가 현재 definition hash와 같으면 후보를 폐기하고 또 다른 새 root seed부터 2단계를 반복한다. 같은 hash의 후보는 `NewMap` 성공으로 취급하지 않는다.
4. 기존 current definition은 그대로 보존하고 다른 hash의 새 package는 pending 후보로만 둔다.
5. 공용 `NetworkMapTransfer`가 새 package를 chunk로 전달하고 Client가 길이/버전→SHA-256→deserialize→semantic fairness validator를 통과한 뒤 `MapReady(success=true)`로 ACK한다.
6. Host도 최종 후보 검증과 양측 준비 상태를 확인한다.
7. 성공 시 pending 후보를 current definition으로 원자적 교체한다.
8. 교체 후에만 기존 Game 씬 재로드 경로를 실행한다.

생성, 전송, hash, deserialize, semantic 검증, ACK 중 하나라도 실패하면 pending 후보를 폐기한다. 기존 definition과 결과 화면은 유지하며 Game 씬을 재로드하지 않는다.

싱글플레이도 결과 UI에서 `SameMap`/`NewMap`을 선택한다. `SameMap`은 현재 정의를 재사용하고, `NewMap`은 로컬 생성·semantic 검증 성공 후 원자적으로 교체한 다음 씬을 재로드한다.

재경기 컨텍스트는 Game 씬 재로드 사이에는 유지한다. 로비 복귀, 세션 종료, 연결 종료/끊김 시 current definition, pending 후보, pending 요청을 모두 폐기한다.

#### 치팅 방어
- **서버 검증**: 모든 행동을 서버(Host)에서 검증
- **자원 관리**: 클라이언트는 읽기만 가능
- **유닛 생성**: 인구수/자원 서버 체크
- **타일 점령**: 유닛 위치 서버 관리

---

## 🗄️ 백엔드 설계

### Firebase 구조

```
Unity 클라이언트
    ↓
Firebase SDK v13.11.0 + GPGS v2.1.0
    ↓
Firebase Services
    ├─ Firebase Authentication  (로그인 — 익명 / Google Play Games / 이메일+비밀번호)
    ├─ Google Play Games Plugin (Google 로그인 OAuth 브릿지)
    ├─ Firestore               (유저 데이터 / 실시간 리더보드)
    ├─ Firebase Functions      (경기 결과 처리, IAP 영수증 검증)
    └─ Google Play Billing     (인앱 결제 — 스킨/배틀패스)
```

### UGS 연결 구조 (인게임 멀티플레이)

**실계정 (Google / 이메일)**
```
Firebase Auth (로그인) → Firebase ID Token 발급
                                ↓
        LoginUseCase.BridgeToUGSAsync()
                                ↓ SignInWithOpenIdConnectAsync("oidc-firebase", token)
        Unity Gaming Services (PlayerId 계정 귀속)
                                ↓
        UGS Lobby + Relay + Cloud Save + Leaderboard + Economy
                                ↓
        NGO 멀티플레이 세션
```

**익명 계정**
```
Firebase Auth (익명 로그인)
        ↓
LoginUseCase.BridgeToUGSAsync()
        ↓ SignInAnonymouslyAsync (기기 종속 PlayerId)
Unity Gaming Services (Lobby + Relay)
        ↓
NGO 멀티플레이 세션
```

### 로그인 흐름 (AuthSystemRules.md 참조)

| 방식 | 구현 파일 | 상태 |
|------|----------|------|
| 익명 로그인 | `FirebaseAuthService.cs` | ✅ 코드 완료 |
| Google Play Games | `FirebaseAuthService.cs` | ✅ 코드 완료 (GPGS 클라이언트 ID 미설정) |
| 이메일+비밀번호 | `FirebaseAuthService.cs` | ✅ 코드 완료 (Firebase Console 설정 미완료) |
| 계정 연동 (익명→실계정) | `AccountLinkUseCase.cs` | ✅ 코드 완료 |

### Firebase Functions 예정 기능

| 함수 | 역할 |
|------|------|
| `completeMatch` | 경기 결과 처리 + Firestore 랭킹 갱신 |
| `verifyPurchase` | Google Play Billing IAP 영수증 검증 |

---

## 💾 데이터베이스 스키마

### Firestore 컬렉션 구조

#### users/{firebaseUid}
```json
{
  "displayName": "한용희",
  "stats": {
    "totalGames": 120,
    "wins": 65,
    "losses": 55,
    "rankPoints": 1450
  },
  "inventory": {
    "races": ["human", "spirit"],
    "skins": []
  }
}
```

#### matches/{matchId}
```json
{
  "mode": "custom",
  "duration": 635,
  "players": {
    "blue": { "userId": "firebase_uid_A", "race": "human" },
    "red": { "userId": "firebase_uid_B", "race": "spirit" }
  },
  "result": {
    "winner": "blue",
    "blueStats": { "tilesControlled": 48, "unitsKilled": 35 },
    "redStats": { "tilesControlled": 32, "unitsKilled": 28 }
  },
  "timestamp": "2026-06-05T12:00:00Z"
}
```

#### leaderboard/{rankId}
```json
{
  "userId": "firebase_uid_A",
  "displayName": "한용희",
  "rankPoints": 1450,
  "wins": 65,
  "updatedAt": "2026-06-05T12:00:00Z"
}
```

---

## 🎨 디자인 패턴

### 1. Singleton Pattern
```csharp
public class GameManager : MonoBehaviour {
    public static GameManager Instance { get; private set; }
    
    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

### 2. Object Pool Pattern
```csharp
public class ObjectPool<T> where T : Component {
    Queue<T> pool = new Queue<T>();
    T prefab;
    
    public T Get() {
        if (pool.Count > 0) return pool.Dequeue();
        return Object.Instantiate(prefab);
    }
    
    public void Return(T obj) {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

### 3. Command Pattern
```csharp
public interface ICommand {
    void Execute();
    void Undo();
}

public class SpawnUnitCommand : ICommand {
    Unit unit;
    HexCoord position;
    
    public void Execute() {
        UnitFactory.Create(unit, position);
    }
    
    public void Undo() {
        UnitFactory.Destroy(unit);
    }
}
```

### 4. State Pattern
```csharp
public interface IUnitState {
    void Enter(Unit unit);
    void Update(Unit unit);
    void Exit(Unit unit);
}

public class IdleState : IUnitState {
    public void Enter(Unit unit) { unit.StopMoving(); }
    public void Update(Unit unit) {
        if (unit.FindEnemy() != null) {
            unit.ChangeState(new AttackState());
        }
    }
    public void Exit(Unit unit) { }
}
```

### 5. Observer Pattern (UniRx)
```csharp
// 이벤트 발행
GameEvents.OnUnitSpawned.OnNext(new UnitSpawnedEvent(unit));

// 이벤트 구독
GameEvents.OnUnitSpawned
    .Subscribe(e => Debug.Log($"Unit spawned: {e.Unit.Type}"))
    .AddTo(this);
```

### 6. Factory Pattern
```csharp
public class UnitFactory {
    Dictionary<UnitType, GameObject> prefabs;
    
    public Unit Create(UnitType type, Vector3 position) {
        var prefab = prefabs[type];
        var unit = Object.Instantiate(prefab, position, Quaternion.identity);
        return unit.GetComponent<Unit>();
    }
}
```

### 7. Strategy Pattern
```csharp
public interface IRaceStrategy {
    void ApplyBonus(Unit unit);
}

public class HumanRaceStrategy : IRaceStrategy {
    public void ApplyBonus(Unit unit) {
        unit.Stats.AttackDamage *= 1.1f;
    }
}
```

### 8. UI DOTween 애니메이션 패턴 (2026-03-19)

인게임 UI 패널의 등장/퇴장 애니메이션을 일관되게 처리하기 위한 프레임워크.

#### UIAnimator (static 헬퍼)

`Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs` — DOTween Sequence를 반환하는 정적 메서드 모음.

```csharp
// 사용 가능한 메서드
UIAnimator.PopupShow(CanvasGroup cg, Transform tf, float duration);
UIAnimator.PopupHide(CanvasGroup cg, Transform tf, Action onComplete, float duration);
UIAnimator.SlideInFromBottom(RectTransform rt, CanvasGroup cg, float offset, float duration);
UIAnimator.SlideOutToBottom(RectTransform rt, CanvasGroup cg, Action onComplete, float offset, float duration);
UIAnimator.SlideInFromTop(RectTransform rt, CanvasGroup cg, float offset, float duration);
UIAnimator.SlideOutToTop(RectTransform rt, CanvasGroup cg, Action onComplete, float offset, float duration);
```

#### UIManager — 전역 공통 UI 싱글톤

`Assets/_Project/Scripts/Presentation/UI/UIManager.cs` — Login 씬에서 1회 생성, DontDestroyOnLoad로 모든 씬에서 유지되는 공통 UI 매니저.

```
[UI Systems] (Login.unity)
└─ UIManager (SingletonMonoBehaviour<UIManager> + IUIManager)
    └─ UIManager Canvas (SortingOrder 100)
        └─ SafeAreaContainer (SafeAreaFitter)
            ├─ ConfirmPopup     ← _confirmPopup 연결
            └─ LoadingIndicator ← _loadingIndicator(CanvasGroup) 연결

SplashOverlay Canvas (씬 루트, SortingOrder 200)
└─ SplashOverlay (CanvasGroup + SplashOverlayView)
    ├─ Background (SafeArea 밖 — 전체화면)
    └─ SafeAreaContainer
        ├─ StatusText ("로딩 중...")
        └─ TapToStartText ("Tap to Start")

Toast (씬 루트) ← ToastUI 프리팹, 자체 DontDestroyOnLoad
```

**외부 호출 패턴** (null-safe — Login 씬 직접 진입 시 Instance=null 가능):
```csharp
UIManager.Instance?.ShowConfirm("메시지", onConfirm, onCancel);
UIManager.Instance?.ShowLoading(true, "로딩 중...");
UIManager.Instance?.ShowLoading(false);
```

**LoadingIndicator 설계 원칙**: 로딩 사유(씬 전환/Firebase/매칭 등)와 로딩 UI를 분리. 모든 로딩 상황에 `ShowLoading(bool, string)` 단일 API 사용.

---

#### AnimatedPanel 컴포넌트

`Assets/_Project/Scripts/Presentation/UI/Common/AnimatedPanel.cs` — 패널 GameObject에 부착하여 Show()/Hide() 호출만으로 애니메이션 자동 처리.

```csharp
// Inspector 설정
AnimationType: PopupFade | SlideFromBottom | SlideFromTop
ShowDuration: 0.25f (인게임 컨텍스트/게임 상태 패널 기준)
HideDuration: 0.2f
SlideOffset: 300f (px)
BackgroundOverlay: CanvasGroup (선택적 — 슬라이드 패널 배경용)

// 코드에서 호출
_panel.Show();
_panel.Hide(onComplete: () => { /* 퇴장 완료 후 실행 */ });
```

**배경 오버레이 규칙**: `_backgroundOverlay`(CanvasGroup 타입)가 연결된 경우 Show() 시 즉시 `alpha=1 / blocksRaycasts=true / interactable=true`, Hide() 완료 후 즉시 `alpha=0 / false / false`. 패널 슬라이드 애니메이션과 독립적으로 즉시 처리. 배경 GameObject는 항상 active 상태 유지.

**분류별 애니메이션 기준** → `Assets/_Project/Docs/UIGuidelines.md` 참조.

---

### 9. UI 팝업 구현 패턴

팝업 UI 구현 시 배경 클릭으로 창을 닫는 기능을 구현할 때 발생하는 문제를 방지하기 위해 다음 패턴을 권장합니다.

#### 문제 상황

- 팝업 패널(`BuildingPanel`)이 콘텐츠 영역보다 큰 투명한 배경을 가질 경우, 이 투명한 영역이 화면 전체를 덮는 닫기 버튼(`Background`)으로의 클릭을 가로막습니다.
- 패널의 `Raycast Target`을 끄면 패널 내부의 버튼까지 클릭이 통과해버리는 문제가 발생합니다.

#### 해결 구조

역할에 따라 게임 오브젝트를 명확히 분리합니다.

```
PopupCanvas
  ├─ Background (전체 화면, Raycast Target ON, 팝업 닫기 Button 컴포넌트)
  └─ BuildingPanel (레이아웃 그룹 역할, Image 컴포넌트 없음, Raycast Target 없음)
      ├─ PanelVisuals (실제 패널 배경 이미지, Raycast Target OFF, 순수 시각적 요소)
      └─ Buttons (버튼들, Raycast Target ON, 실제 상호작용 요소)
```

#### 핵심 원리

1.  **클릭 통과용 시각 요소**: `PanelVisuals`는 `Raycast Target`을 꺼서 시각적으로만 존재하고 모든 클릭을 통과시킵니다.
2.  **클릭 가로채기용 상호작용 요소**: `Buttons`는 `Raycast Target`을 켜서 클릭을 받고 자신의 기능을 수행합니다.
3.  **최후의 클릭 수신자**: 패널의 빈 공간이나 버튼이 아닌 곳을 클릭하면, 모든 클릭은 최하단에 깔린 `Background`에 도달하여 팝업을 닫는 `onClick` 이벤트를 실행합니다.

이 구조는 UI의 시각적 표현과 상호작용 로직을 분리하여 예측 가능하고 안정적인 동작을 보장합니다.

### 9. PopupClosedFrame (팝업 닫힘 프레임 보호)

팝업이 닫힌 직후 같은 프레임에서 배경 클릭이 통과하는 문제를 방지하는 패턴.

#### 문제 상황

- 팝업 Background 버튼 클릭 → `Close()` 호출 → 같은 프레임에서 `InputHandler.HandleClick`이 실행
- 결과: 팝업 뒤의 타일이 의도치 않게 클릭됨

#### 해결 방법

```csharp
// BuildingPlacementUI / ProductionPanelUI
public static int ClosedFrame { get; private set; } = -1;

void Close() {
    ClosedFrame = Time.frameCount;
    gameObject.SetActive(false);
}
```

```csharp
// InputHandler에서 체크
if (Time.frameCount == BuildingPlacementUI.ClosedFrame) return;
if (Time.frameCount == ProductionPanelUI.ClosedFrame) return;
```

각 팝업 UI가 `ClosedFrame`에 닫힌 프레임 번호를 기록하고, `InputHandler`가 같은 프레임의 클릭을 무시합니다.

---

## 🔷 육각형 그리드 시스템

### Cube Coordinates
```
육각형 좌표계:
    q (column)
    r (row)
    s = -q - r

   (-1,1)  (0,1)
      \    /
  (-1,0) (0,0) (1,0)
      /    \
   (0,-1) (1,-1)
```

### 듀얼 Orientation 지원

두 가지 타일 방향을 런타임에서 전환 가능:

| 항목 | PointyTop | FlatTop |
|------|-----------|---------|
| 타일 모양 | 꼭지점 12시 | 변 12시 |
| 그리드 크기 | 7×17 | 10×29 |
| TileWidth | 0.866 | 1.0 |
| TileHeight | 0.866 | 0.866 |
| Offset 방식 | even-r (홀수 행 시프트) | even-q (홀수 열 시프트) |
| 아트 방향 수 | 3 (NE, E, SE) | 4 (N, NE, SE, S) |

```csharp
// OrientationConfig: Orientation별 그리드 설정
[System.Serializable]
public class OrientationConfig {
    public int GridWidth;
    public int GridHeight;
    public float TileWidth;
    public float TileHeight;
}

// GameConfig에서 PointyTop/FlatTop 인스턴스로 관리
public OrientationConfig PointyTop = new OrientationConfig { ... };
public OrientationConfig FlatTop = new OrientationConfig { ... };

// 런타임 맵 전환
public void LoadMap(HexOrientation orientation) {
    OrientationConfig oc = (orientation == HexOrientation.FlatTop)
        ? _config.FlatTop : _config.PointyTop;
    // 설정 적용 → 그리드 생성 → UseCase → 렌더링 → 카메라 → 유닛
}
```

### HexCoord 구조체
```csharp
public struct HexCoord {
    public int Q, R;
    public int S => -Q - R;

    public static int Distance(HexCoord a, HexCoord b) {
        return (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.R - b.R) + Mathf.Abs(a.S - b.S)) / 2;
    }
}
```

### A* 경로찾기 (커스텀 구현)
```csharp
// HexPathfinder: 커스텀 A* 경로탐색
// 헥스 그리드 특화, 6방향 이웃 탐색, 이동 불가 타일 우회
// blockedCoords: 적 유닛 좌표 등 추가로 이동 불가 처리할 좌표 집합
List<HexCoord> path = HexPathfinder.FindPath(grid, start, goal, blockedCoords);
```

**경로 차단 (blockedCoords)**:
- 모든 다른 유닛(아군/적군 무관)의 현재 Position을 이동 불가로 처리
- **같은 팀** 유닛의 ClaimedTile(이동 중 선점 타일)도 차단 목록에 포함 → 아군끼리 겹침 방지
- **적 팀**의 ClaimedTile은 차단하지 않음 → 적과의 타일 경합은 전투로 해결
- UnitMovementUseCase가 RequestMove() 시 자기 자신을 제외한 모든 살아있는 유닛 좌표 + 같은 팀 ClaimedTile을 HashSet으로 구성하여 전달
- **목표 타일은 차단 체크 제외** (2026-03-18 수정): 경로 중간 타일에만 blocked 적용. 목표 타일도 blocked 체크 시, 인접 타일이 모두 선점되면 Castle에 도달 불가한 교착 상태 발생 가능

**ClaimedTile (이동 중 타일 선점)**:
- UnitData.ClaimedTile (HexCoord?) — Lerp 시작 전 설정, Lerp 완료 후 해제
- 같은 팀 유닛만 이 타일을 이동 불가로 인식 (경로탐색 시 우회)
- 적 팀에게는 투과 → 같은 타일에 적이 진입 시 전투 발생

**Per-step 타일 가용성 체크 (이동 중 실시간 검증)**:
- MoveAlongPath에서 각 스텝 시작 전 `IsTileBlockedBySameTeam()` 호출
- 같은 팀 유닛의 Position 또는 ClaimedTile이 다음 타일과 겹치면 차단 판정
- 차단 시 현재 위치에서 최종 목적지까지 재탐색 (RequestMove) → 새 경로로 교체
- 재탐색 실패 시 이동 중단 (Idle 복귀)
- 적 팀은 체크하지 않음 — 전투로 해결

**유닛 스폰 검증**:
- UnitSpawnUseCase.SpawnUnit()에서 계산된 타일 `IsWalkable` 검증 + 유닛 점유 검증 (GetUnitAt)
- 건물이 있거나 다른 유닛이 이미 있는 타일에는 유닛 생성 불가

---

## 🤖 AI 시스템

### 유닛 AI 상태머신 (MVP 목표)
```
Idle State
   ↓
 적 발견?
   ↓
Attack State
   ↓
 적 사망?
   ↓
Move State (랠리 포인트)
   ↓
도착
   ↓
Idle State
```

### AI 스크립트 구조 (MVP 목표)
```csharp
public class UnitAI : MonoBehaviour {
    IUnitState currentState;
    Unit unit;

    void Update() {
        currentState?.Update(unit);
    }

    public void ChangeState(IUnitState newState) {
        currentState?.Exit(unit);
        currentState = newState;
        currentState.Enter(unit);
    }
}
```

### 현재 구현: 전투 시스템 (프로토타입)

프로토타입에서는 State 패턴 대신 코루틴 기반으로 이동→공격 흐름 구현.

#### IDamageable 인터페이스

유닛과 건물의 전투 대상을 통합하는 인터페이스:
```csharp
public interface IDamageable {
    int Id { get; }
    TeamId Team { get; }
    HexCoord Position { get; }
    int Hp { get; }
    int MaxHp { get; }
    bool IsAlive { get; }
    void TakeDamage(int damage);
}
```
UnitData와 BuildingData 모두 IDamageable을 구현하여 UnitCombatUseCase가 동일한 로직으로 공격 가능.

### 싱글플레이 AI 오퍼넌트 시스템 (2026-06-10 확정)

싱글플레이 모드에서 Red 팀을 자동으로 운영하는 AI 오퍼넌트. 빌드오더 스크립트와 반응 시스템으로 구성된다.

**아키텍처 (레이어 배치):**
- `DifficultyLevel`, `AIActionType`, `BuildOrderStep` → `Hexiege.Domain` (`Scripts/Domain/AI/`)
- `AIScenarioConfig` ScriptableObject → `Hexiege.Infrastructure`
- `AIOpponentController` → `Hexiege.Application` (빌드오더 + 반응 시스템)
- `GameBootstrapper` → 시나리오 번들 로드 및 AI 주입 (composition root)

> DifficultyLevel·AIActionType·BuildOrderStep를 Domain 레이어로 분리한 이유: Application 레이어의 AIOpponentController가 이 타입들을 직접 참조하므로, Infrastructure에 두면 Application→Infrastructure 직접 의존이 발생한다. Domain 분리로 의존 방향(Application→Domain)을 정상화했다.

**에셋 구조:**
- 종족별 1개 에셋 × 3개 시나리오
  - `AIScenarioConfig_Human.asset` (Human_A / Human_B / Human_C)
  - `AIScenarioConfig_Spirit.asset` (Inferno / Torrent / Quake)
  - `AIScenarioConfig_Transcendence.asset` (Rush / Flora / Beast)
- 게임 시작 시 `GameRaceContext.RedRace`로 종족을 판별한 뒤 해당 에셋에서 무작위 시나리오 1개를 선택한다.

**ScriptableObject 경로:** `Resources/Config/AIScenarioConfig_{종족}.asset`

#### 중앙 집중 스탯 관리 — ScriptableObject 기반 (2026-04-25 전환)

타입별 기본 스탯을 ScriptableObject로 관리. Inspector에서 코드 수정 없이 수치 편집 가능.

```csharp
// UnitStats: UnitStatsConfig ScriptableObject 기반
// 에셋: Assets/_Project/Resources/Config/UnitStatsConfig.asset
// 내부: Dictionary<UnitType, StatValues> O(1) 조회
// API: UnitStats.GetMaxHp(type), GetAttackPower(type), GetAttackRange(type),
//      GetMoveSeconds(type), GetAttackCooldown(type)
// 유닛 9종 (Pistoleer=0 ~ LionKnight=8) + 신규 16종 추가 예정
// 사거리 임계값(world units) = AttackRange * TileHeight(0.866) + 0.05f (Epsilon)

// BuildingStats: BuildingStatsConfig ScriptableObject 기반
// 에셋: Assets/_Project/Resources/Config/BuildingStatsConfig.asset
// 내부: Dictionary<(BuildingType, RaceId), StatValues>
// BuildingType 26종 × 3종족(Human/Spirit/Transcendence) 조합
// API: BuildingStats.GetMaxHp(type, race), GetGoldCost(type, race),
//      BuildingStats.GetAttackCooldown(type, race), GetUpgradeCost(type, race),
//      BuildingStats.GetTotalInvestedCost(type, race) — 누적 투자비 캐시
```

#### 전투 스탯

**유닛 (UnitData)**:
```csharp
public class UnitData : IDamageable {
    public int MaxHp { get; }          // UnitStats에서 결정
    public int Hp { get; private set; }
    public int AttackPower { get; }    // UnitStats에서 결정
    public int AttackRange { get; }    // UnitStats에서 결정
    public bool IsAlive => Hp > 0;
    public HexCoord? ClaimedTile { get; set; } // 이동 중 선점 타일 (같은 팀만 차단)
}
```

**건물 (BuildingData)**:
```csharp
public class BuildingData : IDamageable {
    public int MaxHp { get; }          // BuildingStats에서 결정
    public int Hp { get; private set; }
    public bool IsAlive => Hp > 0;
}
```

#### 전투 흐름 (이동 중 거리 기반 전투)
```
유닛 이동 명령 (InputHandler / AutoMove)
  ↓
A* 경로 계산 (아군/적군 Position 우회 + 같은 팀 ClaimedTile 우회)
  ↓
각 스텝마다:
  ↓
다음 타일 가용성 체크 (IsTileBlockedBySameTeam)
  ↓ 차단됨
현재 위치 → 최종 목적지 재탐색 (RequestMove) → 새 경로로 교체
  ↓ 통과
ClaimedTile = 다음 타일 (같은 팀 겹침 방지)
  ↓
타일→타일 Lerp 이동 (UnitView 코루틴)
  ↓ Lerp 중 매 프레임
사거리 내 적(유닛/건물) 탐색 (UnitCombatUseCase.TryAttack)
  ↓ 적 발견
이동 중단 → 공격 방향 계산 → IDamageable.TakeDamage() → 이벤트 발행
  ↓
적 HP ≤ 0? → EntityDied 이벤트 → View 파괴 + Dictionary 제거
  ↓
사거리 내 적이 남아있으면 반복 공격
  ↓
전투 승리 → 남은 Lerp 계속 → 타일 중앙 도착 = 점령
  ↓
ClaimedTile 해제, ProcessStep(Position 갱신 + SetOwner)
  ↓
모든 경로 이동 완료 → Idle 상태 복귀
```

**핵심 규칙: 타일 중앙 도착 = 전투 승리 = 점령**
- 전투는 Lerp 이동 중에 거리 기반으로 발동 (타일 중앙 도착 전)
- 패배한 유닛은 타일 중앙에 도달하지 못하므로 점령 불가
- SetOwner는 Lerp 완료 후 ProcessStep에서만 호출 (변경 없음)

#### 사망 처리 (Dead Entity Cleanup, 2026-05-18 강타입 분리 / 2026-06-08 NGO Despawn 패턴 수정)
```
UnitCombatUseCase.ExecuteAttack()
  ↓ target.IsAlive == false
GameEvents.OnUnitDied (유닛) 또는 OnBuildingDied (건물) 강타입 이벤트 발행
  ↓
[유닛 사망 — 싱글플레이]
1. UnitView 구독(Presentation) → EffectManager.PlayUnitDeath() → Destroy(gameObject)
2. UnitSpawnUseCase.RemoveUnit() → Dictionary에서 제거

[유닛 사망 — 멀티플레이]
※ 레이어 규칙: Presentation(UnitView)은 Unity.Netcode 직접 참조 금지 → NetworkContext 홀더만 사용
1. 서버: NetworkCombatController(Infrastructure) 구독
   → EntityDiedClientRpc 발행 → NetworkObject.Despawn(destroy:true)
   → 모든 클라이언트에 GO 파괴 전파 (Destroy(gameObject)는 NGO 전파 불보장)
   → 서버측 UnitView도 OnUnitDied 수신 → EffectManager.PlayUnitDeath() (서버 화면 이펙트)
2. 클라이언트: UnitView.OnUnitDied() → NetworkContext.IsNetworkActive && !IsNetworkServer → return (처리 없음)
   클라이언트: NetworkUnit.OnNetworkDespawn() 자동 호출 → EffectManager.PlayUnitDeath() → GO NGO 파괴
3. 서버/클라이언트 공통: EntityDiedClientRpc 수신 측에서 OnUnitDied 재발행 → UnitSpawnUseCase.RemoveUnit()

[건물 사망/철거]
1. BuildingFactory가 OnBuildingDied 구독 → _buildingObjects Dict O(1) 조회 → GO Destroy
   (BuildingView.cs 삭제됨 — BuildingFactory가 GO 파괴 책임 인수)
2. BuildingPlacementUseCase.RemoveBuilding() → Dictionary 제거 + 타일 `HasBuilding=false` (계산된 `IsWalkable` 자동 갱신, 영구 지형/광산 상태 유지)
```

#### 타일 선택 하이라이트 처리

```csharp
// HexTileView의 OnTileSelected 이벤트 핸들러
// Coord == PreviousCoord일 때 = 선택 해제 이벤트 (Deselect)
// Coord != PreviousCoord일 때 = 새 타일 선택
if (e.Coord == _coord)
{
    _isSelected = !(e.PreviousCoord.HasValue
                    && e.PreviousCoord.Value == e.Coord);
    UpdateColor();
}
```

> **버그 수정 이력:** 초기 구현에서 `_isSelected = !_isSelected` (토글)을 사용했으나,
> Deselect() 이벤트(Coord == PreviousCoord)에서 Check1(해제)과 Check2(토글)가 동일 타일에서
> 연속 실행되어 하이라이트가 잔존하는 버그 발생. 결정적(deterministic) 할당으로 수정.

#### 이벤트 기반 전투 통신 (2026-05-18 강타입 이벤트로 분리)

유닛/건물 사망 이벤트를 강타입으로 분리하여 구독자의 is-캐스팅 필터 전면 제거:

```csharp
// 공격 이벤트 (UnitCombatUseCase → UnitView)
GameEvents.OnEntityAttacked.OnNext(new EntityAttackedEvent(attacker, target));

// 유닛 사망 이벤트 (UnitCombatUseCase → UnitView, UnitSpawnUseCase 등)
GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unitData));

// 건물 사망/철거 이벤트 (BuildingPlacementUseCase → BuildingFactory 등)
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(buildingData));
```

**이벤트 매칭**: View에서 자신의 엔티티를 식별할 때 **Id 비교** 사용:
```csharp
// UnitView에서
if (e.Unit.Id == _unitData.Id) { /* 이 유닛이 사망 */ }
// BuildingFactory에서
if (_buildingObjects.TryGetValue(e.Building.Id, out var go)) { Destroy(go); }
```

#### 피격 표현 큐 (Hit Presentation Queue, 2026-07-12 전투 타격 타이밍 동기화)

데이터(HP)는 서버 시계를, 연출(HP 텍스트·피격 VFX·타격 반응)은 각 클라이언트의 로컬 타격 프레임을 따르도록 역할을 분리한다.

- `EntityDamagedEvent` / `SyncHealthClientRpc`에 **공격자 정보(Id + 유닛 여부)** 를 추가했다. 도메인 HP는 서버 값 도착 즉시 갱신(서버 권위 유지)하되, 연출은 보류한다.
- `HitPresentationQueue`(Presentation 신규)가 피격 정보를 공격자별로 큐에 보류하고, 공격자의 로컬 `UnitView.OnAttackHit` 시점에 FIFO 방출한다. 타임아웃(쿨다운×1.5)·타겟 사망·공격자 사망·공격자 전투 중단 시 즉시 방출한다.
- `HitFrameTimes`(데미지 타격 시점)는 Attack 클립 `OnAttackHit` Animation Event 시간에서 `UnitFactory`가 자동 추출한다(수동 입력은 폴백). 데미지는 항상 서버 타이머로 적용하며 Animator 상태에 종속시키지 않는다.
- 연출 API: `EffectManager.PlayUnitHit`(+`UnitEffectConfig.hitPreset`), `PlayBuildingAttack`(타워 발사, +`BuildingEffectConfig.attackPreset`), `TracerProjectile`(원거리 트레이서, +`tracerPreset`). HP 텍스트는 `FloatingHpTextSpawner.ShowDamage` 단일 진입점으로 통합.
- 상세 규칙: `GameSystemRules_Units.md` 규칙 17~21, `GameSystemRules_Buildings.md` 방어 타워 시스템 규칙 12(타워 발사 연출).
- 구 `UnitEffectView.cs`(DEPRECATED)는 이 파이프라인이 역할을 대체하여 삭제됨.

#### 유닛 애니메이션 상태 동기화 (2026-07-13 엣지 RPC → NetworkVariable 레벨 동기화)

멀티플레이에서 유닛 애니메이션 상태(Walk / Attack)의 동기화 방식을 **"상태가 바뀌는 순간에만 1회성으로 쏘는 엣지 트리거 RPC"** 에서 **"현재 상태 값 자체를 공유하는 레벨 동기화(NetworkVariable)"** 로 전환했다.

- **전(엣지 RPC):** `StartWalkAnimationClientRpc` / `GameEvents.OnUnitWalkStarted` 등 1회성 신호로 클라이언트에 상태 변화를 통지. 신규 유닛이 구독을 완료하기 전에 첫 신호가 도착하면 유실되고 이후 바로잡을 수단이 없었다(스폰 레이스 — 클라 스폰 223기 중 222기에서 첫 Walk RPC 유실 실측). 갓 생산된 유닛에서 걷기 모션이 안 나오는 원인.
- **후(레벨 동기화):** `NetworkUnit`에 `UnitAnimState`(None / Walk / Attack) `NetworkVariable`(ReadPermission=Everyone / WritePermission=Server, `_unitId` 패턴 재사용)을 두고 서버가 상태 진입 지점에서 값을 쓴다. 클라이언트는 `OnValueChanged` + **스폰 시 현재 값 자동 적용**으로 항상 서버의 현재 상태를 받으므로 신호 유실이 구조적으로 불가능하다. 호스트/싱글플레이는 기존 로컬 Animator 직접 제어를 유지.
- **적용 시점 봉합:** 애니메이션 적용이 `UnitView.Initialize`(애니메이터 준비)보다 이르면 무음 실패하므로, `UnitView.Initialize` 말미에서 `NetworkUnit.ReapplyAnimStateToView()`로 현재 값을 **멱등 재적용**한다.
- **역할 분리 유지:** 데미지 판정은 서버 타이머(규칙 18), 조준 회전은 별도 타겟 참조(규칙 12·15)로 애니메이션 상태 값과 분리. `_combatAnimationSent`는 애니메이션이 아니라 데미지(`ExecuteAttack`)·타겟 RPC 게이팅 가드로 유지.
- **부수 위치 보정:** 재경로 재발급 시 첫 스텝이 최종 목적지 역방향으로 향하던 "뒤로 밀림"(서버 경로 자체가 원인, 클라 보간 무죄)은 `MoveTo`의 `AlignPathStartToTransform`로 실제 `transform` 전방 타일(`FindForwardClosestTile`)에서 재발급하여 보정(규칙 11 강제).
- 상세 규칙: `GameSystemRules_Units.md` 규칙 22(규칙 21을 상위 대체). task: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/`.

#### 특수 공격 전략 핸들러 구조 (2026-07-17 도끼병 휩쓸기형 AoE)

일반 유닛의 단일 타깃 피해와 별개로, 특수 능력(휩쓸기 / 착탄 / 파도 / DoT / 힐)을 가진 유닛의 추가 피해·효과를 **전략(핸들러) 패턴**으로 분리했다. 특수 유닛 5종(BattleAxe / QuakeSpirit / TorrentSpirit / MushroomBomber / BloomFairy) 중 도끼병이 첫 구현.

- **계약/구성:** `ISpecialAttackBehavior.Apply(SpecialAttackContext)` 인터페이스 + `SpecialAttackContext`(공격자·주 타깃·유닛 목록·재사용 피해 헬퍼·월드 좌표 조회 수단·reach/arc) + `SpecialAttackRegistry`(`UnitType → 핸들러` 매핑, 현재 `BattleAxe → SweepAttackBehavior`만) + 유닛별 핸들러. 모두 `Scripts/Application/Combat/`. `UnitType` 키 매핑이라 인스펙터 배선 불필요.
- **피해 수렴점 단일화:** `UnitCombatUseCase.ExecuteAttack`의 인라인 단일 피해 로직을 `ApplyDamageToVictim` 헬퍼로 추출하여 주 타깃과 AoE 대상이 **같은 피해·이벤트·사망 처리 경로**를 쓰게 했다(멀티플레이 HP 동기화 일관). `ExecuteAttack` 말미에 특수 공격 훅 1줄만 추가 → 신규 특수 유닛은 핸들러 + 레지스트리 1줄로 확장하며 `ExecuteAttack` 재수정 불필요.
- **휩쓸기 판정(SweepAttackBehavior):** 타일 소속이 아니라 **월드 좌표 전방 부채꼴**(forward = 공격자 → 주 타깃, XZ 거리 ≤ `sweepReach` AND 각도 ≤ `sweepArcHalfAngle`). 월드 좌표는 `IEntityPositionProvider`(서버 권위). 아군/사망/공격자/주 타깃 제외, 건물 미대상.
- **튜닝 SO:** `SpecialAttackConfig`(Infrastructure/Config, `sweepReach`·`sweepArcHalfAngle`). GameBootstrapper가 SO 값을 읽어 핸들러에 **float로 주입**(Application → Infrastructure 역참조 회피). 에셋 생성 + 배선은 `CreateSpecialAttackConfigAsset.cs`가 멱등 자동화. ⚠️ 에셋 생성 ≠ 씬 배선 — 미배선 시 폴백값 사용.
- **AoE 연출 동시 방출:** `HitPresentationQueue`가 공격자 `HitFrameTimes.Length ≤ 1`이면 보류 큐 전부 방출(휩쓸기 N마리 동시 표시), `> 1`이면 신호당 1건(다중 히트 유닛 회귀 없음).
- 상세 규칙: `GameSystemRules_Units.md` 규칙 23~27. task: `_Tasks/2026-07-16/18_06_battleaxe-aoe/`.

### 건물 배치 시스템 (MVP Phase 1)

프로토타입 완료 후 첫 MVP 기능. 건물 배치 + 시각화만 구현 (자원/생산 시스템 미포함).

#### 건물 타입 (2026-05-17 26종 확장)
```csharp
// BuildingType enum — 26종
// Castle×3종족, MiningPost×3종족
// 종족별 생산라인 3단계: HumanBarracks1/2/3, AncientGrove1/2/3, PrimalSanctuary1/2/3
// AutoTower×3종족 (Human=CannonTower, Spirit=RuneSpire, Trans=VineTower)
//   ⚠️ 2026-08-10 정정: Trans 방어 타워는 VineTower다(이전 표기 "Trans=MistShrine"은 오류).
//   MistShrine은 방어 타워가 아니라 공격하지 않는 별도 힐 건물이며 HealShrine = 6 이라는
//   독립 enum 값을 쓴다(AutoTower = 2와 완전히 별개). 규칙:
//   GameSystemRules_Buildings.md — MistShrine 물안개 힐 시스템
//   (2026-08-12 구현 완료 / 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증).
// 추가: ResearchLab (미구현)
//
// BuildingTypeHelper.cs (Domain 레이어):
//   IsProductionBuilding(type) — 생산 패널 표시 여부
//   GetStage(type)             — 건물 단계 (1/2/3)
//   GetNextStage(type)         — 업그레이드 대상 타입
//   CanUpgrade(type)           — 업그레이드 가능 여부
//   CanShowActionPanel(type)   — 비생산 건물 액션 패널 표시 여부
//
// 내부 구조 (2026-06-25 Phase 2):
//   생산건물 메타데이터를 단일 Dictionary<BuildingType, BuildingMeta> lookup table로 보유.
//   BuildingMeta = { IsProduction, Stage, NextStage }. 위 IsProductionBuilding/GetStage/
//   GetNextStage는 이 table을 TryGetValue로 조회만 하며(미등록=비생산), 신규 생산건물 추가 시
//   table에 한 행만 추가하면 세 메서드가 자동 정합. 공개 API 시그니처는 동일.
```

#### 건물 데이터 (IDamageable 패턴)
```csharp
public class BuildingData : IDamageable {
    public int Id { get; }              // 자동 발급
    public BuildingType Type { get; }   // 불변
    public TeamId Team { get; }         // 불변
    public HexCoord Position { get; }   // 불변
    public int MaxHp { get; }           // BuildingStats에서 결정
    public int Hp { get; private set; } // 피격 시 감소
    public bool IsAlive => Hp > 0;
    public void TakeDamage(int damage); // 데미지 적용
}
```

#### 건물 배치 흐름 (상세)

건물 배치 흐름은 `InputHandler`에서 시작하여 `UI`, `UseCase`, `Factory`를 거치는 단방향 데이터 흐름을 따릅니다.

1.  **입력 감지 (InputHandler)**
    -   플레이어가 UI가 아닌 지역을 클릭하면 `InputHandler.HandleClick`이 호출됩니다.
    -   클릭된 좌표의 타일이 현재 플레이어 소유의 비어있는 타일인지 `BuildingPlacementUseCase.CanPlaceBuilding`을 통해 검증합니다.
    -   조건이 맞으면, `BuildingPlacementUI.Show(coord, team)`를 호출하여 건물 선택 팝업을 띄웁니다.

2.  **UI 상호작용 (BuildingPlacementUI)**
    -   `Show()`가 호출되면 팝업 UI가 활성화됩니다.
    -   플레이어가 `BarracksButton` 또는 `MiningPostButton`을 클릭합니다.
    -   각 버튼의 `onClick` 이벤트는 `PlaceAndClose(BuildingType)` 메서드를 호출합니다.
    -   `PlaceAndClose`는 `BuildingPlacementUseCase.PlaceBuilding`을 호출하여 실제 배치 로직을 요청하고, 스스로 `Close()`를 호출하여 팝업을 닫습니다.
    -   (참고: 배경 클릭 시 팝업 닫기는 'UI 팝업 구현 패턴'을 따릅니다.)

3.  **로직 실행 (BuildingPlacementUseCase)**
    -   `PlaceBuilding(type, team, coord)`가 호출되면, 다시 한번 배치 가능 여부를 최종 검증합니다.
    -   `BuildingStats.GetMaxHp(type)`으로 타입별 기본 HP를 조회합니다.
    -   `BuildingData` 인스턴스를 생성합니다 (HP 포함).
    -   해당 타일의 상태를 '건설됨'으로 변경합니다 (`HexTile.HasBuilding = true`).
    -   `GameEvents.OnBuildingPlaced` 이벤트를 발행(OnNext)하여 시스템의 다른 부분에 건물 배치가 완료되었음을 알립니다.
    -   건물 파괴 시: `RemoveBuilding(id)` → Dictionary 제거 + `HexTile.HasBuilding = false`. 이동 가능 여부는 `TerrainKind`/`MineKind`와 함께 다시 계산되므로 영구 차단 지형이나 광산이 열리지 않습니다.

4.  **객체 생성 (BuildingFactory)**
    -   `BuildingFactory`는 `OnBuildingPlaced` 이벤트를 구독(Subscribe)하고 있습니다.
    -   이벤트를 수신하면, 전달받은 `BuildingData`에 맞는 건물 프리팹(`Building_Barracks.prefab` 등)을 `Instantiate`하여 월드에 생성합니다.
    -   생성된 게임 오브젝트의 `BuildingView` 컴포넌트에 `BuildingData`를 전달하여 초기화합니다.

5.  **자동 배치 (GameBootstrapper)**
    -   게임 시작 시 `GameBootstrapper.PlaceCastles` 메서드가 양 팀의 `Castle`을 지정된 위치에 자동으로 배치하며, 이는 `BuildingPlacementUseCase`를 통해 위와 유사한 로직을 실행합니다.

#### 렌더링 순서
3D 전환 이후 sortingOrder는 완전 폐기. Orthographic 55도 틸트 카메라의 Z-buffer(깊이 버퍼) 기반으로 타일/건물/유닛의 렌더링 순서가 자동 결정됨. XZ 평면 사용, Y축이 높이 방향.

#### 건물 관련 이벤트
```csharp
// 건물 배치 (BuildingPlacementUseCase → BuildingFactory)
GameEvents.OnBuildingPlaced.OnNext(new BuildingPlacedEvent(building));

// 건물 업그레이드 (BuildingPlacementUseCase → BuildingFactory, ProductionTicker 등)
GameEvents.OnBuildingUpgraded.OnNext(new BuildingUpgradedEvent(oldBuildingId, newBuilding));

// 건물 사망/철거 (BuildingPlacementUseCase → BuildingFactory)
GameEvents.OnBuildingDied.OnNext(new BuildingDiedEvent(building));
// BuildingFactory가 _buildingObjects Dict에서 O(1) 조회 후 GO Destroy
// (BuildingView.cs 삭제됨 — 2026-05-18)
```

#### 영토 확장 (건물 건설 시)

건물 배치 시 배럭 인접 6타일을 건물 팀으로 자동 점령:
```csharp
// BuildingPlacementUseCase.PlaceBuilding() 내부
var neighbors = _grid.GetNeighbors(position);
foreach (var neighbor in neighbors)
{
    if (neighbor.Owner != team)
    {
        _grid.SetOwner(neighbor.Coord, team);
        GameEvents.OnTileOwnerChanged.OnNext(
            new TileOwnerChangedEvent(neighbor.Coord, team));
    }
}
```

### 유닛 생산 시스템 (MVP Phase 2)

배럭에서 유닛을 생산하는 핵심 게임플레이 루프.

#### 생산 관련 Domain 클래스

```csharp
// UnitProductionStats: 유닛 타입별 생산 시간/비용
public static class UnitProductionStats {
    public static float GetProductionTime(UnitType type) => type switch {
        UnitType.Pistoleer => 5f, _ => 5f
    };
    public static int GetGoldCost(UnitType type) => type switch {
        UnitType.Pistoleer => 50, _ => 50
    };
    public static int GetPopulationCost(UnitType type) => 1;
}

// ProductionState: 배럭 하나의 생산 상태 (2026-04-19 PendingQueue 구조로 재작성)
public class ProductionState {
    public int BarracksId;
    public List<QueueSlot> PendingQueue;    // 수동+자동 통합 단일 큐 (최대 3 가시 슬롯)
    public List<UnitType> AutoTypes;        // 자동 등록 타입 목록 (순환 반복)
    public bool IsAutoMode;
    public int AutoIndex;                   // 자동 순환 인덱스
    // CurrentIsAuto: AutoTypes에서 파생하는 getter (2026-06-05 수동 필드 → getter 전환)
    public bool CurrentIsAuto => PendingQueue.Count > 0 && AutoTypes.Contains(PendingQueue[0].UnitType);
    public HexCoord? RallyPoint;
}
// QueueSlot: UnitType, IsCharged(골드 차감 여부), Progress(생산 진행도)
```

#### UseCase 구조

| UseCase | 역할 |
|---------|------|
| `ResourceUseCase` | 팀별 골드 관리 (시작 500, 차감/추가/조회) |
| `PopulationUseCase` | 인구수 계산 (최대=보유 타일, 사용=건물+유닛) |
| `UnitProductionUseCase` | 생산 큐/타이머/자동-수동 모드/랠리포인트 |

#### 생산 흐름 (상세)
```
배럭 배치 → RegisterBarracks(BuildingData)
  ↓
플레이어 탭 → EnqueueUnit(barracksId, type)
  → 자동 모드 해제, 현재 자동 생산 취소 (골드 환불 없음)
  → ManualQueue에 추가
  → OnProductionQueueChanged 이벤트
  ↓
Tick(dt) — ProductionTicker가 매 프레임 호출
  → TryStartNext: ManualQueue[0] 또는 AutoTypes[AutoIndex]
  → 골드/인구 부족 시 대기
  → 충족 시: 골드 차감 → CurrentProducing 설정 → OnProductionStarted
  ↓
TickProduction(state, dt)
  → ElapsedTime += dt (RequiredTime 초과 방지 캡 처리)
  → Progress >= 1.0 → CompleteProduction()
  ↓
CompleteProduction(state)
  → FindSpawnTile(barracksPos) — 인접 이동 가능 + 유닛 없는 타일
  → 스폰 불가: 대기 (매 프레임 재시도, Progress 1.0 유지)
  → 스폰 가능: UnitSpawnUseCase.SpawnUnit()
  → 자동 모드: AutoIndex 순환
  → OnUnitProduced 이벤트 (랠리포인트 정보 포함)
```

#### 런타임 유닛 의존성 주입

UnitFactory에 의존성 참조를 저장하여 생산된 유닛에 자동 주입:
```csharp
// GameBootstrapper에서 한 번 호출
_unitFactory.SetDependencyReferences(config, movement, combat, unitFactory, buildingFactory);

// UnitFactory.CreateUnitObject() 내부에서 자동 적용
unitView.Initialize(unitData);
if (_hasDependencies)
    unitView.SetDependencies(config, movement, combat, unitFactory, buildingFactory);
```

#### 생산 이벤트
```csharp
// 자원 변경 (ResourceUseCase → UI)
GameEvents.OnResourceChanged.OnNext(new ResourceChangedEvent(team, gold));

// 생산 시작 (UnitProductionUseCase → UI)
GameEvents.OnProductionStarted.OnNext(new ProductionStartedEvent(barracksId, type));

// 유닛 생산 완료 (UnitProductionUseCase → ProductionTicker)
GameEvents.OnUnitProduced.OnNext(new UnitProducedEvent(unit, rallyPoint));

// 큐 변경 (UnitProductionUseCase → UI)
GameEvents.OnProductionQueueChanged.OnNext(new ProductionQueueChangedEvent(barracksId));

// 랠리포인트 변경 (UnitProductionUseCase → ProductionTicker 마커 관리)
GameEvents.OnRallyPointChanged.OnNext(new RallyPointChangedEvent(barracksId, coord));
```

#### ProductionTicker (Presentation 브릿지)

순수 C# UseCase를 Unity Update 루프에 연결하는 MonoBehaviour:
```csharp
public class ProductionTicker : MonoBehaviour {
    void Update() {
        _productionUseCase?.Tick(Time.deltaTime);
        _resourceUseCase?.TickIncome(Time.deltaTime, ...);
        TickSiege(); // 1초 간격으로 공성 유닛 전진 체크
    }
    // OnUnitProduced 구독 → 랠리포인트 자동 이동 처리 (BFS 빈 타일 탐색)
    // OnRallyPointChanged 구독 → 마커 생성/이동/제거
    // OnEntityDied 구독 → 배럭 파괴 시 마커 Destroy + 공성 목록에서 제거
    // ShowRallyMarker/HideAllRallyMarkers — 팝업 연동
}
```

#### 공성 시스템 (Siege System)

생산된 유닛이 자동으로 적 Castle을 향해 진군하는 시스템. ProductionTicker에서 관리.

**진군 흐름:**
```
유닛 생산 완료 (OnUnitProduced)
  ↓
랠리포인트 설정됨?
  ├─ 예 → BFS 빈 타일 탐색 → 랠리포인트 근처로 이동
  │        ↓ OnMoveComplete 콜백
  │        적 Castle 방향 BFS 경로 탐색 → 이동
  └─ 아니오 → 적 Castle 방향 BFS 경로 탐색 → 직접 이동
  ↓
Castle 인접 도착 (또는 경로 상 정지)
  ↓
공성 목록(siegeUnits)에 등록
  ↓
매 1초 TickSiege()
  → Castle까지 BFS 거리 계산
  → 현재보다 가까운 빈 타일이 있으면 이동
  → Castle 인접(거리 1) 도달 시 공성 목록에서 제거 (더 이상 전진 불필요)
```

**공성 목록 관리:**
- 등록: Castle 방향 이동 완료 시 (OnMoveComplete 콜백)
- 제거 조건:
  1. Castle 인접 타일(거리 1) 도달
  2. 유닛 사망 (OnEntityDied 이벤트)
  3. GameObject 파괴 (null 체크)

**UnitView.OnMoveComplete 콜백:**
```csharp
// 이동 완료 시 1회 실행되는 콜백 (System.Action)
public System.Action OnMoveComplete { get; set; }
// MoveAlongPath 코루틴 종료 시 호출 → null로 초기화
// 용도: 랠리→Castle 체인 이동, 공성 목록 등록
```

#### 랠리포인트 마커 표시 규칙
- **설정 직후**: 마커 생성 + 3초간 표시 → 자동 숨김
- **배럭 선택(팝업 열림)**: 마커 표시 (ProductionPanelUI → ShowRallyMarker)
- **팝업 닫힘/다른 오브젝트 클릭**: 마커 숨김 (ProductionPanelUI → HideAllRallyMarkers)
- **배럭 타일에 랠리포인트 설정**: 랠리포인트 해제 + 마커 Destroy
- **배럭 파괴**: 마커 Destroy
- **마커 프리팹**: GameConfig.RallyPointPrefab (Inspector에서 할당)
- **마커 위치/회전**: GameConfig.RallyMarkerOffset / RallyMarkerEuler (Inspector 조정)

#### 랠리포인트 BFS 빈 타일 탐색
랠리포인트 타일이 점유 중일 때 유닛이 멈추는 문제를 방지하기 위해 BFS로 가장 가까운 빈 타일을 탐색:
```
Ring 0: 랠리포인트 자체 (1타일)
Ring 1: 인접 6타일
Ring 2: 그 바깥 12타일
Ring 3: 그 바깥 18타일 (최대 제한, maxRange=3)
```
- 각 타일에 대해 RequestMove 시도 → 성공하면 즉시 반환
- BFS 특성상 랠리포인트에 가장 가까운 빈 타일이 자동 선택
- 범위 내 빈 타일 없으면 이동 안 함

#### 생산 UI (ProductionPanelUI)

배럭 클릭 시 표시. 기존 UI 에셋(ui_panel_dark, ui_slot_queue, ui_bar_progress_frame 등) 활용.

**탭**: 수동 큐 추가 / **롱프레스(0.5초)**: 자동 생산 토글

#### GameConfig 경제 설정
```csharp
[Header("Economy")]
bool MapTestModeEnabled = true;   // 초기 골드 전용 테스트 모드(멀티에서는 Host 권위)
int TestStartingGold = 5000;      // 테스트 모드의 실제 게임 시작 골드
float MiningGoldPerSecond = 10f;  // 채굴소 초당 수입
int BarracksCost = 100;           // 배럭 건설 비용
int MiningPostCost = 50;          // 채굴소 건설 비용
```

---

## ⚡ 성능 최적화

### 모바일 최적화 전략

#### 1. 오브젝트 풀링
```csharp
// 유닛, 이펙트 재사용
ObjectPool<Unit> unitPool = new ObjectPool<Unit>(unitPrefab, 50);
```

#### 2. 컬링
```csharp
// 화면 밖 유닛 렌더링 비활성화
if (!IsVisible()) {
    renderer.enabled = false;
}
```

#### 3. LOD (Level of Detail)
```
멀리: 간단한 모델
가까이: 디테일한 모델
```

#### 4. 배칭
```
- Static Batching: 배경, 타일
- Dynamic Batching: 유닛 (같은 머티리얼)
```

#### 5. Addressables
```csharp
// 동적 에셋 로딩
Addressables.LoadAssetAsync<GameObject>("Units/Soldier");
```

### 타겟 성능
- **FPS**: 60fps (모바일)
- **메모리**: 300MB 이하
- **배터리**: 1시간 플레이 = 20% 소모 이하

---

## 💻 개발 환경

### Unity 프로젝트 설정
```
Unity Version: 6000.0.x (Unity 6 LTS)
Template: 3D (URP)
Platform: Android / iOS
Scripting Backend: IL2CPP
API Level: Android 7.0+ (API 24)
Target Architectures: ARM64
```

### Git 설정
```gitignore
# .gitignore
Library/
Temp/
Obj/
Build/
Builds/
Logs/
UserSettings/
.vs/
*.csproj
*.sln
```

### 빌드 설정
```
Build Settings:
- Compression Method: LZ4
- Development Build: ✅ (개발 중)
- Split Application Binary: ✅ (100MB+)
```

---


> 개발 진행 현황 및 로드맵은 `ROADMAP.md`, `PROJECT_STATUS.md` 참조.

## 📝 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| 0.43.2 | 2026-08-12 | **MistShrine 물안개 힐 구현 반영 — 아키텍처 설계 변경 없음, 상태 표기 정정.** 2026-08-10 시점의 "구현 미착수" 표기를 **코드·프리팹 구현 완료 / 에디터 싱글플레이 실기 검증 완료**로 정정하고, 신설된 구성 요소는 문서 상단 노트에 기록했다(레이어 경계·서버 권위 원칙은 기존 규칙을 그대로 따르며 새 패턴을 도입하지 않았다). **아직 완료가 아닌 것(과대 표기 금지): 멀티플레이 실기 미검증 — 건물 HP 동기화·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱은 멀티에서만 도는 경로이며 실행된 적이 없다 · 물안개 지속 VFX 미제작 · 사용 버튼 아이콘 미제작 · 밸런싱 수치 미확정.** 구현 계약에 규칙 8-1-a(활성 물안개 위상 정렬)가 신설되었다 — `GameSystemRules/GameSystemRules_Buildings.md`. |
| 0.43.1 | 2026-08-10 | 건물 타입 주석 오류 정정 — `AutoTower`×3종족의 Transcendence를 `MistShrine`으로 적어 온 것을 **`VineTower`**로 수정했다. MistShrine은 방어 타워가 아니라 공격하지 않는 별도 힐 건물이며 `AutoTower`(= 2)와 완전히 별개인 `HealShrine`(= 6) enum 값을 쓴다는 점을 주석에 명문화. 당시 MistShrine 물안개 지속 힐은 **기획 확정 / 구현 미착수** 상태였다. 아키텍처·구조 변경 없이 표기 정정만 수행. 규칙: `GameSystemRules/GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템. |
| 0.43.0 | 2026-07-20 | `MapTestModeEnabled`를 초기 골드 전용 설정으로 확정. 정상 모드는 광산 수 표, 테스트 모드는 `TestStartingGold=5000`을 사용하며 멀티플레이에서는 Host 표식·실제 골드가 권위값이다. 표식(0/1)과 실제 골드를 canonical bytes·SHA-256·로그에 포함하고 NewMap 준비에도 동일 권한 경계를 적용하도록 명문화. |
| 0.42.0 | 2026-07-19 | reliable map transfer의 10초 timeout, incomplete 한정 동일 nonce/package 전체 1회 재전송과 두 번째 실패 종료, version/size/SHA/deserialize/semantic/disconnect 즉시 실패를 확정. 최초·NewMap·싱글 실패 복구 UI/state, idempotent Retry와 새 seed 재준비, 내부 정보 비공개를 명문화. |
| 0.41.0 | 2026-07-19 | canonical 180 orbit 기반 중립 광산 균등 sampling, 유형별 zone/lane 제약, spacing filter 없음과 repair 없는 attempt reject 확정. castle-neighbor→mine-neighbor multi-source BFS access metric, center/pair 교차·HexCoord 거리, mine 전후 corridor 폭과 start-to-end continuity validator를 명문화. |
| 0.40.0 | 2026-07-19 | ObstacleOpen/Canyon/Outer/ThreeLane archetype의 행·열 단위 생성 알고리즘과 균등 선택, exact projection, NoBuild/Blocked profile, mine lane 제약, 실패 시 repair 없는 attempt reject를 확정. |
| 0.39.0 | 2026-07-19 | GridInteractionUseCase 입력 우선순위와 Blocked/NoBuild UX 확정. building action·MineKind MiningPost를 우선하고, Blocked는 deselect+패널 닫기/무토스트·무선택 이벤트, 소유 NoBuild는 선택+BuildingNotAllowed 토스트, 중립·적 NoBuild는 선택만 수행하도록 명문화. |
| 0.38.0 | 2026-07-19 | 무작위 맵 범위의 형식 marker를 임시 MapVersion(int, 초기값 1)으로 확정. unknown map format deserialize 차단과 Host/Client mismatch 시 map prep 실패만 담당하며 matchmaking/app update/global connection compatibility는 별도 GameProtocolVersion 중요 작업 책임으로 분리. |
| 0.37.0 | 2026-07-19 | 전역 GameProtocolVersion/build compatibility 관리를 무작위 맵 범위에서 분리. matchmaking same-version filter, custom lobby pre-Relay 검사, NGO approval/rejection, reconnect 검증, update-required UX를 별도 중요 작업으로 이관. |
| 0.36.0 | 2026-07-19 | NoBuild renderer를 standard mesh/height/owner base color + 반투명 짙은 회색 diagonal hatch 3개 overlay로 확정하고 selection 병행 표시를 명문화. Blocked는 domain/231 MapDefinition/hash/대칭 검증에는 유지하되 HexGridRenderer가 standard tile mesh/collider를 만들지 않는 빈 공간으로 표시하며 배경 collider 경유 선택을 차단하고 obstacle asset은 presentation만 대체하도록 확정. |
| 0.35.0 | 2026-07-19 | 최초 archetype/fallback은 빈 `DecorationDefinition` 목록과 decoration draw 0회를 사용하도록 확정. MapDefinition/canonical codec/hash/builder/validator는 위치와 type/materialVariant/scaleStep/rotationStep 정수 ID 스키마 및 exact 180° 대응을 선지원하고, 에셋·테마 확정 뒤 독립 Decoration 스트림으로 추가해 Terrain/MinePlacement를 불변으로 유지하도록 명문화. |
| 0.34.0 | 2026-07-19 | generation/validation 초기 실행 모델을 loading UI 표시→1 frame yield→main-thread synchronous 처리→전송으로 확정. generator/validator는 Unity API 없는 pure C#, Unity object/render 적용은 Game scene main thread로 제한하고 생성 시간·attempt count를 기록하며 profiling에서 hitch 확인 시에만 background 이전을 검토하도록 명문화. |
| 0.33.0 | 2026-07-19 | 최초 경기/NewMap 공용 persistent `NetworkMapTransfer` 프로토콜 확정. Begin/Chunk/Ready 메시지, 1KB chunk·64KB payload 한도, 중복 무시·순서 독립 재조립·stale nonce 무시, 완전 조립 후 hash/deserialize/semantic 검증 및 성공 ACK 전 씬 전환 금지를 명문화. |
| 0.32.0 | 2026-07-19 | `MapSelection`에서 맵 유형·허용 중립 광산 수·시작 광산 방향(A/B 50:50)을 최초 1회 선택해 attempt/fallback까지 고정하고, 초기 골드는 count에서 파생하며 retry는 지형 세부·중립 광산 위치·장식만 변경하도록 확정. fallback A/B는 시작 광산 좌우 대응 변환으로 지원. NewMap은 매치 설정을 유지하고 새 seed로 맵 값만 재선택하며 이전 canonical hash와 같은 후보를 거부하도록 명문화. |
| 0.31.0 | 2026-07-19 | `SymmetricMapBuilder`를 11×21 맵의 단일 생성 변경 경계로 확정. `SetPair(c,r)`→`(10-c,20-r)` 동시 적용, `SetCenter(5,10)` 전용, 팀/장식 rotation 자동 대응, archetype raw 수정 금지와 독립 MapDefinitionValidator exact symmetry 재검증 원칙 명문화. |
| 0.30.0 | 2026-07-19 | 무작위 맵 fallback을 25개 완성 바이너리/에셋 대신 유형별 deterministic base template 5개 + 허용 광산 수별 prevalidated symmetric mine slots 조합으로 확정. PRNG draw 없음, 고정 장식/무장식, 일반 MapDefinitionValidator 전체 검증, 코드/순수 정의 기반 schema migration 불필요 원칙 명문화. |
| 0.29.0 | 2026-07-19 | `MapDefinition` 타일별 `InitialOwner` 제거 확정. 성/팀 시작 광산 위치와 공용 인접 6타일 확장 규칙을 단일 입력으로 사용하고, 런타임 구성·validator가 순수 `InitialMapStateEvaluator`를 재사용하며 점유 건물 타일 제외 후 팀별 즉시 일반 건설 가능 고유 타일 10개를 검증하도록 명문화. |
| 0.28.0 | 2026-07-19 | 재경기 `RematchMapMode`(`SameMap`/`NewMap`)와 MapDefinition 생명주기 확정. 기존 요청/수락/거절·Game 씬 재로드 유지, 다른 mode 동시 요청 시 서버 선접수 제안 우선, SameMap 재사용, NewMap 검증 후 원자 교체, 실패 시 기존 결과/정의 유지, 싱글 선택 및 로비/연결 종료 시 컨텍스트 폐기 명문화. |
| 0.27.0 | 2026-07-19 | `MapDefinition` 권위 전송 형식을 canonical binary + SHA-256으로 확정. 고정 필드 순서·고정폭 정수·명시 byte order, 231 row-major 타일과 성/광산/장식 정규 정렬, string/float/hash 필드 제외, package 구조 및 Client 길이/버전→SHA→deserialize→semantic validator→ACK 순서, JSON 진단 전용 원칙 명문화. |
| 0.26.0 | 2026-07-19 | 무작위 맵 생성의 64-bit root seed와 프로젝트 전용 고정 정수 PRNG 계약 확정. MapSelection/Terrain/MinePlacement/Decoration 및 Attempt-0~99 독립 스트림 파생, 서브시스템·재시도 draw 격리, schema/version+seed 재현성과 Host 최종 MapDefinition 권위 원칙 명문화. |
| 0.25.0 | 2026-07-19 | 무작위 맵용 `HexTile` 상태를 `TerrainKind`/`BuildRule`/`MineKind`/`HasBuilding`으로 분리하고 `IsWalkable`을 setter 없는 계산 결과로 전환하는 설계 확정. 일반 건설·MiningPost·점령 조건과 건물 철거 시 영구 지형/광산 보존 원칙 명문화. |
| 0.24.0 | 2026-07-19 | 무작위 맵 `MapDefinition` 정규 데이터 계약 확정. FlatTop 11×21의 231개 타일 row-major 완성본, 메타데이터·타일 상태·성/광산 배치·정수 장식 식별자·최종 해시를 정의하고, float 해시 배제·hash 필드 제외 정규 직렬화·성/광산 단일 소스 원칙을 명문화. |
| 0.23.0 | 2026-07-19 | FlatTop 11×21 무작위 맵의 멀티플레이 시작 동기화 설계 확정. 로비 로딩 상태에서 Host가 생성·검증·재시도·폴백의 유일한 권위자로 최종 맵 전체를 전달하고, 양측 데이터 해시가 같을 때만 전투 씬으로 이동한다. 실패 시 기존 로비를 유지하며, 전투 씬에서는 확정 데이터를 구성한 뒤 양측 로드 완료 후 시뮬레이션을 시작한다. |
| 0.22.0 | 2026-07-17 | 도끼병(BattleAxe) 휩쓸기형 AoE 구현 — 특수 유닛 5종 중 첫 구현. 특수 공격 전략 핸들러 구조(`ISpecialAttackBehavior` + `SpecialAttackContext` + `SpecialAttackRegistry` + `SweepAttackBehavior`, `Scripts/Application/Combat/`) 신설, `UnitCombatUseCase.ExecuteAttack` 피해 로직을 `ApplyDamageToVictim` 헬퍼로 추출 후 특수 공격 훅 1줄 추가. 휩쓸기 판정 = 월드 좌표 전방 부채꼴(reach/arc), 튜닝 SO `SpecialAttackConfig`(Infrastructure/Config) 신규. "특수 공격 전략 핸들러 구조" 서브섹션 추가. `GameSystemRules_Units.md` 규칙 23~27 등재. BattleAxe attackRange 0.5→0.75, 타격 1.1667s(클립 OnAttackHit 주입). |
| 0.21.0 | 2026-07-13 | 유닛 애니메이션 상태 동기화를 엣지 트리거 RPC(`StartWalkAnimationClientRpc` 등)에서 `NetworkUnit`의 NetworkVariable(`UnitAnimState` None/Walk/Attack) **레벨 동기화**로 전환. 클라 스폰 시 현재 값 자동 적용으로 스폰 레이스 유실 소멸, `UnitView.Initialize` 후 `ReapplyAnimStateToView` 멱등 재적용 봉합, 재경로 첫 스텝 역방향은 `AlignPathStartToTransform`로 보정. "유닛 애니메이션 상태 동기화" 서브섹션 신규 추가. 규칙 U-22 등재(규칙 21 상위 대체). |
| 0.20.0 | 2026-06-26 | `IUnitFactory` 인터페이스 도입(Bootstrap/Infrastructure 역방향 의존 제거 리팩토링). `IGameServices.GetUnitFactory()` 반환 타입을 구체 클래스 `UnitFactory`(Infrastructure) → `IUnitFactory`(Application) 인터페이스로 변경. "의존성 방향 추상화(Application 인터페이스 패턴)" 섹션 신규 추가(IGameServices/IUnitFactory/IEntityPositionProvider/IForfeitService 정리). 동작 변경 없음. |
| 0.19.0 | 2026-06-10 | AI 시나리오 ScriptableObject 3종족 개편. Domain/AI 레이어 신규(DifficultyLevel, BuildOrderStep, AIActionType). 종족별 단일 에셋 구조. |
| 0.18.0 | 2026-06-05 | Firebase 백엔드 전환 완료 반영 (Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료, PlayFab → Firebase/Firestore 구조로 교체). 폴더 구조 Bootstrap/Diagnostics 추가. ScriptableObject 기반 UnitStats/BuildingStats 반영. BuildingType 26종 확장 반영. ProductionState PendingQueue 구조 반영. OnEntityDied → OnUnitDied+OnBuildingDied 강타입 분리 반영. |
| 0.17.0 | 2026-03-19 | 유닛 스탯 코드 예시 재확정 (ATK: Pistoleer=6, Assault=1, Sniper=10 / 사거리: 1.0/2.0/5.0 / epsilon 0.05f 명시 / Sniper MoveSpeed=0.25). Castle HP 50→100. A* 목표 타일 blocked 체크 제거 반영. UIAnimator/AnimatedPanel 패턴 섹션 추가. 개발 로드맵 섹션 삭제 (ROADMAP.md 참조로 대체). |
| 0.16.0 | 2026-03-19 | 카메라 줌 DOTween 보간 완료 (CameraController _targetZoom + DOTween.To Ease.OutCubic, _zoomDuration SerializeField). |
| 0.15.0 | 2026-03-17~18 | 전역 로딩 스크린 (LoadingScreen.cs 싱글턴, Show/Hide DOFade, sceneLoaded 자동 Hide). 재경기 시스템 (RematchRequestPopup, RequestRematchServerRpc, NGO LoadScene 재로드). 멀티플레이 로비 복귀 버그 수정 (로컬 독립 처리, WaitForSecondsRealtime 기반 30초 카운트다운). |
| 0.14.0 | 2026-03-14~16 | 팀별 프리팹 코드 연동 (UnitFactory/BuildingFactory 팀+타입별 분기). Assault/Sniper UnitType enum + UnitStats + UnitProductionStats. 공격 애니메이션-타격 동기화 (AnimationEventRelay + Animation Event + scale punch). 유닛 메시 방향 보정 (하위 Mesh Y=30°). 유닛 회전 DOTween 보간 (ApplyDirection/PlayAttackAnimation). 랜덤매칭 씬 전환 버그 수정 (GetStableHash polynomial hash). |
| 0.13.0 | 2026-03-09 | GameConfig AnimationFps 필드 제거 (미사용), Walk 애니메이션 연속 재생 수정 (매 스텝 0f 리셋 → 이미 Walk 상태이면 리셋 안 함), UnitStats HP 50으로 현행화, SetDependencyReferences 시그니처 현행화 (animData 제거, unitFactory/buildingFactory 추가), T키 자동이동 섹션 제거 (기능 삭제됨), 랠리마커 sortingOrder 제거 (3D Z-buffer 전환 완료) |
| 0.12.0 | 2026-03-07 | 3D 전환 반영: Netcode 버전 2.9.2, 애니메이션 Animator(Mecanim) 기반(Walk/Attack/Dead), sortingOrder 폐기→Z-buffer 렌더링, TileHeight 0.866 통일, ViewConverter 시스템 문서화, 비주얼/카메라 스타일 3D 이소메트릭 반영 |
| 0.11.0 | 2026-02-20 | HUD 타일 카운트: GameHudUI에 블루/레드 팀 보유 타일 수 표시 추가(_blueTileCountText/_redTileCountText), PopulationUseCase.GetMaxPopulation() 활용. 게임 종료 UI 버그 수정: GameEndUI를 Awake() 자체 구독→Initialize() 패턴으로 변경(비활성 패널에서 Awake 미호출 문제 해결), GameBootstrapper.LoadMap()에서 Initialize() 호출, 재시작 시 구독 정리/재구독 처리 |
| 0.10.0 | 2026-02-15 | 공성 시스템: ProductionTicker 공성 흐름(랠리→Castle→siege 전진), UnitView.OnMoveComplete 콜백, 공성 목록 관리(등록/제거), TickSiege 1초 간격 전진 체크. PopupClosedFrame 패턴: BuildingPlacementUI/ProductionPanelUI ClosedFrame으로 팝업 닫힘 같은 프레임 클릭 통과 방지 |
| 0.9.0 | 2026-02-15 | 랠리포인트 시스템 개선: 마커 표시(3초 자동 숨김 + 팝업 연동), RallyPointChangedEvent 이벤트 추가, BFS 빈 타일 탐색(maxRange=3), 배럭 타일 설정→해제, ProductionTicker 마커 관리, ProductionPanelUI 마커 표시/숨김 연동, GameConfig.RallyPointPrefab 추가, 팝업 설정 후 자동 닫힘 |
| 0.8.1 | 2026-02-14 | Per-step 타일 가용성 체크 추가: UnitMovementUseCase.IsTileBlockedBySameTeam() 메서드 추가, MoveAlongPath 각 스텝 시작 전 같은 팀 차단 검증, 차단 시 현재 위치→최종 목적지 재탐색(RequestMove), 재탐색 실패 시 이동 중단. 전투 흐름 다이어그램에 per-step 체크 단계 추가 |
| 0.8.0 | 2026-02-14 | 유닛 이동/전투 시스템 개선: ClaimedTile(같은 팀 이동 중 타일 선점, 적 팀 투과), 이동 중 거리 기반 전투(Lerp 중 매 프레임 사거리 체크), 타일 중앙 도착=전투 승리=점령 규칙 확립, UnitData.ClaimedTile 필드 추가, UnitMovementUseCase 차단 목록에 같은 팀 ClaimedTile 포함, UnitView.MoveAlongPath Claim 설정/해제 및 Lerp 중 전투 |
| 0.7.0 | 2026-02-14 | 유닛 생산 시스템: UnitProductionUseCase/ResourceUseCase/PopulationUseCase 추가, ProductionState/UnitProductionStats(Domain), ProductionTicker/ProductionPanelUI(Presentation), GameConfig 경제 설정, UnitFactory 런타임 의존성 주입(SetDependencyReferences), 영토 확장(건물 건설 시 인접 타일 점령), 경로탐색 아군/적군 무관 차단, 유닛 스폰 점유 검증, 생산 이벤트 4종 추가 |
| 0.6.0 | 2026-02-13 | 전투 시스템 고도화: IDamageable 인터페이스 도입(유닛/건물 통합 전투), BuildingStats/UnitStats 중앙 스탯 관리, 이벤트 일반화(EntityAttacked/EntityDied), 경로탐색 적 유닛 우회(blockedCoords), 이동 중 전투(매 타일 공격 체크 + 전투 후 이동 계속), 사망 엔티티 데이터 정리(Dictionary 제거 + 타일 복구), T키 자동/수동 이동 토글(양팀 Castle 방향 자동 이동) |
| 0.5.0 | 2026-02-08 | 건물 배치 시스템(MVP Phase 1) 추가: BuildingType/BuildingData, 배치 흐름(자동/수동), 정렬 순서(건물 50), BuildingPlacedEvent |
| 0.4.0 | 2026-02-08 | 타일 선택 하이라이트 버그 수정 문서화: HexTileView 토글→결정적 할당, 선택 해제 이벤트 처리 설명 추가 |
| 0.3.0 | 2026-02-08 | 듀얼 Orientation: OrientationConfig, PointyTop(7×17)/FlatTop(10×29), 런타임 맵 전환(LoadMap), HexCoord/A* 코드 현행화 |
| 0.2.0 | 2026-02-07 | 전투 시스템 추가: UnitData 전투 스탯, UnitCombatUseCase 전투 흐름, 이벤트 기반 통신 (Attack/Died) |
| 0.1.0 | 2026-01-27 | 초기 문서 작성 |

---

**문서 끝**
