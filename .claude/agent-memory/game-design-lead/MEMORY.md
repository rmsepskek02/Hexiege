# Game Design Lead Memory — Hexiege

## 2026-08-24 동적 건물 재탐색 판정 기준
- 건물로 현재 경로가 무효화돼도 서버 경로 그래프에 우회로가 있으면 같은 목표로 이동을 계속해야 한다. `Blocked`는 현재 environment revision에서 실제 유효 경로가 없을 때만 정상이다.
- 건물로 현재 `path[0]`이 non-walkable이 된 경우 유닛이 이미 서 있는 그 타일에서 인접 walkable 타일로 빠져나가는 첫 구간만 허용한다. 재진입·건물 관통·비인접 점프는 금지한다.

## ⚠️ GIT 명령 절대 금지 (CRITICAL — 예외 없음)
- **`git restore`, `git reset`, `git checkout`, `git commit`, `git push` 등 모든 git 명령은 사용자가 명시적으로 직접 언급하지 않는 한 절대 실행 금지**
- 2026-03-03 사고: git restore 무단 실행 → 커밋 안 된 작업 전체 삭제 (복구 불가)

## 게임 컨셉
- 장르: 헥스 그리드 기반 1v1 실시간 전략 (RTS)
- 플랫폼: 모바일 세로 화면 (9:16, Android/iOS)
- 테마: Clash of Clans 스타일 Orthographic 이소메트릭 (55도 틸트), 3D 메시
- 매칭: P2P Host-Client (Unity Relay NAT 관통)

## 핵심 게임플레이 루프
1. 시작: 양 팀 Castle 자동 배치 + 시작 골드 500
2. 확장: 타일 점령 → 인구 한도 증가 → 건물 건설
3. 생산: 배럭 → 유닛 생산 큐 → 랠리포인트 설정 → 자동 공성
4. 전투: 유닛이 이동 중 인접 적 자동 공격 (IDamageable)
5. 승리: 적 Castle HP = 0이 될 때까지 공성

## 토픽 파일 인덱스

| 토픽 파일 | 내용 |
|---|---|
| [core-systems.md](core-systems.md) | 핵심 시스템별 **확정 설계 상세** — 연구소 유닛 강화·전투 스탯 ×10 / 헥스 그리드 / 유닛 스탯표 / 특수 유닛 AoE 밸런스 / 건물 / 자원 / 인구 / GameSystemRules 요약 / 전투 / 생산 시스템 5규칙 / 승패 조건 |
| [ai-scenarios.md](ai-scenarios.md) | 싱글플레이 AI 시스템(2026-06-10 완료) — 시나리오 에셋 구조, Phase 4단계, 3종족 시나리오 A/B/C와 생산 건물 라인 표 |

> 이 파일에는 **매 기획 작업마다 필요한 것**(컨셉·루프·관점·멀티 기획·완료/미결 목록·비주얼 목표)만 남긴다. 수치와 표가 큰 확정 설계는 2026-08-24에 위 두 토픽으로 옮겼다(삭제 아님 — 원문 그대로 보존).

## 팀별 관점 (확정)
- Blue팀: 자기 Castle 맵 하단 → 화면 하단에 보여야 함 (그대로)
- Red팀: 자기 Castle 맵 상단 → 화면 하단에 보여야 함 (반전 필요)
- 채택 방식: **ViewConverter** (좌표 변환, 메시 뒤집기 없음)
  - `ToView(pos) = 2*mapCenter - pos` (Red팀만, Blue팀은 항등)
  - 유닛 이동 방향도 FlipDirection() 적용
  - 카메라 Z축 180° 회전 방식은 폐기 (메시 뒤집힘 문제)

## 멀티플레이 기획
- Host = Blue팀, Client = Red팀
- 서버(Host) 권위 모델: 모든 행동 서버 검증
- 동기화 대상: 건물 배치, 유닛 생산, 타일 소유권, 자원, HP, 승패
- 클라이언트 예측: 유닛 이동 (로컬 즉시 + 서버 동기화)
- 재접속: 30초 대기 후 남은 플레이어 승리 처리

## 구현 완료 기능
- 헥스 그리드 (PointyTop/FlatTop 듀얼 지원)
- 유닛 이동 + A* 경로탐색
- 유닛 3D Animator 기반 애니메이션 (IsWalking/IsDead/Attack)
- 전투 시스템 (IDamageable, 이동 중 자동 공격)
  - **전투 거리 정밀도 개선 (2026-03-02)**: 월드좌표 기반 거리 체크 (IEntityPositionProvider)
  - FlatTop 인접 타일 거리 = 0.866 world units 균일 → 단일 임계값으로 정밀 판정
- 건물 배치 (Castle/Barracks/MiningPost)
- 자원 시스템 (골드 수입/지출)
- 인구 시스템
- 유닛 생산 (수동/자동 + 랠리포인트 + 공성)
- 승패 판정 (Castle 파괴)
- HUD (골드/인구/타일 카운트)
- 멀티플레이 인프라 (Lobby/Relay/NGO Phase 1~8)
- ViewConverter (팀별 관점 반전, 구현 완료)
- **2D→3D 전환 완료 (2026-02-27~2026-03-01)**
  - XZ 평면 좌표계, Orthographic 55도 틸트 카메라
  - Animator 기반 유닛, sortingOrder 폐기
  - **건물 3D 모델 완료** (Castle/Barracks/MiningPost — Meshy.ai Image-to-3D, Blue/Red 팀별 프리팹)
  - **헥스 타일 3D 모델 완료** (ProBuilder Cylinder + SG_HexTile Shader Graph)
  - **금광 타일 3D 오브젝트 완료** (크리스탈 바위 더미, GoldMineTile.prefab)
  - **랠리포인트 마커 완료** (RallyPointMarker.prefab)
- **팀별 피아식별 에셋+코드 연동 완료 (2026-03-14)**
  - 유닛 Blue/Red 프리팹: Pistoleer, Assault(돌격소총병), Sniper(저격총병)
  - 건물 Blue/Red 프리팹: Castle, Barracks
  - 반응형 팝업 UI: ProductionPopup/BuildingPopup 앵커 기반 배치 전환
  - 팀별 초상화 동적 업데이트: ProductionPanelUI/BuildingPlacementUI Show() 시 팀별 스프라이트 교체
  - Assault/Sniper 생산 버튼 추가 (ProductionPanelUI) ✅
- **전투 범위 수정 (2026-03-14)**: epsilon +0.1f 제거 → 타일 점령 타이밍 정상화

> 여기 있던 「싱글플레이 AI 시스템 (2026-06-10 완료)」 상세(시나리오 구조 · 3종족 시나리오 A/B/C · 생산 건물 라인 표 3개)는 2026-08-24에 [ai-scenarios.md](ai-scenarios.md) 로 옮겼다.

## 미구현/미결 기획 항목
- 카메라 각도 최적화 (현재 55도 적용, 테스트 후 조정 가능)
- ~~AI Inspector 에셋 작업 (AIScenarioConfig_Spirit/Transcendence.asset 생성 + 수치 입력)~~ ✅ 완료 (2026-06-10): 3종족 단일 에셋 구조로 완성
- AI 실기 테스트 (빌드오더 동작 확인)
- 사운드/BGM
- 튜토리얼
- 밸런싱 (AI delay 수치는 예비값 — StatsReference.md 확정 후 경제 시뮬레이션 재조정 예정)
- PlayFab 백엔드 연동 (계정/랭킹/인앱결제)
- 멀티플레이 로비 UI 완성

### 2026-07-20 - 유닛 이동·공격 규칙 v2 확정

- 서버 권위를 유지한 공통 행동 단계를 AlignToMove / Move / Acquire·Chase / AlignToAttack / Windup / Impact / Recovery로 확정했다.
- 이동은 10° 이내에서 시작하고 이동 중 15°를 넘으면 재정렬한다. 공격은 5° 진입·8° 유지 기준이며, 커밋 전 취소는 무비용이고 커밋 후에는 쿨다운을 환불하지 않는다.
- 공격 전달 방식은 MeleeContact / Hitscan / ProjectileImpact / TravelingArea로 분리하고 TargetScope·AreaShape·Effect·Schedule은 독립 축으로 둔다.
- BloomFairy의 성공 발동 후 3초, Windup 포함 총 4초 예외는 유지했다. 구체 규칙은 `GameSystemRules_Units.md`와 `GameSystemRules_UnitCombatSynchronization.md`가 권위다.
- 문서 설계만 완료했으며 25종 런타임 구현·멀티 검증은 아직 0/25다.

## 비주얼 목표
- 레퍼런스: Clash of Clans, Clash Royale
- 스타일: 카툰/스타일라이즈드, 밝고 선명한 색상
- 뷰: Orthographic 55도 탑다운 이소메트릭
- 모바일 최적화: 로우~미드폴리 3D 모델
### 2026-07-16 - Profile/ranking UX policy update

- Lobby Profile/Ranking cloud slice is complete from a UX-policy perspective: verified email/Google users should have a nickname code, Profile shows account/profile/stats/rank, and Ranking lists only eligible leaderboard entries.
- Nickname setup remains mandatory for first verified login before entering Lobby; nickname change is available from Profile as a modal flow.
- Next UX decision: email verification abandonment. Fresh sign-up verification and existing unverified-login retry should be treated as separate states so the player understands whether going back cancels sign-up or only exits verification retry.
