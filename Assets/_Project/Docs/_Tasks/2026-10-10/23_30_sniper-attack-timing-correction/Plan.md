# Plan — Sniper 1:22 타격 시점 교정

> **Sniper 최종 소스/정적 성공·UI 검증 차단(Main):** 승인6파일 구현 완료, Main6해시 재대조 전부 일치·추가 source patch0. static4 반복 exit0/errors0(warnings Runtime20/Editor0/Development20/Release24), actual generator37/37. AssetsRefresh 후 새 Unity Editor.dll23:49:28 / Runtime23:49:30 및 새 disk IL의 Sniper 두 generator case 확인(programmer). reload 후 실제 Console errors0/warnings20. **23:50 Windows 보안 modal: Relay server for Unity AI Assistant public/private network allow/cancel**로 UI 중단; Main은 보안 dialog를 조작하지 않았다. 사용자 직접 보안 선택 대기, 현행 소스의 fresh UnitAction/ROOT 및 Build And Run 미실행/차단. 23:45 UnitAction FAIL은 남는 실패 이력이지 현행 source 결함이나 재실행 PASS가 아니다. native 전체·device/timing/pixel 수용 OPEN, 기존 Human/VFX 보류·Inferno 확인 금지 유지.

**[2026-10-10 23:49 원인 정정 — 이전 실패/원문 보존]** 23:45 FileNotFound는 현행 generator 소스의 Sniper 목록 누락이 아니었다. current source/static generator에는 이미 두 Sniper case가 있었고, 오래된 Unity disk IL generator와 새 parser의 generation mismatch가 확인됐다. 추가 코드 patch0, source6해시 불변. AssetsRefresh/new DLL 확인 및 actual generator37/37은 새 native UnitAction 전체 PASS가 아니다. 아래 '소스/생성 목록 누락·배선 보완'은 당시 잠정 진단이며 이 정정이 supersede한다.

> **Fresh native23:45:34 FAIL(Main 실제 메뉴):** ActualCLI가 Temp/pistoleer-timing-fixtures/sniper-Host.txt FileNotFound로 실패했다. parser case 목록 확장과 Unity actual formatter fixture generation 목록이 불일치(Sniper 이름 누락)했다. Main이 승인6파일 범위의 기존 gate wiring만 보완하도록 programmer에 지시했으며 기존 assertions는 유지한다. 수정/최종 재실행 결과 대기; 이전 static4·in-memory CLI37/37은 native PASS가 아니며 build 시작 없음. RED 이력을 보존한다.

> **Sniper 구현·정적 인계(programmer) / Main 최종 대조·native 대기:** 승인6파일 구현 완료 보고. static4 exit0/errors0, warnings Runtime20/Editor0/Development20/Release24. 기존 CLI35 + Sniper in-memory2 = 37/37, 새 fixture 파일 없음. Game one-field normalized initial SHA256 1BD0AC04E04D983EF75EFA5DCFBB68A452F9E789457786B4E3FE807B71F34A4B는 경계 대조용 초기값이며 최종 Game 해시가 아니다. 실제6파일 final hashes/Main 독립 검토는 대기, artifacts Temp/expiry-clock-history. UnitView·Assault policy·Human/Cannon 참조·VfxConnections 보존 및 Inferno access 없음은 programmer 인계. 새 native/UI gate·build 시도 결과 없음, runtime/timing/pixel OPEN. 아래 계획 대기는 당시 이력이다.

사용자가 선택한 현재 Sniper 1:22 공격 표현을 보존하면서 서버 타격 설정과 명시 production clip을 정합화하고, 실제 공격부터 결과·Client 표현까지 같은 키의 근거를 수집하는 것이 목적이다.

## Main 기술 Plan 검토 완료 / programmer GO / 구현 인계 대기

Main이 아래 정확한6파일 범위를 검토·승인했다. GO는 구현 허가이며 적용 완료·새 static/native/build 결과는 최종 인계 대기다. 기존 공용 hooks를 쓰므로 UnitView 변경은 필요하지 않으며 승인 범위에 포함하지 않는다. 아래 초기 계획의 검토 대기는 이전 단계다.

### 승인 기술 manifest — 6파일

| 파일 | 승인 변경 경계 / 근거 |
|---|---|
| Assets/_Project/Resources/Config/UnitStatsConfig.asset | type2 hit3→1.7333333(52/30), cooldown3 및 다른 타입/값 보존 — U-ATK-TIMELINE |
| Assets/_Project/Scenes/Game.unity | GameUnitFactory type2 explicit clip GUID783d389748c4c7743a831612d7b1b5f5 **해당 필드 하나만** — NET-PRESENT-002 |
| Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs | ObservesSniper 및 기존 bounded native/raw mandatory 근거 확장 — NET-PRESENT-003/004 |
| Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs | 선택된 실제 source의 SNIPER-TIMELINE 진단, 기존 clip extraction/fallback 의미 보존 — NET-PRESENT-002 |
| Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs | 기존 production + formatter fixtures 확장, 새 validator 없음 — NET-PRESENT-002/003 |
| Tools/analyze_pistoleer_timing.py | 기존 parser/analyzer의 type2 fixtures·상관관계 확장 — NET-PRESENT-003 |

### 우선순위 위험·검증 경계

1. **최상위 — 실제 시간축 오판:** 기존 Stats hit3초만으로 실전 Impact3초였다고 증명할 수 없다. Factory가 clip에서 타임라인을 추출할 수 있으므로 선택 source/추출값/fallback을 구분한다. 설정 정합화를 기존 모든 실전 타격 교정으로 확대하지 않는다.
2. **높음 — native occurrence와 논리 회차 혼동:** 연속 Attack 재생의 native Start는 매 logical sequence의 새 Start가 아니다. 동일 native occurrence·회차/결과 키·cancellation을 분리하고 marker/VFX/result를 시간 근접만으로 합치지 않는다. overflow/mandatory raw coverage 미충족은 unknown으로 남긴다.
3. **높음 — Game scene 범위:** GameUnitFactory type2의 명시 clip 필드만 변경한다. 다른 scene serialized 값·setup/autosave·다른 타입 clip 수정은 금지한다. final manifest에서 one-field 경계를 검증한다.
4. **중간 — 진단/gate 성공 확대:** bounded formatter/parser/type2 fixture 통과는 native 실행·device pixel/정밀 타격 수용과 다르다. actual production gate·Main 새 native 및 빌드 시도는 별도 인계 후 기록한다.

HumanTower 실제 Assault 참조 적용과 다른 VFX 보류 유지. Inferno inspection/hash는 하지 않는다.

1. **Clip 보존:** 현재30fps marker52/30초(1:22), cycle3초/event를 유지한다. 실제 controller·Blue/Red production binding과 변경 범위를 programmer가 정확한 파일 목록으로 Main에 제출한다. 근거: U-ATK-TIMELINE·NET-PRESENT-002.
2. **설정 정합화:** Sniper type2 UnitStats hit3초→52/30초 및 해당 fallback hit를 맞춘다. cooldown3초·피해/범위/HP/단일 서버 writer는 유지한다. 근거: U-ATK-TIMELINE·NET-TIME-001.
3. **명시 clip 연결:** GameUnitFactory Human type2에 실제 production clip을 명시해 이 타입의 이름 fallback 의존을 제거한다. 다른 타입 lookup/clip은 변경하지 않는다. 근거: NET-PRESENT-002.
4. **기존 bounded PIST diagnostics:** Sniper type2 start→marker→VFX success/failure→authority result→Client expression 및 cancellation/death/despawn을 동일 회차/결과 키에서 구분한다. 로그 envelope·bounded 저장/overflow·Dev/Release 경계를 유지하며 별도 무제한 observer를 만들지 않는다. 근거: NET-PRESENT-003/004·NET-TIME-001/002/003, LogRules.
5. **기존 gate 확장:** 새 validator/TC 없이 기존 production timeline/Unit Action gate에서 실제 clip/controller/prefab·Stats/fallback·marker 및 진단 타입 경계를 검사한다. 프로필 promotion·전체 migration PASS는 승인하지 않는다. 근거: NET-PRESENT-002/003.
6. **문서·검증·빌드 경계:** StatsReference1:73→1:22(3:00) 정정, Matrix/상태/메모리 동기화. programmer 구현/static 인계 및 Main fresh native 결과를 단계별로 기록한다. 사용자가 승인한 Build And Run 시도는 Main이 실제 수행한 때만 기록하며 완료/설치/실기는 별도 미확인으로 남긴다.

기술 계획 상세 파일 목록·Main GO 전에 구현 완료로 쓰지 않는다. 이 문서 담당은 docs/meta만 작성하며 source/assets/UI/build/Git은 실행하지 않는다. 다른 VFX 보류·HumanTower 실제 Assault 참조 및 Inferno 확인 금지를 유지한다.

## 검증 결과

구현/static/native/build 전부 인계 대기. 과거 다른 Task PASS를 이번 Sniper 근거로 재사용하지 않는다.
