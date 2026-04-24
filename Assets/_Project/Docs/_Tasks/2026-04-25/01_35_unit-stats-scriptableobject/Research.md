# Research — 유닛 스탯 ScriptableObject 전환

## 목적

밸런스 조절 시 코드 수정 + 재컴파일 없이 Inspector에서 직접 수치를 변경할 수 있도록
`UnitStats.cs` (하드코딩 switch 표현식) → ScriptableObject 방식으로 전환.

---

## 현재 구조 분석

### 스탯 제공 파일 (Domain 레이어, 순수 C#)

#### `Assets/_Project/Scripts/Domain/Unit/UnitStats.cs`
- `static class UnitStats`
- switch 표현식으로 7개 메서드를 제공:
  | 메서드 | 타입 | 설명 |
  |--------|------|------|
  | `GetMaxHp(type)` | `int` | 최대 체력 |
  | `GetAttackPower(type)` | `int` | 공격력 |
  | `GetAttackRange(type)` | `float` | 공격 사거리 |
  | `GetDetectRange(type)` | `float` | 적 감지 사거리 |
  | `GetMoveSpeed(type)` | `float` | 이동 속도 |
  | `GetAttackCooldown(type)` | `float` | 공격 쿨다운 (참조값) |
  | `GetHitFrameTimes(type)` | `float[]` | 타격 프레임 타이밍 배열 |

#### `Assets/_Project/Scripts/Domain/Unit/UnitProductionStats.cs`
- `static class UnitProductionStats`
- 같은 switch 패턴으로 3개 메서드 제공:
  | 메서드 | 타입 | 설명 |
  |--------|------|------|
  | `GetProductionTime(type)` | `float` | 생산 시간(초) |
  | `GetGoldCost(type)` | `int` | 골드 비용 |
  | `GetPopulationCost(type)` | `int` | 인구 비용 (현재 전 유닛 1 고정) |

---

### 스탯을 호출하는 파일

| 파일 | 레이어 | 호출 내용 |
|------|--------|-----------|
| `Domain/Unit/UnitData.cs` | Domain | 생성자 2개에서 `GetAttackCooldown`, `GetHitFrameTimes` 호출 |
| `Application/UseCases/UnitSpawnUseCase.cs` | Application | `SpawnUnit()` 2곳에서 `GetMaxHp`, `GetAttackPower`, `GetAttackRange`, `GetDetectRange`, `GetMoveSpeed` 호출 |
| `Infrastructure/Factories/UnitFactory.cs` | Infrastructure | 런타임에 Animator 클립 길이를 읽어 `unitData.AttackCooldown`을 **덮어씀** |

---

### AttackCooldown 특이사항

`UnitStats.GetAttackCooldown()`은 코드상 참조값으로만 존재하며,
실제 런타임값은 `UnitFactory.GetAttackClipLength(animator)`가 Animator에서 읽어 덮어씀.

→ SO 전환 이후에도 이 덮어쓰기 메커니즘은 그대로 유지됨.
→ SO의 `attackCooldown` 필드는 "Animator 클립 설정이 안 된 경우의 fallback" 역할.

---

### 기존 ScriptableObject 패턴 (참조)

`Infrastructure/Config/GameConfig.cs` — 이미 Infrastructure 레이어에 ScriptableObject가 존재함.
- `[CreateAssetMenu]` → `Resources/Config/` 저장 → `Resources.Load<GameConfig>("Config/GameConfig")`
- `GameBootstrapper`에서 로드하여 UseCase에 주입

→ 동일한 패턴으로 `UnitStatsConfig`를 만들면 기존 아키텍처에 자연스럽게 통합됨.

---

## 아키텍처 제약

| 레이어 | Unity 의존 가능? |
|--------|----------------|
| Domain | ❌ 순수 C# 전용 |
| Application | ❌ 순수 C# 전용 |
| Core | ✅ (HexMetrics 등 Unity 의존 있음) |
| Infrastructure | ✅ ScriptableObject 사용 가능 |

`UnitStats.cs`는 Domain 레이어이므로 ScriptableObject를 직접 참조할 수 없음.
→ **Domain은 Dictionary 기반으로 변환하고, Infrastructure의 SO가 데이터를 초기화**하는 방식으로 레이어 규칙 유지.

---

## 영향 범위

| 파일 | 변경 유형 |
|------|-----------|
| `Domain/Unit/UnitStats.cs` | switch 표현식 → Dictionary 조회로 교체, `Initialize()` 메서드 추가 |
| `Domain/Unit/UnitProductionStats.cs` | 동일 패턴 적용 |
| `Infrastructure/Config/UnitStatsConfig.cs` | **신규** ScriptableObject 생성 |
| `Infrastructure/Bootstrap/GameBootstrapper.cs` | `UnitStats.Initialize(config)` 호출 추가 |
| `Resources/Config/UnitStatsConfig.asset` | **신규** 에셋 파일 |

`UnitData.cs`, `UnitSpawnUseCase.cs`는 **변경 없음** — 메서드 시그니처가 동일하게 유지되므로.

---

## 리스크

| 리스크 | 대응 |
|--------|------|
| `.asset` 파일 미연결 → `Initialize()` 미호출 → Dictionary 비어있음 | `UnitStats.GetXxx()` 에서 Dictionary miss 시 경고 로그 + 기본값 반환 |
| `HitFrameTimes` (float 배열) Inspector 편집 불편 | Unity Inspector 기본 배열 UI로 편집 가능, 원소 수 수동 조정 필요 |
| `AttackCooldown` 이중 관리 (SO + Animator) | SO 주석으로 명시, Animator 덮어쓰기 우선순위 유지 |
