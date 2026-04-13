# Research: 피격 시 부유 텍스트 (Floating HP Text)

**작업일:** 2026-04-12  
**목적:**
- 피격 오브젝트 머리 위에 남은 HP를 텍스트로 표시 (위로 이동 + 페이드아웃)
- 단기: **남은 HP** 표시 → 스탯 적용 테스트 수단
- 추후: **대미지 수치**로 교체 예정

---

## 1. 이벤트 및 데이터 구조

### EntityDamagedEvent
**경로:** `Assets/_Project/Scripts/Application/Events/GameEvents.cs`

```
public readonly struct EntityDamagedEvent
{
    public readonly IDamageable Entity;   // 피격 엔티티 (Id, IsAlive 등)
    public readonly int CurrentHp;        // 데미지 적용 후 남은 HP ← 이번 작업에서 표시할 값
    public readonly bool IsUnit;          // true=유닛, false=건물
}
```

이벤트 Subject: `GameEvents.OnEntityDamaged` — 피격 직후 발행됨

### IDamageable
**경로:** `Assets/_Project/Scripts/Domain/Common/IDamageable.cs`
- `int Id` — 유닛/건물 고유 ID. 월드 좌표 조회에 사용

---

## 2. 월드 좌표 조회

### IEntityPositionProvider / UnitWorldPositionProvider
**경로:**
- `Assets/_Project/Scripts/Application/Interfaces/IEntityPositionProvider.cs`
- `Assets/_Project/Scripts/Infrastructure/UnitWorldPositionProvider.cs`

```
GetUnitWorldPosition(int unitId)     → 유닛 GameObject.transform.position
GetBuildingWorldPosition(int buildingId) → 건물 GameObject.transform.position
```

이미 GameBootstrapper에서 생성·주입된 인스턴스가 존재함.

**주의:** 사망 직후 GameObject가 파괴되면 `Vector3.zero` 반환. 피격 이벤트는 사망 이벤트 전에 발행되므로 일반적으로 안전.

---

## 3. 화면 좌표 변환 방법

UI Canvas가 Screen Space Overlay 방식이므로:
1. `IEntityPositionProvider`로 월드 좌표(Vector3) 획득
2. `Camera.main.WorldToScreenPoint(worldPos)` → 스크린 픽셀 좌표
3. `RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint)` → Canvas 로컬 좌표
4. FloatingHpText의 `anchoredPosition` = localPoint + Y 오프셋 (유닛 위쪽)

---

## 4. DOTween 사용 현황

`UIAnimator.cs` (`Assets/_Project/Scripts/Presentation/UI/Common/UIAnimator.cs`)에서 이미 `DG.Tweening` 사용 중.  
→ DOTween이 프로젝트에 포함되어 있음. 추가 설치 불필요.

DOTween 주요 API:
```csharp
transform.DOLocalMoveY(targetY, duration)  // 위쪽 이동
canvasGroup.DOFade(0f, duration)           // 페이드아웃
```

---

## 5. 폰트

`Assets/_Project/Fonts/Maplestory Light SDF.asset` — 프로젝트 내 확인됨

---

## 6. 오브젝트 풀링 필요성

피격이 빈번하게 발생하므로 매번 Instantiate/Destroy 대신 **오브젝트 풀** 사용 권장.  
Unity 기본 `Queue<T>` 또는 `Stack<T>` 기반으로 단순 구현.

---

## 7. 관련 기존 파일 목록

| 파일 | 역할 | 참고 이유 |
|------|------|-----------|
| `UIAnimator.cs` | DOTween 유틸 | 애니메이션 패턴 참고 |
| `AnimatedPanel.cs` | CanvasGroup 기반 페이드 패턴 | 페이드 구현 패턴 참고 |
| `GameBootstrapper.cs` | 의존성 조합 루트 | 신규 컴포넌트 초기화 위치 |
| `UnitWorldPositionProvider.cs` | 월드 좌표 조회 | 위치 변환 근거 |
| `GameEvents.OnEntityDamaged` | 이벤트 발행 지점 | 구독 타이밍 |

---

## 8. 추후 전환 계획 (대미지 수치로 교체)

| 항목 | 현재 (테스트용) | 추후 (완성형) |
|------|----------------|--------------|
| 표시 값 | `CurrentHp` (남은 HP) | 대미지량 |
| 이벤트 데이터 | `EntityDamagedEvent.CurrentHp` | 별도 `DamageAmount` 필드 추가 필요 |
| 색상 | 흰색 단일 | 대미지 크기에 따라 색상 분기 가능 |

`EntityDamagedEvent`에 `DamageAmount` 필드 추가는 별도 작업으로 분리.  
현재 이벤트 구조는 변경하지 않고, `CurrentHp`만 읽어 표시.
