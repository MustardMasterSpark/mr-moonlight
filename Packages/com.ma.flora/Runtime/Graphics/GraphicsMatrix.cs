// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace MA.Flora
{
    using static math;
    using float3 = float3;
    using float4x4 = float4x4;

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct GraphicsMatrix : IEquatable<GraphicsMatrix>
    {
        public static GraphicsMatrix Identity => new GraphicsMatrix
        {
            XAxis    = new float3(1, 0, 0),
            YAxis    = new float3(0, 1, 0),
            ZAxis    = new float3(0, 0, 1),
            Position = new float3(0, 0, 0)
        };

        public float4 packed0;
        public float4 packed1;
        public float4 packed2;

        public float3 XAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => packed0.xyz;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => packed0.xyz = value;
        }

        public float3 YAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new float3(packed0.w, packed1.xy);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { packed0.w = value.x; packed1.xy = value.yz; }
        }

        public float3 ZAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new float3(packed1.zw, packed2.x);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { packed1.zw = value.xy; packed2.x = value.z; }
        }

        public float3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new float3(packed2.yzw);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => packed2.yzw = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GraphicsMatrix(float4x4 matrix)
        {
            packed0 = new float4(matrix.c0.xyz, matrix.c1.x);
            packed1 = new float4(matrix.c1.yz, matrix.c2.xy);
            packed2 = new float4(matrix.c2.z, matrix.c3.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GraphicsMatrix(float3x3 rotation, float3 position)
        {
            packed0 = new float4(rotation.c0, rotation.c1.x);
            packed1 = new float4(rotation.c1.yz, rotation.c2.xy);
            packed2 = new float4(rotation.c2.z, position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(GraphicsMatrix rhs)
            => XAxis.Equals(rhs.XAxis) &&
               YAxis.Equals(rhs.YAxis) &&
               ZAxis.Equals(rhs.ZAxis) &&
               Position.Equals(rhs.Position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NearlyEquals(in GraphicsMatrix rhs, float epsilon = MathConstants.Epsilon)
            => XAxis.NearlyEquals(rhs.XAxis, epsilon) &&
               YAxis.NearlyEquals(rhs.YAxis, epsilon) &&
               ZAxis.NearlyEquals(rhs.ZAxis, epsilon) &&
               Position.NearlyEquals(rhs.Position, epsilon);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
            => obj is GraphicsMatrix other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
            => UnsafeUtility.As<GraphicsMatrix, float3x4>(ref this).GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
            => $"GraphicsMatrix(X={XAxis}, Y={YAxis}, Z={ZAxis}, O={Position})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
            => $"GraphicsMatrix(X={XAxis.ToString(format, formatProvider)}, Y={YAxis.ToString(format, formatProvider)}, Z={ZAxis.ToString(format, formatProvider)}, O={Position.ToString(format, formatProvider)})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(GraphicsMatrix lhs, GraphicsMatrix rhs)
            => lhs.Equals(rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(GraphicsMatrix lhs, GraphicsMatrix rhs)
            => !lhs.Equals(rhs);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GraphicsMatrix(float4x4 m)
        {
            GraphicsMatrix gm;
            gm.packed0 = new float4(m.c0.xyz, m.c1.x);
            gm.packed1 = new float4(m.c1.yz, m.c2.xy);
            gm.packed2 = new float4(m.c2.z, m.c3.xyz);
            return gm;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4x4(GraphicsMatrix m)
        {
            return new float4x4
            {
                c0 = float4(m.XAxis, 0),
                c1 = float4(m.YAxis, 0),
                c2 = float4(m.ZAxis, 0),
                c3 = float4(m.Position, 1)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GraphicsMatrix(FloraLocalToWorld m)
        {
            GraphicsMatrix gm;
            gm.packed0 = new float4(m.Value.c0.xyz, m.Value.c1.x);
            gm.packed1 = new float4(m.Value.c1.yz, m.Value.c2.xy);
            gm.packed2 = new float4(m.Value.c2.z, m.Value.c3.xyz);
            return gm;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FloraLocalToWorld(GraphicsMatrix m)
        {
            FloraLocalToWorld localToWorld;
            localToWorld.Value = new float4x4
            {
                c0 = float4(m.XAxis, 0),
                c1 = float4(m.YAxis, 0),
                c2 = float4(m.ZAxis, 0),
                c3 = float4(m.Position, 1)
            };
            return localToWorld;
        }
    }
}
