# Game System Rules — 인덱스

**문서 역할**
- **목적·질문:** 작업할 도메인의 규칙 원본을 찾는 인덱스다.
- **포함:** 규칙 파일·도메인별 핵심 찾기 요약과 기존 참조 안내.
- **제외·담당:** 상세 규범은 연결된 각 원본, 전역 문서 찾기는 [AGENTS](../../../AGENTS.md), 작성 절차는 [WORKFLOW](WORKFLOW.md).
- **원본·참조:** 규칙 찾기 요약 원본이며 각 규칙의 사본이 아니다. 상세 역할은 대상 문서 머리말을 확인한다.
- **갱신:** 규칙 파일의 책임·경로·참조 위치가 바뀔 때 안내를 갱신한다. 구현/검증 인계와 상세 수치를 추가하지 않는다.

Plan.md 작성 전 아래 목록에서 관련 규칙 원본을 열어 읽는다.

---

## 파일 목록

| 파일 | 포함 시스템 |
|------|------------|
| [GameSystemRules_Map.md](GameSystemRules/GameSystemRules_Map.md) | 대전 맵 전체 180도 대칭, 중앙/대응쌍 광산 공정성, 정적 최단 접근거리 검증 |
| [GameSystemRules_RandomMap.md](GameSystemRules/GameSystemRules_RandomMap.md) | FlatTop 11×21 무작위 대전 맵 유형별 생성·광산·건설 제한·seed·폴백·검증 (유형 목록과 개수는 3장 「유형별 사양」이 단일 소스) |
| [GameSystemRules_Network.md](GameSystemRules/GameSystemRules_Network.md) | 네트워크(Host–Client) 동작 규정 — 🔴 최상위 원칙(플레이어는 자신이 Host 인지 Client 인지 알 수 없다), 이탈 시 승패 기록, 인게임 이탈 처리 (구조·구현 방법은 이 문서가 아니라 `TechnicalDesignDocument_Network.md` 「🌐 네트워크 설계」) |
| [GameSystemRules_UI.md](GameSystemRules/GameSystemRules_UI.md) | 공통 UI·팝업·로딩 규칙 원본, 이동된 UI 규칙의 기존 참조 안내 |
| [GameSystemRules_MatchFlowUI.md](GameSystemRules/GameSystemRules_MatchFlowUI.md) | 경기 준비 실패·결과·재경기 화면, M/D 규칙 원본 |
| [GameSystemRules_BuildingPanelsUI.md](GameSystemRules/GameSystemRules_BuildingPanelsUI.md) | 생산·MistShrine·건물 배치 패널 규칙 원본 |
| [GameSystemRules_SettingsUI.md](GameSystemRules/GameSystemRules_SettingsUI.md) | 인게임 설정·로비 설정·프로필 규칙 원본 |
| [GameSystemRules_MatchFlow.md](GameSystemRules/GameSystemRules_MatchFlow.md) | 맵 전송·실패 복구·재경기 맵 선택·결과 화면/재로드 이탈 규칙 원본 |
| [GameSystemRules_Units.md](GameSystemRules/GameSystemRules_Units.md) | 유닛 이동(건물 경로 차단 포함)·정렬·타겟·공격·피해 불변 규칙, 전투 연출·애니메이션 상태 동기화, 특수 공격 시스템, 방어력 데미지 감쇄 (특수 공격 확장의 목록과 개수는 「특수 공격 시스템 규칙」 절이 단일 소스) |
| [GameSystemRules_UnitCombatSynchronization.md](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) | 서버 권위 행동 회차, 시간, 방향, 타격 결과 및 멀티플레이 표현 동기화 계약 |
| [UnitCombatAssetMatrix.md](Assets/UnitCombatAssetMatrix.md) | 25종 유닛 공격 프로필·클립·이벤트·VFX·구현 및 검증 상태 감사표 |
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

<a id="사운드-관련-작업"></a>
<a id="시스템별-빠른-참조"></a>
<a id="경기-운영-관련-작업"></a>
<a id="ui-원본-찾기"></a>
<a id="맵-관련-작업"></a>
<a id="네트워크-동작-관련-작업"></a>
<a id="ui-관련-작업"></a>
<a id="유닛-관련-작업"></a>
<a id="건물-관련-작업"></a>
<a id="스킬-건물-관련-작업"></a>
<a id="유닛-강화연구소-시스템"></a>
<a id="ai-시스템-관련-작업"></a>

## 작업별 원본 찾기

위 파일 표에서 담당 규칙을 선택한다. 아래는 여러 문서를 함께 확인해야 하는 작업의 진입점이며 상세 규범의 사본을 두지 않는다.

| 작업 | 먼저 읽을 원본 · 함께 읽을 문서 |
|---|---|
| 맵 생성·대칭·광산 공정성 | [맵](GameSystemRules/GameSystemRules_Map.md) · [무작위 맵](GameSystemRules/GameSystemRules_RandomMap.md) · [네트워크 TDD](TechnicalDesignDocument_Network.md) |
| 접속·이탈·승패의 역할 일관성 | [네트워크 최상위 원칙·동작](GameSystemRules/GameSystemRules_Network.md) · [경기 운영](GameSystemRules/GameSystemRules_MatchFlow.md) |
| 경기 준비 실패·재경기·결과 화면 | [경기 운영](GameSystemRules/GameSystemRules_MatchFlow.md) · [경기 UI의 M/D 규칙](GameSystemRules/GameSystemRules_MatchFlowUI.md) |
| 공통 UI·팝업·로딩 | [공통 UI](GameSystemRules/GameSystemRules_UI.md) · [Canvas 정렬](GameSystemRules/GameSystemRules_CanvasSortingOrder.md) · [UI 가이드](UIGuidelines.md) |
| 생산·물안개·건물 배치·설정 화면 | [건물 패널](GameSystemRules/GameSystemRules_BuildingPanelsUI.md) · [설정·프로필](GameSystemRules/GameSystemRules_SettingsUI.md) |
| 이동·공격·피해·특수 효과 | [유닛 규칙](GameSystemRules/GameSystemRules_Units.md) · [권위·시간·표현 동기화](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) |
| 유닛별 실제 연결·수용·현재 수치 | [전투 감사표](Assets/UnitCombatAssetMatrix.md) · [VFX/SFX 목록](Assets/VFXSFXList.md) · [스탯](StatsReference.md) |
| 건물·스킬·연구 | [건물](GameSystemRules/GameSystemRules_Buildings.md) · [스킬](GameSystemRules/GameSystemRules_Skills.md) · [강화](GameSystemRules/GameSystemRules_Upgrade.md) |
| BGM·SFX·볼륨 | [사운드](GameSystemRules/GameSystemRules_Sound.md) · [효과 제작 가이드](Assets/VFXSFXGuide.md) |
| AI 동작·시나리오 | [AI 공통 규칙과 종족별 시나리오 연결](GameSystemRules/GameSystemRules_AI.md) · [보류/기획 잔여](ROADMAP.md) |

규칙의 변경은 해당 원본, 시스템 상태는 [PROJECT_STATUS](PROJECT_STATUS.md), 실제 미완·보류는 [ROADMAP](ROADMAP.md)에서 확인한다. 종전 상세 요약·변경 경위는 [이력](History/ImplementationVerificationHistory.md#role-maintenance-20261016-r2-index-details)에 보존한다.
