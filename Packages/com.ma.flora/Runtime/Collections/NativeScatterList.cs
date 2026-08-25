using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace MA.Flora
{
    internal unsafe struct NativeScatterList<T> : INativeDisposable where T : unmanaged
    {
        private NativeList<T> m_Values;
        private NativeList<uint> m_Offsets;

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Values.IsCreated && m_Offsets.IsCreated;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length == 0;
        }

        public bool HasScatters
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length > 0;
        }

        public NativeArray<T> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Values.AsArray();
        }

        public NativeArray<uint> Offsets
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Offsets.AsArray();
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Values.Length;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Values.Capacity;
        }

        public NativeScatterList(int count, AllocatorManager.AllocatorHandle allocator)
        {
            m_Values = new NativeList<T>(count, allocator);
            m_Offsets = new NativeList<uint>(count, allocator);
        }

        public void Dispose()
        {
            if (!IsCreated)
                return;

            m_Values.Dispose();
            m_Offsets.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (!IsCreated)
                return inputDeps;

            m_Values.Dispose(inputDeps);
            m_Offsets.Dispose(inputDeps);
            return inputDeps;
        }

        public void Clear()
        {
            m_Values.Clear();
            m_Offsets.Clear();
        }

        public void TrimExcess()
        {
            if (Length == Capacity)
                return;

            m_Values.TrimExcess();
            m_Offsets.TrimExcess();
        }

        public void Resize(int newLength, NativeArrayOptions options)
        {
            m_Values.Resize(newLength, options);
            m_Offsets.Resize(newLength, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T value, uint offset)
        {
            m_Values.Add(value);
            m_Offsets.Add(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(T* values, uint* offsets, int count)
        {
            m_Values.AddRange(values, count);
            m_Offsets.AddRange(offsets, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(NativeArray<T> values, NativeArray<uint> offsets)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (values.Length != offsets.Length)
                throw new System.ArgumentException($"NativeScatterList AddRange length mismatch: {values.Length} != {offsets.Length}");
#endif
            AddRange((T*)values.GetUnsafeReadOnlyPtr(), (uint*)offsets.GetUnsafeReadOnlyPtr(), values.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(T value, uint offset)
        {
            m_Values.AddNoResize(value);
            m_Offsets.AddNoResize(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRangeNoResize(T* values, uint* offsets, int count)
        {
            m_Values.AddRangeNoResize(values, count);
            m_Offsets.AddRangeNoResize(offsets, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRangeNoResize(NativeArray<T> values, NativeArray<uint> offsets)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (values.Length != offsets.Length)
                throw new System.ArgumentException($"NativeScatterList AddRange length mismatch: {values.Length} != {offsets.Length}");
#endif
            AddRangeNoResize((T*)values.GetUnsafeReadOnlyPtr(), (uint*)offsets.GetUnsafeReadOnlyPtr(), values.Length);
        }
    }
}
