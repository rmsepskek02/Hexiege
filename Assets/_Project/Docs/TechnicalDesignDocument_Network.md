# Hexiege — 네트워크 기술 설계

Netcode 구조·동기화·맵 데이터 계약·결과 화면 이탈 구현·치팅 방어의 기술 원본이다. 공통 아키텍처와 개발 환경은 [TechnicalDesignDocument.md](TechnicalDesignDocument.md), 플레이어 동작 규칙은 [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md), 경기 운영 규칙은 [GameSystemRules_MatchFlow.md](GameSystemRules/GameSystemRules_MatchFlow.md)를 참조한다.

> 2026-10-13 역할별 분리: 아래 본문은 기존 TDD의 네트워크 절을 보존했다. 본문의 「이 문서」·「아래」는 종전 TDD 문맥을 포함하며 개발용 강제 실패 플래그 절은 공통 TDD에 있다. 날짜별 상태는 해당 시점 기록이고 전체 구현·검증 완료의 선언이 아니다.

<a id="네트워크-설계"></a>

## 🌐 네트워크 설계

### 전투 결과 표현·지속 피해 전달 계약

`TowerCombatUseCase`가 확정한 결과 identity·Impact 위치/시각·Race snapshot을 `GameEvents`와 `NetworkCombatController`가 표현용으로 전달한다. 현재 대상 재조회나 tracer 도착으로 서버 피해를 새로 만들거나 지연하지 않는다.

Recipient poison/burn은 실제 writer의 `(target lifetime, TimedEffectKind.Damage)` 단일 레코드에 identity/revision/poison-burn provenance를 연결한다. network current value·late-init/restoration/shutdown은 single presenter로 전달하며, 동일 owner refresh의 noRestart·overwrite 종료/교체·취소/만료/사망 후 StopEmitting/drain/lease 반환을 구분한다.

위 현행 기술계약의 승인 원문과 당시 진행 단계는 [구현·검증 이력의 TDD 원문](History/ImplementationVerificationHistory.md#tdd-handoff)에 보존했다. preset 키·Mushroom marker와 에셋 연결 사양은 [VFXSFXList.md](Assets/VFXSFXList.md#2026-10-10-확정-연결-계약과-진행-단계), 현재 개별 연결·검증 범위는 해당 목록과 [UnitCombatAssetMatrix.md](Assets/UnitCombatAssetMatrix.md)에서 확인한다. 이 기술계약 요약은 실기 수용이나 전체 migration 완료를 선언하지 않는다.

### 네트워크 동작 규정은 별도 문서로 이관됐다 — 최상위 원칙 · 이탈 시 승패 기록 · 인게임 이탈 처리 (2026-10-12)

**[쉬운 말로]** 이 절 맨 앞에 있던 「최상위 원칙」과, 아래쪽에 있던 사용자 확정 규칙 집합 두 개는 **제품이 어떻게 동작해야 하는가**를 정하는 규정이다. 규칙을 찾는 사람은 `GameSystemRules/` 로 가므로 기술 설계서가 아니라 규칙 문서에 있어야 해서, [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) 로 **본문 그대로** 옮겼다. 🔴 **이 문서에는 사본을 두지 않는다** — 사본은 원본이 바뀌는 순간 조용히 거짓이 된다.

| 이관된 것 | 새 위치 |
|---|---|
| 🔴 최상위 원칙 — 플레이어는 자신이 Host 인지 Client 인지 알 수 없다 | 새 문서의 첫 절 (번호 없음 — 규칙들 위에 서는 원칙이다) |
| 이탈 시 승패 기록 — 규칙 집합 | 새 문서 규칙 1 |
| 인게임 이탈 처리 — `ReconnectionHandler` 정리 | 새 문서 규칙 2 |

- 🔴 **이 절 아래의 네트워크 설계 전부가 그 원칙의 적용을 받는다.** 원칙의 내용과 충돌했을 때 무엇을 고치는지는 새 문서가 단일 소스이며 여기에 옮겨 적지 않는다.
- ✅ **옮기지 않고 이 문서에 남긴 것** — Netcode 구조 · 동기화 전략 · 무작위 맵 시작 동기화(데이터 계약) · **결과 화면 이탈 판정·통보 구조** · 치팅 방어. 구조·구현 방법이라 이 문서의 몫이다. 결과 화면 이탈의 **규정**은 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17·18·19 가 단일 소스다.
- ⚠️ **앞으로** 사용자가 확정한 네트워크 **동작 규정**은 새 문서에 쓰고, 이 문서에는 구조·구현 방법만 쓴다. 어느 쪽인지 애매하면 쓰기 전에 사용자에게 먼저 묻는다.
- 이관 경위와 절별 판정 근거: `_Tasks/2026-10-12/15_28_network-rules-migration/Plan.md`.

---

### Netcode for GameObjects

#### 아키텍처
```
클라이언트 A ←→ Unity Relay ←→ 클라이언트 B
                (NAT 관통)
```

**특징**:
- P2P 방식 (Host-Client 모델)
- Host가 서버 역할 (Authoritative)
- Unity Relay로 NAT 관통 자동 처리

#### 동기화 전략

**NetworkVariable (자동 동기화)**:
```csharp
// 자원 (서버 → 클라이언트)
NetworkVariable<int> resources = new NetworkVariable<int>(
    value: 1000,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

**ServerRpc (클라이언트 → 서버)**:
```csharp
[ServerRpc(RequireOwnership = false)]
void SpawnUnitServerRpc(UnitType type, Vector3 position) {
    // 서버에서 검증 + 실행
}
```

**ClientRpc (서버 → 클라이언트)**:
```csharp
[ClientRpc]
void ShowEffectClientRpc(Vector3 position) {
    // 모든 클라이언트에 이펙트 표시
}
```

#### 동기화 대상
| 데이터 | 동기화 방식 | 빈도 |
|--------|------------|------|
| **초기 무작위 맵** | Host 생성·검증 → 공용 `NetworkMapTransfer`의 1KB chunk 전송 → Client SHA-256·semantic 검증/ACK (⚠️ 축약 표기 — 전송 객체의 **수명 규정**은 아래 「`NetworkMapTransfer` 전송 프로토콜」 절이 단일 소스다) | 경기 시작 1회 |
| **건물 건설** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **유닛 생성** | ServerRpc → NetworkObject.Spawn | 이벤트 |
| **타일 점령** | NetworkList<TileOwnership> | 변경 시 |
| **자원** | NetworkVariable<int> | 변경 시 |
| **본기지 체력** | NetworkVariable<int> | 변경 시 |
| **유닛 이동** | 클라이언트 예측 (AI 동일 로직) | - |
| **유닛 이동·SimulationFacing** | B3 경기 단위 `ReducerAuthoritative` single-writer에 `UnitServerTrajectoryPlanner`·`UnitTrajectoryStep`·`UnitPathCheckpointTracker` 연속 이동을 연결. Unity self-validation·사용자 Editor smoke PASS, 새 Android 역할교대·25종·Legacy rollback은 미완료. Client reducer·Simulation Root write 금지 | 연속 |
| **유닛 행동 상태** | 목표: 값 기반 `UnitActionSnapshot` 복제; 현재: A1/A2 계약·pose seam과 B2 이동 Shadow 구현, 권위 런타임은 위치/회전 + `UnitAnimState`와 개별 RPC 혼합 | 상태 변경 시 |
| **공격 타격 결과** | 목표: `AttackImpactResult` (`AttackerInstanceId + SequenceId + HitIndex` 기반 정규 키); 현재: HP 동기화 + 공격자 FIFO 표현 큐 | Impact 시 |

> 🔴 **2026-09-16 — 위 「초기 무작위 맵」 행의 축약 표기를 다듬었다.** 종전 표기는 `persistent \`NetworkMapTransfer\`` 였는데, **그 줄만 읽으면 「전송 객체가 계속 살아 있어야 한다」로 오해된다.** 정의 절(「`NetworkMapTransfer` 전송 프로토콜」)의 원문은 *"persistent network connection에 **존재하는** 공용 `NetworkMapTransfer`"* 이며 🔴 **`persistent` 가 꾸미는 것은 `network connection` 이지 `NetworkMapTransfer` 가 아니다.** 요구 사항은 **「전송 수명주기를 Lobby/Game 씬 객체에 귀속시키지 않는다」**이고, 회차마다 전송 객체를 동적으로 스폰하는 현재 구현은 **씬에 귀속돼 있지 않으므로 그 규정을 만족한다**(규칙 문서 쪽 같은 표현은 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 16 의 *"씬에 종속되지 않는 공용 전송 경로"*). **정의 절 원문은 한 글자도 고치지 않았다** — 고친 것은 이 표의 축약 표기 한 자리뿐이며, **개정 이력 표의 `0.33.0`(2026-07-19) 행도 당시 기록이라 손대지 않았다.**

#### 무작위 맵 시작 동기화 (확정 설계, 1~3단계 구현 · 잔여 있음)

> **[🔴 2026-09-15 상태 갱신 — 아래 2026-09-03 블록은 그 시점의 기록이라 한 글자도 고치지 않고 그대로 두고, 현재 상태는 이 블록이 답한다.]**
> **1~3단계가 전부 구현·실기 검증까지 끝났다.** 2026-09-03 블록이 *"이 절의 맵 생성·검증·전송·해시 대조는 전부 미구현으로 읽어야 한다"* 고 적은 것은 **더 이상 사실이 아니다** — 생성·검증·폴백(2단계) · 전송·해시 대조·Client 재생성 검증·씬 전환 게이트(3단계) · **재경기 맵**이 모두 동작하며 2026-09-15 실기(멀티 9판 · 싱글 2판)로 확인됐다.
> 🔴 **함께 사라진 것**: 아래 2단계 목록의 **「테스트 모드 설정 필드」는 추가됐다가 삭제됐다**(커밋 `8c83317`). 그 항목에 달린 「삭제 대상」 표식은 **이제 「삭제 완료」로 읽는다** — 목록에서 지우지 않는 이유는 **끝난 단계의 범위 목록은 기록이기 때문**이다.
> 🔴 **그 삭제의 결과**: canonical 바이트 형식이 바뀌어 **`MapVersion` 이 `1` → `2`** 가 됐고(아래 「범위 경계」 문단의 *"초기값 `1`"* 은 **초기값의 기록이라 여전히 참**이다 — 현재 값을 적는 자리는 「전송 package」 절이며 그쪽은 이미 `2` 로 고쳐져 있다), **canonical 바이트 수 공식은 `315 + 4N`**(N = 중립 광산 수)이 됐다.
> ⚠️ **여전히 남은 것 넷**(→ 2026-10-13: ③ 이 닫혀 남은 것은 ① ② ④ 셋이다. 이 절이 아직 「구현 계약」인 자리): ① **맵이 정한 초기 골드 적용**(소비처 0곳) ② **조각 크기 실측 2건**(NGO RPC 실효 페이로드 상한 · Relay 실효 MTU) ③ ✅ **멀티 최초 경기 준비 실패 표시 UI**(규칙 M-2 — 2026-10-12 에 개정된 사양대로 구현되어 2026-10-13 에 실기로 확인됐다. 관측되지 않은 것의 단일 소스는 `PROJECT_STATUS.md` 의 「✅ 구현 완료 · 실기 검증 (2026-10-13)」 문단. 규칙 M-3·M-4 의 표시 UI 와 countdown 전체 길이 재시작도 구현돼 있다 — 아래 2026-09-28 갱신 블록과 `GameSystemRules_UI.md` 의 해당 규칙이 단일 소스) *(2026-10-13, 낡은 표시를 현재 사실로 고쳤다 — 종전 표시: 「규칙 M-2 — 모달·두 버튼·재시도 요청 경로가 코드에 없다」. 그 앞의 2026-10-12 표시 고침의 종전 표시: 「맵 준비·전송 실패 표시 UI(규칙 M-3·M-4 의 팝업 여부 미정 · countdown 전체 길이 재시작 미구현)」)* ④ **경로 완전 차단 대응**(`GameSystemRules_Units.md` 규칙 45).
> 🔴 **구현은 됐으나 실기에서 한 번도 발화하지 않은 경로**: 전송 실패 복구 · 폴백 템플릿 사용 · timeout/재전송. **「구현했다」와 「실기에서 확인됐다」를 섞어 적지 않는다.** 검증 경계의 단일 소스는 `PROJECT_STATUS.md` 의 「✅ 구현 완료 (2026-09-15)」 문단이다.
>
> **[✅ 2026-09-28 갱신 — 위 「여전히 남은 것 넷」의 항목 ③ 안에 있는 조각 하나가 낡았다. 🔴 이 블록은 덧붙임이다 — 단 항목 ③ 의 상태 표시는 2026-10-12 에 현재 사실로 고쳤고 종전 표시는 그 줄이 이력으로 남긴다(`.claude/MEMORY.md` B-7 · B-5)]**
> 🔴 **항목 ③ 의 *「countdown 전체 길이 재시작 미구현」* 은 더 이상 참이 아니다 — 구현되어 있다.** `Presentation/UI/GameEndUI.cs` 에 **전체 길이로 다시 시작하는 전용 진입점이 있고 호출부도 있다**(메서드 이름 `RestartCountdownFromFullLength()` · 정의 1곳 · 호출 1곳 — 이 문서 작업 중 내가 코드에서 직접 확인. 🔴 **행 번호로 적지 않는다 — 또 바뀐다**). 실기 만료 실측치까지 기록돼 있으며 그쪽의 단일 소스는 **`GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 D-7 의 2026-09-28 실기 검증 블록**이다(전체 길이 재시작 후 **60.2초대 만료**가 여러 회차 관측됐다).
> - ⚠️ **항목 ③ 이 통째로 해소된 것은 아니다** — 이 줄을 쓴 2026-09-28 시점에는 **싱글플레이(규칙 M-4)의 맵 준비 실패 표시 UI 가 미구현**이었고 그것이 진행 중인 작업이었다. **그 작업의 단일 소스는 [Plan.md](_Tasks/2026-09-28/11_50_single-map-failure-ui/Plan.md)** 다. *(2026-10-12, 낡은 표시를 현재 사실로 고쳤다 — 코드 확인: 싱글의 최초 경기·재경기 준비 실패 UI 는 구현돼 있고, 항목 ③ 에 남은 것은 멀티 최초 경기(규칙 M-2)뿐이다. 종전 표시: 「싱글 UI 는 여전히 미구현」. ✅ 2026-10-13: 그 멀티 최초 경기(규칙 M-2)도 구현·실기 확인되어 항목 ③ 에 남은 것은 없다)*
> - 🔴 **그 싱글 UI 에는 카운트다운이 딸려 오지 않는다** — **싱글플레이 결과 화면에는 자동 로비 복귀 타이머가 없다**(2026-09-28 사용자 확정). 🔴 **적용 범위의 단일 소스는 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 D-4 끝의 「2026-09-28 확정 — 적용 범위를 멀티플레이 한정으로」 블록**이며 **여기에 값을 옮겨 적지 않는다.** ✅ **그 확정은 코드에 들어갔다 — 자동 복귀 카운트다운은 네트워크가 켜진 멀티에서만 돈다.** *(2026-10-12, 낡은 표시를 현재 사실로 고쳤다 — 코드 확인. 종전 표시: 「그 확정은 코드 미반영이다 — 현재 코드는 싱글에서도 카운트다운을 돌린다」)*
> - ⚠️ **같은 항목 ③ 의 다른 조각(*「규칙 M-3·M-4 의 팝업 여부 미정」*)과 항목 ①②④ 는 이번에 판정하지 않았다**(CLAUDE.md 규칙 10 — 확인하지 않은 것을 낡았다고 적지 않는다). *(2026-10-12, ③ 의 팝업 여부 조각은 판정해 위 항목 ③ 줄에서 고쳤다 — ①②④ 는 여전히 판정하지 않았다)*

> **2026-09-03 상태 갱신.** 이 절 전체가 「미구현」이던 표기를 바꿨다. **아래 「`HexTile` 런타임 상태 계약」의 1단계(타일 상태 계약 전환)만 구현·실기 검증이 끝났고, 나머지는 여전히 확정 설계·미구현이다.**
> 1단계에서 **`TileKind` · `MineKind` · `MapType` · `DecorationDefinition` · `MapDefinition` · `MapDefinitionCodec` 6개 타입이 생겼지만, 뒤의 4개는 아직 어느 코드에서도 호출되지 않는다**(2·3단계에서 쓰인다). **타입이 있다는 것과 그 계약이 동작한다는 것은 다른 말이므로, 이 절의 맵 생성·검증·전송·해시 대조는 전부 미구현으로 읽어야 한다.**
> 단계 구분 (**2026-09-03 재조정 — 경계를 「싱글플레이 = 2단계 / 멀티플레이 = 3단계」로 다시 그었다**):
> - **1단계** 타일 상태 계약 전환 — **완료**
> - **2단계** 결정적 PRNG 4스트림 · `SymmetricMapBuilder` · 생성기 5종 · `MapDefinitionValidator` · `InitialMapStateEvaluator` · 폴백 템플릿 5개와 제작 도구 · **`MapDefinition` → `HexGrid` 투영** · **`GameConfig` 격자 11×21** · **테스트 모드 설정 필드**(🔴 **2026-09-14 — 이 항목은 삭제 대상이 됐다. 2단계에서 실제로 추가됐던 것은 사실이므로 목록에서 지우지 않고 표시만 한다** — 근거는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 3 의 개정 블록. **[🔴 2026-09-15 표식 갱신 — 위 표식은 그대로 두고 덧붙인다: 「삭제 대상」 → **「삭제 완료」**. 커밋 `8c83317` 로 `GameConfig` 의 두 필드와 `GameConfig.asset` 의 두 행이 실제로 제거됐다. **항목 자체는 계속 지우지 않는다 — 2단계가 그것을 추가한 것은 여전히 참인 이력**이다]**) · **렌더러(막힌 타일 빈 공간·건설 불가 해치)** · **AI 배치 후보 판정 전환** · **건설·점령 전용 조건** · `GridInteractionUseCase` 클릭 판정 순서. 완료 판정은 **「싱글 경기에서 매번 다른 맵이 나오고 정상 플레이된다」**
> - **3단계** `NetworkMapTransfer` 조각 전송 · Host/Client 해시 대조 · 맵 준비 실패 UI · **재경기 맵**(**[🔴 2026-09-14 개정: 종전 표기는 `SameMap`/`NewMap` 재경기였다. 재경기 맵 조건 선택이 없어져 「재경기 맵」 한 갈래가 됐다 — `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 14 의 개정 블록이 단일 소스다. **단계 경계 자체는 바뀌지 않았다**]**)
>
> 종전 분할은 2단계를 「생성기·검증기·폴백」까지로 두어 **완료 판정이 게임 동작과 무관**했다(에디터에서 생성만 확인). 실기로 확인할 것이 없고 「이건 2단계냐 3단계냐」 하는 경계 문제가 쌓여, 규칙 12가 이미 긋고 있는 선(**「싱글플레이는 같은 생성기를 로컬 권위로 실행한다」**)을 따라 다시 잘랐다.

> 범위 경계: 이번 기능에는 canonical binary 형식 식별용 임시 `MapVersion`(`int`, 초기값 `1`)만 둔다. 이는 unknown map format deserialize 차단 전용이며 matchmaking, 앱 업데이트, 전역 connection compatibility 책임이 없다. 전역 `GameProtocolVersion`/build compatibility는 matchmaking same-version filter, custom lobby pre-Relay 검사, NGO connection approval/rejection, reconnect version validation, update-required UX를 포함한 별도 중요 작업이다.

- Host 측 맵 준비 조정자가 64-bit root seed를 만들고, 전용 PRNG의 `MapSelection` 스트림으로 `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 최초 1회 선택한 뒤 최종 맵을 생성·검증한다. 실제 `InitialGold`는 광산 수 표에서 결정하며, 설정 파일의 값이 관여하는 갈래는 없다.
- 최대 100회 재시도와 폴백 선택도 Host에서만 수행한다. 재시도는 지형 세부 형태·중립 광산 위치와 장식 placement 활성화 이후의 장식만 바꾸며 선택값을 다시 뽑지 않는다. 최초 구현의 장식 목록은 항상 비어 있다. **경기 선택값이 그대로 유지되는 구간은 시도 0~99까지이며, 폴백 경로에서는 일부 값이 템플릿 값으로 대체된다** — 무엇이 대체되고 무엇이 유지되는지는 아래 「deterministic fallback 정의」가 단일 소스다.
- 로비 씬에 로딩 화면을 표시한 상태에서 최종 맵 데이터, 64-bit root seed, 맵 유형, 광산 수, 시작 광산 방향, 실제 초기 골드, 최종 맵 해시를 전달한다.
- 로딩 UI는 준비 진행 상태만 표시하고 맵 유형·광산 수·초기 골드·seed·맵 미리보기를 노출하지 않는다.
- Client는 전달받은 맵 정의의 해시를 확인하고 준비 완료를 알린다. 양측 해시가 같을 때만 Host가 전투 씬 전환을 시작한다.
- 준비 실패나 해시 불일치 시 전투 씬으로 이동하지 않고 기존 로비를 유지하며 오류 로그를 남긴다.
- 전투 씬의 `LoadMap()`은 독립 추첨하지 않고 로비에서 확정한 맵 정의를 그대로 구성한다.
- 양측 전투 씬 로드가 완료된 뒤 서버가 시뮬레이션을 시작한다.
- 11×21 맵은 231타일이므로 seed만 전달해 양측이 독립 생성하는 방식보다 최종 타일 상태 전체를 전달하는 안정성을 우선한다.
- 싱글플레이는 동일한 생성기를 로컬 권위로 호출한다. 멀티플레이에서는 Host가 생성·확정한 `MapDefinition`의 실제 초기 골드가 권위값이며 Client의 로컬 설정은 사용하지 않는다.

##### `MapDefinition` 정규 데이터 계약

`MapDefinition`은 생성 후보나 재생성 명령이 아니라, 검증을 통과해 전장을 그대로 구성할 수 있는 최종 완성본이다. 멀티플레이에서는 Host가 이 완성본을 만들고 Client가 동일 데이터를 소비한다.

**상위 필드:**

- `MapVersion`(`int`, 초기값 `1`), 64-bit root seed, 맵 유형
- 너비 11, 높이 21, FlatTop orientation
- 중립 광산 수, 실제 초기 골드
- 최종 해시

> 🔴 **2026-09-14 — 이 목록에서 「테스트 모드 표식(고정폭 0/1)」이 제거됐다 (사용자 확정).** 제거 조항과 근거의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 3 의 개정 블록이다.
> 🔴 **이것은 canonical 바이트 형식의 변경이다** — 고정폭 정수 하나가 상위 필드에서 빠지므로 **그 뒤의 모든 바이트가 앞당겨지고 SHA-256 결과도 전부 달라진다.** 따라서 **`MapVersion` 을 `1` → `2` 로 올리고**, 옛 형식으로 저장된 폴백 템플릿 바이너리 5개를 재생성하며, **옛 형식 기준으로 끝난 전송 실기 검증을 다시 수행해야 한다.**
> ⚠️ **이 개정 시점에는 코드가 아직 옛 형식이다** — 규칙·설계 문서를 먼저 고치는 단계이며, 코드 제거와 버전 상향은 `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md` 의 T2 다. 그래서 아래 「전송 package」 절의 지원 값 표기는 이번에 바꾸지 않았다.

**타일 배열:**

- 231개 타일을 row-major 정규 순서로 저장한다.
- 배열 인덱스가 offset 좌표의 단일 표현이다.

```text
index = row * width + col
col   = index % width
row   = index / width
```

- 타일별 필드: **`TileKind` 3상태 단일 필드** — `Normal`(일반 타일) / `NoBuild`(건설 불가 타일) / `Blocked`(막힌 타일). `InitialOwner`와 장식 상태는 포함하지 않는다.

> 🔴 **2필드(`지형 종류` + `건설 규칙`) 구조는 폐기했다 (2026-08-26).** 막힌 타일의 건설 규칙 값이 미정의여서 **게임 의미가 같은 맵이 서로 다른 canonical bytes·SHA-256** 을 가질 수 있었기 때문이다. 해시는 Host/Client 비교, `NewMap` 재경기의 동일 맵 판정, 폴백 바이너리 유효성의 근거이므로 이 어긋남은 설계 전체의 전제를 흔든다. 3상태 단일 필드는 **모순 조합을 구조적으로 표현 불가능**하게 만든다.
> **[🔴 2026-09-14 정정 — 위 문장은 그대로 두고 덧붙인다: 나열된 세 용도 중 **「`NewMap` 재경기의 동일 맵 판정」은 사라졌다.** 재경기 맵 조건 선택이 없어지면서 「새 후보의 해시가 직전 맵 해시와 같으면 폐기」 조항이 규칙 14 에서 제거됐기 때문이다(사유는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 14 의 개정 블록). 🔴 **나머지 두 용도 — Host/Client 해시 비교 · 폴백 바이너리 유효성 — 는 그대로 살아 있으므로 이 블록의 결론(2필드 구조 폐기 · 3상태 단일 필드)은 바뀌지 않는다.** 즉 **덜어낼 것은 사라진 용도 하나뿐이고 해시 자체의 지위는 그대로다.**]**
> **구현 시 제약**: `TileKind` 는 Domain 계층 타입이므로 `Domain → Core 참조 금지` 제약을 받는다(`.claude/MEMORY.md` 「아키텍처 핵심 제약」).
- orientation, enum, 종류, 변형, 회전은 정수 enum/index로 표현하며 해시 원본에 float를 포함하지 않는다.

**오브젝트 배치:**

- 성: 위치 + 팀
- 시작 광산: 위치 + 팀
- 중립 광산: 위치
- 장식: 위치 + `typeId` + `materialVariantId` + `scaleStepId` + `rotationStepId`로 구성된 `DecorationDefinition` 목록

`DecorationDefinition`의 네 속성은 모두 고정폭 정수 ID다. 실제 material, scale float, rotation float는 해당 ID가 가리키는 표현 계층의 사전 정의이며 canonical bytes에 직접 넣지 않는다. 장식 목록은 canonical codec과 SHA-256 입력에 포함하고 정규 정렬한다.

성·광산의 정체성, 팀, 위치는 이 오브젝트 배치 목록이 유일한 권위 원본이다. 타일 레코드에 `HasCastle`, `HasGoldMine` 같은 중복 정체성 필드를 두지 않는다. 타일 레코드의 `TileKind`는 정적 지형과 건설 규칙만 표현하며 성·광산의 존재를 중복 인코딩하지 않는다. 런타임 이동 가능 여부는 배치 목록에서 투영한 `MineKind`와 동적 `HasBuilding`을 함께 계산한다. 생성기, 검증기, 네트워크 전달, 전투 씬 구성은 모두 같은 `MapDefinition`을 사용하며 별도 위치 목록을 병행 유지하지 않는다.

**초기 소유권 단일 소스:**

`MapDefinition`은 타일별 `InitialOwner`를 저장·전송·해시하지 않는다. Blue/Red 성 위치와 팀 시작 광산 위치, 그리고 아래 공용 초기 영역 확장 규칙이 유일한 입력이다.

```text
각 팀의 초기 소유 타일 =
    Castle 타일 + Castle 인접 6타일
  ∪ Starting MiningPost 타일 + 그 인접 6타일
```

`InitialMapStateEvaluator`는 이 집합과 초기 건물 점유를 계산하는 순수 규칙이다. 런타임 초기 성/채굴소 배치 및 소유권 적용과 semantic fairness validator가 동일 evaluator를 재사용한다. Host와 Client도 canonical definition의 위치에서 같은 결과를 파생한다.

팀별 즉시 일반 건설 가능 타일 검증은 evaluator가 만든 **고유 집합**에서 다음 순서로 수행한다.

1. 성과 초기 채굴소가 점유한 타일을 제외한다.
2. `TileKind == Normal`, `MineKind == None`, `HasBuilding == false`와 기존 소유권 조건을 적용한다.
3. 중복 좌표를 한 번만 세어 Blue/Red 각각 정확히 10개인지 확인한다.

**canonical binary와 SHA-256:**

권위 직렬화는 canonical binary 한 종류만 사용한다.

1. 스키마에 정의된 필드 순서를 고정한다.
2. 모든 수치는 스키마가 지정한 고정폭 정수로 기록한다.
3. 모든 다중 byte 정수는 **little-endian**으로 기록하고 플랫폼 native byte order를 사용하지 않는다.
4. 타일 231개는 row-major 순서로 기록한다.
5. 성·광산·장식 레코드는 row-major tile index를 첫 키로 정규 정렬한다. 동률 시 성·광산은 종류·팀 ID를, 장식은 `typeId`→`materialVariantId`→`scaleStepId`→`rotationStepId`를 스키마 순서의 후속 키로 사용한다.
6. string, float, 최종 hash 필드 자체는 canonical bytes에서 제외한다.
7. hash 필드를 제외한 canonical bytes 전체에 SHA-256을 계산해 32-byte digest를 만든다.

시각 스케일·재질 변형도 float 값을 직렬화하지 않고 정수 variant ID만 기록한다. JSON 표현은 진단 로그 전용이며 권위 전송, canonical bytes 생성, SHA-256 입력에 사용하지 않는다.

**전송 package:**

```text
mapVersion
canonicalLength
canonicalBytes
sha256Digest[32]
```

package header의 `mapVersion`은 preflight decoder 선택용이며 canonical `MapDefinition.MapVersion`과 일치해야 한다. 현재 지원 값은 `2`이다. 🔴 **2026-09-14 — 이 줄은 「초기값」이 아니라 「현재 값」을 적고 있으므로 형식이 바뀌면 거짓이 된다. T1 이 남긴 예고대로 T2(테스트 모드 표식이 canonical 바이트열에서 빠지는 커밋 — `_Tasks/2026-09-14/12_17_rematch-map-selection/Plan.md`)에서 `1` → `2` 로 함께 고쳤다. 실측 근거: `Domain/Map/MapDefinition.cs` `CurrentMapVersion = 2`.** 값이 다르거나 미지원이면 canonical bytes를 deserialize하지 않고 map preparation을 실패 처리한다. 이 값은 앱/접속 호환성 판정에 사용하지 않는다. SHA-256의 입력은 canonical bytes만이다.

**`NetworkMapTransfer` 전송 프로토콜:**

최초 경기와 재경기 맵 준비는 persistent network connection에 존재하는 공용 `NetworkMapTransfer`를 사용한다. 전송 수명주기를 Lobby/Game 씬 객체에 귀속시키지 않으며, scene-bound 단일 RPC 호출 하나에 전체 canonical payload를 실어 보내는 방식은 금지한다.

```text
MapPrepareBegin(matchNonce, mapVersion, totalBytes, chunkCount, SHA256)
MapChunk(matchNonce, index, count, data)
MapReady(matchNonce, success, clientSHA, errorCode)
```

- canonical payload의 `totalBytes`는 최대 64KB다. ⚠️ **근거 미확인 — 구현 시 NGO 실측으로 확정한다.**
- 각 `MapChunk.data`는 최대 1KB이며 `index`와 `count`로 전체 위치를 식별한다. ⚠️ **근거 미확인 — 구현 시 NGO 실측으로 확정한다.**

> 🔴 **위 두 수치(1KB · 64KB)는 왜 그 값인지의 근거가 문서 어디에도 없다 (2026-08-26 확인).** 게다가 canonical payload 실측이 **약 274바이트**(타일 231 + 헤더 19 + 성·광산 24 + 장식 0)라 **조각이 1개뿐이어서 쪼개기·재조립·중복 무시 로직이 실전에서 한 번도 실행되지 않는다.** 2개째 조각이 생기려면 장식이 150개 이상 있어야 하는데 최초 구현의 장식은 0개다.
>
> **그럼에도 값을 지금 바꾸지 않는다.** 조각 크기는 **네트워크가 한 번에 실을 수 있는 한도**에서 나와야 하는 값이므로, 실측 없이 고치면 근거 없는 숫자를 근거 없는 다른 숫자로 바꾸는 것이 된다. 코드를 억지로 실행시키려고 조각 크기를 줄이는 방안도 폐기했다.
> **할 일**: 전송 경로 구현 후 NGO 실측으로 조각 크기·전체 한도를 확정하고 **그 근거를 이 절에 기록**한다. 규칙 문서(`GameSystemRules_RandomMap.md` 6장)에는 이 숫자를 두지 않는다 — 그쪽은 기획 약속만 담는다.
> 실측 근거: `_Tasks/2026-08-26/04_35_map-rules-correction/Plan.md` D7-4 6장. **파이썬 계산이며 Unity·NGO 실기 측정이 아니다.**
- Client는 같은 `matchNonce`와 `index`의 중복 chunk를 무시한다.
- out-of-order chunk는 수신 순서가 아니라 `index` 순서로 재조립한다.
- 활성 준비 작업의 nonce와 다른 stale begin/chunk/ready 메시지는 상태를 바꾸지 않고 무시한다.
- 선언된 `chunkCount`가 모두 모이고 재조립 길이가 `totalBytes`와 일치한 뒤에만 SHA-256→deserialize→semantic fairness validation을 수행한다.
- 부분 조립 데이터로 hash 검증, deserialize, 맵 구성 또는 준비 ACK를 수행하지 않는다.
- Client는 성공 시 계산한 `clientSHA`와 함께 `MapReady(success=true)`를 보내고, 실패 시 `MapReady(success=false, errorCode)`를 보낸다.
- Host는 활성 nonce의 `success=true`와 SHA 일치를 확인하기 전에 scene transition을 시작하지 않는다.

**timeout/retry state machine:**

- Begin/Chunks는 reliable delivery를 사용한다.
- Host는 package 전송 완료 후 활성 nonce의 `MapReady`를 10초 기다린다.
- timeout 또는 incomplete assembly만 같은 nonce와 같은 canonical package 전체를 1회 재전송한다. 재전송 뒤 두 번째 10초 timeout/incomplete면 terminal failure다.
- unsupported `MapVersion`, `totalBytes`가 한도 초과(현재 값 64KB — ⚠️ 근거 미확인, 위 전송 프로토콜 절 참조), SHA mismatch, deserialize failure, semantic invalid, disconnect는 즉시 terminal failure다. 동일 package resend를 수행하지 않는다.
- terminal failure 경로는 모두 scene transition gate를 닫은 상태로 유지한다.

**복구 상태:**

- 최초 멀티 준비 실패: loading hide 후 **양쪽(Host·Client)이 곧바로 방을 나가고**(연결 종료 · 화면은 로비 메인) 각자 알림 모달을 띄운다. 모달은 「확인」 하나이며 모달만 닫는다. **재시도·재시작 요청은 없다** — 다시 하려면 방을 새로 만든다. 문구·제목·버튼과 양쪽 동일 화면의 원칙은 **`GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-2 가 단일 소스**이며 이 문서에 옮겨 적지 않는다. 구현 구조(호스트의 실패 통보 → 접속한 쪽의 한 진입점, 통보가 못 간 경우를 받는 연결 끊김 경로)와 실기 결과는 `_Tasks/2026-10-12/07_08_multi-first-map-failure-ui/Plan.md` 5절·14절이 단일 소스다. *(2026-10-12 사용자 결정으로 사양이 바뀌었다 — 종전: 「loading hide, lobby와 connection 유지, generic `맵 준비에 실패했습니다` modal, 「다시하기」/「로비로」 actions. 양 player의 Retry request를 Host가 idempotency key로 한 번만 수락하고 새 64-bit root seed부터 selection/generation/transfer를 재시작한다」. 그 앞의 같은 날 라벨 변경의 종전 표기는 `Retry`/`Leave Match` 였다. 2026-10-13 구현·실기 확인)*
- 재경기 맵 준비 실패: pending candidate 폐기, old current definition 유지, rematch pending state reset, result UI 복원, auto-return countdown을 full duration으로 reset, **결과 화면의 기존 actions 재활성화**.
- single 최초 실패: modal, 「다시하기」/「로비로」 *(2026-10-12 사용자 결정 — 종전 Retry/Lobby. 바로 아래 줄이 인용한 종전 표기와 「확정된 버튼 라벨이 없다」는 판정은 이 변경 이전 시점의 관측이다)*. single 재경기 실패: 기존 rematch result choices 복원.
- ✅ **위 복구 상태 규정에 달렸던 「정해지지 않은 항목」 표시는 닫혔다** — 규칙 M-3 의 팝업 여부 미정은 상태 줄로 개정돼 닫혔고, 규칙 M-4 의 표시 문구 · loading UI 처리 · 버튼 라벨 · 팝업 여부 미정도 닫혔다. 확정값과 구현·실기 확인 상태의 단일 소스는 `GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-3·M-4 와 그 확정 블록들이며, 이 문서에는 옮겨 적지 않는다. 🔴 **다만 이 절의 위 `single` 두 항목은 그 확정값을 담고 있지 않다(직접 읽어 확인)** — `single 최초 실패: modal, Retry/Lobby` 에는 표시 문구 · loading UI 를 닫는다는 것 · 확정된 버튼 라벨이 없고, `single 재경기 실패: 기존 rematch result choices 복원` 에는 상태 줄로 알린다는 것과 싱글에는 자동 복귀 타이머가 없다는 것이 없다. **비어 있는 채로 두었고 채우지 않았다**(사양 서술 보강은 이번 범위 밖). 또 첫 항목(최초 멀티 준비 실패)은 규칙 M-2 가 정한 규정이며 **2026-10-12 에 개정된 사양대로 구현되어 2026-10-13 에 실기로 확인됐다**(위 첫 항목이 현행 사양이다). *(2026-10-13, 낡은 표시를 현재 사실로 고쳤다 — 종전 표시: 「첫 항목(최초 멀티 준비 실패의 modal · `Retry`/`Leave Match`)은 규칙 M-2 가 정한 규정이며 코드에 구현이 없다(모달 · 두 버튼 · 재시도 요청 경로 0건 확인)」. 그 앞의 2026-10-12 표시 고침은 낡은 미정 표시를 현재 사실로 고친 것이었다 — 사용자 지시)*
- UI payload에는 internal error code, seed, MapType을 포함하지 않는다. 상세 값은 진단 로그 전용이다.

**Client 검증 순서:**

1. package 전체 길이, canonicalLength, 지원 `MapVersion`을 확인한다.
2. deserialize 전에 canonical bytes의 SHA-256을 재계산해 전달된 digest와 비교한다.
3. hash가 맞을 때만 canonical bytes를 `MapDefinition`으로 deserialize한다.
4. 231개 타일, 버전 일치, 좌표/목록 정합, 180도 대칭, 도달성, 광산·시작 공간 등 semantic fairness validator를 실행한다.
5. 모든 단계가 성공한 경우에만 `MapReady(success=true, clientSHA)` 준비 ACK를 Host에 전송한다.

Client가 응답 가능한 상태에서 길이/버전/hash/deserialize/semantic 검증이 실패하면 `MapReady(success=false, errorCode)`를 보내고 로비를 유지하며 실패 단계를 로그로 남긴다. timeout·불완전 수신·disconnect는 위 state machine이 처리한다.

##### 생성·검증 실행 모델

초기 구현은 맵 generation과 validation을 main thread synchronous 작업으로 둔다.

1. 로비에서 loading UI를 표시한다.
2. 1 frame yield해 loading UI가 실제 렌더되도록 한다.
3. Host main thread에서 pure C# generator와 validator를 실행한다.
4. 생성 시간과 최종 attempt count를 로그에 기록한다.
5. 검증 성공 뒤 공용 `NetworkMapTransfer` 전송을 시작한다.
6. 전투 씬에서는 수신·검증을 마친 `MapDefinition`의 Unity object/render 적용을 main thread에서 수행한다.

generator와 validator의 도메인 코드는 `UnityEngine` object, scene object, renderer와 그 밖의 Unity API에 의존하지 않는 pure C# 경계로 유지한다. 이 경계는 향후 실행 위치를 바꿀 수 있게 하기 위한 것이며, 최초 구현에 `Task`, worker thread, cancellation token과 관련 수명주기 조정을 선행 도입한다는 뜻이 아니다.

실제 대상 기기 profiling에서 생성·검증 구간의 사용자 체감 hitch가 확인된 경우에만 pure C# 구간을 background로 이전한다. profiling 근거 없이 비동기화하지 않으며, 이전하더라도 Unity object/render 적용은 계속 Game scene main thread에서 수행한다.

##### 결정적 PRNG 및 독립 스트림 계약

맵 생성은 프로젝트 전용 고정 정수 PRNG만 사용한다. PRNG 알고리즘, 정수 폭, 오버플로 규칙, seed 파생 순서를 프로젝트 코드와 테스트 벡터로 고정한다. `System.Random`, `UnityEngine.Random`, 런타임별 문자열 해시, 기본 `GetHashCode()`는 사용하지 않는다.

입력 seed는 64-bit root seed다. `MapVersion`과 root seed를 안정 해시 또는 동일한 고정 정수 파생 함수에 넣어 다음 도메인 seed를 만든다.

| 스트림 | 책임 |
|--------|------|
| `MapSelection` | `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 재시도 전에 1회 선택 |
| `Terrain` | 완전 차단 지형과 건설 불가 구역 |
| `MinePlacement` | 시작 광산과 중립 광산 배치 |
| `Decoration` | 장식 종류, 변형, 회전 |

스트림 이름은 런타임 문자열 해시로 변환하지 않고 스키마에 고정된 정수 stream ID를 사용한다.

최초 구현의 모든 archetype generator와 fallback은 `DecorationDefinition` 빈 목록을 반환하며 `Decoration` PRNG draw를 소비하지 않는다. 그러나 `MapDefinition`, canonical codec/hash, `SymmetricMapBuilder`, `MapDefinitionValidator`는 처음부터 `typeId`, `materialVariantId`, `scaleStepId`, `rotationStepId` 스키마를 처리한다. builder는 회전 대응 위치에 장식 레코드와 180도 대응 `rotationStepId`를 함께 기록하고, validator는 최종 목록의 exact 180° 대응을 독립 검증한다.

장식 에셋과 맵 테마가 확정된 뒤에만 독립 `Decoration` 스트림을 사용해 placement를 활성화한다. 이 후속 추가는 `Decoration` 스트림 내부에서만 draw를 소비하며 같은 `MapVersion`·root seed·attempt의 `Terrain`과 `MinePlacement` 결과를 바꾸지 않는다.

재시도마다 각 도메인 seed와 attempt index를 다시 파생해 `Attempt-0`~`Attempt-99`의 독립 PRNG 상태를 만든다. 즉 하나의 긴 PRNG 상태를 모든 서브시스템과 attempt가 공유하지 않는다.

`InitialGold`는 PRNG 입력이 아니라 `NeutralMineCount`의 순수 lookup 결과다. 선택된 `MapType`, `NeutralMineCount`, `StartingMineSide`와 실제 `InitialGold`는 **attempt 0~99 구간에서 불변인 경기 선택값**이다. **폴백 경로는 이 구간 밖이며, 그 경로에서 어떤 값이 템플릿 값으로 대체되고 어떤 값이 유지되는지는 아래 「deterministic fallback 정의」가 단일 소스다.** attempt는 `Terrain`, 중립 `MinePlacement`, `Decoration`의 세부 결과만 바꿀 수 있다. 검증 실패는 해당 attempt만 폐기하며 `MapSelection`을 다시 실행하지 않는다. 이 경계로 유형·광산 수·A/B별 후보 실패율이 최초 선택 확률을 편향시키는 것을 금지한다.

```text
domainSeed  = Derive(mapVersion, rootSeed, fixedDomainId)
attemptSeed = Derive(domainSeed, attemptIndex)
```

이 구조가 보장해야 하는 성질:

- Decoration의 draw 수나 호출 순서 변경이 Terrain/MinePlacement 결과를 바꾸지 않는다.
- Terrain과 MinePlacement도 서로의 draw 수에 결합되지 않는다.
- Attempt-N의 draw 수가 Attempt-(N+1)의 시작 상태를 바꾸지 않는다.
- 동일 `MapVersion` + root seed는 동일한 맵 선택, 후보 순서, 검증/폴백 결정과 최종 결과를 재현한다.

결정적 재생성은 진단과 테스트를 위한 계약이다. 멀티플레이 경기의 실제 권위는 Host가 생성·검증해 전달한 최종 `MapDefinition`과 hash다. Client는 root seed로 독립 생성한 결과를 권위 데이터로 사용하지 않고 수신 완성본의 hash만 검증한다.

##### `SymmetricMapBuilder` 생성 경계

11×21 archetype generator가 타일 배열을 직접 수정하지 않도록 `SymmetricMapBuilder`를 유일한 후보 생성 변경 경계로 둔다.

```text
SetPair(col, row, state)
    original = (col, row)
    rotated  = 참 180도 회전(col, row)
                 col → 10 - col
                 row → 21 - row      (col 이 짝수일 때)
                 row → 20 - row      (col 이 홀수일 때)
    두 위치에 state와 RotateState(state)를 원자적으로 기록

SetCenter(state)
    center = (5, 10)
    자기대응 가능한 state를 중심에 기록
```

> 🔴 **종전의 `rotated = (10 - col, 20 - row)`(행열 반전)는 폐기됐다 (2026-08-26).** 그것은 회전이 아니다 — FlatTop 은 홀수 열이 반 칸 아래로 시프트되어 있어 두 축 번호를 뒤집으면 **헥스 인접성이 보존되지 않는다.** 큐브 좌표로는 `p → 2C - p`(`C` = offset `(5,10)`)이며, 규칙 정의의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 1이다.
>
> **`SetPair`는 짝 없는 6칸 `(0,0) (2,0) (4,0) (6,0) (8,0) (10,0)`(짝수 열 0행)을 입력으로 받지 않는다.** 그 좌표의 회전 결과는 21행이라 맵 밖이다. 6칸은 생성이 아니라 **고정값(항상 막힌 타일)** 으로 기록한다.

계약:

- `SetPair`는 중심 `(5,10)` 입력을 거부하고 중심은 `SetCenter`로만 기록한다.
- `SetCenter`는 중심 외 좌표를 받지 않는다.
- `TileKind`, 광산, 장식 등 모든 정적 생성 상태는 builder API를 통과한다.
- archetype generator에는 raw mutable tile buffer와 회전 상대 타일 직접 수정 API를 노출하지 않는다.
- `RotateState(state)`는 팀 전용 상태의 Blue↔Red 대응과 장식 rotation의 180도 대응 정수 ID 변환을 포함한다.
- **좌표를 옮기는 연산과 상태를 바꾸는 연산은 이름을 나눠 쓴다** — 좌표는 `RotateCoord(p)`(계산식의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 1), 상태는 `RotateState(state)`(위 항목)다. 2026-08-31 이전에는 **둘 다 같은 이름**이어서 컴파일러는 입력 타입으로 구분해도 읽는 사람은 구분할 수 없었다. 같은 이름으로 되돌리지 않는다.
- 장식 종류·variant·scale/material 대응은 같은 상태 변환 계약 안에서 유지한다.

builder는 실수로 한쪽만 기록하는 구현을 구조적으로 막는 도구다. 보안·완료 판정 경계로 신뢰하지는 않는다. builder가 완성한 `MapDefinition` 후보를 독립 `MapDefinitionValidator`에 넘겨 타일 `TileKind`, 광산, 장식의 exact 180° symmetry를 다시 검사한다. validator는 builder 내부 상태나 “대칭으로 생성되었다”는 플래그를 신뢰하지 않고 최종 정의만 읽는다.

##### archetype generator 알고리즘

> 🔴 **이 절의 대역 수치는 2026-08-26에 참 180도 회전 기준으로 전면 재작성됐다.** 종전 수치는 행열 반전 전제 위에 적혀 있어 회전 대칭이 성립하지 않았다.
> - **유형별 값의 단일 소스는 `GameSystemRules_RandomMap.md` 3장(규칙 4~8)** 이다. 이 절은 그 사양을 구현하는 생성기 알고리즘을 적으며, **값이 바뀌면 규칙 문서를 먼저 고치고 이 절을 맞춘다.**
> - 「높이 단계」 = `행 × 2 + (홀수 열이면 1)`, 범위 1~41, 회전은 `L ↔ 42-L`. 정의는 규칙 문서 7장 용어 정의.
> - ⚠️ **아래 수치의 공정성 근거는 파이썬 시뮬레이션이며 Unity 실기 검증이 아니다**(`_Tasks/2026-08-26/04_35_map-rules-correction/Research.md` 3-0). **구현 후 C# 검증기로 같은 항목을 재측정하기 전까지 「검증 완료」로 표기하지 않는다.**

**`ObstacleOpenGenerator`:** **윗절반 3~9행**은 행별 count 0~4를 각각 20%로 독립 선택하고 count개의 unique column을 균등 선택한다. 0~2행과 그 회전 상대에는 장애물을 두지 않는다. 아랫절반은 별도 추첨 없이 참 180도 회전 투영이며, 결과 대역은 **11~18행**이다(짝수 열 `21-row` · 홀수 열 `20-row` 차이 때문에 상단 대역과 행 번호가 겹치지 않는다). 중앙선(높이 단계 21 = 홀수 열 10행)은 count 0/2/4를 각각 1/3로 선택해 중앙선 내부 rotation pair `(1,10)↔(9,10)` · `(3,10)↔(7,10)` 로 배치하고 고정점 `(5,10)`에는 두지 않는다. 장애물 pair 0개는 attempt reject다. 기대값은 약 30개다. reachability 실패 시 count 감소·이동·repair 없이 attempt 전체를 버린다.

**`CanyonGenerator`:** `W`는 3/5/7 균등 선택이다. 폭은 **열 구간**으로 해석한다(폭 `w` = col `5-(w-1)/2` ~ `5+(w-1)/2`). **윗절반만 생성하고 아랫절반은 참 180도 회전으로 복제한다.** 0~2행은 width 11이다. rows 3~8 중 `(11-W)/2`개 distinct transition row를 균등 선택하고 해당 row를 지날 때 centered contiguous odd width를 2씩 감소시킨다. **3행의 시작 폭은 11**이고 8행에서 `W`에 도달하며 profile은 단조, adjacent decrement≤2다. **높이 단계 18~24**(짝수 열 9~12행 · 홀수 열 9~11행, 두께 3타일 39칸)는 width `W` 로 고정하고 그 열린 타일을 `TileKind.NoBuild`로 둔다. 회전 투영 결과 아래쪽 좁아지는 구간은 **짝수 열 13~18행 · 홀수 열 12~17행**, 아래쪽 width 11 대역은 **짝수 열 19~20행 · 홀수 열 18~20행**이다. 중심 폭 밖은 `TileKind.Blocked`다.

**`OuterGenerator`:** length 5/7/9/11, maxWidth 3/5, 형태(마름모형/타원형/울퉁불퉁형)를 각각 균등 선택한다. 덩어리 대역은 **높이 단계 `21±L`** 이며 실제 행 범위는 `L=5 → 짝수 8~13·홀수 8~12`, `L=7 → 7~14·7~13`, `L=9 → 6~15·6~14`, `L=11 → 5~16·5~15`다. 포함된 각 높이 단계는 col 5 중심의 단일 contiguous odd-width `TileKind.Blocked` 구간이고 **중앙선(단계 21)은 maxWidth**다. upper profile을 참 180도 회전 투영하며 adjacent width delta≤2, connected, no holes를 보장한다. 마름모형은 `1→3→5` monotonic ramp, 타원형은 tapered ends와 max-width plateau, 울퉁불퉁형은 공통 제약 내 임의 odd-width profile이다. **높이 단계 18~24**에서 덩어리 바깥의 열린 외곽 통로는 `TileKind.NoBuild`이며 validator가 left/right width≥3과 connectivity를 검사한다.

**`ThreeLaneGenerator`:** L 5/7/9/11에 대한 분리 대역은 **높이 단계 `21±L`** 이며 실제 행 범위는 `OuterGenerator`와 같은 표를 따른다(`L=5 → 짝수 8~13·홀수 8~12` … `L=11 → 5~16·5~15`). 대역 안은 cols 0~2/4~6/8~10을 `TileKind.NoBuild`, cols 3/7을 `TileKind.Blocked`로 고정한다. 열 구조는 회전(`col → 10-col`)에 그대로 대응하므로 손대지 않는다. 대역 밖은 모두 `TileKind.Normal`이고 즉시 merge하며 transition obstacle이나 맵을 세로로 관통하는 상시 건설 불가 띠는 없다. 대역 내부 paired mine은 mirrored left/right lane에 lane당 최대 1개, middle lane은 odd count의 `(5,10)` singleton만 허용한다. 나머지 pair는 합쳐진 위·아래 구역에 둔다.

##### 중립 광산 canonical orbit sampling

candidate builder는 성, 시작 광산, `InitialMapStateEvaluator`가 산출한 팀별 보호 초기 건설 타일 10개, `TileKind.Blocked`, archetype 금지 zone을 제거한다. 남은 좌표 `p`에서 `(p, RotateCoord(p))`를 만들고 두 row-major index 중 작은 쪽을 canonical representative로 삼아 reversed duplicate를 제거한다. `MinePlacement` stream으로 필요한 distinct pair slot을 center/edge weight 없이 균등 선택한다. odd count의 singleton은 고정 `(5,10)`이다.

- Open/Obstacle: 공통 제외 뒤 모든 pair 허용
- Canyon: **높이 단계 18~24 밖**에만 pair를 둔다(odd count의 `(5,10)` singleton은 예외)
- Outer: **높이 단계 18~24** 안에는 최대 1 pair, 나머지는 위·아래 구역
- ThreeLane: 분리 대역(높이 단계 `21±L`) 내부 pair는 mirrored left/right lane이고 lane당 mine 최대 1개, 나머지는 합쳐진 위·아래 구역

> 유형별 금지 구역의 **단일 소스는 `GameSystemRules_RandomMap.md` 3장(규칙 4~8)의 ④ 항목**이다. 위 목록은 sampler 구현이 참조하는 요약이며, 값이 어긋나면 규칙 문서가 옳다.
- spacing/adjacency filter 없음
- local constraint 위반은 후보별 동일 확률을 보존하는 rejection sampling으로 다시 선택
- global validator 실패는 chosen mine 이동·repair 없이 attempt reject

##### `MapDefinitionValidator` access metric

공정성 거리는 기존 A*에 임의의 mine 인접 타일 하나를 지정해 구하지 않는다. 각 castle에 인접한 `StaticTraversable` 타일 전체를 거리 0의 **성 접근 칸**으로 두고, **대상 광산 덩어리**에 인접한 `StaticTraversable` 타일 전체를 **광산 덩어리 접근 칸**으로 하는 multi-source BFS에서 최초 도달 거리를 사용한다. **재는 단위는 광산 하나가 아니라 광산 덩어리다**(아래 항목). 용어의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 13 「접근 거리」 절이다.

```text
StaticTraversable = TileKind != Blocked
                 && MineKind == None
                 && !Castle
                 && !StartingPost
```

> **광산 덩어리 접근 칸은 광산 하나가 아니라 「광산 덩어리」 단위로 만든다.** 서로 인접한 광산들을 한 덩어리로 묶고, 그 덩어리에 속한 **어느 광산에라도 인접한 StaticTraversable 타일 전체**를 그 덩어리의 접근 칸으로 쓴다. **접근 칸이 하나도 없는 덩어리가 하나라도 있으면 해당 후보를 거부한다.** 아래의 도달 가능성과 cross equality 도 이 덩어리 접근 칸으로 판정하며, 광산이 하나뿐인 덩어리는 결과가 종전과 같다. 판정 규정과 그 근거의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 13이다.

`TileKind.Normal`과 `TileKind.NoBuild`는 모두 traversable이고 runtime general building은 이 정적 metric에서 제외한다. 모든 castle이 모든 neutral 광산 덩어리 접근 칸에 도달해야 하고 Blue/Red 성 접근 칸끼리도 상호 도달해야 한다. center C는 `Access(B,C)==Access(R,C)`, pair A/RotateCoord(A)는 `Access(B,A)==Access(R,RotateCoord(A))`와 `Access(R,A)==Access(B,RotateCoord(A))`를 모두 만족해야 한다. 같은 cross equality를 geometric `HexCoord` 거리로 독립 검사한다.

##### 필수 통로 validator

> 이 절의 이름은 2026-08-26에 **필수 통로 validator** 로 바뀌었다. 종전 이름은 같은 단어가 「두지 않기로 한 것」과 「반드시 검증할 것」을 동시에 가리켜 폐기했다 — 폐기된 표현은 `GameSystemRules_RandomMap.md` 7장 용어 정의 「필수 통로」의 `_Avoid_` 행에만 남겨 둔다.

- mine placement 전 모든 필수 통로의 같은 높이 단계 cross-section: Canyon 중앙 열린 폭≥3, Outer left/right 각각≥3, ThreeLane 각 lane 정확히 3. 해당 열린 타일은 모두 `TileKind.NoBuild`다.
- mine placement 후 같은 통로·같은 높이 단계: mine 0이면 walkable≥3, mine 1이면 walkable≥2, mine≥2는 invalid다.
- 통로별 정의된 start set→end set BFS로 연속성과 unintended disconnect 부재를 확인한다.
- exact symmetry+위 국소 폭+BFS만 검사하며 alternate route 전체를 열거하지 않는다.

##### deterministic fallback 정의

> 🔴 **2026-08-26 전면 교체.** 종전 구조(유형 × 광산 수 × A/B = 50케이스, 시작 광산 좌우 변환)는 폐기했다. 폐기 근거는 두 가지다 — ① **좌우 변환 `(col,row) → (10-col,row)` 은 보호 대상 10타일 집합을 바꿔 검증을 깨뜨린다** ② 폴백은 실전에서 거의 도달하지 않는 경로(attempt 1회 성공률 최저 99.6%)라 50케이스의 제작·검증·유지 비용이 정당화되지 않는다. 기획 계약의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 12다.
>
> **아래 「구성 요소」의 유형별 최대 광산 수와 초기 골드 값은 구현자가 곧바로 쓸 수 있도록 여기에 함께 적는다. 그러나 값의 단일 소스는 규칙 문서이며, 값이 바뀌면 규칙 문서를 먼저 고치고 이 절을 맞춘다**(「archetype generator 알고리즘」 절과 같은 방식이다).

**구성 요소:**

- 맵 유형별 deterministic 고정 템플릿 **1개씩, 총 5개**. 유형×광산 수×A/B 조합은 만들지 않는다.
- 각 템플릿의 중립 광산 수는 **그 유형이 허용하는 최대값**(완전개방 6 / 장애물 6 / 협곡 4 / 외곽 6 / 3갈래 6)으로 고정하고, `InitialGold`는 그 광산 수에서 파생한다(6→200, 4→400).
- **시작 광산 A/B는 템플릿 안에 그대로 고정해 담는다.** `MirrorStartMine` 같은 좌우 대응 변환은 **사용하지 않는다.**
- 템플릿 데이터는 **윗절반(높이 단계 1~20)과 중앙선(단계 21)만 지정**하고, 아랫절반은 `SymmetricMapBuilder`의 참 180도 회전으로 복제한다.
- 최초 구현은 장식 없음(`DecorationDefinition` count 0). 에셋·테마 확정 뒤에는 유형별 고정 exact 180° symmetric decoration set만 허용

fallback builder는 맵 유형을 키로 위 정의를 조립하며 PRNG draw를 전혀 사용하지 않는다. 같은 유형은 항상 같은 지형과 중립 광산 결과를 만든다. **fallback builder의 입력은 `MapType` 하나뿐이다** — 경기 선택 단계의 `StartingMineSide`·`NeutralMineCount`·`InitialGold`는 이 경로에서 입력으로 쓰이지 않고 **템플릿 값으로 대체된다.** 대체된 값은 canonical `MapDefinition`에 그대로 실려 Client에도 전달되고, 규칙 12의 로그 필수 항목(중립 광산 수·시작 광산 방향·실제 초기 골드·폴백 사용 여부)으로 추적된다. **대체되지 않는 것은 `MapType` 하나**이며, 기획 계약의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 12다. 🔴 **2026-09-14 — 이 문장이 종전에 「대체 대상이 아니다」로 함께 묶고 있던 「테스트 모드 표식과 그때의 `InitialGold` 규정」은 제거됐다**(근거의 단일 소스는 같은 문서 규칙 3 의 개정 블록).

조립 결과는 일반 생성 결과와 동일한 `MapDefinition`이며, **전용 완화 규칙 없이 전체 `MapDefinitionValidator`를 통과해야 한다.** 검증 성공 후에만 canonical binary와 SHA-256 package를 만든다. 폴백이 검증에 실패하면 조용히 사용하지 않고 맵 준비 실패로 처리한다. 빌드·에디터 테스트에서 템플릿 5개를 상시 전수 검증한다.

**저장 포맷은 canonical binary이며, 그 선택에는 부수 조건 2가지가 따라붙는다.**

1. **템플릿 제작 에디터 도구를 삭제하지 않는다.** 포맷이 바뀌면 템플릿을 재생성해야 하는데 도구가 없으면 재생성할 수 없다. 도구는 영구 보존 폴더 `Assets/Editor/Tools/` 에 둔다(`WORKFLOW.md` [5-2]).
2. **재생성용 원본(윗절반 지정값 또는 seed)을 별도 보관한다.** 바이너리만 남으면 포맷 변경 시 손으로 다시 만들어야 한다.

base template은 코드 상수 또는 schema 비종속 순수 정의로도 유지해, schema 변경 시 옛 canonical binary에 발이 묶이지 않게 한다.

##### `HexTile` 런타임 상태 계약

`MapDefinition`을 `HexGrid`로 구성할 때 `HexTile` 상태를 다음 세 축으로 분리한다.

| 상태 | 값 | 변경 주체 |
|------|----|-----------|
| `TileKind` | `Normal`(일반), `NoBuild`(건설 불가), `Blocked`(막힘) | 맵 정의. 경기 중 불변 |
| `MineKind` | `None`, `Neutral`, `BlueStart`, `RedStart` | 광산 배치 목록에서 로드 시 투영. 경기 중 불변 |
| `HasBuilding` | bool | 건물 배치·철거·파괴에 따른 동적 상태 |

> 종전에는 지형 축과 건설 규칙 축이 **별도 필드 2개**였으나, 「막혔는데 건설 허용」 같은 **뜻이 없는 조합**이 표현 가능해 의미가 같은 맵이 서로 다른 canonical bytes·해시를 가질 수 있었다. 2026-08-26에 `TileKind` 3상태 단일 필드로 통합했다(위 「`MapDefinition` 정규 데이터 계약」 참조).

`MapDefinition`의 성/광산 배치 목록은 직렬화·해시의 단일 원본이다. `HexTile.MineKind`는 그 목록에서 생성한 런타임 투영이며 별도 네트워크/직렬화 원본으로 병행 유지하지 않는다.

`IsWalkable`은 mutable 저장 필드가 아니라 다음 계산 프로퍼티다.

```text
IsWalkable = TileKind != Blocked
          && MineKind == None
          && !HasBuilding
```

판정 규칙:

```text
일반 건설 = TileKind == Normal
         && MineKind == None
         && !HasBuilding
         && 기존 소유권 조건

MiningPost = TileKind != Blocked
          && MineKind != None
          && !HasBuilding
          && 기존 인접 팀 타일 조건

점령 가능 = TileKind != Blocked
```

`TileKind.Blocked`는 이동·일반/채굴소 건설·점령이 모두 불가능한 영구 지형이다. `HasBuilding`과 독립적이므로 건물 철거 또는 파괴가 막힌 타일을 열지 않는다. `TileKind.NoBuild`는 이동·점령은 허용하고 일반 건설만 막는다(MiningPost는 예외).

**`HexGridRenderer`와 입력 표현:**

- `TileKind.NoBuild` 좌표는 일반 타일과 같은 standard hex mesh와 높이를 생성한다. owner base color도 Neutral/Blue/Red 상태를 그대로 사용한다.
- 타일 표면에는 별도 표현 계층으로 반투명 짙은 회색 diagonal hatch 3개를 overlay한다. selection highlight와 `TileKind.NoBuild` overlay를 독립 상태로 유지해 둘을 동시에 렌더링한다.
- `TileKind.Blocked`도 domain `HexGrid`와 `MapDefinition`의 231개 row-major 레코드에는 남는다. **canonical codec/hash에서 제외하지 않는다.** `MapDefinitionValidator`의 exact 180° 비교에서도 제외하지 않되, **짝 없는 6칸만 아래 예외를 따른다.**

**`TileKind.Blocked` 좌표 계약 — 짝 없는 6칸 예외 (2026-08-26 추가):**

- **① 어느 6칸인가**: `(0,0) (2,0) (4,0) (6,0) (8,0) (10,0)` — **전부 짝수 열 0행**이다. 참 180도 회전은 짝수 열에서 `row → 21 - row` 이므로 0행의 상대는 21행이 되어 맵 밖이다. 즉 **회전 상대가 존재하지 않는다.** 그래서 이 6칸은 항상 `TileKind.Blocked` 고정이며 231칸 중 플레이에 쓰는 칸은 225칸이다.
- **② 대칭 검증**: `MapDefinitionValidator`는 이 6칸을 **대응쌍 비교 대상에서 제외**하되, 그냥 빠뜨리지 않고 **「항상 `TileKind.Blocked` 인가」를 확인하는 고정값 검사로 대체**한다. 제외만 하면 그 6칸이 무엇이든 될 수 있어 검증에 구멍이 생긴다.
- **③ canonical hash**: **배열 231칸과 해시 범위는 그대로 유지한다.** `index = row * 11 + col` 인덱스 규칙도 바뀌지 않는다. 직렬화 범위와 해시 범위를 갈라 놓으면 `TileKind` 통합이 없애려던 「같은 맵이 다른 바이트가 되는 경로」를 다른 자리에 새로 만들게 된다.
- 같은 내용이 `GameSystemRules_RandomMap.md` 규칙 10에도 있다. **두 곳이 어긋나면 규칙 문서가 옳다.**
- `HexGridRenderer`는 `TileKind.Blocked` 좌표에 standard hex tile mesh와 collider를 생성하지 않는다. `TileKind.Normal`·`TileKind.NoBuild`와 다른 높이 geometry도 만들지 않아 결과는 빈 공간이다.
- selection/raycast 진입점은 hit된 배경 또는 하부 collider만 신뢰하지 않는다. 변환된 맵 좌표가 범위 안인지 확인한 뒤 `TileKind != Blocked`인 경우에만 선택 대상으로 반환한다. 따라서 `TileKind.Blocked` 좌표는 이동 명령, 점령, 건설과 selection event를 발생시키지 않는다.
- 추후 obstacle prefab/asset은 `TileKind.Blocked` 좌표의 빈 공간 시각을 대체하는 presentation 확장점이다. domain `TileKind`, collider 기반 선택 금지, 이동/점령/건설 규칙, canonical hash와 exact symmetry 계약은 유지한다. ⚠️ **짝 없는 6칸은 회전 상대가 없으므로, obstacle asset 도입 시 반드시 예외 처리한다** — 그러지 않으면 한쪽에만 보이는 지형 6개가 생긴다.

**`InputHandler`(1·2) + `GridInteractionUseCase`(3~5) 클릭 판정 순서:**

1. 기존 building action 분기
2. `MineKind` 기반 MiningPost 자격 분기
3. `TileKind.Blocked`
4. `TileKind.NoBuild`
5. 일반 타일(`TileKind.Normal`)

- `TileKind.Blocked`/빈 공간은 논리 좌표가 있어도 unselectable이다. `Deselect`로 이전 highlight를 해제하고 `BuildingPlacementUI`를 닫되 토스트와 새 `TileSelected` 이벤트는 발생시키지 않는다.
- 자기 팀 소유 `TileKind.NoBuild`는 정상 `TileSelected`와 highlight를 발생시킨다. 건설 패널은 열지 않고 stale 패널을 닫은 뒤 `ToastKey.BuildingNotAllowed`(`이 타일에는 건설할 수 없습니다`)를 발행한다.
- 중립·적 소유 `TileKind.NoBuild`는 선택·highlight만 수행하고 stale 패널은 닫는다. 건설 의도가 아니므로 토스트는 없다.

> 이 클릭 판정 순서는 `GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 5~8과 **같은 내용**이다. 한쪽만 고치면 조용히 갈라지므로 항상 함께 고친다.
- building action과 광산 분기를 먼저 처리하므로 기존 건물 상호작용과 `MineKind != None`인 광산의 MiningPost 예외가 `TileKind.NoBuild`에 가로막히지 않는다.

**기존 코드 전환 요구:**

> **2026-09-03 진행 상태 — 무작위 맵 1단계(타일 상태 계약 전환)로 아래 6개 중 4개가 완료됐다.** 각 항목 앞의 표시가 그 항목의 상태다.
> `✅` 는 코드 구현 + 에디터 실기 검증까지 끝난 것, `⚠️` 는 절반만 닿은 것, `⏳` 는 착수 전이다.
> **완료 항목의 「~한다」 미래형 문장은 지우지 않고 그대로 둔다** — 무엇을 요구했던 계약인지가 남아야 나중에 대조할 수 있기 때문이며, 완료 여부는 표시와 괄호 주석으로만 구분한다.

- ✅ **(완료)** mutable `HexTile.IsWalkable` 필드를 제거하거나 setter 없는 계산 프로퍼티로 전환한다.
  → `Domain/Hex/HexTile.cs` 에서 setter 없는 계산 프로퍼티가 되었고, 생성자의 `isWalkable` 매개변수도 함께 제거됐다.
- ✅ **(완료)** 광산 배치의 이중 대입은 `MineKind` 설정으로 교체한다.
  → `Bootstrap/GameBootstrapper.Map.cs` 의 광산 배치가 `MineKind` 한 축만 설정한다. 종전에 함께 대입하던 광산 표시용 저장 필드는 코드에서 삭제됐다(`.cs` 전수 검색 0건).
- ✅ **(완료)** 건물 배치 시 `IsWalkable=false` 대신 `HasBuilding=true`를 설정한다.
  → `Application/UseCases/BuildingPlacementUseCase.cs` 의 일반 건물·채굴소 배치 두 자리 모두.
- ✅ **(완료)** 건물 철거/파괴 시 `IsWalkable=true` 복구 대신 `HasBuilding=false`만 설정한다.
  → 같은 파일 `RemoveBuilding`. **종전에 있던 「광산 타일이면 되돌리지 않는다」 가드는 제거됐다** — 계산 프로퍼티가 `MineKind == None` 을 이미 요구하므로 가드 없이도 광산 타일은 계속 이동 불가로 계산된다. 이 지점이 이번 전환의 최대 위험 자리였으므로 **실기에서 채굴소 철거 후 이동 불가 유지를 직접 확인했다.**
- ⚠️ **(절반 완료 — 남은 절반은 2단계)** 이동, 스폰, 경로탐색은 계산된 `IsWalkable`을 읽고, 건설·점령은 위의 전용 조건을 사용한다.
  → **앞 절반(이동·스폰·경로탐색)은 완료.** 경로탐색 3종과 스폰·이동 판정이 모두 계산된 `IsWalkable`을 읽는다.
  → **뒤 절반(건설·점령의 전용 조건)은 미완.** 일반 건물 배치 판정은 아직 이동 가능 여부를 그대로 쓰고 있고(`TileKind == Normal` 을 확인하지 않는다), 점령에는 `TileKind != Blocked` 확인이 없다. 채굴소 배치만 전용 조건(`MineKind != None` + 건물 없음 + 인접 팀 타일)을 쓴다.
  → **지금 결과가 갈리지 않는 이유:** 현재 고정 맵은 `TileKind` 를 설정하는 코드가 한 곳도 없어 모든 타일이 `Normal` 이다(`.cs` 전수 검색으로 확인). 이동 기준과 건설 기준이 갈라지려면 `NoBuild`/`Blocked` 타일이 실제로 존재해야 하고, 그것을 만들어내는 맵 생성기가 **2단계** 산출물이다.
- ⏳ **(미착수 — 2단계)** `Assets/_Project/Scripts/Application/Services/AIOpponentController.cs`의 `FindPlacementTile()` 배치 후보 판정(**807~809행**, 세 줄에 걸친 한 문장)을 이동 판정에서 **위 「일반 건설」 조건**으로 교체한다. AI 건물 배치는 건설이므로 바로 위 항목의 방침이 그대로 적용되는 자리다. **같은 파일 770~773행의 XML 주석도 같은 조건을 적고 있으므로 함께 옮긴다** — 한쪽만 고치면 주석이 코드와 어긋난 채 남는다. 기획 계약의 단일 소스는 `GameSystemRules/GameSystemRules_AI.md` 규칙 26이다.
  → 2026-09-03 기준 코드·주석 둘 다 그대로다. **바로 위 항목의 뒤 절반과 같은 이유로 3단계까지 미룬다.**
  → ⚠️ 같은 파일의 **광산 타일 조회**(`CacheMineTiles`)는 이 항목과 **별개이며 이미 `MineKind` 기준으로 전환됐다.** 한 파일 안에 전환된 자리와 안 된 자리가 함께 있으므로 한쪽을 보고 다른 쪽을 단정하지 않는다.

##### 재경기 `MapDefinition` 생명주기

기존 재경기 요청·수락·거절 RPC와 Game 씬 재로드 구조를 유지한다. **요청 payload에 맵 조건(mode)을 싣지 않는다 — 재경기 맵은 매번 새로 준비한다.**

> **[🔴 2026-09-14 개정 — 사용자 확정]** 종전 설계는 요청 payload에 `RematchMapMode`(`SameMap`/`NewMap`) enum 을 추가하고, 서버 재경기 컨텍스트가 **pending 요청자와 mode** 를 함께 보관하며, 상대 팝업이 그 조건을 표시하고, **서로 다른 mode 요청이 교차해도 자동 시작하지 않는다**고 정했다. **맵 조건 선택이 없어져 enum · mode 보관 · 조건 표시 · 「서로 다른 mode 교차」 규정 · `SameMap` 흐름 전체를 제거**했다. 기획·규칙 쪽 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 14 의 개정 블록이다.
> 🔴 **함께 사라진 것 — 「완성 후보의 canonical hash 가 현재 definition hash 와 같으면 폐기하고 다시 뽑는다」 단계**(종전 `NewMap` 흐름 3번). 그 검사는 canonical byte 선두에 `MapVersion`·`RootSeed` 가 들어가는 구조(`MapDefinitionCodec`) 때문에 **정상 생성 경로에서는 결코 걸리지 않고**, 실제로 걸리는 경로는 **유형별 고정 폴백 템플릿뿐**이다. 폴백이 연속으로 쓰인 사실은 **재시도로 감추지 않고 `MapPreparationUsedFallbackTemplate`(Warn) 로 드러낸다.**
> ⚠️ **서버 재경기 컨텍스트가 완전히 없어지는 것은 아니다** — **현재 확정 `MapDefinition` package/hash 를 Game 씬 재로드 사이에 유지하고 로비 복귀·연결 종료 시 폐기하는 책임은 그대로 남는다.** 사라진 것은 **pending 요청자의 mode** 다.

서버가 관리하는 재경기 컨텍스트는 현재 확정 `MapDefinition` package/hash를 보관한다.

**요청 조정:**

- 요청을 받은 상대 팝업은 요청자를 표시한다. 표시할 맵 조건은 없다.
- 상대는 수락 또는 거절한다.
- pending 요청이 없는 상태에서 서버가 먼저 받은 요청을 현재 제안으로 확정한다.

**재경기 맵 흐름:**

1. Game 종료 상태와 결과 화면을 유지하며 팀·종족·그 밖의 매치 설정은 직전 경기 값으로 고정한다.
2. Host가 새 64-bit root seed를 만들고 맵 관련 값만 새로 준비한다. `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`, 지형 세부 형태, 중립 광산 위치를 다시 선택·생성한다. 새로 뽑힌 `NeutralMineCount`에서 실제 `InitialGold`를 확정하며 Client 로컬 설정은 사용하지 않는다. 장식 placement 활성화 이후에는 장식도 다시 생성하지만 최초 구현은 빈 목록을 유지한다.
3. 기존 current definition은 그대로 보존하고 새 package는 pending 후보로만 둔다.
4. 공용 `NetworkMapTransfer`가 새 package를 chunk로 전달하고 Client가 길이/버전→SHA-256→deserialize→semantic fairness validator를 통과한 뒤 `MapReady(success=true)`로 ACK한다.
5. Host도 최종 후보 검증과 양측 준비 상태를 확인한다.
6. 성공 시 pending 후보를 current definition으로 원자적 교체한다.
7. 교체 후에만 기존 Game 씬 재로드 경로를 실행한다.

생성, 전송, hash, deserialize, semantic 검증, ACK 중 하나라도 실패하면 pending 후보를 폐기한다. 기존 definition과 결과 화면은 유지하며 Game 씬을 재로드하지 않는다.

싱글플레이 재경기도 매 판 새 맵을 준비한다. 로컬 생성·semantic 검증 성공 후 원자적으로 교체한 다음 씬을 재로드한다. **이는 현재 코드가 이미 하고 있는 동작이다** — `GameEndUI.OnRestartClicked` → `GameBootstrapper.LoadMap` → `MapRootSeed.Create()`(`Guid.NewGuid()` ⊕ `DateTime.UtcNow.Ticks`)로 호출마다 새 root seed 를 만든다(문서 작업 중 직접 실측). **싱글 코드 변경은 0건이다.**

재경기 컨텍스트는 Game 씬 재로드 사이에는 유지한다. 로비 복귀, 세션 종료, 연결 종료/끊김 시 current definition, pending 후보, pending 요청을 모두 폐기한다.

<a id="결과-화면-이탈-판정통보-구조-2026-09-16-확정-설계--미구현"></a>

#### 결과 화면 이탈 판정·통보 구조 (단계 6 구현 · 관측 범위별 검증)

현재 단계 6의 무반응 이탈 판정은 구현되어 있으며 2026-09-30 관측 범위·등급은 아래 갱신 포인터가 지정한 경기 흐름 UI D-6 기록에서 확인한다. 아래 표·설계·날짜별 정정은 당시 결정 과정을 보존한 것이며 최신 구조와 구분해서 읽는다. *(2026-10-13 상태 정리 — 종전 제목의 「미구현」은 같은 절의 2026-09-30 해소 근거와 어긋나 수정했다. 원래 제목은 위 명시 앵커와 아래 인용 기록으로 추적 가능하다.)*

> **[🔴 2026-09-30 갱신 포인터 — 🔴 위 절 제목과 아래 본문·표·기존 갱신 블록은 한 글자도 고치지 않는다(`.claude/MEMORY.md` B-7). 이 블록은 덧붙인 포인터다]**
> 🔴 **절 제목의 「미구현」은 더 이상 참이 아니다 — 이 구조는 「경기 종료 후 UI·프로세스」 단계 6 에서 구현됐고, 2026-09-30 실기에서 무반응 이탈 판정과 그 뒤의 화면 처리가 관측됐다.** 🔴 **이 어긋남은 2026-09-30 회차가 만든 것이 아니라 단계 6(2026-09-27) 이후로 낡아 있던 것**이며, **판정 규정의 단일 소스는 [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) 규칙 17**, **실기 관측 기록의 단일 소스는 [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」 규칙 D-6 의 2026-09-30 블록**이다 — 여기에 옮겨 적지 않는다.
> ⚠️ **「구현됐다」가 「이 절의 모든 조항이 검증됐다」는 뜻은 아니다** — 어느 항목이 어느 근거 등급으로 확인됐는지는 위 규칙 D-6 블록의 등급 서술이 단일 소스이며, 🔴 **이 포인터가 닫는 것은 절 제목의 「미구현」 한 낱말뿐**이다.

경기가 끝나고 결과 화면이 떠 있는 동안 **상대가 사라진 사실이 화면에 전달되지 않는다.** 재경기를 요청한 쪽은 버튼이 모두 잠긴 채 자동 로비 복귀 카운트다운에 실려 나간다. 이 절은 그 문제를 푸는 **네트워크 구조**를 적는다. **기획 규정의 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17·18·19** 이며, **화면 문구·버튼·타이머는 `GameSystemRules_UI.md` 「공통 UI 규칙」**이다. 여기에는 **어느 계층이 무엇을 하는가**만 적는다.

**두 갈래와 그 메시지:**

| 갈래 | 감지 주체 | 메시지 흐름 |
|---|---|---|
| **정상 퇴장** (로비 복귀 버튼) | 나가는 클라이언트 자신 | 나가기 전에 **서버에 통보** → 서버가 **상대에게 브로드캐스트**. 받은 쪽은 **대기 없이 즉시** 반영한다 |
| **무반응 이탈** | **서버(Host)** 의 자체 감시 | **30초 무반응**으로 판정 → **상대에게 브로드캐스트** → **연결 종료** |

- 🔴 **NGO RPC 명명 규약을 반드시 지킨다 — 메서드명은 `ServerRpc` / `ClientRpc` 로 끝나야 한다**(`.claude/MEMORY.md` 「아키텍처 핵심 제약」). 이름 예시: 퇴장 통보 `NotifyLeavingResultScreenServerRpc`, 이탈 알림 `NotifyOpponentLeftClientRpc`. **이름은 확정이 아니며 규약을 지키는 형태의 예시**다.
- 🔴 **`NetworkBehaviour` 는 Infrastructure 레이어에만 둔다.** 이 감시·통보는 기존 재경기 컨트롤러와 같은 `Infrastructure/Network/` 소속이며, **Presentation(결과 화면)에는 `GameEvents`(Application) 를 경유해 전달**한다.
- 🔴 **Application 은 `Unity.Netcode` 를 직접 참조하지 않는다** — 정적 홀더(`NetworkContext`)와 `GameEvents` 만 쓴다. 기존 재경기 실패 알림(`GameEvents.OnNetworkRematchMapFailed`)이 같은 모양의 선례다.
- 🔴 **Host 이탈도 같은 통보 하나로 흡수한다** — Host 전용 경로를 만들지 않는다(규칙 17).

> **[🔴 2026-09-21 정정·보강 — 위 표와 위 항목들은 한 글자도 지우지 않고 그대로 두고 덧붙인다(`.claude/MEMORY.md` B-7). 경위를 남기는 이유는 다음 사람이 같은 구조로 되돌아가지 않게 하기 위해서다.]**
>
> 🔴 **위 표의 「무반응 이탈 — 감지 주체 = 서버(Host) 의 자체 감시 → 상대에게 브로드캐스트」가 뒤집혔다. Host 와 Client 가 각자 자기 쪽에서 판정한다**(2026-09-21 사용자 확정).
> - **왜 뒤집혔는가** — 서버 전용으로 두면 **Host 가 결과 화면에서 사라졌을 때 남은 Client 는 아무 통보도 받지 못한다.** 감시할 서버도 브로드캐스트할 RPC 도 **Host 와 함께 사라지기** 때문이다. 이는 [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) 의 **「🔴 최상위 원칙」**〔2026-10-12 이관 — 종전에는 이 문서의 이 절 앞에 있었다〕 위반이며, 그 원칙은 **개별 규칙이 충돌하면 고칠 대상은 개별 규칙**이라고 정하고 있다.
> - ✅ **표의 첫 행(정상 퇴장)은 그대로 유효하다 — 퇴장 통보 `ServerRpc` 와 브로드캐스트를 없애지 않는다.** 정상 퇴장은 **즉시 알리는 것이 맞고**, 자체 감시는 **그 통보가 오지 못한 경우를 받는 그물**이다. **대체 관계가 아니라 보완 관계**다.
> - **따라서 무반응 갈래에서 달라지는 것은 「감지 주체」 한 칸이다** — **30초**라는 값도, **「판정과 동시에 연결을 종료한다」**도 그대로다.
> - 🔴 **판정 전에 「내 인터넷이 되는가」를 먼저 확인한다.** 절차의 단일 소스는 [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) 규칙 1 **「이탈 시 승패 기록 — 규칙 집합」 절의 「이탈 판정 절차」**〔2026-10-12 이관 — 종전에는 이 문서의 아래 절〕이며, **결과 화면 판정에도 같은 절차가 적용된다는 것이 2026-09-21 에 확정**됐다. 🔴 **도달 확인을 만드는 것은 결과 화면 작업(단계 6)의 범위**이고 전적 기록 작업이 나중에 재사용한다.
> - ⚠️ **「응답이 없다」를 무엇으로 재는지는 미확정이다** — **1순위 기존 RTT(`Infrastructure/Network/NetworkGameManager.cs` 의 `GetCurrentRttMs()`) · 2순위 결과 화면 전용 하트비트**라는 **순서만** 확정됐고, 🔴 **RTT 로 「갱신이 멈췄다」를 판별할 수 있는지는 확인되지 않았다**(`CLAUDE.md` 규칙 10). 확인 시점은 **단계 6 착수 시**다.
>   - **[🔴 2026-09-30 갱신 포인터 — 🔴 위 ⚠️ 줄은 한 글자도 고치지 않는다(`.claude/MEMORY.md` B-7). 아래 두 줄은 덧붙임이다]** 🔴 **「미확정」은 더 이상 참이 아니다 — 2순위(결과 화면 전용 하트비트)가 채택돼 구현됐다.** 결과 화면 전용 감시 시계가 **일정 간격으로 신호를 주고받는 형태**로 돌고 있으며, **1순위 RTT 는 이 판정에 쓰이지 않았다** — 그 값을 읽는 자리는 여전히 핑 표시 한 곳뿐이고 그 부품은 어느 씬에도 배치돼 있지 않다(아래 2026-09-22 블록이 그 사실의 단일 소스다). 🔴 **이 어긋남은 2026-09-30 회차가 만든 것이 아니라 단계 6(2026-09-27) 이후로 낡아 있던 것이다.**
>   - ⚠️ **수단 확정을 적을 규칙 쪽 자리는 [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) 규칙 17 의 ③ 절**이고 🔴 **그 표도 아직 「미확정」이라고 적고 있다 — 같은 낡음이 규칙 쪽에도 남아 있으므로 이 포인터를 「규칙이 이미 갱신됐다」로 읽지 않는다.** 규칙 쪽 수선은 **이번 범위 밖**이다(`CLAUDE.md` 규칙 6). **판정이 실기에서 발화한 기록의 단일 소스는 [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) 「공통 UI 규칙」 규칙 D-6 의 2026-09-30 블록**이며 여기에 옮겨 적지 않는다.
> - **기획 규칙 쪽 단일 소스는 `GameSystemRules/GameSystemRules_RandomMap.md` 규칙 17 의 「2026-09-21 추가 (2차)」 블록**이며 여기에 옮겨 적지 않는다.

**전송 계층 연결 타임아웃 — 30초 → 60초:**

| 항목 | 값 | 자리 |
|---|---|---|
| **결과 화면 이탈 판정** (자체 감시) | **30초** | 위 구조 — 규칙 17 |
| **전송 계층 연결 타임아웃** (`m_DisconnectTimeoutMS`) | **30초 → 60초** | `Game.unity` · `Lobby.unity` **두 씬** 모두 |

- 🔴 **왜 늘리는가 — 판정이 먼저 나야 하기 때문이다.** 연결 타임아웃이 판정과 같거나 짧으면 **전송 계층이 먼저 연결을 끊어** 위 통보가 나갈 자리가 사라지고, 플레이어에게는 **이유 없이 끊긴 화면**만 남는다. 60초는 **판정 30초보다 확실히 뒤에 오게 하려는 값**이며, 규칙 17 이 정하는 것은 **부등호 방향**이고 이 절이 **숫자**를 정한다.
- ⚠️ **이 문서 작업에서 씬 값은 바꾸지 않았다 — 기록만 했다.** `m_DisconnectTimeoutMS` 는 씬 직렬화 값이며 **Inspector 값이 코드 기본값보다 우선**하므로, 실제 적용은 두 씬을 모두 고쳐야 성립한다. **한 씬만 고치면 로비와 인게임의 타임아웃이 갈린다.**
  > **[✅ 2026-09-21 갱신 — 위 원문(2026-09-16 시점의 기록)은 그대로 두고 결과만 덧붙인다]** **두 씬 모두 적용됐다.** 커밋 `007f666` 에서 `Game.unity` **45473행** · `Lobby.unity` **7479행** 이 **둘 다 `m_DisconnectTimeoutMS: 60000`** 이다(문서 작업 중 직접 확인). 🔴 **따라서 「씬 값 미적용」으로 읽지 않는다** — 원문은 *그 회차에 바꾸지 않았다*는 뜻이고, 값 자체는 그 뒤에 반영됐다.

**⚠️ 혼동 주의 — 이 설계가 건드리지 않는 다른 「30초」 두 개:**

| 같은 숫자지만 다른 것 | 무엇인가 | 근거 |
|---|---|---|
| **맵 전송 응답 대기 10초** | Host 가 Client 의 `MapReady` 를 기다리는 시간(재전송 포함 최대 20초) | 위 「timeout/retry state machine」 절 · 규칙 16 |
| **인게임 재접속 대기 30초 → 강제 승리** | 경기 **진행 중** 상대가 끊기면 30초 기다렸다가 남은 팀을 강제 승리 처리한다 | `Infrastructure/Network/ReconnectionHandler.cs` `_reconnectWaitSeconds = 30f`(`:47`) → `WaitAndForceWin()` → `NetworkGameEndController.ForceWin()`. **문서 작업 중 직접 실측** |
| **자동 로비 복귀 카운트다운 30초** | 결과 화면이 스스로 로비로 나가기까지의 시간 | `GameEndUI` — 단일 소스는 `ROADMAP.md` 의 해당 행 |

- 🔴 **위 세 값 중 이 설계가 정하는 것은 하나도 없다.** 이 설계가 새로 정하는 것은 **결과 화면 이탈 판정 30초**와 **연결 타임아웃 60초** 둘뿐이다. **값이 같다고 같은 시계로 묶지 않는다.**
- 🔴 **[2026-09-21 추가 — 위 표와 문장은 그대로 두고 덧붙인다]** 위 표 가운데 **「인게임 재접속 대기 30초 → 강제 승리」는 없어질 예정**이다(**확정 E**). 🔴 **아직 코드는 그대로이므로 위 표는 현재 상태로서 여전히 참이고, 바뀌는 것은 「앞으로」다.** 판정은 **그 30초를 기다리지 않고 끊김을 감지한 그 순간(`t=60`)에** 내린다 — **단일 소스는 [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) 규칙 2 「인게임 이탈 처리 — `ReconnectionHandler` 정리」 절**〔2026-10-12 이관 — 종전에는 이 문서의 아래 절〕이며 여기에 옮겨 적지 않는다. ⚠️ **나머지 두 값(맵 전송 응답 대기 10초 · 자동 로비 복귀 카운트다운)은 이 확정과 무관하다.**
- ⚠️ **확인되지 않은 것 (구현 착수 시 먼저 볼 것)**: `ReconnectionHandler` 의 `OnClientDisconnected`(`:134`)는 **경기 종료 여부를 보는 가드가 없다**(가드는 `LocalClientId` 일치와 `_forceWinTriggered` 둘뿐 — 문서 작업 중 직접 실측). 🔴 **그래서 결과 화면 위에서 상대가 끊겼을 때 이 코루틴이 함께 돌아 30초 뒤 `ForceWin()` 을 부르는지 여부는 확인하지 않았다.** 이번 이탈 판정도 같은 30초라 **두 시계가 겹칠 수 있다.** **확인 전까지 「겹친다」고도 「겹치지 않는다」고도 적지 않는다**(CLAUDE.md 규칙 10).
  > **[✅ 2026-09-16 확인 — 위 원문은 그대로 두고 결과만 덧붙인다]** **겹친다. 다만 무해하다.** 메인 세션이 두 자리를 직접 실측해 확인했다. ⓐ `ReconnectionHandler.OnClientDisconnected` 의 가드는 위 원문대로 **둘뿐이라, 결과 화면 위에서 상대가 끊겨도 재접속 대기 코루틴이 그대로 시작된다.** ⓑ 그러나 그 코루틴이 30초 뒤 부르는 `NetworkGameEndController.ForceWin()` 은 **맨 앞에서 `if (_announced) return;` 으로 막힌다** — 결과 화면이 떠 있다는 것은 이미 승자가 발표됐다는 뜻이고 그 시점에 `_announced` 는 `true` 다. 🔴 **따라서 그 30초는 기다린 끝에 아무 일도 하지 않는 죽은 대기다.** ⚠️ **그래도 구현 시 주의할 것**: 이탈 판정을 새로 넣으면 **같은 30초짜리 시계가 두 개 동시에 돈다.** 하나는 아무것도 하지 않고 하나는 판정을 내리므로 동작은 갈리지 않지만, **로그를 읽을 때 두 시계를 같은 것으로 착각하기 쉽다.** 둘을 구분할 수 있게 남길 것.

**범위 밖 — 씬 재로드 중 이탈:** 전송·검증이 모두 끝난 뒤의 짧은 구간이고 되돌릴 결과 화면이 이미 없어 **이번 설계에서 다루지 않는다.** 근거의 단일 소스는 **규칙 19** 이며 여기에 옮겨 적지 않는다.

**맵 전송 중 이탈:** 규칙 16 의 **「연결 끊김은 즉시 실패」 경로를 그대로 쓴다.** 🔴 **새 메시지도, 새 `LogEvent` 키도 만들지 않는다** — 갈라지는 것은 **실패 사유가 `Disconnected` 일 때의 화면 문구 하나뿐**이다(규칙 18). 위 「복구 상태」 절의 복원 절차는 **사유와 무관하게 그대로**다.

**🔴 [2026-09-22 사실 확인 + 단계 7 진단 정정] `NetworkStatusUI` 가 어느 씬에도 배치돼 있지 않다 — 이 블록이 이 사실의 단일 소스다.**

> **위 문장들은 한 글자도 지우지 않고 그대로 두고 덧붙인다(`.claude/MEMORY.md` B-7).** 정정 대상은 **단계 7 회차의 진단 한 줄**이고, **구조 설계는 아무것도 바뀌지 않는다.**

**[쉬운 말로]** 핑(지연 시간)을 화면에 보여 주고 **「상대방이 연결을 끊었습니다」 팝업**을 띄우도록 만들어 둔 부품이 있는데, **그 부품이 게임 화면에 실제로 붙어 있지 않다.** 그래서 ⓐ **핑 표시가 나오지 않고** ⓑ 🔴 **그 팝업은 아예 존재하지 않는다.** 파일이 있다는 것과 화면에 놓여 있다는 것은 다른 이야기다.

**실측 (이 문서 작업 중 직접 확인):**

| 무엇을 재었나 | 결과 |
|---|---|
| `Assets/_Project/Scripts/Presentation/UI/NetworkStatusUI.cs.meta` 의 guid `adc83ceead3e36246ba137d2ceede0a6` 로 `Assets` 전체의 `*.unity`·`*.prefab` 검색 | 🔴 **0건 — 어느 씬·프리팹에도 없다** |
| 2026-09-22 에디터 로그(`_Logs/_editor/2026-09-22/RuntimeLog.txt`)와 실기 logcat(`_Logs/2026-09-22/03_43_logcat/RuntimeLog_device.txt`) 에서 `NetworkStatusUI` · `Start()` 의 *"네트워크 상태 모니터링 시작"* | 🔴 **양쪽 각 0건 — 한 번도 실행되지 않았다** |
| 같은 파일 헤더 주석 | *"씬 구조 (Inspector에서 수동 배치)"* — **배치를 전제하고 있으나 그 배치가 되어 있지 않다** |

- **그래서 무슨 일이 되는가**: ⓐ **핑(RTT) 표시가 동작하지 않는다** ⓑ 🔴 **「상대방이 연결을 끊었습니다」 팝업(`_disconnectPanel`)이 존재하지 않는다.**
- 🔴 **단계 7 회차의 진단 한 줄을 정정한다** — 그 회차는 *"`ShutdownNetwork()` 가 「연결이 끊겼습니다」 팝업을 띄워 결과 화면의 문구(`GameSystemRules/GameSystemRules_UI.md` 「공통 UI 규칙」 **규칙 D-1**)를 가릴 수 있다"* 고 보고 `NetworkStatusUI` 에 억제 가드를 넣었다(커밋 `05094ad`). **그 팝업은 현재 상태에서 애초에 뜨지 않으므로 「지금 겹치고 있다」는 서술은 사실이 아니었다.** 원인은 **코드만 보고 씬 배치를 확인하지 않은 것**이다.
- ⚠️ **가드 자체는 지우지 않는다 — 이 정정은 코드 제거 근거가 아니다.** `NetworkStatusUI` 를 나중에 배치하면 그 겹침이 **실제로 발생**하므로 가드는 **유효한 예방**이다. **바뀌는 것은 「지금 겹치고 있다」는 서술 하나뿐**이며, **코드는 이 회차에 한 줄도 건드리지 않았다.**
- 🔴 **처리 방침은 확정되지 않았다 — 배치할 것인가 · 그 팝업이 필요한가 둘 다 미정이다.** ⚠️ **메인 세션의 관찰로는** 「이탈 시 승패 기록」의 **확정 D**〔2026-10-12 이관·정정 — 확정 D 는 실제로는 [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) 규칙 2 「인게임 이탈 처리」 절에 있다. 종전 표기의 귀속이 어긋나 있었다〕 가 들어가면 승패가 나서 결과 화면이 뜨므로 **사유 통보를 그 화면이 대신할 수 있다** — 🔴 **그러나 이것은 제안이며 확정이 아니다.** **작업 항목은 [ROADMAP.md](ROADMAP.md) 의 「`NetworkStatusUI` 미배치」 행**이고, 그 행도 **방침이 미확정이라고 적고 있다.**
- ⚠️ **이 사실이 함께 드러낸 것** — 위 2026-09-21 정정 블록의 **「1순위 기존 RTT(`GetCurrentRttMs()`)」** 에서, **현재 그 값을 읽는 유일한 자리가 이 `NetworkStatusUI` 였다.** 🔴 **즉 RTT 는 지금 런타임에서 아무도 읽지 않는다** — **「RTT 로 「갱신이 멈췄다」를 판별할 수 있는지」는 여전히 미확인**이며, 이 사실은 그 질문의 **답이 아니라 「확인할 자리조차 실행되지 않았다」는 사정**이다.
- ⚠️ **인계값 대조** — 메인 세션이 전달한 에디터 로그 행수는 **5,119행**이었으나 이 체크아웃의 사본은 **1,222행**(마지막 항목 `03:41:47`)이고, 전달된 **12:01~12:05 구간의 발췌는 이 사본에 없다.** 이 파일은 **에디터 세션마다 append 되는 상시 로그**(사본 안에 `=== 세션 시작 ===` 2건)이므로 **이후 더 커진 상태로 보인다.** 🔴 **그러므로 그 구간은 「없다」가 아니라 「이 체크아웃에서 재측정하지 못했다」**로 적는다(`CLAUDE.md` 규칙 10).

---

#### 치팅 방어
- **서버 검증**: 모든 행동을 서버(Host)에서 검증
- **자원 관리**: 클라이언트는 읽기만 가능
- **유닛 생성**: 인구수/자원 서버 체크
- **타일 점령**: 유닛 위치 서버 관리

---
