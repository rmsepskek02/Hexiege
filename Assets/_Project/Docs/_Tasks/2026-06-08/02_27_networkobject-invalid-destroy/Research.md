# Research — NetworkObject 클라이언트 무단 Destroy 버그

## 이 작업이 무엇인지

멀티플레이 중 유닛이 사망할 때, 클라이언트(비호스트) 쪽에서 아래 에러가 발생합니다.

> [Netcode] [Invalid Destroy][Unit_Pistoleer_Red_2][NetworkObjectId:13]  
> Destroy a spawned NetworkObject on a non-host client is not valid.  
> Call Destroy or Despawn on the server/host instead.

NGO(Netcode for GameObjects)에서 네트워크로 Spawn된 오브젝트는 반드시 **서버/호스트에서만** `Despawn()` 또는 `Destroy()`를 호출해야 합니다. 클라이언트가 직접 `Destroy(gameObject)`를 호출하면 이 에러가 발생합니다.

---

## 에러 스택 트레이스

```
[Netcode] [Invalid Destroy][Unit_Pistoleer_Red_2][NetworkObjectId:13]
Destroy a spawned NetworkObject on a non-host client is not valid.
Call Destroy or Despawn on the server/host instead.

UnityEngine.Debug:LogError (object)
Unity.Netcode.NetworkObject:OnDestroy ()
```

스택트레이스에 유저 코드가 없음 → NGO 내부에서 감지된 것이므로, 호출 위치를 코드에서 직접 추적해야 함.

---

## 버그 발생 흐름

```
[서버]
  1. UnitCombatUseCase.ExecuteAttack()
      → target.TakeDamage() → target.IsAlive = false
      → GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit))  ← Subject 동기 발행

  2. Subject 구독자 동시 실행:
      a. 서버의 UnitView 구독자
           → Destroy(gameObject) 호출 (서버는 허용됨)
           → NGO가 프레임 말에 클라이언트로 Despawn 전파 예약
      b. NetworkCombatController.OnUnitDied()
           → EntityDiedClientRpc(unitId, true) 전송 ← RPC 즉시 발송

[클라이언트]
  3. EntityDiedClientRpc 수신 (NGO Despawn 메시지보다 먼저 도착할 수 있음)
      → HandleUnitDied(unitId) 호출
      → unitSpawn.RemoveUnit(unitId)
      → GameEvents.OnUnitDied.OnNext(new UnitDiedEvent(unit))  ← 재발행

  4. 클라이언트의 UnitView 구독자 실행
      → Destroy(gameObject) 호출  ← ⚠️ NetworkObject가 아직 Spawned 상태
      → NGO.OnDestroy() 내부에서 에러 발생

  5. (이후 도착) NGO Despawn 메시지
      → OnNetworkDespawn() 호출
      → 이미 Destroy됐거나 상태 불일치
```

---

## 근본 원인

`UnitView.SetDependencies()` 내 `GameEvents.OnUnitDied` 구독자에서 **`Destroy(gameObject)`를 조건 없이 호출**하고 있습니다.

**파일**: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — 약 라인 453

```csharp
GameEvents.OnUnitDied
    .Subscribe(e =>
    {
        if (_unitData != null && e.Unit == _unitData)
        {
            // 애니메이션, VFX 처리 ...

            Destroy(gameObject);  // ← 클라이언트에서도 실행됨 → 에러
        }
    })
```

- **싱글플레이**: NetworkObject 없음 → `Destroy()` 문제없음
- **멀티플레이 서버**: 서버는 `Destroy()` 허용
- **멀티플레이 클라이언트**: NetworkObject가 Spawned 상태 → `Destroy()` 금지 → **에러**

---

## 관련 파일 및 라인

| 파일 | 라인 | 내용 |
|------|------|------|
| `UnitView.cs` | ~453 | `Destroy(gameObject)` 호출 — 수정 대상 |
| `NetworkCombatController.cs` | ~695 | 클라이언트에서 `GameEvents.OnUnitDied` 재발행 |
| `NetworkCombatController.cs` | ~515 | 서버에서 `EntityDiedClientRpc` 전송 |
| `NetworkUnit.cs` | 166-173 | `OnNetworkDespawn()` — 현재 역할 확인 필요 |

---

## 영향 범위

| 항목 | 영향 |
|------|------|
| 싱글플레이 | 영향 없음 — NetworkObject 없으므로 기존 경로 유지 |
| 멀티플레이 호스트 | 영향 없음 — 서버는 Destroy 허용 |
| 멀티플레이 클라이언트 | 에러 발생 — 유닛 사망 시마다 콘솔 에러 출력 |
