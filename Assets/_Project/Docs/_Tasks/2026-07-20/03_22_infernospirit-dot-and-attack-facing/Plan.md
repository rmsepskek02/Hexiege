# Plan — InfernoSpirit DoT 구현 + 공격 방향 버그 수정

## 이 작업이 무엇인지 (자연어 설명)

두 가지를 함께 처리합니다.
1. **InfernoSpirit DoT** — 이미 완성된 원거리 유닛 InfernoSpirit에, 스펙에만 있고 코드엔 없던 특수 능력
   "때린 적 유닛에게 3초간 초당 5(총 15) 지속 피해"를 구현합니다. MushroomBomber에서 만든 **DoT 초 단위 틱
   시스템(규칙 40)을 단일 대상으로 재사용**하므로 아주 단순합니다(핸들러 1개 + 등록 1줄 + 튜닝값).
2. **공격 방향 버그** — 유닛이 공격할 때 타겟을 제대로 안 바라보는 현상을 재현·진단해 최소 수정합니다.

관련 규칙(`GameSystemRules_Units.md`): **규칙 23**(전략 핸들러) · **25**(튜닝 파라미터) · **34/40**(DoT 초 단위 틱).
기본 규칙 16(아군 무피해)·18(서버 권위 타이밍) 전제.

---

## 확정된 설계 결정 (사용자, 2026-07-20)

### InfernoSpirit DoT
| # | 결정 |
|---|------|
| 1 | 구조 = 일반 원거리 공격(직접 25, 기존 `ExecuteAttack`) + DoT 특수 핸들러. `ReplacesPrimaryAttack=false` |
| 2 | DoT = **주 타깃 1마리에게만**(AoE 아님). 주 타깃도 직접 25 + DoT 둘 다 |
| 3 | DoT 대상 = **적 유닛만**. 건물 DoT 제외(건물은 직접 25만) |
| 4 | DoT = 5/초 × 3초(총 15), **1초 discrete·올림·매초 남은 체력 텍스트·서버 권위** (MushroomBomber와 동일) |
| 5 | 갱신 = 중첩 없음(리셋) |

### 공격 방향 버그 — ⚠️ 이번 범위 제외(보류, 사용자 결정 2026-07-20)
- **이번 작업에서는 수정하지 않는다.** DoT만 구현한다.
- **진단 결과(기록용, 향후 참고)**: InfernoSpirit 에셋(모델·애니·Root Motion·NetworkTransform)은 정상 작동
  정령 유닛 FlameSpirit과 **완전히 동일** → 에셋 문제 아님. FlameSpirit·EmberSpirit은 **근접(사거리 0.5)** 이라
  타겟에 붙어 공격해 facing 오차가 안 보이고, InfernoSpirit은 **원거리(4.0)** 라 오차가 드러난다. 즉
  InfernoSpirit 전용이 아니라 **멀티플레이에서 원거리 공격 시 서버측 facing(공유 회전 로직) 문제**로 추정.
  회전은 멀티에서 서버 계산 + NetworkTransform 동기화(클라 직접 회전 안 함). 수정 시 근접 유닛 회귀 주의.
  (향후 별도 작업으로 진행.)

---

## 구현 항목

### (a) InfernoSpirit DoT 핸들러 `InfernoAttackBehavior` (규칙 23) [신규]

- `Scripts/Application/Combat/InfernoAttackBehavior.cs` 신설. `ISpecialAttackBehavior` 구현,
  `ReplacesPrimaryAttack = false`(직접 25는 주 타깃 단일 피해가 담당).
- `SpecialAttackRegistry`에 `_behaviors[UnitType.InfernoSpirit] = new InfernoAttackBehavior();` 1줄.
- `Apply(SpecialAttackContext)` 동작:
  1. **주 타깃이 적 유닛이면** 그 1명에게 DoT 부여((b) 호출). **건물이면 아무것도 안 함**(직접 25만).
  2. 주 타깃이 직접 25에 이미 죽었으면(제거됨) DoT 스킵(자연 배제 — 데이터 흐름으로 보장, MushroomBomber와 동일).
  3. AoE 아님 — 반경 수집 없음. 오직 주 타깃 1명.
- MushroomBomber `BlastAttackBehavior`가 반경 수집 후 각자 DoT를 걸던 것을, InfernoSpirit은 **주 타깃 1명에게만** 거는 축소판.

### (b) DoT 부여 = 기존 DoT 초 단위 틱 재사용 (규칙 40)

- `UnitCombatUseCase.ApplyDamageOverTime(source, target, perSecond=5, duration=3, tickInterval=1)` **그대로 호출**.
  (MushroomBomber `ApplyBlastDot`처럼 InfernoSpirit용 진입점 `ApplyInfernoDot`을 두고 `SpecialAttackContext`에
  델리게이트로 넘기는 구조 — 기존 `ApplyDot` 패턴 재사용.)
- 1초 discrete·틱당 `CeilToInt`·총량 15 클램프·매초 `OnEntityDamaged`(남은 체력 텍스트)·갱신 리셋 = **이미 구현됨, 무변경**.
- ⚠️ `SpecialAttackContext.ApplyDot`이 현재 MushroomBomber 값(2/3)으로 고정 주입돼 있으면, InfernoSpirit은 **자기 튜닝값(5/3)** 을 쓰도록 별도 진입점/파라미터가 필요. (컨텍스트에 유닛별 DoT 진입점을 분리하거나, 핸들러가 값을 넘기는 형태 — 구현자 판단, 단 MushroomBomber 회귀 없게.)

### (c) 튜닝 파라미터 (SpecialAttackConfig, 규칙 25)

- 신규 필드: `_infernoDotPerSecond`(기본 5) / `_infernoDotDuration`(기본 3) + getter.
- GameBootstrapper가 읽어 float 주입(기존 blast/sweep/wave/bloom 주입 패턴). 코드 폴백(5/3).
- ⚠️ 에셋≠배선(규칙 25): `_specialAttackConfig` 연결 확인. 미연결 시 폴백.

### (d) 공격 방향 버그 — ⚠️ 이번 미구현(보류)

사용자 결정(2026-07-20)으로 **이번 작업 범위에서 제외**한다. 진단 결과는 위 "확정된 설계 결정 > 공격 방향 버그"에
기록해 두었으며(InfernoSpirit 에셋은 정상 유닛과 동일 → 멀티 원거리 facing 공유 로직 문제로 추정), 별도 작업으로 다룬다.
이번 구현은 **(a)~(c) DoT만** 수행한다.

---

## 영향 범위 / 파일

| 파일/영역 | 변경 | 구분 |
|-----------|------|------|
| `Application/Combat/InfernoAttackBehavior.cs` | 단일 대상 DoT 핸들러 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | 등록 1줄 | 수정 |
| `Application/Combat/SpecialAttackContext.cs` / `UnitCombatUseCase.cs` | InfernoSpirit DoT 진입점(5/3), 튜닝 주입 | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | inferno DoT 필드 | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | inferno DoT 주입 | 수정 |
| `Presentation/Unit/UnitView.cs` 또는 InfernoSpirit 프리팹 | 공격 방향 버그 수정(진단 결과에 따라) | 수정(진단) |

**무변경 재사용**: DoT 초단위 틱(규칙 40)·특수공격 전략 구조(규칙 23)·`ExecuteAttack`/`ApplyDamageToVictim`/피해·사망·데미지 텍스트. InfernoSpirit 에셋/생산/VFX/OnAttackHit.

---

## 위험 요소 / 주의
1. **MushroomBomber DoT 회귀** — InfernoSpirit이 자기 값(5/3)을 쓰되 MushroomBomber(2/3) 경로를 건드리지 말 것. `ApplyDot` 공유 시 유닛별 값 분리 확인.
2. **건물 DoT 제외** — 주 타깃 건물이면 DoT 없음(유닛만). 직접 25는 건물에 적용(공성).
3. **직접+DoT** — 주 타깃 유닛은 직접 25 + DoT. 직접 25 사망 시 DoT 스킵.
4. **공격 방향 수정의 광범위 영향** — 회전 로직이 공통이면 전 유닛에 영향 → 회귀 테스트 필수. 유닛 특정이면 국소.
5. **멀티 회전 동기화** — 서버 권위/NetworkTransform 구조 유지(클라 직접 회전 금지).
6. **에셋≠배선**(규칙 25) — inferno DoT 값·`_specialAttackConfig` 배선 확인.

---

## 검증 (QA 포인트, TC 별도 작성 불요)
- InfernoSpirit 공격 시 주 타깃 유닛에 직접 25 + DoT 5/초×3초(총 15), 매초 남은 체력 텍스트, 올림, 초과 없음.
- 건물 대상: 직접 25만, DoT 없음. 아군 무피해.
- DoT 갱신(리셋), 중첩 없음. DoT로 사망 시 정상 처리.
- 멀티: 서버 틱 1회, 클라 HP·텍스트 동기화(이중 없음).
- **공격 방향**: 유닛이 공격 시 타겟을 정확히 바라봄(스냅/추적). 근접·원거리·힐러·기타 유닛 회귀 없음.
- MushroomBomber(2/3)·BloomFairy 힐·파도·기존 공격 회귀 없음.

---

## 에이전트 위임 (CLAUDE.md 규칙 3)
- 코드 구현(InfernoSpirit DoT 핸들러·튜닝) + 공격 방향 버그 진단·수정 → **game-programmer**.
- 구현 후 검증 → **qa-tester**(위 포인트, TC 문서 없이).
- 버그가 광범위 로직 수정으로 번지거나 설계 판단 필요 시 → 사용자 확인(규칙 12).

## 남은 특수 유닛(참고)
InfernoSpirit 이후 잔여: **QuakeSpirit**(착탄형 — MushroomBomber 원형 반경 판정 규칙 38 재사용). 특수 유닛 마무리 단계.
