# Fox marker 회귀 fixture 비교 교정 — Plan

값이 바뀌지 않았는데도 Fox 검사에서 실패하는 회전 비교를 고친다. production 동작 대신 검사 함수에서 lease와 기록값을 각각 확인하고, 기본 회전과 정상 회전을 모두 다룬다. 조사 중 main에 이미 반영된 Bloom 수정은 그대로 보존한다.

> **현행 상태 (2026-10-19):** 사용자 전체 suite PASS 보고로 해당 회귀 검증 문제를 수용했고 승인된 옛 단언 주석 정리·최종 검토도 완료했다(§5). 사용자 native 실행과 에이전트 직접 실행·실제 경기 수용은 구분한다. Bloom·production은 보존하며 게시 상태는 Git과 메인 최종 보고로 별도 확인한다.

## 최상단 — 대체 단언의 보존과 안전 근거

기존 Fox fixture의 「lease 불변 && Quaternion `==`」 복합 단언을 새 분리 단언으로 대체한다. 옛 단언은 먼저 주석으로 비활성화하고 사용자 suite 재실행 수용 후 최종 정리한다.

**현행 정리 단계:** 사용자 전체 suite PASS 수용 후 이번 Fox 옛 단언 주석만 최종 제거하고 활성 토큰 동일성을 확인했다. 아래 최초 안전 근거와 다른 작업의 주석은 보존한다.

안전 근거는 zero Quaternion이 성분상 그대로여도 `==`가 false인 실제 DLL 격리 재현이다. 검증을 지우는 것이 아니라 lease 수와 scope·target·position·rotation snapshot 비교로 분리·보강한다. production 코드나 실제 공격 계약을 삭제하지 않는다.

## 1. 근거와 수정 파일

| 항목 | 근거 |
|---|---|
| Fox impact는 charge lease·emitter를 소비하지 않음 | [유닛 규칙](../../../GameSystemRules/GameSystemRules_Units.md) 「전투 연출 동기화 규칙」 규칙 17 및 FoxMagician typed marker 계약. 기존 `UnitView` 조기 return과 속성 대입 위치도 대조했다. |
| Bloom 전 PS 계약 보존 | 같은 문서 「특수 공격 시스템 규칙」 규칙 34. 현재 main runtime/fixture가 이미 authored 개수·개별 PS 조건을 검사하므로 추가 변경하지 않는다. |
| fixture 비교 교정 | [Research §2](Research.md#2-원본과-현행-코드-확인)의 실제 Quaternion 비교 재현과 현행 복합 단언. |

수정 파일은 **`Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.cs` 하나**, 함수는 **`ValidateFoxMagicianMarkerCallbacks` 하나**다. 다른 suite 함수·VfxPoolItem·UnitView·프리팹·설정·RPC·메모리·상시 문서는 수정하지 않는다.

## 2. 구체 변경

1. impact 전 charge lease 남은 수와 emitter metadata(scope·target·position·rotation)를 snapshot한다.
2. impact 후 lease 불변을 별도 단언하고 metadata 불변을 별도 단언한다. 회전은 `==` 대신 `Equals`로 성분 동일성을 비교한다.
3. fixture의 zero 기본 회전과 유효 회전 조건을 모두 검사한다. 테스트 초기 조건만 구성하며 production 자동 속성 initializer를 identity로 바꾸지 않는다.
4. 각 Host/Client 조건에서 impact 후 lease 유지 → charge 한 번에 소비 → 반복 charge는 재획득·추가 소비 없음의 기존 검증을 유지한다. SOLO/Host/Client의 recipient/local-hit/HP 불변 확인도 유지한다.
5. 실패 문구를 lease 소비와 metadata 변경으로 구별한다. metadata 비교만으로 모든 emitter 호출 부재를 증명한다고 쓰지 않고 현행 production 조기 return의 정적 근거를 함께 유지한다.

Bloom의 최초 5개 가정 수정은 외부 main 통합으로 이미 반영됐으므로 재적용하거나 4개 고정으로 바꾸지 않는다. 타워 추적·recipient fit 등 동시 통합된 변경을 가져오거나 되돌리는 작업도 하지 않는다.

## 3. 확인 방법·위험

- 구현 후 기존 참조로 필요한 별도 컴파일을 확인하고 zero·유효 회전 조건이 모두 포함됐는지 코드 대조한다. 격리 CLR 비교 재현은 fixture 원인 근거이며 native suite PASS와 구분한다.
- 사용자가 기존 Unity 회귀 suite를 재실행한 뒤 결과를 판정한다. 구현·별도 컴파일만으로 전체 suite PASS를 선언하지 않는다. 새 TC·별도 QA·빌드·새 계측 API는 추가하지 않는다.
- 현재 main의 다른 작업과 세대를 보존한다. 최초 Bloom 오류를 최신 main의 활성 실패나 stale DLL 원인이라고 바꾸어 기록하지 않는다.

계획 공유 후 사용자 명시적 승인을 받아 game-programmer가 main에서 구현했다. 문서 담당은 두 Task 문서만 수정했다.

## 4. 반영 결과와 사용자 재실행 경계

위 계획의 검증 함수 한 곳만 반영했다. zero·identity × Host/Client에서 impact 전후 lease와 metadata를 별도로 비교하고 rotation에 `Equals`를 사용한다. 구현 인계 당시 기존 charge/중복/SOLO/HP 확인과 옛 복합 단언 주석 보존을 확인했다. 수용 후 주석 정리는 §5에서 구분한다. production initializer·API·Bloom을 변경하지 않았다.

최종 Runtime/Editor 별도 컴파일은 각 exit 0·오류 0, 경고 23/0이다. 실제 Unity DLL 격리 CLR 비교도 exit 0이며 복사 동일성·성분 변이 검출을 확인했다. 메인은 함수 밖 diff 없음과 단일 최종 SHA 일치를 확인했다. 최초 낡은 rsp source 목록 실패와 최종 source 집합 대조, 임시 로그·명령 근거는 [Research §5](Research.md#5-승인-구현별도-검증-결과)를 따른다.

최초 구현 인계 단계에는 사용자 native suite 재실행 전이었다. 이후 전체 suite 수용과 승인된 문서·메모리 갱신은 §5를 따른다. 에이전트 native·별도 QA·빌드 실행이나 실제 경기 수용으로 확대하지 않는다.

## 5. 사용자 수용과 승인된 마감

사용자의 전체 suite 「모두 PASS」 보고로 이번 회귀 fixture 문제를 수용했다. 정확한 장치·역할·계측 로그나 실제 경기의 공격 시각·HP 수용은 아니며 상세 범위는 [Research §6](Research.md#6-사용자-전체-suite-pass-수용과-최종-정리)을 따른다.

game-programmer가 Fox 옛 단언·대기 설명 4줄만 제거했고 메인은 Editor/Player 활성 토큰·정확 문자열 동일성 및 최종 SHA 일치를 확인했다. 최신 Editor 컴파일 exit 0·오류 0·경고 0, Runtime은 source 무변경으로 기존 통과 근거 유지(재실행 없음)다. 최종 해시·임시 증거는 [Research §6](Research.md#6-사용자-전체-suite-pass-수용과-최종-정리)을 따른다. 문서·메모리는 승인 범위에서 갱신했으며 다른 VFX 비활성 코드·production은 보존했다. 게시 상태는 Git과 메인 최종 보고로 별도 확인한다.
