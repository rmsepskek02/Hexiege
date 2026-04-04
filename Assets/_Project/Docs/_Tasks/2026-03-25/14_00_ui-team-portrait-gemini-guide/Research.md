# Research — 팀 구분 UI 초상화 Gemini 가이드 문서

**작성일:** 2026-03-25
**작업 목적:** 3종족 건물/유닛의 팀 구분 UI 에셋(초상화 아이콘) 제작을 위한 Gemini 이미지 생성 가이드 문서 작성

---

## 1. 현재 상태 파악

### 1-1. 기존 UI 초상화 에셋 (인간계, 완료)

| 파일 경로 | 용도 |
|-----------|------|
| `Sprites/Units/Pistoleer/pistoleer_portrait_red.png` | 피스톨러 레드팀 생산패널 버튼 |
| `Sprites/Units/Pistoleer/pistoleer_portrait_blue.png` | 피스톨러 블루팀 생산패널 버튼 |
| `Sprites/Units/Assult/assault_portrait_red.png` | 어설트 레드팀 생산패널 버튼 |
| `Sprites/Units/Assult/assault_portrait_blue.png` | 어설트 블루팀 생산패널 버튼 |
| `Sprites/Units/Sniper/sniper_portrait_red.png` | 스나이퍼 레드팀 생산패널 버튼 |
| `Sprites/Units/Sniper/sniper_portrait_blue.png` | 스나이퍼 블루팀 생산패널 버튼 |

| 파일 경로 | 용도 |
|-----------|------|
| `Sprites/Buildings/bld_castle_red.png` | 케슬 레드팀 건물 배치 UI |
| `Sprites/Buildings/bld_castle_blue.png` | 케슬 블루팀 건물 배치 UI |
| `Sprites/Buildings/bld_barracks_red.png` | 배럭 레드팀 건물 배치 UI |
| `Sprites/Buildings/bld_barracks_blue.png` | 배럭 블루팀 건물 배치 UI |
| `Sprites/Buildings/bld_mining_post.png` | 미닝포스트 (팀 무관) |

### 1-2. 기존 UI 슬롯 에셋

| 파일 경로 | 용도 |
|-----------|------|
| `Sprites/UI/Slots/ui_slot_icon_light.png` | 밝은 배경 아이콘 슬롯 프레임 |
| `Sprites/UI/Slots/ui_slot_icon_dark.png` | 어두운 배경 아이콘 슬롯 프레임 |
| `Sprites/UI/Slots/ui_slot_queue.png` | 생산 대기열 슬롯 |

### 1-3. UI 시스템 연동 방식

`ProductionPanelUI.cs`:
- `UpdateButtonPortraits(TeamId team)` — Show(barracks) 호출 시 팀에 맞는 유닛 초상화 버튼 Image 교체
- `UnitPortraitSet { pistoleer, assault, sniper }` — 블루/레드 각각 Inspector 연결
- `_pistoleerButtonPortrait`, `_assaultButtonPortrait`, `_sniperButtonPortrait` Image 교체

→ **신규 종족 유닛도 동일 패턴 적용 예정**: 팀별 초상화 스프라이트 Inspector 연결

---

## 2. 신규 제작 필요 에셋 목록

### 2-1. 유닛 초상화 (생산패널 버튼 아이콘)

**정령계 (Elemental)**

| 유닛 | 레드팀 파일명 | 블루팀 파일명 |
|------|-------------|-------------|
| Fire Spirit Stage 1 (Tier 1) | `firespirit1_portrait_red.png` | `firespirit1_portrait_blue.png` |
| Fire Spirit Stage 2 (Tier 2) | `firespirit2_portrait_red.png` | `firespirit2_portrait_blue.png` |
| Fire Spirit Stage 3 (Tier 3) | `firespirit3_portrait_red.png` | `firespirit3_portrait_blue.png` |

**초월계 (Transcendent)**

| 유닛 | 레드팀 파일명 | 블루팀 파일명 |
|------|-------------|-------------|
| Bear Warrior (Tier 1) | `bearwarrior_portrait_red.png` | `bearwarrior_portrait_blue.png` |
| Lion Knight (Tier 2) | `lionknight_portrait_red.png` | `lionknight_portrait_blue.png` |
| Fox Mage (Tier 3) | `foxmage_portrait_red.png` | `foxmage_portrait_blue.png` |

### 2-2. 건물 스프라이트 (건물 배치 UI 버튼)

**정령계 (Elemental)**

| 건물 | 레드팀 파일명 | 블루팀 파일명 |
|------|-------------|-------------|
| Spirit Nexus (본기지) | `bld_spiritnexus_red.png` | `bld_spiritnexus_blue.png` |
| Summoning Altar (배럭) | `bld_summoningaltar_red.png` | `bld_summoningaltar_blue.png` |
| Mana Rift (채굴소) | `bld_manarift.png` (팀 무관) | — |

**초월계 (Transcendent)**

| 건물 | 레드팀 파일명 | 블루팀 파일명 |
|------|-------------|-------------|
| Ancient Den (본기지) | `bld_ancientden_red.png` | `bld_ancientden_blue.png` |
| War Totem (배럭) | `bld_wartotem_red.png` | `bld_wartotem_blue.png` |
| Nature Shrine (채굴소) | `bld_natureshrine.png` (팀 무관) | — |

---

## 3. 기존 초상화 스타일 분석

기존 초상화(pistoleer/assault/sniper)를 참조해 확인해야 할 항목:
- **구성**: 캐릭터 상반신 or 전신 클로즈업 + 팀 색상 **테두리(Border/Frame)**
- **팀 구분**: 레드팀 = 빨간 테두리, 블루팀 = 파란 테두리
- **배경**: 단색 또는 그라디언트 배경 (어두운 톤)
- **해상도**: 통일 필요 (가이드 문서에서 256×256px 또는 512×512px 지정)

> ⚠️ 기존 초상화 이미지가 참조 이미지로 첨부 권장 (Gemini 스타일 일치용)

---

## 4. 가이드 문서 요구사항 (사용자 지정)

1. **블루팀 + 레드팀 동시 제작** — 각 에셋마다 두 팀 버전 모두 프롬프트 포함
2. **해상도 통일** — 모든 UI 초상화: 512×512px (1:1 정사각형)
3. **Positive + Negative 프롬프트 한 블록에 묶어서** 작성
4. **기존 에셋 수정/참조 시** — 참조 이미지 첨부 권장 문구 포함

---

## 5. 기존 가이드 문서 위치

`Assets/_Project/Docs/_Tasks/2026-03-24/14_00_new-faction-assets/GeminiGuide.md`
→ 3D 에셋(Meshy AI) 용도. 신규 가이드는 **2D UI 전용** 별도 문서로 작성 예정.
