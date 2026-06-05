# Plan — 신규 유닛 프리팹 컴포넌트 부착

## 작업 개요 (자연어 설명)

32개 신규 유닛 프리팹에 게임 동작에 필요한 컴포넌트 5종을 부착한다.
32개를 Inspector에서 하나하나 수동으로 작업하면 실수가 생기기 쉬우므로,
Unity 에디터 메뉴에서 한 번만 실행하면 전부 자동으로 처리되는 1회성 스크립트를 작성한다.
스크립트 실행 완료 후 인스펙터에서 결과를 확인하고, 스크립트 파일은 삭제해도 무방하다.

---

## GameSystemRules 근거

- **GameSystemRules_Units.md 규칙 3**: 유닛 이동 결정은 서버에서만 이루어지며 클라이언트는 NetworkTransform으로 결과를 받음 → **NetworkTransform 필수**
- **GameSystemRules_Units.md 규칙 7, 8, 12, 15**: 유닛 회전(이동/전투/공격 중)은 RotateTowards 방식 → **UnitView 필수** (_rotationSpeed=270)
- **memory_units.md**: Root Motion 반드시 OFF — 이미 신규 프리팹에 ApplyRootMotion=0으로 설정되어 있음 (확인 완료)
- **memory_units.md**: Mesh Child Rotation Y = 0 — 신규 프리팹에 이미 0으로 설정되어 있음 (확인 완료)

---

## 구현 방법

### 방법: Editor 1회성 스크립트 (자동 일괄 처리)

수동 작업(32개 Inspector 설정) 대신 Editor 스크립트로 일괄 처리한다.

**이유**:
- 32개 프리팹에 동일한 설정값을 반복 입력하는 과정에서 실수 발생 가능성이 높음
- 에디터 스크립트는 검증 후 삭제 가능한 1회성 도구

---

## 변경 파일 목록

### 신규 생성 (1회성 — 실행 후 삭제 가능)
| 파일 | 역할 |
|------|------|
| `Assets/Editor/Setup/SetupNewUnitPrefabs.cs` | 신규 프리팹 32개에 컴포넌트 일괄 부착 |

### 수정 대상 프리팹 (32개)
에디터 스크립트가 아래 프리팹들을 자동으로 수정함.

**Human (10개)**
- Unit_BattleAxe_Blue/Red
- Unit_SpearMan_Blue/Red
- Unit_LittleKnight_Blue/Red
- Unit_CannonCart_Blue/Red
- Unit_Tank_Blue/Red

**Spirit (12개)**
- Unit_BoulderSpirit_Blue/Red
- Unit_DustSpirit_Blue/Red
- Unit_QuakeSpirit_Blue/Red
- Unit_TideSpirit_Blue/Red
- Unit_TorrentSpirit_Blue/Red
- Unit_StreamSpirit_Blue/Red

**Transcendence (10개)**
- Unit_EagleArcher_Blue/Red
- Unit_RabbitTrickster_Blue/Red
- Unit_MushroomBomber_Blue/Red
- Unit_RhinoBreaker_Blue/Red
- Unit_BloomFairy_Blue/Red

---

## 스크립트 동작 상세

### 처리 흐름
```
메뉴 Hexiege/Setup/신규 유닛 컴포넌트 부착
   ↓
대상 프리팹 경로 목록 순회 (32개)
   ↓ 각 프리팹에 대해:
[1] PrefabUtility.LoadPrefabContents() — 프리팹 편집 모드로 로드
[2] Root GameObject 처리:
    - UnitView 없으면 추가 → Inspector 값 설정
      (_idleToWalkBlend=0.1, _toAttackBlend=0.08, _attackToWalkBlend=0.1, _rotationSpeed=270)
    - NetworkObject 없으면 추가 (기본 설정값 사용)
    - NetworkTransform 없으면 추가 → 동기화 축 설정
      (Position XYZ=ON / RotAngle Y만 ON / Scale XYZ=ON / Interpolate=ON)
    - NetworkUnit 없으면 추가
[3] _Mesh 자식 GameObject 처리:
    - 이름에 "_Mesh" 포함된 자식 탐색
    - AnimationEventRelay 없으면 추가
[4] PrefabUtility.SaveAsPrefabAsset() — 저장
   ↓
완료 로그 출력 (처리된 프리팹 수)
```

### 안전 처리
- 이미 컴포넌트가 있는 경우 `AddComponent` 하지 않음 (중복 방지)
- 기존 완성 프리팹(Pistoleer, Assault 등)은 대상 경로에 포함하지 않아 수정되지 않음

---

## 위험 요소 및 제약

| 위험 요소 | 대응 |
|---|---|
| NetworkObject의 GlobalObjectIdHash 자동 생성 | Unity가 프리팹 저장 시 자동 부여 — 수동 설정 불필요 |
| _Mesh 오브젝트 이름 불일치 가능성 | 스크립트에서 "_Mesh" 포함 여부로 탐색하되, 찾지 못한 경우 로그 경고 출력 |
| 기존 완성 프리팹 실수로 포함 | 대상 경로를 신규 유닛 이름으로 명시적 지정 — 기존 유닛 경로 제외 |
| Animation Event 미설정 | 이번 작업 범위 외 — AnimationEventRelay는 부착하지만 클립 Event는 별도 작업 |

---

## 작업 순서

1. **game-programmer** 에이전트에게 Editor 스크립트 구현 위임
2. Unity 에디터에서 `Hexiege/Setup/신규 유닛 컴포넌트 부착` 메뉴 실행
3. 임의로 3~4개 프리팹을 Inspector에서 열어 컴포넌트 부착 여부 확인
4. 완료 확인 후 스크립트 파일 삭제 (선택)
