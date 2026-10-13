# Hexiege - 프로젝트 진행 현황

이 문서는 현재 구현·검증 상태를 요약한다. 다음 작업은 [ROADMAP](ROADMAP.md), 유닛별 수용·이행 상태는 [전투 감사표](Assets/UnitCombatAssetMatrix.md)를 기준으로 한다.

날짜별 인계·fixture·native·빌드 시도 원문은 [구현·검증 상세 이력](History/ImplementationVerificationHistory.md#status-handoff)에 보존한다. native PASS와 빌드 클릭은 실기 시각 수용이나 빌드 완료가 아니다.

### 현행 진행 경계 (2026-10-13 문서 정리)

- 사용자 추가 결정에 따라 유닛 스탯·공격 시간·건물 현재 기능 문서를 실제 구현값 기준으로 정정했다. 밸런싱은 추후이며, 생산 클립 조사와 기존 사용자 공격 교정 수용을 새 전체 migration·실기 정밀 검증으로 확대하지 않는다. EagleArcher 클립 열거 순서의 제한된 확인 경계는 [StatsReference](StatsReference.md#생산-타임라인-구현-근거), AI 시나리오 재논의는 [ROADMAP](ROADMAP.md#ai-시나리오-재논의-사용자-추가-결정-2026-10-13)을 따른다.

- RhinoBreaker·QuakeSpirit·BattleAxe·DustSpirit·LittleKnight·EagleArcher의 사용자 개별 수용을 유지한다. Mushroom/Bloom 새 VFX·HumanTower 표현 후속, Tank/Cannon 세부 조정·Torrent 복구 회귀·공통 랠리 보류는 감사표의 현행 목록으로 구분한다. LittleKnight 다음 추천은 철회됐으며 MigrationRequired는 미테스트를 뜻하지 않는다.
- wave 최신 경기 전달·표시 scoped PASS와 상위 clock/NGO 원인·pending 자연 회복 미실행·정밀 시각·전체 migration OPEN은 별개다. Assault/Sniper/Inferno focused 수용과 전체 Invalid·과거63 expiry 원인·dead-abort 미재현도 분리한다.
- 로그 private 보관·source/main 반영 완료의 현행 근거는 [실제 영수증](_Tasks/2026-10-12/01_41_log-rules-history-archive/Research.md)이다. 이전 병합 대기 표시는 당시 이력으로 이관했다.

## 전체 구현 현황

### 🔴 P0 재오픈 — 서버 권위 유닛 ActionSequence

- 권위·표현·observer의 현행 책임 경계는 유닛 규칙과 전투 동기화 계약을 따른다. 개별 테스트 수용은 [감사표](Assets/UnitCombatAssetMatrix.md)의 현행 목록을 따른다.
- 전체25종·역할교대·Legacy rollback·정밀 시각 수용과 clock/Invalid 진단 미결은 개별 focused 결과로 닫지 않는다.
- A0~C3 단계별 과거 수치와 회귀 범위: [상세 원문](History/ImplementationVerificationHistory.md#status-action-sequence). 계약 원본: [유닛 규칙](GameSystemRules/GameSystemRules_Units.md) / [전투 동기화](GameSystemRules/GameSystemRules_UnitCombatSynchronization.md).

### 📐 무작위 맵 — 본체 구현·검증 완료, 적용 후속 남음

#### FlatTop 11×21 무작위 대전 맵 (2026-07-19 기획 확정)

- 무작위 맵1~3단계 및 재경기 맵은 구현·실기 검증 기록이 있다. 멀티 최초 맵 준비 실패 안내도2026-10-13 실기로 확인됐다.
- 잔여: 경로 완전 차단 대응(유닛 행동), 맵 InitialGold 실제 적용(추후 밸런싱과 함께 작업).
- 맵 테스트 모드는 삭제됐고 매 재경기는 새 맵 한 갈래다. 세부 규칙·수치는 [맵](GameSystemRules/GameSystemRules_Map.md) / [무작위 맵](GameSystemRules/GameSystemRules_RandomMap.md), 당시 변경·미관측 경계는 [상세 원문](History/ImplementationVerificationHistory.md#status-random-map)을 따른다.

### ✅ 완료된 시스템

아래는 기존 구현·검증 기록의 인덱스다. 제목에 명시된 검증 범위를 유지하며 현재 전투 수용은 감사표, 잔여는 아래 미완성 항목과 ROADMAP이 우선한다. 세부 사양 사본은 아래 이력에 보존하고 현행 [게임 규칙](GameSystemRules.md)과 [스탯](StatsReference.md)을 기준으로 읽는다.

#### MistShrine 물안개 힐 시스템 ✅ 구현 완료 · 에디터 싱글플레이 실기 검증 완료 (2026-08-12) — **멀티 미검증**

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-1).

#### 스킬 건물 시스템 (Phase 1 타입 A·B + 조준/UI/좌표화/렌더링/버그수정, Phase 2 타입 C 전역 상태변경) ✅ 구현 완료 · 실기기 테스트 PASS (Phase 1: 2026-08-04, Phase 2: 2026-08-05 실기+멀티(클라) PASS)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-2).

#### 연구소 유닛 강화 시스템 + 전투 스탯 ×10 스케일 + 연구 패널 UI ✅ 구현 완료 · 멀티플레이 실기 PASS (2026-07-31)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-3).

#### 코어 게임플레이

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-4).

#### 도끼병(BattleAxe) 휩쓸기형 AoE + 특수 공격 아키텍처 (2026-07-17)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-5).

#### BloomFairy(꽃요정) 힐러 + HoT/DoT 공용 시스템 (2026-07-18) — 특수 유닛 5종 중 3번째

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-6).

#### MushroomBomber(버섯폭격기) 착탄형 범위 DoT (2026-07-19) — 특수 유닛 5종 중 4번째

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-7).

#### InfernoSpirit(지옥불 정령) 단일 대상 DoT (2026-07-20) — 특수 유닛 5종 중 5번째

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-8).

#### QuakeSpirit(대지의 정령) 착탄형 즉발 AoE (2026-07-20) — 특수 유닛 5종 전량 완료

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-9).

#### 코드 정리 3건 — 죽은 코드 제거 / Animator 상태 의존 제거 / Firebase 게이트 제거 (2026-07-13)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-10).

#### 플랫폼 / 빌드 최적화 (2026-07-15)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-11).

#### 팀별 피아식별 + 신규 유닛 에셋 (2026-03-13)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-12).

#### 싱글플레이 AI 시스템 (2026-06-07)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-13).

#### 3D 전환 (2026-02-27 ~ 2026-03-01)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-14).

#### Game UI Lifecycle Framework (2026-03-24)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-15).

#### UI DOTween 애니메이션 프레임워크 (2026-03-19)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-16).

#### 전역 로딩 스크린 (2026-03-17)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-17).

#### 커스텀게임 재경기 시스템 (2026-03-17)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-18).

#### Google 로그인 실기 디버깅 (2026-06-27~28)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-19).

#### 인게임/로비 볼륨·음소거·프로필 버튼 UI 로직 연결 (2026-07-09, 실기 PASS)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-20).

#### 게임포기 로딩 인디케이터 미해제 버그 수정 (2026-06-26)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-21).

#### 매치메이킹 404 수정 — 호스트 결정 Lobby CreateOrJoin 전환 (2026-07-17)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-22).

#### 랜덤 매칭 2회차 실패 버그 수정 + RuntimeLogger (2026-06-25)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-23).

#### 멀티플레이 로비 복귀 버그 수정 (2026-03-17)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-24).

#### 멀티플레이 (Phase 1~8)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-25).

#### 팀별 관점 (ViewConverter)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-26).

#### 로비 종족 선택 UI (2026-04-04~06)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-27).

#### 유닛/건물 스탯 확정 적용 (2026-04-12~13)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-28).

#### 피격 시 부유 HP 텍스트 — World Space (2026-04-12~13, 2026-04-17 World Space 전환)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-29).

#### 종족 인게임 적용 (2026-04-07)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-30).

#### UnitType 개편 + 근접 사거리 시스템 (2026-04-10~11)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-31).

#### 원거리 유닛 공격 중 회전 추적 (2026-04-11~12)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-32).

#### 근접유닛 적 감지 사거리 + 추적 회전 개선 (2026-04-19~24)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-33).

#### 타일 소유권 실시간 감지 — TileOwnershipService (2026-04-26)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-34).

#### 근접유닛 뒷무빙 5차 개선 (2026-04-26)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-35).

#### 유닛/건물 스탯 ScriptableObject 전환 (2026-04-25)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-36).

#### Lobby 패널 CanvasGroup 사전 부착 + ProfileView 로그아웃 버튼 (2026-06-22)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-37).

#### Lobby 씬 UI 규칙 재점검 및 추가 수정 (2026-06-15)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-38).

#### Lobby UI 전체 규칙 준수 수정 (2026-05-30)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-39).

#### 공통 UI 규칙 수립 및 Canvas Scaler / SafeArea 적용 (2026-05-25)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-40).

#### 게임 화면 UI TC 전체 실기기 테스트 (2026-05-27)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-41).

#### BuildingActionPanelUI 씬 계층 재설계 + 런타임 슬롯 제어 (2026-05-29)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-42).

#### BuildingPlacementUI 씬 계층 재설계 (2026-05-29)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-43).

#### 설계 규칙 문서 (GameSystemRules.md)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-44).

#### 인증 시스템 설계 규칙 문서 (AuthSystemRules.md)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-45).

#### 로그인 시스템 C# 구현 (2026-05-24)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-46).

#### 유닛 이동/전투 시스템 재설계 (2026-05-11~13)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-47).

#### 유닛 생산 실패 피드백 시스템 (2026-05-16)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-48).

#### 건물 배치 패널 실패 피드백 + UI 개선 (2026-05-16)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-49).

#### 건물 업그레이드 시스템 (2026-05-17~18)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-50).

#### ProductionPopup UI 레이아웃 재구성 (2026-05-18)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-51).

#### 건물 철거 시스템 (2026-05-18)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-52).

#### 비생산 건물 공용 액션 패널 UI (2026-05-18~19)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-53).

#### 인게임 설정 메뉴 + 게임 포기 기능 (2026-05-18~19)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-54).

#### 코드 리팩토링 (2026-05-18)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-55).

#### 코드 리팩토링 전체 완료 (2026-05-24)

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-56).

#### 버그 수정 및 폴리싱

구현·검증 범위와 당시 제약: [상세 원문](History/ImplementationVerificationHistory.md#status-system-57).

### ⚠️ 알려진 미완성/버그 항목

#### 로그 체계(`GameLog`) 전환 — 미완 항목 (2026-08-17 기준)
| 항목 | 상태 | 비고 |
|------|------|------|
| **컴파일 검증** (`4e027e68` · `675203ae` · `a253232e`) | ✅ **통과 확인됨 (2026-08-17)** | 205건 이관분(`668e0aeb` 계열)에 이어 **마스킹·파일명 단일화·헤더·정리 메뉴 변경분**과 **씬 무관 구현(`a253232e`)까지 사용자 유니티에서 통과가 확인**되었다 — 사용자가 그 상태로 실기 테스트를 수행했다. **⚠️ 단, 후속 커밋 `73574a23`(`Role` 값 표기 통일)은 명시적인 컴파일 확인을 받지 않았다** — 문자열 리터럴 대소문자 1줄 변경이지만 "통과했다"고 적지 않는다 |
| **실기 동작 테스트** | ✅ **PASS (2026-08-17)** | ~~"⚠️ 하지 않았다"~~ → 사용자가 랜덤매칭 실기(에디터+실기기)로 확인했고 근거 로그가 `_Logs/_editor/2026-08-17/RuntimeLog.txt`(**199줄**)에 커밋되어 있다. 확인 항목: 파일명 `RuntimeLog.txt` · 헤더 3줄 규정(목적/시각/빈 줄) · 199줄 축적 · **같은 파일에 2번째 헤더**(일 단위 이어쓰기) · `Role=` 기록 · **`[Auth/...]` 7건**(씬 무관 구현의 목적 달성) · 마스킹(`Uid`·`PlayerId` **16자리 16진수** · `Email=` **0건** · `@` 포함 줄 0건) · 감사 누락분 `HostId` 가 `PlayerId` 와 **같은 해시** |
| **수동 정리 메뉴 실기** (`Hexiege > Logcat > 3.`) | ✅ **PASS (2026-08-17)** | ~~"실제로 실행해 본 적이 없다"~~ → 실제로 실행했다. `_Logs/_editor/` 에 `2020-01-01`(날짜 형식)과 `2020.01.01`(점 구분)을 두고 확인: **`2020-01-01` 삭제 · `2020.01.01` 제외**(`TryParseExact("yyyy-MM-dd")` — **느슨한 `TryParse` 였다면 삭제됐을 케이스**) · **오늘 폴더 보존** · 취소 경로 · **정리 범위 한정 — `_Logs/` 직속 날짜 폴더 17개를 하나도 건드리지 않음**(`LogRules.md` **1.10** 이 *"작업에 귀속된 영구 보존물"* 로 규정한 영역이라 지워지면 복구 불가) |
| **`Lobby.unity` 직접 진입 로그 수집** | **커버하지 않음 — 사용자 결정** | 이 씬에는 부트스트래퍼가 없어(참조 0건) 직접 열고 실행하면 sink 가 0개라 콘솔 폴백만 탄다. **사용자가 "로비를 직접 열어 실행하는 경우는 없다(항상 Login 을 거친다)" 고 판단해 커버하지 않기로 결정**했다 — 미해결 결함이 아니라 **범위 밖 확정 항목** |
| **나머지 계층 `Debug.Log` 이관** | ✅ 이관 완료 (2026-08-19) | 2026-08-19 에 코드 전 계층 이관이 끝났다(위 「현재 단계」 문단: 누적 386건 이관 · 최종 잔존 46건). 2026-10-12 재측정(같은 `grep -rn "Debug\.Log" Assets/_Project/Scripts --include=*.cs`, 주석 포함): **58건**. `Application/GameLog.cs` **9건은 이관 대상이 아니다**(sink 폴백 구현 — `LogRules.md` **1.8** 이 요구하는 동작 그 자체). *(2026-10-12, 낡은 표시를 현재 사실로 고쳤다 — 종전 표시(2026-08-17): 「미착수 (별도 task 분리 결정) — 잔존 234건, 이번 8파일 밖」)* |
| **`NetworkCombatController.cs` raw `Debug.Log`** | ✅ 이관됨 (위 항목과 함께) | 2026-10-12 재측정: 이 파일의 `Debug.Log` 문자열은 **1건**이며 주석 처리된 줄이다. *(2026-10-12, 낡은 표시를 현재 사실로 고쳤다 — 종전 표시(2026-08-17): 「미이관 — 잔존 11건, 그중 하나가 새 역할 로그와 나란히 IsServer 를 찍는 중복」)* |
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

## 2026-10-09 Assault bounded clock-history readiness 추가 기록

이전 source 미구현/Unity 메뉴 대기는 당시 이력이며 현재는 승인4 source 구현 및 diagnostic gate 완료다. [Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md](D:/Projects/Hexiege/Hexiege/Assets/_Project/Docs/_Tasks/2026-10-08/10_55_assault-attack-timing-correction/Research.md)의 “Clock-history 최종 파일 인벤토리 (2026-10-09)”에 전체 source4·Temp38·문서/기준값 목록과 정확한 근거를 연결했다. Main 실제 Unity Action13:41:11→13:41:36 overall/C2/C3 PASS, Root13:44:44 PASS; errors0/warnings27. static4 errors0, Main CLR3 exit0. Main frozen4 manifest hash 및 protected Game/Login/Stats/Assault anim baseline 동일 확인; 빌드/씬 저장 없음.

실제00_10 Expired4/source-marker/PhaseClockRegression/실기기 오류/timing/pixel/성능/NGO native query count는 OPEN. 다음은 사용자 새 Development 빌드→경기 종료→로비 복귀→저장(EndSession dump); 강제 종료/종료 전 reset 시 누락 가능. 구현은 원인 분석용 numeric ring/frozen history이지 오류 해결 판정이 아니다.
