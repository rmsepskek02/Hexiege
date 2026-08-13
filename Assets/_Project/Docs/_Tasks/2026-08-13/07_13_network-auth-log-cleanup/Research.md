# Research — 네트워크·인증 계층 Debug.Log 정리

**작성일:** 2026-08-13
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-13/07_13_network-auth-log-cleanup/`
**기준 문서:** `Assets/_Project/Docs/LogRules.md`
**후속 문서:** [Plan.md](Plan.md)
**현재 상태:** **조사 완료 / 코드·프리팹·씬·에셋 무변경** (이 문서 작성 시점에 어떤 파일도 수정하지 않았다. git 명령도 실행하지 않았다.)

---

## 이 조사가 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

게임을 만들다 보면 개발자가 프로그램 안에 **"지금 여기까지 왔다"** 는 쪽지를 잔뜩 붙여 둡니다.
문제가 생겼을 때 어디서 잘못됐는지 찾기 위한 것입니다.

지금 이 게임의 **접속·방 만들기·로그인** 부분에는 그런 쪽지가 **209장** 붙어 있습니다.
그런데 이 중 상당수는 **이미 테스트가 끝나서 더 볼 이유가 없는 쪽지**입니다.
예를 들면 *"건물 만들기 성공"* 같은 것인데, 건물이 만들어졌는지는 **화면을 보면 바로 알 수 있으므로**
굳이 쪽지로 남길 이유가 없습니다.

사용자가 **"이미 테스트가 끝난 불필요한 쪽지는 떼는 것을 원칙으로 하자"** 고 정했습니다.
이건 새로운 규칙이 아니라, **원래 있던 규칙을 이제야 지키는 것**입니다
(`LogRules.md` 금지사항 1이 이미 이런 쪽지를 진단용으로 쓰지 말라고 정해 두었습니다).

### 그런데 전부 떼면 안 됩니다 — 이 조사의 핵심

쪽지 중에는 **성격이 완전히 다른 것이 섞여** 있습니다.

- **떼야 하는 쪽지**: *"건물 만들기 성공"*, *"화면 켜짐"* — 개발 중에만 쓰던 흔적
- **남겨야 하는 쪽지**: *"인터넷 서버에 연결 실패했음"*, *"로그인 서버가 응답을 안 함"* —
  이건 **플레이어 기기에서만 벌어지는 일**이라, 이 쪽지가 없으면 나중에 *"게임이 안 켜져요"* 라는
  문의가 왔을 때 **원인을 알아낼 방법이 아예 없어집니다.**

그래서 **이번 작업의 본체는 "지우기"가 아니라 "가르기"** 입니다.
어떤 쪽지가 어느 쪽인지 **판단하는 기준을 먼저 세우는 것**이 이 조사의 목적입니다.

### 이 조사에서 새로 밝혀진 것 네 가지

**첫째, 상위 조사가 준 숫자에 오차가 있었습니다.**
일부는 **이미 주석 처리되어 실행되지 않는 줄**이고, 일부는 **설명글 안에 적힌 글자**였습니다.
실제로 실행되는 쪽지만 다시 셌습니다. (§2-2)

**둘째, 조사 도중에 파일이 바뀌었습니다.**
동시에 진행 중이던 다른 작업이 **07:16에 완료**되면서 로그인 관련 파일 두 개의 숫자가 달라졌습니다.
바뀐 뒤 기준으로 다시 쟀고, **어느 시점 기준인지 문서에 명시**했습니다. (§2-3)

**셋째, "다른 방식으로 옮겨 쓰면 된다"는 전제가 성립하지 않습니다.**
규칙 문서는 쪽지를 `RuntimeLogger`라는 다른 도구로 옮겨 쓰라고 되어 있는데,
실제로 그 도구를 확인해 보니 **특정 작업을 디버깅하는 동안에만 잠깐 쓰는 도구**였고,
지금 이 프로젝트에서 **그 도구를 쓰는 곳은 한 군데도 없습니다.** (§6)

**넷째, 쪽지를 떼면 프로그램이 깨지는 자리가 있습니다.**
쪽지가 **"만약 ~라면"의 유일한 내용물**인 자리가 **9곳** 있습니다.
쪽지만 떼면 빈 껍데기가 남아 **프로그램이 아예 컴파일되지 않거나, 오류를 조용히 삼키게** 됩니다. (§8)

---

## 1. 조사 범위와 방법

### 1-1. 조사 대상

상위 에이전트가 지목한 8개 파일을 **1순위 대상**으로 하되, 계층 전체 분포를 함께 확인했다.

| # | 확인 항목 | 방법 | 결과 |
|:-:|----------|------|------|
| 1 | 파일별 로그 건수·레벨 분해 | `grep` 실측 후 전 건 육안 통독 | §2 |
| 2 | 상위 조사 수치의 정확성 | 주석/문서주석 제외 재계수 | **정정 2건** — §2-2 |
| 3 | 동시 작업으로 인한 수치 변동 | mtime + 재실측 | **변동 확인** — §2-3 |
| 4 | 진단/운영을 가르는 판단 기준 | 209건 전수 통독 후 귀납 | **기준 5개 도출** — §3 |
| 5 | 각 분류의 대표 사례 | 파일:라인 인용 | §4 |
| 6 | 판단이 갈리는 로그 | 별도 수집 | **38건 / 6유형** — §5 |
| 7 | `RuntimeLogger` 대체 가능 여부 | `RuntimeLogger.cs` 전문 통독 + 호출처 전수 검색 | **대체 불가** — §6 |
| 8 | 매 프레임·매 틱 경로 로그 | `Update`/`FixedUpdate`/`LateUpdate` 본문 스캔 + 루프 스캔 | §7 |
| 9 | 제어 흐름과 얽힌 로그 | 중괄호 없는 `if`/`else` · `catch` 본문 스캔 | **9곳** — §8 |
| 10 | 민감 데이터 출력 | 식별자 키워드 스캔 | **19건** — §9 |

> **읽기만 수행했다.** 코드·프리팹·씬·에셋 어느 것도 수정하지 않았고, git 명령도 실행하지 않았다.

### 1-2. 계수 방법 (재현 가능하도록 명시)

```
grep -cE 'Debug\.Log\('        # Debug.Log 호출 라인 수
grep -cE 'Debug\.LogWarning\(' # Debug.LogWarning 호출 라인 수
grep -cE 'Debug\.LogError\('   # Debug.LogError 호출 라인 수
```

- **1 호출 = 1건**으로 센다. 문자열이 여러 줄에 걸친 로그(예: `NetworkGameManager.cs:255`)도 **1건**이다.
- 위 명령은 **주석 처리된 줄과 설명글(`///`) 안의 글자까지 함께 센다.** 그 보정이 §2-2다.

---

## 2. 실측 결과

### 2-1. 리포지토리 전체 분포 (맥락용)

| 레벨 | 전체 건수 |
|------|:-:|
| `Debug.Log` | 214 |
| `Debug.LogWarning` | 115 |
| `Debug.LogError` | 99 |
| **합계** | **428** |

`Assets/_Project/Scripts` 전체 기준. 이 중 **209건(48.8%)이 이번 대상 8개 파일에 몰려 있다.**

### 2-2. **정정 ①** — 상위 조사 수치에 실행되지 않는 줄이 섞여 있었다

| 파일:라인 | 내용 | 성격 |
|-----------|------|------|
| `UnityServicesInitializer.cs:113` | `//     Debug.Log("[Network] 기존 UGS 세션 로그아웃 — 토큰 갱신을 위해 재로그인 수행.");` | **이미 주석 처리됨** |
| `UnityServicesInitializer.cs:115` | `// Debug.Log("[Network] UGS 익명 로그인 수행...");` | **이미 주석 처리됨** |

> **결과:** 상위 조사의 `UnityServicesInitializer.cs` **8건**은 **실행되는 것 6건 + 죽은 주석 2건**이다.
> 이 2건은 **"제거"가 아니라 "이미 제거된 것의 잔해"** 이므로 Plan에서 별도 취급한다.

추가로, 조사 초기 스냅샷에서 `FirebaseAuthService.cs:546`과 `LoginBootstrapper.cs:380`의
`/// [DEBUG-TEMP] 한 줄 로그를 Debug.Log(Logcat)로 출력한다.` 라는 **설명글**이 계수에 잡혔다.
해당 줄들은 §2-3의 동시 작업으로 이미 사라졌다.

### 2-3. **정정 ②** — 조사 도중 파일이 변경되었다 (동시 작업 `[DEBUG-TEMP]` 제거)

| 파일 | 조사 시작 시점 (약 07:05) | **재실측 (07:17)** | mtime |
|------|:-:|:-:|:-:|
| `FirebaseAuthService.cs` | Log 16 / Warn 2 / Err 4 = **22** | Log 14 / Warn 2 / Err 4 = **20** | 2026-08-13 **07:16:52** |
| `LoginBootstrapper.cs` | Log 6 / Warn 2 / Err 2 = **10** | Log 4 / Warn 2 / Err 2 = **8** | 2026-08-13 **07:16:10** |

- 나머지 6개 대상 파일의 mtime은 모두 **2026-08-08 23:49:56** 으로 조사 중 무변경이다.
- `AnonymousWarningPopup.cs` · `NetworkErrorPopup.cs`(실제 경로: `Presentation/UI/Views/Login/`)는
  **현재 `Debug.*` 0건**으로, 동시 작업이 이미 정리를 마쳤다.

> **이 문서의 `FirebaseAuthService.cs` 수치와 라인 번호는 모두 "2026-08-13 07:17 스냅샷" 기준이다.**
> 동시 작업이 추가 커밋을 하면 다시 달라질 수 있으므로, **Plan 실행 직전에 재실측이 필요하다**(§11-①).

### 2-4. 파일별 실측 + 분류 결과 (실행되는 호출만)

분류 근거는 §3의 기준 5개다.

| # | 파일 | Log | Warn | Err | **계** | **진단(제거)** | **운영(유지)** | **경계(확인 필요)** |
|:-:|------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| 1 | `Infrastructure/Network/NetworkGameManager.cs` | 27 | 2 | 16 | **45** | **20** | 18 | 7 |
| 2 | `Infrastructure/Network/NetworkBuildingController.cs` | 13 | 15 | 12 | **40** | **13** | 19 | 8 |
| 3 | `Infrastructure/Network/NetworkProductionController.cs` | 13 | 17 | 5 | **35** | **13** | 14 | 8 |
| 4 | `Infrastructure/Network/LobbyManager.cs` | 14 | 4 | 10 | **28** | **12** | 14 | 2 |
| 5 | `Infrastructure/Auth/FirebaseAuthService.cs` ※ | 14 | 2 | 4 | **20** | **13** | 6 | 1 |
| 6 | `Application/UseCases/LoginUseCase.cs` | 8 | 7 | 3 | **18** | **6** | 5 | 7 |
| 7 | `Infrastructure/Network/NetworkGameEndController.cs` | 16 | 0 | 0 | **16** | **13** | 0 | 3 |
| 8 | `Infrastructure/Network/UnityServicesInitializer.cs` ※※ | 6 | 0 | 1 | **7** | **4** | 1 | 2 |
| | **합계** | **111** | **47** | **51** | **209** | **94** | **77** | **38** |

- ※ `FirebaseAuthService.cs` — 07:17 스냅샷. **확정 수치가 아니다**(§2-3).
- ※※ `UnityServicesInitializer.cs` — 죽은 주석 2건 **제외**한 값(§2-2).

**비율: 진단 45.0% / 운영 36.8% / 경계 18.2%.**
즉 **일괄 삭제하면 209건 중 115건(55%)을 잘못 지운다.**

### 2-5. 파일별 특징 (통독하며 확인한 것)

| 파일 | 특징 |
|------|------|
| `NetworkGameEndController.cs` | **`LogWarning`·`LogError`가 0건이고 16건 전부 `Debug.Log`다.** 계층에서 **진단 비율이 가장 높다(81%)**. 실패 경로에 로그가 전혀 없다는 뜻이기도 하다 — §10-③ |
| `LobbyManager.cs` | **`OnError` 통지 경로가 없다(0건).** 실패 시 `null`을 반환하고 로그만 남긴다 → **로그가 유일한 실패 기록**이다. 이것이 §3의 기준 O-3을 도출한 근거다 |
| `NetworkGameManager.cs` | `OnError?.Invoke` **25건**. 에러 로그와 사용자 통지가 **쌍으로** 배치되어 있다 |
| `NetworkBuildingController.cs` · `NetworkProductionController.cs` | `LogWarning`이 가장 많다(15·17건). 대부분 **서버가 클라이언트 요청을 규칙대로 거부**하는 정상 경로다 → §5의 최대 경계 유형 |
| `FirebaseAuthService.cs` | `Debug.Log` 14건 중 **10건이 식별자·이메일을 출력**한다 → §9 |

---

## 3. 분류 기준 도출 — 209건을 통독해서 얻은 것

> **아래 기준은 일반론이 아니라, 이 코드베이스의 실제 로그 209건을 읽고 귀납한 것이다.**
> 각 기준 옆에 그 기준을 도출하게 만든 실제 코드를 표시했다.

### 3-1. 진단 로그로 판정하는 기준 (제거 대상) — D-1 ~ D-5

| ID | 기준 | 판정 근거 (왜 지워도 되는가) | 도출한 실제 코드 |
|:-:|------|------------------------------|------------------|
| **D-1** | **진입·구독·초기화 흔적** — 분기 없이 "여기 도달했다"만 알린다 | 실패했을 때 별도로 남는 로그가 이미 있다. 성공 경로의 도달 사실은 **다음 단계 로그로 대체 확인**된다 | `NetworkProductionController.cs:183` *"서버 측 생산 이벤트 구독 완료"* / `NetworkBuildingController.cs:61` *"NetworkBuildingController 스폰. IsServer=..."* |
| **D-2** | **정상 성공 결과 통보** — 성공했다는 사실이 **화면에 즉시 반영**된다 | 건물이 지어졌는지·유닛이 나왔는지는 **플레이어가 보고 있다.** 로그는 정보를 추가하지 않는다 | `NetworkBuildingController.cs:209` *"서버: 건물 배치 성공"* / `NetworkProductionController.cs:281` *"서버 유닛 생산 완료"* |
| **D-3** | **RPC 수신 확인 + 인자 덤프** — 정상 플레이 중 **조작할 때마다** 출력된다 | 동기화가 깨졌을 때 유용했으나 **동기화 자체는 이미 검증 완료**다. 남겨 두면 정상 플레이가 로그로 도배된다 | `NetworkBuildingController.cs:239·460·613` *"...ClientRpc 수신. Id=..., Type=..."* / `NetworkProductionController.cs:454` |
| **D-4** | **개발 중 임시 추적 문구** — 문장 자체가 **일회성 조사용**임을 드러낸다 | 특정 버그를 쫓던 흔적이다. 그 버그는 이미 닫혔다 | `LobbyManager.cs:274` *"FirstOrDefault null — 비교 실패"* / `LobbyManager.cs:262` *"- 로비: {l.Name}, matchId=..."* (조회된 로비 **전수 열거**) |
| **D-5** | **대칭 쌍 로그** — 시작/완료를 짝으로 남긴다 | 사이에 아무 분기가 없다면 두 줄 다 흐름 표시일 뿐이다 | `NetworkGameManager.cs:341·359` *"Disconnect 시작." / "Disconnect 완료."* / `:803·815` Heartbeat 시작·정지 |

### 3-2. 운영 로그로 판정하는 기준 (유지 대상) — O-1 ~ O-4

| ID | 기준 | 판정 근거 (왜 남겨야 하는가) | 도출한 실제 코드 |
|:-:|------|------------------------------|------------------|
| **O-1** | **외부 서비스 호출 실패** — Relay / Lobby / Matchmaker / UGS / Firebase 예외 | **개발자 기기에서 재현할 수 없다.** 원인은 서버 측·네트워크·계정 상태에 있고, `e.Message`가 **유일한 단서**다 | `LobbyManager.cs:111` *"Lobby 생성 실패: {e.Message}"* / `NetworkGameManager.cs:402` *"StartMatchmakingAsync 예외: {e.Message}"* / `UnityServicesInitializer.cs:139` |
| **O-2** | **불변식 위반 — 발생하면 무조건 버그** | *"GameBootstrapper를 찾을 수 없다"*, *"UseCase가 null"*, *"NetworkManager.Singleton이 null"* 은 **정상 플레이에서 절대 발생하지 않는다.** 발생 자체가 신고 가치다 | `NetworkBuildingController.cs:142·152·246·253` / `NetworkGameManager.cs:713·734` / `NetworkGameManager.cs:667` *"LoadGameScene: 서버가 아니므로 무시."* |
| **O-3** | **사용자 통지 경로가 없는 실패** — 실패가 로그 외에는 **어디에도 기록되지 않는다** | `LobbyManager`는 `OnError`가 **0건**이고 실패 시 `null`을 반환한다. 호출자가 `null`을 어떻게 처리하든 **실패한 이유는 로그에만 있다.** 지우면 완전 무음이 된다 | `LobbyManager.cs` 전체 (`OnError` 검색 결과 0건) — 특히 `:184` *"CreateOrJoin 실패 (matchId=...): {e.Message}"* |
| **O-4** | **시간 초과 / 재시도 소진** — 대기 끝에 포기한 지점 | 타이밍 의존 실패라 재현이 어렵다. 게다가 **해결 방법까지 메시지에 적혀 있다** | `NetworkProductionController.cs:539` *"{maxWait}초 초과 ... Network Prefabs List에 유닛 프리팹이 등록되어 있는지 확인하세요."* |

### 3-3. 가르는 질문 (기준을 한 줄로 압축)

> **"이 로그가 없는 상태에서 플레이어가 *'게임이 안 돼요'* 라고 문의했을 때, 원인을 좁힐 수 있는가?"**
>
> - **좁힐 수 있다 → 진단** (제거). 화면에 이미 보이거나, 개발자가 재현할 수 있다.
> - **좁힐 수 없다 → 운영** (유지). 그 기기·그 순간·그 계정에서만 벌어진 일이다.

이 질문 하나로 §3-1과 §3-2가 모두 설명된다.
D-1~D-5는 전부 *"개발자가 재현 가능하거나 화면에 보인다"* 에 해당하고,
O-1~O-4는 전부 *"플레이어 기기에서만 벌어지고 흔적이 로그뿐"* 에 해당한다.

---

## 4. 대표 사례 (실제 코드 인용)

### 4-1. 진단 로그 대표 5건

**① D-2 — 성공 결과 통보 (`NetworkBuildingController.cs:209`)**
```csharp
Debug.Log($"[Network] 서버: 건물 배치 성공. Id={placed.Id}, Type={buildingType}, Team={team}, Coord={coord}, 차감골드={cost}");
```
바로 다음 줄에서 클라이언트에 스폰 RPC가 나가고 **화면에 건물이 나타난다.** 로그가 없어도 성공 여부를 안다.

**② D-3 — RPC 수신 덤프 (`NetworkBuildingController.cs:613`)**
```csharp
Debug.Log($"[Network] DemolishBuildingClientRpc 수신. Id={buildingId}");
```
같은 파일에 `SpawnBuildingClientRpc 수신`(239), `UpgradeBuildingClientRpc 수신`(460)이
**동일 패턴으로 3벌** 있다. 건물을 조작할 때마다 3종이 번갈아 찍힌다.

**③ D-4 — 임시 조사 흔적 (`LobbyManager.cs:256~274`)**
```csharp
Debug.Log($"[Matchmaker] Lobby 전체 조회: {results.Results.Count}개. 검색 matchId={matchId}");

foreach (var l in results.Results)
{
    string storedMatchId = l.Data != null && l.Data.ContainsKey(MatchIdKey)
        ? l.Data[MatchIdKey].Value : "없음";
    Debug.Log($"[Matchmaker] - 로비: {l.Name}, matchId={storedMatchId}");   // ← 조회된 로비 전수 열거
}
...
if (lobby != null)
    Debug.Log($"[Matchmaker] 매칭 Lobby 발견! Name={lobby.Name}, Id={lobby.Id}");
else
    Debug.Log($"[Matchmaker] FirstOrDefault null — 비교 실패");             // ← 문구 자체가 임시 조사용
```
*"FirstOrDefault null — 비교 실패"* 는 **LINQ 메서드 이름이 그대로 노출된** 전형적인 임시 로그다.
`- 로비:` 줄은 **§7의 성능 항목이자 §8의 제어 흐름 항목**이기도 하다.

**④ D-1 — 구독 완료 흔적 (`NetworkProductionController.cs:183`)**
```csharp
Debug.Log("[Network] NetworkProductionController: 서버 측 생산 이벤트 구독 완료.");
```
같은 문구가 `NetworkGameEndController.cs:113`, `NetworkCombatController.cs:169`,
`NetworkHealthSync.cs:84`에도 **거의 그대로** 있다.

**⑤ D-5 — 대칭 쌍 (`NetworkGameManager.cs:803·815`)**
```csharp
803:  Debug.Log("[Network] Heartbeat 코루틴 시작.");
815:  Debug.Log("[Network] Heartbeat 코루틴 정지.");
```
사이에 실패 분기가 없다. 실제 Heartbeat 실패는 `LobbyManager.cs:498`이 따로 잡는다(그쪽은 운영).

### 4-2. 운영 로그 대표 4건

**① O-3 — 유일한 실패 기록 (`LobbyManager.cs:182~186`)**
```csharp
catch (LobbyServiceException e)
{
    Debug.LogError($"[Network] CreateOrJoin 실패 (matchId={matchId}): {e.Message}");
    return null;   // ← 호출자에게는 "null"만 전달된다. 이유는 로그에만 있다.
}
```
`LobbyManager`에는 `OnError` 이벤트가 **없다.** 이 로그를 지우면 **랜덤 매칭 실패의 원인이 영구히 사라진다.**

**② O-2 — 불변식 위반 (`NetworkGameManager.cs:711~714`)**
```csharp
if (NetworkManager.Singleton == null)
{
    Debug.LogError("[Network] StartNetworkHost: NetworkManager.Singleton 이 null 입니다.");
    return false;
}
```
`.claude/MEMORY.md`의 *"GameBootstrapper가 유일한 의존성 조합 루트"* 전제가 깨진 상태다. 반드시 신고돼야 한다.

**③ O-1 — 외부 서비스 예외 (`UnityServicesInitializer.cs:137~140`)**
```csharp
catch (Exception e)
{
    Debug.LogError($"[Network] UGS 초기화 실패: {e.Message}");
    onFailure?.Invoke(e);
}
```
UGS 초기화 실패는 **지역·네트워크·프로젝트 설정에 따라 달라진다.** 개발 기기에서 재현되지 않는다.

**④ O-4 — 재시도 소진 (`NetworkProductionController.cs:539`)**
```csharp
Debug.LogError($"[Network] RetryInitializeUnitView: {maxWait}초 초과. UnitId={unitData.Id} 초기화 실패. " +
               "NetworkManager의 Network Prefabs List에 유닛 프리팹이 등록되어 있는지 확인하세요.");
```
**메시지 자체가 조치 방법을 담고 있다.** 이런 로그는 지우면 손해가 명백하다.

### 4-3. 진단과 운영이 **한 메서드 안에 섞여 있는** 예 (`NetworkGameManager.cs:212~262`)

```csharp
Debug.Log($"[Network] HostGame 시작. 방 이름: {lobbyName}");                        // ← D-1 진단

string relayJoinCode = await _relayManager.CreateRelayAsync();
if (string.IsNullOrEmpty(relayJoinCode))
{
    const string errorMsg = "Relay 할당 실패. 네트워크 상태를 확인하세요.";
    Debug.LogError($"[Network] {errorMsg}");                                        // ← O-1 운영
    OnError?.Invoke(errorMsg);
    return;
}
...
Debug.Log($"[Network] Host 게임 시작 완료. Lobby Code: {lobby.LobbyCode}");          // ← 경계 (§5-B2)
OnHostStarted?.Invoke(lobby.LobbyCode);
```

> **이것이 "파일 단위 일괄 처리"가 불가능한 이유다.** 한 메서드 안에 세 분류가 모두 있다.

---

## 5. 경계 사례 — 판단이 갈리는 38건 (Plan에서 사용자 확인 필요)

### B1. 게임플레이 규칙에 따른 정상 거부 — **17건 (최대 유형)**

서버가 클라이언트 요청을 **규칙대로 거절**하는 경로다. **버그가 아니라 정상 동작**이다.

| 파일 | 라인 | 내용 |
|------|------|------|
| `NetworkProductionController.cs` | 357, 699, 764, 817 | 팀 불일치 |
| | 380 | 골드 부족 |
| | 391 | 인구 부족 |
| `NetworkBuildingController.cs` | 171 | 팀 불일치 |
| | 182, 412 | 골드 부족 / 업그레이드 골드 부족 |
| | 394, 565 | 업그레이드·철거 소유권 불일치 |
| | 403 | 업그레이드 불가 (최고 단계) |
| | 556 | Castle은 철거할 수 없음 |
| `NetworkProductionController.cs` | 904 | 큐 추가 실패 통보 (`reason` 인자) |
| `NetworkBuildingController.cs` | 314 | 건물 배치 실패 통보 (`reason` 인자) |

**양쪽 논거:**
- **제거 근거** — 골드 부족·인구 부족은 **UI가 이미 막고 있어야 하는** 정상 경로다. 로그로 남길 일이 아니다.
- **유지 근거** — **팀 불일치·소유권 불일치**는 성격이 다르다. 정상 클라이언트라면 애초에 보낼 수 없는 요청이므로
  **변조 클라이언트 탐지 신호**로 읽을 수 있다.

> **→ 사용자 확인 필요: "골드/인구 부족(정상)" 과 "팀/소유권 불일치(비정상 요청)" 를 분리 처리할 것인가?**

### B2. 성공했지만 **상태 전이**를 기록하는 로그 — 6건

| 파일:라인 | 내용 |
|-----------|------|
| `NetworkGameManager.cs:255` | Host 게임 시작 완료. **Lobby Code** |
| `NetworkGameManager.cs:386` | 매칭 완료. **MatchId** |
| `NetworkGameManager.cs:479` | 매칭 Host 게임 시작 완료. Lobby Code |
| `LobbyManager.cs:104` | Lobby 생성 완료 |
| `LobbyManager.cs:177` | CreateOrJoin 완료. matchId |
| `NetworkGameEndController.cs:173` | 서버: 게임 종료 감지. 승리 팀·**랜덤매칭 여부** |

**논거:** D-2(성공 통보)로 보면 제거지만, **`MatchId`·`Lobby Code`는 나중에 특정 경기를 찾아내는 유일한 열쇠**다.
승패 분쟁이나 매칭 문의가 들어왔을 때 이 값이 없으면 추적이 불가능하다.

> **→ 사용자 확인 필요: 매칭 식별자(MatchId / Lobby Code)와 경기 결과는 남길 것인가?**

### B3. 접속·종료 감지 — 3건

| 파일:라인 | 내용 |
|-----------|------|
| `NetworkGameManager.cs:128` | 클라이언트 측 서버 연결 끊김 감지 |
| `NetworkGameManager.cs:692` | Client 접속 감지 |
| `NetworkGameEndController.cs:278` | 포기 처리 (포기자 ClientId) |

**논거:** 연결 끊김은 **정상 종료와 장애를 구분할 수 없다.** 같은 로그가 두 상황에서 모두 나온다.
장애 추적에는 필요하지만, 정상 종료마다 찍히면 소음이다.

### B4. 폴링 진행 로그 — 2건

| 파일:라인 | 내용 |
|-----------|------|
| `NetworkGameManager.cs:514` | `RelayJoinCode 대기 중... ({i+1}/{maxRetries})` |
| `NetworkGameManager.cs:583` | `Lobby 대기 중... ({i+1}/{maxRetries})` |

**논거:** 매칭이 **몇 번 만에 성사됐는지**는 매칭 품질 지표다. 그러나 **성공 시에도 매번 출력**된다.
루프 안에 있어 §7의 성능 항목이기도 하다.

### B5. 동일 문구 5회 중복 — 5건 (`LoginUseCase.cs`)

```
137, 166, 195, 233, 304:
Debug.LogWarning("[LoginUseCase] ○○: UGS 미연결 — 멀티플레이 기능이 제한될 수 있습니다.");
```

자동 로그인 / 익명 / Google / 이메일 / 이메일 인증 완료 — **5개 경로가 같은 경고를 복제**하고 있다.

**논거:** 운영 신호(멀티플레이 불가)는 맞지만 **5벌은 과하다.**
`BridgeToUGSAsync()` 내부(`:494`)에 이미 실패 원인 로그가 있어 **상위 5건은 중복일 수 있다.**

> **→ 사용자 확인 필요: 5건을 1건(호출부 → `BridgeToUGSAsync` 내부)으로 합칠 것인가?**

### B6. 인증 경로 분기 + 되돌릴 수 없는 조작 — 5건

| 파일:라인 | 내용 | 성격 |
|-----------|------|------|
| `LoginUseCase.cs:127`, `:225` | 이메일 미인증 계정 → 인증 대기 | 로그인 실패 문의의 핵심 분기 |
| `UnityServicesInitializer.cs:129` | UGS 세션 없음 — 익명 로그인 폴백 | **익명 폴백은 계정이 뒤바뀌는 사고 경로** |
| `UnityServicesInitializer.cs:134` | UGS 초기화 완료. SignedIn / PlayerId | 세션 상태 확정 지점 |
| `FirebaseAuthService.cs:463` | `Current user deleted. UID={uid}` | **계정 삭제 — 복구 불가 조작** |

**논거:** 전부 `Debug.Log`(정보 레벨)라 D-1/D-2로 보이지만, **되돌릴 수 없거나 계정이 뒤바뀌는 지점**이다.

> **→ 사용자 확인 필요: 계정 삭제·익명 폴백은 `Debug.LogWarning`으로 승격할 것인가?**

### 경계 사례 집계

| 유형 | 건수 |
|------|:-:|
| B1 게임플레이 정상 거부 | 17 |
| B2 상태 전이 성공 로그 | 6 |
| B3 접속·종료 감지 | 3 |
| B4 폴링 진행 | 2 |
| B5 동일 문구 5회 중복 | 5 |
| B6 인증 분기·비가역 조작 | 5 |
| **합계** | **38** |

---

## 6. `RuntimeLogger` 대체 여부 — **대체 불가 (이 조사의 핵심 발견)**

### 6-1. `LogRules.md`가 요구하는 것

`LogRules.md` **금지 사항 1**:
> *"`Debug.Log` 등 Unity 콘솔 로그를 RuntimeLog 대신 사용 금지 — 콘솔 로그는 Claude가 읽을 수 없음"*

`.claude/agent-memory/game-programmer/logging.md`(읽기만 수행)도 같은 취지로 정리하고 있다:
> *"raw `Debug.Log`/`Debug.LogWarning`를 진단 로그로 쓰면 규칙 위반 — 반드시 `RuntimeLogger` 경유"*

### 6-2. 실측 — `RuntimeLogger`는 **상시 로깅 도구가 아니다**

`Assets/_Project/Scripts/Infrastructure/Debug/RuntimeLogger.cs` 전문을 읽고 확인한 사실:

| # | 확인 사실 | 근거 |
|:-:|----------|------|
| 1 | **파일 기록은 `#if UNITY_EDITOR` 안에만 있다** | 47·59·123·163행의 `#if UNITY_EDITOR` 블록. 빌드에서는 `_writer` 필드조차 존재하지 않는다 |
| 2 | **`BeginSession()`을 호출해야만 파일이 열린다** | 125행 `if (_writer != null)` — 세션이 없으면 파일 기록이 통째로 건너뛰어진다 |
| 3 | **세션이 없으면 결국 `Debug.Log`를 호출한다** | 141~152행 `switch (level)` → `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` |
| 4 | **현재 호출처가 리포지토리 전체에 0건이다** | `grep -rn 'RuntimeLogger\.'` 결과 — 정의 파일 자신 외 **일치 없음** |
| 5 | **로그 코드는 애초에 제거 전제다** | `LogRules.md` 「생성 및 제거」: *"**로그를 출력하는 코드**는 작업 완료 후 반드시 제거"* |

### 6-3. 결론 — 세 갈래로 나뉜다

> **`RuntimeLogger`는 "특정 버그를 쫓는 동안에만 켜는 일회용 조사 장비"이지, 상시 운영 로그의 목적지가 아니다.**
> 4번(호출처 0건)이 그 증거다 — **쓰고 나서 전부 제거된 상태**가 이 도구의 정상 상태다.

따라서 209건의 처리는 **세 갈래**가 된다.

| 갈래 | 대상 | 처리 | 근거 |
|:-:|------|------|------|
| **①** | **진단 94건** | **삭제** | `LogRules.md` 금지 사항 1. `RuntimeLogger`로 옮기는 것은 **오답** — 세션 없이 호출하면 §6-2 ③에 의해 **`Debug.Log`로 되돌아가고 문자열 조립 비용만 늘어난다** |
| **②** | **운영 77건** | **`Debug.LogWarning` / `Debug.LogError` 그대로 유지** | 빌드 Logcat에 남아야 장애 추적이 된다. `RuntimeLogger`로 옮기면 **빌드에서 파일 기록이 없어**(§6-2 ①) 이득이 0이고 호출 경로만 길어진다 |
| **③** | **향후 신규 디버깅** | `RuntimeLogger` + `BeginSession` | 이것이 `LogRules.md`가 의도한 본래 용법이다 |

### 6-4. 남길 로그의 레벨 판정 기준

| 남길 로그의 성격 | 레벨 | 근거 |
|------------------|:-:|------|
| 동작은 계속되나 기능이 제한됨 (UGS 미연결, Heartbeat 1회 실패) | `LogWarning` | `LogRules.md` 로그 레벨 표 — *"예상 밖이지만 동작은 계속됨"* |
| 로직 오류·불변식 위반·외부 서비스 실패로 흐름 중단 | `LogError` | 같은 표 — *"반드시 원인 파악이 필요한 상황"* |
| **정보(`Debug.Log`)로 남길 운영 로그** | — | **§2-4 기준으로 0건이 목표.** 운영 로그가 `Debug.Log`로 남아 있다면 레벨 승격 대상이다(→ B6) |

> **주의:** `LogRules.md`의 로그 레벨 표는 **런타임 로그(파일 기록) 형식**을 규정한 것이다.
> 콘솔 로그의 레벨 선택에 그대로 적용해도 되는지는 **문서에 명시되어 있지 않다** → §10-①.

---

## 7. 성능 영향 (모바일 대상)

### 7-1. 매 프레임 경로 — **0건 (확인 완료)**

`Update()` / `FixedUpdate()` / `LateUpdate()` 본문을 스캔한 결과,
**네트워크·인증 계층의 8개 대상 파일에 매 프레임 로그는 없다.**

> 상위 지시의 *"매 프레임·매 틱 경로 확인"* 항목은 **해당 없음**으로 판정한다.

### 7-2. 루프·폴링 안의 로그 — 4건

| 파일:라인 | 위치 | 호출 빈도 | 할당 |
|-----------|------|-----------|------|
| `LobbyManager.cs:262` | `foreach (var l in results.Results)` | **조회된 로비 수만큼** | 매 반복 문자열 보간 1회 + `l.Data` 조회 |
| `NetworkGameManager.cs:514` | `for (i < maxRetries)` Relay 대기 | 재시도 횟수만큼 | 매 반복 보간 1회 |
| `NetworkGameManager.cs:583` | `for (i < maxRetries)` Lobby 대기 | 재시도 횟수만큼 | 매 반복 보간 1회 |
| `NetworkGameEndController.cs:443` | `foreach (var netObj in spawnedCopy)` Despawn | **재경기 시 동적 오브젝트 수만큼** | 매 반복 보간 + `netObj.name` **접근** |

**`NetworkGameEndController.cs:443`이 가장 무겁다:**
```csharp
foreach (var netObj in spawnedCopy)
{
    if (netObj != null && netObj.IsSpawned == true && netObj.IsSceneObject == false)
    {
        Debug.Log($"[Network] StartRematch: 동적 NetworkObject Despawn. name={netObj.name}");
        netObj.Despawn();
    }
}
```
`netObj.name`은 **Unity 네이티브 오브젝트의 이름을 읽는 호출로, 매번 관리 힙에 `string`을 새로 만든다.**
재경기 시점에 살아 있는 유닛·건물 전부를 도는 루프이므로, **한 번의 재경기에서 수십~수백 회** 발생한다.
재경기 전환은 이미 씬 재로드로 프레임이 튀는 구간이라 **체감 악화가 겹치는 자리**다.

### 7-3. 구조적 비용 — **모든 로그가 릴리스 빌드에 그대로 들어간다**

| 확인 항목 | 결과 |
|-----------|------|
| `[Conditional("...")]` 특성 사용 | **0건** |
| `#if UNITY_EDITOR` / `DEVELOPMENT_BUILD` 로그 게이팅 | **0건** |
| 커스텀 로그 래퍼 (`ENABLE_LOG`, `HEXIEGE_LOG` 등) | **0건** |

> **결론:** 리포지토리 전체 **428건의 `Debug.*` 호출이 전부 릴리스 APK/AAB에 포함되어 실행된다.**
> `Debug.Log`는 릴리스에서도 문자열을 조립하고 Logcat에 기록한다.
> **보간 문자열(`$"..."`)은 호출 시점에 무조건 `string`을 할당**하므로, 레벨 필터로도 이 비용은 제거되지 않는다.
>
> 이 사실은 **진단 94건을 지우는 것 자체가 곧 GC 압력 감소**임을 뜻한다.
> 다만 **정량 수치는 측정하지 않았다** → §10-②.

### 7-4. 참고 — 대상 8파일 **밖**의 고빈도 경로

이번 범위 밖이지만 같은 계층에서 확인된 항목을 기록한다.

```
NetworkHealthSync.cs:292   Debug.Log($"[Network] 유닛 HP 동기화. UnitId={unitId}, 적용 데미지={diff}, 현재HP={unit.Hp}");
NetworkHealthSync.cs:334   Debug.Log($"[Network] 유닛 힐 동기화. ...");
NetworkHealthSync.cs:415   Debug.Log($"[Network] 건물 HP 동기화. ...");
```

**피격·회복이 발생할 때마다** 호출된다. 다수 유닛이 교전 중이면 §7-2의 루프보다 **누적 빈도가 훨씬 높다.**
`MistShrine` 물안개 힐은 **1초 discrete 틱 아우라**(`GameSystemRules_Buildings.md` MistShrine 물안개 힐 시스템)라
범위 안 아군 수만큼 힐 이벤트가 초당 발생한다 — `:334`가 그만큼 찍힌다.

> **→ Plan의 「이번 범위 밖」에 기록하고, 후속 작업으로 제안한다.**

---

## 8. 영향 범위 — 제어 흐름과 얽힌 로그 **9곳**

로그를 지우면 **컴파일이 깨지거나 동작이 달라지는** 자리다. 전수 스캔 결과다.

### 8-1. 중괄호 없는 `if`/`else`의 **유일한 본문** — 3쌍 (6건)

**① `LobbyManager.cs:271~274`**
```csharp
if (lobby != null)
    Debug.Log($"[Matchmaker] 매칭 Lobby 발견! Name={lobby.Name}, Id={lobby.Id}");
else
    Debug.Log($"[Matchmaker] FirstOrDefault null — 비교 실패");

return lobby?.Id;
```
> 두 로그만 지우면 `if (lobby != null) else` 가 남아 **컴파일 오류(CS1525)** 다.
> **`if`/`else` 구문 전체(4줄)를 함께 지워야 한다.** `return lobby?.Id;`는 로그와 무관하므로 동작은 불변이다.

**② `NetworkGameManager.cs:718~721` / ③ `:739~742`**
```csharp
bool result = NetworkManager.Singleton.StartHost();
if (result)
    Debug.Log("[Network] NetworkManager.StartHost() 성공.");
else
    Debug.LogError("[Network] NetworkManager.StartHost() 실패.");

return result;
```
> **성공 쪽(`Debug.Log`)은 진단, 실패 쪽(`Debug.LogError`)은 운영**이다 — **한 구문 안에서 분류가 갈린다.**
> 성공 로그만 지우려면 `if (result) ... else ...` 를 `if (!result) Debug.LogError(...)` 로 **반전**해야 한다.
> **단순 삭제가 불가능한 자리다.**

### 8-2. `catch` 본문이 **로그 1줄뿐** — 5곳

로그를 지우면 `catch { }` 가 되어 **예외를 조용히 삼킨다.**

| 파일:라인 | 예외 종류 | 분류 |
|-----------|-----------|:-:|
| `NetworkGameManager.cs:400~403` | `StartMatchmakingAsync` 예외 | 운영 |
| `NetworkGameManager.cs:629~632` | 티켓 삭제 오류 (무시) | 운영 |
| `LobbyManager.cs:379~382` | Relay Join Code 업데이트 실패 | 운영 |
| `LobbyManager.cs:444~447` | Lobby 나가기 실패 | 운영 |
| `LoginUseCase.cs:387~390` | UGS SignOut 예외 (무시) | 운영 |

> **5곳 모두 운영 로그로 판정되어 이번 제거 대상이 아니다.** 즉 **실제 충돌은 발생하지 않는다.**
> 다만 *"catch 안의 로그는 지우지 않는다"* 는 것을 **작업 지침으로 명시**해야 사고를 막는다.

### 8-3. `catch` 본문에 다른 문장이 함께 있는 곳 — 21곳

`Debug.LogError(...)` + `OnError?.Invoke(...)` 또는 `return null;` 형태다.
로그만 지워도 컴파일·동작에 문제가 없다. **다만 전부 운영 로그로 판정되어 제거 대상이 아니다.**

### 8-4. 얽힘 없음이 확인된 항목

| 확인 항목 | 결과 |
|-----------|------|
| 한 줄 `if (...) Debug.Log(...);` 형태 | **0건** |
| 로그 안에서 **부수효과가 있는 식**을 호출하는 경우 | **미확인** → §10-④ |

---

## 9. 부가 발견 — 민감 데이터 출력 **19건** (`LogRules.md` 금지 사항 3 위반)

`LogRules.md` **금지 사항 3**: *"민감한 데이터 출력 금지 — 사용자 ID, 인증 토큰 등"*
`RuntimeLogger.cs` 33~35행 주석도 같은 경고를 담고 있다:
> *"PlayerId, 인증 토큰, 세션 키 등 '민감한 데이터'는 로그(message/data)에 절대 출력하지 말 것."*

| 파일 | 라인 | 출력 항목 |
|------|------|-----------|
| `FirebaseAuthService.cs` ※ | 115, 146, 191, 265, 289, 399, 424, 463 | **Firebase UID** |
| | 318, 365, 424 | **이메일 주소** |
| `LoginUseCase.cs` | 131 | Firebase UID |
| | 485 | **UGS PlayerId** |
| `UnityServicesInitializer.cs` | 123, 134 | UGS PlayerId |
| `NetworkGameManager.cs` | 196 | UGS PlayerId |
| `LobbyManager.cs` | 104, 177, 299, 319, 338 | Lobby 이름 / matchId (준식별자) |

※ 07:17 스냅샷 기준. `:424`는 UID와 이메일을 **동시에** 출력한다:
```csharp
Debug.Log($"[FirebaseAuth] 이메일 연동 성공. UID={FirebaseUID}, Email={email}");
```

> **다행히 이 19건 중 대부분이 §3 기준으로 이미 진단(제거) 판정을 받았다** —
> 즉 **이번 정리가 곧 금지 사항 3 위반 해소**가 된다.
> 다만 §5-B2·B6에서 **유지 후보로 올라온 것**(`UnityServicesInitializer.cs:134`, `FirebaseAuthService.cs:463` 등)은
> **유지한다면 마스킹이 필요**하다 → Plan 사용자 확인 항목.
>
> **이 항목은 상위 지시에 없던 부가 발견이므로, 처리 여부 자체가 사용자 판단 대상이다.**

---

## 10. 미확인 항목 (추정하지 않고 남긴다 — CLAUDE.md 규칙 10)

| # | 항목 | 왜 확인하지 못했는가 |
|:-:|------|---------------------|
| **①** | `LogRules.md`의 **로그 레벨 표(INFO/WARN/ERROR)가 콘솔 로그에도 적용되는지** | 해당 표는 「1. 런타임 로그」 = **파일로 저장되는 로그** 절 안에 있다. 콘솔 로그 레벨 선택 규칙은 문서 어디에도 없다. **문서 공백이다** |
| **②** | 진단 94건 제거의 **정량 성능 이득** (프레임·GC 할당) | 이 환경에 Unity가 없어 프로파일링 불가. §7-3은 **구조적 근거**일 뿐 측정값이 아니다 |
| **③** | `NetworkGameEndController.cs`에 **실패 경로 로그가 없는 것이 의도인지** | `LogWarning`·`LogError`가 0건이다. 재경기·포기 처리에 실패 분기가 실제로 없는 것인지, 로그가 누락된 것인지 **코드만으로 확정 불가** |
| **④** | 로그 인자 안에서 **부수효과 있는 식**을 호출하는 곳이 있는지 | 209건 통독 중 발견하지 못했으나, **전수 정적 검증은 수행하지 않았다.** 제거 시 game-programmer가 개별 확인 필요 |
| **⑤** | `FirebaseAuthService.cs` **최종 건수** | 동시 작업(`[DEBUG-TEMP]` 제거)이 07:16:52에 파일을 변경했다. 추가 변경 가능성이 있어 **07:17 스냅샷 이상으로 확정할 수 없다** |
| **⑥** | `GameSystemRules.md` 및 하위 문서에 **로그 관련 규칙이 있는지** | 인덱스와 `AuthSystemRules.md`를 확인한 결과 **로그를 규정한 규칙은 없다.** 이번 작업의 근거는 전적으로 `LogRules.md` + `CLAUDE.md`다 |

---

## 11. Plan으로 넘기는 판단 항목

| # | 항목 | 근거 절 |
|:-:|------|:-:|
| ① | **Plan 실행 직전 `FirebaseAuthService.cs` 재실측** — 동시 작업 종료 확인 후 | §2-3, §10-⑤ |
| ② | 경계 사례 **38건 / 6유형**에 대한 사용자 확인 | §5 |
| ③ | 분할 단위를 **파일 기준으로 할지 분류 기준으로 할지** 결정 | §4-3 (한 메서드에 세 분류 공존) |
| ④ | **제어 흐름 얽힘 9곳**의 개별 처리 방침 — 특히 `NetworkGameManager.cs:718·739`의 `if`/`else` 반전 | §8-1 |
| ⑤ | 민감 데이터 19건 처리 — **제거로 자동 해소되는 분 / 마스킹이 필요한 분** 구분 | §9 |
| ⑥ | 죽은 주석 2건(`UnityServicesInitializer.cs:113·115`) 처리 | §2-2 |
| ⑦ | **범위 밖**으로 명시할 것 — `[DEBUG-TEMP]` 제거 작업 / `NetworkHealthSync` 고빈도 로그 / 릴리스 빌드 로그 게이팅 도입 | §2-3, §7-4, §7-3 |
