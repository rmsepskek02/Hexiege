# Plan: 공격 애니메이션 Root Motion 문제 해결

## 구현 방향

Apply Root Motion ON을 유지하면서, `OnAnimatorMove()` 콜백으로 실제 캐릭터 이동을 차단.

유니티는 `OnAnimatorMove()`가 MonoBehaviour에 존재하면 Root Motion 자동 적용을 해당 콜백에 위임함.
콜백 내부에서 아무것도 하지 않으면 → Root Motion이 시각적으로는 올바르게 재생되지만 캐릭터 위치는 변하지 않음.

## 수정 파일

### 1. `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`

`OnAnimatorMove()` 메서드 추가.

위치: `StopCombatAnimation()` 메서드 하단 (클래스 말미).

```csharp
/// <summary>
/// Apply Root Motion이 ON일 때 유니티가 Root Motion을 자동 적용하는 대신
/// 이 콜백에 위임한다. 내부를 비워두면 Root Motion의 위치/회전 변화가
/// 캐릭터 transform에 반영되지 않음.
///
/// 이유: 믹사모 공격 애니메이션은 Apply Root Motion ON이어야 팔 각도가
/// 올바르게 재생되지만, 실제 캐릭터 이동은 MoveAlongPath(타일 Lerp)가
/// 전담하므로 Root Motion 이동은 차단해야 함.
/// </summary>
private void OnAnimatorMove()
{
    // 의도적으로 비워둠 — Root Motion 이동/회전 차단
}
```

### 2. Inspector 작업 (모든 유닛 프리팹)

각 유닛 프리팹의 Animator 컴포넌트에서 **Apply Root Motion을 ON**으로 변경.

대상 프리팹:
- Unit_FlameSpirit_Red / Unit_FlameSpirit_Blue
- Unit_BearGuard_Red / Unit_BearGuard_Blue
- Unit_EmberSpirit_Red / Unit_EmberSpirit_Blue
- Unit_FoxMagician_Red / Unit_FoxMagician_Blue
- Unit_InfernoSpirit_Red / Unit_InfernoSpirit_Blue
- Unit_LionKnight_Red / Unit_LionKnight_Blue
- Unit_Pistoleer_Red / Unit_Pistoleer_Blue
- Unit_Sniper_Red / Unit_Sniper_Blue

→ Editor 1회성 스크립트로 일괄 처리 예정 ([5-2] 단계)

## 위험 요소

| 위험 | 대응 |
|------|------|
| Walk 애니메이션에도 Root Motion이 있을 경우 이동 간섭 | OnAnimatorMove()가 모든 상태에서 차단하므로 영향 없음 |
| 다른 유닛 애니메이션이 Apply Root Motion OFF에 맞춰 튜닝된 경우 | 각 유닛 실기 확인 필요 |

## 구현 순서

1. `UnitView.cs`에 `OnAnimatorMove()` 추가
2. Editor 스크립트로 모든 유닛 프리팹 Apply Root Motion ON 일괄 적용
3. Testcase.md 작성 → QA → 사용자 실기 테스트
