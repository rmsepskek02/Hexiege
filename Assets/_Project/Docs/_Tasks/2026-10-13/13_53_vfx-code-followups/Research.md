# Research — VFX 코드 후속 3건 (Bloom 힐 파티클 개수 계약 · RuneSpire 번개 끝점 추적 · 상태 이펙트 크기 맞춤)

## 0. 이 문서가 무엇인가 (쉬운 설명)

사용자가 Unity AI로 몇몇 이펙트(VFX) 프리팹을 새로 만들어 main에 올렸다(`88f60e38`). 그런데 프리팹만 바꿔서는 고칠 수 없고 **코드를 고쳐야만 해결되는 문제 3가지**가 남았다. 이 문서는 그 3가지가 지금 코드에서 왜 생기는지를 실제 파일을 읽어 정리한 조사 기록이다. 고치는 방법은 같은 폴더의 `Plan.md`에 있다.

1. **꽃요정(BloomFairy) 힐 이펙트가 아예 안 나온다.** 코드가 "힐 이펙트 안의 파티클 묶음은 정확히 5개여야 한다"고 검사하는데, 새 프리팹은 사용자 결정으로 고리(Bloom_Ring)를 빼서 4개다. 그래서 코드가 이펙트를 "규격 불량"으로 보고 재생을 거부한다.
2. **정령 방어 타워(RuneSpire)의 번개가 움직이는 적을 따라가지 않는다.** 번개가 나가는 약 0.35초 동안 맞은 적이 걸어가도 번개 끝은 처음 자리에 그대로 있다. 조사해 보니 현재 코드는 번개 **방향만** 정하고, 번개 **길이를 거리에 맞추는 코드 자체가 없다.**
3. **화상·독·힐처럼 유닛에 붙는 상태 이펙트가 유닛 크기와 상관없이 항상 같은 크기다.** 그래서 작은 유닛에게는 너무 크고, 큰 유닛에게는 어색하게 보인다. 코드가 프리팹 원래 크기 × 고정 배율(현재 1)만 쓰고, 맞은 유닛의 크기를 보지 않기 때문이다.

세 가지 모두 **화면 표현만의 문제**다. 피해량·회복량·HP·타이밍 같은 게임 규칙(서버 권위)은 건드리지 않는다.

---

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
