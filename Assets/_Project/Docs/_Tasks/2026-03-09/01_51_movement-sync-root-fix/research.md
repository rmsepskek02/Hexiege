# Research: 멀티플레이 이동 동기화 어긋남 — 근본 원인

**작성일:** 2026-03-09
**작업명:** movement-sync-root-fix

---

## 핵심 발견: 동기화 어긋남의 진짜 원인

### 구조 확인 (코드 전체 파악 후)

| 컴포넌트 | 서버 | 클라이언트 |
|----------|------|------------|
| `MoveAlongPath` | 실행 (serverAuthoritative=false) | 실행 (serverAuthoritative=true) |
| ClaimedTile 설정 | O | X (serverAuthoritative 가드) |
| per-step IsTileBlockedBySameTeam | O | X (serverAuthoritative 가드) |
| 멀티플레이 pre-Lerp 전투 체크 | O | O ← **여기서 어긋남 발생** |

### 전투 체크가 어긋나는 이유

서버/클라이언트 양쪽 모두 `HasEnemyInRangeByCoord(_unitData)`를 독립적으로 실행.

- **적 유닛 Position**: 각자의 `MoveAlongPath` coroutine이 `ProcessStep`을 호출하여 업데이트
- **coroutine 실행 타이밍**: Unity 코루틴은 동일한 경로를 받아도 프레임 순서에 따라 다른 타이밍에 실행됨
- **결과**: 서버의 `unit.Position`과 클라이언트의 `unit.Position`이 같은 시점에 다를 수 있음

예시:
```
프레임 1:
  서버: A의 ProcessStep 실행 → A.Position = tile2
  서버: B의 pre-Lerp 체크 → 적 A가 tile2에 있음, 거리=1 → 정지
  클라이언트: B의 pre-Lerp 체크 → 적 A가 아직 tile1에 있음 (ProcessStep 미실행), 거리=2 → 정지 안 함
```

같은 경로를 받아도 유닛들의 상태가 다른 순서로 업데이트되어 다른 결정 → 영구적 위치 괴리.

### HOST 유닛 겹침 원인

pre-Lerp 체크를 추가하면서 생긴 문제:

- 유닛 A가 tile3에서 tile4로 이동 시작. ClaimedTile=tile4. pre-Lerp 체크: 적 tile4 인접. 대기.
- 유닛 B는 A.Position=tile3 → tile3 차단됨 → 우회 경로 탐색
- 우회 경로가 없으면 B는 멈춤. A도 멈춤.
- 유닛이 많아지면 모두 서로를 막아 경로 계산이 꼬이고 TickSiege가 새 경로 발행 시 겹침 발생

### 전투 체크 없이 전투가 되는 이유

`NetworkCombatController`는 이미 서버 전용으로 매 0.1초마다 TryAttack 실행:
- 데미지 → NetworkHealthSync → HP NetworkVariable 클라이언트 동기화
- 공격 애니메이션 → TriggerAttackAnimationClientRpc → 양쪽 재생
- 사망 → EntityDiedClientRpc → 양쪽 파괴

**MoveAlongPath의 전투 체크는 전투 자체가 아닌 "시각적 이동 정지"만 담당함.**

---

## 영향 범위

| 파일 | 관련 내용 |
|------|-----------|
| `UnitView.cs` | MoveAlongPath: pre-Lerp 멀티플레이 전투 체크 블록 (345-362줄) |
| `UnitCombatUseCase.cs` | HasEnemyInRangeByCoord — 위 체크에서만 사용 |

---

## 결론

`MoveAlongPath`의 멀티플레이 전투 체크가 동기화 어긋남의 근본 원인.
해결책: 해당 블록 제거 → 양쪽 동일한 경로를 동일한 방식으로 실행 → 완전 동기화.
