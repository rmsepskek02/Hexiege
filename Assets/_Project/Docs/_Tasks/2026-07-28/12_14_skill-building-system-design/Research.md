# Research — 스킬 건물 시스템 (기획 확정 + 설계 문서화)

## 이 작업이 무엇인가 (자연어 서두)

Hexiege의 세 종족(Human / Spirit / Transcendence)에는 각각 **스킬 건물**이 하나씩 있습니다. 스킬 건물은 유닛을 뽑거나 자원을 캐는 대신, 플레이어가 직접 버튼을 눌러 전장에 즉각적인 효과(피해·상태 변화 등)를 발생시키는 특수 건물입니다.

그런데 지금까지 이 세 건물은 **화면에 놓이고 얻어맞기만 하는 껍데기**였습니다. 클릭해도 철거 버튼만 있는 범용 패널이 뜰 뿐, 실제로 무슨 스킬을 어떻게 쓰는지에 대한 기획도 코드도 전혀 없었습니다.

이번 사이클의 목적은 **"스킬 건물이 앞으로 어떻게 동작해야 하는가"를 프레임워크 레벨에서 확정하고, 그것을 정식 문서로 남기는 것**입니다. 즉 스킬을 어떤 자원으로 쓰는지(마나 없이 쿨다운만), 몇 개까지 가질 수 있는지(최대 5개), 어떤 UI로 노출되는지, 모바일 터치로 어떻게 조준하는지, 멀티플레이에서 누가 판정하는지 같은 "규칙의 틀"을 정하는 작업입니다.

**중요:** 이번 사이클은 **기획 확정 + 설계 문서화**까지이며, **실제 코드/프리팹 구현은 착수하지 않았습니다(향후 별도 task).** 구체적인 스킬 목록이나 수치(어떤 스킬이 몇 초 쿨다운에 얼마의 피해를 주는지)도 아직 정하지 않았고, 나중에 데이터로 채워 넣을 수 있도록 "그릇"만 규정합니다.

---

## 현재 상태 파악

### 1. 스킬 건물 3종 매핑

| 종족 | 자산명(스킬 건물) | 프리팹 | BuildingType enum |
|------|------------------|--------|-------------------|
| Human | FlightFacility | `Building_FlightFacility_Blue` / `Building_FlightFacility_Red` | `FlightFacility` (= 3) |
| Spirit | MagicSpirit | `Building_MagicSpirit_Blue` / `Building_MagicSpirit_Red` | `MagicBuilding` (= 5) |
| Transcendence | WillowShrine | `Building_WillowShrine_Blue` / `Building_WillowShrine_Red` | `MagicBuilding` (= 5, Spirit과 공유) |

- Blue/Red 프리팹·FBX·텍스처는 이미 완비되어 있다(배치·피격에 필요한 시각 자산 준비 완료).
- `MagicSpirit`(정령)과 `WillowShrine`(초월)은 **동일한 enum 멤버 `MagicBuilding`(= 5)을 공유**한다. 근거: `Assets/_Project/Scripts/Domain/Building/BuildingType.cs` — enum 멤버 순서·값 변경 시 직렬화(ScriptableObject/Scene) 및 RPC(`NetworkBuildingController`가 `(int)` 캐스트로 전송) 정합성이 깨진다는 주석 존재.

### 2. 기존 구현 상태

- 세 건물 모두 **배치·피격만 되는 시각 오브젝트**다. 스킬 발동 로직·데이터·전용 UI는 전무하다.
- 클릭 시 범용 건물 행동 패널(`BuildingActionPanelUI`)을 공유한다. 이 패널은 **3×3 그리드(9칸)** 구조이며, 현재는 **철거 버튼만 활성**이고 나머지 칸은 alpha=0(비활성)이다.
- 즉, "스킬 건물"이라는 개념만 존재하고 스킬 기능 자체가 비어 있는 상태다.

### 3. 실제 건설 비용

- 권위 소스는 런타임 실제값인 `Assets/_Project/Resources/Config/BuildingStatsConfig.asset`(Inspector 값 우선)이다.
- 확인 결과 **세 스킬 건물 모두 200 골드**:
  - Human `FlightFacility`(buildingType 3): humanGoldCost = 200
  - Spirit `MagicSpirit`(buildingType 5): spiritGoldCost = 200
  - Trans `WillowShrine`(buildingType 5): transcendenceGoldCost = 200
- `StatsReference.md`의 건설 비용 표기(200)와 일치한다. (과거 GDD의 "150 골드"는 옛 값 — 정정 대상)

### 4. 입력 시스템 재사용 기반 (모바일 지점 조준)

- 스킬의 "지점 지정" 조준은 신규 입력 패턴이지만, 기존 **"랠리포인트 설정 모드"** 아키텍처를 재사용/확장할 수 있다.
- 참고 지점: `InputHandler.HandleClick` 최상단의 조준 모드 플래그 + 다음 탭 처리 흐름 — "특정 모드에 진입한 뒤 다음 지도 입력을 특수 처리"하는 패턴이 스킬 조준과 동일 구조다.

---

## 발견한 충돌/불일치 (이번 사이클에서 모두 해소 완료)

기존 문서들이 확정 설계와 어긋나 있어 아래 5건 + 용어/비용 정정을 수행했다. (상세 반영 내역은 Plan.md "문서화 접근" 참조)

| # | 충돌 내용 | 해소 방식 |
|---|----------|----------|
| 1 | `GameDesignDocument.md` §3 "마법 건물(Magic Tower)" 구기술(번개/지역회복/버프·디버프, 150골드, 업그레이드, "쿨다운 관리")이 확정 3타입 설계와 불일치 | §3을 확정 설계 요약으로 교체 + 상세는 `GameSystemRules_Skills.md` 참조로 연결 |
| 2 | `MagicBuilding` enum을 Spirit·Transcendence가 공유 → 스킬셋 분기 방침 부재 | 종족 키 분기 방침을 `GameSystemRules_Skills.md` 규칙 1에 명문화(enum 미변경, 코드 미수정) |
| 3 | `ROADMAP.md` D-1 "마법 타워: 범위 공격, 마나 자원 추가 필요 가능성" → 마나 없음 결정과 충돌 | 마나 미도입 + 스킬 시스템 새 문서 참조로 갱신 |
| 4 | 방어 타워를 "MagicTower"로 표기(GDD 213·431, TDD 1304) → 스킬 건물 `MagicSpirit`과 혼동 | 방어 타워는 `RuneSpire`로 교정 + `GameSystemRules_Skills.md` 명칭 주의 블록 명시 |
| 5 | 범용 `BuildingActionPanelUI` 3×3 슬롯 배치 미문서화 | 슬롯 1~5 스킬 / 6 철거(고정) / 7~9 예약을 새 문서 규칙 9 + Buildings.md 철거 규칙 2 참조로 명시 |

**추가 정정(사용자 확정 후 2차 수행):**
- 용어: 스킬 건물을 가리키는 구용어 "마법 건물" → **"스킬 건물"**로 표준화(`AssetList.md`, `CommonAssetGuide.md`, `StatsReference.md`, `GameDesignDocument.md`). enum `MagicBuilding` 코드 식별자는 유지, 방어 타워·FlightFacility(지원 건물) 라벨은 대상 아님.
- 비용: **150 → 200 골드**로 정정(`BuildingStatsConfig.asset` 실제값 기준). GDD §3, `GameSystemRules_Skills.md`에서 "미확정" 표기 제거.

---

## 이번 작업 범위 밖 (향후 별도 task)

- 스킬 발동 로직, 타입별 실행기(즉발 AoE / 장판 DoT / 전역 상태변경) 코드 구현
- 지점 조준 입력 모드·엣지 스크롤·조준점 clamp 구현
- 서버 권위 스킬 RPC 신설
- 종족별 스킬 로드아웃 데이터(ScriptableObject) 및 구체 스킬·수치 확정
- 전용/확장 UI(쿨다운 오버레이, X 취소 버튼 에셋) 구현

이번 사이클은 위 구현의 **기준 문서를 확정**하는 데까지다. 구현은 미착수.
