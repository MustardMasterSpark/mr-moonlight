using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct ConstantBufferArrayRef<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> m_Data;
        private NativeArray<byte> m_AlignedData;
        private GraphicsBufferRef m_Buffer;
        private FixedString64Bytes m_Name;
        private NativeArray<GraphicsBufferRef> m_BufferArray;
        private readonly int m_SizeInBuffer;
        private readonly int m_ConstantBufferOffsetAlignment;
        private readonly bool SupportsConstantBufferAlignment => m_ConstantBufferOffsetAlignment > 0;

        public ConstantBufferArrayRef(int count, string name)
        {
            m_Data = new NativeArray<T>(count, Allocator.Persistent);
            m_Name = name;

            int sizeOfElement = UnsafeUtility.SizeOf<T>();

            m_ConstantBufferOffsetAlignment = SystemInfo.constantBufferOffsetAlignment;
            if (m_ConstantBufferOffsetAlignment > 0)
            {
                m_SizeInBuffer = MathUtility.NextMultipleOf(sizeOfElement, SystemInfo.constantBufferOffsetAlignment);
                m_AlignedData = new NativeArray<byte>(count * m_SizeInBuffer, Allocator.Persistent);
                m_Buffer = new GraphicsBufferRef(GraphicsBuffer.Target.Constant, count, m_SizeInBuffer, m_Name);
                m_BufferArray = default;
            }
            else
            {
                m_AlignedData = default;
                m_SizeInBuffer = sizeOfElement;
                m_Buffer = default;
                m_BufferArray = new NativeArray<GraphicsBufferRef>(count, Allocator.Persistent);
                for (int i = 0; i < count; i++)
                    m_BufferArray[i] = new GraphicsBufferRef(GraphicsBuffer.Target.Constant, 1, m_SizeInBuffer, m_Name);
            }
        }

        public void Dispose()
        {
            if (!m_Data.IsCreated)
                return;

            m_Data.Dispose();

            if (m_Buffer.IsCreated)
            {
                m_Buffer.Dispose();
                m_AlignedData.Dispose();
            }
            else
            {
                for (int i = 0; i < m_BufferArray.Length; i++)
                    m_BufferArray[i].Dispose();

                m_BufferArray.Dispose();
            }
        }

        public bool IsCreated => m_Data.IsCreated;

        public int BufferStride => m_SizeInBuffer;

        public int Length => m_Data.Length;

        public NativeArray<T> Data => m_Data;

        public GraphicsBufferRef Buffer => m_Buffer;

        public T this[int index]
        {
            get => m_Data[index];
            set => m_Data[index] = value;
        }

        public unsafe void UpdateData()
        {
            if (m_Buffer.IsCreated)
            {
                Assert.IsTrue(m_AlignedData.IsCreated && SupportsConstantBufferAlignment,
                    "Aligned data must be created if constant buffer alignment is supported.");

                UnsafeUtility.MemClear(m_AlignedData.GetUnsafePtr(), m_AlignedData.Length);

                // Copy to the aligned staging array
                for (int i = 0; i < m_Data.Length; i++)
                {
                    byte* src = (byte*)m_Data.GetUnsafePtr() + i * UnsafeUtility.SizeOf<T>();
                    byte* dst = (byte*)m_AlignedData.GetUnsafePtr() + i * m_SizeInBuffer;
                    UnsafeUtility.MemCpy(dst, src, UnsafeUtility.SizeOf<T>());
                }

                m_Buffer.SetData(m_AlignedData);
            }
            else
            {
                for (int i = 0; i < m_BufferArray.Length; i++)
                    m_BufferArray[i].SetData(m_Data.GetSubArray(i, 1));
            }
        }

        public unsafe void UpdateData(int index)
        {
            Assert.IsTrue(index >= 0 && index < m_Data.Length, "Index out of range.");

            if (m_Buffer.IsCreated)
            {
                Assert.IsTrue(m_AlignedData.IsCreated && SupportsConstantBufferAlignment,
                    "Aligned data must be created if constant buffer alignment is supported.");

                // Copy the single element into the aligned staging array
                byte* src = (byte*)m_Data.GetUnsafePtr() + index * UnsafeUtility.SizeOf<T>();
                byte* dst = (byte*)m_AlignedData.GetUnsafePtr() + index * m_SizeInBuffer;
                UnsafeUtility.MemCpy(dst, src, UnsafeUtility.SizeOf<T>());

                m_Buffer.SetData(
                    data:                     m_AlignedData,
                    nativeBufferStartIndex:   index * m_SizeInBuffer,
                    graphicsBufferStartIndex: index * m_SizeInBuffer,
                    count:                    m_SizeInBuffer);
            }
            else
            {
                m_BufferArray[index].SetData(m_Data.GetSubArray(index, 1));
            }
        }

        public unsafe void UpdateData(CommandBuffer cmd)
        {
            if (m_Buffer.IsCreated)
            {
                Assert.IsTrue(m_AlignedData.IsCreated && SupportsConstantBufferAlignment,
                    "Aligned data must be created if constant buffer alignment is supported.");

                UnsafeUtility.MemClear(m_AlignedData.GetUnsafePtr(), m_AlignedData.Length);

                // Copy to the aligned staging array
                for (int i = 0; i < m_Data.Length; i++)
                {
                    byte* src = (byte*)m_Data.GetUnsafePtr() + i * UnsafeUtility.SizeOf<T>();
                    byte* dst = (byte*)m_AlignedData.GetUnsafePtr() + i * m_SizeInBuffer;
                    UnsafeUtility.MemCpy(dst, src, UnsafeUtility.SizeOf<T>());
                }

                cmd.SetBufferData(m_Buffer, m_AlignedData);
            }
            else
            {
                for (int i = 0; i < m_BufferArray.Length; i++)
                {
                    cmd.SetBufferData(m_BufferArray[i], m_Data.GetSubArray(i, 1));
                }
            }
        }

        public void Resize(int count)
        {
            if (count != m_Data.Length)
            {
                m_Data.ResizeArraySafe(count);

                if (SupportsConstantBufferAlignment)
                {
                    m_AlignedData.ResizeArraySafe(count * m_SizeInBuffer);

                    if (m_Buffer.IsCreated)
                        m_Buffer.Dispose();

                    if (count > 0)
                    {
                        m_Buffer = new GraphicsBufferRef(GraphicsBuffer.Target.Constant, count, m_SizeInBuffer, m_Name);
                        UpdateData();
                    }
                    else
                    {
                        m_Buffer = default;
                    }
                }
                else
                {
                    for (int i = 0; i < m_BufferArray.Length; ++i)
                        m_BufferArray[i].Dispose();

                    m_BufferArray.ResizeArraySafe(count);

                    for (int i = 0; i < count; ++i)
                    {
                        m_BufferArray[i] = new GraphicsBufferRef(GraphicsBuffer.Target.Constant, 1, m_SizeInBuffer, m_Name);
                        m_BufferArray[i].SetData(m_Data.GetSubArray(i, 1));
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"ConstantBufferRef<{typeof(T).Name}> {m_Name} ({m_Data.Length})";
        }

        public readonly void Bind(int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                Shader.SetGlobalConstantBuffer(nameID, m_Buffer, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                Shader.SetGlobalConstantBuffer(nameID, m_BufferArray[index], 0, UnsafeUtility.SizeOf<T>());
        }

        public readonly void Bind(CommandBuffer cmd, int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                cmd.SetGlobalConstantBuffer(m_Buffer, nameID, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                cmd.SetGlobalConstantBuffer(m_BufferArray[index], nameID, 0, UnsafeUtility.SizeOf<T>());
        }

        public readonly void Bind(CommandBuffer cmd, ComputeShader cs, int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                cmd.SetComputeConstantBufferParam(cs, nameID, m_Buffer, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                cmd.SetComputeConstantBufferParam(cs, nameID, m_BufferArray[index], 0, UnsafeUtility.SizeOf<T>());
        }

        public readonly void Bind(ComputeShader cs, int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                cs.SetConstantBuffer(nameID, m_Buffer, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                cs.SetConstantBuffer(nameID, m_BufferArray[index], 0, UnsafeUtility.SizeOf<T>());
        }

        public readonly void Bind(Material mat, int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                mat.SetConstantBuffer(nameID, m_Buffer, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                mat.SetConstantBuffer(nameID, m_BufferArray[index], 0, UnsafeUtility.SizeOf<T>());
        }

        public readonly void Bind(MaterialPropertyBlock mpb, int nameID, int index)
        {
            if (SupportsConstantBufferAlignment)
                mpb.SetConstantBuffer(nameID, m_Buffer, index * m_SizeInBuffer, UnsafeUtility.SizeOf<T>());
            else
                mpb.SetConstantBuffer(nameID, m_BufferArray[index], 0, UnsafeUtility.SizeOf<T>());
        }

        public static implicit operator GraphicsBufferRef(ConstantBufferArrayRef<T> constantBuffer)
        {
            return constantBuffer.Buffer;
        }

        public static implicit operator GraphicsBuffer(ConstantBufferArrayRef<T> constantBuffer)
        {
            return constantBuffer.Buffer.Value;
        }
    }

    internal static class ConstantArrayBufferRefHelpers
    {
        public static void SetGlobalConstantBuffer<T>(this CommandBuffer cmd, int nameID, ConstantBufferArrayRef<T> constantBuffer, int index) where T : unmanaged
        {
            constantBuffer.Bind(cmd, nameID, index);
        }

        public static void SetComputeConstantBufferParam<T>(this CommandBuffer cmd, ComputeShader computeShader, int nameID, ConstantBufferArrayRef<T> constantBuffer, int index) where T : unmanaged
        {
            constantBuffer.Bind(cmd, computeShader, nameID, index);
        }

        public static void SetConstantBuffer<T>(this Material mat, int nameID, ConstantBufferArrayRef<T> constantBuffer, int index) where T : unmanaged
        {
            constantBuffer.Bind(mat, nameID, index);
        }

        public static void SetConstantBuffer<T>(this MaterialPropertyBlock mpb, int nameID, ConstantBufferArrayRef<T> constantBuffer, int index) where T : unmanaged
        {
            constantBuffer.Bind(mpb, nameID, index);
        }
    }
}
