# Research — StreamSpirit / TorrentSpirit 공격 VFX 정밀 튜닝

## 이 작업이 무엇인지

이미 연결된 두 물 정령의 공격 VFX를 Unity 화면에서 직접 확인하면서 공격 모션과 판정에 맞게 조정한다.
StreamSpirit은 이펙트가 나타나는 위치와 공격 애니메이션 내 발생 시점이 어긋나고, TorrentSpirit은
이펙트가 재생되지만 위치·크기·이동 표현이 실제 파도 공격과 맞지 않는 상태다.

이번 작업은 연결을 새로 만드는 작업이 아니라, 화면과 공격 규칙을 기준으로 연출을 정밀하게 맞추는 작업이다.

---

## 현재 연결 구조

공격 애니메이션의 `OnAttackHit` 이벤트가 `AnimationEventRelay.OnAttackHit`을 거쳐
`UnitView.OnAttackHit`을 호출한다. 이후 `EffectManager.PlayUnitAttack`이 `UnitEffectConfig`에서
유닛별 `EffectPreset`을 찾아 오브젝트 풀을 통해 VFX를 재생한다.

- StreamSpirit(17) → `EffectPreset_StreamSpirit_Attack` → `vfx_streamspirit_attack.prefab`
- TorrentSpirit(18) → `EffectPreset_TorrentSpirit_Attack` → `vfx_torrentspirit_attack.prefab`

## StreamSpirit 현황

- Blue/Red 프리팹 모두 전용 `VfxSpawnPoint`가 연결되어 있다.
- 현재 공격 이벤트는 `StreamSpirit_Attack.anim`의 약 0.17초에 있다.
- VFX 위치는 `VfxSpawnPoint.position`, 방향은 유닛 루트의 `transform.forward`를 사용한다.
- 문제는 연결 누락이 아니라 현재 스폰 위치와 공격 모션 내 재생 프레임의 시각적 불일치다.

## TorrentSpirit 현황

- Blue/Red 프리팹 모두 `_vfxSpawnPoint`가 비어 있어 유닛 루트 위치에서 생성된다.
- 공격 이벤트는 `TorrentSpirit_Attack.anim`의 약 0.5초에 있다.
- VFX 프리팹 원본 스케일을 그대로 사용하며, 현재 재생 API에는 유닛별 위치·회전·크기 오프셋이 없다.
- 서버 판정 파도는 폭 3, 전방 길이 3, 이동 시간 0.5초인 이동 전선이다.
- 현재 VFX는 공격 시 한 번 재생될 뿐 서버 파도 진행과 직접 연결되지 않는다.
- 따라서 유닛 중심에서 벗어난 생성 위치, 과하거나 부족한 크기, 판정과 맞지 않는 전진 표현이 발생할 수 있다.

## 나머지 Units VFX 감사 결과

- `vfx_tank_attack`은 미할당이며 Tank가 현재 CannonCart와 `vfx_cannon_attack`을 공유한다.
- Pistoleer·Assault·Sniper가 `vfx_pistoleer_attack`을 공유한다.
- InfernoSpirit·FoxMagician은 이름이 `charge`인 VFX를 공격 타격 이벤트에서 재생한다.
- 공통 사망 VFX는 등록된 전 유닛에 연결되어 있다.

이 항목들은 이번 두 물 정령 튜닝 후 화면으로 적합성을 점검하되, 사용자 승인 없이 별도 유닛 설정을
변경하지 않는다.

## 관련 규칙

- `GameSystemRules_Units` 규칙 17: 공격 타격 시점의 단일 출처는 Attack 클립의 `OnAttackHit` 이벤트다.
- 규칙 18: 데미지 판정은 서버 타이머 권위이며 화면 VFX에 종속시키지 않는다.
- 규칙 20: 이동 공격 연출은 시각 표현이며 데미지 판정을 변경하지 않는다.
- TorrentSpirit 특수 공격 규칙: 화면의 파도는 서버 파도 진행과 시각적으로 일치시켜야 한다.

## 핵심 위험

- Animation Event를 이동하면 공격 VFX뿐 아니라 로컬 피격 연출 방출 시점도 함께 바뀐다.
- VFX 프리팹 자체를 수정하면 같은 프리팹을 사용하는 모든 인스턴스에 영향을 준다.
- 풀에서 재사용할 때 Transform scale과 파티클 상태가 초기화되지 않으면 두 번째 공격부터 결과가 달라질 수 있다.
- Blue/Red 프리팹을 따로 조정하면 진영별 위치 차이가 생길 수 있다.

