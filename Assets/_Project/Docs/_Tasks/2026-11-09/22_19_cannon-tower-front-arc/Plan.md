# Plan — 인간 방어탑(CannonTower) 전방 120° 공격 제한

## 이 문서는 무엇인가

인간 대포탑이 **포신이 향한 정면 좌우 60°씩(합 120°) 안의 적만** 공격하게 바꾸는 계획이다. 이 계획대로 하면 다음처럼 동작한다.

- 내 대포탑은 상대 진영 쪽(화면 위쪽)을 바라보고, 그 앞쪽 부채꼴 안에 들어온 적 중 **가장 가까운 적**을 쏜다.
- 적이 탑 **뒤나 옆 바깥**에만 있으면 쏘지 않고 기다린다. 기다리는 동안 재장전 시간은 그대로 흐르므로, 적이 앞쪽으로 들어오는 순간 바로 쏜다.
- 앞에도 적이 있고 뒤에 더 가까운 적이 있으면 **앞의 적**을 쏜다.
- 이미 쏜 포탄은 대상이 옆 · 뒤로 빠져도 지금처럼 날아가 맞는다.
- 정령 룬 첨탑 · 초월 덩굴 타워는 지금처럼 모든 방향을 쏜다.

바뀌는 곳은 **서버가 공격 대상을 고르는 판정 한 곳**이다. 화면 연출 · 통신 형식 · 접속 확인 번호(스키마)는 바뀌지 않는다. 테스트 때 「정말 뒤로는 안 쏘는지」를 눈으로만 판단하기 어렵기 때문에 `[TOWER-ARC]` 진단 로그를 함께 넣는다(5절).

현재 코드 상태와 근거(파일 · 행)는 [Research.md](Research.md)에 있다. 추천안은 game-programmer가 코드를 읽고 낸 것이며, **사용자 결정이 아니다.** 7절의 사용자 확인 Q1~Q14를 정한 뒤 이 Plan을 사용자가 승인해야 구현을 시작한다(WORKFLOW [4]).

> **현재 상태 (2026-11-09):** **사용자 확인 Q1~Q14 대기, 구현 승인 전.** 코드 · 에셋 · 규칙 문서 무변경.

> **🔴 게임 규칙 변경 — 사용자 승인 대상.** `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 2 · 11에 단서를 붙이고 규칙 14를 새로 만드는 안이다(8절). **규칙 문서는 사용자 테스트 통과 뒤 반영한다** — 이번 작업의 구현 커밋에서는 고치지 않는다.

**연결 기록:** 처음 요청(2026-10-21, 기록만) — [ROADMAP](../../../ROADMAP.md) 29행 · [서버 권위 투사체 Plan 9절 F-120](../../2026-10-19/13_30_server-authoritative-projectile/Plan.md#9-follow-up-items-not-started) · [Research 15-9](../../2026-10-19/13_30_server-authoritative-projectile/Research.md#15-9-front-120-request). 그 Research의 행 번호는 지금 맞지 않는다 — 이 Task Research의 행 번호를 쓴다.

**표기:** `[사실]` = Research에서 확인한 것 · `[추천 — 프로그래머]` = game-programmer 추천(결정 아님) · `[제안]` = 결정 아님 · `[사용자 확인]` = 7절에서 사용자가 정할 것. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Editor/` = `Assets/Editor/`.

> **기존 로직 제거 — WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 최상단에 둔다.**
>
> **코드 제거 · 주석 처리는 없다.** 판정은 기존 최단 거리 탐색 안에 「Human이면 전방 범위 밖 후보를 건너뛴다」는 조건을 **덧붙인다.**
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
| 뒤에만 적 | **쏘지 않고 대기, 쿨다운 유지** | — | Q6 |
| 발사 뒤 대상 이탈 | **그대로 명중(현행)** | 취소 | Q7 |
| 값 보관 | **코드 상수** | `BuildingStatsConfig` 칸 | Q8 |
| 좌표 | 방향 계산 전 **도메인으로 정규화** | 정규화 없음 | Research 1-5 · R3 |
| 스키마 | **올리지 않음**(8 유지) | 올림 | Research 4절 · Q12 |

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

- **바꾸지 않는 것** `[사실 — Research 6-1]`: `TowerCombatUseCase` 5인자 생성자, `ExecuteTowerAttack(BuildingData, UnitData)` 이름 · 매개변수(Editor 리플렉션), C-6 소스 gate가 찾는 세 줄, `server-tower v=1` END 줄, `ResolveTowerMuzzle`, 표현(EffectManager · 프로필 · 프리셋), RPC, 스키마, `BuildingData`, `BuildingStatsConfig`, AI.
- 위 1 · 2 · 5는 같은 커밋(9절 커밋 1)이다 — 코드만 바뀌고 fixture가 그대로면 회귀 스위트가 깨진다.

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

- 전방 범위 안 후보가 없으면 `FindNearestEnemyUnit`이 null → 기존 196~197행 흐름대로 `continue`, 쿨다운 시작 없음 `[사실]`. **코드 추가 없음.**
- 쿨다운이 이미 0이면 0으로 남아, 적이 범위에 들어오는 첫 Tick에 바로 발사한다.

### 3-4. 진단 로그

5절 대응표대로. 매 Tick 줄을 남기지 않고 **상태가 바뀔 때만**: 발사(Fire), 대기 시작(HoldBegin — 쿨다운 0 · 사거리 안 적 ≥ 1 · 범위 안 0이 처음 성립한 순간, 탑별 1회), 대기 끝(HoldEnd — 발사 / 사거리 안 적 0 / 탑 파괴 중 이유), 경기 종료(End). Fire 상세는 경기당 앞 N줄(예: 40)만, 이후는 End 수에만 반영(`DetailOmitted`). 로그 머리 `[TOWER-ARC]`, 버전 키 `DiagRev=1`(WORKFLOW [4] 「회차 구분 접두어와 버전 키」).

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
| Q6 | 뒤에만 적 | (a) 안 쏘고 대기, 쿨다운 유지 (b) 다른 동작 | **(a)** |
| Q7 | 발사 뒤 대상 이탈 | (a) 그대로 명중(현행) (b) 취소 | **(a)** — 규칙 12 · 서버 투사체 계약 무변경 |
| Q8 | 120° 값 보관 | (a) 코드 상수 (b) `BuildingStatsConfig` 칸(Inspector 조정) | **(a)** — 표 구조 · fixture 표 주입(`ServerProjectiles.cs` 584~593행) 변경 없음. 튜닝이 필요해지면 (b) |
| Q9 | 밸런스 보정(공격력 · 사거리) | 지금 함 / 나중 | **나중**(game-design-lead) |
| Q10 | 탑에 아주 붙은 적 예외 | 없음 / 근접 반경 안 360° | **없음**(요청 범위 밖, 필요하면 후속) |
| Q11 | 진단 로그 존속 | `개발` / `임시` | **`개발`**(EMP 선례) |
| Q12 | 스키마 · 접속 확인 | 올리지 않음 / 올림 | **올리지 않음**(판정 서버 전용, 표현 · RPC 무변경) |
| Q13 | 규칙 문안 | 규칙 2 · 11 단서 + 규칙 14 신설 / 규칙 2 본문 개정만 | **규칙 14 신설**(Human 예외를 한 곳에 모음) |
| Q14 | 범위 밖 발견 — `LocalPlayerTeam.Reset` 호출 0건(2026-10-13 랠리 Plan 86행에 이미 기록됨) | 별도 조사 Task로 / 보고만 | **보고만**, 착수는 사용자 결정(이번에는 R3 정규화로 판정만 보호) |

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

---

<a id="9-commits"></a>
## 9. 커밋 분할 — 3단위 `[추천 — 프로그래머]`

| 커밋 | 내용 | 비고 |
|---|---|---|
| 1 | 판정: Human 한정 전방 120° 필터(정면 헬퍼 · 도메인 정규화 · 허용 오차 상수) + C-2 Red fixture 위치 값 변경 + 새 fixture (a)~(f) | 기존 로직 제거 없음(최상단 표). 회귀 스위트가 이 커밋 단독으로 녹색이어야 한다 |
| 2 | 진단 로그 `[TOWER-ARC]` Fire · HoldBegin · HoldEnd · End | 판정과 무관, Conditional(릴리스 제외) |
| 3 | (사용자 테스트 PASS 뒤) 규칙 2 · 11 단서 + 규칙 14 신설, ROADMAP 29행 · F-120 상태 갱신(8절) | document-manager · `python3 Tools/check_docs.py` 0건 |

---

<a id="10-not-doing"></a>
## 10. 하지 않는 것

- 탑 · 포신 실시간 회전, 배치 때 방향 선택, 사거리 · 각도 표시 UI.
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

---

완료 후 main 병합(사용자 지시 — 2026-11-09 「작업 완료되면 병합할 거니까 알고 있어」).
