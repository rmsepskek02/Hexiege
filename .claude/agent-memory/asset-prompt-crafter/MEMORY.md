# Asset Prompt Crafter Memory — Hexiege

## 프로젝트 렌더링 컨텍스트 (확정, 2026-02-27)
- 카메라: Orthographic + X축 55도 틸트 (Clash of Clans 스타일)
- 좌표계: XZ 평면 (Y=0 바닥, Y=높이)
- UI만 2D 스프라이트 — 타일/건물/유닛은 3D 메시
- 비주얼 목표: Clash of Clans/Royale 스타일 카툰/스타일라이즈드

## 에셋 현황

### 완료된 에셋
- **Pistoleer (유닛)**: `Assets/_Project/Models/Units/Pistoleer/Pistoleer.fbx`
  - 애니메이션: `Assets/_Project/Animations/Units/Pistoleer/Pistoleer_[Idle|Walk|Run|Dead|Attack].anim`
  - Avatar: Pistoleer.fbx 기준 (추가 유닛은 Copy From Other Avatar)
  - Animator: IsWalking(bool), IsDead(bool), Attack(trigger)
- **헥스 타일 (FlatTop)**: ProBuilder Cylinder로 제작 완료
  - `Assets/_Project/Prefabs/Tiles/` 에 저장
  - mat_tile_top: SG_HexTile Shader Graph (Object Space Position 기반 테두리), 밝은색 #BCBCBC, 테두리 #3A3A3A, 두께 0.02
  - mat_tile_side: #3A3A3A 단색

### 제작 필요 에셋
- **Castle**: 크고 웅장한 중세 성
- **Barracks**: 중간 크기 군사 막사
- **MiningPost**: 소형 채굴 장비

## Meshy.ai 프롬프트 패턴

### 스타일 기본 키워드 (모든 에셋 공통)
```
Clash of Clans style, stylized cartoon, isometric game asset, mobile game,
colorful, clean geometry, low-poly to mid-poly, orthographic top-down view
```

### 유닛 프롬프트 구조
```
[스타일] humanoid character, [직업/설명], T-pose, rigged humanoid,
Mixamo compatible, [색상/재질], game ready
```

### 건물 프롬프트 구조
```
[스타일] [건물 타입], isometric building, static mesh, [크기 힌트],
[색상/재질 특징], game asset, clean silhouette
```

### 타일 프롬프트 구조
```
[스타일] flat hexagonal tile, FlatTop orientation, 1.0 unit width,
0.1-0.2 unit height, [바이옴/재질], game terrain tile
```

## FBX 임포트 설정 (Unity)

### 유닛 (리깅 있음)
- Scale Factor: 1.0
- Rig Type: Humanoid
- Avatar: Copy From Other Avatar (Pistoleer Avatar 사용)
- Animation: Extract animations from FBX OR 별도 .anim 파일 사용
- 애니메이션 네이밍: `[UnitName]_[State].anim` (예: Pistoleer_Attack.anim)

### 건물/타일 (Static Mesh)
- Scale Factor: 확인 필요 (HexMetrics 기준 1.0 unit = 1m)
- Rig Type: None
- Animation: None
- Read/Write: Off (최적화)

## 에셋 폴더 구조
```
Assets/_Project/
├── Models/Units/[UnitName]/[UnitName].fbx
├── Models/Buildings/[BuildingName]/[BuildingName].fbx
├── Models/Tiles/HexTile/HexTile_[Type].fbx
├── Animations/Units/[UnitName]/[UnitName]_[State].anim
├── Textures/ → tex_[name]_albedo.png
├── Materials/ → mat_[name].mat
└── Prefabs/Units/, Buildings/, Tiles/
```

## Mixamo 연동 (Pistoleer 기준)
- FBX 업로드 → Auto Rigger → Humanoid 리깅
- 다운로드: FBX for Unity, Without Skin (애니메이션만)
- 애니메이션 클립 이름: Unity에서 `[UnitName]_[State].anim`으로 명명
- Attack 클립: "Firing Pistol" 또는 유사한 사격 애니메이션 선택

## 머티리얼 공통 설정 (확정)
- **Smoothness 기본값: 0.3** — Roughness 맵 대신 슬라이더로 통일 (모든 Meshy.ai 모델 기본)
- **예외: 금속 오브젝트(권총 등) → 0.5** (실제 테스트 기준, 반사감이 더 자연스러움)
- **Emission: OFF** — Meshy.ai FBX에서 _EmissionColor=(1,1,1,1)로 추출될 수 있음 → 반드시 제거 (흰색 버그)
- Metallic 맵: sRGB 체크 해제 (선형 색공간)
- Normal 맵: 파란색 단색이면 사용 가능, 흰/회색 단색이면 사용 금지
- Roughness 맵: URP 미지원 → 무시 (Smoothness 슬라이더로 대체)

## 알려진 제약사항
- 모바일 최적화: 로우~미드폴리 유지 (유닛 1,000~3,000 tri, 건물 500~2,000 tri)
- 텍스처: 2의 거듭제곱 해상도 (512×512 or 1024×1024 권장)
- Albedo만 사용 (노말맵/스페큘러 필요 시 별도 언급)
