# Research: 애니메이션 전환 부드럽게 개선 (Idle 클립 포함)

**작성일:** 2026-03-27 (재작성)

---

## 작업 배경

Walk ↔ Attack ↔ Stop(Idle) 간 전환이 딱딱하게 끊긴다는 피드백.
특히 "공격 종료 → 이동 재개"가 가장 어색하다고 확인됨.
죽음 애니메이션은 이펙트 대체 예정이므로 이번 작업 범위에서 제외.

---

## 에셋 현황

### Idle 클립

| 유닛 | 클립 경로 | 상태 |
|------|----------|------|
| Sniper | `Animations/Units/Sniper/Sniper_Idle.anim` | ✅ 생성 완료 |
| Assault | — | ❌ 미생성 (작업 필요) |
| Pistoleer | `Animations/Units/Pistoleer/meshy4/Pistoleer_Idle.anim` | ✅ 기존 존재 |

### AnimatorController Idle 상태

| 유닛 | Controller 경로 | Idle 상태 | Motion 연결 |
|------|---------------|----------|------------|
| Sniper | `Animations/Units/Sniper/Sniper.controller` | "Sniper_Idle" 상태 있음 | 확인 필요 |
| Assault | `Animations/Units/Assault/Assault.controller` | "Idle" 상태 있음 | ❌ Motion 없음 |
| Pistoleer | `Animations/Units/Pistoleer/Pistoleer.controller` | "Idle" 상태 있음 | 확인 필요 |

---

## 현재 코드 분석

### 파일: `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

#### 현재 상태 전환 방식 (모두 하드컷)

| 전환 | 현재 코드 | 문제 |
|------|---------|------|
| Walk 시작 | `Animator.Play(StateWalk, 0, 0f)` + `speed=1` | 하드컷 |
| Walk 정지 | `animator.speed = 0f` | Idle 클립 없이 Walk 포즈 고정 |
| Walk → Attack | `Animator.Play(StateAttack, 0, 0f)` | 하드컷 |
| Attack 종료 후 대기 | `_attackCoroutine = null`, Animator 그대로 | Attack 마지막 포즈 유지 |
| Attack → Walk | `Animator.Play(StateWalk, 0, 0f)` | **가장 어색한 하드컷** |

#### clipLen 읽기 (중요 제약)

```csharp
animator.Play(StateAttack, 0, 0f);
yield return null;  // 1프레임 대기
float clipLen = animator.GetCurrentAnimatorStateInfo(0).length;
```

- `Animator.Play()` 후 1프레임이면 상태 반영 완료 → `length` 정상 반환
- `CrossFadeInFixedTime()` 전환 시: Attack 클립은 CrossFade 시작 시점부터 재생
- 따라서 `blendTime` 대기 후 length를 읽고, 남은 시간 = `clipLen - blendTime`

---

## 개선 방향: Idle을 전환 허브로 사용

Idle을 중간 허브로 두면 모든 전환이 명확하고 자연스러워짐:

```
[ Idle ] ←→ [ Walk ]
[ Idle ] ←→ [ Attack ]
```

| 전환 | 변경 후 |
|------|--------|
| 초기 진입 | Entry → Idle (기본 상태) |
| 대기 (이동 없음) | Idle 루프 |
| 이동 시작 | CrossFade(Walk) |
| 이동 중 적 발견 | CrossFade(Idle) → 이후 Attack |
| Attack 시작 | CrossFade(Attack) |
| Attack 종료 | CrossFade(Idle) |
| 이동 재개 | CrossFade(Walk) |

---

## 영향 범위

### UnitView.cs 수정 필요 위치

| 메서드 | 변경 내용 |
|--------|---------|
| `StartWalkAnimation()` | `Play` → `CrossFade(Walk)` |
| `StopWalkAnimation()` | `speed=0` → `CrossFade(Idle)` |
| `PlayAttackAnimation()` | `Play` → `CrossFade(Attack)` + clipLen 타이밍 수정, 종료 시 `CrossFade(Idle)` |
| `MoveAlongPath()` Walk 시작 | `Play` → `CrossFade(Walk)` |
| `MoveAlongPath()` 전투 대기 | `speed=0` → `CrossFade(Idle)` |
| `MoveAlongPath()` 전투 후 Walk 재개 | `Play` → `CrossFade(Walk)` |

### AnimatorController Inspector 설정 필요

| 유닛 | 필요 작업 |
|------|---------|
| Sniper | Idle 상태 기본 상태(Default State) 설정, Motion 연결 확인, 트랜지션 추가 |
| Assault | Idle 클립 제작 → Motion 연결, 기본 상태 설정, 트랜지션 추가 |
| Pistoleer | Idle Motion 연결 확인, 기본 상태 설정, 트랜지션 추가 |

---

## 결론

- `Animator.Play()` → `Animator.CrossFadeInFixedTime()` 전면 교체
- Walk 정지: `speed=0` 제거 → `CrossFade(Idle)`
- Idle이 모든 전환의 허브 역할 → 자연스러운 포즈 전환
- AnimatorController에 Idle 상태 연결 + 트랜지션 설정 (Inspector 작업)
- Attack 클립 길이 읽기: blendTime 대기 후 읽고 `clipLen - blendTime` 만큼 추가 대기
