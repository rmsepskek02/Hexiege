# Plan — Production 잠금 유닛 Lock Icon 표시

잠긴 유닛 버튼에 두 가지 시각적 변화를 동시에 적용한다.
첫째, 초상화를 어둡게 디밍해서 "생산할 수 없는 유닛"임을 색감으로 전달한다.
둘째, 버튼 우측 하단에 자물쇠 아이콘 배지를 표시해서 잠금 상태임을 명확히 알린다.
코드 변경은 `UpdateLockIndicators()` 한 곳에만 집중되고, 나머지는 Unity Editor Inspector 작업이다.

---

## 변경 항목 1. [코드] UpdateLockIndicators() 초상화 디밍 추가

**파일**: [ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs)  
**대상 메서드**: `UpdateLockIndicators()` (line 703)

### 현재 코드
```csharp
private void UpdateLockIndicators()
{
    if (_unitLockIndicators == null) return;
    for (int i = 0; i < _unitLockIndicators.Count; i++)
    {
        if (_unitLockIndicators[i] == null) continue;
        bool show = i < _activeUnitLocks.Count
                    && i < _activeUnitTypes.Count
                    && _activeUnitLocks[i];
        _unitLockIndicators[i].SetActive(show);
    }
}
```

### 변경 후 코드
```csharp
private void UpdateLockIndicators()
{
    if (_unitLockIndicators == null) return;
    for (int i = 0; i < _unitLockIndicators.Count; i++)
    {
        if (_unitLockIndicators[i] == null) continue;

        // 잠금 여부 판정: _activeUnitLocks[i]가 true이면 해금 단계 미달 유닛
        bool locked = i < _activeUnitLocks.Count
                      && i < _activeUnitTypes.Count
                      && _activeUnitLocks[i];

        // 자물쇠 아이콘 오버레이 표시/숨김
        _unitLockIndicators[i].SetActive(locked);

        // 초상화 디밍: 잠금 상태면 어둡게, 해금 상태면 원래 색으로 복원
        // _unitButtonPortraits가 설정되지 않았거나 슬롯이 없는 경우를 안전하게 처리
        if (_unitButtonPortraits != null && i < _unitButtonPortraits.Count && _unitButtonPortraits[i] != null)
        {
            // 0.35f는 약 35% 밝기로 유닛 실루엣을 희미하게 유지하는 값
            _unitButtonPortraits[i].color = locked
                ? new Color(0.35f, 0.35f, 0.35f, 1f)
                : Color.white;
        }
    }
}
```

**근거**: GameSystemRules.md "공통 UI 규칙" — UI 요소의 상태 표현은 시각적으로 명확해야 하며, 색상 변화는 즉각적인 피드백 수단으로 활용한다.

---

## 변경 항목 2. [Inspector] 잠금 인디케이터 GameObject에 자물쇠 아이콘 Image 추가

**작업 위치**: 씬에서 ProductionPopup 프리팹(또는 씬 오브젝트) → 각 유닛 버튼의 LockIndicator 하위

### 구조 예시
```
UnitButton[0]
  ├─ Portrait (Image — 초상화)
  ├─ CostText (TextMeshPro)
  ├─ AutoIndicator (GameObject)
  ├─ BorderOverlay (Image)
  └─ LockIndicator  ← _unitLockIndicators[0] (이 GO 안에 작업)
       └─ LockIcon (Image)
            ├─ Sprite: ui_icon_lock.png
            ├─ RectTransform: 앵커 우측 하단 (anchorMin/Max = 1,0)
            ├─ 크기: 버튼 대비 약 35~40% 크기
            └─ Raycast Target: OFF
```

### 설정 기준
- **앵커**: 버튼 GO 기준 우측 하단 코너 (anchorMin=(1,0), anchorMax=(1,0))
- **Pivot**: (1, 0) — 우측 하단 기준
- **크기**: 버튼 크기에 대해 앵커 비율로 설정 (GameSystemRules 규칙 2 — 고정 픽셀 금지)
- **Raycast Target**: OFF (클릭 이벤트를 버튼에 그대로 전달)
- **Color**: 흰색 (White) 유지 — 아이콘 자체 색상으로 표현

---

## 예상 위험 요소

| 위험 | 대응 |
|------|------|
| 초상화 color가 다른 곳에서 재설정될 수 있음 | `UpdateButtonPortraits()`는 `.sprite`만 변경하고 `.color`는 건드리지 않아 충돌 없음 (Research 확인) |
| 2유닛 특수 배치 슬롯1(더미) 처리 | `_unitLockIndicators[1]`은 `show=false`가 되고, 더미 슬롯은 `CanvasGroup.alpha=0`으로 숨겨져 있어 시각적 영향 없음 (Research 확인) |
| 패널 재사용 시 이전 color 잔류 | `UpdateLockIndicators()`는 `OnShow()` 진입 시 매번 호출되므로, 패널을 다시 열 때 항상 재평가됨 |

---

## 작업 순서

1. `ProductionPanelUI.cs` — `UpdateLockIndicators()` 수정 (코드 변경)
2. 씬에서 각 `_unitLockIndicators[i]` GO 하위에 LockIcon Image 추가 (Inspector 작업)
3. 빌드/플레이 후 확인: 1단계 건물에서 2단계 유닛이 잠긴 경우 → 초상화 디밍 + 자물쇠 아이콘 표시
