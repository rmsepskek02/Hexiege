// ============================================================================
// SfxNormalizeCore.cs  (영구 보존 도구 — Assets/Editor/Tools/, 삭제 금지)
//
// 무엇을 하는가 (2026-10-27, 사용자 승인 기준):
//   Unity AI로 만든 효과음(wav)은 프롬프트에 "모노·짧게·-3dBFS"를 적어도 지키지 않는다
//   (스테레오로 나오고, 길이가 1.04초로 고정이며, 뒤쪽 약 0.5초가 무음이고, 파일마다 소리 크기가 제각각).
//   그래서 만든 뒤에 "후처리"로 같은 기준에 맞춘다. 이 파일은 그 계산만 담당하는 순수 C# 코드다.
//   실제 메뉴·파일 백업·재임포트는 SfxNormalizeTool.cs 가 한다.
//
// 왜 Unity API 를 쓰지 않는가:
//   WAV 읽기/쓰기·측정·게인 계산을 Unity 없이 돌릴 수 있어야, 에디터를 열지 않고도
//   (예: dotnet 콘솔) 같은 코드로 실제 파일을 측정해 검증할 수 있다. 그래서 System 네임스페이스만 쓴다.
//
// 처리 순서 (이 순서를 바꾸면 결과 크기가 달라진다):
//   1) 모노 다운믹스       — 모든 채널의 같은 시점 샘플을 평균한다(채널 평균).
//   2) 앞 무음 자르기      — "소리 시작" 직전 LeadPaddingMs 만큼만 남기고 앞을 잘라낸다.
//   3) 뒤 무음 자르기      — "소리 끝" 뒤 TailPaddingMs 만큼만 남기고, 마지막 FadeOutMs 는 0으로 서서히 줄인다
//                            (파형이 갑자기 끊기면 '딱' 하는 클릭 소리가 나기 때문).
//   4) 체감 크기 맞추기    — 「체감 크기」를 TargetLoudnessDb 로 맞추는 게인(배율)을 곱한다.
//   5) 피크 상한 지키기    — 게인을 곱한 뒤 가장 큰 샘플이 PeakCeilingDbfs 를 넘으면, 게인 자체를 낮춰
//                            피크가 상한에 딱 닿게 한다(이 경우 체감 크기는 목표보다 작아지고 경고를 남긴다).
//                            리미터·압축처럼 소리 모양을 바꾸는 처리는 하지 않는다.
//   6) 44100Hz · 16bit · 모노 PCM wav 로 저장할 바이트를 만든다.
//
// 측정 정의 (메인 세션 측정과 같은 정의 — 바꾸지 말 것):
//   - 샘플 값은 16bit 정수를 32768 로 나눈 -1.0 ~ +1.0 범위 실수로 다룬다(1.0 = 0 dBFS).
//   - 피크(peak)      = 모노 다운믹스 샘플 절댓값의 최댓값 → dB 로 표시.
//   - 체감 크기       = 50ms 길이의 창을 한 샘플씩 밀면서 잰 RMS(제곱평균제곱근)의 최댓값 → dB.
//                       "가장 시끄러운 50ms 구간이 얼마나 큰가" 를 뜻한다. 짧은 타격음끼리 비교하기에 알맞다.
//   - 소리 구간       = 피크 대비 SoundThresholdDb(-40dB)보다 큰 첫 샘플 ~ 마지막 샘플.
//
// 같은 파일에 두 번 돌려도 결과가 같은가(멱등):
//   - 2번째 실행에서는 이미 모노이고, 소리 시작 앞 여유가 정확히 LeadPaddingMs 라 앞을 자를 것이 없다.
//   - 뒤 여유·페이드 구간은 피크 대비 -40dB 아래(사실상 무음)라서 소리 끝 위치가 바뀌지 않는다.
//   - 체감 크기가 이미 목표와 거의 같으므로 게인이 0dB 근처가 되는데, GainDeadBandDb 안이면
//     게인을 정확히 1배로 두어 16bit 반올림 오차가 쌓이지 않게 한다.
//   - 결과 바이트가 원본과 완전히 같으면 Tool 쪽에서 파일을 다시 쓰지 않는다(NoChange).
// ============================================================================

using System;
using System.Globalization;

namespace Hexiege.EditorTools
{
    /// <summary>
    /// 효과음 wav 정규화(모노화·무음 트림·체감 크기 맞춤·피크 상한) 계산을 담당하는 순수 C# 정적 클래스.
    /// Unity API 에 의존하지 않는다.
    /// </summary>
    public static class SfxNormalizeCore
    {
        // ─────────────────────────────────────────────────────────────────
        //  기준값 (사용자 승인 2026-10-27) — 바꾸면 이미 처리한 파일과 기준이 달라진다.
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 목표 「체감 크기」(dB). 가장 시끄러운 50ms 구간의 RMS 를 이 값에 맞춘다.
        /// 예: 원래 -20.4dB 인 소리는 -5.6dB 만큼 줄이고, -26.3dB 인 소리는 +0.3dB 만큼 키운다.
        /// </summary>
        public const double TargetLoudnessDb = -26.0;

        /// <summary>
        /// 피크 상한(dBFS). 게인을 곱한 뒤 가장 큰 샘플이 이 값을 넘지 않게 한다.
        /// 0dBFS(16bit 최대값)에 붙으면 재생 장치·믹서에서 찌그러질 수 있어 1dB 여유를 둔다.
        /// </summary>
        public const double PeakCeilingDbfs = -1.0;

        /// <summary>「소리 구간」 판정 기준(dB). 피크보다 40dB 이상 작은 샘플은 "무음"으로 본다.</summary>
        public const double SoundThresholdDb = -40.0;

        /// <summary>소리 시작 앞에 남겨 두는 여유(ms). 공격음의 첫 타격(어택)이 잘리지 않게 한다.</summary>
        public const double LeadPaddingMs = 5.0;

        /// <summary>
        /// 소리 끝 뒤에 남겨 두는 여유(ms). 잔향 꼬리가 갑자기 잘리지 않게 한다.
        /// 마지막 FadeOutMs 구간은 이 여유 안에서 페이드아웃된다(여유가 페이드보다 길어야 소리 본체를 건드리지 않는다).
        /// </summary>
        public const double TailPaddingMs = 30.0;

        /// <summary>파일 맨 끝의 페이드아웃 길이(ms). 끝에서 파형이 뚝 끊길 때 나는 '딱' 클릭을 막는다.</summary>
        public const double FadeOutMs = 10.0;

        /// <summary>「체감 크기」를 잴 때 쓰는 창 길이(ms).</summary>
        public const double LoudnessWindowMs = 50.0;

        /// <summary>
        /// 이 피크(dBFS)보다 작은 파일은 "너무 작음(사실상 무음)"으로 보고 건너뛴다.
        /// 이런 파일을 -26dB 에 맞추려면 수십 dB 를 키워야 해서 잡음만 커진다.
        /// </summary>
        public const double MinInputPeakDbfs = -60.0;

        /// <summary>
        /// 계산된 게인이 이 범위(±dB) 안이면 게인을 정확히 1배(0dB)로 둔다.
        /// 이미 정규화된 파일을 다시 돌릴 때 16bit 반올림 오차가 쌓이지 않게 하는 장치(멱등).
        /// 대가: 체감 크기가 목표에서 최대 이 값만큼 벗어날 수 있다(귀로 구분 불가한 크기).
        /// </summary>
        public const double GainDeadBandDb = 0.1;

        /// <summary>이 도구가 받아들이는/저장하는 샘플레이트(Hz). 다르면 변환하지 않고 건너뛴다.</summary>
        public const int RequiredSampleRate = 44100;

        /// <summary>이 도구가 받아들이는/저장하는 비트 깊이. 다르면 변환하지 않고 건너뛴다.</summary>
        public const int RequiredBitsPerSample = 16;

        // 16bit 정수 ↔ 실수 변환 배율. -32768 → -1.0, 32767 → 약 +0.99997.
        private const double Pcm16Scale = 32768.0;
        // 무음 파일에서 log10(0) = -무한대가 되지 않게 쓰는 dB 하한 표시값.
        private const double SilenceDb = -200.0;

        // WAV 포맷 코드: 1 = 일반 PCM, 0xFFFE = 확장(WAVE_FORMAT_EXTENSIBLE, 내부 서브포맷이 PCM 일 수 있음).
        private const int WaveFormatPcm = 1;
        private const int WaveFormatExtensible = 0xFFFE;

        // ─────────────────────────────────────────────────────────────────
        //  결과 타입
        // ─────────────────────────────────────────────────────────────────

        /// <summary>정규화 한 건의 결과 상태.</summary>
        public enum Outcome
        {
            /// <summary>처리했고 새 바이트가 원본과 다르다(덮어쓰기 대상).</summary>
            Normalized,
            /// <summary>처리했지만 결과 바이트가 원본과 완전히 같다(이미 기준에 맞음 — 쓰지 않음).</summary>
            NoChange,
            /// <summary>범위 밖이거나 너무 작아서 건너뛰었다(SkipReason 참고).</summary>
            Skipped
        }

        /// <summary>WAV 파일 하나에서 읽은 PCM 데이터(16bit 전제).</summary>
        public sealed class PcmWav
        {
            /// <summary>채널 수(1 = 모노, 2 = 스테레오 ...).</summary>
            public int Channels;
            /// <summary>초당 샘플 수(Hz).</summary>
            public int SampleRate;
            /// <summary>샘플 하나의 비트 수.</summary>
            public int BitsPerSample;
            /// <summary>WAV 포맷 코드(1 = PCM).</summary>
            public int FormatCode;
            /// <summary>채널이 번갈아 저장된(인터리브) 16bit 샘플. 길이 = 프레임 수 × 채널 수.</summary>
            public short[] Interleaved;
            /// <summary>프레임 수(채널 하나 기준 샘플 수).</summary>
            public int FrameCount => Channels > 0 && Interleaved != null ? Interleaved.Length / Channels : 0;
        }

        /// <summary>한 신호(처리 전 또는 후)의 측정값. 로그로 남겨 사용자가 확인한다.</summary>
        public struct Stats
        {
            /// <summary>채널 수.</summary>
            public int Channels;
            /// <summary>파일 길이(초).</summary>
            public double DurationSec;
            /// <summary>모든 채널 원본 샘플 중 최대 절댓값(dBFS). 다운믹스 전 실제 파일의 피크.</summary>
            public double RawPeakDbfs;
            /// <summary>모노 다운믹스의 피크(dBFS).</summary>
            public double PeakDbfs;
            /// <summary>모노 다운믹스의 「체감 크기」(50ms 이동창 RMS 최댓값, dB).</summary>
            public double LoudnessDb;
            /// <summary>소리 구간 시작(초). 소리가 없으면 -1.</summary>
            public double SoundStartSec;
            /// <summary>소리 구간 끝(초). 소리가 없으면 -1.</summary>
            public double SoundEndSec;
        }

        /// <summary>정규화 한 건의 전체 결과.</summary>
        public sealed class Result
        {
            /// <summary>처리 결과 상태.</summary>
            public Outcome Outcome;
            /// <summary>건너뛴 이유(Skipped 일 때만). 사람이 읽는 한글 문장.</summary>
            public string SkipReason;
            /// <summary>처리 전 측정값(읽기에 성공했을 때만 유효).</summary>
            public Stats Before;
            /// <summary>처리 후 측정값(저장할 16bit 결과를 다시 잰 값).</summary>
            public Stats After;
            /// <summary>체감 크기를 목표에 맞추려고 처음 계산한 게인(dB).</summary>
            public double TargetGainDb;
            /// <summary>실제로 곱한 게인(dB). 피크 제한이나 데드밴드 때문에 TargetGainDb 와 다를 수 있다.</summary>
            public double AppliedGainDb;
            /// <summary>피크 상한 때문에 게인을 목표보다 낮췄으면 true(경고 대상).</summary>
            public bool PeakLimited;
            /// <summary>앞에서 잘라낸 길이(초).</summary>
            public double TrimmedLeadSec;
            /// <summary>뒤에서 잘라낸 길이(초).</summary>
            public double TrimmedTailSec;
            /// <summary>저장할 wav 파일 바이트(Normalized/NoChange 일 때).</summary>
            public byte[] OutputBytes;
        }

        // ─────────────────────────────────────────────────────────────────
        //  공개 진입점
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// wav 파일 바이트를 받아 기준에 맞게 정규화한 결과를 돌려준다. 파일을 직접 쓰지는 않는다.
        /// </summary>
        /// <param name="inputBytes">원본 wav 파일 전체 바이트.</param>
        public static Result Normalize(byte[] inputBytes)
        {
            var result = new Result();
            if (!TryReadPcm16(inputBytes, out PcmWav wav, out string readError))
                return Skip(result, readError);

            double[] mono = DownmixToMono(wav);
            result.Before = Measure(mono, wav.SampleRate, wav.Channels, RawPeakDbfs(wav.Interleaved));

            // 범위 검사 — 변환(리샘플링·비트 변환)은 이 도구의 범위 밖이다.
            if (wav.SampleRate != RequiredSampleRate)
                return Skip(result, $"샘플레이트가 {wav.SampleRate}Hz 라서 건너뜀(이 도구는 {RequiredSampleRate}Hz 만 처리하고 변환하지 않는다)");
            if (mono.Length == 0)
                return Skip(result, "샘플이 하나도 없는 빈 파일이라 건너뜀");
            if (result.Before.PeakDbfs < MinInputPeakDbfs)
                return Skip(result, $"피크 {Fmt(result.Before.PeakDbfs)}dBFS 가 하한 {Fmt(MinInputPeakDbfs)}dBFS 보다 작아(무음/너무 작음) 건너뜀");

            double[] trimmed = TrimSilence(mono, wav.SampleRate, out int leadCut, out int tailCut);
            result.TrimmedLeadSec = (double)leadCut / wav.SampleRate;
            result.TrimmedTailSec = (double)tailCut / wav.SampleRate;

            double gain = ComputeGain(trimmed, wav.SampleRate, result);
            short[] output = QuantizeWithCeiling(trimmed, gain);

            result.OutputBytes = WriteMonoPcm16(output, RequiredSampleRate);
            double[] outMono = ToDouble(output);
            result.After = Measure(outMono, RequiredSampleRate, 1, RawPeakDbfs(output));
            result.Outcome = BytesEqual(inputBytes, result.OutputBytes) ? Outcome.NoChange : Outcome.Normalized;
            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  WAV 읽기 / 쓰기
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// RIFF/WAVE 바이트에서 16bit PCM 데이터를 읽는다. fmt·data 외의 청크(LIST 등)는 건너뛴다.
        /// 16bit PCM 이 아니면 실패로 돌려준다(이 도구는 비트 변환을 하지 않는다).
        /// </summary>
        public static bool TryReadPcm16(byte[] bytes, out PcmWav wav, out string error)
        {
            wav = null;
            error = null;
            if (bytes == null || bytes.Length < 12 || !Tag(bytes, 0, "RIFF") || !Tag(bytes, 8, "WAVE"))
            {
                error = "RIFF/WAVE 형식의 wav 파일이 아니라 건너뜀";
                return false;
            }

            var parsed = new PcmWav();
            bool haveFmt = false;
            int dataOffset = -1, dataSize = 0;
            int pos = 12;
            // 청크는 [4바이트 이름][4바이트 크기][내용] 이 반복된다. 크기가 홀수면 1바이트 패딩이 붙는다.
            while (pos + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                int body = pos + 8;
                if (size < 0 || body + size > bytes.Length) size = Math.Max(0, bytes.Length - body); // 잘린 파일 방어
                if (id == "fmt " && size >= 16)
                {
                    parsed.FormatCode = BitConverter.ToUInt16(bytes, body);
                    parsed.Channels = BitConverter.ToUInt16(bytes, body + 2);
                    parsed.SampleRate = BitConverter.ToInt32(bytes, body + 4);
                    parsed.BitsPerSample = BitConverter.ToUInt16(bytes, body + 14);
                    // 확장 포맷이면 서브포맷 GUID 의 앞 2바이트가 실제 포맷 코드다(cbSize 2 + validBits 2 + mask 4 뒤).
                    if (parsed.FormatCode == WaveFormatExtensible && size >= 26)
                        parsed.FormatCode = BitConverter.ToUInt16(bytes, body + 24);
                    haveFmt = true;
                }
                else if (id == "data")
                {
                    dataOffset = body;
                    dataSize = size;
                }
                pos = body + size + (size & 1);
            }

            if (!haveFmt || dataOffset < 0)
            {
                error = "fmt/data 청크를 찾지 못해 건너뜀(손상된 wav 일 수 있음)";
                return false;
            }
            if (parsed.FormatCode != WaveFormatPcm || parsed.BitsPerSample != RequiredBitsPerSample)
            {
                error = $"16bit PCM 이 아니라 건너뜀(포맷 코드 {parsed.FormatCode}, {parsed.BitsPerSample}bit — 이 도구는 변환하지 않는다)";
                return false;
            }
            if (parsed.Channels < 1)
            {
                error = "채널 수가 0 이라 건너뜀(손상된 wav 일 수 있음)";
                return false;
            }

            // 마지막 프레임이 덜 들어 있으면(잘린 파일) 완전한 프레임까지만 읽는다.
            int frameBytes = parsed.Channels * 2;
            int usable = dataSize / frameBytes * frameBytes;
            parsed.Interleaved = new short[usable / 2];
            Buffer.BlockCopy(bytes, dataOffset, parsed.Interleaved, 0, usable);
            if (!BitConverter.IsLittleEndian) // WAV 는 리틀엔디언. 거의 모든 PC 가 리틀엔디언이지만 방어한다.
                for (int i = 0; i < parsed.Interleaved.Length; i++)
                    parsed.Interleaved[i] = (short)((parsed.Interleaved[i] << 8) | ((parsed.Interleaved[i] >> 8) & 0xFF));
            wav = parsed;
            return true;
        }

        /// <summary>16bit 모노 샘플을 표준 44바이트 헤더의 PCM wav 파일 바이트로 만든다.</summary>
        public static byte[] WriteMonoPcm16(short[] samples, int sampleRate)
        {
            const int headerSize = 44;
            int dataSize = samples.Length * 2;
            var bytes = new byte[headerSize + dataSize];
            WriteTag(bytes, 0, "RIFF");
            WriteInt(bytes, 4, headerSize - 8 + dataSize);
            WriteTag(bytes, 8, "WAVE");
            WriteTag(bytes, 12, "fmt ");
            WriteInt(bytes, 16, 16);                    // fmt 청크 크기
            WriteShort(bytes, 20, WaveFormatPcm);       // PCM
            WriteShort(bytes, 22, 1);                   // 모노
            WriteInt(bytes, 24, sampleRate);
            WriteInt(bytes, 28, sampleRate * 2);        // 초당 바이트 = 샘플레이트 × 채널 1 × 2바이트
            WriteShort(bytes, 32, 2);                   // 프레임당 바이트
            WriteShort(bytes, 34, RequiredBitsPerSample);
            WriteTag(bytes, 36, "data");
            WriteInt(bytes, 40, dataSize);
            for (int i = 0; i < samples.Length; i++)
                WriteShort(bytes, headerSize + i * 2, samples[i]);
            return bytes;
        }

        // ─────────────────────────────────────────────────────────────────
        //  측정
        // ─────────────────────────────────────────────────────────────────

        /// <summary>채널 평균으로 모노 다운믹스한 -1.0~+1.0 실수 배열을 만든다.</summary>
        public static double[] DownmixToMono(PcmWav wav)
        {
            int frames = wav.FrameCount, ch = wav.Channels;
            var mono = new double[frames];
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int c = 0; c < ch; c++) sum += wav.Interleaved[f * ch + c];
                mono[f] = sum / ch / Pcm16Scale;
            }
            return mono;
        }

        /// <summary>모노 신호의 피크·체감 크기·소리 구간을 잰다.</summary>
        public static Stats Measure(double[] mono, int sampleRate, int channels, double rawPeakDbfs)
        {
            var s = new Stats
            {
                Channels = channels,
                DurationSec = sampleRate > 0 ? (double)mono.Length / sampleRate : 0,
                RawPeakDbfs = rawPeakDbfs,
                PeakDbfs = ToDb(Peak(mono)),
                LoudnessDb = ToDb(MaxWindowRms(mono, sampleRate)),
                SoundStartSec = -1,
                SoundEndSec = -1
            };
            if (FindSoundRange(mono, out int first, out int last))
            {
                s.SoundStartSec = (double)first / sampleRate;
                s.SoundEndSec = (double)(last + 1) / sampleRate;
            }
            return s;
        }

        /// <summary>
        /// 「체감 크기」 계산: LoudnessWindowMs 길이의 창을 한 샘플씩 밀며 RMS 를 재고 그중 최댓값(선형)을 돌려준다.
        /// 신호가 창보다 짧으면 신호 전체의 RMS 를 쓴다.
        /// </summary>
        public static double MaxWindowRms(double[] mono, int sampleRate)
        {
            if (mono.Length == 0) return 0;
            int window = Math.Max(1, (int)Math.Round(sampleRate * LoudnessWindowMs / 1000.0));
            if (window > mono.Length) window = mono.Length;

            // 이동 합(running sum): 창이 한 칸 갈 때 새 샘플 제곱을 더하고 빠지는 샘플 제곱을 뺀다(O(n)).
            double sum = 0, best = 0;
            for (int i = 0; i < mono.Length; i++)
            {
                sum += mono[i] * mono[i];
                if (i >= window) sum -= mono[i - window] * mono[i - window];
                if (i >= window - 1 && sum > best) best = sum;
            }
            return Math.Sqrt(Math.Max(0, best) / window);
        }

        /// <summary>피크 대비 SoundThresholdDb 보다 큰 첫/마지막 샘플 위치를 찾는다. 소리가 없으면 false.</summary>
        public static bool FindSoundRange(double[] mono, out int first, out int last)
        {
            first = -1;
            last = -1;
            double peak = Peak(mono);
            if (peak <= 0) return false;
            double threshold = peak * FromDb(SoundThresholdDb);
            for (int i = 0; i < mono.Length; i++)
                if (Math.Abs(mono[i]) > threshold) { first = i; break; }
            for (int i = mono.Length - 1; i >= 0; i--)
                if (Math.Abs(mono[i]) > threshold) { last = i; break; }
            return first >= 0;
        }

        // ─────────────────────────────────────────────────────────────────
        //  처리 단계
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 앞·뒤 무음을 잘라내고 끝에 페이드아웃을 건 새 배열을 돌려준다.
        /// 남기는 범위 = [소리 시작 − 앞 여유, 소리 끝 + 뒤 여유] (파일 범위를 넘지 않게 자른다).
        /// </summary>
        private static double[] TrimSilence(double[] mono, int sampleRate, out int leadCut, out int tailCut)
        {
            FindSoundRange(mono, out int first, out int last); // 호출 전에 피크 하한을 통과했으므로 항상 찾는다.
            int lead = MsToSamples(LeadPaddingMs, sampleRate);
            int tail = MsToSamples(TailPaddingMs, sampleRate);
            int start = Math.Max(0, first - lead);
            int endExclusive = Math.Min(mono.Length, last + 1 + tail);
            leadCut = start;
            tailCut = mono.Length - endExclusive;

            var trimmed = new double[endExclusive - start];
            Array.Copy(mono, start, trimmed, 0, trimmed.Length);
            // 페이드아웃을 다시 걸지 않는 경우: 뒤에서 잘라낸 것이 없고 마지막 샘플이 이미 0 이다.
            //   = 이 도구로 이미 처리한 파일(지난번 페이드가 끝을 정확히 0 으로 만들었다).
            //   여기에 또 페이드를 곱하면 페이드 구간이 실행할 때마다 조금씩 더 작아져 멱등이 깨진다.
            //   끝이 이미 0 이면 '딱' 클릭도 생기지 않으므로 다시 걸 이유가 없다.
            bool alreadyEndsSilent = tailCut == 0 && trimmed.Length > 0 && trimmed[trimmed.Length - 1] == 0;
            if (!alreadyEndsSilent)
                ApplyFadeOut(trimmed, MsToSamples(FadeOutMs, sampleRate));
            return trimmed;
        }

        /// <summary>
        /// 마지막 fadeSamples 개 샘플에 1 → 0 으로 줄어드는 직선 배율을 곱한다. 맨 마지막 샘플은 정확히 0 이 된다.
        /// </summary>
        private static void ApplyFadeOut(double[] samples, int fadeSamples)
        {
            int n = Math.Min(fadeSamples, samples.Length);
            if (n <= 0) return;
            int begin = samples.Length - n;
            for (int i = 0; i < n; i++)
            {
                // i = 0 → (n-1)/n 배, i = n-1 → 0 배. 이미 0 인 구간에 다시 곱해도 0 이므로 반복 실행에 안전하다.
                double factor = (double)(n - 1 - i) / n;
                samples[begin + i] *= factor;
            }
        }

        /// <summary>
        /// 체감 크기 목표 게인을 계산하고, 피크 상한·데드밴드를 적용한 실제 배율을 돌려준다(결과 필드도 채운다).
        /// </summary>
        private static double ComputeGain(double[] trimmed, int sampleRate, Result result)
        {
            double loudnessDb = ToDb(MaxWindowRms(trimmed, sampleRate));
            double peakDbfs = ToDb(Peak(trimmed));
            double targetGainDb = TargetLoudnessDb - loudnessDb;
            double maxGainDb = PeakCeilingDbfs - peakDbfs; // 이 이상 키우면 피크가 상한을 넘는다.

            double appliedDb = targetGainDb;
            if (appliedDb > maxGainDb)
            {
                appliedDb = maxGainDb;
                result.PeakLimited = true;
            }
            // 데드밴드: 변화가 아주 작으면 정확히 1배로 둔다(멱등 — 16bit 반올림 오차가 쌓이지 않게).
            //   피크 제한이 걸린 경우에는 "1배로 둬도 피크가 상한을 넘지 않을 때"(appliedDb >= 0)만 적용한다.
            //   음수(입력 피크가 이미 상한보다 큼)를 0 으로 올리면 상한을 어기기 때문이다.
            if (Math.Abs(appliedDb) < GainDeadBandDb && (!result.PeakLimited || appliedDb >= 0))
                appliedDb = 0;

            result.TargetGainDb = targetGainDb;
            result.AppliedGainDb = appliedDb;
            return FromDb(appliedDb);
        }

        /// <summary>
        /// 게인을 곱해 16bit 정수로 반올림한다. 반올림 때문에 피크 상한을 0.5 LSB 넘는 샘플이 생기면
        /// 상한 정수값으로 맞춘다(반올림 오차 보정일 뿐, 소리 모양을 바꾸는 리미터가 아니다).
        /// </summary>
        private static short[] QuantizeWithCeiling(double[] samples, double gain)
        {
            int ceiling = (int)Math.Floor(FromDb(PeakCeilingDbfs) * Pcm16Scale);
            var output = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                double v = Math.Round(samples[i] * gain * Pcm16Scale, MidpointRounding.AwayFromZero);
                if (v > ceiling) v = ceiling;
                else if (v < -ceiling) v = -ceiling;
                output[i] = (short)v;
            }
            return output;
        }

        // ─────────────────────────────────────────────────────────────────
        //  작은 도우미
        // ─────────────────────────────────────────────────────────────────

        /// <summary>선형 크기를 dB 로 바꾼다(0 이하는 SilenceDb).</summary>
        public static double ToDb(double linear) => linear > 0 ? 20.0 * Math.Log10(linear) : SilenceDb;

        /// <summary>dB 를 선형 배율로 바꾼다.</summary>
        public static double FromDb(double db) => Math.Pow(10.0, db / 20.0);

        /// <summary>dB 값을 소수 첫째 자리 문자열로(문화권 무관 — 로그에 쉼표 소수점이 섞이지 않게).</summary>
        public static string Fmt(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

        private static Result Skip(Result result, string reason)
        {
            result.Outcome = Outcome.Skipped;
            result.SkipReason = reason;
            return result;
        }

        private static double Peak(double[] samples)
        {
            double peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double a = Math.Abs(samples[i]);
                if (a > peak) peak = a;
            }
            return peak;
        }

        private static double RawPeakDbfs(short[] samples)
        {
            int peak = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                int a = Math.Abs((int)samples[i]); // (int) 로 바꿔야 -32768 의 절댓값이 넘치지 않는다.
                if (a > peak) peak = a;
            }
            return ToDb(peak / Pcm16Scale);
        }

        private static double[] ToDouble(short[] samples)
        {
            var d = new double[samples.Length];
            for (int i = 0; i < samples.Length; i++) d[i] = samples[i] / Pcm16Scale;
            return d;
        }

        private static int MsToSamples(double ms, int sampleRate) => (int)Math.Round(ms * sampleRate / 1000.0);

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool Tag(byte[] bytes, int offset, string tag)
        {
            for (int i = 0; i < 4; i++)
                if (bytes[offset + i] != (byte)tag[i]) return false;
            return true;
        }

        private static void WriteTag(byte[] bytes, int offset, string tag)
        {
            for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)tag[i];
        }

        private static void WriteInt(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteShort(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }
    }
}
