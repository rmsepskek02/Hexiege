# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-07-22
**정밀 동기화 상태:** 유닛 이동·공격 규칙 v2와 25종 감사 문서는 확정됐고, Tracer A0 schedule/dispatch Shadow와 A1 pure Application UnitAction 계약·stateful reducer가 검증을 통과했다. 다만 런타임 pose/result seam, 피해·RPC·VFX, Simulation/Visual Root 분리·Snapshot/ImpactResult 복제·표현 전환은 미연결이다. 기존 이동/Walk·타격 동기화 PASS는 Legacy 범위의 이력으로만 보존하며, 세 불일치 문제는 **P0 진행 중** 상태다.
**최신 main 반영:** InfernoSpirit은 직접 25 + 유닛 전용 DoT 5/초×3초가 Legacy 경로에서 사용자 실기 확인됐고, QuakeSpirit은 직접 20 + 주변 적 유닛·건물 10 및 Host/Client HP 결과가 로그로 확인됐다. 다만 Inferno는 marker 0.50초/설정 1.15초가 불일치하며, Quake는 기본 `OnAttackHit`이 없고 1.00초 placeholder를 사용한다. 두 구현 모두 권위 `AttackSequenceId`, `ImpactPoint`, 결과 순번이 없어 v2 Complete가 아니다.
**현재 단계 (규칙 v2):** A1 순수 계약·stateful reducer 완료. 다음은 **A2 server-authoritative pose seam shadow**이며, 아래 Legacy 스냅샷은 현재 판정에 사용하지 않는다.
**Legacy 스냅샷 (2026-07-19):** BattleAxe·TorrentSpirit·BloomFairy·MushroomBomber의 당시 기능 완료 기록은 `WORK_HISTORY.md`로 이관해 보존한다. 이후 반영된 InfernoSpirit·QuakeSpirit 상태와 v2 판정은 위 최신 상태 및 AssetMatrix를 따른다.

> Legacy 기능 PASS는 규칙 v2 Complete가 아니다. 실제 현재 상태는 위 `정밀 동기화 상태`와 `UnitCombatAssetMatrix.md`를 따른다.

---

## 전체 구현 현황

### 🔴 P0 재오픈 — 서버 권위 유닛 ActionSequence

- 규칙 v2 완료: `GameSystemRules_Units.md`, `GameSystemRules_UnitCombatSynchronization.md`
- 25종 에셋 감사 완료: `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- Tracer A0 완료: `UnitActionSequencing` 계약 유틸리티와 SpearMan schedule/dispatch Shadow 계측. 기존 피해·HP·RPC·VFX는 그대로 유지했다.
- 사용자 멀티 Host 계측: scheduled 204 / dispatch 204 / unique 204, missing·duplicate·target mismatch·schedule↔dispatch facing change 모두 0. Windup 240ms 전건 일치.
- 방출 지연: min 0.013ms / avg 9.105ms / p50 8.226ms / p95 19.862ms / max 29.386ms. 16.667ms 초과 27건, 33.333ms·50ms 초과 0건.
- Client 로그는 현재 계측 범위상 header only이므로 Host/Client 상관관계 검증 완료로 판정하지 않는다.
- Tracer A1 완료: `UnitActionContracts`와 `UnitActionSequencer` 순수 Application 계약·stateful reducer. C# 9/Application 및 Editor 컴파일 PASS, Unity Editor 메뉴 PASS, reflection `Validate*` 10개 PASS, 최종 Standards/Spec P0~P3 지적 0건.
- A1 미연결 경계: 런타임 pose/result seam과 피해·RPC·VFX는 연결하지 않았다. 다음은 A2 server-authoritative pose seam shadow다.
- 목표: Simulation Root/Visual Root 분리, `AttackerInstanceId + AttackSequenceId + HitIndex` 기반 정규 결과 키, 서버 시간 Impact, FIFO 대체
- 런타임: A0 진단 외에는 reducer 입력·결과 seam이 미연결이고 기존 Network Root Red 보정, Walk/Attack 상태 분리, 공격자 FIFO가 남아 있어 **미완료**
- 중요 사실: UnitStatsConfig는 25/25 등록됐으나 기본 Attack marker 누락 4종, BattleAxe·Inferno 등 marker/설정 불일치, hit/tracer preset 0/25, Quake의 표현 최대 7.5초 지연 가능성이 남아 있다.
- 후속 구현과 검증은 현재 task Plan의 A2 이후 게이트를 따른다.

### 📐 확정 설계 — 구현 예정

#### FlatTop 11×21 무작위 대전 맵 (2026-07-19 기획 확정)

- 다섯 유형(완전개방형/장애물 개방형/협곡형/외곽형/3갈래형), 각각 20%
- 모든 생성 요소와 장식 exact 180° 대칭, 팀별 즉시 건설 가능 고유 타일 10개
- 유형별 중립 광산 1~6개와 정상 모드 광산 수별 초기 골드 700~200
- 초기 골드 전용 `MapTestModeEnabled` 확정: ON=5000, OFF=광산 수 표. 멀티플레이는 Host 표식·실제 골드 권위
- 국소 건설 불가 구역, 완전 차단 지형, 결정적 seed·독립 PRNG 스트림·100회 재시도·검증된 폴백 규칙 확정
- canonical binary + SHA-256, persistent chunk 전송, `SameMap`/`NewMap`, 임시 `MapVersion=1` 계약 확정
- `GameSystemRules_Map.md`, `GameSystemRules_RandomMap.md`, GDD, TDD, 작업 Research/Plan 반영 완료
- **상태:** 문서 설계 완료, 런타임 생성기·검증기·전송·테스트 모드·건설 불가/차단 지형·경로 완전 차단 대응은 미구현

### ✅ 완료된 시스템

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 (2026-04-26 갱신) | TileOwnershipService — Phase 0/1/2 모든 이동 방식에서 매 프레임 물리 위치 기반 실시간 점령 |
| 금광 타일 시스템 | ✅ 완료 (2026-04-08 갱신) | HasGoldMine, 채굴소 건설 조건, 건물 배치 시 광산 숨김/파괴 시 재표시+타일 중립 복원 |
| A* 경로탐색 | ✅ 현행 동작 / v2 문서 정정 | 정적 지형·건물 차단, 유닛 겹침 허용. `ClaimedTile` 아군 차단 설명은 Legacy이며 TDD·Units v2에서 폐기됨. |
| 카메라 줌 보간 | ✅ 완료 (2026-03-19) | DOTween.To + Ease.OutCubic, _targetZoom 누적, _zoomDuration(0.25f) SerializeField |
| 유닛 이동 (Lerp) | ✅ 완료 | Per-step 가용성 체크, 재탐색 |
| 전투 시스템 | ✅ 완료 | IDamageable, 이동 중 자동 공격 |
| 전투 거리 정밀도 | ✅ 완료 (2026-03-18 갱신) | 월드좌표 기반, Epsilon=0.05f 추가 (인접 경계 부동소수점 오차 방지) |
| 공격 방향 정밀도 | ✅ 완료 (2026-03-07) | 타겟 실제 transform.position 기반 Atan2, 2D 레거시 제거 |
| 공격 쿨다운 시스템 | ✅ 완료 (2026-04-04 갱신) | 유닛별 AttackCooldown=클립 길이 (Assault=0.2s, Pistoleer=2.0s, Sniper=3.0s), elapsed 기반 정확한 감소 |
| 다중 히트 데미지 | ✅ 완료 (2026-04-24) | FlameSpirit 6히트(총 12dmg), LionKnight 2히트(총 18dmg). HitFrameTimes float[] 기반, 싱글=PendingHit 타이머, 멀티=코루틴 N개 병렬 |
| 전투 애니메이션 시스템 (멀티플레이) | ✅ 완료 (2026-04-04) | 3-신호 RPC, 6가지 규칙, _combatAnimationSent 경쟁조건 수정, 사이클 동기화 |
| Walk 애니메이션 연속 재생 | ✅ 완료 (2026-03-09) | 매 스텝 0f 리셋 제거 → 이미 Walk 상태이면 클립 유지 |
| 공격 애니메이션-타격 시각 동기화 | ✅ 완료 (2026-03-14) | Animation Event + AnimationEventRelay → scale punch (데미지 타이밍 무변경) |
| 전투 타격 타이밍 동기화 | ⚠️ Legacy 완료 / v2 재오픈 | 2026-07-12 당시 FIFO·Animation Event 범위는 실기 PASS였으나 정상 방출 85.7%, 타임아웃 2.7%가 남았다. 규칙 v2에서 AttackProfile·AttackSequenceId+HitIndex·CombatPresentationDelay로 대체 예정. 당시 task: `_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`. |
| 전투 타이밍 검증 중 기존 버그 3건 수정 | ✅ 완료 (2026-07-12) | **[버그1]** `NetworkCombatController.Update()` Tick 이월 잔여분이 다음 Tick의 경과 시간에 이중 계산되어 쿨다운 15~25% 조기 소진(Pistoleer 2.0초 대비 실측 1.71초) → 실제 경과 시간 1:1 감소로 수정. **[버그2]** 피격 표현 큐가 공격자 사망/전투 중단(StopCombat) 시 잔여 항목 미방출 → 즉시 방출 경로 추가. **[버그3]** 클라 Attack 루프 이탈(Walk RPC) 시 `_combatAnimationSent` 잔존으로 StartCombat 재전송 억제 → 유닛이 굳어 보이는 시각 버그(실기 75초 Assault 사례). Walk RPC 전송 시 가드 해제로 수정. 3건 모두 이번 작업 이전부터 존재한 기존 결함으로, 이번 계측이 처음 가시화. 승패 무관. |
| 이동/Walk 애니메이션 동기화 (레벨 동기화 전환) | ⚠️ Legacy 완료 / v2 재검증 (2026-07-13) | 당시 실기/로그 검증 PASS. 유닛 애니메이션 상태(`UnitAnimState` None/Walk/Attack)를 1회성 엣지 RPC에서 NetworkVariable로 전환하고 스폰 레이스와 재경로 역방향 첫 스텝을 줄였다. 다만 이 완료 판정은 Simulation Root/Visual Root와 U-MOV-ALIGN 도입 전 기준이며, 현 클라이언트 root 회전 쓰기와 이동 전 정렬 부재는 P0에서 다시 교정한다. 상세 이력과 수치는 `WORK_HISTORY.md`, task `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/` 참조. |
| 유닛 메시 방향 보정 | ✅ 완료 (2026-04-29 갱신) | 전 유닛 Mesh Y=0, _meshYOffset 제거, 이동 anim offset=0, DirectionAngles={60,120,180,240,300,0} (FlatTop 월드 각도 기준) |
| 유닛 회전 시스템 (RotateTowards 통일) | ✅ 완료 (2026-05-14 개편) | 모든 회전 Quaternion.RotateTowards 통일. 방향 계산 Atan2(현재 월드 위치→목적지) 기반. [SerializeField] _rotationSpeed = 270f Inspector 조정 가능 |
| 공격 후 Walk 복귀 버그 수정 | ✅ 완료 (2026-03-14) | 타겟 소멸 후 이동 재개 시 Play(StateWalk) 명시 호출 (멀티/싱글 공통) |
| 건물 배치 (Castle/Barracks/MiningPost) | ✅ 완료 | 건설 검증, 영토 확장 |
| 자원 시스템 (골드) | ✅ 완료 | 채굴소 수입, 건물/유닛 비용 |
| 인구 시스템 | ✅ 완료 | 타일 수 = 최대 인구 |
| 유닛 생산 (수동/자동) | ✅ 완료 | 큐 최대 3, 롱프레스 자동 |
| 랠리포인트 | ✅ 완료 | 마커 표시, BFS 빈 타일 탐색, 위치/회전 Inspector 조정. 팀별 표시 분리: 각 플레이어 자신의 깃발만 표시 (2026-05-16) |
| 공성 시스템 | ✅ 완료 | 랠리→Castle 방향 자동 진군 |
| 유닛 분산 이동 (혼잡도 기반) | ✅ 완료 (2026-05-15) | CongestionMap + CongestionAwarePathfinder — 타일 혼잡도 가중 A*로 경로 자연 분산. GameConfig에 DecayInterval/CongestionWeight 통합 |
| 승패 판정 (Castle 파괴) | ✅ 완료 | GameEndUseCase, UI 표시 |

#### 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 아키텍처 (2026-07-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| 특수 공격 전략 핸들러 아키텍처 | ⚠️ Legacy 구현 / v2 이전 필요 | 현재 등록: BattleAxe, TorrentSpirit, MushroomBomber, InfernoSpirit, QuakeSpirit. BloomFairy는 별도 힐러 경로다. 모든 결과를 ActionSequence로 이전해야 한다. |
| 피해 수렴점 단일화 | ⚠️ 재오픈 | 기존 `ApplyDamageToVictim` 공용화 이후 Quake가 `ApplyFixedDamageToVictim` 경로를 추가했다. 피해·이벤트·사망 처리의 단일 writer/emitter를 v2 결과 적용기로 다시 수렴해야 한다. |
| InfernoSpirit 직접+DoT | ⚠️ Legacy PASS / v2 재검증 | 직접 25 + 유닛 전용 DoT 5/초×3초는 사용자 실기 확인. 건물은 직접 피해만. marker 0.50/설정 1.15 불일치와 권위 착탄·sequence 부재가 남아 있다. |
| QuakeSpirit 즉발 원형 AoE | ⚠️ 피해 로그 PASS / 표현·v2 미완료 | 주 타깃 20, 반경 내 다른 적 유닛·건물 10의 Host/Client HP는 확인. `OnAttackHit` 누락, 1.00초 placeholder, 권위 ImpactPoint/sequence 부재로 Complete 금지. |
| 도끼병 휩쓸기형 AoE (SweepAttackBehavior) | ✅ 완료 (2026-07-17) | 판정 = 월드 좌표 전방 부채꼴. forward=공격자→주 타깃 방향(XZ), 각 적 XZ거리 ≤ `sweepReach` AND 각도 ≤ `sweepArcHalfAngle`이면 피격. Y 무시, 겹친 적 포함, 아군/사망/공격자/주 타깃 제외, 건물 미대상. 월드 좌표는 `IEntityPositionProvider`(서버 권위). 초기 "전방 5타일 타일 기준"에서 실기 후 변경. |
| SpecialAttackConfig 튜닝 SO + 셋업 스크립트 | ✅ 완료 (2026-07-17) | `SpecialAttackConfig`(Infrastructure/Config) — `sweepReach`(기본 1.0, 실기값 0.75)·`sweepArcHalfAngle`(기본 120). GameBootstrapper가 SO값을 float로 UnitCombatUseCase 생성자에 주입(미연결 시 코드 폴백). 에셋 `Resources/Config/SpecialAttackConfig.asset` + `_specialAttackConfig` 배선 완료. 에디터 툴 `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs`(메뉴 `Hexiege/Setup/Create SpecialAttackConfig Asset (Game)`)로 에셋 생성+배선 멱등 자동화. |
| BattleAxe 스탯·타격 타이밍 | ⚠️ 불일치 재오픈 | UnitStatsConfig HitOffset 1.1667s, 실제 기본 Attack marker 1.02s. 권위 AttackTimeline 확정 전 Complete 판정 금지. |
| AoE 피격 연출 동시 방출 | ✅ 완료 (2026-07-17) | `HitPresentationQueue.OnLocalAttackHit`이 공격자 `HitFrameTimes.Length≤1`(단일 타격 프레임)이면 보류 큐 전부 방출(휩쓸기 N마리 동시 표시), `>1`(LionKnight 2타·FlameSpirit 6타)이면 기존대로 1건(회귀 없음). 데미지·HP는 서버에서 전원 정확 적용, 이 변경은 연출 타이밍만. |
| 실기 테스트 | ✅ PASS (2026-07-17) | 사용자 실기 통과. 도끼병 전방 부채꼴 범위 적 전원 피해·연출 동시 표시 확인. main 최신화 병합 완료(폰트 에셋 충돌은 main 버전으로 정리). |

#### BloomFairy(꽃요정) 힐러 + HoT/DoT 공용 시스템 (2026-07-18) — 특수 유닛 5종 중 3번째
| 항목 | 상태 | 비고 |
|------|------|------|
| 힐러 전용 경로 (적 공격 흐름 분리) | ✅ 완료 (2026-07-18) | BloomFairy는 적을 때리지 않고 부상 아군을 회복하는 힐러. `ExecuteAttack`/`ISpecialAttackBehavior`/`SpecialAttackRegistry`(적 공격 흐름)에 **등록하지 않고**, 상태머신이 데이터(힐러 플래그)로 인식해 힐 루프를 타는 독립 경로. 힐 발동은 `OnAttackHit`이 아니라 상태머신 `HitFrameTimes` 타이머(`OnAttackHit`은 연출 전용). 규칙 32. |
| 부상 아군 탐색 (팀 필터 반대) | ✅ 완료 (2026-07-18) | 기존 적 탐색은 무변경, 아군 탐색을 별도 메서드로 신설(같은 팀 AND 살아있음 AND `Hp<MaxHp`, **본인 포함**, 아군 유닛만·건물 제외). 사거리 4.0 월드. 우선순위 잃은 체력 비율 최대 → 동률 시 거리 최소. 규칙 33. |
| HoT/DoT 공용 시간 지속 효과 시스템 | ✅ 완료 (2026-07-18) | 서버 권위 diff 틱(`ActiveTimedEffect`/`ApplyTimedEffect`/`TickTimedEffects`, Application). 대상별 동종 1레코드·갱신=리셋. HoT는 매 프레임 부드럽게(diff)로 총량 정확 도달(3초 20HP). Damage(DoT) 분기는 구조로 수용(MushroomBomber가 초 단위 틱으로 실제 배선). 규칙 34. |
| 힐 유휴 감시 + 쿨다운 예외 | ✅ 완료 (2026-07-18) | 경로 끝 도달 후에도 부상 아군 지속 감시(`HealerIdleWatchV3`, 규칙 35). ⚠️ **쿨다운 예외**(프로젝트 유일): `AttackCooldown`(3.0s)이 힐 발동 준비(1.0s)를 미포함 → 발동 후부터 카운트라 실제 힐 주기 4.0s. 의도된 설계(되돌리지 말 것). 규칙 36. |
| HoT 힐 텍스트 집계 (완료 시 1회) | ✅ 완료 (2026-07-19, 실기 확정) | HP 회복은 종전대로 틱마다 상승(HP바·멀티 동기화 무변경). 플로팅 힐 텍스트만 틱마다 억제하고 효과 정상 종료 시 회복 후 현재 HP로 1회 표시(대상 사망 시 생략). `EntityHealedEvent.ShowText`+`NetworkHealthSync` 전파, `ActiveTimedEffect.ActualHealed>0`일 때만. TorrentSpirit 즉발 힐·데미지 텍스트는 무변경. 규칙 37. |
| 실기 테스트 | ✅ 완료 (2026-07-18) | 사용자 실기 테스트 완료. task: `_Tasks/2026-07-18/03_40_bloomfairy-healer/`. |

#### MushroomBomber(버섯폭격기) 착탄형 범위 DoT (2026-07-19) — 특수 유닛 5종 중 4번째
| 항목 | 상태 | 비고 |
|------|------|------|
| 착탄형 특수 핸들러 (BlastAttackBehavior) | ✅ 완료 (2026-07-19) | `Application/Combat/BlastAttackBehavior.cs` 신설(`ISpecialAttackBehavior`, `ReplacesPrimaryAttack=false`) + 레지스트리 `MushroomBomber → BlastAttackBehavior` 1줄. 착탄 중심(주 타깃 위치) 기준 **월드 원형 반경**(XZ, arc 없음) 내 적 유닛 수집(주 타깃 포함·아군/사망/공격자/건물 제외) 후 DoT 부여. 수집부 `CollectEnemyUnitsInRadius`는 QuakeSpirit 재사용 위해 static 헬퍼 분리. 규칙 38. |
| 직접 10 + DoT 역할 분담 | ✅ 완료 (2026-07-19) | 직접 10=기존 `ExecuteAttack` 주 타깃 단일 피해(`ApplyDamageToVictim`, 건물 공성 포함), DoT AoE=특수 핸들러(적 유닛만). 주 타깃 유닛=직접+DoT, 반경 내 다른 적 유닛=DoT만, 주 타깃 건물=직접만(주변 유닛엔 DoT), 아군 무피해. 규칙 39. |
| DoT 초 단위 틱 모드 (규칙 34 확장) | ✅ 완료 (2026-07-19) | `ActiveTimedEffect.TickInterval` 분기(0=연속 HoT/양수=discrete DoT). `ApplyDamageOverTime`: 틱 간격 1.0s, 틱당 올림(`CeilToInt(perSecond×interval)`, 최소 1), 총량 클램프(`RoundToInt(perSecond×duration)`=6). 매초 `OnEntityDamaged` 발행→남은 체력 데미지 텍스트(힐과 반대로 억제 안 함). 서버 권위·이중 틱 금지. 규칙 40. |
| 튜닝 SO + 스탯·데이터 배선 | ✅ 완료 (2026-07-19) | `SpecialAttackConfig`에 `blastRadius`(1.0=인접 1칸)/`blastDotPerSecond`(2)/`blastDotDuration`(3) 추가, GameBootstrapper가 float 주입(미연결 시 폴백). UnitStatsConfig(26): HP40/공격력10/사거리2.0/감지2.0/이동1/쿨다운3.0/생산15·200골드·인구1. 클립 `OnAttackHit` 주입(규칙 27, 1개). VFX(투사체·폭발)는 사용자 별도 제작. |
| 식물 라인 생산 …15536 tokens truncated…er.cs 신설 (Domain) | ✅ 완료 | IsProductionBuilding / GetStage / GetNextStage / CanUpgrade |
| BuildingData.Stage 파생 프로퍼티 | ✅ 완료 | BuildingType에서 도출, 별도 저장 없음 |
| BuildingStats.GetUpgradeCost + GetTotalInvestedCost | ✅ 완료 | 업그레이드 비용 조회 + 누적 투자비 캐시 |
| BuildingStatsConfig.upgradeCost 필드 | ✅ 완료 | 32종 BuildingType Inspector 설정 완료 |
| GameEvents.OnBuildingUpgraded | ✅ 완료 | BuildingUpgradedEvent(OldBuildingId, NewBuilding) |
| BuildingPlacementUseCase.UpgradeBuilding() | ✅ 완료 | 기존 BuildingData 제거 → 다음 단계 BuildingData 생성 |
| BuildingFactory.UpgradeBuildingObject() | ✅ 완료 | 새 GO 먼저 생성 → 기존 GO Destroy (빈 타일 방지) |
| NetworkBuildingController 업그레이드 RPC | ✅ 완료 | RequestUpgradeServerRpc / UpgradeBuildingClientRpc |
| ProductionPanelUI BuildingUnitMapping 구조 | ✅ 완료 | BuildingType별 유닛 라인업 + requiredStage 단계별 잠금 |
| ToastKey.UpgradeRequired + ToastMessageConfig | ✅ 완료 | 잠금 유닛 탭 시 "건물 업그레이드가 필요합니다" 토스트 |
| GameBootstrapper 누적 투자비 캐싱 | ✅ 완료 | 단계별 체인 순회 → BuildingStats._totalInvestedCostCache |
| 신규 3D 에셋 (HumanBarracks, AncientGrove, PrimalSanctuary) | ✅ 완료 | Blue/Red 프리팹 + 머티리얼 |

#### ProductionPopup UI 레이아웃 재구성 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingIconEntry 팀별 Sprite 분리 (blueIcon/redIcon) | ✅ 완료 | GetBuildingIcon(BuildingType, TeamId). Sprite 명명 규칙: bld_{type}_blue/red.png |
| 2유닛 건물 레이아웃 [유닛1][빈슬롯][유닛2] | ✅ 완료 | _unitButtonGroups (List<CanvasGroup>). 가운데 슬롯 alpha=0 숨김, 레이아웃 공간 유지 |
| UpdateButtonPortraits() 2유닛 슬롯 매핑 수정 | ✅ 완료 | 2유닛 시: slot0=list[0], slot2=list[1] (slot1 스킵). 이전 건물 초상화 잔존 버그 수정 |
| HeaderText 건물 이름 동적 표시 | ✅ 완료 | BuildingType.ToString() 기반. Show() 호출 시 갱신 |
| 철거 환불 누적 계산 | ✅ 완료 | 1단계 건설비 + 모든 업그레이드비 합산의 50%. BuildingStats.GetTotalInvestedCost() + GameBootstrapper 캐싱 |
| 2/3단계 건물 랠리 마커 미표시 버그 수정 | ✅ 완료 | ProductionTicker에 OnBuildingUpgraded 구독 추가. 전 종족 테스트 통과 |

#### 건물 철거 시스템 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitProductionUseCase.CancelAllQueue() | ✅ 완료 | 생산 큐 전체 취소 + IsCharged=true 항목 전액 환불 + UnregisterBarracks |
| ProductionPanelUI.OnDemolishButtonClick() | ✅ 완료 | 싱글: CancelAllQueue → AddGold(50%) → DemolishBuilding. 멀티: RequestDemolishServerRpc |
| BuildingPlacementUseCase.DemolishBuilding() | ✅ 완료 | OnEntityDied 발행 → RemoveBuilding 호출 |
| NetworkBuildingController — RequestDemolishServerRpc | ✅ 완료 | 소유권/Castle/존재 검증 후 철거 + DemolishBuildingClientRpc 동기화 |
| BuildingFactory — OnEntityDied 구독 (GO 파괴) | ✅ 완료 | B방식: 구독 1개 + _buildingObjects Dict O(1) 조회로 GO 파괴 |
| BuildingView.cs + MiningEffectView.cs 삭제 | ✅ 완료 | 미사용 코드 제거. BuildingFactory가 GO 파괴 책임 인수 |
| 채굴소(MiningPost) 철거 UI | ✅ 기본 완료 | BuildingActionPanelUI에서 철거 지원. 전용 패널(일시정지 등)은 별도 작업 예정 |

---

#### 비생산 건물 공용 액션 패널 UI (2026-05-18~19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `BuildingPanelBase.cs` 추상 베이스 신규 | ✅ 완료 | ProductionPanelUI / BuildingActionPanelUI 공통 부모. Template Method 패턴. |
| `BuildingActionPanelUI.cs` 신규 | ✅ 완료 | 비생산 건물 클릭 시 공용 팝업 (헤더 + 철거 버튼) |
| `ProductionPanelUI` BuildingPanelBase 상속 리팩토링 | ✅ 완료 | 공통 필드/메서드 베이스 이전. 외부 API 동일 유지 |
| `BuildingTypeHelper.CanShowActionPanel()` 추가 | ✅ 완료 | `!IsProductionBuilding && type != Castle` |
| `InputHandler` 분기 추가 | ✅ 완료 | CanShowActionPanel 분기 + ClosedFrame 체크 |
| `GameBootstrapper` 주입/등록 추가 | ✅ 완료 | UIManager 등록 + 비생산 건물 환불 캐시 루프 |
| `SetupBuildingActionPanelUI.cs` 에디터 스크립트 | ✅ 완료 | 씬 자동 생성 + 필드 배선 + GameBootstrapper 연결 |
| 싱글플레이 실기 테스트 | ✅ PASS | 채굴소/AutoTower 팝업 표시 + 철거 버튼 동작 확인 |

---

#### 인게임 설정 메뉴 + 게임 포기 기능 (2026-05-18~19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `InGameSettingsUI.cs` 신규 | ✅ 완료 | IGameUI 구현. Show() 싱글 일시정지(timeScale=0). Hide() 복원 + ConfirmPopup 닫기 |
| `ConfirmPopup.cs` 신규 | ✅ 완료 | 범용 확인 팝업. BlockingOverlay로 공유 Background 클릭 차단 |
| 포기 흐름 구현 | ✅ 완료 | 싱글: GameEndUseCase.Forfeit() / 멀티: NetworkGameEndController.ForfeitServerRpc |
| `GameEndUseCase.Forfeit()` 신규 | ✅ 완료 | IsGameOver=true + GameEvents.OnGameEnd(TeamId.Red) 발행 |
| `NetworkGameEndController.ForfeitServerRpc` 신규 | ✅ 완료 | RequireOwnership=false. _announced 재사용, AnnounceWinnerClientRpc 재사용 |
| `GameHudUI` 설정 버튼 연결 | ✅ 완료 | _settingsButton, _settingsUI 필드 + OnSettingsClicked() |
| `GameBootstrapper` 등록 | ✅ 완료 | _inGameSettingsUI, _confirmPopup SerializeField + UIManager 등록 |
| `SetupInGameSettingsUI.cs` 에디터 스크립트 | ✅ 완료 | HUD 재배치 + 설정 패널 생성 + 필드 배선 자동화 |
| AnimatedPanel._backgroundOverlay 배선 | ✅ 완료 | [UI]/Background CanvasGroup 연결 → 패널 열릴 때 반투명 배경 표시 |
| 싱글플레이 실기 테스트 | ✅ PASS | 설정 메뉴 열기/닫기, 일시정지, 포기 기능 확인 |

---

#### 코드 리팩토링 (2026-05-18)
| 항목 | 상태 | 비고 |
|------|------|------|
| OnEntityDied 이벤트 분리 | ✅ 완료 | 단일 공용 이벤트 → OnUnitDied + OnBuildingDied 강타입 분리. 구독자 타입 필터(is-캐스팅) 전면 제거. 13개 파일 수정 |

---

#### 코드 리팩토링 전체 완료 (2026-05-24)

7개 그룹 전체 구현 완료. task 문서: `Assets/_Project/Docs/_Tasks/2026-05-19/10_46_code-refactoring/`

| 그룹 | 내용 | 상태 |
|------|------|------|
| **그룹 1** — Slot/Occupancy 시스템 제거 | AttackPositionManager, TileOccupancyManager 등 ~600줄 삭제. 관련 주석/참조 전면 정리 | ✅ 완료 |
| **그룹 2** — Application→Core 의존성 제거 | IHexCoordinateMapper 인터페이스 신규. HexMetricsCoordinateMapper 구현체. Application이 Core HexMetrics 직접 참조 금지 | ✅ 완료 |
| **그룹 2-B** — Infrastructure→Core 의존성 분리 | HexCoordinateMapper 구현체 Infrastructure로 이동. 레이어 경계 완성 | ✅ 완료 |
| **그룹 3** — Presentation→NGO 의존성 제거 (카테고리 A~E) | Presentation 레이어에서 `using Unity.Netcode` 0건. Infrastructure에서 `using Hexiege.Presentation` 0건. ServerRpc 래퍼 메서드 패턴 도입. 11개 파일 수정 | ✅ 완료 |
| **그룹 4** — FindFirstObjectByType 캐시화 | 30+회 매 프레임 호출 → OnNetworkSpawn 시점 1회 캐시로 전환 (~12회 이하로 감소) | ✅ 완료 |
| **그룹 5** — O(n) 탐색 캐시화 | `_unitsByPosition`, `_buildingsByPosition`, `_ownedTileCounts`, `_usedPopulationByTeam` Dictionary 도입. GetUnitAt/GetBuildingAt/CountTilesOwnedBy/GetUsedPopulation → O(1) | ✅ 완료 |
| **그룹 6** — 가독성/유지보수 (15개 sub-task) | enum 명시값, 중복 생성자 제거, 메서드 분해, GameEvents Subject 허브 통일, ToastKey Application 이동, IUnitView 인터페이스, IsNetworkMode→NetworkContext.IsNetworkActive 전면 교체, TODO 토스트 해소 등 15항목 전부 완료 | ✅ 완료 |
| **그룹 7** — GameBootstrapper partial class 분리 | GameBootstrapper.cs / Setup.cs / Map.cs / Network.cs 4파일로 분리 | ✅ 완료 |

**인스펙터 수동 연결 필요** (에디터에서 직접 연결):
- ~~`GameEndUI` → `_networkGameManager` SerializeField에 NetworkGameManager 오브젝트 연결~~ ✅ 완료 (2026-06-25, `Initialize()`에서 `FindFirstObjectByType` 자동 탐색으로 대체)
- ~~`NetworkStatusUI` → `_networkGameManager` SerializeField에 NetworkGameManager 오브젝트 연결~~ ✅ 완료 (기존 코드에 이미 `FindFirstObjectByType` 자동 탐색 적용됨)

---

#### 버그 수정 및 폴리싱
| 항목 | 상태 |
|------|------|
| 건물 배치 팝업 3행 버튼 가로폭 불일치 | ✅ 완료 (2026-05-19) — Human/Spirit(7개 건물) 시 3행 버튼 1개가 전체 가로폭 채우던 버그. SetActive(false) → CanvasGroup alpha=0 전환으로 HorizontalLayoutGroup 레이아웃 공간 보존. BuildingPlacementUI.cs 수정 |
| 건물 생성/파괴 시 유닛 이동 멈춤 | ✅ 완료 (2026-05-17) — OnPathInvalidated에서 코루틴 즉시 재시작 대신 _pendingPath 예약 방식 도입. 다음 타일 도착 시점에 부드럽게 경로 교체. 앞 타일에 건물이 생긴 경우만 즉시 재시작 (건물 관통 방지). UnitView.cs 단독 수정 |
| 랠리포인트 깃발 상대팀에도 표시되는 버그 | ✅ 완료 (2026-05-16) — RallyPointChangedEvent에 TeamId 추가, ProductionTicker에 팀 필터 추가. 멀티: 각 플레이어 자신의 깃발만 표시. 싱글플레이 영향 없음 |
| 랜덤 매칭 후 캐릭터 잘못 표시 버그 | ✅ 완료 (2026-05-15) — Lobby 씬 CharPreview 오브젝트가 실제 유닛 프리팹 인스턴스(NetworkTransform 포함)여서 Host 캐러셀 위치가 Red 클라이언트로 동기화되던 원인 확정. Unpack Completely + NetworkObject 계열 컴포넌트 5종 제거 |
| 자동생산 반복 순환 시 골드 미소모 (BUG-20) | ✅ 완료 (2026-04-04) — CompleteProduction IsCharged 리셋 누락 수정 |
| Pistoleer Idle 첫 프레임 동결 | ✅ 완료 (2026-04-06) — Pistoleer.controller Idle 상태 m_Speed: 0 → 1 수정 |
| Android 실기기 캐릭터 잔상 + RenderPass 에러 | ✅ 완료 (2026-04-06) — RT antiAliasing 2→1, Camera allowMSAA/allowHDR false, backgroundColor alpha 1 |
| 근접 공격 거리 다듬기 | ✅ 완료 (2026-04-11) — 유닛 vs 유닛 0.35f, 유닛 vs 건물 0.55f (타겟 타입별 분리) |
| 타겟 고정(Target Lock) 데미지 불일치 버그 | ✅ 완료 (2026-04-18) — 멀티플레이에서 애니메이션 타겟(B)과 다른 유닛(C)에게 데미지 적용되던 버그. NetworkCombatController.TickCombat() damageTargetId 분리로 수정 |
| 생산 슬롯 깜빡임 버그 (등록 경로) | ✅ 완료 (2026-04-19) — 큐 비어있을 때 자동 등록 시 슬롯2→슬롯1 1프레임 이동. AddNewAutoSlot에서 즉시 TryStartNext 호출로 수정 |
| 생산 슬롯 깜빡임 버그 (완료 사이클 경로) | ✅ 완료 (2026-06-05) — 자동생산 완료 시 재순환 항목이 슬롯2에 1프레임 표시되는 버그. CompleteProduction에서 ChargeVisibleSlots+이벤트 직접 발행 제거 → TryStartNext 즉시 호출로 수정 (UnitProductionUseCase.cs) |
| 자동생산 재등록 슬롯 중복/누락 버그 | ✅ 완료 (2026-06-05) — 자동 해제/재등록 시 슬롯 중복 표시 및 슬롯3 미추가 버그 3케이스. CurrentIsAuto를 수동 필드에서 파생 계산 getter로 구조 개선(ProductionState.cs), RegisterAutoType에 PendingQueue.Count==0 조건 추가, GameSystemRules 규칙 20 보완 |
| 랠리포인트 Client 무시 버그 | ✅ 완료 (2026-04-19) — 멀티플레이 Client(Red팀)에서 랠리포인트 설정이 서버에 전달되지 않던 버그. NetworkProductionController에 SetRallyPointServerRpc 추가, ProductionPanelUI에 네트워크 분기 추가 |
| 근접유닛 뒷무빙 현상 | ✅ 완료 (2026-04-26) — Phase 1 타겟 사망 시 무조건 Phase 2 진입으로 후방 스냅 발생. 타겟 사망 즉시 다음 적 재선택 + Phase 2 후방 스냅 방지 + 점유 누수 방지 (UnitView.cs 3곳 수정) |
| Phase 1 중 타일 소유권 미갱신 | ✅ 완료 (2026-04-26) — Phase 1(월드 직선 추적) 중 유닛이 타일을 지나가도 소유권이 갱신되지 않던 구조적 문제. TileOwnershipService(Pull 모델)로 매 프레임 물리 위치 기반 실시간 점령 |

---

### ⚠️ 알려진 미완성/버그 항목

#### 멀티플레이 기능 미구현
| 항목 | 파일 | 비고 |
|------|------|------|
| BuildFailedClientRpc UI 피드백 없음 | NetworkBuildingController | RPC 구조 완성, UI 기획 후 구현 예정 |
| EnqueueFailedClientRpc UI 피드백 없음 | NetworkProductionController | 싱글플레이 피드백 완료(2026-05-16). 멀티플레이 분기(RPC)는 별도 작업 예정 |
| 재접속 실제 구현 없음 | ReconnectionHandler | 30초 대기 후 ForceWin만 |
| 로비 UI 비주얼 폴리싱 | Lobby Views | UI 에셋 제작 완료 (2026-05-30) — 비주얼 폴리싱 작업만 잔여 |

#### GameConfig 코드 기본값 vs Inspector 값
- AnimationFps 필드 제거 완료 (2026-03-09 — 미사용 필드)
- TileHeight 코드 기본값 수정 완료: PointyTop=0.866, FlatTop=0.866
- FlatTop GridHeight 코드 기본값 수정 완료: 20
- CameraZoomDefault 수정 완료: 7

---

### ❌ 미구현 기능

| 기능 | 우선순위 | 관련 Phase |
|------|---------|-----------|
| 마법 타워 (Magic Tower) | 낮음 | Phase 3 |
| 연구소 (Research Lab) | 낮음 | Phase 3 |
| 유닛 AI 상태머신 | 낮음 | Phase 3 |
| 타임라인/서든데스 시스템 | 낮음 | Phase 3 |
| 사운드/BGM — Inspector 작업 + 실기 테스트 | 높음 | Phase 4 |
| 튜토리얼 | 낮음 | Phase 4 |
| 게임 내 밸런싱 | 중간 | Phase 4 |
| 로그인 시스템 구현 (Login.unity) | 낮음 | Phase 4 |
| Firebase 백엔드 (랭킹/실시간 리더보드/IAP) | 낮음 | Phase 4 |
| 카드 수집 시스템 | 낮음 | Phase 4 |

---

#### 개발 도구 / 에이전트 인프라
| 항목 | 상태 | 비고 |
|------|------|------|
| document-manager 에이전트 | ✅ 완료 (2026-06-23) | 프로젝트 전체 문서 통합 관리 전담. CLAUDE.md / AGENTS.md / WORKFLOW.md / 설계 문서 / 메모리 파일 / Task 문서 등 전 문서 담당. `.claude/agents/document-manager.md` |

---

## 기술 스택 현황

| 항목 | 기술 | 버전 |
|------|------|------|
| 게임 엔진 | Unity | 6000.0.x (Unity 6 LTS) |
| 렌더 파이프라인 | URP | Universal Render Pipeline |
| 네트워크 | Netcode for GameObjects | 2.9.2 |
| 멀티플레이 서비스 | Unity Multiplayer Services | 2.0.0 (Lobby+Relay 전용) |
| 인증 (로그인) | Firebase Authentication + Google Play Games Plugin | Firebase SDK v13.11.0 + GPGS v2.1.0 설치 완료 (런타임 설정 미완료) |
| 이벤트 시스템 | UniRx | 7.1.0 |
| 3D 모델링 도구 | Meshy.ai | Image-to-3D 파이프라인 |
| 애니메이션 | Mixamo + Unity Animator | Mecanim |
| 셰이더 | Shader Graph (SG_HexTile) | Object Space SDF 기반 |

---

## 아키텍처 현황

```
Bootstrap
  └── GameBootstrapper (유일한 composition root)

Domain ← (참조 금지) Core
  ├── HexCoord, HexGrid, HexPathfinder
  ├── UnitData, BuildingData (IDamageable)
  └── HexOrientationContext (정적 홀더)

Application
  ├── UseCases (Combat, Movement, Spawn, Production, ...)
  ├── Interfaces/IEntityPositionProvider (2026-03-02 추가)
  ├── NetworkContext (정적 홀더 — 네트워크 상태)
  └── GameEvents (UniRx Subject 허브)

Core
  ├── HexMetrics (헥스↔월드 좌표 변환, XZ 평면)
  └── ViewConverter (팀별 관점 반전, Red팀만)

Infrastructure
  ├── Config/GameConfig (ScriptableObject)
  ├── Factories/UnitFactory, BuildingFactory
  ├── UnitWorldPositionProvider (IEntityPositionProvider 구현)
  └── Network/ (NetworkXxxController × 8)

Presentation
  ├── Grid/HexTileView, HexGridRenderer
  ├── Unit/UnitView (Lerp + Animator + Register/Unregister)
  ├── Camera/CameraController (XZ 레이캐스트 팬, 55도 틸트)
  ├── Input/InputHandler (XZ 평면 입력)
  ├── UI/ (HUD, 생산 패널, 건물 배치, 게임 종료)
  └── UI/Views/Lobby/ (MVVM — LobbyRootView, TabBarView, BattleRootView + 서브뷰 8종)
       └── UI/ViewModels/ (LobbyViewModel, BattleViewModel — UniRx ReactiveProperty)
```

---

## 에셋 현황

### 3D 모델 (Meshy.ai)
| 에셋 | 경로 | 상태 |
|------|------|------|
| Pistoleer 유닛 (Blue/Red) | Prefabs/Units/Unit_Pistoleer_Blue/Red.prefab | ✅ 완료 |
| Assault 유닛 (Blue/Red) | Prefabs/Units/Unit_Assault_Blue/Red.prefab | ✅ 완료 |
| Sniper 유닛 (Blue/Red) | Prefabs/Units/Unit_Sniper_Blue/Red.prefab | ✅ 완료 |
| Castle (Blue/Red) | Prefabs/Buildings/Building_Castle_Blue/Red.prefab | ✅ 완료 |
| Barracks (Blue/Red) | Prefabs/Buildings/Building_Barracks_Blue/Red.prefab | ✅ 완료 |
| MiningPost | Prefabs/Buildings/Building_MiningPost.prefab | ✅ 완료 |
| GoldMineTile | Prefabs/Buildings/GoldMineTile.prefab | ✅ 완료 |
| RallyPointMarker | Prefabs/Misc/RallyPointMarker.prefab | ✅ 완료 |

### 타일
| 에셋 | 경로 | 상태 |
|------|------|------|
| HexTile (FlatTop) | Prefabs/Tiles/HexTile.prefab | ✅ 완료 (ProBuilder + SG_HexTile) |

### 미제작 에셋
| 에셋 | 용도 |
|------|------|
| 방어타워/마법타워/연구소 3D | 미구현 건물 타입 |
## 2026-07-16 추가 완료: 로비 프로필/랭킹 클라우드 연동

- `codex/profile-cloudsave-leaderboard-port` 작업에서 Firebase 인증 이후 UGS Cloud Save 기반 플레이어 프로필, 닉네임 코드, 전적 표시, 닉네임 변경 팝업, UGS Leaderboards 기반 랭킹 테이블을 로비 UI에 1차 통합했다.
- 이메일 회원가입/인증 완료 후 최초 로그인 시 닉네임 설정 화면을 거치도록 보완했다.
- Profile/Ranking 탭은 CanvasGroup 기반 표시/숨김 규칙을 유지하며, 숨겨진 랭킹 패널이 로비 진입 시 자동 로드되지 않도록 랭킹 데이터 로드 시점을 탭 선택/수동 새로고침으로 제한했다.
- 기본 UI 레이아웃은 런타임 보정 + 에디터 생성 스크립트 기준값을 함께 조정했다. 세부 픽셀 튜닝은 Unity Inspector에서 후속 조정한다.
- 후속 이메일 인증 플로우 보정은 2026-07-18 완료됨. 인증 대기 화면 이메일 표시, 가입 취소/계정 삭제 정책, 앱 재실행 자동 로그인 게이트, 닉네임 미설정 Lobby 우회 차단을 반영했다.
---

## 2026-07-18 완료: 이메일 인증 플로우 보정

- 이메일 인증 대기 화면 진입 시 실제 입력 이메일과 진입 원인(`SignUpPending` / `ExistingUnverifiedLogin`)을 명시적으로 전달하도록 보정했다.
- 신규 이메일 회원가입 직후 인증 대기에서 뒤로가기를 누르면 가입 취소 확인 후 현재 미인증 Firebase 사용자를 삭제하는 흐름을 추가했다.
- 기존 미인증 계정 로그인 후 인증 대기에서 뒤로가기를 누르면 계정은 삭제하지 않고 로그아웃 후 이전 로그인 화면으로 복귀한다.
- 인증 대기 화면에서 앱을 종료/강제 종료하면 가입 취소로 보지 않고, 재실행 시 미인증 계정은 인증 화면으로 복귀한다.
- 인증 완료 후 닉네임을 설정하지 않은 계정은 재실행/자동 로그인 경로에서도 Lobby로 우회 진입하지 않고 닉네임 설정 화면으로 복귀한다.
- 사용자 실기 확인: 실제 이메일 표시, 가입 취소 팝업, 미인증 Firebase 계정 삭제, 인증 계속하기 유지, 인증 화면 재실행 복귀, 닉네임 설정 화면 재실행 복귀 PASS.
