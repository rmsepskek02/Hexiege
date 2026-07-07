# Plan — 사운드 시스템 버그 수정

## 작업 목적 (자연어 설명)

실기 테스트에서 발견된 3가지 버그를 수정한다.
BGM 겹침 버그는 AudioManager 크로스페이드 로직 수정으로 해결하고,
볼륨 UI 깨짐은 에디터 스크립트의 폰트/픽셀값 위반 수정 후 씬 재실행으로 반영하며,
SFX 볼륨 미작동은 AudioMixer Exposed Parameter 이름 확인 후 수정한다.

> ⚠️ 에디터 스크립트(BUG-2) 수정 후에는 반드시 Unity에서 메뉴를 다시 실행해야 씬에 반영된다.

---

## BUG-1 수정 — AudioManager.cs 크로스페이드 로직

**파일:** `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs`

**변경 내용:** `StartCrossfade()` 내에서 기존 코루틴을 중단할 때, 페이드아웃 중이던 채널(active가 아닌 쪽)을 즉시 `Stop()`하여 이전 BGM이 계속 재생되는 문제 해결.

```csharp
// 현재 코드
if (_crossfadeRoutine != null)
    StopCoroutine(_crossfadeRoutine);

// 수정 후
if (_crossfadeRoutine != null)
{
    StopCoroutine(_crossfadeRoutine);
    // 페이드아웃 중이던 채널(active가 아닌 채널)을 즉시 정리
    AudioSource staleSource = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
    if (staleSource != null)
    {
        staleSource.volume = 0f;
        staleSource.Stop();
        staleSource.clip = null;
    }
    _crossfadeRoutine = null;
}
```

---

## BUG-2 수정 — 에디터 스크립트 UI 규칙 위반

### 공통 수정: CreateSlider() 함수

두 파일 모두 `CreateSlider()` 내부의 고정 픽셀값을 앵커 비율로 교체한다.

**`SetupInGameVolumePanel.cs` / `SetupLobbySettingsTab.cs` 동일 적용:**

```csharp
// FillArea — 고정 offset 제거, 앵커 비율로 대체
fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
fillAreaRt.offsetMin = Vector2.zero;  // 픽셀 오프셋 0으로
fillAreaRt.offsetMax = Vector2.zero;

// Fill — anchorMax.x=1로 전체 채움
fillRt.anchorMin = Vector2.zero;
fillRt.anchorMax = Vector2.one;
fillRt.sizeDelta = Vector2.zero;

// HandleSlideArea — 고정 offset 제거
handleAreaRt.anchorMin = Vector2.zero;
handleAreaRt.anchorMax = Vector2.one;
handleAreaRt.offsetMin = Vector2.zero;
handleAreaRt.offsetMax = Vector2.zero;

// Handle — anchorMax.x=0 유지, sizeDelta 앵커 비율로
handleRt.anchorMin = new Vector2(0f, 0f);
handleRt.anchorMax = new Vector2(0f, 1f);
handleRt.sizeDelta = new Vector2(0f, 0f);  // 비율로만 크기 결정
```

### 공통 수정: 폰트 설정 (규칙 6)

모든 TextMeshProUGUI 생성 후 Maplestory Light SDF 폰트를 명시 적용한다.

```csharp
// 각 스크립트 상단에 폰트 경로 상수 추가
private const string FontPath = "Assets/_Project/Fonts/Maplestory Light SDF.asset";

// TextMeshProUGUI 생성 직후 적용
var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
if (font != null) tmp.font = font;
```

적용 대상 (두 파일 공통):
- `CreateSliderRow()` 내 Label TMP, ValueText TMP
- `SetupLobbySettingsTab.cs`의 `CreateMenuButton()` 내 Label TMP
- BackButton Label TMP (`SetupInGameVolumePanel.cs`)

### 에디터 스크립트 재실행 필요

수정 후 Unity에서:
1. `Hexiege/Setup/사운드 - 인게임 볼륨 패널 생성` 재실행 (Game.unity)
2. `Hexiege/Setup/사운드 - 로비 설정 탭 구성` 재실행 (Lobby.unity)

---

## BUG-3 수정 — SFX 볼륨 Exposed Parameter 확인

**사전 확인 (사용자):**
Unity에서 `GameAudioMixer` 열기 → 우측 Exposed Parameters 탭 확인
- `MasterVolume`, `BGMVolume`, `SFXVolume` 세 이름이 정확히 일치하는지 확인

**이름 불일치 시:**
AudioMixer Inspector에서 파라미터 이름을 코드 상수와 동일하게 수정
- `ParamMasterVolume = "MasterVolume"`
- `ParamBgmVolume = "BGMVolume"`
- `ParamSfxVolume = "SFXVolume"`

코드 수정은 불필요 (상수값이 이미 정확함).

---

## 작업 순서

1. **BUG-3 사전 확인** — AudioMixer Exposed Parameter 이름 사용자가 직접 확인
2. **BUG-1** — `AudioManager.cs` `StartCrossfade()` 수정 (game-programmer 에이전트)
3. **BUG-2** — 에디터 스크립트 2개 수정 (game-programmer 에이전트)
4. **에디터 스크립트 재실행** — Unity에서 메뉴 2개 실행 (사용자)
5. **실기 테스트** — 3가지 버그 재확인

---

## 위험 요소

- BUG-2 수정 후 에디터 스크립트 재실행 시 기존 씬 구조를 덮어씀 → `GetOrCreate` 패턴으로 기존 요소 재사용하므로 데이터 손실 없음
- Handle sizeDelta를 Vector2.zero로 하면 핸들이 너무 작아 보일 수 있음 → 실기 확인 후 anchorMax.x 비율 조정
- BUG-3가 Exposed Parameter 이름 문제가 아닌 경우 추가 조사 필요
