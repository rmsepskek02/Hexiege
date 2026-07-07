# Research — 사운드 시스템 버그 수정

## 작업 목적 (자연어 설명)

사운드 시스템 실기 테스트에서 발견된 3가지 버그를 수정한다.
에디터 스크립트로 자동 생성된 볼륨 조절 UI가 프로젝트 UI 규칙을 어기고 있고,
BGM 전환 시 이전 소리가 겹치는 문제와 SFX 볼륨이 슬라이더로 조절되지 않는 문제가 확인되었다.

---

## 버그 목록

### BUG-1 — BGM 화면 전환 시 소리 겹침

**증상:** 씬 전환 시 이전 BGM과 새 BGM이 동시에 재생됨

**원인:** `StartCrossfade()` 내에서 크로스페이드 도중 새 전환이 요청되면 `StopCoroutine(_crossfadeRoutine)`으로 코루틴만 중단하지만, 진행 중이던 `fadeOut` AudioSource의 `Stop()`이 호출되지 않아 이전 BGM이 계속 재생된다.

**관련 파일:** `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs`
- `StartCrossfade()` (line 360~373)
- `CrossfadeRoutine()` (line 380~438)

**수정 방향:** `StopCoroutine` 호출 직후, `_bgmSourceA`와 `_bgmSourceB` 중 현재 페이드아웃 중인 채널(active가 아닌 채널)을 즉시 `Stop()`하여 이전 BGM을 강제 중단한다.

---

### BUG-2 — 볼륨 조절 UI 규칙 위반

**증상:** 볼륨 슬라이더 UI가 깨져 보임 (레이아웃 이상, 폰트 이상)

**원인 A — 규칙 2 위반 (고정 픽셀값):**
`SetupInGameVolumePanel.cs`와 `SetupLobbySettingsTab.cs`의 `CreateSlider()` 내부에서 슬라이더 서브 요소(FillArea, HandleSlideArea, Handle)에 `offsetMin`, `offsetMax`, `sizeDelta`를 고정 픽셀값으로 설정한다.

```csharp
fillAreaRt.offsetMin = new Vector2(5f, 0f);    // 위반
fillAreaRt.offsetMax = new Vector2(-15f, 0f);   // 위반
handleAreaRt.offsetMin = new Vector2(10f, 0f);  // 위반
handleAreaRt.offsetMax = new Vector2(-10f, 0f); // 위반
handleRt.sizeDelta = new Vector2(20f, 0f);      // 위반
```

**원인 B — 규칙 6 위반 (폰트 미설정):**
두 스크립트 모두 TextMeshProUGUI 생성 시 Maplestory SDF 폰트를 지정하지 않아 Unity 기본 폰트가 적용된다.

**관련 파일:**
- `Assets/Editor/Setup/SetupInGameVolumePanel.cs` — `CreateSlider()`, `CreateSliderRow()`
- `Assets/Editor/Setup/SetupLobbySettingsTab.cs` — `CreateSlider()`, `CreateSliderRow()`, `CreateMenuButton()`

**수정 방향:**
- 슬라이더 서브 요소는 앵커 비율 기반(anchorMin/anchorMax)으로 변경
- 모든 TextMeshProUGUI에 `Maplestory Light SDF` 폰트 에셋 명시 적용
- 단, LayoutElement의 `preferredWidth/Height`는 LayoutGroup 내 상대 배분 용도이므로 위반 대상에서 제외

---

### BUG-3 — SFX 볼륨 슬라이더 조절 안 됨

**증상:** 볼륨 슬라이더를 움직여도 SFX 소리 크기가 변하지 않음

**원인 (추정):** AudioMixer의 SFX 채널 Exposed Parameter 이름이 코드에서 사용하는 `"SFXVolume"`과 불일치할 가능성이 높다. Unity에서 파라미터를 Expose할 때 이름을 잘못 입력하면 `_audioMixer.SetFloat("SFXVolume", dB)`가 false를 반환하며 조용히 실패한다.

**확인 필요:**
Unity에서 `GameAudioMixer` 열기 → Exposed Parameters 탭에서 이름 확인
- `MasterVolume` ✓/✗
- `BGMVolume` ✓/✗
- `SFXVolume` ✓/✗

**관련 파일:**
- `Assets/_Project/Audio/GameAudioMixer.mixer` (Inspector 확인 필요)
- `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs` — `ApplyVolume()` (line 623~629)

**수정 방향:**
- AudioMixer Exposed Parameter 이름이 불일치하면 이름 수정
- 코드 측 상수(`ParamSfxVolume = "SFXVolume"`)는 정상이므로 믹서 쪽을 맞춘다

---

## 영향 범위

| 파일 | 변경 유형 |
|------|---------|
| `AudioManager.cs` | BUG-1 수정 |
| `SetupInGameVolumePanel.cs` | BUG-2 수정 |
| `SetupLobbySettingsTab.cs` | BUG-2 수정 |
| `GameAudioMixer.mixer` | BUG-3 확인 후 수정 (Inspector) |

---

## 참조 규칙

- GameSystemRules_UI 규칙 2 — 앵커 기반 배치, 고정 픽셀값 금지
- GameSystemRules_UI 규칙 6 — 폰트: Maplestory Light SDF / Bold SDF
- GameSystemRules_Sound 규칙 8 — BGM 크로스페이드: 새 전환 시 기존 코루틴 StopCoroutine 후 재시작
- GameSystemRules_Sound 규칙 17 — AudioMixerGroup 연결 필수
- GameSystemRules_Sound 규칙 18 — Exposed Parameter 이름 정확히 일치해야 함
