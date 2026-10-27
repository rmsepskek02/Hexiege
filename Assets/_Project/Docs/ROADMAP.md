# Hexiege - 작업 로드맵

**문서 역할**
- **목적·질문:** 남아 있는 일의 우선순위·선행 조건·보류 결정을 관리한다.
- **포함:** 미완·추후 검토·결정 필요 항목과 필요한 최소 현재 경계.
- **제외·담당:** 현재 구현은 [현황](PROJECT_STATUS.md), 개별 유닛 후속은 [Matrix](Assets/UnitCombatAssetMatrix.md), 완료 결과는 [작업 이력](WORK_HISTORY.md).
- **원본·참조:** 미완 작업 목록 원본. 기획·기술·값의 답은 각 원본을 참조한다.
- **갱신:** 항목 완료·보류·우선순위 결정 때 본문을 갱신한다. 닫힌 항목의 고유 경위는 이력으로 보존하고 완료 보고를 쌓지 않는다.

## 관리 범위와 선행 결정

이 문서는 실제 미완·보류·우선순위를 관리한다. 과거 조사 잔여는 착수 시 현행과 대조하며 당시 건수를 현재 건수로 사용하지 않는다. 구현·개별 수용은 [Status](PROJECT_STATUS.md)·[Matrix](Assets/UnitCombatAssetMatrix.md), 당시 긴 경위는 [보존 원문](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-management-roadmap)을 따른다.

- 밸런싱과 Human/Spirit/Transcendence AI 시나리오는 묶어서 추후 별건으로 검토한다. 고정 시작 골드·채굴소 가정과 현행 맵 경제의 차이도 그 범위다.
- 낮은 우선 추가 기획은 보류를 유지한다. 완료 수용을 오래된 미주입/미구현 표기로 되돌리거나 새로운 전면 재테스트를 만들지 않는다.
- VFX 코드 후속(2026-10-13~15)의 Bloom 힐 개수 계약 · 차지 사망 정지 · RuneSpire 번개 크리스털→가슴 조준 · 상태 이펙트 크기 맞춤 · 측정 도구 스케일 버그는 사용자 실기로 확인됐다([Plan 13절](_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md)). 남은 것은 아래 「전투·유닛 후속」 표의 「VFX 후속」 두 행이다. *(2026-10-19 병합 반영: 종전 「우선순위 요약」 표는 문서 역할 정리로 이 표 구조로 바뀌었다)*

## 전투·유닛 후속

| 우선 | 남은 일·경계 | 기준 |
|---|---|---|
| P0 | B3 이동·C1 회차·C2 결과·C3 표현의 통합 범위: 전체25종·역할교대·Legacy rollback·단일 emitter/구조 이행. 개별 focused 수용을 유지하며 전체 완료/Legacy 제거와 구별 | Matrix의 전역 이행 축 |
| 개별 우선 유지 | 실제 연결·marker·VFX·사용자 수용/미관측 후속 | [Matrix 현행 목록](Assets/UnitCombatAssetMatrix.md)만 사용. 옛 신규16종 등록/Quake 미주입·실기대기 목록은 폐기된 당시 목록 |
| 별건 후속 | BattleAxe 경기에서 관찰된 심한 렉의 재현·계측·원인 분석 | 사용자 지시로 타이밍 교정과 분리. 원인 미확정 |
| 후속 | 멀티 원거리 facing의 공통 경로·기존 근접 영향 확인 | 기존 보류. 새로운 원인 확정이나 수용 취소는 아님 |
| 🟡 중간 (**OPEN · 원인 미검증** — 2026-10-15 등록) | **VFX 후속 ① RuneSpire 번개 — 맞는 순간 대상이 죽으면 끝점이 살짝 어긋난다.** 살아 있는 대상은 정상(사용자 확인). 코드상 이 경우만 재생 시작 때 대상이 이미 죽어 있어(`HitPresentationQueue.cs` 278행) 추적 없이 「서버 스냅샷 Impact + 가슴 높이」를 끝점으로 쓰고(`VfxPoolItem.TowerBolt.cs` 172행), 그 가슴 높이는 크기 표가 아니라 **0.6m 고정값**이다(`EffectManager.cs` 550·560~563행). 어긋남의 원인(0.6m 고정 · 스냅샷 좌표 ↔ 화면 위치 차이 등)은 **가설일 뿐 미검증**. 다음 단계: 재생 시작 시 스냅샷 Impact·화면 위치·가슴 높이를 Host/Client · Blue/Red 시점에서 진단 기록으로 비교 → 원인 확정 후 수정(후보 중 화면 위치를 쓰는 안은 🔴 Buildings 「방어 타워 시스템」 규칙 12 「추적 불가로 시작할 때」 문구를 바꾸므로 규칙 변경 전 사용자 승인). 🔴 이 결함이 사용자 테스트를 통과한 뒤에만 이 작업의 주석 비활성화 블록 6곳을 최종 삭제한다(2026-10-15 사용자 결정). **인계 블록(단일 소스): [Plan 13-4](_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md)** · 원 분석 [Research 10-4](_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md) | 분야: 표현/VFX · 규모: 소 |
| 🟢 낮음 (**미착수** — 2026-10-15 등록, 이전 작업에서 사용자가 미룬 항목 모음) | **VFX 후속 ② 남은 VFX 작업 묶음** — (a) MushroomBomber 트레이서 **포물선 궤적**(현재 직선 비행 `TracerProjectile.cs` 161행 `Lerp` — [08_12 Plan](_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md) 5절 4번) · (b) 새 VFX의 **SFX**(화상·독·힐 상태 이펙트·RuneSpire/버섯 착탄·InfernoSpirit/FoxMagician **차지 SFX**(2026-10-19 사용자 결정으로 추후 — [Task 3절](_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md)) — 현재 `EffectManager.cs` `Sound15:` 주석대로 미제작, `GameSystemRules_Sound.md` 규칙 15) · (c) **미연결·제작 예정 VFX**(목록의 단일 소스는 [VFXSFXList](Assets/VFXSFXList.md) 「2026-10-14 실측 연결 상태」 블록 — 이 행에 사본을 두지 않는다). 상태 이펙트 프리셋 값 미세 조정은 사용자 데이터 작업이라 이 행에 넣지 않는다 | 분야: 에셋/표현 · 규모: 중 |
| 별건 후속 (**✅ 완료(2026-10-28) — 단계 A~E · P5 · 손 화살 수정안 A · C 모두 사용자 테스트 PASS · main 병합: 브랜치 전체(사용자 SFX 작업 커밋 포함, 사용자 결정 — 실행은 메인 세션) · 남은 후속은 기록만(R-A1~R-A3 · 수정안 B · 손 화살 진단 유지/삭제 결정 대기)** — 2026-10-28 20차 갱신, 직전 「진행 중 — 단계 A·B·B-2·C …」(바로 뒤 원문 유지). 이 행은 main 병합이 끝나면 닫힌 항목으로 정리 대상 · 원문: **진행 중 — 단계 A·B·B-2·C(StreamSpirit · Tank · CannonCart · 인간 대포 타워) 구현 · 사용자 육안·회귀 스위트 PASS, 단계 D·D-2(InfernoSpirit · FoxMagician) 구현 · 실기 로그·육안 정상 · 사용자 테스트 PASS, main 병합, 단계 E(EagleArcher · MushroomBomber) 구현 · 사용자 테스트 PASS, 화살 도달 거리 후속 사용자 테스트 PASS, 단계 E 전체 `origin/main` 반영(fast-forward `33ce4257`) · P5 정리 사용자 테스트 PASS(2026-10-28) · EagleArcher 손 화살 튐 수정안 A 사용자 테스트 PASS(2026-10-28) — 남은 것 수정안 C(화살 출발점을 손 화살 위치로 — 구현 중) → main 병합(수정안 C 사용자 확인 뒤) · 손 화살 진단 유지/삭제 결정 · 후속 후보 R-A1~R-A3(기록만)** — 2026-10-28 갱신, 직전 「— 남은 것 P5 정리 사용자 확인(코드 `9cefe80d` · `b73c0993` · `d77b8e68` 커밋 · 문서 반영 완료) · EagleArcher 손 화살 튐(수정안 A 구현 중) · 후속 후보 R-A1~R-A3(기록만)」; 2026-10-27 P5 갱신, 직전 「— 남은 것 P5 정리(사용자 지시 대기) · 후속 후보 R-A1~R-A3(기록만)」; 2026-10-27 화살 도달 거리 · main 반영 갱신, 직전 「… 단계 E(EagleArcher · MushroomBomber) 구현 · 사용자 테스트 PASS(브랜치 `claude/vfx-code-followups` — 이 checkout의 `origin/main` ref `751436a1`에는 아직 없음) · 화살 도달 거리 후속 진행 중」; 2026-10-27 단계 E 갱신, 직전 「… 사용자 테스트 PASS, main 병합」; 2026-10-27 정정, 직전 「… 회귀 스위트 미보고, main 병합」 2026-10-26; 종전 「단계 A·B·B-2·C … PASS, 단계 D 구현 진행 중」 2026-10-21, 「단계 A(StreamSpirit만) 구현 · 사용자 육안 확인」 2026-10-20, 「미착수 · 방식 결정됨」 2026-10-19 사용자 결정) | **원거리·마법 투사체 표시 — 서버 권위 투사체(C 방식)**: StreamSpirit 물폭탄 · InfernoSpirit 화염구 · FoxMagician 마법탄 · Tank/CannonCart 포탄 · MushroomBomber 포자 트레이서. 지금은 타격 순간에 바로 피해가 나서 비행이 거의 안 보인다 → 서버가 발사·착탄 시각(발사 + 거리 ÷ 속도)을 정하고 착탄 때 피해를 확정한다. A·B·D 방식은 기각. 선행: VFX 제작(지금 진행) → 완성 VFX로 착수. 착수 전 기획 결정 필요(속도 · 비행 중 공격자/대상 사망·이동 · 밸런스). 결정·조사 단일 소스: [Research 14절](_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md#14-projectile-visibility-decision). *(2026-10-19 덧붙임: R5 실기에서 찾은 착탄 이펙트 버그 3건 — 기록만, 미착수 · 이 작업과 함께 해결 대상 → [Plan 18-5](_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md#18-5-r5-followup-fix-queue))* *(2026-10-19 덧붙임: 차지 유닛 발사 시점 기준(차지 시작 → 차지 끝 순간 발사, 둘 다 애니메이션 이벤트, 피해 시점 무변경)과 InfernoSpirit 차지·화염구 동시 출발 → [Plan 18-6](_Tasks/2026-10-13/13_53_vfx-code-followups/Plan.md#18-6-charge-unit-fire-timing). **같은 날 별건으로 표현 쪽 완료·사용자 시각 확인** — [Task](_Tasks/2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-implementation-result). 남은 Fox 피해 숫자 선행·취소 후 숫자만 표시는 이 C 방식에서 다룬다. *(2026-10-26: 단계 D `a0a23423`에서 Fox 즉시 표시 분기 비활성화(RM-5) · 기기 로그 Fox 숫자 immediate 0, 차지 중 취소는 서버 발사 취소로 피해 없음 — [Research 16-4](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#16-4-conclusion) · 16-7)* 종전 표시 「기록만, VFX 제작 뒤 별건」)* *(2026-10-19 덧붙임: 착수 task 문서 작성 — [Research](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md) · [Plan 초안](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md), 사용자 결정 Q1~Q17 대기 · 코드 변경 없음)* *(2026-10-20 갱신: 결정·Plan 승인 뒤 단계 A 구현 `d09cc4a9` — StreamSpirit만 새 경로. 멀티 육안 「유닛을 잘 따라감」, 서버 판정은 로그 상한 구간에서 정상(scoped), 화면 표현 로그 미확인, Client 발사 취소 공격의 총구 VFX 의심 1건 판단 대기, 회귀 스위트 미보고 → [Research 12절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#12-stage-a-field-log-analysis-20261020))* **2026-10-21 기준:** StreamSpirit · Tank · CannonCart · 인간 대포 타워가 서버 투사체로 동작(단계 B `639ac1aa` · B-2 `64b743c8` · C `b819dc51`), 각 단계 실기 로그 정상(scoped)·사용자 육안 확인, 회귀 스위트 PASS(사용자 Console 확인 — RuntimeLog에는 남지 않음) → [Research 15절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#15-stage-c-human-cannon-tower-check-20261021). *(2026-10-26 갱신: 단계 D `a0a23423`(InfernoSpirit · FoxMagician)과 D-2 `fb77548c`(Client에서 사망 표현·화상 VFX가 화면 착탄보다 앞서던 문제를 화면 착탄까지 보류로 수정, 기기 파티클 오류 수정)도 서버 투사체로 동작, 실기 로그 정상(scoped)·사용자 육안 정상, 단계 D-2까지 main 병합·원격 반영 `f367cb52` → [Research 17절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#17-stage-d2-death-hold-check-20261026). 종전 남은 일 첫 항목 「단계 D InfernoSpirit · FoxMagician(구현 진행 중 — 미완료)」)* **남은 일: ① ~~단계 D 마무리 — U-20 종결 확인~~ ✅ 닫힘(2026-10-27 — 사용자 결정 「인페르노·여우 작업 완료한 거 아니야?」로 단계 D·D-2 사용자 테스트 PASS, [Plan 1-3 U-20](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#1-3-남은-미결확인-항목) 종결. 직전 표기 「U-20 행은 아직 「결정 대기」」). BearGuard 화상 VFX가 잘 안 보이던 문제는 별건 [상태 이펙트 Task](_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md)에서 화상 v2 교체로 처리돼 사용자 테스트 PASS(2026-10-27). 회귀 스위트는 2026-10-27 사용자 결정으로 무언급 PASS 간주. 늦은 접속은 사용자 결정으로 실기 테스트 생략 *(2026-10-27 갱신: 종전 「회귀 스위트 결과 확인, BearGuard 화상 VFX가 잘 안 보이는 문제의 크기 수정안 사용자 결정」)* *(2026-10-26 갱신: 종전 이 자리의 「`NET-TIME-002` 문장 정리 개정안 사용자 승인」은 사용자 결정으로 반영돼 닫힘 — [Plan 4-3](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-3-net-time-002-pending-amendment-20261026))* → ② ~~EagleArcher · MushroomBomber 이행~~ ✅ 닫힘(2026-10-27 — 단계 E `5b8fb82e` · 화살 에셋 `be9b6e1a`, 사용자 테스트 PASS, [Research 19절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-stage-e-field-check-20261027)) → ③ ~~화살 도달 거리 후속~~ ✅ 닫힘(2026-10-27 — 수정안 (c) 화살 프리팹 자식 위치 조정 `33ce4257`, 사용자 「잘 수정된 것 같고」 → 사용자 테스트 PASS, [Research 19-10](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-10-arrow-reach-followup). 진단 중 드러난 후속 후보 R-A1 마지막 프레임 그린 뒤 거두기(전 투사체 공통 — 사용자 승인 필요) · R-A2 발사 · 끝점 높이 · R-A3 건물 반경 0.35 측정은 기록만, 미착수 — [Plan 10-11](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#10-11-arrow-reach-followup). 직전 표기 「원인 진단 · 수정안 비교 진행 중」) → ④ ~~스위치·주석 블록 정리(Plan P5)~~ ✅ 닫힘(2026-10-28 — 코드 P5-1 ~ P5-3 · 문서 P5-4 뒤 멀티 2경기 로그 정상 · 회귀 스위트 PASS · 사용자 「특별한 문제는 발견하지 못했어」 → 사용자 테스트 PASS, [Plan 11-9](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#11-9-p5-execution-log) · [Research 20-10](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#20-10-p5-field-check-20261028). 직전 표기 「결정 확정 · 코드 P5-1 ~ P5-3 커밋 · 문서 반영 완료, 사용자 확인(회귀 스위트 + 멀티 실기 1회) 대기」) *(2026-10-27 P5 갱신 — 종전 「(Plan P5 — 전 대상 PASS 조건은 충족, 착수는 사용자 지시 대기)」)* → ⑤ EagleArcher 손 화살 튐 — 수정안 A(프리팹 손 화살 기본 위치 · 회전을 공격 클립 값으로, `2de0ed28`) ✅ 사용자 테스트 PASS(2026-10-28 — 개발 전용 확인 진단 `[EAGLE-ARROW]` 네 화면 PASS, 진단 유지/삭제는 사용자 결정 대기) → ⑥ ~~수정안 C — 날아가는 화살이 손 화살 위치에서 출발~~ ✅ 닫힘(2026-10-28 — `5510b175`, 멀티 1경기 로그 정상 · 사용자 「특별히 문제없어 보여」 · 회귀 스위트 PASS(규칙) → 사용자 테스트 PASS, [Plan 12-7](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#12-7-fix-c-result). 직전 표기 「사용자 요청 2026-10-28 · 구현 중」) → main 병합: 브랜치 전체(사용자 SFX 작업 커밋 포함, 사용자 결정 — 실행은 메인 세션) *(2026-10-28 20차 갱신 — 직전 「→ main 병합(수정안 C 사용자 확인 뒤 — 사용자 지시)」)* → 남은 후속(기록만 · 미착수): R-A1 ~ R-A3(R-A2는 출발점 쪽이 수정안 C로 풀리고 끝점 높이 발밑만 남음), 수정안 B(발사 순간 손 화살 숨기기), 손 화살 진단 `EagleArrowPoseProbe` 유지/삭제(사용자 결정 대기)([Plan 12절](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#12-eagle-hand-arrow-jump)). *(2026-10-28 갱신 — 직전 「⑤ … 채택 · 구현 중, 확인용 로그 진단 추가 중」)* 규칙 문서의 「🟡 부분 구현」 머리는 2026-10-27 「투사체 유닛 7종 전원 구현」으로 갱신됨** *(2026-10-27 갱신 — 종전 「→ ② EagleArcher · MushroomBomber 이행**, 그 뒤 전체 사용자 테스트 PASS 후 스위치·주석 블록 정리(Plan P5)와 규칙 문서 「🟡 부분 구현」 표시 정리」)* | `GameSystemRules_UnitCombatSynchronization.md` 「7. 전달 방식별 동기화」 `NET-DELIVERY-PROJECTILE` · `GameSystemRules_Units.md` 「공격 회차 안정 ID」 `U-IMPACT-LOCKEDPOINT` (규칙 변경 없음 *— 2026-10-20: 승인 개정안 F-1~F-9가 목표 계약으로 반영됨(`7e527c12`), [Plan 4-2-3](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-2-3-규칙-문서-반영과-개정-전-원문)* *— 2026-10-26: 「5. 서버 시간 재생」 `NET-TIME-002` 문장 정리를 사용자 결정으로 반영(현재 동작에 맞춤 — [Plan 4-3](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#4-3-net-time-002-pending-amendment-20261026)); 같은 날 종전 표기 「개정안 사용자 승인 대기, 본문 변경 없음」* *— 2026-10-27 P5: `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12에 적용 범위 문구만 추가(내용 불변, 사용자 승인 P5-J)*) |
| 🟡 중간 (**미착수 · 기록만** — 2026-10-27 사용자 요청) | **지속 효과 이펙트(화상·독·힐) 가시성 강화.** 사용자 요청 원문: 「후속 작업으로 지속 효과를 부여하는 이펙트들(화상·독·힐)을 눈에 띄게 만드는 작업을 할 수 있도록 기록해줘」(직전 04:15 실기 소감 「세 가지 이펙트 모두 전체적으로 눈에 띄는 느낌은 아님, 지금 상태로도 사용 가능」). **현재 상태(코드·에셋 확인):** 화상 = v2 `vfx_infernospirit_burn_v2.prefab`(PS 5개, 몸 둘레 방출), 독 = `vfx_mushroombomber_poison.prefab`, 힐 = `vfx_bloomfairy_heal.prefab`. 프리셋 `Scale Multiplier` 화상 1 · 독 2 · 힐 2, 위치 보정 0. 폭 맞춤·카메라 쪽 당김 옵션은 코드에 있으나 세 프리셋 모두 꺼짐(`EffectPreset.cs` 「Recipient Width Fit」·「Recipient View Pull」). 크기는 `UnitEffectConfig` 크기 표의 키 기반 균등 배율. **후보 방향 [제안 — 미결정]:** (a) 색·밝기 대비(배경·유닛 색과 구별되게) · (b) 프리셋 배율 조정(데이터만) · (c) 발밑 링/글로우 표시 · (d) 효과가 걸리는 시작 순간 강조. **연계 후보:** 독·힐 v2화(사용자 「화상 결과 좋으면 독·힐도 적용 검토」) · Shape 반경을 몸통 반경에 맞추는 코드(넓은 유닛에서 불꽃 원이 몸통 안쪽에 머무는 문제 — 가정 계산) · 옛 화상 프리팹 `vfx_infernospirit_burn.prefab` 삭제(사용자 승인 뒤, guid `32b8d45a8ebc4984485a467f6b7cbda3` 참조 0 재확인 — 2026-10-27 에셋·코드 참조 0) · 사망 뒤 몸 없이 불이 1초 남짓 남는 현상(H2 — 사용자 언급 없음). 🔴 둘레 방출 프리팹에 폭 맞춤을 켜면 Shape를 덮어쓴다(VFXSFXGuide 「코드 계약」 2번). 단일 소스: [Research 11-7 · 13-7](_Tasks/2026-10-26/18_34_status-effect-width-fit/Research.md#13-7-남은-후속-이-task-범위-밖--기록만) · 제작 계약 [VFXSFXGuide](Assets/VFXSFXGuide.md) | 분야: 에셋/표현 · 규모: 중 · 표현 전용(피해·DoT·서버 판정 무관) |
| 후속 (**미착수 · 기록만** — 2026-10-21 사용자 요청) | **인간 방어타워(CannonTower) 전방 120° 공격 제한** — 지금은 360° 아무 방향의 가장 가까운 적을 쏘아, 적이 타워 뒤에 있으면 포탄이 포신 끝에서 뒤로 날아가 어색하다. 사용자 요청 「전방 120도 영역만 공격 가능하도록 바꿔야 어색하지 않을 것 — 문서에 기록만」. 착수 전에 각도 기준점(루트·포구)·인간 타워만 적용할지·밸런스를 정해야 하며, 🔴 규칙 변경 전 사용자 승인 대상. 단일 소스: [Plan 9절 F-120](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Plan.md#9-follow-up-items-not-started) · 코드 사실 [Research 15-9](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#15-9-front-120-request) | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 1 · 2 · 3 · 11 (본문 변경 없음) |

## 네트워크·경기 흐름

| 우선 | 남은 일·선행/미결 |
|---|---|
| 높음 | GameProtocolVersion/build 호환: matchmaking 동일 버전·pre-Relay·NGO 승인/거절·reconnect 검증·업데이트 UX. MapVersion2는 canonical 형식 식별이며 전역 호환을 대신하지 않음 |
| 높음·별건 | 전적 쓰기 연결·상황별 승패·비정상 종료의 다음 실행 처리·서버 위조 방지. 프로필의 전적/랭킹 표시 구현과 별개이며 착수 때 실제 쓰기/도달 확인 재사용 범위를 확인 |
| 높음·선행 있음 | 인게임 ReconnectionHandler를 Host/Client 각자 판정과 끊김 감지 시점으로 정리하고 기존 추가 유예 제거. 결과 화면 판정 구조를 재사용하되 Inspector·기존 코드 정리 절차를 따름 |
| 결정 필요 | Host/Client 종료 비대칭의 Client 판정 범위. 종료 일관성에 Relay 승계·경기 계속하기를 필수로 요구하지 않음 |
| 낮음·미확정 | 인게임 재접속·상태 전체 복원. Host 이탈의 Relay/Lobby 수명·승계 방침과 복구 범위가 선행.120초는 보류 제안이며 결과화면 이탈/기존 유예 제거와 다른 값 |
| 중간·별건 | Relay 경로 장애 처리. 고의 승패 위조 검증과 구별하며 무효 경기 처리안은 미확정 |
| 중간·미결 | NetworkStatusUI 배치·핑/이탈 팝업 필요성·RTT 활용 여부. 착수 때 현재 배치 상태 확인 |
| 중간·이월 | 끊긴 당사자를 구분하는 화면 문구. 연결 정리 뒤 표시 가능 상태와 판정 계층의 존재가 미확인 |
| 높음 | 경기 종료 UI의 남은 관측: ShowResult 삭제 뒤 재검증, 매초 상태 줄 성능/깜빡임, 최초 실패 모달 z-order. 기존 단계1~10 구현·수용은 유지 |
| 중간·추후 | 정상 퇴장 통보의 상대 수신/도달 확인 없이 나갈 가능성 검토. 사용자 지시로 뒤로 미룸 |
| 관측 미확인 | 맵 폴백·자연 실패·복구·timeout/재전송/씬 gate의 미관측 경로. 기존 강제 실패 화면 수용과 구별 |
| 중간 | 멀티 건설/생산 실패 RPC의 UI 피드백 |
| 관찰 중 | 매치메이킹404 수정 뒤 간헐 재현·Host/Client 연결과 RelayJoinCode 대기 경계. 레거시 비활성 코드 삭제는 기존 확인 조건 충족 뒤 판정 |
| 환경 확인 | UGS OIDC 제공자/실계정 브릿지의 환경 설정·연결 범위 |
| 별도 미관측 | 게스트 재실행 닉네임 유지는 UI후속 사용자 확인으로 닫힘. 재실행 전후 전적 상세 보존・실계정 진행도/익명 이전 진행도 미승계・명시logout새guest새닉・취소 경쟁/역할교환은 별도이며 UI완료를 막는 gate가 아님. 기존 랜덤 매칭3단계 수용도 반복 필수검사로 남기지 않음. [UI 확인 범위](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023)·[매칭 근거](_Tasks/2026-10-20/12_19_matchmaking-environment-diagnosis/Research.md#13-랜덤-매칭-세-단계-사용자-수용-완료--2026-10-21) |

제품 조건은 [네트워크 규칙](GameSystemRules/GameSystemRules_Network.md)·[경기 운영](GameSystemRules/GameSystemRules_MatchFlow.md), 구현 구조는 [네트워크 TDD](TechnicalDesignDocument_Network.md), 화면 조건은 [경기 흐름 UI](GameSystemRules/GameSystemRules_MatchFlowUI.md)에서 확인한다. 판정 시계·맵 응답·UI countdown·재접속 대기는 서로 다른 책임이다.

## 기획·경제·콘텐츠·UI

| 우선 | 남은 일·보류 |
|---|---|
| 추후 별건 | 밸런싱+AI 시나리오 통합 재검토. 현재 값은 [Stats](StatsReference.md)만 참조하고 이곳에 값/DPS/쿨다운 사본을 두지 않음 |
| 중간·밸런싱과 함께 | 맵 광산 수가 정한 InitialGold를 실제 시작 자원에 연결. 생성·전송값과 실제 소비를 구분하고 현재 개발값을 버그/출시 확정값으로 해석하지 않음 |
| 실측/미완 | NGO 실효 페이로드·Relay MTU와 경로 완전 차단 대응. 최초/재경기 UI 완료를 다시 열지 않음 |
| 기존 세부 검증 | AI 반응R1~R3·3종족 무작위 선택의 기존 미확인 범위. 핵심 흐름의 과거 확인과 시나리오 재설계는 별개 |
| 별도 경계 | 최종10행 랭킹 높이·배치 사용자 확인까지 완료돼 UI후속의 해당 확인은 닫힘. 제품/통합기록의 로컬main FF는69b5e5c4까지 완료·push 없음. 빌드 완료/설치/실행의 에이전트 직접 관측과 장문·전체기기글자/터치/SafeArea는 별도이며 새 필수 반복관문으로 추가하지 않음. 과거 HUD-007/SET-004/SET-007/END-001/MULTI-END-002 전체수용으로 확대하지 않음. [최신 범위](_Tasks/2026-10-19/15_41_ui-layout-audit/Research.md#ui-layout-implementation-acceptance-20261023) |
| 중간 | MistShrine 멀티: 건물 힐 동기화·Client 표시·팀 검증·쿨다운 미러·이중 틱의 기존 미관측 경계. 물안개 지속VFX/사용 아이콘 제작은 별도 |
| 중간·기획 미확정 | 스킬 건물 실제 스킬 목록·값/슬롯·쿨다운·회복 지점 지정 등 데이터 결정. 현재 플레이스홀더를 완성 스킬 기획으로 승격하지 않음 |
| 결정 필요 | CanvasGroup 숨김 원칙의 제목/취소 버튼 예외를 규칙에 명시할지. 현재 알림 레이아웃의 SetActive 의도와 구현 수용을 뒤집는 작업은 아님 |
| 낮음·추후 기획 미결 | GDD의 Non-P2W 원칙과 카드 레벨업 스탯 증가·패배 후 광고 부활 원안의 충돌 검토. 선택·해결책은 아직 미정 |
| 낮음·보류 | 마법 타워·Idle/Patrol/Retreat 확장·서든데스·튜토리얼·카드/IAP 등 추가 콘텐츠 |
| 추후 추가 예정 | HP바. 현재 게임에는 없다. 추가되면 표시 시점은 [UnitCombatSynchronization](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) `NET-TIME-002`(「5. 서버 시간 재생」)의 같은 표현 시점 정책을 따른다. 우선순위·디자인 미정 *(2026-10-26 사용자 결정 「HP바는 나중에 추가할 생각」)* |
| 추후 조건부 | Victory/Defeat BGM 분리는 [Sound](GameSystemRules/GameSystemRules_Sound.md)의 LocalPlayerTeam.Set() 선행조건이 있는 향후 작업. 일반 게임종료 BGM·기존 사운드 구현을 미구현으로 되돌리지 않음 |
| 별도 백엔드 | 서버 전적 처리·Functions/IAP 등 실제 미완 범위. Firebase 인증·이미 연결된 UGS Cloud Save/Leaderboards와 Firestore 새 계획을 섞지 않고 플랫폼/기능 선택은 확정 근거에 따라 진행 |
| 인프라 | Firebase/EDM SDK·플러그인의 버전 관리/신규 clone 재현성 방침. 과거 Auth 컴파일 게이트 제거 완료와 별개 |
| 결정 필요 · 보안 (**미결 · 기록만** — 2026-10-27) | **Android 서명 키스토어 `hexiege-release.keystore`가 저장소에 커밋돼 있다**(저장소 루트, 마지막 변경 `3151d0df`; `ProjectSettings.asset`의 `AndroidKeystoreName`이 이 파일을 가리킴). 저장소 `rmsepskek02/Hexiege`는 공개 저장소다(메인 세션 전달). 메인 세션이 사용자에게 보고했고 처리 방법은 아직 정하지 않았다 — **판단은 사용자**. 이 문서에는 비밀번호 등 민감 정보를 적지 않는다. 경위: [서버 투사체 Research 19-10](_Tasks/2026-10-19/13_30_server-authoritative-projectile/Research.md#19-10-android-build-incidents) |

## 로그·도구·코드 정리의 기존 잔여

| 우선 | 남은 확인/후보 |
|---|---|
| 중간·미관측 | 자연 엔진 오류가 발생한 회차의 전역 훅 수집. 오류0을 훅 미구현/완료의 증거로 해석하지 않음 |
| 중간·기존 조건 | NetworkCombatController 재경기 리셋의 서버 경로와 NetworkUpgradeController 지연 구독의 실제 스폰 레이스 관측. Client 정상/원래 분기 미실행과 구별 |
| 낮음·후보 | baseline measured_at 갱신의 과거 보고를 현행 도구와 대조해 처리 필요 여부 판정 |
| 낮음·후보 | ProductionTicker 종료 가드·재경기 RPC/일반 ServerRpc/NetworkUnit.SetAnimState의 스폰/종료 가드 검토. 기존 상위 가드·Inspector·동작 변경 필요를 구분 |
| 낮음·미확인 | 옛 LobbyUI.cs 사용 여부. 미참조 추측만으로 삭제하지 않음 |
| 낮음·범위 밖 후속 | FileSink/LogcatCapture의 Editor 로그 경로 문자열 중복 제거·접근 수준 조정. 로그 정책 변경이 아님 |
| 기존 미확인 | 무작위 맵2단계 실기 당시 콘솔 오류/경고 정체 분류 |

Log.md·Task·raw txt.meta 등 보관 규범은 [LogRules](LogRules.md) §1.10/§2를 따른다. 옛 meta 규정 공백은 현재 미결이 아니며 여기서 정책을 복제하지 않는다.

## 기존 규칙·구현 점검의 잔여

| 우선 | 기존 조사 축 | 현재 관리 경계 |
|---|---|---|
| 높음 | 분류1 규정 대비 코드 누락 / 분류2 구현 불일치 | 기존 조사 목록의 닫힌 M-2 등을 제외한 미판정 잔여 |
| 중간/높음 | 분류3 규정 밖 구현 / 분류4 문서 사실 불일치 | 문서·승인 결정·실제 근거 대조가 필요 |
| 중단 | 분류5 코드 주석 검수 | 사용자 중단 결정 유지. 기존 부분 처리·잔여 목록은 원문에 보존하며 재개하지 않음 |
| 결정 필요 | 분류6 문서 간 미확정 충돌 | 확정되지 않은 쪽을 임의 선택하지 않음 |
| 기존 후속 | 테스트 후 삭제 표식 정리 | 기존 명시 대상·검증/삭제 승인 경계를 따른다 |

목록·우선순위의 근거는 [기존 전수 점검 Research](_Tasks/2026-10-01/05_19_rules-implementation-audit/Research.md)와 그 Plan의 중단 범위다.

<!-- 기존 문서 참조의 호환 앵커 -->
<a id="-a-2-매치메이킹-404-수정--호스트-결정-lobby-createorjoin-전환-a방식--초기-정상지속-관찰-중-2026-07-17"></a>
<a id="2026-10-09-assault-bounded-clock-history-readiness-추가-기록"></a>
<a id="ai-시나리오-재논의-사용자-추가-결정-2026-10-13"></a>
<a id="b-0-전역-gameprotocolversionbuild-호환성-관리--높음-2026-07-19-등록"></a>
<a id="b-1-로비-ui-비주얼-폴리싱"></a>
<a id="b-2-재접속-실제-구현"></a>
<a id="c-0-싱글플레이-ai-시스템"></a>
<a id="c-1-게임-내-밸런싱"></a>
<a id="d-1-방어마법-타워"></a>
<a id="d-3-유닛-ai-상태머신"></a>
<a id="d-4-신규-유닛-프리팹-완성-16종"></a>
<a id="e-1-사운드bgm"></a>
<a id="e-2-튜토리얼"></a>
<a id="e-3-firebase-백엔드"></a>
<a id="e-4-로그인-시스템-구현-loginunity"></a>
<a id="f-3-피격-vfx-프리셋-연결--중간"></a>
<a id="f-4-미구현-특수-타격-클립-이벤트-주입--낮음"></a>
<a id="f-5-firebaseedm-저장소-방침-정리--중간-2026-07-13-등록"></a>
<a id="phase-a--네트워크-버그-수정"></a>
<a id="phase-b--네트워크-미완성-기능"></a>
<a id="phase-c--게임플레이-완성도"></a>
<a id="phase-d--콘텐츠-확장-백로그"></a>
<a id="phase-e--플랫폼폴리싱"></a>
<a id="phase-f--전투-타격-타이밍-동기화-후속-2026-07-12-등록"></a>
<a id="공통-이동-복귀-focused-종결-2026-10-15"></a>
<a id="밸런싱ai-시나리오-통합-재검토--추후-별도-작업"></a>
<a id="우선순위-요약"></a>
<a id="원래-교정-반영-감사-종결-2026-10-15"></a>
<a id="현행-진행-경계-2026-10-13-문서-정리"></a>
