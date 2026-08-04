# Research — 스킬 지점 조준 좌표화 + 조준원 오버레이 렌더링

## 이 작업이 무엇이고 왜 하는가 (자연어 설명)

지금 스킬(지점 지정형)을 쓸 때, 손가락을 어디에 두든 조준 원의 **중심이 항상 어떤 헥스 타일의 정중앙에 딱 붙는다.** 손가락이 타일과 타일 사이에 있어도, 코드가 손가락 위치를 가장 가까운 타일의 정수 좌표(HexCoord)로 "스냅(snap)"해 버리기 때문이다. 그래서 플레이어는 범위를 타일 격자 위에서만 옮길 수 있고, "타일 두 개 사이 경계에 착탄시키고 싶다" 같은 미세한 조준을 할 수 없다.

이번 작업의 목표는 두 가지다.

1. **조준 중심을 타일에 묶지 않고, 손가락을 그대로 따라가는 "연속 좌표"로 바꾼다.** 조준 원이 타일 격자를 무시하고 손가락 위치 그대로 부드럽게 이동하도록 만든다. (착탄 반경 판정 자체는 이미 연속 원이라 그대로 두고, "중심 입력"만 연속화한다.)

2. **조준 범위 UI(조준 원)가 3D 지형·타일과 겹쳐도 항상 그 위에 선명하게 보이도록 한다.** 현재는 조준 원이 지면에 아주 낮게(y=0.05) 깔린 스프라이트라, 높이가 있는 3D 타일에 파묻혀(가려져) 겹치는 부분이 안 보이는 것으로 추정된다.

> **주의:** 이 문서는 현재 코드/규칙의 **사실 조사**만 담는다. 실제 코드 수정과 규칙 문서(GameSystemRules_Skills.md) 정정은 이 Research/Plan 승인 이후 별도 단계에서 진행한다. 조준원 렌더링(셰이더/머티리얼)과 맵 경계 판정의 최종 구현 방식은 game-programmer가 실측·확정할 사항이며, 이 문서에는 "추정" 표시와 함께 근거만 기록한다.

---

## 관련 규칙 (SSoT: GameSystemRules_Skills.md)

이 작업이 건드리는 규칙(모두 "기획 확정 / 미구현" 또는 "설계 정정 대기" 상태):

- **규칙 19 (172행)** — "조준 모드에서 화면을 터치하면 그 순간부터 범위(조준 원)가 손가락을 따라 이동"한다고 되어 있으나, 실제 코드는 손가락을 따라가되 **타일 중심으로 스냅**한다. 문구상 "연속 이동"과 코드의 "타일 스냅" 사이에 간극이 있다.
- **규칙 22 (188~189행)** — "조준점의 중심은 맵 타일 범위 안으로 clamp… 최외곽 타일이 한계." 현재 clamp는 "마지막 유효 타일 유지" 방식이며, 중심이 곧 타일 좌표다.
- **규칙 24 (194~195행)** — 지정 가능 영역은 "맵 안 어디든", 사거리/시야 제한 없음. (의미 변화 없음 — 참고용.)
- **규칙 26 (206~210행)** — 클라이언트는 "손을 뗀 순간의 조준 좌표(1개)"만 RPC 전송, 서버는 "전송된 좌표가 **유효한 맵 타일**인지" 재검증. 현재 좌표는 정수 타일(HexCoord).
- **규칙 10 등 조준원 렌더링 관련 규칙은 없음** — 조준 범위 미리보기가 "지형과 겹쳐도 항상 위에 그려져야 한다"는 규칙은 현재 문서에 **존재하지 않는다**(신규 규칙 필요).

---

## A. 현재 "타일 스냅"이 실제로 일어나는 위치

**파일:** `Assets/_Project/Scripts/Presentation/Input/SkillAimController.cs` (Presentation)

핵심은 `UpdateAimPoint(Vector2 screenPos)` (255~277행)다. 흐름:

1. **259~260행** — 화면 좌표 → XZ 평면(뷰 좌표): `ScreenToXZPlane(screenPos)` → `viewPos`.
2. **261행** — 뷰 좌표 → 도메인 좌표: `ViewConverter.FromView(viewPos)` → `domainWorld`. (Red 팀 반전 흡수.)
3. **262행** — 도메인 좌표 → **정수 헥스 좌표**: `HexCoord coord = HexMetrics.WorldToHex(domainWorld);` ← **여기서 연속 좌표가 정수 타일로 양자화된다.**
4. **264~269행** — `_isValidTile(coord)`(= `grid.HasTile`)로 유효 타일이면 `_lastValidCoord = coord` 저장(맵 밖이면 마지막 유효 좌표 유지 = 규칙 22 clamp 느낌).
5. **271~276행** — 조준 원 표시 위치를 **타일 중심으로 되돌려** 스냅:
   `Vector3 snappedView = ViewConverter.ToView(HexMetrics.HexToWorld(_lastValidCoord));`
   `_reticle.Show(snappedView, _radius);`
   즉 **표시 좌표도 타일 중심**으로 강제된다.

**결론(A):** 표시 위치와 발동 좌표가 **둘 다 `HexCoord`(정수 타일)로 양자화**되어 있다. 원인은 262행의 `WorldToHex`(연속→정수) 한 줄과, 그 결과를 다시 `HexToWorld`로 되돌려 표시하는 274행이다.

관련 부수 지점(모두 HexCoord 전제):
- `BeginAim(...)` (174~198행): `HexCoord fallbackCoord` 인자, 콜백 타입 `Action<int,int,HexCoord> onConfirm` (필드 선언 110행).
- `ResolveDefaultCoord(HexCoord)` (332~343행): 화면 중앙을 `WorldToHex`로 타일화(340행).
- `ShowReticleAtCoord(HexCoord)` (348~357행): 타일 중심으로 표시.
- `ResolveRelease(...)` (282~296행): 발동 시 `_onConfirm?.Invoke(_buildingId, _skillSlot, _lastValidCoord)` (295행) — **HexCoord를 그대로 상위로 전달**.
- 마지막 유효 좌표 필드: `HexCoord _lastValidCoord; bool _hasValidCoord;` (106~107행).

---

## B. 발동 → 유닛 적용 경로 (반경 판정은 이미 "연속 원", 중심만 타일)

**파일:** `Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs` (Application)

- `Activate(int buildingId, int skillSlot, HexCoord? aimCoord)` (90행) — 단일 진입점(플레이어/AI 공유).
- **110~119행** — 지점 지정 스킬이면 재검증: `if (_grid != null && !_grid.HasTile(aim)) return false;` (117행). 즉 **중심이 유효 타일인지**를 검사. `aim`은 `HexCoord`.
- 실행기 → 전투 다리(델리게이트):
  - `ApplyInstantAreaDamageBridge(TeamId, HexCoord center, float radius, int rawDamage, int sourceBuildingId)` (142행)
  - `ApplyAreaDotBridge(TeamId, HexCoord center, float radius, float dps, float duration)` (147행)
  - 둘 다 `center`를 `HexCoord`로 전달.

**파일:** `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` (Application)

- `ApplySkillInstantAreaDamage(TeamId, HexCoord center, float radius, int rawDamage, int sourceBuildingId)` (1603행):
  - **1607행** — `Vector3 centerWorld = Flatten(_mapper.HexToWorld(center));` ← 중심을 다시 월드 좌표로 변환(즉 타일 중심 월드).
  - **1608행** — `float radiusSqr = radius * radius;`
- `ApplySkillAreaDot(TeamId, HexCoord center, float radius, float dps, float duration)` (1633행): 동일 구조(1637~1638행).
- 반경 판정 본체:
  - `CollectEnemyUnitsInRadiusDomain(TeamId, Vector3 centerWorld, float radiusSqr, List<UnitData>)` (1719행):
    **1726~1727행** — `Vector3 rel = Flatten(_mapper.HexToWorld(unit.Position)) - centerWorld; if (rel.sqrMagnitude <= radiusSqr) result.Add(unit);`
  - `CollectEnemyBuildingsInRadiusDomain(...)` (1737행): 동일 — **1744~1745행** 유클리드 거리 제곱 비교.

**결론(B):** 실제 착탄 판정은 이미 **월드 연속 원**(중심 월드좌표 + 반경 제곱, 유클리드 거리 비교)이다. 유일하게 타일에 묶인 것은 **"중심 좌표"의 입력 타입(HexCoord)뿐**이다. 좌표화하려면 이 중심 파라미터를 `HexCoord` → **연속 월드 좌표(Vector3)** 로 바꾸고, `_mapper.HexToWorld(center)`(1607·1637행) 변환을 제거하면 판정 알고리즘은 그대로 재사용된다.

---

## C. 네트워크 경로 (RPC 좌표 타입)

**파일:** `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs` (Infrastructure, NetworkBehaviour)

- 래퍼 `RequestActivateSkill(int buildingId, int skillSlot, HexCoord aimCoord, bool hasAim)` (84~87행):
  - **86행** — `RequestActivateSkillServerRpc(buildingId, skillSlot, aimCoord.Q, aimCoord.R, hasAim);` ← **좌표를 정수 두 개(Q, R)로 분해 전송**.
- `RequestActivateSkillServerRpc(int buildingId, int skillSlot, int q, int r, bool hasAim, ServerRpcParams)` (98~127행):
  - 팀 소유권 검증(111~116행) — Host(ClientId=0)=Blue, 그 외=Red.
  - **119행** — `HexCoord? aim = hasAim ? new HexCoord(q, r) : (HexCoord?)null;` ← 정수 좌표 재조립.
  - **122행** — `bool activated = skill.Activate(buildingId, skillSlot, aim);` (내부에서 규칙 26 재검증).
  - 성공 시 쿨다운 총량을 읽어 `SkillActivatedClientRpc`로 양 클라 브로드캐스트(125~126행). **이 경로는 좌표와 무관(쿨다운 동기화만)** — 좌표화 영향 없음.

**결론(C):** RPC 시그니처가 **`int q, int r`(정수 2개)** 로 고정되어 있다. 좌표화 시 **부동소수 좌표(예: float 2개 또는 Vector2/Vector3)** 전송으로 바꾸고, 119행의 `new HexCoord(q, r)` 재조립을 연속 좌표 재조립으로 교체해야 한다. 서버 재검증(규칙 26)은 `Activate` 내부(§B의 117행)에서 이뤄지므로, 그 재검증도 "유효 타일" → "맵 경계 안 점"으로 함께 바뀌어야 한다. NGO 2.9.2 RPC는 `float`/`Vector2`/`Vector3`를 기본 직렬화하므로 타입 자체의 전송은 문제없다(Infrastructure 레이어라 Netcode 사용 허용).

---

## D. 조준 원이 3D 타일과 겹쳐 안 그려지는 문제

**파일:** `Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs` (Presentation)

- 3겹 SpriteRenderer(Ring/Fill/Dot)를 월드에 눕혀(X축 90° 회전, `Awake` 92행 / `Show` 107행) 지면에 깐다.
- **83행** — `_yOffset = 0.05f` (지면 z-fighting 회피용 아주 낮은 높이).
- **108행** — `transform.position = new Vector3(worldPos.x, _yOffset, worldPos.z);` ← 조준 원은 y=0.05에 위치.
- `ApplyVisual` (126~141행) — Ring/Fill/Dot의 **`sortingOrder`를 0/1/2로만 구분**(136·138·140행). `sortingOrder`는 **스프라이트(투명) 렌더러들 사이의 상대 순서**만 정할 뿐, 불투명 3D 메시와의 앞뒤(occlusion)는 결정하지 못한다.

**파일:** `Assets/Editor/Setup/SkillSetup_Scene.cs` (Editor, 셋업 스크립트)

- `EnsureReticlePart(Transform, string name, Sprite)` (402~410행): 각 겹을 `SpriteRenderer`로 확보하고 **내장 원 스프라이트만 물린다**(407~408행). **머티리얼을 명시적으로 지정하지 않음** → SpriteRenderer 기본 머티리얼(`Sprites/Default`)이 쓰인다.
- 스프라이트 소스: `LoadBuiltinSprite("UI/Skin/Knob.psd", "UI/Skin/UISprite.psd")` (383행, 385~387행에서 3겹에 동일 원 스프라이트 배선).

**추정 원인(depth 충돌 — game-programmer 실측 필요):**
`Sprites/Default` 셰이더는 `ZWrite Off`지만 **`ZTest LEqual`(깊이 테스트 수행)** 이다. 조준 원은 y=0.05로 지면에 붙어 있고, 3D 타일 프리팹은 Y Scale 0.4의 등각 높이를 가져(타일 윗면이 y=0.05보다 위로 올라옴) **타일 지오메트리가 조준 원보다 카메라에 가깝게 앞을 가리면 조준 원의 그 부분이 깊이 테스트에서 탈락해 파묻힌다.** `sortingOrder`(§D 위)는 이 불투명-투명 앞뒤 관계를 뒤집지 못한다. 따라서 "지형과 겹쳐도 항상 위"를 보장하려면 **깊이 테스트를 무시하는 오버레이 방식**(예: `ZTest Always` 커스텀 머티리얼, 또는 지면 데칼/전용 렌더 패스)이 필요하다. 정확한 타일 렌더링(메시 높이·URP 렌더 큐·머티리얼)은 game-programmer가 실측해 확정한다.

> **미확인(추정):** 위는 `Sprites/Default`의 표준 렌더 상태와 "Y Scale 0.4 타일" 사실에 근거한 추정이다. 실제 타일 프리팹의 셰이더 렌더 큐/ZTest 값과 조준 원의 최종 픽셀 깊이는 game-programmer가 씬에서 실측해 확정해야 한다.

---

## 좌표계·아키텍처 사실 (변경안 판단의 근거)

- **좌표계:** `HexMetrics.HexToWorld(coord)` → `Vector3(x, 0, z)` (XZ 평면, Y=0). `WorldToHex(Vector3)`는 주변 9개 후보 중 최근접 타일을 고르는 브루트포스(165~211행) — **연속→정수 양자화 지점**.
- **뷰 변환:** Presentation은 `ViewConverter.FromView/ToView`로 Red 팀 좌표 반전(2·center−pos, X·Z만)을 흡수. Application은 `IHexCoordinateMapper.NormalizeToDomainPosition`(= FromView)로 동일 처리.
- **맵 경계 판정 헬퍼 부재:** `HexGrid`에는 `HasTile(HexCoord)`(158~161행, `Dictionary.ContainsKey`)만 있고, **"연속 월드 점이 맵 안인가"를 판정하는 헬퍼는 없다.** 좌표화 시 새 판정 로직(예: `WorldToHex(point)` 후 `HasTile` 재사용, 또는 월드 AABB clamp)이 필요 — **조사 필요 항목**(Plan 참조).
- **Application의 Unity 의존 허용 범위:** `IHexCoordinateMapper.cs` 20행 주석 — *"Application 레이어 — UnityEngine.Vector3만 사용 (Unity 의존 허용)."* 실제로 `UnitCombatUseCase`(15행 `using UnityEngine`)와 이 인터페이스가 `Vector3`를 자유롭게 쓴다. **금지 대상은 `Unity.Netcode`뿐**이다. → 연속 중심 좌표를 `Vector3`(도메인 월드)로 표현하는 것은 기존 선례와 일관되며 레이어 규칙을 위반하지 않는다. (`HexCoord`는 Domain 값 타입.)

---

## 영향 범위 요약 (6개 지점)

| # | 파일 / 레이어 | 현재 | 좌표화 시 변경 필요 |
|---|--------------|------|--------------------|
| 1 | `SkillAimController.cs` (Presentation) | 262행 `WorldToHex`로 타일 스냅, 274행 타일 중심 표시 | 스냅 제거 — 연속 좌표 산출·조준 원 표시, 콜백/필드 타입 연속화 |
| 2 | `SkillActivationUseCase.Activate` (Application) | `HexCoord? aimCoord`, 117행 `HasTile` 재검증 | 연속 좌표 타입, 재검증 "맵 경계 안 점"으로 |
| 3 | `UnitCombatUseCase.ApplySkill*` (Application) | `HexCoord center` + 1607/1637행 `HexToWorld` 변환 | `Vector3 center`(월드) 직접 수용, 변환 제거 |
| 4 | `NetworkSkillController` (Infrastructure) | 86행 `q,r`(int) 전송, 119행 `new HexCoord` | float/Vector 전송·재조립 |
| 5 | 서버 재검증(맵 경계) — 신규 헬퍼 | `HasTile`만 존재 | point-in-bounds 판정 위치·방식 조사 필요 |
| 6 | `SkillAimReticle.cs` + `SkillSetup_Scene.cs` (렌더링) | `Sprites/Default`, y=0.05, sortingOrder만 | 깊이 무시 오버레이 방식(ZTest Always류) — game-programmer 실측 |

---

## 발견된 부가 이슈

- **규칙 문서 간극(규칙 19):** 규칙 19의 "손가락을 따라 이동"은 현재 코드의 "타일 스냅" 동작과 문구가 어긋난다. 좌표화는 규칙 19의 의도(연속 추종)에 오히려 부합하지만, 규칙 22(타일 clamp)·규칙 26(유효 타일 재검증)과는 문구 정정이 필요하다.
- **조준원 렌더링 규칙 부재:** "범위 미리보기가 지형과 겹쳐도 항상 위에 선명하게"라는 요구를 담은 규칙이 SSoT에 없다. 신규 규칙 추가가 필요(Plan 참조).
- **AI 경로 영향:** `Activate`는 플레이어/AI 공용 진입점(SkillActivationUseCase 16~20행 주석). 중심 좌표 타입을 바꾸면 AI가 좌표를 직접 계산해 넘기는 경로도 함께 타입을 맞춰야 한다(현재 AI 스킬 사용은 미구현이므로 시그니처만 정렬).
- **셋업 스크립트 재실행 필요 가능성:** 조준원 머티리얼을 오버레이 방식으로 바꾸면 `SkillSetup_Scene.cs`의 `EnsureReticlePart`가 커스텀 머티리얼을 배선하도록 수정하고 씬 셋업을 재실행해야 할 수 있다(Inspector 작업). — Plan 단계에서 확정.
