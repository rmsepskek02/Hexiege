# QA 패턴 및 버그 히스토리

## 세션: 2026-02-21 — 팀 기반 입력 / 터치 팬 / 카메라 뷰 플립

### 발견된 패턴
1. 팀 일반화 작업 시 StartAutoMove 같은 유틸 메서드의 하드코딩 잔존 위험
   - 검사 방법: `TeamId.Blue|TeamId.Red` grep 후 LocalPlayerTeam.Current 교체 여부 확인

2. `using` 선언이 실제 사용 없이 남는 경우 발생
   - CameraController의 `using Hexiege.Infrastructure` — 코드 내 Infrastructure 타입 미사용
   - 검사 방법: `Infrastructure\.` grep으로 실사용 확인

3. Debug.Log를 제거하지 않고 PR 병합된 케이스
   - InputHandler.IsPointerOverUI: 매 클릭마다 2줄 이상 로그
   - 규칙: 프로덕션 빌드 전 `[InputHandler]` 태그 로그 전수 확인

### 주요 버그 목록
| ID | 심각도 | 파일 | 라인 | 설명 | 상태 |
|----|--------|------|------|------|------|
| B001 | Major | CameraController.cs | 265 | 에디터 터치 팬 차단 (mouse==null 조건) | OPEN |
| B002 | Major | InputHandler.cs | 363,375 | StartAutoMove 팀 일반화 범위 확인 필요 | 기획 확인 대기 |
| B003 | Minor | InputHandler.cs | 437-439 | Debug.Log 프로덕션 노이즈 | OPEN |
| B004 | Minor | CameraController.cs | 302-308 | Red팀 카메라 위치/경계 실기기 확인 필요 | 테스트 대기 |
| B005 | Minor | InputHandler.cs | 395-410 | FindClosestWalkableNeighbor IsWalkable 미검증 | OPEN |
