# Research: 공격 애니메이션 Root Motion 문제

## 작업 배경

믹사모에서 FlameSpirit 모델을 업로드하여 받은 공격 애니메이션이 유니티에서 믹사모 미리보기와 다르게 재생됨.
팔의 움직임과 각도가 다르게 보이는 현상.

## 시도한 해결 시도 및 결과

| 시도 | 결과 |
|------|------|
| Bake Into Pose 체크 | 동일 현상 |
| 애니메이션 FBX Avatar → Copy From Other Avatar (FlameSpiritAvatar) | 동일 현상 |
| Avatar Configure → Enforce T-Pose | 동일 현상 |
| Generic 리그로 전환 | 뼈 Missing 오류 → 재생 불가 |
| **Apply Root Motion ON** | **믹사모와 동일하게 재생됨 (원인 확인)** |

Generic 리그 실패 원인: 믹사모가 자동 리깅 과정에서 원본 모델에 없는 뼈(Left Foot Q, Left Foot T 등)를 추가하여 뼈 이름 불일치 발생.

## 근본 원인

Apply Root Motion OFF 상태에서 팔 각도가 다르게 보이는 정확한 원인은 불명확하나, Apply Root Motion ON 시 믹사모와 동일하게 재생됨이 실기로 확인됨.

Apply Root Motion ON의 부작용: 공격 애니메이션에 포함된 루트 본 이동 데이터가 캐릭터 위치에 실제로 반영되어 공격 중 캐릭터가 이동함 → 헥스 타일 기반 이동 코드와 충돌 가능.

## 관련 파일

| 파일 | 역할 |
|------|------|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 모든 유닛의 애니메이션 제어 (Animator 캐시, CrossFade 호출) |
| `Assets/_Project/Models/Units/FlameSpirit/FlameSpirit.fbx` | FlameSpirit 모델 (FlameSpiritAvatar 포함) |
| `Assets/_Project/Models/Units/FlameSpirit/[공격 애니메이션].fbx` | 믹사모에서 받은 공격 애니메이션 |

## 영향 범위

- `UnitView.cs` 1개 파일만 수정
- `OnAnimatorMove()`는 Animator가 있는 모든 유닛에 동일하게 적용됨
- 이동 코드(`MoveAlongPath`)는 `transform.position` Lerp 기반으로 Root Motion과 별개 → 충돌 없음

## 추가 확인 사항

현재 모든 유닛의 Animator Inspector에서 **Apply Root Motion을 ON으로 변경**해야 함.
(FlameSpirit 외 다른 유닛도 동일한 현상이 있을 수 있음)
