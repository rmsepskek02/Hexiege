# Research: 재경기 초기화 버그

작성일: 2026-04-03
상태: ✅ 해결 완료 (2026-04-04)

---

## 증상

멀티플레이에서 재경기(Rematch) 시 이전 게임의 유닛들이 화면에 그대로 남아있음.
새 게임이 시작되어도 이전 게임의 유닛 GameObject가 씬에 존재.

---

## 재경기 흐름 (현재 구조)

```
양측 재경기 동의
  → NetworkGameEndController.StartRematch()
  → NGO SceneManager.LoadScene("Game")
  → 씬 리로드: 모든 NetworkObject Despawn → 새 씬 로드 → 재스폰
  → GameBootstrapper.LoadMap() 호출
  → 새로운 UseCase 인스턴스 생성
```

NGO Scene Management = ON이므로 씬 리로드 시 NetworkObject가 자동으로 정리되어야 함.

---

## 추정 원인

### 가설 1 — NGO 전환 과정에서 유닛 스폰 방식 불일치
NGO NetworkObject 전환 이전에는 유닛이 일반 GameObject로 관리됐을 수 있음.
전환 과정에서 일부 유닛이 NetworkObject가 아닌 일반 GameObject로 남아있다면,
NGO 씬 리로드 시 자동 정리 대상에서 제외됨.

**확인 필요:** 모든 유닛 프리팹에 NetworkObject 컴포넌트가 부착되어 있는지.

### 가설 2 — UnitFactory._unitObjects 딕셔너리 잔존
UnitFactory는 GameBootstrapper가 new로 생성하므로 재경기 시 새 인스턴스가 만들어짐.
그러나 씬에 남아있는 GameObject(Destroy되지 않은 유닛)가 있다면 메모리 누수.

**확인 필요:** 재경기 시 이전 유닛 GameObject가 실제로 Destroy되는지.

### 가설 3 — DontDestroyOnLoad 오브젝트
LoadingScreen 등 DontDestroyOnLoad가 적용된 오브젝트 중 유닛과 관련된 것이 있는지.

**확인 필요:** DontDestroyOnLoad가 적용된 모든 오브젝트 목록.

### 가설 4 — NetworkObject 스폰 순서 문제
재경기 씬 로드 시 이전 NetworkObject가 Despawn 완료되기 전에 새 씬의 오브젝트가 스폰되어 중복 발생.

---

## 코드 분석 결과 (2026-04-04)

### GameBootstrapper — 씬과 함께 파괴되는 일반 MonoBehaviour

`GameBootstrapper`는 `MonoBehaviour`를 상속하며 `DontDestroyOnLoad`가 없음.
씬 재로드 시 파괴되고 새 인스턴스가 생성됨.

### 재경기 시 실제 코드 흐름

```
StartRematch()
  → NetworkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single)
  → 씬 재로드
  → 기존 GameBootstrapper 파괴 → 새 GameBootstrapper 생성
  → 새 UnitFactory 생성 (_unitObjects = 빈 딕셔너리)
  → LoadMap() → ClearAll() → DestroyAllUnits()
      ← _unitObjects가 비어있으므로 아무것도 Destroy하지 않음
```

**결론: 유닛 정리는 전적으로 NGO의 자동 Despawn에만 의존하는 구조.**

### NGO 동적 NetworkObject 자동 Despawn 미보장

유닛은 `NetworkObject.Spawn()`으로 동적 스폰된 오브젝트.
NGO SceneManager.LoadScene(Single)으로 **같은 씬을 재로드**할 경우,
동적 스폰 NetworkObject가 자동으로 Despawn된다는 보장이 없음.

씬 전환과 달리 씬 재초기화 시나리오에서는 NGO가 이를 명시적으로 처리하지 않을 수 있음.

### NetworkObject Inspector 확인 결과

유닛 프리팹 NetworkObject 설정:
- `Active Scene Synchronization`: 체크(ON)으로 변경 후 테스트 → **증상 동일 (미해결)**
- 이 설정은 씬 전환 시 오브젝트를 새 씬으로 이동시키는 용도이며,
  같은 씬 재로드 시나리오에서는 관련 없음.

### 확정된 원인

`StartRematch()` → `LoadScene()` 호출 전에 **유닛/건물을 명시적으로 Despawn하지 않음**.
NGO 자동 정리에만 의존 → 같은 씬 재로드 시 이전 게임 유닛 GameObject가 씬에 잔존.

---

## 우선순위

보통 — 전투 시스템 안정화 완료 후 진행. Plan.md 참조.
