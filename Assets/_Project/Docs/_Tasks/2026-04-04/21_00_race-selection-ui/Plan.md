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

## 구현 후 확인 항목

- [ ] 3개 캐릭터가 RenderTexture에 동시에 보임
- [ ] 버튼 클릭 시 캐러셀 전환 애니메이션 재생
- [ ] 중앙 캐릭터가 좌우보다 크게 보임 (원근감)
- [ ] 반응형: 해상도 변경 시 UI가 깨지지 않음
- [ ] 탭 재진입 시 이전 선택 종족 유지 (LocalPlayerRace.Current 초기값)
