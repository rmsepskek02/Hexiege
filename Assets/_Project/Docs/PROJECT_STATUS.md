# Hexiege - 프로젝트 진행 현황

**최종 수정일:** 2026-08-17

**구현 완료 (2026-08-17) — 컴파일 통과 · ✅ 실기 검증 PASS:** **씬 무관 로그 수집 구현 완료** (커밋 `a253232e`). 로그 파일에 기록을 켜는 배선이 `GameBootstrapper` 에만 있었고 그것은 **`Game.unity` 에만 배치**되어 있어, **로그인·로비 구간의 로그 73건이 파일에 한 줄도 남지 않던** 문제를 해소했다. **[구현]** 신규 정적 클래스 **`Infrastructure/Debug/LogSessionOwner.cs`** 가 sink 인스턴스·초기화 플래그·훅 중복 방지 상태를 **한 곳의 `static`** 으로 소유한다(계획서 가칭 `LogSessionBootstrap` 에서 이름 변경 — Infrastructure 에 `~Bootstrap` 을 두면 **세 번째 조합 루트로 오독**되기 때문). 규칙을 **여는 쪽 둘 / 닫는 쪽 하나**로 뒤집었다 — 두 부트스트래퍼(`LoginBootstrapper` · `GameBootstrapper`)가 `Awake` 첫 줄에서 `EnsureInitialized()`(멱등)를 부르고, 닫는 것은 `UnityEngine.Application.quitting` **1회**다(`GameBootstrapper.OnDestroy` 의 `ShutdownLogging()` 호출은 주석 처리 — 씬 언로드 때 닫으면 앞 씬이 연 세션까지 끊긴다). 에디터 보조 배선은 `EnteredEditMode`(`ExitingPlayMode` 는 `OnDestroy` 보다 앞설 수 있어 종료 과정 로그를 흘린다). **`LogRules.md` 1.9 의 전역 미처리 예외 훅도 함께 이관**했다 — 등록·해제와 중복 방지 상태가 인스턴스에 묶여 있어 커버리지만 고치면 로그인 구간에 구멍이 남기 때문이다. `LoginBootstrapper.cs` 13행의 *"GameBootstrapper 는 Lobby/Game 씬에 존재"* 주석도 실측(`Lobby.unity` 참조 0건)에 맞춰 정정. **[⚠️ 부수 영향]** 미처리 예외 로그의 `className` 이 `GameBootstrapper` → **`LogSessionOwner`** 로 바뀐다(`[Runtime/GameBootstrapper]` → `[Runtime/LogSessionOwner]`). Infrastructure 가 Bootstrap 을 참조할 수 없어 불가피하며 **로그를 grep 하는 쪽에 영향**이 있다. **[✅ 실기 검증 — 전 항목 PASS]** 사용자가 랜덤매칭 실기(에디터+실기기)로 확인했고 근거 로그가 `_Logs/_editor/2026-08-17/RuntimeLog.txt`(**199줄**)에 커밋되어 있다: 파일명 `RuntimeLog.txt` · 헤더 3줄 규정 · 199줄 축적 · **같은 파일에 2번째 헤더(일 단위 이어쓰기)** · `Role=` 3건 · **`[Auth/...]` 7건 기록(목적 달성)** · 마스킹(`Uid`·`PlayerId` **16자리 16진수** · `Email=` 0건 · `@` 포함 줄 0건) · 감사 누락분 `HostId` 해시 일치. **[후속 — `Role` 값 표기 통일 (커밋 `73574a23`)]** 실기 로그에서 같은 `Role=` 키가 두 표기로 갈린 것이 발견되어(`NetworkGameManager` 5곳 `Host`/`Client` · `NetworkCombatController` 1곳 `host`/`client`) **소수 쪽을 `Host`/`Client` 로 맞췄다.** 근거는 `LogRules.md` **1.4** — 표기가 섞이면 *"집계가 조용히 둘로 갈라진다"*. **[⚠️ 과대 표기 금지 — 남는 단서]** **커밋 `73574a23` 은 명시적인 컴파일 확인을 받지 않았다**(문자열 리터럴 대소문자 1줄 변경이지만 "통과했다"고 적지 않는다. 커밋 `a253232e` 까지는 통과 확인됨 — 사용자가 그 상태로 실기를 수행했다) · **`Lobby.unity` 직접 진입은 커버되지 않는다**(부트스트래퍼가 없어 sink 0개 → 콘솔 폴백. **사용자가 "로비를 직접 열어 실행하는 경우는 없다(항상 Login 을 거친다)" 고 판단해 커버하지 않기로 결정** — 미해결 결함이 아니라 범위 밖 확정) · **나머지 계층 이관 미착수**(잔존 `Debug.Log` **234건** — 별도 task) · **잔여 조치 1건 해소**: `GameBootstrapper.cs` 의 주석 처리된 `// ShutdownLogging();` **삭제 완료**(커밋 `7ef682db` — 사용자 테스트 통과 후 삭제 예정이었던 항목. 동작 변경 없음). 상세: `LogRules.md` **1.8**·**1.9**·**1.10**·**1.13**, task `_Tasks/2026-08-17/11_07_scene-independent-logging/Plan.md`(§9-1 · §10-1 · §13 · §14).

**코드 작성 완료 (2026-08-17) — 컴파일 통과 확인됨 (2026-08-17) · ✅ 실기 동작 테스트 PASS (2026-08-17):** **로그 체계(`GameLog`) 전환 — 네트워크·인증 계층 정리 task 의 코드 작업이 전부 끝났다.** `LogRules.md` 2026-08-13 전면 개정(심각도 + 존속 **두 축**)에 맞춰, 네트워크·인증 **8파일 205건**의 raw `Debug.Log` 를 `GameLog` 로 이관(`개발` 120 / `운영` 85, 커밋 `668e0aeb` 계열 — **이 이관분은 사용자 유니티 컴파일 통과 확인됨**)한 데 이어, 남아 있던 **미조치 4항목**을 구현했다(커밋 `4e027e68` · `675203ae`). **[구현 내역]** ① **민감 데이터 마스킹 15곳** — Firebase UID · UGS PlayerId 를 `GameLog.HashId` 해시로 치환하고 **이메일 출력은 없앴다**(`Email=` 잔존 0건). `LogRules.md` **1.6** 이 *"에디터 포함 항상 적용"* 이고 **에디터 로그 파일은 커밋되어 공유**되므로 `개발` 로그에도 적용했다. ② **에디터 로그 파일명 단일화** — ~~`RuntimeLog_host.txt` / `RuntimeLog_client.txt`~~ → **`RuntimeLog.txt`**. 빌드는 `#if UNITY_EDITOR` 로 파일을 쓰지 않아 **파일을 쓰는 프로세스가 항상 에디터 1개**라 나눌 이유가 없었고, 기존 파일명은 *"에디터 = Host"* 라는 **틀린 전제**에 기대고 있었다. 역할은 `NetworkCombatController.OnNetworkSpawn` 이 `Role=host` / `Role=client` **로그 한 줄**로 남긴다. ③ **`RuntimeLogger` 헤더 규정 준수** — `BeginSession(folderPath, purpose)` 로 시그니처를 바꿔 헤더 1줄째에 목적을 넣고 헤더 뒤 빈 줄 1줄을 추가. ④ **수동 정리 메뉴 구현** — `Hexiege > Logcat > 3. 오래된 에디터 로그 정리`(보존 기간 선택 → 대상 목록 확인 → `_Logs/_editor/` 하위 한정 · 날짜 폴더 단위 · 삭제 직전 매 건 경로 재검사). **[감사가 놓쳤던 1건]** `LobbyManager` 의 `"CreateOrJoin 완료"` 로그가 **`HostId` 를 평문 출력**하고 있었다(`HostId` 가 UGS PlayerId 인 근거는 같은 파일이 `AuthenticationService.Instance.PlayerId` 와 직접 비교한다는 점) — 감사표에 없던 자리라 그 **누락 사실 자체를 `LogAudit.md` §6 에 기록**했다. **⚠️ 과대 표기 금지 — 아직 완료가 아닌 것:** **커밋 `4e027e68` · `675203ae` 는 사용자 유니티 컴파일 통과가 확인되었다(2026-08-17) — 다만 컴파일 통과는 문법이 맞다는 뜻일 뿐이다** · **실기 동작 테스트를 하지 않았다** · **새 정리 메뉴는 실제로 실행해 본 적이 없다** · **나머지 계층 이관 미착수**(전체 잔존 `Debug.Log` **234건**은 이번 8파일 밖 — 별도 task 로 분리 결정) · **`.meta` 취급 규정 명문화 보류**(사용자 판단 대기). 상세: `LogRules.md` **1.13**, task `_Tasks/2026-08-13/07_13_network-auth-log-cleanup/`(`Plan.md` §0-7 · §0-9 · `LogAudit.md`).

**구현 완료 (2026-08-12) — 에디터 싱글플레이 실기 검증 완료 · 멀티 미검증:** **MistShrine(HealShrine) 물안개 힐 시스템 구현 완료.** 2026-08-10 기획 확정 후 코드·프리팹·씬 배선을 모두 구현하고 **에디터 싱글플레이 실기 + 런타임 로그 실측으로 검증**했다. 검증 완료 항목: **범위 원 경계 = 실제 회복 판정 일치**(회복 최대 거리 2.29 / 탈락 최소 거리 3.12, 설정 반경 3.00과 모순 0건), **범위 이탈 시 즉시 끊김**(규칙 9 — 거리 2.29→3.77 이탈 다음 틱부터 HP 고정), **회복량 매 틱 +10**(설정값 일치), **중첩 해소**(규칙 13 — 후보 4개에서도 대상당 1회만 적용, 거리 동률 0.87에서 Id 작은 신전 선택), 자동/수동 토글·쿨다운 방향·적 건물 미반응·연구/스킬 패널 `ClosedFrame` 가드. **⚠️ 과대 표기 금지 — 아직 완료가 아닌 것:** **멀티플레이 실기 미검증**(싱글로만 검증했다. 범위 판정 코드는 싱글·멀티 공유라 판정 로직은 유효하나 **건물 HP 동기화·클라 표시·RPC 팀 검증·쿨다운 로컬 미러 등 멀티 고유 경로는 실행된 적이 없다**) · **물안개 지속 VFX 미제작**(물안개가 눈에 보이지 않는다) · **사용 버튼 아이콘 미제작**(임시 텍스트 라벨) · **밸런싱 수치 미확정**(현재 값은 전부 임시값). **구현 중 발견·수정한 규칙 위반 버그:** 겹친 물안개 2개가 같은 대상을 각각 회복시켜 **초당 회복량이 2배**가 되던 문제 — 물안개마다 틱 위상이 달라 중첩 해소 코드가 **돌 기회 자체가 없던 죽은 코드**였다. **활성 물안개 위상 정렬 + 소유권 판정을 발화 여부와 분리**해 수정(커밋 `be17148`), 이 불변식을 **규칙 8-1에 보강 기재**. 상세: 아래 "완료된 시스템 > MistShrine 물안개 힐 시스템", `GameSystemRules/GameSystemRules_Buildings.md`, task `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`(§9-4 · §10).

**기획 확정 이력 (2026-08-10):** MistShrine은 공격하지 않는 별도 힐 건물로, 시전 시 **건물 중심 고정 원형 범위**에 물안개가 깔려 지속 동안 **아군 유닛+아군 건물**(자기 자신·Castle 포함)을 **1초 discrete 틱**으로 회복한다(범위 이탈 시 즉시 끊기는 아우라, 물안개 지속 < 쿨다운, 시전 비용 없음, 파괴 시 물안개 즉시 제거, 물안개 간 중첩 금지 — 가까운 건물 우선·거리 동률이면 Id 작은 쪽, **연구소 자연회복과는 중첩 적용**). UI는 `BuildingPanelBase` 상속 전용 패널(탭=수동/롱프레스=자동 토글, **자동 기본 OFF**, `SkillCooldownOverlay` 재사용, 범위 표시는 아군·패널 열린 동안만). 로직은 `SkillActivationUseCase` 재사용 불가로 **전용 UseCase 신설**(쿨다운 패턴만 차용 + 클라 로컬 미러). **함께 정정한 문서 오류:** GDD/TDD가 Transcendence 방어 타워를 MistShrine으로 적어 왔으나 **정확히는 VineTower**이며 MistShrine은 `HealShrine`(=6) 별도 건물이다. 수치(회복량·지속·쿨다운·반경·텍스트 주기)는 **전부 밸런싱 미확정.** 상세: 아래 "완료된 시스템 > MistShrine 물안개 힐 시스템", `GameSystemRules/GameSystemRules_Buildings.md`, task `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`(기획) · `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`(구현).
**버그 수정 (2026-08-08):** **랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정 · 실기 테스트 PASS**(커밋 `9a19cd5`). 배럭 팝업에서 랠리포인트 버튼을 누르면 팝업만 숨겨지고 공유 반투명 오버레이(BlockingOverlay)가 화면에 남아, 집결지를 찍으려 맵을 탭하면 오버레이(탭=`Close`)가 터치를 먼저 가로채 `Close()`→`OnBeforeClose()`(`IsSettingRallyPoint=false`+랠리 마커 숨김)가 실행되며 **지정이 그냥 취소된 것처럼 보이던** 버그. 원인은 `ProductionPanelUI.OnRallyPointClick()`이 `_popup?.Hide()`만 호출하고 오버레이를 내리지 않은 것(오버레이를 켜는 곳은 `BuildingPanelBase.Show()`, 끄는 곳은 `Close()` 단 하나). **수정: `ProductionPanelUI.cs` 1파일 2줄 순수 추가** — ① `OnRallyPointClick()`에 `HideBlockingOverlay()`(스킬 패널 `BuildingSkillPanelUI`의 조준 진입 패턴과 동일) ② `Close()`를 거치지 않는 `CompleteRallyPointSetting()`에 참조 카운터 반납 호출(②를 빠뜨리면 카운터 미반납으로 다음 팝업의 오버레이가 어긋남 — 기존엔 이 버그가 대신 `Close()`를 태워 우연히 상쇄하고 있었음). 이로써 `GameSystemRules_Buildings.md` 랠리포인트 시스템 규칙 2의 "설정 직후 3초 표시"도 정상화. 규칙 근거 `GameSystemRules_UI.md` 공통 UI 규칙 5(BlockingOverlay 단일 소유·참조 카운터) — **규칙 문서 변경 없음(코드가 기존 규칙을 다시 준수하도록 맞춘 수정)**. task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/`(Testcase 미작성 — 사용자 미지시).
**구현 완료 (2026-08-08):** **건물 파괴 시 열린 패널/조준 UI 원복** **구현 완료 · 실기 테스트 PASS**(커밋 `8c7fa01`). 건물 패널 4종(생산/건물액션/스킬/연구)이 열려 있거나 스킬 조준 중일 때 그 건물이 파괴되면 화면에 유령 패널·조준 UI가 남던 시각적 갭을 해소. 공통 베이스 `BuildingPanelBase`가 `GameEvents.OnBuildingDied`를 구독해, 파괴된 건물이 **현재 표시/조준 중인 건물이면(`_currentBuilding.Id` 매칭 — 조준 중엔 `_popup.Hide()`로 `IsOpen=false`라 IsOpen 아닌 Id로 판정) `Close()` 호출** → 각 패널 `OnBeforeClose`로 스킬 조준 취소(`CancelAim`)·생산 랠리 마커 숨김이 자동 연계(4개 패널 전부 커버). **코드 변경은 `BuildingPanelBase.cs` 1개 파일만**(자식 4개 패널 무변경). 멀티는 `NetworkCombatController.HandleBuildingDied`가 클라에서 `OnBuildingDied`를 재발행해 싱글/호스트/순수 클라 전부 커버(별도 배선 불필요). **교훈: MonoBehaviour 베이스에서 자식(`ResearchPanelUI`)이 자체 `OnDestroy`를 선언하면 베이스 `OnDestroy`가 은닉(hide)돼 구독 해제가 누락되는 회귀가 생김 → Plan의 `OnDestroy`+Dispose 대신 `.AddTo(this)`(UniRx, 프로젝트 관용 패턴)로 컴포넌트 수명에 묶어 해결.** task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`. 아래 "완료된 시스템 > 스킬 건물 시스템" 후속/미완 항목 ① 참조.
**구현 완료 (2026-08-05):** 스킬 시스템 **타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2** **구현 완료 · 실기+멀티(클라) 테스트 PASS**. 스킬 프레임워크의 마지막 메커니즘 타입으로, 버프·디버프·제어(둔화·빙결)·회복을 **하나의 상태변경 시스템**으로 표현. Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}`(순수 계산) + Application `Services/StatusEffectSystem`(서버 권위 부여/틱) + `Skill/GlobalStatusChangeExecutor`(타입 C 실행기, 조준 없음 전역 즉시) 신설. 유효 스탯 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`)에 상태 배율을 연구 강화 배율과 **곱연산 합성** + 공격 게이트 `CanAttack`(빙결/기절 시 데미지 봉쇄) 추가 — **무상태면 배율1·CanAttack true라 기존과 완전 동일(회귀 안전)**. 빙결=이동배율 0 + `Animator.speed=0` 애니 정지(`UnitAnimState.Frozen`·`OnUnitFreezeChanged` 클라 동기화), 둔화=이동 코루틴이 매 프레임 유효 배율을 재조회해 **즉시 라이브 반영**(전투 종료 정렬 Lerp 구간만 캡처값 미세 잔여), 회복=기존 HoT 재사용. 멀티는 `StatusAppliedClientRpc`로 빙결/둔화/버프 동기화(회복은 HP 동기화로 재현). 실기 확인: 공격버프·빙결·둔화(0.5)·회복·무상태 회귀 + 순수 클라 재현 모두 PASS. **정리(cleanup):** 개발용 진단 로그 코드를 LogRules 준수 위해 제거(`IRuntimeLogSink`/`RuntimeLoggerSink` 삭제, 상시 기능 아님 — 로그 파일은 LogRules대로 보존), 좌표화 때 주석 비활성화했던 코드 3곳 삭제. **유지 중인 테스트 스캐폴딩:** 종족별 플레이스홀더 스킬 5슬롯(1 폭탄A/2 빙결/3 공격버프/4 둔화/5 회복), 임시 텍스트 라벨 — 최종 기획·아이콘 전까지 테스트용. **미완(별도 작업·과대 표기 금지):** ~~건물 파괴 시 패널/조준 UI 원복 미구현~~ → **2026-08-08 구현 완료·실기 PASS(위 참조)** · 구체 스킬 목록/수치/아이콘(기획) 보류. 아래 "완료된 시스템 > 스킬 건물 시스템" 참조.
**구현 완료 (2026-08-04):** 스킬 건물 시스템 Phase 1(타입 A 즉발 범위 피해 · 타입 B 장판 DoT + 프레임워크 골격 + 3×3 패널 UI·쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 렌더링 + 취소 버그 수정·쿨다운 안내 토스트) **구현 완료 · 실기기 테스트 PASS**. 조준 중심을 타일 스냅(HexCoord) → 연속 도메인 월드 Vector3로 좌표화 — **착탄 반경 판정은 원래 연속 원이라 무변경, "중심 좌표"만 연속화**하고 서버 재검증을 유효타일(HasTile) → 맵 경계 안 점(point-in-bounds, `HexMetrics` 경계 헬퍼 신규)으로 교체(최외곽 타일 바깥선까지 엄밀 clamp). 조준원은 지형(ProBuilder 실린더)과 coplanar z-fighting으로 파묻히던 것을 신규 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader`(ZTest LEqual + Offset -1,-1 + ZWrite Off + Cull Off)로 지형엔 안 가려지고 유닛/건물엔 가려지는 지면 데칼로 해결(셋업 스크립트가 머티리얼 자동 생성·배선). 실기(Android) 취소 버그 — 손 뗀 프레임에 합성 마우스 좌표(0,0)가 유효로 반환돼 캐시 폴백을 가로채 취소 X 위에서 손을 떼도 발동·쿨다운이 걸리던 문제 — 를 release 프레임엔 라이브 좌표를 읽지 않고 캐시된 마지막 드래그 좌표로만 취소/발동 판정하도록 근본 수정. 쿨다운 중 스킬 탭 시 조용히 무시하던 것을 ToastUI(`ToastKey.SkillOnCooldown` + `ToastMessageConfig` key:4 "스킬이 쿨다운 중입니다") 안내로 개선. **미완(당시 기준·별도 작업·과대 표기 금지):** 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2 — **이후 2026-08-05 구현 완료(위 참조)** · 건물 파괴 시 패널/조준 UI 원복 — **이후 2026-08-08 구현 완료(위 참조)** · 구체 스킬 목록/수치(기획) 보류. 아래 "완료된 시스템 > 스킬 건물 시스템 Phase 1" 참조.
**구현 완료 (2026-07-31):** 연구소 유닛 강화 시스템(공/방/속 + 초월 자연회복) + 전투 스탯 ×10 스케일 + 연구 패널 UI(매트릭스 2-레이어) **구현 완료 · 멀티플레이 실기 PASS**. ×10은 config `.asset`에 ×10 커밋 반영(적용에 쓰였던 셋업 스크립트는 역할 종료 후 제거됨). **후속 보류:** UI 레이아웃 다듬기·매트릭스 헤더 아이콘·AI 연구 사용 실기·MistShrine 힐(**2026-08-12 구현 완료 / 멀티 미검증**)·싱글 자연회복 실기. 아래 "완료된 시스템 > 연구소 유닛 강화 시스템" 섹션 참조.
**현재 단계:** MistShrine(HealShrine) 물안개 힐 **구현 완료 · 에디터 싱글플레이 실기 검증 완료 / 멀티 미검증**(2026-08-12 — 위 항목 참조. 범위 판정·아우라 끊김·회복량·중첩 해소를 로그 실측으로 확인. **멀티 실기·물안개 VFX·버튼 아이콘·밸런싱 수치는 미완**). 직전 랠리포인트 조준 시 반투명 오버레이 잔존 버그 수정·실기 테스트 PASS(2026-08-08, `ProductionPanelUI` 2줄 추가 — 조준 진입 시 `HideBlockingOverlay()` + `Close()`를 우회하는 랠리 완료 경로의 참조 카운터 반납). 직전 건물 파괴 시 열린 패널/조준 UI 원복 구현·실기 테스트 PASS(2026-08-08, `BuildingPanelBase`가 `OnBuildingDied` 구독→현재 건물 매칭 시 `Close()`, 4개 건물 패널 공통 커버·`.AddTo(this)` 해제). 직전 스킬 건물 시스템 Phase 1(타입 A·B + 프레임워크 + 3×3 패널 UI/쿨다운 오버레이 + 모바일 탭 조준 + 조준 지점 연속 좌표화 + 조준원 지면 데칼 + 취소 버그·쿨다운 토스트) 구현·실기기 테스트 PASS(2026-08-04)에 이어 타입 C(전역 상태변경: 버프/디버프/CC/힐) Phase 2도 구현·실기+멀티(클라) PASS(2026-08-05) — 스킬 관련 잔여는 구체 스킬 목록/수치/아이콘(기획)뿐. 직전 연구소 유닛 강화 시스템 + 전투 스탯 ×10 개편 구현·멀티 실기 완료(2026-07-31). 직전 QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료(2026-07-20, QA PASS·**멀티플레이 실기 로그 검증 완료**) — 마지막 남은 특수 유닛으로, 이로써 **특수 유닛 5종(BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/QuakeSpirit) 전량 + InfernoSpirit DoT까지 전부 완료**. 착탄형 **즉발** AoE(DoT 아님): 주 타깃 1마리 **직접 100%(공격력 200)**(스플래시 제외, 건물이 주 타깃이면 100%로 공성) + 착탄 **월드 좌표 원형 반경**(`_quakeRadius` 기본 1.0=인접 1칸) 내 **다른 적 유닛·적 건물 50%**(올림 `CeilToInt(200×0.5)`=100, 주 타깃 제외). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함**(그쪽 두 유닛의 AoE는 유닛만 — 차별점). 아군 무피해, 서버 권위. 구현: 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`) + 레지스트리 `QuakeSpirit → QuakeAttackBehavior` 1줄, MushroomBomber 원형 반경 헬퍼(`CollectEnemyUnitsInRadius`)를 `internal static`으로 공용화 재사용(MushroomBomber 유닛만 로직 무변경) + 건물 순회 `CollectEnemyBuildingsInRadius` 신설 + 유닛/건물 hit-set 분리(규칙 29 계승), `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5) GameBootstrapper 주입(폴백 1.0/0.5). **멀티플레이(Host/Client) 실기 로그로 100%=20·스플래시=10·건물 스플래시·주 타깃 제외·반경 이내·아군 무피해·서버↔클라 동기화 전부 정상 검증**(로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`). 규칙 43 등재. **알려진 이슈(보류, 별도 task 분리)**: ① QuakeSpirit 타격 애니↔데미지 텍스트 타이밍 어긋남 — Attack 클립 `OnAttackHit` 미주입 + placeholder `hitFrameTimes`(1.0s)로 스플래시 텍스트가 `HitPresentationQueue` 타임아웃(쿨다운×1.5=7.5s)까지 지연(피해·판정·동기화 자체는 정상), ② 멀티 원거리 공격 facing 버그(InfernoSpirit 작업에서 진단) — 둘 다 사용자 결정으로 이번 미수정. 직전 InfernoSpirit(지옥불 정령) 단일 대상 지속 피해(DoT) 구현 완료(2026-07-20, QA PASS·사용자 실기 테스트 완료) — 특수 유닛 5종 외 추가 DoT 유닛. 이미 완성된 원거리 유닛(에셋·VFX·생산·`OnAttackHit` 완비)에 DoT 특수만 추가. 원거리 공격이 주 타깃 1마리에 **직접 250**(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성 포함) + 특수 핸들러 `InfernoAttackBehavior`가 그 **주 타깃 1마리에게만** DoT 50/초×3초(총 150) 부여(AoE 아님·반경 없음). DoT는 **적 유닛만**(건물 제외 — 건물은 직접 250만), 주 타깃 유닛은 직접+DoT, 아군 무피해, 갱신=리셋. DoT는 MushroomBomber에서 만든 **규칙 40 초 단위 discrete 틱 시스템을 단일 대상으로 재사용**(1초 간격·틱당 올림·총량 150 클램프·매초 남은 체력 데미지 텍스트·서버 권위). InfernoSpirit(50/3)은 MushroomBomber(20/3)와 **별도 필드·델리게이트·진입점**(`_infernoDot*`/`ApplyInfernoDot`)으로 분리 — MushroomBomber 회귀 없음. 튜닝값 `SpecialAttackConfig`(`_infernoDotPerSecond`/`_infernoDotDuration`), GameBootstrapper 주입(폴백 50/3). 레지스트리 `InfernoSpirit → InfernoAttackBehavior` 1줄. 규칙 41~42 등재. **잔여 특수 유닛(레지스트리 확인 결과 QuakeSpirit이 유일 미구현 — BattleAxe/TorrentSpirit/MushroomBomber/InfernoSpirit 등록됨, BloomFairy는 힐러 전용 경로로 의도적 미등록)**: QuakeSpirit(착탄형 — MushroomBomber 원형 반경 헬퍼 재사용 예정). **알려진 이슈(보류)**: 멀티플레이 원거리 공격 시 유닛이 타겟을 정확히 안 바라보는 facing 버그 — 진단상 InfernoSpirit 에셋은 정상 유닛(FlameSpirit)과 동일 → 멀티 원거리 facing 공유 로직 문제로 추정(근접 유닛은 안 보임). 사용자 결정으로 이번 미수정·보류, 상세는 task Plan 참조. — 직전 MushroomBomber(버섯폭격기) 착탄형 범위 딜러 구현 완료(2026-07-19, QA PASS·사용자 실기 테스트 완료) — 특수 유닛 5종 중 **4번째**. 착탄형 AoE: 주 타깃 1마리 **직접 100**(건물 공성 포함, 기존 `ExecuteAttack` 단일 피해 `ReplacesPrimaryAttack=false`) + 착탄 중심 **월드 원형 반경**(`blastRadius` 기본 1.0=인접 1칸) 내 적 유닛 **DoT 20/초×3초(총 60)**. DoT는 HoT/DoT 공용 시스템(규칙 34)에 신설한 **초 단위 discrete 틱 모드** — 1초 간격·틱당 올림(CeilToInt)·총량 60 클램프·매초 남은 체력 데미지 텍스트(힐과 반대로 억제 안 함), 서버 권위. 핸들러 `BlastAttackBehavior`(원형 반경 수집 static 헬퍼 — QuakeSpirit 재사용 여지) + 레지스트리 `MushroomBomber → BlastAttackBehavior` 1줄. 식물 라인 생산 배선 완료(SporePatch 1단계=MushroomBomber, FloralNursery 2단계=BloomFairy). 규칙 38~40 등재. VFX(투사체·폭발)는 사용자 별도 제작. 직전 **BloomFairy(꽃요정 힐러)** 구현·실기 테스트 완료(2026-07-18) — 특수 유닛 5종 중 **3번째**. 적을 때리지 않고 부상 아군을 회복하는 **힐러 전용 경로**(적 공격 흐름·`SpecialAttackRegistry`와 분리), 부상 아군 탐색(팀 필터 반대·본인 포함), HoT 공용 시스템(규칙 34 diff 틱)·힐 유휴 감시·쿨다운 예외(발동 준비 1.0s 미포함 → 실제 힐 주기 4.0s, 프로젝트 유일 예외)·HoT 힐 텍스트 완료 시 1회 표시(사망 시 생략). 규칙 32~37 등재. **잔여 특수 유닛(코드 확인 결과 특수 공격 핸들러 미등록 = 미구현)**: QuakeSpirit(착탄형 — MushroomBomber 원형 반경 헬퍼 재사용 예정), InfernoSpirit(DoT — 초 단위 틱 재사용 예정). 그 이전 TorrentSpirit(물의 상급 정령) 파도형 이동 AoE + 힐 서브시스템 구현 완료(2026-07-17) — 특수 유닛 5종 중 **2번째**. special-only(단일 공격 없음, `ReplacesPrimaryAttack`) 파도가 전방으로 이동(서버 권위 전선 `SpawnWave`/`TickWaves`)하며 월드 직사각형 안 **적 유닛·적 건물 피해 / 아군 유닛 힐**(각 1회, hit-set). **힐 서브시스템 신설**(`UnitData.Heal`·`OnEntityHealed`·NetworkHealthSync 힐 동기화·치유 색상 텍스트 — BloomFairy 공용). `SpecialAttackConfig` 파도 파라미터(폭 3·전방 3·이동시간 0.5·힐 100) Inspector. `VfxPoolItem` 다중 파티클 시스템(빈 루트+형제) 재생 수정. UnitStatsConfig(18)/EffectPreset/OnAttackHit(0.5s 임시) 배선. QA CONDITIONAL PASS(BUG-002 건물 공격 수정 완료). 규칙 28~31 등재. VFX 세부 튜닝은 사용자 진행. — 직전 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 전략 핸들러 아키텍처 구현 완료(2026-07-17, 사용자 실기 PASS) — 특수 유닛 5종(BattleAxe/QuakeSpirit/TorrentSpirit/MushroomBomber/BloomFairy) 중 첫 구현. `ISpecialAttackBehavior` + `SpecialAttackRegistry`(UnitType 키) + `SweepAttackBehavior` 신설, `UnitCombatUseCase.ExecuteAttack`을 `ApplyDamageToVictim` 헬퍼로 정리 후 특수 공격 훅 1줄 추가(신규 유닛 = 핸들러 + 등록 1줄). 휩쓸기 판정은 월드 좌표 전방 부채꼴(반경 `sweepReach` 실기값 0.75 · 반각 120°, `SpecialAttackConfig` SO 튜닝), AoE 피격 연출 동시 방출(HitFrameTimes.Length 분기). BattleAxe attackRange 0.5→0.75, Attack 클립 OnAttackHit 1.1667s 주입. 규칙 23~27 등재. 직전 Android AAB 빌드 용량 최적화 완료(2026-07-15, main 반영) — `codex/asset-size-optimization` 작업이 main에 병합되어 AAB 용량을 **190.66 MB → 125.30 MB**로 65.36 MB 절감. 핵심 변경은 3D 건물/유닛 텍스처 Android max texture size `1024 → 512` 적용이며, `_Old` 미사용 에셋 정리와 보수적 FBX import 조정도 함께 수행. UI 스프라이트/초상화/건물 아이콘/UI 배경/TMP 폰트는 최종 품질 확인 전 유지. 직전 코드 정리 3건(2026-07-13), 이동/Walk 애니메이션 동기화(2026-07-13), 전투 타격 타이밍 동기화(2026-07-12) 완료 상태 유지. 싱글플레이 AI 시스템은 Inspector 작업(AIConfig/AIScenarioConfig 3종족 에셋 생성, DifficultySelectView 레이아웃 배치)이 완료되었고, 핵심 흐름(유닛 생산/건물 업그레이드) 실기가 조건부 완료(2026-07-16, PASS·특별한 문제 미발견)됨 — 단 반응 시스템(R1~R3)·3종족 시나리오 무작위 동작 등 세부 정밀 검증은 미완. 후속은 기기 QA로 3D 텍스처 품질 확인, 피격 VFX 프리셋 연결, 잔여 특수 타격(QuakeSpirit/InfernoSpirit) 클립 이벤트, Firebase/EDM 저장소 방침 정리, AI 반응 시스템·3종족 시나리오 정밀 검증, 신규 유닛 프리팹 실기 테스트. **병행 관찰 항목 — 매치메이킹 404(호스트 결정 단계) 수정(A방식, 2026-07-17, 브랜치 `claude/matchmaker-404-error-pi9qdn` 커밋 `a3dbc73`)**: 랜덤 매칭 후 호스트 결정을 매치 결과 조회(P2P 클라 호출 시 404) → Lobby CreateOrJoin(matchId=lobbyId) 원자 선점으로 전환. 초기 매칭 실기에서 404 없이 정상 연결 확인했으나 간헐(intermittent) 버그라 지속 테스트 중(확정 PASS 아님) — 비활성화(주석)한 레거시 코드는 지속 테스트 확정 후 삭제 예정. 상세는 하단 매치메이킹 404 섹션 참조. **※ 2026-08-10 ×10 스케일 반영 정정:** 이 "현재 단계" 문단의 전투 수치(직접 피해·DoT 틱값·파도 힐량)를 ×10 스케일 개편(2026-07-31) 기준으로 정정했다. **비율·반경(100% / 50% / ×0.5 / 반경 1.0)과 쿨다운·사거리·비용은 ×10 대상이 아니라 그대로다.** 단, 위 QuakeSpirit 문장 안의 **2026-07-20 멀티 로그 검증 기록(100%=20·스플래시=10)은 당시 측정 사실이므로 원문을 보존**한다(현재 유효 수치는 200/100). 아래 "✅ 완료 (2026-07-19/07-20)" 표들도 날짜가 박힌 완료 이력이라 원문 보존한다. 단일 진실 소스: `StatsReference.md`.

---

## 전체 구현 현황

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

#### MistShrine 물안개 힐 시스템 ✅ 구현 완료 · 에디터 싱글플레이 실기 검증 완료 (2026-08-12) — **멀티 미검증**

초월(Transcendence) 종족의 **공격하지 않는 힐 건물** MistShrine(`BuildingType.HealShrine` = 6).
2026-08-10 재설계 기획 확정 → **코드·프리팹·씬 배선 구현 완료**, 2026-08-12 **에디터 싱글플레이 실기 + 런타임 로그 실측으로 검증**.

**✅ 검증 완료 항목 (에디터 싱글플레이 실기 / 로그 실측)**

| 항목 | 결과 |
|------|------|
| 범위 원 경계 = 실제 회복 판정 | **정상** — 회복된 최대 거리 **2.29** / 회복에서 탈락한 최소 거리 **3.12**, 설정 반경 **3.00**과 모순 **0건** |
| 범위 이탈 시 즉시 끊김 (규칙 9) | **정상** — 거리 `2.29(Healed)` → `3.77(OutOfRange)` 이탈 **다음 틱부터 HP 고정** |
| 회복량 | **정상** — 매 틱 **+10** (`_mistHealPerSecond: 10`과 일치) |
| 중첩 해소 (규칙 13) | **수정 후 정상** — 후보 물안개 4개에서도 대상당 **1회만** 적용, 거리 동률(0.87)에서 **Id 작은 신전** 선택 |
| 자동/수동 토글 · 쿨다운 방향 · 적 건물 미반응 · 연구/스킬 패널 `ClosedFrame` 가드 | 정상 (육안 확인) |
| 컴파일 | 오류 0건 (사용자 에디터 확인) |

**⚠️ 아직 완료가 아닌 것 (과대 표기 금지)**

- **멀티플레이 실기 미검증** — 검증은 **에디터 싱글플레이로만** 이루어졌다. 범위 판정·중첩 해소·회복량 계산은 싱글·멀티가 **같은 `MistShrineUseCase` 경로를 공유**하므로 판정 로직 자체는 유효하지만, **건물 HP 동기화(`SyncBuildingHealClientRpc`)·클라이언트 표시·RPC 팀 검증·쿨다운 로컬 미러·이중 틱 여부는 멀티에서만 도는 경로이며 한 번도 실행되지 않았다.**
- **물안개 지속 VFX 미제작**(규칙 26) — 물안개는 현재 **눈에 보이지 않는다.**
- **사용(시전) 버튼 아이콘 미제작**(UI 규칙 15) — 임시 텍스트 라벨.
- **밸런싱 수치 미확정**(규칙 16) — 현재 값은 전부 임시값. 확정 시 `SpecialAttackConfig.asset` 한 곳만 교체하면 된다.

**🐞 구현 중 발견·수정한 규칙 위반 버그 — 중첩 해소가 동작하지 않던 문제 (커밋 `be17148`)**

- **증상(로그 실측):** 거리 0.87로 겹친 신전 2개가 같은 유닛을 각각 회복시켜 **3초에 +60**(기댓값 +30) — **초당 회복량 2배**. 규칙 13의 "가장 가까운 하나"도 어긋나 **더 먼 신전까지** 회복을 적용했다.
- **원인:** 물안개마다 틱 누적기가 독립이라 **시전 시각이 다르면 발화 프레임이 달라진다**(실측 위상 `.36x` / `.73x`, 동시 틱 **0건**). 그런데 중첩 해소는 **"이번 프레임 발화분"끼리만** 비교하는 구조여서 비교 대상이 늘 하나뿐 → **해소 코드가 돌 기회 자체가 없는 죽은 코드**였다.
- **수정(두 겹):** ① **위상 정렬** — 새 물안개의 누적기를 0이 아니라 **기존 물안개와 같은 값**에서 출발시킨다 ② **소유권 판정 분리** — **활성 물안개 전체**에서 대상별 최근접 소유자를 정하고 그 소유자가 발화한 틱에만 적용.
- **규칙 정합:** 규칙 8-1이 요구하는 것은 "물안개마다 자기 누적기"이지 "시작 위상이 제각각"이 아니다 — **누적기 개수는 물안개 개수 그대로**(규칙 14 표). 매 틱 재수집 구조도 무변경이라 규칙 9 유지. **이 불변식을 규칙 8-1에 보강 기재**해 되돌림을 막았다.
- **건물 경로도 함께 고쳐졌다** — 규칙 13은 "한 대상"이므로 유닛만의 문제가 아니었고, 건물 해소 코드도 같은 이유로 돌지 못하고 있었다.
- **진단 로그:** `848d891`(추가) → `939bd87`(세션 배선) → `be17148`(확장) → `cfe73bb`(전량 제거). 로그 파일은 `_Logs/2026-08-10/14_12_mistshrine-heal-implementation/RuntimeLog_host.txt`에 **영구 보존**(LogRules).

**📋 확정 사양 (규칙 1~27)**

- **동작:** 시전 → 건물 중심 고정 원형 범위에 물안개 생성 → 지속 시간 동안 **1초 단위 discrete 틱**으로 회복 → 물안개 소멸 → 쿨다운 잔여 → 재사용. **물안개 지속시간 < 쿨다운**(다운타임 존재)
- **대상:** 범위 안의 **아군 유닛 + 아군 건물**(시전 건물 자신·본기지 Castle 포함). 최대 체력 대상은 회복 없음
- **아우라 방식:** 시전 시점 스냅샷이 아니라, 범위를 벗어나면 즉시 회복이 끊김
- **시전 비용 없음**(쿨다운으로만 제어), **시전 건물 파괴 시 물안개 즉시 제거**, 물안개는 이동하지 않음
- **중첩:** 물안개끼리는 중첩되지 않음(더 가까운 건물 우선, 거리 동률이면 **건물 Id가 작은 쪽** — 결정적 규칙). 단 **연구소 자연회복과는 별개 효과로 중첩 적용**
- **UI:** `BuildingPanelBase` 상속 전용 패널 신설(`BuildingSkillPanelUI` 재사용 불가 — 5슬롯·지점 조준 전제). 짧은 탭=수동 시전 / 롱프레스=자동 모드 토글 / 자동 중 탭=해제, **자동 모드 기본 OFF**. 쿨다운은 `SkillCooldownOverlay` 재사용. 범위 표시는 **아군 건물 + 패널 열린 동안만**(적 MistShrine은 표시 안 함). 회복 텍스트는 실제 회복 대상만·표시 주기 분리(임시 3초)
- **로직:** `SkillActivationUseCase` 재사용 불가(`IsSkillBuilding` 게이트가 `HealShrine` 차단 + 슬롯/로드아웃 전제) → **전용 UseCase 신설**. 쿨다운 관리 패턴(Dictionary + 총 쿨다운 보관 + 서버 틱 + **클라 로컬 미러**)만 차용. 자동 토글 동기화는 생산 시스템의 `Request → ServerRpc(팀 검증) → ClientRpc` 3단 구조
- **신규로 필요했던 것 (VFX 제외 전부 구현 완료):** 아군 유닛·건물 수집 헬퍼(기존 원형 반경 헬퍼는 **전부 적 대상 전용**이었음) · **건물 회복 경로**(`BuildingData.Heal` 신설 + 멀티 동기화용 전용 RPC) · 파괴·철거 시 물안개/자동모드/쿨다운 정리 경로. **물안개 VFX만 여전히 미제작**(현재 MistShrine VFX는 destroy/upgrade뿐)
- **미확정(밸런싱):** 회복량 · 물안개 지속시간 · 쿨다운 · 범위 반경 · 회복 텍스트 표시 주기. `StatsReference.md`의 기존 값(HP 500 / 건설비 100 / 10 HP/s·범위 3)은 재설계 이전 수치로 재검토 대상
- **함께 정정한 문서 오류:** GDD·TDD가 Transcendence 방어 타워를 MistShrine으로 적고 있었으나 **정확한 방어 타워는 `VineTower`(`AutoTower` = 2)** 다. `AssetList.md`·`StatsReference.md`·`BuildingType.cs`·`GameSystemRules_Buildings.md`는 원래 올바르게 구분하고 있었음
- **문서:** `GameSystemRules/GameSystemRules_Buildings.md`(MistShrine 물안개 힐 시스템 규칙 1~27 — **2026-08-12 규칙 8-1에 "활성 물안개 위상 공유" 불변식 보강** · 특수 건물 2종 이상 시 `GameSystemRules_SpecialBuildings.md`로 분리한다는 분기 시점 명시), `GameSystemRules/GameSystemRules_UI.md`(MistShrine 패널 UI 규칙 1~15 — 규칙 15는 아이콘 미제작), GDD §5, TDD, `StatsReference.md`, `GameSystemRules_Upgrade.md`
- **task:** `_Tasks/2026-08-10/09_34_mistshrine-heal-redesign/`(기획) · `_Tasks/2026-08-10/14_12_mistshrine-heal-implementation/`(구현 — §9 보완 수정 4건 · §10 완료 판정)

#### 스킬 건물 시스템 (Phase 1 타입 A·B + 조준/UI/좌표화/렌더링/버그수정, Phase 2 타입 C 전역 상태변경) ✅ 구현 완료 · 실기기 테스트 PASS (Phase 1: 2026-08-04, Phase 2: 2026-08-05 실기+멀티(클라) PASS)

스킬 건물(FlightFacility / MagicSpirit / WillowShrine)로 액티브 스킬을 발동하는 프레임워크와 타입 A(즉발 범위 피해)·B(장판 DoT)·C(전역 상태변경: 버프/디버프/CC/힐) 실행기, 모바일 탭 조준, 전용 UI를 구현하고 실기기 테스트로 검증(타입 C는 멀티 클라이언트 재현까지 확인). SSoT는 `GameSystemRules/GameSystemRules_Skills.md`(규칙 1~26). task: `_Tasks/2026-07-28/12_14_skill-building-system-design/`(프레임워크 설계·Phase 1·Phase 2) + `_Tasks/2026-08-04/04_46_skill-aim-coordinate-based/`(조준 좌표화·렌더링·버그수정).

| 항목 | 상태 | 비고 |
|------|------|------|
| 프레임워크 골격 (데이터 주도) | ✅ 완료 | Domain `Skill/SkillMechanicType.cs`(A/B/C **전부 구현**)·`Skill/SkillAimType.cs`(Instant/PointTarget). Application `Skill/{SkillData,ISkillExecutor,SkillActivationContext,SkillExecutorRegistry}`·`UseCases/SkillActivationUseCase.cs`(발동 단일 진입점 `Activate`, 플레이어/AI 공유). 건물 글로벌 쿨다운=UseCase `Dictionary<int,float>`(BuildingData 무변경). 로드아웃=RaceId 키(`SkillLoadoutConfig:ISkillDataProvider` — MagicBuilding enum 공유라 종족 분기). 규칙 1~7 |
| 타입 A — 즉발 범위 피해 실행기 | ✅ 완료 | `Application/Skill/InstantAreaDamageExecutor.cs`. 건물 출처 피해=`UnitCombatUseCase.ApplySkillInstantAreaDamage`(방어 감쇄 O, **Tank 2배 미적용**). 반경 수집은 도메인월드(`_mapper.HexToWorld`)+TeamId 기준 전용 헬퍼(`CollectEnemy{Units,Buildings}InRadiusDomain`). 규칙 11 |
| 타입 B — 범위 지속 피해(장판 DoT) 실행기 | ✅ 완료 | `Application/Skill/AreaDotDamageExecutor.cs`. `ApplySkillAreaDot`(무감쇄 DoT=`ApplyDamageOverTime(source:null)`, 규칙 40 초 단위 틱 재사용). 규칙 12 |
| 타입 C — 전역 상태변경(버프/디버프/CC/힐) 실행기 | ✅ 완료 (2026-08-05, 실기+멀티 PASS) | Domain `Status/{StatusEffectKind,StatusEffect,UnitStatusState}.cs`(순수 계산) + Application `Services/StatusEffectSystem.cs`(서버 권위 부여/틱) + `Skill/GlobalStatusChangeExecutor.cs`(조준 없음 전역 즉시). `StatusEffectKind`=None/MoveSpeedMul/AttackPowerMul/AttackDisabled/Freeze(이속0+공격불가)/HealOverTime. 유효 스탯 접근자(`EffectiveAttack`/`GetUnitMoveSpeedMultiplier`)에 상태 배율 **곱연산 합성** + 공격 게이트 `CanAttack`. **무상태면 기존과 완전 동일(회귀 안전).** 빙결=`Animator.speed=0` 애니 정지(`UnitAnimState.Frozen` 클라 동기화), 둔화=이동 코루틴 매 프레임 배율 재조회로 라이브 반영, 회복=기존 HoT 재사용. 규칙 13 |
| 타입 C — 멀티 동기화 | ✅ 완료 | `StatusAppliedClientRpc`로 빙결/둔화/버프 브로드캐스트(클라 자기 유닛 재현, 서버 skip), 회복은 HP 동기화로 재현(이중 힐 방지) |
| 타입 C — UI 버튼 균일화 | ✅ 완료 | 스킬 슬롯 CostContainer를 `SetActive(false)`(행 높이 붕괴) 대신 CanvasGroup alpha=0(HideChildKeepLayout)로 숨겨 행 높이 보존 |
| 3×3 건물 패널 UI + 쿨다운 오버레이 | ✅ 완료 | `Presentation/UI/{BuildingSkillPanelUI,SkillCooldownOverlay}.cs`. 슬롯 1~5 스킬/6 철거/7~9 예약(규칙 8·9). 쿨다운 오버레이=버튼 전체 덮는 어두운 반투명 + 12시 기점 시계방향 소멸(`fillOrigin=Top`+`fillClockwise=false`)+남은 초 텍스트, 유휴 시 숨김(규칙 10). BuildingSkillPanel=BuildingActionPanel 복제로 룩 상속 |
| 모바일 탭 조준 (2단계) | ✅ 완료 | `Presentation/Input/SkillAimController.cs`. 버튼 탭 → 조준 모드 진입(패널 닫힘·조준원·취소 X 표시, 버튼 자신 release 무시) → 새 화면 press·드래그로 조준 이동+엣지 스크롤 → release로 발동, 하단 X 위 release로 취소. X hover 시 취소버튼 Lerp 확대 예고(규칙 20-1). static `IsAiming` 가드로 CameraController.HandlePan/InputHandler.HandleClick 억제. 규칙 15~21 |
| 조준 지점 연속 좌표화 (HexCoord → Vector3) | ✅ 완료 (2026-08-04) | 조준 중심을 타일 스냅(HexCoord) → **연속 도메인 월드 Vector3**로. **착탄 반경 판정은 원래 연속 원(중심 월드 + 반경 유클리드)이라 무변경 — "중심 입력"만 연속화**. 전 계층 시그니처 Vector3화(SkillAimController/BuildingSkillPanelUI/`SkillActivationUseCase.Activate(Vector3?)`/`SkillActivationContext.AimWorld`/`UnitCombatUseCase.ApplySkill*(Vector3 center)`/`NetworkSkillController` RPC=NGO Vector3 기본 직렬화, int q,r 폐지). 규칙 19·26 |
| 맵 경계 point-in-bounds 재검증 (신규 헬퍼) | ✅ 완료 (2026-08-04) | 서버 재검증을 유효타일(HasTile) → **맵 경계 안 점(point-in-bounds)**으로 교체. `Core/HexMetrics.{ComputeMapWorldBounds,IsWithinMapBounds,ClampToMapBounds}` 신규(최외곽 타일 중심 극값 + 반칸 AABB = 최외곽 타일 바깥선까지 엄밀 clamp). HexGrid(Domain)는 Vector3 불가 → Core 수학 + 클로저 주입(GameBootstrapper `_grid` 캡처=맵 재로드 대응). 규칙 22·26 |
| 조준원 지면 데칼 렌더링 (z-fighting 해결) | ✅ 완료 (2026-08-04) | 원인=조준원(y=0.05)과 HexTile(ProBuilder 실린더) coplanar → z-fighting으로 파묻힘. 신규 셰이더 `Assets/_Project/Shaders/SkillAimOverlay.shader`(Transparent + ZWrite Off + **ZTest LEqual + Offset -1,-1** + Cull Off) = 지형엔 안 가려지고 불투명 유닛/건물엔 정상 가려지는 데칼. **ZTest Always 금지**. 머티리얼은 셋업 스크립트(`SkillSetup_Scene.EnsureOverlayMaterial`)가 생성·3겹 SpriteRenderer + `SkillAimReticle._overlayMaterial` 배선. 타일 Y Scale(등각) 무변경. 규칙 22-1 |
| 취소 버그 근본 수정 (2026-08-04) | ✅ 완료 (2026-08-04) | 실기(Android) 터치에서 취소 X 위에서 손을 떼도 발동·쿨다운이 걸리던 문제. 원인=손 뗀 프레임에 `TryGetPointerScreenPos`의 마우스 분기가 합성 마우스 좌표(0,0)를 유효로 반환해 캐시 폴백을 가로챔. 수정=release 프레임엔 라이브 좌표를 읽지 않고 캐시된 마지막 드래그 좌표(`_lastDragScreenPos`)로만 취소/발동 판정 |
| 쿨다운 스킬 안내 토스트 (2026-08-04) | ✅ 완료 (2026-08-04) | 쿨다운 중 스킬 탭 시 조용히 무시하던 것을 안내. 기존 ToastUI(에셋 기반)에 `ToastKey.SkillOnCooldown` 추가 + `ToastMessageConfig.asset` key:4 "스킬이 쿨다운 중입니다"(`BuildingSkillPanelUI`에서 `ToastUI.Show`) |
| 멀티플레이 / 서버 권위 | ✅ 완료 | `Infrastructure/Network/NetworkSkillController.cs`(NetworkBehaviour). 좌표만 전송 + 서버 재검증(건물 생존·글로벌 쿨다운·맵 경계 안 점). 발동 성공 시 `SkillActivatedClientRpc`로 양 클라 쿨다운 미러. 쿨다운 틱=싱글 GameBootstrapper.Update / 멀티 서버 NetworkCombatController.TickCombat / 순수 클라 오버레이 미러 감소(이중 틱 금지). 규칙 25·26 |
| 건물 파괴 시 패널/조준 UI 원복 | ✅ 완료 (2026-08-08, 실기 PASS) | 스킬 포함 4개 건물 패널(생산/건물액션/스킬/연구) 공통 갭 해소. `BuildingPanelBase`가 `GameEvents.OnBuildingDied` 구독 → 파괴 건물이 현재 표시/조준 중인 건물(`_currentBuilding.Id` 매칭, 조준 중엔 `IsOpen=false`라 IsOpen 아닌 Id로 판정)이면 `Close()` → 각 패널 `OnBeforeClose`로 조준 취소(`CancelAim`)·랠리 마커 숨김 자동 연계. **코드 변경 `BuildingPanelBase.cs` 1파일**(자식 무변경). 구독 해제=`.AddTo(this)`(자식 `ResearchPanelUI` 자체 `OnDestroy`가 베이스 은닉하는 회귀 회피). 멀티=`NetworkCombatController.HandleBuildingDied` 클라 재발행으로 커버. 커밋 `8c7fa01` |
| 문서 | ✅ 완료 | `GameSystemRules/GameSystemRules_Skills.md`(규칙 1~26, 조준 좌표화 정정 17·19·22·22-1·24·26 반영·구현 상태 갱신) + 건물 파괴 원복은 공통 UI 규칙(`GameSystemRules_UI.md` 팝업 규칙 11) 명문화 |

> **✅ 타입 C Phase 2 완료(2026-08-05):** `SkillMechanicType.GlobalStatusChange` 실행기(`GlobalStatusChangeExecutor`)와 상태효과 시스템(`StatusEffectSystem`) 구현·실기+멀티(클라) PASS. 유효 스탯 접근자에 상태 배율을 연구 강화 배율과 곱연산 합성(무상태면 회귀 안전). 업그레이드 연동은 이 곱연산 합성으로 확정됨.
>
> **⏳ 후속 / 미완 (별도 작업 — 과대 표기 금지):** ① ✅ **건물 파괴 시 패널/조준 UI 원복 — 2026-08-08 구현 완료·실기 PASS(커밋 `8c7fa01`)**. 스킬 포함 4개 건물 패널 공통 갭을 `BuildingPanelBase`가 `OnBuildingDied` 구독→현재 건물(`_currentBuilding.Id`) 매칭 시 `Close()`로 해소(각 패널 `OnBeforeClose`로 조준 취소·랠리 마커 숨김 자동 연계). 구독 해제는 `.AddTo(this)`(자식 `ResearchPanelUI`의 자체 `OnDestroy` 은닉 회귀 회피). task `_Tasks/2026-08-08/07_40_building-death-ui-restore/`. ② **구체 스킬 목록·수치·아이콘(기획)** — 보류(각 슬롯 1~5의 스킬·쿨다운·반경·지속·피해·상태효과는 추후 ScriptableObject로 확정). **현재는 종족별 타입 A/C 플레이스홀더 5슬롯(1 폭탄A/2 빙결/3 공격버프/4 둔화/5 회복, 임시 텍스트 라벨) = 최종 기획·아트 전까지 유지되는 테스트용.** ③ 둔화 전투종료 정렬 Lerp 구간은 캡처값 사용(미세 잔여, 필요 시 후속).
>
> **정리(cleanup, 2026-08-05):** 개발용 진단 로그 코드를 LogRules 준수 위해 제거(`IRuntimeLogSink`/`RuntimeLoggerSink` 삭제 — 상시 기능 아님, 로그 파일은 LogRules대로 보존), 좌표화 때 주석 비활성화했던 코드 3곳 삭제. 교훈: 로그 작업 착수 전 반드시 `LogRules.md`를 먼저 확인.

#### 연구소 유닛 강화 시스템 + 전투 스탯 ×10 스케일 + 연구 패널 UI ✅ 구현 완료 · 멀티플레이 실기 PASS (2026-07-31)

| 항목 | 상태 | 비고 |
|------|------|------|
| 전투 스탯 ×10 스케일 (config 재조정) | ✅ 완료 | UnitStatsConfig·BuildingStatsConfig·SpecialAttackConfig `.asset`에 ×10 커밋 반영(전투 스탯 ×10 확정). 적용에 쓰였던 셋업 스크립트는 제거됨. HP·공격력 동일 배율 → TTK 불변(상성·매치업 불변). 사거리·이동속도·쿨다운·비용·비율은 불변 |
| 방어력 신규 스탯 + 비율 감쇄 공식(K=120) | ✅ 완료 | 순수 함수 `Domain/Combat/DamageCalculator.ApplyDefense`(floor 1, 하드캡 65%, 방어 0이면 무감쇄 하위호환). 방어 0/8/16/24/32/40. 직격·스플래시·타워→유닛 일괄, DoT 미적용. `UnitStats.StatValues.Defense`/`UnitData.Defense`/`BuildingData.Defense=>0` |
| 유닛별 고정 정수 공격력 / 이동속도 배율 / 힐량 트랙 / 초월 자연회복 | ✅ 완료 | Application `UseCases/UnitUpgradeUseCase.cs` (B) 실시간 배율/증가치 조회(`GetEffectiveAttack`/`GetMoveSpeedMultiplier`/`GetDefense`/`GetRegenPerSecond`/`ScaleByGroupAttack`). 자연회복은 BloomFairy 힐과 별개 채널 |
| 유닛→그룹 매핑 | ✅ 완료 | `Domain/Unit/UpgradeGroup.cs`(`UpgradeGroup`·`UnitUpgradeStat`·`UpgradeGroupHelper`, 순수 Domain 정적 헬퍼) |
| Tank/CannonCart 건물 2배 | ✅ 완료 | `UnitCombatUseCase.ComputeFinalDamage` 건물 분기(대상 건물 + 공격자 Tank/CannonCart → ×2) |
| 팀별 강화 상태 + 서버 권위 + 소급 강화(B) | ✅ 완료 | 유닛 스냅샷 미변경 → 이미 전장에 나온 유닛도 연구 완료 즉시 강화 |
| 네트워크 동기화 (연구 RPC·완료 브로드캐스트·파괴 환불) | ✅ 완료 | Infrastructure `Network/NetworkUpgradeController.cs`. 완료 레벨은 양 클라 브로드캐스트(효과 양쪽 적용), 진행 중은 소유자만, 파괴 시 취소·100% 환불. **MP 완료 처리 서비스 스폰 레이스 버그는 `ResolveServices()`(지연 재조회)로 수정 완료** |
| 연구 패널 UI (매트릭스 2-레이어) | ✅ 완료 | `Presentation/UI/ResearchPanelUI.cs`(`: BuildingPanelBase` — 공통 헤더·닫기·철거+환불) + 매트릭스(`ResearchMatrixView`/`ResearchCellView`) ↔ 진행 게이지(`ResearchProgressView`) 2-레이어, 연구소 단위 전환, 진행 트랙 잠금, 배경 탭 닫기. 규칙 13 |
| 문서 | ✅ 완료 | `GameSystemRules/GameSystemRules_Upgrade.md`(구현 계약·구현 상태·규칙 13 UI), `StatsReference.md`, `GameSystemRules_Units.md` 규칙 44, GDD. old/new 대조: `_Tasks/2026-07-22/10_08_unit-upgrade-system/BalanceReview.md` |

> **⏳ 후속 / 보류 (별도 작업 — 과대 표기 금지):** ① 연구 패널 UI 레이아웃 다듬기(현재 에디터 스크립트 자동생성 골격, 사용자가 Unity에서 직접 다듬을 예정) ② 매트릭스 헤더 아이콘(공/방/속·그룹, 텍스트 라벨 대체 중) ③ AI 시나리오 연구 사용(코드는 있으나 실기 미검증) ④ **MistShrine(HealShrine) 힐 — 2026-08-12 구현 완료 · 에디터 싱글플레이 실기 검증 완료 / 멀티 미검증 · VFX·아이콘 미제작 · 밸런싱 미확정**(위 "완료된 시스템 > MistShrine 물안개 힐 시스템" 참조. 자연회복과는 별개 효과로 중첩 적용) ⑤ 싱글플레이 자연회복 실기 미검증(코드상 정상 예상). ×10 적용·배선·디버그에 쓰였던 에디터·디버그 스크립트는 config `.asset` 커밋·씬 배선 반영 후 제거됨.

#### 코어 게임플레이
| 시스템 | 상태 | 비고 |
|--------|------|------|
| 헥스 그리드 (FlatTop/PointyTop) | ✅ 완료 | 듀얼 Orientation 지원, 런타임 전환 |
| 타일 소유권/점령 | ✅ 완료 (2026-04-26 갱신) | TileOwnershipService — Phase 0/1/2 모든 이동 방식에서 매 프레임 물리 위치 기반 실시간 점령 |
| 금광 타일 시스템 | ✅ 완료 (2026-04-08 갱신) | HasGoldMine, 채굴소 건설 조건, 건물 배치 시 광산 숨김/파괴 시 재표시+타일 중립 복원 |
| A* 경로탐색 | ✅ 완료 (2026-03-18 갱신) | ClaimedTile 기반 아군 차단 (중간 타일만), 목표 타일 blocked 체크 제거 |
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
| 전투 타격 타이밍 동기화 | ✅ 완료 (2026-07-12) | 실기/로그 검증 PASS. **[타이밍 단일 소스]** `HitFrameTimes`를 Attack 클립 `OnAttackHit` 이벤트에서 자동 추출(UnitFactory), 검증 메뉴 `CombatHitEventValidator` + 주입 메뉴 `CombatHitEventInjector`로 13종 클립 이벤트 주입(특수 타격 5종 의도적 제외). **[서버 정밀화]** `NetworkCombatController.TickCombat` 오버슈트를 데미지 딜레이+쿨다운 리셋 양쪽 차감. **[피격 표현 큐]** `EntityDamagedEvent`/`SyncHealthClientRpc`에 공격자 정보 추가, `HitPresentationQueue` 신설(HP텍스트/피격VFX/스케일펀치를 공격자 로컬 타격 프레임에 동기화, 도메인 HP는 즉시=서버 권위), `EffectManager.PlayUnitHit`+`UnitEffectConfig.hitPreset`, `HitReactionPunch`, `FloatingHpTextSpawner` `ShowDamage` 단일 진입점화. **[연출 공백]** 타워 발사 VFX(`BuildingEffectConfig.attackPreset`+`PlayBuildingAttack`), 원거리 트레이서(`TracerProjectile`+`tracerPreset`, 데미지 타이밍 불변). `UnitEffectView.cs` 최종 삭제. 규칙 U-17~U-21(Units)/B-12(Buildings) 등재. task: `_Tasks/2026-07-09/01_12_combat-hit-timing-sync/` |
| 전투 타이밍 검증 중 기존 버그 3건 수정 | ✅ 완료 (2026-07-12) | **[버그1]** `NetworkCombatController.Update()` Tick 이월 잔여분이 다음 Tick의 경과 시간에 이중 계산되어 쿨다운 15~25% 조기 소진(Pistoleer 2.0초 대비 실측 1.71초) → 실제 경과 시간 1:1 감소로 수정. **[버그2]** 피격 표현 큐가 공격자 사망/전투 중단(StopCombat) 시 잔여 항목 미방출 → 즉시 방출 경로 추가. **[버그3]** 클라 Attack 루프 이탈(Walk RPC) 시 `_combatAnimationSent` 잔존으로 StartCombat 재전송 억제 → 유닛이 굳어 보이는 시각 버그(실기 75초 Assault 사례). Walk RPC 전송 시 가드 해제로 수정. 3건 모두 이번 작업 이전부터 존재한 기존 결함으로, 이번 계측이 처음 가시화. 승패 무관. |
| 이동/Walk 애니메이션 동기화 (레벨 동기화 전환) | ✅ 완료 (2026-07-13) | 실기/로그 검증 PASS. **[레벨 동기화]** 유닛 애니메이션 상태(`UnitAnimState` None/Walk/Attack)를 1회성 엣지 RPC(`StartWalkAnimationClientRpc`/`OnUnitWalkStarted` 등)에서 **NetworkUnit의 NetworkVariable(서버 쓰기 / 클라 읽기)** 로 전환. 클라는 `OnValueChanged` + 스폰 시 현재 값 자동 적용 → 갓 생성 유닛의 첫 Walk RPC 스폰 레이스 유실(클라 222/223기 실측) 구조적 소멸. 호스트/싱글플레이는 기존 로컬 직접 제어 유지. **[초기화 후 재적용]** 상태 적용이 `UnitView.Initialize`보다 이르면 애니메이터 미준비로 무음 실패(unit=-1 125건/무귀속 15기) → Initialize 말미 `NetworkUnit.ReapplyAnimStateToView()`(멱등)로 재적용해 봉합. **[위치 보정]** 재경로 첫 스텝이 최종 목적지 역방향으로 향하던 뒤로 밀림(서버 282건 실측) → `MoveTo` `AlignPathStartToTransform`로 첫 스텝이 역방향일 때만 실제 `transform` 전방 타일(`FindForwardClosestTile`)에서 경로 재발급(규칙 11 강제). **[최종 3차 검증]** 로그 15,316줄 — 생성 634기 전원 애니메이션 상태 적용 보장(무귀속 15→0기), Initialize 후 재적용 634회, 역방향 282→41(보정 353건/실이동 278). 잔여 41건은 전부 스폰 직후 우회 경로를 "첫 걸음이 먼 성 직선 방향인가" 지표가 오탐한 계측 한계로 코드 무수정 종결(육안 밀림 없음). **[정리]** `[MOVESYNC-LOG]` 계측 코드 전량 제거, 엣지 Walk 경로 최종 삭제. Plan 대비: `_combatAnimationSent`는 애니메이션이 아니라 데미지(ExecuteAttack)·타겟 RPC 게이팅 가드로 확인돼 주석 처리하지 않고 유지. 규칙 U-22 등재. task: `_Tasks/2026-07-12/07_55_movement-walk-anim-sync/` |
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
| 특수 공격 전략 핸들러 아키텍처 | ✅ 완료 (2026-07-17) | `ISpecialAttackBehavior`(계약) + `SpecialAttackContext`(공격자·주 타깃·유닛목록·피해헬퍼·월드좌표 조회·reach/arc) + `SpecialAttackRegistry`(UnitType→핸들러, 현재 BattleAxe만) + `SweepAttackBehavior`. 모두 `Scripts/Application/Combat/`. 신규 특수 유닛 = 핸들러 추가 + 레지스트리 1줄, `ExecuteAttack` 재수정 불필요. |
| 피해 수렴점 단일화 (ApplyDamageToVictim) | ✅ 완료 (2026-07-17) | 싱글/멀티 공통 `UnitCombatUseCase.ExecuteAttack`의 인라인 단일 피해 로직을 `ApplyDamageToVictim` 헬퍼로 추출(주 타깃/AoE 공용 → 멀티 HP 동기화 일관). `ExecuteAttack` 말미에 특수 공격 훅 1줄 추가. |
| 도끼병 휩쓸기형 AoE (SweepAttackBehavior) | ✅ 완료 (2026-07-17) | 판정 = 월드 좌표 전방 부채꼴. forward=공격자→주 타깃 방향(XZ), 각 적 XZ거리 ≤ `sweepReach` AND 각도 ≤ `sweepArcHalfAngle`이면 피격. Y 무시, 겹친 적 포함, 아군/사망/공격자/주 타깃 제외, 건물 미대상. 월드 좌표는 `IEntityPositionProvider`(서버 권위). 초기 "전방 5타일 타일 기준"에서 실기 후 변경. |
| SpecialAttackConfig 튜닝 SO + 셋업 스크립트 | ✅ 완료 (2026-07-17) | `SpecialAttackConfig`(Infrastructure/Config) — `sweepReach`(기본 1.0, 실기값 0.75)·`sweepArcHalfAngle`(기본 120). GameBootstrapper가 SO값을 float로 UnitCombatUseCase 생성자에 주입(미연결 시 코드 폴백). 에셋 `Resources/Config/SpecialAttackConfig.asset` + `_specialAttackConfig` 배선 완료. 에디터 툴 `Assets/Editor/Setup/CreateSpecialAttackConfigAsset.cs`(메뉴 `Hexiege/Setup/Create SpecialAttackConfig Asset (Game)`)로 에셋 생성+배선 멱등 자동화. |
| BattleAxe 스탯·타격 타이밍 확정 | ✅ 완료 (2026-07-17) | UnitStatsConfig(unitType 5): HP80/공격력15/attackRange 0.75(0.5→조정)/detectRange1/moveSpeed1/attackCooldown3.05/hitFrameTimes[1.1667]/생산20s/골드200/인구1. Attack 클립에 `OnAttackHit` 이벤트를 `Hexiege/Combat/Inject OnAttackHit Events` 인젝터로 주입(1.1667s=클립 타격모션 종료 프레임 35f/30fps). 근거리A 라인(TrainingCamp/WarAcademy/HumanBarracks) requiredStage 3 생산(기존 확인). |
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
| 식물 라인 생산 배선 (에디터 스크립트) | ✅ 완료 (2026-07-19) | UnitFactory `_transcendencePrefabs`에 type 26 등록 + `ProductionPanelUI._buildingUnitMappings` 식물 라인 배선: SporePatch(1단계)=MushroomBomber, FloralNursery(2단계)=BloomFairy(BloomFairy 생산 노출도 이때 완성). 멱등 에디터 스크립트. |
| QA / 실기 테스트 | ✅ QA PASS · 실기 완료 (2026-07-19) | qa-tester PASS + 사용자 실기 테스트 완료. task: `_Tasks/2026-07-19/01_42_mushroombomber-impact-dot/`. |

#### InfernoSpirit(지옥불 정령) 단일 대상 DoT (2026-07-20) — 특수 유닛 5종 중 5번째
| 항목 | 상태 | 비고 |
|------|------|------|
| 단일 대상 DoT 특수 핸들러 (InfernoAttackBehavior) | ✅ 완료 (2026-07-20) | `Application/Combat/InfernoAttackBehavior.cs` 신설(`ISpecialAttackBehavior`, `ReplacesPrimaryAttack=false`) + 레지스트리 `InfernoSpirit → InfernoAttackBehavior` 1줄. 반경 수집 없이 **주 타깃 1마리만** 판정(AoE 아님) — 유닛으로 캐스팅 성공(적 유닛)·`IsAlive`·팀 필터 통과 시 DoT 부여, 건물이면 무동작. 규칙 41. |
| 직접 25 + DoT 역할 분담 | ✅ 완료 (2026-07-20) | 직접 25=기존 `ExecuteAttack` 주 타깃 단일 피해(`ApplyDamageToVictim`, 공격력 25, 건물 공성 포함), DoT=특수 핸들러(주 타깃 적 유닛만). 주 타깃 유닛=직접+DoT, 주 타깃 건물=직접만, 아군 무피해, 직접 25로 주 타깃 사망 시 DoT 스킵. 규칙 41. |
| DoT 초 단위 틱 재사용 (규칙 40) | ✅ 완료 (2026-07-20) | 규칙 40 초 단위 discrete 틱 시스템을 단일 대상으로 재사용 — 5/초×3초(총 15), 1초 간격·틱당 올림(`CeilToInt`)·총량 15 클램프·매초 남은 체력 데미지 텍스트·서버 권위·이중 틱 금지·갱신=리셋. 틱 간격은 MushroomBomber와 동일 `BlastDotTickInterval`(1.0s). 규칙 41. |
| 유닛별 DoT 값 분리 (MushroomBomber 회귀 방지) | ✅ 완료 (2026-07-20) | InfernoSpirit(5/3)은 MushroomBomber(2/3)와 별도 필드·델리게이트·진입점으로 분리: `SpecialAttackContext.ApplyInfernoDot` → `UnitCombatUseCase.ApplyInfernoDot`(`_infernoDot*` 필드). MushroomBomber `ApplyBlastDot`(`_blastDot*`) 경로 무변경. `SpecialAttackConfig`에 `_infernoDotPerSecond`(5)/`_infernoDotDuration`(3) 추가, GameBootstrapper float 주입(미연결 시 폴백 5/3). 규칙 42. |
| QA / 실기 테스트 | ✅ QA PASS · 실기 완료 (2026-07-20) | qa-tester PASS + 사용자 실기 테스트 완료. task: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/`. |
| 알려진 이슈(보류) — 공격 방향(facing) 버그 | ⏸️ 보류 (2026-07-20) | 멀티플레이 원거리 공격 시 유닛이 타겟을 정확히 안 바라봄. 진단상 InfernoSpirit 에셋은 정상 유닛(FlameSpirit)과 동일 → 멀티 원거리 facing 공유(서버 회전+NetworkTransform) 로직 문제로 추정(근접 유닛은 미노출). 사용자 결정으로 이번 미수정·보류(DoT만 구현). 상세: task Plan.md `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/`. |

#### QuakeSpirit(대지의 정령) 착탄형 즉발 AoE (2026-07-20) — 특수 유닛 5종 전량 완료
| 항목 | 상태 | 비고 |
|------|------|------|
| 착탄형 즉발 특수 핸들러 (QuakeAttackBehavior) | ✅ 완료 (2026-07-20) | `Application/Combat/QuakeAttackBehavior.cs` 신설(`ISpecialAttackBehavior`, `ReplacesPrimaryAttack=false`) + 레지스트리 `QuakeSpirit → QuakeAttackBehavior` 1줄. 착탄 중심(주 타깃 위치) 기준 **월드 좌표 원형 반경**(XZ, arc 없음 — 규칙 38 판정 재사용) 내 대상에 **즉발**(DoT 아님) 스플래시. 규칙 43. |
| 직접 100% + 스플래시 50% 역할 분담 (건물 포함) | ✅ 완료 (2026-07-20) | 직접 100%(공격력 20)=기존 `ExecuteAttack` 주 타깃 단일 피해(`ApplyDamageToVictim`, 건물이면 100% 공성), 스플래시 50%=특수 핸들러(올림 `CeilToInt(20×0.5)`=10, **주 타깃 제외**). ⚠️ **MushroomBomber/BattleAxe와 달리 스플래시가 적 유닛뿐 아니라 적 건물도 포함**. 아군 무피해. 규칙 43. |
| 원형 반경 헬퍼 공용화 + 건물 순회 + hit-set 분리 | ✅ 완료 (2026-07-20) | 규칙 38의 유닛 수집 헬퍼 `CollectEnemyUnitsInRadius`를 `internal static`으로 **공용화**해 재사용(MushroomBomber "유닛만" 로직 무변경 — 회귀 없음) + 건물용 `CollectEnemyBuildingsInRadius` **신설**. 유닛 Id/건물 Id 카운터 충돌 방지 위해 **유닛/건물 hit-set 분리**(규칙 29 계승). 규칙 43. |
| 튜닝 SO + 주입 | ✅ 완료 (2026-07-20) | `SpecialAttackConfig`에 `_quakeRadius`(1.0=인접 1칸)/`_quakeSplashRatio`(0.5) 추가, GameBootstrapper가 float 주입(미연결 시 폴백 1.0/0.5). 즉발 연출은 규칙 26(단일 타격 프레임 = 보류 큐 전부 방출)에 따라 폭발과 동시 표시. |
| QA / 멀티플레이 실기 로그 검증 | ✅ QA PASS · 멀티 로그 검증 완료 (2026-07-20) | qa-tester PASS + **멀티플레이(Host/Client) 실기 로그로 100%=20·스플래시=10·건물 스플래시·주 타깃 제외·반경 이내·아군 무피해·서버↔클라 동기화 전부 정상 확인**. 로그: `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`. task: `_Tasks/2026-07-20/10_24_quakespirit-impact-aoe/`. |
| 알려진 이슈(보류) — 타격 타이밍 어긋남 | ⏸️ 보류 (2026-07-20) | 시각(애니)↔데미지 텍스트 타이밍 불일치. 원인=QuakeSpirit Attack 클립 `OnAttackHit` **미주입** + `hitFrameTimes` placeholder(1.0s) → 스플래시 텍스트가 `HitPresentationQueue` 타임아웃(쿨다운×1.5=7.5s)까지 지연. **피해·판정·동기화 자체는 정상**(로그 검증 완료), 연출 표시 시점만 어긋남. 사용자 결정으로 **별도 task 분리**(이번 미수정). 멀티 원거리 facing 버그와 함께 알려진 이슈로 기록. |

#### 코드 정리 3건 — 죽은 코드 제거 / Animator 상태 의존 제거 / Firebase 게이트 제거 (2026-07-13)
| 항목 | 상태 | 비고 |
|------|------|------|
| `UnitView.StopMovement()` 죽은 코드 삭제 | ✅ 완료 (2026-07-13) | 호출 0건(코드베이스 Grep 전수 확인)인 미사용 메서드 삭제. 함께 있던 주석이 이미 제거된 `OnUnitWalkStopped` 이벤트를 언급하던 문서 불일치도 해소. 순수 삭제로 런타임 동작 불변. 커밋 `8840798`(main). |
| 전투 종료 후 Walk 재개 Animator 상태 의존 제거 (리팩토링) | ✅ 완료 (2026-07-13) | 전투 종료 후 Walk 재개 3곳(`EnterCombatLoopV3` 멀티 서버 분기/싱글 분기, `ResumeFromForwardTileV3`)이 `Animator.GetCurrentAnimatorStateInfo`로 "이미 Walk인지"를 판별하던 것 → CrossFade 블렌딩 도중 출발 상태 반환으로 판별이 어긋날 수 있는 잠재 취약점(프로젝트 자체 원칙 "Animator 런타임 상태 의존 제거", 규칙 U-18·U-22와 동일 방향). **해결**: 신규 필드 `_currentAnimStateHash`(마지막 지시한 상태 해시 로컬 추적, 초기값 0) + 공통 헬퍼 `ResumeWalkAnimation()`. CrossFade 4곳(MoveAlongPathV3 Walk 시작 / StartWalkAnimation / PlayAttackAnimation / StartCombatAnimation)에서 필드 갱신, 위반 3곳을 헬퍼 호출로 대체, Animator 런타임 상태 질의 제거. 실행 컨텍스트가 서버/호스트/싱글 한정이라 클라 애니메이션(규칙 U-22 값 기반 경로) 무영향. 겉보기 동작 불변. 비활성화(주석 처리)했던 기존 블록은 실기 통과 후 최종 삭제 완료. 커밋 `97adaad`+후속. task: `_Tasks/2026-07-13/09_28_anim-resume-state-tracking/`. |
| Firebase 인증 게이트 제거 (로그인 무조건 실패 버그 수정) | ✅ 완료 (2026-07-13) | main 커밋 `528c7c6`이 도입한 `#if HEXIEGE_ENABLE_FIREBASE_AUTH` 게이트가 심볼 미정의 환경에서 스텁 `FirebaseAuthService`를 컴파일시켜 로그인이 무조건 실패하던 문제. Firebase Unity SDK는 `.gitignore` 정책상 git 미포함(용량 대형 — 각자 로컬 임포트)이므로, 게이트를 제거해 실제 Firebase 코드가 무조건 컴파일되도록 복원(검증된 `combat-system-visuals` 브랜치 상태와 동일). 파일: `FirebaseAuthService.cs`(게이트+스텁 제거), `LoginBootstrapper.cs`(GPGS 가드 2곳), `Assets/Plugins/Android/mainTemplate.gradle`(firebase-auth 24.1.0 / firebase-app-unity 13.11.0 / gpgs-plugin-support 2.1.0 등 Android 의존성 복원). 사용자가 Firebase Unity SDK 13.11.0 + GooglePlayGames v2.1.0 로컬 임포트 후 컴파일/테스트 통과. 커밋 `4fe1cf0`(main). **잔여(별도 이슈)**: 에디터 "Firebase 초기화 실패" 런타임 로그(코드 게이트와 무관), Firebase/EDM 저장소 버전 관리 방침 정리(ROADMAP Phase F-5). |

#### 플랫폼 / 빌드 최적화 (2026-07-15)
| 항목 | 상태 | 비고 |
|------|------|------|
| Android AAB 용량 최적화 | ✅ 완료 (2026-07-15) | `codex/asset-size-optimization` 작업 main 병합. AAB 용량 **190.66 MB → 125.30 MB**(65.36 MB 절감). 핵심 변경: `Assets/_Project/Texture/Buildings/**`, `Assets/_Project/Texture/Units/**` Android max texture size `1024 → 512`. `_Old` 미사용 에셋 7개 디렉터리 정리, 93개 normal-map PNG + 84개 roughness PNG 정리, 보수적 FBX import 조정 적용. TMP Font Atlas 축소 테스트는 AAB 효과가 작아 되돌림. 상세: `AABSizeOptimization.md`, `BuildAssetOptimizationReport.md`, `UnusedAssetAudit.md`. |

#### 팀별 피아식별 + 신규 유닛 에셋 (2026-03-13)
| 항목 | 상태 | 비고 |
|------|------|------|
| 건물 Blue/Red 프리팹 (Castle, Barracks) | ✅ 완료 (2026-03-14) | BuildingFactory 팀별 분기 |
| 유닛 Pistoleer Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기 |
| 유닛 Assault(돌격소총병) Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기, UnitStats/ProductionStats 정의 |
| 유닛 Sniper(저격총병) Blue/Red 프리팹 | ✅ 완료 (2026-03-14) | UnitFactory 팀+타입별 분기, UnitStats/ProductionStats 정의 |
| 초상화 스프라이트 Blue/Red (전 유닛) | ✅ 완료 | UI용 |
| 반응형 팝업 UI (ProductionPopup/BuildingPopup) | ✅ 완료 | 앵커 기반 배치, ResponsivePopupUISetup.cs |
| 종족+팀별 초상화 동적 업데이트 (ProductionPanelUI/BuildingPlacementUI) | ✅ 완료 (2026-04-30) | Show() 호출 시 종족+팀에 맞는 스프라이트 교체 및 생산 타입 바인딩 |
| 잠금 유닛 Lock Icon 표시 (ProductionPopup) | ✅ 완료 (2026-05-31) | 초상화 디밍(35% 밝기) + 우측 하단 자물쇠 아이콘 배지. 업그레이드 후 잠금 해제 확인 |
| 건물 업그레이드 생산 상태 처리 오류 수정 | ✅ 완료 (2026-05-31) | 업그레이드 시 생산 중 골드 환불 누락 + 랠리포인트 초기화 2건 수정. ProductionTicker.cs 1개 수정 |
| 방어 타워(AutoTower) 공격 기능 | ✅ 완료 (2026-06-01) | TowerCombatUseCase 신규. 종족별 쿨다운(Human 5s/Spirit 3.5s/Trans 5s), 사거리 4.0 타일, 서버 권위. |
| Human CannonTower 초기 방향 | ✅ 완료 (2026-06-02) | BuildingFactory.GetInitialRotation() — 상대 포탑 Y180도, 내 포탑 기본값. |
| UnitStatsConfig 미사용 필드 제거 + 스탯 정비 | ✅ 완료 (2026-06-02) | AttackKind/occupancySize 제거. 유닛 9종 쿨다운/hitFrameTimes 문서 기준 입력. |
| BuildingView Missing Script 정리 | ✅ 완료 (2026-06-05) | Spirit/Transcendence 건물 프리팹 8개에서 삭제된 BuildingView 참조 제거. Editor 스크립트 1회 실행. |
| 자동생산 재등록 슬롯 버그 수정 | ✅ 완료 (2026-06-05) | CurrentIsAuto 파생 getter 구조로 개선. UnitProductionUseCase reset 2곳 제거. |
| 신규 유닛 프리팹 컴포넌트 부착 (32개) | 🔧 스크립트 완료 (2026-06-05) / 실기 테스트 예정 | Human 5종·Spirit 6종·Transcendence 5종 × Blue/Red. `Assets/Editor/Setup/SetupNewUnitPrefabs.cs`. 후속: Animation Event·UnitFactory 등록·스탯 추가 별도 필요. |
| NetworkGameManager 고아 필드 + Game씬 NGM 제거 | ✅ 완료 (2026-06-06) | GameBootstrapper 미사용 _networkGameManager 필드 제거. Game.unity 중복 NGM 제거. 싱글+멀티 실기 확인. |
| 멀티플레이 유닛 사망 GO 미파괴 + 이펙트 미재생 버그 수정 | ✅ 완료 (2026-06-08) | 근본 원인: 서버 UnitView의 Destroy(gameObject)가 NGO 클라이언트 전파 불보장. 수정: NetworkCombatController(Infrastructure)에서 EntityDiedClientRpc 발행 후 NetworkObject.Despawn(destroy:true) 명시 호출. UnitView에서 Unity.Netcode 직접 참조 완전 제거(레이어 규칙 준수). 런타임 로그 13킬 전체 이펙트 재생 확인. |
| 전체 유닛 사망 VFX 적용 | ✅ 완료 (2026-06-08) | EffectPreset_Unit_Death_Common.asset 신규 생성(vfx_unit_death+SFX). SetUnitDeathVfxAll 에디터 스크립트로 UnitEffectConfig 전체 24종 deathPreset 일괄 연결. 코드 변경 없음(에셋 작업만). 기존 EffectPreset_Pistoleer_Death.asset 삭제. |
| 유닛 VFX 디테일 개선 3종 | ✅ 완료 (2026-06-08) | ① VFX 프리팹 3개 ParticleSystem ScalingMode Local→Hierarchy (VfxScalingModeFixer 에디터 스크립트). ② 피스톨러 공격 VFX 스폰 위치 — VfxSpawnPoint GO(총구 위치)로 position 참조, rotation은 `Quaternion.LookRotation(transform.forward)` (스켈레톤 본 하위 배치로 _vfxSpawnPoint.rotation 사용 불가). UnitView + EffectManager 수정. ③ vfx_unit_death 퍼짐 효과 제거 (3개 PS startSpeed→0 YAML 직접 수정). |
| 사운드 시스템 (AudioManager + SFX/BGM 분리) | 🔵 코드 완료 (2026-06-10) + 실기 버그 3종 수정 (2026-07-08) / 로비 볼륨 패널 별도 작업 | SoundConfig.cs(Infrastructure) + AudioManager.cs(Presentation, DontDestroyOnLoad) 신규. BGM 크로스페이드(A/B 채널), SFX 풀(8개, 2D), 볼륨 3채널(Master/BGM/SFX, PlayerPrefs). EffectManager VFX 전용 분리(SFX 비활성화). VFX+SFX 쌍 호출 UnitView×2 + NetworkUnit×1 복원. LoginBootstrapper Initialize 추가. InGameSettingsUI 볼륨 슬라이더 연동. 로비 볼륨 패널 미구현(별도 작업). |
| 사운드 시스템 실기 버그 3종 수정 | ✅ 완료 (2026-07-08) | **BUG-1** BGM 씬 전환 시 소리 겹침: `AudioManager.StartCrossfade()`에서 `StopCoroutine` 직후 페이드아웃 중이던 stale AudioSource를 즉시 `Stop()`(volume 0/clip null)하도록 수정. GameSystemRules_Sound 규칙 8에 요건 명문화. **BUG-2** 볼륨 UI 규칙 위반: 에디터 스크립트(`SetupInGameVolumePanel.cs`/`SetupLobbySettingsTab.cs`) 슬라이더 서브 요소 고정 픽셀값→앵커 비율(규칙 2), 전 TMP에 Maplestory **Bold** SDF 폰트 적용(규칙 6) + `EditorUtility.SetDirty()`로 씬 저장 반영, 라벨/여백/BackButton lavender 스프라이트/패딩 개선(레이아웃 미세 조정은 사용자 직접). **BUG-3** SFX 볼륨 슬라이더 미작동: Exposed Parameter 이름 3종 정상 확인(불일치 아님), `ApplyVolume()`에 `SetFloat` 실패 감지 디버그 로깅 추가. 브랜치 `claude/sound-system-review-itwt0t`. task: `_Tasks/2026-07-07/12_28_sound-system-bugfix/` |
| 전역 UI 시스템 (UIManager + SplashOverlay) | ✅ 완료 (2026-06-18) | UIManager(SingletonMonoBehaviour+IUIManager), SplashOverlayView(DOTween 깜빡임+페이드아웃), SpinnerRotator 신규. ConfirmPopup/LoadingIndicator 전역 통합 (Login 씬 1회 생성 → DontDestroyOnLoad). 씬별 중복 ConfirmPopup/LoadingScreen 제거, BattleViewModel LoadingScreen→UIManager 전환. 사용자 실기 TC-01~07 PASS. |
| LoadingIndicator 최소 표시 시간 보장 | ✅ 완료 (2026-06-22) | `UIManager.ShowLoading(false)` 호출 시 최소 1초 미만이면 코루틴으로 지연(`WaitForSecondsRealtime`). `_loadingMinDuration` SerializeField(기본 1f). 모든 호출부 자동 적용. |
| LoadingIndicator 독립 Canvas(300) 추가 | ✅ 완료 (2026-06-22) | AnonymousWarningPopup(SortingOrder=200)이 LoadingIndicator를 가리는 문제 해결. `LoginUiSetup.cs` 에디터 스크립트에 메뉴 항목 추가(`Hexiege/Setup/Login UI — LoadingIndicator 독립 Canvas 추가`). LoadingIndicator에 독립 Canvas(SortingOrder=300, overrideSorting=true) + GraphicRaycaster 자동 추가. |
| ConfirmPopup + NetworkErrorPopup 씬 버그 수정 | ✅ 완료 (2026-06-22) | ① ConfirmPopup: 씬 프리팹 인스턴스 오버라이드로 루트 CanvasGroup이 alpha=0/interactable=0으로 강제된 버그 Inspector 수정. ② NetworkErrorPopup: ConfirmPopup 컴포넌트가 남아있던 것 교체, `_panel` 슬롯 연결, LoginRootView + LoginBootstrapper `_networkErrorPopup` 슬롯 연결. |
| 로그인 팝업 CloseButton 무반응 버그 수정 | ✅ 완료 (2026-06-23) | AnonymousWarningPopup / NetworkErrorPopup 두 팝업에 `_closeButton` SerializeField 추가 + Inspector 연결. CloseButton GO가 씬에 활성(Active) 상태로 존재했으나 코드 필드 누락으로 리스너 미등록 → 클릭 무반응. AnonymousWarningPopup은 `SetInteractable()`에 `_closeButton` 포함(로그인 진행 중 취소 방지). |
| 스플래시 화면 로그인 흐름 개선 (skipFade 모드) | ✅ 완료 (2026-06-23) | 로그인된 상태 재실행 시 스플래시 FadeOut으로 로그인 씬 배경이 노출되던 문제 해결. `SplashOverlayView._skipFadeOnTap` 필드 + `SetTapCallback(callback, skipFade=false)` 파라미터 추가. `OnPointerClick` skipFade 분기: true이면 FadeOut 없이 즉시 콜백 호출. `LoginBootstrapper` 자동 로그인 성공 분기에서 `SetTapCallback(GoToNextScene, skipFade:true)` + `ShowLoading(false)` + `ShowTapToStart()` 적용. 로딩 인디케이터(SO=300)가 즉시 화면 커버. 로그인 X 흐름 변화 없음. |
| 코드 정리(클린업) Phase 1 — 히스토리성 주석 및 폐기 코드 제거 | ✅ 완료 (2026-06-23) | 약 30개 파일에서 `[2026-XX-XX]`/`[Phase X]` 형식 이력 라벨 제거, 구방식→현재방식 전환 설명 주석 제거. 폐기 코드 블록 제거: `GameBootstrapper.cs`의 `_enableAI` 블록(주석 처리 코드+메모)·`_confirmPopup` 전환 설명 블록. `NetworkGameFlow.cs` 빈 섹션 헤더 제거. `GameBootstrapper.Setup.cs` 중복 RaceId 배열 → 지역 변수 1개 통합(동작 동일). 런타임 동작 변경 없음(순수 주석/폐기코드 정리). 브랜치 `claude/code-refactor-cleanup-jsa24o`. task: `_Tasks/2026-06-23/00_00_코드정리-클린업/` |
| 코드 구조 개선 Phase 2 — switch→Dictionary lookup table + HexMetrics 중복 제거 | ✅ 완료 (2026-06-25) | 동작 보존 리팩토링(SINGLE 7 + MULTI 2 전 항목 PASS). ① `BuildingTypeHelper.cs`: IsProductionBuilding/GetStage/GetNextStage 3개 switch → 단일 `Dictionary<BuildingType, BuildingMeta>` lookup table. `BuildingMeta` struct(IsProduction/Stage/NextStage). 신규 생산건물 추가 시 table 한 줄 추가로 끝(세 메서드 자동 정합). CanUpgrade/CanShowActionPanel 무수정 자동 반영. PrimalSanctuary는 기존 switch에도 포함돼 있던 항목으로 동작 보존(`(true,3)` 명시). ② `GameBootstrapper.Network.cs`: StartNetworkGame HexMetrics 수동 4줄 → `ApplyConfig(FlatTop, oc)` 1줄. ApplyConfig 멱등(멀티서 2회 실행 무해), UnitYOffset 누락 부분중복 해소. 기존 switch/수동4줄은 주석 보존(별도 지시 시 삭제). 브랜치 `claude/code-refactor-phase2-structural`(3838c4d). task: `_Tasks/2026-06-23/15_37_구조개선-Phase2/` |
| GameBootstrapper.Setup.cs 하드코딩 배열 파생 | ✅ 완료 (2026-06-25) | 환불 캐시 초기화(`InitializeBuildingStatsFromConfig`)에서 손으로 나열하던 건물 목록 배열 2개를 `BuildingTypeHelper` 공개 API 파생으로 교체. ① `stage1Buildings`(1단계 생산건물 9개 하드코딩) → `Array.FindAll((BuildingType[])Enum.GetValues(typeof(BuildingType)), t => BuildingTypeHelper.GetStage(t) == 1)`. ② `nonProductionBuildings`(비생산 건물 6개, Castle 제외 하드코딩) → `Array.FindAll(..., t => !BuildingTypeHelper.IsProductionBuilding(t) && t != BuildingType.Castle)`. Setup.cs에 `using System;` 추가. 환불 캐시 foreach 루프는 변수명·타입(`BuildingType[]`) 동일 유지로 무변경. Phase 2 lookup table 통합의 연장선 — **신규 생산건물 추가 시 `BuildingTypeHelper._buildingTable` 한 줄 추가만으로 환불 캐시 목록까지 자동 반영**(Setup.cs 무수정). 안 2(도메인 레이어 무변경) 선택, 곧바로 교체(파생 목록이 기존 배열과 값 동치·환불 계산 순서 무관임을 Research에서 검증). 동작/환불 값 불변. 사용자 실기 PASS(생산/비생산 건물 철거 환불 금액 정상). 커밋 `8d74e06`(main). task: `_Tasks/2026-06-25/07_23_하드코딩배열-파생/` |
| IUnitFactory 인터페이스 도입 (Bootstrap 의존성 제거 리팩토링) | ✅ 완료 (2026-06-26) | Application → Infrastructure 역방향 의존 제거. `IGameServices.GetUnitFactory()` 반환 타입을 구체 클래스 `UnitFactory`(Infrastructure) → `IUnitFactory`(Application) 인터페이스로 변경. 신규 파일 `Assets/_Project/Scripts/Application/Interfaces/IUnitFactory.cs` — `GetUnitObject(int)`/`RegisterUnitObject(int, GameObject)`/`InitializeUnitView(UnitData)` 3개 멤버. `UnitFactory.cs`(Infrastructure)에 `IUnitFactory` 구현 추가. 호출부 `GameBootstrapper.cs`/`NetworkProductionController.cs`/`NetworkCombatController.cs`/`NetworkUnitMovementController.cs`/`NetworkUnit.cs`가 `IUnitFactory` 타입으로 수신하도록 업데이트. 기존 `IEntityPositionProvider`/`IForfeitService`와 동일한 의존성 역전 패턴. 동작 변경 없음. 싱글/멀티 실기 PASS(사용자 확인). 브랜치 `claude/code-refactor-cleanup-jsa24o`. |
| 방 만들기 취소 후 텍스트 잔류 버그 수정 | ✅ 완료 (2026-06-26) | 커스텀 게임 방 만들기 취소 후 CustomHostPanel 재진입 시 이전 LobbyCode/ConnectedPlayers/ErrorMessage 값이 잔류하던 문제. `BattleViewModel.CancelHosting()`에 세 값 초기화 3줄 추가(`LobbyCode.Value=""`, `ConnectedPlayers.Value=0`, `ErrorMessage.Value=""`). main 머지 완료. |
| ConfirmPopup z-order 버그 수정 (Canvas SO=250) | ✅ 완료 (2026-06-22) | InGameSettings Panel(SO=200)이 ConfirmPopup 위에 렌더링되던 문제. ConfirmPopup.prefab 루트에 Canvas(Override Sorting=true, SO=250) + GraphicRaycaster 추가. Canvas SortingOrder 전체 구조 확정: SO=0/100/200/250/300. GameSystemRules_CanvasSortingOrder.md 작성. |
| BlockingOverlay 씬 전환 후 미표시 버그 수정 | ✅ 완료 (2026-06-22) | 근본 원인: UIManager가 Login.unity에서 `[UI Systems]` 자식으로 배치되어 DontDestroyOnLoad가 동작하지 않음(Unity 제약: DontDestroyOnLoad는 루트 GO에만 적용). Game 씬 전환 후 UIManager가 파괴되어 `UIManager.Instance=null`. 수정: Login.unity에서 UIManager GO를 계층 루트로 이동(Inspector). Canvas SortingOrder 확정 구조: SO=0(HUD) / SO=100(UIManager BlockingOverlay) / SO=200(패널 Canvas Override) / SO=300(LoadingIndicator). 파일 기반 RuntimeLog(RuntimeLogWriter.cs [DEBUG-TEMP]) 로 흐름 추적 후 원인 확인 → 디버깅 완료 후 로그 코드 제거. |

#### 싱글플레이 AI 시스템 (2026-06-07)
| 항목 | 상태 | 비고 |
|------|------|------|
| LocalPlayerDifficulty.cs (정적 홀더) | ✅ 완료 | `Infrastructure/` — DifficultyLevel enum(Easy/Normal/Hard), LocalPlayerRace 패턴 동일 |
| AIConfig.cs ScriptableObject | ✅ 완료 | `Infrastructure/Config/` — DifficultyParams(goldIncomeMultiplier/productionTimeMultiplier/decisionInterval 외 6개) × Easy/Normal/Hard |
| AIScenarioConfig.cs ScriptableObject | ✅ 완료 | `Infrastructure/Config/` — BuildOrderStep(ActionType/BuildingType/UnitType/targetBuildingLine/delay 3종) 플랫 리스트 구조 |
| AIConfigSetup.cs Editor 스크립트 | ✅ 완료 | `Assets/Editor/` — 메뉴 `Hexiege/Setup/AIConfig 생성` + `AIScenarioConfig_Human_A/B/C 생성` |
| UnitProducedEvent.BarracksId 추가 | ✅ 완료 | `Application/Events/GameEvents.cs` — AI 콜백 기반 연속 생산용. 기존 구독자 ProductionTicker 영향 없음 |
| ResourceUseCase.SetIncomeMultiplier() | ✅ 완료 | `Application/UseCases/ResourceUseCase.cs` — Red 팀 골드 수입 배율 설정. TickTeamIncome()에 적용 |
| AIOpponentController.cs (AI 핵심) | ✅ 완료 | `Application/Services/` — 빌드오더 스크립트(Phase 1~4), 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴), BFS 건물 배치, MiningPost 병행 트랙 |
| AI On/Off 설정 — AIConfig.enableAI | ✅ 완료 | `Infrastructure/Config/AIConfig.cs` — `public bool enableAI = true` 필드. Project 창에서 AIConfig.asset 선택 → Inspector 토글 (Game.unity 씬 접근 불필요). 구 GameBootstrapper._enableAI 주석 처리 블록은 코드 정리 Phase 1(2026-06-23)에서 제거됨 |
| GameBootstrapper — InitializeAI() | ✅ 완료 | `Bootstrap/GameBootstrapper.Setup.cs` — AIConfig 로드 → enableAI=false면 조기 반환 → 시나리오 랜덤 선택, SetIncomeMultiplier 호출, AIOpponentController 생성·주입 |
| GameBootstrapper — Map.cs 연동 | ✅ 완료 | `Bootstrap/GameBootstrapper.Map.cs` — SetupProduction() 직후 `if (!NetworkContext.IsNetworkActive) InitializeAI();` (enableAI 체크는 InitializeAI() 내부) |
| BattleViewModel — SingleplayDifficulty | ✅ 완료 | `BattleViewModel.cs` — BattleScreen.SingleplayDifficulty 추가, CmdSelectDifficulty Subject<DifficultyLevel> 추가, CmdStartSingleplay → 난이도 화면 전환으로 변경 |
| DifficultySelectView.cs 신규 + UI 구조 | ✅ 완료 | `Presentation/UI/Views/Lobby/Battle/` — 쉬움/보통/어려움/뒤로 버튼 + CanvasGroup 패턴(Rule 5). BattlePanel 상단 절반 배치. VLG Padding 60/60, Spacing 20, preferredHeight=100 (BattleMainPanel 동일 구조) |
| BattleRootView — DifficultySelectView 바인딩 | ✅ 완료 | `BattleRootView.cs` — _difficultySelectView 필드 + Bind/Unbind 포함 |
| LogRules.md 작성 | ✅ 완료 (2026-06-20) | 런타임 로그 파일 규칙 문서. 파일 위치/명명, 형식, 레벨 3단계([INFO]/[WARN]/[ERROR]), 카테고리([System/Class]), 금지사항 확정. |
| AI 시나리오 문서 (전 종족) | ✅ 완료 (2026-06-10) | Human 3개 + Spirit 3개 + Transcendence 3개 — 총 9개 빌드오더 시나리오. 골드 수지 검토 완료. |
| AI 시나리오 ScriptableObject 3종족 개편 | ✅ 완료 (2026-06-10) | Human/Spirit/Transcendence 각 1파일 × 3시나리오. DifficultyLevel·BuildOrderStep Domain 레이어 분리. |
| Inspector 작업 | ✅ 완료 (2026-07-16) | `Hexiege/Setup/AIConfig 생성` → `AIScenarioConfig_Human_A/B/C 생성` → Spirit/Transcendence 시나리오 에셋 생성 → (Lobby.unity) `Hexiege/Fix/DifficultySelectView 레이아웃 수정` 실행 완료. AIConfig 에셋, AIScenarioConfig 3종족 에셋, DifficultySelectView 레이아웃 배치 모두 반영됨(실기 테스트가 진행된 사실로 확인). |
| 실기 테스트 | 🔵 조건부 완료 (2026-07-16) | 핵심 흐름(유닛 생산, 건물 업그레이드 등)을 반복 실기로 확인 — PASS, 특별한 문제 미발견. 단, 세부 정밀 검증은 미완: 반응 시스템(R1 유닛열세/R2 골드과잉/R3 채굴소 파괴)의 트리거·동작, 3종족(Human/Spirit/Transcendence) 시나리오 무작위 선택 동작은 아직 정밀 확인하지 않음. 후속으로 정밀 검증 필요. |

---

#### 3D 전환 (2026-02-27 ~ 2026-03-01)
| 항목 | 상태 |
|------|------|
| XY→XZ 좌표계 전환 | ✅ 완료 |
| Orthographic 55도 틸트 카메라 | ✅ 완료 |
| 카메라 이동 경계(ClampPosition) 줌 연동 | ✅ 완료 |
| 2D Sprite → 3D Mesh 렌더링 | ✅ 완료 |
| sortingOrder 폐기 → Z-buffer | ✅ 완료 |
| Animator (IsDead/Walk/Attack) | ✅ 완료 |
| 헥스 타일 3D (ProBuilder + Shader Graph) | ✅ 완료 |
| 유닛 3D 모델 (Pistoleer, Meshy.ai) | ✅ 완료 |
| 건물 3D 모델 (Castle/Barracks/MiningPost) | ✅ 완료 |
| 금광 타일 3D 오브젝트 (GoldMineTile) | ✅ 완료 |
| 랠리포인트 마커 3D | ✅ 완료 |
| HexTileView 팀 색상 (_BaseColor, Shader Graph) | ✅ 완료 |

#### Game UI Lifecycle Framework (2026-03-24)
| 항목 | 상태 | 비고 |
|------|------|------|
| `IGameUI.cs` 인터페이스 | ✅ 완료 | OnGameStarted/OnGameEnded/OnGamePaused/OnGameResumed, default 빈 구현 |
| `GameUIManager.cs` 매니저 | ✅ 완료 | Register/Initialize, CompositeDisposable 중복 구독 방지, GameEndUI 제외 로직 |
| GameEvents — OnGameStarted/Paused/Resumed 추가 | ✅ 완료 | Subject<Unit> 3개 추가 |
| 기존 UI 4종 IGameUI 구현 | ✅ 완료 | GameHudUI/ProductionPanelUI/BuildingPlacementUI/GameEndUI |
| 멀티플레이 클라이언트 팝업 미닫힘 버그 수정 | ✅ 완료 | NetworkGameEndController.AnnounceWinnerClientRpc에서 NotifyGameEnded() 직접 호출 |

#### UI DOTween 애니메이션 프레임워크 (2026-03-19)
| 항목 | 상태 | 비고 |
|------|------|------|
| `UIAnimator.cs` — static 헬퍼 | ✅ 완료 | PopupShow/Hide, SlideFromBottom/Top, ButtonPunch, CountTo, FlashText, FillTo |
| `AnimatedPanel.cs` — 팝업 컴포넌트 | ✅ 완료 | AnimationType(PopupFade/SlideFromBottom/SlideFromTop), IsVisible, SetUpdate(true), `_backgroundOverlay`(CanvasGroup — Show 시 alpha=1/blocksRaycasts/interactable=true, Hide 시 alpha=0/false/false 즉시 전환) |
| `AnimatedPanelSetup.cs` — Inspector 자동화 에디터 스크립트 | ✅ 완료 | `Hexiege/Setup/Apply AnimatedPanel Setup` 메뉴 |
| GameEndPanel → SlideFromTop | ✅ 완료 | 위에서 아래로 슬라이드 인 |
| ProductionPopup → SlideFromBottom | ✅ 완료 | 하단 슬라이드 업 |
| BuildingPopup → SlideFromBottom | ✅ 완료 | 기존 PopupFade → 변경 (ProductionPanelUI와 일관성 통일) |
| RematchRequestPopup DOFade 수정 | ✅ 완료 | _currentFade 공유 → 3개 별도 Tween / blocksRaycasts 해제 버그 수정 |

#### 전역 로딩 스크린 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| LoadingScreen.cs (싱글턴, DontDestroyOnLoad) | ✅ 완료 | `Presentation/UI/Common/` |
| Lobby 씬 UI 배치 (Canvas SO:100, Background/Spinner/StatusText) | ✅ 완료 | MCP로 생성 |
| 싱글플레이 씬 전환 2초 딜레이 | ✅ 완료 | `LoadSingleplayScene()` async void |
| 커스텀 호스트/참가 씬 전환 시 로딩 스크린 | ✅ 완료 | `LoadGameScene()` 직전 Show() |
| 랜덤 매칭 완료 시 로딩 스크린 | ✅ 완료 | `onMatchFound` 콜백 |
| sceneLoaded 이벤트 자동 Hide() | ✅ 완료 | NGO 씬 전환에도 정상 발동 확인 |

#### 커스텀게임 재경기 시스템 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| `NetworkGameManager.IsRandomMatchmaking` 속성 추가 | ✅ 완료 | 게임 모드 판별용 |
| `NetworkGameEndController` 재경기 RPC 시스템 | ✅ 완료 | Request/Accept/Decline ServerRpc + Notify ClientRpc(targeted) |
| `GameEndUI.SetupRematchButton()` / `RestoreRematchButton()` | ✅ 완료 | 모드별 버튼 분기, 요청 중 상태 관리 |
| `RematchRequestPopup.cs` 신규 생성 | ✅ 완료 | `Presentation/UI/Common/` — 수락/거절/거절알림 팝업 |
| `RematchPopupBuilder.cs` 에디터 스크립트 | ✅ 완료 | `Hexiege/UI/Build Rematch Popup` 메뉴 |
| 레이스 컨디션 처리 (`_rematchRequesterId`) | ✅ 완료 | 양측 동시 요청 → 즉시 재경기 |
| 랜덤매칭 다시하기 버튼 숨김 | ✅ 완료 | `isRandomMatch=true` 시 버튼 비활성화 |
| 싱글플레이 다시하기 동작 유지 | ✅ 완료 | 변경 없음 |

#### Google 로그인 실기 디버깅 (2026-06-27~28)
| 항목 | 상태 | 비고 |
|------|------|------|
| Google 로그인 즉시 Canceled 버그 수정 | ✅ 완료 (2026-06-27~28) | 실기 기기에서 구글 로그인 시 계정 선택 UI 미표시 + 즉시 `signInStatus=Canceled` 반환 문제. 3가지 원인을 순차 해결하여 로그인 성공(`UID=xdmWpVNyyvaBe0cB878mSg0URm83`, IsLoggedIn=True, IsAnonymous=False). task: `_Tasks/2026-06-27/12_26_google-login-debug/` |
| [문제 1] `Authenticate()` → `ManuallyAuthenticate()` 코드 수정 | ✅ 완료 | `FirebaseAuthService.cs`. `Authenticate()`는 `isAuthenticated()`만 호출하여 기존 세션 없으면 무조건 Canceled 반환 → `ManuallyAuthenticate()`(`signIn()` 호출)로 변경해야 계정 선택 UI 표시. GPGS Plugin 2.1.0 기준. |
| [문제 2] google-services.json SHA-1 보강 | ✅ 완료 | Firebase Console에 SHA-1 1개(릴리즈)만 등록돼 있던 것을 디버그 + Play App Signing 추가하여 3개로 업데이트. `Assets/google-services.json` 재다운로드. |
| [문제 3] 실제 빌드 키스토어 SHA-1 미등록 (근본 원인) | ✅ 완료 | logcat `PlayGamesServices[SignInAuthenticator]`에서 실제 APK 서명 SHA-1(`18:E0:32:5F:5A:F9:C5:A7:3F:22:34:BE:65:1F:E6:CA:61:2E:DE:3D`) 확인 → 등록된 3개 어느 것과도 불일치(`hexiege-release.keystore`가 SHA-1 등록 시 키스토어와 다른 파일). Firebase Console + Play Console GPGS 사용자 인증 정보에 실제 SHA-1 추가 등록·게시 + Firebase Authentication Play Games 제공업체 활성화(Web Client ID/Secret 입력). |
| GPGS Server Auth Code 발급 정상화 | ✅ 완료 | SHA-1 불일치 시 `serverAuthCode length=0`(빈 값) → SHA-1 정합 후 `length=73` 정상 발급. |
| UGS OIDC 브릿지 실패 (`id provider not found`) | ❌ 미해결 (별도 이슈) | Firebase 로그인 성공 후 `SignInWithOpenIdConnectAsync("oidc-firebase")` 단계에서 UGS Dashboard OIDC 제공자 미등록으로 실패 → UGS PlayerId 미발급, 멀티플레이 제한. UGS Dashboard OIDC Provider 등록 후 재확인 필요. |

#### 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 (2026-07-09, 실기 PASS)
| 항목 | 상태 | 비고 |
|------|------|------|
| AudioManager 음소거 기능 신규 | ✅ 완료 (2026-07-09) | `SetMuted(bool)`/`IsMuted()`/`ResetAllVolumes()` 추가. 뮤트는 **Master 채널만 -80dB(`MutedDb`)** 로 눌러 전체 무음(BGM/SFX 논리 볼륨값 보존). `ApplyVolume`을 `ApplyDb(param,dB)`로 리팩터하여 무음·볼륨변환이 SetFloat 진단 로깅 경로 공유. 슬라이더 조작 시 자동 언뮤트. PlayerPrefs 키 `"Muted"`(0/1) 영속화. `Initialize()`에서 저장된 뮤트 상태 로드·적용. |
| VolumeControlBinder 신규 (공용 순수 C#) | ✅ 완료 (2026-07-09) | `Presentation/UI/Common/VolumeControlBinder.cs`. 인게임/로비 공통 볼륨 UI 로직(슬라이더3 + On/Off/Reset/Back 버튼 + 색상) 캡슐화. `Bind(Refs)` 참조 주입, `RefreshFromAudioManager()`로 패널 표시 시 재동기화. On/Off 버튼 CanvasGroup 상호배타(규칙24), 슬라이더 Fill 색상(규칙26, `soundOnColor`/`soundMutedColor`). 프로그램 값 설정은 `SetValueWithoutNotify`로 자동 언뮤트 부작용 차단. |
| UIColorConfig 색상 토큰 추가 | ✅ 완료 (2026-07-09) | `soundOnColor`(초록)/`soundMutedColor`(빨강) 추가. **별개 정리**: 미사용 `confirmButtonColor`/`cancelButtonColor` 죽은 코드 제거(ConfirmPopup이 이미지 에셋 기반으로 전환됨) → `ConfirmPopup.cs`의 `Awake()`/`_colorConfig` 필드도 제거. |
| InGameSettingsUI 프로필 버튼 + 볼륨 연동 | ✅ 완료 (2026-07-09) | 프로필 버튼 추가(사운드 버튼과 동일 CanvasGroup 열기/닫기, 규칙6, 내부 콘텐츠는 범위 밖 빈 토글). VolumeControlBinder 연동. **버그 수정**: `Hide()`가 서브패널 복원 부수효과 없이 현재 화면 그대로 페이드아웃(닫힘 시 메인 화면 깜빡임 해소), `Show()`/`Initialize()`는 `ResetToMainView()`로 통합. |
| LobbySettingsView Setting 전용 정리 | ✅ 완료 (2026-07-09) | Profile 관련 필드/로직 제거(비활성화 후 삭제), VolumeControlBinder 연동. 스크립트 컴포넌트를 자식 오브젝트 → `SettingPanel` 패널 루트로 이동(탭 패널 컨벤션 통일). |
| 로비 설정 탭 배선 완성 (버그 수정) | ✅ 완료 (2026-07-09) | 증상: 로비 하단 탭바가 "설정" 탭을 인식 못 함(클릭 무반응 + 항상 선택된 것처럼 표시). 원인: `LobbyViewModel.LobbyTab` enum에 `Setting` 부재, `TabBarView`/`LobbyRootView`에 설정 버튼·패널 참조 부재. 수정: enum에 `Setting` 추가(Profile↔Ranking 사이), 탭 버튼 바인딩·색상 갱신·패널 CanvasGroup 전환 완성. task: `_Tasks/2026-07-09/09_58_lobby-setting-tab-wiring/` |
| On/Off 버튼 위치·크기 균등화 (버그 수정) | ✅ 완료 (2026-07-09) | 증상: 전체소리켜기/전체음소거 버튼이 VerticalLayoutGroup에서 서로 다른 슬롯·크기를 차지. 수정: `MuteToggleSlot` 래퍼로 재부모화(파괴/재생성 없이 `SetParent`)하여 완전히 겹침. 추가 발견된 높이 불균등(빈 슬롯 선호높이 0)은 `LayoutElement.preferredHeight=0f`/`flexibleHeight=1f` 비율 가중치 방식으로 최종 해결(고정 픽셀 금지, 공통 규칙 2 준수). Editor 스크립트 `FixMuteToggleOverlap_20260709.cs`. |
| Editor 1회성 배선 스크립트 | ✅ 완료 (2026-07-09) | `SetupVolumeProfileUI_20260709.cs`(신규 Serialized 필드 자동 연결, LobbySettingsView 컴포넌트 이동, UIColorConfig 참조 연결, ProfileSubView 자동 생성). 사용자가 Unity에서 직접 실행하여 `Game.unity`/`Lobby.unity`에 반영. |
| 규칙 문서 반영 | ✅ 완료 (2026-07-09) | `GameSystemRules_UI.md` 인게임 설정 메뉴 규칙6(인게임 프로필 버튼)·로비 ProfilePanel/SettingPanel 분리, `GameSystemRules_Sound.md` 규칙23~26(볼륨 버튼 구성/상호배타/리셋/색상) + 규칙26 음소거 내부 구현 확정 반영. |
| 실기 테스트 | ✅ PASS (2026-07-10) | 인게임/로비 슬라이더+뮤트+초기화, 인게임 프로필 버튼 열기/닫기, 로비 프로필/설정 탭 분리·전환, 닫힘 화면 깜빡임 해소, On/Off 버튼 위치·크기 균등화 전부 확인. task: `_Tasks/2026-07-09/06_09_ingame-lobby-volume-profile-ui/` |

#### 게임포기 로딩 인디케이터 미해제 버그 수정 (2026-06-26)
| 항목 | 상태 | 비고 |
|------|------|------|
| 게임포기 시 로딩 인디케이터 영구 잔존 버그 수정 | ✅ 완료 (2026-06-26) | 증상: 멀티플레이 게임포기(Forfeit) 시 로딩 인디케이터가 사라지지 않고 화면에 영구히 남음. 원인: `InGameSettingsUI.OnForfeitConfirmed()`에서 `UIManager.ShowLoading(true, "게임을 포기하는 중...")` 호출 후 씬 전환이 없어 `ShowLoading(false)`가 호출되지 않음(규칙 L-3의 해제 책임자는 씬 전환 후 Bootstrapper/RootView). 수정: 포기는 씬 전환 없이 GameEndUI만 표시하므로 ShowLoading 호출 자체를 제거. `GameSystemRules_UI.md` 규칙 L-2에서 "게임 포기(멀티)" 항목 제거 + "게임 포기(싱글/멀티 모두)는 씬 전환 없이 GameEndUI만 표시하므로 해당 없음" 명시. 수정 파일: `Presentation/UI/InGameSettingsUI.cs`, `GameSystemRules/GameSystemRules_UI.md`. 사용자 실기 PASS. 브랜치 `claude/game-quit-loading-indicator-0h3w0u`. task: `_Tasks/2026-06-26/02_16_forfeit-loading-indicator-stuck/` |

#### 매치메이킹 404 수정 — 호스트 결정 Lobby CreateOrJoin 전환 (2026-07-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| 매치메이킹 404(호스트 결정 단계) 수정 — A방식 | 🔵 초기 정상·지속 관찰 중 (2026-07-17) | 증상: 랜덤 매칭은 성사되나 직후 **호스트 결정 단계에서 HTTP 404** 발생 → 게임 연결 끊김. 원인: `MatchmakerManager.DetermineIsHostAsync` 내부 `GetMatchmakingResultsAsync`가 **전용 서버(Multiplay)용 서버 지향 API**인데 P2P(Relay) 클라이언트가 호출 → 조회 대상 리소스 없어 404(매칭 자체는 정상, 호스트 결정만 실패). 해결(A방식): 호스트 결정을 매치 결과 조회 → **Lobby CreateOrJoin 원자 선점**으로 전환. 모든 플레이어가 같은 `matchId`를 `lobbyId`로 `LobbyService.Instance.CreateOrJoinLobbyAsync(lobbyId, lobbyName, maxPlayers, options)`(com.unity.services.multiplayer@2.0.0) 호출 → 없으면 생성=호스트 / 있으면 참가=클라이언트. 서버 원자 처리로 정확히 한 명만 호스트 → race condition 원천 차단. 호스트/클라 판별은 기존 `LobbyManager.IsHost` 재사용. **수정 파일(3개, Infrastructure/Network)**: `LobbyManager.cs`[추가: `CreateOrJoinLobbyByMatchIdAsync`, `RefreshCurrentLobbyAsync`; `FindLobbyByMatchIdAsync` 미사용화·삭제 안 함], `MatchmakerManager.cs`[비활성화 주석: `DetermineIsHostAsync`/`GetStableHash`], `NetworkGameManager.cs`[추가: `StartMatchmadeGameAsync`/`HostMatchmadeGameAsync`/`JoinMatchmadeGameAsync`, `StartMatchmakingAsync` 분기 교체, 구 클라 참가 경로 `JoinByMatchIdAsync`/`JoinGameByIdAsync` 비활성화 주석]. 클라 참가 경로는 CreateOrJoin 한 곳으로 일원화, RelayJoinCode 채워짐만 대기(`RefreshCurrentLobbyAsync` 최대 15회 폴링). **상태**: 초기 매칭 실기에서 404 없이 정상 연결 확인. 단 **간헐(intermittent) 버그라 지속 테스트 중** — 확정 PASS 아님, 비활성화(주석)한 레거시 코드는 지속 테스트 확정 후 삭제. 잔여 리스크: ① SDK 시그니처 에디터 컴파일 최종 확인 권장, ② "정확히 한 명만 호스트"·간헐 재현 지속 멀티 실기 검증, ③ 클라 RelayJoinCode 대기 15초 타임아웃. 브랜치 `claude/matchmaker-404-error-pi9qdn`, 커밋 `a3dbc73`. task: `_Tasks/2026-07-16/19_09_matchmaker-404-host-determination/` |

#### 랜덤 매칭 2회차 실패 버그 수정 + RuntimeLogger (2026-06-25)
| 항목 | 상태 | 비고 |
|------|------|------|
| 랜덤 매칭 2회차 "Cannot start Host" 버그 수정 | ✅ 완료 (2026-06-25) | 증상: 첫 게임 완료 → 로비 복귀 → 2번째 랜덤 매칭 시 NGO "Cannot start Host while an instance is already running" + 로딩 무한 대기. 원인: `GameEndUI._networkGameManager` Inspector 미연결(null) → ReturnToLobby에서 BackToLobby 미호출 → NetworkManager.Shutdown 없이 씬 전환 → 2번째 매칭 시 IsListening=True로 StartHost 재호출. 수정: `GameEndUI.Initialize()`에 `FindFirstObjectByType<NetworkGameManager>()` 자동 탐색 추가(LobbyUI 동일 패턴). 실기 PASS. task: `_Tasks/2026-06-25/...`(GameEndUI 수정) |
| RuntimeLogger 유틸리티 생성 | ✅ 완료 (2026-06-25) | `Infrastructure/Debug/RuntimeLogger.cs` 신규. `BeginSession(folderPath, role)` / `Log(level, system, className, message, data)` / `EndSession()` API. `#if UNITY_EDITOR`에서 파일 기록, 항상 `Debug.Log` 출력(Logcat 대응). task: `_Tasks/2026-06-25/07_25_runtime-logger/`<br>**⚠️ [2026-08-17 시그니처 변경] 위 API 서술은 2026-06-25 시점 기록이다.** 현행은 **`BeginSession(folderPath, purpose)`** — `role` 인자가 빠지고 **목적 문자열**이 들어갔다(파일명이 `RuntimeLog.txt` 로 단일화되고 헤더 1줄째가 목적을 받게 되면서). 커밋 `4e027e68` · `LogRules.md` **1.4** / **1.10** / **1.11** 참조 |

#### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)
| 항목 | 상태 | 비고 |
|------|------|------|
| 버그 원인 파악 | ✅ 완료 | `_lobbySceneName` Inspector 값 "Game"으로 설정된 것이 근본 원인 |
| "로비로" 버튼 독립 처리 | ✅ 완료 | RPC 제거 → 로컬 Shutdown+LoadScene |
| 30초 자동 복귀 카운트다운 | ✅ 완료 | `WaitForSecondsRealtime` (timeScale=0 대응) |
| 싱글/멀티 통합 `ReturnToLobby()` | ✅ 완료 | NetworkContext 분기 제거 |

#### 멀티플레이 (Phase 1~8)
| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 1 | Lobby/Relay/NGO 연결 인프라 | ✅ 완료 |
| Phase 2 | 팀 할당 + 게임 시작 흐름 | ✅ 완료 |
| Phase 3 | 타일/자원 동기화 (NetworkTileSync, NetworkResourceSync) | ✅ 완료 |
| Phase 4 | 건물 배치 동기화 (NetworkBuildingController) | ✅ 완료 |
| Phase 5 | 유닛 생산 동기화 (NetworkProductionController, 자동생산 포함) | ✅ 완료 |
| Phase 6 | 유닛 이동 + 전투 동기화 (NetworkUnitMovementController, NetworkCombatController) | ✅ 완료 |
| Phase 6+ | AI 이동(Siege/랠리) 서버 권위 동기화 (BroadcastServerMove) | ✅ 완료 (2026-03-07) |
| Phase 6++ | 유닛 NGO NetworkObject 전환 (NetworkTransform 위치 동기화, 클라이언트 예측 제거) | ✅ 완료 (2026-03-26) |
| Phase 6+++ | 이동 전 회전 선행 (Rotate-then-Move, _isPreRotating 플래그로 DOTween-LateUpdate 충돌 해소) | ✅ 완료 (2026-03-27) |
| Phase 6++++ | 공격 타이밍 정밀화 (타격 프레임 데미지, 타겟 고정, 쿨다운 통일) | ✅ 완료 (2026-03-27) |
| Phase 7 | 승패 판정 동기화 (NetworkGameEndController) | ✅ 완료 |
| Phase 8 | UI/UX 네트워크 대응 (LobbyUI, NetworkStatusUI, ReconnectionHandler) | ✅ 완료 |

#### 팀별 관점 (ViewConverter)
| 항목 | 상태 |
|------|------|
| ViewConverter (Core 레이어) | ✅ 완료 |
| Red팀 좌표 반전 (ToView/FromView) | ✅ 완료 |
| 건물/유닛 위치 반전 | ✅ 완료 |
| 입력 좌표 역변환 | ✅ 완료 |
| ViewConverter 초기화 순서 수정 (Setup→LoadMap) | ✅ 완료 |
| 싱글플레이 ViewConverter LocalPlayerTeam 기반 초기화 | ✅ 완료 (2026-03-20) |

---

#### 로비 종족 선택 UI (2026-04-04~06)
| 항목 | 상태 | 비고 |
|------|------|------|
| RaceId enum (Human/Spirit/Transcendence) | ✅ 완료 | Domain 레이어 |
| LocalPlayerRace / GameRaceContext 정적 홀더 | ✅ 완료 | Infrastructure 레이어 |
| RaceSelectionViewModel (UniRx) | ✅ 완료 | 종족 순환 + LocalPlayerRace 연동 |
| RaceSelectionView (캐러셀 + DOTween) | ✅ 완료 | 3캐릭터 원근감 배치, Walk/Idle CrossFade 1초 |
| BattleMainView BindRace 연동 | ✅ 완료 | BattleRootView에서 ViewModel 생성·주입 |
| CharacterPreview RenderTexture 카메라 | ✅ 완료 | CharacterPreview 레이어 격리 |
| 종족 선택 항상 표시 (화면 전환 무관) | ✅ 완료 | BattleMainView에서 RaceSelectionView 독립 토글 제거 |
| 종족명 자연→초월 변경 | ✅ 완료 | Nature→Transcendence (코드+한글 표시명 모두) |
| Pistoleer Idle 첫 프레임 동결 버그 수정 | ✅ 완료 | Pistoleer.controller Idle m_Speed 0→1 |
| Android URP RenderTexture 잔상 + RenderPass 에러 수정 | ✅ 완료 | RT antiAliasing 2→1, allowMSAA/allowHDR false, backgroundColor alpha 1 |

#### 유닛/건물 스탯 확정 적용 (2026-04-12~13)
| 항목 | 상태 | 비고 |
|------|------|------|
| Spirit/Transcendence 6종 HP/ATK/생산시간/비용 확정 | ✅ 완료 | StatsReference.md 기준 |
| Pistoleer MoveSpeed 1.0→0.5 수정 | ✅ 완료 | |
| Transcendence 건물 HP 종족별 분기 | ✅ 완료 | BuildingStats.GetMaxHp(type, RaceId) 오버로드 |
| 생산 패널 골드 비용 텍스트 표기 | ✅ 완료 | _slot1/2/3CostText (숫자만, G 없음) |
| 건물 배치 팝업 골드 비용 텍스트 표기 | ✅ 완료 | _barracksCostText / _miningPostCostText |
| 싱글/멀티 실기 테스트 | ✅ PASS | TC-SINGLE-01~10, TC-MULTI-01 전체 PASS |

#### 피격 시 부유 HP 텍스트 — World Space (2026-04-12~13, 2026-04-17 World Space 전환)
| 항목 | 상태 | 비고 |
|------|------|------|
| FloatingHpText.cs (DOTween 애니메이션, 오브젝트 풀 반환) | ✅ 완료 | TextMeshPro 3D World Space |
| FloatingHpTextSpawner.cs (이벤트 구독, 풀 관리) | ✅ 완료 | 월드 좌표 직접 사용, 좌표 변환 없음 |
| FloatingHpText 프리팹 (Maplestory Light SDF FloatingHpText Material.mat) | ✅ 완료 | 독립 .mat 파일 사용 (폰트 .asset 오염 방지) |
| 줌 연동 비율 일관성 (scale=1f 고정) | ✅ 완료 (2026-04-17) | 텍스트가 유닛/건물처럼 줌 비례 동작 → 모든 줌에서 유닛 대비 비율 일정 |
| 빌보드 회전 + 좌우 반전 보정 | ✅ 완료 (2026-04-13) | LookRotation(-forward,up) + localScale(-s,s,s) |
| 멀티플레이 클라이언트 표시 (NetworkHealthSync 재발행) | ✅ 완료 | |
| 팀별 텍스트 색상 (Blue=연두, Red=노랑) | ✅ 완료 (2026-04-13) | Inspector SerializedField로 조정 가능 |
| SetupFloatingHpText 에디터 스크립트 자동화 | ✅ 완료 | Hexiege/Setup/FloatingHpText 설정 |
| 싱글플레이 실기 테스트 | ✅ PASS | TC-1~6 전체 PASS, TC-7(멀티) 미확인 |

#### 종족 인게임 적용 (2026-04-07)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | GameRaceContext 기반 (race, team) 튜플 switch |
| BuildingFactory 종족별 6세트 프리팹 분기 | ✅ 완료 | MiningPost 포함 종족별 분기 |
| GameBootstrapper 싱글 GameRaceContext 초기화 | ✅ 완료 | LoadMap() 직전 Set(LocalPlayerRace.Current, 랜덤종족) — Enum.GetValues + Random.Range (2026-04-24 랜덤으로 변경) |
| 오브젝트 이름 실제 프리팹명 반영 | ✅ 완료 | {prefab.name}_{id} 형식 |
| 에디터 자동 연결 스크립트 | ✅ 완료 | Hexiege/Setup/UnitFactory·BuildingFactory 프리팹 연결 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-01~06 전체 통과 |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-01 통과 |

#### UnitType 개편 + 근접 사거리 시스템 (2026-04-10~11)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitType enum 유닛별 독립 식별자로 개편 | ✅ 완료 | Pistoleer=0 ~ LionKnight=8 (9종) |
| UnitFactory List<UnitPrefabEntry> 구조 변경 | ✅ 완료 | 종족별 리스트 (type/blue/red) |
| Spirit/Transcendence UnitStats 추가 | ✅ 완료 | Range/Cooldown/HitFrameTime 확정, HP/ATK 미정 |
| ProductionPanelUI 종족별 버튼 동적 바인딩 | ✅ 완료 | BindButtonUnitTypes(RaceId), 6세트 초상화 필드 |
| 생산 패널/건물 배치 종족별 초상화 스프라이트 연결 | ✅ 완료 (2026-04-13) | Spirit/Transcendence 유닛+건물 초상화 Inspector 연결 완료 (Spirit Blue ManaRift 2026-04-13 제작 완료) |
| 근접 유닛 Castle 방향 Lerp 이동 + 공격 | ✅ 완료 | FindPathToNeighbor + 경로 끝에 Castle 타일 추가 |
| 다중 유닛 Castle 연속 공격 (ClaimedTile 버그 수정) | ✅ 완료 | 마지막 non-walkable 타일 ClaimedTile 설정 생략 |
| SetupUnitFactoryPrefabs 에디터 스크립트 재작성 | ✅ 완료 | List 구조 대응 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-001~006 통과 (003/004 스프라이트 CONDITIONAL PASS) |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-001 통과 |

#### 원거리 유닛 공격 중 회전 추적 (2026-04-11~12)
| 항목 | 상태 | 비고 |
|------|------|------|
| 공격 중 타겟 방향 지속 추적 | ✅ 완료 | Transform 참조 저장 + Update() RotateTowards |
| 멀티플레이 타이밍 방어 (B사망→C전환 고착 버그) | ✅ 완료 | 백업 ID 필드 + Update() 재조회 로직 |
| 타겟 고착성 (현재 타겟 생존 중 교체 방지) | ✅ 완료 | IsCurrentTargetStillValid() 추가, TickCombat 2곳 수정 |
| 타겟 변경 시 부드러운 회전 전환 | ✅ 완료 (2026-04-12) | RotateTowards(270°/s), ChangeTarget() 즉시 스냅 제거 |
| 멀티플레이 실기 테스트 | ✅ PASS | MULTI-001~007 전체 통과 |

#### 근접유닛 적 감지 사거리 + 추적 회전 개선 (2026-04-19~24)
| 항목 | 상태 | 비고 |
|------|------|------|
| DetectRange / AttackRange 분리 | ✅ 완료 (2026-04-19) | 근접유닛 DetectRange=1.0f(타일), 원거리는 AttackRange와 동일 |
| 하이브리드 이동 시스템 (Phase 0/1/2) | ✅ 완료 (2026-04-19) | Phase 1 월드 직선 추적, Phase 2 타일 스냅 후 A* 재개 |
| Phase 1 추적 중 타겟 방향 회전 개선 | ✅ 완료 (2026-04-24) | CalculateAttackAngle + RotateTowards(270°/s), 이전 타일 방향 고정 문제 해소 |
| 싱글플레이 실기 테스트 | ✅ PASS | SINGLE-001~002 통과 (2026-04-24) |

#### 타일 소유권 실시간 감지 — TileOwnershipService (2026-04-26)
| 항목 | 상태 | 비고 |
|------|------|------|
| `TileOwnershipService.cs` 신규 생성 | ✅ 완료 | `Application/Services/` — Pull 모델, 매 프레임 유닛 물리 위치 기반 타일 소유권 결정 |
| HashSet 풀(`Queue<HashSet<TeamId>>`) | ✅ 완료 | 매 프레임 GC 최소화 |
| 점령 규칙 (한 팀/양 팀/없음) | ✅ 완료 | 한 팀만 있을 때만 갱신, 나머지 현 상태 유지 |
| 이벤트 중복 발행 방지 | ✅ 완료 | `GetOwner != claimingTeam` 조건 가드 |
| `HexGrid.GetOwner(HexCoord)` 신규 추가 | ✅ 완료 | `_tiles.TryGetValue` → `tile.Owner` |
| GameBootstrapper Tick 연결 | ✅ 완료 | `(!NetworkContext.IsNetworkActive \|\| NetworkContext.IsNetworkServer)` 가드 |

#### 근접유닛 뒷무빙 5차 개선 (2026-04-26)
| 항목 | 상태 | 비고 |
|------|------|------|
| Phase 1 타겟 사망 시 즉시 다음 적 재선택 | ✅ 완료 | 다음 적 있으면 Phase 1 유지, 없으면 Phase 2 진입 |
| 전투 루프 종료 후 다음 타겟 선택 | ✅ 완료 | 전투 종료 후에도 감지 범위 내 적 재탐색 |
| Phase 2 후방 스냅 방지 | ✅ 완료 | `HexCoord.Distance` 비교로 nearestTile이 finalTarget보다 멀면 현 위치 유지 |
| Phase 2 점유 누수 방지 | ✅ 완료 | `nearestTile == _unitData.Position`이면 `RegisterOccupancyMove` 생략 |

#### 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25)
| 항목 | 상태 | 비고 |
|------|------|------|
| UnitStatsConfig ScriptableObject (전투+생산 통합) | ✅ 완료 | `UnitStatEntry` struct, Inspector에서 9종 유닛 수치 편집 |
| BuildingStatsConfig ScriptableObject (건물타입별 종족 묶음) | ✅ 완료 (2026-05-18 갱신) | `BuildingTypeEntry` B방식, 32종 BuildingType × 3종족 전체 항목 채움. `AttackCooldown` (float) 필드 추가 — AutoTower 종족별 쿨다운 적용 (Human/Trans 5.0s, Spirit 3.5s). `BuildingStats.GetAttackCooldown(type, race)` API 신규. |
| UnitStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<UnitType, StatValues>`, `Initialize()` 추가 |
| UnitProductionStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<UnitType, ProductionValues>`, `Initialize()` 추가 |
| BuildingStats switch → Dictionary 전환 | ✅ 완료 | `Dictionary<(BuildingType, RaceId), StatValues>`, HP+골드+공격력 통합 |
| BuildingStats.GetGoldCost / GetAttackPower 신규 메서드 | ✅ 완료 | 건물 배치 비용 조회 + 향후 타워 기능 대비 |
| GameBootstrapper Initialize 연결 | ✅ 완료 | `_unitStatsConfig`, `_buildingStatsConfig` SerializedField |
| BuildingPlacementUI GetBuildingCost BuildingStats 연동 | ✅ 완료 | `BuildingStats.GetGoldCost(type, race)` |
| 에디터 자동 생성 스크립트 2종 | ✅ 완료 | `Hexiege/Setup/UnitStatsConfig 생성`, `Hexiege/Setup/BuildingStatsConfig 생성` |
| 에셋 파일 생성 | ✅ 완료 | `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `BuildingStatsConfig.asset` |

#### Lobby 패널 CanvasGroup 사전 부착 + ProfileView 로그아웃 버튼 (2026-06-22)
| 항목 | 상태 | 비고 |
|------|------|------|
| ProfileView 로그아웃 버튼 UI 추가 | ✅ 완료 | `AddLogoutButtonToProfileView.cs` 에디터 스크립트. LogoutButton GO 생성 + ProfileView._logoutButton 연결. 임시 배치(추후 재설계 예정) |
| Lobby 탭 패널 CanvasGroup 에디터 사전 부착 | ✅ 완료 | `SetupLobbyPanelCanvasGroups.cs` 에디터 스크립트. 4개 패널 활성화 + CanvasGroup 초기값 설정 |
| LobbyRootView EnsureCanvasGroup → GetComponent 전환 | ✅ 완료 | 런타임 AddComponent 방식 폐기. EnsureCanvasGroup() 헬퍼 제거 |

#### Lobby 씬 UI 규칙 재점검 및 추가 수정 (2026-06-15)
| 항목 | 상태 | 비고 |
|------|------|------|
| Rule 5: `LobbyUI._lobbyPanel` CanvasGroup 전환 | ✅ 완료 | `GameObject` → `CanvasGroup`, SetActive → alpha/blocksRaycasts/interactable |
| Rule 6: `LoadingScreen > StatusText` 폰트 교체 | ✅ 완료 | LiberationSans SDF → Maplestory Light SDF. `FixLobbyRuleViolations.cs` 에디터 스크립트 실행 |
| `BattleRootView.cs` 미사용 `using System;` 제거 | ✅ 완료 | 코드 정리 |
| 규칙 1~6 전수 재점검 (YAML 173개 GO) | ✅ 완료 | Rule 1/2/4/5/6 전체 준수. Rule 3 실기 확인 권장 |

#### Lobby UI 전체 규칙 준수 수정 (2026-05-30)
| 항목 | 상태 | 비고 |
|------|------|------|
| Lobby.unity 규칙 위반 25건 전수 수정 | ✅ 완료 | 규칙 1(Toast CanvasScaler 추가) 1건, 규칙 2(앵커 비율화) 23건, 규칙 5(CanvasGroup 초기값) 1건. 에디터 스크립트 4종(GroupA~D) 실행 적용 |
| 규칙 1~6 전체 준수 검증 | ✅ 완료 | Explore 에이전트 정적 분석 + 실기 확인. 위반 0건 |

#### 공통 UI 규칙 수립 및 Canvas Scaler / SafeArea 적용 (2026-05-25)
| 항목 | 상태 | 비고 |
|------|------|------|
| 공통 UI 규칙 10개 확정 (GameSystemRules.md) | ✅ 완료 | Canvas Scaler, 앵커 기반 레이아웃, SafeArea, CanvasGroup 패턴, 폰트, 골드 부족 UI, 팝업/모달 구분 등 |
| Canvas Scaler 통일 (Rule 1) | ✅ 완료 | SetupCanvasScaler.cs 에디터 스크립트. Game.unity(540×960→1080×1920, 0.5→0) + Lobby.unity Canvas2(0.5→0) |
| SafeAreaFitter.cs 신규 구현 (Rule 4) | ✅ 완료 | Screen.safeArea 기반 anchorMin/anchorMax 정규화. `Presentation/UI/Common/` |
| SafeAreaContainer 씬 구조 적용 (Rule 4) | ✅ 완료 | SetupSafeAreaContainer.cs 에디터 스크립트. Game.unity 7개 UI 이동, Lobby.unity 캔버스별 적용, ToastUI SafeAreaFitter 직접 부착 |
| 전체 UI 규칙 준수 검증 (Rule 5 CanvasGroup 전환) | ✅ 완료 (2026-05-27) | 로비 7개 뷰 SetActive → CanvasGroup 전환. LobbyRootView, BattleMainView, CustomGameView, CustomHostView, CustomJoinView, RandomMatchView, ProfileView. 실기기 TC 전체 PASS (2026-05-27): TC-SINGLE-001~014 PASS, TC-SINGLE-015~016 SKIP(로그인 미구현). 랜덤 매칭 대기화면 BUG(GameObj inactive)→ Inspector에서 직접 수정 완료 |
| 로비 배경 Safe Area 수정 (Rule 4) | ✅ 완료 (2026-05-26) | LobbyRoot Image가 SafeAreaContainer 안에 있어 Safe Area 경계에서 배경이 끊기는 문제. Canvas 직속 자식 LobbyBackground 오브젝트 신규 추가(전체화면 stretch, 남색), LobbyRoot Image 비활성화. FixLobbyBackground.cs 에디터 스크립트로 적용. 실기기 테스트 PASS |

#### 게임 화면 UI TC 전체 실기기 테스트 (2026-05-27)
| 항목 | 상태 | 비고 |
|------|------|------|
| 게임 화면 UI TC 62개 실기기 테스트 | ✅ 완료 | `Assets/_Project/Docs/_Tasks/2026-05-26/game-ui-tc/Testcase.md` 참조 |
| TC-MULTI-END-001 버그 수정 (BUG-001) | ✅ 완료 | 포기 시 호스트 GameEndUI 미표시. `NetworkGameEndController.ForfeitServerRpc`에 `GameEvents.OnGameEnd.OnNext()` 1줄 추가. 근본 원인: ForfeitServerRpc가 GameEndUseCase를 건너뛰어 서버 측 OnGameEnd 미발행 |
| TC-MULTI-END-001 버그 수정 (BUG-002) | ✅ 완료 | 재경기 팝업이 GameEndUI 뒤에 가려짐. Canvas Hierarchy에서 RematchRequestPopup을 SafeAreaContainer 이후 인덱스로 이동 (Inspector). AnimatedPanel.Show()에 SetAsLastSibling() 없음 확인 → 영구 수정 |
| UI 크기/레이아웃 FAIL 항목 (BP-001/002, BAP-001, PRD-001 완료) | ⚠️ 일부 완료, 나머지 별도 작업 예정 | ✅ BP-001/BP-002(건물 배치 패널): 씬 계층 재설계로 해결 (2026-05-29). ✅ BAP-001(비생산 건물 패널): 3x3 그리드 + 런타임 슬롯 제어로 해결 (2026-05-29). ✅ PRD-001(패널 버튼 크기): LayoutElement(prefH=0,flexH=1) Row 적용 + Rallypoint 패딩 수정으로 해결 (2026-05-31). ❌ HUD-007(설정 버튼), SET-004(확인팝업 버튼 색상), SET-007/END-001(UI 크기), MULTI-END-002(재경기 UI 크기) |

#### BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 (2026-05-29)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingActionPanel 3x3 그리드 구조 재구성 | ✅ 완료 | BuildingPlacementUI와 동일한 VLG+HLG 중첩 패턴. 래퍼 오프셋 제거(anchoredPosition=0,sizeDelta=0). CancelButton 위치 BuildingPlacementUI와 통일 |
| 런타임 슬롯 표시/숨김 제어 | ✅ 완료 | BuildingActionPanelUI.cs에 _allSlotButtons/_activeSlotButtons 추가. OnShow() 오버라이드에서 CanvasGroup alpha 제어. BuildingPlacementUI._buttonCanvasGroups 패턴과 동일 |
| HeaderText 앵커 순수 앵커 기반 변환 | ✅ 완료 | anchorMin=(0.096,0.826), anchorMax=(0.867,1.006), pos=(0,0), delta=(0,0) |

---

#### BuildingPlacementUI 씬 계층 재설계 (2026-05-29)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingPlacementUI 씬 계층 전면 재구성 | ✅ 완료 | TC-SINGLE-BP-001/002 FAIL 해결. GridLayoutGroup 제거 → VLG+HLG 중첩 구조(GameSystemRules Rule 2). 3행×3열 그리드 정상 표시. 순수 앵커 기반(anchoredPosition=0, sizeDelta=0) |
| BuildingPlacementUI.cs _buildingGoldIcons 필드 추가 | ✅ 완료 | 버튼 내부 골드 아이콘 Inspector 참조 신규 |
| GameSystemRules.md Rule 2 보완 | ✅ 완료 | Layout Group 반응형 패턴 명세 추가 (VLG+HLG 중첩 + Control Child Size + Force Expand) |
| GameSystemRules 준수 검증 (Rule 2/4/5/6) | ✅ 완료 | BuildingPopup 전체 계층 씬 YAML 직접 파악 + 검증 통과 |
| RebuildBuildingPlacementUI.cs 에디터 스크립트 | ✅ 완료 | Assets/Editor/ — 재구성 재실행 가능한 1회성 스크립트 |

---

#### 설계 규칙 문서 (GameSystemRules.md)
| 항목 | 상태 | 비고 |
|------|------|------|
| 유닛 이동 시스템 규칙 1~8 | ✅ 확정 (2026-05-14 개편) | 이동/전투/회전 규칙 통합, 총 16개 규칙으로 재번호화 |
| 회전 규칙 통합 | ✅ 확정 (2026-05-14) | 규칙 7(A* 이동 중 서서히 회전), 8(재개 시 이동 방향 바라봄), 12(전투 이동 중), 15(공격 중) — 이동/전투 규칙에 통합 |

#### 인증 시스템 설계 규칙 문서 (AuthSystemRules.md)
| 항목 | 상태 | 비고 |
|------|------|------|
| AuthSystemRules.md 작성 | ✅ 완료 (2026-05-23, 2026-06-10 갱신) | Firebase Auth 기반. 로그인 3종(익명/Google Play Games/이메일+비밀번호). Firebase ID Token → UGS OIDC Bridge(`SignInWithOpenIdConnectAsync`) 설계. UGS 데이터 플랫폼(Cloud Save/Leaderboard/Economy) 채택 확정 |

---

#### 로그인 시스템 C# 구현 (2026-05-24)
| 항목 | 상태 | 비고 |
|------|------|------|
| Firebase SDK v13.11.0 설치 | ✅ 완료 | FirebaseAuth.unitypackage 임포트. FirebaseApp은 v12+에서 각 패키지에 번들됨 (별도 임포트 불필요) |
| Google Play Games Plugin v2.1.0 설치 | ✅ 완료 | GitHub `current-build/` 폴더 내 .unitypackage 임포트. v1은 2026년 5월부터 deprecated |
| EDM4U Android 의존성 해결 | ✅ 완료 | Custom Main Gradle Template + Custom Gradle Properties Template 활성화 (Jetifier 필요). Multidex 불필요 (Min API 25) |
| FirebaseAuthService.cs (Infrastructure) | ✅ 완료 | Firebase SDK 래퍼. 익명/Google/이메일 로그인 API 제공. AuthException + AuthErrorReason enum 정의. SignInWithCredentialAsync 반환값 FirebaseUser로 수정 (SDK 13.x 호환) |
| LoginUseCase.cs (Application) | ✅ 완료 (2026-06-10 갱신) | 로그인 흐름 조율. BridgeToUGSAsync(Task<bool>) — 실계정: OIDC Bridge(`SignInWithOpenIdConnectAsync("oidc-firebase")`), 익명: UGS 익명 로그인 분기 구현 완료 |
| AccountLinkUseCase.cs (Application) | ✅ 완료 | 익명 → 실계정 연동 흐름 |
| LoginBootstrapper.cs (Bootstrap) | ✅ 완료 | Login.unity 씬 Composition Root. PlayGamesPlatform.Activate() + Firebase 초기화 + DI |
| LoginRootView.cs (Presentation) | ✅ 완료 | 패널 전환 + Back 스택 + Android 뒤로가기. Application.Quit() → UnityEngine.Application.Quit() 수정 (Hexiege.Application 네임스페이스 충돌 방지) |
| LoginSelectView.cs | ✅ 완료 | 로그인 방식 선택 화면 |
| EmailLoginView.cs | ✅ 완료 | 이메일 로그인 화면 |
| SignUpView.cs | ✅ 완료 | 이메일 회원가입 화면 |
| EmailVerifyView.cs | ✅ 완료 | 이메일 인증 대기 화면 |
| PasswordResetView.cs | ✅ 완료 | 비밀번호 재설정 화면 |
| AnonymousWarningPopup.cs | ✅ 완료 | 익명 로그인 경고 팝업 |
| ProfileView.cs (Lobby — 수정) | ✅ 완료 | 계정 정보 표시, Google/이메일 연동 버튼, 로그아웃 UI 구현 |
| UnityServicesInitializer.cs (수정) | ✅ 완료 (2026-06-10 갱신) | OIDC 세션 보존 로직으로 교체. 기존 세션 있으면 재로그인 없이 보존, 세션 없을 때만 익명 폴백. 기존 "항상 재로그인" 블록은 주석 처리(테스트 통과 후 최종 삭제 예정) |
| 컴파일 에러 전체 해결 | ✅ 완료 | CS0103(AuthException/AuthErrorReason using 누락), CS0029(SignInWithCredentialAsync 반환 타입), CS1061(SignInWithCustomIdAsync 미지원), CS0234(Application.Quit 네임스페이스 충돌) 전체 해결 |
| 기존 UGS 로그인 동작 보존 | ✅ 완료 | Lobby.unity 직접 실행 시 익명 로그인으로 PlayerId 발급 — 멀티플레이 기능 정상 동작. 401 버그 수정 후 커스텀 게임 + 랜덤 매칭 모두 확인 |
| Firebase Console 설정 | ✅ 완료 (2026-06-27) | google-services.json SHA-1 3개 + 실제 빌드 키스토어 SHA-1(`18:E0:...:3D`) 등록, Firebase Authentication Play Games 제공업체 활성화(Web Client ID/Secret). Google 로그인 실기 성공. |
| GPGS 클라이언트 ID + Play Console 사용자 인증 정보 | ✅ 완료 (2026-06-27) | Web Client ID 입력 완료. Play Console GPGS 사용자 인증 정보에 실제 빌드 키스토어 SHA-1 등록·게시 완료(GPGS `signIn()` 검증 통과). |
| Login.unity 씬 생성 | 🔵 부분 완료 (2026-06-18) | UIManager + SplashOverlay 배치 완료. 로그인 UI 배치(UIWireframe.md 기반) + Inspector 연결은 미완료 — 추후 진행 |
| Firebase → UGS OIDC Bridge | 🔵 코드 완료 (2026-06-10) / 실기 실패 (2026-06-27) | `SignInWithOpenIdConnectAsync("oidc-firebase", firebaseToken)` 구현 완료. 실기 결과 `id provider not found`로 실패 — UGS Dashboard OIDC 제공자(`oidc-firebase`) 미등록. UGS PlayerId 미발급으로 멀티플레이 제한. UGS Dashboard OIDC Provider 등록 후 재확인 필요. |

---

#### 유닛 이동/전투 시스템 재설계 (2026-05-11~13)
| 항목 | 상태 | 비고 |
|------|------|------|
| 슬롯 시스템 전면 폐기 | ✅ 완료 (2026-05-11) | TileMoveSlotManager / TileOccupancyManager / AttackPositionManager 비활성화(주석 처리). GameBootstrapper 주입 코드 포함 |
| 타일 점유 한도(OccupancySize) 제거 | ✅ 완료 (2026-05-11) | UnitData.ClaimedTile / UnitStats.OccupancySize / UnitMovementUseCase 점유 메서드 비활성화 |
| 근접/원거리 동일 상태 머신 (MoveAlongPathV3) | ✅ 완료 (2026-05-11) | Phase 0(A* Lerp) → 감지 → Phase 1(직선 추격) → 공격 → FindForwardClosestTile → 재개. 겹침 허용 |
| UnitCombatUseCase DetectRange 판정 통합 | ✅ 완료 (2026-05-11) | isMelee 분기 제거, 모든 유닛 DetectRange × TileHeight 통일 |
| 원거리 유닛 DetectRange 수치 분리 | ✅ 완료 (2026-05-11) | UnitStatsConfig Inspector에서 원거리 유닛 DetectRange > AttackRange 설정 |
| 추격 중 건물 변화 시 유닛 멈춤 (BUG-001) | ✅ 완료 (2026-05-12) | `_isInCombatPursuit` 필드 추가, IsInCombat() 추격 단계도 전투 중으로 판정 |
| 전투 종료 후 순간이동 (BUG-002) | ✅ 완료 (2026-05-13) | 즉시 스냅 제거 → 정렬 Lerp(동일 이동 속도)로 교체. 정렬 중 적 감지 시 즉시 전투 재진입 |

---

#### 유닛 생산 실패 피드백 시스템 (2026-05-16)
| 항목 | 상태 | 비고 |
|------|------|------|
| 범용 토스트 UI 시스템 (ToastUI) | ✅ 완료 | DontDestroyOnLoad 독립 Canvas, 큐 방식, 터치 제거, DOTween 페이드아웃 |
| ToastMessageConfig ScriptableObject | ✅ 완료 | Resources/Config — 메시지/노출시간 Inspector 편집 가능 |
| 골드 부족 피드백 — 유닛 생산 비용 텍스트 빨간색 | ✅ 완료 | `_unitCostTexts[i]` 개별 평가 (보유 골드 표시 텍스트는 변경 안 함) |
| 인구 초과 피드백 — HUD 인구수 텍스트 빨간색 | ✅ 완료 | `used >= max` 조건 매 갱신 시 색상 자동 전환 |
| 수동 생산 실패 토스트 3종 | ✅ 완료 | GoldInsufficient / PopulationFull / ProductionQueueFull |
| 자동 생산 자원 부족 시 즉시 취소 | ✅ 완료 | IsCharged=false 항목만 취소, IsCharged=true는 Rule 2 유지 |
| 싱글플레이 실기 테스트 | ✅ PASS (골드부족·큐초과) | 인구초과·자동취소는 코드 검토 완료 |

#### 건물 배치 패널 실패 피드백 + UI 개선 (2026-05-16)
| 항목 | 상태 | 비고 |
|------|------|------|
| 골드 부족 시 건물 비용 텍스트 빨간색 | ✅ 완료 | 팝업 열릴 때 + OnResourceChanged 구독으로 실시간 재평가. 팝업 닫힐 때 흰색 초기화 |
| 골드 부족 시 토스트 메시지 | ✅ 완료 | `ToastKey.GoldInsufficient` 재사용. 팝업 유지(Close 호출 없음). 싱글플레이 분기만 적용 |
| 건물 비용 텍스트 'G' 접미사 제거 | ✅ 완료 | "200G" → "200". 생산 패널과 동일 표기로 통일 |
| ToastUI SetActive 버그 수정 | ✅ 완료 | ClearAll/FinishCurrent의 SetActive(false) 제거 → CanvasGroup.blocksRaycasts+interactable로 대체. 루트 항상 활성 유지 |
| 싱글플레이 실기 테스트 | ✅ PASS | 골드 부족 비용 텍스트 빨간색, 토스트 메시지, 팝업 유지 동작 확인 |

#### 건물 업그레이드 시스템 (2026-05-17~18)
| 항목 | 상태 | 비고 |
|------|------|------|
| BuildingType enum 26종 확장 | ✅ 완료 | 단일 Barracks 제거 → 종족별 생산라인×단계(1/2/3) |
| BuildingTypeHelper.cs 신설 (Domain) | ✅ 완료 | IsProductionBuilding / GetStage / GetNextStage / CanUpgrade |
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
| 랠리포인트 조준 시 반투명 오버레이 잔존 (집결지 지정이 취소된 것처럼 보임) | ✅ 완료 (2026-08-08, 실기 PASS·커밋 `9a19cd5`) — 랠리포인트 버튼 탭 시 팝업만 숨겨지고 공유 BlockingOverlay(탭=`Close`)가 `blocksRaycasts=true`로 잔존 → 맵 탭을 오버레이가 먼저 가로채 `Close()`→`OnBeforeClose()`로 조준 취소·랠리 마커 숨김. `ProductionPanelUI.cs` 2줄 순수 추가로 수정: ① `OnRallyPointClick()`에 `HideBlockingOverlay()`(스킬 패널 조준 진입과 동일 패턴) ② `Close()`를 거치지 않는 `CompleteRallyPointSetting()`에 참조 카운터 반납(②를 빠뜨리면 다음 팝업 오버레이가 어긋남). 랠리포인트 규칙 2의 "설정 직후 3초 표시"도 정상화. task `_Tasks/2026-08-08/13_33_rally-point-blocking-overlay-bug/` |
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

#### 로그 체계(`GameLog`) 전환 — 미완 항목 (2026-08-17 기준)
| 항목 | 상태 | 비고 |
|------|------|------|
| **컴파일 검증** (`4e027e68` · `675203ae` · `a253232e`) | ✅ **통과 확인됨 (2026-08-17)** | 205건 이관분(`668e0aeb` 계열)에 이어 **마스킹·파일명 단일화·헤더·정리 메뉴 변경분**과 **씬 무관 구현(`a253232e`)까지 사용자 유니티에서 통과가 확인**되었다 — 사용자가 그 상태로 실기 테스트를 수행했다. **⚠️ 단, 후속 커밋 `73574a23`(`Role` 값 표기 통일)은 명시적인 컴파일 확인을 받지 않았다** — 문자열 리터럴 대소문자 1줄 변경이지만 "통과했다"고 적지 않는다 |
| **실기 동작 테스트** | ✅ **PASS (2026-08-17)** | ~~"⚠️ 하지 않았다"~~ → 사용자가 랜덤매칭 실기(에디터+실기기)로 확인했고 근거 로그가 `_Logs/_editor/2026-08-17/RuntimeLog.txt`(**199줄**)에 커밋되어 있다. 확인 항목: 파일명 `RuntimeLog.txt` · 헤더 3줄 규정(목적/시각/빈 줄) · 199줄 축적 · **같은 파일에 2번째 헤더**(일 단위 이어쓰기) · `Role=` 기록 · **`[Auth/...]` 7건**(씬 무관 구현의 목적 달성) · 마스킹(`Uid`·`PlayerId` **16자리 16진수** · `Email=` **0건** · `@` 포함 줄 0건) · 감사 누락분 `HostId` 가 `PlayerId` 와 **같은 해시** |
| **수동 정리 메뉴 실기** (`Hexiege > Logcat > 3.`) | ✅ **PASS (2026-08-17)** | ~~"실제로 실행해 본 적이 없다"~~ → 실제로 실행했다. `_Logs/_editor/` 에 `2020-01-01`(날짜 형식)과 `2020.01.01`(점 구분)을 두고 확인: **`2020-01-01` 삭제 · `2020.01.01` 제외**(`TryParseExact("yyyy-MM-dd")` — **느슨한 `TryParse` 였다면 삭제됐을 케이스**) · **오늘 폴더 보존** · 취소 경로 · **정리 범위 한정 — `_Logs/` 직속 날짜 폴더 17개를 하나도 건드리지 않음**(`LogRules.md` **1.10** 이 *"작업에 귀속된 영구 보존물"* 로 규정한 영역이라 지워지면 복구 불가) |
| **`Lobby.unity` 직접 진입 로그 수집** | **커버하지 않음 — 사용자 결정** | 이 씬에는 부트스트래퍼가 없어(참조 0건) 직접 열고 실행하면 sink 가 0개라 콘솔 폴백만 탄다. **사용자가 "로비를 직접 열어 실행하는 경우는 없다(항상 Login 을 거친다)" 고 판단해 커버하지 않기로 결정**했다 — 미해결 결함이 아니라 **범위 밖 확정 항목** |
| **나머지 계층 `Debug.Log` 이관** | 미착수 (별도 task 분리 결정) | `Assets/_Project/Scripts/` 전체 잔존 **234건** — 이번 8파일 **밖**이다. 그중 `Application/GameLog.cs` **9건은 이관 대상이 아니다**(sink 폴백 구현 — `LogRules.md` **1.8** 이 요구하는 동작 그 자체) |
| **`NetworkCombatController.cs` raw `Debug.Log`** | 미이관 (위 항목에 포함) | 이 파일은 8파일 목록 밖이라 잔존 **11건**이다. 그중 하나가 **새 역할 로그와 나란히 `IsServer` 를 찍는 중복**이다 |
| **`FileSink.EditorLogsRootRelativeToAssets` 가 `private`** | 미해결 (범위 밖) | `LogcatCapture.cs` 가 같은 경로 문자열을 **복제**하고 「FileSink 와 동기화 필요」 경고 주석을 달았다. `internal const` 로 올리면 복제를 없앨 수 있다 |
| **`_Logs/_editor/` 의 `.meta` 취급 규정** | 규정 공백 — **사용자 판단 대기** | `.gitignore` 는 이미 규정에 부합한다. `LogRules.md` 는 `.meta` 를 언급하지 않는다. 사용자가 커밋 `23a8da06` 에서 `.meta` 를 실제로 커밋했으나, **그 관찰만으로 규정이 확정된 것으로 적지 않는다** |

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
| ~~연구소 (Research Lab)~~ | ✅ 완료 | 유닛 강화 시스템·연구 패널 UI 구현·멀티 실기 완료(2026-07-31). 3D 모델/UI 레이아웃 다듬기만 후속 |
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
