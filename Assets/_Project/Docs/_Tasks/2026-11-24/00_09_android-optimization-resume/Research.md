# Android 최적화 재개 — 조사

프로젝트의 기존 동작을 유지하면서 실제 비용을 줄일 다음 최적화 대상을 고른다. 이번은 새 작업이므로 이전의 긴 누적 기록과 분리하고, 이미 완료한 최적화를 반복하지 않도록 근거만 연결한다.

**문서 역할**
- 목적·질문: 다음 후보의 실제 구조·발생 빈도·제거 가능 비용과 근거를 비교한다.
- 포함: 제한 조사 결과, 완료/미완 구분, 예상 효과의 신뢰도와 위험.
- 제외·담당: 구체 실행·종료 기준은 [Plan](Plan.md), 과거 실행 이력은 아래 기존 문서.
- 원본·갱신: 이번 재개 조사의 원본이며 새로운 소스/측정 근거를 확보할 때 갱신한다.

## 1. 승인 범위와 현재 상태

2026-11-24 사용자는 최적화 재개와 새 Task 작성을 요청했다. 전문 담당의 핵심8~9파일 제한 읽기 조사로 아래3후보를 비교했으며, 사용자 요청에 따른 작업별 소스·문서 연결 재감사를 아래4절에 마쳤다. 확인한 작업 단위의 검수이지 프로젝트 모든 최적화가 끝났다는 선언은 아니다. 첫 제안은 국소 실현 가능성 설계·검증이며 제품 구현·빌드·새 실기는 없다. 조사 근거 없이 절감 ms/FPS나 구현 순위를 확정하지 않는다.

사용자 판단 기준은 예상 실제 비용 절감이 큰 순서, 비슷하면 작업량이 작은 순서다. 관측 시간은 제거 가능량과 다르므로 구조·빈도·제거 가능 부분·신뢰도·구현/검증 부담을 구분한다. 프로젝트 진도에 맞는 작업량과 종료 기준을 함께 제시한다.

## 2. 완료·추가 후보·조사 분야의 구분

기존 Research (`Assets/_Project/Docs/_Tasks/2026-10-28/14_10_android-performance-correction/Research.md`, opt 근거)와 기존 Plan (`Assets/_Project/Docs/_Tasks/2026-10-28/14_10_android-performance-correction/Plan.md`, opt 근거)은10/28부터의 누적 최적화·측정·이동 교정 이력이다. 이번 새 Task가 그 원문을 덮어쓰거나 대량 복제하지 않는다.

2026-11-24 1차 요약의 혼동을 정정하고 작업별 메서드·현재 행·기존 Task 절·구현 증거·성능 한계를 아래4절에 연결했다. 넓은 분야 이름을 미완 작업으로 나열했던 설명을 아래처럼 정정한다. Android FPS 등 실기 효과가 미측정이라는 이유로 구현 완료 항목을 미구현으로 되돌리지 않는다.

| 분류 | 해당 항목 | 이번 처리 |
|---|---|---|
| 구현 완료 | 타일 mesh/material·hatch, owned Bundle·collector 재사용, 이동6이웃, Detect/Attack early-exit, NCC 중복 query | 재선정하지 않음. 각 검증 범위와 성능 미측정 한계는 기존 기록 유지 |
| 구현 완료 | NCC scratch3개와 재진입 guard, Wave 버퍼·emitter/clock·predicate/key 검색, 투사체 private Visual 반환 재사용512상한, UnitFactory marker 정확 크기 배열, UnitView native 검사 List 제거, 중간 문자열·GameLog 문자열 교정, Ranking lazy 생성 | 기존 Research27/35.1/40~48 및 현재 소스에 구현 근거 있음. 전체 영역 최적화 완료라는 뜻은 아님 |
| 구체적인 추가 후보 | 아래 공간 후보 제한·힐러 존재 early-exit·AI CountUnits 병합 | 현재 소스에 해당 미구현 반복 구조가 남아 있음. 기존 완료 항목과 다른 제거 범위를 설계·동등성 검증한 뒤 구현 판단 |
| 단순 조사 분야 | 전투 CPU, VFX CPU/GPU, GC·메모리 보관, 네트워크, 물리, 발열, 로딩 등 | 구체적인 남은 호출·빈도·제거 가능 부담을 확인하기 전에는 미완 작업 수나 구현 순위로 세지 않음 |

현재 소스의 대표 확인 위치는 NetworkCombatController222~225/827의 scratch·guard, UnitCombatUseCase315~318/3436~3438의 Wave 버퍼, ProjectilePresenter106/275/492의 private Visual pool, UnitFactory587~608의 snapshot/owned 배열, RankingView488/784의 지연 생성이다. VFX GameObject pool과 ParticleSystem 초기 cache도 존재한다. private Visual pool은 모든 유닛 GameObject pool과 같지 않으며 후자까지 완료했다고 포괄하지 않는다.

이동 교정은 현재 수용 가능한 단계에서 이번 최적화 재개와 분리한다. 잔여 큰 회전·terminal2 미완과 실기 한계는 기존 Research42.7~42.8/Plan39.7~39.8에 보존하며 이번에 재작업하지 않는다.

## 3. 후보 비교 결과

전문 담당의 최종 중복 감사에서 아래3개는 현재 소스에 남은 구체적인 추가 후보로 확인했다. 구현할 절대 우선순위나 절감량을 확정한 것은 아니다. 새 Testcase·메모리·계측 도구나 규칙 변경은 이번 문서 준비 범위에 포함하지 않는다.

| 후보 | 확인 구조·잠재 감소 | 작업량·위험·미확정 |
|---|---|---|
| 적 공간 후보 제한 | UnitCombatUseCase1716/1744/1815/1839의 최근접/Detect가 U유닛+B건물을 전수 순회. Q회 검색에서 보수적 후보 K로 제한하면 제거 상한 Q×(U+B−K) | 핵심3~4파일/3단계의 중간 규모 검증. 밀집·장거리에서는 감소0 가능, 실제 K분포·ms/FPS 미측정 |
| 힐러 존재 조회 early-exit | UnitCombatUseCase1558/1578의 bool은 FindInjuredAllyToHeal1604의 전체 U 비율/거리 ranking을 완료한 뒤 존재만 소비 | 제품1파일·작은 범위. 실제 힐 target 선택은 유지해야 함. 힐러 빈도 미측정·힐러0이면 효과0 |
| AI 팀 수 세기 병합 | AIOpponentController711/712의 CountUnits2회 전수 순회를1회로 합쳐 R1호출당 U검사1회 제거 | 제품1파일·작은 범위. Single의 decision interval/cooldown 제한, 전투 전체 효과로 확대 불가 |

첫 후보는 이미 완료한 early-exit/중복 query와 달리 탐색 후보 집합 자체를 줄이는 축이다. 잠재 규모 때문에 먼저 국소 실현 가능성을 제안하지만 절대 효과1위나 구현 GO로 확정하지 않는다. 후보별 시간 견적 근거가 없어 파일·단계·검증 부담으로만 설명한다.

UnitSpawnUseCase53의 logical tile→List 역인덱스는 존재하나 GetUnitAt154는 겹친 유닛 중 첫 항목만 반환하므로 그대로 재사용하면 후보를 누락한다. Root/logical tile 차이·terminal contact·건물 반경·같은 거리의 순회 순서·spawn/death·동기 재진입까지 보수적 포함/결과 동일성을 증명해야 한다. 거리/공격·이동 규칙을 바꾸어 후보를 줄이지 않는다.

VFX는 기존 pool·cache·증분 fixed-step이 이미 있고 이번 제한 조사에서 큰 중복은 확인하지 못했다. 전체 VFX 최적화 완료나 비용0 판정은 아니다. 이동의 수용 가능 현재 상태와 잔여 큰 회전·terminal2는 기존 Task의 별도 후속으로 유지한다.

빗금의 최종 구현은 GPU instancing이 아니다. 현재 HexGridRenderer349의 `Mesh.CombineMeshes`와 `NoBuildHatchCombined` 단일 MeshRenderer를 사용하며368에서 개별 renderer를 비활성화한다. 기존 Research38.1의 동일 Editor 맵53draw→1, 제출 정점 약30.19% 증가·추가42.76KiB는 해당 combined mesh 결과다. 초기 instancing 제안이나 event 감소만 있었던 static batching 검토와 구분한다. 기존8 marker·Profiler·수집 메뉴는 도구/기능으로 제품 최적화 완료 개수에 섞지 않는다.

## 4. 작업별 소스·문서 검수 장부 — 2026-11-24

검수 기준은 D 작업 트리의 `codex/android-performance-optimization`, HEAD `a64b31d16b788ad413739f0b31be809453384f82`와 현재 dirty 소스다. 원격 main 반영 여부를 검증한 표가 아니다. 전문 담당·메인의 작업별 소스 대조를 정리했으며 아래 과거 fixture/native 수치는 오늘 재실행한 결과가 아니다. 동일 항목을 다른 분야에서 다시 합산하지 않는다. 아래 각 행은 현재 구현 근거가 확인됐지만 검증 종류·효과 한계는 서로 다르다.

기존 절 번호는 10/28 Research (`Assets/_Project/Docs/_Tasks/2026-10-28/14_10_android-performance-correction/Research.md`, opt 근거) 기준이며 Plan 표기는 기존 Plan (`Assets/_Project/Docs/_Tasks/2026-10-28/14_10_android-performance-correction/Plan.md`, opt 근거)이다.

| 작업 단위 | 현재 소스·메서드/행 | 기존 절 | 구현 상태·과거 근거·효과 한계 |
|---|---|---|---|
| NCC scratch3개 | [NetworkCombatController:222](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) — 222~225/827~852 scratch・재진입 guard | §2/35.1 | 구현 근거 확인. 반복 임시 버퍼 재사용; 실제 경기 절감 ms 미측정 |
| Wave 버퍼3개 | [UnitCombatUseCase:315](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) — 315~318/3436~3438 재사용 | §35.1 | 구현 근거 확인. 버퍼 재사용 구조 확인; FPS 미측정 |
| Wave emitter·owner clock | [WavePresentationPresenter:23](../../../../Scripts/Presentation/Effects/WavePresentationPresenter.cs) — 23~25/40/57/102~104/117 cache | §3 | 구현 근거 확인. 반복 준비·조회 회피; 실기 CPU 이득 미측정 |
| Wave baseline predicate | [WavePresentationCoordinator:120](../../../../Scripts/Application/Combat/WavePresentationCoordinator.cs) — 39/SetBaseline120~124 | §3 | 구현 근거 확인. predicate 재사용; 실기 할당 byte 미측정 |
| 공격 표현 key 검색 | [AuthoritativeAttackPresentationCoordinator:78](../../../../Scripts/Application/Combat/Sequencing/AuthoritativeAttackPresentationCoordinator.cs) — 78/110/123/131 dictionary·scope index | §3 | 구현 근거 확인. 옛 순수 key helper에서 후속 구조로 대체됨. 옛 구현 그대로 존재한다는 판정 아님 |
| hit marker 배열 | [UnitFactory:583](../../../../Scripts/Infrastructure/Factories/UnitFactory.cs) — GetHitFrameTimes583~609 snapshot1/count 정확 배열/Sort | §3 | 구현 근거 확인. 중간 수집 구조 축소; 최종 owned 배열 유지 |
| Bundle 중간 List 제거 | [NetworkAttackPresentationBundle:25](../../../../Scripts/Infrastructure/Network/NetworkAttackPresentationBundle.cs) — 25/58~61/125~136 owned 배열 | §36 | 구현 근거 확인. 과거 동일 입력21case79assert 중 Bundle12case; byte/ms 미측정 |
| applied collector 재사용 | [UnitCombatUseCase:196](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) — 196/216/226/1193~1224 idle1/reset/finally | §40.1 | 구현 근거 확인. 과거16case1170assert·warm 임시3객체 회피; owned 결과 배열 유지·실기 직접 할당 수 미측정 |
| Detect 존재 early-exit | [UnitCombatUseCase:1450](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) — 1450/1479/1737/1762 | §35.1 | 구현 근거 확인. 존재 참일 때 후속 검사 생략; false/nearest 유지·전체 FPS 미입증 |
| Attack 존재 early-exit | [UnitCombatUseCase:1404](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) — 1404/1428/1832/1855 | §35.2 | 구현 근거 확인. 과거30case210assert; 조건부 검사 감소·실기 ms 미측정 |
| NCC 빈 검색 결과 재사용 | [NetworkCombatController:910](../../../../Scripts/Infrastructure/Network/NetworkCombatController.cs) — 910~914 + UnitCombatUseCase1000~1018 | §39/Plan36 | 구현 근거 확인. 과거36case232assert; 완료 empty 조회만2→1, 실제 발생 수/K 미측정 |
| 복귀 후보6이웃 | [UnitMovementUseCase:711](../../../../Scripts/Application/UseCases/UnitMovementUseCase.cs) — 711~731 Neighbor/GetTile | §36 | 구현 근거 확인. 과거21case79assert 중 이동9case; 전체 grid 대신6조회·A* 전체 비용 제거 아님 |
| retire 제거 버퍼·retained 집합 | [UnitAttackResultPresentationShadow:285](../../../../Scripts/Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs) — 285~287/427~446/715~722 | §3 | 구현 근거 확인. 임시 제거 목록 재사용 구조; 실기 절감량 미측정 |
| 타일 mesh bake | BakeHexTileRuntimeMesh:25 (`Assets/Editor/Tools/Combat/BakeHexTileRuntimeMesh.cs`, opt 근거) — Run25/93~112; HexTile.prefab95/117 baked mesh2개 참조 | §18/18.1, Plan14 | 구현 근거 확인. runtime ProBuilder2개 제거. 과거187tile246ms→211tile6.879ms는 다른 조건이므로 개선율 아님 |
| top material만 복제 | [HexTileView:98](../../../../Scripts/Presentation/Grid/HexTileView.cs) — InitializeColorMaterial98/ReleaseColorMaterial137 | §37~37.3 | 구현 근거 확인. 동일 Editor231tile 8배치:462→231개,1326864→723492B/net603372B. Android 전체 RAM/FPS 아님 |
| 빗금 combined mesh | [HexGridRenderer:318](../../../../Scripts/Presentation/Grid/HexGridRenderer.cs) — BatchNoBuildHatches318/CombineMeshes349/Release394 | §38.1 | 구현 근거 확인. 동일 Editor53draw→1/pixelDiff0; 정점636→828(+30.19%), 추가42.76KiB. GPU instancing 아님/Android ms 미측정 |
| private Visual 반환 재사용 | [ProjectilePresentationPresenter:106](../../../../Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs) — 106/BeginVisual275/RecycleVisual477~492 cap512 | §3/35.1 | 구현 근거 확인. 최대512 private Visual pool; 모든 Unit GameObject pool 완료라는 뜻 아님 |
| 투사체 진단 문자열 사전 guard | [ProjectilePresentationPresenter:543](../../../../Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs) — DiagBegin543/568·DiagFinish579/592 | §3 | 구현 근거 확인. 표본24 이후 버릴 문자열 조립 회피; 진단 자체 비용 교정, Release FPS 주장 아님 |
| Wave 짧은 진단 포맷 | [WavePresentationDiagnostics:234](../../../../Scripts/Application/Combat/WavePresentationDiagnostics.cs) — TryWriteSingleChunk234/232~262·900byte guard198~226 | §3 | 구현 근거 확인. 짧은1part 포맷 경로; 실기 byte/시간 절감 미측정 |
| UnitView native 검사 List 제거 | [UnitView:826](../../../../Scripts/Presentation/Unit/UnitView.cs) — ApplyAttackPhasePresentation826/native count842~855 | §3 | 구현 근거 확인. 임시 List 제거 구조; 모든 native 검사 비용 제거 아님 |
| Ranking row 지연 생성 | [RankingView:187](../../../../Scripts/Presentation/UI/Views/Lobby/Ranking/RankingView.cs) — InitializeIfNeeded187/EnsureRowPool488/Build501/RenderCurrentPage782 | §16/35.1, Plan12 | 구현 근거 확인. Lobby init0→첫 Render10row로 생성 시점 이동. 생성 비용 삭제 아님 |
| 로그 최종 줄 조립 | [GameLog:372](../../../../Scripts/Application/GameLog.cs) — Compose372 | §3/13 | 구현 근거 확인. 중간 줄 문자열 조립 제거; 출력 보존·실기 FPS 미입증 |
| 재경기 진단 문자열 조립 | [GameEndUI:2527](../../../../Scripts/Presentation/UI/GameEndUI.cs) — BuildRematchDiagFields2527 | §3/13 | 구현 근거 확인. 중간 기본 문자열 회피; 재경기 기능 비용 전체 제거 아님 |
| 선택 Dev Info NoStack | [ConsoleSink:186](../../../../Scripts/Infrastructure/Debug/ConsoleSink.cs) — OmitInfoStack186 | §3/13~14/20.5 | 구현 근거 확인. PCRecovery/RematchDespawn/UnitDied/Wave 지정 Info만. 모든 Info caller stack 제거 정책 아님 |
| Android archive 성공 후 native 중복 생략 | [ConsoleSink:90](../../../../Scripts/Infrastructure/Debug/ConsoleSink.cs) — 90/94/100 + RuntimeLogger355/361 | §21.5~21.6/후속 Warning, LogRules1.10 | 구현 근거 확인. DevAndroid 일반 Info·지정2 Diagnostic Warning만 archive 성공 조건부 생략. 전체 경고/에러 끄기 아님 |

별도 기존 기능: EffectManager158/1287의 VFX GameObject pool과 VfxPoolItem46의 ParticleSystem cache는 존재하지만 이번 신규 최적화 성과로 합산하지 않는다. 8 markers·Profiler/exporter·로그 수집/ZIP 메뉴도 계측·도구로 분리한다.

추가 후보3개는 구현 완료 행과 별개다. 현재 UnitCombatUseCase1716/1744/1815/1839의 전역 scan에는 공간 후보 제한이 없고,1558~1578의 힐 존재 bool은 FindInjured1627의 전체 ranking을 소비한다. AIOpponentController711~712/CountUnits894~897은 두 전체 순회를 한다. 기존 logical 위치 index가 있다는 것만으로 보수적인 전투 공간 후보 제한이 구현됐다고 판단하지 않는다. 구체안·동등성 검증 전 구현하지 않으며 예상 제거량과 절대 비용 순위는 미확정이다.

## 5. 물리·발열을 포함한 확장 비용 조사 — 2026-11-24

사용자가 제외했던 분야까지 비용·불필요 참조/반복을 확인하도록 요청해 읽기 조사 범위를 확장했다. 아래11개는 완료25항목의 재선정이 아닌 추가 후보이며 앞3후보도 다시 번호만 합산하지 않는다. 식은 동일 조건에서 제거 가능한 직접 작업량으로, 실측 ms/FPS/GC byte나 전체 게임 개선율이 아니다. 제품 구현·새 테스트·빌드·Git 변경은 없다.

| 후보·분야 | 소스 근거 | 제거 가능한 직접 작업량 | 효과0 조건·tradeoff·검증 한계 | 예상 범위 |
|---|---|---|---|---|
| 전투 공간 후보 제한 | [UnitCombatUseCase:1716](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) 1716/1744/1815/1839 | Q(U+B)→QK; 제거 가능 상한 Q(U+B−K) | K≈U+B이면0. Root/논리 tile·반경·순서 포함 안전성 미확정 | 3~4파일/중간 검증 부담 |
| 힐 존재 early-exit | [UnitCombatUseCase:1558](../../../../Scripts/Application/UseCases/UnitCombatUseCase.cs) 1558~1578/1627 | true일 때 U→첫 적격 j; false는 U 유지 | 힐러0이면0. 실제 힐 target ranking 유지 | 1파일/작음 |
| AI 팀 집계 병합 | [AIOpponentController:711](../../../../Scripts/Application/Services/AIOpponentController.cs) 711~712/894~897 | 두 전체 순회2U→U: 해당 집계 검사50% 감소 | Single 조건. AIConfig decision20/10/5초는 설정이며 예외 경로 포함 실제 빈도 미측정 | 1파일/작음 |
| AI 배치 BFS scratch | [AIOpponentController:782](../../../../Scripts/Application/Services/AIOpponentController.cs) 782~799 | 시도마다 HashSet2+Queue1 생성식을 warm 재사용으로 회피 | 탐색량 그대로/retry10초 조건·high-water 용량 상주·재진입 검토 | 1파일/작음 |
| 혼잡 감쇠 key scratch | [CongestionMap:85](../../../../Scripts/Application/Services/CongestionMap.cs) 85~90 | empty guard/재사용으로 new List(keys) 생성식 회피; 5초 설정 기준 정상 약12회/분 가능 | 키 M복사와 M감쇠 유지·실제 빈도 별도. GameConfig CongestionDecayInterval5초 | 1파일/작음 |
| UI 쿨다운 정수 cache | [SkillCooldownOverlay:71](../../../../Scripts/Presentation/UI/SkillCooldownOverlay.cs) 71~88 + [BuildingSkillPanelUI:362](../../../../Scripts/Presentation/UI/BuildingSkillPanelUI.cs) 362~378 | N회 format/text→정수 변화 K회; fill은 계속 갱신. 5slot·30fps·모두 cooldown·패널 열림 예150→약5회/초(해당 format만 약96.7%) | 패널 닫힘/쿨다운 없음 효과 축소·0. heap byte/FPS 개선율 아님 | 2파일/작음 |
| 숨겨진 로비 preview camera gate | [Lobby.unity:751](../../../../Scenes/Lobby.unity) 751/764/798/813~821 + [LobbyRootView:144](../../../../Scripts/Presentation/UI/Views/Lobby/LobbyRootView.cs) 144~151/203~208 | Battle탭 밖 필요 없는 추가 camera-render1/frame→0 후보. 30fps예30회/초·512²×30=7,864,320 목표 RT pixel 환산 | 실제 GPU 실행/pixel shader 비용 아님·RT 상주 메모리 자동 해제 아님·전투 효과0. alpha탭·독립 root의 camera 생명주기 검증 | 중소 범위/확실한 불필요 반복 후보 |
| 타워 launch ParticleSystem cache | [EffectManager:907](../../../../Scripts/Presentation/Effects/EffectManager.cs) 907~916 | 고정 hierarchy라면 warm GetComponentsInChildren 검색/배열1→0 | 기존 타워 작은 후보와 같은 caller인지 확인 대상·과거 보류 후보이며 새 발견 아님. hierarchy 교체 처리 필요 | 약2파일/작음 |
| 지속피해 VFX reject cache | [VfxPoolItem.TimedDamage:18](../../../../Scripts/Presentation/Effects/VfxPoolItem.TimedDamage.cs) 18~20/43 | RejectTimedDamageAcquire warm PS 검색·배열1회 회피; damage cache 이미 존재 | 정상 reject0이면 효과0·모든 VFX pool 재구현 아님 | 1파일/작음 |
| 입력 UI raycast scratch | [InputHandler:493](../../../../Scripts/Presentation/Input/InputHandler.cs) 493~513/HandleClick285 | 클릭당 new PointerEventData+new List<RaycastResult> 생성식2개→같은 EventSystem warm0 | 내부 포함 heap 객체 총2개라는 뜻 아님. RaycastAll 유지/EventSystem 교체 참조 정리·재진입 보존 | 1파일/작음 |
| 타일 MeshCollider 필요성 검증 | [HexTile.prefab](../../../../Prefabs/Tiles/HexTile.prefab) class64/96행 | 231tile예 native collider 등록/생성 최대231개 제거 가능성 | 불필요 확정·삭제 승인 아님. 런타임 Physics Raycast/Overlap/Rigidbody/Nav/OnMouse 호출0, Input/Camera/SkillAim Plane.Raycast는 수학 함수. 외부 prefab 상속·기존31ray 계약 확인 필수 | 검증 우선/실제 byte·ms 미측정 |

물리 추가 한계: 프로젝트 자체 씬의 PhysicsRaycaster 사용은 확인되지 않았고 Game GraphicRaycaster blocking도0이다. 그래도 참조가 없다는 검색 결과만으로 collider가 불필요하다고 확정하지 않는다. FixedUpdate1131은 Animator fixed 공격 clock이며 FixedTimestep0.02/자동 물리 설정을 근거로50Hz 물리 비용 전부를 불필요하다고 계산하지 않는다.

기존 구현과 별개인 분야: NetworkResourceSync172~205는 event-driven이고 GameHud90~99/245~266은 cache가 이미 있다. 참조 보관 자체는 누수 증거가 아니다. 현재 비용 근거로 추가 제거량을 산정하지 못한 영역을 비용0이나 미완 작업 수로 바꾸지 않는다.

현재 Android defaultQuality0의 Mobile RP는 GUID/meta 대응이 확인된 기존 설정이다: renderScale0.8, depth/opaque0, MSAA1, SRP batcher1, shadow1024/1cascade/soft0. 이 설정을 새 최적화로 다시 선정하지 않는다. Bloom/Shadow 끄기·scale/FPS 낮추기는 화질/표시 변경이 있는 별도 제품 선택안이다. 예를 들어 scale0.8→0.7의 목표 pixel23.4375% 감소는 전체 GPU ms/FPS 개선율 예측이 아니다. 현재 품질 변경 승인은 없다.

발열/전력: 프로젝트 소스의 frame-rate writer는 확인되지 않았고 Quality vSync0/runInBackground0/URP Adaptive1은 실제 기기 열 대응 보장이 아니다. 과거32.2→33.5°C는 배터리 snapshot이며 post 지연·연속 열/전력 trace 부재 때문에 SoC throttling이나 몇%/몇°C 절감 가능량을 산정할 수 없다.

판단: 숨겨진 로비 preview는 필요 없는 추가 GPU 반복과 작은 범위가 확인되는 후보지만 전투 성능에는 효과가 없다. 전투 공간 후보 제한은 잠재 규모가 큰 대신 포함·순서·수명 안전 설계 위험이 높다. 둘을 절대 ms1위로 비교할 근거는 아직 없다. 나머지도 발생 빈도와 기존 동작 보존 조건을 확인한 구체안을 공유한 뒤 구현 여부를 정한다.

제품판 구분: 이 문서의 측정·진단 근거는 opt 측정판에서 확보했다. main 선별 제품판에는 해당 Dev 진단·실험 코드/도구를 포함하지 않으며, opt 전용 원문·private 증거는 제품 산출물이 아니다.
