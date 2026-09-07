// ============================================================================
// MapRandom.cs
// 무작위 맵 생성 전용 "결정적 난수 발생기"(PRNG).
//
// ─────────────────────────────────────────────────────────────────────────────
// 왜 C# 기본 난수를 쓰지 않는가 (초급자용 설명)
// ─────────────────────────────────────────────────────────────────────────────
//   우리가 지켜야 하는 약속은 하나다 — "같은 seed 를 넣으면 언제 어디서 돌려도
//   똑같은 맵이 나온다". 이 약속이 있어야 버그 재현이 되고, 나중에 멀티플레이에서
//   Host 와 Client 가 같은 맵을 갖고 있는지 대조할 수 있다.
//
//   그런데 아래 네 가지는 이 약속을 지키지 못한다.
//
//   1) .NET 표준 라이브러리의 난수 클래스 (System 네임스페이스에 있는 Random)
//      내부 알고리즘이 .NET 런타임 버전에 따라 바뀐 전례가 있다. Unity 를 업그레이드
//      하거나 IL2CPP/Mono 를 바꾸는 것만으로 같은 seed 가 다른 수열을 낼 수 있다.
//   2) Unity 엔진이 제공하는 정적 난수 (Unity 엔진 네임스페이스에 있는 Random)
//      전역 상태 하나를 온 게임이 공유한다. 다른 시스템이 난수를 한 번 더 뽑기만 해도
//      맵 생성 결과가 통째로 달라진다. 게다가 알고리즘이 문서로 고정되어 있지 않다.
//   3) 런타임 문자열 해시 / object 의 기본 해시 코드 메서드
//      .NET 은 보안상의 이유로 문자열 해시에 프로세스마다 다른 임의값을 섞는다.
//      즉 "같은 프로그램을 두 번 실행하면 값이 다르다". 절대 seed 재료로 쓸 수 없다.
//
//   4) float/double 연산
//      부동소수점 반올림은 플랫폼·컴파일러 최적화에 따라 미세하게 달라질 수 있다.
//      그래서 이 파일은 처음부터 끝까지 정수(ulong)만 쓴다.
//
//   ※ 위 금지 API 들을 코드 형태(점 표기)로 적지 않은 것은 의도적이다 —
//      "이 프로젝트에 금지 API 가 남아 있는가" 를 grep 으로 확인할 때
//      이 설명 주석이 잘못 걸리지 않게 하기 위함이다(.claude/mistakes.md 2026-09-02).
//
//   근거: TechnicalDesignDocument.md 「결정적 PRNG 및 독립 스트림 계약」
//         ("PRNG 알고리즘, 정수 폭, 오버플로 규칙, seed 파생 순서를
//           프로젝트 코드와 테스트 벡터로 고정한다")
//
// ─────────────────────────────────────────────────────────────────────────────
// 채택 알고리즘: SplitMix64
// ─────────────────────────────────────────────────────────────────────────────
//   고른 이유는 네 가지다.
//
//   * 상태가 64비트 정수 "하나"뿐이다. 스트림을 여러 개 만들어도 각자 ulong 하나만
//     들고 있으면 되므로, 스트림끼리 상태가 섞일 여지가 구조적으로 없다.
//   * 전진식(state += 고정 상수)과 뒤섞기식(finalizer)이 곱셈·XOR·시프트뿐이라
//     정수 폭만 고정하면 어떤 플랫폼에서도 결과가 완전히 같다.
//   * 같은 이유로 손계산(또는 파이썬)으로 기대값을 뽑아 테스트 벡터로 박아 둘 수
//     있다. 실제로 이 파일의 기대값은 그렇게 만들었다(MapRandomStreams 의 자기 검증).
//   * seed 를 파생시키는 "섞기 함수"로도 그대로 재사용할 수 있어, PRNG 와 seed 파생이
//     서로 다른 두 알고리즘이 되는 일을 피할 수 있다.
//
//   고정 규격 (바꾸면 과거 seed 의 맵이 재현되지 않는다 — 바꿀 때는 MapVersion 을 올린다):
//     정수 폭      : 전부 64비트 부호 없는 정수(ulong)
//     오버플로 규칙: unchecked — 64비트를 넘는 자리는 그냥 버린다(2^64 나머지 연산)
//     전진식       : state = state + 0x9E3779B97F4A7C15
//     뒤섞기       : z ^= z >> 30; z *= 0xBF58476D1CE4E5B9;
//                    z ^= z >> 27; z *= 0x94D049BB133111EB;
//                    z ^= z >> 31
//     ※ ulong 의 >> 는 언제나 논리 시프트(빈자리에 0)라 부호 확장 문제가 없다.
//        int 로 같은 식을 쓰면 >> 가 산술 시프트가 되어 결과가 달라진다.
//
// Domain 레이어 — 순수 C#, Unity/Core 의존 없음(양쪽 다 참조하지 않는다).
// (TDD 「생성·검증 실행 모델」: generator/validator 는 Unity API 에 의존하지 않는다)
// ============================================================================

using System;
using System.Collections.Generic;

namespace Hexiege.Domain
{
    /// <summary>
    /// 무작위 맵 생성에 쓰는 결정적 PRNG. 상태는 64비트 정수 하나이며,
    /// 인스턴스마다 자기 상태를 따로 갖는다(= 스트림끼리 상태를 공유하지 않는다).
    /// </summary>
    public sealed class MapRandom
    {
        // ====================================================================
        // 고정 상수 — 이 값을 바꾸면 과거 seed 의 맵이 재현되지 않는다
        // ====================================================================

        /// <summary>
        /// SplitMix64 의 상태 전진량(황금비 기반 홀수 상수).
        /// 홀수라서 2^64 주기를 한 바퀴 도는 동안 같은 상태를 두 번 만들지 않는다.
        /// </summary>
        public const ulong Gamma = 0x9E3779B97F4A7C15UL;

        /// <summary> SplitMix64 finalizer 1단계 곱수. </summary>
        private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;

        /// <summary> SplitMix64 finalizer 2단계 곱수. </summary>
        private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;

        // ====================================================================
        // 상태
        // ====================================================================

        /// <summary>
        /// 이 스트림의 현재 상태. 인스턴스 전용이므로 다른 스트림에 영향을 주지 않는다.
        /// </summary>
        private ulong _state;

        /// <summary> 지금까지 이 스트림에서 뽑은 64비트 난수의 개수(진단용). </summary>
        public long DrawCount { get; private set; }

        /// <summary>
        /// 지정한 seed 로 새 스트림을 만든다.
        /// </summary>
        /// <param name="seed">이 스트림의 시작 상태(보통 MapRandomStreams 가 파생해 준 값)</param>
        public MapRandom(ulong seed)
        {
            _state = seed;
            DrawCount = 0;
        }

        // ====================================================================
        // 핵심 정수 연산 — 순수 함수(상태 없음)
        // ====================================================================

        /// <summary>
        /// SplitMix64 의 뒤섞기 함수(finalizer). 입력 비트 하나가 바뀌면 출력의 절반쯤이
        /// 바뀌도록 흩어 주는 순수 함수이며, PRNG 와 seed 파생 양쪽에서 같은 것을 쓴다.
        /// </summary>
        /// <param name="z">뒤섞을 64비트 값</param>
        /// <returns>뒤섞인 64비트 값</returns>
        public static ulong Mix64(ulong z)
        {
            // unchecked: 곱셈이 64비트를 넘칠 때 예외를 던지지 말고 잘라내라는 뜻.
            //            이 "잘라냄"이 알고리즘의 일부이므로 반드시 명시한다.
            unchecked
            {
                z ^= z >> 30;
                z *= MixMultiplier1;
                z ^= z >> 27;
                z *= MixMultiplier2;
                z ^= z >> 31;
                return z;
            }
        }

        /// <summary>
        /// 두 정수를 섞어 새 64비트 값을 만든다. seed 파생의 유일한 재료 함수다.
        /// (salt + 1) 을 쓰는 이유: Mix64(0) 은 0 이라, salt 가 0 일 때도 값이 움직이게 하기 위함.
        /// </summary>
        /// <param name="seed">바탕이 되는 값</param>
        /// <param name="salt">구분자(스트림 ID, 시도 번호 등)</param>
        /// <returns>파생된 64비트 값</returns>
        public static ulong Combine(ulong seed, ulong salt)
        {
            unchecked
            {
                return Mix64(seed + Gamma * (salt + 1UL));
            }
        }

        // ====================================================================
        // 뽑기 연산
        // ====================================================================

        /// <summary>
        /// 64비트 난수를 하나 뽑고 상태를 한 칸 전진시킨다. 다른 모든 뽑기의 바탕이다.
        /// </summary>
        /// <returns>0 이상 ulong.MaxValue 이하의 값</returns>
        public ulong NextUInt64()
        {
            unchecked
            {
                _state += Gamma;      // 먼저 전진 → seed 가 0 이어도 첫 결과가 0 이 되지 않는다
                DrawCount++;
                return Mix64(_state);
            }
        }

        /// <summary>
        /// 0 이상 exclusiveMax 미만의 정수를 "편향 없이" 균등하게 뽑는다.
        /// </summary>
        /// <param name="exclusiveMax">뽑을 수 있는 값의 개수(1 이상). 결과는 0 ~ exclusiveMax-1</param>
        /// <returns>0 이상 exclusiveMax 미만의 정수</returns>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax),
                    "뽑을 수 있는 값의 개수는 1 이상이어야 한다.");
            }

            // ── 왜 그냥 (난수 % exclusiveMax) 로 하면 안 되는가 ──────────────
            // 64비트 난수의 가짓수 2^64 는 대부분의 exclusiveMax 로 나누어떨어지지
            // 않는다. 예를 들어 가짓수가 3 이면 0/1/2 중 하나가 딱 한 번 더 나올 수
            // 있는 구간이 남고, 그만큼 그 값이 더 자주 뽑힌다("모듈로 편향").
            // 규칙 3·규칙 5 등이 "동일 확률"을 명시하므로 이 편향을 허용할 수 없다.
            //
            // 해결책: 나누어떨어지지 않고 남는 앞쪽 구간(threshold 미만)을 통째로
            //         버리고 다시 뽑는다. 남은 구간의 크기는 exclusiveMax 의 배수이므로
            //         나머지 연산이 정확히 균등해진다.
            //         버려질 확률은 exclusiveMax / 2^64 로, 실질적으로 0 에 가깝다.
            unchecked
            {
                ulong bound = (ulong)exclusiveMax;
                ulong threshold = (ulong.MaxValue - bound + 1UL) % bound;  // = 2^64 mod bound

                ulong r = NextUInt64();
                while (r < threshold)
                {
                    r = NextUInt64();
                }
                return (int)(r % bound);
            }
        }

        /// <summary>
        /// minInclusive 이상 maxExclusive 미만의 정수를 균등하게 뽑는다.
        /// </summary>
        /// <param name="minInclusive">하한(포함)</param>
        /// <param name="maxExclusive">상한(미포함). minInclusive 보다 커야 한다</param>
        /// <returns>minInclusive 이상 maxExclusive 미만의 정수</returns>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                    "상한은 하한보다 커야 한다.");
            }

            // long 으로 폭을 넓혀 계산한다. int 로 빼면 범위가 아주 넓을 때 오버플로가 난다.
            long width = (long)maxExclusive - minInclusive;
            if (width > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                    "한 번에 뽑을 수 있는 폭이 int 범위를 넘었다.");
            }
            return minInclusive + NextInt((int)width);
        }

        /// <summary>
        /// 후보 목록에서 하나를 동일 확률로 고른다.
        /// (예: 협곡형의 통로 폭 후보 3·5·7 중 하나)
        /// </summary>
        /// <param name="candidates">후보 값 목록(1개 이상)</param>
        /// <returns>고른 값</returns>
        public int Choose(IReadOnlyList<int> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0)
            {
                throw new ArgumentException("후보가 비어 있어 고를 수 없다.", nameof(candidates));
            }
            return candidates[NextInt(candidates.Count)];
        }

        /// <summary>
        /// 참/거짓을 50:50 으로 뽑는다(예: 시작 광산 방향 A/B).
        /// 최상위 비트를 쓰는 이유: SplitMix64 출력에서 상위 비트가 가장 잘 섞여 있다.
        /// </summary>
        /// <returns>50% 확률로 true</returns>
        public bool NextBool()
        {
            return (NextUInt64() >> 63) != 0UL;
        }
    }
}
