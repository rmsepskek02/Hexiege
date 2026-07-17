# Plan — TorrentSpirit 파도형 AoE 구현

## 이 계획이 무엇인지 (자연어 설명)

TorrentSpirit이 전방으로 물의 파도를 일으켜, 파도가 지나가며 닿는 적에게는 피해(20), 아군에게는
회복(10)을 주는 기능을 만듭니다. 도끼병에서 만든 특수 공격 구조(전략 핸들러) 위에, 이 유닛에 필요한
**세 가지 신규 요소** — ① 회복(힐) 서브시스템, ② "파도가 유일한 공격"인 special-only 모델,
③ 전방으로 이동하는 파도(서버 권위 타이밍) — 를 얹습니다.

여기서 만드는 **힐 서브시스템은 BloomFairy(힐러)에도 재사용**되고, 이동 파도 패턴도 이후 유닛에
활용됩니다. 이번 단계는 TorrentSpirit만 구현합니다.

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW [4])

**기존 로직을 제거하지 않는다.** special-only 분기는 기존 `ExecuteAttack`의 주 타깃 단일 피해 경로를
**조건부로 건너뛰는** 것이며, 일반 유닛과 도끼병의 동작은 그대로 유지된다. 힐/파도는 순수 추가.

---

## 근거 규칙 (GameSystemRules)

| 규칙 | 적용 |
|------|------|
| 규칙 16 (범위 공격 AoE) | 이동/상태 머신 무변경, 대미지/효과 계산만 확장. 아군엔 피해 없음(힐만). |
| 규칙 18 (서버 데미지 타이밍) | 파도 효과는 **서버가 파도 전선 전진을 모델링**해 닿는 서버 시각에 적용. 클라 파티클 위치에 종속 금지. |
| 규칙 20 (원거리 트레이서) | 이동 파도 연출은 로컬 파티클, 연출 방출은 파도 진행에 맞춰(트레이서 패턴 연장). |
| 규칙 23 (전략 핸들러 구조) | `TorrentAttackBehavior` 신규 + 레지스트리 등록. `ExecuteAttack`은 훅만. |
| 규칙 24 (월드 좌표 판정) | 파도는 **월드 좌표 직사각형**(폭 3 × 전방 3, 타겟 방향). |
| 규칙 25 (SpecialAttackConfig 튜닝) | 파도 파라미터(폭·전방길이·피해·힐·이동시간) Inspector 편집. |
| 규칙 26 (AoE 연출 동시 방출) | 파도는 유닛별로 닿는 시점이 달라 동시 방출 아님 — 파도 전용 연출 경로(아래 D-3). |
| 규칙 27 (OnAttackHit 주입) | TorrentSpirit 클립 OnAttackHit 주입(쿨다운·파도 시작 시점 확정 후). |

---

## 설계 결정

### 확정 (사용자, 2026-07-17)
- **D-1** 파도 = 전방 이동, 파티클(`vfx_torrentspirit_attack.prefab`), 닿는 순간 효과.
- **D-2** 적 20 피해 / 아군 10 힐 (아군 무피해). 힐도 아군이 닿는 시점.
- **D-3** 단일 공격 없음 — 파도가 유일한 공격(special-only).
- **D-4** 방향 = 타겟을 바라보는 방향(공격자→주 타깃).
- **D-5** 각 유닛 **무조건 1회만** 적용(아군·적군 공통).
- **D-6** 월드 좌표 직사각형(폭 3 × 전방 3, 타겟 방향 정렬).
- **D-7** 시전자 자신 힐 제외 / 죽은 유닛(양측) 제외.

### 권장 (승인 필요 — 구현 방식 결정)
- **D-8. special-only 구현 = `ISpecialAttackBehavior`에 `ReplacesPrimaryAttack` 플래그**
  - `TorrentAttackBehavior.ReplacesPrimaryAttack = true`. `ExecuteAttack`은 특수 핸들러가 이 플래그면
    **주 타깃 단일 피해(`ApplyDamageToVictim`)를 건너뛰고** 핸들러만 실행. 도끼병(false)은 기존대로.
- **D-9. 파도 타이밍 = 서버 틱 기반 이동 전선 + hit-set**
  - 시전 시 파도(전선)를 생성, **이동 시간(config) 동안 서버 틱으로 전방으로 전진**. 각 틱마다 전선이
    새로 지난 폭 3 밴드 내의 아직 안 맞은 유닛에 효과 1회 적용(hit-set로 중복 방지). "닿을 때/1회만"과
    움직이는 유닛을 자연스럽게 반영. (대안: 시전 시 3×3 스냅샷 후 유닛별 도달 시각 스케줄 — 더 단순하나
    파도 도중 진입한 유닛 누락. game-programmer가 서버 권위·복잡도 균형으로 최종 방식 확정.)
- **D-10. 힐 이벤트 = 신규 `OnEntityHealed`**
  - 피격 이벤트에 부호를 섞지 않고 별도 이벤트로 둠(연출/색상 분리 명확). NetworkHealthSync가 HP 증가
    감지 시 이 이벤트를 클라에 재발행.

---

## 구현 상세 (그룹별)

### A. 힐 서브시스템 (신규 — BloomFairy 공용)
1. **`UnitData.Heal(int amount)`**: `Hp = Min(Hp + amount, MaxHp)`. 죽은 유닛(`!IsAlive`)엔 적용 안 함.
2. **힐 이벤트**: `GameEvents.OnEntityHealed`(엔티티, 회복 후 HP, 힐러 Id 등). 도메인 HP는 서버 즉시 갱신.
3. **멀티 동기화** — `NetworkHealthSync`:
   - 현재 `SyncUnitHealth`는 `diff = Hp - serverHp > 0`(감소)만 처리. **`diff < 0`(증가=힐) 분기 추가**:
     `unit.Heal(-diff)` 후 클라에서 `OnEntityHealed` 재발행. (HP 동기화는 이미 절대값 serverHp를 보내므로
     구조 재사용 — 증가 방향만 열어주면 됨.)
   - 힐도 공격자(힐러) 정보를 실어 연출이 힐러를 알 수 있게.
4. **힐 연출**: `FloatingHpTextSpawner`에 힐 표시 경로(피해와 구분되는 **초록/치유 색상**, 남은 HP 또는 `+10`).
   피격 텍스트와 오브젝트 풀·배치 로직 재사용. VFX는 있으면 연결, 없으면 주석 명시(사운드 규칙 15 준수).

### B. special-only 공격 모델
- `ISpecialAttackBehavior`에 `bool ReplacesPrimaryAttack { get; }` 추가(도끼병=false, Torrent=true).
- `UnitCombatUseCase.ExecuteAttack`:
  ```
  Facing 갱신(타겟 방향)  // 그대로
  var special = _specialAttacks.TryGet(attacker.Type);
  if (special == null || !special.ReplacesPrimaryAttack)
      ApplyDamageToVictim(attacker, target);   // 일반/도끼병: 주 타깃 단일 피해
  special?.Apply(ctx);                          // 특수 훅
  ```
- 주 타깃은 파도가 지나며 처리되므로(다른 적과 동일) special-only여도 주 타깃이 피해를 받는다.

### C. `TorrentAttackBehavior` (신규 — 핵심)
- **판정 영역**: forward = 공격자→주 타깃(월드 XZ). 폭 `sweepWidth`(기본 3), 전방 길이 `waveLength`(기본 3)의
  직사각형. 전선은 공격자 앞에서 시작해 전방으로 `waveTravelTime` 동안 전진(D-9).
- **효과 적용**(전선이 유닛에 닿을 때, 1회):
  - 적(`Team != 공격자`)·생존: `ApplyDamageToVictim`(피해 20). 
  - 아군(`Team == 공격자`)·생존·**시전자 제외**: 힐 헬퍼(10).
  - hit-set(이미 맞은 Id)로 중복 방지(D-5). 순회 중 사망 제거 대비 안전 처리.
- **월드 좌표**: `IEntityPositionProvider`(서버 권위) — 도끼병과 동일 `ResolveWorldPosition` 재사용.
- 이동 전선 진행 로직은 서버에서 동작(단일/멀티 공통 수렴점 기준). 정확한 배치(핸들러 내부 코루틴 vs
  전용 서버 컨트롤러)는 레이어 규칙에 맞춰 game-programmer가 확정.

### D. 컨텍스트 / 설정 확장
- **`SpecialAttackContext`**: 힐 헬퍼(`Action<UnitData,int>` 또는 유사) + 공격자 팀 + 파도 파라미터 전달.
  기존 피해 헬퍼·월드좌표 델리게이트 유지.
- **`SpecialAttackConfig`**: 파도 값 추가 — `waveWidth`(3)·`waveLength`(3)·`waveDamage`(20)·`waveHeal`(10)·
  `waveTravelTime`(튜닝). 도끼병 sweep 값과 공존하므로 **유닛별/특수별 파라미터 그룹으로 구조화**(규칙 25 연장).
  GameBootstrapper가 float로 주입(레이어 규칙). **에셋 생성+GameBootstrapper 배선 필수**(규칙 25 교훈 —
  `CreateSpecialAttackConfigAsset.cs` 재사용/확장).

### E. 이펙트 배선
- `vfx_torrentspirit_attack.prefab`을 TorrentSpirit 파도 파티클로 사용. `UnitEffectConfig`의 TorrentSpirit
  `attackPreset`(또는 전용 프리셋)에 연결. `EffectManager`로 공격 시 재생.
- 파티클이 전방 이동하는지(파티클 자체 방출) GO를 이동시켜야 하는지 프리팹 특성 확인 후 결정. 연출 타이밍은
  파도 진행(서버 모델)과 시각적으로 맞춤(규칙 18·20 — 데미지는 서버, 연출은 로컬).

### F. 스탯 / 등록 / 타격 이벤트
- **UnitStatsConfig**: TorrentSpirit(unitType 18) 스탯 입력(HP100/공격력20/사거리3/감지3/이동0.5/쿨다운/
  hitFrameTimes/생산30·골드400·인구1). 힐량 10은 SpecialAttackConfig 쪽(공격력과 별개).
- **UnitFactory 씬 등록(type 18)·생산 매핑** 확인, 미등록 시 처리(물 3단계 라인 OceanicHeart 등).
- **타격 이벤트**: 쿨다운·파도 시작 시점 확정 후 `Inject OnAttackHit Events`로 클립에 주입(규칙 27).

---

## 위험 요소

| 위험 | 대응 |
|------|------|
| 힐 HP 증가가 NetworkHealthSync 감소 전용 경로에 막힘 | 증가 분기 신설(A-3). 절대값 동기화 구조라 방향만 열면 됨. |
| 이동 파도의 서버/클라 연출 어긋남 | 데미지=서버 파도 모델, 연출=로컬 파티클(규칙 18·20). 파도 이동 시간을 공통 파라미터로. |
| 파도 중복 타격 | hit-set(Id)로 1회 보장(D-5). |
| special-only인데 주 타깃이 안 맞음 | 파도 판정에 주 타깃도 포함되므로 파도가 처리(중복 제외 불필요 — 단일 피해 자체를 생략). |
| 힐이 죽은/시전자에 적용 | 생존·팀·시전자 제외 필터(D-7). Heal은 !IsAlive에 무동작. |
| 클립 OnAttackHit 부재로 타이밍 어긋남 | 규칙 27 인젝터 주입. 파도 시작 시점 = 타격 프레임. |
| SpecialAttackConfig 미배선 시 폴백값 | 규칙 25 교훈 — 에셋 생성+배선 확인(셋업 스크립트). |

---

## 검증 방법 (구현 후)

- 정적: 컴파일, 일반/도끼병 유닛 무변경(special-only 플래그 false), 힐 없는 유닛 무영향.
- 실기(사용자): TorrentSpirit이 전방에 파도 → 경로상 적 20 피해·아군 10 힐, 각 1회, 시전자·죽은 유닛 제외,
  방향=타겟, 멀티플레이 힐/피해 HP 동기화, 연출이 파도에 맞춤.
- TC/QA는 사용자 명시 지시 시에만(WORKFLOW [5-1~5-3]).

---

## 위임 계획

- 코드 구현: **game-programmer** (규칙 3). `.claude/MEMORY.md` 전달. 힐 서브시스템·special-only·이동 파도가
  핵심 — 서버 권위/레이어 규칙 준수.
- 파도 판정 형태/밸런스 확정 필요 시: game-design-lead.
- 이펙트 프리팹 연결/프리셋: 필요 시 asset-prompt-crafter(기존 프리팹 재사용이라 대부분 배선 작업).
