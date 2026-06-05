# Research — 신규 유닛 프리팹 컴포넌트 부착

## 작업 개요 (자연어 설명)

새로 아트 작업이 완료된 유닛 프리팹들에 게임 동작에 필요한 컴포넌트들을 부착하는 작업이다.
기존에 완성된 유닛(Pistoleer, Assault, Sniper 등)은 이미 필요한 컴포넌트가 모두 붙어 있지만,
새로 추가된 유닛들은 아트 파일(메시, 애니메이터)만 있을 뿐 게임 로직 컴포넌트가 전혀 없는 상태다.

이 컴포넌트들이 없으면 유닛이 게임 안에서 스폰되더라도 움직이지 않고, 방향을 바꾸지 않으며,
멀티플레이에서 상대방 화면에 보이지 않고, 공격 애니메이션의 타격 타이밍도 인식하지 못한다.

---

## 대상 프리팹 현황

### 기존 완성 프리팹 (변경 불필요)
| 종족 | 유닛 |
|------|------|
| Human | Pistoleer, Assault, Sniper |
| Spirit | FlameSpirit, EmberSpirit, InfernoSpirit |
| Transcendence | BearGuard, FoxMagician, LionKnight |

### 신규 — 컴포넌트 부착 필요 (32개)
| 종족 | 유닛 (Blue + Red 각 1개) | 합계 |
|------|------|------|
| Human | BattleAxe, SpearMan, LittleKnight, CannonCart, Tank | 10개 |
| Spirit | BoulderSpirit, DustSpirit, QuakeSpirit, TideSpirit, TorrentSpirit, StreamSpirit | 12개 |
| Transcendence | EagleArcher, RabbitTrickster, MushroomBomber, RhinoBreaker, BloomFairy | 10개 |

---

## 기존 완성 프리팹 구조 분석 (Unit_Pistoleer_Blue 기준)

### 프리팹 계층 구조
```
Unit_Pistoleer_Blue (Root GameObject)
├─ UnitView                     ← 이동·전투·애니메이션·회전 제어
├─ NetworkObject                ← 멀티플레이 네트워크 오브젝트 식별
├─ NetworkTransform             ← 위치·회전을 상대방 화면에 동기화
└─ NetworkUnit                  ← Hexiege 전용 네트워크 유닛 처리
│
└─ Unit_Pistoleer_Blue_Mesh (자식 GameObject)
   ├─ Animator                  ← 이미 존재 (Avatar + Controller 연결됨)
   └─ AnimationEventRelay       ← 공격 타격 프레임 이벤트를 UnitView로 전달
```

### Root 컴포넌트 Inspector 값 (Pistoleer 기준, 전 유닛 동일)
| 컴포넌트 | 필드 | 값 |
|---|---|---|
| UnitView | _idleToWalkBlend | 0.1 |
| UnitView | _toAttackBlend | 0.08 |
| UnitView | _attackToWalkBlend | 0.1 |
| UnitView | _rotationSpeed | 270 |
| NetworkTransform | SyncPositionX/Y/Z | ON |
| NetworkTransform | SyncRotAngleX | OFF |
| NetworkTransform | SyncRotAngleY | **ON** |
| NetworkTransform | SyncRotAngleZ | OFF |
| NetworkTransform | SyncScaleX/Y/Z | ON |
| NetworkTransform | Interpolate | ON |

---

## 신규 프리팹 현재 상태 (Unit_BattleAxe_Blue 기준)

### 현재 계층 구조
```
Unit_BattleAxe_Blue (Root GameObject)
└─ Transform만 존재  ← 나머지 컴포넌트 전혀 없음

└─ Unit_BattleAxe_Blue_Mesh (자식 GameObject)
   └─ Animator  ← Avatar + Controller 연결됨, ApplyRootMotion=OFF (정상)
      (AnimationEventRelay 없음)
```

### 빠진 컴포넌트 목록
| 위치 | 빠진 컴포넌트 |
|------|------|
| Root | UnitView |
| Root | NetworkObject |
| Root | NetworkTransform |
| Root | NetworkUnit |
| _Mesh 자식 | AnimationEventRelay |

---

## 각 컴포넌트의 역할 (근거: 코드 분석)

### UnitView (`Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`)
- 유닛의 이동(A* 경로 따라 타일 이동), 전투(공격 애니메이션 재생), 방향 회전을 제어
- Animator 컴포넌트를 자식에서 자동 조회하여 사용
- `_rotationSpeed = 270f` → RotateTowards 방식으로 서서히 회전

### NetworkObject (`Unity.Netcode`)
- 이 GameObject를 네트워크 상에서 고유 ID로 식별
- 없으면 멀티플레이에서 오브젝트를 스폰/디스폰할 수 없음
- `GlobalObjectIdHash`는 Unity가 프리팹 저장 시 자동 생성

### NetworkTransform (`Unity.Netcode.Components`)
- 서버에서 계산한 위치(XYZ)와 Y축 회전을 클라이언트에 자동 동기화
- X, Z 회전 동기화는 OFF — 유닛은 Y축 회전만 사용 (FlatTop 헥스 이동)
- `Interpolate = ON` → 클라이언트에서 부드러운 이동 보간

### NetworkUnit (`Assets/_Project/Scripts/Infrastructure/NetworkUnit.cs`)
- Hexiege 게임 전용 네트워크 유닛 처리 컴포넌트
- Red 팀 클라이언트 rotation 보정 처리 포함

### AnimationEventRelay (`Assets/_Project/Scripts/Presentation/Unit/AnimationEventRelay.cs`)
- Animator가 있는 자식 오브젝트에 부착
- 공격 애니메이션 클립의 타격 프레임에서 `OnAttackHit()` Animation Event 호출 시
  부모 UnitView의 `OnAttackHit()`으로 중계
- Unity Animation Event는 Animator와 동일한 오브젝트의 MonoBehaviour에서만 동작하므로 이 중계 컴포넌트가 필요

---

## 작업 범위 및 제외 사항

### 이번 작업 범위
- 32개 신규 프리팹에 컴포넌트 5종 부착 및 Inspector 값 설정

### 이번 작업에서 제외 (별도 작업)
- Animation Event 설정 (공격 애니메이션 클립의 OnAttackHit 타이밍 지정) — 각 유닛의 애니메이션 클립별 개별 설정 필요
- UnitFactory에 신규 유닛 등록 — 종족 매핑 테이블 확장 별도 작업
- UnitStatsConfig 신규 유닛 스탯 추가 — 별도 작업

---

## 발견된 부가 이슈

없음.
