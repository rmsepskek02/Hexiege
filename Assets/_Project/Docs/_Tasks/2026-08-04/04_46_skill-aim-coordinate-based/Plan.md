# Plan — 스킬 지점 조준 좌표화 + 조준원 오버레이 렌더링

## 이 계획이 무엇을 하려는가 (자연어 설명)

Research에서 확인했듯이, 지금 스킬 조준은 손가락 위치를 가장 가까운 헥스 타일의 정수 좌표로 "스냅"해서, 조준 원의 중심이 항상 타일 정중앙에만 놓인다. 하지만 실제 착탄 판정(원 안에 들어온 적을 고르는 계산)은 이미 타일과 무관한 **연속 원**이다. 즉 문제는 딱 한 군데 — **"중심 좌표를 정수 타일로 만드는 부분"** 뿐이다.

그래서 이 계획은 (1) 그 "정수 타일화" 한 지점을 걷어내고 중심을 손가락 그대로의 **연속 좌표**로 흐르게 바꾸고, (2) 서버가 그 연속 좌표를 "유효한 타일인지"가 아니라 "맵 경계 안의 점인지"로 재검증하도록 고치며, (3) 조준 범위를 보여주는 원이 3D 타일에 파묻히지 않고 **항상 위에 선명하게** 그려지도록 렌더링 방식을 바꾼다.

> **승인 게이트:** 이 문서는 **제안(Plan)** 이다. 실제 코드 수정과 규칙 문서 정정은 사용자 승인 후에만 game-programmer / game-design-lead에게 위임하여 진행한다. 조준원 셰이더 구성과 맵 경계 판정 방식 등 일부 항목은 game-programmer가 씬에서 실측해 확정할 "미확정 항목"으로 남긴다(문서 하단 참조).

---

## ⚠️ 기존 로직 제거 원칙 (WORKFLOW 규칙 — 문서 최상단 명시)

이 작업에서 **제거 대상**은 아래 세 지점이다. 세 곳 모두 검증 전까지는 **삭제가 아니라 "비활성화(주석 처리)"** 를 기본으로 하며, 최종 삭제는 [6] 사용자 테스트 통과 후 [7] 문서 업데이트 전에 수행한다.

1. **`SkillAimController.UpdateAimPoint` 262행의 `HexMetrics.WorldToHex` 타일 스냅** — 연속 좌표 산출로 대체. 제거 안전 근거: 착탄 판정이 이미 연속 원(§B)이라 타일 스냅은 표시·전송 정밀도만 낮추는 잉여 단계.
2. **`SkillAimController.UpdateAimPoint` 274행의 `HexToWorld(_lastValidCoord)` 되돌림 표시** — 연속 좌표를 그대로 표시하도록 대체. 제거 안전 근거: 타일 중심 되돌림은 스냅 산출물이므로 스냅 제거와 한 쌍.
3. **`UnitCombatUseCase.ApplySkill*`의 `_mapper.HexToWorld(center)` 변환(1607·1637행)** — 연속 월드 좌표를 직접 수용하도록 대체. 제거 안전 근거: center가 이미 월드 좌표로 들어오면 재변환이 불필요·왜곡 요인.

> 규칙 26 서버 재검증 로직(`HasTile`, SkillActivationUseCase 117행)은 **제거가 아니라 "판정 기준 교체"**(유효 타일 → 맵 경계 안 점)다. 재검증 자체는 유지된다(클라 입력 불신뢰 원칙 = 규칙 25·26 존속).

---

## 1부. 규칙 문서(GameSystemRules_Skills.md) 정정안

> 이번 task에서 코드와 **함께** 후속 반영 예정. 아래는 정정 방향과 근거이며, 실제 문서 편집은 코드 구현 승인과 같은 사이클에서 진행한다.

| # | 대상 규칙(줄) | 현재 문구 요지 | 정정 방향 | 근거 |
|---|--------------|----------------|-----------|------|
| 1 | 규칙 19 (172행) | 화면 터치 시 범위가 손가락을 따라 이동 | 조준 원 중심은 **타일 스냅이 아니라 연속 좌표**로 손가락을 그대로 따라 이동함을 명시 | Research §A(262·274행 스냅), §B(판정은 연속 원) |
| 2 | 규칙 22 (188~189행) | 중심을 맵 타일 범위로 clamp, 최외곽 타일이 한계 | 중심은 **임의의 연속 좌표**이며, 위치만 **맵 경계(최외곽 타일 바깥선) 안으로 clamp**. 타일 사이 어디든 놓일 수 있음 | Research §A, §"맵 경계 판정 헬퍼 부재" |
| 3 | 규칙 26 (206~210행) | 좌표(정수 타일) 전송 + "유효한 맵 타일" 재검증 | RPC 페이로드를 **연속 좌표**로, 서버 재검증을 **"맵 경계 안 점(point-in-bounds)"** 으로 변경 | Research §C(RPC int q,r), §B(117행 HasTile) |
| 4 | 규칙 17 구현상태 주석 (163행) | 설계 정정 이력 주석 | "**조준 중심을 타일 스냅 → 연속 좌표로 변경**(정정 이력)" 한 줄 추가 | Research §A |
| 5 | **신규 규칙** (예: 규칙 20-2 또는 22-1) | (없음) | **범위 미리보기 렌더링 규칙 신설**: 조준 범위(조준 원)는 지형·타일과 겹쳐도 **항상 그 위에 선명하게 표시**(깊이 테스트에 가려지지 않는 오버레이/지면 데칼 방식) | Research §D(깊이 충돌 추정), 부가 이슈 |
| 6 | (변경 없음 명시) 규칙 24·25 | 지정 영역 "맵 안 어디든" / 서버 판정 | **의미 변화 없음.** 반경 판정은 이미 월드 연속 원(§B)이므로 "중심만 연속화, 반경 판정 유지" — 규칙 24·25 문구 유지 | Research §B |

---

## 2부. 코드 변경안 (파일별 · 위험 요소 포함)

**핵심 축:** 조준 "중심 좌표" 타입을 `HexCoord`(정수) → **연속 좌표**로 바꾼다. 연속 좌표의 구체 타입은 미확정(3부 참조)이나, 유력안은 **도메인 월드 `Vector3`(XZ 평면, Y=0)** 이다(근거: Research §"Application의 Unity 의존 허용 범위" — Application은 `Vector3` 사용이 명시적으로 허용되고 선례 다수, 금지 대상은 `Unity.Netcode`뿐).

### 2-1. `SkillAimController.cs` (Presentation) — 근거: 규칙 19
- `UpdateAimPoint`(255~277행): 262행 `WorldToHex` 스냅 **비활성화**, 261행 `domainWorld`(연속 도메인 좌표)를 그대로 유효 좌표로 채택. 표시(275행)는 손가락 위치(뷰 좌표)를 그대로 `_reticle.Show`에 전달(274행 타일 되돌림 제거).
- 맵 경계 clamp(규칙 22): "마지막 유효 타일 유지" → **"맵 경계 안으로 clamp된 연속 좌표 유지"** 로 판정 함수 교체. 판정 주입 `_isValidTile : Func<HexCoord,bool>`(114행)을 **연속 좌표 기준 판정**으로 교체 필요(3부 조사).
- 중심 좌표 필드/콜백 타입 연속화: `_lastValidCoord`(106행), `Action<int,int,HexCoord> _onConfirm`(110행), `BeginAim`의 `HexCoord fallbackCoord`(174행), `ResolveDefaultCoord`(332행), `ShowReticleAtCoord`(348행), `ResolveRelease`의 발동 인자(295행).
- **위험:** 뷰↔도메인 변환(ViewConverter) 방향을 연속 좌표에서도 정확히 유지해야 함(Red 팀 반전). 표시(뷰) vs 전송(도메인) 좌표 혼동 시 Red 팀에서 조준 위치가 반전될 수 있음.

### 2-2. `SkillActivationUseCase.Activate` (Application) — 근거: 규칙 22·26
- 시그니처 `Activate(int, int, HexCoord? aimCoord)`(90행) → **연속 좌표 타입**(`Vector3? aimWorld` 유력). 
- 재검증(117행) `_grid.HasTile(aim)` → **맵 경계 안 점 판정**으로 교체(3부 조사).
- 다리 `ApplyInstantAreaDamageBridge`/`ApplyAreaDotBridge`(142·147행): `HexCoord center` → **`Vector3 center`(월드)** 로 전달.
- **위험:** `Activate`는 플레이어/AI 공용 진입점(16~20행). 타입 변경이 향후 AI 스킬 경로(미구현)까지 파급 — 시그니처만 정렬해 두면 됨.

### 2-3. `UnitCombatUseCase.ApplySkill*` (Application) — 근거: 규칙 11·12(판정 재사용), 규칙 24(반경 유지)
- `ApplySkillInstantAreaDamage`(1603행)·`ApplySkillAreaDot`(1633행)의 `HexCoord center` → **`Vector3 center`(월드)**. 1607·1637행 `Flatten(_mapper.HexToWorld(center))` **제거**하고 전달받은 월드 좌표를 직접 `centerWorld`로 사용.
- `CollectEnemyUnitsInRadiusDomain`/`CollectEnemyBuildingsInRadiusDomain`(1719·1737행): **변경 없음**(이미 `Vector3 centerWorld` + `radiusSqr` 유클리드 판정). ← 좌표화의 핵심 이점.
- **위험:** 넘겨받는 월드 좌표가 "도메인 기준"인지 "뷰 기준"인지 계약을 명확히. 기존 center는 도메인 HexCoord였으므로, 연속 좌표도 **도메인 월드**여야 판정이 기존과 동일.

### 2-4. `NetworkSkillController.cs` (Infrastructure) — 근거: 규칙 26
- 래퍼 `RequestActivateSkill(..., HexCoord aimCoord, bool hasAim)`(84행) → **연속 좌표 인자**. 86행 `aimCoord.Q, aimCoord.R`(int 2개) → **float 2개 또는 Vector2/Vector3** 전송.
- `RequestActivateSkillServerRpc(..., int q, int r, ...)`(99행) → **float 좌표 파라미터**. 119행 `new HexCoord(q,r)` → 연속 좌표 재조립. 122행 `Activate(...)` 호출은 타입만 정렬.
- 쿨다운 브로드캐스트(125~126행, `SkillActivatedClientRpc`): **좌표 무관 — 변경 없음.**
- **위험:** NGO 2.9.2 직렬화 — `float`/`Vector2`/`Vector3` 모두 기본 지원(문제 없음). 다만 팀 소유권/재검증 순서(규칙 26)는 그대로 유지. RPC 메서드명 `ServerRpc`/`ClientRpc` 접미사 규칙 준수.

### 2-5. 서버 맵 경계 판정 헬퍼 (신규) — 근거: 규칙 22·26
- 현재 `HexGrid`에는 연속 점 판정 헬퍼가 없음(`HasTile`만, Research §"헬퍼 부재"). **point-in-bounds 판정을 어디에·어떻게 둘지 조사·확정 필요**(3부).
- **위험:** 판정을 `WorldToHex(point)` 후 `HasTile`로 재사용하면 "최근접 타일 존재"로 근사되어 사실상 반칸 여유가 생김. 엄밀한 월드 AABB clamp를 원하면 별도 계산 필요 — 정밀도/구현량 트레이드오프.

### 2-6. 조준원 오버레이 렌더링 — 근거: 규칙 5(신규, 렌더링)
- `SkillAimReticle.cs`(Presentation) 3겹 렌더러가 **깊이 테스트에 가려지지 않도록** 오버레이 방식으로 변경. 유력안: `ZTest Always`(항상 통과) 커스텀 URP 머티리얼을 3겹에 부여, 필요 시 렌더 큐/`sortingOrder` 상향.
- `SkillSetup_Scene.cs` `EnsureReticlePart`(402~410행): 현재 머티리얼 미지정(→ `Sprites/Default`). **커스텀 오버레이 머티리얼을 배선**하도록 수정 후 씬 셋업 재실행(Inspector 작업 가능성 — [5-2]).
- **위험:** 깊이 무시(ZTest Always)는 UI가 항상 위에 뜨지만, 다른 오브젝트 위로도 무조건 겹쳐 보일 수 있어 시각적 과다 노출 가능 → 셰이더 구성·큐를 game-programmer가 실측해 균형 잡아야 함. **셰이더/머티리얼 최종 구성은 미확정(3부).**

---

## 3부. 아키텍처 제약 및 미확정 / 조사 필요 항목

### 아키텍처 제약 (반드시 준수)
- **Application의 Netcode 직접 참조 금지.** 연속 중심 좌표 타입은 Netcode에 의존하지 않는 순수 타입이어야 한다. → **`Vector3`(도메인 월드)가 유력**(Application은 `UnityEngine.Vector3` 사용 허용 — Research §"Unity 의존 허용 범위", `IHexCoordinateMapper.cs` 20행). `HexCoord`는 Domain 값 타입.
- **NetworkBehaviour는 Infrastructure에만**, RPC 메서드명은 `ServerRpc`/`ClientRpc`로 종료(NetworkSkillController 준수).
- **Application → Infrastructure 역참조 금지** — 인터페이스는 Application에 선언(기존 `INetworkSkillController`/`IHexCoordinateMapper` 패턴 유지).
- **좌표 계약 일관성:** 표시(뷰 좌표) vs 전송/판정(도메인 좌표) 경계를 명확히 유지(ViewConverter Red 반전). 연속화로 스냅이 사라지면 부동소수 오차가 그대로 드러나므로 변환 순서를 흩트리지 말 것.
- **Y Scale 0.4 타일 프리팹은 의도된 등각 효과 — 변경 금지.** 조준원 렌더링 문제를 타일 높이 조정으로 풀지 않는다(오버레이 방식으로만 해결).

### 미확정 / 조사 필요 (game-programmer / game-design-lead 확정 대상)
1. **연속 좌표 타입 선택** — `Vector3`(도메인 월드) 유력 vs 실수 셀 좌표(float col/row) vs 신규 Domain 값 struct(`WorldPoint`). 레이어 순수성·직렬화·기존 선례 종합해 확정.
2. **맵 경계 판정 위치·방식** — `HexGrid`에 연속 point-in-bounds 헬퍼 신설 여부, "최근접 타일 HasTile 근사" vs "월드 AABB clamp" 중 택. 규칙 22 clamp의 정확한 경계 정의(최외곽 타일 바깥선)와 일치시킬 것.
3. **조준원 셰이더/머티리얼 구성** — `ZTest Always` 커스텀 URP 머티리얼 구성, 렌더 큐/sortingOrder 값, 지면 데칼 대안 검토. 실제 타일 렌더 큐·ZTest 실측 후 확정. `SkillSetup_Scene` 배선 수정 및 씬 재셋업(Inspector) 필요 여부 판단.
4. **좌표화 후 규칙 19/22/26 문구 최종 확정** — game-design-lead 확인(정정 방향은 1부 표 기준).

---

## 변경 예정 파일 목록 (구현 승인 시)

```
[수정 — 코드]
- Assets/_Project/Scripts/Presentation/Input/SkillAimController.cs
- Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs
- Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs
- Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs
- Assets/Editor/Setup/SkillSetup_Scene.cs   (조준원 머티리얼 배선 — 확정 시)
- (조사 결과에 따라) Assets/_Project/Scripts/Domain/Hex/HexGrid.cs 또는 Application 인터페이스 — 맵 경계 판정 헬퍼

[수정 — 규칙 문서]
- Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md   (규칙 17·19·22·26 + 신규 렌더링 규칙)

[추가 가능]
- 조준원 오버레이용 커스텀 머티리얼/셰이더 (game-programmer 확정 시)
```

> 위 목록은 **예정**이며, 실제 구현은 사용자 승인 후 전문 에이전트(game-programmer / game-design-lead) 위임으로 진행한다.
