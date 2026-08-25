// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct CompressedTransform
    {
        public float3 Position;
        public half4 Rotation;
        public half3 Scale;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator CompressedTransform(FloraInstanceTransform instanceTransform)
        {
            return new CompressedTransform
            {
                Position = instanceTransform.Position,
                Rotation = new half4(instanceTransform.Rotation.value),
                Scale = new half3(instanceTransform.Scale),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FloraInstanceTransform(CompressedTransform transform)
        {
            return new FloraInstanceTransform
            {
                Position = transform.Position,
                Rotation = new quaternion(new float4(transform.Rotation)),
                Scale = transform.Scale,
            };
        }
    }

    [BurstCompile]
    internal static class SerializationHelpers
    {
        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance, CompileSynchronously = true)]
        private unsafe struct WriteCompressedTransformsJob : IJob
        {
            public int Length;
            [ReadOnly,  NoAlias, NativeDisableUnsafePtrRestriction] public FloraInstanceTransform* Src;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public CompressedTransform* Dst;

            public void Execute()
            {
                for (int i = 0; i < Length; i++)
                    Dst[i] = Src[i];
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance, CompileSynchronously = true)]
        private unsafe struct ReadCompressedTransformsJob : IJob
        {
            public int Length;
            [ReadOnly,  NoAlias, NativeDisableUnsafePtrRestriction] public CompressedTransform* Src;
            [WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction] public FloraInstanceTransform* Dst;

            public void Execute()
            {
                for (int i = 0; i < Length; i++)
                    Dst[i] = Src[i];
            }
        }

        internal static unsafe int SerializeTransformsToByteArray(NativeArray<FloraInstanceTransform> transforms, ref byte[] compressedBytes)
        {
            if (transforms.Length > 0)
            {
                int length = transforms.Length;
                int compressedSize = transforms.Length * UnsafeUtility.SizeOf<CompressedTransform>();
                if (compressedBytes == null || compressedBytes.Length != compressedSize)
                    compressedBytes = new byte[compressedSize];

                fixed (byte* dstPtr = compressedBytes)
                {
                    new WriteCompressedTransformsJob
                    {
                        Length = length,
                        Src = (FloraInstanceTransform*)transforms.GetUnsafeReadOnlyPtr(),
                        Dst = (CompressedTransform*)dstPtr,
                    }.Execute();
                }

                return length;
            }
            else
            {
                compressedBytes = Array.Empty<byte>();
                return 0;
            }
        }

        internal static unsafe void DeserializeByteArrayToTransforms(ref byte[] compressedBytes, int serializedCount, NativeList<FloraInstanceTransform> transforms)
        {
            if (serializedCount > 0 && compressedBytes is { Length: > 0 })
            {
                int compressedCount = compressedBytes.Length / UnsafeUtility.SizeOf<CompressedTransform>();
                if (compressedCount != serializedCount)
                {
                    Debug.LogError($"Serialized count {serializedCount} does not match compressed count {compressedCount}");
                    transforms.Clear();
                    compressedBytes = Array.Empty<byte>();
                    return;
                }

                transforms.Resize(serializedCount, NativeArrayOptions.UninitializedMemory);

                fixed (byte* srcPtr = compressedBytes)
                {
                    new ReadCompressedTransformsJob
                    {
                        Length = serializedCount,
                        Src = (CompressedTransform*)srcPtr,
                        Dst = transforms.GetUnsafePtr(),
                    }.Execute();
                }
            }
            else
            {
                transforms.Clear();
            }

            compressedBytes = Array.Empty<byte>();
        }
    }
}
