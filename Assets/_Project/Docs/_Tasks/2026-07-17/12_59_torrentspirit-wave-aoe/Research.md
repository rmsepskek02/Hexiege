# Research — TorrentSpirit(물의 상급 정령) 파도형 AoE 구현

## 이 작업이 무엇인지 (자연어 설명)

TorrentSpirit은 물의 상급 정령으로, 도끼병처럼 한 마리를 때리는 것이 아니라 **전방으로 밀려가는
물의 파도**를 일으킵니다. 이 파도는 파티클 이펙트로 앞으로 이동하며, **파도가 닿는 순간** 그 자리의
적에게는 피해를, 아군에게는 회복을 줍니다.

도끼병(BattleAxe)은 "기본 공격에 얹는 즉발 범위 피해"였지만, TorrentSpirit은 세 가지가 근본적으로
다릅니다: ① **단일 대상 공격이 아예 없고 파도가 유일한 공격**, ② **전방으로 이동하는 파도**(즉발이
아님), ③ **적에게 피해 + 아군에게 회복**(회복 기능은 프로젝트에 아직 없어 새로 만들어야 함).

이번 작업은 특수 유닛 5종 중 두 번째이며, 도끼병에서 만든 특수 공격 아키텍처(전략 핸들러) 위에
**힐 서브시스템**과 **이동 파도 모델**을 새로 얹는 것이 핵심입니다. 여기서 만드는 힐과 이동 파도는
이후 BloomFairy(힐러)에도 재사용됩니다.

---

## 대상 유닛 스펙 (StatsReference.md 기준)

| 항목 | 값 |
|------|----|
| UnitType | `TorrentSpirit` (enum 값 18, 정령계) |
| HP | 100 |
| 공격력(피해) / 힐량 | 20 / 10 |
| 공격 사거리 / 감지 사거리 | 3.0 / 3.0 (원거리) |
| 이동 속도 | 0.5 |
| 공격 쿨다운 | 4.0초 *(파도 이동 시간에 맞춰 조정 예정)* |
| 생산 / 골드 / 인구 | 30초 / 400 / 1 |
| 특수 능력 | **파도형 AoE** — 가로 3 × 전방 3 파도가 전방 이동, 닿는 적 20 피해 / 닿는 아군 10 힐 |

### 확정된 설계 결정 (사용자, 2026-07-17)

1. **파도 = 전방 이동형**. 파티클 이펙트로 구현하며 **파도가 닿는 순간** 효과 발동(즉발 아님).
2. **적에겐 피해만, 아군에겐 힐만** (아군은 피해 없음).
3. **단일 원거리 공격 없음** — 파도가 **유일한 공격 수단**(special-only).
4. **공격 방향 = 타겟을 바라보는 방향** (도끼병과 동일 기준).
5. **힐도 아군이 닿는 시점에 부여**(피해와 동일 타이밍 규칙).
6. **각 유닛에 무조건 1번만** 적용 (아군·적군 공통, 파도가 지나며 중복 타격 없음).
7. **월드 좌표 직사각형** 형태 (폭 3 × 전방 3, 타겟 방향 정렬).
8. **시전자 자신은 힐 대상 아님**.
9. **죽은 유닛(아군·적군)은 대상에서 제외**.
10. **이펙트 = `vfx_torrentspirit_attack.prefab` 사용** (`Assets/_Project/Prefabs/VFX/Units/`, main에 이미 존재).
11. 공격 쿨다운·파도 이동 시간은 튜닝값으로 맞춤.

---

## 현재 코드 구조 (파악 결과)

### 1. 특수 공격 아키텍처 (도끼병에서 구축, GameSystemRules_Units 규칙 23~27)
- `ISpecialAttackBehavior.Apply(SpecialAttackContext)` + `SpecialAttackRegistry`(UnitType→핸들러) +
  `SpecialAttackContext`(공격자·주 타깃·유닛목록·피해헬퍼·월드좌표 델리게이트·튜닝값). 모두 `Scripts/Application/Combat/`.
- 피해 수렴점 `UnitCombatUseCase.ExecuteAttack`:
  ```
  attacker.Facing = FacingDirection.FromCoords(attacker.Position, target.Position); // 타겟 방향 갱신
  ApplyDamageToVictim(attacker, target);          // ← 주 타깃 단일 피해
  _specialAttacks.TryGet(attacker.Type)?.Apply(ctx); // ← 특수 훅
  ```
- **TorrentSpirit 관련 문제**: 현재는 항상 `ApplyDamageToVictim`(주 타깃 단일 피해)가 먼저 실행된다.
  TorrentSpirit은 단일 피해가 없어야 하므로 **special-only 유닛은 이 단일 피해를 건너뛰는 분기**가 필요.

### 2. 힐(HP 회복)은 전혀 구현되어 있지 않음 ← 핵심 신규 작업
- `UnitData`에는 `TakeDamage(int)`만 있고 **`Heal` 메서드가 없다**. `Hp`는 `private set`.
- **`NetworkHealthSync`는 HP 감소만 동기화한다**: `SyncUnitHealth`에서 `diff = unit.Hp - serverHp`가
  `> 0`(감소)일 때만 `TakeDamage(diff)`. **HP 증가(힐, diff < 0)는 아무 처리도 안 함.**
  → 멀티플레이 힐을 위해 **증가 분기(힐 적용 + 클라 재발행)** 를 추가해야 한다.
- `HealShrine`(MistShrine)은 enum·건물 타입만 있고 **힐 로직 미구현**.
- 피격 연출: `FloatingHpTextSpawner.ShowDamage(evt)`가 남은 HP를 팀 색상 텍스트로 표시.
  `HitPresentationQueue`가 공격자 타격 프레임에 맞춰 방출(규칙 19·26). **힐은 별도 연출/색상 필요**.

### 3. 원거리 연출 / 이펙트
- `UnitEffectConfig`(SO)는 UnitType별 `attackPreset`/`deathPreset`/`hitPreset`/`tracerPreset`을 보관.
  `EffectManager.PlayUnitAttack(type, pos, rot)`로 공격 이펙트 재생.
- 원거리 유닛은 `UnitView.OnAttackHit`에서 `AttackRange >= RangedAttackThreshold(1.0)`이면
  **트레이서(`TracerProjectile`)** 를 발사→착탄 콜백에서 `OnLocalAttackHit` 발행(규칙 20). 이동하는
  파도 연출은 이 "이동 후 착탄 시점 방출" 패턴과 성격이 유사(파티클이 앞으로 이동).
- `vfx_torrentspirit_attack.prefab`을 파도 파티클로 사용(TorrentSpirit `attackPreset` 또는 전용 프리셋에 연결).

### 4. 서버 데미지 타이밍 (규칙 18)
- 데미지는 서버 타이머 권위. 애니메이션/클라 파티클 위치에 종속 금지.
- TorrentSpirit 파도도 **서버가 파도 전선의 전진(이동 시간 기반)을 모델링**하여 전선이 유닛에 닿는
  서버 시각에 효과 적용. 클라 파티클·연출은 로컬(규칙 20 패턴).

### 5. 방향/좌표 API (도끼병에서 사용한 것 재사용)
- `IEntityPositionProvider`(서버 권위 월드 좌표), `ResolveWorldPosition`(UnitCombatUseCase), XZ 평면 판정.
- forward = 공격자 → 주 타깃 방향(월드 XZ). ExecuteAttack이 Facing을 타겟 방향으로 갱신.

### 6. 프리팹/등록 현황
- `UnitType.TorrentSpirit` = 18 등록됨. UnitStatsConfig에 unitType 18 스탯 **없음**(특수 5종 미입력) → 입력 필요.
- 프리팹 `Unit_TorrentSpirit_Blue/Red` 존재, UnitFactory 씬 등록(type 18)·생산 매핑은 확인 필요.

---

## 영향 범위

| 파일 | 예상 변경 | 구분 |
|------|-----------|------|
| `Domain/Unit/UnitData.cs` | `Heal(int amount)` 추가(MaxHp 클램프) | 수정 |
| `Application/Combat/SpecialAttackContext.cs` | 힐 헬퍼 + 팀 구분 효과 + 파도 파라미터 전달 | 수정 |
| `Application/Combat/SpecialAttackRegistry.cs` | TorrentSpirit 등록 + **special-only 구분** | 수정 |
| `Application/Combat/TorrentAttackBehavior.cs` | 파도 핸들러(신규) | 신규 |
| `Application/UseCases/UnitCombatUseCase.cs` | special-only 유닛은 주 타깃 단일 피해 생략 분기 | 수정 |
| 이동 파도 서버 모델 | 파도 전선 전진 + 닿은 유닛 1회 적용(hit-set) — 서버 권위 | 신규 |
| 힐 이벤트 | `GameEvents`에 힐 이벤트(또는 EntityDamaged 확장) | 수정/신규 |
| `Infrastructure/Network/NetworkHealthSync.cs` | HP 증가(힐) 동기화 분기 + 클라 힐 이벤트 재발행 | 수정 |
| 힐 연출 | `FloatingHpTextSpawner`/`HitPresentationQueue` 힐 색상·경로 | 수정 |
| `Presentation/Effects/EffectManager` + `UnitEffectConfig` | 파도 이펙트(vfx_torrentspirit_attack) 재생 배선 | 수정/에셋 |
| `SpecialAttackConfig`(SO) | 파도 파라미터(폭·전방길이·피해·힐·이동시간) 추가 | 수정 |
| `UnitStatsConfig`(asset) | TorrentSpirit(18) 스탯 입력 | 에셋 |
| UnitFactory 씬 등록 / 생산 매핑 | type 18 확인·필요 시 등록 | 에셋/씬 |

---

## 핵심 난이도 & 확정/검토 항목 (Plan에서 확정)

1. **special-only 공격 모델**: ExecuteAttack에서 주 타깃 단일 피해 생략 방식(레지스트리 플래그 vs 핸들러가 신호). — Plan에서 방식 확정.
2. **이동 파도의 서버 타이밍 모델**: 파도 전선을 서버 틱으로 전진시키며 닿은 유닛에 1회 적용(연속) vs
   시전 시점에 3×3 내 유닛을 스냅샷하여 각자 도달 시각에 스케줄(이산). "닿을 때/1회만"과 서버 권위를
   만족하는 선에서 game-programmer가 확정. 파도 이동 시간은 config 값.
3. **힐 서브시스템 형태**: 힐 이벤트를 신규(`OnEntityHealed`)로 둘지, 기존 피격 이벤트에 부호/플래그로
   통합할지. 멀티 동기화(NetworkHealthSync 증가 분기)와 연출(초록 텍스트) 포함.
4. **파도 파라미터의 config 구조**: 도끼병 sweep 값과 TorrentSpirit wave 값이 같은 SO에 쌓이므로,
   유닛별 파라미터 구조로 확장할지 검토(규칙 25 연장).
5. **파도 이펙트 재생 방식**: `vfx_torrentspirit_attack.prefab`을 attackPreset로 1회 재생할지, 전방
   이동시킬지(파티클 자체 전방 방출인지 GO 이동인지) — 프리팹 특성 확인 후 결정.
6. **타격 시점(OnAttackHit)**: TorrentSpirit 클립도 OnAttackHit 없음 → 쿨다운·파도 시작 시점 확정 후 인젝터 주입(규칙 27).
