# Game Design Lead Memory — Hexiege

## 2026-08-24 동적 건물 재탐색 판정 기준
- 건물로 현재 경로가 무효화돼도 서버 경로 그래프에 우회로가 있으면 같은 목표로 이동을 계속해야 한다. `Blocked`는 현재 environment revision에서 실제 유효 경로가 없을 때만 정상이다.
- 건물로 현재 `path[0]`이 non-walkable이 된 경우 유닛이 이미 서 있는 그 타일에서 인접 walkable 타일로 빠져나가는 첫 구간만 허용한다. 재진입·건물 관통·비인접 점프는 금지한다.

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)

## 게임 컨셉
- 장르: 헥스 그리드 기반 1v1 실시간 전략 (RTS)
- 플랫폼: 모바일 세로 화면 (9:16, Android/iOS)
- 테마: Clash of Clans 스타일 Orthographic 이소메트릭 (55도 틸트), 3D 메시
- 매칭: P2P Host-Client (Unity Relay NAT 관통)

## 핵심 게임플레이 루프
1. 시작: 양 팀 Castle 자동 배치 + 시작 골드 500
2. 확장: 타일 점령 → 인구 한도 증가 → 건물 건설
3. 생산: 배럭 → 유닛 생산 큐 → 랠리포인트 설정 → 자동 공성
4. 전투: 유닛이 이동 중 인접 적 자동 공격 (IDamageable)
5. 승리: 적 Castle HP = 0이 될 때까지 공성

## 핵심 시스템별 확정 설계

### 연구소 유닛 강화 + 전투 스탯 ×10 (2026-07-23 밸런스 확정 → 2026-07-31 구현·멀티 실기 PASS)
- **강화 3스탯 + 초월 자연회복**: 공격력(레벨당 `Round(기본×8%)` 고정 정수 등차, Lv5≈×1.40) / 방어력(신규, 0/8/16/24/32/40, 감쇄 `Max(1,Round(공×(1−방/(방+120))))` K=120 Lv5≈25%) / 이동속도(×1.000~×1.320) + 초월 자연회복(0/3/6/9/12/15 HP/s). 최대 HP는 강화 대상 아님. 종족별 그룹×스탯 트랙(인간 9·정령 9·초월 7).
- **전투 스탯 ×10 스케일**: HP·공격력 동일 배율 → **TTK 불변**(상성·매치업 불변, 강화 그리드만 정규화 — ×10 없이 +8%는 죽은 레벨/불규칙 증가폭 발생). 사거리·이동속도·쿨다운·비용·비율은 불변. 값 SSOT: `StatsReference.md`, old/new 대조: task `BalanceReview.md`.
- **비용(효과 동일·비용만 배율)**: 표준 트랙 1,000골드(80/120/180/260/360)/15~70초, 그룹 배율 초월 동물 ×2.0·자연회복 ×2.5·(초월 식물 포함) 그 외 ×1.0. 연구소 건설비 200(불변).
- **확정 쟁점**: F-1 Tank vs Fox/Rabbit 원샷 유지(코스트 상성, TTK 불변, HP 미변경) / F-2 방어 8~40·K=120(대안 80~400/K=1200과 감쇄율 동일) / F-3 초월 식물 비용 ×1.0. **DoT 틱값은 공격력 연구·방어력 감쇄 어느 쪽에도 영향 없는 고정값.** 힐량은 그룹 공격력 트랙 조회(BloomFairy 200→280 식물, TorrentSpirit 100→140 물×0.5).
- 규칙: `GameSystemRules_Upgrade.md`(구현 계약·규칙 1~13). 후속 보류: AI 연구 사용 실기·UI 레이아웃·MistShrine 힐(미구현).

### 헥스 그리드
- 좌표계: Cube (Q, R, S=-Q-R)
- 기본 맵: FlatTop 10×29 (멀티플레이 기본)
- 보조 맵: PointyTop 7×17 (지원)
- 타일 색상: Neutral=회색, Blue=파랑, Red=빨강, Selected=노란 틴트

### 유닛 스탯 (2026-03-14 최종 확정)

| 항목 | Pistoleer(권총병) | Assault(돌격소총병) | Sniper(저격총병) |
|------|-----------------|-------------------|----------------|
| HP | 30 | 50 | 30 |
| 공격력 | 3 | 6 | 20 |
| 사거리(float) | 1.0 | 2.0 | 5.0 |
| 이동속도(초/칸) | 1.0 | 1.0 | 4.0 |
| 생산시간 | 5초 | 10초 | 15초 |
| 골드비용 | 50 | 100 | 200 |
| 인구 | 1 | 1 | 1 |
| UnitType enum | Pistoleer=0 | Assault=1 | Sniper=2 |

- **사거리 설계 의도**:
  - Pistoleer 1.0: 인접 타일(0.866 world units) 범위
  - Assault 2.0: 2타일 범위 (약 1.73 world units)
  - Sniper 5.0: 5타일 원거리 (약 4.33 world units)
### 특수 유닛 AoE 밸런스 (2026-07-17, 도끼병 첫 구현·실기 PASS)
- **도끼병(BattleAxe)**: HP 80 / 공격력 15 / **attackRange 0.75**(0.5에서 사용자 실기 조정) / detectRange 1.0 / moveSpeed 1 / attackCooldown 3.05 / 생산 20초·200골드·인구 1. 근거리A 라인 3단계(HumanBarracks) 생산.
- **휩쓸기형 AoE 밸런스 값 2종(별개, 혼동 주의)**: 유닛 `attackRange`(주 타깃 공격/추격 거리, UnitStatsConfig) vs 특수 AoE `sweepReach`(부채꼴 반경, SpecialAttackConfig SO, **현재 실기값 0.75**, 반각 `sweepArcHalfAngle` 120°).
- **판정 방식 확정**: 초기 "전방 5타일" 타일 기준에서 **월드 좌표 전방 부채꼴**(공격자→주 타깃 방향 기준 XZ 거리 ≤ reach AND 각도 ≤ 반각)로 변경. 아군/주 타깃/공격자/사망 제외, 건물 미대상. 겹친 적 포함. 상세: GameSystemRules_Units 규칙 23~27.
- **튜닝 caveat**: 헥스 인접 타일 중심 간 거리 ≈ 0.9~1.0 월드(FlatTop, TileWidth/Height=1.0). sweepReach 튜닝 시 이 값 + 상대 유닛 사거리(예: 피스톨러 1.0)와의 관계 고려 — reach가 크면 전방 원거리 유닛까지 휩쓸림. SpecialAttackConfig.asset은 Inspector 편집(코드 재컴파일 불필요)이나 GameBootstrapper `_specialAttackConfig` 배선 확인 필수.
- **잔여 특수 유닛 4종(QuakeSpirit 착탄형/TorrentSpirit 파도형/MushroomBomber 착탄형/BloomFairy 힐)**: StatsReference.md 설계 그대로 미구현. 동일한 전략 핸들러 구조 위에서 확장 예정.

- **팀별 피아식별**: Blue/Red 각각 별도 프리팹 사용, 에셋+코드 연동 완료 (2026-03-14)
- **3D 모델**: Meshy.ai 제작, Mixamo 애니메이션 (Walk/Attack/Dead)
- **Animator**: Walk/Attack/Dead 스테이트, IsDead bool 파라미터 (Animator.Play() 직접 호출)
- **방향 표현**: Y축 회전 (NE=30°, E=90°, SE=150°, SW=210°, W=270°, NW=330°)
- 미래 계획: 3종족, 추가 유닛 타입 (TDD Phase 3)

### 건물
| 타입 | HP | 건설 비용 | 역할 |
|------|----|-----------|------|
| Castle | 50 | 자동 배치 | 본기지, 파괴 시 패배 |
| Barracks | 30 | 100골드 | 유닛 생산 |
| MiningPost | 20 | 50골드 | 채굴소, 금광 타일 전용 |

**3D 모델**: Castle/Barracks/MiningPost는 Meshy.ai Image-to-3D 제작 및 Blue/Red 팀별 프리팹 연동 완료. 신규 유닛/건물은 `AssetList.md`와 `PROJECT_STATUS.md` 기준으로 상태 확인 후 기획한다.

### 자원 시스템
- 시작 골드: 500
- 기본 수입: 0 (채굴소 없으면 무수입)
- 채굴소 수입: 10골드/초
- 배럭 비용: 100골드, 채굴소 비용: 50골드

### 인구 시스템
- 최대 인구 = 보유 타일 수
- 사용 인구 = 건물 수 + 유닛 수

### GameSystemRules.md — 구현 기준 규칙서 (2026-05-14 전면 개편)

**파일 위치**: `Assets/_Project/Docs/GameSystemRules.md`

아이디어/기획 의도가 아닌, 코드 구현 시 기준이 되는 구체적 규칙 모음. 총 16개 규칙.

**2026-05-14 주요 변경**:
- 회전 방향 규칙을 별도 섹션에서 이동/전투 섹션으로 통합
- "즉시 회전" → "서서히 회전(RotateTowards)" 전면 교체
- 규칙 전체 재번호화(1~16)

| 규칙 | 내용 |
|------|------|
| 규칙 7 | A* 이동 중 다음 타일 방향으로 서서히 회전 |
| 규칙 8 | 전투 종료 후 재개 시 이동 방향 바라보며 서서히 회전 (뒤 바라보며 이동 금지) |
| 규칙 12 | 전투 이동 중 타겟 방향으로 서서히 회전 |
| 규칙 15 | 공격 중 타겟 방향으로 서서히 회전 |

---

### 전투
- 이동 중 인접(거리 1) 적 자동 공격
- Lerp 이동 중 매 프레임 거리 체크 → 적 발견 시 공격
- ClaimedTile: 이동 중 타일 선점(같은 팀만 차단, 적팀 투과)
- 타일 중앙 도착 = 전투 승리 = 점령

### 생산 시스템 (전역 규칙 5가지 확정 — 2026-03-23)
- 수동 모드: 탭 → 큐 추가 (최대 대기 2 + 생산 중 1)
- 자동 모드: 롱프레스(0.5초) 토글, 지정 유닛 무한 생산 (최대 3종)
- 랠리포인트: 배럭 클릭 후 타일 지정, 생산된 유닛 자동 이동
- 공성 시스템: 랠리 도착 후 적 Castle 방향 자동 전진

**전역 규칙 5가지** (상세: `GameDesignDocument.md` → "생산 패널 운영 규칙"):
1. 생산 취소(슬롯 X 버튼) 시 항상 전액 환불
2. 슬롯에 표시된(골드 차감된) 자동 항목은 자동 취소 후에도 수동 큐 이관으로 생산 유지
3. 수동 추가 시 자동 모드 해제 (Rule 2 이관 선행)
4. 생산큐 최대 3개 = 현재 생산 중 + 수동 대기 (자동 대기 별도)
5. 비용 차감은 슬롯에 표시되는 시점 (슬롯 풀이면 미차감, 슬롯 진입 시 차감)

### 승패 조건
- 적 Castle HP = 0 → GameEndUseCase → OnGameEnd 이벤트 → UI 표시

## 팀별 관점 (확정)
- Blue팀: 자기 Castle 맵 하단 → 화면 하단에 보여야 함 (그대로)
- Red팀: 자기 Castle 맵 상단 → 화면 하단에 보여야 함 (반전 필요)
- 채택 방식: **ViewConverter** (좌표 변환, 메시 뒤집기 없음)
  - `ToView(pos) = 2*mapCenter - pos` (Red팀만, Blue팀은 항등)
  - 유닛 이동 방향도 FlipDirection() 적용
  - 카메라 Z축 180° 회전 방식은 폐기 (메시 뒤집힘 문제)

## 멀티플레이 기획
- Host = Blue팀, Client = Red팀
- 서버(Host) 권위 모델: 모든 행동 서버 검증
- 동기화 대상: 건물 배치, 유닛 생산, 타일 소유권, 자원, HP, 승패
- 클라이언트 예측: 유닛 이동 (로컬 즉시 + 서버 동기화)
- 재접속: 30초 대기 후 남은 플레이어 승리 처리

## 구현 완료 기능
- 헥스 그리드 (PointyTop/FlatTop 듀얼 지원)
- 유닛 이동 + A* 경로탐색
- 유닛 3D Animator 기반 애니메이션 (IsWalking/IsDead/Attack)
- 전투 시스템 (IDamageable, 이동 중 자동 공격)
  - **전투 거리 정밀도 개선 (2026-03-02)**: 월드좌표 기반 거리 체크 (IEntityPositionProvider)
  - FlatTop 인접 타일 거리 = 0.866 world units 균일 → 단일 임계값으로 정밀 판정
- 건물 배치 (Castle/Barracks/MiningPost)
- 자원 시스템 (골드 수입/지출)
- 인구 시스템
- 유닛 생산 (수동/자동 + 랠리포인트 + 공성)
- 승패 판정 (Castle 파괴)
- HUD (골드/인구/타일 카운트)
- 멀티플레이 인프라 (Lobby/Relay/NGO Phase 1~8)
- ViewConverter (팀별 관점 반전, 구현 완료)
- **2D→3D 전환 완료 (2026-02-27~2026-03-01)**
  - XZ 평면 좌표계, Orthographic 55도 틸트 카메라
  - Animator 기반 유닛, sortingOrder 폐기
  - **건물 3D 모델 완료** (Castle/Barracks/MiningPost — Meshy.ai Image-to-3D, Blue/Red 팀별 프리팹)
  - **헥스 타일 3D 모델 완료** (ProBuilder Cylinder + SG_HexTile Shader Graph)
  - **금광 타일 3D 오브젝트 완료** (크리스탈 바위 더미, GoldMineTile.prefab)
  - **랠리포인트 마커 완료** (RallyPointMarker.prefab)
- **팀별 피아식별 에셋+코드 연동 완료 (2026-03-14)**
  - 유닛 Blue/Red 프리팹: Pistoleer, Assault(돌격소총병), Sniper(저격총병)
  - 건물 Blue/Red 프리팹: Castle, Barracks
  - 반응형 팝업 UI: ProductionPopup/BuildingPopup 앵커 기반 배치 전환
  - 팀별 초상화 동적 업데이트: ProductionPanelUI/BuildingPlacementUI Show() 시 팀별 스프라이트 교체
  - Assault/Sniper 생산 버튼 추가 (ProductionPanelUI) ✅
- **전투 범위 수정 (2026-03-14)**: epsilon +0.1f 제거 → 타일 점령 타이밍 정상화

## 싱글플레이 AI 시스템 (2026-06-10 완료)

### AI 시나리오 ScriptableObject 에셋 (2026-06-10 완료)
- 3종족 시나리오가 각 1파일에 3시나리오 내장된 형태로 완성됨 (종족당 단일 에셋).
- 파일명: `AIScenarioConfig_Human.asset` / `AIScenarioConfig_Spirit.asset` / `AIScenarioConfig_Transcendence.asset`
- 경로: `Resources/Config/`. 게임 시작 시 `GameRaceContext.RedRace`로 종족 판별 후 해당 에셋에서 무작위 시나리오 1개 선택.
- 레거시 Human_A/B/C 개별 에셋은 삭제됨.

### AI 시나리오 구조
- Phase 4단계: Phase 1(0~3분) / Phase 2(3~6분) / Phase 3(6~9분) / Phase 4(9분~종료)
- 시나리오 선택: 게임 시작 시 A/B/C 중 균등 확률(33.3%) 랜덤. `UnityEngine.Random.Range(0, 3)` 인덱스 사용.
- 난이도는 `delaySeconds` 배율(쉬움 ×1.5 / 어려움 ×0.7)과 `goldIncomeMultiplier`로만 반영.
- 채굴소(MiningPost 계열)는 Phase 2와 Phase 4에 병행 트랙으로 자동 처리.
- 골드 경제: 시작 500g + 채굴소 10g/s × 180s × 2 = 10분 기준 이론 최대 약 5,300g.

### Human 종족 시나리오 (완료)
- **건물 비용**: 1단계 100g / 2단계 업글 100g / 3단계 업글 200g / MiningPost 50g
- **시나리오 A — 물량형**: TrainingCamp→Gunsmith 각 3단계. 건물 총 비용 900g.
- **시나리오 B — 테크형**: 근거리A 3단계 + 총기 2단계 + 탈것 2단계. 총 1,200g.
- **시나리오 C — 균형형**: 근거리A 3단계 + 총기 3단계 + 탈것 1단계(+추가시간 2단계). 총 1,400g.

### Human 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 근거리A | TrainingCamp | WarAcademy | HumanBarracks | LittleKnight | SpearMan | BattleAxe |
| 1 — 총기류 | Gunsmith | Armory | WeaponForge | Pistoleer | Assault | Sniper |
| 2 — 탈것류 | Garage | VehicleBay | — | CannonCart | Tank | — |

### Spirit 종족 시나리오 (완료)
- **건물 비용**: 1단계 75g / 2단계 업글 200g / 3단계 업글 400g / ManaRift 50g
- 단일 라인 3단계 총 비용 675g. 3단계 업글(400g)이 비싸므로 Phase 3 진입 직후(15초 지연) 배치.
- 3개 시나리오 모두 동일 구조: 메인 라인 3단계 + 보조 라인 2단계(Phase 3) → 보조 라인 3단계(Phase 4). 총 1,450g.
- **시나리오 A — Spirit-Inferno**: 불 메인(InfernoSpirit) + 땅 보조(QuakeSpirit Phase 4).
- **시나리오 B — Spirit-Torrent**: 물 메인(TorrentSpirit) + 불 보조(InfernoSpirit Phase 4).
- **시나리오 C — Spirit-Quake**: 땅 메인(QuakeSpirit) + 물 보조(TorrentSpirit Phase 4).

### Spirit 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 불 | FireSpire | BlazeConduit | InfernoCore | EmberSpirit | FlameSpirit | InfernoSpirit |
| 1 — 물 | AquaSpring | TidalNexus | OceanicHeart | TideSpirit | StreamSpirit | TorrentSpirit |
| 2 — 땅 | StoneMound | TerraForge | GaeaSanctum | DustSpirit | BoulderSpirit | QuakeSpirit |

### Transcendence 종족 시나리오 (완료)
- **건물 비용**: 1단계 125g / 2단계 업글 200g / 3단계 업글 300g / FungalNode 100g
- 단일 라인 3단계 총 비용 625g. 1단계 건설(125g)이 타 종족보다 비싸 초반 멀티 라인 오픈 타이밍이 중요.
- **시나리오 A — Trans-Rush**: 동물A + 동물B 두 라인 모두 2단계(Phase 3), 동물B 3단계(Phase 4). 총 1,150g.
- **시나리오 B — Trans-Flora**: 동물A 3단계(BearGuard) + 식물 2단계(BloomFairy) + 동물B 1단계(FoxMagician, Phase 4). 총 1,275g.
- **시나리오 C — Trans-Beast**: 동물B 3단계(LionKnight) 우선 → 동물A 3단계(BearGuard, Phase 4). 총 1,450g.

### Transcendence 생산 건물 라인

| 라인 | Stage 1 | Stage 2 | Stage 3 | S1유닛 | S2유닛 | S3유닛 |
|------|---------|---------|---------|--------|--------|--------|
| 0 — 동물A | PrimalAltar | PrimalDen | PrimalSanctuary | RabbitTrickster | RhinoBreaker | BearGuard |
| 1 — 동물B | FeralAltar | FeralDen | FeralSanctuary | FoxMagician | EagleArcher | LionKnight |
| 2 — 식물 | SporePatch | FloralNursery | — | MushroomBomber | BloomFairy | — |

> 식물 라인은 2단계(FloralNursery)가 최상위. BloomFairy는 공격 불가 힐 전용 유닛.

---

## 미구현/미결 기획 항목
- 카메라 각도 최적화 (현재 55도 적용, 테스트 후 조정 가능)
- ~~AI Inspector 에셋 작업 (AIScenarioConfig_Spirit/Transcendence.asset 생성 + 수치 입력)~~ ✅ 완료 (2026-06-10): 3종족 단일 에셋 구조로 완성
- AI 실기 테스트 (빌드오더 동작 확인)
- 사운드/BGM
- 튜토리얼
- 밸런싱 (AI delay 수치는 예비값 — StatsReference.md 확정 후 경제 시뮬레이션 재조정 예정)
- PlayFab 백엔드 연동 (계정/랭킹/인앱결제)
- 멀티플레이 로비 UI 완성

### 2026-07-20 - 유닛 이동·공격 규칙 v2 확정

- 서버 권위를 유지한 공통 행동 단계를 AlignToMove / Move / Acquire·Chase / AlignToAttack / Windup / Impact / Recovery로 확정했다.
- 이동은 10° 이내에서 시작하고 이동 중 15°를 넘으면 재정렬한다. 공격은 5° 진입·8° 유지 기준이며, 커밋 전 취소는 무비용이고 커밋 후에는 쿨다운을 환불하지 않는다.
- 공격 전달 방식은 MeleeContact / Hitscan / ProjectileImpact / TravelingArea로 분리하고 TargetScope·AreaShape·Effect·Schedule은 독립 축으로 둔다.
- BloomFairy의 성공 발동 후 3초, Windup 포함 총 4초 예외는 유지했다. 구체 규칙은 `GameSystemRules_Units.md`와 `GameSystemRules_UnitCombatSynchronization.md`가 권위다.
- 문서 설계만 완료했으며 25종 런타임 구현·멀티 검증은 아직 0/25다.

## 비주얼 목표
- 레퍼런스: Clash of Clans, Clash Royale
- 스타일: 카툰/스타일라이즈드, 밝고 선명한 색상
- 뷰: Orthographic 55도 탑다운 이소메트릭
- 모바일 최적화: 로우~미드폴리 3D 모델
### 2026-07-16 - Profile/ranking UX policy update

- Lobby Profile/Ranking cloud slice is complete from a UX-policy perspective: verified email/Google users should have a nickname code, Profile shows account/profile/stats/rank, and Ranking lists only eligible leaderboard entries.
- Nickname setup remains mandatory for first verified login before entering Lobby; nickname change is available from Profile as a modal flow.
- Next UX decision: email verification abandonment. Fresh sign-up verification and existing unverified-login retry should be treated as separate states so the player understands whether going back cancels sign-up or only exits verification retry.
