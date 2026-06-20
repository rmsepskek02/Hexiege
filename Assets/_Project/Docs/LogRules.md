# 로그 규칙

이 문서는 Hexiege 프로젝트에서 사용하는 두 종류의 로그 파일 규칙을 정의합니다.

| 종류 | 파일명 | 목적 | 작성 주체 |
|------|--------|------|----------|
| 런타임 로그 | `RuntimeLog_host.txt` / `RuntimeLog_client.txt` | 게임 실행 중 디버깅 | game-programmer 에이전트 |
| QA-Fix 로그 | `Log.md` | QA ↔ DEV 버그 수정 이터레이션 기록 | qa-tester / game-programmer 에이전트 |

---

# 1. 런타임 로그

게임 실행 중 발생하는 동작을 파일로 기록하는 디버그 로그입니다.
콘솔 로그(Debug.Log 등)가 아닌, 파일로 저장되는 로그에 적용됩니다.

---

## 목적

- **특정 작업 디버깅 전용** — 버그 재현 및 원인 파악이 필요한 작업에서만 사용
- Claude(QA 에이전트 포함)가 로그 파일을 읽고 버그 흐름을 추적하기 위한 용도

---

## 파일 규칙

### 저장 위치
```
Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/
```

### 파일명
| 파일 | 설명 |
|------|------|
| `RuntimeLog_host.txt` | Host(서버) 측 로그 |
| `RuntimeLog_client.txt` | Client 측 로그 |

### 생성 및 제거
- 디버깅이 필요한 작업에서만 로그 출력 코드를 추가
- **로그 파일 자체는 영구 보존** — `_Logs/` 폴더에 작업 이력으로 남김
- **로그를 출력하는 코드**는 작업 완료 후 반드시 제거

---

## 형식

### 헤더
파일 최상단에 아래 두 줄을 작성한다.

```
=== [작업명 또는 로그 목적] ===
=== 세션 시작: YYYY-MM-DD HH:MM:SS ===
```

예시:
```
=== 유닛 사망 NGO 버그 픽스 검증 로그 ===
=== 세션 시작: 2026-06-08 10:52:40 ===
```

### 로그 라인
```
[HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value
```

예시:
```
[10:53:23.124] [INFO] [Network/NetworkUnit] OnNetworkDespawn | IsServer=False, UnitType=Pistoleer
[10:53:23.125] [WARN] [Network/NetworkUnit] 예상치 못한 상태 감지 | IsServer=False, Reason=AlreadyDespawned
[10:53:23.126] [ERROR] [Network/NetworkUnit] Despawn 실패 | IsServer=False, Reason=ObjectNotFound
```

---

## 로그 레벨

| 레벨 | 사용 시점 |
|------|---------|
| `[INFO]` | 정상 흐름 — 함수 진입, 완료, 상태 확인 |
| `[WARN]` | 예상 밖이지만 동작은 계속됨 (예: null이지만 기본값으로 대체 가능한 경우) |
| `[ERROR]` | 로직 오류, 반드시 원인 파악이 필요한 상황 |

---

## 카테고리 규칙

`[System/Class]` 형식으로 작성한다.

- **System**: 어느 시스템 영역인지 (예: `Network`, `Combat`, `UI`, `HexGrid`)
- **Class**: 정확히 어느 클래스에서 출력하는지 (예: `NetworkUnit`, `UnitView`, `ProductionPanelUI`)

예시:
```
[Network/NetworkUnit]
[Combat/UnitView]
[UI/ProductionPanelUI]
[HexGrid/HexTileView]
```

---

## 실기기 테스트

에디터에서는 파일로 직접 저장하지만, 실기기에서는 파일 저장이 불가능하므로 Logcat을 사용한다.

| 환경 | 로그 출력 방식 |
|------|-------------|
| 에디터 | `_Logs/` 폴더에 파일로 저장 (`RuntimeLog_host.txt` / `RuntimeLog_client.txt`) |
| 실기기 | Logcat에 출력 → 사용자가 복사해서 Claude 채팅창에 직접 공유 |

- Logcat 출력 형식은 에디터 파일 형식과 동일하게 맞춘다
  ```
  [HH:MM:SS.ms] [LEVEL] [System/Class] 메시지 | key=value, key=value
  ```

---

## 금지 사항

1. **`Debug.Log` 등 Unity 콘솔 로그를 RuntimeLog 대신 사용 금지** — 콘솔 로그는 Claude가 읽을 수 없음
2. **의미 없는 메시지 작성 금지** — 예: `[INFO] [Network/NetworkUnit] 여기`
3. **민감한 데이터 출력 금지** — 사용자 ID, 인증 토큰 등
4. **레벨 표기 생략 금지** — 모든 로그 라인에 `[INFO]` / `[WARN]` / `[ERROR]` 중 하나를 반드시 명시

---

# 2. QA-Fix 로그 (Log.md)

QA 에이전트가 버그를 발견했을 때 생성하며, QA ↔ DEV 버그 수정 과정을 Round 단위로 기록합니다.
에이전트(qa-tester, game-programmer) 전용 문서입니다.

## 폴더 구조

```
Assets/_Project/Docs/_Logs/
└── YYYY-MM-DD/
  └── HH_MM_[작업명]/                ← _Tasks와 동일 경로명 (대응 관계)
    ├── RuntimeLog_host.txt           ← 런타임 로그 (Host)
    ├── RuntimeLog_client.txt         ← 런타임 로그 (Client)
    └── Log.md                        ← QA ↔ DEV 이터레이션 이력 (에이전트 전용)
```

예시:
```
Assets/_Project/Docs/_Logs/
└── 2026-03-05/
  └── 18_05_network-input-fix/
    ├── RuntimeLog_host.txt
    ├── RuntimeLog_client.txt
    └── Log.md
```

## 위치 및 생성 시점
- 경로: `Assets/_Project/Docs/_Logs/YYYY-MM-DD/HH_MM_[작업명]/Log.md`
- **_Tasks와 동일한 날짜/시간/작업명 폴더**를 사용 (대응 관계 유지)
- QA 에이전트가 FAIL을 처음 발견한 시점에 생성
- 모든 TC가 PASS이면 생성 불필요

## 작성 주체
| 섹션 | 작성 주체 |
|------|----------|
| `[QA]` | qa-tester 에이전트 |
| `[DEV]` | game-programmer 에이전트 |

## 형식

```markdown
# QA-Fix 반복 로그 — [작업명]

## Round 1 — YYYY-MM-DD HH:MM

### [QA] 발견된 문제
- BUG-001: [TC-ID] — [증상 설명]
  - 관련 파일: [파일 경로:라인]
  - 원인 분석: [분석 내용]
- BUG-002: [TC-ID] — [증상 설명]
  - ...

### [DEV] 수정 내용
- BUG-001: [수정 파일:라인] — [수정 내용]
- BUG-002: [수정 파일:라인] — [수정 내용]

## Round 2 — YYYY-MM-DD HH:MM

### [QA] 발견된 문제
...
```

## 운영 규칙
- DEV 수정 완료 후 반드시 `[DEV]` 섹션 채운 뒤 QA에게 재요청
- Round는 순서대로 append — 이전 Round 내용 수정 금지
- 최종 전체 PASS 시 Log.md에 별도 종료 표기 불필요 (Testcase.md 최종 판정으로 대체)
