# Hexiege Domain Language

Hexiege의 게임 기획과 규칙 문서에서 같은 개념을 같은 이름으로 사용하기 위한 용어집이다.

## Language

**대전 맵**:
Blue와 Red가 같은 초기 조건에서 경쟁하는 하나의 전장.
_Avoid_: 스테이지, 레벨

**맵 유형**:
이동 공간, 완전 차단 지형, 건설 불가 구역의 배치 원칙으로 구분되는 무작위 대전 맵의 형태.
_Avoid_: 맵 테마, 바이옴

**완전개방형**:
완전 차단 지형과 건설 불가 구역이 없는 맵 유형.
_Avoid_: 오픈형, 기본형

**장애물 개방형**:
열린 전장에 무작위 완전 차단 지형 군집이 놓이는 맵 유형.
_Avoid_: 무작위 장애물형, 개방형

**협곡형**:
바깥이 막히고 중앙으로 갈수록 통행 폭이 좁아지는 모래시계 형태의 맵 유형.
_Avoid_: 병목형

**외곽형**:
중앙의 연결된 차단 지형을 좌우 외곽 통로로 우회하는 맵 유형.
_Avoid_: 중앙 장애물형, 우회형

**3갈래형**:
중앙 분리 대역에서 전장이 세 통로로 갈라졌다가 바깥에서 다시 합쳐지는 맵 유형.
_Avoid_: 3레인형, 삼거리형

**시작 광산**:
각 팀의 성 가까이에 하나씩 배치되고 게임 시작 시 해당 팀의 초기 채굴소가 놓이는 골드광산.
_Avoid_: 개인 광산, 기본 광산

**중립 광산**:
어느 팀에도 선점되지 않은 채 양 팀이 경쟁하도록 배치되는 골드광산.
_Avoid_: 중앙 광산(중앙에 있지 않을 수도 있음)

**중앙 단독 광산**:
180도 회전 중심에 놓여 회전 후 자기 자신과 일치하는 하나의 중립 광산.
_Avoid_: 홀수 광산

**대칭 광산 쌍**:
맵 중심 기준 180도 회전으로 서로 대응하는 두 중립 광산.
_Avoid_: 좌우 광산, 균형 광산

**즉시 건설 가능 타일**:
게임 시작 직후 추가 점령 없이 일반 건물을 지을 수 있는 한 팀 소유의 고유 타일.
_Avoid_: 시작 타일, 안전 타일

**건설 불가 구역**:
유닛 이동과 점령은 가능하지만 일반 건물은 지을 수 없는 타일 구역.
_Avoid_: 장애물, 차단 타일

**완전 차단 지형**:
이동·건설·점령·파괴가 모두 불가능한 맵의 고정 지형.
_Avoid_: 건설 불가 구역, 벽 건물

**맵 생성 요소**:
무작위 맵 생성이 배치하거나 변형하는 타일, 지형, 광산, 건설 제한, 장식의 총칭. 플레이어 소유의 성, 채굴소, 건물, 유닛은 포함하지 않는다.
_Avoid_: 게임플레이 요소(장식도 포함됨)

**맵 상성**:
특정 종족이나 유닛 구성이 어떤 맵 유형에서 상대적으로 유리하거나 불리한 정도.
_Avoid_: 맵 공정성

**Simulation Root**:
서버 권위 위치와 방향을 보유하고 NetworkTransform·사거리·타겟·공격 판정이 참조하는 유닛 루트.
_Avoid_: Network Root, 실제 루트

**Visual Root**:
Simulation Root의 자식으로서 팀별 화면 변환, 모델 방향 오프셋, Animator와 VFX를 담당하는 클라이언트 표현 루트.
_Avoid_: Mesh Root(메시만 포함한다는 오해)

**행동 회차(Action Sequence)**:
서버가 확정한 하나의 이동·회전·공격 행동과 그 진행 단계를 식별하는 기록.
_Avoid_: 애니메이션 상태, RPC 한 번

**공격 회차 ID(AttackSequenceId)**:
공격자별로 단조 증가하며 하나의 Windup, Impact, Recovery를 끝까지 묶는 서버 권위 식별자.
_Avoid_: 공격자 ID, 애니메이션 재생 횟수

**타격 번호(HitIndex)**:
한 공격 회차 안의 여러 Impact를 0부터 순서대로 식별하는 번호.
_Avoid_: 피해자 큐 순번

**Impact**:
서버가 공격의 적중·빗나감·피해·회복·상태 효과 결과를 확정하는 권위 순간.
_Avoid_: OnAttackHit, 단순 애니메이션 타격 프레임

**전달 방식(Delivery)**:
효과가 목표 위치에 도달하는 방식. MeleeContact, Hitscan, ProjectileImpact, TravelingArea로 구분한다.
_Avoid_: 원거리, 착탄형 AoE

**대상 범위(TargetScope)**:
공격이 단일 대상인지 범위 대상인지 나타내는 축. Single 또는 Area.
_Avoid_: 범위 모양

**범위 모양(AreaShape)**:
TargetScope가 Area일 때 사용하는 Cone, Circle, Rectangle 등의 월드 좌표 판정 형태.
_Avoid_: Single, 전달 방식

**효과 종류(Effect)**:
공격이 적용하는 Damage, Heal, Status의 의미.
_Avoid_: 전달 방식

**적용 일정(ApplicationSchedule)**:
효과가 Instant, MultiImpact, Periodic, ImpactThenPeriodic 또는 ContactOncePerTarget 중 어떤 시간 패턴으로 적용되는지 나타내는 축.
_Avoid_: 쿨다운

**Windup**:
공격 회차가 커밋된 뒤 첫 Impact 전까지의 준비 구간.
_Avoid_: 공격 정렬

**Recovery**:
마지막 Impact 후 다음 행동이 가능해질 때까지의 구간.
_Avoid_: 전체 공격 쿨다운

**SimulationFacing**:
서버 Simulation Root가 보유하고 이동·사거리·공격 판정에 사용하는 권위 방향.
_Avoid_: 화면에서 보이는 방향

**VisualFacing**:
각 클라이언트가 SimulationFacing을 팀 관점으로 변환해 Visual Root에 표시하는 방향.
_Avoid_: 권위 방향

**AimDirection**:
MeleeContact·Hitscan의 Impact 또는 ProjectileImpact·TravelingArea의 Launch·Activation에 서버가 기록하는 판정 방향.
_Avoid_: 클라이언트가 타겟 위치로 다시 계산한 방향

**ImpactPoint**:
서버가 ProjectileImpact 또는 범위 결과의 중심으로 확정한 월드 좌표.
_Avoid_: 로컬 VFX 도착점

**AttackTimeline**:
Windup, ActionMarkerOffset과 Recovery를 정의하는 검증된 공격 시간 데이터.
_Avoid_: Animation Event 목록

**AttackProfile**:
Delivery, TargetScope, AreaShape, Effect, ApplicationSchedule, RangeMetric과 AttackTimeline을 묶은 서버 판정 설정.
_Avoid_: Animator Controller

**AcquireRange**:
타겟이 없는 유닛이 새 타겟 후보를 획득할 수 있는 거리.
_Avoid_: 공격 사거리

**LoseRange**:
이미 획득한 타겟을 유지할 수 있는 최대 거리. AcquireRange보다 커 타겟 떨림을 막는다.
_Avoid_: 사거리 Epsilon

**Interval**:
하나의 공격 회차 커밋부터 다음 공격 회차를 커밋할 수 있을 때까지의 주기 의미.
_Avoid_: Recovery만을 뜻하는 표현
