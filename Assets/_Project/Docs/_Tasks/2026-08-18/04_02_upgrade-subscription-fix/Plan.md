# Plan — 연구 완료 브로드캐스트 구독 누락 수정 + 죽은 카메라 코드 정리 + 유닛 스폰 실패 로그 보강

작성일: 2026-08-18 04:02
대상 작업 3건 (① 동작 버그 수정 / ② 죽은 코드 제거 / ③ 로그 진단 필드 추가)

---

## ⚠️ [최상단] 기존 로직 제거 — 근거와 방식 (WORKFLOW.md [4] 「기존 로직 제거 규칙」)

이번 작업에는 **기존 메서드 1개의 제거**가 포함된다. 규칙이 요구하는 대로 제거 대상·제거해도 안전한 근거·제거 방식을 문서 최상단에 먼저 밝힌다.

### 제거 대상

| 항목 | 위치 |
|---|---|
| `SetCameraStartPositionForTeam(TeamId, OrientationConfig)` 메서드 전체 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` 541~585행 |
| 파일 헤더 목차 주석의 메서드 이름 표기 | 같은 파일 13행 |

### 제거해도 안전한 근거 (실측)

1. **호출부가 0곳이다.** `Assets/` 전체를 `SetCameraStartPositionForTeam` 으로 검색하면 결과가 **2건뿐**이며, 그 2건은 **선언 1곳(541행) + 파일 헤더 목차 주석 1곳(13행)** 이다. 실제 호출은 한 곳도 없다. `private` 메서드이므로 외부 어셈블리·리플렉션 경로도 없다.
2. **대체 방식이 이미 자리를 잡았다.** 팀별 카메라 이동 대신 `GameBootstrapper.Network.cs` 82행이 `ViewConverter.Setup(isRed, mapCenter)` 로 **뷰 좌표계 자체를 반전**한다. 같은 파일 91행에 방침이 명문으로 남아 있다 — *"싱글플레이와 동일하게 맵 중심에서 시작하도록 팀별 카메라 이동은 수행하지 않음."*
3. **로그 이관 작업이 이미 같은 사실을 확인했다.** 541~585행의 `GameLog.Dev.Info` 자리에는 배치 2 작업자가 남긴 주석이 그대로 붙어 있다 — *"이 메서드는 2026-08-18 실측 기준 호출부가 한 곳도 없다 … 즉 이 로그는 현재 실행되지 않는다."*
4. 따라서 **회귀 표면(regression surface)이 존재하지 않는다.** 실행되지 않는 코드를 지우는 것이므로 런타임 동작이 달라질 여지가 없다.

### 제거 방식 — **① 주석 처리 → ② 사용자 테스트 통과 후 삭제** (2단계)

근거 4에 따르면 즉시 삭제해도 논리적으로 안전하지만, **WORKFLOW.md [4]는 "검증 전까지는 제거 대신 비활성화(주석 처리)를 기본으로 한다"를 예외 없이 규정**하고, 최종 삭제 시점까지 *"[6] 사용자 테스트 통과 후, [7] 문서/메모리 업데이트 전"* 으로 못박고 있다. 이 작업은 그 시점이 같은 사이클 안에 존재하므로 규칙을 우회할 이유가 없고, 2단계를 밟는 추가 비용도 사실상 없다. **규칙대로 주석 처리를 먼저 한다.**

- **1단계(이번 구현):** 541~585행 메서드 전체를 주석 처리. 13행 목차 주석에는 `(제거 예정)` 표기만 병기.
- **2단계([6] 통과 후):** 주석 처리한 블록을 완전 삭제하고, 13행 목차를 `4. 카메라 초기 위치/경계/줌 설정 (SetupCamera)` 로 정리.

> ⚠️ **1단계에서 주의할 점:** 주석 처리하면 메서드 본문의 `return`(543행)이 주석 안으로 들어간다. 로그 이관 작업의 자기 검증 스크립트는 `grep -nE 'return'` 류로 구조 불변을 확인하므로(`.claude/agent-memory/game-programmer/logging.md` "이관 작업 시 자기 검증"), 이 파일을 그 스크립트로 재검증하면 **오탐이 난다.** 2단계 삭제로 자연히 해소되지만, 그 사이에 검증을 돌린다면 이 파일은 예외로 취급한다.

---

## 이 작업이 무엇이고 왜 하는가 (자연어 설명 — CLAUDE.md 규칙 13)

멀티플레이에서 **연구소로 유닛 강화를 끝까지 진행해도, 상대 쪽(방에 참가한 플레이어) 화면에서는 연구가 영원히 끝나지 않는 것처럼 보이는 문제**가 간헐적으로 발생할 수 있다. 골드는 정상적으로 빠지고 진행 막대도 정상적으로 돌지만, 시간이 다 되어도 진행 화면이 사라지지 않고 연구 매트릭스로 돌아오지 않는다. 그 트랙은 "연구 중"으로 묶인 채 다시 누를 수도 없다.

원인은 **게임을 여는 쪽(방장) 프로그램이 시작할 때, "연구가 끝났다"는 소식을 상대에게 전달해 주는 담당자를 등록하지 못한 채 그냥 넘어가 버리는 경우**가 있기 때문이다. 그런데 이 등록은 게임 시작 순간 딱 한 번만 시도되고, 놓치면 그 판이 끝날 때까지 두 번 다시 시도되지 않는다. 등록을 놓치는 조건은 "게임 준비물이 다 갖춰지기 전에 통신 담당 부품이 먼저 깨어나는 것"인데, 이 순서는 **회선 상태에 따라 달라지기 때문에 에디터에서는 거의 재현되지 않고 실제 기기·불안정한 회선에서만 가끔 터진다.**

고치는 방법은 새로 발명할 필요가 없다. **같은 폴더의 스킬 담당 부품(`NetworkSkillController`)이 완전히 똑같은 문제를 이미 해결해 두었기 때문**이다. 그 파일은 "시작할 때 못 하면 처음 요청이 들어온 순간 다시 시도한다"는 방식을 쓰고 있고, 두 번 등록되지 않도록 안전장치도 갖고 있다. 이번 작업은 **그 검증된 방식을 연구 담당 부품에 그대로 이식**하는 것이다. 새 설계를 만드는 것이 아니다.

여기에 더해, 이번 수정과 함께 **한 번도 실행되지 않는 죽은 카메라 코드 한 덩어리(②)** 를 정리하고, **유닛이 상대 화면에서 영구히 사라지는 심각한 사고가 났을 때 로그에 원인 판단 정보가 하나도 안 남는 문제(③)** 를 고친다. 세 건 모두 최근 로그 정리 작업 중에 발견된 것들이다.

---

## 1. 배경 — 이 3건이 어떻게 발견되었는가

세 건 모두 **로그 이관 작업**(`Assets/_Project/Docs/_Tasks/2026-08-17/17_19_remaining-layers-log-migration/`) 중에 발견되었다. 그 작업의 원칙은 *"로그 외 코드는 건드리지 않는다"* 였기 때문에, 발견된 코드 결함은 **고치지 않고 코드 주석과 메모리에 기록만 남긴 상태**다.

| 건 | 발견 경위 | 남아 있는 기록 |
|---|---|---|
| ① | 배치 1-B, `NetworkUpgradeController` 이관 중 | 파일 62~66행 주석 「⚠️ 잠정 판정 — 축 A 를 Error 로 올릴 여지가 있다」 |
| ② | 배치 2, `Bootstrap` 계층 이관 중 | 파일 579~582행 주석 + `logging.md` 「배치 2 에서 확인한 사실」 |
| ③ | 배치 2, 중복 로깅 지점 정리 중 | `logging.md` 「중복 로깅 지점 2곳」 |

이번 작업은 그때 미룬 세 건을 **코드 수정 작업으로 정식 처리**하는 것이다.

---

## 2. ① `NetworkUpgradeController` 완료 훅 구독 누락 — 실제 동작 버그

### 2-1. 증상

멀티플레이에서 연구를 착수한 뒤, 연구 시간이 지나 완료되어도 **참가 클라이언트 쪽 연구 패널이 진행 레이어에 갇힌다.** 골드는 착수 시점에 정상 차감되고 진행 막대도 정상 동작한다. 완료 후 그 트랙은 클라이언트 UI에서 계속 "진행 중"으로 잠긴 채 남아 재연구도 취소도 되지 않는다.

> **증상 서술 정정 (실측 근거).** "강화가 아예 걸리지 않는다"는 표현은 정확하지 않다. 서버 쪽 레벨은 **정상적으로 올라간다** — `UnitUpgradeUseCase.TickResearch` 가 완료 처리에서 `_levels[key] = newLevel` 을 먼저 수행한 **다음에** `OnResearchCompleted` 를 발화하기 때문이다(`Application/UseCases/UnitUpgradeUseCase.cs:329~332`). 죽는 것은 **양 클라 브로드캐스트뿐**이며, 그 결과 **순수 클라이언트의 로컬 레벨·진행 레코드만** 갱신되지 않는다. 이 파일 255~257행 주석이 서술한 과거 버그("완료 후 진행 레이어→매트릭스 복귀 실패")와 **정확히 같은 증상의 서버 측 쌍둥이**다.

### 2-2. 원인 (실측 확인)

`Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs` `OnNetworkSpawn`(50~88행):

```csharp
if (ResolveServices() == null)
{
    GameLog.Ops.Warn(LogEvent.NetworkControllerSpawnedWithoutGameServices, ...);
    return;                                           // ← ⓐ
}
if (IsServer)
{
    UnitUpgradeUseCase upgrade = _services.GetUpgradeUseCase();
    if (upgrade != null)
    {
        _completedHandler = OnResearchCompletedOnServer;
        upgrade.OnResearchCompleted += _completedHandler;   // ← ⓑ
    }
}
```

전수 검색 결과:

| 확인 항목 | 결과 |
|---|---|
| `_completedHandler` 에 **대입**하는 곳 | **81행 한 곳뿐** (97행 `OnNetworkDespawn` 은 `null` 로 되돌릴 뿐) |
| `OnResearchCompleted +=` 구독 | **프로젝트 전체에서 82행 한 곳뿐** |

즉 ⓐ에서 `return` 하면 ⓑ는 **그 세션이 끝날 때까지 다시 시도되지 않는다.**

### 2-3. 왜 "반쪽만" 죽는가

`ResolveServices()`(117~121행)는 `if (_services == null) _services = GameServicesLocator.Current;` 로 **호출될 때마다 지연 재조회**한다. 그래서 ServerRpc 를 타는 **요청 경로(착수·취소)는 첫 요청 시점에 스스로 살아나지만**, 스폰 시 한 번만 거는 **구독은 복구 경로가 아예 없다.** 요청은 되는데 완료 통지만 죽는 비대칭 손상이 여기서 나온다.

### 2-4. 언제 터지는가

`GameServicesLocator.Current` 가 아직 비어 있을 때 = **`GameBootstrapper` 의 서비스 등록보다 `NetworkUpgradeController` 의 씬 오브젝트 스폰이 먼저인 경우**다. NGO 스폰 타이밍은 회선 상태에 좌우되므로 에디터에서는 재현이 어렵고, 실기기·불안정 회선에서 간헐적으로 발생한다.

### 2-5. 채택안 — **같은 폴더의 검증된 선례를 이식한다**

`Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs` 가 **똑같은 문제를 이미 해결**해 두었다.

| 요소 | `NetworkSkillController` 위치 | 내용 |
|---|:-:|---|
| `OnNetworkSpawn` 에서 `return` 하지 않음 | 58~68행 | 로그만 남기고 흐름을 계속 진행 |
| 멱등 구독 보장 메서드 | 94~101행 `EnsureStatusSubscription()` | `if (!IsServer \|\| _statusHandler != null) return;` 로 중복 구독 차단 |
| 스폰 시 1차 시도 | 72행 | `EnsureStatusSubscription();` |
| **ServerRpc 처리부에서 복구** | 151행 | 실제 동작(Activate) **전에** 호출 — 이벤트 유실 방지 |

**이것은 새 설계가 아니라, 이미 코드에 존재하고 동작 중인 패턴의 1:1 이식이다.** `NetworkSkillController` 파일 헤더 16~18행 스스로가 *"참고 패턴 = NetworkUpgradeController"* 라고 적고 있어, 두 파일은 원래부터 같은 관례를 공유하도록 설계되었다. 이번 수정은 **뒤늦게 개선된 쪽(Skill)의 개선분을 원본(Upgrade)에 되돌려 맞추는 것**이다.

#### 구체 변경

**(1) 신규 `EnsureUpgradeSubscription()` — `ResolveServices()` 바로 아래 배치**

```
private void EnsureUpgradeSubscription()
    - if (!IsServer || _completedHandler != null) return;          ← 서버 전용 + 멱등 가드
    - UnitUpgradeUseCase upgrade = ResolveServices()?.GetUpgradeUseCase();
    - if (upgrade == null) return;                                  ← 아직 못 얻으면 다음 기회에
    - _completedHandler = OnResearchCompletedOnServer;
    - upgrade.OnResearchCompleted += _completedHandler;
```

**(2) `OnNetworkSpawn`** — 71행 `return` 제거, 74~84행 인라인 구독 블록을 `EnsureUpgradeSubscription();` 호출 한 줄로 교체. 87행 `GameLog.Dev.Info("네트워크 스폰")` 은 그대로 두되, `return` 이 사라져 **이제 항상 실행된다**(선례와 동일 — `NetworkSkillController:75`).

**(3) ServerRpc 처리부에서 복구 호출**

이 파일의 ServerRpc 는 **2개**다.

| # | 메서드 | 행 | `EnsureUpgradeSubscription()` 호출 | 판단 근거 |
|:-:|---|:-:|:-:|---|
| 1 | `RequestResearchServerRpc` | 158~204 | **호출한다.** `ResolveServices()` null 체크(164~169행) 직후, **`TryStartResearch`(192행)보다 앞** | 완료 이벤트를 낳는 유일한 착수 지점. 선례(`NetworkSkillController:151`)와 동일하게 **동작 전에** 구독돼 있어야 브로드캐스트가 유실되지 않는다 |
| 2 | `RequestCancelResearchServerRpc` | 215~240 | **호출하지 않는다.** | 취소는 완료 이벤트를 발생시키지 않는다. 그리고 취소가 도달했다는 것은 **이미 착수 ServerRpc 를 지났다**는 뜻이므로 이 시점에는 구독이 이미 복구돼 있다 — 호출해도 항상 no-op 이다. 무해하지만 불필요한 호출을 늘리지 않는다 |

**서버에서 ServerRpc 를 거치지 않고 연구가 착수될 경로가 없는지 전수 확인했다** — `TryStartResearch` 호출부는 3곳이다.

| 호출부 | 네트워크 모드에서 도달하는가 | 근거 |
|---|:-:|---|
| `Infrastructure/Network/NetworkUpgradeController.cs:192` | ○ | 착수 ServerRpc 본체 |
| `Presentation/UI/ResearchPanelUI.cs:262` | ✕ | 254행 `if (_networkController != null && NetworkContext.IsNetworkActive)` 분기의 **else(싱글) 경로** |
| `Application/Services/AIOpponentController.cs:403` | ✕ | `_aiController` 는 싱글플레이 + `AIConfig.enableAI` 일 때만 생성 — **멀티플레이에서는 항상 null**(`GameBootstrapper.cs:221·648~651`) |

⇒ **착수 ServerRpc 1곳에만 넣으면 네트워크 모드의 모든 연구 완료가 커버된다.**

**(4) `OnNetworkDespawn`(90~100행) 짝 맞추기**

현재 조건은 `if (_completedHandler != null && _services != null)` 이다. 새 구조에서 `_completedHandler != null` 이면 `EnsureUpgradeSubscription()` 이 `ResolveServices()` 로 서비스를 얻은 **뒤에만** 대입했다는 뜻이므로 `_services != null` 은 **항상 참**이 되어 조건으로서 의미를 잃는다. 선례(`NetworkSkillController:78~88`)와 형태를 맞춰 아래로 정리한다.

```
if (_completedHandler != null)
{
    UnitUpgradeUseCase upgrade = _services?.GetUpgradeUseCase();
    if (upgrade != null) upgrade.OnResearchCompleted -= _completedHandler;
    _completedHandler = null;
}
```

- **재경기·씬 전환 시 중복 구독 방지**는 두 겹으로 보장된다: ⓐ Despawn 에서 `_completedHandler = null` 로 해제 완료를 표시하고, ⓑ 재스폰 후 `EnsureUpgradeSubscription()` 의 `_completedHandler != null` 멱등 가드가 두 번째 `+=` 를 막는다.
- 착수 ServerRpc 에서 매 요청마다 호출되지만, 첫 호출 이후에는 멱등 가드에서 즉시 반환되므로 **연구를 100번 눌러도 구독은 1개**다.

**(5) 로그 주석 정리 — `NetworkControllerSpawnedWithoutGameServices` 판정 확정**

67~70행의 **운영 로그 자체는 그대로 유지**한다(레벨·키·메시지·데이터 모두 불변). 다만 62~66행의 아래 주석은 **전제가 사라져 삭제 대상**이 된다.

> *"⚠️ 잠정 판정 — 축 A 를 Error 로 올릴 여지가 있다. 바로 아래 return 때문에 …"*

이 잠정 판정은 **`return` 이 존재한다는 사실 하나에만** 기대고 있었다. `return` 이 사라지고 ServerRpc 복구 경로가 생기면 이 컨트롤러는 `NetworkSkillController` 와 **완전히 같은 상황**이 되므로, `logging.md` 판정 선례표 첫 행 그대로 **`Warn` + `운영` 으로 확정**된다.

| 코드 패턴 | 축 A | 축 B | 키 |
|---|:-:|:-:|---|
| `OnNetworkSpawn` 에서 `GameServicesLocator.Current == null` (**스폰은 계속**) | Warn | 운영 | `NetworkControllerSpawnedWithoutGameServices` |
| (이번 수정 전 상태) 같은 상황인데 **즉시 return** 해 기능이 죽음 | **Error** | 운영 | 같은 키 |

→ 주석은 `NetworkSkillController:60~63` 과 같은 형태(*"여기서 return 하지 않고 EnsureUpgradeSubscription() 이 첫 요청 시 복구한다 → Warn"*)로 교체한다.

> ⚠️ 주석 문구에 `return` 이라는 낱말이 남는 점은 `logging.md` 가 경고한 검증 grep 오탐 요인이다. 선례 파일도 같은 표현을 쓰고 있어 형태 일치를 우선하되, 이 파일은 이번 배치의 로그 이관 검증 대상이 아니므로 실무상 영향은 없다.

### 2-6. 기각안

| 안 | 기각 사유 |
|---|---|
| `OnNetworkSpawn` 에서 **코루틴으로 재시도** | 프로젝트에 선례가 없다. 코루틴 수명(Despawn·씬 전환 시 중단) 관리가 새로 늘고, "언제까지 재시도할 것인가"라는 답 없는 파라미터가 생긴다. 반면 채택안은 **요청이 들어오는 순간**이라는 자연스러운 복구 시점을 이미 갖고 있다 |
| **`GameBootstrapper` 가 등록 완료 후 컨트롤러들에게 역으로 알린다** | 조합 루트가 개별 네트워크 컨트롤러를 알게 되어 **의존 방향이 뒤집힌다**(`Bootstrap → Infrastructure` 단방향 위배). `.claude/MEMORY.md` 의 *"`GameBootstrapper` 는 유일한 의존성 조합 루트"* 원칙에도 어긋난다 |
| `_services` 를 `Update()` 에서 매 프레임 폴링 | 매 프레임 비용 + 상태 전이가 아닌 폴링이라 구조가 지저분하다. 지연 재조회 선례를 두고 새 방식을 만들 이유가 없다 |

### 2-7. 확인이 필요해 남긴 항목 (구현 착수 시 game-programmer 가 확정)

1. **클라이언트 로컬 레벨 미반영이 전투 수치까지 영향을 주는가.**
   - 확인된 것: 전투 판정(`UnitCombatUseCase` 의 데미지·자연회복)은 `NetworkCombatController` 가 `IsServer` 가드 아래에서만 구동하므로(291·332~352행), **실제 게임 판정은 정상 상승한 서버 레벨을 따른다.**
   - 미확인: `UnitCombatUseCase:301` 의 이동속도 배율 조회가 **클라이언트 로컬 이동/보간 경로에서도 호출되는지**. 호출된다면 클라이언트 화면에서 유닛이 강화 전 속도로 보이는 표시 불일치가 추가로 존재한다. 착수 시 `NetworkUnitMovementController` 경로를 확인해 Plan 에 반영한다.
2. **`_services` 가 이전 경기의 서비스를 가리킬 위험.** `.claude/MEMORY.md` 의 *"Scene NetworkObjects → Despawn/Respawn 시 리셋"* 교훈과 관련. Despawn 시 `_completedHandler` 는 null 이 되지만 **`_services` 는 그대로 남는다.** 이는 이번 변경이 새로 만드는 위험이 아니라 `NetworkSkillController` 를 포함한 기존 구조 공통 사항이므로 **이번 범위에서 고치지 않는다.** 다만 재경기 시 강화가 이상하면 이 지점을 먼저 의심하라는 기록으로 남긴다.

---

## 3. ② `SetCameraStartPositionForTeam` 제거 — 죽은 코드

근거·제거 방식은 **문서 최상단 절**에 기술했다(WORKFLOW.md [4] 규정). 여기서는 요약만 둔다.

- 대상: `Bootstrap/GameBootstrapper.Setup.cs` 541~585행 + 13행 목차 주석.
- 호출부 0곳 / 대체 방식(`ViewConverter.Setup`) 확립 / 이관 작업이 이미 실측 확인.
- **결론: 이번 구현에서는 주석 처리, 최종 삭제는 [6] 사용자 테스트 통과 후.**
- 메서드 안의 `GameLog.Dev.Info`(583행)도 함께 비활성화된다. 이 로그는 **현재도 실행되지 않는 로그**이므로 로그 커버리지 손실이 아니다.

---

## 4. ③ `NetworkProductionController` 유닛 스폰 실패 로그에 진단 필드 추가

### 4-1. 현재 상태

`Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` 515~525행:

```csharp
UnitData unit = unitSpawn.SpawnUnitWithId(unitId, unitType, team, spawnCoord);   // 515행
if (unit == null)
{
    GameLog.Ops.Error(LogEvent.ClientStateSyncApplyFailed, "Network", nameof(NetworkProductionController),
                      "SpawnUnitClientRpc — SpawnUnitWithId 실패. 이 유닛은 클라이언트에서 영구히 누락된다",
                      $"Request=SpawnUnit, UnitId={unitId}");                     // ← 좌표·타입·팀 누락
    return;
}
```

`spawnCoord`(509행) · `unitType`(507행) · `team`(508행) 이 **바로 위 스코프에 전부 준비돼 있는데도** 로그 데이터에는 `UnitId` 만 들어간다.

### 4-2. 왜 고쳐야 하는가

이 사건은 *"유닛이 클라이언트에서 **영구히** 누락된다"* 는 재시도 불가 사고이고, 그래서 축 A 가 `Warn → Error` 로 승격된 자리다(518~520행 주석). 그런데 **릴리스 로그에 남는 정보가 `UnitId` 하나뿐**이라 원인 분류가 불가능하다. 실패 원인은 `UnitSpawnUseCase.SpawnUnitWithId` 의 `_grid.GetTile(position) == null` — **즉 "그 좌표 타일이 클라이언트 그리드에 없다"** 이므로(`Application/UseCases/UnitSpawnUseCase.cs:210~212`), **좌표가 있어야 맵 desync 인지 아닌지를 판단할 수 있다.**

참고로 같은 메서드의 **개발 로그**(484행)는 이미 `UnitId · UnitType · Team · Q · R` 를 전부 찍고 있다. 즉 정보는 이미 그 자리에 있었고, **릴리스에 남는 운영 로그 쪽에만 빠져 있다.**

### 4-3. 변경 내용

데이터 문자열만 아래로 교체한다. **레벨·키·`system`·클래스명·메시지는 일절 건드리지 않는다.**

```
$"Request=SpawnUnit, UnitId={unitId}, UnitType={unitType}, Team={team}, Q={q}, R={r}"
```

**`key=` 표기 근거 (`.claude/agent-memory/game-programmer/logging.md` 「`key=value` 표기 규약」 — 실측 확정 매핑):**

| 키 | 규약 | 값 표기 판단 |
|---|---|---|
| `UnitType=` | 확정 매핑에 존재 | 프로젝트 실측 최빈형이 **enum 값**(`UnitType={unitType}` 11건 vs `{unitTypeInt}` 3건) → 507행에서 변환된 `unitType` 사용 |
| `Team=` | 확정 매핑에 존재 | 실측 최빈형이 **enum 값**(`Team={team}` 계열) → 508행에서 변환된 `team` 사용 |
| `Q=` / `R=` | 확정 매핑에 존재 | 실측 최빈형이 `Q={q}, R={r}`(8건). **같은 메서드 484행 개발 로그와도 표기가 일치**하므로 그대로 쓴다 |
| `Request=` | 기존 유지 | 값 `SpawnUnit` 불변 |

> 새 키를 만들지 않는다. 네 키 모두 이미 프로젝트에서 사용 중인 확정 표기다.

### 4-4. 판정(축 A / 축 B)은 건드리지 않는다

현행 배치는 아래와 같고 **그대로 둔다.**

| 지점 | 축 B | 근거 |
|---|:-:|---|
| `Application/UseCases/UnitSpawnUseCase.cs:214`(하위, 원인 계층) | **개발** | 214~222행 주석이 명시 — 상위가 운영으로 남기므로 여기서도 운영이면 같은 사건이 두 줄이 되어 서버 집계가 두 배로 부풀려진다 |
| `Infrastructure/Network/NetworkProductionController.cs:521`(상위) | **운영** | 사건의 최종 처리 지점 |

**`LogRules.md` 1.14 금지 사항 9 — "같은 사건을 두 곳에서 로깅 금지, 최종 처리 지점에서 한 번만"** 이 이미 지켜지고 있다. 필요한 것은 **그 한 줄에 정보를 채우는 것**뿐이다.

> **판정을 뒤집는 대안(예: 하위를 운영으로 올려 좌표를 그쪽에서 남긴다)은 채택하지 않는다.** 그렇게 하면 로그 이관 **배치 1-A·배치 2의 결과를 되돌려야** 하고, `logging.md` 「중복 로깅 지점 2곳」에 확정 기록된 배치를 무효화한다. 같은 사건이 두 줄이 되는 것도 금지 9 위반이다.

---

## 5. 근거 규칙

### 5-1. `GameSystemRules/GameSystemRules_Upgrade.md` — ① 이 깨는 규칙

| 규칙 | 규정 내용 | ① 이 깨는 방식 |
|---|---|---|
| **규칙 9** (서버 권위 네트워크 처리) | *"연구 **완료** 시 서버는 해당 팀의 트랙 레벨을 **양 클라이언트 모두에 전파**한다"* | 서버가 완료 훅을 구독하지 못하면 `ResearchLevelClientRpc` 가 **아예 발신되지 않는다** → 양 클라 전파가 통째로 소실. **정면 위배** |
| **규칙 8** (연구소 운영 — 비공개 정의) | *"완료된 업그레이드 레벨(효과)은 양 클라이언트에 모두 동기화되어 양쪽에 적용된다 … 완료 효과는 양쪽 공개·적용, 진행 상태만 비공개"* | 완료 효과가 클라이언트에 전달되지 않아 **"완료 = 양쪽 공개"가 성립하지 않는다** |
| **규칙 13** (연구 패널 UI — 2-레이어) | *"전환은 `Open(building)` 시점 + 상태 변화 시 `UpdateLayerVisibility()` 가 결정"* | 클라이언트에 상태 변화(레벨 상승)가 도달하지 않아 **진행 레이어에서 매트릭스로 복귀하지 못한다** |
| 규칙 4 ((B) 실시간 소급 적용) | 팀 배율을 사용 지점에서 곱함 | 클라이언트 로컬 레벨이 0에 머물러, **클라이언트 측 배율 조회 경로가 있다면** 강화 전 값을 쓴다 (2-7 ①번 확인 항목) |

- **② ③ 에 해당하는 `GameSystemRules` 규칙: 해당 없음.** ②는 카메라 초기 위치라는 순수 Presentation 배선이고 실행되지도 않는 코드다. ③은 로그 데이터 필드 보강으로 게임 시스템 계약과 무관하다. 두 건의 근거 문서는 `LogRules.md` 와 `.claude/agent-memory/game-programmer/logging.md` 다.

### 5-2. `LogRules.md`

- **1.14 금지 사항 9** — *"같은 사건을 두 곳에서 로깅 금지 — 최종 처리 지점에서 한 번만"*, 그리고 *"이 금지가 원칙 3(`Error` 는 항상 `운영`)과 충돌할 때는 **원칙 1이 우선**한다 — 원인을 가진 계층을 `운영` 으로 두고, 중복되는 상위 호출부를 `개발` 로 내린다."*
  → ③ 에서 **판정 배치를 그대로 유지하는 근거**. (본 건은 예외적으로 하위를 개발, 상위를 운영으로 둔 형태이며 그 사유는 `UnitSpawnUseCase.cs:214~222` 주석에 근거와 함께 확정 기록돼 있다.)
- **1.14 금지 사항 8** — *"매 틱·매 프레임 로깅 금지 — 상태 전이 시점에만"*. ① 에서 `return` 제거로 항상 실행되게 되는 87행 `Dev.Info` 는 **스폰 시 1회**이므로 위반이 아니다.
- **1.5 / 1.14 금지 사항 6** — 운영 로그 이벤트 키 필수. ③ 은 기존 키 `ClientStateSyncApplyFailed` 를 유지한다(신규 키 없음).

### 5-3. `WORKFLOW.md`

- **[4] 기존 로직 제거 규칙** → ② 의 제거 방식 결정 근거(문서 최상단 절).
- **[5-1]·[5-3]** → Testcase.md 작성과 QA 는 **사용자의 명시적 지시가 있을 때만** 진행. 이번 Plan 은 제안하지 않는다.

---

## 6. 파일별 변경 계획

### [수정] `Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs` — ①

| 위치 | 변경 |
|---|---|
| 50~88행 `OnNetworkSpawn` | 71행 `return` **제거**. 74~84행 인라인 구독 블록 → `EnsureUpgradeSubscription();` 한 줄로 교체. 62~66행 「잠정 판정」 주석을 확정 주석으로 교체. 67~70행 운영 로그 **불변**. 87행 `Dev.Info` **불변** |
| 90~100행 `OnNetworkDespawn` | 조건을 `if (_completedHandler != null)` 로, 내부를 `_services?.GetUpgradeUseCase()` 로 정리(선례 형태 일치) |
| `ResolveServices()` 아래 (신규) | **`private void EnsureUpgradeSubscription()` 추가** — 서버 전용 + 멱등 가드 + 지연 해석. XML 주석은 선례(`NetworkSkillController:90~93`) 형태로, 유니티 초급자도 이해 가능한 수준으로 작성(CLAUDE.md 규칙 8) |
| 158~204행 `RequestResearchServerRpc` | `ResolveServices()` null 체크 직후 · `TryStartResearch` 앞에 `EnsureUpgradeSubscription();` 1줄 추가 + 이유 주석 |
| 215~240행 `RequestCancelResearchServerRpc` | **변경 없음**(3절 표 근거) |
| 파일 헤더 주석 1~24행 | 흐름 설명에 "스폰 레이스 시 첫 착수 요청에서 구독 복구" 1줄 보강 |

### [수정] `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs` — ②

| 위치 | 변경 |
|---|---|
| 541~585행 | `SetCameraStartPositionForTeam` 메서드 전체 **주석 처리** + 비활성화 사유·최종 삭제 시점 명시 주석 |
| 13행 | 목차에 `(제거 예정)` 병기 → **2단계에서** 이름 삭제 |

### [수정] `Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs` — ③

| 위치 | 변경 |
|---|---|
| 523행 | `data` 문자열만 `$"Request=SpawnUnit, UnitId={unitId}, UnitType={unitType}, Team={team}, Q={q}, R={r}"` 로 교체. 레벨·키·메시지 **불변** |

### 변경하지 않는 파일 (명시)

- `Assets/_Project/Scripts/Application/UseCases/UnitSpawnUseCase.cs` — ③ 의 하위 지점. 판정·로그 **불변**.
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs` — 이식의 **원본**. 읽기만 한다.
- `Assets/_Project/Scripts/Application/UseCases/UnitUpgradeUseCase.cs` — Application 계층. 이번 수정은 **Infrastructure 구독 측 결함**이므로 손대지 않는다.
- 씬·프리팹 — **Inspector 작업 없음.** 신규 `[SerializeField]` 도, 신규 컴포넌트도 없다 → WORKFLOW [5-2] 해당 없음.

---

## 7. 위험 요소

| # | 위험 | 평가 · 대응 |
|:-:|---|---|
| R1 | **① 은 동작 변경이다.** 지금까지 서비스 미해결 시 `OnNetworkSpawn` 이 조기 종료하던 흐름이, 앞으로는 끝까지 진행한다 | 이어지는 코드는 `EnsureUpgradeSubscription()`(내부에서 null 이면 즉시 반환)과 `Dev.Info` 뿐이라 **null 역참조 위험이 없다.** 78행의 `_services.GetUpgradeUseCase()` 직접 접근이 `ResolveServices()?.` 로 바뀌면서 오히려 안전해진다 |
| R2 | **재경기·씬 전환 시 중복 구독** | 두 겹으로 차단 — Despawn 의 `_completedHandler = null` + Ensure 의 `_completedHandler != null` 멱등 가드. 착수 ServerRpc 에서 매 요청 호출되지만 두 번째부터는 즉시 반환 |
| R3 | **Despawn 해제 조건 변경으로 구독이 남는 경우** | `_services` 가 null 이 되는 경로는 없고(`ResolveServices` 는 대입만 한다), `_services?.GetUpgradeUseCase()` 가 null 을 돌려주면 `-=` 를 못 하지만 그때는 UseCase 인스턴스 자체가 사라진 상황이라 실질 누수가 아니다. `_completedHandler = null` 은 **분기 밖에서 항상 수행**되도록 배치한다 |
| R4 | **클라이언트 인스턴스에 영향** | `EnsureUpgradeSubscription` 첫 줄 `!IsServer` 가드로 차단. 순수 클라이언트는 이전과 완전히 동일하게 동작한다 |
| R5 | **호스트 자신의 강화가 이중 적용될 가능성** | 없다. `ResearchLevelClientRpc` 는 첫 줄 `if (IsServer) return;` 로 서버를 스킵한다(265행). 이번 변경은 이 구조를 건드리지 않는다 |
| R6 | **② 주석 처리로 인한 검증 grep 오탐** | 최상단 절의 ⚠️ 참고. 2단계 삭제로 해소 |
| R7 | **③ 로그 길이 증가** | 한 줄에 필드 4개 추가. 이 로그는 사고 시에만 찍히는 `Error` 라 스팸이 아니며, 같은 메서드 484행 개발 로그가 이미 같은 길이다 |
| R8 | **① 은 실기에서만 재현되는 간헐 버그** | 수정이 먹었는지 즉시 확인할 방법이 없다. 8절에서 로그 기반 확인 절차를 별도로 정의한다 |

---

## 8. 검증 방법 (사용자 실기 확인 — 자연어)

### 8-1. ① — 회귀 확인 (반드시 통과해야 하는 항목)

이 항목은 **간헐 버그가 나지 않은 정상 상황에서 기존 동작이 그대로인지** 확인한다.

1. 방을 만들고 상대와 멀티플레이 대전을 시작한다.
2. **방에 참가한 쪽(방장이 아닌 쪽)** 플레이어가 연구소를 짓고 아무 트랙이나 연구를 시작한다.
3. 골드가 정상적으로 빠지고 진행 막대가 도는지 본다.
4. 연구 시간이 끝나면 **진행 화면이 사라지고 연구 매트릭스로 돌아오는지**, 그리고 그 트랙의 레벨 숫자가 올라가 있는지 본다.
5. 같은 트랙의 다음 레벨을 이어서 연구할 수 있는지 본다.
6. 방장 쪽에서도 같은 절차를 반복해 정상인지 본다.
7. 연구 도중 [취소] 버튼을 눌러 골드가 환불되고 매트릭스로 돌아오는지 본다.

### 8-2. ① — 간헐 버그 자체의 확인 (현실적인 방법)

**이 버그는 "재현해서 확인"하는 것이 사실상 불가능하다.** 발생 조건이 회선 사정에 좌우되는 스폰 순서이고, 코드로 강제 재현 스위치를 넣는 것은 이번 범위 밖이기 때문이다. 따라서 **로그를 근거로 판정**한다.

1. 대전을 여러 판 진행한 뒤, **방장 쪽 로그**에서 아래 문구가 있는 판을 찾는다.
   - 찾을 문구: **"스폰 시점에 IGameServices 를 얻지 못했다"** — 그리고 그 줄의 클래스 이름이 **`NetworkUpgradeController`** 인 것.
   - 에디터로 플레이한 쪽은 `Assets/_Project/Docs/_Logs/_editor/(오늘 날짜)/RuntimeLog.txt` 파일에 남는다.
   - 실기기 빌드 쪽은 파일로 남지 않으므로(파일 기록은 에디터 전용) 안드로이드 로그(logcat)를 봐야 한다.
2. **그 문구가 있는 판에서 8-1의 4~5번이 정상이면 → 수정이 실제로 동작한 것이다.** 이것이 이번 수정의 유일한 직접 증거다.
3. **그 문구가 한 판도 나오지 않았다면 → 그 판들은 애초에 문제 상황이 아니었다는 뜻**이며, 이번 수정의 효과는 판정할 수 없다. 이 경우 "확인 불가"로 기록하고, 8-1 회귀 확인만 통과 처리한다. **"안 났으니 고쳐졌다"고 결론 내리지 않는다.**
4. 그 문구가 있는데도 연구가 여전히 진행 화면에 갇힌다면 → 수정이 불충분한 것이므로 즉시 보고한다.

> 참고: 이 문구가 `NetworkSkillController` 이름으로 찍히는 것은 **정상**이다. 그쪽은 이미 같은 방식으로 복구되도록 되어 있다.

### 8-3. ② — 죽은 코드 주석 처리 확인

1. 유니티에서 **컴파일 오류가 없는지** 확인한다(이 메서드를 부르는 곳이 없으므로 오류가 나면 안 된다).
2. 싱글플레이를 시작해 **카메라가 예전과 똑같은 위치에서 시작하는지** 본다.
3. 멀티플레이를 방장·참가자 양쪽으로 시작해, **양쪽 모두 자기 진영이 화면 아래쪽에 오도록 보이는지** 본다(뷰 반전이 정상인지).
4. 위 3항목이 모두 예전과 같으면, [6] 통과 후 주석 블록을 완전히 삭제한다.

### 8-4. ③ — 로그 필드 확인

이 로그는 **사고가 났을 때만** 찍히므로 일부러 재현할 필요가 없다. 대신 아래를 확인한다.

1. 유니티에서 컴파일 오류가 없는지 확인한다.
2. 멀티플레이에서 유닛을 여러 기 생산해 **양쪽 화면에 모두 정상적으로 나타나는지** 본다(이 로그가 찍히지 않는 것이 정상).
3. 만약 앞으로 실제로 이 로그가 찍히면, 그 줄에 **유닛 종류·팀·좌표(Q, R)** 가 함께 남아 있는지 확인한다.

---

## 9. 이번 작업 범위 밖 (명시)

아래 항목은 발견되었거나 인접해 있지만 **이번 작업에서 다루지 않는다** (CLAUDE.md 규칙 6).

1. **`NetworkUpgradeController` 서버 측 거부 지점의 로그 신규 추가** — 파일 350~352행 주석이 지적한 대로, 서버가 요청을 거부하는 4곳(167·175·187·195행)에는 로그가 한 줄도 없다. 선례(`NetworkProductionController`)는 그 자리에 운영 로그가 있다. **로그 신규 추가는 별도 작업**으로 분리한다.
2. **판정(축 A / 축 B) 재조정** — ③ 의 상·하위 배치를 포함해 어떤 로그의 레벨·존속도 바꾸지 않는다. ① 의 67~70행 로그도 레벨 불변(주석만 확정 문구로 교체).
3. **`_services` 가 이전 경기 인스턴스를 가리키는 문제**(2-7 ②번) — 구조 공통 사항. 이번 수정과 무관하며 별도 판단이 필요하다.
4. **`NetworkSkillController` 쪽 수정** — 이식의 원본이므로 손대지 않는다.
5. **연구 패널 UI 레이아웃·아이콘** — `GameSystemRules_Upgrade.md` 「구현 상태」의 후속 보류 항목.
6. **로그 이관 배치 3·4** (`Presentation` 39건 / `UIManagerTestButtonHandler` 4건) — 별도 작업.
7. **Testcase.md 작성 및 QA 테스트** — WORKFLOW [5-1]·[5-3] 에 따라 **사용자의 명시적 지시가 있을 때만** 진행한다.
8. **Inspector·씬·프리팹 작업** — 이번 변경에는 없다(WORKFLOW [5-2] 해당 없음).

---

## 10. 참고 문서 · 코드

- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` — 규칙 4·8·9·13
- `Assets/_Project/Docs/LogRules.md` — 1.5 / 1.14 금지 사항 6·8·9
- `Assets/_Project/Docs/WORKFLOW.md` — [4] 기존 로직 제거 규칙, [5-1]·[5-2]·[5-3]
- `.claude/agent-memory/game-programmer/logging.md` — 판정 선례표 / `key=value` 표기 규약 / 배치 2 확인 사실
- `Assets/_Project/Docs/_Tasks/2026-08-17/17_19_remaining-layers-log-migration/Plan.md` — 세 건의 발견 경위
- 이식 원본: `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs` 58~101행 · 151행

---

# 11. 구현 결과 (2026-08-18 추가)

> **아래는 계획이 아니라 실제로 벌어진 일의 기록이다.** 위 §1~§10 은 착수 시점의 계획이므로 원문을 그대로 둔다.
> 아래 내용은 **2026-08-18 코드 재실측**으로 확인했다(CLAUDE.md 규칙 10).

## 11-1. 3건 모두 구현 완료 (커밋 `da5eeaab`)

| 건 | 결과 | 실측 확인 |
|:-:|---|---|
| **①** `NetworkUpgradeController` 완료 훅 구독 누락 | **구현 완료** | `EnsureUpgradeSubscription()` 이 **139행에 신설**되어 `ResolveServices()`(117행) 바로 아래 자리에 있다. 호출부는 계획대로 **2곳** — `OnNetworkSpawn` **81행**, `RequestResearchServerRpc` **201행**(`TryStartResearch` 앞). `OnNetworkSpawn` 의 조기 `return` 은 **제거되었고**, 62~70행의 「잠정 판정」 주석은 **확정 주석**(`Warn` + `운영`)으로 교체되었다 |
| **②** `SetCameraStartPositionForTeam` | **주석 처리 완료 (최종 삭제 미완 — 아래 11-3)** | `GameBootstrapper.Setup.cs` **537행**에 `[비활성화 2026-08-18]` 블록으로 남아 있고 비활성화 사유가 함께 적혀 있다. 파일 헤더 목차(**14행**)에도 `— 제거 예정` 이 병기되었다 |
| **③** 유닛 스폰 실패 로그 진단 필드 | **구현 완료** | `NetworkProductionController` **529행** — `$"Request=SpawnUnit, UnitId={unitId}, UnitType={unitType}, Team={team}, Q={q}, R={r}"`. 레벨·키(`ClientStateSyncApplyFailed`)·`system`·클래스명·메시지는 **불변**이며 신규 키도 없다 |

## 11-2. 계획과 달라진 점

| 항목 | 계획 | 실제 |
|---|---|---|
| **행 번호** | §2-2 ⓐ/ⓑ · §4-1 「515~525행」 · §6 「523행」 등 | **전부 어긋난다.** 이관·주석 보강으로 파일이 밀렸다. 위 11-1 의 번호가 **재실측값**이며, 앞으로는 이 표를 기준으로 삼는다 |
| **로그 주석의 참조 대상** | §2-5 (5) 는 `NetworkSkillController:60~63` 과 「같은 형태」로 교체한다고만 적었다 | 실제 주석은 **판정 확정 근거(2026-08-18)** 를 함께 적었고, 같은 키를 쓰는 근거로 **배치 1-A** 를 명시한다 |

> **⚠️ 확인하지 못한 것.** 이 사이클의 인계 메모에는 *"클래스명이 계획과 달라졌다"* 는 항목이 있었으나,
> Plan 이 언급한 클래스 13종을 전수 조회한 결과 **전부 실재하고 이름도 일치**했다.
> **어느 클래스명을 가리키는지 특정할 수 없어 추정하지 않고 그대로 둔다**(CLAUDE.md 규칙 10·12).

## 11-3. ⚠️ 미완 — 반드시 남는 단서 (규칙 10)

1. **② `SetCameraStartPositionForTeam` 의 최종 삭제가 남아 있다.**
   현재는 **주석 처리(비활성화) 상태**다. WORKFLOW.md [4] 대로 **[6] 사용자 테스트 통과 후 · [7] 문서 업데이트 전**에
   537행 블록과 14행 목차 표기를 삭제한다. 그때까지는 §최상단 절의 ⚠️(검증 grep 오탐)도 유효하다.
2. **컴파일은 통과했으나 실기 동작 테스트를 하지 않았다.**
   특히 **① 은 §8-2 가 적은 대로 "재현해서 확인"이 사실상 불가능한 간헐 버그**다.
   **"안 났으니 고쳐졌다"고 결론 내리지 않는다** — 로그에 *"스폰 시점에 IGameServices 를 얻지 못했다"* 가
   `NetworkUpgradeController` 이름으로 찍힌 판에서 8-1 의 4~5번이 정상일 때만 직접 증거가 된다.
3. **`_services` 가 이전 경기 인스턴스를 가리킬 위험**(§2-7 ②)은 **그대로 남아 있다.**
   `NetworkSkillController` 를 포함한 구조 공통 사항이며 **이번 변경이 만든 위험이 아니다.**
   재경기 후 강화가 이상하면 여기를 먼저 의심한다.
4. **§2-7 ① 은 절반만 확인되었다.** `GetUnitMoveSpeedMultiplier` 호출부 실측 결과
   `Presentation/Unit/UnitView.cs` **3곳**(926 · 1030 · 1489행)이 부르므로 **클라이언트 표시 경로에서도 조회된다.**
   다만 **순수 클라이언트에서 그 `_combatUseCase` 가 실제로 주입되는지는 이번에 확인하지 않았다** —
   확인 전까지 "표시 불일치가 있다/없다" 어느 쪽으로도 단정하지 않는다.
5. §9 의 범위 밖 6항목(서버 거부 지점 로그 신규 추가 · 판정 재조정 · `NetworkSkillController` 수정 등)은
   **손대지 않았다.**

## 11-4. 변경 파일 리스트업 (WORKFLOW.md [12])

> **git 명령을 쓰지 않았다**(CLAUDE.md 규칙 5). 아래는 §6 의 계획을 **코드 재실측으로 대조**한 결과다.

```
[수정]
- Assets/_Project/Scripts/Infrastructure/Network/NetworkUpgradeController.cs   (①)
- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.Setup.cs                  (② 주석 처리 — 최종 삭제 미완)
- Assets/_Project/Scripts/Infrastructure/Network/NetworkProductionController.cs (③ data 문자열 1줄)

[변경 없음 — 계획대로]
- Assets/_Project/Scripts/Application/UseCases/UnitSpawnUseCase.cs
- Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs
- Assets/_Project/Scripts/Application/UseCases/UnitUpgradeUseCase.cs
- 씬 · 프리팹 (Inspector 작업 없음 — WORKFLOW [5-2] 해당 없음)

[문서]
- Assets/_Project/Docs/_Tasks/2026-08-18/04_02_upgrade-subscription-fix/Plan.md (이 문서 §11 추가)
```
