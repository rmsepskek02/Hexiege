# RhinoBreaker 실제 Attack 타격 마커 교정 — 테스트 케이스

RhinoBreaker의 실제 공격 모션과 타격 표현이 같은 순간에 한 번만 발생하는지 확인한다. 이번 실기는 RhinoBreaker와 이미 정상인 근거리 대조군 1종만 다루며, 전체 25종과 Legacy rollback은 별도 통합 회귀로 남긴다.

## 테스트 환경

- 구성: Unity Editor Host + Android Client
- 교정 대상: Blue/Red RhinoBreaker
- 정상 대조군: LittleKnight 1종
- 현재 결과: **CONDITIONAL PASS — 다음 통합 빌드 최종 확인 대기**

### MULTI-1: 양 팀 RhinoBreaker 공격 시작

**전제:** Host와 Client가 같은 경기에 접속했고 Blue/Red RhinoBreaker가 적과 교전할 수 있다.

**동작:**
1. 양 팀 RhinoBreaker를 각각 적에게 접근시킨다.
2. 첫 공격과 이후 반복 공격을 관찰한다.

**기댓값:**
- 양 팀 모두 공격 직전에 멈춘 채 진행 불능이 되지 않는다.
- 실제 공격 모션과 피해 적용이 반복해서 발생한다.

**결과:** PASS — 이전 두 역할 세션에서 Blue/Red RhinoBreaker 모두 유닛 공격을 시작했고, 추가 Editor Host + Android Client 경기에서도 Blue RhinoBreaker가 정지·정렬 후 건물 공격을 Accepted commit으로 시작했다. 공격 진입 누락·영구 정지·stationary Walk 위반은 0건이었다.

### MULTI-2: 타격 표현 시점과 중복

**전제:** RhinoBreaker가 여러 번 공격할 수 있을 만큼 체력이 있는 대상을 공격한다.

**동작:**
1. 공격 모션의 접촉 구간과 피해 텍스트·피격 반응 시점을 비교한다.
2. 여러 공격 회차에서 같은 항목을 반복 확인한다.

**기댓값:**
- 타격 표현이 실제 접촉 동작과 맞는다.
- 회차당 피해 텍스트와 피격 표현이 한 번만 발생한다.
- 늦게 남은 표현이 다음 회차에 붙지 않는다.

**결과:** CONDITIONAL PASS — Host/Client 모두 필수 표현 692/692, 중복·전송·표현 실패 0으로 회차당 단일 표현은 수렴했다. 실제 접촉 동작과 피해 표현의 육안 시점은 사용자가 명시하지 않아 다음 통합 빌드에서 최종 확인한다.

### MULTI-3: 대상 사망·변경 후 복구

**전제:** RhinoBreaker가 현재 대상을 공격 중이며 주변에 다음 공격 대상이 있다.

**동작:**
1. 현재 대상이 공격 도중 사망하게 한다.
2. RhinoBreaker가 다음 대상을 획득하도록 경기를 계속한다.

**기댓값:**
- 취소된 회차의 추가 피해나 표현이 발생하지 않는다.
- 다음 대상으로 정상 전환해 새 공격을 시작한다.
- 공격 전 정지 상태에 영구적으로 남지 않는다.

**결과:** CONDITIONAL PASS — 유닛 99기와 건물 6기 사망 동안 취소 회차 추가 피해, 결과 거부, 타겟 불일치, 공격 진입 오류는 0건이었다. 다만 개별 RhinoBreaker가 대상 사망 직후 다음 대상으로 전환하는 장면의 사용자 육안 판정은 명시되지 않았다.

### MULTI-4: 유닛과 건물 대상

**전제:** RhinoBreaker가 적 유닛과 적 건물에 각각 접근할 수 있다.

**동작:**
1. 적 유닛을 공격한다.
2. 적 건물을 공격한다.

**기댓값:**
- 두 대상 종류 모두 실제 공격과 피해가 발생한다.
- 피해 텍스트가 피격 대상의 올바른 위치에 표시된다.

**결과:** PASS — 이전 세션에서 유닛 공격이 정상 수렴했고, 추가 세션에서 RhinoBreaker `unitId=0`이 Red TrainingCamp `BuildingId=11`에 Accepted commit을 만든 뒤 해당 건물이 사망했다. 앞선 사용자 확인에서 피해 텍스트 위치도 정상으로 보고됐다.

### MULTI-5: 정상 대조군 회귀

**전제:** 같은 경기에서 LittleKnight가 정상적으로 교전할 수 있다.

**동작:**
1. LittleKnight의 첫 공격과 반복 공격을 짧게 확인한다.
2. 공격 모션과 피해 텍스트 위치를 확인한다.

**기댓값:**
- 기존 정상 근거리 공격이 유지된다.
- 공격 전 멈춤, 타격 누락, 피해 텍스트 위치 회귀가 없다.

**결과:** PASS — 추가 경기에서 Red LittleKnight 77기가 대조군으로 교전했고 결과·표현·이동 terminal에 회귀 오류가 없었다.

## 판정

- 자동검증 PASS는 빌드 허용 조건이며 실기 PASS를 대신하지 않는다.
- RhinoBreaker 또는 대조군에서 공격 누락·정지·표현 중복·위치 오류가 하나라도 보이면 FAIL이다.
- 현재 로그 판정은 **CONDITIONAL PASS**다. 실제 접촉 동작과 표현의 육안 시점 및 개별 대상 사망 직후 전환을 다음 통합 빌드에서 확인하기 전까지 Task는 **OPEN**이다.

## 로그 근거와 범위

- 최신 추가 세션: `sharedSessionKey=f9b842d958704ce62d2a3e2e555d9453259462967aab4fcae1cd36308c35a8d0`, Editor Host + Android Client
- 결과 수렴: Host 724/실패 0 ↔ Client 724 수락/거부 0
- 표현 수렴: 양쪽 ready 724, expectedVisual 692, presentationEmits 692, duplicate/transport/failure 0
- 이동·공간: gate·writer·handoff·stationary Walk·spatial mismatch·error/drop 0
- 별도 관찰: Client UnitView init delayed/completion 107/107, retry failure/exhaustion 0; Root pose `maxUnits=64` 경고 양쪽 1건
- 이번에는 새 빌드를 하지 않았다. `U+27F3` 경고 2건은 특수문자 제거가 반영되지 않은 기존 APK 결과이며 현재 소스 실패로 판정하지 않는다.
