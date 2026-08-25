// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Defines policies for growing a graphics buffer when doing a grow operation.
    /// </summary>
    public enum GraphicsBufferGrowPolicy
    {
        /// <summary>
        /// Resize the buffer to exactly the required length.
        /// </summary>
        Exact,
        /// <summary>
        /// Resize the buffer to the required length plus some extra slack (25%).
        /// </summary>
        WithSlack,
        /// <summary>
        /// Double the current buffer size, or use the required length if larger.
        /// </summary>
        Double,
        /// <summary>
        /// Resize the buffer to the next power of two greater than or equal to the required length.
        /// </summary>
        NextPowerOfTwo
    }

    /// <summary>
    /// Defines policies for trimming a graphics buffer when doing a trim operation.
    /// </summary>
    public enum GraphicsBufferTrimPolicy
    {
        /// <summary>
        /// Don't trim the buffer size.
        /// </summary>
        None,
        /// <summary>
        /// When the buffer is less than half full, reduce its size to half.
        /// </summary>
        Half,
        /// <summary>
        /// When the buffer is less than a quarter full, reduce its size to a quarter.
        /// </summary>
        Quarter
    }

    /// <summary>
    /// Holds a reference to a <see cref="GraphicsBuffer"/> along with metadata describing its layout and usage.
    /// </summary>
    [DebuggerDisplay("{Index} ({Descriptor})")]
    internal struct GraphicsBufferRef : IDisposable, IEquatable<GraphicsBufferRef>, IComparable<GraphicsBufferRef>
    {
        /// <summary>
        /// A null or invalid reference to a graphics buffer.
        /// </summary>
        public static readonly GraphicsBufferRef Null = default;

        /// <summary>
        /// Internal index for tracking the associated <see cref="GraphicsBuffer"/>.
        /// </summary>
        public int Index;

        /// <summary>
        /// Indicates if the reference has been assigned a valid index.
        /// </summary>
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index > 0;
        }

        /// <summary>
        /// Indicates if the reference is still valid (i.e., exists within <see cref="GraphicsBufferStore"/>).
        /// </summary>
        public readonly bool Exists
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsCreated && GraphicsBufferStore.Exists(this);
        }

        /// <summary>
        /// The descriptor that defines the layout and characteristics of this <see cref="GraphicsBuffer"/>.
        /// </summary>
        public readonly GraphicsBufferDescriptor Descriptor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsCreated ? GraphicsBufferStore.GetDescriptor(this) : default;
        }

        /// <summary>
        /// The target type of the underlying <see cref="GraphicsBuffer"/> (e.g. Vertex, Index, Constant).
        /// </summary>
        public readonly GraphicsBuffer.Target Target
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.Target;
        }

        /// <summary>
        /// The usage flags assigned to the underlying <see cref="GraphicsBuffer"/>.
        /// </summary>
        public readonly GraphicsBuffer.UsageFlags UsageFlags
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.UsageFlags;
        }

        /// <summary>
        /// The size (in bytes) of each element in the buffer.
        /// </summary>
        public readonly int Stride
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.Stride;
        }

        /// <summary>
        /// The total number of elements in the buffer.
        /// </summary>
        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.Length;
        }

        /// <summary>
        /// The total size of the buffer in bytes.
        /// </summary>
        public readonly long SizeInBytes
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Descriptor.SizeInBytes;
        }

        /// <summary>
        /// Retrieves the internal handle to the buffer stored in <see cref="GraphicsBufferStore"/>.
        /// </summary>
        public readonly GraphicsBufferHandle BufferHandle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GraphicsBufferStore.GetBufferHandle(this);
        }

        /// <summary>
        /// Gets the actual <see cref="GraphicsBuffer"/> object.
        /// </summary>
        public readonly GraphicsBuffer Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GraphicsBufferStore.GetBufferUnchecked(this);
        }

        /// <summary>
        /// A human-readable or debug name assigned to this buffer.
        /// </summary>
        public readonly string DebugName
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GraphicsBufferStore.GetDebugName(this);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => GraphicsBufferStore.SetDebugName(this, value);
        }

        /// <summary>
        /// Creates a new graphics buffer reference with no usage flags specified.
        /// </summary>
        /// <param name="target">The target type (e.g. Vertex, Index, Constant).</param>
        /// <param name="length">The number of elements in the buffer.</param>
        /// <param name="stride">The size of each element in bytes.</param>
        /// <param name="name">A human-readable label for debugging.</param>
        public GraphicsBufferRef(GraphicsBuffer.Target target, int length, int stride, string name = null)
        {
            this = GraphicsBufferStore.Create(target, GraphicsBuffer.UsageFlags.None, length, stride, name);
        }

        /// <summary>
        /// Creates a new graphics buffer reference with no usage flags specified.
        /// </summary>
        /// <param name="target">The target type (e.g. Vertex, Index, Constant).</param>
        /// <param name="length">The number of elements in the buffer.</param>
        /// <param name="stride">The size of each element in bytes.</param>
        /// <param name="name">A human-readable label for debugging.</param>
        public GraphicsBufferRef(GraphicsBuffer.Target target, int length, int stride, in FixedString64Bytes name)
        {
            this = GraphicsBufferStore.Create(target, GraphicsBuffer.UsageFlags.None, length, stride, name.ToString());
        }

        /// <summary>
        /// Creates a new graphics buffer reference with the specified usage flags.
        /// </summary>
        /// <param name="target">The target type (e.g. Vertex, Index, Constant).</param>
        /// <param name="usageFlags">Defines how the buffer can be used (e.g. for compute, indirect drawing).</param>
        /// <param name="length">The number of elements in the buffer.</param>
        /// <param name="stride">The size of each element in bytes.</param>
        /// <param name="name">A human-readable label for debugging.</param>
        public GraphicsBufferRef(GraphicsBuffer.Target target, GraphicsBuffer.UsageFlags usageFlags, int length, int stride, string name = null)
        {
            this = GraphicsBufferStore.Create(target, usageFlags, length, stride, name);
        }

        /// <summary>
        /// Creates a new graphics buffer reference based on a <see cref="GraphicsBufferDescriptor"/>.
        /// </summary>
        /// <param name="descriptor">Defines layout and usage properties for the graphics buffer.</param>
        /// <param name="name">A human-readable label for debugging.</param>
        public GraphicsBufferRef(GraphicsBufferDescriptor descriptor, string name = null)
        {
            this = GraphicsBufferStore.Create(descriptor.Target, descriptor.UsageFlags, descriptor.Length, descriptor.Stride, name);
        }

        /// <summary>
        /// Disposes of the underlying graphics buffer and sets this reference to null.
        /// </summary>
        public void Dispose()
        {
            if (IsCreated)
                GraphicsBufferStore.Release(this);

            Index = 0;
        }

        /// <summary>
        /// Checks if this buffer reference exists and the underlying buffer is valid.
        /// </summary>
        /// <returns><c>true</c> if the buffer is valid; otherwise, <c>false</c>.</returns>
        public readonly bool IsValid()
        {
            return IsCreated && GraphicsBufferStore.IsValid(this);
        }

        /// <summary>
        /// Returns a string representation of this buffer reference.
        /// </summary>
        public override string ToString()
        {
            return IsCreated ? $"GraphicsBufferRef({Index})" : "GraphicsBufferRef(Null)";
        }

        /// <summary>
        /// Resizes the buffer, discarding existing data in the process.
        /// </summary>
        /// <param name="length">The new number of elements in the buffer.</param>
        public readonly void ResizeAndDiscardContents(int length)
        {
            GraphicsBufferStore.Resize(this, length, copyContents: false);
        }

        /// <summary>
        /// Resizes the buffer while preserving its existing data.
        /// </summary>
        /// <param name="length">The new number of elements in the buffer.</param>
        public readonly void ResizeAndCopyContents(int length)
        {
            GraphicsBufferStore.Resize(this, length, copyContents: true);
        }

        /// <summary>
        /// Resizes the buffer while preserving its existing data.
        /// </summary>
        /// <param name="arrayCount">The number of arrays in the structure-of-arrays layout.</param>
        /// <param name="length">The new number of elements in the buffer.</param>
        public readonly void ResizeAndCopyContentsSOA(int arrayCount, int length)
        {
            GraphicsBufferStore.ResizeAndCopySOA(this, length, arrayCount);
        }

        /// <summary>
        /// Grows the buffer if the required length exceeds the current length, using the specified growth policy.
        /// </summary>
        /// <param name="requiredLength">The minimum required length of the buffer.</param>
        /// <param name="growPolicy">The policy to use when growing the buffer.</param>
        /// <param name="keepContents">Whether to preserve existing data when resizing.</param>
        public readonly void GrowIfNeeded(int requiredLength, GraphicsBufferGrowPolicy growPolicy = GraphicsBufferGrowPolicy.Exact, bool keepContents = true)
        {
            if (requiredLength <= Length)
                return;

            int newLength = CalculateNewGrowthSize(Length, requiredLength, growPolicy);
            GraphicsBufferStore.Resize(this, newLength, copyContents: keepContents);
        }

        /// <summary>
        /// Trims the buffer if the required length is significantly smaller than the current length, based on the specified trim policy.
        /// </summary>
        /// <param name="requiredLength">The minimum required length of the buffer.</param>
        /// <param name="trimPolicy">The policy to use when trimming the buffer.</param>
        /// <param name="keepContents">Whether to preserve existing data when resizing.</param>
        public readonly void TrimIfNeeded(int requiredLength, GraphicsBufferTrimPolicy trimPolicy = GraphicsBufferTrimPolicy.Half, bool keepContents = true)
        {
            int newLength = CalculateNewTrimSize(Length, requiredLength, trimPolicy);
            if (newLength != Length)
                GraphicsBufferStore.Resize(this, newLength, copyContents: keepContents);
        }

        /// <summary>
        /// Resizes the buffer based on the required length, applying growth or trimming policies as needed.
        /// </summary>
        /// <param name="requiredLength">The minimum required length of the buffer.</param>
        /// <param name="growPolicy">The policy to use when growing the buffer.</param>
        /// <param name="trimPolicy">The policy to use when trimming the buffer.</param>
        /// <param name="keepContents">Whether to preserve existing data when resizing.</param>
        public readonly void ResizeIfNeeded(int requiredLength,
            GraphicsBufferGrowPolicy growPolicy = GraphicsBufferGrowPolicy.Exact,
            GraphicsBufferTrimPolicy trimPolicy = GraphicsBufferTrimPolicy.Half,
            bool keepContents = true)
        {
            int newLength = CalculateNewSize(Length, requiredLength, growPolicy, trimPolicy);
            if (newLength != Length)
                GraphicsBufferStore.Resize(this, newLength, copyContents: keepContents);
        }

        /// <summary>
        /// Calculates the new size for a buffer growth operation based on the specified policy.
        /// </summary>
        /// <param name="currentLength">The current length of the buffer.</param>
        /// <param name="requiredLength">The required length of the buffer.</param>
        /// <param name="growPolicy">The growth policy to apply.</param>
        /// <returns>The calculated new length for the buffer.</returns>
        public static int CalculateNewGrowthSize(int currentLength, int requiredLength, GraphicsBufferGrowPolicy growPolicy)
        {
            if (requiredLength <= currentLength)
                return currentLength;

            return growPolicy switch
            {
                GraphicsBufferGrowPolicy.WithSlack      => requiredLength + (requiredLength >> 2),
                GraphicsBufferGrowPolicy.Double         => Math.Max(requiredLength, currentLength * 2),
                GraphicsBufferGrowPolicy.NextPowerOfTwo => Mathf.NextPowerOfTwo(requiredLength),
                _                                       => requiredLength
            };
        }
        /// <summary>
        /// Calculates the new size for a buffer trim operation based on the specified policy.
        /// </summary>
        /// <param name="currentLength">The current length of the buffer.</param>
        /// <param name="requiredLength">The required length of the buffer.</param>
        /// <param name="trimPolicy">The trim policy to apply.</param>
        /// <returns>The calculated new length for the buffer.</returns>
        public static int CalculateNewTrimSize(int currentLength, int requiredLength, GraphicsBufferTrimPolicy trimPolicy)
        {
            int newLength = currentLength;

            switch (trimPolicy)
            {
                case GraphicsBufferTrimPolicy.Half:
                    if (requiredLength < (currentLength >> 1))
                        newLength = currentLength >> 1;
                    break;
                case GraphicsBufferTrimPolicy.Quarter:
                    if (requiredLength < (currentLength >> 2))
                        newLength = currentLength >> 2;
                    break;
                case GraphicsBufferTrimPolicy.None:
                default:
                    break;
            }

            return math.max(1, newLength);
        }

        /// <summary>
        /// Calculates the new size for a buffer based on growth or trimming policies.
        /// </summary>
        /// <param name="currentLength">The current length of the buffer.</param>
        /// <param name="requiredLength">The required length of the buffer.</param>
        /// <param name="growPolicy">The growth policy to apply if growing.</param>
        /// <param name="trimPolicy">The trim policy to apply if trimming.</param>
        /// <returns>The calculated new length for the buffer.</returns>
        public static int CalculateNewSize(int currentLength, int requiredLength, GraphicsBufferGrowPolicy growPolicy, GraphicsBufferTrimPolicy trimPolicy)
        {
            if (requiredLength > currentLength)
            {
                return CalculateNewGrowthSize(currentLength, requiredLength, growPolicy);
            }
            else
            {
                return CalculateNewTrimSize(currentLength, requiredLength, trimPolicy);
            }
        }

        /// <summary>
        /// Locks a pooled buffer for writing, ensuring the pool does not recycle it until unlocked.
        /// </summary>
        public readonly void Lock()
        {
            GraphicsBufferStore.LockBuffer(this);
        }

        /// <summary>
        /// Unlocks a previously locked pooled buffer, allowing the pool to recycle it.
        /// </summary>
        public readonly void Unlock()
        {
            if (IsCreated)
                GraphicsBufferStore.UnlockBuffer(this);
        }

        /// <summary>
        /// Locks a portion of the underlying buffer for writing and returns it as a <see cref="NativeArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored by the buffer.</typeparam>
        /// <param name="startIndex">Offset (in elements) from the beginning of the buffer.</param>
        /// <param name="length">Number of elements to lock; if negative, locks until the end of the buffer.</param>
        /// <returns>A <see cref="NativeArray{T}"/> referencing the locked data.</returns>
        public readonly NativeArray<T> LockForWrite<T>(int startIndex = 0, int length = -1) where T : unmanaged
        {
            if (length < 0)
                length = Length - startIndex;

            return GraphicsBufferStore.LockBufferForWrite<T>(this, startIndex, length);
        }

        /// <summary>
        /// Unlocks the portion of the buffer previously locked for writing, specifying how many elements were written.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored by the buffer.</typeparam>
        /// <param name="countWritten">The number of elements that were actually written.</param>
        public readonly void UnlockAfterWrite<T>(int countWritten) where T : unmanaged
        {
            GraphicsBufferStore.UnlockBufferAfterWrite<T>(this, countWritten);
        }

        /// <summary>
        /// Unlocks the portion of the buffer previously locked for writing, specifying how many bytes were written.
        /// </summary>
        /// <param name="bytesWritten">The number of bytes that were actually written.</param>
        public readonly void UnlockBytesAfterWrite(int bytesWritten)
        {
            GraphicsBufferStore.UnlockBufferAfterWrite(this, bytesWritten);
        }

        /// <summary>
        /// Sets the entire contents of this buffer from a <see cref="NativeArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored in the data array.</typeparam>
        /// <param name="data">The data to copy from. Must not exceed the buffer size.</param>
        public readonly void SetData<T>(NativeArray<T> data) where T : unmanaged
        {
            if (data.Length == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(data.Length * UnsafeUtility.SizeOf<T>() > SizeInBytes))
                throw new ArgumentException("Data size exceeds buffer size.");
#endif

            GraphicsBufferStore.SetData(this, data.Reinterpret<byte>(UnsafeUtility.SizeOf<T>()), 0, 0, data.Length, UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Sets a range of data in this buffer from a <see cref="NativeArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored in the data array.</typeparam>
        /// <param name="data">The data source array.</param>
        /// <param name="nativeBufferStartIndex">Start index in the native array.</param>
        /// <param name="graphicsBufferStartIndex">Start index in this buffer (in elements).</param>
        /// <param name="count">Number of elements to copy.</param>
        public readonly void SetData<T>(NativeArray<T> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count) where T : unmanaged
        {
            if (data.Length == 0 || count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(nativeBufferStartIndex + count > data.Length))
                throw new ArgumentException("Invalid native buffer range.");
            if (Hint.Unlikely(graphicsBufferStartIndex * UnsafeUtility.SizeOf<T>() + count * UnsafeUtility.SizeOf<T>() > SizeInBytes))
                throw new ArgumentException("Invalid graphics buffer range.");
#endif

            GraphicsBufferStore.SetData(this, data.Reinterpret<byte>(UnsafeUtility.SizeOf<T>()), nativeBufferStartIndex, graphicsBufferStartIndex, count, UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Sets data in this buffer from a pointer to unmanaged memory.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored in the memory block.</typeparam>
        /// <param name="data">Pointer to the source data.</param>
        /// <param name="count">Number of elements to copy.</param>
        public readonly unsafe void SetData<T>(T* data, int count) where T : unmanaged
        {
            SetData(data, 0, 0, count);
        }

        /// <summary>
        /// Sets data in this buffer from a pointer to unmanaged memory with a specified range.
        /// </summary>
        /// <typeparam name="T">Unmanaged type stored in the memory block.</typeparam>
        /// <param name="data">Pointer to the source data.</param>
        /// <param name="nativeBufferStartIndex">Offset in the source data (in elements).</param>
        /// <param name="graphicsBufferStartIndex">Offset in this buffer (in elements).</param>
        /// <param name="count">Number of elements to copy.</param>
        public readonly unsafe void SetData<T>(T* data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count) where T : unmanaged
        {
            if (count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(graphicsBufferStartIndex * UnsafeUtility.SizeOf<T>() + count * UnsafeUtility.SizeOf<T>() > SizeInBytes))
                throw new ArgumentException("Invalid graphics buffer range.");
#endif

            GraphicsBufferStore.SetData(this, data, nativeBufferStartIndex, graphicsBufferStartIndex, count, UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Sets data in this buffer from a pointer to unmanaged memory with a specified range.
        /// </summary>
        /// <param name="data">Pointer to the source data.</param>
        /// <param name="nativeBufferStartIndex">Offset in the source data (in elements).</param>
        /// <param name="graphicsBufferStartIndex">Offset in this buffer (in elements).</param>
        /// <param name="count">Number of elements to copy.</param>
        /// <param name="stride">The size of each element in bytes.</param>
        public readonly unsafe void SetData(void* data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count, int stride)
        {
            if (count == 0)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(graphicsBufferStartIndex * stride + count * stride > SizeInBytes))
                throw new ArgumentException("Invalid graphics buffer range.");
#endif

            GraphicsBufferStore.SetData(this, data, nativeBufferStartIndex, graphicsBufferStartIndex, count, stride);
        }

        /// <summary>
        /// Sets data in this buffer from a managed array.
        /// </summary>
        /// <param name="data">The managed array to copy from.</param>
        /// <param name="nativeBufferStartIndex">Offset in the managed array.</param>
        /// <param name="graphicsBufferStartIndex">Offset in this buffer (in elements).</param>
        /// <param name="count">Number of elements to copy. If zero or negative, the entire array is used.</param>
        [ExcludeFromBurstCompatTesting("Taking a managed array as an argument.")]
        public void SetData(Array data, int nativeBufferStartIndex = 0, int graphicsBufferStartIndex = 0, int count = 0)
        {
            if (count <= 0)
                count = data.Length;

            if (data.Length == 0 || count == 0)
                return;

            GraphicsBufferStore.SetData(this, data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        /// <summary>
        /// Requests asynchronous GPU readback of data from this buffer.
        /// </summary>
        /// <param name="offsetInBytes">Offset in the buffer from which to start reading (in bytes).</param>
        /// <param name="sizeInBytes">Number of bytes to read. If negative, reads until the end of the buffer.</param>
        /// <returns>An <see cref="AsyncGPUReadbackRequest"/> representing the readback operation.</returns>
        public readonly AsyncGPUReadbackRequest RequestData(long offsetInBytes = 0, long sizeInBytes = -1)
        {
            if (sizeInBytes < 0)
                sizeInBytes = SizeInBytes - offsetInBytes;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(offsetInBytes + sizeInBytes > SizeInBytes))
                throw new ArgumentException("Invalid offset and size.");
#endif

            return GraphicsBufferStore.RequestDataAsync(this, offsetInBytes, sizeInBytes);
        }

        /// <summary>
        /// Requests asynchronous GPU readback to populate a <see cref="NativeArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type matching the data in this buffer.</typeparam>
        /// <param name="data">A <see cref="NativeArray{T}"/> to be filled with the readback results.</param>
        /// <returns>An <see cref="AsyncGPUReadbackRequest"/> representing the readback operation.</returns>
        public readonly AsyncGPUReadbackRequest RequestData<T>(ref NativeArray<T> data) where T : unmanaged
        {
            if (data.Length == 0)
                return default;

            var byteArray = data.Reinterpret<byte>(UnsafeUtility.SizeOf<T>());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(byteArray.Length > SizeInBytes))
                throw new ArgumentException("Data size exceeds buffer size.");
#endif

            return GraphicsBufferStore.RequestDataAsync(this, byteArray);
        }

        /// <summary>
        /// Implicitly converts a <see cref="GraphicsBufferRef"/> to its underlying <see cref="GraphicsBuffer"/>.
        /// </summary>
        /// <param name="value">The <see cref="GraphicsBufferRef"/> to convert.</param>
        /// <returns>The underlying <see cref="GraphicsBuffer"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GraphicsBuffer(GraphicsBufferRef value)
        {
            return GraphicsBufferStore.GetBufferUnchecked(value);
        }

        /// <summary>
        /// Compares this <see cref="GraphicsBufferRef"/> to another by their <see cref="Index"/>.
        /// </summary>
        /// <param name="other">Another <see cref="GraphicsBufferRef"/> to compare against.</param>
        /// <returns>The relative order between the two references.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(GraphicsBufferRef other)
        {
            return Index.CompareTo(other.Index);
        }

        /// <summary>
        /// Indicates whether this <see cref="GraphicsBufferRef"/> is equal to another.
        /// </summary>
        /// <param name="other">Another <see cref="GraphicsBufferRef"/>.</param>
        /// <returns>True if both references point to the same buffer, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(GraphicsBufferRef other)
        {
            return Index == other.Index;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is GraphicsBufferRef other && Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return Index;
        }

        /// <summary>
        /// Checks whether two <see cref="GraphicsBufferRef"/> instances are equal.
        /// </summary>
        /// <param name="lhs">Left-hand side reference.</param>
        /// <param name="rhs">Right-hand side reference.</param>
        /// <returns>True if both references have the same index, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(GraphicsBufferRef lhs, GraphicsBufferRef rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Checks whether two <see cref="GraphicsBufferRef"/> instances are not equal.
        /// </summary>
        /// <param name="lhs">Left-hand side reference.</param>
        /// <param name="rhs">Right-hand side reference.</param>
        /// <returns>True if their indexes differ, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(GraphicsBufferRef lhs, GraphicsBufferRef rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}
