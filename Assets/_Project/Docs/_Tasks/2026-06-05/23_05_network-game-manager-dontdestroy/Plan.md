# Plan — NetworkGameManager DontDestroyOnLoad 경고 제거

> ⚠️ **이 Plan은 추가 조사로 접근 방식이 변경되어 실행되지 않았습니다.**
> 실제 수행된 작업: `Assets/_Project/Docs/_Tasks/2026-06-06/00_08_networkgamemanager-cleanup/`

## 작업 목적 (자연어 설명)

플레이모드 실행 시 출력되는 "DontDestroyOnLoad only works for root GameObjects" 경고를 없앱니다.
`NetworkGameManager` GameObject가 Lobby 씬에서 다른 오브젝트의 자식으로 배치되어 있어 발생하는 문제입니다.
코드 한 줄 추가로 런타임에 루트로 분리하여 해결합니다.

---

## 수정 방법 선택

| 방법 | 접근 | 선택 이유 |
|------|------|-----------|
| A. 씬 수정 | Hierarchy에서 드래그 | 씬 파일 저장 필요. 어떤 오브젝트 자식인지 먼저 확인 필요 |
| **B. 코드 수정 (채택)** | `transform.SetParent(null)` 추가 | 즉시 적용. 씬 저장 불필요. 코드가 의도를 명시적으로 표현 |

코드 수정(방법 B)을 채택합니다.
`NetworkGameManager`는 "씬 전환 후에도 살아남아야 하는 싱글턴" 개념이므로,
코드 내에서 스스로 루트로 분리하는 것이 더 안전하고 명시적입니다.

---

## 수정 대상

| 파일 | 위치 |
|------|------|
| `Assets/_Project/Scripts/Infrastructure/Network/NetworkGameManager.cs` | `Awake()` 내부 102번째 줄 |

---

## 변경 내용

### 수정 전

```csharp
private void Awake()
{
    // 씬 전환 시에도 NetworkGameManager 유지
    DontDestroyOnLoad(gameObject);
    ...
}
```

### 수정 후

```csharp
private void Awake()
{
    // DontDestroyOnLoad는 루트 오브젝트에만 동작하므로
    // 부모가 있을 경우 먼저 루트로 분리한 뒤 호출한다.
    transform.SetParent(null);
    DontDestroyOnLoad(gameObject);
    ...
}
```

---

## 위험 요소

| 항목 | 내용 |
|------|------|
| 씬 계층 구조 변화 | `SetParent(null)` 호출로 런타임에 부모에서 분리됨. Inspector 배치와 실제 동작이 달라짐 |
| 의도치 않은 중복 생성 | 현재 중복 방지 로직 없음 — 이미 DontDestroyOnLoad로 유지된 상태에서 씬 재진입 시 중복 인스턴스 가능성 검토 필요 |

> 중복 방지 로직 추가는 이 작업 범위에 포함하지 않음. 현재 코드에 이미 DontDestroyOnLoad를 호출하고 있으므로 기존 동작 방식과 동일.

---

## GameSystemRules 관련

이 작업은 씬 구조/네트워크 경고 제거에 해당하며 GameSystemRules 파일의 적용 범위 밖입니다.

---

## 검증 방법

1. 코드 수정 후 플레이모드 실행
2. 콘솔에 "DontDestroyOnLoad only works for root GameObjects" 경고가 사라지면 완료
3. 멀티플레이 씬 전환(Lobby → Game) 시 NetworkGameManager가 정상 유지되는지 확인

---

## 작업 순서

- [ ] 1. `NetworkGameManager.cs` Awake에 `transform.SetParent(null);` 추가
- [ ] 2. 플레이모드 실행 → 경고 사라짐 확인
- [ ] 3. (선택) 멀티플레이 씬 전환 테스트
