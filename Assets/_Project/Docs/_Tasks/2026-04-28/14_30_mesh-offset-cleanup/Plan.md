# Plan — Mesh Y Offset 제거 및 통일

## 작업 목적 (자연어 설명)

6종 유닛 프리팹(BearGuard, FoxMagician, LionKnight, EmberSpirit, FlameSpirit, InfernoSpirit)의
메시 자식 오브젝트 회전값과 UnitView `_meshYOffset`을 모두 0으로 설정하고,
이동 애니메이션(Idle, Walk)의 Root Transform Rotation 오프셋을 0으로 통일한다.
이에 맞춰 코드의 `DirectionAngles` 값을 -30씩 조정하고 `_meshYOffset` 변수를 제거한다.
공격 애니메이션 오프셋은 유닛별로 직접 테스트하여 별도 설정한다.

---

## 설계 근거

**이동 방향** — `DirectionAngles`와 애니메이션 offset의 관계:

| | DirectionAngles[NE] | 이동 anim offset | 시각 결과 |
|---|---|---|---|
| 기존 방식 | 30° | -30° | 0° |
| 변경 방식 | 0° | 0° | 0° |

결과 동일 → `DirectionAngles` -30 변경 + 이동 anim offset 0으로 통일 가능.

**공격 방향** — `CalculateAttackAngle()`은 `DirectionAngles`를 사용하지 않고 Atan2를 직접 계산.
이동 방향 변경의 영향 없음 → 공격 anim offset은 유닛별 독립 설정.

**Red 클라이언트 +180° 보정** — NetworkUnit.LateUpdate()의 결과 동일:
- 기존: (30 + (-30)) + 180° = 180°
- 변경: (0 + 0) + 180° = 180°

---

## 작업 순서

### Phase 1 — DirectionAngles 코드 수정 (완료)

파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

```csharp
// 변경 전
private static readonly float[] DirectionAngles = { 30f, 90f, 150f, 210f, 270f, 330f };

// 변경 후 (최종 수정값 — 초기 계획의 -30° 방향 오류를 +30°로 수정)
private static readonly float[] DirectionAngles = { 60f, 120f, 180f, 240f, 300f, 0f };
```

주석도 함께 수정:
```csharp
// NE(0)=60, E(1)=120, SE(2)=180, SW(3)=240, W(4)=300, NW(5)=0
```

> ⚠️ **주의**: 초기 Plan에서 -30° 조정({0,60,...,300})으로 기재했으나,
> FlatTop 월드 좌표 분석 결과 올바른 값은 기존 DirectionAngles + 30° = {60,120,...,0}임.
> 근거: FlatTop NW 방향(Q=0, R-1)의 실제 Unity 월드 각도 = atan2(0, +1) = 0°

---

### Phase 2 — 유닛별 프리팹 + 애니메이션 조정 (사용자 직접 진행)

각 유닛에 대해 아래 순서로 반복:

**1. 프리팹 Inspector 수정**
- 메시 자식 오브젝트 선택 → `Transform.localEulerAngles.y` → `0` 설정
- `UnitView` 컴포넌트 → `_meshYOffset` → `0` 설정
- Blue / Red 프리팹 각각 적용

**2. 이동 애니메이션 오프셋 수정**
- Idle, Walk 애니메이션 → Root Transform Rotation Offset → `0` 설정

**3. 공격 애니메이션 오프셋 수정**
- 유닛을 씬에 배치하고 공격 방향 직접 테스트
- 공격 시 방향이 어긋나면 해당 유닛 공격 애니메이션의 Root Transform Rotation 오프셋 개별 조정

**4. 작업 대상 유닛 목록**

| 유닛 | 프리팹 경로 | 상태 |
|------|-----------|------|
| BearGuard Blue | `Prefabs/Units/Transcendence/Unit_BearGuard_Blue.prefab` | [x] |
| BearGuard Red | `Prefabs/Units/Transcendence/Unit_BearGuard_Red.prefab` | [x] |
| FoxMagician Blue | `Prefabs/Units/Transcendence/Unit_FoxMagician_Blue.prefab` | [x] |
| FoxMagician Red | `Prefabs/Units/Transcendence/Unit_FoxMagician_Red.prefab` | [x] |
| LionKnight Blue | `Prefabs/Units/Transcendence/Unit_LionKnight_Blue.prefab` | [x] |
| LionKnight Red | `Prefabs/Units/Transcendence/Unit_LionKnight_Red.prefab` | [x] |
| EmberSpirit Blue | `Prefabs/Units/Spirit/Unit_EmberSpirit_Blue.prefab` | [x] |
| EmberSpirit Red | `Prefabs/Units/Spirit/Unit_EmberSpirit_Red.prefab` | [x] |
| FlameSpirit Blue | `Prefabs/Units/Spirit/Unit_FlameSpirit_Blue.prefab` | [x] |
| FlameSpirit Red | `Prefabs/Units/Spirit/Unit_FlameSpirit_Red.prefab` | [x] |
| InfernoSpirit Blue | `Prefabs/Units/Spirit/Unit_InfernoSpirit_Blue.prefab` | [x] |
| InfernoSpirit Red | `Prefabs/Units/Spirit/Unit_InfernoSpirit_Red.prefab` | [x] |

---

### Phase 3 — 코드 수정 (완료)

파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- `_meshYOffset` SerializeField 변수 선언 제거
- `CalculateAttackAngle()` 반환값에서 `- _meshYOffset` 제거
- 관련 주석 업데이트

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| DirectionAngles 변경 후 기존 유닛 이동 방향이 달라 보이는 경우 | 이동 anim offset -30이 남아 있으면 두 배 보정됨 — Phase 2에서 offset 0으로 변경 필수 |
| 코드 변수 제거 후 기존 프리팹에 직렬화된 값이 남아 있는 경우 | Phase 2에서 모든 프리팹을 0으로 설정하고 저장하면 Unity가 필드 제거 시 자동으로 무시함 |
| Blue / Red 중 하나만 적용하고 누락되는 경우 | 체크리스트에 Blue/Red 분리하여 관리 |
