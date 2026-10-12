# Research — TorrentSpirit 파도 발사 시점 교정

TorrentSpirit의 공격 이펙트가 시작되는 애니메이션 시점과 게임이 파도를 발사하는 시점을 맞추는 작업이다. 사용자가 Attack 클립의 이벤트를 `2:10`으로 직접 수정·저장했으므로, 이번 조사에서는 클립을 다시 열거나 바꾸지 않는다. 발사 뒤 파도가 이동하면서 대상마다 피해·회복을 주는 동작은 별개의 단계로 유지한다.

## 1. 이번 작업의 기준과 증거 경계

- **사용자 확정 입력(2026-09-30):** Attack 이펙트/파도 발사 마커 `2:10`. Unity Animation 창의 `초:프레임(30fps)`이므로 `2 + 10/30 = 70/30 ≈ 2.3333333초`. 사용자가 클립 변경을 저장했고 재확인을 금지했다. 아래 `2:10`은 사용자 제공값이지 이 조사에서 클립 파일을 다시 측정한 결과가 아니다.
- **직접 확인한 런타임 자산:** `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 `unitType: 18`은 `attackCooldown: 4`, `hitFrameTimes: [0.5]`이다. `Assets/_Project/Resources/Config/SpecialAttackConfig.asset`은 `_waveTravelTime: 0.5`이다. 앞의 값은 공격 시작에서 파도 발사까지의 예약 지연, 뒤의 값은 발사 후 서버 전선 이동 시간이며 서로 다른 설정이다.
- **문서 불일치:** `Assets/_Project/Docs/StatsReference.md` TorrentSpirit 행은 `0:50(4:00)`과 “타격 0.5s는 임시”를 동시에 담고 있다. 문서 상단은 과거 콜론 표기에 `초:프레임`과 소수 초 표기가 혼재했다고 경고하므로 `0:50`을 임의로 50프레임이나 0.50초로 재해석하지 않는다. 이번에는 사용자 확정 `2:10`으로 현행 타격/발사 시점을 명확히 교정해야 한다.
- **감사표의 과거 상태:** `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` TorrentSpirit 행은 `1 @ 0.50s / 동일`, `MigrationRequired`라고 기록한다. 새 설정 적용 전의 상태로 취급한다.

## 2. 실제 동작의 경계

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`는 `HitFrameTimes`마다 서버 공격 실행을 예약한다. TorrentSpirit은 `TorrentAttackBehavior`가 기본 단일 공격을 대체하고 파도 생성 요청을 하므로, `hitFrameTimes` 교정은 파도 **발사 예약 시점**에 영향을 준다. 단순 VFX 지연값만 바꾸는 작업이 아니다. 근거: `GameSystemRules_Units.md` 규칙 27~29.
- 발사 뒤 `ActiveWave`의 전선은 서버 `TickWaves`에서 `_waveTravelTime` 동안 전진하며 대상별 첫 접촉에 적 유닛·건물 피해 또는 아군 유닛 회복을 적용한다. 이 접촉 시각은 공격 시작으로부터 항상 `70/30초`로 고정되는 값이 아니다. 파도 너비·길이, 각 대상 1회, 아군/적 분리와 서버 권위는 그대로 둔다. 근거: 규칙 28~30, `NET-DELIVERY-TRAVELING`.
- `OnAttackHit`은 로컬 이펙트/발사 표식이며 직접 피해를 만들지 않는다. 발사 표식과 서버 예약은 일치시킬 대상이지만, 접촉별 HP 변화나 텍스트까지 같은 프레임이라고 주장할 수 없다. 근거: `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001/002`, `GameSystemRules_Units.md` 규칙 29·31.
- `UnitAttackShadowProfileResolver.cs`는 TorrentSpirit을 `TravelingArea / ActiveTravelingArea / Unresolved`로 분류하며 이유는 `traveling-area-sequence-unresolved`다. 대상별 sequence/contact ID 이관이 끝나지 않았으므로 이번 시점 교정만으로 `Supported`로 승격하지 않는다. 근거: `NET-DELIVERY-TRAVELING`, `UnitCombatAssetMatrix.md`.

## 3. 영향 범위와 남은 확인

| 영역 | 현재 상태 | 이번 변경의 의도 |
|---|---|---|
| 사용자 저장 Attack 클립 | 사용자 확정 `2:10`; 이번 문서 작업에서는 미열람 | 재검사·수정하지 않음 |
| `UnitStatsConfig.asset` unitType 18 | 발사 예약 `0.5초`, 쿨다운 `4초` | 발사 예약만 `70/30초`; 쿨다운 유지 |
| `SpecialAttackConfig.asset` | 발사 후 전선 이동 `0.5초` | 유지 |
| `StatsReference.md` | 혼재된 옛 표기 `0:50`, 임시 `0.5s` | 현행 `2:10(30fps)`과 초 환산, 발사/접촉 구별 |
| production 연결·자동 검증 | TorrentSpirit 전용 정확한 클립/Controller/Blue·Red 프리팹/설정 결합 게이트는 추가 조사·구현 대상 | 단일 마커와 설정 일치, 실제 생산 경로 확인을 제안 |
| 서버 wave·멀티 결과 | 서버 전선·접촉별 피해/힐 유지, full sequence/contact migration 미완 | 게임플레이 경로 보존, `Unresolved` 유지 |

기존 구현의 출처는 `Assets/_Project/Docs/_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/{Research,Plan}.md`이며, 당시 수치·미구현 설명을 현행 상태로 소급 해석하지 않는다. 실제 플레이에서의 발사·접촉 타이밍, 양 클라이언트 표현, 빌드 완료는 아직 이 작업의 검증 증거가 아니다.

## 4. 2026-09-30 구현 후 확인 — 위 조사 당시 상태와 구분

이 절은 위의 `0.5초` 설정과 미구현 게이트 기록보다 나중에 확인된 현재 상태다. 사용자 저장 Attack 클립을 이 문서 작업에서 다시 열거나 수정하지 않았다.

- `Assets/_Project/Resources/Config/UnitStatsConfig.asset`의 type 18 `hitFrameTimes`는 단일 `2.3333333초`로 바뀌었고 `attackCooldown: 4`는 유지된다. `Assets/_Project/Resources/Config/SpecialAttackConfig.asset`의 발사 **후** 전선 이동 시간 `0.5초`는 별도 값이다.
- `Assets/_Project/Scenes/Game.unity`의 type 18 생산 항목에 `attackTimelineClip` 명시 연결이 추가됐다. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`에는 TorrentSpirit production gate가 추가되어 설정·실제 Attack state·Controller·Blue/Red 프리팹·Game 생산 항목을 함께 검증한다. 이 게이트는 접촉별 피해/힐 실기를 증명하지 않는다.
- 메인 세션이 변경 후 Unity에서 새로 실행한 `[UAS-DIAG][PRODUCTION-TIMELINE]`은 TorrentSpirit launch를 포함해 PASS했고, `[UAS-ROOT-CROSS-AUDIT]` self-validation도 PASS했다. 문서 정합성 검사도 문제 0건이다. 이는 **자동 게이트 통과**이지 사용자 게임 실기 PASS가 아니다.
- `File > Build And Run`은 클릭했으나 Unity가 `Missing Project ID ... not a member ... this build will not access Unity services. Do you want to continue?` 경고창에서 사용자 선택을 기다리고 있다. 아직 `Yes`를 누르지 않았으므로 **Android 빌드 파이프라인 시작·완료 모두 미확인**이다.
- `TravelingArea`의 회차/대상별 접촉 ID 이관과 실제 화면 발사·대상별 피해/회복 시각은 여전히 미검증이다. `Unresolved / LegacyFallback`과 `MigrationRequired`를 유지한다.
