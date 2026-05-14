# 자동/수동 생산 시스템 — 멀티플레이 전용 테스트 케이스

**작성일:** 2026-03-23
**목적:** 싱글플레이 TC와 별개로, 멀티플레이 환경에서만 발생할 수 있는 문제를 검증한다.

---

## 테스트 환경 전제

- 빌드 2대 또는 에디터(Host) + 빌드(Client) 구성
- Host는 Blue팀, Client는 Red팀
- 각 플레이어는 본인 팀 배럭만 조작 가능
- 테스트 전 양측 골드 초기값 확인
- 한 TC 종료 후 재경기로 상태를 초기화한 뒤 다음 TC 진행

---

## TC 목록

---

### MULTI-1: Client — 자동생산 1개 등록

**전제:** 게임 시작 직후, Client 슬롯 모두 비어있음

**동작:**
1. Client가 배럭 팝업을 열고 Assault 자동생산 버튼을 길게 눌러 등록한다

**기댓값:**
- Client 슬롯0에 Assault가 표시되고 자동생산 인디케이터가 켜진다
- Client 골드가 Assault 생산비만큼 차감된다
- Host 화면에는 아무 변화가 없다

**결과:** PASS;

---

### MULTI-2: Client — 자동생산 2개 등록

**전제:** 게임 시작 직후, Client 슬롯 모두 비어있음

**동작:**
1. Client가 Assault 자동생산을 등록한다
2. Client가 Pistoleer 자동생산을 등록한다

**기댓값:**
- Client 슬롯0에 Assault (자동 인디케이터 ON), 슬롯1에 Pistoleer (자동 인디케이터 ON)
- 2번째 등록 시 Pistoleer 골드가 차감된다
- Host 화면에는 아무 변화가 없다

**결과:** PASS;

---

### MULTI-3: Client — 자동생산 3개 등록 시 슬롯2 골드 차감

**전제:** 게임 시작 직후, Client 슬롯 모두 비어있음

**동작:**
1. Client가 Assault 자동생산을 등록한다 → 골드 차감 확인
2. Client가 Pistoleer 자동생산을 등록한다 → 골드 차감 확인
3. Client가 Sniper 자동생산을 등록한다

**기댓값:**
- 슬롯0 Assault, 슬롯1 Pistoleer, 슬롯2 Sniper 모두 자동 인디케이터 ON
- 3번째 Sniper 등록 시 Sniper 골드가 차감된다
- Host 화면에는 아무 변화가 없다

**결과:** PASS;

---

### MULTI-4: Client — 슬롯1 탭 취소 후 슬롯 정리

**전제:** Client 슬롯0에 Assault, 슬롯1에 Sniper가 자동생산으로 등록된 상태

**동작:**
1. Client가 슬롯1 (Sniper)을 탭하여 취소한다

**기댓값:**
- 취소 후 슬롯0 Assault (자동 인디케이터 ON), 슬롯1 비어있음, 슬롯2 비어있음
- Sniper 골드가 환불된다
- 슬롯2에 Assault가 중복 표시되지 않는다
- Host 화면에는 아무 변화가 없다

**결과:** PASS;

---

### MULTI-5: Host 조작이 Client 화면에 영향을 주지 않음

**전제:** 게임 시작 직후, 양측 슬롯 모두 비어있음

**동작:**
1. Host가 Assault, Pistoleer, Sniper 자동생산을 순서대로 등록한다
2. Client는 아무것도 조작하지 않는다

**기댓값:**
- Host 슬롯0/1/2에 각 유닛이 표시된다
- Client 슬롯0/1/2는 모두 비어있다
- Client 골드에 변화가 없다

**결과:** PASS;

---

### MULTI-6: Client 조작이 Host 화면에 영향을 주지 않음

**전제:** 게임 시작 직후, 양측 슬롯 모두 비어있음

**동작:**
1. Client가 Assault, Pistoleer, Sniper 자동생산을 순서대로 등록한다
2. Host는 아무것도 조작하지 않는다

**기댓값:**
- Client 슬롯0/1/2에 각 유닛이 표시된다
- Host 슬롯0/1/2는 모두 비어있다
- Host 골드에 변화가 없다

**결과:** PASS;

---

### MULTI-7: Client 슬롯0 취소 후 슬롯 이동

**전제:** Client 슬롯0에 Assault, 슬롯1에 Pistoleer가 자동생산으로 등록된 상태

**동작:**
1. Client가 슬롯0 (Assault)을 탭하여 취소한다

**기댓값:**
- Assault 골드가 환불된다
- 슬롯0에 Pistoleer가 표시된다 (자동 인디케이터 ON)
- 슬롯1, 슬롯2는 비어있다
- Host 화면에는 아무 변화가 없다

**결과:** PASS;

---

### MULTI-8: Client 빠른 연속 입력 후 최종 상태 안정성

**전제:** 게임 시작 직후, Client 슬롯 모두 비어있음

**동작:**
1. Client가 Assault 자동등록 → 즉시 취소 → 즉시 재등록을 빠르게 연속으로 실행한다

**기댓값:**
- 모든 입력이 처리된 후 슬롯 상태가 마지막 입력 결과와 일치한다
- 슬롯이 잘못된 상태로 고정되지 않는다
- 골드 차감 횟수가 최종 등록된 수와 일치한다

**결과:** PASS (BUG-15~19 수정 후 재테스트 2026-03-24);

---

## 테스트 결과 기록

| TC | 정적 분석 | 실기 (Host측) | 실기 (Client측) | 비고 |
|----|---------|-------------|----------------|------|
| MULTI-1 | PASS | PASS | PASS | |
| MULTI-2 | PASS | PASS | PASS | |
| MULTI-3 | PASS | PASS | PASS | |
| MULTI-4 | FAIL | PASS | PASS | BUG-14 수정 후 재테스트 PASS |
| MULTI-5 | PASS | PASS | PASS | 격리 |
| MULTI-6 | PASS | PASS | PASS | 격리 |
| MULTI-7 | PASS* | PASS | PASS | |
| MULTI-8 | FAIL* | PASS | PASS | BUG-15~19 수정 완료, 재테스트 PASS (2026-03-24) |

---

## 정적 분석 결과 (qa-tester)

> **분석 기준일:** 2026-03-24
> **분석 대상:** NetworkProductionController.cs / UnitProductionUseCase.cs / ProductionPanelUI.cs / ProductionState.cs

---

### MULTI-1: PASS

Client가 자동생산 버튼을 길게 누르면 서버에 요청이 전달되고, 서버가 처리한 뒤 결과를 Client에 동기화하는 흐름이 코드상 완결되어 있다. 골드 차감은 서버에서만 수행되며, 클라이언트 UI는 서버 동기화 이후 갱신된다.

---

### MULTI-2: PASS

MULTI-1과 동일한 흐름. 2번째 항목 등록 시 서버가 슬롯 표시 가능 여부를 판단하고 골드를 즉시 차감하는 로직이 올바르게 동작한다.

---

### MULTI-3: PASS

3번째 자동 항목 등록 시 서버의 슬롯 표시 판단 로직(CanAutoEntryShowInSlot)이 Client 요청 경로에서도 동일하게 서버에서 실행된다. BUG-12 수정 내용이 멀티플레이에서도 정상 적용된다.

---

### MULTI-4: FAIL — BUG-14

Client가 슬롯을 탭하여 취소할 때 서버에 취소 요청을 전달하는 RPC가 현재 코드에 존재하지 않는다. Client가 로컬에서 직접 취소를 처리하므로 서버의 생산 상태에 반영되지 않는다. 이후 서버가 상태를 동기화하면 취소가 무효화되어 슬롯이 원상복귀된다. 골드 환불도 서버에서 발생하지 않는다.

**필요한 수정:**
- `NetworkProductionController.cs`에 슬롯 취소 전용 ServerRpc 추가
- `ProductionPanelUI.cs`에서 멀티플레이 시 해당 ServerRpc를 호출하도록 분기 추가

---

### MULTI-5: PASS

생산 상태가 배럭 ID 기준으로 관리되며, 각 플레이어는 본인 팀 배럭의 상태만 보유한다. 동기화 이벤트가 전달되더라도 상대방 배럭 ID에 해당하는 상태가 없으므로 화면에 영향을 주지 않는다.

---

### MULTI-6: PASS

MULTI-5와 동일한 격리 구조. Host는 서버이므로 동기화 이벤트 수신 시 서버 측은 조기 반환 처리되어 UI에 영향을 주지 않는다.

---

### MULTI-7: PASS*

슬롯0 취소가 서버를 통해 처리되고 최종적으로 클라이언트에 올바른 상태가 동기화된다. 다만 두 개의 네트워크 이벤트가 순차적으로 도착하는 사이에 슬롯이 일시적으로 잘못 표시될 수 있다. 최종 수렴은 보장되므로 실기에서 깜빡임 여부 확인 필요.

---

### MULTI-8: FAIL — BUG-15, BUG-16, BUG-17 발견 (2026-03-24 실기)

취소 방법이 슬롯 직접 탭인 경우 BUG-14와 동일한 문제로 취소가 서버에 반영되지 않는다. 취소 방법이 자동생산 버튼 탭인 경우에는 정상 경로로 처리되어 최종 수렴이 보장된다.

**실기 테스트 결과 (Host/Client 모두 발생):**
- 자동생산 등록 → 취소 → 재등록 시, 슬롯0에 이미 동일 유닛이 생산 중임에도 골드가 추가로 차감됨
- 생산 큐에 새로 추가하지 않았으므로 골드 차감이 발생하면 안 됨 → BUG-15로 등록

---

### 실기 테스트 중 신규 발견 버그

#### BUG-15: 자동생산 등록 → 취소 → 재등록 시 골드 중복 차감 ✅ 수정 완료 (2026-03-24)

**발견 경위:** MULTI-8 실기 테스트에서 발견 (Host/Client 모두 발생)

**재현 절차:**
1. Assault 롱프레스 → 자동생산 등록 (슬롯0 Assault, 골드 차감)
2. Assault 롱프레스 → 자동생산 취소 (인디케이터 OFF, 골드 환불)
3. Assault 롱프레스 → 자동생산 재등록

**기댓값 (3번):**
- 슬롯0에 Assault가 이미 생산 중이므로 골드 차감 없이 인디케이터만 ON

**실제 결과 (3번):**
- 골드가 다시 차감됨

**수정 내용:** `UnitProductionUseCase.ToggleAutoProduction` 추가 경로에서 `type == state.CurrentProducing`이면 `canShowInSlot = false` 강제

---

#### BUG-16: 이미 자동생산 중인 유닛을 롱프레스 시 취소 대신 슬롯2에 추가됨 ✅ 수정 완료 (2026-03-24)

**발견 경위:** TC에 없는 시나리오에서 발견 (2026-03-24 실기, Client만 발생)

**재현 절차:**
1. Assault 롱프레스 → 자동생산 등록 (슬롯0 Assault)
2. Sniper 롱프레스 → 자동생산 등록 (슬롯0 Assault, 슬롯1 Sniper)
3. Assault 또는 Sniper 롱프레스

**기댓값 (3번):**
- 이미 자동생산 중인 유닛이므로 자동생산 취소 → 해당 유닛 인디케이터 OFF

**실제 결과 (3번):**
- 슬롯2에 해당 유닛이 추가됨 (취소가 아닌 추가로 동작)

**근본 원인:** `AutoProductionChangedClientRpc`에서 `isAuto=true`일 때 취소된 유닛을 다시 추가하는 버그.
- `SyncQueueStateClientRpc`(전체 상태 동기화)가 먼저 도착하여 Assault를 AutoEntries에서 제거
- 이후 `AutoProductionChangedClientRpc(isAuto=true, Assault)` 도착 → 기존 코드가 Assault를 다시 추가
- 다음 롱프레스 시 클라이언트는 Assault가 등록된 것으로 보지만, 서버는 미등록 상태 → 서버가 추가 경로 실행

**수정 내용:** `AutoProductionChangedClientRpc`에서 AutoEntries 수정 로직 제거. `IsAutoMode` 반영과 UI 이벤트 발행만 수행. AutoEntries는 `SyncQueueStateClientRpc`가 전담.

**영향 범위:** Client만 발생

---

#### BUG-17: Assault 취소 후 Sniper 취소 시 슬롯1 Sniper 소멸 ✅ 수정 완료 (2026-03-24)

**발견 경위:** TC에 없는 시나리오에서 발견 (2026-03-24 실기, Host/Client 모두 발생)

**재현 절차:**
1. Assault 롱프레스 → 자동생산 등록 (슬롯0 Assault, 인디케이터 ON)
2. Sniper 롱프레스 → 자동생산 등록 (슬롯0 Assault, 슬롯1 Sniper, 인디케이터 ON)
3. Assault 롱프레스 → Assault 자동생산 취소 (슬롯0 Assault 유지, 슬롯1 Sniper 유지, Assault 인디케이터 OFF)
4. Sniper 롱프레스 → Sniper 자동생산 취소

**기댓값 (4번):**
- 슬롯1 Sniper는 수동생산 큐로 이관되어 생산 유지 (인디케이터 OFF, 슬롯에는 남아있음)

**실제 결과 (4번):**
- 슬롯1 Sniper가 소멸됨 (골드 환불 없음)

**근본 원인:** `ToggleAutoProduction` 취소 경로의 `isSlot0` 판단이 `AutoIndex 위치 비교`로 구현되어 있음.
- 3번에서 Assault(AutoEntries[0]) 취소 후 Sniper가 AutoEntries[0]으로 이동, AutoIndex=0
- 4번에서 Sniper 취소 시 `isSlot0 = (AutoIndexOf(Sniper)==AutoIndex)` = true → 오판
- 실제로는 Sniper가 슬롯1(CurrentProducing=Assault)인데 슬롯0으로 착각 → Rule 2(ManualQueue 이관) 미적용 → 소멸

**수정 내용:** `UnitProductionUseCase.ToggleAutoProduction`의 `isSlot0` 판단을 `removedEntry.Type == state.CurrentProducing.Value`로 변경

**영향 범위:** Host/Client 모두 발생

---

#### BUG-18: 슬롯2 자동생산 취소 시 큐 순서가 변경됨 ✅ 수정 완료 — 정적 분석 PASS (실기 재테스트 필요)

**발견 경위:** TC에 없는 시나리오에서 발견 (2026-03-24 실기, Client에서 확인)

**재현 절차:**
1. Assault 롱프레스 → 자동생산 등록 (슬롯0 Assault)
2. Sniper 롱프레스 → 자동생산 등록 (슬롯0 Assault, 슬롯1 Sniper)
3. Pistoleer 롱프레스 → 자동생산 등록 (슬롯0 Assault, 슬롯1 Sniper, 슬롯2 Pistoleer)
4. Pistoleer 롱프레스 또는 버튼 탭 → Pistoleer 자동생산 취소

**기댓값 (4번):**
- 슬롯0 Assault, 슬롯1 Sniper, 슬롯2 Pistoleer (인디케이터 OFF)
- Pistoleer 인디케이터 OFF, Assault/Sniper 인디케이터 ON
- 큐 순서 변경 없음

**실제 결과 (4번):**
- 슬롯0 Assault, 슬롯1 Pistoleer, 슬롯2 Sniper
- Assault/Sniper 인디케이터 ON (Pistoleer 인디케이터 OFF)
- Sniper와 Pistoleer 위치가 바뀜

**근본 원인:** `UpdateQueueSlots` 렌더링 우선순위 오류
- Pistoleer는 Rule 2에 의해 ManualQueue로 이관됨 (GDD Rule 2 설계대로 정상)
- 그런데 `UpdateQueueSlots`에서 ManualQueue 항목이 AutoEntries 항목보다 무조건 먼저 표시됨
- ManualQueue[0]=Pistoleer → slot1에 배치, AutoEntries[1]=Sniper → slot2로 밀림
- 올바른 렌더링: AutoEntries 항목 먼저 표시(slot1=Sniper), 이후 ManualQueue 항목(slot2=Pistoleer)

**수정 내용:** `ProductionPanelUI.UpdateQueueSlots` slot1~2 렌더링 로직을 pending 목록 방식으로 교체.
- 1단계: isNormalAutoState 여부에 따라 AutoEntries 대기 항목 수집 (AutoIndex+1 또는 AutoIndex+0 기준)
- 2단계: ManualQueue 항목을 그 뒤에 추가
- slot1 = pending[0], slot2 = pending[1]

**영향 범위:** Host/Client 모두 (렌더링 로직 공통)

---

#### BUG-19: ManualQueue 항목이 슬롯에 표시될 때 탭 취소 안됨, 슬롯0 취소 시 큐 순서 역전 ✅ 수정 완료 (2026-03-24)

**발견 경위:** TC에 없는 시나리오에서 발견 (2026-03-24 실기, Host/Client 모두 발생 가능)

**재현 절차:**
1. Assault 롱프레스 → 자동생산 등록
2. Sniper 롱프레스 → 자동생산 등록
3. Pistoleer 롱프레스 → 자동생산 등록 (슬롯0 Assault, 슬롯1 Sniper, 슬롯2 Pistoleer)
4. Pistoleer 롱프레스 → 자동생산 취소 (Rule 2: ManualQueue로 이관)
   - 슬롯0 Assault, 슬롯1 Sniper, 슬롯2 Pistoleer (Assault/Sniper 인디케이터 ON, Pistoleer OFF)
5. 슬롯2 탭

**기댓값 (5번):**
- ManualQueue에서 Pistoleer 제거, 골드 환불, 슬롯2 비어짐

**실제 결과 (5번):**
- 아무 변화 없음 (취소 요청이 서버에서 처리되지 않음)

**근본 원인:** `CancelQueueAt` 자동 모드 슬롯 1~2 분기가 ManualQueue 항목을 처리하지 못함.
- BUG-18 수정 이후 `UpdateQueueSlots`는 pending 목록 방식을 사용 → 슬롯2에 ManualQueue 항목이 올 수 있음
- `CancelQueueAt`은 구 방식(`count < 3` 가드) 유지 → autoCount=2일 때 슬롯2 취소 차단
- ManualQueue 항목을 취소하는 경로가 자동 모드 분기에 없음

**영향 범위:** Host/Client 모두 (서버 측 UseCase 로직)

**파생 현상 (Bug B — 독립 버그):** Bug A와 무관하게 발생. Rule 2로 ManualQueue에 이관된 항목이 있는 상태에서 슬롯0을 탭 취소하면, TryStartNext가 ManualQueue를 AutoEntries보다 우선 선택하여 큐 순서가 역전됨.
- 재현: 1~4번 후 슬롯2 탭 없이 슬롯0 탭
- 기댓값: 슬롯0=Sniper, 슬롯1=Pistoleer (큐 시프트)
- 실제: 슬롯0=Pistoleer, 슬롯1=Sniper (역전)
- 수정 내용: CancelQueueAt(slot=0) 자동 모드에서, 환불 후 TryStartNext에 위임하지 않고 AutoEntries[AutoIndex]를 직접 시작하여 ManualQueue 우선 처리를 우회

---

### BUG-19 정적 분석 — CancelQueueAt pending 로직 + 슬롯0 즉시 시작 검증 (2026-03-24)

수정된 `CancelQueueAt` 로직(Bug A: pending 경로 / Bug B: 슬롯0 즉시 시작)을 9개 케이스에 대해 정적 추적.

#### Bug A — 슬롯1~2 취소 경로 (pendingOffset 기반)

| 케이스 | 전제 상태 | CancelQueueAt 결과 | 판정 |
|--------|----------|-------------------|------|
| TC-A1 | 자동3개(isNormal), slot2 탭 | AutoEntries[2]=Pistoleer 취소, 환불 | ✅ PASS |
| TC-A2 | 자동2+수동1(isNormal), slot2 탭 | ManualQueue[0]=Pistoleer 취소, 환불 | ✅ PASS |
| TC-A3 | 취소상태+수동1(!isNormal), slot2 탭 | ManualQueue[0]=Pistoleer 취소, 환불 | ✅ PASS |
| TC-A4 | 자동1만(isNormal), slot2 탭 | false 반환 (빈 슬롯) | ✅ PASS |

#### Bug B — 슬롯0 취소 후 자동 항목 즉시 시작

| 케이스 | 전제 상태 | 슬롯0 취소 결과 | 판정 |
|--------|----------|----------------|------|
| TC-B1 | 자동2+ManualQueue[Pistoleer], slot0=Assault | CurrentProducing=Sniper 즉시 시작, Pistoleer는 슬롯1 유지 | ✅ PASS |
| TC-B2 | 수동모드, slot0 취소 | Bug B 블록 미개입, 수동 취소 정상 처리 | ✅ PASS |
| TC-B3 | 자동1만, slot0=Assault 취소 | AutoMode OFF, 아무것도 시작 안됨 | ✅ PASS |
| TC-B4 | 자동2개, ManualQueue=[], slot0=Assault | CurrentProducing=Sniper 즉시 시작 | ✅ PASS |
| TC-B5 | Sniper 완료 후 (ManualQueue=[Pistoleer]) | TryStartNext → Pistoleer 선택 | ✅ PASS |

**결론:** Bug A의 pending 경로 분기와 Bug B의 직접 시작 우회 모두 9개 케이스에서 기댓값과 일치. 코드 로직 정확.

---

### BUG-18 정적 분석 — UpdateQueueSlots pending 로직 검증 (2026-03-24)

수정된 `UpdateQueueSlots` 렌더링 로직을 6개 케이스에 대해 정적 추적.

| 케이스 | 전제 상태 | 기댓값 (slot1 / slot2) | 판정 |
|--------|----------|----------------------|------|
| BUG-18 재현 | 자동 Assault(생산중)+Sniper, ManualQueue=[Pistoleer], isNormal=true, autoCount=2 | Sniper / Pistoleer | ✅ PASS |
| 자동1+수동1 | 자동 Assault(생산중), ManualQueue=[Sniper], isNormal=true, autoCount=1 | Sniper / null | ✅ PASS |
| 취소 상태+수동1 | 자동 Sniper(취소됨, !isNormal), ManualQueue=[Sniper], isNormal=false | Sniper / null | ✅ PASS |
| 자동 3개 | 자동 Assault(생산중)+Sniper+Pistoleer, isNormal=true, autoCount=3 | Sniper / Pistoleer | ✅ PASS |
| 자동 2개 | 자동 Assault(생산중)+Sniper, isNormal=true, autoCount=2 | Sniper / null | ✅ PASS |
| 자동 1개 | 자동 Assault(생산중), isNormal=true, autoCount=1 | null / null | ✅ PASS |

**결론:** 수정된 pending 목록 방식이 모든 케이스에서 AutoEntries 우선 → ManualQueue 순서를 올바르게 유지함.

---

### 정적 분석 요약

| TC | 판정 | 핵심 근거 |
|----|------|---------|
| MULTI-1 | PASS | 서버 요청 → 처리 → 동기화 흐름 완결 |
| MULTI-2 | PASS | 2번째 항목 골드 차감 조건 충족 |
| MULTI-3 | PASS | 3번째 항목 골드 차감 조건 충족 (멀티 경로에서도 서버 실행) |
| MULTI-4 | PASS | BUG-14 수정 후 실기 PASS |
| MULTI-5 | PASS | 배럭 ID 기준 격리 보장 |
| MULTI-6 | PASS | 배럭 ID 기준 격리 + 서버 조기 반환 |
| MULTI-7 | PASS | 실기 깜빡임 미발생 확인 |
| MULTI-8 | PASS | BUG-15~19 모두 ✅ 수정완료 + 실기 PASS (2026-03-24) |
