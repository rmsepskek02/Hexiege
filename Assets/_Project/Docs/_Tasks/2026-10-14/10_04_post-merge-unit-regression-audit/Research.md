main 병합 뒤 싱글플레이 유닛 문제의 기존 감사·추격 교정 근거를 보존하고, 추가 타격 타이밍 교정의 적용·Main 독립 검증 인계를 기록한다. 광산·랠리는 제외한다. 코드/CLR 검증 완료와 실제 화면·경기 해결 수용은 다른 판정이다.

# Research — 병합 후 유닛 회귀 감사

## 전 유닛 과거 승인 계약 감사 — 25종 보고·Main 검토 완료 / 한계 명시

사용자가 전 유닛의 **기존 교정·사용자 수용 계약을 먼저 복원한 뒤 현재 구현과 비교**하도록 승인했다. 현재 config/Stats/UnitCombatAssetMatrix/status만으로 과거 기대값을 정하지 않는다. UnitType.cs의 실제 이름·ID를 기준으로 Human8/Spirit9/Transcendence8의 각 유닛을 빠짐없이 조사한다(현재 enum 직접 읽기 기준). 타격 marker `ss:ff@30fps`, 권위 피해 offset 예외, 전체 cycle, 모델 offset/rotation, VFX 전달·종료·전투 후 복구를 별도 축으로 기록한다.

기대 계약은 옛 Task의 최종 승인/수용 문단과 후속 사용자 변경에서 추출해 경로·행/절·날짜를 기록했다. Human/Transcendence programmer 및 Spirit Copernicus 최종 감사는 Main 검토 완료다. 아래 두25행 표는 당시 수집·비교 초안으로 보존하고, 말미 「전 유닛 감사 최종 판정」이 현재 한정된 결론이다. 과거 수용 primary가 부족한 이름별 항목은 그대로 미확정이며 현재 config로 메우지 않는다. 이번 감사는 새 구현·Stats/규칙 동기화 승인이 아니다. 기존 pursuit·SOLO timing·Fox 실제 로그 근거도 보존한다.

## 범위와 현재 판정

### Historical expected 수집 초안 — 25종 / Main 현재 비교 검토용

**이 표는 과거 승인/수용 문단을 읽은 수집 결과이지 현재 모든 값의 정답/PASS 판정이 아니다.** ID는 현재 UnitType.cs로 재확인했다. `ss:ff@30fps`와 literal seconds는 구분한다. 옛 Task가 저장값을 초 단위로 검증했다는 사실만으로 원래 사용자의 프레임 표기 의도가 해소됐다고 하지 않는다. 행 번호는 이번 읽기 시점의 원본 위치이며 날짜·절을 병기한다. 현재 코드/자산 대조는 Human/Transcendence programmer·Spirit Copernicus 결과의 Main 검토 후 별도 열로 확정한다.

모델/VFX 열의 **M0**은 공통 [ActionSequence Plan](../../2026-07-22/13_26_unit-action-sequence-implementation/Plan.md) L792~813의 B1 구조·7/29 수용: identity VisualRoot, Root Motion off, 단일 Simulation writer/Client Root 쓰기 금지다. 모델 자식의 모든 yaw/import offset을 0으로 강제하는 뜻은 아니다. **M6**은 [4/28 mesh cleanup Plan](../../2026-04/2026-04-28/14_30_mesh-offset-cleanup/Plan.md) L5~10/L57~88의 Bear/Fox/Lion/Ember/Flame/Inferno mesh Y0·Idle/Walk rotation offset0 작업 기록이다. 초기 DirectionAngles -30 제안은 L41~56에서 +30으로 정정됐고 Attack import offset은 별도 유닛 테스트 대상이다. **R0**은 타겟 사망/전투 종료 뒤 연속 Attack·안전 재개를 보존하되, 개별 수용과 공통 rally/전체 migration을 분리하는 기준이다. 수치가 없는 유닛의 모델 yaw/VFX endpoint는 미확정으로 남긴다.

| 유닛(ID) | 과거 expected 시간·예외·주기 | 실제 옛 승인/수용 출처·변경/부족한 근거 | 모델·VFX/복구 기대 경계 |
|---|---|---|---|
| Pistoleer(0) | marker/HP0.8초=`0:24`, cycle2초. native continuous epoch와 진짜 re-entry 구분 | [10/4 Research](../../2026-10-04/21_58_pistoleer-attack-timing-correction/Research.md) L76/L93 및 [Plan](../../2026-10-04/21_58_pistoleer-attack-timing-correction/Plan.md) L91~93/§12.13. 설정2→0.8·명시 clip 교정; 이후 source-owner/re-entry 교정. 정밀 시각/최종 human visual는 그 Task에도 OPEN | M0/R0. 기존 source marker lease·닫힌 old occurrence 구분 보존; projectile 준비와 결과 수용 별개 |
| Assault(1) | 사용자 `0:05`=5/30초, production cycle`0:10`=10/30초; clip 미사용 fallback0.2초는 다른 값 | [10/8 Research](../../2026-10-08/10_55_assault-attack-timing-correction/Research.md) 「확인한 원본」L94~98/첫 「10/11 사용자 focused 수용」L3~5. 옛0:17·0:33은 이전 표기, 0:33 의미 미확정 | M0/R0. focused 로그 수용을 원63 expiry 해결/full visual로 확대하지 않음 |
| Sniper(2) | 사용자 `1:22`=52/30초, cycle3초 | [10/10 Plan](../../2026-10-10/23_30_sniper-attack-timing-correction/Plan.md) L11/L37~42. 1:73 옛 오표기→1:22, 저장 clip 보존. 사용자 focused 로그 수용은 위 Assault Research10/11 L3~5 | M0/R0. VFX death/despawn 소유권·정밀 접촉은 별도 |
| LittleKnight(3) | 공통 교정은 literal0.25/1.15초·2hit/3초를 전제로 함. 원래 `0:25,1:15`(Main 전달)와 동일 뜻으로 단정 못 함 | [8/31 Research](../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Research.md) L77~88/[Plan](../../2026-08-31/23_30_b3-c3-runtime-failure-correction/Plan.md) L47: continuous anchor의 다음 marker occurrence 교정. [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L150~158은0.25/1.15 실기 기록. 10/11 사용자 개별 수용은 Main 공용 인계; 최초 두 frame 수치의 명시 승인 원문은 추가 필요 | M0/R0. 전용 Task 부재=미테스트 아님. safe post-combat 복구와 old MOVE 실패는 별도 |
| SpearMan(4) | 최종 사용자 `0:25`=25/30초, cycle2초 | [9/30 Plan](../../2026-09-30/18_02_spearman-attack-timing-correction/Plan.md) L11~20/L29/L43. 옛0.24초/0:24→0:25, 양 역할 focused PASS/CLOSED. precise offset는 미계측 | M0/R0. 단일 actual Attack/양팀 명시 연결 유지 |
| BattleAxe(5) | 9/19 교정의 명시 expected literal1.02초. 7월1.1667초와 원래1:17(Main 전달)의 관계는 승인 계보를 더 확인해야 함 | [9/19 Plan](../../2026-09-19/23_06_battleaxe-impact-marker-correction/Plan.md) L11/L28~33/L85:1.1667→1.02, 9/20 정밀 수용 제한. 10/11 사용자 개별 수용 Main 인계는 보존하나 원래1:17 재해석 승인으로 확대하지 않음 | M0/R0. [7/16 Plan](../../2026-07-16/18_06_battleaxe-aoe/Plan.md)의 전방 sweep·아군/뒤/범위밖 제외, 동시 AoE 표현 유지; 주기 변경 승인 추가 확인 |
| Tank(6) | 7/11 실제 offset≈0.167초 기록. 원래`0:17`(Main 전달)=17/30초와의 의도 변경 원문 미확보; 현재0.16667을 expected로 채우지 않음 | [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L150~158은 측정 이력이지 새 frame 승인 아님. [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L36는 효과 간헐 누락 보고; timing 재승인 아님 | M0/R0. Tank/Cannon 기존 효과 참조·HP는 유지; 실제 projectile authority/endpoint 수용을 이름만으로 선언하지 않음 |
| CannonCart(7) | Tank와 동일하게≈0.167초 옛 실기와 원래`0:17`의 차이 남음. cycle4초 기대의 과거 사용자 원문 추적 필요 | 위 [7/9 Research](../../2026-07-09/01_12_combat-hit-timing-sync/Research.md) L156 및 [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L36. 후속 VFX 참조 결정을 타격 frame 변경 승인으로 쓰지 않음 | M0/R0. Tank 공유 preset과 시각 간헐 누락은 별도 evidence |
| FlameSpirit(10) | 최종6hit `0:20,1:05,1:13,1:20,1:27,2:03`=20/30,35/30,43/30,50/30,57/30,63/30초; cycle3초 | [9/28 Plan](../../2026-09-28/21_55_flamespirit-multihit-attack-correction/Plan.md) L3/L35~39. 9/29 사용자 확인1:27이 옛1:28을 대체. 육안 긍정 수용과 회차별 정밀6hit 검증은 별개 | M0/M6/R0. 각 HitIndex/Stop·death 잔여 타격을 합산 PASS로 대체하지 않음 |
| EmberSpirit(11) | marker1.00초=`1:00`, 명시 clip2.6666667초; fallback cooldown2.2초 보존 | [9/28 Plan](../../2026-09-28/21_05_ember-spirit-attack-correction/Plan.md) L12~13/L29~35. 연결 focused PASS/CLOSED, period 재설계 없음. 4/12 Task2:20→2.33 기록은 과거 표기/초 환산 오류 가능성을 별도로 남김 | M0/M6/R0. 첫 Attack 열거 순서 대신 실제 motion 명시 |
| InfernoSpirit(12) | 9월 literal0.50초/cycle3초 교정 후 10/11 사용자 `1:15`=1.5초 재확정은 Main 공용 인계. 최신 숫자 승인 primary Task 위치는 Spirit 감사에서 보강 필요 | [9/21 Plan/Research](../../2026-09-21/02_42_infernospirit-movement-jump-correction/Research.md) §17~18의 이동/방향/VFX 사용자 수용과 10/10 0.5 실행은 다른 날짜다. [10/10 Plan](../../2026-10-10/00_19_inferno-burn-and-race-tower-vfx-connection/Plan.md) L27~31은 당시0.5와1:15 모순, 후속1.5 승인 전 제안 기록 | M0/M6/R0. Research L289~303:mesh X0, SpawnPoint X Blue−0.0085951/Red−0.00859513, Attack−58° 보존. 커밋 source-only VFX/SFX는 Stop 후 보존하고 target/tracer/HP는 닫음 |
| DustSpirit(13) | actual Attack marker literal1.04초·cycle3초; Attack2-first 방지 명시 clip | [9/27 Plan](../../2026-09-27/10_54_dustspirit-explicit-attack-clip-binding/Plan.md) L9~11 및 [Fox Plan](../../2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md) L444의9/28 당시 conditional. 10/11 사용자 수용은 후속 Main 인계이며 precision 확인과 구분 | M0/R0. Attack/Attack2 순서 독립, source·result/방향 복구 보존 |
| BoulderSpirit(14) | 최종 사용자`1:10`=40/30초·cycle4초 | [9/29 Plan](../../2026-09-29/03_13_boulderspirit-attack-correction/Plan.md) L5/L77~79. 옛1.15초→1:15=1.5 수용→사용자1:10 새 수용, 서로 다른 버전 | M0/R0. Little MOVE4 미재발을 원인 해결로 바꾸지 않음 |
| QuakeSpirit(15) | 9/14 gate expected literal1.667초·단일 impact. 원래 frame 표기와 cycle 숫자 승인 primary는 추가 수집 | [9/14 Plan](../../2026-09-14/00_56_quake-spirit-impact-marker-correction/Plan.md) L3/L11/L145:1.0 placeholder→1.667. 10/11 개별 수용 후속 Main 인계, 당시 precise strike는 conditional | M0/R0. 직접100%+다른 적 유닛/건물 splash50%, original target 제외; 기존 special handler 유지 |
| TideSpirit(16) | 사용자 저장`1:15`=1.5초·cycle3초 | [9/29 Plan](../../2026-09-29/19_09_tidespirit-attack-correction/Plan.md) L5~10/L43~47.1.15초→1.5, focused visual PASS/CLOSED | M0/R0. 설정 정합과 전체 movement/migration 별개 |
| StreamSpirit(17) | 9/22 production marker/HP0.50초. 원래`0:17`(Main 전달)과 관계 미확정; 초값 정렬 수용이 최초frame변경승인인지 분리 | [9/22 Plan](../../2026-09-22/12_13_streamspirit-production-timeline-correction/Plan.md) L9~12/L163~166. Legacy VFX 보존 focused PASS, marker→timer 실제차이도 기록됨. 0.5=0:15 환산을 사용자0:17 승인 변경으로 만들지 않음 | M0/R0. Unresolved/LegacyFallback 유지, projectile flight/impact exact-key는 완료 아님 |
| TorrentSpirit(18) | 사용자 launch`2:10`=70/30초·cycle4초; travel0.5초는 launch marker와 다름. HP/heal은 각 contact 시각 | [9/30 Plan](../../2026-09-30/23_19_torrentspirit-attack-timing-correction/Plan.md) L9~10/§6 및 [10/2 Plan](../../2026-10-02/23_45_common-recovery-and-torrent-launch-correction/Plan.md) L90~98. 0.5 임시launch→2:10; lost-primary launch 예외/기존 contact 결과 수용은 제한된 match 범위 | M0/R0. living original target의 current aim/상실 시 last-valid aim, current caster origin, prelaunch source death/gameend 취소. VFX가 stationary라는 옛 문장은 후속 이동 구현과 대조해야 함 |
| BearGuard(20) | 사용자 marker`0:13`=13/30초. 옛 Task는 clip`1:20`≈1.6666666초와 config cooldown1.2초를 **둘 다 보존**하며 cycle 결정 OPEN | [9/29 Plan](../../2026-09-29/23_28_bearguard-attack-timing-correction/Plan.md) L3/L40~55. focused0:13 사용자 수용은 주기1.2→clip.length 승인 여부를 확정하지 않음. 4/12 1:20→1.33초 표기도 과거 환산 기록 | M0/M6/R0. precise visual/전체 movement는 별개 |
| FoxMagician(21) | 의도예외: charge marker1.00초/HP2.25초/cycle4초. 기본marker로 HP 덮어쓰기 금지 | [9/22 Plan](../../2026-09-22/19_48_foxmagician-production-timeline-correction/Plan.md) §16 L337~396의2.25 복원/Factory 양경로 예외 및 §21 L438~444의9/28 사용자 현행수용. 이전2.25→1.0 가설은 철회. 새 SOLO 시각 우려는 별도 OPEN | M0/M6/R0. default-key writer immediate·source charge와 HP 분리, projectile 재튜닝 후속; VFX 종료/동일 공격 exact correlation 미측정 |
| LionKnight(22) | 사용자최종2hit`0:18,1:13`=.6,43/30초; 옛 Task는 cycle3초를 보존 | [9/30 Plan](../../2026-09-30/01_21_lionknight-attack-timing-correction/Plan.md) L3~5/L12/L40~42. 옛0.22/1.08초 및0:22/1:08 문구와 새frame를 분리. current clip2초(Main 전달)는 과거3초 보존 결정과 대조 대상 | M0/M6/R0. closed focused 수용과 C3 expiry7 별도 OPEN |
| RhinoBreaker(23) | 전용Task expected actual Attack literal1.05초. 원래`1:05`(Main 전달)=35/30초와 승인 계보 불명 | [9/13 Research](../../2026-09-13/20_34_rhino-breaker-impact-marker-correction/Research.md) L16~19/L35~40/L53~68:Attack2의1.05를 actual Attack에 주입. 10/11 개별 수용 Main 인계는 보존하지만 정확한frame재해석 승인과 구분 | M0/R0. actual Attack state만 교정/Attack2 보존, building/후속 target 전환·full scope와 분리 |
| EagleArcher(24) | 전용 최종 timing 변경의 primary 근거 미확보. 현재0.1/clip1 또는Attack2 .7 값을 expected로 대입하지 않음 | [4/28 integration Plan](../../2026-04/2026-04-28/14_00_new-asset-game-integration/Plan.md)와 [6/5 prefab Plan](../../2026-06-05/22_14_unit-prefab-component-setup/Plan.md) L59은 등록/구조 근거. 10/11 사용자 개별 테스트 수용 Main 인계는 유효; exact marker/cycle 변경 승인 원문은 이름별 부족 항목 | M0/R0. 전용Task 부재나 MigrationRequired로 미테스트 재분류 금지. 개별 modelYaw/VFX endpoint 숫자 primary 미확보 |
| RabbitTrickster(25) | 최종사용자`0:20`=20/30초·cycle2초 | [9/28 Plan](../../2026-09-28/10_03_rabbittrickster-explicit-attack-clip-binding/Plan.md) L72~87. 원래0:18 및 old literal0.18초 버전과 분리; 새0:20 저장·사용자수용이 latest | M0/R0. 명시Attack, alternate-first gate, 최종 focused수용과precision 별개 |
| MushroomBomber(26) | marker1초/cycle3초, 직접주타깃+별도3초DoT. marker/tracer/impact/poison 종료는 서로 다름 | [10/9 Research](../../2026-10-09/08_12_mushroombomber-vfx-split/Research.md) L11~13/L23~30 및 [7/19 Plan](../../2026-07-19/01_42_mushroombomber-impact-dot/Plan.md) L25~35 사용자 설계. 10/10 source freeze는 visual수용 아님; timing 최초1초 사용자지정 원문 추가 필요 | M0/R0. immutable attack due impact1회, tracer callback HP/queue 비구동, recipient Damage record sole owner/refresh noRestart·drain vsforced teardown 구분 |
| BloomFairy(27) | marker`1:00`/prep1+발동후cooldown3=명목cycle4초, HoT200/3; clip3→4 승인retime, healer clip.length로cooldown 덮기금지 | [10/3 Plan](../../2026-10-03/03_28_bloomfairy-heal-timing-correction/Plan.md) L254~273/§12 및 §23 L735~743. 후반1~3→1~4 retime·marker전반보존·heal blend.15시험값, 실제postwait3·duration3 scoped수용과 전체visualOPEN 분리 | M0/R0. gradual server facing/future-start wait/연속heal noHeld, HoT writer와 VFX current owner/Drain 별도. 현재clip4를 옛3에 대한 임의불일치로 판정하지 않음 |

**수집 결과의 수용 경계:** 10/11 Rhino/Quake/Battle/Dust/Little/Eagle 개별 사용자 수용은 이번 Main 공용 인계와 기존 실수 사건의 사용자 정정 기록에 있다. 개별 수용을 무효화하지 않지만 exact offset/frame의 primary 변경 승인까지 역산하지 않는다. Task가 conditional로 남아 있는 날짜와 후속 사용자 수용 날짜를 각각 표시한다. 현재 clip·config·Factory가 동일하다는 이유로 이 표의 불명확한 원래frame계약을 닫지 않는다. 10/13 「현재 구현 기준」 문서 정정은 현황 mirror이지 historical correctness oracle가 아니다.

### 현재 보존 여부의 한정된 Main 증거 — 최종 per-unit 판정 아님

Main이 read-only `git diff --stat b65751c6`로 `Assets/_Project/Animations/Units`, `Assets/_Project/Resources/Config/UnitStatsConfig.asset`, `Assets/_Project/Scripts/Infrastructure/Factories/UnitFactory.cs`, `Assets/_Project/Prefabs/Units`를 비교한 출력은 EMPTY라는 인계를 받았다. 따라서 **이 정확한 경로들은 final10/12 combatcommit 이후 rollback 차이가 없다는 보존 근거**다. 원래 수정/프레임환산이 옳았다는 증명은 아니다. Game.unity의16줄 변경은 당시 Main 검토 진행 중이며 이 조사에서 source/asset 수정하지 않는다.

Main 전달 현재 clips는 Little .25/1.15, Battle1.02, Rhino1.05, Stream.5, Tank/Cannon.16667, Rabbit.66667, Bear clip1.6666666, Lion clip2, Bloom clip4다. 이 전달값의 실측범위/최종 per-unit source wiring은 담당 감사표의 Main 검토 후 기록하며, **현재 숫자를 expected로 복제하지 않는다**. Rabbit/Bloom은 실제 후속 승인으로 original값이 바뀐 사례다. Little/Tank/Cannon/Stream/Rhino의 원래frame와literal 값·Bear preserved cooldown·Lion3초 보존은 위 named 부족 근거를 해소해야 한다. 최종 비교 결론을 대기하되 runtime을 필수로 요구해 historical 감사를 막지 않는다. 지금은 수정 제안/구현 허가가 아니라 보고다.

### Main 독립 추출25종 marker vs historical 초안 — 현재 코드 교정 제안 아님

현재 열은 Main이 실제 clip time을 독립 추출해 전달한 값(초)이다. 일부는 인계에서 반올림됐으며 이 문서 담당의 자산 재실측은 아니다. 위 historical 표의 원본을 비교 기준으로 삼는다. **A축:10/12 이후 지정경로 보존**, **B축:옛 Task literal 값/후속 승인과 현재값 대조**, **C축:원래 ss:ff 사용자 의도 확인**을 나눈다. B축 숫자가 같아도 C축 미확정이 없어지지 않는다. clip event·config fallback·Factory 실효값·실제 HP/화면은 별도 관측이며 이 표는 marker 비교다.

| 유닛(ID) | 현재 Main 추출 marker(초) | historical 대조 결과/남은 named 근거 |
|---|---|---|
| Pistoleer(0) | .8 | 옛0:24/Task .8 일치; re-entry/source-owner 보존의 현재 full wiring은 담당 검토 별도 |
| Assault(1) | .166667 | 사용자0:05 후속 결정과 일치; old0:17/0:33을 다시 expected로 쓰지 않음 |
| Sniper(2) | 1.7333 | 사용자1:22 후속 결정과 일치; 반올림 값으로 exact float/pixel을 판정하지 않음 |
| LittleKnight(3) | .25/1.15 | 7/11·8/31 Task literal 값과 동일. original0:25/1:15의 해석·변경 승인 primary 부족은 남음 |
| SpearMan(4) | .8333 | 최종0:25 후속 사용자 수용과 일치; old0.24/0:24로 되돌리지 않음 |
| BattleAxe(5) | 1.02 | 옛 전용 Task explicit1.02 artifact와 동일, merge loss 아님. original1:17 의도/변경 계보는 미확정 |
| Tank(6) | .16667 | 옛7/11 실행값≈.167과 동일. original0:17 의도의 확정/변경 승인 primary 없음; 현재값을 정답으로 선언하지 않음 |
| CannonCart(7) | .16667 | Tank와 같은 original0:17 vs old literal 문제. laterVFX참조 수용은 frame변경 승인이 아님 |
| FlameSpirit(10) | .666667/1.166667/1.433333/1.666667/1.9/2.1 | 최종6frames0:20/1:05/1:13/1:20/1:27/2:03과 일치. 옛1:28이 아니라 후속1:27 계약 |
| EmberSpirit(11) | 1 | 전용 Task explicit1.00과 일치. marker일치와 clip-derived cycle/fallback2.2 보존은 구분 |
| InfernoSpirit(12) | 1.5 | Main 전달10/11 사용자1:15 재확정과 일치. old0.5와 다른 승인 날짜이며 최신 primary Task 위치 보강 항목은 유지 |
| DustSpirit(13) | 1.04 | 옛 Task explicit1.04 artifact와 동일. 원래 frame 의미를 새로 자동 환산하지 않음 |
| BoulderSpirit(14) | 1.333333 | 최종1:10 사용자 수용과 일치; old1.15초/후속1:15=1.5와 버전 분리 |
| QuakeSpirit(15) | 1.667 | 전용 Task explicit1.667과 동일. literal 반올림 계약과 원래 정확한frame 의도는 구분 |
| TideSpirit(16) | 1.5 | 사용자1:15 저장·focused 수용과 일치 |
| StreamSpirit(17) | .5 | old Task explicit.50 artifact와 동일, merge loss 아님. original0:17 의미/승인계보는 미확정 |
| TorrentSpirit(18) | 2.333333 | 사용자launch2:10와 일치; travel/contact HP 시각은 이 marker와 동일한 값이 아님 |
| BearGuard(20) | .43333 | 최종0:13 marker와 일치. 현재clip1.6666666 vs oldTask config1.2 보존/주기OPEN은 marker 판정과 별개 |
| FoxMagician(21) | event1 / damage config2.25 | 옛 Plan§16/§21 의도예외와 일치. event=HP 단일화 금지. 새 SOLO vs 이전buildMulti 시각 우려 OPEN |
| LionKnight(22) | .6/1.43333 | 최종0:18/1:13과 일치. 현재clip2 vs oldTask3초 보존은 cycle/Factory 계보 검토가 필요한 별도 항목 |
| RhinoBreaker(23) | 1.05 | old Task explicit1.05 artifact와 동일, merge loss 아님. original1:05 의미/변경승인은 별도 미확정 |
| EagleArcher(24) | Attack.1 / Attack2 event없음 | historical exact marker/cycle 변경 승인 primary 미확보. 현재.1은 expected를 만드는 근거가 아니며 개별 사용자 수용도 취소하지 않음 |
| RabbitTrickster(25) | .666667 | 최종사용자0:20 저장/수용과 일치. original0:18은 새 결정으로 superseded됨 |
| MushroomBomber(26) | 1 | 옛source-freeze marker1과 일치. 최초 timing 지정·VFX/DoT 수용 범위는 별도 primary 추적 항목 |
| BloomFairy(27) | 1 / clip stop4 | 승인된marker1/후반clip3→4 retime과 일치. configpost3 유지·명목cycle4를 clip3 기대값과 혼동하지 않음 |

**초안 결론:** 지금 입증된 보존은 정확한10/12 비교 경로에 한정된다. Axe/Dust/Rhino/Stream의 explicit decimal은 이미 옛 Task에 있던 승인된 artifact 값이지 이번 merge에서 새로 생긴 차이가 아니다. 원래 Stats frame 의미가 그 당시 적절히 확인됐는지는 여전히 별도 질문이다. Rabbit/Boulder/Flame/Bloom/Fox에는 후속 결정·예외가 실제 원문에 있어 original값으로 단순 rollback 판정을 할 수 없다. **「25종 모두 정확하다」는 결론 없음.** 모델 yaw/import 수치·VFX delivery/recovery의 전 유닛 현재 대조 및 부족한 primary 승인은 담당 결과의 Main 검토로 보강하며, runtime을 기다리는 항목으로 숨기지 않는다. 상시 규칙/Stats를 현재에 맞춰 고쳐 이 차이를 없애지 않는다.

- 2026-10-14 10:04는 Main이 측정해 전달한 작업 기준 시각이다. 사용자 후속 「기존 Task 갱신」과 SOLO impact timing 구현 요청을 명시 승인으로 받았다. 같은 Task를 유지하며 document-manager는 이 Research와 [Plan](Plan.md), 별도 승인된 `.claude/mistakes.md` 사건의 목차·본문 추가만 편집한다. 광산·랠리는 제외한다.
- **최신 사용자 확인: 재현 환경은 SINGLEPLAYER(싱글플레이).** SOLO 분기 분석이 우선이다. 멀티 B3 보존은 SOLO 실행 경로의 안전성 증명이 아니다.
- 기존 pursuit 적용·Main 독립 검토 및 타이밍 후속 소스5/CLR 근거는 유지한다. 현재 FoxMagician(type21) SOLO 실제 로그 focused 분석 인계를 완료했으나 사용자 시각 reportedMismatch/수용/원인은 OPEN이다. 비교 멀티는 이전 빌드이며 현재 빌드의 동일 조건 peer 비교가 아니다. 한 종족 근접이 괜찮다는 제한 관측은 전체 PASS가 아니다. 에이전트 native 메뉴·빌드는 NOT RUN, CLR 결과도 실기 시각 PASS가 아니다.
- 기준: fetch된 `origin/main=88f60e38cf9f7a8dce0c8cc498fe89dfd09f06b6`, final 교정 비교 `0de4f868`, 원래 비교 `b65751c6`. 현재 checkout은 `codex/docs-checker-maintenance`이며 최신 main 파일 적용·병합을 의미하지 않는다.
- 기존 dirty 검사기·Skills 규칙·다른 Task·tests·삭제된 meta는 보호한다. programmer 후속 승인 범위는 SOLO impact timing/targeted bounded 진단이며 경로·네트워크·Stats·클립·프리팹·씬은 제외한다. 문서 담당은 소스/에셋/메모리·다른 Task를 쓰지 않고 fetch/merge/checkout/bisect/build/Unity/원시 Editor.log 접근·TC/QA도 하지 않는다.

### 2026-10-14 17:28 후속 부분 테스트 — 사용자 보고와 focused 분석 완료

사용자 후속 명확화: **한 종족 근접은 괜찮다는 제한 관측이며 FoxMagician(type21)의 SOLO 타이밍이 멀티보다 이르게 보인다. 비교 멀티는 이전 빌드다.** type21은 Main UnitType.cs 확인 근거다. 기존 charge1.0초/HP2.25초 의도 계약과 Factory 예외는 유지하지만 사용자 비교를 틀림/정상으로 기각하지 않는다. focused 분석은 아래와 같이 완료됐고 시각 timing reportedMismatch·원인·수용은 OPEN이다. 앞선 CLR GREEN/소스 수용과 이번 실제 timer 관측은 human visual PASS가 아니다.

Main StageUAS 원본 경로는 `Assets/_Project/Docs/_Logs/_editor/2026-10-14/RuntimeLog.txt`다. 분석은 [고정 스냅샷](../../../../../../Temp/solo-timing/Solo1728.snapshot.txt) 기준 SHA256 `CFFE86D0D96FA4B073ABBB02888CE16EBCF65FB62F077F355EF8D27FCDC120F0`이다. 크기958969bytes/2425행은 programmer 전달값이며 Main 크기 재계수 아님. 문서 담당은 원시 로그를 읽거나 독립 계수하지 않았고 아래 Main 독립 확인과 programmer 전달 근거를 분리한다. live-read 재발 기록은 mistakes 기존 사건에 추가 완료했으며 게임 원인 증거가 아니다. 아래 NOT RUN 표기는 구현 인계 당시 이력이고 현재 실제 부분 테스트는 이 절을 따른다.

| 관측 | 근거 출처·범위 |
|---|---|
| Fox25기, commit205, HP dispatch71 전부 StatusApplied, native201 | Main의 고정 입력 File.ReadLines 독립 재계수. dispatch Late 최소0.025999149670497ms/최대9.10580357142976ms/음수0 |
| 모든71의 nominal `anchor+4×loop+2.25` | programmer source/trace 검증 전달. Main 전체 exact native-marker/HP join 검증이 아님 |
| Unit3 Epoch1 제한 직접 확인 | Main bounded raw 확인: anchor53.52557466695, charge marker1초 이벤트54.53735957341, HP due55.77557466695→actual55.7825467687, Late6.972101757ms. 한 표본을 전체 native/pixel 연계로 확대하지 않음 |
| 큐64 received/emitted 쌍, ImmediateTrue·same-frame·text played, 최대1.779ms | 전체 값은 programmer 전달; Main은 raw2쌍만 직접 확인. 71과64를 같은 모집단 또는 유실 차이로 간주하지 않음 |
| coverage 상한 | Fox owner9개 cap·text cap128은 programmer 전달. commit205−dispatch71은 취소/사망/관측 범위 차이가 있어 실패 수가 아님 |

Sidecar profile와 key-generator 추적에서 Fox는 Unresolved/CanRunSequencer=false라 token/authorization에 들어가지 못해 result key가 default가 되고 Fox immediate=true로 흐른다는 programmer 근거다. 이 immediate 분기는 **SOLO와 멀티 양쪽**이며 SOLO-only 즉시 표기 원인이라고 하지 않는다. 해당 경로에 Coordinator100ms delay를 적용해 비교하지 않는다. SOLO HP 로컬 writer와 멀티 ClientRPC 수신은 실제 branch 차이지만 현재 멀티 trace의 수신 지연/화면 비교는 확인되지 않았다.

현재 관측된71 HP dispatch는 승인된2.25초 기준보다 이르지 않다. 이는 관측 timer에 한정된 사실이지 사용자의 이전 빌드 멀티 대비 시각 우려가 잘못됐다는 결론이 아니다. 새 root cause/추가 교정은 선택하지 않았다. 교정 선택에 부족한 근거는 **현재 빌드의 focused Fox 멀티 trace**와 같은 기준의 시각 연결이며 전체 유닛 재테스트 요구가 아니다. programmer는 source5 해시 불변을 보고했다. 이번 focused 분석/문서 반영에서 source/config/build/Unity 변경은 없고 기존 CLR·실제 timer 관측·human visual 수용은 계속 분리한다.

### 타이밍 후속의 구현 전 production CLR 기준선 — 당시 측정 이력

Main은 Probe.cs/exe/runtime assembly를 실행 전후 frozen 확인해 독립 실행 exit 0을 받았다. 입력은 120초·주기2초의 일반 Spear production 경로이며 실제 TryAttack/TickCooldowns/PendingHits/HP 적용을 사용한다(Main 전달 실행 근거; 문서 담당 재실행 아님). [Probe.cs](../../../../../../Temp/solo-timing/Probe.cs)·[RunProbe.ps1](../../../../../../Temp/solo-timing/RunProbe.ps1)은 ignored 로컬 재현 자료다.

| 입력 | final start drift(ms) | due drift/error(ms) |
|---|---:|---:|
| 30fps | 1933.339484 | 1933.339561 |
| 60fps | 966.672767 | 983.339511 |
| 45fps | 0.002857 | 11.114022 |
| irregular 30fps | 1933.332894 | 1966.666257 |

값은 Main 독립 실행 전달값이다. HP event count와 실제 HP 감소는 정확히 일치했다는 확인이며 수치 목록이 없는 aggregate를 새로 만들지 않는다. 실행의 native Time warning은 인쇄됐지만 일반 Spear 타격 경로는 TorrentTime을 통과하지 않는다는 검증 범위를 유지한다. 이 측정은 결정적 CLR scheduling 근거이지 native Animator·사용자 경기·pixel 타격의 재현/수용이 아니다. Factory 최종 cooldown/marker 덮어쓰기 확인 및 config-vs-clip 가설 철회는 유지한다.

당시 Main 승인 계약은 continuous epoch/미래 occurrence/captured target·sequence의 결합과 finite old-hit 소유권·최대1new commit/unit/frame이었다. 이후 구현·최종 manifest/검증은 다음 절에 기록 완료했다. 이 기준선의 Main 실행은 drift/HP 측정 exit0이며 독립 RED assertion 실행이 아니다. Temp Probe의 현재 파일/exe는 GREEN 세대로 바뀌었으므로 위 링크를 보존된 baseline binary로 간주하지 않는다. 기존 pursuit 근거를 timing 검증으로 재분류하지 않는다.

### 최종 타이밍 적용 및 검증 — Main 독립 확인 완료

[최종 Receipt](../../../../../../Temp/solo-timing/Receipt.md)를 전문 읽어 실행 근거로 사용했다(새 규칙 원본 아님). 실제 변경은 아래 기존 소스 5개이며 Main이 source hooks/authority ownership을 직접 검토하고 최종 해시 전후 일치를 확인했다.

| 적용 경로 | SHA256 |
|---|---|
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitAttackPresentationPolicy.cs` | `B534BC0AA79D18FDEFBA13B405113B25B0BF869B8834A14EE6921F2CC589FA76` |
| `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` | `242F14CDBAF04B47E432E13CE7321A27563DEB5403BD81F3E7FB4C17FD281734` |
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `6427105CB972B868F9978EF02932EEC49AC99DDC0D999BA9764D46C9C20BA933` |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | `C02D66CDF38AA09E87521B0CDFE59F3DBB90D0436129A593BFB46749DAF02FB2` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `4A7D92F4F22F3E0D32F89944162CE9D17C085BDF1C5144340CD8834D9C0459A9` |

positive sorted offset(중복 포함)의 절대 미래 occurrence만 새 clock을 사용한다. ZERO/invalid/unsorted offset은 변경 없는 Legacy fallback이며 zero 교정을 주장하지 않는다. Torrent/Healer는 밖이다. Bootstrap nonNetwork 블록은 동일 SoloNow를 cooldown/pending에 전달하는 변경만이며 다른 stage 순서 변경은 없다. retarget은 같은 playback anchor를 유지하고 새 playback은 새 epoch다. 이미 커밋된 hit의 captured target/due는 CC/Stop/commanded pause/close에서 유지한다. alive/current Domain에 대한 View disable/rebind는 취소 권위가 아니며 Domain death/removal/replacement가 owner를 무효화한다. 기존 ApplyAttackDamageCore/AppliedDamageObservation을 사용하고 새 range/facing/CC 피해 제약을 넣지 않았다.

| 독립/원본 증거 | 결과 및 한계 |
|---|---|
| programmer 원본 RED | tool `0d1016`/process `68173`, exit1. 반복 occurrence drift 한 frame 초과 assertion 실패. RED binary는 보존되지 않았고 현재 Probe.exe는 GREEN. Main 독립 RED 주장 없음 |
| Main 새 Compile·회귀 | command `67b91c`→completion `e8da5b`, 모두 exit0. Runtime303/appended15/omitted0/errors0, Editor32/project14/errors0, Release303/errors0/warnings27. Runtime23/Editor0 warnings는 Receipt의 새 compile 기록. 기존 pursuit GREEN 뒤 실제 새 clock/HP Probe와 private 실제 authority core 실행 |
| Main 전체 frozen 재실행 | `ca720e` exit0, `FullManifestAndArtifactsFrozen=True`. 전체5source+Probe.cs/exe+Runtime/Editor DLL 실행 전후 고정. 앞선 compile/run도 pinnedsource4+기존pursuit+Probe.cs/RunProbe 불변 확인 |
| 실제 실행한 보존 assertion | captured-target/old pending HP, CC/Stop pending 유지, pause는 미래만 이동, alive View close HP 유지, Domain death/removal/reuse/target replacement, Legacy zero cadence, client role/null stats/nextId 복원; 실제 production authority core 실행이며 native Editor entry는 아님 |
| 보호·범위 | Main 보호4(Skills/checker/Stats/Game) 해시 및 기존 UnitMovementContracts `0FF35145…33958` 불변. 광산/랠리/경로/네트워크/config/asset 변경 없음. 기존 Factory 가설 철회 유지 |
| 문서·수용 | Main 최종 검사 completion `b1cb09` 실제 exit1, 동일한 기존 참조6건·신규 실패 없음·메모리 고아/감소 없음. Task는 검사 제외이며 이 상태 줄 갱신에 추가 검사 없음. native 메뉴/빌드/실제 경기 NOT RUN. first blend·native frame exact/pixel·원래 사용자 증상의 인과는 OPEN |

| 120초 교정 후 CLR 입력 | final due lateness(ms) | final start drift(ms) | HP events/실제 감소 |
|---|---:|---:|---:|
| 60fps | 0.006231 | 0.006154 | 60/60 |
| 30fps | 0.006231 | 0.006154 | 60/60 |
| 45fps | 11.114022 | 0.002857 | 60/60 |
| irregular30fps | 33.332931 | 23.332901 | 60/60 |

Main 독립 전달값이며 각 lateness는 실제 최대 frame interval 안이다. native Animator 정밀도/실기 픽셀 동시성을 증명하지 않는다. actual Start callback은 ConfirmSoloAttackPlayback을 호출했고 loop당 Start1/retarget0; 별도 retarget assertion과 일반 loop 관측을 혼동하지 않는다.

`[UAS-SOLO-TIME]`는 실제 owner당 detail64+cap1+Domain terminal1이고 cap 후 incomplete를 명시한다. 실제 최대 UTF-8 body370/GameLog.Compose line465는 production preflight600/envelope 예산780 이하다(최악 runtime 상한 실측 주장 아님). dispatch는 captured Epoch/Sequence/Loop/Hit/Target/Due와 실제 Now/LateMs/AppliedAmount/Status/HP를 기록한다. native event state/clip 및 fallback-current-state는 read-only metadata이고 `ReservationJoin=unassigned`다. full native HP join을 만들거나 metadata로 HP/clock을 구동하지 않는다. native marker observer/인자 구성은 Editor/Development guard 안이다.

중간 View lifecycle 취소 오류는 Main source 검토에서 최종 전 수정됐고 실제 alive View-close HP 보존 및 Domain lifecycle CLR assertion을 통과했다. 별도 허용된 `.claude/mistakes.md` 목차/사건 기록만 추가하며 새 C3 범위·게임 규칙·실기 발생을 주장하지 않는다. 문서 담당 변경 목록은 Research/Plan과 이 사건 추가뿐이다.

### 기존 pursuit 구현 증거 경계

승인된 pure SOLO step helper(remaining distance clamp/actual reached), UnitView SOLO Legacy pursuit의 정확히 한 checkpoint 소비와 bounded GameLog.Dev 진단, 기존 validation 회귀 보강은 적용·소스 인계 검토 완료다. DomainProcessStep·Legacy→Reducer 전환·멀티·에셋·일반 광산/랠리/타이밍 교정은 하지 않았다. Factory 확인과 config-vs-clip 가설 철회는 유지한다. 소스 적용 수용은 runtime 수용이 아니다.

### 최종 적용 manifest 및 독립 검증 — 기존 pursuit 범위

소스 변경은 아래 기존 3파일만이며 Main 전달 diff는 +310/-6이다. [로컬 Receipt](../../../../../../Temp/solo-pursuit/Receipt.md)는 구현·검사 수령 근거이며 공개 게시/원시 로그 분석을 하지 않았다. SHA256은 Main이 frozen 실행 전후 모두 일치 확인한 값이다.

| 실제 적용 파일 | 최종 SHA256 |
|---|---|
| `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` | `7656CF29710CCC48ECEDC2E9861C1EF250B91369678468F31BC90E53F18CA1BA` |
| `Assets/_Project/Scripts/Application/Combat/Sequencing/UnitMovementContracts.cs` | `0FF35145D397E96138E8A10040628511A05BEE36E1A6BD1893A7073B03033958` |
| `Assets/_Project/Scripts/Editor/Combat/RunUnitActionSelfValidation.cs` | `63FC97A66E6B59ACFA94F26F6E033353D106395BEA13DAE06844908244690CB5` |

| 근거 축 | 최종 결과·한계 |
|---|---|
| programmer 원본 RED | helper의 이전 unclamped candidate/missing arrival로 첫 비최종 waypoint 소비 assertion 실패, 실제 exit 1; Receipt tool `a11046`/process `59583`. 현재 exe는 GREEN이며 RED binary 보존·Main 독립 RED·Unity 증상 재현은 아님 |
| 최종 helper 회귀 | programmer GREEN exit 0 및 Main frozen 독립 RunRegression GREEN exit 0. 실제 production helper/checkpoint/validation 추출 범위; Unity/native 원래 증상 재현은 아님 |
| Main 독립 정적 컴파일 | Compile exit 0. Runtime sources303/project303/appended15/omitted0/errors0/warnings23. Editor sources32(project14+Assets/Editor18)/appended0/omitted0/errors0/warnings0. 현재 Unity EditorMode rsp 기반 정적 축이며 Unity native 실행은 아님 |
| 문서 검사 | Main 최종 실제 exit 1, 아래 기존 참조 6건과 정확히 동일; 신규 실패 없음. Task 제외/수동 링크 확인 경계 유지 |
| 보호·적용 위치 | Main Skills/checker/Stats/Game 보호 해시 불변. 다른 코드·에셋 변경 없음. 현재 `codex/docs-checker-maintenance`, main은 fetch refs만이며 merge/push 없음 |
| 미실행·미수용 | native/빌드/실제 경기 NOT RUN. 전체 증상 해결·Root/domain 일치·runtime 수용은 미확정 |

진단은 `[UAS-SOLO-PURSUIT]` begin/repath/consume/attack/close/stall/root-domain 차이/cap 보고다. UnitData lifetime당 detail64+최초 cap1+terminal1, episode당 stall 최대2(2초 무진전 뒤, 실제 root 진행 시 clock reset). detail UTF-8 600 bytes+envelope 예약180의 preflight로 초과를 FormatRejected/incomplete로 표시하며 정상처럼 truncate하지 않는다. cap/거부 계수는 terminal까지 유지한다. Initialize의 참조 재사용 때 lifetime 계수를 reset하고 OnDisable에서 종료한다. 매 frame raw 출력은 없으며 DomainProcessStep을 추가하지 않아 Root/domain 차이는 여전히 후속 범위다.

**2026-10-14 helper 실행 증거 정정(Main 전달):** Main은 실제 GREEN 실행/exit 0만 독립 확인했다. script/model의 과거 RED 단계 읽기를 실제 RED 실행으로 잘못 전달한 초기 보고는 즉시 정정·철회됐다. Main 독립 RED 또는 독립 RED→GREEN을 주장하지 않는다. programmer 원본 RED와 최종 manifest/검사 범위는 위 표로 인계 완료됐으며, 한정된 알고리즘 실행과 Unity 원래 증상/실기 수용은 구분한다.

## 증상별 증거 경계

| 증상 | 사용자 관측 보고 | 정적으로 아는 것 | 재현·추가 증거 필요 |
|---|---|---|---|
| 근접 공격 불발 | SOLO에서 공격이 나가지 않음 | 근접 판정은 같은 타겟의 생존·거리·방향·회차 조건을 구분해야 함 | 어떤 유닛/타겟인지, 진입·예약·marker·HP 중 끊긴 단계 |
| 광산 통과 | SOLO 유닛이 광산을 통과 | 광산은 영구 이동 불가; 건설 불가 구역의 이동 허용과 다른 계약 | domain MineKind/walkability, 실제 이동 선분, 표시 위치 대조 |
| 공격 후 제자리 걷기 | 짧은 stationary Walk | 위치 정지와 표현 정지는 별도; 일반 Held와 공격 handoff도 구분 | SOLO Recovery→재개 경로, 해당 frame의 실제 이동·Animator 상태 |
| 공격 타이밍 어긋남 | 모션과 타격 시점 불일치 | 서버 판정 시각과 로컬 marker는 별도; 애니메이션 동일성은 실행 시계 동일성이 아님 | SOLO loop/cooldown/AttackSpeed/직접 marker 경로와 HP 시각 |

## 초기 비교 사실 — Main 전달, 범위 한정

다음은 2026-10-14 Main의 전체 경로 비교 보고다. 이 문서 작성자는 `origin/main` 해시와 UnitCombat diff를 직접 확인했으며, 나머지 동일성은 Main 전달 근거로 표시한다. 집계 수치는 독립 재계수 전 복제하지 않는다.

- `0de4f868→origin/main`: 전체 `*.anim`·`*.controller` 및 Unit 프리팹 diff가 없다는 Main 보고. 씬 Bootstrap 유닛 config 참조 변경은 발견되지 않았다는 보고다.
- `Assets/_Project/Scripts/Application/UseCases/UnitMovementUseCase.cs`, `Presentation/Unit/UnitView.cs`, `Infrastructure/Network/NetworkCombatController.cs`, UnitStatsConfig, TorrentLaunch/WavePresentation은 Main의 전체 blob 비교에서 동일. 축약 표기의 실제 전체 경로 목록은 programmer 인계 후 확정한다.
- 직접 확인: `Assets/_Project/Scripts/Application/UseCases/UnitCombatUseCase.cs` diff는 HoT 텍스트 규칙 인용 주석 변경뿐이다. 이것을 전체 코드 무변경으로 확대하지 않는다.
- Main 보고: `Game.unity` diff는 텍스트 비움, `_autoReturnSeconds` 30→60, Disconnect 30000→60000. GameConfig는 FlatTop 10×20→11×21 및 test-gold 필드 제거. 변경 프리팹은 NetworkMapTransfer·ConfirmPopup·Bloom/Mushroom VFX 범위다.
- Map bootstrap/projection/gridRenderer/network-map 경로에는 변경이 있다. 동일 유닛 코드라도 다른 맵 입력·연결·SOLO 분기를 소비할 수 있다. 이전 교정 소실 주장은 파일 전체 diff/blob와 caller 연결을 확인하기 전 확정하지 않는다.

## 우선순위 조사 항목 — 원인 확정 아님

1. **SOLO 실행 분기:** `NetworkContext.ActiveUnitMovementPipelineMode` getter 내부에서 `MovementPipeline.IsMatchActive` latch가 false이면 Legacy를 반환하는 경로와, `UnitView.Initialize`가 `!NetworkActive`일 때 명시적으로 Legacy를 선택하는 경로를 구분한다(Main 정정 전달). latch를 NetworkContext의 static property로 취급하지 않는다. Legacy AStar/FlowField/Chase와 공격 loop의 실제 caller를 추적하며, 멀티 B3 보존으로 SOLO corridor 안전을 주장하지 않는다.
2. **광산과 이동 입력:** 맵 projection의 MineKind→walkability 및 SOLO 이동 선분 검사를 비교한다. 가설: 안전 검사를 우회하는 직선 이동이 있으면 A* 경로가 광산을 피하더라도 실제 root가 관통할 수 있다. 반증: 실제 사용된 모든 선분 경로에서 동일 차단 검사가 보장되는지 확인.
3. **근접 진입·재개:** SOLO Chase→attackLoop 및 Recovery→AStar/FlowField 전환의 guard·target·취소 순서를 확인한다. 코드에서 가능한 실패 조건과 실제 발화는 구분한다.
4. **SOLO 시간축:** cooldown, AnimationAttackSpeed, direct marker와 authoritative-marker 비활성화 조건을 같이 확인한다. 가설: scheduler와 clip 진행의 서로 다른 시간 원점/배율이면 불일치가 가능하다. 반증에는 같은 공격의 예약·marker·HP 시각이 필요하다.

위 목록은 초안 시점의 조사 항목이다. **2026-10-14 최종 인계 수신으로 감사 대기는 종료했으며, 아래 최종 근거가 코드 감사 판정이다.** 당시 새 로그 추가는 미승인이었고, 이후 승인된 진단 범위는 상단 「현재 구현 증거 경계」를 따른다.

### Main 추가 SOLO 코드 근거 — 전달값, 후보이지 확정 버그 아님

아래 행은 Main이 읽은 `88f60e38:Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` 기준이다. 문서 담당의 독립 재독과 변경 커밋 판정은 대기한다.

- `Initialize:1601`에서 NetworkActive가 아니면 Legacy 모드를 선택한다.
- `ApplyMovementAuthorityFrame:2083`의 Legacy 분기는 candidate를 즉시 `transform.position`에 적용한다. spatial corridor preflight는 그 뒤 Reducer 분기에 있다. 이 함수의 검사 분리 사실은 caller가 이미 candidate를 검증했는지까지 설명하지 않는다. 광산 통과 후보 판정에는 AStar/FlowField/Chase의 candidate 생성·사전 검사를 이어 읽어야 한다.
- `EnterCombatLoopV3:5623` SOLO 경로는 TryAttack의 pending-hit delay로 예약한다. 같은 타겟에서는 첫 Start event만 있으며 매 cycle native epoch는 없다는 Main 보고다. `StartCombatAnimation`은 TransferToAction과 snap rotation을 한다. scheduler·clip/AttackSpeed·direct marker의 반복 주기가 실제로 어긋나는지는 미확정이다.

이 차이는 SOLO 우선 조사 근거다. 오래전부터 있던 분기인지 새 map 입력으로 드러난 조건인지, 근접 불발/Walk-after-Attack과 같은 인과인지도 변경 이력·caller 감사 전에는 확정하지 않는다.

## 2026-10-14 최종 코드 감사 — 현재 판정

두 programmer 인계와 Main 독립 검증을 종합한다. 아래 행은 `88f60e38` 기준이며, `S=Assets/_Project/Scripts/`, `V=S/Presentation/Unit/UnitView.cs`로 표기한다. 정적 결함·조건부 후보와 실제 사용자 증상의 원인은 다른 판정이다. 원시 감사 tool 출력은 별도 영속 보고서로 보관되지 않았으며 이 절은 전달 근거의 요약이다.

1. **확인된 정적 결함 — SOLO 추격 waypoint 소비 정지.** `V:1601` SOLO Legacy 선택 → `V:2061` `_lastMovementTrajectoryReachedWaypoint=false` 초기화 → `V:2083~2095` Legacy candidate 위치 적용 후 반환. true 대입은 Reducer의 `V:2522`에만 있다(Main 독립 확인). 그런데 비동일 타일 추격의 `V:5530~5532`는 Advanced·이 flag·pursuitCheckpoint를 모두 요구해 index를 증가시킨다. Legacy 다중 waypoint가 첫 점 도착 뒤 소비되지 않는 코드 결함이다. consumer 도입 `7b4a8927d`(2026-08-25), Legacy return `65ee51fa5`(2026-08-18)는 Main blame 확인. 근접 불발과 맞닿는 후보지만 실제 발생 연결은 미증명. 반증 관측: 문제 SOLO 공격자가 해당 비동일 타일·다중점 분기를 사용하지 않았다면 이 결함은 그 사례의 원인이 아니다. 현재 그 경로/도착/공격 진입을 묶는 새 로그가 없다.
2. **확인된 unsafe-goal 경로 — 광산 목적지와 중간 통과 분리.** `S/Presentation/Input/InputHandler.cs:219` → `S/Presentation/UI/ProductionPanelUI.cs:565` → `S/Application/UseCases/UnitProductionUseCase.cs:465` → `S/Presentation/Production/ProductionTicker.cs:307` → `S/Application/UseCases/UnitMovementUseCase.cs:225` RequestMoveFlowField. `S/Domain/Hex/HexFlowField.cs:218~225`는 nonwalkable goal을 끝에 붙인다. `V:4928~4930`은 그 중심을 실제 root 목표로 쓰지만 `V:4973` domain ProcessStep은 제외한다. Legacy writer에는 그 물리 endpoint 차단 검사가 없다. append 도입 `dfe3edbd0`, skip-step `659fc0e3d`, writer `65ee51fa5`는 programmer 이력 전달. 광산을 **목적지로 지정한 사례**의 위험이지 사용자 **중간 광산 관통** 원인 확정이 아니다. 반증 관측: goal이 walkable이고 광산이 중간 선분에만 있으면 이 설명은 부족하다. goal/MineKind·실제 선분·domain 점유의 같은 시점 근거가 없으며 기존 랠리 보류를 확대해 수정하지 않는다.
3. **타이밍 가설 정정 — config/clip 차이를 runtime 주기 결함으로 판정한 설명은 철회.** Main 독립 에셋 확인: Assault(type1) looping clip 0.33333334초/speed1, config cooldown0.2·첫 hit0.16666667; BattleAxe(type5) looping clip3.166667초/speed1, config cooldown3.05·첫 hit1.02. 두 첫 OnAttackHit marker는 config와 일치하며 decimal 오해 근거가 아니다. **후속 programmer 확인:** `S/Bootstrap/GameBootstrapper.Setup.cs:59~88`→`S/Domain/Unit/UnitData.cs:169`의 config 복사만으로 최종값을 판단할 수 없다. `S/Infrastructure/Factories/UnitFactory.cs:273,287,519` 공통 SOLO/Server 생성 경로가 nonhealer 양수 clip 길이로 cooldown을 덮어쓰고 marker도 교체한다(Fox 의도 예외). 정상 생성 Assault 최종0.333333/BattleAxe3.1667이며, `38f56aebe` 및 `b65751c6`에서도 보존됐다는 programmer 보고다(Main은 Factory `259~299/510~551`의 Initialize→GetAttackTimelineClip→ApplyAttackClipCooldown→marker 교체→SetDependencies/Observe 경로를 직접 읽어 최종값 덮어쓰기 코드 사실을 독립 확인 완료했다; runtime은 미확정). 같은 타겟에서 새 Start가 없는 `V:5623`→`S/Application/UseCases/UnitCombatUseCase.cs:553,592` 반복 scheduling은 **frame drift 조건부 후보만** 남는다. 기존 Factory `:547`의 `[ASSAULT-TIMELINE]`/`[SNIPER-TIMELINE]`에서 roleSolo·fallbackCooldown·finalCooldown·finalHit로 최종값을 반증 가능하게 확인할 수 있으나 이번 새 실행 증거는 없다. LittleKnight type3와 LionKnight type22를 혼동하지 않는다(Lion 첫/둘째0.6/1.4333).
4. **Walk-after-Attack — 조건부 전환 후보.** `V:5623` ClearState→`V:7342` Resume에서 speed1 복구 후 root 이동은 뒤따른다. `_isMovementHeld` guard가 있으므로 실제 flag 상태 없이 stationary Walk를 확정하지 않는다. 같은 frame의 Recovery/held·speed·root 변화가 필요하다. 기존 `[UAS-PC-RECOVERY]`(`V:596~601`)는 해당 분기/필드가 관측되는 범위만 증거이며 SOLO 전체 Animator trace를 대신하지 않는다.
5. **맵 교정 소실·이중 변환 주장 미확정.** Main 맵 감사는 projection `S/Bootstrap/GameBootstrapper.Map.cs:191`→CreateUseCases가 같은 grid를 사용하고 RenderGoldMines `S/Presentation/Grid/HexGridRenderer.cs:459` 본문은 비교 diff hunk가 없으며 HexToWorld→ToView가 한 번이라고 보고했다. 기존 full-blob/asset 비교 범위를 유지하고 모든 코드·새 맵 runtime 정상으로 확대하지 않는다. `V:5458` clamp 부재는 새 사례의 조건부 후보이며 중간 광산 관통 원인은 OPEN이다. 실제 맵 hash·mine 좌표·root/view trajectory의 대조가 부족하다.

**분석 인계 완료 / runtime 미확정.** 분석 인계 당시 교정은 미승인이었으나 이후 사용자가 상단의 좁은 SOLO 범위를 승인했다. 이 절의 코드 근거와 이전 초안은 구현 전 감사 스냅샷이다. 실제 구현·검사 인계는 상단 최종 표에 완료 기록됐으며 원인 전체 확정·실기 PASS는 아니다.

## 규칙 원본과 증거 부족

기준 원본: [Units](../../../GameSystemRules/GameSystemRules_Units.md)의 규칙 2·6·10·11·14, U-MOV-REPATH·U-ATK-TIMELINE·U-IMPACT-TARGETLOCKED; [CombatSync](../../../GameSystemRules/GameSystemRules_UnitCombatSynchronization.md)의 NET-AUTH-001·NET-ROOT·NET-TIME-001~004; [RandomMap](../../../GameSystemRules/GameSystemRules_RandomMap.md)의 규칙 1·3·9·10·11. 광산과 건설 불가·완전 차단을 혼동하지 않는다. source/event marker는 사용자 기준 `ss:ff@30fps`로 해석하며 `ss+ff/30`초다.

부족한 근거: 문제 실행 빌드/커밋·유닛 종류·맵 유형/seed·타겟 종류·발생 시각·새 SOLO 로그/영상, `b65751c6→0de4f868→88f60e38` 전체 caller 비교, 증상별 실행 분기. 사용자가 재테스트할 수 있으나 현재 결과는 없다. 기존 로그에 필요한 필드가 없으면 관측 불가로 남기며 새 계측·실기 승인을 추정하지 않는다.

## 문서 검사 경계 — Main 전달

Main 실행 `check_docs`는 exit 1, 기존 상시 참조 문제 6건이다(전달값, 문서 담당 재계수 아님): `.claude/mistakes/unit-action-correction.md:45` NET-PRESENT-001·`:321` NET-PRESENT-003의 Units 범위 오인, `Assets/_Project/Docs/GameSystemRules.md:107` NET-DELIVERY-TRAVELING·NET-CANCEL-004의 Units 범위 오인, `Assets/_Project/Docs/History/ImplementationVerificationHistory.md:3397` UI 규칙 11의 섹션 모호성, `Assets/_Project/Docs/TechnicalDesignDocument.md:1521` UI 무작위 맵 제목 참조 부재. 이 위치는 Main 검사 시점 기준이며 이번 범위에서 수정하지 않는다.

새 `_Tasks`는 검사 대상에서 제외된다. Main은 이 Task의 `../../../` 링크 경로를 수동 확인했다. 따라서 검사 PASS·Task 자동 검증·게임플레이 정상 판정을 선언하지 않는다. 메모리 기준선 증가 알림 `+3662`도 Main 전달값이며 기존 변경에 관한 것으로, 이번 메모리 편집이나 기준선 갱신은 없다.

## 전 유닛 감사 최종 판정 — Main 검토 완료 / 읽기 전용 보고

**현재25종의 과거 교정 marker artifact는 유지된다. 그러나 original ss:ff와 현재 literal 초값의 차이는8종에 남아 있으며 「전부 정확/정상」 결론은 아니다.** 위25행 historical 출처와25행 current 값 표를 Main이 검토 승인했다. 표의 초안/추가인계 대기는 당시 단계로 보존하며 이 절이 현재 최종 한정 판정이다. 숫자는 현재 enum/Stats 원문 열과 Task 출처를 대조했다. Stats10/13의 구현값 mirror를 historical correctness 기준으로 삼지 않았다.

### 1. 보존·현재 연결 근거와 출처

- **Main 독립 확인:**25개 actual clip marker·config fallback·Factory 정책 대조, enum Human8/Spirit9/Transcendence8 확인. source5 최종 SHA가 앞선 SOLO 교정 인계와 동일하다. config fallback 자체는 최종 Factory runtime 값이 아니다.
- **보존 축:**`b65751c6`(final10/12 combatcommit) 대비 Animations/Units·UnitStatsConfig.asset·UnitFactory.cs·Prefabs/Units의 Main read-only diff stat는 EMPTY다. 이 정확한 경로의 clip/config/Factory/prefab rollback 근거는 없으며 원래 교정의 정확성 증명으로 확대하지 않는다.
- **담당 최종 인계/Main 검토:**Human+Transcendence16종/32프리팹 production wiring와 Spirit9종 대응 wiring·프리팹 감사가 완료됐다. 이 수령 근거와 Main의 직접 scene/25clip 확인은 별도 축이다. 새로운 native gameplay나 모든 모델 방향/시각 타이밍 측정이 아니다.
- **Game.unity Main 전체 diff 검토:**변경은 텍스트6곳 비움·autoReturn60·disconnect60으로 unit production wiring 변경이 아니다. 기존16줄 diff를 unit Factory 참조 회귀로 해석하지 않는다.
- marker별 actual값/과거 source 링크는 위25행 표에 보존했다. SpearMan은 [9/30 Plan](../../2026-09-30/18_02_spearman-attack-timing-correction/Plan.md) L3/L11/L29/「사용자 실기 결과 — focused PASS/CLOSED」L39~43의 사용자0:25 수용을 직접 확인했다. current.8333333은 그 결정 유지이며 historical unknown으로 남기지 않는다. Fox Plan§16/§21의 event1/HP2.25 예외도 과거 수용 근거가 있다. Rabbit0:20·Boulder1:10·Flame1:27·Bloom clip4는 실제 후속 결정으로 옛값을 대체했다.

### 2. original ss:ff 차이8종 — retained artifact와 다른 판정 축

원문 표기는 현재 StatsReference의 **보존된 old 표기 열**에서 직접 확인한 것이며 새 의도 승인으로 사용하지 않는다. 초 환산은30fps 산술 비교다. 현재값은 Main 독립 clip 추출이다.

| 이름 | 원문 ss:ff와 환산(초) | 현재 marker/옛 literal artifact(초) | 최종 한계 |
|---|---|---|---|
| LittleKnight | 0:25/1:15 →25/30/1.5 | .25/1.15 | 공통 oldTask literal과동일. originalframe최종승인 primary는 unverified historical final |
| BattleAxe | 1:17 →47/30 | 1.02 | old 전용Task explicit1.02 보존. 원문notation과 과거 교정초값의 차이지 이번merge 소실 아님 |
| Tank | 0:17 →17/30 | .16666667 | old 실행≈.167 보존, originalframe의 변경/최종승인 primary는 unverified historical final |
| CannonCart | 0:17 →17/30 | .16666667 | Tank와 같은 한계. config fallbackhit4와 actualmarker를 구분 |
| DustSpirit | 1:04 →34/30 | 1.04 | old 전용Task explicit1.04 보존. decimal을 새로frame로 재해석/자동수정하지 않음 |
| RhinoBreaker | 1:05 →35/30 | 1.05 | old actualAttack marker 주입Task의 explicit1.05 보존. originalnotation 의미 문제와 분리 |
| EagleArcher | 0:10 →10/30 | Attack.1/Attack2 event없음 | originalframe최종승인 primary와 실제 runtimeAttack선택 미확인. 개별 사용자수용은 취소하지 않음 |
| StreamSpirit | 0:17 →17/30 | .5 | 위7종에 더한8번째. oldTask explicit.50/focused수용 artifact 유지, originalnotation 재해석은 별도 |

이8종을 「8개 버그」·「8개 rollback」로 집계하지 않는다. oldTask 초값 유지 사실과 originalframe 의도 확인은 독립이다. source/clip이 과거와 같아도 최초 환산/교정의 의도는 추가 historical 근거가 필요할 수 있다. 반대로 Task에 명시된 후속 값이 있으면 옛 표기만으로 새 correction을 선택하지 않는다.

### 3. 주기·선택·보조 참조 한계

- **Bear/Lion:**oldTask config cooldown1.2/3 보존과 current clip length1.6666666/2는 서로 다른 입력 축이다. Main 확인 pre38f56 Factory부터 nonhealer clip.length로 실효주기를 읽는 정책이 있어 현재 실효1.6666/2는 새 cycle 교정/merge reversion이 아니다. Bear marker0:13/Lion0:18·1:13 사용자 수용을 cycle 설계 전체 승인으로 확대하지 않는다. Bloom은 사용자 승인 clip3→4와 healer cooldown3 예외라 Bear/Lion과 합치지 않는다.
- **Eagle 선택:**Attack marker.1과 Attack2 event없음은 actual asset 확인값이다. Factory의 controller.animationClips 열거에서 어느 것이 실제 선택됐는지 현재 native 관측 proof가 없다. 모호성은 explicit missing current proof이며 현재 잘못된 clip이 골라졌다는 결론/교정 선택은 없다.
- **Quake 보조 GUID:**Main exact-guid `.meta` 검색의 exit1/0개 결과로 `e36150d12a3f7474092bb70a6524596d`가 해소되지 않았다. controller Jump Attack 1(state141/motion154)의 dangling 후보지만 **정상 Attack(state128)의75f086… clip과1.667 marker는 존재**한다. pre-b657에도 동일 GUID라 existing unresolved asset reference이며 merge reversion·정상Attack누락·현재runtime원인 아님. 실제 animationClips runtime순서에서 문제가 발생했다고 단정하지 않는다.

### 4. 새 SOLO clock과 미완 근거

10/14 일반23종 shared SOLO occurrence clock 경로 변경은 이번 승인된 source 교정이다. clip/config/prefab의 과거 교정 소실이 아니다. positive sorted offset만 clock을 사용하고 ZERO/invalid/unsorted는 기존 Legacy fallback, Torrent/Healer는 제외다. prior pursuit 및 source5/CLR frozen GREEN 근거는 유지하지만 모든23종 실기 시각 수용으로 승격하지 않는다. 모델import/yaw·VFX endpoint·회차 종료/복구의 exact visual은 앞선 historical 표의 이름별 계약/한계 이상으로 검증하지 않았다.

Main의 이전 「현재2.25 timer 확인 뒤 peer 비교부터」 권고는 잘못된 요청 순서였다. `.claude/mistakes.md`에 별도4칸 사건으로 기록 완료했고 그 peer-test 문단은 이전 focused 범위 이력으로만 보존한다. 지금 첫 결론은 historical/current 계약 감사 보고이며 새멀티테스트가 먼저가 아니다. 새 교정·구현·build·Unity·QA·전체재테스트는 선택/승인되지 않았다. 사용자 Fox 시각 우려 및 native/pixel/전체 runtime 수용은 OPEN이다.

**최종 문서·보호:**Main은 새절 링크39개/누락0과 source5 해시 불변을 확인했고 check_docs 최종 실제exit1/정확히 동일한 기존참조6건을 인계했다. Task는 자동검사 제외이며 link 확인은 별도다. 이 Task-only 상태 갱신에 document-manager가 검사기를 병렬/추가 실행하지 않는다. 기존 source·Stats·규칙·다른문서·메모리 수정 없이 두 Task 문서와 별도 승인된 실수 사건 추가만 인계한다.
