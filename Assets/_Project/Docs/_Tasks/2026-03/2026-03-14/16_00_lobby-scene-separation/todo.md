# TODO: 로비 씬 분리 — 남은 작업

**작성일:** 2026-03-15
**상태:** 코드 완료 / 씬 셋업 대기

---

## 씬 셋업 (Unity 에디터 작업)

### STEP 1 — Lobby.unity 씬 빌드
- [ ] Unity 메뉴 `Hexiege/UI/Build Lobby Scene` 실행
  - 전체 UI 계층 자동 생성 + SerializeField 자동 연결
  - 폰트(Maplestory Bold SDF), 색상, 레이아웃 포함

### STEP 2 — Build Settings 등록
- [ ] `File → Build Settings` 열기
- [ ] `Assets/_Project/Scenes/Lobby.unity` 추가 → **Index 0** 으로 설정
- [ ] `Assets/_Project/Scenes/Game.unity` → **Index 1** 로 설정
- [ ] 앱 시작 시 Lobby 씬이 먼저 로드되는지 확인

### STEP 3 — Game.unity → Lobby.unity 이전 (⚠️ 중요)
- [ ] Game.unity에서 NGO `NetworkManager` 오브젝트를 **Lobby.unity로 이동**
  - Game.unity 열기 → NetworkManager 오브젝트 선택 → 복사
  - Lobby.unity 열기 → `[Managers]` 하위에 붙여넣기
  - Game.unity의 원본 NetworkManager 오브젝트 삭제
  - ※ LobbySceneBuilder가 커스텀 NetworkGameManager만 생성하므로 NGO NetworkManager는 수동 이동 필요
- [ ] Game.unity에서 기존 `LobbyUI` 오브젝트 비활성화 또는 삭제

### STEP 4 — GameEndUI "로비로" 버튼 추가
- [ ] Game.unity의 `GameEndUI` 오브젝트 선택
- [ ] "로비로 돌아가기" Button GameObject 추가
- [ ] `GameEndUI` 컴포넌트 Inspector에서:
  - `Back To Lobby Button` 필드 → 위에서 만든 Button 연결
  - `Network Game End Controller` 필드 → 씬의 NetworkGameEndController 연결

---

## 테스트 체크리스트

STEP 1~4 완료 후 아래 항목 순서대로 확인:

- [ ] 앱 시작 → Lobby 씬 진입, 전투 탭 + BattleMainView 표시
- [ ] 탭 전환 (전투→상점→프로필→랭킹) 정상 동작
- [ ] **싱글플레이**: 버튼 클릭 → Game 씬 즉시 진입, 맵 로드 정상
- [ ] **커스텀 게임 호스트**: "방 만들기" → 코드 표시, 연결 대기 화면 전환
- [ ] **커스텀 게임 참가**: 코드 입력 → 접속 → 양쪽 Game 씬 동시 전환
- [ ] **게임 종료 후 "로비로"**: NetworkManager Shutdown → Lobby 씬 복귀
- [ ] 에러 발생 시 (잘못된 코드 등) ErrorText 표시

---

## 코드 완료 현황 (참고)

| 항목 | 파일 | 상태 |
|------|------|------|
| MVVM ViewModel | `ViewModels/LobbyViewModel.cs`, `BattleViewModel.cs` | ✅ |
| Lobby View 전체 | `Views/Lobby/**` (11개 파일) | ✅ |
| NGO 씬 전환 | `NetworkGameManager.LoadGameScene()` | ✅ |
| 로비 복귀 | `NetworkGameEndController.BackToLobbyClientRpc()` | ✅ |
| GameEndUI 로비 복귀 | `GameEndUI.OnBackToLobbyClicked()` | ✅ |
| 씬 빌드 자동화 | `Editor/LobbySceneBuilder.cs` | ✅ |
