using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using System.Linq.Expressions;
using Unity.Collections;
using Unity.Profiling;

namespace MA.Flora
{
    [GenerateBurstMonoInterop]
    internal static partial class GraphicsBufferUtility
    {
        private static class Profiling
        {
            public static readonly ProfilerMarker MemsetMarker = new ProfilerMarker("GraphicsBufferUtility.Memset");
            public static readonly ProfilerMarker MemcpyMarker = new ProfilerMarker("GraphicsBufferUtility.Memcpy");
            public static readonly ProfilerMarker ScatterMarker = new ProfilerMarker("GraphicsBufferUtility.Scatter");
        }

        private static class Compute
        {
            public static ComputeShader Shader;
            public static CommandBuffer CommandBuffer = new CommandBuffer();

            public static int MemsetKernel;
            public static int MemcpyKernel;
            public static int ScatterKernel;

            public static LocalKeyword RawUIntKeyword;
            public static LocalKeyword RawUInt4AlignedKeyword;

            public static LocalKeyword StructuredUInt1Keyword;
            public static LocalKeyword StructuredUInt2Keyword;
            public static LocalKeyword StructuredUInt4Keyword;
            public static LocalKeyword StructuredUInt8Keyword;
        }

        private static class LocalNameID
        {
            public static readonly int Value = Shader.PropertyToID("_Value");
            public static readonly int Size = Shader.PropertyToID("_Size");

            public static readonly int SrcOffset = Shader.PropertyToID("_SrcOffset");
            public static readonly int DstOffset = Shader.PropertyToID("_DstOffset");

            public static readonly int SrcByteBuffer = Shader.PropertyToID("_SrcByteBuffer");
            public static readonly int DstByteBuffer = Shader.PropertyToID("_DstByteBuffer");

            public static readonly int ScatterCount = Shader.PropertyToID("_ScatterCount");
            public static readonly int ScatterByteBuffer = Shader.PropertyToID("_ScatterByteBuffer");
            public static readonly int UploadByteBuffer = Shader.PropertyToID("_UploadByteBuffer");

            public static readonly int ScatterStructuredBuffer = Shader.PropertyToID("_ScatterStructuredBuffer");
            public static readonly int UploadStructuredBuffer1 = Shader.PropertyToID("_UploadStructuredBuffer1x");
            public static readonly int UploadStructuredBuffer2 = Shader.PropertyToID("_UploadStructuredBuffer2x");
            public static readonly int UploadStructuredBuffer4 = Shader.PropertyToID("_UploadStructuredBuffer4x");
            public static readonly int UploadStructuredBuffer8 = Shader.PropertyToID("_UploadStructuredBuffer8x");

            public static readonly int SrcStructuredBuffer1 = Shader.PropertyToID("_SrcStructuredBuffer1x");
            public static readonly int SrcStructuredBuffer2 = Shader.PropertyToID("_SrcStructuredBuffer2x");
            public static readonly int SrcStructuredBuffer4 = Shader.PropertyToID("_SrcStructuredBuffer4x");
            public static readonly int SrcStructuredBuffer8 = Shader.PropertyToID("_SrcStructuredBuffer8x");

            public static readonly int DstStructuredBuffer1 = Shader.PropertyToID("_DstStructuredBuffer1x");
            public static readonly int DstStructuredBuffer2 = Shader.PropertyToID("_DstStructuredBuffer2x");
            public static readonly int DstStructuredBuffer4 = Shader.PropertyToID("_DstStructuredBuffer4x");
            public static readonly int DstStructuredBuffer8 = Shader.PropertyToID("_DstStructuredBuffer8x");
        }

        private static class Delegates
        {
            public delegate void GraphicsBufferSetDataDelegate(GraphicsBuffer buffer, IntPtr data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int elemSize);
            public static GraphicsBufferSetDataDelegate GraphicsBufferSetData;

            public delegate void CommandBufferSetDataBufferDataGbDelegate(CommandBuffer cmd, GraphicsBuffer buffer, IntPtr data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int elemSize);
            public static CommandBufferSetDataBufferDataGbDelegate CmdSetDataBufferDataGb;

            public delegate CommandBuffer GetWrappedCommandBufferDelegate(ComputeCommandBuffer cmd);
            public static GetWrappedCommandBufferDelegate GetWrappedCommandBuffer;
        }

        private const int ThreadGroupSize = 64;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void InitializeOnLoad()
        {
            MethodInfo gbInternalSetNativeDataInfo
                = typeof(GraphicsBuffer).GetMethod("InternalSetNativeData", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Delegates.GraphicsBufferSetData
                = (Delegates.GraphicsBufferSetDataDelegate)Delegate.CreateDelegate(typeof(Delegates.GraphicsBufferSetDataDelegate), gbInternalSetNativeDataInfo);

            MethodInfo cmdInternalSetCbNativeDataInfoGb
                = typeof(CommandBuffer).GetMethod("InternalSetGraphicsBufferNativeData", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Delegates.CmdSetDataBufferDataGb
                = (Delegates.CommandBufferSetDataBufferDataGbDelegate)Delegate.CreateDelegate(typeof(Delegates.CommandBufferSetDataBufferDataGbDelegate), cmdInternalSetCbNativeDataInfoGb);

            FieldInfo cmdWrappedCommandBufferInfo = typeof(ComputeCommandBuffer).GetField("m_WrappedCommandBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ParameterExpression parameter = Expression.Parameter(typeof(ComputeCommandBuffer), "instance");
            MemberExpression fieldAccess = Expression.Field(parameter, cmdWrappedCommandBufferInfo);
            Expression<Delegates.GetWrappedCommandBufferDelegate> lambda = Expression.Lambda<Delegates.GetWrappedCommandBufferDelegate>(fieldAccess, parameter);
            Delegates.GetWrappedCommandBuffer = lambda.Compile();
        }

        public static void Initialize(FloraRuntimeResources runtimeResources)
        {
            Compute.Shader = runtimeResources.GraphicsBufferUtilityCS;
            Compute.MemsetKernel = Compute.Shader.FindKernel("MemsetCS");
            Compute.MemcpyKernel = Compute.Shader.FindKernel("MemcpyCS");
            Compute.ScatterKernel = Compute.Shader.FindKernel("ScatterCS");

            Compute.RawUIntKeyword = new LocalKeyword(Compute.Shader, "TYPE_RAW_UINT");
            Compute.RawUInt4AlignedKeyword = new LocalKeyword(Compute.Shader, "TYPE_RAW_UINT4_ALIGNED");
            Compute.StructuredUInt1Keyword = new LocalKeyword(Compute.Shader, "TYPE_STRUCTURED_UINT1");
            Compute.StructuredUInt2Keyword = new LocalKeyword(Compute.Shader, "TYPE_STRUCTURED_UINT2");
            Compute.StructuredUInt4Keyword = new LocalKeyword(Compute.Shader, "TYPE_STRUCTURED_UINT4");
            Compute.StructuredUInt8Keyword = new LocalKeyword(Compute.Shader, "TYPE_STRUCTURED_UINT8");
        }

        private static void ResetInternal()
        {
            Compute.Shader.DisableKeyword(Compute.RawUIntKeyword);
            Compute.Shader.DisableKeyword(Compute.RawUInt4AlignedKeyword);
            Compute.Shader.DisableKeyword(Compute.StructuredUInt1Keyword);
            Compute.Shader.DisableKeyword(Compute.StructuredUInt2Keyword);
            Compute.Shader.DisableKeyword(Compute.StructuredUInt4Keyword);
            Compute.Shader.DisableKeyword(Compute.StructuredUInt8Keyword);
        }

        private static void ResetKeywords()
        {
            if (Compute.Shader == null)
                Initialize(FloraSystem.Instance.Resources);

            ResetInternal();
        }

        private static void ResetKeywords(CommandBuffer cmd)
        {
            if (Compute.Shader == null)
                Initialize(FloraSystem.Instance.Resources);

            var cs = Compute.Shader;
            cmd.DisableKeyword(cs, Compute.RawUIntKeyword);
            cmd.DisableKeyword(cs, Compute.RawUInt4AlignedKeyword);
            cmd.DisableKeyword(cs, Compute.StructuredUInt1Keyword);
            cmd.DisableKeyword(cs, Compute.StructuredUInt2Keyword);
            cmd.DisableKeyword(cs, Compute.StructuredUInt4Keyword);
            cmd.DisableKeyword(cs, Compute.StructuredUInt8Keyword);
        }

        private enum StructuredBufferSize
        {
            Size1,
            Size2,
            Size4,
            Size8,
        }

        private static StructuredBufferSize GetStructuredBufferElementSize(int strideInBytes)
        {
            switch (strideInBytes)
            {
                case 32:
                    return StructuredBufferSize.Size8;
                case 16:
                    return StructuredBufferSize.Size4;
                case 8:
                    return StructuredBufferSize.Size2;
                case 4:
                    return StructuredBufferSize.Size1;
                default:
                {
                    int log2ElementCount = math.floorlog2(strideInBytes / 4);
                    Assert.IsTrue(strideInBytes % 4 == 0 && math.ispow2(strideInBytes / 4), "Stride must be multiple of 4.");
                    return (StructuredBufferSize)log2ElementCount;
                }
            }
        }

        public static void Memset(GraphicsBuffer buffer, int value, int offset, int count)
        {
            var cmd = Compute.CommandBuffer;
            Memset(cmd, buffer, value, offset, count);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static void Memset(CommandBuffer cmd, GraphicsBuffer buffer, int value, int offset, int count)
        {
            if (count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.count)
                throw new ArgumentOutOfRangeException(nameof(offset), $"Offset must be in the range [0,{buffer.count})");
            if (count < 0 || offset + count > buffer.count)
                throw new ArgumentOutOfRangeException(nameof(count), $"Count must be in the range [0,{buffer.count - offset})");
#endif

            cmd.BeginSample(Profiling.MemsetMarker);
            ResetKeywords(cmd);

            var cs = Compute.Shader;
            var kernel = Compute.MemsetKernel;
            int elementsPerThread = 1;

            switch (buffer.target)
            {
                case GraphicsBuffer.Target.Raw:
                {
                    cmd.EnableKeyword(cs, Compute.RawUIntKeyword);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstByteBuffer, buffer);
                    elementsPerThread = 4;
                    break;
                }
                case GraphicsBuffer.Target.Structured:
                {
                    StructuredBufferSize size = GetStructuredBufferElementSize(buffer.stride);
                    switch (size)
                    {
                        case StructuredBufferSize.Size1:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt1Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer1, buffer);
                            break;
                        case StructuredBufferSize.Size2:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt2Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer2, buffer);
                            break;
                        case StructuredBufferSize.Size4:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt4Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer4, buffer);
                            break;
                        case StructuredBufferSize.Size8:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt8Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer8, buffer);
                            break;
                        default:
                            throw new ArgumentException("Unsupported stride for structured buffer (supported sizes are 4,8,16).");
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"Unsupported buffer target: {buffer.target}");
            }

            cmd.SetComputeIntParam(cs, LocalNameID.SrcOffset, 0);
            cmd.SetComputeIntParam(cs, LocalNameID.DstOffset, offset);
            cmd.SetComputeIntParam(cs, LocalNameID.Value, value);
            cmd.SetComputeIntParam(cs, LocalNameID.Size, count);

            int dispatchThreadCount = (count + elementsPerThread - 1) / elementsPerThread;
            int3 threadGroups = ComputeUtility.WrapDispatchCount(dispatchThreadCount, ThreadGroupSize);
            cmd.DispatchCompute(cs, kernel, threadGroups);
            cmd.EndSample(Profiling.MemsetMarker);
        }

        public static void Memcpy(GraphicsBuffer dstBuffer, GraphicsBuffer srcBuffer, int srcOffset, int dstOffset, int count)
        {
            var cmd = Compute.CommandBuffer;
            Memcpy(cmd, dstBuffer, srcBuffer, srcOffset, dstOffset, count);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static void Memcpy(CommandBuffer cmd, GraphicsBuffer dstBuffer, GraphicsBuffer srcBuffer, int srcOffset, int dstOffset, int count)
        {
            if (count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (srcBuffer.target != dstBuffer.target)
                throw new ArgumentException("Source and destination buffers must have the same target.", nameof(dstBuffer));
            if (srcOffset < 0 || srcOffset >= srcBuffer.count)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), $"Source offset must be in the range [0,{srcBuffer.count})");
            if (dstOffset < 0 || dstOffset >= dstBuffer.count)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), $"Destination offset must be in the range [0,{dstBuffer.count})");
            if (count < 0 || srcOffset + count > srcBuffer.count || dstOffset + count > dstBuffer.count)
                throw new ArgumentOutOfRangeException(nameof(count), $"Count must be in the range [0,{math.min(srcBuffer.count - srcOffset, dstBuffer.count - dstOffset)})");
#endif

            cmd.BeginSample(Profiling.MemcpyMarker);
            ResetKeywords(cmd);

            var cs = Compute.Shader;
            var kernel = Compute.MemcpyKernel;
            int elementsPerThread = 1;

            switch (dstBuffer.target)
            {
                case GraphicsBuffer.Target.Raw:
                {
                    cmd.EnableKeyword(cs, Compute.RawUIntKeyword);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.SrcByteBuffer, srcBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstByteBuffer, dstBuffer);
                    elementsPerThread = 4;
                    break;

                }
                case GraphicsBuffer.Target.Structured:
                {
                    StructuredBufferSize size = GetStructuredBufferElementSize(dstBuffer.stride);
                    switch (size)
                    {
                        case StructuredBufferSize.Size1:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt1Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.SrcStructuredBuffer1, srcBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer1, dstBuffer);
                            break;
                        case StructuredBufferSize.Size2:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt2Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.SrcStructuredBuffer2, srcBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer2, dstBuffer);
                            break;
                        case StructuredBufferSize.Size4:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt4Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.SrcStructuredBuffer4, srcBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer4, dstBuffer);
                            break;
                        case StructuredBufferSize.Size8:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt8Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.SrcStructuredBuffer8, srcBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer8, dstBuffer);
                            break;
                        default:
                            throw new ArgumentException("Unsupported stride for structured buffer (supported sizes are 4,8,16).");
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"Unsupported buffer target: {dstBuffer.target}");
            }

            cmd.SetComputeIntParam(cs, LocalNameID.SrcOffset, srcOffset);
            cmd.SetComputeIntParam(cs, LocalNameID.DstOffset, dstOffset);
            cmd.SetComputeIntParam(cs, LocalNameID.Size, count);

            int dispatchThreadCount = (count + elementsPerThread - 1) / elementsPerThread;
            int3 threadGroups = ComputeUtility.WrapDispatchCount(dispatchThreadCount, ThreadGroupSize);
            cmd.DispatchCompute(cs, kernel, threadGroups);
            cmd.EndSample(Profiling.MemcpyMarker);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scatter<T>(CommandBuffer cmd, GraphicsBuffer dstBuffer, NativeArray<T> values, NativeArray<uint> offsets) where T : unmanaged
        {
            if (values.Length == 0)
                return;

            bool isRawBuffer = dstBuffer.target == GraphicsBuffer.Target.Raw;
            GraphicsBuffer valuesBuffer;
            GraphicsBuffer offsetBuffer;

            if (isRawBuffer)
            {
                valuesBuffer = GraphicsBufferStore.RequestRaw(cmd, values);
                offsetBuffer = GraphicsBufferStore.RequestRaw(cmd, offsets);
            }
            else
            {
                valuesBuffer = GraphicsBufferStore.RequestStructured(cmd, values);
                offsetBuffer = GraphicsBufferStore.RequestStructured(cmd, offsets);
            }

            Scatter(cmd, dstBuffer, valuesBuffer, offsetBuffer, values.Length, UnsafeUtility.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scatter<T>(CommandBuffer cmd, GraphicsBuffer dstBuffer, NativeArray<T> values, GraphicsBuffer offsetBuffer) where T : unmanaged
        {
            if (values.Length == 0)
                return;

            bool isRawBuffer = dstBuffer.target == GraphicsBuffer.Target.Raw;
            GraphicsBuffer valuesBuffer = isRawBuffer ? GraphicsBufferStore.RequestRaw(cmd, values) : GraphicsBufferStore.RequestStructured(cmd, values);
            Scatter(cmd, dstBuffer, valuesBuffer, offsetBuffer, values.Length, UnsafeUtility.SizeOf<T>());
        }

        public static void Scatter(CommandBuffer cmd, GraphicsBuffer dstBuffer, GraphicsBuffer valueBuffer, GraphicsBuffer offsetBuffer, int count, int stride)
        {
            if (count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (stride < 4)
                throw new ArgumentOutOfRangeException(nameof(stride), "Stride must be greater than 4.");
            if (offsetBuffer.count < count)
                throw new ArgumentOutOfRangeException(nameof(count), $"Scatter count must be in the range [0,{offsetBuffer.count})");
            if (valueBuffer.count * valueBuffer.stride < count * stride)
                throw new ArgumentOutOfRangeException(nameof(valueBuffer), $"Upload count must be in the range [0,{valueBuffer.count})");
            if (dstBuffer.count * dstBuffer.stride % stride != 0)
                throw new ArgumentException("Destination buffer size must be a multiple of stride.", nameof(dstBuffer));
            if (dstBuffer.count * dstBuffer.stride % 4 != 0)
                throw new ArgumentException("Destination buffer size must be a multiple of 4.", nameof(dstBuffer));
            if (dstBuffer.target != offsetBuffer.target || dstBuffer.target != valueBuffer.target)
                throw new ArgumentException("Source and destination buffers must have the same target.", nameof(dstBuffer));
#endif

            cmd.BeginSample(Profiling.ScatterMarker);
            ResetKeywords(cmd);

            var cs = Compute.Shader;
            var kernel = Compute.ScatterKernel;
            int bytesPerThread;
            int elementsPerScatter;

            switch (dstBuffer.target)
            {
                case GraphicsBuffer.Target.Raw:
                {
                    bytesPerThread = (stride & 15) == 0 ? 16 : 4;
                    elementsPerScatter = math.max(1, stride / bytesPerThread);

                    cmd.EnableKeyword(cs, bytesPerThread == 16 ? Compute.RawUInt4AlignedKeyword : Compute.RawUIntKeyword);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.ScatterByteBuffer, offsetBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.UploadByteBuffer, valueBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstByteBuffer, dstBuffer);
                    break;
                }
                case GraphicsBuffer.Target.Structured:
                {
                    bytesPerThread = stride;
                    elementsPerScatter = 1;

                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID.ScatterStructuredBuffer, offsetBuffer);

                    StructuredBufferSize size = GetStructuredBufferElementSize(bytesPerThread);
                    switch (size)
                    {
                        case StructuredBufferSize.Size1:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt1Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.UploadStructuredBuffer1, valueBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer1, dstBuffer);
                            break;
                        case StructuredBufferSize.Size2:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt2Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.UploadStructuredBuffer2, valueBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer2, dstBuffer);
                            break;
                        case StructuredBufferSize.Size4:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt4Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.UploadStructuredBuffer4, valueBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer4, dstBuffer);
                            break;
                        case StructuredBufferSize.Size8:
                            cmd.EnableKeyword(cs, Compute.StructuredUInt8Keyword);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.UploadStructuredBuffer8, valueBuffer);
                            cmd.SetComputeBufferParam(cs, kernel, LocalNameID.DstStructuredBuffer8, dstBuffer);
                            break;
                        default:
                            throw new ArgumentException("Unsupported stride for structured buffer (supported sizes are 4,8,16,32).");
                    }
                    break;
                }
                default:
                    throw new ArgumentException($"Unsupported buffer target: {dstBuffer.target}");
            }

            cmd.SetComputeIntParam(cs, LocalNameID.ScatterCount, count);
            cmd.SetComputeIntParam(cs, LocalNameID.Size, elementsPerScatter);
            cmd.SetComputeIntParam(cs, LocalNameID.SrcOffset, 0);
            cmd.SetComputeIntParam(cs, LocalNameID.DstOffset, 0);

            int threadCount = count * elementsPerScatter;
            int3 groupCount = ComputeUtility.WrapDispatchCount(threadCount, ThreadGroupSize);
            cmd.DispatchCompute(cs, kernel, groupCount);
            cmd.EndSample(Profiling.ScatterMarker);
        }

        public static bool ResizeIfNeeded(ref GraphicsBuffer buffer, int stride, int sizeInBytes, GraphicsBuffer.Target target, string debugName = null)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (stride <= 0)
                throw new ArgumentOutOfRangeException(nameof(stride), "Stride must be greater than 0.");
            if (sizeInBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be greater than 0.");
            if (sizeInBytes % stride != 0)
                throw new ArgumentException("Size must be a multiple of stride.", nameof(sizeInBytes));
            if (buffer != null && buffer.count * buffer.stride % stride != 0)
                throw new ArgumentException("Buffer size must be a multiple of stride.", nameof(buffer));
#endif

            int newCount = sizeInBytes / stride;
            int oldCount = buffer != null ? (buffer.count * buffer.stride) / stride : 0;

            if (buffer == null)
            {
                buffer = new GraphicsBuffer(target, newCount, stride);
                buffer.name = debugName;
                return true;
            }
            else if (newCount != oldCount)
            {
                GraphicsBuffer newBuffer = new GraphicsBuffer(target, newCount, stride);
                if (!string.IsNullOrEmpty(debugName))
                    newBuffer.name = debugName;

                Memcpy(newBuffer, buffer, 0, 0, math.min(newCount, oldCount));

                buffer.Dispose();
                buffer = newBuffer;
                return true;
            }

            return false;
        }

        public static bool ResizeSOAIfNeeded(ref GraphicsBuffer buffer, int stride, int sizeInBytes, int arrayCount, GraphicsBuffer.Target target, string debugName = null)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (stride <= 0)
                throw new ArgumentOutOfRangeException(nameof(stride), "Stride must be greater than 0.");
            if (sizeInBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be greater than 0.");
            if (sizeInBytes % stride != 0)
                throw new ArgumentException("Size must be a multiple of stride.", nameof(sizeInBytes));
            if (buffer != null && buffer.count * buffer.stride % stride != 0)
                throw new ArgumentException("Buffer size must be a multiple of stride.", nameof(buffer));
#endif

            int newCount = sizeInBytes / stride;
            int oldCount = buffer != null ? (buffer.count * buffer.stride) / stride : 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (newCount % arrayCount != 0)
                throw new ArgumentException("Size must be a multiple of arrayCount.", nameof(sizeInBytes));
            if (oldCount % arrayCount != 0)
                throw new ArgumentException("Buffer size must be a multiple of arrayCount.", nameof(buffer));
#endif

            if (buffer == null)
            {
                buffer = new GraphicsBuffer(target, newCount, stride);
                buffer.name = debugName;
                return true;
            }
            else if (oldCount != newCount)
            {
                GraphicsBuffer newBuffer = new GraphicsBuffer(target, newCount, stride);
                if (!string.IsNullOrEmpty(debugName))
                    newBuffer.name = debugName;

                int oldArrayCount = oldCount / arrayCount;
                int newArrayCount = newCount / arrayCount;
                int copyCount = math.min(oldArrayCount, newArrayCount);

                for (int i = 0; i < arrayCount; i++)
                {
                    Memcpy(newBuffer, buffer, i * oldArrayCount, i * newArrayCount, copyCount);
                }

                buffer.Dispose();
                buffer = newBuffer;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CommandBuffer GetWrappedCommandBuffer(this ComputeCommandBuffer cmd)
        {
            return Delegates.GetWrappedCommandBuffer(cmd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetData(this GraphicsBuffer buffer, void* ptr, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int stride)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            long sizeInBytes = count * (long)stride;
            long rangeInBytes = nativeBufferStartIndex * (long)stride;
            long bufferSizeInBytes = buffer.count * (long)buffer.stride;
            if (rangeInBytes < 0 || rangeInBytes + sizeInBytes > bufferSizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(count), "The specified range is out of bounds of the GraphicsBuffer.");
#endif
            Delegates.GraphicsBufferSetData(buffer, (IntPtr)ptr, nativeBufferStartIndex, graphicsBufferStartIndex, count, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetBufferData(this CommandBuffer cmd, GraphicsBuffer buffer, void* ptr, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int stride)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            long sizeInBytes = count * (long)stride;
            long rangeInBytes = nativeBufferStartIndex * (long)stride;
            long bufferSizeInBytes = buffer.count * (long)buffer.stride;
            if (rangeInBytes < 0 || rangeInBytes + sizeInBytes > bufferSizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(count), "The specified range is out of bounds of the GraphicsBuffer.");
#endif
            Delegates.CmdSetDataBufferDataGb(cmd, buffer, (IntPtr)ptr, nativeBufferStartIndex, graphicsBufferStartIndex, count, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetData(this GraphicsBuffer buffer, void* ptr, int count, int stride)
        {
            SetData(buffer, ptr, 0, 0, count, stride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetBufferData(this CommandBuffer cmd, GraphicsBuffer buffer, void* ptr, int count, int stride)
        {
            SetBufferData(cmd, buffer, ptr, 0, 0, count, stride);
        }
    }
}
