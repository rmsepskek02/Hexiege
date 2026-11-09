# Plan — 인간 방어탑(CannonTower) 전방 120° 공격 제한

## 이 문서는 무엇인가

인간 대포탑이 **포신이 향한 정면 좌우 60°씩(합 120°) 안의 적만** 공격하게 바꾸는 계획이다. 이 계획대로 하면 다음처럼 동작한다.

- 내 대포탑은 상대 진영 쪽(화면 위쪽)을 바라보고, 그 앞쪽 부채꼴 안에 들어온 적 중 **가장 가까운 적**을 쏜다.
- 적이 탑 **뒤나 옆 바깥**에만 있으면 쏘지 않고 기다린다. **공격하지 않는 동안에도 쿨다운(재장전 시간)은 계속 줄어**, 0이 되면 0에서 기다리다가 적이 앞 120°에 들어오는 순간 바로 쏜다 — 지금 코드의 동작 그대로다(7-1 Q6). *(2026-11-09 정정 — 종전: 「기다리는 동안 재장전 시간은 그대로 흐르므로, 적이 앞쪽으로 들어오는 순간 바로 쏜다.」)*
- 앞에도 적이 있고 뒤에 더 가까운 적이 있으면 **앞의 적**을 쏜다.
- 이미 쏜 포탄은 대상이 옆 · 뒤로 빠져도 지금처럼 날아가 맞는다.
- 정령 룬 첨탑 · 초월 덩굴 타워는 지금처럼 모든 방향을 쏜다.
- **(2026-11-09 범위 추가)** 내 방어탑을 눌러 선택하면 **공격 범위가 바닥에 표시**된다. 탑 3종 모두 사거리 원이 나오고, 인간 대포탑은 그 원 안에서 **앞 120° 부채꼴이 강조**된다. 선택을 풀면 사라진다. 화면에만 그리는 표시라 공격 판정에는 아무 영향이 없다(3-5).

바뀌는 곳은 **서버가 공격 대상을 고르는 판정 한 곳**과, **탑을 선택했을 때 범위를 그리는 화면 표시**(판정과 무관)다. 공격 연출 · 통신 형식 · 접속 확인 번호(스키마)는 바뀌지 않는다. 테스트 때 「정말 뒤로는 안 쏘는지」를 눈으로만 판단하기 어렵기 때문에 `[TOWER-ARC]` 진단 로그를, 「보이는 원이 실제 사거리와 같은지」를 확인하려고 `[TOWER-RANGE-UI]` 진단 로그를 함께 넣는다(5절). *(2026-11-09 범위 추가 — 종전: 「바뀌는 곳은 **서버가 공격 대상을 고르는 판정 한 곳**이다. 화면 연출 · 통신 형식 · 접속 확인 번호(스키마)는 바뀌지 않는다. … `[TOWER-ARC]` 진단 로그를 함께 넣는다(5절).」)*

현재 코드 상태와 근거(파일 · 행)는 [Research.md](Research.md)에 있다. 추천안은 game-programmer가 코드를 읽고 낸 것이며, **사용자 결정이 아니다.** 7절의 사용자 확인 Q1~Q14를 정한 뒤 이 Plan을 사용자가 승인해야 구현을 시작한다(WORKFLOW [4]). *(2026-11-09 — Q1~Q14는 전부 추천안으로 결정됐고(Q6은 문구 정정), 범위 표시 추가와 함께 구현이 승인됐다 → [7-1](#7-1-user-decisions).)*

> **현재 상태 (2026-11-09):** **구현 승인 — 진행 중.** 결정 = [7-1](#7-1-user-decisions). 구현 · 푸시 뒤 메인 세션이 테스트 절차를 안내한다. **로그 올리기: 작업명 `cannon-tower-front-arc`**(WORKFLOW 「[6] 사용자 테스트」 — 폴더 이름 `22_19_cannon-tower-front-arc`에서 시각을 뺀 것). 규칙 문서는 아직 무변경(8절). *(종전 상태: 「사용자 확인 Q1~Q14 대기, 구현 승인 전. 코드 · 에셋 · 규칙 문서 무변경.」)*

> **🔴 게임 규칙 변경 — 사용자 승인 대상.** `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 2 · 11에 단서를 붙이고 규칙 14를 새로 만드는 안이다(8절). **규칙 문서는 사용자 테스트 통과 뒤 반영한다** — 이번 작업의 구현 커밋에서는 고치지 않는다.

**연결 기록:** 처음 요청(2026-10-21, 기록만) — [ROADMAP](../../../ROADMAP.md) 29행 · [서버 권위 투사체 Plan 9절 F-120](../../2026-10-19/13_30_server-authoritative-projectile/Plan.md#9-follow-up-items-not-started) · [Research 15-9](../../2026-10-19/13_30_server-authoritative-projectile/Research.md#15-9-front-120-request). 그 Research의 행 번호는 지금 맞지 않는다 — 이 Task Research의 행 번호를 쓴다.

**표기:** `[사실]` = Research에서 확인한 것 · `[추천 — 프로그래머]` = game-programmer 추천(결정 아님) · `[제안]` = 결정 아님 · `[사용자 확인]` = 7절에서 사용자가 정할 것. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Editor/` = `Assets/Editor/`.

> **기존 로직 제거 — WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 최상단에 둔다.**
>
> **코드 제거 · 주석 처리는 없다.** 판정은 기존 최단 거리 탐색 안에 「Human이면 전방 범위 밖 후보를 건너뛴다」는 조건을 **덧붙인다.** 범위 표시(2026-11-09 추가)도 새 표시 · 새 훅을 **더하기만** 한다 — 기존 MistShrine 범위 원 · 액션 패널 슬롯 동작은 바꾸지 않는다.
>
> | 바뀌는 것 | 방식 | 안전 근거 |
> |---|---|---|
> | 회귀 fixture `ValidateHumanTowerRedMuzzle`(C-2 Red)의 Blue 유닛 위치 (0.1, 0, 0.1) | **값 변경** — 탑의 −Z 쪽(예: (0.6, 0, −2.4))으로 옮긴다. 검사 항목(Red 탑이 Blue 유닛을 노림 · 포구 = 탑 + (−x, y, −z) 오프셋)은 그대로 | 지금 위치는 Red 탑 정면(−Z)에서 153.4°라 새 규칙에서는 「탑 뒤」다(Research 6-1). 이 테스트가 확인하려는 것은 Red 포구 방향이지 뒤쪽 사격이 아니다. 옛 값은 이 Plan과 Research 6-1에 남아 있어 되돌릴 수 있다. 다른 fixture가 같은 공격자 위치를 쓰는지는 fixture 생성 구조상 테스트마다 새로 만든다고 프로그래머가 보고했다 `[전달]` — 구현 때 확인 |

---

<a id="0-rules"></a>
## 0. 근거로 읽은 규칙

WORKFLOW [4]에 따라 `Assets/_Project/Docs/GameSystemRules.md`(인덱스)를 읽고 건물 행의 원본을 열었다. 아래 각 수정 항목의 「근거」 칸은 이 규칙을 가리킨다. 「타워 규칙 N」 = [`GameSystemRules/GameSystemRules_Buildings.md`](../../../GameSystemRules/GameSystemRules_Buildings.md) **「방어 타워 시스템」** 절 규칙 N(건물 문서는 절마다 번호가 다시 시작한다).

| 규칙 | 이 계획과의 관계 |
|---|---|
| 타워 규칙 1 「타겟 대상」 | 대상 종류(적 유닛만)는 바꾸지 않는다 |
| 타워 규칙 2 「타겟 선택 기준」 | **바뀐다** — Human CannonTower는 전방 범위 안 후보 중 최단. 단서 1문장 필요 → 8절 |
| 타워 규칙 3 「사거리 판정 기준」 | 거리 판정(`Vector3.Distance`)은 그대로. 각도 기준점도 같은 탑 중심(Q2) |
| 타워 규칙 4 「배치 직후 첫 공격」 | 배치 직후 전방에 적이 없으면 첫 공격은 전방에 들어올 때까지 미뤄진다 — 쿨다운 0 유지라 문장과 충돌 없음 |
| 타워 규칙 5 「공격 후 쿨다운」 | 「쿨다운 중에도 타겟 탐색은 계속」과 충돌 없음. 뒤에만 적일 때 쿨다운은 시작하지 않는다(Q6) |
| 타워 규칙 6 「타겟 사망 시 즉시 새 타겟 탐색」 | 새 타겟도 전방 범위 안에서만 — 규칙 2 단서가 덮는다 |
| 타워 규칙 9 「서버 권위 처리」 | 각도 판정도 서버(싱글 · Host) Tick에서만. Client 변경 0 |
| 타워 규칙 11 「Human CannonTower 배치 시 초기 회전」 | **「정면」의 근거.** 규칙은 화면 기준으로 서술돼 있어 도메인 정의(Blue +Z / Red −Z) 1문장 필요 → 8절 |
| 타워 규칙 12 「타워 발사 연출」 | 서버 권위 투사체 · 포구 · 착탄 때 피해는 그대로. 발사 뒤 대상 이탈해도 명중(Q7) |
| 타워 규칙 13 「무력화」 | EMP 관문은 탐색 앞이라 그대로. 무력화 중에는 각도 판정까지 가지 않는다 |
| **전방 사격 범위를 정의한 규칙 — 없음** | 규칙 14를 새로 만든다(Q13). **사용자 테스트 통과 뒤 반영 → 8절** |
| `GameSystemRules_UnitCombatSynchronization.md` 「10. 현재 구조에서의 전환 규칙」 5항 | RPC 모양이 바뀌지 않으므로 스키마를 올리지 않는다(Q12) |
| [`GameSystemRules_BuildingPanelsUI.md`](../../../GameSystemRules/GameSystemRules_BuildingPanelsUI.md) **「MistShrine 패널 UI」** 규칙 8 「패널이 열려 있는 동안에만 범위 표시」 · 규칙 12 「반투명 채움 + 외곽선」 | **범위 표시의 선례**(2026-11-09 추가). MistShrine 전용 규칙이라 탑에 직접 적용되지는 않는다 — 같은 방식을 따른다 `[제안]` |
| **방어탑 범위 표시를 정의한 규칙 — 없음** | 규칙 신설 필요. **사용자 테스트 통과 뒤 반영 → 8절** |

---

<a id="1-recommendations"></a>
## 1. 추천안 한눈에 `[추천 — 프로그래머]`

| 결정할 것 | 추천 | 다른 안 | 자세히 |
|---|---|---|---|
| 적용 범위 | **Human CannonTower만** | 탑 3종 모두 | Research 3절 · Q1 |
| 각도 기준점 | **탑 중심(루트) XZ** — 사거리와 같은 점 | 포구 XZ(실질 각도가 좁아짐) | Research 2-2 · Q2 |
| 「정면」 | **팀 진영 고정 — Blue +Z / Red −Z**(규칙 11 배치 방향) | 배치 때 선택 / 적 성 방향 / 실시간 회전 | Research 2-3 · Q3 |
| 경계 60° | **포함 + 허용 오차**(cos 임계 0.5 − 0.001 ≈ 60.07°) | 엄격(대각 앞 타일 전부 빠짐) | Research 2-1 · Q4 |
| 선택 의미 | **범위 안 적 중 최단** | 전체 최단이 뒤면 침묵 | Research 3절 · Q5 |
| 뒤에만 적 | **쏘지 않고 대기. 공격하지 않는 동안에도 쿨다운은 계속 줄어 0에서 대기하다, 앞 120°에 들어오면 즉시 발사(현행 동작 유지)** *(2026-11-09 문구 정정 — 종전: 「쏘지 않고 대기, 쿨다운 유지」)* | — | Q6 |
| 발사 뒤 대상 이탈 | **그대로 명중(현행)** | 취소 | Q7 |
| 값 보관 | **코드 상수** | `BuildingStatsConfig` 칸 | Q8 |
| 좌표 | 방향 계산 전 **도메인으로 정규화** | 정규화 없음 | Research 1-5 · R3 |
| 스키마 | **올리지 않음**(8 유지) | 올림 | Research 4절 · Q12 |
| 범위 표시 (2026-11-09 추가) | **포함 — 탑 3종 사거리 원 + Human 앞 120° 부채꼴 강조**, 선택 중에만 · 화면 전용 | 이번에 하지 않음 | 3-5 · [7-1](#7-1-user-decisions) |

---

<a id="2-files"></a>
## 2. 변경 파일 목록 (추천안 기준)

레이어 방향은 Domain → Application → Core → Infrastructure → Presentation → Bootstrap을 지킨다. 판정은 Application(`TowerCombatUseCase`) 안에서 끝나고, 좌표 정규화는 이미 있는 Application 인터페이스 `IHexCoordinateMapper.NormalizeToDomainPosition`을 쓴다 `[사실 — Research 1-5]`. **새 RPC · 새 인터페이스 메서드 · 생성자 인자 추가 없음.**

| # | 파일 | 변경 | 레이어 | 근거 규칙 |
|---|---|---|---|---|
| 1 | `Scripts/Application/UseCases/TowerCombatUseCase.cs` | `FindNearestEnemyUnit`(231~276행) 안에서, 탑이 Human AutoTower일 때 각 후보의 수평 방향이 전방 범위 밖이면 건너뛴다(새 private 헬퍼 가능). 방향 계산 전 탑 · 유닛 위치를 도메인으로 정규화. 각도 상수(반각 60° · 허용 오차) 코드 상수 | Application | 타워 규칙 2 · 3 · 9 · 11 · 신설 14 |
| 2 | `Scripts/Application/Combat/ProjectileDeliveryContracts.cs` (`ProjectileSettings` 옆) | 정면 벡터를 돌려주는 정적 헬퍼 1개(예: `ResolveTowerForward(team)` — Blue +Z / Red −Z) `[제안]`. 기존 `ResolveTowerMuzzle`(454~459행)을 이 헬퍼로 바꾸는 것은 **하지 않는다**(범위 확장 — R6, 사용자 확인 시에만) | Application | 타워 규칙 11 |
| 3 | `Scripts/Application/UseCases/TowerCombatUseCase.cs` 또는 새 partial(예: `TowerCombatUseCase.ArcDiagnostics.cs`) | `[TOWER-ARC]` 진단 — Fire · HoldBegin · HoldEnd · End. `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` | Application | — (WORKFLOW [4] 진단 로그 규칙) |
| 4 | `Scripts/Bootstrap/GameBootstrapper.cs`(548행 인근) · `Scripts/Infrastructure/Network/NetworkCombatController.cs`(2750행 인근) | 경기 종료 때 `[TOWER-ARC] End` 1회 호출(중복 방지 플래그 — 선례 `EndWritten`) | Bootstrap / Infrastructure | — |
| 5 | `Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs` | C-2 Red fixture 유닛 위치 **값 변경**(최상단 표) + 새 fixture (a)~(f)(Research 6-1) | Editor | 타워 규칙 2 · 14 |
| 6 | **(2026-11-09 추가)** `Scripts/Presentation/Effects/` 아래 새 표시 컴포넌트(이름 예: `TowerRangeIndicator`) `[제안 — 이름 · 재사용 여부는 game-programmer가 구현 때 정하고 보고]` | 바닥 사거리 원(채움 + 외곽선) + Human일 때 앞 120° 부채꼴 강조. API 예 `Show(BuildingData)` / `Hide()`. 선례 `MistShrineRangeIndicator.cs`(채움 · 외곽선 2겹, 비활성 저장 시 `Awake` 함정 · 스프라이트 기준 지름 — mistshrine Plan §9-1 · §9-3) | Presentation | 신설 UI 규칙(8절) · 「MistShrine 패널 UI」 규칙 8 · 12 선례 |
| 7 | **(2026-11-09 추가)** `Scripts/Presentation/UI/BuildingActionPanelUI.cs` | AutoTower 패널이 열릴 때(`OnShow`, 160행) 표시, 닫힐 때(`OnBeforeClose` 훅 — 선례 `MistShrinePanelUI.cs` 245행 표시 · 256행 숨김) 숨김. 18행 주석 「AutoTower 사거리 표시 토글은 아직 없으며」 갱신 `[사실 — 행 번호 문서 담당 재확인]` | Presentation | 같음 |
| 8 | **(2026-11-09 추가)** 배선 — 씬 오브젝트 · 머티리얼 생성 위치(`Game.unity` 직접 / 에디터 Setup 메뉴 / 런타임 생성) `[미정 — game-programmer가 구현 때 정하고 보고]` | 선례: MistShrine은 씬 오브젝트 `MistShrineRangeIndicator`(`Game.unity` 28768행) + `SkillSetup_Scene.EnsureOverlayMaterial()` 패턴(mistshrine Plan E5) | Presentation / Editor | — |
| 9 | **(2026-11-09 추가)** 6 또는 7 안 | `[TOWER-RANGE-UI]` 진단 — Show · Hide. `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]` | Presentation | — (WORKFLOW [4] 진단 로그 규칙) |

- **바꾸지 않는 것** `[사실 — Research 6-1]`: `TowerCombatUseCase` 5인자 생성자, `ExecuteTowerAttack(BuildingData, UnitData)` 이름 · 매개변수(Editor 리플렉션), C-6 소스 gate가 찾는 세 줄, `server-tower v=1` END 줄, `ResolveTowerMuzzle`, 공격 표현(EffectManager · 프로필 · 프리셋), RPC, 스키마, `BuildingData`, `BuildingStatsConfig`, AI. 범위 표시(6~9)는 판정 · 서버 · RPC를 건드리지 않는다.
- 위 1 · 2 · 5는 같은 커밋(9절 커밋 1)이다 — 코드만 바뀌고 fixture가 그대로면 회귀 스위트가 깨진다. 6~9는 9절 커밋 3이다.
- **레이어 주의 (범위 표시):** 표시 반경 · 반각은 판정과 **같은 값을 읽어야** 한다(R10). 판정 상수(2번 정면 헬퍼 · 1번 반각 상수)를 Presentation이 읽는 것은 바깥 → 안쪽 방향이라 레이어 규칙과 맞는다 — 판정 상수를 Presentation에서 읽을 수 있게 공개 범위만 조정하는 정도면 된다 `[제안]`.

---

<a id="3-method"></a>
## 3. 항목별 방법

### 3-1. 판정식 `[추천 — 프로그래머]`

- 수평 방향 `d = (유닛 − 탑)`의 XZ 성분(Y 무시)을 단위벡터로, 정면 `f` = Blue (0, 0, 1) · Red (0, 0, −1).
- **`dot(d, f) ≥ 0.5 − 0.001`이면 범위 안**(약 60.07°까지). 헥스 대각 앞 이웃이 60.0007°라 허용 오차가 없으면 빠진다(Research 2-1).
- 거리가 0에 가까워 방향을 정할 수 없는 경우(유닛이 탑 중심과 겹침)의 처리는 구현 때 정하고 보고한다 `[제안 — 범위 안으로 보는 쪽이 자연스럽다는 판단, 미결정]`.
- 순서: 사거리 검사 → (Human이면) 각도 검사 → 최단 갱신. 사거리 밖 후보는 각도를 계산하지 않는다.

### 3-2. 좌표 정규화 `[추천 — 프로그래머]`

- 탑 위치 · 유닛 위치는 Transform(화면 좌표)에서 온다. 서버(Host)는 Blue라 화면 = 도메인이지만, 싱글에서 `LocalPlayerTeam`이 Red로 남는 경우(미검증, Research 1-5)를 막기 위해 **방향 계산 직전에만** `NormalizeToDomainPosition`을 적용한다. 거리 계산은 점대칭이라 그대로 둔다.
- fixture 변환기는 항등(`p => p`)이라 fixture 결과는 바뀌지 않는다 `[사실]`.

### 3-3. 대기 · 쿨다운

- **쿨다운은 공격 여부와 관계없이 매 Tick 줄어든다**(`TowerCombatUseCase.cs` 174~176행, 0 아래로 내려가지 않음) `[사실 — 문서 담당 재확인]`. 그래서 「공격하지 않고 기다리는 동안」에도 쿨다운은 멈추지 않는다.
- 전방 범위 안 후보가 없으면 `FindNearestEnemyUnit`이 null → 기존 196~197행 흐름대로 `continue` — 쏘지 않고, 쿨다운도 **다시 채우지 않는다**(재충전은 실제 발사 때만 — 213~214행) `[사실 — 문서 담당 재확인]`. **코드 추가 없음.**
- 그래서 쿨다운은 0까지 줄어 0에서 머물고, 적이 범위에 들어오는 첫 Tick에 바로 발사한다 — **현행 동작 유지**(7-1 Q6).
- *(2026-11-09 정정 — 이 절 종전 문구: 「전방 범위 안 후보가 없으면 `FindNearestEnemyUnit`이 null → 기존 196~197행 흐름대로 `continue`, 쿨다운 시작 없음 `[사실]`. **코드 추가 없음.**」 · 「쿨다운이 이미 0이면 0으로 남아, 적이 범위에 들어오는 첫 Tick에 바로 발사한다.」 — 사용자가 Q6 「쿨다운은 그대로 둡니다」를 「쿨다운이 줄지 않는다」로 읽을 수 있다고 지적해 표현을 바꿨다. 동작 자체는 같다.)*

### 3-4. 진단 로그

5절 대응표대로. 매 Tick 줄을 남기지 않고 **상태가 바뀔 때만**: 발사(Fire), 대기 시작(HoldBegin — 쿨다운 0 · 사거리 안 적 ≥ 1 · 범위 안 0이 처음 성립한 순간, 탑별 1회), 대기 끝(HoldEnd — 발사 / 사거리 안 적 0 / 탑 파괴 중 이유), 경기 종료(End). Fire 상세는 경기당 앞 N줄(예: 40)만, 이후는 End 수에만 반영(`DetailOmitted`). 로그 머리 `[TOWER-ARC]`, 버전 키 `DiagRev=1`(WORKFLOW [4] 「회차 구분 접두어와 버전 키」). 범위 표시 로그 `[TOWER-RANGE-UI]`는 3-5.

<a id="3-5-range-indicator"></a>
### 3-5. 탑 선택 시 공격 범위 표시 (2026-11-09 추가) `[제안 — 세부는 game-programmer가 구현 때 정하고 보고]`

사용자 결정(7-1 범위 추가)에 따라 이번 작업에 넣는다. **화면에만 그리는 표시이며 공격 판정에 영향이 없다.**

- **언제 보이나:** 방어탑(AutoTower)을 선택해 액션 패널이 열려 있는 동안만. 선택 해제 · 다른 건물 선택 · 탑 파괴 · 경기 종료 때 숨긴다(선례 — 「MistShrine 패널 UI」 규칙 8 「패널이 열려 있는 동안에만」). 탑이 파괴될 때 패널이 함께 닫히는지는 구현 때 확인하고, 닫히지 않으면 파괴 때 따로 숨긴다 `[미확인]`.
- **무엇을 그리나:** 탑 3종 = 사거리 원(반투명 채움 + 외곽선 — 「MistShrine 패널 UI」 규칙 12 선례). Human CannonTower = 같은 원 + 그 안의 **앞 120° 부채꼴을 더 진하게** 강조. 색 · 두께는 임시값(조정은 사용자).
- **반경 = 판정 사거리:** 판정이 쓰는 값(종족별 `AttackRange` × `TileHeight` — `TowerCombatUseCase.cs` 226~234행)을 **같은 경로로** 읽는다. 패널이 자기 값을 따로 들면 설정만 바꿨을 때 「보이는 원」과 「실제 사거리」가 조용히 어긋난다(선례 주석 `MistShrinePanelUI.cs` 292행 위 「반경 주의」) `[사실 — 행 번호 문서 담당 재확인]`.
- **부채꼴 방향 = 판정 정면:** 정면 · 반각도 판정과 같은 상수(2절 1 · 2)를 읽는다. 판정은 도메인 좌표(Blue +Z / Red −Z)이고, 화면에 놓을 때는 **뷰 좌표로 바꿔야** 한다 — Red 화면은 맵을 뒤집어 보여 주므로 변환을 빠뜨리면 원 위치 · 부채꼴 방향이 틀어진다(선례 주석 `MistShrinePanelUI.cs` 292행 위 「좌표 주의」). 변환 뒤 각 화면에서 부채꼴이 포신이 향한 쪽과 겹쳐 보여야 한다(S11).
- **누구의 탑:** 내 탑만 대상으로 본다 `[제안 — 선례 「아군 MistShrine」]`. 지금 입력이 상대 탑 선택 때 패널을 여는지는 구현 때 확인하고 보고한다 — 열지 않으면 해당 없음(7-1 U-1).
- **진단 로그 `[TOWER-RANGE-UI]`:** Show · Hide 두 가지, **상태가 바뀔 때만**(매 프레임 금지 — LogRules §1.14 금지 8). 표시 반경과 판정 사거리를 같은 줄에 남겨 **일치 여부를 로그로 판정**한다. 모양(원 · 부채꼴이 보이는지, 방향이 맞는지)은 **육안으로만** 판정한다(5절 T12~T16). 화면 표시라 **Client 로그에도 이 줄이 남는 것이 정상**이다(서버 전용인 `[TOWER-ARC]`와 다르다). 버전 키 `DiagRev=1`, 존속 `개발`(Q11과 같게) `[제안]`.

---

<a id="4-test-scenarios"></a>
## 4. 테스트 시나리오 `[제안]`

| # | 모드 | 절차 | 관측 |
|---|---|---|---|
| S1 | 싱글(Human 선택, AI 아무 종족) | 탑을 성 앞(상대 쪽)에 배치, 적 유닛이 정면에서 접근 | 정상 발사 · `Fire InArc=true` |
| S2 | 싱글 | 탑을 전진 배치해 적 유닛이 옆을 지나 탑 뒤(성 쪽)로 가게 함 — 성을 치는 적이 탑 뒤에 서는 배치 | 뒤 적에게 안 쏨 · `HoldBegin` · 포탄이 뒤로 나가는 장면 0 |
| S3 | 싱글 | S2 상태에서 새 적이 정면으로 들어옴 | 바로 발사 · `HoldEnd Reason=Fire` |
| S4 | 싱글 | 앞 · 뒤 동시(뒤가 더 가까움) | 앞 적 조준 · `ExcludedCloser≥1` |
| S5 | 멀티(Host Blue · Client Red 둘 다 Human) | 각자 탑 배치 | 양쪽 화면 모두 뒤로 쏘는 장면 0 · Host 로그 `Team=Red` 줄 |
| S6 | 싱글 Spirit(선택) | RuneSpire 뒤에 적 | 여전히 공격(360°) |
| S7 | 에디터 | `Hexiege/Combat/Run Full Regression Suite` | PASS(사용자가 실패를 말하지 않으면 PASS — WORKFLOW 「[6] 사용자 테스트」, 결과 파일 없음 · Console만) |
| S8 | 싱글 Human (2026-11-09 추가) | 내 대포탑을 눌러 선택 → 다른 곳을 눌러 해제 | 선택 중 사거리 원 + 앞 120° 부채꼴이 보이고, 부채꼴이 포신 쪽(화면 위)과 겹침 · 해제하면 사라짐(육안) · `[TOWER-RANGE-UI] Show` `RadiusMatch=true` → `Hide Reason=Deselect` |
| S9 | 싱글 Spirit · Transcendence (추가, 선택) | 룬 첨탑 · 덩굴 타워 선택 | 원만 보이고 부채꼴 없음(육안) · `Show HasArc=false, RadiusMatch=true` |
| S10 | 싱글 (추가) | 탑을 선택한 채로 탑이 파괴됨 · 선택한 채 경기 종료 | 표시가 남지 않음(육안) · `Hide Reason=Destroyed` / `GameEnd` |
| S11 | 멀티 Client Red Human (추가) | Client 화면에서 내 대포탑 선택 | 원이 탑 위치에, 부채꼴이 포신 쪽(내 화면 위)에 보임(육안) · Client 로그 `Show Team=Red, RadiusMatch=true` |
| S12 | 싱글 (추가) | 탑 선택 → 바로 다른 탑 · 다른 건물 선택 | 이전 탑 표시가 사라지고 새 탑에만 보임(육안) · `Hide` 다음 `Show`(다른 `TowerId`) |

- 싱글 시험 가능 `[사실 — Research 6-2]`: AI는 방어탑을 짓지 않으므로 「내 탑 vs AI 유닛」이다. Red 인간 탑은 멀티 Client 쪽에서만 실기로 볼 수 있다.
- S2 · S4의 「적이 탑 뒤에 서는」 배치는 AI 경로에 좌우돼 매번 재현된다는 보장이 없다 `[추정]` — `.claude/mistakes.md` 2026-09-09 「관측 불가능한 체크리스트」 재발 방지를 위해 **판정은 fixture (a)~(f)로 보장하고, 실기는 육안 확인 위주**로 둔다. 실기에서 S2 · S4가 나오지 않으면 「이번 회차 미관측」으로 기록한다.

---

<a id="5-test-log-map"></a>
## 5. 테스트 항목 ↔ 진단 로그 대응표 `[TOWER-ARC]` (WORKFLOW [4] 「테스트 항목 진단 로그 규칙」)

공통 `[제안]`: `GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] <Event>", "Diag=TowerFrontArc, DiagRev=1, Role=<Single|Host>, …")`. 서버 전용 판정이라 **Client 로그에는 `[TOWER-ARC]` 0줄이 정상**이다. 존속 제안: **`개발`**(Q11, EMP `[EMP-DIAG]` 선례). 작성은 [LogRules](../../../LogRules.md)(§1.2 · §1.4 · §1.5 · §1.14 금지 1 · 8)를 따른다.

| 테스트 항목 | 로그 이름(카테고리 · 이벤트 키) | 남기는 시점(상태 변화) | 정상 판정 값(`key=value`) |
|---|---|---|---|
| T1 앞쪽 적 공격 (S1) | `Combat` · `[TOWER-ARC] Fire` | Human 탑이 실제로 발사할 때마다(쿨다운 5초라 탑당 최대 0.2줄/초). 경기당 앞 N줄(예: 40)만 상세, 이후는 End 수에만 반영(`DetailOmitted`) | `Race=Human, InArc=true, AngleDeg≤60.07`, `Team`, `TowerId`, `TargetId`, `Dist` |
| T2 뒤에만 적 → 안 쏨 (S2) | `Combat` · `[TOWER-ARC] HoldBegin` | 쿨다운 0 · 사거리 안 적 ≥ 1 · 범위 안 0이 **처음 성립한 순간**(탑별 1회) | `RangeEnemies≥1, ArcEnemies=0, NearestAngleDeg>60` |
| T3 앞으로 들어오면 바로 발사 (S3) | `Combat` · `[TOWER-ARC] HoldEnd` + 직후 `Fire` | 대기가 풀린 순간(이유: 발사 / 사거리 안 적 0 / 탑 파괴) | `Reason=Fire, HeldTicks=n, HeldMs=…`, 같은 `TowerId`의 `Fire`가 같은 시각 |
| T4 앞 · 뒤 동시 → 앞 적 선택 (S4) | `Combat` · `[TOWER-ARC] Fire`의 칸 | 발사 때 | `ExcludedCloser≥1`(선택 대상보다 가깝지만 범위 밖이라 뺀 적 수) |
| T5 경계 60° | `Fire`의 `AngleDeg` · End의 `MaxFireAngleDeg` | 발사 · 경기 종료 | `MaxFireAngleDeg≤60.07`. **실기에서 경계 상황을 일부러 만들기 어렵다** → 판정은 fixture (c) · (d)로, 실기 로그는 보조 |
| T6 Red 탑 (S5) | `Combat` · `[TOWER-ARC] Fire` `Team=Red` | Host 로그 | Red 탑 `AngleDeg≤60.07, InArc=true` |
| T7 RuneSpire · VineTower 360° 유지 (S6) | `Combat` · `[TOWER-ARC] End` 종족별 `outsideArcFires` | 경기 종료 | `Human:outsideArcFires=0`. Spirit · Transcendence는 0 이상 아무 값(제한 없음의 증거) |
| T8 발사 뒤 대상 이탈해도 명중 | 기존 `[PROJECTILE-END]` `byTower=…hit:` | 기존 | 🔴 **새 로그로는 판정 불가** — 착탄 때 각도를 재지 않는다. 기존 hit 수로 간접 확인만 된다 — 사용자에게 알린다 |
| T9 EMP 공존 | 기존 `[EMP-DIAG]` + `[TOWER-ARC] Fire` | 기존 | 무력화 중인 탑의 `Fire` 0 |
| T10 회귀 스위트 (S7) | — | — | 🔴 **로그로 판정 불가** — 결과는 Console에만 나오고 RuntimeLog에 자체 검증 줄이 없다(공용 메모리 「Regression suite verdict rule」). 사용자 보고로 판정 |
| T11 양쪽 화면에 뒤로 쏘는 장면 0 (S5 육안) | `Fire` `InArc=true`(서버 판정) | — | 🔴 **화면 표현 자체는 로그로 판정 불가** — 서버가 범위 안 대상만 골랐다는 것까지만 로그로 보이고, 화면의 궤적(Homing 휨 — R7)은 육안으로만 본다 |
| 종합 | `Combat` · `[TOWER-ARC] End` | 경기 종료 1회(싱글 · 멀티 서버 두 경로 — 중복 방지 플래그) | 종족별 `fires, inArcFires, outsideArcFires, holdEpisodes, heldTicks, excludedCandidates, maxFireAngleDeg`, `verdict=PASS`(= `Human:outsideArcFires=0`) |

**범위 표시 `[TOWER-RANGE-UI]` (2026-11-09 추가)** `[제안]`: `GameLog.Dev.Info("UI", <표시 컴포넌트 이름>, "[TOWER-RANGE-UI] <Show|Hide>", "Diag=TowerRangeUI, DiagRev=1, Role=<Single|Host|Client>, …")`. 카테고리 이름은 구현 때 기존 UI 로그 카테고리에 맞춘다. **화면 표시라 Host · Client 둘 다 남는 것이 정상.** 선택 · 해제 때만 1줄(매 프레임 금지).

| 테스트 항목 | 로그 이름(카테고리 · 이벤트 키) | 남기는 시점(상태 변화) | 정상 판정 값(`key=value`) |
|---|---|---|---|
| T12 선택하면 표시 (S8 · S9) | `UI` · `[TOWER-RANGE-UI] Show` | 탑 선택으로 표시를 켤 때 1줄 | `TowerId`, `Race`, `Team`, `ShownRadius`, `JudgeRadius`, **`RadiusMatch=true`**(두 값 차이 ≤ 0.001), Human `HasArc=true, ArcHalfDeg=60`, Spirit · Transcendence `HasArc=false` |
| T13 해제 · 전환 · 파괴 · 종료 때 숨김 (S8 · S10 · S12) | `UI` · `[TOWER-RANGE-UI] Hide` | 표시를 끌 때 1줄 | `Reason=Deselect / Switch / Destroyed / GameEnd`, 같은 `TowerId`의 `Show` 뒤에 짝으로 나옴 · 숨김 없이 남은 표시 0(경기 종료 때 열린 Show 수 = 0) |
| T14 Red 화면 위치 · 방향 (S11) | `Show` `Team=Red` (Client 로그) | 선택 때 | `RadiusMatch=true`. 🔴 **위치 · 부채꼴 방향 자체는 로그로 판정 불가** — 뷰 변환 결과는 육안으로만 본다 |
| T15 원 · 부채꼴 모양 (S8 · S9) | — | — | 🔴 **로그로 판정 불가** — 「보이는지 · 모양이 맞는지 · 포신 쪽과 겹치는지」는 육안 |
| T16 판정과 표시가 같은 범위 | `Show`의 `RadiusMatch` + `[TOWER-ARC] Fire`의 `Dist` · `AngleDeg` | — | 반경 일치는 로그로 판정. **부채꼴 경계와 실제 발사 경계가 화면에서 겹치는지는 육안 보조** — 경계 판정 자체는 fixture (c) · (d) |

---

<a id="6-risks"></a>
## 6. 위험

| # | 위험 | 대응 `[제안]` |
|---|---|---|
| R1 | C-2 Red fixture가 깨진다(Research 6-1) | 코드와 같은 커밋에서 fixture 위치 값 변경(최상단 표) |
| R2 | 대각 축 타일이 60.0007°라 엄격 비교면 빠진다 | 허용 오차(cos 0.5 − 0.001) + 경계 fixture (c) · (d) |
| R3 | 반전된 서버(싱글 Red 잔존 가능성 — Research 1-5, 미검증)에서 방향이 정반대로 판정된다 | 방향 계산 전 `NormalizeToDomainPosition`. `LocalPlayerTeam.Reset` 0건 자체는 **범위 밖 — 보고만**(Q14) |
| R4 | C-6 소스 gate 문자열 · `ExecuteTowerAttack` 시그니처 · 5인자 생성자 | 건드리지 않는다. 필터는 `FindNearestEnemyUnit` 안 또는 새 private 헬퍼 |
| R5 | 인간 탑 실질 화력 감소(덮는 면적 1/3) | 밸런스는 game-design-lead · 사용자 — 이번 범위 밖(Q9) |
| R6 | 「앞」 정의가 두 곳(`ResolveTowerMuzzle` 부호 · 새 판정)에 생겨, 한쪽만 바뀌면 어긋난다 | 정면 벡터 헬퍼 1개를 새 판정이 쓴다. `ResolveTowerMuzzle`을 그 헬퍼로 바꾸는 것은 범위 확장이라 사용자 확인 시에만 |
| R7 | Homing 비행 중 대상이 뒤로 돌면 궤적이 휜다 | 표현 현상 — 관찰만 |
| R8 | 탑에 붙은 유닛은 작은 이동에도 각도가 크게 변해 안 · 밖이 바뀐다 | 정상 동작(결함 아님). 근접 예외는 Q10 |
| R9 | 싱글 실기에서 S2 · S4가 재현되지 않을 수 있다 | fixture로 판정 보장, 실기 미관측은 그대로 기록(4절) |
| R10 (추가) | 표시 반경 · 반각이 판정 값과 따로 놀아, 설정만 바꿨을 때 「보이는 범위」와 「실제 범위」가 조용히 어긋난다 | 판정과 같은 경로 · 상수를 읽는다(3-5) + `RadiusMatch` 로그 |
| R11 (추가) | Red 화면(뒤집힌 뷰)에서 원 위치 · 부채꼴 방향이 틀어진다 | 도메인 → 뷰 변환(선례 MistShrine 「좌표 주의」) + S11 육안 |
| R12 (추가) | 비활성으로 저장된 표시 오브젝트가 `Awake`에서 스스로 꺼져 영영 안 보이는 함정 · 기준 지름 오류(MistShrine 선례 버그) | 선례 수정(mistshrine Plan §9-1 · §9-3) 방식을 따른다 |
| R13 (추가) | 탑 파괴 · 경기 종료 때 표시가 남는다 | 숨김 이유 `Destroyed` · `GameEnd` 경로 + T13 「열린 Show 수 = 0」 |

---

<a id="7-user-questions"></a>
## 7. 사용자 확인 질문 (추천 포함)

| # | 질문 | 선택지 | 추천 |
|---|---|---|---|
| Q1 | 적용 범위 | (a) Human CannonTower만 (b) 탑 3종 모두 | **(a)** — 방향 있는 모델은 Human뿐 |
| Q2 | 각도 기준점 | (a) 탑 중심 (b) 포구 | **(a)** — 사거리와 같은 기준, 헥스 축과 경계 일치 |
| Q3 | 「정면」 정의 | (a) 팀 진영 고정 Blue +Z / Red −Z (b) 배치 때 선택 (c) 적 성 방향 (d) 실시간 회전 | **(a)** — 모델 방향과 일치, 데이터 · RPC 변경 0 |
| Q4 | 경계 60° | (a) 포함 + 작은 허용 오차 (b) 엄격 | **(a)** — 엄격하면 대각 앞 타일 전체가 빠진다 |
| Q5 | 선택 의미 | (a) 범위 안 적 중 최단 (b) 전체 최단이 뒤면 침묵 | **(a)** |
| Q6 | 뒤에만 적 | (a) 안 쏘고 대기. 공격하지 않는 동안에도 쿨다운은 계속 줄어 0에서 대기하다, 앞 120°에 들어오면 즉시 발사(현행 동작 유지) (b) 다른 동작 *(2026-11-09 문구 정정 — 종전 (a): 「안 쏘고 대기, 쿨다운 유지」)* | **(a)** |
| Q7 | 발사 뒤 대상 이탈 | (a) 그대로 명중(현행) (b) 취소 | **(a)** — 규칙 12 · 서버 투사체 계약 무변경 |
| Q8 | 120° 값 보관 | (a) 코드 상수 (b) `BuildingStatsConfig` 칸(Inspector 조정) | **(a)** — 표 구조 · fixture 표 주입(`ServerProjectiles.cs` 584~593행) 변경 없음. 튜닝이 필요해지면 (b) |
| Q9 | 밸런스 보정(공격력 · 사거리) | 지금 함 / 나중 | **나중**(game-design-lead) |
| Q10 | 탑에 아주 붙은 적 예외 | 없음 / 근접 반경 안 360° | **없음**(요청 범위 밖, 필요하면 후속) |
| Q11 | 진단 로그 존속 | `개발` / `임시` | **`개발`**(EMP 선례) |
| Q12 | 스키마 · 접속 확인 | 올리지 않음 / 올림 | **올리지 않음**(판정 서버 전용, 표현 · RPC 무변경) |
| Q13 | 규칙 문안 | 규칙 2 · 11 단서 + 규칙 14 신설 / 규칙 2 본문 개정만 | **규칙 14 신설**(Human 예외를 한 곳에 모음) |
| Q14 | 범위 밖 발견 — `LocalPlayerTeam.Reset` 호출 0건(2026-10-13 랠리 Plan 86행에 이미 기록됨) | 별도 조사 Task로 / 보고만 | **보고만**, 착수는 사용자 결정(이번에는 R3 정규화로 판정만 보호) |

<a id="7-1-user-decisions"></a>
### 7-1. 사용자 결정 (2026-11-09) `[전달 — 메인 세션]`

**원문 1 (Q 답변):** 「'쏘지 않고 대기. 쿨다운은 그대로 둡니다' 쿨다운이 줄어들지 않는다는 의미인가? 공격하지 않는 상태이면 쿨다운은 줄어야 하지 않을까? 그 외에는 추천대로 진행」
→ 메인 세션 답변(코드 확인 — `TowerCombatUseCase.cs` 174~176행 매 Tick 감소, 196~197행 표적이 없으면 쏘지 않고 재충전도 안 함, 213~214행 실제 발사 때만 재충전 — 문서 담당이 같은 행을 다시 읽어 확인): **공격하지 않는 동안에도 쿨다운은 계속 줄어, 0에서 대기하다 앞 120°에 들어오면 즉시 발사한다 — 현행 동작 유지.** 사용자의 기대와 코드 동작이 같으므로 동작은 바꾸지 않고 **Q6 문구만 정정**했다(1절 · 3-3 · 7절).

**원문 2 (범위 표시):** 「포탑을 선택하면 공격 범위를 표시하는 범위가 나타나면 좋겠는데 어떻게 생각해?」 → 메인 세션 제안(선례 `MistShrineRangeIndicator.cs` · `MistShrinePanelUI.cs` 245 · 256 · 292행, `BuildingActionPanelUI.cs` 18행 「AutoTower 사거리 표시 토글은 아직 없으며」): (가) 이번 작업에 포함 — 탑 3종 사거리 원 + 인간 대포탑 앞 120° 부채꼴 강조 → 사용자 **「가로 진행하자. 문서 갱신하고 바로 작업 진행해. 완료되면 푸시하고 테스트 절차 안내해. 로그도 확인하는 게 좋을 것 같으니 로그도 추가하고, 업로드 이름도 안내해 줘」**.

| # | 결정 | 고른 안 | 반영 위치 |
|---|---|---|---|
| Q1 | 적용 범위 | (a) Human CannonTower만 — 추천 | 2절 1 |
| Q2 | 각도 기준점 | (a) 탑 중심 — 추천 | 3-1 |
| Q3 | 「정면」 | (a) 팀 진영 고정 Blue +Z / Red −Z — 추천 | 2절 2 · 3-1 |
| Q4 | 경계 60° | (a) 포함 + 허용 오차(cos 0.5 − 0.001) — 추천 | 3-1 |
| Q5 | 선택 의미 | (a) 범위 안 적 중 최단 — 추천 | 3-1 |
| Q6 | 뒤에만 적 | (a) 쏘지 않고 대기, **쿨다운은 계속 줄어 0에서 대기 → 앞 120° 진입 즉시 발사(현행 유지)** — 추천(문구 정정) | 3-3 |
| Q7 | 발사 뒤 대상 이탈 | (a) 그대로 명중 — 추천 | — (변경 없음) |
| Q8 | 값 보관 | (a) 코드 상수 — 추천 | 2절 1 |
| Q9 | 밸런스 보정 | 나중(game-design-lead) — 추천 | 10절 |
| Q10 | 근접 예외 | 없음 — 추천 | — |
| Q11 | 진단 로그 존속 | `개발` — 추천. `[TOWER-RANGE-UI]`도 같게 `[제안]` | 5절 |
| Q12 | 스키마 | 올리지 않음(8 유지) — 추천 | — |
| Q13 | 규칙 문안 | 규칙 14 신설 — 추천 | 8절 |
| Q14 | `LocalPlayerTeam.Reset` 0건 | 보고만 — 추천 | 10절 |
| 범위 추가 | 탑 선택 시 공격 범위 표시 | **(가) 이번 작업에 포함** — 탑 3종 사거리 원 + Human 앞 120° 부채꼴 강조, 화면 전용 · 판정 무관 | 2절 6~9 · 3-5 · 4절 S8~S12 · 5절 T12~T16 · 9절 커밋 3 |
| 진행 | 구현 · 푸시 · 테스트 안내 · 로그 · 업로드 이름 | 문서 갱신 뒤 바로 구현, 완료 뒤 푸시 · 테스트 절차 안내. 로그 업로드 작업명 **`cannon-tower-front-arc`**(WORKFLOW 「[6] 사용자 테스트」) | 머리 「현재 상태」 · 문서 끝 |

**구현 중 확인할 열린 항목 (결정 아님 — 구현 보고에서 답한다):**

| # | 항목 | 왜 열려 있나 |
|---|---|---|
| U-1 | 상대 탑을 선택했을 때 범위를 보일지 | 지금 입력이 상대 건물 패널을 여는지 확인되지 않았다 `[미확인]`. 열지 않으면 해당 없음. 연다면 메인 세션이 사용자에게 묻는다(CLAUDE.md 규칙 12) |
| U-2 | 탑 파괴 때 패널이 함께 닫히는지 | 닫히지 않으면 파괴 때 따로 숨겨야 한다(3-5 · R13) `[미확인]` |
| U-3 | 표시 오브젝트 · 머티리얼 배선 방식 | 2절 8 `[미정]` |
| U-4 | 유닛이 탑 중심과 겹칠 때 각도 처리 | 3-1 「구현 때 정하고 보고」 그대로 |

---

<a id="8-post-test-rules"></a>
## 8. 사용자 테스트 통과 뒤 반영할 규칙 · 문서 `[제안 — 이번 구현에서는 고치지 않는다]`

규칙 문안은 Research 5절 개정안이 출발점이며, Q13 결정과 실제 구현에 맞춰 다듬는다. **사용자 테스트 통과 뒤** 반영한다.

| # | 문서 | 위치 | 할 일 |
|---|---|---|---|
| 1 | `GameSystemRules_Buildings.md` | 「방어 타워 시스템」 규칙 2(112~113행) | 「단, Human CannonTower는 전방 사격 범위(규칙 14) 안의 적 유닛 중에서만 고른다.」 단서 |
| 2 | 같은 문서 | 규칙 11(180~187행) | 「정면」 도메인 정의(Blue +Z / Red −Z) 1문장 |
| 3 | 같은 문서 | 규칙 13 뒤 | **규칙 14 「Human CannonTower 전방 사격 범위」 신설**(Research 5절 ①~⑥) |
| 4 | `ROADMAP.md` | 29행 | 완료 시 행 삭제(ROADMAP은 이력 문서가 아니다) |
| 5 | 서버 권위 투사체 Plan 9절 F-120 · Research 15-9 | 해당 행 | 「미착수」 → 이 Task로 이어졌다는 날짜 조각 |
| 6 | `GameDesignDocument.md` 152행~ · `StatsReference.md` 174행 | 「4. 방어 타워」 · 타워 행 | 각도 언급 필요 여부 확인 |
| 7 | PROJECT_STATUS · WORK_HISTORY · 메모리 | — | WORKFLOW [9] · [11] 절차대로 |
| 8 (추가) | `GameSystemRules_BuildingPanelsUI.md` (또는 `GameSystemRules_Buildings.md` 「방어 타워 시스템」 — 어느 문서에 둘지 반영 때 사용자 확인) | 해당 절 끝 다음 번호 | **방어탑 범위 표시 규칙 신설** — 선택 중에만 · 탑 3종 사거리 원 · Human 앞 120° 부채꼴 강조 · 반경 = 판정 사거리 · 화면 전용. 구현 결과에 맞춰 다듬는다 |
| 9 (추가) | Research 1-2 「사거리 표시 UI — 없다」 행 | 해당 행 | 이번 Task 기록이라 소급 수정하지 않고, 이번 갱신에서 날짜 조각만 붙였다(Research 1-2) |

---

<a id="9-commits"></a>
## 9. 커밋 분할 — 4단위 `[추천 — 프로그래머 · 커밋 3은 2026-11-09 범위 추가]`

*(2026-11-09 — 종전 「3단위」. 범위 표시를 커밋 3으로 넣고, 종전 커밋 3(규칙 반영)은 커밋 4가 됐다.)*

| 커밋 | 내용 | 비고 |
|---|---|---|
| 1 | 판정: Human 한정 전방 120° 필터(정면 헬퍼 · 도메인 정규화 · 허용 오차 상수) + C-2 Red fixture 위치 값 변경 + 새 fixture (a)~(f) | 기존 로직 제거 없음(최상단 표). 회귀 스위트가 이 커밋 단독으로 녹색이어야 한다 |
| 2 | 진단 로그 `[TOWER-ARC]` Fire · HoldBegin · HoldEnd · End | 판정과 무관, Conditional(릴리스 제외) |
| 3 | **(추가)** 탑 선택 시 범위 표시(2절 6~8) + `[TOWER-RANGE-UI]` Show · Hide(2절 9) | 화면 전용 · 판정 무관. 판정 상수 · 사거리 경로를 읽기만 한다(R10). 씬 · 머티리얼 변경이 있으면 그 파일 목록을 구현 보고에 적는다 |
| 4 | (사용자 테스트 PASS 뒤) 규칙 2 · 11 단서 + 규칙 14 신설 + 범위 표시 규칙 신설, ROADMAP 29행 · F-120 상태 갱신(8절) — *종전 커밋 3* | document-manager · `python3 Tools/check_docs.py` 0건 |

- 커밋 1~3 뒤 **푸시하고 메인 세션이 테스트 절차를 안내**한다(사용자 지시 2026-11-09). 안내 끝에 「로그 올리기: 작업명 `cannon-tower-front-arc`」를 적는다(WORKFLOW 「[6] 사용자 테스트」).

---

<a id="10-not-doing"></a>
## 10. 하지 않는 것

- 탑 · 포신 실시간 회전, 배치 때 방향 선택. *(2026-11-09 — 종전 이 줄 끝의 「사거리 · 각도 표시 UI」는 사용자 결정으로 이번 범위에 들어왔다 → 3-5.)*
- 배치 전(건설 미리보기) 범위 표시 — 이번 결정은 「선택하면」 표시다.
- 밸런스 보정(공격력 · 사거리 · 쿨다운) — Q9.
- RuneSpire · VineTower 동작 변경 — Q1.
- 표현 코드 · RPC · 스키마 변경 — Q12.
- `ResolveTowerMuzzle` 정리(R6), `LocalPlayerTeam.Reset` 호출 추가(Q14).
- 규칙 문서 수정(8절 — 테스트 통과 뒤).

---

## 11. 변경 이력

| 날짜 | 내용 | 근거 |
|---|---|---|
| 2026-11-09 | 작성. game-programmer 읽기 전용 조사의 추천안 · 커밋 분할 · 진단 로그 설계 · 테스트 시나리오 · 위험 · 질문을 옮김. 대응표에 로그로 판정할 수 없는 항목 T10(회귀 스위트) · T11(화면 궤적)을 이유와 함께 추가 | 원자료(scratchpad `cannon120-research.md`) `[전달 — 메인 세션]` · [Research 10절](Research.md) |
| 2026-11-09 | **사용자 결정 반영 · 범위 추가.** 7-1 결정 표 신설(Q1~Q14 전부 추천안, Q6은 문구 정정 — 쿨다운은 공격하지 않는 동안에도 계속 줄어 0에서 대기 · 현행 유지, 종전 문구는 날짜 조각으로 보존). 범위 추가: 탑 선택 시 공격 범위 표시(탑 3종 원 + Human 앞 120° 부채꼴, 화면 전용) — 머리말 · 0절 선례 행 · 1절 · 2절 6~9 · 3-5 · 4절 S8~S12 · 5절 `[TOWER-RANGE-UI]` T12~T16 · 6절 R10~R13 · 8절 8~9 · 9절 커밋 3(종전 3 → 4) · 10절. 구현 중 열린 항목 U-1~U-4. 상태 「구현 승인 — 진행 중」, 로그 업로드 작업명 `cannon-tower-front-arc`. 문서 담당이 `TowerCombatUseCase.cs` 170~216 · 226~234행, `MistShrinePanelUI.cs` 240~300행, `BuildingActionPanelUI.cs` 14~22 · 160행, `GameSystemRules_BuildingPanelsUI.md` 「MistShrine 패널 UI」 제목, WORKFLOW 282~285행을 다시 읽음 | 사용자 원문 2건 `[전달 — 메인 세션]` |

---

완료 후 main 병합(사용자 지시 — 2026-11-09 「작업 완료되면 병합할 거니까 알고 있어」). 구현이 끝나면 푸시하고 테스트 절차를 안내한다 — **로그 올리기: 작업명 `cannon-tower-front-arc`**(사용자 지시 2026-11-09).
