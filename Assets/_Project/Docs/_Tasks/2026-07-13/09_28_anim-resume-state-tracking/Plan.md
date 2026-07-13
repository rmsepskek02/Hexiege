# Plan — 전투 종료 후 Walk 재개 시 Animator 상태 의존 제거

## 이 계획이 무엇이고 왜 하는가 (자연어 설명)

유닛이 전투를 끝내고 다시 걷기 시작할 때, 코드는 "이미 걷는 중인지"를 판단해서
걷는 중이면 애니메이션 속도만 되돌리고, 아니면 걷기 애니메이션으로 부드럽게 전환한다.
지금은 그 판단을 **Unity Animator에게 직접 물어보는** 방식으로 하는데,
애니메이션이 섞이는 도중에는 Animator가 엉뚱한(출발) 상태를 알려줄 수 있어 잠재적으로 위험하다.

이 계획은 Animator에게 묻지 않고,
"우리가 마지막으로 지시한 애니메이션 상태"를 유닛이 스스로 로컬 변수에 기억해 두었다가
그 값으로 판단하도록 바꾸는 것이다.
겉보기 동작(플레이 화면)은 전혀 바뀌지 않으며, 프로젝트가 이미 세워둔 원칙
("Animator 런타임 상태에 의존하지 않는다")에 코드를 일치시키는 리팩토링이다.

> ⚠️ **이 문서는 계획서다. 이 문서만으로는 코드를 수정하지 않는다.**
> 사용자 승인 후 game-programmer 에이전트에게 구현을 위임한다.

---

## ⚠️ 기존 로직 제거/대체 고지 (WORKFLOW [4] 기존 로직 제거 규칙)

이 작업은 아래 3개 블록의 `GetCurrentAnimatorStateInfo` 기반 분기를 **로컬 상태 기반 판별로 대체**한다.
- 대체 대상: UnitView.cs `1434~1442`, `1469~1476`, `1531~1538`
- 대체해도 안전한 근거는 하단 **위험 요소 및 동작 동일성 근거** 섹션에 기술한다.
- 구현 시 검증 전까지 기존 블록은 **즉시 삭제하지 않고 주석 처리(비활성화)** 를 기본으로 한다.
  주석 처리된 기존 블록의 **최종 삭제는 사용자 테스트 통과 후, 문서/메모리 업데이트 직전**에 수행한다.

---

## 규칙 근거 (WORKFLOW [4] — 각 수정 항목의 근거 명시)

| 근거 | 내용 | 이번 작업과의 관계 |
|------|------|-------------------|
| **UnitView.cs 1683~1686행 자체 원칙** | "Animator 상태 의존성을 제거하여 CrossFade 블렌딩 중 상태 판별 오류를 근본적으로 방지" (과거 `WaitForAttackCycleEnd` 제거 시 확립) | 이번 작업의 직접 근거 — 잔여 위반 3곳을 이 원칙에 맞춘다. |
| **GameSystemRules_Units 규칙 18 (서버 데미지 타이밍)** | 데미지는 항상 서버 타이머로만 적용하며 Animator 상태(`OnAttackHit`)에 종속시키지 않는다. | "제어 흐름을 Animator 런타임 상태에 종속시키지 않는다"는 동일 방향성. |
| **GameSystemRules_Units 규칙 22 (애니메이션 상태 값 기반 동기화)** | 멀티플레이 애니메이션 상태는 서버 권위 NetworkVariable 값으로 동기화 — 엣지/런타임 상태 의존 회피. | 본 리팩토링도 런타임 Animator 질의 대신 명시적 값(로컬 필드) 기반으로 판별. |

> Plan 작성 전 `Assets/_Project/Docs/GameSystemRules.md` 인덱스 및 `GameSystemRules_Units.md`(규칙 18/22 원문)를 확인했다.

---

## 수정 설계

핵심: **Animator 조회(`GetCurrentAnimatorStateInfo`) 대신 로컬 논리 상태 필드로 추적**한다.

### 1. 신규 필드 추가

UnitView.cs 상태 해시 정의 부근(53~54행 근처)에 추가:

```csharp
// 마지막으로 CrossFade를 지시한 애니메이션 상태 해시(StateWalk 또는 StateAttack)를 기억한다.
// Animator.GetCurrentAnimatorStateInfo는 CrossFade 블렌딩 도중 "출발" 상태를 반환할 수 있어
// 상태 판별이 어긋날 수 있으므로(이 클래스 상단 원칙 — Animator 상태 의존 제거),
// 우리가 직접 지시한 상태를 로컬로 추적해 판별의 단일 출처로 삼는다.
// 0 = 미설정(아직 어떤 CrossFade도 지시하지 않음).
private int _currentAnimStateHash = 0;
```

`StateWalk`/`StateAttack`는 `Animator.StringToHash` 결과이며 0이 될 수 없으므로,
초기값 0은 어떤 실제 상태 해시와도 구분된다(최초 이동 시 정상적으로 Walk CrossFade 유도).

### 2. 기존 CrossFade 4개 지점에서 필드 갱신 (동작 불변, 추적만 추가)

각 CrossFade 호출 **직후** 한 줄을 추가한다. CrossFade 자체 로직은 손대지 않는다.

| # | 위치 | 라인 | 추가 |
|---|------|------|------|
| a | `MoveAlongPathV3` 이동 시작 Walk CrossFade | 879 직후 | `_currentAnimStateHash = StateWalk;` |
| b | `StartWalkAnimation` Walk CrossFade | 1655 직후 | `_currentAnimStateHash = StateWalk;` |
| c | `PlayAttackAnimation` Attack CrossFade | 1676 직후 | `_currentAnimStateHash = StateAttack;` |
| d | `StartCombatAnimation` Attack CrossFade (`applyCrossFadeHere` 블록 내부) | 1725 직후 | `_currentAnimStateHash = StateAttack;` |

### 3. 전투 종료 후 Walk 재개 공통 헬퍼 신설

3곳의 동일 블록을 이 헬퍼 호출 1줄로 대체한다.

```csharp
// 전투 종료 후 Walk 재개 공통 처리.
// Animator.GetCurrentAnimatorStateInfo는 CrossFade 블렌딩 도중 출발 상태를 반환해
// 상태 판별이 어긋날 수 있으므로(이 클래스 상단 원칙 — Animator 상태 의존 제거),
// Animator를 조회하지 않고 로컬 추적 상태(_currentAnimStateHash)로 이미 Walk인지 판별한다.
private void ResumeWalkAnimation()
{
    if (_animator == null) return;
    _animator.speed = 1f;
    if (_currentAnimStateHash == StateWalk) return; // 이미 Walk — 재-CrossFade로 블렌드 리셋 방지
    _animator.CrossFadeInFixedTime(StateWalk, _attackToWalkBlend, 0);
    _currentAnimStateHash = StateWalk;
}
```

대체 대상 3곳(기존 `var stateInfo = ...; if (...) speed=1; else CrossFade;` 블록 → `ResumeWalkAnimation();`):

| # | 메서드 | 기존 블록 라인 | 대체 후 |
|---|--------|---------------|---------|
| 1 | `EnterCombatLoopV3` (멀티 서버 분기) | 1434~1442 | `ResumeWalkAnimation();` |
| 2 | `EnterCombatLoopV3` (싱글 분기) | 1469~1476 | `ResumeWalkAnimation();` |
| 3 | `ResumeFromForwardTileV3` | 1531~1538 | `ResumeWalkAnimation();` |

> 3곳의 `if (_animator != null)` null 가드는 헬퍼 내부의 `if (_animator == null) return;`로 흡수된다.
> 3곳에 붙어 있던 `GameEvents.OnUnitWalkStarted` 발행(예: 1443행, 1539~1540행)은 헬퍼 밖의 기존 위치에 그대로 둔다 — 이번 리팩토링 범위 아님.

---

## 수정 대상 파일

```
[수정]
- Assets/_Project/Scripts/Presentation/Unit/UnitView.cs
```

- 신규 필드 1개(`_currentAnimStateHash`)
- 신규 private 메서드 1개(`ResumeWalkAnimation`)
- CrossFade 추적 4곳 각 1줄 추가
- 기존 분기 블록 3곳 → 헬퍼 호출로 대체

신규 파일 없음. 다른 파일 변경 없음.

---

## 위험 요소 및 동작 동일성 근거

### 겉보기 동작이 바뀌지 않는 근거

1. **재개 시점의 로컬 상태는 항상 Attack이다.**
   3개 재개 지점은 항상 Attack 루프가 안정적으로 재생된 뒤(전투 종료 시점) 호출된다.
   그 직전 `StartCombatAnimation`이 싱글/호스트/서버 컨텍스트(`applyCrossFadeHere == true`)에서
   `_currentAnimStateHash = StateAttack`으로 설정한 상태다.
   따라서 `ResumeWalkAnimation`에서 `_currentAnimStateHash != StateWalk` → 기존과 동일하게 Walk로 CrossFade.

2. **실행 컨텍스트가 서버/호스트/싱글 한정이다.**
   3곳은 `MoveAlongPathV3` 서버 가드(856~859행) 이후 흐름이므로 멀티플레이 클라이언트에서 실행되지 않는다.
   클라이언트 애니메이션(규칙 22 값 기반 레벨 동기화 경로)에는 영향이 없다.

3. **초기값 0이 안전하다.**
   필드 초기값 0은 `StateWalk`/`StateAttack` 어떤 해시와도 다르므로,
   최초 이동 등 아직 상태를 지시하지 않은 경우에도 `!= StateWalk`로 판정되어 정상적으로 Walk CrossFade가 일어난다.

### 남는 리스크 / 확인 포인트

- 추적 필드 갱신을 **CrossFade 발생 지점 4곳 모두**에 빠짐없이 넣어야 정합성이 유지된다.
  한 곳이라도 누락되면 로컬 상태와 실제 애니메이션이 어긋날 수 있다 → 구현 시 4곳 체크 필수.
- 향후 Walk/Attack 외 새 상태를 CrossFade로 도입하면, 그 지점에도 필드 갱신을 추가해야 한다(설계 규약으로 유지).

---

## 범위 제한 (명시)

- 이번 작업은 **위 3곳의 원칙 위반 제거 + 그에 필요한 로컬 상태 추적 도입**만 포함한다.
- `StartWalkAnimation`의 XML 주석과 코드 불일치(Research.md 부가 이슈)는 이번 범위 밖 — 수정하지 않는다.
- `Testcase.md`는 사용자가 명시적으로 요청하지 않았으므로 작성하지 않는다(WORKFLOW [5-1]).
- 코드 구현은 사용자 승인 후 game-programmer 에이전트에 위임한다(CLAUDE.md 규칙 3·11).
