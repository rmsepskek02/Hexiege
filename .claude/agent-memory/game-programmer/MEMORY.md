# game-programmer 메모리 (Hexiege)

상세 주제 노트: [`logging.md`](./logging.md) — GameLog / 판정 선례 / `key=value` 규약 / 네임스페이스 함정

## 네트워크 컨트롤러 — 스폰 레이스 대응 표준 패턴 (2026-08-18 확정)

씬 NetworkObject 컨트롤러는 `GameBootstrapper` 의 `GameServicesLocator.Register` 보다 **먼저 스폰될 수 있다**
(NGO 스폰 타이밍은 회선 상태에 좌우 → 에디터 재현 거의 불가, 실기에서만 간헐 발생).

**요청 경로는 `ResolveServices()` 지연 재조회로 저절로 살아나지만, `OnNetworkSpawn` 에서 한 번만 거는
이벤트 구독은 복구 경로가 없다** → "요청은 되는데 완료 브로드캐스트만 죽는" 비대칭 손상이 난다.

표준 형태 (`NetworkSkillController` = 원본, `NetworkUpgradeController` = 이식본):

```csharp
private void EnsureXxxSubscription()
{
    if (!IsServer || _handler != null) return;              // 서버 전용 + 멱등 가드
    SomeUseCase uc = ResolveServices()?.GetXxx();
    if (uc == null) return;                                 // 못 얻으면 다음 기회에
    _handler = OnXxxOnServer;
    uc.OnXxx += _handler;
}
```

- **호출 지점은 반드시 2곳**: `OnNetworkSpawn`(1차) + **해당 이벤트를 낳는 ServerRpc 의 동작 직전**(복구).
  스폰에만 두면 `return` 을 걷어내도 결과가 같다 — 서비스가 null 이면 그대로 구독을 못 한 채 끝난다.
- `OnNetworkSpawn` 에서 서비스 미해결 시 **조기 종료 금지.** 로그만 남기고 흐름을 계속한다.
- `OnNetworkDespawn` 짝: `if (_handler != null) { uc?.OnXxx -= _handler; _handler = null; }`
  — `_handler = null` 이 재경기 재구독을 허용하고, 멱등 가드가 중복 구독을 막는 2겹 구조.
- 이벤트를 **낳지 않는** RPC(예: 취소)에는 넣지 않는다 — 그 지점은 항상 no-op.

관련 파일: `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs`,
`.../NetworkUpgradeController.cs`

## 죽은 코드 제거 절차 (WORKFLOW.md [4])

**예외 없이 2단계**: ① 주석 처리(비활성화) → ② 사용자 테스트([6]) 통과 후 삭제([7] 전).
호출부 0곳을 실측했더라도 즉시 삭제하지 않는다.

- 주석 처리 시 **XML doc `///` 도 `//` 로 바꿀 것** — 언어 요소 없는 `///` 는 CS1587 경고.
- ⚠️ 주석 안으로 들어간 `return` / `Debug.Log` 낱말이 **검증 grep 을 오탐**시킨다. 해당 파일은 예외 처리.
- 현재 비활성화 대기 중: `Bootstrap/GameBootstrapper.Setup.cs` 의 `SetCameraStartPositionForTeam`
  (팀별 카메라 이동은 `GameBootstrapper.Network.cs` 의 `ViewConverter.Setup` 뷰 반전으로 대체됨).

## 검증 스니펫

```bash
# 중괄호 균형 — 반드시 주석·문자열 리터럴을 걷어낸 뒤 셀 것(인라인 중괄호·문자열 보간이 오탐)
grep -nE '(^|[^.a-zA-Z_])Application\.' <path>   # 0건이어야 함 (Hexiege.Application 네임스페이스 함정)
grep -cE '^[[:space:]]*GameLog\.(Ops|Dev)\.' <path>   # 활성 로그 건수(주석 제외)
```

`LogEvent` enum 멤버는 **36개**(`Application/Interfaces/ILogSink.cs`). 로그 작업 후 무변경 확인 대상.
