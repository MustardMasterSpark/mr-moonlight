// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    using static math;
    using float3 = float3;
    using float3x3 = float3x3;
    using float4x4 = float4x4;
    using quaternion = quaternion;

    /// <summary>
    /// Represents an affine transformation from local space to world space.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct FloraLocalToWorld : IEquatable<FloraLocalToWorld>, IFormattable
    {
        /// <summary>
        /// The identity matrix.
        /// </summary>
        public static readonly FloraLocalToWorld Identity = new() { Value = float4x4.identity };

        /// <summary>
        /// The 4x4 transformation matrix.
        /// </summary>
        public float4x4 Value;

        #region Construction

        /// <summary>
        /// Create from basis vectors and position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloraLocalToWorld(float3 xAxis, float3 yAxis, float3 zAxis, float3 position)
        {
            Value = new float4x4
            {
                c0 = new float4(xAxis,    0),
                c1 = new float4(yAxis,    0),
                c2 = new float4(zAxis,    0),
                c3 = new float4(position, 1)
            };
        }

        /// <summary>
        /// Create from a 3×3 rotation and position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloraLocalToWorld(float3x3 rotation, float3 position)
        {
            Value = new float4x4
            {
                c0 = new float4(rotation.c0, 0),
                c1 = new float4(rotation.c1, 0),
                c2 = new float4(rotation.c2, 0),
                c3 = new float4(position,    1)
            };
        }

        /// <summary>
        /// Create from an instance transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromTransform(FloraInstanceTransform transform)
        {
            float3x3 r = new float3x3(transform.Rotation);
            return new FloraLocalToWorld
            {
                XAxis    = r.c0 * transform.Scale.x,
                YAxis    = r.c1 * transform.Scale.y,
                ZAxis    = r.c2 * transform.Scale.z,
                Position = transform.Position
            };
        }

        /// <summary>
        /// Create from position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromPositionRotationScale(float3 position, quaternion rotation, float3 scale)
        {
            float3x3 r = new float3x3(rotation);
            return new FloraLocalToWorld
            {
                XAxis    = r.c0 * scale.x,
                YAxis    = r.c1 * scale.y,
                ZAxis    = r.c2 * scale.z,
                Position = position
            };
        }

        /// <summary>
        /// Create from position and rotation (unit scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromPositionRotation(float3 position, quaternion rotation) => FromPositionRotationScale(position, rotation, 1.0f);

        /// <summary>
        /// Create from position (identity rotation and unit scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromPosition(float3 position) => FromPositionRotationScale(position, quaternion.identity, 1.0f);

        /// <summary>
        /// Create from rotation (zero translation, unit scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromRotation(quaternion rotation) => FromPositionRotationScale(float3.zero, rotation, 1.0f);

        /// <summary>
        /// Create from scale (zero translation, identity rotation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraLocalToWorld FromScale(float3 scale) => FromPositionRotationScale(float3.zero, quaternion.identity, scale);

        #endregion

        #region Axes / Directions

        /// <summary>
        /// The X basis (right) vector.
        /// </summary>
        public float3 XAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Value.c0.xyz;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Value.c0 = new float4(value, 0);
        }

        /// <summary>
        /// The Y basis (up) vector.
        /// </summary>
        public float3 YAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Value.c1.xyz;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Value.c1 = new float4(value, 0);
        }

        /// <summary>
        /// The Z basis (forward) vector.
        /// </summary>
        public float3 ZAxis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Value.c2.xyz;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Value.c2 = new float4(value, 0);
        }

        /// <summary>
        /// The translation (origin) vector.
        /// </summary>
        public float3 Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Value.c3.xyz;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Value.c3 = new float4(value, 1);
        }

        /// <summary>
        /// Normalized forward (+Z) direction.
        /// </summary>
        public readonly float3 Forward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => normalizesafe(ZAxis);
        }

        /// <summary>
        /// Normalized back (-Z) direction.
        /// </summary>
        public readonly float3 Back
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => -Forward;
        }

        /// <summary>
        /// Normalized up (+Y) direction.
        /// </summary>
        public readonly float3 Up
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => normalizesafe(YAxis);
        }

        /// <summary>
        /// Normalized down (-Y) direction.
        /// </summary>
        public readonly float3 Down
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => -Up;
        }

        /// <summary>
        /// Normalized right (+X) direction.
        /// </summary>
        public readonly float3 Right
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => normalizesafe(XAxis);
        }

        /// <summary>
        /// Normalized left (-X) direction.
        /// </summary>
        public readonly float3 Left
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => -Right;
        }

        #endregion

        #region Properties

        /// <summary>
        /// True if equal to <see cref="Identity"/> exactly.
        /// </summary>
        public readonly bool IsIdentity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                XAxis.Equals(float3(1, 0, 0)) &&
                YAxis.Equals(float3(0, 1, 0)) &&
                ZAxis.Equals(float3(0, 0, 1)) &&
                Position.Equals(float3(0, 0, 0));
        }

        /// <summary>
        /// True if any component is NaN.
        /// </summary>
        public readonly bool ContainsNaN
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => any(isnan(XAxis)) || any(isnan(YAxis)) || any(isnan(ZAxis)) || any(isnan(Position));
        }

        /// <summary>
        /// True if the basis is left-handed (flipped winding).
        /// </summary>
        public readonly bool IsFlipped
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BasisDeterminant < 0f;
        }

        /// <summary>
        /// True if all axis magnitudes are equal within tolerance.
        /// </summary>
        public readonly bool IsUniformScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float3 s = Scale;
                return abs(s.x - s.y) <= MathConstants.ZeroTolerance &&
                       abs(s.x - s.z) <= MathConstants.ZeroTolerance;
            }
        }

        /// <summary>
        /// True if any scale component is approximately zero.
        /// </summary>
        public readonly bool ContainsZeroScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => any(abs(Scale) <= MathConstants.ZeroTolerance);
        }

        /// <summary>
        /// The 3×3 linear part (rotation + scale).
        /// </summary>
        public readonly float3x3 BasisMatrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new(XAxis, YAxis, ZAxis);
        }

        /// <summary>
        /// Determinant of the 3×3 linear part. Negative indicates a flipped (odd) basis.
        /// </summary>
        public readonly float BasisDeterminant
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => determinant(BasisMatrix);
        }

        /// <summary>
        /// The orthonormalized 3×3 linear part (rotation only).
        /// </summary>
        public readonly float3x3 RotationMatrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => orthonormalize(BasisMatrix);
        }

        /// <summary>
        /// Extracts an orientation quaternion from the linear part, orthonormalizing the basis first.
        /// </summary>
        public quaternion Rotation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => quaternion(RotationMatrix);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3x3 r = float3x3(value);
                float3   s = Scale;
                XAxis = r.c0 * s.x;
                YAxis = r.c1 * s.y;
                ZAxis = r.c2 * s.z;
            }
        }

        /// <summary>
        /// Per-axis scale magnitudes (signed, via basis alignment).
        /// </summary>
        public float3 Scale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                float3x3 r = RotationMatrix;
                float sx = length(XAxis) * sign(dot(XAxis, r.c0));
                float sy = length(YAxis) * sign(dot(YAxis, r.c1));
                float sz = length(ZAxis) * sign(dot(ZAxis, r.c2));
                return float3(sx, sy, sz);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3x3 r = RotationMatrix;
                XAxis = r.c0 * value.x;
                YAxis = r.c1 * value.y;
                ZAxis = r.c2 * value.z;
            }
        }

        /// <summary>
        /// Largest axis magnitude (useful for conservative bounds).
        /// </summary>
        public readonly float MaxAxisScale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cmax(abs(Scale));
        }

        #endregion

        #region Transform Composition

        /// <summary>
        /// Exact inverse (works with non-uniform scale and shear).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld Inverse() => inverse(Value);

        /// <summary>
        /// Fast inverse that assumes an orthonormal 3×3 (i.e., rigid transform: rotation + translation only).
        /// Use <see cref="Inverse"/> if non-uniform scale or shear is present.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld InverseFast() => fastinverse(Value);

        /// <summary>
        /// Applies <paramref name="rhs"/> after this transform (i.e., returns this * rhs).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld Transform(FloraLocalToWorld rhs) => mul(Value, rhs.Value);

        /// <summary>
        /// Applies a <see cref="FloraInstanceTransform"/> after this transform (ie, returns this * rhs).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld Transform(FloraInstanceTransform rhs) => Transform(rhs.ToMatrix());

        /// <summary>
        /// Applies <paramref name="lhs"/> before this transform (i.e., returns lhs * this).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld TransformBy(FloraLocalToWorld lhs) => mul(lhs.Value, Value);

        /// <summary>
        /// Applies a <see cref="FloraInstanceTransform"/> before this transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld TransformBy(FloraInstanceTransform lhs) => TransformBy(lhs.ToMatrix());

        #endregion

        #region Point / Vector / Normal

        /// <summary>
        /// Transforms a point (applies rotation/scale then translation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformPoint(float3 rhs) => transform(Value, rhs);

        /// <summary>
        /// Inverse-transforms a point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformPoint(float3 rhs) => mul(inverse(BasisMatrix), rhs - Position);

        /// <summary>
        /// Transforms a direction (ignores translation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformDirection(float3 rhs) => mul(BasisMatrix, rhs);

        /// <summary>
        /// Inverse-transforms a direction.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformDirection(float3 rhs) => mul(inverse(BasisMatrix), rhs);

        /// <summary>
        /// Transforms an axis-aligned <see cref="Bounds"/> by this affine transform and returns
        /// a conservatively-tight AABB in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Bounds TransformBounds(Bounds rhs)
        {
            if (any(float3(rhs.extents) <= 0.0f))
                return new Bounds(TransformPoint(rhs.center), float3.zero);

            float3 x         = XAxis * rhs.extents.x;
            float3 y         = YAxis * rhs.extents.y;
            float3 z         = ZAxis * rhs.extents.z;
            float3 newExtent = abs(x) + abs(y) + abs(z);
            float3 newCenter = TransformPoint(rhs.center);

            return new Bounds
            {
                center = newCenter,
                extents = newExtent
            };
        }

        /// <summary>
        /// Transforms a <see cref="Bounds"/> by the inverse of this transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Bounds InverseTransformBounds(Bounds rhs) => Inverse().TransformBounds(rhs);

        #endregion

        #region With*

        /// <summary>
        /// Returns a transform with the specified position, preserving the same basis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld WithPosition(float3 position) => new(BasisMatrix.c0, BasisMatrix.c1, BasisMatrix.c2, position);

        /// <summary>
        /// Returns a transform with the specified rotation, preserving the same position and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld WithRotation(quaternion rotation)
        {
            float3x3 r = float3x3(rotation);
            float3   s = Scale;
            return new FloraLocalToWorld(r.c0 * s.x, r.c1 * s.y, r.c2 * s.z, Position);
        }

        /// <summary>
        /// Returns a transform with the specified scale, preserving the same position and rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld WithScale(float3 scale)
        {
            float3x3 r = RotationMatrix; // orthonormal
            return new FloraLocalToWorld(r.c0 * scale.x, r.c1 * scale.y, r.c2 * scale.z, Position);
        }

        /// <summary>
        /// Returns a transform with the specified uniform scale, preserving the same position and rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld WithUniformScale(float s) => WithScale(float3(s));

        /// <summary>
        /// Returns a transform with the same position and rotation, but unit scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld WithoutScale() => new(RotationMatrix, Position);

        #endregion

        #region Non-Mutating Modifiers

        /// <summary>
        /// Translates the position of this transform by the specified amount in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld Translate(float3 translation) => new(XAxis, YAxis, ZAxis, Position + translation);

        /// <summary>
        /// Rotates the basis by the specified quaternion in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld RotateBy(quaternion delta)
        {
            float3x3 r = mul(float3x3(delta), RotationMatrix);
            float3 s = Scale;
            return new FloraLocalToWorld(r.c0 * s.x, r.c1 * s.y, r.c2 * s.z, Position);
        }

        /// <summary>
        /// Rotates the basis to look in the specified forward direction with the specified up direction in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld RotateTowards(float3 forward, float3 up)
        {
            float3x3 r = float3x3.LookRotationSafe(forward, up);
            float3 s = Scale;
            return new FloraLocalToWorld(r.c0 * s.x, r.c1 * s.y, r.c2 * s.z, Position);
        }

        /// <summary>
        /// Rotates the basis to look at the specified target point with the specified up direction in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld LookAt(float3 target, float3 up) => RotateTowards(target - Position, up);

        /// <summary>
        /// Rotates the basis around the specified point by the specified quaternion in world space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld RotateAround(float3 point, quaternion delta)
        {
            FloraLocalToWorld rotated = RotateBy(delta);
            float3 position = point + mul(float3x3(delta), Position - point);
            return rotated.WithPosition(position);
        }

        /// <summary>
        /// Scales the basis by the specified amount per-axis.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld ScaleBy(float3 scale)
        {
            float3x3 r = RotationMatrix;
            float3 s = Scale * scale;
            return new FloraLocalToWorld(r.c0 * s.x, r.c1 * s.y, r.c2 * s.z, Position);
        }

        #endregion

        #region Conversions

        /// <summary>Returns the underlying TRS matrix (alias for <see cref="Value"/>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToMatrix() => Value;

        /// <summary>Returns the inverse TRS matrix (equivalent to <c>Inverse().ToMatrix()</c>).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToInverseMatrix() => inverse(Value);

        /// <summary>Returns a matrix with rotation/translation only (drops scale/shear).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToMatrixNoScale() => float4x4(Rotation, Position);

        #endregion

        #region Interface Implementations

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(FloraLocalToWorld rhs) => Value.Equals(rhs.Value);

        /// <summary>Component-wise fuzzy compare.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool NearlyEquals(FloraLocalToWorld rhs, float epsilon = MathConstants.Epsilon) => Value.NearlyEquals(rhs.Value, epsilon);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is FloraLocalToWorld other && Equals(other);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"FloraLocalToWorld(X={XAxis}, Y={YAxis}, Z={ZAxis}, P={Position})";

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
            => $"FloraLocalToWorld(X={XAxis.ToString(format, formatProvider)}, Y={YAxis.ToString(format, formatProvider)}, Z={ZAxis.ToString(format, formatProvider)}, P={Position.ToString(format, formatProvider)})";

        #endregion

        #region Operators

        /// <summary>
        /// Exact equality.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FloraLocalToWorld lhs, FloraLocalToWorld rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Exact inequality.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FloraLocalToWorld lhs, FloraLocalToWorld rhs) => !lhs.Equals(rhs);

        /// <summary>
        /// Implicit conversion from <see cref="float4x4"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FloraLocalToWorld(float4x4 m) => new() { Value = m };

        /// <summary>
        /// Implicit conversion to <see cref="float4x4"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator float4x4(FloraLocalToWorld m) => m.Value;

        /// <summary>
        /// Implicit conversion from <see cref="Matrix4x4"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FloraLocalToWorld(Matrix4x4 m) => (float4x4)m;

        /// <summary>
        /// Implicit conversion to <see cref="Matrix4x4"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Matrix4x4(FloraLocalToWorld m) => (float4x4)m;

        #endregion
    }
}
