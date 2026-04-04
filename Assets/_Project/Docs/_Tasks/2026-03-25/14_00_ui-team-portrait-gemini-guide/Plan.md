# Plan — 팀 구분 UI 초상화 Gemini 가이드 문서

**작성일:** 2026-03-25
**산출물:** `Assets/_Project/Docs/_Tasks/2026-03-25/14_00_ui-team-portrait-gemini-guide/GeminiGuide_UI.md`

---

## 작업 범위

- 신규 2종족(정령계, 초월계) 유닛 초상화 6종 × 2팀 = 12개
- 신규 2종족 건물 스프라이트 4종 × 2팀 + 2종 팀 무관 = 10개
- **총 22개 에셋**에 대한 Gemini 이미지 생성 요청 프롬프트

---

## 가이드 문서 구조

```
GeminiGuide_UI.md
├── [공통 원칙]
│   ├── 해상도 규칙 (512×512px)
│   ├── 스타일 규칙 (기존 초상화 스타일 일치)
│   ├── 팀 색상 규칙 (레드 테두리 / 블루 테두리)
│   ├── 프롬프트 형식 규칙 (Positive + Negative 묶음)
│   └── 참조 이미지 첨부 규칙
│
├── [정령계 유닛 초상화]
│   ├── Fire Spirit Stage 1 (레드팀 + 블루팀)
│   ├── Fire Spirit Stage 2 (레드팀 + 블루팀)
│   └── Fire Spirit Stage 3 (레드팀 + 블루팀)
│
├── [초월계 유닛 초상화]
│   ├── Bear Warrior (레드팀 + 블루팀)
│   ├── Lion Knight (레드팀 + 블루팀)
│   └── Fox Mage (레드팀 + 블루팀)
│
├── [정령계 건물 스프라이트]
│   ├── Spirit Nexus (레드팀 + 블루팀)
│   ├── Summoning Altar (레드팀 + 블루팀)
│   └── Mana Rift (팀 무관)
│
└── [초월계 건물 스프라이트]
    ├── Ancient Den (레드팀 + 블루팀)
    ├── War Totem (레드팀 + 블루팀)
    └── Nature Shrine (팀 무관)
```

---

## 프롬프트 블록 형식

각 에셋마다 아래 형식으로 블루팀과 레드팀을 한 섹션 내에 작성:

```
### [에셋명] — 블루팀 / 레드팀

**참조 이미지:** [기존 유사 에셋 파일 첨부 권장]

**공통 컨셉:** ...

[블루팀]
Positive: ...
Negative: ...
저장 파일명: xxx_portrait_blue.png

[레드팀]
Positive: ...
Negative: ...
저장 파일명: xxx_portrait_red.png
```

---

## 공통 원칙 세부 내용

### 해상도
- **유닛 초상화:** 512×512px (1:1)
- **건물 스프라이트:** 512×512px (1:1)
- 모든 에셋 동일 해상도 — Unity Sprite Import 설정 통일 목적

### 스타일 규칙
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 스타일
- 배경: 어두운 단색 또는 그라디언트 (기존 초상화 스타일 일치)
- 캐릭터 구도: 상반신 클로즈업 (버스트 샷) — 얼굴~가슴 범위
- 균일하고 부드러운 조명 (드라마틱 그림자 없음)

### 팀 구분 색상 규칙
- **블루팀:** 초상화 외곽 테두리 = 파란색 (#1E90FF), 악센트 요소에 파란 빛
- **레드팀:** 초상화 외곽 테두리 = 빨간색 (#FF3030), 악센트 요소에 빨간 빛
- 테두리 두께: 두껍고 뚜렷하게 (얇은 선 금지)

### 참조 이미지 첨부 규칙
- 기존 에셋을 참조/수정하는 경우 → **반드시 참조 이미지 첨부** 권장 문구 포함
- 유닛 초상화: 기존 `pistoleer_portrait_red.png` 또는 `pistoleer_portrait_blue.png` 첨부
- 건물 스프라이트: 기존 `bld_barracks_red.png` 또는 `bld_castle_blue.png` 첨부

---

## 파일 저장 경로 규칙

```
Assets/_Project/Sprites/
├── Units/
│   ├── FireSpirit/
│   │   ├── firespirit1_portrait_red.png
│   │   ├── firespirit1_portrait_blue.png
│   │   ├── firespirit2_portrait_red.png
│   │   ├── firespirit2_portrait_blue.png
│   │   ├── firespirit3_portrait_red.png
│   │   └── firespirit3_portrait_blue.png
│   ├── BearWarrior/
│   │   ├── bearwarrior_portrait_red.png
│   │   └── bearwarrior_portrait_blue.png
│   ├── LionKnight/
│   │   ├── lionknight_portrait_red.png
│   │   └── lionknight_portrait_blue.png
│   └── FoxMage/
│       ├── foxmage_portrait_red.png
│       └── foxmage_portrait_blue.png
└── Buildings/
    ├── bld_spiritnexus_red.png
    ├── bld_spiritnexus_blue.png
    ├── bld_summoningaltar_red.png
    ├── bld_summoningaltar_blue.png
    ├── bld_manarift.png
    ├── bld_ancientden_red.png
    ├── bld_ancientden_blue.png
    ├── bld_wartotem_red.png
    ├── bld_wartotem_blue.png
    └── bld_natureshrine.png
```

---

## 에이전트 위임

`asset-prompt-crafter` 에이전트에게 위임:
- Research.md + Plan.md 공유
- 기존 3D 가이드 문서(`14_00_new-faction-assets/GeminiGuide.md`) 참조 — 종족 컨셉 정보 포함
- 기존 초상화 파일 경로 공유 — 스타일 참조용

---

## 주의사항

- 건물 스프라이트는 이소메트릭 뷰 (55도) — 초상화와 다른 앵글
- 팀 무관 에셋(Mana Rift, Nature Shrine)은 단일 버전만 제작
- 기존 인간계 초상화와 스타일 일관성 유지 필수
