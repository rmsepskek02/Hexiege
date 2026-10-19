# Plan — VFX 코드 후속 3건 (Bloom 힐 파티클 개수 계약 · RuneSpire 번개 끝점 추적 · 상태 이펙트 크기 맞춤)

## 0. 이 계획이 무엇인가 (쉬운 설명)

새로 만든 이펙트 프리팹이 제대로 보이도록 **화면 표현 코드만** 세 군데 고친다. 게임 규칙(피해·회복·HP·쿨다운·타겟 선택)은 하나도 바꾸지 않는다. 조사 근거는 같은 폴더 `Research.md`.

- **A. 꽃요정 힐 이펙트가 다시 보이게 한다.** "파티클 묶음이 정확히 5개여야 한다"는 검사를 "1개 이상이고, 묶음 하나하나가 규격(반복 재생·로컬 좌표 등)을 지키면 된다"로 바꾼다. 로그에는 실제 개수를 적는다.
- **B. 정령 타워 번개가 움직이는 적을 따라가게 한다.** 번개가 보이는 동안 매 프레임 "원래 맞은 그 유닛"의 위치를 다시 읽어 번개의 방향·길이·끝 불꽃 위치를 맞춘다. 그 유닛이 죽거나 사라지면 마지막 위치에서 멈춘다. 인간 타워는 그대로다.
- **C. 화상·독·힐 이펙트를 맞은 유닛 크기에 맞춘다.** 크기를 알아내는 방식은 사용자가 2026-10-13에 추천안으로 결정했다(아래 D-1): "유닛 종류별 크기 표(Inspector에서 조정 가능)를 에디터 측정 도구로 처음 채워 두고, 게임 중에는 그 표만 읽는다". 힐·독도 화상과 같은 방식으로, 한 배율(균일 스케일)로 맞춘다(D-2·D-3).

구현은 이 Plan을 사용자가 승인한 뒤에만 시작한다(WORKFLOW [4]). C는 D-1~D-3 결정 전에는 착수하지 않는다.

> **사용자 승인: 2026-10-13, 구현 착수 (game-programmer).** 사용자 원문: "추천방식대로 구현하면될거같아 구현진행해". Plan 전체를 승인하고 D-1~D-5 모두 추천안을 선택했다(2절 결정 기록). 이 승인은 구현 착수 허가이며 구현 완료·정적/native 검사·실기 수용을 뜻하지 않는다.

---

> **[2026-10-19 main 병합 메모 — 아래 본문의 경로·메뉴 표기는 작성 당시 그대로 보존]** main(`0978d908`)의 에디터 도구 정리로 `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` · `RunUnitActionSelfValidation.VfxConnections.cs`는 `Assets/Editor/Tools/Combat/` 아래로 옮겨졌고(GUID·partial 보존), 전투 자기 검증 메뉴 이름은 `Hexiege/Combat/Run Full Regression Suite`, 루트 포즈 자기 검증은 `Hexiege/Combat/Diagnostics/Self Validate Root Pose Audit`이 됐다. 본문의 「Unit Action 자기 검증 메뉴」 · 「Self Validate Unit Root Pose Cross Audit」는 이 두 메뉴를 가리킨다. 이 작업이 만든 `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`(`Hexiege/Effects/Measure Unit Recipient Fit`)는 경로 변경이 없으며, WORKFLOW [5-2] 「에디터 스크립트 최소 유지 기준」의 현재 진입점 표에 들어 있지 않다 — 유지 여부는 사용자 결정 대기다. 현행 도구 목록은 [WORKFLOW](../../../WORKFLOW.md) [5-2].

## 1. 🔴 기존 로직 제거·비활성화 항목 (WORKFLOW [4] — 최상단 명시)

모두 **삭제가 아니라 주석 처리로 비활성화**한 뒤 새 코드를 옆에 둔다. 주석 처리분의 최종 삭제는 [6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전에 한다.

| # | 대상 (2026-10-13 작업 트리 기준) | 처리 | 안전한 근거 |
|---|---|---|---|
| R-1 | `VfxPoolItem.cs` `PlayBloomLoop`의 `if (_bloomSystems.Length != 5) return 0;` (134행) | 주석 비활성화 → `Length == 0`이면 거부로 대체 | 5라는 수는 옛 프리팹 구성(Bloom_Ring 포함)에 대한 가정일 뿐 규칙 조항이 아니다(Research 2-4). 바로 아래 시스템별 계약 검사(loop·unscaled·stopAction·Local·burst)는 **그대로 유지**하므로 잘못된 프리팹은 여전히 재생 전에 거부된다. 회복량·HoT 상태 복제와 무관(표현 전용). |
| R-2 | `VfxPoolItem.cs` 로그 리터럴 `"RecursivePS=5"` (154·180·432·531행) | 리터럴을 실제 개수 출력으로 교체(옛 리터럴은 주석으로 남기지 않아도 됨 — 로그 문자열은 동작 로직이 아님) | 저장소 안에 이 문자열을 파싱하는 분석 도구가 없다(Research 2-2). 진단 로그 의미가 "사실"에 맞게 바뀔 뿐 동작 변화 없음. |
| R-3 | `RunUnitActionSelfValidation.cs` `ValidateBloomHoTEffectBoundary`의 `authored.Length == 5`(1385행), `BloomRecursiveSystemCount == 5`(1448행) | 주석 비활성화 → "authored 개수 ≥ 1" 및 "lease가 재생한 개수 == authored 개수" 단언으로 대체 | 검증 강도는 낮추지 않는다: 숫자 5를 "실제 프리팹에서 센 값"으로 바꾸고, 시스템별 계약·루트 PS 존재·자연 drain·malformed 거부 단언은 모두 유지한다. |
| R-4 (C 승인 시) | `VfxPoolItem.TimedDamage.cs` `PlayTimedDamage`의 고정 스케일 `transform.localScale = Vector3.Scale(_originalLocalScale, preset.ScaleMultiplier);` (30행) | 주석 비활성화 → 대상 맞춤 스케일로 대체 | 표현 전용 값. preset 배율은 새 계산의 곱셈 인자로 계속 쓴다(=기존 조정 수단 보존). |
| R-5 (C 승인 시) | `VfxPoolItem.cs` `FollowBloomPose`의 `pose.lossyScale` 곱셈(170행) | 주석 비활성화 → 대상 맞춤 스케일로 대체 | VisualRoot 스케일은 셋업 검증상 1이며(Research 4-1) 실제 크기 차이를 반영하지 못한다. 단 유닛 루트 스케일이 1이 아닌 유닛이 있으면 이중/누락 스케일이 될 수 있으므로 **구현 전 game-programmer가 유닛별 `lossyScale` 실측 후** 대체 여부를 확정한다(U-5). |

- B에는 제거 항목이 없다(추가만 한다). 인간 타워·버섯 착탄 경로는 기존 `VfxPoolItem.Play` 그대로다.
- 호출자 없는 레거시 `HitPresentationQueue.PlayTowerAttackVfx`(1138~1174행)는 **건드리지 않는다**(범위 밖).

---

## 2. 사용자 결정 항목 (2026-10-13 전부 결정됨)

**결정 기록 (2026-10-13, 사용자 원문 "추천방식대로 구현하면될거같아 구현진행해" — 전 항목 추천안 채택):**

- **D-1 → (3) 혼합 방식.** 에디터 측정 도구가 유닛 종류별 크기를 재서 `UnitEffectConfig`의 표에 채우고, 런타임은 그 표만 읽는다(4-C-2 1안 그대로).
- **D-2 → 같이.** 힐(BloomFairy)·독(MushroomBomber)도 화상과 같은 방식으로 맞춘다.
- **D-3 → 균등(단일 배율, 균일 스케일).** 기본값 = 키 비율, 표에서 유닛별 수동 배율로 덮어쓰기 가능.
- **D-4 → RuneSpire 프리팹 현 구조 유지.** `Impact_Sparks`는 `Bolt`의 자식이며, 코드는 `Bolt`의 Z 스케일만 설정한다(Impact_Sparks 위치는 따로 설정하지 않는다). 확인된 사실: 프리팹에 저장된 `Bolt` localScale = (1, 1, 3.5), `Bolt` 아래 `Impact_Sparks` localPosition = (0, 0, 1) — 메인 세션이 commit `88f60e38`에서 확인. 프리팹 수정 없음(5절 「바꾸지 않는 것」 유지). 검증기 (a)의 계층 단언은 "자식" 계층을 기대값으로 한다.
- **D-5 → 해석 a 허용.** 스냅샷의 *원래* 맞은 유닛을 **화면 표현으로만** 따라가는 것은 규칙 12의 "현재 target 재조회 금지"에 해당하지 않는 것으로 해석한다. 피해·타겟 판정은 바꾸지 않는다. 규칙 12 상태 블록 문구 보강은 **구현·테스트 후 [7]/[11] 문서 반영 단계의 항목으로 남기며, 지금은 규칙 문서를 수정하지 않는다**(규칙 번호 불변).

아래 표는 결정 근거(원래 선택지·추천 이유)로 보존한다.

| ID | 질문 | 선택지 | 추천 | 상태 |
|---|---|---|---|---|
| **D-1** | C: 대상 유닛 크기를 어떻게 알아낼까 | (1) 런타임 렌더러 bounds / (2) 유닛 종류별 크기 표(ScriptableObject) / (3) 혼합: 에디터 도구가 표를 측정해 채우고 런타임은 표만 사용 | **(3)** — 4-3절 비교 | **결정됨 (2026-10-13): (3) 혼합** |
| **D-2** | C: 힐·독도 화상과 같은 방식으로 맞출까 | 같이 / 화상만 | **같이** — 세 프리팹이 같은 제작 기준(발밑 pivot, 반경 0.25·높이 1.0)이고 같은 VfxPoolItem 경로라 한 번에 맞추는 편이 일관된다. 화상만 하면 같은 유닛에 독·힐은 계속 크기가 안 맞는다 | **결정됨 (2026-10-13): 같이** |
| **D-3** | C: 크기를 축별로 다르게(가로=반경 비율, 세로=키 비율) 줄까, 한 배율로 줄까 | 비균등 / 균등(한 값) | **균등**(기본값 = 키 비율, 표에서 유닛별로 덮어쓰기 가능) — 프리팹이 Hierarchy 스케일링이라 비균등이면 파티클이 납작/길쭉해질 수 있다(Research 4-2). 사용자가 승인한 화상의 "모양"을 보존하는 쪽 | **결정됨 (2026-10-13): 균등(단일 배율)** |
| **D-4** | B: RuneSpire 프리팹의 실제 의도 계층 | 디스크 상태: `Impact_Sparks`가 `Bolt`의 자식, Bolt z=3.5 / 전달 설명: 형제, Bolt z 0→1 | 구현은 **어느 쪽이든 동작**하게 만든다(5-B 설계). 다만 검증기에 넣을 "계약"을 정하려면 확정 필요. 디스크 상태 그대로 쓰면 `Bolt.z`만 설정하면 됨 | ~~**OPEN** (메인 세션이 git으로 확인 후 사용자 확인)~~ → **결정됨 (2026-10-13): 현 구조 유지** — `Impact_Sparks`는 `Bolt`의 자식, 코드는 `Bolt` Z 스케일만 설정. 저장값 `Bolt` localScale (1,1,3.5)·`Impact_Sparks` localPosition (0,0,1), 메인 세션 commit `88f60e38` 확인 |
| **D-5** | B: 규칙 12의 "현재 target 재조회 금지"와 이번 추적의 관계 | 해석 a: 금지 대상은 "타워의 *현재* 타겟을 새로 찾는 것"이며, 스냅샷의 *원래* TargetId 객체를 시각적으로 따라가는 것은 허용 / 해석 b: 표현은 스냅샷 Impact 좌표만 써야 함 | **a** — 사용자가 "원래 맞은 유닛 추적"을 직접 요청했고, 피해·타겟 선택은 바뀌지 않는다. 확정되면 구현·테스트 후 [11]에서 규칙 12 상태 블록에 문구를 보강한다(규칙 번호 불변) | ~~**OPEN** — 사용자 확인 전 규칙 문서 수정 없음(규칙 충돌은 변경 전에 알린다는 영구 선호)~~ → **결정됨 (2026-10-13): 해석 a 허용** — 화면 표현으로만 원래 대상을 따라가며 피해·타겟 판정 무변경. 규칙 12 상태 블록 보강은 구현·테스트 후 문서 반영 항목(지금 규칙 문서 수정 없음) |

---

## 3. 근거 규칙 (각 수정 항목이 기대는 조항)

| 항목 | 근거 규칙 | 어떻게 기대는가 |
|---|---|---|
| A | `GameSystemRules_Units.md` 규칙 31 (다중 파티클 시스템 VFX 재생) | 풀 아이템이 프리팹 안의 시스템을 **전부** 재생해야 한다는 조항. 개수를 고정하지 않는다 → 4개짜리도 전부 재생하면 규칙을 지킨다. |
| A | Units 규칙 34 (HoT/DoT 공용 시간 지속 효과) · 규칙 37 | HoT 회복·텍스트는 서버 권위이며 표현 변경이 이를 바꾸지 않는다. **파티클 개수를 정한 조항은 없음** — 규칙 34의 "all5PS"는 구현 상태 기록이라 구현 후 문서 반영 대상. |
| B | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 9 (서버 권위 처리) | 타겟·사거리·피해는 서버만 판정, 클라이언트는 결과를 시각 표현에만 사용 → 추적은 표현 전용. |
| B | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 (타워 발사 연출) | 즉발 피해 유지·각 클라이언트 로컬 VFX 재생. "현재 target 재조회 금지" 문구는 D-5로 해석 확인. Race로 Spirit만 분기(Type+Race 선택 유지, Transcendence 미지정 no fallback 유지). |
| B | `GameSystemRules_UnitCombatSynchronization.md` NET-AUTH-001 · NET-PRESENT-003 | VFX 위치는 결과 입력이 될 수 없다 / 현재 View 조회를 필수 조건으로 삼지 않고, 객체 반응은 동일 객체 수명 확인 시에만 → 대상 해석 실패 시 스냅샷 Impact로 재생, 동일성 확인 실패 시 추적 중단. |
| B | Units 규칙 20 (원거리 유닛 트레이서) — 유추 | 연출 전용 발사체는 피해 타이밍에 영향을 주지 않는다는 원칙을 타워 번개에도 동일하게 적용. |
| C | **크기 맞춤을 직접 정한 규칙 없음.** 관련: Units 규칙 34·40·41·42(효과 수명·수치·갱신 정책 불변, 상태 블록의 "대상에 붙어 재생") | 이번 변경은 크기·위치만 바꾸고 수명·갱신(같은 owner refresh 무재시작)·종료(StopEmitting→drain→반환) 계약을 그대로 둔다. |
| 전부 | `GameSystemRules_Sound.md` 규칙 15 (VFX+SFX 쌍) | **새 VFX 호출 지점을 만들지 않는다.** 기존 호출 지점의 "SFX 미제작" 주석(`EffectManager.cs` 268·291·423행의 `Sound15:` 주석)을 유지한다. 타워·상태 SFX 추가는 범위 밖. |

---

## 4. 항목별 구현 계획

### 4-A. BloomFairy 힐 VFX — 개수 무관 계약

**파일:** `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`, `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

1. `PlayBloomLoop`: R-1대로 `!= 5` 거부를 비활성화하고 **`_bloomSystems.Length == 0`이면 0 반환**. 시스템별 계약 검사(135~137행)는 한 글자도 바꾸지 않는다.
2. 로그: `"RecursivePS=" + _bloomSystems.Length` 형태로 실제 개수 기록(Play·StopEmitting·Drain·Return 4곳). `VfxPoolItem.TimedDamage.cs:37`과 같은 표기.
3. 주석의 "5PS"(125·151행)를 "authored 전체 PS"로 고친다(사실에 맞게).
4. 검증기 `ValidateBloomHoTEffectBoundary`:
   - 1385행: `authored.Length >= 1 && 루트 PS 존재`로 교체(R-3). 메시지에서 5 제거.
   - 1448행: `item.BloomRecursiveSystemCount == authored.Length && item.ParticleSystemCount == 1`로 교체.
   - 1474·1477·1479·1491행 메시지의 "All5PS"/"Malformed5PS"를 개수 중립 표현으로 교체(로직 불변).
   - **추가 단언 1개:** PS가 0개인 임시 오브젝트로 `PlayBloomLoop`가 0을 반환하는지(개수 하한 회귀 방지). 원본 프리팹·config는 수정하지 않는다.

**수용 기준:** 4PS 새 프리팹으로 힐 이펙트가 재생된다 / malformed(loop OFF 등)·0PS는 여전히 재생 전 거부된다 / 기존 refresh 무재시작·Stop 시 마지막 pose 고정·자연 drain 1회 반환 단언이 그대로 통과한다.

### 4-B. RuneSpire 번개 — 원래 맞은 유닛을 끝점으로 추적

**파일(예상):** `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`, `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs`, `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` 또는 신규 partial `VfxPoolItem.TowerBolt.cs`(+`.meta`), 검증기 `RunUnitActionSelfValidation.VfxConnections.cs`

설계:
1. **대상 해석(재생 1회):** `HitPresentationQueue.EmitAttackEffects`에서 Tower·Race=Spirit 스냅샷이면 `_presentationPoseProvider.GetUnitTransform(s.TargetId)`로 Transform을 얻고, `GameBootstrapper.Map.cs:271~287`과 같은 방식으로 **그 View의 `UnitData`가 살아 있고 같은 객체인지**를 확인할 수 있는 정보(원래 `UnitData` 참조 또는 확인용 콜백)를 함께 넘긴다. 해석 실패 → 추적 없이 **현재와 똑같이 스냅샷 Impact로 재생**(NET-PRESENT-003).
2. **재생:** `EffectManager.PlayAttackEffect`에 선택 인자(대상 Transform + 동일성 확인)를 추가한다. Spirit 타워 분기에서만 `VfxPoolItem`의 새 추적 모드를 켠다. Human 타워·MushroomImpact는 인자를 무시하고 기존 경로 그대로.
3. **매 프레임 갱신(VfxPoolItem 추적 모드, 아이템이 살아 있는 동안 = 풀 반환 전까지):**
   - 끝점 = 대상이 유효하면 대상 Transform 위치, 아니면 **마지막으로 알던 끝점에서 고정**(한 번 고정되면 다시 추적하지 않음 — ID 재사용 방지).
   - 시작점 = 재생 때의 source(타워 위치, Client 반전 변환 포함) 고정.
   - 회전 = source→끝점 **XZ 평면** 방향(현재 `PlayAttackEffect` 413~414행과 같은 규칙. 높이 차는 기존처럼 무시).
   - 길이 = source→끝점 **XZ 거리**. `Bolt.localScale.z = 길이`(X·Y는 authored 값 유지).
   - `Impact_Sparks`: **Bolt의 자식이면 별도 설정하지 않는다**(Bolt 스케일로 자동으로 끝점에 감) / **Bolt의 형제이면 `localPosition.z = 길이`**. 계층은 재생 1회 시점에 이름으로 찾아 판정하고, `Bolt`를 못 찾으면 추적 없이 기존처럼 재생 + 진단 기록(D-4 결정과 무관하게 안전).
   - 대상 무효 판정: Transform이 null/파괴됨, `activeInHierarchy == false`, 원래 `UnitData`가 죽었거나 그 View의 Data가 원래 객체가 아님.
4. **풀 반환 시 원상복구:** `Return`/`ResetTravelState` 경로에서 Bolt의 authored localScale, Impact_Sparks의 authored localPosition을 복원하고 추적 참조를 모두 비운다(다음 재사용에 잔여값·옛 유닛 참조가 남지 않게). authored 값은 최초 1회 캐시(`_originalLocalScale` 캐시와 같은 방식).
5. **진단(Dev 빌드·에디터만):** 기존 `AttackEffectIdentity.Record` outcome 문자열에 `Endpoint=Tracked|Untracked(사유)`, 고정 발생 시 `Endpoint=Frozen(사유)`를 덧붙인다(기존 4096 상세 예산 안에서). 새 로그 채널은 만들지 않는다.
6. **Host·Client:** 위 로직은 `HitPresentationQueue`가 이미 양쪽에서 같은 스냅샷으로 도는 표현 경로에 있으므로 각 화면이 자기 화면의 유닛 Transform을 따라간다. 서버 데이터·RPC·피해 처리 변경 없음.

**수용 기준:** 번개가 보이는 동안 끝점이 이동하는 원래 대상에 붙어 있다 / 대상이 사망·제거되면 그 자리에서 멈추고 다른 유닛으로 옮겨 가지 않는다 / 인간 타워 VFX 동작 불변 / 피해·HP·쿨다운 로그 불변 / 풀 재사용 시 이전 길이가 남지 않는다.

**검증기 보강(기존 `ValidateWriterSnapshotVfx` 또는 같은 파일의 새 private 함수, 새 메뉴 없음):** (a) 실제 RuneSpire preset 프리팹에 `Bolt`가 있고 계층 판정이 기대값과 같다(D-4 확정값으로 단언 — 2026-10-13 확정: `Impact_Sparks`가 `Bolt`의 자식), (b) 임시 대상 Transform을 이동시키면 Bolt z·회전이 따라간다, (c) 대상 무효화 후 이동해도 값이 고정된다, (d) Return 후 authored 값으로 복원된다, (e) Human 타워 재생에는 추적이 붙지 않는다. EditMode에서 `Update`/`LateUpdate` 수명이 자동으로 돌지 않는 점은 기존 fixture처럼 리플렉션 호출로 재현한다(`.claude/mistakes.md` 2026-10-10 「EditMode VFX fixture omitted EffectManager Awake lifecycle registration」 교훈 — 수명 전제부터 단언).

### 4-C. 상태 이펙트 크기·위치 맞춤 (D-1~D-3 결정 후 착수 — 2026-10-13 결정 완료: (3) 혼합 · 힐·독 같이 · 균등 배율)

#### 4-C-1. 선택지 비교 (D-1)

| 기준 | (1) 런타임 렌더러 bounds | (2) 유닛 종류별 크기 표(SO) | (3) 혼합: 에디터 측정 → 표, 런타임은 표만 |
|---|---|---|---|
| 정확도 | 실제 모델에 자동으로 맞음. 단 무기·이펙트 메시까지 bounds에 들어가 과대 측정될 수 있음 | 표 값만큼 정확(사람이 넣은 값) | 측정 도구가 bind pose·본체 렌더러만 재므로 정확, 이후 사람이 미세 조정 |
| 애니메이션 영향 | SkinnedMesh bounds가 **프레임마다 변함** → 매 프레임 쓰면 이펙트가 출렁임, 1회만 쓰면 공격 자세 등 시점에 따라 크기가 들쭉날쭉 | 없음(고정값) | 없음(고정값) |
| 모바일 비용 | bounds 계산·렌더러 순회 비용이 이펙트 수만큼 반복 | 사전 조회 1회 | 사전 조회 1회 |
| 같은 유닛이 항상 같은 모양 | 보장 안 됨 | 보장 | 보장 |
| Host/Client 일관성 | 화면마다 애니메이션 진행이 달라 미세 차이 | 양쪽 동일 | 양쪽 동일 |
| 유지 비용 | 없음 | 25종 값을 손으로 채워야 함, 새 유닛 추가 시 누락 위험 | 도구로 재측정 가능, 누락 시 기본값 + 검증기 경고 |
| Inspector 튜닝 | 불가(자동) | 가능 | 가능 |

**추천: (3) 혼합.** 이유 — 매 프레임 흔들리지 않고(결정적), 모바일 비용이 거의 없으며, 같은 유닛이 Host·Client에서 똑같이 보이고, 사용자가 Inspector에서 유닛별로 직접 고칠 수 있다. 처음 25종 값을 사람이 일일이 재지 않아도 되는 것이 (2)와의 차이다.

#### 4-C-2. 추천안(3) 기준 구현 내용 (2026-10-13 사용자가 (3)을 채택 — 이 내용대로 구현)

1. **크기 표:** 기존 `UnitEffectConfig`(ScriptableObject, `Resources/Config/UnitEffectConfig.asset`)에 UnitType별 `RecipientFit` 목록(반경, 키, 발밑 기준 위치 보정, 선택적 수동 배율)을 추가하는 것을 1안으로 한다 — 새 자산·씬 배선이 필요 없다. 별도 SO가 낫다고 판단되면 programmer가 이유와 함께 제안(설계 변경은 메인 세션 승인).
2. **에디터 측정 도구(영구 보존 → `Assets/Editor/Tools/`, WORKFLOW [5-2] 폴더 규칙):** 각 유닛 production 프리팹의 본체 렌더러를 bind pose로 측정해 표를 채우는 메뉴. 무기·이펙트 렌더러 제외 기준은 programmer가 프리팹 구조를 보고 정해 보고한다. 저장(자산 변경)은 사용자가 메뉴를 실행할 때만. 이 도구 실행이 WORKFLOW [5-2] Inspector 작업 단계가 된다.
3. **배율 계산(D-3 균등 추천 기준):** `배율 = 표의 키 ÷ 1.0`(프리팹 기준 높이). 유닛별 수동 배율이 있으면 그것을 우선. 최종 스케일 = 원본 × preset 배율 × 맞춤 배율. 위치 = pose 위치(발밑) + pose 회전 × (preset offset + 표의 발밑 보정).
4. **적용 지점:** `PlayTimedDamage`(R-4)와 `PlayBloomLoop`/`FollowBloomPose`(R-5)에서 재생 시 1회 계산해 보관, Follow는 보관값만 사용(매 프레임 재계산 없음). 대상 UnitType은 presenter의 `owner.Target.Type`을 `EffectManager.StartTimedDamageEffect`/`StartBloomEffect`에 추가 인자로 넘긴다.
5. **같은 owner refresh·Stop 동작 불변:** refresh는 재시작하지 않으므로 크기도 유지, Stop 뒤 마지막 pose 고정(기존 `Stop freezes last valid world pose.` 단언) 유지.
6. **표에 없는 유닛:** 배율 1(현재와 같은 결과) + 검증기 경고. 화상 승인 모양은 "기준 크기(반경 0.25·키 1.0) 유닛에서 배율 1"로 보존된다.
7. **검증기:** `ValidateDamageParticles`·`ValidateBloomHoTEffectBoundary`에 (a) 기준 크기 유닛 → 배율 1, (b) 큰/작은 표 값 → 기대 배율, (c) refresh 후 배율 불변, (d) Return 후 원본 스케일 복원 단언 추가. 25종 표 완비 여부 검사.

---

## 5. 변경 파일 목록 (예상 — 최종 manifest는 game-programmer 인계로 확정)

| 파일 | A | B | C |
|---|---|---|---|
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` | ● | ● | ● |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` | | | ● |
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` + `.meta` (신규, partial 분리 시) | | ● | |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` | | ● | ● |
| `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` | | ● | |
| `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs` | | | ● |
| `Assets/_Project/Scripts/Presentation/Effects/BloomHealEffectPresenter.cs` | | | ● |
| `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` | | | ●(추천안) |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` (측정 도구 실행 결과) | | | ●(추천안) |
| `Assets/Editor/Tools/<측정 도구>.cs` + `.meta` (신규) | | | ●(추천안) |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | ● | | ● |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs` | | ● | ● |

**바꾸지 않는 것:** 모든 VFX 프리팹·preset 자산(D-4 결정으로 프리팹 수정이 필요해지면 별도 승인), `TowerCombatUseCase`, `NetworkCombatController`, `AttackEffectPresentation` 구조·RPC, Stats·clip·marker, HoT/DoT 수치·틱·수명, SFX.

---

## 6. 위험 요소

| # | 위험 | 대응 |
|---|---|---|
| K-1 | (B) Client에서 서버 TargetId로 같은 유닛 View를 못 찾을 수 있음(U-3) | 해석 실패 시 현재와 동일한 스냅샷 Impact 재생으로 떨어진다. programmer가 Client ID 매핑을 코드로 확인해 인계. |
| K-2 | (B) 0.35초 안에 대상이 죽고 같은 ID가 다른 유닛에 재사용 | Transform·UnitData 참조를 재생 때 1회 고정하고 매 프레임 "같은 객체·생존"을 확인, 실패 시 영구 고정. ID로 재조회하지 않는다. |
| K-3 | (B) 풀 재사용 시 이전 Bolt 길이·Impact 위치·유닛 참조 잔존 | Return에서 authored 값 복원·참조 해제, 검증기 (d) 단언. |
| K-4 | (B) Client 화면 반전에서 source(변환된 스냅샷)와 대상 Transform(이미 뷰 좌표)의 좌표계 혼동 | 둘 다 뷰 좌표임을 programmer가 확인. 반전 Client 실기 테스트 포함. |
| K-5 | (B) 메시 파티클이 Bolt z 스케일에 기대한 길이로 늘지 않음(U-4) | 검증기는 Transform 값까지만 증명 — **보이는 길이는 사용자 육안 확인**. |
| K-6 | (B) 프리팹 계층 해석 오류(D-4)로 Impact 위치 이중 적용 | 계층을 이름·부모로 판정하는 분기 + 검증기 계층 단언. |
| K-7 | (B) 규칙 12 문구와의 해석 충돌(D-5) | 사용자 확인 전 규칙 문서 수정 없음. → 2026-10-13 사용자가 해석 a(표현 전용 추적 허용) 확정. 규칙 12 문구 보강은 구현·테스트 후 문서 반영 항목. |
| K-8 | (C) Hierarchy 스케일링에서 비균등 배율 시 파티클 왜곡 | D-3 균등 추천. |
| K-9 | (C) VisualRoot가 발밑이 아니거나(UnitYOffset) 유닛 루트 스케일≠1(U-5) → 위치 어긋남·이중 스케일 | 착수 전 programmer 실측 보고, 표의 발밑 보정값으로 흡수. |
| K-10 | (C) 측정 도구가 무기·이펙트 렌더러까지 재서 과대 크기 | 측정 대상 렌더러 기준을 programmer가 보고, 사용자가 Inspector에서 조정 가능. |
| K-11 | (C) 새 유닛 추가 시 표 누락 | 기본 배율 1 + 검증기 경고. |
| K-12 | (A) 0PS·malformed 프리팹이 통과 | 하한 0 거부 + 시스템별 계약 유지 + 검증기 추가 단언. |
| K-13 | 공통: EditMode fixture가 Awake/Update 수명을 자동으로 돌리지 않아 거짓 RED/GREEN | 기존 fixture처럼 수명 전제부터 단언(mistakes 2026-10-10). |

---

## 7. 에이전트 분담과 검증 순서 (WORKFLOW [5] — 메인 세션이 조율)

3개 이상 파일·설계 결정이 함께 있으므로 **메인 세션이 직접 조율·결과 검토**한다.

1. **사용자:** 이 Plan 승인 + D-1~D-5 답변(C는 D-1~D-3 전에는 착수 안 함). → **완료 (2026-10-13):** Plan 승인, D-1~D-5 전부 추천안 채택.
2. **메인 세션:** U-1(작업 트리=`88f60e38`), U-2(프리팹 계층) 확인 → D-4 확정 자료를 사용자에게 제시. → **완료 (2026-10-13):** commit `88f60e38`에서 `Bolt` localScale (1,1,3.5)·자식 `Impact_Sparks` localPosition (0,0,1) 확인.
3. **game-programmer:** A → B → C 순서로 구현(A가 가장 작고 힐 이펙트를 바로 살린다). 착수 전 U-3·U-5·U-6을 코드/에디터로 확인해 인계. 구현 후 변경 파일 manifest·정적 컴파일 결과 인계. 전달 필수 컨텍스트: 이 Plan, Research, 관련 규칙(위 3절), CLAUDE.md 아키텍처 규칙, `.claude/MEMORY.md`.
4. **메인 세션:** 실제 diff 검토(표현 전용인지, 서버·수치 경로 무변경인지), 정적 결과 확인, Unity 메뉴 실행.
5. **(C 추천안 선택 시) 사용자:** 측정 도구 메뉴 실행(WORKFLOW [5-2]) → 표 값 확인.
6. **사용자 실기(WORKFLOW [6]):** 에디터 싱글 + Host/Client(에디터 Host·기기 Client, 역할 바꿔 1회 이상).
7. **qa-tester / Testcase.md:** WORKFLOW 규정상 **사용자가 명시적으로 요청할 때만** 진행한다.
8. **document-manager:** [7] 사용자 확인 후 문서 반영(규칙 34 상태 블록의 5PS 표기, D-5 확정 시 규칙 12 상태 블록, PROJECT_STATUS·ROADMAP·WORK_HISTORY 등). `VFXSFXList.md`는 사용자 처리.

---

## 8. 범위 밖 (이번에 하지 않음)

- Tank/CannonCart VFX 간헐 누락 진단
- InfernoSpirit 공격 타이밍(1.5초 여부)
- 모든 SFX 추가(타워 발사·상태 이펙트·힐)
- `VFXSFXList.md` 변경(사용자 별도 처리)
- 프리팹·preset 자산 수정, 레거시 `PlayTowerAttackVfx` 정리, TimedDamage 계약 강화(Research 6절)
- Human 타워 번개/총구 추적 — Human은 현행 유지

---

## 9. 변경 후 점검

**자동(메인 세션 / programmer):**
- 정적 컴파일 Runtime·Editor·Development·Release 4종 errors 0(경고 수는 기존 대비 보고).
- Unity: 기존 Unit Action 자기 검증 메뉴 전체 PASS(A·B·C 단언 포함) 및 `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit` PASS, Console errors 0.
- 서버·수치 무변경 확인: `TowerCombatUseCase`, `NetworkCombatController`, `UnitCombatUseCase`, Stats/config 자산이 diff에 없는지.

**사용자 실기(각 항목은 그 조건에서 실제로 눈으로 볼 수 있는 것만 적었다):**
- A: BloomFairy가 다친 아군을 회복할 때 대상 유닛에 힐 이펙트가 보이고, 힐이 끝나면 이펙트가 자연스럽게 사라진다(에디터 싱글 + Host 화면 + Client 화면).
- B: 정령 타워 사거리 안을 **걸어서 지나가는** 적을 타워가 칠 때, 번개 끝이 그 적에게 붙어 있다(Host 화면·Client 화면 각각). 같은 장면에서 인간 타워 이펙트는 이전과 같다.
- B: 번개 한 방에 죽는 체력의 적에게서는 번개 끝이 다른 유닛으로 튀지 않는다.
- C(승인 시): 작은 유닛·큰 유닛 각각에 화상(InfernoSpirit)·독(MushroomBomber)·힐(BloomFairy)이 걸렸을 때 이펙트가 유닛 크기에 맞고 발밑에 붙어 있다. 같은 유닛이 Host·Client 화면에서 같은 크기다.
- 공통: 피해 숫자·HP바·힐 텍스트 시점이 이전과 같다.

**문서:** [7] 확인 후 document-manager가 반영하고 `python3 Tools/check_docs.py` 0건 확인.

---

## 10. 재작업 R2 — 1차 테스트 반영 (2026-10-13 추가, WORKFLOW [6] 재작업 — 0~9절은 원 계획으로 보존)

### 10-0. 쉬운 설명

1차 실기 결과(`Research.md` 8절)를 반영해 네 갈래로 추가 수정한다. 코드로만 고칠 수 있는 두 가지(**차지 이펙트가 유닛 사망 후에도 남는 버그**, **정령 타워 번개를 크리스털에서 유닛 가슴으로 꽂히게 하기**)는 game-programmer가 고친다. 숫자만 바꾸면 되는 **화상·힐 위치·크기**는 사용자가 Inspector에서 직접 맞추고, **독 표시 모양**은 사용자가 Unity AI로 프리팹을 다시 만든다. 이번에도 피해·회복·HP·쿨다운·타겟 선택 같은 게임 규칙은 바꾸지 않는다.

> **사용자 승인: 2026-10-13.** 사용자 원문: "기존 문서에 테스트 결과 반영하고 추가 수정 작업 진행해야해". 이 승인은 R2 착수 허가이며 구현 완료·검사 통과·실기 수용을 뜻하지 않는다. 버섯 착탄(Research 8-2 T-3)은 사용자 승인으로 이번 재작업 대상이 아니다.

### 10-1. 🔴 기존 로직 제거·비활성화 항목 (WORKFLOW [4])

| # | 대상 | 처리 | 안전한 근거 |
|---|---|---|---|
| R2-X1 | `VfxPoolItem.TowerBolt.cs` `ApplyTowerBoltPose`의 수평 조준 `direction.y = 0f;`(약 176행)·수평 길이 `span.y = 0f;`(약 182행) | 삭제하지 않고 **주석 비활성화** 후 3D 계산으로 대체 | 표현 전용 값이며 서버 피해·타겟 판정과 무관(Buildings 「방어 타워 시스템」 규칙 9). Spirit 타워 추적 모드에서만 쓰이는 경로라 Human 타워에 영향이 없다. |
| R2-X2 | `VfxPoolItem.TowerBolt.cs` `TryReadEndpoint`의 끝점 = 대상 루트(발밑) 위치(약 70행) | 주석 비활성화 후 "발밑 + 가슴 높이"로 대체 | 위와 같음. 위치 원천(Projector/Pose)과 동일 객체·생존 확인 로직은 그대로 유지한다. |

- R2-1은 **추가**만 한다(제거 항목 없음 — 예정). programmer가 기존 정지 로직을 바꿔야 한다고 판단하면 착수 전에 메인 세션에 보고한다.
- 주석 처리분 최종 삭제 시점은 0~9절과 같다([6] 통과 후, [7] 전).

### 10-2. 수정 항목

| ID | 종류 · 담당 | 내용 | 근거 규칙 |
|---|---|---|---|
| **R2-1** | 코드 · game-programmer | **차지 VFX 사망 정지.** 차지 VFX를 재생 중인 유닛이 죽거나(또는 제거되면) 그 차지 VFX를 멈추고 풀에 반환한다. 적용 대상은 차지형 공격 VFX를 쓰는 **모든 유닛** — 최소 FoxMagician(`vfx_foxmagician_charge`, `EffectPreset_FoxMagician_Attack`)·InfernoSpirit(`vfx_infernospirit_charge`, `EffectPreset_InfernoSpirit_Attack`)이며, **programmer가 나머지 유닛을 코드·preset에서 검색해 목록을 인계한다.** 표현 전용 — 게임플레이 변경 없음. | Units 규칙 17(FoxMagician 예외: 1.00초 marker는 charge VFX를 시작하는 **표현** marker, 피해는 서버 예약) · Units 규칙 19(공격자 사망은 미승인 미래 marker 허가를 닫는 입력이지만 **이미 서버가 확정한 결과를 취소하지 않는다**) · `GameSystemRules_UnitCombatSynchronization.md` NET-AUTH-001(VFX는 결과 입력이 아님). → 차지 VFX 정지가 서버 확정 결과·피해·착탄/HP 텍스트 표현을 취소하거나 바꾸면 안 된다. 차지 정지 자체를 정한 조항은 **없다**. |
| **R2-2** | 코드 · game-programmer | **정령 타워 번개 크리스털→가슴.** ① 시작점 = 타워 위치 + preset `Local Position Offset`(크리스털 높이를 Inspector에서 조정 가능). programmer가 RuneSpire 건물 프리팹의 크리스털/꼭대기 높이를 실측해 **초깃값을 preset에 설정**하고 값과 측정 근거를 인계한다. ② 조준 방향과 길이는 **3D 전체**로 계산해 대상의 가슴 지점을 향한다. ③ 가슴 지점 = 대상 발밑 + 가슴 높이. 가슴 높이는 C에서 만든 `UnitEffectConfig` 「상태 이펙트 크기 맞춤」 표의 키(height)에서 유도한다(예: 약 0.6 × 키). 표에 항목이 없으면 안전한 기본값으로 떨어진다(값은 programmer가 제안). ④ 4-B의 추적·고정(사망/교체 시 마지막 끝점 고정, ID로 재조회하지 않음) 동작은 **그대로 유지**. ⑤ Human 타워는 **변경 없음**. | Buildings 「방어 타워 시스템」 규칙 9 · 규칙 12(D-5 해석 a 유지 — 원래 대상의 화면 표현 추적만, 현재 target 재조회 없음) · NET-AUTH-001 · NET-PRESENT-003(대상 해석 실패 시 스냅샷 Impact로 재생) |
| **R2-3** | 데이터 · 사용자(Inspector) | **화상·힐 위치·크기 조정.** `EffectPreset_InfernoSpirit_Burn.asset` / `EffectPreset_BloomFairy_Heal.asset`의 `Local Position Offset` Y·`Scale Multiplier`, 그리고 유닛별 `UnitEffectConfig.asset` → 「상태 이펙트 크기 맞춤」의 `manualScale`·`footOffset`. **코드 변경 없음.** | 크기·위치를 정한 규칙 조항 **없음**(0~9절 3절 C 행과 같음). 수명·수치·갱신 정책 불변(Units 규칙 34·40·42). |
| **R2-4** | 프리팹 · 사용자(Unity AI) | **독 표시 재설계.** 퍼지는 독 연출이 아니라 "이 유닛이 중독됐다"를 보여 주는, **유닛 중심에 모인** 표시로 `vfx_mushroombomber_poison` 계열 프리팹을 다시 만든다(원인: Research 8-2 T-4의 `Poison_Bubbles` 바깥 방출). 기존 TimedDamage 재생 계약(전 시스템 loop ON·stopAction None 등, Research 4-2)은 지켜야 재생된다. | 모양을 정한 규칙 조항 **없음**. 독 DoT 수치·수명 불변(Units 규칙 34·41). |

### 10-3. R2-1 · R2-2 구현 시 지켜야 할 것

- **R2-1:** 정지는 해당 유닛이 소유한 차지 VFX에만 적용한다(같은 preset을 쓰는 다른 유닛 인스턴스를 멈추지 않는다). 이미 끝나 반환된 풀 아이템을 다시 반환하지 않는다(중복 반환 방지). 사망 VFX·피격 VFX·착탄 VFX 등 다른 표현 채널은 건드리지 않는다(Units 규칙 19 본문: 사망 VFX는 별도 수명). Host·Client 각 화면에서 같은 사망 사건으로 정지한다.
- **R2-1 확인 사항:** 2026-09-22 InfernoSpirit focused PASS에 "marker 전 Stop 시 source VFX 보존" 동작이 포함돼 있다(document-manager 메모리 기록). 이번 정지는 **사망·제거**에만 적용하고 일반 Stop(상태 전환)에는 적용하지 않는지를 programmer가 코드로 확인해 인계한다.
- **R2-2:** 끝점 높이 계산은 각 화면 로컬 표현이며 서버 데이터·RPC·스냅샷 구조를 바꾸지 않는다. Client 화면 반전 시 시작점·가슴 지점이 같은 좌표계인지 확인(K-4와 같음). 기존 검증기 (b)~(e) 단언은 3D 기준으로 갱신하고, 가슴 높이·표 미등록 기본값 단언을 추가한다.

### 10-4. 변경 파일 (예상 — 최종 manifest는 game-programmer 인계로 확정)

| 파일 | R2-1 | R2-2 | R2-3 | R2-4 |
|---|---|---|---|---|
| `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` | | ● | | |
| `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` / `HitPresentationQueue.cs` | ●(필요 시) | ●(필요 시) | | |
| 차지 VFX 재생·유닛 사망 처리 경로(programmer가 검색으로 확정, 예: `UnitView` 계열) | ● | | | |
| `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset` (`Local Position Offset` 초깃값) | | ● | | |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation*.cs` | ● | ● | | |
| `EffectPreset_InfernoSpirit_Burn.asset` · `EffectPreset_BloomFairy_Heal.asset` · `UnitEffectConfig.asset` | | | ●(사용자) | |
| 독 VFX 프리팹 | | | | ●(사용자) |

- 0~9절의 「바꾸지 않는 것」 중 "모든 VFX 프리팹·preset 자산"은 R2에서 **`EffectPreset_RuneSpire_Attack.asset`의 offset 초깃값(R2-2)과 사용자가 직접 하는 R2-3·R2-4에 한해** 예외가 된다. 그 밖의 서버·수치·RPC 무변경 원칙은 그대로다.

### 10-5. 범위 밖 (변경 없음)

8절 범위 밖 목록을 그대로 유지한다(Tank/CannonCart VFX 누락 진단, InfernoSpirit 공격 타이밍, 모든 SFX, `VFXSFXList.md`, 레거시 `PlayTowerAttackVfx` 정리, TimedDamage 계약 강화, Human 타워 추적). 버섯 착탄 VFX는 승인됐으므로 손대지 않는다.

### 10-6. 진행 순서와 점검

1. game-programmer: R2-1(차지 VFX 대상 유닛 검색 결과 포함) → R2-2 구현, 변경 manifest·정적 컴파일 4종 결과·RuneSpire 크리스털 높이 실측값 인계.
2. 메인 세션: diff 검토(표현 전용, 서버·수치 경로 무변경), Unity 자기 검증 메뉴 실행.
3. 사용자: R2-3 Inspector 조정, R2-4 독 프리팹 재제작(R2-1·R2-2와 병행 가능).
4. 사용자 2차 실기(WORKFLOW [6]) — 실제로 눈으로 확인 가능한 항목:
   - FoxMagician·InfernoSpirit(및 programmer가 찾은 다른 차지형 유닛)이 기를 모으는 중에 죽으면 차지 이펙트가 그 자리에서 사라진다. 죽지 않은 경우 차지·공격 이펙트는 이전과 같다.
   - 정령 타워 번개가 타워 꼭대기 크리스털에서 나와 움직이는 적의 가슴 높이에 꽂힌다(Host·Client 화면 각각). 인간 타워 이펙트는 이전과 같다.
   - 화상이 유닛 아래쪽에 알맞은 크기로, 힐이 유닛 위로 지나치게 솟지 않게 보인다.
   - 독 표시가 주변 칸으로 퍼지지 않고 중독된 유닛에 모여 있다.
   - 공통: 피해 숫자·HP바·힐 텍스트 시점이 이전과 같다.

---

## 12. 재작업 R3 — 측정 도구 스케일 버그 (2026-10-13 추가, WORKFLOW [6] 재작업 — 0~10절은 보존)

### 12-0. 쉬운 설명

2차 실기(`Research.md` 9절)에서 차지 이펙트 정지(R2-1)와 번개 크리스털 시작점(R2-2 ①)은 확인됐다. 하지만 독·힐이 유닛보다 너무 크고 높게 보였고, 원인은 **유닛 크기 표를 채우는 에디터 측정 도구가 사람형 유닛의 크기를 약 5배 크게 잰 것**이었다. 이 표가 상태 이펙트 크기와 정령 타워 번개의 가슴 높이를 함께 정하므로, **도구를 먼저 고치고 표를 다시 채운 뒤에** 나머지를 테스트한다. 게임 규칙·서버·런타임 코드는 바꾸지 않는다.

> **사용자 승인: 2026-10-13.** 사용자 원문: "측정 도구를 수정해야 나머지가 정상적으로 테스트될거같아 / 문서 정리도 같이 진행해". 이 승인은 R3 착수 허가이며 수정 완료·재측정·실기 수용을 뜻하지 않는다.

### 12-1. 🔴 기존 로직 제거·비활성화 항목 (WORKFLOW [4])

- 측정 도구 안에서 잘못된 크기 계산을 바꿀 때 옛 계산은 **삭제하지 않고 주석 비활성화**한다(0~10절과 같은 원칙). 무엇을 비활성화했는지는 game-programmer 인계로 이 절에 추가한다.
- 런타임 코드(`UnitEffectConfig`·`VfxPoolItem`·`EffectManager`)는 이번 재작업 대상이 아니다 — 표를 읽는 쪽은 맞게 동작하고, 표에 들어간 값이 틀렸기 때문이다(Research 9-3).

### 12-2. 수정 항목

| ID | 종류 · 담당 | 내용 | 근거 규칙 |
|---|---|---|---|
| **R3-1** | 코드(에디터 도구) · game-programmer | `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs`가 **모든 렌더러를 하나의 일관된 월드 공간에서** 재도록 고친다. 스킨드 메시도 메시 렌더러도 **유닛 루트와 렌더러 사이의 중간 Transform 배율(예: Pistoleer `Unit_Pistoleer_Blue_Mesh` 0.2)** 을 포함해 계산해야 한다. 결과 키·반경·발밑 보정은 유닛 루트(VisualRoot) 기준 값이어야 런타임 계산(4-C-2 3번, R2-2 ③)과 맞는다. | 크기 측정을 정한 규칙 조항 **없음**(3절 C 행과 같음). 표현 전용·서버 무관(NET-AUTH-001). |
| **R3-2** | 코드(에디터 도구) · game-programmer | **상식 검사 경고** 추가: 측정한 키가 비정상적으로 크거나 작으면(범위는 programmer가 근거와 함께 제안) Console에 경고를 남긴다. 저장은 막지 않는다(사용자가 보고 판단). | 같음 |
| **R3-3** | 에디터 실행 · 사용자 | 도구 수정 후 사용자가 메뉴 `Hexiege/Effects/Measure Unit Recipient Fit`을 **다시 실행**해 `UnitEffectConfig.asset`의 표를 다시 채운다(WORKFLOW [5-2]). 이전 값(커밋 `10a38cc0`)은 git 이력에 남는다. | 같음 |
| **R3-4** | 참고 · 사용자 | **임시 우회:** 도구 수정 전에 테스트해야 하면 `UnitEffectConfig.asset` → 「상태 이펙트 크기 맞춤」의 유닛별 `manualScale`로 크기를 직접 덮어쓸 수 있다(D-3: 수동 배율 우선). 단 **정령 타워 가슴 높이도 이 균등 배율에서 나오므로**(키 × 0.6) `manualScale`을 넣으면 그 유닛의 가슴 높이도 함께 바뀐다 — 우회는 근본 수정이 아니다. | 같음 |

### 12-3. 지켜야 할 것

- 도구는 **에디터 전용**이며 런타임 빌드·서버·RPC·수치에 영향이 없다. 자산 저장은 사용자가 메뉴를 실행할 때만 한다(4-C-2 2번 원칙 유지).
- 측정 대상 렌더러 선정(무기·이펙트 제외 기준)은 기존 결정을 유지한다. 바꿔야 하면 programmer가 이유와 함께 보고한다.
- 사용자가 이미 넣은 `manualScale`·`footOffset` 값이 있으면 재측정이 그 값을 덮어쓰는지 programmer가 확인해 인계한다(덮어쓰면 사용자에게 먼저 알린다).

### 12-4. 변경 파일 (예상 — 최종 manifest는 game-programmer 인계로 확정)

| 파일 | R3-1 | R3-2 | R3-3 |
|---|---|---|---|
| `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` | ● | ● | |
| `Assets/_Project/Resources/Config/UnitEffectConfig.asset` (재측정 결과) | | | ●(사용자 실행) |
| 검증기(필요 시, 새 메뉴 없음) | ●(programmer 판단) | | |

### 12-5. 진행 순서와 점검

1. game-programmer: R3-1·R3-2 구현, 변경 manifest·정적 컴파일 결과, Pistoleer·MushroomBomber·Tank·CannonCart 등 대표 유닛의 **수정 후 예상 측정값** 인계(Pistoleer는 화면 키 약 0.34 근처가 기대된다 — Research 9-3).
2. 메인 세션: diff 검토(에디터 전용·런타임 무변경).
3. 사용자: 메뉴 재실행(R3-3) → 표 값 확인.
4. 사용자 3차 실기(WORKFLOW [6]) — Research 9-4의 미테스트 3건: 정령 타워 번개가 움직이는 적의 가슴 높이에 꽂히는가 / 화상·힐 위치·크기(R2-3 조정 포함) / 독이 유닛에 알맞은 크기로 붙는가. 공통: 피해 숫자·HP바·힐 텍스트 시점이 이전과 같다.

### 12-6. 같은 승인으로 함께 한 문서 정리 (document-manager, 2026-10-13)

사용자 원문 "문서 정리도 같이 진행해"에 따라 메인 세션이 아래를 document-manager에게 맡겼다. 0~10절의 "`VFXSFXList.md`는 사용자 처리"(7절 8번·8절)와 "규칙 12 문구 보강은 구현·테스트 후"(2절 D-5)는 **이 승인으로 앞당겨 해제**됐다 — 원문은 당시 계획으로 보존한다.

- `Assets/_Project/Docs/Assets/VFXSFXList.md`: BloomFairy heal(`_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md`)·MushroomBomber 분할(`_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md`) 두 Plan의 최종 모습과 현재 연결 상태 반영.
- `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12: D-5 해석 a(원래 맞은 유닛의 화면 표현 추적만 허용)·크리스털 시작·가슴 높이 조준을 현재 코드(`VfxPoolItem.TowerBolt.cs`)대로 덧붙임. **가슴 높이 조준의 실기 수용은 R3 뒤**라는 상태를 함께 적었다.
- `GameSystemRules_Units.md` 규칙 27(MushroomBomber 클립 이벤트 현재 상태)·규칙 31(EffectPreset 「Stop When Caster Dies」) 덧붙임.

---

## 11. 계획 변경 이력

| 날짜 | 변경 | 이유 |
|---|---|---|
| 2026-10-13 | 10절 「재작업 R2 — 1차 테스트 반영」 추가(R2-1~R2-4) | 1차 사용자 실기(Research 8절)에서 ① 차지 VFX가 유닛 사망 후에도 남는 **새 결함**이 발견됐고, ② 정령 타워 번개는 4-B 원 설계("높이 차 무시, XZ 평면만")대로 구현돼 발밑을 수평으로 겨누므로 사용자가 원하는 "크리스털→가슴" 모양을 preset 오프셋만으로는 만들 수 없음이 확인됐다(Research 8-3) — 그래서 4-B의 수평 조준 설계를 3D 조준으로 바꾼다. ③ 화상·힐은 코드가 아닌 값 조정으로, ④ 독은 사용자 의도("중독 표시")에 맞는 프리팹 재제작으로 해결하기로 했다. 사용자 승인 원문: "기존 문서에 테스트 결과 반영하고 추가 수정 작업 진행해야해". |
| 2026-10-13 | 12절 「재작업 R3 — 측정 도구 스케일 버그」 추가(R3-1~R3-4) | 2차 사용자 실기(Research 9절): R2-1 차지 정지·R2-2 크리스털 시작점은 PASS. 그러나 독(재제작 프리팹 `10a38cc0`)·힐이 유닛보다 너무 크고 높게 보였고, 메인 세션 진단으로 측정 도구가 채운 크기 표가 스킨드 메시 유닛에서 약 5배 큼(Pistoleer 키 1.71, 중간 Transform 배율 0.2 미반영)이 확인됐다. 이 표가 상태 이펙트 배율(키 ÷ 1.0)과 번개 가슴 높이(키 × 0.6)를 함께 정하므로 도구를 먼저 고친다. 사용자 승인 원문: "측정 도구를 수정해야 나머지가 정상적으로 테스트될거같아 / 문서 정리도 같이 진행해". 절 번호는 기존 11절(이 표) 뒤 번호를 이어 12로 했으며 위치는 이 표 앞이다. |
| 2026-10-15 | 13절 「최종 결과 — 여기까지의 기록」 추가(문서 끝) | 3차 사용자 실기(Research 10절) 결과 기록. 계획 변경이 아니라 결과 기록이며, 남은 결함 1건(타워 번개 사망 시 끝점 어긋남)은 별도 승인 대상으로 남겼다. 사용자 지시 원문: "우선 여기까지 작업한 것을 기록하자". |
| 2026-10-19 | 14절 「재작업 R4 — 검증 해시 줄바꿈 문제」 추가(문서 끝) | main(`0978d908`) 병합 뒤 Edit Mode 전체 회귀 검증에서 `TorrentWaveProductionValidation`이 "source VFX hash changed"로 실패(Research 11절). 원인은 사용자 PC `core.autocrlf=true`로 디스크 파일이 CRLF가 되어 원본 바이트 해시가 LF 기준 상수와 달라진 것 — 병합·VFX 작업 결함이 아니다. 병합 전 검사를 끝내려면 이 검증을 줄바꿈에 무관하게 만들어야 하므로 이 브랜치에서 고친다. 사용자 승인 원문: "1번대로 진행하자" · "가로 진행해". |

---

## 13. 최종 결과 — 여기까지의 기록 (2026-10-15 추가 — 0~12절은 계획 원문으로 보존)

### 13-0. 쉬운 설명

세 번의 실기를 거쳐 이 작업에서 고치려던 것은 **하나만 빼고 모두 사용자가 확인했다.** 꽃요정 힐 이펙트가 다시 나오고, 정령 타워 번개는 크리스털에서 나와 움직이는 적의 가슴에 꽂히며, 화상·독·힐은 유닛 크기에 맞춰 붙고 프리셋 값으로 크기·높이를 조정할 수 있다. 차지 이펙트는 시전자가 죽으면 사라진다. **남은 것은 「번개가 맞는 순간 대상이 죽으면 끝점이 살짝 어긋나는」 결함 하나**이고, 원인은 아직 확인하지 않았다(가설만 있다). 상태 이펙트 값 다듬기는 사용자가 계속하는 데이터 조정이다. 사용자 지시(2026-10-15): "우선 여기까지 작업한 것을 기록하자" — 이 절은 그 시점의 기록이다.

### 13-1. 항목별 최종 상태

| 항목 | 커밋 | 최종 상태 | 근거 |
|---|---|---|---|
| A. Bloom 힐 개수 계약(1개 이상 + 시스템별 계약) | `d3f9abb1` | ✅ **사용자 실기 PASS** (PS 4개 프리팹 재생) | Research 10-2 |
| B. RuneSpire 번개 원래 대상 추적 | `d3f9abb1` | ✅ 살아 있는 대상 PASS · ⚠️ **맞는 순간 사망 시 끝점 어긋남 OPEN** | Research 10-2 · 10-4 |
| C. 상태 이펙트 대상 크기 맞춤 | `d3f9abb1` · 재측정 표·프리셋 `91371e55` | ✅ R3 재측정 뒤 **의도대로 조정됨** (사용자 화면 확인) · 값 미세 조정 진행 중 | Research 10-2 · 10-3 |
| R2-1 차지 VFX 시전자 사망 정지 | `a840a152` | ✅ **사용자 실기 PASS** (2차·3차) | Research 9-2 · 10-2 |
| R2-2 크리스털→가슴 3D 조준 | `a840a152` | ✅ 크리스털 시작·가슴 높이 PASS · 사망 시 예외는 B 행과 같은 결함 | Research 10-2 |
| R2-3 화상·힐 값 조정 | (사용자 데이터) | ⏳ **진행 중** — 코드 항목 아님 | Research 10-5 |
| R2-4 독 프리팹 재제작 | `10a38cc0` | ✅ **사용자 승인** | Research 10-2 |
| R3 측정 도구 스케일 버그 | `84b6ff2a` | ✅ **수정 · 재측정 · 실기 확인** | Research 10-2 |
| T-3 버섯 착탄 | — (변경 없음) | ✅ **사용자 승인** (1차) | Research 8-2 |

### 13-2. 미해결과 남은 항목

1. 🔴 **OPEN — 타워 번개 사망 시 끝점 어긋남.** 코드 사실·가설(⚠️ 미검증)·다음 단계 제안은 Research 10-4가 단일 소스다. 요지: 대상이 이미 죽은 상태로 재생이 시작되면 추적을 켜지 않고 서버 스냅샷 Impact + 가슴 높이를 끝점으로 쓴다(`HitPresentationQueue.cs` 278행 → `VfxPoolItem.TowerBolt.cs` 172행). 이때 가슴 높이는 크기 표가 아니라 0.6m 고정값이다 — 다음 작업 인계는 아래 13-4. *(2026-10-15 정정: 첫 판은 이 판정 자리를 `VfxPoolItem.TowerBolt.cs` 76행으로 적었다 — 76행은 추적 도중 사망 확인이다)* 제안된 수정(화면 위치를 쓰는 방향)은 `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 2026-10-13 블록 「추적 불가로 시작할 때」 문구와 맞닿으므로 **규칙 변경 전에 사용자 승인**이 필요하다.
2. ⏳ **데이터 조정 계속** — 화상·독·힐 프리셋 값(사용자, 반복 테스트). 현재 값은 커밋 `91371e55`에 있다: 재측정 표(Pistoleer 키 0.342 · 최대 키 약 0.53 · 최대 반경 약 0.41 · `manualScale` 없음), Heal·Poison Scale Multiplier 2, Burn 1, 오프셋 모두 0(Research 10-3). *(2026-10-15 정정: 첫 판의 「`84b6ff2a`에는 없다 — 사용자 PC의 커밋되지 않은 작업으로 보인다」는 사용자가 `91371e55`로 올려 해소)*
3. **주석 비활성화한 옛 로직 — 지금은 남겨 둔다(사용자 결정, 2026-10-15: 작업이 끝나지 않았으므로).** 최종 삭제는 아래 13-4의 RuneSpire 번개 사망 시 끝점 결함이 사용자 테스트를 통과한 뒤에만 한다(WORKFLOW [4] 「[6] 통과 후 · [7] 전」). 현재 남아 있는 자리 [직접]: R-1 `VfxPoolItem.cs` 164행 · R-5 `VfxPoolItem.cs` 210행 · R-4 `VfxPoolItem.TimedDamage.cs` 42행 · R-3 `RunUnitActionSelfValidation.cs` 1385행 · R2-X1 `VfxPoolItem.TowerBolt.cs` 204행 · R3 `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` 332행. (R2-X2 끝점 계산은 주석 블록이 아니라 호출 쪽에 가슴 높이를 더하는 방식으로 바뀌었다 — 같은 파일 165·172·188행.)
4. **이 작업의 범위 밖으로 남은 VFX 후속**(8절·10-5절 범위 밖 목록에서 이어짐): MushroomBomber 트레이서 **포물선 궤적**(현재 `TracerProjectile.cs` 161행 직선 `Lerp`, 사용자가 이전 작업에서 나중으로 미룸 — `_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md` 5절 4번) · 새 VFX의 **SFX**(상태 이펙트·힐·타워 착탄은 `EffectManager.cs`의 `Sound15:` 주석대로 미제작) · `VFXSFXList.md`의 **미연결·제작 예정 VFX**(같은 문서 「2026-10-14 실측 연결 상태」 블록이 단일 소스). 모두 `ROADMAP.md`에 등록했다.
5. **`Testcase.md` 없음** — TC 작성을 요청받지 않아(WORKFLOW [5-1]) 만들지 않았다. WORKFLOW [8](Testcase 결과 갱신)은 해당 없음이며 실기 결과는 Research 8~10절과 이 절이 담는다.

### 13-3. 변경 파일 리스트업 (WORKFLOW [12] — `git diff --name-status 88f60e38..HEAD` + 2026-10-15 문서 반영분)

**[추가]**
- `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` (+ `.meta`)
- `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.CasterOwned.cs` (+ `.meta`)
- `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` (+ `.meta`)

**[수정] 코드**
- `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`
- `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs`
- `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`
- `Assets/_Project/Scripts/Presentation/Effects/EffectPreset.cs`
- `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs`
- `Assets/_Project/Scripts/Presentation/Effects/TimedDamageEffectPresenter.cs`
- `Assets/_Project/Scripts/Presentation/Effects/BloomHealEffectPresenter.cs`
- `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.VfxConnections.cs`

**[수정] 프리팹·에셋**
- `Assets/_Project/Prefabs/VFX/Units/vfx_mushroombomber_poison.prefab` (사용자 재제작, `10a38cc0`)
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_FoxMagician_Attack.asset` (Stop When Caster Dies)
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_InfernoSpirit_Attack.asset` (Stop When Caster Dies)
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_RuneSpire_Attack.asset` (위치 보정 Y 0.65)
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset` (`10a38cc0` 첫 측정 표 → `91371e55` 재측정 표, 사용자)
- `Assets/_Project/Resources/Config/EffectPresets/EffectPreset_BloomFairy_Heal.asset` · `EffectPreset_MushroomBomber_Poison.asset` (Scale Multiplier 2, 사용자, `91371e55`)
- *(2026-10-15 정정: 첫 판은 `88f60e38..84b6ff2a` 기준이라 `91371e55`의 사용자 데이터 커밋이 빠져 있었다. 이 목록은 이제 `88f60e38..91371e55` 기준이며, 그 커밋의 로그 `.meta` 2개는 아래 작업 문서 항목에 더했다)*

**[수정] 상시 문서**
- `Assets/_Project/Docs/Assets/VFXSFXList.md` (2026-10-15 2차: VineTower 공격 VFX 계획 블록)
- `Assets/_Project/Docs/Assets/VFXSFXGuide.md` (2026-10-15 2차: 「Unity AI 프롬프트 작성 규칙 — 코드 계약」 추가)
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Units.md`
- `Assets/_Project/Docs/History/ImplementationVerificationHistory.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`

**[추가·수정] 에이전트 메모리·기록 (`.claude/`)**
- 추가: `.claude/agent-memory/game-programmer/vfx-presentation.md`
- 수정: `.claude/MEMORY.md` · `.claude/mistakes.md` · `.claude/agent-memory/_baseline.json` · `.claude/agent-memory/game-programmer/MEMORY.md` · `.claude/agent-memory/game-programmer/logging.md` · `.claude/agent-memory/document-manager/MEMORY.md` · `.claude/agent-memory/qa-tester/patterns.md` · `.claude/agent-memory/asset-prompt-crafter/MEMORY.md`

**[작업 문서 — 별도]**
- 추가: `Assets/_Project/Docs/_Tasks/2026-10-13/13_53_vfx-code-followups/Research.md` · `Plan.md` (+ 각 `.meta`, 폴더 `.meta`)
- 수정: `Assets/_Project/Docs/_Tasks/2026-10-08/17_09_bloomfairy-heal-vfx-naming/Plan.md` · `Assets/_Project/Docs/_Tasks/2026-10-09/08_12_mushroombomber-vfx-split/Plan.md` (VFXSFXList 정리 때 상단 메모 추가)
- 추가(로그 `.meta`): `Assets/_Project/Docs/_Logs/_editor/2026-10-13/RuntimeLog.txt.meta` · `Assets/_Project/Docs/_Logs/_editor/2026-10-14.meta` · `Assets/_Project/Docs/_Logs/_editor/2026-10-14/RuntimeLog.txt.meta` · `Assets/_Project/Docs/_Logs/_editor/2026-10-15.meta` · `Assets/_Project/Docs/_Logs/_editor/2026-10-15/RuntimeLog.txt.meta` (뒤 두 개는 `91371e55`)

### 13-4. 🔴 다음 작업 인계 — RuneSpire 번개: 맞는 순간 대상이 죽으면 끝점이 살짝 어긋난다 (OPEN, 2026-10-15 사용자 지시로 추가)

이 블록만 읽고 다음 작업을 시작할 수 있게 정리했다. 분석의 원본은 Research 10-4, 같은 내용의 programmer용 요약은 `.claude/agent-memory/game-programmer/vfx-presentation.md` §4-1, 로드맵 행은 `ROADMAP.md` 「VFX 후속 ①」.

**증상 [사용자 관찰, 2026-10-15]:** 타워 번개 한 방에 대상이 죽으면, 번개 끝이 유닛 위치에서 살짝 벗어난다. 살아 있는 대상(맞고도 사는 경우, 이동 중 추적)은 정상이다.

**코드로 확인한 사실 [직접, `84b6ff2a` 기준 행 번호 — 고치기 전에 다시 읽을 것]:**
1. 서버 순서 — `Assets/_Project/Scripts/Application/UseCases/TowerCombatUseCase.cs` `ExecuteTowerAttack`: 223~226행에서 표현용 시작점·맞은 위치를 읽고(`GetBuildingWorldPosition` / `GetUnitWorldPosition(target.Id)`, 값이 0이면 헥스 중심 `HexToWorld`), 241행 `target.TakeDamage(damage)`로 피해를 적용한 뒤, 245행 `_effectIdentity.Create(…, effectSource, effectImpact, …)`로 스냅샷을 만들고 248행 `OnAttackEffectConfirmed`로 발행한다. → 치명타면 스냅샷이 화면에 도착할 때 대상은 이미 죽어 있다(서버·Host·싱글. Client의 HP 반영과 스냅샷 수신 순서는 추적하지 않았다).
2. 화면 쪽, 대상 해석 — `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` `ResolveTowerBoltTarget`(267행~): **278행 `if (!data.IsAlive) { reason = "TargetDead"; return default; }`** — 재생 시작 때 대상이 이미 죽었으면 여기서 **빈 대상(`default`, `Data == null`)** 을 돌려준다. 호출은 246행.
3. 화면 쪽, 번개 — `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` `BeginTowerBolt`: 대상 참조가 없으므로 추적을 켜지 않고 약 172행 `_boltEndpoint = snapshotImpact + Vector3.up * _boltChestHeight;`로 끝점을 고정한다(기록 `Untracked(TargetDead)`). 살아 있으면 약 165행 `live + 가슴 높이`이고 `live` = `VisualRootProjector.PresentationPosition`(없으면 VisualRoot `Pose.position`). 같은 파일 약 76행 `TryReadEndpoint`의 `TargetDead`는 **추적 도중** 죽은 경우(LateUpdate → 고정)에 걸리는 확인이다.
4. 🔴 **가슴 높이도 이 경우 달라진다** — `EffectManager.cs` 550행이 `ResolveBoltChestHeight(boltTarget.Data)`를 부르고, 560~563행은 `target == null`이면 **`UnitEffectConfig.FallbackChestHeight`(0.6m)** 를 돌려준다. 즉 「맞는 순간 죽은」 대상은 크기 표(`GetChestHeight` = 균등 배율 × 기준 키 1.0 × 0.6)가 아니라 **항상 0.6m**를 받는다. `91371e55` 표 기준 예: Pistoleer(키 0.342) 표 가슴 높이 약 0.21m ↔ 사망 시 0.6m(약 0.4m 위), 표 최대 키 약 0.53 유닛도 약 0.32m ↔ 0.6m.
5. 스냅샷 Impact의 원천 — `Assets/_Project/Scripts/Infrastructure/UnitWorldPositionProvider.cs` `GetUnitWorldPosition`(35행~)은 유닛 GameObject **루트** `transform.position`(파괴됐으면 `Vector3.zero` → 헥스 중심 폴백)을 준다. 살아 있는 대상의 끝점은 **VisualRoot 투영 위치**다 — 두 원천이 다르다(값 차이는 재지 않았다).
6. Client 반전 화면에서는 `HitPresentationQueue.cs` 235~236행이 스냅샷 Source/Impact를 `ViewConverter.ToView`로 바꿔 넘긴다.

**가설 — ⚠️ 모두 미검증 (위 4·5번은 코드 사실이지만, 그것이 관찰된 어긋남의 원인인지는 재지 않았다):**
- H0. 가슴 높이가 크기 표 대신 0.6m 고정값으로 들어가 끝점이 위로 뜬다(위 4번). 작은 유닛일수록 차이가 크다 — 사용자가 본 어긋남이 「위쪽」이면 가장 먼저 확인할 후보.
- H1. 스냅샷 Impact(서버 시점의 유닛 GameObject 루트 위치 — 위 5번)와 그 순간 화면에 그려진 위치(보간·Visual Root 투영)가 다르다.
- H2. 스냅샷 Impact가 발밑이 아니다(예: 높이가 이미 들어 있거나 헥스 중심 폴백) → 가슴 높이가 이중으로 더해지거나 수평 위치가 셀 중심으로 간다.
- H3. Client 반전 변환 또는 Red 시점에서만 어긋난다(관찰이 어느 화면·어느 팀 시점이었는지 전달받지 않았다).

**첫 진단 단계 (구현 전 — 승인 후 실행):**
1. `BeginTowerBolt`의 「추적 불가로 시작」 갈래에서 진단 기록 한 줄을 남긴다: 스냅샷 Impact, 그 순간 읽을 수 있는 화면 위치(View가 아직 활성이면 `Projector.PresentationPosition`과 `Pose.position` 둘 다 — 이 갈래에는 대상 참조가 없으므로 `s.TargetId`로 View를 따로 읽어야 한다), 사용한 가슴 높이와 그 유닛의 표 가슴 높이(`GetChestHeight(type)`), 사유(`TargetDead` 등). 기존 `AttackEffectIdentity.Record` outcome 문자열에 덧붙이면 새 로그 채널이 필요 없다(4-B 5번과 같은 방식, Dev 빌드·에디터만).
2. Host 화면과 Client 화면 각각, **Blue 시점과 Red(반전) 시점** 각각에서 「한 방에 죽는」 대상을 맞혀 위 값을 모은다(역할을 바꿔 한 번 더).
3. 차이(수평·수직)가 사용자가 본 어긋남과 맞는지로 H1~H3을 가린다. 원인이 확정되기 전에는 동작 코드를 바꾸지 않는다.

**후보 수정과 규칙 문구 영향 (⚠️ 어느 것도 승인되지 않았다):**
| 후보 | 내용 | `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 (2026-10-13 블록) 영향 |
|---|---|---|
| F0 | (H0이 원인일 때) 대상이 죽었어도 스냅샷 `TargetId`의 유닛 타입으로 크기 표 가슴 높이를 구해 넘긴다(추적은 그대로 끄고 스냅샷 Impact 사용) | 문구 변경 **없음으로 보인다** — 규칙의 「끝점 높이: 발밑 + 가슴 높이(크기 표의 키 × 비율, 표에 없으면 0.6m)」에 오히려 맞춰지는 수정이다. 그래도 Plan에 적고 승인 후 구현 |
| F1 | 재생 시작 때 대상이 죽었어도 View가 아직 활성이고 같은 데이터 객체면 **그 순간의 화면 위치**를 끝점으로 쓰고 추적 없이 고정. 읽을 수 없을 때만 스냅샷 Impact | 「추적 불가로 시작할 때: … 서버 스냅샷의 Impact 위치를 끝점으로 쓴다」 문구를 바꿔야 한다 → **규칙 변경 전 사용자 승인 필요** |
| F2 | 스냅샷 Impact 쪽을 바로잡는다(발밑 기준으로 맞추거나 반전 변환 수정 등 — H2·H3이 원인일 때) | 문구는 그대로 둘 수 있다(스냅샷 Impact를 쓰는 규칙 유지). 단 서버 스냅샷 생성 코드를 건드리면 표현 데이터 계약 변경이므로 범위를 Plan에서 다시 정한다 |
| F3 | 치명타일 때만 사망 처리 직전 위치를 따로 넘기는 등 스냅샷 구조 확장 | 스냅샷·RPC 구조 변경 — 이번 작업이 「바꾸지 않는 것」으로 둔 영역이라 별도 설계·승인 |
피해·타겟 선택·HP·쿨다운은 어느 후보도 바꾸지 않는다(규칙 9 서버 권위, NET-AUTH-001).

**주석 비활성화 블록 6곳 — 이 결함이 사용자 테스트를 통과한 뒤에만 최종 삭제(2026-10-15 사용자 결정):**
| # | 파일 (`84b6ff2a` 기준 행) | 꺼 둔 옛 로직 |
|---|---|---|
| R-1 | `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs` 약 164행 | Bloom 「정확히 5개」 거부 |
| R-5 | 같은 파일 약 210행 | Bloom 대상 `lossyScale` 곱셈 |
| R-4 | `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs` 약 42행 | 화상·독 고정 배율 |
| R-3 | `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` 약 1385행 | 검증기 개수 5 단언 |
| R2-X1 | `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.TowerBolt.cs` 약 204행 | 번개 XZ 평면 조준·길이 |
| R3 | `Assets/Editor/Tools/UnitRecipientFitMeasureTool.cs` 약 332행 | 스케일 1을 강제한 `TRS` 배치 행렬 |
찾을 때는 `[R… 비활성화` / `[버그 — 2026-10-13 주석 처리]` 표식으로 검색한다(행 번호는 바뀐다).

---

## 14. 재작업 R4 — 검증 해시 줄바꿈 문제 (2026-10-19 추가, WORKFLOW [6] 재작업 — 0~13절은 보존)

### 14-0. 쉬운 설명

급류 정령 파도 검증이 원본 VFX·애니메이션 파일의 지문(SHA256)을 디스크 바이트 그대로 비교하기 때문에, 줄바꿈만 Windows 방식(CRLF)으로 바뀐 사용자 PC에서는 내용이 같아도 「파일이 바뀌었다」고 실패한다(Research 11절). **지문을 계산하기 전에 줄바꿈을 LF로 통일**해서, 저장소에 저장된 내용이 같으면 어느 PC에서든 같은 지문이 나오게 한다. 에디터 검증 코드만 고치며 게임 실행 코드는 바꾸지 않는다. 사용자 승인(2026-10-19): "1번대로 진행하자"(이 브랜치에서 수정) · "가로 진행해"(기존 task 문서에 기록).

### 14-1. 🔴 기존 로직 제거·비활성화 항목 (WORKFLOW [4])

- 없음. 고정된 해시 상수 2개(32·34행)는 **그대로 유지**한다 — 저장소의 LF 원본과 이미 일치하기 때문이다(Research 11-3). 해시 함수의 읽는 방식만 바꾼다.

### 14-2. 수정 항목

| # | 내용 | 파일 |
|---|---|---|
| R4-1 | 해시 계산 전 CRLF → LF 정규화. 원본 바이트를 읽은 뒤 줄바꿈만 LF로 바꿔 해시한다(현재 490~491행 `File.ReadAllBytes` 그대로 해시). 상수와 실패 메시지는 유지 | `Assets/Editor/Tools/TorrentWaveProductionValidation.cs` |
| R4-2 | 같은 방식(디스크 파일 해시를 상수로 고정)의 다른 검사가 있는지 찾는다. 메인 세션은 이 한 곳만 찾았다 [전달] — 더 있으면 수정 전에 보고하고 범위를 정한다 | (검색 결과에 따름) |

- 구현: **game-programmer**. 에디터 전용이며 런타임·피해·RPC·프리팹·애니메이션 파일은 바꾸지 않는다.

### 14-3. 진행 순서와 점검

1. game-programmer가 R4-1·R4-2를 구현하고 변경 파일을 보고한다.
2. 사용자가 `Hexiege/Combat/Run Full Regression Suite`를 **Edit Mode**로 다시 실행해 Torrent 파도 검사까지 통과하는지 확인한다.
3. 이어서 병합 전 확인을 마저 한다 — **이 브랜치에서** Fox 차지 VFX가 시전자 사망 시 사라지는지, 집결 지점(랠리 포인트) 이동이 정상인지.
4. 위가 모두 통과한 뒤에 main으로 병합한다.
