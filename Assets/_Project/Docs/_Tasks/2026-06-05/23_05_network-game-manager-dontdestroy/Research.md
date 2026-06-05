# Research — NetworkGameManager DontDestroyOnLoad 경고 제거

## 작업 목적 (자연어 설명)

플레이모드 실행 시 Unity 콘솔에 다음 경고가 출력됩니다:
> "DontDestroyOnLoad only works for root GameObjects or components on root GameObjects."

이 경고는 `NetworkGameManager`가 씬 계층에서 다른 오브젝트의 **자식**으로 배치되어 있어서 발생합니다.
`DontDestroyOnLoad`는 씬 최상위(루트) 오브젝트에만 동작하므로, 자식 오브젝트에 호출하면 경고가 뜨고 씬 전환 시 유지도 보장되지 않습니다.

---

## 원인 분석

### 발생 위치

- 파일: [NetworkGameManager.cs:102](Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs#L102)
- 코드: `DontDestroyOnLoad(gameObject);` (Awake 내부)

### Unity 동작 규칙

Unity의 `DontDestroyOnLoad`는 **루트 오브젝트(부모가 없는 오브젝트)** 에만 적용됩니다.
자식 오브젝트에 호출하면:
1. 경고 메시지 출력
2. 씬 전환 시 해당 오브젝트가 유지되지 않을 수 있음 → 멀티플레이 흐름 오작동 가능성

### 원인

Lobby.unity 씬의 Hierarchy에서 `NetworkGameManager` GameObject가 다른 오브젝트 하위에 자식으로 배치되어 있음.
씬 파일을 텍스트로 직접 확인하지 않았으므로 어떤 오브젝트의 자식인지는 Unity Editor에서 확인 필요.

---

## 해결 방법 (2가지)

### 방법 A — 씬 계층 수정 (권장)
Lobby.unity 씬 Hierarchy에서 `NetworkGameManager` GameObject를 루트로 이동.
- Unity Editor에서 드래그 또는 `Transform.SetParent(null)` 에디터 스크립트로 처리
- 코드 변경 없음
- 씬 구조가 의도대로 정리됨

### 방법 B — 코드 수정 (보조)
`NetworkGameManager.cs` Awake에서 DontDestroyOnLoad 호출 전 `transform.SetParent(null)` 추가:
```csharp
transform.SetParent(null); // 부모에서 분리하여 루트로 이동
DontDestroyOnLoad(gameObject);
```
- Inspector에서 부모-자식 구조 변경 없이 런타임에 루트로 분리
- 씬 저장 없이 코드만으로 해결 가능
- 단, 씬 구조 자체의 의도적 배치와 맞지 않을 수 있음

---

## 현재 상태 요약

| 항목 | 내용 |
|------|------|
| 경고 발생 위치 | `NetworkGameManager.cs:102` — `DontDestroyOnLoad(gameObject)` |
| 근본 원인 | NetworkGameManager가 씬 Hierarchy에서 루트가 아닌 자식으로 배치됨 |
| 게임 동작 영향 | DontDestroyOnLoad 미동작 가능 → 씬 전환 시 NetworkGameManager 소멸 위험 |
| 권장 수정 방법 | 씬 Hierarchy에서 루트로 이동 (방법 A) |
