# Research — ProductionPopup 규칙 준수 점검

## 작업 목적 (자연어 설명)

현재 Game 씬에 있는 ProductionPopup의 모든 자식 오브젝트를 GameSystemRules.md의 공통 UI 규칙과 대조하여,
고정 픽셀값 사용(규칙 2), Filled 이미지 자식 앵커 규칙(규칙 3) 위반 항목을 파악한다.
이 문서는 씬 파일(Game.unity)을 직접 파싱하여 확인한 실측 데이터를 기반으로 작성됐다.

---

## 분석 대상 씬

`Assets/_Project/Scenes/Game.unity`

---

## ProductionPopup 전체 계층 구조 (현재 상태)

```
ProductionPopup  (RT: min=(0,0) max=(1,1) size=(0,0))
│  Components: ProductionPanelUI, PopupController
│
└── ProductionPanel  (RT: min=(0,0) max=(1,0.4) size=(0,150) pivot=(0.5,0))
    │  Components: Image(배경)
    │
    ├── HeaderText  (RT: min=(0.096,0.826) max=(0.867,1.006) size=(0,0))
    │     [TMP]
    │
    ├── CancelButton  (RT: min=(0.883,0.852) max=(0.993,0.97) size=(0,0))
    │     [Image]
    │
    ├── GridContainer  (RT: min=(0.08,0.123) max=(0.92,0.864) size=(0,0))
    │   [VLG] pad=L20 R20 T20 B20, spacing=8, ctrl=(1,1), force=(1,1)
    │   │
    │   ├── Row0  [HLG] pad=0, spacing=8, ctrl=(1,1), force=(1,1)
    │   │   ├── Slot1  [HLG] pad=L60 R60 T20 B20
    │   │   │   [Image] sprite=YES
    │   │   │   ├── IconImage  [LE] flex=(6,1)  [Image] preserve=1
    │   │   │   └── CostContainer  [VLG] [LE] flex=(4,1)
    │   │   │         ├── GoldIcon  [LE] min=(44,44) pref=(44,44)  [Image] preserve=1
    │   │   │         └── CostText  [LE] pref=(400,22)  [TMP]
    │   │   ├── BorderOverlay  [LE] ignore=1  RT: min=(0,0) max=(1,1) pos=(-284.66,0) size=(-569.32,0)
    │   │   ├── Slot2  (Slot1과 동일 구조)
    │   │   ├── BorderOverlay  [LE] ignore=1  RT: pos=(-1,0) size=(-569.32,0)
    │   │   └── Slot3  (Slot1과 동일 구조)
    │   │       └── BorderOverlay  [LE] ignore=1  RT: pos=(290,0) size=(-569.32,0)
    │   │
    │   ├── Row1  [HLG] pad=0, spacing=8, ctrl=(1,1), force=(1,1)
    │   │   ├── Rallypoint  [HLG] pad=L125 R125 T20 B20
    │   │   │   [Image] sprite=YES  [CG] alpha=1
    │   │   │   └── IconImage  [LE] ignore=1  RT: min=(0,0) max=(1,1) size=(0,0)  [Image] preserve=1
    │   │   ├── Slot5  [HLG] pad=L60 R60 T20 B20
    │   │   │   [Image] sprite=YES
    │   │   │   ├── IconImage  [LE] flex=(6,1)  [Image] preserve=1
    │   │   │   └── CostContainer  [VLG] [LE] flex=(4,1)
    │   │   │         ├── GoldIcon  [LE] min=(44,44) pref=(44,44)
    │   │   │         └── CostText  [LE] pref=(400,22)  [TMP]
    │   │   └── Destroy  [HLG] pad=L60 R60 T20 B20
    │   │       [Image] sprite=YES
    │   │       ├── IconImage  [LE] flex=(6,1)  [Image] preserve=1
    │   │       └── CostContainer  [VLG] [LE] flex=(4,1)
    │   │             ├── GoldIcon  [LE] min=(44,44) pref=(44,44)
    │   │             └── CostText  [LE] pref=(400,22)  [TMP]
    │   │
    │   └── Row2  [HLG] pad=0, spacing=8, ctrl=(1,1), force=(1,1)
    │       ├── Slot7  (Slot1과 동일 구조)
    │       ├── Slot8  (Slot1과 동일 구조)
    │       └── Slot9  (Slot1과 동일 구조)
    │
    ├── QueueSlots  (RT: min=(0,0.25) max=(1,0.49) pos=(0,-71) size=(0,0))
    │   [HLG] pad=0, spacing=100, ctrl=(0,0), force=(0,0), align=MiddleCenter
    │   ├── Slot1  RT: size=(160,160)
    │   │     ├── SlotImage  RT: anchor=(0.5,0.5)~(0.5,0.5) size=(150,150)
    │   │     └── UnitImage  RT: min=(0,0) max=(1,1) size=(0,0)
    │   ├── Slot2  (Slot1과 동일)
    │   └── Slot3  (Slot1과 동일)
    │
    ├── ProgressBar  (RT: min=(0,0.17) max=(1,0.25) pos=(0,-36) size=(0,100))
    │   [Image] sprite=YES
    │   └── Fill  RT: min=(0,0) max=(1,1) pos=(0,0) size=(-300,-140)
    │         [Image] sprite=YES
    │
    └── InfoBar  (RT: min=(0.1,0) max=(0.9,0.09) pos=(0,17) size=(0,0))
        [HLG] ctrl=(1,0) force=(1,0)
        ├── GoldIcon  RT: size=(0,100)  [LE] min=(100,-1) pref=(100,100)
        ├── GoldText  RT: size=(0,100)  [LE] flex=(1,-1)  [TMP]
        ├── PopIcon   RT: size=(0,100)  [LE] min=(100,-1) pref=(100,100)
        └── PopText   RT: size=(0,100)  [LE] flex=(1,-1)  [TMP]
```

---

## 발견된 규칙 위반 항목

### 규칙 2 위반 — 고정 픽셀값 사용

| # | 오브젝트 | 위반 내용 |
|---|----------|----------|
| 1 | ProductionPanel | sizeDelta.y=**150** 고정 픽셀 |
| 2 | ProgressBar | pos.y=**-36** 고정 + sizeDelta.y=**100** 고정 |
| 3 | QueueSlots | pos.y=**-71** 고정, HLG spacing=**100** 고정 |
| 4 | QueueSlots > Slot1/2/3 | sizeDelta=(**160**, **160**) 고정 |
| 5 | QueueSlots > SlotImage | anchor 단일점 (0.5,0.5)~(0.5,0.5) + sizeDelta=(**150**, **150**) |
| 6 | InfoBar | pos.y=**17** 고정 |
| 7 | InfoBar > GoldIcon, PopIcon | sizeDelta.y=**100** + LE min=(**100**, -1) 고정 |
| 8 | InfoBar > GoldText, PopText | sizeDelta.y=**100** 고정 |
| 9 | BorderOverlay ×3 | ignoreLayout=1이나 pos/size 모두 고정 픽셀 |

### 규칙 3 위반 — Filled 이미지 자식 앵커 규칙

| # | 오브젝트 | 위반 내용 |
|---|----------|----------|
| 1 | ProgressBar > Fill | 부모(Filled Image) 자식인데 sizeDelta=(**-300**, **-140**) 고정 픽셀 오프셋 |

---

## 구조 이슈 (규칙 위반은 아님)

| 항목 | 내용 |
|------|------|
| Row2 (Slot7/8/9) | 현재 사용되지 않는 잔존 오브젝트. 이전 6유닛 슬롯 구조의 잔재. |
| BorderOverlay ×3 | sprite=NONE — 빈 오버레이. 시각적 역할 없음. |
| Slot5 이름 | 업그레이드 버튼으로 추정되나 이름이 "Slot5"로 잔존. |
