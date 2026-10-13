# C3 독립형 피격 표현 스냅샷 — 계획

공격 결과가 이미 확정됐다면 피해 유닛이 화면에서 사라졌더라도 그 결과만으로 피해 숫자와 피격 효과를 재생할 수 있어야 한다. 이번 교정은 현재 유닛 객체를 다시 찾는 의존성을 필수 경로에서 없애고, 살아 있는 View가 있을 때만 추가 반응을 적용한다. 서버 피해 계산과 기존 공격·이동 구조는 그대로 유지한다.

> 기존 LegacyFallback은 25종 전환과 사용자 실기 PASS 전 삭제하지 않는다. 이번 변경은 `Supported + ResultPresentation`의 최종 피격 소비 경계만 교정하며 C1/C2/B3 writer를 바꾸지 않는다.

## 1. 규칙 근거

| 수정 항목 | 규칙 근거 |
|---|---|
| 확정 결과의 self-contained 표현 스냅샷 | `NET-PRESENT-003`, Units 「전투 연출 동기화 규칙」 |
| 사망·Despawn 뒤 확정 결과 보존 | `NET-TIME-003~005`, `NET-CANCEL-005` |
| 현재 View와 서버 권위 결과 분리 | `NET-AUTH-002`, `NET-ROOT-002` |
| ID 재사용 시 과거 View 반응 금지 | `NET-TIME-004`, `NET-PRESENT-003` |
| AoE 완결 묶음·단일 방출 | `NET-PRESENT-003`, Units 규칙 26 |
| 타임아웃을 정상 동기화로 사용하지 않음 | `NET-PRESENT-004` |

## 2. RED 회귀 고정

실제 production adapter 경계에서 다음 순서를 먼저 실패시키고, 구현 후 같은 검증을 통과시킨다.

- 별도 피해 이벤트와 피해자 View가 전혀 없어도 유효 스냅샷 결과가 HP 텍스트 또는 VFX를 정확히 한 번 표시한다.
- 치명타 결과를 Domain 제거·View Despawn 뒤 소비해도 저장된 Impact 위치에 표시한다.
- 결과와 HP 동기화 도착 순서를 서로 바꿔도 동일하다.
- 같은 피해자 ID가 재사용돼도 과거 결과가 새 View를 펀치하지 않는다.
- AoE 일부 피해자의 View가 없어도 완결 묶음 전체의 필수 표현이 같은 방출 tick에 각각 한 번 실행된다.
- 손상된 타입·팀·Impact 위치는 fail-closed하며 정상 결과로 위조하지 않는다.

## 3. 결과 계약 확장

`AppliedAttackPresentationFact`와 `AttackResultPresentationInput`, 네트워크 완료 묶음에 피해자 표현 타입과 팀을 추가한다. 시각 결과는 유효한 Impact 위치와 표현 스냅샷을 반드시 가져야 한다. Miss/취소처럼 화면 피격 표현이 없는 결과는 명시적 sentinel을 허용한다.

완료 묶음의 전송 형식이 바뀌므로 Host/Client 관측 스키마는 `c3-authoritative-result-presentation-shadow-v7`, 완료 묶음 coordinator 스키마는 `c3-complete-bundle-presentation-v3`으로 올린다. 구버전과 신버전 로그는 같은 계약으로 비교하지 않는다.

서버의 한 피해 적용 호출 안에서 스냅샷을 캡처해 피해자 제거 뒤 현재 상태를 재조회하지 않는다. 네트워크 직렬화·역직렬화와 equality/duplicate classifier도 새 필드를 포함해 Host와 Client가 byte-level 의미가 같은 결과를 소비하게 한다.

## 4. Presentation adapter 교정

- HP 텍스트는 `ResultingHp + VictimTeam + ImpactPosition`으로 표시한다.
- 유닛 피격 VFX는 `VictimPresentationType + ImpactPosition`으로 표시한다.
- 현재 View와 동일 Domain 객체가 확인되면 펀치를 추가한다. 캐시가 없거나 객체가 교체됐으면 펀치만 생략한다.
- 필수 채널은 현재 `IDamageable`과 View를 요구하지 않는다. 별도 피해 이벤트 캐시는 Legacy 및 선택 반응에만 사용한다.
- self-contained 입력이 유효하면 피해자별 준비 실패로 AoE 전체를 만료시키지 않는다.

## 5. 진단과 PASS gate

- `viewUnavailable`은 원인별 bounded evidence로 나눈다: 스냅샷/Impact 계약 손상, 필수 presenter 미준비, 준비 후 실제 채널 방출 실패. 선택 View identity 불일치는 필수 실패와 분리한다.
- terminal에는 필수 표현 실패 원인을 `스냅샷 손상 / presenter 미준비 / 실제 채널 방출 실패` 순서의 압축 계수로 남기고, 살아 있는 View가 없어 선택 펀치만 생략한 횟수는 별도 계수로 남긴다.
- 선택 펀치 생략은 필수 표현 실패로 계산하지 않는다.
- 전용 production adapter 검증과 전체 UAS self-validation이 모두 PASS여야 한다.
- 새 네트워크 필드 왕복·중복·충돌·AoE·사망/순서 역전 회귀가 PASS여야 한다.
- 컴파일 오류와 전투 ERROR가 0인 경우에만 Android Build And Run을 시작한다.
- 실기 판정은 양측 `expectedVisual == presentationEmits`, `viewUnavailable=0`, duplicate·transport failure·gameplayWrites=0을 요구한다.

## 6. 예상 수정 파일

- `Application/Combat/AttackDamageApplyStatus.cs`
- `Application/UseCases/UnitCombatUseCase.cs`
- `Application/Combat/Sequencing/UnitAttackResultPresentationShadow.cs`
- `Infrastructure/Network/NetworkUnitActionShadowState.cs`
- `Infrastructure/Network/NetworkAttackPresentationBundle.cs`
- `Presentation/Effects/HitPresentationQueue.cs`
- `Presentation/UI/FloatingHpTextSpawner.cs`
- C3 전용 및 전체 self-validation 스크립트

## 7. 완료 상태 기준

문서 작성만으로 완료하지 않는다. 자동 검증 PASS와 Build And Run 시작까지가 이번 구현 단계이며, 최종 완료는 새 Android/Editor 동일 경기 실기 로그에서 C3 표현 누락 0을 확인한 뒤 판정한다.

## 8. v7 실기 FAIL 후 보강 계획

첫 v7 실기에서 적중 1,059건이 모두 완료 묶음 전송 전에 거부됐다. C3 조립기에서 임시 위치를 만들어 우회하지 않고, 서버 피해 writer가 실제로 적용한 주 대상 위치를 `AttackDamageObservation`에 함께 반환한다. C2 결과 생성기는 그 위치를 `AttackImpactResult`에 처음부터 기록하고, 완료 묶음은 C2 주 결과와 같은 호출에서 수집된 주 피해 fact의 위치·피해량·HP가 정확히 일치할 때만 전송한다. Miss는 위치 없는 sentinel을 계속 허용한다.

회귀 검증은 완성 DTO를 직접 만드는 얕은 왕복 외에 실제 production 순서를 추가한다. `피해 적용 → 위치가 든 observation → C2 결과 생성 → fact와 일치 검증 → 완료 묶음 직렬화` 전체를 통과시켜, 적중은 위치가 있고 Miss만 위치가 없음을 확인한다. fact와 결과 위치가 다르면 fail-closed한다.

피격 VFX는 미래 에셋을 위한 선택 채널로 유지한다. `hitPreset` 미설정은 `SkippedNoAsset`로 정상 집계하고, 프리셋이 설정됐는데 실제 방출이 실패한 경우만 `ChannelFailure`로 판정한다. 사망 VFX는 변경하지 않는다. terminal은 transport 실패의 정확한 단계와 `configured / emitted / skippedNoAsset / channelFailure`를 분리해 다음 실기에서 원인을 한 번에 식별할 수 있게 한다.

이 보강은 v7과 terminal 의미 및 주 결과 필수 계약이 다르므로 관측 스키마를 `c3-authoritative-result-presentation-shadow-v8`로 올린다. 새 빌드의 Host/Client가 모두 v8일 때만 결과를 비교한다.
