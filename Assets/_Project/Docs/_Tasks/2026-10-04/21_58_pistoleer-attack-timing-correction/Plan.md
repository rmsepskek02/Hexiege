# Plan — Pistoleer 공격 타이밍 정합성 교정

> **최신 저장 경기 판정 — 2026-10-08:** payload/피해 표현 확인, 이전 source 누락 상세 표본 없음. clock으로 completeness unknown·종료 후 callback 별도 OPEN, overall OPEN. 상세 단일 원본은 Research §10.9; 아래 prebuild/복구/계획 기록은 이력이다.

### 12.16 최신 단일 Pistoleer 경기 결과와 미확정 경계 (2026-10-08)

Research §10.9의 동일 shared Editor Host/Android Client 단일 경기만 반영한다.64기 Pistoleer만 관측됐으므로 세 clip 연결 gate를 Torrent/Bloom runtime 수용으로 확대하지 않는다. exact canonical payload307과 양쪽 필수 표현276 일치는 해당 전달 범위 확인이다. 메인의 기존 parser 직접 호출로 parsed12891·canonical paired307·paired325·unpaired0·fresh clock 오류2 및 양쪽 정상204scope의 Marker/Attempt/Result204/204 독립 재확인을 완료했다. 상세 표본 source incomplete0 및 attack paired325/unpaired0은 전325회 VFX 성공 증거가 아니며 source 상세64scope 한도/critical 전스코프 범위를 분리한다. 이전 복구 보호64파일과 현재64유닛/상세64scope는 다른 분모다.

agent 최종 관측은 로컬 VFX Started Host298/Client301, systems/active/manager 양수·maxsystems1이다. SourceStop/Cancel/Unavailable 없는 양쪽204 결과 scope의 Marker→Attempt→Result204/204 및 각 peer 내부 exact Admission/Marker/Attempt/VfxResult의 missing/extra/duplicate0을 확인했다. terminal source 미관측 Host9/Client6은 원인 미분류로 유지하고 결함으로 단정하지 않는다. 공간 표현 Host233/Client232은 mismatch0이며 상세 수치 원본은 Research §10.9에 둔다. 로컬 연결 관측은 전체 회차/픽셀/정확 타이밍 수용이 아니다.

global fresh phase clock regression2 records는 하나의 Client 보정 episode의2units 관측이다. frame26530 hold→26540 resume·actual 불변, 각각311.854ms/311.444ms pause 후 재개를 확인해 영구 멈춤으로 분류하지 않는다. 이는 UnitView의 raw clock 고수위 회복 대기 source와 일치하지만 rebased와 별개이며 completeness unknown·보정 원천/정확 영상 픽셀 timing OPEN을 유지한다. GameEnd 뒤 unit37 handoff failure는 source와 부합하는 종료 후 callback/reject chain 의심이고 실제 reject trace 미확보로 최종 원인 OPEN이다. ROOT 양쪽 INCOMPLETE/errors0은 단일 타입 coverage 부족이며 formal CrossAudit 미실행이다.

overall OPEN·exact timing/pixel/full migration 미수용. 추가 agent location/source facts는 이 기존 절에 증분 기록하며 현재 교정/새 gate/빌드를 실행하지 않는다. 이번 문서 manifest는 Research/Plan, Matrix/PROJECT_STATUS/ROADMAP/WORK_HISTORY의 최소 pointer, 공용 MEMORY7개이며 code/scene/assets/Unity/build/Git/새 Task·TC0이다.

> **최종 빌드 시도 — 2026-10-08:** fresh 두 메뉴 PASS 후 Build And Run1회·MissingProjectID Yes·JDK Checking 진행창 관찰까지 완료, 저장창 없음. 이후 모든 UI/Editor/build log 감시 중단. 완료/설치/runtime 미확인·빌드 성공 선언 없음. 아래 예정/시작 미확인은 이전 단계 이력이다.

> **최신 실행 결과 — 2026-10-08:** 세 연결 적용·Reload 확인·fresh 두 메뉴 PASS 완료. Build And Run은 시작 시도 예정/아직 시작 미확인이다. runtime·정확 시각·full migration OPEN 유지. 아래 계획과 이전 복구 대기는 단계별 이력이다.

> **최신 현재 — 2026-10-08 / 사용자 Game 씬 폐기 완료:** MistShrine 대응은 사용자 직접 폐기로 종료됐다. §12.14의 복구 대기는 이전 단계 이력이며 추가 복구를 수행하지 않는다. Pistoleer·TorrentSpirit·BloomFairy 세 연결 재적용 계획으로 복귀한다. 새 적용/fresh 메뉴/빌드 시작은 미확인이고 runtime·정확 시각·full migration OPEN은 유지한다.

### 12.15 세 유닛 production clip 연결만 재적용하는 승인 계획 (2026-10-08)

**계획4 실행 인계:** 메인 File→Build And Run 좌표 클릭1회 후 MissingProjectID 확인창 Yes, 즉시 JDK 검사 진행창을 관찰했다. 저장창 없이 시작 시도/preflight까지 확인했으며 Building Player 시작 또는 완료를 추정하지 않는다. 이후 UI/Editor/build logs 감시0, 완료·설치·runtime·정확 시각·full migration 미확인/OPEN이다. 실제 결과 원본은 Research §10.8; 이전 계획4의 예정 상태는 이력으로 보존한다. 이번 최종 문서 manifest는 기존 Task2개+PROJECT_STATUS/ROADMAP+공용 MEMORY5개, baseline 직접 편집/새 문서/새 Task·TC 없음.

**실행 결과 증분:** 메인 Game UnitFactory 세 연결 적용 및 diff 확인 완료, 구조2255blocks 유지·extra/deleted/duplicate/dangling0. 연결3줄 추가와 주변12줄 CRLF→LF 변경을 구별하며 “byte 변경이 세 줄뿐”이라고 확대하지 않는다. source8/보호64파일 hash 불변, Unity 실제 Reload 후 Game* 없음·새 structural 오류0이다. 상세 hash와 근거는 Research §10.8에 둔다.

**fresh gate 완료 / 다음 단계:**10-08 Unit Action00:35:14 전체/Production/C2/C3·Bloom exact timeline PASS 및 Root SelfValidate00:36:25 PASS를 실제 UI/fresh Editor.log로 확인했다. Console fixture warnings7·errors0. 계획1~3은 메인 인계로 완료됐으며4의 Build And Run 시작 시도는 아직 예정/시작 미확인이다. 이전 PASS 재사용이 아닌 fresh gate이고 실기·픽셀·정확 시각·full migration 수용은 OPEN이다. 후속 빌드 관측 사실을 받은 뒤 시작/차단 여부만 기록하며 완료 감시는 하지 않는다. 이번 문서 직접 수정은 Research/Plan2개이며 source/scene/UI/Git mutation은 문서 담당이 실행하지 않았다.

누락된 세 `attackTimelineClip` 연결만 복원하여 기존 교정 코드와 production timeline을 다시 연결한다. 사용자의 세 유닛 동시 진행 승인 및 기존 Task 갱신 standing approval에 따른 계획 기록이며 재승인 질문이나 신규 Task/TC/검증기를 만들지 않는다. 사용자 폐기는 에이전트의 semantic 복구 완료가 아니며 이전 사고/검증 이력은 삭제하지 않는다.

**근거와 불변 범위:** `GameSystemRules.md` → Units 규칙17~22/U-ATK-TIMELINE, Torrent 규칙27/29, Bloom 규칙32~37 및 synchronization의 NET-AUTH/ROOT/TIME/PRESENT/CANCEL·NET-DELIVERY-TRAVELING·SeparateHeal 원문을 따른다. 문서 담당은 관련 원문을 확인했으며 메인도 실행 전 직접 확인해야 한다. 연속 native 재생, 서버 피해/HP 권위, Torrent 예약/contact와 Bloom 독립 heal/cooldown 계약을 유지한다. Stats/밸런스·clip/events·VFX·코드/공유 정책·settings 변경은 없다. source8/diagnostics/contract3는 유지하고 이전 PASS는 fresh 실행을 대체하지 않는다.

1. **사전 확인:** 메인은 Game.unity snapshot과 UnitFactory `1708566852`의 Human0/Spirit18/BloomFairy enum27·Transcendence[7] 항목을 확인한다. 정확 GUID는 Research §10.8의 메인 meta 확인 표를 따른다. 로드된 씬이 dirty이면 저장 금지; disk reload 및 로드 내용 일치를 확인하고 진행한다.

   **[🔴 2026-10-08 correction — original kept: 최초 계획은 “Human0/Spirit18/Transcendence25”라고 기록했다.]** 코드 담당 preflight 및 실제 UnitType enum 대조 결과 type25는 RabbitTrickster이며 BloomFairy는27이다. 세 연결 중 Bloom 대상만 enum27 / Transcendence[7]로 정정한다. 디스크 구조 확인 인계와 아직 patch 전인 경계는 Research §10.8을 따른다; 코드/enum 수정은 없다.
2. **예정 변경 manifest:** `Assets/_Project/Scenes/Game.unity` 한 파일의 위 세 `attackTimelineClip` 연결만 정확히 3-line patch한다. 다른 empty-field 정규화/전체 overwrite/setup 실행은 하지 않는다. pre/post snapshot·diff로 세 연결 외 변경 없음과 기존 설정 보존을 확인한다.
3. **fresh 검증:** 기존 Unit Action 및 Root Pose 메뉴를 새로 실행하고 실제 결과를 확인한다. 실패 시 그 gate/실제 범위만 기록하며 이전 PASS나 연결 성공으로 통과를 대신하지 않는다. 새 검증기·코드 교정은 이 계획에 포함되지 않는다.
4. **빌드 경계:** 두 메뉴 fresh PASS 뒤 Build And Run 시작만 시도한다. 실제 시작 전 저장창/차단이 나오면 시작 미확인으로 기록한다. 이후 완료·설치·실행 감시는 하지 않으며 runtime 수용/full migration/정확 시각 PASS를 선언하지 않는다.
5. **문서 인계:** 지금 직접 수정 manifest는 기존 Research.md와 Plan.md 두 파일뿐이다. 메인의 실제 scene diff·reload·menu·빌드 시도 facts를 받은 뒤 결과를 증분 기록하고 필요한 상태 문서 후속 갱신을 판단한다. 문서 담당 source/scene/UI/Git mutation0; 이전 복구 탐색은 재개하지 않는다.

> **최신 정정 — 2026-10-08 / recovery OPEN·build blocked:** 무관 MistShrine setup 실행·Game 자동저장 사고를 기록한다. 이전 scene0 전범위 주장은 원문 보존 정정이며 사용자 Cancel 확인이 과거 disk 저장 복구를 뜻하지 않는다. 기존 source8·검증 PASS 보존, 원본 Game baseline/serialized diff 미확보·복구/새 빌드 없음. 상세 §12.14 / Research §10.7; 아래 계획·저장창 대기 표기는 이전 이력이다.

### 12.14 비범위 씬 setup 사고 기록·정정 및 복구 미완 경계 (2026-10-08)

**최신 후속 — read-only 승인·후보 확보:** 사용자 Git read-only 승인 후 메인이 HEAD old Mist panel 후보를 확인했다(이력·diff 수치는 Research §10.7). 현재 semantic/block-level diff 분리 대기이며 복구 미완이다.10/04 이후 정당한 Pistoleer 설정을 보존해야 하므로 Game 전체 overwrite는 불가하다. Sagan 분류 인계 전 복구 성공·안전한 범위 확정을 주장하지 않는다. 이전 baseline 미확보·Git 승인 대기는 당시 이력이며 문서 담당 Git/씬 입력0.

**추가 복구 후보·source 경계:** sibling Game은2026-07-13의 오래된 파일이며 승인된 baseline이 아니다. 현재 Game hash·panel 식별값과 현재 구조는 Research §10.7에만 상세 기록한다. Sagan은 panel 파괴/복제에 Undo/full reference remap 없음·bootstrapper2slots만 배선됨을 확인했다. Range/Network 신규 여부는 미확정이며 삭제된 custom layout/icons/events/references를 source만으로 재구성하는 것은 안전하지 않다. baseline 없는 복구를 실행하지 않는다. native 조회의 Game*·Cancel popup 사라짐과 foreground 가림 capture를 구별한다. Unity 입력0, Git read-only 질문 대기·이 문서 작업 Git0, 복구 완료 미확보.

무관 설정 메뉴가 Game 씬을 변경·자동저장한 사실을 기존 Task에 남겨, 검증 PASS를 씬 안전성이나 빌드 진행 가능성으로 오인하지 않게 한다. 사용자의 기존 Task 갱신 승인 범위이며 복구/코드/씬 편집 계획을 새로 승인하거나 실행하는 절이 아니다.

Research §10.7의 조작95/observed mapping41108, Console 및 disk 시각은 메인 인계 근거다. 실제 MistShrine setup 실행과 자동저장은 확인되었으나 정확 dispatch 원인은 미확정이다. setup source의 panel 제거/복제·Range/Net/Bootstrapper 배선·조건부 SaveScene과 실제 serialized diff를 구별한다. material mtime 불변은 유지하고 material 수정 목록에 넣지 않는다.

**[🔴 2026-10-08 correction — original kept: §12.13의 scene0 및 작업 전범위 asset/settings 변경0을 현재 truth로 재사용하지 않는다. 저장창 미선택은 당시 이력이고 현재 사용자가 Cancel을 확인했다. Cancel은 이미 저장된 Game 복원 증거가 아니다. source8 manifest·4compile·CLI·두 메뉴 PASS는 별도 보존하되 Game baseline 정합/복구/빌드 시작 증거로 확대하지 않는다.]**

현재 recovery OPEN/build blocked다.14:53 Temp backup은 LOGIN이어서 Game 복원 원본으로 사용할 수 없고 원본 Game baseline·정확 전후 scene diff는 아직 없다. 기존 Battle/Pistoleer 코드는 보존한다. 문서 담당은 코드/씬/asset/Unity 입력/Git 및 복구/빌드를 실행하지 않는다. 후속 복구 결과·근거를 받으면 이 기존 절에 증분 기록하며 성공/완료를 선기재하지 않는다.

이번 직접 문서 manifest: Research/Plan, .claude/mistakes.md, 기존 unit-action-correction 사건 후속, 공용 MEMORY, UnitCombatAssetMatrix/PROJECT_STATUS/ROADMAP/WORK_HISTORY9개. 기존 source8과 Temp 검사 산출·과거 도구 baseline은 별도 이력이며 현재 사고 Game 저장은 비범위 사고로 따로 기록한다. 기존 수치·문장 삭제0, 새 Task/TC/기능 변경0. 문서 checker는 이번 수정 후 새 실행하며 이전0건을 재사용하지 않는다.


> **최신 두 경기 분석 — 2026-10-07 후속:** canonical 피해 결과1165건 HostResult=ClientReceive 완전 일치(conflict/unpaired0)는 결과 피해 정합 수용 범위다. 정상 Host HitApplied19 scope에서 raw Marker/VfxAttempt/VfxResult가 없고 취소·Unavailable·SourceStop 없이 다음 revision replace로 종료됐다. 발사표현 누락 의심·원인분석 OPEN/전체 수용 미완이며 schema341·fresh clock20으로 parser 전체 records unknown이다. schema 오류만으로 전체 정상이나 gameplay 실패를 단정하지 않는다. 정확 시각·픽셀·full migration OPEN. 상세 Research §10.5 / Plan §12.12; 아래 prebuild 및 이전 경기 기록은 이력으로 보존한다.

> **최신 상태 — 2026-10-07:** 승인된 기존11파일 구현·보완 및 빌드 전 검증 완료(4경로 errors0, CLI23 독립 PASS, 실제 Unit Action14:06:49·Root14:07:44 PASS). Build And Run1회 Building Player / Backup & unload 관찰 후 감시 중단; 완료/설치/실기는 사용자 확인 사항이다. 정확 Pistoleer VFX·타이밍/육안/full migration은 OPEN·MigrationRequired 유지. 아래 이전 상태·coverage 미완·대기 기록은 당시 이력이다.

권총병이 사용하는 기존 Attack 클립을 정확히 연결하고, 그 클립의 0:24 표식과 설정값이 다시 어긋나지 않도록 기존 검증을 보강합니다. 발사 연출이나 피해 권한을 새로 만드는 계획은 아닙니다.

**작성 시각:** 2026-10-04 21:58 KST  
**현재 상태(2026-10-07):** 기존11파일 구현·최종 gate PASS·Build And Run 시작 시도까지 완료. 빌드 완료/설치 미확인, 사용자 exact Pistoleer VFX/타이밍·육안/full migration OPEN(MigrationRequired). 상세 Research §10.4 / Plan §12.11.

**공통 표현 코드 영향 경계:** 위 “다른 유닛/연출을 그대로 유지”는 에셋·스탯·gameplay policy·피해 권위 보존을 뜻한다. 공통 native phase 적격 타입에는 shared expression 코드 영향이 있으며 기존 gate에서 Pistoleer/LionKnight 대표 생산 경로를 검사했다. 규칙 변경이나25종 전체 실기 PASS를 뜻하지 않는다.

**10-05 당시 분석 상태(원문 이력):** 10-05 Android Host/Editor Client 결과 전달 정상 / sampled Client 2회 committed scope와 연속 marker 연결 결함 강한 근거·정확 원인 일부 미관측 / 파서 production 의미 해석 오류 확인 / 정확 타이밍·육안·full migration OPEN, MigrationRequired 유지. 이전 구현·fixture·메뉴·빌드 관찰은 당시 이력으로 보존한다.

## 1. 적용 규칙과 보존 계약

현행 관련 규칙을 확인했으며 정책 변경은 하지 않습니다.

| 원본 규칙 | 이번 적용 |
|---|---|
| [Units](../../../GameSystemRules/GameSystemRules_Units.md) 규칙 17 | 동일한 생산 Attack 클립에서 marker를 읽고 설정·명시 참조를 정합화 |
| 같은 문서 규칙 18 및 U-ATK-TIMELINE | 서버 타이머의 피해 권위와 기존 회차 시간축 유지; 로컬 이벤트로 피해 발생 금지 |
| 같은 문서 규칙 19·20·22 | HP/피격 결과, Tracer, 상태 동기화·기존 Idle 보존 |
| [동기화 계약](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md) NET-AUTH-001/002 | 서버 판정·즉시 HP와 표현 책임 분리 유지 |
| 같은 문서 NET-PRESENT-001/002 | Animation Event는 로컬 표현 표식; production 연결 검증은 실기 수용이나 분류 승격의 대체 증거가 아님 |

VFX offset/pool, 프리팹 Visual Root, 기존 attack/Idle 상태, 다른 타입과 스탯은 그대로 둡니다. 현재 분류를 변경하거나 full migration을 선언하지 않습니다.

## 2. 승인된 최소 변경 — 기존 3파일

**착수 당시 계획 이력(2026-10-04):** 아래 config2초→0.8초·Game 참조 신설은 최초3파일 교정 계획이며 이미 구현된 이력이다.10-07 후속 교정의 actual11파일·최종 검증은 §12.11을 따른다.

1. `Assets/_Project/Resources/Config/UnitStatsConfig.asset`: type 0의 `hitFrameTimes`만 `[2] → [0.8]`. attackCooldown 등 다른 값 유지.
2. `Assets/_Project/Scenes/Game.unity`: **UnitFactory** `_humanPrefabs` type 0에 `attackTimelineClip: {fileID: 7400000, guid: 3dc19d88c3a013c46af811597796c70a, type: 2}` 명시. BuildingFactory 목록과 Blue/Red 참조는 변경하지 않음.
3. `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`: 기존 BearGuard production 검사 패턴을 따라 Pistoleer 검사를 기존 메뉴에 통합. 정확한 clip/controller/양 팀 prefab/`Base Layer/Attack` motion, 30fps·2초·단일 0.8초 marker, config 및 실제 Factory 명시 연결을 확인하고 불일치 시 실패.

정확한 자산 GUID는 [Research §2](Research.md#2-명시-연결-식별자)를 따릅니다. 새 validator·emitter·TC·QA task는 만들지 않습니다. 클립 시간·이벤트 및 VFX 에셋을 수정하지 않습니다.

## 3. 실행·검증 순서와 판정

- 메인 GO 후 programmer가 위 3파일만 구현하고 메인이 실제 변경을 검토합니다.
- 기존 static 검사와 실제 Unity Unit Action / Root Pose self-validation 결과는 메인이 실행·확인한 내용만 인계받습니다. 이전 메뉴 PASS를 이번 결과로 재사용하지 않습니다.
- 사용자가 승인한 빌드는 메인이 **시도**만 수행합니다. 시작·preflight 관찰은 완료/PASS가 아니며 완료 감시는 하지 않습니다.
- production 정합성 및 메뉴 PASS는 실제 피해/발사 순간의 시각적 일치, 양 역할 실기, full migration의 완료를 증명하지 않습니다. 실기·육안 수용 전 focused 상태는 OPEN입니다.

## 4. 현재 문서 범위

현재 생성은 이 Task의 Research/Plan 두 문서뿐입니다. StatsReference·UnitCombatAssetMatrix·PROJECT_STATUS·ROADMAP·WORK_HISTORY는 메인 최종 GO와 실제 결과 인계 후 필요한 최소 상태만 반영합니다. 과거 이력은 보존합니다.

문서 담당은 source/assets/UI/build/Git/checker를 실행하지 않으며 메모리·baseline도 변경하지 않습니다. 구현 및 실제 검증 결과는 추후 별도 후속 절에 추가합니다.

## 5. 메인 실행 인계 — 2026-10-04

§2의 기존 3파일 최소 변경은 구현·메인 review 완료다. 새 static 4경로 오류0(Runtime/Editor/Development/Release 경고20/0/20/24), 실제 Refresh 뒤 22:04:36 기존 Unit Action 메뉴 PASS를 인계받았다. 새 production guard가 PASS 출력 전에 실행됐다. 상세는 [Research](Research.md)의 §4를 따른다.

Root self-validation은 실제 UI·새 Editor.log PASS, 승인된 빌드 시도는 22:09 Importing assets preflight까지만 확인했다. 실기·육안 수용 및 full migration은 OPEN이며 초기 대기 문구는 당시 이력이다. StatsReference를 `0.8초(주기 2초)`·30fps `0:24`로 정정하고 관련 현황 문서에 최소 상태를 동기화했다. 정책·분류·메모리/baseline 변경 및 새 TC/QA/validator 생성은 없다.

**별도 파일 관찰:** 메인은 Blue Pistoleer prefab SHA 변경(B2B…→5319…, 22:07:10, 40087 bytes)을 확인했다. 승인된 직접 3파일 구현과 분리하며 원인 미확인·복원 없음이다. 양 팀 prefab bytes 불변을 주장하지 않는다. clips/controller/Red/runtime hash 불변 인계와 상세 증거 경계는 Research §4의 별도 관찰을 따른다. Root는 실제 UI 실행·새 로그 PASS이며 22:09 빌드 시도/preflight만 확인했다.

## 6. 최종 인계 — 2026-10-04

메인 실제 Root SelfValidate 및 새 Editor.log PASS로 두 기존 메뉴 확인이 완료됐다. static 오류/경고 Runtime 0/20·Editor 0/0·Development 0/20·Release 0/24, Unit Action 22:04:36 PASS를 유지한다.

22:09 File Build And Run 클릭 뒤 Hold on/Importing assets preflight까지만 확인했다. 승인된 빌드 **시도**는 수행됐으나 성공/완료/설치/실행은 미확인이다. UI와 완료 감시는 중단했다. 실기·육안 수용·full migration은 OPEN이다.

Task 2문서와 StatsReference·UnitCombatAssetMatrix·PROJECT_STATUS·ROADMAP·WORK_HISTORY 총 7문서를 최소 동기화했다. Root PASS·빌드 시도만 문구는 당시 이력으로 남기고 최신 상태는 이 절을 따른다. source/assets 추가 수정·메모리/baseline·checker 실행은 없다.

## 7. 최종 파일 목록

승인된 직접 구현 3파일:

- [UnitStatsConfig.asset](../../../../Resources/Config/UnitStatsConfig.asset)
- [Game.unity](../../../../Scenes/Game.unity)
- [RunUnitActionSelfValidation.cs](../../../../Scripts/Editor/Combat/RunUnitActionSelfValidation.cs)

동기화한 참조 문서 5파일:

- [StatsReference.md](../../../StatsReference.md)
- [UnitCombatAssetMatrix.md](../../../Assets/UnitCombatAssetMatrix.md)
- [PROJECT_STATUS.md](../../../PROJECT_STATUS.md)
- [ROADMAP.md](../../../ROADMAP.md)
- [WORK_HISTORY.md](../../../WORK_HISTORY.md)

신규 Task 문서 2파일:

- [Research.md](Research.md)
- [Plan.md](Plan.md)

Unity 자동 생성 meta 4파일(문서 담당의 직접 생성/편집 아님):

- [2026-10-04.meta](../../2026-10-04.meta)
- [21_58_pistoleer-attack-timing-correction.meta](../21_58_pistoleer-attack-timing-correction.meta)
- [Research.md.meta](Research.md.meta)
- [Plan.md.meta](Plan.md.meta)

승인된 직접 구현과 별도 관찰 파일:

- [Unit_Pistoleer_Blue.prefab](../../../../Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab): SHA B2B…→5319…, mtime22:07:10, 40087 bytes. 원인 미확인·복원 없음이며 Research §4의 증거 경계를 유지한다.

메뉴·빌드 시도 경계는 두 기존 메뉴 PASS, **22:09 KST Build And Run 시도·Importing assets preflight만 확인**이다. 빌드 완료/성공 미확인·완료 감시 중단 기록을 유지하며 최신 경기 판정은 §8을 따른다.
## 8. 최신 경기 판정과 남은 수용 — 2026-10-04 23:06 KST

[Research §6](Research.md#6-최신-paired-경기-분석--2026-10-04-2306-kst)의 메인 최종 분석을 반영했다. Editor22:31–22:34 / device22_56의 같은 session(192c9a08…34711d)만 사용하며 이전18시 기록은 제외한다. Pistoleer235개(Blue119/Red116)의 생산·Client 생성, Ready/commit/schedule 각1302, Host Results1225=Client accepted1225(failure/reject0), 양쪽 필수 표현1020/1020(failure0)을 확인했다. Host 이동220148 frames의 gate/adapter/writer/stationaryWalk/fatal0, Client 복제1655 samples의 invalid/conflict/entrygap/order0, 양쪽 최신 ERROR/FATAL0이다.

로컬 ROOT는 errors0이나 representativeTypes<2로 INCOMPLETE다. cap64는 전체235개 coverage나 mismatch 증거가 아니며 endpoint는 Host52/Client48이다. Client 초기화235지연은235완료·missing0; 별도 경고282건을 fatal/피해 손실로 해석하지 않는다. 첫 회차·raw anchors와 Blue 이벤트 확인/의도적 설정 변경 없음 진술은 Research에 기록하고 기존 해시 변경 관찰을 보존했다.

focused 로그상 긍정만 수용한다. marker/실제 Impact 상세가 없어 실제0.8초 offset·2초 주기 계산과 전체 exact-key join 검증은 미완이며 최신 육안 수용도 OPEN이다. MigrationRequired·full migration 미완을 유지한다. 이번에는 기존7md만 수정했으며 코드/에셋/새 Task/TC/UI/build/Git·메모리/baseline 수정은 없다. 문서 검사기는 이 인계 후 메인이 재실행한다.

## 9. 타이밍 진단 보완 계획 — 2026-10-05 00:01 KST

**상태: 기존 Task 보완 요청 승인·Sagan 제안 반영, 문서 메인 review/코드 GO 대기.** 별도 Task는 만들지 않는다. 아래는 구현 계약이며 아직 구현·parse gate·새 static/menu/build 결과가 아니다.

### 9.1 변경 범위 — 기존 6파일 + 읽기 전용 Tool 파서 1개

| 대상 | 추가 관측 책임 |
|---|---|
| NetworkCombatController | 실제 schedule·absolute due·overshoot·cancel 및 canonical result 발행 |
| UnitCombatUseCase | 실제 TakeDamage 전/후를 같은 immutable full key로 관측; 적정 Application GameLog/interface 경유, Infrastructure 역참조 금지 |
| UnitView | scoped command·연속 재생 anchor·rendered Animator first sample·raw marker admission·VFX attempt/return·tracer skip·retire |
| UnitAttackShadowObserver | bounded Pistoleer scoped exact flow/detail 및 terminal 완전성 |
| NetworkUnit | canonical result receive |
| 기존 RunUnitActionSelfValidation | 실제 production format fixture와 hook guard; 새 validator/TC 아님 |

저장 로그만 분석하는 **읽기 전용 Tool 파서 1개**는 필요한 최소 신규 파일로 허용한다. 이름·정확한 경로는 programmer가 결정해 최종 인계에 적는다. 새 gameplay 모듈·QA·Task가 아니며 이 문서 담당은 소스나 Tool을 생성하지 않는다. 기존 config/clip/prefab/VFX·피해/HP/취소/표현 정책·분류는 바꾸지 않는다.

### 9.2 필드·시계·완전성 계약

동일 immutable full key와 역할/객체 수명/회차·revision·HitIndex/결과 식별을 각 사건 경계에서 보존한다. clock domain, absolute due, continuous anchor와 calibration을 기록하여 서버 예약/실제 적용과 로컬 marker·표현을 구분한다. 연속 Attack을 commit+0.8로 추정하지 않고, 보정 없는 peer clock 빼기나 임의0.2초 PASS 허용치를 도입하지 않는다.

진단은 EDITOR/Development 전용으로 기존 GameLog 형식·심각도/존속 분류를 따른다. bounded 사건 단계와 first sample을 기록하며 매 프레임 로그를 만들지 않는다. full-line UTF-8 cap과 drop/overflow를 명시하고, complete/incomplete/unknown을 구분한다. 기록이 빠졌거나 상한에 걸린 것을 정상0 또는 전체 coverage로 바꾸지 않는다. 구체 상한·schema는 구현자가 명시해 메인 검토받으며 이 문서에서 수치를 지어내지 않는다.

### 9.3 재빌드 전 필수 gate

1. 실제 production hook과 출력 필드가 위 관측 책임에 연결되는지 기존 self-validation hook guard로 확인한다.
2. **production formatter가 만든 원시 출력 fixture를 Temp에 생성하고 CLI 파서로 실제 분석한다.** Host/Client 양 역할 형식과 필수 key/clock/due/anchor/marker/Impact/표현 단계가 누락 없이 읽히는지, 손상·drop·unknown completeness가 정상으로 숨겨지지 않는지 확인한다. 파서에 맞춰 별도 가짜 문자열만 만든 테스트로 대체하지 않는다.
3. formatter/CLI parse gate와 hook 점검을 완료한 뒤 새 static 및 기존 Unit Action/Root self-validation을 메인이 확인한다. 누락·충돌·모호한 연계가 남으면 빌드 전에 보고·보완한다. fixture 통과와 실제 네트워크 production/runtime coverage는 별도로 기록한다.
4. 문서 메인 review와 코드 GO 뒤 구현한다. 검토·사전 gate 후 승인된 재빌드 **시도**만 수행하며 완료 감시는 하지 않는다. 실제 compile/menu/build 관측은 메인이 제공한 새 결과만 추가한다.

현재 gate 실행 결과는 없다. 정확 타격·주기/육안 및 full migration은 OPEN이며 이전 양성 집계는 보존한다. 실패 기록과 근거는 [Research §7](Research.md#7-타이밍-진단-보완-초안--2026-10-04-2359-kst)을 따른다. 문서 담당 변경은 Research/Plan·기존 전용 실패 기록뿐이며 UI/assets/Git/checker·메모리/baseline을 건드리지 않는다.

## 10. 승인 후 구현·검증 최종 인계 — 2026-10-05

§9는 승인 전 계획 이력이다. 메인이 검토하고 코드 GO를 전달한 뒤 구현되었으며, 아래는 새 실제 인계 결과다.

메인 review 및 코드 GO 후 진단 보완이 구현되었다. Application 계층 helper를 사용해 Infrastructure 역참조를 피하고, 기존 실제 writer 전/후의 local-only 기록·schedule/due/overshoot/cancel·canonical result 발행/수신·scoped command/marker admission·VFX attempt/return·tracer skip/retire를 연결했다. same-host dispatch anchor는 추정값과 bracket/residual을 함께 다루며 실제 writer의 서버 시각으로 둔갑시키지 않는다. firstRenderedStart·연속 재생 interval·Impact/receipt/presentation delay를 구분한다. commit+0.8 추정, 보정 없는 peer clock 차감, 임의0.2초 또는 다른 숫자의 timing PASS 기준을 도입하지 않았다.

진단은 EDITOR/Development bounded 사건 단계·first sample 관측이다. 팀별32, 총64 **attack scope** 표본이며 64유닛/전체 경기 coverage가 아니다. sampleExcluded·unclassifiedScoped·실제 omitted와 format/drop/completeness unknown을 구분한다. full-line UTF-8 cap을 넘는 기록은 명시적 formatIncomplete로 처리하며 정상0이나 완전한 증거로 해석하지 않는다. 메인 요청에 따라 schema·first anchor·quota/mixed 처리를 보완한 최종본이다.

새 static Runtime/Editor/Development/Release errors **0/0/0/0**, warnings **20/0/20/24**. 메인은 메뉴 실행 전에 production formatter 출력에 대해 CLI `--fixtures Temp/pistoleer-timing-fixtures`를 독립 실행해 **16개 case·Host/Client 양 역할, exit0**을 확인했다(normal/late/cancel/missing/duplicates/truncated/regression/arrival/drop/schema/terminal/format/quota/mixed/miss/sourceonly). 메뉴는 같은16개를 재생성했다. 이는 실제 네트워크 production hook 실행 coverage나 실제0.8초 offset·2초 주기 PASS가 아니다.

2026-10-05 **00:45:26 KST** 새 컴파일 소스의 실제 Unit Action 메뉴 UI PASS 및 새 Editor 로그의 overall/production/C2/C3/AUTH-PRESENT PASS를 메인이 확인했다. **00:48:23 KST** 실제 Root 메뉴의 console/statusbar `UAS-ROOT-CROSS-AUDIT self-validation PASS`를 확인했다. Root 추가 Editor raw-log 읽기는 usage/auto-review 실패로 실행되지 않았으므로 raw-log 확인을 주장하지 않는다.

사용자 continue 뒤 메인은 File → Build And Run을 **한 번** 클릭하고 BuildingPlayer/BuildPreProcess를 관찰했다. 사용자 지시에 따라 모든 UI·빌드 로그 감시를 중단했다. 완료·설치·새 실기는 미확인으로, 빌드 성공을 선언하지 않는다. 메인이 독립 비교한 Attack.anim·controller·Blue/Red prefab·config·Game.scene 총6파일의 해시는 이번 진단 보완 시작 대비 유지되었다. VFX는 구현 manifest상 미수정이며 VFX 에셋 해시 baseline 비교는 수행하지 않았다. 권위 payload·gameplay 정책은 변경하지 않았다. 앞선 Blue 해시 관찰과 사용자 이벤트 확인/의도적 변경 없음 진술은 그대로 별도 이력으로 보존한다. 이전 경기의 focused 로그상 긍정 판정도 보존하며 정확 타이밍·육안·full migration/분류 승격은 계속 OPEN이다.

### 10.1 실제 진단 보완 파일 manifest

기존 수정6파일:

- `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs`
- `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs`
- `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs`

신규3파일:

- `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs`
- `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs.meta`
- `Tools/analyze_pistoleer_timing.py` — 저장 로그 읽기 전용 분석 도구

앞선 설정/생산 연결 교정과 Unity 생성 meta 목록은 §7의 별도 단계 manifest를 보존한다. 이번 진단 보완에서는 그 설정·scene·clip·prefab·VFX를 추가 수정하지 않았다.

### 10.2 검증 산출물과 문서 변경 구분

Temp는 제품 소스 manifest와 별개다. `Temp/pistoleer-timing-fixtures/*-Host.txt`, `*-Client.txt` 합계32 txt와 `*.json` report1개가16 case 양 역할 fixture 산출물이다. static 컴파일은 재사용 `Temp/bloom-cooldown-compile/` 산출물이며 개별 파일명을 인계 없이 지어내지 않는다. 메뉴 재생성 fixture는 실기 로그가 아니다.

이번 문서 직접 수정8파일: 이 Task의 `Research.md`·`Plan.md`, `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`, `Assets/_Project/Docs/PROJECT_STATUS.md`, `Assets/_Project/Docs/ROADMAP.md`, `Assets/_Project/Docs/WORK_HISTORY.md`, `.claude/mistakes/unit-action-correction.md`, 기존 인덱스에 연결된 `.claude/agent-memory/game-programmer/logging.md`. 메모리에는 재사용 가능한 관측 설계 교훈만 추가하며 세션 수치/WIP를 저장하지 않는다. 새 Task/TC/QA·source/UI/Git/checker·baseline 변경 없이 문서 인계에서 종료한다.

## 11. 역할교대 실기 후속 상태 — 2026-10-05

메인 검토 Android Host/Editor Client03:46–03:49 session103e57ed…6789eb의 상세 raw 근거와 시간 분포는 Research §9에 기록했다. results878/accepted878·ready/released878·visual728/728 실패0, schedule/commit924는 전달 증거다. 양 역할64 attack scope(팀별32) join, canonical payload63 exact match(58 HitApplied/5 Miss)+death cancel1을 전체878 payload 또는64유닛 coverage로 확대하지 않는다.

LIVE Client instance1 seq6·instance9 seq1은 결과 수신/Ready 뒤 해당 scope RawMarker/VfxAttempt 없이 retire됐다. committed scope arm이 loop marker 뒤이고 retire가 다음 marker 전인 연속 phase/lease 연결 결함과 강하게 부합한다. instance9 다음 normalized marker는1.4다. prior/provisional marker 처리와 정확 close 호출 원인은 미관측이므로 화면 전체 VFX 부재/완전한 인과 증명을 선언하지 않는다. 실제 수정은 이번 턴 수행하지 않는다.

parser schema15(regexp가 아닌 합법 Miss hp=-1 검증 문제)·Register revision축 오결합·estimate anchor rebase regression11은 진단 결함이지 gameplay/network-delay 실패 숫자가 아니다. 기존16 fixture PASS는 보존하되 production 의미 coverage 부족을 실패 기록에 후속 추가한다. 후속 검토는 lease와 Animator phase의 연결, parser identity/semantic/clock 분류·production fixture 보완에 한정하며 아직 수정 승인/실행 결과가 아니다. 정확 타이밍·육안·MigrationRequired/full migration은 OPEN. 로컬 ROOT INCOMPLETE(errors0, 단일type 표본 부족)를 전체 roster 요구나 CrossAudit 완료로 바꾸지 않는다.

이번 직접 변경 manifest는 Research/Plan·Matrix·PROJECT_STATUS·ROADMAP·WORK_HISTORY·기존 unit-action-correction incident·기존 programmer logging topic 총8문서다. 이전 source manifest/Temp fixture/static/menu/build 인계는 §10의 별도 이력이다. 소스/에셋/Git/UI/빌드·새 Task/TC/QA·checker 실행 없이 종료하며 문서 checker는 메인이 실행한다.

## 12. 승인된 후속 교정 방향 — 구체 제안 검토 대기 (2026-10-05)

사용자 승인 범위는 **기존 Task 갱신 → phase 정합성·scope 연결 교정 구현 → 검증 → 빌드 시작 시도**다. §11의 수정 미승인/분석만 수행했다는 문구는 당시 이력으로 보존한다. 현재 문서 담당 범위는 Research/Plan 두 파일뿐이며 다른 문서·source/assets·UI/Git·checker/baseline·새 Task/TC/QA는 변경하지 않는다. 메인이 read-only programmer 제안과 규칙 감사를 검토한 뒤 전달하면 이 절에 concrete file manifest·선택 설계·규칙 적용·실제 검증 근거를 추가한다. **아직 제안 확정·구현 완료를 주장하지 않는다.**

### 12.1 제안 수용에 필요한 보존 계약

- Units U-ATK-TIMELINE과 Sync NET-PRESENT-001/002에 따라 provisional Attack의 연속 phase를 보존한다. commit마다 Animator를 restart/rewind하지 않으며 precommit 과거 marker·빈 이벤트는 새 scope에 재사용하지 않는다.
- 서버가 선택한 commit 뒤 유효 marker occurrence, per-hit 절대 due와 불변 scope가 함께 정합해야 한다. Client lease만 다음 loop로 밀거나 서버만 고정 offset을 더하는 분리 교정은 수용하지 않는다. 늦은 승인 후 occurrence 처리와 승인 전 이미 지난 marker 처리를 명확히 나눈다.
- 서버 단일 gameplay writer·정규 결과 멱등·객체 수명/sequence 보호를 유지한다. 취소·사망·게임 종료와 미확정 future hit 폐기, 이미 확정된 결과 보존, LegacyFallback 및 Fox/SeparateHeal/Torrent 등 타입별 예외를 무단 변경하지 않는다. 규칙 충돌·정책 변경 필요 시 사용자에게 먼저 보고한다.

### 12.2 생산 관측과 분석 gate 요구 — 구현 결과 아님

생산 경계 로그에서 ending call site/reason, per-hit due, 객체·회차/재생 epoch와 선택 occurrence, 경계 시점 sampled phase, lease admission/거부 이유, output dedupe, overflow/format-incomplete/omission을 판별할 수 있어야 한다. 모든 프레임을 출력하는 대신 실제 전이·예약·소비·종료 경계에서 기록하고, GameLog의 심각도/존속 분류·파일 경유·개발 로그 strip·UTF-8 길이 경계를 유지한다. fixture-only PASS를 실제 production callback 실행이나 pixel VFX 수용으로 대신하지 않는다.

parser/fixture gate는 immutable session+instance+sequence+hit 연결과 canonical result payload 비교를 분리하고, Host action revision/Client presentation revision을 강제로 같은 축에 놓지 않는다. 합법 Miss hp=-1을 허용하는 의미 검증, raw local monotonic clock과 rebased NGO/presentation anchor의 구분, scope quota/실제 omission·overflow 분리 및 양 역할 생산 의미 사례를 포함해야 한다. 기존16 formatter fixture PASS와 부족했던 production coverage는 모두 이력으로 보존한다.

### 12.3 실행 인계·검증·빌드 경계

구체 제안 검토 → 실제 수정 manifest 확인 → 최종 수정 뒤 정적/production fixture/parser 검사 → 메인의 기존 Unity 자동 gate 확인 → 승인된 Build And Run 시작 시도 순서로 실제 근거를 추가한다. 실행되지 않은 검사는 PASS로 표기하지 않는다. 빌드 시작 관찰은 완료·설치·게임 실행의 증거가 아니며 완료 확인은 사용자 소관이다. 새 실기 exact scope/phase/marker/result 상관과 육안 수용 전까지 정확 타이밍·MigrationRequired/full migration은 OPEN이다. 현재는 규칙 읽기와 승인 경계 문서 준비만 완료했고, 구체 제안 전달 및 이후 실행 결과는 대기다.

### 12.4 초기 제안 수신 — 최종 설계·code GO 대기

초기 제안은 메인에 수신됐으나 full rule audit와 **활성 연속 phase의 production 교정 전략이 미완**이다. shared epoch/per-hit due 원자 전달 및 최초 provisional phase 정렬은 검토 후보일 뿐 확정 구현 계획이 아니다. 기존9파일 범위 후보이며 새 validator·에셋·hit writer 신설은 계획하지 않지만, 최종 reviewed manifest는 설계 확정 뒤 기록한다. 코드 착수 승인은 아직 없다.

검증은 (1) 최초 provisional 재생의 phase 정렬, (2) 이미 활성인 같은 epoch에서 연속 재생·승인 occurrence·scope의 연결로 분리한다. 최초 정렬 또는 payload 전달 성공만으로 두 번째 문제의 해결을 주장하지 않는다. 같은 epoch commit에서 rewind하지 않고 precommit 과거 marker를 재방출하지 않으며, 임의 delay나 tolerance 변경으로 PASS를 만들지 않는다. parser는 기존 strict revision의 원본 raw 축을 보존한 뒤 명시적 alias join을 구분하고 canonical result payload는 계속 strict 비교해야 한다. 이 조건을 만족하는 최종 specialist 제안과 메인 검토를 기다린다.

### 12.5 최종 설계 검토 완료·구체 구현 manifest

메인이 최종 specialist 설계를 검토했다. §12 도입부와 §12.4의 미완/대기 문구는 이전 단계 이력이며 아래가 **승인된 구현 준비 계획**이다. 사용자 교정·검증·빌드 시도 승인은 유지하며 추가 질문하지 않는다. 메인의 code GO는 written plan 검토 뒤 발급한다. 아래는 계획 manifest이며 실제 수정 manifest/검증 결과와 구분한다.

`Assets/_Project/Scripts/` 아래 기존 C#10파일과 기존 Tools parser1파일, **총11기존파일**을 계획한다.

| 기존 파일 | 계획 역할 |
|---|---|
| `Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | epoch/occurrence·lease admission·baseline/수명·종료/중복 계약 |
| `Application/Events/GameEvents.cs` | 원자 Start payload 및 per-hit metadata 전달 계약 |
| `Infrastructure/Network/NetworkCombatController.cs` | 실제 committed Schedule의 occurrence/due 복사와 Host/Client 원자 전달 |
| `Infrastructure/Network/NetworkUnit.cs` | fresh NGO 시간 접근·current payload cache·lifetime/revision/baseline/init 복구 |
| `Presentation/Unit/UnitView.cs` | 최초 phase 진입·활성 phase follow·native event admission/output·priority/종료 |
| `Application/Combat/PistoleerTimingDiagnostics.cs` | ACTIVE lifecycle tracker·critical 사건·clock/phase/용량/format 증거 |
| `Infrastructure/Network/UnitAttackShadowObserver.cs` | 생산 scope/예약/종료와 diagnostic 연결 |
| `Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 gate에서 생산 callsite RED→GREEN·formatter/CLI 검증 보강 |
| `Tools/analyze_pistoleer_timing.py` (프로젝트 루트) | outcome 의미·revision alias/canonical payload·clock 분류 및 CLI fixture |
| `Presentation/Unit/AnimationEventRelay.cs` | 실제 marker callback의 event metadata: multihit/crossfade 구분 |
| `Infrastructure/Network/UnitAttackShadowProfileResolver.cs` | 새 wire payload에 맞는 기존 SchemaRevision/hash gate 일관성 |

마지막2파일은 메인 검토로 추가된 필요 범위다. 기존 empty relay로 multihit/전환 callback을 구분했다고 가정하지 않으며 새 validator로 우회하지 않는다. 신규 파일·validator·에셋·TC/QA·hit writer 신설은 없다. 추가 기존 파일이 필요하면 먼저 메인에게 보고한다. 규칙·권위 HP writer·타입별 정책은 변경하지 않는다.

### 12.6 epoch 원자 연결과 두 단계 native phase 제어

**원본과 원자 전달:** Host의 기존 provisional 적용 후 확보한 epoch E를 유지하고 backdate하지 않는다. bind 성공 시 동일 E를 Host/Client에 전달한다. atomic Start payload는 lifetime/epochId/E/duration/target/presentationRev/actionRev/scope/commit 및 per-hit markerOffset/absolute occurrence ordinal/markerServerTime/impactServerTime을 함께 가진다. occurrence/due는 **실제 committed Schedule에서 복사**하며 commit+0.8로 만들지 않는다. Host도 같은 기존 presentation delay와 동일 epoch를 따라 표현하고 서버 authority/예약 writer는 바꾸지 않는다.

**최초 provisional:** Client 첫 CrossFade의 fixedTimeOffset은 normalized 값이 아닌 **초 단위 `freshNGOTime - delay - E`**다. future이면 Attack 준비 상태로 기다리며 Walk/Idle/Held를 중간 삽입하지 않는다. Host bind는 기존 E를 유지한다. current payload cache는 객체 lifetime·revision·baseline을 지키고 View init pending에서 실제 적용 성공 전에 소비 완료로 처리하지 않는다. terminal/stale/death/gameend 입력은 거부한다.

**활성 같은 epoch:** commit에서 Play/CrossFade/rewind하지 않는다. 매 Update에서 Client 조기 return보다 먼저 실제 native Attack 및 next-state phase를 읽고 `advance = clamp(targetClipSeconds - currentClipSeconds, 0, 2 * actualAnimatorDt)`, `speed = advance / dt`의 forward-only bounded 표현 제어를 적용한다. 최대2는 표현 controller bound일 뿐 timing PASS 기준이 아니다. state.speed/speedMultiplier·Animator updateMode와 실제 dt·finite 검사를 존중하며 Frozen/Held/Dead priority가 우선한다. exit에서는 원래 speed/priority 소유권을 복구한다. SeparateHeal·Unresolved의 별도 charge/독립 정책은 제외하고 기존 Legacy 표현을 보존한다.

**fresh clock:** NetworkUnit은 실제 IsSpawned·manager listening을 확인한 fresh NGO ServerTime accessor를 사용한다. bridge CurrentServerTime의 stale 값이나 result-derived anchor를 이 용도로 쓰지 않는다. clock generation과 raw NGO/local 값을 기록한다. backward estimate는 hold하고 rewind하거나 단조 시계를 꾸며내지 않는다. Coordinator의 기존 pipeline clock은 조용히 대체하지 않는다. exact Ready/pixel 관계는 runtime 검증 OPEN이다.

**실제 marker admission:** native event의 실제 crossing을 bound epoch/occurrence/HitIndex와 유효 hit count에 대조한 뒤 기존 lease를 정확히 한 번 소비한다. precommit marker·빈 신호 재사용은 금지한다. 이미 지난 source occurrence는 `LateSourceOccurrenceSkipped`로 skip하고 source VFX를 소급 재방출하지 않는다. 이것은 확정 victim 결과의 기존 catch-up 정책을 취소하지 않는다. 기존 SourceOnly는 같은 승인 occurrence만 보존하며 target 경로는 닫힌다. Retire는 정확한 typed call reason을 남기고 §9/§11의 이전 미관측 close 원인을 소급 확정하지 않는다.

### 12.7 quota 밖 critical 관측·parser·schema gate

기존64 상세 scope 표본은 교정 전 이력과 분리해 유지한다. 별도의 bounded **ACTIVE scope lifecycle tracker**를 두고 critical/admission/miss-retire/cancel/dedup/baseline keyed 사건은 누적 상세 quota 밖에서 필수 기록한다. stage별 첫 사건·상세 critical과 반복 count를 구분하며24줄 cap 뒤 blind 상태로 만들지 않는다. active capacity, UTF-8 format-incomplete, drop/omission 및 전체 관측 population을 명시한다. 로그는 전이/경계에서 기존 GameLog를 통해 남기고 개발 strip·개인정보·기존 sink 등록/전달 및 중복 예외 로그 규칙을 유지한다.

parser는 합법 Miss의 amount0/hp=-1을 해당 outcome 계약에 한해 허용하며 HitApplied는 계속 strict 검증한다. 원본 raw action/presentation revision축과 explicit alias join을 보존하고 canonical result payload는 revision/victim/kind/effect/ordinal/outcome/amount/hp/impact를 strict 비교한다. local Ready-minus-Vfx를 pixel HP나 clock/network delay 증거로 바꾸지 않는다. raw local clock과 rebased NGO/presentation-derived anchor를 구분한다.

wire payload 변경은 기존 resolver의 SchemaRevision/hash gate에 함께 반영하고 old/new mismatch 거부를 시험한다. 경기 시작 시 확정한 fixed pipeline mode는 유지한다. schema bump가 Supported 승격·authority 전환·full migration 완료를 뜻하지 않는다.

### 12.8 구현 전후 검증 순서와 미완 경계

기존 gate에서 실제 production callsite의 **InitialProvisional → Animator/Relay → lease → emit/retire**를 trace-shaped controlled-clock fixture로 RED→GREEN 검증한다. 최초 정렬과 활성 phase follow를 각각 검증하며 이전 raw capture에 없던 clock을 실제 관측값으로 가장하지 않는다. multihit·crossfade callback·duplicate·lifetime·late baseline/View init·cancels/death/gameend·clock rebase·active/sample/UTF-8 cap 경계 fixture를 포함한다. pure policy fixture만으로 production 실행을 대체하지 않는다.

실제 formatter→양 역할 CLI parser 검증은 **prebuild 필수 gate**다. 최종 수정 뒤 Runtime/Editor/Development/Release 컴파일 결과와 기존 Unity Unit Action/Root Pose 자동 메뉴의 실제 관찰을 기록한 후 승인된 빌드 시작을 시도한다. 각 gate 실행 결과는 아직 대기이며 기존16 fixture/menu PASS를 새 결과로 재사용하지 않는다. 이번 문서 담당 직접 변경은 Research/Plan뿐으로 checker·source/UI/build를 실행하지 않았다. 메인이 written plan 검토 후 code GO와 구현 pipeline을 진행한다.

Units U-ATK-TIMELINE·규칙17~22 및 NET-AUTH/ROOT/TIME/PRESENT/CANCEL을 보존하는 교정이며 정책 변경은 없다. 기존 권위 HP·취소·Legacy 예외·single-writer/single-emitter를 유지하고 source 과거 marker catch-up이나 임의 delay/tolerance PASS를 만들지 않는다. 실제 RED→GREEN/컴파일/메뉴/빌드 결과와 새 runtime exact scope/phase/Ready·pixel 수용은 각각 기록한다. 빌드 시도는 완료·설치·실행 증거가 아니며 완료 확인은 사용자 소관이다. **구현 준비 계획 확정 / 실제 검증 결과 대기 / 정확 타이밍·육안·MigrationRequired/full migration OPEN.**

### 12.9 CODE GO 후 중단·재개 현황 — 2026-10-07

메인 인계상10-05 written review 뒤 CODE GO 발급 및 programmer 부분 patch 착수까지 진행됐으나 usage limit(`try again 8:07AM`)으로 중단됐다. 사용자의10-07 재개 요청으로 메인이 Sagan을 재개했다. §12.5의 기존11파일 승인 범위를 유지하며 새 Task를 만들지 않고 부분 변경·이력을 보존한다. 앞선 GO 대기/구현 준비 문구는 당시 이력이다. **현재는 구현 진행 중이며 최종 수정 manifest·검증·빌드 준비 완료로 선언하지 않는다.**

중간 검토 요구는 stale inactive close identity, queued Apply≠actual success/eligible native fallback, 좁은 SourceOnly, native event gate-before-cursor, baseline/lifetime, quota 밖 critical 로그와 parser semantic이다. programmer가 계속 작업 중이며 반영 완료나 regression PASS는 아직 확인되지 않았다. 이번 교정의 새 컴파일4경로·formatter→CLI·기존 Unit Action/Root 자동 메뉴·Build And Run 시도는 모두 미실행이다. 최종 코드/manifest 인계와 §12.8의 실제 검증 로그 확보 뒤 메인이 빌드 시작만 시도하며 완료·설치·runtime 확인은 사용자 소관이다. 정확 타이밍·육안·MigrationRequired/full migration OPEN 유지. 이번 문서 담당 직접 수정 manifest는 Research/Plan 두 파일뿐이며 source/assets/Git/UI/build·새 TC/QA/Task를 변경하지 않았다.

### 12.10 fixture 실패 교정·실제 검증 진행 — 2026-10-07

> **후속 인계:** 메인 실제 Unit Action13:57:02 A1/C2/C3 v12·production·Bloom PASS, Root13:58:29 PASS. Console errors0/warnings26(compile20+negative fixture6). programmer 보고상 추가보완 전4경로는 Runtime13:56:49 0errors/20warnings, Development13:56:51 0/20, Release13:56:50 0/24, Editor13:57:13 0/0 및 CLI21 PASS다. 아래 결과 대기는 이전 시점 이력으로 보존한다.
>
> **최종 gate 미완:** specialist가 §12.8 native multihit/crossfade coverage 미완을 인계했고 메인은 기존 gate 보완을 요청했다. 방금 PASS는 보완 전 소스 이력이지 승인 scope 전체 검증 완료가 아니다. 동결/최종4경로라는 이전 단계 표현을 후속 수정본에 재사용하지 않는다. 빌드는 아직 미시도이며 보완 후 재컴파일·CLI·Unity gate 근거를 받아 최종 보고를 갱신한다. 합성 CLI와 자동 self-validation은 runtime acceptance가 아니며 전체 OPEN을 유지한다.

13:39:12 실제 Unity Unit Action FAILED(`Ready retry must apply exactly once`)를 이력으로 남긴다. UnitData ctor의 HP/attack/range/detect를 cooldown으로 오해해 fixture fallback1초와 production clip2초가 달랐고 eligibility guard가 거부했다. fixture를 실제 UnitFactory `ApplyAttackClipCooldown`/`GetAttackClipLength`/`GetHitFrameTimes` 경로로 준비하도록 교정했으며 생산 guard·tolerance는 불변이다. 같은 기존 실패 incident에 실수·예방 근거를 추가한다. production 데이터 초기화와 fixture의 전제 일치를 먼저 검증하며 실패한 assertion을 완화해 통과시키지 않는다.

현재 확보 근거는 메인 독립 CLI21cases PASS(같은 fixture 합성 기반; runtime 수용 아님),13:54:58 Unity 재컴파일 errors0/warnings20이다. 이어 실행 중인 새 Unit Action 결과는 대기다. programmer는 현재 소스11파일 동결을 인계했고 최종4경로 컴파일 보고는 후속 대기다. §12.9의 미실행 문구는 이전 시점 이력이다. 새 Unit Action/Root·최종 전체 gate·빌드 결과·실기 acceptance를 아직 PASS/완료로 선언하지 않는다. 최종 evidence와 actual manifest 수신 뒤 갱신하며 전체 OPEN을 유지한다. 이번 직접 수정 manifest는 기존 Research/Plan 및 `.claude/mistakes/unit-action-correction.md` 총3문서뿐이다. 상시 상태 문서/메모리·source/assets/Git/UI/build·새 Task/TC/QA는 변경하지 않는다.

### 12.11 최종 구현 manifest·검증 인계 — 2026-10-07

§12.5의 승인 목록이 **실제 구현된 기존11파일 manifest**와 동일함을 메인 최종 인계로 확정한다. §12.5의 “계획/준비” 문구는 당시 상태이며 목록 자체는 그대로 보존한다. 실제 기존 C#10파일(아래 경로는 `Assets/_Project/Scripts/` 기준)과 프로젝트 루트 parser1파일:

- `Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs`
- `Application/Events/GameEvents.cs`
- `Infrastructure/Network/NetworkCombatController.cs`
- `Infrastructure/Network/NetworkUnit.cs`
- `Presentation/Unit/UnitView.cs`
- `Application/Combat/PistoleerTimingDiagnostics.cs`
- `Infrastructure/Network/UnitAttackShadowObserver.cs`
- `Editor/Combat/RunUnitActionSelfValidation.cs`
- `Presentation/Unit/AnimationEventRelay.cs`
- `Infrastructure/Network/UnitAttackShadowProfileResolver.cs`
- `Tools/analyze_pistoleer_timing.py` (프로젝트 루트)

최종 보완은 위11개 중 self-validation/UnitView/diagnostics/parser4개다. §12.8 native coverage 미완 이력은 보존하되 actual Lion hit0 baseline→hit1·각 marker1회, native clip/hash/loop, outgoing transition callback 거부/incoming 정상 소비 검사를 보완했다. duplicate-time prepublish Legacy qualification 유지, evicted known scope coverageUnknown 및 strict END numeric도 포함한다. 실제 controller/relay/production VFX callsite를 검사했지만 입자 pixel·새 multiplayer 수용은 아니다.

최종4경로 Runtime14:03:54 0errors/20warnings, Dev14:03:56 0/20, Release14:03:57 0/24, Editor14:04:28 0/0. CLI23 programmer+메인 독립 PASS. 최종 Unity compile14:05:14 errors0/warnings20, 실제 Unit Action14:06:49(A1/C2/C3v12/production/Bloom/nativePistol·Lion/diagnosticsCLI) PASS, Root14:07:44 PASS. Console errors0/warnings27(compile20+validation 중7 추가; 세부 전체 분류 미확정). 최종 소스의 implementation prebuild acceptance 완료이며 보완 전 PASS 재사용이 아니다. hash6 불변 및 과거 로그 재parse 경계는 Research §10.4를 따른다.

Root 뒤 메인 Build And Run1회에서 Building Player / Backup & unload 관찰 후 모든 UI/log/progress/완료/설치/runtime 감시를 중단했다. **빌드 시도만, 완료/PASS 아님.** 사용자 완료 확인과 새 exact Pistoleer VFX/타이밍 실기를 기다리며 전체 OPEN/MigrationRequired/full migration 미완이다. 신규 Task/TC/QA/validator/에셋·권위 writer·정책 변경은 없다.

**이번 직접 문서 수정 목록(기존10파일, 추가/삭제 없음):**

- `Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`
- `.claude/mistakes/unit-action-correction.md`
- `.claude/agent-memory/game-programmer/logging.md`
- `.claude/agent-memory/qa-tester/patterns.md`
- `.claude/MEMORY.md`

기존 programmer logging index는 이미 연결돼 있어 인덱스 파일을 중복 수정하지 않았다. 메모리는 재사용 교훈과 Task pointer만 증분 추가하며 원수치/WIP를 복제하지 않는다. 이전 이력은 보존했다. 문서 담당은 source/assets/Git/UI/build/checker/baseline을 실행·변경하지 않았고 문서 checker는 메인이 최종본 뒤 실행한다.

WORKFLOW [9] 버그 교정 대상인 qa-tester도 기존 MEMORY에서 이미 연결된 patterns 토픽 한 곳에 회귀 교훈과 Research §10.4 pointer만 추가했다(새 QA 실행 아님). gameplay/balance·문서 구조 변경이 없어 design-lead/document-manager 메모리 추가 갱신은 불필요하다.

**2026-10-07 도구 산출 후속 인계:** 전체 작업 manifest에는 직접 수정10문서와 별도로 **도구 자동 수정 `.claude/agent-memory/_baseline.json` 1개**를 포함한다. 메인이 `Tools/check_docs.py --update-baseline`을 실행해 programmer3847→3852(+5)/qa1355→1361(+6), 파일 수 불변·감소 없음으로 자동 갱신했다(직접 편집 아님). 전체 checker 검사는 메인이 이어서 실행 중이며 아직0건 결과는 미확보다.

**최종 checker 인계(2026-10-07):** 위 baseline 도구 자동 갱신과 별도로 메인의 전체 `Tools/check_docs.py`는 exit0/“문제 없음”,7종 모두 이상 없음이었다(실제 검사 범위 rules10/md77/claude40; `_Tasks/`는 검사 제외). 앞선 대기는 당시 이력이며 직접10문서+도구 자동 수정 `_baseline.json`1개 manifest는 유지한다. 빌드 완료 감시는 재개하지 않았고 완료/설치·사용자 exact VFX/타이밍/full migration OPEN은 그대로다.

### 12.12 최신 두 경기 분석 문서 인계 — 2026-10-07

**메인 checker 인계(2026-10-07):** 첫 전체 검사 exit0/“문제 없음”, 실제 범위 rules10/md77/claude40·7종 문제0을 확인했다. 이는 최종 source facts 추가 전 검사이며 최종본 검증으로 재사용하지 않는다. 최종 facts는 이 Research §10.5 / Plan §12.12에 적용 완료했고, 메인이 추가 후 전체 검사를 재실행할 예정이다. 이번 마무리 추가 수정은 Research/Plan2문서만이며 다른 문서·메모리 변경은 없다.

**Sagan+메인 최종 분석 후속:** Research §10.5에 D4361→D5007 새 provisional E, instance6 seq2의 native actual≈1.899782초 대 target≈0.000258초, HitApplied 뒤 scoped marker/VFX0·remaining1 retire 및 seq3 동일 E/native admission을 기록했다. 매 commit 무조건 E reset이 아니라 새 provisional E와 기존 Animator phase 기준 불일치의 강한 실행 근거다. dictionary E 생성/copy/activeThisTick remove와 Host sameRev no-restart·actual>target advance0 source 근거는 있으나 정확 remove/recreate 원인·매 frame hold duration·전체 근본원인 OPEN이다. 교정 검토 방향은 논리 provisional 재진입/native epoch 분리와 연속재생의 E/absolute phase/occurrence 일치이며 코드 GO/구현은 아니다. 강제 매 commit restart·delay/tolerance·HP writer 변경 금지. schema341은 진단 형식 계약 불일치, clock20 units는1 episode fanout/raw -0.3233·hold1이며 단독 지속시간은 미확정이다. 직접 수정 manifest 기존10문서 + 메인 도구 자동 baseline1개 유지, source/assets/UI/build/Git·새 Task/TC/QA 변경0. 메인 첫 checker 뒤 이 최종본으로 재실행 예정; 최종 checker 결과 대기.

**메인 review 후속:** Research §10.5에 parsed 로그행40782 / attack records2476의 단위 정정, 대부분 revision replaced·2건 동일 scope 후반 combat-target-change 추가 기록, 원본 확인 full session ID2개를 반영했다. 원수치 문장은 정정 이력과 함께 보존하며 발사표현 누락 의심·정확 시각/최종 source 원인 OPEN 경계는 유지한다.

**전체 manifest 도구 자동 수정:** 위 직접10문서와 별도로 메인이 자동 갱신한 `.claude/agent-memory/_baseline.json`1개를 포함한다. programmer3852→3854(+2)/QA1361→1363(+2), 파일 수 불변·감소0; 직접 baseline 편집은 아니다. 이번 full checker와 최종 agent facts는 후속 대기다.

단일 상세 증거 원본은 [Research §10.5](Research.md#105-최신-역할교대-두-경기-로그--2026-10-07-최종-facts-인계)다. 결과 피해 정합 수용 / 발사표현 누락 의심·미완 / 정확 시각 미확정을 분리한다. 정상 Host HitApplied의 raw marker/VFX hook 공백은 parser schema 오류만으로 정상 처리하지 않는다. source 원인과 normal pause duration은 최종 specialist 인계 대기이며 수정 계획·원인 확정·runtime PASS를 선결론으로 쓰지 않는다. formal CrossAudit 이번 미실행, exact timing/pixel/full migration OPEN이다.

이번은 기존 문서 증분 갱신만이다. 이전11 source manifest는 구현 이력으로 유지하고 이번 source/assets/UI/build/Git·새 Task/TC/QA 변경은0이다. 직접 수정 기존10문서는 다음과 같으며 신규/삭제·행수 감소 없이 기록을 추가했다:
- `Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md`
- `Assets/_Project/Docs/_Tasks/2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md`
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md`
- `Assets/_Project/Docs/PROJECT_STATUS.md`
- `Assets/_Project/Docs/ROADMAP.md`
- `Assets/_Project/Docs/WORK_HISTORY.md`
- `.claude/mistakes/unit-action-correction.md`
- `.claude/agent-memory/game-programmer/logging.md`
- `.claude/agent-memory/qa-tester/patterns.md`
- `.claude/MEMORY.md`

메모리는 전체 수치 복제 없이 기존 linked programmer/QA 토픽과 공용 교훈 pointer만 추가한다. 실패 기록도 동일 기존 incident의 후속 경계로 보존한다. 이번 checker 및 필요한 baseline 도구 갱신은 메인이 최종 문서 뒤 실행하며, 앞선 §12.11 checker exit0을 이번 수정 검증 결과로 재사용하지 않는다. 이번 문서 담당 baseline 직접 편집/도구 실행은 없다. 최종 source facts 수신 시 이 기존 절에 짧게 추가한다.

### 12.13 사용자 승인 후속 교정 계획 — native 재생 시작 기준 보존 (2026-10-07)

**최종 도구·manifest 증분:** 메인 baseline `--update-baseline` exit0(programmer3854→3858 +4 / QA1363→1367 +4; 파일 수20/3 불변·감소0·직접 편집0). 메인 전체 `check_docs` exit0·7종 이상 없음, 범위 rules10/md77/claude40(_Tasks/_Logs 제외). 전체 manifest source8 + 직접 문서10 + 자동 baseline1 =19; Temp compile/formatter CLI fixture 등 검사 임시 산출 별도. 이번 Task2 증분 뒤 메인이 전체 checker1회 재실행 예정이며 앞선0건을 증분 후 결과로 재사용하지 않는다. 사용자 runtime OPEN·Game 씬 저장창 선택 대기 및 빌드 시작/완료 미확인 경계는 유지한다. 다른 문서/메모리 추가 변경 없음.

**최신 빌드 시도20:10:51 KST — 앞선 미시도 인계의 후속:** File→Build And Run1회 클릭+즉시 snapshot에서 Game `Scene(s) Have Been Modified` 저장 확인창을 관찰했다. 저장하지 않으면 변경이빌드에 포함되지 않는다는 안내다. 범위 밖 사용자 변경 여부 미확인 Game 씬이므로 Save/Don't Save/Cancel 모두 미선택 상태로 인계했다. 씬 수정0. Building Player 실제 시작 증거가 아니며 started/completed/PASS·설치 주장 없음. 이후 UI/log/progress 감시0, 메인도 더 확인하지 않는다.

**이번 SHA256 비교 범위:** Pistoleer_Attack.anim / Pistoleer.controller / Unit_Pistoleer_Blue.prefab / Unit_Pistoleer_Red.prefab / EffectPreset_Pistoleer_Attack.asset / StatsReference.md의 실제6대상 모두 전후 불변이다. Game 씬은 이번 비교 대상이 아니며 앞선 Game hash 이력은 별도 보존한다. 아래 최종 인계의6hash는 이 범위다.

**산출 구분:** specialist 실제 source8개 / 직접 문서10개 / Temp 검사 산출은 별도다. Temp 개별 경로·개수는 인계되지 않아 추정하지 않는다. editor-schema3.log는 미생성으로 산출에서 제외한다. baseline은 메인 도구 자동 산출 범주이며 이번 추가 갱신 여부/checker 결과는 아직 미확보다.


**최종 실행 인계(20:07:02 KST):** 계획8파일 구현 완료로 갱신한다. 실제 source manifest는 아래7파일 표와 UnitAttackShadowProfileResolver.cs 추가1파일이며 resolver contract revision3이다. C3 observer v12와 별도 hash 계약이다. 기존 Unity 메뉴의 의도한 Host re-entry RED→GREEN, NCC production chain의 continuous/noRestart/true Walk re-entry/scoped once/immutable duplicate conflict/Held closed sequence 회귀가 최종 gate에 포함되었다. 과거 RED·candidate FAIL·잠정 검증 이력은 보존한다.

fresh4compile errors0/warnings Runtime20·Development20·Release24·Editor0; 메인 독립 formatter CLI28 exit0. **[🔴 2026-10-07 correction — original kept: 실제 Editor20:06:31–36 exit0·stdout 빈 값, editor-schema3.log 미생성·대체 로그 없음. 앞선 로그 파일 산출 인계는 정정하며 파일 증거로 재사용하지 않는다.]** Unity20:07:12 compile UI0errors/20warnings → Unit Action20:09:00 A1/Production/C2/C3 PASS → Root20:10:11 PASS, 실제 Console0errors/27warnings.6hash(Stats 포함) 전후 불변. 메뉴 자동 검증은 새 실기/full migration 수용이나 formal CrossAudit가 아니다.

빌드는 이 인계 시점 미시도이며 즉시 시작 예정, 결과 후속. 완료 감시·완료/설치 PASS 주장 없음. runtime 수용·정확 시각·pixel·MigrationRequired/full migration OPEN. 이번 최종 문서 실제 manifest10개: Task Research/Plan, UnitCombatAssetMatrix, PROJECT_STATUS, ROADMAP, WORK_HISTORY, 기존 unit-action-correction 실패 기록, programmer logging, QA patterns, 공용 MEMORY. 원이력 보존·행 삭제0·신규 문서0. 문서 담당 source/settings/assets/Git 추가 변경0(구현 source8개는 specialist 실제 수정). checker 및 baseline 도구 산출은 메인이 실행·인계하며 아직 이번 증분 검사 결과를 확보하지 않았다.


**최신 specialist 증분 — 필수 호환성 gate / 계획8파일:** 기존7파일에 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`를 추가한다. `PresentationContractSchemaRevision`2→3으로 원자 교정 계약의 hash를 구분하여 혼합 구/신 peer의 같은 hash 접속을 막는다. 사용자 승인 교정의 필수 호환성 gate 동기화이며 gameplay/asset 변경은 아니다. 이 인계 시점8번째 source patch는 아직 없고, 아래7파일 표는 앞선 확정 이력으로 보존한다. 최신 계획 manifest는 그7파일+resolver1파일=8파일이며 실제 수정 완료 manifest와 구분한다. 규칙 근거는 서버 권위·PRESENT 원자 적용/계약 일치 경계다.

**중간 RED와 fixture 정밀화:**19:40:15 실제 RED는 same-revision native first apply 누락이다.19:59:54 Unity compile errors0/warnings20 뒤20:00경 첫 candidate Unit Action `NativeOccurrenceBoundary` line324 `!pending.Active` FAIL은 logical close 뒤 nativeActive 유지/Scope suppression 폐기 계약과 old assertion의 혼동이었다.20:02:14 programmer 강화는 old pending Epoch1/revision2/Scope invalid/Hits null/ModeSuppressed + applied revision2 + closed instance41/sequence8을 확인한다. 동일 closed sequence8 never-reopen 불변은 유지하고 logical suppression과 native lifetime을 분리해 관찰한다. 이 강화는 최종 GREEN 또는 전체 메뉴 PASS 인계가 아니다.

**잠정 실행 결과와 재실행 gate:**7파일4compile errors0(경고 Runtime20/Development20/Release24/Editor0), compiled production formatter CLI28=기존23+신규5 PASS 인계.8번째 resolver 계약 bump 후 fresh 최종4compile·formatter CLI·Unity Unit Action/Root Pose 결과를 확보해야 하며 앞선 잠정 결과로 대체하지 않는다. 빌드는 새 gate 통과 후 시작 시도만, 완료 감시/완료 PASS는 범위 밖이다. 이번 문서 증분 실제 수정은 Research.md·Plan.md2개뿐이며 source/assets patch0, 신규 문서0; 최종 상태/메모리 갱신은 후속 최종 facts를 기다린다.

**실행 증분 — 기존 Unity Unit Action RED:** 실제 기존 메뉴 실행에서 `Host reentry RED new epoch first apply must align old native playback; same-revision preapply currently skips it`을 확인했다(fixture97 / Run598 인계 anchor). 계획된 production Host re-entry 회귀의 의도한 재현이며 setup 실패로 분류하지 않는다. RED 단계 확인 후 프로그래머 교정 재개 지시가 내려졌다. GREEN/구현 PASS 및 새4compile·CLI·Unity2menus 결과와 빌드 완료는 미확보다. 최종 facts 수신 전 완료 판정하지 않는다.

**Specialist 확정 보강(동일7파일):** 아래 예상 manifest는 동일7파일 계획으로 확정되었다. 후속 확정 인계가 앞선 “확정 전” 표기의 상태를 갱신하며 실제 source 수정 결과는 아직 아니다. native 적격 Host old 선행 Start를 대체하고 검증 전 기존 로직을 주석 비활성화하여 보존한다. owner/lifetime·Root·priority·metadata·clock 사전검증 → E/phase 준비 → phase bind+first native apply 원자 경계 → 실제 성공 후 publish/receipt를 요구한다. queued≠success, 중복 fallback 금지. `firstApplied(lifetime,nativeEpoch)`와 revision을 분리하고 same-epoch commit은 scope lease만 연결한다. true exit/re-entry에는 새 first sync, SourceOnly terminal held/closed epoch에는 보존 정책을 적용한다.

로그에 episode ID를 넣어 hold/resume을 구별한다. clip.length/state.length/speed/state rate를 분리하고 speed0-derived Infinity는 explicit paused에서만 인정하며 비정상 Infinity는 오류로 유지한다. canonical 정합과 source completeness는 독립 gate다. 기존 회귀를 실제 Host re-entry production 체인의 약1.9초 phase 불일치 RED→GREEN으로 보강하고 실제 controller/clip/relay/VFX를 검증한다. private fixture 값만 대입하는 shallow 검증으로 대체하지 않는다. Stats/assets/damage/due/delay/tolerance 불변. 아래4compile·formatter CLI·Unity2menus·build start-only 순서는 그대로이며 실제 새 결과는 후속 메인 인계로만 기록한다.

논리 공격 회차와 native 재생 시작 기준을 분리한다. 타겟 교체·연속 commit 중 Attack이 연속 재생되면 같은 E/absolute phase/occurrence를 유지하고, 실제 state exit/re-entry에만 새 E와 Host/Client firstStart를 원자적으로 적용한다. 매 commit 강제 restart로 증상을 덮지 않는다. Research §10.6의 Host preapply→새 E 생성→same-revision bind는 강한 단서이며, 정확한 원인·수명 설계는 specialist 후속 검토로 정밀화한다.

**공통 규칙·불변 범위:** 서버 권위(NET-AUTH/SERVERAUTH), ROOT, TIME, PRESENT, CANCEL과 Units `U-ATK-TIMELINE`·규칙17~22를 준수한다. damage/HP writer·Root·Stats·clip marker·에셋·delay·tolerance 불변. 새 Task/TC/QA를 만들지 않고 기존 fixture/gate를 확장한다. 기존 교정 로직 제거 전 주석 비활성화 정책과 원이력 보존을 적용한다.

**예상 구현 manifest — 기존7파일, specialist 세부 확정 전 계획이며 실제 수정 목록 아님:**

| 기존 파일 | 계획 범위 / 규칙 근거 |
|---|---|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkCombatController.cs` | 논리 provisional 수명과 native E 수명 분리; activeThisTick cleanup·Host 선적용/new E/publish 연결 검토. SERVERAUTH, PRESENT, CANCEL, U-ATK-TIMELINE |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | 연속 same-revision bind와 실제 firstStart/re-entry 구분; Root writer 불변. ROOT, PRESENT, Units19~22 |
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkUnit.cs` | Host/Client atomic firstStart·revision/target/lifetime 경계 보존. SERVERAUTH, PRESENT, CANCEL, Units22 |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | 같은 E/absolute phase/occurrence 유지와 실제 re-entry 기준 연결; delay/tolerance 불변. TIME, PRESENT, U-ATK-TIMELINE, Units17~20 |
| `Assets/_Project/Scripts/Application/Combat/PistoleerTimingDiagnostics.cs` | epoch 사유·phase·hold/resume·native approval·remaining·expiration/drop 기록; 원본 clip.length와 speed0-derived Infinity 분리. TIME, PRESENT, CANCEL |
| `Tools/analyze_pistoleer_timing.py` | 위 production 진단 형식의 검증·분류; schema 오류/피해 결과/표현 누락/clock episode를 별도 판정. TIME, PRESENT |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | 기존 native fixture에 실제 Host 체인 RED→GREEN과 수명/재진입 회귀 추가. SERVERAUTH, ROOT, TIME, PRESENT, CANCEL, Units17~22 |

specialist가 위 목록·구체 경계를 확정하거나 수정하면 구현 전 기존 절에 증분 기록한다. 문서 담당은 source 설계/patch를 수행하지 않는다.

**로그와 기존 회귀 gate:** E create/keep/remove 사유, logical scope/native epoch, actual/target phase, hold/resume, native approval, remaining, expiration/drop을 상관키로 연결한다. 원본 clip.length와 speed0-derived Infinity를 구별하며 기존 fail-closed를 완화하지 않는다. 로그 생성·유지를 계획하고 제거/비활성화 사유를 기록한다. 기존 native fixture는 Host preapply→new E→same-revision bind의 실제 production 체인에서 RED를 확인한 뒤 GREEN을 요구한다. true Walk exit/re-entry, continuous commit, target switch, Frozen, SourceOnly, late baseline, duplicate 및 lifetime 회귀를 포함한다. 확정 결과를 취소하거나 과거 occurrence를 새 회차로 재사용하지 않는다.

**실행·수용 순서:** specialist 세부 계획 확정 → 승인 범위 구현 → 기존 gate RED/GREEN → 새 Runtime/Development/Release/Editor4compile → production formatter CLI → Unity Unit Action 및 Root Pose 두 메뉴의 새 PASS → 빌드 시작 시도만. 실제 결과·시각·수정 manifest는 메인 인계 후 기록한다. 빌드 완료 감시/완료 PASS는 이 계획에 포함하지 않으며 메뉴 PASS는 formal CrossAudit 또는 신규 실기 PASS가 아니다.

현재 사용자 계획 승인 기록 완료, 구현·새 gate/compile/CLI/menu/build 결과 미확보. 결과 피해 정합 수용 / 발사표현 미완 / 정확 시각 미확정 및 전체 OPEN·MigrationRequired 경계를 유지한다. 이 증분의 실제 수정 manifest는 Research.md·Plan.md2개뿐이다. 기존 수치·이력은 삭제하지 않았고 source/assets 변경0, 신규 문서0. 최종 문서 checker는 메인이 실행하며 이전 exit0을 이번 증분 결과로 재사용하지 않는다.
