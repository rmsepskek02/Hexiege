# Research — 유닛 공격 효과음 묶음 통일(무기 종류 × 강중약 + 특수 공격 전용)과 유닛 공격 외형 프로필

**작성일:** 2026-11-02
**상태:** 조사 완료 · 「8. 남은 결정」 대부분 사용자 결정 반영(2026-11-02, 「11. 사용자 결정·답변」) — 일부 항목 미확인 유지 · [Plan](Plan.md) 작성 · 승인(2026-11-02) · 1단계 1-B · 1-C 사용자 테스트 PASS(범위 한정 — 큰 전투 청취는 연기, 2026-11-03, 「11-15」 · Plan 17절) · EagleArcher 공격음 결정(2026-11-03, 「11-17」) *(종전: 「8. 남은 결정」 전부 미결; 2026-11-03 갱신 — 직전 「조사 완료 · Plan 미작성(사용자 지시 없음) · …」)*
**근거 표기:** [확인] = 이 문서 작성 중 원본 파일·코드를 직접 다시 읽거나 측정함 · [전달] = 메인 세션이 이 대화에서 전달한 사실(원문 대조 불가) · [미확인] = 어느 문서·파일로도 확인하지 못함(이유 병기)

---

## 0. 이 문서는 무엇인가 (먼저 읽기)

지금 게임 유닛 25종의 공격 효과음은 「유닛마다 하나씩 따로 만든다」는 계획이었다. 사용자가 Unity AI로 샘플 몇 개를 만들어 들어 보니, 유닛마다 따로 만들어도 소리 차이가 크지 않았고(예: 곰 방패병과 바위 정령이 거의 같은 저음), 프롬프트로 「이 유닛은 이런 음색」이라고 세밀하게 지시해도 Unity AI가 잘 따르지 않았다. 그래서 사용자는 방향을 바꿨다 — **「무기 종류(예: 총, 칼, 둔기 …) × 세기(강·중·약)」로 소리를 몇 묶음만 만들고 여러 유닛이 같은 소리를 나눠 쓰며, 특수한 공격을 하는 유닛만 전용 소리를 준다.**

그런데 이 방향으로 가려면 먼저 「각 유닛이 실제로 무엇으로 어떻게 때리는가」를 알아야 한다. 지금까지 그 정보가 어느 문서에도 없어서, 효과음 가이드의 짧은 키워드(「바위 투척」 등)만 보고 잘못된 소리를 주문하는 실수가 두 번 있었다. 그래서 사용자는 **유닛별 공격 외형 프로필 문서(`UnitAttackProfile.md`)를 새로 만들고**, 효과음 가이드·목록을 그에 맞춰 고치기로 결정했다.

이 Research는 그 작업의 준비 자료다. 다음을 담는다.

- 지금까지의 경과와 소리 측정 결과(그리고 측정의 한계)
- 공격음이 코드에서 언제·어떻게 나오는지(같은 소리를 여러 유닛이 나눠 쓸 수 있는지)
- 25종 유닛별로 문서·파일에서 확인할 수 있는 사실(공격 구조, 타격 횟수, 사거리, 무기 모델 파일, 몸집)과 **확인하지 못한 것**
- 사용자에게 물어야 할 목록(유닛별 공격 동작·무기·재질)
- 지금 가이드·목록에 적힌 유닛 설명 중 실제와 다른 것
- 아직 정하지 않은 결정 사항(이 문서에서는 결정하지 않는다)

이 문서는 아무것도 바꾸지 않는다. 문서·코드·에셋 수정은 Plan과 사용자 승인 뒤의 일이다.

---

## 1. 배경과 경과

| # | 사실 | 근거 |
|---|---|---|
| 1-1 | 사용자가 기존 공격음이 마음에 들지 않아 Unity AI로 유닛 공격음을 새로 만들기로 했다. 제작 원칙은 `VFXSFXGuide.md`, 목록은 `VFXSFXList.md`가 원본이다. 목록의 「완성된 SFX」는 「현재 없음」이다(`VFXSFXList.md` 296~298행). | [전달] 경위 · [확인] 목록 문구 |
| 1-2 | 샘플 6종(Pistoleer, LittleKnight, EmberSpirit, BoulderSpirit, EagleArcher, BearGuard)의 프롬프트를 asset-prompt-crafter가 작성했고 사용자가 일부를 생성했다. 저장 위치는 `Assets/_Project/Audio/SFX/Units/`. 이 폴더에 6종 파일이 모두 있고 `SoundConfig.asset`에 6종이 연결돼 있다(3-4절). | [전달] 경위 · [확인] 파일·연결 |
| 1-3 | Unity AI 생성물은 프롬프트의 「모노 · 짧은 길이 · -3dBFS」 지시를 따르지 않았다(스테레오, 1.04초 고정, 뒤쪽 무음). 아직 보정하지 않은 3개 파일은 실제로 스테레오 · 1.045초다(2-2절). | [전달] · [확인] 파일 형식 |
| 1-4 | 이에 음량 통일 에디터 도구를 추가했다 — `Assets/Editor/Tools/SfxNormalizeCore.cs`, `SfxNormalizeTool.cs`, 메뉴 `Hexiege/Audio/Normalize Selected SFX`(`SfxNormalizeTool.cs` 45행). 처리: 모노로 합치기 → 앞뒤 무음 자르기(+끝 페이드아웃) → 체감 크기 -26dB(50ms 창 최대 RMS) → 피크 -1dBFS 이하 → 44100Hz 16bit 모노 저장(`SfxNormalizeTool.cs` 6행 주석, `SfxNormalizeCore.cs` 59·65·83행 상수). 덮어쓰기 전 원본을 `Assets/_Project/Audio/.backups/`에 복사한다(`SfxNormalizeTool.cs` 22·51행). | [확인] |
| 1-5 | 사용자가 일부 파일에 도구를 실행했다. `.backups`에 eaglearcher(10-27 16:22) · pistoleer(10-27 16:23) · littleknight(10-28 02:02) 원본 3개가 있다. 이 백업 3개는 git이 추적하고 있다(`git ls-files` 결과에 포함). | [확인] |
| 1-6 | 사용자 피드백: Pistoleer 1차 「유난히 크고 듣기 싫음」 → 음색 재작성 프롬프트 제공. LittleKnight 「적절히 잘 나옴」. BearGuard 「더 강하게」. 이후 사용자: EmberSpirit · BearGuard · EagleArcher · BoulderSpirit 사이 차이가 크지 않아 유닛별로 나눌 의미가 없다 → 「무기 종류 × 강중약」 통일 방향 승인, 특수 공격은 전용 소리. | [전달] |
| 1-7 | 실수 2건이 이미 `.claude/mistakes.md`에 기록돼 있다(108행 목차, 2124~2149행 본문). ① 가이드 키워드로 공격 방식을 정해 EmberSpirit 「불꽃 구 발사」 · BoulderSpirit 「바위 투척」으로 잘못 작성 — 실제 둘 다 근접, 사용자 확인 「팔로 내려치는 공격」. ② BearGuard를 「앞발 내려치기」로 출처를 「사용자 설명」이라 위조해 전달 — 실제 사용자 확인 「나무 방패를 옆으로 휘둘러 때림」(mistakes.md 본문 표현은 「나무 방패를 휘둘러 때리는 공격」, 재질 「나무」는 이후 질문으로 확인). 근본 원인: 유닛 공격 외형 정보가 어느 문서에도 없다. | [확인] mistakes.md 기록 · [전달] 사용자 확인 문장 |
| 1-8 | 사용자 결정(2026-11-02) — 기록 위치: ① 새 문서 `Assets/_Project/Docs/Assets/UnitAttackProfile.md`(유닛별 공격 동작 · 무기 · 재질 · 특수 공격 여부 · 몸집 등 외형 프로필. **공격력 · HP 등 수치는 복사하지 않고 StatsReference 링크와 등급 근거만**), ② `VFXSFXGuide.md`에 효과음 묶음 규칙(무기 종류 × 강중약, 특수 기준, 등급별 음색 방향) — 기존 유닛별 키워드 표 대체, ③ `VFXSFXList.md`에 유닛 → 효과음 파일 배정 · 제작 상태, ④ `AGENTS.md` 인덱스 한 줄. 이유: 수치 중복을 피하면 동기화 부담이 작고, 외형 정보의 단일 원본이 재발을 막는다. | [전달] |

> 1-7의 「어느 문서에도 없다」 보충 [확인]: `Docs/Assets/AssetList.md` 「유닛 (Units)」 표의 비고 칸에 역할 · 속성 라벨(예: 「곰탱커」, 「사자검사」, 「불정령1」, 「흙정령2」)이 있고, 「기타 오브젝트」 표에 EagleArrow · EagleBow · RabbitSword가 「무기 서브프리팹」으로 적혀 있다(AssetList 64~103행). 그러나 **무엇으로 · 어떻게 때리는지(공격 동작)와 재질은 어디에도 없다** — 이 점은 1-7과 같다. 애니메이션 클립 이름도 모두 `<유닛>_Attack` 형식이라 동작을 알려 주지 않는다(`Assets/_Project/Animations/Units/`).

---

## 2. 측정 결과와 한계

### 2-1. 메인 세션 측정 [전달]

방법: 순수 Python, 체감 크기 = 50ms 창 최대 RMS, 음역 = 영교차 기반 대략값.

| 파일 | 체감 크기 | 대략 음역 | 보정 여부(당시) |
|---|---|---|---|
| Pistoleer 1차 | -20.4dB | 약 1,800Hz(고음 성분 많음) | — |
| EmberSpirit | -40.1dB | 약 2,300Hz | 미보정 |
| BearGuard | -33.7dB | 약 170Hz | 미보정 |
| BoulderSpirit | -22.5dB | 약 160Hz | 미보정 |
| EagleArcher | -26.0dB | 약 560Hz | 보정됨 |

메인 세션 해석 [전달]: BearGuard · BoulderSpirit 모두 저음 위주로 비슷했고, 프롬프트의 「200~800Hz」 지시도 반영되지 않았다 → Unity AI 프롬프트로 유닛별 음색을 세밀하게 구분하기 어렵다는 근거.

### 2-2. 이 문서 작성 중 재측정 [확인]

방법(메인 세션과 다름): 순수 Python `wave` 모듈, 채널 평균 모노 → 50ms 창 RMS를 12.5ms 간격으로 이동하며 최대값, 피크, 파일 전체 영교차 수 ÷ 2 ÷ 길이. 스크립트는 세션 임시 폴더에만 있고 저장소에 넣지 않았다.

| 파일 | 채널 | 길이 | 체감 크기 | 피크 | 영교차 기반 대략값 |
|---|---|---|---|---|---|
| `SFX/Units/sfx_pistoleer_attack.wav` | 모노 | 0.536초 | -26.2dB | -16.1dBFS | 약 1,820Hz |
| `.backups/sfx_pistoleer_attack_20261027_162302.wav` | 스테레오 | 1.045초 | -20.5dB | -10.6dBFS | 약 1,220Hz |
| `SFX/Units/sfx_littleknight_attack.wav` | 모노 | 0.432초 | -26.0dB | -16.2dBFS | 약 5,250Hz |
| `.backups/sfx_littleknight_attack_20261028_020228.wav` | 스테레오 | 1.045초 | -10.9dB | -1.0dBFS | 약 4,000Hz |
| `SFX/Units/sfx_eaglearcher_attack.wav` | 모노 | 0.451초 | -26.2dB | -13.7dBFS | 약 620Hz |
| `.backups/sfx_eaglearcher_attack_20261027_162256.wav` | 스테레오 | 1.045초 | -26.3dB | -13.9dBFS | 약 1,290Hz |
| `SFX/Units/sfx_emberspirit_attack.wav` | 스테레오 | 1.045초 | -40.3dB | -30.0dBFS | 약 2,280Hz |
| `SFX/Units/sfx_bearguard_attack.wav` | 스테레오 | 1.045초 | -33.9dB | -27.4dBFS | 약 530Hz |
| `SFX/Units/sfx_boulderspirit_attack.wav` | 스테레오 | 1.045초 | -22.5dB | -15.3dBFS | 약 370Hz |
| `SFX/Units/sfx_tank_attack.wav`(2026-06-08 커밋 `b7746f82`, SoundConfig 미연결) | 모노 | 0.800초 | -3.2dB | -0.2dBFS | 약 500Hz |

읽는 법:
- 체감 크기는 메인 세션 값과 0.3dB 안에서 일치한다(Ember -40.1/-40.3, BearGuard -33.7/-33.9, Boulder -22.5/-22.5, Eagle -26.0/-26.2, Pistoleer 1차 -20.4/백업 -20.5).
- **음역 대략값은 방법에 따라 크게 다르다** — BearGuard 170 대 530, Boulder 160 대 370. 이 문서의 값은 무음 · 잡음 구간까지 포함한 파일 전체 영교차라 메인 세션 값과 직접 비교하면 안 된다. 다만 두 방법 모두 측정한 6종 가운데 BoulderSpirit · BearGuard가 가장 낮은 쪽이라는 순서는 같다.
- 스테레오 · 1.045초인 Ember · BearGuard · Boulder 3개는 아직 보정 도구를 거치지 않은 상태다(1-5의 백업도 없다). 모노 · 짧은 길이인 Pistoleer · LittleKnight · EagleArcher 3개는 보정된 상태다.
- Pistoleer: 현재 파일은 백업(1차 원본) 직후 시각에 만들어진 보정본이다(백업 파일명 시각 16:23:02, 현재 파일 수정 시각 16:23). 음색 재작성 프롬프트로 새로 만든 Pistoleer 파일이 있는지는 **[미확인]** — 폴더에 다른 Pistoleer 파일이 없다.

### 2-3. 한계

- 메인 세션 비교 당시 4종은 음량이 보정되지 않아 체감 크기 차이가 최대 약 18dB였다(Ember -40.1 ~ Boulder -22.5) [전달 · 수치 확인]. 크기가 다르면 사람이 「음색 차이」와 「크기 차이」를 섞어 듣는다 — 「차이가 크지 않다」는 판단이 같은 크기에서 다시 들어 봐도 유지되는지는 **[미확인]**.
- 영교차 기반 음역은 대략값이고 방법 의존성이 크다(2-2). 정식 주파수 분석(스펙트럼)은 하지 않았다.
- 측정 대상은 샘플 6종(+옛 Tank 파일)뿐이다. 나머지 19종은 파일이 없다.

---

## 3. 코드 영향 범위 — 공격음은 언제 · 어떻게 나오는가 [확인]

### 3-1. 클립 고르는 경로 — 여러 유닛이 같은 파일을 써도 되는가

- 모든 공격음은 `AudioManager.PlayUnitAttackSfx(UnitType)` 한 곳을 거친다(`Presentation/Audio/AudioManager.cs` 503~507행). 이 메서드는 `SoundConfig`에서 그 유닛 종류의 클립과 볼륨을 받아 `PlaySfxClip`으로 재생한다.
- `SoundConfig`(`Infrastructure/Config/SoundConfig.cs`)는 유닛 종류별 항목 목록 `_unitSoundEntries`(157행)를 유닛 종류 → 항목 사전으로 바꿔 쓴다(184~188행). 항목마다 `attackClip`(50행) · `attackVolume`(54행)이 있다. 항목이 없으면 클립은 없음(212행 `return null`), 볼륨은 1.0(224행).
- 클립이 없으면 재생하지 않는다(`AudioManager.cs` 559행 `if (clip == null …) return;`). 즉 **항목이 없는 유닛은 소리가 나지 않는다.**
- 항목은 유닛 종류마다 따로이고 각 항목의 클립 칸은 오디오 파일 참조일 뿐이므로, **여러 유닛 항목이 같은 파일을 가리키는 것은 코드 수정 없이 가능하다**(같은 파일을 막는 검사 코드 없음). 볼륨도 항목마다 따로 줄 수 있다(사운드 규칙 16 「SFX 클립별 볼륨 조절」, `GameSystemRules_Sound.md`).

### 3-2. 재생 순간 — 유닛 종류별 네 갈래

| 갈래 | 대상 유닛 | 언제 소리가 나는가 | 근거 |
|---|---|---|---|
| ① 타격 marker | 투사체 유닛 · TorrentSpirit을 뺀 나머지(근접 · 히트스캔 유닛) | 공격 애니메이션의 타격 신호(marker)마다 1번. **다회 타격 유닛은 타격마다** — LittleKnight 2번, LionKnight 2번, FlameSpirit 6번(타격 횟수는 4절 표). | `Presentation/Unit/UnitView.cs` `ProcessAttackMarker`(2718행~) → 3188~3189행 `if (!_hasAttackChargeMarker) AudioManager.Instance?.PlayUnitAttackSfx(...)` |
| ② 서버 투사체 발사 | Tank · CannonCart · InfernoSpirit · StreamSpirit · FoxMagician · EagleArcher · MushroomBomber(분류 `ServerProjectileDeliveryPolicy.IsProjectileUnit`) | 서버 발사 기록이 화면에 나오는 순간 1번. 이 유닛들의 타격 marker는 소리 없이 끝난다. | `UnitView.cs` 3074~3075행(투사체 판정) · 3085행 `if (serverProjectileMarker) return;` → `PlayServerProjectileLaunchCue`(1457행~) 1471행에서 재생. 호출: `Presentation/Effects/ProjectilePresentationPresenter.cs` 310행 |
| ③ 파도 생성 | TorrentSpirit | 파도 표현이 생성되는 순간 1번(파도 표현 담당이 연결되면 marker 경로는 2750~2751행에서 끝난다). | `Presentation/Effects/WavePresentationPresenter.cs` 108행 `PlayUnitAttackSfx(UnitType.TorrentSpirit)` · 47행(담당 연결) |
| ④ 차지 시작 | InfernoSpirit · FoxMagician의 차지 marker | **소리 없음.** 코드 주석: 「차지 SFX는 추후 SFX 작업에서 다룬다」. | `UnitView.cs` 3182~3189행 주석과 조건 |

- **BloomFairy(힐러)**: 힐 marker가 허용되면(2794~2800행) 일반 정책 검사(2929~2931행, 힐은 통과)를 지나 ①과 같은 3189행에 도달하는 것으로 코드를 읽었다 — 즉 `PlayUnitAttackSfx(BloomFairy)`가 호출되는 구조로 보인다. 실행 관측은 없다 **[실행 미확인]**. 지금은 BloomFairy 항목이 `SoundConfig`에 없어 실제 소리는 나지 않는다.

### 3-3. 동시 재생 한도와 클립 길이

- 동시에 재생할 수 있는 효과음은 최대 8개이고 넘으면 새 요청을 버린다(사운드 규칙 13, `AudioManager.cs` 155행 기본값 8, `Login.unity` 저장값 8).
- 재생 한 번이 그 클립 길이만큼 자리를 차지한다(`AudioManager.cs` 569행 — 클립 길이 뒤 반환). 그래서 **뒤 무음이 남은 1.04초 파일은 실제 소리보다 오래 자리를 잡는다.** 다회 타격 유닛(FlameSpirit 6타)이나 주기가 짧은 유닛(Assault 0.333초)이 많을수록 영향이 크다. 묶음 방식에서도 이 사실은 같다(같은 파일을 나눠 써도 재생은 유닛마다 따로 일어난다).
- *(4차 2026-11-02: 이 한도 동작을 바꾸기로 결정 — 11-14. 위 문단은 현재 코드 기준 기록이다.)*

### 3-4. 지금 `SoundConfig.asset` 연결 상태 [확인]

`Assets/_Project/Resources/Config/SoundConfig.asset` `_unitSoundEntries`(20~49행) — 6항목, 그 외 19종은 항목 없음(무음):

| 유닛 종류 번호 → 유닛 | 연결 파일(GUID로 대조) | 볼륨 |
|---|---|---|
| 0 → Pistoleer | `sfx_pistoleer_attack.wav` | 0.85 |
| 24 → EagleArcher | `sfx_eaglearcher_attack.wav` | 0.85 |
| 20 → BearGuard | `sfx_bearguard_attack.wav` | 0.9 |
| 3 → LittleKnight | `sfx_littleknight_attack.wav` | 0.85 |
| 14 → BoulderSpirit | `sfx_boulderspirit_attack.wav` | 0.9 |
| 11 → EmberSpirit | `sfx_emberspirit_attack.wav` | 0.85 |

유닛 종류 번호는 `Domain/Unit/UnitType.cs` 18~50행 열거형 값과 대조했다(예: FlameSpirit=10, EmberSpirit=11 — 정령계는 이름 순서와 번호 순서가 다르다). 폴더의 `sfx_tank_attack.wav`(2026-06-08)와 `sfx_unit_death.wav`는 이 표에 연결돼 있지 않다.

> **[2026-11-02 3차 추가]** 위 표는 작성 당시 상태다. 이후 `sfx_pistoleer_attack.wav` · `sfx_tank_attack.wav` · `sfx_unit_death.wav`가 삭제됐다(커밋 전) — Pistoleer 칸은 「찾을 수 없음」 참조가 됐다. 또 VFX 프리팹 4개가 AudioSource로 옛 클립을 직접 재생하던 사실이 새로 확인됐다. 상세는 11-13.

### 3-5. 보정 도구의 문서상 위치 [확인]

- 보정 도구는 어느 문서에도 적혀 있지 않다(`Docs/`, `AGENTS.md`에서 `SfxNormalize` · `Normalize Selected` 검색 0건).
- `WORKFLOW.md` [5-2] 「에디터 스크립트 최소 유지 기준」 표(로그 저장 · ZIP · 강제 실패 · 전투 검사 2개)에 없다. 그 절은 「현재 필요한 … 만 유지한다」고 적고 있으므로, 이 도구를 계속 둘지는 결정이 필요하다(8절).
- `VFXSFXGuide.md` 「SFX 프롬프트 추가 원칙」(300행~)은 여전히 「모노, 44100Hz, 16bit」 · 공격음 「0.3초 ~ 1.0초」를 프롬프트로 지시하라고만 적고, Unity AI가 이를 따르지 않는다는 사실과 보정 절차는 없다.

---

## 4. 유닛별 자료 표 (25종)

공통 출처: 공격 구조 · 타격 횟수 = `Docs/Assets/UnitCombatAssetMatrix.md` 「공격 프로필 및 준비 상태」(88~119행) 「목표 프로필」 · 「기본 Attack 이벤트」 열. 사거리 · 타격 시점 · 주기 = `Docs/StatsReference.md` 유닛 표(46~93행) — **참고 인용이며 새 프로필 문서에는 복사하지 않는다(사용자 결정 1-8).** 모두 [확인].

### 4-1. 전투 구조와 소리 재생 시점

| 유닛 | 종족 | AssetList 비고(역할 · 속성) | 공격 구조(Matrix) | 타격 횟수 / 공격 1회 | 사거리(타일) | 타격 · 발사 시점(초) | 전체 주기(초) | 소리 갈래(3-2) |
|---|---|---|---|---|---|---|---|---|
| Pistoleer | Human | 권총병 | Hitscan · Single | 1 | 1 | 0.8 | 2 | ① |
| Assault | Human | 돌격소총병 | Hitscan · Single | 1 | 2 | 0.1667 | 0.3333 | ① |
| Sniper | Human | 저격총병 | Hitscan · Single | 1 | 5 | 1.7333 | 3 | ① |
| LittleKnight | Human | 근접 보병 | MeleeContact · Single · MultiImpact(2) | **2** | 0.5 | 0.25, 1.15 | 3 | ① ×2 |
| SpearMan | Human | 창병 | MeleeContact · Single | 1 | 1 | 0.8333 | 2 | ① |
| BattleAxe | Human | 도끼병 | MeleeContact · Single 직격 + Area/Cone(부채꼴 휩쓸기, SweepAttackBehavior) | 1 | 0.75 | 1.02 | 3.1667 | ① |
| Tank | Human | 중장갑 포격 전차 | ProjectileImpact(잠정) · Single · 서버 투사체 | 1 | 3 | 0.1667(발사) | 4 | ② |
| CannonCart | Human | 원거리 포격 수레 | ProjectileImpact(잠정) · **Single** · 서버 투사체 | 1 | 3 | 0.1667(발사) | 4 | ② |
| EmberSpirit | Spirit | 불정령1 | MeleeContact · Single | 1 | 0.5 | 1 | 2.6667 | ① |
| FlameSpirit | Spirit | 불정령2 | MeleeContact · Single · MultiImpact(6) | **6** | 0.5 | 0.6667, 1.1667, 1.4333, 1.6667, 1.9, 2.1 | 3 | ① ×6 |
| InfernoSpirit | Spirit | 불정령3 | ProjectileImpact(잠정) · Single · 착탄 뒤 화상(DoT) · 차지 유닛 · InfernoAttackBehavior | 1 | 4 | 1.5(발사, 차지 시작 0.1667) | 3 | ② (차지 ④ 무음) |
| DustSpirit | Spirit | 흙정령1 | MeleeContact · Single | 1 | 0.5 | 1.04 | 3 | ① |
| BoulderSpirit | Spirit | 흙정령2 | MeleeContact · Single | 1 | 0.5 | 1.3333 | 4 | ① |
| QuakeSpirit | Spirit | 흙정령3 | MeleeContact · Single 직격 + Area/Circle(주변 피해, QuakeAttackBehavior) | 1 | 0.5 | 1.667 | 5 | ① |
| TideSpirit | Spirit | 물정령1 | MeleeContact · Single | 1 | 0.5 | 1.5 | 3 | ① |
| StreamSpirit | Spirit | 물정령2 | ProjectileImpact(잠정) · Single · 서버 투사체 | 1 | 3 | 0.5(발사) | 1.5 | ② |
| TorrentSpirit | Spirit | 물정령3 | TravelingArea · Area/Rectangle · 피해+힐 · 파도(TorrentAttackBehavior) | 1(파도 발사) | 3 | 2.3333(파도 발사) | 4 | ③ |
| FoxMagician | Transcendence | 여우마법사 | ProjectileImpact(잠정) · Single · 서버 투사체 · 차지 유닛 | 1 | 3 | 2.8333(발사, 차지 시작 1) | 4 | ② (차지 ④ 무음) |
| BearGuard | Transcendence | 곰탱커 | MeleeContact · Single | 1 | 0.5 | 0.4333 | 1.6667 | ① |
| LionKnight | Transcendence | 사자검사 | MeleeContact · Single · MultiImpact(2) | **2** | 0.5 | 0.6, 1.4333 | 2 | ① ×2 |
| RhinoBreaker | Transcendence | 돌진 탱커 | MeleeContact · Single · 특수 처리 없음 | 1 | 0.5 | 1.05 | 2 | ① |
| EagleArcher | Transcendence | 원거리 궁수 | ProjectileImpact(잠정) · Single · 서버 투사체 · 착탄 이펙트 없음 | 1 | 3 | 0.2(발사) | 1 | ② |
| RabbitTrickster | Transcendence | 민첩 근접 | MeleeContact · Single | 1 | 0.5 | 0.6667 | 2 | ① |
| MushroomBomber | Transcendence | 범위 폭발 딜러 | ProjectileImpact · Homing · Single 직격 + Area/Circle · 독(DoT) · BlastAttackBehavior | 1 | 2 | 1(발사) | 3 | ② |
| BloomFairy | Transcendence | 힐러 | Hitscan cast(잠정) · Single · **Heal** · 별도 힐 경로 | 1(힐 발동) | 4 | 1(힐 발동) | 4 | ①로 보임(3-2 BloomFairy 문단, 실행 미확인) |

- Matrix 「특수 처리」 열에 전용 동작이 있는 유닛은 BattleAxe(Sweep) · InfernoSpirit(Inferno) · QuakeSpirit(Quake) · TorrentSpirit(Torrent) · MushroomBomber(Blast) · BloomFairy(별도 Healer 경로)다. 이것을 효과음의 「특수 공격」 범위로 볼지는 **미결**(8절).
- EagleArcher의 전체 주기 칸은 StatsReference 원문이 「Attack 1」로 적혀 있어 그대로 옮겼다(초 단위 의미인지는 원문 표기 그대로 둔다).

### 4-2. 외형 정보

무기 모델 파일: 각 유닛 Blue · Red 프리팹(`Assets/_Project/Prefabs/Units/<종족>/Unit_<유닛>_<팀>.prefab`)이 GUID로 참조하는 fbx 가운데 유닛 본체가 아닌 것을 모았다(`Assets/_Project/Models/Units/` 아래 fbx `.meta`의 GUID와 대조). **무기 종류는 파일 이름 기준이며 모델을 눈으로 확인하지 않았다.**
몸집: `Assets/_Project/Resources/Config/UnitEffectConfig.asset` `_recipientFits`(241~366행)의 radius(몸통 반경) · height(몸통 키), 단위 월드 미터, 발밑 기준(`Presentation/Effects/UnitEffectConfig.cs` 85~101행). 자산의 `unitType` 숫자는 `UnitType` 열거형 값과 직접 대응한다(필드 타입이 `UnitType`, 91행) — 25개 번호 모두 대조했다.

| 유닛 | 무기 · 부속 모델 파일(파일명 기준) | 몸집 radius / height (m) | 사용자 확인 공격 동작 | 무기 · 몸 재질 | 지금 공격음 |
|---|---|---|---|---|---|
| Pistoleer | `Human/Pistoleer/Pistol.fbx` | 0.096 / 0.342 | 미확인 | **권총 — 옛날 총(화약식)** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | **파일 삭제됨(3차 실행, 커밋 전) — SoundConfig 칸은 「찾을 수 없음」 참조로 무음(11-13)** · 종전: 샘플 연결(보정됨) |
| Assault | `Human/Assault/Rifle.fbx` | 0.256 / 0.444 | 미확인 | **연사 소총 — 옛날 총(화약식)** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 없음 |
| Sniper | `Human/Sniper/Sniper Rifle.fbx` | 0.240 / 0.510 | 미확인 | **저격총 — 옛날 총(화약식)** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 없음 |
| LittleKnight | `Human/HumanBlade/HumanBlade.fbx`(유닛 폴더 밖) | 0.135 / 0.334 | **찌르고 휘두르는 2타 — 공격음은 하나로 충분(구분 불필요)** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 샘플 연결(보정됨) |
| SpearMan | `Human/HumanSpear/HumanSpear.fbx`(유닛 폴더 밖) | 0.148 / 0.422 | **창으로 찌르기** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 (2차 답변 11-12) | 미확인 | 없음 |
| BattleAxe | `Human/HumanAxe/HumanAxe.fbx`(유닛 폴더 밖) | 0.184 / 0.440 | **큰 반원을 그리며 휘두르기** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 없음 |
| Tank | 별도 파일 없음(`Human/Tank/Tank.fbx`만) | 0.413 / 0.521 | 미확인(CannonCart와 차이는 거의 없고 스탯 차이 — 확인됨(사용자, 2026-11-02)) | **큰 전차포 느낌** — 확인됨(사용자, 2026-11-02, 2차 답변 11-12) · 종전: 미확인(포 외형 미답) | 없음(옛 `sfx_tank_attack.wav`는 미연결 → **3차 실행에서 삭제, 커밋 전(11-13)**) |
| CannonCart | 별도 파일 없음(`Human/Cannon/Cannon.fbx`만) | 0.405 / 0.530 | 미확인(Tank와 차이는 거의 없고 스탯 차이 — 확인됨(사용자, 2026-11-02)) | **큰 전차포 느낌** — 확인됨(사용자, 2026-11-02, 2차 답변 11-12) · 종전: 미확인(포 외형 미답) | 없음 |
| EmberSpirit | 없음 | 0.155 / 0.346 | **팔로 내려치기**(사용자 확인) | **몸 = 불** — 확인됨(사용자, 2026-11-02) · 종전: 미확인(속성 라벨은 불) | 샘플 연결(**미보정**) |
| FlameSpirit | 없음 | 0.179 / 0.379 | **격투선수가 연속 주먹질하는 연속 공격** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인(속성 라벨은 불) | 없음 |
| InfernoSpirit | 없음 | 0.274 / 0.427 | **자기 앞에 불덩이를 모으는 액션 → 화염구 발사**(투사체 VFX 제작됨) — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인(속성 라벨은 불) | 없음 |
| DustSpirit | 없음 | 0.189 / 0.293 | **특별한 액션 없음 — 양손을 머리 위로 들었다가 내려치기** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | **몸 = 바위** — 확인됨(사용자, 2026-11-02, 2차 답변 11-12) · 종전: 미확인(속성 라벨은 흙, 몸 재질 미답) | 없음 |
| BoulderSpirit | 없음 | 0.221 / 0.378 | **팔로 내려치기**(사용자 확인) | **몸 = 바위** — 확인됨(사용자, 2026-11-02) · 종전: 미확인(속성 라벨은 흙) | 샘플 연결(**미보정**) |
| QuakeSpirit | 없음(Blue 프리팹만 `Spirit/QuakeSpirit/QuakeSpirit/QuakeSpirit/QuakeSpirit.fbx`를 추가로 참조 — 용도 미확인) | 0.249 / 0.458 | **하늘로 뛰어올라 땅을 내려치기**(VFX 제작됨) — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인(속성 라벨은 흙) | 없음 |
| TideSpirit | 없음 | 0.135 / 0.343 | **원문 「FlameSpirit와 같다고 생각하면 돼. 물 속성이지만 별도의 속성 공격이 아닌 팔로 내려치는 모션」** — 확인됨(사용자, 2026-11-02, 2차 답변) · 종전: 미확인(미답). **해석 쟁점 재확인 중**: 감사표는 1타(Flame은 6타) — 「연속 주먹질」인지 「팔 내려치기 1회」인지 미정(11-12). **3차 답변(2026-11-02)으로 해소: 「FlameSpirit이 아니라 Ember」 — EmberSpirit과 같은 팔로 내려치는 1회 타격 · 종전: 해석 재확인 중(11-13)** | 미확인(속성 라벨은 물, 몸 재질 미답) | 없음 |
| StreamSpirit | 없음 | 0.167 / 0.416 | 발사체 = **물폭탄을 발사하는 컨셉**(VFX 제작됨) — 확인됨(사용자, 2026-11-02, 2차 답변 11-12) · 종전: 미확인(미답 — 동작 · 발사체). 발사 동작 자체는 미답 | 미확인(속성 라벨은 물, 몸 재질 미답) | 없음 |
| TorrentSpirit | 없음 | 0.192 / 0.425 | **양팔을 벌려 뒤로 젖히며 기를 모으듯 하다가 앞으로 뻗으며 파도 발사** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인(속성 라벨은 물) | 없음 |
| FoxMagician | `Transcendence/FoxMagician/FoxStaff.fbx` | 0.175 / 0.307 | **지팡이를 대상 방향으로 뻗어 차징 → 차징된 구체 발사** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 없음 |
| BearGuard | `Transcendence/BearGuard/BearShield.fbx` | 0.220 / 0.530 | **나무 방패를 옆으로 휘둘러 때림**(사용자 확인) | 방패 = **나무**(사용자 확인) | 샘플 연결(**미보정**) |
| LionKnight | `Transcendence/LionKnight/LionSword.fbx` | 0.226 / 0.420 | **장검 연속 베기** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | **무기 = 장검(검)** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 (검의 재질은 미답) | 없음 |
| RhinoBreaker | 없음 | 0.249 / 0.509 | **제자리에서 주먹질 — 복싱의 훅 같은 모션** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 없음 |
| EagleArcher | `EagleArcher/EagleBow/EagleBow.fbx`, `EagleArcher/EagleArrow/EagleArrow.fbx` | 0.196 / 0.467 | 미확인 | **나무 활, 화살촉만 금속** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 샘플 연결(보정됨) |
| RabbitTrickster | `RabbitTrickster/RabbitSword/RabbitSword.fbx` | 0.089 / 0.356 | **단검으로 찌르기** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | **무기 = 단검** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 (단검 재질은 미답) | 없음 |
| MushroomBomber | 없음 | 0.157 / 0.388 | **공을 던지듯 독포자를 던짐**(VFX 제작됨) — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 없음 |
| BloomFairy | 없음 | 0.131 / 0.434 | **하늘로 무언가를 던지는 듯한 모션** — 확인됨(사용자, 2026-11-02) · 종전: 미확인 | 미확인 | 없음(항목 없음) |

- 「속성 라벨」은 AssetList 비고(불정령 · 흙정령 · 물정령)에서 온 것이고, 몸이 실제로 불 · 바위 · 물로 표현됐는지는 모델을 보지 않아 **미확인**이다.
- 메인 세션이 전달한 확인 예 중 「Assault `Assault Rifle.fbx`」는 실제 파일명이 `Rifle.fbx`다(`Models/Units/Human/Assault/`) — 위 표는 실제 이름을 따른다. 나머지 5개(Pistol, Sniper Rifle, BearShield, FoxStaff, LionSword)는 그대로 있다. 또 LittleKnight · SpearMan · BattleAxe의 무기는 유닛 폴더가 아니라 `HumanBlade` · `HumanSpear` · `HumanAxe` 폴더에 있어, 「유닛 폴더 안의 다른 fbx」만 보면 놓친다 — 프리팹 참조로 찾아야 한다.
- 「무기 · 부속 모델 파일 없음」은 「무기를 들지 않는다」는 뜻이 아니다 — 본체 모델에 붙어 있을 수 있다(미확인).
- **[2026-11-02 추가]** 위 표의 「확인됨(사용자, 2026-11-02)」 칸은 메인 세션이 전달한 사용자 답변이다 [전달] — 원문 대화는 볼 수 없고 모델은 여전히 눈으로 보지 않았다. 답이 없는 칸은 「미확인」 그대로 두었다. 답변 전체는 「11. 사용자 결정·답변」.

---

## 5. 사용자 확인 필요 목록

새 프로필 문서와 묶음 배정에 필요한데 문서 · 파일로 확인할 수 없는 것. **추정해서 채우지 않는다**(1-7 실수 재발 방지).

| 유닛 | 물어야 할 것 | 질문에 쓸 수 있는 단서(확인된 것만) | 답변 상태(2026-11-02 추가 열 — 종전 표에는 없었다, 앞 세 칸은 원문 그대로) |
|---|---|---|---|
| Pistoleer · Assault · Sniper | 총 종류가 파일명대로인지(권총 · 소총 · 저격총), 총의 재질 · 외형(현대식/구식 등) | 무기 파일 `Pistol` · `Rifle` · `Sniper Rifle`. Assault는 공격 1회 = 1발, 주기 0.333초 | 확인됨(사용자, 2026-11-02) — 권총 · 연사 소총 · 저격총, 셋 다 옛날 총(화약식) · 종전: 미확인 |
| LittleKnight | 무엇으로 어떻게 치는지(베기/찌르기 등), 칼 재질 | 무기 파일 `HumanBlade`, 공격 1회에 2타 | 확인됨(사용자, 2026-11-02) — 찌르고 휘두르는 2타, 공격음 하나로 충분 · 종전: 미확인. 칼 재질은 미답(미확인) |
| SpearMan | 찌르기인지 휘두르기인지, 창 재질 | 무기 파일 `HumanSpear`, 사거리 1 | 확인됨(사용자, 2026-11-02, 2차 답변) — 창으로 찌르기 · 종전: 미확인(찌르기/휘두르기 답변 없음). 창 재질은 미답 |
| BattleAxe | 도끼를 **내려치는지 옆으로 휩쓰는지**, 도끼 재질 | 무기 파일 `HumanAxe`. 판정은 전방 부채꼴 휩쓸기(Matrix · StatsReference 반각 120°). 가이드는 「도끼 내려치기」 | 확인됨(사용자, 2026-11-02) — 큰 반원을 그리며 휘두르기 · 종전: 미확인. 도끼 재질은 미답(미확인) |
| Tank · CannonCart | 발사 외형(포 크기, 차체 재질), 둘이 같은 소리여도 되는지 | 둘 다 서버 투사체 · 단일 대상 · 같은 tracer · 착탄 이펙트 | 일부 확인됨(사용자, 2026-11-02) — 둘의 차이는 거의 없고 스탯 차이, 같은 포격 소리 사용 가능 · 종전: 미확인. 포 외형 = **큰 전차포 느낌** — 확인됨(사용자, 2026-11-02, 2차 답변) · 종전: 미확인 유지 |
| EmberSpirit · BoulderSpirit | 팔(몸)의 재질 — 불꽃 몸인지, 바위 몸인지 | 동작은 확인됨(팔로 내려치기) | 확인됨(사용자, 2026-11-02) — Ember 불 몸, Boulder 바위 몸 · 종전: 미확인 |
| FlameSpirit | 무엇으로 어떻게 치는지, 6타가 어떤 동작인지 | 근접 · 공격 1회 6타 | 확인됨(사용자, 2026-11-02) — 격투선수가 연속 주먹질하는 연속 공격 · 종전: 미확인 |
| InfernoSpirit | 무엇을 쏘는지(화염구 등)와 발사 동작, 차지 동작 | 서버 투사체 · 차지 유닛 · 착탄 뒤 화상 | 확인됨(사용자, 2026-11-02) — 화염구(투사체 VFX 제작됨), 자기 앞에 불덩이를 모으는 액션 · 종전: 미확인 |
| DustSpirit · TideSpirit | 무엇으로 어떻게 치는지, 몸 재질 | 둘 다 근접 1타 | DustSpirit 동작만 확인됨(사용자, 2026-11-02) — 양손을 머리 위로 들었다가 내려치기 · 종전: 미확인. DustSpirit 몸 = **바위** — 확인됨(사용자, 2026-11-02, 2차 답변) · 종전: 미확인 유지. TideSpirit 동작 — 원문 확인됨(2차 답변), **해석(연속 주먹질 / 팔 내려치기 1회) 재확인 중**(11-12) · 종전: 미확인 유지. **3차 답변으로 해소 — EmberSpirit과 같은 팔 내려치기 1회(11-13)**. **TideSpirit 몸 재질은 미확인 유지** |
| QuakeSpirit | 무엇으로 어떻게 치는지(내려찍기 등) | 근접 직격 + 주변 범위 피해 | 확인됨(사용자, 2026-11-02) — 하늘로 뛰어올라 땅을 내려치기(VFX 제작됨) · 종전: 미확인 |
| StreamSpirit | 무엇을 쏘는지(물줄기 · 물방울 등) | 서버 투사체 1발 | 발사체 확인됨(사용자, 2026-11-02, 2차 답변) — 물폭탄을 발사하는 컨셉, VFX 제작됨 · 종전: 미확인 유지. **발사 동작 · 몸 재질은 미확인 유지** |
| TorrentSpirit | 파도를 일으키는 동작 | 파도가 전진하며 접촉 대상에 피해 · 아군 힐 | 확인됨(사용자, 2026-11-02) — 양팔을 벌려 뒤로 젖히며 기를 모으듯 하다가 앞으로 뻗으며 파도 발사 · 종전: 미확인 |
| FoxMagician | 지팡이로 무엇을 쏘는지, 지팡이 재질 | 무기 파일 `FoxStaff`, 차지 유닛 | 확인됨(사용자, 2026-11-02) — 지팡이를 대상 방향으로 뻗어 차징, 차징된 구체 발사 · 종전: 미확인. 지팡이 재질은 미답(미확인) |
| BearGuard | (확인됨) 나무 방패 옆으로 휘두르기 — 이 외에 추가 정보가 필요하면 질문 | 무기 파일 `BearShield` | 확인됨(종전과 같음 — 사용자 재확인 2026-11-02) |
| LionKnight | **검인지 발톱인지**(가이드는 「발톱 베기」), 2타가 어떤 동작인지, 검 재질 | 무기 파일 `LionSword`, 공격 1회 2타 | 확인됨(사용자, 2026-11-02) — 장검(검) 연속 베기 · 종전: 미확인. 검 재질은 미답(미확인) |
| RhinoBreaker | **돌진인지 제자리 들이받기 · 내려치기인지**, 뿔 · 무기 여부 | 근접 1타, 특수 처리 없음(Matrix). AssetList 「돌진 탱커」 | 확인됨(사용자, 2026-11-02) — 제자리에서 주먹질, 복싱의 훅 같은 모션(돌진 아님) · 종전: 미확인 |
| EagleArcher | 활 재질, 시위 소리 위주인지 | 무기 파일 `EagleBow` · `EagleArrow` | 확인됨(사용자, 2026-11-02) — 나무 활, 화살촉만 금속 · 종전: 미확인. 시위 소리 위주인지는 미답 |
| RabbitTrickster | **단검인지 검인지**(가이드는 「단검」), 무엇으로 어떻게 치는지 | 무기 파일 `RabbitSword` | 확인됨(사용자, 2026-11-02) — 단검으로 찌르기 · 종전: 미확인 |
| MushroomBomber | 무엇을 던지는지(포자 등)와 던지는 동작 | 서버 투사체(유도) · 착탄 범위 · 독 | 확인됨(사용자, 2026-11-02) — 공을 던지듯 독포자를 던짐(VFX 제작됨) · 종전: 미확인 |
| BloomFairy | 힐 시전에 공격음이 필요한지 · 어떤 소리인지 | 힐러, 공격력 0. 코드상 공격음 호출 경로에 닿는 것으로 보임(3-2) | 확인됨(사용자, 2026-11-02) — 하늘로 무언가를 던지는 듯한 모션, 힐 소리 필요(메인 세션 제안 A안 채택 — 11-3) · 종전: 미확인 |

---

## 6. 기존 문서 문제 — 가이드 · 목록의 유닛 설명이 감사표와 다른 것

대조 대상: `VFXSFXGuide.md` 「1. 유닛 공격 이펙트」 키워드 표(357~385행) · 프롬프트 예시(332~360행) · 「1. 유닛 공격 사운드」 키워드 표(554~582행), `VFXSFXList.md` 「유닛 공격 사운드」 표(304~331행) ↔ `UnitCombatAssetMatrix.md` 공격 프로필. 모두 [확인].

### 6-1. 감사표와 분명히 어긋나는 것

| 유닛 | 가이드 · 목록 표기 | 감사표 · 확인된 사실 |
|---|---|---|
| EmberSpirit | VFX 「불 마법 · fire orb」, VFX 프롬프트 예시 「작은 불꽃 에너지 구가 발사되는 이펙트」(Guide 349행), SFX 「fire whoosh」 | 근접 1타(MeleeContact), 사용자 확인 팔로 내려치기 |
| BoulderSpirit | VFX 「rock throw, boulder」, SFX 「heavy rock throw」, 목록 「바위 투척음」 | 근접 1타, 사용자 확인 팔로 내려치기 |
| BearGuard | VFX 「heavy slam, ground shake, paw」, SFX 「heavy slam, bear growl」 | 사용자 확인 나무 방패 옆으로 휘두르기, `BearShield.fbx` |
| CannonCart | VFX 「범위 포격 · area explosion」, 목록 「범위 포격음」 | Single(단일 대상) 서버 투사체 — 범위 피해 없음 |
| DustSpirit · TideSpirit | 「흙 마법」 · 「물 마법」(공격 방식 칸) | 둘 다 근접 1타(MeleeContact) |
| FlameSpirit | 「불 마법 · fire burst」 | 근접 · 공격 1회 6타 — 근접과 6타 모두 표에 없음 |

### 6-2. 모델 파일 · 판정과 맞지 않을 수 있어 사용자 확인이 필요한 것

| 유닛 | 가이드 · 목록 표기 | 확인된 사실 | 비고 |
|---|---|---|---|
| LionKnight | 「claw slash」, 목록 「발톱 베는 소리」 | 무기 파일 `LionSword.fbx`, 2타 | 검인지 발톱인지 미확인 |
| RabbitTrickster | 「단검」 | 무기 파일 `RabbitSword.fbx` | 파일명은 sword — 단검 여부 미확인 |
| BattleAxe | 「도끼 내려치기」 | 판정은 전방 부채꼴 휩쓸기 | 판정 모양과 애니메이션 동작은 다를 수 있어 동작 미확인 |
| RhinoBreaker | 「돌진 · charge dust」 | 근접 1타, 돌진 특수 처리 없음 | 애니메이션이 돌진 동작인지 미확인 |

### 6-3. 다회 타격 · 구조 정보 누락

- LittleKnight(2타) · LionKnight(2타) · FlameSpirit(6타)가 타격마다 공격음을 낸다는 사실이 가이드에 없다.
- InfernoSpirit · FoxMagician의 차지 구간에 소리가 없다는 사실(3-2 ④)이 가이드에 없다.

### 6-4. 그 밖의 문서 문제

- `VFXSFXList.md` 「유닛 공격 사운드」 표는 24행이고 **BloomFairy 행이 없다**. 가이드 키워드 표에는 BloomFairy가 있다(「gentle heal chime」).
- `VFXSFXList.md` 「완성된 SFX: 현재 없음」 — 샘플 6종이 이미 `SoundConfig`에 연결돼 게임에서 재생되는 상태다(3-4). 이 6종을 「완성」으로 볼지는 사용자 판단이며, 목록이 연결 상태를 전혀 보여 주지 않는다는 사실만 기록한다.
- `VFXSFXGuide.md` 「1. 유닛 공격 사운드」 머리말 「같은 근접 공격이라도 종족별로 다르게」(552행)는 새 방향(무기 종류 × 강중약 묶음)과 충돌할 수 있다 — 종족별 사운드 스타일(Guide 24~33행)을 묶음 규칙에 남길지는 미결.
- `VFXSFXGuide.md` 「SFX 프롬프트 추가 원칙」의 오디오 사양 · 길이 지시가 Unity AI에서 지켜지지 않는다는 사실과 보정 도구가 문서에 없다(3-5).

---

## 7. 작업 중 발견한 부가 사항

- **`.backups` 폴더는 git에 커밋돼 있다**(`git ls-files`에 3개 wav 포함). 폴더 이름이 점으로 시작해 Unity는 가져오지 않는다(이 폴더에 `.meta`가 없다). git에서 뺄지는 미결(8절).
- **옛 파일 `sfx_tank_attack.wav`**(2026-06-08 `b7746f82`, 모노 0.8초, 체감 -3.2dB · 피크 -0.2dBFS)가 미연결 상태로 남아 있다. 새 샘플 기준(-26dB)보다 약 23dB 크다. 재사용 · 삭제 여부는 미결 범위(기존 샘플 재사용 여부)에 포함된다.
- **QuakeSpirit Blue 프리팹만** 중첩 폴더의 `QuakeSpirit.fbx`를 추가로 참조한다(Red는 참조하지 않음). 효과음과 직접 관계는 없고 용도는 미확인 — 기록만 한다.
- 차지 효과음(차지 시작 순간 소리)은 코드 주석상 「추후 SFX 작업」으로 남아 있다(3-2 ④). 이번 범위에 넣을지는 「작업 범위」 결정에 포함된다.

---

## 8. 남은 결정 (작성 당시 미결 — 2026-11-02 사용자 결정은 오른쪽 열과 「11」절. 이 문서에서는 정하지 않는다)

| # | 결정할 것 | 이 문서에서 모은 관련 사실 | 상태(2026-11-02 추가 열 — 종전: 전부 미결) |
|---|---|---|---|
| D-1 | 무기 종류 묶음 목록 | 4-2 무기 파일 · 5절 확인 결과에 따라 달라짐 | 방향 확인됨(사용자, 2026-11-02) — 「무기 종류 × 강중약」 공용 소리 · 종전: 미결. **구체 묶음 목록은 아직 없음(Plan에서 제안)**. **3차 답변: 등급(강중약) 기준은 공격력이 아니라 유닛의 특성과 모션, 모르는 부분은 사용자에게 묻고 제작 전에 반드시 먼저 언급(11-13)** |
| D-2 | 특수 공격 유닛 범위 | Matrix 특수 처리 유닛 6종(4-1 아래 문단), 차지 유닛 2종, 파도(Torrent) · 힐(Bloom)의 별도 재생 경로 | 확인됨(사용자, 2026-11-02) — 특수 공격 유닛은 모두 전용 SFX: Tank · CannonCart(같은 포격 소리 가능, Human 방어 타워도 Tank와 같은 SFX) · InfernoSpirit · QuakeSpirit · TorrentSpirit · FoxMagician · MushroomBomber · BloomFairy · BattleAxe · 종전: 미결. RuneSpire 전용(번개)은 **메인 세션 제안 — 사용자 확정 전(미확인)**, VineTower는 **보류**(11-1). **2차 답변(2026-11-02): RuneSpire 전용 확정 · 종전: 제안. VineTower는 기존 컨셉 기준 설계 결정, 공격음은 그 설계 확정 후(11-12)**. **4차: VineTower 공격 VFX = 작은 정적 덩굴 모델 파티클(B안), 공격음은 VFX 확정 후(11-14)** |
| D-3 | 강중약 음량 차등 여부 — 같은 -26dB + 음색 차이만 vs 등급별 기준(예: 강 -23 / 중 -26 / 약 -29 → 보정 도구 수정 필요) | 보정 도구의 기준값은 상수 하나(`SfxNormalizeCore.cs` 59행 -26.0). 항목별 볼륨 칸(`attackVolume`)도 따로 있다(3-1) | 확인됨(사용자, 2026-11-02) — 강중약 모두 같은 -26dB로 시작, 테스트하며 필요 시 조정 · 종전: 미결 |
| D-4 | 다회 타격 유닛 처리 | LittleKnight 2 · LionKnight 2 · FlameSpirit 6타, 타격마다 재생, 동시 8개 한도 · 클립 길이만큼 점유(3-3) | 확인됨(사용자, 2026-11-02) — 다른 유닛과 동일하게 진행 후 테스트로 조정 · 종전: 미결. **4차: 동시 재생 한도 개편 채택(같은 클립 최대 3개 · 최소 간격 약 0.05초 · 특수 · 타워 소리 우선 · 전체 8→16) — 사운드 규칙 13 개정 + AudioManager 수정, Plan에 포함(11-14)** |
| D-5 | 기존 샘플 재사용 여부 | 6종 샘플(3개 보정 · 3개 미보정) + 옛 Tank 파일(2-2, 7절) | 확인됨(사용자, 2026-11-02) — 사용할 수 있으면 재사용(듣기 싫은 소리이거나 배정과 안 맞을 때만 새로 제작). Pistoleer 1차본은 「듣기 싫음」 · 종전: 미결. **2차 답변: 사용하지 않을 기존 SFX 삭제 지시 — 진행 중(11-12)**. **3차: 목록 확정 · 삭제 실행(git rm, 커밋 전) — eaglearcher · bearguard · boulderspirit · emberspirit은 같은 음량으로 맞춘 뒤 재테스트로 결정, littleknight 유지(11-13)** |
| D-6 | 작업 범위(공격음만인지) | 사망음 경로도 같은 구조(`PlayUnitDeathSfx`, `UnitView.cs` 2077행 · `NetworkUnit.cs` 1162행)이나 항목의 사망 클립은 6항목 모두 비어 있다(`SoundConfig.asset` `deathClip: {fileID: 0}`). 차지 효과음 미구현(7절) | 확인됨(사용자, 2026-11-02) — 공격음 + 방어 타워 공격음(코드 추가 포함), 원거리는 발사음만(착탄음 제외), 차징 소리 제외, 사망음 제외(나중에) · 종전: 미결. **3차: 타워 공격음 코드는 「임시 코드, 개편할 것」 — 개편 범위 · 시점 미결. 우선순위는 제작보다 문서 정리 · 작성 먼저(11-13)** |
| D-7 | `.backups` git 무시 여부 | 현재 git 추적 중(7절) | 확인됨(사용자, 2026-11-02) — git 무시. 메인 세션이 `.gitignore`에 추가 · 추적 해제(11-6) · 종전: 미결 |
| D-8 | 보정 도구를 `WORKFLOW.md` [5-2] 유지 도구로 등록할지 | 현재 표에 없음 · 어느 문서에도 없음(3-5) | 일부 확인됨(사용자, 2026-11-02) — 도구는 계속 사용 · 종전: 미결. **`WORKFLOW.md` [5-2] 등록 여부 자체는 답변에 없음(미확인)**. 길이 상한 경고 기능 추가 여부는 Plan에서(11-7) |

---

## 9. 관련 파일 경로

**문서**
- `Assets/_Project/Docs/Assets/VFXSFXGuide.md` — 고칠 대상(효과음 묶음 규칙, 유닛별 키워드 표 대체)
- `Assets/_Project/Docs/Assets/VFXSFXList.md` — 고칠 대상(유닛 → 효과음 배정 · 제작 상태)
- `Assets/_Project/Docs/Assets/UnitAttackProfile.md` — 새로 만들 대상(아직 없음)
- `AGENTS.md` — 인덱스 한 줄 추가 대상
- `Assets/_Project/Docs/Assets/UnitCombatAssetMatrix.md` — 공격 구조 · 타격 횟수 원본
- `Assets/_Project/Docs/StatsReference.md` — 사거리 · 타격 시점 · 수치 원본(새 문서는 링크만)
- `Assets/_Project/Docs/Assets/AssetList.md` — 역할 · 속성 라벨, 무기 서브프리팹 3종
- `Assets/_Project/Docs/GameSystemRules/GameSystemRules_Sound.md` — 규칙 13(동시 8개) · 15(VFX+SFX 쌍) · 16(항목별 볼륨)
- `Assets/_Project/Docs/WORKFLOW.md` — [5-2] 에디터 스크립트 유지 기준
- `.claude/mistakes.md` — 108행 · 2124~2149행(이번 작업의 실수 2건)

**코드 · 설정 · 에셋**
- `Assets/_Project/Scripts/Presentation/Audio/AudioManager.cs` — 155 · 503~507 · 556~570행
- `Assets/_Project/Scripts/Infrastructure/Config/SoundConfig.cs` — 50 · 54 · 157 · 184~188 · 208~225행
- `Assets/_Project/Scripts/Presentation/Unit/UnitView.cs` — 1457~1471 · 2718~ · 2750~2751 · 2794~2800 · 3074~3085 · 3182~3189행
- `Assets/_Project/Scripts/Presentation/Effects/ProjectilePresentationPresenter.cs` — 310행
- `Assets/_Project/Scripts/Presentation/Effects/WavePresentationPresenter.cs` — 47 · 108행
- `Assets/_Project/Scripts/Domain/Unit/UnitType.cs` — 18~50행
- `Assets/_Project/Scripts/Presentation/Effects/UnitEffectConfig.cs` — 85~101행
- `Assets/_Project/Resources/Config/SoundConfig.asset` — 20~49행
- `Assets/_Project/Resources/Config/UnitEffectConfig.asset` — 241~366행
- `Assets/Editor/Tools/SfxNormalizeCore.cs`, `Assets/Editor/Tools/SfxNormalizeTool.cs`
- `Assets/_Project/Audio/SFX/Units/`, `Assets/_Project/Audio/.backups/`
- `Assets/_Project/Models/Units/`, `Assets/_Project/Prefabs/Units/`

---

## 10. 직접 확인하지 못한 근거

| 항목 | 이유 |
|---|---|
| 1-1 · 1-2 · 1-6 · 1-8의 경위 · 사용자 발언 · 결정 | 이 대화의 메인 세션 전달. 원문 대화는 볼 수 없다 |
| 사용자 확인 동작 3건(Ember · Boulder 팔로 내려치기, BearGuard 나무 방패 옆으로 휘두르기) | 메인 세션 전달 + mistakes.md 기록. BearGuard는 mistakes.md에 「옆으로」가 없다 |
| 2-1 메인 세션 측정값(특히 음역 170Hz · 160Hz) | 방법이 달라 재현하지 못함 — 체감 크기만 0.3dB 안에서 재확인 |
| Unity AI가 「200~800Hz」 지시를 반영하지 않았다는 판단 | 프롬프트 원문을 보지 않음 |
| 무기 종류(파일명 기준) · 재질 · 몸 표현 | 모델을 눈으로 보지 않음 |
| BloomFairy 힐 marker의 공격음 호출 | 코드를 읽은 결과일 뿐 실행 관측 없음 |
| Pistoleer 음색 재작성본의 존재 | 폴더에 해당 파일 없음 |
| QuakeSpirit 중첩 fbx의 용도 | 조사 범위 밖 |

---

## 11. 사용자 결정·답변 (2026-11-02)

출처: 메인 세션이 이 대화에서 전달한 사용자 답변 · 결정이다 **[전달]** — 원문 대화는 볼 수 없다. 이 절은 결정을 기록할 뿐이며 문서 · 코드 · 에셋은 아직 바꾸지 않았다(실행은 Plan과 사용자 승인 뒤). 위 4-2 · 5 · 8절의 해당 칸은 「확인됨(사용자, 2026-11-02)」으로 고쳤고 종전 표기는 같은 칸에 남겼다.

### 11-1. 방향과 특수 공격(전용 SFX) 범위 — D-1 · D-2

- 방향: 공격음을 「무기 종류 × 강중약」 공용 소리로 통일하고, **특수 공격을 하는 유닛은 모두 전용 SFX**를 준다.
- 전용 SFX 대상:

| 대상 | 결정 | 비고 |
|---|---|---|
| Tank · CannonCart | 전용(포격) | 둘은 스탯 차이뿐이라 **같은 포격 소리를 써도 된다** |
| Human 방어 타워 | Tank와 같은 SFX | 방어 타워 공격음은 11-4 범위 참고 |
| InfernoSpirit · QuakeSpirit · TorrentSpirit · FoxMagician · MushroomBomber · BloomFairy | 전용 | — |
| BattleAxe | 전용(베는 소리) | **특수로 분류** — 특수 역할 유닛이라 전용 베는 소리 |
| RuneSpire(Spirit 방어 타워) | **확정 — 전용**(2차 답변 11-12) · 종전: 제안 — 사용자 명시 확인 전 | 메인 세션이 전용(번개) 제안. 종전: 확정 전이므로 미확인으로 둔다 |
| VineTower(Transcendence 방어 타워) | **공격음 보류** | 사용자는 촉수 공격을 원하나 모션 · VFX가 미정. 설계는 별도 작업으로 제안됨. **2차 답변: 기존 컨셉 기준 설계로 결정 — 공격음은 설계 확정 후(11-12)**. **4차: 공격 VFX를 「작은 정적 덩굴 모델 파티클(B안)」로 만들기로 결정, 공격음은 그 VFX 확정 후(11-14)** |

- 위 목록에 없는 유닛은 공용 묶음 대상이다. **구체적인 묶음 목록(무기 종류 · 강중약 배정)은 아직 정하지 않았다 — Plan에서 제안한다.**
- 참고: 4-1 아래 문단의 Matrix 특수 처리 6종(BattleAxe · Inferno · Quake · Torrent · Mushroom · Bloom)은 모두 전용에 들어갔고, 여기에 Tank · CannonCart · FoxMagician이 더해졌다.

### 11-2. 음량과 다회 타격 — D-3 · D-4

- 강 · 중 · 약 모두 **같은 기준(-26dB)으로 시작**하고, 테스트하며 필요하면 조정한다.
- 다회 타격 유닛(LittleKnight 2 · LionKnight 2 · FlameSpirit 6타)도 다른 유닛과 **동일하게 진행한 뒤 테스트로 조정**한다.

### 11-3. 기존 샘플 · BloomFairy 힐 소리 — D-5

- 기존 샘플은 **사용할 수 있으면 재사용**한다. 새로 만드는 이유는 「듣기 싫은 소리」이거나 「배정과 안 맞을 때」뿐이다.
- Pistoleer 1차본은 사용자 「듣기 싫음」.
- BloomFairy: 힐 소리가 필요하다. 메인 세션 제안 **A안 채택** — 부드럽게 올라가는 맑은 종소리 3~4음 + 꽃잎 바람, **너무 길지 않게**.

### 11-4. 작업 범위 — D-6

| 포함 | 제외 |
|---|---|
| 유닛 공격음 | 원거리 **착탄음**(착탄음은 재생 코드가 없어 추가 작업이 필요했다) |
| **방어 타워 공격음(코드 추가 포함)** | **차징 소리**(3-2 ④) |
| 원거리 유닛은 **발사음만** | **사망음**(나중에) |

- 방어 타워 공격음의 재생 코드 경로는 이 Research에서 조사하지 않았다(3절은 유닛만 다룬다) — **[미확인]**, Plan 작성 전 확인 대상.
- *(3차 답변 2026-11-02: 사용자 「임시로 작성된 코드고 현재는 컨셉별로 나누어 사용하기 때문에 개편할 것」 — 개편 범위 · 시점은 미결, 11-13)*

### 11-5. 길이 상한과 톤

- **길이 상한 규칙을 정한다.** 이유: 대규모 전투라 특정 유닛 SFX가 길면 소리가 난무한다(3-3 동시 8개 한도 · 클립 길이만큼 점유와 같은 문제). **값은 Plan에서 제안한다.**
- **톤: 「카툰 + 묵직함」** — 카툰 비주얼에 맞추되 장난감 같은 가벼움은 빼고 묵직 · 단단하게. 명료함(여러 소리가 겹쳐도 각 소리가 들림)은 기본 전제다. *(3차 답변: 힐 소리는 이 톤의 예외인 특수 케이스, 11-13)*
- 사용자는 특정 게임(스타크래프트) 같은 느낌을 언급했다. **프롬프트에는 게임명을 쓰지 않고 특징으로 풀어 쓴다.**

### 11-6. 백업 폴더 — D-7

- 원본 백업은 **PC에만** 둔다. 메인 세션이 `.gitignore`에 `/Assets/_Project/Audio/.backups/`를 추가했고, 이미 추적되던 백업 3개를 `git rm --cached`로 추적 해제했다(파일은 PC에 남음, **커밋 안 함**). [확인] 작성 시점 `git status`: `.gitignore` 수정(M), 백업 3개 인덱스 삭제(D), `git diff -- .gitignore`에 위 경로 한 줄과 주석 2줄 추가.
- `Assets/_Project/Audio/Effects/.backups/sfx_healing_magic_sharp_original.wav`는 이번 대상이 **아니다 — 사용자 확인 대기**(미확인). 이 파일은 PC에 있다 [확인]. *(2차 답변 2026-11-02: 사용자 「못 쓸 것 같다, 새로 제작」 — 힐 소리는 새로 제작, 11-12)*

### 11-7. 보정 도구 — D-8

- 보정 도구(`Hexiege/Audio/Normalize Selected SFX`)는 **계속 사용**한다.
- 현재 기준(동일 -26dB)이면 도구 수정은 필요 없다. 등급별 음량이 필요해지면 그때 수정한다.
- 길이 상한 경고 기능을 추가할지는 **Plan에서** 정한다.
- `WORKFLOW.md` [5-2] 유지 도구 표에 등록할지는 답변에 없다 — **미확인**.

### 11-8. 문서 정정(Plan에서 실행)

- CannonCart를 **단일 대상**으로 정정(6-1).
- BattleAxe를 「**큰 반원을 그리며 휘두르기**」로 정정(6-2).
- `VFXSFXList.md`에 **BloomFairy 행 추가**(다른 유닛과 통일, 6-4).

### 11-9. 기록 위치 — 1-8과 같음(확인만)

1-8의 네 곳과 같다: 새 문서 `Docs/Assets/UnitAttackProfile.md`(수치 복사 없이 StatsReference 링크), `VFXSFXGuide.md`(묶음 규칙), `VFXSFXList.md`(배정 · 상태), `AGENTS.md` 한 줄. 새로 바뀐 것은 없다.

### 11-10. 유닛별 답변 요약

| 유닛 | 답변 |
|---|---|
| Pistoleer · Assault · Sniper | 권총 · 연사 소총 · 저격총 — 셋 다 옛날 총(화약식) |
| LittleKnight | 찌르고 휘두르는 2타, 공격음은 하나로 충분(구분 불필요) |
| BattleAxe | 큰 반원을 그리며 휘두르는 액션 |
| Tank · CannonCart | 차이는 거의 없고 스탯 차이 |
| EmberSpirit · BoulderSpirit | Ember 불 몸 · Boulder 바위 몸 — 둘 다 팔로 내려치기 |
| FlameSpirit | 격투선수가 연속 주먹질하는 연속 공격 |
| InfernoSpirit | 화염구(투사체 VFX 제작됨), 자기 앞에 불덩이를 모으는 액션 |
| DustSpirit | 특별한 액션 없음 — 양손을 머리 위로 들었다가 내려치기 |
| QuakeSpirit | VFX 제작됨, 하늘로 뛰어올라 땅을 내려치기 |
| TorrentSpirit | 양팔을 벌려 뒤로 젖히며 기를 모으듯 하다가 앞으로 뻗으며 파도 발사 |
| FoxMagician | 지팡이를 대상 방향으로 뻗어 차징, 차징된 구체 발사 |
| BearGuard | 나무 방패를 옆으로 휘둘러 때림 |
| LionKnight | 장검 연속 베기(검) |
| RhinoBreaker | 제자리에서 주먹질 — 복싱의 훅 같은 모션 |
| EagleArcher | 나무 활, 화살촉만 금속 |
| RabbitTrickster | 단검으로 찌르기 |
| MushroomBomber | VFX 제작됨, 공을 던지듯 독포자를 던짐 |
| BloomFairy | 하늘로 무언가를 던지는 듯한 모션, 힐 소리 필요(11-3 A안) |

- 이 답변으로 6-2의 확인 대기 4건이 풀렸다: LionKnight = 검(발톱 아님), RabbitTrickster = 단검, BattleAxe = 큰 반원 휘두르기(가이드 「내려치기」와 다름), RhinoBreaker = 제자리 훅(가이드 「돌진」과 다름). 6절 본문은 작성 당시 기록이라 그대로 두고, 가이드 · 목록 정정은 Plan에서 다룬다.

### 11-11. 아직 답이 없는 것(미확인 유지)

> 2026-11-02 2차 답변으로 풀린 행은 취소선 + 「→ 11-12」로 표시했다(행은 지우지 않음). 현재 남은 미확인은 11-12 끝 목록이 기준이다.

| 항목 | 위치 |
|---|---|
| ~~SpearMan 동작(찌르기/휘두르기)~~ → 2차 답변으로 확인(11-12) | 4-2 · 5 |
| TideSpirit · StreamSpirit 동작 · 발사체 → 2차 답변: Tide 원문 확인 · 해석 재확인 중, Stream 발사체 확인 · 발사 동작 미답(11-12) | 4-2 · 5 |
| ~~DustSpirit 몸 재질~~ → 2차 답변으로 확인(바위, 11-12) | 4-2 · 5 |
| TideSpirit · StreamSpirit 몸 재질 | 4-2 · 5 |
| ~~Tank · CannonCart 포 외형~~ → 2차 답변으로 확인(큰 전차포 느낌, 11-12) | 4-2 · 5 |
| ~~RuneSpire 전용(번개) 여부 확정~~ → 2차 답변으로 확정(11-12) | 11-1 · D-2 |
| ~~`Audio/Effects/.backups/sfx_healing_magic_sharp_original.wav` 처리~~ → 2차 답변: 못 씀, 힐 소리 새로 제작(11-12) | 11-6 |
| 보정 도구의 `WORKFLOW.md` [5-2] 등록 여부 | 11-7 · D-8 |
| 무기 재질 일부(LittleKnight 칼 · BattleAxe 도끼 · FoxMagician 지팡이 · LionKnight 검 · RabbitTrickster 단검), Pistoleer · Assault · Sniper · EagleArcher 공격 동작, TorrentSpirit · FlameSpirit · InfernoSpirit · QuakeSpirit · RhinoBreaker · MushroomBomber · BloomFairy 몸 재질 — 질문 목록(5절)에 있었거나 4-2 칸이 비어 있던 것 중 답변에 없는 것 | 4-2 · 5 |
| 방어 타워 공격음 재생 코드 경로(조사 안 함) | 11-4 |

### 11-12. 2차 답변 (2026-11-02)

출처: 메인 세션이 전달한 추가 사용자 답변 **[전달]**. 위 4-2 · 5 · 8 · 11-1 · 11-11의 해당 칸에 「2차 답변」으로 반영했다.

| 항목 | 답변 · 결정 | 비고 |
|---|---|---|
| SpearMan | 창으로 찌르기 | 창 재질은 미답 |
| TideSpirit | 원문: 「FlameSpirit와 같다고 생각하면 돼. 물 속성이지만 별도의 속성 공격이 아닌 팔로 내려치는 모션」 | **[3차 답변으로 해소 — 11-13: 「FlameSpirit이 아니라 Ember」, 팔 내려치기 1회]** 종전: **해석 쟁점 — 메인 세션이 사용자에게 재확인 중**: 감사표(4-1)상 TideSpirit은 공격 1회 1타, FlameSpirit은 6타다. 「Flame과 같다」가 「연속 주먹질」을 뜻하는지, 「팔 내려치기 1회」를 뜻하는지 정해지지 않았다. 이 문서에서는 고르지 않는다 |
| StreamSpirit | 물폭탄을 발사하는 컨셉, VFX 제작되어 있음 | [확인] `Assets/_Project/Prefabs/VFX/Units/`에 `vfx_streamspirit_attack` · `_tracer` · `_impact` 프리팹 3개가 있다. 발사 동작 · 몸 재질은 미답 |
| DustSpirit | 몸 재질 바위 | — |
| RuneSpire | 전용 소리 **확정**(종전: 메인 세션 제안) | 11-1 표 갱신. 소리 내용(번개)은 메인 세션 제안 문구 그대로 |
| Tank · CannonCart | 큰 전차포 느낌으로 | 같은 포격 소리 사용 가능(11-1)은 그대로 |
| `Audio/Effects/.backups/sfx_healing_magic_sharp_original.wav` | 사용자 「못 쓸 것 같다, 새로 제작」 — **힐 소리는 새로 제작** | 11-6의 「사용자 확인 대기」가 풀렸다. 파일 자체의 처리(삭제 여부)는 아래 「기존 SFX 정리」에 포함되는지 미확인 |
| 기존 SFX 정리 | 사용자 지시: 기존 SFX 중 사용할 것을 제외한 나머지는 삭제하고, 삭제분은 git에 푸시 | **진행 중** — 메인 세션이 삭제 목록을 사용자에게 확인받는 중, 아직 미실행 *(3차: 목록 확정 · 삭제 실행, 커밋 전 — 11-13)* |
| VineTower | 「기존 컨셉(대상 발밑 가시 덩굴 솟구치기) 기준」 설계로 결정. 별도 task `_Tasks/2026-11-02/14_40_vinetower-attack-design/`에서 진행 | 공격음은 그 설계 확정 후. [확인] 이 문서 갱신 시점에 그 폴더는 아직 없다 |

**2차 답변 뒤 남은 미확인**

- ~~TideSpirit 동작 해석(연속 주먹질 / 팔 내려치기 1회) — 재확인 중~~ → 3차 답변으로 해소(11-13)
- TideSpirit 몸 재질, StreamSpirit 발사 동작 · 몸 재질
- 보정 도구의 `WORKFLOW.md` [5-2] 등록 여부
- 방어 타워 공격음 재생 코드 경로(조사 안 함, 11-4)
- ~~기존 SFX 삭제 목록(사용자 확인 중)과 healing 원본 백업 파일의 처리~~ → 3차에서 목록 확정 · 삭제 실행(healing 원본 백업 포함, 커밋 전, 11-13)
- 답변에 없던 세부(11-11 마지막 행): 무기 재질 일부(칼 · 도끼 · 지팡이 · 검 · 단검 · 창), Pistoleer · Assault · Sniper · EagleArcher 공격 동작, 몸 재질 7종, EagleArcher 시위 소리 위주 여부

### 11-13. 3차 답변 · 메인 세션 실행 사실 (2026-11-02)

출처: 메인 세션이 전달한 사용자 답변 · 결정과 메인 세션 실행 사실 **[전달]**. [확인] 표시는 이 문서 갱신 중 직접 다시 확인한 것이다. 위 3-4 · 4-2 · 5 · 8 · 11-4 · 11-5 · 11-12의 해당 칸에 「3차」로 반영했다.

#### 11-13-1. 사용자 답변 · 결정

| 항목 | 답변 · 결정 | 비고 |
|---|---|---|
| TideSpirit | 「FlameSpirit이 아니라 Ember야, 잘못 말했어」 → **EmberSpirit과 같은 팔로 내려치는 1회 타격**. 물 속성이지만 별도 속성 공격 아님 | 11-12의 해석 쟁점 **해소**. 감사표(4-1) 1타와 일치 |
| 등급(강중약) 기준 | 공격력이 아니라 **해당 유닛의 특성과 모션 기준** | 잘 모르는 부분은 사용자에게 묻고, **제작 전에 반드시 사용자에게 먼저 언급** |
| 힐 소리 톤 | 「카툰 + 묵직함」의 예외인 **특수 케이스** | 11-3 A안(맑은 종소리 3~4음 + 꽃잎 바람)과 함께 읽는다 |
| 방어 타워 공격음 코드 | 사용자: 「임시로 작성된 코드고 현재는 컨셉별로 나누어 사용하기 때문에 개편할 것」 | **개편 범위 · 시점은 미결** — 메인 세션이 확인 중 |
| 우선순위 | 제작보다 **문서 정리 · 작성 먼저** | — |
| 기존 SFX 정리 | `sfx_eaglearcher` · `sfx_bearguard` · `sfx_boulderspirit` · `sfx_emberspirit_attack.wav`는 유지 → **같은 음량으로 맞춘 뒤 다시 테스트해서 결정**. `sfx_littleknight_attack.wav` 유지. `sfx_unit_death.wav`는 삭제, 나중에 새로 제작 | 11-12 「기존 SFX 정리」의 목록 확정 |

#### 11-13-2. 메인 세션 실행 — 기존 SFX 삭제(사용자 지시 「목록대로 진행」)

- `git rm`으로 삭제(파일 + meta), **아직 커밋 전** — 메인 세션은 이 반영 직후 커밋 · 푸시 예정이라고 전달했다.
  - `Audio/SFX_GunShot.wav`, `Audio/SFX_TankBlast.wav`
  - `Audio/Effects/` — `sfx_assault_fire.wav`, `sfx_bullet_hit.wav`, `sfx_healing_magic.wav`, `sfx_healing_magic_soft.wav`, `sfx_healing_magic_v3_organic.wav`, `sfx_gold_collect.wav`, `.backups/sfx_healing_magic_sharp_original.wav`(meta 없음), 빈 폴더 meta `Audio/Effects.meta`
  - `Audio/SFX/Units/` — `sfx_pistoleer_attack.wav`, `sfx_tank_attack.wav`, `sfx_unit_death.wav`
- [확인] 이 갱신 시점 `git status`: 위 파일 · meta가 모두 인덱스 삭제(D)이고, `.gitignore` 수정(M)과 `.backups/` 백업 3개 추적 해제(D)도 같은 상태다(같은 커밋에 들어갈 예정 — [전달]).
- 남은 오디오 [전달]: `SFX/Units`의 bearguard · boulderspirit · eaglearcher · emberspirit · littleknight 공격음 5개 + `GameAudioMixer.mixer`, PC 전용 `.backups/` 3개.
- 이 때문에 2-2 측정표의 Pistoleer(현재 파일) · Tank 행과 3-4 연결표의 Pistoleer 행은 **삭제된 파일 기준의 작성 당시 기록**이 됐다(본문은 그대로 둔다).
- 삭제 영향 [전달 — 메인 세션 확인]: `SoundConfig.asset`의 BGM 4칸(Login · Lobby · Battle · GameEnd — 임시 효과음이 들어 있던 칸), Pistoleer 공격음 · 사망음 칸, EagleArcher 사망음 칸이 「찾을 수 없음」 참조로 남아 **무음**이다(사운드 규칙 10). **참조 정리는 미실행.**
  - [확인] 이 갱신 중 디스크의 `SoundConfig.asset`과 삭제 파일의 HEAD meta GUID를 대조했다: BGM 4칸(15~18행)은 `sfx_healing_magic`(Login) · `sfx_healing_magic_soft`(Lobby) · `sfx_healing_magic_v3_organic`(Battle) · `sfx_gold_collect`(GameEnd)를, Pistoleer 공격음 칸(22행)은 `sfx_pistoleer_attack`을 가리킨다 — 모두 삭제 대상이라 전달 내용과 일치. **그러나 사망음 칸은 6항목 모두 `deathClip: {fileID: 0}`(24 · 29 · 34 · 39 · 44 · 49행)으로 비어 있어**, 「Pistoleer 사망음 칸 · EagleArcher 사망음 칸이 찾을 수 없음 참조로 남음」은 디스크 상태와 **다르다**(1차 조사 3-4 · D-6과 같음). `sfx_unit_death`(GUID `271d925c…`)를 참조하는 곳은 `vfx_unit_death.prefab`과 `EffectPreset_Unit_Death_Common.asset` 두 곳뿐이다(`Assets` 아래 .asset · .prefab · .unity 검색). 메인 세션이 어느 칸을 가리켰는지는 **[미확인]** — 메인 세션 확인 필요. *(4차 2026-11-02 해소: 메인 세션이 디스크를 확인 — 사망음 6칸은 이미 모두 비어 있고, 종전 전달은 옛 변경 내역을 본 오판이었다. 11-14)*

#### 11-13-3. 새 발견 — VFX 프리팹이 옛 소리를 직접 재생하던 상태(메인 세션 정정)

- VFX 프리팹 4개(`Prefabs/VFX/Units/vfx_pistoleer_attack` · `vfx_streamspirit_attack` · `vfx_tank_attack` · `vfx_unit_death`)에 AudioSource가 있다. Unity 6 직렬화 필드 `m_Resource`에 옛 클립이 연결돼 있고 `m_PlayOnAwake: 1`, `m_Enabled: 1`, `OutputAudioMixerGroup` 없음이었다 [전달].
  - [확인] 네 프리팹 모두 `m_audioClip: {fileID: 0}`, `m_PlayOnAwake: 1`, `m_Resource`의 GUID는 pistoleer · streamspirit = `cf0be5aa…`(HEAD의 `sfx_pistoleer_attack.wav.meta`와 같음), tank = `44888c9c…`(`sfx_tank_attack.wav.meta`와 같음), unit_death = `271d925c…`(`sfx_unit_death.wav.meta`와 같음). `m_Enabled` · 믹서 그룹 칸은 이 갱신에서 따로 보지 않았다.
- 의미 [전달]: 이 VFX가 재생될 때 옛 소리가 **AudioManager와 믹서를 거치지 않고**(볼륨 설정 무시) 재생되던 상태였다 — 사운드 규칙 1(EffectManager는 VFX만, 오디오는 AudioManager 전담) · 규칙 17 위반 상태.
- 메인 세션 정정: 첫날 「클립이 빈 오디오 컴포넌트라 소리가 나지 않는다」고 보고한 것은 `m_audioClip`만 보고 `m_Resource`를 보지 않은 **오판**이었다.
- 이번 삭제로 클립이 사라져 이 AudioSource들은 무음이 된다. **컴포넌트 자체 제거는 미실행(사용자 확인 대기).** *(4차: 사용자 결정 — 제거하고 AudioManager로만 재생. Plan에 포함, 승인 후 실행 · 종전: 사용자 확인 대기. 11-14)*
- 사용자의 이전 실기 청취에 이 옛 소리가 섞였을 가능성이 있다 — **확인 불가**. 2-3 「한계」(샘플 비교 판단의 조건)에 더해 읽을 것.
- `EffectPreset_Pistoleer_Attack.asset` · `EffectPreset_Unit_Death_Common.asset`의 `_sfxClip`은 코드에서 주석 처리된 옛 필드의 잔존 직렬화 값이다 [전달].

#### 11-13-4. 3차 뒤 남은 미확인 · 미결

- 방어 타워 공격음 코드 개편 범위 · 시점(메인 세션 확인 중)과 재생 코드 경로(조사 안 함)
- ~~VFX 프리팹 4개의 AudioSource 컴포넌트 제거 여부(사용자 확인 대기)~~ → 4차: 제거로 결정, Plan에 포함(11-14)
- `SoundConfig.asset`의 「찾을 수 없음」 참조 정리(미실행) · ~~「사망음 칸」 전달 내용과 디스크 상태의 차이(11-13-2)~~ → 4차 해소(11-14)
- 유지 샘플 4종(eaglearcher · bearguard · boulderspirit · emberspirit) 동일 음량 보정 뒤 재테스트 결과
- ~~삭제 커밋 · 푸시(메인 세션 예정, 이 갱신 시점 미실행)~~ → 4차: 커밋 `323dd454` · 푸시 완료(11-14)
- TideSpirit 몸 재질, StreamSpirit 발사 동작 · 몸 재질
- 보정 도구의 `WORKFLOW.md` [5-2] 등록 여부
- 답변에 없던 세부(11-12 끝 목록의 마지막 항목과 같음)

### 11-14. 4차 결정 · 사실 (2026-11-02)

출처: 메인 세션 전달 **[전달]**. [확인]은 이 갱신 중 직접 다시 확인한 것. 위 3-3 · 8(D-2 · D-4) · 11-1 · 11-13-2 · 11-13-3 · 11-13-4의 해당 칸에 「4차」로 반영했다.

| 항목 | 결정 · 사실 | 실행 상태 |
|---|---|---|
| 사망음 칸 차이(11-13-2) | 메인 세션이 디스크를 확인 — 사망음 6칸은 이미 모두 비어 있다. 종전 「Pistoleer · EagleArcher 사망음 칸이 찾을 수 없음으로 남음」은 옛 변경 내역을 본 **오판** | **해소** · 종전: 미확인 |
| VFX 프리팹 4개의 AudioSource(11-13-3) | 사용자 「소리 관리자와 볼륨 설정을 거치도록 수정하도록 해」 → 4개 VFX 프리팹의 AudioSource를 **제거**하고, 소리는 사운드 규칙 15(VFX+SFX 짝 호출)로 **AudioManager에서만** 재생 | **끄기 완료(2026-11-03 1회성 메뉴) · 삭제는 큰 전투 테스트 통과 뒤** — Plan 17절 · 종전: Plan에 포함, 승인 후 실행 · 그 전: 사용자 확인 대기 |
| 동시 재생 한도 | 메인 세션 제안을 사용자가 채택(「권장대로 진행, 특수 소리는 우선권」): ① 같은 클립 동시 최대 3개 ② 같은 클립 최소 간격 약 0.05초 ③ 특수 · 타워 소리 우선 — 한도가 차면 가장 오래된 일반 소리를 끊고 재생 ④ 전체 한도 8 → 16(테스트로 조정) | **사운드 규칙 13 개정 · AudioManager 코드 반영(`167410e8`) · 씬 한도 16(2026-11-03 메뉴)** — 큰 전투 실기 미검증(Plan 17절) · 종전: Plan에 포함, 미실행 |
| 커밋 · 푸시 | 삭제 · `.gitignore` · 이 Research가 커밋 `323dd454`에 들어갔고, 원격 커밋 `dce9e9e4`와 머지 `9c5b8885`로 `origin/claude/vfx-code-followups`에 푸시 완료. 폴더 meta `_Tasks/2026-11-02.meta` 충돌은 원격 GUID로 해소 [전달] | **완료** · 종전: 커밋 전 |
| VineTower | 공격 VFX를 「작은 정적 덩굴 모델 파티클(B안)」로 만들기로 결정(별도 task 14_40) | 공격음은 그 VFX 확정 후 |

- [확인] 현재 한도 동작: `AudioManager.cs` 155행 `_maxConcurrentSfx = 8`, 559행 `if (clip == null || _activeSfxCount >= _maxConcurrentSfx) return;` — 한도에 닿으면 새 요청을 버리며, 우선순위 · 같은 클립 제한은 없다(3-3과 같음).
- [확인] 커밋: `git show --stat 323dd454`에 이 Research.md(539행)와 meta가 들어 있다. `9c5b8885`는 `origin/claude/vfx-code-followups`에 포함돼 있다(`git branch -r --contains`). 이 갱신 시점 로컬 HEAD는 그 뒤의 `7368d7f1`(= `origin/claude/vfx-code-followups`)이다. **이 4차 갱신 내용은 아직 커밋되지 않았다.**
- 위 3-3 「동시 8개 한도 · 새 요청 버림」, D-4 · 11-5의 「동시 8개 한도」 언급은 개편 전 현재 코드 기준이다.

**4차 뒤 남은 미확인 · 미결**

- 방어 타워 공격음 코드 개편 범위 · 시점(메인 세션 확인 중)과 재생 코드 경로(조사 안 함)
- `SoundConfig.asset`의 「찾을 수 없음」 참조 정리(BGM 4칸 · Pistoleer 공격음 칸) — **완료**(2026-11-03 1회성 메뉴, 미커밋 — Plan 17-2) *(종전: 미실행)*
- 유지 샘플 4종(eaglearcher · bearguard · boulderspirit · emberspirit) 동일 음량 보정 뒤 재테스트 결과
- VFX AudioSource 제거 · 동시 재생 한도 개편 — **구현 · 사용자 테스트 PASS(범위 한정)**, AudioSource는 꺼짐 · 삭제와 큰 전투 청취는 연기(Plan 17절) *(종전: 결정됨, Plan · 승인 대기(미실행))*
- VineTower 공격음 — VFX 확정 대기
- TideSpirit 몸 재질, StreamSpirit 발사 동작 · 몸 재질
- 보정 도구의 `WORKFLOW.md` [5-2] 등록 여부 — **등록됨**(사용자 승인 2026-11-02, Plan Q-13) *(2026-11-03 갱신 — 종전: 미결)*
- 답변에 없던 세부(11-12 끝 목록의 마지막 항목과 같음)

### 11-15. 1단계 사용자 테스트 뒤 사실 · 결정 (2026-11-03)

출처: 메인 세션 전달 **[전달]**(사용자 보고 포함). 구현 위치 · 메뉴 실행 결과 · 로그 관측의 상세는 [Plan 17절](Plan.md#17-stage1-user-test) — 여기에는 사용자 결정 · 보고만 둔다.

| 항목 | 사용자 결정 · 사실 |
|---|---|
| 테스트 결과 | 1회성 메뉴 실행 완료 · LittleKnight 공격음 정상 재생 · 회귀 스위트 「문제없음」 |
| 큰 전투 청취(같은 클립 다수 · 혼합 · 장시간) | **연기** — 「SFX를 여러 가지 추가한 뒤에 해 봐도 된다」. 새 동시 재생 판단은 실기 미검증 |
| 옛 코드 주석 · 꺼 둔 AudioSource 삭제 · 1회성 메뉴 퇴역 | 큰 전투 테스트 통과 뒤로 연기 |
| 보정 도구 길이 경고 실기 확인 | 기존 샘플 재청취 단계로 연기 — EmberSpirit 파일을 덮어쓰지 않으려고(사용자 동의) |
| 다음 작업 | 1-D 프롬프트 작성 진행 |
| VineTower 덩굴 모델 | 「Unity AI 크레딧을 모두 소모해 진행 불가 — 보류」 |

### 11-16. 5차 답변 · 순서 결정 (2026-11-03)

출처: 메인 세션이 전달한 사용자 답변 **[전달]**(원문 요지 — 원문 대조 불가). 유닛별 외형 답은 [`UnitAttackProfile.md`](../../../Assets/UnitAttackProfile.md) 2절의 해당 칸(「2026-11-03 — 종전: …」 표시)과 4절 미확인 목록에 반영했다. 위 11-10 ~ 11-15의 표 · 목록은 작성 당시 기록이라 고치지 않는다.

| 항목 | 답변 · 결정 | 반영 · 비고 |
|---|---|---|
| Pistoleer · Assault · Sniper 공격 동작 | 총을 겨누고 쏘는 동작, **한 번 공격에 한 발**(Assault 포함) | 프로필 2-1 공격 동작 3칸. Assault 「공격 1회 = 1발」은 감사표(4-1 — 타격 수 1)와 일치하므로 Plan 2-2의 등급 근거(「연사 속에서 한 발씩」 — 짧은 주기로 한 발씩 반복)는 바뀌지 않는다 |
| Tank · CannonCart | 포신은 **금속 재질**(사용자: VFX를 확인하면 된다), **반동 같은 동작 없음** | 프로필 2-1 공격 동작 · 재질 각 2칸. VFX의 색 · 재질 표현은 이 갱신에서 직접 보지 않았다 **[미확인]** |
| EagleArcher | 사용자 「활은 특별히 SFX를 쓰기보다 **타격음만 있어도 되지 않을까**」 | **해석 미결 — 메인 세션이 사용자에게 다시 묻는 중.** *(2026-11-03 같은 날 결정 → [11-17](#11-17-eaglearcher-decision): 해석 ①, 타격 약 공용 소리)* 두 해석: ① 발사 순간에 활 소리 대신 타격 계열 소리를 쓴다 / ② 발사음 없이 맞을 때 소리를 낸다 — 착탄음은 이번 범위가 아니고(Plan 1-1) 착탄음을 내는 코드도 없다. 이 문서에서는 고르지 않는다. 프로필 2-3 · 3-1 · 4절에 원문과 미결을 적었다. 공격 동작 자체는 여전히 미답 |
| StreamSpirit 발사 동작 | **양손을 앞으로 모은 뒤 발사** | 프로필 2-2 공격 동작 칸. 몸 재질은 미답 |
| 몸 재질 | InfernoSpirit = **불**, QuakeSpirit = **바위**, TorrentSpirit = **물** | 프로필 2-2 재질 3칸(종전: 속성 라벨만) |
| MushroomBomber | **버섯 컨셉**, 포자 자체의 별도 컨셉은 없음 | 프로필 2-3 재질 칸 |
| FoxMagician 구체 | VFX 참고 — **푸른빛과 보랏빛**을 쓴 이펙트 | 프로필 2-3 재질 칸(지팡이 재질은 여전히 미답). VFX 색은 이 갱신에서 직접 보지 않았다 **[미확인]** |
| 효과음 생성 | Unity AI 크레딧 소진으로 **지금 생성 불가** — 프롬프트(1-D)만 먼저 쓰고, 생성(1-E)은 크레딧 확보 뒤 | Plan 1-3 1-D · 1-E, 17-5. VineTower 덩굴 모델 보류(11-15)와 같은 원인 |
| 작업 순서 | **타워 개편(Plan 1-2 ②)을 지금 진행** — 효과음 1단계 제작(1-E)보다 먼저 | Plan 1-2 표는 ① → ② 순서로 적혀 있다(작성 당시). 이 결정으로 ②가 1-E보다 앞선다. ②는 1-C가 만든 우선 재생 입구(`SfxPriority.High`)를 쓰므로(Plan 12절) 1-C가 끝난 지금 시작할 수 있다 |

**5차 뒤 남은 미확인 · 미결**

- EagleArcher 「타격음만」의 해석(①/②, 재확인 중)과 공격 동작 *(2026-11-03 해석은 11-17에서 결정 — 공격 동작만 남음)*
- 몸 재질: FlameSpirit · TideSpirit · StreamSpirit · RhinoBreaker · BloomFairy
- 무기 재질 일부(LittleKnight 칼 · BattleAxe 도끼 · SpearMan 창 · FoxMagician 지팡이 · LionKnight 검 · RabbitTrickster 단검)
- 방어 타워 재질(Human CannonTower · RuneSpire), VineTower 공격음(VFX 확정 대기 — 덩굴 모델은 크레딧 보류)
- 유지 샘플 4종 동일 음량 보정 뒤 재청취 결과, 큰 전투 청취(11-15 — 연기)
- 효과음 생성 시점(크레딧 확보 뒤)

<a id="11-17-eaglearcher-decision"></a>
### 11-17. 6차 결정 — EagleArcher 공격음 (2026-11-03)

출처: 메인 세션이 전달한 사용자 답변 **[전달]** — 원문 「EagleArcher 약으로」(11-16의 두 해석과 메인 세션 제안을 사용자에게 다시 물은 데 대한 답). 위 11-16의 표 · 목록은 작성 당시 기록이라 고치지 않고 해당 줄 끝에 날짜 조각만 붙였다.

| 항목 | 결정 | 반영 |
|---|---|---|
| EagleArcher 공격음 | **활 전용 파일 없이 타격 약 공용 소리 `sfx_strike_light_attack.wav`**(FlameSpirit과 같은 파일)를 쓴다. 11-16의 해석 ① — **쏘는 순간** 재생하며 착탄음이 아니다 | [`UnitAttackProfile.md`](../../../Assets/UnitAttackProfile.md) 3-1 행(종전 「활 · 단일」) · 4절 「활 소리」 해결 표시 · [VFXSFXList](../../../Assets/VFXSFXList.md) 「유닛 공격 사운드」 EagleArcher 행 · [Prompts.md](Prompts.md) 확정 사항 · Plan 2-2 표 · 14절 Q-7 |
| 기존 샘플 `sfx_eaglearcher_attack.wav` | 배정 파일에서 빠진다. 지금 `SoundConfig`의 EagleArcher 칸에 연결된 이 샘플을 언제 바꿀지 · 파일을 지울지는 **결정 없음** | VFXSFXList 해당 행 상태 칸 |
| 활 묶음(`sfx_bow_attack.wav`, 가이드의 활 행 · 상한 0.35초) | 이 결정으로 쓰는 유닛이 없다. 가이드에서 활 묶음 규칙을 지울지는 결정 없음 — 가이드 본문은 고치지 않고 EagleArcher 샘플 줄에만 날짜 조각을 붙였다 | [VFXSFXGuide](../../../Assets/VFXSFXGuide.md) 「길이 상한」 아래 EagleArcher 줄 |

- 공격 동작(EagleArcher가 활을 어떻게 쏘는지)은 여전히 미답이다 — 이 결정은 소리에 대한 것뿐이다. *(2026-11-03 같은 날 답변 [전달]: 「시위를 당겨 쏘는 모션이긴 한데 실제로 시위가 당겨지진 않아」 — `UnitAttackProfile.md` 2-3 공격 동작 칸 · 4절 「공격 동작」 해결로 반영)*
- 타격 약 공용 프롬프트(Prompts.md 6번)는 FlameSpirit 연타 기준으로 쓰였다. EagleArcher 발사에도 어울리는지는 생성 뒤 청취로 본다.
