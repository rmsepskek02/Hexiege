# AAB 용량 최적화 기록

작성일: 2026-07-15

## 요약

`codex/asset-size-optimization` 브랜치에서 Android AAB 빌드 용량을 **190.66 MB**에서 **125.30 MB**로 줄였다.

총 감소량은 **65.36 MB**다.

가장 효과가 컸던 변경은 3D 모델용 텍스처의 Android import 최대 크기를 낮춘 것이다.

- `Assets/_Project/Texture/Buildings/**`: Android max texture size `1024 -> 512`
- `Assets/_Project/Texture/Units/**`: Android max texture size `1024 -> 512`

이번 작업에서는 UI 스프라이트, 유닛 초상화, 건물 아이콘, UI 배경은 변경하지 않았다.

## 빌드 용량 변화

| 단계 | AAB 용량 | 비고 |
|---|---:|---|
| 최초 측정 | 190.66 MB | 최적화 전 |
| 안전한 에셋 삭제 및 FBX import 조정 후 | 186.71 MB | FBX import 조정은 최종 AAB에는 영향이 작았음 |
| 건물/유닛 텍스처 일부 512 적용 후 | 156.40 MB | 건물 텍스처 대부분 적용, 일부 유닛 텍스처 미적용 |
| 건물/유닛 텍스처 전체 512 적용 후 | 125.30 MB | 최종 측정 결과 |

## 적용한 변경

### 3D 텍스처 Android 최대 크기 조정

주요 절감 효과는 모델 텍스처 폴더에 Android max texture size `512`를 적용하면서 발생했다.

대상:

- 건물 모델 텍스처
- 유닛 모델 텍스처
- 해당 폴더의 base color, emission, metallic 텍스처

전체 적용 후 빌드 리포트 기준 주요 텍스처 카테고리는 다음과 같다.

| 카테고리 | 패킹 후 용량 |
|---|---:|
| `Texture/Buildings` | 14.31 MB |
| `Texture/Units` | 12.20 MB |
| `Sprites/Buildings` | 32.20 MB |
| `Sprites/Units` | 22.74 MB |
| `Sprites/UI` | 12.06 MB |

추가 절감 후보는 3D 모델 텍스처보다 스프라이트 계열 에셋이다.

### FBX Import 조정

건물 FBX와 명확히 장비/무기 전용으로 분리된 FBX에 보수적인 import 설정을 적용했다.

- mesh compression 적용
- blend shape import 비활성화
- animation import 비활성화
- animation type none 설정

이 변경은 안전하게 시도할 수 있었지만, 최종 AAB 용량 감소 효과는 작았다. 원본 FBX 파일 크기에 비해 실제 빌드에 포함되는 mesh 데이터가 훨씬 작았기 때문이다.

### TMP Font Atlas 테스트는 되돌림

`Maplestory Bold SDF.asset`, `Maplestory Light SDF.asset`의 TMP atlas 크기를 줄이는 테스트를 진행했다.

패킹 기준 폰트 에셋은 대략 `16 MB + 16 MB`에서 `4 MB + 4 MB`로 줄었지만, 압축된 최종 AAB 용량에는 의미 있는 변화가 거의 없었다. 그래서 폰트 에셋은 원래 상태로 되돌렸다.

## 변경하지 않은 영역

다음 영역은 의도적으로 건드리지 않았다.

- UI 배경 스프라이트
- 유닛 초상화 스프라이트
- 건물 아이콘 스프라이트
- TextMeshPro 폰트 에셋
- Addressables 또는 asset bundle 구조
- Firebase/Auth 또는 게임플레이 코드

## 기기 테스트 체크리스트

기기에서는 다음 항목을 확인한다.

- 설치 및 실행
- 로그인 흐름
- 로비 UI 가독성
- 인게임 건물 시각 품질
- 인게임 유닛 시각 품질
- 팀 색상 변형
- 공격 이펙트와 emission이 강한 에셋
- 가까운 카메라에서 눈에 띄는 텍스처 뭉개짐

특히 3D 유닛/건물은 Android 텍스처 최대 크기가 512로 낮아졌으므로 우선 확인 대상이다.

## 롤백 기준

3D 텍스처 품질이 지나치게 낮아 보이면 아래 경로의 Android max texture size 변경만 되돌린다.

- `Assets/_Project/Texture/Buildings/**`
- `Assets/_Project/Texture/Units/**`

전체를 되돌리기보다는 대부분은 `512`를 유지하고, 화면에서 크게 보이거나 품질 저하가 눈에 띄는 핵심 에셋만 `1024`로 되돌리는 방식을 우선 고려한다.

## 다음 최적화 후보

기기 QA 후 추가 절감이 필요하면 다음 순서로 검토한다.

1. `Assets/_Project/Sprites/Units` 아래 작은 카드/목록용 유닛 스프라이트를 선별적으로 낮춘다.
2. `Assets/_Project/Sprites/Buildings` 아래 작은 건물 아이콘을 선별적으로 낮춘다.
3. 전체 화면 UI 배경은 시각 품질 확인 전까지 높은 품질을 유지한다.
4. 시각 에셋 압축을 충분히 진행한 뒤에 Addressables 도입 여부를 검토한다.
