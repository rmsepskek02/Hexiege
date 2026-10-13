# C3 독립형 피격 표현 스냅샷 — 조사

서버는 피해 결과를 정확히 계산해 양쪽 기기에 모두 전달했지만, 화면 효과를 재생하는 마지막 단계가 이미 죽거나 사라진 유닛 객체를 다시 찾고 있었다. 이번 작업은 확정된 공격 결과 자체에 화면 표시용 정보를 완결해, 피해자가 제거된 뒤에도 맞은 위치에서 피해 숫자와 피격 효과를 정확히 한 번 보여 주도록 고치는 작업이다.

## 1. 실기 실패 증거

- 2026-09-09 동일 경기 `afc490a9d344ef1dd2ea827a5eeea872fca2b049fc621bb043b6961c6534138d`, Editor Host / Android Client.
- C2 서버 결과 863/실패 0, Client 수락 863/거부 0.
- C3 완료 묶음 863/863, pending·duplicate·transport failure 0.
- 화면 표시 대상 800건 중 Host 747/`viewUnavailable` 53, Client 737/`viewUnavailable` 63.
- B3와 양측 로컬 ROOT는 PASS이므로 이동·서버 피해·결과 전송을 이번 원인으로 보지 않는다.

## 2. 코드에서 확인된 구조 원인

`AttackResultPresentationInput`은 피해자 ID·피해량·결과 HP·Impact 위치를 전달하지만 피해자 타입과 팀을 전달하지 않는다. `HitPresentationQueue.TryPrepareAuthoritative`는 별도 `OnEntityDamaged` 이벤트에서 보관한 `IDamageable`이나 현재 UseCase/Factory 조회가 성공해야 다음 단계로 갈 수 있다.

서버에서는 피해 이벤트 직후 사망 이벤트와 Domain 제거가 이어지고, Client에서는 HP 동기화와 C3 완료 묶음이 서로 다른 RPC 흐름이다. 따라서 결과가 온전해도 피해자 캐시·Domain·View가 먼저 사라지거나 아직 준비되지 않을 수 있다. 현재 0.5초 재시도는 이 입력 결손을 해결하지 못하고 `viewUnavailable`로 닫는다.

기존 production adapter 자동 검증은 `OnEntityDamaged`를 먼저 직접 발행해 캐시를 채운 뒤 완료 묶음을 넣는다. 실제 실패 순서인 결과 선도착, 치명타 제거 뒤 결과, 캐시 없는 View 부재를 재현하지 않아 PASS가 실제 멀티플레이 완성도를 보장하지 못했다.

## 3. 가설과 판별 예측

1. 결과 입력 불완전성이 주원인이라면 캐시와 현재 View 없이도 타입·팀·Impact 위치가 든 결과만으로 HP 텍스트/VFX를 방출하게 했을 때 Host/Client `viewUnavailable`이 사라진다.
2. 치명타/Despawn 경주가 주요 발생 조건이라면 `ResultingHp=0` 결과를 제거 뒤 소비하는 회귀가 현재 구현에서 실패하고 교정 뒤 정확히 한 번 표시된다.
3. Client RPC 순서가 추가 누락 원인이라면 `Bundle→Health`와 `Health→Bundle` 순열의 결과가 교정 뒤 동일해야 한다.
4. AoE 한 피해자의 준비 실패가 누락을 확대한다면 일부 View가 없는 묶음이 현재 전체 보류되고, 교정 뒤 모든 self-contained 결과를 같은 tick에 각각 한 번 표시한다.

## 4. 보존할 경계

- HP·사망·사거리·타겟·공격 방향은 계속 서버 권위다.
- C1 회차와 C2 판정 결과, B3 이동 writer는 변경하지 않는다.
- `Supported`는 ResultPresentation 단일 emitter, `Unresolved`는 기존 LegacyFallback을 유지한다.
- 신규 VFX 에셋은 만들지 않고 기존 타입별 피격 프리셋을 사용한다.
- 늦은 참가 baseline과 age 제한, 정규 결과 키 멱등성은 유지한다.

## 5. v7 첫 실기 결과와 새로 확인된 누락

- 2026-09-09 동일 경기 `dae3d6225be0b847d0dcbc41d6527364253a9001b278a72714dac570b17ea755`, Android Host / Editor Client.
- 양측 `ResultPresentation`, 공격 관측 v7, 완료 묶음 v3가 일치해 혼합 버전 표본이 아니다.
- C2는 서버 결과 1,251건/실패 0, Client 수락 1,251건/거부 0이었다.
- B3는 서버 56,131프레임에서 reject·invalid·writer 충돌·stationary Walk 위반·공간 commit 실패가 모두 0이고 양측 로컬 ROOT도 PASS였다.
- C3는 Miss 계열 192개 묶음만 전달됐고, 적중 결과 1,059건은 `server-completed-bundle-publish-rejected`로 전송 전에 거부됐다.

피해 적용 경계의 `AppliedAttackPresentationFact`에는 실제 대상 위치가 수집됐지만, C2 `AttackImpactResult`는 `HasImpactPosition=false`로 고정 생성됐다. 완료 묶음 조립은 주 결과에 fact의 위치를 연결하지 않고 C2의 빈 위치를 그대로 사용했다. v7이 시각 결과의 Impact 위치를 올바르게 필수화하자 이 연결 누락이 모든 적중 결과에서 드러났다. 기존 자동 검증은 완성된 입력을 직접 만들어 transport 왕복만 확인했기 때문에 production의 `피해 fact에는 위치 있음 / 주 C2 결과에는 위치 없음` 모양을 재현하지 못했다.

현재 `UnitEffectConfig`의 모든 `hitPreset`은 비어 있으며 실제 게임의 VFX는 공격 VFX와 제거 시 사망 VFX가 중심이다. 피격 VFX 경로는 향후 에셋 연결을 위한 선택 채널로 보존하되, 에셋 미설정은 정상 생략이어야 한다. 이번 C3 실패는 피격 VFX 에셋 부재가 아니라 미래 채널에도 필요한 권위 결과 위치가 묶음에 연결되지 않은 데이터 계약 실패다.
