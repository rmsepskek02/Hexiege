# Plan — RuneSpire 번개: 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋난다

## 이 문서는 무엇인가

정령 방어탑 RuneSpire의 번개가 **유닛을 죽이는 순간에는 끝이 유닛에서 살짝 벗어나는** 결함을 고치는 계획이다. 조사 결과는 같은 폴더 [Research.md](Research.md)에 있다.

쉽게 말하면 이렇다. 번개는 맞은 유닛의 가슴 높이를 겨눈다. 유닛이 살아 있으면 그 유닛의 실제 키에 맞춘 가슴 높이를 쓴다. 그런데 **번개가 그려지기 시작할 때 유닛이 이미 죽어 있으면**, 코드는 그 유닛이 누구였는지 모른 채로 **모든 유닛에 같은 0.6m 높이**를 쓴다. 실제 유닛 가슴 높이는 약 0.18~0.32m라서, 번개 끝이 약 0.3~0.4m 위로 뜬다. 이것은 코드와 기존 테스트 로그로 확인한 사실이다. 그 밖에 「서버가 기록한 위치와 화면 위치가 옆으로 다른가」 같은 다른 원인도 있을 수 있어, **진단 로그를 함께 넣어 잰다.**

이 계획은 두 가지 진행 방법을 제안한다(1절).
- **안 A:** 진단 로그만 먼저 넣어 원인을 확정한 뒤 고친다(테스트 2회).
- **안 B (추천):** 이미 확정된 원인(0.6m 고정)을 고치는 수정 F0와 진단 로그를 **한 번에** 넣고, 테스트 1회로 「고친 결과」와 「남은 원인이 있는지」를 함께 본다.

어느 쪽이든 **화면 표현만** 바뀐다. 피해 · 타겟 선택 · HP · 쿨다운 · 서버 스냅샷 · 통신 형식 · 스키마는 바꾸지 않는다. 이 결함이 사용자 테스트를 통과하면 이전 VFX 작업에서 주석으로 꺼 둔 옛 코드 6곳을 최종 삭제한다(최상단 표).

> **현재 상태 (2026-11-10): ✅ 완료 — 구현(`b495ce0`) · 사용자 테스트 PASS(멀티 1경기 로그 판정 + 사용자 육안) · 옛 주석 블록 8곳 최종 삭제(`fa3934c`) · 문서 반영.** F1은 진행하지 않는다. 커밋 · 로그 판정 · 결정 · 삭제 · 남은 것은 [12절](#12-implementation-and-user-test). *(2026-11-10 갱신 — 종전: 「Plan 승인 — 구현 중. 2026-11-10 사용자 결정(원문 「추천대로 진행해」): 안 B(F0 + 진단 로그 함께, 테스트 1회) · 진단 로그 `[BOLT-END]` 존속 `개발`(유지). 주석 블록 6곳 최종 삭제는 테스트 통과 뒤 실행 전 사용자 확인, F1은 결과를 본 뒤 질문 — 10절. 규칙 문서는 무변경(9절은 테스트 통과 뒤).」 그 전: 「제안 — 사용자 승인 대기 …」)*

**표기:** `[사실]` = Research에서 확인한 것 · `[제안]` = 결정 아님 · `[추천 — 메인 세션]` = 메인 세션 추천(결정 아님) · `[사용자 결정]` = 10절에서 사용자가 정할 것. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Editor/` = `Assets/Editor/`. 행 번호는 HEAD `c28d0d7` 기준(Research).

---

> **기존 로직 제거 — WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 최상단에 둔다.**
>
> **① 이번 수정에서 새로 꺼 두는 것 (안 B를 고를 때만):** F0는 기존 동작에 「유닛 종류를 미리 잡아 두었으면 표 가슴 높이를 쓴다」를 **덧붙이는** 방식이 원칙이다. 기존 줄을 바꿔야 하면 지우지 않고 **주석으로 꺼 두고** 새 줄을 옆에 둔다. 해당 후보는 둘이다 `[제안 — 실제 대상은 game-programmer가 구현 때 보고]`:
>
> | 대상 | 처리 | 안전 근거 |
> |---|---|---|
> | `Scripts/Presentation/Effects/EffectManager.cs` 702~703행의 가슴 높이 인자 `ResolveBoltChestHeight(boltTarget.Data)` (바꿔야 할 때만) | 주석 비활성화 → 「대상 데이터가 없으면 미리 잡아 둔 유닛 종류로 표 조회, 그것도 없으면 0.6m」로 대체 | 표현 전용 값이다(규칙 12 「바꾸지 않는 것」 — 추적 위치 · 끝점은 어떤 판정의 입력도 아니다, NET-AUTH-001). 종류를 모르는 경우는 지금과 똑같이 0.6m라 기존 동작이 그대로 남는다 |
> | `Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 1720~1726행 검증 (e)의 기대값 | (e)의 「종류 모름 → 0.6m」 단언은 **유지**. 종류를 아는 고정 갈래의 기대값이 바뀌면 옛 단언을 주석으로 꺼 두고 새 단언을 추가 | 검증 강도를 낮추지 않는다 — 0.6m 대체 경로 단언은 그대로 남고 표 가슴 높이 단언이 더해진다 |
>
> **② 이전 VFX 작업의 주석 블록 6곳 — 최종 삭제 계획.** 2026-10-15 사용자 결정: **이 결함이 사용자 테스트를 통과한 뒤에만** 최종 삭제한다. 시점은 WORKFLOW [4] 「[6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전」이다. 안 B면 이번 작업의 테스트 통과가 곧 그 조건이다(안 A면 두 번째 테스트 통과 뒤).
>
> | # | HEAD 위치 (Research 6절) | 꺼 둔 옛 로직 | 대체 로직 | 대체 로직이 사용자 테스트를 통과한 기록 |
> |---|---|---|---|---|
> | R-1 | `Scripts/Presentation/Effects/VfxPoolItem.cs` 170~173행 | Bloom 「파티클 정확히 5개」 거부 | 1개 이상 + 시스템별 규격 검사 | [옛 Research 10-2](../../2026-10-13/13_53_vfx-code-followups/Research.md) A행 「힐 이펙트가 다시 재생된다 — PASS」 · [옛 Plan 13-1](../../2026-10-13/13_53_vfx-code-followups/Plan.md) A행 |
> | R-5 | 같은 파일 233~237행 | Bloom 대상 `lossyScale` 곱셈 | 크기 표의 균등 배율 | 옛 Research 10-2 R3행 「재측정 뒤 화상 · 독 · 힐 크기 · 높이가 의도대로 조정된다 — PASS」 · 옛 Plan 13-1 C행 |
> | R-4 | `Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` 58~60행 | 화상 · 독 고정 배율 | 프리셋 배율 × 대상 맞춤 균등 배율 | 옛 Research 10-2 R3행 · 옛 Plan 13-1 C행 |
> | R-3 | `Editor/Tools/Combat/RunUnitActionSelfValidation.cs` 1422~1424행 | 검증기 개수 5 단언 | 실제 프리팹에서 센 개수(1개 이상) 단언 | 옛 Research 10-2 A행(4개 프리팹 재생) · 이후 회귀 스위트는 사용자 무언급 PASS 규칙(WORKFLOW 「[6] 사용자 테스트」) — 결과 파일 없음 |
> | R2-X1 | `Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` 204~213행 (표식 `[R2-2 비활성화`) | 번개 XZ 평면 조준 · 길이 | 3D 조준 · 길이 | 옛 Research 10-2 R2-2 ①③행 「크리스털에서 시작 · 살아 있는 대상 가슴 높이 — PASS(사망 순간 제외)」 · 옛 Plan 13-1 R2-2행 |
> | R3 | `Editor/Tools/UnitRecipientFitMeasureTool.cs` 332~333행 | 스케일 1을 강제한 `TRS` 배치 행렬 | `localToWorldMatrix` | 옛 Research 10-2 R3행 · 옛 Plan 13-1 R3행 「수정 · 재측정 · 실기 확인」 |
>
> - 6곳은 모두 주석이라 지워도 실행 코드는 바뀌지 않는다. 삭제 뒤에는 주석만 빠졌는지(동작 코드 무변경) 확인한다 — WORKFLOW 「검수 · 판정 작업과 수치 보고의 절차」 6번(주석 · 문자열을 구분한 본문 해시 대조) `[제안]`.
> - R2-X1은 바로 이 번개 경로의 옛 계산이다. 이번 결함이 해결돼야 3D 조준 전체가 수용됐다고 볼 수 있어 2026-10-15 결정이 이 결함을 삭제 조건으로 걸었다.
> - 삭제는 사용자 테스트 통과 후 **사용자에게 확인을 받고** 실행한다(WORKFLOW 「규칙」 — 삭제 관련 명령은 실행 전 명시 허가).
>
> *(2026-11-10 — ① · ② 모두 실행 완료. ①은 실제로 검증기가 아니라 호출 2곳(`EffectManager.cs` · `HitPresentationQueue.cs`)이 `[F0 비활성화` 블록이 됐고, ② 6곳과 함께 **8곳 · 7파일 · 주석 36줄**을 `fa3934c`에서 삭제했다(사용자 원문 「삭제하고 갱신해서 메인에 병합하도록 하자 또한 푸시도 해줘」). 상세 · 삭제 범위 밖으로 남은 블록 1곳은 [12-5](#12-5-final-deletion))*

---

<a id="0-rules"></a>
## 0. 근거로 읽은 규칙

WORKFLOW [4]에 따라 `Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고 건물 행의 원본을 열었다. 「타워 규칙 N」 = [`GameSystemRules/GameSystemRules_Buildings.md`](../../../GameSystemRules/GameSystemRules_Buildings.md) **「방어 타워 시스템」** 절 규칙 N(건물 문서는 절마다 번호가 다시 시작한다).

| 규칙 | 이 계획과의 관계 |
|---|---|
| 타워 규칙 9 「서버 권위 처리」 | 타겟 선택 · 사거리 · 피해는 서버에서만 — **이 계획은 건드리지 않는다.** 진단 · F0 모두 각 화면의 표현 코드다 |
| 타워 규칙 12 「타워 발사 연출」 2026-10-13 블록 「추적 불가로 시작할 때」(214행) | 「재생 시작 때 원래 대상을 찾지 못하면 추적 없이 서버 스냅샷의 Impact 위치를 끝점으로 쓴다」 — **F0는 이 문구를 지킨다**(끝점 위치는 그대로 스냅샷 Impact). F1을 택하면 이 문구를 바꿔야 해서 **사용자 승인 전에는 넣지 않는다** |
| 같은 블록 「끝점 높이」(216행) | 「발밑 + 가슴 높이. 가슴 높이 = 크기 표의 키 × 비율(기본 0.6). **표에 그 유닛이 없으면 0.6m.**」 — **F0의 근거.** 지금 코드는 대상이 죽으면 표에 있는 유닛에게도 0.6m를 준다(Research 2-4) → F0는 코드를 이 문구에 맞춘다. 문구 변경 없음 |
| 같은 블록 「바꾸지 않는 것」(218행) | 서버 권위 · 즉발 피해 · 쿨다운 · 범위 · 공격 결과 식별 · **스냅샷 · RPC** 불변 — 이 계획은 서버 스냅샷 · RPC를 바꾸지 않는다(F2 · F3 제외, 채택하지 않음) |
| 같은 블록 「상태」(219행) | 「⚠️ OPEN — 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋난다」 — 사용자 테스트 통과 뒤 갱신할 줄(9절) |
| [`GameSystemRules_UnitCombatSynchronization.md`](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001 | 표현은 결과의 입력이 아니다 — 끝점 · 가슴 높이는 어떤 판정에도 쓰이지 않는다 |
| 같은 문서 「10. 현재 구조에서의 전환 규칙」 5항 | RPC 모양이 바뀌면 스키마를 올린다 — **이 계획은 RPC를 바꾸지 않으므로 스키마 8 유지** |
| **진단 로그 작성 규칙** — [LogRules](../../../LogRules.md) §1.2 · §1.4 · §1.5 · §1.7 · §1.14 | 2절 진단 로그의 형식 · 존속 · 스트리핑 · 금지 사항 |

---

<a id="1-approach"></a>
## 1. 진행 방법 — 안 A와 안 B `[사용자 결정]`

| | **안 A — 진단 먼저** | **안 B — F0 + 진단 동시** `[추천 — 메인 세션]` |
|---|---|---|
| 1회차 구현 | 진단 로그만(동작 변경 0) | F0(0.6m 고정 → 표 가슴 높이) + 같은 진단 로그 |
| 1회차 테스트로 아는 것 | 실제 어긋남의 수직 · 수평 성분, 원인(H0 · H1 · H4) | **수정 뒤 수직 차가 사라졌는가**(로그 `ChestDeltamm` ≈ 0 + 육안) + 남은 수평 차(H1)가 있는가 + H4 빈도 |
| 2회차 | 원인에 맞는 수정 → 다시 테스트 | 수평 어긋남이 남았을 때만 F1을 다음 회차로(🔴 규칙 문구 변경 → 사용자 승인 필요) |
| 사용자 테스트 횟수 | 최소 2회 | 최소 1회 |
| 규칙 문구 변경 | 원인에 따라 생길 수 있음 | **없음** — F0는 216행 문구에 코드를 맞추는 수정 |
| 장점 | 고치기 전 상태를 측정해 둘 수 있다(수정 전 기준선) | 이미 코드 · 로그로 확정된 결함 H0을 먼저 고친다. 나머지 원인(H1 · H4)은 같은 로그로 판정된다. 고치기 전 기준선도 진단 로그의 `ChestTablemm`(표 값)과 0.6m를 같은 줄에서 비교할 수 있어 잃지 않는다 |
| 단점 | 이미 확정된 결함을 알면서 한 번 더 미룬다 | 수정 전 상태의 **육안** 비교는 없다(로그 비교로 대신). Client에서 받는 순간에 유닛 정보가 이미 없으면 그 경우는 F0로도 0.6m가 남는다(3-2) — 로그로 드러난다 |

**결정 (2026-11-10):** 사용자가 **안 B**를 골랐다 — 원문 「추천대로 진행해」(10절 Q1).

**추천 근거 `[추천 — 메인 세션]`:** 원인 후보 가운데 코드와 로그로 확정된 결함(H0 — 「죽는 순간」이 실제로 0.6m 고정 갈래로 들어간다, Research 3절)을 먼저 고치고, 확정되지 않은 원인(H1 · H4)은 같은 진단 로그로 판정한다. 규칙 문구 변경이 없어 사용자 승인 항목이 늘지 않는다.

- F1(화면 위치 사용)은 규칙 12 「추적 불가로 시작할 때」 문구 변경이라 **어느 안에서도 이번 회차에 넣지 않는다.** 결과를 본 뒤 필요하면 별도로 제안한다.
- F2 · F3는 채택하지 않는다(Research 5절 — 스냅샷 · RPC 불변 원칙). 단 3-2의 「Client에서 종류를 못 찾는 경우」가 로그로 많이 드러나면 F3(스키마 8 → 9)를 별도 설계 후보로 다시 올린다.

---

<a id="2-diagnostics"></a>
## 2. 진단 로그 설계 `[BOLT-END]` `[제안]`

안 A · 안 B 공통. **동작을 바꾸지 않는다** — 읽기와 기록만 한다.

### 2-1. 공통

- 형식: `GameLog.Dev.Info("Combat", nameof(HitPresentationQueue), "[BOLT-END] <Event>", "Diag=TowerBoltEnd, DiagRev=1, Event=<Event>, …")` — 기존 `[TOWER-RANGE]` · `[EMP-DIAG]`와 같은 모양. 값은 `FormattableString.Invariant`로 쓴다.
- 축(LogRules §1.2): 심각도 `Info`, 존속 제안 **`개발`**(진단은 에디터에서 재현 가능 → §1.2 축 B 1번 「아니오」). `GameLog.Dev`라 `[Conditional("UNITY_EDITOR")]` · `[Conditional("DEVELOPMENT_BUILD")]`가 이미 붙어 있다(§1.7) — 기기 개발 빌드에서도 남는다. 콜백 안에 넣을 때는 블록 본문 람다로 쓴다(§1.7 「람다에 넣으면 스트리핑이 무효」). 존속은 **`개발`(유지)로 사용자가 정했다**(2026-11-10, 10절 Q2). *(2026-11-10 갱신 — 종전: 「존속은 테스트 뒤 사용자가 정한다(WORKFLOW [4]).」)*
- 대상: `Visual=Tower`이면서 그 종족 공격 프로필이 번개(Beam)인 스냅샷만(지금은 Spirit RuneSpire뿐 — `TowerAttackNeedsTargetTracking`). **인간 대포탑 · 덩굴 타워는 0줄이 정상.**
- 🔴 **자체 `try/catch` 필수:** Host · 싱글에서 받는 순간(①)은 서버 `ExecuteTowerAttack` 안에서 동기 실행된다(Research 2-1) — 진단 예외가 피해 · 사망 처리를 막으면 안 된다. 실패하면 `Event=Fault` 1줄(경기당 1회)만 남기고 넘어간다.
- 값 표기: 좌표는 밀리미터 정수, 축은 `|`로 잇는다(예: `Impactmm=1234|150|-5678`) — 값 안에 `, `를 넣지 않는다(§1.4). 키 이름 · 값 표기는 아래 목록대로 고정한다.
- 좌표계: 비교는 **화면에 그리는 좌표계**에서 한다. Client 반전 화면이면 Impact를 `ToView`로 바꾼 뒤 화면 위치와 비교한다(재생 쪽 402~403행과 같은 변환). 원래 값은 `Impactmm`에 그대로 남긴다.
- 회차 구분: `Diag=TowerBoltEnd, DiagRev=1`. 안 B면 `Fix=F0`, 안 A면 `Fix=None`을 모든 줄에 넣어 회차 로그가 섞이지 않게 한다.

### 2-2. 이벤트와 키

| 이벤트 | 남기는 자리 · 시점 | 키 |
|---|---|---|
| `Receive` (①) | `HitPresentationQueue.ReceiveAttackEffect`(377~389행)에서 목록에 넣은 직후 — 스냅샷 1건당 1줄 | `Role`(`Single`/`Host`/`Client`) · `ViewFlipped` · `Match` · `Lifetime` · `Sequence` · `TargetId` · `ResultHp` · `Lethal`(`ResultHp<=0`) · `AliveAtReceive`(`true`/`false`/`Unknown`) · `ViewAtReceive`(`Present`/`Missing`/`Mismatch`) · `UnitType`(모르면 `Unknown`) · `TypeSource`(`View`/`Domain`/`None` — 화면 오브젝트의 데이터에서 찾았는지, 도메인 유닛 목록 `_unitSpawn.GetUnit`에서 찾았는지) · `Impactmm` · `RecvViewmm`(`PresentationPosition`, 없으면 `None`) · `RecvPosemm`(VisualRoot `Pose.position`, 없으면 `None`) · `HDiffmm`(화면 좌표 Impact ↔ `RecvViewmm` 수평 거리) · `VDiffBasemm`(같은 두 점의 높이 차) · `ChestTablemm`(그 종류의 표 가슴 높이, 모르면 `None`) |
| `Begin` (②) | `EmitAttackEffects`(390~438행)에서 `PlayAttackEffect`가 돌아온 직후 — 1건당 1줄 | 같은 키 3개(`Match` · `Lifetime` · `Sequence`)와 `TargetId` · `Role` · `ViewFlipped` · `Lethal` · `UnitType` · `Branch`(`Tracked`/`Untracked`) · `Reason`(`Untracked(…)`의 사유, 추적이면 `None`) · `AliveAtBegin` · `ChestUsedmm`(실제로 끝점에 더한 가슴 높이) · `ChestTablemm` · `ChestDeltamm`(`ChestUsedmm − ChestTablemm`, 표 값을 모르면 `None`) · `ChestSource`(`Table`/`Fallback`) · `EndYmm`(끝점 Y) · `EndHDiffmm`(끝점 ↔ 그 순간 화면 위치의 수평 거리, 화면 오브젝트가 없으면 `None`) · `RecvToBeginMs` |
| `Limit` | 상한을 처음 넘은 순간 1줄 | `DiagnosticLimit=<상한>` · `Kind=Lethal`/`Control` · `Completeness=Unknown` |
| `End` | 경기 종료(`CloseAttackEffects`, 368~375행) 1줄 | `Received` · `Begun` · `NotStarted`(받고 재생 못 한 수) · `Tracked` · `UntrackedByReason`(사유별 개수를 `TargetDead:n` 꼴로 세로선으로 이은 값) · `LethalUntracked` · `NonLethalDeadAtBegin`(H4 수) · `ChestSourceTable` · `ChestSourceFallback` · `FallbackKnownType`(종류를 아는데 0.6m를 쓴 수 — 안 B면 0이 정상) · `TypeUnknownAtReceive` · `MaxAbsChestDeltamm` · `MaxHDiffmm` · `Omitted` |

- 키별 임시 보관: ①에서 잡은 `UnitType` · 받은 시각 · 받는 순간 값은 `(Match, Lifetime, Sequence)` 키로 보관했다가 ②에서 꺼내고 지운다. 경기 종료 · 재초기화 때 비운다(Research 7절 누수 위험).
- 받는 순간 `PresentationPosition` 읽기는 `ProjectNow`를 부른다 — 같은 입력이라 무해하다는 판단이며 `[전달 — 판단]`, 구현 때 game-programmer가 다시 확인한다.

### 2-3. 상한 (LogRules §1.14 금지 8 취지)

- 경기당 **치명 · 고정 갈래**(`Lethal=true` 또는 `Branch=Untracked`) 상세 **128건**, **비치명 추적 대조군**(살아 있는 대상) **32건**. `Receive` · `Begin` 한 쌍을 1건으로 센다.
- 넘으면 `Limit` 1줄만 남기고 상세를 멈춘다. 그 뒤는 `End`의 개수에만 반영한다(`Omitted`).

---

<a id="3-fix-f0"></a>
## 3. 수정 F0 — 고정 갈래에서도 표 가슴 높이 (안 B일 때) `[제안]`

### 3-1. 방법

1. **받는 순간(①)에 대상 종류를 잡는다.** 화면 오브젝트가 있으면 그 `UnitView.Data.Type`, 없으면 도메인 유닛 목록(`HitPresentationQueue`가 이미 들고 있는 `_unitSpawn`, 311 · 539행)에서 ID로 찾는다. 대상이 이미 죽은 상태여도 종류는 읽을 수 있다(Host · 싱글은 434행 발행 시점에 도메인 제거(460행) 전이다 `[사실 — 호출 순서]`).
2. 키별로 보관한 종류를 재생(②)으로 넘긴다.
3. 번개 고정 갈래(대상 참조 없음)에서 가슴 높이를 정할 때 **종류를 알면 `UnitEffectConfig.GetChestHeight(종류)`**, 모르면 지금처럼 **0.6m**. 추적 갈래는 지금 그대로(`ResolveBoltChestHeight(boltTarget.Data)`).
4. 끝점 위치는 지금처럼 **스냅샷 Impact**를 쓴다(규칙 12 「추적 불가로 시작할 때」 그대로). 바뀌는 것은 높이뿐이다.
5. 검증기: `RunUnitActionSelfValidation.VfxConnections.cs` (e)의 「종류 모름 → 0.6m」는 유지하고, **새 자가 검증 케이스**를 더한다 — 「대상 참조 없음 + 종류를 앎 → 스냅샷 Impact + 표 가슴 높이」, 「종류를 알지만 표에 없음 → 0.6m」. 기대값이 바뀌는 기존 단언은 **같은 커밋에서** 고친다(최상단 ①).

넘기는 방법(재생 함수에 인자를 더할지, 별도 조회를 둘지)은 game-programmer가 구현 때 정해 보고한다. 이미 공개된 메서드 모양을 바꾸면 그 호출부 전부(검증기 포함)를 같은 커밋에서 맞춘다.

### 3-2. F0가 고치지 못할 수 있는 경우 `[추정 — 미검증]`

Client에서 사망 처리 RPC가 공격 표현 RPC보다 먼저 도착해 **받는 순간에 이미 화면 오브젝트와 도메인 데이터가 모두 없으면**, 종류를 알 길이 없어 0.6m가 남는다(스냅샷에는 종류가 없다 — Research 2-1). 그 빈도는 `Receive`의 `TypeSource=None` · `End`의 `TypeUnknownAtReceive`로 드러난다. 많으면 F3(RPC에 종류 추가, 스키마 8 → 9)를 별도로 제안한다 — 이번 범위에는 넣지 않는다.

---

<a id="4-files"></a>
## 4. 변경 파일 (예상 — 최종 목록은 game-programmer 보고로 확정)

| 파일 | 안 A (진단) | 안 B (진단 + F0) |
|---|---|---|
| `Scripts/Presentation/Effects/HitPresentationQueue.cs` | ● 받는 순간 캡처 · 재생 순간 기록 · 키별 보관 · 상한 · `End` | ● 위 + 종류 보관 · 재생으로 전달 |
| `Scripts/Presentation/Effects/EffectManager.cs` | ● 진단용 읽기 전용 가슴 높이 조회 | ● 위 + 고정 갈래 가슴 높이 결정 |
| `Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` | 무변경 | 무변경 예상(가슴 높이는 인자로 받는다) — 바꿔야 하면 보고 |
| `Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | 무변경 | ● (e) 기대값 확인 · 새 케이스 |
| 사용자 테스트 통과 뒤 — 6곳 최종 삭제 | `VfxPoolItem.cs` · `VfxPoolItem.TimedDamage.cs` · `RunUnitActionSelfValidation.cs` · `VfxPoolItem.TowerBolt.cs` · `UnitRecipientFitMeasureTool.cs` (주석만) | 같음 |

---

<a id="5-test"></a>
## 5. 사용자 테스트 절차 (초안) `[제안]`

게임 규칙과 대조한 전제 `[사실 — .claude/mistakes.md 「사용자 테스트 안내」 항목의 대조 기준]`: 플레이어는 종족을 하나만 고르고 탑도 그 종족 것만 짓는다 → **RuneSpire를 보려면 정령으로 플레이해야 한다.** AI는 방어탑을 짓지 않는다 → 싱글에서는 「내 RuneSpire vs AI 유닛」이다. 싱글 · Host는 Blue 시점이다.

- **RuneSpire 한 방으로는 아무 유닛도 죽지 않는다** — 공격력 150, 가장 약한 유닛도 체력 200(FoxMagician · RabbitTrickster)이다 `[사실 — StatsReference.md]`. 「죽는 순간」은 **두 번째 이상의 번개**다. 체력 300 유닛(Pistoleer · Sniper · TideSpirit · StreamSpirit)과 200 유닛(FoxMagician · RabbitTrickster)은 연구 전이면 정확히 두 방에 죽는다. AI가 어떤 유닛을 보낼지는 정할 수 없으므로, **탑이 오래 쏘게 두어 사망 장면이 여러 번 나오게** 한다.
- 내 유닛을 많이 같이 싸우게 하면 번개가 아니라 내 유닛이 막타를 치는 경우가 늘어난다 — 탑이 주로 막타를 치게 하려면 탑 앞에 내 유닛을 적게 둔다(필수 아님).

| # | 모드 | 절차 | 눈으로 볼 것 |
|---|---|---|---|
| S1 | 싱글 · 정령 | RuneSpire를 성 앞(적이 오는 쪽)에 1~2개 짓고 AI 유닛이 들어오게 한다. 몇 분 플레이 | 번개가 **유닛을 죽이는 순간에도** 끝이 유닛 몸통(가슴)에 꽂히는가 · 살아 있는 유닛을 따라갈 때도 이전처럼 정상인가 |
| S2 | 멀티 — Host(에디터) · Client(기기) | **Client 쪽 플레이어가 정령으로 RuneSpire**를 짓고, Host 쪽 유닛이 그 탑 사거리로 들어가게 한다. 가능하면 Host도 정령으로 RuneSpire를 지어 Client 유닛을 쏘게 한다 | Client 화면(반전 · Red 시점)과 Host 화면 각각에서 S1과 같은 것 |
| S3 | 에디터 | `Hexiege/Combat/Run Full Regression Suite` 실행 | 실패가 없는가(사용자가 실패를 말하지 않으면 PASS — WORKFLOW 「[6] 사용자 테스트」, 결과 파일 없음 · Console만) |

- 🔴 **반전 화면 조건 메모 `[사실 — 코드]`:** Client 화면에 그려지는 번개는 **누구의 탑이든** 반전 변환을 거친다(`HitPresentationQueue.cs` 402~403행은 Client · 반전이면 모든 타워 스냅샷을 바꾼다). 그래서 반전 시점 관측에 꼭 Client가 정령일 필요는 없고, **Client 화면에 RuneSpire 번개가 보이기만 하면 된다**(Host가 정령이어도 된다). S2는 양쪽을 모두 보도록 「Client 정령 + 가능하면 Host도 정령」으로 적었다.
- 사망 장면은 순식간이라 육안으로 놓치기 쉽다 — 판정은 6절 로그로 한다. 사용자는 **이상하게 보인 장면만** 알려 주면 된다(WORKFLOW 「[6] 사용자 테스트」).
- 메인 세션은 테스트 안내 끝에 **「로그 올리기: 작업명 `runespire-bolt-death-endpoint`」** 를 적는다(WORKFLOW 「[6] 사용자 테스트」 — task 폴더 이름에서 시각을 뺀 부분). 사용자가 이전 보관본 이름에 숫자만 바꿔 올릴 수도 있다(공용 메모리 2026-11-10 사용자 선호) — 로그를 찾을 때 그 패턴도 확인한다.

---

<a id="6-test-log-map"></a>
## 6. 테스트 항목 ↔ 진단 로그 대응표 (WORKFLOW [4] 「테스트 항목 진단 로그 규칙」)

| 테스트 항목 | 로그 이름(카테고리 · 이벤트 키) | 남기는 시점(상태 변화) | 정상 판정 값(`key=value`) |
|---|---|---|---|
| T1 「죽는 순간」이 고정 갈래로 들어가는지 (S1 · S2) | `Combat` · `[BOLT-END] Begin` | 재생 시작 1회 | `Lethal=true`인 줄의 `Branch=Untracked`, `Reason=TargetDead`(Host · 싱글) / `TargetDead`·`TargetViewMissing`(Client) — 이 자체는 정상(규칙 12 「추적 불가로 시작할 때」) |
| T2 죽는 순간 끝점 높이 = 표 가슴 높이 (안 B, S1 · S2) | `[BOLT-END] Begin` · `End` | 재생 시작 · 경기 종료 | `Branch=Untracked`이고 종류를 아는 줄: **`ChestSource=Table`, `ChestDeltamm` 절댓값 ≤ 1** · `End` **`FallbackKnownType=0`**. 안 A면 같은 줄에서 `ChestDeltamm`이 약 280~424 → 원인 H0 확정 |
| T3 수평 어긋남 (H1) | `[BOLT-END] Receive` `HDiffmm` · `Begin` `EndHDiffmm` · `End` `MaxHDiffmm` | 받는 순간 · 재생 시작 · 종료 | Host · 싱글: `HDiffmm` ≈ 0(정확한 허용값은 결과를 보고 정한다 — 지금 기준 없음). Client는 이동 중 차이가 나올 수 있다 — **크기를 재는 항목**이며 값이 크면 F1 제안 근거 |
| T4 Red(반전) 시점 (H3) | `Receive` · `Begin`의 `ViewFlipped=true` 줄 | — | `ViewFlipped=true`와 `false`의 `HDiffmm` · `ChestDeltamm` 분포가 같은 수준 |
| T5 살아남은 결과인데 재생 때 사망 (H4) | `Begin` `Lethal=false, AliveAtBegin=false` · `End` `NonLethalDeadAtBegin` · `Receive` `AliveAtReceive` | — | 판정 항목이 아니라 **원인 관측**. 안 B면 이 줄도 `ChestSource=Table`이어야 한다 |
| T6 Client에서 받는 순간 종류를 아는가 (3-2) | `Receive` `TypeSource` · `End` `TypeUnknownAtReceive` | 받는 순간 · 종료 | `TypeSource=View` 또는 `Domain`. `None`이 있으면 그 수가 F3 판단 근거 |
| T7 살아 있는 대상 추적 회귀 없음 (S1 · S2) | `[BOLT-END] Begin` 대조군 + 기존 `[Combat/AttackEffectIdentity] Stage=Play … Endpoint=Tracked` | 재생 시작 | `Lethal=false, Branch=Tracked, ChestSource=Table, ChestDeltamm=0` |
| T8 인간 대포탑 · 덩굴 타워 무변 | `[BOLT-END]` 전체 | — | `Race`가 Spirit가 아닌 줄 **0줄**(대상이 번개 프로필뿐) |
| T9 육안 — 끝이 유닛 몸통에 꽂혀 보이는가 | — | — | 🔴 **로그로 판정 불가** — 로그는 숫자(높이 차 · 수평 차)까지만 보여 준다. 「보기에 맞는가」는 사용자 육안 |
| T10 피해 · HP · 사망이 바뀌지 않았다 | 기존 `[Combat/AttackEffectIdentity] Stage=ServerConfirm` `Applied` · `HP` | — | 🔴 **새 로그로는 판정 불가** — 이 계획은 서버 코드를 바꾸지 않으므로 변경 범위(4절 파일 목록)로 보장한다. 기존 줄의 `Applied=150` 등이 이전 회차와 같은 모양인지 보조 확인만 |
| T11 회귀 스위트 (S3) | — | — | 🔴 **로그로 판정 불가** — 결과는 Console에만 나오고 RuntimeLog에 자체 검증 줄이 없다(WORKFLOW 「[6] 사용자 테스트」). 사용자 보고로 판정 |
| 종합 | `[BOLT-END] End` | 경기 종료 1회 | 안 B: `FallbackKnownType=0` · `Fault` 0줄 · `Limit`이 있으면 `Omitted` 수 확인 |

- 받기 줄만 있고 재생 줄이 없는 건은 경기 종료로 재생 전에 닫힌 것일 수 있다(`End` `NotStarted`) — 결함이 아니다(Research 7절).

---

<a id="7-risks"></a>
## 7. 위험

| # | 위험 | 대응 `[제안]` |
|---|---|---|
| R1 | Host · 싱글에서 받는 순간 코드가 서버 공격 흐름 안에서 돈다(Research 2-1) — 예외가 피해 · 사망 처리를 막을 수 있다 | 진단 · F0 캡처 모두 자체 `try/catch`. 실패 시 0.6m(지금 동작)로 떨어지고 `Fault` 1줄 |
| R2 | 키별 보관이 비워지지 않아 쌓인다 | 재생 · 경기 종료 · 재초기화 때 비운다. `End` 직후 보관 수 0 확인 |
| R3 | 진단 로그 폭주 | 2-3 상한 |
| R4 | Client에서 받는 순간 종류를 못 찾아 F0가 일부 경우를 못 고친다(3-2) | 로그로 빈도를 재고, 필요하면 F3를 별도 제안 |
| R5 | 수직은 고쳐졌는데 사용자가 본 어긋남이 수평이었다(방향 미전달 — Research 8절 1번) | T3 로그로 수평 차를 함께 잰다. 남으면 F1을 사용자 승인 후 다음 회차로 |
| R6 | 받는 순간 `PresentationPosition` 읽기가 VisualRoot를 다시 쓴다 | 같은 입력 · 같은 값이라 무해하다는 판단 `[전달]` — 구현 때 재확인 |
| R7 | 6곳 최종 삭제 때 주석이 아닌 줄을 함께 지운다 | 삭제 뒤 주석 · 문자열을 구분한 본문 해시로 동작 코드 무변경 확인(최상단 ②) |

---

<a id="8-unchanged"></a>
## 8. 바꾸지 않는 것

- 피해량 · 타겟 선택 · 사거리 · 쿨다운 · HP · 사망 처리(타워 규칙 9).
- 서버 스냅샷(시작점 · Impact · 결과 HP) 생성과 그 시점, RPC 모양, 전투 표현 스키마 **8**.
- 번개 끝점의 **수평 위치**(고정 갈래 = 스냅샷 Impact, 추적 갈래 = 화면 위치) — 규칙 12 「추적 불가로 시작할 때」 그대로.
- 추적 갈래의 가슴 높이 계산, 시작점(크리스털), 3D 조준 · 길이.
- 인간 대포탑 · 덩굴 타워의 공격 표현.
- 크기 표(`UnitEffectConfig.asset`) 값과 가슴 높이 비율 0.6.

---

<a id="9-docs-after-pass"></a>
## 9. 사용자 테스트 통과 뒤 반영할 문서 `[제안 — 이번 구현에서는 고치지 않는다]` *(2026-11-10 — 반영 완료 → [12-6](#12-6-docs))*

- `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 2026-10-13 블록 「상태」 줄(219행) — OPEN 해소 갱신. 안 B면 「끝점 높이」 · 「추적 불가로 시작할 때」 문구는 변경 없음.
- `ROADMAP.md` 「VFX 후속 ①」 행 — 닫히면 행을 내린다(공용 메모리 「ROADMAP is a to-do list」).
- `PROJECT_STATUS.md` · `WORK_HISTORY.md` · 공용 메모리 · game-programmer 메모리(`vfx-presentation.md` §4-1) — WORKFLOW [9]~[11].
- 이 Plan 끝에 구현 · 테스트 결과 절 추가.

---

<a id="10-decisions"></a>
## 10. 사용자 결정 *(2026-11-10 결정 완료 — 종전 제목: 「사용자 결정 대기」)*

아래 질문 표는 작성 당시 그대로 두고, 결정은 그 아래 표에 적는다.

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| Q1 | 진행 방법 | 안 A(진단 먼저, 테스트 2회) / 안 B(F0 + 진단 동시, 테스트 1회) | **안 B** `[추천 — 메인 세션]` — 1절 근거 |
| Q2 | 진단 로그 `[BOLT-END]` 존속 | `개발`(남김 — 개발 빌드 · 에디터에만) / `임시`(작업 끝나면 코드 제거, LogRules §1.14 금지 10) | **`개발`** `[제안]` — EMP `[EMP-DIAG]` · 대포탑 `[TOWER-ARC]` 선례. 최종 결정은 테스트 뒤 |
| Q3 | 주석 블록 6곳 최종 삭제 시점 | 이 결함 테스트 통과 직후([7] 전) — 2026-10-15 결정 그대로 | 결정 그대로. 삭제 실행 전 사용자 확인만 받는다 |
| Q4 | F1(화면 위치 사용 — 규칙 문구 변경) | **이번에는 넣지 않는다.** 테스트 결과로 수평 어긋남이 확인되면 그때 묻는다 | 결과 후 판단 |

**결정 — 2026-11-10, 사용자 원문 「추천대로 진행해」** (위 표의 「추천」 열을 그대로 채택):

| # | 결정 | 이 Plan에서 뜻하는 것 |
|---|---|---|
| Q1 | **안 B** — F0 + 진단 로그 함께, 테스트 1회 | 1회차 구현 = 2절 진단 로그 + 3절 F0. 최상단 ①의 비활성화 후보가 적용 대상이 된다. 로그 모든 줄에 `Fix=F0` |
| Q2 | 진단 로그 `[BOLT-END]` 존속 **`개발`**(유지) | 작업이 끝나도 코드를 지우지 않는다(`GameLog.Dev` — 에디터 · 개발 빌드에만). LogRules §1.14 금지 10(`임시` 제거 의무)은 해당 없음 |
| Q3 | Plan에 적힌 그대로 — 이 결함 테스트 통과 직후([7] 전), **삭제 실행 전 사용자 확인** | 최상단 ②. 안 B라 이번 테스트 통과가 곧 삭제 조건이다 |
| Q4 | Plan에 적힌 그대로 — F1은 이번에 넣지 않고 **결과를 본 뒤 질문** | 수평 어긋남(T3)이 남으면 그때 사용자에게 묻는다 *(2026-11-10 결과 — F1 진행 안 함. 사용자에게 따로 묻지 않았다: 메인 세션 판단 + 사용자 육안 PASS → [12-4](#12-4-decisions))* |

---

## 11. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-10 (KST · 폴더 이름은 UTC 2026-11-09 19:52) | 최초 작성 — 사용자 승인 「task 문서 작성해」. 안 A / 안 B 제안, 승인 대기 |
| 2026-11-10 | 사용자 결정 기록 — 원문 「추천대로 진행해」. Q1 안 B · Q2 `개발`(유지) · Q3 · Q4는 Plan대로(10절 결정 표). 상단 현재 상태 → 「Plan 승인 — 구현 중」, 1절 결정 줄 · 2-1 존속 문장 갱신. 코드 · 규칙 문서 무변경 |
| 2026-11-10 | 완료 기록(WORKFLOW [7]~[11], 사용자 원문 「삭제하고 갱신해서 메인에 병합하도록 하자 또한 푸시도 해줘」) — 12절 추가(커밋 · 계획과 달라진 것 · 로그 판정 · 결정 · 최종 삭제 · 문서 반영 · 남은 것). 상단 현재 상태 → 완료, 최상단 ① · ② · 9절 제목 · 10절 Q4 행에 날짜 조각 |

---

<a id="12-implementation-and-user-test"></a>
## 12. 구현 · 사용자 테스트 결과 (2026-11-10)

쉽게 말하면: 번개가 유닛을 죽이는 순간에도 이제 그 유닛의 실제 가슴 높이에 꽂힌다. 고치기 전에는 그 순간에만 모든 유닛에 0.6m를 써서 끝이 약 0.3~0.4m 위로 떴다. 사용자가 멀티 1경기로 확인했고 로그로도 높이 차이 0이 나왔다. 남은 수평 차이는 최대 약 6.6cm이고 사용자 눈에는 문제가 없어 규칙 문구를 바꾸는 F1은 하지 않는다. 이전 VFX 작업과 이번 작업에서 주석으로 꺼 둔 옛 코드 8곳을 지웠다.

<a id="12-1-commits"></a>
### 12-1. 커밋

| 커밋 | 내용 |
|---|---|
| `b495ce0` | F0 + 진단 로그 + 자가 검증(안 B, 1절). 파일: `Scripts/Presentation/Effects/HitPresentationQueue.cs`(받는 순간 대상 종류 잡기 · 키별 보관 · `[BOLT-END]` Receive / Begin / Limit / End / Fault) · `Scripts/Presentation/Effects/EffectManager.cs`(`PlayAttackEffect`에 선택 인자 `boltCapturedTargetType` · 고정 갈래 가슴 높이 `ResolveCapturedBoltChestHeight` · 읽기 전용 시작 기록) · `Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs`(자가 검증 F0-1~F0-5). 같은 커밋에 이 Research · Plan |
| `b3f2a3a` | 병합 — 위 커밋과 원격 브랜치(`e6921b4`)를 합친 것 |
| `fa3934c` | 옛 주석 블록 8곳 최종 삭제(12-5) — 7파일 · **주석 36줄만 삭제, 추가 0줄** `[확인 — git diff]` |
| (이 기록) | 이 문서 반영(규칙 · 상시 문서 · 메모리) — 메인 세션이 커밋 · 병합한다 |

<a id="12-2-plan-diff"></a>
### 12-2. 계획과 달라진 것 `[확인 — HEAD 코드]`

| 항목 | 계획 | 실제 |
|---|---|---|
| 종류 전달 방법(3-1 3번 · 4절) | game-programmer가 정함 | `EffectManager.PlayAttackEffect`에 **기본값이 있는 선택 인자** `UnitType? boltCapturedTargetType = null`을 더했다 — 기존 호출부는 그대로 컴파일된다. 종류를 찾는 순서는 화면 오브젝트(`View`, ID 일치 때만) → 도메인 목록(`Domain`) → 못 찾으면 보관하지 않음(→ 0.6m) |
| 최상단 ① 비활성화 대상 | `EffectManager.cs` 가슴 높이 인자 · 검증기 (e) 기대값 | **검증기 (e)는 바꾸지 않았다**(0.6m 단언 유지). 대신 옛 호출 **2곳**이 `[F0 비활성화` 주석 블록이 됐다 — `EffectManager.cs`의 `BeginTowerBolt(… ResolveBoltChestHeight(boltTarget.Data))` 호출, `HitPresentationQueue.cs`의 종류를 넘기지 않던 `PlayAttackEffect` 호출. 둘 다 `fa3934c`에서 삭제 |
| 새 자가 검증(3-1 5번) | 2케이스(종류 앎 → 표 값, 표에 없음 → 0.6m) | **F0-1~F0-5 다섯 케이스** — F0-1 종류를 아는 고정 갈래(사유 `TargetDead` · `TargetViewMissing` 둘 다) → 표 값, F0-2 표에 없는 종류 → 0.6m, F0-3 종류 모름 → 0.6m, F0-4 추적 갈래는 넘겨받은 종류를 보지 않음, F0-5 실제 큐 순서(받는 순간 잡기 → 도메인 제거 → 재생 때 표 값, 보관 0으로 정리) |
| `Fault` 줄 심각도(2-1) | 모든 줄 `Info` | `Fault`만 **`Warn`**(`GameLog.Dev.Warn`), 나머지 `Info`. 존속은 그대로 `개발` |
| `End` 줄(2-2) | 경기 종료 1줄 | 경기 종료 외에 **종료 신호 없는 재초기화** 때도 지난 경기 `End`를 남긴다. 번개를 하나도 받지 않은 경기는 `End`를 남기지 않는다 |
| `Race` 키 | 6절 T8만 언급 | `Receive` · `Begin` 줄에 `Race=` 칸이 있다 |
| 정적 하네스 | — | 54 → 54(새 실패 없음) `[전달 — 메인 세션]` |

<a id="12-3-verdict"></a>
### 12-3. 사용자 테스트 판정 `[전달 — 메인 세션 로그 판정]`

- **로그 보관본:** `2026-11-10_12_41_runespire-bolt-death-endpoint_01` — ZIP · manifest SHA256이 GitHub 자산 digest · manifest와 일치(메인 세션 확인). 원시 로그는 비공개 보관소에만 있고 이 저장소에 옮기지 않는다(LogRules §1.10).
- **표본:** 멀티 1경기(Host = 에디터 · Blue, Client = 기기 · Red 반전 화면), 약 30초. 번개 쪽마다 12발 · 그중 치명 6발, 대상은 전부 TideSpirit. **싱글은 이번 로그에 없다**(12-4).

| 시나리오 · 항목 | 판정 | 근거 |
|---|---|---|
| T1 죽는 순간 고정 갈래 | PASS | Host 치명 6발 모두 `Branch=Untracked Reason=TargetDead` · Client 치명 6발 모두 `Reason=TargetViewMissing` |
| T2 죽는 순간 표 가슴 높이 | **PASS** | 양쪽 치명 6발 모두 `ChestSource=Table`, `ChestUsedmm=206=ChestTablemm`, `ChestDeltamm=0`(Client도 치명 6발 같은 값). 206mm = TideSpirit 표 키 0.3433(`UnitEffectConfig.asset` 314행) × 0.6 `[확인 — 표 값]` — 고치기 전이면 600mm로 394mm 위였다. Host `End` `ChestSourceFallback=0` · `FallbackKnownType=0` · `MaxAbsChestDeltamm=0` |
| T3 수평 어긋남(H1) | 측정 — 사용자 육안 문제 없음 | Host `MaxHDiffmm=0`. Client 받는 순간 서버 Impact ↔ Client 화면 위치 수평 차 `HDiffmm` 1~66(보통 19~29) — 고정 갈래 끝점의 수평 어긋남은 **최대 약 6.6cm** |
| T4 Red(반전) 시점(H3) | 이상 없음 | Client(Red · 반전)도 높이 차 0. 수평 차는 T3 값 |
| T5 살아남은 결과인데 재생 때 사망(H4) | **이번 표본 0건 — 원인 미확인으로 남김** | 3절의 옛 로그(Host 16건 · Client 10건)와 달리 이번 경기에서 생기지 않았다 |
| T6 Client 받는 순간 종류를 아는가(3-2) | PASS(이번 표본) | Client `TypeSource=View` 12/12, `AliveAtReceive=true` 12/12 — 받는 순간에는 화면 오브젝트가 아직 있었다(RPC 도착 순서 가정이 **이 표본에서** 성립). Host `End` `TypeUnknownAtReceive=0` |
| T7 살아 있는 대상 추적 회귀 없음 | PASS(Host) | Host 비치명 6발 `Branch=Tracked`, 차이 0. Client 비치명 줄의 판정은 전달된 판정에 없음 |
| T8 인간 대포탑 · 덩굴 타워 무변 | 전달된 판정에 없음 | 이번 경기 탑은 RuneSpire뿐이라 다른 탑 줄은 판정 대상이 없었다 `[추정 — 전달된 판정에 탑 구성 언급 없음]` |
| T9 육안 | PASS | 사용자 원문 「문제없어 보여」 |
| T10 피해 · HP · 사망 무변 | 범위로 보장 | 서버 코드 무변경(4절 · 12-1 파일 목록) |
| T11 회귀 스위트 | PASS(규칙) | 사용자가 실패를 말하지 않음 → PASS(WORKFLOW 「[6] 사용자 테스트」). 결과 파일 없음 · Console만 · 에이전트 미관측. **에디터 로그에도 스위트 픽스처 줄은 없었다.** `fa3934c` 삭제 뒤 스위트 재실행은 아직 안 함 |
| 종합 `End` | PASS(Host) | Host `ChestSourceFallback=0` · `FallbackKnownType=0` · `TypeUnknownAtReceive=0` · `MaxAbsChestDeltamm=0` · `MaxHDiffmm=0` · `Fault` 0 · `Limit` 0. Client `End` 값은 위 T2 · T6 항목 외에 전달된 판정에 없음 |

- **사용자 테스트 PASS** — 로그 판정(위 표) + 사용자 육안. 싱글 · 인간 대포탑 · 덩굴 타워 · H4는 이번 회차 관측 밖이다(12-7).
- **관측된 사실 — Client 탑 즉발 사망은 `TargetViewMissing`이다** `[전달 — 메인 세션 해석]`: 탑 번개는 즉발 피해라 사망 RPC와 오브젝트 파괴(`Despawn(destroy: true)`)가 같은 흐름에서 처리되고, 다음 `Update`의 재생 때는 화면 오브젝트가 이미 없다. 그래서 F0처럼 **받는 순간에 종류를 잡아 두는 것**이 Client 쪽을 고친 핵심이다.

<a id="12-4-decisions"></a>
### 12-4. 결정 `[전달 — 메인 세션]`

| # | 결정 | 근거 · 원문 |
|---|---|---|
| Q1 | 안 B | 10절(「추천대로 진행해」) |
| Q2 | `[BOLT-END]` 존속 **`개발`** — 코드를 지우지 않는다 | 10절. `GameLog.Dev` + `[Conditional("UNITY_EDITOR")]` · `[Conditional("DEVELOPMENT_BUILD")]` `[확인 — 코드]` |
| Q3 | 최종 삭제 **실행** | 사용자 원문 「삭제하고 갱신해서 메인에 병합하도록 하자 또한 푸시도 해줘」 → 12-5 |
| Q4 | **F1 진행 안 함**(규칙 12 「추적 불가로 시작할 때」 문구 그대로) | 남은 수평 차 최대 약 6.6cm(12-3 T3)에 사용자 육안 PASS — **메인 세션 판단**. 사용자에게 따로 묻지 않았다 |
| 싱글 테스트 생략 | **생략 확정** | 사용자 원문 「싱글도 따로 테스트를 해야 해? 멀티에서 성공적이면 된 거 아니야?」 → 메인 세션 근거: 싱글과 Host는 같은 받기 경로(`HitPresentationQueue.cs`의 `OnAttackEffectConfirmed` 동기 구독 — HEAD 949~950행, 작성 당시 `c28d0d7` 533~534행)와 같은 `TargetDead` 갈래를 타고 Host가 PASS했다 |

<a id="12-5-final-deletion"></a>
### 12-5. 옛 주석 블록 최종 삭제 — `fa3934c`

WORKFLOW [4] 「기존 로직 제거 규칙」의 「[6] 통과 후 · [7] 전」에 맞춰 이 문서 반영보다 먼저 삭제했다. 삭제된 줄 36줄이 모두 `//` 주석이고 추가된 줄은 0이다 — 실행 코드 무변경 `[확인 — git diff HEAD~1 HEAD]`.

| # | 파일 | 삭제한 블록 |
|---|---|---|
| R-1 | `Scripts/Presentation/Effects/VfxPoolItem.cs` | Bloom 「파티클 정확히 5개」 거부 |
| R-5 | 같은 파일 | Bloom 대상 `lossyScale` 곱셈 |
| R-4 | `Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | 화상 · 독 고정 배율 |
| R-3 | `Editor/Tools/Combat/RunUnitActionSelfValidation.cs` | 검증기 개수 5 단언 |
| R2-X1 | `Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` | 번개 XZ 평면 조준 · 길이 |
| R3 | `Editor/Tools/UnitRecipientFitMeasureTool.cs` | 스케일 1을 강제한 `TRS` 배치 행렬 |
| F0-1 | `Scripts/Presentation/Effects/EffectManager.cs` | 고정 갈래에 항상 0.6m를 주던 옛 `BeginTowerBolt` 호출 |
| F0-2 | `Scripts/Presentation/Effects/HitPresentationQueue.cs` | 잡아 둔 종류를 넘기지 않던 옛 `PlayAttackEffect` 호출 |

(F0-1 · F0-2는 위 표의 블록 이름이며 12-2의 자가 검증 케이스 F0-1~F0-5와 다른 것이다.)

- 🔴 **남은 비활성화 블록 1곳 — 후속 판단:** `Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 1645~1651행 `[2026-10-13 R2-2 비활성화]`(높이를 무시한 XZ 기대값 — 아래 3D 기대값으로 대체, 주석 7줄). 이전 작업의 6곳 표(옛 Plan 13-2 3번 · 13-4)에 없어 이번 삭제 범위에 들지 않았다. 지울지는 정하지 않았다 — 메인 세션 보고 사항. 코드 전체에서 `비활성화` 표식을 찾으면 이 1곳만 남는다 `[확인 — grep]`.

<a id="12-6-docs"></a>
### 12-6. 문서 반영 (9절 대응)

| 9절 항목 | 결과 |
|---|---|
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 「상태」 줄 | OPEN을 해소로 갱신(날짜 조각). 「끝점 높이」 · 「추적 불가로 시작할 때」 문구는 그대로 |
| `ROADMAP.md` 「VFX 후속 ①」 행 | 행 삭제(ROADMAP은 할 일 목록 — 공용 메모리 「ROADMAP is a to-do list」). 이력은 WORK_HISTORY · 이 Task |
| `PROJECT_STATUS.md` · `WORK_HISTORY.md` | VFX 표현 후속 행의 OPEN · 6곳 문구 갱신 / 완료 행 1개 |
| (추가) `Assets/VFXSFXList.md` | 머리 상태 줄 · RuneSpire 행의 OPEN 갱신 |
| (추가) 옛 Task `2026-10-13/13_53_vfx-code-followups/` | Plan 13-1 B · R2-2 행, 13-2 1 · 3번, 13-4 제목 · 6곳 표에 완료 조각. Research 10-4 제목에 완료 조각 |
| 공용 메모리 · game-programmer 메모리 | `.claude/MEMORY.md` 완료 1항목 · 2026-10-15 항목 상태 도장 갱신, `vfx-presentation.md` §4 · §4-1 · §6 갱신과 교훈 |
| `TechnicalDesignDocument*.md` | 변경 없음 — 번개 끝점 · 가슴 높이를 다루는 줄이 없다 `[확인 — grep]` |
| Testcase.md | 없음 — 요청받지 않아 만들지 않았다(WORKFLOW [5-1]). [8]은 해당 없음 |

<a id="12-7-remaining"></a>
### 12-7. 남은 것 · 미관측

- **H4**(결과 HP > 0인데 재생 때 이미 사망) — 이번 표본 0건, 원인 미확인.
- **싱글** — 이번 로그 없음. 생략은 결정(12-4)이며 관측이 아니다.
- **F3**(RPC에 종류 추가) — 필요 없었다(Client `TypeSource=View` 12/12). 단 표본은 1경기 · 대상 1종(TideSpirit)이다.
- **`fa3934c` 삭제 뒤 회귀 스위트 재실행** — 아직 안 함(삭제는 주석만이라 실행 코드 무변경).
- **남은 비활성화 블록 1곳**(12-5) — 후속 판단.
