# 신규 종족 3D 에셋 — Gemini 요청 가이드

> 정령계(Elemental) + 초월계(Transcendent) 2개 종족 에셋 제작용
> 각 섹션의 요청 텍스트를 **복사-붙여넣기**해서 Gemini에 바로 사용 가능

**작성일:** 2026-03-24
**에셋 수:** 총 12개 (종족 2 × 건물 3 + 유닛 3)
**파이프라인:** Gemini → Nano Banana(이미지) → Meshy AI(3D) → Unity FBX


## 에셋 목록

| 종족 | 에셋 | 역할 | 파일명 |
|------|------|------|--------|
| 정령계 | Spirit Nexus | Castle (본기지) | `Building_SpiritNexus` |
| 정령계 | Summoning Altar | Barracks (유닛 생산) | `Building_SummoningAltar` |
| 정령계 | Mana Rift | MiningPost (자원 수집) | `Building_ManaRift` |
| 정령계 | Fire Spirit I | Tier1 유닛 | `Unit_FireSpirit_Stage1` |
| 정령계 | Fire Spirit II | Tier2 유닛 | `Unit_FireSpirit_Stage2` |
| 정령계 | Fire Spirit III | Tier3 유닛 | `Unit_FireSpirit_Stage3` |
| 초월계 | Ancient Den | Castle (본기지) | `Building_AncientDen` |
| 초월계 | War Totem | Barracks (유닛 생산) | `Building_WarTotem` |
| 초월계 | Nature Shrine | MiningPost (자원 수집) | `Building_NatureShrine` |
| 초월계 | Bear Warrior | Tier1 유닛 | `Unit_BearWarrior` |
| 초월계 | Lion Knight | Tier2 유닛 | `Unit_LionKnight` |
| 초월계 | Fox Mage | Tier3 유닛 | `Unit_FoxMage` |


## 공통 원칙 (모든 요청에 포함)

Gemini에게 요청할 때 아래 3가지를 **반드시** 지정한다.

```
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
  (예: 어떤 게임의 어떤 캐릭터, 어떤 키워드로 검색하면 좋은지)
- Meshy AI image-to-3D 변환에 최적화
```

### 이미지 공통 조건

| 조건 | 이유 |
|------|------|
| 배경: 순수 흰색 | Meshy AI 배경 분리 정확도 향상 |
| 건물: 55도 이소메트릭 뷰 | 게임 카메라 방향과 일치 |
| 유닛: 정면뷰 + 대기 포즈 | T-pose는 Meshy AI Animate 탭에서 설정 |
| 균일하고 부드러운 조명 | 강한 그림자가 텍스처에 구워지는 현상 방지 |
| 전신/전체 보이게 | 일부 잘리면 3D 변환 시 왜곡 |
| 명확한 실루엣 | 복잡한 외곽선은 메시 엉킴 |
|레드팀과 블루팀을 구분할 수 있는 부분이 반드시 필요함 | 3D 모델에서 팀 색상 머티리얼을 적용할 영역 확보 |
| 해상도/비율: 1:1 (1024px 이상) | Meshy AI 최적화 및 모바일 텍스처 표준 준수 |

---

## 정령계 (Elemental) 건물 에셋

**종족 아트 방향**: 원소 에너지 기반 마법 구조물. 반투명 결정체 + 빛나는 룬 문양 + 원소별 색상. 유기적이고 신비로운 외형. 중력을 거스르는 듯한 부유 요소.

---

### Building 1 — Spirit Nexus (본기지)

**컨셉**: 정령계 본거지. 거대한 마법 수정탑을 중심으로 원소 에너지가 소용돌이치는 신전. 인간계 Castle보다 훨씬 신비롭고 웅장한 느낌. 여러 색상의 빛이 방출됨.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 건물 (본기지, 3D 게임 에셋)
- 이름/설명: Spirit Nexus — 정령계의 본기지 건물. 거대한 마법 수정탑을 중심으로 원소 에너지가 소용돌이치는 신전
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 정령계 (원소 마법 기반, 신비로운 분위기)
- 비주얼 컨셉:
  - 중앙에 빛나는 거대 수정/크리스탈 타워
  - 주변에 작은 결정체들이 원형 배치
  - 파랑/보라/금색 빛이 방사
  - 공중에 떠 있는 룬 문양 파편
  - 돌 기단부 위에 마법 구조물 배치

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰 (Clash of Clans 카메라 기준)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette
- 건물 전체가 화면에 다 보이게

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```

---

### Building 2 — Summoning Altar (유닛 생산 건물)

**컨셉**: 정령을 소환하는 제단. 원형 소환 마법진이 새겨진 돌 제단. 중앙에서 원소 에너지가 솟아오르는 이펙트. 인간계 Barracks보다 작고 신비로운 느낌.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 건물 (유닛 생산 건물, 3D 게임 에셋)
- 이름/설명: Summoning Altar — 정령을 소환하는 마법 제단. 원형 소환 마법진이 바닥에 새겨져 있음
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 정령계 (원소 마법 기반)
- 비주얼 컨셉:
  - **형태 차별화:** 납작한 제단이 아닌, **수직으로 서 있는 '소환 게이트(Portal Gate)'** 형태
  - 허공에 떠 있는 고대 돌 고리(Ring) 또는 아치형 구조물
  - 고리 중앙에 소용돌이치는 차원문(Vortex) 형성
  - Spirit Nexus(타워), Mana Rift(바닥 수정)와 실루엣 완전 차별화

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette
- 건물 전체가 화면에 다 보이게

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```

---

### Building 3 — Mana Rift (자원 수집 건물)

**컨셉**: 대지에 균열이 생겨 마나 에너지가 솟아오르는 건물. 인간계 MiningPost와 같이 금광(자원 타일) 위에 건설. 지면 균열 + 에너지 방출 모습. 작고 낮은 구조.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 건물 (자원 수집 건물, 3D 게임 에셋)
- 이름/설명: Mana Rift — 마나 에너지를 추출하는 균열 구조물. 지면에 박힌 결정체 기둥들이 에너지를 흡수
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 정령계
- 비주얼 컨셉:
  - **형태 차별화:** 인공적인 기단부 제거, **날카롭고 불규칙한 '수정 클러스터'**
  - 지면을 뚫고 솟아오른 거친 수정 덩어리들 (Spikes)
  - 수정 사이로 마력 에너지가 스파크처럼 튐
  - 건물이 아니라 자연 현상처럼 보이도록 연출
  - 매우 낮고 뾰족뾰족한 실루엣

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette
- 건물 전체가 화면에 다 보이게

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```

---

## 정령계 (Elemental) 유닛 에셋

**종족 유닛 아트 방향**: 인간형 실루엣. 몸체가 원소 에너지로 이루어진 반투명 느낌. 원소별 대표색 (불=주황/빨강, 물=파랑/청록, 전기=노랑/하늘). 눈에 빛이 남.

> ⚠️ T-pose는 Meshy AI "Animate 탭 → Set T-pose"에서 설정. 프롬포트에 T-pose 불필요.

---

### Unit 1 — Fire Spirit Stage 1 (Tier 1, 불꽃 유년기)

**컨셉**: 진화형 불꽃 정령의 1단계. 어린아이 같은 비율의 작은 인간형. 귀엽지만 뜨거운 불꽃 에너지를 가짐.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 유닛 캐릭터 (3D 게임 에셋)
- 이름/설명: Fire Spirit Stage 1 — 1단계 불꽃 정령. 작은 체구의 인간형 소환수
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 정령계
- 비주얼 컨셉:
  - **1단계 (소형):** 2등신의 귀여운 비율, 단순한 형태의 주황색 불꽃 몸체.
  - 팔다리가 둥글둥글하고 불꽃이 부드럽게 타오름.
  - **팀 식별(중요):** 탑뷰에서 잘 보이는 **팀 색상 투구**와 **가슴 보석**을 착용.
    - **블루팀:** 파란색 돌로 만들어진 투구 + 가슴에 빛나는 파란색 보석.
    - **레드팀:** 붉은색 돌로 만들어진 투구 + 가슴에 빛나는 붉은색 보석.

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, game character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)
- 해상도: 1:1 비율, 1024x1024 해상도 (프롬프트에 포함)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

### Unit 2 — Fire Spirit Stage 2 (Tier 2, 불꽃 전사)

**컨셉**: 2단계 진화형. 청소년/성인 비율의 날렵한 전사형 정령. 불꽃이 더 거세지고 흑요석 갑주가 생기기 시작함.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 이름/설명: Fire Spirit Stage 2 — 2단계 불꽃 정령. 근육질의 날렵한 격투가형 정령
  - **2단계 (중형):** 일반적인 인간 비율, 근육질의 불꽃 몸체
  - 어깨와 팔뚝에 **팀 색상이 칠해진 굳은 마그마 조각(Armor)**이 붙어 있음
  - 머리카락처럼 타오르는 불꽃이 더 길고 날카로움
  - 주먹에 화염을 두르고 있는 격투 자세

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, game character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```


### Unit 3 — Fire Spirit Stage 3 (Tier 3, 불꽃 군주)

**컨셉**: 3단계 최종 진화형. 거대하고 위압적인 불꽃 마신. 무거운 흑요석 갑옷을 두르고 폭발적인 화염을 뿜어냄.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 유닛 캐릭터 (3D 게임 에셋)
- 이름/설명: Thunder Spirit — 정령계 Tier3 유닛. 광역 체인 번개 공격형 전기 정령. 가장 크고 위협적
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 정령계
- 비주얼 컨셉:
  - 세 정령 중 가장 큰 체구 (위압감 있는 실루엣)
  - 전신이 번쩍이는 전기/플라즈마로 이루어진 몸체
  - 하늘색~노란색~흰색 전류 색상
  - 온몸에서 번개 아크가 방전되는 이펙트
  - 눈이 강렬한 백색광으로 빛남
  - 두 팔에 전기 소용돌이 (공격 준비 포즈)

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, game character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘
```

---

## 초월계 (Transcendent) 건물 에셋

**종족 아트 방향**: 동물/식물 모티프 기반 자연 소재 건축물. 나무/돌/뼈로 만든 거친 질감. 동물 두개골/뿔 장식. 덩굴과 이끼가 덮인 고대 유적 느낌. 자연과 야생의 에너지.

---

### Building 4 — Ancient Den (본기지)

**컨셉**: 초월계 본거지. 거대한 동물 두개골과 뼈로 장식된 고대 요새. 자연석과 나무를 엮어 만든 웅장한 성채. 덩굴과 이끼가 가득.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 건물 (본기지, 3D 게임 에셋)
- 이름/설명: Ancient Den — 초월계 본기지. 동물 두개골과 뼈로 장식된 고대 야생 요새
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계 (의인화된 동식물, 야생 판타지)
- 비주얼 컨셉:
  - **컨셉 변경:** 통나무/뼈 요새 → **살아있는 거대 식물** 컨셉으로 변경
  - 거대한 세계수(World Tree)가 요새 역할을 함
  - 굵은 뿌리가 성벽을 이루고, 거대한 잎이 지붕을 형성
  - 나무줄기에 빛나는 이끼와 꽃이 피어남
  - **팀 식별:** 줄기에 박힌 **빛나는 수액 결정**이나 **거대 꽃의 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette
- 건물 전체가 화면에 다 보이게

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```

---

### Building 5 — War Totem (유닛 생산 건물)

**컨셉**: 전사를 훈련시키는 부족 토템 신전. 세 개의 토템 기둥이 삼각 배치된 훈련장. 동물 조각이 새겨진 토템. 인간계 Barracks의 야생 버전.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 이름/설명: Hunter Plant — 초월계 유닛 생산 건물. 거대한 식충 식물(Pitcher Plant)이 유닛을 뱉어내는 훈련소
  - 3개의 거대한 벌레잡이 통풀(Pitcher Plant)이 삼각형으로 배치된 구조
  - 식물의 입구가 바깥쪽 위를 향해 벌려져 있음 (유닛 생성구)
  - 덩굴이 바닥을 지탱하고 있음
  - **팀 식별:** 통풀 외부의 **빛나는 줄무늬**나 입구 주변 **점액의 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, tribal fantasy game asset, clean silhouette
- 건물 전체가 화면에 다 보이게

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```


### Building 6 — Fungal Node (자원 수집 건물)

**컨셉**: 자연의 힘으로 골드(자원)를 수집하는 신전에서 **거대한 발광 버섯 군락**으로 컨셉 변경. 금광 위에 자라나 자원을 빨아올리는 형태.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 건물 (자원 수집 건물, 3D 게임 에셋)
- 이름/설명: Nature Shrine — 초월계 자원 수집 건물. 고대 나무 뿌리가 땅의 자원을 흡수하는 소형 신전
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계
- 비주얼 컨셉:
  - **형태 차별화:** 그루터기/구근 형태에서 벗어나, **빛나는 버섯 군집** 형태로 변경
  - 땅에서 솟아난 여러 개의 거대한 발광 버섯들
  - 뿌리가 땅의 에너지를 흡수하는 모습
  - **팀 식별:** 버섯 갓 부분의 **빛나는 포자/반점 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 55도 이소메트릭 뷰
- 해상도: 1:1 비율, 1024x1024 해상도 (프롬프트에 포함)

[필수 요청 사항]
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

## 초월계 (Transcendent) 유닛 에셋

**종족 유닛 아트 방향**: 동물 특징(귀/꼬리/체형) + 판타지 갑옷/무기. 자연 소재감 (가죽/나무/뼈). 진화형 계열 — 체구와 장비가 단계별로 강화됨.

> ⚠️ T-pose는 Meshy AI "Animate 탭 → Set T-pose"에서 설정. 프롬포트에 T-pose 불필요.

---

### Unit 4 — Bear Warrior (Tier 1, 곰 전사)

**컨셉**: 가장 기본 초월계 유닛. 튼튼한 탱커. 직립 곰 체형. 가죽 갑옷과 큰 방패. 느리지만 높은 체력.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 유닛 캐릭터 (3D 게임 에셋)
- 이름/설명: Bear Warrior — 초월계 Tier1 유닛. 직립 곰 캐릭터 전사. 높은 체력의 탱커형
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계 (의인화된 동물, 짐승화 판타지)
- 비주얼 컨셉:
  - 직립 보행하는 의인화된 곰 체형 (두껍고 다부진 체구)
  - 곰 귀, 짧은 꼬리, 두꺼운 발 발톱
  - 투박한 가죽 갑옷과 뼈/금속 어깨 보호대
  - **무기 분리:** 맨손으로 싸우는 자세. 방패는 별도 에셋으로 제작.
  - 짙은 갈색/회색 곰 털 색상
  - **팀 식별:** 얼굴/몸의 **전투 문양(War Paint)**과 갑옷의 **가죽 끈/천 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 해상도: 1:1 비율, 1024x1024 해상도 (프롬프트에 포함)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, kemono fantasy character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

### Unit 4-1 — Bear Shield (곰 전사 방패)

**컨셉**: 곰 전사가 사용하는 튼튼한 원형 방패. 나무, 뼈, 가죽 등 자연 소재로 제작.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 무기 오브젝트 (3D 게임 에셋)
- 이름/설명: Bear Shield — 초월계 곰 전사의 방패. 나무와 뼈로 만든 원형 방패
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계
- 비주얼 컨셉:
  - 튼튼한 원형 나무 방패
  - 뼈나 금속 조각으로 테두리 보강
  - **팀 식별:** 방패 중앙의 **곰 발바닥 문양 색상** 또는 **가죽 손잡이 끈 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

### Unit 5 — Lion Knight (Tier 2, 사자 기사)

**컨셉**: 중간 등급 초월계 유닛. 공격형 돌진 딜러. 화려한 갑옷을 입은 사자 기사. Bear Warrior보다 날렵한 체형.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 유닛 캐릭터 (3D 게임 에셋)
- 이름/설명: Lion Knight — 초월계 Tier2 유닛. 화려한 갑옷의 사자 기사. 돌진 공격형 딜러
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계 (의인화된 동물, 짐승화 판타지)
- 비주얼 컨셉:
  - 직립 보행하는 의인화된 사자 체형 (**민첩한 검사 스타일**, 곰 전사보다 훨씬 날렵함)
  - 갈기가 투구/목 보호대 사이로 흘러내림
  - **가볍고 날렵한 경갑(Light Armor)** 또는 가죽 갑옷. (곰 전사의 판금 갑옷과 차별화)
  - **무기 분리:** 장검(Longsword)을 들 준비가 된 자세. 무기는 별도 에셋으로 제작.
  - 황금빛 사자 털 색상
  - **팀 식별:** 갑옷 위에 걸친 **천(Tabbard)**이나 어깨 보호대의 **보석 색상**으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 해상도: 1:1 비율, 1024x1024 해상도 (프롬프트에 포함)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, kemono fantasy character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

### Unit 5-1 — Lion Sword (사자 기사 장검)

**컨셉**: 사자 기사가 사용하는 화려한 장검. 사자 갈기나 머리 문양이 장식된 손잡이.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 무기 오브젝트 (3D 게임 에셋)
- 이름/설명: Lion Sword — 초월계 사자 기사의 장검. 화려한 사자 문양 장식.
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계
- 비주얼 컨셉:
  - 화려하고 긴 장검 (Longsword)
  - 손잡이(Hilt)나 폼멜(Pommel)에 사자 머리 또는 갈기 문양 장식
  - **팀 식별:** 손잡이에 감긴 천이나 폼멜에 박힌 보석 색상으로 팀 구분

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view)
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, fantasy game asset, clean silhouette

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트를 하나로 묶어서 작성해줘 (--no 파라미터 사용)
```

---

### Unit 6 — Fox Mage (Tier 3, 여우 마법사)

**컨셉**: 최강 초월계 유닛. 마법 공격 + 서포터 역할. 귀여운 외형의 여우 마법사. 지팡이를 들고 원거리 마법을 사용.

```
다음 조건에 맞는 Nano Banana 이미지 생성 프롬포트를 작성해줘.

[에셋 정보]
- 종류: 유닛 캐릭터 (3D 게임 에셋)
- 이름/설명: Fox Mage — 초월계 Tier3 유닛. 마법 공격형 여우 마법사. 지팡이를 든 서포터형
- 게임 스타일: Clash of Clans/Clash Royale 풍 카툰 3D 이소메트릭
- 종족: 초월계 (의인화된 동물, 짐승화 판타지)
- 비주얼 컨셉:
  - 직립 보행하는 의인화된 여우 체형 (날씬하고 우아한 체구)
  - 큰 여우 귀, 복슬복슬한 꼬리
  - 마법사 로브 + 망토 (초록+보라 색상)
  - 한 손에 크리스탈이 달린 마법 지팡이
  - 다른 손에 마법진/룬 이펙트
  - 주황/크림색 여우 털
  - 안경 또는 마법 안경 액세서리 (선택)

[이미지 조건]
- 배경: 순수 흰색 배경 (no background)
- 앵글: 정면뷰 (front view), 전신이 다 보이게
- 조명: 균일하고 부드러운 조명, 강한 그림자 없음
- 스타일: cartoon, stylized, kemono fantasy character, clean silhouette
- 포즈: 자연스러운 대기 포즈 (idle stance)

[필수 요청 사항]
- 프롬포트는 반드시 영어로 작성해줘
- 영어로 작성한 각 항목의 의미를 한글로 설명해줘
- 이 에셋에 어울리는 레퍼런스 이미지를 어디서 찾으면 좋을지 알려줘
- Meshy AI image-to-3D 변환에 최적화
- Positive 프롬프트와 Negative 프롬프트 분리해서 작성해줘
```

---

## Unity Import 체크리스트

### 건물 Import (Static Mesh)

| 항목 | 설정값 |
|------|--------|
| Scale Factor | 시각적으로 헥스 타일 1칸에 맞게 조정 (Castle≒50, Barracks≒40, MiningPost≒35 참고) |
| Rig Type | None |
| Animation | None |
| Read/Write | Off |
| Shader | URP/Lit (Standard로 되어있으면 Lit으로 교체 필수 — 아닐 경우 핑크색) |
| Smoothness | 0.3 (기본), 금속 부위 0.5 |
| Emission | 반드시 제거 (Meshy.ai가 _EmissionColor=(1,1,1,1)로 추출 — 흰색 버그 원인) |

**프리팹 구조** (기존 건물과 동일):
```
Building_[Name] (빈 루트, Position 0,0,0)
  └── [Name].fbx (보정값: Y Position, Y Rotation)
```

**프리팹 저장 경로**: `Assets/_Project/Prefabs/Buildings/Building_[Name].prefab`

---

### 유닛 Import (Rigged + Animated)

| 항목 | 설정값 |
|------|--------|
| Scale Factor | 1.0 |
| Rig Type | Humanoid |
| Avatar | Copy From Other Avatar → Pistoleer Avatar 사용 |
| Smoothness | 0.3 (기본), 금속 무기 0.5 |
| Emission | 반드시 제거 |

**애니메이션 클립 이름 (코드와 반드시 일치)**:
- `Walk` — Loop ON
- `Attack` — Loop OFF
- `Dead` — Loop OFF

**Mixamo 리깅 흐름** (Meshy AI 리깅 품질 불안정 시 대안):
```
Meshy AI FBX → Mixamo.com Auto Rigger → FBX for Unity (Without Skin) → Unity Import
```

**프리팹 구조** (기존 유닛과 동일):
```
Unit_[Name] (빈 루트, UnitView 스크립트 부착)
  └── [Name].fbx (보정값: Y Position -0.1 전후, Y Rotation 30° 전후 — 실제 임포트 후 조정)
```

**프리팹 저장 경로**: `Assets/_Project/Prefabs/Units/Unit_[Name].prefab`

---

## 에셋 명명규칙 정리

| 종류 | 파일명 | 예시 |
|------|--------|------|
| 건물 FBX | `[BuildingName].fbx` | `SpiritNexus.fbx`, `WarTotem.fbx` |
| 유닛 FBX | `[UnitName].fbx` | `FireSpirit.fbx`, `BearWarrior.fbx` |
| 건물 프리팹 | `Building_[Name].prefab` | `Building_SpiritNexus.prefab` |
| 유닛 프리팹 | `Unit_[Name].prefab` | `Unit_FireSpirit.prefab` |
| 텍스처 | `tex_[name]_albedo.png` | `tex_firespirit_albedo.png` |
| 머티리얼 | `mat_[name].mat` | `mat_firespirit.mat` |
| 애니메이션 | `[UnitName]_[State].anim` | `FireSpirit_Walk.anim` |

---

## 작업 순서 제안

1. **정령계 건물 3종** → Gemini 요청 → 이미지 생성 → Meshy 3D 변환
2. **초월계 건물 3종** → 동일 흐름
3. **유닛 6종** → 동일 흐름 (유닛은 Animate 탭 T-pose + 리깅 포함)
4. Unity Import → 프리팹 생성 → 스케일/보정값 조정

> 💡 같은 종족 에셋은 한꺼번에 진행하면 스타일 일관성이 높아짐.
> 정령계 건물 3개를 같은 세션에서 요청하면 Gemini가 일관된 스타일을 유지함.
