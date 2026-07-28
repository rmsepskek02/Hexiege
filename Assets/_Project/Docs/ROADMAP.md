# Hexiege - 작업 로드맵

**최종 수정일:** 2026-07-21
**현재 단계:** QuakeSpirit(대지의 정령) 착탄형 즉발 범위 딜러 구현 완료(2026-07-20, QA PASS·**멀티플레이 실기 로그 검증 완료**) — 마지막 남은 특수 유닛으로, **특수 유닛 5종(BattleAxe/TorrentSpirit/BloomFairy/MushroomBomber/QuakeSpirit) 전량 + InfernoSpirit DoT까지 전부 완료**. 착탄형 **즉발** AoE(DoT 아님): 주 타깃 1마리 직접 100%(공격력 20, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 기본 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(20×0.5)`=10, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 아군 무피해, 서버 권위. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼(`internal static` 공용화)+건물 순회 신설+유닛/건물 hit-set 분리(규칙 29), `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5). 멀티 로그로 전 항목 정상 검증(로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`). 규칙 43. 직전 InfernoSpirit(지옥불 정령) 단일 대상 DoT 구현 완료(2026-07-20) — 이미 완성된 원거리 유닛에 DoT 특수만 추가(주 타깃 1마리 직접 25 + 그 1마리에게만 DoT 5/초×3초, AoE 아님). 규칙 40 초 단위 틱을 단일 대상 재사용, 별도 진입점(`ApplyInfernoDot` 5/3) 분리. 규칙 41~42. **알려진 이슈(보류, 별도 task 분리)**: ① QuakeSpirit 타격 애니↔데미지 텍스트 타이밍 어긋남(Attack 클립 `OnAttackHit` 미주입+placeholder hitFrameTimes → 스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상), ② 멀티 원거리 공격 facing 버그(에셋 아닌 공유 회전 로직 추정). 후속(Phase F): 기기 QA로 3D 텍스처 품질 확인, 피격 VFX 프리셋 연결, QuakeSpirit `OnAttackHit` 클립 이벤트 주입(타이밍 이슈 해소), 멀티 원거리 facing 버그 수정, Firebase/EDM 저장소 방침 정리, AI 반응 시스템·시나리오 정밀 검증 + 잔여 신규 유닛 프리팹 실기 테스트. **병행 관찰 항목 — 매치메이킹 404(호스트 결정 단계) 수정(A방식, 2026-07-17, 브랜치 `claude/matchmaker-404-error-pi9qdn` 커밋 `a3dbc73`)**: 랜덤 매칭 후 호스트 결정을 매치 결과 조회(P2P 클라 호출 시 404) → Lobby CreateOrJoin(matchId=lobbyId) 원자 선점으로 전환. 초기 매칭 실기에서 404 없이 정상 연결 확인했으나 간헐(intermittent) 버그라 지속 테스트 중(확정 PASS 아님) — 비활성화(주석)한 레거시 코드는 지속 테스트 확정 후 삭제 예정. 상세는 우선순위 표 및 Phase A-2 참조
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
| ✅ 완료 | MushroomBomber(버섯폭격기) 착탄형 범위 DoT — 특수 유닛 5종 중 4번째. 착탄 중심 월드 원형 반경(`BlastAttackBehavior`, arc 없는 도끼병 부채꼴, QuakeSpirit 재사용 헬퍼) 내 적 유닛 DoT + 주 타깃 직접 10(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성). DoT 초 단위 discrete 틱 모드 신설(규칙 34 확장 — 1초 간격·틱당 올림·총량 클램프·매초 데미지 텍스트). 식물 라인 생산 배선(SporePatch=MushroomBomber, FloralNursery=BloomFairy). 규칙 38~40. QA PASS·실기 완료 (2026-07-19) | 기능 | 중 |
| ✅ 완료 | InfernoSpirit(지옥불 정령) 단일 대상 DoT — 특수 유닛 5종 외 추가 DoT 유닛. 이미 완성된 원거리 유닛에 DoT 특수만 추가: 주 타깃 1마리 직접 25(기존 단일 피해 `ReplacesPrimaryAttack=false`, 건물 공성) + 그 주 타깃 1마리에게만 DoT 5/초×3초(총 15, AoE 아님·반경 없음, 적 유닛만). 규칙 40 초 단위 틱을 단일 대상으로 재사용, 값은 별도 진입점(`ApplyInfernoDot` 5/3)으로 분리(MushroomBomber 2/3 회귀 없음). `SpecialAttackConfig` inferno DoT 필드 + GameBootstrapper 주입. 레지스트리 1줄. 규칙 41~42. 공격 방향(facing) 버그는 사용자 결정으로 보류(별도 작업). QA PASS·실기 완료 (2026-07-20) | 기능 | 소 |
| ✅ 완료 | QuakeSpirit(대지의 정령) 착탄형 즉발 AoE — **특수 유닛 5종 마지막(전량 완료)**. 착탄형 즉발(DoT 아님): 주 타깃 1마리 직접 100%(공격력 20, 건물이면 100% 공성) + 착탄 월드 원형 반경(`_quakeRadius` 1.0=인접 1칸) 내 다른 적 유닛·**적 건물** 50%(올림 `CeilToInt(20×0.5)`=10, 주 타깃 제외). ⚠️ MushroomBomber/BattleAxe와 달리 스플래시가 건물도 포함. 핸들러 `QuakeAttackBehavior`(`ReplacesPrimaryAttack=false`)+레지스트리 1줄, MushroomBomber 원형 반경 헬퍼 `internal static` 공용화 재사용(유닛만 로직 무변경)+건물 순회 `CollectEnemyBuildingsInRadius` 신설+유닛/건물 hit-set 분리(규칙 29). `SpecialAttackConfig` `_quakeRadius`/`_quakeSplashRatio`(1.0/0.5) 주입. 규칙 43. 타격 타이밍 어긋남(OnAttackHit 미주입)은 사용자 결정으로 별도 task 보류. **QA PASS·멀티플레이 실기 로그 검증 완료** (2026-07-20, 로그 `_Logs/2026-07-20/11_00_quakespirit-aoe-verify/`) | 기능 | 중 |
| 🟡 중간 | 피격 VFX 프리셋 연결 — `UnitEffectConfig.hitPreset` Inspector 배선(Human 6종 등 미연결) | 에셋 | 소 |
| 🟢 낮음 | QuakeSpirit Attack 클립 `OnAttackHit` 이벤트 주입 — 특수 공격 로직(규칙 43)은 구현·검증 완료됐으나 클립 `OnAttackHit` 미주입 + placeholder `hitFrameTimes`(1.0s)로 타격 애니↔데미지 텍스트 타이밍이 어긋남(스플래시 텍스트 타임아웃 7.5s 지연, 피해·동기화 자체는 정상). 인젝터로 실제 타격 프레임 주입 시 해소. 사용자 결정으로 별도 후속 task. (특수 공격 핸들러 확장 완료: BattleAxe·TorrentSpirit 2026-07-17, BloomFairy 2026-07-18, MushroomBomber 2026-07-19, InfernoSpirit·QuakeSpirit 2026-07-20) | 기능/에셋 | 소 |
| 🟡 중간 | 멀티플레이 원거리 공격 방향(facing) 버그 — 원거리 유닛이 공격 시 타겟을 정확히 안 바라봄. 진단상 에셋 아닌 멀티 원거리 facing 공유 회전 로직(서버 계산+NetworkTransform) 문제로 추정(근접 유닛 미노출). 수정 시 근접 유닛 회귀 주의. 상세: `_Tasks/2026-07-20/03_22_infernospirit-dot-and-attack-facing/Plan.md` (InfernoSpirit 작업에서 진단·보류) | QA/버그 | 소 |
| ⬜ 백로그 | 튜토리얼 | 기능 | 대 |
| ⬜ 백로그 | Firebase 백엔드 (랭킹/IAP) | 기능 | 대 |

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
| Pistoleer HP/공격/사거리 | 30 / 6 / 1.0 | DPS=3, cooldown≈2.0s |
| Assault HP/공격/사거리 | 50 / 1 / 2.0 | DPS=5, cooldown≈0.2s |
| Sniper HP/공격/사거리 | 30 / 10 / 5.0 | DPS≈3.3, cooldown≈3.0s |
| Castle HP (Human/Spirit/Trans) | 200 / 150 / 300 | 확정 — 플레이테스트 후 조정 |
| AutoTower 공격력/쿨다운 (Human) | 15 / 5.0s | 확정 |
| AutoTower 공격력/쿨다운 (Spirit) | 15 / 3.5s | 확정 — 가장 강한 타워 |
| AutoTower 공격력/쿨다운 (Trans) | 15 / 5.0s | 확정 |
| MistShrine 힐량/범위 (Trans) | 1 HP/s / 범위 3 | 확정 — 플레이테스트 후 조정 |

---

## Phase D — 콘텐츠 확장 (백로그)

### D-1. 방어/마법 타워
**✅ 방어 타워 완료 (2026-06-01~02)**: TowerCombatUseCase 신규 구현. 종족별 스탯(사거리 4.0/공격력 15, 쿨다운 Human·Trans 5.0s/Spirit 3.5s). 서버 권위 처리. Human CannonTower 초기 방향(내 진영 0도/상대 진영 180도) 구현 완료.
- 스킬 건물(FlightFacility / MagicSpirit / WillowShrine): **기획 확정 / 미구현**. **마나 미도입** — 자원 없이 건물별 글로벌 쿨다운만 사용. 건물당 최대 5개 스킬, 스킬 타입 3종(즉발 범위 피해 / 장판 DoT / 전역 상태변경), 모바일 지점 조준 UX, 서버 권위. 상세: [GameSystemRules_Skills.md](GameSystemRules/GameSystemRules_Skills.md). 구체 스킬·수치는 추후 데이터로 확정.

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
