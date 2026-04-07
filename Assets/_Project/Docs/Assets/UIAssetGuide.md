# UI 에셋 제작 가이드

> **제작 흐름**: Gemini로 2D 이미지 생성 → Unity Sprite로 Import
>
> 공통 규칙(프로젝트 컨셉, 프롬포트 원칙, 공통 이미지 조건) → [CommonAssetGuide.md](CommonAssetGuide.md)

---

## UI 에셋 종류

현재 사용 중이거나 사용 예정인 UI 에셋 카테고리:

| 카테고리 | 설명 | 예시 |
|---------|------|------|
| **아이콘** | HUD 및 패널에 사용하는 소형 이미지 | 골드 아이콘, 인구 아이콘, 타이머 아이콘 |
| **버튼** | 탭, 액션 버튼 배경 | 확인 버튼, 생산 버튼, 탭 버튼 |
| **패널 배경** | 팝업/패널 배경 프레임 | 건물 정보 패널, 생산 패널 |
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

## Gemini 요청 템플릿

아래 템플릿을 복사해서 [대괄호] 항목만 채워 Gemini에게 요청한다.

```
다음 조건에 맞는 UI 이미지를 생성해줘. 이 이미지는 모바일 게임 UI에 직접 사용할 거야.

[에셋 정보]
- 종류: [아이콘 / 버튼 / 패널 배경 / 장식 요소]
- 이름 / 설명: [예: 골드 아이콘, 동전 모양의 HUD 자원 아이콘]
- 사용 위치: [예: HUD 상단, 건물 정보 패널]

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
| 아이콘 | `icon_[이름]` | `icon_gold.png`, `icon_population.png` |
| 버튼 | `btn_[상태]_[이름]` | `btn_normal_confirm.png` |
| 패널 배경 | `bg_[이름]` | `bg_panel_building.png` |
| 프레임 | `frame_[이름]` | `frame_hud.png` |
| 장식 | `deco_[이름]` | `deco_divider.png`, `deco_badge.png` |

### 폴더 구조

```
Assets/_Project/UI/Sprites/
  ├── Icons/
  ├── Buttons/
  ├── Backgrounds/
  └── Decorations/
```
