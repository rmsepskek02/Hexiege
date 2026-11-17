# Research — 지원 스킬 2-L 공습 (인간 FlightFacility 슬롯 2)

## 이 문서는 무엇인가

인간 스킬 건물(FlightFacility)의 두 번째 스킬 **공습**은 기획상 「지정한 방향의 **직선 범위**를 폭격해 적 유닛과 적 건물에 피해를 준다」는 스킬이다. 지금 게임에는 이 스킬이 슬롯에 들어가 있기만 하고 **「사용 불가」로 꺼져 있다.** 이유는 두 가지다.

- 지금 스킬은 모두 **원 하나**(지점 + 반경)로만 범위를 정한다. **직선(길쭉한 직사각형) 범위**와 그 직선의 **방향**이라는 개념이 게임 어디에도 없다.
- 조준도 「화면을 누르고 끌어서 원을 옮긴 뒤 손을 떼면 발동」하는 방식뿐이라, **방향을 정하는 조작**이 없다.

사용자는 2026-11-17에 공습의 동작을 정했다. 공습은 직선 위를 **시작점에서 끝점 쪽으로 차례대로 여러 번 떨어지는 폭격(연속 폭격)** 이고, 조준은 **화면을 누른 곳이 직사각형의 시작점, 손을 떼지 않고 끈 방향이 직사각형의 방향**이 되며, **충분히 끌지 않고 손을 떼면 발사되지 않는다.** **두 손가락으로 화면을 옮기고 확대 · 축소**하는 조작은 공습뿐 아니라 모든 지점 지정 스킬 조준 중과 평소에도 쓰고, PC에서는 오른쪽 버튼 끌기로 화면을 옮긴다. 한 번 쓴 공습에 같은 대상은 한 번만 맞고, 직선이 맵 밖으로 나가면 그 부분은 잘라낸다(2026-11-17 2차 결정). 조준하던 손가락을 둔 채 두 번째 손가락을 대도 조준은 유지되고 표시는 첫 손가락 아래 지점을 따라가며, 발동은 한 손가락만 남았을 때 손을 떼야 한다(2026-11-17 3차 결정).

이 문서는 그 결정을 구현하려면 **지금 코드가 어떻게 되어 있고 어디를 고쳐야 하며 무엇이 함께 영향을 받는지**를 정리한다. 무엇을 어떤 순서로 만들지, 남은 결정, 테스트 방법은 [Plan.md](Plan.md)에 있다. **코드 · 자산은 이 문서 작성 중 바뀌지 않았다.**

> **현재 상태 (2026-11-17):** 조사 기록(HEAD `1f9d048`, 브랜치 `claude/vfx-code-followups`). 사용자 결정은 Plan 1절 — 1차(조준 방식 · 연속 폭격) U-1~U-10, **2차(두 손가락 · 오른쪽 버튼 화면 이동 범위 · 시전당 1회 · 두 번째 손가락 중단 · 맵 밖 잘림) U-11~U-14**, **3차(두 손가락 중 조준 유지 · 첫 손가락 추종 · 「한 손가락만 남아야 발동」 · 공습 통일로 U-13 대체 · 두 손가락 중 엣지 스크롤 끔) U-15~U-18**, 메인 세션 기본안 M-1 · M-2. *(2026-11-17 3차 갱신)* 조사 에이전트 설계안은 Plan 2절, 남은 열린 항목은 Plan 3절. **구현 착수 전 — Plan 사용자 승인 대기.** *(2026-11-17 2차 갱신 — 직전 「사용자 결정(조준 방식 · 연속 폭격)은 Plan 1절, …」)*

**사용자 결정의 출처:** 아래 결정은 메인 세션이 사용자 대화에서 정리해 전달한 것이다 **[전달 — 메인 세션 정리]**. 사용자 원문 문장 그대로가 아니라 정리된 항목이다(Plan 1절에 같은 항목을 결정 번호로 둔다).

**출처 · 표기:** 코드 사실은 조사 에이전트의 HEAD `1f9d048` 읽기 전용 조사 결과 **[전달]** 이다. 그중 document-manager가 같은 커밋의 파일을 직접 다시 읽어 행 내용을 대조한 것만 **[확인]** 으로 적었다. 대조에서 어긋난 인용은 지우지 않고 **[정정 — 문서 담당]** 으로 바른 값을 덧붙였다(14절에 모음). `[문서 담당]` = document-manager가 덧붙인 관찰(결정 아님). 경로 머리 `Scripts/` = `Assets/_Project/Scripts/`. 「상위 Plan N행」 = 아래 상위 Task Plan의 행 번호(HEAD `1f9d048`).

**상위 Task:** [종족별 지원 스킬 기획 Plan](../../2026-11-02/14_25_race-support-skills-design/Plan.md) — 1-1(인간 슬롯 2), 2절(공습 행), 2-1(V8), 3-1(인간 2 행), 3-2(2-L 행), 3-3 B-1, 4절 16, 5절 O-5, 6절 R-9 · R-10, 7-1(V8). **선례:** [포격 Task](../../2026-11-03/05_24_support-skill-foundation-bombardment/Plan.md#10-implementation-and-user-test)(착탄 예약 · 서버 권위 틱 · 발동 표현), [지원 스킬 새 상태 Task](../../2026-11-16/01_00_support-skill-new-statuses/Plan.md#14-stage-b-result)(문서 형식 · 스키마 9 → 10 · 셋업 도구 기대 수), [지속 상태 구역 Task](../../2026-11-10/16_02_support-skill-persistent-zones/Plan.md#11-7-final-result)(새 메커니즘 번호 4 · 새 규칙 28).

---

## 1. 범위

| 이 Task가 다루는 것 | 이 Task에서 하지 않는 것 |
|---|---|
| 공습의 서버 판정 — 직선(직사각형) 안 판정, 연속 폭격 예약 · 처리 | 공습 VFX 프리팹 제작(V8 — VFX 기획 · 제작 단계, 상위 Plan 3-0) |
| 방향을 서버로 보내고 양쪽 화면에 알리는 전송(전투 표현 스키마 10 → 11) | 수치 확정 — 길이 · 폭 · 피해 · 쿨다운 · 적 건물 피해 비율(밸런싱 단계, 상위 Plan 3-3 B-1) |
| 조준 입력 — 시작점 터치 · 방향 드래그 · 최소 드래그 · 재터치 · X 취소 | 다른 지점 지정 스킬(포격 · EMP · 서리 감옥 · 수액 늪)의 조준 방식 변경 — 단, 조준 중 두 손가락 · 오른쪽 버튼 화면 이동은 더한다(U-11), 두 번째 손가락 뒤 처리는 Plan O-11 |
| 두 손가락 화면 이동 + 확대 · 축소(**조준 중 · 평소 모두**), PC 오른쪽 버튼 끌기 화면 이동(U-11) | 평소 한 손가락 · 왼쪽 버튼 팬 · 휠 줌 변경 |
| 맵 밖 직선 잘라내기(U-14) · 시전당 대상 1회(U-12) | |
| 직선 조준 표시(직사각형 + 화살표) | 연막탄 · 메테오 · 원소 공명(「사용 불가」 그대로) |
| 발동 VFX 칸 · 폭격 1회분 VFX 칸(비워도 동작) | AI의 스킬 사용(AI는 스킬을 쓰지 않는다) |
| 공습 켜기(셋업 도구 기대 수 12) · 진단 로그 · 자가 검증 | |

---

## 2. 근거로 읽은 규칙 · 문서

- `GameSystemRules/GameSystemRules_Skills.md` — 규칙 3(건물 글로벌 쿨다운) · 7(데이터 주도 — 요소 표) · 9(슬롯) · 11(타입 A — 착탄 지연 · 「적 건물 피해 비율은 이 개정에 들어 있지 않다 — 밸런싱 단계」 149행) · 16(지점 지정 발동) · 17(탭으로 조준 모드 진입) · 18(엣지 스크롤) · 19(손 떼면 발동 · 연속 좌표) · 20 · 20-1(X 취소 · release 프레임 캐시 좌표) · 21 · 22(맵 경계 clamp) · 22-1(지면 데칼) · 23 · 24 · 25(서버 권위 · 착탄 지연 서버 틱 · 발동 표현) · 26(좌표 1개 전송 + 서버 재검증) · 27 · 28(새 메커니즘에 글자 이름을 붙이지 않은 선례) [확인]
- `GameDesignDocument.md` 「카메라 시스템」 — 화면 이동 = 한 손가락 드래그, 확대 · 축소 = 두 손가락 핀치(73~74행) [확인]
- `TechnicalDesignDocument_Network.md` 「경기 시작 호환 확인」 — 스키마 = 10, 「RPC 모양이 바뀌면 스키마를 올린다」(40행) [확인]
- `WORKFLOW.md` [3] · [4](「기존 로직 제거 규칙」 · 「테스트 항목 진단 로그 규칙」) · [6](「테스트 로그 올리기」 수집 메뉴 · 회귀 스위트 판정) [확인]
- `.claude/MEMORY.md` 「공통 중요 교훈」 게임 규칙 체크리스트(한 경기 한 종족 · 자기 스킬 건물 · AI는 스킬을 쓰지 않음 · 건물은 짓는 즉시 생김 · 싱글/멀티 역할) [확인]

---

## 3. 핵심 발견 6가지

1. **스킬 시스템 전체에 「방향」이 없다.** 조준 확정 콜백은 점 하나(`Action<int, int, Vector3>` — `SkillAimController.cs` 119 · 196) [확인], 발동 문맥은 `AimWorld` · `HasAim`만(`SkillActivationContext.cs` 105~129) [확인], 발동 표현도 방향 칸이 없다(`SkillActivationPresentation.cs` 79~97) [확인]. 요청 RPC는 `(int, int, Vector3 aimWorld, bool hasAim)`(`NetworkSkillController.cs` 231~233) [확인]. → 조준 · 요청 · 발동 문맥 · 발동 알림 네 곳에 방향을 더해야 한다.
2. **범위 판정은 원뿐이다.** 스킬 피해 대상 수집은 「타일 중심 ↔ 중심점」 거리² ≤ 반경²(`UnitCombatUseCase.cs` 2786~2815 — 유닛 2793~2797, 건물 2808~2812) [확인]. 직사각형 판정은 새로 만든다.
3. **연속 폭격에 쓸 예약 장치가 이미 있다.** 포격의 착탄 예약 `PendingImpact`(`SkillActivationUseCase.cs` 401~418) [확인]은 서버 권위 틱에서만 줄고(`TickPendingImpacts` 463~498) [확인], 경기 종료에 남은 예약을 버린다(`ClosePendingImpacts` 505~511) [확인]. 다만 예약 1건 = 원 1개 · 피해 1회라, 직선의 「몇 번째 구간」을 담을 칸이 없다.
4. **서버 틱은 0.1초다.** 멀티 서버 틱이 `_attackInterval`(씬 값 0.1초) 주기라 착탄이 최대 그만큼 늦을 수 있다(`NetworkCombatController.cs` 778 주석, 780 `TickPendingImpacts(elapsed)`) [확인]. → 폭격 간격은 0.1초의 배수로 잡는 것이 깔끔하다 [전달].
5. **조준 중 카메라는 한 손가락 팬이 꺼져 있고 엣지 스크롤만 돈다.** `CameraController.HandlePan` 294 `if (SkillAimController.IsAiming)` → 팬 억제 [확인], 두 손가락이면 팬 비활성(301~306) [확인]. 확대 · 축소(`HandleZoom` 204)에는 조준 중 억제가 없다 [전달 — 이 문서는 `HandleZoom` 본문 전체를 읽지 않았다]. 조준 컨트롤러는 드래그 중 매 프레임 `EdgeScroll`을 부른다(`SkillAimController.cs` 308~309) [확인]. → **두 손가락 팬은 지금 어디에도 없다**(평소 두 손가락 = 핀치 줌 전용 — GDD 74행).
6. **조준 표시는 원(타원)뿐이다.** `SkillAimReticle.Show(worldPos, radius)`(133~145) [확인]. 직선 표시는 새로 만든다 — 런타임 메시 선례는 방어탑 범위 표시 `TowerRangeIndicator`(원판 · 고리 · 부채꼴 메시를 코드로 만든다 — 132 · 133 · 151 · 152) [확인].

---

## 4. 기획 원문 인용 (상위 Plan)

| 자리 | 원문 | 등급 |
|---|---|---|
| 1-1 인간 슬롯 2 (74행) | 「공습 \| 일시 타격 \| 모두 \| 직선 범위 피해」 | [확인] |
| 2절 공습 행 (131행) | 효과 「직선 범위 피해」, 대상 「적 유닛 · 적 건물」, 조준 「지점 + 방향」, 필요 기능 「**L** · 방향 조준 · 방향 전송」, 필요 VFX 「직선 경고 표시 + 연속 폭격」, 근거 규칙 「스킬 규칙 11 · 16 · 17 · 26 (개정 필요)」 | [확인] |
| 2-1 V8 (155행) | 「직선 경고 + 연속 폭격 \| 공습 \| 발동 \| 신규」 | [확인] |
| 3-1 인간 2 (206행) | 「V8 \| 2-L \| 적 건물 피해 비율 · 직선 길이 · 폭」 — 밸런싱에서 정할 것 | [확인] |
| 3-2 2-L (290행) | 「**선형 범위** + 방향 조준 \| 공습 \| 조준 UX · 전송 데이터 · 규칙 26 개정이 함께 필요」 | [확인] |
| 3-3 B-1 (299행) | 적 건물 피해 비율 — 「포격 지원 · 공습 · 메테오가 적 건물에 주는 피해를 얼마나 줄일지」, 사용자 원문 「건물 피해의 경우 구체적인 수치 작업이기 때문에 밸런싱에 포함하자」 | [확인] |
| 4절 16 (343~344행) | 「방향 조준 입력 방식(O-5)에 따른 전송 데이터와 서버 재검증(맵 경계 안 점 판정을 선의 양 끝에도 할지).」 | [확인] |
| 5절 O-5 (374행) | 「공습의 방향 지정 방식 — 예: 두 번째 드래그로 방향 · 시작점→끝점 드래그 · 고정 방향(내 진영→적 진영) \| 2-L, 스킬 규칙 17 · 19 · 26 \| 열림」 | [확인] |
| 6절 R-9 · R-10 (447~448행) | R-9 규칙 17 · 22-1 「공습의 직선 조준 표시 추가(O-5 확정 후)」, R-10 규칙 26 「공습 방향 데이터 추가(O-5 확정 후). 서버 재검증 대상 확장」 | [확인] |
| 7-1 V8 (507행) | 「V8 공습 \| 2-L \| 조준 방식(O-5) 확정 뒤」 | [확인] |

- **O-5는 이번 사용자 결정(Plan 1절 U-2~U-9)으로 답이 정해졌다** — 「시작점 → 끝점 드래그」 계열(터치 = 시작점, 드래그 방향 = 방향). 상위 Plan의 O-5 칸은 이 Task에서 고치지 않는다(Plan 11절 — 테스트 통과 뒤 반영).
- 4절 16의 「선의 양 끝에도 맵 경계 판정을 할지」는 2차 결정 **U-14 「맵 밖으로 나간건 짤라내고 맵안에서만 발동」**으로 답이 생겼다(끝점을 거부하지 않고 잘라낸다 — Plan P-13). *(2026-11-17 2차 — 직전 「이번 결정에 들어 있지 않다 → Plan 3절 열린 항목」)*

---

## 5. 지금 자산 · 셋업 도구 · 스위트 상태

| 자산 `Resources/Config/Skills/Skill_Human_AirStrike.asset` | 값 | 등급 |
|---|---|---|
| 사용 가능 · 조준 · 메커니즘 | `_isAvailable: 0` · `_aimType: 1`(PointTarget) · `_mechanic: 0`(InstantAreaDamage) | [확인] |
| 수치 | `_cooldown: 10` · `_radius: 2` · `_duration: 0` · `_totalDamage: 150` · `_impactDelay: 0` · `_targetsAllies: 0` | [확인] |
| VFX | `_activationVfxPrefab: {fileID: 0}`(비어 있음) · `_vfxReferenceRadius: 2` | [확인] |
| 이름 · 설명 | 「공습」 · 「지정한 방향의 직선 범위를 폭격해 적 유닛과 적 건물에 피해를 준다.」 | [확인 — 유니코드 이스케이프 해독] |

| 셋업 도구 `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs` | 등급 |
|---|---|
| 96행 `private const int ExpectedAvailableCount = 11;`(95행에 옛 값 `8` 주석) | [확인] |
| 183~192행 `PointPlaceholder` — 사용 불가 · PointTarget · InstantAreaDamage · 쿨다운 10 · 반경 2 · 피해 150 | [확인] |
| 300~301행 `PointPlaceholder("Skill_Human_AirStrike", h, 1, "공습", …)` | [확인] |
| 498~500행 — 사용 가능 수가 `ExpectedAvailableCount`와 다르면 쓰기 전에 멈춘다 | [확인] |

| 스위트가 고정한 값 | 등급 |
|---|---|
| 사용 가능 수 `== 11` — `RunUnitActionSelfValidation.SkillZones.cs` 349 · `.SupportNewStatuses.cs` 594 · `.SupportRegen.cs` 519 · `.SupportShield.cs` 357 (**4파일**) | [확인] [정정 — 문서 담당: 전달 「스위트 3파일」 → 4파일, 14절] |
| 스키마 `== 10` — `.SupportNewStatuses.cs` 596 · `.SupportRegen.cs` 521 · `.SupportShield.cs` 353 · `.ServerProjectiles.cs` 265 · `.SupportBuffSkills.cs` 265 · `.SkillZones.cs` 344 (**6곳**) | [확인] |
| 스키마 상수 `PresentationContractSchemaRevision = 10` — `Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs` 41 | [확인] |

- 메커니즘 번호: `SkillMechanicType` = `InstantAreaDamage 0` · `AreaDotDamage 1` · `GlobalStatusChange 2` · `AreaTowerDisable 3` · `PersistentStatusZone 4`(`Scripts/Domain/Skill/SkillMechanicType.cs`) [확인]. 조준 방식: `SkillAimType` = `Instant 0` · `PointTarget 1` [확인]. 실행기는 메커니즘 → 실행기 사전(`SkillExecutorRegistry.cs` 34~35)으로 고른다 [확인].
- 타일 폭: `HexMetrics.TileWidth` 기본 1.0, 맵 로드 때 설정 값으로 덮어쓴다(`GameBootstrapper.Setup.cs` 265) [확인]. → 「약 0.5칸」 최소 드래그 임계값은 월드 단위로 잡을 때 이 값을 기준으로 한다 `[문서 담당]`.

---

## 6. 조준 입력 — 지금 구조 (`Scripts/Presentation/Input/SkillAimController.cs`)

| 자리 | 지금 동작 | 공습에 필요한 변화 | 등급 |
|---|---|---|---|
| `BeginAim` 195~220 | 건물 · 슬롯 · 반경 · 대체 위치 · 확정 / 취소 콜백을 받아 조준 시작, 버튼 탭 gesture가 끝날 때까지 대기(`_pendingButtonRelease`), 화면 중앙(맵 밖이면 시전 건물)에 조준 원 표시, X 버튼 표시 | 직선 스킬이면 조준 표시를 직선용으로, 확정 콜백이 시작점 + 방향을 넘겨야 한다 | [확인] |
| `Update` 1단계 239~260 | 버튼 release 대기 → 그다음 「새 press」부터 드래그 시작, 누른 자리로 조준점 이동 | 누른 자리 = **시작점**(U-2). 조준 중 다시 누르면 시작점 이동(U-6) | [확인] |
| `Update` 2단계 283~312 | release 프레임은 캐시 좌표 `_lastDragScreenPos`로만 판정(실기 취소 버그 수정 — 264~282 주석), 드래그 중 `UpdateAimPoint` · `EdgeScroll` · X hover | 드래그 = **방향**(시작점 고정, U-3). **엣지 스크롤 끔**(U-8). release 캐시 판정은 그대로 써야 한다(규칙 20) | [확인] |
| `UpdateAimPoint` 319~341 | 화면 → XZ 평면(뷰) → 도메인(`ViewConverter.FromView`) → 맵 경계 clamp → 조준 원 표시 | 시작점은 같은 변환 · clamp. 방향 = 도메인(손가락) − 도메인(시작점) `[문서 담당]` | [확인] |
| `ResolveRelease` 346~365 | X 위면 취소, 유효 좌표가 없으면 취소, 아니면 `_onConfirm(건물, 슬롯, 좌표)` | **드래그 거리 < 임계값이면 발사도 취소도 하지 않고 조준 유지**(U-5), X 위면 취소(U-7) | [확인] |
| `IsAiming` 정적 플래그 50 · 206 · 383 | 카메라 팬 억제(`CameraController` 294) · 타일 선택 억제(`InputHandler.cs` 250)가 읽는다 | 두 손가락 카메라는 조준 중에만(U-9) — 이 플래그가 기준이 될 수 있다 | [확인] |

- 「드래그하지 않으면 발사하지 않음」은 지금 규칙 19(「손가락을 떼는 순간 그 위치에 스킬이 발동」)와 다르다 → 규칙 개정 필요(Plan 11절).
- 지금은 조준 표시가 「진입 즉시 화면 중앙」에 나온다(213~215). 공습은 터치 전에 시작점이 없으므로 진입 직후 무엇을 보여 줄지는 메인 세션 기본안 **M-1**(직사각형 없이 기존 조준과 같은 범위 표시 · X만 — 사용자에게 보고 예정)로 정했다. *(2026-11-17 2차 — 직전 「(중앙에 기본 방향의 직사각형 / 아무것도 안 보임)는 결정에 없다 → Plan 3절 열린 항목」)*

---

## 7. 카메라 — 지금 구조 (`Scripts/Presentation/Camera/CameraController.cs`)

- `HandleZoom` 204~: 마우스 휠과 터치 핀치로 줌 [확인 — 머리 · 휠 분기만 읽음].
- `HandlePan` 290~: ① 조준 중이면 팬을 끄고 돌아간다(294~298) ② 두 손가락 이상이면 팬 끔(300~306) ③ 마우스 팬(에디터) ④ 터치 1개 팬(342 `activeTouches.Count == 1`) [확인].
- `EdgeScroll(screenPos, dt, edgeMarginPx, speed)` 433 — 조준 컨트롤러가 부른다. 여백 60px · 속도 8(`SkillAimController.cs` 82 · 85 직렬화 기본값) [확인].
- **핀치 줌(`HandleZoom` 232~247):** 터치가 **정확히 2개**일 때 이전 · 현재 프레임 두 손가락 거리 차 × 줌 속도 × 0.01을 줌 변화로 쓴다 [확인]. 팬은 없다.
- **마우스 오른쪽 버튼:** `Scripts` 안에서 `rightButton` · `middleButton` 사용 0건 [확인 — grep]. 오른쪽 버튼 끌기 팬은 새로 만든다.
- **조준 컨트롤러가 읽는 포인터:** 터치는 `Touchscreen.primaryTouch`(첫 손가락)만, 마우스는 `leftButton`만 읽는다(`TryGetPointerScreenPos` · `WasPointerReleasedThisFrame` · `WasPointerPressedThisFrame` · `IsPointerPressed` — 479~567) [확인]. 그래서 오른쪽 버튼은 조준을 건드리지 않는다. 반면 두 번째 손가락이 닿아도 조준은 계속 첫 손가락을 따라가고, **첫 손가락을 떼는 순간 지금 코드는 마지막 캐시 좌표로 발동한다** `[추정 — 코드 읽기, 실기 미확인]` — 3차 결정 U-16 「한 손가락만 남아야 발동」이 이것을 막는다(첫 손가락 먼저 떼면 미발동). *(2026-11-17 3차 — 직전 「공습은 U-13이 막고, 원형 조준 스킬은 Plan O-11」)* 첫 손가락이 떨어진 뒤 `primaryTouch`가 남은 손가락을 가리키게 되면 「첫 손가락」 판정과 release 캐시 좌표(`_lastDragScreenPos`)가 그 손가락 값으로 바뀔 수 있다 → 터치 Id 추적이 필요하다(메인 세션 검토 b). 실기 취소 버그 수정 커밋으로 전달된 `4e5da5e`는 이 얕은(shallow) checkout의 `git log --all`에 없어 확인하지 못했다.
- **조준 원 크기는 월드 기준이다:** `SkillAimReticle.Show`가 월드 위치를 지정하고(142 `transform.position = new Vector3(worldPos.x, _yOffset, worldPos.z)`), `ApplyRenderer`가 월드 지름 ÷ 스프라이트 원본 지름으로 `localScale`을 정한다(189~198) [확인]. 그래서 줌하면 화면에서 함께 커지고 작아진다 — U-15의 「ui는 그에 맞게 확대 축소」는 별도 처리가 필요 없다.
- **한 손가락 팬 기준점:** 터치 팬의 기준점 `_dragOrigin`은 터치 1개일 때 `TouchPhase.Began`에서만 잡힌다(`CameraController.cs` 342 · 347~351, 이동은 355~360에서 `_dragOrigin − 현재`) [확인]. 두 손가락에서 한 손가락으로 돌아오면 남은 손가락은 `Began`이 아니므로 옛 기준점으로 계산돼 화면이 튈 수 있다 → 전환 때 재설정 필요(메인 세션 검토 c).
- **타일 탭 판정:** `InputHandler`는 터치에서 `primaryTouch` 누름 위치를 기록하고 뗄 때 이동이 `ClickThreshold` = 10px 미만이면 `HandleClick`(102 · 199~211, 마우스는 180~190) [확인]. 두 손가락 제스처에서 첫 손가락이 거의 안 움직이면 뗄 때 클릭으로 처리될 수 있다 → 억제 필요(메인 세션 검토 d).
- **필요한 것(2차 결정 U-11 반영):** 두 손가락이면 「중점 이동 = 팬, 거리 변화 = 줌」을 **평소와 모든 지점 지정 스킬 조준 중에** 쓴다(지금 평소 두 손가락은 줌만, 팬은 300~306에서 막힘). PC는 오른쪽 버튼 끌기 = 팬. 평소 한 손가락 · 왼쪽 버튼 팬과 원형 조준의 한 손가락 드래그 · 엣지 스크롤은 그대로. *(2026-11-17 2차 — 직전 「조준 중 두 손가락이면 …(U-9). … 조준 중 한 손가락은 조준 전용이다. 평소 카메라는 바꾸지 않는다.」)*
- 에디터는 마우스뿐이라 두 손가락을 재현할 수 없다. 지금 에디터 마우스는 왼쪽 드래그 = 팬(조준 중엔 억제) · 휠 = 줌이다 [확인 — `HandlePan` 머리 주석 285~289 · 「마우스 팬 (에디터 및 데스크톱)」 분기 308 · `HandleZoom` 휠 분기 212~227]. 조준 중 에디터 대체 조작은 2차 결정 U-11로 **오른쪽 버튼 끌기 = 화면 이동**이 됐다(줌은 휠 그대로). *(2026-11-17 2차 — 직전 「결정에 없다 → Plan 3절 열린 항목(사용자에게 보고)」)*

---

## 8. 조준 표시 — 지금 구조

- `Scripts/Presentation/Effects/SkillAimReticle.cs` — `Show(worldPos, radius)` 133~145: 지면에 눕혀(`Euler(90,0,0)`) 위치 · 크기만 바꾼다 [확인]. 회전 · 직사각형 · 화살표가 없다.
- 지면 데칼 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader`(규칙 22-1 — `ZTest LEqual + Offset -1,-1`) [확인 — 규칙 문서]. 직선 표시도 같은 가림 관계(지형엔 안 가려지고 유닛 · 건물엔 가려짐)를 지켜야 한다.
- 런타임 메시 선례: `TowerRangeIndicator`(`BuildDisc` · `BuildAnnulus` · `BuildSector` · `BuildArcEdges` — 132 · 133 · 151 · 152, 정리 174~177) [확인].

---

## 9. 발동 경로 · 네트워크

| 자리 | 지금 | 공습에 필요한 변화 | 등급 |
|---|---|---|---|
| `BuildingSkillPanelUI.cs` 431~449 | `AimType == PointTarget`이면 팝업 · 차단 오버레이를 내리고 `BeginAim(…, skill.Radius, fallbackDomain(447), OnAimConfirm, OnAimCancel)` | 직선 스킬 정보(길이 · 폭)와 방향을 받는 확정 콜백 | [확인] |
| `BuildingSkillPanelUI.ActivateSkill` 489~503 | 멀티 = `RequestActivateSkill(건물, 슬롯, aimWorld, hasAim)`, 싱글 = `Activate(건물, 슬롯, 좌표?)` | 방향 인자 추가 | [확인] |
| `NetworkSkillController.RequestActivateSkillServerRpc` 231~264 | `(int buildingId, int skillSlot, Vector3 aimWorld, bool hasAim)` | 방향 인자 추가 → RPC 모양 변경 | [확인] |
| `NetworkSkillController.OnSkillActivationConfirmedOnServer` 271~282 · `SkillActivatedClientRpc` 303~318 | 건물 · 팀 · 슬롯 · 조준 여부 · 좌표 · 순번 · 착탄 지연 · 쿨다운 | 방향 추가 → RPC 모양 변경 | [확인] |
| `SkillActivationUseCase` 241~246 | 「사용 가능」이 꺼진 스킬은 `LogActivationRejected(…, "Unavailable")` 후 거부 | 공습 켜면 통과 | [확인] |
| `SkillActivationUseCase` 251~262 | 지점 지정이면 좌표 필수 · 맵 경계 안 점인지 재검증 | 방향 재검증(0 · NaN 거부 등) 추가, 끝점 재검증 여부는 열림 | [확인] |
| `SkillActivationContext` 105~129 | 맨 뒤 선택 인자로 다리를 늘려 온 생성자(지속 구역 · 지원 효과) | 방향 · 직선 폭격 다리를 같은 방식으로 맨 뒤에 | [확인] |
| `SkillActivationPresentation` 79~97 | 방향 칸 없음, 조준이 없으면 좌표 0 | 방향 칸 추가 | [확인] |

- 「RPC 모양이 바뀌면 스키마를 올린다」(TDD_Network 40행) → 요청 · 발동 알림에 방향을 더하면 **스키마 10 → 11**, 스위트 `== 10` 6곳 갱신, 에디터 · 기기 두 빌드 모두 새로 설치 [전달 — 스키마 판단] [확인 — 6곳].
- 지금 규칙 26은 「손을 뗀 순간의 조준 좌표(1개)만」 전송한다 → 개정 필요(상위 Plan R-10).

---

## 10. 서버 판정 · 피해 경로 · 착탄 예약

- **타입 A 다리:** `SelectInstantAreaDamageBridge` 426~433 — 지연 0이면 즉시 다리, 0보다 크면 `ScheduleImpact`로 예약 [확인].
- **예약 1건(`PendingImpact` 403~413):** 팀 · 중심 · 반경 · 원본 피해 · 시전 건물 · 순번 · 남은 시간 · 지연 [확인]. 처리(`TickPendingImpacts` 463~498): 남은 시간을 줄여 다 된 예약을 순서대로 처리, 처리 중 경기가 끝나면 나머지를 버리고 `LogImpactDropped(…, "ClosedDuringTick")` [확인].
- **피해 적용:** `ApplySkillInstantAreaDamage` 2589~2608 — 원 안 적 유닛 · 적 건물을 먼저 모은 뒤(순회 중 사망 제거 대비 2단계) 각각 `ApplySkillInstantDamageToVictim` [확인]. 그 안에서 방어 감쇄(`ApplyDefense`) → `IncomingDamage.Apply(…, IncomingDamageWriter.Skill)`(2723) — 방어 → 받는 피해 배율 → 쉴드 → HP 순서(유닛 규칙 44) [확인]. **건물도 같은 값으로 맞는다**(「2배 미적용」 주석 2603) — 적 건물 피해 비율 장치는 없다 [확인].
- **공습이 다시 쓸 수 있는 것:** 피해 1회 적용 함수(`ApplySkillInstantDamageToVictim`)와 예약 · 서버 틱 · 경기 종료 닫힘. **새로 필요한 것:** 직사각형 대상 수집(유닛 · 건물), 예약에 「직선 시작점 · 방향 · 구간 번호」를 담을 칸 또는 새 예약 종류 [전달 — 권장 구조, Plan 2절].
- **대상 판정 기준:** 지금 스킬 판정은 모두 「대상이 선 **타일 중심**」 기준이다(2793~2797 · 2808~2812, 규칙 27 · 28도 같은 기준) [확인]. 직선 판정도 같은 기준이면 일관된다 [전달].
- 피해 지점 ③(스킬 피해)은 지원 스킬 새 상태 Task가 이미 단일 헬퍼로 모았다 — 대지의 갑주 · 쉴드가 공습 피해에도 자동으로 걸린다 [확인 — 2721~2723].

---

## 11. VFX 경로

- `EffectManager.PlaySkillActivationVfx` 573~591: 회전 `Quaternion.identity` 고정, 균일 배율(`Vector3.one * uniformScale`)로 `VfxPoolItem.Play` 호출 [확인]. → 직선 경고 표시를 직선 방향으로 돌리거나 길이 방향만 늘릴 수 없다.
- `VfxPoolItem.Play` 341~361: 회전(`rot`) · 벡터 배율(`Vector3.Scale(원본, scaleMultiplier)`) · 전진 이동을 이미 받는다 [확인]. → 재생 쪽은 회전 · 길이 배율을 넘기기만 하면 된다.
- 발동 VFX는 서버가 확정한 발동 알림으로 양쪽 화면이 각자 재생한다(규칙 25) [확인 — 규칙]. 연속 폭격 1회분 VFX는 지금 칸도, 타이밍 장치도 없다 — 각 화면이 발동 알림의 시작점 · 방향 · 순번과 자기 빌드의 스킬 데이터(폭격 수 · 첫 지연 · 간격)로 타이밍을 재현할 수 있다 [전달 — 권장 구조, Plan 2절].
- 지면 높이: 스킬 VFX는 지면 + 0.06(`GameConfig.SkillVfxGroundYOffset` — 공용 메모리 EMP 항목) [전달].

---

## 12. 영향 범위

| 영역 | 영향 | 근거 |
|---|---|---|
| 다른 지점 지정 스킬(포격 · EMP · 서리 감옥 · 수액 늪) | 조준 컨트롤러 · 패널 · 요청 RPC를 공유한다 — 공습 분기가 그쪽 조준(원 · 손 떼면 발동 · 엣지 스크롤)을 바꾸면 안 된다 | 6 · 9절 |
| 즉시 발동 스킬 | 요청 RPC · 발동 알림 모양이 바뀐다(방향 칸은 안 쓴다) | 9절 |
| 카메라 | 두 손가락 팬 + 줌(평소 · 조준 중) · 오른쪽 버튼 팬 추가, 한 손가락 · 왼쪽 버튼 팬 · 휠 줌 불변 *(2026-11-17 2차 — 직전 「조준 중 두 손가락 처리 추가, 평소 조작 불변」)* | 7절 |
| 타일 선택(`InputHandler`) | 두 손가락 제스처를 뗄 때 10px 미만 탭이 건물 · 타일 클릭으로 오발동할 수 있어 억제 필요 *(2026-11-17 3차 — 직전 「… 확인 필요(이 문서는 `InputHandler` 탭 판정을 읽지 않았다) — 15절」)* | 7절 |
| 접속 호환 | 스키마 10 → 11 — 10 빌드와 11 빌드는 경기 시작에서 거부(의도된 동작) | TDD_Network 40행 |
| 회귀 스위트 | 사용 가능 수 `== 11` 4파일 · 스키마 `== 10` 6곳 | 5절 |
| 대지의 갑주 · 쉴드 | 공습 피해에도 그대로 걸린다(피해 지점 ③ 헬퍼) — 변경 없음 | 10절 |
| AI | AI는 스킬을 쓰지 않는다 — 공습을 쓰는 쪽은 언제나 사람이다 | 공용 메모리 게임 규칙 체크리스트 |

---

## 13. 범위 밖 관찰 — 기록만 (손대지 않음)

- **`.meta` 파일이 저장소에 없는 C# 파일이 19개다** `[문서 담당 — git ls-files 대조]`. 전달 관찰은 「SupportNewStatuses / SupportRegen / SupportShield `.meta` 없음」(Editor 3개)이었으나, 같은 방법으로 세면 `Scripts/Application/Combat/IncomingDamage.cs` · `Scripts/Application/Services/SupportSkillEffectSystem*.cs`(7개) · `Scripts/Application/Skill/BuildingStatusVfxTargetPolicy.cs` · `ShieldBrokenPresentation.cs` · `SkillExtraEffect.cs` · `Scripts/Presentation/Effects/BuildingStatusVfxPresenter.cs` · `UnitBuffVfxPresenter.ShieldDriver.cs` 등 지원 스킬 새 상태 Task가 만든 파일이 함께 빠져 있다. Unity가 열 때 새 GUID로 만들므로 동작에는 영향이 없을 수 있으나, 기기마다 GUID가 달라질 수 있다 — 이 Task 범위가 아니며 보고만 한다(CLAUDE.md 규칙 6).

---

## 14. 대조 중 바로잡은 인용 (document-manager, HEAD `1f9d048`)

| 전달 내용 | 대조 결과 |
|---|---|
| 「`ExpectedAvailableCount = 11`(스위트 3파일 `== 11` 검사)」 | **4파일**이다 — `SkillZones.cs` 349 · `SupportNewStatuses.cs` 594 · `SupportRegen.cs` 519 · `SupportShield.cs` 357 |
| 「`.meta` 없음 — SupportNewStatuses/SupportRegen/SupportShield」 | 그 셋을 포함해 19개(13절) |
| 「`CameraController.cs:294` 조준 중 드래그 팬 억제 · 2터치 시 팬 비활성(302)」 | 맞다 — 294 `if (SkillAimController.IsAiming)`, 2터치 가드는 301~306(`activeTouches.Count >= 2` 302) |
| 스키마 상수 위치 | `UnitAttackShadowProfileResolver.cs` **41**행(지원 스킬 새 상태 Task Research 5절의 37행은 그때 HEAD 기준) |

그 밖의 행 번호(6 · 9 · 10 · 11절 [확인] 표시)는 HEAD `1f9d048`에서 전달된 범위와 일치했다.

---

## 15. 이번 조사에서 확인하지 않은 것

- ~~`CameraController.HandleZoom` 본문 전체(터치 핀치 분기 232~)와 핀치 줌 계산 방식~~ — 2차 결정 반영 때 232~247을 읽었다(7절). 남은 것: 두 손가락 팬을 더한 뒤 DOTween 줌 보간(`_zoomTween`)과 팬이 섞일 때의 느낌은 실기에서 본다.
- ~~`InputHandler`의 탭 판정이 두 손가락 · 오른쪽 버튼 제스처에 반응하는지.~~ — 탭 판정 코드는 3차 때 읽었다(7절). 오른쪽 버튼은 `leftButton`만 보므로 반응하지 않는다 [확인 — 180~190]. 두 손가락 쪽 실제 오발동 여부는 실기 미확인.
- `SkillAimReticle`의 `ApplyVisual`(타원 · 시각 배율)과 머티리얼 · 셰이더 속성 — 직선 표시가 같은 머티리얼을 쓸 수 있는지.
- ~~조준 중 두 번째 손가락이 닿을 때 지금 `WasPointerPressedThisFrame` · `TryGetPointerScreenPos`(479~)가 어느 손가락을 읽는지.~~ — 첫 손가락(`primaryTouch`)만 읽는다(7절). 두 손가락 중 첫 손가락을 먼저 뗄 때 `primaryTouch`가 어떻게 보고되는지는 실기 미확인(Plan R-16 · LS-T13 ④).
- AI 종족 · 유닛 구성(싱글 테스트에서 어떤 적 유닛이 나오는지) — 테스트에는 「AI 유닛 · 건물이 직선 안에 있으면 된다」만 쓴다.
- 늦은 접속 Client가 스키마 확인을 거치는지(TDD_Network 40행이 「코드를 읽지 않았다」고 적은 범위 그대로).

---

## 16. 변경 이력

| 날짜 | 내용 |
|---|---|
| 2026-11-17 | 첫 판 — 조사 에이전트 HEAD `1f9d048` 조사 결과와 메인 세션이 정리한 사용자 결정(2026-11-17)을 바탕으로 작성. document-manager가 6 · 9 · 10 · 11절 행을 같은 커밋에서 대조, 스위트 사용 가능 수 고정 파일 수(3 → 4)와 `.meta` 누락 범위(3 → 19)를 바로잡음(14절) |
| 2026-11-17 | 2차 사용자 결정(U-11~U-14) · 메인 세션 기본안(M-1 · M-2) 반영 — 머리 설명 · 현재 상태 · 1절 범위 · 4절 16 답 · 6절 진입 표시 · 7절(핀치 줌 232~247 · 오른쪽 버튼 사용 0건 · 조준 컨트롤러가 `primaryTouch` · `leftButton`만 읽음 [확인]) · 12절 카메라 · 타일 선택 행 · 15절 갱신 |
| 2026-11-17 | 3차 사용자 결정(U-15~U-18) 반영 — 머리 설명 · 현재 상태 · 7절(첫 손가락 떼면 발동 → U-16, `primaryTouch` 오염 · `4e5da5e` 미확인, 조준 원 월드 크기 142 · 189~198, 팬 기준점 `_dragOrigin` 349, `InputHandler` 탭 10px 102 · 206~211 [확인]) · 12절 타일 선택 행 · 15절 갱신 |

---

## 주요 파일

- `Assets/_Project/Scripts/Presentation/Input/SkillAimController.cs`
- `Assets/_Project/Scripts/Presentation/Camera/CameraController.cs`
- `Assets/_Project/Scripts/Presentation/Effects/SkillAimReticle.cs` · `TowerRangeIndicator.cs` · `EffectManager.cs` · `VfxPoolItem.cs`
- `Assets/_Project/Scripts/Presentation/UI/BuildingSkillPanelUI.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkSkillController.cs` · `NetworkCombatController.cs` · `UnitAttackShadowProfileResolver.cs`
- `Assets/_Project/Scripts/Application/UseCases/SkillActivationUseCase.cs` · `UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Application/Skill/SkillActivationContext.cs` · `SkillActivationPresentation.cs` · `SkillExecutorRegistry.cs`
- `Assets/_Project/Scripts/Domain/Skill/SkillMechanicType.cs` · `SkillAimType.cs`
- `Assets/_Project/Resources/Config/Skills/Skill_Human_AirStrike.asset`
- `Assets/Editor/Setup/SupportSkillLoadoutSetup.cs`
- `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.SkillZones.cs` · `.SupportNewStatuses.cs` · `.SupportRegen.cs` · `.SupportShield.cs` · `.ServerProjectiles.cs` · `.SupportBuffSkills.cs`
