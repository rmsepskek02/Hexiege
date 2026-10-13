# Plan: 멀티플레이 유닛 이동·공격 동기화 규칙 개정

**작성일:** 2026-07-20  
**Research 참조:** `Research.md`  
**계획 범위:** 규칙 문서 개정까지만 — 코드·프리팹·애니메이션 에셋 수정 제외

**2026-07-22 재감사:** main에 InfernoSpirit·QuakeSpirit Legacy 핸들러와 Quake 스탯이 추가됐다. 해당 피해 의미는 v2 설계 입력으로 보존하지만 marker·ImpactPoint·sequence·단일 writer가 미완료이므로 구현 완료 수에는 포함하지 않는다.

---

## 이 계획으로 무엇을 하는가

현재 유닛 규칙은 서버 권위라는 올바른 원칙 위에 세워졌지만, 회전 정렬과 공격 타임라인의 세부 정의가 부족하고 과거의 FIFO 기반 연출 보완책이 정식 규칙으로 남아 있다. 이번 단계에서는 구현을 먼저 건드리지 않고, 멀티플레이에서 모든 유닛이 따라야 할 공통 계약을 문서로 확정한다.

개정 규칙이 승인되기 전에는 기존 로직을 제거하거나 비활성화하지 않는다. 코드 변경 계획과 마이그레이션 계획은 규칙 승인 후 별도 작업으로 작성한다.

---

## 1. 근거 규칙

- 서버 권위 유지: 현행 규칙 3, 18, 29, 34, 40
- 월드 좌표 판정 유지: 현행 규칙 6, 16, 24, 38
- 전투 중 이동·공격의 배타성: 현행 규칙 10, 14
- 타겟 방향 정렬 의도: 현행 규칙 8, 12, 15
- Animation Event와 서버 피해 분리: 현행 규칙 17, 18

이번 개정은 위 원칙을 제거하지 않고, 모순과 누락을 해결하는 하위 계약을 추가한다.

---

## 2. 문서 개정 목표 구조

### 2.1 `GameSystemRules_Units.md`

플레이어가 체감하는 안정적인 게임플레이 불변 규칙만 남긴다.

- 상태: Idle / AlignToMove / Move / AcquireTarget / AlignToAttack / Windup / Impact / Recovery
- 각 상태에서 이동·회전·타겟 변경·공격 허용 여부
- 공격 커밋과 취소 조건
- 근접·Hitscan·ProjectileImpact·이동 영역형의 판정 의미
- 범위 공격의 권위 방향과 권위 위치
- 쿨다운 및 다중 타격 의미

### 2.2 신규 `GameSystemRules_UnitCombatSynchronization.md`

서버와 클라이언트의 동기화 계약을 분리한다.

- 서버 권위 행동 시퀀스
- 서버 시각 또는 네트워크 틱 기준 시작·타격 시각
- `AttackSequenceId + HitIndex` 상관관계
- 늦은 참가, 재접속, 순서 역전, 중복 메시지 처리
- Simulation Root와 Visual Root의 책임
- 클라이언트 표현 보정과 허용 가능한 시각적 지연

### 2.3 신규 `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`

유닛별 에셋·판정 완성도를 관리한다.

- 공격 유형
- 한 공격의 타격 수
- 권위 타격 시각 교정 상태
- Attack 클립 및 표현 이벤트 상태
- 투사체·VFX·SFX 상태
- 싱글플레이 검증 상태
- Host / Client / Blue / Red 멀티플레이 검증 상태
- 미완성 사유와 다음 작업

문서명과 최종 위치는 `document-manager` 검토 후 확정한다.

---

## 3. 규칙 결정 게이트

초기에는 각 게이트를 하나씩 확인할 예정이었으나, 사용자가 2026-07-20 관련 문서를 한 번에 수정하도록 요청했다. 아래 권장안을 단일 규칙 세트로 확정해 일괄 반영한다.

### Gate 1. 공격 판정 모델

**권장안:** 원거리 공격을 Hitscan과 ProjectileImpact로 분리한다.

- Hitscan: 서버 발사 타격 시각에 대상 유효성과 결과를 확정한다.
- ProjectileImpact: 서버가 권위 착탄 시각·위치·대상을 관리하고 착탄 시 결과를 확정한다.
- 이동형 파동: 서버가 전선을 시뮬레이션하고 대상별 접촉 시각에 결과를 확정한다.

결정 후 모든 공격 유닛을 분류한다. 미완성 유닛은 `미확정`으로 둘 수 있지만, 검증 완료 상태로 표시할 수 없다.

**결정:** 승인됨 (2026-07-20). 전달 방식은 `MeleeContact / Hitscan / ProjectileImpact / TravelingArea`로 구분한다. 대상 범위(`Single / Area`), 범위 모양(`Cone / Circle / Rectangle` 등), 효과 종류(`Damage / Heal / Status`), 적용 일정(`Instant / Periodic`)은 별도 축으로 관리한다. `GameSystemRules_Units.md` 규칙 20을 `U-COMBAT-DELIVERY`로 개정했다. 개별 유닛 분류는 완성도 매트릭스 작성 단계에서 실제 공격 의도와 에셋 상태를 확인해 확정한다.

### Gate 2. 이동 방향 정렬

**권장안:** 큰 방향 전환은 `AlignToMove`에서 제자리 회전 후 이동하고, 작은 오차만 이동 중 보간을 허용한다.

확정할 값:

- 제자리 회전이 필요한 각도 임계값
- 이동 중 허용되는 최대 방향 오차
- 정렬 중 적 감지 시 이동 정렬 중단 및 공격 정렬 전환 여부

**결정:** 승인됨. 10° 이하에서 이동 시작, 이동 중 15° 초과 시 정지·재정렬한다. 정렬 중 적 감지는 공격 정렬을 우선한다.

### Gate 3. 공격 방향 정렬

**권장안:** 서버가 `AlignToAttack` 완료를 확인하기 전에는 Windup과 공격 쿨다운을 시작하지 않는다.

확정할 값:

- 공격 허용 각도 오차
- 타겟이 이동할 때 Windup 중 추적 회전 허용 범위
- 일반 공격은 Windup 중 잠긴 타겟을 서버가 계속 추적하고 각 Impact 순간 권위 방향을 기록할지
- 고정 착탄형은 발사 순간 권위 착탄점과 방향을 고정할지
- 유도 투사체는 서버에서 어떤 방식으로 타겟을 추적할지

**결정:** 승인됨. 공격 진입 5° / 유지 8°. 일반 공격은 잠긴 타겟을 Windup 중 추적하고 Impact 방향을 서버가 기록한다. LockedPoint는 발사 시 착탄점·방향 고정, Homing은 서버 추적이다.

### Gate 4. 타겟 변경과 공격 커밋

**권장안:** Windup 전에는 자유롭게 타겟을 변경하고, Windup 이후에는 동일 시퀀스의 `TargetId`를 유지한다. 타겟 잠금은 방향 고정과 구분한다. 타겟이 사망·무효화되면 공격 유형별 취소 또는 빗나감 규칙을 적용한다.

확정할 내용:

- 근접 공격의 사거리 이탈 처리
- Hitscan의 발사 순간 유효성 검사
- ProjectileImpact의 발사 후 타겟 추적 또는 고정 착탄점
- 공격 취소 시 쿨다운 소비 여부

**결정:** 승인됨. 커밋 전 취소는 무비용, 커밋 후 빗나감·취소는 쿨다운 환불 없음. MeleeContact·Hitscan은 Impact별 유효성 재검증, 발사된 ProjectileImpact·TravelingArea는 독립 진행한다.

### Gate 5. 공격 시퀀스와 타격 결과

**권장안:** 서버가 다음 데이터를 포함하는 권위 공격 시퀀스를 생성한다.

- 공격자 ID
- `AttackSequenceId`
- 타겟 ID 또는 권위 착탄 위치
- 공격 유형
- 타격 순간별 권위 공격 방향 또는 발사 시 고정된 권위 착탄 방향·위치
- 서버 시작 시각
- 타격별 `HitIndex`와 권위 타격 시각
- 취소·빗나감·적중 및 대상별 결과

클라이언트는 이 데이터를 재생하며, 로컬 애니메이션 상태나 FIFO 순서로 공격 결과를 추측하지 않는다.

**결정:** 승인됨. 상태 스냅샷과 실제 Impact 결과를 분리하며 `AttackSequenceId + HitIndex`를 상관키로 사용한다.

### Gate 6. 시각 표현 계약

**권장안:** Animation Event는 VFX·SFX·카메라·발사점 표식에만 사용한다. 실제 결과 표현은 `AttackSequenceId + HitIndex`에 연결한다.

- 로컬 이벤트가 먼저 또는 나중에 도착해도 같은 타격에 결합한다.
- 중복 결과는 멱등 처리한다.
- 타임아웃은 정상 동기화 수단이 아니라 오류 복구 및 계측 수단으로만 사용한다.
- ProjectileImpact는 권위 착탄 시각을 중심으로 폭발·피격 표현을 재생한다.

**결정:** 승인됨. Simulation Root와 Visual Root를 분리하고 Animation Event는 표현·검증 marker로만 사용한다. FIFO는 대체 대상이다.

### Gate 7. 쿨다운과 회복 구간

**권장안:** 공통 용어를 `Windup / Impact / Recovery / Interval`로 정의하고 각 유닛 데이터가 어떤 구간을 포함하는지 명시한다.

BloomFairy의 별도 쿨다운 의미가 의도된 게임 감각인지 역사적 예외인지 확인한 뒤, 의도된 경우에도 공통 타임라인 필드로 표현한다.

**결정:** 승인됨. 일반 쿨다운은 Align을 제외한 커밋→다음 커밋 전체 주기다. BloomFairy는 성공 Impact 후 3초 쿨다운, Windup 포함 총 4초 예외를 유지한다.

---

## 4. 현행 규칙별 처리안

| 현행 규칙 | 처리 | 개정 방향 |
|---:|---|---|
| 3, 6 | 유지 | 서버 권위 및 좌표 계약 명확화 |
| 7, 8 | 재작성 | AlignToMove 상태와 이동 허용 오차 추가 |
| 9 | 결정 후 수정 | 감지 거리 `>` 또는 `>=` 의미 확정 |
| 10 | 확장 | 정렬·준비·타격·회복 상태 추가 |
| 12, 15 | 재작성 | 목표 방향, 권위 방향, 공격 게이트 분리 |
| 13 | 재작성 | 최초·최근접·우선순위 타겟 의미 정의 |
| 17 | 재작성 | 권위 타격 데이터와 Animation Event 역할 분리 |
| 18 | 유지·일반화 | 서버 예약 타격 시각으로 표현, 0.05초는 기술 세부로 이동 |
| 19 | 폐기·대체 | FIFO를 시퀀스/타격 번호 상관관계로 대체 |
| 20 | 재작성 | Hitscan / ProjectileImpact / 이동 영역형 분리 |
| 21 | 현행 규범에서 제거 | 구현 역사 또는 폐기 기록으로 이동 |
| 22 | 확장 | 애니메이션 값이 아닌 행동 시퀀스 계약 추가 |
| 23~25 | 분리 | 게임 의미는 Units, 클래스·파일은 TDD 또는 작업 문서로 이동 |
| 26 | 폐기·대체 | AoE도 동일 시퀀스의 대상별 결과로 묶음 |
| 27 | 사실 수정 | 완료 주장 제거, 완성도 표로 이동 |
| 28~40 | 재검토 | 특수 유닛 게임 의미와 구현 세부를 분리 |

---

## 5. 실제 문서 수정 순서

1. Gate 1~7을 사용자와 하나씩 확정한다.
2. 확정된 용어와 불변 조건을 `GameSystemRules_Units.md`에 새 안정 ID로 반영한다. 예: `U-MOV-ALIGN`, `U-COMBAT-IMPACT`, `NET-ACTION-SEQ`.
3. 멀티플레이 동기화 계약 문서를 신설한다.
4. 유닛별 공격 완성도 표를 신설하고 실제 에셋 상태를 기록한다.
5. `GameSystemRules.md` 인덱스에 신규 문서를 등록한다.
6. `TechnicalDesignDocument.md`에서 서버 권위 행동 시퀀스와 Simulation/Visual Root 경계를 연결한다.
7. 기존 규칙 번호를 참조하는 문서를 전수 검색해 새 규칙 링크로 교체한다.
8. 문서 간 모순, 폐기 규칙 잔존, 완료되지 않은 유닛의 완료 표기를 검증한다.

기존 규칙 번호는 과거 작업 문서와 이력의 참조를 보호하기 위해 즉시 재번호화하지 않는다. 1차 개정에서는 `유지 / 대체됨 / 폐기됨` 상태와 새 안정 ID를 함께 표시하고, 기존 번호를 Legacy alias로 보존한다.

---

## 6. 예정 문서 변경 범위

### 수정 예정

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/GameSystemRules.md`
- `Assets/_Project/Docs/TechnicalDesignDocument.md`

### 신규 예정

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`

### 규칙 개정 완료 후 정합성 갱신 예정

- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`
- `AGENTS.md`
- 새 도메인 용어 확정 시 `CONTEXT.md`
- 관련 에이전트 MEMORY 문서

파일명과 분리 범위는 규칙 결정 과정에서 조정할 수 있다. 변경이 생기면 이 Plan의 변경 기록에 이유를 남긴다.

---

## 7. 위험 요소와 방어책

| 위험 | 방어책 |
|---|---|
| 규칙 문구만 이상적이고 현재 구조로 구현 불가능 | 각 Gate에서 현재 서버 상태·네트워크 데이터로 표현 가능한지 검증 |
| 규칙 개정 중 기존 특수 유닛 의도 손실 | 유닛별 완성도 표와 예외 근거를 함께 보존 |
| 규칙 번호 변경으로 다른 문서 참조 파손 | 전수 검색 후 새 섹션 링크로 교체, 번호보다 고유 용어 우선 |
| Animation Event를 다시 서버 판정 트리거로 사용 | 서버 결과 권위와 표현 표식의 역할을 별도 규칙으로 명시 |
| FIFO 제거 전에 기존 코드 삭제 | 규칙 승인 → 계측 → 신규 시퀀스 병행 → 검증 후 제거 순서 유지 |
| 미완성 유닛을 공통 시스템 결함으로 오판 | 미완성·미교정·검증 완료 상태를 분리 |

---

## 8. 규칙 개정 승인 체크포인트

규칙 문서 수정은 다음 조건을 만족한 뒤 시작한다.

- [x] Gate 1 공격 판정 모델 승인 — 2026-07-20, `U-COMBAT-DELIVERY` 반영
- [x] Gate 2 이동 방향 정렬 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] Gate 3 공격 방향 정렬 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] Gate 4 타겟 변경과 공격 커밋 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] Gate 5 서버 공격 시퀀스 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] Gate 6 시각 표현 계약 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] Gate 7 쿨다운 의미 승인 — 일괄 개정 요청에 따라 권장안 반영
- [x] 문서 분리 구조 승인 — Units / Synchronization / AssetMatrix 분리

---

## 9. 규칙 승인 후 후속 작업

규칙 개정 완료 후 별도의 구현 Research/Plan을 작성한다. 그 후 다음 순서를 따른다.

1. 현재 구조 계측 및 재현 기준선 확보
2. Simulation Root / Visual Root 분리
3. 서버 행동·공격 시퀀스 도입
4. 기존 FIFO와 병행하는 shadow mode 검증
5. 유닛별 공격 유형 및 타격 데이터 이전
6. Host/Client, Blue/Red, 지연·지터·패킷 손실·다수 유닛 검증
7. 검증을 통과한 뒤 기존 봉합 로직 제거

---

## 변경 기록

- 2026-07-20: 최초 계획 작성. 규칙 개정과 코드 구현을 별도 단계로 분리함.
- 2026-07-20: 사용자 요청으로 Gate별 중간 확인을 중단하고 권장안 전체를 단일 문서 변경 세트로 확정함.

---

## 완료 결과 (2026-07-20)

- Gate 1~7을 단일 규칙 세트로 확정하고 Units / Synchronization / AssetMatrix 구조로 일괄 반영했다.
- 서버 권위와 Clean Architecture는 유지하고, Simulation Root / Visual Root, ActionSequence, AttackTimeline, 결과 상관키와 전환기의 single-writer / single-emitter 계약을 명문화했다.
- 25종 에셋·설정 감사를 문서화했으며 QuakeSpirit 설정 누락, 기본 Attack marker 누락 4종, 다수 타이밍 불일치와 VFX 프리셋 미연결을 Complete 차단 항목으로 기록했다.
- 과거 FIFO·Animation Event 기준 완료 표기는 Legacy 완료 / v2 재검증으로 정정했다.
- 코드·프리팹·애니메이션 에셋은 수정하지 않았다. 따라서 이번 완료는 규칙과 구현 기준선의 완료이며 런타임 교정 완료가 아니다.
- 사용자가 TC/QA 작성을 요청하지 않았고 런타임 구현도 범위가 아니므로 `Testcase.md`는 만들지 않았다. 대신 로컬 Markdown 링크와 `git diff --check`를 검증했다.
