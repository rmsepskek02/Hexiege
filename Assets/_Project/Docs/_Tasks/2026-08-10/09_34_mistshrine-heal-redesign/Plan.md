# Plan — MistShrine(물안개 신전) 힐 건물 재설계

**작성일:** 2026-08-10
**작업 폴더:** `Assets/_Project/Docs/_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`
**선행 문서:** [Research.md](Research.md)

---

## 이 계획이 무엇이고 왜 필요한가 (자연어 설명 — 기술 용어 없이)

초월 종족의 **MistShrine(물안개 신전)** 은 회복해 주는 건물로 만들어졌지만 지금까지 아무 회복도 하지 않습니다.
이번에 이 건물이 앞으로 **어떻게 동작할지를 확정**했고, 그 내용을 프로젝트 문서에 적어 두는 것이 이 계획의 목적입니다.

확정한 동작을 쉬운 말로 옮기면 이렇습니다.

- 플레이어가 이 건물을 사용하면 **건물 주위에 물안개가 깔립니다.**
- 물안개가 깔려 있는 동안, **그 안에 들어 있는 내 편 부대와 내 편 건물이 1초마다 체력을 회복**합니다.
  내 건물이니 **이 건물 자신과 본진도 회복 대상**입니다.
- 물안개 밖으로 나가면 **바로 회복이 끊깁니다.** 다시 들어오면 다시 회복됩니다.
- 물안개는 **정해진 시간이 지나면 걷히고**, 그 뒤 조금 더 기다려야 다시 쓸 수 있습니다(쉴 틈이 있는 구조).
- 쓰는 데 **골드는 들지 않습니다.** 대신 다시 쓰기까지 기다려야 합니다.
- **건물이 부서지면 물안개도 그 즉시 사라집니다.**
- 물안개가 여러 개 겹쳐도 **회복은 한 번만** 받습니다(제일 가까운 건물 것으로).
  다만 연구소에서 배우는 **초월 자연회복은 다른 효과라서 물안개와 같이 적용**됩니다.
- 조작은 **생산 건물과 똑같이** 만듭니다. **짧게 누르면 한 번 사용**, **길게 누르면 자동 모드 켜기/끄기**입니다.
  자동 모드는 **처음에는 꺼져 있습니다.**

이 계획서는 위 내용을 **문서에만 반영**합니다. **코드는 한 줄도 고치지 않습니다.**
실제로 게임이 이렇게 동작하게 만드는 작업은 이 문서를 근거로 **다음 작업에서 따로** 진행합니다.
이 계획서 뒷부분에는, 다음 작업에서 무엇을 새로 만들어야 하는지도 미리 정리해 두었습니다.

---

## ⚠️ 기존 로직 제거 여부 (WORKFLOW.md [4] 최상단 기술 규칙)

**이번 작업에서 제거하거나 비활성화하는 기존 로직은 없다.**

- 이번 작업은 **문서 수정 전용**이며 코드·프리팹·씬·에셋을 일절 변경하지 않는다.
- 문서에서 삭제되는 서술은 **사실과 다른 오류 문장뿐**이다
  (GDD·TDD의 "Transcendence 방어 타워 = MistShrine"). 이는 로직 제거가 아니라 **오기 정정**이며,
  근거는 `BuildingType.cs`(`AutoTower = 2` / `HealShrine = 6` 별개 enum) · `AssetList.md` · `StatsReference.md` ·
  `GameSystemRules_Buildings.md` 방어 타워 규칙 11(Transcendence = VineTower)이다 → Research.md §1 참조.
- 정정 시 원래 문장을 그냥 지우지 않고 **"2026-08-10 정정" 주석을 함께 남겨** 혼동 재발을 막는다.

---

## 1. 접근 방식

### 1-1. 문서 배치 결정 (사용자 확정)

**독립 규칙 파일(`GameSystemRules_MistShrine.md`)을 만들지 않고 `GameSystemRules_Buildings.md`에 섹션으로 추가한다.**

근거:
- `GameSystemRules_Buildings.md`의 "방어 타워 시스템"(규칙 1~12)이 이미
  *비생산 단일 건물의 자동 동작 · 대상 선정 · 쿨다운 · 서버 권위 · 파괴 시 중단 · 클릭 팝업* 규칙 세트를 담고 있어 MistShrine과 성격이 거의 동일하다.
- 독립 파일을 받은 `GameSystemRules_Skills.md`(23KB) · `GameSystemRules_Upgrade.md`(22KB)는 건물 하나가 아니라 **프레임워크 규모**였다.
- `GameSystemRules_Buildings.md`는 현재 6KB로 여유가 충분하다.

**분기(분리) 시점을 섹션 서두에 함께 못박는다:**
> 특수 동작 건물이 **2종 이상**이 되면 `GameSystemRules_SpecialBuildings.md`로 분리한다.

이 문구를 MistShrine 섹션 맨 앞에 두어, 다음 특수 건물 작업 시 자동으로 분리 판단이 걸리도록 한다.

### 1-2. 역할 분리 (중복 기술 금지)

| 문서 | 담당 |
|------|------|
| `GameSystemRules_Buildings.md` — MistShrine 물안개 힐 시스템 | **단일 소스.** 건물 동작·대상·범위·중첩·로직·네트워크·에셋 계약 |
| `GameSystemRules_UI.md` — MistShrine 패널 UI | **단일 소스.** 패널 구조·조작·쿨다운 표시·범위 표시·회복 텍스트 |
| `GameDesignDocument.md` | 기획 관점 자연어 요약 + 규칙 문서로 링크 |
| `StatsReference.md` | 수치(현재는 **미확정** 표기) |
| `GameSystemRules_Upgrade.md` | 자연회복과의 관계(별개 효과·중첩)만 명시하고 규칙은 Buildings.md 참조로 연결 |

---

## 2. 문서별 변경 내용 및 근거 규칙

> WORKFLOW.md [4] 필수 요건: **각 수정 항목이 GameSystemRules의 어느 규칙에 근거하는지 명시**한다.
> 이번 작업은 **규칙을 신설하는 작업**이므로, 신설 항목은 "신설 규칙 번호"를, 기존 규칙과 정합을 맞추는 항목은 "근거 규칙"을 적는다.

### 2-1. `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Buildings.md` [수정]

| 변경 | 근거 |
|------|------|
| 파일 서두에 "섹션마다 규칙 번호가 1부터 다시 시작 — 인용 시 섹션명 병기" 규약 명시 | 기존 파일 구조(랠리포인트 1~2 / 철거 1~6 / 방어 타워 1~12가 각각 독립 번호). Research.md §5-1 부가 이슈 |
| 목차에 "MistShrine 물안개 힐 시스템" 추가 | 기존 목차 형식 유지 |
| **방어 타워 시스템** 서두에 "MistShrine은 방어 타워가 아니다(Trans 방어 타워 = VineTower)" 교차 참조 추가 | 방어 타워 규칙 11이 이미 "Transcendence(VineTower)"로 적고 있음 — 혼동 재발 방지 |
| **MistShrine 물안개 힐 시스템 섹션 신설 (규칙 1~27)** | 신설. 아래 표 참조 |

**신설 규칙 구성:**

| 규칙 | 내용 | 관련 기존 규칙 |
|:-:|------|------|
| 1 | 방어 타워가 아님 — `BuildingType.HealShrine`(=6) 별도 분류. 방어 타워 규칙 1~12 미적용 | 방어 타워 규칙 1~12, `BuildingType.cs` |
| 2 | 스킬 건물도 아님 — 스킬 규칙·발동 경로 미적용 | `GameSystemRules_Skills.md` 규칙 1 |
| 3 | 범위 = 건물 중심 고정 원형, 조준 없음, 월드 좌표 거리 판정 | 방어 타워 규칙 3, `GameSystemRules_Units.md` 규칙 6 |
| 4 | 대상 = 아군 유닛 + 아군 건물(자기 자신·Castle 포함) | 신설 |
| 5 | 최대 체력 대상은 회복 없음 | `GameSystemRules_Upgrade.md` 규칙 7(Heal MaxHp 클램프) |
| 6 | 물안개 생명주기(시전→생성→지속→소멸→쿨다운 잔여→재사용) | 신설 |
| 7 | 물안개 지속시간 < 쿨다운 (다운타임 존재) | 신설 |
| 8 | 1초 단위 discrete 틱 회복 | `GameSystemRules_Units.md` 규칙 40 재사용 |
| 9 | 아우라 방식 — 범위 이탈 시 즉시 끊김(스냅샷 아님) | 신설 |
| 10 | 물안개는 이동하지 않음 | 대비: `GameSystemRules_Units.md` 규칙 28~31(TorrentSpirit 파도는 전진) |
| 11 | 시전 비용 없음(쿨다운으로만 제어) | `GameSystemRules_Skills.md` 규칙 3(자원 없음·쿨다운만)과 동일 방침 |
| 12 | 시전 건물 파괴 시 물안개 즉시 제거 | 방어 타워 규칙 7, 건물 철거 규칙 6 |
| 13 | 물안개 간 중첩 금지 — 가까운 건물 우선, **거리 동률이면 건물 Id 작은 쪽**(결정적) | 신설 (서버·클라 판정 분기 방지) |
| 14 | 연구소 자연회복과는 별개 효과 · 중첩 적용 + 독립 채널 구현 요구 | `GameSystemRules_Upgrade.md` 규칙 7(Heal 버킷 분리 선례) |
| 15 | 실제 회복 대상만 텍스트 표시 + 표시 주기 분리(임시 3초) | `GameSystemRules_Units.md` 규칙 36(BloomFairy HoT 완료 시 1회 표시) |
| 16 | 밸런싱 미확정 항목 표 | 신설 |
| 17 | UI 규칙 단일 소스는 `GameSystemRules_UI.md` | 역할 분리 |
| 18 | 자동/수동 모드(탭·롱프레스·자동 중 탭=해제·기본 OFF) | `GameSystemRules_UI.md` 생산 패널 규칙 3·4·5 |
| 19 | 자동 상태는 단순 bool 1개 (AutoTypes 파생 방식 미사용) | `ProductionState.IsAutoMode => AutoTypes.Count > 0` 대비 |
| 20 | 전용 UseCase 신설 — `SkillActivationUseCase` 재사용 금지 | Research.md §3-3 (`IsSkillBuilding` 게이트) |
| 21 | 쿨다운 관리 패턴 차용(Dictionary + 총 쿨다운 + 서버 틱 + **클라 로컬 미러**) | `GameSystemRules_Skills.md` 규칙 3·25·26 |
| 22 | 서버 권위 + 자동 토글 3단 동기화(`Request → ServerRpc(팀 검증) → ClientRpc`) | 방어 타워 규칙 9, 생산 시스템 구조 |
| 23 | 아군 유닛·건물 수집 헬퍼 신규 작성 | Research.md §3-4 (기존 헬퍼 전부 적 전용) |
| 24 | 건물 회복 경로 신규 마련 | Research.md §3-5 (`BuildingData`에 회복 메서드 없음) |
| 25 | 파괴·철거 시 물안개/자동모드/쿨다운 정리 경로 | `GameSystemRules_Upgrade.md` 규칙 8·9(연구소 파괴 시 취소 패턴) |
| 26 | 물안개 VFX 신규 제작 필요 + VFX+SFX 쌍 준수 | `GameSystemRules_Sound.md` 규칙 15, `VFXSFXList.md` |
| 27 | 범위 표시 UI는 `SkillAimOverlay.shader` 재사용 우선 검토, **ZTest Always 금지** | `GameSystemRules_Skills.md` 규칙 22-1 |

### 2-2. `Assets/_Project/Docs/GameSystemRules.md` [수정]

| 변경 | 근거 |
|------|------|
| 파일 목록 표 Buildings 행에 "MistShrine 물안개 힐 시스템 (기획 확정/미구현)" 추가 | 인덱스 정합 (Skills·Upgrade 행이 상태를 함께 표기하는 기존 형식 준수) |
| "건물 관련 작업" 빠른 참조에 MistShrine 항목 + 방어 타워 종족 매핑(Trans = VineTower) 추가, 분기 시점 병기 | Buildings 신설 규칙 1·13·14·18·20 요약 |

### 2-3. `Assets/_Project/Docs/GameSystemRules/GameSystemRules_UI.md` [수정]

| 변경 | 근거 |
|------|------|
| 문서 서두·목차에 "MistShrine 패널 UI" 추가 | 기존 목차 형식 유지 |
| **MistShrine 패널 UI 섹션 신설 (규칙 1~9)** | 신설 |

| 규칙 | 내용 | 근거 |
|:-:|------|------|
| 1 | `BuildingPanelBase` 상속 전용 패널 신설 | `ResearchPanelUI : BuildingPanelBase` 선례(`GameSystemRules_Upgrade.md` 규칙 13) |
| 2 | `BuildingSkillPanelUI` 재사용 금지(5슬롯·조준 전제) | `GameSystemRules_Skills.md` 규칙 8·9 |
| 3 | 패널 구성(사용 버튼 / 쿨다운 / 자동 표시 / 철거+환불은 베이스 제공) | 건물 철거 규칙 2·4 |
| 4 | 닫기 = 배경 탭 + 건물 파괴 시 자동 닫힘 | 공통 UI 규칙 8·9·11 |
| 5 | 탭 = 수동 시전 / 롱프레스(0.5초) = 자동 토글 / 자동 중 탭 = 해제 | 생산 패널 규칙 3·4·5 |
| 6 | 자동 모드 기본 OFF + 자동 상태 시각 표시(생산 패널 테두리 회전 패턴 재사용) | 생산 패널 자동 생산 규칙 18~22 |
| 7 | 쿨다운 오버레이 = `SkillCooldownOverlay` 재사용(`total` 필요) | `GameSystemRules_Skills.md` 규칙 10 |
| 8 | 범위 표시 = 아군만 · 패널 열린 동안만 · 지면 데칼 재사용 | `GameSystemRules_Skills.md` 규칙 22-1, 랠리포인트 규칙 1(상대 정보 비공개 원칙) |
| 9 | 회복 텍스트 = 실제 회복 대상만 · 표시 주기 분리(임시 3초) | `GameSystemRules_Units.md` 규칙 36 |

### 2-4. `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Upgrade.md` [수정]

| 변경 | 근거 |
|------|------|
| 후속 보류 목록의 "MistShrine 힐 — 미구현(보류)"을 **"재설계 확정(2026-08-10) / 구현 미착수"** 로 갱신하고 규칙은 Buildings.md 참조로 연결 | 상태 정확 표기(과대 표기 금지) |
| **자연회복(규칙 7)과 물안개 힐은 별개 효과이며 중첩 적용**됨을 명시 + 독립 채널 구현 요구 | 규칙 7(Heal 버킷 분리 — 같은 버킷이면 서로 덮어씀) |
| 규칙 1의 "MistShrine 1→10/s" 표기에 **밸런싱 재검토 대상** 주석 추가 | 규칙 1(×10 원리는 유효, 구체값만 재확정) + Buildings MistShrine 규칙 16 |
| 상단 요약문·참고 문서 절에 MistShrine 링크 및 상태 반영, `Buildings.md 규칙 9` 인용에 섹션명 보강 | 2-1의 인용 규약 |

### 2-5. `Assets/_Project/Docs/GameDesignDocument.md` [수정]

| 변경 | 근거 |
|------|------|
| §4 방어 타워: Transcendence를 **VineTower**로 정정 + 정정 사유 박스 추가. 공격력 표기를 ×10 값(150)으로 정정 | `BuildingType.cs`, `StatsReference.md`(3종족 타워 공격력 150), `GameSystemRules_Upgrade.md` 규칙 1(×10) |
| **§5 MistShrine(물안개 신전) 절 신설** — 재설계 컨셉을 기획 관점 자연어로 서술, 규칙 문서 링크, **기획 확정/구현 미착수** 명시 | Buildings MistShrine 규칙 1~16 |
| 기존 §5 연구소 → **§6**으로 번호 조정 | 절 삽입에 따른 정합 |
| 종족 시스템 초월계: 방어 타워를 **VineTower**로 정정, 종족 특성의 "MistShrine 범위 힐(1 HP/s, 범위 3타일)"을 재설계 내용으로 갱신(미확정 명시), 자연회복과 중첩 적용 추가 | Buildings MistShrine 규칙 1·3·4·9·13·14·16 |
| 버전 1.11.0 → **1.12.0**, 최종 수정일 2026-08-10, 상단 변경 요약 + 변경 이력 표 항목 추가 | 문서 관리 관례 |

### 2-6. `Assets/_Project/Docs/TechnicalDesignDocument.md` [수정]

| 변경 | 근거 |
|------|------|
| 건물 타입 주석 `Trans=MistShrine` → **`Trans=VineTower`** 정정 + `HealShrine = 6`이 별도 enum임을 주석으로 명확화 | `BuildingType.cs`(`AutoTower = 2`, `HealShrine = 6`) |
| 버전 0.43.0 → **0.43.1**, 최종 수정일 2026-08-10, 정정 요약 추가 | 문서 관리 관례 |

### 2-7. `Assets/_Project/Docs/StatsReference.md` [수정]

| 변경 | 근거 |
|------|------|
| 초월계 §특수 건물 표의 "힐 건물 (MistShrine)" 힐량을 **미확정**으로 변경, 효과란에 물안개 지속 힐 + **기획 확정/구현 미착수** 표기 | Buildings MistShrine 규칙 16 |
| 표 아래에 재설계 요약 + 미확정 항목 + "HP 500 / 건설비 100 / 10 HP/s(범위 3)은 재설계 이전 값" 주석 추가 | Buildings MistShrine 규칙 16, Upgrade 규칙 1 주석 |
| 상단 후속 보류 목록의 MistShrine 표기 갱신, 최종 수정일 2026-08-10 + 변경 요약 | 상태 정확 표기 |

### 2-8. `Assets/_Project/Docs/PROJECT_STATUS.md` [수정]

| 변경 | 근거 |
|------|------|
| "📐 확정 설계 — 구현 예정"에 **MistShrine 물안개 힐 시스템** 항목 신설(동작·UI·로직·신규 필요·미확정·문서 목록) | Buildings/UI 신설 규칙 전체 요약 |
| 연구소 섹션 후속 보류 ④번 항목을 "재설계 기획 확정 / 구현 미착수"로 갱신 | 상태 정확 표기 |
| 문서 상단 최종 수정일·요약·"현재 단계" 갱신 | 문서 관리 관례 |
| **과대 표기 금지 준수** — 모든 표기를 "기획 확정 / 구현 미착수"로 통일, "완료" 표현 사용 금지 | WORKFLOW.md, CLAUDE.md 규칙 10 |

### 2-9. [신규] task 문서 2종

- `Research.md` — 문서 불일치 내역, 기존 코드 자산 조사, 영향 범위, 부가 이슈
- `Plan.md` (이 문서)

두 문서 모두 **첫 부분에 자연어 설명**을 둔다(CLAUDE.md 규칙 13).

---

## 3. 이번 작업에서 하지 않는 것 (범위 밖 — CLAUDE.md 규칙 6)

- **코드·프리팹·씬·에셋 수정 일절 없음.**
- `ROADMAP.md` 수정 없음 — MistShrine 힐량/범위가 "확정"으로 적혀 있어 재설계 후 갱신이 필요하지만,
  이번 지시 범위(문서 8종)에 포함되지 않았다. **사용자 승인 후 별도 진행 권장**(Research.md §5-2).
- `Assets/AssetList.md` · `VFXSFXList.md` 수정 없음 — 물안개 VFX 신규 제작 항목 등재는 에셋 작업 시점에 진행.
- `Testcase.md` 미작성 — WORKFLOW.md [5-1] 및 절대 금지 사항에 따라 **사용자 명시 지시가 있을 때만** 작성한다.
- 밸런싱 수치 결정 없음 — game-design-lead 협의로 별도 진행.

---

## 4. 위험 요소

| # | 위험 | 대응 |
|:-:|------|------|
| R1 | **구현 상태 과대 표기** — 규칙 문서가 상세할수록 "구현된 것"으로 오독될 수 있다 | Buildings·UI 신설 섹션 서두, GDD §5, StatsReference, PROJECT_STATUS 전부에 **"기획 확정 / 구현 미착수"** 를 명시. "완료" 단어 사용 금지 |
| R2 | **규칙 번호 인용 모호성** — Buildings.md는 섹션마다 규칙 번호가 1부터 다시 시작하는데, 타 문서가 섹션명 없이 "Buildings.md 규칙 9"로 인용한 사례가 있다 | 파일 서두에 인용 규약 명시 + `GameSystemRules_Upgrade.md`의 기존 인용에 섹션명 보강 |
| R3 | **오류 정정이 새 혼동을 낳음** — 과거 문서를 읽은 기억으로 "MistShrine = 방어 타워"를 다시 적을 수 있다 | 정정 지점(GDD §4, TDD 주석, Buildings 방어 타워 서두)에 **"2026-08-10 정정" 경고 박스**를 남겨 재발 차단 |
| R4 | **미확정 수치가 확정처럼 굳어짐** — StatsReference의 10 HP/s·범위 3이 재설계 후에도 유효한 것처럼 인용될 수 있다 | StatsReference·Upgrade 규칙 1·Buildings 규칙 16 세 곳에 "재설계 이전 값 / 밸런싱 재검토 대상" 명시 |
| R5 | **문서 비대화** — 특수 건물이 늘면 Buildings.md가 관리 곤란해진다 | 섹션 서두에 **분기 시점("특수 동작 건물 2종 이상 시 `GameSystemRules_SpecialBuildings.md`로 분리")** 명문화 |
| R6 | **구현 단계에서 힐 채널 충돌** — 물안개 힐을 기존 `Heal` 버킷에 넣으면 BloomFairy 힐·자연회복과 서로 덮어쓴다 | Buildings 규칙 14에 **독립 채널 구현 요구**를 계약으로 명시(Upgrade 규칙 7 선례 인용) |
| R7 | **거리 동률 시 서버/클라 판정 분기** — 중첩 해소 기준이 순회 순서에 좌우되면 멀티에서 화면이 갈린다 | Buildings 규칙 13에 **"거리 동률이면 건물 Id가 작은 쪽"** 결정적 규칙 명시 |

---

## 5. 아키텍처 제약 (구현 단계 사전 고지 — 이번 작업 범위 아님)

이번 작업은 문서 전용이지만, 규칙 문서가 **구현 계약**이 되므로 아래 제약을 규칙에 반영해 두었다.

| 제약 | 반영 위치 |
|------|----------|
| **Application → Unity.Netcode 직접 참조 금지** — 전용 UseCase는 Netcode를 모르고, 네트워크는 Infrastructure가 담당 | Buildings 규칙 20·22 |
| **NetworkBehaviour는 Infrastructure에만** | Buildings 규칙 22 |
| **RPC 메서드명은 `ServerRpc` / `ClientRpc` 접미사 필수** | Buildings 규칙 22 |
| **서버 권위** — 시전 판정·대상 수집·회복 적용·쿨다운은 서버에서만 | Buildings 규칙 22 (방어 타워 규칙 9와 동일 원리) |
| **이중 틱 금지** — 싱글 = `GameBootstrapper.Update` / 멀티 서버 = `NetworkCombatController` / 순수 클라 = 로컬 미러만 | Buildings 규칙 21 |
| **Domain → Core 참조 금지** — 원형 반경 판정에 필요한 월드 좌표는 `IEntityPositionProvider` 등 주입 경로로 얻는다 | Buildings 규칙 3·23 (기존 특수 공격 핸들러 선례와 동일) |
| **Application → Infrastructure 역참조 금지** — 인터페이스는 Application에 선언, 구현은 Infrastructure | Buildings 규칙 20·22 |
| **`GameBootstrapper`가 유일한 조합 루트** | Buildings 규칙 21·25 |

---

## 6. 구현 단계에서 필요한 신규 작업 목록 (참고 — 이번 범위 아님)

> 다음 작업(구현) 착수 시 이 표를 출발점으로 삼는다. **현재는 전부 미착수다.**

### 6-1. 코드 (레이어별)

| 레이어 | 신규/수정 | 내용 | 근거 규칙 |
|--------|:-:|------|------|
| Domain | 수정 | `BuildingData`에 회복 경로 추가(MaxHp 클램프) — 현재 `TakeDamage`만 존재 | Buildings 규칙 24 |
| Application | **신규** | MistShrine 전용 UseCase(시전 검증·물안개 상태·틱 회복·쿨다운·자동 모드 bool) | Buildings 규칙 19·20·21 |
| Application | **신규** | 아군 유닛 + 아군 건물 원형 반경 수집 헬퍼(시전 건물 자신·Castle 포함) | Buildings 규칙 4·23 |
| Application | 수정 | 힐 채널 분리 — 물안개 힐이 `Heal` 버킷·자연회복과 겹치지 않도록 독립 채널화 | Buildings 규칙 14 |
| Application | 수정 | 중첩 해소(가까운 건물 우선, 거리 동률 시 Id 오름차순) 판정 | Buildings 규칙 13 |
| Application | 수정 | 건물 파괴 시 물안개·자동모드·쿨다운 정리(`GameEvents.OnBuildingDied` 구독) | Buildings 규칙 12·25 |
| Infrastructure | **신규** | MistShrine 네트워크 컨트롤러(NetworkBehaviour) — 시전 요청·자동 토글 3단 구조·쿨다운 브로드캐스트 | Buildings 규칙 22 |
| Infrastructure | 수정 | 건물 HP 회복 멀티 동기화(`NetworkHealthSync` 힐 경로가 현재 유닛 전제) | Buildings 규칙 24 |
| Infrastructure | 수정 | 서버 틱 진입점 배선(싱글/멀티, 이중 틱 금지) | Buildings 규칙 21 |
| Presentation | **신규** | MistShrine 전용 패널(`BuildingPanelBase` 상속, 탭/롱프레스, 자동 인디케이터, `SkillCooldownOverlay` 배치) | UI 규칙 1~7 |
| Presentation | **신규** | 범위 표시 반투명 원형 오브젝트(아군·패널 열린 동안만) | UI 규칙 8, Buildings 규칙 27 |
| Presentation | 수정 | 회복 텍스트 표시 주기 분리 처리(기존 `ShowText` 억제 메커니즘 활용) + `FloatingHpTextSpawner.ShowHeal` 주석 갱신 | UI 규칙 9 |
| Bootstrap | 수정 | `GameBootstrapper`에 신규 UseCase·컨트롤러·패널 조합 및 주입 | 조합 루트 단일화 제약 |

### 6-2. Inspector / 에디터 작업 ([5-2] 1회성 에디터 스크립트 대상)

WORKFLOW.md [5-2]에 따라, 아래는 **Editor 1회성 스크립트(`Hexiege/...` 메뉴)로 자동화한 뒤 사용자에게 실행을 요청**한다.

| 항목 | 내용 |
|------|------|
| MistShrine 패널 프리팹 생성·배치 | 기존 건물 패널(BuildingActionPanel / ResearchPanel) 룩을 복제해 골격 생성, Canvas 계층 배치는 `GameSystemRules_CanvasSortingOrder.md` 준수 |
| 패널 `SerializeField` 배선 | `BuildingPanelBase`의 `_popup` / `_headerText` / `_cancelButton` / `_demolishButton` / `_demolishRefundText` / `_colorConfig` + 사용 버튼 + `SkillCooldownOverlay`(`_fillImage` Radial 360·`_remainingText`·`_canvasGroup`) |
| 자동 모드 인디케이터 배선 | 생산 패널의 테두리 회전 머티리얼·인디케이터 자산 재사용 배선 |
| 범위 표시 오브젝트 + 머티리얼 | `SkillAimOverlay.shader` 기반 머티리얼 생성(스킬 조준원 셋업 스크립트 `EnsureOverlayMaterial` 패턴 참고) 및 SpriteRenderer 배선 |
| 밸런싱 수치 config 반영 | 회복량·지속시간·쿨다운·반경 — **수치 확정 후** `BuildingStatsConfig` 등에 반영(Inspector 값이 코드 기본값보다 우선) |

### 6-3. 에셋

| 항목 | 상태 |
|------|------|
| **물안개 지속 VFX** | **신규 제작 필요.** 현재 MistShrine 등록 VFX는 `vfx_mistshrine_destroy` / `vfx_mistshrine_upgrade` 뿐(`VFXSFXList.md`) |
| 물안개 SFX | VFX와 쌍으로 필요(`GameSystemRules_Sound.md` 규칙 15) |
| 범위 표시 머티리얼 | 기존 `SkillAimOverlay.shader` 재사용 우선 검토 — **ZTest Always 금지** |
| 힐 VFX(대상 위) | 현재 프로젝트에 전용 힐 VFX 프리셋이 없어 텍스트만 표시 중(`FloatingHpTextSpawner` 주석). 확보 시 함께 반영 |

---

## 7. 완료 판정 (이번 문서 작업)

- [ ] 8개 문서 수정 완료, 상호 내용 충돌 없음
- [ ] 모든 MistShrine 관련 표기가 **"기획 확정 / 구현 미착수"** 로 통일 (과대 표기 0건)
- [ ] GDD·TDD의 "Trans 방어 타워 = MistShrine" 오류가 전부 정정되고 정정 주석이 남아 있음
- [ ] 미확정 수치가 세 곳(Buildings 규칙 16 / Upgrade 규칙 1 주석 / StatsReference)에서 일관되게 "미확정"으로 표기됨
- [ ] Buildings.md MistShrine 섹션 서두에 **분기(분리) 시점 문구**가 존재함
- [ ] task 문서 2종의 첫 부분에 자연어 설명이 존재함 (CLAUDE.md 규칙 13)
- [ ] 코드·프리팹·씬·에셋 변경 0건, git 명령 실행 0건
