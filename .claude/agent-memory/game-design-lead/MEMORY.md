# Game Design Lead Memory — Hexiege

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

## 핵심 시스템별 확정 설계

### 헥스 그리드
- 좌표계: Cube (Q, R, S=-Q-R)
- 기본 맵: FlatTop 10×29 (멀티플레이 기본)
- 보조 맵: PointyTop 7×17 (지원)
- 타일 색상: Neutral=회색, Blue=파랑, Red=빨강, Selected=노란 틴트

### 유닛 스탯 (2026-03-14 최종 확정)

| 항목 | Pistoleer(권총병) | Assault(돌격소총병) | Sniper(저격총병) |
|------|-----------------|-------------------|----------------|
| HP | 30 | 50 | 30 |
| 공격력 | 3 | 6 | 20 |
| 사거리(float) | 1.0 | 2.0 | 5.0 |
| 이동속도(초/칸) | 1.0 | 1.0 | 4.0 |
| 생산시간 | 5초 | 10초 | 15초 |
| 골드비용 | 50 | 100 | 200 |
| 인구 | 1 | 1 | 1 |
| UnitType enum | Pistoleer=0 | Assault=1 | Sniper=2 |

- **사거리 설계 의도**:
  - Pistoleer 1.0: 인접 타일(0.866 world units) 범위
  - Assault 2.0: 2타일 범위 (약 1.73 world units)
  - Sniper 5.0: 5타일 원거리 (약 4.33 world units)
- **팀별 피아식별**: Blue/Red 각각 별도 프리팹 사용, 에셋+코드 연동 완료 (2026-03-14)
- **3D 모델**: Meshy.ai 제작, Mixamo 애니메이션 (Walk/Attack/Dead)
- **Animator**: Walk/Attack/Dead 스테이트, IsDead bool 파라미터 (Animator.Play() 직접 호출)
- **방향 표현**: Y축 회전 (NE=30°, E=90°, SE=150°, SW=210°, W=270°, NW=330°)
- 미래 계획: 3종족, 추가 유닛 타입 (TDD Phase 3)

### 건물
| 타입 | HP | 건설 비용 | 역할 |
|------|----|-----------|------|
| Castle | 50 | 자동 배치 | 본기지, 파괴 시 패배 |
| Barracks | 30 | 100골드 | 유닛 생산 |
| MiningPost | 20 | 50골드 | 채굴소, 금광 타일 전용 |

**3D 모델**: Meshy.ai로 제작 예정 (Castle/Barracks/MiningPost)

### 자원 시스템
- 시작 골드: 500
- 기본 수입: 0 (채굴소 없으면 무수입)
- 채굴소 수입: 10골드/초
- 배럭 비용: 100골드, 채굴소 비용: 50골드

### 인구 시스템
- 최대 인구 = 보유 타일 수
- 사용 인구 = 건물 수 + 유닛 수

### 전투
- 이동 중 인접(거리 1) 적 자동 공격
- Lerp 이동 중 매 프레임 거리 체크 → 적 발견 시 공격
- ClaimedTile: 이동 중 타일 선점(같은 팀만 차단, 적팀 투과)
- 타일 중앙 도착 = 전투 승리 = 점령

### 생산 시스템 (전역 규칙 5가지 확정 — 2026-03-23)
- 수동 모드: 탭 → 큐 추가 (최대 대기 2 + 생산 중 1)
- 자동 모드: 롱프레스(0.5초) 토글, 지정 유닛 무한 생산 (최대 3종)
- 랠리포인트: 배럭 클릭 후 타일 지정, 생산된 유닛 자동 이동
- 공성 시스템: 랠리 도착 후 적 Castle 방향 자동 전진

**전역 규칙 5가지** (상세: `GameDesignDocument.md` → "생산 패널 운영 규칙"):
1. 생산 취소(슬롯 X 버튼) 시 항상 전액 환불
2. 슬롯에 표시된(골드 차감된) 자동 항목은 자동 취소 후에도 수동 큐 이관으로 생산 유지
3. 수동 추가 시 자동 모드 해제 (Rule 2 이관 선행)
4. 생산큐 최대 3개 = 현재 생산 중 + 수동 대기 (자동 대기 별도)
5. 비용 차감은 슬롯에 표시되는 시점 (슬롯 풀이면 미차감, 슬롯 진입 시 차감)

### 승패 조건
- 적 Castle HP = 0 → GameEndUseCase → OnGameEnd 이벤트 → UI 표시

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

## 미구현/미결 기획 항목
- 카메라 각도 최적화 (현재 55도 적용, 테스트 후 조정 가능)
- 3종족 시스템 (TDD Phase 3)
- AI 상태머신 (현재는 공성 시스템으로 임시 대체)
- 사운드/BGM
- 튜토리얼
- 밸런싱 (골드/생산시간/HP — 유닛 기본 스탯은 2026-03-13 확정, 건물 HP/수입 등 추가 조정 가능)
- PlayFab 백엔드 연동 (계정/랭킹/인앱결제)
- 멀티플레이 로비 UI 완성

## 비주얼 목표
- 레퍼런스: Clash of Clans, Clash Royale
- 스타일: 카툰/스타일라이즈드, 밝고 선명한 색상
- 뷰: Orthographic 55도 탑다운 이소메트릭
- 모바일 최적화: 로우~미드폴리 3D 모델
