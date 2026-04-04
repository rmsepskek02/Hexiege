# Research: 로비 종족 선택 UI (v2 — 캐러셀 방식)

## 작업 개요

로비 전투 탭 랜덤 매칭 버튼 하단에 종족 선택 UI를 추가한다.
3종족 대표 캐릭터 3D 모델이 동시에 화면에 표시되고, 버튼으로 선택하면
중앙(선택)↔좌우(비선택) 포지션이 이동하는 캐러셀 방식.

**이번 작업 범위**: 종족 선택 UI + 선택 정보 저장 + 멀티플레이 종족 동기화 설계
**다음 작업 범위**: 종족별 유닛/건물 실제 적용 (인게임 플레이 시스템)

---

## 1. v1과 v2의 차이점

| 항목 | v1 (폐기) | v2 (채택) |
|------|----------|----------|
| 캐릭터 표시 방식 | 한 종족만 활성/비활성 전환 | 3종족 동시에 카메라 앵글 내 배치 |
| 공간감 | 없음 (flat) | 원근감 — 선택된 캐릭터는 앞(크게), 비선택은 뒤(작게) |
| 전환 | SetActive 토글 | 캐릭터 포지션 이동 (DOTween) |
| 색상 틴트 | 없음 | 없음 (모델 그대로, 크기/원근감으로만 구분) |
| 인간 대표 캐릭터 | Unit_LionKnight_Blue | Unit_Pistoleer_Blue |

---

## 2. 로비 MVVM 구조 (변경 없음)

```
LobbyRootView
  └─ BattleRootView
       └─ BattleMainView      ← RaceSelectionView 하위 배치
            └─ RaceSelectionView (신규)
```

- `BattleMainView.BindRace(RaceSelectionViewModel)` — 기존 구조 유지
- `BattleRootView`에서 `RaceSelectionViewModel` 생성/Dispose — 기존 구조 유지
- 변경이 필요한 것: **RaceSelectionView.cs** 로직 (hide/show → carousel)

---

## 3. 캐러셀 배치 방식

### 3D 씬 구성

```
[CharacterPreviewCamera]  위치: (1000, 1.2, -1), FOV: 50, 원근 투영
                                    ↓ RenderTexture
[CharPreview_Human]     ─ 위치: 선택 상태에 따라 동적 이동
[CharPreview_Spirit]    ─ 위치: 선택 상태에 따라 동적 이동
[CharPreview_Nature]    ─ 위치: 선택 상태에 따라 동적 이동
```

### 3개 슬롯 위치 (World Space)

| 슬롯 | World Position | 의미 |
|------|---------------|------|
| Center | (1000, 0, 4) | 선택된 종족 — 카메라에 가까워 크게 보임 |
| Left   | (997.5, 0, 7) | 이전 종족 — 멀고 왼쪽 |
| Right  | (1002.5, 0, 7) | 다음 종족 — 멀고 오른쪽 |

### 포지션 배정 로직

선택 인덱스 S, 캐릭터 인덱스 i:
```
offset = (i - S + 3) % 3
  0 → Center
  1 → Right
  2 → Left
```

**예시 (S=1, 정령 선택)**:
- Human(0): offset=(0-1+3)%3=2 → Left
- Spirit(1): offset=(1-1+3)%3=0 → Center
- Nature(2): offset=(2-1+3)%3=1 → Right

### 전환 애니메이션

- DOTween `.DOMove(targetPos, 0.3f).SetEase(Ease.OutCubic)`
- 이미 DOTween이 프로젝트에 적용되어 있음 (CameraController 등)

---

## 4. 대표 캐릭터 프리팹

| 종족 | 대표 유닛 | 프리팹 경로 |
|------|---------|-----------|
| 인간 (Human) | 피스톨러 | `Assets/_Project/Prefabs/Units/Unit_Pistoleer_Blue.prefab` ✅ |
| 정령 (Spirit) | 꼬마불정령 | `Assets/_Project/Prefabs/Units/Unit_EmberSpirit_Blue.prefab` ✅ |
| 자연 (Nature) | 여우마법사 | `Assets/_Project/Prefabs/Units/Unit_FoxMagician_Blue.prefab` ✅ |

- 애니메이션: Idle 클립 재생 상태 유지 (프리팹 Animator 그대로 사용)
- CharacterPreview 레이어만 이 카메라에 렌더링 → 게임 씬 오브젝트와 격리

---

## 5. UI 레이아웃 (모바일 9:16 반응형)

```
BattleMainView
  └─ RaceSelectionView (Anchor: 가로 전체 stretch, 높이 45% 비율)
       ├─ CharacterDisplay (RawImage — RenderTexture 출력)
       │    Anchor: 중앙, 가로 80%, 높이 80% (정사각형 유지)
       ├─ RaceNameText (TMP_Text — 종족명)
       │    Anchor: CharacterDisplay 하단 중앙
       ├─ PrevButton (삼각형 버튼 — 왼쪽)
       │    Anchor: CharacterDisplay 좌측, 수직 중앙 정렬
       └─ NextButton (삼각형 버튼 — 오른쪽)
            Anchor: CharacterDisplay 우측, 수직 중앙 정렬
```

- 고정 픽셀 크기 없이 **앵커 + sizeDelta 0** (stretch) 위주로 구성
- Canvas Scaler 기준 해상도(1080×1920) 대비 비율로 설정

---

## 6. 영향 범위

### 코드 변경 파일

| 파일 | 변경 내용 |
|------|---------|
| `RaceSelectionView.cs` | 핵심 로직 전면 교체: hide/show → carousel DOTween 포지션 이동 |
| `RaceSelectionPreviewSetup.cs` | 캐릭터 초기 위치, 카메라 FOV, 인간 프리팹 경로 업데이트, 반응형 UI 생성 |

### 변경 없는 파일

| 파일 | 이유 |
|------|------|
| `RaceId.cs` | Domain enum — 그대로 |
| `LocalPlayerRace.cs` | 정적 홀더 — 그대로 |
| `GameRaceContext.cs` | 정적 홀더 — 그대로 |
| `RaceSelectionViewModel.cs` | ViewModel 로직 — 그대로 (BUG-3 수정 포함) |
| `BattleMainView.cs` | BindRace 구조 — 그대로 |
| `BattleRootView.cs` | ViewModel 생성 — 그대로 |
| `NetworkGameFlow.cs` | 종족 동기화 RPC — 그대로 |
| `BattleViewModel.cs` | GameRaceContext.Set 싱글플레이 — 그대로 |

---

## 7. 씬 초기화 작업 (에디터 스크립트로 전자동)

1. "CharacterPreview" 레이어 추가
2. RenderTexture 에셋 생성 (512×512)
3. CharacterPreviewCamera 배치 (위치/FOV/CullingMask/TargetTexture)
4. 캐릭터 3개 프리팹 인스턴스 → 초기 포지션 배정 (S=0, Human 선택 기준)
5. BattleMainView 하위 RaceSelectionView UI 생성
   - RawImage, TMP_Text, PrevButton(삼각형), NextButton(삼각형)
   - 반응형 앵커 설정
6. 모든 Inspector 슬롯 자동 연결
