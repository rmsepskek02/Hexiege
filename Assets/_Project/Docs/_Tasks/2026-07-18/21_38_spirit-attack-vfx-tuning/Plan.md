# Plan — StreamSpirit / TorrentSpirit 공격 VFX 정밀 튜닝

## 이 계획이 무엇인지

Unity 플레이 화면에서 두 물 정령의 공격을 반복 재생하며 VFX의 위치·방향·크기·발생 시점을 수치로
조정한다. StreamSpirit은 기존 `VfxSpawnPoint`와 Animation Event를 미세 조정하고, TorrentSpirit은
전용 스폰 및 표시 파라미터를 마련해 서버 파도의 전진 거리와 시간에 맞는 연출로 만든다.

---

## 기존 로직 제거 여부

기존 공격 판정, 특수 공격, VFX 풀, 설정 연결은 제거하지 않는다. 화면 연출을 위한 위치·회전·크기·이동
제어만 추가하거나 조정한다. 데미지 및 힐 계산은 변경하지 않는다.

## 근거 규칙

| 규칙 | 계획에 적용하는 방법 |
|---|---|
| 유닛 규칙 17 | `OnAttackHit`를 타격 프레임의 단일 출처로 유지하며 화면을 보고 이벤트 시간을 조정한다. |
| 유닛 규칙 18 | 서버 파도 판정은 그대로 유지하고 VFX만 같은 거리·시간에 맞춰 표시한다. |
| 유닛 규칙 20 | Torrent 파도 이동은 순수 Presentation 연출로 구현한다. |
| 유닛 규칙 25 | 위치·크기·이동 시간 등 튜닝값은 Inspector에서 조정 가능한 설정으로 둔다. |

## 구현 계획

### A. Unity 화면 기준선 확보

1. Game 씬에서 StreamSpirit/TorrentSpirit Blue·Red의 공격을 재현한다.
2. 정면 및 사선 공격을 각각 확인한다.
3. 첫 공격과 풀 재사용 이후 공격을 비교한다.
4. 현재 화면을 기준 이미지로 남겨 변경 전후를 같은 조건에서 비교한다.

### B. StreamSpirit

1. Blue/Red의 `VfxSpawnPoint`를 같은 로컬 좌표로 맞춘다.
2. 공격 손동작과 물줄기 시작점이 일치하도록 위치를 조정한다.
3. `StreamSpirit_Attack.anim`의 `OnAttackHit` 시간을 공격 모션의 실제 방출 프레임으로 이동한다.
4. 이벤트 이동으로 피격 표현이 너무 빨라지거나 늦어지지 않는지 확인한다.
5. VFX 원본 크기는 위치·타이밍으로 해결되지 않을 때만 최소 조정한다.

### C. TorrentSpirit

1. Blue/Red에 동일한 전용 VFX 스폰 기준점을 두거나, 전용 연출 설정에 위치 오프셋을 둔다.
2. Torrent 전용으로 위치 오프셋, 회전 오프셋, 스케일을 Inspector에서 조절 가능하게 한다.
3. 파도 연출이 공격자 전방으로 길이 3을 0.5초 동안 진행하도록 Presentation 전용 이동 경로를 둔다.
4. 서버 판정은 수정하지 않고 시각 이동만 동일 파라미터를 사용한다.
5. 파도 폭이 판정 폭 3과 화면상 자연스럽게 대응하도록 스케일을 조절한다.
6. 지면 높이, 시작 위치, 전진 종료 위치와 파티클 수명을 화면으로 맞춘다.

### D. 풀 재사용 안전성

1. 재생 시 Transform position/rotation/scale을 항상 명시적으로 초기화한다.
2. 이동 코루틴 또는 진행 상태를 풀 반환 시 정리한다.
3. 연속 공격 3회 이상에서 위치·크기·수명이 동일한지 확인한다.

### E. 나머지 VFX 점검

두 물 정령 튜닝 후 화면에서 다음만 기록한다. 이번 계획 승인만으로 설정을 변경하지 않는다.

- Tank가 `vfx_tank_attack` 대신 cannon VFX를 쓰는 상태
- Pistoleer/Assault/Sniper의 공용 총구 VFX
- InfernoSpirit/FoxMagician의 charge VFX 재생 시점
- 공통 사망 VFX의 크기와 위치

## 예상 변경 파일

- `Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Blue.prefab`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_StreamSpirit_Red.prefab`
- `Assets/_Project/Animations/Units/StreamSpirit/StreamSpirit_Attack.anim`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_TorrentSpirit_Blue.prefab`
- `Assets/_Project/Prefabs/Units/Spirit/Unit_TorrentSpirit_Red.prefab`
- `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` 또는 Torrent 전용 Presentation 컴포넌트
- 필요 시 `EffectPreset`에 연출 Transform 튜닝 필드

실제 화면 확인 결과에 따라 프리팹 자체 수정 대신 전용 설정/컴포넌트가 더 안전하면 그 경로를 선택하고
이 문서에 변경 이유를 기록한다.

## 완료 기준

- StreamSpirit의 VFX 시작점이 공격 모션과 맞고, 타격 프레임에 자연스럽게 발생한다.
- TorrentSpirit 파도가 지면에서 적절한 크기로 생성되고 전방 판정 거리·시간과 시각적으로 일치한다.
- Blue/Red 및 정면/사선 공격에서 방향이 정상이다.
- 풀 재사용 후에도 위치·크기·파티클 재생이 동일하다.
- 전투 데미지·힐·쿨다운 수치는 변경되지 않는다.

## 위임 및 검증

- 구현: game-programmer
- 전체 조율 및 결과 검토: project-orchestrator
- 구현 후 정적·화면 검증: qa-tester
- 문서 동기화: document-manager

