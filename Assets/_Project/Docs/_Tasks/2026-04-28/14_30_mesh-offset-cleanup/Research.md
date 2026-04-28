# Research — Mesh Y Offset 제거 및 통일

## 작업 목적 (자연어 설명)

현재 일부 유닛 프리팹의 메시 자식 오브젝트가 Y축으로 30도 회전된 상태로 설정되어 있고,
이를 보정하기 위해 `UnitView` 스크립트에 `_meshYOffset` 변수가 존재한다.

이 작업은 모든 유닛의 메시 자식 회전값을 0으로 통일하고,
애니메이션의 Root Transform Rotation 오프셋을 각 유닛별로 직접 테스트하여 방향을 맞춘 뒤,
더 이상 필요 없어진 `_meshYOffset` 변수를 코드에서 제거하는 것이 목적이다.

이미 Human 유닛 3종(Assault, Pistoleer, Sniper)에서 이 과정이 완료되어 정상 동작을 확인하였으므로,
동일한 방식을 나머지 6종 유닛에 적용하는 작업이다.

---

## 현재 상태

### `_meshYOffset` 사용 위치 (코드)

파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- **라인 84**: `[SerializeField] private float _meshYOffset = 30f;` — 기본값 30
- **라인 427**: `return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - _meshYOffset;`
  - 공격 방향 계산(`CalculateAttackAngle`)에서만 사용
  - 이동 방향(`ApplyDirection`, 라인 407)에는 사용되지 않음

### 프리팹별 현재 `_meshYOffset` 값

| 유닛 | _meshYOffset | 작업 필요 여부 |
|------|-------------|--------------|
| Assault Blue/Red | **0** | 완료 (이미 적용됨) |
| Pistoleer Blue/Red | **0** | 완료 (이미 적용됨) |
| Sniper Blue/Red | **0** | 완료 (이미 적용됨) |
| BearGuard Blue/Red | **30** | 작업 필요 |
| FoxMagician Blue/Red | **30** | 작업 필요 |
| LionKnight Blue/Red | **30** | 작업 필요 |
| EmberSpirit Blue/Red | **30** | 작업 필요 |
| FlameSpirit Blue/Red | **30** | 작업 필요 |
| InfernoSpirit Blue/Red | **30** | 작업 필요 |

### 완료 검증 근거

Assault, Pistoleer, Sniper에서 아래 절차를 적용하여 정상 동작 확인:
1. 프리팹 내 메시 자식 오브젝트의 `Transform.localEulerAngles.y` → 0으로 설정
2. `_meshYOffset` Inspector 값 → 0으로 설정
3. 애니메이션 Root Transform Rotation 오프셋 → 직접 테스트 후 값 조정

### 주의 사항

- `_meshYOffset`은 **공격 방향 계산에만** 사용되며, **이동 방향에는 영향 없음**
- 이동 중 방향이 애니메이션 오프셋 조정에 영향을 받는지는 각 유닛별 테스트로 확인 필요
- UnitView 컴포넌트가 없는 미완성 유닛(EagleArcher, MushroomBomber 등)은 이 작업 범위에 포함되지 않음
