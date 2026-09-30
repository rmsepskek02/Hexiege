# DustSpirit 공격 클립 명시 연결 조사

DustSpirit에는 공격처럼 이름 붙은 애니메이션이 두 개 있다. 실제 공격에 쓰는 클립을 명시해 두지 않으면, 클립을 읽어 오는 순서가 바뀔 때 공격 주기와 타격 시각의 기준이 달라질 수 있다. 이번 조사는 그 위험을 production 에셋과 생성 경로에서 확인하고, 다음 구현이 고정해야 할 연결을 정리한다.

**상태:** 구현·자동 검증 PASS / 두 경기 Dust 49기 focused 로그 확인 / CONDITIONAL PASS·OPEN. 명시적 사용자 육안 최종 수용 전 Complete 아님. 아래 재테스트 결과가 최신이며 이전 실기 대기는 이력이다.

## 확인한 production 사실

| 대상 | 확인 결과 |
|---|---|
| UnitType | `DustSpirit=13` |
| Controller | `DustSpirit.controller` GUID `c6b047497cf8c3b41bd1f1757edd7ff0`; Blue/Red 프리팹이 함께 참조 |
| 실제 `Base Layer/Attack` motion | `DustSpirit_Attack.anim` GUID `961cafd21c1fa13429c74aa65a3a6345`, 길이 3초, `OnAttackHit` 1개 @ 1.04초 |
| 별도 `Attack2` motion | `DustSpirit_Attack2.anim` GUID `621d10329be07614ea53d32c996ded63`, 길이 4.6333337초, marker 0개 |
| 설정 | `UnitStatsConfig.asset` 명시값 `unitType: 13`, `attackCooldown: 3`, `hitFrameTimes: [1.04]` |
| 실제 씬 등록 | `Game.unity`의 `UnitFactory._spiritPrefabs` DustSpirit 항목(type 13)에 Blue/Red 프리팹 연결; 공격 클립 명시 필드는 현재 없음 |

`UnitFactory.cs`의 `GetAttackClipLength`와 `GetHitFrameTimes`는 모두 Animator controller의 `animationClips`에서 이름에 `Attack`이 포함된 **첫** 클립을 고른다. 서버/싱글 생성 경로와 멀티 Client 생성 경로가 각각 이 두 함수를 호출한다. 만약 `Attack2`가 먼저 열거되면 길이 4.6333337초가 쿨다운을 덮어쓰고, marker가 없어서 수동 설정 `[1.04]`는 남는다. 이는 소스상 가능한 순서 의존 실패 형태이지, 현재 실기에서 그 순서가 선택됐다는 증거는 아니다.

`UnitCombatAssetMatrix.md`의 DustSpirit 행도 복수 Attack 클립 선택 순서 위험을 `Provisional + MigrationRequired`로 기록한다. 발사체 에셋이 준비되지 않아 projectile/tracer 후속은 이번 범위에서 보류한다. 이 작업은 DustSpirit의 실제 `Attack` motion 선택을 명시하는 데 한정하며 `Supported` 승격이나 gameplay migration 완료를 뜻하지 않는다.

## 규칙·검증 경계

- `GameSystemRules_Units.md` 「전투 연출 동기화 규칙」 규칙 17: 일반 공격 marker는 실제 Attack 클립 이벤트에서 추출; marker가 없을 때만 설정값 폴백. FoxMagician의 charge VFX/피해 시점 분리 예외는 유지한다.
- 같은 문서 규칙 18·19·22: 서버 타이머 권위, 확정 결과 표현, 값 기반 애니메이션 상태를 변경하지 않는다.
- 같은 문서 `U-ATK-TIMELINE` 및 `GameSystemRules_UnitCombatSynchronization.md` `NET-PRESENT-002`: 서버가 읽는 검증된 타임라인과 표현 클립의 대응이 깨지지 않아야 한다.
- 영구 검증은 단순 이름/설정 대조가 아니라 실제 씬 항목→Blue/Red prefab controller→`Base Layer/Attack` motion과 명시 참조가 같은지, 이 참조로 3초/[1.04]가 추출되는지 확인해야 한다.

## 구현 반영

`UnitPrefabEntry.attackTimelineClip`을 추가하고 Game 씬의 DustSpirit 항목에 실제 Attack clip을 연결했다. 서버/싱글과 Client는 `GetAttackTimelineClip`에서 한 번 선택한 클립을 길이·marker 추출에 함께 전달한다. 기존 미지정 항목의 선택 방식과 FoxMagician 설정 피해 시점 보존은 유지했다.

기존 Unit Action 검증에 Game 씬 preview의 실제 등록, 양 팀 prefab/controller/Attack motion, 설정과 생산 추출값을 대조하는 항목을 넣었다. 같은 선택 함수에 Attack2-first 목록을 전달해 기존 경로의 4.63초/marker 없음과 명시 연결의 3초/[1.04]를 구별한다. preview는 검사 후 닫으며 씬을 저장하지 않는다. 전용 런타임 진단 로그나 Testcase는 추가하지 않았다.

## 2026-09-28 사용자 재테스트 결과 반영

원본은 `Assets/_Project/Docs/_Logs/2026-09-28/03_50_logcat/RuntimeLog_device.txt`와 `Assets/_Project/Docs/_Logs/_editor/2026-09-28/RuntimeLog.txt`다. Editor Host / Android Client 동일 `sharedSessionKey=be8b171a5d2d58c113c4702b8ea5d8d054bf5855322f2ad45c097796bd94d4f5`의 **두 경기**를 runId/startedAt으로 분리했다. `03_41_logcat`은 겹치는 gameplay 캡처이므로 합산하지 않았다. Host attack runId는 `0e16018819d949288f14f378c63f6d83` / `bebba109c44540b7bf81caa3a49c4958`, Client는 `fc947d78f77d4d27bb90a2bcf0462472` / `74702837e689471c960b66edb37c34ab`이다.

**DustSpirit: focused runtime evidence 확인 / CONDITIONAL PASS·OPEN.** 실제 생산 Dust 49기/Fox 10기이며 이번 실제 생산 타입 중 Supported는 Dust뿐이다. 서버 결과 84+166=250과 Client 수락 250이 일치하고 각 peer 필수 표현은 83+153=236/236, 결과·표현 실패/중복/공간 mismatch/recovery 0이다. Host 이동 52,714+94,799=147,513 frame에서 gate/handoff/stationaryWalk/errors 0이며 이 이동 집계는 경기 전체이지 Dust 전용 집계는 아니다. 각 경기 양 peer local ROOT PASS지만 공식 CrossAudit 실행 결과는 아니다. 초기화 지연 59건은 59건 완료, retry failure 0이다. 게임 로그 ERROR/FATAL/실제 Exception 발생 없음(스택의 Exception 매개변수 문자열은 제외). Dust 공격 접촉·대상 전환 등에 대한 명시적 사용자 육안 수용은 없어 Complete/최종 PASS로 닫지 않는다. 이전 17_21 경기의 Dust 부재 판단은 당시 사실로 보존하며 이번 두 경기에서 해소됐다.

**Fox 표시 지연 focused 교정: 사용자 수용 / 현행 타이밍 유지.** 양 peer에서 received/emitted 61쌍씩이 모두 같은 frame, immediate=True, dispatch=text-played다. localTime 차이의 최대는 Host 7.097ms / Client 2.445ms, 평균은 1.07182ms / 1.71633ms다. 이는 큐 수신→텍스트 생성 호출 성공 구간이며 VFX 종료→동일 공격 피해 표시의 exact 측정이 아니다. VFX 실제 시작은 Host 14+65=79, Client 14+64=78, 실패 0이다. 미완료는 Host 1/Client 1이고 원인은 미확정이다. 종료 disconnect 경고를 그 원인으로 단정하지 않는다. 사용자는 약간 이른 느낌은 있지만 현재 Fox 타이밍을 명시적으로 수용했으며 재튜닝은 향후 발사체 구현 때로 미룬다. VFX 1.00초/피해 2.25초를 유지한다. 이번 표시 지연 focused 범위는 수용됐으나 완벽한 화면 일치, 권위 projectile/tracer, 전체 migration·25종·역할교대·rollback 완료로 확대하지 않는다. MigrationRequired / Unresolved / LegacyFallback 유지.

이번 변경은 승인된 결과 문서 반영만이며 코드·TC·새 Task·빌드 실행은 하지 않았다. 다음 유닛은 별도 조사·선정 대상으로 남기며 이 문서 갱신이 구현 착수 승인은 아니다.
