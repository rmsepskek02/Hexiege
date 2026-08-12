# Game System Rules — 인덱스

구현 시 따라야 하는 게임 시스템별 규칙 모음.
아이디어나 기획 의도가 아닌, 실제 코드로 구현할 때 기준이 되는 구체적인 규칙을 기록한다.

세부 규칙은 아래 파일에 있다. Plan.md 작성 전 관련 파일을 반드시 읽는다.

---

## 파일 목록

| 파일 | 포함 시스템 |
|------|------------|
| [GameSystemRules_Map.md](GameSystemRules/GameSystemRules_Map.md) | 대전 맵 전체 180도 대칭, 중앙/대응쌍 광산 공정성, 정적 최단 접근거리 검증 |
| [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) | FlatTop 11×21 무작위 대전 맵 5종 생성·광산·건설 제한·seed·폴백·검증 |
| [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) | 공통 UI 규칙, 생산 패널 UI, 건물 배치 패널 UI, 인게임 설정 메뉴, 로비 설정/프로필 UI |
| [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md) | 유닛 이동 시스템, 전투 진입, 전투 연계, 방어력 데미지 감쇄(구현 완료) |
| [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 랠리포인트 시스템, 건물 철거 시스템, 방어 타워 시스템, MistShrine 물안개 힐 시스템 (구현 완료 / 싱글 실기 검증 완료 · 멀티 미검증) |
| [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md) | 스킬 건물 3종, 쿨다운/스킬 수 공통 규칙, 3×3 스킬 UI, 스킬 타입 3종, 모바일 지점 조준 UX, 서버 권위 (기획 확정/미구현) |
| [GameSystemRules_Upgrade.md](GameSystemRules/GameSystemRules_Upgrade.md) | 연구소 기반 유닛 강화(공/방/속 + 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI (구현 완료 / 멀티 실기 PASS) |
| [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md) | Canvas SortingOrder 구조, 씬별 Canvas 계층, 전역 UI z-order |
| [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md) | BGM 전환 규칙, SFX 정책, 볼륨 제어, AudioManager 아키텍처 |
| [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md) | AI 난이도 시스템, 빌드오더 스크립트, 반응 시스템, 건물 배치 로직, 가드 메커니즘 |
| [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md) | Human 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md) | Spirit 종족 AI 빌드오더 시나리오 A/B/C |
| [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md) | Transcendence 종족 AI 빌드오더 시나리오 A/B/C |

---

### 사운드 관련 작업
→ [GameSystemRules_Sound.md](GameSystemRules/GameSystemRules_Sound.md)
- AudioManager 레이어 및 DontDestroyOnLoad 규칙
- BGM 전환 시점 (Login/Lobby/Battle/Victory/Defeat)
- BGM 크로스페이드 방식
- SFX 2D 고정, 동시 재생 한도 8개
- VFX+SFX 쌍 호출 규칙
- 볼륨 채널 (Master/BGM/SFX), PlayerPrefs 저장
- 볼륨 컨트롤 버튼 (전체 소리켜기/음소거/초기화/뒤로), 슬라이더 색상

---

## 시스템별 빠른 참조

### 맵 관련 작업
→ [GameSystemRules_Map.md](GameSystemRules/GameSystemRules_Map.md)
- 모든 맵 생성 요소와 장식의 정확한 180도 대칭
- 중앙 단독 광산 직접 대칭 / 180도 대응 광산 쌍의 교차 거리·접근성 대칭
- 팀별 시작 광산 개수·거리·초기 채굴소 상태·경제 효과 대칭
- 정적 장애물과 초기 건물을 포함한 성 인접 영역→광산 인접 영역 최단 접근거리 대칭 및 도달 가능성
- 새 맵 또는 성·광산·정적 장애물 배치 변경 시 재검증

→ [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md)
- FlatTop 11×21, 다섯 맵 유형 동일 확률
- 유형별 지형·통로·중립 광산 수·건설 불가 구역
- 광산 수별 초기 골드, 시작 공간 10타일
- 결정적 seed, 최대 100회 재시도, 동일 조건 폴백과 생성 로그

### UI 관련 작업
→ [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md)
- Canvas SortingOrder 구조는 [GameSystemRules_CanvasSortingOrder.md](GameSystemRules/GameSystemRules_CanvasSortingOrder.md)를 함께 확인
- Canvas Scaler, 앵커 기반 배치, Safe Area, CanvasGroup 숨김/표시
- 폰트, 골드 부족 표시, 팝업/모달 타입 구분
- 생산 패널: 큐 구조, 골드 차감 시점, 자동 생산, 토스트 메시지
- 건물 배치 패널: 비용 표시, 실패 피드백
- 인게임 설정 메뉴: 일시정지, 포기 처리, 프로필 서브 패널
- 로비 설정/프로필 UI: ProfilePanel/SettingPanel 탭 분리

### 유닛 관련 작업
→ [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md)
- A* 이동, 공유 타일 상태, 경로 재계산
- 상태 머신 (A* 이동 / 전투 이동 / 공격)
- 감지/공격 사거리, 타겟 선택, AoE
- 전투 연출 동기화 (타격 프레임 단일 소스, 피격 표현 큐, 원거리 트레이서, 애니메이션 상태 값 기반 동기화)
- 특수 공격 시스템 (전략 핸들러 구조, 휩쓸기 월드 부채꼴 판정, SpecialAttackConfig 튜닝, AoE 연출 동시 방출)

### 건물 관련 작업
→ [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md)
- 랠리포인트 표시/숨김
- 철거 처리, 골드 환불, 연쇄 처리
- 방어 타워: 타겟 선택, 쿨다운, 서버 권위 처리 (종족별 = `AutoTower` — Human CannonTower / Spirit RuneSpire / **Trans VineTower**)
- MistShrine 물안개 힐(`HealShrine`, 방어 타워와 별개 건물): 건물 중심 고정 원형 범위, 아군 유닛+건물 회복, 1초 discrete 틱 아우라, 물안개 지속 < 쿨다운, 물안개 간 중첩 금지(가까운 건물 우선·동률 시 Id 작은 쪽), 자연회복과는 중첩 적용, 자동/수동 모드(기본 OFF), 전용 UseCase·서버 권위 (**2026-08-12 구현 완료 / 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증 · VFX·아이콘 미제작 · 밸런싱 미확정**. 규칙 8-1에 "활성 물안개는 서로 같은 위상으로 틱한다" 불변식 보강 — 되돌리면 중첩 해소가 죽는다)
  - 특수 동작 건물이 2종 이상이 되면 `GameSystemRules_SpecialBuildings.md`로 분리한다(해당 섹션 서두 명시)

### 스킬 건물 관련 작업
→ [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)
- 스킬 건물 3종(FlightFacility / MagicSpirit / WillowShrine), enum `MagicBuilding` 공유 + 종족 키 분기
- 자원 없음(쿨다운만), 건물별 글로벌 쿨다운, 건물당 최대 5개, 업그레이드 없음
- 범용 `BuildingActionPanelUI` 3×3 그리드(스킬 1~5 / 철거 6 / 예약 7~9), 쿨다운 시계방향 오버레이
- 스킬 타입 3종(즉발 범위 피해 / 장판 DoT / 전역 상태변경), 발동 경로 2종
- 모바일 지점 조준 UX(탭으로 조준 모드 진입 → 화면 드래그로 범위 이동 → 손 떼면 발동, 엣지 스크롤, X 취소·취소 버튼 확대 예고, 맵 clamp) — 설계 정정: hold-drag → 탭 기반(코드 미반영, 후속 작업)
- 서버 권위(좌표만 RPC 전송 + 서버 재검증), 기획 확정 / 미구현

### 유닛 강화(연구소) 시스템 (구현 완료 / 멀티 실기 PASS)
→ [GameSystemRules_Upgrade.md](GameSystemRules/GameSystemRules_Upgrade.md)
- 전투 스탯 ×10 스케일(HP·공격력·건물·타워·DoT·힐), 불변 항목(사거리·이동속도·쿨다운·비용·비율). ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)
- 강화 스탯 3종(공/방/속) + 초월 자연회복, 종족별 그룹×스탯 트랙, 유닛→그룹 매핑
- (B) 팀 배율 실시간 소급 적용, 방어력 감쇄 공식(K=120, floor 1, 직격·스플래시·타워→유닛 일괄, DoT 미적용)
- 연구소 운영(복수 건설·연구 시간·진행 중 트랙 잠금·파괴 시 100% 환불·서버 권위), 비용·시간·그룹 배율
- 연구 패널 UI(규칙 13): `ResearchPanelUI : BuildingPanelBase` + 매트릭스/진행 2-레이어(연구소 단위)
- 후속 보류: UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐(**2026-08-12 구현 완료 / 멀티 미검증**)·싱글 자연회복 실기

### AI 시스템 관련 작업
→ [GameSystemRules_AI.md](GameSystemRules/GameSystemRules_AI.md)
- 난이도 파라미터 (AIConfig ScriptableObject)
- 빌드오더 스크립트 (Phase 1~4 구조, actionType 정의)
- 반응 시스템 (R1 유닛열세, R2 골드과잉, R3 MiningPost 파괴 감지)
- 건물 배치 로직 (BFS 타일 선택, MiningPost 병행 트랙)
- 가드 메커니즘 (재시도, 생산 취소 1회 한도)

→ [GameSystemRules_AI_Scenario_Human.md](GameSystemRules/GameSystemRules_AI_Scenario_Human.md)
- Human 종족 시나리오 A (물량형), B (테크형), C (균형형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Spirit.md](GameSystemRules/GameSystemRules_AI_Scenario_Spirit.md)
- Spirit 종족 시나리오 A (Spirit-Inferno 불 집중형), B (Spirit-Torrent 물 집중형), C (Spirit-Quake 땅 집중형) 빌드오더 테이블

→ [GameSystemRules_AI_Scenario_Transcendence.md](GameSystemRules/GameSystemRules_AI_Scenario_Transcendence.md)
- Transcendence 종족 시나리오 A (Trans-Rush 초반 물량형), B (Trans-Flora 동물A+식물 균형형), C (Trans-Beast 동물 고테크형) 빌드오더 테이블
