// Copyright © Magnetic Arcade. All Rights Reserved.

#if DEBUG
#define DEBUG_BUFFER_NAMES
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Determines the alignment of a <see cref="GraphicsBuffer"/>.
    /// </summary>
    internal enum GraphicsBufferAlignment
    {
        /// <summary>
        /// Default alignment (none).
        /// </summary>
        None,
        /// <summary>
        /// Aligns the buffer to the next page size (64 KB by default).
        /// </summary>
        Page,
        /// <summary>
        /// Aligns the buffer to the next power of two in size.
        /// </summary>
        PowerOfTwo
    }

    /// <summary>
    /// Determines how a <see cref="GraphicsBuffer"/> is managed in the store.
    /// </summary>
    internal enum GraphicsBufferStoreType
    {
        /// <summary>
        /// A persistent buffer managed by the <see cref="GraphicsBufferStore"/>.
        /// </summary>
        Persistent,
        /// <summary>
        /// Buffer is pooled, i.e. can be shared between frames and only released when stale.
        /// </summary>
        Pooled,
        /// <summary>
        /// Buffer managed by the user, not the global store pool.
        /// </summary>
        Transient,
    }

    /// <summary>
    /// A global store for <see cref="GraphicsBuffer"/> references.
    /// Tracks allocation, deallocation, and offers optional pooling of buffers.
    /// </summary>
    internal static class GraphicsBufferStore
    {
        #region Public

        /// <summary>
        /// The number of currently allocated buffers in the store.
        /// </summary>
        public static int AllocatedBufferCount => s_Shared.AllocatedBufferIndices.Count;

        /// <summary>
        /// Computes the total allocated size of all currently allocated buffers in bytes.
        /// </summary>
        public static long ComputeAllocatedSizeInBytes()
        {
            var allocatedIndices = s_Shared.AllocatedBufferIndices;
            var allocatedBytes = 0L;

            for (var i = 0; i < allocatedIndices.Count; i++)
            {
                int allocatedIndex = allocatedIndices[i];
                var bufferData = s_Shared.BufferInfoArray[allocatedIndex];
                if (bufferData.IsCreated)
                {
                    allocatedBytes += bufferData.Descriptor.SizeInBytes;
                }
            }

            return allocatedBytes;
        }

        /// <summary>
        /// Debug information for a buffer in the store.
        /// </summary>
        public struct DebugBufferInfo
        {
            public GraphicsBufferStoreType StoreType;
            public GraphicsBufferDescriptor Descriptor;
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            public string DebugName;
#endif
        }

        /// <summary>
        /// Gets debug information for all currently allocated buffers in the store.
        /// </summary>
        public static void GetDebugBufferInfos(List<DebugBufferInfo> outInfos)
        {
            outInfos.Clear();
            var allocatedIndices = s_Shared.AllocatedBufferIndices;

            for (var i = 0; i < allocatedIndices.Count; i++)
            {
                int allocatedIndex = allocatedIndices[i];
                var bufferData = s_Shared.BufferInfoArray[allocatedIndex];
                if (bufferData.IsCreated)
                {
                    outInfos.Add(new DebugBufferInfo
                    {
                        StoreType = bufferData.StoreType,
                        Descriptor = bufferData.Descriptor,
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
                        DebugName = bufferData.DebugName,
#endif
                    });
                }
            }
        }

        /// <summary>
        /// Advances the store to the next frame.
        /// This will also attempt to release any stale buffers in the pool.
        /// </summary>
        public static void NextFrame()
        {
            const int framesUntilRelease = 30;
            ref var pooledIndices = ref s_Shared.PooledBufferIndices;

            for (var i = pooledIndices.Count - 1; i >= 0; i--)
            {
                int pooledIndex = pooledIndices[i];
                var bufferData = s_Shared.BufferInfoArray[pooledIndex];
                var isStale = s_Shared.FrameIndex - bufferData.LastUsedFrame > framesUntilRelease;
                var isNotLockedOrInFlight = bufferData is { IsLocked: false, InFlight: false };
                if (isStale && isNotLockedOrInFlight)
                {
                    DestroyBufferImmediate(pooledIndex);
                }
            }

            s_Shared.FrameIndex++;
        }

        /// <summary>
        /// Releases *all* buffers in the store, destroying them.
        /// </summary>
        public static void ReleaseAll()
        {
            for (int i = s_Shared.AllocatedBufferIndices.Count - 1; i >= 0; i--)
            {
                int bufferIndex = s_Shared.AllocatedBufferIndices[i];
                DestroyBufferImmediate(bufferIndex);
            }

            s_Shared.FreeIndices.Clear();
            s_Shared.PooledBufferIndices.Clear();
            s_Shared.AllocatedBufferIndices.Clear();
            s_Shared.BufferInfoArray = new GraphicsBufferInfo[1];
            s_Shared.Buffers = new GraphicsBuffer[1];
            s_Shared.NextBufferIndex = 1;
        }

        /// <summary>
        /// Checks whether the given <see cref="GraphicsBufferRef"/> is valid and currently tracked.
        /// </summary>
        public static bool Exists(GraphicsBufferRef buffer)
        {
            return buffer.Index > 0 &&
                   buffer.Index < s_Shared.BufferInfoArray.Length &&
                   s_Shared.BufferInfoArray[buffer.Index].IsCreated;
        }

        /// <summary>
        /// Checks whether the given <see cref="GraphicsBufferRef"/> exists and is a valid buffer.
        /// </summary>
        public static bool IsValid(GraphicsBufferRef buffer)
        {
            if (!Exists(buffer))
                return false;

            return s_Shared.Buffers[buffer.Index]?.IsValid() ?? false;
        }

        /// <summary>
        /// Creates and stores a new <see cref="GraphicsBufferRef"/> with the specified descriptor.
        /// </summary>
        public static GraphicsBufferRef Create(GraphicsBuffer.Target target, GraphicsBuffer.UsageFlags usageFlags, int count, int stride, string name)
        {
            var descriptor = new GraphicsBufferDescriptor(target, usageFlags, stride, count);
            return CreateInternal(descriptor, name, GraphicsBufferStoreType.Persistent);
        }

        /// <summary>
        /// Releases the given <see cref="GraphicsBufferRef"/> if it exists in the store.
        /// Pooled buffers are not immediately disposed, but regular ones are.
        /// </summary>
        public static void Release(GraphicsBufferRef buffer)
        {
            if (Exists(buffer))
                ReleaseBuffer(buffer.Index);
        }

        /// <summary>
        /// Gets the managed <see cref="GraphicsBuffer"/> instance for a given reference, or null if invalid.
        /// </summary>
        public static GraphicsBuffer GetBufferUnchecked(GraphicsBufferRef buffer)
        {
            if (!Exists(buffer))
                return null;

            return s_Shared.Buffers[buffer.Index];
        }

        /// <summary>
        /// Gets the descriptor for the given buffer reference.
        /// </summary>
        public static GraphicsBufferDescriptor GetDescriptor(GraphicsBufferRef buffer)
        {
            CheckExists(buffer);
            return s_Shared.BufferInfoArray[buffer.Index].Descriptor;
        }

        /// <summary>
        /// Gets the low-level <see cref="GraphicsBufferHandle"/> for the given reference.
        /// </summary>
        public static GraphicsBufferHandle GetBufferHandle(GraphicsBufferRef buffer)
        {
            CheckExists(buffer);
            return s_Shared.Buffers[buffer.Index].bufferHandle;
        }

        /// <summary>
        /// Gets the debug name stored for the given buffer.
        /// </summary>
        public static string GetDebugName(GraphicsBufferRef buffer)
        {
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            CheckExists(buffer);
            return s_Shared.BufferInfoArray[buffer.Index].DebugName;
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// Sets the debug name for the given buffer.
        /// </summary>
        public static void SetDebugName(GraphicsBufferRef buffer, string name)
        {
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            CheckExists(buffer);
            s_Shared.BufferInfoArray[buffer.Index].DebugName = name;
            s_Shared.Buffers[buffer.Index].name = name;
#endif
        }

        /// <summary>
        /// Sets data into the given graphics buffer from a <see cref="NativeArray{T}"/>.
        /// </summary>
        public static unsafe void SetData(GraphicsBufferRef buffer, NativeArray<byte> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int elementSize)
        {
            CheckExists(buffer);
            s_Shared.Buffers[buffer.Index].SetData(data.GetUnsafeReadOnlyPtr(), nativeBufferStartIndex, graphicsBufferStartIndex, count, elementSize);
        }

        /// <summary>
        /// Sets data into the given graphics buffer from a raw pointer.
        /// </summary>
        public static unsafe void SetData(GraphicsBufferRef buffer, void* data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int elementSize)
        {
            CheckExists(buffer);
            s_Shared.Buffers[buffer.Index].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count, elementSize);
        }

        /// <summary>
        /// Sets data into the given graphics buffer from a managed <see cref="Array"/>.
        /// </summary>
        public static void SetData(GraphicsBufferRef buffer, Array data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count)
        {
            CheckExists(buffer);
            s_Shared.Buffers[buffer.Index].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        /// <summary>
        /// Resizes the given buffer to a new length, optionally copying contents.
        /// </summary>
        public static void Resize(GraphicsBufferRef buffer, int newLength, bool copyContents)
        {
            CheckExists(buffer);

            ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
            Assert.IsFalse(data.StoreType == GraphicsBufferStoreType.Pooled, "GraphicsBufferStore: Cannot resize pooled buffers.");

            ref var description = ref data.Descriptor;
            if (description.Length == newLength)
                return;

#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            string debugName = s_Shared.BufferInfoArray[buffer.Index].DebugName;
#else
            string debugName = string.Empty;
#endif
            if (copyContents)
            {
                int strideInBytes = description.Stride;
                int newSizeInBytes = strideInBytes * newLength;
                GraphicsBufferUtility.ResizeIfNeeded(ref s_Shared.Buffers[buffer.Index], strideInBytes, newSizeInBytes, description.Target, debugName);
            }
            else
            {
                var newBuffer = new GraphicsBuffer(description.Target, description.UsageFlags, newLength, description.Stride);
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
                if (!string.IsNullOrEmpty(debugName))
                    newBuffer.name = debugName;
#endif

                s_Shared.Buffers[buffer.Index].Dispose();
                s_Shared.Buffers[buffer.Index] = newBuffer;
            }

            description.Length = newLength;
        }

        /// <summary>
        /// Resizes the given buffer to a new length for a structure-of-arrays (SOA) layout, copying contents.
        /// </summary>
        public static void ResizeAndCopySOA(GraphicsBufferRef buffer, int newLength, int arrayCount)
        {
            CheckExists(buffer);

            ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
            Assert.IsFalse(data.StoreType == GraphicsBufferStoreType.Pooled, "GraphicsBufferStore: Cannot resize pooled buffers.");

            ref var description = ref data.Descriptor;
            if (description.Length == newLength)
                return;

#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            string debugName = s_Shared.BufferInfoArray[buffer.Index].DebugName;
#else
            string debugName = string.Empty;
#endif

            int strideInBytes = description.Stride;
            int newSizeInBytes = strideInBytes * newLength;
            GraphicsBufferUtility.ResizeSOAIfNeeded(ref s_Shared.Buffers[buffer.Index], strideInBytes, newSizeInBytes, arrayCount, description.Target, debugName);
            description.Length = newLength;
        }

        /// <summary>
        /// Locks a buffer range for CPU write access, returning a <see cref="NativeArray{T}"/> for direct access.
        /// </summary>
        public static NativeArray<T> LockBufferForWrite<T>(GraphicsBufferRef buffer, int index, int count)
            where T : struct
        {
            CheckExists(buffer);

            ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
            if (data.StoreType == GraphicsBufferStoreType.Pooled)
                data.LockForWrite();

            return s_Shared.Buffers[buffer.Index].LockBufferForWrite<T>(index, count);
        }

        /// <summary>
        /// Unlocks a previously-locked buffer range, indicating how many elements were written.
        /// </summary>
        public static void UnlockBufferAfterWrite<T>(GraphicsBufferRef buffer, int countWritten) where T : unmanaged
        {
            CheckExists(buffer);
            UnlockBufferAfterWriteInternal(buffer.Index, countWritten * UnsafeUtility.SizeOf<T>(), 1);
        }

        /// <summary>
        /// Unlocks a previously-locked buffer range, indicating how many bytes were written.
        /// </summary>
        public static void UnlockBufferAfterWrite(GraphicsBufferRef buffer, int bytesWritten)
        {
            CheckExists(buffer);
            UnlockBufferAfterWriteInternal(buffer.Index, bytesWritten, 1);
        }

        /// <summary>
        /// Locks the given buffer to prevent it from being reused. Has no effect on non-pooled buffers.
        /// </summary>
        public static void LockBuffer(GraphicsBufferRef buffer)
        {
            CheckExists(buffer);
            ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
            if (data.StoreType == GraphicsBufferStoreType.Pooled)
                data.Lock();
        }

        /// <summary>
        /// Unlocks the given buffer to allow it to be reused. Has no effect on non-pooled buffers.
        /// </summary>
        public static void UnlockBuffer(GraphicsBufferRef buffer)
        {
            CheckExists(buffer);
            ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
            if (data.StoreType == GraphicsBufferStoreType.Pooled)
                data.Unlock();
        }

        private static void UnlockBufferAfterWriteInternal(int bufferIndex, int countWritten, int elemSize)
        {
            var buffer = s_Shared.Buffers[bufferIndex];
            buffer.UnlockBufferAfterWrite<byte>(countWritten * elemSize);

            ref var data = ref s_Shared.BufferInfoArray[bufferIndex];
            if (data.StoreType == GraphicsBufferStoreType.Pooled)
                data.UnlockForWrite();
        }

        /// <summary>
        /// Requests an async readback of the given buffer range.
        /// </summary>
        public static AsyncGPUReadbackRequest RequestDataAsync(GraphicsBufferRef buffer, long offsetInBytes, long sizeInBytes)
        {
            CheckExists(buffer);
            Assert.IsTrue(offsetInBytes <= int.MaxValue, "GraphicsBufferStore: Async readback offset cannot exceed " + int.MaxValue);
            Assert.IsTrue(sizeInBytes <= int.MaxValue, "GraphicsBufferStore: Async readback size cannot exceed " + int.MaxValue);

            return AsyncGPUReadback.Request(s_Shared.Buffers[buffer.Index], (int)sizeInBytes, (int)offsetInBytes);
        }

        /// <summary>
        /// Requests an async readback of the given buffer into a <see cref="NativeArray{T}"/>.
        /// </summary>
        public static AsyncGPUReadbackRequest RequestDataAsync(GraphicsBufferRef buffer, NativeArray<byte> data)
        {
            CheckExists(buffer);
            return AsyncGPUReadback.RequestIntoNativeArray(ref data, s_Shared.Buffers[buffer.Index], data.Length, 0);
        }
        #endregion

        #region Pooled Buffers

        /// <summary>
        /// Requests a pooled buffer matching <paramref name="descriptor"/>.
        /// If one exists (and is free), reuses it; otherwise creates a new one.
        /// </summary>
        public static GraphicsBufferRef Request(GraphicsBufferDescriptor descriptor, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
        {
            var alignedDescriptor = AlignDescriptor(descriptor, alignment);
            var descriptorHash = alignedDescriptor.GetHashCode();

            if (!TryFindPooledBuffer(descriptorHash, alignedDescriptor, out var buffer))
            {
                buffer = CreateInternal(alignedDescriptor, name, GraphicsBufferStoreType.Pooled);
            }
            else
            {
                ref var data = ref s_Shared.BufferInfoArray[buffer.Index];
                data.LastUsedFrame = s_Shared.FrameIndex;
            }

            return buffer;
        }

        /// <summary>
        /// Requests a pooled buffer with the specified target and element type <typeparamref name="T"/>.
        /// </summary>
        public static GraphicsBufferRef Request<T>(GraphicsBuffer.Target target, int count, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var descriptor = new GraphicsBufferDescriptor(target, GraphicsBuffer.UsageFlags.None, UnsafeUtility.SizeOf<T>(), count);
            return Request(descriptor, name, alignment);
        }

        #endregion

        #region Pooled Structured Buffers

        /// <summary>
        /// Requests a pooled structured buffer for type <typeparamref name="T"/>, with the specified element count.
        /// </summary>
        public static GraphicsBufferRef RequestStructured<T>(int count, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var descriptor = new GraphicsBufferDescriptor(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, UnsafeUtility.SizeOf<T>(), count);
            return Request(descriptor, name, alignment);
        }

        /// <summary>
        /// Requests a pooled structured buffer and immediately sets data from <paramref name="data"/>.
        /// </summary>
        public static GraphicsBufferRef RequestStructured<T>(T[] data, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var buffer = RequestStructured<T>(data.Length, name, alignment);
            buffer.SetData(data);
            return buffer;
        }

        /// <summary>
        /// Requests a pooled structured buffer and immediately sets data from <paramref name="data"/>.
        /// </summary>
        public static GraphicsBufferRef RequestStructured<T>(NativeArray<T> data, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var buffer = RequestStructured<T>(data.Length, name, alignment);
            buffer.SetData(data);
            return buffer;
        }

        /// <summary>
        /// Requests a pooled structured buffer, then enqueues a command buffer set-data operation.
        /// </summary>
        public static GraphicsBufferRef RequestStructured<T>(CommandBuffer cmd, NativeArray<T> data, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var buffer = RequestStructured<T>(data.Length, name, alignment);
            cmd.SetBufferData(buffer, data);
            return buffer;
        }

        #endregion

        #region Pooled Raw Buffers

        /// <summary>
        /// Requests a pooled raw buffer with explicit stride.
        /// </summary>
        public static GraphicsBufferRef RequestRaw(int count, int stride, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
        {
            var descriptor = new GraphicsBufferDescriptor(GraphicsBuffer.Target.Raw, GraphicsBuffer.UsageFlags.None, stride, count);
            return Request(descriptor, name, alignment);
        }

        /// <summary>
        /// Requests a pooled raw buffer with a default 4-byte stride.
        /// </summary>
        public static GraphicsBufferRef RequestRaw(int count, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
        {
            return RequestRaw(count, 4, name, alignment);
        }

        /// <summary>
        /// Requests a pooled raw buffer for type <typeparamref name="T"/>, matching its size.
        /// </summary>
        public static GraphicsBufferRef RequestRaw<T>(int count, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            return RequestRaw(count, UnsafeUtility.SizeOf<T>(), name, alignment);
        }

        /// <summary>
        /// Requests a pooled raw buffer, then immediately sets data from <paramref name="data"/>.
        /// </summary>
        public static GraphicsBufferRef RequestRaw<T>(NativeArray<T> data, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var buffer = RequestRaw(data.Length, UnsafeUtility.SizeOf<T>(), name, alignment);
            buffer.SetData(data);
            return buffer;
        }

        /// <summary>
        /// Requests a pooled raw buffer, then enqueues a command buffer set-data operation.
        /// </summary>
        public static GraphicsBufferRef RequestRaw<T>(CommandBuffer cmd, NativeArray<T> data, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.PowerOfTwo)
            where T : unmanaged
        {
            var buffer = RequestRaw(data.Length, UnsafeUtility.SizeOf<T>(), name, alignment);
            cmd.SetBufferData(buffer, data);
            return buffer;
        }

        #endregion

        #region Pooled Indirect Buffers

        /// <summary>
        /// Requests a pooled indirect arguments buffer with a specified count and stride.
        /// </summary>
        public static GraphicsBufferRef RequestIndirect(int count, int stride, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.None)
        {
            var descriptor = new GraphicsBufferDescriptor(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.None, stride, count);
            return Request(descriptor, name, alignment);
        }

        /// <summary>
        /// Requests a pooled indirect arguments buffer with a default 4-byte stride.
        /// </summary>
        public static GraphicsBufferRef RequestIndirect(int count, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.None)
        {
            return RequestIndirect(count, 4, name, alignment);
        }

        /// <summary>
        /// Requests a pooled indirect arguments buffer, then sets data from <paramref name="args"/>.
        /// </summary>
        public static GraphicsBufferRef RequestIndirect<T>(NativeArray<T> args, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.None) where T : unmanaged
        {
            var buffer = RequestIndirect(args.Length, UnsafeUtility.SizeOf<T>(), name, alignment);
            buffer.SetData(args);
            return buffer;
        }

        /// <summary>
        /// Requests a pooled indirect arguments buffer, then enqueues a command buffer set-data operation.
        /// </summary>
        public static GraphicsBufferRef RequestIndirect<T>(CommandBuffer cmd, NativeArray<T> args, string name = null, GraphicsBufferAlignment alignment = GraphicsBufferAlignment.None) where T : unmanaged
        {
            var buffer = RequestIndirect(args.Length, UnsafeUtility.SizeOf<T>(), name, alignment);
            cmd.SetBufferData(buffer, args);
            return buffer;
        }

        #endregion

        #region Internal Data

        /// <summary>
        /// Describes all relevant metadata for an unmanaged graphics buffer entry in the store.
        /// </summary>
        private struct GraphicsBufferInfo
        {
            public bool IsCreated;
            public int Hash;
            public int IndexInPool;
            public int IndexInAllocated;
            public GraphicsBufferStoreType StoreType;
            public GraphicsBufferDescriptor Descriptor;
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            public string DebugName;
#endif
            public int LastUsedFrame;
            public int FenceFrame;

            public void Lock()
            {
                FenceFrame = -2;
            }

            public void Unlock()
            {
                FenceFrame = -1;
            }

            public void LockForWrite()
            {
                FenceFrame = -2;
                LastUsedFrame = s_Shared.FrameIndex;
            }

            public void UnlockForWrite()
            {
                LastUsedFrame = s_Shared.FrameIndex;
                FenceFrame = s_Shared.FrameIndex;
            }

            public bool IsLocked => FenceFrame == -2;

            public bool InFlight
            {
                get
                {
                    int frameIndex = s_Shared.FrameIndex;
                    if (LastUsedFrame >= frameIndex)
                        return true; // used this frame

                    if (FenceFrame == -1)
                        return false; // never mapped, so no fence

                    // If the buffer was mapped, wait until after X frames in flight.
                    return frameIndex - FenceFrame < CullingUtility.NumFramesInFlight + 1;
                }
            }
        }

        private struct SharedData
        {
            public bool IsInitialized;
            public int FrameIndex;
            public List<int> FreeIndices;
            public List<int> PooledBufferIndices;
            public List<int> AllocatedBufferIndices;
            public GraphicsBufferInfo[] BufferInfoArray;
            public GraphicsBuffer[] Buffers;
            public int NextBufferIndex;
        }

        private static SharedData s_Shared = new SharedData
        {
            IsInitialized = false,
            FrameIndex = 0,
            FreeIndices = new List<int>(),
            PooledBufferIndices = new List<int>(),
            AllocatedBufferIndices = new List<int>(),
            BufferInfoArray = new GraphicsBufferInfo[1], // index 0 is always "null"
            Buffers = new GraphicsBuffer[1], // index 0 is always "null"
            NextBufferIndex = 1
        };

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            if (s_Shared.IsInitialized)
                return;

            s_Shared.IsInitialized = true;
            s_Shared.FrameIndex = 0;
            s_Shared.FreeIndices.Clear();
            s_Shared.PooledBufferIndices.Clear();
            s_Shared.AllocatedBufferIndices.Clear();
            s_Shared.BufferInfoArray = new GraphicsBufferInfo[1]; // index 0 is always
            s_Shared.Buffers = new GraphicsBuffer[1]; // index 0 is always
            s_Shared.NextBufferIndex = 1;

            void Shutdown()
            {
                if (s_Shared.IsInitialized)
                {
                    ReleaseAll();
                    s_Shared.IsInitialized = false;
                }
            }

            AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        }

        #endregion

        #region Internal Management

        private static int AllocateBufferIndex()
        {
            int newBufferIndex;
            if (s_Shared.FreeIndices.Count > 0)
            {
                newBufferIndex = s_Shared.FreeIndices[^1];
                s_Shared.FreeIndices.RemoveAtSwapBack(s_Shared.FreeIndices.Count - 1);
            }
            else
            {
                newBufferIndex = s_Shared.NextBufferIndex++;
                if (newBufferIndex >= s_Shared.Buffers.Length)
                {
                    int newSize = Math.Max(s_Shared.Buffers.Length * 2, 8);
                    Array.Resize(ref s_Shared.Buffers, newSize);
                    Array.Resize(ref s_Shared.BufferInfoArray, newSize);
                }
            }

            return newBufferIndex;
        }

        private static bool TryFindPooledBuffer(int descriptorHash, in GraphicsBufferDescriptor descriptor, out GraphicsBufferRef pooledBuffer)
        {
            pooledBuffer = GraphicsBufferRef.Null;

            foreach (var pooledBufferIndex in s_Shared.PooledBufferIndices)
            {
                var nextBuffer = s_Shared.BufferInfoArray[pooledBufferIndex];
                if (nextBuffer.Hash != descriptorHash)
                    continue;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (nextBuffer.Descriptor != descriptor)
                    throw new InvalidOperationException("GraphicsBufferStore: Pooled buffer descriptor mismatch.");
#endif
                if (nextBuffer.InFlight || nextBuffer.IsLocked)
                    continue;

                pooledBuffer = new GraphicsBufferRef { Index = pooledBufferIndex };
                return true;
            }

            return false;
        }

        private static GraphicsBufferDescriptor AlignDescriptor(GraphicsBufferDescriptor descriptor, GraphicsBufferAlignment alignment)
        {
            const int bufferPageSize = 64 * 1024;

            var alignedDescriptor = descriptor;
            long alignedBytes = alignment switch
            {
                GraphicsBufferAlignment.PowerOfTwo => math.ceilpow2(descriptor.SizeInBytes),
                GraphicsBufferAlignment.Page       => MathUtility.NextMultipleOf(descriptor.SizeInBytes, bufferPageSize),
                _                                  => alignedDescriptor.SizeInBytes
            };
            alignedDescriptor.Length = (int)((alignedBytes + descriptor.Stride - 1) / descriptor.Stride);

            return alignedDescriptor;
        }

        private static GraphicsBufferRef CreateInternal(GraphicsBufferDescriptor descriptor, string debugName, GraphicsBufferStoreType type)
        {
            var bufferIndex = AllocateBufferIndex();
            var buffer = new GraphicsBuffer(descriptor.Target, descriptor.UsageFlags, descriptor.Length, descriptor.Stride);
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
            if (!string.IsNullOrEmpty(debugName))
                buffer.name = debugName;
#endif

            s_Shared.Buffers[bufferIndex] = buffer;

            var bufferData = new GraphicsBufferInfo
            {
                IsCreated        = true,
                Hash             = descriptor.GetHashCode(),
                IndexInPool      = -1,
                IndexInAllocated = -1,
                StoreType        = type,
                Descriptor       = descriptor,
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
                DebugName        = debugName,
#endif
                LastUsedFrame    = s_Shared.FrameIndex,
                FenceFrame       = -1
            };

            if (type == GraphicsBufferStoreType.Pooled)
            {
                bufferData.IndexInPool = s_Shared.PooledBufferIndices.Count;
                s_Shared.PooledBufferIndices.Add(bufferIndex);
            }

            bufferData.IndexInAllocated = s_Shared.AllocatedBufferIndices.Count;
            s_Shared.AllocatedBufferIndices.Add(bufferIndex);
            s_Shared.BufferInfoArray[bufferIndex] = bufferData;

            return new GraphicsBufferRef { Index = bufferIndex };
        }

        private static void ReleaseBuffer(int bufferIndex)
        {
            ref var data = ref s_Shared.BufferInfoArray[bufferIndex];
            if (data.StoreType == GraphicsBufferStoreType.Pooled)
            {
                // Mark it free for reuse
                data.Unlock();
            }
            else
            {
                // For store/transient, just destroy it immediately.
                DestroyBufferImmediate(bufferIndex);
            }
        }

        private static void DestroyBufferImmediate(int bufferIndex)
        {
            var data = s_Shared.BufferInfoArray[bufferIndex];

            var buffer = s_Shared.Buffers[bufferIndex];
            if (data.StoreType != GraphicsBufferStoreType.Transient)
                buffer.Dispose();

            s_Shared.Buffers[bufferIndex] = null;

            if (data.StoreType == GraphicsBufferStoreType.Pooled)
            {
                // Remove from pooled list
                int indexInPool = data.IndexInPool;
                s_Shared.PooledBufferIndices.RemoveAtSwapBack(indexInPool);
                if (indexInPool < s_Shared.PooledBufferIndices.Count)
                {
                    int movedIndex = s_Shared.PooledBufferIndices[indexInPool];
                    s_Shared.BufferInfoArray[movedIndex].IndexInPool = indexInPool;
                }
            }

            // Remove from allocated list
            int indexInAllocated = data.IndexInAllocated;
            s_Shared.AllocatedBufferIndices.RemoveAtSwapBack(indexInAllocated);
            if (indexInAllocated < s_Shared.AllocatedBufferIndices.Count)
            {
                int movedIndex = s_Shared.AllocatedBufferIndices[indexInAllocated];
                s_Shared.BufferInfoArray[movedIndex].IndexInAllocated = indexInAllocated;
            }

            s_Shared.BufferInfoArray[bufferIndex] = default;
            s_Shared.FreeIndices.Add(bufferIndex);
        }

        #endregion

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckExists(GraphicsBufferRef buffer)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!Exists(buffer))
                throw new InvalidOperationException("GraphicsBufferStore: Invalid buffer handle " + buffer);
#endif
        }
    }
}
