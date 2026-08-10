# Hexiege - 작업 로드맵

**최종 수정일:** 2026-08-10
**기획 확정 (2026-08-10) — 구현 미착수:** **MistShrine(HealShrine) 물안개 힐 재설계 확정.** **문서만 확정했고 코드·프리팹·전용 패널 UI·범위 표시 UI·물안개 VFX는 전혀 구현되지 않았다(과대 표기 금지).** MistShrine(`BuildingType.HealShrine` = 6)은 공격하지 않는 **아군 힐 전용 건물**로, 시전하면 **건물 중심 고정 원형 범위**(조준 없음)에 물안개가 깔려 지속 동안 **아군 유닛 + 아군 건물**(시전 건물 자신·Castle 포함)을 **1초 discrete 틱**으로 회복한다. 최대 체력 대상은 회복 없음, **범위 이탈 시 즉시 끊기는 아우라**, **물안개 지속시간 < 쿨다운**(다운타임 존재), 시전 비용 없음, 시전 건물 파괴 시 물안개 즉시 제거, **물안개끼리는 중첩 금지**(더 가까운 건물 우선 · 거리 동률이면 건물 Id가 작은 쪽), **연구소 자연회복과는 별개 효과로 중첩 적용**. UI는 `BuildingPanelBase` 상속 전용 패널(탭=수동 시전 / 롱프레스=자동 모드 토글, **자동 기본 OFF**, `SkillCooldownOverlay` 재사용, 범위 표시는 아군·패널 열린 동안만). 로직은 `SkillActivationUseCase` 재사용 불가로 **전용 UseCase 신설**(쿨다운 패턴만 차용 + 클라 로컬 미러). **함께 정정한 문서 오류:** GDD/TDD가 Transcendence 방어 타워를 MistShrine으로 적어 왔으나 **정확히는 VineTower**(`AutoTower` = 2)이며 MistShrine은 별개 건물이다. **수치(회복량·물안개 지속시간·쿨다운·범위 반경·회복 텍스트 표시 주기)는 전부 밸런싱 미확정** — 아래 C-1 표 참조. 상세: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템**(규칙 1~27), `PROJECT_STATUS.md` "📐 확정 설계 — 구현 예정", task `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`.
**버그 수정 (2026-08-08):** **랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 · 실기 테스트 PASS**(커밋 `9a19cd5`). 랠리포인트 버튼 탭 시 팝업만 숨겨지고 공유 BlockingOverlay(탭=`Close`)가 잔존해 맵 탭을 가로채면서 집결지 지정이 취소된 것처럼 보이던 버그. `ProductionPanelUI.cs` **1파일 2줄 순수 추가** — ① `OnRallyPointClick()`에 `HideBlockingOverlay()`(스킬 패널 조준 진입과 동일 패턴) ② `Close()`를 거치지 않는 `CompleteRallyPointSetting()`에 참조 카운터 반납(②를 빠뜨리면 다음 팝업 오버레이가 어긋남 — 기존엔 이 버그가 대신 `Close()`를 태워 우연히 상쇄). 랠리포인트 규칙 2의 "설정 직후 3초 표시"도 정상화. 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5 — **규칙 문서 변경 없음**(코드가 기존 규칙을 다시 준수하도록 맞춘 수정). task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`.
**구현 완료 (2026-08-08):** **건물 파괴 시 열린 패널/조준 UI 원복** **구현 완료 · 실기 테스트 PASS**(커밋 `8c7fa01`). 건물 패널 4종(생산/건물액션/스킬/연구)이 열려 있거나 스킬 조준 중일 때 그 건물이 파괴되면 유령 패널·조준 UI가 남던 갭 해소. 공통 베이스 `BuildingPanelBase`가 `GameEvents.OnBuildingDied` 구독 → 파괴 건물이 현재 표시/조준 중인 건물(`_currentBuilding.Id` 매칭)이면 `Close()` → 각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계. **코드 변경 `BuildingPanelBase.cs` 1파일**(자식 4개 패널 무변경). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행으로 커버. **교훈: 자식(`ResearchPanelUI`)이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy` 은닉(hide)으로 해제 누락 → `.AddTo(this)`(UniRx)로 컴포넌트 수명에 묶음.** task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`.
**구현 완료 (2026-08-05):** 스킬 시스템 **타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2** **구현 완료 · 실기+멀티(클라) 테스트 PASS**. 스킬 프레임워크의 마지막 메커니즘 타입 — 버프·디버프·제어(둔화·빙결)·회복을 하나의 상태변경 시스템으로 표현. Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}` + Application `Services/StatusEffectSystem`(서버 권위 부여/틱) + `Skill/GlobalStatusChangeExecutor`(조준 없음 전역 즉시) 신설. 유효 스탯 접근자에 상태 배율 곱연산 합성 + 공격 게이트 `CanAttack`(무상태면 회귀 안전). 빙결=`Animator.speed=0` 애니 정지·클라 동기화, 둔화=이동 코루틴 매 프레임 배율 재조회로 라이브, 회복=HoT 재사용, 멀티=`StatusAppliedClientRpc`. **정리:** 진단 로그 코드 제거(LogRules 준수, `IRuntimeLogSink`/`RuntimeLoggerSink` 삭제)·좌표화 주석 비활성 코드 3곳 삭제. **미완:** ~~건물 파괴 시 UI 원복~~(→2026-08-08 완료·위 참조)·구체 스킬 목록/수치/아이콘(기획). 상세: `GameSystemRules/GameSystemRules_Skills.md`, `_Tasks/2026-07-28/12_14_skill-building-system-design/`.
**구현 완료 (2026-08-04):** 스킬 건물 시스템 Phase 1(타입 A 즉발 범위 피해 · 타입 B 장판 DoT + 프레임워크 골격 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 렌더링 + 취소 버그·쿨다운 토스트) **구현 완료 · 실기기 테스트 PASS**. 조준 중심 타일 스냅(HexCoord) → 연속 도메인 월드 Vector3 좌표화(**착탄 반경 판정은 원래 연속 원이라 무변경, 중심만 연속화** + 서버 재검증 HasTile → 맵 경계 안 점, `HexMetrics` 경계 헬퍼 신규·최외곽 타일 바깥선 엄밀 clamp). 조준원 coplanar z-fighting → 신규 셰이더 `SkillAimOverlay.shader`(ZTest LEqual + Offset -1,-1 + ZWrite Off) 지면 데칼. 실기 취소 버그(손 뗀 프레임 합성 마우스 좌표가 캐시 폴백 가로챔) → release 프레임 캐시 좌표만 판정으로 근본 수정. 쿨다운 중 탭 무시 → `ToastKey.SkillOnCooldown` 안내. **미완:** 타입 C(전역 상태변경) Phase 2·건물 파괴 시 UI 원복·구체 스킬 기획. 상세: `GameSystemRules/GameSystemRules_Skills.md`, `_Tasks/2026-07-28/12_14_skill-building-system-design/`·`_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`.
**구현 완료 (2026-07-31):** 연구소 유닛 강화 시스템(공/방/속 + 초월 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI **구현 완료 · 멀티플레이 실기 PASS**. ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨). 후속 보류: UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐·싱글 자연회복 실기. 상세: `GameSystemRules/GameSystemRules_Upgrade.md`, `StatsReference.md`, `_Tasks/2026-07-22/10_08_unit-upgrade-system/`.
**현재 단계:** MistShrine(HealShrine) 물안개 힐 **재설계 기획 확정 · 구현 미착수**(2026-08-10, 문서 반영만 완료 — 위 항목 참조. 구현 시 전용 UseCase·전용 패널 UI·아군 수집 헬퍼·건물 회복 경로·물안개 VFX가 전부 신규 필요하며 수치는 밸런싱 미확정). 직전 스킬 건물 시스템 Phase 1(타입 A·B + 프레임워크 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 + 취소 버그·쿨다운 토스트) 구현·실기기 테스트 PASS(2026-08-04)에 이어 타입 C(전역 상태변경) Phase 2도 구현·실기+멀티(클라) PASS(2026-08-05)로 스킬 메커니즘 3종(A/B/C) 전부 완료. **직전 건물 파괴 시 패널/조준 UI 원복 구현·실기 PASS(2026-08-08, `BuildingPanelBase` OnBuildingDied 구독→현재 건물 매칭 시 Close, 4개 건물 패널 공통 커버).** 스킬 관련 다음 우선순위: 구체 스킬 목록·수치·아이콘(기획, ScriptableObject 확정 — 현재는 플레이스홀더 5슬롯 테스트용). 직전 QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료(2026-07-20, QA PASS·**멀티플레이 실기 로그 검증 완료**) — 마지막 남은 특수 유닛으로, **특수 유닛 5종(BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/QuakeSpirit) 전량 + InfernoSpirit DoT까지 전부 완료**. 착탄형 **즉발** AoE(DoT 아님): 주 타깃 1마리 직접 100%(공격력 200, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 기본 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 아군 무피해, 서버 권위. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼(`internal static` 공용화)+건물 순회 신설+유닛/건물 hit-set 분리(규칙 29), `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5). 멀티 로그로 전 항목 정상 검증(로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`). 규칙 43. 직전 InfernoSpirit(지옥불 정령) 단일 대상 DoT 구현 완료(2026-07-20) — 이미 완성된 원거리 유닛에 DoT 특수만 추가(주 타깃 1마리 직접 250 + 그 1마리에게만 DoT 50/초×3초, AoE 아님). 규칙 40 초 단위 틱을 단일 대상 재사용, 별도 진입점(`ApplyInfernoDot` 50/3) 분리. 규칙 41~42. **알려진 이슈(보류, 별도 task 분리)**: ① QuakeSpirit 타격 애니↔데미지 텍스트 타이밍 어긋남(Attack 클립 `OnAttackHit` 미주입+placeholder hitFrameTimes → 스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상), ② 멀티 원거리 공격 facing 버그(에셋 아닌 공유 회전 로직 추정). 후속(Phase F): 기기 QA로 3D 텍스처 품질 확인, 피격 VFX 프리셋 연결, QuakeSpirit `OnAttackHit` 클립 이벤트 주입(타이밍 이슈 해소), 멀티 원거리 facing 버그 수정, Firebase/EDM 저장소 방침 정리, AI 반응 시스템·시나리오 정밀 검증 + 잔여 신규 유닛 프리팹 실기 테스트. **병행 관찰 항목 — 매치메이킹 404(호스트 결정 단계) 수정(A방식, 2026-07-17, 브랜치 `claude/matchmaker-404-error-pi9qdn` 커밋 `a3dbc73`)**: 랜덤 매칭 후 호스트 결정을 매치 결과 조회(P2P 클라 호출 시 404) → Lobby CreateOrJoin(matchId=lobbyId) 원자 선점으로 전환. 초기 매칭 실기에서 404 없이 정상 연결 확인했으나 간헐(intermittent) 버그라 지속 테스트 중(확정 PASS 아님) — 비활성화(주석)한 레거시 코드는 지속 테스트 확정 후 삭제 예정. 상세는 우선순위 표 및 Phase A-2 참조. **※ 이 문단의 전투 수치(직접 피해·DoT 틱값)는 2026-08-10에 ×10 스케일 개편(2026-07-31) 기준으로 정정했다 — 비율·반경·쿨다운은 ×10 대상이 아니라 그대로이며, 단일 진실 소스는 [StatsReference.md](StatsReference.md)다(우선순위 표 하단 ※ 참조).**
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| 🔵 초기 정상·지속 관찰 중 | 매치메이킹 404(호스트 결정 단계) 수정 — 호스트 결정을 매치 결과 조회(P2P 클라 404) → Lobby CreateOrJoin 원자 선점(A방식)으로 전환 (2026-07-17, 커밋 `a3dbc73`). 초기 실기 정상, 간헐 버그라 지속 멀티 실기 검증 필요 | QA/버그 | 중 |
| ✅ 완료 | 코드 리팩토링 7개 그룹 전체 | 아키텍처 | 대 |
| ✅ 완료 | 코드 정리(클린업) Phase 1 — 히스토리성 주석/폐기 코드 제거 (약 30개 파일) | 코드 정리 | 소 |
| ✅ 완료 | 코드 구조 개선 Phase 2 — switch→Dictionary lookup table(BuildingTypeHelper) + HexMetrics 중복 setup 제거 (2026-06-25) | 코드 정리 | 중 |
| ✅ 완료 | GameBootstrapper.Setup.cs 하드코딩 배열 파생 — 환불 캐시의 stage1Buildings/nonProductionBuildings 하드코딩 배열을 BuildingTypeHelper 공개 API 파생으로 교체. 신규 건물 추가 시 `_buildingTable` 한 줄로 환불 캐시 자동 반영 (2026-06-25) | 코드 정리 | 소 |
| ✅ 완료 | IUnitFactory 인터페이스 도입 — IGameServices.GetUnitFactory() 반환 타입을 UnitFactory(Infrastructure) → IUnitFactory(Application)로 변경. Application → Infrastructure 역방향 의존 제거 (2026-06-26) | 아키텍처 | 소 |
| ✅ 완료 | 로그인 시스템 C# 구현 (Firebase Auth + GPGS) | 기능 | 중 |
| ✅ 완료 | 게임 화면 UI TC 62개 실기기 테스트 + END UI 버그 수정 | QA/버그 | 중 |
| ✅ 완료 | BuildingPlacementUI 씬 계층 재설계 (BP-001/BP-002 해결) | UI | 중 |
| ✅ 완료 | 패널 버튼 크기 불일치 수정 (PRD-001, BAP-001) — LayoutElement 균등화 | UI | 소 |
| 🔵 조건부 완료 | AI 시스템 — 코드 + Inspector 작업(AIConfig/Scenario 3종족 에셋 생성, DifficultySelectView GO 배치) 완료. 핵심 흐름(유닛 생산/건물 업그레이드) 실기 조건부 완료(2026-07-16, PASS·특별한 문제 미발견). 후속: 반응 시스템(R1~R3)·3종족 시나리오 무작위 동작 정밀 검증 | 기능 | 대 |
| 🔴 높음 | AI 시스템 — 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴) 트리거·동작 + 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 정밀 실기 검증 (핵심 흐름은 확인됨, 세부 정밀 검증만 잔여) | 기능 | 소 |
| 🔴 높음 | 신규 유닛 프리팹 실기 테스트 + 후속 작업 (Animation Event 부착, UnitFactory 등록, StatsReference 스탯 확정) | 기능 | 대 |
| 🔴 높음 | 게임 화면 UI 크기/레이아웃 수정 잔여 (HUD-007, SET-004, SET-007/END-001, MULTI-END-002 — 5항목) | UI | 소 |
| 🔴 높음 | FlatTop 11×21 무작위 대전 맵 구현 — 5종 생성 전략, exact 180° 대칭, 광산/초기 골드, 초기 골드 전용 테스트 모드(멀티 Host 권위), 건설 불가·차단 지형, 결정적 seed/검증/폴백, canonical chunk 전송, SameMap/NewMap, 경로 완전 차단 대응 (설계 확정·문서 동기화 2026-07-20) | 기능/맵 | 대 |
| ✅ 완료 (2026-08-04, 실기 PASS) | 스킬 건물 시스템 Phase 1 — 타입 A(즉발 범위 피해)·B(장판 DoT) 실행기 + 데이터 주도 프레임워크(SkillData/Executor/Registry/SkillActivationUseCase, 건물 글로벌 쿨다운) + 3×3 패널 UI/쿨다운 오버레이(12시 시계방향 소멸) + 모바일 탭 조준(2단계, 엣지 스크롤, 취소 X Lerp 확대) + 조준 지점 연속 좌표화(HexCoord→Vector3, **반경 판정 무변경·중심만 연속화**) + 맵 경계 point-in-bounds 재검증(`HexMetrics` 헬퍼 신규) + 조준원 지면 데칼 렌더링(`SkillAimOverlay.shader`) + 취소 버그 근본 수정 + 쿨다운 안내 토스트. 서버 권위(`NetworkSkillController`). 규칙 1~26. 미완: 타입 C·건물파괴 UI 원복·구체 스킬 기획 | 기능 | 대 |
| ✅ 완료 (2026-08-05, 실기+멀티 PASS) | 스킬 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 — 전역 즉시 발동(조준 없음), 상태효과 시스템(`StatusEffectSystem`, 서버 권위 부여/틱) + 실행기(`GlobalStatusChangeExecutor`) 신설, 유효 스탯 접근자에 상태 배율 곱연산 합성 + 공격 게이트 `CanAttack`(무상태 회귀 안전), 빙결 애니 정지(`Animator.speed=0`)·둔화 라이브·회복 HoT 재사용, 멀티 `StatusAppliedClientRpc` 동기화. 업그레이드 연동은 곱연산 합성으로 확정. 진단 로그 제거(LogRules 준수). 규칙 13 | 기능 | 대 |
| ✅ 완료 (2026-08-08, 실기 PASS) | 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 — 랠리포인트 버튼 탭 시 팝업만 숨겨지고 공유 BlockingOverlay(탭=`Close`)가 `blocksRaycasts=true`로 잔존해 맵 탭을 가로채면서 집결지 지정이 취소된 것처럼 보이던 버그. `ProductionPanelUI.cs` 1파일 2줄 순수 추가(① `OnRallyPointClick()` 오버레이 해제 ② `Close()` 미경유 `CompleteRallyPointSetting()` 참조 카운터 반납). 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5(단일 소유·참조 카운터), 랠리포인트 규칙 2 "설정 직후 3초 표시" 정상화. 커밋 `9a19cd5` | QA/버그 | 소 |
| ✅ 완료 (2026-08-08, 실기 PASS) | 건물 파괴 시 패널/조준 UI 원복 — 스킬 포함 4개 건물 패널 공통 갭(파괴돼도 열린 패널·조준 락 잔존) 해소. `BuildingPanelBase`가 `OnBuildingDied` 구독→현재 건물(`_currentBuilding.Id`) 매칭 시 `Close()`(각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계). 코드 변경 `BuildingPanelBase.cs` 1파일, 구독 해제=`.AddTo(this)`(자식 `ResearchPanelUI` 자체 `OnDestroy` 은닉 회귀 회피). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행 커버. 커밋 `8c7fa01` | QA/버그 | 소 |
| 🟡 중간 (미착수) | **MistShrine 물안개 힐 시스템 구현** — 2026-08-10 재설계 **기획 확정 / 구현 미착수**(코드·프리팹·UI·VFX 전부 없음). 건물 중심 고정 원형 범위(조준 없음)에 물안개 생성 → 지속 동안 1초 discrete 틱으로 **아군 유닛 + 아군 건물**(자기 자신·Castle 포함) 회복, 범위 이탈 시 즉시 끊기는 아우라, 지속 < 쿨다운, 시전 비용 없음, 파괴 시 즉시 제거, 물안개 간 중첩 금지(가까운 건물 우선·동률이면 Id 작은 쪽), 연구소 자연회복과는 중첩 적용. 신규 필요: 전용 UseCase(`SkillActivationUseCase` 재사용 불가) · `BuildingPanelBase` 상속 전용 패널(탭=수동/롱프레스=자동, 자동 기본 OFF) · 아군 유닛·건물 수집 헬퍼(기존은 전부 적 대상 전용) · 건물 회복 경로(`BuildingData`에 회복 메서드 없음) · 파괴/철거 정리 경로 · 물안개 VFX. **수치는 전부 밸런싱 미확정**(C-1 표 참조). 규칙 1~27: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) | 기능 | 중 |
| 🟡 중간 | 스킬 건물 구체 스킬 목록·수치 확정(기획) — 각 슬롯 1~5 스킬·쿨다운·반경·지속·피해·상태효과를 ScriptableObject 데이터로 확정. 개별 스킬 쿨다운 도입 여부·회복 스킬 지점 지정 전환 여부 포함 | 기획 | 중 |
| ✅ 완료 (2026-07-31, 멀티 실기 PASS) | 연구소 유닛 강화 시스템 + 전투 스탯 ×10 개편 + 연구 패널 UI — 공/방(신규)/속 3스탯 + 초월 자연회복, 종족별 그룹×스탯 트랙, (B) 팀 배율 실시간 소급 적용, 방어력 감쇄(K=120), 연구소 운영(복수 건설·트랙 잠금·파괴 100% 환불·서버 권위), 전투 수치 ×10(TTK 불변), 매트릭스 2-레이어 UI. 후속 보류: UI 레이아웃·헤더 아이콘·AI 연구·MistShrine 힐·싱글 자연회복 | 기능/기획 | 대 |
| 🔴 높음 | 전역 GameProtocolVersion/build 호환성 관리 — matchmaking 동일 버전 필터, custom lobby Relay 전 검사, NGO connection approval/rejection, reconnect 버전 검증, update-required UX | 네트워크/호환성 | 대 |
| ✅ 완료 | 전역 UI 시스템 (UIManager + SplashOverlay) | UI | 중 |
| ✅ 완료 | Firebase Console 설정 (Google 로그인) — SHA-1 정합 + Play Games 제공업체 활성화, 실기 로그인 성공 (2026-06-27) | 인프라 | 소 |
| 🔴 높음 | Login.unity 씬 로그인 UI 조립 | UI | 소 |
| 🔴 높음 | UGS OIDC 브릿지 활성화 — UGS Dashboard OIDC 제공자(`oidc-firebase`) 등록 (현재 `id provider not found`로 멀티플레이 제한) | 인프라 | 소 |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 (멀티) | UI | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 비주얼 폴리싱 (에셋 제작 완료 2026-05-30) | UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| ✅ 완료 | 방어 타워(AutoTower) 공격 기능 | 기능 | 대 |
| ✅ 완료 | 사운드 시스템 — 코드 완료 + Inspector 작업 + 실기 버그 3종(BGM 겹침/볼륨 UI 규칙/SFX 진단) 수정 (2026-07-08) | 기능 | 중 |
| ✅ 완료 | 사운드 시스템 — 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 (AudioManager 음소거 + 공용 VolumeControlBinder + 로비 설정 탭 배선 + 실기 버그 3건 수정, 2026-07-09 실기 PASS) | 기능/UI | 중 |
| ✅ 완료 | 전투 타격 타이밍 동기화 — 타격 프레임 클립 이벤트 단일 소스화 + 서버 Tick 정밀화 + 피격 표현 큐 + 타워 발사 VFX/원거리 트레이서 + 기존 버그 3건 수정 (2026-07-12 실기/로그 검증 PASS) | 기능 | 대 |
| ✅ 완료 | 이동/Walk 애니메이션 동기화 (2026-07-13) — 애니메이션 상태를 엣지 RPC → NetworkVariable 레벨 동기화 전환(스폰 레이스 유실 소멸) + Initialize 후 재적용 봉합 + 재경로 첫 스텝 역방향 보정. 규칙 U-22 등재 | QA/버그 | 중 |
| ✅ 완료 | 죽은 코드 제거 — `UnitView.StopMovement()` 미사용 메서드 삭제(호출 0건, 커밋 8840798) (2026-07-13) | 코드 정리 | 소 |
| ✅ 완료 | Animator 상태 의존 제거 — 전투 종료 후 Walk 재개 3곳의 `GetCurrentAnimatorStateInfo` 질의 → 로컬 추적 필드 기반 판별 리팩토링(겉보기 동작 불변, 커밋 97adaad) (2026-07-13) | 코드 정리 | 소 |
| ✅ 완료 | Firebase 인증 게이트 제거 — `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 스텁이 로그인 무조건 실패시키던 버그 수정, 실제 Firebase 코드 무조건 컴파일 복원(커밋 4fe1cf0) (2026-07-13) | QA/버그 | 소 |
| ✅ 완료 | 이메일 인증 플로우 보정 — 실제 이메일 표시, 가입 취소 시 미인증 Firebase 계정 삭제, 앱 재실행 시 인증/닉네임 게이트 복귀, Lobby 게스트 우회 차단 (2026-07-18 실기 PASS) | QA/버그 | 중 |
| ✅ 완료 | 빌드 에셋 용량 최적화 — Android AAB 190.66 MB → 125.30 MB 절감(2026-07-15). 3D 건물/유닛 텍스처 Android max size 512 적용, 미사용 `_Old` 에셋/normal/roughness PNG 정리, FBX import 조정 | 플랫폼 | 중 |
| ✅ 완료 | 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 전략 핸들러 아키텍처 — 특수 유닛 5종 중 첫 구현. 월드 좌표 전방 부채꼴 판정 + `SpecialAttackConfig` 튜닝 SO + `ApplyDamageToVictim` 피해 수렴점 단일화 + AoE 연출 동시 방출 + BattleAxe 스탯/클립 이벤트 확정 (2026-07-17 실기 PASS) | 기능 | 중 |
| ✅ 완료 | TorrentSpirit 파도형 이동 AoE + 힐 서브시스템 — 특수 유닛 5종 중 2번째. special-only(`ReplacesPrimaryAttack`) 서버 권위 이동 파도(`TickWaves`, 월드 직사각형, 적 유닛·건물 피해/아군 힐 각 1회) + 힐 서브시스템(`UnitData.Heal`/`OnEntityHealed`/NetworkHealthSync 힐 동기화/치유 텍스트, BloomFairy 공용) + `VfxPoolItem` 다중 파티클 재생 수정 + 데이터 배선(스탯/EffectPreset/OnAttackHit). QA CONDITIONAL PASS(BUG-002 건물 공격 수정). 규칙 28~31. VFX 튜닝은 사용자 진행 (2026-07-17) | 기능 | 중 |
| ✅ 완료 | BloomFairy(꽃요정) 힐러 — 특수 유닛 5종 중 3번째. 힐러 전용 경로(적 공격 흐름·레지스트리 분리) + 부상 아군 탐색(팀 필터 반대·본인 포함) + HoT/DoT 공용 시간 지속 효과 시스템(서버 권위 diff 틱, 규칙 34) + 힐 유휴 감시 + 쿨다운 예외(발동 준비 1.0s 미포함 → 실제 힐 주기 4.0s, 프로젝트 유일) + HoT 힐 텍스트 완료 시 1회 표시(사망 시 생략). 규칙 32~37. 실기 완료 (2026-07-18) | 기능 | 중 |
| ✅ 완료 | MushroomBomber(버섯폭격기) 착탄형 범위 DoT — 특수 유닛 5종 중 4번째. 착탄 중심 월드 원형 반경(`BlastAttackBehavior`, arc 없는 도끼병 부채꼴, QuakeSpirit 재사용 헬퍼) 내 적 유닛 DoT + 주 타깃 직접 100(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성). DoT 초 단위 discrete 틱 모드 신설(규칙 34 확장 — 1초 간격·틱당 올림·총량 클램프·매초 데미지 텍스트). 식물 라인 생산 배선(SporePatch=MushroomBomber, FloralNursery=BloomFairy). 규칙 38~40. QA PASS·실기 완료 (2026-07-19) | 기능 | 중 |
| ✅ 완료 | InfernoSpirit(지옥불 정령) 단일 대상 DoT — 특수 유닛 5종 외 추가 DoT 유닛. 이미 완성된 원거리 유닛에 DoT 특수만 추가: 주 타깃 1마리 직접 250(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성) + 그 주 타깃 1마리에게만 DoT 50/초×3초(총 150, AoE 아님·반경 없음, 적 유닛만). 규칙 40 초 단위 틱을 단일 대상으로 재사용, 값은 별도 진입점(`ApplyInfernoDot` 50/3)으로 분리(MushroomBomber 20/3 회귀 없음). `SpecialAttackConfig` inferno DoT 필드 + GameBootstrapper 주입. 레지스트리 1줄. 규칙 41~42. 공격 방향(facing) 버그는 사용자 결정으로 보류(별도 작업). QA PASS·실기 완료 (2026-07-20) | 기능 | 소 |
| ✅ 완료 | QuakeSpirit(대지의 정령) 착탄형 즉발 AoE — **특수 유닛 5종 마지막(전량 완료)**. 착탄형 즉발(DoT 아님): 주 타깃 1마리 직접 100%(공격력 200, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용(유닛만 로직 무변경)+건물 순회 `CollectEnemyBuildingsInRadius` 신설+유닛/건물 hit-set 분리(규칙 29). `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5) 주입. 규칙 43. 타격 타이밍 어긋남(OnAttackHit 미주입)은 사용자 결정으로 별도 task 보류. **QA PASS·멀티플레이 실기 로그 검증 완료** (2026-07-20, 로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`) | 기능 | 중 |
| 🟡 중간 | 피격 VFX 프리셋 연결 — `UnitEffectConfig.hitPreset` Inspector 배선(Human 6종 등 미연결) | 에셋 | 소 |
| 🟢 낮음 | QuakeSpirit Attack 클립 `OnAttackHit` 이벤트 주입 — 특수 공격 로직(규칙 43)은 구현·검증 완료됐으나 클립 `OnAttackHit` 미주입 + placeholder `hitFrameTimes`(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상). 인젝터로 실제 타격 프레임 주입 시 해소. 사용자 결정으로 별도 후속 task. (특수 공격 핸들러 확장 완료: BattleAxe·TorrentSpirit 2026-07-17, BloomFairy 2026-07-18, MushroomBomber 2026-07-19, InfernoSpirit·QuakeSpirit 2026-07-20) | 기능/에셋 | 소 |
| 🟡 중간 | 멀티플레이 원거리 공격 방향(facing) 버그 — 원거리 유닛이 공격 시 타겟을 정확히 안 바라봄. 진단상 에셋 아닌 멀티 원거리 facing 공유 회전 로직(서버 계산+NetworkTransform) 문제로 추정(근접 유닛 미노출). 수정 시 근접 유닛 회귀 주의. 상세: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md` (InfernoSpirit 작업에서 진단·보류) | QA/버그 | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

> **※ 2026-08-10 ×10 스케일 반영 정정:** 위 표의 **MushroomBomber · InfernoSpirit · QuakeSpirit** 행과 상단 **"현재 단계"** 문단에 남아 있던 ×10 개편(2026-07-31) **이전** 전투 수치(직접 피해·DoT 틱값)를 현재 값으로 정정했다. **비율·반경(100% / 50% / `×0.5` / 반경 1.0)과 쿨다운·사거리·이동속도·골드 비용은 ×10 대상이 아니므로 그대로다.** 날짜가 박힌 완료 이력(Phase D-1 방어 타워 완료, Phase F-4 클립 이벤트 주입 이력 등)은 **당시 기록이므로 원문을 보존**한다. 수치의 단일 진실 소스는 [StatsReference.md](StatsReference.md)다.

---

## Phase A — 네트워크 버그 수정

### ✅ A-1. BuildFailed/EnqueueFailed UI 피드백 (멀티플레이 분기) — 완료 (2026-05-24)
- **완료 내용**: `GameEvents.OnToastRequested` Subject 패턴 도입. NetworkBuildingController / NetworkProductionController RPC 핸들러에서 Subject 발행 → ToastUI 구독. Presentation이 Infrastructure를 직접 참조하지 않는 구조 완성.
- **파일**: `NetworkBuildingController.cs`, `NetworkProductionController.cs`, `GameEvents.cs`, `ToastKey.cs`

### 🔵 A-2. 매치메이킹 404 수정 — 호스트 결정 Lobby CreateOrJoin 전환 (A방식) — 초기 정상·지속 관찰 중 (2026-07-17)
- **증상**: 랜덤 매칭은 성사되나 **직후 호스트 결정 단계에서 HTTP 404** → 게임 연결 끊김.
- **원인**: `MatchmakerManager.DetermineIsHostAsync` 내부 `GetMatchmakingResultsAsync`가 전용 서버(Multiplay)용 서버 지향 API인데 P2P(Relay) 클라이언트가 호출 → 조회 대상 리소스 없어 404. 매칭 자체는 정상, 호스트 결정 단계만 실패.
- **해결(A방식)**: 호스트 결정을 매치 결과 조회 → **Lobby CreateOrJoin 원자 선점**으로 전환. 모든 플레이어가 같은 `matchId`를 `lobbyId`로 `CreateOrJoinLobbyAsync` 호출 → 없으면 생성=호스트 / 있으면 참가=클라. 서버 원자 처리로 정확히 한 명만 호스트.
- **변경 파일(3개, Infrastructure/Network)**: `LobbyManager.cs`(추가: `CreateOrJoinLobbyByMatchIdAsync`, `RefreshCurrentLobbyAsync`), `MatchmakerManager.cs`(비활성화 주석: `DetermineIsHostAsync`/`GetStableHash`), `NetworkGameManager.cs`(추가: `StartMatchmadeGameAsync`/`HostMatchmadeGameAsync`/`JoinMatchmadeGameAsync`, `StartMatchmakingAsync` 분기 교체, 구 클라 참가 경로 비활성화 주석). 클라 참가는 CreateOrJoin으로 일원화, RelayJoinCode 채워짐만 폴링 대기(최대 15회).
- **상태**: 초기 매칭 실기에서 404 없이 정상 연결 확인. 단 **간헐(intermittent) 버그라 지속 테스트 중** — 확정 PASS 아님. 비활성화(주석)한 레거시 코드(`DetermineIsHostAsync`/`GetStableHash`, 구 클라 참가 경로)와 미사용 `FindLobbyByMatchIdAsync`의 최종 삭제는 지속 테스트 확정 후.
- **잔여 리스크**: ① SDK 시그니처 에디터 컴파일 최종 확인 권장, ② "정확히 한 명만 호스트"·간헐 재현 지속 멀티 실기(Host+Client) 검증, ③ 클라 RelayJoinCode 대기 15초 타임아웃.
- **브랜치/커밋**: `claude/matchmaker-404-error-pi9qdn` / `a3dbc73`. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/`.

---

## Phase B — 네트워크 미완성 기능

### B-0. 전역 GameProtocolVersion/build 호환성 관리 🔴 높음 (2026-07-19 등록)

- **범위**: matchmaking same-version filter, custom lobby의 Relay 진입 전 호환성 검사, NGO connection approval/rejection, reconnect 시 버전 재검증, update-required UX.
- **경계**: FlatTop 무작위 맵 구현에 종속시키지 않고 모든 멀티플레이 진입·재접속 경로에 공통 적용한다.
- **맵 기능과의 경계**: 무작위 맵은 canonical binary 식별용 임시 `MapVersion(int, 초기값 1)`만 사용하며 unknown map format deserialize 차단과 map prep mismatch 실패만 담당한다. 이 값으로 앱/접속 호환성을 판정하지 않는다.

### B-1. 로비 UI 비주얼 폴리싱
- **현황**: MVVM 코드 완료 (2026-03-15). UI 에셋 제작 완료 (2026-05-30) — 아이콘 13종(탭/기능/로비버튼), 버튼 배경 2종(Primary/Secondary), 스피너 1종(HexOrb)
- **남은 작업**: 제작된 에셋을 로비 씬 Inspector에 연결하여 비주얼 폴리싱 진행

### B-2. 재접속 실제 구현
- **파일**: `Assets/_Project/Scripts/Infrastructure/Network/ReconnectionHandler.cs`
- **현황**: 30초 대기 후 ForceWin만 구현
- **구현 필요**: NGO Reconnect API 활용, 재접속 후 게임 상태 복원

---

## Phase C — 게임플레이 완성도

### C-0. 싱글플레이 AI 시스템
**🔵 코드 구현 완료 (2026-06-07)**: LocalPlayerDifficulty / AIConfig / AIScenarioConfig ScriptableObject / AIOpponentController(빌드오더+반응시스템+BFS) / GameBootstrapper 연동 / 로비 난이도 선택 UI(DifficultySelectView) 전체 완료. 3종족 시나리오 에셋 개편 완료 (2026-06-10): Human/Spirit/Transcendence 각 1개 에셋 × 3시나리오, Domain 레이어 아키텍처 정리.

**✅ Inspector 작업 완료 (2026-07-16)**: AIConfig 에셋 생성, AIScenarioConfig 3종족 에셋 생성, DifficultySelectView 레이아웃 배치 반영(실기 테스트가 진행된 사실로 확인).

**🔵 핵심 흐름 실기 조건부 완료 (2026-07-16)**: 유닛 생산·건물 업그레이드 등 핵심 흐름을 반복 실기로 확인 — PASS, 특별한 문제 미발견.

**남은 작업 (세부 정밀 검증)**:
1. 반응 시스템(R1 유닛열세 / R2 골드과잉 / R3 채굴소 파괴)의 트리거·동작 정밀 검증
2. 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 확인

---

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

**✅ 밸런싱 인프라 완료 (2026-04-25)**: `UnitStatsConfig`, `BuildingStatsConfig` ScriptableObject 전환 완료. 코드 수정·재컴파일 없이 Unity Inspector에서 수치 직접 편집 가능.

**✅ 건물 스탯 전체 확정 (2026-05-18)**: StatsReference.md 기준으로 모든 종족 건물 HP/비용/공격력/쿨다운 확정. BuildingStatsConfig.asset 32종 항목 전부 채움.

| 항목 | 현재값 | 조정 방향 |
|------|--------|---------|
| 시작 골드 | 500 | 테스트 후 결정 |
| 채굴소 수입 | 10골드/초 | 타일 경제 밸런스 체크 |
| Pistoleer HP/공격/사거리 | 300 / 60 / 1.0 | DPS=30, cooldown≈2.0s — 2026-08-10 ×10 스케일 반영 정정 |
| Assault HP/공격/사거리 | 400 / 10 / 2.0 | cooldown≈0.2s ※2 — 2026-08-10 ×10 스케일 반영 정정 (HP 400은 단순 ×10이 아닌 별도 조정값) |
| Sniper HP/공격/사거리 | 300 / 180 / 5.0 | DPS=60, cooldown≈3.0s — 2026-08-10 ×10 스케일 반영 정정 (공격력 180은 단순 ×10이 아닌 별도 조정값) |
| Castle HP (Human `Castle` / Spirit `SpiritNexus` / Trans `ElderTree`) | 2000 / 1500 / 3000 | 확정 — 플레이테스트 후 조정. 2026-08-10 ×10 스케일 반영 정정 |
| AutoTower 공격력/쿨다운 (Human · CannonTower) | 150 / 5.0s | 확정 — 공격력은 ×10 스케일(2026-07-31) 반영값, 쿨다운은 ×10 대상 아님 |
| AutoTower 공격력/쿨다운 (Spirit · RuneSpire) | 150 / 3.5s | 확정 — 가장 강한 타워. 공격력 ×10 반영, 쿨다운 불변 |
| AutoTower 공격력/쿨다운 (Trans · **VineTower**) | 150 / 5.0s | 확정 — 공격력 ×10 반영, 쿨다운 불변. ⚠️ Transcendence 방어 타워는 **VineTower**이며 MistShrine이 아니다(MistShrine은 공격하지 않는 별도 힐 건물 `HealShrine`=6) |
| MistShrine 회복량 / 범위 반경 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 2026-08-10 물안개 힐 재설계로 재산정 대상. 종전 표기값(이 표의 옛 값 1 HP/s는 ×10 이전, `StatsReference.md`의 10 HP/s는 ×10 적용값, 범위 3)은 **모두 재설계 이전 수치**다 |
| MistShrine 물안개 지속시간 / 쿨다운 (Trans · HealShrine) | **미확정** | **미확정(밸런싱 예정)** — 재설계로 신규 필요한 수치. **지속시간 < 쿨다운**(다운타임 필수, 상시 유지 설정 금지) |
| MistShrine 회복 텍스트 표시 주기 (Trans · HealShrine) | **미확정** (임시 3초) | **미확정(밸런싱 예정)** — 회복 틱(1초)과 표시 주기를 분리. 화면 도배 방지용 임시값 |

> **※ 수치의 단일 진실 소스(SSOT)는 [StatsReference.md](StatsReference.md)다.** 위 표는 밸런싱 논의용 **요약**이므로, 두 문서의 값이 갈리면 **항상 `StatsReference.md`를 따른다**(이 표를 근거로 `StatsReference.md`를 고치지 말 것). 2026-08-10, ×10 스케일 개편(2026-07-31) 이전 값이 이 표에 남아 있던 것을 `StatsReference.md` 기준으로 정정했다 — Pistoleer · Assault · Sniper · Castle HP 4행. ⚠️ **Assault HP(400)와 Sniper 공격력(180)은 단순 ×10이 아니라 별도 밸런스 조정이 반영된 값**이므로, 계산으로 유도하지 말고 `StatsReference.md` 원문을 그대로 옮길 것. 이 표의 이동속도·사거리·쿨다운·골드 비용은 ×10 대상이 아니다.
>
> **※2 Assault 쿨다운 표기 불일치(이번 정정 범위 밖):** 이 표의 `cooldown≈0.2s`는 `StatsReference.md`의 실제 값 `0:17(0:33)` = **0.33초**와 어긋난다. 쿨다운은 ×10 대상이 아니어서 이번(2026-08-10) 정정에서 손대지 않았으며, 어느 쪽이 맞는지 확정한 뒤 정정한다. 확정 전까지 Assault DPS는 재산정하지 않는다.

> **MistShrine 관련 수치 근거:** [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 16**(밸런싱 미확정 표)이 단일 소스다. **기획 확정 / 구현 미착수** 상태이므로 수치 확정 전 구현 착수 시 임시값임을 명시할 것.

---

### C-2. 연구소 유닛 강화 시스템 + 전투 스탯 ×10 개편 + 연구 패널 UI ✅ 구현 완료 · 멀티플레이 실기 PASS (2026-07-31)

연구소(Research 건물)로 팀 단위 유닛을 강화하는 신규 시스템 + 함께 진행하는 전투 수치 ×10 스케일 개편 + 연구 패널 UI. **구현 완료, 멀티플레이 실기 테스트 통과.**

- **전투 스탯 ×10 스케일**: 유닛 HP·공격력, 건물 HP, 타워 HP·공격력, DoT 틱값, 힐량 전부 ×10. HP·공격력 동일 배율로 **TTK 불변**(상성·매치업 불변). ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨)(1회 실행).
- **강화 스탯**: 공격력(유닛별 고정 정수 증가) / 방어력(신규, K=120 감쇄) / 이동속도 배율 + 초월 자연회복(3~15 HP/s). `UnitUpgradeUseCase`가 (B) 실시간 배율/증가치 조회 → 소급 강화.
- **네트워크**: 완료 레벨 양 클라 브로드캐스트(효과 양쪽 적용), 진행 중은 소유자만, 파괴 시 취소·100% 환불. `NetworkUpgradeController`. MP 완료 처리 서비스 스폰 레이스 버그 `ResolveServices()`로 수정.
- **연구 패널 UI**: `ResearchPanelUI : BuildingPanelBase`(헤더·닫기·철거+환불) + 매트릭스/진행 2-레이어(연구소 단위 전환, 진행 트랙 잠금). 규칙 13.
- **후속 보류(미완/미검증 — 별도 작업)**: ① UI 레이아웃 다듬기(자동생성 골격) ② 매트릭스 헤더 아이콘 ③ AI 연구 사용 실기 ④ **MistShrine(HealShrine) 힐 — 2026-08-10 물안개 힐 재설계 기획 확정 / 구현 미착수**(코드·UI·VFX 전부 없음. 규칙: [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템**. 연구소 자연회복과는 **별개 효과로 중첩 적용** — 규칙 14) ⑤ 싱글 자연회복 실기.
- **문서**: `GameSystemRules/GameSystemRules_Upgrade.md`(구현 계약·구현 상태·규칙 13 UI), `StatsReference.md`(×10 값·업그레이드 표), `GameSystemRules_Units.md` 규칙 44(방어력 감쇄), GDD 연구소 섹션. old/new 대조: `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md`·`Research.md`·`Plan.md`(하단 "완료 결과").

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
**✅ 방어 타워 완료 (2026-06-01~02)**: TowerCombatUseCase 신규 구현. 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 서버 권위 처리. Human CannonTower 초기 방향(내 진영 0도/상대 진영 180도) 구현 완료.
- 스킬 건물(FlightFacility / MagicSpirit / WillowShrine): **Phase 1 + Phase 2 구현 완료 · 실기기 테스트 PASS (Phase 1: 2026-08-04, Phase 2 타입 C: 2026-08-05 실기+멀티(클라))**. **마나 미도입** — 자원 없이 건물별 글로벌 쿨다운만 사용. 건물당 최대 5개 스킬, 3×3 패널 UI(슬롯 1~5 스킬/6 철거/7~9 예약), 모바일 탭 조준(2단계·엣지 스크롤·취소 X Lerp 확대), 서버 권위. **타입 A(즉발 범위 피해)·B(장판 DoT)·C(전역 상태변경: 버프/디버프/CC/힐) 실행기 완료**, 조준 지점 연속 좌표화(HexCoord→Vector3, 반경 판정 무변경·중심만 연속화)·맵 경계 point-in-bounds 재검증·조준원 지면 데칼 렌더링(`SkillAimOverlay.shader`)·취소 버그 수정·쿨다운 안내 토스트까지 반영. 타입 C는 상태효과 시스템(`StatusEffectSystem` 서버 권위)+유효 스탯 곱연산 합성+공격 게이트(무상태 회귀 안전)·빙결 애니 정지·둔화 라이브·회복 HoT·멀티 `StatusAppliedClientRpc`. **건물 파괴 시 열린 패널/조준 UI 원복은 2026-08-08 구현 완료·실기 PASS(`BuildingPanelBase` OnBuildingDied 구독→Close, 4개 건물 패널 공통).** **미완: 구체 스킬 목록/수치/아이콘(기획, 현재 플레이스홀더 5슬롯 테스트용).** 상세: [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)(규칙 1~26), task `_Tasks/2026-07-28/12_14_skill-building-system-design/`·`_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`.

### D-2. 건물 업그레이드 시스템
**✅ 완료 (2026-05-17~18)**: 종족별 단계 BuildingType 26종 확장, BuildingPlacementUseCase.UpgradeBuilding(), NetworkBuildingController RPC, ProductionPopup 업그레이드 버튼/아이콘, 철거 환불 누적 계산 완료. 전 종족 테스트 통과.

### D-2-1. 건물 철거 로직 구현
**✅ 완료 (2026-05-18~19)**: BuildingActionPanelUI를 통해 비생산 건물(채굴소/타워 등) 철거 가능. BuildingPanelBase.OnDemolishButtonClick()에 싱글/멀티 분기 공통 구현. 채굴소 전용 패널(일시정지 등)은 별도 작업.

### D-3. 유닛 AI 상태머신
**✅ 기본 전투 AI 구현 완료 (2026-05-11)**: 슬롯/점유 시스템 폐기. 근접·원거리 모두 단일 상태 머신(MoveAlongPathV3) 적용. A* 이동 → 적 감지(DetectRange) → 직선 추격(EnterCombatPursuitV3) → 공격 → 재개(Lerp 정렬) 사이클 완성. 겹침 허용, 모든 유닛 동일 규칙 적용.
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

### D-4. 신규 유닛 프리팹 완성 (16종)
**🔧 에디터 스크립트 완료 (2026-06-05)**: Human 5종(LittleKnight/SpearMan/BattleAxe/Tank/CannonCart)·Spirit 6종(DustSpirit/BoulderSpirit/QuakeSpirit/TideSpirit/StreamSpirit/TorrentSpirit)·Transcendence 5종(RabbitTrickster/RhinoBreaker 등) × Blue/Red 총 32개 프리팹 자동 컴포넌트 부착. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`

**진행 (2026-07-21)**: 특수 유닛 5종 **전량 구현 완료 + InfernoSpirit DoT까지 완료** — BattleAxe(휩쓸기형 부채꼴, 2026-07-17), TorrentSpirit(파도형 이동 AoE + 힐 서브시스템, 2026-07-17), BloomFairy(힐러 전용 경로 + HoT/DoT 공용 시스템, 2026-07-18), MushroomBomber(착탄형 원형 반경 DoT + DoT 초 단위 틱 모드 + 식물 라인 생산 배선, 2026-07-19), InfernoSpirit(단일 대상 DoT — 규칙 40 초 단위 틱 재사용, 값 별도 진입점 분리, 2026-07-20), QuakeSpirit(착탄형 즉발 2단계 AoE — 주 타깃 100%/스플래시 50%, 스플래시가 건물도 포함, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용 + 건물 순회 신설 + 유닛/건물 hit-set 분리, 2026-07-20 멀티 로그 검증). 특수 공격 전략 핸들러 아키텍처(핸들러 + 레지스트리 1줄) 확립. **잔여 없음**(BloomFairy는 힐러 전용 경로로 의도적 미등록). **알려진 이슈(보류, 별도 task)**: QuakeSpirit `OnAttackHit` 미주입 타이밍 어긋남, 멀티 원거리 facing 버그. 상세: `PROJECT_STATUS.md` / `WORK_HISTORY.md`, task `_Tasks/2026-07-16/18_06_battleaxe-aoe/` · `_Tasks/2026-07-17/12_59_torrentspirit-wave-aoe/` · `_Tasks/2026-07-18/03_40_bloomfairy-healer/` · `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/` · `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/` · `_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/`.

**남은 작업**:
1. 실기 테스트 — 프리팹 컴포넌트 부착 정상 동작 확인
2. Animation Event 부착 (각 유닛 공격 애니메이션 타이밍)
3. UnitFactory 종족별 리스트에 신규 16종 등록
4. StatsReference.md 스탯 확정 후 UnitStatsConfig Inspector 입력

---

## Phase E — 플랫폼/폴리싱

### E-1. 사운드/BGM
- BGM (로비/인게임/승리/패배)
- 효과음 (공격, 건물 건설, 골드 획득, 유닛 사망)

### E-2. 튜토리얼
- 첫 실행 시 인터랙티브 튜토리얼
- 헥스 클릭 → 건물 건설 → 유닛 생산 → 공성 흐름 안내

### E-3. Firebase 백엔드
- 실시간 글로벌 리더보드 (Firestore onSnapshot)
- 승/패 기록 저장 (Firestore)
- Android 인앱결제 (Google Play Billing — 스킨)
- Firebase Functions (경기 결과 처리, IAP 영수증 검증)

### E-4. 로그인 시스템 구현 (Login.unity)
- Login.unity 씬 신규 생성 (Build Index 분리)
- LoginUI (익명/Google Play Games/이메일+비밀번호 선택 화면)
- ProfileView 계정 연동 탭 구현 (익명 → 실계정 전환)
- AuthSystemRules.md 기준 구현

---

## Phase F — 전투 타격 타이밍 동기화 후속 (2026-07-12 등록)

전투 타격 타이밍 동기화 작업(`_Tasks/2026-07-09/01_12_combat-hit-timing-sync/`) 완료 후 남은 후속 항목.

### F-1. 이동/Walk 애니메이션 동기화 ✅ 완료 (2026-07-13)
- **결과**: 애니메이션 상태(None/Walk/Attack)를 1회성 엣지 RPC → NetworkUnit의 **NetworkVariable 레벨 동기화**로 전환. 클라 스폰 시 현재 값 자동 적용으로 첫 Walk RPC 스폰 레이스 유실(222/223기) 구조적 소멸. `UnitView.Initialize` 후 `ReapplyAnimStateToView()`(멱등) 재적용으로 애니메이터 미준비 무음 실패 봉합. 재경로 첫 스텝 역방향(뒤로 밀림, 서버 282건)은 `AlignPathStartToTransform`로 282→41 급감.
- **검증**: 3차 로그 15,316줄 — 생성 634기 전원 상태 적용 보장, 육안 걷기 미재생/뒤로 밀림 소멸. 잔여 41건은 스폰 직후 우회 경로를 판정 지표가 오탐한 계측 한계로 코드 무수정 종결. 규칙 U-22 등재.
- **task**: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/`.

### F-2. 빌드 에셋 용량 최적화 ✅ 완료 (2026-07-15)
- **결과**: Android AAB 용량 **190.66 MB → 125.30 MB**로 65.36 MB 절감. 작업 브랜치 `codex/asset-size-optimization`이 main에 병합됨.
- **주요 변경**: `Assets/_Project/Texture/Buildings/**`, `Assets/_Project/Texture/Units/**` Android max texture size `1024 → 512`. `_Old` 미사용 에셋 디렉터리 7개 정리, normal-map PNG 93개 + roughness PNG 84개 정리, 건물/장비 FBX import를 보수적으로 조정.
- **유지한 영역**: UI 배경, 유닛 초상화, 건물 아이콘, UI 스프라이트, TextMeshPro 폰트 에셋은 품질 확인 전 유지. TMP Font Atlas 축소 테스트는 AAB 효과가 작아 되돌림.
- **후속 확인**: 기기 QA에서 설치/실행, 로그인, 로비 UI 가독성, 인게임 유닛/건물 텍스처 품질, 팀 색상 변형, emission/공격 이펙트 품질을 확인한다.
- **상세 문서**: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`.

### F-3. 피격 VFX 프리셋 연결 🟡 중간
- **작업**: `UnitEffectConfig.hitPreset`을 Inspector에서 각 유닛에 배선. Human 6종 등 미연결.
- **비고**: 코드 아님, 에셋 연결 작업.

### F-4. 미구현 특수 타격 클립 이벤트 주입 🟢 낮음
- **✅ BattleAxe 완료 (2026-07-17)**: 휩쓸기형 AoE 구현과 함께 Attack 클립 `OnAttackHit` 이벤트 주입 완료(`hitFrameTimes=1.1667s`, 클립 타격모션 종료 프레임 35f/30fps). `Hexiege/Combat/Inject OnAttackHit Events` 인젝터 사용.
- **✅ TorrentSpirit 완료 (2026-07-17)**: 파도형 이동 AoE + 힐 서브시스템 구현과 함께 `OnAttackHit` 주입(`0.5s`, 임시 — 실제 파도 발동 프레임 튜닝 대상). 규칙 28~31 신설.
- **✅ BloomFairy 완료 (2026-07-18)**: 힐러 전용 경로 구현과 함께 힐 발동 타이밍을 `HitFrameTimes` 타이머로 구동(`OnAttackHit`은 힐 연출 전용). HoT/DoT 공용 시스템 신설. 규칙 32~37 신설.
- **✅ MushroomBomber 완료 (2026-07-19)**: 착탄형 범위 DoT 구현과 함께 Attack 클립 `OnAttackHit` 주입(규칙 27, 1개). DoT 초 단위 discrete 틱 모드(규칙 34 확장) 신설. 규칙 38~40 신설.
- **✅ InfernoSpirit 완료 (2026-07-20)**: 이미 완성된 원거리 유닛(에셋·VFX·생산·`OnAttackHit` 완비)에 단일 대상 DoT 특수만 추가(`InfernoAttackBehavior`, 레지스트리 1줄). 주 타깃 1마리 직접 25 + 그 1마리에게만 DoT 5/초×3초(AoE 아님). 규칙 40 초 단위 틱 재사용, 값은 별도 진입점(`ApplyInfernoDot` 5/3)으로 분리. 규칙 41~42 신설.
- **🔨 QuakeSpirit 특수 공격 로직 완료·클립 이벤트 잔여 (2026-07-20)**: 착탄형 즉발 2단계 AoE 구현·멀티 로그 검증 완료(규칙 43, 핸들러 `QuakeAttackBehavior` + 레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용). 단 Attack 클립 `OnAttackHit`은 **아직 미주입** + `hitFrameTimes` placeholder(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트가 `HitPresentationQueue` 타임아웃 7.5s까지 지연 — 피해·판정·동기화 자체는 정상). 사용자 결정으로 **별도 후속 task 분리**.
- **남은 대상**: QuakeSpirit `OnAttackHit` 클립 이벤트 주입만 잔여(특수 공격 핸들러 확장은 5종 전량 완료). BloomFairy는 힐러 전용 경로로 의도적 미등록.
- **작업**: QuakeSpirit 후속 task에서 실제 타격 프레임으로 `hitFrameTimes` 확정 후 `Hexiege/Combat/Inject OnAttackHit Events` 인젝터로 클립에 `OnAttackHit`을 주입(규칙 17·27) → 타이밍 어긋남 해소.

### F-5. Firebase/EDM 저장소 방침 정리 🟡 중간 (2026-07-13 등록)
- **현황**: 이번 세션에서 발견 — 신규 환경에 저장소를 클론하면 Firebase/EDM(External Dependency Manager) 관련 임포트가 누락되어 재임포트가 필요한 문제.
- **작업**: Firebase/EDM 패키지·플러그인을 저장소에 어떻게 커밋/제외할지(버전 관리 방침)를 정리하여 신규 클론 시 재임포트 없이 빌드 가능하도록 정비.
- **비고**: 인프라/저장소 관리 작업(코드 아님).
- **2026-07-13 진행**: 관련하여 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 컴파일 게이트를 제거(커밋 4fe1cf0)해 SDK 로컬 임포트 시 실제 Firebase 코드가 무조건 컴파일되도록 복원(스텁 컴파일로 로그인 무조건 실패하던 버그 해소). **남은 작업은 SDK/EDM 자체의 버전 관리(커밋/제외) 방침 확정**으로, 게이트 제거와 별개.
## 2026-07-16 로비 프로필/랭킹 클라우드 연동 상태

- 완료: UGS Cloud Save 플레이어 프로필 모델/서비스/UseCase, 닉네임 설정/변경 흐름, 로비 Profile 탭 전적 표시, UGS Leaderboards Ranking 탭, RankRow 프리팹/에디터 생성 스크립트, 기본 UI 레이아웃 보정.
- 완료: 이메일 인증 완료 후 최초 로그인 시 닉네임 설정 화면으로 이동하는 흐름.
- 완료: RankingView의 숨김 상태 자동 로드 제거. 랭킹 데이터는 Ranking 탭 표시 또는 새로고침 버튼에서 로드한다.
- 완료: 이메일 인증 플로우 보완(2026-07-18).
  - 인증 대기 화면에 가입/로그인 시도 이메일을 명시적으로 전달해 표시한다.
  - 회원가입 직후 인증 대기와 기존 미인증 계정 로그인 진입을 구분한다.
  - 회원가입 직후 인증을 포기하고 되돌아가는 경우 Firebase 미인증 계정을 삭제한다.
  - 앱 재실행/자동 로그인 경로에서도 미인증 계정은 인증 화면으로, 닉네임 미설정 계정은 닉네임 설정 화면으로 복귀한다.
  - 장기적으로 생성 후 일정 기간 지난 `emailVerified=false` 계정 정리 정책을 검토한다.
---

## 2026-07-18 이메일 인증 플로우 보정 완료

- 완료: 이메일 인증 대기 화면의 이메일 표시, 뒤로가기 가입 취소 정책, 앱 재실행/자동 로그인 게이트를 보정했다.
- 사용자 실기 PASS:
  - 신규 이메일 회원가입 후 인증 화면에서 실제 이메일 표시.
  - 인증 화면 Back 시 가입 취소 확인 팝업 표시.
  - 가입 취소 확정 시 Firebase Authentication 미인증 사용자 레코드 삭제.
  - 인증 계속하기 선택 시 인증 화면 유지.
  - 인증 화면에서 앱 종료 후 재실행 시 다시 인증 화면 표시.
  - 닉네임 설정 화면에서 앱 종료 후 재실행 시 다시 닉네임 설정 화면 표시.
- 장기 과제: 일정 기간 이상 지난 `emailVerified=false` 계정의 서버/관리자 정리 정책 검토.
