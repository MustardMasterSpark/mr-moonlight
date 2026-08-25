using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct CommandBufferProfilerScope : System.IDisposable
    {
        private CommandBuffer m_CommandBuffer;
        private ProfilerMarker m_Marker;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CommandBufferProfilerScope(CommandBuffer cmd, ProfilerMarker marker)
        {
            m_CommandBuffer = cmd;
            m_Marker = marker;
            cmd.BeginSample(marker);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            m_CommandBuffer.EndSample(m_Marker);
        }
    }
}
