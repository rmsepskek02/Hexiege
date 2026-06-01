# Research: 리팩토링 회귀 점검

## 이 문서가 다루는 것

이 문서는 2026-05-19~24에 완료된 대규모 코드 리팩토링 작업이 이후 추가 작업들에 의해 어떤 영향을 받았는지 전체적으로 점검한 결과를 담고 있습니다.

쉽게 말하면 이렇습니다.

- 리팩토링 당시 "이렇게 고쳤다"고 기록해 둔 내용이 현재 코드에도 그대로 살아있는지 사진을 찍어 비교하는 작업입니다.
- 이후 다른 기능들이 추가되면서 의도치 않게 리팩토링 결과를 되돌려 버린 부분이 있는지 찾아내는 것이 목적입니다.

점검 기준은 [이전 리팩토링 Plan.md](../../2026-05-19/10_46_code-refactoring/Plan.md)에서 "✅ 전체 완료 — 2026-05-24"로 표시된 7개 그룹의 완료 조건입니다.

---

## 1. 점검 결과 요약

| 항목 | 상태 | 설명 |
|------|------|------|
| 슬롯/점유 시스템 잔재 삭제 | ✅ PASS | AttackPositionManager, TileOccupancyManager, ProductionPopupDiagnostic 모두 삭제 유지 |
| IHexCoordinateMapper 인터페이스 유지 | ✅ PASS | 인터페이스 파일 존재, Application 3개 UseCase의 Core 직접 의존 없음 |
| Presentation → NGO 의존 제거 | ✅ PASS | Presentation 폴더 전체에서 using Unity.Netcode 없음 |
| Infrastructure → Presentation 의존 제거 | ✅ PASS | NetworkCombatController, NetworkGameEndController 에서 using Hexiege.Presentation 없음 |
| **FindFirstObjectByType 캐시화** | ⚠️ **REGRESSION** | 3개 파일에서 OnNetworkSpawn 외부 호출 발견 |
| GameBootstrapper partial class 분리 | ✅ PASS | .cs / .Setup.cs / .Map.cs / .Network.cs 4개 파일 존재 |
| PopulationUseCase 이벤트 캐시 구조 | ✅ PASS | _usedPopulationByTeam 딕셔너리, CompositeDisposable, Dispose() 모두 존재 |
| 리팩토링 이후 신규 파일 아키텍처 위반 여부 | ✅ PASS | 신규 추가 파일에서 동일 패턴 위반 없음 |

**결론: 7개 항목 PASS, 1개 항목 REGRESSION 확인.**

---

## 2. 회귀 항목 상세 분석

### REGRESSION: FindFirstObjectByType 캐시화 — 3개 파일

**배경**
리팩토링 그룹 4에서 씬 전체를 순회하는 `FindFirstObjectByType<GameBootstrapper>()` 호출이 30회 이상 반복되던 문제를 해결했습니다. 각 NetworkBehaviour의 OnNetworkSpawn에서 단 1회만 호출하고 이후에는 캐시된 값을 사용하는 패턴으로 통일했습니다.

**현재 상태**
리팩토링 이후 추가/수정된 코드에서 이 패턴을 따르지 않는 3개 위치가 확인되었습니다.

---

#### 회귀 항목 1: NetworkUnit.cs — RegisterToFactory 메서드 (라인 178)

```
위치: Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs
문제: RegisterToFactory() 메서드 내부에서 FindFirstObjectByType<GameBootstrapper>() 호출
상황: OnValueChanged 콜백 내에서 실행되므로, 유닛이 생성될 때마다 씬 탐색 발생
```

**왜 문제인가**: NetworkUnit은 유닛 수만큼 인스턴스가 생성됩니다. 유닛이 10마리면 10회, 전투가 길어질수록 누적 호출 횟수가 증가합니다. 모바일에서는 씬 전체 탐색이 프레임 부하로 직결됩니다.

**권장 수정 방향**: NetworkUnit도 OnNetworkSpawn에서 GameBootstrapper를 1회 캐시하고, RegisterToFactory 호출 시 캐시된 값 사용.

---

#### 회귀 항목 2: NetworkGameEndController.cs — OnGameEndServer 메서드 (라인 160)

```
위치: Assets/_Project/Scripts/Infrastructure/Network/NetworkGameEndController.cs
문제: OnGameEndServer() 이벤트 핸들러 내부에서 FindFirstObjectByType<NetworkGameManager>() 호출
상황: 게임 종료 이벤트가 발생할 때마다 씬 탐색 실행
```

**왜 문제인가**: 게임 종료는 1회성이므로 성능 영향은 미미하지만, 리팩토링 당시 확립한 "이벤트 핸들러 내부에서 FindFirst 금지" 패턴과 일관성이 깨집니다. NetworkGameEndController는 이미 리팩토링을 통해 UI 직접 참조를 전부 제거한 파일이므로, 이 위반이 더욱 이질적입니다.

**권장 수정 방향**: OnNetworkSpawn에서 NetworkGameManager를 캐시하거나, GameBootstrapper 통해 접근.

---

#### 회귀 항목 3: ReconnectionHandler.cs — WaitAndForceWin 코루틴 (라인 188)

```
위치: Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs
문제: WaitAndForceWin 코루틴 내부에서 FindFirstObjectByType<NetworkGameEndController>() 호출
상황: 재연결 대기 후 강제 승리 처리 시 씬 탐색 실행
```

**왜 문제인가**: 재연결 흐름 자체가 비정상 경로이므로 호출 빈도는 낮지만, 리팩토링 패턴 일관성 측면에서 수정이 필요합니다. OnNetworkSpawn에서 미리 캐시하기 어려운 구조라면 GameBootstrapper에서 GetNetworkGameEndController()로 접근하는 것이 대안입니다.

**권장 수정 방향**: OnNetworkSpawn에서 NetworkGameEndController 캐시. 또는 GameBootstrapper 통해 접근.

---

## 3. 영향 범위 평가

### 기능 영향

| 항목 | 평가 |
|------|------|
| 현재 게임 크래시 가능성 | 없음 — 탐색 결과가 항상 null이 아닌 유효한 객체를 반환하므로 기능 자체는 정상 동작 |
| 멀티플레이 동기화 | 영향 없음 |
| 싱글플레이 동작 | 영향 없음 (싱글플레이는 NetworkBehaviour 미사용) |

### 성능 영향

| 항목 | 평가 |
|------|------|
| NetworkUnit.cs | 유닛 수에 비례한 씬 탐색 비용 누적. 유닛 10마리 이상에서 영향 시작. 모바일 주의 대상 |
| NetworkGameEndController.cs | 게임 종료 1회 호출이므로 성능 영향 미미 |
| ReconnectionHandler.cs | 재연결 시에만 실행되므로 성능 영향 미미 |

### 아키텍처 영향

리팩토링으로 확립한 "OnNetworkSpawn 단일 캐시 패턴" 일관성이 깨집니다. 향후 신규 NetworkBehaviour 작성 시 참고할 코드 예시가 혼재되는 문제가 생깁니다.

---

## 4. 총평

전반적으로 리팩토링 결과가 매우 잘 유지되고 있습니다. 이후 추가된 기능들도 리팩토링 패턴을 준수하고 있으며, 가장 중요한 아키텍처 위반(Application → Core, Presentation → NGO, Infrastructure → Presentation) 제거 결과는 완벽히 유지됩니다.

회귀된 항목은 FindFirstObjectByType 3건으로 범위가 작고 수정 방법도 명확합니다. Plan.md에서 수정 방법을 구체화합니다.
