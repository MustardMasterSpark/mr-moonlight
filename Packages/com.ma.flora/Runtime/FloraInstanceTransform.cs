// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using float3 = Unity.Mathematics.float3;
using float4x4 = Unity.Mathematics.float4x4;
using quaternion = Unity.Mathematics.quaternion;

namespace MA.Flora
{
    /// <summary>
    /// Represents a position, rotation, and scale for an instance, either relative to a parent or in world space if no parent exists.
    /// </summary>
    [Serializable]
    public struct FloraInstanceTransform : IEquatable<FloraInstanceTransform>
    {
        /// <summary>
        /// A transform with position = (0,0,0), rotation = <see cref="quaternion.identity"/>, and scale = (1,1,1).
        /// </summary>
        public static readonly FloraInstanceTransform Identity = new(0, quaternion.identity, 1);

        /// <summary>
        /// The position component of this transform.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// The rotation component of this transform.
        /// </summary>
        public quaternion Rotation;

        /// <summary>
        /// The scale component of this transform.
        /// </summary>
        /// <remarks>
        /// Uniform or non-uniform scaling is supported. Many helper methods account for non-uniform
        /// scales; for example, <see cref="HasNonUniformScale"/> and <see cref="GetScaleReciprocalSafe"/>.
        /// </remarks>
        public float3 Scale;

        /// <summary>
        /// Calculates the product of all scale components, akin to the determinant if the matrix were purely scale.
        /// </summary>
        public readonly float ScaleDeterminant
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Scale.x * Scale.y * Scale.z;
        }

        #region Construction

        /// <summary>
        /// Constructs a transform from the specified position, rotation, and scale.
        /// </summary>
        /// <param name="position">Position in local or world space.</param>
        /// <param name="rotation">Rotation in local or world space.</param>
        /// <param name="scale">Scale in local or world space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FloraInstanceTransform(float3 position, quaternion rotation, float3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        /// <summary>
        /// Creates a transform by decomposing a TRS matrix (Translation, Rotation, Scale).
        /// </summary>
        /// <remarks>
        /// If the determinant of <paramref name="matrix"/> is negative, the X basis vector is negated.
        /// Hence, it's assumed the input is a valid TRS matrix, not containing reflections or shears.
        /// </remarks>
        /// <param name="matrix">A 4x4 TRS matrix.</param>
        /// <returns>A <see cref="FloraInstanceTransform"/> corresponding to the matrix.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromMatrix(float4x4 matrix)
        {
            quaternion rotation = new quaternion(orthonormalize(new float3x3(matrix)));

            float det = determinant(matrix);
            if (det < 0f)
                matrix[0].xyz = -matrix[0].xyz;

            float3 position = matrix.c3.xyz;
            float3 scale = float3(length(matrix.c0.xyz), length(matrix.c1.xyz), length(matrix.c2.xyz));

            return FromPositionRotationScale(position, rotation, scale);
        }

        /// <summary>
        /// Creates a transform with the specified position and rotation (scale = (1,1,1)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromPositionRotation(float3 position, quaternion rotation) => new() { Position = position, Rotation = rotation, Scale = 1.0f };

        /// <summary>
        /// Creates a transform with the specified position and scale (rotation = identity).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromPositionScale(float3 position, float3 scale) => new() { Position = position, Rotation = quaternion.identity, Scale = scale };

        /// <summary>
        /// Creates a transform with the specified position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromPositionRotationScale(float3 position, quaternion rotation, float3 scale) => new() { Position = position, Rotation = rotation, Scale = scale };

        /// <summary>
        /// Creates a transform with the specified position (rotation = identity, scale = (1,1,1)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromPosition(float3 position) => new() { Position = position, Rotation = quaternion.identity, Scale = 1.0f };

        /// <summary>
        /// Creates a transform with the specified position (rotation = identity, scale = (1,1,1)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromPosition(float x, float y, float z) => new() { Position = float3(x, y, z), Rotation = quaternion.identity, Scale = 1.0f };

        /// <summary>
        /// Creates a transform with the specified rotation (position = (0,0,0), scale = (1,1,1)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromRotation(quaternion rotation) => new() { Position = float3.zero, Rotation = rotation, Scale = 1.0f };

        /// <summary>
        /// Creates a transform with the specified scale (position = (0,0,0), rotation = identity).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromScale(float3 scale) => new() { Position = float3.zero, Rotation = quaternion.identity, Scale = scale };

        /// <summary>
        /// Creates a transform from a <see cref="UnityEngine.Transform"/>, specifying whether to read from local or world space.
        /// </summary>
        /// <param name="unityTransform">The Unity transform to read from.</param>
        /// <param name="space">If <see cref="Space.World"/>, uses <see cref="Transform.position"/>, <see cref="Transform.rotation"/>, and <see cref="Transform.lossyScale"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform FromUnityTransform(Transform unityTransform, Space space)
        {
            return space == Space.World
                ? FromPositionRotationScale(unityTransform.position, unityTransform.rotation, unityTransform.lossyScale)
                : FromPositionRotationScale(unityTransform.localPosition, unityTransform.localRotation, unityTransform.localScale);
        }

        #endregion

        #region Axes / Directions

        /// <summary>
        /// Returns the unit-length right vector (i.e., local X-axis) for this transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 Right() => TransformDirection(float3(1, 0, 0));

        /// <summary>
        /// Returns the unit-length up vector (i.e., local Y-axis) for this transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 Up() => TransformDirection(float3(0, 1, 0));

        /// <summary>
        /// Returns the unit-length forward vector (i.e., local Z-axis) for this transform.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 Forward() => TransformDirection(float3(0, 0, 1));

        #endregion

        #region Transform Composition

        /// <summary>
        /// Transforms another <see cref="FloraInstanceTransform"/> by this transform (applies this transform first).
        /// </summary>
        /// <param name="instanceTransform">The other transform to be transformed.</param>
        /// <returns>A new transform representing the composition of the two.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Transform(FloraInstanceTransform instanceTransform)
        {
            return new FloraInstanceTransform
            {
                Position = TransformPoint(instanceTransform.Position),
                Scale = TransformScale(instanceTransform.Scale),
                Rotation = TransformRotation(instanceTransform.Rotation),
            };
        }

        /// <summary>
        /// Inversely transforms another <see cref="FloraInstanceTransform"/>, effectively applying the inverse of this transform.
        /// </summary>
        /// <param name="instanceTransform">The other transform to be inversely transformed.</param>
        /// <returns>A new transform representing the inverse composition.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform InverseTransform(FloraInstanceTransform instanceTransform)
        {
            return new FloraInstanceTransform
            {
                Position = InverseTransformPoint(instanceTransform.Position),
                Scale = InverseTransformScale(instanceTransform.Scale),
                Rotation = InverseTransformRotation(instanceTransform.Rotation),
            };
        }

        /// <summary>
        /// Returns a transform that is the inverse of this transform (negates rotation, reciprocal of scale, etc.).
        /// </summary>
        /// <exception cref="Exception">If any <see cref="Scale"/> component is zero (division by zero).</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Inverse()
        {
            quaternion inverseRotation = conjugate(Rotation);
            float3 inverseScale = GetScaleReciprocalSafe(Scale);
            return new FloraInstanceTransform
            {
                Position = -rotate(inverseRotation, Position) * inverseScale,
                Scale = inverseScale,
                Rotation = inverseRotation,
            };
        }

        #endregion

        #region With Methods

        /// <summary>
        /// Returns a copy of this transform with a new position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform WithPosition(float3 position) => new() { Position = position, Rotation = Rotation, Scale = Scale };

        /// <summary>
        /// Returns a copy of this transform with a new rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform WithRotation(quaternion rotation) => new() { Position = Position, Scale = Scale, Rotation = rotation };

        /// <summary>
        /// Returns a copy of this transform with a new scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform WithScale(float3 scale) => new() { Position = Position, Rotation = Rotation, Scale = scale };

        #endregion

        #region Point / Vector / Normal

        /// <summary>
        /// Transforms a point by this transform (applies scale, rotation, then translation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformPoint(float3 point) => Position + rotate(Rotation, point * Scale);

        /// <summary>
        /// Transforms a point by the inverse of this transform (applies inverse translation, rotation, then scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformPoint(float3 point) => rotate(conjugate(Rotation), point - Position) * GetScaleReciprocalSafe(Scale);

        /// <summary>
        /// Transforms a direction vector by rotation only (no translation or scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformDirection(float3 direction) => rotate(Rotation, direction);

        /// <summary>
        /// Transforms a direction vector by the inverse of this transform's rotation (no translation or scale).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformDirection(float3 direction) => rotate(conjugate(Rotation), direction);

        /// <summary>
        /// Transforms a vector by rotation and scale, ignoring translation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformVector(float3 vector) => mul(Rotation, Scale * vector);

        /// <summary>
        /// Transforms a vector by the inverse of this transform's rotation and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformVector(float3 vector) => GetScaleReciprocalSafe(Scale) * mul(inverse(Rotation), vector);

        /// <summary>
        /// Transforms a surface normal. Correctly handles non-uniform scale by applying the inverse scale, normalizing, then rotating.
        /// </summary>
        /// <param name="normal">The normal to transform.</param>
        /// <returns>The transformed surface normal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformNormal(float3 normal)
        {
            float3 s = Scale;
            float detSign = sign(s.x * s.y * s.z);
            float3 safeInvS = float3(s.y * s.z * detSign, s.x * s.z * detSign, s.x * s.y * detSign);
            return TransformDirection(normalizesafe(safeInvS * normal));
        }

        /// <summary>
        /// Inversely transforms a surface normal. Correctly handles non-uniform scale
        /// by applying the scale, normalizing, then inverse-rotating.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformNormal(float3 normal) => InverseTransformDirection(normalizesafe(Scale * normal));

        /// <summary>
        /// Transforms a <see cref="Ray"/>, affecting its origin (translation + rotation + scale) and
        /// direction (rotation only, then normalized).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Ray TransformRay(Ray ray)
        {
            float3 origin = TransformPoint(ray.origin);
            float3 direction = normalizesafe(TransformDirection(ray.direction));
            return new Ray(origin, direction);
        }

        /// <summary>
        /// Inversely transforms a <see cref="Ray"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Ray InverseTransformRay(Ray ray)
        {
            float3 invOrigin = InverseTransformPoint(ray.origin);
            float3 invDirection = normalizesafe(InverseTransformDirection(ray.direction));
            return new Ray(invOrigin, invDirection);
        }

        #endregion

        #region Rotation & Scale Transforms

        /// <summary>
        /// Transforms a rotation by this transform's rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly quaternion TransformRotation(quaternion rotation) => mul(Rotation, rotation);

        /// <summary>
        /// Transforms a rotation by the inverse of this transform's rotation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly quaternion InverseTransformRotation(quaternion rotation) => mul(conjugate(Rotation), rotation);

        /// <summary>
        /// Combines the given scale with this transform's scale (multiplicative).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 TransformScale(float3 scale) => scale * Scale;

        /// <summary>
        /// Divides the given scale by this transform's scale, effectively the inverse scale transform.
        /// </summary>
        /// <remarks>Throws if any scale component is zero.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformScale(float3 scale) => scale / Scale;

        /// <summary>
        /// Safely divides the given scale by this transform's scale, replacing near-zero divides with 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 InverseTransformScaleSafe(float3 scale) => scale * GetScaleReciprocalSafe(Scale);

        #endregion

        #region Fluent (Non-Mutating) Modifiers

        /// <summary>
        /// Returns a copy of this transform translated by <paramref name="translation"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Translate(float3 translation) => new() { Position = Position + translation, Rotation = Rotation, Scale = Scale };

        /// <summary>
        /// Returns a copy of this transform, scaled by <paramref name="scale"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform ApplyScale(float3 scale) => new() { Position = Position, Rotation = Rotation, Scale = Scale * scale };

        /// <summary>
        /// Returns a copy of this transform, rotated by <paramref name="rotation"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Rotate(quaternion rotation) => new() { Position = Position, Scale = Scale, Rotation = mul(Rotation, rotation) };

        /// <summary>
        /// Returns a copy of this transform, rotated by Euler angles (XYZ) in radians.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Rotate(float3 eulers) => Rotate(quaternion.EulerXYZ(eulers));

        /// <summary>
        /// Returns a copy of this transform, rotated about a given axis by <paramref name="angle"/> radians.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform Rotate(float3 axis, float angle) => Rotate(quaternion.AxisAngle(axis, angle));

        /// <summary>
        /// Returns a copy of this transform, rotated around a specified point in space.
        /// </summary>
        /// <param name="point">The pivot point for the rotation.</param>
        /// <param name="axis">The axis to rotate around, assumed normalized.</param>
        /// <param name="angle">Rotation angle in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform RotateAround(float3 point, float3 axis, float angle)
        {
            quaternion q = normalizesafe(quaternion.AxisAngle(normalizesafe(axis), angle));
            return new FloraInstanceTransform
            {
                Position = point + mul(q, Position - point),
                Rotation = mul(q, Rotation),
                Scale = Scale
            };
        }

        /// <summary>
        /// Returns a copy of this transform, rotated around a specified point and axis according to a parent transform's space.
        /// </summary>
        /// <param name="parent">Optional parent. If non-null, <paramref name="point"/> and <paramref name="axis"/> are interpreted in parent's space.</param>
        /// <param name="point">Pivot point in the parent's space (or world space if parent is null).</param>
        /// <param name="axis">Axis to rotate around.</param>
        /// <param name="angle">Rotation angle in radians.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform RotateAround(Transform parent, float3 point, float3 axis, float angle)
        {
            if (parent)
            {
                point = parent.InverseTransformPoint(point);
                axis = parent.InverseTransformDirection(axis);
            }

            return RotateAround(point, axis, angle);
        }

        /// <summary>
        /// Rotates this transform around the X axis by <paramref name="angleRadians"/> (returns a new transform).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform RotateX(float angleRadians) => Rotate(quaternion.RotateX(angleRadians));

        /// <summary>
        /// Rotates this transform around the Y axis by <paramref name="angleRadians"/> (returns a new transform).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform RotateY(float angleRadians) => Rotate(quaternion.RotateY(angleRadians));

        /// <summary>
        /// Rotates this transform around the Z axis by <paramref name="angleRadians"/> (returns a new transform).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform RotateZ(float angleRadians) => Rotate(quaternion.RotateZ(angleRadians));

        /// <summary>
        /// Rotates this transform to look at a target point, using the specified up vector. (returns a new transform).
        /// </summary>
        /// <param name="target">The point to look at.</param>
        /// <param name="up">The up vector to use for orientation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform LookAtPoint(float3 target, float3 up)
        {
            quaternion lookRotation = quaternion.LookRotationSafe(target - Position, up);
            return WithRotation(lookRotation);
        }

        /// <summary>
        /// Rotates this transform to look at a target point, using the specified up vector. (returns a new transform).
        /// </summary>
        /// <param name="forward">The forward direction to look towards (should be normalized).</param>
        /// <param name="up">The up vector to use for orientation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraInstanceTransform LookAtDirection(float3 forward, float3 up)
        {
            quaternion lookRotation = quaternion.LookRotationSafe(forward, up);
            return WithRotation(lookRotation);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Indicates whether the scale components of this transform are non-uniform, within a given tolerance.
        /// </summary>
        /// <param name="tolerance">The tolerance below which scale components are considered equal.</param>
        /// <returns>True if the absolute differences among the scale components are greater than <paramref name="tolerance"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool HasNonUniformScale(float tolerance = MathConstants.ZeroTolerance)
            => abs(Scale.x - Scale.y) > tolerance ||
               abs(Scale.x - Scale.z) > tolerance ||
               abs(Scale.y - Scale.z) > tolerance;

        /// <summary>
        /// Retrieves the maximum absolute scale component (i.e., max(|x|, |y|, |z|)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float GetMaximumAxisScale() => cmax(abs(Scale));

        /// <summary>
        /// Ensures that each scale component has at least <paramref name="minimumScale"/> magnitude,
        /// preserving the component sign if below that threshold.
        /// </summary>
        /// <param name="minimumScale">The minimum absolute value of scale allowed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClampMinimumScale(float minimumScale = MathConstants.ZeroTolerance)
        {
            for (int j = 0; j < 3; ++j)
            {
                float value = Scale[j];
                if (abs(value) < minimumScale)
                {
                    value = minimumScale * sign(value);
                    Scale[j] = value;
                }
            }
        }

        /// <summary>
        /// Returns the safe reciprocal (1/x) of scale, outputting 0 if |x| is less than <paramref name="tolerance"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetScaleReciprocalSafe(float3 scale, float tolerance = MathConstants.ZeroTolerance)
            => select(1.0f / scale, float3.zero, abs(scale) <= float3(tolerance));

        #endregion

        #region Conversions

        /// <summary>
        /// Converts this transform to a <see cref="float4x4"/> TRS matrix.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToMatrix() => float4x4.TRS(Position, Rotation, Scale);

        /// <summary>
        /// Converts this transform to a matrix ignoring scale (just rotation and translation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToMatrixNoScale() => new(Rotation, Position);

        /// <summary>
        /// Creates the inverse TRS matrix of this transform. Equivalent to <c>Inverse().ToMatrix()</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float4x4 ToInverseMatrix() => Inverse().ToMatrix();

        /// <summary>
        /// Converts this transform to a <see cref="FloraLocalToWorld"/> matrix.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld ToLocalToWorld() => FloraLocalToWorld.FromPositionRotationScale(Position, Rotation, Scale);

        /// <summary>
        /// Converts this transform to a FloraLocalToWorld ignoring scale (just rotation and translation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld ToLocalToWorldNoScale() => FloraLocalToWorld.FromPositionRotation(Position, Rotation);

        /// <summary>
        /// Creates the inverse FloraLocalToWorld of this transform. Equivalent to <c>Inverse().ToMatrix()</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly FloraLocalToWorld ToLocalToWorldMatrix() => Inverse().ToLocalToWorld();

        #endregion

        #region Equality & Hashing

        /// <summary>
        /// Checks if two transforms have the same position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(FloraInstanceTransform other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);

        /// <summary>
        /// Checks if this transform is equal to another object (which must be a <see cref="FloraInstanceTransform"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object o) => o is FloraInstanceTransform instanceTransform && Equals(instanceTransform);

        /// <summary>
        /// Returns a hash code that combines position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode() => unchecked((int)(hash(Position) + hash(Rotation) + hash(Scale) + 0x4FC93C25u));

        /// <summary>
        /// Returns a string representation of the transform, showing position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString() => $"Position={Position}, Rotation={Rotation}, Scale={Scale}";

        #endregion

        #region Operators

        /// <summary>
        /// Returns a copy of <paramref name="lhs"/> translated by <paramref name="rhs"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform operator +(FloraInstanceTransform lhs, float3 rhs) => lhs.Translate(rhs);

        /// <summary>
        /// Returns a copy of <paramref name="lhs"/> translated by -<paramref name="rhs"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform operator -(FloraInstanceTransform lhs, float3 rhs) => lhs.Translate(-rhs);

        /// <summary>
        /// Returns true if two transforms have the same position, rotation, and scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(FloraInstanceTransform lhs, FloraInstanceTransform rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Returns true if two transforms differ in position, rotation, or scale.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(FloraInstanceTransform lhs, FloraInstanceTransform rhs) => !lhs.Equals(rhs);

        #endregion
    }

    /// <summary>
    /// Provides extension methods for converting between <see cref="FloraInstanceTransform"/> and Unity <see cref="Transform"/>.
    /// </summary>
    public static class FloraTransformHelpers
    {
        /// <summary>
        /// Copies a Unity <see cref="Transform"/> into a <see cref="FloraInstanceTransform"/>, choosing local or world space.
        /// </summary>
        /// <param name="instanceTransform">The target <see cref="FloraInstanceTransform"/> to modify.</param>
        /// <param name="transform">The Unity transform source.</param>
        /// <param name="space">The space in which to interpret the transform (local or world).</param>
        /// <returns>A <see cref="FloraInstanceTransform"/> reflecting <paramref name="transform"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom(ref this FloraInstanceTransform instanceTransform, Transform transform, Space space)
        {
            instanceTransform = space == Space.World
                ? FloraInstanceTransform.FromPositionRotationScale(transform.position, transform.rotation, transform.lossyScale)
                : FloraInstanceTransform.FromPositionRotationScale(transform.localPosition, transform.localRotation, transform.localScale);
        }

        /// <summary>
        /// Copies a <see cref="FloraInstanceTransform"/> into a Unity <see cref="Transform"/>, choosing local or world space.
        /// </summary>
        /// <param name="unityTransform">The target transform to modify.</param>
        /// <param name="instanceTransform">The source <see cref="FloraInstanceTransform"/>.</param>
        /// <param name="space">Specifies whether to apply the transform in local or world space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom(this Transform unityTransform, FloraInstanceTransform instanceTransform, Space space)
        {
            if (space == Space.World)
            {
                Transform parent = unityTransform.parent;
                unityTransform.parent = null; // Temporarily remove parent to set position/scale in world space properly
                unityTransform.position = instanceTransform.Position;
                unityTransform.rotation = instanceTransform.Rotation;
                unityTransform.localScale = instanceTransform.Scale;
                unityTransform.SetParent(parent, true);
            }
            else
            {
                unityTransform.localPosition = instanceTransform.Position;
                unityTransform.localRotation = instanceTransform.Rotation;
                unityTransform.localScale = instanceTransform.Scale;
            }
        }

        /// <summary>
        /// Converts a Unity <see cref="Transform"/> to a <see cref="FloraInstanceTransform"/>, specifying the space (local or world).
        /// </summary>
        /// <param name="t">The Unity transform to convert.</param>
        /// <param name="space">The space in which to interpret the transform (local or world).</param>
        /// <returns>A <see cref="FloraInstanceTransform"/> representing the Unity transform.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FloraInstanceTransform AsInstanceTransform(this Transform t, Space space) => FloraInstanceTransform.FromUnityTransform(t, space);

        /// <summary>
        /// Transforms a <see cref="FloraInstanceTransform"/> by a Unity <see cref="Transform"/>, effectively <c>t (child) * localTransform</c>.
        /// </summary>
        /// <param name="t">The Unity transform used as a parent space.</param>
        /// <param name="localInstanceTransform">The <see cref="FloraInstanceTransform"/> to transform.</param>
        /// <returns>The transformed <see cref="FloraInstanceTransform"/> in world space.</returns>
        public static FloraInstanceTransform Transform(this Transform t, FloraInstanceTransform localInstanceTransform)
        {
            FloraInstanceTransform space = FloraInstanceTransform.FromUnityTransform(t, Space.World);
            return space.Transform(localInstanceTransform);
        }

        /// <summary>
        /// Inversely transforms a <see cref="FloraInstanceTransform"/> by a Unity <see cref="Transform"/>.
        /// </summary>
        /// <param name="t">The Unity transform used as a parent space.</param>
        /// <param name="worldInstanceTransform">The <see cref="FloraInstanceTransform"/> to transform.</param>
        /// <returns>The inverse-transformed <see cref="FloraInstanceTransform"/> in local space.</returns>
        public static FloraInstanceTransform InverseTransform(this Transform t, FloraInstanceTransform worldInstanceTransform)
        {
            FloraInstanceTransform space = FloraInstanceTransform.FromUnityTransform(t, Space.World);
            return space.InverseTransform(worldInstanceTransform);
        }
    }
}
