# Research — 전투 종료 후 Walk 재개 시 Animator 상태 의존 제거

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

유닛이 전투를 끝내고 다시 걷기 시작할 때, 코드는 "지금 이 유닛이 이미 걷는 중인지"를 판단해서
- 이미 걷는 중이면 애니메이션 속도만 되돌리고,
- 아직 공격 자세면 걷기 애니메이션으로 부드럽게 전환(CrossFade)한다.

문제는 "지금 걷는 중인지"를 **Unity Animator에게 직접 물어보는** 방식(`GetCurrentAnimatorStateInfo`)으로 확인한다는 점이다.
애니메이션이 부드럽게 섞이는(CrossFade 블렌딩) 도중에는 Animator가 "지금 향하고 있는 목표 상태"가 아니라 "출발 상태"를 알려줄 수 있다.
그래서 블렌딩 도중에 이 판단이 호출되면 결과가 어긋날 수 있다.

이 프로젝트는 과거에 바로 이 문제 때문에 "Animator 런타임 상태에 의존하지 말자"는 원칙을 세웠다
(UnitView.cs의 `WaitForAttackCycleEnd()` 제거 주석에 명문화됨).
그런데 지금 3곳이 아직 그 원칙을 어기고 Animator에게 직접 상태를 묻고 있다.

**현재는 실제 버그가 아니다.** 이 3곳은 항상 전투가 완전히 끝나 애니메이션이 안정된 시점에만 호출되므로 정상 동작한다.
하지만 나중에 호출 흐름이 바뀌면 오작동할 수 있는 **잠재 취약점**이다.

이번 작업은 동작을 바꾸는 것이 아니라, Animator에게 묻는 대신
"우리가 마지막으로 어떤 애니메이션을 지시했는지"를 유닛이 스스로 로컬 필드에 기억하게 만들어
기존 원칙과 일치시키는 **리팩토링**이다. 플레이 화면상 겉보기 동작은 전혀 바뀌지 않는다.

---

## 대상 파일

`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

- 레이어: **Presentation** — Unity.Netcode 직접 참조 금지, 네트워크 상태는 `NetworkContext` 정적 홀더로만 조회
  (`NetworkContext.IsNetworkActive` / `IsNetworkServer`).
- 애니메이션 상태 해시(파일 상단 정의):
  - `private static readonly int StateWalk = Animator.StringToHash("Walk");` (53행)
  - `private static readonly int StateAttack = Animator.StringToHash("Attack");` (54행)
- 블렌드 시간 필드: `_idleToWalkBlend`(85행), `_toAttackBlend`(88행), `_attackToWalkBlend`(91행)

---

## 확립된 원칙 (근거의 출처)

UnitView.cs 1683~1686행, `WaitForAttackCycleEnd()` 제거 주석:

> WaitForAttackCycleEnd() 제거됨 (2026-04-03).
> Animator normalizedTime 기반 대기 → 도메인 AttackCooldownRemaining 기반 대기로 교체.
> **Animator 상태 의존성을 제거하여 CrossFade 블렌딩 중 상태 판별 오류를 근본적으로 방지.**

이 원칙이 아직 적용되지 않은 잔여 지점이 아래 3곳이다.

---

## 원칙 위반 위치 3곳 (모두 동일 패턴)

세 곳 모두 아래와 같이 Animator에 현재 상태를 질의하여 "이미 Walk면 speed만, 아니면 Walk로 CrossFade"로 분기한다.

```csharp
if (_animator != null)
{
    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
    if (stateInfo.shortNameHash == StateWalk)
        _animator.speed = 1f;
    else
        _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
}
```

| # | 메서드 | 라인 | 맥락 |
|---|--------|------|------|
| 1 | `EnterCombatLoopV3` (멀티플레이 서버 분기) | **1434~1442** | 공격 사이클 완료 후 사거리 이탈 → 이동 재개 |
| 2 | `EnterCombatLoopV3` (싱글플레이 분기) | **1469~1476** | 싱글플레이 전투 종료 → 이동 재개 |
| 3 | `ResumeFromForwardTileV3` | **1531~1538** | 근접 전투 종료 후 A* 재개 |

> 참고: 작업 지시 시 전달된 라인 번호(1451~1459 / 1486~1493 / 1548~1555)는 현재 파일 기준으로
> 각각 위 표의 번호로 확인되었다(코드 내용은 동일). 본 문서는 실제 파일을 읽어 확인한 라인 번호를 사용한다.

---

## 영향 범위 분석

### 이 3곳은 클라이언트에서 실행되지 않는다 (서버/호스트/싱글 전용)

세 지점은 모두 `MoveAlongPathV3` 코루틴 흐름 안(또는 그 흐름에서 호출되는 `EnterCombatLoopV3` /
`ResumeFromForwardTileV3`) 에서 실행된다. `MoveAlongPathV3`는 코루틴 진입 직후 서버 가드로 막힌다.

UnitView.cs 856~859행:

```csharp
if (NetworkContext.IsNetworkActive && !NetworkContext.IsNetworkServer)
{
    // ... (멀티플레이 non-server 클라이언트)
    yield break;
}
```

즉 이 세 지점의 실행 컨텍스트는 다음 세 가지뿐이다.
- 싱글플레이 (`IsNetworkActive == false`)
- 멀티플레이 호스트(서버)
- 멀티플레이 전용 서버

멀티플레이 **클라이언트**의 Walk/Attack 전환은 별도의 값 기반 레벨 동기화 경로
(`NetworkUnit`의 애니메이션 상태 NetworkVariable → `StartWalkAnimation` / `PlayAttackAnimation`)가 담당하므로,
이번 리팩토링은 클라이언트 애니메이션에 영향을 주지 않는다.

### 현재 정상 동작하는 이유 (왜 지금은 버그가 아닌가)

세 지점은 항상 Attack 루프가 안정적으로 재생된 뒤(전투 종료 시점)에만 도달한다.
그 시점의 Animator 현재 상태는 Attack이므로 `shortNameHash == StateWalk`가 거짓 →
기존 코드는 정상적으로 Walk CrossFade를 수행한다. 블렌딩 도중에 호출되는 경로가 현재는 없어서 겉으로 문제가 드러나지 않는다.

### 잠재 위험

향후 흐름 변경으로 이 판단이 CrossFade 블렌딩 도중에 호출되면,
`GetCurrentAnimatorStateInfo`가 출발 상태를 반환하여 분기가 어긋날 수 있다.
이것이 원칙(1683~1686행)이 애초에 제거하고자 한 위험이다.

---

## 추적 지점 (동작 유지에 필요한 CrossFade 발생 위치)

원칙을 지키려면 Animator에 묻는 대신 "마지막으로 지시한 상태"를 로컬에 기록해야 한다.
그 기록을 갱신할 실제 CrossFade 발생 지점은 아래 4곳이다(모두 확인 완료).

| CrossFade | 라인 | 대상 상태 |
|-----------|------|-----------|
| `MoveAlongPathV3` 이동 시작 Walk CrossFade | 879 | Walk |
| `StartWalkAnimation` (클라이언트 값 동기화 경로) | 1655 | Walk |
| `PlayAttackAnimation` (클라이언트 값 동기화 경로) | 1676 | Attack |
| `StartCombatAnimation` Attack CrossFade (`applyCrossFadeHere` 블록) | 1725 | Attack |

`StartCombatAnimation`의 `applyCrossFadeHere` 조건(1721행)은
`!NetworkContext.IsNetworkActive || NetworkContext.IsNetworkServer` — 즉 싱글/호스트/서버에서만 Attack을 직접 CrossFade한다.
이는 위 3개 재개 지점의 실행 컨텍스트와 정확히 일치하므로, 재개 직전 상태 기록은 항상 Attack으로 세팅되어 있다.

---

## 작업 중 발견한 부가 이슈 (이번 범위 밖 — 수정하지 않음)

- **`StartWalkAnimation` XML 주석과 코드 불일치** (UnitView.cs 1645행 vs 1654~1655행)
  - 주석: "이미 Walk 상태면 리셋하지 않고 speed만 복원."
  - 실제 코드: 조건 없이 `_animator.speed = 1f;` 후 무조건 `CrossFadeInFixedTime(StateWalk, ...)` 호출.
  - 즉 주석이 설명하는 "이미 Walk면 speed만" 분기가 코드에 없다.
  - 이번 작업 범위(3곳 원칙 위반 제거)와 별개 이슈이므로 기록만 하고 수정하지 않는다.

---

## 결론

- 원칙 위반 3곳(1434~1442 / 1469~1476 / 1531~1538)은 기능상 현재 정상이나 Animator 런타임 상태에 의존하는 잠재 취약점이다.
- 로컬 추적 필드로 대체하면 원칙과 일치하며 겉보기 동작은 불변이다(근거: 실행 컨텍스트가 서버/호스트/싱글 한정 + 재개 직전 상태가 항상 Attack).
- 구체 설계는 Plan.md 참조.
