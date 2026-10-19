# Plan — 서버 권위 투사체 (옵션 C)

> 작성 2026-10-19 · 기준 checkout HEAD `ab9d55d7` · 근거 조사: 같은 폴더 [Research.md](Research.md)
> **상태: 초안 — 사용자 결정 대기(승인 전).** 1절의 질문 Q1~Q17이 정해지고 이 Plan이 승인되기 전에는 코드·에셋·규칙 문서·`StatsReference.md`를 하나도 바꾸지 않는다(WORKFLOW [4]). 아래 단계·파일·문구는 모두 **추천안 기준의 초안**이며 결정에 따라 바뀐다.
> 근거 표기는 Research와 같다: [확인] · [전달] · [추정] · [미확인]. 행 번호는 바뀔 수 있으므로 메서드 이름을 함께 적는다.
> 읽은 규칙 원본(WORKFLOW [4]): `GameSystemRules.md` 인덱스 · `GameSystemRules_Units.md`(「공격 회차 안정 ID」, 「전투 연출 동기화 규칙」 규칙 17~20, 「착탄형 DoT 확장」 규칙 38~40) · `GameSystemRules_UnitCombatSynchronization.md`(「6. 방향과 타겟 동기화」, 「7. 전달 방식별 동기화」, 「9. 취소·사망·중단」) · `GameSystemRules_Buildings.md`(「방어 타워 시스템」 규칙 12).

## 0. 이 계획이 무엇인가 (쉬운 설명)

지금은 물폭탄·화염구·마법탄·포탄·포자 폭탄이 **쏘는 순간에 이미 피해가 들어간다.** 화면의 투사체는 그 뒤에 날아가는 장식이라, 숫자·사망이 투사체보다 먼저 나오거나 투사체가 거의 안 보인다.

이 계획은 **서버가 투사체를 직접 관리**하게 바꾼다. 서버가 「지금 쐈다」를 기록하고, 거리 ÷ 속도만큼 지난 「닿는 순간」에 피해를 준다. 화면은 서버가 알려 준 발사·착탄 시각에 맞춰 투사체를 날리고, 닿는 순간에 착탄 이펙트와 피해 숫자를 함께 보여 준다. 싱글플레이에서도 같은 규칙으로 동작한다.

정할 것이 많다. 예를 들어 「투사체가 날아가는 동안 대상이 도망가면 따라가는가」, 「쏜 유닛이 비행 중에 죽으면 피해가 들어가는가」 같은 것이다. 이런 질문 17개를 1절에 모았다. **사용자가 정하기 전에는 구현을 시작하지 않는다.** 정해지면 2절의 순서대로, 한 유닛씩 바꾸면서 확인한다.

이 작업이 끝나면 앞선 작업의 남은 문제 — 움직이는 대상에게 쏘면 엉뚱한 자리에서 터짐(B2), 대상이 죽으면 중간에 이펙트가 나옴(B3), 큰 대상에 이펙트가 가려짐(B4), 여우 마법사 숫자가 먼저 뜸(L-1), 차지 취소 뒤 숫자만 뜸(L-2) — 이 함께 정리된다.

---

## 🔴 기존 로직 제거·비활성화 예정 항목 (WORKFLOW [4] — 최상단 명시)

- 모두 **삭제가 아니라 주석 처리로 비활성화**하는 것이 기본이다. 최종 삭제는 [6] **C 사용자 테스트 PASS 뒤**, [7] 문서/메모리 업데이트 전에 한다.
- 「출처」 칸은 그 코드를 만든 Task다. 🔴 「R-1」 같은 번호가 두 Task에서 다른 코드를 가리키므로(Research 1절 주의), 이 Plan의 항목은 **RM-** 번호를 쓰고 옛 번호는 출처 Task 이름과 함께 적는다.
- 아래 항목 중 「결정 의존」이 붙은 것은 해당 질문이 추천대로 정해질 때만 실행한다.

| # | 대상 | 출처 | 처리 | 안전한 근거 |
|---|---|---|---|---|
| **RM-1** | 투사체 유닛의 「타격 순간 즉시 피해」 호출 — 싱글 `TickPendingHits` 만기의 `ApplyAttackDamageCore` 호출(1103행 [전달]), 멀티 `DelayedAttackDamage`의 `ApplyAttackDamageObserved`(1213행 [전달]) | 기존 구조 | **유닛별 스위치가 켜진 투사체 유닛에 한해** 발사 기록 생성으로 분기한다. 스위치가 꺼진 유닛과 비투사체 유닛은 기존 호출을 그대로 탄다. 기존 호출문은 지우지 않는다 | 피해는 착탄 시각에 같은 단일 writer(`ApplyAttackDamageCore` → `ExecuteAttack`)로 들어가므로 피해량·DoT·범위 계산은 같다(Research 4-3). 근거 규칙 `NET-DELIVERY-PROJECTILE` |
| **RM-2** | R5 임시 착탄 연결 — `EffectManager.cs` `PlayTracer`의 `playImpactOnArrive` 분기(621~632행), `ResolveLegacyArrivalImpact`(653행~), `PlayLegacyArrivalImpact`(669행~) [확인], 호출 쪽 `UnitView.cs` 7859~7862행 [전달] | vfx-code-followups 재작업 R5(Plan 18-2 R5-2) | 해당 유닛이 새 경로로 옮겨질 때 그 유닛의 호출을 주석 처리. 모든 대상 유닛 이행 후 메서드 전체 주석 처리 → C PASS 뒤 삭제 | R5는 처음부터 「C 방식 구현 시 교체」로 결정된 임시 경로다(vfx-code-followups Research 15-4). 남겨 두면 착탄 VFX 이중 재생(위험 2) |
| **RM-3** | R5 검증 — `RunUnitActionSelfValidation.VfxConnections.cs` 76행, `ValidateLegacyTracerArrivalImpact`(372~480행), `RunUnitActionSelfValidation.cs` 7508~7524 · 8276 · 8391~8451행 [전달] | vfx-code-followups 재작업 R5(R5-4) | 새 검증으로 대체 후 주석 보존 → C PASS 뒤 삭제 | RM-2가 없어지면 검증 대상이 없다 |
| **RM-4** | 차지 작업 주석 블록 R-1 · R-2 · R-3 · R-4 (`UnitView.cs` 7340 · 1157 · 7460 · 7766/7825/7863/7870행) · V-1 · V-2 · V-3 (`RunUnitActionSelfValidation.cs` 8155 · 8200/8254 · 7972행) [확인] | [차지 유닛 발사 시점 Task](../11_09_charge-unit-launch-timing/Plan.md#9-구현-결과-2026-10-19-추가) | **이미 주석 처리돼 있다.** C 사용자 테스트 PASS 뒤 **삭제**한다 | 사용자 결정 「C 작업 후 정리」 [전달 — 메인 세션]. 그 Task Plan 9-1은 「최종 삭제 미실행 — 사용자 지시 없었다」로 남아 있다 [확인]. Q16에서 재확인 |
| **RM-5** | FoxMagician 즉시 표시 분기 — `UnitCombatUseCase.cs` 1768~1769행 `immediatePrimaryPresentation` [확인] (+ 이를 요구하는 검증 7641 · 8074행 [전달]) | 기존 구조(규칙 19 예외) | **결정 의존 (Q7·Q8).** 주석 처리하고 투사체 키 경로로 통합 | 피해가 착탄으로 옮겨지면 즉시 표시 ≈ 착탄 시각이라 예외의 이유가 없어진다(Research 5-5). 규칙 19 개정 승인 필요(4절) |
| **RM-6** | 원거리 marker에서의 트레이서 발사 — `UnitView.cs` 원거리 marker 경로(7797~7867행 [전달]) | 기존 구조(규칙 20) | **결정 의존 (Q9).** 대상 유닛의 marker는 총구 VFX·SFX만 남기고 트레이서 발사는 주석 처리 | 트레이서는 서버 발사 기록을 받을 때 쏜다(marker·기록 경쟁 회피). 규칙 20 개정 승인 필요 |
| **RM-7** | 원거리 HP 숫자의 공격자별 FIFO 보류 — `HitPresentationQueue` 경로, 대상 유닛 한정 | 기존 구조 | **결정 의존 (Q7).** 투사체 키를 가진 결과는 FIFO를 우회해 단일 소비자로. FIFO 코드 자체는 다른 유닛이 쓰므로 그대로 | 빈 큐 신호 버림(`HitPresentationQueue.cs` 565~570행 [확인]) 때문에 숫자가 최대 6초 밀린다(위험 1) |
| **RM-8** | MushroomBomber 스냅샷 경로 — `AttackEffectPresentation` 생성(1732~1742행), `OnAttackEffectConfirmed` ClientRpc(`NetworkCombatController.cs` 60~86행), `HitPresentationQueue.cs` 197~256행, `EffectManager` Mushroom 고정 프리셋 조회(505 · 533행) [전달] | 기존 구조(Mushroom) | **결정 의존 (Q17 — Mushroom 차례).** Mushroom을 새 경로로 옮긴 뒤 주석 처리. 타워 표현(`AttackEffectVisual` Tower)은 건드리지 않는다 | 착탄 뒤 1회 메시지라 발사·결과 분리가 안 된다(Research 3-1). 남겨 두면 이중 재생 |

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

---

## 2. 구현 단계 (초안 — 결정 뒤 확정)

순서: **P0 → P1 → P2 → P3 → P4(유닛별 반복) → 사용자 테스트 → P5.** P1~P3은 공통 기반이며 유닛 스위치가 모두 꺼진 상태로 들어가므로, P4 전까지 게임 동작은 바뀌지 않아야 한다.

| 단계 | 내용 | 담당 | 근거 규칙 |
|---|---|---|---|
| **P0** | Q1~Q17 결정. Q1·Q13·Q15는 game-design-lead 검토 먼저. 4절 규칙 문구 개정 승인 | game-design-lead + 사용자 | — |
| **P1** | Application 공통 기반: 신규 `UnitCombatUseCase.Projectiles.cs` — 발사 기록(공격자·대상 **객체 참조**, 발사 때 공격력, 대상 처리 방식, 착탄 시각), **매 프레임** 진행, 착탄 시 기존 writer 호출, 닫기(게임 종료·맵 정리·디스폰·서비스 교체), 발사·결과 이벤트. `UnitStatsConfig` 속도 필드(Q2). Persist=true면 착탄용 진입점(Q3, 위험 3) | game-programmer | `NET-DELIVERY-PROJECTILE` · `U-IMPACT-INDEPENDENT` / `U-IMPACT-LOCKEDPOINT` · `NET-CANCEL-003` · 규칙 18(서버 타이머 소유) |
| **P2** | 네트워크: 신규 `NetworkCombatController.Projectiles.cs` — 발사/결과 ClientRpc, epoch, 중복 거부, 늦은 접속 스냅샷(Q14), 매 프레임 Tick 연결(634행 부근), 스키마 3 → 4(Q11) | game-programmer | `NET-AUTH-001`·`002` · `NET-TIME-002`·`003`·`005` · `NET-CANCEL-005` |
| **P3** | 화면: 투사체 전담 단일 소비자(파도 `WavePresentationPresenter` 선례), Homing 화면 추적(`TracerProjectile` 확장), 착탄 VFX와 숫자를 착탄 시각에 연결(Q7), B4 표면점(Q12), 대상 유닛 marker = 총구 VFX·SFX만(Q9) | game-programmer | 규칙 19 · 규칙 20 · 사운드 규칙 15 · `NET-PRESENT-002` |
| **P4** | 유닛별 이행(Q17 순서): 스위치 켜기 → RM-1·2·6·7(·5·8) 해당분 주석 처리 → 검증 갱신 → 실기 확인. 순서 StreamSpirit(단일, DoT 없음, 에셋 있음) → Tank/CannonCart(건물 2배) → InfernoSpirit(화상 + 차지) → FoxMagician(차지 + 규칙 19) → MushroomBomber(기존 스냅샷 교체) | game-programmer → 사용자 실기 (qa-tester는 사용자 지시 시) | 위 단계의 규칙 + 규칙 38~40(Mushroom) |
| **P5** | 검증 정리·문서 반영. **사용자 테스트 PASS 뒤** RM-2·3·4(와 결정된 RM-5·6·8) 주석 블록 삭제 → 문서/메모리 갱신 | game-programmer, document-manager | WORKFLOW [4]·[6]·[7] |

- EagleArcher(공격 애니메이션 결정 후)와 방어 타워(「즉발 유지」)는 범위 밖이다(Q10).
- 50ms 전투 틱이 아니라 매 프레임으로 진행해야 착탄이 격자만큼 늦지 않는다(위험 4, `TickTorrentLaunches` 선례).

---

## 3. 파일별 변경 초안 [전달 — 결정에 따라 바뀐다]

| 레이어 | 파일 | 변경 |
|---|---|---|
| Application | `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | `ExecuteAttack` 진입부(투사체 분기), 1768행 Fox 분기(RM-5) |
| Application | 신규 `UnitCombatUseCase.Projectiles.cs` | 발사 기록·진행·착탄·닫기·이벤트 |
| Application | `BlastAttackBehavior.cs` · `InfernoAttackBehavior.cs` | Persist=true일 때 공격자 사망 return 처리(Q3) |
| Application | `AttackEffectPresentation.cs` | Mushroom 이전 시(RM-8) |
| Application | 신규 투사체 계약 파일(`WavePresentationContracts.cs` 선례), `Events/GameEvents.cs` | 발사·결과 이벤트 |
| Infrastructure | `NetworkCombatController.cs` | 1430~1518 예약, 1100~1291 `DelayedAttackDamage`, 634행 Tick |
| Infrastructure | 신규 `NetworkCombatController.Projectiles.cs` | RPC·epoch·늦은 접속 |
| Infrastructure | `UnitStatsConfig.cs` (+ `.asset`) | 속도 필드(Q2) |
| Infrastructure | `NetworkGameFlow.cs` · `UnitAttackShadowProfileResolver.cs` | 스키마 상향(19행 상수 [확인]), 필요 시 행 |
| Infrastructure | `NetworkHealthSync.cs` | 투사체 키 전송 시(Q7) |
| Presentation | `UnitView.cs` | 7797~7867 원거리 marker 경로(RM-6), R5 호출(RM-2) |
| Presentation | `EffectManager.cs` | R5 교체, 투사체 재생 API |
| Presentation | `TracerProjectile.cs` | Homing 추적 |
| Presentation | `HitPresentationQueue.cs` | 197~256 Mushroom 경로(RM-8), 투사체 키 우회(RM-7) |
| Presentation | 신규 투사체 단일 소비자 | P3 |
| Presentation | `UnitEffectConfig` · `BuildingEffectConfig` | B4 반경(Q12) |
| Bootstrap | `GameBootstrapper.cs` | 621~628 Tick 순서에 투사체 진행 추가, 닫기 호출 5곳(`.Map` 375, `.Setup` 346 · 535 · 551 등 [전달]) |
| Editor | `RunUnitActionSelfValidation.cs` · `.VfxConnections.cs` | 6절 |
| 문서 | `GameSystemRules_Units.md` 규칙 17·19·20·38·40 + 419행 인용 블록, `StatsReference.md` 49행 + 표, `UnitCombatAssetMatrix.md`, `VFXSFXGuide.md`, `VFXSFXList.md` | 4절 승인 뒤, P5 |

아키텍처 제약: 속도·발사 기록은 Application/Infrastructure에 두고 Presentation 값(프리팹 `_speed`)을 서버 판정에 쓰지 않는다(Domain → Application → Core → Infrastructure → Presentation → Bootstrap 방향 유지).

---

## 4. 근거 규칙 — 변경 없음 vs 개정 필요

### 4-1. 변경 없이 근거로 쓰는 규칙 [확인 — 원문 읽음]

- `GameSystemRules_UnitCombatSynchronization.md` 「7. 전달 방식별 동기화」 `NET-DELIVERY-PROJECTILE` — 이 작업의 목표 설계 그 자체.
- 같은 문서 「6. 방향과 타겟 동기화」 `NET-FACING-002` · 「9. 취소·사망·중단」 `NET-CANCEL-003`·`NET-CANCEL-004`·`NET-CANCEL-005` · 「5. 서버 시간 재생」 `NET-TIME-002`·`003`·`005` · 「1. 권위 경계」 `NET-AUTH-001`·`002` · 「7.」 `NET-DELIVERY-TRAVELING`(Torrent 선례) · 「8. Animation Event와 표현」 `NET-PRESENT-002`.
- `GameSystemRules_Units.md` 「공격 회차 안정 ID」 `U-ATK-TIMELINE` · `U-IMPACT-LOCKEDPOINT` · `U-IMPACT-INDEPENDENT`, 「전투 연출 동기화 규칙」 규칙 18.

### 4-2. 🔴 개정이 필요한 문장 — **사용자 승인 후에만** 고친다

규칙 번호는 바꾸지 않는다. 개정 때는 현재 문장을 Task Plan에 원문 그대로 보존하고 날짜 붙은 개정 조각을 남긴다.

| 대상 | 현재 문장 요지 [확인] | 개정 방향(초안) | 연결 질문 |
|---|---|---|---|
| 규칙 17 (324행) | 「서버 피해 시점·피해량은 이 계약으로 바뀌지 않는다」 · 「`OnAttackHit`만 권위 피해 offset」 | 투사체 유닛은 `OnAttackHit` = 서버 **발사** offset, 피해는 착탄 시각 | Q1·Q2 |
| 규칙 17 차지 계약 셋째 항목 | 「발사 전에 끊기면 투사체·공격 SFX는 내지 않는다」 | 서버가 발사하면 화면도 서버 기록으로 투사체를 보여 준다 | Q6 |
| 규칙 19 | FoxMagician 즉시 표시 예외 | 예외 제거, 투사체 결과 키로 착탄 시각에 1회 표시 | Q7·Q8 |
| 규칙 20 (346~349행) | 「원거리 유닛은 `OnAttackHit` 시점에 트레이서를 재생」 | 투사체 유닛은 서버 발사 기록 수신 시 재생, marker는 총구 VFX·SFX | Q9 |
| 「특수 공격 시스템 규칙」 419행 인용 블록 | Mushroom은 규칙 20을 스냅샷 소유 방식으로 대체 | Mushroom 이전 후 새 경로로 서술 | Q17 |
| 규칙 38 · 40 | 판정 중심 = 주 타깃 위치 · 착탄 때 한 번 수집 | 고정 지점 중심(결정 시) | Q13 |
| `StatsReference.md` 49행 + 표 「타격·발동 시점」 열 | 「일반 공격은 권위 피해 예약 시점」 | 투사체 유닛은 「발사 시점」, 속도 열 추가 | Q2 |
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 | 「방어 타워는 즉발 데미지를 유지한다」 | **타워를 범위에 넣을 때만**(추천은 제외 → 변경 없음) | Q10 |

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
| 9 | 밸런스 체감 변화(첫 피해 0.25~0.7초 지연 [추정]) | Q15 game-design-lead 검토, 실기 확인 |
| 10 | 서버 착탄 좌표(유닛 루트)와 화면 위치 차이 — 타워 번개 H1과 같은 종류 [미확인] | Q12 화면 표면점, 실기 확인 |

---

## 6. 검증 · fixture (초안)

- **바뀌는 기존 검증**: Research 8-1 표 전체(Fox 타이밍·표시, Fox marker fixture, resolver 단언, Unresolved 수 고정, `ValidateLegacyTracerArrivalImpact`, `ValidateWriterSnapshotVfx`, `ValidateChargeUnitLaunchTiming`, Stream/Inferno R5 블록). 옛 검사는 주석 보존 후 새 검사로 대체한다.
- **새 fixture**(Research 8-2):
  1. 싱글 — 발사 시 피해 0, 착탄 시 1회.
  2. 멀티 — 서버 발사 → 착탄 1회, 프레임 단위 진행.
  3. 비행 중 대상 사망 / 공격자 사망(Persist 양쪽) / 게임 종료·맵 정리 닫기 / ID 재사용 다른 객체 미적중.
  4. Homing 착탄 또는 LockedPoint 반경 판정(Q1).
  5. DoT 시작 시각 = 착탄 시각.
  6. 발사·결과 중복 거부, 늦은 접속 복원, Red팀 반전 좌표.
  7. 화면 — 비행 중 숫자·VFX 없음 → 착탄 때 1회, 빈 큐 회귀 방지, 착탄 VFX 이중 재생 금지.
  8. B4 표면점, 건물 반경 누락 시 대체값.
  9. 해시·스키마 불일치 시 경기 시작 거부.
- 실행: 메뉴 `Hexiege/Combat/Run Full Regression Suite`(차지 작업 Plan P5가 확인한 메뉴). 결과는 「PASS / FAIL / 미보고」로 구분해 기록한다.

---

## 7. 테스트 절차 (초안 — 사용자 실기)

> 유닛 하나를 이행할 때마다(P4) 그 유닛에 대해 반복한다. `Testcase.md`는 사용자가 요청할 때만 만든다(WORKFLOW [5]).

**싱글플레이**
- S-1. 대상 유닛이 정지한 적 유닛을 공격 → 투사체가 날아가 닿는 순간 착탄 이펙트·피해 숫자·피격 반응이 **함께** 나온다. 숫자가 투사체보다 먼저 뜨지 않는다.
- S-2. 움직이는 적을 공격(B2) → Q1 결정대로: Homing이면 따라가 맞고, LockedPoint면 처음 조준한 자리에서 터지며 반경 밖이면 피해가 없다.
- S-3. 비행 중 다른 아군이 대상을 처치(B3) → 중간 이펙트가 없다. Q4 결정대로 소멸 또는 마지막 위치 착탄 이펙트(숫자 없음).
- S-4. 비행 중 공격자가 죽음 → Q3 결정대로 피해가 들어가거나 들어가지 않는다.
- S-5. 큰 건물·큰 유닛을 공격(B4) → 착탄 이펙트가 몸체에 가려지지 않는다.
- S-6. InfernoSpirit·MushroomBomber → 화상·독이 착탄 순간부터 시작된다.
- S-7. FoxMagician → 차지 → 발사 → 착탄 때 숫자(L-1 해소). 차지 중 공격이 끊기면 Q6 결정대로 동작(L-2).

**멀티플레이 (Host 화면 · Client 화면 각각)**
- M-1. S-1~S-7을 Host와 Client 양쪽 화면에서 확인 — 두 화면의 착탄·숫자·사망 시점이 맞는다.
- M-2. Red팀(반전 좌표) 쪽 유닛이 쏜 투사체도 올바른 방향·위치로 날아간다.
- M-3. 경기 종료 직전 발사한 투사체가 종료 뒤에 피해를 주지 않는다.

**늦은 접속**
- J-1. 투사체가 날아가는 중에 Client가 접속하거나 다시 연결 → Q14 결정대로 비행 중 투사체가 보이거나 착탄 결과만 보인다. 같은 착탄이 두 번 나오지 않는다.

---

## 8. 계획 변경 이력

| 날짜 | 변경 | 이유 |
|---|---|---|
| 2026-10-19 | 초안 작성(사용자 결정 대기) | 사용자 지시 「C 작업 관련 task 문서 작성」. game-programmer 읽기 전용 조사(HEAD `657a4f31`)를 근거로 document-manager가 HEAD `ab9d55d7`에서 표본 행을 다시 확인. 조사 보고서 목록에 없던 개정 후보 1건(`GameSystemRules_Units.md` 419행 Mushroom 인용 블록)과 규칙 17 차지 계약 셋째 항목(Q6 연결)을 document-manager가 추가했다 |
