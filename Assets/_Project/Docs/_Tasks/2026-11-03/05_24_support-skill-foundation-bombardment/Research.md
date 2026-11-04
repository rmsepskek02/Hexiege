# Research — 지원 스킬 공통 전제(C-1 · C-4) + 포격 지원

## 이 문서는 무엇인가

세 종족의 스킬 건물에 들어갈 진짜 스킬 목록은 2026-11-02에 정해졌다(상위 Task). 그런데 지금 게임 안에는 여전히 **테스트용 임시 스킬 5개**(폭탄 · 빙결 · 공격 버프 · 둔화 · 회복)가 들어 있고, 스킬을 써도 **화면에 이펙트가 하나도 나오지 않는다.** 멀티플레이에서는 상대 화면에 "스킬을 썼다"는 사실조차 쿨다운 숫자 말고는 전해지지 않는다.

사용자는 2026-11-03 「추천대로 진행하고 task 문서 작성해」로 **첫 코드 작업**을 정했다(상위 Plan 5-1 결정 31). 이번 작업은 세 가지를 한 묶음으로 한다.

1. **C-1 발동 연출 동기화** — 스킬을 쓰면 **내 화면과 상대 화면 모두**에 같은 자리 · 같은 이펙트가 나오게 한다. 앞으로 모든 스킬이 이 길을 쓴다.
2. **C-4 로드아웃 교체 + 이름 · 설명 칸** — 세 종족 스킬 칸 5개를 **확정 스킬로 바꿔 넣고**, 스킬 데이터에 **이름 · 설명**을 적을 칸을 만든다. 아직 기능이 없는 스킬은 **눌러도 쓰이지 않게** 막는다.
3. **포격 지원(인간 슬롯 1)** — 이미 만들어 둔 포격 이펙트(포탄 7발이 한꺼번에 떨어지는 것, V1)를 연결하고, **포탄이 땅에 닿는 순간**에 피해가 들어가게 한다.

이 문서는 그 작업을 하기 전에 **지금 코드가 어떻게 되어 있는지, 어떤 규칙이 근거인지, 무엇이 위험한지**를 파일 · 행 단위로 정리한 기록이다. 어떻게 바꿀지(추천안 · 단계 · 테스트 · 사용자 확인 Q1~Q6)는 같은 폴더의 [Plan.md](Plan.md)에 있다. **이번 단계는 문서만 만든다** — 코드 · 에셋 · 규칙 문서는 바꾸지 않았다.

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 3-2 「③ 코드 구현 단계의 내부 순서」(공통 전제 C-1 · C-4, 1차 포격 지원), 5-1 결정 6(O-6 동시 낙하) · 15(원소 공명 이름만) · 16(스킬 표시 UI는 추후 별도 작업) · 31(코드 구현 착수), 4절 1 · 4 · 20 · 21(프로그래머 확인 물음), 8절(위험).

**원자료:** game-programmer 읽기 전용 조사(2026-11-03, 메인 세션 전달). 프로그래머는 파일 수정 · git 쓰기 · 메모리 갱신을 하지 않았고 git은 `log` · `show`만 썼다고 보고했다.

**표기:** `[확인]` = 문서 담당이 2026-11-03 HEAD `9def81ce` 작업 트리에서 그 행을 직접 다시 읽어 확인한 것 · `[전달]` = 프로그래머 조사를 그대로 옮긴 것(문서 담당 미재확인) · `[계산]` = 확인한 값으로 계산한 것(게임 측정 아님) · `[추정]` = 확인하지 않은 판단 · `[미확인]` = 이 단계에서 확인할 수 없었던 것 · `[제안 — 프로그래머]` = 프로그래머 추천(결정 아님). 행 번호는 원자료 그대로이며, 다시 읽어 다르게 나온 곳은 그 자리에 「문서 담당 정정」으로 적었다.

---

## 1. 결정과 범위

| 항목 | 내용 | 출처 |
|---|---|---|
| 첫 코드 task | C-1 · C-4 + 포격 지원(1차, V1) | 사용자 「추천대로 진행하고 task 문서 작성해」 — 상위 Plan 5-1 결정 31 `[확정]` |
| 포격 방식 | 포탄 7발 동시 낙하, 착탄 시각에 한 번 피해 | 상위 Plan 5-1 결정 6(O-6) `[확정]` |
| 로드아웃 | 다섯 칸을 확정 스킬로 채운다. 기능 없는 스킬은 사용 불가. 정령 5 원소 공명은 이름만 | 상위 Plan 5-1 결정 3 · 15 `[확정]` |
| 이번 범위 밖 | 버튼 아이콘 · 설명 탭 · 임시 라벨 정리 · 「준비 중」 화면 표시 방식 | 상위 Plan 5-1 결정 16 — 추후 별도 UI 작업 `[확정]` |
| 이번 범위 밖 | 적 건물 피해 비율 | 상위 Plan 3-3 B-1 — 밸런싱 단계 `[확정]` |
| 이번 범위 밖 | 규칙 문서 수정 | 메인 세션 지시 — 구현 뒤 반영(Plan 8절) |

---

## 2. 근거로 읽은 규칙

`Assets/_Project/Docs/GameSystemRules.md`(인덱스)에서 스킬 · 사운드 · 전투 동기화 행을 골라 원본을 열었다. 「스킬 규칙 N」 = `GameSystemRules/GameSystemRules_Skills.md` 규칙 N.

| 규칙 | 내용(요지) | 이 Task와의 관계 |
|---|---|---|
| 스킬 규칙 3 「건물별 글로벌 쿨다운」 | 건물 단위 쿨다운 `[확인]` | 착탄이 늦게 와도 쿨다운은 **발동 순간** 시작(현행 `SkillActivationUseCase.cs` 146행 `[확인]`) |
| 스킬 규칙 4 · 6 | 건물당 최대 5개, 종족별 스킬셋 분리 `[확인]` | 로드아웃 교체(C-4)의 틀 |
| 스킬 규칙 7 「데이터 주도 설계」 | 스킬 = 데이터. 요소 표는 아이콘 · 조준 방식 · 쿨다운 · 타입 · 파라미터 `[확인]` | 이름 · 설명 · 사용 가능 여부 · 발동 VFX · 착탄 지연 칸을 넣으면 **요소 표 갱신이 필요**(Plan 8절) |
| 스킬 규칙 9 「3×3 그리드 슬롯 배치」 | 슬롯 1~5 스킬, 6 철거 `[확인]` | 다섯 칸을 모두 채운다 |
| 스킬 규칙 11 「타입 A — 즉발 범위 피해」 | 지정 지점 원형 범위에 **1회(즉발) 피해** `[확인]` | 「착탄 지연」이 들어가면 「즉발」 문구 개정 필요(Plan 8절, 상위 Plan 6절 R-5 · R-8 옆) |
| 스킬 규칙 16 「지점 지정 발동」 | 버튼 입력으로 조준 모드 진입 `[확인]` | 사용 불가 스킬이 조준에 들어가지 않게 막는 가드(Q3)의 근거 |
| 스킬 규칙 25 「서버에서만 판정·실행」 | 발동 · 판정 · 피해 · 상태변경은 서버만. 조준 · 미리보기는 로컬 표현 `[확인]` | 지연 판정도 서버 틱에서만. 연출은 각 화면 로컬. 사용 불가 거부도 서버에서 |
| 스킬 규칙 26 「좌표만 전송 + 서버 재검증」 | 클라이언트는 조준 좌표 1개만 전송 `[확인]` | **요청 RPC는 그대로** 두고 성공 알림(서버 → 클라)만 바꾼다. 규칙 위반 없음 |
| 스킬 「멀티플레이 / 서버 권위」 머리 문장 | NetworkBehaviour는 Infrastructure에만, RPC 이름은 `ServerRpc`/`ClientRpc`로 끝남 `[확인]` | 새 ClientRpc의 자리와 이름 |
| `GameSystemRules_Sound.md` 규칙 15 「VFX+SFX 쌍 호출 규칙」 | VFX와 SFX가 **동시에 재생되어야 하는 경우** 같은 메서드에서 연달아 호출 `[확인]` | 스킬 SFX 경로가 없다(4-6) |
| `GameSystemRules_UnitCombatSynchronization.md` 「10. 현재 구조에서의 전환 규칙」 5항(408행) | 경기 시작 전 `CombatSchemaRevision + AttackProfileHash + CombatPipelineMode` 일치 확인, 불일치면 거절 또는 Legacy `[확인]` | 스키마 상향(3-2)의 근거 |
| 같은 문서 「5. 서버 시간 재생」 NET-TIME-001 · 002 | 단일 서버 시간축 · 지연 흡수 표현 시간축 `[확인 — 제목만]` | 유닛 공격용이다. 스킬에 적용하지 않는 것을 추천(4-3) `[제안 — 프로그래머]` |

---

## 3. C-1 — 발동 연출 동기화

### 3-1. 현재 코드

| 사실 | 근거 | 등급 |
|---|---|---|
| 싱글: 패널이 `_skillActivation.Activate(...)`를 직접 부른다. 연출 호출 없음 | `BuildingSkillPanelUI.cs` 398행 | `[확인]` |
| 멀티: 서버 Activate 성공 → `SkillActivatedClientRpc(buildingId, cooldown)` | `NetworkSkillController.cs` 168~172 · 186행 | `[확인]` |
| 수신 쪽은 Host면 건너뛰고(`if (IsServer) return;`), 순수 클라이언트는 쿨다운 미러만 세운다 | 같은 파일 188~191행 | `[확인]` |
| 「VFX 등 연출은 플레이스홀더이므로 이번 범위에서는 쿨다운 미러만 동기화한다」 주석 | 같은 파일 183행 | `[확인]` |
| 스킬 RPC는 접속 확인 대상이 아니다. 확인되는 것은 이동 스키마(`ContractSchemaRevision = 1`)와 전투 표현 스키마(`PresentationContractSchemaRevision = 6`)뿐 | `NetworkGameFlow.cs` 70~83행, `UnitMovementContracts.cs` 2559행, `UnitAttackShadowProfileResolver.cs` 25행 | `[확인]` |
| RPC 모양을 바꾸면 스키마를 올린 선례(3 → 4 → 5 → 6) | `UnitAttackShadowProfileResolver.cs` 19~24행 주석 | `[확인]` |
| 공격 표현 두 채널 방식 선례: 서버 확정 이벤트 → 서버가 구독해 ClientRpc → 클라이언트가 수신 이벤트 발행 → 화면이 둘 다 구독 | `GameEvents.cs` 951~952행, `NetworkCombatController.cs` 72~92 · 331행, `HitPresentationQueue.cs` 518~520행 | `[전달]` |
| 조준 좌표는 도메인 월드 좌표이고, 화면 좌표 변환은 `ViewConverter.ToView`가 한다 | `ViewConverter.cs` 86행, `SkillAimController.cs` 338행 | `[전달]` |
| AI는 스킬을 쓰지 않는다 | grep 결과(상위 Research 2-11과 같음) | `[전달]` |
| 회귀 fixture에 `IGameServices` 대역 구현이 있다(`GetSkillActivationUseCase() => null`). 인터페이스 메서드가 늘면 같이 고쳐야 한다 | `RunUnitActionSelfValidation.cs` 54 · 75행 | `[확인]` |
| 맵 전체 팀 상태는 기존 `StatusAppliedClientRpc`(팀 단위)로 이미 양쪽에 재현된다 | `NetworkSkillController.cs` 217행 | `[확인]` |

### 3-2. 스키마 · 호환성 사실

- 성공 알림 RPC의 모양을 바꾸면, 선례대로라면 전투 표현 스키마를 **6 → 7**로 올리게 된다 `[전달 — 선례 기준 판단]`.
- 그러면 회귀 스위트의 fixture 한 줄이 함께 바뀌어야 한다: `RunUnitActionSelfValidation.ServerProjectiles.cs` 257행 `Require(UnitAttackShadowProfileResolver.PresentationContractSchemaRevision == 6, …)` `[확인]`.
- 그 밖의 fixture는 스킬 타입을 참조하지 않는다 — `IGameServices` 대역은 `GetSkillActivationUseCase() => null`뿐 `[전달 — grep]`, 대역 줄 자체는 `[확인]`.
- NGO가 RPC를 이름으로 식별하는지 서명으로 식별하는지는 **확인하지 않았다** `[미확인]`. 스키마를 올리지 않을 때(Plan S3) 섞인 빌드에서 무슨 일이 생기는지가 여기에 걸린다.

### 3-3. 흐름에서 지켜야 할 점 (사실 기반)

- **Host 화면 이중 재생:** Host는 서버이자 클라이언트라, 「서버 확정」과 「수신」 두 채널을 모두 구독하면 두 번 재생된다. 지금 수신부의 `IsServer` 가드(188행 `[확인]`)와 공격 표현 선례(`NetworkCombatController.cs` 84행 `if (IsServer || _combatStopped) return;` `[확인]`)가 같은 방식으로 이를 막는다.
- **빨강(뒤집힌) 화면:** 페이로드를 도메인 좌표 그대로 보내고 화면 쪽에서 `ViewConverter.ToView(aim)`로 바꾸면 올바른 자리에 나온다 `[전달]`. 프리팹이 원형이라 회전은 항등이지만 **포탄 낙하 무늬가 180° 돌아 보일 수 있다** — 연출 전용 `[추정 — 게임에서 확인]`.
- **회귀 fixture 회피:** `IGameServices`에 메서드를 추가하지 않고 이벤트 허브(`GameEvents`)로 연결하면 fixture 대역 구현을 건드리지 않는다 `[전달]`.

---

## 4. 포격 지원

### 4-1. 프리팹 사실 — `Assets/_Project/Prefabs/VFX/Buildings/vfx_flightfacility_bombardment.prefab`

| 항목 | 값 | 등급 |
|---|---|---|
| GUID | `8d261a35773d38c449b73148b79cfa01` | `[확인]` |
| 참조처 | `.asset` · `.prefab` · `.unity`에서 0건(미연결) | `[확인]` |
| 최종 커밋 | `0e5a7e9d` 「all seven shells drop at once」 | `[전달]` |
| 자식 구성 | Shock_Ring · Sparks · Impact_Flash · Debris · Fireball · Smoke · Scorch · Shell · Target_Marker | `[확인 — m_Name]` |
| Shell | 0초에 7발 한 번, 수명 0.4초, Sub Emitter 6개(type 2 = 소멸 시) | `[전달]` |
| 폭발 6종 | 착탄 시각 = Shell 소멸 = **0.4초** | `[전달]` |
| 루트 길이 | 1.6초 | `[전달]` |
| Target_Marker | 수명 1.0초, 크기 5.56(파일 44345행) | 크기 `[확인]` · 수명 `[전달]` |
| 모든 파티클 | `playOnAwake: 0` · `looping: 0` · `startDelay: 0` | `playOnAwake: 1` · `looping: 1` 0건 `[확인]`, `startDelay` `[전달]` |
| `scalingMode` | 전부 0 = Hierarchy(루트 균일 스케일이 먹음) | `[전달]` |
| `moveWithTransform` | 전부 0 = Local 시뮬레이션으로 읽었으나 **필드명과 값의 대응은 Inspector로 확인 필요** | `[추정]` |
| VfxPoolItem 부착 | 프리팹에는 없음. `EffectManager.GetOrCreateVfx`가 실행 중에 붙인다(`EffectManager.cs` 868행) | 868행 `[확인]` |

- 재생 경로는 기존 `VfxPoolItem.Play(pos, rot, offset, euler, scaleMultiplier, …)`이고, 루트 `localScale`에 배율을 곱한다(`VfxPoolItem.cs` 355 · 364행) `[확인]`.
- 반경 배율은 `R / 2.0`이다. 지금 Bomb 반경 2이면 1.0 `[계산 — 기준 반경 2.0은 프로그래머 판단]`.

### 4-2. 지금의 피해 경로

| 사실 | 근거 | 등급 |
|---|---|---|
| A 실행기 → `ApplySkillInstantAreaDamage` → 적 유닛 · 건물을 먼저 모은 뒤 방어 감쇄 피해 | `InstantAreaDamageExecutor.cs` 46행, `UnitCombatUseCase.cs` 2381~2400행 | 2381행 시그니처 `[확인]` · 나머지 `[전달]` |
| 범위 판정 위치는 유닛 · 건물의 **타일 중심**(`_mapper.HexToWorld(…Position)`) | `UnitCombatUseCase.cs` 2551 · 2569행 | `[확인]` |
| 건물은 방어 0 = 그대로 맞는다(비율은 밸런싱 B-1) | 같은 파일 2494~2501행 | `[확인]` |
| 피해 이벤트 `attackerIsUnit: false, immediatePresentation: true` | 같은 파일 2510~2513행 | `[확인]` |
| 화면 큐가 즉시 방출 → 피해 숫자 | `HitPresentationQueue.cs` 639~658행 | `[전달]` |
| 화면 큐에는 AudioManager 호출이 없다 = **맞는 소리 없음** | grep 0건 | `[확인 — "Audio" grep 0건]` |
| `AudioManager`에 스킬 SFX 메서드가 없다(`PlayUnitAttackSfx` · 건물 배치/파괴/업그레이드 · UI뿐) | `AudioManager.cs` 595~641행 | `[전달]` |
| 서버 틱: 싱글은 매 프레임(`GameBootstrapper.cs` 711~714행 — 싱글 **또는 순수 클라이언트**일 때 쿨다운 감소), 멀티 서버는 `TickCombat` 안(`NetworkCombatController.cs` 763행 `TickCooldowns`) | — | `[확인]` |
| 멀티 서버 틱 주기: 코드 기본값 0.05초, **씬 값 0.1초** | `NetworkCombatController.cs` 61행, `Game.unity` 45954행 `_attackInterval: 0.1` | `[확인]` |
| UseCase는 맵을 불러올 때마다 새로 만들어진다 → 대기 상태가 자연히 초기화된다 | `GameBootstrapper.Map.cs` 200행(`CreateUseCases()`) → `GameBootstrapper.Setup.cs` 279 · 418행 | 200행 `[확인]` · Setup `[전달]` |
| 타입 C에서 상태 종류가 None이면 바로 return | `UnitCombatUseCase.cs` 2450행 | `[확인]` |

> **문서 담당 메모(행 번호 성격):** `GameBootstrapper.cs` 711~714행의 쿨다운 틱은 조건이 `!IsNetworkMode() || !NetworkContext.IsNetworkServer` — **싱글과 멀티 순수 클라이언트 둘 다**에서 돈다 `[확인]`. 착탄 예약을 이 틱에 얹으면 순수 클라이언트도 예약을 처리하게 된다. Plan D1 하위 결정 d(권위 전용 틱 분리)의 근거다.

### 4-3. 발동 → 착탄 시간이 맞는지 (계산, 게임 측정 아님)

서버가 발동 0.4초 뒤에 피해를 주는 경우(Plan D1)의 화면 시각 `[계산]`:

- **Host:** VFX가 0초에 시작하고, 피해는 0.4초(+ 틱 간격 최대 0.1초)에 나와 화면과 맞는다.
- **순수 클라이언트:** VFX는 RPC 도착 시각 L에 시작해 폭발이 0.4 + L에 보이고, HP 동기화는 서버 0.4 + L에 도착한다. 둘 다 같은 L만큼 늦으므로 **NET-TIME-002 같은 클라이언트 지연 보정 없이도 맞는다.**
- 남는 오차는 서버 틱 양자화다. 0.1초 틱이면 착탄 피해가 최대 약 0.1초 늦다.

### 4-4. 적 건물 피해

변경 없다. 피해 비율은 밸런싱 B-1(상위 Plan 3-3)이다.

### 4-5. 착탄 전 사건 관련 선례

- 시전 건물이 착탄 전에 파괴될 때: CannonTower 투사체는 「비행 중 탑이 파괴돼도 피해」 선례가 있다(상위 Research 2-8) `[전달]`.
- 경기 종료 뒤: 싱글 `GameBootstrapper.Update`는 경기 종료 뒤에도 틱이 돈다(메모리 기록) `[전달]` → 남은 착탄 예약을 그대로 두면 종료 뒤 피해가 들어갈 수 있다 `[추정]`.

### 4-6. SFX

스킬 SFX 항목과 재생 메서드가 없고(4-2), 피해 화면 큐에도 소리가 없다 `[전달 · 확인 혼합 — 4-2 표]`. **「기존 경로로 SFX」에 해당하는 경로가 지금은 없다.** 사운드 규칙 15는 「동시에 재생되어야 하는 경우」라 SFX 없이 VFX만 내는 것은 위반이 아니다 `[확인 — 규칙 문장]`. 어떻게 할지는 Plan Q5.

---

## 5. C-4 — 로드아웃 교체

### 5-1. 현재 상태

| 항목 | 사실 | 근거 | 등급 |
|---|---|---|---|
| 로드아웃 저장 위치 | `Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset` | `GameBootstrapper.cs` 79행 `[SerializeField] _skillLoadoutConfig` → `GameBootstrapper.Setup.cs` 425행으로 주입 | `[확인]` |
| 종족 키 | Human = 0, Spirit = 1, Transcendence = 2 | `RaceId.cs` 25~27행 | `[전달]` (asset의 `race: 0/1/2` 세 줄은 `[확인]`) |
| 세 종족 로드아웃 | 모두 Bomb(A, 지점, 반경 2, 피해 150, 쿨 10) · Freeze(C, kind 4, 3초) · Buff(C, kind 2, ×1.5, 8초, 아군) · Slow(C, kind 1, ×0.5, 5초) · Heal(C, kind 5, 200, 5초, 아군). Buff · Slow · Heal 쿨 15 | 위 asset + 자산 18개 | 인간 순서(GUID 대조) `[확인]` · 수치 `[전달]` |
| Dot 자산 3개 | `Skill_*_Dot`(B, 40/초, 4초)은 **어느 로드아웃에도 없다** | asset GUID 대조 | `[확인]` |
| 아이콘 | 18개 모두 `_icon: {fileID: 0}` | 각 `.asset` 15행 | `Skill_Human_Bomb` `[확인]` · 나머지 `[전달]` |
| 이름 · 설명 칸 | 없다 | `SkillDefinition.cs` 34~71행, `SkillData.cs` 28~92행 | `[전달]` (34~36행 `[Header("표시")] _icon`은 `[확인]`) |
| 빈 슬롯 처리 | 빈 참조는 건너뛰고 뒤 슬롯이 앞당겨진다 | `SkillLoadoutConfig.cs` 76행 | `[확인]` |
| `SkillData` 생성자 호출처 | `SkillDefinition.ToSkillData` 한 곳 | `SkillDefinition.cs` 80행 | 80행 `[확인]` · "한 곳" `[전달]` |
| 상태 종류 값 | 0~5. 새 값은 뒤에 추가 | `StatusEffectKind.cs` 10~11행 | `[확인]` |

### 5-2. 확정 스킬로 바꿨을 때 슬롯 위에 보이는 임시 라벨 (상위 Plan 4절 21 ②)

임시 라벨(`BuildingSkillPanelUI.cs` 235~255행 `[확인]`)은 **실행 방식과 상태 종류만** 본다. 그래서 자산에 넣는 자리표시 값에 따라 라벨이 정해진다.

| 자산의 실행 방식 / 상태 | 보이는 라벨 |
|---|---|
| A | 「폭탄」 |
| B | 「장판」 |
| C + 공격력(2) | 「버프」 |
| C + 이동(1) | 「둔화」(이동 배율 > 1인 순풍도 「둔화」로 보인다) |
| C + 공격 불가(3) | 「기절」 — *문서 담당 정정: 원자료 표에 없던 줄. 같은 switch 251행 `[확인]`(주석 "대비(현재 로드아웃엔 없음)")* |
| C + 빙결(4) | 「빙결」 |
| C + 회복(5) | 「회복」 |
| C + None(0) | 「상태」 |

- 결과적으로 포격은 「폭탄」, 기능 없는 스킬은 자리표시 값에 따라 「폭탄」이나 「상태」처럼 **실제 스킬과 다른 이름**이 보인다 `[전달 — 위 표에서 도출]`.
- 라벨 수정은 이번 범위가 아니다(상위 결정 16). 이 결과를 그대로 보고한다.
- 슬롯 버튼은 스킬이 있으면 누를 수 있다(`BuildingSkillPanelUI.cs` 210행 `cg.interactable = hasSkill`) `[확인]` — 「있지만 사용 불가」 상태는 지금 없다.

### 5-3. 「사용 불가」를 데이터로 두지 않을 때의 위험

- 실행기가 등록되지 않은 타입은 발동이 실패하고 쿨다운도 돌지 않는다(`SkillActivationUseCase.cs` 127~131행 주석 · 130~131행 null 검사) `[확인]`.
- **그러나** 기능 없는 스킬에 자리표시로 A를 넣으면 A 실행기가 실제로 돈다(같은 파일 130~143행) `[확인 — 흐름]`. 쿨다운도 돈다(146행). 즉 **공습 슬롯이 폭탄으로 동작**한다 `[전달 — 위 흐름에서 도출]`.
- C + None으로 두면 `ApplySkillGlobalStatus`가 None에서 바로 return(`UnitCombatUseCase.cs` 2450행 `[확인]`)하지만, 실행기는 등록돼 있으므로 **쿨다운이 돈다** `[전달]`.
- 그래서 "사용 가능 여부"를 따로 나타내고 **서버에서 막는** 장치가 필요하다 `[제안 — 프로그래머]` → Plan U1.

### 5-4. 새 자산을 만드는 방법에 관한 사실

- 기존 18개 자산은 지우지 않으면 GUID로 되돌릴 수 있다(상위 Plan 최상단 표) `[확인 — 상위 문서]`.
- Inspector 작업은 WORKFLOW [5-2] 에디터 스크립트로 한다. 한 번만 실행해도, 여러 번 실행해도 결과가 같게 만들고 사용 후 정리한다(BattleAxe · Quake config 셋업 선례) `[전달]`.
- 이름 칸 값 중 가칭(화염의 축복 · 대지의 갑주)은 O-10이 아직 열려 있다(상위 Plan 5절) `[확인]`.

---

## 6. 위험

| 위험 | 내용 | 등급 |
|---|---|---|
| 스키마 상향 | 구버전 빌드와 경기 불가(의도) — 두 기기 모두 새 빌드 필요 | `[전달]` |
| 회귀 스위트 | fixture `== 6` 한 줄(3-2) 외에는 직접 영향이 확인되지 않았다 | `[전달]` |
| 미구현 스킬 발동 | 5-3 — 자리표시 A가 실제로 폭탄처럼 동작 | `[확인 — 흐름]` |
| 착탄 양자화 | 멀티 서버 틱 0.1초(씬 값) → 착탄 피해 최대 약 0.1초 늦음 | `[계산]` |
| 착탄 사이 이동 | 유닛이 0.4초 사이 범위를 드나들 수 있다(O-3 「착탄 순간 범위 안」 의도). 판정은 타일 중심 기준(4-2) | `[전달]` |
| 싱글 경기 종료 뒤 착탄 | 4-5 | `[추정]` |
| 뒤집힌 화면 무늬 | 포탄 배치가 180° 회전해 보일 수 있다 | `[추정]` |
| 착탄 시간 이중 관리 | 프리팹 0.4초와 데이터 「착탄 지연」 0.4초 중 한쪽만 바꾸면 어긋난다 | `[전달]` |
| 테스트 수단 감소 | 로드아웃 교체로 임시 스킬 5종(빙결 · 회복 등)의 실기 시험 경로가 화면에서 사라진다. 자산은 보존 | `[전달]` · 상위 Plan 8절 같은 행 `[확인]` |

---

## 7. 미확인으로 남긴 것

- NGO RPC 식별 방식(이름 / 서명) — 3-2
- 프리팹 `moveWithTransform` 값의 의미 — 4-1
- 뒤집힌 화면의 포탄 무늬 — 3-3
- 착탄 0.4초를 게임에서 측정한 값(지금은 프리팹 값 · 계산뿐) — 4-3

---

## 8. 참고 파일

- `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs`
- `Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs`
- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Infrastructure/Config/SkillDefinition.cs`
- `Assets/_Project/Scripts/Application/Skill/SkillData.cs`
- `Assets/_Project/Scripts/Infrastructure/Config/SkillLoadoutConfig.cs`
- `Assets/_Project/Resources/Config/Skills/SkillLoadoutConfig.asset`
- `Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs`
- `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs`
- `Assets/_Project/Scripts/Presentation/Effects/VfxPoolItem.cs`
- `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameFlow.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` · `GameBootstrapper.Setup.cs` · `GameBootstrapper.Map.cs`
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` · `RunUnitActionSelfValidation.ServerProjectiles.cs`
- `Assets/_Project/Prefabs/VFX/Buildings/vfx_flightfacility_bombardment.prefab`
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Skills.md`

---

## 9. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-03 | 최초 작성 — game-programmer 읽기 전용 조사를 사실 · 추정 · 제안 구분을 유지해 옮김. 문서 담당이 HEAD `9def81ce`에서 주요 행을 다시 읽어 `[확인]` 표시. 정정 1건: 임시 라벨 표에 C + 공격 불가(3) → 「기절」 줄 추가(`BuildingSkillPanelUI.cs` 251행). 코드 · 에셋 · 규칙 문서 무변경 |
| 2026-11-03 | 구현 · 사용자 테스트 뒤 10절 추가 — 이 조사의 사실 중 구현으로 바뀐 것 · 그대로인 것 · 미확인으로 남은 것. 위 1~9절 본문은 조사 시점 기록이라 고치지 않았다 |

---

## 10. 구현 뒤 대조 (2026-11-03)

이 문서 1~9절은 **구현 전**(HEAD `9def81ce`) 상태 기록이다. 구현(`03e5551d` · `56d5ab2a` · `c4cbf8ab`)과 사용자 자산 푸시(`3ff5ed60`) 뒤 바뀐 것을 여기 모은다. 결과 · 테스트 · 남은 위험의 단일 기록은 [Plan 10절](Plan.md#10-implementation-and-user-test)이다.

| 조사 시점 사실 | 지금 | 근거 |
|---|---|---|
| 3-1 성공 알림은 `SkillActivatedClientRpc(buildingId, cooldown)` · 수신 쪽은 쿨다운 미러만 · 183행 「연출은 플레이스홀더」 주석 | 같은 이름의 RPC가 (건물 Id · 팀 · 슬롯 · 조준 여부 · 조준 좌표 · 발동 순번 · 착탄 지연 · 쿨다운)을 싣고, 순수 클라이언트는 쿨다운 미러 + 「수신」 이벤트를 낸다. Host 이중 재생 방지 `IsServer` 가드는 유지 | `NetworkSkillController.cs` 219행 · `UnitAttackShadowProfileResolver.cs` 25~28행 주석 [확인] |
| 3-1 · 3-2 전투 표현 스키마 6, fixture `== 6`(257행) | 스키마 **7**, fixture `== 7`(259행) | `UnitAttackShadowProfileResolver.cs` 29행 · `RunUnitActionSelfValidation.ServerProjectiles.cs` 259행 [확인] |
| 4-1 포격 프리팹 참조처 0건 | `Skill_Human_Bombardment.asset`의 발동 VFX 칸이 이 프리팹 GUID를 가리킨다 | 자산 33행 [확인] |
| 4-2 타입 A는 발동 즉시 `ApplySkillInstantAreaDamage` | 착탄 지연 > 0이면 예약 → 권위 전용 틱에서 1회 호출. 지연 0이면 종전과 같다 | `SkillActivationUseCase.cs` 288~295행 [확인] |
| 5-1 이름 · 설명 칸 없음 · 로드아웃은 임시 5종 | 이름 · 설명 · 사용 가능 · 착탄 지연 · 발동 VFX · VFX 기준 반경 칸이 생겼고, 로드아웃은 새 확정 스킬 15개(사용 가능은 포격 지원 1개). 기존 18개 자산은 남아 있다 | Plan 10-2 [확인] |
| 5-2 슬롯 라벨 | 예측대로 — 인간 패널은 「폭탄 × 4 + 상태 + 철거」(Plan 7-2 사용자 보고) | 사용자 보고 |
| 5-3 기능 없는 스킬이 폭탄처럼 동작할 위험 | 「사용 가능」 off면 서버가 슬롯 조회 직후 거부(쿨다운 안 돎) · 패널은 조준에 들어가지 않는다 | `SkillActivationUseCase.cs` 128행 [확인] |

**그대로 미확인인 것(7절):** NGO RPC 식별 방식 · 프리팹 `moveWithTransform` 의미 · 뒤집힌 화면의 포탄 무늬 · 착탄 0.4초의 게임 측정값 — 사용자 테스트는 종합 판정(「특별히 문제없는 것으로 확인했어」)이라 이 넷을 따로 재지 않았다.
