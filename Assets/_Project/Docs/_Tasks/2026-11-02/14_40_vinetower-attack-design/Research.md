# VineTower 가시 덩굴 공격 — 모션·VFX 설계 Research

> **작성일:** 2026-11-02 · **작성:** document-manager · **기준 커밋:** `6e1d3167` (브랜치 `claude/vfx-code-followups`)
> **상태:** 조사 문서. Plan.md는 아직 쓰지 않는다(사용자 지시 「VineTower 관련 task 문서 작성해」 — 이번 범위는 Research만).
> **표기:** [확인] = 이 문서 작성 중 파일을 직접 열어 확인 · [전달] = 메인 세션·사용자 발언 전달(직접 확인 못 함) · [미확인] = 확인하지 못함(이유 함께 기록). 행 번호는 위 기준 커밋 기준이며 메서드·필드 이름을 함께 적는다(행 번호는 코드가 바뀌면 어긋난다).

---

## 1. 이 작업이 무엇인가 (자연어 설명)

초월(Transcendence) 종족의 방어 타워 **VineTower**는 지금 적을 공격해도 **화면에 아무 공격 효과가 나오지 않는다.** 피해는 서버에서 정상으로 들어가고 맞은 유닛의 HP 숫자도 뜨지만, 「타워가 무엇으로 때렸는지」 보이는 연출이 없다. 인간 타워는 대포알이 날아가고, 정령 타워는 번개가 꽂히는데 VineTower만 비어 있다.

사용자는 VineTower가 **촉수(덩굴)를 쓰는 공격**을 하기를 원하며, 그 움직임과 이펙트를 어떻게 만들지 고민하고 있다. 2026-10-15에 이미 기록해 둔 아이디어가 있어서, 사용자는 그 **기존 컨셉을 기준으로 설계**하기로 정했다. 그 컨셉은 이렇다.

> 맞는 적의 **발밑 땅에서 가시 덩굴이 솟구쳐 올라** 적을 휘감듯 내리치고, 다시 **땅속으로 들어간다.** 그때 흙과 파편이 튄다.

이 문서는 그 연출을 실제로 만들려면 **무엇이 필요한지**를 조사한다 — 지금 코드·에셋·규칙 문서가 어떤 상태인지, 덩굴 모양과 움직임을 무엇으로 만들 수 있는지, 바꿔야 하는 코드와 규칙이 무엇인지, 무엇이 위험한지, 그리고 **사용자가 정해 줘야 하는 것**(덩굴 몇 개, 얼마나 굵게, 가시를 달지, 얼마나 오래 보일지, 피해 숫자가 뜨는 순간과 맞출지 등)을 정리한다. 어떤 방법으로 만들지는 이 문서에서 정하지 않는다 — 사용자 답을 받은 뒤 Plan에서 정한다.

---

## 2. 사용자 결정과 전달 사항

| # | 내용 | 출처 |
|---|------|------|
| U-1 | VineTower 공격은 **촉수(덩굴)를 사용하는 공격**이기를 원한다. 모션·VFX를 어떻게 만들지 고민 중 | [전달] 사용자 2026-11-02 (메인 세션) |
| U-2 | 메인 세션이 기존 기록을 보여 준 뒤 **「기존 컨셉 기준」으로 설계**하기로 결정 — 아래 컨셉 (A) 채택, (B) 기각 그대로 | [전달] 사용자 2026-11-02. 같은 결정이 [유닛 공격 SFX 등급 Research](../13_39_unit-attack-sfx-tiering/Research.md) 369행·479행에도 「2차 답변: 기존 컨셉 기준 설계로 결정」으로 적혀 있다 [확인] |
| U-3 | VineTower 공격음은 **이 설계가 확정된 뒤** 정한다 | [확인] 같은 SFX Research 369행 「공격음은 설계 확정 후(11-12)」 |
| U-4 | 「타워 공격음 코드는 임시로 작성된 코드고, 현재는 컨셉별로 나누어 사용하기 때문에 개편할 것」 — 타워 공격 표현 코드가 **개편 예정**. 개편 범위·시점은 **미결** | [전달] 사용자 2026-11-02 (메인 세션 추가 전달). 이 발언이 가리키는 코드가 정확히 어느 것인지는 [미확인] — 6-3절 참고 |

**기존 컨셉 원문 위치** [확인]: `Assets/_Project/Docs/Assets/VFXSFXList.md` 146~151행 「VineTower 공격 VFX 계획 (2026-10-15 사용자 아이디어 기록 — 계획만, 미착수·미승인 구현)」.
- **(A) 가시 덩굴 솟구치기 — 채택:** 대상 유닛 발밑 땅에서 솟구쳐 휘감듯 내리치고 다시 땅속으로. 흙·파편 파티클. 구성 = **애니메이션되는 메시 오브젝트**(솟구침 → 휘감기 → 들어감) + 흙·파편 **파티클**. 착탄형이라 대상 위치 지면에 놓는다.
- **(B) 가시 씨앗 투사체 — 기각:** 타워에서 씨앗을 쏘아 날리는 방식. 「덩굴 지하 공격 컨셉과 맞지 않아」 채택하지 않음.
- 제안 파일명: `vfx_vinetower_attack.prefab` (같은 표 144행, 「제안 파일명, 미착수」).

---

## 3. 현재 상태

### 3-1. 문서

| 문서 | 내용 | 확인 |
|------|------|------|
| `Assets/_Project/Docs/StatsReference.md` 249행 「방어 건물」 표 | VineTower: HP 1000 · 건설 비용 175 · **공격력 150 · 공격 사거리 4.0 · 공격 쿨다운 5.0s** · 투사체 속도 「— (즉발 유지)」 | [확인] |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 서두 (93~102행) | 종족별 방어 타워는 `BuildingType.AutoTower` 하나. Transcendence = VineTower. MistShrine은 방어 타워가 아니다 | [확인] |
| 같은 문서 규칙 9 「서버 권위 처리」 (164~166행) | 타겟 선택·사거리·피해는 서버에서만. 클라이언트는 결과를 시각 표현에만 쓴다 | [확인] |
| 같은 문서 규칙 11 (180~187행) | Human CannonTower 배치 초기 회전 규칙 — **VineTower는 따르지 않는다** | [확인] |
| 같은 문서 규칙 12 「타워 발사 연출」 (193~199행) | 발사 프리셋은 **BuildingType + Race**로 선택. **「Transcendence VineTower는 미지정 상태에서 다른 종족 preset으로 fallback하지 않는다.」** Human 외 타워는 확정 공격 결과 identity·Impact 위치/시각·Race snapshot을 표현에 전달하고 **현재 target 재조회·tracer 도착으로 피해를 재판정·지연하지 않는다.** 2026-10-20 개정: **「Spirit RuneSpire와 Transcendence VineTower는 즉발 데미지를 유지한다(히트 딜레이를 도입하지 않는다).」** Human 외 타워의 맞은 유닛 피격 표현은 「서버의 동일 타워 공격 결과 ID와 Impact 시각에 연결해 **즉시** 재생」. 발사 VFX는 사운드 규칙 15(VFX+SFX 쌍)를 지킨다 | [확인] |
| 같은 문서 규칙 12 「2026-10-13 추가 — Spirit(RuneSpire) 번개의 화면 표현 추적」 (203~214행) | 「현재 target 재조회 금지」는 **타워의 새 대상 탐색** 금지이며, 원래 맞은 유닛을 화면 표현으로만 따라가는 것은 해당하지 않는다. **적용 범위: Spirit만 — 「Transcendence(미지정, fallback 없음)는 바뀌지 않는다」** | [확인] |
| `Assets/_Project/Docs/Assets/VFXSFXList.md` 144행 | VineTower 공격 VFX 「미지정」, 타 종족 fallback 금지. 2026-10-15 실측 메모 「`EffectManager.cs` 529행이 Transcendence 타워를 `UnspecifiedTranscendence`로 재생하지 않는다」 — **현재는 558행**(아래 3-2) | [확인] 행 번호 차이는 이후 코드 추가로 밀린 것으로 보인다(메모 자체는 2026-10-15 기준 사실) |
| 같은 문서 149행 「코드 선행 조건」 | 「`VfxPoolItem.cs` 548행 `AnyAlive()`, 494행 반환 판정」 — **현재는 571행 `AnyAlive()`, 517행 반환 판정**(아래 3-2) | [확인] 같은 이유 |
| `Assets/_Project/Docs/Assets/CommonAssetGuide.md` 124행 「초월계」 표 | VineTower 외형 「지면에 낮게 퍼진 거대 덩굴 군집, 넓적한 실루엣」. **현행 표에는 「역할」 칸이 없어 「방어 타워 — 덩굴 지하 공격」 문구가 없다** | [확인] |
| `Assets/_Project/Docs/History/ImplementationVerificationHistory.md` 10999행 | 옛 CommonAssetGuide 표 원문: VineTower 역할 「**방어 타워 — 덩굴 지하 공격**」 | [확인] VFXSFXList 147행은 이 문구를 「CommonAssetGuide의 컨셉」으로 인용하지만, 현행 CommonAssetGuide에서는 문서 역할 정리로 History에 옮겨졌다. 컨셉 출처를 Plan에서 인용할 때는 History 원문 위치를 함께 적어야 한다 |
| `Assets/_Project/Docs/Assets/VFXSFXGuide.md` 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 (217행~) | 「1회성 착탄」 계약: Looping OFF · Stop Action None · **길이 약 1초 이하** · 풀 재사용 전제. 근거 = `EffectManager.PlayAttackEffect`가 모든 하위 시스템 Stop Action을 None으로 덮어쓰고, **풀은 파티클이 모두 사라지면 반환**. 「4. 길이가 바뀌는 빔」은 RuneSpire `Bolt` 로컬 Z 0→1 구조 | [확인] |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` 107행 규칙 15 「VFX+SFX 쌍 호출 규칙」 | VFX 연결 시 SFX 쌍(또는 「미제작」 주석) | [확인] 제목 행만 확인, 본문 세부는 읽지 않음 |
| `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UnitCombatSynchronization.md` | `RuneSpire` · `VineTower` · 「타워 발사」 문자열 0건. 타워 발사 스냅샷 전송을 따로 다루는 규칙 조항은 찾지 못함. 관련 일반 조항: NET-PRESENT-003 「피격 표현 상관관계」(331행~), NET-AUTH-001(규칙 12가 인용) | [확인] grep 결과. 규칙 문서 전체를 읽지는 않았으므로 「타워 표현을 다루는 조항이 전혀 없다」는 [미확인] |
| `Assets/_Project/Docs/TechnicalDesignDocument_Network.md` 28행 | 서버 권위 투사체 대상 = 유닛 7종 + Human CannonTower. VineTower 언급 0건 | [확인] |

### 3-2. 코드 — VineTower 공격 한 번이 지나가는 길

**① 서버 피해 (즉발)** — `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` [확인]
- 머리 주석 31~38행: 「Spirit RuneSpire · Transcendence VineTower는 바뀌지 않는다 — 기존 즉발 경로 그대로」, 「즉발 피해 · Tower 발사 스냅샷 경로는 지우지 않는다 — RuneSpire · VineTower가 지금도 쓰는 유일한 피해 경로」.
- `ExecuteTowerAttack` (247행~): `ServerProjectileDeliveryPolicy.IsProjectileTower`가 false인 타워(VineTower 포함)는 즉발 경로로 간다.
  - 264~272행: 표현용 좌표를 구한다 — `effectSource` = 타워 월드 위치, `effectImpact` = **대상 유닛의 현재 월드 위치(발밑 — `VfxPoolItem.TowerBolt.cs` `BeginTowerBolt` 안 주석 「스냅샷 Impact도 발밑 좌표이므로」 근거)**, `effectTime` = 표현 시계.
  - 285행 `target.TakeDamage(damage)` — **피해는 이 순간 바로 들어간다.**
  - 289~292행: `AttackEffectVisual.Tower`, `Race`(= Transcendence) 스냅샷을 만들어 `GameEvents.OnAttackEffectConfirmed`로 발행. 스냅샷에는 Source·Impact·ImpactTime·AppliedDamage·ResultHp가 들어간다.
  - 304행 `OnEntityDamaged` 발행(공격자 = 타워, `attackerIsUnit: false`), 대상이 죽었으면 315행 `OnUnitDied` 발행 후 318행에서 **곧바로 `_unitSpawn.RemoveUnit`**.
- `ServerProjectileDeliveryPolicy.IsProjectileTower` (`Assets/_Project/Scripts/Application/Combat/ProjectileDeliveryContracts.cs` 334행): 주석 「RuneSpire·VineTower는 항상 false」 [확인].

**② 스냅샷 전송 (멀티플레이)** — `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` [확인]
- 331행: 서버가 `OnAttackEffectConfirmed`를 구독 → `PublishAttackEffect`(70행) → `AttackEffectClientRpc`(80행)로 Race·Source·Impact·ImpactTime 등 전체를 클라이언트에 보낸다. 클라이언트는 `OnAttackEffectReceived`로 넘긴다.
- 즉 **VineTower 스냅샷은 지금도 클라이언트까지 전송되고 있으며, 화면에서 재생만 안 한다.** 새 RPC 없이 기존 스냅샷의 Impact(대상 발밑 위치)로 덩굴 위치를 정할 수 있는 구조다(재생 쪽 코드 변경은 필요 — 아래 ④).

**③ 화면 수신·재생 대기열** — `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` [확인]
- 518~520행: Host/싱글은 `OnAttackEffectConfirmed`, 순수 Client는 `OnAttackEffectReceived`를 `ReceiveAttackEffect`로 받는다.
- `EmitAttackEffects` (390행~): 타워 스냅샷은 **표시 시각을 기다리지 않고 받은 즉시 재생**(주석 400행). 화면이 뒤집힌 Client는 Source·Impact를 `ViewConverter.ToView`로 바꾼다(402~403행). Spirit일 때만 맞은 유닛의 화면 오브젝트를 해석해 넘긴다(412~413행).
- 416행: 재생 실패 중 **`UnspecifiedTranscendence`는 실패 수에 세지 않는다** — VineTower 미지정을 의도된 상태로 취급하는 코드.
- 맞은 유닛의 HP 숫자·피격 표현: `OnEntityDamaged`에서 공격자가 유닛이 아니면(타워) **보류 없이 즉시 방출**(646~658행 「ⓒ 공격자가 유닛이 아니면(타워)」). 주석 653행 「Tower VFX has a sole emitter from the confirmed authority snapshot, not this HP event」.
- `PlayTowerAttackVfx` (1494행) → `EffectManager.PlayBuildingAttack` 경로는 **호출하는 곳이 없다**(전체 `grep` 결과 정의 1건뿐) [확인]. 타워 VFX의 현재 유일한 재생 경로는 위 스냅샷 경로다.

**④ 재생 판정** — `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` `PlayAttackEffect` (551행~) [확인]
- 558행: `if (s.Race == RaceId.Transcendence) { outcome = "UnspecifiedTranscendence"; return false; }` — **VineTower는 여기서 끝난다.**
- 559행: 프리셋 = `_buildingConfig.GetAttack(BuildingType.AutoTower, s.Race)`.
- 564~565행: 프리팹에 **ParticleSystem이 하나도 없으면** `ParticleSystemUnavailable`로 재생 거부. 566행: 모든 하위 파티클의 Stop Action을 None으로 덮어씀.
- 568~569행: **재생 위치 = `source`(타워 위치)** + 프리셋 위치 보정. 회전 = 타워→대상 수평 방향. → 컨셉 (A)는 **대상 발밑(`impact`)**에 놓아야 하므로 이 위치 선택이 VineTower용으로 달라져야 한다.
- 577~579행: Spirit만 `BeginTowerBolt`(끝점 추적). 580행 주석 「Sound15: impact and tower SFX are unspecified」.

**⑤ 프리셋 설정** — `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs` [확인]
- `GetAttack(BuildingType, RaceId)` 50~56행: **53행 `if (race == RaceId.Transcendence) return null;`** — 설정 에셋에 Transcendence 행을 추가해도 이 줄 때문에 반환되지 않는다. 즉 **에셋 행 추가 + 이 줄 + `EffectManager` 558행** 세 곳이 함께 바뀌어야 연결된다.
- `Assets/_Project/Resources/Config/BuildingEffectConfig.asset` 15~19행 `_autoTowerAttacks`: race 0, race 1 두 행뿐. 20~23행 `_autoTowerProjectiles`: race 0 한 행 [확인].

**⑥ 풀 반환 수명** — `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` [확인]
- 머리 주석 12~20행: 「`ParticleSystem.IsAlive(true)` → 모든 파티클이 소멸하면 Return」, 「ParticleSystem 컴포넌트는 프리팹 루트에 있어야 한다」(루트에 없으면 직속 자식 시스템들을 각각 재생 — 46행 `_childSystems`).
- `Update` (487행~) → 517행: `if (!AnyAlive() && (!_waveDriven || _waveAge >= _wave.Duration)) Return();`
- `AnyAlive` (571행~): 루트 PS 또는 형제 시스템 중 하나라도 살아 있으면 true. **메시·Animator의 재생 상태는 보지 않는다.**
- 선례 두 가지:
  - **파도(`_waveDriven`)**: `_wave.Duration`이 지날 때까지 반환하지 않는다(517행) — 「정해진 수명만큼 유지」.
  - **힐 루프(`_bloomLoop`)**: 정지 신호 전까지 반환하지 않는다(501~508행) — 「외부 신호로 끝냄」.
- `Play` (350행~): 위치·회전·스케일 설정 후 `SetActive(true)`, 파티클 `Clear()` + `Play()`. **Animator·애니메이션을 처음부터 다시 트는 코드는 없다.**
- `ResetTravelState` (540행~): 재생별 상태 초기화(파도, 이동, `ResetTowerBolt`, `ResetCasterOwnership`). 애니메이션 상태 초기화 항목은 없다.

**⑦ RuneSpire 번개 (참고 선례)** — `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` [확인]
- 머리 주석 3~33행: 번개 길이를 실제 거리에 맞추고, **원래 맞은 유닛만** 화면 표현으로 따라가며(매 프레임 ID 재조회 없음), 대상이 죽거나 사라지면 마지막 지점에 고정.
- `BeginTowerBolt` (138행~), `LateUpdate` (183행~)에서 끝점 갱신.
- 컨셉 (A)가 「대상을 휘감는」 동안 대상이 걸어가면 덩굴이 따라가야 하는지에 이 선례를 쓸 수 있다(사용자 확인 Q-7).

**⑧ 자기 검증(Editor)** [확인]
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 48~49행: `buildings.GetAttack(BuildingType.AutoTower, RaceId.Transcendence) == null`을 **요구**한다(메시지 「Race selector preserves unspecified tower.」). 연결하면 이 검사도 바뀌어야 한다.
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs` 242행(`IsProjectileTower(AutoTower, Transcendence)`가 false여야 함), 358·699행(`towerPath=…|Transcendence:s0/l0` 문자열). 컨셉 (A)는 투사체가 아니므로 이 검사들은 그대로 유효할 것으로 보이나, 실제 영향은 [미확인](Plan에서 game-programmer 확인).

### 3-3. 에셋 [확인]

| 대상 | 상태 |
|------|------|
| 타워 모델 `Assets/_Project/Models/Buildings/Transcendence/VineTower.fbx` | 메타 87행 `importAnimation: 0`, 37행 `clipAnimations: []`, 106행 `animationType: 0` — **애니메이션 클립 없음** |
| 타워 프리팹 `Assets/_Project/Prefabs/Buildings/Transcendence/Building_VineTower_Blue.prefab` · `_Red.prefab` | 각각 GameObject 2 · Transform 2 · MeshFilter 1 · MeshRenderer 1. **Animator 없음** → 타워 자체의 공격 모션은 지금 없다 |
| 타워 텍스처 `Assets/_Project/Texture/Buildings/Transcendence/VineTower/VineTower/` | `tex_vinetower_blue_base.png` · `_red_base.png` · `_emission.png` · `_metallic.png` (덩굴 VFX 재질 재사용 후보 — 실제 쓸 수 있는지는 [미확인]) |
| VineTower 공격 VFX 프리팹 | **없음.** `Assets/_Project/Prefabs/VFX/Buildings/`에는 `vfx_flightfacility_bombardment.prefab`, `vfx_runespire_attack.prefab`뿐 |
| VFX 프리팹 중 Animator/Animation 컴포넌트를 가진 것 | **0건**(`Prefabs/VFX` 전체에서 클래스 ID 95·111 검색). → **「애니메이션되는 메시」 VFX는 이 프로젝트에 선례가 없다** |
| VFX 프리팹 중 MeshRenderer를 가진 것 | `vfx_eaglearcher_tracer_blue/red.prefab` 2건뿐(날아가는 화살 — 메시를 코드가 이동시킬 뿐 애니메이션 없음) |
| `vfx_runespire_attack.prefab` 구성 | GameObject 4(루트 · `Source_Flash` · `Bolt` · `Impact_Sparks`), ParticleSystem 4, ParticleSystemRenderer 4 — **번개 「메시」도 파티클 시스템**이다 |
| 패키지 (`Packages/manifest.json`) | `com.unity.timeline` 1.8.10, `com.unity.modules.animation`, URP 17.3.0 있음. Splines · VFX Graph 패키지는 manifest에서 찾지 못함 |

---

## 4. 컨셉 (A)를 만들려면 필요한 요소

| # | 요소 | 무엇이 필요한가 | 근거 / 상태 |
|---|------|----------------|------------|
| E-1 | 덩굴 형태 | 가시 덩굴(촉수) 모양 — 메시 또는 파티클로 표현 | 에셋 없음(3-3). 제작 수단은 5절 |
| E-2 | 덩굴 움직임 | 솟구침 → 휘감기/내리치기 → 땅속으로 들어감 | 선례 없음(Animator 가진 VFX 0건). 제작 수단은 5절 |
| E-3 | 흙·파편 파티클 | 덩굴이 나오고 들어갈 때 튀는 흙·파편 | 「1회성 착탄」 계약(VFXSFXGuide 1절) 적용 대상. 단, 그 계약의 「길이 약 1초 이하」가 덩굴 길이와 맞는지는 Q-4 |
| E-4 | 풀 반환 수명 | 메시·애니메이션이 파티클보다 길면 파티클이 먼저 사라지는 순간 풀로 반환돼 덩굴이 잘린다 → 「고정 수명」 또는 「애니메이션 종료 신호」를 반환 판정에 넣는 코드 | 3-2 ⑥. 파도(`_waveDriven`)·힐(`_bloomLoop`) 선례. 덩굴을 **전부 파티클로** 만들면 이 변경이 필요 없을 수 있음(5절 M-3) |
| E-5 | 풀 재사용 시 애니메이션 처음부터 | 풀에서 다시 꺼낼 때 애니메이션을 처음 프레임부터 다시 트는 처리 | 3-2 ⑥ `Play`에 Animator 처리 없음. Unity Animator가 `SetActive(true)` 때 어떻게 동작하는지 이 프로젝트에서 검증한 기록 없음 — [미확인] |
| E-6 | 재생 위치 = 대상 발밑 | 현재 `PlayAttackEffect`는 타워 위치(`source`)에서 재생 → VineTower는 스냅샷 `Impact`(대상 발밑)에서 재생하도록 분기 | 3-2 ④ 568~569행. 스냅샷 Impact는 이미 대상 발밑 좌표이고 Client 뒤집힘 변환도 이미 됨(3-2 ③) |
| E-7 | 프리셋·설정 연결 | `EffectPreset` 신규 + `BuildingEffectConfig.asset` `_autoTowerAttacks`에 race 2 행 추가 + `BuildingEffectConfig.GetAttack` 53행 제거/변경 + `EffectManager` 558행 분기 변경 + `HitPresentationQueue` 416행 예외 문자열 정리 + Editor 자기 검증 49행 변경 | 3-2 ④⑤⑧ |
| E-8 | 규칙 문서 변경 | Buildings 규칙 12의 「Transcendence VineTower는 미지정 … fallback하지 않는다」 서술, 같은 규칙 2026-10-13 블록 「Transcendence(미지정, fallback 없음)는 바뀌지 않는다」, VFXSFXList 144·146~151행 | 3-1. **규칙 변경은 사용자 승인 후**(VFXSFXList 149행에도 같은 조건) |
| E-9 | 피해 시점과 보이는 시점 | 피해·HP 숫자는 서버 공격 순간 즉시(3-2 ①③). 덩굴이 땅에서 솟아 「내리치는」 순간은 재생 시작 후 얼마 뒤가 된다 → 숫자가 먼저 뜨고 덩굴 타격이 나중에 보일 수 있다 | 규칙 12 「즉발 데미지를 유지한다(히트 딜레이를 도입하지 않는다)」·「즉시 재생」. 맞출지 여부는 Q-6 |
| E-10 | 대상 사망 시 | 이번 타격으로 죽으면 서버가 바로 `RemoveUnit`(3-2 ①). 덩굴이 「휘감는」 동안 유닛 사망 연출과 겹친다 | RuneSpire에도 「맞는 순간 대상이 죽으면 끝점이 살짝 어긋남」 OPEN이 있다(규칙 12 2026-10-13 블록 「상태」, 원인 미검증). VineTower가 대상을 따라가지 않고 스냅샷 위치에 고정되면 이 문제와는 무관할 수 있으나 [미확인] |
| E-11 | 멀티플레이 표현 | 서버가 이미 VineTower 스냅샷을 Client까지 보낸다(3-2 ②). 화면 재생은 각 머신 로컬. 새 RPC는 필요 없을 것으로 보이나, 대상 추적(Q-7)을 하면 RuneSpire처럼 화면 오브젝트 해석이 추가된다 | 3-2 ②③. 동기화 규칙 문서에서 타워 표현을 직접 다루는 조항은 찾지 못함(3-1) |
| E-12 | 공격음 | 사운드 규칙 15 — VFX 연결 시 SFX 쌍 또는 「미제작」 표기 | U-3: 이 설계 확정 후. 현재 `AudioManager`에 타워 공격음 메서드 없음(6-3) |

---

## 5. 제작 수단 후보 (선택하지 않음 — 확인된 것과 미확인 구분)

| # | 수단 | 확인된 사실 | 미확인 |
|---|------|------------|--------|
| M-1 | **Meshy로 덩굴 3D 메시 + 애니메이션** | 프로젝트 가이드 `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md` 163행·282~292행: Meshy 후처리 = 유닛은 「T-pose + Auto-Rig + Walk/Attack/Dead」, 건물·오브젝트는 「리깅 불필요」 [확인] | **사람형이 아닌 덩굴을 Meshy가 자동 리깅·애니메이션할 수 있는지** — 가이드에 기록 없음 [미확인]. 리깅된 메시를 쓰면 SkinnedMeshRenderer + Animator가 필요하고, VFX 풀에 그런 선례가 없다(E-4·E-5) |
| M-2 | **Meshy 정적 메시 + Unity 안에서 움직임**(Animation 클립을 Unity에서 만들거나, 코드로 높이·스케일·회전을 시간에 따라 바꿈) | Unity animation 모듈·Timeline 패키지 설치됨 [확인]. 코드로 Transform을 시간에 따라 바꾸는 선례: `VfxPoolItem`의 전진 이동(`UpdateForwardTravel`), 번개 길이 맞춤(`TowerBolt`) [확인] | 「휘감기」처럼 휘는 움직임을 정적 메시의 이동·스케일만으로 표현할 수 있는지 [미확인] — 휘려면 뼈대(리깅)나 셰이더 변형이 필요할 수 있음 |
| M-3 | **Unity 파티클만으로**(Mesh 렌더 모드 파티클·Stretch 등) | 기존 타워 VFX(`vfx_runespire_attack`)의 번개도 파티클 4개로 만들어졌다 [확인]. 파티클만이면 현재 풀 반환 규칙(`AnyAlive`)이 그대로 맞는다 [확인 — 코드 구조상] | 덩굴이 「솟구쳐 휘감고 들어가는」 형태로 보일 만큼 표현되는지 [미확인] — 시각 판단은 사용자 |
| M-4 | **LineRenderer / 라인 기반**(점 여러 개로 덩굴 곡선을 그리고 코드로 펼침) | 프로젝트 스크립트에서 `LineRenderer`·`TrailRenderer` 사용 0건 [확인] | 선례 없음. 굵기·가시 표현 방법 [미확인]. 풀 반환 수명 코드는 E-4와 같은 문제 |
| M-5 | **Unity AI(Assistant)로 프리팹 생성** | `VFXSFXGuide.md` 서두: 이 프로젝트의 VFX 제작 가이드는 Unity AI Assistant 기준이며 프롬프트에 코드 계약을 적는다 [확인] | Unity AI가 애니메이션 메시가 포함된 프리팹을 만들 수 있는지 [미확인] |
| M-6 | **셰이더(정점 변형)로 휘기** | URP 17.3.0 설치 [확인] | 이 프로젝트에 정점 변형 셰이더 선례 여부 조사 안 함 [미확인] |

> 여러 수단을 섞는 것(예: 덩굴은 M-2, 흙·파편은 M-3)도 가능하다 — 기존 컨셉 원문도 「애니메이션 메시 + 파티클」 조합이다. 어느 조합으로 할지는 사용자 결정(Q-1)이며, 제작 전 짧은 시험 제작으로 모양을 보는 방법도 있다(제안 — 미승인).

---

## 6. 영향 범위

### 6-1. 바뀌는 코드·에셋 (컨셉 (A) 구현 시 예상 — Plan에서 확정)

| 영역 | 파일 | 내용 |
|------|------|------|
| 재생 분기 | `Scripts/Presentation/Effects/EffectManager.cs` `PlayAttackEffect` | 558행 Transcendence 차단, 568행 재생 위치(대상 발밑) |
| 설정 조회 | `Scripts/Presentation/Effects/BuildingEffectConfig.cs` `GetAttack` | 53행 Transcendence null 반환 |
| 풀 수명 | `Scripts/Presentation/Effects/VfxPoolItem.cs` (필요 시 새 partial 파일) | 517행 반환 조건, `Play`의 애니메이션 재시작, `ResetTravelState` |
| 수신 대기열 | `Scripts/Presentation/Effects/HitPresentationQueue.cs` | 416행 `UnspecifiedTranscendence` 예외, (대상 추적 시) 412~413행 해석 분기 |
| 설정 에셋 | `Resources/Config/BuildingEffectConfig.asset` | `_autoTowerAttacks` race 2 행 |
| 새 에셋 | `Prefabs/VFX/Buildings/vfx_vinetower_attack.prefab`(제안 이름) + `EffectPreset` + 메시·재질·애니메이션 | 제작 수단에 따라 다름 |
| Editor 검증 | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 49행 | 「Transcendence 미지정」 요구 |
| 문서 | `GameSystemRules_Buildings.md` 규칙 12, `VFXSFXList.md` 144·146~151행, `VFXSFXGuide.md`(애니메이션 메시 계약이 새로 생기면) | 규칙 변경은 사용자 승인 후 |

### 6-2. 바뀌지 않아야 하는 것
- 서버 피해·타겟 선택·사거리·쿨다운(규칙 9) — `TowerCombatUseCase` 즉발 경로. 2026-10-20 개정 「VineTower는 즉발 데미지를 유지」.
- Human CannonTower 투사체 경로, Spirit RuneSpire 번개 경로(같은 `PlayAttackEffect`·풀 코드를 공유하므로 회귀 위험 — 7절 R-3).
- 공격 스냅샷 구조·RPC(`AttackEffectClientRpc`).

### 6-3. 타워 공격 표현 코드 개편 예정 (U-4)
- 사용자 발언(전달): 「타워 공격음 코드는 임시로 작성된 코드고, 현재는 컨셉별로 나누어 사용하기 때문에 개편할 것」. **개편 범위·시점은 미결.**
- 이 문서에서 확인한 「타워 공격 표현」 관련 코드의 현재 모습 [확인]:
  - 타워 공격음 재생 메서드는 **없다.** `AudioManager`의 건물 메서드는 `PlayBuildingPlaceSfx`(519행) · `PlayBuildingDestroySfx`(527행) · `PlayBuildingUpgradeSfx`(535행) 셋뿐. `SoundConfig`의 건물 항목은 `BuildingType` 키(`BuildingSoundEntry` 74~77행)라 AutoTower 하나로는 종족을 구분하지 못한다.
  - 공격음 자리를 표시한 주석만 있다: `EffectManager.PlayBuildingAttack`(527~536행, 「추후 … `AudioManager.Instance?.PlayBuildingAttackSfx(type)` 짝을 추가」) — 이 메서드는 호출되지 않는 `PlayTowerAttackVfx`(HitPresentationQueue 1494행)에서만 부른다. `PlayAttackEffect` 580행·`TowerCombatUseCase` 286행에는 「Sound15: … unspecified」 주석.
  - 타워 발사 VFX는 종족별로 경로가 갈라져 있다: Human = 서버 투사체 표현(`ProjectilePresentationPresenter`), Spirit = 스냅샷 + `TowerBolt`, Transcendence = 스냅샷 수신 후 미지정 차단. `BuildingType` 단독 경로(`GetAttack(BuildingType)` · `PlayBuildingAttack`)는 남아 있으나 호출되지 않는다.
- 사용자 발언이 위 중 **어느 코드를 가리키는지**(공격음 자리 주석인지, 호출되지 않는 `PlayBuildingAttack` 경로인지, 종족별 분기 전체인지)는 [미확인] — Q-11.
- 이 설계에 주는 영향: VineTower 연결(E-6·E-7)은 바로 그 개편 대상 코드(`PlayAttackEffect`의 종족 분기, `BuildingEffectConfig`의 종족 조회)를 고친다. 개편이 먼저 오면 VineTower 분기를 새 구조에 맞춰 다시 짜야 하고, VineTower가 먼저 가면 개편 때 다시 손댈 수 있다. 같은 날의 [유닛 공격 SFX 등급 task](../13_39_unit-attack-sfx-tiering/Research.md)가 「방어 타워 공격음(코드 추가 포함)」을 범위에 두고 있어(같은 문서 390행) 세 작업이 같은 코드를 만진다 [확인].

---

## 7. 위험

| # | 위험 | 근거 |
|---|------|------|
| R-1 | **애니메이션 메시가 파티클보다 먼저 잘림** — 현재 풀은 파티클만 보고 반환 | 3-2 ⑥ 517·571행 |
| R-2 | **풀 재사용 시 애니메이션이 처음부터 다시 안 나올 수 있음** — `Play`에 애니메이션 초기화 없음, 프로젝트에 애니메이션 VFX 선례 0건 | 3-2 ⑥, 3-3 |
| R-3 | **RuneSpire·Human 타워 회귀** — `PlayAttackEffect`·`VfxPoolItem`·`BuildingEffectConfig.GetAttack`을 공유 | 3-2 ④⑤⑥ |
| R-4 | **숫자와 덩굴 타격의 시차** — 피해·HP 표시는 즉시, 덩굴 타격은 솟구친 뒤. 맞추려고 HP 표시를 늦추면 규칙 12 「즉시 재생」·「히트 딜레이를 도입하지 않는다」 문장과 충돌 | 3-1 규칙 12, 3-2 ①③ |
| R-5 | **대상 사망과 겹침** — 이번 타격으로 죽으면 덩굴이 휘감는 동안 사망 연출이 나온다. RuneSpire에는 같은 상황의 미해결 OPEN이 있다 | 3-2 ①, 규칙 12 2026-10-13 블록 「상태」 |
| R-6 | **「1회성 착탄」 계약(약 1초 이하)과 덩굴 길이 충돌 가능** — 쿨다운 5.0s 안에 끝나야 겹치지 않는다는 점은 확인되나 적정 길이는 미정 | VFXSFXGuide 1절, StatsReference 249행 |
| R-7 | **ParticleSystem 없는 프리팹은 재생 거부** — 덩굴을 메시만으로 만들면 `ParticleSystemUnavailable`로 실패. 흙·파편 파티클이 있으면 회피됨 | 3-2 ④ 564~565행 |
| R-8 | **개편 예정 코드와 겹침** — 6-3. 작업 순서에 따라 이중 작업 | U-4, 6-3 |
| R-9 | **Meshy 비인간형 리깅 가능 여부 불명** — M-1을 고르면 제작 단계에서 막힐 수 있음 | 5절 M-1 |
| R-10 | **모바일 성능** — 타워가 여러 개면 5초마다 덩굴 메시·파티클이 동시에 여러 개. 성능 측정 기록 없음 [미확인] | — |

---

## 8. 사용자 확인 필요 사항 (추정으로 채우지 않음)

| # | 질문 | 왜 필요한가 |
|---|------|------------|
| Q-1 | 덩굴을 무엇으로 만들까 — 5절 M-1~M-6 중(또는 조합) | 수명 코드(E-4·E-5) 필요 여부와 제작 담당(asset-prompt-crafter / Unity 안 작업)이 갈린다 *(2026-11-02 해소 — 사용자 선택 B: 작은 정적 덩굴 모델을 파티클(Renderer Mesh 모드)로 그림. [11절](#decision-20261102-vfx-only-b))* |
| Q-2 | 덩굴 **개수** (한 줄기 / 여러 줄기) | 모양·파티클 양·성능 *(2026-11-02 해소 — 덩굴 2~3개가 대상 주변에서 솟구침. [11-6](#answers-20261102-proposals) ①)* |
| Q-3 | 덩굴 **굵기**와 **가시 여부**(가시를 붙일지, 크기) | 메시·재질 제작 *(2026-11-02 해소 — 손목 굵기 정도의 가시 덩굴. [11-6](#answers-20261102-proposals) ②)* |
| Q-4 | **솟구침부터 땅속으로 들어갈 때까지 전체 길이**(초), 그리고 각 단계(솟구침 / 휘감기·내리치기 / 들어감) 비율 | 풀 수명, 「1회성 착탄 약 1초 이하」 계약과의 관계(R-6) *(2026-11-02 해소 — 솟구침 약 0.15초 → 내리침 → 사라짐, 총 0.6~0.8초. [11-6](#answers-20261102-proposals) ③)* |
| Q-5 | 덩굴 **높이** — 대상 유닛 키에 맞출지(유닛마다 다름), 고정 높이일지 | 큰 유닛·작은 유닛에서 보이는 모습. RuneSpire는 유닛 크기 표의 키 × 0.6을 쓴다(규칙 12 2026-10-13 블록) *(2026-11-02 해소 — 고정 높이, 유닛 키의 약 1.5배 수준. [11-6](#answers-20261102-proposals) ④)* |
| Q-6 | **피해 숫자가 뜨는 순간과 덩굴이 내리치는 순간을 맞출지** — 맞춘다면 (가) 덩굴 동작을 「내리치기」가 첫 프레임 근처에 오게 짜기, (나) HP 표시를 덩굴 타격에 맞춰 늦추기(규칙 12 문장 변경 필요) 등 | R-4. 피해 자체(서버 즉발)는 바꾸지 않는 것이 현행 규칙 *(2026-11-02 해소 — 피해 숫자는 현행처럼 즉시, 덩굴은 0.15초 안에 빠르게 솟구쳐 시차를 줄임. [11-6](#answers-20261102-proposals) ⑤)* |
| Q-7 | 덩굴이 보이는 동안 **대상이 움직이면 따라갈지**(RuneSpire 방식), 공격 순간 자리에 고정할지 | E-11, 3-2 ⑦ *(2026-11-02 해소 — 공격 순간 대상 자리에 고정, 따라가지 않음. [11-6](#answers-20261102-proposals) ⑥)* |
| Q-8 | 이번 타격으로 **대상이 죽을 때** 덩굴을 어떻게 보일지(그대로 끝까지 / 짧게 줄임 등) | R-5 *(2026-11-02 해소 — 대상이 죽어도 덩굴은 끝까지 재생 후 사라짐. [11-6](#answers-20261102-proposals) ⑦)* |
| Q-9 | **타워 자체에 모션**이 필요한지(예: 타워 덩굴이 꿈틀하는 공격 동작) — 현재 타워 모델·프리팹에 애니메이션 없음 | 3-3. 원 컨셉에는 타워 모션 언급이 없다 *(2026-11-02 해소 — 불필요. 사용자 「VFX만 제작하면서 진행하고 싶어」, 타워 프리팹·모델은 수정하지 않는다. [11절](#decision-20261102-vfx-only-b))* |
| Q-10 | 흙·파편 파티클의 **양과 색**(지면 타일과 맞출지) | E-3 *(2026-11-02 해소 — 솟구치는 순간 갈색 흙덩이·작은 파편 적당량. [11-6](#answers-20261102-proposals) ⑧)* |
| Q-11 | U-4 「타워 공격음 코드 개편」이 가리키는 코드와 범위, 그리고 **VineTower 작업과 개편 중 무엇을 먼저** 할지 | 6-3, R-8 *(2026-11-02 — 아직 미결: 메인 세션이 개발 담당에게 현재 타워 3종 읽기 전용 조사를 맡김, 결과 대기. [11-6](#answers-20261102-proposals))* |
| Q-12 | Blue/Red 팀 색 구분이 덩굴 VFX에도 필요한지 — 타워 텍스처는 팀별(`tex_vinetower_blue_base` / `_red_base`)이 있다 | 3-3. 기존 타워 공격 VFX는 팀 구분 기록 없음 [미확인] *(2026-11-02 해소 — 팀 색 구분 없음, 초록·갈색 자연색. [11-6](#answers-20261102-proposals) ⑨)* |

---

## 9. 직접 확인하지 못한 근거

| 항목 | 이유 |
|------|------|
| Meshy가 사람형이 아닌 덩굴 메시를 자동 리깅·애니메이션할 수 있는지 | 프로젝트 문서에 기록 없음, 외부 확인 안 함 |
| Unity Animator가 풀 재사용(`SetActive(true)`) 때 애니메이션을 처음부터 다시 트는지 | 이 프로젝트 VFX에 Animator 선례 0건, 실행 확인 불가 |
| Unity AI Assistant가 애니메이션 메시 프리팹을 만들 수 있는지 | 기록 없음 |
| 정점 변형 셰이더 선례 | 조사하지 않음 |
| 동기화 규칙 문서 전체에서 타워 표현을 다루는 조항이 정말 없는지 | `grep`만 했다(전체 읽기 안 함) |
| `GameSystemRules_Sound.md` 규칙 15 본문 세부 | 제목 행만 확인 |
| Editor 자기 검증 `ServerProjectiles.cs` 검사들에 대한 실제 영향 | 코드 문자열만 확인, 실행 안 함 |
| 사용자 발언 U-4가 가리키는 코드 | 발언 원문은 메인 세션 전달, 대상 코드 특정 불가 |
| 덩굴 VFX의 모바일 성능 | 측정 기록 없음 |
| 사용자 발언 U-1·U-2·U-4 원문 | 메인 세션 전달(U-2는 SFX Research 기록으로 교차 확인) |

---

## 10. 관련 파일 경로

- 컨셉 원문: `Assets/_Project/Docs/Assets/VFXSFXList.md` (144행, 146~151행)
- 옛 역할 문구: `Assets/_Project/Docs/History/ImplementationVerificationHistory.md` 10999행 · 현행 `Assets/_Project/Docs/Assets/CommonAssetGuide.md` 124행
- 규칙: `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 9 · 11 · 12 · `GameSystemRules_Sound.md` 규칙 15 · `GameSystemRules_UnitCombatSynchronization.md` NET-PRESENT-003
- 제작 가이드: `Assets/_Project/Docs/Assets/VFXSFXGuide.md` 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 · `Assets/_Project/Docs/Assets/3DAssetCreationGuide.md`
- 수치: `Assets/_Project/Docs/StatsReference.md` 249행
- 코드: `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` · `Assets/_Project/Scripts/Application/Combat/AttackEffectPresentation.cs` · `Assets/_Project/Scripts/Application/Combat/ProjectileDeliveryContracts.cs` · `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` · `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` · `EffectManager.cs` · `BuildingEffectConfig.cs` · `VfxPoolItem.cs` · `VfxPoolItem.TowerBolt.cs` · `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs` · `Assets/_Project/Scripts/Infrastructure/Config/SoundConfig.cs`
- Editor 검증: `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` · `RunUnitActionSelfValidation.ServerProjectiles.cs`
- 에셋: `Assets/_Project/Resources/Config/BuildingEffectConfig.asset` · `Assets/_Project/Models/Buildings/Transcendence/VineTower.fbx(.meta)` · `Assets/_Project/Prefabs/Buildings/Transcendence/Building_VineTower_Blue.prefab` · `_Red.prefab` · `Assets/_Project/Prefabs/VFX/Buildings/vfx_runespire_attack.prefab`
- 관련 task: `Assets/_Project/Docs/_Tasks/2026-11-02/13_39_unit-attack-sfx-tiering/Research.md`

---

<a id="decision-20261102-vfx-only-b"></a>

## 11. 사용자 결정 반영 (2026-11-02) — VFX만 제작 · B안

> 이 절은 1~10절 작성 뒤에 받은 결정을 덧붙인 것이다. 1~10절의 종전 서술(제작 수단 후보·위험·질문)은 그대로 두고, 바뀐 판단은 여기에만 적는다.

### 11-1. 무엇을 정했나 (자연어)

사용자는 타워 모델을 새로 만들거나 고치지 않고 **공격 이펙트(VFX) 하나만 만들어서** 덩굴 공격을 보이고 싶어 한다. 그래서 「작은 덩굴 모델 하나를 파티클로 여러 번 찍어 내고, 크기와 회전을 시간에 따라 바꿔서 땅에서 솟구치는 것처럼 보이게 하는」 방식(B안)을 골랐다. 덩굴 모델에 뼈대를 넣거나 애니메이션을 만들 필요가 없다.

### 11-2. 경위

| 순서 | 내용 | 출처 |
|------|------|------|
| 1 | 사용자: 「현재 프리팹에서 별도로 3D 모델을 제작하지 않고는 불가능해? VFX만 제작하면서 진행하고 싶어」 | [전달] 메인 세션 |
| 2 | 메인 세션 답: 타워 프리팹·타워 모델은 건드리지 않고 공격 VFX 하나로 가능. 단 VFX에 **파티클이 반드시 있어야 한다**(근거: 3-2 ④ `PlayAttackEffect` 564~565행 — 파티클 0개면 `ParticleSystemUnavailable`로 재생 거부, 7절 R-7). 두 안 제시 — **A안:** 덩굴 그림(스프라이트 시트) 파티클 / **B안:** 작은 정적 덩굴 모델을 파티클(Renderer **Mesh** 모드)로 그리고 크기·회전 변화로 솟구침 표현(리깅·애니메이션 불필요) | [전달] 메인 세션. 파티클 필수 근거는 [확인](3-2 ④) |
| 3 | **사용자 선택: B안(작은 덩굴 모델 파티클)** | [전달] 메인 세션 |

### 11-3. 이 결정으로 바뀌는 것

| 대상 | 종전(1~10절) | 이 결정 이후 | 상태 |
|------|------------|------------|------|
| 제작 수단 (5절) | M-1~M-6 후보, 미선택 | **정적 덩굴 메시 + 파티클 계열로 확정.** M-1(Meshy 리깅·애니메이션)은 쓰지 않는다. 5절 분류로는 M-3(파티클)에 「메시를 파티클로 그리는」 방식을 더한 것이다 | 확정 [전달] |
| 작은 덩굴 모델을 무엇으로 만들지 | — | 미정(Meshy 정적 모델인지, 다른 수단인지 지시 없음) | **미결** |
| 타워 모델·프리팹 (3-3) | 애니메이션 없음 | **수정하지 않는다** — Q-9 해소 | 확정 [전달] |
| 풀 수명 코드 선행 조건 (E-4·E-5, R-1·R-2) | 애니메이션 메시가 파티클보다 길면 잘림 → 고정 수명/종료 신호 코드 필요 | 메인 세션 설명: 애니메이션 메시가 아니므로(덩굴 자체가 파티클) 이 조건은 **약해진다** — 파티클 수명 기준 반환(`VfxPoolItem.cs` 517·571행)을 그대로 쓸 수 있는지 **Plan에서 확인** | 🔴 **미검증**(메인 세션 판단, 코드·실물로 확인 안 함) |
| R-2(풀 재사용 시 애니메이션 재시작) | 위험 | Animator를 쓰지 않으면 해당 없음으로 보이나, 위와 같은 이유로 **미검증** | 미검증 |
| 「1회성 착탄」 계약(약 1초 이하)과 길이 (R-6, Q-4) | 미정 | 바뀌지 않음 — Q-4는 열려 있다 | 미결 |

### 11-4. 여전히 필요한 코드 변경 (결정과 무관하게 남음)

- **재생 위치를 대상 발밑(스냅샷 `impact`)으로** — 지금은 타워 위치(`source`)에서 재생(3-2 ④ 568행, E-6).
- **Transcendence 연결 3곳** — `BuildingEffectConfig.asset` `_autoTowerAttacks` race 2 행 · `BuildingEffectConfig.cs` 53행 · `EffectManager.cs` 558행(3-2 ④⑤, E-7).
- **자동 검증** — `RunUnitActionSelfValidation.VfxConnections.cs` 49행의 「Transcendence 미지정」 요구(3-2 ⑧).
- **`HitPresentationQueue.cs` 416행**의 `UnspecifiedTranscendence` 예외 처리(3-2 ③).
- 위 변경은 모두 **타워 공격 표현 코드 개편(U-4, 6-3)** 대상 코드와 겹친다. 순서는 Q-11로 열려 있다.
- 규칙 문서 변경(E-8)도 그대로 필요하다 — 사용자 승인 후.

### 11-5. 남은 사용자 확인

Q-1·Q-9는 해소(8절 표에 표시). Q-2~Q-8, Q-10~Q-12는 그대로 열려 있다. 위 11-3의 「작은 덩굴 모델을 무엇으로 만들지」가 새로 미결로 추가됐다.

<a id="answers-20261102-proposals"></a>

### 11-6. 사용자 답변 반영 (2026-11-02) — 나머지 질문은 메인 세션 제안대로

> 11-5까지의 서술은 그대로 두고, 이후 받은 답을 여기에만 적는다.

**무엇을 정했나 (자연어):** 사용자는 남은 질문을 메인 세션이 제안한 대로 하기로 했고, 덩굴 모델은 Unity AI로 직접 만들어 보기로 했다. 정리하면 — 덩굴 2~3개가 적 주변 땅에서 빠르게(약 0.15초) 솟구쳐 내리치고 0.6~0.8초 안에 사라진다. 피해 숫자는 지금처럼 바로 뜨고, 덩굴은 공격 순간 적이 있던 자리에 고정되며, 적이 죽어도 끝까지 재생된다.

- 사용자 원문: 「나머지는 제안대로 하면 될 것 같고, Unity AI를 통해서 3D 모델도 제작해 보도록 하자」 — [전달] 메인 세션
- 아래 ①~⑩은 메인 세션 제안이며 위 답으로 **확정**됐다 — [전달] 메인 세션

| # | 확정 내용 | 해소한 질문 |
|---|----------|------------|
| ① | 덩굴 **2~3개**가 대상 주변에서 솟구친다 | Q-2 |
| ② | **손목 굵기** 정도의 **가시** 덩굴 | Q-3 |
| ③ | 솟구침 **약 0.15초** → 내리침 → 사라짐까지 **총 0.6~0.8초**(쿨다운 5초 — StatsReference 249행 [확인]) | Q-4 |
| ④ | **고정 높이**(유닛 키의 약 1.5배 수준) | Q-5 |
| ⑤ | 피해 숫자는 **현행처럼 즉시** 표시하고, 덩굴은 0.15초 안에 빠르게 솟구쳐 시차를 줄인다 | Q-6 |
| ⑥ | 공격 순간 **대상 자리에 고정**(따라가지 않음) | Q-7 |
| ⑦ | 대상이 이 공격으로 죽어도 덩굴은 **끝까지 재생 후 사라짐** | Q-8 |
| ⑧ | 솟구치는 순간 **갈색 흙덩이·작은 파편 적당량** | Q-10 |
| ⑨ | **팀 색 구분 없음**(초록·갈색 자연색) | Q-12 |
| ⑩ | 덩굴 모델은 **Unity AI로 3D 정적 모델 제작**(리깅 없음; Meshy 대신 — 사용자 지정) | 11-3 「작은 덩굴 모델을 무엇으로 만들지」 |

**이 확정으로 달라지는 판단 (문서 대조 — 판정은 Plan에서):**
- ③ 총 0.6~0.8초는 VFXSFXGuide 「1회성 착탄」 계약의 「길이 약 1초 이하」 안에 든다(3-1) — R-6은 이 수치 기준으로 충돌하지 않는다.
- ⑤는 규칙 12 「즉발 데미지 유지」·「즉시 재생」 문장을 바꾸지 않는 선택이다(R-4의 HP 표시 지연 안은 쓰지 않음).
- ⑥은 대상 추적(RuneSpire `TowerBolt` 방식)을 쓰지 않으므로, 3-2 ③의 「Spirit만 화면 오브젝트 해석」 분기를 VineTower로 넓힐 필요가 없다. 위치는 스냅샷 `Impact` 한 번(E-6).
- ④ 「고정 높이」와 「유닛 키의 약 1.5배 수준」은 유닛마다 키가 다르므로 **어떤 키를 기준으로 고정값 하나를 정할지**는 문구만으로 정해지지 않는다 — Plan에서 값과 근거를 적는다. [미확인]
- ⑩ Unity AI가 3D 정적 모델(메시)을 만들 수 있는지는 9절에 「Unity AI가 애니메이션 메시 프리팹을 만들 수 있는지 미확인」으로 적혀 있었다. 정적 모델 생성 가능 여부도 이 문서 작성 중 **확인하지 않았다** [미확인] — 사용자가 제작해 보기로 했다.

**아직 미결 — Q-11 (타워 공격 표현 코드 개편 순서 · 「컨셉별로 나누어 사용」 구조):**
- 사용자: 「현재 작업된 3가지 타워를 파악하면 된다, 파악해 보고 순서를 알려 달라」 — [전달] 메인 세션
- 메인 세션이 개발 담당에게 **읽기 전용 조사**를 맡겼다. **결과 대기** — 순서·개편 범위는 정해지지 않았다(6-3, R-8).
