# Research — InfernoSpirit DoT 구현 + 공격 방향(타겟 바라보기) 버그 수정

## 이 작업이 무엇인지 (자연어 설명)

두 가지를 함께 진행합니다.

**1) InfernoSpirit(지옥불 정령) DoT 구현**
InfernoSpirit은 원거리 포격 유닛으로 **이미 완성**되어 있습니다(프리팹·VFX·애니메이션·EffectPreset·스탯·`OnAttackHit` 주입·소환 가능). 그러나 스펙에 있는 특수 능력 **"때린 적 유닛에게 3초간 초당 5씩(총 15) 지속 피해(DoT)"** 가 **코드로 구현된 적이 없습니다**(리포 전체·git 히스토리 확인 결과 DoT 로직 부재). 지금 게임에서 InfernoSpirit은 일반 원거리 공격만 하고 DoT는 들어가지 않습니다. 이번에 MushroomBomber에서 만든 **DoT 초 단위 틱 시스템(규칙 40)** 을 **단일 대상**으로 재사용해 이 DoT를 얹습니다(AoE 아님 — 때린 그 유닛에게만).

**2) 공격 방향이 타겟을 바라보지 않는 버그 수정**
실기 테스트에서 **유닛이 공격할 때 타겟 방향을 제대로 바라보지 않는** 현상이 발견됐습니다. 원인을 재현·진단해 수정합니다(InfernoSpirit 특정 문제인지, 원거리 유닛 공통인지, 전체 공통인지 진단 포함).

---

## 파트 1 — InfernoSpirit DoT

### 대상 유닛 스펙 (StatsReference.md 기준)
| 항목 | 값 |
|------|----|
| UnitType | `InfernoSpirit` (enum 값 12, 정령계) |
| HP | 60 |
| 공격력(직접) | 25 |
| 공격 사거리 / 감지 사거리 | 4.0 / 4.0 |
| 이동 속도 | 1 |
| 쿨다운 | 1:15(3:00) — 전체 주기 3.0s |
| 생산 / 골드 / 인구 | 30초 / 400 / 1 |
| 특수 | **피격 유닛에 DoT 5/초 × 3초(총 15) — 유닛 대상만** |

### 확정된 설계 결정 (사용자, 2026-07-20)
1. **구조**: 일반 원거리 공격이 주 타깃에 **직접 25**를 적용(기존 `ExecuteAttack`, 무변경). 특수 핸들러는
   **주 타깃 1마리에게만 DoT** 부여(AoE 아님, 반경 없음). `ReplacesPrimaryAttack=false`.
2. **주 타깃도 직접 25 + DoT 둘 다** 받음(때린 그 유닛).
3. **DoT 대상 = 적 유닛만**. **건물은 DoT 제외**(건물은 직접 25만).
4. **DoT 방식 = MushroomBomber와 동일**: **1초 간격 discrete**, 틱당 **올림**, 총 **15** 클램프,
   **매초 남은 체력 데미지 텍스트**, 서버 권위.
5. **갱신 = 중첩 없음(리셋)** — 같은 유닛 연속 피격 시 3초·15 리셋(규칙 34/40 공용).

### 현재 코드 구조 (재사용)
- **특수공격 전략 핸들러(규칙 23)**: `Application/Combat/`의 `ISpecialAttackBehavior`/`SpecialAttackRegistry`/
  `SpecialAttackContext`. `ExecuteAttack`이 단일 피해 직후 특수 훅 호출. 신규 유닛 = **핸들러 + 레지스트리 1줄**.
- **DoT 초 단위 틱(규칙 40, MushroomBomber에서 구축)**: `UnitCombatUseCase.ApplyDamageOverTime(source, target, perSecond, duration, tickInterval)`
  이 이미 있음. 1초 discrete·올림·총량 클램프·매초 `OnEntityDamaged`(데미지 텍스트)·갱신 리셋 전부 구현됨.
  **InfernoSpirit은 이 함수를 주 타깃 1명에게 호출만 하면 됨.**
- **SpecialAttackConfig(규칙 25)**: 튜닝값(초당/지속)을 SO 필드로 추가 → GameBootstrapper가 float 주입.
- **에셋/배선**: InfernoSpirit은 프리팹·VFX(`vfx_infernospirit_charge`)·애니·EffectPreset·스탯·`OnAttackHit`(클립에
  이벤트 1개 존재 확인)·소환까지 이미 완비 → **에디터 작업·생산 배선·VFX 불필요**. DoT는 기존 공격에 얹힌다.

### InfernoSpirit vs MushroomBomber 차이 (단순함)
- MushroomBomber: 착탄 지점 **원형 반경 AoE** DoT. InfernoSpirit: **때린 대상 1마리** DoT(반경 없음).
- 따라서 InfernoSpirit 핸들러는 반경 수집 없이 **주 타깃이 적 유닛이면 그 1명에게 DoT 부여**로 끝.

---

## 파트 2 — 공격 방향(타겟 바라보기) 버그

### 증상
유닛이 공격할 때 **타겟을 정확히 바라보지 않는다**(공격 방향/몸 방향이 타겟과 어긋남). 실기 테스트(InfernoSpirit)에서 발견.

### 현재 회전(바라보기) 메커니즘 (파악 결과)
- **전투 시작 시 스냅 회전**: `UnitView.StartCombatAnimation`(`UnitView.cs:1942`)이 전투 시작 시
  `CalculateAttackAngle(타겟 위치)`로 각도를 구해 `transform.rotation = Quaternion.Euler(0, angle, 0)`로 **즉시 스냅**(1948~1949).
- **공격 중 추적 회전**: `Update`(`:273`)가 매 프레임 `_combatTargetTransform`을 향해
  `Quaternion.RotateTowards(..., _rotationSpeed(270°/s) * dt)`로 **점진 회전**(299~309).
  - 멀티 클라이언트는 `NetworkContext.IsNetworkActive && !IsNetworkServer`면 회전 직접 적용 안 함(NetworkTransform 동기화, `:297`).
- **타겟 참조 설정**: `StartCombatAnimation`(1972) / `ChangeTarget`(1990)에서 `_combatTargetTransform`/`_combatTargetId` 설정.
- **VFX 발사 방향**: `OnAttackHit`이 `transform.forward`(유닛 정면)를 발사 방향으로 사용(`_vfxSpawnPoint` 위치 + LookRotation(forward)).
  주석(`:103`)에 "유닛은 공격 시 적 방향으로 이미 회전하므로 이 Transform의 월드 회전이 곧 발사 방향".

### 진단할 후보 원인 (Plan에서 game-programmer가 재현·확정)
1. **프리팹 모델 방향 오프셋**: `transform`은 타겟을 향하지만 InfernoSpirit **모델(메시)의 정면이 transform.forward와 다른 축**이면 몸이 어긋나 보임(유닛 특정). VFX 발사도 forward 기준이라 함께 틀어짐.
2. **VFX 스폰 회전**: `_vfxSpawnPoint`(본 하위) 회전 오프셋 관련(기존에 본 회전 섞임 이슈를 forward로 교체한 이력 있음, `:103`).
3. **원거리 유닛 타이밍**: 사거리 4.0으로 **멀리서 즉시 공격** 시, 점진 회전(`RotateTowards`)이 첫 발사 시점에 아직 타겟을 다 안 바라봤을 수 있음(스냅이 있으나 타겟 이동/교체 시 추적 지연).
4. **팀(Red) 좌표 반전 +180° 보정**(`NetworkUnit.LateUpdate`)과의 상호작용.
5. **타겟 참조 경로 누락**: 특정 경로에서 `ChangeTarget`/`StartCombatAnimation` 미호출로 `_combatTargetTransform` 미설정 → 회전 안 함.

→ **InfernoSpirit 특정 vs 공통** 여부부터 재현으로 판별한 뒤 근본 원인 수정. (증상이 원거리 유닛 공통이면 다른 원거리 유닛도 함께 개선.)

---

## 영향 범위 (예상)

| 파일/영역 | 예상 변경 | 구분 |
|-----------|-----------|------|
| `Application/Combat/InfernoAttackBehavior.cs` | 단일 대상 DoT 특수 핸들러 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | InfernoSpirit 등록 1줄 | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | inferno DoT(초당/지속) 튜닝 필드 | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | inferno DoT 튜닝값 주입 | 수정 |
| `Presentation/Unit/UnitView.cs` (또는 프리팹) | 공격 방향 버그 수정(진단 후 확정) | 수정(진단) |
| InfernoSpirit 프리팹 | (버그가 모델 오프셋이면) 조정 — 에디터 | 검토/에디터 |

**무변경 재사용**: DoT 초단위 틱(규칙 40) 전체, 특수공격 전략 구조(규칙 23), `ExecuteAttack`/`ApplyDamageToVictim`/피해·사망 이벤트, 데미지 텍스트. InfernoSpirit 에셋/생산/VFX/OnAttackHit(무변경).

---

## 현재 상태 (구현 전제)
- InfernoSpirit(12): 유닛 에셋 전부 존재, 스탯 입력됨, 소환·원거리 공격 동작. **DoT 로직만 없음.**
- 특수공격 레지스트리에 InfernoSpirit 미등록.
- DoT 초단위 틱 시스템(규칙 40)은 MushroomBomber로 이미 구현·검증됨(재사용 가능).
- 공격 방향 버그: 재현·진단 전(회전 로직은 `UnitView.StartCombatAnimation` 스냅 + `Update` RotateTowards 추적).

---

## 핵심 난이도 / Plan에서 결정할 항목
1. **InfernoSpirit DoT 핸들러**: 주 타깃 1명에게만 DoT(유닛 한정, 건물 제외), 직접 25 사망 시 자연 배제.
2. **DoT 튜닝값**: 5/초·3초를 SpecialAttackConfig에(에셋≠배선 확인, 코드 폴백).
3. **공격 방향 버그 진단**: 재현 → 근본 원인(모델 오프셋 / VFX 회전 / 타이밍 / 타겟참조 / 팀보정) 확정 → 최소 수정.
   유닛 특정인지 공통인지 판별, 회귀 없이 수정.
