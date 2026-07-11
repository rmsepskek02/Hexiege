# Research — 사운드 시스템 Inspector 설정 자동화

## 개요 (자연어 설명)

이전 세션에서 사운드 시스템 코드(AudioManager, SoundConfig)를 완성했지만, Unity Inspector에서 수동으로 연결해야 하는 작업들이 남아있다. 이 Research는 각 씬의 현재 상태를 파악하여, 어떤 에디터 스크립트가 필요한지, 어떤 부분은 자동화가 불가능한지를 정리한다.

---

## 1. 씬별 현재 상태

### Login.unity
- **생성 경위**: `SetupLoginScene.cs` 에디터 스크립트로 생성됨 (2026-06-10)
- **현재 포함 내용**: Canvas, SafeAreaContainer, [Bootstrap](LoginBootstrapper), LoginRootView, 5개 패널, AnonymousWarningPopup, ConfirmPopup, EventSystem
- **누락**: `[Audio]` GameObject가 없음 → AudioManager 컴포넌트 미부착 상태
- **LoginBootstrapper._soundConfig**: SoundConfig 에셋이 아직 없으므로 null

### Game.unity
- **InGameSettingsUI 코드 상태**: `_volumePanelGroup`, `_masterSlider`, `_bgmSlider`, `_sfxSlider` 필드가 코드에 이미 추가됨. `SetupVolumeSliders()`, `OnSoundButtonClicked()`, `HideVolumePanel()` 모두 구현 완료
- **씬 상태**: 위 필드들이 null — 볼륨 패널 UI GameObject가 씬에 존재하지 않음
- **사운드 버튼**: `_soundButton`은 이미 InGameSettingsUI 팝업에 있음 (이전 작업에서 생성)

### Lobby.unity
- **현재 상태**: 볼륨 패널 UI 없음 — GameSystemRules_Sound 규칙 22(Lobby 볼륨 패널)는 이번 작업에서 처음 구현
- **Lobby 설정 버튼**: LobbyRootView에 Settings 버튼(`ui_icon_settings`)이 있으나 현재 별도 동작 없음 — 이 버튼에 볼륨 패널 토글을 연결

---

## 2. AudioMixer 에셋 — 자동화 불가 제약

Unity에는 `AudioMixer` 에셋을 코드로 생성하는 공개 API가 없다.
- `AssetDatabase.CreateAsset()` — AudioMixer 미지원
- `UnityEditor.Audio.AudioMixerController` — internal 클래스, 버전 의존성 높음

**결론**: AudioMixer 에셋 자체는 Unity Editor에서 수동으로 생성해야 한다. 단, Exposed Parameter 이름이 코드의 PlayerPrefs 키와 정확히 일치해야 하므로 사용자에게 명확한 지침 제공이 필요하다.

필요한 수동 작업:
1. `Assets/_Project/Audio/` 폴더에 AudioMixer 에셋 생성
2. BGM Group, SFX Group 추가
3. 각 그룹 Volume 파라미터 Expose:
   - Master Volume → 이름: `MasterVolume`
   - BGM Volume → 이름: `BGMVolume`
   - SFX Volume → 이름: `SFXVolume`

---

## 3. SoundConfig.asset — 자동화 가능

`ScriptableObject.CreateInstance<SoundConfig>()` + `AssetDatabase.CreateAsset()` 패턴으로 생성 가능.
기존 에셋 생성 스크립트(SetupLoginScene.cs 내 에셋 생성 코드)와 동일한 방식.

---

## 4. 기존 에디터 스크립트 패턴 (SetupLoginScene.cs)

| 패턴 | 내용 |
|------|------|
| 메뉴 경로 | `[MenuItem("Hexiege/Setup/...")]` |
| 씬 열기 | `EditorSceneManager.OpenScene(path)` |
| GameObject 생성 | `new GameObject(name)` → `AddComponent<T>()` |
| 필드 주입 | `SerializedObject` + `FindProperty` |
| 씬 저장 | `EditorSceneManager.SaveScene(scene, path)` |
| 에셋 생성 | `ScriptableObject.CreateInstance<T>()` + `AssetDatabase.CreateAsset()` |
| 폴더 생성 | `System.IO.Directory.CreateDirectory()` |

---

## 5. 에디터 스크립트 분리 전략

작업 성격이 다른 3개로 분리:

| 스크립트 | 대상 씬/에셋 | 주요 작업 |
|---------|------------|---------|
| `SetupAudioManager.cs` | Login.unity + SoundConfig.asset | [Audio] GO 생성, AudioManager 부착, SoundConfig 생성, LoginBootstrapper 연결 |
| `SetupInGameVolumePanel.cs` | Game.unity | InGameSettingsUI 팝업 내 볼륨 패널 UI 생성 + 슬라이더 필드 연결 |
| `SetupLobbyVolumePanel.cs` | Lobby.unity | 로비 볼륨 패널 UI 생성 + Settings 버튼 연결 |

각 스크립트는 독립 실행 가능하도록 설계하되, 전체를 한 번에 실행할 수 있는 마스터 메뉴 항목도 제공.

---

## 6. AudioMixer 연결 방식

에디터 스크립트에서는 AudioMixer를 `AssetDatabase.LoadAssetAtPath<AudioMixer>(path)`로 찾아 연결한다.
AudioMixer가 아직 없는 경우 `EditorUtility.DisplayDialog`로 경고 후 종료.

```csharp
const string MixerPath = "Assets/_Project/Audio/GameAudioMixer.mixer";
var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
if (mixer == null)
{
    EditorUtility.DisplayDialog("AudioMixer 없음",
        $"{MixerPath} 를 먼저 생성해주세요.\n" +
        "Create > Audio Mixer → BGM/SFX 그룹 추가 → 각 볼륨 Expose",
        "확인");
    return;
}
```
