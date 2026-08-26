# Asset Prompt Crafter Memory — Hexiege

## 프로젝트 렌더링 컨텍스트 (확정, 2026-02-27)
- 카메라: Orthographic + X축 55도 틸트 (Clash of Clans 스타일)
- 좌표계: XZ 평면 (Y=0 바닥, Y=높이)
- UI만 2D 스프라이트 — 타일/건물/유닛은 3D 메시
- 비주얼 목표: Clash of Clans/Royale 스타일 카툰/스타일라이즈드

## 에셋 현황

### 완료된 에셋
- **Pistoleer (유닛)**: `Assets/_Project/Models/Units/Pistoleer/Pistoleer.fbx`
  - 프리팹: `Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_{Blue,Red}.prefab` (애니메이션 작동 확인 완료)
  - 애니메이션 클립: `Assets/_Project/Animations/Units/Pistoleer/Pistoleer_[Walk|Dead|Shoot].anim`
  - Avatar: Pistoleer.fbx 기준 (추가 유닛은 Copy From Other Avatar)
  - Animator Controller: `IsDead`(bool) 파라미터 1개 + `Any State → Dead` 트랜지션만 존재
  - 스테이트 이름 (코드와 반드시 일치): **Walk**, **Attack**, **Dead**
  - Walk = 기본 Entry 스테이트, Loop 재생
  - Attack/Walk 전환은 Animator.Play() 직접 호출 (트랜지션 없음)
- **헥스 타일 (FlatTop)**: ProBuilder Cylinder로 제작 완료
  - 프리팹: `Assets/_Project/Prefabs/Tiles/` 에 저장 (테스트 확인 완료)
  - mat_tile_top: `SG_HexTile` Shader Graph, mat_tile_side: #3A3A3A 단색 Lit

### SG_HexTile 쉐이더 그래프 상세 (경로: Assets/_Project/Materials/ 또는 Shaders/)
**그래프 구조**:
```
Color (TileColor, 기본 #BCBCBC) ──────→ A ──┐
                                             Lerp → Fragment [Base Color]
Color (BorderColor, 고정 #3A3A3A) ──→ B ──┤
                                             T ←── HexBorder.Border(1)

Position (Object Space) ──→ HexBorder.Position(3)
0.02 (float literal) ──────→ HexBorder.BorderSize(1)

Metallic = 0, Smoothness = 0.5, Emission = 0
```

**HexBorder Custom Function HLSL**:
```hlsl
float2 p = abs(float2(Position.x, Position.z));
float d = max(p.y, p.x * 0.866 + p.y * 0.5);
Border = step(0.433 - BorderSize, d);
// Border=1이면 테두리, Border=0이면 중앙 → Lerp(TileColor, BorderColor, Border)
```

**팀 색상 연동 (중요)**:
- Blackboard에 `_BaseColor` (Color, Reference: `_BaseColor`, Default: #BCBCBC) 프로퍼티 추가 필요
- 상단 Color 노드(TileColor)를 `_BaseColor` 프로퍼티 노드로 교체해야 `material.color = teamColor` 동작
- 미수정 시 `HexTileView.UpdateColor()`가 호출돼도 색 변화 없음 (하드코딩 상태)
- `HexTileView.cs`에서 `material.color` 대신 `material.SetColor("_BaseColor", ...)` 사용 필요
  - `material.color`는 내부적으로 `_Color`를 변경하므로 커스텀 Shader Graph에는 동작 안 함 (확인됨)

**Border 색상 커스터마이징**:
- 하단 Color 노드(#3A3A3A)도 `_BorderColor` Blackboard 프로퍼티로 교체 가능 (선택 사항)

### 완료된 건물 에셋 (2026-03-01 확정)
- **모든 건물/오브젝트는 헥스 타일 1칸 안에 배치 — XZ 풋프린트 1.0 unit 이내 (공통)**
- 건물 간 크기 차이는 높이(Y축)로만 표현
- 프리팹 구조: 빈 루트(0,0,0) + 자식 FBX 메시(회전/Y 보정)
- 건물 프리팹 저장: `Assets/_Project/Prefabs/Buildings/Building_[Name].prefab`
- 머티리얼 저장: `Assets/_Project/Materials/Buildings/mat_[buildingname].mat`
- 텍스처 저장: `Assets/_Project/Texture/Buildings/tex_[name]_albedo.png`

| 건물 | Scale Factor | 자식 Y Position | 자식 Y Rotation |
|------|-------------|----------------|----------------|
| Castle | 50 | 0.43 | 45° |
| Barracks | 40 | 0.33 | -135° |
| MiningPost | 35 | 0.24 | 45° |
| GoldMineTile | 35 | 0.14 | 0° |

### 완료된 기타 에셋
- **금광 타일 오브젝트** (2026-03-01 완료): 크리스탈 바위 더미
  - 프리팹 경로: `Assets/_Project/Prefabs/Tiles/GoldMineTile.prefab`
  - 건물이 아닌 타일 오버레이 — HexGridRenderer에서 생성, MiningPost 건설 시 숨김
- **랠리포인트 마커** (2026-03-01 확정)
  - 프리팹 경로: `Assets/_Project/Prefabs/Misc/RallyPointMarker.prefab`
  - 구조: FBX 단일 루트 (빈 루트 불필요 — GameConfig에서 코드로 위치/회전 제어)
  - Scale Factor: 25
  - 위치/회전 보정: `GameConfig.RallyMarkerOffset`, `GameConfig.RallyMarkerEuler`로 Inspector에서 조정

### 유닛 프리팹 보정값 (확정)
| 유닛 | Scale Factor | 자식 Y Position | 자식 Y Rotation |
|------|-------------|----------------|----------------|
| Pistoleer | 0.25 | -0.1 | 30° |
| Pistol(무기 FBX) | 0.25 | - | - |

- 유닛 프리팹 구조: 빈 루트(UnitView 부착) + 자식 FBX(보정값 설정)
- Y Position 보정: 자식 FBX 로컬 Y (GameConfig.UnitYOffset에 더해짐)
- Y Rotation 보정: 자식 FBX 로컬 Y (ApplyDirection() Y 회전에 더해짐)

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
- Scale Factor: 모델마다 개별 설정 (Castle=50, Barracks=40, MiningPost=35 — Meshy.ai cm 단위 출력 기준)
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
- **Meshy.ai 생성 모델은 리메시 후 약 3,000 tri로 제작** (건물/오브젝트 공통 기준)
- 모바일 최적화: 3,000 tri 이하 유지 (유닛 포함 동일 기준)
- 텍스처: 2의 거듭제곱 해상도 (1024×1024 권장, 모바일이면 512×512 고려)
- Albedo만 사용 (노말맵/스페큘러 필요 시 별도 언급)
- 폴리카운트 초과 시: Blender Decimate Modifier (Ratio 0.7~0.8) 사용
- MiningPost는 금색/랜턴 요소로 인해 Emission 버그 발생 위험이 Castle/Barracks보다 높음
- Meshy.ai가 cm 단위로 출력하면 Scale Factor 0.01 또는 Blender에서 재출력
- Shader가 Standard로 잘못 설정되면 URP에서 핑크색 — Import 후 Lit으로 교체 필수
