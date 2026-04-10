# Plan: 근접(Melee) 유닛 타입 추가 및 사거리 시스템 수정

**날짜**: 2026-04-10  
**작업**: UnitType.Melee 카테고리 추가 + 1타일 미만 근접 사거리 지원

---

## 1. 작업 범위 정의

### 포함
- `UnitType.Melee` 카테고리 enum 추가
- 근접 사거리(< 1.0 타일) 동작이 올바르게 동작하도록 판정 로직 수정
- 경로탐색 로직 수정 (근접 유닛이 적에게 접근 가능하도록)
- UnitFactory 프리팹 슬롯 추가 (3종족 × 2팀)
- 생산 UI에 근접 유닛 버튼 추가 (ProductionPanelUI)
- UnitStats에 Melee 타입 기본값 추가 (StatsReference.md 기입 전 플레이스홀더)

### 미포함 (별도 작업)
- StatsReference.md 구체적인 스탯 수치 기입 (사용자 직접 작성)
- Inspector 프리팹 연결 (에디터 스크립트로 별도 처리)
- 다중 히트 프레임 시스템 (FlameSpirit 6히트, LionKnight 4히트) — 현재 단일 HitFrameTime 구조와 별개 작업

---

## 2. 근접 사거리 동작 원리

### 현재 문제
`FindFirstEnemyTarget` 의 판정식:
```
maxDist = AttackRange × HexMetrics.TileHeight(0.866f) + Epsilon(0.05f)
```
- AttackRange = 1.0: maxDist = 0.916f → 인접 타일(0.866f) 감지 ✓
- AttackRange = 0.5: maxDist = 0.483f → 인접 타일(0.866f) 감지 불가 ✗

또한 `UnitMovementUseCase.RequestMove`가 **적 유닛 Position을 blocked에 추가**하므로
근접 유닛이 적에게 접근하는 경로가 생성되지 않는다.

### 수정 후 동작 흐름
1. 근접 유닛(AttackRange < 1.0)은 경로탐색 시 적 유닛 위치를 blocked에서 제외
2. 적 타일 방향으로 직접 접근 경로 생성
3. Lerp 이동 중, 세계 좌표 거리가 maxDist 이하로 좁혀지면 이동 정지 → 공격 시작
4. 유닛이 적 타일에 완전히 도달(같은 타일)하기 전 정지 → **겹침 없음**

### AttackRange 권장 기본값
- 제안값: `0.5f` → maxDist = 0.5 × 0.866 + 0.05 = **0.483f**
- 이 값에서 유닛은 인접 타일 Lerp의 약 44% 지점에서 공격 시작 (시각적으로 적과 맞닿은 느낌)
- StatsReference.md에서 최종 결정. 반드시 0 < AttackRange < 1.0 범위여야 함.

---

## 3. 파일별 변경 내용

### 3-1. `Assets/_Project/Scripts/Domain/Unit/UnitType.cs`
**변경**: `Melee = 3` enum 값 추가

```
Pistoleer = 0
Assault   = 1
Sniper    = 2
Melee     = 3   ← 추가
```

---

### 3-2. `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`
**변경**: 6개 static 메서드에 Melee case 추가

| 메서드 | Melee 반환값 | 비고 |
|--------|------------|------|
| GetMaxHp | 플레이스홀더 | StatsReference 기입 전 임시값 |
| GetAttackPower | 플레이스홀더 | 동일 |
| `GetAttackRange` | **0.5f** | < 1.0 필수. 근접 판정 핵심값 |
| GetMoveSpeed | 플레이스홀더 | 동일 |
| GetAttackCooldown | 플레이스홀더 | UnitFactory에서 클립 길이로 덮어씀 |
| GetHitFrameTime | 플레이스홀더 | Inspector에서 실측 후 갱신 |

---

### 3-3. `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`
**변경**: `RequestMove()` — Melee 유닛은 적 Position을 blocked에서 제외

```csharp
// 변경 전
blocked.Add(other.Position);   // 아군 + 적군 모두 차단

// 변경 후
if (other.Team == unit.Team || unit.AttackRange >= 1.0f)
    blocked.Add(other.Position);
// 근접 유닛(AttackRange < 1.0)은 적 위치를 차단하지 않음
// → 적 타일을 향한 직접 경로 생성 가능
```

---

### 3-4. `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
**변경**: `FindFirstEnemyTargetByHexCoord()` — 소수 AttackRange의 정수 변환 버그 수정

```csharp
// 변경 전
if (distance <= attacker.AttackRange && ...)
// AttackRange = 0.5 → distance <= 0 → 실제로 never match (같은 타일 없음)

// 변경 후
int rangeThreshold = Mathf.Max(1, Mathf.CeilToInt(attacker.AttackRange));
if (distance <= rangeThreshold && ...)
// AttackRange = 0.5 → rangeThreshold = 1 → 인접 타일까지 폴백 탐색
// 폴백이므로 과탐지되어도 무방 (주 경로는 세계 좌표 기반)
```

---

### 3-5. `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`
**변경**: `UnitTeamPrefabSet` 구조체에 `melee` 필드 추가 + 프리팹 선택 switch에 Melee case 추가

```csharp
// UnitTeamPrefabSet 구조체
public struct UnitTeamPrefabSet
{
    public GameObject pistoleer;
    public GameObject assault;
    public GameObject sniper;
    public GameObject melee;   // ← 추가
}

// 프리팹 선택 switch (2곳 동일 변경)
UnitType.Melee => set.melee,   // ← 추가
```

---

### 3-6. `Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs`
**변경**: Melee 버튼/초상화/자동 지시자 필드 추가 + 초기화 코드 추가

추가할 필드:
- `_meleeButton` (Button)
- `_meleeButtonPortrait` (Image)
- `_meleeAutoIndicator` (GameObject)
- `UnitPortraitSet.melee` (Sprite)

초기화 라인 추가:
```csharp
SetupUnitButton(_meleeButton, UnitType.Melee);
```

자동 생산 지시자 업데이트 추가:
```csharp
if (_meleeAutoIndicator != null)
    _meleeAutoIndicator.SetActive(state.IsAutoMode && state.AutoContains(UnitType.Melee));
```

---

## 4. 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `UnitCombatUseCase.FindFirstEnemyTarget` (세계좌표 주 경로) | maxDist 공식이 이미 임의 float range 지원 |
| `UnitView.MoveAlongPath` | `HasEnemyInRange` → `FindFirstEnemyTarget` 경유하므로 변경 불필요 |
| `NetworkCombatController` | `TryFindTarget` / `HasEnemyInRange` 경유하므로 변경 불필요 |
| `UnitData` | `AttackRange` 필드 이미 float, 변경 불필요 |
| `HexPathfinder` | blocked 목록은 호출 쪽(UnitMovementUseCase)이 관리 |

---

## 5. Inspector 작업 (에디터 스크립트 필요)

코드 구현 후 아래 Inspector 작업 필요:
- **UnitFactory**: 각 종족 × 팀(6세트)의 `melee` 슬롯에 프리팹 연결
- **ProductionPanelUI**: 새 버튼/초상화 오브젝트 연결

→ 에디터 1회성 스크립트 또는 수동으로 처리. 프로그래머에게 스크립트 작성 요청.

---

## 6. 위험 요소 / 주의사항

| 위험 | 내용 | 대응 |
|------|------|------|
| 근접 유닛 경로 충돌 | blocked 미포함 시 근접 유닛이 적 타일로 이동 중 다른 아군도 같은 경로 시도 | 아군 ClaimedTile 차단은 유지 (team==same만 적용) |
| 같은 타일 겹침 | Lerp t=1.0에 도달 시 ProcessStep이 적 타일로 Position 업데이트 | AttackRange < 1.0 이면 Lerp 중 적을 반드시 감지하므로 t=1 도달 전 정지됨 (Epsilon=0.05f 안전망) |
| HexCoord 폴백 과탐지 | rangeThreshold=1로 올려서 인접 폴백 발생 | 폴백은 positionProvider null인 엣지 케이스만. 게임 흐름에서 실질적으로 차이 없음 |
| ProductionPanelUI 레이아웃 | 4번째 버튼 추가 시 기존 3버튼 배치 변경 필요 | Presenter에게 Unity UI 레이아웃 조정 요청 |
| AttackCooldown 덮어씌움 | UnitFactory가 Animator 클립 길이로 UnitStats 값을 덮어씀 | Melee도 동일하게 동작 — Attack.anim 클립 길이 자동 적용 |

---

## 7. 구현 순서 (game-programmer에게 위임)

1. `UnitType.cs` — Melee 추가 (컴파일 에러 발생, 하위 항목들이 즉시 필요)
2. `UnitStats.cs` — Melee 플레이스홀더 추가 (컴파일 복구)
3. `UnitFactory.cs` — UnitTeamPrefabSet + switch case 추가
4. `UnitMovementUseCase.cs` — blocked 조건 분기 추가
5. `UnitCombatUseCase.cs` — HexCoord 폴백 수정
6. `ProductionPanelUI.cs` — Melee 버튼/초상화/지시자 추가
7. Inspector 에디터 스크립트 작성 (UnitFactory melee 슬롯 연결 자동화)
