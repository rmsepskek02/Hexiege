# Plan: 로비 종족 선택 UI (v2 — 캐러셀 방식)

## 구현 목표

- 3종족 캐릭터를 동시에 RenderTexture 카메라 뷰에 배치
- 중앙(선택): 카메라에 가까워 크게, 좌우(비선택): 멀어서 작게 — 원근감으로 구분
- 버튼 클릭 시 DOTween으로 캐릭터 포지션 이동 (캐러셀)
- 반응형 UI (앵커 기반, 고정 픽셀 없음)

---

## 변경할 파일 (2개만)

### 1. `RaceSelectionView.cs` — 캐러셀 로직으로 전면 교체

### 2. `RaceSelectionPreviewSetup.cs` — 씬 자동 구성 에디터 스크립트 전면 교체

---

## RaceSelectionView.cs 설계

### Inspector 참조 (기존과 동일)

```csharp
[Header("UI")]
[SerializeField] private RawImage _rawImage;
[SerializeField] private TMP_Text _raceNameText;
[SerializeField] private Button _prevButton;
[SerializeField] private Button _nextButton;

[Header("캐릭터 오브젝트")]
[SerializeField] private GameObject[] _characterRoots; // [0]인간 [1]정령 [2]자연

[Header("캐러셀 슬롯 위치 (World Space)")]
[SerializeField] private Vector3 _centerPos  = new Vector3(1000f, 0f, 4f);
[SerializeField] private Vector3 _leftPos    = new Vector3(997.5f, 0f, 7f);
[SerializeField] private Vector3 _rightPos   = new Vector3(1002.5f, 0f, 7f);
[SerializeField] private float   _moveDuration = 0.3f;
```

### Bind 로직

1. `SelectedRace` 구독 → `ApplyCarouselPositions(race)` 호출
2. `SelectedRaceName` 구독 → `_raceNameText.text` 갱신
3. 버튼 → Cmd 연결

### ApplyCarouselPositions(RaceId selected)

```
offset = (characterIndex - selectedIndex + 3) % 3
  0 → _centerPos (선택)
  1 → _rightPos
  2 → _leftPos
```

각 캐릭터에 대해:
- `_characterRoots[i].SetActive(true)` — 모두 활성 유지
- `DOTween.Kill(_characterRoots[i].transform)` → `.DOMove(targetPos, _moveDuration).SetEase(Ease.OutCubic)`

초기 바인딩 시에는 애니메이션 없이 즉시 배치 (`transform.position = targetPos`).

---

## RaceSelectionPreviewSetup.cs 설계

### 처리 순서

```
[1] CharacterPreview 레이어 생성
[2] RenderTexture 에셋 생성 (512×512)
[3] CharacterPreviewCamera 생성
      위치: (1000, 1.2, -1)
      회전: Quaternion.identity (정면 +Z 촬영)
      FOV: 50, 원근, CullingMask: CharacterPreview, TargetTexture: RT
[4] CharacterPreviewRoot 아래 3개 캐릭터 인스턴스
      Human   → Unit_Pistoleer_Blue, 초기위치: CenterPos(1000, 0, 4)
      Spirit  → Unit_EmberSpirit_Blue, 초기위치: RightPos(1002.5, 0, 7)
      Nature  → Unit_FoxMagician_Blue, 초기위치: LeftPos(997.5, 0, 7)
      (Human이 기본 선택이므로 S=0 기준 배치)
      모두 SetActive(true)
      레이어 재귀 적용
[5] BattleMainView 찾기 (FindFirstObjectByType)
[6] BattleMainView 하위에 RaceSelectionView UI 생성
      - 반응형 패널 (가로 stretch, 높이 = 부모의 50%)
      - RawImage (가로 stretch, 높이 = 패널의 85%)
      - TMP_Text (RaceNameText)
      - PrevButton / NextButton (삼각형 느낌 텍스트 "◀" "▶", 수직 중앙)
      - RaceSelectionView 컴포넌트 추가
[7] RaceSelectionView Inspector 슬롯 연결 (SerializedObject)
      _rawImage, _raceNameText, _prevButton, _nextButton
      _characterRoots[0]=CharPreview_Human [1]=Spirit [2]=Nature
      _centerPos, _leftPos, _rightPos (SerializeField Vector3)
[8] BattleMainView._raceSelectionView 슬롯 연결
```

### UI 반응형 앵커 설계

```
RaceSelectionView 패널:
  anchorMin = (0, 0), anchorMax = (1, 0)   ← 가로 전체 stretch
  pivot = (0.5, 0)
  anchoredPosition = (0, 0)
  sizeDelta = (0, 0)                        ← 높이도 stretch: anchorMax.y 조정으로 비율 지정
  → anchorMax.y = 0.5f (부모 높이의 50%)

CharacterDisplay (RawImage):
  anchorMin = (0.1, 0.15), anchorMax = (0.9, 1.0)  ← 패널 기준 양쪽 10% 여백, 상단 밀착
  sizeDelta = (0, 0)                                  ← 완전 stretch

RaceNameText:
  anchorMin = (0.1, 0), anchorMax = (0.9, 0.18)
  sizeDelta = (0, 0)

PrevButton:
  anchorMin = (0, 0.3), anchorMax = (0.12, 0.7)  ← 왼쪽 12%, 수직 40% 구간
  sizeDelta = (0, 0)

NextButton:
  anchorMin = (0.88, 0.3), anchorMax = (1, 0.7)  ← 오른쪽 12%, 수직 40% 구간
  sizeDelta = (0, 0)
```

---

## 위험 요소

| 항목 | 내용 | 대응 |
|------|------|------|
| DOTween 의존 | RaceSelectionView에서 DOTween 사용 | 이미 프로젝트에 포함됨 — `using DG.Tweening` 추가 |
| 초기 바인딩 시 애니메이션 | 씬 로드 시 DOMove 실행되면 이상하게 보임 | 첫 바인딩은 즉시 배치(`transform.position`), 이후 구독에서만 DOTween |
| 카메라 위치 (1000, 1.2, -1) 충돌 | 다른 오브젝트와 좌표 겹칠 가능성 | CharacterPreview 레이어만 렌더링이므로 시각적 충돌 없음 |
| BattleMainView에서 RaceSelectionView 못 찾음 | FindFirstObjectByType 실패 시 | 씬에 BattleMainView가 없으면 에러 로그 출력 후 종료 |

---

## v2 수정사항 (2026-04-05 추가)

### 문제 1: RaceSelectionView 배치 방식 수정

**기존 방식(잘못됨)**: RaceSelectionView를 BattleMainPanel VerticalLayoutGroup 흐름에 포함
→ VerticalLayoutGroup 수정으로 기존 버튼 3개 위치가 변경됨

**수정 방향**: `LayoutElement.ignoreLayout = true` 적용
- RaceSelectionView를 BattleMainPanel 자식으로 유지 (BattleMainPanel 숨김 시 함께 숨겨짐)
- `ignoreLayout=true`로 VerticalLayoutGroup 레이아웃 흐름에서 제외 → 버튼 3개 위치 무변경
- 앵커 (0,0)~(1,0.45)로 BattleMainPanel 하단 45%를 절대 위치로 차지

**에디터 스크립트에서 제거:**
- VerticalLayoutGroup.childControlHeight 변경 코드
- childAlignment 변경 코드
- 버튼들에 LayoutElement 추가하는 foreach 루프
- BattleMainView RectTransform 수정 코드

**에디터 스크립트에서 추가:**
- RaceSelectionView에 `LayoutElement.ignoreLayout = true` 설정

**절대 건드리지 말 것:** BattleMainPanel, CustomGamePanel, CustomHostPanel, CustomJoinPanel, RandomMatchPanel

### 문제 2: 캐릭터 원근감 및 조명 설정 업데이트

| 항목 | 기존 | 수정 |
|------|------|------|
| CenterPos | (1000, 0, 3) | (1000, 0, 2) |
| LeftPos | (997.5, 0, 8) | (998.5, 0, 9) |
| RightPos | (1002.5, 0, 8) | (1001.5, 0, 9) |
| CamPos | (1000, 1.0, 0.5) | 유지 |
| FOV | 40 | 45 |
| Light intensity | 2.0 | 유지 |

- X 간격 축소 (±2.5 → ±1.5): 비선택 캐릭터가 가장자리에서 잘리는 문제 해결
- Z 차이 확대 (Center=2, Left/Right=9): 선택/비선택 크기 차이 극대화

---

## v3 추가 사항 (2026-04-06)

### 확정된 씬 구조

```
BattlePanel (BattleRootView)
  ├── BattleMainPanel  (anchorMin.y=0.5, anchorMax.y=1 → 상단 50%)
  └── RaceSelectionView  (anchorMin=0,0 ~ anchorMax=1,0.5 → 하단 50%, BattlePanel 직속 자식)
        ├── CharacterDisplay (anchorMin=0.05,0.15 ~ anchorMax=0.95,1.3, sizeDelta=0,0)
        ├── RaceNameText
        ├── PrevButton
        └── NextButton
```

### 확정된 씬 설정값

```
RaceSelectionView:
  _centerPos: (1000, 0.35, 2)
  _leftPos:   (999.7, 0.3, 4)
  _rightPos:  (1000.3, 0.3, 4)
  _moveDuration: 1
```

### 애니메이션 전환 기능 추가

**요구사항**: 선택된(중앙) 캐릭터 → Walk, 비선택(좌우) 캐릭터 → Idle, CrossFade 0.3초

**Animator 컨트롤러 상태 현황:**
| 종족 | Idle | Walk |
|------|------|------|
| 인간(Pistoleer) | ✅ "Idle" | ✅ "Walk" |
| 정령(EmberSpirit) | ✅ "Idle" | ✅ "Walk" |
| 초월(FoxMagician) | ✅ "Idle" | ✅ "Walk" |

**버그 수정**: Pistoleer.controller의 Idle 상태 `m_Speed: 0` → `m_Speed: 1` 수정 (Idle 첫 프레임 동결 원인)

**RaceSelectionView.cs 변경 사항:**
- `Bind()` 시 각 캐릭터 루트에서 `GetComponentInChildren<Animator>()` 캐시
- `ApplyCarouselPositions()` 에서 offset=0(중앙) → `CrossFadeInFixedTime("Walk", 1.0f, 0)`, offset=1,2(좌우) → `CrossFadeInFixedTime("Idle", 1.0f, 0)`
- `_animators` 배열은 `_characterRoots`와 같은 길이
- AnimBlendTime = 1.0f (_moveDuration과 동일)

### 에디터 스크립트 업데이트

CharacterDisplay 앵커를 수동 조정값(anchorMax.y=1.3, sizeDelta.x=-100) 기준으로 순수 앵커 방식으로 정리:
- anchorMin: (0.05, 0.15)
- anchorMax: (0.95, 1.3)
- sizeDelta: (0, 0)

캐릭터 포지션 상수도 씬 확정값으로 업데이트.

---

## v4 추가 사항 (2026-04-06)

### 종족 이름 변경: Nature(자연) → Transcendence(초월)

| 항목 | 변경 전 | 변경 후 |
|------|---------|---------|
| C# enum 식별자 | `RaceId.Nature` | `RaceId.Transcendence` |
| 한글 표시 이름 | `"자연"` | `"초월"` |
| 씬 오브젝트명 | `CharPreview_Nature` | `CharPreview_Transcendence` |

**변경된 파일:**
- `Domain/Common/RaceId.cs`
- `Presentation/UI/ViewModels/RaceSelectionViewModel.cs`
- `Presentation/UI/Views/Lobby/Battle/RaceSelectionView.cs`
- `Editor/RaceSelectionPreviewSetup.cs`
- `Infrastructure/LocalPlayerRace.cs`
- `Infrastructure/GameRaceContext.cs`

---

## v5 추가 사항 (2026-04-06)

### RaceSelectionView 항상 표시

**변경 전**: 커스텀 게임 등 서브 화면 전환 시 RaceSelectionView 비표시
**변경 후**: 화면 전환과 무관하게 RaceSelectionView 항상 표시 유지

`BattleMainView.cs` CurrentScreen 구독에서 `_raceSelectionView.gameObject.SetActive(visible)` 제거.
BattleMainPanel(버튼 영역)만 숨기고 RaceSelectionView는 독립적으로 항상 활성 상태.

---

## v6 추가 사항 (2026-04-06)

### 모바일 URP 렌더링 버그 수정

**증상**: 실기기 Android에서 `EndRenderPass: Not inside a Renderpass` 에러 발생
**원인**: `CharacterPreviewRT.renderTexture`의 `antiAliasing = 2` (MSAA 2x) — Android Vulkan에서 URP가 MSAA RenderTexture RenderPass 종료 시 상태 불일치 발생
**수정**:
1. `EnsureRenderTexture`에서 신규 RT: `antiAliasing = 1` (MSAA 비활성화)
2. `EnsureRenderTexture`에서 기존 RT도 `antiAliasing != 1`이면 업데이트하여 에셋 재생성 없이 수정
3. `EnsureCamera`에서 `UniversalAdditionalCameraData` 추가 — URP Base 카메라로 명시 (Overlay로 잘못 처리되는 케이스 방지)

---

## v7 추가 사항 (2026-04-06)

### Android 실기기 잔상(ghosting) + RenderPass 에러 수정

**증상**:
1. `RenderPass: Attachment 0 was created with 1 samples but 2 samples were requested` 에러
2. 캐릭터 이동 시 이전 위치에 잔상이 남는 현상

**근본 원인**: `CharacterPreviewRT.renderTexture`의 `m_AntiAliasing: 2` (에셋 파일에 실제 저장된 값)

- 카메라는 `allowMSAA=false` (1 sample)로 렌더링하려 하지만, RT 에셋 자체가 2 sample을 요구
- URP가 1 sample 중간 버퍼를 생성한 뒤 2 sample RT에 쓰려 해서 sample count 충돌 발생
- 충돌로 인해 Render Pass 초기화 실패 → clear 대신 이전 프레임 타일 메모리 로드 → 잔상

**수정된 파일 및 값:**

| 파일 | 항목 | 변경 전 | 변경 후 |
|------|------|---------|---------|
| `CharacterPreviewRT.renderTexture` | `m_AntiAliasing` | `2` | `1` |
| `Lobby.unity` (Camera 컴포넌트) | `m_AllowMSAA` | `1` | `0` |
| `Lobby.unity` (Camera 컴포넌트) | `m_HDR` | `1` | `0` |
| `Lobby.unity` (Camera 컴포넌트) | `m_BackGroundColor.a` | `0` | `1` |
| `RaceSelectionPreviewSetup.cs` | `cam.allowMSAA` | (미설정 기본값 true) | `false` |
| `RaceSelectionPreviewSetup.cs` | `cam.allowHDR` | (미설정 기본값 true) | `false` |
| `RaceSelectionPreviewSetup.cs` | `backgroundColor.a` | `0f` | `1f` |
| `RaceSelectionPreviewSetup.cs` | `urpData.antialiasing` | (미설정) | `AntialiasingMode.None` |

**결과**: 실기기 테스트 PASS — 잔상 없음, RenderPass 에러 없음

---

## 구현 후 확인 항목

- [ ] 3개 캐릭터가 RenderTexture에 동시에 보임
- [ ] 버튼 클릭 시 캐러셀 전환 애니메이션 재생
- [ ] 중앙 캐릭터가 좌우보다 크게 보임 (원근감)
- [ ] 비선택 캐릭터가 화면 가장자리에서 잘리지 않음
- [ ] 기존 버튼 3개(싱글플레이어/커스텀게임/랜덤매칭) 위치 변경 없음
- [ ] 반응형: 해상도 변경 시 UI가 깨지지 않음
- [ ] 탭 재진입 시 이전 선택 종족 유지 (LocalPlayerRace.Current 초기값)
- [ ] 선택된 캐릭터 Walk 재생, 비선택 캐릭터 Idle 재생
- [ ] 전환 시 1.0초 CrossFade 블렌드 적용
- [ ] 종족명 '초월' 텍스트 정상 표시
