// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal static class TransformUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion TransformRotation(this Transform transform, Quaternion rotation)
        {
            return transform.rotation * rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion InverseTransformRotation(this Transform transform, Quaternion rotation)
        {
            return Quaternion.Inverse(transform.rotation) * rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 TransformScale(this Transform transform, Vector3 scale)
        {
            return Vector3.Scale(transform.lossyScale, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 InverseTransformScale(this Transform transform, Vector3 scale)
        {
            return Vector3.Scale(1f / (float3)transform.lossyScale, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseTransformScale(this Transform transform, float scale)
        {
            float maxScale = math.cmax(transform.lossyScale);
            return scale / maxScale;
        }
    }
}
