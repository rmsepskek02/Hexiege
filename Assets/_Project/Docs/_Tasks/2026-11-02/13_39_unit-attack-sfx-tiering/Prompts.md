# Prompts — 유닛 공격음 Unity AI 프롬프트 18개 (1-D, 2026-11-03)

**작성일:** 2026-11-03
**상태:** 프롬프트만 보관 — **소리 파일은 아직 만들지 않았다**(Unity AI 크레딧 소진, 생성은 크레딧 확보 뒤 — [Research 11-16](Research.md#11-16-5차-답변--순서-결정-2026-11-03))
**작성:** asset-prompt-crafter(프롬프트 원문) · document-manager(이 파일로 옮김, 사용자 승인 2026-11-03 「나머지는 추천대로」)

---

## 이 문서는 무엇인가

유닛과 방어 타워가 공격할 때 나는 효과음을 새로 만들기 위해, Unity의 AI 소리 생성 기능에 그대로 넣을 **영어 주문문(프롬프트) 18개**를 모아 둔 문서다. 지금은 생성에 쓰는 크레딧이 다 떨어져서 소리를 바로 만들 수 없기 때문에, 써 둔 주문문을 잃지 않도록 이 작업 폴더에 먼저 저장해 둔다.

각 소리마다 ① 그 소리를 쓰는 유닛과 동작 설명, ② 복사해서 붙여 넣을 영어 프롬프트, ③ 한글 요약, ④ 들어 볼 때 확인할 체크포인트가 있다. 묶음 소리(총기 · 날붙이 · 타격)는 여러 유닛이 한 파일을 나눠 쓰고, 전용 소리는 특수한 공격을 하는 유닛만 쓴다.

**소리를 만든 뒤의 순서**는 [Plan 7절](Plan.md#7-제작-절차1-d--1-e)을 따른다 — Unity AI로 생성 → 메뉴 `Hexiege/Audio/Normalize Selected SFX`로 보정(모노 · 앞뒤 무음 자르기 · 음량 맞추기) → 사람이 직접 청취 → `SoundConfig`에 연결 → VFXSFXList 상태 칸 갱신.

**근거 문서:** 소리 묶음 · 등급 · 길이 상한과 프롬프트 공통 블록은 [VFXSFXGuide](../../../Assets/VFXSFXGuide.md) 「SFX 카테고리별 가이드」의 「1. 유닛 공격 사운드」, 유닛마다 눈에 보이는 공격 동작은 [UnitAttackProfile](../../../Assets/UnitAttackProfile.md) 2 · 3절이다.

> **옮긴 방식:** 아래 「원본」은 asset-prompt-crafter가 작성한 원문(2026-11-03)을 그대로 옮긴 것이다. 영어 프롬프트 블록은 한 글자도 바꾸지 않았다. 바뀐 곳은 두 군데뿐이다 — 원문 제목 줄(이 문서 제목으로 대체)과 맨 끝 「확정 사항」의 EagleArcher 항목 표기(아래 해당 줄의 주석 참조).

---

## 원본 (asset-prompt-crafter, 2026-11-03)

모든 영어 프롬프트는 VFXSFXGuide 「프롬프트 공통 블록」 원문 위에 채워 넣은 것이며 게임 이름은 쓰지 않았다. 동작 근거: 총기 3종, Tank/CannonCart, StreamSpirit, 몸 재질, FoxMagician 구체 색, MushroomBomber 컨셉은 2026-11-03 사용자 답변, 나머지는 UnitAttackProfile.md 2·3절. Unity AI는 모노·길이·dBFS·주파수 지시를 따르지 않으므로 생성 뒤 `Hexiege/Audio/Normalize Selected SFX` 보정을 거친다. 전용 소리는 종족 색 미정이던 시점에 공통 블록의 `Neutral:` 줄을 뺐다(묶음 소리에만 넣음). BloomFairy는 Tone 줄만 예외.

## 목차

| # | 파일명 | 상한(초) | 대상 유닛 | 종류 |
|---|---|---|---|---|
| 1 | `sfx_gun_light_attack.wav` | 0.20 | Assault | 공용 |
| 2 | `sfx_gun_medium_attack.wav` | 0.40 | Pistoleer | 공용 |
| 3 | `sfx_gun_heavy_attack.wav` | 0.70 | Sniper | 공용 |
| 4 | `sfx_blade_light_attack.wav` | 0.45 | RabbitTrickster, LittleKnight | 공용 (LittleKnight 샘플 재사용 후보, 대체안만 작성) |
| 5 | `sfx_blade_medium_attack.wav` | 0.45 | LionKnight, SpearMan | 공용 |
| 6 | `sfx_strike_light_attack.wav` | 0.20 | FlameSpirit (6연타) | 공용 |
| 7 | `sfx_strike_medium_attack.wav` | 0.45 | EmberSpirit, TideSpirit, DustSpirit | 공용 |
| 8 | `sfx_strike_heavy_attack.wav` | 0.70 | BoulderSpirit, BearGuard, RhinoBreaker | 공용 |
| 9 | `sfx_cannon_attack.wav` | 1.0 | Tank, CannonCart, Human 방어 타워 | 전용 |
| 10 | `sfx_infernospirit_attack.wav` | 0.9 | InfernoSpirit | 전용 |
| 11 | `sfx_quakespirit_attack.wav` | 1.0 | QuakeSpirit | 전용 |
| 12 | `sfx_torrentspirit_attack.wav` | 1.0 | TorrentSpirit | 전용 |
| 13 | `sfx_foxmagician_attack.wav` | 0.8 | FoxMagician | 전용 |
| 14 | `sfx_mushroombomber_attack.wav` | 0.5 | MushroomBomber | 전용 (독가스 쉭 포함 재작성본) |
| 15 | `sfx_bloomfairy_cast.wav` | 0.8 | BloomFairy | 전용 (톤 예외) |
| 16 | `sfx_battleaxe_attack.wav` | 0.6 | BattleAxe | 전용 |
| 17 | `sfx_streamspirit_attack.wav` | 0.5 | StreamSpirit | 전용 |
| 18 | `sfx_runespire_attack.wav` | 0.8 | RuneSpire (2단계 타워) | 전용, 저장 경로 `Audio/SFX/Buildings/` |

---

## 공용 소리

### 1. sfx_gun_light_attack.wav (총기 약, 상한 0.20초)
- 대상: Assault. 겨누고 한 번 공격에 한 발. 연사처럼 반복되지만 소리는 한 발. 겨누는 동작 소리는 넣지 않는다.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: light-tier firearm group (old-fashioned black-powder gun, rifle), a single shot fired after aiming; one shot per attack, heard as one shot among repeated shots. Very short, dry and light "tak" thump with a rounded soft attack; almost no tail. Avoid a sharp high-pitched crack or click.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.20 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_gun_light_attack.wav
```
[한글 요약] 옛날 화약 소총의 아주 짧고 건조한 한 발. 둥근 「탁」, 꼬리 거의 없음, 날카로운 crack 금지.
체크포인트: ① 보정 뒤 0.20초 이내인가 ② 0.33초 간격으로 여러 번 겹쳐도 한 발씩 구분되는가 ③ 1~4kHz가 날카롭게 쏘지 않는가

### 2. sfx_gun_medium_attack.wav (총기 중, 상한 0.40초)
- 대상: Pistoleer. 겨누고 권총 한 발.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: medium-tier firearm group (old-fashioned black-powder pistol), a single shot fired after aiming; one shot per attack. A full-bodied powder "pop" burst with a short fading tail. Round, thumpy body with mid-low weight; avoid a sharp high-pitched crack or click.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.40 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_gun_medium_attack.wav
```
[한글 요약] 옛날 화약 권총 한 발. 화약이 터지는 둥글고 몸통 있는 소리 + 짧은 꼬리. 날카로운 crack 금지(1차 피드백).
체크포인트: ① 0.40초 이내 ② 듣기 싫은 날카로운 고음이 없는가 ③ 총기 약보다 확실히 무겁고 강보다 가벼운가

### 3. sfx_gun_heavy_attack.wav (총기 강, 상한 0.70초)
- 대상: Sniper. 겨누고 저격총 한 발.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: heavy-tier firearm group (old-fashioned black-powder long rifle), a single shot fired after aiming; one shot per attack. One heavy powder blast with a resonant tail that decays naturally; strong low-frequency weight but with solid low-mid body too, not only deep bass, and the tail should not drop away too abruptly. Avoid a sharp high-pitched crack.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.70 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_gun_heavy_attack.wav
```
[한글 요약] 옛날 화약 장총의 묵직한 한 발 + 울림 꼬리. 저음 비중이 크되 중저음 몸통도 있고 꼬리가 너무 빨리 끊기지 않게(Bear 1차 교훈).
체크포인트: ① 0.70초 이내 ② 중과 구분되는 무게인가 ③ 저음만 쏠려 소형 스피커에서 안 들리지 않는가

### 4. sfx_blade_light_attack.wav (날붙이 약, 상한 0.45초) — 재사용 후보
- 대상: RabbitTrickster(단검 찌르기), LittleKnight(찌르고 휘두르는 2타). LittleKnight 샘플 `sfx_littleknight_attack.wav` 재사용이 우선이며, 이 프롬프트는 청취 뒤 대체가 필요할 때만 쓴다(Q-5).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: light-tier bladed weapon group (dagger / short sword), a quick thrust or swing. Light fast whoosh of air with a small metallic accent at the end.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.45 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_blade_light_attack.wav
```
[한글 요약] 단검·짧은 검의 빠른 찌르기/휘두름 바람 소리 + 끝에 작은 금속감.
체크포인트: ① LittleKnight 샘플과 비교해 더 나은가(아니면 재사용 유지) ② 0.45초 이내 ③ 가벼워도 장난감처럼 들리지 않는가

### 5. sfx_blade_medium_attack.wav (날붙이 중, 상한 0.45초)
- 대상: LionKnight(장검 연속 베기, 타격마다 재생), SpearMan(창 찌르기).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: medium-tier bladed weapon group (long sword slash / spear thrust), one strike. A longer, heavier whoosh of a long blade cutting the air, with slightly more metallic ring than the light tier. It must work as one hit among repeated hits.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.45 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_blade_medium_attack.wav
```
[한글 요약] 긴 날의 휘두름·베기 바람 소리 + 약보다 금속 울림이 조금 더. 연타 중 한 번으로 들려야 함.
체크포인트: ① 0.45초 이내 ② 약과 구분되는 길이감·울림인가 ③ 창 찌르기에도 어색하지 않은가(Q-4 청취)

### 6. sfx_strike_light_attack.wav (타격 약, 상한 0.20초)
- 대상: FlameSpirit(격투선수의 연속 주먹질 6타, 타격마다 재생).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: light-tier strike group (arm / fist), a short quick jab. Tiny dry punchy thump with a snappy attack, almost no tail. It will be played repeatedly in a rapid sequence, so each one must be very short and clearly separate.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.20 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_strike_light_attack.wav
```
[한글 요약] 연타 중 하나로 들리는 아주 짧은 잽. 꼬리 거의 없음.
체크포인트: ① 0.20초 이내(보정 뒤 무음 잘린 길이) ② 6번 연속 재생 시 뭉개지지 않는가 ③ 같은 클립 3개 한도 안에서도 타격이 들리는가

### 7. sfx_strike_medium_attack.wav (타격 중, 상한 0.45초)
- 대상: EmberSpirit(불 몸), TideSpirit, DustSpirit(바위 몸) — 팔로 내려치기 1타. 재질이 섞이므로 중립으로 만들고 청취 뒤 분리 판단(Q-8).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: medium-tier strike group (arm smash), a single downward arm blow. A dull, solid thud with mid-low body and a short natural decay. No material-specific sound (not metallic, not fiery, not watery).
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.45 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_strike_medium_attack.wav
```
[한글 요약] 한 번 내려치는 둔탁한 타격. 중저음 몸통 + 짧은 자연 감쇠, 재질색 없음.
체크포인트: ① 0.45초 이내 ② 불/바위/물 몸 세 유닛에 모두 어색하지 않은가 ③ 기존 Ember 샘플과 비교(Q-6)

### 8. sfx_strike_heavy_attack.wav (타격 강, 상한 0.70초)
- 대상: BoulderSpirit(바위 몸), BearGuard(나무 방패 옆 휘두르기), RhinoBreaker(제자리 훅).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: heavy-tier strike group (huge body / shield / hook), one massive blow. A weighty collision with strong low-frequency impact but also solid low-mid body, followed by a short rumble that decays naturally rather than cutting off quickly. No material-specific sound.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.70 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- Neutral: no race-specific color (for weapon-group sounds).
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_strike_heavy_attack.wav
```
[한글 요약] 큰 몸·방패·훅의 묵직한 충돌, 저음 + 중저음 몸통, 꼬리가 너무 빨리 사그라들지 않게(Bear 1차 교훈).
체크포인트: ① 0.70초 이내 ② 저음만 쏠려 소형 스피커에서 사라지지 않는가 ③ 중과 확실히 구분되는가

---

## 전용 소리

### 9. sfx_cannon_attack.wav (포격 공용, 상한 1.0초)
- 대상: Tank, CannonCart, Human 방어 타워(2단계). 포신 금속, 반동 동작 없음, 「큰 전차포」 느낌. 발사음만.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a large tank cannon firing a shell from a metal gun barrel. One deep, heavy gun blast with a metallic bore ring and a rumbling tail that decays naturally; strong low-frequency weight with solid low-mid body, not only deep bass. Firing sound only, no shell flight, no impact, no recoil movement sound.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 1.0 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_cannon_attack.wav
```
[한글 요약] 금속 포신의 큰 전차포 발사 한 발. 묵직한 폭발 + 금속 울림 + 자연 감쇠 꼬리. 착탄·반동 소리 없음.
체크포인트: ① 1.0초 이내 ② Tank 여러 대가 겹쳐도 뭉개지지 않는가 ③ 총기 강(Sniper)과 확실히 다른 덩치감인가

### 10. sfx_infernospirit_attack.wav (상한 0.9초)
- 대상: InfernoSpirit. 불덩이 모으기(차지)는 무음, 화염구 발사 순간만.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a big fireball launched forward by a fire elemental. A heavy fiery whoosh burst as the fireball is released and flies out, with crackling flame texture and a short fading fiery tail. Launch only: no gathering/charging sound, no impact.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.9 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_infernospirit_attack.wav
```
[한글 요약] 화염구가 손을 떠나는 순간의 묵직한 불 휘익 + 타닥거리는 불 질감. 차지·착탄 소리 없음.
체크포인트: ① 0.9초 이내 ② 시작이 바로 터지는가(앞 무음/상승 없음) ③ 차징 소리가 섞이지 않았는가

### 11. sfx_quakespirit_attack.wav (상한 1.0초)
- 대상: QuakeSpirit. 하늘로 뛰어올라 땅을 내려치기. 내려치는 충격 중심, 도약 바람 소리는 넣지 않는다(사용자 결정).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a rock-bodied giant leaps into the air and smashes the ground. One massive ground slam: deep thunderous impact with stone crunch and cracking, followed by a short low rumble that decays naturally. Strong low-frequency weight with solid low-mid body.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 1.0 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_quakespirit_attack.wav
```
[한글 요약] 바위 몸이 뛰어올라 땅을 내려치는 큰 충격 + 돌 갈라지는 소리 + 낮은 울림. 도약 소리는 넣지 않음.
체크포인트: ① 1.0초 이내 ② 타격 순간에 소리 정점이 맞는가 ③ 타격 강 소리와 구분되는 「땅 울림」이 있는가

### 12. sfx_torrentspirit_attack.wav (상한 1.0초)
- 대상: TorrentSpirit. 물 몸. 양팔을 벌려 뒤로 젖혔다 앞으로 뻗으며 파도 발사. 파도가 생기는 순간.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a water-bodied spirit thrusts both arms forward and unleashes a big wave. A powerful surging water whoosh that swells quickly and rushes forward, with heavy splashing weight and a short watery tail. Release moment only: no gathering sound, no impact.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 1.0 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_torrentspirit_attack.wav
```
[한글 요약] 물 몸이 파도를 쏟아내는 묵직한 물살 휘몰아침. 빠르게 부풀어 앞으로 쏟아지는 느낌, 짧은 물 꼬리.
체크포인트: ① 1.0초 이내 ② 시작이 늦지 않고 파도 생성 순간에 맞는가 ③ StreamSpirit(물폭탄)과 구분되는가

### 13. sfx_foxmagician_attack.wav (상한 0.8초)
- 대상: FoxMagician. 지팡이로 차징한 푸른빛·보라빛 구체 발사. 차징은 무음.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a magician launches a glowing blue-and-purple energy orb forward from a staff. A punchy magical release: a quick mystical "whoomp" burst with a shimmering airy sweep and a short sparkling tail. Launch only: no charging sound, no impact.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.8 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_foxmagician_attack.wav
```
[한글 요약] 푸른·보라 마법 구체가 나가는 순간의 단단한 마법 발사음 + 반짝이는 공기 스윕. 차징·착탄 없음.
체크포인트: ① 0.8초 이내 ② 가볍고 반짝이기만 한 장난감 소리가 아닌가 ③ Inferno·Mushroom 발사음과 구분되는가

### 14. sfx_mushroombomber_attack.wav (상한 0.5초) — 독가스 쉭 포함 재작성본
- 대상: MushroomBomber. 버섯 컨셉, 공을 던지듯 투척. 던진 순간만 재생.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a mushroom-themed character throws a poison mushroom bomb overhand, like throwing a ball. A short throw: a quick air whoosh of the throwing arm, then a soft squishy mushroom "pomf" release, then a brief soft "pssh" of toxic gas leaking out of the mushroom as it leaves the hand. The gas hiss is short, airy and muffled, with its energy kept in the low-mid and the very high range, not a sharp or piercing hiss in the 1-4 kHz range. Light and bouncy yet with solid body. Throw moment only: no impact, no explosion.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.5 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_mushroombomber_attack.wav
```
[한글 요약] 버섯 독 폭탄을 공 던지듯 던지는 순간의 소리. 팔을 휘두르는 짧은 바람, 말랑한 버섯 「폼」, 짧게 새는 독가스 「쉭」 순. 쉭 소리는 짧고 먹먹하게, 1~4kHz가 날카롭게 찌르지 않게. 착탄·폭발 없음.
체크포인트: ① 보정 뒤 0.5초 이내인가, 바람·폼·쉭이 모두 들리는가 ② 쉭 소리가 귀를 찌르지 않는가(1~4kHz가 튀면 Pistoleer 1차와 같은 문제) ③ 독가스 쉭이 던지는 느낌을 가리지 않는가(가리면 더 짧고 약하게 재생성)

### 15. sfx_bloomfairy_cast.wav (상한 0.8초) — 톤 예외
- 대상: BloomFairy. 힐 시전 순간 1회. 맑은 종소리 3~4음 상승 + 꽃잎 바람. `sfx_bloomfairy_heal.wav`(지속음)와 별개.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a fairy casts a healing spell once. Three to four clear, gentle bell chimes rising in pitch, accompanied by a soft rustle of flower petals in a light breeze. Short and not too long.
- Tone: exception to the usual weighty style — clear, soft and gentle healing feel, bright but not harsh or shrill. Still readable when other sounds overlap. Short tail.
- Length: under 0.8 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_bloomfairy_cast.wav
```
[한글 요약] 부드럽게 올라가는 맑은 종소리 3~4음 + 꽃잎 바람, 짧게. 톤 예외 케이스.
체크포인트: ① 0.8초 이내 ② 음이 3~4개로 세어지는가 ③ 고음이 날카롭거나 길게 늘어지지 않는가

### 16. sfx_battleaxe_attack.wav (상한 0.6초)
- 대상: BattleAxe. 큰 반원을 그리며 휘두르기. 도끼로 진행(사용자 결정).

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a heavy battle axe swung in a wide half-circle arc. A big, weighty sweeping whoosh with a heavy cutting swish, rising then passing through, ending with a short low-mid thud of the blade connecting. One swing only.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.6 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_battleaxe_attack.wav
```
[한글 요약] 큰 반원 휘두름의 묵직한 바람 가르기 + 끝에 짧은 중저음 타격감.
체크포인트: ① 0.6초 이내 ② 날붙이 중보다 확실히 크고 무거운가 ③ 반원 휘두름의 움직임이 귀에 그려지는가

### 17. sfx_streamspirit_attack.wav (상한 0.5초)
- 대상: StreamSpirit. 양손을 앞으로 모은 뒤 물폭탄 발사.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a water spirit fires a water bomb forward from both hands held together in front. A compact, punchy watery launch: a quick water-balloon "bloop" burst with a short splashing whoosh. Launch only: no impact, no splash landing.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.5 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Units/
- File name: sfx_streamspirit_attack.wav
```
[한글 요약] 두 손에서 물폭탄이 나가는 순간. 짧고 단단한 물 「블룹」 + 짧은 물살 휘익.
체크포인트: ① 0.5초 이내 ② Torrent 파도와 구분되는가(작고 빠름) ③ 주기 1.5초에서 반복돼도 거슬리지 않는가

### 18. sfx_runespire_attack.wav (상한 0.8초) — 2단계
- 대상: Spirit RuneSpire. 크리스털 높이에서 대상에게 번개. 저장 경로만 Buildings.

[English Prompt]
```
Create a short game sound effect as a wav file for a cartoon-style isometric mobile RTS.
- Sound: a magical crystal tower strikes a target with a bolt of lightning. A sharp electric zap and crackle with a solid low-mid thump underneath so it is not thin, plus a short electric fade. One bolt only.
- Tone: cartoon visuals but weighty and solid, not toy-like. Clear and punchy so it stays
  audible when many sounds overlap. Short tail.
- Length: under 0.8 seconds of audible sound. No leading silence.
- Do not include charge-up / wind-up sound. Launch (firing) sound only for ranged units.
- wav, mono, 44100 Hz, 16-bit.
- Save path: Assets/_Project/Audio/SFX/Buildings/
- File name: sfx_runespire_attack.wav
```
[한글 요약] 크리스털 타워의 번개 한 줄기. 날카로운 전기 지직 + 밑에 단단한 중저음 + 짧은 전기 꼬리.
체크포인트: ① 0.8초 이내 ② 고음만 얇게 쏘지 않는가 ③ 타워 공격 간격에서 반복돼도 괜찮은가(2단계 때 주기 확인)

---

확정 사항(2026-11-03):

- 전용 소리 종족 색 없음
- 날붙이 짧은 금속 울림
- BattleAxe 도끼
- Quake 도약 소리 없음
- Mushroom 독가스 쉭 포함
- EagleArcher: 활 전용 파일 없이 타격 약 공용 소리(`sfx_strike_light_attack.wav`) 사용 — **사용자 확정(2026-11-03 「EagleArcher 약으로」)**. 쏘는 순간 재생하며 착탄음이 아니다. 이번 묶음에 활 프롬프트 없음 *(2026-11-03 갱신 — 직전 표기 「— 메인 세션 제안, 사용자 확인 대기」. [Research 11-17](Research.md#11-17-eaglearcher-decision))*

*(원문 표기: 「EagleArcher는 활 전용 파일 없이 타격 약 공용 소리 사용 제안(사용자 확인 대기) — 이번 묶음에 활 프롬프트 없음」. 원문은 한 줄 나열이었고, 이 문서에서는 항목별 줄로 나누었다.)*

*(2026-11-03 추가 — 위 「목차」 6번 줄의 사용 유닛 칸은 원문 그대로 FlameSpirit만 적혀 있다. 확정에 따라 이 파일은 EagleArcher도 함께 쓴다(쏘는 순간). 6번 영어 프롬프트는 FlameSpirit 연타 기준으로 쓰였고 바꾸지 않았다 — EagleArcher 발사에도 어울리는지는 생성 뒤 청취로 본다.)*
