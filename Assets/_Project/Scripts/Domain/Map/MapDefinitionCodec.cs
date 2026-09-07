// ============================================================================
// MapDefinitionCodec.cs
// MapDefinition을 "정규(canonical) 바이너리"로 바꾸고 다시 되돌리는 변환기,
// 그리고 그 바이트열의 SHA-256 해시를 계산하는 도구.
//
// 왜 "정규(canonical)" 형식이 필요한가:
//   멀티플레이에서 Host와 Client가 같은 맵을 받았는지 확인하려면 맵 데이터를
//   바이트열로 바꾼 뒤 해시를 비교한다. 그런데 같은 맵인데도 바이트열이 달라질
//   여지가 있으면(필드 순서가 다르거나, 목록 순서가 다르거나, 기기마다 정수 저장
//   방식이 다르거나) 해시가 달라져 "다른 맵"으로 오판한다.
//   그래서 아래 규칙을 못박아 "같은 맵 = 항상 똑같은 바이트열"을 보장한다.
//
//   1. 필드 순서 고정 (아래 Encode의 순서가 그 단일 소스)
//   2. 모든 수치는 고정폭 정수로 기록 (float 금지 — 기기마다 미세하게 달라짐)
//   3. 다중 바이트 정수는 항상 little-endian (BitConverter는 플랫폼 순서를 따르므로 쓰지 않는다)
//   4. 타일 231개는 row-major 순서로 기록
//   5. 성·광산·장식 목록은 정규 정렬 후 기록
//   6. string, float, 해시 필드 자신은 바이트열에서 제외
//   7. 위 바이트열 전체에 SHA-256 → 32바이트 다이제스트
//
// ⚠️ 이 파일은 1단계에서 "만들어만 두는" 도구다. 프로젝트 어디에서도
//    Encode/Decode/ComputeHash를 호출하지 않는다(네트워크 전송은 3단계 작업).
//
// 근거: TechnicalDesignDocument.md 「MapDefinition 정규 데이터 계약」
//       canonical binary와 SHA-256 항목
//
// Domain 레이어 — 순수 C#. System / System.Collections.Generic /
// System.Security.Cryptography(BCL)만 사용하며 Hexiege.Core·UnityEngine을 참조하지 않는다.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Hexiege.Domain
{
    /// <summary>
    /// MapDefinition ↔ canonical binary 변환기 및 SHA-256 해시 계산기.
    /// 인스턴스를 만들 필요가 없는 정적 도구 클래스다.
    /// </summary>
    public static class MapDefinitionCodec
    {
        // ====================================================================
        // 인코드 (MapDefinition → 바이트열)
        // ====================================================================

        /// <summary>
        /// 맵 정의를 canonical 바이트열로 변환한다.
        /// 해시 필드(MapDefinition.Hash) 자신은 결과에 포함하지 않는다.
        /// </summary>
        /// <param name="def">변환할 맵 정의</param>
        /// <returns>canonical 바이트열</returns>
        public static byte[] Encode(MapDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var buffer = new List<byte>(EstimateSize(def));

            // ---- 상위 필드 (순서 고정) ----
            WriteInt32(buffer, def.MapVersion);
            WriteUInt64(buffer, def.RootSeed);
            WriteInt32(buffer, (int)def.MapType);
            WriteInt32(buffer, def.Width);
            WriteInt32(buffer, def.Height);
            WriteInt32(buffer, (int)def.Orientation);
            WriteInt32(buffer, def.NeutralMineCount);
            WriteInt32(buffer, def.TestModeFlag);
            WriteInt32(buffer, def.InitialGold);

            // ---- 타일 배열 (row-major, 한 칸당 1바이트) ----
            // 길이를 따로 적지 않는 이유: Width * Height로 이미 확정되기 때문이다.
            for (int i = 0; i < def.Tiles.Length; i++)
            {
                buffer.Add((byte)def.Tiles[i]);
            }

            // ---- 오브젝트 배치 목록 (정규 정렬한 복사본을 기록) ----
            // 원본 List를 정렬하면 호출자의 데이터를 몰래 바꾸는 부작용이 생기므로
            // 반드시 복사본을 만들어 정렬한다.
            var castles = new List<MapObjectPlacement>(def.Castles);
            castles.Sort(MapObjectPlacement.CompareCanonical);
            WriteInt32(buffer, castles.Count);
            for (int i = 0; i < castles.Count; i++)
            {
                WriteInt32(buffer, castles[i].TileIndex);
                WriteInt32(buffer, (int)castles[i].Team);
            }

            var startingMines = new List<MapObjectPlacement>(def.StartingMines);
            startingMines.Sort(MapObjectPlacement.CompareCanonical);
            WriteInt32(buffer, startingMines.Count);
            for (int i = 0; i < startingMines.Count; i++)
            {
                WriteInt32(buffer, startingMines[i].TileIndex);
                WriteInt32(buffer, (int)startingMines[i].Team);
            }

            var neutralMines = new List<int>(def.NeutralMines);
            neutralMines.Sort();
            WriteInt32(buffer, neutralMines.Count);
            for (int i = 0; i < neutralMines.Count; i++)
            {
                WriteInt32(buffer, neutralMines[i]);
            }

            var decorations = new List<DecorationDefinition>(def.Decorations);
            decorations.Sort(DecorationDefinition.CompareCanonical);
            WriteInt32(buffer, decorations.Count);
            for (int i = 0; i < decorations.Count; i++)
            {
                WriteInt32(buffer, decorations[i].TileIndex);
                WriteInt32(buffer, decorations[i].TypeId);
                WriteInt32(buffer, decorations[i].MaterialVariantId);
                WriteInt32(buffer, decorations[i].ScaleStepId);
                WriteInt32(buffer, decorations[i].RotationStepId);
            }

            return buffer.ToArray();
        }

        // ====================================================================
        // 디코드 (바이트열 → MapDefinition)
        // ====================================================================

        /// <summary>
        /// canonical 바이트열을 맵 정의로 되돌린다.
        /// 형식이 어긋나면 예외 대신 null을 반환한다 — 네트워크로 받은 데이터는
        /// 손상됐을 수 있으므로 호출부가 "실패"로 조용히 처리할 수 있어야 하기 때문이다.
        /// </summary>
        /// <param name="bytes">canonical 바이트열</param>
        /// <returns>복원된 맵 정의. 형식이 어긋나면 null.</returns>
        public static MapDefinition Decode(byte[] bytes)
        {
            if (bytes == null) return null;

            int offset = 0;
            try
            {
                int mapVersion = ReadInt32(bytes, ref offset);
                // 지원하지 않는 형식 버전은 해석을 시도하지 않는다.
                if (mapVersion != MapDefinition.CurrentMapVersion) return null;

                ulong rootSeed = ReadUInt64(bytes, ref offset);
                int mapType = ReadInt32(bytes, ref offset);
                int width = ReadInt32(bytes, ref offset);
                int height = ReadInt32(bytes, ref offset);
                int orientation = ReadInt32(bytes, ref offset);
                int neutralMineCount = ReadInt32(bytes, ref offset);
                int testModeFlag = ReadInt32(bytes, ref offset);
                int initialGold = ReadInt32(bytes, ref offset);

                if (width <= 0 || height <= 0) return null;
                // 타일 배열이 지나치게 커서 메모리를 통째로 먹는 입력을 막는다.
                long tileCount = (long)width * height;
                if (tileCount > bytes.Length) return null;

                var def = new MapDefinition(width, height)
                {
                    MapVersion = mapVersion,
                    RootSeed = rootSeed,
                    MapType = (MapType)mapType,
                    Orientation = (HexOrientation)orientation,
                    NeutralMineCount = neutralMineCount,
                    TestModeFlag = testModeFlag,
                    InitialGold = initialGold
                };

                for (int i = 0; i < def.Tiles.Length; i++)
                {
                    def.Tiles[i] = (TileKind)ReadByte(bytes, ref offset);
                }

                int castleCount = ReadInt32(bytes, ref offset);
                if (castleCount < 0) return null;
                for (int i = 0; i < castleCount; i++)
                {
                    int tileIndex = ReadInt32(bytes, ref offset);
                    int team = ReadInt32(bytes, ref offset);
                    def.Castles.Add(new MapObjectPlacement(tileIndex, (TeamId)team));
                }

                int startingMineCount = ReadInt32(bytes, ref offset);
                if (startingMineCount < 0) return null;
                for (int i = 0; i < startingMineCount; i++)
                {
                    int tileIndex = ReadInt32(bytes, ref offset);
                    int team = ReadInt32(bytes, ref offset);
                    def.StartingMines.Add(new MapObjectPlacement(tileIndex, (TeamId)team));
                }

                int neutralCount = ReadInt32(bytes, ref offset);
                if (neutralCount < 0) return null;
                for (int i = 0; i < neutralCount; i++)
                {
                    def.NeutralMines.Add(ReadInt32(bytes, ref offset));
                }

                int decorationCount = ReadInt32(bytes, ref offset);
                if (decorationCount < 0) return null;
                for (int i = 0; i < decorationCount; i++)
                {
                    int tileIndex = ReadInt32(bytes, ref offset);
                    int typeId = ReadInt32(bytes, ref offset);
                    int materialVariantId = ReadInt32(bytes, ref offset);
                    int scaleStepId = ReadInt32(bytes, ref offset);
                    int rotationStepId = ReadInt32(bytes, ref offset);
                    def.Decorations.Add(new DecorationDefinition(
                        tileIndex, typeId, materialVariantId, scaleStepId, rotationStepId));
                }

                // 남는 바이트가 있으면 형식이 어긋난 것이다.
                if (offset != bytes.Length) return null;

                return def;
            }
            catch (ArgumentOutOfRangeException)
            {
                // 바이트열이 중간에서 끊긴 경우 — 손상된 입력으로 보고 실패 처리.
                return null;
            }
        }

        // ====================================================================
        // 해시
        // ====================================================================

        /// <summary>
        /// 맵 정의의 canonical 바이트열에 SHA-256을 적용해 32바이트 다이제스트를 만든다.
        /// 해시 필드 자신은 입력에서 제외된다(Encode가 애초에 넣지 않는다).
        /// </summary>
        /// <param name="def">해시를 계산할 맵 정의</param>
        /// <returns>32바이트 SHA-256 다이제스트</returns>
        public static byte[] ComputeHash(MapDefinition def)
        {
            return ComputeHash(Encode(def));
        }

        /// <summary> 이미 만들어 둔 canonical 바이트열의 SHA-256 다이제스트를 계산한다. </summary>
        /// <param name="canonicalBytes">canonical 바이트열</param>
        /// <returns>32바이트 SHA-256 다이제스트</returns>
        public static byte[] ComputeHash(byte[] canonicalBytes)
        {
            if (canonicalBytes == null) throw new ArgumentNullException(nameof(canonicalBytes));

            // SHA256은 IDisposable이므로 using으로 확실히 해제한다.
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(canonicalBytes);
            }
        }

        /// <summary>
        /// 두 해시가 같은지 비교한다. null이거나 길이가 다르면 false.
        /// Host/Client의 맵 일치 판정에 쓰기 위한 단순 바이트 비교다.
        /// </summary>
        /// <param name="a">해시 1</param>
        /// <param name="b">해시 2</param>
        /// <returns>모든 바이트가 같으면 true</returns>
        public static bool HashEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        // ====================================================================
        // 저수준 읽기/쓰기 — 항상 little-endian
        // ====================================================================

        /// <summary>
        /// 32비트 정수를 little-endian 4바이트로 기록한다.
        /// BitConverter를 쓰지 않는 이유: BitConverter는 실행 플랫폼의 바이트 순서를
        /// 따르므로, 빅엔디언 기기에서 다른 바이트열이 나와 해시가 어긋날 수 있다.
        /// </summary>
        /// <param name="buffer">기록할 버퍼</param>
        /// <param name="value">기록할 값</param>
        private static void WriteInt32(List<byte> buffer, int value)
        {
            uint u = unchecked((uint)value);
            buffer.Add((byte)(u & 0xFF));
            buffer.Add((byte)((u >> 8) & 0xFF));
            buffer.Add((byte)((u >> 16) & 0xFF));
            buffer.Add((byte)((u >> 24) & 0xFF));
        }

        /// <summary> 64비트 부호 없는 정수를 little-endian 8바이트로 기록한다. </summary>
        /// <param name="buffer">기록할 버퍼</param>
        /// <param name="value">기록할 값</param>
        private static void WriteUInt64(List<byte> buffer, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                buffer.Add((byte)((value >> (i * 8)) & 0xFF));
            }
        }

        /// <summary> little-endian 4바이트를 32비트 정수로 읽는다. </summary>
        /// <param name="bytes">원본 바이트열</param>
        /// <param name="offset">읽기 위치(읽은 만큼 자동으로 전진)</param>
        /// <returns>읽어들인 값</returns>
        private static int ReadInt32(byte[] bytes, ref int offset)
        {
            EnsureAvailable(bytes, offset, 4);
            uint u = (uint)bytes[offset]
                   | ((uint)bytes[offset + 1] << 8)
                   | ((uint)bytes[offset + 2] << 16)
                   | ((uint)bytes[offset + 3] << 24);
            offset += 4;
            return unchecked((int)u);
        }

        /// <summary> little-endian 8바이트를 64비트 부호 없는 정수로 읽는다. </summary>
        /// <param name="bytes">원본 바이트열</param>
        /// <param name="offset">읽기 위치(읽은 만큼 자동으로 전진)</param>
        /// <returns>읽어들인 값</returns>
        private static ulong ReadUInt64(byte[] bytes, ref int offset)
        {
            EnsureAvailable(bytes, offset, 8);
            ulong v = 0;
            for (int i = 0; i < 8; i++)
            {
                v |= (ulong)bytes[offset + i] << (i * 8);
            }
            offset += 8;
            return v;
        }

        /// <summary> 1바이트를 읽는다. </summary>
        /// <param name="bytes">원본 바이트열</param>
        /// <param name="offset">읽기 위치(읽은 만큼 자동으로 전진)</param>
        /// <returns>읽어들인 바이트</returns>
        private static byte ReadByte(byte[] bytes, ref int offset)
        {
            EnsureAvailable(bytes, offset, 1);
            return bytes[offset++];
        }

        /// <summary> 남은 바이트가 요청한 길이만큼 있는지 확인한다. 부족하면 예외를 던진다. </summary>
        /// <param name="bytes">원본 바이트열</param>
        /// <param name="offset">현재 읽기 위치</param>
        /// <param name="need">필요한 바이트 수</param>
        private static void EnsureAvailable(byte[] bytes, int offset, int need)
        {
            if (offset < 0 || offset + need > bytes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
        }

        /// <summary>
        /// 버퍼 초기 용량 추정치. 정확할 필요는 없고 List 재할당을 줄이기 위한 값이다.
        /// </summary>
        /// <param name="def">대상 맵 정의</param>
        /// <returns>추정 바이트 수</returns>
        private static int EstimateSize(MapDefinition def)
        {
            return 40
                 + def.Tiles.Length
                 + def.Castles.Count * 8
                 + def.StartingMines.Count * 8
                 + def.NeutralMines.Count * 4
                 + def.Decorations.Count * 20
                 + 16;
        }
    }
}
