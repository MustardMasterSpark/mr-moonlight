using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct PersistentGraphicsBufferRef<T> : IDisposable where T : unmanaged
    {
        private GraphicsBufferRef m_Buffer;
        private NativeArray<T> m_Data;

        public PersistentGraphicsBufferRef(int count, string name = null, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Assert.IsTrue(count > 0, "Count must be greater than zero.");
            Assert.IsTrue(UnsafeUtility.SizeOf<T>() >= 4, "Size of T must be >= 4 bytes.");
            Assert.IsTrue(UnsafeUtility.SizeOf<T>() % 4 == 0, "Size of T must be a multiple of 4 bytes.");

            m_Data = new NativeArray<T>(count, Allocator.Persistent, options);
            m_Buffer = new GraphicsBufferRef(target, count, UnsafeUtility.SizeOf<T>(), name);
            if (options == NativeArrayOptions.ClearMemory)
                m_Buffer.SetData(m_Data);
        }

        public void Dispose()
        {
            m_Buffer.Dispose();
            m_Data.Dispose();
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data.IsCreated;
        }

        public int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnsafeUtility.SizeOf<T>();
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data.Length;
        }

        public long SizeInBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data.Length * UnsafeUtility.SizeOf<T>();
        }

        public GraphicsBufferRef Buffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Buffer;
        }

        public NativeArray<T> Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => m_Data[index] = value;
        }

        public void Resize(int newLength)
        {
            if (newLength == m_Data.Length) return;
            CheckCreated();
            CheckLength(newLength);
            m_Data.ResizeArraySafe(newLength);
            m_Buffer.ResizeAndCopyContents(newLength);
        }

        public void ResizeIfNeeded(int requiredLength, GraphicsBufferGrowPolicy growPolicy = GraphicsBufferGrowPolicy.Exact, GraphicsBufferTrimPolicy trimPolicy = GraphicsBufferTrimPolicy.None)
        {
            CheckCreated();
            int newLength = GraphicsBufferRef.CalculateNewSize(m_Data.Length, requiredLength, growPolicy, trimPolicy);
            if (newLength == m_Data.Length) return;
            m_Data.ResizeArraySafe(newLength);
            m_Buffer.ResizeAndCopyContents(newLength);
        }

        public void UpdateBufferData()
        {
            CheckCreated();
            m_Buffer.SetData(m_Data);
        }

        public void UpdateBufferData(CommandBuffer cmd)
        {
            CheckCreated();
            cmd.SetBufferData(m_Buffer, m_Data);
        }

        public void UpdateBufferRange(int startIndex, int count)
        {
            CheckCreated();
            CheckRange(startIndex, count);
            m_Buffer.SetData(m_Data, startIndex, startIndex, count);
        }

        public void UpdateBufferRange(CommandBuffer cmd, int startIndex, int count)
        {
            CheckCreated();
            CheckRange(startIndex, count);
            cmd.SetBufferData(m_Buffer, m_Data, startIndex, startIndex, count);
        }

        public static implicit operator GraphicsBufferRef(PersistentGraphicsBufferRef<T> graphicsBuffer)
        {
            return graphicsBuffer.Buffer;
        }

        public static implicit operator GraphicsBuffer(PersistentGraphicsBufferRef<T> graphicsBuffer)
        {
            return graphicsBuffer.Buffer.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckCreated()
        {
            if (!IsCreated)
                throw new InvalidOperationException("PersistentGraphicsBufferRef is not created.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckLength(int length)
        {
            if (length == 0)
                throw new InvalidOperationException("PersistentGraphicsBufferRef length is zero.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private void CheckRange(int startIndex, int count)
        {
            if ((uint)startIndex >= (uint)Length || count < 0 || startIndex + count > Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), $"Range {startIndex} to {startIndex + count} is out of range of '{Length}' Length.");
        }
    }
}
