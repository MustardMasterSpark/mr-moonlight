using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace MA.Flora
{
    internal readonly struct IntProfilerCounter
    {
        private readonly IntPtr m_Handle;

        public bool IsCreated => m_Handle != IntPtr.Zero;

        public IntProfilerCounter(string name, ProfilerMarkerDataUnit unit)
        {
            m_Handle = ProfilerUnsafeUtility.CreateMarker(name, ProfilerUnsafeUtility.CategoryScripts, MarkerFlags.Counter | MarkerFlags.Script, 1);
            ProfilerUnsafeUtility.SetMarkerMetadata(m_Handle, 0, "Value", (byte)ProfilerMarkerDataType.Int32, (byte)unit);
        }

        public unsafe void Sample(int value)
        {
            ProfilerMarkerData data = default;
            data.Type = (byte)ProfilerMarkerDataType.Int32;
            data.Size = (uint)UnsafeUtility.SizeOf<int>();
            data.Ptr = UnsafeUtility.AddressOf(ref value);
            ProfilerUnsafeUtility.SingleSampleWithMetadata(m_Handle, 1, &data);
        }
    }
}
