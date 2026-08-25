// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct ConstantBufferRef<T> : IDisposable where T : unmanaged
    {
        private NativeArray<T> m_Data;
        private GraphicsBufferRef m_Buffer;

        public ConstantBufferRef(string name)
        {
            m_Data = new NativeArray<T>(1, Allocator.Persistent);
            m_Buffer = new GraphicsBufferRef(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<T>(), name);
        }

        public void Dispose()
        {
            if (!m_Data.IsCreated)
                return;

            m_Data.Dispose();
            m_Buffer.Dispose();
        }

        public bool IsCreated => m_Data.IsCreated;

        public int Stride => UnsafeUtility.SizeOf<T>();

        public GraphicsBufferRef Buffer => m_Buffer;

        public NativeArray<T> Data => m_Data;

        public T Value
        {
            get => m_Data[0];
            set => m_Data[0] = value;
        }

        public void UpdateData()
        {
            m_Buffer.SetData(m_Data);
        }

        public void UpdateData(T value)
        {
            m_Data[0] = value;
            m_Buffer.SetData(m_Data);
        }

        public void UpdateData(CommandBuffer cmd, T value)
        {
            m_Data[0] = value;
            cmd.SetBufferData(m_Buffer, m_Data);
        }

        public void UpdateData(CommandBuffer cmd)
        {
            cmd.SetBufferData(m_Buffer, m_Data);
        }

        public readonly void Bind(int nameID)
        {
            Shader.SetGlobalConstantBuffer(nameID, m_Buffer, 0, UnsafeUtility.SizeOf<T>());
        }

        public static implicit operator T(ConstantBufferRef<T> constantBuffer)
        {
            return constantBuffer.Value;
        }

        public static implicit operator GraphicsBufferRef(ConstantBufferRef<T> constantBuffer)
        {
            return constantBuffer.Buffer;
        }

        public static implicit operator GraphicsBuffer(ConstantBufferRef<T> constantBuffer)
        {
            return constantBuffer.Buffer.Value;
        }
    }

    internal static class ConstantBufferRefHelpers
    {
        public static void SetGlobalConstantBuffer<T>(this CommandBuffer cmd, int nameID, ConstantBufferRef<T> constantBuffer) where T : unmanaged
        {
            cmd.SetGlobalConstantBuffer(constantBuffer, nameID, 0, UnsafeUtility.SizeOf<T>());
        }

        public static void SetComputeConstantBufferParam<T>(this CommandBuffer cmd, ComputeShader computeShader, int nameID, ConstantBufferRef<T> constantBuffer) where T : unmanaged
        {
            cmd.SetComputeConstantBufferParam(computeShader, nameID, constantBuffer, 0, UnsafeUtility.SizeOf<T>());
        }

        public static void SetConstantBuffer<T>(this Material mat, int nameID, ConstantBufferRef<T> constantBuffer) where T : unmanaged
        {
            mat.SetConstantBuffer(nameID, constantBuffer, 0, UnsafeUtility.SizeOf<T>());
        }

        public static void SetConstantBuffer<T>(this MaterialPropertyBlock mpb, int nameID, ConstantBufferRef<T> constantBuffer) where T : unmanaged
        {
            mpb.SetConstantBuffer(nameID, constantBuffer, 0, UnsafeUtility.SizeOf<T>());
        }
    }
}
