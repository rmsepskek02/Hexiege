# Plan — 방어 타워 공격 표현·소리 개편 (C 작업)

> **작성일:** 2026-11-02 · **작성:** document-manager · **기준 커밋:** `7368d7f1` (브랜치 `claude/vfx-code-followups`)
> **상태:** **완료 — 구현 · 사용자 테스트 PASS(2026-11-03) · 옛 코드 최종 삭제 완료 · 규칙 B-1~B-4 · S-1~S-3 반영** — [15절](#15-implementation-and-user-test). 최종 삭제 뒤 회귀 스위트 재실행도 **PASS**(사용자 「회귀 테스트는 정상이야」 2026-11-03 — 결과 파일 없음(Console만) · 에이전트 미관측). *(2026-11-03 2차 갱신 — 직전 「최종 삭제 뒤 회귀 스위트 재실행 결과는 **미확인**(사용자 실행 예정).」)* 승인: 사용자 2026-11-02 추천안(13절 「결정」 열). *(2026-11-03 갱신 — 직전 「**승인됨(사용자 2026-11-02, 추천안)** — 13절 「결정」 열. 이 기록 시점 구현 착수 기록 없음 — 순서상 ① 효과음 1단계 뒤에 착수한다(12절 · K-7).」)* *(2026-11-02 갱신 — 종전 「🔴 사용자 승인 대기. 구현하지 않았다. 승인 전에는 game-programmer에게 코드·에셋 작업을 맡기지 않는다(WORKFLOW [4]).」)*
> **근거 문서:** 같은 폴더 [Research.md](Research.md). 사용자 결정(2026-11-02 「추천대로」)은 Research 1절이 원본이며 이 Plan은 그 결정을 바꾸지 않는다. 결정이 정하지 않은 세부는 **[제안]** 으로 표시했고 13절에서 승인을 받는다.
> **표기:** [확인] = 이 Plan 작성 중 파일을 직접 열어 확인 · [전달] = 메인 세션 전달 · [제안] = 이 Plan의 제안(사용자 확인 전) · [미확인] = 확인하지 못함.
> **최상단 기재 규칙:** WORKFLOW [4] 「기존 로직 제거 규칙」에 따라 제거·비활성화 목록을 1절(자연어 설명 바로 다음, 본문 맨 앞)에 둔다.

---

## 0. 이 작업이 무엇인가 (자연어 설명)

세 종족의 방어 타워(인간 대포 타워 · 정령 룬 첨탑 · 초월 덩굴 타워)는 프로그램 안에서 같은 건물 종류다. 그런데 공격할 때 화면에 무엇을 보여 줄지는 「인간이면 이렇게, 정령이면 저렇게, 초월이면 아무것도 안 함」처럼 여러 파일에 **종족 이름으로 직접** 갈라 놓았다. 이번 작업은 이것을 **타워마다 한 줄짜리 「공격 프로필」** 로 바꾼다. 프로필에는 「이 타워는 어떤 방식으로 공격하는가(대포알이 날아가는 투사체 / 번개 / 땅에서 솟는 지면 분출)」, 「어떤 이펙트를 쓰는가」, 「어디서 재생하는가(타워 위치 / 맞는 적의 발밑 / 포구)」가 들어간다. 화면 연출 코드는 종족 대신 **공격 방식**을 보고 움직인다. 그러면 다음 작업에서 덩굴 타워 연출을 붙일 때 코드를 고치지 않고 프로필 한 줄만 채우면 된다.

함께 타워 **공격음** 자리도 만든다. 소리 설정 파일(`SoundConfig`)에 「건물 종류 + 종족」 칸을 새로 두고, 소리 관리자(`AudioManager`)에 타워 공격음을 내는 통로를 만들고, 이펙트를 재생하는 바로 그 자리에서 소리도 함께 부르게 한다. 다만 실제 소리 파일은 아직 넣지 않는다 — 그래서 이 작업이 끝나도 **귀로 들리는 변화는 없다.**

그리고 지금 아무도 부르지 않는 옛 타워 연출 코드를 정리한다.

피해가 언제·얼마나 들어가는지(서버 판정), 인간 타워가 대포알을 실제로 쏘는 방식, 네트워크 신호는 **전혀 바꾸지 않는다.** 사용자가 이 작업 뒤 게임에서 보는 화면은 지금과 같아야 한다(인간 타워 대포알 · 룬 첨탑 번개 그대로, 덩굴 타워는 여전히 연출 없음).

---

## 1. ⚠️ 기존 로직 제거·비활성화 목록 (WORKFLOW [4] — 최상단)

**방식:** 모두 **주석 처리(비활성화)** 가 기본이다. 주석 처리한 코드의 최종 삭제는 [6] 사용자 테스트 통과 뒤, [7] 문서/메모리 업데이트 전에 한다(WORKFLOW [4]). 주석에는 날짜·이 Task 경로·대체한 새 코드 위치를 적는다(CLAUDE.md 규칙 8 — 초급자도 왜 꺼졌는지 알 수 있게).

| # | 위치 (기준 커밋 행) | 처리 | 안전 근거 |
|---|------------------|------|----------|
| RM-1 | `Assets/_Project/Scripts/Presentation/Effects/HitPresentationQueue.cs` `PlayTowerAttackVfx` 1486~1530행 | 주석 처리 | private 메서드, 저장소 전체 `.cs`에서 호출 0건 · 에디터 리플렉션 호출 0건 [확인 — Research 7절 U-1]. 꺼도 실행 경로가 바뀌지 않는다 |
| RM-2 | `Assets/_Project/Scripts/Presentation/Effects/EffectManager.cs` `PlayBuildingAttack` 518~535행 | 주석 처리 | 유일한 호출처가 RM-1(1529행) [확인 — Research U-2] |
| RM-3 | `Assets/_Project/Scripts/Presentation/Effects/BuildingEffectConfig.cs` `GetAttack(BuildingType)` 142~153행 | 주석 처리. 이를 부르던 `GetAttack(BuildingType, RaceId)` 52행 `if (type != BuildingType.AutoTower) return GetAttack(type);`는 `return null;`로 바꾼다(옛 줄은 주석으로 남김) | 호출처는 RM-2와 52행뿐. 52행으로 실제 오는 호출은 없다(모든 호출자가 AutoTower) · 읽는 값(`attackPreset`)이 에셋에 0건이라 지금도 늘 null을 돌려준다 [확인 — Research U-3]. 결과값이 같다 |
| RM-4 | 같은 파일 `BuildingEffectEntry.attackPreset` 36~37행 | 필드 주석 처리 | `BuildingEffectConfig.asset` `_entries` 3행에 `attackPreset` 키가 없다(직렬화 0건) → 지워질 데이터가 없다 [확인 — Research 5-1] |
| RM-5 | `HitPresentationQueue.cs` 649~652행 옛 「타워 발사 연출(3-1)」 설명 주석 | 주석 문장 삭제 · 653행 현행 설명을 한국어로 보강 | 코드가 아니라 옛 설계 설명이다. 653행이 이미 「HP 이벤트가 아니라 확정 스냅샷이 유일한 발사 VFX 출처」라고 적고 있다 [확인 — Research U-5] |
| RM-6 | `BuildingEffectConfig.cs` 종족 직접 조회 — 53행(Transcendence면 null) · 54행 `_autoTowerAttacks` 검색 · 83~84행 · 93~94행 `_autoTowerProjectiles` 검색 | 주석 처리하고 새 프로필 조회로 대체(4-2절) | 같은 프리셋이 새 프로필 행으로 옮겨 가고(5절), 에디터 검증이 옮긴 값을 실제 프리팹 기준으로 대조한다(7절 V-1·V-2) |
| RM-7 | 같은 파일 `_autoTowerAttacks`·`_autoTowerProjectiles` 직렬화 필드와 `RaceAttack`·`RaceProjectile` 구조체 | **선언은 남기고 읽지 않는다**(Inspector에서 숨김 + 「비활성화 — 사용자 테스트 뒤 삭제」 주석). 최종 삭제 때 필드·구조체와 에셋의 두 키를 함께 지운다 | 필드 선언을 지금 지우면 Unity가 에셋을 다시 저장할 때 옛 값이 사라진다. 사용자 테스트 통과 전까지 옛 값을 에셋 안에 그대로 두어 되돌릴 수 있게 한다. 옛 값은 커밋 `7368d7f1`의 에셋 15~23행에도 남는다 |
| RM-8 | `EffectManager.cs` `PlayAttackEffect` 558행(Transcendence 차단) · 577행(`s.Race == RaceId.Spirit`이면 번개) | 주석 처리하고 공격 방식 기준 분기로 대체(4-3절) | 동작 대응표(3-3절)에서 Human·Spirit 결과가 지금과 같고, 번개 fixture와 (f)가 그대로 통과해야 한다(7절 V-3) |
| RM-9 | `HitPresentationQueue.cs` 412~413행(`s.Race == RaceId.Spirit`이면 대상 해석) · 416행(`UnspecifiedTranscendence` 실패 제외) | 주석 처리하고 대체(4-4절) | 위와 같음. 416행은 새 「프로필 비어 있음」 결과로 바뀐다 |
| RM-10 | `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` 48~49행 중 `GetAttack(AutoTower, Transcendence) == null` 조건과 메시지 「Race selector preserves unspecified tower.」 | 검증 문장 개정(옛 조건은 주석으로 남김) | 사용자 결정 2(「VineTower 프로필이 비어 있으면 재생하지 않음」으로 개정) [전달] |

> RM-6·RM-8·RM-9·RM-10은 [VineTower Plan](../14_40_vinetower-attack-design/Plan.md) 0절이 이 작업으로 넘긴 차단 4곳 **X-1(558행) · X-2(53행) · X-3(416행) · X-4(검증 48~49행)** 과 같다. 이 작업이 네 곳을 모두 처리한다.

**지우지 않는 것:** `TowerCombatUseCase.cs` 286행 주석 「Sound15: tower fire SFX is unspecified.」는 서버 파일이고 C-6 소스 게이트가 읽는 파일이라 건드리지 않는다(13절 A-9). 공격음은 클라이언트 쪽에서 내며 ④ 전까지 클립이 없으므로 문장 내용도 거짓이 되지 않는다.

---

## 2. 범위 · 근거 규칙

### 2-1. 하는 것 / 하지 않는 것

| 하는 것 | 하지 않는 것 |
|--------|-------------|
| 타워 공격 프로필 데이터 구조 · 에셋 이전 · 공격 방식 기준 재생 분기 | 서버 피해 경로 전부(`TowerCombatUseCase`, `ProjectileDeliveryContracts` H-1~H-5, `ProjectileConfig`, 핸드셰이크 토큰) — 결정 1 |
| GroundBurst 재생 경로(코드만, 데이터 없음) | VineTower 프로필 값 · 프리팹 · 프리셋(③ 작업) |
| `SoundConfig` 타워 공격음 칸 · `AudioManager` 타워 공격음 API · 호출 지점 | 클립 연결(④) · 동시 재생 우선순위 구현 자체(①) |
| 옛 타워 연출 코드 정리(1절) | RuneSpire 번개 끝점 OPEN(결정 7) · 건물 파괴/업그레이드음 종족 구분(Research 11절 2) |
| 에디터 검증 개정 · 규칙 문서 개정 초안 | 스냅샷 구조·RPC · `BuildingFactory` 초기 회전 |

### 2-2. 읽은 규칙 (WORKFLOW [4])

`GameSystemRules.md` 인덱스 → `GameSystemRules_Buildings.md` 「방어 타워 시스템」(규칙 9·11·12) · `GameSystemRules_Sound.md` 전체(규칙 1·3·5·13·14·15·16) · 인덱스가 함께 보라고 한 `GameSystemRules_UnitCombatSynchronization.md`의 NET-AUTH-001(서버 행동 권위)·NET-PRESENT-003(피격 표현 상관관계) 제목 위치 [확인].

| 규칙 | 이 Plan에서의 쓰임 |
|------|------------------|
| 방어 타워 규칙 9 「서버 권위 처리」 | 표현은 서버 결과를 받아 보여 주기만 한다 → 서버 경로 불변 |
| 방어 타워 규칙 12 「타워 발사 연출」 | 프로필이 대체하는 「BuildingType + Race 선택」 · Transcendence 미지정 서술의 원본 → 개정 초안 B-1~B-4 |
| 규칙 12 2026-10-13 블록 「Spirit 번개 화면 표현 추적」 | 번개 추적 동작은 그대로, 적용 조건만 「공격 방식 Beam」으로 |
| 사운드 규칙 1 「역할 분리」 | `EffectManager`에 오디오 코드를 넣지 않는다 → 짝 호출은 부르는 쪽(Presenter · HitPresentationQueue) |
| 사운드 규칙 3 「SoundConfig 레이어 의존 범위」 | 키 = `BuildingType` + `RaceId`(둘 다 Domain — Research 2절) |
| 사운드 규칙 5 「null-safe」 | `AudioManager.Instance?.` |
| 사운드 규칙 13 「동시 재생 한도」 | ①이 개정(특수·타워 우선) — 이 Plan은 연결만 |
| 사운드 규칙 14 「SFX 동기화 금지」 | 각 화면이 로컬로 재생 — 새 네트워크 신호 없음 |
| 사운드 규칙 15 「VFX+SFX 쌍」 | 호출 지점 6절 |
| 사운드 규칙 16 「클립별 볼륨」 | 새 항목에 `attackVolume` |
| NET-AUTH-001 | 번개 추적·GroundBurst 위치는 어떤 판정 입력도 되지 않는다 |

---

## 3. 설계 — 타워 공격 프로필

### 3-1. 데이터 모양 [제안 — 결정 2의 구체화]

새 파일 `Assets/_Project/Scripts/Presentation/Effects/TowerAttackProfile.cs`(Presentation 레이어 — `BuildingEffectConfig`와 같은 층). `.meta`는 Unity가 만든다.

| 이름 | 종류 | 값 |
|------|------|----|
| `TowerAttackKind` | 열거형 | `Unset = 0`(비어 있음 — 재생 안 함) · `Projectile = 1`(투사체) · `Beam = 2`(번개) · `GroundBurst = 3`(지면 분출) — `Unset`은 13절 A-2 |
| `TowerAttackAnchor` | 열거형 | `Source = 0`(타워 위치) · `Impact = 1`(공격 순간 대상 발밑 — 스냅샷 Impact) · `Muzzle = 2`(서버 포구) |
| `TowerAttackProfile` | 직렬화 구조체 | `race`(RaceId) · `kind` · `anchor` · `launchPreset` · `tracerPreset` · `impactPreset`(모두 `EffectPreset`) |

`BuildingEffectConfig`에 `[SerializeField] List<TowerAttackProfile> _towerAttackProfiles` 한 목록을 둔다. 「비어 있음」의 정의: **행이 없거나, 세 프리셋이 모두 비어 있으면** 비어 있는 프로필이다(공격 방식 값과 무관). 같은 종족 행이 둘이면 첫 행만 쓴다(기존 `Initialize` 중복 처리와 같은 규칙 — `BuildingEffectConfig.cs` 113행).

### 3-2. 행 값

| 종족 | kind | anchor | launchPreset | tracerPreset | impactPreset | 출처 |
|------|------|--------|-------------|-------------|-------------|------|
| Human(0) | Projectile | Muzzle | `EffectPreset_HumanAutoTower_Attack` (GUID `1ddf58caf0af4ccdb04822bcb0fc7935`) | `EffectPreset_Cannon_Tracer` (`d6770727cae145341a419751a9037530`) | `EffectPreset_Cannon_Impact` (`f1b34352eb8d5d14d9579a5fb81b9c28`) | 지금 `_autoTowerAttacks` race 0 + `_autoTowerProjectiles` race 0 [확인 — 에셋 15~23행] |
| Spirit(1) | Beam | Source | `EffectPreset_RuneSpire_Attack` (`a01e8a0e35619b02dcd055e935a715d7`) | 없음 | 없음 | 지금 `_autoTowerAttacks` race 1 |
| Transcendence(2) | — | — | — | — | — | **행을 두지 않는다** [제안 — 13절 A-3]. VineTower Plan 5-6 C-2가 「VineTower 행 추가」로 적혀 있어 그쪽과 맞는다 |

### 3-3. 공격 방식별 재생 — 지금과의 대응

**(가) 즉발 스냅샷 경로** (`HitPresentationQueue.EmitAttackEffects` → `EffectManager.PlayAttackEffect`):

| kind | 재생 | 위치·회전 | 지금과 비교 |
|------|------|----------|-----------|
| 비어 있음 | 재생하지 않음. 결과 토큰 `TowerProfileEmpty`, 실패로 세지 않음 | — | 지금 Transcendence(`UnspecifiedTranscendence`)와 같은 효과 |
| Beam | `launchPreset`을 anchor 위치에서 재생 → `BeginTowerBolt`(번개 길이·원래 대상 추적) | anchor Source = 타워 위치. 회전 = 타워→대상 수평 방향(561~562행 그대로) | 지금 Spirit과 같다 |
| GroundBurst | `launchPreset`을 anchor 위치에서 **한 번** 재생. 번개·추적 없음 | anchor Impact = **스냅샷 Impact(공격 순간 대상 발밑, 화면이 뒤집힌 Client는 402~403행에서 이미 화면 좌표로 바뀐 값)에 한 번 놓고, 이후 대상 화면 위치를 다시 읽지 않는다.** 회전 = 타워→대상 수평 방향(같은 계산) | 새 경로 — 지금은 데이터가 없어 실행되지 않음 |
| Projectile | `launchPreset`을 **타워 위치**에서 재생(번개 없음) | anchor가 Muzzle이어도 스냅샷에는 포구 값이 없으므로 Source로 대신한다 | 지금 Human 스냅샷과 같다. 이 경로는 투사체 발사기가 연결되지 않은 경우(`TowerCombatUseCase.cs` 76·260행 — 주입 전·에디터 fixture)에만 온다. 에디터 검증 (f)가 고정 → 13절 A-4 |

- 프리셋이 있어야 하는데 없는 경우(프로필이 비어 있지 않은데 그 방식이 쓰는 `launchPreset`이 빈 경우)는 지금처럼 `MissingConfiguredPreset`(실패로 셈).
- 파티클이 0개면 `ParticleSystemUnavailable`로 거부하는 지금 규칙(564~565행)과 Stop Action 정리(566행)는 모든 방식에 그대로 적용한다.
- anchor 해석은 한 곳의 작은 함수로 둔다: Source → `source`, Impact → `impact`, Muzzle → `source`(스냅샷 경로).

**(나) 서버 투사체 경로** (Presenter → `EffectManager.BeginServerTowerProjectile` · `PlayServerTowerLaunchCue` · `PlayServerTowerProjectileImpact`): **세 메서드의 이름·인자·호출 줄은 그대로**다(C-6 게이트, 8절). 메서드 안에서 프리셋을 꺼내는 곳만 프로필의 `tracerPreset` · `launchPreset` · `impactPreset`으로 바뀐다. 발사 신호 위치는 지금처럼 Presenter가 넘기는 서버 포구(`visual.Start`)다 — Human 프로필의 anchor Muzzle은 이 사실을 데이터로 적어 두는 값이다.

**(다) 대상 추적 여부:** `HitPresentationQueue`가 원래 맞은 유닛을 해석하는 조건(지금 412행 `s.Race == RaceId.Spirit`)을 「그 종족 프로필의 kind가 Beam」으로 바꾼다. GroundBurst는 대상을 해석하지 않는다(VineTower Plan D-7 「공격 순간 자리 고정」과 맞음).

---

## 4. 파일별 변경 내용

> 모든 새 코드·바뀐 줄에는 초급자용 상세 주석을 단다(CLAUDE.md 규칙 8): 무엇을 하는지, 왜 종족이 아니라 공격 방식으로 고르는지, 이 Task 경로.

### 4-1. `Presentation/Effects/TowerAttackProfile.cs` (새 파일)
3-1의 열거형 2개와 구조체. 근거: 방어 타워 규칙 12(발사 연출 선택), 결정 2.

### 4-2. `Presentation/Effects/BuildingEffectConfig.cs`
- `_towerAttackProfiles` 목록 추가(Inspector 툴팁 한국어). `Initialize`에서 종족 → 프로필 캐시를 만든다.
- 새 조회: `bool TryGetTowerProfile(RaceId race, out TowerAttackProfile profile)`(비어 있으면 false) · `TowerAttackKind GetTowerAttackKind(RaceId race)`(비어 있으면 Unset).
- **기존 공개 조회 3개는 서명을 유지**하고 속만 프로필로 바꾼다: `GetAttack(BuildingType, RaceId)` → `launchPreset`, `GetTowerTracer` → `tracerPreset`, `GetTowerImpact` → `impactPreset`. AutoTower가 아니면 null(지금과 같음). 이유: `EffectManager` 744·722·764행과 에디터 검증 31·32·49·1588행이 이 서명을 쓴다 — 서명을 지키면 Human·RuneSpire 계약 검증이 고치지 않고 그대로 통과해야 한다.
- 1절 RM-3·RM-4·RM-6·RM-7 처리. 머리 주석 3~8행(「파괴/업그레이드 이펙트」)에 타워 공격 프로필 설명 추가.
- 근거: 방어 타워 규칙 12, 결정 2·3.

### 4-3. `Presentation/Effects/EffectManager.cs`
- `PlayAttackEffect`: RM-8. 3-3 (가) 표대로 「프로필 비어 있음 → 공격 방식별 재생」 분기. 번개 호출(578~579행 `BeginTowerBolt` 인자)은 그대로 두고 조건만 Beam으로.
- 새 공개 조회 `bool TowerAttackNeedsTargetTracking(RaceId race)`(kind == Beam) — `HitPresentationQueue`가 쓴다.
- 서버 투사체 세 메서드(720~772행): 이름·인자 불변, 내부는 4-2의 유지된 조회를 그대로 부르므로 **코드 변경 없음**이 기본이다.
- RM-2. 사운드 관련 주석 정리: 580행 「Sound15: … unspecified」 · 735행 「타워 발사 SFX는 아직 지정되지 않았다」를 「공격음은 이 메서드를 부르는 쪽(…)에서 짝으로 호출한다 — 사운드 규칙 1·15」로 고친다(코드 동작 없음).
- 근거: 방어 타워 규칙 12 · 2026-10-13 블록, 사운드 규칙 1, 결정 2.

### 4-4. `Presentation/Effects/HitPresentationQueue.cs`
- RM-9: 412행 조건 → `EffectManager.Instance != null && EffectManager.Instance.TowerAttackNeedsTargetTracking(s.Race)`. 416행 → `outcome != "TowerProfileEmpty"`.
- 공격음 호출 추가(6-3 P-2). RM-1 · RM-5.
- 근거: 규칙 12 2026-10-13 블록(추적은 Beam만), 사운드 규칙 15.

### 4-5. `Presentation/Effects/ProjectilePresentationPresenter.cs` — **줄 추가만**
- 6-3 P-1의 공격음 한 줄과 착탄 줄의 「착탄음 없음」 주석. 기존 줄은 한 글자도 바꾸지 않는다(8절 C-6 문자열).
- 근거: 사운드 규칙 15.

### 4-6. `Infrastructure/Config/SoundConfig.cs` · `Presentation/Audio/AudioManager.cs` — 6절.

### 4-7. `Resources/Config/BuildingEffectConfig.asset` — 5절.

### 4-8. `Assets/Editor/Tools/Combat/RunUnitActionSelfValidation.VfxConnections.cs` — 7절.

---

## 5. 에셋 데이터 이전 — Human · RuneSpire 참조가 그대로 이어지게

| 단계 | 할 일 | 담당 |
|------|------|------|
| E-1 | 코드(4-1·4-2) 컴파일 뒤, `BuildingEffectConfig.asset`에 `_towerAttackProfiles` 2행을 3-2 표 값으로 넣는다. 프리셋 참조는 **기존 행의 GUID를 그대로 복사**한다(새 참조를 고르지 않는다). 열거형은 숫자로 들어간다(kind Projectile=1·Beam=2, anchor Muzzle=2·Source=0) | game-programmer(YAML 직접 편집) 또는 사용자(Inspector) — 13절 A-7 |
| E-2 | 옛 키 `_autoTowerAttacks`·`_autoTowerProjectiles`는 **그대로 둔다**(RM-7) | — |
| E-3 | Unity가 에셋을 다시 읽은 뒤 회귀 스위트로 대조(7절 V-1·V-2: Human 발사 프리셋 = 실제 Assault 프리팹, Human 트레이서·착탄 = Cannon 프리셋, RuneSpire = `vfx_runespire_attack.prefab`) | 사용자(메뉴 실행) |
| E-4 | [6] 사용자 테스트 통과 뒤 RM-7 최종 삭제(필드·구조체·에셋 두 키) | game-programmer |

`GameBootstrapper.cs` 182행 `_buildingEffectConfig`(Game 씬 참조)는 같은 에셋 파일이라 바뀌지 않는다 [확인 — 참조 위치].

---

## 6. 타워 공격음

### 6-1. `SoundConfig.cs` [제안 세부]
- 새 구조체 `TowerAttackSoundEntry { BuildingType buildingType; RaceId race; AudioClip attackClip; [Range(0,1)] float attackVolume; }`(사운드 규칙 16 — 볼륨 칸).
- 새 목록 `_towerAttackSoundEntries`(Header 「타워 공격 SFX」, 툴팁 「건물 종류 + 종족별 타워 공격음. 클립은 효과음 2단계에서 연결」), 캐시 `Dictionary<(BuildingType, RaceId), TowerAttackSoundEntry>`(중복이면 첫 행 — 기존 규칙).
- 조회 `GetTowerAttackClip(BuildingType, RaceId)`(없으면 null) · `GetTowerAttackVolume(...)`(없으면 1.0 — 기존 조회와 같은 규칙).
- 머리 주석 24행 「Domain 레이어 타입(UnitType, BuildingType)만」에 RaceId 추가(규칙 문서 S-1 승인과 함께).
- 근거: 사운드 규칙 3(키는 Domain 타입 — `RaceId`는 `Domain/Common/RaceId.cs`, Research 2절), 16, 결정 4.

### 6-2. `SoundConfig.asset`
**목록을 비워 둔다**[제안 — 13절 A-6]. 이유: 행을 미리 만들면 Unity가 볼륨을 0으로 넣는다(지금 `_buildingSoundEntries` 행도 볼륨 0 — `SoundConfig.asset` 51~58행 [확인]). 나중에 클립만 넣고 볼륨을 잊으면 무음이 된다. ④에서 클립과 볼륨을 함께 넣는다.

### 6-3. `AudioManager.cs` API와 호출 지점

- 새 API `PlayTowerAttackSfx(BuildingType type, RaceId race)`: `_config`가 없으면 끝. 클립·볼륨은 `SoundConfig.GetTowerAttackClip` · `GetTowerAttackVolume`에서 꺼낸다. 재생은 **①이 개편한 `PlaySfxClip`에 「우선」을 넘겨** 한다.
  - 입구: [효과음 Plan](../13_39_unit-attack-sfx-tiering/Plan.md) 6-2 「재생 진입점」 — 「`PlaySfxClip(clip, volume)`(556행)에 우선 여부 인자를 받는 형태를 추가」하고, 같은 표 「우선 소리 표시」 — 「타워 공격음: 타워 task(②)가 만드는 API가 **항상 우선**으로 재생」 [확인 — 효과음 Plan 6-2]. 그래서 이 API는 항목별 표시를 읽지 않고 **늘 우선 값을 고정으로** 넘긴다. 형태는 `PlaySfxClip(clip, volume, 우선 = 참)`이다.
  - 인자 이름·타입(bool인지, 열거형인지)은 효과음 Plan이 「game-programmer가 확정」으로 남겼다 → **① 구현에서 확정된 서명을 그대로 쓴다**(이 Plan은 새 이름을 만들지 않는다). 우선 여부가 bool이 아니면 그 「우선」 값을 넘긴다.
  - 우선 소리가 받는 제약: 같은 클립 동시 3개 · 최소 간격 약 0.05초(효과음 Plan 6-1 A·B)는 우선 소리에도 적용하자는 것이 그 Plan Q-15의 제안이다. Human 타워는 Tank · CannonCart와 같은 `sfx_cannon_attack`을 쓰므로(효과음 Plan 2-5 · 10절) **세 종류가 한 3개 한도를 함께 쓴다**. 이것은 ①의 결정 사항이며 이 Plan은 따르기만 한다.
  - 순서: ①이 끝나 이 입구가 있어야 착수한다(결정 5). ①이 아직이면 시작하지 않고 메인 세션에 알린다(13절 A-5).
- 근거: 사운드 규칙 2·5·13(① 개정), 결정 4.

| # | 호출 지점 | 넣을 자리 | 조건 |
|---|---------|---------|------|
| P-1 | `ProjectilePresentationPresenter.cs` 타워 발사 신호(지금 315~317행 `if (!launch.AttackerIsUnit && !midFlight && effects != null) towerCue = effects.PlayServerTowerLaunchCue(...)`) | **그 if 문 바로 아래에 새 줄** `if (!launch.AttackerIsUnit && !midFlight) AudioManager.Instance?.PlayTowerAttackSfx(visual.TowerType, visual.TowerRace);` | 비행 중간 복원이면 생략(발사 VFX와 같은 규칙). VFX 재생 성공 여부와 무관 — 유닛 발사음과 같음(`UnitView.cs` 1471행) [제안 — A-5] |
| P-2 | `HitPresentationQueue.EmitAttackEffects`의 `PlayAttackEffect` 호출(지금 414~415행) | 바로 아래 줄 `AudioManager.Instance?.PlayTowerAttackSfx(BuildingType.AutoTower, s.Race);` | 스냅샷이 재생 단계까지 온 경우만(397~400행에서 버려진 스냅샷은 소리도 없음). VFX 성공 여부와 무관 |
| P-3 | Presenter 착탄 줄(지금 362~363행 `PlayServerTowerProjectileImpact`) | 바로 아래 **주석** 「착탄 SFX 없음 — 미지정(사운드 규칙 15)」 | 규칙 15 「하나만 재생하면 나머지를 주석으로 명시」 |

- **「공격음 미제작」 표시 위치(사운드 규칙 15, VineTower Plan U-6):** P-1·P-2 줄 위 주석에 「클립은 `SoundConfig` 타워 공격 SFX 칸 — 비어 있으면 무음. Human·RuneSpire는 효과음 2단계, VineTower는 VFX 확정 뒤」라고 적는다. 즉 표시는 **설정 칸이 아니라 호출 줄**에 있다. 칸이 비어 있으면 `PlaySfxClip` 559행처럼 소리 없이 끝난다.
- 두 지점이 겹쳐 같은 공격에 두 번 소리가 나는가: Human은 발사기가 연결돼 있으면 스냅샷을 내지 않으므로(`TowerCombatUseCase.cs` 250~258행) P-2에 오지 않는다. 스냅샷 경로의 Human은 발사기가 없을 때뿐이다 → 이중 재생 없음 [확인 — 코드 읽기].
- 멀티: 각 화면이 자기 표현 시점에 로컬로 낸다(사운드 규칙 14) — 새 네트워크 신호 없음.

---

## 7. 에디터 자동 검증 개정 (`RunUnitActionSelfValidation.VfxConnections.cs`)

| # | 위치 | 개정 |
|---|------|------|
| V-1 | 31~47행 Human 계약 | **바꾸지 않는다.** 유지된 `GetAttack(AutoTower, Human)` 서명이 새 프로필의 `launchPreset`을 돌려주므로 그대로 통과해야 한다 — 이전이 맞았는지의 첫 확인 |
| V-2 | 같은 묶음에 추가 | Human 프로필 kind = Projectile · anchor = Muzzle · `GetTowerTracer`/`GetTowerImpact`가 각각 `EffectPreset_Cannon_Tracer.asset`·`EffectPreset_Cannon_Impact.asset`(경로로 대조). Spirit 프로필 kind = Beam · anchor = Source |
| V-3 | 48~49행 (RM-10) | RuneSpire 프리팹 경로 조건은 유지. Transcendence 조건을 **데이터 기준**으로 바꾼다: 「VineTower 프로필이 비어 있으면 → `GetAttack(AutoTower, Transcendence) == null`이고, 그 스냅샷은 `PlayAttackEffect`가 false · `TowerProfileEmpty`로 끝난다」 / 「비어 있지 않으면 → kind = GroundBurst · anchor = Impact · `launchPreset` 프리팹에 파티클이 있다」. 이렇게 두면 ③이 VineTower 행을 채워도 **이 검증을 고칠 필요가 없다**(VineTower Plan U-5·C-3에 대한 답 — 메시지 문구만 ③에서 확인) |
| V-4 | 번개 fixture 1586~1715 | 기대값 **변경 없음**. (f) Human 스냅샷 → true · `Endpoint` 없음(3-3 Projectile 행)도 그대로. 추가 (g): Transcendence 스냅샷 → false · `TowerProfileEmpty` · 추적 중인 풀 아이템 없음 |
| V-5 | 추가 (GroundBurst 경로) | 실제 에셋을 바꾸지 않고, fixture 안에서 **임시 설정 객체**(`ScriptableObject.CreateInstance<BuildingEffectConfig>()`에 GroundBurst 행 1개, 프리셋은 RuneSpire 것을 빌려 씀 — 파티클이 있는 실제 프리셋)를 만들어 재생 위치 = 스냅샷 Impact + 프리셋 위치 보정, 추적 없음(`TowerBoltTracking` false), 대상 오브젝트를 옮긴 뒤에도 위치가 변하지 않음을 본다 [제안]. 끝나면 임시 객체를 지운다 |
| V-6 | 1717~1830 writer fixture | 변경 없음(Spirit 경로). 선택 [제안]: 같은 fixture에서 Transcendence 스냅샷 1건을 큐에 넣고 `_attackEffectFailures`가 늘지 않는지 리플렉션으로 확인 |
| — | `RunUnitActionSelfValidation.ServerProjectiles.cs` C-1 · C-5 · C-6 | **편집하지 않는다.** 바뀐 코드로 그대로 통과해야 한다(8절) |

근거: 결정 2(검증 개정), 방어 타워 규칙 12, WORKFLOW [5-2] 에디터 검증 유지 기준.

---

## 8. 바꾸지 않는 것 — C-6 게이트 확인 목록

구현 뒤 아래 파일은 `git diff`에 **나오지 않아야** 한다: `TowerCombatUseCase.cs`, `ProjectileDeliveryContracts.cs`, `UnitCombatUseCase.ProjectileDiagnostics.cs`, `BuildingFactory.cs`, `RunUnitActionSelfValidation.ServerProjectiles.cs`, `ProjectileConfig` 에셋, `VfxPoolItem.TowerBolt.cs`.

`ProjectilePresentationPresenter.cs`는 **추가 줄만** 있어야 하고, 아래 다섯 문자열이 주석이 아닌 줄에 그대로 남아야 한다(`RunUnitActionSelfValidation.ServerProjectiles.cs` 1181~1185행 [확인]):

1. `visual.Start = ViewConverter.ToView(launch.Source);`
2. `effects.BeginServerTowerProjectile(visual.TowerType, visual.TowerRace, visual.Start)`
3. `effects.PlayServerTowerLaunchCue(visual.TowerType, visual.TowerRace, visual.Start,`
4. `!launch.AttackerIsUnit && !midFlight && effects != null`
5. `effects.PlayServerTowerProjectileImpact(visual.TowerType, visual.TowerRace, point, point - visual.Start)`

같은 파일의 다른 게이트 문자열(`attackerView.PlayServerProjectileLaunchCue()` · `!midFlight` · `_hits?.ReleaseProjectileImpact(slot.Key, NumberFallbackPoint(slot, visual));` 등, 1148~1159 · 1563~1564행)도 건드리지 않는다.

---

## 9. 규칙 문서 개정 초안 (🔴 사용자 승인 대상 — 승인 전 원문 수정 금지)

> 「현행 문장」은 2026-11-02 원문이다 [확인]. 개정은 이 프로젝트 방식대로 날짜 조각과 종전 문장을 남긴다. 반영 시점은 [11] 문서 반영(구현·사용자 테스트 뒤)이다.

| # | 문서·위치 | 현행 문장 | 개정 초안 |
|---|----------|---------|----------|
| B-1 | `GameSystemRules_Buildings.md` 규칙 12 첫 문단 | 「종족별 타워는 단일 `BuildingType.AutoTower`를 유지하고 발사 프리셋은 **BuildingType + Race**로 선택한다.」 | 「종족별 타워는 단일 `BuildingType.AutoTower`를 유지하고, 공격 표현은 종족별 **타워 공격 프로필**(`BuildingEffectConfig` — 공격 방식 투사체/번개/지면 분출, 발사·투사체·착탄 프리셋, 재생 위치 기준)로 고른다. 화면 코드는 종족이 아니라 공격 방식으로 분기한다.」 |
| B-2 | 같은 문단 | 「… Transcendence VineTower는 미지정 상태에서 다른 종족 preset으로 fallback하지 않는다.」 | 「VineTower 프로필이 비어 있으면 공격 표현을 재생하지 않으며, 다른 종족 프로필로 대체(fallback)하지 않는다.」 (fallback 금지 원칙 유지 — VineTower Plan U-9와 같은 방향) |
| B-3 | 2026-10-20 개정 문단 | 「Human CannonTower 외 타워는 서버가 공격 이벤트를 발행할 때 각 클라이언트 로컬에서 발사 VFX(`BuildingEffectConfig.attackPreset`)를 재생한다.」 | 「… 각 클라이언트 로컬에서 그 타워 프로필의 발사 프리셋을 재생 위치 기준(번개 = 타워 위치, 지면 분출 = 공격 순간 대상 발밑에 한 번)에서 재생한다.」 · 같은 문단 끝 「발사 VFX에는 사운드 규칙 15(VFX+SFX 쌍)를 준수한다.」 뒤에 「타워 공격음은 `SoundConfig`의 「건물 종류 + 종족」 칸에서 고르고, 발사 VFX를 부르는 자리(투사체 타워 = 화면 투사체 표현의 발사 신호, 즉발 타워 = 공격 스냅샷 재생) 바로 아래에서 짝으로 호출한다.」 추가 |
| B-4 | 2026-10-13 블록 「적용 범위」 | 「Race가 Spirit인 AutoTower(RuneSpire)의 발사 VFX만. Human(별도 preset)·Transcendence(미지정, fallback 없음)는 바뀌지 않는다.」 | 「공격 방식이 번개(Beam)인 타워 프로필의 발사 VFX만(현재 Spirit RuneSpire뿐). 다른 공격 방식은 대상을 추적하지 않는다.」 |
| S-1 | `GameSystemRules_Sound.md` 규칙 3 | 「SoundConfig는 Infrastructure/Config 레이어에 배치하며, Domain 레이어 타입(UnitType, BuildingType)만 키로 사용할 수 있다.」 | 「… Domain 레이어 타입(UnitType, BuildingType, RaceId)만 키로 사용할 수 있다.」 — 괄호가 전체 목록인지 예시인지 문구로 정해지지 않아(Research 2절) 명시 |
| S-2 | 규칙 15 끝에 추가 | — | 「EffectManager는 오디오를 모르므로(규칙 1) 짝 호출은 EffectManager를 부르는 쪽에서 한다. 방어 타워 공격음: 서버 투사체 타워는 `ProjectilePresentationPresenter`의 발사 신호 자리, 즉발 타워는 `HitPresentationQueue`의 공격 스냅샷 재생 자리. 클립이 아직 없으면 호출 줄 위 주석에 「미제작」을 적는다.」 |
| S-3 | 규칙 16 둘째 문장 | 「SoundConfig의 각 SFX 엔트리(UnitSoundEntry, BuildingSoundEntry 등)에 `attackVolume`, `deathVolume` 등 항목별 볼륨 필드를 포함한다.」 | 엔트리 예시에 `TowerAttackSoundEntry` 추가 |

- 규칙 13(동시 재생 한도·우선순위)은 ① 작업이 개정한다 — 이 Plan은 초안을 두지 않는다.
- 규칙 12의 VineTower 서술(G-1~G-4)은 ③이 이 개정 **뒤 문장 기준으로** 다시 맞춘다(VineTower Plan 9절 머리말).
- [11] 현황 문서 후보(규칙 아님): `Assets/VFXSFXList.md` 「건물 공격 이펙트」 142~144행(설정 이름 · 옛 행 번호 529 → Research 11절 1), 같은 문서 SFX 절의 타워 공격음 자리(④ 때), `PROJECT_STATUS.md` · `WORK_HISTORY.md`.

---

## 10. 위험

| # | 위험 | 대비 |
|---|------|------|
| K-1 | **번개 회귀** — 대상 해석(412행)과 번개 분기(577행) 두 곳의 조건을 함께 바꾸므로 한쪽만 바뀌면 번개가 추적하지 않거나 길이가 안 맞음 | 두 곳이 같은 판단(`TowerAttackNeedsTargetTracking` = kind Beam)을 쓰게 한다. V-4 번개 fixture 전체 · 실기 RuneSpire(11절 S-2) |
| K-2 | **에셋 참조 누락** — YAML 이전 때 GUID·열거형 숫자 오타로 Human 발사·트레이서·착탄 또는 RuneSpire가 빠짐 | GUID는 기존 행에서 복사(E-1). V-1(실제 Assault 프리팹 대조) · V-2(Cannon 프리셋 경로 대조) · RuneSpire 프리팹명 대조. 실기 S-1·S-2 |
| K-3 | **C-6 게이트** — Presenter 줄을 정리하다 게이트 문자열이 바뀜 | 8절 목록. 구현 보고에 `git diff` 상 Presenter가 추가 줄만인지 명시하게 한다. 서버 파일 diff 0 확인 |
| K-4 | 옛 직렬화 값 유실 | RM-7 — 사용자 테스트 전까지 필드 유지. 커밋 `7368d7f1`에도 남음 |
| K-5 | 실패 집계 변화 — 새 토큰을 416행에서 빼지 않으면 VineTower 공격마다 `_attackEffectFailures`가 늘어 진단 END 줄 숫자가 달라짐 | 4-4 · V-6 |
| K-6 | 진단 표기 변화 — `UnspecifiedTranscendence` → `TowerProfileEmpty`. 옛 로그와 새 로그의 표기가 다름 | `LogRules.md`에는 이 토큰 목록이 없다(Research 11절 4). 변경을 이 Plan과 구현 보고에 기록 |
| K-7 | ① 미완료 상태 착수 — 우선 인자를 받는 `PlaySfxClip`(효과음 Plan 6-2)이 없음 | 6-3 — ① 완료 확인 뒤 착수. `AudioManager.cs` · `SoundConfig.cs`는 ①도 고치므로(12-1) 같은 시기에 두 작업이 같은 파일을 고치지 않는다(효과음 Plan 12절 조율과 같음) |
| K-8 | ③ Research의 행 번호·구조 서술이 이 작업 뒤 옛 기준이 됨 | ③ Plan은 이미 「② 완료 뒤 확인(C-1)」로 쓰였다. 이 작업 완료 보고에 바뀐 구조(프로필 이름·토큰·GroundBurst 위치 규칙)를 적어 ③ C-1의 입력으로 쓴다 |
| K-9 | 타워 writer fixture는 `AudioManager`를 격리하지 않음 | 호출은 `?.`이고 클립이 없어 아무 소리도 나지 않는다(Research 8절) |

---

## 11. 검증 방법

1. **컴파일:** Unity Console 오류 0.
2. **회귀 스위트:** 메뉴 `Hexiege/Combat/Run Full Regression Suite`(사용자 실행). WORKFLOW [6]: 사용자가 실패를 따로 말하지 않으면 PASS로 기록하되 「사용자 무언급에 의한 간주 · 결과 파일 없음(Console만)」 메모를 남긴다. 이 PASS는 회귀 스위트 한 축만이다.
3. **정적 확인(game-programmer 구현 보고에 포함):** 8절 diff 조건 · 게이트 문자열 5개 존재 · 에셋 새 행 GUID 4개가 기존 행과 같음.
4. **실기(사용자 육안 — 회귀 스위트와 별도 축):**

| # | 장면 | 기대 |
|---|------|------|
| S-1 | 싱글, Human 대포 타워 공격 | 포구에서 발사 이펙트 · 포탄 비행 · 착탄 이펙트 — 개편 전과 같음. 소리 없음(클립 없음) |
| S-2 | 싱글, RuneSpire 공격 · 대상 이동 | 크리스털 높이 시작 · 살아 있는 대상 가슴 높이 추적 — 개편 전과 같음 |
| S-3 | 싱글, VineTower 공격 | 공격 연출 없음(지금과 같음) · 피해·HP 숫자 정상 · Console 오류 없음 |
| S-4 | 멀티 Host + Client(Client 화면 뒤집힘) | S-1·S-2가 양쪽 화면에서 개편 전과 같음 |

5. **공격음 청취 확인은 이 작업에서 불가** — 클립이 없다. 호출 지점이 실제로 불리는지는 ④에서 클립을 넣은 뒤 듣는다. (임시 클립으로 미리 확인할지는 13절 A-10.)

---

## 12. 담당 · 순서 · 다른 task와의 접점

| 단계 | 담당 |
|------|------|
| 구현(4절 전부 · 5절 E-1 · 7절) | **game-programmer** |
| 구현 결과 검토 | 메인 세션(CLAUDE.md 규칙 3 — 3파일 이상) |
| QA | qa-tester — 사용자가 명시적으로 지시한 경우에만(WORKFLOW [5] 「TC·QA는 사용자 명시 지시 때만, 먼저 제안·질문 금지」) |
| 회귀 스위트 · 실기 | 사용자 |
| 문서·메모리 반영([11] — 9절 개정 승인분, VFXSFXList, 메모리) | document-manager |

**순서(결정 5):** ① `13_39` 효과음 1단계 → **② 이 작업** → ③ `14_40` VineTower 연결 → ④ 효과음 2단계.

### 12-1. 겹치는 파일

| 파일 | ① 13_39 | ② 이 작업 | ③ 14_40 | ④ |
|------|:------:|:--------:|:------:|:--:|
| `Presentation/Audio/AudioManager.cs` | 동시 재생 개편 · 우선 인자를 받는 `PlaySfxClip`(효과음 Plan 6-2) | `PlayTowerAttackSfx` 추가 — ①의 입구를 「우선」으로 호출(6-3) | — | — |
| `Infrastructure/Config/SoundConfig.cs` | `UnitSoundEntry`에 우선 재생 표시 칸 + 읽기 메서드(효과음 Plan 6-2 · 8절) | 새 `TowerAttackSoundEntry` · 목록 · 조회(6-1) — 같은 파일의 **다른 구조체** | — | — |
| `Resources/Config/SoundConfig.asset` | 끊긴 참조 정리(RM-3) · 제작분 연결 · 우선 표시(효과음 Plan 8절) | 바꾸지 않음(6-2) | — | 타워 행 추가: Human = `sfx_cannon_attack`, RuneSpire = 전용(효과음 Plan 10절) |
| `GameSystemRules_Sound.md` | 규칙 13 | 규칙 3·15·16 초안 | — | — |
| `Presentation/Effects/EffectManager.cs` | — | 공격 방식 분기 | 새 코드 없음(VineTower Plan 4절) | — |
| `Presentation/Effects/BuildingEffectConfig.cs` | — | 프로필 구조 | — | — |
| `Resources/Config/BuildingEffectConfig.asset` | — | Human·Spirit 행 | VineTower 행 추가(C-2) | — |
| `Presentation/Effects/HitPresentationQueue.cs` | — | 412·416행 · 공격음 | — | — |
| `RunUnitActionSelfValidation.VfxConnections.cs` | — | V-1~V-6 | V-3이 데이터 기준이면 개정 불필요 | — |
| `GameSystemRules_Buildings.md` 규칙 12 | — | B-1~B-4 | G-1~G-4(②반영 뒤 문장 기준) | — |
| `Assets/VFXSFXList.md` | SFX 절 | 142~144행 | 144~151행 | SFX 절 |
| `ProjectilePresentationPresenter.cs` | — (효과음 Plan 8절 파일 목록에 없음 · 12절 「`UnitView.cs` 발사음 자리 — 코드 수정 없음」) | 줄 추가 | — | — |
| `Prefabs/VFX/Units/vfx_pistoleer_attack.prefab`(Human 타워 발사 VFX로도 쓰임) | AudioSource 제거(효과음 Plan RM-1) | 바꾸지 않음 — 프리셋 참조만 새 프로필 행으로 옮김 | — | — |

### 12-1-1. 효과음 Plan 10절과의 역할 분담

| 할 일 | 담당 | 비고 |
|------|------|------|
| 우선 인자를 받는 재생 입구 | ① | 효과음 Plan 6-2 |
| `SoundConfig` 타워 공격음 칸 · `AudioManager.PlayTowerAttackSfx` · 호출 지점 P-1~P-3 | **② 이 작업** | 효과음 Plan 10절 시작 조건 「타워 task(②)가 끝나 … 칸과 … API가 있다」와 같음 |
| 칸에 클립 연결(Human `sfx_cannon_attack`, RuneSpire 전용) | ④(효과음 2단계) | 효과음 Plan 8절 「에셋(2단계)」 · 10절 |
| VineTower 공격음 | ③ 확정 뒤 ④ | 두 Plan 같음 |
| 2단계 문서(`VFXSFXList.md` 타워 공격음 행 · `UnitAttackProfile.md` 타워 행) | ④의 document-manager | 이 Plan 9절의 [11] 후보는 VFX 행(142~144행)만이다 |

겹치거나 빠진 곳(보고 — 효과음 Plan은 이 작업에서 고치지 않는다):
- **볼륨 칸:** 효과음 Plan 10절은 「타워 칸에 같은 파일」만 적고 `attackVolume`을 말하지 않는다. 이 Plan 6-2대로 ②는 행을 만들지 않으므로, ④가 행을 만들 때 **클립과 볼륨을 함께** 넣어야 한다(Unity가 새 행 볼륨을 0으로 넣는다 — 지금 `_buildingSoundEntries` 행도 0).
- **옛 자리 서술:** 효과음 Plan 10절 첫 bullet은 타워 발사 VFX 자리로 `EffectManager.PlayBuildingAttack`(527~535행)을 든다. 이 메서드는 호출처가 없는 경로이고 이 작업이 끈다(RM-2). 효과음 Plan은 같은 bullet에서 「연결 자리는 타워 task가 정한다」고 했으므로 충돌은 없다. 다만 ② 구현 뒤 그 서술은 옛 기준이 된다 — 실제 연결 자리는 이 Plan 6-3 P-1·P-2다.
- **폴더 상태 표기:** 효과음 Plan 12절의 이 task 칸 「작성 중, 폴더 미생성」은 지금 사실과 다르다(폴더·Research·Plan 있음).
- 빠진 일은 없다: API·칸·호출 지점(②), 입구(①), 클립·볼륨(④)이 한 번씩만 배정돼 있다.

### 12-2. VineTower Plan(③)과 맞춘 경계

| ③이 전제한 것 | 이 Plan |
|-------------|--------|
| X-1~X-4 차단 4곳을 ②가 처리 | 1절 RM-6·RM-8·RM-9·RM-10으로 모두 처리 |
| GroundBurst = 스냅샷 Impact(대상 발밑)에 한 번 놓고 대상 화면 위치를 다시 읽지 않음 | 3-3 (가) GroundBurst 행 · (다) — 그대로. 회전 = 타워→대상 수평(지금 561~562행과 같음 — ③ K-9 입력) |
| 「프로필이 비면 재생 안 함」 검증 | V-3(데이터 기준) · V-4 (g) |
| 사운드 규칙 15 「공격음 미제작」 주석 위치(③ U-6) | 6-3 — 호출 줄(P-1·P-2) 위 주석 |
| ③ 4절·5-6 C-2의 「프로필의 공격음 칸」 | ⚠️ **다르다:** 결정 4로 공격음 칸은 프로필(`BuildingEffectConfig`)이 아니라 `SoundConfig`에 있다. ③ C-2의 「공격음 비움」은 「`SoundConfig` 타워 공격 SFX에 VineTower 행을 두지 않음」으로 읽어야 한다 — ③ Plan 정정은 ③ 쪽 일(이 작업은 그 문서를 고치지 않음) |
| ③ C-2 「VineTower 행 추가」 | 3-2 — ②는 VineTower 행을 두지 않음(A-3) |

---

## 13. 사용자 승인 필요 항목

| # | 항목 | 이 Plan의 제안 | 결정 (사용자 2026-11-02 「추천대로 승인」) |
|---|------|-------------|------|
| **A-1** | 이 Plan 전체 승인(구현 착수) | — | 승인 |
| **A-2** | 공격 방식 열거형에 「비어 있음(`Unset = 0`)」 값을 둘지 | 둔다 — 새 행의 기본값이 실수로 「투사체」가 되지 않게 | 승인 — 둔다 |
| **A-3** | VineTower 프로필 행: (가) 두지 않음 / (나) 공격 방식만 GroundBurst로 두고 프리셋 비움 | (가) — ③ C-2 「행 추가」와 맞음. 검증은 데이터 기준(V-3)이라 어느 쪽이든 동작은 같다 | 승인 — (가) VineTower 프로필 행을 두지 않음 |
| **A-4** | 스냅샷 경로에 온 투사체(Human) 프로필은 타워 위치에서 발사 프리셋 재생(지금 동작 유지 · 검증 (f) 유지) | 유지 | 승인 — 유지 |
| **A-5** | 타워 공격음을 VFX 재생 성공 여부와 무관하게 호출(유닛 발사음과 같은 방식), 그리고 ① 완료 뒤에만 착수 | 그렇게 | 승인 — 그렇게 |
| **A-6** | `SoundConfig.asset` 타워 공격 SFX 목록을 비워 두기(④에서 클립·볼륨 함께) | 비워 둠 | 승인 — 비워 둠 |
| **A-7** | 에셋 이전(E-1)을 누가 할지 — game-programmer YAML 편집 / 사용자 Inspector | 사용자 선택 | **1회성 에디터 메뉴**로(WORKFLOW [5-2] — 효과음 Plan Q-2와 같은 결정) |
| **A-8** | 9절 규칙 개정 초안 B-1~B-4 · S-1~S-3 (반영은 [11]) | 초안대로 | 승인 — 초안대로. 반영 시점은 이 task 구현 · 사용자 테스트 뒤 [11] |
| **A-9** | `TowerCombatUseCase.cs` 286행 「Sound15」 주석을 건드리지 않음 | 그대로 둠 | 승인 — 그대로 둠 |
| **A-10** | 공격음 호출 확인을 임시 클립으로 이 작업 안에서 할지, ④로 미룰지 | ④로 미룸 | 승인 — ④로 미룸 |
| **A-12** | 새 진단 결과 토큰 이름 `TowerProfileEmpty`(종전 `UnspecifiedTranscendence`) | 그 이름 | 승인 — `TowerProfileEmpty` |
| **A-13** | V-5(GroundBurst 임시 fixture)를 이 작업에서 넣을지 — 데이터가 없는 경로를 미리 검증 | 넣음 | 승인 — 넣음 |

---

## 14. 변경 이력

| 날짜 | 내용 |
|------|------|
| 2026-11-02 | 최초 작성(사용자 결정 「추천대로」 반영, VineTower Plan 0·4·5-6절 경계 대조). 승인 대기 |
| 2026-11-02 | 효과음 Plan(`13_39` Plan 6·8·10·12절) 대조 반영: 6-3 `PlayTowerAttackSfx`가 ①의 `PlaySfxClip`(우선 인자 추가형)을 「항상 우선」으로 부른다고 확정(인자 이름·타입은 ① 구현 서명을 따름) · 12-1 표의 `SoundConfig.cs`·`ProjectilePresentationPresenter.cs` ① 칸 확정, `SoundConfig.asset`·`AudioManager.cs` ① · ④ 칸 구체화, `vfx_pistoleer_attack.prefab` 행 추가 · 12-1-1 역할 분담과 보고 3건(④ 볼륨 칸, 효과음 Plan 10절의 옛 자리 서술, 12절 폴더 표기) 추가 · K-7 보강 · 승인 목록과 11절에서 A-11(Testcase.md 작성 여부)을 삭제 — WORKFLOW [5-1]·[5]의 「TC 작성은 사용자 명시 지시가 있을 때만, 먼저 제안·질문도 금지」에 어긋남(번호는 비워 둠). 같은 이유로 12절 담당 표의 「QA(필요 시)」를 「사용자 명시 지시 때만」으로 고침 |
| 2026-11-02 | **사용자 승인 「추천대로 승인」**(메인 세션 전달) — 13절에 「결정」 열 추가(종전 칸 그대로): A-1~A-10 · A-12 · A-13 모두 Plan 제안대로, A-7(제안 「사용자 선택」)은 1회성 에디터 메뉴. 9절 B-1~B-4 · S-1~S-3 문구도 승인 — 반영은 구현 · 테스트 뒤 [11]. 상단 상태 줄 갱신 |
| 2026-11-03 | **구현 · 사용자 테스트 · 최종 삭제 · [11] 문서 반영**(document-manager, 메인 세션 전달) — 15절 신설. 9절 B-1~B-4 · S-1~S-3을 `GameSystemRules_Buildings.md` 규칙 12 · `GameSystemRules_Sound.md` 규칙 3 · 15 · 16에 반영(종전 문장은 날짜 조각으로 보존). 상단 상태 줄 갱신 |
| 2026-11-03 | 15절 끝에 「머지 뒤 확인」 한 줄(지원 스킬 머지 `5669e170` 뒤 회귀 스위트 PASS · 푸시 `da5c561b`), 15절 머리 「미커밋」 → 커밋 `d719fa53` 정정 |

---

<a id="15-implementation-and-user-test"></a>
## 15. 구현 결과 · 사용자 테스트 · 최종 삭제 (2026-11-03)

> **출처:** 구현 사실은 game-programmer 보고(2026-11-03, 메인 세션 전달) **[전달]**이며, 아래 [확인] 표시 줄은 이 기록 중 document-manager가 작업 트리 파일을 직접 읽어 대조했다. 사용자 테스트 결과는 메인 세션 전달 **[전달]**. 이 작업의 코드 · 에셋 · 문서는 커밋 `d719fa53`에 들어갔다 *(2026-11-03 갱신 — 직전 「이 시점 이 작업의 코드 · 에셋 변경은 **미커밋**이다(`git status` — 새 파일 `TowerAttackProfile.cs` 미추적, 나머지 수정).」)*.

### 15-1. 들어간 것 — 어디에

| 계획 | 실제 | 확인 |
|---|---|---|
| 4-1 프로필 데이터 | `Assets/_Project/Scripts/Presentation/Effects/TowerAttackProfile.cs` — `TowerAttackKind { Unset, Projectile, Beam, GroundBurst }` · `TowerAttackAnchor { Source, Impact, Muzzle }` · 구조체 `TowerAttackProfile`(`race` · `kind` · `anchor` · `launchPreset` · `tracerPreset` · `impactPreset`, 비어 있음 = 세 프리셋이 모두 없음) | [확인] |
| 4-2 설정 · 조회 | `BuildingEffectConfig` `_towerAttackProfiles` · `TryGetTowerProfile` · `GetTowerAttackKind`. 기존 공개 조회 `GetAttack(BuildingType, RaceId)` · `GetTowerTracer` · `GetTowerImpact` 서명 유지(속만 프로필) | [확인] |
| 4-3 재생 분기 | `EffectManager.PlayAttackEffect` — 프로필 없음 → `TowerProfileEmpty`, 공격 방식에 따라 기준점 재생, 번개만 `BeginTowerBolt`. 새 조회 `TowerAttackNeedsTargetTracking`(Beam). 서버 투사체 세 메서드는 이름 · 인자 그대로 | [확인] |
| 4-4 큐 | `HitPresentationQueue` — 대상 해석 조건 = `TowerAttackNeedsTargetTracking`, 실패 제외 토큰 = `TowerProfileEmpty`, P-2 공격음 호출 | [확인] |
| 4-5 Presenter | P-1 공격음 호출 · P-3 「착탄 SFX 없음」 주석 — 줄 추가만(삭제 줄 0, 전달) | [확인 — 호출 · 주석 존재] / 삭제 줄 0은 [전달] |
| 6 공격음 | `SoundConfig` `TowerAttackSoundEntry { buildingType, race, attackClip, attackVolume }` · `_towerAttackSoundEntries`(비어 있음) · `GetTowerAttackClip/Volume`. `AudioManager.PlayTowerAttackSfx(BuildingType, RaceId)` → `PlaySfxClip(..., SfxPriority.High)` | [확인]. `SoundConfig.asset`에는 아직 `_towerAttackSoundEntries` 키 줄이 없다(에셋이 새 필드로 다시 저장되지 않음 — 코드 기본값 빈 목록) |
| 5 에셋 이전 | `BuildingEffectConfig.asset` `_towerAttackProfiles` 2행 — race 0: kind 1(Projectile) · anchor 2(Muzzle) · 프리셋 GUID 3개(`1ddf58ca…` · `d6770727…` · `f1b34352…`), race 1: kind 2(Beam) · anchor 0(Source) · `a01e8a0e…` 1개. 3-2 표와 같다 | [확인 — 에셋 15~27행] |
| 7 검증 | `RunUnitActionSelfValidation.VfxConnections.cs` V-2 · V-3(데이터 기준) · K-1 · (g) · V-5 GroundBurst 임시 fixture · V-6 | [확인 — 표시 문자열 존재] |
| 8 바꾸지 않는 것 | 서버 파일 · C-6 게이트 문자열 무변경 | [전달] |

**계획 밖 판단(game-programmer, 2026-11-03):**
- 새 실패 토큰 `UnsetAttackKind` — 프리셋은 있는데 공격 방식이 `Unset`인 줄은 「어떻게 재생할지 모르는」 설정 오류로 보고 실패로 센다. 3-1의 「비어 있음」 정의(세 프리셋이 모두 없음)는 그대로이고, 그 정의에 들지 않는 `Unset` 줄을 위한 토큰이다 [확인 — `EffectManager.cs` `PlayAttackEffect`].
- 설정 에셋 자체가 없으면(`_buildingConfig == null`) `MissingConfiguredPreset`(실패) — 빈 프로필과 구분 [확인].
- 에셋 이전(E-1)은 13절 A-7 결정대로 **1회성 에디터 메뉴**로 했다. 그 메뉴 파일 `TowerAttackProfileOneTimeSetup.cs`는 사용자 테스트 뒤 퇴역(삭제)됐고 **한 번도 커밋되지 않았다** — `git log --all`에 그 이름의 기록이 없다 [확인]. 그래서 저장소에는 그 메뉴의 흔적이 남지 않는다.
- 진단 토큰 변경: `UnspecifiedTranscendence` → `TowerProfileEmpty`(A-12 승인). 옛 로그와 새 로그의 표기가 다르다(K-6). `LogRules.md`에는 이 토큰 목록이 없다(Research 11절 4).

### 15-2. 사용자 테스트 [6] (2026-11-03) [전달]

| 축 | 결과 |
|---|---|
| 1회성 메뉴 실행 결과 | 모두 ✓ — 메인 세션이 에셋의 `_towerAttackProfiles` 2줄을 직접 확인(race 0 Projectile/Muzzle GUID 3개, race 1 Beam/Source GUID 1개) |
| 회귀 스위트(`Hexiege/Combat/Run Full Regression Suite`) | **PASS — 최종 삭제 전 실행.** 결과 파일 없음(Console만), 에이전트 미관측(WORKFLOW [6]) |
| 빌드 멀티 실기(S-4) | **PASS** — Human 대포 타워 · RuneSpire가 양쪽 화면에서 개편 전과 같음. VineTower는 연출 없이 피해만(S-3과 같은 기대) |
| 싱글 S-1~S-3 | 따로 보고 없음 — 위 멀티 실기에서 같은 장면을 봤다 |
| 타워 공격음 | 클립이 없어 무음 — 정상(A-10, 11절 5) |
| **최종 삭제 뒤 회귀 스위트 재실행** | **PASS** — 사용자 「회귀 테스트는 정상이야」(2026-11-03, 명시 보고). 결과 파일 없음(Console만) · 에이전트 미관측 *(2026-11-03 갱신 — 직전 「**미확인** — 사용자가 진행 예정. 결과를 받으면 이 줄을 고친다」)* |

### 15-3. 최종 삭제 (WORKFLOW [4] — 사용자 테스트 통과 뒤, [7] 전) [전달]

1절 RM-1~RM-10의 옛 주석 블록 전부, RM-7의 `_autoTowerAttacks` · `_autoTowerProjectiles` 필드와 `RaceAttack` · `RaceProjectile` 구조체, `BuildingEffectConfig.asset`의 두 YAML 블록, 1회성 메뉴(15-1). 옛 값은 커밋 `7368d7f1`의 에셋에 남아 있다. 지금 코드에는 `BuildingEffectConfig.cs` 44 · 54~55행, `EffectManager.cs` 518행에 「삭제했다」는 안내 주석만 있다 [확인]. 컴파일은 Unity 밖 Roslyn으로 오류 0 [전달] — 삭제 뒤 회귀 스위트는 PASS(15-2), 그 실행이 Unity Editor 컴파일을 거쳤다. *(2026-11-03 갱신 — 직전 「Unity Editor 컴파일 · 삭제 뒤 회귀 스위트는 15-2의 미확인 줄.」)*

### 15-4. [11] 문서 반영 (이 기록과 같은 회차)

| 문서 | 반영 |
|---|---|
| `GameSystemRules_Buildings.md` 「방어 타워 시스템」 규칙 12 | B-1 · B-2(첫 문단) · B-3(2026-10-20 개정 문단 + 공격음 문장) · B-4(2026-10-13 블록 적용 범위). 2026-10-21 괄호의 `_autoTowerProjectiles` 언급 뒤에 이동 사실 조각 |
| `GameSystemRules_Sound.md` | S-1(규칙 3 키에 `RaceId`) · S-2(규칙 15 끝) · S-3(규칙 16 엔트리 예시) |
| `Assets/VFXSFXList.md` | 「건물 공격 이펙트」 Human · RuneSpire · VineTower 행, 「신규 프리팹 연결 실측」 RuneSpire 칸, VineTower 계획 블록의 옛 `EffectManager.cs` 529행 문장(VineTower Plan 9절 G-5 · G-7 중 이 시점 해당분 — 행 번호 정정 · 분기 소멸), 「유닛 공격 사운드」 끝 타워 공격음 줄 |
| `Assets/UnitAttackProfile.md` | 3-2 타워 행의 「소리 나는 순간」, 머리 현재 상태 |
| VineTower Plan · 효과음 Plan | 상태 줄 · 접점 서술 날짜 조각(그쪽 변경 이력) |
| `PROJECT_STATUS.md` · `ROADMAP.md` · `WORK_HISTORY.md` | 현재 상태 행 · 남은 일 · 완료 행 |

Research.md 본문의 행 번호는 기준 커밋 `7368d7f1` 기준이며 구현 뒤 옛 기준이다 — 고치지 않는다(이력).

### 15-5. 남은 일 (이 task 범위 밖)

- **③ VineTower 공격 VFX:** 지면 분출(GroundBurst) 재생 경로와 V-3 · V-5 검증은 있고 VineTower 프로필 행은 없다. 덩굴 모델 제작은 Unity AI 크레딧 소진으로 보류 — [VineTower Plan](../14_40_vinetower-attack-design/Plan.md). 행을 채우면 코드 변경 없이 재생된다(V-3이 데이터 기준).
- **④ 타워 공격음 클립 연결:** `SoundConfig` 타워 공격 SFX 목록에 Human(`sfx_cannon_attack`) · RuneSpire(전용) 행 — 클립과 볼륨을 함께([효과음 Plan](../13_39_unit-attack-sfx-tiering/Plan.md) 10절).
- **효과음 1단계의 연기된 일**(① — 큰 전투 청취 · 옛 코드 삭제)은 이 task와 별개 — 효과음 Plan 17-5.
- RuneSpire 번개 끝점 OPEN(결정 7 — 제외)은 그대로다.

**머지 뒤 확인 (2026-11-03, [전달] — 커밋 해시는 `git log`로 확인):** 원격 지원 스킬 커밋 `56d5ab2a` · `c4cbf8ab`를 머지(`5669e170`, 충돌은 `EffectManager.cs` 1곳 — 원격에 다시 나온 옛 `PlayBuildingAttack`은 버리고 지원 스킬의 `PlaySkillActivationVfx` · `ComputeSkillVfxScale`은 유지, 메인 세션 해결 · Unity 밖 Roslyn 컴파일 오류 0)한 뒤 회귀 스위트 PASS(사용자 「PASS됐어」, 결과 파일 없음 — Console만), 푸시 `origin/claude/vfx-code-followups` = `da5c561b`. 머지로 서버 투사체 검증 파일의 스키마 기대값이 6 → 7로 바뀐 것은 지원 스킬 쪽 변경이며 타워 검증 · C-6 게이트는 바뀌지 않았다.

