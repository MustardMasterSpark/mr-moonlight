// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace MA.Flora
{
    internal static unsafe class BitUtility
    {
        #region Utility

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignDown(int value, int alignPow2)
        {
            return value & ~(alignPow2 - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp(int value, int alignPow2)
        {
            return AlignDown(value + alignPow2 - 1, alignPow2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignUp64(int index)
        {
            return (index + 63) & ~63;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWordAligned(int dstOffset, int srcOffset, int bitCount)
        {
            // SIMD is only used when the offsets are aligned and bit count is a multiple of 64
            return (dstOffset & 63) == 0 && (srcOffset & 63) == 0 && (bitCount & 63) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong LoadAlignedBits(ulong* src, int srcWord, int shiftDelta)
        {
            if (shiftDelta == 0)
                return src[srcWord];

            if (shiftDelta > 0)                 // src starts *to the right* of dst
            {
                int shL =  shiftDelta;          // 1‥63
                int shR = 64 - shL;
                return (src[srcWord] >> shL) | (src[srcWord + 1] << shR);
            }
            else                                // src starts *to the left* of dst
            {
                int shL = -shiftDelta;          // 1‥63
                int shR = 64 - shL;
                return (src[srcWord] << shL) | (src[srcWord - 1] >> shR);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int FromBool(bool value)
        {
            return value ? 1 : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSet(ulong* ptr, int pos)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;
            return 0ul != (ptr[idx] & mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(ulong* ptr, int pos, bool value)
        {
            var idx = pos >> 6;
            var shift = pos & 0x3f;
            var mask = 1ul << shift;
            var bits = (ptr[idx] & ~mask) | ((ulong)-FromBool(value) & mask);
            ptr[idx] = bits;
        }

        #endregion

        #region General

        public static void SetBits(ulong* ptr, int pos, bool value, int numBits)
        {
            var end = pos + numBits;
            var idxB = pos >> 6;
            var shiftB = pos & 0x3f;
            var idxE = (end - 1) >> 6;
            var shiftE = end & 0x3f;
            var maskB = 0xfffffffffffffffful << shiftB;
            var maskE = 0xfffffffffffffffful >> (64 - shiftE);
            var orBits = (ulong)-FromBool(value);
            var orBitsB = maskB & orBits;
            var orBitsE = maskE & orBits;
            var cmaskB = ~maskB;
            var cmaskE = ~maskE;

            if (idxB == idxE)
            {
                var maskBE = maskB & maskE;
                var cmaskBE = ~maskBE;
                var orBitsBE = orBitsB & orBitsE;
                ptr[idxB] = (ptr[idxB] & cmaskBE) | orBitsBE;
                return;
            }

            ptr[idxB] = (ptr[idxB] & cmaskB) | orBitsB;

            for (var idx = idxB + 1; idx < idxE; ++idx)
            {
                ptr[idx] = orBits;
            }

            ptr[idxE] = (ptr[idxE] & cmaskE) | orBitsE;
        }

        public static int CountBits(ulong* ptr, int pos, int numBits = 1)
        {
            var end = pos + numBits;
            var idxB = pos >> 6;
            var shiftB = pos & 0x3f;
            var idxE = (end - 1) >> 6;
            var shiftE = end & 0x3f;
            var maskB = 0xfffffffffffffffful << shiftB;
            var maskE = 0xfffffffffffffffful >> (64 - shiftE);

            if (idxB == idxE)
            {
                var mask = maskB & maskE;
                return math.countbits(ptr[idxB] & mask);
            }

            var count = math.countbits(ptr[idxB] & maskB);

            for (var idx = idxB + 1; idx < idxE; ++idx)
            {
                count += math.countbits(ptr[idx]);
            }

            count += math.countbits(ptr[idxE] & maskE);

            return count;
        }

        internal static bool TestAny(ulong* ptr, int pos, int numBits = 1)
        {
            var end = pos + numBits;
            var idxB = pos >> 6;
            var shiftB = pos & 0x3f;
            var idxE = (end - 1) >> 6;
            var shiftE = end & 0x3f;
            var maskB = 0xfffffffffffffffful << shiftB;
            var maskE = 0xfffffffffffffffful >> (64 - shiftE);

            if (idxB == idxE)
            {
                var mask = maskB & maskE;
                return 0ul != (ptr[idxB] & mask);
            }

            if (0ul != (ptr[idxB] & maskB))
            {
                return true;
            }

            for (var idx = idxB + 1; idx < idxE; ++idx)
            {
                if (0ul != ptr[idx])
                {
                    return true;
                }
            }

            return 0ul != (ptr[idxE] & maskE);
        }

        #endregion

        #region AND (dst &= src)

        public static void AndWords(ulong* dst, ulong* src, int wordCount)
        {
            if (wordCount <= 0)
                return;

            int i = 0;

            if (X86.Avx2.IsAvx2Supported) // 64-byte / 256-bit AVX2 (Windows, Linux, consoles)
            {
                for (; i <= wordCount - 16; i += 16)
                {
                    var a0 = X86.Avx.mm256_loadu_si256(dst + i +  0);
                    var b0 = X86.Avx.mm256_loadu_si256(src + i +  0);
                    X86.Avx.mm256_storeu_si256(dst + i +  0, X86.Avx2.mm256_and_si256(a0, b0));

                    var a1 = X86.Avx.mm256_loadu_si256(dst + i +  4);
                    var b1 = X86.Avx.mm256_loadu_si256(src + i +  4);
                    X86.Avx.mm256_storeu_si256(dst + i +  4, X86.Avx2.mm256_and_si256(a1, b1));

                    var a2 = X86.Avx.mm256_loadu_si256(dst + i +  8);
                    var b2 = X86.Avx.mm256_loadu_si256(src + i +  8);
                    X86.Avx.mm256_storeu_si256(dst + i +  8, X86.Avx2.mm256_and_si256(a2, b2));

                    var a3 = X86.Avx.mm256_loadu_si256(dst + i + 12);
                    var b3 = X86.Avx.mm256_loadu_si256(src + i + 12);
                    X86.Avx.mm256_storeu_si256(dst + i + 12, X86.Avx2.mm256_and_si256(a3, b3));
                }
            }
            else if (Arm.Neon.IsNeonSupported) // 16-byte / 128-bit NEON (Apple, Android, Quest)
            {
                for (; i <= wordCount - 8; i += 8)
                {
                    var a0 = Arm.Neon.vld1q_u64(dst + i + 0);
                    var b0 = Arm.Neon.vld1q_u64(src + i + 0);
                    Arm.Neon.vst1q_u64(dst + i + 0, Arm.Neon.vandq_u64(a0, b0));

                    var a1 = Arm.Neon.vld1q_u64(dst + i + 2);
                    var b1 = Arm.Neon.vld1q_u64(src + i + 2);
                    Arm.Neon.vst1q_u64(dst + i + 2, Arm.Neon.vandq_u64(a1, b1));

                    var a2 = Arm.Neon.vld1q_u64(dst + i + 4);
                    var b2 = Arm.Neon.vld1q_u64(src + i + 4);
                    Arm.Neon.vst1q_u64(dst + i + 4, Arm.Neon.vandq_u64(a2, b2));

                    var a3 = Arm.Neon.vld1q_u64(dst + i + 6);
                    var b3 = Arm.Neon.vld1q_u64(src + i + 6);
                    Arm.Neon.vst1q_u64(dst + i + 6, Arm.Neon.vandq_u64(a3, b3));
                }
            }
            else if (X86.Sse2.IsSse2Supported) // legacy SSE2 fallback (old x64 / Rosetta)
            {
                for (; i <= wordCount - 2; i += 2)
                {
                    var a = X86.Sse2.loadu_si128(dst + i);
                    var b = X86.Sse2.loadu_si128(src + i);
                    X86.Sse2.storeu_si128(dst + i, X86.Sse2.and_si128(a, b));
                }
            }

            // Fallback or SIMD tail
            for (; i < wordCount; ++i)
                dst[i] &= src[i];
        }

        public static void AndBits(ulong* dst, int dstOffset, ulong* src, int srcOffset, int bitCount)
        {
            if (bitCount <= 0)
                return;

            if (IsWordAligned(dstOffset, srcOffset, bitCount))
            {
                int wordCount = bitCount >> 6;
                AndWords(dst + (dstOffset >> 6), src + (srcOffset >> 6), wordCount);
                return;
            }

            int srcChunkIndexB = srcOffset >> 6;
            int dstChunkIndexB = dstOffset >> 6;

            int dstChunkIndexE = (dstOffset + bitCount - 1) >> 6;
            int srcChunkIndexE = (srcOffset + bitCount - 1) >> 6;

            int dstShiftB = dstOffset & 0x3f;
            int srcShiftB = srcOffset & 0x3f;

            int dstShiftE = (dstOffset + bitCount) & 0x3f;
            int srcShiftE = (srcOffset + bitCount) & 0x3f;

            // Mask for the first chunk
            ulong dstMaskB = ~0ul << dstShiftB;
            ulong srcMaskB = ~0ul << srcShiftB;

            // Mask for the last chunk
            ulong dstMaskE = ~0ul >> (64 - dstShiftE);
            ulong srcMaskE = ~0ul >> (64 - srcShiftE);

            if (dstChunkIndexB == dstChunkIndexE)
            {
                // Single chunk case
                dstMaskB &= dstMaskE;
                srcMaskB &= srcMaskE;

                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] &= srcBits & dstMaskB;
            }
            else
            {
                // First chunk
                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] &= srcBits & dstMaskB;

                // Middle chunks
                int shiftDelta = dstShiftB - srcShiftB;
                for (int i = dstChunkIndexB + 1, j = srcChunkIndexB + 1; i < dstChunkIndexE; i++, j++)
                    dst[i] &= LoadAlignedBits(src, j, shiftDelta);

                // Last chunk
                srcBits = src[srcChunkIndexE] & srcMaskE;
                dst[dstChunkIndexE] &= srcBits & dstMaskE;
            }
        }

        #endregion

        #region OR (dst |= src)

        public static void OrWords(ulong* dst, ulong* src, int wordCount)
        {
            int i = 0;

            if (X86.Avx2.IsAvx2Supported) // 64-byte / 256-bit AVX2 (Windows, Linux, consoles)
            {
                for (; i <= wordCount - 16; i += 16)
                {
                    var a0 = X86.Avx.mm256_loadu_si256(dst + i +  0);
                    var b0 = X86.Avx.mm256_loadu_si256(src + i +  0);
                    X86.Avx.mm256_storeu_si256(dst + i +  0, X86.Avx2.mm256_or_si256(a0, b0));

                    var a1 = X86.Avx.mm256_loadu_si256(dst + i +  4);
                    var b1 = X86.Avx.mm256_loadu_si256(src + i +  4);
                    X86.Avx.mm256_storeu_si256(dst + i +  4, X86.Avx2.mm256_or_si256(a1, b1));

                    var a2 = X86.Avx.mm256_loadu_si256(dst + i +  8);
                    var b2 = X86.Avx.mm256_loadu_si256(src + i +  8);
                    X86.Avx.mm256_storeu_si256(dst + i +  8, X86.Avx2.mm256_or_si256(a2, b2));

                    var a3 = X86.Avx.mm256_loadu_si256(dst + i + 12);
                    var b3 = X86.Avx.mm256_loadu_si256(src + i + 12);
                    X86.Avx.mm256_storeu_si256(dst + i + 12, X86.Avx2.mm256_or_si256(a3, b3));
                }
            }
            else if (Arm.Neon.IsNeonSupported) // 16-byte / 128-bit NEON (Apple, Android, Quest)
            {
                for (; i <= wordCount - 8; i += 8)
                {
                    var a0 = Arm.Neon.vld1q_u64(dst + i + 0);
                    var b0 = Arm.Neon.vld1q_u64(src + i + 0);
                    Arm.Neon.vst1q_u64(dst + i + 0, Arm.Neon.vorrq_u64(a0, b0));

                    var a1 = Arm.Neon.vld1q_u64(dst + i + 2);
                    var b1 = Arm.Neon.vld1q_u64(src + i + 2);
                    Arm.Neon.vst1q_u64(dst + i + 2, Arm.Neon.vorrq_u64(a1, b1));

                    var a2 = Arm.Neon.vld1q_u64(dst + i + 4);
                    var b2 = Arm.Neon.vld1q_u64(src + i + 4);
                    Arm.Neon.vst1q_u64(dst + i + 4, Arm.Neon.vorrq_u64(a2, b2));

                    var a3 = Arm.Neon.vld1q_u64(dst + i + 6);
                    var b3 = Arm.Neon.vld1q_u64(src + i + 6);
                    Arm.Neon.vst1q_u64(dst + i + 6, Arm.Neon.vorrq_u64(a3, b3));
                }
            }
            else if (X86.Sse2.IsSse2Supported) // legacy SSE2 fallback (old x64 / Rosetta)
            {
                for (; i <= wordCount - 2; i += 2)
                {
                    var a = X86.Sse2.loadu_si128(dst + i);
                    var b = X86.Sse2.loadu_si128(src + i);
                    X86.Sse2.storeu_si128(dst + i, X86.Sse2.or_si128(a, b));
                }
            }

            // Fallback or SIMD tail
            for (; i < wordCount; ++i)
                dst[i] |= src[i];
        }

        public static void OrBits(ulong* dst, int dstOffset, ulong* src, int srcOffset, int bitCount)
        {
            if (bitCount <= 0)
                return;

            if (IsWordAligned(dstOffset, srcOffset, bitCount))
            {
                int wordCount = bitCount >> 6;
                OrWords(dst + (dstOffset >> 6), src + (srcOffset >> 6), wordCount);
                return;
            }

            int srcChunkIndexB = srcOffset >> 6;
            int dstChunkIndexB = dstOffset >> 6;

            int dstChunkIndexE = (dstOffset + bitCount - 1) >> 6;
            int srcChunkIndexE = (srcOffset + bitCount - 1) >> 6;

            int dstShiftB = dstOffset & 0x3f;
            int srcShiftB = srcOffset & 0x3f;

            int dstShiftE = (dstOffset + bitCount) & 0x3f;
            int srcShiftE = (srcOffset + bitCount) & 0x3f;

            // Mask for the first chunk
            ulong dstMaskB = ~0ul << dstShiftB;
            ulong srcMaskB = ~0ul << srcShiftB;

            // Mask for the last chunk
            ulong dstMaskE = ~0ul >> (64 - dstShiftE);
            ulong srcMaskE = ~0ul >> (64 - srcShiftE);

            if (dstChunkIndexB == dstChunkIndexE)
            {
                // Single chunk case
                dstMaskB &= dstMaskE;
                srcMaskB &= srcMaskE;

                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] |= srcBits & dstMaskB;
            }
            else
            {
                // First chunk
                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] |= srcBits & dstMaskB;

                // Middle chunks
                int shiftDelta = dstShiftB - srcShiftB;
                for (int i = dstChunkIndexB + 1, j = srcChunkIndexB + 1; i < dstChunkIndexE; i++, j++)
                    dst[i] |= LoadAlignedBits(src, j, shiftDelta);

                // Last chunk
                srcBits = src[srcChunkIndexE] & srcMaskE;
                dst[dstChunkIndexE] |= srcBits & dstMaskE;
            }
        }

        #endregion

        #region AND NOT (dst &= ~src)

        public static void AndNotWords(ulong* dst, ulong* src, int wordCount)
        {
            int i = 0;

            if (X86.Avx2.IsAvx2Supported) // 64-byte / 256-bit AVX2 (Windows, Linux, consoles)
            {
                for (; i <= wordCount - 16; i += 16)
                {
                    var a0 = X86.Avx.mm256_loadu_si256(dst + i +  0);
                    var b0 = X86.Avx.mm256_loadu_si256(src + i +  0);
                    X86.Avx.mm256_storeu_si256(dst + i +  0, X86.Avx2.mm256_andnot_si256(b0, a0));

                    var a1 = X86.Avx.mm256_loadu_si256(dst + i +  4);
                    var b1 = X86.Avx.mm256_loadu_si256(src + i +  4);
                    X86.Avx.mm256_storeu_si256(dst + i +  4, X86.Avx2.mm256_andnot_si256(b1, a1));

                    var a2 = X86.Avx.mm256_loadu_si256(dst + i +  8);
                    var b2 = X86.Avx.mm256_loadu_si256(src + i +  8);
                    X86.Avx.mm256_storeu_si256(dst + i +  8, X86.Avx2.mm256_andnot_si256(b2, a2));

                    var a3 = X86.Avx.mm256_loadu_si256(dst + i + 12);
                    var b3 = X86.Avx.mm256_loadu_si256(src + i + 12);
                    X86.Avx.mm256_storeu_si256(dst + i + 12, X86.Avx2.mm256_andnot_si256(b3, a3));
                }
            }
            else if (Arm.Neon.IsNeonSupported) // 16-byte / 128-bit NEON (Apple, Android, Quest)
            {
                for (; i <= wordCount - 8; i += 8)
                {
                    var a0 = Arm.Neon.vld1q_u64(dst + i + 0);
                    var b0 = Arm.Neon.vld1q_u64(src + i + 0);
                    Arm.Neon.vst1q_u64(dst + i + 0, Arm.Neon.vbicq_u64(a0, b0));

                    var a1 = Arm.Neon.vld1q_u64(dst + i + 2);
                    var b1 = Arm.Neon.vld1q_u64(src + i + 2);
                    Arm.Neon.vst1q_u64(dst + i + 2, Arm.Neon.vbicq_u64(a1, b1));

                    var a2 = Arm.Neon.vld1q_u64(dst + i + 4);
                    var b2 = Arm.Neon.vld1q_u64(src + i + 4);
                    Arm.Neon.vst1q_u64(dst + i + 4, Arm.Neon.vbicq_u64(a2, b2));

                    var a3 = Arm.Neon.vld1q_u64(dst + i + 6);
                    var b3 = Arm.Neon.vld1q_u64(src + i + 6);
                    Arm.Neon.vst1q_u64(dst + i + 6, Arm.Neon.vbicq_u64(a3, b3));
                }
            }
            else if (X86.Sse2.IsSse2Supported) // legacy SSE2 fallback (old x64 / Rosetta)
            {
                for (; i <= wordCount - 2; i += 2)
                {
                    var a = X86.Sse2.loadu_si128(dst + i);
                    var b = X86.Sse2.loadu_si128(src + i);
                    X86.Sse2.storeu_si128(dst + i, X86.Sse2.andnot_si128(b, a));
                }
            }

            // Fallback or SIMD tail
            for (; i < wordCount; ++i)
                dst[i] &= ~src[i];
        }

        public static void AndNotBits(ulong* dst, int dstOffset, ulong* src, int srcOffset, int bitCount)
        {
            if (bitCount <= 0)
                return;

            if (IsWordAligned(dstOffset, srcOffset, bitCount))
            {
                int wordCount = bitCount >> 6;
                AndNotWords(dst + (dstOffset >> 6), src + (srcOffset >> 6), wordCount);
                return;
            }

            int srcChunkIndexB = srcOffset >> 6;
            int dstChunkIndexB = dstOffset >> 6;

            int dstChunkIndexE = (dstOffset + bitCount - 1) >> 6;
            int srcChunkIndexE = (srcOffset + bitCount - 1) >> 6;

            int dstShiftB = dstOffset & 0x3f;
            int srcShiftB = srcOffset & 0x3f;

            int dstShiftE = (dstOffset + bitCount) & 0x3f;
            int srcShiftE = (srcOffset + bitCount) & 0x3f;

            // Mask for the first chunk
            ulong dstMaskB = ~0ul << dstShiftB;
            ulong srcMaskB = ~0ul << srcShiftB;

            // Mask for the last chunk
            ulong dstMaskE = ~0ul >> (64 - dstShiftE);
            ulong srcMaskE = ~0ul >> (64 - srcShiftE);

            if (dstChunkIndexB == dstChunkIndexE)
            {
                // Single chunk case
                dstMaskB &= dstMaskE;
                srcMaskB &= srcMaskE;

                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] &= ~srcBits & dstMaskB;
            }
            else
            {
                // First chunk
                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] &= ~srcBits & dstMaskB;

                // Middle chunks
                int shiftDelta = dstShiftB - srcShiftB;
                for (int i = dstChunkIndexB + 1, j = srcChunkIndexB + 1; i < dstChunkIndexE; i++, j++)
                    dst[i] &= ~LoadAlignedBits(src, j, shiftDelta) & ~dstMaskB;

                // Last chunk
                srcBits = src[srcChunkIndexE] & srcMaskE;
                dst[dstChunkIndexE] &= ~srcBits & dstMaskE;
            }
        }

        #endregion

        #region XOR (dst ^= src)

        public static void XorWords(ulong* dst, ulong* src, int wordCount)
        {
            int i = 0;
            if (X86.Avx2.IsAvx2Supported) // 64-byte / 256-bit AVX2 (Windows, Linux, consoles)
            {
                for (; i <= wordCount - 16; i += 16)
                {
                    var a0 = X86.Avx.mm256_loadu_si256(dst + i +  0);
                    var b0 = X86.Avx.mm256_loadu_si256(src + i +  0);
                    X86.Avx.mm256_storeu_si256(dst + i +  0, X86.Avx2.mm256_xor_si256(a0, b0));

                    var a1 = X86.Avx.mm256_loadu_si256(dst + i +  4);
                    var b1 = X86.Avx.mm256_loadu_si256(src + i +  4);
                    X86.Avx.mm256_storeu_si256(dst + i +  4, X86.Avx2.mm256_xor_si256(a1, b1));

                    var a2 = X86.Avx.mm256_loadu_si256(dst + i +  8);
                    var b2 = X86.Avx.mm256_loadu_si256(src + i +  8);
                    X86.Avx.mm256_storeu_si256(dst + i +  8, X86.Avx2.mm256_xor_si256(a2, b2));

                    var a3 = X86.Avx.mm256_loadu_si256(dst + i + 12);
                    var b3 = X86.Avx.mm256_loadu_si256(src + i + 12);
                    X86.Avx.mm256_storeu_si256(dst + i + 12, X86.Avx2.mm256_xor_si256(a3, b3));
                }
            }
            else if (Arm.Neon.IsNeonSupported) // 16-byte / 128-bit NEON (Apple, Android, Quest)
            {
                for (; i <= wordCount - 8; i += 8)
                {
                    var a0 = Arm.Neon.vld1q_u64(dst + i + 0);
                    var b0 = Arm.Neon.vld1q_u64(src + i + 0);
                    Arm.Neon.vst1q_u64(dst + i + 0, Arm.Neon.veorq_u64(a0, b0));

                    var a1 = Arm.Neon.vld1q_u64(dst + i + 2);
                    var b1 = Arm.Neon.vld1q_u64(src + i + 2);
                    Arm.Neon.vst1q_u64(dst + i + 2, Arm.Neon.veorq_u64(a1, b1));

                    var a2 = Arm.Neon.vld1q_u64(dst + i + 4);
                    var b2 = Arm.Neon.vld1q_u64(src + i + 4);
                    Arm.Neon.vst1q_u64(dst + i + 4, Arm.Neon.veorq_u64(a2, b2));

                    var a3 = Arm.Neon.vld1q_u64(dst + i + 6);
                    var b3 = Arm.Neon.vld1q_u64(src + i + 6);
                    Arm.Neon.vst1q_u64(dst + i + 6, Arm.Neon.veorq_u64(a3, b3));
                }
            }
            else if (X86.Sse2.IsSse2Supported) // legacy SSE2 fallback (old x64 / Rosetta)
            {
                for (; i <= wordCount - 2; i += 2)
                {
                    var a = X86.Sse2.loadu_si128(dst + i);
                    var b = X86.Sse2.loadu_si128(src + i);
                    X86.Sse2.storeu_si128(dst + i, X86.Sse2.xor_si128(a, b));
                }
            }

            // Fallback or SIMD tail
            for (; i < wordCount; ++i)
                dst[i] ^= src[i];
        }

        public static void XorBits(ulong* dst, int dstOffset, ulong* src, int srcOffset, int bitCount)
        {
            if (bitCount <= 0)
                return;

            if (IsWordAligned(dstOffset, srcOffset, bitCount))
            {
                int wordCount = bitCount >> 6;
                XorWords(dst + (dstOffset >> 6), src + (srcOffset >> 6), wordCount);
                return;
            }

            int srcChunkIndexB = srcOffset >> 6;
            int dstChunkIndexB = dstOffset >> 6;

            int dstChunkIndexE = (dstOffset + bitCount - 1) >> 6;
            int srcChunkIndexE = (srcOffset + bitCount - 1) >> 6;

            int dstShiftB = dstOffset & 0x3f;
            int srcShiftB = srcOffset & 0x3f;

            int dstShiftE = (dstOffset + bitCount) & 0x3f;
            int srcShiftE = (srcOffset + bitCount) & 0x3f;

            // Mask for the first chunk
            ulong dstMaskB = ~0ul << dstShiftB;
            ulong srcMaskB = ~0ul << srcShiftB;

            // Mask for the last chunk
            ulong dstMaskE = ~0ul >> (64 - dstShiftE);
            ulong srcMaskE = ~0ul >> (64 - srcShiftE);

            if (dstChunkIndexB == dstChunkIndexE)
            {
                // Single chunk case
                dstMaskB &= dstMaskE;
                srcMaskB &= srcMaskE;

                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] ^= srcBits & dstMaskB;
            }
            else
            {
                // First chunk
                ulong srcBits = (src[srcChunkIndexB] & srcMaskB) >> srcShiftB;
                srcBits <<= dstShiftB;
                dst[dstChunkIndexB] ^= srcBits & dstMaskB;

                // Middle chunks
                int shiftDelta = dstShiftB - srcShiftB;
                for (int i = dstChunkIndexB + 1, j = srcChunkIndexB + 1; i < dstChunkIndexE; i++, j++)
                    dst[i] ^= LoadAlignedBits(src, j, shiftDelta);

                // Last chunk
                srcBits = src[srcChunkIndexE] & srcMaskE;
                dst[dstChunkIndexE] ^= srcBits & dstMaskE;
            }
        }

        #endregion

        #region Find

        public static int FindFirst(bool value, ulong* bits, int offset, int count)
        {
            if (count <= 0)
                return -1;

            int endBit = offset + count;
            int firstWord =  offset >> 6;
            int lastWord = (endBit - 1) >> 6;
            int firstShift = offset & 63;
            int lastShift = endBit & 63;

            // Single Word

            if (firstWord == lastWord)
            {
                int width  = endBit - offset;
                ulong mask = width == 64 ? ~0ul : ((1ul << width) - 1ul) << firstShift;
                ulong chunk = (value ? bits[firstWord] : ~bits[firstWord]) & mask;
                return chunk != 0 ? (firstWord << 6) + math.tzcnt(chunk) : -1;
            }

            // Head

            ulong headMask = ~0ul << firstShift;
            ulong head = (value ? bits[firstWord] : ~bits[firstWord]) & headMask;
            if (head != 0)
                return (firstWord << 6) + math.tzcnt(head);

            // Body

            for (int word = firstWord + 1; word < lastWord; ++word)
            {
                ulong chunk = value ? bits[word] : ~bits[word];
                if (chunk != 0)
                    return (word << 6) + math.tzcnt(chunk);
            }

            // Tail

            if (lastWord >= firstWord)
            {
                ulong tailMask = lastShift == 0 ? ~0ul : (1ul << lastShift) - 1ul;
                ulong tail = (value ? bits[lastWord] : ~bits[lastWord]) & tailMask;
                if (tail != 0)
                    return (lastWord << 6) + math.tzcnt(tail);
            }

            return -1;
        }

        public static int FindLast(bool value, ulong* bits, int offset, int count)
        {
            if (count <= 0)
                return -1;

            int endBit = offset + count;
            int firstWord =  offset >> 6;
            int lastWord = (endBit - 1) >> 6;
            int firstShift = offset &  63;
            int lastShift = (endBit - 1) &  63;

            // Single word

            if (firstWord == lastWord)
            {
                int width  = endBit - offset;
                ulong mask = width == 64 ? ~0ul : ((1ul << width) - 1ul) << firstShift;
                ulong chunk = (value ? bits[firstWord] : ~bits[firstWord]) & mask;
                return chunk != 0 ? (firstWord << 6) + (63 - math.lzcnt(chunk)) : -1;
            }

            // Tail

            ulong tailMask = (1ul << (lastShift + 1)) - 1ul;
            ulong tail = (value ? bits[lastWord] : ~bits[lastWord]) & tailMask;
            if (tail != 0)
                return (lastWord << 6) + (63 - math.lzcnt(tail));

            // Body

            for (int word = lastWord - 1; word > firstWord; --word)
            {
                ulong chunk = value ? bits[word] : ~bits[word];
                if (chunk != 0)
                    return (word << 6) + (63 - math.lzcnt(chunk));
            }

            // Head

            ulong headMask = ~0ul << firstShift;
            ulong head = (value ? bits[firstWord] : ~bits[firstWord]) & headMask;
            if (head != 0)
                return (firstWord << 6) + (63 - math.lzcnt(head));

            return -1;
        }

        #endregion
    }
}
