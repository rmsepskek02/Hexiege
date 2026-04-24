# 3D 에셋 제작 가이드

> **제작 흐름**: Gemini로 2D 이미지 생성 → Meshy AI로 3D 모델 변환 → Unity Import
>
> 공통 규칙(프로젝트 컨셉, 프롬포트 원칙, 공통 이미지 조건) → [CommonAssetGuide.md](CommonAssetGuide.md)

### 파티클 및 이펙트 효과 제외 (No Particles/VFX)
3D 모델링 변환 시 파티클이나 마법 효과(빛나는 오라, 불꽃, 연기, 번개 등)는 메시에 포함되어 형태를 심각하게 왜곡시킨다. 이펙트는 Unity 인게임에서 별도로 구현해야 한다.
```
요청 키워드: no particles, no special effects, clean mesh, solid form
Negative 키워드: particle effects, magic auras, glowing energy, smoke, fire, vfx, sparks, floating elements
```

## 팀 색상 구분 규칙

블루팀과 레드팀은 **팀 포인트 컬러 하나**로만 구분한다.
형태, 구조, 장비는 완전히 동일하며 **색상만 다르다.**

### 제작 방식

1. **블루팀 에셋만 제작** — 프롬포트는 블루 컬러 기준으로 작성
2. **레드팀은 텍스처 재활용** — 블루 텍스처의 파란색 영역을 빨간색으로 교체하여 사용

→ 프롬포트를 작성할 때는 **항상 블루팀 기준**으로 작성한다.

### 팀 색상

| 팀 | 색상 | 프롬포트 키워드 예시 |
|----|------|-------------------|
| **블루팀** (제작 기준) | 파란색 | `blue accents`, `blue banners`, `blue insignia`, `blue armor trim`, `royal blue emblem` |
| **레드팀** (텍스처 교체) | 빨간색 | 별도 프롬포트 작성 불필요 — 텍스처 색상 교체로 생성 |

### 팀 색상 적용 위치

- **건물**: 지붕 색, 깃발, 문장(紋章), 창문 빛
- **유닛**: 어깨패드, 망토, 방패 문양, 헬멧 장식
- **공통 중립 오브젝트** (GoldMineTile 등): 팀 색 불필요

### 블루 → 레드 변환 프롬포트

블루팀 이미지를 Gemini에 첨부한 뒤 아래 프롬포트로 레드팀 버전을 생성한다.

```
-Transform this Blue Team game asset into the Red Team version. -Change all blue areas, including fabric, glowing energy effects, and painted accents, -to a vibrant and highly saturated crimson red (#FF3030). -Maintain the exact same character pose, silhouette, and metallic/leather material textures. -Ensure the soft 3D cartoon stylized rendering and even lighting remain identical to the original. -The output must be a clean game asset on a pure white background. -Do not include: background, floor shadows, desaturated colors, -changes to the character's shape or pose, realistic textures, artifacts, blurry details. +Transform this Blue Team game asset into the Red Team version by changing the color palette. +Replace all blue areas (including fabric, painted accents, and decorative elements) +with a vibrant and highly saturated crimson red (#FF3030). +Keep the asset's original structure, silhouette, and material textures (metal, wood, leather) exactly the same. +Ensure the lighting, cartoon stylized rendering, and high-quality details are preserved. +The output must be a clean asset on a pure white background. +Do not include: background, floor shadows, changes to the asset's shape, desaturated colors, +realistic textures, artifacts, blurry details, particle effects, or magic VFX.
```

---

## 종족 컨셉

### 인간계 (Human)
- **컨셉**: 중세 시대 인간 군대 — 검, 창, 활, 총기 등 중세 무기 전반을 사용
- **아트 방향**: 두툼한 갑옷 + 다양한 무기 조합, 밝은 금속 + 원색 천 소재감
- **무기 범위**: 총기류에 한정하지 않으며, 검사/창병/궁수/기사 등 중세 병과 전반 포함
- **키워드**: `medieval warrior`, `armored soldier`, `fantasy knight`, `metal armor`, `colorful cloth`, `medieval weapon`
- **레퍼런스**: CoC 바바리안/궁수/기사 계열

### 정령계 (Spirit / Elemental)
- **컨셉**: 다양한 원소 정령 — 현재 불꽃 구현, 이후 물/땅/전기/빛/어둠 등 추가 예정
- **아트 방향**: 반투명 에너지 몸체, 원소별 대표색, 인간형 실루엣
- **원소별 색상 참고**:
  | 원소 | 대표 색상 | 키워드 추가 예시 |
  |------|---------|---------------|
  | 불 (Fire) | 주황/빨강 | `flame aura`, `ember glow`, `fire spirit` |
  | 물 (Water) | 파랑/청록 | `water spirit`, `aqua glow`, `tidal aura` |
  | 땅 (Earth) | 갈색/초록 | `earth spirit`, `stone body`, `moss aura` |
  | 전기 (Lightning) | 하늘/노랑 | `lightning spirit`, `electric glow`, `spark aura` |
  | 빛 (Light) | 흰색/금색 | `light spirit`, `radiant glow`, `holy aura` |
  | 어둠 (Dark) | 보라/검정 | `dark spirit`, `shadow glow`, `void aura` |
- **공통 키워드**: `elemental spirit`, `glowing body`, `translucent energy form`, `humanoid silhouette`
- **레퍼런스**: Genshin Impact 정령 캐릭터, League of Legends 정령 스킨

### 초월계 (Transcendence)
- **컨셉**: 의인화된 동물과 식물이 혼재 — 동물은 주로 유닛, 식물은 주로 건물로 사용 (식물 유닛도 가능)
- **아트 방향**:
  - **동물 유닛**: 동물 특징(귀/꼬리/체형) + 판타지 장비, 자연 소재감 (가죽/나무/꽃)
  - **식물 건물**: 거대 나무/식물 형태의 구조물, 자연과 마법의 조화
  - **식물 유닛**: 이동 가능한 소형 식물 생명체, 덩굴/꽃/버섯 등 형태 다양
- **동물 키워드**: `anthropomorphic animal warrior`, `furry fantasy knight`, `beast character`, `animal ears`, `nature armor`
- **식물 키워드**: `plant creature`, `living plant warrior`, `nature spirit`, `vine body`, `mushroom creature`, `floral armor`
- **레퍼런스**: Clash Royale 동물 캐릭터, 짐승화(Kemono) 판타지 스타일, Hearthstone 자연 계열 카드

---

## Gemini 요청 템플릿

아래 템플릿을 복사해서 [대괄호] 항목만 채워 Gemini에게 요청한다.

```
다음 조건에 맞는 2D 이미지를 생성해줘. 이 이미지는 Meshy AI로 3D 모델로 변환할 예정이야.

[에셋 정보]
- 종류: [유닛 / 건물 / 오브젝트]
- 이름 / 설명: [예: 인간계 저격수 유닛, 판타지 군인 스타일]
- 종족: [Human / Spirit / Transcendence]
- 팀: [블루팀 / 팀 구분 없음]

[게임 정보]
- 게임: Hexiege — 헥스 타일 기반 1v1 RTS, 모바일 세로 모드
- 비주얼: Clash of Clans / Clash Royale 풍 카툰 3D 이소메트릭
- 뷰: Orthographic 55도 틸트 이소메트릭 카메라

[필수 조건]
- 프롬포트는 영어로 작성하고, 각 항목의 의미를 한글로 설명해줘
- Positive / Negative 두 섹션으로 명확히 나눠줘
- 프롬포트는 하나로 합쳐서 작성해줘 (여러 개로 나누지 말 것)
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화할 것

[이미지 조건]
- 배경: 순수 흰색 (pure white background)
- 해상도: 1024 × 1024
- 전신 / 전체 건물이 모두 보일 것 (잘리면 안 됨)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 실루엣: 외곽선이 명확할 것
- 스타일: cartoon stylized, game asset, vibrant colors
```

---

## 공통 Positive / Negative 키워드

### 공통 Positive
```
cartoon stylized, game asset, vibrant colors, clean silhouette,
soft even lighting, pure white background, full body visible,
Clash of Clans art style, sharp details, high quality render, 3D render style
```

### 공통 Negative
```
background, shadow on ground, motion blur, cropped body,
dark lighting, realistic, photorealistic, complex background,
multiple characters in frame, accessories overlapping silhouette,
fog, depth of field, lens flare, watermark, text
```

---

## 유닛 vs 건물 — 차이점

| 항목 | 유닛 | 건물 |
|------|------|------|
| **앵글** | 정면 뷰 (front view) | 55도 이소메트릭 뷰 |
| **포즈** | 자연스러운 대기 포즈 (idle pose) | 해당 없음 |
| **Meshy 후처리** | T-pose 설정 + 리깅 + Walk/Attack/Dead 애니메이션 | 리깅 불필요 |
| **팀 색상 위치** | 어깨패드, 망토, 헬멧 장식, 방패 문양 | 지붕, 깃발, 문장, 창문 빛 |
| **Negative 추가** | `sitting, crouching, running, weapon floating` | `characters nearby, interior visible, cross-section` |

---

## 프롬포트 예시 (블루팀 기준)

> 레드팀은 별도 제작 없이 블루 텍스처의 파란색 영역을 빨간색으로 교체하여 사용한다.

### 유닛 예시 (Human — Assault)
**Positive:**
```
medieval assault soldier, heavy armor, machine gun,
cartoon stylized game character, Clash of Clans art style,
front view, full body, idle pose,
blue armor trim, blue shoulder pads, blue banner insignia, royal blue emblem,
soft even lighting, pure white background,
vibrant colors, clean silhouette, game asset, 3D render style
```
**Negative:**
```
background, shadow on ground, motion blur,
dark lighting, realistic, photorealistic, complex background,
cropped, partial body, multiple characters in frame,
sitting, crouching, running, weapon floating,
accessories overlapping silhouette, fog, watermark, text
```

### 건물 예시 (Human — Barracks)
**Positive:**
```
medieval barracks building, stone and wood construction,
cartoon stylized, Clash Royale art style,
isometric 55 degree view, full building visible,
blue roof, blue banners, royal blue flag, blue insignia on gate,
pure white background, soft top-down lighting,
vibrant warm colors, clean architecture, game asset, 3D render style
```
**Negative:**
```
background, interior visible, cross-section, partial view,
realistic texture, dark shadows, fog,
characters nearby, vegetation overlapping building,
motion blur, watermark, text,
fire, smoke, glowing auras, particle effects
```

---

## 2D 이미지 주의사항 (Meshy AI 변환 특화)

Gemini로 생성한 2D 이미지를 Meshy AI에서 3D로 변환할 때, **이미지 품질이 3D 결과물 품질을 직접 결정**한다.

### 1. 조명은 균일하고 부드럽게 (강한 그림자 금지)
강한 방향성 조명은 그림자를 만들고, 그 그림자가 텍스처에 구워진다.
오른쪽에 강한 조명이 있으면 왼쪽 면이 영구적으로 어둡게 표현된 3D 모델이 만들어진다.
```
요청 키워드: soft even lighting, no harsh shadows, diffuse lighting, flat lighting
```

### 2. 실루엣(외곽선)이 명확해야 함
팔, 무기, 날개 등이 몸통에 붙거나 겹치면 Meshy AI가 별개의 파츠로 인식하지 못해 메시가 뭉쳐진다.
```
요청 키워드: clean silhouette, clear outline, no overlapping parts
```

### 3. 전신/전체 오브젝트가 모두 보여야 함
잘린 부분은 3D 변환 시 해당 부위가 없거나 뭉개진 형태로 생성된다.
```
요청 키워드: full body, full building visible, no cropping
```

### 4. 앵글 기준 엄수
앵글이 맞지 않으면 3D 모델의 비율이 틀어진다.

| 에셋 종류 | 앵글 | 이유 |
|---------|------|------|
| **유닛** | 정면 뷰 (front view) | 좌우 대칭 확인, 리깅 품질 향상 |
| **건물 / 오브젝트** | 55도 이소메트릭 뷰 | 게임 내 카메라 각도와 일치 |

```
유닛 키워드: front view, facing forward, symmetrical pose
건물 키워드: isometric 55 degree view, top-down angle
```

### 5. 포즈는 대기 자세 (유닛 한정)
달리거나 공격하는 포즈는 팔다리가 몸통과 겹쳐 메시가 뭉친다.
```
요청 키워드: idle pose, standing pose, neutral stance, arms slightly away from body
Negative 키워드: running, attacking, jumping, sitting, crouching
```

---

## 이미지 품질 체크리스트 (Meshy AI 변환 전)

| 기준 | 이유 |
|------|------|
| **흰색 배경** | Meshy AI가 주체와 배경을 정확히 분리 |
| **전신/전체 보이게** | 일부 잘린 모델은 3D 변환 시 왜곡 발생 |
| **균일한 조명** | 강한 그림자가 모델에 텍스처처럼 구워짐 |
| **명확한 실루엣** | 복잡한 외곽선은 3D 메시가 엉킴 |
| **이펙트 없음** | 파티클 등이 3D 메시의 일부로 굳어지는 현상 방지 |
| **정면뷰 (유닛)** | 리깅을 위한 좌우 대칭 확인 가능 |
| **55도뷰 (건물)** | 이소메트릭 카메라에 자연스러운 3D 변환 |

---

## Meshy AI 변환 설정

### Image to 3D 설정
```
- AI Refine: ON
- Topology: Low Poly 또는 Mid Poly (모바일 게임용)
- Texture Resolution: 1024 (모바일)
- PBR Texture: ON
```

### 유닛 애니메이션 (Animate 탭)
```
1. Set T-pose 활성화
2. Auto-Rig 적용
3. 필요 애니메이션: Walk (Loop ON) / Attack (Loop OFF) / Dead (Loop OFF)
4. FBX로 다운로드
```

### 건물 / 오브젝트
```
- 리깅 불필요
- FBX 또는 GLB로 다운로드
```

---

## 에셋 명명 규칙

| 종류 | 규칙 | 예시 |
|------|------|------|
| 유닛 FBX | `Unit_[유닛명]` | `Unit_Pistoleer.fbx` |
| 건물 FBX | `[건물명]` | `Castle.fbx`, `Barracks.fbx` |
| 유닛 Prefab (팀 포함) | `Unit_[유닛명]_[팀]` | `Unit_Assault_Blue.prefab` |
| 건물 Prefab (팀 포함) | `Building_[건물명]_[팀]` | `Building_Barracks_Red.prefab` |

---

> 에셋 완성 현황 및 제작 예정 목록 → [AssetList.md](AssetList.md)
