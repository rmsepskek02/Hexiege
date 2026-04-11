# UI 에셋 제작 가이드

> **제작 흐름**: Gemini로 2D 이미지 생성 → Unity Sprite로 Import
>
> 공통 규칙(프로젝트 컨셉, 프롬포트 원칙, 공통 이미지 조건) → [CommonAssetGuide.md](CommonAssetGuide.md)

---

## UI 에셋 종류

현재 사용 중이거나 사용 예정인 UI 에셋 카테고리:

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
| **제작 흐름** | Gemini → Meshy AI → Unity | Gemini → Unity (직접) |
| **Meshy AI 변환** | 필요 | 불필요 |
| **앵글** | 정면 / 55도 이소메트릭 | 정면 플랫 (2D 기준) |
| **포즈 고려** | 필요 (유닛) | 불필요 |
| **배경** | 순수 흰색 (Meshy 분리용) | 투명 또는 순수 흰색 |
| **Unity Import** | FBX / GLB | Sprite (PNG) |

---

## 유닛 초상화 제작 흐름

> Meshy AI에서 3D 모델을 만들 때 사용했던 원본 2D 컨셉 이미지를 Gemini에 첨부하여 초상화를 생성한다.
> 동일한 원본 이미지를 레퍼런스로 사용하기 때문에 3D 모델과 시각적 일관성이 유지된다.

```
[1] Meshy AI 3D 모델 제작 시 사용했던 원본 2D 컨셉 이미지 준비
      ↓
[2] Gemini에 원본 2D 컨셉 이미지 + 아래 템플릿 프롬포트 전달
      ↓
[3] Gemini에서 2D 카툰 초상화 이미지 생성
      ↓
[4] 팀별 컬러(Red / Blue) 적용 버전 각각 생성
      ↓
[5] Unity에서 Sprite로 Import
      ↓
[6] Assets/_Project/Sprites/Units/[유닛명]/ 폴더에 배치
```

### 초상화 Gemini 요청 템플릿

원본 2D 컨셉 이미지를 Gemini에 첨부하면서 아래 내용을 함께 전달한다.

```
첨부한 2D 이미지를 참고해서 아래 조건에 맞는 2D 카툰 초상화 이미지를 생성해줘.
이 첨부 이미지는 Meshy AI에서 3D 모델을 만들 때 사용했던 원본 컨셉 이미지야.
생산 패널 초상화로 사용할 거야.

[유닛 정보]
- 유닛명: [예: Pistoleer / 권총병]
- 종족: [Human / Spirit / Transcendence]
- 팀 컬러: [Red (주황-빨간 계열) / Blue (파란 계열)]
- 역할: [예: 권총을 든 원거리 보병]

[게임 정보]
- 게임: Hexiege — 헥스 타일 기반 1v1 RTS, 모바일 세로 모드
- 비주얼: Clash of Clans / Clash Royale 풍 카툰 스타일
- 구도: 상반신 클로즈업 초상화 (얼굴~가슴 위주)

[필수 조건]
- 프롬포트는 영어로 작성하고, 각 항목의 의미를 한글로 설명해줘
- Positive / Negative 두 섹션으로 명확히 나눠줘
- 프롬포트는 하나로 합쳐서 작성해줘 (여러 개로 나누지 말 것)
- 이 에셋에 어울리는 레퍼런스를 어디서 찾으면 좋을지 알려줘

[이미지 조건]
- 배경: 투명 또는 순수 흰색
- 해상도: 1024 × 1024
- 스타일: cartoon stylized, 2D portrait, vibrant colors, bold outline
- 구도: 상반신 클로즈업 (chest-up portrait), 정면 또는 약 3/4 앵글
- 팀 컬러를 갑옷, 무기, 액세서리 등에 반영해줘
- 블루팀은 파란색 외곽선, 레드팀은 빨간색 외곽선을 포함하여 제작하며 외곽선은 얇은 선으로 제작
```

### 팀 컬러 가이드

| 팀 | 컬러 방향 | 주요 적용 부위 |
|----|---------|-------------|
| **Red** | 주황-빨간 계열 (warm red, orange-red) | 갑옷, 어깨 보호대, 무기 장식 |
| **Blue** | 파랑-청록 계열 (royal blue, teal) | 갑옷, 어깨 보호대, 무기 장식 |

> 팀 컬러는 전체 색상을 바꾸는 것이 아니라 포인트 컬러로 적용한다.
> 예: 권총병 피부색/복장 기본 톤 유지 + 어깨 갑옷만 Red 또는 Blue로 변경

---

## Gemini 요청 템플릿 (일반 UI 에셋)

아래 템플릿을 복사해서 [대괄호] 항목만 채워 Gemini에게 요청한다.

```
다음 조건에 맞는 UI 이미지를 생성해줘. 이 이미지는 모바일 게임 UI에 직접 사용할 거야.

[에셋 정보]
- 종류: [아이콘 / 버튼 / 패널 배경 / 바 / 슬롯 / 장식 요소]
- 이름 / 설명: [예: 골드 아이콘, 동전 모양의 HUD 자원 아이콘]
- 사용 위치: [예: HUD 상단, 건물 정보 패널, 생산 패널]

[게임 정보]
- 게임: Hexiege — 헥스 타일 기반 1v1 RTS, 모바일 세로 모드
- 비주얼: Clash of Clans / Clash Royale 풍 카툰 스타일
- 해상도: 1080 × 1920 기준

[필수 조건]
- 프롬포트는 영어로 작성하고, 각 항목의 의미를 한글로 설명해줘
- Positive / Negative 두 섹션으로 명확히 나눠줘
- 프롬포트는 하나로 합쳐서 작성해줘 (여러 개로 나누지 말 것)
- 이 에셋에 어울리는 레퍼런스를 어디서 찾으면 좋을지 알려줘

[이미지 조건]
- 배경: 투명 또는 순수 흰색
- 해상도: 1024 × 1024 (모든 UI 에셋 동일)
- 스타일: cartoon stylized, flat game UI, vibrant colors
- 단일 오브젝트 (아이콘 하나 또는 버튼 하나만)
- 조명: 균일하고 부드러운 조명
```

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
1:1 정사각형으로 제작하고, Unity의 9-Slice 설정으로 원하는 가로 비율로 늘려 사용한다.
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
투명 배경이 필수다.
게임 전체 테마와 어울리는 소재감(금속, 돌, 나무 등)을 사용한다.
```
추가 Positive: transparent background, decorative divider, fantasy UI ornament
추가 Negative: solid background, plain line, modern flat design
```

---

## 2D 이미지 주의사항 (UI 특화)

### 1. 선명하고 진한 색상 사용
모바일 화면에서 작게 표시되므로 탁하거나 파스텔 톤은 가독성이 떨어진다.
```
요청 키워드: vibrant colors, saturated colors, bold colors
Negative 키워드: pastel, muted, washed out, desaturated
```

### 2. 외곽선(Outline) 포함 권장
카툰 스타일 UI는 외곽선이 있어야 3D 에셋과 시각적으로 통일감이 생긴다.
```
요청 키워드: bold outline, cartoon outline, thick outline
```

### 3. 초상화는 팀별로 각각 생성
Red / Blue 두 버전을 따로 요청한다.
팀 컬러를 갑옷이나 액세서리 포인트 컬러로 적용하고 캐릭터 기본 톤은 유지한다.

---

## Unity Sprite Import 설정

```
Texture Type          : Sprite (2D and UI)
Sprite Mode           : Single
Pixels Per Unit       : 100
Filter Mode           : Bilinear
Compression           : ASTC (모바일)
Alpha Is Transparency : ON (투명 배경 사용 시)
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
