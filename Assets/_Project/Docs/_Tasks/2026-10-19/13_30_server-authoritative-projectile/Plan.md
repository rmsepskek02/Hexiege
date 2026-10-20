# Plan — 서버 권위 투사체 (옵션 C)

> 작성 2026-10-19 · 기준 checkout HEAD `ab9d55d7` · 근거 조사: 같은 폴더 [Research.md](Research.md)
> **상태: 승인됨 — 구현 진행 · 단계 A(StreamSpirit만 ON) 구현 `d09cc4a9`, 사용자 육안 확인 + 서버 판정 로그 확인(scoped), 화면 표현 로그 미확인, 의심 1 후속 판단 대기** *(2026-10-20 5차 — 근거 [Research 12절](Research.md#12-stage-a-field-log-analysis-20261020). 아래 4차 조각의 「코드·에셋은 아직 바뀌지 않았다」는 4차 당시 사실이다)* *(2026-10-20 4차 갱신 — 사용자 승인 「나머지는 추천방식대로 진행해」. 1-3절 미결 전부 닫힘, 4-2절 개정안 F-1~F-9를 규칙 문서·`StatsReference.md`에 **승인된 목표 계약(미구현 표시)**으로 반영 — [4-2-3절](#4-2-3-규칙-문서-반영과-개정-전-원문). 코드·에셋은 아직 바뀌지 않았다. 종전 상태 「결정 반영 초안 — 남은 미결(1-3절의 「열림」 행) + 4-2절 개정안 승인 + Plan 승인 대기(구현 전)」. 아래 이 줄의 나머지는 2차 당시 문장이다)* Q1~Q17 결정이 모두 [1-1절](#1-1-사용자-결정-기록-2026-10-1920)·[1-1-2절](#1-1-2-추가-결정-2026-10-20-2차)·[1-1-3절](#1-1-3-mushroombomber-예외-철회-2026-10-20-3차)에 기록됐고(Q12는 2026-10-20 2차에 확정, Mushroom 예외는 3차에 철회), 남은 미결·확인 항목은 [1-3절](#1-3-남은-미결확인-항목)에 있다. *(2026-10-20 2차 갱신 — 1차 때는 「Q12를 뺀 결정 … 남은 미결·확인 U-1~U-12」였다)* 이것들이 닫히고 이 Plan이 승인되기 전에는 코드·에셋·규칙 문서·`StatsReference.md`를 하나도 바꾸지 않는다(WORKFLOW [4]). 규칙 개정 문안은 4-2절에 **승인 대기 개정안**으로만 두었다. *(2026-10-20 갱신 — 종전 상태 「초안 — 사용자 결정 대기(승인 전)」. 2·3·4·5·6·7절은 결정에 맞춰 고쳐 썼고, 2026-10-19 초안 원문은 커밋 `e5958406`의 이 파일에 그대로 있다. 1절 질문 표는 원문 그대로 두었다)*
> 갱신 기준 checkout HEAD `a11b5381`(2026-10-20 1차) → **`968e56bc`(2026-10-20 2차 — 사용자 커밋: `EagleArcher_Attack.anim` `OnAttackHit` 0.1 → 0.2초)**. 이번 회차에 새로 다시 읽은 행은 [확인]으로 적었다.
> 근거 표기는 Research와 같다: [확인] · [전달] · [추정] · [미확인]. 행 번호는 바뀔 수 있으므로 메서드 이름을 함께 적는다.
> 읽은 규칙 원본(WORKFLOW [4]): `GameSystemRules.md` 인덱스 · `GameSystemRules_Units.md`(「공격 회차 안정 ID」, 「전투 연출 동기화 규칙」 규칙 17~20, 「착탄형 DoT 확장」 규칙 38~40) · `GameSystemRules_UnitCombatSynchronization.md`(「6. 방향과 타겟 동기화」, 「7. 전달 방식별 동기화」, 「9. 취소·사망·중단」) · `GameSystemRules_Buildings.md`(「방어 타워 시스템」 규칙 12).

## 0. 이 계획이 무엇인가 (쉬운 설명)

지금은 물폭탄·화염구·마법탄·포탄·포자 폭탄이 **쏘는 순간에 이미 피해가 들어간다.** 화면의 투사체는 그 뒤에 날아가는 장식이라, 숫자·사망이 투사체보다 먼저 나오거나 투사체가 거의 안 보인다.

이 계획은 **서버가 투사체를 직접 관리**하게 바꾼다. 서버가 「지금 쐈다」를 기록하고, 거리 ÷ 속도만큼 지난 「닿는 순간」에 피해를 준다. 화면은 서버가 알려 준 발사·착탄 시각에 맞춰 투사체를 날리고, 닿는 순간에 착탄 이펙트와 피해 숫자를 함께 보여 준다. 싱글플레이에서도 같은 규칙으로 동작한다.

정할 것이 많다. 예를 들어 「투사체가 날아가는 동안 대상이 도망가면 따라가는가」, 「쏜 유닛이 비행 중에 죽으면 피해가 들어가는가」 같은 것이다. 이런 질문 17개를 1절에 모았다. **사용자가 정하기 전에는 구현을 시작하지 않는다.** 정해지면 2절의 순서대로, 한 유닛씩 바꾸면서 확인한다.

이 작업이 끝나면 앞선 작업의 남은 문제 — 움직이는 대상에게 쏘면 엉뚱한 자리에서 터짐(B2), 대상이 죽으면 중간에 이펙트가 나옴(B3), 큰 대상에 이펙트가 가려짐(B4), 여우 마법사 숫자가 먼저 뜸(L-1), 차지 취소 뒤 숫자만 뜸(L-2) — 이 함께 정리된다.

**2026-10-19~20 사용자 결정 뒤 (요약).** 사용자가 정한 큰 원칙은 이렇다. **공격을 시작하면 투사체가 눈으로 보이는 속도로 날아가고, 맞는 순간에 피해가 들어간다. 모든 판정은 서버가 한다.** 투사체는 적을 따라가 맞힌다(버섯 폭격기만 예외 — 쏜 순간의 그 자리를 공격한다 *(3차: 이 예외는 철회됐다 — 버섯 폭격기도 똑같이 대상에게 날아간다)*). 날아가는 동안 적이 죽어도 투사체는 그 자리까지 날아가 터지는 모습만 보여 주고 피해는 없다. 쏜 유닛이 날아가는 동안 죽어도 맞으면 피해가 들어간다. 반대로 **쏘기 전에** 정지 명령이나 타겟 교체로 공격이 끊기면 투사체도 소리도 피해도 없다. 다만 적이 사거리 밖으로 걸어 나간 것은 「끊김」이 아니라서 그대로 쏜다. 늦게 접속한 사람도 날아가는 중인 투사체를 똑같이 본다.
범위도 넓어졌다. 독수리 궁수(EagleArcher)의 화살과 **인간 방어타워(CannonTower)의 포탄**도 같은 방식의 투사체가 된다. 정령 방어타워(RuneSpire)의 번개는 투사체가 아니므로 지금처럼 즉시 피해를 준다. 결정 전체는 1-1절, 아직 정하지 못한 것은 1-3절에 있다.
**같은 날 2차 결정.** 큰 유닛·건물은 투사체가 **몸체 표면에 닿는 순간** 피해가 들어가고 착탄 이펙트도 그 표면에서 보인다. 쏘기 전에 이동 명령을 받거나 대상이 죽으면 쏘지 않는다. 인간 타워는 대포 입구에서 쏘고, 쏜 뒤 타워가 부서져도 포탄은 맞으면 피해를 준다. 독수리 궁수는 공격 동작 0.2초에 화살을 쏘며 착탄 이펙트는 없다. 버섯 폭격기는 쏜 순간 대상이 있던 자리를 노리며, 그 자리를 벗어난 적은 피해를 받지 않는 것이 메인 세션의 기본안이다(사용자 승인 대기). **3차(같은 날):** 사용자가 「복잡하니 예외를 두지 말자」고 정해 버섯 폭격기도 다른 투사체처럼 대상을 따라가 맞히고, 맞은 대상 위치를 중심으로 지금처럼 폭발·독을 준다. 앞 문장의 기본안은 폐기됐다.

---

## 🔴 기존 로직 제거·비활성화 예정 항목 (WORKFLOW [4] — 최상단 명시)

- 모두 **삭제가 아니라 주석 처리로 비활성화**하는 것이 기본이다. 최종 삭제는 [6] **C 사용자 테스트 PASS 뒤**, [7] 문서/메모리 업데이트 전에 한다.
- 「출처」 칸은 그 코드를 만든 Task다. 🔴 「R-1」 같은 번호가 두 Task에서 다른 코드를 가리키므로(Research 1절 주의), 이 Plan의 항목은 **RM-** 번호를 쓰고 옛 번호는 출처 Task 이름과 함께 적는다.
- 아래 항목 중 「결정 의존」이 붙은 것은 해당 질문이 추천대로 정해질 때만 실행한다. *(2026-10-20 갱신: RM-5~RM-8이 걸려 있던 Q7·Q8·Q9·Q13·Q17이 모두 정해져 네 항목 모두 **실행 확정**이다. RM-9~RM-11은 결정으로 새로 생긴 항목이다)*

| # | 대상 | 출처 | 처리 | 안전한 근거 |
|---|---|---|---|---|
| **RM-1** | 투사체 유닛의 「타격 순간 즉시 피해」 호출 — 싱글 `TickPendingHits` 만기의 `ApplyAttackDamageCore` 호출(1103행 [전달]), 멀티 `DelayedAttackDamage`의 `ApplyAttackDamageObserved`(1213행 [전달]) | 기존 구조 | **유닛별 스위치가 켜진 투사체 유닛에 한해** 발사 기록 생성으로 분기한다. 스위치가 꺼진 유닛과 비투사체 유닛은 기존 호출을 그대로 탄다. 기존 호출문은 지우지 않는다 | 피해는 착탄 시각에 같은 단일 writer(`ApplyAttackDamageCore` → `ExecuteAttack`)로 들어가므로 피해량·DoT·범위 계산은 같다(Research 4-3). 근거 규칙 `NET-DELIVERY-PROJECTILE` |
| **RM-2** | R5 임시 착탄 연결 — `EffectManager.cs` `PlayTracer`의 `playImpactOnArrive` 분기(621~632행), `ResolveLegacyArrivalImpact`(653행~), `PlayLegacyArrivalImpact`(669행~) [확인], 호출 쪽 `UnitView.cs` 7859~7862행 [전달] | vfx-code-followups 재작업 R5(Plan 18-2 R5-2) | 해당 유닛이 새 경로로 옮겨질 때 그 유닛의 호출을 주석 처리. 모든 대상 유닛 이행 후 메서드 전체 주석 처리 → C PASS 뒤 삭제 | R5는 처음부터 「C 방식 구현 시 교체」로 결정된 임시 경로다(vfx-code-followups Research 15-4). 남겨 두면 착탄 VFX 이중 재생(위험 2) |
| **RM-3** | R5 검증 — `RunUnitActionSelfValidation.VfxConnections.cs` 76행, `ValidateLegacyTracerArrivalImpact`(372~480행), `RunUnitActionSelfValidation.cs` 7508~7524 · 8276 · 8391~8451행 [전달] | vfx-code-followups 재작업 R5(R5-4) | 새 검증으로 대체 후 주석 보존 → C PASS 뒤 삭제 | RM-2가 없어지면 검증 대상이 없다 |
| **RM-4** | 차지 작업 주석 블록 R-1 · R-2 · R-3 · R-4 (`UnitView.cs` 7340 · 1157 · 7460 · 7766/7825/7863/7870행) · V-1 · V-2 · V-3 (`RunUnitActionSelfValidation.cs` 8155 · 8200/8254 · 7972행) [확인] | [차지 유닛 발사 시점 Task](../11_09_charge-unit-launch-timing/Plan.md#9-구현-결과-2026-10-19-추가) | **이미 주석 처리돼 있다.** C 사용자 테스트 PASS 뒤 **삭제**한다 | 사용자 결정 「C 작업 후 정리」 [전달 — 메인 세션]. 그 Task Plan 9-1은 「최종 삭제 미실행 — 사용자 지시 없었다」로 남아 있다 [확인]. Q16에서 재확인 |
| **RM-5** | FoxMagician 즉시 표시 분기 — `UnitCombatUseCase.cs` 1768~1769행 `immediatePrimaryPresentation` [확인] (+ 이를 요구하는 검증 7641 · 8074행 [전달]) | 기존 구조(규칙 19 예외) | **실행 확정 (Q7·Q8 결정, 2026-10-20 — 종전 「결정 의존」).** 주석 처리하고 투사체 키 경로로 통합 | 피해가 착탄으로 옮겨지면 즉시 표시 ≈ 착탄 시각이라 예외의 이유가 없어진다(Research 5-5). 규칙 19 개정 승인 필요(4절) |
| **RM-6** | 원거리 marker에서의 트레이서 발사 — `UnitView.cs` 원거리 marker 경로(7797~7867행 [전달]) | 기존 구조(규칙 20) | **실행 확정 (Q9 결정, 2026-10-20 — 종전 「결정 의존」).** 대상 유닛의 marker는 총구 VFX·SFX만 남기고 트레이서 발사는 주석 처리 | 트레이서는 서버 발사 기록을 받을 때 쏜다(marker·기록 경쟁 회피). 규칙 20 개정 승인 필요 |
| **RM-7** | 원거리 HP 숫자의 공격자별 FIFO 보류 — `HitPresentationQueue` 경로, 대상 유닛 한정 | 기존 구조 | **실행 확정 (Q7 결정, 2026-10-20 — 종전 「결정 의존」).** 투사체 키를 가진 결과는 FIFO를 우회해 단일 소비자로. FIFO 코드 자체는 다른 유닛이 쓰므로 그대로 | 빈 큐 신호 버림(`HitPresentationQueue.cs` 565~570행 [확인]) 때문에 숫자가 최대 6초 밀린다(위험 1) |
| **RM-8** | MushroomBomber 스냅샷 경로 — `AttackEffectPresentation` 생성(1732~1742행), `OnAttackEffectConfirmed` ClientRpc(`NetworkCombatController.cs` 60~86행), `HitPresentationQueue.cs` 197~256행, `EffectManager` Mushroom 고정 프리셋 조회(505 · 533행) [전달] | 기존 구조(Mushroom) | **실행 확정 (Q1·Q13·Q17 결정, 2026-10-20 — 종전 「결정 의존」). P4의 Mushroom 차례에** 새 경로로 옮긴 뒤 주석 처리(3차: 다른 투사체와 같은 **Homing** 경로 — 지점 공격 아님). 타워 표현(`AttackEffectVisual` Tower)은 **Spirit RuneSpire가 계속 쓰므로** 건드리지 않는다(인간 타워는 RM-10) | 착탄 뒤 1회 메시지라 발사·결과 분리가 안 된다(Research 3-1). 남겨 두면 이중 재생 |
| **RM-9** | 유닛별 이행 스위치(P1에서 새로 만드는 것) | 이 작업 | **2026-10-20 신설 (Q17 결정).** 대상 전체의 사용자 테스트가 끝나면 스위치를 제거하고 항상 새 경로로 동작하게 한다. 스위치가 꺼진 쪽의 옛 경로는 RM-1·RM-10과 함께 정리한다 | 사용자 결정 「모든 테스트 완료 전까지만 두고 완료 후 제거」 [전달 — 메인 세션] |
| **RM-10** | 인간 방어타워의 즉발 피해 — `TowerCombatUseCase.cs` 241행 `target.TakeDamage(damage)`와 그 뒤 Tower 스냅샷 발행(245~248행) [확인], 화면 쪽 Tower 표현 중 Human 분기 | 기존 구조(`GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 「즉발 데미지 유지」) | **2026-10-20 신설 (Q10 결정).** Race가 Human인 타워만 발사 기록 생성으로 분기하고 즉발 호출은 주석 보존. **Spirit RuneSpire·Transcendence VineTower 경로는 그대로** | 규칙 12 개정(4-2절, 승인 대기)이 먼저다. 타워는 유닛 전투 경로(`UnitCombatUseCase`)가 아니라 `TowerCombatUseCase`에서 피해를 주므로 P1 발사 기록이 건물 공격자를 받아야 한다 [확인 — 파일 분리 / 설계는 추정] |
| **RM-11** | `EagleArcher_Attack2.anim` (컨트롤러 미참조 클립) | 기존 에셋 | **2026-10-20 신설 → 4차: 사용자가 직접 삭제한다(U-11).** 이 작업의 어느 단계에서도 손대지 않는다(주석·삭제 대상 아님). 사용자가 커밋 `a11b5381`에서 컨트롤러의 Attack2 상태를 지웠고 파일만 남았다(Research 11절). *(종전 「정리 후보, 사용자 확인 필요 … 삭제 여부·시점은 사용자 지시로만 정한다」)* | 컨트롤러·프리팹·에셋·씬·코드 어디에서도 이 클립 GUID나 이름을 찾지 못했다(Research 11-1) |
| **RM-12** | `UnitEffectConfig._recipientFits`의 `radius`(Presentation 설정) | 기존 구조 | **2026-10-20 2차 신설 (Q12 결정).** 반경을 서버 권위 데이터로 **옮긴 뒤** Presentation 쪽 값을 남길지(다른 용도 — 상태 이펙트 크기 맞춤 등)·주석 처리할지는 game-programmer 설계에서 정한다 | 서버와 화면이 다른 반경을 읽으면 착탄 지점이 어긋난다(위험 17). 「옮겨 사용」은 사용자 결정 [전달 — 메인 세션] |

---

## 1. 사용자 결정 질문 Q1~Q17

> 🔴 **결정 전 구현 금지.** 아래가 모두 정해지고 이 Plan이 승인된 뒤에 P1을 시작한다.
> 「메인 추천」 칸은 game-programmer 조사 보고서의 제안을 메인 세션이 추천으로 전달한 것이다 [전달]. **Q1 · Q13 · Q15는 기획 판단이 필요하므로 game-design-lead 검토 후 사용자가 결정한다.**

| # | 질문 | 선택지 | 메인 추천 | 영향 | 관련 규칙 |
|---|---|---|---|---|---|
| **Q1** | 유닛별 대상 처리 방식 — **game-design-lead 검토 후 사용자 결정** | (가) LockedPoint(발사 지점 고정, 회피 가능) (나) Homing — 비행 시간은 발사 때 고정 (다) Homing — 매 틱 재계산 | Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician = (나), MushroomBomber = (가) Area | (가)는 회피라는 새 게임플레이. (나)는 지금(빗나감 없음)과 가장 가깝고 B2 해소 | `U-IMPACT-LOCKEDPOINT` · `U-IMPACT-INDEPENDENT` · `NET-FACING-002` |
| **Q2** | 투사체 속도를 어디에, 얼마로 | (가) `UnitStatsConfig` 서버 전용 필드 (나) 프리팹 `TracerProjectile._speed` (다) 고정 비행 시간 | (가), 초기값 = 현 프리팹 값(Stream 4 · Tank/Cannon 4 · Inferno 5 · Fox 5 · Mushroom 7) | (나)는 레이어 위반·피어 간 불일치 위험(Research 4-2). `StatsReference.md`에 열 추가 | `NET-DELIVERY-PROJECTILE` |
| **Q3** | 비행 중 공격자가 죽으면 | (가) 계속 진행 — `PersistsAfterSourceDeath`=true (나) 소멸 — false | (가) | (가)는 착탄용 진입점과 발사 때 공격력 저장이 필요(Research 4-6, 위험 3). (나)는 피해 손실 | `NET-CANCEL-003` · `U-IMPACT-INDEPENDENT` |
| **Q4** | 비행 중 대상이 (다른 원인으로) 죽으면 — B3 | 서버 피해 없음은 공통. 화면 (가) 투사체 소멸 (나) 마지막 위치까지 날아가 착탄 VFX(숫자 없음) | (나) | 사용자가 B3에서 제시한 두 안 그대로 | `NET-CANCEL-004` · `NET-CANCEL-005` |
| **Q5** | 사거리 재판정 | (가) 발사·착탄 모두 안 함 (나) 발사 때 (다) 착탄 때 | (가) | (가)가 지금 동작과 같다(Unresolved는 타격 순간 사거리 검사 없음 — Research 4-7) | `U-ATK-TIMELINE` 마지막 문단 |
| **Q6** | 공격 커밋 뒤 발사 전에 서버가 공격을 멈추거나 타겟을 바꾸면 | (가) 서버는 예정대로 발사하고 화면도 서버 기록으로 투사체를 보여 줌 (나) 발사 취소 | (가) — 차지 작업 L-2 해소 | (가)는 규칙 17 차지 계약 「발사 전에 끊기면 투사체를 내지 않는다」와 어긋난다 → 개정 필요 | 규칙 17 · `NET-CANCEL-005` |
| **Q7** | 피격 표시(숫자·피격 반응) 경로 | (a) 기존 ImmediatePresentation (b) 투사체 키 + 단일 소비자(파도 선례) | (b) | 빈 큐 신호 문제를 구조적으로 해결(위험 1). RM-7 | 규칙 19 · `NET-TIME-002`·`003` |
| **Q8** | FoxMagician 규칙 19 즉시 표시 예외 | (가) 유지 (나) 제거·통합 | (나) | 차지 작업 L-1 해소. RM-5. 규칙 19 개정 | 규칙 19 |
| **Q9** | 트레이서를 무엇에 맞춰 쏘는가 | (가) 로컬 `OnAttackHit` marker (나) 서버 발사 기록 수신 | (나), marker는 총구 VFX·SFX만 | RM-6. 규칙 20 개정 | 규칙 17 · 규칙 20 · `NET-DELIVERY-PROJECTILE` |
| **Q10** | 대상 범위 | EagleArcher 포함 여부 · 방어 타워 포함 여부 | EagleArcher 제외(공격 애니메이션 결정 전, tracer/impact preset 없음) · 타워 제외 | 타워를 넣으면 「즉발 유지」 규칙 개정 | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 |
| **Q11** | C3 분류 | (가) Unresolved 유지 + 서버 투사체 별도 경로(Torrent 선례) (나) Supported 승격 | (가), 승격은 별건. `PresentationContractSchemaRevision` 3 → 4 | 스키마를 안 올리면 구·신버전 혼합 경기(위험 5) | `U-ATK-TIMELINE` · `NET-PRESENT-002` · `NET-DELIVERY-TRAVELING` |
| **Q12** | 착탄 이펙트 위치 — B4 | (가) 서버 착탄점 그대로 (나) 화면에서만 대상 표면점(중심 → 공격자 쪽으로 반경만큼) | (나): 유닛 = `UnitEffectConfig` radius, 건물 = `BuildingEffectConfig`에 타입별 반경 신설 | 건물 반경 데이터 형식·값을 새로 정해야 함 | 규칙 변경 없음(화면 표현) |
| **Q13** | MushroomBomber LockedPoint의 직접 피해·범위 중심 — **game-design-lead 검토 후 사용자 결정** | (가) 고정 지점의 `blastRadius` 안일 때만 주 타깃 직접 피해, DoT 수집 중심도 고정 지점 (나) 지금처럼 착탄 시점 대상 위치 중심 | (가) | (가)는 규칙 38 「판정 중심 = 주 타깃 위치」 · 규칙 40 개정. 주 타깃이 도망가면 직접 피해가 빗나간다 | 규칙 38 · 39 · 40 · `U-IMPACT-LOCKEDPOINT` |
| **Q14** | 늦게 접속한 Client에게 비행 중 투사체 복원 | (가) P2 후반에 포함 (나) 제외(착탄 결과만) | (가) | 파도의 늦은 접속 스냅샷 선례(`NetworkCombatController.Waves.cs`) | `NET-TIME-005` |
| **Q15** | 이미 처치가 확정된 대상에게 비행 중 또 쏘는 문제 — **game-design-lead 검토 후 사용자 결정** | (가) 타겟 선택은 생존만 본다(현행) (나) 비행 중 예정 피해를 반영 | (가) 유지, 밸런스 영향은 game-design-lead 검토 | 첫 피해·처치가 0.25~0.7초 늦어짐, 낭비 사격 [추정] | — (밸런스) |
| **Q16** | 옛 코드 정리 시점 | R5 코드(RM-2·3) · 차지 작업 R-1~R-4 · V-1~V-3(RM-4)를 **C 사용자 테스트 PASS 뒤 삭제** | 기존 결정 재확인 | vfx-code-followups의 R-번호와는 출처로 구분 | WORKFLOW [4] |
| **Q17** | 이행 방식·순서 | (가) 유닛별 스위치로 하나씩 (나) 일괄 | (가): StreamSpirit → Tank/CannonCart → InfernoSpirit → FoxMagician → MushroomBomber | 한 유닛씩 실기 확인 가능, 문제 범위 축소 | — |

> 위 표는 2026-10-19 초안의 질문·선택지·추천 **원문 그대로**다(2026-10-20 결정 뒤에도 고치지 않았다). 결정은 아래 1-1절에 따로 적었다.

<a id="1-0-사용자-기본-원칙-2026-10-1920"></a>

### 1-0. 사용자 기본 원칙 (2026-10-19~20) [전달 — 메인 세션, 사용자 원문 취지]

1. 공격을 시작하면 투사체가 날아가 **맞았을 때** 피해가 적용된다.
2. 투사체 속도는 **눈으로 확인할 수 있는** 속도다.
3. **모든 것은 반드시 서버 권위**다(발사·위치·착탄·피해).
4. 비행 중에 적이 죽어도 투사체는 그 지점까지 날아가고, 착탄 이벤트도 그 지점에서 발생한다.
5. 투사체는 적을 따라가 적중한다(Homing). ~~예외는 MushroomBomber(1-1절 Q13).~~ *(2026-10-20 3차: 예외 철회 — **MushroomBomber 포함 모든 투사체가 Homing**, 1-1-3)*

<a id="1-1-사용자-결정-기록-2026-10-1920"></a>

### 1-1. 사용자 결정 기록 (2026-10-19~20)

> 출처: 메인 세션이 전달한 사용자 결정(원문 취지) [전달 — 메인 세션]. 「1절 표 선택지와의 대응」 칸은 document-manager가 결정 문장을 위 표의 선택지에 맞춰 본 것이며, 맞춘 근거가 결정 문장에 직접 없는 곳은 [추정]으로 적고 1-3절에 확인 항목으로 올렸다.

| # | 사용자 결정 | 1절 표 선택지와의 대응 | 메인 추천과 | 비고 |
|---|---|---|---|---|
| **Q1** | Homing(MushroomBomber 제외) **[🔴 2026-10-20 3차 — 사용자가 Mushroom 예외를 철회, 1-1-3]** — MushroomBomber도 Homing | (나) 또는 (다) — **둘 중 어느 쪽인지는 결정 문장에 없다** [1-3절 U-1] *(2026-10-20 4차: U-1이 (나) 발사 때 고정으로 닫힘)* | 일치(대상 쪽) | 대상: StreamSpirit · Tank · CannonCart · InfernoSpirit · FoxMagician · **EagleArcher · 인간 타워**(Q10). Mushroom은 Q13 |
| **Q2** | 속도를 **Inspector에서 조절**할 수 있게(서버 권위 데이터), 테스트로 튜닝. MushroomBomber는 지금 너무 빨라 조금 낮춰 시작 | (가) `UnitStatsConfig` 서버 전용 필드 — Inspector에서 고치는 ScriptableObject(`UnitStatsConfig.cs` 98~99행 `CreateAssetMenu` · `ScriptableObject` [확인]) | 일치. 단 초기값의 Mushroom 7은 **낮춘 값으로 변경** | 초기값·인간 타워·EagleArcher 속도는 미결 [1-3절 U-4] |
| **Q3** | 발사된 투사체는 공격자가 죽어도 맞으면 피해 | (가) `PersistsAfterSourceDeath`=true | 일치 | 착탄용 진입점·발사 때 공격력 저장 필수(위험 3). 인간 타워가 비행 중 파괴되는 경우도 같은 원칙으로 본다 [추정 — 결정 문장은 「공격자」, 1-3절 U-6] |
| **Q4** | 비행 중 대상 사망 → 마지막 지점까지 비행, **착탄 이펙트만**, 피해·피해 숫자 없음 | (나) | 일치 | 기본 원칙 4. 착탄 이펙트는 서버 결과(대상 소실) 기준으로 양쪽 화면 같은 지점 |
| **Q5** | 차지 도중 대상이 사거리 밖으로 나가도 이미 시작했으므로 **발사**, 맞으면 피해 | (가) 발사·착탄 모두 재판정 안 함 | 일치 | 「끊김」이 아니다(1-2절) |
| **Q6** | 발사 전(차지 도중) 공격이 **끊기면**(Stop·타겟 교체 등) 서버도 발사 취소 → 투사체·소리·피해 없음. 사거리 이탈(Q5)은 끊김이 아니라 발사. 대상이 발사 시점에 죽어 있으면 발사하지 않음(Q15) | (나) 발사 취소 | 🔴 **메인이 기존 추천 (가)를 철회**하고 사용자 원칙 「날아가 맞아야 피해」에 따름 | 끊김의 정확한 경계는 1-2절 — **메인 확인 필요** [1-3절 U-2]. 규칙 17·`NET-PRESENT-001` 개정 필요(4-2절) |
| **Q7** | 피해 숫자·착탄은 **서버 착탄 기준** | (b) 투사체 키 + 단일 소비자 | 일치 | 서버 권위 원칙에 따름 |
| **Q8** | FoxMagician 피해 숫자 즉시 표시 예외(규칙 19) **제거** — VFX가 없던 시절의 규칙이므로, 이제 착탄 시 표시로 통일 | (나) 제거·통합 | 일치 | RM-5. 규칙 19 개정(4-2절) |
| **Q9** | 트레이서는 **서버 발사 기록 기준** | (나) 서버 발사 기록 수신, marker는 총구 VFX·SFX만 | 일치 | RM-6. 규칙 20 개정(4-2절) |
| **Q10** | ① **인간 방어타워(CannonTower — HumanAutoTower)를 투사체 타워로 전환.** 투사체·착탄 이펙트는 CannonCart 것을 쓰고, 발사 이펙트는 지금 적용된 것(`EffectPreset_HumanAutoTower_Attack` → `vfx_pistoleer_attack`)을 쓰되 **위치 조정** ② **Spirit RuneSpire는 투사체 개념이 아니므로 즉시 피해 유지** ③ 건물 규칙 12는 **인간 타워만** 개정 ④ **EagleArcher는 화살 오브젝트를 투사체로 사용, 이번 범위 포함** | 선택지 밖(표의 추천 「EagleArcher 제외 · 타워 제외」를 **두 쪽 모두 뒤집음**) | 🔴 추천과 다름 | 프리셋 연결 [확인]: `EffectPreset_HumanAutoTower_Attack.asset` 15행 → `vfx_pistoleer_attack.prefab` GUID, `EffectPreset_Cannon_Tracer` → `vfx_cannon_tracer.prefab`, `EffectPreset_Cannon_Impact` → `vfx_cannon_impact.prefab`. Transcendence VineTower는 결정에 없으므로 **변경 없음**(규칙 12 「인간 타워만」). EagleArcher 사실은 Research 11절 |
| **Q11** | 분류를 **투사체 유닛 / 즉발 유닛**으로 나눈다 | 선택지 밖 — 기존 Supported/Unresolved와의 관계는 아래 「Q11 설계안」 | — | 설계안은 제안이며 승인 필요 |
| **Q12** | **확정 — 1-1-2절** *(2026-10-20 2차 갱신, 종전 「대기 — 사용자 질문 중」)*. 1차 당시 메인 설명 예정안: 서버가 비행 거리를 **대상 표면까지(중심 거리 − 대상 반경)** 로 계산해 피해 시점 = 화면상 접촉, 착탄 이펙트도 같은 표면 지점(양쪽 같은 계산) | 결정 전 | — | 메인 설명안은 원래 추천 (나) 「화면에서만 표면점」과 다르다 — **서버가 반경을 쓰는 안**이다. 이때 반경은 서버 데이터여야 한다: 지금 반경이 있는 `UnitEffectConfig`·`BuildingEffectConfig`는 Presentation 파일이다(`Scripts/Presentation/Effects/` [확인]). [1-3절 U-3] |
| **Q13** | **[🔴 2026-10-20 3차 — 사용자가 Mushroom 예외를 철회, 1-1-3]** ~~MushroomBomber는 특수 케이스 — **공격 시점에 그 지점을 공격**(LockedPoint 지점 공격). 대상을 공격하는 것이 아니다~~ → **예외 없음, Homing + 주 타깃 위치 기준 기존 Blast/독** | (가) 고정 지점 중심 [추정 — 「대상을 공격하지 않는다」를 (가)로 읽음]. 「공격 시점」= 발사 시점으로 읽음 [추정] | 일치(방향) | 규칙 38·40 개정(4-2절). 고정 시각과 「주 타깃 직접 피해」 처리 확인 [1-3절 U-5] |
| **Q14** | 투사체도 게임 오브젝트다 — **어느 순간이든 양 플레이어가 같은 화면**을 본다(늦은 접속 포함 비행 중 투사체 복원, 발사·위치·착탄 동기) | (가) 포함 | 일치 | P2 필수 범위(후반 선택 아님) |
| **Q15** | 발사 시점에 타겟이 **살아 있으면 발사**. 죽을 것을 예상해 공격하지 않는 것은 잘못 | (가) 타겟 선택은 생존만 본다(현행) | 일치 | 반대로 발사 시점에 이미 죽어 있으면 발사하지 않는다(Q6) |
| **Q16** | C 사용자 테스트 PASS 뒤 RM 항목 삭제(재확인) | 기존 결정 재확인 | 일치 | RM-2·3·4 (+5·6·7·8·10의 주석 보존분, RM-9 스위치) |
| **Q17** | 유닛별 켜고 끄는 스위치는 **모든 테스트 완료 전까지만** 두고 완료 후 제거 | (가) 유닛별 스위치 | 일치 + 스위치 제거 시점 추가 | RM-9. 순서에 EagleArcher·인간 타워 추가 필요 [1-3절 U-7] |

- **game-design-lead 검토:** 1절 머리말은 「Q1·Q13·Q15는 game-design-lead 검토 후 사용자가 결정」이라고 했다. 이번 결정은 사용자가 직접 내렸고, game-design-lead 검토가 있었는지는 전달받지 않았다 [미확인]. P0의 검토 단계를 둘지는 1-3절 U-8. *(2026-10-20 4차: U-8 닫힘 — 사용자가 직접 결정했으므로 검토 생략)*

**Q11 설계안 (제안 — game-programmer 검토 · 사용자 승인 대상, document-manager 작성)**

- 「투사체 / 즉발」은 **게임플레이 전달 방식** 분류다. 기존 Supported/Unresolved는 **C3 공격 회차 시퀀서(`CanRunSequencer`)가 그 유닛을 다루는가** 하는 다른 축이다(`U-ATK-TIMELINE` · `NET-PRESENT-002`). 두 축을 섞지 않는다.
- **투사체 유닛** = 서버 발사 기록 → 비행 → 착탄 때 피해. StreamSpirit · Tank · CannonCart · InfernoSpirit · FoxMagician · EagleArcher · MushroomBomber(지점 공격) + 인간 타워. C3 축에서는 1절 Q11 원래 추천 (가)대로 **Unresolved를 유지**하고(Torrent 선례 `NET-DELIVERY-TRAVELING`), 서버 투사체를 별도 경로로 둔다. Supported 승격은 별건.
- **즉발 유닛** = 공격 타격 순간에 피해. 근접·Hitscan 유닛, Spirit RuneSpire, Transcendence VineTower. 지금 동작 그대로.
- 분류가 놓일 자리: resolver의 `UnresolvedProjectile(...)` 행이 이미 투사체 7종을 가리킨다(`UnitAttackShadowProfileResolver.cs` 62~68행 [확인]). 새 분류를 이 행에서 읽게 할지, 별도 표를 둘지는 game-programmer가 정한다. 사유 문자열(예: EagleArcher 67행 「projectile-animation-selection-unresolved」)은 이 작업에서 갱신 후보다(Research 11-2).
- 스키마: 새 전송 계약이므로 `PresentationContractSchemaRevision` 3 → 4(원래 추천 유지, 위험 5).
- ✚ **설계 주의 — resolver의 MushroomBomber `LockedPoint` (2026-10-20 3차):** `UnitAttackShadowProfileResolver.cs` `UnresolvedProjectile` 167~169행은 `type == UnitType.MushroomBomber ? AttackTargetMode.LockedPoint : AttackTargetMode.TargetLocked`로 Mushroom만 LockedPoint로 만든다 [확인]. 사용자 결정으로 Mushroom도 Homing이 됐으므로 **이 값은 실제 게임플레이(대상 추적)와 다르다.** 처리 선택지: (a) 행을 TargetLocked로 바꾼다 — 25종 manifest 해시가 바뀌므로 스키마 상향(3 → 4)과 같은 커밋에서 처리, 이 값을 단언하는 검증 갱신 (b) C3 축 분류값으로만 남기고 새 투사체 경로는 이 값을 읽지 않는다 — 어긋난 값이 남아 이후 오독 위험. Q11 「투사체 유닛」 분류는 대상 처리 방식을 **모든 투사체 Homing**으로 두고, 어느 쪽을 고를지는 game-programmer 설계 + 메인 확인(1-3절 U-16). *(2026-10-20 4차: U-16 닫힘 — **(a)** TargetLocked로 변경, 스키마 3 → 4 상향과 같은 커밋에서. 구현 단계)* Research 4-4의 「Mushroom은 resolver상 LockedPoint지만 실제 범위 중심은 착탄 시점 대상 위치」 [전달]와도 이어진다.

<a id="1-1-2-추가-결정-2026-10-20-2차"></a>

### 1-1-2. 추가 결정 (2026-10-20 2차, HEAD `968e56bc`) [전달 — 메인 세션]

| # | 결정 | 닫는 항목 | 비고 |
|---|---|---|---|
| **Q12 / B4** | 서버가 비행 거리를 **대상 표면까지(중심 거리 − 대상 반경)** 로 계산한다 → 피해 시점 = 화면상 접촉. 착탄 이펙트도 **같은 표면 지점**(서버·Client 같은 계산). 유닛 반경은 `UnitEffectConfig._recipientFits`의 `radius`를 **서버 권위 데이터로 옮겨** 쓰고, 건물은 **종류별 반경을 새로 만든다** | U-3 | 1절 Q12의 (가)·(나) 어느 쪽도 아닌 **서버 표면 거리안**. `UnitEffectConfig`는 Presentation 파일이라(`Scripts/Presentation/Effects/UnitEffectConfig.cs` [확인]) 반경 값을 서버가 읽는 자리(Application/Infrastructure 데이터)로 옮겨야 한다 — 옮긴 뒤 Presentation 쪽 radius를 남길지·지울지는 game-programmer 설계(RM 후보) |
| **경계** | 발사 전후 상황표 확정 — 1-2절 | U-2 | 이동 명령 = 발사 취소, 차지 중 대상 사망 = 발사 취소 |
| **타겟 교체** | 「차지 중 타겟 교체」는 코드상 **독립적으로 생기지 않는다**(메인 확인, document-manager 재확인 [확인] — 1-2절 아래 근거) | U-2의 ★ 「자동 타겟 교체」 행 | 결국 사망(→ 취소) 또는 사거리 이탈(→ 원래 타겟에 발사) 둘로 귀결. 예외 조항 불필요 |
| **인간 타워 발사 위치** | 사용자 지시 「**대포 입구에서 발사**」. 위치는 메인/game-programmer가 프리팹 기준으로 정한다(사용자 조정 불필요). **발사 이펙트도 같은 위치** | (1차 Q10 「위치 조정」의 구체화) | `EffectPreset_HumanAutoTower_Attack` `_localPositionOffset` 지금 (0,0,0) [확인] |
| **속도 초기값** | Inspector 조절 · 서버 권위 데이터. **StreamSpirit 4 · Tank 4 · CannonCart 4 · 인간 타워 4 · InfernoSpirit 5 · FoxMagician 5 · EagleArcher 6 · MushroomBomber 5**(기존 프리팹 7에서 낮춤) | U-4(값) | 인간 타워 속도를 둘 자리(제안 `BuildingStatsConfig` 종족별 칸)는 설계 단계에서 확정 |
| **진행 순서** | **메인 재량**(사용자: 「순서는 알아서, 모두 완료하기만 하면 돼」) | U-7 | 2절 P4 순서는 제안으로 남긴다 |
| **EagleArcher 발사 시점** | `OnAttackHit` **0.2초**(30fps `0:06`) — 사용자 커밋 `968e56bc` [확인: `EagleArcher_Attack.anim` 20749행 `time: 0.2`]. 상시 문서의 0.1초 표기를 0.2초로 고치는 것을 사용자가 승인 → `StatsReference.md` 87·118행, `UnitCombatAssetMatrix.md` 115·133행 반영함 | U-9 | ⚠️ `UnitStatsConfig.asset` EagleArcher(type 24) `hitFrameTimes` fallback은 **0.1 그대로**다 [확인]. marker가 있으면 clip 값을 쓰므로(`UnitFactory.cs` 529~530행 `markerCount > 0` [확인]) 지금 동작에는 영향이 없지만 두 값이 다르다 — 에셋 수정은 이번 범위 밖, 메인 판단 필요 *(2026-10-20 4차: U-15 닫힘 — 0.2로 맞춘다, 구현 단계에서 game-programmer)* |
| **`EagleArcher_Attack2.anim`** | 컨트롤러 미참조(메인 GUID 확인 0건, Research 11-1과 같음) — **정리 후보로 기록** | U-11(기록) | 삭제는 사용자 지시가 있을 때만(RM-11) *(2026-10-20 4차: 사용자가 직접 삭제 — 이 작업에서는 손대지 않음)* |
| **인간 타워 비행 중 파괴** | **공격자 사망과 같다** — 이미 발사된 포탄은 계속 날아가 맞으면 피해 | U-6 | Q3(Persist=true)를 건물 공격자에게도 적용. 발사 때 공격력 저장 필요(위험 3과 같은 이유) |
| ~~**Mushroom 지점**~~ **[🔴 2026-10-20 3차 — 사용자가 Mushroom 예외를 철회, 1-1-3]** | 지점 = **발사 순간의 대상 위치로 고정**(지점 공격) | U-5(고정 시각) | 사용자 결정 |
| ~~**Mushroom 비켜난 주 타깃**~~ **[🔴 2026-10-20 3차 — 사용자가 Mushroom 예외를 철회, 1-1-3]** — **기본안 폐기** | ~~🔶 **메인 기본안 — 사용자 승인 대상(사용자가 명시하지 않음):** 「지점 공격」이므로 착탄 때 **그 지점의 `blastRadius` 안에 있는 적에게만** 피해·독을 준다. **주 타깃도 반경 밖이면 피해 없음**~~ | U-5(직접 피해 조건) — 승인 전까지 「기본안」 | 1절 Q13 (가)와 같은 방향. 규칙 38·39·40 개정안(4-2절)에 포함하고 승인 대상으로 표시 |
| **EagleArcher 화살** | 유닛 프리팹의 `EagleArrow` 메시를 **복사해 투사체 프리팹**으로 만들어 쓴다. **착탄 이펙트 없음**(impactPreset 비움) | U-10 | 손에 든 화살(자식 메시)을 발사 때 숨길지는 결정 문장에 없다 [1-3절 U-13] *(2026-10-20 4차: U-13 닫힘 — 그대로 둔다)* |

<a id="1-1-3-mushroombomber-예외-철회-2026-10-20-3차"></a>

### 1-1-3. MushroomBomber 예외 철회 (2026-10-20 3차 — 1-1 Q1·Q13과 1-1-2 Mushroom 두 행을 대체) [전달 — 메인 세션]

| 항목 | 결정 |
|---|---|
| 사용자 원문 | 「MushroomBomber의 예외를 처리하지 말자. 그냥 똑같이 대상에게 날아가도록 하자, 복잡하니까.」 |
| 대상 처리 | MushroomBomber도 다른 투사체 유닛과 같은 **Homing**(대상 추적). 지점 공격(LockedPoint) 없음 |
| 착탄 처리 | 착탄 때 **주 타깃 위치 기준**으로 기존 Blast/독 처리 — 규칙 38 「판정 중심 = 주 타깃 월드 위치」, 규칙 40 「착탄 때 한 번 수집」 유지 |
| 폐기되는 것 | 1-1 Q13 「공격 시점에 그 지점을 공격」, 1-1-2 「지점 = 발사 순간 대상 위치 고정」, 🔶 메인 기본안 「지점 반경 밖이면 주 타깃도 피해 없음」(U-5) |
| 규칙 영향 | 규칙 38·39·40 **개정 불필요**(4-2-1 F-6·F-7, 4-2-2 A-7~A-9). 419행 인용 블록(F-5/A-6)은 스냅샷 경로 교체(RM-8) 때문에 여전히 개정 대상 |
| 설계 주의 | resolver의 Mushroom `LockedPoint` 값(1-1 Q11 설계 주의, U-16) · 범위 중심은 표면 착탄점이 아니라 주 타깃 위치(4-2-1 아래 주의) |
| 그대로인 것 | 속도 초기값 Mushroom 5, 기존 스냅샷 경로 교체(RM-8), 독 DoT 시작 = 착탄 |

<a id="1-2-공격-끊김의-경계-q5q6q15"></a>

### 1-2. 공격 「끊김」의 경계 (Q5 · Q6 · Q15) — ✅ 확정 (2026-10-20 2차)

> *(2026-10-20 2차 갱신 — 1차 제목은 「🔴 메인 확인 필요」였고 ★ 세 행(이동 명령 · 시스템 자동 타겟 교체 · 사거리 이탈 뒤 추격/재선정)이 확인 대상이었다. 셋 모두 아래 표와 「타겟 교체」 근거로 닫혔다. 1차 표 원문은 이 회차 작업 중의 초안이라 따로 보존하지 않았고 닫힌 질문은 이 문단에 남긴다.)*
> 「발사 전」 = 공격 커밋 뒤, 서버 발사 시각(= `OnAttackHit` offset) 전 — 차지 유닛은 차지 중. 차지 VFX가 없는 투사체 유닛(Stream · Tank · CannonCart · EagleArcher · Mushroom · 인간 타워)의 발사 전 구간에도 같은 표를 적용하는 것으로 읽었다 [추정 — 결정 문장은 「차지 중」, 1-3절 U-14]. *(2026-10-20 4차: U-14 닫힘 — 사용자 승인으로 이 읽기가 확정됐다)*

| 상황 | 처리 | 근거 |
|---|---|---|
| 발사 전 — 대상 사망 | **발사 취소.** 차지 VFX를 끄고 다음 동작으로 이어간다(취소 전용 애니메이션 없음). 투사체·공격 소리·피해 없음 | 사용자 확정표 · Q15 |
| 발사 전 — 공격자 사망 | **발사 안 함** | 사용자 확정표 · `NET-CANCEL-003` |
| 발사 전 — 이동 명령 | **발사 취소** | 사용자 확정표 |
| 발사 전 — 정지 명령(StopCombat) | **발사 취소** | 1차 Q6 결정 |
| 발사 전 — 대상이 사거리 밖으로 이동 | **원래 대상에게 발사**, 맞으면 피해 | 사용자 확정표 · Q5 |
| 발사 순간 — 대상 사망 | **발사 안 함** | 사용자 확정표 · Q15 |
| 발사 후 — 대상 사망 | 마지막 지점까지 비행, **착탄 이펙트만**, 피해·숫자 없음 | 사용자 확정표 · Q4 |
| 발사 후 — 공격자 사망(인간 타워 파괴 포함) | 계속 비행, 맞으면 **피해** | 사용자 확정표 · Q3 · 1-1-2 |
| 게임 종료 · 맵 정리 · 디스폰 · 서비스 교체 | **취소**(닫기) | 위험 7 · Torrent 선례 |

- **「차지 중 타겟 교체」는 독립적으로 생기지 않는다** [전달 — 메인 확인 / document-manager가 HEAD `968e56bc`에서 다시 읽음 [확인]]:
  - 싱글: `UnitCombatUseCase.cs` `TryAttack`은 657행 쿨다운 중 반환 뒤 660행 `FindFirstEnemyTarget`으로 타겟을 고르고, 680~684행에서 이전 타겟과 다르면 `OnCombatTargetChanged`를 낸다 — **쿨다운이 끝난 새 공격 시작 때만** 타겟이 바뀐다.
  - 멀티 서버: `NetworkCombatController.cs` 819~826행 — 이전 타겟과 다를 때 826행 `IsCurrentTargetStillValid`가 false(현재 타겟 사망 또는 사거리 이탈)일 때만 교체한다(825행 주석 「현재 타겟이 사망했거나 사거리를 벗어난 경우에만 교체를 허용」).
  - 그래서 차지 중 타겟 교체는 **대상 사망(→ 발사 취소)** 또는 **사거리 이탈(→ 원래 타겟에 발사)** 로 귀결된다. 별도 예외 조항은 필요 없다.
  - 🔴 **구현 주의:** 투사체 유닛은 커밋된 회차(차지·발사 전) 중에 사거리 이탈을 이유로 서버가 **타겟을 바꾸거나 표현을 닫지 않도록** 해야 한다 — 그렇지 않으면 「사거리 이탈 = 발사」가 지켜지지 않는다(위험 16).
- **충돌 없음 확인 [확인 — 원문 다시 읽음]:** `GameSystemRules_Units.md` 187행(「커밋된 회차의 타겟은 … 다음 타겟 탐색 … 으로 다른 대상에 이전하지 않는다. … 새 타겟은 반드시 새 `AttackSequenceId`를 가진 새 회차에서만 사용한다」)과 `NET-CANCEL-004`(타겟 사망이 회차를 다른 타겟으로 옮기지 않는다)는 위 표와 같은 방향이다. 342행(타겟 전환·StopCombat은 확정 결과를 취소하지 않는다)은 **발사 전엔 확정 결과가 없으므로** 충돌하지 않는다. `NET-CANCEL-002`·`NET-CANCEL-005` 둘째 항목도 같은 방향이다.
- **지금 코드와의 차이 [전달 → 설계 영향은 추정]:** 지금 멀티는 투사체 유닛(Unresolved, 토큰 없음)이 `DelayedAttackDamage`의 due 대기·취소 확인을 건너뛰고(Research 2-1 ④), 싱글 `TickPendingHits`는 공격자 사망과 공격자/타겟 **객체 교체**만 취소한다(Research 2-2). 그래서 「이동·정지 명령 / 발사 전 대상 사망 시 서버 발사 취소」는 **서버 쪽에 새로 넣는 판정**이다(P1).
- (참고) 규칙 17 차지 계약의 현행 화면 경계는 「타겟 교체·Stop·SourceOnly 등 미래 marker 허가가 닫히는 경계」(Units 323행 [확인])다 — 개정안 A-1에서 위 표로 바꾼다.

<a id="1-3-남은-미결확인-항목"></a>

### 1-3. 남은 미결·확인 항목

> 상태 칸이 현재 상태다(2026-10-20 4차 갱신 — **모든 항목 닫힘**, 사용자 승인 「나머지는 추천방식대로 진행해」 [전달 — 메인 세션]). 「항목」 칸은 1차 때 연 질문 원문이다.

| # | 항목 | 상태 | 막는 단계 |
|---|---|---|---|
| **U-1** | Q1 Homing의 비행 시간: (나) 발사 때 고정 vs (다) 매 틱 재계산. 「눈에 보이는 속도」를 지키려면 대상이 움직일 때 속도와 비행 시간 중 무엇을 고정할지가 갈린다 | ✅ **닫힘(4차) — (나) 비행 시간은 발사 때 계산해 고정**(사용자). Units 규칙 17 F-2 문장에 반영 | — |
| **U-2** | Q6 끊김 경계 — 1-2절 ★ 세 행과 화면 경계와의 일치 여부 | ✅ 닫힘(2차) — 1-2절 확정표 | — |
| **U-3** | Q12 — 서버 표면 거리안을 고르면 서버가 읽을 반경 데이터(유닛·건물)의 위치를 정해야 한다 | ✅ 결정 닫힘(2차 — 1-1-2 Q12). 반경 데이터의 구체 위치·형식은 game-programmer 설계 | P1 설계 |
| **U-4** | 속도 초기값 — 현재 프리팹 `_speed` [확인]: Stream 4 · Cannon 4 · Inferno 5 · Fox 5 · Mushroom 7. 인간 타워·EagleArcher 값과 인간 타워 속도 자리 | ✅ 값 닫힘(2차 — 4·4·4·타워 4·5·5·Eagle 6·Mushroom 5). 인간 타워 속도 자리(제안 `BuildingStatsConfig` 종족별 칸 — `humanAttackRange` 94행 등 [확인])는 설계 | P1 설계 |
| **U-5** | Q13 — 지점 고정 시각과 「주 타깃 직접 피해」 처리 | ✅ **닫힘(3차 — 질문 자체가 사라짐)**: 사용자가 Mushroom 예외를 철회해 지점 공격이 없다. 2차의 「고정 시각 = 발사 순간 대상 위치」와 🔶 메인 기본안(반경 안의 적만)은 **폐기** | — |
| **U-6** | 인간 타워가 비행 중 파괴되면 피해가 들어가는가 | ✅ 닫힘(2차) — 들어간다 | — |
| **U-7** | P4 이행 순서 | ✅ 닫힘(2차) — 메인 재량 | — |
| **U-8** | P0의 game-design-lead 검토(Q1·Q13·Q15)를 둘지 생략할지 | ✅ **닫힘(4차) — 생략.** 사용자가 Q1·Q13·Q15를 직접 결정했으므로 game-design-lead 검토를 두지 않는다 | — |
| **U-9** | EagleArcher 발사 시점 — `OnAttackHit` 0.1초가 화살을 놓는 순간인지 | ✅ 닫힘(2차) — 사용자가 0.2초로 옮김(`968e56bc`). ⚠️ config fallback 0.1 불일치는 1-1-2 비고 | — |
| **U-10** | EagleArcher 화살 투사체를 무엇으로 만들지 | ✅ 닫힘(2차) — `EagleArrow` 메시 복사 투사체 프리팹, 착탄 이펙트 없음 | — |
| **U-11** | `EagleArcher_Attack2.anim` 정리(RM-11) 여부 | ✅ **닫힘(4차) — 사용자가 직접 삭제한다.** 이 작업(구현·정리 단계 포함)에서는 손대지 않는다(RM-11) | — |
| **U-12** | 4-2절 승인 대기 개정안의 승인 | ✅ **닫힘(4차) — 승인·반영.** F-1~F-5·F-8·F-9 반영, F-6·F-7 개정 없음(근거 기록) — 4-2-3절 | — |
| **U-13** ✚ | EagleArcher 발사 때 손에 든 화살(유닛 프리팹의 `EagleArrow` 자식 메시)을 숨길지 — 결정 문장에 없다. 착탄 이펙트가 없으므로 Q4(비행 중 대상 사망)에도 화면엔 화살이 마지막 지점에서 사라지기만 한다 [추정] | ✅ **닫힘(4차) — 그대로 둔다.** 손에 든 화살은 숨기지 않는다 | — |
| **U-14** ✚ | 1-2절 확정표(「차지 중」)를 차지가 없는 투사체 유닛의 발사 전 구간에도 똑같이 적용하는지 | ✅ **닫힘(4차) — 적용한다.** 차지 없는 투사체 유닛도 공격 시작~발사 사이에 끊기면 취소(1-2절 같은 상황표). Units 규칙 17 F-1 항목 끝 문장에 반영 | — |
| **U-16** ✚ | resolver의 MushroomBomber `LockedPoint`(167~169행)를 TargetLocked로 바꿀지, C3 축 값으로만 남길지(1-1 Q11 설계 주의) | ✅ **닫힘(4차) — (a) TargetLocked로 변경**, 스키마 3 → 4 상향과 **같은 커밋**에서(구현 단계, game-programmer). 이 값을 단언하는 검증도 함께 갱신 | P1·P2(스키마) 구현 |
| **U-15** ✚ | `UnitStatsConfig.asset` EagleArcher fallback 0.1을 marker 0.2에 맞출지(에셋 수정은 이번 범위 밖) | ✅ **닫힘(4차) — 0.2로 맞춘다.** 에셋 수정은 구현 단계에서 game-programmer가 한다(이번 문서 회차에서는 에셋을 고치지 않았다) | P4 EagleArcher 구현 |

---

## 2. 구현 단계 (2026-10-20 결정 반영 — 4차: 승인됨, 구현 진행)

순서: **P0 → P1 → P2 → P3 → P4(대상별 반복) → 사용자 테스트 → P5.** P1~P3은 공통 기반이며 대상별 스위치가 모두 꺼진 상태로 들어가므로, P4 전까지 게임 동작은 바뀌지 않아야 한다.

**범위 (Q10 결정):** 투사체 대상 = StreamSpirit · Tank · CannonCart · InfernoSpirit · FoxMagician · **EagleArcher** · MushroomBomber(*3차: Homing — 종전 「지점 공격」*) · **인간 방어타워(CannonTower)**. 범위 밖 = Spirit RuneSpire(즉시 피해 유지) · Transcendence VineTower(결정 없음 → 변경 없음) · 근접·Hitscan 유닛.

| 단계 | 내용 | 담당 | 근거 규칙 |
|---|---|---|---|
| **P0** | ✅ **완료(2026-10-20 4차)** — 1-3절 미결 전부 닫힘, 4-2절 개정안 승인·규칙 문서 반영(4-2-3), game-design-lead 검토 생략(U-8). *(종전: 「1-3절 미결·확인 항목 정리(최소 U-1·U-2·U-3은 P1 전), 4-2절 개정안 승인. game-design-lead 검토 여부(U-8)」)* | 메인 + 사용자 · document-manager(규칙 반영) | — |
| **P1** | Application 공통 기반: 신규 `UnitCombatUseCase.Projectiles.cs` — 발사 기록(공격자·대상 **객체 참조**, 발사 때 공격력, 대상 처리 방식 Homing / 지점 공격, 착탄 시각), **매 프레임** 진행, 착탄 시 기존 writer 호출, 닫기(게임 종료·맵 정리·디스폰·서비스 교체), 발사·결과(명중 / 대상 소실 / 취소) 이벤트. 속도는 Inspector에서 고치는 서버 데이터(Q2 — 유닛 `UnitStatsConfig`, 인간 타워는 U-4). Persist=true 착탄용 진입점(Q3, 위험 3). **발사 전 끊김 시 서버 발사 취소**(Q6 — 1-2절 경계, 지금 없는 판정이라 새로 넣는다). **발사 시점 대상 생존 확인**(Q15). 인간 타워 공격(`TowerCombatUseCase`)이 같은 발사 기록을 쓰도록 건물 공격자 지원(RM-10, 타워 파괴 뒤에도 비행 계속 — 1-1-2). **Q12(2차 확정): 비행 거리 = 중심 거리 − 대상 반경, 착탄 지점 = 같은 표면 지점** — 유닛 반경을 `UnitEffectConfig._recipientFits` radius에서 서버 권위 데이터로 옮기고 건물 종류별 반경 신설(RM-12). **커밋된 투사체 회차 중에는 사거리 이탈로 타겟을 바꾸거나 표현을 닫지 않는다**(1-2절 구현 주의, 위험 16). ~~Mushroom = 발사 순간 대상 위치에 고정한 지점 공격, 착탄 때 그 지점 `blastRadius` 안의 적만(메인 기본안, U-5 승인 대기)~~ → *3차:* Mushroom도 Homing, 착탄 때 주 타깃 위치 기준 기존 Blast/독(1-1-3) | game-programmer | `NET-DELIVERY-PROJECTILE` · `U-IMPACT-INDEPENDENT` / `U-IMPACT-LOCKEDPOINT` · `NET-CANCEL-003`·`004`·`005` · 규칙 18(서버 타이머 소유) · Buildings 「방어 타워 시스템」 규칙 9(서버 권위) |
| **P2** | 네트워크: 신규 `NetworkCombatController.Projectiles.cs` — 발사/결과 ClientRpc, epoch, 중복 거부, **늦은 접속 비행 중 투사체 복원(Q14 — 필수)**, 매 프레임 Tick 연결(634행 부근), 스키마 3 → 4(Q11 설계안) | game-programmer | `NET-AUTH-001`·`002` · `NET-TIME-002`·`003`·`005` · `NET-CANCEL-005` |
| **P3** | 화면: 투사체 전담 단일 소비자(파도 `WavePresentationPresenter` 선례), Homing 화면 추적(`TracerProjectile` 확장), 착탄 VFX와 숫자를 **서버 착탄** 시각에 연결(Q7), 비행 중 대상 소실 시 마지막 지점 착탄 이펙트만(Q4), 착탄 이펙트는 서버와 같은 표면 지점(Q12), 대상 marker = 총구 VFX·SFX만이고 트레이서는 서버 발사 기록 수신 때(Q9), 발사 취소 시 투사체·소리 없음(Q6) | game-programmer | 규칙 19 · 규칙 20 · 사운드 규칙 15 · `NET-PRESENT-001`·`002` |
| **P4** | 대상별 이행(Q17): 스위치 켜기 → RM 해당분 주석 처리 → 검증 갱신 → 사용자 실기 확인. **순서는 메인 재량**(2026-10-20 2차, 사용자 「순서는 알아서, 모두 완료하기만 하면 돼」). 아래는 1차 때 적은 **제안 순서**(EagleArcher 선행 조건 U-9·U-10은 2차에 닫힘 — 0.2초 marker, `EagleArrow` 메시 복사 투사체·착탄 이펙트 없음): StreamSpirit(단일, DoT 없음, 에셋 있음) → Tank/CannonCart(건물 2배) → **인간 타워**(Cannon 투사체·착탄 재사용, 발사 이펙트 위치 조정 — 같은 에셋이라 Tank/CannonCart 다음) → InfernoSpirit(화상 + 차지) → FoxMagician(차지 + 규칙 19 제거) → **EagleArcher**(발사 시점 U-9·화살 투사체 U-10 확인 뒤) → MushroomBomber(*3차: Homing — 종전 「지점 공격」*, 기존 스냅샷 교체, 속도 낮춤) | game-programmer → 사용자 실기 (qa-tester는 사용자 지시 시) · 화살 투사체 에셋이 필요하면 asset-prompt-crafter | 위 단계의 규칙 + 규칙 38~40(Mushroom) + Buildings 규칙 12(인간 타워) |
| **P5** | 검증 정리·문서 반영. **모든 대상의 사용자 테스트 PASS 뒤** 스위치 제거(RM-9, Q17), RM-1~RM-8·RM-10 주석 블록 삭제(Q16), RM-11은 사용자가 직접 삭제하므로 손대지 않음(4차, U-11) → 문서/메모리 갱신(규칙 문서의 「🔴 코드 미반영(미구현)」 표시를 결과에 맞춰 고친다 — 4-2-3) | game-programmer, document-manager | WORKFLOW [4]·[6]·[7] |

- 50ms 전투 틱이 아니라 매 프레임으로 진행해야 착탄이 격자만큼 늦지 않는다(위험 4, `TickTorrentLaunches` 선례). 인간 타워 쪽 진행도 같은 매 프레임 진행에 묶는다 [제안].
- *(2026-10-20: 초안의 「EagleArcher(공격 애니메이션 결정 후)와 방어 타워(「즉발 유지」)는 범위 밖이다(Q10)」는 Q10 결정으로 뒤집혔다 — EagleArcher·인간 타워 포함, RuneSpire 제외.)*

---

## 3. 파일별 변경 초안 [전달 — 2026-10-20 결정 반영, ✚ = 이번에 추가한 행]

| 레이어 | 파일 | 변경 |
|---|---|---|
| Application | `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | `ExecuteAttack` 진입부(투사체 분기), 1768행 Fox 분기(RM-5) |
| Application ✚ | `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` | `ExecuteTowerAttack`(216행~) — Human만 241행 즉발 `TakeDamage` 대신 발사 기록(RM-10). Spirit·Transcendence 경로 유지 [행 확인] |
| Application ✚ | `BlastAttackBehavior.cs` (Mushroom) | ~~지점 공격 — 범위 중심을 고정 지점으로(Q13, U-5)~~ → *3차:* 범위 계산 변경 없음(주 타깃 위치 중심 유지). 착탄 시각에 호출되도록 하는 것과 Persist=true 처리(위의 Blast·Inferno 행)만 남는다 |
| Application | 신규 `UnitCombatUseCase.Projectiles.cs` | 발사 기록·진행·착탄·닫기·이벤트 |
| Application | `BlastAttackBehavior.cs` · `InfernoAttackBehavior.cs` | Persist=true일 때 공격자 사망 return 처리(Q3) |
| Application | `AttackEffectPresentation.cs` | Mushroom 이전 시(RM-8) |
| Application | 신규 투사체 계약 파일(`WavePresentationContracts.cs` 선례), `Events/GameEvents.cs` | 발사·결과 이벤트 |
| Infrastructure | `NetworkCombatController.cs` | 1430~1518 예약, 1100~1291 `DelayedAttackDamage`, 634행 Tick |
| Infrastructure | 신규 `NetworkCombatController.Projectiles.cs` | RPC·epoch·늦은 접속 |
| Infrastructure | `UnitStatsConfig.cs` (+ `.asset`) | 속도 필드(Q2) — Inspector 조절, 초기값 U-4 |
| Infrastructure ✚ | `BuildingStatsConfig.cs` (+ `.asset`) | 인간 타워 투사체 속도 [제안 — U-4] |
| Infrastructure | `NetworkGameFlow.cs` · `UnitAttackShadowProfileResolver.cs` | 스키마 상향(19행 상수 [확인]), 필요 시 행. Q11 투사체/즉발 분류를 읽을 자리, EagleArcher 사유 문자열(67행 [확인]) 갱신 후보 |
| Infrastructure | `NetworkHealthSync.cs` | 투사체 키 전송 시(Q7) |
| Presentation | `UnitView.cs` | 7797~7867 원거리 marker 경로(RM-6), R5 호출(RM-2) |
| Presentation | `EffectManager.cs` | R5 교체, 투사체 재생 API |
| Presentation | `TracerProjectile.cs` | Homing 추적 |
| Presentation | `HitPresentationQueue.cs` | 197~256 Mushroom 경로(RM-8), 투사체 키 우회(RM-7) |
| Presentation | 신규 투사체 단일 소비자 | P3 |
| Presentation | `UnitEffectConfig` · `BuildingEffectConfig` | B4 반경 — **Q12 2차 확정: 유닛 radius를 서버 권위 데이터로 옮김(RM-12), 건물 종류별 반경 신설**(새 반경 데이터의 레이어·파일은 설계). `BuildingEffectConfig`: 인간 타워의 투사체·착탄 프리셋 연결(Cannon 재사용) |
| Presentation ✚ | `EffectManager.cs` 타워 표현 | Human 타워를 Tower 즉시 표현에서 투사체 경로로(RM-10). RuneSpire 번개(`VfxPoolItem.TowerBolt.cs`)는 그대로 |
| 에셋 ✚ | `Resources/Config/EffectPresets/EffectPreset_HumanAutoTower_Attack.asset` | 발사 이펙트 위치(`_localPositionOffset` 지금 0,0,0 [확인]) — **2차 확정: 「대포 입구」, 메인/game-programmer가 타워 프리팹 기준으로 정함(사용자 조정 불필요). 투사체 발사 위치도 같은 지점** |
| 에셋 ✚ | EagleArcher 화살 투사체 프리팹(신규) · tracer EffectPreset(신규) | **2차 확정: `Unit_EagleArcher_Blue/Red.prefab`의 `EagleArrow` 메시를 복사해 투사체 프리팹을 만든다. impactPreset은 비운다(착탄 이펙트 없음).** 손의 화살 처리는 U-13 |
| 에셋 ✚ | `vfx_*_tracer.prefab` `_speed`(Stream 4 · Cannon 4 · Inferno 5 · Fox 5 · Mushroom 7) | 서버 속도 초기값(2차 확정): Stream 4 · Tank 4 · CannonCart 4 · 인간 타워 4 · Inferno 5 · Fox 5 · **EagleArcher 6** · **Mushroom 5**. 화면 프리팹 값의 역할은 game-programmer가 정한다(서버 판정·Client 비행 계산에 쓰지 않는다 — 위험 15) |
| 에셋 ✚ | `Resources/Config/UnitStatsConfig.asset` EagleArcher(type 24) `hitFrameTimes` 0.1 | marker 0.2와 불일치 → **4차: 0.2로 맞춘다(U-15), 구현 단계에서 game-programmer** |
| 에셋 ✚ | `Animations/Units/EagleArcher/EagleArcher_Attack2.anim` | RM-11 — **4차: 사용자가 직접 삭제, 이 작업에서 손대지 않음(U-11)** |
| Bootstrap | `GameBootstrapper.cs` | 621~628 Tick 순서에 투사체 진행 추가, 닫기 호출 5곳(`.Map` 375, `.Setup` 346 · 535 · 551 등 [전달]) |
| Editor | `RunUnitActionSelfValidation.cs` · `.VfxConnections.cs` | 6절 |
| 문서 | `GameSystemRules_Units.md` 규칙 17·19·20·38·40 + 419행 인용 블록, `StatsReference.md` 49행 + 표, `UnitCombatAssetMatrix.md`, `VFXSFXGuide.md`, `VFXSFXList.md` | 4절 승인 뒤, P5. **4차: 규칙 17·19·20·419행과 `StatsReference.md` 49행·속도 열은 목표 계약으로 반영 완료(4-2-3), 규칙 38·40은 개정 없음(F-6·F-7).** `UnitCombatAssetMatrix.md`·`VFXSFXGuide.md`·`VFXSFXList.md`는 구현 결과를 적는 문서라 P5에 |
| 문서 ✚ | `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-001`(309행), `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12, `StatsReference.md` 87·118행(EagleArcher Attack2 서술) | 4-2절 승인 뒤. **4차: `NET-PRESENT-001`·Buildings 규칙 12 반영 완료(4-2-3).** *(2026-10-20 2차: `StatsReference.md` 87·118행과 `UnitCombatAssetMatrix.md` 115·133행의 EagleArcher 0.1 → 0.2초·Attack2 미참조 서술은 **사용자 승인으로 반영 완료** — A-12)* |

아키텍처 제약: 속도·발사 기록은 Application/Infrastructure에 두고 Presentation 값(프리팹 `_speed`)을 서버 판정에 쓰지 않는다(Domain → Application → Core → Infrastructure → Presentation → Bootstrap 방향 유지).

---

## 4. 근거 규칙 — 변경 없음 vs 개정 필요

### 4-1. 변경 없이 근거로 쓰는 규칙 [확인 — 원문 읽음]

- `GameSystemRules_UnitCombatSynchronization.md` 「7. 전달 방식별 동기화」 `NET-DELIVERY-PROJECTILE` — 이 작업의 목표 설계 그 자체.
- 같은 문서 「6. 방향과 타겟 동기화」 `NET-FACING-002` · 「9. 취소·사망·중단」 `NET-CANCEL-003`·`NET-CANCEL-004`·`NET-CANCEL-005` · 「5. 서버 시간 재생」 `NET-TIME-002`·`003`·`005` · 「1. 권위 경계」 `NET-AUTH-001`·`002` · 「7.」 `NET-DELIVERY-TRAVELING`(Torrent 선례) · 「8. Animation Event와 표현」 `NET-PRESENT-002`.
- `GameSystemRules_Units.md` 「공격 회차 안정 ID」 `U-ATK-TIMELINE` · `U-IMPACT-LOCKEDPOINT` · `U-IMPACT-INDEPENDENT`, 「전투 연출 동기화 규칙」 규칙 18.
- ✚ (2026-10-20) **결정과 대조해 충돌 없음을 확인한 조항** [확인 — 원문 다시 읽음]: `GameSystemRules_Units.md` 342행(타겟 전환·StopCombat은 확정 결과를 취소하지 않는다 — 발사 전엔 확정 결과가 없다), `NET-CANCEL-002`(커밋 후 취소는 Units 전달 방식별 규칙을 따른다), `NET-CANCEL-004`(새 타겟으로 옮기지 않음 — Q15의 「발사 시점 사망이면 미발사」와 같은 방향), `NET-CANCEL-005`(취소된 future 타격은 결과를 만들지 않는다 — Q6), `NET-CANCEL-003`(발사한 ProjectileImpact만 Persist에 따라 진행 — Q3), `GameSystemRules_Buildings.md` 「방어 타워 시스템」 212행 「바꾸지 않는 것 … 즉발 피해」(Spirit RuneSpire 블록 안의 문장이라 RuneSpire 즉발 유지 결정과 맞다), 규칙 20 349행 마지막 문장(「서버 투사체 판정이 있는 ProjectileImpact는 별도 권위 착탄 계약을 유지한다」).

### 4-2. ✅ 개정안 — 사용자 승인·규칙 문서 반영됨 (2026-10-20 4차)

> **2026-10-20 4차: 사용자가 승인했고 F-1~F-5·F-8·F-9를 규칙 문서·`StatsReference.md`에 승인된 목표 계약(🔴 코드 미반영 표시)으로 반영했다. F-6·F-7은 개정하지 않았다. 반영 위치와 개정 전 원문은 [4-2-3절](#4-2-3-규칙-문서-반영과-개정-전-원문).** *(종전 제목 「🔴 승인 대기 개정안 — 사용자 승인 후에만 규칙 문서에 반영한다」, 종전 첫 문장 「규칙 문서 본문은 이번 회차에 고치지 않았다.」 — 1~3차 회차에 대해서는 참이다)* 아래 「개정안」은 2026-10-20 결정을 반영한 **문안 초안**이며 승인 전에는 어디에도 적용하지 않는다. 규칙 번호는 바꾸지 않는다. 반영 때는 현재 문장을 이 Plan에 원문 그대로 보존하고(아래 「현재 문장」 칸이 그 원문 발췌다) 규칙 문서에는 날짜 붙은 개정 조각을 남긴다. 행 번호는 HEAD `a11b5381` 기준 [확인].
> *(2026-10-20 갱신 — 초안 당시 이 절은 「개정 방향」 표였다. 그중 Q6 행 「서버가 발사하면 화면도 서버 기록으로 투사체를 보여 준다」는 **결정으로 반대 방향이 됐고**, Q10 행 「타워를 범위에 넣을 때만(추천은 제외 → 변경 없음)」은 **인간 타워 포함**으로 바뀌었다. 원문은 커밋 `e5958406`.)*
> *(2026-10-20 2차 — 결정이 추가돼 **4-2-1 최종안**을 새로 두었다. 4-2-2 대조표의 「승인 대기 개정안」 칸은 1차 초안이며, 두 곳이 다르면 **4-2-1이 우선**이다. 규칙 문서 행 번호는 HEAD `968e56bc`에서도 같다 — 그 커밋은 `.anim` 한 파일뿐 [확인].)*

<a id="4-2-1-최종-승인-대기-개정안"></a>

#### 4-2-1. 최종 개정안 (2026-10-20 2차 작성 — 4차에 사용자 승인·반영)

> **2026-10-20 4차: 아래 문안은 사용자가 승인했고 반영됐다(4-2-3).** 반영 때 U-1(비행 시간 발사 때 고정)·U-14(차지 없는 투사체 유닛도 같은 경계) 결정을 F-2·F-1 문장에 한 구절씩 더했다. *(종전: 「모두 승인 전 문안이다. 🔶 = 메인 기본안이 들어 있어 따로 승인이 필요한 부분.」 — 3차 철회 뒤 🔶 표시 행은 남지 않았다)*

| # | 규칙 · 위치 | 개정안 (짧은 문안) |
|---|---|---|
| **F-1** | Units 규칙 17 차지 계약, **323행** | 「발사 전에 공격이 끊기면(대상 사망 · 공격자 사망 · 이동 명령 · 정지 명령) **서버가 발사를 취소한다.** 투사체·공격 SFX·피해가 없고, 차지 VFX는 즉시 꺼진 뒤 다음 동작으로 이어간다(취소 전용 애니메이션 없음). 대상이 사거리 밖으로 나간 것은 끊김이 아니며 **원래 대상에게 발사**한다. 발사 순간 대상이 사망·소멸했으면 발사하지 않는다. 차지 SFX는 아직 없다.」 |
| **F-2** | Units 규칙 17, **324행** | 「… 실제 `OnAttackHit`만 offset으로 추출하며 `OnAttackCharge`는 제외한다. **투사체 유닛은 이 offset이 서버 발사 시각이고, 피해는 서버 착탄 시각(발사 + (중심 거리 − 대상 반경) ÷ 투사체 속도)에 확정된다. 즉발 유닛은 종전대로 권위 피해 offset이다.**」 끝 문장 「Fox 피해 숫자 즉시 표시는 규칙 19를 따른다」와 Immediate bool 언급은 삭제 |
| **F-3** | UnitCombatSynchronization `NET-PRESENT-001`, **309행** | 「발사 전 취소 시 **서버가 발사를 취소하며** 차지 VFX는 즉시 정지하고 tracer·SFX·피해는 없다(경계는 Units 규칙 17). **투사체 유닛의 피해는 서버 착탄 시각에 확정된다.** Unresolved 분류와 기존 권위 writer는 유지하고 Fox Immediate intent는 제거한다.」 |
| **F-4** | Units 규칙 19, **336행** — **예외 제거** | Fox 즉시 표시 예외 문단 삭제 → 「투사체 유닛의 피해 숫자·피격 표현은 **서버 착탄 결과로 착탄 시각에 한 번** 표시한다. 착탄보다 먼저 표시하는 예외는 없다.」 |
| **F-5** | Units 규칙 20, **347행** (+ 419행 Mushroom 인용 블록) | 「투사체 유닛의 투사체는 **서버 발사 기록으로** 재생하고 **서버 착탄 시각·표면 지점**에 닿는다. Homing은 대상을 따라가고, 비행 중 대상이 사라지면 마지막 지점에서 착탄 이펙트만 낸다(피해·숫자 없음). 로컬 `OnAttackHit` marker는 총구 VFX·SFX만 낸다. 늦게 접속한 화면도 비행 중 투사체를 복원한다.」 419행: 「포자 폭탄도 다른 투사체 유닛과 같은 **서버 투사체 기록(Homing)** 이 발사·비행·착탄을 소유한다」로 교체 *(3차: 「지점 공격」 문구 삭제)* |
| **F-6** | Units 규칙 38, **535행** (+ 규칙 39 **544행**) | **개정 불필요** *(2026-10-20 3차 — 사용자가 Mushroom 예외를 철회: 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」·주 타깃 직접 피해+DoT가 그대로 맞다. 2차 문안 「발사 순간 주 타깃 위치에 고정한 지점 … 주 타깃도 반경 밖이면 피해 없음」은 폐기)*. 설계 주의: 중심은 Q12 **표면 착탄점이 아니라 주 타깃 월드 위치**로 둔다(아래 4-2-1 주의) |
| **F-7** | Units 규칙 40, **551행** | **개정 불필요** *(3차 — 「착탄 때 한 번 수집한 대상」이 그대로 맞다. 착탄 시각만 서버 착탄으로 옮겨질 뿐 문장은 참. 2차 문안 「고정 지점 기준으로」는 폐기)* |
| **F-8** | Buildings 「방어 타워 시스템」 규칙 12, **197·199행** — **인간 타워만** | 197: 「**Human CannonTower는 서버 권위 투사체로 피해를 준다** — 대포 입구에서 발사(발사 이펙트도 같은 위치), CannonCart의 투사체·착탄 이펙트, 착탄 때 피해, 비행 중 타워가 파괴돼도 피해. **Spirit RuneSpire와 Transcendence VineTower는 즉발 데미지를 유지한다.**」 199: Human은 「서버 착탄 결과와 착탄 시각」에 연결, 다른 타워는 현행 |
| **F-9** | `StatsReference.md` **49행** + 유닛 표 | 「… **즉발 유닛**의 일반 공격은 권위 피해 예약 시점, **투사체 유닛은 서버 발사 시점**(피해는 착탄) …」 + 투사체 속도 열: Stream 4 · Tank 4 · CannonCart 4 · 인간 타워 4 · Inferno 5 · Fox 5 · EagleArcher 6 · Mushroom 5 (Inspector 튜닝 값) |
| (완료) | `StatsReference.md` 87·118행 · `UnitCombatAssetMatrix.md` 115·133행 | EagleArcher 0.1 → **0.2초**, Attack2 미참조 — **사용자 승인으로 2026-10-20 2차에 반영됨**(승인 대기 아님) |

- **규칙 38·39·40 개정 불필요의 근거 (2026-10-20 3차):** 사용자 결정 「MushroomBomber의 예외를 처리하지 말자. 그냥 똑같이 대상에게 날아가도록 하자, 복잡하니까」 [전달 — 메인 세션]. Mushroom이 Homing으로 대상에게 맞으면 규칙 38 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」(535행), 규칙 39 「주 타깃이 적 유닛이면 직접 피해+DoT」(544행), 규칙 40 「착탄 때 한 번 수집한 대상」(551행) [확인]이 그대로 참이다. 바뀌는 것은 「착탄 때」가 서버 착탄 시각이 된다는 것뿐이고 이는 F-2·F-5가 다룬다.
- **설계 주의 — 범위 중심과 Q12 표면 착탄점:** Q12로 착탄 이펙트·피해 시각은 대상 **표면** 기준이 되지만, Blast/독 판정 중심은 지금처럼 **주 타깃 월드 위치**(중심)로 둔다고 읽어야 규칙 38 개정이 필요 없다. 표면점을 범위 중심으로 쓰면 반경 판정이 대상 반경만큼 공격자 쪽으로 밀리므로 규칙 38 개정이 다시 필요해진다 — game-programmer가 이 구분을 지켜야 한다 [추정 — 설계 해석, 메인 확인 권장].

<a id="4-2-3-규칙-문서-반영과-개정-전-원문"></a>

#### 4-2-3. 규칙 문서 반영과 개정 전 원문 (2026-10-20 4차 — 사용자 승인 「나머지는 추천방식대로 진행해」)

> 🔴 **반영된 문장은 승인된 목표 계약이며 구현·테스트 전이다.** 규칙 문서의 각 개정 자리에 「사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)」 표시와 「구현 전인 지금 코드는 개정 전 문장대로 동작한다」를 함께 적었다. 구현·사용자 테스트가 끝나면 그 표시를 고친다(P5). 규칙 번호는 바꾸지 않았다.
> 반영 기준 checkout HEAD `6a557edf`. 아래 「개정 전 원문」은 그 커밋의 해당 행을 글자 그대로 옮긴 것이다.

| # | 반영 위치 | 반영 내용 |
|---|---|---|
| **F-1** | `GameSystemRules/GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17 차지 계약 셋째 항목(종전 323행) | 4-2-1 F-1 문안 그대로 + **U-14 결정** 문장 「차지가 없는 투사체 유닛도 공격 시작부터 발사 전까지 같은 경계를 적용한다」 |
| **F-2** | 같은 규칙 17 넷째 항목(종전 324행) | 4-2-1 F-2 문안 + **U-1 결정** 구절 「비행 시간은 발사 때 계산해 고정한다」. 「서버 피해 시점·피해량은 이 계약으로 바뀌지 않는다」·「Immediate bool」·끝 문장 「Fox 피해 숫자 즉시 표시는 규칙 19를 따른다」 삭제. 「기존 권위 timer/writer」 → 「기존 권위 writer」(투사체 유닛은 timer가 아니라 착탄이 피해 시각이므로). 두 항목 아래에 날짜 붙은 개정 조각 1개 |
| **F-3** | `GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」 `NET-PRESENT-001`(종전 309행) | 4-2-1 F-3 문안으로 해당 두 문장 교체 + 개정 조각(종전 문장 인용). 첫 문단(Animation Event는 결과를 만들지 않는다)과 2026-10-19 개정 조각은 그대로 |
| **F-4** | Units 규칙 19 첫 문단(종전 336행) | Fox 즉시 표시 예외 두 문장 삭제 → 4-2-1 F-4 문안. 같은 문단 뒷부분(큐 재분류 금지 · 다른 Legacy/특수 피해 경로 유지 · 규칙 17 참조 · 이력 링크)은 유지 |
| **F-5** | Units 규칙 20 첫 줄(종전 347행) + 「특수 공격 시스템 규칙」 2026-10-13 MushroomBomber 인용 블록 둘째 문단(종전 419행) | 347: 4-2-1 F-5 문안. 348·349행 유지(A-5). 419: 앞 문장(marker 역할 변경 사실)은 유지하고 스냅샷 소유 서술 뒤쪽을 「포자 폭탄도 … 서버 투사체 기록(Homing) …」으로 교체, 「C3 축 Unresolved 유지」를 남김 |
| **F-6 · F-7** | Units 규칙 38(535행) · 39(544행) · 40(551행) | **개정하지 않음.** 근거는 4-2-1 아래 「규칙 38·39·40 개정 불필요의 근거」 그대로다 — Mushroom이 Homing으로 대상에게 맞으므로 「판정 중심 = 주 타깃(착탄 지점) 월드 위치」·「주 타깃이 적 유닛이면 직접 피해+DoT」·「착탄 때 한 번 수집한 대상」이 그대로 참이고, 바뀌는 「착탄 시각」은 F-2·F-5가 다룬다. 범위 중심 = 주 타깃 월드 위치(표면 착탄점 아님) 설계 주의도 그대로 유효 |
| **F-8** | `GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12(종전 197·199행) — **인간 타워만** | 197: 4-2-1 F-8 문안(+ 「히트 딜레이를 도입하지 않는다」는 RuneSpire·VineTower 쪽에 유지). 199: Human은 서버 착탄 결과·착탄 시각, 그 밖은 현행. 200행 뒤에 개정 조각. RuneSpire 블록의 「바꾸지 않는 것 … 즉발 피해」(Spirit 한정)는 그대로 |
| **F-9** | `StatsReference.md` 「유닛 — 인간계」 시간 컬럼 설명(종전 49행) + 유닛 표 3개 · 방어 건물 표 3개 | 49: 「즉발 유닛 … / 투사체 유닛은 서버 발사 시점(피해는 착탄)」 + 미구현 조각. 「투사체 속도 (초기값)」 열 신설: Stream 4 · Tank 4 · CannonCart 4 · 인간 타워(CannonTower) 4 · Inferno 5 · Fox 5 · EagleArcher 6 · Mushroom 5, 나머지 「—」(RuneSpire·VineTower는 「— (즉발 유지)」). 열 설명 줄에 「초기값 · Inspector 조절 · 서버 필드 아직 없음 · 단위는 `TracerProjectile._speed`와 같은 월드 유닛/초」 |

**개정 전 원문 (HEAD `6a557edf` — 글자 그대로 보존):**

`GameSystemRules_Units.md` 323행:

````markdown
- 발사 전에 공격이 끊기면(타겟 교체·Stop·SourceOnly 등 미래 marker 허가가 닫히는 경계) 차지 VFX는 즉시 정지하고 투사체·공격 SFX는 내지 않는다. 표현 전용이며 이미 예약된 서버 피해를 취소하지 않는다. 차지 SFX는 아직 없다.
````

`GameSystemRules_Units.md` 324행:

````markdown
- SOLO/server/client 생성은 실제 `OnAttackHit`만 권위 피해 offset으로 추출하며 `OnAttackCharge`는 제외한다. 서버 피해 시점·피해량은 이 계약으로 바뀌지 않는다. type21 fallback은2.8333333초다. 실제 피해는 기존 권위 timer/writer가 소유한다. Fox 값은 사용자 원래 `2:25@30fps = 85/30초`이며2.25초는 잘못된 환산이다. Unresolved/LegacyFallback·기존 writer/Immediate bool·range·다른 타입 의미는 유지한다. Fox 피해 숫자 즉시 표시는 규칙 19를 따른다.
````

`GameSystemRules_Units.md` 336행:

````markdown
FoxMagician이면서 결과 key가 유효하지 않은 Legacy 주 타깃 피해에만 `UnitCombatUseCase.ExecuteAttack`이 기존 `immediatePresentation` 의도를 확정한다. 이벤트·bool RPC·큐로 전달하여 다음 marker를 기다리지 않고 결과 수신 시 한 번 표시한다. 큐에서 공격자 조회로 타입을 재분류하지 않는다. Supported bridge·유효 exact key·다른 Legacy/특수 피해 경로는 유지한다. 현행 charge/damage marker는 규칙17을 따른다. 이전2.25초 및 focused 수용 기록은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-units-03)이며 최신 typed 계약의 native queue·멀티 표시·시각 수용을 대신하지 않는다.
````

`GameSystemRules_Units.md` 347행:

````markdown
원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체(트레이서: 발사→비행→착탄)를 재생한다.
````

`GameSystemRules_Units.md` 419행:

````markdown
> 같은 연결 작업으로 이 marker의 **역할이 바뀌었다**: MushroomBomber의 1.0초 marker는 이제 로컬 트레이서를 발사하지 않고(옛 호출은 주석 비활성화) 기존 레거시 HP·텍스트 맞춤 신호만 낸다. 포자 폭탄 트레이서와 착탄 이펙트는 서버 writer가 확정한 공격 표현 스냅샷 하나가 함께 소유한다(남은 비행 시간 동안 트레이서, 예정 시각에 착탄 1회 — `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`의 MushroomBomber 분기). 피해는 그대로 규칙 18의 서버 타이머다. 규칙 20의 "원거리 유닛은 `OnAttackHit` 시점에 트레이서를 재생한다"는 MushroomBomber에 한해 이 스냅샷 소유 방식으로 대체돼 있다(Unresolved/Legacy 유지, C3 Supported 승격 없음).
````

`GameSystemRules_UnitCombatSynchronization.md` 309행:

````markdown
차지 유닛(InfernoSpirit·FoxMagician)의 typed charge·launch marker와 추출·HP 경계는 `GameSystemRules_Units.md` 규칙17 「차지 유닛 typed marker 계약」을 따른다. `OnAttackCharge`(차지 시작 — 차지 VFX만, lease 확인만·소비 없음)와 `OnAttackHit`(발사 — lease 소비·tracer 발사·공격 SFX)를 구분하며 charge를 피해 offset으로 추출하거나 `OnAttackHit`에서 charge를 다시 재생하지 않는다. 발사 전 취소 시 차지 VFX는 즉시 정지하고 tracer·SFX는 없으며, 서버 피해 시점은 바뀌지 않는다. Unresolved/LegacyFallback·Immediate intent·기존 권위 writer를 유지한다. *(2026-10-19 개정 — 사용자 승인: 종전은 Fox 전용 문단이었고 「damage callback에서 charge를 소비·재생하지 않는다」였다. 개정 전 원문: [작업 Plan 9-4](../_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Plan.md#9-4-상시-문서-반영과-개정-전-원문))* 원래 `2:25@30fps`의 복원이며 이전2.25초 환산은 현행 값이 아니다. source8 manifest·controlled compile·일반23종 감사 및 native/멀티/시각 미실행 경계 원문은 [당시 원문·검증 근거](../History/ImplementationVerificationHistory.md#role-maintenance-20261016-rules-unitcombatsynchronization-05), 현재 개별 수용은 [감사표](../Assets/UnitCombatAssetMatrix.md)를 따른다. 이 한정 결과를 전체 SOLO 완료로 확대하지 않는다.
````

`GameSystemRules_Buildings.md` 197행:

````markdown
방어 타워는 즉발 데미지를 유지한다(히트 딜레이를 도입하지 않는다).
````

`GameSystemRules_Buildings.md` 199행:

````markdown
맞은 유닛의 피격 표현은 서버의 동일 타워 공격 결과 ID와 Impact 시각에 연결해 즉시 재생한다. 유닛 공격자 FIFO나 Animation Event를 사용하지 않는다. 타워 공격도 중복·순서 역전 시 멱등하게 식별할 수 있는 결과 ID를 가져야 한다.
````

`StatsReference.md` 49행:

````markdown
> 시간은 권위 행동 회차 시작부터 해당 사건까지의 offset이다. 일반 공격은 권위 피해 예약 시점, TorrentSpirit는 파도 발사 시점(대상별 피해·힐은 그 뒤 전선 접촉), BloomFairy는 힐 발동 시점이다. 차지 유닛(InfernoSpirit·FoxMagician)은 피해·발사 `OnAttackHit`(Inferno 1.5초, Fox 85/30초)와 차지 시작 `OnAttackCharge`(Inferno 5/30초, Fox 1초)를 구분하며, 시간 칸에는 피해·발사 시점을 쓰고 차지 시작을 괄호에 적는다. 투사체 발사·공격 SFX는 `OnAttackHit` 순간이고 차지 시작은 피해 offset이 아니며 로컬 이벤트가 HP를 적용하지 않는다(`GameSystemRules_Units.md` 규칙 17 「차지 유닛 typed marker 계약」). *(2026-10-19 갱신: 종전 「FoxMagician은 피해 `OnAttackHit @ 85/30초`와 charge `OnAttackCharge @ 1초`를 구분하며 로컬 이벤트가 HP를 적용하지 않는다.」)* `초:프레임(30fps)` 마커는 `초 + 프레임/30`으로 환산한다.
````

(`StatsReference.md` 유닛 표 3개와 방어 건물 표 3개는 「투사체 속도 (초기값)」 열이 없던 것 말고는 바뀌지 않았다 — 원래 표는 같은 커밋에 있다.)

#### 4-2-2. 대조표 — 현재 문장 원문 발췌와 1차 초안

| # | 대상 · 위치 | 현재 문장 (원문 발췌) [확인] | 승인 대기 개정안 (초안) | 결정 |
|---|---|---|---|---|
| **A-1** | `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17 차지 계약, **323행** | 「발사 전에 공격이 끊기면(타겟 교체·Stop·SourceOnly 등 미래 marker 허가가 닫히는 경계) 차지 VFX는 즉시 정지하고 투사체·공격 SFX는 내지 않는다. 표현 전용이며 이미 예약된 서버 피해를 취소하지 않는다. 차지 SFX는 아직 없다.」 | 「발사 전에 공격이 끊기면(〈1-2절 확정 경계〉) **서버가 발사를 취소한다** — 투사체·공격 SFX·피해가 모두 없고 차지 VFX는 즉시 정지한다. 대상이 사거리 밖으로 나간 것은 끊김이 아니며 그대로 발사한다. 발사 시점에 대상이 이미 사망·소멸했으면 발사하지 않는다. 차지 SFX는 아직 없다.」 | Q5·Q6·Q15 (경계 U-2 확정 뒤 괄호 채움) |
| **A-2** | 같은 규칙 17, **324행** | 「SOLO/server/client 생성은 실제 `OnAttackHit`만 권위 피해 offset으로 추출하며 `OnAttackCharge`는 제외한다. 서버 피해 시점·피해량은 이 계약으로 바뀌지 않는다. … 실제 피해는 기존 권위 timer/writer가 소유한다. … Unresolved/LegacyFallback·기존 writer/Immediate bool·range·다른 타입 의미는 유지한다. Fox 피해 숫자 즉시 표시는 규칙 19를 따른다.」 | 「… 실제 `OnAttackHit`만 offset으로 추출하며 `OnAttackCharge`는 제외한다. **투사체 유닛(Q11 분류)은 이 offset이 서버 발사 시각이고, 피해·피해량은 서버 착탄 레코드에서 확정된다(`NET-DELIVERY-PROJECTILE`). 즉발 유닛은 종전대로 권위 피해 offset이다.** type21 fallback · Fox 85/30초 문장은 유지. Unresolved 분류·range 의미는 유지하되 **Fox Immediate bool은 규칙 19 개정으로 제거**된다.」 (끝 문장 「Fox 피해 숫자 즉시 표시는 규칙 19를 따른다」 삭제) | Q1·Q2·Q8·Q11 |
| **A-3** | `GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」 `NET-PRESENT-001`, **309행** | 「발사 전 취소 시 차지 VFX는 즉시 정지하고 tracer·SFX는 없으며, 서버 피해 시점은 바뀌지 않는다. Unresolved/LegacyFallback·Immediate intent·기존 권위 writer를 유지한다.」 | 「발사 전 취소 시 **서버가 발사를 취소하며** 차지 VFX는 즉시 정지하고 tracer·SFX·피해는 없다(경계는 Units 규칙 17). **투사체 유닛의 피해는 서버 착탄 시각에 확정된다.** Unresolved 분류와 기존 권위 writer는 유지하고 Fox Immediate intent는 제거한다.」 같은 조항 첫 문단(Animation Event는 결과를 만들지 않는다)은 그대로 참이므로 유지 | Q6·Q8 |
| **A-4** | `GameSystemRules_Units.md` 규칙 19, **336행** | 「FoxMagician이면서 결과 key가 유효하지 않은 Legacy 주 타깃 피해에만 `UnitCombatUseCase.ExecuteAttack`이 기존 `immediatePresentation` 의도를 확정한다. 이벤트·bool RPC·큐로 전달하여 다음 marker를 기다리지 않고 결과 수신 시 한 번 표시한다. …」 | **Fox 예외 문단 제거.** 대신: 「투사체 유닛의 주 타깃 피해 표시는 서버 착탄 결과 키로 **착탄 시각에 한 번** 표시한다(투사체 단일 소비자). 피해 숫자를 착탄보다 먼저 표시하는 예외는 없다. 다른 Legacy/특수 피해 경로는 유지한다.」 *(사유: 이 예외는 VFX가 없던 시절의 규칙 — 사용자, 2026-10-20)* | Q7·Q8 |
| **A-5** | `GameSystemRules_Units.md` 규칙 20, **347행** (348·349행은 유지) | 「원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체(트레이서: 발사→비행→착탄)를 재생한다.」 | 「**투사체 유닛**의 투사체는 서버 발사 기록을 받았을 때 재생하고 서버 착탄 시각·지점에 닿는다(Homing은 대상을 따라가고, 비행 중 대상이 사라지면 마지막 지점에서 착탄 이펙트만 낸다). 로컬 `OnAttackHit` marker는 총구 VFX·SFX만 낸다. 늦게 접속한 화면도 비행 중 투사체를 서버 기록으로 복원한다.」 348행 「순수 시각 표현 … 데미지 판정 타이밍에 영향을 주지 않는다」는 「서버 궤적의 표현」으로 여전히 참 | Q4·Q9·Q14 |
| **A-6** | `GameSystemRules_Units.md` 「특수 공격 시스템 규칙」, **419행** 인용 블록 | 「… 포자 폭탄 트레이서와 착탄 이펙트는 서버 writer가 확정한 공격 표현 스냅샷 하나가 함께 소유한다 … 규칙 20의 "원거리 유닛은 `OnAttackHit` 시점에 트레이서를 재생한다"는 MushroomBomber에 한해 이 스냅샷 소유 방식으로 대체돼 있다 …」 | P4 Mushroom 이행 뒤: 「포자 폭탄은 **서버 지점 공격 투사체 기록**이 발사·비행·착탄을 소유한다(규칙 20 개정안과 같은 경로, ~~대상이 아니라 발사 시점의 지점을 향한다~~ *(3차: 다른 투사체와 같은 Homing)*). 스냅샷 소유 방식은 RM-8로 교체됐다.」 | Q13·Q17 |
| **A-7**  **→ 개정 불필요(2026-10-20 3차, 사용자가 Mushroom 예외 철회 — F-6·F-7)** | 규칙 38, **535행** 「판정 중심」 항목 | 「**판정 중심 = 주 타깃(착탄 지점) 월드 위치** — … 폭탄이 떨어진 자리(주 타깃)가 중심이다. 주 타깃은 유닛/건물 무관하게 착탄 중심 좌표를 제공한다.」 | 「**판정 중심 = 발사 시점에 고정한 지점**(지점 공격). 고정 지점은 발사 시점 주 타깃 위치로 정하고, 그 뒤 주 타깃이 움직여도 중심은 바뀌지 않는다.」 | Q13 (고정 시각 U-5) |
| **A-8**  **→ 개정 불필요(2026-10-20 3차, 사용자가 Mushroom 예외 철회 — F-6·F-7)** | 규칙 40 머리 문단, **551행** | 「… 실제 서버 DoT3초·착탄 때 한 번 수집한 대상·HP writer·틱·범위·갱신 정책을 유지한다. …」 | 「… 실제 서버 DoT3초·착탄 때 **고정 지점 기준으로** 한 번 수집한 대상·… 정책을 유지한다. …」 | Q13 |
| **A-9** ✚  **→ 개정 불필요(2026-10-20 3차, 사용자가 Mushroom 예외 철회 — F-6·F-7)** | 규칙 39, **544행** (**메인 목록에 없음 — document-manager 추가**) | 「주 타깃이 적 유닛이면 직접 피해+DoT, 주변 적 유닛이면 DoT만 받는다.」 | **U-5 결정에 따라** 「주 타깃이 착탄 때 고정 지점 `blastRadius` 안에 있으면 직접 피해+DoT, 밖으로 벗어났으면 직접 피해 없음」 같은 조건 추가가 필요할 수 있다 | Q13 · U-5 |
| **A-10** | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12, **197행** (+ 199행) | 197: 「방어 타워는 즉발 데미지를 유지한다(히트 딜레이를 도입하지 않는다).」 199: 「맞은 유닛의 피격 표현은 서버의 동일 타워 공격 결과 ID와 Impact 시각에 연결해 즉시 재생한다. …」 | 197: 「**Human CannonTower는 서버 권위 투사체로 피해를 준다**(발사 → 비행 → 착탄 때 피해, `NET-DELIVERY-PROJECTILE` · Units 규칙 20 개정안과 같은 경로). 투사체·착탄 이펙트는 CannonCart의 Cannon 프리셋을 쓰고 발사 이펙트는 Human tower preset(위치 조정)을 쓴다. **Spirit RuneSpire와 Transcendence VineTower는 즉발 데미지를 유지한다**(히트 딜레이를 도입하지 않는다).」 199: Human은 「서버 착탄 결과 ID와 착탄 시각에 연결」, 그 밖의 타워는 현행 유지 | Q10 |
| **A-11** | `StatsReference.md` **49행** + 유닛 표 「타격·발동 시점」 열 | 「시간은 권위 행동 회차 시작부터 해당 사건까지의 offset이다. 일반 공격은 권위 피해 예약 시점, …」 | 「… **즉발 유닛**의 일반 공격은 권위 피해 예약 시점, **투사체 유닛은 서버 발사 시점**(피해는 착탄 = 발사 + 비행 거리 ÷ 투사체 속도), …」 + 투사체 속도 열 추가(값은 U-4 튜닝 뒤) | Q2·Q11 |
| **A-12** ✚ (2026-10-20 2차 반영 완료 — 사용자 승인, 0.2초 포함) | `StatsReference.md` **87행**(EagleArcher 행 「Attack 1 / Attack2 0.70000005 (선택 순서 미확인)」) · **118행** 「남은 구현 선택 경계」 (**메인 목록에 없음 — document-manager 추가**) | 118: 「EagleArcher의 controller Attack은 1초·marker0.1이며 Attack2는 0.70000005초·이벤트 없음이다. Factory가 이름에 Attack이 포함된 첫 클립을 선택하므로 실제 `animationClips` 순서를 실행 관측하지 않은 상태에서는 주기를 하나로 확정하지 않는다. …」 | 커밋 `a11b5381`로 컨트롤러에 Attack2 상태가 없어졌으므로(Research 11-1) 「Attack 1」 단일 주기로 고치고 118행 경계 문단은 해소 기록으로 바꾼다. 단 「Factory가 이제 Attack을 고른다」는 **실행 관측 없는 추정**이므로 그 표시를 함께 단다(Research 11-2) | Q10 |

- **규칙 문서는 아니지만 같은 사실로 낡는 서술 (1차: 참고, 수정 안 함 → 2026-10-20 2차: 사용자 승인으로 115·133행 반영 완료):** `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` 133행 EagleArcher 「Attack2: 이벤트 0 · 컨트롤러 클립 순서 변경 시 타격점 손실 가능」 [확인]. 고칠지는 사용자 지시로 정한다(CLAUDE.md 규칙 6). `History/ImplementationVerificationHistory.md`의 같은 서술은 이력이라 고치지 않는다.

---

## 5. 위험

| # | 위험 | 대응 |
|---|---|---|
| 1 | 빈 큐 신호 버림 — 화면 경로를 함께 안 바꾸면 숫자가 최대 6초 밀림 | Q7 (b), RM-7, 회귀 fixture |
| 2 | 착탄 VFX 이중 재생(R5 · Mushroom 스냅샷 · 새 경로) | 유닛 스위치를 켤 때 RM-2·RM-8 해당분을 같은 커밋에서 주석 처리, 「1회 재생」 fixture |
| 3 | Persist=true인데 `ApplyAttackDamageCore` 987행 · `BlastAttackBehavior.Apply` · `InfernoAttackBehavior.Apply`의 공격자 사망 return이 그대로면 피해가 조용히 사라짐 | P1에 착탄용 진입점과 발사 때 공격력 저장, fixture |
| 4 | 50ms 격자 지연 | 매 프레임 진행 |
| 5 | 스키마 미상향 시 구·신버전 혼합 경기 | 3 → 4 상향, 불일치 시 시작 거부 fixture |
| 6 | ID만 보관하면 ID 재사용으로 다른 대상 적중 | 객체 참조 보관 + 동일성 확인(Torrent 선례) |
| 7 | 게임 종료·맵 정리 후 착탄 피해 | 닫기 호출 5곳 연결, fixture |
| 8 | 기존 검증 다수 깨짐(Research 8-1) | P4 유닛별로 갱신, 옛 검사는 주석 보존 |
| 9 | 밸런스 체감 변화(첫 피해 0.25~0.7초 지연 [추정]) | Q15는 「발사 시점 생존이면 발사」로 결정(2026-10-20). 속도를 Inspector에서 튜닝(Q2), 실기 확인. game-design-lead 검토 여부는 U-8 *(4차: 생략으로 닫힘)* |
| 10 | 서버 착탄 좌표(유닛 루트)와 화면 위치 차이 — 타워 번개 H1과 같은 종류 [미확인] | Q12 2차 확정 — 서버·화면이 같은 표면 지점(중심 거리 − 반경)을 쓴다. 실기 확인 |
| 11 ✚ | Q6 서버 발사 취소는 **지금 없는 판정**이다. 서버 취소 경계와 화면(차지 VFX 정지·marker 허가 닫힘) 경계가 다르면 「화면엔 투사체가 날았는데 피해 없음」 또는 그 반대가 생긴다 | 1-2절 경계를 확정(U-2)하고 서버·화면이 같은 입력으로 끊김을 판정, fixture(Stop · 타겟 교체 · 사거리 이탈 · 발사 시점 대상 사망 각각) |
| 12 ✚ | 인간 타워는 `TowerCombatUseCase`라는 **다른 피해 경로**다. 건물 공격자를 발사 기록에 넣다가 Spirit RuneSpire·VineTower의 즉발 경로까지 바뀔 수 있다 | Race 분기를 Human 한정으로, RuneSpire 번개·즉발 회귀 fixture. 비행 중 타워 파괴 처리(U-6) |
| 13 ✚ | EagleArcher — 화살 투사체 에셋이 없고, 손에 든 화살(`EagleArrow` 자식 메쉬)과 날아가는 화살이 동시에 보일 수 있다. `OnAttackHit` 0.1초가 화살을 놓는 순간이 아닐 수 있다 | 2차: marker 0.2초로 교정(U-9 닫힘), 메시 복사 투사체·착탄 이펙트 없음(U-10 닫힘). 손의 화살 처리 U-13, config fallback 0.1 불일치 U-15. *(4차: U-13 그대로 둠 — 손의 화살과 날아가는 화살이 함께 보일 수 있는 것은 사용자 선택, U-15 0.2로 맞춤 — 구현 단계)* |
| 14 ✚ | ~~Mushroom 속도를 낮추고 지점 공격으로 바꾸면 움직이는 적이 직접 피해를 피한다 — 새 게임플레이~~ | **해소(2026-10-20 3차)** — 지점 공격 철회로 회피가 생기지 않는다. 남는 것은 속도 7 → 5에 따른 비행 시간 증가뿐(위험 9) |
| 15 ✚ | 속도는 서버 데이터인데 `UnitStatsConfig`는 handshake 해시에 들어가지 않는다(Research 4-2 [전달]). Client가 자기 에셋 속도로 투사체를 움직이면 Host 화면과 어긋난다 | Client는 서버가 보낸 발사·착탄 시각(과 지점)만 쓴다. 속도값 자체를 Client 계산에 쓰지 않는다(Q14 「같은 화면」) |
| 16 ✚ | 멀티 서버 `IsCurrentTargetStillValid`(`NetworkCombatController.cs` 826행)는 현재 타겟이 **사거리를 벗어나면 교체를 허용**한다. 투사체 회차가 커밋된 뒤(차지·발사 전) 이 경로가 타겟을 바꾸거나 표현을 닫으면 「사거리 이탈 = 원래 타겟에 발사」(Q5)가 깨진다 | 커밋된 투사체 회차가 끝날 때까지 타겟 교체·표현 닫기를 미룬다(1-2절 구현 주의). fixture 16. Units 187행·`NET-CANCEL-004`(타겟 이전 금지)와 같은 방향이라 규칙 예외는 필요 없다 |
| 17 ✚ | 반경을 서버 데이터로 옮기면서(RM-12) 서버와 화면이 서로 다른 반경을 읽으면 피해 시점과 착탄 이펙트 지점이 어긋난다. 건물 반경이 빠진 종류는 거리 계산이 불가 | 반경 출처를 하나로, 화면은 서버가 보낸 착탄 지점을 쓴다. 건물 반경 누락 시 대체값 fixture(6절 8) |

---

## 6. 검증 · fixture (초안)

- **바뀌는 기존 검증**: Research 8-1 표 전체(Fox 타이밍·표시, Fox marker fixture, resolver 단언, Unresolved 수 고정, `ValidateLegacyTracerArrivalImpact`, `ValidateWriterSnapshotVfx`, `ValidateChargeUnitLaunchTiming`, Stream/Inferno R5 블록). 옛 검사는 주석 보존 후 새 검사로 대체한다.
- **새 fixture**(Research 8-2):
  1. 싱글 — 발사 시 피해 0, 착탄 시 1회.
  2. 멀티 — 서버 발사 → 착탄 1회, 프레임 단위 진행.
  3. 비행 중 대상 사망 / 공격자 사망(Persist 양쪽) / 게임 종료·맵 정리 닫기 / ID 재사용 다른 객체 미적중.
  4. Homing 착탄 또는 LockedPoint 반경 판정(Q1). *(3차: LockedPoint 대상 없음 — 모든 투사체 Homing 착탄)*
  5. DoT 시작 시각 = 착탄 시각.
  6. 발사·결과 중복 거부, 늦은 접속 복원, Red팀 반전 좌표.
  7. 화면 — 비행 중 숫자·VFX 없음 → 착탄 때 1회, 빈 큐 회귀 방지, 착탄 VFX 이중 재생 금지.
  8. B4 표면점, 건물 반경 누락 시 대체값.
  9. 해시·스키마 불일치 시 경기 시작 거부.
  10. ✚ (1-2절 확정표) 발사 전 Stop / 이동 명령 / 대상 사망 / 공격자 사망 → 발사 기록·결과·피해·SFX 0건(차지 VFX 정지). 발사 전 대상 사거리 이탈 → 원래 대상에게 발사 1건·착탄 피해 1회. 발사 순간 대상 사망 → 발사 0건. 발사 후 공격자 사망(인간 타워 파괴 포함) → 착탄 피해 1회.
  11. ✚ (Q4) 비행 중 대상 소실 → 서버 피해 0, 결과 「대상 소실」 1건, 화면 착탄 이펙트 1회·숫자 0.
  12. ✚ (Q10) 인간 타워 — 발사 시 피해 0, 착탄 때 1회. Spirit RuneSpire·Transcendence VineTower — 지금처럼 즉발 1회(회귀).
  13. ✚ ~~(Q13) Mushroom 지점 공격 — 지점 = 발사 순간 대상 위치. 발사 뒤 주 타깃이 지점 `blastRadius` 밖으로 이동하면 피해·독 0(메인 기본안, U-5 승인 대기), 반경 안의 다른 적은 피해·독, DoT 수집 중심 = 고정 지점.~~ *(3차 폐기)* → **(3차)** Mushroom Homing 착탄 — 착탄 시각에 주 타깃 위치 중심으로 직접 피해·독 1회, 비행 중 주 타깃이 움직여도 따라가 맞힌다. 범위 중심은 표면 착탄점이 아니라 주 타깃 월드 위치.
  14. ✚ (Q2) 속도 값을 바꾸면 서버 착탄 시각이 그에 맞게 바뀌고, Client는 자기 에셋 값이 달라도 서버 시각을 따른다.
  15. ✚ (Q17) 대상별 스위치 OFF → 옛 경로 그대로, ON → 새 경로만(이중 피해·이중 VFX 없음).
  16. ✚ (위험 16) 커밋된 투사체 회차 중 대상이 사거리 밖으로 나가도 서버 타겟·표현이 바뀌지 않고, 회차가 끝난 뒤에만 새 타겟을 고른다(싱글·멀티).
  17. ✚ (Q12) 서버 착탄 시각 = 발사 + (중심 거리 − 대상 반경) ÷ 속도, 서버·Client 착탄 이펙트 지점이 같은 표면 지점. 유닛 반경은 옮긴 서버 데이터에서 읽는다.
  18. ✚ (U-10) EagleArcher 착탄 때 착탄 이펙트 0건, 피해·숫자 1회.
- 실행: 메뉴 `Hexiege/Combat/Run Full Regression Suite`(차지 작업 Plan P5가 확인한 메뉴). 결과는 「PASS / FAIL / 미보고」로 구분해 기록한다.

---

## 7. 테스트 절차 (2026-10-20 결정 반영 초안 — 사용자 실기)

> 대상 하나를 이행할 때마다(P4) 그 대상에 대해 반복한다. `Testcase.md`는 사용자가 요청할 때만 만든다(WORKFLOW [5]). 속도는 이 실기 중에 Inspector로 튜닝한다(Q2).

**싱글플레이**
- S-1. 대상 유닛이 정지한 적 유닛을 공격 → 투사체가 **눈으로 보이는 속도로** 날아가 닿는 순간 착탄 이펙트·피해 숫자·피격 반응이 **함께** 나온다. 숫자가 투사체보다 먼저 뜨지 않는다.
- S-2. 움직이는 적을 공격(B2) → **MushroomBomber를 포함한 모든 투사체**가 따라가 맞는다. Mushroom은 맞은 대상 위치를 중심으로 지금처럼 폭발·독. *(2026-10-20 3차 갱신 — 2차 문장 「MushroomBomber는 쏜 순간 대상이 있던 자리에서 터지고, 그 자리 반경 밖으로 나간 적은 — 주 타깃이라도 — 피해·독을 받지 않는다(메인 기본안, U-5 승인 대기)」는 예외 철회로 폐기)*
- S-3. 비행 중 다른 아군이 대상을 처치(B3) → 중간 이펙트가 없다. 투사체는 마지막 위치까지 날아가 **착탄 이펙트만** 내고 숫자·피해는 없다(Q4).
- S-4. 비행 중 공격자가 죽음 → 투사체가 계속 날아가 맞으면 **피해가 들어간다**(Q3).
- S-5. 큰 건물·큰 유닛을 공격(B4) → 투사체가 몸체 **표면에 닿는 순간** 피해·숫자가 나오고 착탄 이펙트도 그 표면 지점에 보인다(몸체에 가려지지 않는다, Q12).
- S-6. InfernoSpirit·MushroomBomber → 화상·독이 착탄 순간부터 시작된다.
- S-7. FoxMagician → 차지 → 발사 → 착탄 때 숫자(L-1 해소 — 즉시 표시 예외 없음, Q8).
- S-8. ✚ 차지 중(발사 전) 정지 명령·이동 명령, 또는 차지 중 대상이 죽음 → 투사체·공격 소리·피해·숫자가 **모두 없고** 차지 VFX가 꺼진 뒤 다음 동작으로 이어진다(Q6, L-2 해소). 차지 중 대상이 사거리 밖으로 걸어 나감 → **원래 대상에게** 쏘고 맞으면 피해(Q5). 발사 순간 대상이 이미 죽어 있음 → 쏘지 않는다(Q15). 차지 중 공격자가 죽음 → 쏘지 않는다.
- S-9. ✚ 인간 방어타워(CannonTower) → **대포 입구**에서 발사 이펙트와 포탄이 함께 나오고, 포탄이 날아가 착탄 때 피해·숫자. 포탄이 날아가는 중 타워가 파괴돼도 맞으면 피해. Spirit RuneSpire는 지금처럼 번개와 동시에 피해(변화 없음).
- S-10. ✚ EagleArcher → 공격 동작 0.2초(활을 놓는 순간)에 화살이 날아가 맞을 때 피해·숫자, 착탄 이펙트는 없다. 손의 화살 처리는 U-13 결정대로(4차: 숨기지 않고 그대로 둔다).

**멀티플레이 (Host 화면 · Client 화면 각각)**
- M-1. S-1~S-10을 Host와 Client 양쪽 화면에서 확인 — 두 화면의 발사·투사체 위치·착탄·숫자·사망 시점이 맞는다(Q14 「어느 순간이든 같은 화면」).
- M-2. Red팀(반전 좌표) 쪽 유닛·타워가 쏜 투사체도 올바른 방향·위치로 날아간다.
- M-3. 경기 종료 직전 발사한 투사체가 종료 뒤에 피해를 주지 않는다.

**늦은 접속**
- J-1. 투사체가 날아가는 중에 Client가 접속하거나 다시 연결 → **비행 중 투사체가 Host와 같은 위치에 보이고**(Q14), 같은 착탄이 두 번 나오지 않는다.

---

## 8. 계획 변경 이력

| 날짜 | 변경 | 이유 |
|---|---|---|
| 2026-10-19 | 초안 작성(사용자 결정 대기) | 사용자 지시 「C 작업 관련 task 문서 작성」. game-programmer 읽기 전용 조사(HEAD `657a4f31`)를 근거로 document-manager가 HEAD `ab9d55d7`에서 표본 행을 다시 확인. 조사 보고서 목록에 없던 개정 후보 1건(`GameSystemRules_Units.md` 419행 Mushroom 인용 블록)과 규칙 17 차지 계약 셋째 항목(Q6 연결)을 document-manager가 추가했다 |
| 2026-10-20 | 사용자 결정(2026-10-19~20) 기록: 1-0 기본 원칙 · 1-1 결정 표(Q12만 대기) · 1-2 끊김 경계 · 1-3 미결 U-1~U-12 · Q11 설계안. RM-5~RM-8 「결정 의존」→「실행 확정」, RM-9(스위치 제거) · RM-10(인간 타워 즉발) · RM-11(`EagleArcher_Attack2.anim`) 신설. 2·3·4·5·6·7절을 결정에 맞춰 고쳐 씀 — 범위에 EagleArcher·인간 타워 추가, RuneSpire 제외. 4-2절을 「승인 대기 개정안」 A-1~A-12로 교체(**규칙 문서 본문은 수정하지 않음**). 1절 질문 표는 원문 유지, 초안 원문은 커밋 `e5958406` | 메인 세션이 전달한 사용자 결정(원문 취지) [전달]. HEAD `a11b5381`에서 document-manager가 규칙 문서 행 · EagleArcher 컨트롤러/클립 · 프리셋 연결 · `TowerCombatUseCase` · 속도 값을 다시 읽음 [확인]. 메인 목록에 없던 개정 후보 2건(규칙 39 — A-9, `StatsReference.md` 87·118행 — A-12)과 규칙 문서 밖 낡은 서술 1건(`UnitCombatAssetMatrix.md` 133행)을 추가 |
| 2026-10-20 (2차) | 추가 사용자 결정 기록(1-1-2): Q12/B4 서버 표면 거리 확정 · 발사 전후 상황표(1-2절 확정, ★ 닫힘) · 「차지 중 타겟 교체」 독립 발생 없음(코드 근거) · 인간 타워 대포 입구 발사·비행 중 파괴 시 피해 · 속도 초기값 · 순서 메인 재량 · EagleArcher 0.2초·메시 복사 화살·착탄 이펙트 없음 · Mushroom 발사 순간 지점 고정 + 비켜난 주 타깃 처리(🔶 메인 기본안, 승인 대상). 1-3절에 상태 칸 추가(U-2·3·4·6·7·9·10 닫힘, U-13~U-15 신설). RM-12 신설. 2·3·5·6·7절 갱신(위험 16·17, fixture 16~18). 4-2-1 최종 승인 대기 개정안 F-1~F-9 신설. **상시 문서 반영(사용자 승인):** `StatsReference.md` 87·118행, `UnitCombatAssetMatrix.md` 115·133행 — EagleArcher 0.2초·Attack2 미참조 | 메인 세션 전달 [전달]. HEAD `968e56bc`에서 document-manager가 `.anim` 20749행(0.2), `UnitCombatUseCase.cs` 657·660·683행, `NetworkCombatController.cs` 819~826행, Units 187행, `UnitFactory.cs` 529~530행, `UnitStatsConfig.asset` type 24 fallback 0.1을 다시 읽음 [확인]. fallback 불일치(U-15)는 document-manager가 찾은 것 |
| 2026-10-20 (3차) | **MushroomBomber 예외 철회**(사용자: 「예외를 처리하지 말자 … 복잡하니까」) — 1-1-3 신설. 1-0 원칙 5 · 1-1 Q1·Q13 · 1-1-2 Mushroom 두 행 · U-5(🔶 기본안 폐기) · RM-8 · 2절 범위·P1·P4 · 3절 Blast 행 · 위험 14 · fixture 4·13 · S-2를 표시·갱신(지운 문장은 취소선으로 남김). 4-2-1 F-6·F-7과 4-2-2 A-7~A-9를 「개정 불필요」로 정리하고 근거 기록, F-5 419행 문구에서 「지점 공격」 삭제. Q11 설계 주의(resolver Mushroom `LockedPoint` 167~169행)와 U-16 신설, 범위 중심 = 주 타깃 위치 설계 주의 추가. 위험 표 순서 정리(14·15 → 16·17) | 메인 세션 전달 [전달]. resolver 167~169행과 규칙 38·39·40 원문(535·544·551행)을 document-manager가 다시 읽음 [확인] |
| 2026-10-20 (4차) | **사용자 승인 — Plan 상태 「승인됨 — 구현 진행」.** 4-2-1 F-1~F-9 반영: Units 규칙 17 두 항목(F-1 + U-14 문장, F-2 + U-1 구절) · 규칙 19 Fox 예외 삭제(F-4) · 규칙 20 첫 줄과 419행 Mushroom 인용 블록(F-5, Homing) · `NET-PRESENT-001`(F-3) · Buildings 「방어 타워 시스템」 규칙 12 인간 타워만(F-8) · `StatsReference.md` 49행과 「투사체 속도 (초기값)」 열(F-9). 규칙 38·39·40은 개정 없음(F-6·F-7, 근거 기록). 모든 반영 자리에 「사용자 승인 목표 계약 · 🔴 코드 미반영(미구현)」 표시. 4-2-3절 신설(반영 위치 + 개정 전 원문 글자 그대로). 1-3절 U-1(발사 때 고정) · U-8(검토 생략) · U-11(사용자 직접 삭제) · U-12(승인·반영) · U-13(손 화살 그대로) · U-14(차지 없는 투사체도 같은 상황표) · U-15(fallback 0.2로 — 구현 단계) · U-16(TargetLocked로, 스키마 상향과 같은 커밋 — 구현 단계) 닫음. 머리 상태 · RM-11 · 2절 제목 · P0 · P5 · 3절 문서·에셋 행 · 4-2 제목·머리 · 4-2-1 제목 · 1-1 Q1 · game-design-lead 문단 · Q11 설계 주의 · 1-1-2 두 행 · 1-2 머리 · 위험 9·13 · S-10에 날짜 조각 | 사용자 승인 「나머지는 추천방식대로 진행해」(2026-10-20) [전달 — 메인 세션]. 반영 기준 HEAD `6a557edf`, 개정 전 원문은 그 커밋에서 옮김 [확인]. **코드·에셋은 바꾸지 않았다**(U-15 에셋·U-16 resolver는 구현 단계 game-programmer) |
| 2026-10-20 (5차) | **단계 A 사용자 육안 확인 + 서버 판정 로그 확인(scoped), 화면 표현 로그 미확인, 의심 1 후속 판단 대기.** 구현 `d09cc4a9`(P1~P3 공통 기반 + 스위치 StreamSpirit만 ON — 이 Plan에 구현 회차 기록이 없어 여기 함께 적음). 사용자 실기: 멀티 육안 「유닛을 잘 따라감」, 싱글도 테스트함(판정 문구 미전달). 로그(private Release ZIP 3개, 영수증 Research 12-2)로 서버 발사·착탄 피해·비행 중 대상 소실 피해 0·발사 전 대상 사망 취소·게임 종료 닫기가 **`[PROJECTILE]` 96줄 상한 구간에서** 정상. 트레이서·착탄 VFX·숫자 1회는 로그 없음. 의심 1(Client가 서버 발사 취소 공격에도 총구 VFX — Q6과 어긋날 가능성) 수정 여부는 사용자 판단 대기. 회귀 스위트 **미보고**. Research 12절 신설, 머리 상태(Plan·Research)에 날짜 조각 | game-programmer 읽기 전용 로그 분석을 메인 세션이 전달 [전달]. 사용자 상시 승인(로그 분석 결과 문서화). document-manager가 HEAD `51271083`에서 `ProjectileDeliveryContracts.cs` 229~232 · `UnitCombatUseCase.Projectiles.cs` 46~47·105~108·242~248·582 · `NetworkCombatController.Projectiles.cs` 121~126 · `UnitView.cs` 7687~7696 · `UnitAttackShadowObserver.cs` 3949~3953행과 `.gitignore` 814행(로그 본문 제외)을 다시 읽음 [확인]. **코드·에셋은 바꾸지 않았다** |
