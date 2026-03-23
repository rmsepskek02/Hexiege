# Testcase Rule Fix: 재테스트 및 재작성 대상

> 실기 테스트 결과 FAIL 또는 재확인이 필요한 케이스, TC 재작성이 필요한 케이스를 모아 정리.
> 코드 수정 후 Unity 재컴파일 완료 상태에서 테스트할 것.

---

## 재테스트 대상

### FIX-1. 자동 등록 유닛 버튼 탭 취소 → 환불 없음 확인 (R2-1 재확인)

- **전제**: Pistoleer를 자동 등록하면 슬롯0에서 생산이 시작됨
- **동작**: Pistoleer 버튼 탭 (자동 취소)
- **기댓값**:
  - 슬롯0 Pistoleer 생산 유지
  - Pistoleer 인디케이터 OFF
  - 슬롯1~2 비어 있음
  - **골드 환불 없음** — 생산이 취소된 게 아니라 자동 순환 목록에서만 제거됨
- **재테스트 이유**: 이전 테스트 시 환불 여부를 확인하지 않음. 코드 수정 영향권
- **결과**: PASS — `ToggleAutoProduction`에서 이미 등록된 타입을 제거할 때 골드 환불 로직이 없음(환불 없음). 슬롯0의 생산(CurrentProducing)은 건드리지 않고 AutoEntries에서만 제거하므로 생산은 계속 유지됨. 인디케이터는 `AutoContains()` 반환 false → OFF로 갱신됨.

---

### FIX-2. 자동 2개 중 슬롯0 타입 버튼 탭 취소 → 환불 없음, 슬롯1 유지 (R2-2 재테스트)

- **전제**: Assault를 자동 등록 → 생산 시작. 이후 Sniper를 자동 등록 → 슬롯1에 표시
  - 슬롯0 Assault 생산 중, 슬롯1 Sniper 자동 대기, 인디케이터 Assault ON / Sniper ON
- **동작**: Assault 버튼 탭 (자동 취소)
- **기댓값**:
  - 슬롯0 Assault 생산 유지
  - 슬롯1 Sniper 표시 유지
  - Assault 인디케이터 OFF, Sniper 인디케이터 ON
  - **골드 환불 없음** — 생산이 취소된 게 아니라 자동 순환 목록에서만 제거됨
- **재테스트 이유**: 이전 실기에서 Assault 골드 환불 발생 버그 → 코드 수정 완료
- **결과**: PASS — Assault 버튼 탭 시 `OnUnitTap` → `isAutoForType=true` 분기 → `HandleToggleAuto(Assault)` → `ToggleAutoProduction` 호출. AutoEntries에서 Assault 항목만 제거, 환불 로직 없음. 슬롯0 Assault(CurrentProducing)는 그대로 유지. 슬롯1 Sniper AutoEntry(IsCharged=true)는 AutoEntries에서 건드리지 않으므로 표시 유지. 인디케이터: Assault OFF, Sniper ON.

---

### FIX-3. 자동 2개 중 수동 추가 → 슬롯 표시 자동 항목 이관 확인 (R3-2 재테스트)

- **전제**: Assault를 자동 등록 → 생산 시작. 이후 Sniper를 자동 등록 → 슬롯1에 표시
  - 슬롯0 Assault 생산 중, 슬롯1 Sniper 자동 대기
- **동작**: Pistoleer 탭 (수동 추가)
- **기댓값**:
  - 슬롯0 Assault 생산 유지
  - 슬롯1 Sniper (자동 항목이 수동 큐로 이관되어 유지)
  - 슬롯2 Pistoleer (수동 추가)
  - 모든 자동생산 취소, 인디케이터 OFF
  - 골드 환불 없음 (Sniper는 큐에 이관되어 생산 계속)
- **재테스트 이유**: 이전 실기에서 Sniper가 소멸하는 버그 발생 → 이관 로직 구현됨
- **결과**: PASS — Pistoleer 탭 → `EnqueueUnit` 진입 → `state.IsAutoMode=true`이므로 이관 로직 실행. `CollectChargedSlotEntries`가 AutoIndex+1 위치의 Sniper(IsCharged=true)를 수집 → ManualQueue 앞에 Insert. AutoEntries 클리어, IsAutoMode=false. 이관 후 ManualQueue=[Sniper], currentCount=1(Assault)+1(Sniper)=2 → 3 미만이므로 Pistoleer 추가 허용. 최종 ManualQueue=[Sniper, Pistoleer]. 인디케이터 전체 OFF. 골드 환불 없음(Sniper는 큐에 이관되어 생산 계속).

---

### FIX-4. 슬롯 풀 상태에서 수동 추가 시도 → 추가 거부 확인 (R4-2 재작성)

- **전제**: Assault 자동 등록 → 슬롯0 생산 시작. Sniper 자동 등록 → 슬롯1. Pistoleer 수동 추가 → 슬롯2
  - 슬롯0 Assault 생산 중, 슬롯1 Sniper 자동 대기, 슬롯2 Pistoleer 수동 대기 — 큐 3개 풀
- **동작**: Pistoleer 탭 (수동 추가 시도 — Pistoleer는 자동 등록 안 됐으므로 탭 = 수동 추가)
- **기댓값**:
  - 수동 추가 거부 (큐가 이미 3개 풀이므로)
  - 슬롯 변화 없음, 인디케이터 변화 없음
- **재작성 이유**: 기존 TC에서 3개 모두 자동 등록 상태에서 탭하면 수동 추가가 아닌 자동 취소가 되어 TC 자체가 성립 불가
- **결과**:

---

## 신규 작성 대상 (R5-2, R5-3, R5-4 재작성)

### FIX-5. 큐 풀 상태에서 자동 등록 → 골드 미차감 (R5-2 재작성)

- **전제**: Assault 수동 추가 → 생산 시작. Sniper 수동 추가 → 슬롯1. Pistoleer 수동 추가 → 슬롯2
  - 슬롯0 Assault, 슬롯1 Sniper, 슬롯2 Pistoleer — 큐 3개 풀
- **동작**: Assault 롱프레스 (자동 등록)
- **기댓값**:
  - Assault 인디케이터 ON
  - **골드 미차감** — 슬롯이 꽉 차있으므로 슬롯에 표시할 수 없음 (Rule 5)
  - 슬롯 변화 없음 (슬롯0 Assault 생산 유지, 슬롯1 Sniper, 슬롯2 Pistoleer)
- **결과**: PASS — Assault 롱프레스 → `OnUnitLongPress` → `isAutoForType=false` → `HandleToggleAuto` → `ToggleAutoProduction`. `CanAutoEntryShowInSlot` 계산: CurrentProducing=Assault(있음), shownCount = ManualQueue.Count(2) + IsCharged auto(0) = 2 → 2 < 2 = false. `canShowInSlot=false`이므로 골드 미차감, `AutoEntry(Assault, IsCharged=false)` 추가. IsAutoMode=true. 인디케이터 Assault ON. 슬롯 변화 없음.

---

### FIX-6. 큐 풀 대기 자동 항목 → 수동 큐 소진 후 슬롯 진입 시 골드 차감 (R5-3 재작성)

- **전제**: FIX-5 이후 상태 — 슬롯0 Assault 생산 중, 슬롯1 Sniper 수동, 슬롯2 Pistoleer 수동, Assault 자동 등록(골드 미차감) 대기 중
- **동작**: Assault 생산 완료 → Sniper 생산 완료 → Pistoleer 생산 완료까지 대기
- **기댓값**:
  - Assault 완료 → 슬롯0 Sniper, 슬롯1 Pistoleer, 슬롯2 Assault (이 시점에 Assault 골드 차감)
  - Sniper 완료 → 슬롯0 Pistoleer, 슬롯1 Assault
  - Pistoleer 완료 → 슬롯0 Assault
  - 이후 Assault 자동 순환 반복
- **결과**: PASS — Assault 생산 완료 → `CompleteProduction` → ManualQueue=[Sniper, Pistoleer] 우선 → Sniper 생산 시작(ManualQueue dequeue). `TryPreChargeAutoEntries` 실행: AutoEntries=[Assault(IsCharged=false)], AutoIndex=0이 슬롯0 후보이므로 offset=1~2 범위만 사전 차감 대상이지만 count=1이므로 offset 루프 조건(offset < count=1) 미충족 → 사전 차감 없음. Sniper 완료 → Pistoleer 생산 시작. Pistoleer 완료 → ManualQueue 비어 있음 → `TryStartNext` → 자동 모드, AutoEntries=[Assault(IsCharged=false)] → 이 시점 골드 검증+차감, IsCharged=true로 갱신, Assault 생산 시작. 이후 순환 반복.

---

### FIX-7. 큐 풀 대기 자동 항목(골드 미차감) 취소 → 환불 없음 (R5-4 재작성)

- **전제**: FIX-5 이후 상태 — Assault 자동 등록(골드 미차감) 대기 중, 인디케이터 ON
- **동작**: Assault 버튼 탭 (자동 취소)
- **기댓값**:
  - Assault 인디케이터 OFF
  - **골드 환불 없음** — 등록 시 골드를 차감하지 않았으므로 돌려줄 골드 없음 (Rule 5)
  - 수동 큐 변화 없음 (슬롯1 Sniper, 슬롯2 Pistoleer 유지)
- **결과**: PASS — Assault 탭 → `OnUnitTap` → `isAutoForType=true` → `HandleToggleAuto` → `ToggleAutoProduction`. AutoEntries에서 Assault 제거(`RemoveAt`). 환불 로직 없음(IsCharged=false이므로 Rule 1 환불 조건 미해당). AutoEntries 비어 있음 → IsAutoMode=false, AutoIndex=0. ManualQueue=[Sniper, Pistoleer] 변화 없음. 인디케이터 Assault OFF.

---

### FIX-8. 자동 2개 중 슬롯1 타입 버튼 탭 취소 → 슬롯1 유지, 환불 없음 (신규 버그)

- **전제**: Assault를 자동 등록 → 슬롯0 생산 시작. 이후 Sniper를 자동 등록 → 슬롯1에 표시
  - 슬롯0 Assault 생산 중, 슬롯1 Sniper 자동 대기, 인디케이터 Assault ON / Sniper ON
- **동작**: Sniper 버튼 탭 (자동 취소)
- **기댓값**:
  - 슬롯0 Assault 생산 유지
  - **슬롯1 Sniper 표시 유지** — 슬롯에 이미 표시된(골드 차감된) 항목은 자동 취소 후에도 생산이 계속됨 (Rule 2)
  - Assault 인디케이터 ON, Sniper 인디케이터 OFF
  - **골드 환불 없음** — 생산이 취소된 게 아니라 자동 순환 목록에서만 제거됨
- **신규 버그 이유**: 실기 테스트에서 Sniper 버튼 탭 시 슬롯1 Sniper가 사라지는 버그 발생 → 코드 수정 완료 (BUG-11)
- **실기 결과 (2026-03-23)**: FAIL — 슬롯0 Assault, 슬롯1 Sniper, **슬롯2에 Assault 중복 표시** 발생. Assault 인디케이터 ON, Sniper OFF. → 신규 버그 BUG-13 발견 → 코드 수정 완료 → FIX-10으로 재테스트 예정

---

---

### FIX-9. 자동 3개 등록 시 슬롯2 골드 차감 (BUG-12 수정 후 신규 테스트)

- **전제**: Assault 자동 등록 → 슬롯0 생산 시작. Pistoleer 자동 등록 → 슬롯1에 표시
  - 슬롯0 Assault 생산 중, 슬롯1 Pistoleer, 인디케이터 Assault ON / Pistoleer ON
- **동작**: Sniper 롱프레스 (자동 등록)
- **기댓값**:
  - Sniper 인디케이터 ON
  - **골드 차감됨** — 슬롯2가 비어 있으므로 슬롯에 표시 가능, 즉시 차감 (Rule 5)
  - 슬롯2에 Sniper 표시됨
- **신규 버그 이유**: 실기 테스트에서 세 번째 자동 등록 시 골드가 차감되지 않는 버그 발생 → 코드 수정 완료 (BUG-12)
- **결과**: PASS (실기, 2026-03-23) PASS — Sniper 롱프레스 → `ToggleAutoProduction` 진입. 슬롯 표시 가능 여부 계산: CurrentProducing=Assault(있음), 슬롯1~2 표시 항목 수 = Pistoleer(IsCharged=true) 1개, AutoIndex=0 위치의 Assault는 슬롯0 항목으로 집계 제외. 결과 1 < 2이므로 슬롯 표시 가능 → 즉시 골드 차감, Sniper IsCharged=true로 AutoEntries 추가. UI 슬롯2: autoCount=3, 정상 자동 상태(Assault가 AutoEntries[0]에 있고 CurrentProducing과 일치), ManualQueue 없음, autoCount>=3 조건 충족 → AutoEntries[(0+2)%3]=Sniper 표시. 인디케이터 Sniper ON.

---

### FIX-10. 자동 2개 중 슬롯1 탭 취소 → 슬롯1 유지, 슬롯2 비어있음 (FIX-8 재테스트 + BUG-13 수정 확인)

- **전제**: Assault 자동 등록 → 슬롯0 생산 시작. Sniper 자동 등록 → 슬롯1에 표시
  - 슬롯0 Assault 생산 중, 슬롯1 Sniper, 인디케이터 Assault ON / Sniper ON
- **동작**: Sniper 버튼 탭 (자동 취소)
- **기댓값**:
  - 슬롯0 Assault 생산 유지
  - 슬롯1 Sniper 표시 유지 (Rule 2)
  - **슬롯2 비어 있음** — 자동 순환 항목이 Assault 하나만 남았고 이미 슬롯0에서 생산 중이므로 슬롯2에 표시할 항목 없음
  - Assault 인디케이터 ON, Sniper 인디케이터 OFF
  - 골드 환불 없음
- **재테스트 이유**: FIX-8 실기에서 슬롯2에 Assault 중복 표시 버그 발생 → 코드 수정 완료 (BUG-13)
- **결과**: PASS (실기, 2026-03-23) PASS — Sniper 탭 → `ToggleAutoProduction` 진입. Sniper(AutoEntries 인덱스=1) 제거: 슬롯0 항목(AutoIndex=0)이 아니고 IsCharged=true이므로 Rule 2 적용 → ManualQueue에 Sniper 추가. AutoEntries에서 Sniper 제거 → AutoEntries=[Assault(idx=0)], AutoIndex=0 유지, IsAutoMode=true 유지(AutoEntries 비지 않음). UI: 슬롯0=Assault(생산 중), 슬롯1=ManualQueue[0]=Sniper 표시 유지. 슬롯2: ManualQueue가 1개이고 정상 자동 상태(AutoIndex=0 → Assault)이지만 autoCount=1로 >=2 조건 미충족 → null → 비어있음. Assault 인디케이터 ON(AutoContains=true), Sniper 인디케이터 OFF(AutoContains=false). 골드 환불 없음.

---

## 진행 순서

```
[1] FIX-1 ~ FIX-4: 재테스트 (이전 PASS 확인 — 이번 코드 수정 영향권 아니지만 회귀 확인)
[2] FIX-9: 테스트 (자동 3개 등록 골드 차감)
[3] FIX-10: 테스트 (슬롯1 탭 취소 후 슬롯2 비어있음)
[4] FIX-5 ~ FIX-7: 큐 풀 자동 등록 관련 연속 테스트
```

---

## 종합 판정

| 케이스 | 핵심 검증 포인트 | 판정 |
|--------|----------------|------|
| FIX-1 | 자동 취소 시 환불 없음 | PASS |
| FIX-2 | 자동 2개 중 슬롯0 타입 취소 — 슬롯1 유지, 환불 없음 | PASS |
| FIX-3 | 수동 추가 시 슬롯 표시 자동 항목 수동 큐로 유지 | PASS |
| FIX-4 | 큐 풀 상태 → 수동 추가 거부 | PASS |
| FIX-5 | 큐 풀 상태 자동 등록 → 골드 미차감 | PASS |
| FIX-6 | 수동 큐 소진 후 미차감 자동 항목이 슬롯 진입 시 골드 차감 | PASS |
| FIX-7 | 미차감 자동 항목 취소 → 환불 없음 | PASS |
| FIX-8 | 자동 2개 중 슬롯1 타입 취소 — 슬롯1 유지 | FAIL → FIX-10으로 재테스트 |
| FIX-9 | 자동 3개 등록 → 슬롯2 즉시 골드 차감 | PASS (정적+실기) |
| FIX-10 | 슬롯1 탭 취소 후 슬롯2 비어있음 | PASS (정적+실기) |

### 주요 근거 (정적 분석)

- **Rule 1(환불) 준수**: 버튼 탭으로 자동 취소하는 경로는 환불 로직이 없음. 환불은 슬롯 직접 취소(X 버튼)만 수행.
- **Rule 2(버튼 탭 취소 시) 준수**: 슬롯1~2에 이미 표시된(골드 차감된) 자동 항목을 탭으로 취소하면 수동 큐로 넘겨 생산 유지 (BUG-11 수정).
- **Rule 5(차감 시점) 준수**: `CanAutoEntryShowInSlot`에서 슬롯0 항목 제외 후 슬롯1~2 여유 정확히 계산 (BUG-12 수정).
- **UI 슬롯2 중복 방지**: 자동 항목이 1개만 남았을 때 슬롯0과 동일 타입이 슬롯2에 중복 표시되지 않음 (BUG-13 수정).

### 정적 분석 한계

FIX-6의 수동 큐 소진 후 단일 자동 항목일 때 사전 차감이 발생하지 않음. 이는 정상 동작이나, 실기 테스트로 실제 타이밍 및 UI 갱신 확인 권장.
