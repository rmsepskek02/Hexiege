# Plan — 이동 슬롯 오프셋 Inspector 조정 기능 추가

작성일: 2026-05-11  
Research: `_Tasks/2026-05-11/10_49_slot-ratio-inspector/Research.md`  
규칙 근거: `Docs/GameSystemRules.md` 규칙 17

---

## 이 작업이 무엇인지

타일 내 유닛 이동 슬롯의 오프셋 수치(앞뒤 비율, 좌우 비율)를
Unity 인스펙터에서 실시간으로 조정할 수 있도록 변경한다.

GameSystemRules.md 규칙 17에 "슬롯 오프셋 수치는 Inspector에서 조정 가능하다"고 명시되어 있으나,
현재 구현은 코드 상수로 고정되어 있어 이를 충족하지 못하는 상태다.

---

## 변경 파일 및 내용

### 1. TileMoveSlotManager.cs

**변경 전:**
```csharp
private const float SlotForwardRatio = 0.30f;
private const float SlotSideRatio = 0.30f;

public TileMoveSlotManager() { }
```

**변경 후:**
```csharp
// 인스턴스 필드로 전환 — 생성 시 외부에서 값을 주입받는다.
// 기본값 0.30f는 Inspector에서 따로 설정하지 않았을 때의 폴백.
private readonly float SlotForwardRatio;
private readonly float SlotSideRatio;

/// <summary>
/// 슬롯 오프셋 비율을 받아 초기화한다.
/// forwardRatio : 슬롯 1번이 타일 중심에서 성 방향(앞)으로 얼마나 떨어지는지 (0~0.5 권장).
/// sideRatio    : 슬롯 2·3번이 타일 중심에서 좌/우로 얼마나 떨어지는지 (0~0.5 권장).
/// </summary>
public TileMoveSlotManager(float forwardRatio = 0.30f, float sideRatio = 0.30f)
{
    SlotForwardRatio = forwardRatio;
    SlotSideRatio    = sideRatio;
}
```

파일 헤더 주석의 비율 값도 동적 값으로 안내하도록 수정한다.

---

### 2. GameBootstrapper.cs

Inspector 노출용 SerializeField 2개 추가:
```csharp
[Header("이동 슬롯 오프셋 (TileMoveSlotManager)")]
[Tooltip("슬롯 1번(앞)이 타일 중심에서 성 방향으로 떨어지는 비율. 0~0.5 권장.")]
[SerializeField] private float _slotForwardRatio = 0.30f;

[Tooltip("슬롯 2·3번(뒤좌·뒤우)이 타일 중심에서 좌/우로 떨어지는 비율. 0~0.5 권장.")]
[SerializeField] private float _slotSideRatio = 0.30f;
```

생성자 호출에 인자 전달:
```csharp
// 변경 전:
_moveSlotManager = new TileMoveSlotManager();

// 변경 후:
_moveSlotManager = new TileMoveSlotManager(_slotForwardRatio, _slotSideRatio);
```

---

## 위험 요소

| 위험 | 대응 |
|---|---|
| Inspector 기본값과 기존 const 값이 다를 경우 동작 변화 | 기본값을 기존과 동일한 0.30f로 유지하므로 동작 변화 없음 |
| 게임 중 값 변경 (Play 도중 Inspector 수정) | TileMoveSlotManager는 생성 시 1회만 값을 받으므로 런타임 중 변경은 적용 안 됨. 플레이 시작 전에 설정해야 함 |

---

## 구현 순서

1. `TileMoveSlotManager.cs` — const 제거 + 인스턴스 필드 + 생성자 파라미터 추가
2. `GameBootstrapper.cs` — SerializeField 추가 + 생성자 인자 전달
