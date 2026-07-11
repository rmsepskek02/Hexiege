# Plan — 전투 타격 타이밍 동기화 (combat-hit-timing-sync)

작성일: 2026-07-09
작업 폴더: `Assets/_Project/Docs/_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`
선행 문서: 같은 폴더의 `Research.md`

---

## ⚠️ 기존 로직 제거/비활성화 대상 (WORKFLOW 최상단 명시 규칙)

아래 항목은 이번 작업에서 정리 대상이다. **검증(사용자 실기 테스트) 통과 전까지는 "삭제"가 아니라 "비활성화(주석 처리) 유지"를 기본으로 한다.** 최종 삭제는 WORKFLOW [6] 테스트 통과 후, [7] 문서/메모리 업데이트 전에 수행한다.

| # | 대상 | 처리 방식 | 안전 근거 |
|---|------|-----------|-----------|
| A | `HitFrameTimes`의 **수동 입력 의존** (UnitStatsConfig 입력값 → UnitStats.GetHitFrameTimes) | Phase 1에서 클립 이벤트 자동 추출로 대체. 수동 입력 필드/경로는 **당장 삭제하지 않고**, 자동값이 검증될 때까지 폴백(자동 실패 시 기존 값 사용)으로 남긴다. | 자동 추출이 클립 `OnAttackHit` 이벤트에 의존하는데, 이벤트 누락 유닛이 있을 수 있어(Research 위험요소 1) 전수 검사 통과 전 수동값 폴백이 안전망 역할을 한다. |
| B | `Presentation/Unit/UnitEffectView.cs` (이미 전체 주석, DEPRECATED 2026-06-08) | Phase 2에서 신규 **피격 표현 큐**가 그 역할(피격 VFX)을 정식 대체함을 확인한 뒤 파일 삭제 + 프리팹에서 컴포넌트 제거. | 이미 비활성 상태라 런타임 영향 없음. 신규 파이프라인이 멀티 클라이언트 양쪽에서 피격 연출을 재생함을 실기 확인해야 "기존 버그(서버 전용 구독으로 클라 VFX 미표시)"가 재발하지 않음이 보장됨. |
| C | `EffectManager.cs`의 SFX 관련 주석 블록(`SOUND_SYSTEM_REFACTOR`) | **이번 작업 범위 아님.** 별도 사운드 리팩토링 검증 태스크 소관이므로 그대로 둔다. | 범위 초과 금지(CLAUDE.md 규칙 6). |

> 코드 수정은 이 Plan을 사용자가 승인한 뒤 game-programmer 에이전트가 수행한다. 본 문서는 계획만 기술한다(코드 미수정).

---

## 이 작업이 무엇이고 왜 하는가 (비개발자용 설명)

Research에서 확인한 세 가지 어긋남과 연출 공백을 **네 개의 축**으로 나눠 해결한다.

1. **타이밍의 출처를 하나로 통일한다.** 지금은 "화면 타격 순간"과 "데미지 순간"이 서로 다른 두 곳에 적혀 있어 어긋난다. 앞으로는 화면 애니메이션에 찍힌 타격 순간 하나만을 기준으로 삼고, 데미지도 그 값을 그대로 따라가게 한다.
2. **서버가 데미지를 주는 시점을 더 정밀하게** 만든다. 지금은 최대 50ms의 오차가 매 공격마다 생기는데, 이 오차를 계산해서 빼주어 없앤다.
3. **맞는 쪽의 연출을 새로 만든다(핵심).** 서버가 "누가 누구를 때렸고 남은 체력이 얼마"인지 알려주면, 맞은 유닛 화면에서는 그 정보를 잠깐 대기시켰다가 **공격자가 실제로 칼을 휘두르는 순간에 맞춰** 체력 숫자·피격 이펙트·움찔 반응을 함께 터뜨린다. 체력 데이터 자체는 서버가 준 즉시 반영하되(권위 유지), 보여주는 타이밍만 맞춘다.
4. **연출 공백을 메운다.** 타워에 발사 이펙트를, 원거리 유닛에 날아가는 발사체 연출을 추가하고, 맞은 유닛은 3번의 피격 연출을 재사용한다.

작업은 **Phase 1 → 2 → 3** 순서로 진행하며 각 Phase는 독립적으로 테스트할 수 있게 설계한다.

---

## 설계 개요 (4개 축)

- **축 1 — 타이밍 소스 단일화**: `HitFrameTimes`를 클립의 `OnAttackHit` Animation Event 시간에서 자동 추출.
- **축 2 — 서버 데미지 타이밍 정밀화**: `TickCombat`의 50ms 격자 오버슈트를 데미지 코루틴 딜레이에서 차감.
- **축 3 — 피격 표현 큐(Hit Presentation Queue) 신설**: 공격자 Id를 이벤트/RPC에 추가하고, 클라이언트 연출을 공격자의 로컬 `OnAttackHit`에 동기화.
- **축 4 — 연출 공백 보강**: 타워 발사 VFX + 원거리 트레이서 + 피격 큐 재사용.

---

## Phase 1 — 타이밍 소스 단일화 + 서버 데미지 정밀화 (축 1 + 축 2)

### 1-1. HitFrameTimes 자동 추출 (축 1)

**변경 내용**
- `UnitFactory.CreateUnit`에서 이미 하는 `GetAttackClipLength(animator)` 패턴을 확장하여, Attack 클립의 `AnimationClip.events` 중 함수 이름이 `OnAttackHit`인 이벤트들의 `time`(초)을 오름차순으로 수집하는 `GetHitFrameTimes(animator)`(가칭)를 신설한다.
- `CreateUnit`에서 추출 결과가 1개 이상이면 `unitData.HitFrameTimes`에 대입(클립 length로 `AttackCooldown`을 덮어쓰는 것과 동일한 위치·방식). 추출이 비어 있으면 기존 `UnitStats.GetHitFrameTimes()`(수동/안전망) 값을 **폴백으로 유지**(제거 대상 A의 안전망).
- 에디터 1회성 검증 스크립트(메뉴 `Hexiege/Combat/Validate Attack Hit Events` 가칭): 전 종족·전 유닛 프리팹의 Attack 클립을 열어 `OnAttackHit` 이벤트 유무·개수를 리포트(Console 또는 텍스트)로 출력. Inspector 보정이 필요한 유닛을 특정하기 위함(WORKFLOW [5-2] 절차).

**근거 규칙**
- **규칙 신설 필요** — 현재 GameSystemRules_Units에는 "타격 프레임 타이밍의 출처"를 규정한 규칙이 없다. 아래 **[신설 규칙 초안 U-17]** 참조.
- 관련: 유닛 규칙 14(공격 중 이동 금지), 규칙 16(AoE는 데미지 계산 차이일 뿐 상태 전환 동일) — 다중 히트/AoE 유닛도 동일 파이프라인을 쓴다는 기존 원칙과 정합.

### 1-2. 서버 데미지 오버슈트 차감 (축 2)

**변경 내용**
- `NetworkCombatController.TickCombat`에서 쿨다운 만료를 감지한 순간, "만료 후 이번 Tick까지 초과 경과한 시간(오버슈트)"을 계산한다. 유닛의 `AttackCooldownRemaining`이 이번 `elapsed` 차감으로 0 밑으로 내려간 크기가 곧 오버슈트다.
- `ExecuteAttack` → `DelayedAttackDamage`로 넘기는 각 `hitTime` 딜레이에서 이 오버슈트를 차감(`Mathf.Max(0, hitTime - overshoot)`)하여, 격자 오차가 데미지 시점에 누적되지 않게 한다.
- 싱글플레이 `UnitCombatUseCase.TickPendingHits`도 동일 원리 적용을 **검토**한다. 단 싱글은 매 프레임 호출이라 오차가 프레임 시간(≤16.7ms) 수준으로 작으므로, 우선순위는 낮게 두고 멀티 우선.
- **데미지는 계속 서버 타이머로만 적용**한다. Animator 상태(`OnAttackHit`)에 데미지를 종속시키지 않는다(데미지 누락 방지).

**근거 규칙**
- 건물 규칙과 무관, 유닛 전투 타이밍 정밀화 사항 → **규칙 신설 필요** — 아래 **[신설 규칙 초안 U-18]** 참조.
- 관련: 방어 타워 규칙 9 / 유닛 서버 권위 처리(멀티 데미지는 서버만)와 정합 — 서버 권위를 강화하는 방향이므로 기존 규칙과 충돌 없음.

### Phase 1 예상 수정 파일

| 파일 | 변경 |
|------|------|
| `Infrastructure/Factories/UnitFactory.cs` | `GetHitFrameTimes(animator)` 추가, `CreateUnit`에서 `HitFrameTimes` 자동 대입(+폴백) |
| `Infrastructure/Network/NetworkCombatController.cs` | `TickCombat`/`ExecuteAttack`에 오버슈트 차감 전달 |
| `Application/UseCases/UnitCombatUseCase.cs` | (검토) `TickPendingHits` 오버슈트 반영 |
| (신규) `Editor/CombatHitEventValidator.cs` (가칭, 1회성) | 전 유닛 Attack 클립 `OnAttackHit` 전수 검사 리포트 |

---

## Phase 2 — 피격 표현 큐 신설 (축 3, 핵심)

### 2-1. 공격자 Id 전달 경로 확장

**변경 내용**
- `EntityDamagedEvent`(Application/Events/GameEvents.cs)에 **공격자 Id + 공격자가 유닛인지 여부**를 추가한다. 발행처(`UnitCombatUseCase.ExecuteAttack`, `TowerCombatUseCase.ExecuteTowerAttack`)가 공격자 정보를 함께 실어 발행한다.
- `NetworkHealthSync.SyncHealthClientRpc`에 공격자 Id 파라미터를 추가하고, 클라이언트 재발행 시 `EntityDamagedEvent`에 그대로 전달한다.
- 레이어 준수: 공격자 Id 전달은 **Application 이벤트 + Infrastructure RPC** 확장만으로 이뤄지며, Infrastructure→Presentation 역참조는 없다(기존처럼 GameEvents 경유).

**근거 규칙**
- 유닛 규칙 13(타겟 선택) — "공격자-타겟" 관계는 이미 도메인이 관리하므로 이벤트에 실어 나르는 것은 자연스러운 확장.
- **규칙 신설 필요** — 피격 표현 큐 자체는 신규 개념 → **[신설 규칙 초안 U-19]** 참조.

### 2-2. 피격 표현 큐 (Presentation, 신설)

**변경 내용**
- 신규 Presentation 컴포넌트(가칭 `HitPresentationQueue`)를 만든다. `OnEntityDamaged`를 구독하되, **도메인 HP는 이미 즉시 갱신된 상태**(권위 유지)이고 이 큐는 오직 "표현"만 담당한다.
- 도착한 표현 정보 `{공격자 Id, 타겟 Id, 남은 HP, 팀}`를 공격자별 큐에 보류한다.
- 공격자의 로컬 `UnitView.OnAttackHit`가 발생하는 순간 해당 공격자의 큐를 방출(FIFO 1건)하여 아래를 동시에 실행한다.
  - HP 텍스트: 현재 `FloatingHpTextSpawner`가 `OnEntityDamaged` 즉시 표시하는 것을, 큐 방출 시점 표시로 전환(방식은 구현 시 결정 — 큐가 직접 스폰 호출 또는 지연 이벤트 재발행).
  - 피격 VFX: `EffectManager`에 **`PlayUnitHit(UnitType, pos)` 신설** 후 호출.
  - 타격 반응: 피격 유닛의 스케일 펀치 또는 플래시(짧은 비주얼 반응).
- **안전망(필수)**: ⓐ **타임아웃** — 공격 사이클 1회분(해당 유닛 `AttackCooldown`) 경과해도 `OnAttackHit`가 오지 않으면 큐를 즉시 방출(연출 유실 방지). ⓑ **타겟 사망** — `OnUnitDied`/`OnBuildingDied` 수신 시 해당 타겟에 대한 잔여 큐를 즉시 방출한 뒤 사망 연출로 넘어간다.
- `UnitEffectView`(DEPRECATED)의 피격 VFX 역할을 이 파이프라인이 정식 대체 → 제거 대상 B 실행 근거.

**VFX+SFX 쌍 규칙 검토 (Sound 규칙 15)**
- `EffectManager.PlayUnitHit`(VFX)를 호출하면 **바로 아래 줄에 대응 SFX**가 있어야 한다(Sound 규칙 15). 피격 SFX(`AudioManager.PlayUnitHitSfx` 가칭)를 짝으로 추가할지, 아니면 피격은 VFX만 두고 SFX 없음을 주석으로 명시할지는 **game-design-lead 확인이 필요한 판단 지점**(현재 SoundConfig에 피격 SFX 엔트리 유무 미확인 — Plan 승인 후 확인). 규칙 14(멀티 SFX는 로컬 재생)에 따라 큐 방출은 각 클라이언트 로컬에서 일어나므로 SFX 동기화는 불필요.

**근거 규칙**
- 유닛 규칙 14/15(공격 중 이동 금지·타겟 방향 유지)와 정합 — 큐는 공격자 상태를 바꾸지 않고 표현만 얹는다.
- Sound 규칙 1(EffectManager는 VFX 전용), 규칙 15(VFX+SFX 쌍), 규칙 14(SFX 로컬), 규칙 13(SFX 동시 8개 — 피격 SFX 추가 시 한도 영향 검토).
- **규칙 신설 필요** — **[신설 규칙 초안 U-19]** 참조.

### Phase 2 예상 수정 파일

| 파일 | 변경 |
|------|------|
| `Application/Events/GameEvents.cs` | `EntityDamagedEvent`에 공격자 Id/유닛여부 추가 |
| `Application/UseCases/UnitCombatUseCase.cs` | `ExecuteAttack` 발행 시 공격자 정보 포함 |
| `Application/UseCases/TowerCombatUseCase.cs` | `ExecuteTowerAttack` 발행 시 공격자(타워) 정보 포함 |
| `Infrastructure/Network/NetworkHealthSync.cs` | `SyncHealthClientRpc` 공격자 Id 파라미터 추가 + 재발행 반영 |
| (신규) `Presentation/Effects/HitPresentationQueue.cs` (가칭) | 피격 표현 큐 |
| `Presentation/Effects/EffectManager.cs` | `PlayUnitHit(UnitType, pos)` API 추가 |
| `Presentation/UI/FloatingHpTextSpawner.cs` | HP 텍스트 표시 시점을 큐 방출과 연동 |
| `Presentation/Unit/UnitView.cs` | `OnAttackHit`에서 큐 방출 트리거 발행(자기 Id) |
| (필요 시) `Presentation/Audio/AudioManager.cs` + SoundConfig | 피격 SFX 짝 (design-lead 확인 후) |
| **삭제 예정** `Presentation/Unit/UnitEffectView.cs` | 검증 후 파일 삭제 + 프리팹 컴포넌트 제거 (제거 대상 B) |

---

## Phase 3 — 연출 공백 보강 (축 4)

### 3-1. 타워 발사 연출 + 피격

**변경 내용**
- `BuildingEffectConfig`에 **공격(발사) 프리셋 슬롯(`attackPreset`)** 추가 + `GetAttack(BuildingType)` 신설. `EffectManager`에 `PlayBuildingAttack(BuildingType, pos, rot)` 신설.
- 타워는 **즉발 유지**(히트 딜레이 도입하지 않음). 서버가 `OnEntityAttacked`(공격자=타워)를 발행할 때 각 클라이언트 로컬에서 발사 VFX를 재생하도록 연결(구독처 신설 또는 큐 확장). 맞은 유닛의 피격 연출은 축 3 큐를 **즉시 방출**(타워는 공격자 애니메이션 타격 프레임이 없으므로 타임아웃/즉시 방출 경로 사용).

**근거 규칙**
- 방어 타워 규칙 4(배치 직후 즉시 첫 공격)·규칙 5(공격 후 쿨다운)·규칙 9(서버 권위) — 데미지 흐름은 그대로 두고 연출만 얹으므로 기존 규칙 유지.
- Sound 규칙 15(VFX+SFX 쌍) 준수.
- **규칙 신설 필요** — 타워 발사 연출 규정 → **[신설 규칙 초안 B-12]** 참조.

### 3-2. 원거리 유닛 트레이서

**변경 내용**
- 원거리 유닛(AttackRange ≥ 1.0)의 `OnAttackHit` 시점에 **연출 전용 트레이서**(발사 → 비행 → 착탄)를 생성. 데미지 타이밍(서버)은 **불변**이며, 트레이서는 순수 시각 표현이다. 착탄 시점에 피격 표현(축 3 큐)을 방출하도록 연결.
- 트레이서 비행 시간은 짧게(연출용)이며 데미지 판정과 분리한다.

**근거 규칙**
- 유닛 규칙 9(근접/원거리 차이는 공격 사거리·공격 방식에만 있고 상태 전환은 동일) — 트레이서는 "공격 방식"의 시각 표현이므로 정합.
- **규칙 신설 필요** — 원거리 트레이서 규정 → **[신설 규칙 초안 U-20]** 참조.

### Phase 3 예상 수정 파일

| 파일 | 변경 |
|------|------|
| `Presentation/Effects/BuildingEffectConfig.cs` | `attackPreset` 슬롯 + `GetAttack` |
| `Presentation/Effects/EffectManager.cs` | `PlayBuildingAttack`, 트레이서 재생 API |
| `Presentation/Unit/UnitView.cs` | 원거리 유닛 `OnAttackHit` 시 트레이서 생성 + 착탄 시 피격 큐 방출 |
| (신규) 타워 공격 VFX 구독처 또는 큐 확장 | `OnEntityAttacked`(타워) → 로컬 발사 VFX |

---

## 아키텍처 제약 준수 체크

- 큐·트레이서·발사 VFX는 전부 **Presentation** 레이어에 둔다.
- 공격자 Id 전달은 **Application 이벤트**(`EntityDamagedEvent`) + **Infrastructure RPC**(`SyncHealthClientRpc`)에서만 이뤄진다.
- **Infrastructure → Presentation 역참조 금지**: 서버 이벤트는 반드시 `GameEvents`를 경유해 Presentation이 구독한다.
- `EffectManager`는 VFX 전용 유지(Sound 규칙 1). SFX는 항상 `AudioManager`에 짝 호출(Sound 규칙 15).
- 데미지 판정은 서버 권위 불변(멀티 규칙 9). 데미지를 Animator 상태에 종속시키지 않는다.

---

## 위험 요소 (Plan 관점 재정리)

1. 클립에 `OnAttackHit` 이벤트가 없는/개수가 틀린 유닛 → 자동 추출 실패. Phase 1 검증 스크립트 리포트에 따라 Inspector에서 클립 이벤트 보정 작업이 선행되어야 할 수 있음.
2. `SyncHealthClientRpc` 시그니처 변경 → 서버·클라 빌드 동시 갱신 필요(호환성).
3. 피격 큐 타임아웃/사망 방출 로직 누락 시 HP 표시 유실 위험 → 안전망 필수 구현.
4. 피격 SFX 도입 시 동시 재생 한도(Sound 규칙 13, 8개) 압박 가능 → 대규모 전투에서 드랍 빈도 확인 필요.
5. 멀티 Host/Client 양쪽 실기 검증 필수(에이전트 단독 실기 불가 TC 존재).

---

## 신설 규칙 초안 (근거 규칙이 없는 신규 사항 — 승인 시 GameSystemRules에 반영)

> 아래는 문안 초안이다. Plan 승인 및 구현·검증 완료 후 document-manager가 해당 파일에 정식 반영한다.

**[U-17] 타격 프레임 타이밍의 단일 출처** (GameSystemRules_Units, 전투 연계 규칙에 추가)
> 공격의 타격 시점(`HitFrameTimes`)은 유닛 Attack 애니메이션 클립의 `OnAttackHit` Animation Event 시간을 유일한 출처로 한다. 유닛 생성 시 클립 이벤트에서 자동 추출하며, 수동 입력값은 클립에 이벤트가 없을 때만 폴백으로 사용한다. 다중 히트 유닛은 클립의 여러 `OnAttackHit` 이벤트 시간을 오름차순으로 모두 수집한다.

**[U-18] 서버 데미지 타이밍 정밀화** (GameSystemRules_Units)
> 멀티플레이 서버의 전투 Tick(50ms 격자)에서 쿨다운 만료를 감지할 때, 만료 후 초과 경과한 시간(오버슈트)을 데미지 딜레이에서 차감하여 격자 오차 누적을 제거한다. 데미지는 항상 서버 타이머로만 적용하며 Animator 상태에 종속시키지 않는다.

**[U-19] 피격 표현 큐** (GameSystemRules_Units, 신규 섹션 후보)
> 도메인 HP는 서버 값 도착 즉시 갱신한다(권위 유지). 단 피격 연출(HP 텍스트·피격 VFX·타격 반응)은 공격자의 로컬 `OnAttackHit` 시점까지 보류했다가 방출한다. 공격 사이클 1회분 경과(타임아웃) 또는 타겟 사망 시에는 잔여 연출을 즉시 방출한다. 피격 VFX에는 Sound 규칙 15에 따라 대응 SFX를 짝으로 두거나, 없을 경우 주석으로 명시한다.

**[U-20] 원거리 유닛 트레이서** (GameSystemRules_Units)
> 원거리 유닛은 `OnAttackHit` 시점에 연출 전용 발사체(트레이서: 발사→비행→착탄)를 재생한다. 트레이서는 순수 시각 표현이며 데미지 판정 타이밍(서버)에 영향을 주지 않는다. 착탄 시점에 피격 표현 큐를 방출한다.

**[B-12] 타워 발사 연출** (GameSystemRules_Buildings, 방어 타워 시스템)
> 방어 타워는 즉발 데미지를 유지한다. 서버가 공격 이벤트를 발행할 때 각 클라이언트 로컬에서 발사 VFX(`BuildingEffectConfig.attackPreset`)를 재생하고, 맞은 유닛의 피격 표현은 즉시 방출한다. Sound 규칙 15(VFX+SFX 쌍)를 준수한다.

---

## 승인 요청

- 위 Phase 1~3 계획과 신설 규칙 초안 4건(U-17~U-20, B-12)에 대한 승인을 요청합니다.
- 승인 시 실제 코드 구현은 **game-programmer** 에이전트에 위임하며(CLAUDE.md 규칙 3), 피격 SFX 도입 여부는 **game-design-lead** 확인 후 결정합니다.
- 제거 대상 A/B는 검증 통과 전까지 비활성화/폴백을 유지합니다(C는 범위 밖).

---

## 추가 수정 (검증 로그 분석 후속, 2026-07-11) — 승인 완료

Phase 1~3 구현과 클립 이벤트 주입을 마친 뒤, 임시 계측 로그(Research 7절 참조)를 분석한 결과 원 목표였던 "데미지-연출 불일치"의 숨은 근본 원인이 서버 Tick의 경과 시간 이중 계산에 있음을 확인했다. 아래 두 가지 후속 수정을 진행한다. 코드 수정은 **game-programmer** 에이전트가 수행한다.

### 수정 1 [필수] — Tick 경과 시간 이중 계산 수정

**대상**: `Infrastructure/Network/NetworkCombatController.cs`

- 현재 `Update()`는 이월 잔여분이 다음 Tick의 경과 시간(elapsed)에 다시 포함되어, 쿨다운이 실제 경과 시간보다 15~25% 빠르게 소진된다(로그 증거: Pistoleer 쿨다운 2.0초 대비 실측 사이클 간격 1.71초).
- **마지막 Tick 처리 시각 기준의 실제 경과 시간(realElapsed)** 으로 쿨다운을 감소시키도록 `Update()`를 수정한다. 이월분은 Tick 발화 주기(cadence)를 맞추는 데에만 사용하고, 쿨다운을 깎는 경과 시간 계산에서는 제외한다.
- **근거**: 신설 규칙 초안 U-18(서버 데미지 타이밍 정밀화)의 전제 — 쿨다운 감소는 실제 경과 시간과 1:1이어야 한다.

### 수정 2 [보강] — 피격 표현 큐 공격자 기준 방출

**대상**: `Presentation/Effects/HitPresentationQueue.cs`

- 현재는 타겟 사망 시에만 잔여 큐를 방출한다. 여기에 **공격자 사망 시** + **공격자의 전투 중단(StopCombat 이벤트) 시**에도 해당 공격자의 보류 항목을 즉시 방출하도록 확장한다.
- 목적: 위상 밀림이나 전투 중단으로 `OnAttackHit`가 오지 않을 때 최대 쿨다운만큼 표시가 지연되던 타임아웃 대기를 제거한다.
- **근거**: 신설 규칙 초안 U-19(피격 표현 큐)의 안전망 조항 확장.

### 재검증 계획

수정 후 동일한 임시 계측 로그로 재테스트한다. 판정 기준은 다음과 같다.
- 타임아웃 방출 건수가 **0에 수렴**할 것.
- 타격프레임 방출 대기 시간의 꼬리(p95)가 **대폭 감소**할 것.
- 실측 사이클 간격이 각 유닛의 **쿨다운 값과 일치**할 것.

재검증 통과 후 임시 계측 로그 코드는 제거하고, 제거 대상 A/B 최종 정리 및 신설 규칙(U-17~U-20, B-12) GameSystemRules 반영을 진행한다.
