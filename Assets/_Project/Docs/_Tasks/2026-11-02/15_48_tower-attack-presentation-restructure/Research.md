# Research — 방어 타워 공격 표현·소리 개편 (C 작업)

> **작성일:** 2026-11-02 · **작성:** document-manager · **기준 커밋:** `7368d7f1` (브랜치 `claude/vfx-code-followups`)
> **상태:** 조사 완료 · 짝 문서 [Plan.md](Plan.md) 작성됨(승인 대기). 구현하지 않았다.
> **표기:** [확인] = 이 문서 작성 중 파일을 직접 열어 행 번호까지 다시 확인 · [전달] = 메인 세션이 전달한 개발 담당 읽기 전용 조사(2026-11-02) 또는 사용자 발언(직접 대조 못 함) · [미확인] = 확인하지 못함(이유 병기).
> 행 번호는 위 기준 커밋의 작업 트리 기준이다. 작성 시점 `git status`에서 코드 파일은 바뀐 것이 없었다(바뀐 것은 다른 task 문서뿐) — 작업 트리 코드 = 커밋 `7368d7f1`. 행 번호는 코드가 바뀌면 어긋나므로 메서드·필드 이름을 함께 적는다.

---

## 0. 이 작업이 무엇인가 (먼저 읽기)

게임에는 종족마다 다른 방어 타워가 하나씩 있다 — 인간의 **대포 타워(CannonTower)**, 정령의 **룬 첨탑(RuneSpire)**, 초월의 **덩굴 타워(VineTower)**. 셋 다 프로그램 안에서는 같은 건물 종류(「자동 타워」)이고, 종족만 다르다.

그런데 타워가 공격할 때 **화면에 무엇을 보여 줄지 정하는 코드**가 「종족이 인간이면 이렇게, 정령이면 저렇게, 초월이면 아무것도 안 함」처럼 여러 파일에 종족 이름으로 직접 갈라져 있다. 인간 타워는 대포알이 날아가고, 정령 타워는 번개가 꽂히고, 덩굴 타워는 아무 연출이 없다. 사용자는 이 코드를 「임시로 작성된 코드」라고 했고, 지금은 타워마다 **공격 컨셉(대포알 / 번개 / 땅에서 솟는 덩굴)** 이 달라졌으니 그 컨셉별로 나누어 쓰도록 개편하기로 했다. 또 타워 공격 **소리**는 지금 세 타워 모두 낼 길 자체가 없다.

이번 작업(C)은 **표현과 소리만** 바꾼다.

- 「종족이 무엇이냐」 대신 **「이 타워의 공격 방식이 무엇이냐(투사체 / 번개 / 지면 분출)」** 로 화면 연출을 고르도록, 타워별 설정을 한 줄짜리 **공격 프로필**로 묶는다. 덩굴 타워 자리는 만들어 두되 지금은 비워 둔다(덩굴 연출은 다음 작업 ③).
- 타워 공격음을 넣을 **칸(설정 위치)과 재생 통로**를 만든다. 실제 소리 파일 연결은 나중 작업(④)이다.
- 쓰이지 않는 옛 타워 연출 코드를 정리한다.

피해가 언제·얼마나 들어가는지(서버 판정)는 **전혀 바꾸지 않는다.** 이 문서는 그 개편을 위해 지금 코드가 어떻게 생겼는지, 무엇이 영향을 받는지를 조사한 결과다.

---

## 1. 사용자 결정 (2026-11-02, 사용자 「추천대로」) [전달]

| # | 결정 | 이 문서에서의 의미 |
|---|------|------------------|
| 1 | 범위는 **표현과 소리만**. 서버 피해 경로(투사체/즉발 분기, Human 전용 상수, `ProjectileConfig`, 핸드셰이크 토큰)는 건드리지 않는다 | 4절 표의 「서버」 칸은 바꾸지 않는 목록 |
| 2 | 방식은 **1안 「표현 전략 데이터화」** — `BuildingEffectConfig`를 종족별 한 행의 타워 공격 프로필로 통합: 공격 방식 열거(Projectile / Beam / GroundBurst) + launch/tracer/impact 프리셋 + 재생 위치 기준(Source/Impact/Muzzle). `EffectManager`의 종족 if 분기를 공격 방식 기준 분기로 교체. GroundBurst(대상 발밑 재생) 자리를 만들되 **VineTower 행은 비워 둔다**. 검증 49행의 「Transcendence null」 계약은 「VineTower 프로필이 비어 있으면 재생하지 않음」으로 개정 | 5·8절 |
| 3 | 미사용 옛 타워 연출 코드 정리를 범위에 **포함**(WORKFLOW 기존 로직 제거 규칙 준수) | 7절 |
| 4 | 타워 공격음 설정 위치는 **기존 `SoundConfig`에 「건물 종류 + 종족(RaceId)」 키 칸 추가**(사운드 규칙 3) + `AudioManager`에 타워 공격음 API. 실제 클립 연결은 이 작업 범위가 아님(효과음 task 2단계). VFX와 같은 지점에서 짝 호출(사운드 규칙 15). 동시 재생 우선순위(특수·타워 우선)는 효과음 task 1단계에서 들어가므로 타워 공격음이 「특수/타워」 분류로 재생되도록 **연결만** 한다 | 6절 |
| 5 | 순서: ① 효과음 1단계(task `13_39_unit-attack-sfx-tiering`) → ② **이 개편(C)** → ③ VineTower VFX 연결(task `14_40_vinetower-attack-design`, 덩굴 모델 제작은 병행 가능) → ④ 효과음 2단계(타워 공격음 클립: Human = Tank 포격 공용, RuneSpire 전용, VineTower는 VFX 확정 후) | 10절 |
| 6 | Human 타워 공격음은 ②~④ 전까지 무음이어도 된다(옛 클립은 커밋 `323dd454`에서 이미 삭제되어 현재도 무음) | 6절 |
| 7 | RuneSpire 번개 끝점 OPEN 문제(대상 사망 시 끝점 어긋남)는 **제외** | 9-2 |

이 결정의 배경 — 사용자 원문 「타워 공격음 코드는 임시로 작성된 코드고, 현재는 컨셉별로 나누어 사용하기 때문에 개편할 것」 — 은 [유닛 공격 SFX Research](../13_39_unit-attack-sfx-tiering/Research.md) 11-13-1과 [VineTower Research](../14_40_vinetower-attack-design/Research.md) U-4·6-3에 기록돼 있다 [확인].

---

## 2. 세 타워가 같은 건물 종류다 [확인]

- 세 타워 모두 `BuildingType.AutoTower`(= 2)이고 종족(`RaceId`)으로만 구분된다 — `GameSystemRules_Buildings.md` 「방어 타워 시스템」 머리 블록.
- `RaceId`는 Domain 레이어 열거형이다: `Assets/_Project/Scripts/Domain/Common/RaceId.cs`, 네임스페이스 `Hexiege.Domain`, 머리 주석 「Domain 레이어 — 외부 의존성 없음」, 값 Human 0 · Spirit 1 · Transcendence 2(23~28행). → 사운드 규칙 3(「SoundConfig는 Domain 레이어 타입만 키로」)의 키로 쓸 수 있는 근거다. 단 규칙 3 본문의 괄호 예시는 「(UnitType, BuildingType)」 두 개뿐이라, 이 괄호가 전체 목록인지 예시인지는 문구만으로 정해지지 않는다 → Plan의 규칙 개정 초안 S-1.

---

## 3. 타워 공격 한 번이 지나가는 길 — 피해 2갈래 · 표현 3갈래 [확인]

공격 시점은 `Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack`(247행~)이다.

| 타워 | 피해(서버) | 화면 표현 | 근거(행) |
|------|-----------|----------|---------|
| Human CannonTower | **서버 투사체** — 253~258행 `IsProjectileTower` 참이면 `_projectiles.TryLaunchTowerProjectile(...)`, 착탄 때 `ApplyTowerProjectileImpact`(339행~)가 피해. 공격 스냅샷을 내지 않는다(250~251행 주석) | `Presentation/Effects/ProjectilePresentationPresenter.cs` — 299~301행 트레이서(`effects.BeginServerTowerProjectile`), 311~317행 발사 신호(`effects.PlayServerTowerLaunchCue`, 비행 중간 복원(`midFlight`)이면 생략), 361~363행 착탄(`effects.PlayServerTowerProjectileImpact`) → `EffectManager` 720~772행 | Presenter 285~286행: 건물 공격자의 출발점 = 서버가 보낸 포구(`launch.Source`). 포구 위치 계산은 `Application/Combat/ProjectileDeliveryContracts.cs` 407행 기본값 · 454~458행 `ResolveTowerMuzzle` |
| Spirit RuneSpire | **즉발** — 262~285행 피해 적용 | 286~294행 공격 스냅샷 생성·발행(`GameEvents.OnAttackEffectConfirmed`) → `Infrastructure/Network/NetworkCombatController.cs` 73·80행 `AttackEffectClientRpc` → 92행 `OnAttackEffectReceived` → `HitPresentationQueue.ReceiveAttackEffect`(377~389행) → `EmitAttackEffects`(390~423행) → `EffectManager.PlayAttackEffect`(551~582행) → 577~579행 `item.BeginTowerBolt`(`VfxPoolItem.TowerBolt.cs`) | 번개 추적 대상 해석: `HitPresentationQueue` 412~413행(`s.Race == RaceId.Spirit`일 때만 `ResolveTowerBoltTarget`) |
| Transcendence VineTower | **즉발** — RuneSpire와 같은 줄 | 스냅샷은 같은 길로 전송되지만 `EffectManager.cs` 558행 `if (s.Race == RaceId.Transcendence) { outcome = "UnspecifiedTranscendence"; return false; }`에서 버린다. `HitPresentationQueue.cs` 416행이 이 결과만 실패 집계(`_attackEffectFailures`)에서 뺀다 | — |

- Human 타워도 `_projectiles`(투사체 발사기)가 연결되지 않았으면 즉발 경로를 탄다 — `TowerCombatUseCase.cs` 260행 주석 「투사체 launcher가 연결되지 않았을 때의 인간 타워」, 76행 「null이면(주입 전·Editor fixture)」. 이때는 Human 스냅샷이 `PlayAttackEffect`로 와서 Human 발사 프리셋이 **타워 위치**에서 재생된다(558~571행, 번개 분기는 Spirit만). 에디터 검증 (f)가 이 동작을 고정한다(8절).
- Presenter의 `visual.TowerType`은 서버 발사 기록의 `AttackerKind`에서 온다(`ProjectilePresentationPresenter.cs` 277~278행). 서버는 `IsProjectileTower`(= AutoTower + Human)일 때만 타워 발사 기록을 만드므로 실제로는 늘 AutoTower다 [확인 — 코드 읽기, 실행 관측 아님].

---

## 4. 종족을 직접 적은 곳 9곳 [확인]

| # | 위치 | 내용 | 성격 | 이번 작업 |
|---|------|------|------|----------|
| H-1 | `ProjectileDeliveryContracts.cs` 324행 | `ProjectileTowerRaces = { RaceId.Human }` — 핸드셰이크 토큰에 들어감(322행 주석) | 서버 분류 | **바꾸지 않음**(결정 1) |
| H-2 | 같은 파일 334~335행 | `IsProjectileTower` | 서버 분류 | 바꾸지 않음 |
| H-3 | 같은 파일 407행 | `DefaultHumanTowerMuzzleOffset` (0, 0.405, 0.17) | 서버 포구 | 바꾸지 않음 |
| H-4 | 같은 파일 441행 | `GetTowerSpeed` → 늘 `HumanTowerSpeed` | 서버 속도 | 바꾸지 않음 |
| H-5 | 같은 파일 454~458행 | `ResolveTowerMuzzle` — Human 포구 오프셋, Red는 x·z 반전 | 서버 포구 | 바꾸지 않음 |
| H-6 | `Presentation/Effects/BuildingEffectConfig.cs` 53행 | `if (race == RaceId.Transcendence) return null;` | 표현 조회 | **바꿈** — 프로필 조회로 |
| H-7 | `EffectManager.cs` 558행 · 577행 | Transcendence 차단 · Spirit만 번개 | 표현 분기 | **바꿈** — 공격 방식 기준으로 |
| H-8 | `HitPresentationQueue.cs` 412~413행 · 416행 | Spirit만 대상 해석 · Transcendence 결과 실패 제외 | 표현 분기 | **바꿈** |
| H-9 | `Infrastructure/Factories/BuildingFactory.cs` 234행 | `race == RaceId.Human && type == AutoTower`이면 초기 회전(Buildings 규칙 11) | 배치 규칙 — 표현 아님 | 바꾸지 않음 |
| (참고) | `Application/UseCases/UnitCombatUseCase.ProjectileDiagnostics.cs` 150~156행 `RecordProjectileTowerAttackPath` · 391행~ `BuildProjectileTowerEndLine` | 종족 번호를 진단 칸 번호로 씀 | 서버 진단(판정 무관) | 바꾸지 않음 |

전달받은 「9곳」은 위 H-1~H-9와 진단(참고) 묶음을 세는 방식에 따라 달라진다. 이 표는 줄 단위로 다시 적은 것이다.

---

## 5. 설정 3갈래 · 조회 3갈래 · 재생 메서드 5개 [확인]

### 5-1. 설정 — `BuildingEffectConfig.cs` / `Resources/Config/BuildingEffectConfig.asset`

| 칸 | 코드 | 에셋 실제 값 |
|----|------|-------------|
| `_entries[].attackPreset` (건물 종류별) | 36~37행 필드, 99행 목록 | 에셋 24~33행 `_entries` 3행(buildingType 0·1·2)에 **`attackPreset` 키 자체가 없다**(직렬화 0건) — `destroyPreset`·`upgradePreset`만 있고 모두 비어 있음 |
| `_autoTowerAttacks` (종족 → 발사 프리셋) | 47~49행 `RaceAttack { race, preset }` | 에셋 15~19행: race 0 → GUID `1ddf58ca…` = `EffectPreset_HumanAutoTower_Attack`, race 1 → `a01e8a0e…` = `EffectPreset_RuneSpire_Attack`. **race 2 행 없음** |
| `_autoTowerProjectiles` (종족 → 트레이서·착탄) | 62~76행 `RaceProjectile { race, tracerPreset, impactPreset }` | 에셋 20~23행: race 0만 — tracer `d6770727…` = `EffectPreset_Cannon_Tracer`, impact `f1b34352…` = `EffectPreset_Cannon_Impact` |

GUID → 파일 대응은 `Resources/Config/EffectPresets/*.asset.meta`를 검색해 확인했다.

### 5-2. 조회

| 메서드 | 행 | 동작 |
|--------|----|------|
| `GetAttack(BuildingType, RaceId)` | 50~56 | AutoTower가 아니면 `GetAttack(type)`로 넘김(52), Transcendence면 null(53), 아니면 `_autoTowerAttacks`에서 찾음 |
| `GetTowerTracer` · `GetTowerImpact` | 81~86 · 91~96 | AutoTower가 아니거나 행이 없으면 null |
| `GetAttack(BuildingType)` | 149~153 | `_entries` 캐시의 `attackPreset` |

### 5-3. 재생 메서드 5개 (`EffectManager.cs`)

| 메서드 | 행 | 호출처 |
|--------|----|-------|
| `PlayBuildingAttack` | 527~535 | `HitPresentationQueue.PlayTowerAttackVfx` 1529행 **하나뿐** — 그 메서드 자체가 호출처 0(7절) |
| `PlayAttackEffect` | 551~582 | `HitPresentationQueue` 414행, 에디터 검증 1614·1683·1697·1705행 |
| `BeginServerTowerProjectile` | 720~728 | Presenter 301행 |
| `PlayServerTowerLaunchCue` | 742~752 | Presenter 316행 |
| `PlayServerTowerProjectileImpact` | 762~772 | Presenter 363행 |

---

## 6. 소리 현황 — 세 타워 모두 공격음 경로가 없다 [확인]

- 코드에 타워 공격음 호출이 없다. 자리 표시 주석만 있다: `EffectManager.cs` 531~534행(`PlayBuildingAttack` 안 — 「추후 … `AudioManager.Instance?.PlayBuildingAttackSfx(type)` 짝을 추가」), 580행 「Sound15: impact and tower SFX are unspecified」, 735행 「타워 발사 SFX는 아직 지정되지 않았다」, `TowerCombatUseCase.cs` 286행 「Sound15: tower fire SFX is unspecified」.
- `Infrastructure/Config/SoundConfig.cs`의 건물 항목 `BuildingSoundEntry`는 `BuildingType` 키(74~77행) + 배치·파괴·업그레이드 클립·볼륨뿐이고, 캐시도 `Dictionary<BuildingType, …>`(171·191~196행), 조회 메서드도 그 여섯 개뿐(260~325행). 종족을 구분할 키가 없다. 머리 주석 24행 「Domain 레이어 타입(UnitType, BuildingType)만 키로 사용한다」.
- `SoundConfig.asset` `_buildingSoundEntries`(51~58행)에는 buildingType 0 한 행뿐이고 클립·볼륨 모두 비어 있다.
- `Presentation/Audio/AudioManager.cs` SFX API(497~570행): `PlayUnitAttackSfx` · `PlayUnitDeathSfx` · `PlayBuildingPlaceSfx`(519) · `PlayBuildingDestroySfx`(527) · `PlayBuildingUpgradeSfx`(535) · `PlayUiSfx` · `PlaySfxClip`. 공격음 API 없음. `PlaySfxClip` 559행은 클립이 없거나 동시 재생 한도면 버린다(우선순위 없음 — 효과음 task ①이 바꾸기로 결정, [13_39 Research](../13_39_unit-attack-sfx-tiering/Research.md) 11-14).
- **사운드 규칙 1**: 「EffectManager는 VFX만 담당하며 오디오 코드가 한 줄도 포함되어서는 안 된다」. 그래서 규칙 15의 짝 호출은 `EffectManager`를 **부르는 쪽**(Presenter · HitPresentationQueue)에서 해야 한다. 유닛의 서버 투사체 발사음도 같은 방식이다 — `Presentation/Unit/UnitView.cs` 1469행 `effectManager.PlayUnitAttack(...)` 바로 아래 1471행 `AudioManager.Instance?.PlayUnitAttackSfx(...)`이며, 이 발사음은 **VFX 재생 성공 여부와 무관하게** 호출된다(1464행 if 바깥).
- 사운드 규칙 16은 SFX 항목마다 볼륨 칸을 두라고 한다(「UnitSoundEntry, BuildingSoundEntry 등에 `attackVolume`, `deathVolume` 등」).

---

## 7. 쓰이지 않는 옛 타워 연출 코드 [확인]

| # | 위치 | 쓰이지 않는 근거 |
|---|------|----------------|
| U-1 | `HitPresentationQueue.PlayTowerAttackVfx` 1486~1530행(요약 주석 1486~1493 포함) | private, 저장소 전체 `.cs` 검색에서 정의(1494)만 있고 호출 0. 에디터 리플렉션 호출도 없음(`GetMethod("PlayTowerAttackVfx"` 0건) |
| U-2 | `EffectManager.PlayBuildingAttack` 518~535행(요약 주석 포함) | 호출처는 U-1의 1529행뿐 |
| U-3 | `BuildingEffectConfig.GetAttack(BuildingType)` 142~153행 | 호출처 2곳: U-2(529행)와 **`GetAttack(BuildingType, RaceId)` 52행**(AutoTower가 아닌 종류일 때 넘김). 52행으로 실제 오는 호출은 없다 — 모든 호출자가 AutoTower를 넘긴다(`EffectManager` 559행 상수 AutoTower, 744행 `towerType` = 서버 발사 기록의 AutoTower(3절), 에디터 검증 31·32·49·1588행 AutoTower). 그리고 이 메서드가 읽는 `attackPreset`은 에셋에 값이 없다(5-1). *(전달 조사의 「호출처 0」은 52행 내부 호출을 세지 않은 표현이다 — 실제로 도달하는 호출이 없다는 뜻으로는 맞다.)* |
| U-4 | `BuildingEffectEntry.attackPreset` 36~37행 | 에셋 직렬화 0건(5-1). 읽는 곳은 U-3뿐 |
| U-5 | `HitPresentationQueue.cs` 649~652행 옛 「타워 발사 연출(3-1)」 주석 | 653행 영문 주석 「Tower VFX has a sole emitter from the confirmed authority snapshot, not this HP event.」이 현행 설명이고, 649~652행은 HP 이벤트에서 발사 VFX를 내던 옛 설계 설명이다. 646~648·655~657행은 현행 코드(전달 범위 「646~653」 중 실제 옛 설명은 649~652행) |

---

## 8. 에디터 자동 검증 영향 [확인]

회귀 스위트 메뉴 `Hexiege/Combat/Run Full Regression Suite`(`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` 986행 `Run`)가 1041행에서 `ValidateVfxConnections()`를 부른다. 그 안에서 76·77행이 `ValidateWriterSnapshotVfx` · `ValidateRuneSpireBoltEndpoint`를 부른다.

| 검증 | 파일·행 | 지금 고정하는 것 | 이번 작업 영향 |
|------|--------|----------------|---------------|
| Human 프리셋 계약 | `RunUnitActionSelfValidation.VfxConnections.cs` 31~47 | `GetAttack(AutoTower, Human)` 프리셋의 프리팹 = 실제 Assault 프리팹(`vfx_pistoleer_attack.prefab`), 유닛 프리셋 3개와 별개 객체 | `GetAttack(AutoTower, race)` 서명을 유지하면 그대로 통과해야 한다 |
| RuneSpire · Transcendence | 같은 파일 48~49 | RuneSpire 프리팹 경로가 `vfx_runespire_attack.prefab`으로 끝남 **그리고 `GetAttack(AutoTower, Transcendence) == null`** (메시지 「Race selector preserves unspecified tower.」) | 결정 2에 따라 「VineTower 프로필이 비어 있으면 재생하지 않음」 계약으로 **개정** |
| 번개 추적 fixture | 같은 파일 1586~1715 | Spirit 스냅샷 → `Endpoint=Tracked`, 이동 추적, 사망·교체 시 고정, 반환 시 원본 복원, 해석 실패 시 0.6m 대체 높이. **(f) 1703~1708: Race = Human 스냅샷이 `PlayAttackEffect`에서 true를 돌려주고 `Endpoint`가 없어야 함** | 번개 분기 조건이 「Spirit」에서 「공격 방식 Beam」으로 바뀌어도 기대값은 그대로여야 한다. (f)는 Human 프로필(투사체)이 스냅샷 경로에서 타워 위치 재생을 유지해야 통과 |
| 타워 writer fixture | 같은 파일 1717~1830, 특히 1791·1796·1815 | `TowerCombatUseCase` 생성자 인자와 `ExecuteTowerAttack`(이름·인자 `BuildingData, UnitData`)을 **리플렉션**으로 호출. `TowerCombatUseCase.cs` 245행 주석도 「이름·매개변수는 바꾸지 않는다」 | `TowerCombatUseCase`를 건드리지 않으므로 영향 없음 |
| 서버 투사체 C-1 | `RunUnitActionSelfValidation.ServerProjectiles.cs` 240~244 | `IsProjectileTower`가 Human AutoTower만 참 | 서버 분류 불변 → 영향 없음 |
| C-5 | 같은 파일 565행~ · 809~818 | RuneSpire 즉발 피해 · 스냅샷 1건 · 쿨다운 · 서버 END 경로 | 영향 없음 |
| **C-6 소스 문자열 게이트** | 같은 파일 1163~1191 | 주석이 아닌 줄만 모은 소스에 아래 문자열이 **그대로** 있어야 함. Presenter: `visual.Start = ViewConverter.ToView(launch.Source);` · `effects.BeginServerTowerProjectile(visual.TowerType, visual.TowerRace, visual.Start)` · `effects.PlayServerTowerLaunchCue(visual.TowerType, visual.TowerRace, visual.Start,` · `!launch.AttackerIsUnit && !midFlight && effects != null` · `effects.PlayServerTowerProjectileImpact(visual.TowerType, visual.TowerRace, point, point - visual.Start)`. TowerCombatUseCase: `ServerProjectileDeliveryPolicy.IsProjectileTower(tower.Type, projectileRace)` · `_projectiles.TryLaunchTowerProjectile(tower, projectileRace, target, launchPower, this)` · `projectilePresentationKey: key` | Presenter의 이 다섯 줄과 `EffectManager`의 세 메서드 이름·인자 순서를 바꾸면 실패한다. 줄을 **더하는 것**은 `Contains` 검사라 통과한다 |
| Presenter 다른 게이트 | 같은 파일 1148~1149 · 1157~1159 · 1563~1564 | `attackerView.PlayServerProjectileLaunchCue()` · `!midFlight` 등 | 유닛 경로 — 건드리지 않음 |

- `AudioManager` 격리: 차지 유닛 fixture(VfxConnections 92~103행, ServerProjectiles 1403~1418행)는 `AudioManager` 싱글턴을 null로 바꿔 격리하지만, 타워 writer fixture(1717행~)는 격리하지 않는다. 타워 공격음 호출을 `AudioManager.Instance?.`로 넣으면 Edit Mode에서 인스턴스가 없으면 아무 일도 없고, 있어도 클립이 없으면 `PlaySfxClip` 559행에서 끝난다 [코드 읽기 — 실행 관측 아님].
- `SoundConfig`·`AudioManager`를 직접 검사하는 에디터 검증은 찾지 못했다(`Assets/Editor`에서 `SoundConfig` 검색 — 위 격리 코드와 `Editor/Tools/SfxNormalizeTool.cs`만 나옴).

---

## 9. 영향 범위

### 9-1. 바뀌는 것 (Plan에서 확정)

| 영역 | 파일 |
|------|------|
| 표현 설정·조회 | `Presentation/Effects/BuildingEffectConfig.cs`, `Resources/Config/BuildingEffectConfig.asset`, (새 파일 후보) `Presentation/Effects/TowerAttackProfile.cs` |
| 표현 분기 | `Presentation/Effects/EffectManager.cs`, `Presentation/Effects/HitPresentationQueue.cs` |
| 소리 | `Infrastructure/Config/SoundConfig.cs`, `Presentation/Audio/AudioManager.cs`, 호출 지점 `ProjectilePresentationPresenter.cs`(줄 추가만) · `HitPresentationQueue.cs` |
| 에디터 검증 | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` |
| 규칙 문서(승인 후) | `GameSystemRules_Buildings.md` 규칙 12, `GameSystemRules_Sound.md` 규칙 3·15·16 |
| 현황 문서([11] 단계) | `Assets/VFXSFXList.md` 「건물 공격 이펙트」 142~151행, `PROJECT_STATUS.md`·`ROADMAP.md`·`WORK_HISTORY.md` |

### 9-2. 바뀌지 않아야 하는 것

- 서버 판정 전체: `TowerCombatUseCase.cs`(피해·스냅샷 발행·리플렉션 대상 이름), `ProjectileDeliveryContracts.cs`(H-1~H-5, 핸드셰이크 토큰 `"6,7,12,17,21,24,26;tower:human"` — ServerProjectiles 237행), `ProjectileConfig` 에셋, 진단 코드.
- 스냅샷 구조·RPC(`AttackEffectClientRpc`), 공격 결과 식별·중복 거부.
- RuneSpire 번개의 시작점·추적·고정 동작(`VfxPoolItem.TowerBolt.cs`) — 분기 조건만 바뀐다. 번개 끝점 OPEN 문제(Buildings 규칙 12 2026-10-13 블록 「상태」)는 결정 7로 제외.
- Human 타워 연출 결과(포구 발사 이펙트 · CannonCart 트레이서·착탄) — 같은 프리셋이 새 프로필 행으로 옮겨 갈 뿐이다.
- `BuildingFactory` 초기 회전(Buildings 규칙 11).

---

## 10. 다른 두 task와의 접점 [확인 — 문서 기준]

| task | 상태 | 이 작업과 겹치는 것 |
|------|------|-------------------|
| ① [`13_39_unit-attack-sfx-tiering`](../13_39_unit-attack-sfx-tiering/Research.md) | Research만 있음, **Plan 미작성** | `AudioManager.cs`(동시 재생 한도 개편 · 특수·타워 우선 — 11-14 ③), `GameSystemRules_Sound.md`(규칙 13 개정), `SoundConfig.asset`(유닛 행 · 「찾을 수 없음」 참조 정리 — 미실행), `VFXSFXList.md` SFX 절. **「특수/타워」 분류로 재생하는 입구의 이름·형태는 ① Plan이 정한다 — 지금 [미확인]** |
| ③ [`14_40_vinetower-attack-design`](../14_40_vinetower-attack-design/Research.md) | Research(11-6까지 결정 반영) + [Plan](../14_40_vinetower-attack-design/Plan.md)(승인 대기 — 0절에서 차단 4곳 X-1~X-4를 이 작업으로 넘기고, 4절·5-6절이 이 작업의 결과를 전제로 쓰임) | 그 Research 6-1·11-4의 변경 대상(`EffectManager` 558·568행, `BuildingEffectConfig.cs` 53행, 에셋 `_autoTowerAttacks` race 2 행, `HitPresentationQueue` 412~413·416행, 검증 49행, 규칙 12, `VFXSFXList` 144·146~151행)이 **모두 이 작업이 먼저 바꾸는 곳**이다. 이 작업 뒤에는 ③이 「VineTower 프로필 행에 GroundBurst 프리셋 하나를 채우는 일 + 검증 개정」으로 줄어든다. 그 Research의 행 번호·구조 서술은 이 작업 구현 뒤 옛 기준이 된다 |
| ④ 효과음 2단계 | 문서 없음 | 이 작업이 만든 `SoundConfig` 타워 공격음 칸에 클립을 넣는다(Human = Tank 포격 공용, RuneSpire 전용, VineTower는 VFX 확정 후 — 결정 5) |

---

## 11. 작업 중 발견한 부가 사항 (이번 범위 아님 — 보고만)

1. **`VFXSFXList.md`의 옛 행 번호:** 144행·149행이 「`EffectManager.cs` 529행이 Transcendence 타워를 … 재생하지 않는다」고 적었으나 지금 그 분기는 558행이다(529행은 `PlayBuildingAttack` 안). 이 작업 [11] 문서 반영 때 같이 고칠 후보.
2. **건물 파괴·업그레이드 효과음도 종족을 구분하지 못한다:** `VFXSFXList.md` 「건물 파괴 사운드」(371행~)·「건물 업그레이드 사운드」(417행~) 표는 `sfx_cannontower_destroy.wav` · `sfx_vinetower_destroy.wav`처럼 종족별 타워 파일을 계획하지만, `SoundConfig`의 건물 칸은 `BuildingType` 키뿐이라(6절) 지금 구조로는 종족별로 다르게 낼 수 없다. 이번 결정은 **타워 공격음만** 「건물 종류 + 종족」 키로 넓힌다 — 파괴·업그레이드는 사용자 결정 없음.
3. **game-programmer 메모리의 옛 설명:** `.claude/agent-memory/game-programmer/MEMORY.md` 429행이 「타워 발사 VFX(3-1): `BuildingEffectConfig.attackPreset` + `GetAttack`, `EffectManager.PlayBuildingAttack`」를 적고 있다. 이 작업 구현 뒤 사실과 달라진다 — [9] 메모리 갱신 때 보고 대상(직접 수정하지 않음).
4. **결과 토큰과 로그:** `PlayAttackEffect`의 결과 문자열(`AuthoritySnapshotStarted` 등)은 `AttackEffectIdentity.Record`로 런타임 로그에 남는다 — 예전 실기 로그(`_Logs/2026-10-16/11_31_logcat/RuntimeLog_device.txt` 등)에서 이 토큰들이 검색된다. `LogRules.md`에는 이 토큰 목록이 없다(`AttackEffect` 검색 0건). `UnspecifiedTranscendence`를 다른 이름으로 바꾸면 옛 로그와 새 로그의 표기가 달라진다.

---

## 12. 직접 확인하지 못한 것

- 효과음 task ①의 「특수/타워」 우선 재생 입구 이름·형태 — ① Plan 미작성 [미확인].
- ③ VineTower가 쓸 실제 프리셋 이름 — ③ Plan(승인 대기)의 제안 이름이며 제작 전 [미확인].
- 이 문서의 모든 동작 설명은 코드 읽기 기준이다. 실행·실기 관측은 하지 않았다.
- 전달 조사가 「9곳」·「호출처 0」으로 센 기준 — 4절·7절 U-3처럼 줄 단위로 다시 셌고, 세는 방식 차이만 있었다.
