# Research — Production 잠금 유닛 Lock Icon 표시

업그레이드가 필요한 유닛 버튼에 `ui_icon_lock.png`를 이용한 시각적 잠금 표현을 추가하는 작업이다.
현재는 잠긴 유닛 버튼을 눌렀을 때 토스트만 뜨고, 버튼 자체는 평범하게 보여서 잠금 상태임을 직관적으로 알기 어렵다.
이 작업으로 잠긴 유닛 버튼의 초상화를 어둡게 디밍하고, 우측 하단에 자물쇠 아이콘 배지를 표시하여
플레이어가 "이 유닛은 업그레이드 후 사용 가능하다"는 것을 바로 인식할 수 있게 한다.

---

## 1. 현재 잠금 시스템 구조

### 파일
- [ProductionPanelUI.cs](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs)

### 잠금 판정 흐름

1. `OnShow()` 호출 시 `BindButtonUnitTypes()` → `UpdateLockIndicators()` 순서로 실행됨
2. `BindButtonUnitTypes()` ([line 864](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs#L864)):
   - 현재 건물 단계(`_currentBuilding.Stage`)와 각 유닛의 `requiredStage`를 비교
   - `currentStage < requiredStage` 이면 `_activeUnitLocks[i] = true`로 설정
3. `UpdateLockIndicators()` ([line 703](Assets/_Project/Scripts/Presentation/UI/ProductionPanelUI.cs#L703)):
   - `_unitLockIndicators[i].SetActive(show)` 호출로 잠금 오버레이 ON/OFF
4. 잠금 유닛 탭 시 `IsUnitLocked()` 체크 후 `ToastUI.Show(ToastKey.UpgradeRequired)` 표시

### 관련 필드 (Inspector 직렬화)

| 필드 | 타입 | 역할 |
|------|------|------|
| `_unitLockIndicators` | `List<GameObject>` | 잠금 오버레이 GO 리스트. 현재 ON/OFF만 하며 내용물(Image 등)은 Inspector에서 설정 |
| `_unitButtonPortraits` | `List<Image>` | 유닛 초상화 Image. 잠금 시 색상 변경 대상 |
| `_activeUnitLocks` | `List<bool>` | 슬롯별 잠금 여부. `BindButtonUnitTypes()`에서 계산 |

---

## 2. Lock Icon 에셋

- 경로: `Assets/_Project/Sprites/UI/Icons/ui_icon_lock.png`
- 상태: 파일 존재 확인 완료

---

## 3. 구현에 필요한 변경 범위

### A. 코드 변경 (1곳)
`UpdateLockIndicators()` 내부에 초상화 디밍 로직 추가:
- 잠금 상태: `_unitButtonPortraits[i].color = new Color(0.35f, 0.35f, 0.35f, 1f)`
- 해금 상태: `_unitButtonPortraits[i].color = Color.white`

현재 `UpdateLockIndicators()`는 GO SetActive만 처리하므로, 초상화 color 변경은 별도로 추가해야 한다.

### B. Inspector 작업 (씬에서 수동 설정)
`_unitLockIndicators[i]` GameObject 내부에 자물쇠 아이콘 Image 설정:
- 버튼 우측 하단 배지 위치에 Image 컴포넌트 추가
- `ui_icon_lock.png` Sprite 할당
- 크기 및 앵커는 버튼 대비 비율로 설정 (GameSystemRules 규칙 2 준수)

---

## 4. 영향 범위 확인

- `UpdateLockIndicators()`는 `OnShow()` 진입 시 1회만 호출됨
- 패널이 열린 상태에서 건물 단계가 변경되는 경우 없음 (업그레이드 시 `Close()` 강제 호출됨 — line 623)
- 초상화 color 변경은 `UpdateButtonPortraits()`가 재실행될 때 덮어써지지 않음 (해당 메서드는 sprite만 변경)

---

## 5. 사이드 이펙트 없음 확인

- 2유닛 특수 배치(슬롯1 더미)의 경우, `_unitLockIndicators[1]`도 `show=false`로 꺼지므로 문제없음
- `_unitButtonPortraits[1]` (더미 슬롯)은 `CanvasGroup.alpha=0`으로 숨겨져 있어 color가 바뀌어도 시각적 영향 없음
