# Research — Pistoleer 공격 타이밍 정합성 교정

> **최신 저장 로그 분석 — 2026-10-08 / overall OPEN:**01:26 device+editor의 Editor Host/Android Client 단일 Pistoleer 경기에서 payload/피해 필수 표현 일치를 확인했다. 이전 source 누락의 상세 표본은 이번에 없으나 global fresh phase clock regression으로 parser completeness는 unknown이며 전체 PASS가 아니다. 종료 후 callback 의심은 별도 OPEN, 정확 시각/픽셀/full migration OPEN. 아래 빌드 관찰 경계와 과거 실패는 독립 이력이다.

### 10.9 최신 저장 단일 경기 분석 (2026-10-08 01:26 인계)

**경기 식별·범위:** 메인 확정 facts의 Editor Host/Android Client 동일 shared session은 `1126c7730be6f0a9313c32e51b735a5ec3e174a237102706b13f11d66cd096b4`다. Pistoleer64기(32+32)만 생산됐으며 TorrentSpirit/BloomFairy runtime 증거는 없다.01:26 저장 device+editor 한 경기로 집계하고 이전 경기·메뉴 gate와 합산하지 않는다. 추가 agent facts는 후속 증분으로 받는다.

**독립 결과 축:** 기존 parser가 파싱한 로그행12891, schema/truncated/duplicate/canonical conflict0; canonical paired307, attack records650/paired325/unpaired0/source incomplete0이다. 메인이 기존 parser를 직접 호출해 parsed12891·canonical paired307·paired325·unpaired0·fresh clock 오류2 및 양쪽 정상204scope의 Marker/Attempt/Result204/204를 독립 재확인 완료했다. canonical payload307 exact 일치와 Host ready307·expectedVisual276·emits276·failures0 및 Client 동일276 필수 표현은 확인된 피해/표현 전달 범위다. 공간 표현 표본은 Host233/mismatches0/max0.000, Client232/mismatches0/max0.001이다. records650/paired325를 전325회 native/VFX PASS로 바꾸지 않는다. 상세64scope 한도와 critical 전스코프 채널의 범위를 구별한다. 이번64유닛, 상세64scope, §10.8 이전 복구의 보호64파일은 서로 다른 분모다. 상세 표본의 source incomplete0·이전 누락 표본 부재는 전스코프 해결 또는 픽셀 수용을 뜻하지 않는다.

**agent 최종 발사표현 관측:** peer-local VFX Started는 Host298/Client301이다. 모두 systems/active/manager 양수·maxsystems1이었다. SourceStop/Cancel/Unavailable이 없는 결과 scope는 Host204/Client204이고, 양쪽 Marker→Attempt→Result 연결204/204를 확인했다. 각 peer 내부 exact Admission/Marker/Attempt/VfxResult 연결의 missing/extra/duplicate는0이다. 이는 관측된 로컬 연결의 완전성이지 Host298과 Client301의 동일 회차 집합 일치 또는 전325회 VFX 수용을 뜻하지 않는다. terminal source 미관측 Host9/Client6은 유지하며 종료 등 원인 분류가 없으므로 결함으로 단정하지 않는다. clock2records의 전체 unknown과 상세64scope 범위 때문에 정확 타이밍 PASS는 불가하다.

**clock 경계·재개 확인:** global `fresh_phase_clock_regression`2 records 때문에 parser completeness는 전체 unknown이다. Client frame26530/unit0·1,01:24:41.230/232에서 raw `48.316379278711977→47.983352895826101`(333.026ms 후퇴), hold1을 관측했다. 이는2units에 보인 하나의 clock adjustment이며 독립2episode로 집계하지 않는다. rebased217과 별개이고 네트워크 보정의 정확한 원천은 미확정이다. Device12790/12804의 PhaseHold reason=clock-regression은 local1214.5649522886031(unit0)/1214.566674673604(unit1),frame26530이다.12943/12950의 resume은 local1214.876806634589/1214.8781185195783,frame26540·같은 reason이며 actual2.8888001441955566은 변하지 않았다. 따라서 pause311.854ms/311.444ms 후 재개가 확인됐고 영구 멈춤이 아니다. UnitView932~959의 raw clock<high-water에서 Animator.speed=0·고수위 회복 뒤 재개하는 source와 일치한다는 메인 인계다. 이 짧은 정지/재개 관측은 정확 영상·픽셀 시점이나 전체 timing PASS를 확정하지 않는다.

**종료 후 callback 별도 OPEN:** Host GameEnd01:25:32.176 뒤13.408초인01:25:45.584에 unit37 `provisional-apply` MOVE handoff error가 발생했고, 종료 직전 END despawn01:25:45.725는 FAIL이다. 메인 source 인계는 OnEnteredCombat2603의 spawn/server guard에 `_combatStopped` 없음, NetworkUnit700 GameEnd CloseAttackPhase(true),370 ended=true,227 Publish reject를 확인했다. 종료 후 callback→거부→진단 failure는 code-supported 유력 chain이나 실제 reject 분기의 추가 trace가 없어 최종 원인을 단정하지 않는다. 이번은 분석이며 교정 구현/취소 정책 변경을 승인하거나 실행하지 않는다.

**ROOT·최종 판정:** 양쪽 ROOT INCOMPLETE/errors0은 only1type 및 representativeTypes<2 coverage 때문이며 pose failure로 분류하지 않는다. formal CrossAudit는 이번 미실행이다. 최신 판정은 “payload/피해 표현 확인, 이전 source 누락 상세 표본 없음; clocks/종료 후 callback 별도 OPEN, overall OPEN”이다. exact timing/pixel/full migration과 Torrent/Bloom 실기는 미수용이다. 이번 code/scene/assets/Unity/build/Git/TC 실행0, 과거 이력 보존.

> **최종 빌드 시도 인계 — 2026-10-08:** fresh 두 메뉴 PASS 후 File→Build And Run1회 클릭, MissingProjectID 확인창 Yes 선택 후 `Detect Java Development Kit (JDK)` / `Checking JDK...` 진행창만 관찰했다. 저장창 없었음. 이후 Unity UI/Editor/build logs 감시 전부 중단했다. 빌드 시도/preflight 관찰이지 Building Player 시작·완료·설치·runtime 수용 증거가 아니다. 아래 시작 시도 예정 표기는 이전 단계다.

> **최신 실행 결과 — 2026-10-08:** 세 clip 연결 적용·Unity Reload·fresh Unit Action00:35:14 및 Root SelfValidate00:36:25 PASS가 메인 실제 UI/fresh Editor.log로 확인됐다. Build And Run은 다음 시작 시도 예정이며 아직 시작 미확인이다. runtime·정확 시각·full migration OPEN. 아래 계획/patch 전 표기는 보존된 이전 단계다.

> **최신 현재 — 2026-10-08 / 사용자 씬 폐기 완료·기존 작업 복귀:** 사용자가 직접 Game 씬 변경을 폐기했다. §10.7의 복구 후보 탐색·복구 OPEN은 이전 단계 이력이며 더 진행하지 않는다. 에이전트 재구성/복원 성공을 뜻하지 않는다. 사용자가 TorrentSpirit·BloomFairy도 함께 진행하도록 승인했다. 현재는 세 클립 연결 재적용 계획이며 새 연결 적용·fresh Unity 검증·빌드 시작은 아직 확인 전이다. 기존 source8/contract3와 이전 PASS는 보존하되 runtime/정확 시각/full migration은 OPEN이다.

### 10.8 사용자 씬 폐기 후 세 유닛 AttackTimeline 연결 재적용 계획 (2026-10-08)

**빌드 시도 최종 facts:** 메인이 File→Build And Run을 좌표 클릭1회 실행하고, MissingProjectID의 “Unity services 없이 계속?” 확인창에서 Yes를 선택했다. 직후 Detect Java Development Kit(JDK)/Checking JDK... 진행창을 관찰했으며 저장창은 없었다. 이후 모든 Unity UI/Editor/build logs 감시는 중단했다. 완료/설치/runtime은 미확인이고 빌드 성공을 주장하지 않는다. 이 시도의 문서 manifest는 Research/Plan 및 PROJECT_STATUS/ROADMAP의 최소 pointer, 공용 MEMORY의 이전 recovery 대기 superseding pointer다. 코드/씬/asset/UI/Git 조작은 문서 담당이 실행하지 않는다.

**적용 및 fresh gate 완료 인계:** 메인이 Game UnitFactory의 Pistoleer0/TorrentSpirit18/BloomFairy27(Transcendence[7]) 세 clip 연결만 적용하고 git diff로 확인했다.2255blocks 동일·extra/deleted0·duplicate/dangling0이며, apply_patch 주변12줄 CRLF→LF 개행 변경 외에는 연결3줄 추가뿐이다. 보호64파일 hash 및 기존 source8은 불변이다. Unity Reload를 실제 수행했으며 Game* 없음, Console clear 이후 새 오류0·새 structural 오류0을 확인했다. 문서 담당의 scene/UI/Git 실행 근거가 아니라 메인 실행 인계다.

**최종 gate 인계:**10-08 Unit Action00:35:14 전체/Production/C2/C3 및 Bloom exact timeline PASS, Root SelfValidate00:36:25 PASS를 실제 Unity UI와 fresh Editor.log로 확인했다. Console은 fixture warnings7·errors0이고 Game* 없음이다. 메인 확인 Game 디스크 SHA256 `95ba815e1fa2bced2e83a1c55ba639f0f526a52a828e80a7379a065b2c2856a6`은 gate 후에도 동일했다. 기존 source8/보호64assets 불변은 이 범위의 증거이며 사용자 폐기 전 사고가 없었다거나 시각 실기 수용이 완료됐다는 뜻은 아니다. Build And Run 시작 시도는 예정이고 아직 시작/완료/설치 미확인이다. 후속 build facts를 별도로 추가한다.

사용자 폐기로 MistShrine 원복 대응을 종료하고, 기존 Pistoleer 교정 코드가 남아 있는 상태에서 누락된 production clip 연결만 재적용한다. 메인의 disk 확인에 따르면 `Assets/_Project/Scenes/Game.unity`의 UnitFactory component `1708566852`에서 Human0 Pistoleer, Spirit18 TorrentSpirit, Transcendence25 BloomFairy의 `attackTimelineClip` 연결이 누락됐다. 아래 GUID는 메인이 각 clip meta를 직접 확인해 인계한 값이며 문서 담당이 씬을 편집하거나 독립 검증한 결과는 아니다.

| 기존 UnitFactory 항목 | 재연결할 clip GUID |
|---|---|
| Human0 / Pistoleer | `3dc19d88c3a013c46af811597796c70a` |
| Spirit18 / TorrentSpirit | `0396c9df8348dd04a99ba18bfb31485a` |
| Transcendence[7] / enum27 / BloomFairy | `b934af543a2aa8f4dbc72962f233fa9f` |

**[🔴 2026-10-08 correction — original kept: 위 최초 인계의 “Transcendence25 BloomFairy”는 메인 식별 오류다.]** 코드 담당 preflight 보고를 `Assets/_Project/Scripts/Domain/Unit/UnitType.cs`의 enum 선언으로 대조했다: RabbitTrickster=25, BloomFairy=27. 실제 연결 계획 대상은 BloomFairy enum27 / Transcendence[7]이며 type25에 Bloom clip을 연결하지 않는다. 메인/프로그래머 디스크 인계는2255blocks·중복0·dangling0·HEAD와 개행 외 일치이며 아직 scene patch 전이다. 문서 담당은 enum을 변경하지 않고 계획 식별만 정정한다.

**규칙 근거:** 문서 담당은 `GameSystemRules.md` 인덱스와 `GameSystemRules_Units.md`, `GameSystemRules_UnitCombatSynchronization.md`의 관련 원문을 확인했다. 메인도 실행 전 같은 원문을 직접 확인하고 근거를 남겨야 한다. Units 규칙17~22와 U-ATK-TIMELINE, NET-PRESENT-001/002의 production AttackTimeline 연결·연속 재생·회차 허가를 유지한다. Torrent는 Units 규칙27/29와 NET-DELIVERY-TRAVELING 및 NET-CANCEL의 기존 예약/독립 contact 정책을 유지하고, Bloom은 Units 규칙32~37 및 SeparateHeal 계약을 유지한다. NET-AUTH/ROOT/TIME/PRESENT/CANCEL의 서버 권위·Root 단일 writer·기존 시간축·취소 경계는 변경하지 않는다. 이는 연결 복원 계획이지 정책 변경이나 Supported/full migration 승격이 아니다.

**실행 범위와 증거 경계:** 메인 변경 예정 파일은 Game.unity 하나, 위 세 연결의 정확한 3-line patch뿐이다. 사전 snapshot과 사후 diff로 이 범위를 확인하며 다른 empty field 정규화·전체 씬 overwrite·setup 재실행은 금지한다. Unity가 읽고 있는 씬이 dirty라면 저장하지 않고 disk reload와 실제 로드 내용 일치를 먼저 확인한다. Stats/밸런스, clip/events, VFX, 코드/공유 정책 및 settings는 변경하지 않는다. 기존 source8·diagnostics·resolver contract3는 유지하며 새 코드 교정은 필요하지 않다는 메인 인계다.

**후속 gate:** 연결 및 disk reload 확인 후 기존 Unit Action와 Root Pose 메뉴를 새로 실행해 실제 PASS를 확인한다. 이전 source8 compile/menu PASS는 역사적 증거로만 유지한다. 두 fresh gate가 통과한 뒤 Build And Run 시작만 시도하며 완료·설치·runtime 감시는 하지 않는다. 저장 선택창이나 시작 전 blocker는 실제 관측대로 별도 기록하고 빌드 시작으로 추정하지 않는다. 결과는 메인 후속 facts 수신 후 기록하며 지금은 계획만 완료한다. 신규 Task/TC/검증기 없음; 문서 담당은 이 Research와 Plan만 수정하고 source/scene/UI/Git mutation은 실행하지 않는다.

> **최신 정정 — 2026-10-08 / 씬 복구 OPEN·빌드 보류:** 10-07 자동 조작 중 무관 MistShrine 씬 setup 실행·자동저장 사고가 확인되었다. 이전 “씬 수정0” 작업 전체 주장을 철회하며 원문은 이력으로 보존한다. 사용자는 이후 저장창 Cancel을 확인했으나 이미 발생한 disk 저장의 복구 증거는 아니다. 원본 Game baseline·정확한 serialized diff 미확보, 복구/새 빌드 미실행. source8·compile·두 메뉴 PASS는 독립 증거이며 씬 무변경/실기 수용을 보증하지 않는다. 상세 §10.7 / Plan §12.14; 아래 “최신” 표기는 이전 인계다.

### 10.7 무관 MistShrine setup 실행·자동저장 사고와 씬 범위 정정 (2026-10-08 기록)

**최신 복구 후보 인계 — 앞선 baseline 미확보 이력의 후속:** 사용자가 Git read-only를 승인했고 메인이 Game 최신 이력 `38f56aeb`,2026-09-30 19:50:22, `chore preserveunitcombat`를 확인했다. HEAD의 old Mist panel이 복구 후보로 확보되었지만 semantic/block-level diff 분리는 아직 대기다. 메인 diff6221insert/6205delete는 대부분 Mist panel data이며10/04 이후 정당한 Pistoleer configs도 있으므로 Game 전체 파일 덮어쓰기는 안전하지 않다. Sagan에게 block-level 분리가 위임되었다. “baseline 없음”은 이전 탐색 단계이며 현재는 HEAD old Mist panel available/semantic diff pending, restoration 미완이다. 이 문서 담당은 Git 실행·씬 복구0.

**추가 read-only 인계:** sibling `D:/Projects/Hexiege/Hexiege-main-firebase/Assets/_Project/Scenes/Game.unity`는 mtime2026-07-13 20:48:54·length991248로 너무 오래되며 승인된 복구 baseline이 아니다. 현재 Game SHA256은 `A51F7F5C4C9E52210CA450D721AD60497D313143F9CBBE467D07D3671D74202F`로 인계되었다(현재 식별값이지 사고 전 baseline 아님). native window 조회는 Game*·Cancel popup 사라짐을 확인했지만 capture는 ChatGPT foreground 가림 상태여서 추가 Unity 내용 확인으로 확대하지 않는다. Unity 입력0. Git read-only 허가는 질문 후 대기이며 이 문서 작업에서 Git 실행0. 복구 완료 아님.

**Sagan read-only 완료 인계:** setup의 기존 panel DestroyImmediate/clone에는 Undo/full reference remap이 없고 bootstrapper는2slots만 배선한다. 현재 panel GO277456444/UI277456446,58GO/303blocks를 확인했지만 Range/Network가 기존인지 신규인지 미확정이다. 이것은 현재 구조 식별값이며 사고 전과의 diff가 아니다. 삭제된 기존 custom layout/icons/events/references는 baseline 없이는 안전하게 복원할 수 없다. source-only 재구성은 안전한 복구 대안이 아니며 원본 baseline이 필요하다.

권총병 교정과 무관한 씬 설정 작업이 자동 조작 중 실행되어 저장된 사고를 기록하고, 원래 씬을 확인하지 못한 채 빌드를 진행하지 않도록 현재 상태를 바로잡는다. 이 문서 갱신은 복구 실행 승인이 아니며 기존 Battle/Pistoleer 구현과 이력을 보존한다.

**확정 인계 근거(발생일2026-10-07):** 원래 Login에서 Game으로 전환되었다. 자동 조작95는 Unit Action의 observed mapping41108을 대상으로 했으나 실제 MistShrine setup이 실행되었다. Console19:37:20 제거 기록 /19:37:23 완료 기록, Game.unity diskLastWrite19:37:21이 인계되었다. 실행·자동저장 사고는 확인되었지만 정확한 dispatch 원인은 미확정이다. stale mapping 등 특정 원인을 확정하지 않는다.

**source 동작과 실제 변경의 경계:** 인계된 `Assets/Editor/Setup/MistShrineSetup_Scene.cs` Run은 bootstrapper가 없으면 Game을 열고, panel DestroyImmediate·clone, Range/Net/Bootstrapper 배선을 수행하며 자신이 씬을 열었으면 SaveScene한다. 이는 source상 가능한 menu effects이고, 변경 전후 Game의 정확 serialized diff나 모든 대상의 실제 변경 확정 목록이 아니다. MaterialMistShrineRangeOverlay의 mat mtime은09-30로 불변이며 material 수정이라고 주장하지 않는다.

**복구 근거 미확보:** 현재 인계 씬은 Game*이며 사용자가 저장 확인창 Cancel을 눌렀다고 확인했다. Cancel은 이전 자동저장을 취소/복원했다는 근거가 아니다. `Temp/__Backupscenes/0.backup`14:53 파일은 byte strings LoginRootView/EmailLoginPanel 및 Game 부재 근거로 LOGIN으로 확인되었으므로 원본 Game 복구 baseline으로 쓰지 않는다. 원본 Game baseline은 아직 찾지 못했고 정확 scene diff도 미확정이다. 복구 실행·추가 빌드 없음.

**[🔴 2026-10-08 correction — original kept: 이전 §10.6 및 관련 상태 문서의 “씬 수정0”·작업 전체 asset/settings 변경0 범위 주장은 이 무관 setup 실행·자동저장 사고를 누락하여 부정확했다. 이번 문서 담당이 씬을 편집하지 않았다는 국소 사실과 전체 조작 동안 씬 변경이 없었다는 주장은 다르다. source8/compile/메뉴 PASS와 이번6hash 불변은 독립 범위로 유지하며 Game 무변경 또는 복구 완료 증거로 확대하지 않는다. 이전20:10:51 저장창 미선택 기록은 당시 이력이며 최신은 사용자 Cancel 확인이다. Building Player 시작/완료 PASS는 확보되지 않았고 현재 build blocked/recovery OPEN이다.]**

이번 허용 범위는 문서/기록 정정·정합성 검사뿐이다. 코드/씬/asset/Unity 입력/Git 작업0, 신규 Task/TC0. 실제 사고로 저장된 Game은 이전 source8 manifest와 별도 비범위 사고 대상이며, 정확 diff 미확보 상태에서 추가 변경 개수나 복구 성공을 추정하지 않는다.


> **최신 두 경기 분석 — 2026-10-07 후속:** canonical 피해 결과1165건 HostResult=ClientReceive 완전 일치(conflict/unpaired0)는 결과 피해 정합 수용 범위다. 정상 Host HitApplied19 scope에서 raw Marker/VfxAttempt/VfxResult가 없고 취소·Unavailable·SourceStop 없이 다음 revision replace로 종료됐다. 발사표현 누락 의심·원인분석 OPEN/전체 수용 미완이며 schema341·fresh clock20으로 parser 전체 records unknown이다. schema 오류만으로 전체 정상이나 gameplay 실패를 단정하지 않는다. 정확 시각·픽셀·full migration OPEN. 상세 Research §10.5 / Plan §12.12; 아래 prebuild 및 이전 경기 기록은 이력으로 보존한다.

> **최신 상태 — 2026-10-07:** 승인된 기존11파일 구현·보완 및 빌드 전 검증 완료(4경로 errors0, CLI23 독립 PASS, 실제 Unit Action14:06:49·Root14:07:44 PASS). Build And Run1회 Building Player / Backup & unload 관찰 후 감시 중단; 완료/설치/실기는 사용자 확인 사항이다. 정확 Pistoleer VFX·타이밍/육안/full migration은 OPEN·MigrationRequired 유지. 아래 이전 상태·coverage 미완·대기 기록은 당시 이력이다.

권총병의 발사 순간을 바꾸는 작업이 아니라, 이미 저장된 Attack 클립의 0:24 타격 표식과 설정·실제 생산 연결을 일치시키는 작업입니다. 다른 유닛과 기존 공격 연출은 그대로 유지합니다.

**작성 시각:** 2026-10-04 21:58 KST  
**현재 상태(2026-10-07):** 기존11파일 구현·최종 gate PASS·Build And Run 시작 시도까지 완료. 빌드 완료/설치 미확인, 사용자 exact Pistoleer VFX/타이밍·육안/full migration OPEN(MigrationRequired). 상세 Research §10.4 / Plan §12.11.

**공통 표현 코드 영향 경계:** 위 “다른 유닛/연출을 그대로 유지”는 에셋·스탯·gameplay policy·피해 권위 보존을 뜻한다. 공통 native phase 적격 타입에는 shared expression 코드 영향이 있으며 기존 gate에서 Pistoleer/LionKnight 대표 생산 경로를 검사했다. 규칙 변경이나25종 전체 실기 PASS를 뜻하지 않는다.

**10-05 당시 분석 상태(원문 이력):** 10-05 Android Host/Editor Client 결과 전달 정상 / sampled Client 2회 committed scope와 연속 marker 연결 결함 강한 근거·정확 원인 일부 미관측 / 파서 production 의미 해석 오류 확인 / 정확 타이밍·육안·full migration OPEN, MigrationRequired 유지. 이전 구현·fixture·메뉴·빌드 관찰은 당시 이력으로 보존한다.

## 1. 착수 당시 확인 상태 — 2026-10-04 이력

아래 config2초·Game 명시 참조 없음 표는 착수 당시 관찰이다. 초기 교정 뒤 config marker0.8초·Game 명시 연결을 적용했으며 이후 이력과10-07 최종 소스/검증 상태를 이 초기 표로 대체하지 않는다.

메인·Sagan의 읽기 전용 검토와 저장 데이터 확인 기준입니다.

| 항목 | 확인 내용 |
|---|---|
| 생산 Attack 클립 | `Assets/_Project/Animations/Units/Pistoleer/Pistoleer_Attack.anim`: 30fps, 길이 2초, 단일 `OnAttackHit` 0.8초 = 0:24 |
| 설정 | `Assets/_Project/Resources/Config/UnitStatsConfig.asset` type 0의 `hitFrameTimes=[2]`; 다른 스탯은 변경 대상 아님 |
| 문서 표기 | StatsReference의 기존 `0:80(2:00)`는 이번 30fps 0:24와 구분해야 하며, 현재 문서 수정은 보류 |
| 실제 유닛 등록 | `Assets/_Project/Scenes/Game.unity`의 UnitFactory `_humanPrefabs` type 0에 명시 `attackTimelineClip` 없음 |
| 생성 경로 | 기존 UnitFactory는 선택한 클립의 이벤트를 자동 추출하므로 config 2초만으로 실제 피해가 2초에 발생한다고 단정할 수 없음 |

Game 씬의 BuildingFactory Human 목록이 아니라 **UnitFactory의 유닛 목록**이 변경 대상입니다. 확인 당시 위치는 UnitFactory 약 41678행이며 BuildingFactory 약 9897행과 혼동하지 않습니다.

## 2. 명시 연결 식별자

- Attack: fileID `7400000`, GUID `3dc19d88c3a013c46af811597796c70a`.
- Blue: `Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab`, GUID `7bb22226c2f892a4b861224ca882519a`.
- Red: `Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Red.prefab`, GUID `0fb1fc8960816a449a56977bd4ca2c78`.
- Controller: `Assets/_Project/Animations/Units/Pistoleer/Pistoleer.controller`, GUID `f65c79390b6c2734fae59aa4d0a48098`.

기존 선택·자동 추출 구현은 `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`에 있습니다. 이 구현 파일 자체는 이번 변경 대상이 아닙니다.

## 3. 최소 교정과 증거 경계

계획은 config type 0을 0.8초로 정합화하고, 실제 Game 유닛 등록에 정확한 Attack 클립을 명시하며, 기존 Unit Action self-validation에 production 정합성 검사를 추가하는 세 가지입니다. 상세 계약은 [Plan](Plan.md)에 있습니다.

클립·컨트롤러·팀 프리팹·VFX 에셋·emitter·pool·offset·Idle은 변경하지 않습니다. 서버 권위와 기존 HP/결과 표현 경로를 유지하고, 분류 승격·full migration·랠리 공간 결함·성능 교정은 포함하지 않습니다.

6월 Pistoleer VFX pool 작업은 [기존 Research](../../2026-06-07/23_56_pistoleer-attack-vfx-pool/Research.md)와 [Plan](../../2026-06-07/23_56_pistoleer-attack-vfx-pool/Plan.md)에 보존합니다. 4월 Idle 및 6월 8일 spawnpoint 이력도 이번 범위에서 재작성하지 않습니다. 과거 Plan의 클래스 설명을 현행 구현 증거로 사용하지 않습니다.

새 static/menu PASS, 빌드 시도 및 실제 화면 타격 수용은 아직 없습니다. 메인의 실제 인계 후에만 결과를 추가하며, 빌드 시도와 빌드 완료를 구분합니다.

## 4. 구현·검증 인계 — 2026-10-04

메인이 승인된 정확한 3파일 구현을 검토했다: UnitStatsConfig type 0 hit=[0.8], Game 씬 UnitFactory type 0의 정확한 Attack 명시 연결, 기존 RunUnitActionSelfValidation의 Pistoleer production guard 추가. 클립·VFX·Idle·권위·분류 불변이다.

새 csc static 결과(오류/경고)는 Runtime **0/20**, Editor **0/0**, Development **0/20**, Release **0/24**다. 메인이 실제 Assets Refresh 뒤 **22:04:36** Unit Action self-validation을 실행했고, 제한된 새 Editor.log에서 전체 및 PRODUCTION-TIMELINE/C2/C3/Bloom PASS를 확인했다. 새 Pistoleer guard 호출이 production PASS 출력 전에 실행되는 것도 확인했다.

Root 메뉴는 실제 UI·새 Editor.log PASS이며 22:09 빌드 시도/preflight만 확인했다. production 정합성 검증은 새 실제 경기·피해/발사 순간 육안 수용·full migration PASS가 아니다. 실기 OPEN을 유지한다.

### 별도 관찰 — 승인된 3파일 밖 Blue 프리팹 변경

메인 해시 비교에서 `Assets/_Project/Prefabs/Units/Human/Unit_Pistoleer_Blue.prefab`의 SHA가 baseline `B2B…`에서 `5319…`로 바뀌었다(전달된 축약값). mtime은 **22:07:10**, 길이는 **40087 bytes**다. 관찰 당시 Unity prefab AutoSave/Animation이 열려 있었으나 이것이 원인이라는 증거는 없다. 메인과 programmer의 직접 프리팹 쓰기는 없었다고 인계받았으며 실제 변경 원인은 미확인이다.

이는 승인된 직접 구현 3파일과 분리되는 파일 변경 관찰이다. 양 팀 프리팹 bytes가 모두 불변이라고 주장하지 않는다. 메인 비교에서는 clips/controller/Red/runtime hash는 불변이었다. Blue를 복원하거나 추가 수정하지 않으며 사용자 책임·preview 원인·게임 실패를 추정하지 않는다. 위 VFX/Idle 등 보존 문구는 승인된 구현 범위 계약이지 모든 자산 bytes 불변의 증명이 아니다.

## 5. 최종 메뉴·빌드 시도 인계 — 2026-10-04

메인이 실제 UI에서 UnitRootPoseCrossAudit SelfValidate를 실행하고 새 Editor.log의 Root self-validation PASS를 확인했다. §4의 Unit Action PASS와 함께 기존 두 메뉴 검증이 완료됐다. static 오류/경고는 Runtime 0/20, Editor 0/0, Development 0/20, Release 0/24다.

**22:09 KST** 메인이 File Build And Run을 클릭했고 Hold on/Importing assets preflight를 관찰했다. 빌드 시도 증거일 뿐 실제 빌드 성공·완료·설치·기기 실행 증거가 아니다. 사용자 지시에 따라 UI 조작과 완료 감시를 중단했다.

실제 경기와 타격 육안 수용·full migration은 OPEN이다. 이전 Root PASS·빌드 시도만는 중간 인계 이력이며 별도 Blue 프리팹 변경 관찰은 원인 미확인 상태로 보존한다.
## 6. 최신 paired 경기 분석 — 2026-10-04 23:06 KST

메인 최종 검토 인계 기준이다. Editor **22:31–22:34**와 device **22_56 capture**에서 paired session `192c9a086aacb92bbfc37bc1513799fd01cddc5c44c3425cfd9b37337534711d`의 **Editor Host / Android Client** 경기만 분리했다. capture 안의 이전 18시 기록은 이번 수치에 합산하지 않는다.

| 관측 경계 | 이번 경기 결과 |
|---|---|
| 생산·Client 생성 | Pistoleer만 235개, Blue119 / Red116 |
| Ready·commit·schedule | 각 1302건 |
| 결과 | Host Results1225 / Client accepted1225, failure·reject0 |
| 필수 표현 | 양쪽 expectedVisual / emits1020/1020, failure0 |
| Host 이동 | 220148 frames, gate·adapter·writer·stationaryWalk·fatal0 |
| Client 복제 | 1655 samples, invalid·conflict·entrygap·order0 |
| 최신 ERROR·FATAL | 양쪽0 |
| 로컬 ROOT | 양쪽 INCOMPLETE, errors0; representativeTypes<2(Pistoleer만)가 원인, endpoint Host52 / Client48 |

ROOT tracking cap64는 불일치가 아니라 관측 한계이며 전체235개의 ROOT coverage를 뜻하지 않는다. Client 초기화 지연235건은 완료235건과 전부 대응하고 missing0이다. 별도 기존 GameObject 경고282건은 fatal이나 피해 손실 증거로 분류하지 않는다.

첫 근거는 Host unit0 / seq1 / rev3의 Ready **22:32:12.172**, commit **22:32:12.189**, serverTime **72.742710**, 같은 schedule이다. 메인 인계 raw anchor는 Host **44509–44523행**, Client **165474–165545행**이다. 이 단일 회차 근거와 집계 일치는 전체 exact-key join 검증 선언이 아니다.

**판정:** focused 로그상 긍정이며 정확 타격·주기 및 육안 수용은 OPEN이다. 이번 로그에 marker/실제 Impact 상세가 없어 실제0.8초 offset과2초 주기를 계산할 수 없다. 저장 config/clip/명시 연결의 정합성을 실측 exact timing PASS로 바꾸지 않는다. 사용자는 최신 육안 PASS를 말하지 않았으며 full migration·전체 완료·분류 승격은 선언하지 않는다. 앞선22:09 빌드 클릭은 여전히 preflight 관찰만이며 이번 경기 존재로 그 클릭의 완료를 소급 확정하지 않는다.

사용자는 Blue 프리팹을 이벤트 확인용으로 열었고 의도적인 설정 변경은 없었다고 확인했다. §4의 B2B…→5319…/22:07:10/40087 bytes 관찰은 그대로 보존하되 원인은 미확인이다. 이 진술과 해시 관찰을 함께 기록하며 새로운 결함이나 사용자/preview 원인으로 단정하지 않고 복원하지 않는다.

## 7. 타이밍 진단 보완 초안 — 2026-10-04 23:59 KST

사용자는 관측 누락 때문에 다시 빌드·시험을 반복하는 일이 없도록 보완 후 재빌드 시도를 요청했다. **2026-10-05 00:01 KST 후속 승인:** 이 기존 Task를 사용하며 다른 Task는 만들지 않는다. Sagan의 기존6파일 진단 보완·저장 로그용 읽기 전용 Tool 파서1개 제안을 반영했다. 상세 계약은 Plan §9이며 문서 메인 review/코드 GO 전에는 착수하지 않는다. parse gate/static/menu/build 결과는 아직 없다.

이번 실패는 정적 production 정합성 guard를 넣었지만 요청한 실제 타이밍 분석에 필요한 production marker/실제 Impact 관측을 준비하지 않은 것이다. §6의 결과·표현 집계는 긍정이나 실제0.8초 offset·2초 주기를 계산할 수 없어 사용자에게 재빌드·재시험 부담을 만들었다. 이는 관측 준비 실패이지 이번 경기 피해/표현 실패의 증거가 아니다. [전용 실패 기록](../../../../../../.claude/mistakes/unit-action-correction.md)에 원인·재발 방지 계약을 남긴다.

관련 Units 규칙17–22와 NET-AUTH-001/002·NET-PRESENT-001/002의 경계를 유지한다. 로컬 Animation Event, 서버 예약, 실제 권위 Impact, 결과 수신/표현은 다른 사건이며 같은 회차와 유효 시계 정보로 구분해 분석해야 한다. 연속 Attack anchor 때문에 commit+고정0.8초만으로 다음 marker occurrence를 가정하지 않는다. 피해·HP·취소·기존 표현 경로 및 MigrationRequired는 변경하지 않는다.

재빌드 전 계약은 **실제 production 원시 이벤트 → 기존 형식의 로그 → 분석 파서 유효성** 확인이다. Host/Client 양 역할에서 immutable full key·clock domains/calibration·absolute due·continuous anchor·marker admission·실제 TakeDamage 전/후·canonical result 수신·VFX attempt/return·tracer skip/retire를 구분한다. EDITOR/Development bounded 단계·first sample만 기록하며 full-line UTF-8 cap/drop와 complete/incomplete/unknown을 명시한다. production formatter 출력 fixture를 Temp에서 CLI 파서로 검증하고 필수 필드 누락·모호한 결합·미관측 hook을 빌드 전에 보완한다. commit+0.8 추정·임의0.2초 허용치·보정 없는 peer clock 차감은 금지한다. 이 사전 gate는 실기·육안 PASS를 대신하지 않는다.

## 8. 진단 구현·새 검증·빌드 시도 인계 — 2026-10-05

메인 review 및 코드 GO 후 진단 보완이 구현되었다. Application 계층 helper를 사용해 Infrastructure 역참조를 피하고, 기존 실제 writer 전/후의 local-only 기록·schedule/due/overshoot/cancel·canonical result 발행/수신·scoped command/marker admission·VFX attempt/return·tracer skip/retire를 연결했다. same-host dispatch anchor는 추정값과 bracket/residual을 함께 다루며 실제 writer의 서버 시각으로 둔갑시키지 않는다. firstRenderedStart·연속 재생 interval·Impact/receipt/presentation delay를 구분한다. commit+0.8 추정, 보정 없는 peer clock 차감, 임의0.2초 또는 다른 숫자의 timing PASS 기준을 도입하지 않았다.

진단은 EDITOR/Development bounded 사건 단계·first sample 관측이다. 팀별32, 총64 **attack scope** 표본이며 64유닛/전체 경기 coverage가 아니다. sampleExcluded·unclassifiedScoped·실제 omitted와 format/drop/completeness unknown을 구분한다. full-line UTF-8 cap을 넘는 기록은 명시적 formatIncomplete로 처리하며 정상0이나 완전한 증거로 해석하지 않는다. 메인 요청에 따라 schema·first anchor·quota/mixed 처리를 보완한 최종본이다.

새 static Runtime/Editor/Development/Release errors **0/0/0/0**, warnings **20/0/20/24**. 메인은 메뉴 실행 전에 production formatter 출력에 대해 CLI `--fixtures Temp/pistoleer-timing-fixtures`를 독립 실행해 **16개 case·Host/Client 양 역할, exit0**을 확인했다(normal/late/cancel/missing/duplicates/truncated/regression/arrival/drop/schema/terminal/format/quota/mixed/miss/sourceonly). 메뉴는 같은16개를 재생성했다. 이는 실제 네트워크 production hook 실행 coverage나 실제0.8초 offset·2초 주기 PASS가 아니다.

2026-10-05 **00:45:26 KST** 새 컴파일 소스의 실제 Unit Action 메뉴 UI PASS 및 새 Editor 로그의 overall/production/C2/C3/AUTH-PRESENT PASS를 메인이 확인했다. **00:48:23 KST** 실제 Root 메뉴의 console/statusbar `UAS-ROOT-CROSS-AUDIT self-validation PASS`를 확인했다. Root 추가 Editor raw-log 읽기는 usage/auto-review 실패로 실행되지 않았으므로 raw-log 확인을 주장하지 않는다.

사용자 continue 뒤 메인은 File → Build And Run을 **한 번** 클릭하고 BuildingPlayer/BuildPreProcess를 관찰했다. 사용자 지시에 따라 모든 UI·빌드 로그 감시를 중단했다. 완료·설치·새 실기는 미확인으로, 빌드 성공을 선언하지 않는다. 메인이 독립 비교한 Attack.anim·controller·Blue/Red prefab·config·Game.scene 총6파일의 해시는 이번 진단 보완 시작 대비 유지되었다. VFX는 구현 manifest상 미수정이며 VFX 에셋 해시 baseline 비교는 수행하지 않았다. 권위 payload·gameplay 정책은 변경하지 않았다. 앞선 Blue 해시 관찰과 사용자 이벤트 확인/의도적 변경 없음 진술은 그대로 별도 이력으로 보존한다. 이전 경기의 focused 로그상 긍정 판정도 보존하며 정확 타이밍·육안·full migration/분류 승격은 계속 OPEN이다.

실제 변경 manifest와 Temp 검증 산출물 구분은 Plan §10을 따른다. §7의 승인 대기/결과 없음은 당시 초안 이력이며 이 후속 인계가 현재 상태를 갱신한다.

## 9. 역할교대 실기 분석 — 2026-10-05 03:46–03:49 KST

메인이 검토한 실제 raw 로그 인계를 기록한다. Android Host / Editor Client shared session `103e57edb569d8f10352b0d09fa9b2a9cd1bb593a0e9cf888c6a5df5396789eb`. 원본은 `Assets/_Project/Docs/_Logs/2026-10-05/03_55_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-10-05/RuntimeLog.txt`이다. 앞선 Editor Host 경기와 합쳐 하나의 run으로 계산하지 않는다.

### 9.1 결과 전달과 표본 범위

Host results878 = Client accepted878, reject/failure0. coordinator ready/released878, pending/failure0; expectedVisual/emits728/728, transport/emitter failure0. schedule/commit924. 이 집계는 결과 전달 증거이지 실제 타격 시점이나 픽셀 단위 HP/VFX 표시 PASS가 아니다.

각 역할64 attack scope(Blue32/Red32), omitted/formatFailure0이며 64유닛/전체 경기 coverage가 아니다. immutable session+instance+seq+hit로64 scope가 일치한다. canonical result payload63개는 revision/victim/kind/effect/ordinal/outcome/amount/hp/impact까지 exact match:58 HitApplied·5 Miss, 나머지1 scope는 death cancel이다. 전 경기878개 전체 payload join을 선언하지 않는다.

Host dispatch63개 lateness는 min약0 / median16.624ms / p95 33.189ms / max65.951ms, early0. 실제 Host writer58개 전/후가 관측됐다. VFXStarted는 Host63 / Client61, 각각 systems1·active1이다. cold-pool 실행이나 화면 픽셀 관측 증거는 아니다.

### 9.2 살아 있는 Client scope 2회의 marker/VFX 연결 공백

sampled instance1 seq6·instance9 seq1은 Client result receive1850/1854행, Decision1899/1900행이 있지만 해당 scope의 RawMarker/VfxAttempt는 없고 retire1977/1981행에 닫힌다. Client Command1817행 normalized5.499는 이번 loop marker5.4 뒤이며, 1820행 normalized0.442729는 marker0.4 뒤다. 두 scope 모두 다음 loop marker 전에 retire됐다. instance9의 다음 marker는 normalized **1.4**이며2.4가 아니다. 이는 앞선1개 death cancel과 별개인 LIVE scope 관측이다. 정상 취소나 피해 손실로 분류하지 않는다.

**메인 검토 programmer 인과 결론:** committed scope가 현재 loop marker 뒤에 arm되고 다음 marker 전에 retire되는 패턴과 강하게 부합한다. ScopeCommand는 old lease를 닫고 rewind 없이 new lease를 arm하며, lease count만 관리하고 Animator phase를 반영하지 않는다. OnAttackHit는 lease consume을 요구한다(source anchor: `UnitAttackPresentationPolicy.cs:122`, `UnitView.cs:7256`). 따라서 **scope-level presentation 연결 결함의 강한 근거**이나 exact old-marker/prior/provisional 처리와 정확한 close 호출 원인은 완전히 관측되지 않았다. 화면 전체에 VFX가 전혀 없었다는 증명이나 완전한 runtime 인과 증명으로 확대하지 않는다. 이번 턴 실제 수정은 없다.

### 9.3 시간 측정 경계

Client 같은 local clock의 coordinator Decision(Ready; 화면 HP 표출 아님) minus VfxAttempt61개는 -0.86405..+0.849633초, median+0.163889초다. instance10/11 seq1–4에서 음수 offset이 지속된다. Host near-writer VfxAttempt offset은 median+0.008816초, range -0.03511..+0.02310초다. raw Attack marker interval은 Host43개1.944..2.029초 / Client42개1.682..2.045초. 관측된 부분 표본의 분포이며 실제0.8초 offset·정확2초 주기 또는 전체 timing PASS를 선언하지 않는다.

### 9.4 파서 오류와 fixture 부족 — gameplay 실패와 분리

파서 출력 parsed1602 / schema15 / regression11 / paired18 / unpaired92 / unknown64는 그대로 기록하되 gameplay 실패 수가 아니다. schema15는 합법적인 Miss의 hp=-1을 blanket negative 규칙이 거부한 것이다. Register의 Host action revision과 Client presentation revision은 다른 축인데 parser KEY에 revision을 포함해 false unpaired가 생겼다. 실제 canonical result revision은 일치한다.

NGO estimate/presentation-derived anchor의 rebase로 잡힌 regression11을 raw local clock 역행이나 네트워크1.33초 지연의 증거로 바꾸지 않는다. 기존16 formatter fixture PASS는 사실로 보존하지만 이 production 의미 사례를 충분히 포함하지 못했다. 이는 우리 fixture/분석 준비의 coverage 부족이며 사용자 잘못이 아니다. 파서/fixture 보완은 필요한 후속 검토이지 이번 턴 구현 완료가 아니다.

양쪽 로컬 ROOT는 focused Pistoleer1 type으로 representative coverage<2여서 INCOMPLETE, errors0이다. 25종 생산을 강제하거나 Root CrossAudit 완료로 해석하지 않는다. **판정:** outcome delivery 긍정, sampled Client 연결 결함과 진단 파서 결함 OPEN; 정확 타이밍·육안·migration OPEN. 분석 문서만 수정하며 source/assets/Git/UI/build·새 Task/TC/QA 변경은 없다.

## 10. 후속 교정 승인과 제안 검토 경계 — 2026-10-05

사용자는 기존 Task 갱신 뒤 phase 정합성·committed scope 연결의 두 부분 교정 구현과 빌드 시작 시도를 요청했다. 메인은 programmer에게 구체 제안과 규칙 감사를 먼저 read-only로 맡겼다. **방향과 후속 실행은 승인됐지만, 이 문서 준비 시점에는 메인이 검토한 구체 제안·파일 manifest가 아직 전달되지 않았다.** 따라서 구현 방식·새 코드·검증 PASS를 선결론으로 기록하지 않는다. §9와 Plan §11은 교정 전 실기 분석 이력이다.

현행 Units의 U-ATK-TIMELINE 및 규칙17~22, Sync의 NET-PRESENT-001~004·NET-TIME·NET-CANCEL, LogRules의 런타임 분류·형식·strip·sink·실기 캡처·금지사항을 기준으로 준비한다. commit 전 marker를 소급 승인하거나 빈 이벤트를 재사용하지 않고, commit에서 Attack을 restart/rewind하지 않는다. 연속 재생 중 선택한 **commit 뒤 승인 occurrence**와 서버 per-hit 절대 due·표현 scope는 같은 타격을 가리켜야 한다. 늦게 관측된 승인 후 occurrence와 승인 전 이미 지난 marker는 구분한다.

두 LIVE 누락은 current-loop marker 뒤 arm→next-loop marker 전 retire라는 scope/연속 phase 연결 결함과 강하게 부합한다. 기존 lease-count만으로 Animator phase를 보장할 수 없다는 source 분석은 유지하지만, 이전/provisional marker의 실제 처분과 정확한 close 호출 원인은 완전히 관측되지 않았다. instance9의 다음 normalized marker는1.4다. 화면에서 VFX가 전혀 없었다거나 피해가 누락됐다고 확대하지 않는다.

구체 제안에는 생산 경로에서 종료 호출 지점/사유, per-hit due, 객체·회차/재생 epoch, sampled phase, lease admission, output dedupe와 overflow를 확인할 로그 및 parser gate가 필요하다. clock domain·Host action/Client presentation revision축·합법 Miss sentinel을 보존한다. 취소·공격자 사망·게임 종료·기존 Legacy/타입별 예외와 single-writer/single-emitter는 유지한다. 필요한 정책 변경이나 규칙 충돌은 변경 전에 사용자에게 보고해야 한다. 이 단계 직접 변경은 기존 Research/Plan뿐이며 구현·빌드 시도는 아직 실행 결과가 없다. 정확 타이밍·육안·MigrationRequired/full migration은 OPEN이다.

**초기 제안 수신 후 갱신:** programmer 초기 제안은 메인에 도착했지만 full rule audit와 활성 연속 phase의 production 교정 전략은 아직 보완 중이며 **code GO는 없다**. shared epoch/per-hit due의 원자 전달과 최초 provisional phase 정렬은 검토 중인 방향이지 확정 설계가 아니다. 최초 정렬과 활성 재생 중 phase/scope 연결을 두 단계로 각각 검증해야 하며, 같은 epoch의 commit rewind·과거 source marker 재방출·임의 delay 추가로 통과시키지 않는다. 최종 제안·파일 manifest 확정 전에는 구현 계획이나 PASS로 승격하지 않는다.

### 10.1 최종 specialist 설계 메인 검토 완료 — 구현 준비 인계

위 제안 미수신/미완·GO 대기 문구는 이전 준비 단계 이력이다. 최종 specialist 설계를 메인이 검토했고 사용자에게 이미 승인받은 교정·빌드 시도 범위 안에서 **Plan §12.5~§12.8의 구체 계획을 확정**한다. 메인 code GO는 이 written plan 검토 뒤 발급하는 순서다. 현재 기록은 구현 준비이며 실제 RED→GREEN·컴파일·메뉴·빌드 결과가 아니다. 이전 §9.1의 미관측 close 원인을 새 계획으로 소급 확정하지 않는다.

Host provisional 실제 적용 뒤 기존 epoch E를 backdate 없이 확보하고, 같은 E의 bind 성공을 Host/Client에 원자 payload로 전달한다. per-hit occurrence/due는 실제 committed Schedule에서 복사하며 commit+0.8초로 재계산하지 않는다. 최초 Client provisional은 fresh NGO 시간-delay-E의 초 단위 phase로 진입하고, 활성 같은 epoch는 commit restart 없이 native Attack/next-state phase를 매 Update의 bounded forward-only 표현 제어로 따른다. 과거 source marker는 재방출하지 않으며 이미 늦은 source occurrence는 명시적 skip이다. 이 source skip은 확정 victim result catch-up과 별개다.

기존 빈 AnimationEventRelay만으로 다중 hit/전환 callback을 구분할 수 없으므로 event metadata 연결이 필요하다. 새 wire payload는 기존 resolver의 SchemaRevision/hash gate와 함께 old/new mismatch 거부를 검증한다. **계획 manifest는 기존11파일**이며 상세 경로·역할은 Plan §12.5의 단일 목록을 따른다. 신규 파일/validator/에셋/TC/QA/hit writer는 만들지 않는다. 추가 기존 파일이 필요해지면 메인에게 먼저 보고한다.

64 상세 scope 표본은 역사적 분모로 보존하되 bounded ACTIVE lifecycle tracker와 quota 밖 mandatory critical 사건으로 생산 누락·종료 이유를 관측한다. raw NGO/local clock·원본 revision축을 보존하고 explicit alias join과 canonical payload strict 비교를 구분한다. trace-shaped controlled-clock fixture는 생산 callsite를 검증하되 이전 capture에 없던 clock을 실제 관측값처럼 만들지 않는다. 정책 변경 없이 Units Timeline/17~22 및 NET-AUTH/ROOT/TIME/PRESENT/CANCEL을 유지한다. Coordinator clock을 조용히 교체하지 않으며 Ready/pixel 일치·정확 타이밍·전체 migration은 새 실기까지 OPEN이다.

### 10.2 구현 중단 뒤 재개 — 2026-10-07

메인 인계 기준으로10-05에 Plan §12.5~§12.8 검토 뒤 **CODE GO가 발급됐고**, programmer가 부분 patch 중 usage limit(`try again 8:07AM`)으로 중단됐다. 사용자의10-07 “이어서 진행해”에 따라 메인이 Sagan의 기존 작업을 재개했다. 승인된 기존11파일 계획과 기존 Task를 그대로 잇고 이전 기록·부분 변경을 보존한다. 부분 코드는 구현 완료/빌드 준비 완료가 아니며 최종 실제 수정 manifest는 아직 대기다.

메인 중간 검토는 stale inactive close의 identity 보호, queued Apply와 실제 성공의 구분 및 eligible native fallback, 좁은 SourceOnly, native event gate를 cursor 변경보다 먼저 검증하는 순서, baseline/lifetime, critical quota 밖 로그와 parser 의미 검증을 전달했다. 이는 계속 작업 중인 검토 요구이며 모두 반영·검증됐다는 결과가 아니다. **이번 교정의 새 컴파일·formatter→CLI·Unit Action/Root 메뉴·빌드 시도는 아직 없다.** 이전 PASS/빌드 관찰을 재사용하지 않는다. programmer 작업과 최종 근거 인계를 기다리며 runtime/정확 타이밍·육안·MigrationRequired/full migration은 OPEN이다. 이번 직접 변경은 기존 Research/Plan 두 문서뿐이다.

### 10.3 fixture 설정 실수와 검증 진행 — 2026-10-07 중간 인계

> **후속 실제 근거 — 아래 대기 문구는 당시 이력:** 메인은13:57:02 Unity Unit Action의 A1/C2/C3 v12·production·Bloom PASS,13:58:29 Root self-validation PASS를 확인했다. Console errors0/warnings26은 compile20+negative fixture6이다. programmer의 추가보완 전 최종4경로 보고는 Runtime13:56:49 errors0/warnings20, Development13:56:51 0/20, Release13:56:50 0/24, Editor13:57:13 0/0이며 CLI21 PASS도 보고됐다. 메인 독립 CLI21 확인은 별도 근거지만 같은 fixture 합성 기반으로 runtime 수용은 아니다.
>
> **검증 범위 미완/빌드 미시도:** specialist 최종 인계에서 승인 Plan §12.8의 native multihit/crossfade coverage 미완을 밝혀 메인이 기존 gate의 추가 보완을 요청했다. 위 PASS·컴파일은 그 추가보완 전 소스 이력이며 승인된 전체 production gate 충족/최종 완료를 뜻하지 않는다. 소스 동결 인계도 당시 단계다. 보완 후 새 컴파일·CLI·메뉴 재검증을 받아 최종 기록을 갱신한다. 이번 빌드는 아직 미시도, runtime/정확 타이밍·육안·MigrationRequired/full migration OPEN 유지.

메인 실제 관찰상13:39:12 Unity Unit Action은 `Ready retry must apply exactly once`로 **FAILED**했다. fixture가 UnitData 생성자의 HP/attack/range/detect 인자를 cooldown 설정으로 착각해 fallback1초를 사용했고, production Attack clip2초와 달라 eligibility guard가 거부했다. fixture 준비 실수이며 이 실패만으로 production retry 결함을 확정하지 않는다. 교정은 fixture에서 실제 UnitFactory의 `ApplyAttackClipCooldown`·`GetAttackClipLength`·`GetHitFrameTimes`를 사용하도록 한 것으로, 생산 guard/tolerance를 완화하지 않았다. 같은 기존 unit-action-correction incident에 후속 기록한다.

메인은13:54:58 Unity 재컴파일 errors0/warnings20을 확인한 뒤 새 Unit Action을 실행 중이며 **그 결과는 아직 없다**. 메인이 CLI21cases PASS를 독립 확인했지만 같은 formatter fixture 합성 기반으로 runtime acceptance가 아니다. programmer 인계상 현재 소스11파일은 동결됐고 최종4경로 컴파일 보고는 후속 대기다. 동결·Editor compile·CLI PASS를 최종 전체 검증/빌드 준비 완료로 확대하지 않는다. Root 메뉴·이번 빌드 결과 및 새 실기 수용은 아직 미확보, 최종검증/빌드 결과는 후속 인계 뒤에만 추가한다. §10.2의 미실행 문구는 이전 시점 이력이며 runtime/정확 타이밍·육안·MigrationRequired/full migration OPEN을 유지한다.

### 10.4 최종 구현·빌드 전 검증과 빌드 시도 — 2026-10-07

메인·programmer 최종 인계 기준으로 **승인된 기존11파일 구현/동결 및 implementation prebuild acceptance 완료**다. §10.2~§10.3의 부분 patch/검증 대기/native coverage 미완은 당시 이력이며 삭제하지 않는다. Plan §12.5 목록과 실제 수정11파일은 동일하다. Host 기존 E의 원자 bind, fresh NGO 기반 첫 phase/forward-only native follow, native occurrence admission-before-cursor, lifetime/baseline/종료 보호 및 ACTIVE/critical 진단·parser 의미 교정을 구현했다. 서버 권위 피해/HP writer·타입별 Legacy 정책과 에셋은 이번 교정에서 변경하지 않았다.

마지막 보완은 기존 RunUnitActionSelfValidation/UnitView/PistoleerTimingDiagnostics/parser4파일이다. 실제 Lion 다중 marker에서 hit0 baseline 뒤 정확 hit1, 두 marker 각각1회 소비, 같은 native clip/hash/loop, transition outgoing callback 거부와 incoming 정상 소비를 검사했다. duplicate-time clip의 prepublish Legacy qualification, evicted known scope의 coverageUnknown, strict END numeric도 보강했다. 이는 실제 controller/relay/production VFX callsite 검증이지 입자 pixel 검증이 아니다. 이전 §12.8 coverage 미완을 숨기거나 보완 전 PASS를 재사용하지 않고 최종 소스로 재검증했다.

| 최종 근거(KST) | 결과·경계 |
|---|---|
| programmer Runtime14:03:54 / Development14:03:56 / Release14:03:57 / Editor14:04:28 | errors0 각각; warnings20/20/24/0 |
| CLI23cases | programmer PASS + 메인 독립 PASS; formatter fixture 기반으로 새 실기 수용 아님 |
| 메인 실제 Unity compile14:05:14 | errors0/warnings20 |
| 실제 Unit Action14:06:49 | A1/C2/C3v12/production/Bloom + native Pistol/Lion/diagnostics CLI 검사 포함 PASS |
| 실제 Root self-validation14:07:44 | PASS; 새 경기 formal CrossAudit 아님 |
| 최종 Console | errors0/warnings27: compile20 + validation 중7 추가. 추가7의 세부 전체 분류는 단정하지 않는다 |
| 메인6 asset hash 비교 | Pistol Attack clip/controller/Blue·Red/Game/Stats 전부 불변. VFX hash 비교 근거는 없음 |
| 과거 runtime 재parse | parsed1617/errors0/rolejoin64/canonical63/complete126/incomplete2; clock rebase NGO9 + presentation2. 새 runtime도 과거 LIVE Client2건 해결 증거도 아님 |

**빌드 시도:** 마지막 Root PASS 뒤 메인이 File → Build And Run을1회 실행했다. action 직후 refresh에서 **Building Player / Backup & unload** 창을 관측했다. 이후 Unity UI/Editor log/build progress/completion/installation/runtime 감시는 전부 중단했다. 빌드 시작 시도까지만 확인한 것이며 빌드 완료/PASS·설치·실행은 확인하지 않았다. 완료 확인은 사용자 소관이다.

**현재 판정:** 구현·빌드 전 자동 검증 수용과 사용자 실기 수용은 별개다. 새 exact-scope Pistoleer VFX/타이밍·육안 및 full migration은 OPEN/MigrationRequired 유지다. 과거 runtime 실패2건과 미관측 원인은 새 실기 증거 없이 CLOSED로 바꾸지 않는다. 직접 문서 목록은 Plan §12.11에 있고 checker는 메인이 최종본 뒤 실행한다.

**2026-10-07 도구 산출 후속 인계:** 전체 작업 manifest에는 직접 수정10문서와 별도로 **도구 자동 수정 `.claude/agent-memory/_baseline.json` 1개**를 포함한다. 메인이 `Tools/check_docs.py --update-baseline`을 실행해 programmer3847→3852(+5)/qa1355→1361(+6), 파일 수 불변·감소 없음으로 자동 갱신했다(직접 편집 아님). 전체 checker 검사는 메인이 이어서 실행 중이며 아직0건 결과는 미확보다.

**최종 checker 인계(2026-10-07):** 위 baseline 도구 자동 갱신과 별도로 메인의 전체 `Tools/check_docs.py`는 exit0/“문제 없음”,7종 모두 이상 없음이었다(실제 검사 범위 rules10/md77/claude40; `_Tasks/`는 검사 제외). 앞선 대기는 당시 이력이며 직접10문서+도구 자동 수정 `_baseline.json`1개 manifest는 유지한다. 빌드 완료 감시는 재개하지 않았고 완료/설치·사용자 exact VFX/타이밍/full migration OPEN은 그대로다.

### 10.5 최신 역할교대 두 경기 로그 — 2026-10-07 최종 facts 인계

**메인 checker 인계(2026-10-07):** 첫 전체 검사 exit0/“문제 없음”, 실제 범위 rules10/md77/claude40·7종 문제0을 확인했다. 이는 최종 source facts 추가 전 검사이며 최종본 검증으로 재사용하지 않는다. 최종 facts는 이 Research §10.5 / Plan §12.12에 적용 완료했고, 메인이 추가 후 전체 검사를 재실행할 예정이다. 이번 마무리 추가 수정은 Research/Plan2문서만이며 다른 문서·메모리 변경은 없다.

**Sagan+메인 최종 source 검토 후속(2026-10-07; 분석만):** 아래 D번호는 위 device raw 로그의 행 anchor다. D4361 provisional E73.0102311825 → D5007 new provisional E75.0429767783; D5073 instance6 seq2는 crossFade0 / normalized0.9498909712 / speed0이다. D5079 raw75.1432346635와 duration2 기준 target≈0.000258초, actual≈1.899782초, 차이≈1.8995초다. D5536 HitApplied의 scoped admission marker/VFX는0이고 D5805 remaining1 retire가 뒤따른다. D5819 seq3는 동일 E를 유지하며 D6193 native admission이 있다. 따라서 **매 commit마다 무조건 E를 reset한 것은 아니다.** 새 provisional E와 기존 Animator phase 기준 불일치의 강한 실행 근거이며, 화면 전체 VFX 부재나 모든 회차의 보편 실패 증명은 아니다.

메인 source 검토 anchors: `NetworkCombatController.cs`3025는 dictionary에 없을 때 현재 time으로 E 생성,1392는 copy,1009는 activeThisTick 정리 시 E remove다. `UnitView.cs`827의 Host sameRev bind는 restart하지 않는다. policy의 `actual>target` 분기는 advance0이다(인계 표기 Policy15). 이 조합은 위 실행 패턴과 부합하나 **정확 E 제거/recreate 호출 원인과 매 frame hold 분기 체류 duration은 확정 불가**, 전체 근본원인 분석 OPEN이다. source 행 anchors는 이번 검토 시점 위치이지 영구 식별자가 아니다.

교정 검토 방향은 **논리 provisional 재진입과 native 재생 epoch 분리; 연속재생 유지 시 같은 E/absolute phase/occurrence 일치**다. 승인된 코드 수정이나 구현 완료가 아니라 분석상의 방향이며, 강제 매 commit restart·임의 delay/tolerance·HP writer 변경으로 우회하지 않는다. schema341의 length/anchorLength Infinity·speed0는 진단 형식 계약 불일치이며341건 피해 오류가 아니다. 둘째 경기 clock20 units는 같은1 episode의 fanout(19개 frame40818+1개 frame40819), raw -0.3233초 / hold1 관측이다. 이 기록만으로 단독 hold 지속시간은 확정하지 않는다. 결과 피해 정합 수용 / 발사표현 미완 / 정확 시각 미확정·전체 OPEN은 유지하며 이번 source 변경0이다. 메인 첫 checker 실행 중이고 이 추가 최종본 뒤 재실행 예정이므로 이번 최종 checker PASS는 아직 기록하지 않는다.

메인+Sagan의 분석 인계 중 확정 facts만 기록한다. 첫 경기 Android device Host / Editor Client session `e8f8586ef3264f2bb266dfbfe22501d334905b64abfecd0352275778b6af5a85`, 둘째 Editor Host / Android device Client session `c0e415e9eb2e046c3c0e1773515617432bf1d7f7d5903cb940b8b06932505442`를 분리했다. 두 원본 로그에서 full session ID를 확인했다. 원본: `Assets/_Project/Docs/_Logs/2026-10-07/15_16_logcat/RuntimeLog_device.txt`, `Assets/_Project/Docs/_Logs/_editor/2026-10-07/RuntimeLog.txt`. source 원인 최종 인계와 normal pause duration은 아직 미확정이다.

| 증거 범위 | 확정 관측과 한계 |
|---|---|
| canonical 피해 결과 | 첫 경기570 / 둘째595, 총1165 HostResult=ClientReceive 완전 일치; conflict/unpaired0. 결과 피해 정합 수용이며 화면 HP·발사표현 동시성 수용 아님 |
| VfxResult Started | H1 549 / C1 561 / H2 567 / C2 587, 총2264; 실패 status0. 관측된 Started 결과이며 모든 HitApplied scope의 표현 완전성이나 픽셀 검증 아님 |
| critical coverage | 네 역할 critical 누락/format/overflow0; allcriticalscopes 첫 경기612 / 둘째626(인계 분모). 상세 표본 scope와 혼동하지 않는다 |
| 정상 Host 결과와 발사 hook 공백 | HitApplied19 scope(첫 경기6 / 둘째13)에 raw Marker/VfxAttempt/VfxResult 없음. Cancel/Unavailable/SourceStop 없고 다음 revision replace로 종료. 발사표현 누락 의심으로 남기며 원인·화면 전체 VFX 부재 확정 아님 |
| parser | 전체40782 records, schema341(Animator length/anchorLength Infinity·speed0), fresh clock20; parser 전체 records unknown. 유효성 경고 수를 피해 실패 수로 치환하지 않으며 schema 오류만으로 전체 정상이라고 하지 않는다 |
| fresh clock 관측 | 둘째 경기 기기 Client 동일 시간 보정 fanout19개 frame40818 +1개 frame40819;105.306659→104.983359, -0.323300초.20개 경고를20개 독립 시간 보정 사건으로 확대하지 않는다. 원인 source 분석 OPEN |
| runtime 오류·이동/진입 | runtime [ERROR]/Exception0; MOVE ownership/gate/adapter/failureDetails0, Client attack entry order/gap0. 이번 formal CrossAudit 미실행; movement 전체·full migration PASS 아님 |

**[🔴 2026-10-07 correction — original kept: 위 parser “전체40782 records”의 정확한 단위는 parsed 로그행40782이며 attack records는2476이다. parser 전체 attack records의 unknown 경계는 유지한다.19건 종료 표기는 대부분 next revision replaced이고,2건에는 동일 scope 후반 combat-target-change 추가 기록도 있다. 이 후반 기록을 앞선 hook 공백의 취소/Unavailable/SourceStop 근거로 소급하지 않는다.]**

**메인 baseline 도구 산출 인계(2026-10-07):** 전체 manifest는 직접 수정10문서 + 도구 자동 수정 `.claude/agent-memory/_baseline.json`1개다. 메인 자동 갱신으로 programmer3852→3854(+2), QA1361→1363(+2), 파일 수 불변·감소0을 기록한다(직접 편집 아님). 이번 전체 checker 결과는 아직 후속 인계 대기이며 이전 exit0을 재사용하지 않는다. 최종 agent source facts도 대기다.

**분리 판정:** 결과 피해 정합은 위 exact canonical 범위에서 수용한다. 발사표현은 정상 HitApplied의19 hook 공백 때문에 미완/누락 의심이며, 정확 시각은 schema·fresh clock·normal pause duration 불확정 때문에 미확정이다. Agent 최종 source 원인은 후속 인계 전까지 OPEN이다. 자동 prebuild PASS·Started 실패0·critical 손실0·runtime 오류0 어느 것도 이 공백을 지우거나 전체 runtime PASS를 만들지 않는다. 사용자 exact timing/입자 pixel·육안/full migration/MigrationRequired는 OPEN 유지한다. source/assets/UI/build/Git·새 Task/TC/QA 변경0이며 과거 경기/검증·빌드 시도 기록은 보존한다.

### 10.6 사용자 승인 후속 교정 준비 — 논리 회차와 native 재생 epoch 분리 (2026-10-07)

**최종 도구 산출 인계:** 메인 baseline `--update-baseline` exit0, programmer3854→3858(+4)/QA1363→1367(+4), 파일 수20/3 불변·감소0·직접 편집0. 메인 전체 `check_docs` exit0·7종 이상 없음, 실제 범위 rules10/md77/claude40이며 _Tasks/_Logs는 제외된다. 전체 변경 manifest는 source8 + 직접 문서10 + 도구 자동 baseline1 =19개, Temp compile/formatter CLI fixture 등 검사 임시 산출은 별도다. 이0건 결과는 이번 최종 Task2 증분 전 검사이며 증분 뒤 메인이 전체 checker1회 재실행할 예정이다. 문서 검사0건은 사용자 runtime OPEN이나 Game 씬 저장 선택 대기를 해소하지 않는다.

**최신 빌드 시도20:10:51 KST — 앞선 미시도 인계의 후속:** File→Build And Run1회 클릭+즉시 snapshot에서 Game `Scene(s) Have Been Modified` 저장 확인창을 관찰했다. 저장하지 않으면 변경이 빌드에 포함되지 않는다는 안내다. 범위 밖 사용자 변경 여부 미확인 Game 씬이므로 Save/Don't Save/Cancel 모두 미선택 상태로 인계했다. 씬 수정0. Building Player 실제 시작 증거가 아니며 started/completed/PASS·설치 주장 없음. 이후 UI/log/progress 감시0, 메인도 더 확인하지 않는다.


**최종 구현·자동 검증 인계(20:07:02 KST) — 새 실기 OPEN:** specialist가 기존8source 수정 완료 및 resolver PresentationContractSchemaRevision3을 인계했다. Plan §12.13의8파일이 실제 구현 manifest다. 논리 회차/native epoch 분리, Host production re-entry 원자 first apply와 continuous/noRestart, scoped once, immutable duplicate conflict, Held closed sequence 보존을 기존 gate에서 실제 RED→GREEN 경로로 검증했다.19:40:15 의도한 RED와 첫 candidate assertion FAIL은 이전 실행 이력으로 보존한다.

새4compile errors0, 경고 Runtime20/Development20/Release24/Editor0. **[🔴 2026-10-07 correction — original kept: 앞선 Editor 로그 산출 인계와 달리 실제20:06:31–36 Editor 실행은 exit0·stdout 빈 값이며 editor-schema3.log 파일은 생성되지 않았다. 대체 로그는 없고 존재하지 않는 파일을 검증 근거로 인용하지 않는다.]** 메인 독립 compiled production formatter CLI28은 exit0 확인이다.

실제 Unity 최종 compile20:07:12 UI errors0/warnings20, Unit Action20:09:00 A1/Production/C2/C3 PASS, Root Pose20:10:11 PASS. C3 observer v12는 유지되고 resolver contract3는 독립 contract hash revision이다. 실제 UI Console errors0/warnings27; 로컬 Root 메뉴 PASS는 formal CrossAudit 실행 증거가 아니다.6개 비교 hash(Stats 포함)는 이번 전후 모두 불변이며 정확 대상 목록은 기존 hash 기록을 따른다.

**[🔴 2026-10-07 correction — original kept: 바로 위 “기존 hash 기록을 따른다”는 이전 §10.4의 Game 씬 포함 범위와 혼동될 수 있어 이번 범위를 명시한다. 이번 실제 SHA256 대상6개는 Pistoleer_Attack.anim / Pistoleer.controller / Unit_Pistoleer_Blue.prefab / Unit_Pistoleer_Red.prefab / EffectPreset_Pistoleer_Attack.asset / StatsReference.md이며6개 모두 전후 불변이다. Game 씬은 이번 hash 비교 대상이 아니다. 이전 Game hash 이력은 그대로 보존한다.]**

이 인계 시점 빌드는 아직 누르지 않았고 메인이 즉시 시작 시도 예정이며 결과는 후속 기록한다. 완료/설치/실기 수용 주장은 없다. source는 실제 기존8파일 수정이며 이번 문서 담당의 추가 source/settings/assets/Git 변경0, 신규 Task/TC/QA0. 새 runtime 수용·정확 시각·pixel·full migration은 OPEN. 분석 당시 source0 기록은 당시 범위 이력이지 이번8source 완료를 부정하는 현재 상태가 아니다. checker/baseline 갱신은 메인 도구 담당이다.


**Specialist 호환성 gate 확정 — 계획 manifest8파일로 증분:** 추가 기존 파일 `Assets/_Project/Scripts/Infrastructure/Network/UnitAttackShadowProfileResolver.cs`의 `PresentationContractSchemaRevision`을2→3으로 올려야 한다. 원자 phase bind/first native apply 교정 계약이 다른 구/신 peer가 같은 contract hash로 접속하는 것을 막기 위한 필수 호환성 gate 동기화이며 gameplay/asset 변경이 아니다. 이 인계 시점에는 승인7파일 외 해당8번째 source patch는 아직 없다. 앞선7파일 확정 이력은 보존하며 최신 계획은8파일이다.

**중간 실행·fixture 강화 인계:** 실제 RED19:40:15는 same-revision native first apply 누락 재현이다. Unity19:59:54 compile errors0/warnings20 뒤 첫 candidate20:00경 Unit Action `NativeOccurrenceBoundary` line324의 `!pending.Active` assertion이 FAIL했다. 이는 논리 close 후 nativeActive 유지와 Scope 폐기/ModeSuppressed라는 새 계약을 old assertion과 혼동한 것으로, 최초 RED를 setup 실패로 바꾸는 근거가 아니다. programmer20:02:14 fixture 강화는 old pending Epoch1/revision2/Scope invalid/Hits null/ModeSuppressed, applied revision2, closed instance41/sequence8을 관찰하여 동일 closed sequence8은 절대 reopen하지 않는 불변식을 유지한다. pending의 logical suppression과 native playback lifetime을 따로 검증한다.

**잠정 검증 경계:**7파일 단계4compile은 errors0, 경고는 Runtime/Development/Release/Editor 순20/20/24/0으로 인계되었고 compiled production formatter CLI28(기존23+신규5)이 PASS했다. 이는8번째 resolver 변경 전 잠정 증거다. 계약 bump 후 fresh 최종 실행이 필요하며 최종 Unity 메뉴 PASS·구현 전체 PASS·빌드 완료를 주장하지 않는다. 최종 facts는 후속 인계로 기록한다.

**실제 기존 Unity Unit Action 메뉴 RED 재현 인계:** `Host reentry RED new epoch first apply must align old native playback; same-revision preapply currently skips it` 실패를 확인했다(인계 anchor: fixture97 / Run598). 승인한 Host re-entry 회귀의 의도한 RED이며 fixture setup 실패가 아니다. 이 실행은 same-revision preapply가 새 epoch의 첫 native 정렬 적용을 건너뛰는 재현 근거다. 프로그래머에게 교정 재개가 지시되었으며, GREEN·구현 PASS·후속 compile/menu 검증·빌드 완료 증거는 아직 없다. 최종 facts는 후속 인계로 추가한다.

**Specialist 세부 계획 확정 인계:** 예상 manifest의 동일7파일로 확정한다. native 적격 Host의 기존 선행 Start를 대체하되 검증 전 old 로직은 주석 비활성화하여 보존한다. owner/lifetime·Root·priority·metadata·clock 사전검증 → E/phase 준비 → phase bind와 first native apply의 원자 경계 → 실제 성공 후 publish/receipt 순서다. queued는 성공이 아니며 중복 fallback은 금지한다. `firstApplied(lifetime,nativeEpoch)`는 revision과 별개다. same-epoch commit은 scope lease만 연결하고, true exit/re-entry는 새 first sync를 적용하며 SourceOnly의 terminal held/closed epoch는 보존한다.

hold/resume은 episode ID로 구분한다. `clip.length`, `state.length`, speed/state rate를 별도 기록하며 speed0-derived Infinity는 명시적인 paused 경우에만 분류하고 비정상 Infinity는 오류로 유지한다. canonical 결과 정합과 source completeness는 독립 판정한다. 회귀는 약1.9초 phase 불일치 trace를 실제 Host re-entry production 체인으로 RED→GREEN 재현하고 실제 controller/clip/relay/VFX까지 연결한다. private fixture 값만 얕게 대입한 검증은 대체 근거가 아니다. Stats/에셋/damage/due/delay/tolerance 불변이며, 이 인계는 구현 설계 확정이지 근본원인 전체 증명이나 runtime PASS가 아니다.

이번 교정의 목적은 새로운 논리 공격 회차를 준비하는 일을 실제 Animator 재생이 처음 시작되는 일과 구분하여, 계속 재생 중인 공격의 시간 기준이 새 provisional 진입 때문에 어긋나지 않게 하는 것이다. 타겟 교체·연속 commit이 native Attack을 계속 유지하면 같은 재생 epoch E와 absolute phase/occurrence를 유지한다. 실제 state exit/re-entry에는 새 E와 Host/Client firstStart를 원자적으로 적용한다. 매 commit 강제 restart는 승인된 교정 방향이 아니다.

규칙 근거는 `GameSystemRules_UnitCombatSynchronization.md`의 서버 권위(NET-AUTH), ROOT, TIME, PRESENT, CANCEL 계약과 `GameSystemRules_Units.md`의 `U-ATK-TIMELINE` 및 규칙17~22다. authoritative damage/HP writer, Root, Stats, production clip/marker·에셋, 기존 delay/tolerance는 바꾸지 않는다. 기존19 Host 정상 결과의 native/VFX hook 공백은 표현 미완 근거로 유지하며 피해 정합 수용과 정확 시각 미확정을 별도로 유지한다.

**강한 단서와 미확정 경계:** 메인 source 검토의 BeginProvisional `TryApplyHost → 새 E 생성 → Publish same-revision bind` 순서는 Host가 새 E에 대응하는 첫 native 재생을 하지 못할 가능성을 보인다. `activeThisTick` 정리(기존 source anchor1009)의 논리 종료와 실제 native 재생 종료를 구별해야 한다. 이는 specialist가 정확한 호출 원인·수명 경계를 정밀화할 후속 연결이며 전체 근본원인 확정이 아니다. 매 commit마다 E가 무조건 reset된다는 주장도 하지 않는다.

후속 구현 계획 manifest는 Plan §12.13의 기존7파일이며 specialist 확정 전 예상 목록이다. 로그는 E 생성/유지/삭제 사유, actual/target phase, hold/resume, native approval, remaining, expiration/drop을 연결한다. 원본 `clip.length`와 speed0에서 계산된 derived Infinity를 분리하여 진단 형식 계약 오류를 피해 오류로 오인하지 않게 한다. 진단 로그는 새 실기 증거 수집까지 유지하고 삭제·비활성화 시 사유를 남긴다.

기존 native fixture에서 실제 Host preapply→new E→same-revision bind 체인을 먼저 RED로 재현하고 교정 후 GREEN을 확인한다. true Walk re-entry, continuous commit/target switch, Frozen, SourceOnly, late baseline, duplicate/lifetime 회귀도 같은 기존 gate에 포함한다. 이후 새4compile·formatter CLI·Unity Unit Action/Root Pose 두 메뉴를 검증하고, 통과 뒤 빌드는 시작만 시도한다. 과거 PASS를 새 교정의 검증으로 재사용하지 않는다. 기존 로직을 제거하기 전 주석 비활성화 정책을 적용하고 원이력을 보존한다. 현재는 사용자 승인 계획 기록이며 구현·새 검증·빌드 결과는 아직 없다. 코드/에셋 변경0, 신규 Task/TC/QA0; specialist 후속 사실로 이 절과 Plan §12.13을 정밀화한다.
