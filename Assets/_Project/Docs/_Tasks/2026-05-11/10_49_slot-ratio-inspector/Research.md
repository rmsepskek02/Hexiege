# Research — 이동 슬롯 오프셋 Inspector 조정 기능 추가

작성일: 2026-05-11  
작업명: slot-ratio-inspector

---

## 이 작업이 무엇인지

현재 타일 내 유닛 분산 위치(이동 슬롯)는 코드에 상수로 고정되어 있어,
값을 바꾸려면 코드를 수정하고 다시 빌드해야 한다.
이 작업은 해당 수치를 Unity 인스펙터에서 실시간으로 바꿀 수 있도록 변경하는 것이다.

---

## 현재 상태

### TileMoveSlotManager.cs — 슬롯 위치 계산

```
Assets/_Project/Scripts/Application/Services/TileMoveSlotManager.cs
```

슬롯 위치는 두 개의 `private const float`로 결정된다.

| 상수 | 값 | 의미 |
|---|---|---|
| `SlotForwardRatio` | `0.30f` | 슬롯 1번이 타일 중심에서 앞쪽(성 방향)으로 얼마나 떨어지는지 |
| `SlotSideRatio` | `0.30f` | 슬롯 2·3번이 타일 중심에서 좌/우로 얼마나 떨어지는지 |

슬롯 위치 계산 (GetSlotWorldPositionInternal):
- 슬롯 1: `center + forward × (SlotForwardRatio × TileWidth)`
- 슬롯 2: `center - forward × (SlotForwardRatio × TileWidth) + perpLeft × (SlotSideRatio × TileWidth)`
- 슬롯 3: `center - forward × (SlotForwardRatio × TileWidth) - perpLeft × (SlotSideRatio × TileWidth)`

문제: `TileMoveSlotManager`는 순수 C# 클래스(`MonoBehaviour` 아님)라서
`[SerializeField]`를 직접 붙일 수 없다.

### GameBootstrapper.cs — TileMoveSlotManager 생성 위치

```
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs  (라인 850~851)
```

```csharp
if (_moveSlotManager == null)
    _moveSlotManager = new TileMoveSlotManager();  // 현재: 생성자 인자 없음
```

`GameBootstrapper`는 `MonoBehaviour`이므로 `[SerializeField]`를 사용할 수 있다.

---

## 영향 범위

| 파일 | 변경 종류 |
|---|---|
| `TileMoveSlotManager.cs` | `const` 제거 → 인스턴스 필드로 전환 + 생성자 파라미터 추가 |
| `GameBootstrapper.cs` | `[SerializeField]` 필드 2개 추가 + 생성자 호출에 인자 전달 |

다른 파일은 변경 없음. `TileMoveSlotManager` 생성자 호출은 GameBootstrapper 한 곳뿐이다.
