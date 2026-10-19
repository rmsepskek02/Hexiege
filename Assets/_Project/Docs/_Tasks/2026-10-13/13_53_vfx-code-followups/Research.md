# Research — VFX 코드 후속 3건 (Bloom 힐 파티클 개수 계약 · RuneSpire 번개 끝점 추적 · 상태 이펙트 크기 맞춤)

## 0. 이 문서가 무엇인가 (쉬운 설명)

사용자가 Unity AI로 몇몇 이펙트(VFX) 프리팹을 새로 만들어 main에 올렸다(`88f60e38`). 그런데 프리팹만 바꿔서는 고칠 수 없고 **코드를 고쳐야만 해결되는 문제 3가지**가 남았다. 이 문서는 그 3가지가 지금 코드에서 왜 생기는지를 실제 파일을 읽어 정리한 조사 기록이다. 고치는 방법은 같은 폴더의 `Plan.md`에 있다.

1. **꽃요정(BloomFairy) 힐 이펙트가 아예 안 나온다.** 코드가 "힐 이펙트 안의 파티클 묶음은 정확히 5개여야 한다"고 검사하는데, 새 프리팹은 사용자 결정으로 고리(Bloom_Ring)를 빼서 4개다. 그래서 코드가 이펙트를 "규격 불량"으로 보고 재생을 거부한다.
2. **정령 방어 타워(RuneSpire)의 번개가 움직이는 적을 따라가지 않는다.** 번개가 나가는 약 0.35초 동안 맞은 적이 걸어가도 번개 끝은 처음 자리에 그대로 있다. 조사해 보니 현재 코드는 번개 **방향만** 정하고, 번개 **길이를 거리에 맞추는 코드 자체가 없다.**
3. **화상·독·힐처럼 유닛에 붙는 상태 이펙트가 유닛 크기와 상관없이 항상 같은 크기다.** 그래서 작은 유닛에게는 너무 크고, 큰 유닛에게는 어색하게 보인다. 코드가 프리팹 원래 크기 × 고정 배율(현재 1)만 쓰고, 맞은 유닛의 크기를 보지 않기 때문이다.

세 가지 모두 **화면 표현만의 문제**다. 피해량·회복량·HP·타이밍 같은 게임 규칙(서버 권위)은 건드리지 않는다.

---

> **[2026-10-19 main 병합 메모 — 아래 본문의 경로·메뉴 표기는 작성 당시 그대로 보존]** main(`0978d908`)의 에디터 도구 정리로 `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` · `RunUnitActionSelfValidation.VfxConnections.cs`는 `Assets/Editor/Tools/Combat/` 아래로 옮겨졌고(GUID·partial 보존), 전투 자기 검증 메뉴 이름은 `Hexiege/Combat/Run Full Regression Suite`, 루트 포즈 자기 검증은 `Hexiege/Combat/Diagnostics/Self Validate Root Pose Audit`이 됐다. 본문의 「Unit Action 자기 검증 메뉴」 · 「Self Validate Unit Root Pose Cross Audit」는 이 두 메뉴를 가리킨다. 이 작업이 만든 `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`(`Hexiege/Effects/Measure Unit Recipient Fit`)는 경로 변경이 없으며, WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」의 현재 진입점 표에 들어 있지 않다 — 유지 여부는 사용자 결정 대기다. 현행 도구 목록은 [WORKFLOW](../../../WORKFLOW.md) [5-2].

## 1. 조사 범위와 근거 수준

- 조사 시점: 2026-10-13. 작업 브랜치 `claude/vfx-code-followups`(메인 세션이 origin/main `88f60e38`에서 생성)의 작업 트리 파일을 직접 읽었다.
- **git 명령은 이 조사에서 한 번도 실행하지 않았다**(위임 지시). 따라서 작업 트리가 정확히 `88f60e38`과 같은지는 이 문서가 직접 확인한 사실이 아니라 **메인 세션 전달값**이다.
- 아래 `파일:행` 표기는 이 시점 작업 트리 기준이다. 행 번호는 파일이 바뀌면 낡으므로(`.claude/mistakes.md` 2026-08-24 「행 번호로 위치를 가리켜 전부 어긋남」) **메서드·필드 이름을 함께** 적는다.
- 근거 표기: **[직접]** = 이 문서 작성자가 파일을 읽어 확인 / **[전달]** = 메인 세션이 확인해 전달한 값(재검증하지 않았거나 일부만 재검증).

---

## 2. 항목 A — BloomFairy 힐 VFX 파티클 개수 계약

### 2-1. 프리팹 현재 상태

`Assets/_Project/Prefabs/VFX/Units/vfx_bloomfairy_heal.prefab`
- **[직접]** GameObject 4개(`vfx_bloomfairy_heal` 루트, `Soft_Aura`, `Rising_Petals`, `Sparkle_Motes`), ParticleSystem 블록 4개. `Bloom_Ring`은 없다(사용자 결정으로 삭제 — [전달]).
- **[직접]** 4개 모두 `looping: 1`, `stopAction: 0`(None), `useUnscaledTime: 0`, `moveWithTransform: 0`(Local 시뮬레이션), `m_BurstCount: 0`, `scalingMode: 0`(Hierarchy).
- **[직접]** prefab GUID `ff068a5e9b80f3043a0517ebb1cfb00f` — 기존 검증기가 요구하는 GUID와 같다(변경 없음). preset `Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset`의 offset 0 / scale 1.

### 2-2. 재생을 거부하는 코드

`Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`
- **[직접]** `PlayBloomLoop` (126행~): 134행 `if (_bloomSystems.Length != 5) return 0;` — 하위 전체 ParticleSystem이 **정확히 5개가 아니면 0(실패)을 반환**한다. 4개짜리 새 프리팹은 여기서 항상 거부된다.
- **[직접]** 135~137행: 시스템별 계약(loop ON, unscaled 아님, stopAction None, Local, burst 0)을 검사한다. **이 시스템별 검사는 새 프리팹도 통과한다**(2-1).
- **[직접]** 거부 시 흐름: `EffectManager.StartBloomEffect`(`EffectManager.cs:272~293`)가 `PlayBloomLoop == 0`이면 `RejectBloomAcquire()` 후 `null` 반환 → `BloomHealEffectPresenter.LateUpdate`(`BloomHealEffectPresenter.cs:107~113`)가 `Fault / Reason=RecursivePSContract`를 기록하고 owner를 제거한다. 즉 **힐(HP 회복)은 정상이지만 힐 이펙트는 한 번도 재생되지 않는다.**
- **[직접]** 로그 문자열에 개수 5가 하드코딩: `ObserveBloom("Play", "RecursivePS=5")`(154행), `"RecursivePS=5, Reason="`(180행), `"Drain", "RecursivePS=5"`(432행), `"Return", "RecursivePS=5"`(531행). 주석에도 `5PS`(125행 `/// 새 lease에서만 5PS를 시작한다`, 151행 `authored 5PS burst=0 계약`).
- **[직접]** 대조: 같은 클래스의 화상/독 경로 `VfxPoolItem.TimedDamage.cs` `PlayTimedDamage` 23~26행은 **"1개 이상"**(`Length == 0`이면 예외) + 시스템별 loop/stopAction 검사만 하고, 37행에서 로그에 **실제 개수**(`"RecursivePS=" + _damageSystems.Length`)를 적는다. 사용자 결정(개수 무관, 1개 이상)은 이 형태에 맞추는 것이다. 단 Bloom은 시스템별 검사가 TimedDamage보다 엄격(unscaled·Local·burst 추가)하며, 이 엄격한 검사는 유지 대상이다.
- **[직접]** `BloomRecursiveSystemCount`(101행)는 `_bloomSystems.Length`를 그대로 노출하는 읽기 전용 값 — 개수 가정 없음.
- **[직접]** 로그 문자열 `RecursivePS`를 파싱하는 분석 도구(`.py/.cs/.ps1/.sh`)는 저장소에 없다(검색 결과 위 3개 소스 파일뿐). 문자열 변경이 분석기를 깨뜨리지 않는다.

### 2-3. 개수 5를 가정한 에디터 검증

`Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` `ValidateBloomHoTEffectBoundary()` (1375행~)
- **[직접]** 1385행 `Require(authored.Length == 5 && preset.VfxPrefab.GetComponent<ParticleSystem>() != null, "Recursive5/root1 are separate metrics.")` — 원본 프리팹이 5개가 아니면 **검증 메뉴 자체가 실패**한다(새 프리팹 기준 현재 RED 예상 — 실행은 하지 않았다).
- **[직접]** 1386~1389행: 시스템별 계약 검사(메시지 `"Authored5PS loop/local/scaled/None/burst0 contract must remain unchanged."`) — 로직은 개수 무관, 메시지에만 5.
- **[직접]** 1448행 `item.BloomRecursiveSystemCount == 5 && item.ParticleSystemCount == 1` (`"All5PS lease starts."`) — 개수 5 가정. `ParticleSystemCount == 1`은 루트에 PS가 있다는 뜻이고 새 프리팹도 루트에 PS가 있다(GameObject 4 = PS 4).
- **[직접]** 1474·1477·1479행 메시지 `All5PS ...`, 1491행 `"Malformed5PS contract fails before Play."` — 로직은 개수 무관(시스템 배열 순회), 메시지만 5.
- **[직접]** 다른 검증기: `RunUnitActionSelfValidation.VfxConnections.cs`의 `ValidateDamageParticles`(161~195행)는 화상/독 전용이며 `systems.Length > 0`만 요구 — Bloom 개수 가정 없음. `RunAuthoritativeAttackPresentationValidation.cs:530~535`의 `== 5`는 피격 방출 횟수 집계이지 파티클 개수가 아니다(무관).

### 2-4. 규칙·문서상의 언급

- **[직접]** `GameSystemRules_Units.md` 규칙 34의 2026-10-09 구현 상태 블록이 "실제 대상의 all5PS loop를 소유"라고 적고 있다. 이는 규칙 본문(정책)이 아니라 **구현 상태 기록**이다. 파티클 개수를 정하는 규칙 조항은 없다.
- 구현·사용자 테스트 후 [11] 문서 반영 때 이 상태 기록의 "5PS" 표기를 고칠 필요가 있다(이번 위임 범위 밖 — Plan의 사후 문서 항목 참조). `VFXSFXList.md` 변경은 사용자가 별도로 처리한다(위임 지시).

---

## 3. 항목 B — RuneSpire(정령 타워) 번개 끝점이 움직이는 적을 따라가지 않음

### 3-1. 현재 데이터 흐름 (서버 → 각 화면)

1. **[직접]** 서버(또는 싱글): `TowerCombatUseCase.ExecuteTowerAttack`(`Application/UseCases/TowerCombatUseCase.cs:216~`)이 피해를 즉시 적용하고(241행 `target.TakeDamage`), 표현 스냅샷 `AttackEffectPresentation`을 만든다(245행 `_effectIdentity.Create(tower, target, AttackEffectVisual.Tower, race, effectSource, effectImpact, effectTime)`). `effectSource`=타워 월드 위치, `effectImpact`=**공격 순간의** 대상 유닛 월드 위치(223~226행). 248행에서 `GameEvents.OnAttackEffectConfirmed` 발행.
2. **[직접]** 스냅샷 구조(`Application/Combat/AttackEffectPresentation.cs:9~27`): `AttackerId`, `TargetId`, `Visual`, `Race`, `Source`, `Impact`, `ImpactTime` 등. **대상 객체 참조는 없고 TargetId(정수)만 있다.**
3. **[직접]** 멀티: `NetworkCombatController.PublishAttackEffect`(60~68행)가 ClientRpc로 같은 값(TargetId 포함)을 보내고, Client는 `AttackEffectClientRpc`(69~86행)에서 `GameEvents.OnAttackEffectReceived` 발행. Host/싱글은 `HitPresentationQueue`가 `OnAttackEffectConfirmed`를 직접 받는다(`HitPresentationQueue.cs:291~293`).
4. **[직접]** `HitPresentationQueue.ReceiveAttackEffect`(197~221행) → `EmitAttackEffects`(222~248행): Tower는 지연 없이 다음 갱신에서 방출. Client + 화면 반전(`ViewConverter.IsFlipped`)이면 Source/Impact를 뷰 좌표로 변환(234~236행) 후 `EffectManager.PlayAttackEffect(s, source, impact, out outcome)` 호출(240행).
5. **[직접]** `EffectManager.PlayAttackEffect`(`EffectManager.cs:405~425`): 413~414행에서 **Source→Impact의 XZ 방향으로 회전만** 계산, 418행에서 모든 하위 PS의 stopAction을 None으로 강제, 419~420행에서 `VfxPoolItem.Play(source, rotation, preset offset/euler/scale, travel)` 호출. **대상(TargetId)으로 Transform을 찾는 코드도, 거리로 길이를 맞추는 코드도 없다.**
6. **[직접]** `VfxPoolItem.Play`(281~324행): 위치·회전 1회 설정, `transform.localScale = _originalLocalScale × scaleMultiplier`(295행). 이후 `Update`(418~447행)는 forward travel(이번 preset은 0)과 자연 반환만 처리한다.
7. **[직접]** `Bolt`·`Impact_Sparks`라는 이름을 코드에서 찾으면 `Scripts/` 전체에서 0건(`RuneSpire`는 검증기 `VfxConnections.cs:48`의 경로 문자열 1건뿐). 즉 **번개 길이·끝점 위치를 바꾸는 런타임 코드가 존재하지 않는다.**

> 따라서 10-10 Plan(`_Tasks/2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md` 「21_43 최종 로그·소스 인계」 Tower 항목)의 "현행 source는 방향만 사용하며 actual target transform 추적/거리 refit 없음"은 현재도 그대로다. 사용자가 "생성 순간 길이는 맞다"고 본 것은 **코드로 설명되지 않는다** — 아래 3-2의 프리팹 고정 길이(3.5)가 그때의 실제 거리와 비슷했을 가능성이 있으나 이는 확인하지 않은 추정이므로 원인으로 단정하지 않는다.

### 3-2. 프리팹 현재 상태 — ⚠️ 전달된 설명과 디스크 내용이 다르다

`Assets/_Project/Prefabs/VFX/Buildings/vfx_runespire_attack.prefab` (GUID `a1406d4f5b9f0544cab305479195055f`) **[직접]**

| 오브젝트 | 부모 | localPosition | localScale | ParticleSystem 요지 |
|---|---|---|---|---|
| `vfx_runespire_attack`(루트) | — | (0,0,0) | (1,1,1) | PS 있음, **Emission 비활성**(빈 컨테이너 역할), duration 0.35, authored stopAction 2(Destroy — 런타임에서 None으로 덮어씀, `EffectManager.cs:418`), loop OFF |
| `Source_Flash` | 루트 | (0,0,0) | (1,1,1) | Local, scaling Hierarchy |
| `Bolt` | 루트 | (0,0,0) | **(1,1,3.5)** | Mesh 렌더(`m_RenderMode: 4`), mesh `mesh_vfx_beam_cross`(AABB 중심 z 0.5, 반경 z 0.5 → **로컬 Z 0→1**), Local, scaling Hierarchy, burst 3회, 수명 0.07 |
| `Impact_Sparks` | **Bolt** | **(0,0,1)** | (1,1,1) | Local, scaling **Shape**, burst 6, 수명 0.25 |

- 위임 지시의 설명("`Impact_Sparks`는 Bolt 아래가 아니라 루트 아래, 로컬 Z=1")과 **다르다.** 디스크에서는 `Impact_Sparks`가 **`Bolt`의 자식**이고, Bolt의 Z 스케일이 1이 아니라 **3.5**로 저장돼 있다. 결과적으로 Impact_Sparks의 월드 위치는 루트 기준 Z=3.5다.
- 의미: 이 계층이면 `Bolt.localScale.z = 거리`만 설정해도 `Impact_Sparks`가 자동으로 끝점에 놓인다(자식이므로). 반대로 Impact_Sparks의 localPosition.z를 거리로 따로 설정하면 **이중 적용**(거리 × 거리)이 된다. 어느 상태가 사용자 의도인지는 **메인 세션·사용자 확인이 필요하다**(Plan 결정 항목 D-4).
- preset `Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset` **[직접]**: offset 0, euler 0, scale (1,1,1), travel 0/0.
- 효과 수명: 루트 duration 0.35초, 하위 burst·수명 짧음. 풀 반환은 `AnyAlive()`가 거짓이 될 때(`VfxPoolItem.cs:443`)이므로 **실제 표시 수명은 "모든 파티클 소멸까지"**다.

### 3-3. 대상 Transform을 찾는 데 쓸 수 있는 기존 수단

- **[직접]** `IPresentationPoseProvider.GetUnitTransform(int unitId)` (`Presentation/Unit/PresentationPoseProvider.cs:31~43`): `UnitFactory.GetUnitObject(id)` → `VisualRootProjector.PresentationTransform`(없으면 유닛 루트 transform). `HitPresentationQueue`는 이 provider를 이미 보유한다(`_presentationPoseProvider`, 299행).
- **[직접]** 동일 객체 확인 선례: `GameBootstrapper.Map.cs:271~287` — 화상/독/힐 presenter용 pose 해석에서 `ReferenceEquals(view.Data, target) && go.activeInHierarchy`일 때만 transform을 돌려준다(ID 재사용 방어).
- **[직접]** 미사용 레거시: `HitPresentationQueue.PlayTowerAttackVfx`(1138~1174행)는 대상 transform으로 방향만 계산하는 옛 경로이며 **호출하는 곳이 없다**(검색 결과 정의 1건). 이번 작업에서 건드리지 않는다.
- **미확인:** Client에서 스냅샷의 `TargetId`(서버 ID)가 Client `UnitFactory`의 같은 유닛 키와 일치하는지. 기존 피격 표현이 ID로 View를 찾는 구조라 일치할 가능성이 높지만, 이 조사에서 Client ID 매핑 코드를 끝까지 추적하지 않았다 → Plan 위험 항목, 구현 담당이 확인.

### 3-4. 이 작업과 맞닿는 규칙 문구

- **[직접]** `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 상태 블록: "확정 공격 identity·Impact 위치/시각·Race snapshot의 표현 전달을 보강하며 **현재 target 재조회**·tracer 도착 피해 지연은 금지한다." — "타워의 *현재* 타겟을 다시 조회"하는 것과 "스냅샷의 *원래* TargetId 객체를 시각적으로 따라가는 것"이 같은지 규칙이 명시하지 않는다. Plan에서 해석 확인 항목으로 둔다(D-5).
- **[직접]** `GameSystemRules_UnitCombatSynchronization.md` NET-PRESENT-003: 필수 표현은 현재 View 조회를 필수 조건으로 삼지 않고, 살아 있는 View에 붙는 반응은 **동일 객체 수명이 확인될 때만** 추가한다. → 끝점 추적은 "보조"이며, 대상 View가 없거나 동일성을 확인할 수 없으면 스냅샷 Impact로 그대로 재생해야 한다는 근거.
- **[직접]** NET-AUTH-001: Client의 VFX 위치는 서버 결과의 입력이 될 수 없다 → 추적은 표현 전용이어야 한다.

---

## 4. 항목 C — 상태 이펙트(화상·독·힐) 크기·위치가 대상 유닛에 맞지 않음

### 4-1. 현재 코드

- **[직접]** 화상/독: `VfxPoolItem.TimedDamage.cs` `PlayTimedDamage` 29~30행 — offset/euler는 preset 값, **scale = 원본 × preset 배율(고정)**. `FollowTimedDamage`(39~40행)는 매 프레임 **위치·회전만** 갱신하고 스케일은 건드리지 않는다.
- **[직접]** 힐: `VfxPoolItem.cs` `PlayBloomLoop` 141~142행에서 preset offset/euler/scale 저장, `FollowBloomPose`(165~173행)는 위치·회전 + **scale = 원본 × preset 배율 × `pose.lossyScale`**을 부모 스케일로 나눠 적용한다. 즉 힐만 대상 Transform의 스케일을 반영하지만, 이는 "모델 크기"가 아니라 Transform 스케일이다.
- **[직접]** pose Transform은 `PresentationPoseProvider.GetUnitTransform` = VisualRoot(`GameBootstrapper.Map.cs:271~287` 경유). 유닛 셋업 검증 `ValidateUnitCombatSetup.cs:795~796`이 "재사용 VisualRoot localScale must be one"을 요구하므로, 대상 간 크기 차이는 VisualRoot 스케일이 아니라 **하위 모델(메시) 자체 크기**에서 온다고 보는 것이 맞다(유닛 루트 스케일 값은 전 유닛을 측정하지 않았다 — 미확인).
- **[직접]** preset 3종(`EffectPreset_InfernoSpirit_Burn`, `EffectPreset_MushroomBomber_Poison`, `EffectPreset_BloomFairy_Heal`) 모두 offset (0,0,0), scale (1,1,1).
- **[직접]** Presentation 코드에 유닛별 크기(반경·키) 데이터나 렌더러 bounds를 읽는 코드가 없다(`.bounds`/`localBounds` 검색: 스프라이트 레티클·카메라·UnitView의 진단 주석뿐). → 크기 맞춤에 쓸 기존 데이터가 **없다.**
- **[직접]** presenter는 각 피어에서 로컬로 돈다: `TimedDamageEffectPresenter.LateUpdate`(46~71행), `BloomHealEffectPresenter.LateUpdate`(79~118행)가 매 프레임 Follow를 호출. 크기 계산을 넣을 자리가 이 두 경로다.

### 4-2. 새 프리팹의 제작 기준 ([전달] + 일부 [직접])

- [전달] 기준: 루트 pivot = 유닛 발밑, 반경 0.25 원기둥 안, 높이 약 1.0(힐·독). 화상(`vfx_infernospirit_burn.prefab`)은 사용자가 **현 상태 그대로 승인** — 대상별 크기·위치만 맞추면 된다.
- [직접] 파티클 설정: 세 프리팹 모두 전 시스템 loop ON, stopAction None, Local, burst 0, **scaling Hierarchy**. Hierarchy 스케일링이므로 루트 Transform 스케일을 바꾸면 **파티클 크기·방출 형상이 함께 커진다.** 축별로 다른 스케일(비균등)을 주면 파티클이 납작/길쭉하게 왜곡될 수 있다(Plan 결정 D-3).
- [직접] 프리팹 구성: 힐 PS 4개, 독 PS 4개(`Poison_Bubbles`·`Poison_Aura`·`Toxic_Drips`·루트), 화상 PS 2개(`Flame_Haze`·루트).

### 4-3. 멀티플레이 관점

- 상태 이펙트는 서버 상태(`TimedDamageEffectChannel`/`BloomHealEffectChannel`)를 받아 **각 화면이 로컬로** 재생한다. 크기 계산도 각 화면에서 따로 일어난다. 같은 유닛이 Host·Client에서 다르게 보이지 않으려면 계산 입력이 양쪽에서 같아야 한다 — 애니메이션 진행이 화면마다 조금씩 다르므로, 매 프레임 렌더러 bounds를 읽는 방식은 양쪽 크기가 미세하게 다를 수 있다(Plan 선택지 비교에 반영).

### 4-4. 규칙 근거

- **[직접]** 크기·위치 맞춤을 직접 정한 규칙 조항은 **없다.** 관련 규칙은 이펙트가 "실제 효과 수명 동안 대상에 붙어 재생"한다는 상태 블록(Units 규칙 40·42의 2026-10-10 블록, 규칙 34 2026-10-09 블록)과, 표현이 HP·틱·수치를 바꾸지 않는다는 계약(규칙 34·40·41·42 본문)이다.

---

## 5. 영향 범위 요약

| 항목 | 바뀌는 곳(예상) | 바뀌지 않는 곳 |
|---|---|---|
| A | `VfxPoolItem.cs`의 Bloom 계약 검사·로그 문자열, `RunUnitActionSelfValidation.cs` Bloom 검증 | HoT 회복량·틱·텍스트·서버 상태 복제, 프리팹/preset |
| B | `EffectManager.PlayAttackEffect`(Spirit 분기), `VfxPoolItem`(신규 추적 상태), `HitPresentationQueue`(대상 Transform 해석 전달), 검증기 | 타워 피해·쿨다운·타겟 선택·스냅샷 발행/전송, Human 타워(Assault VFX) 경로, 프리팹 |
| C | `VfxPoolItem`(TimedDamage·Bloom 스케일/위치), presenter 또는 EffectManager의 대상 UnitType 전달, 크기 표 자산(선택지에 따라) | DoT/HoT 수치·수명·갱신 정책, 프리팹 자체 |

---

## 6. 조사 중 발견한 부가 사항 (이번 범위 밖, 기록만)

1. `HitPresentationQueue.PlayTowerAttackVfx`(1138~1174행)는 호출자가 없는 레거시 메서드다. 이번 작업에서 수정·삭제하지 않는다.
2. RuneSpire 루트 PS의 authored stopAction이 Destroy(2)지만 `EffectManager.PlayAttackEffect` 418행이 재생 직전 None으로 덮어쓴다. 풀 인스턴스 값만 바뀌고 원본 프리팹은 그대로다.
3. 화상/독 계약(`PlayTimedDamage`)은 Bloom보다 느슨하다(unscaled·Local·burst 미검사). 이번 A의 요구는 Bloom을 "개수만" TimedDamage처럼 바꾸는 것이므로 TimedDamage 쪽 강화는 범위 밖이다.
4. 규칙 34 구현 상태 블록·`VFXSFXList.md` 등 문서의 "5PS" 표기는 구현 후 정리 대상이다(VFXSFXList는 사용자 처리).

## 7. 미확인 — 구현 전/중 확인 필요

| # | 내용 | 확인 주체 |
|---|---|---|
| U-1 | 작업 트리 = `88f60e38` 일치 여부 | 메인 세션(git 읽기 권한) |
| U-2 | RuneSpire 프리팹 계층: 디스크(Impact_Sparks ⊂ Bolt, Bolt z=3.5)와 전달 설명(형제, z 0→1) 중 어느 쪽이 의도인가 | 메인 세션 → 사용자 |
| U-3 | Client에서 스냅샷 TargetId로 같은 유닛 View를 찾을 수 있는가 | game-programmer |
| U-4 | Bolt 메시 파티클이 `Bolt.localScale.z = d`에서 실제로 0→d 길이로 보이는가(파티클 startSize·3D 크기 설정 영향) | game-programmer + 사용자 육안 |
| U-5 | 유닛별 VisualRoot 위치가 발밑인지(`HexMetrics.UnitYOffset` 적용 여부), 유닛 루트 스케일 값 | game-programmer |
| U-6 | 25종 유닛의 실제 렌더 크기(반경·키) | game-programmer(에디터 측정) |

---

## 8. 1차 사용자 테스트 결과 (2026-10-13 추가 — 위 1~7절은 구현 전 조사 원문으로 보존)

### 8-0. 쉬운 설명

A·B·C 구현을 사용자가 처음으로 직접 플레이해 보았다. 버섯 착탄은 그대로 승인됐지만, 화상·힐 이펙트의 위치·크기, 독 이펙트 모양, 정령 타워 번개의 끝점이 사용자가 원한 모습과 달랐고, **유닛이 기를 모으다(차지) 죽으면 차지 이펙트가 계속 남는 버그**가 새로 발견됐다. 이 절은 무엇이 보였고 왜 그런지를 기록한다. 고치는 계획은 `Plan.md` 「재작업 R2」.

### 8-1. 테스트 대상과 근거 수준

- 대상: 브랜치 `claude/vfx-code-followups`, `88f60e38` **이후** 구현 커밋 [전달]. 테스트 환경·매치 구성 세부는 전달받지 않았다.
- 근거 표기는 1절과 같다. 이 절의 원인 분석은 메인 세션이 확인해 전달한 것이며 [전달], document-manager가 2026-10-13 직접 다시 읽은 부분만 [직접]으로 표시한다. 행 번호는 이 시점 작업 트리 기준이며 메서드 이름을 함께 적는다.

### 8-2. 항목별 결과

| # | 대상 | 사용자 관찰 | 판정 | 원인 |
|---|---|---|---|---|
| T-1 | 화상 (InfernoSpirit) | 유닛에서 더 **아래**에 붙고, 조금 **더 작아야** 한다 | 조정 필요 | [전달] `vfx_infernospirit_burn.prefab`의 `Flame_Haze`가 로컬 y 0.45, 크기 0.55이며, 이 값 전체에 C의 대상 맞춤 배율(recipient fit scale)이 곱해진다. [직접] 프리팹에 `Flame_Haze` 오브젝트 존재 확인. |
| T-2 | 힐 (BloomFairy) | 크기는 괜찮지만 **너무 높이 올라간다** | 조정 필요 | [전달] `Rising_Petals`·`Sparkle_Motes`가 수명 동안 속도(velocity over lifetime)로 1.2~1.5초 상승하고, 그 높이도 맞춤 배율로 함께 커진다. |
| T-3 | 버섯 착탄 (MushroomBomber impact) | 문제 없음 | **사용자 승인** | — |
| T-4 | 독 (MushroomBomber poison) | 여전히 주변 6칸으로 **퍼져 보인다** | 재설계 필요 | [전달, 메인 세션 확인] `Poison_Bubbles`가 Circle 형태에서 Start Speed 0.55~0.75로 바깥 방향으로 방출되고 수명 약 1.2초 → 약 0.9 월드 단위 바깥까지 나간다(인접 칸 거리 약 0.87). **사용자 의도:** 독이 퍼지는 연출이 아니라 "**이 유닛이 중독됐다**"를 보여 주는 표시로, 유닛 중심에 모여 있어야 한다. 해결은 프리팹 변경이며 사용자가 Unity AI 프롬프트로 직접 한다. |
| T-5 | **버그** — 차지 VFX | 유닛이 차지 중에 죽어도 차지 이펙트가 끝까지 재생된다 | **결함** | [전달] FoxMagician `vfx_foxmagician_charge`(2초), InfernoSpirit `vfx_infernospirit_charge`(1초)가 각 유닛의 공격 preset(`EffectPreset_FoxMagician_Attack` / `EffectPreset_InfernoSpirit_Attack`)을 통해 재생되는데, 유닛 사망 시 이를 멈추는 코드가 없다. [직접] 두 preset 파일 존재 확인. 이번 A·B·C 변경이 만든 결함인지 기존 결함인지는 전달받지 않았다(미확인). |
| T-6 | 정령 타워 (RuneSpire) 번개 | 끝점이 유닛에 닿지 않는다. 유닛 **가슴 높이**를 맞혀야 하고, 시작점은 **타워 꼭대기 크리스털**이어야 한다 | 재작업 필요 | 아래 8-3 |

### 8-3. T-6 정령 타워 번개 — 오프셋만으로는 고칠 수 없는 이유

- [직접] `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs`
  - `ApplyTowerBoltPose` 약 176행 `Vector3 direction = _boltEndpoint - _boltSource; direction.y = 0f;` — 조준 방향의 **높이 성분을 0**으로 만든다(수평 조준만).
  - 같은 메서드 약 182행 `Vector3 span = _boltEndpoint - transform.position; span.y = 0f;` — 번개 길이도 **수평 거리만** 쓴다.
  - `TryReadEndpoint` 약 70행 `endpoint = Projector.PresentationPosition;` — 끝점이 대상 유닛의 **루트 위치(발밑)** 다.
- [전달] preset의 `Local Position Offset`은 **시작점만** 옮긴다. 시작점을 크리스털 높이로 올리면 번개는 그 높이에서 **수평으로** 뻗으므로 유닛 위를 지나가 버린다. 즉 오프셋 조정만으로는 "크리스털에서 가슴으로" 내려꽂히는 모양을 만들 수 없고, **코드에서 조준·길이를 3D로 계산하고 끝점을 가슴 높이로 올려야** 한다.
- 이 수평 처리는 원래 4-B 설계("높이 차는 기존처럼 무시")를 그대로 구현한 결과다 — 구현 오류가 아니라 설계가 사용자 의도와 달랐던 것이다.

### 8-4. 다음 단계

- 수정 계획: `Plan.md` 「재작업 R2 — 1차 테스트 반영」(코드 R2-1·R2-2, 사용자 Inspector 조정 R2-3, 사용자 프리팹 재제작 R2-4).

---

## 9. 2차 사용자 테스트 결과 (2026-10-13 추가 — 1~8절은 당시 기록으로 보존)

### 9-0. 쉬운 설명

R2 수정(커밋 `a840a152`)과 사용자가 다시 만든 독 프리팹(커밋 `10a38cc0`)으로 두 번째 실기를 했다. **차지 이펙트가 시전자 사망 시 사라지는 것**과 **정령 타워 번개가 크리스털에서 시작하는 것**은 사용자가 확인했다. 그런데 독·힐 이펙트가 유닛에 비해 **너무 높고 크게** 보였다. 메인 세션이 원인을 찾았다 — 유닛 크기 표를 채우는 **측정 도구가 사람형(스킨드 메시) 유닛의 키를 약 5배 크게 재서** 표에 넣었다. 이 표가 상태 이펙트 크기와 정령 타워 번개의 "가슴 높이"를 함께 정하므로, 표가 고쳐지기 전에는 이 둘을 제대로 판정할 수 없다. 고치는 계획은 `Plan.md` 「재작업 R3 — 측정 도구 스케일 버그」.

### 9-1. 근거 수준

- 근거 표기는 1·8절과 같다. 사용자 관찰과 원인 진단은 메인 세션 전달값 **[전달]**, document-manager가 2026-10-13 작업 트리(`a840a152`, 측정 도구는 programmer가 동시에 수정 중이라 읽지 않음)에서 직접 다시 확인한 값은 **[직접]**.
- 이번 절에서 쓴 git 명령은 읽기 전용(`git show --stat`)뿐이다.

### 9-2. 항목별 결과

| # | 대상 | 사용자 관찰 [전달] | 판정 |
|---|---|---|---|
| R2-1 | 차지 VFX 사망 정지 (FoxMagician·InfernoSpirit) | 기를 모으는 도중 시전자가 죽으면 차지 이펙트가 사라진다 | **PASS (사용자 확인)** |
| R2-2 ① | 정령 타워(RuneSpire) 번개 시작점 | 번개가 타워 꼭대기 크리스털에서 시작한다 | **PASS (사용자 확인)** |
| R2-2 ③ | 번개 끝점의 가슴 높이 조준 | — | **미판정** — 가슴 높이가 아래 9-3의 부풀려진 표 값에서 나오므로 표 수정 전에는 판정 불가 |
| R2-4 | 독 (MushroomBomber, 사용자 재제작 프리팹 `10a38cc0`) | 프리팹 자체 모양은 맞다. 하지만 유닛에 붙으면 **너무 높고 크게** 보인다(힐과 같은 증상) | 모양 OK · **크기 미판정**(9-3 원인) |
| R2-3 | 화상·힐 Inspector 조정 | — | **미진행** — 표 수정 전에 조정하면 잘못된 배율 위에 값을 맞추게 된다 |

- [직접] 독 프리팹 `Assets/_Project/Prefabs/VFX/Units/vfx_mushroombomber_poison.prefab`: GameObject 4개(`vfx_mushroombomber_poison` 루트·`Poison_Bubbles`·`Poison_Aura`·`Toxic_Drips`), ParticleSystem 4개.

### 9-3. 원인 — 측정 도구가 스킨드 메시 유닛의 크기를 약 5배 크게 잰다

- [전달 + 표 값 직접] 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`(도구 `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`)이 채운 `Assets/_Project/Resources/Config/UnitEffectConfig.asset`의 `_recipientFits` 표(커밋 `10a38cc0`에 포함)를 직접 읽은 값:

| 유닛 (UnitType 값) | 렌더러 경로 [전달] | height | radius |
|---|---|---|---|
| Pistoleer (0) | SkinnedMeshRenderer | 1.7118616 | 0.47790378 |
| MushroomBomber (26) | SkinnedMeshRenderer | 1.9401472 | 0.78346676 |
| Tank (6) | MeshRenderer | 0.5212989 | 0.41331026 |
| CannonCart (7) | MeshRenderer | 0.5301163 | 0.40547904 |

  - ⚠️ 전달값은 「Tank 0.53 · CannonCart 0.52」였으나 표의 실제 값은 **Tank 0.521 · CannonCart 0.530** 으로 두 이름이 서로 바뀌어 있다(UnitType 6=Tank, 7=CannonCart — `Scripts/Domain/Unit/UnitType.cs`). 두 값 모두 약 0.52~0.53 이라는 결론은 그대로다.
- [직접] `Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab`: `VisualRoot`(scale 1) 아래 `Unit_Pistoleer_Blue_Mesh`의 localScale = **(0.2, 0.2, 0.2)**, 그 아래 `char1`에 SkinnedMeshRenderer(scale 1). → 실제 화면 키 ≈ 1.71 × 0.2 ≈ **0.34**. 표의 1.71은 중간 Transform의 0.2 배율이 빠진 값으로 보인다(측정 코드 자체는 읽지 않았다 — 진단은 [전달]).
- 영향 [직접 — 코드 확인]:
  - 상태 이펙트(화상·독·힐) 크기 = 원본 × preset 배율 × **맞춤 배율(= 키 ÷ 기준 키 1.0)** (Plan 4-C-2 3번). Pistoleer라면 약 0.34배여야 할 것이 **1.71배**가 된다.
  - 정령 타워 번개 끝점 = 발밑 + **키 × 0.6**(`UnitEffectConfig.GetChestHeight` = 균등 배율 × 기준 키 1.0 × `ChestHeightRatio` 기본 0.6, 표에 없으면 0.6m). Pistoleer라면 약 0.21m여야 할 것이 **약 1.03m**가 된다.
  - MeshRenderer 경로(Tank·CannonCart)는 값이 작아 이 부풀림의 영향이 없는 것으로 보인다(전달 진단과 표 값이 일치).
- 따라서 사용자가 본 "독·힐이 너무 높고 크다"는 R2-4 프리팹이나 R2-3 값의 문제가 아니라 **표 값의 문제**로 설명된다. 1차 테스트(8-2 T-1·T-2)의 화상·힐 크기 관찰도 같은 표 위에서 나온 것이므로 R3 이후 다시 본다.

### 9-4. 아직 테스트하지 못한 것 (R3 수정·표 재측정 뒤로 미룸)

1. 정령 타워 번개의 가슴 높이 조준(R2-2 ②·③).
2. 화상·힐 위치·크기 조정(R2-3).
3. 독이 유닛 위에서 알맞은 크기로 보이는지(R2-4의 크기 부분).

### 9-5. 다음 단계

- `Plan.md` 「재작업 R3 — 측정 도구 스케일 버그」. 사용자 승인(2026-10-13): "측정 도구를 수정해야 나머지가 정상적으로 테스트될거같아 / 문서 정리도 같이 진행해".

---

## 10. 3차(이번 회차 마지막) 사용자 테스트 결과 (2026-10-15 추가 — 1~9절은 당시 기록으로 보존)

### 10-0. 쉬운 설명

측정 도구를 고친 뒤(커밋 `84b6ff2a`) 사용자가 유닛 크기 표를 다시 재고 세 번째 실기를 했다. **꽃요정 힐 이펙트가 다시 나오고**, **차지 이펙트는 시전자가 죽으면 사라지며**, **정령 타워 번개는 크리스털에서 나와 유닛 가슴 높이로 꽂힌다.** 상태 이펙트(화상·독·힐)의 크기·높이는 이제 프리셋 값(`Local Position Offset` · `Scale Multiplier`)으로 **의도대로 조정된다** — 사용자는 반복 테스트로 값을 계속 다듬을 예정이며, 이것은 코드 작업이 아니라 데이터 조정이다. 버섯 착탄과 새 독 프리팹은 승인됐다.
남은 결함은 하나다 — **타워 번개가 맞는 순간 대상이 죽으면 번개 끝이 살짝 어긋난다.** 원인은 아직 확인하지 않았고, 아래 10-4에 코드로 읽은 사실과 **검증되지 않은 가설**을 구분해 적었다. 사용자 지시(2026-10-15): "우선 여기까지 작업한 것을 기록하자".

### 10-1. 근거 수준

- 사용자 관찰은 메인 세션 전달값 **[전달]**. document-manager가 2026-10-15 작업 트리(`84b6ff2a`, 커밋되지 않은 변경 0건)에서 직접 다시 읽은 값은 **[직접]**.
- 이번 절에서 쓴 git 명령은 읽기 전용(`rev-parse` · `status` · `log` · `diff` · `show`)뿐이다.
- 이 작업에는 **`Testcase.md`가 없다** — TC 작성을 요청받지 않았으므로(WORKFLOW [5-1]) 만들지 않았고, 실기 결과는 이 절과 `Plan.md` 13절에 적는다.

### 10-2. 항목별 결과

| # | 대상 | 사용자 관찰 [전달] | 판정 |
|---|---|---|---|
| A | BloomFairy 힐 (PS 4개 프리팹) | 힐 이펙트가 다시 재생된다(개수 계약 「1개 이상」) | **PASS** |
| R2-1 | 차지 VFX 사망 정지 (FoxMagician·InfernoSpirit) | 시전자가 죽으면 차지 이펙트가 사라진다 | **PASS** (2차에 이어 재확인) |
| R2-2 ① | 정령 타워 번개 시작점 | 타워 꼭대기 크리스털에서 시작한다 | **PASS** |
| R2-2 ③ | 번개 끝점의 가슴 높이 조준 | 살아 있는 대상에게는 가슴 높이로 꽂힌다 [전달 — 메인 세션 정리] | **PASS — 단, 맞는 순간 대상이 죽는 경우 제외**(10-4) |
| R3 | 측정 도구 수정 + 표 재측정 | 재측정 뒤 화상·독·힐 크기·높이가 프리셋 `Local Position Offset` · `Scale Multiplier`로 의도대로 조정된다 | **PASS** — 값 미세 조정은 사용자가 계속(10-5) |
| R2-3 | 화상·힐 위치·크기 조정 | 위 R3 행과 같은 수단으로 진행 중 | **진행 중(데이터 조정)** — 코드 항목 아님 |
| R2-4 | 독 (MushroomBomber, 재제작 프리팹) | 프리팹 승인 | **사용자 승인** |
| T-3 | 버섯 착탄 (MushroomBomber impact) | 승인(1차에서 이미 승인) | **사용자 승인** |

### 10-3. 재측정 값과 프리셋 조정값 — 커밋 `91371e55`에 반영됨

*(2026-10-15 정정: 이 절의 첫 판은 「`84b6ff2a`에는 재측정 표·프리셋 조정값이 없다 — 사용자 PC의 커밋되지 않은 작업으로 보인다」였다. 그 시점 체크아웃 기준으로는 사실이었으나, 사용자가 같은 날 로컬 데이터를 브랜치에 올렸다(커밋 `91371e55`, `84b6ff2a`의 후손). 아래가 현재 사실이다.)*

- [직접 — `git show 91371e55`로 읽음, 메인 세션도 같은 값을 확인] 커밋 `91371e55`의 변경 파일: `UnitEffectConfig.asset` · `EffectPreset_BloomFairy_Heal.asset` · `EffectPreset_MushroomBomber_Poison.asset`(+ 로그 `.meta` 2개).
- **재측정 표** (`UnitEffectConfig.asset` `_recipientFits`, 25행): Pistoleer(0) height **0.342**(`0.34237233`, 9-3의 기대값 약 0.34와 일치 — 옛 값 `1.7118616`), 표 전체 최대 height **약 0.53**(BearGuard(20) `0.53033525` · CannonCart(7) `0.5301163`), 최대 radius **약 0.41**(Tank(6) `0.41331026`), `manualScale`은 25행 모두 **0**(수동 배율 없음). 측정 도구의 이상값 경고 기준(키 1.2m · 반경 0.43m · 키 0.1m) 안이다.
- **프리셋**: `EffectPreset_BloomFairy_Heal` Scale Multiplier **2** · `EffectPreset_MushroomBomber_Poison` Scale Multiplier **2**(둘 다 1 → 2) · `EffectPreset_InfernoSpirit_Burn` **변경 없음(1)**. 세 프리셋 모두 `Local Position Offset` (0,0,0). (두 프리셋 파일에는 직렬화 필드 `_stopWhenCasterDies: 0` 한 줄도 함께 기록됐다 — 값 변경이 아니라 R2-1에서 생긴 필드가 저장된 것이다.)
- 값 조정은 계속된다(10-5) — 위 숫자는 `91371e55` 시점의 값이다.
- ⚠️ `91371e55`는 이 문서를 고친 시점에 로컬 작업 트리에 아직 반영되지 않았다(파일은 수정하지 않았고 커밋 객체만 읽었다).

### 10-4. 미해결 결함 — 맞는 순간 대상이 죽으면 번개 끝점이 살짝 어긋난다 (OPEN)

**사용자 관찰 [전달]:** 타워 번개가 맞히는 순간 대상이 죽으면, 번개 끝이 유닛 위치에서 조금 벗어난다. 살아 있는 대상에게는 문제가 없다.

**코드로 읽은 사실 [직접]:**
1. `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack` — 표현용 Impact 위치를 먼저 읽고(223~226행, `_positionProvider.GetUnitWorldPosition(target.Id)`, 0이면 헥스 중심 `HexToWorld`), 그다음 피해를 적용하고(241행 `target.TakeDamage`), 그 뒤에 표현 스냅샷을 만들어 발행한다(245·248행). 즉 **한 방에 죽는 공격이면 스냅샷이 화면에 도착할 때 대상은 이미 죽은 상태**다(서버·Host·싱글 기준. Client의 HP 반영 순서는 추적하지 않았다).
2. `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` `TowerBoltTarget.TryReadEndpoint` 76행 — `Data.IsAlive == false`이면 `TargetDead`로 실패한다.
3. 같은 파일 `BeginTowerBolt` — 재생 시작 때 이 확인이 실패하면 추적을 켜지 않고 **끝점 = 스냅샷 Impact + 가슴 높이**로 고정한다(172행). 살아 있으면 끝점 = 화면에 그려진 유닛 위치(`VisualRootProjector.PresentationPosition`) + 가슴 높이다(165행).
4. 따라서 「맞는 순간 죽는」 경우만 **화면 위치가 아니라 서버가 공격 순간에 읽은 좌표**를 끝점으로 쓴다. 관찰된 증상이 이 경우에만 나타난다는 점과 맞아떨어진다.

**[🔴 2026-10-15 정정 — 원문 유지: 위 2번이 가리킨 `TryReadEndpoint` 76행은 실제로는 「추적 도중」 사망 확인이다]** 다시 읽은 결과 [직접]: 재생 시작 때 대상이 이미 죽었으면 그보다 앞의 `HitPresentationQueue.cs` `ResolveTowerBoltTarget` 278행(`!data.IsAlive` → `TargetDead`, 빈 대상 반환)에서 걸리고, `BeginTowerBolt`는 대상 참조 없이 172행 갈래로 간다. 🔴 또 이때 `EffectManager.cs` 550행 `ResolveBoltChestHeight(boltTarget.Data)`가 `Data == null`을 받아 560~563행에서 **크기 표가 아니라 `FallbackChestHeight` 0.6m**를 돌려준다(예: Pistoleer 표 가슴 높이 약 0.21m ↔ 0.6m). 스냅샷 Impact 원천은 `UnitWorldPositionProvider.GetUnitWorldPosition` = 유닛 GameObject 루트 위치다. 이 두 사실이 증상의 원인인지는 여전히 재지 않았다 — 가설·진단 순서·후보 수정은 `Plan.md` 13-4가 단일 소스다.

**가설 — ⚠️ 검증되지 않음:** 스냅샷 Impact(서버가 읽은 유닛 월드 위치)와 그 순간 화면에 그려진 유닛 위치가 다르거나, 스냅샷 Impact가 애초에 발밑이 아닌 지점이어서 어긋나 보인다. 어느 쪽인지, 또는 다른 원인인지는 로그·측정으로 확인하지 않았다. 원인으로 단정하지 않는다.

**다음 단계 제안 (승인 전 — 실행하지 않았다):**
1. **먼저 관측:** 재생 시작 시점에 「스냅샷 Impact」와 「그 순간 화면에 그려진 대상 위치」(대상이 죽었어도 View가 아직 살아 있으면 읽을 수 있다)를 함께 진단 기록에 남긴다. 기존 `AttackEffectIdentity.Record` outcome 문자열에 덧붙이는 방식이면 새 로그 채널이 필요 없다(4-B 5번과 같은 방식). 두 값의 차이가 증상 크기와 맞는지 보고 가설을 확정/기각한다.
2. **확정되면 고려할 수정:** 대상이 죽었더라도 재생 시작 때 화면 위치를 읽을 수 있으면 그 위치를 끝점으로 쓰고(추적은 켜지 않고 그 자리에서 고정), 읽을 수 없을 때만 스냅샷 Impact로 떨어진다.
3. ⚠️ **2번은 규칙 문구와 맞닿는다** — `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 2026-10-13 블록의 「추적 불가로 시작할 때: 재생 시작 때 원래 대상을 찾지 못하면 … 서버 스냅샷의 Impact 위치를 끝점으로 쓴다」. 2번을 채택하면 이 문구도 함께 바꿔야 하므로 **규칙 변경 전에 사용자에게 먼저 알리고 승인받는다**(영구 선호: 규칙 충돌은 변경 전에 알린다). 피해·타겟 판정·스냅샷 구조는 어느 쪽이든 바뀌지 않는다.

### 10-5. 남은 데이터 조정 (코드 항목 아님)

- 화상·독·힐의 크기·높이 미세 조정은 사용자가 프리셋(`Local Position Offset` · `Scale Multiplier`)과 필요하면 `UnitEffectConfig` 「상태 이펙트 크기 맞춤」의 유닛별 `manualScale` · `footOffset`으로 반복 테스트하며 계속한다. ⚠️ `manualScale`을 넣으면 그 유닛의 정령 타워 가슴 높이도 함께 바뀐다(Plan 12-2 R3-4 — 가슴 높이 = 균등 배율 × 기준 키 1.0 × 0.6, `UnitEffectConfig.GetChestHeight`).

### 10-6. 다음 단계

- 결과 정리와 남은 항목: `Plan.md` 13절. 위 10-4 결함은 별도 승인 후 착수한다(로드맵 등록).

---

## 11. 재작업 R4 — 검증 해시 줄바꿈 문제 (2026-10-19 추가 — 1~10절은 당시 기록으로 보존)

### 11-0. 쉬운 설명

main 브랜치를 이 브랜치에 합친 뒤, 사용자가 전체 자동 검증(`Hexiege/Combat/Run Full Regression Suite`, Edit Mode)을 돌렸다. 이 작업에서 고친 꽃요정 힐 검사와 SOLO 타이밍 검사는 이제 통과했지만, 그 뒤에 있는 **급류 정령 파도 검사가 「원본 VFX 파일이 바뀌었다」며 실패**했다. 실제로 파일 내용은 바뀌지 않았다. 이 검사는 파일을 디스크에서 바이트 그대로 읽어 지문(SHA256)을 비교하는데, 사용자 PC의 git 설정이 파일을 꺼낼 때 **줄바꿈을 Windows 방식(CRLF)으로 바꿔 두기** 때문에 지문이 달라진 것이다. 병합이나 이번 VFX 작업이 만든 문제가 아니라, 원래 main에 있던 검사가 그동안 앞 검사에 막혀 실행되지 못하다가 이번에 처음 실행되며 드러났다. 고치는 계획은 `Plan.md` 「재작업 R4 — 검증 해시 줄바꿈 문제」.

### 11-1. 근거 수준

- 아래 사실은 메인 세션이 확인해 전달한 값이다 **[전달]**. document-manager가 2026-10-19 작업 트리에서 다시 읽은 것은 **[직접]**으로 표시한다(읽기만 했다).

### 11-2. 무엇이 일어났나

- [전달] origin/main(`0978d908`)을 이 브랜치에 병합(병합 커밋 `6c007872`, 이어서 문서 정리 `d9d31f12`)한 뒤 사용자가 이 브랜치에서 위 메뉴를 Edit Mode로 실행했다.
- [전달] Bloom 힐 검사(`RunUnitActionSelfValidation.cs` Run 약 1008행)와 SOLO 타이밍 검사는 **통과**했다.
- [전달 · 호출 위치는 직접] 그다음 `RunUnitActionSelfValidation.cs` 1016행이 부르는 `TorrentWaveProductionValidation.Run()`에서 **"Torrent wave: source VFX hash changed"** 로 실패했다.

### 11-3. 원인

- [직접] `Assets/Editor/Tools/TorrentWaveProductionValidation.cs` 32~35행이 `vfx_torrentspirit_attack.prefab`과 `TorrentSpirit_Attack.anim`의 SHA256을 상수로 고정해 비교하고, 해시 함수(490~491행)는 `File.ReadAllBytes`로 **디스크의 원본 바이트를 그대로** 해시한다 — 줄바꿈 정규화가 없다.
- [전달] 저장소에 저장된 두 파일(blob)은 **LF** 줄바꿈이며, main · 이 브랜치 · 병합 기준점 모두에서 상수와 일치한다. 즉 파일 내용 자체는 바뀌지 않았다.
- [전달] 사용자 PC는 `core.autocrlf=true`(git status는 깨끗)라서 작업 트리 파일이 **CRLF**로 꺼내진다 → 디스크 바이트가 달라져 해시가 맞지 않는다.
- [전달] 이 검사는 main 커밋 `b65751c6`(2026-10-12)에서 들어왔다. main에서는 그보다 앞의 Bloom 검사(파티클 시스템 「정확히 5개」 가정)가 늘 먼저 실패했기 때문에 **이 검사까지 실행이 도달하지 못했다.** 이번 브랜치가 Bloom 검사를 고쳐 처음으로 도달하면서 드러났다. → **병합이나 VFX 작업이 만든 결함이 아니다.**

### 11-4. 병합 전 테스트에서 있었던 혼선 (결함 아님 — 해소됨)

- [전달] 첫 번째 Fox 차지 VFX 확인은 **main**에서 했다 — main에는 R2-1(시전자 사망 시 차지 정지)이 없으므로 이펙트가 남는 것이 당연하다. 이 브랜치에서 다시 확인하는 것으로 정리했다.
- [전달] 두 번째 검증 실행은 **Play Mode**에서 했다 — 이 검증 묶음은 Edit Mode 전용이다(위 `Run()` 30행 `validation is Edit Mode only`). Edit Mode로 다시 실행해 11-2의 결과를 얻었다.
- 두 건 모두 실행 조건의 차이였으며 코드 결함이 아니다.

### 11-5. 다음 단계

- `Plan.md` 「재작업 R4 — 검증 해시 줄바꿈 문제」. 사용자 승인(2026-10-19): "1번대로 진행하자"(이 브랜치에서 수정) · "가로 진행해"(기존 task 문서에 기록).

---

## 12. 범위 밖 별건 — 전체 검증이 Fox 표시 검사에서 멈춤 (2026-10-19 추가 — 1~11절은 당시 기록으로 보존)

### 12-0. 쉬운 설명

R4 뒤 전체 자동 검증을 다시 돌리자 급류 파도 검사는 통과했지만, 그 바로 뒤 **여우 마법사(FoxMagician) 표시 검사에서 실패**했다. 사용자가 main에서도 똑같이 실패하는 것을 확인했으므로 **이 VFX 작업의 결함이 아니라 main 쪽 별건**으로 따로 기록한다(사용자 승인 2026-10-19: "그렇게 정리하자"). 원인은 아직 조사하지 않았다. 남은 일은 [ROADMAP](../../../ROADMAP.md) 「전투·유닛 후속」 표에서 관리한다.

### 12-1. 사실 (근거 수준 표시)

- [전달] 메뉴 `Hexiege/Combat/Run Full Regression Suite`(Edit Mode)가 실패. 메시지: "Fox impact metadata must not consume charge lease or call the emitter."
- [직접] 호출 경로: `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` Run 1019행 `ValidateFoxMagicianProductionTimeline()` → 7908행 `ValidateFoxMagicianMarkerCallbacks()` → 8073행이 위 메시지를 낸다(2026-10-19 작업 트리, 읽기만 함).
- [전달] 사용자가 **main에서도 같은 실패**를 확인했다.
- [전달] `UnitView.ProcessAttackMarker`의 Fox 분기는 main과 이 브랜치가 같다. 이 브랜치는 시전자 소유 차지 정지 3줄(R2-1)만 더했다.
- [전달] 이 Fox 검사는 main `dbc48a8c`(2026-10-15)에서 들어왔다. main의 전체 검증은 `88f60e38`(2026-10-13) 이후 늘 그보다 앞의 Bloom 「PS 5개」 검사에서 멈췄으므로 **이 검사는 main에서 한 번도 도달하지 못했을 가능성이 높다**(11-3의 Torrent 검사와 같은 양상). 그 뒤 main 커밋들이 `UnitView`도 바꿨다(랠리 수정).
- **원인은 미조사**다. 에이전트 환경에 Unity가 없어 재현하지 못했다. 위 경위에서 원인을 추정하지 않는다.

### 12-2. 영향

- 전체 검증이 Run 1019행에서 멈추므로 그 뒤 검사(Inferno 타임라인·애니메이션, C1~C3 검사, `RunAuthoritativeAttackPresentationValidation`, FlameSpirit 등)는 **실행되지 않는다** — 통과도 실패도 아닌 미실행이다.
- 이 작업과 관련된 검사는 그보다 앞에 있으며 이 브랜치에서 통과했다 [전달]: Bloom(Run 약 1006~1008행) · VfxConnections(1009행) · Torrent 파도(1016행).

### 12-3. 이 작업의 테스트 방침

- 이 VFX 작업의 자동 검증 기준은 **Run 1016행(Torrent 파도)까지 통과**로 본다.
- 미실행 구간 중 `RunAuthoritativeAttackPresentationValidation`이 다루는 **방어 타워·버섯 착탄 표시 경로는 Fox 결함이 고쳐질 때까지 인게임 실기로 눈으로 확인**한다.
- Fox 결함의 조사·수정은 이 작업 범위가 아니다.

---

<a id="13-quakespirit-attack-vfx-rework"></a>

## 13. QuakeSpirit 공격 VFX 재제작 결과 (2026-10-19 추가 — 1~12절은 당시 기록으로 보존)

### 13-0. 쉬운 설명

대지의 정령(QuakeSpirit)이 공격할 때 나오는 이펙트를 사용자가 Unity AI와 Inspector 조정으로 새로 만들었다. 목표는 「땅을 내려치면 맞은 칸과 그 주변 6칸에 걸쳐 땅이 갈라지고 흙먼지가 피어오르는」 모습이다. 바닥에 6갈래로 뻗는 균열 그림, 천천히 부풀며 퍼지는 갈색 흙먼지, 튀는 흙줄기·흙알갱이·돌 조각, 중심의 짧은 섬광으로 구성된다. 사용자가 결과를 보고 승인했다(2026-10-19: "이정도면 괜찮은거같아 이렇게 완료하도록하자"). **화면 표현만 바뀌었고 실제 피해 범위(반경 1.0)는 그대로다.** 이펙트가 피해 범위보다 조금 더 넓게 보이는 것은 사용자가 고른 결과다.

### 13-1. 근거 수준과 기록 위치

- 이 작업의 원래 Research·Plan(1~12절, Plan 0~15절)에는 QuakeSpirit 항목이 **없었다** [직접 — 두 문서에서 `Quake` 검색 0건]. 사용자가 같은 브랜치에서 이어서 한 프리팹 작업이며, 사용자 지시(2026-10-19 "문서 갱신진행하자")로 이 문서에 결과를 기록한다. 코드 변경은 없다.
- 프리팹 커밋 [직접 `git log`]: `97239372`(2026-10-15) · `e3461650` · `fe449034` · `8da297ff` · `2f0de96d` · `e5d6ab21` · `d1a994fb`(2026-10-19, 브랜치 HEAD). 그 전 마지막 변경은 `3aa70c26`(2026-08-24).
- 아래 값은 메인 세션이 올라간 프리팹에서 확인해 전달한 것이며 **[전달]**, document-manager가 2026-10-19 작업 트리(`d1a994fb`)에서 프리팹·머티리얼·preset YAML을 직접 읽어 다시 확인한 값은 **[직접]**으로 표시한다(읽기만 했다).

### 13-2. 최종 프리팹 구성 — `Assets/_Project/Prefabs/VFX/Units/vfx_quakespirit_attack.prefab`

공통 [직접]: 루트 포함 7개 Particle System 모두 Looping OFF · Stop Action None · 길이 1초 · Scaling Mode Hierarchy(직렬화 값 0). 1회성 착탄 계약(VFXSFXGuide 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 1번 표)을 지킨다.

| 레이어 | 머티리얼 [직접] | 주요 값 [직접] | 비고 |
|---|---|---|---|
| `Ground_Cracks` | `Mat_VFX_HexFissures` (텍스처 `Art/VFX/QuakeSpirit/vfx_hex_fissures.png`, 새로 만듦) | Horizontal Billboard · Start Size 3.4 · 1개 | 6방향 균열이 중심에서 벌어지며 자란다 [전달]. 균열 끝은 텍스처 반폭의 약 95% → 중심에서 약 1.6 [전달 — 메인 세션이 PNG 알파에서 잼. 이 환경에는 이미지 라이브러리가 없어 재측정하지 않았다] |
| `Dust_Puffs` | 공용 `Mat_VFX_Smoke` (수정하지 않음 [전달]) | 12개 · 크기 0.6~0.8 · 속도 1.65~2.25 | 6방향 × 2, 어두운 갈색·숯색 Start Color, 약 1.3배로 부풀음, 형태 반경 0.18, Limit Velocity dampen 0.35, Noise 켬 [전달] |
| `Dirt_Streaks` | `Mat_VFX_QuakeDustGrain` | Stretched Billboard · 8개 · 속도 4.2~4.9 | dampen 0.4 [전달] |
| `Dirt_Specks` | `Mat_VFX_QuakeDustGrain` | 10개 · 속도 2.9~3.9 · 중력 3.2 | 형태 반경 0.65 [전달] |
| `Rock_Debris` | `Mat_VFX_PuffCloud` | 5개 · 속도 3.25~4.3 · 중력 3 | |
| `Impact_Flash_Core` | `Mat_VFX_GlowAdditive` | 크기 1.55~1.95 | |

**재작업 중 만든 에셋의 현재 참조 상태 [직접 — GUID 검색, `Assets` 아래 프리팹·머티리얼·에셋·씬]:** 아무것도 지우지 않았다.

| 에셋 | 상태 |
|---|---|
| `Mat_VFX_QuakeDustGrain` (+ `vfx_quake_dust_grain.png`) | **사용 중** — 위 `Dirt_Streaks`·`Dirt_Specks`. 처음엔 먼지용으로 만들었으나 먼지 레이어에서는 빠지고 흙 알갱이 레이어에 남았다 |
| `Mat_VFX_QuakeDust` (+ `vfx_quake_dust.png`) | **미사용** — 어느 프리팹·에셋도 참조하지 않는다 |
| `Mat_VFX_GroundCracks` (+ `vfx_ground_cracks.png`, 이전 사각형 균열 텍스처) | **미사용** — 어느 프리팹·에셋도 참조하지 않는다. 파일은 그대로 있다 |

정리(삭제) 여부는 사용자 결정 대상이며 이 기록은 삭제를 제안하지 않는다(WORKFLOW 「규칙」 삭제 명령 사전 허가).

- [직접] 같은 시기 별도 커밋 `575eee3f`(2026-10-15)가 `Assets/_Project/Prefabs/VFX/Units/vfx_cannon_impact.prefab`을 추가했다(13-5 2번의 먼지 비교 기준 `Smoke_Puff`가 이 프리팹 안에 있다). 이 프리팹은 **어느 preset·config·코드에서도 참조되지 않는다** — 용도와 연결 여부는 전달받지 않았으므로 추정하지 않는다.

### 13-3. 게임 연결 [직접]

- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_QuakeSpirit_Attack.asset`의 `_vfxPrefab`이 이 프리팹(GUID `6d2d8817…`)을 가리키고, `UnitEffectConfig.asset`이 이 preset을 참조한다 — 기존 공격 슬롯 그대로이며 preset은 이번에 바뀌지 않았다(마지막 변경 `3aa70c26`). 위치 보정 (0, 0.05, 0.5), 회전 보정 0, Scale Multiplier 1.
- [전달] 이펙트는 QuakeSpirit이 바라보는 방향을 따라 회전한다. 그래서 균열 6갈래의 방향은 **월드 헥스 격자에 고정되지 않고 공격 방향을 따른다.**

### 13-4. 보이는 범위와 실제 피해 범위 — 사용자가 받아들인 차이

- 사용자 의도 [전달]: 「타일 A를 공격하면 A와 둘레 6칸 전체에 이펙트가 보인다」.
- 헥스 수치 [전달]: 타일 폭 0.866 · 이웃 칸 중심까지 약 0.78~0.87 · 이웃 칸 바깥 가장자리 약 1.3.
- 최종 이펙트 끝은 약 1.6으로 이웃 칸 바깥 가장자리(약 1.3)를 넘는다 — **사용자 선택** [전달].
- 실제 피해 판정은 바뀌지 않았다 [직접]: `SpecialAttackConfig.asset` `_quakeRadius: 1`(코드 기본값 `SpecialAttackConfig.cs` `_quakeRadius = 1.0f`), 규칙은 `GameSystemRules_Units.md` 「특수 공격 시스템 규칙 — 착탄형 즉발 AoE 확장」 규칙 43. 따라서 **보이는 범위(약 1.6) > 피해 반경(1.0)** 이며, 이는 결함이 아니라 승인된 표현 결정이다.

### 13-5. 재작업에서 얻은 교훈 (다음 VFX 제작용 — VFXSFXGuide에도 반영)

1. **이미지 생성기에 「투명 배경에 6갈래 균열 한 장」을 요청하면 처음에는 갈라진 사각형 석판이 나왔다** [전달]. 투명 배경과 구도(중심에서 뻗는 6갈래, 바깥은 비어 있음)를 명시해 다시 요청하고, 크기를 정하기 전에 **텍스처 알파에서 균열 끝 위치를 재서** Start Size를 계산한다.
2. **먼지** [전달]: 큰 파티클 몇 개에 먼지 덩어리 그림 한 장씩을 실으면 「도장 찍힌 그림이 미끄러지는」 것처럼 보였고, 아주 작은 알갱이는 점으로만 보였다. 부드러운 연기 텍스처의 **중간 크기 퍼프가 천천히 부풀며 퍼질 때** 먼지로 읽혔다(`vfx_cannon_impact.prefab`의 `Smoke_Puff`와 같은 방식 — 같은 `Mat_VFX_Smoke` 사용 [직접]).
3. **「한 타일 범위」를 먼저 확인한다**: A + 이웃 6칸 전체 / 이웃 칸 중심까지 / 피해 반경 1.0 중 무엇인지에 따라 크기가 약 1.3·0.8·1.0으로 달라진다.
4. **Scaling Mode가 Local인 레이어는 루트·preset의 Scale Multiplier를 따르지 않는다** [전달 — 재작업 중 관찰] → 그런 레이어는 레이어마다 조정해야 한다. 최종 프리팹은 모든 레이어가 Hierarchy다 [직접].

### 13-6. 판정

- **사용자 승인 — 완료**(2026-10-19). 사용자 화면 확인이며 정밀 픽셀·멀티 양 peer 비교·자동 검증 대상은 아니다. `Testcase.md`는 요청받지 않아 만들지 않았다(WORKFLOW [8] 해당 없음).
- 이 결과는 10-4의 OPEN 결함(타워 번개 사망 시 끝점)과 무관하며 그 상태를 바꾸지 않는다.

<a id="14-projectile-visibility-decision"></a>

## 14. 원거리·마법 투사체가 보이지 않는 문제 — 사용자 결정 (2026-10-19 추가 — 1~13절은 당시 기록으로 보존)

### 14-0. 쉬운 설명

물폭탄·화염구·마법탄·포탄처럼 「날아가서 맞는」 공격이 화면에서 거의 보이지 않는다. 지금은 공격 동작의 타격 순간에 서버가 바로 피해를 주므로 날아가는 시간이 없기 때문이다. 사용자는 **C 방식(서버가 투사체의 발사·착탄 시각을 관리)** 으로 고치기로 정했고, **구현은 별건**으로 미뤘다. 지금은 VFX 제작을 계속하고, 완성된 VFX로 그 별건을 진행한다. 사용자 원문(2026-10-19): "C 방식으로 하는게 맞다고 생각해. 지금은 vfx작업을 진행하고 별건으로 완성된 vfx들을 활용하여 진행하도록할게 문서에 기록만 남겨줘". **이번에는 기록만 하며 코드는 바꾸지 않았다.**

### 14-1. 대상과 결정

- 대상: StreamSpirit 물폭탄 · InfernoSpirit 화염구 · FoxMagician 마법탄 · Tank/CannonCart 포탄 · MushroomBomber 기존 포자 트레이서.
- **채택 C — 서버 권위 투사체**: 서버가 발사 시각과 착탄 시각(= 발사 + 거리 ÷ 투사체 속도)을 정하고 **착탄 시각에 피해를 확정**한다 → 비행·피해·피해 숫자·사망·착탄 VFX가 모든 화면에서 맞는다. 이미 규칙에 있는 목표 설계다 [직접 확인]: `GameSystemRules_UnitCombatSynchronization.md` 「7. 전달 방식별 동기화」 `NET-DELIVERY-PROJECTILE`(268~270행) · `GameSystemRules_Units.md` 「공격 회차 안정 ID」 `U-IMPACT-LOCKEDPOINT`(225~229행). 규칙 변경은 없다.
- 보류(기각): **A** 발사 표시를 더 앞당김(사용자: 완성도 부족) · **B** 타격 표시를 뒤로 미룸(거리와 무관한 고정 비행 시간) · **D** 화면만 느린 투사체(피해 숫자·사망·착탄 VFX와 어긋남).

### 14-2. 조사 사실 [전달 — game-programmer 읽기 전용 조사, HEAD `ec000217`, 행 번호는 근사]

- 피해는 공격 클립의 OnAttackHit 시각에 서버에서 발생한다(`NetworkCombatController.cs` ~1430–1518, `UnitCombatUseCase.cs` ~709–718; 타격 시각 추출 `UnitFactory.cs` ~577–593). 게임플레이상 투사체 비행 시간은 없다.
- 이 유닛들은 「Unresolved」 프로파일이다(`UnitAttackShadowProfileResolver.cs` ~62–68).
- MushroomBomber 트레이서 비행 = ImpactTime + 0.10 − 현재 ≈ Host에서 0.10초(`HitPresentationQueue.cs` ~210–214, `EffectManager.cs` ~500–506) → 약 10~18 단위/초로 거의 안 보인다. 착탄 VFX 재생은 MushroomBomber 전용(`EffectManager.cs` ~531, `HitPresentationQueue.cs` ~206).
- FoxMagician 타격 표시는 일찍 반환한다(`UnitView.cs` ~7224) → 별도 경로가 필요하다.
- 타격 표시 시각 / 클립 길이(초): StreamSpirit 0.5/1.5 · MushroomBomber 1.0/3 · FoxMagician 차지 1.0·타격 2.8333/4 · InfernoSpirit 1.5/3 · Tank/CannonCart 0.1667/4.

### 14-3. 별건에서 정할 기획 질문 (game-design-lead — 미결정)

1. 유닛별 투사체 속도. 2. 비행 중 공격자가 죽으면 피해를 그대로 줄지. 3. 비행 중 대상이 죽거나 움직일 때 — 고정 지점(LockedPoint)인지 유도(Homing)인지. 4. 피해가 늦어지는 데 따른 밸런스 영향.

### 14-4. 그 사이 할 일

- VFX 프리팹(파티클 트레이서 + 착탄)은 지금 만들고, 연결은 그 별건에서 한다.
- 파티클 트레이서 계약: Looping ON · Stop Action None · 몸체 Local / 꼬리(trail) World · +Z 앞 방향 · 스크립트 없음.
- 미착수 기록: [ROADMAP 「전투·유닛 후속」](../../../ROADMAP.md#전투유닛-후속) · 계획 쪽 기록 [Plan 17절](Plan.md#17-projectile-visibility-decision).

<a id="15-r5-interim-impact-hookup"></a>

## 15. 재작업 R5 — VFX 확인용 임시 착탄 연결 (C 방식 구현 시 교체) (2026-10-19 추가 — 1~14절은 당시 기록으로 보존)

### 15-0. 쉬운 설명

사용자가 물의 정령(StreamSpirit)의 물폭탄 트레이서와 착탄(물 튀김) VFX를 만들어 게임에 연결했지만, 착탄 VFX가 **물폭탄을 맞은 쪽이 아니라 엉뚱한 때에** 나왔다. 착탄 슬롯이 「때린 유닛」이 아니라 「맞은 유닛」의 종류로 고르는 칸(hitPreset)이었기 때문이다. 그래서 StreamSpirit이 남을 때릴 때는 물 튀김이 안 나오고, 반대로 StreamSpirit·Tank·CannonCart가 **맞을 때** 나왔다. 사용자는 C 방식(14절)이 구현되기 전에 **지금 VFX를 눈으로 확인**하기 위해, 피해 시각과 어긋나는 것을 감수하고 **투사체를 느리게 날리고 도착한 자리에서 착탄 VFX를 재생**하는 임시 연결을 하기로 정했다(2026-10-19). 이 연결은 C 방식 구현 때 교체된다. Tank/CannonCart도 나중에 트레이서 프리팹을 받아 같은 경로를 쓴다.

### 15-1. 근거 수준

- [직접] = document-manager가 이 checkout(HEAD `d302e54e`)에서 파일을 읽어 확인. [전달] = 사용자·메인 세션·game-programmer가 전달한 값으로 다시 확인하지 않음.

### 15-2. 사용자가 만든 에셋 — 커밋 `bb5132ce`(2026-10-19, 메시지 「.」), 이어서 병합 커밋 `d302e54e`

- [직접 `git show --name-status bb5132ce`] 추가: `Assets/_Project/Prefabs/VFX/Units/vfx_streamspirit_tracer.prefab` · `vfx_streamspirit_impact.prefab`, preset `EffectPreset_StreamSpirit_Tracer` · `EffectPreset_StreamSpirit_Hit` · `EffectPreset_Cannon_Impact`(`Assets/_Project/Resources/Config/EffectPresets/`), 텍스처 `Assets/_Project/Art/VFX/StreamSpirit/vfx_water_orb.png` · `vfx_water_ripple.png`, 머티리얼 `Mat_VFX_WaterOrb` · `Mat_VFX_WaterRipple`, Unity AI 원본 `GeneratedAssets/…`(png+json 4쌍), 각 `.meta`. 수정: `Assets/_Project/Resources/Config/UnitEffectConfig.asset` · `Assets/_Project/Scenes/Login.unity`.
- [전달] 트레이서: 파티클 시스템 5개, Looping ON, Stop Action None, 몸체 3개 Local + 꼬리 2개 World — 14-4의 트레이서 계약에 맞는다. 착탄: 파티클 시스템 5개, 1회성, Stop Action None.
- [전달] preset 연결: `EffectPreset_StreamSpirit_Hit` → `vfx_streamspirit_impact`, `EffectPreset_Cannon_Impact` → `vfx_cannon_impact`(Plan 16-3에서 「아무도 참조하지 않음」으로 남겼던 프리팹 — 이제 이 preset이 참조한다).
- [직접 GUID 검색] `UnitEffectConfig.asset`: StreamSpirit 행 `tracerPreset` = `EffectPreset_StreamSpirit_Tracer`(143행, 정상) · `hitPreset` = `EffectPreset_StreamSpirit_Hit`(142행). Tank·CannonCart 두 행 `hitPreset` = `EffectPreset_Cannon_Impact`(70·78행).
- `Login.unity`의 키보드 UI 변경은 사용자가 **의도한 별도 UI 작업**이라고 확인했다 [전달]. 이 작업과 무관하며 내용은 검토하지 않았다.

### 15-3. 발견된 문제

1. **슬롯 의미 불일치** [직접]: `HitPresentationQueue.cs` 870·873행이 `TryPlayUnitHit(prepared.VictimUnitType, …)`로 **맞은 유닛 종류**의 hitPreset을 재생한다. 그래서 물 튀김이 StreamSpirit이 때릴 때는 안 나오고, StreamSpirit·Tank·CannonCart가 맞을 때 나왔다.
2. **착탄 프리팹이 작고 짧다** [전달]: Splash_Burst 입자 2~4개, 크기 0.2~0.32, 수명 0.18~0.24초. 사용자에게 키우는 값을 제안했다(제안일 뿐 적용 여부는 사용자 결정): Splash 4~6개 / 크기 0.35~0.5 / 수명 0.3~0.4초, 물방울 8~10개 / 0.08~0.12, 물결 0.7~0.8 / 0.6초, 전체 반경 약 0.4까지.
3. 투사체가 거의 안 보이는 원래 문제는 14절 그대로다(비행 약 0.1초).

### 15-4. 사용자 결정 (2026-10-19)

- C 방식(14절) 구현 전 **임시 조치**로, 피해와 어긋나는 것을 받아들이고 투사체를 느리게 날린 뒤 **도착 지점에서 착탄 VFX 재생**. C 방식 구현 시 이 연결을 교체한다. 서버·피해 판정은 바꾸지 않는다.
- Tank/CannonCart는 나중에 트레이서 프리팹을 만들어 같은 경로를 쓴다.
- 계획 항목과 알려진 부작용은 [Plan 18절](Plan.md#18-r5-interim-impact-hookup)이 단일 소스다.

<a id="15-5-r5-field-bugs"></a>

### 15-5. R5 실기 테스트에서 발견된 버그 (2026-10-19 추가 — 기록만, 미착수)

**쉬운 설명.** 사용자가 R5(물폭탄이 날아가 도착한 자리에서 물 튀김이 나오게 한 임시 연결, 커밋 `507cb07d`)를 직접 해 보니 물 튀김(착탄 이펙트)이 나오는 **때와 자리**가 어색한 경우가 세 가지 있었다. 모두 **코드를 고쳐야 하는 버그**지만, 사용자 지시로 **지금은 고치지 않고 기록만** 한다(지금은 VFX 제작에 집중). 이 버그들은 R5 임시 연결과, 나중에 만들 C 방식(서버 권위 투사체 — [14절](#14-projectile-visibility-decision) · [Plan 17절](Plan.md#17-projectile-visibility-decision))을 구현할 때 **함께 해결할 대상**이다. 고치는 방향은 [Plan 18-5](Plan.md#18-5-r5-followup-fix-queue)에 **제안**으로만 적었다(승인되지 않음).

**근거 수준.** [사용자] = 사용자 보고 원문 요지(그대로 옮김). [확인] = document-manager가 이 checkout(HEAD `507cb07d`)의 코드를 읽어 확인(행 번호는 이 시점 기준이며 메서드 이름을 함께 적는다). [미확인] = 코드만으로 정할 수 없음. 사용자가 테스트한 빌드에 `507cb07d`가 들어 있었는지는 **미확인**이다(사용자 보고에 빌드 표시가 없다).

> 번호는 B2부터 시작한다. B1(해당 유닛이 피격당할 때 피격 이펙트 발생)은 사용자가 바로 해결해 2026-10-19 사용자 지시로 이 목록에서 삭제했다. 다른 문단이 B2~B4 번호로 참조하므로 번호는 다시 매기지 않았다.

| # | 사용자 보고 [사용자] | 코드에서 확인한 것 | 원인 판정 |
|---|---|---|---|
| B2 | **이동 중인 유닛**을 공격하면 착탄 이펙트가 투사체가 유닛에 닿을 때가 아니라 **최초 발사 시점의 타겟 위치**에서 발생한다. | [확인] 도착 지점이 발사 순간 값으로 고정된다: `UnitView.cs` 7632~7640행이 발사 순간 타겟 위치를 `targetPos`로 한 번 읽고 7669~7688행 `PlayTracer(…, playImpactOnArrive: true)`에 넘긴다 → `EffectManager.cs` `PlayTracer` 624~626행이 시작/도착 좌표를 값으로 복사하고 636~637행 `Launch`에 넘긴다 → `TracerProjectile.cs` `Launch` 116~125행이 `_target`에 저장, `Update` 154~165행이 그 고정 지점까지 직선 보간한다(비행 중 타겟을 다시 읽는 코드 없음 — 파일 머리 주석 19~21행도 이것을 설계로 적고 있다) → 도착 시 `PlayLegacyArrivalImpact` 667~677행이 그 고정 지점에서 재생한다. 덧붙여 [확인] 같은 도착 순간의 **피해 숫자·타격 반응**은 `HitPresentationQueue.cs` `Emit` 957~964행이 타겟의 **현재** 위치를 다시 읽어 그 자리에 띄우므로, 움직이는 대상이면 물 튀김과 피해 숫자가 서로 다른 자리에 나온다. | [확인] 원인 확인. 🔴 규칙 관련: `GameSystemRules_Units.md` 「공격 회차 안정 ID」 `U-IMPACT-LOCKEDPOINT`는 LockedPoint 공격이면 발사 뒤 목표점을 바꾸지 **않는다**고 정한다. 그래서 「움직이는 대상을 따라가야 한다」는 기대는 그 유닛이 **Homing**(같은 문서 `U-IMPACT-INDEPENDENT`)으로 정해질 때만 규칙과 맞는다. 유닛별 LockedPoint/Homing은 [14-3](#14-projectile-visibility-decision) 3번 **미결정**이다. |
| B3 | **타겟이 삭제(사망/디스폰)되면** 날아가던 중간에 착탄 이펙트가 발생한다. 사용자 희망: 중간 이펙트 없이 투사체 소멸, **또는** 타겟 지점까지 날아간 뒤 착탄 이펙트 발생. | [확인] 트레이서 자체는 비행 중간에 도착 처리되지 않는다: `TracerProjectile.cs`의 도착(`Arrive`, 175~196행)은 `Launch`의 「비행 시간 0」(143~144행)과 `Update`의 「진행도 1」(163~164행)에서만 불리고, 꺼짐·파괴 때 도착을 부르는 코드가 없다. 트레이서는 타겟이 아니라 EffectManager의 VFX 보관 오브젝트 아래에 만들어진다(`EffectManager.cs` 843행). 즉 코드상 물 튀김(R5 도착 재생)은 타겟이 죽어도 **고정 지점까지 날아간 뒤** 나온다 — 사용자 희망의 두 번째 안이 코드상 이미 R5의 동작이다. 한편 타겟이 죽는 순간에 따로 화면 표현을 내는 경로는 있다: `HitPresentationQueue.cs` 안전망 ⓑ(머리 주석 20~21행, 구독 371~375행, `OnUnitDied`/`OnBuildingDied` 584~596행 → `FlushTarget` 1010~1018행)가 그 타겟을 기다리던 피격 표현을 **즉시** 방출한다 → `Emit`(941~975행)이 피해 숫자 + 타격 반응 + `PlayUnitHit(맞은 유닛 종류)`를 낸다. 또 사망 이펙트(`UnitView.cs` 1848행 `PlayUnitDeath`, 공용 preset `EffectPreset_Unit_Death_Common`)가 사망 순간에 나온다. | [미확인] **원인 미확인.** 사용자가 본 「중간 이펙트」가 위 어느 것인지 코드만으로 가를 수 없다. HEAD에서는 `hitPreset`이 모두 비어 있어 안전망 ⓑ가 VFX를 내지 않지만, `507cb07d` 이전 빌드였다면 맞은 쪽이 StreamSpirit·Tank·CannonCart일 때 안전망 ⓑ가 **사망 순간 대상 위치에서** 착탄 preset을 재생했을 것이다(테스트 빌드가 `507cb07d` 이전이었을 가능성). 사망 이펙트를 착탄으로 본 가능성도 배제하지 못했다. |
| B4 | 착탄 이펙트가 대상 유닛/건물의 **크기에 따라 몸체에 가려진다.** 사용자 제안: 대상이 크면 중심에서 그 크기만큼 떨어진 지점(예: 공격자 쪽 표면)에서 이펙트 발생. | [확인] 재생 위치는 도착 지점 그대로다(`EffectManager.cs` `PlayLegacyArrivalImpact` 675행 `Play(impact, arrival, rotation)`; 방향은 수평 비행 방향 670~674행, 그 밖의 보정은 preset 오프셋·배율뿐). 도착 지점은 발사 순간 타겟의 표시 위치다(B2와 같은 경로 — 메서드 주석 661행은 이를 「대상 발밑 위치」라고 적는다). 대상 크기를 보는 코드는 이 경로에 없다. 참고 가능한 기존 데이터 [확인]: 유닛은 `UnitEffectConfig.asset` `_recipientFits` 표(25행 — 유닛 종류별 `radius`·`height`·`footOffset`, 측정 도구가 채움; 예 StreamSpirit 292~294행 radius 0.1667·height 0.4157)가 있으나, `UnitEffectConfig.cs` 75행 툴팁대로 `radius`는 **현재 어떤 배율 계산에도 쓰지 않는 참고값**이다. 건물은 `BuildingEffectConfig.cs` 항목(25~37행)이 파괴·업그레이드·공격 preset뿐이고 **크기 표가 없다.** | [확인] 원인(크기 미반영) 확인. 건물용 크기 데이터는 새로 마련해야 한다. |

- 이 목록은 15-3(슬롯 의미 불일치 · 착탄 프리팹 크기 · 원래 비행 시간 문제)과 **별개**다. 15-3은 수정하지 않았다.
- **[2026-10-19 덧붙임 — 15-3 원문 유지]** 15-3 1번이 인용한 `HitPresentationQueue.cs` 870·873행은 확정(authoritative) 결과 경로다. StreamSpirit 같은 Legacy 유닛이 실제로 타는 경로는 같은 파일 `Emit` 969~973행 `PlayUnitHit(맞은 유닛 종류)`이며, 이것도 맞은 유닛 종류로 고르므로 15-3의 결론(슬롯 의미 불일치)은 그대로다.
- C 방식과의 관계: B2·B3는 14-3의 기획 질문 3번(비행 중 대상이 죽거나 움직일 때)과 같은 질문이다. B4의 「표면에서 터뜨리기」는 C 방식의 착탄 VFX에도 그대로 필요하다. 그래서 R5만 고치고 끝내지 말고 C 방식 설계 때 함께 다룬다(제안 — Plan 18-5).

<a id="15-6-charge-unit-fire-timing"></a>

### 15-6. 차지 유닛 발사 시점 기준과 InfernoSpirit 차지 어긋남 (2026-10-19 추가 — 같은 날 별건으로 완료)

> **상태 (2026-10-19 갱신): 완료** — 별건 [`2026-10-19/11_09_charge-unit-launch-timing`](../../2026-10-19/11_09_charge-unit-launch-timing/Research.md#9-implementation-result)에서 구현·사용자 싱글·멀티 시각 확인. 아래 본문은 HEAD `3306cf1b` 기준 기록이라 고치지 않았다(차지 시점은 그 뒤 0.1667초로 확정). *(종전 제목 표시 「기록만, 미착수」)*

**쉬운 설명.** 지옥불 정령(InfernoSpirit)은 공격할 때 먼저 불덩이를 모으는 「차지」 이펙트를 보여 준다. 그런데 오늘 화염구(투사체)를 연결해 보니, 차지가 **공격 동작 중간쯤에야 나타나고 같은 순간에 화염구도 날아가** 「모으는 중에 이미 쏜」 것처럼 어긋나 보였다. 원인은 이 공격 동작에 「차지 시작」 신호가 없고 「타격」 신호 하나만 있어서, 차지와 발사가 둘 다 그 한 신호에서 시작되기 때문이다. 사용자는 차지하는 유닛의 기준을 **「차지 시작 → 차지가 끝나는 순간 투사체 발사」** 로 정했고(2026-10-19), 두 시점은 코드에서 시간을 늦추는 방식이 아니라 **애니메이션 이벤트로** 정한다. 사용자 지시("기록만 해두고 VFX를 마저 제작한 뒤 별건으로 작업")에 따라 **이번에는 기록만 하며 코드·애니메이션·규칙 문서는 바꾸지 않았다.**

**근거 수준.** [확인] = document-manager가 이 checkout(HEAD `3306cf1b`)에서 파일·이력을 읽어 확인(행 번호는 이 시점 기준). [전달] = 메인 세션이 전달한 값으로, 다시 재지 않음. [사용자] = 사용자 결정·기억.

#### 15-6-1. 사용자 확정 기준 (2026-10-19) [사용자]

- 차지하는 유닛은 **차지 시작 → 차지가 끝나는 순간 투사체 발사**.
- `OnAttackCharge` 이벤트 = **차지 VFX만** 재생. `OnAttackHit` 이벤트 = **투사체 발사**. 서버 피해도 지금처럼 `OnAttackHit` 시점이다 — **피해 시점은 바꾸지 않는다.**
- 두 시점 모두 애니메이션 클립의 이벤트로 정한다(코드 지연 아님).

#### 15-6-2. 확인된 사실

| 항목 | 내용 | 근거 |
|---|---|---|
| Inferno 클립 이벤트 | `InfernoSpirit_Attack.anim`(30fps, 길이 3초)의 이벤트는 `OnAttackHit` @ 1.5초 **하나뿐**이다(93389~93391행). `OnAttackCharge`는 없다. | [확인] |
| Inferno 이벤트 이력 | `d692cec1`(2026-06-20)·`38f56aeb`(2026-09-30): `OnAttackHit` 0.5초 → `b65751c6`(2026-10-12): 1.5초(사용자 교정 1:15@30fps). 그 이전 커밋에는 이벤트가 없다. **어떤 버전에도 `OnAttackCharge`는 없었다.** | [확인 — 클립 파일 `git log` + 각 버전 `git show`] |
| 차지 이벤트 처리 | `UnitView.cs` `OnAttackCharge`(1149~1155행)는 1151행에서 **FoxMagician이 아니면 바로 return**한다. 이벤트 중계는 `AnimationEventRelay.cs` 40~44행. | [확인] |
| 공격(차지) VFX 재생 위치 | 공격 VFX 재생 호출 `effectManager.PlayUnitAttack(…)`는 `UnitView.cs` 7519행 **한 곳뿐**이며 공격 마커 처리 `ProcessAttackMarker` 안에 있다. Inferno에는 차지 이벤트가 없으므로 이 호출은 **타격 마커(1.5초)** 에서만 실행된다. | [확인] |
| Inferno 차지 프리팹 연결 | `UnitEffectConfig.asset` InfernoSpirit 행(unitType 12, 96행): `attackPreset` = `EffectPreset_InfernoSpirit_Attack`(97행) → `_vfxPrefab` = `vfx_infernospirit_charge.prefab`(GUID `386d8a44…` 일치). `impactPreset` = `EffectPreset_InfernoSpirit_Impact`(100행), `tracerPreset` = `EffectPreset_InfernoSpirit_Tracer`(103행). | [확인 — GUID 대조] |
| 오늘 무변경 | 차지 프리팹 마지막 변경 `3151d0df`(2026-08-17), 공격 preset 마지막 변경 `a840a152`(2026-10-13, 시전자 사망 시 정지). 오늘(2026-10-19) 둘 다 바뀌지 않았다. | [확인 — `git log -1`] |
| 오늘 달라진 것 | 사용자 커밋 `35150e8f`(2026-10-19 19:08, 메시지 「.」)가 Inferno tracer·impact 프리팹 2개와 preset 2개를 추가하고 `UnitEffectConfig.asset`을 수정했다. 그래서 화염구가 같은 1.5초 타격 마커의 기존(legacy) 트레이서 경로(`UnitView.cs` 7669행 `em.PlayTracer(…)`)에서 출발한다 → **차지와 화염구가 동시에 나가 어긋나 보인다.** | [확인 — `git show --name-status 35150e8f`, 행 위치] |
| 피해 시점 추출 | 서버 피해 시점은 `UnitFactory.cs` 586행이 함수 이름 `OnAttackHit`인 이벤트만 모아 정한다. `UnitView.cs` 820·849행도 `OnAttackHit`만 센다. → 클립에 `OnAttackCharge`를 더해도 **피해 시점 추출은 바뀌지 않는다.** | [확인] |
| Inferno 자동 검증 | `RunUnitActionSelfValidation.cs` `ValidateInfernoSpiritProductionTimeline`(8129행~)은 `OnAttackHit`만 세어 「정확히 1개 · 1.5초」를 요구한다(8208~8217행). `OnAttackCharge`를 더해도 이 검사는 그대로 통과할 것으로 **코드상 보인다**(실행 확인은 아님). | [확인 — 코드 읽기] |
| FoxMagician 클립 | `FoxMagician_Attack.anim`(30fps, 길이 4초): `OnAttackCharge` @ 1.0초(113276~113277행), `OnAttackHit` @ 2.8333333초(113283~113284행). → **애니메이션은 이미 사용자 기준에 맞는다.** | [확인] |
| Fox 타격 마커 | `UnitView.cs` 7224~7235행: Fox는 타격 마커에서 진단만 기록하고 **일찍 return**한다(주석: 「VFX·recipient 큐·로컬 피격·트레이서를 다시 실행하지 않는다」). → **투사체를 쏘지 않는다.** | [확인] |
| Fox 검증 | `RunUnitActionSelfValidation.cs` 7966~7967행이 Fox `tracerPreset` = null을 요구한다(메시지: 「tracer 연결은 별도 권위 투사체 설계가 필요」). | [확인] |

#### 15-6-3. 사용자 기억과의 대조

- [사용자] 「차지가 중간쯤 나오고 그 뒤에 피해」.
- [확인] 2026-10-12(`b65751c6`) 이후 차지는 3초 동작의 **1.5초(중간)** 에서 시작한다 — 기억의 「중간쯤」과 맞는다.
- [전달] 차지 이펙트는 약 1초 동안 커진다. 「그 뒤에 피해」는 피해 대부분이 화상 지속 피해(초당 50 — [확인] `StatsReference.md` 289행; 지속 3초는 [전달])라 직접 피해 뒤에 숫자가 이어 뜨기 때문으로 보인다(해석 — 실기 측정 아님).

#### 15-6-4. 🔴 규칙 문서와 맞닿는 곳 (변경 전 사용자 확인 대상 — 이번에 수정하지 않음)

- `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 **규칙 17** 「FoxMagician typed marker 계약」(320행) [확인]: `OnAttackCharge @ 1초`는 source VFX/SFX만 내고 tracer 경로 **전에 종료**, `OnAttackHit @ 85/30초`의 로컬 callback은 **VFX 재생을 하지 않는다**고 적혀 있다. 사용자 기준의 「Fox 타격 마커에서 투사체 발사」는 이 문장 중 **「OnAttackHit에서 VFX 재생 안 함」과 충돌**한다. 별건 작업에서 이 문장을 바꾸려면 변경 전에 사용자에게 보고하고 승인을 받는다.
- 같은 내용이 `GameSystemRules_UnitCombatSynchronization.md` 「8. Animation Event와 표현」(309행)에도 「damage callback에서 charge를 소비·재생하지 않는다」로 있다 [확인]. 「차지 VFX 재재생 금지」와는 맞지만 Fox 투사체 발사 위치는 위 규칙 17을 따른다.
- `InfernoSpirit`의 차지 이벤트를 다루는 규칙 문장은 찾지 못했다(규칙 17은 Fox 전용) [확인 — `OnAttackCharge` 전체 검색]. 「차지 유닛 공통 기준」을 규칙 문서·`VFXSFXGuide.md`에 넣을지는 별건 작업 때 사용자 확인 뒤 정한다.

#### 15-6-5. 다른 기록과의 관계

- [14절](#14-projectile-visibility-decision)(C 방식)과 같은 투사체 주제다. C 방식은 「언제 피해를 줄지」(착탄 시각)를, 이 절은 「언제 쏘는 모습을 보일지」(발사 시각)를 다룬다. 사용자 기준은 피해 시점을 그대로 `OnAttackHit`에 두므로, C 방식에서도 발사 = `OnAttackHit`, 착탄 = 발사 + 거리 ÷ 속도로 이어 붙일 수 있다(해석 — 설계 결정 아님).
- [15-5](#15-5-r5-field-bugs)(R5 버그 B2~B4)와는 별개 결함이다. 같은 R5 임시 트레이서 경로를 쓰므로 함께 다루기를 제안한다.
- 후속 작업 항목은 [Plan 18-6](Plan.md#18-6-charge-unit-fire-timing)이 단일 소스다 — 2026-10-19 별건으로 완료(위 상태 블록). *(종전 「(제안, 미착수)」)*
