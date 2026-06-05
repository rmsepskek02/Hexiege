# Plan — NetworkGameManager 정리

## 작업 목적 (자연어 설명)

두 가지를 정리합니다:
1. **Game.unity에서 불필요한 NetworkGameManager GameObject 제거** — Lobby에서 생성된 NGM이 DontDestroyOnLoad로 유지되므로 Game씬에 따로 배치할 필요가 없습니다.
2. **GameBootstrapper.cs에서 미사용 `_networkGameManager` 필드 제거** — 선언만 있고 코드에서 전혀 쓰이지 않습니다.

두 작업 완료 후 DontDestroyOnLoad 경고도 자연히 사라집니다.

---

## ⚠️ 기존 로직 제거 규칙 적용 항목

이 작업에는 기존 오브젝트/필드 제거가 포함됩니다. 제거 전 안전 근거를 명시합니다.

**제거 1 — Game.unity NetworkGameManager GameObject**
- **근거**: 4개 Bootstrap partial 파일 전체 grep 결과 `_networkGameManager` 사용처 없음. 아무 코드도 이 인스턴스를 활성 경로에서 참조하지 않음.
- **방식**: Inspector에서 직접 오브젝트 삭제 (에디터 스크립트 불필요)

**제거 2 — GameBootstrapper._networkGameManager 필드**
- **근거**: 동일. 선언만 있고 사용처 0건.
- **방식**: `GameBootstrapper.cs` 코드 편집

---

## 수정 대상

### 작업 1 — GameBootstrapper.cs 필드 제거 (코드)

**파일**: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

**제거할 코드** (110~112번째 줄):
```csharp
[Header("Network")]
[Tooltip("네트워크 게임 세션 관리 컴포넌트 (씬에 NetworkGameManager GameObject 배치 후 연결)")]
[SerializeField] private Hexiege.Infrastructure.NetworkGameManager _networkGameManager;
```

> `[Header("Network")]`는 아래에 다른 네트워크 SerializeField들이 이어지므로 헤더 자체는 유지해야 합니다.
> 해당 `[Header]`, `[Tooltip]`, `[SerializeField]` 3줄만 제거합니다.

---

### 작업 2 — Game.unity에서 NetworkGameManager GameObject 제거 (Inspector)

**방법**: Unity Editor에서 Game.unity 씬을 열고 Hierarchy에서 `NetworkGameManager` GameObject를 선택 후 삭제.

**주의사항**:
- 삭제 전 Game.unity의 다른 컴포넌트(GameBootstrapper 등) Inspector를 열어 Missing Reference가 생기지 않는지 확인
- `GameBootstrapper._networkGameManager` 필드가 먼저 코드에서 제거된 상태라면 Inspector 슬롯 자체가 사라지므로 자동으로 정리됨
- **순서 중요**: 코드 수정(작업 1) → 컴파일 완료 → 씬 오브젝트 삭제(작업 2)

---

## GameSystemRules 관련

이 작업은 코드/씬 구조 정리에 해당하며 GameSystemRules(UI/Units/Buildings/AI) 적용 범위 밖입니다.

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| Inspector 연결 끊김 | `_networkGameManager` 필드 제거 시 Game.unity에서 해당 슬롯 연결이 이미 있는 경우 자동으로 null 처리됨. 필드 자체가 없어지므로 경고 없음. |
| 싱글플레이 직접 실행 | Game.unity에서 직접 실행해도 NGM이 없어도 정상 동작 (`IsNetworkMode() = false` 분기 → NGM 불필요) |
| 멀티플레이 정상 흐름 | Lobby에서 생성된 NGM이 DontDestroyOnLoad로 유지 → Game씬에서도 동일 인스턴스 사용 |

---

## 검증 방법

1. 코드 수정 후 컴파일 오류 없음 확인
2. 플레이모드 실행 → "DontDestroyOnLoad only works for root GameObjects" 경고 없음
3. 싱글플레이 게임 정상 진행 확인
4. (선택) 멀티플레이 Host → Client 연결 후 Game씬 전환 정상 확인

---

## 작업 순서

- [ ] 1. `GameBootstrapper.cs` 110~112번째 줄 제거 (코드 수정)
- [ ] 2. Unity 컴파일 완료 확인
- [ ] 3. Game.unity 씬에서 `NetworkGameManager` GameObject 삭제 (Inspector)
- [ ] 4. Game.unity 씬 저장
- [ ] 5. 플레이모드로 경고 없음 + 싱글플레이 정상 동작 확인
