# QuakeSpirit 공격 타격 시점·표현 프로필 교정 테스트

이 문서는 새 빌드에서 QuakeSpirit의 공격이 실제 애니메이션 타격 순간에 표시되고, 기존 직접 피해와 범위 피해가 함께 정상 동작하는지 확인하기 위한 체크리스트입니다. 자동 검증과 실기 검증을 분리하며, 사용자가 실제 기기 결과를 알려주기 전에는 PASS로 기록하지 않습니다.

**현재 판정:** **CONDITIONAL PASS / OPEN** · 자동·focused Android Host/Client 로그 게이트 PASS · 육안 확인 항목 대기

## 사용자 테스트 케이스

### SINGLE-1: 공격 타격 순간 일치

**전제:** 새 구현이 반영된 빌드에서 QuakeSpirit과 적 유닛이 공격 가능한 위치에 있다.

**동작:**
1. QuakeSpirit이 적에게 접근해 첫 공격을 시작하는 장면을 관찰한다.
2. 땅을 내려치는 동작, 폭발 표현, 대미지 텍스트가 나타나는 순간을 비교한다.
3. 같은 공격을 여러 차례 반복해서 관찰한다.

**기댓값:**
- 대미지 텍스트와 폭발 표현이 땅을 내려치는 타격 순간에 자연스럽게 나타난다.
- 공격 전에 비정상적으로 오래 멈추거나 걷기 동작에 고착되지 않는다.
- 첫 공격뿐 아니라 이후 반복 공격에서도 같은 시점 관계가 유지된다.

**결과:** CONDITIONAL PASS — 로그에서 공격 준비의 정상 대기→준비→공격 승인과 정렬 전 정상 보류, 전체 비정상 gate 0 및 정지 중 걷기 위반 0을 확인했다. 정확한 내려치기 화면 프레임과 체감 장시간 정지 없음은 사용자 육안 확인이 남았다.

### SINGLE-2: 직접 피해와 주변 적 유닛 스플래시

**전제:** 주대상 주변에 다른 적 유닛이 있고, 비교를 위해 범위 밖 적 유닛도 있다.

**동작:**
1. QuakeSpirit이 가운데 적 유닛을 공격하게 한다.
2. 주대상과 주변 적 유닛의 피해를 확인한다.
3. 범위 밖 적 유닛의 체력을 확인한다.

**기댓값:**
- 주대상은 직접 피해를 한 번 받는다.
- 반경 안의 다른 적 유닛은 같은 타격 순간에 스플래시 피해를 받는다.
- 주대상에 스플래시가 중복 적용되지 않는다.
- 범위 밖 적 유닛은 피해를 받지 않는다.

**결과:** CONDITIONAL PASS — 같은 타격에서 주대상 유닛 피해 200과 주변 적 유닛 피해 100이 함께 기록됐다. 범위 밖 대상 무피해는 이번 로그만으로 명시적으로 분리하지 못했다.

### SINGLE-3: 적 건물 스플래시와 아군 제외

**전제:** 주대상 주변에 적 건물과 아군 유닛 또는 아군 건물이 배치되어 있다.

**동작:**
1. QuakeSpirit이 주대상을 공격하게 한다.
2. 주변 적 건물과 아군 대상의 체력 변화를 확인한다.

**기댓값:**
- 반경 안의 적 건물은 스플래시 피해를 받는다.
- 아군 유닛과 아군 건물은 피해를 받지 않는다.
- 모든 피해 표현은 같은 권위 타격의 결과로 한 번씩 표시된다.

**결과:** CONDITIONAL PASS — 적 건물 스플래시 피해 100과 같은 결과 묶음의 완결은 확인했다. 아군 유닛·건물 무피해는 이번 로그만으로 명시적으로 분리하지 못했다.

### SINGLE-4: 적 건물을 주대상으로 공격

**전제:** QuakeSpirit이 적 건물을 직접 공격할 수 있고 주변에 다른 적 대상이 있다.

**동작:**
1. 적 건물을 주대상으로 지정되게 전투를 구성한다.
2. 주대상 건물의 직접 피해와 주변 적 대상의 스플래시 피해를 확인한다.
3. 건물이 파괴된 뒤 QuakeSpirit의 다음 행동을 관찰한다.

**기댓값:**
- 주대상 건물은 직접 피해를 받는다.
- 반경 안의 다른 적 대상은 스플래시 피해를 받는다.
- 주대상 건물 파괴 뒤 공격 동작이나 걷기 동작에 고착되지 않고 다음 유효 행동으로 전환한다.

**결과:** CONDITIONAL PASS — 주대상 건물 피해 200과 주변 건물 피해 100, 이후 주대상 건물 사망은 확인했다. 특정 QuakeSpirit의 사망 직후 다음 행동 전환은 화면에서 별도로 확인되지 않았다.

### MULTI-1: Host와 Client의 타격 표현 일치

**전제:** Editor와 Android 기기가 같은 멀티플레이 경기에 참가하고 QuakeSpirit이 배치되어 있다.

**동작:**
1. QuakeSpirit이 적 유닛과 적 건물을 여러 차례 공격하게 한다.
2. 양쪽 화면에서 타격 동작, 폭발, 대미지 텍스트와 체력 변화를 비교한다.
3. 타겟 사망과 타겟 교체가 포함되도록 전투를 계속한다.

**기댓값:**
- 양쪽에서 같은 공격의 타격 순간에 표현이 나타난다.
- 피해량과 체력 결과가 양쪽에서 수렴한다.
- 같은 공격이 중복 표시되거나 다음 공격으로 밀려 표시되지 않는다.
- 타겟 사망·교체 뒤에도 공격이 정상적으로 이어진다.

**결과:** CONDITIONAL PASS — Host 결과 770/실패 0과 Client 수락 770/거부 0, 양쪽 필수 표현 909/909, 중복·전송·표현 실패 0으로 수렴했다. 양쪽 화면의 정확한 타격 프레임 비교는 사용자 육안 확인이 남았다.

### MULTI-2: 기존 지원 유닛 회귀 확인

**전제:** QuakeSpirit과 함께 이전에 정상 공격을 확인한 근거리 또는 지원 완료 유닛이 같은 경기에 있다.

**동작:**
1. 비교 유닛과 QuakeSpirit이 각각 여러 차례 공격하게 한다.
2. 접근, 공격 시작, 반복 공격, 대미지 텍스트와 효과를 관찰한다.

**기댓값:**
- 비교 유닛의 기존 공격 동작과 표현이 이전과 동일하게 유지된다.
- QuakeSpirit 교정 때문에 다른 유닛의 공격이 멈추거나 표현이 사라지지 않는다.

**결과:** CONDITIONAL PASS — 이번 4/25종 비교 범위에서 공격·이동·표현 오류 지표는 0이었다. 비교 유닛의 화면상 공격 동작과 효과가 이전과 동일한지는 사용자 육안 확인이 남았다.

### SINGLE-5: 새 빌드의 로비 새로고침 문구

**전제:** 이번 작업 뒤 생성한 새 Android 빌드로 로비에 진입한다.

**동작:**
1. 새로고침 기능이 있는 화면을 연다.
2. 버튼 문구와 폰트 경고 발생 여부를 확인한다.

**기댓값:**
- 버튼은 일반 한글 `새로고침`으로 표시된다.
- 폐기한 특수문자에 대한 폰트 경고가 발생하지 않는다.

**결과:** CONDITIONAL PASS — 최신 Android 실행에서 폐기 특수문자 폰트 경고는 0건이었다. 일반 한글 `새로고침`이 실제 화면에 표시되는지는 사용자가 별도로 확인하지 않았다.

## QA 전용 섹션

### QA-STATIC-1: production asset 연결과 timeline

**예정 확인:**
- Blue/Red QuakeSpirit 프리팹의 Controller GUID 동일성
- `Base Layer/Attack`의 clip GUID 일치
- clip 5초/30fps, `OnAttackHit` 1개/1.667초
- `UnitStatsConfig` QuakeSpirit `hitFrameTimes=[1.667]`

**결과:** PASS — production `UnitStatsConfig=[1.667]`, 실제 Attack clip의 `OnAttackHit=1.667` 1개, Controller와 Blue/Red 프리팹 연결 검증

### QA-STATIC-2: profile partition과 secondary 의미

**예정 확인:**
- QuakeSpirit = `Supported`, `MeleeContact`, Impact 1, secondary true
- Legacy 실행 종류 = `TimerImpactWithSecondaryEffect`
- 전체 partition = Supported 16 / Unresolved 8 / N/A 1
- 다른 24종 profile 분류 무변경

**결과:** PASS — QuakeSpirit `Supported / MeleeContact / Impact 1 / secondary true`, 전체 partition 16/8/1 확인

### QA-STATIC-3: 변경 범위 감사

**예정 확인:**
- Quake 직접 피해, 반경, 스플래시 비율, 적 유닛/건물 수집, 방어력 적용 무변경
- 공통 C2/C3 결과·피해 writer/emitter 무변경
- 과거 placeholder reason과 Quake `Unresolved` fixture 잔존 없음

**결과:** PASS — 공통 C2/C3 및 Quake 피해·AoE 의미 변경 없음, production marker/config 불일치 영구 fail-closed 검증 추가

### QA-UNITY-1: Unit Action self-validation

**메뉴:** `Hexiege → Combat → Run Unit Action Self Validation`

**결과:** PASS — A1/B2/C2/C3 전체 PASS, errors 0

### QA-UNITY-2: Unit Root Pose Cross Audit self-validation

**메뉴:** `Hexiege → Combat → Diagnostics → Self Validate Unit Root Pose Cross Audit`

**결과:** PASS — errors 0

### QA-BUILD-1: Android Build And Run

**진행 조건:** QA-UNITY-1과 QA-UNITY-2가 모두 PASS해야 한다.

**결과:** PASS — 01:20경 빌드 시작 확인 뒤 최신 Android PID 24233에서 실행된 focused 멀티플레이 로그를 확보해 빌드 설치·실행을 확인했다.

### QA-ANDROID-1: QuakeSpirit focused Host/Client 로그 검증

**범위:** Editor Host + Android Client 동일 세션, LittleKnight/Pistoleer/QuakeSpirit/SpearMan 4/25종

**결과:** PASS(로그 축) — QuakeSpirit 10기, Host commits 596·결과 770/실패 0, Client 수락 770/거부 0, 양쪽 필수 표현 909/909, AoE bundle 770/770, 공간 mismatch 0, Host 이동 165,383 frame 오류 0, 공간 계획/커밋 1,118/1,118, 복제 932건 오류 0, ROOT Host/Client PASS를 확인했다. 직접 200과 스플래시 100의 동시 유닛·건물 결과도 확인했다. UnitView 초기화 지연 123건은 완료 123건과 1:1이며 최종 viewUnavailable 0이다. 최신 실행의 ERROR/FATAL/NullReference와 U+27F3 경고는 0건이다.

**제한:** 정확한 내려치기 화면 프레임, 아군·범위 밖 제외, 체감 장시간 정지 없음, 기존 비교 유닛 육안 회귀는 로그 판정 범위 밖이다.

## 최종 판정 기준

- **PASS:** 정적/Unity 코드 게이트와 새 Android 실기 결과가 모든 기댓값에 일치한다.
- **FAIL:** 공격 시점, 직접·스플래시 피해, 반복 공격, 양측 수렴 또는 기존 유닛 회귀 중 하나라도 어긋난다.
- **CONDITIONAL PASS:** 코드 게이트는 통과했지만 사용자 실기 확인이 남아 있다.

이번 Testcase는 QuakeSpirit focused 교정만 판정한다. 전체 25종, 역할교대, Legacy rollback 또는 ActionSequence 전체 완료 판정으로 확대하지 않는다.
