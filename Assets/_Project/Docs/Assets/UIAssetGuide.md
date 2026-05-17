# UI 에셋 제작 가이드

> **제작 흐름**: AI 이미지 생성 → Unity Sprite로 Import
>
> 공통 규칙(프로젝트 컨셉, 프롬포트 원칙, 공통 이미지 조건) → [CommonAssetGuide.md](CommonAssetGuide.md)

---

## ⚠️ 절대 규칙 — 모든 AI 도구 공통, 예외 없음

**이미지 생성 규칙**

| 규칙 | 내용 |
|------|------|
| **해상도** | **1024 × 1024** — 아이콘, 버튼, 초상화, 패널 전부 동일 |
| **배경** | **투명 (transparent)** |
| **버튼 비율** | 반드시 **1:1 정사각형**으로 제작 — 가로 비율 조정은 Unity 9-Slice로 처리 |

**프롬프트를 사용자에게 제공할 때 반드시 포함해야 할 3가지**

**1. Positive / Negative를 하나의 코드 블록 안에 함께 작성**
```
Positive: [영어 프롬프트 내용]
Negative: [제외할 영어 키워드]
```
❌ Positive 코드 블록 + Negative 코드 블록으로 분리 금지

**2. 각 항목의 의미를 한글로 설명**
- 영어 키워드가 어떤 의도인지 한글로 함께 설명

**3. 프로젝트 내 레퍼런스 에셋 언급**
- `Assets/_Project/Sprites/` 하위에 존재하는 기존 에셋과 스타일을 맞춰야 하는 경우, 어떤 에셋을 참고하면 좋은지 명시

---

## UI 에셋 종류

| 카테고리 | 설명 | 예시 |
|---------|------|------|
| **유닛 초상화** | 생산 패널에 표시되는 유닛 대표 이미지 | 피스톨리어 초상화, 스나이퍼 초상화 |
| **아이콘** | HUD 및 패널에 사용하는 소형 이미지 | 골드 아이콘, 인구 아이콘, 타이머 아이콘 |
| **버튼** | 탭, 액션 버튼 배경 | 확인 버튼, 생산 버튼, 탭 버튼 |
| **패널 배경** | 팝업/패널 배경 프레임 | 건물 정보 패널, 생산 패널 |
| **바(Bar)** | HP, 진행 상태 등을 표시하는 막대 프레임 | HP 바 프레임, 생산 진행 바 |
| **슬롯(Slot)** | 유닛 카드, 생산 큐 등의 칸 프레임 | 생산 큐 슬롯, 유닛 아이콘 슬롯 |
| **장식 요소** | 구분선, 테두리, 뱃지 | 섹션 구분선, 알림 뱃지 |

---

## 3D 에셋 제작과의 차이점

| 항목 | 3D 에셋 (유닛/건물) | UI 에셋 |
|------|-------------------|---------|
| **제작 흐름** | Gemini/GPT → Meshy AI → Unity | AI 이미지 생성 → Unity (직접) |
| **Meshy AI 변환** | 필요 | 불필요 |
| **앵글** | 정면 / 55도 이소메트릭 | 정면 플랫 (2D 기준) |
| **포즈 고려** | 필요 (유닛) | 불필요 |
| **배경** | 순수 흰색 (Meshy 분리용) | 투명 |
| **Unity Import** | FBX / GLB | Sprite (PNG) |

---

## 유닛 초상화 제작 흐름

> Meshy AI에서 3D 모델을 만들 때 사용했던 원본 2D 컨셉 이미지를 AI에 첨부하여 초상화를 생성한다.
> 동일한 원본 이미지를 레퍼런스로 사용하기 때문에 3D 모델과 시각적 일관성이 유지된다.

```
[1] Meshy AI 3D 모델 제작 시 사용했던 원본 2D 컨셉 이미지 준비
      ↓
[2] AI에 원본 2D 컨셉 이미지 + 아래 템플릿 프롬포트 전달
      ↓
[3] 2D 카툰 초상화 이미지 생성
      ↓
[4] 팀별 컬러(Red / Blue) 적용 버전 각각 생성
      ↓
[5] Unity에서 Sprite로 Import
      ↓
[6] Assets/_Project/Sprites/Units/[유닛명]/ 폴더에 배치
```

### 팀 컬러 가이드

| 팀 | 컬러 방향 | 주요 적용 부위 |
|----|---------|-------------|
| **Red** | 주황-빨간 계열 (warm red, orange-red) | 갑옷, 어깨 보호대, 무기 장식 |
| **Blue** | 파랑-청록 계열 (royal blue, teal) | 갑옷, 어깨 보호대, 무기 장식 |

> 팀 컬러는 전체 색상을 바꾸는 것이 아니라 포인트 컬러로 적용한다.
> 예: 권총병 피부색/복장 기본 톤 유지 + 어깨 갑옷만 Red 또는 Blue로 변경

---

## 공통 Positive / Negative 키워드

### 공통 Positive
```
cartoon stylized, game UI asset, vibrant colors, clean design,
soft even lighting, mobile game UI, Clash of Clans art style,
sharp details, high quality, flat icon style
```

### 공통 Negative
```
background, shadow on ground, photorealistic, complex background,
3D render, depth of field, motion blur, watermark, text label,
multiple objects in frame, dark gloomy colors, muted colors
```

---

## UI 에셋 종류별 주의사항

### 유닛 초상화
상반신 클로즈업으로, 유닛의 얼굴과 무기가 잘 보여야 한다.
팀 컬러(Red / Blue)를 반드시 구분하여 각각 별도 생성한다.
```
추가 Positive: chest-up portrait, 2D cartoon character portrait,
               expressive face, bold outline, fantasy game character
추가 Negative: full body, action pose, background scenery,
               multiple characters, overly complex armor detail
```

### 아이콘
작은 크기(64px 이하)에서도 알아볼 수 있어야 한다.
외곽선이 단순하고 실루엣이 명확한 디자인이 적합하다.
```
추가 Positive: single icon, bold outline, clear silhouette, flat icon design, recognizable at small size
추가 Negative: fine details, thin lines, complex patterns, heavy gradients
```

### 버튼
텍스트가 들어갈 공간을 고려해 중앙 영역은 단순하게 유지한다.
```
추가 Positive: button frame, rounded corners, cartoon game button, center area clean for text
추가 Negative: text on button, icon on button, overly decorative center
```
> Unity Import 후 Sprite Editor에서 9-Slice 경계선을 잡아주면 모서리 품질을 유지한 채 어떤 비율로도 사용 가능하다.

### 패널 배경
내부에 들어갈 콘텐츠(텍스트, 아이콘)를 가리지 않도록 테두리/프레임 형태로 제작한다.
```
추가 Positive: panel frame, decorative border, game UI panel background, empty center area
추가 Negative: filled solid center, busy pattern inside, text, icons inside panel
```

### 바(Bar / Progress Bar)
HP 바, 생산 진행 바 등 수치를 시각화하는 프레임이다.
내부에 채워지는 색상(채움 이미지)과 외곽 프레임을 별도로 분리하여 제작한다.
```
추가 Positive: progress bar frame, health bar UI, game HUD bar, horizontal bar shape,
               empty inside (fill handled in Unity), rounded ends
추가 Negative: filled color inside bar, text inside, vertical orientation (가로형이 기본)
```
> Unity에서 Image 컴포넌트의 Fill Amount로 채움 비율을 조절하므로
> 프레임(테두리)과 채움(Fill) 이미지를 각각 별도로 만드는 것이 좋다.

### 슬롯(Slot)
유닛 카드, 생산 큐, 자원 슬롯 등 특정 오브젝트가 들어가는 칸 프레임이다.
내부는 비어 있어야 하며 Unity에서 Image를 배치할 공간을 확보해야 한다.
```
추가 Positive: card slot frame, inventory slot, game UI slot, empty inside,
               decorative border, slight inset shadow effect
추가 Negative: item inside slot, filled center, busy pattern, text
```

### 장식 요소 (구분선, 뱃지 등)
게임 전체 테마와 어울리는 소재감(금속, 돌, 나무 등)을 사용한다.
```
추가 Positive: transparent background, decorative divider, fantasy UI ornament
추가 Negative: solid background, plain line, modern flat design
```

---

## Unity Sprite Import 설정

```
Texture Type          : Sprite (2D and UI)
Sprite Mode           : Single
Pixels Per Unit       : 100
Filter Mode           : Bilinear
Compression           : ASTC (모바일)
Alpha Is Transparency : ON
```

---

## 에셋 명명 규칙

| 종류 | 규칙 | 예시 |
|------|------|------|
| 유닛 초상화 | `[유닛명]_portrait_[팀]` | `pistoleer_portrait_red.png`, `sniper_portrait_blue.png` |
| 아이콘 | `ui_icon_[이름]` | `ui_icon_gold.png`, `ui_icon_population.png` |
| 버튼 | `ui_btn_[이름]_[상태]` | `ui_btn_gold_normal.png`, `ui_btn_cancel.png` |
| 패널 배경 | `ui_panel_[스타일]` | `ui_panel_dark.png`, `ui_panel_light.png` |
| 바 | `ui_bar_[이름]_[종류]` | `ui_bar_hp_frame.png`, `ui_bar_progress_frame.png` |
| 슬롯 | `ui_slot_[이름]` | `ui_slot_queue.png`, `ui_slot_icon_dark.png` |
| 장식 | `ui_deco_[이름]` | `ui_deco_divider.png`, `ui_deco_badge.png` |

### 폴더 구조

```
Assets/_Project/Sprites/
  ├── Units/
  │   └── [유닛명]/
  │       ├── [유닛명]_portrait_red.png   ← 유닛 초상화 (Red 팀)
  │       └── [유닛명]_portrait_blue.png  ← 유닛 초상화 (Blue 팀)
  └── UI/
      ├── Icons/       ← ui_icon_*.png
      ├── Buttons/     ← ui_btn_*.png
      ├── Panels/      ← ui_panel_*.png
      ├── Bars/        ← ui_bar_*.png
      ├── Slots/       ← ui_slot_*.png
      └── Decorations/ ← ui_deco_*.png
```
