# Plan — NetworkObject 클라이언트 무단 Destroy 버그 수정

## 이 Plan이 무엇인지

유닛이 사망할 때 클라이언트가 NetworkObject에 직접 `Destroy(gameObject)`를 호출해서 NGO 에러가 발생하는 문제, 그리고 이후에 발생한 두 가지 부작용(클라이언트 GO 미파괴, 모바일 이펙트 미재생)을 올바르게 수정합니다.

**이전 수정 접근의 문제:**
- 1차 수정: 서버 `Destroy()` → `Despawn(true)` 교체 → 클라이언트 GO가 오히려 안 사라짐
- 2차 수정: 서버 `Destroy()` 복원, 클라이언트 return → OnNetworkDespawn이 게임 중 발생하지 않음 (씬 언로드 시에만 발생)
- 런타임 로그로 확인: 서버의 `Destroy(gameObject)`가 NGO 클라이언트 전파를 일으키지 않고 있음

**근본 원인:**
서버의 `Destroy(gameObject)` 호출이 NGO의 Despawn 메시지를 클라이언트에 전파하지 않는다.
NGO에서 클라이언트에 GO 파괴를 전파하려면 반드시 **서버에서 `NetworkObject.Despawn(destroy: true)`를 명시적으로 호출**해야 한다.

**아키텍처 규칙 준수:**
- game-programmer MEMORY.md: `NetworkBehaviour: Infrastructure 레이어에만 (Presentation/Application 금지)`
- 현재 UnitView.cs(Presentation)에 있는 `Unity.Netcode` API 참조는 규칙 위반
- 수정 후 UnitView에서 `Unity.Netcode` 참조를 완전히 제거한다

**GameSystemRules 근거:** NGO 공식 제약 (서버/호스트만 Despawn 가능) + game-programmer MEMORY.md 레이어 제약.

---

## [수정 1] NetworkCombatController.cs — 서버 Despawn 처리 (Infrastructure)

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
**위치**: `OnUnitDied()` 서버 핸들러 (IsServer 분기 내부)

### 수정 내용

서버에서 `EntityDiedClientRpc` 발행 직후, 해당 유닛의 `NetworkObject.Despawn(destroy: true)`를 명시적으로 호출한다.

이렇게 하면:
- NGO가 모든 클라이언트에 GO 파괴 메시지를 보냄
- 클라이언트의 `NetworkUnit.OnNetworkDespawn()` 호출 → 이펙트 재생 → GO 파괴
- Infrastructure 레이어 안에서만 NGO API 사용 → 레이어 규칙 준수

**추가 필요 사항**: `OnUnitDied()` 핸들러에서 유닛 GO에 접근하기 위해 `UnitFactory`(또는 `UnitSpawnUseCase`) 참조가 필요하다. NetworkCombatController에 이미 주입되어 있는지 확인하고, 없으면 GameBootstrapper에서 주입한다.

**변경 전**:
```csharp
private void OnUnitDied(UnitDiedEvent e)
{
    if (!IsServer) return;
    // ...
    EntityDiedClientRpc(unitId, true);
}
```

**변경 후**:
```csharp
private void OnUnitDied(UnitDiedEvent e)
{
    if (!IsServer) return;
    // ...
    EntityDiedClientRpc(unitId, true);

    // NGO를 통해 클라이언트에 GO 파괴를 전파한다.
    // Destroy(gameObject) 방식은 NGO 전파가 보장되지 않으므로 Despawn을 명시적으로 호출한다.
    // OnNetworkDespawn이 모든 클라이언트에서 호출되어 이펙트 재생 + GO 파괴가 보장된다.
    var unitGo = _unitFactory.GetUnitGameObject(e.Unit.Id);
    if (unitGo != null)
    {
        var networkObj = unitGo.GetComponent<NetworkObject>();
        if (networkObj != null && networkObj.IsSpawned)
            networkObj.Despawn(true);
    }
}
```

---

## [수정 2] UnitView.cs — NGO API 제거 및 서버 분기 정리 (Presentation)

**파일**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
**위치**: `SetDependencies()` 내 `GameEvents.OnUnitDied` 구독자

### 수정 내용

현재 코드에 있는 `Unity.Netcode.NetworkObject`, `Unity.Netcode.NetworkManager` 직접 참조를 **완전히 제거**한다.

대신 `NetworkContext.IsNetworkActive` / `NetworkContext.IsNetworkServer`(또는 동등한 홀더)만 사용하여 분기한다.

| 환경 | 이펙트 | GO 파괴 |
|------|--------|---------|
| 싱글플레이 | UnitView에서 재생 | `Destroy(gameObject)` |
| 멀티 서버 | UnitView에서 재생 | **하지 않음** (NetworkCombatController가 Despawn 담당) |
| 멀티 클라이언트 | **하지 않음** (OnNetworkDespawn에서 재생) | **하지 않음** (NGO가 파괴) |

**변경 전 (현재 코드 — 레이어 규칙 위반)**:
```csharp
var netObjForEffect = GetComponent<Unity.Netcode.NetworkObject>();
bool isNetworkClient = netObjForEffect != null
    && netObjForEffect.IsSpawned
    && !Unity.Netcode.NetworkManager.Singleton.IsServer;

if (!isNetworkClient)
    EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);

if (isNetworkClient)
    return;

Destroy(gameObject);
```

**변경 후 (레이어 규칙 준수 — Unity.Netcode 참조 없음)**:
```csharp
// 멀티플레이 클라이언트: 이펙트 재생과 GO 파괴 모두 NetworkUnit.OnNetworkDespawn에서 처리.
if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
    return;

// 서버 또는 싱글플레이: 이펙트 재생.
EffectManager.Instance?.PlayUnitDeath(_unitData.Type, transform.position);

// 싱글플레이만: GO 직접 파괴. 멀티 서버는 NetworkCombatController가 Despawn으로 처리.
if (!NetworkContext.IsNetworkActive)
    Destroy(gameObject);
```

---

## [수정 3] NetworkUnit.cs — OnNetworkDespawn 이펙트 (Infrastructure) — 이미 구현됨

**파일**: `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
**위치**: `OnNetworkDespawn()` 메서드

클라이언트에서 NGO Despawn이 전파되면 `OnNetworkDespawn()`이 호출된다.
이 시점에 이펙트를 재생하면 타이밍 문제 없이 항상 이펙트가 발동된다.

현재 코드에 이미 구현되어 있으나, **임시 DeathDiag 로그 코드는 수정 완료 후 제거한다.**

---

## [수정 4] UnitFactory.cs — GetUnitGameObject 메서드 추가 (Infrastructure)

**파일**: `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`

NetworkCombatController에서 유닛 ID로 GO를 조회하려면 UnitFactory에 접근 메서드가 필요하다.
이미 `_unitObjects` 딕셔너리를 보유하고 있으므로 간단한 조회 메서드를 추가한다.

```csharp
/// <summary>
/// 유닛 ID로 해당 유닛의 GameObject를 반환한다.
/// NetworkCombatController에서 NGO Despawn 호출 시 GO 참조에 사용.
/// </summary>
public GameObject GetUnitGameObject(int unitId)
{
    _unitObjects.TryGetValue(unitId, out var go);
    return go;
}
```

---

## 진단 코드 제거 (수정 완료 후)

아래 임시 DeathDiag/EffectDiag 코드는 수정 및 테스트 통과 후 모두 제거한다.

| 파일 | 제거 대상 |
|------|----------|
| `UnitView.cs` | `DeathLog` 정의 + 호출부, `_deathDiagPath` 필드, `EffectDiag` 관련 코드 |
| `NetworkUnit.cs` | `DeathLog` 정의 + 호출부, `_deathDiagPath` 필드 |
| `NetworkCombatController.cs` | `DeathLog` 정의 + 호출부, `_deathDiagPath` 필드 |
| `EffectManager.cs` | `DiagLog`, `DeathLog` 정의 + 호출부, 관련 path 필드, `Awake()` 내 초기화 코드 |

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| NetworkCombatController에 UnitFactory 참조 없음 | GameBootstrapper에서 주입 여부 확인, 없으면 추가 |
| Despawn 시점에 이미 Despawn된 GO 참조 | `networkObj != null && networkObj.IsSpawned` 가드로 처리 |
| 서버 UnitView.OnUnitDied와 Despawn 중복 | 서버 UnitView는 이펙트만 재생, Destroy 호출 없음 → 중복 없음 |
| 싱글플레이 회귀 | `NetworkContext.IsNetworkActive` false일 때 기존 경로 유지 |
| NetworkContext.IsNetworkServer 홀더 미존재 | 존재 여부 확인 후 없으면 `NetworkManager.Singleton?.IsServer` 래핑 |

---

## [추가] 런타임 로그 — 테스트 결과 분석용

수정 사항이 올바르게 동작하는지 확인하기 위해 각 핵심 경로에 파일 기록 로그를 추가한다.

### 로그 파일 경로
```
Assets/_Project/Docs/_Logs/2026-06-08/02_27_networkobject-invalid-destroy/RuntimeLog.txt
```

### 로그 작성 규칙
- `System.IO.File.AppendAllText`로 자동 기록 (수동 복사 붙여넣기 불필요)
- `UnityEngine.Application.dataPath`로 경로 구성 (`Application.dataPath`는 네임스페이스 충돌 위험)
- 타임스탬프 형식: `[HH:mm:ss.fff]`
- 앱 시작 시 `File.WriteAllText`로 파일 초기화 (이전 로그 덮어쓰기)

### 로그 추가 위치 및 내용

#### NetworkCombatController.cs (서버)
`OnUnitDied()` 핸들러 내부:
```
[서버] OnUnitDied 수신 — unitId={id}, type={type}
[서버] EntityDiedClientRpc 발행 완료
[서버] Despawn 시도 — GO 조회 결과: {found/null}
[서버] Despawn 호출 완료 — unitId={id}
```
또는 GO가 없을 경우:
```
[서버] Despawn 실패 — GO 없음 또는 IsSpawned=false, unitId={id}
```

#### NetworkUnit.cs (클라이언트)
`OnNetworkDespawn()` 내부:
```
[클라이언트] OnNetworkDespawn 호출 — IsServer={bool}
[클라이언트] 이펙트 재생 시도 — type={type}, EffectManager={null/있음}
[클라이언트] 이펙트 재생 완료
```

#### UnitView.cs (서버/싱글)
`OnUnitDied` 구독자 내부:
```
[UnitView] OnUnitDied 수신 — type={type}, IsNetworkActive={bool}, IsNetworkServer={bool}
[UnitView] 클라이언트 경로 — return (OnNetworkDespawn 대기)
[UnitView] 서버/싱글 경로 — 이펙트 재생
[UnitView] 싱글플레이 — Destroy 호출
```

### 테스트 후 제거 대상
런타임 로그 코드는 테스트 통과 후 전부 제거한다. 대상:
- `NetworkCombatController.cs` — `TestLog` 정의 + 호출부, path 필드, 초기화 코드
- `NetworkUnit.cs` — `TestLog` 정의 + 호출부, path 필드
- `UnitView.cs` — `TestLog` 정의 + 호출부, path 필드, 초기화 코드

---

## 구현 순서

```
[1] NetworkCombatController.cs — OnUnitDied 서버 핸들러에 TestLog 추가
[2] NetworkUnit.cs — OnNetworkDespawn에 TestLog 추가
[3] UnitView.cs — OnUnitDied 구독자에 TestLog 추가
```
