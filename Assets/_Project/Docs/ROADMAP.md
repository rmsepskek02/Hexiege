# Hexiege - 작업 로드맵

**최종 수정일:** 2026-08-25

**최우선 게이트 (2026-08-25):** B3 v10 중심 경로·건물 안전 Chase·전방 중심 복귀는 Android Host·Editor Client 집중 실기에서 MOVE EVIDENCE와 양쪽 local ROOT PASS를 확보했다. Host 중심 checkpoint 766회·최대 오차 0, direct-safe/path Chase 806/2,977 frame, authority/adapter/stationary Walk/drop 오류 0이다. 공식 CrossAudit INCONCLUSIVE의 원인이던 긴 ROOT terminal은 채점 필수 필드만 남긴 803-byte 최악값 compact END와 출력 전 preflight로 교정했으며 Runtime/Editor Roslyn과 Unity self-validation 2종까지 PASS했다. 다음은 Tracer C Phase 4 공격 회차 Shadow와 서버 권위 타겟·공격 방향 교정이다.

> **이 문서의 역할: 앞으로 해야 할 작업.** 완료된 항목은 이 문서에 남기지 않는다.
> 완료 이력은 [WORK_HISTORY.md](WORK_HISTORY.md), 현재 상태는 [PROJECT_STATUS.md](PROJECT_STATUS.md).

**최우선 작업:** RootCrossAudit compact terminal 교정 후 Tracer C Phase 4 공격 회차 Shadow와 서버 권위 타겟·공격 방향 교정. B3 v10 집중 교정은 실기 PASS이며, 25종·반대 역할·Legacy rollback은 Tracer C 이후 권위 전환 전 통합 회귀로 유지한다. Impact/피해 표현 시점은 다음 Phase 5다.
**현재 단계:** 채점기 절단 회귀를 먼저 닫고 공격 Shadow로 이동한다. Legacy 피해·HP·RPC·VFX writer는 그대로 두며 신규 회차는 타겟·방향·Impact 계획을 기록·복제만 한다.
**작업 이력:** [WORK_HISTORY.md](WORK_HISTORY.md) 참조

> B2 PASS는 당시 10°/15° 일반 정렬 계약의 이력이다. v2.1은 이를 안전 fallback으로 재분류했으므로 B2 및 현재 B3 정확성 로그만으로 연속 이동 완료를 선언하지 않는다. 공격 방향·Impact·피해 시점과 ActionSequence 전체 권위 전환도 계속 미완료다.

---

## 우선순위 요약

| 우선순위 | 작업 | 카테고리 | 예상 규모 |
|---------|------|---------|---------|
| 🟡 중간 (**여전히 미검증** — 2026-08-24 (3차) 갱신) | **전역 훅의 엔진 오류 수집(B) 실기 확인** — 2026-08-19 회차에 엔진 오류가 **나지 않아** `[ERROR]`·`UnhandledEngineError`·`Suppressed=` 가 전부 0건이었고, **커밋 `55d24d83` 검증에서 3경기를 더 돌렸는데도 여전히 0건**이다(1408줄 기준). **훅이 동작한다는 증거가 아직 없다.** **⚠️ 역설이지만 기록해 둔다 — B 로 잡으려던 대상이 바로 그 RPC 에러였고, `55d24d83` 의 가드 수정이 그 에러를 없앴다. 즉 수정이 잘 돼서 검증 기회가 사라졌다.** 이제 *"우리가 만들지 않은 엔진 오류"* 가 우연히 나는 회차를 기다려야 한다. 다음에 엔진 오류가 나는 회차에서 로그 파일에 `Event=UnhandledEngineError` 줄이 남는지, `Suppressed=` 가 함께 찍히는지 확인한다 **[🔴 2026-08-24 3차 재확인 — 원문은 그대로 두고 덧붙인다: `bcf45ec1` 검증 세션에서 **3경기를 더 돌렸는데도 `[ERROR]` 가 13,003행 전체에 0건**이라 이번에도 **잡을 대상이 없었다.** 누적하면 2026-08-19 회차 + `55d24d83` 3경기 + 이번 3경기로 **연속 0건**이다. *수정이 잘 돼서 검증 기회가 사라지는 역설*이 계속되고 있으며, **훅이 동작한다는 증거는 여전히 없다.** ⚠️ 이 세션은 **클라이언트 쪽 로그를 처음 수집한 회차**라 표본 성격이 달라졌는데도 0건이었다는 점만 추가된다]** | QA | 소 |
| 🟡 중간 (**미검증** — 2026-08-24 등록) | **`_combatStopped` 재경기 리셋의 실기 확인** — 커밋 `55d24d83` 의 리셋(`NetworkCombatController.cs` **205행 `OnNetworkSpawn`** · **311행 디스폰**)이 2026-08-19 에 「재경기 2회 연속 통과」로 확인됐으나, **2026-08-24 세션으로는 재확인되지 않았다.** 가드가 **380행 `if (!IsSpawned \|\| !IsServer \|\| _combatStopped) return;`** 이라 **2·3경기처럼 에디터가 클라이언트(`IsServer=False`)면 `_combatStopped` 를 평가하기 전에 `!IsServer` 에서 반환**된다. 실제로 2·3경기의 사망 로그는 전부 `EntityDiedClientRpc 수신 → 클라 처리` 경로였고(`서버: 유닛 사망` **0건** · `서버 측 … 이벤트 구독 완료` **0건**) **상대 호스트의 틱이 돈 것**이다. 2026-08-24 실측 내역 — 1경기(서버) 유닛 사망 129 · 건물 5 / 2경기(클라) 수신 64 → 유닛 62 · 건물 2 / 3경기(클라) 수신 179 → 유닛 175 · 건물 4. **확인 조건: 에디터가 호스트로 연속 2경기.** ⚠️ *"판정 로직은 **그 로직이 실제로 실행되는 조건까지** 확인할 것"*(`.claude/MEMORY.md` MistShrine 교훈 ①)의 같은 함정이다 | 검증 | 소 |
| 🟢 낮음 (**미착수** — 2026-08-24 등록) | **`_baseline.json` 의 `measured_at` 을 `--update-baseline` 이 갱신하지 않는다** — 갱신 시 `folders` 만 바뀌고 `measured_at` 은 그대로라, **다음 갱신 후에는 *"언제 실측한 값인가"* 가 거짓이 된다.** 방금 고친 **드리프트(ⓑ)와 정확히 같은 성격**이고, 위 항목의 「하드코딩된 위치·상태 참조」 4건과도 같은 부류다. 오늘(2026-08-24) 갱신했고 오늘 날짜와 우연히 일치하므로 **지금은 맞다** — 다음 갱신 때 어긋난다. ※ 문서 에이전트는 `Tools/check_docs.py` 와 `_baseline.json` 을 직접 편집하지 않는다 | 도구 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ProductionTicker` 에 종료 가드 추가** — `Presentation/Production/ProductionTicker.cs` **238행 `Update()`** 에 종료 가드가 없다. 246행의 유일한 분기는 *"멀티 클라이언트면 스킵"* 뿐이라, 위험 구간에서 **생산 틱(260행)과 수입 틱(265행)이 정상 속도로 돈다.** **길목으로는 이번 8곳보다 근본적**이지만 `Presentation` 레이어이고 틱 전체를 멈추는 것은 **동작 변경**이라 별도 설계 판단이 필요하다 | 코드 정리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`NetworkGameEndController` 재경기 3종(`_localRematch*`)의 가드** — `ServerRpc` 3종(`RequestRematchServerRpc`·`AcceptRematchServerRpc`·`DeclineRematchServerRpc`)이고 **`IsServer` 블록 밖에서 구독**되어 클라이언트에서도 살아 있다. 따라서 필요한 가드는 **`!IsSpawned` 만**으로 이번 8곳과 **형태가 다르다.** 위험 구간(수십 ms) 안에 사용자가 재경기 버튼을 눌러야 발동하므로 실현 가능성도 사실상 없다 | 코드 정리 | 소 |
| 🟢 낮음 (범위 밖 — 후속 후보, 2026-08-20 등록) | **`ServerRpc` 계열 전반의 종료 가드** — `NetworkProductionController` · `NetworkBuildingController` · `NetworkUpgradeController` · `NetworkSkillController` 등 대부분 진입부 가드가 없다. **호출 주체가 UI 입력**이라 *"이벤트 구독으로 자동 실행되는 경로"* 를 본 이번 조사와 **성격이 다르다.** 별건으로 다뤄야 한다 | 코드 정리 | 중 |
| 🟢 낮음 (범위 밖 — 후속 후보) | **`NetworkUnit.SetAnimState` 의 `IsSpawned` 가드** — 현재 `if (!IsServer) return;` 만 있어 `IsSpawned` 를 보지 않는다. 애니메이션 상태 쓰기의 **더 근본적인 자리**지만 다른 파일이라 2026-08-19 작업에서는 `NetworkCombatController.SetUnitAnimState` 쪽에서 막았다. **디스폰 후 NetworkVariable 쓰기가 실제로 오류를 내는지 자체가 미확정**이라 착수 전 그 확인이 먼저다. **[2026-08-20 재확인]** 여전히 `NetworkUnit.cs` **173행 `if (!IsServer) return;`** 뿐이다(선언 170행). 이번 전수 보강에서도 **의도적으로 제외** — `.SetAnimState(` 호출부가 `NetworkCombatController.SetUnitAnimState` **한 곳뿐**이고 그 상위가 이미 `!IsSpawned` 로 막혀 있어 **중복**이기 때문이다 | 코드 정리 | 소 |
| 🟡 중간 (미완 · [6] 통과 후) | **`SetCameraStartPositionForTeam` 주석 처리분 최종 삭제** — 호출부 0곳으로 확인되어 `GameBootstrapper.Setup.cs` **537행**에 비활성화 블록으로 남아 있다(파일 헤더 목차 14행에 `— 제거 예정` 병기). WORKFLOW.md [4] 대로 **사용자 테스트 통과 후 · 문서 업데이트 전**에 삭제한다 | 코드 정리 | 소 |
| 🟡 중간 (**여전히 미검증** — 2026-08-19 갱신) | **`NetworkUpgradeController` 구독 누락 수정의 실기 확인** — 커밋 `da5eeaab`. **2026-08-19 회차에서 정상 경로는 로그로 확인되었으나**(착수 6 ↔ 완료 브로드캐스트 6 1:1, `Level=1`→`2`) **원래 버그의 원인인 스폰 레이스가 발생하지 않아**(`NetworkControllerSpawnedWithoutGameServices` **0건**) **지연 구독 경로 자체가 필요해진 상황이 없었다.** 즉 확인된 것은 **「버그가 재현되지 않았고 정상 경로는 확인됐다」** 까지다. **회선 상태에 좌우되는 간헐 버그**라 재현이 사실상 불가능하다. 로그에 *"스폰 시점에 IGameServices 를 얻지 못했다"* 가 **`NetworkUpgradeController` 이름으로** 찍힌 판에서 연구 완료가 정상일 때만 직접 증거가 된다. **"안 났으니 고쳐졌다"로 결론 내리지 않는다** | QA | 소 |
| 🟢 낮음 (미확인) | **`Presentation/UI/LobbyUI.cs` 가 현재 쓰이는지 확인** — 로비가 `LobbyRootView` 계열로 재구성됐는데 이 파일은 옛 구조 그대로다. 이관 작업에서는 **확인 없이 판단하지 않고 10건 전부 이관**했다(CLAUDE.md 규칙 10·12). 쓰이지 않는다면 죽은 코드 정리 대상 | 코드 정리 | 소 |
| 🟢 낮음 (사용자 판단 대기) | **`_Logs/_editor/` 의 `.meta` 취급 규정 명문화** — `.gitignore` 는 이미 `LogRules.md` 1.10 규정에 부합한다. 남은 것은 `.meta` 를 커밋할지에 대한 **규정 공백**이다. 사용자가 커밋 `23a8da06` 에서 `.meta` 를 실제로 커밋했으나, **그 관찰만으로 규정이 확정된 것으로 적지 않는다**(CLAUDE.md 규칙 10) | 문서/규정 | 소 |
| 🟢 낮음 (범위 밖으로 미룸) | **`FileSink.EditorLogsRootRelativeToAssets` 접근 수준 조정** — 현재 `private` 이라 `LogcatCapture.cs` 가 같은 경로 문자열을 **복제**하고 「FileSink 와 동기화 필요」 경고 주석을 달아 두었다. `internal const` 로 올리면 복제를 없앨 수 있다 | 코드 정리 | 소 |
| 🔵 초기 정상·지속 관찰 중 | 매치메이킹 404(호스트 결정 단계) 수정 — 호스트 결정을 매치 결과 조회(P2P 클라 404) → Lobby CreateOrJoin 원자 선점(A방식)으로 전환 (2026-07-17, 커밋 `a3dbc73`). 초기 실기 정상, 간헐 버그라 지속 멀티 실기 검증 필요 | QA/버그 | 중 |
| 🔴 P0 | 서버 권위 Unit ActionSequence 구현 — A0/A1/A2/B0/B1/B2 완료 → B3 연속 이동 구현·Editor PASS, Android 역할교대·25종·rollback 대기 → Snapshot/ImpactResult 복제 → FIFO 대체 → 멀티 QA → Legacy 제거 | 전투/네트워크 | 특대 |
| ✅ Tracer A0 | SpearMan schedule/dispatch Shadow — Host 204/204·고유 204·누락/중복/타겟/facing 불일치 0. Client는 header only이며 실제 피해 결과 검증이 아님 (2026-07-22) | 전투/계측 | 중 |
| ✅ Tracer A1 | Pure Application UnitAction 계약+stateful reducer — C# 9/Application·Editor compile PASS, Unity Editor 메뉴 PASS, reflection Validate* 10 PASS, Standards/Spec P0~P3 0 (2026-07-22) | 아키텍처/전투 | 중 |
| ✅ Tracer A2 | Server-authoritative pose seam shadow — Host 완료 회차 상관관계 누락·중복 0, attacker-dead 2건 DeadTerminal, capacity eviction·예외 0, Client observer 0. 기존 피해·RPC·VFX 권위 유지 (2026-07-27) | 전투/네트워크 | 대 |
| ✅ Tracer B0 | Visual Root migration readiness — 50/50 read-only 감사, errors 0, mutation/assetsModified 0, 연속 실행 aggregate manifest 동일 (2026-07-27) | 에셋/검증 | 중 |
| ✅ Tracer B1 | 50개 프리팹 Simulation Root / Visual Root migration·rollback과 Android/Editor 역할교대 양방향 NetworkTransform 이동 pose 검증 PASS. Match A/B pose mismatch 0. 공격 방향·Impact·피해 시점은 범위 밖 | 전투/네트워크 | 대 |
| ✅ Tracer B2 | 서버 이동·SimulationFacing pure reducer + read-only Shadow — Android/Editor 역할교대, 25/25종·Blue/Red 증거, 최종 오류 카운터 0, 손실 없는 manifest/SHA 검증 PASS. 실제 writer 전환은 아님 (2026-08-03) | 전투/네트워크 | 대 |
| ✅ 집중 PASS / 통합 회귀 유지 | Tracer B3 v10 중심 이동·건물 안전 Chase·전방 중심 복귀 — Android Host MOVE EVIDENCE, 중심 766/최대 오차 0, Chase 806/2,977, 오류 0, 양쪽 local ROOT 53/53 PASS. 긴 Host terminal 절단 채점기 교정과 25종·반대 역할·Legacy rollback은 다음 통합 회귀에 포함 | 전투/네트워크 | 특대 |
| 🔴 P0 다음 | Tracer C Phase 4 공격 회차 Shadow — 서버 권위 TargetId·AimDirection commit, AlignToAttack 5°/8°, Windup 전 정렬, Legacy와 신규 타겟·방향 차이 계측. 기존 피해·HP·RPC·VFX writer는 유지 | 전투/네트워크 | 특대 |
| ✅ 설계 완료 | 유닛 이동·공격 규칙 v2 일괄 개정 + 25종 공격 에셋 감사. 런타임 완료를 의미하지 않음 (2026-07-20) | 설계/문서 | 대 |
| ✅ Legacy 반영 | QuakeSpirit UnitStatsConfig 등록 — HP250/ATK20/range0.5/detect1.0/move0.5/cooldown5.0. 단 hitFrameTimes 1.00초는 placeholder | 데이터/전투 | 중 |
| 🔴 P0 | AttackTimeline 교정 — 기본 Attack marker 누락 4종(Quake 포함), BattleAxe·Inferno·Stream 등 설정/클립 불일치, 복수 Attack 클립 선택 순서 제거 | 애니메이션/전투 | 대 |
| 🔴 P0 | 전투 결과 단일 writer/emitter 복구 — `ApplyDamageToVictim`과 Quake의 `ApplyFixedDamageToVictim`을 ActionSequence 결과 적용기로 수렴 | 아키텍처/전투 | 중 |
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
| 🟡 중간 (미착수) | **MistShrine 멀티플레이 실기 검증** — 이번 사이클 검증은 **에디터 싱글플레이로만** 이루어졌다. 범위 판정·중첩 해소·회복량 계산은 싱글·멀티가 같은 코드 경로를 공유하지만, **건물 HP 동기화(`SyncBuildingHealClientRpc`)·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱 여부는 멀티에서만 도는 경로이며 한 번도 실행되지 않았다.** Host+Client 구성으로 확인 필요 | QA | 소 |
| 🟡 중간 (미착수) | **MistShrine 물안개 지속 VFX + 사용 버튼 아이콘 제작** — 현재 물안개는 **눈에 보이지 않고**(등록 VFX는 `vfx_mistshrine_destroy`/`vfx_mistshrine_upgrade`뿐 — 규칙 26), 사용 버튼은 **임시 텍스트 라벨**(UI 규칙 15). VFX 재생 시 사운드 규칙 15(VFX+SFX 쌍) 준수 | 에셋 | 중 |
| 🟡 중간 | 스킬 건물 구체 스킬 목록·수치 확정(기획) — 각 슬롯 1~5 스킬·쿨다운·반경·지속·피해·상태효과를 ScriptableObject 데이터로 확정. 개별 스킬 쿨다운 도입 여부·회복 스킬 지점 지정 전환 여부 포함 | 기획 | 중 |
| 🔴 높음 | 전역 GameProtocolVersion/build 호환성 관리 — matchmaking 동일 버전 필터, custom lobby Relay 전 검사, NGO connection approval/rejection, reconnect 버전 검증, update-required UX | 네트워크/호환성 | 대 |
| 🔴 높음 | Login.unity 씬 로그인 UI 조립 | UI | 소 |
| 🔴 높음 | UGS OIDC 브릿지 활성화 — UGS Dashboard OIDC 제공자(`oidc-firebase`) 등록 (현재 `id provider not found`로 멀티플레이 제한) | 인프라 | 소 |
| 🟡 중간 | BuildFailed/EnqueueFailed UI 피드백 (멀티) | UI | 소 |
| 🟡 중간 | 게임 내 밸런싱 (골드/HP/생산시간) | 기획 | 중 |
| 🟡 중간 | 로비 UI 비주얼 폴리싱 (에셋 제작 완료 2026-05-30) | UI | 중 |
| 🟢 낮음 | 재접속 실제 구현 | 기능 | 중 |
| 🟡 중간 | 피격 VFX 프리셋 연결 — `UnitEffectConfig.hitPreset` Inspector 배선(Human 6종 등 미연결) | 에셋 | 소 |
| 🟢 낮음 | QuakeSpirit Attack 클립 `OnAttackHit` 이벤트 주입 — 특수 공격 로직(규칙 43)은 구현·검증 완료됐으나 클립 `OnAttackHit` 미주입 + placeholder `hitFrameTimes`(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상). 인젝터로 실제 타격 프레임 주입 시 해소. 사용자 결정으로 별도 후속 task. (특수 공격 핸들러 확장 완료: BattleAxe·TorrentSpirit 2026-07-17, BloomFairy 2026-07-18, MushroomBomber 2026-07-19, InfernoSpirit·QuakeSpirit 2026-07-20) | 기능/에셋 | 소 |
| 🟡 중간 | 멀티플레이 원거리 공격 방향(facing) 버그 — 원거리 유닛이 공격 시 타겟을 정확히 안 바라봄. 진단상 에셋 아닌 멀티 원거리 facing 공유 회전 로직(서버 계산+NetworkTransform) 문제로 추정(근접 유닛 미노출). 수정 시 근접 유닛 회귀 주의. 상세: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md` (InfernoSpirit 작업에서 진단·보류) | QA/버그 | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

> **※ 2026-08-10 ×10 스케일 반영 정정:** 위 표의 **MushroomBomber · InfernoSpirit · QuakeSpirit** 행과 상단 **"현재 단계"** 문단에 남아 있던 ×10 개편(2026-07-31) **이전** 전투 수치(직접 피해·DoT 틱값)를 현재 값으로 정정했다. **비율·반경(100% / 50% / `×0.5` / 반경 1.0)과 쿨다운·사거리·이동속도·골드 비용은 ×10 대상이 아니므로 그대로다.** 날짜가 박힌 완료 이력(Phase D-1 방어 타워 완료, Phase F-4 클립 이벤트 주입 이력 등)은 **당시 기록이므로 원문을 보존**한다. 수치의 단일 진실 소스는 [StatsReference.md](StatsReference.md)다.

---

## Phase A — 네트워크 버그 수정

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

**🔵 핵심 흐름 실기 조건부 완료 (2026-07-16)**: 유닛 생산·건물 업그레이드 등 핵심 흐름을 반복 실기로 확인 — PASS, 특별한 문제 미발견.

**남은 작업 (세부 정밀 검증)**:
1. 반응 시스템(R1 유닛열세 / R2 골드과잉 / R3 채굴소 파괴)의 트리거·동작 정밀 검증
2. 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작 확인

---

### C-1. 게임 내 밸런싱
현재 수치는 임시값. 플레이테스트 후 조정 필요.

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

> **MistShrine 관련 수치 근거:** [GameSystemRules_Buildings.md](GameSystemRules/GameSystemRules_Buildings.md) **MistShrine 물안개 힐 시스템 규칙 16**(밸런싱 미확정 표)이 단일 소스다.
> **2026-08-12 갱신 — 구현은 완료됐지만 수치는 여전히 미확정이다.** 현재 게임에서 도는 값은 **전부 임시값**이며(`_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/Plan.md` §3), **어떤 문서에서도 확정 수치로 인용해서는 안 된다.**
> 밸런싱 확정 시 바꿀 곳은 **`Resources/Config/SpecialAttackConfig.asset` 한 파일뿐**이다(코드 기본값은 폴백일 뿐이며 Inspector 값이 우선). 반경 값이 표시용/판정용으로 이중화돼 있던 문제는 2026-08-11에 제거되어, **에셋만 고치면 화면의 범위 원까지 함께 따라간다.**

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
- **미완:** 구체 스킬 목록·수치·아이콘 확정(기획). 현재는 종족별 플레이스홀더 5슬롯 테스트용.
  상세: [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md)

### D-3. 유닛 AI 상태머신
- 추가 목표: Idle → Patrol → Retreat 상태 확장 (현재 미구현)

### D-4. 신규 유닛 프리팹 완성 (16종)
**🔧 에디터 스크립트 완료 (2026-06-05)**: Human 5종(LittleKnight/SpearMan/BattleAxe/Tank/CannonCart)·Spirit 6종(DustSpirit/BoulderSpirit/QuakeSpirit/TideSpirit/StreamSpirit/TorrentSpirit)·Transcendence 5종(RabbitTrickster/RhinoBreaker 등) × Blue/Red 총 32개 프리팹 자동 컴포넌트 부착. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`(**1회성 — 실행 후 삭제됨**)

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

### F-3. 피격 VFX 프리셋 연결 🟡 중간
- **작업**: `UnitEffectConfig.hitPreset`을 Inspector에서 각 유닛에 배선. Human 6종 등 미연결.
- **비고**: 코드 아님, 에셋 연결 작업.

### F-4. 미구현 특수 타격 클립 이벤트 주입 🟢 낮음
- **🔨 QuakeSpirit 특수 공격 로직 완료·클립 이벤트 잔여 (2026-07-20)**: 착탄형 즉발 2단계 AoE 구현·멀티 로그 검증 완료(규칙 43, 핸들러 `QuakeAttackBehavior` + 레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용). 단 Attack 클립 `OnAttackHit`은 **아직 미주입** + `hitFrameTimes` placeholder(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트가 `HitPresentationQueue` 타임아웃 7.5s까지 지연 — 피해·판정·동기화 자체는 정상). 사용자 결정으로 **별도 후속 task 분리**.
- **남은 대상**: QuakeSpirit `OnAttackHit` 클립 이벤트 주입만 잔여(특수 공격 핸들러 확장은 5종 전량 완료). BloomFairy는 힐러 전용 경로로 의도적 미등록.
- **작업**: QuakeSpirit 후속 task에서 실제 타격 프레임으로 `hitFrameTimes` 확정 후 `Hexiege/Combat/Inject OnAttackHit Events` 인젝터로 클립에 `OnAttackHit`을 주입(규칙 17·27) → 타이밍 어긋남 해소.

### F-5. Firebase/EDM 저장소 방침 정리 🟡 중간 (2026-07-13 등록)
- **현황**: 이번 세션에서 발견 — 신규 환경에 저장소를 클론하면 Firebase/EDM(External Dependency Manager) 관련 임포트가 누락되어 재임포트가 필요한 문제.
- **작업**: Firebase/EDM 패키지·플러그인을 저장소에 어떻게 커밋/제외할지(버전 관리 방침)를 정리하여 신규 클론 시 재임포트 없이 빌드 가능하도록 정비.
- **비고**: 인프라/저장소 관리 작업(코드 아님).
- **2026-07-13 진행**: 관련하여 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 컴파일 게이트를 제거(커밋 4fe1cf0)해 SDK 로컬 임포트 시 실제 Firebase 코드가 무조건 컴파일되도록 복원(스텁 컴파일로 로그인 무조건 실패하던 버그 해소). **남은 작업은 SDK/EDM 자체의 버전 관리(커밋/제외) 방침 확정**으로, 게이트 제거와 별개.
