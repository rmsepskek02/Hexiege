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
