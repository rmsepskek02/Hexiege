# Plan: 피격 시 부유 텍스트 (Floating HP Text)

**작업일:** 2026-04-12  
**표시 값:** 남은 HP (`EntityDamagedEvent.CurrentHp`) — 스탯 테스트 목적  
**추후 교체:** 대미지 수치로 전환 (별도 작업)

---

## 구현 목표

피격 이벤트 발생 시 피격 오브젝트 머리 위쪽에 남은 HP 수치가 나타나며,
위로 서서히 이동하면서 투명해지다 사라지는 UI 텍스트 효과 구현.

---

## 신규 파일 목록

| 파일 | 레이어 | 역할 |
|------|--------|------|
| `FloatingHpText.cs` | Presentation | 단일 부유 텍스트 오브젝트 — 표시·애니메이션·풀 반환 |
| `FloatingHpTextSpawner.cs` | Presentation | 이벤트 수신, 위치 계산, 풀 관리 |
| `FloatingHpText.prefab` | Prefab | TextMeshProUGUI + CanvasGroup 구성 |

**기존 파일 수정:**  
- `GameBootstrapper.cs` — `FloatingHpTextSpawner` 초기화 및 의존성 주입

---

## [1] FloatingHpText.cs
**경로:** `Assets/_Project/Scripts/Presentation/UI/Common/FloatingHpText.cs`

### 역할
- TextMeshProUGUI로 HP 수치를 표시
- DOTween으로 위로 이동 + 페이드아웃 애니메이션 실행
- 애니메이션 완료 후 `FloatingHpTextSpawner`의 풀에 자신을 반환

### 주요 멤버
```
[SerializeField] TextMeshProUGUI _text      — TMP 참조
[SerializeField] CanvasGroup _canvasGroup   — 페이드아웃 제어
Action<FloatingHpText> _onReturn            — 풀 반환 콜백 (Spawner가 등록)

void Play(string text, Vector2 anchoredPosition)
    → 위치 설정 → 텍스트 설정 → 애니메이션 시작
```

### 애니메이션 상세
```
duration = 1.2초
이동: anchoredPosition.y → anchoredPosition.y + 80f  (DOLocalMoveY)
페이드: alpha 1 → 0  (DOFade)
완료 시: _onReturn(this) 호출 → 풀 반환
```

---

## [2] FloatingHpTextSpawner.cs
**경로:** `Assets/_Project/Scripts/Presentation/UI/FloatingHpTextSpawner.cs`

### 역할
- `GameEvents.OnEntityDamaged` 구독
- 피격 엔티티 ID + IsUnit 여부로 월드 좌표 조회
- 월드 좌표 → Canvas 로컬 좌표 변환
- 풀에서 FloatingHpText 꺼내어 Play() 호출

### 의존성 (GameBootstrapper에서 주입)
```
IEntityPositionProvider _positionProvider   — 월드 좌표 조회
Canvas _canvas                              — 씬의 [UI] Canvas (월드→로컬 변환 기준)
FloatingHpText _prefab                      — 풀 생성 원본
```

### 위치 계산 흐름
```
1. EntityDamagedEvent.Entity.Id + IsUnit 으로 GetUnitWorldPosition / GetBuildingWorldPosition 호출
2. Camera.main.WorldToScreenPoint(worldPos) → 스크린 픽셀 좌표
3. RectTransformUtility.ScreenPointToLocalPointInRectangle(
       canvas.transform as RectTransform, screenPoint, null, out localPoint)
4. anchoredPosition = localPoint + new Vector2(0, 80f)  // 머리 위 오프셋
```

### 풀 구현
```
Queue<FloatingHpText> _pool   — 비활성 텍스트 보관
int InitialPoolSize = 10       — 게임 시작 시 미리 생성

Get()  → _pool에 있으면 꺼냄, 없으면 Instantiate
Return() → SetActive(false) 후 _pool에 Enqueue
```

### 구독 관리
- `Initialize()` 에서 `GameEvents.OnEntityDamaged.Subscribe(OnDamaged)` 구독
- `AddTo(this)` 또는 CompositeDisposable로 씬 종료 시 자동 해제

---

## [3] FloatingHpText.prefab

**구성:**
```
FloatingHpText (RectTransform + CanvasGroup)
  │   blocksRaycasts: false  ← 입력 차단 방지 (Awake에서 코드로 설정)
  └─ Text (TextMeshProUGUI)
       폰트: Maplestory Light SDF.asset
       폰트 크기: 28
       색상: 흰색 (255, 255, 255)
       Alignment: Center
       RaycastTarget: OFF    ← 텍스트 영역 입력 통과
```

**입력 차단 방지 설계:**
| 설정 위치 | 항목 | 값 | 이유 |
|-----------|------|----|------|
| TextMeshProUGUI | RaycastTarget | false | 텍스트 렌더 영역이 터치 이벤트를 가로채지 않도록 |
| CanvasGroup | blocksRaycasts | false | CanvasGroup 영역 전체의 입력 차단을 비활성화 |

`FloatingHpText.Awake()`에서 `_canvasGroup.blocksRaycasts = false` 초기화.

**RectTransform 기본값:**
- sizeDelta: (120, 40)
- Anchor: (0.5, 0.5) — Spawner가 anchoredPosition으로 위치를 직접 지정하므로 앵커 위치는 무관

---

## [4] GameBootstrapper.cs 수정

기존 초기화 흐름에 `FloatingHpTextSpawner` 초기화 추가:
```csharp
// FloatingHpTextSpawner 초기화 — 피격 시 부유 HP 텍스트 표시
_floatingHpTextSpawner = floatingHpTextSpawnerObj.GetComponent<FloatingHpTextSpawner>();
_floatingHpTextSpawner.Initialize(_positionProvider, _uiCanvas, floatingHpTextPrefab);
```

Inspector에서 연결할 SerializedField:
- `FloatingHpTextSpawner` 컴포넌트가 붙을 빈 GameObject → 씬에 배치
- `FloatingHpText` 프리팹 → Resources 또는 직접 Inspector 연결

---

## 구현 순서

```
[1] FloatingHpText.cs 작성 + 프리팹 생성 (Editor 작업)
[2] FloatingHpTextSpawner.cs 작성
[3] GameBootstrapper.cs 수정
[4] 씬에 FloatingHpTextSpawner GameObject 배치 + Inspector 연결
[5] NetworkHealthSync.cs 수정 — 클라이언트 측 이벤트 재발행
```

---

## 멀티플레이 클라이언트 동작

`GameEvents.OnEntityDamaged`는 서버의 `UnitCombatUseCase`에서만 발행된다.  
클라이언트는 `SyncHealthClientRpc`로 HP를 수신하지만 이벤트를 발행하지 않아 `FloatingHpTextSpawner`가 반응하지 못한다.

**수정:** `NetworkHealthSync.SyncUnitHealth` / `SyncBuildingHealth`에서 `TakeDamage` 적용 후 클라이언트에서도 `GameEvents.OnEntityDamaged.OnNext()` 재발행.  
`diff > 0`인 경우에만 발행하므로 이미 동기화된 상태에서 중복 표시 없음.

**수정 파일:** `Assets/_Project/Scripts/Infrastructure/Network/NetworkHealthSync.cs`

---

## 위험 요소

| 위험 | 내용 | 대응 |
|------|------|------|
| 사망 직후 위치 조회 | OnEntityDamaged와 OnEntityDied가 같은 프레임에 발행될 수 있음 | 이벤트 발행 순서 확인 — OnEntityDamaged가 먼저 발행됨 (UnitCombatUseCase 코드 기준) |
| Camera.main = null | 씬 로드 직후 참조 없을 수 있음 | Initialize()에서 Camera.main 캐싱 |
| Canvas 참조 | [UI] Canvas가 여러 개일 경우 잘못된 Canvas에 생성될 수 있음 | GameBootstrapper에서 명시적 참조 주입 |
| DOTween 중복 실행 | 풀에서 꺼낸 직후 이전 Tween이 완료 안 된 경우 | Play() 시작 시 기존 Sequence Kill 처리 |
| 클라이언트 UI 미표시 | OnEntityDamaged가 서버에서만 발행됨 | NetworkHealthSync에서 클라이언트 재발행으로 해결 |

---

## 추후 대미지 수치 전환 시 변경 범위

| 항목 | 변경 내용 |
|------|-----------|
| `EntityDamagedEvent` | `DamageAmount` 필드 추가 |
| `UnitCombatUseCase` | 이벤트 발행 시 대미지량 전달 |
| `FloatingHpTextSpawner` | `CurrentHp` → `DamageAmount` 로 읽는 값 변경 (1줄) |
| 텍스트 포맷 | `$"{currentHp}"` → `$"-{damageAmount}"` 또는 색상 추가 |

코드 변경 최소화를 위해 `FloatingHpTextSpawner`에 `bool _showDamage` 플래그 or 메서드 분기를 두는 방향 권장.
