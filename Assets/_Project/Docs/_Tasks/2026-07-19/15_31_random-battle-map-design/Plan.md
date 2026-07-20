# Plan — 11×21 FlatTop 무작위 대전 맵 문서화 및 구현 준비

## 이 작업이 무엇인지

구현자가 임의로 해석하지 않도록 무작위 맵의 크기, 다섯 맵 유형, 광산과 초기 골드, 건설 제한, 대칭, 검증과 폴백을 문서에 먼저 고정한다. 이번 작업에서는 코드를 바꾸거나 QA를 수행하지 않는다.

---

## 문서 반영 계획

1. `GameSystemRules_Map.md`
- 모든 맵 요소와 장식의 exact 180° 대칭을 보편 규칙으로 확정하고, 플레이어 소유 오브젝트의 외형은 맵 시각 대칭 대상에서 구분
   - 중앙 단독 광산과 대칭 광산 쌍의 검증 방식을 분리
   - 시작 조건과 정적 접근성 완료 기준 정정
2. `GameSystemRules_RandomMap.md` 신규
   - 11×21 FlatTop 공통 사양과 다섯 맵 유형별 생성 수치
   - 광산 수/초기 골드, 건설 불가/차단 지형, seed·재시도·폴백·로그·검증 규칙
   - 건물로 완전히 막힌 경로의 후속 동작 기록
3. `GameDesignDocument.md`
   - 자원, 타일, 맵 크기/유형, 건설 불가, 장애물, 경로 차단 경험 갱신
4. `CONTEXT.md` 신규
   - 맵 유형, 시작 광산, 중립 광산, 중앙 단독 광산, 대칭 광산 쌍, 건설 불가 구역, 완전 차단 지형 등 확정 용어만 기록
5. `GameSystemRules.md`와 `AGENTS.md`
   - 새 규칙 및 용어 문서 인덱스 등록
6. `PROJECT_STATUS.md`, `ROADMAP.md`, `WORK_HISTORY.md`
   - 설계 확정·구현 예정 상태를 일관되게 기록

각 항목의 근거는 `GameSystemRules_Map.md`의 맵 변경 시 재검증 규칙과 `WORKFLOW.md`의 상시 문서 동기화 규칙이다.

---

## 후속 구현 단위

1. 아래 계약의 완성된 `MapDefinition`을 도메인 전달 형식으로 구현한다.
2. 맵 config를 FlatTop 11×21로 전환하고 성·시작 광산 위치에서 초기 소유권을 파생한다.
3. seed와 180도 회전 변환을 단일 소스로 만드는 생성 컨텍스트를 구현한다.
4. 건설 불가 구역과 완전 차단 지형의 도메인 상태·표현·UI 피드백을 구현한다.
5. 다섯 맵 유형을 독립 생성 전략으로 구현한다.
6. 광산 수 선택, 배치, 초기 골드 매핑을 구현한다.
7. 대칭·고유 시작 타일·도달성·교차 접근성·유형 제약 검증기를 구현한다.
8. 100회 재시도, 동일 조건 폴백, 재현 로그를 구현한다.
9. 건물로 완전히 막힌 유닛의 적 차단물 공격/아군 `Blocked` 상태를 별도 기능으로 구현한다.
10. 기존 재경기 요청/수락/거절·Game 씬 재로드 경로에 `RematchMapMode`와 정의 교체 생명주기를 추가한다.

PointyTop 좌표와 생성 규칙은 이번 후속 구현에도 포함하지 않는다.

### 범위 밖 — 전역 게임 프로토콜 호환성

`GameProtocolVersion`/build 호환성의 전역 관리는 무작위 맵 구현 범위에 포함하지 않고 별도 중요 작업으로 진행한다. 별도 작업은 matchmaking same-version filter, custom lobby의 Relay 진입 전 검사, NGO connection approval/rejection, reconnect 버전 재검증, update-required UX를 함께 다룬다.

이번 범위에는 canonical map binary 형식을 식별하는 임시 `MapVersion`(`int`, 초기값 `1`)만 포함한다. 이는 미지원 형식 deserialize를 차단하기 위한 값이며 matchmaking, 앱 업데이트, 전역 connection compatibility를 판단하지 않는다. Host/Client의 `MapVersion`이 다르거나 지원하지 않으면 map preparation을 실패 처리한다.

### `MapDefinition` 확정 계약

`MapDefinition`은 생성 도중의 후보나 seed만 담는 명령이 아니라, 검증을 통과해 즉시 같은 전장을 구성할 수 있는 **완성본**이다.

**상위 메타데이터:**

- `MapVersion`(`int`, 초기값 `1`)
- 64-bit root seed
- 맵 유형
- 크기(11×21)와 orientation(FlatTop)
- 중립 광산 수
- `MapTestModeEnabled` 실제 표식(canonical에서는 고정폭 정수 `0`/`1`)
- 실제 초기 골드
- 최종 해시

**타일 데이터:**

- 정확히 231개를 row-major 정규 순서로 저장한다.
- 배열 인덱스 자체가 offset 좌표의 단일 표현이다.
  - `index = row * 11 + col`
  - `col = index % 11`
  - `row = index / 11`
- 각 타일은 `TerrainKind`(`Open`/`Blocked`)와 `BuildRule`(`Allowed`/`NoBuild`)을 담는다. `InitialOwner`와 장식 상태는 타일 레코드에 저장하지 않는다.

**오브젝트 배치:**

- 성 위치와 팀
- 시작 광산 위치와 팀
- 중립 광산 위치
- `DecorationDefinition` 목록: 위치와 `typeId`, `materialVariantId`, `scaleStepId`, `rotationStepId`의 정수 식별자

장식 목록도 `MapDefinition`·canonical codec·SHA-256 hash에 포함한다. 장식의 스케일·재질·회전은 정수 식별자로만 표현하며 해시 원본에 float를 넣지 않는다.

**단일 소스 원칙:**

- 성·광산의 정체성, 팀, 위치는 오브젝트 배치 목록만이 권위 원본이다.
- 타일 데이터에 별도의 `HasCastle`/`HasGoldMine` 같은 중복 정체성 상태를 저장하지 않는다.
- 타일 데이터의 `TerrainKind`/`BuildRule`은 정적 지형과 건설 규칙만 표현하며 성·광산 정체성을 중복 인코딩하지 않는다. 런타임 이동 가능 여부는 배치 목록에서 투영한 `MineKind`와 동적 `HasBuilding`을 함께 계산한다.
- 생성, 검증, 직렬화, 네트워크 구성은 모두 같은 `MapDefinition`을 소비하며 별도 광산/성 배열을 병행 유지하지 않는다.

**초기 소유권 파생:**

- `MapDefinition` 타일에는 `InitialOwner`를 직렬화하지 않는다.
- Blue/Red 성 위치와 팀 시작 광산 위치가 초기 소유권의 입력 단일 원본이다.
- 공용 초기 영역 확장 규칙은 각 초기 건물 타일과 인접 6타일에 해당 팀 소유권을 부여한다.
- 런타임 초기 건물/소유권 구성과 validator는 같은 순수 `InitialMapStateEvaluator`를 호출한다.
- Host와 Client는 전달된 위치와 같은 규칙으로 동일 초기 소유권을 파생한다.
- evaluator 결과에서 점유된 성 타일과 초기 채굴소 타일을 제외하고, 일반 건설 조건을 만족하는 팀별 고유 타일 수가 정확히 10개인지 검증한다. 두 초기 영역의 중복 타일은 한 번만 센다.

**정규 직렬화와 해시:**

- 권위 직렬화 형식은 canonical binary다. JSON은 진단 로그에만 사용하고 권위 전송이나 해시 원본으로 사용하지 않는다.
- 스키마가 정한 고정 필드 순서와 고정폭 정수를 사용하며, 모든 다중 byte 정수의 byte order는 **little-endian**으로 고정한다. 플랫폼 native byte order에 의존하지 않는다.
- enum, 종류, 변형, 회전, 팀 ID는 고정폭 정수로 직렬화한다.
- 타일은 정확히 231개 row-major 순서다.
- 성·광산·장식 레코드는 스키마가 정한 키로 정규 정렬한다. 위치는 row-major tile index를 사용하고, 같은 위치에서 필요한 경우 팀 ID 또는 장식의 `typeId`→`materialVariantId`→`scaleStepId`→`rotationStepId`를 후속 키로 사용한다.
- string, float, 최종 hash 필드 자체는 canonical bytes와 해시 원본에서 제외한다.
- SHA-256은 hash 필드를 제외한 canonical bytes 전체에 계산하며 32-byte digest로 보관한다.
- Host와 Client는 동일 canonical bytes에서 SHA-256을 다시 계산해 비교한다.

**권위 전송 package:**

- `MapVersion`
- canonical bytes 길이와 canonical bytes
- SHA-256 digest

package header의 `MapVersion`은 canonical `MapDefinition` 내부 값과 일치해야 한다. 불일치 또는 미지원 값은 deserialize 전에 map preparation 실패로 처리한다.

**공용 네트워크 전송 프로토콜:**

- persistent network connection의 `NetworkMapTransfer`가 최초 경기와 `NewMap` 재경기의 package 전송을 모두 담당한다.
- scene-bound 객체의 단일 RPC에 전체 payload를 담아 전송하지 않는다.
- `MapPrepareBegin(matchNonce, mapVersion, totalBytes, chunkCount, SHA256)`으로 전송을 시작한다.
- `MapChunk(matchNonce, index, count, data)`의 `data`는 최대 1KB이며 canonical payload 전체는 최대 64KB다.
- Client는 중복 chunk를 무시하고 out-of-order chunk를 index로 재조립한다. 현재 준비 nonce와 다른 stale 메시지는 무시한다.
- 모든 chunk가 모이고 선언된 `totalBytes`·`chunkCount`와 일치한 뒤에만 hash→deserialize→semantic validator를 실행한다.
- `MapReady(matchNonce, success, clientSHA, errorCode)`로 성공 또는 실패 결과를 회신한다. `success=true` ACK 전에는 씬 전환을 시작하지 않는다.

**timeout과 실패 복구:**

- Begin/Chunks는 reliable이며 Host는 `MapReady`를 10초 기다린다.
- timeout/incomplete assembly에만 같은 nonce·같은 package 전체를 한 번 재전송하고 다시 10초 기다린다. 두 번째 실패 시 중단한다.
- unsupported `MapVersion`, >64KB, SHA mismatch, deserialize fail, semantic invalid, disconnect는 즉시 실패하고 동일 package 재전송을 하지 않는다.
- 최초 멀티 실패는 loading dismiss, lobby/network 유지, generic `맵 준비에 실패했습니다` popup과 Retry/Leave Match를 표시한다. 어느 player Retry도 Host 요청으로 수렴하고 duplicate는 idempotent하며 새 root seed부터 다시 준비한다.
- NewMap 실패는 old definition 보존, rematch state reset, result UI와 full auto-return countdown 및 SameMap/NewMap/Lobby action 복원이다.
- single 최초 실패는 Retry/Lobby, single rematch 실패는 기존 선택지 복원이다. internal code/seed/type은 UI에 숨기고 log에만 기록한다.

**Client 처리 순서:**

1. package 길이와 지원 `MapVersion` 확인
2. canonical bytes의 SHA-256 재계산 및 전달 digest 비교
3. canonical bytes deserialize
4. 타일 수·좌표·상태·공정성을 포함한 semantic fairness validator 실행
5. 모든 단계 성공 시에만 `MapReady(success=true)` 준비 ACK 전송

Client가 응답 가능한 상태에서 검증 단계가 실패하면 `MapReady(success=false, errorCode)`를 전송하고 기존 로비를 유지한다. timeout·불완전 수신·disconnect는 위 timeout/failure state machine이 처리한다.

### 생성·검증 실행 스레드 정책

- 최초 구현의 맵 generation/validation은 main thread에서 synchronous하게 실행한다.
- 로비에 loading UI를 먼저 표시하고 1 frame yield해 화면이 실제 렌더된 다음 생성·검증을 시작한다. 완료된 뒤에만 네트워크 전송을 시작한다.
- generator와 validator는 Unity API에 의존하지 않는 pure C#으로 설계한다.
- `MapDefinition`을 Unity object와 render 상태에 적용하는 작업은 Game scene의 main thread에서만 수행한다.
- 생성 소요 시간과 최종 attempt count를 필수 로그로 측정한다.
- 실제 기기 profiling에서 사용자 체감 hitch가 확인된 경우에만 pure C# generation/validation을 background 실행으로 이전하는 후속 작업을 검토한다.
- 최초 구현부터 `Task`, thread, cancellation 수명주기 복잡성을 추가하지 않는다.

### 결정적 PRNG 및 스트림 분리 계약

- seed는 부호 해석에 의존하지 않는 64-bit root seed로 취급한다.
- 프로젝트가 알고리즘과 정수 연산을 고정한 전용 PRNG를 사용한다.
- `System.Random`과 `UnityEngine.Random`은 맵 생성에 사용하지 않는다.
- root seed와 `MapVersion`을 고정 파생 함수의 입력으로 사용한다. 런타임/플랫폼별 문자열 해시나 기본 `GetHashCode()`에 의존하지 않는다.

root seed에서 다음 명명 스트림을 안정적인 정수 stream ID로 분리한다.

- `MapSelection`: `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`(A/B 50:50)를 경기당 최초 1회 선택
- `Terrain`: 정적 지형과 건설 불가 구역
- `MinePlacement`: 시작/중립 광산 배치
- `Decoration`: 장식 종류·변형·회전

최초 구현에서는 모든 archetype generator와 fallback이 빈 `DecorationDefinition` 목록을 생성하므로 `Decoration` 스트림에서 draw를 소비하지 않는다. 다만 `MapDefinition`, canonical codec/hash, `SymmetricMapBuilder`, `MapDefinitionValidator`는 처음부터 장식 스키마와 exact 180° 대응을 지원한다. 에셋과 테마가 확정된 뒤에만 독립 `Decoration` 스트림으로 placement를 추가하며, 그 추가·draw 수·호출 순서는 `Terrain`과 `MinePlacement` 결과를 바꾸지 않아야 한다.

재시도 0~99는 각 서브시스템 seed와 attempt index를 다시 고정 파생해 `Attempt-0`부터 `Attempt-99`까지 독립 스트림으로 만든다.

- 정상 모드의 `InitialGold`는 `NeutralMineCount` 매핑에서 파생하며 PRNG draw를 소비하지 않는다. `GameConfig.MapTestModeEnabled=true`이면 실제 초기 골드는 `TestStartingGold=5000`이다.
- 싱글은 로컬 설정, 멀티는 Host 설정만 권위다. Client 로컬 설정은 무시하며 Host가 전송한 실제 표식과 골드를 양쪽이 적용한다.
- 선택된 `MapType`, `NeutralMineCount`, `StartingMineSide`, 실제 테스트 모드 표식과 `InitialGold`는 attempt 0~99와 fallback까지 불변이다. 둘 다 canonical bytes와 SHA-256 입력에 포함한다.
- 재시도에서 바뀔 수 있는 값은 지형 세부 형태, 중립 광산 위치, 장식 placement 활성화 이후의 장식뿐이다. 최초 구현의 장식은 항상 빈 목록이며 시작 광산 방향을 포함한 선택값은 다시 추첨하지 않는다.
- 유효하지 않은 후보는 해당 attempt만 탈락시킨다. 선택 단계로 돌아가 재추첨하지 않으므로 생성 실패율 차이가 유형·광산 수·A/B의 최초 선택 확률을 편향시키지 않는다.
- 장식 draw 추가·삭제·순서 변경은 지형이나 광산 결과를 바꾸지 않는다.
- 어떤 attempt에서 소비한 draw 수는 다음 attempt의 시작 상태에 영향을 주지 않는다.
- 같은 `MapVersion`과 root seed는 같은 선택, 후보 순서, 검증 결과, 폴백 여부와 최종 결과를 재현한다.
- 재현 가능한 생성은 디버깅 계약이다. 실제 경기의 권위 데이터는 Host가 확정해 전달한 최종 `MapDefinition`과 그 hash이며, Client의 독립 재생성 결과가 아니다.

### `SymmetricMapBuilder` 단일 생성 경계

- 모든 archetype generator는 raw 타일 배열이나 180도 상대 타일을 직접 수정하지 않는다.
- 지형, `BuildRule`, 광산, 장식 배치는 모두 `SymmetricMapBuilder`의 공용 대칭 API를 통해서만 기록한다.
- `SetPair(col, row, state)`는 원본과 180도 대응 좌표 `(10-col, 20-row)`를 한 연산으로 함께 적용한다.
- `SetCenter(state)`는 회전 중심 `(5,10)`의 자기대응 상태에만 사용한다.
- 중심은 `SetPair`로 기록하지 않고, 중심이 아닌 좌표는 `SetCenter`로 기록할 수 없게 한다.
- 대응 state 변환은 팀 전용 상태의 Blue↔Red 교환과 장식 rotation의 180도 대응값을 자동 적용한다.
- builder는 생성 과정의 구조적 exact symmetry를 보장하지만 최종 판정 권위는 아니다.
- 완성 후보는 별도의 독립 `MapDefinitionValidator`가 모든 타일·광산·장식의 exact symmetry를 다시 검증한다.

### archetype 생성 알고리즘

**ObstacleOpen:**

- 3~9행은 행별 장애물 수 0~4를 각각 20%로 독립 선택하고, 해당 수만큼 고유 열을 균등 선택한다. 11~17행은 exact 180° 투영이다.
- 10행은 0/2/4를 각각 1/3로 선택해 내부 회전쌍으로 둔다.
- 전체 장애물 쌍이 0이면 attempt를 거부한다. 도달성 실패 시 수를 줄이거나 이동·repair하지 않고 다음 attempt로 간다.
- 검증 전 raw 기대 장애물 수는 약 30/231이다.

**Canyon:**

- 0~2행과 18~20행은 폭 11 Open이다. `W∈{3,5,7}`를 균등 선택한다.
- 3~8행은 중심 연속 홀수 폭이며 정확히 `(11-W)/2`개의 transition row를 균등 선택한다. 선택 행을 지날 때 폭을 2 줄여 단조 `11→9→7→5→3` 계열과 인접 감소량 최대 2를 지킨다.
- 9~11행은 폭 W의 `Open+NoBuild`, 12~17행은 상단 profile의 exact 180° 투영, profile 밖은 Blocked다.

**Outer:**

- 길이 5/7/9/11과 최대 폭 3/5를 각각 균등 선택한다. 10행·5열 중심이며 각 포함 행은 하나의 중심 연속 홀수 폭 Blocked segment, 중앙행은 최대 폭이다.
- 상단 profile을 180° 투영하며 인접 폭 차이≤2, connected, no holes를 지킨다.
- Diamond/Oval/Irregular을 각각 1/3로 선택한다. Diamond는 1→3→5 단조 ramp, Oval은 tapered ends+max plateau, Irregular은 모든 공통 제약 안의 임의 홀수 폭 profile이다.
- 9~11행의 바깥 Open route는 모두 NoBuild이고 좌·우 route 폭≥3 및 연결성을 검증한다.

**ThreeLane:**

- L=5/7/9/11 균등 선택에 따라 band는 각각 8~12/7~13/6~14/5~15행이다.
- band 각 행은 `0~2 Open+NoBuild | 3 Blocked | 4~6 Open+NoBuild | 7 Blocked | 8~10 Open+NoBuild`다.
- band 밖은 11칸 전부 Open+Allowed로 즉시 합쳐지며 transition obstacle과 세로 보호 통로가 없다.
- band 내부 pair는 회전 대응 좌·우 lane에만, lane별 최대 1 mine이다. middle lane 내부는 홀수 count의 center singleton `(5,10)`만 허용하고 나머지 pair는 merged upper/lower zone에 둔다.

### 중립 광산 sampling과 공정성 metric

**sampling:** 성·시작 광산·팀별 보호 초기 10타일·Blocked·유형별 금지 zone을 제외한 뒤 canonical 180° orbit pair를 만들고 역순 중복을 제거한다. `MinePlacement` 스트림으로 distinct pair slot을 중심/edge 가중치 없이 균등 선택하며 odd count는 고정 `(5,10)` singleton을 추가한다. Open/Obstacle은 모든 legal pair, Canyon은 rows 9~11 밖 widening zone, Outer는 rows 9~11 최대 1 pair와 나머지 upper/lower, ThreeLane은 band 내부 mirrored left/right lane당 최대 1 mine과 나머지 merged zone을 사용한다. spacing/adjacency filter는 없다. local constraint는 동일 확률 rejection sampling, global validation 실패는 선택 mine을 이동·repair하지 않고 attempt reject다.

**access BFS:** castle 인접 statically walkable 전체를 distance 0 source set, target mine 인접 statically walkable 전체를 target set으로 하는 multi-source BFS의 최초 target 도달 거리를 사용한다. Open Allowed/NoBuild만 traversable이고 Blocked·모든 mine·castle·starting post는 제외하며 runtime building은 제외한다. 모든 castle→모든 neutral mine 도달과 Blue/Red castle access region 상호 도달을 요구한다. center는 Blue/Red 거리 동일, pair A/rot(A)는 `B→A=R→rot(A)`와 `R→A=B→rot(A)`를 모두 검사하고 geometric `HexCoord` cross distance도 독립 검사한다. 기존 A*의 임의 인접 목표는 사용하지 않는다.

**corridor validator:** mine 전 row cross-section은 Canyon 중앙≥3, Outer 좌/우 각각≥3, ThreeLane 각 lane 정확히3이며 모두 NoBuild다. mine 후 같은 corridor+row는 mine 0개면 walkable≥3, 1개면≥2이고 2개 이상 mine은 금지한다. BFS로 corridor start-to-end continuity와 unintended disconnect를 검사하되 alternate path 전체를 열거하지 않는다.

### deterministic fallback 구성

fallback은 유형×광산 수 조합별로 미리 직렬화한 25개 완성 바이너리/에셋을 보관하지 않는다.

- 맵 유형별 deterministic base template 1개씩, 총 5개를 코드 또는 순수 정의로 유지한다.
- 각 유형의 허용 중립 광산 수별로 사전 검증된 180도 대칭 mine slot 조합을 유지한다.
- fallback 구성 중에는 PRNG draw를 소비하지 않는다.
- 같은 맵 유형+중립 광산 수는 항상 같은 base template+mine slot 조합을 선택한다.
- fallback은 경기 선택 단계에서 정한 `StartingMineSide`를 그대로 적용한다.
- A/B별 전체 fallback 맵을 복제해 보관하지 않는다. 기준 시작 광산 쌍에 대해 B는 시작 광산 좌표에만 좌우 대응 변환 `(col,row) → (10-col,row)`을 적용한다. base terrain과 중립 광산 조합은 이 변환의 대상이 아니다.
- 최초 구현의 fallback도 `DecorationDefinition` 빈 목록을 사용한다. 장식 에셋·테마 확정 뒤 추가하더라도 exact 180° symmetric 고정 set만 허용한다.
- 조립된 fallback도 예외 없이 일반 `MapDefinitionValidator` 전체를 통과해야 한다.
- canonical binary asset을 저장하지 않는다. 현재 schema의 `MapDefinition`을 코드/순수 정의에서 조립하므로 schema 변경 때 과거 fallback 바이너리 마이그레이션이 필요하지 않아야 한다.

### `HexTile` 상태 분리와 `IsWalkable` 전환

런타임 `HexTile`은 서로 다른 원인을 하나의 mutable bool에 섞지 않고 다음 네 상태로 분리한다.

- `TerrainKind`: `Open` / `Blocked` — 맵 생성기가 정한 영구 지형
- `BuildRule`: `Allowed` / `NoBuild` — 열린 타일의 일반 건설 허용 여부
- `MineKind`: `None` / `Neutral` / `BlueStart` / `RedStart` — `MapDefinition` 광산 배치 목록에서 로드 시 투영
- `HasBuilding`: 건물 배치·철거·파괴에 따라 바뀌는 동적 점유 상태

`IsWalkable`은 직접 저장하거나 set하지 않는 계산 결과로 전환한다.

```text
IsWalkable = TerrainKind == Open
          && MineKind == None
          && !HasBuilding
```

행동별 조건은 다음과 같다.

- 일반 건설: `Open && Allowed && MineKind == None && !HasBuilding` + 기존 소유권 조건
- MiningPost: `MineKind != None && !HasBuilding` + 기존 인접 팀 타일 조건
- 점령 가능: `TerrainKind == Open`
- `TerrainKind.Blocked`: 이동·건설·점령 모두 불가. 건물 철거/파괴로 열리지 않음

기존 `HexTile.IsWalkable` mutable 필드와 각 호출부의 직접 대입을 제거 또는 계산 프로퍼티 기반으로 전환해야 한다. 건물 배치 시 `HasBuilding = true`, 철거/파괴 시 `HasBuilding = false`만 변경하며 `TerrainKind`, `BuildRule`, `MineKind`는 건물 수명주기에 따라 덮어쓰지 않는다.

`MapDefinition`의 광산 배치 목록이 광산 위치·종류·팀의 직렬화 단일 원본이고, `HexTile.MineKind`는 전투 맵 구성 시 생성되는 런타임 투영이다. 두 값을 독립적으로 수정하는 병행 원본으로 운영하지 않는다.

### `NoBuild`·`Blocked` 시각 및 입력 표현

- `BuildRule.NoBuild`는 일반 `Open` 타일과 동일한 standard hex mesh와 높이를 사용한다.
- 타일의 Neutral/Blue/Red owner base color를 유지하고 표면에 반투명 짙은 회색 diagonal hatch 3개를 overlay한다.
- NoBuild overlay와 selection highlight는 동시에 유지한다. 선택 상태가 해치를 제거하거나 해치가 선택 표시를 가리지 않도록 표현 계층을 분리한다.
- `TerrainKind.Blocked` 좌표는 domain `HexGrid`와 231개 `MapDefinition` 레코드에 유지하고 canonical codec/hash 및 exact 180° validator의 대상에 포함한다.
- `HexGridRenderer`는 blocked 좌표에 standard hex tile mesh와 collider를 생성하지 않는다. 별도 높낮이를 두지 않고 빈 공간으로 표시한다.
- 입력 선택 경계는 raycast가 배경·하부 collider에 닿더라도 최종 좌표의 `TerrainKind.Blocked`를 확인해 선택을 거부한다. blocked 좌표에서는 이동·선택·점령·건설 동작을 시작하지 않는다.
- 추후 obstacle asset은 blocked 빈 위치의 시각 표현만 대체할 수 있다. domain 상태, hash, exact symmetry와 게임플레이 판정은 변경하지 않는다.
- `GridInteractionUseCase` 판정 순서는 기존 building action→광산 `MineKind`/MiningPost 자격→Blocked→NoBuild→일반 타일이다.
- blocked/빈 공간 클릭은 이전 선택 해제와 stale 건설 패널 닫기만 수행한다. 토스트와 새 타일 선택 이벤트는 없다.
- 자기 팀 소유 `Open+NoBuild`는 정상 선택·highlight, stale 건설 패널 닫기, `ToastKey.BuildingNotAllowed`(`이 타일에는 건설할 수 없습니다`) 표시를 수행한다.
- 중립·적 소유 `Open+NoBuild`는 선택·highlight와 stale 패널 닫기만 수행하고 토스트는 표시하지 않는다.

### 재경기 맵 mode 및 정의 교체

- 기존 요청·수락·거절과 Game 씬 재로드 구조를 유지한다.
- 요청에 `RematchMapMode.SameMap` / `RematchMapMode.NewMap`을 포함한다.
- 상대 팝업에 요청 조건을 표시하고 그 조건에 대한 수락/거절을 받는다.
- 서로 다른 mode가 동시에 요청되면 자동 시작하지 않는다. 서버 선접수 요청을 pending 제안으로 유지하고 상대가 그 조건을 확인·수락하게 한다.
- `SameMap`: 현재 canonical `MapDefinition` package/hash를 그대로 재사용한다.
- `NewMap`: Game 종료 상태에서 Host가 새 정의를 생성·전송하고 Client SHA-256/semantic 검증 ACK를 받은 뒤에만 교체한다.
- `NewMap`은 팀·종족·그 밖의 매치 설정을 유지하고, 새 64-bit root seed부터 맵 관련 값만 다시 추첨·생성한다. 새 `MapType`, 허용 `NeutralMineCount`, `StartingMineSide`, 지형 세부 형태, 중립 광산 위치가 대상이다. Host는 새 맵 준비 시점의 `GameConfig.MapTestModeEnabled`로 실제 초기 골드를 확정한다. 정상 모드는 새 광산 수 표, 테스트 모드는 `5000`을 쓴다. 장식 placement 활성화 이후에는 장식도 대상이지만 최초 구현은 빈 목록이다.
- 새 후보의 canonical hash가 현재 맵 hash와 같으면 새 맵으로 인정하지 않고 폐기한다. 다른 새 root seed부터 준비를 반복한다.
- 새 정의 검증 전에는 기존 definition을 current로 유지하고 새 정의는 pending으로 분리한다.
- 성공 시 pending→current를 원자적으로 교체한 뒤 씬을 재로드한다.
- 실패 시 pending을 폐기하고 기존 결과 화면과 기존 definition을 유지한다.
- 싱글플레이도 같은 맵/새 맵 선택을 제공하며 새 맵은 로컬 semantic 검증 성공 후 교체한다.
- 재경기 컨텍스트는 Game 씬 재로드 동안 유지하고 로비 복귀·세션/연결 종료 시 폐기한다.

---

## 검증 계획

사용자가 구현/QA를 요청하는 후속 작업에서 다음을 검증한다.

- 동일 seed의 완전 재현
- `MapDefinition` 타일 수 231개와 row-major 인덱스↔offset 좌표 왕복
- 정규 직렬화의 결정성, hash 필드 제외, float 비포함, Host/Client 해시 일치
- canonical binary 고정 필드 순서·고정폭 정수·고정 byte order 테스트 벡터
- 테스트 모드 OFF에서 광산 1~6개가 각각 실제 초기 골드 700/600/500/400/300/200을 만드는지 확인
- 테스트 모드 ON에서 싱글과 멀티 모두 실제 초기 골드가 `5000`인지 확인
- 멀티에서 Client 로컬 설정이 Host 결정을 덮어쓰지 못하고 양쪽 실제 골드가 같은지 확인
- 테스트 모드 표식(`0`/`1`)과 실제 초기 골드가 canonical round-trip 및 SHA-256에 포함되는지 확인
- `MapTestModeEnabled`가 초기 골드 외 지형·광산·비용·수입 규칙에 영향을 주지 않는지 확인
- 성·광산·장식 목록 입력 순서를 바꿔도 정규 정렬 뒤 canonical bytes/SHA-256 동일
- package 길이/버전→SHA-256→deserialize→semantic validator→ACK 순서와 단계별 거부
- 최초 경기/NewMap 공용 `NetworkMapTransfer`, 1KB chunk·64KB total 한도, 단일 payload RPC 부재
- chunk 중복·순서 역전·stale nonce에서 안전하게 재조립/무시하고 완성 전 검증하지 않는지 확인
- `MapReady` 성공 ACK 전 씬 전환 금지와 실패 `errorCode` 회신
- reliable 전송, 10초×2 timeout과 incomplete 전용 동일 nonce 전체 재전송 1회
- version/size/SHA/deserialize/semantic/disconnect 즉시 실패와 무재전송
- 최초/NewMap/싱글 실패별 popup·선택지·countdown·기존 definition 복구 및 Retry idempotency/new seed
- loading UI 표시 후 1 frame yield→main-thread 동기 생성·검증→전송 순서
- generator/validator의 Unity API 비의존성과 Game scene Unity object 적용의 main-thread 한정
- 생성 시간·attempt count 로그 및 profiling 근거 없는 background Task/thread/cancellation 부재
- JSON이 권위 전송 또는 해시 경로에 사용되지 않는지 확인
- 서로 다른 mode 동시 요청이 자동 시작되지 않고 서버 선접수 조건으로 수렴
- SameMap이 canonical package/hash를 변경하지 않고 재로드
- NewMap 실패 단계별 current definition/결과 화면 유지, 성공 시 원자 교체 후 재로드
- NewMap에서 매치 설정 불변, 맵 관련 값만 새 seed로 재선택, 이전 hash와 같은 후보의 새 seed 재준비
- 싱글 SameMap/NewMap과 로비 복귀·연결 종료 시 컨텍스트 폐기
- 고정 PRNG 테스트 벡터와 64-bit root seed 재현성
- 장식 draw 변경 시 Terrain/MinePlacement 불변
- 최초 archetype/fallback의 빈 `DecorationDefinition` 목록과 decoration PRNG draw 0회
- decoration schema 네 정수 ID의 canonical round-trip/hash 포함 및 exact 180° builder/validator 대응
- 한 attempt의 draw 수 변경 시 다른 attempt 후보 불변
- 동일 `MapVersion`+root seed의 후보 순서·최종 결과 동일
- `SetPair(c,r)`가 `(10-c,20-r)`에 상태·팀·장식 rotation 대응값을 함께 기록
- `SetCenter`의 `(5,10)` 전용 제약과 `SetPair` 중심 사용 거부
- archetype generator의 raw 배열/상대 타일 직접 수정 경로 부재
- builder 결과를 독립 validator가 exact symmetry 재검증
- 유형별 base template 5개와 허용 광산 수별 mine slot 조합의 결정성
- fallback 구성 중 PRNG draw 0회 및 동일 type+mine count의 동일 지형/중립 광산 결과
- fallback도 일반 `MapDefinitionValidator` 전체 통과
- schema 변경 시 저장된 fallback binary asset 마이그레이션이 필요 없는지 확인
- 성·광산 배치 목록 단일 소스 및 타일 정체성 중복 상태 부재
- canonical bytes에 `InitialOwner`가 없고 위치+`InitialMapStateEvaluator`로 Host/Client 초기 소유권이 일치
- 점유된 성/초기 채굴소 타일 제외 후 팀별 즉시 일반 건설 가능 고유 타일 정확히 10개
- `TerrainKind`/`BuildRule`/`MineKind`/`HasBuilding` 조합별 이동·건설·점령 판정
- NoBuild의 동일 mesh/height/owner color와 짙은 회색 diagonal hatch 3개, selection 동시 표시
- Blocked 좌표의 MapDefinition/hash 유지, standard mesh/collider 미생성 및 배경 collider 경유 선택 차단
- Blocked 좌표의 exact symmetry와 추후 obstacle 시각 교체 시 gameplay/hash 불변
- building/MineKind 우선순위, Blocked 무선택·deselect, 소유권별 NoBuild 선택/패널/토스트 분기
- 건물 철거/파괴가 `HasBuilding`만 해제하고 `Blocked` 지형이나 광산을 이동 가능하게 만들지 않는지 확인
- mutable `IsWalkable` 직접 대입 호출부 제거와 계산 결과 일관성
- 유형·광산 수 확률 분포
- `StartingMineSide` A/B 50:50 분포와 attempt 실패 시 세 선택값 불변
- 재시도에서 지형 세부·중립 광산 위치·장식 외 값이 바뀌지 않고 선택확률 편향이 생기지 않는지 확인
- exact 180° 대칭(장식 속성 포함)
- canonical orbit pair 균등 sampling, 역순 중복 제거, 유형별 mine zone/lane 제약, spacing filter 부재와 repair 없는 global reject
- multi-source castle-neighbor→mine-neighbor BFS access 거리, center/pair 교차 등식과 독립 HexCoord 거리
- mine 전후 corridor row 폭·mine 상한과 corridor start-to-end BFS continuity
- 팀별 즉시 건설 가능한 고유 타일 10개
- 성/광산 도달성과 중앙/대응쌍 접근거리
- 다섯 맵 유형별 통로·광산·건설 불가 규칙
- 광산 수별 초기 골드
- 100회 실패 뒤 같은 유형·광산 수·시작 광산 방향 폴백, A/B 좌우 대응 변환 및 필수 로그

이번 문서화 단계에서는 `Testcase.md`를 만들거나 QA를 수행하지 않는다.

---

## 논의 중 추가 확정

- 현재 버전에서는 시작 광산과 중립 광산의 기본 채굴량을 모두 `10골드/초`로 동일하게 유지한다.
- 맵 유형이나 중립 광산 수에 따른 채굴량 배율은 두지 않는다.
- 광산별 차등 채굴량은 추후 확장 시 별도 기획한다.
- 멀티플레이에서는 Host가 최종 맵 전체를 생성·검증·확정하고 Client에 전달한다.
- Client는 맵을 별도로 재추첨하지 않으며, 양측 최종 맵 해시가 다르면 게임 시작을 중단한다.
- 우회 경로 전체를 열거하지 않는다. 회전 대응 타일 상태의 완전 일치로 이동 그래프 대칭을 보장하고, 실제 경로 탐색은 핵심 도달성·최단거리·통로 폭 검증에 사용한다.
- 맵 준비와 데이터 해시 검증은 로비 씬의 로딩 상태에서 끝내고, 성공한 경우에만 전투 씬으로 전환한다.
- 준비 실패나 해시 불일치 시 씬 전환 없이 기존 로비를 유지한다. 전투 씬에서는 확정된 맵을 구성하고 양측 로드 완료 후 시뮬레이션을 시작한다.
- 로딩 화면에는 맵 유형·광산 수·초기 골드·seed·전체 맵 미리보기를 표시하지 않고, 전투 씬에서 실제 전장을 공개한다.


