# Research — QuakeSpirit(대지의 정령) 착탄형 즉발 AoE 구현 + 특수 유닛 설계문서 기록

## 이 작업이 무엇인지 (자연어 설명)

QuakeSpirit은 특수 유닛 5종의 **마지막**으로, MushroomBomber와 같은 **착탄형 범위 딜러**입니다. 다만
지속 피해(DoT)가 아니라 **즉발(한 번에 터지는) 범위 피해**를 줍니다.

공격이 착탄하면 두 단계로 피해가 들어갑니다.
1. **주 타깃 1마리 = 100%(20)** — 폭발 중심에서 정통으로 맞은 1명(직접 피해).
2. **주변 = 50%(10)** — 착탄 중심 주변(월드 반경 = 인접 1칸) 안의 **다른 적 유닛과 적 건물 전원**에게
   절반 피해. **주 타깃은 여기서 제외**(주 타깃은 100%만 받음).

MushroomBomber와의 핵심 차이:
- **DoT가 아니라 즉발** 피해.
- **주 타깃은 100%만**(MushroomBomber는 주 타깃이 직접+DoT 둘 다였음).
- **스플래시가 건물도 때림**(MushroomBomber DoT·BattleAxe 휩쓸기는 유닛만이었음 — QuakeSpirit은 건물 포함).
- 소수점 **올림**.

판정은 MushroomBomber에서 만든 **월드 좌표 원형 반경 헬퍼(규칙 38)** 를 재사용합니다(착탄 중심에서 XZ 평면
거리 ≤ 반경). 단 기존 헬퍼는 유닛만 수집하므로 **적 건물 순회를 추가**해야 합니다.

추가로, 이번 작업에서 **특수 유닛 6종의 능력 설계를 `GameDesignDocument.md`(유닛 설계문서)에 기록**합니다
(QuakeSpirit + 이전 5종 소급). GameSystemRules_Units는 "구현 규칙", GameDesignDocument는 "설계/기획"으로 역할 분리.

---

## 대상 유닛 스펙 (StatsReference.md 기준)

| 항목 | 값 |
|------|----|
| UnitType | `QuakeSpirit` (enum 값 15, 정령계 · "흙정령3") |
| HP | 250 (탱커) |
| 공격력 | 20 |
| 공격 사거리 / 감지 사거리 | 0.5 / 1.0 (근접 착탄) |
| 이동 속도 | 0.5 (느림) |
| 쿨다운 | 1:20(5:00) — 전체 주기 5.0s |
| 생산 / 골드 / 인구 | 30초 / 400 / 1 |
| 특수 | 착탄형 AoE — 중심 타일 1마리 **100%(20)**, 중심 타일 나머지 + 인접 6타일 전체 **50%(10)** |

### 확정된 설계 결정 (사용자, 2026-07-20)
1. **즉발 피해**(DoT 아님).
2. **주 타깃 1마리 = 100%(20)만** — 스플래시(50%) 대상에서 제외.
3. **스플래시 50%(10) = 착탄 반경 내 적 유닛 + 적 건물**(주 타깃 제외). **건물도 영향받음**(다른 착탄/휩쓸기 유닛과 다른 점).
4. 스플래시 데미지 **올림**(`CeilToInt`) — 50% 계산 시 소수점 올림.
5. **판정 = 월드 좌표 원형 반경**(규칙 38 재사용). 반경 = 전용 `_quakeRadius`(기본 1.0 = 인접 1칸, SpecialAttackConfig, MushroomBomber `blastRadius`와 분리해 독립 튜닝).
6. **아군 무피해**(규칙 16). 모든 피해 서버 권위(규칙 18). VFX(폭발)는 **사용자 별도 제작**. OnAttackHit 주입·스탯 입력·생산 배선은 **에디터 스크립트**.

---

## 현재 코드/에셋 상태 (파악 결과)

### 1. 특수 공격 전략 핸들러 구조 (규칙 23) — 재사용
- `SpecialAttackRegistry` 등록 현황: BattleAxe·TorrentSpirit·MushroomBomber·InfernoSpirit. **QuakeSpirit 미등록**(주석 자리만).
- `ExecuteAttack`이 단일 피해 직후 특수 훅 호출. 신규 유닛 = **핸들러 + 등록 1줄**.

### 2. 착탄형 월드 원형 반경 헬퍼 (규칙 38, MushroomBomber에서 구축) — 재사용 + 확장
- `BlastAttackBehavior`의 `CollectEnemyUnitsInRadius(ctx, attacker, center, radiusSqr, buffer)` static 헬퍼:
  착탄 중심에서 XZ 거리 ≤ 반경인 **적 유닛**(아군·사망·건물 제외) 수집.
- **QuakeSpirit 관련**: 이 헬퍼는 유닛만 수집한다. QuakeSpirit은 **적 건물도 스플래시** 대상이므로,
  **반경 내 적 건물 순회를 별도로 추가**해야 한다(TorrentSpirit BUG-002 "special-only는 건물도 순회" 교훈과 동일 취지).
  헬퍼를 건물까지 포함하도록 확장하거나(공용화), QuakeSpirit 핸들러에서 건물 순회를 병행.

### 3. 즉발 피해 적용 (규칙 24/26, BattleAxe 방식) — 재사용
- BattleAxe 휩쓸기가 이미 **즉발 AoE**를 `ApplyDamageToVictim`(선수집 후 일괄)으로 적용하고, 단일 타격 프레임 AoE
  연출 동시 방출(규칙 26)을 쓴다. QuakeSpirit도 동일: 반경 수집 후 각 대상에 **50% 즉발 피해** 적용.
- DoT 초단위 틱(규칙 40)은 사용하지 않는다(즉발이므로).

### 4. 직접 100% = 주 타깃 단일 피해 (기존 경로)
- `ExecuteAttack`의 단일 피해(`ApplyDamageToVictim`, 공격력 20)가 주 타깃(유닛/건물)에 100% 적용.
  `ReplacesPrimaryAttack=false`. 이후 핸들러가 50% 스플래시(주 타깃 제외).

### 5. 데이터/에셋 (미완비 — MushroomBomber 수준의 셋업 필요)
- 프리팹 `Unit_QuakeSpirit_Blue/Red` + 애니(Walk/Attack) 존재.
- **UnitStatsConfig에 type 15 미입력**(폴백값 사용). UnitEffectConfig에 type 15 항목은 있으나 attackPreset 미연결.
- **VFX 없음**(사용자 제작 예정). **Attack 클립 OnAttackHit 미주입**(규칙 27 잔여 마지막 유닛).
- UnitFactory 정령 프리팹 등록·생산 배선 확인 필요.

### 6. SpecialAttackConfig (규칙 25)
- 기존: sweep/wave/bloom/blast/inferno 튜닝값. QuakeSpirit용 `_quakeRadius`(+ 필요 시 스플래시 비율) 추가 예정.

---

## 특수 유닛 설계문서 기록 (요청)
- **`GameDesignDocument.md` "🪖 유닛 시스템"** 섹션에 특수 유닛 6종 능력 설계를 자연어로 기록:
  - BattleAxe(휩쓸기 부채꼴), TorrentSpirit(이동 파도+힐), BloomFairy(힐러 HoT), MushroomBomber(착탄 DoT),
    InfernoSpirit(단일 대상 DoT), QuakeSpirit(착탄 즉발 2단계).
- 현재 GameDesignDocument는 유닛 시스템/스탯/타입/종족을 다루나 **특수 능력 상세 설계 항목이 없음** → 신설.

---

## 영향 범위 (예상)

| 파일/영역 | 예상 변경 | 구분 |
|-----------|-----------|------|
| `Application/Combat/QuakeAttackBehavior.cs` | 착탄 즉발 2단계(반경 내 유닛+건물 50%) 핸들러 | 신규 |
| `Application/Combat/SpecialAttackRegistry.cs` | QuakeSpirit 등록 1줄 | 수정 |
| `Application/Combat/BlastAttackBehavior.cs` 또는 공용 | 원형 반경 헬퍼 건물 포함 확장(또는 병행 순회) | 수정/검토 |
| `Application/UseCases/UnitCombatUseCase.cs` | 스플래시 즉발 피해 진입점, quake 튜닝 배선 | 수정 |
| `Infrastructure/Config/SpecialAttackConfig.cs` (+asset) | `_quakeRadius`(+비율) | 수정/에셋 |
| `Bootstrap/GameBootstrapper.Setup.cs` | quake 튜닝 주입 | 수정 |
| `UnitStatsConfig.asset` | type 15 스탯 입력 | 에셋(에디터 스크립트) |
| Attack 클립 OnAttackHit / UnitEffectConfig | 주입 / 프리셋(사용자 VFX) | 에디터 |
| UnitFactory 등록 / 생산 배선 | type 15 | 에디터 스크립트 |
| `GameDesignDocument.md` | 특수 유닛 6종 능력 설계 기록 | 문서 |

**무변경 재사용**: 특수공격 전략 구조(규칙 23), 즉발 AoE·연출 동시 방출(규칙 24/26), `ExecuteAttack`/`ApplyDamageToVictim`/피해·사망 이벤트·데미지 텍스트, 기존 유닛 로직(회귀 방지).

---

## 현재 상태 (구현 전제)
- `UnitType.QuakeSpirit` = 15. 프리팹·애니 존재. **스탯 미입력·VFX 없음·OnAttackHit 미주입·레지스트리 미등록.**
- 착탄형 월드 반경 헬퍼(규칙 38)·즉발 AoE(규칙 24/26)는 이미 있음(재사용). 건물 순회만 추가 필요.

---

## 핵심 난이도 / Plan에서 결정할 항목
1. **반경 내 건물 순회 추가**: 헬퍼를 건물 포함으로 확장 vs QuakeSpirit 핸들러 병행 순회(유닛 hit-set과 건물 hit-set 분리 — 규칙 29 교훈).
2. **2단계 피해**: 주 타깃 100%(기존 경로) + 나머지 유닛·건물 50%(올림, 핸들러). 주 타깃 제외 확실히.
3. **quake 전용 반경/비율**: SpecialAttackConfig 독립 필드(에셋≠배선 확인, 폴백).
4. **에디터 스크립트**: type 15 스탯 입력·프리팹 등록·생산 배선·OnAttackHit(가능 범위) 자동화.
5. **GameDesignDocument 특수 유닛 6종 설계 기록**(신규 섹션).
