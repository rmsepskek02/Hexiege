# game-programmer 메모리 — 인덱스

프로젝트 규칙 단일 소스는 리포지토리의 `CLAUDE.md` / `AGENTS.md` / `Assets/_Project/Docs/`.
여기에는 **코드에서 반복적으로 발을 헛디딘 지점**만 적는다. 충돌하면 항상 프로젝트 문서가 옳다.

## 토픽 파일

- `logging.md` — GameLog / sink / RuntimeLogger 구조, 판정 선례표, `key=value` 확정 매핑,
  전역 로그 훅(4겹 방어 + 스로틀). **로그 관련 작업은 여기부터 읽는다.**
- `network-infra.md` — NGO 컨트롤러 구조, 스폰 레이스, **종료(Shutdown) 시점 뒷정리 관례 +
  `_combatStopped`(게임 종료 후 서버 틱 정지) 패턴**. 네트워크 작업은 여기부터 읽는다.

## 컴파일에서 반복해서 물린 함정

- **`Hexiege.Application` 네임스페이스가 존재한다.** 수식 없는 `Application` 은 `UnityEngine.Application` 이 아니다
  (CS0234 3건 이력). `UnityEngine.Application.logMessageReceived` 처럼 **완전 수식** 필수.
  검증: `grep -nE '(^|[^.a-zA-Z_])Application\.' <file>` 이 0건이어야 한다.
- **`LogLevel` 이 `Hexiege.Application` · `Hexiege.Infrastructure` 양쪽에 있다.**
  인터페이스 구현 시그니처는 `Hexiege.Application.LogLevel` 로 완전 수식해야 구현으로 인정된다.
- `Infrastructure/Debug/LogSessionOwner.cs` 는 **의도적으로 `using` 이 하나도 없다.** 새 타입도 완전 수식으로 쓴다
  (`System.Collections.Generic.Dictionary`, `System.Diagnostics.Stopwatch`).

## NGO(Netcode) 관용구

- **`IsServer` 는 "이 오브젝트가 살아 있는가" 가 아니다.** `NetworkManager.Shutdown()` 뒤에도 참일 수 있어
  늦은 `Update` 가 통과하고 RPC 발신이 *"Rpc methods can only be invoked after starting the NetworkManager!"* 로 터진다.
  → 서버 틱/RPC 발신 자리는 **`if (!IsSpawned || !IsServer) return;`**.
  `IsSpawned` 를 **앞에** 두는 이유는 단락 평가로 싱글플레이(미스폰)에서 `IsServer` 를 건드리지 않기 위해서다.
  선례: `NetworkUnit:291` · `NetworkCombatController:310`(Update) · `NetworkGameEndController:457` · `UnitFactory:533`.
- **host 는 서버이자 클라이언트다.** `ClientRpc` 안의 로그는 `if (IsServer) return;` **뒤**에 둬야
  같은 사건이 host 파일에 두 줄로 남지 않는다(LogRules 1.14 금지 9).

## 알려진 잔존 구멍 (2026-08-19 기준)

- ~~`NetworkCombatController` 의 게임 종료 구독 0건 / `OnUnitDied` 가드 부족~~ → **2026-08-19 해소.**
  `_combatStopped` 플래그 + 6개 핸들러 가드. 상세는 `network-infra.md` 「네트워크 종료 시점 뒷정리」 참조.
- **아직 안 봄**: `NetworkProductionController` / `NetworkBuildingController` / `NetworkUpgradeController` 의
  같은 형태 구멍(전수 점검 미실시). `NetworkUnit.SetAnimState`(`NetworkUnit.cs:170`)는 `IsServer` 만 본다.
- **싱글플레이의 같은 낭비**: `GameBootstrapper.Update`(530~590행)도 게임 종료 후 쿨다운/파도/HoT/자연회복/
  연구/물안개 틱을 계속 돌린다. 네트워크가 없어 오류는 안 나고 낭비만 있다.

## 자기 검증 스크립트

- 중괄호 개폐 균형은 **주석·문자열 리터럴을 걷어낸 뒤** 세야 한다. 단독행 카운트나 `{` 총계는 오탐이 잦다
  (문자열 보간 `$"{x}"` 때문). 파이썬으로 스트립 후 세는 것이 유일하게 신뢰할 수 있다.
- ⚠️ **주석에 `Debug.Log` / `GameLog.Dev.` / `Pos=` 같은 검증 grep 대상 낱말을 쓰지 마라.** 그 자체가 오탐이 된다.
