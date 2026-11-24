# MistShrine 멀티 검증용 진단 로그 — Research

## 무엇을 왜 하나요

초월 종족의 힐 건물 MistShrine은 2026-08에 **싱글플레이로만** 검증했고, 멀티플레이 4항목은 한 번도 확인하지 못했습니다([원 task Plan](../../2026-08-10/14_12_mistshrine-heal-implementation/Plan.md) 10-3절).
오늘 1차 멀티 테스트를 했지만, MistShrine 전용 기록(로그)이 하나도 없어서 통과 여부를 판단할 수 없었습니다.
그래서 **게임 동작은 바꾸지 않고, 판단에 필요한 개발용 진단 로그만 추가**한 뒤 다시 테스트합니다.

> **상태: 종결 (2026-11-25).** 진단 로그를 넣은 뒤의 멀티 테스트에서 4항목 모두 **PASS**했다. 새로 찾은 「물안개 1개가 11회 회복」 결함은 다음 작업으로 넘겼다 → [6절](#6-multi-result-summary) · [Plan 6절](Plan.md#6-multi-result).

---

## 1. 1차 멀티 테스트 결과 (로그 `mistshrine-multi_01`, Editor=Host · 기기=Client)

- **판정 불가.** 신전 배치·재생성 줄(Host 3줄, Client 3줄)만 있고 시전·쿨다운·토글·물안개 회복 기록이 없다.
- Client `유닛 힐 동기화 | UnitId=79, AppliedHeal=10` 5줄이 있으나 줄에 회복 출처가 없어 물안개 회복인지 가를 수 없다.
- Client 건물 HP 상승 2건이 보인다(메인 세션 판독) — 건물 회복 동기화의 **간접 증거**일 뿐, 출처를 확정하지 못한다.

## 2. 로그가 없던 이유

- 2026-08 진단 로그를 전량 제거해 지금 코드에 MistShrine 전용 진단이 없다(`grep -rn "Diag=MistShrine\|MIST-DIAG" Assets/_Project/Scripts` 0건).
- 건물 회복 수신 `SyncBuildingHeal`(`Infrastructure/Network/NetworkHealthSync.cs:467`)에는 로그가 없고, 유닛 쪽 줄(444)에는 회복 주체 Id가 없다.
- 기존 `[SKILL-REGEN] HealAmpSample Source=Mist`는 재생의 뿌리가 켜졌을 때만 나온다.

## 3. 관련 코드 (경로 접두 `Assets/_Project/Scripts/`)

| 역할 | 위치 |
|---|---|
| 패널 입력(탭·롱프레스 0.5초) | `Presentation/UI/MistShrinePanelUI.cs` `HandleTap` 384 · `HandleActivate` 406 · `HandleToggleAuto` 419 |
| 시전·토글 요청 검증 | `Infrastructure/Network/NetworkMistShrineController.cs` `ActivateMistServerRpc` 166 · `ToggleAutoServerRpc` 188 · `ValidateRequest` 216 |
| Client 브로드캐스트 | 같은 파일 `MistActivatedClientRpc` 271 · `AutoModeChangedClientRpc` 286 (둘 다 Host는 return) |
| 시전·쿨다운·물안개 틱 | `Application/UseCases/MistShrineUseCase.cs` `Activate` 231 · `StartCooldownLocal` 313 · `TickCooldowns` 331 · `TickAutoCast` 404 · `TickMists` 447 · `ApplyHealTick` 522 |
| 회복 적용 | 같은 파일 `ApplyHealToUnitEntity` 596 · `ApplyHealToBuildingEntity` 620 · 정리 `OnShrineDestroyed` 725 · `ClearAll` 734 |
| 틱 호출 | 싱글·Client 쿨다운 `Bootstrap/GameBootstrapper.cs:742`, 싱글 물안개 754-755 / Host `Infrastructure/Network/NetworkCombatController.cs:805-807` |
| HP 동기화 송신·수신 | `Infrastructure/Network/NetworkHealthSync.cs` `OnEntityHealed` 184(건물 송신 204) · `SyncBuildingHeal` 467 · `SyncUnitHeal` 417 |

## 4. 주의할 점

- **서버 거부는 정상 플레이에서 거의 안 난다.** Client는 쿨다운 중 탭을 패널에서 먼저 버리고, 팀·소유 거부는 변조 요청에서만 난다. 거부 0건은 「PASS」가 아니라 「미발생」으로 기록한다.
- **출처 혼재.** 건물이 주는 회복(`healerIsUnit=false`)은 물안개와 재생의 뿌리 둘 다다. Client 진단은 회복 주체 Id가 HealShrine일 때만 물안개로 센다.
- **이중 틱은 정상 경로에서 불가능하다**(싱글 754 가드와 UseCase 내부 가드가 같은 네트워크 상태를 본다). 그래서 진단은 「없음을 확인」하는 계측이다.
- 멀티 경기 종료 시 Host 틱이 멈춰 물안개가 끝나지 않은 채 남는다 → 경기 종료 때 요약을 한 번 쓴다.

## 5. 결정 (2026-11-24)

- 사용자: 「로그 작성해서 다시 테스트해보자」, 연막탄 미커밋 작업은 그대로 둔다(이번 작업과 섞지 않음).
- 메인 기본안: System 값은 파일별 기존 관례(`Skill` 사용 안 함) · 존속 `개발` · 한 줄 900바이트 이하 · 서버 시각 필드 없이 각 기기 경과 ms와 개수 대조로 판정.

<a id="6-multi-result-summary"></a>
## 6. 진단 후 멀티 테스트 결과 (2026-11-25)

상세 회차 · 수치 · 판정은 [Plan 6절](Plan.md#6-multi-result)이 원본이다. 여기에는 조사 관점의 요약만 둔다.

- **판정:** ① Client 시전 서버 재검증 · 쿨다운 동일 감소 · ② 건물 HP 회복 Client 동기화 · ③ Host 이중 틱 없음 · ④ 자동 모드 토글 브로드캐스트 — **4항목 PASS**(유효 회차 2개, 멀티 3판, Android = Host · 에디터 = Client). 회복 증폭 1.5배가 한 번만 곱해지는 것도 확인됐다.
- **4절 「주의할 점」의 예상과 실제:** 서버 거절은 예상대로 0건(미발생)이었다 — 정상 플레이로 생기지 않는 경로라 사용자와 협의해 추가 테스트를 생략했다. 이중 틱도 예상대로 0건이었다. 경기 종료 요약(`SyncSummary`)으로 Host 송신 = Client 수신을 대조할 수 있었다.
- **2절의 「출처 혼재」 대응:** Client 진단이 회복 주체가 HealShrine일 때만 세도록 한 덕분에 건물 회복 수가 Host 송신 · Client 수신 · 물안개 종료 합계 세 곳에서 같게 나왔다.
- **새로 찾은 것:** ⓐ 물안개 1개가 11회 회복(관찰 31개 중 1개 — 지속시간 끝 프레임에서 틱 발화가 소멸보다 먼저 처리됨) → 다음 작업. ⓑ 신전 파괴 뒤에도 Client 쿨다운 미러가 돌아 만료 줄 1개를 남김 — 표시용 타이머만 남는 현상이며 버그 아님(기록만).
