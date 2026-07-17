# QA 패턴 및 버그 히스토리

## 세션: 2026-02-21 — 팀 기반 입력 / 터치 팬 / 카메라 뷰 플립

### 발견된 패턴
1. 팀 일반화 작업 시 StartAutoMove 같은 유틸 메서드의 하드코딩 잔존 위험
   - 검사 방법: `TeamId.Blue|TeamId.Red` grep 후 LocalPlayerTeam.Current 교체 여부 확인

2. `using` 선언이 실제 사용 없이 남는 경우 발생
   - CameraController의 `using Hexiege.Infrastructure` — 코드 내 Infrastructure 타입 미사용
   - 검사 방법: `Infrastructure\.` grep으로 실사용 확인

3. Debug.Log를 제거하지 않고 PR 병합된 케이스
   - InputHandler.IsPointerOverUI: 매 클릭마다 2줄 이상 로그
   - 규칙: 프로덕션 빌드 전 `[InputHandler]` 태그 로그 전수 확인

### 주요 버그 목록
| ID | 심각도 | 파일 | 라인 | 설명 | 상태 |
|----|--------|------|------|------|------|
| B001 | Major | CameraController.cs | 265 | 에디터 터치 팬 차단 (mouse==null 조건) | OPEN |
| B002 | Major | InputHandler.cs | 363,375 | StartAutoMove 팀 일반화 범위 확인 필요 | CLOSED — StartAutoMove가 LocalPlayerTeam.Current 기반으로 팀 일반화 완료 |
| B003 | Minor | InputHandler.cs | 437-439 | Debug.Log 프로덕션 노이즈 | OPEN |
| B004 | Minor | CameraController.cs | 302-308 | Red팀 카메라 위치/경계 실기기 확인 필요 | 테스트 대기 |
| B005 | Minor | InputHandler.cs | 395-410 | FindClosestWalkableNeighbor IsWalkable 미검증 | OPEN |

## 세션: 2026-07-17 — TorrentSpirit 파도형 AoE + 힐 서브시스템 (정적 분석만)

### 핵심 발견 — "코드는 맞는데 데이터가 안 채워진" 패턴 (규칙 25 교훈의 재발)
신규 특수 유닛 구현 시 로직(Application/Infrastructure 코드)은 정교하게 잘 만들어졌으나,
**3개의 데이터 자산(SO 항목/애니메이션 이벤트/이펙트 프리셋)이 비어 있어 실질적으로 기능 불능**이었던 사례.
- `UnitStatsConfig.asset`에 신규 유닛의 `unitType` 항목이 통째로 없으면 `UnitStats.TryGet`이 실패해
  범용 폴백값(MaxHp10/AttackPower1/Range1.0/MoveSpeed1.0/Cooldown1.0/HitFrameTimes[0.2])으로 조용히 동작한다.
  컴파일 에러도, 콘솔 에러도 없다 — **grep으로 `unitType: N` 항목 존재 여부를 직접 확인해야만 드러남.**
- Attack 애니메이션 클립(`.anim`)의 `m_Events: []`(빈 배열)이면 규칙 27(OnAttackHit 주입) 미완료 상태.
  `UnitFactory.GetHitFrameTimes()`가 이벤트 0개 → 기존 폴백([0.2]s) 유지 → 실제 클립 길이(예: 4초)와
  괴리된 시점에 데미지/특수효과가 발동. **`grep -n "m_Events:" *.anim`으로 즉시 확인 가능.**
- `UnitEffectConfig.asset`의 `attackPreset: {fileID: 0}`이면 이펙트 프리팹이 존재해도(고아 에셋) 연결 안 됨.
  **VFX 프리팹 파일 존재 ≠ 배선 완료. 반드시 관련 .asset에서 해당 unitType 항목의 fileID가 0이 아닌지 확인.**

### 검사 루틴(향후 신규 유닛/특수 공격 QA 시 필수)
1. `grep -n "unitType: <N>" -A 12 UnitStatsConfig.asset` — 스탯 항목 존재 + 기획값과 일치 확인.
2. 대상 유닛 Attack `.anim` 파일에서 `m_Events:` 확인 — `[]`이면 규칙 27 미완료.
3. `UnitEffectConfig.asset`에서 해당 unitType의 `attackPreset`/`deathPreset` fileID가 0이 아닌지 확인.
4. `_spiritPrefabs`/`_humanPrefabs`/`_transcendencePrefabs` 필드명이 **UnitFactory와 BuildingFactory 양쪽에 동일하게 존재**
   (기존에 기록된 `_bluePrefabs`/`_redPrefabs` 패턴과 동일한 함정) — 반드시 `m_Script`의 클래스명(`Hexiege.Infrastructure.UnitFactory` vs `...BuildingFactory`)을 같이 확인한 뒤 올바른 블록에서 unitType 항목을 찾을 것. guid로 실제 프리팹 경로(.meta)까지 역추적해 실물 유닛 프리팹인지 재확인.

### 구조적 버그 — special-only(ReplacesPrimaryAttack=true) 유닛의 건물 공격 불가
`ISpecialAttackBehavior.ReplacesPrimaryAttack=true`인 유닛(TorrentSpirit)은 주 타깃 단일 피해가
무조건 스킵된다. 파도(TickWaves)의 피해 판정이 `_unitSpawn.Units.Values`만 순회하고 `_buildingPlacement.Buildings`를
전혀 고려하지 않으면, **주 타깃이 건물일 때 그 공격 사이클에 아무 피해도 발생하지 않는다.**
도끼병(ReplacesPrimaryAttack=false)은 주 타깃 단일 피해가 먼저 들어가므로 이 문제가 없음 — special-only
모델 특유의 함정. **향후 special-only 유닛(BloomFairy 등) QA 시 "주 타깃이 건물인 경우"를 반드시 별도 점검.**

### Epsilon 불일치 패턴
사거리 판정(`CalculateRangeLimits`)은 `+0.05f` Epsilon을 두지만, AoE 판정 로직(`TickWaves`의 `p > wave.Length`,
`SweepAttackBehavior`의 반경 비교 등)은 Epsilon 없이 엄격 비교하는 경우가 있다. 두 값이 같은 크기로 튜닝되면
경계값 근처에서 "방금 사거리에 들어온 주 타깃이 AoE 판정에서는 제외"되는 미세 불일치가 발생할 수 있음 — 신규 AoE
로직 리뷰 시 사거리 판정 쪽 Epsilon과 AoE 판정 쪽 경계 처리를 항상 대조할 것.

### task 문서
`Assets/_Project/Docs/_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/Plan.md`
