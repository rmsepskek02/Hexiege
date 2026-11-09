# Research — 인간 방어탑(CannonTower) 전방 120° 공격 제한

## 이 문서는 무엇인가

지금 인간 대포탑은 사거리 안에 적이 있으면 **어느 방향이든** 가장 가까운 적을 쏜다. 그런데 대포 모델은 배치될 때 상대 진영 쪽을 향해 고정되고, 그 뒤로는 돌지 않는다. 그래서 적이 탑 **뒤**에 서 있으면 포탄이 포신 끝에서 나와 뒤쪽으로 날아가는, 어색한 장면이 생긴다.

이번 작업은 인간 대포탑이 **포신이 향한 정면에서 좌우 60°씩, 합쳐 120° 안에 있는 적만** 고르도록 바꾸는 것이다. 뒤쪽에만 적이 있으면 쏘지 않고 기다리다가, 적이 앞쪽으로 들어오는 순간 바로 쏜다. 바뀌는 곳은 **서버가 공격 대상을 고르는 판정 한 곳**이고, 화면 연출과 통신 형식은 바뀌지 않는다. 정령 룬 첨탑 · 초월 덩굴 타워는 방향이 있는 모델이 아니라서 지금처럼 360°를 유지하는 안을 추천한다.

이 문서는 그 판정을 바꾸기 전에 **지금 코드가 어떻게 되어 있는지, 어떤 게임 규칙이 근거인지, 헥스 격자에서 120° 경계가 어디에 떨어지는지, 기존 회귀 테스트 중 무엇이 깨지는지, 멀티 · AI에 어떤 영향이 있는지**를 파일 · 행 단위로 정리한 기록이다. 어떻게 만들지(추천안 · 변경 파일 · 커밋 3단위 · 진단 로그 대응표 · 테스트 · 사용자 확인 Q1~Q14)는 같은 폴더의 [Plan.md](Plan.md)에 있다. **이번 단계는 문서만 만든다** — 코드 · 에셋 · 규칙 문서는 바꾸지 않았다.

> **🔴 게임 규칙이 바뀌는 작업이다.** `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 2(타겟 선택 기준)에 단서가 붙고, 규칙 11(초기 회전)에 「정면」의 도메인 정의가 붙으며, 규칙 14(전방 사격 범위)를 새로 만드는 안이다([5절](#5-related-rules)). 규칙 변경은 사용자 승인 대상이며, **규칙 문서는 사용자 테스트 통과 뒤에 반영한다**(Plan 8절). 이번에는 고치지 않았다.

> **현재 상태 (2026-11-09):** **사용자 확인 Q1~Q14 대기, 구현 승인 전.** 코드 · 에셋 · 규칙 문서 무변경.

**사용자 지시 (2026-11-09, 원문 — 메인 세션 전달):** 「인간 대포탑 전방 120° 공격 제한까지 진행하고 메인에 병합 준비하자」 → 범위 확인 뒤 「(가)를 말한 것이고 task 문서 작성해서 진행할 거야. 작업 완료되면 병합할 거니까 알고 있어」 `[전달 — 메인 세션]`.

**처음 요청 (2026-10-21):** 「전방 120도 영역만 공격 가능하도록 바꿔야 어색하지 않을 것 — 문서에 기록만」. 그때는 기록만 했다 — [ROADMAP](../../../ROADMAP.md) 29행(「후속 — 미착수 · 기록만」), [서버 권위 투사체 Plan 9절 F-120](../../2026-10-19/13_30_server-authoritative-projectile/Plan.md#9-follow-up-items-not-started), [같은 Research 15-9](../../2026-10-19/13_30_server-authoritative-projectile/Research.md#15-9-front-120-request). 🔴 그 Research 15-9의 행 번호(190~235 등)는 그 뒤 EMP 작업으로 밀려 **지금은 맞지 않는다** — 이 문서의 행 번호를 쓴다(`.claude/mistakes.md` 2026-08-24 「행 번호로 위치를 가리켜 전부 어긋남」과 같은 부류). 그 세 곳의 상태 표기는 이번에 고치지 않았다(완료 뒤 문서 반영 대상 — Plan 8절).

**원자료:** game-programmer 읽기 전용 조사(2026-11-09, 메인 세션 전달 — 저장소 파일 수정 0 · git 쓰기 0). 기준 HEAD `1e28526`(`Docs: close EMP task, add tower disable rules`), 작업 트리 깨끗함(`git status` — document-manager 확인).

**표기:** `[확인]` = 프로그래머가 파일을 직접 읽어 확인했고, 문서 담당이 같은 HEAD에서 그 행을 **다시 읽어** 확인한 것 · `[전달]` = 프로그래머가 [확인]으로 보고했으나 문서 담당이 다시 읽지 않은 것 · `[산술]` = 수치 계산으로 확인(문서 담당이 다시 계산) · `[추정]` = 근거는 있으나 실행으로 확인하지 않은 판단 · `[제안]` = 결정 아님, 사용자 · 메인 세션 판단 대상. 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`, `Editor/` = `Assets/Editor/`.

---

<a id="1-current-code"></a>
## 1. 현재 코드 사실

### 1-1. 표적 선택 흐름 — `Scripts/Application/UseCases/TowerCombatUseCase.cs`

| 항목 | 사실 | 근거 |
|---|---|---|
| Tick 진입 | 멀티 Client면 즉시 반환(서버 권위) | `[확인]` 157행 |
| 순회 대상 | `BuildingType.AutoTower`만, 살아 있는 것만 — **종족 구분 없이 3종 공통** | `[전달]` 165~171행 |
| 쿨다운 | 매 Tick 감소, 남아 있으면 `continue` | `[전달]` 174~179행 |
| EMP 관문 | 쿨다운 감소 **뒤** · 적 탐색 **앞**. 막히면 `DiagNoteGateBlocked` 뒤 `continue` | `[확인]` 187~193행 |
| 표적 탐색 | `FindNearestEnemyUnit(building)` 1회 호출, null이면 `continue` — **쿨다운을 시작하지 않는다** | `[확인]` 196~197행 |
| 공격 실행 | `ExecuteTowerAttack`이 false(발사 실패)면 쿨다운 시작 안 함 | `[전달]` 203행 |
| 쿨다운 리셋 | 공격 성공 뒤 `BuildingStats.GetAttackCooldown` | `[전달]` 212~214행 |
| 탐색 본문 | 사거리 = `AttackRange × TileHeight + Epsilon(0.05)`, 기준점 = 탑 Transform 위치(없으면 `HexToWorld`), 적 유닛 · 생존만, `Vector3.Distance`(Y 포함 3D) 최단. **방향 · 각도 검사 없음** | `[확인]` `FindNearestEnemyUnit` — 사거리 240행, 기준점 244~248행, 거리 265행, 최단 갱신 268~272행 |
| 표적 유지 | **없다.** 공격 결정 때마다 최단 적을 새로 찾는다. 탑에 「현재 표적」 칸이 없다(`BuildingData`에 쿨다운 외 전투 상태 없음) | `[전달]` 196행 · `Scripts/Domain/Building/BuildingData.cs` 22~139행 |
| 실제 수치 | Human AutoTower 공격력 150 · 쿨다운 5초 · 사거리 4타일(= 4 × 0.866 + 0.05 = **3.514 m**). Spirit 쿨다운 3.5초, Transcendence 5초, 사거리 모두 4 | `[전달]` `Assets/_Project/Resources/Config/BuildingStatsConfig.asset` 50~65행 · `GameConfig.asset` FlatTop TileHeight 0.866 · `[산술]` 3.514 |

### 1-2. 탑 회전 · 조준 표현

| 항목 | 사실 | 근거 |
|---|---|---|
| 모델 초기 회전 | Human + AutoTower만: **내 탑 = 회전 없음, 상대 탑 = Y 180°**. 그 밖 건물 · 종족은 회전 없음 | `[확인]` `Scripts/Infrastructure/Factories/BuildingFactory.cs` 231~248행(`GetInitialRotation`, 180° = 243행) · 호출 169행 `[전달]` |
| 실시간 회전 | **없다.** 탑 · 포신을 돌리는 코드 없음. `LookAt` · `LookRotation` 검색 결과는 VFX · 떠오르는 숫자뿐(`EffectManager` 682 · 852 · 906 · 926행, `TracerProjectile`, `VfxPoolItem.TowerBolt`) | `[전달]` 저장소 검색 |
| 업그레이드 | 기존 오브젝트 회전을 그대로 복사 | `[전달]` `BuildingFactory.cs` 270행 |
| 타워 공격 프로필 | Human = 투사체 · 포구, Spirit = 번개 · 타워 위치, VineTower 행 없음(표현 없음) | `[전달]` `Scripts/Presentation/Effects/TowerAttackProfile.cs` 77행 · `EffectManager.cs` 724행 · 규칙 12 |
| 발사 VFX 방향 | 포구 → 대상 수평 방향으로 `LookRotation`(`PlayServerTowerLaunchCue`), 착탄 VFX도 비행 방향 | `[전달]` `EffectManager.cs` 900~910 · 920~930행 |
| 사거리 표시 UI | **없다**(「AutoTower 사거리 표시 토글은 아직 없으며」) | `[전달]` `Scripts/Presentation/UI/BuildingActionPanelUI.cs` 18행 |

### 1-3. 탑의 「앞」 방향이 데이터 · 코드에 있는가

| 질문 | 답 | 근거 |
|---|---|---|
| 도메인 데이터(`BuildingData`)에 방향 칸? | **없다** — Id · Type · Team · Position · HP · 쿨다운뿐 | `[전달]` `BuildingData.cs` 25~64행 |
| 코드에 「앞」이 암묵적으로 있는가? | **있다 — 한 곳.** `ProjectileSettings.ResolveTowerMuzzle`이 Blue는 오프셋 그대로(+Z), Red는 x · z 부호를 뒤집어(−Z) 포구를 계산한다. 주석이 「도메인 좌표로는 Blue 타워 포신이 +Z, Red 타워 포신이 −Z」라고 적고 있다 | `[확인]` `Scripts/Application/Combat/ProjectileDeliveryContracts.cs` 443~459행(부호 457행) |
| 도메인 +Z가 정말 「상대 진영 쪽」인가? | 맞다. FlatTop `z = −row × TileHeight`(행이 클수록 아래) → Blue 성 = 맵 아래쪽(낮은 Z), Red = 위쪽(높은 Z). Blue 탑의 +Z = Red 쪽 | `[전달]` `Scripts/Core/HexMetrics.cs` 132~145행 · `ViewConverter.cs` 5~13행 |
| Red 화면에서도 맞는가? | Red Client는 맵을 점대칭(`2C − p`)으로 보여 주고 Red 탑은 「내 탑 = 회전 없음」이라 화면 위쪽을 향한다 → 도메인으로 되돌리면 −Z. 일치 | `[전달]` `ViewConverter.cs` 86~95행 · `BuildingFactory.cs` 238~243행 `[확인]` |
| 실제 경기 격자 방향 | 싱글 · 멀티 모두 FlatTop | `[전달]` `GameBootstrapper.cs` 635행 · `GameBootstrapper.Network.cs` 86행 |
| 맵 대칭 | 맵 전체가 180° 회전 대칭(`p → 2C − p`) → 「Blue +Z / Red −Z」 정면은 서로의 회전상이라 양 팀에 공정 | `[전달]` `GameSystemRules_RandomMap.md` 47~61행 |

### 1-4. 서버 권위 투사체 발사 위치(포구)

| 항목 | 사실 | 근거 |
|---|---|---|
| 분기 | `_projectiles != null && IsProjectileTower(Human AutoTower)` → `TryLaunchTowerProjectile`. 나머지는 즉발 | `[전달]` `TowerCombatUseCase.cs` 294~303행 |
| 발사 위치 | 탑 루트(Transform 위치, Y 오프셋 포함) + 팀 방향 포구 오프셋 | `[전달]` `Scripts/Application/UseCases/UnitCombatUseCase.Projectiles.cs` 286~287행 |
| 포구 오프셋 값 | 코드 기본 · Inspector 모두 (0, 0.405, 0.17) — 탑 앞쪽 0.17 m | `[전달]` `ProjectileDeliveryContracts.cs` 407행 · `Assets/_Project/Resources/Config/ProjectileConfig.asset` 31행 |
| 비행 | 착탄 시각은 발사 때 고정(`거리 / 속도`, 속도 4), Homing true | `[전달]` `UnitCombatUseCase.Projectiles.cs` 291~305행 |
| 발사 뒤 대상 이동 | 착탄 시각에 원래 대상이 살아 있으면 맞는다(위치 · 각도 재판정 없음) | `[전달]` `TowerCombatUseCase.cs` 381~385행(`ApplyTowerProjectileImpact`는 생존만 검사) |

<a id="1-5-coordinates"></a>
### 1-5. 좌표계 주의 (판정 입력)

- `IEntityPositionProvider` 구현은 **GameObject Transform 위치 = 화면(뷰) 좌표**를 돌려준다 `[전달]` `Scripts/Infrastructure/UnitWorldPositionProvider.cs` 35~48행.
- 서버(Host)는 항상 Blue라 뷰 = 도메인이다 `[전달]` `Scripts/Infrastructure/Network/NetworkGameFlow.cs` 189행(`IsHost ? Blue : Red`). 싱글은 `LocalPlayerTeam.Current`(기본 Blue)로 반전 여부를 정한다 `[전달]` `GameBootstrapper.Map.cs` 184~187행.
- ⚠️ **`LocalPlayerTeam.Reset()`을 부르는 곳이 저장소 코드에 0건이다** `[확인 — 검색: 일치는 2026-10-13 랠리 Task Plan 86행의 문서 1건뿐, 정의는 `Scripts/Infrastructure/Network/LocalPlayerTeam.cs` 60행]`. 그래서 「같은 실행 안에서 멀티 Client(Red)로 한 판 한 뒤 싱글을 시작하면 싱글 화면이 반전된 채일 수 있다」는 가능성이 있다 `[추정 — 미검증, 다른 경로에서 값이 바뀌는지 끝까지 따라가지 않았다]`. 그 경우 Transform 위치는 반전된 뷰 좌표인데 「Blue = +Z」는 도메인 기준이라 **방향이 정반대로 판정된다**(거리는 점대칭이라 그대로). 기존 포구 계산(`ResolveTowerMuzzle`)도 같은 가정을 쓴다.
  - *문서 담당 확인:* 같은 사실(호출처 0건 · Red 잔존 가능성 · 실행 미확인)은 [2026-10-13 랠리 Task Plan](../../2026-10-13/10_37_rally-point-fix/Plan.md) 86행에 이미 「이번 범위 밖, 수정하지 않음」으로 기록돼 있다 — 이번이 첫 발견은 아니다. 이번 작업에서도 **범위 밖 — 보고만**(Plan Q14).
- 레이어 규칙을 지키며 이 위험을 피하는 수단이 이미 있다: `IHexCoordinateMapper.NormalizeToDomainPosition`(= `ViewConverter.FromView`) `[전달]` `Scripts/Application/Interfaces/IHexCoordinateMapper.cs` 58~66행, 선례 `UnitCombatUseCase.cs` 1315행 · `TileOwnershipService.cs` 146행. 방향을 계산하기 전에 두 위치를 도메인으로 바꾸면 반전 여부와 무관해진다 `[제안]`.

---

<a id="2-geometry"></a>
## 2. 각도 기준점 · 「앞」의 정의

### 2-1. 헥스 격자와 120° — 경계가 어디에 떨어지나

| 사실 | 값 | 근거 |
|---|---|---|
| 120° 판정식 | 수평(XZ) 방향 단위벡터 · 정면 벡터 ≥ cos 60° = 0.5 | 수학 · 기존 Research 15-9 항목 7 |
| FlatTop 이웃 방향 | 정북(+Z, 0°) · 북동 · 북서(±60°) · … — 타일 크기 W 1, H 0.866에서 대각 이웃 차이 Δ = (0.75, 0.433) | `[전달]` `HexMetrics.cs` 139~143행 · `GameConfig.asset` FlatTop |
| 🔴 대각 앞 이웃의 각도 | **60.0007°**(cos = 0.433 / 0.866019 = **0.499989**). H가 √3/2(0.8660254)가 아니라 0.866이라서 **경계 바로 바깥**에 떨어진다 | `[산술]` 문서 담당 재계산 60.00073° · 0.4999890 |
| 의미 | 타일 중심에 선 유닛 기준으로, 탑 중심에서 ±60° 축(북동 · 북서 일직선) 위의 모든 타일이 **정확히 경계선**이다. 허용 오차 없이 `≥ 0.5`로 짜면 대각 앞 타일이 전부 「뒤」로 빠진다 | `[추정 — 산술에서 도출]` |
| 「120° = 육각형 2변?」 | 정면(북) 중심 ±60°는 **북쪽 변 전체 + 북동 변 절반 + 북서 변 절반 = 2변 분량**(꼭짓점에서 꼭짓점까지 2변이 아니다). 이웃 방향으로는 북 · 북동 · 북서 3방향이고, 북동 · 북서가 경계선이다. 헥스 축이 만드는 60° 부채꼴 6개 중 **정면 2개**와 정확히 같다 | `[전달 — 기하]` |
| PointyTop | 정북이 꼭짓점 방향이고 이웃 방향 ±30°는 완전히 안쪽(cos 0.866)이라 경계 문제가 없다. 같은 내적식이 두 방향 모두에 맞는다(지금 경기는 FlatTop만 쓴다) | `[전달 — 기하]` |

### 2-2. 각도 기준점

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **A. 탑 중심(루트) XZ** `[제안 · 추천]` | 사거리와 같은 기준점에서 방향을 잰다 | 사거리(규칙 3)와 기준점이 하나 · 헥스 축과 경계가 정확히 맞아 규칙 문장이 단순 · 양 팀 대칭 · 테스트 기하 단순 | 대각 축 위 타일이 경계선 → 허용 오차 필요(2-1) · 탑에 아주 붙은 유닛(관측 최소 거리 0.379 — 기존 Research 15-9 항목 6)은 포구 기준으로는 조금 옆 · 뒤로 보일 수 있다 |
| B. 포구(루트 + 0.17 m 앞) XZ | 실제 포탄이 나가는 점에서 잰다 | 「포탄이 뒤로 가지 않음」을 엄밀히 보장 | 사거리와 기준점이 갈린다 · 대각 앞 이웃이 **70.7°**(Δ = (0.75, 0.263)), 2칸 대각도 65.1°로 빠져 **실질 각도가 120°보다 좁아진다** · 포구 오프셋(Inspector 값)을 바꾸면 판정이 바뀐다(표현 값이 판정 입력이 됨) |
| 높이(Y) | 두 안 모두 XZ만 `[제안]` | 건물 Y 오프셋 · 유닛 높이와 무관 | — |

*(B안 70.7°는 문서 담당 재계산 70.68° `[산술]`. 2칸 대각 65.1°는 재계산하지 않았다 `[전달]`.)*

### 2-3. 「앞」의 정의

| 안 | 내용 | 장점 | 단점 |
|---|---|---|---|
| **① 팀 진영 방향 고정(Blue +Z / Red −Z)** `[제안 · 추천]` | 규칙 11의 배치 방향 그대로 | 모델이 실제로 보는 방향과 같다(1-3) · `ResolveTowerMuzzle`이 이미 같은 정의를 쓴다 · 데이터 추가 없음 · 맵이 회전 대칭이라 공정 | 탑이 맵 가장자리에 있어도 정면은 무조건 위 / 아래 |
| ② 배치 때 방향 선택(플레이어가 돌림) | `BuildingData`에 방향 칸 + 배치 UI + RPC | 전략성 | 규칙 11 변경 · 도메인 · RPC · UI · 스키마 모두 변경 · 범위 대폭 초과 |
| ③ 적 성 방향 | 탑 → 적 성 벡터 | 「적 쪽」에 더 충실 | **모델은 +Z 고정이라 모델과 판정이 어긋난다**(포신은 정면인데 판정 정면은 비스듬) → 원래 문제(어색함)를 일부 되살림 · 규칙 11과 불일치 |
| ④ 실시간 회전(포신이 적을 따라 돎) | 규칙 11 변경 | 360° 유지 + 자연스러움 | 사용자 요청(「120° 제한」)과 다른 해법 · 표현 · 동기화 신설 |

---

<a id="3-scope"></a>
## 3. 적용 범위 · 경계 동작

| 질문 | 현재 사실 | 제안 |
|---|---|---|
| 적용 종족 | 탐색은 3종 공통(1-1). 방향이 있는 모델은 Human뿐(규칙 11, `BuildingFactory.cs` 234행) | **Human CannonTower만** `[제안 · 추천]`. RuneSpire(꼭대기 크리스털 번개) · VineTower(지면 분출)는 방향 있는 모델이 없어 360°가 자연스럽다 `[추정 — 표현 형태에서의 판단]` |
| 선택 의미 | — | **「120° 안의 적만 후보 → 그중 최단」** `[추천]`. 대안 「전체 최단을 고른 뒤 그게 뒤면 안 쏨」은 앞에 적이 있어도 뒤의 더 가까운 적 때문에 침묵해 부자연스럽다 |
| 앞에는 없고 뒤에만 있을 때 | 대상이 null이면 쿨다운을 시작하지 않고 다음 Tick에 다시 찾는다(196~197행 `[확인]`) | **공격하지 않고 대기, 쿨다운은 그대로**(0이면 0 유지) → 앞으로 들어오는 첫 Tick에 바로 발사. 코드 추가 없이 성립 `[추정 — 현재 흐름에서 도출]`. 규칙 5 「쿨다운 중에도 타겟 탐색은 계속 수행한다」와 충돌 없음 |
| 표적이 범위 밖으로 나갈 때 | 「잡고 있는 표적」 자체가 없다(1-1) | 발사 전: 다음 결정에서 자연히 빠진다. **발사 후: 그대로 날아가 맞는다**(착탄 시각 고정, 생존만 검사 — 1-4) `[추천 — 변경 없음]`. Homing이라 비행 중(최대 약 0.85초) 대상이 탑 뒤로 돌아가면 궤적이 휘어 보일 수 있다 `[추정 — 표현, 실기 미관측]` |
| 경계 포함 | — | **60° 포함 + 작은 허용 오차**(예: cos 임계 0.5 − 0.001 ≈ 60.07°) `[제안]` — 2-1의 60.0007° 대각 축 타일을 「앞」에 넣기 위해 필요. *(0.499 → 60.066° 문서 담당 재계산 `[산술]`)* |
| EMP와의 순서 | EMP 관문이 탐색 앞(187행 `[확인]`) | 각도 필터는 탐색 안에 들어가므로 EMP 동작(규칙 13)은 바뀌지 않는다 `[추정]` |

---

<a id="4-multiplayer"></a>
## 4. 멀티플레이 · 스키마

| 항목 | 사실 / 판단 | 근거 |
|---|---|---|
| 판정 위치 | 표적 선택은 서버(Host) · 싱글 Tick에서만 — Client는 호출 즉시 반환 | `[확인]` `TowerCombatUseCase.cs` 157행 · 호출처 `[전달]` `GameBootstrapper.cs` 772~775행(싱글) · `NetworkCombatController.cs` 728~737행(서버, EMP 틱 바로 뒤) |
| 표현 | Client는 서버 발사 기록(포구 · 대상 · 시각)을 재생할 뿐 → **표현 코드 변경 0** | 규칙 12 · `ProjectilePresentationPresenter` C-6 gate(6-1) `[전달]` |
| RPC 형식 | 바뀌는 칸 없음 | `[추정 — 판정 내부만 바뀜]` |
| 스키마 | `PresentationContractSchemaRevision = 8` · 접속 확인(handshake) 해시 = 스키마 + `ServerProjectileDeliveryPolicy.ManifestToken`(분류 목록). 이 작업은 둘 다 건드리지 않으므로 **올리지 않는다** `[제안]` | `[확인]` `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 33행 · 292~301행 `[전달]` |
| 다른 빌드끼리 | 해시가 같아 새 Host + 옛 Client 경기가 열린다. 판정은 Host만 하므로 결과는 Host 빌드 기준으로 일관된다 | `[추정]` |

---

<a id="5-related-rules"></a>
## 5. 관련 규칙과 개정안 — 🔴 규칙 변경 = 사용자 승인 대상

현행 문장 위치: [`GameSystemRules_Buildings.md`](../../../GameSystemRules/GameSystemRules_Buildings.md) 「방어 타워 시스템」 절(93~231행) `[확인 — 문서 담당이 이 절 전체를 다시 읽음]`. 건물 문서는 절마다 규칙 번호가 다시 시작하므로 아래 「규칙 N」은 모두 **「방어 타워 시스템」 절**의 번호다. 이 절의 마지막 번호는 규칙 13(무력화, 2026-11-09 신설)이라 새 규칙은 **14**가 된다 `[확인]`.

| 규칙 | 행 | 현행 요지 | 영향 | 개정안 `[제안 — 문안은 확정 아님]` |
|---|---|---|---|---|
| 1 타겟 대상 | 108~110 | 적 유닛만 | 대상 종류는 그대로 | 변경 없음 |
| 2 타겟 선택 기준 | 112~113 | 사거리 안 월드 거리 최단 | 후보 집합이 줄어든다 | 끝에 1문장: 「단, Human CannonTower는 전방 사격 범위(규칙 14) 안의 적 유닛 중에서만 고른다.」 |
| 3 사거리 판정 | 119~122 | `Vector3.Distance` | 거리 판정은 그대로 | 변경 없음(각도는 규칙 14가 따로 정의) |
| 5 공격 후 쿨다운 | 132~134 | 쿨다운 중에도 탐색 계속 | 충돌 없음 | 변경 없음 |
| 6 타겟 사망 시 | 140~143 | 즉시 새 타겟 탐색 | 「새 타겟」도 범위 안에서만 — 규칙 2 단서가 덮는다 | 변경 없음 |
| 9 서버 권위 | 164~166 | 타겟 선택 · 사거리 · 피해는 서버 전용 | 각도 판정도 서버 | 변경 없음(규칙 14에 「서버에서만」 명시) |
| 11 초기 회전 | 180~187 | 내 탑 회전 없음, 상대 탑 180°, 실시간 회전 없음 — **화면(관점) 기준 서술** | 규칙 14의 「정면」이 여기서 나온다 | 끝에 1문장: 「도메인 좌표로 Blue 탑의 정면은 +Z, Red 탑은 −Z(항상 상대 진영 쪽)이며, 규칙 14의 정면은 이 방향이다.」 |
| 12 발사 연출 | 193~215 | 서버 권위 투사체 · 포구 | 발사 뒤 대상이 이탈해도 명중 — 기존 문장 그대로 성립 | 변경 없음 |
| 13 무력화 | 221~231 | 「타겟 대상 · 사거리 · 프로필은 바꾸지 않는다」 | 충돌 없음 | 변경 없음 |
| **14 (신설)** 「Human CannonTower 전방 사격 범위」 | — | — | — | ① Human CannonTower는 정면(규칙 11) 좌우 60°씩, 합 120° 안의 적 유닛만 공격한다. ② 각도는 탑 중심에서 대상 유닛까지의 **수평(XZ) 방향** 기준이고, 60°는 포함한다(허용 오차 포함). ③ 범위 밖에만 적이 있으면 공격하지 않고 쿨다운은 그대로 진행 · 유지된다 — 범위 안에 들어오면 쿨다운이 0인 즉시 공격한다. ④ 이미 발사된 포탄은 대상이 범위 밖으로 나가도 그대로 날아가 피해를 준다(규칙 12). ⑤ 판정은 서버에서만 한다(규칙 9). ⑥ Spirit RuneSpire · Transcendence VineTower는 360° 그대로다. |

*(원자료는 규칙 12 범위를 193~202행으로 적었다. 문서 담당이 다시 읽어 보니 규칙 12의 2026-10-13 추가 블록이 215행까지 이어진다 — 표에는 193~215로 적었다 `[확인]`.)*

**다른 문서(위치만, 이번에 고치지 않음):** `GameDesignDocument.md` 152행~ 「4. 방어 타워」 서술, `StatsReference.md` 174행 — 각도 언급을 덧붙일지는 완료 뒤 문서 반영 때 정한다 `[전달 — 위치만]`.

---

<a id="6-regression-ai"></a>
## 6. 회귀 fixture · AI 영향

### 6-1. 회귀 fixture — `Editor/Tools/Combat/RunUnitActionSelfValidation.ServerProjectiles.cs`

fixture 탑 위치 = (0.6, 0.1, −0.9) `[확인]` 625행. Blue 유닛(fixture 공격자) 위치 = (0.1, 0, 0.1), Red 피해자 = (2.1, 0, 0.1) `[확인]` 138~139행.

| fixture | 하는 일 | 영향 | 근거 |
|---|---|---|---|
| `ValidateHumanTowerLaunchAndImpact`(C-2 Blue) | 실제 `Tick`으로 Blue Human 탑이 Red 피해자를 쏜다. Δ = (1.5, 1.0) → **56.3°, 범위 안** | 통과 유지 `[추정 — 산술]`. 경계에서 3.7°밖에 떨어져 있지 않다 | `[산술]` 56.31° · 653~661행 `[전달]` |
| 🔴 `ValidateHumanTowerRedMuzzle`(C-2 Red) | Red Human 탑(같은 위치)이 Blue 유닛 (0.1, 0, 0.1)을 쏘는지 · 포구가 −Z인지 확인. Δ = (−0.5, +1.0), Red 정면 −Z → **153.4°, 탑 뒤** | **깨진다** — 발사 0건 → `Launches.Count == 1` Require 실패 `[추정 — 산술, 실행 안 함]`. 같은 커밋에서 이 테스트의 Blue 유닛 위치를 탑의 −Z 쪽으로 옮겨야 한다(예: (0.6, 0, −2.4)) `[제안]` → Plan 최상단 「값 변경」 | `[확인]` 708~721행 · `[산술]` 153.4° |
| `ValidateHumanTowerFlightOutcomes` / `NoLaunchCases` | Blue 탑 기본 배치 사용 | 같은 56.3° → 영향 없음 `[추정]`. C-4(건물만 있음)도 무관 | `[전달]` 724~799행 |
| `ValidateSpiritTowerStaysInstant`(C-5) | Spirit 탑 | Human 한정이면 무관 `[추정]` | `[전달]` 801~822행 |
| 소스 문자열 gate C-6 | `TowerCombatUseCase.cs`의 주석 아닌 줄에 `ServerProjectileDeliveryPolicy.IsProjectileTower(tower.Type, projectileRace)` · `_projectiles.TryLaunchTowerProjectile(tower, projectileRace, target, launchPower, this)` · `projectilePresentationKey: key`가 그대로 있어야 한다 | 이 세 줄을 바꾸지 말 것 | `[전달]` 1183~1192행 |
| VfxConnections | `ExecuteTowerAttack(BuildingData, UnitData)`를 리플렉션으로 직접 호출(Spirit) — 탐색을 거치지 않는다 | 이름 · 시그니처를 유지하면 무관 | `[전달]` `RunUnitActionSelfValidation.VfxConnections.cs` 1913~1937행 · `TowerCombatUseCase.cs` 287행 주석 |
| 생성자 | fixture 2곳이 5인자 생성자를 직접 호출 | **생성자 변경 금지**(EMP가 설정자 주입을 택한 이유와 같다) | `[전달]` `TowerCombatUseCase.cs` 123~126행 · `ServerProjectiles.cs` 638행 · `VfxConnections.cs` 1913행 |
| fixture 좌표 변환기 | `NormalizeToDomainPosition(p) => p` | 1-5의 정규화를 넣어도 fixture 결과는 그대로 | `[전달]` `ServerProjectiles.cs` 96행 |
| END 줄 | `server-tower v=1` 줄은 `Contains`로 부분 검사 | 각도 수를 이 줄에 끼우면 검사 문자열이 깨질 수 있어 **별도 줄 권장** `[제안]` | `[전달]` 695~703행 |

**새 fixture 제안** `[제안]`: (a) Blue 탑 뒤에만 적 → 발사 0 · 쿨다운 0 유지 · 다음 Tick에 앞으로 옮기면 바로 발사, (b) 앞 · 뒤 동시(뒤가 더 가까움) → 앞 적 선택, (c) 대각 축 경계(60.0007°) 포함, (d) 60° + 1° 제외, (e) Red 대칭, (f) Spirit 탑 뒤 적 → 여전히 즉발(360° 유지 회귀).

### 6-2. AI

| 질문 | 답 | 근거 |
|---|---|---|
| AI가 방어탑을 짓는가? | **짓지 않는다.** AI 컨트롤러 코드의 `BuildingType` 참조는 Castle · Research · MiningPost뿐이고, 세 종족 시나리오 에셋의 `buildingType` 값에 2(AutoTower)가 없다(0 · 7~31만) | `[전달]` `Scripts/Application/Services/AIOpponentController.cs` 245 · 411 · 420 · 553 · 624 · 655행 · `Assets/_Project/Resources/Config/AIScenarioConfig_{Human,Spirit,Transcendence}.asset` 검색 · `BuildingType.cs` 32~34행 |
| 결과 | AI 동작 영향 0. 싱글에서는 **플레이어(Blue)의 인간 탑**만 시험할 수 있고, Red 인간 탑은 멀티 Client 쪽에서만 실기 관측 가능 | `[추정]` |

---

<a id="7-diagnostic-log-design"></a>
## 7. 진단 로그 설계 재료 (WORKFLOW [4] 「테스트 항목 진단 로그 규칙」)

프로그래머 제안 `[제안]` — 확정 대응표는 [Plan 5절](Plan.md#5-test-log-map).

- 형식: `GameLog.Dev.Info("Combat", nameof(TowerCombatUseCase), "[TOWER-ARC] <Event>", "Diag=TowerFrontArc, DiagRev=1, Role=<Single|Host>, …")` — EMP의 `[EMP-DIAG]` 형식 선례(`BuildingStatusEffectSystem.cs` 46 · 379~381행 `[전달]`).
- 진단 메서드는 `[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]`로 릴리스에서 빠지게 한다(선례 `UnitCombatUseCase.ProjectileDiagnostics.cs` 294행 `[전달]`).
- **매 Tick 줄 금지** — 상태가 바뀔 때 · 발사 때 · 경기 종료 때만(LogRules §1.14 금지 8).
- 서버 전용이므로 **Client 로그에는 0줄이 정상**이다.
- 경기 종료 줄은 싱글(`GameBootstrapper.cs` 548행 인근)과 멀티 서버(`NetworkCombatController.cs` 2750행 인근) 두 경로에서 불릴 수 있어 중복 방지 플래그가 필요하다(선례 `EndWritten` — `ProjectileDiagnostics.cs` 297~300행 `[전달]`).
- 착탄 때 각도는 재지 않으므로 「발사 뒤 대상 이탈해도 명중」은 새 로그로 판정할 수 없다 — 기존 `[PROJECTILE-END]` `byTower=…hit:` 수로 간접 확인.
- 존속 제안: `개발`(EMP 선례). 결정은 사용자(Q11).

---

<a id="8-risks"></a>
## 8. 위험 · 회귀 영향 요약

| # | 위험 | 근거 | 대응(Plan 6절) |
|---|---|---|---|
| R1 | C-2 Red fixture가 깨진다 | 6-1 | 코드와 같은 커밋에서 fixture 기하 수정 |
| R2 | 대각 축 타일이 60.0007°라 엄격 비교면 빠진다 | 2-1 | 허용 오차 + 경계 fixture |
| R3 | 반전된 서버(싱글 Red 잔존 가능성 — 미검증)에서 방향이 정반대로 판정된다 | 1-5 | 방향 계산 전 도메인 정규화. `LocalPlayerTeam.Reset` 0건 자체는 범위 밖 — 보고만 |
| R4 | C-6 소스 gate 문자열 · `ExecuteTowerAttack` 시그니처 · 5인자 생성자 | 6-1 | 건드리지 않는다 |
| R5 | 인간 탑 실질 화력 감소(덮는 면적 1/3) | 3절 | 밸런스는 game-design-lead · 사용자 — 이번 범위 밖 |
| R6 | 「앞」 정의가 두 곳(`ResolveTowerMuzzle` 부호 · 새 판정)에 중복 | 1-3 | 정면 벡터 헬퍼 하나로 공유하는 안 — `ResolveTowerMuzzle`를 바꾸는 것은 범위 확장이라 사용자 확인 |
| R7 | Homing 비행 중 대상이 뒤로 돌면 궤적이 휜다 | 3절 | 표현 현상 — 관찰만 |
| R8 | 탑에 붙은 유닛은 작은 이동에도 각도가 크게 변해 안 · 밖이 바뀐다 | 2-2 | 정상 동작(결함 아님). 근접 예외는 사용자 결정(Q10) |

---

<a id="9-unverified"></a>
## 9. 이번 조사에서 확인하지 않은 것

- 회귀 스위트 · fixture를 **실행하지 않았다** — C-2 Red 실패와 C-2 Blue 통과는 산술 판단이다.
- `LocalPlayerTeam.Current`가 멀티 뒤 싱글에서 실제로 Red로 남는지 — 실행 미확인(1-5).
- Homing 비행 중 대상이 탑 뒤로 돌아갈 때의 궤적 모양 — 실기 미관측(3절).
- 싱글에서 「적이 탑 뒤에 서는」 상황이 매번 재현되는지 — AI 경로에 좌우된다(Plan 4절).
- 문서 담당이 다시 읽지 않은 행(표의 `[전달]`)은 프로그래머 보고를 그대로 옮겼다.

---

## 10. 변경 이력

| 날짜 | 내용 | 근거 |
|---|---|---|
| 2026-11-09 | 작성. game-programmer 읽기 전용 조사를 옮김. 문서 담당이 HEAD `1e28526`에서 `TowerCombatUseCase.cs` 155~160 · 185~198 · 238~250 · 262~274행, `BuildingFactory.cs` 231~248행, `ProjectileDeliveryContracts.cs` 443~460행, `ServerProjectiles.cs` 136~140 · 625 · 708~722행, `UnitAttackShadowProfileResolver.cs` 33행, `GameSystemRules_Buildings.md` 90~234행, ROADMAP 29행, 서버 투사체 Plan F-120 · Research 15-9 앵커를 다시 읽고, 각도 수치(60.0007° · 70.7° · 56.3° · 153.4° · 60.07° · 3.514 m)를 다시 계산함. 원자료와 다른 점 2건: 규칙 12 범위 193~202 → 193~215, `LocalPlayerTeam.Reset` 0건은 2026-10-13 랠리 Plan 86행에 이미 기록됨 | 원자료(scratchpad `cannon120-research.md`) `[전달 — 메인 세션]` |
