# Research — OIDC Bridge & UGS 데이터 플랫폼 설계

## 이 작업이 무엇인가

현재 Hexiege는 Firebase로 로그인한 뒤 UGS(Unity Gaming Services)와의 연결이 끊겨 있는 상태다.
Firebase 계정과 UGS PlayerId가 서로 연결되지 않아서, 게임 내 기록·랭킹·재화 등 계정에 귀속되어야 하는
모든 데이터를 어디에 저장할지 기반 자체가 없다.

이 작업은 두 가지를 해결한다:

1. **OIDC Bridge 구현**: Firebase 로그인 후 Firebase가 발급한 JWT 토큰을 UGS에 전달하여
   Firebase 계정과 UGS PlayerId를 1:1로 연결한다. 기기가 바뀌어도 동일한 Firebase 계정이면
   동일한 UGS PlayerId를 받게 된다.

2. **데이터 플랫폼 확정**: 계정에 귀속될 데이터(랭킹, 재화, 인벤토리, 매치 기록 등)를
   어디에 저장하고 어떻게 구성할지 설계한다. 실시간 동기화가 불필요하다고 확인되었으므로
   UGS Cloud Save / Leaderboard / Economy를 기반으로 한다.

---

## 1. 현재 상태 분석

### 1-1. 브릿지 미구현 (핵심 문제)

**`LoginUseCase.cs` lines 362–368**
```csharp
// TODO: 이 UGS Authentication SDK 버전은 SignInWithCustomIdAsync 를 지원하지 않는다.
//   현재는 익명 로그인으로 UGS PlayerId 를 발급받는 임시 처리.
if (!AuthenticationService.Instance.IsSignedIn)
{
    await AuthenticationService.Instance.SignInAnonymouslyAsync();
}
```

- Firebase 로그인 후 UGS에는 **익명**으로 연결 → PlayerId가 계정과 무관한 임시 ID
- 기기가 바뀌면 PlayerId도 바뀜 → 계정 귀속 데이터 불가

### 1-2. UnityServicesInitializer의 세션 덮어쓰기 문제

**`UnityServicesInitializer.cs` lines 87–93**
```csharp
if (AuthenticationService.Instance.IsSignedIn)
{
    AuthenticationService.Instance.SignOut();
}
await AuthenticationService.Instance.SignInAnonymouslyAsync();
```

- 게임 씬(Game.unity)에서 `NetworkGameManager`가 이 초기화를 호출할 때
  Login 씬에서 발급된 OIDC 세션을 **무조건 로그아웃 후 익명 로그인으로 덮어씀**
- 코드 내 주석: "※ 추후 Login 씬 흐름 완성 시 재검토 필요"
- OIDC Bridge 구현 후 반드시 수정해야 하는 지점

### 1-3. AuthSystemRules.md의 설계 불일치

**`AuthSystemRules.md` line 50–59** 에는 여전히 구 설계가 기록되어 있다:
```
Firebase 로그인 → Firebase UID 발급
                       ↓
UGS SignInWithCustomIdAsync(Firebase UID) → UGS PlayerId 발급
```
- `SignInWithCustomIdAsync`는 UGS Auth SDK v3.x에서 **삭제된 API**
- 규칙 문서가 현재 SDK와 불일치 → 새 설계로 업데이트 필요

### 1-4. 랭킹/데이터 시스템 미구현 상태

- `RankingView.cs`: `/* 추후 구현 예정 */` 플레이스홀더만 존재
- 데이터 저장 레이어 없음 — Cloud Save, Leaderboard, Economy 미연동

---

## 2. 기술 조사 결과

### 2-1. OIDC Bridge 방법

**`SignInWithOpenIdConnectAsync` — UGS Auth SDK v2.2.0+에서 공식 지원**

현재 설치 버전: `com.unity.services.multiplayer: 2.0.0` → UGS Auth SDK v3.6.0 포함
→ 조건 충족 ✅

```csharp
// Firebase ID Token을 UGS에 전달 (OIDC 표준 방식)
await AuthenticationService.Instance.SignInWithOpenIdConnectAsync(
    "oidc-firebase",    // UGS Dashboard에 등록한 Provider 이름 (oidc- 접두사 필수)
    firebaseIdToken     // Firebase.Auth.FirebaseUser.TokenAsync(false) 로 발급
);
```

**Firebase OIDC Provider 설정값**
| 항목 | 값 |
|------|-----|
| Provider 이름 | `oidc-firebase` (oidc- 접두사 필수) |
| Issuer URL | `https://securetoken.google.com/<firebase-project-id>` |
| Client ID | Firebase 프로젝트 ID |

**PlayerId 일관성 보장**
- 공식 문서: "같은 OIDC 자격증명으로 로그인하면 항상 동일한 UGS PlayerId 반환"
- 기기 변경 후에도 동일 Firebase 계정 → 동일 PlayerId ✅

### 2-2. UGS 데이터 서비스 현황

**실시간 리스너 필요 여부: 없음 (사용자 확인 완료)**
→ UGS 폴링 방식으로 충분

| 서비스 | 역할 | 비고 |
|--------|------|------|
| UGS Cloud Save | 계정 귀속 데이터 저장 (JSON Key-Value) | PlayerId 기준 저장 |
| UGS Leaderboard | 랭킹 조회/제출 | 조회 시점 스냅샷 |
| UGS Economy | 재화·인벤토리 관리 | 빌트인 검증 |

### 2-3. 매칭 시스템 — Firebase 무관 확인

`MatchmakerManager.cs` 검토 결과:
- UGS Matchmaker + Lobby + Relay 전용 구현
- Firebase 의존 없음 → 브릿지 구현 후에도 **변경 불필요**

---

## 3. 아키텍처 결정 사항

### 결정: OIDC Bridge + UGS Services (Option B 채택)

| 구분 | 결정 |
|------|------|
| 인증 | Firebase Auth (기존 유지) |
| Firebase-UGS 연결 | `SignInWithOpenIdConnectAsync` OIDC Bridge |
| 계정 귀속 데이터 | UGS Cloud Save |
| 랭킹 | UGS Leaderboard (조회/게임종료 시 갱신) |
| 재화·인벤토리 | UGS Economy |
| Firestore 직접 구현 | 불필요 |

**이 결정을 내린 이유**
- 실시간 리스너 불필요 → Firestore 핵심 장점 소멸
- 랭킹·재화·인벤토리 등 계정 귀속 기능이 많이 예정됨 → UGS 빌트인 서비스로 구현 비용 절감
- 매칭 시스템이 이미 UGS 기반 → 데이터도 UGS로 통일하면 SDK/대시보드 단일화

---

## 4. 영향 범위

### 수정 필요 파일

| 파일 | 위치 | 변경 내용 |
|------|------|-----------|
| `LoginUseCase.cs` | Application/UseCases | `SignInAnonymouslyAsync` → `SignInWithOpenIdConnectAsync` |
| `UnityServicesInitializer.cs` | Infrastructure/Network | 게임 씬 재호출 시 OIDC 세션 보존 로직으로 교체 |
| `AuthSystemRules.md` | Docs | 구 설계(`SignInWithCustomIdAsync`) → OIDC Bridge 설계로 업데이트 |

### 신규 파일 (추후 각 기능 구현 시 생성)

| 파일 | 내용 |
|------|------|
| `CloudSaveService.cs` | UGS Cloud Save 연동 (Infrastructure) |
| `LeaderboardService.cs` | UGS Leaderboard 연동 (Infrastructure) |
| `EconomyService.cs` | UGS Economy 연동 (Infrastructure) |

### 변경 없는 파일 (확인)

- `LobbyManager.cs` — UGS PlayerId만 사용, Firebase 무관 → **변경 없음**
- `RelayManager.cs` — Relay 서버 연결 전용 → **변경 없음**
- `MatchmakerManager.cs` — UGS Matchmaker 전용 → **변경 없음**
- `FirebaseAuthService.cs` — Firebase Auth 래퍼, 역할 유지 → **변경 없음**

### 외부 설정 필요 (코드 외)

| 항목 | 설명 |
|------|------|
| UGS Dashboard | OIDC Provider `oidc-firebase` 등록 (Issuer URL, Client ID 입력) |
| Firebase Console | `google-services.json` 프로젝트 ID 확인 |
