# 써드파티 설정 가이드: Firebase Auth + Google Play Games Plugin

로그인 시스템 구현에 앞서 외부 서비스 설정과 SDK 설치가 필요합니다. 이 문서는 Firebase Console 설정, Google Play Console 설정, Unity 프로젝트에 SDK를 설치하는 방법을 단계별로 안내합니다. 순서대로 진행하면 로그인 기능 구현을 시작할 수 있는 환경이 완성됩니다.

---

## 설정 순서 요약

```
[1] Firebase Console — 프로젝트 생성 및 Android 앱 등록
[2] Firebase Authentication — 로그인 방법 3가지 활성화
[3] Firebase Unity SDK — Unity에 설치
[4] Google Play Console — 게임 서비스 설정 (Google 로그인용)
[5] Google Play Games Plugin for Unity — 설치 및 설정
[6] Unity 프로젝트 최종 설정 확인
```

---

## [1] Firebase Console — 프로젝트 생성

### 프로젝트 생성
1. [Firebase Console](https://console.firebase.google.com/) 접속
2. **"프로젝트 추가"** 클릭
3. 프로젝트 이름 입력 (예: `Hexiege`)
4. Google Analytics 사용 여부 선택 후 **"프로젝트 만들기"** 클릭

### Android 앱 등록
1. Firebase Console 프로젝트 홈에서 **Android 아이콘( )** 클릭
2. **패키지 이름 입력**:
   - Unity 프로젝트의 패키지명과 반드시 동일해야 함
   - 확인 위치: `Edit > Project Settings > Player > Android > Other Settings > Package Name`
   - 예: `com.yourcompany.hexiege`
3. 앱 닉네임 입력 (선택 사항, 예: `Hexiege Android`)
4. **디버그 서명 인증서 SHA-1 입력** (Google 로그인 사용 시 필수):
   - Windows 명령 프롬프트에서 아래 명령 실행:
     ```
     keytool -list -v -keystore "%USERPROFILE%\.android\debug.keystore" -alias androiddebugkey -storepass android -keypass android
     ```
   - 출력 결과에서 `SHA1:` 값 복사 후 Firebase Console에 입력
5. **"앱 등록"** 클릭

### google-services.json 다운로드
1. **"google-services.json 다운로드"** 클릭
2. 다운로드된 파일을 아래 경로에 배치:
   - `Assets/google-services.json`
   - (Assets 폴더 바로 아래에 위치해야 Firebase SDK가 자동 인식)
3. 나머지 단계는 SDK가 처리하므로 **"다음 > 콘솔로 이동"** 클릭

---

## [2] Firebase Authentication — 로그인 방법 활성화

1. Firebase Console → 왼쪽 메뉴 **"Authentication"** 클릭
2. **"시작하기"** 클릭 (최초 1회)
3. **"Sign-in method"** 탭 선택

### 익명 로그인 활성화
1. 제공업체 목록에서 **"익명"** 클릭
2. **"사용 설정"** 토글 ON
3. **"저장"** 클릭

### 이메일/비밀번호 로그인 활성화
1. **"이메일/비밀번호"** 클릭
2. 첫 번째 토글 **"이메일/비밀번호"** ON
3. 두 번째 토글 **"이메일 링크(패스워드가 없는 로그인)"** 는 OFF 유지
4. **"저장"** 클릭

### Google 로그인 활성화
1. **"Google"** 클릭
2. **"사용 설정"** 토글 ON
3. 프로젝트 지원 이메일 선택 (본인 Firebase 계정 이메일)
4. **"저장"** 클릭
5. 저장 후 표시되는 **웹 클라이언트 ID** 메모 → Google Play Games Plugin 설정 시 사용

---

## [3] Firebase Unity SDK 설치

### 다운로드
1. [Firebase Unity SDK GitHub Releases](https://github.com/firebase/firebase-unity-sdk/releases) 접속
2. 최신 버전의 `firebase_unity_sdk_XX.X.X.zip` 다운로드
3. 압축 해제

### Unity에 임포트 (필요한 패키지만)
압축 해제 폴더에서 아래 파일만 임포트. 나머지는 사용하지 않으므로 임포트하지 않음.

> **참고**: 최신 Firebase Unity SDK(v12+)에서는 `FirebaseApp.unitypackage`가 별도로 존재하지 않음.
> 각 패키지에 Firebase 코어가 내장되어 있으므로 `FirebaseAuth.unitypackage` 하나만 임포트하면 됨.

| 파일 | 필요 여부 | 이유 |
|------|---------|------|
| `FirebaseAuth.unitypackage` | **필수** | Firebase 인증 기능 + 코어 내장 |

임포트 방법:
1. Unity Editor가 열린 상태에서 `FirebaseAuth.unitypackage` 더블클릭
2. Import 창에서 **"Import"** 클릭
3. 설치 완료 후 Unity 재시작 권장

### 설치 확인
- `Assets/Firebase/` 폴더가 생성되었는지 확인
- Unity Console에 빨간색 Firebase 관련 오류가 없는지 확인

### Android 빌드 추가 설정

**Gradle 템플릿 활성화** (External Dependency Manager가 Firebase Android 의존성을 자동 주입하기 위해 필요):
1. `Edit > Project Settings > Player > Android > Publishing Settings`
2. **"Custom Main Gradle Template"** 체크 → `Assets/Plugins/Android/mainTemplate.gradle` 자동 생성
3. **"Custom Gradle Properties Template"** 체크 → `Assets/Plugins/Android/gradleTemplate.properties` 자동 생성
   - Unity 6 + EDM4U 조합에서 Jetifier 활성화에 필수
4. `Assets > External Dependency Manager > Android Resolver > Resolve` 실행 → Firebase 의존성 자동 추가

> **Multidex 설정 불필요**: 이 프로젝트의 Minimum API Level은 **25 (Android 7.1)**로,
> API 21 이상에서는 Multidex가 OS에 내장되어 있으므로 `multiDexEnabled true` 추가 불필요.

---

## [4] Google Play Console — 게임 서비스 설정

> Google 로그인을 구현하지 않는 경우 이 단계를 건너뛰어도 됩니다.

### 앱 등록 (미등록 시)
1. [Google Play Console](https://play.google.com/console) 접속
2. **"앱 만들기"** 클릭
3. 앱 이름, 언어, 앱/게임 유형, 유/무료 선택 후 생성

### Play 게임 서비스 연결
1. Google Play Console → 해당 앱 선택
2. 왼쪽 메뉴 **"Play 게임 서비스"** → **"설정 및 관리"** → **"설정"**
3. **"Google API 프로젝트에 게임 연결"** 선택
4. 앞서 생성한 Firebase 프로젝트 선택 → **"연결"** 클릭

### OAuth 클라이언트 ID 확인
1. Google Play Console → Play 게임 서비스 → **"사용자 인증 정보"**
2. **Android 앱** 항목의 클라이언트 ID 확인
3. 또는 [Google Cloud Console](https://console.cloud.google.com/) → API 및 서비스 → 사용자 인증 정보에서도 확인 가능

---

## [5] Google Play Games Plugin for Unity 설치

### 다운로드 및 임포트
1. [Google Play Games Plugin GitHub Releases](https://github.com/playgameservices/play-games-plugin-for-unity/releases) 접속
2. 최신 버전의 `.unitypackage` 파일 다운로드
3. Unity Editor에서 더블클릭 → **"Import"** 클릭
4. 설치 완료 후 Unity 메뉴에 **"Google"** 항목이 추가됨 확인

### Unity에서 Google Play Games 설정
1. Unity Editor → **Window > Google Play Games > Setup > Android Setup**
2. **"Client ID"** 입력란에 Firebase Authentication에서 확인한 **웹 클라이언트 ID** 입력
   - 형식: `xxxxxxxxxxxx-xxxxxxxxxx.apps.googleusercontent.com`
   - Firebase Console → Authentication → Sign-in method → Google → 웹 클라이언트 ID 에서 확인
3. **"Setup"** 클릭 → 설정 완료 메시지 확인

### 설치 확인
- `Assets/GooglePlayGames/` 폴더 존재 확인
- Unity Console에 관련 오류 없음 확인

---

## [6] Unity 프로젝트 최종 설정 확인

### Build Settings — Login.unity 추가
1. `File > Build Settings`
2. **"Add Open Scenes"** 또는 드래그로 `Login.unity` 추가
3. **로그인 기능 테스트 시** Build Index 설정:
   - Login: 0
   - Lobby: 1
   - Game: 2
4. **게임 기능 테스트 시** Build Index 설정:
   - Lobby: 0
   - Login: 1
   - Game: 2

### Android Manifest 인터넷 권한 확인
Firebase Auth는 인터넷 접근 권한이 필요합니다. Firebase SDK 설치 시 자동으로 포함되는 경우가 많으나, 수동 확인 방법:
- `Assets/Plugins/Android/AndroidManifest.xml` 파일에 아래 권한 확인:
  ```xml
  <uses-permission android:name="android.permission.INTERNET" />
  ```

---

## 최종 확인 체크리스트

| 항목 | 확인 |
|------|------|
| `Assets/google-services.json` 파일 존재 | ☐ (미완료 — Firebase Console 설정 후 진행) |
| `Assets/Firebase/` 폴더 존재 | ✅ 완료 (2026-05-24, v13.11.0) |
| `Assets/GooglePlayGames/` 폴더 존재 | ✅ 완료 (2026-05-24, v2.1.0) |
| Custom Main Gradle Template 활성화 | ✅ 완료 (2026-05-24) |
| Custom Gradle Properties Template 활성화 (Jetifier) | ✅ 완료 (2026-05-24) |
| EDM4U Android Resolver 실행 완료 | ✅ 완료 (2026-05-24) |
| Unity Console — 컴파일 에러 없음 | ✅ 완료 (2026-05-24) |
| Firebase Console — 익명 로그인 활성화 | ☐ (미완료 — 추후 진행) |
| Firebase Console — 이메일/비밀번호 로그인 활성화 | ☐ (미완료 — 추후 진행) |
| Firebase Console — Google 로그인 활성화 | ☐ (미완료 — 추후 진행) |
| Google Play Games Plugin 웹 클라이언트 ID 설정 완료 | ☐ (미완료 — Firebase Console Google 로그인 활성화 이후) |
| Build Settings — Login.unity 등록 완료 | ☐ (미완료 — Login.unity 씬 생성 이후) |
| mainTemplate.gradle multiDexEnabled true 설정 | ✖ 불필요 (Min API Level = 25, API 21+ 내장 Multidex) |
