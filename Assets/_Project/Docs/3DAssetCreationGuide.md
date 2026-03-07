# 3D 에셋 제작 가이드 (Gemini → Nano Banana → Meshy AI)

> 클로드 토큰 없이 3D 게임 에셋 및 UI 이미지를 제작하는 워크플로우

---

## 워크플로우 개요

### 3D 에셋 (유닛 / 건물 / 오브젝트)
```
1. Gemini          → 이미지 생성용 프롬포트 작성
2. Nano Banana     → 프롬포트로 레퍼런스 이미지 생성
3. Meshy AI        → 이미지를 3D 모델로 변환
4. Unity           → FBX/GLB Import + 애니메이션 설정
```

### UI 이미지
```
1. Gemini          → 이미지 생성용 프롬포트 작성
2. Nano Banana     → 프롬포트로 UI 이미지 생성 (3D 변환 없음)
3. Unity           → Sprite Import + UI 배치
```

---

## 1단계: Gemini — 이미지 프롬포트 작성

### 기본 요청 템플릿

Gemini에게 직접 프롬포트를 생성하도록 요청한다.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: (유닛 / 건물 / 오브젝트)
- 이름/설명: (예: 권총병 유닛, 중세 판타지 스타일)
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭

[이미지 조건 — 반드시 포함]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면 또는 55도 이소메트릭 뷰
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, game asset, clean silhouette
- 포즈: 자연스러운 대기 포즈 (유닛인 경우 — T-pose는 Meshy AI에서 별도 지정 가능, 프롬포트 불필요)

[추가 요구사항]
- Meshy AI image-to-3D 변환에 최적화
- 디테일은 선명하게, 실루엣은 명확하게
```

### Gemini 활용 팁

- **스타일 레퍼런스 명시**: "Clash of Clans 스타일" 처럼 구체적인 게임을 언급하면 스타일이 일관됨
- **부정 프롬포트 요청**: "포함하지 말아야 할 요소도 작성해줘" 라고 추가 요청
- **여러 변형 요청**: "동일 캐릭터의 프롬포트 3가지 변형을 작성해줘" — 다양한 시도 가능
- **Meshy 목적 명시**: "Meshy AI image-to-3D 변환용"이라고 반드시 언급 → 배경 처리, 실루엣 명확도 최적화

---

## 2단계: Nano Banana — 이미지 생성

### 프롬포트 구조

```
[주제], [스타일], [앵글], [조명], [배경], [품질 키워드], --no [제외 요소]
```

### 실전 예시 — 권총병 유닛

**Positive:**
```
medieval pistoleer soldier, cartoon stylized game character,
Clash of Clans art style, front view,
full body, clean white background,
soft even lighting, no harsh shadows,
vibrant colors, sharp details, clean silhouette,
game asset, 3D render style, high quality
```

**Negative (--no / 제외):**
```
background, shadow on ground, motion blur,
cropped, partial body, dark lighting,
realistic, photorealistic, complex background,
multiple characters, accessories overlapping silhouette
```

### 실전 예시 — 병영 건물 (Barracks)

**Positive:**
```
medieval barracks building, cartoon stylized,
Clash Royale art style, isometric 55 degree view,
full building visible, pure white background,
soft top-down lighting, vibrant warm colors,
stone and wood materials, clean architecture,
game asset, 3D render style
```

**Negative:**
```
background, interior, cross-section, partial view,
realistic texture, dark shadows, fog,
characters nearby, vegetation overlapping building
```

### 핵심 규칙

| 조건 | 이유 |
|------|------|
| **흰색/단색 배경** | Meshy AI가 주체와 배경을 정확히 분리 |
| **전신/전체 보이게** | 일부 잘린 모델은 3D 변환 시 왜곡 발생 |
| **균일한 조명** | 강한 그림자가 모델에 텍스처처럼 구워짐 |
| **명확한 실루엣** | 복잡한 외곽선은 3D 메시가 엉킴 |
| **55도 뷰 (건물)** | 카메라 55도 틸트 기준 이소메트릭 게임에 자연스러운 3D 변환 |

### 여러 장 생성 전략

- 동일 프롬포트로 **4~6장** 생성 후 가장 실루엣이 명확한 것 선택
- 유닛은 **정면뷰 + 측면뷰** 두 장 생성해 Meshy에 멀티뷰로 입력하면 품질 향상

---

## 3단계: Meshy AI — 이미지 → 3D 변환

### 변환 모드 선택

| 모드 | 사용 시기 |
|------|----------|
| **Image to 3D** | Nano Banana로 만든 이미지가 있을 때 (권장) |
| **Text to 3D** | 빠른 초안이 필요할 때 (품질 낮음) |
| **Multi-view to 3D** | 정면+측면+후면 이미지가 있을 때 (최고 품질) |

### Image to 3D 설정

```
- AI Refine: ON (메시 품질 향상, 크레딧 소모 증가)
- Topology: 게임용 → "Low Poly" 또는 "Mid Poly" 선택
- Texture Resolution: 1024 (모바일 게임) / 2048 (PC)
- PBR Texture: ON (Unity에서 자연스러운 재질 표현)
```

### 유닛 — T-pose 및 애니메이션 리깅

유닛 모델은 반드시 **Rigging** 단계 포함:

```
1. 3D 변환 완료 후 → "Animate" 탭 선택
2. T-pose 옵션: "Set T-pose" 활성화 (Meshy AI 내에서 직접 지정 가능)
3. Auto-Rig: 자동 리깅 적용
4. 필요 애니메이션: Walk / Attack / Dead
   - Walk: 이동 루프 애니메이션
   - Attack: 공격 1회 애니메이션 (클립 길이 = AttackCooldown 기준)
   - Dead: 사망 애니메이션
5. FBX로 다운로드
```

### 다운로드 포맷

| 용도 | 포맷 |
|------|------|
| 유닛 (애니메이션 포함) | FBX |
| 건물 / 오브젝트 | FBX 또는 GLB |
| Unity 직접 Import | GLB (텍스처 내장) |

---

## 4단계: UI 이미지 제작 (Nano Banana 전용)

UI는 3D 변환 없이 Nano Banana에서 생성한 이미지를 Sprite로 직접 사용한다.

### Gemini 요청 템플릿 (UI용)

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: UI 이미지
- 이름/설명: (예: 골드 아이콘, 체력바 프레임, 버튼 배경)
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰

[이미지 조건]
- 배경: 투명 또는 순수 흰색 (PNG로 내보낼 것)
- 크기: 정사각형 구도 권장 (1:1 또는 용도에 맞는 비율)
- 스타일: cartoon, flat icon, game UI, vibrant colors
- 용도: 모바일 게임 HUD
```

### UI 프롬포트 핵심 규칙

| 조건 | 이유 |
|------|------|
| **투명/흰색 배경** | Unity에서 배경 제거 용이 |
| **정사각형 구도** | UI 버튼/아이콘 표준 비율 |
| **flat icon 스타일** | 작은 해상도에서도 가독성 유지 |
| **단일 오브젝트** | 복잡한 구성은 UI에서 코드로 조합 |

### Unity Sprite Import 설정

```
Texture Type: Sprite (2D and UI)
Sprite Mode: Single
Pixels Per Unit: 100
Filter Mode: Bilinear
Compression: 모바일 → ASTC
```

---

## 5단계: Unity Import (3D 에셋)

### 폴더 구조

```
Assets/_Project/Models/
  ├── Units/
  │   └── [유닛명]/
  │       ├── [유닛명].fbx
  │       └── Textures/
  └── Buildings/
      └── [건물명]/
          ├── [건물명].fbx
          └── Textures/
```

### Import 설정 (유닛 FBX)

```
Model 탭:
  - Scale Factor: 1 (또는 게임 스케일에 맞게 조정)
  - Read/Write Enabled: OFF

Rig 탭:
  - Animation Type: Humanoid (사람형) / Generic (그 외)

Animation 탭:
  - 클립 분리: Walk / Attack / Dead 이름으로 분리
  - Walk: Loop Time ON
  - Attack: Loop Time OFF
  - Dead: Loop Time OFF
```

### Animator Controller 설정

Hexiege 프로젝트 컨벤션:
- 스테이트 이름: `Walk`, `Attack`, `Dead` (고정)
- Walk: speed 파라미터로 이동/정지 구분 (0 = 정지)
- Attack: Trigger로 호출
- Dead: IsDead Bool로 전환

---

## 체크리스트

### 이미지 생성 전
- [ ] 에셋 종류 확정 (유닛 / 건물 / 오브젝트 / UI)
- [ ] 게임 스타일 기준 명확 (Clash of Clans/Royale 카툰 이소메트릭)
- [ ] 건물/오브젝트이면 55도 이소메트릭 뷰 지정
- [ ] 유닛이면 T-pose는 Meshy AI Animate 탭에서 별도 지정 (프롬포트 불필요)

### 이미지 선택 기준
- [ ] 배경이 흰색/단색인가
- [ ] 전신/전체가 모두 보이는가
- [ ] 실루엣이 명확한가
- [ ] 강한 그림자가 없는가
- [ ] 다른 오브젝트와 겹치지 않는가

### Meshy 변환 후
- [ ] 메시가 깨진 부분 없는가
- [ ] 텍스처가 제대로 입혀졌는가
- [ ] 유닛이면 Walk/Attack/Dead 애니메이션 포함됐는가
- [ ] Unity에서 스케일이 올바른가

---

## 에셋 명명규칙

### 3D 모델 파일 (FBX/GLB)

| 종류 | 규칙 | 예시 |
|------|------|------|
| 유닛 | `Unit_[유닛명]` | `Unit_Pistoleer.fbx` |
| 건물 | `[건물명]` (PascalCase) | `Castle.fbx`, `Barracks.fbx`, `MiningPost.fbx` |
| 오브젝트 | `[오브젝트명]` (PascalCase) | `GoldMineTile.fbx` |

### Prefab

| 종류 | 규칙 | 예시 |
|------|------|------|
| 유닛 | `Unit_[유닛명]` | `Unit_Pistoleer.prefab` |
| 건물 | `[건물명]` | `Castle.prefab`, `Barracks.prefab` |
| 타일 | `HexTile_[방향]` | `HexTile_FlatTop.prefab`, `HexTile_PointyTop.prefab` |
| 기타 | `[오브젝트명]` | `GoldMineTile.prefab`, `RallyPointMarker.prefab` |

### 텍스처 / 머티리얼

| 종류 | 규칙 | 예시 |
|------|------|------|
| 머티리얼 | `mat_[용도]` | `mat_tile_top`, `mat_tile_side` |
| 텍스처 | `[에셋명]_[맵종류]` | `Pistoleer_Albedo`, `Pistoleer_Normal` |

### UI 스프라이트

| 종류 | 규칙 | 예시 |
|------|------|------|
| 아이콘 | `icon_[이름]` | `icon_gold`, `icon_hp` |
| 버튼 | `btn_[상태]` | `btn_normal`, `btn_pressed` |
| 배경/프레임 | `bg_[이름]`, `frame_[이름]` | `bg_panel`, `frame_hud` |

### 폴더 구조

```
Assets/_Project/
  ├── Models/
  │   ├── Units/[유닛명]/          → FBX + Textures/
  │   └── Buildings/[건물명]/      → FBX + Textures/
  ├── Prefabs/
  │   ├── Units/
  │   ├── Buildings/
  │   ├── Tiles/
  │   └── Misc/
  ├── Materials/
  └── UI/
      └── Sprites/
          ├── Icons/
          ├── Buttons/
          └── Backgrounds/
```

---

## 자주 발생하는 문제

| 문제 | 원인 | 해결 |
|------|------|------|
| 3D 메시가 배경과 합쳐짐 | 배경이 흰색이 아님 | 배경 제거 후 재시도 |
| 모델 일부가 뭉개짐 | 이미지에서 해당 부위가 가려짐 | 앵글 조정해 재생성 |
| 텍스처가 어두움 | 이미지 조명이 강했음 | 조명 균일한 이미지로 재시도 |
| 애니메이션 리깅 오류 | 비인간형 실루엣 | Generic 타입으로 변경 |
| Unity 스케일 이상 | FBX Scale Factor 문제 | Import Settings Scale 조정 |
