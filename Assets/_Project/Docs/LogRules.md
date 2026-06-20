# 런타임 로그 규칙

이 문서는 Hexiege 프로젝트에서 런타임 로그 파일을 작성할 때 따르는 규칙을 정의합니다.
콘솔 로그(Debug.Log 등)가 아닌, 파일로 저장되는 런타임 로그에 적용됩니다.

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

## 금지 사항

1. **`Debug.Log` 등 Unity 콘솔 로그를 RuntimeLog 대신 사용 금지** — 콘솔 로그는 Claude가 읽을 수 없음
2. **의미 없는 메시지 작성 금지** — 예: `[INFO] [Network/NetworkUnit] 여기`
3. **민감한 데이터 출력 금지** — 사용자 ID, 인증 토큰 등
4. **레벨 표기 생략 금지** — 모든 로그 라인에 `[INFO]` / `[WARN]` / `[ERROR]` 중 하나를 반드시 명시
