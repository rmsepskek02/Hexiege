# Game System Rules — 인덱스

구현 시 따라야 하는 게임 시스템별 규칙 모음.
아이디어나 기획 의도가 아닌, 실제 코드로 구현할 때 기준이 되는 구체적인 규칙을 기록한다.

세부 규칙은 아래 파일에 있다. Plan.md 작성 전 관련 파일을 반드시 읽는다.

---

## 파일 목록

| 파일 | 포함 시스템 |
|------|------------|
| [GameSystemRules_Map.md](GameSystemRules/GameSystemRules_Map.md) | 대전 맵 전체 180도 대칭, 중앙/대응쌍 광산 공정성, 정적 최단 접근거리 검증 |
| [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) | FlatTop 11×21 무작위 대전 맵 유형별 생성·광산·건설 제한·seed·폴백·검증 (유형 목록과 개수는 3장 「유형별 사양」이 단일 소스) |
| [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) | 공통 UI 규칙, 생산 패널 UI, MistShrine 패널 UI, 건물 배치 패널 UI, 인게임 설정 메뉴, 로비 설정/프로필 UI |
| [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md) | 유닛 이동 시스템(건물로 경로가 막혔을 때의 동작 포함), 전투 진입, 전투 연계, 전투 연출 동기화, 애니메이션 상태 동기화, 특수 공격 시스템, 방어력 데미지 감쇄 (특수 공격 확장의 목록과 개수는 「특수 공격 시스템 규칙」 절이 단일 소스) |
| [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 랠리포인트 시스템, 건물 철거 시스템, 방어 타워 시스템, MistShrine 물안개 힐 시스템 |
| [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md) | 스킬 건물, 쿨다운/스킬 수 공통 규칙, 3×3 스킬 UI, 스킬 메커니즘 타입, 발동 경로, 모바일 지점 조준 UX, 서버 권위, 추후 데이터로 확정할 항목 (건물과 타입의 목록·개수는 「스킬 건물 3종 정의」·「스킬 메커니즘 타입」 절이 각각 단일 소스) |
| [GameSystemRules_Upgrade.md](GameSystemRules/GameSystemRules_Upgrade.md) | 연구소 기반 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI |
| [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md) | Canvas SortingOrder 구조, 씬별 Canvas 계층, 전역 UI z-order, 새 Canvas 추가 시 규칙 |
| [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md) | BGM 전환 규칙, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md) | AI 난이도 시스템, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘, 아키텍처 및 구현 규칙 |
| [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md) | Human 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md) | Spirit 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md) | Transcendence 종족 AI 빌드오더 시나리오 A/B/C |

---

### 사운드 관련 작업
→ [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md)
- AudioManager 레이어 및 DontDestroyOnLoad 규칙
- BGM 전환 시점 — 씬 전환·게임 시작·게임 종료 계기 외에, `AudioManager Initialize()`가 현재 씬 이름을 확인해 즉시 재생하는 **최초 진입 경로**가 따로 있다. 계기와 BGM 의 대응 전체는 `GameSystemRules_Sound.md` 규칙 6 이 단일 소스
- 🔴 Victory/Defeat BGM 분리는 **아직 하지 않는다** — V1 은 싱글/멀티 구분 없이 게임종료 BGM 하나이며, 분리는 `GameSystemRules_Sound.md` 규칙 11 이 「향후 작업」으로 미뤄 둔 **미확정** 사항이다(선행 조건 있음)
- BGM 크로스페이드 방식
- SFX 2D 고정, 동시 재생 한도 있음 — 한도 값과 그 값을 조정하는 Inspector 필드의 단일 소스는 `GameSystemRules_Sound.md` 규칙 13
- VFX+SFX 쌍 호출 규칙
- 볼륨 채널 분리와 PlayerPrefs 저장 — 채널 구성의 단일 소스는 `GameSystemRules_Sound.md` 규칙 18, 저장 키는 규칙 21
- 볼륨 컨트롤 버튼 구성과 음소거 상태별 슬라이더 색상 — 버튼 목록의 단일 소스는 `GameSystemRules_Sound.md` 규칙 23, 색상은 규칙 26

---

## 시스템별 빠른 참조

### 맵 관련 작업
→ [GameSystemRules_Map.md](GameSystemRules/GameSystemRules_Map.md)
- 모든 맵 생성 요소와 장식의 정확한 180도 대칭
- 중앙 단독 광산 직접 대칭 / 180도 대응 광산 쌍의 교차 거리·접근성 대칭
- 팀별 시작 광산 개수·거리·초기 채굴소 상태·경제 효과 대칭
- 정적 장애물과 초기 건물을 포함한 성 인접 「시작 칸」→광산 덩어리 인접 「도착 칸」 최단 접근거리 대칭 및 도달 가능성 (재는 단위는 광산 하나가 아니라 광산 덩어리 — 두 이름의 정의는 `GameSystemRules_Map.md` 규칙 4)
- 새 맵 추가, 크기/orientation 변경, 성·광산·정적 장애물·초기 건물·건설 불가 구역·장식 배치 변경, 초기 경제 변경 시 재검증 — 계기 목록과 재검증 항목의 단일 소스는 `GameSystemRules_Map.md` 규칙 5

→ [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md)
- FlatTop 11×21, 맵 유형 동일 확률 선택 — 유형 목록과 개수의 단일 소스는 `GameSystemRules_RandomMap.md` 3장
- 유형별 지형·통로·중립 광산 수·건설 불가 구역
- 초기 골드는 두 갈래로 갈린다 — 정상 모드는 중립 광산 수에서 파생하고, 명시적 테스트 모드는 광산 수와 무관한 고정값이다. 두 갈래의 값과 조건의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 3
- 시작 공간 10타일
- 결정적 seed, 최대 100회 재시도, 유형별 고정 템플릿 폴백과 생성 로그 — 폴백에서 어느 값이 템플릿 값으로 교체되는지의 단일 소스는 `GameSystemRules_RandomMap.md` 규칙 12

### UI 관련 작업
→ [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md)
- Canvas SortingOrder 구조는 [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md)를 함께 확인
- Canvas Scaler, 앵커 기반 배치, Filled/Simple 이미지 자식 앵커, Safe Area, CanvasGroup 숨김/표시
- 폰트, 골드 부족 표시
- 팝업 규칙 한 벌 — 팝업/모달 타입 구분, 배경 탭으로 닫히는지의 타입별 차이, 팝업 중첩, 대상 건물 파괴 시 자동 닫힘. 단일 소스는 `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 8~11
- 로딩 인디케이터: `UIManager.ShowLoading()`으로만 제어, 호출해야 하는 상황, `ShowLoading(false)` 책임 소재, null-safe 패턴 — `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 L-1~L-4
- 무작위 맵 준비 실패 UI: 내부 정보 비공개, 멀티플레이 최초 경기 실패 · 멀티플레이 NewMap 재경기 실패 · 싱글플레이 실패 각각의 표시와 action 구성 — `GameSystemRules_UI.md` 「공통 UI 규칙」 규칙 M-1~M-4. 🔴 재경기 실패의 **팝업 여부와 싱글 최초 실패의 표시 문구·loading UI 처리는 미정**이며 그 미정 표시도 같은 규칙들이 단일 소스다
- 생산 패널: 큐 구조, 골드 차감 시점, 자동 생산, 토스트 메시지
- MistShrine 패널: `BuildingPanelBase` 상속 전용 패널, 3×3 그리드 배치, 짧은 탭=수동 시전 / 롱프레스=자동 모드 토글(기본 OFF), 쿨다운 오버레이, 범위 원 표시(아군 패널 열림 중에만), 회복 텍스트 — 건물 동작 규칙의 단일 소스는 `GameSystemRules_Buildings.md`
- 건물 배치 패널: 비용 표시, 실패 피드백, 무작위 맵 타일 클릭 판정(판정 우선순위 · 막힌 타일과 빈 공간 · 건설 불가 타일의 자기 팀 / 중립·적 분기) — `GameSystemRules_UI.md` 「건물 배치 패널 UI」 규칙 5~8
- 인게임 설정 메뉴: 일시정지, 포기 처리, 재경기, 프로필 서브 패널
- 로비 설정/프로필 UI: ProfilePanel/SettingPanel 탭 분리

### 유닛 관련 작업
→ [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md)
- A* 이동, 공유 타일 상태, 경로 재계산
- 상태 머신 (A* 이동 / 전투 이동 / 공격)
- 감지/공격 사거리, 타겟 선택, AoE
- 전투 연출 동기화 (타격 프레임 단일 소스, 피격 표현 큐, 원거리 트레이서, 애니메이션 상태 값 기반 동기화)
- 특수 공격 시스템 (전략 핸들러 구조, 휩쓸기 월드 부채꼴 판정, SpecialAttackConfig 튜닝, AoE 연출 동시 방출)
- 🔴 같은 「특수 공격 시스템 규칙」 섹션에 **힐과 지속 효과 규칙이 함께 들어 있다** — 힐(회복) 서브시스템, 힐러 전용 경로, 부상 아군 탐색, HoT/DoT 공용 시간 지속 효과, 힐러 유휴 감시, 힐러 쿨다운 예외, HoT 힐 텍스트 집계, 착탄형·단일 대상 DoT 확장 (`GameSystemRules_Units.md` 규칙 30~37 · 40~42)
- 방어력 데미지 감쇄 — 공식·상수·삽입 지점의 단일 소스는 `GameSystemRules_Units.md` 규칙 44
- 건물로 경로가 완전히 막혔을 때의 유닛 동작 — 2026-08-26 에 맵 문서에서 이관돼 온 규칙이다. 단일 소스는 `GameSystemRules_Units.md` 규칙 45

### 건물 관련 작업
→ [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md)
- 랠리포인트 표시/숨김
- 철거 처리, 골드 환불, 생산 큐 처리(생산 건물 한정), 연쇄 처리
- 방어 타워: 타겟 선택과 사거리 판정, 배치 직후 첫 공격, 공격 후 쿨다운, 타겟 사망·타워 파괴 시 처리, 배치 개수 제한 여부, 서버 권위 처리, 타워 클릭 시 팝업, 종족 한정 배치 시 초기 회전, 발사 연출 — 규칙 전체와 **종족별 타워가 하나의 `BuildingType.AutoTower` 를 공유한다는 것 및 종족별 이름**의 단일 소스는 `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 1~12 및 그 섹션 서두
- MistShrine 물안개 힐(`HealShrine`, 방어 타워와 별개 건물): 건물 중심 고정 원형 범위, 아군 유닛+건물 회복, 최대 체력인 대상은 회복되지 않음, 1초 discrete 틱 아우라, 물안개 지속 < 쿨다운, 물안개 간 중첩 금지(가까운 건물 우선·동률 시 Id 작은 쪽), 자연회복과는 중첩 적용, 자동/수동 모드(기본 OFF), 전용 UseCase·서버 권위 (규칙 8-1에 "활성 물안개는 서로 같은 위상으로 틱한다" 불변식 보강 — 되돌리면 중첩 해소가 죽는다)
  - 🔴 **회복량 · 물안개 지속시간 · 쿨다운 · 범위 반경 · 회복 텍스트 표시 주기는 전부 미확정**이며 밸런싱 단계에서 확정한다 — 위 요약을 확정된 수치 사양으로 읽지 말 것. 미확정 목록의 단일 소스는 `GameSystemRules_Buildings.md` 「MistShrine 물안개 힐 시스템」 규칙 16
  - 특수 동작 건물이 2종 이상이 되면 `GameSystemRules_SpecialBuildings.md`로 분리한다(해당 섹션 서두 명시)

### 스킬 건물 관련 작업
→ [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)
- 스킬 건물은 종족마다 하나이며 정령·초월이 enum `MagicBuilding` 을 공유 + 종족 키 분기 — 종족별 자산명·프리팹·enum 대응의 단일 소스는 `GameSystemRules_Skills.md` 「스킬 건물 3종 정의」 절의 표
- 자원 없음(쿨다운만), 건물별 글로벌 쿨다운, 업그레이드 없음 — 건물당 스킬 수 상한의 단일 소스는 `GameSystemRules_Skills.md` 규칙 4
- 종족별 스킬셋 분리, 데이터 주도 설계(ScriptableObject) — `GameSystemRules_Skills.md` 규칙 6·7
- 범용 `BuildingActionPanelUI` 3×3 그리드, 쿨다운 시계방향 오버레이 — 슬롯 배치의 단일 소스는 `GameSystemRules_Skills.md` 규칙 9
- 스킬 메커니즘 타입과 발동 경로 — 타입 목록과 경로 목록의 단일 소스는 `GameSystemRules_Skills.md` 규칙 11~13 · 15~16
- 모바일 지점 조준 UX(탭으로 조준 모드 진입 → 화면 드래그로 범위 이동 → 손 떼면 발동, 엣지 스크롤, X 취소·취소 버튼 확대 예고, 맵 clamp)
- 서버 권위(좌표만 RPC 전송 + 서버 재검증)
- 🔴 각 건물의 **구체 스킬 목록과 수치는 아직 정해지지 않았다** — 미확정 항목의 단일 소스는 `GameSystemRules_Skills.md` 「추후 데이터로 확정할 항목」 절

### 유닛 강화(연구소) 시스템
→ [GameSystemRules_Upgrade.md](GameSystemRules/GameSystemRules_Upgrade.md)
- 전투 스탯 ×10 스케일 — **무엇을 ×10 하고 무엇을 하지 않는지(불변 항목)** 의 단일 소스는 `GameSystemRules_Upgrade.md` 규칙 1. 적용은 config `.asset`에 커밋 반영됐다(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)
- 강화 스탯 3종(공/방/속) + 초월 자연회복, 종족별 그룹×스탯 트랙, 유닛→그룹 매핑
- (B) 팀 배율 실시간 소급 적용, 방어력 감쇄 공식 — 공식·상수·트랙 값과 적용/미적용 지점의 단일 소스는 `GameSystemRules_Upgrade.md` 규칙 5 이며, 전투 쪽 삽입 지점은 `GameSystemRules_Units.md` 규칙 44 가 함께 정한다
- 연구소 운영(복수 건설·연구 시간·진행 중 트랙 잠금·파괴 시 진행 중 연구 취소와 투입 골드 환불·진행 상태 비공개·서버 권위), 비용·시간·그룹 배율 — 환불 비율의 단일 소스는 `GameSystemRules_Upgrade.md` 규칙 8
- 연구 패널 UI(규칙 13): `ResearchPanelUI : BuildingPanelBase` + 매트릭스/진행 2-레이어(연구소 단위)
- 후속 보류: UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐·싱글 자연회복 실기

### AI 시스템 관련 작업
→ [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md)
- 난이도 파라미터 (AIConfig ScriptableObject)
- 빌드오더 스크립트 (Phase 분할 구조, Phase 내 순서 보장, Phase 스킵 금지, actionType 정의) — Phase 개수와 각 Phase 의 목표는 `GameSystemRules_AI.md` 규칙 6 이 단일 소스
- 반응 시스템 (R1 유닛열세, R2 골드과잉, R3 MiningPost 파괴 감지)
- 건물 배치 로직 (BFS 타일 선택, 후보가 거부돼도 탐색을 멈추지 않음, MiningPost 병행 트랙)
  - ⏳ **배치 후보 판정은 무작위 맵 3단계에 「일반 건설」 조건으로 전환 예정**이다 — 지금은 이동 판정 기준이며 **전환 전까지는 그것이 맞다.** 전환 예고의 단일 소스는 `GameSystemRules_AI.md` 규칙 26
  - ⚠️ **2026-09-03 — 「무작위 맵 구현 시」라고만 적혀 있던 시점을 「3단계」로 좁혔다.** 무작위 맵 1단계(타일 상태 계약 전환)가 끝나 「무작위 맵 구현 시」가 이미 시작된 것으로 읽히게 됐기 때문이다. **같은 문서의 광산 타일 조회는 1단계에서 이미 전환됐고, 이 배치 후보 판정만 남았다** — 두 항목을 함께 묶어 읽지 말 것
- 가드 메커니즘 (재시도, 골드 부족 시 생산 취소 시도, 타일 부족 시 대기, 행동 타입별 독립 쿨다운) — 취소 시도 횟수 한도의 단일 소스는 `GameSystemRules_AI.md` 규칙 22
- 아키텍처 및 구현 규칙 — `AIOpponentController` 와 config 자산의 레이어 귀속, `GameBootstrapper` 초기화와 Tick, AI On/Off 토글, 난이도 전달 정적 홀더와 씬 진입 시 AIConfig 선택, 로비 난이도 선택 화면 흐름과 구현 변경 범위, `goldIncomeMultiplier` 적용 방식 — `GameSystemRules_AI.md` 규칙 28~36

→ [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md)
- Human 종족 시나리오 A (물량형), B (테크형), C (균형형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md)
- Spirit 종족 시나리오 A (Spirit-Inferno 불 집중형), B (Spirit-Torrent 물 집중형), C (Spirit-Quake 땅 집중형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md)
- Transcendence 종족 시나리오 A (Trans-Rush 초반 물량형), B (Trans-Flora 동물A+식물 균형형), C (Trans-Beast 동물 고테크형) 빌드오더 테이블
