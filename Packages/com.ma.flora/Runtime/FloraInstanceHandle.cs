// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;

namespace MA.Flora
{
    /// <summary>
    /// Represents a handle to a Flora instance, uniquely identified by an Index and a Version.
    /// </summary>
    /// <remarks>
    /// A <see cref="FloraInstanceHandle"/> is valid only if both the <see cref="Index"/> and <see cref="Version"/>
    /// match an existing instance in the <see cref="FloraSystem"/>. If an instance is destroyed (or recycled),
    /// the Version is incremented, so an old handle referencing the same Index but with a different Version
    /// becomes invalid.
    /// </remarks>
    public partial struct FloraInstanceHandle : IEquatable<FloraInstanceHandle>, IComparable<FloraInstanceHandle>
    {
        /// <summary>
        /// A "blank" handle that does not refer to a valid instance.
        /// </summary>
        public static FloraInstanceHandle Null => new();

        /// <summary>
        /// The index portion of this handle.
        /// </summary>
        /// <value>An index into the internal list of instances within the <see cref="FloraSystem"/>.</value>
        /// <remarks>
        /// Index values are recycled. When an instance is destroyed, its index may be reused for a new instance, but the
        /// <see cref="Version"/> is incremented. Therefore, an instance is only truly the same if both the <see cref="Index"/>
        /// and <see cref="Version"/> match.
        /// </remarks>
        public int Index;

        /// <summary>
        /// The generational version of this handle.
        /// </summary>
        /// <value>
        /// Used to distinguish between recycled instances that might use the same <see cref="Index"/>.
        /// </value>
        /// <remarks>
        /// Because <see cref="Version"/> can increment many times, it may eventually overflow. For this reason,
        /// you cannot rely on the numeric ordering of <see cref="Version"/> to infer which instance is more recent.
        /// </remarks>
        public int Version;

        /// <summary>
        /// Checks if this handle is valid and refers to an existing instance in the <see cref="FloraSystem"/>.
        /// </summary>
        /// <returns>True if this handle is not <see cref="Null"/> and the instance exists in the <see cref="FloraSystem"/>; otherwise, false.</returns>
        public bool Exists()
        {
            return this != Null && InstanceRegistry.Data.Exists(this);
        }

        /// <summary>
        /// Compares two handles to determine if they refer to the same instance.
        /// </summary>
        /// <param name="lhs">The left-hand handle to compare.</param>
        /// <param name="rhs">The right-hand handle to compare.</param>
        /// <returns>True if both <paramref name="lhs"/> and <paramref name="rhs"/> have identical <see cref="Index"/> and <see cref="Version"/>.</returns>
        public static bool operator ==(FloraInstanceHandle lhs, FloraInstanceHandle rhs)
        {
            return lhs.Index == rhs.Index && lhs.Version == rhs.Version;
        }

        /// <summary>
        /// Compares two handles to determine if they refer to different instances.
        /// </summary>
        /// <param name="lhs">The left-hand handle to compare.</param>
        /// <param name="rhs">The right-hand handle to compare.</param>
        /// <returns>True if either the <see cref="Index"/> or <see cref="Version"/> differ between <paramref name="lhs"/> and <paramref name="rhs"/>.</returns>
        public static bool operator !=(FloraInstanceHandle lhs, FloraInstanceHandle rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Compares this handle to another handle by their <see cref="Index"/> values.
        /// </summary>
        /// <param name="other">Another <see cref="FloraInstanceHandle"/> to compare against.</param>
        /// <returns>
        /// A signed integer indicating the relative ordering:
        /// <list type="bullet">
        /// <item><description>&lt; 0 if this handle's <see cref="Index"/> is less than <paramref name="other"/>.</description></item>
        /// <item><description>0 if the <see cref="Index"/> values are equal.</description></item>
        /// <item><description>&gt; 0 if this handle's <see cref="Index"/> is greater than <paramref name="other"/>.</description></item>
        /// </list>
        /// </returns>
        public int CompareTo(FloraInstanceHandle other)
        {
            return Index - other.Index;
        }

        /// <summary>
        /// Checks for equality with another object.
        /// </summary>
        /// <param name="compare">The object to compare to this handle.</param>
        /// <returns>
        /// True if <paramref name="compare"/> is a <see cref="FloraInstanceHandle"/> with the same <see cref="Index"/> and <see cref="Version"/>;
        /// otherwise, false.
        /// </returns>
        public override bool Equals(object compare)
        {
            return compare is FloraInstanceHandle instance && Equals(instance);
        }

        /// <summary>
        /// Returns a hash code based solely on the <see cref="Index"/>.
        /// </summary>
        /// <remarks>
        /// Note that this omits the <see cref="Version"/> in the hash, which can lead to collisions if multiple handles
        /// share the same <see cref="Index"/> but differ by <see cref="Version"/>.
        /// </remarks>
        /// <returns>An integer hash code.</returns>
        public override int GetHashCode()
        {
            return Index;
        }

        /// <summary>
        /// Determines whether this handle references the same instance as another handle.
        /// </summary>
        /// <param name="instance">The other <see cref="FloraInstanceHandle"/> to compare against.</param>
        /// <returns>
        /// True if both the <see cref="Index"/> and <see cref="Version"/> match; otherwise, false.
        /// </returns>
        public bool Equals(FloraInstanceHandle instance)
        {
            return instance.Index == Index && instance.Version == Version;
        }

        /// <summary>
        /// Returns a string representation of this handle for debugging.
        /// </summary>
        /// <returns>
        /// <c>"FloraInstanceHandle.Null"</c> if this handle is null, or <c>"FloraInstanceHandle(Index:Version)"</c> otherwise.
        /// </returns>
        public override string ToString()
        {
            return Equals(Null) ? "FloraInstanceHandle.Null" : $"FloraInstanceHandle({Index}:{Version})";
        }

        /// <summary>
        /// Returns a Burst-compatible string representation of this handle for debugging.
        /// </summary>
        /// <returns>
        /// A <see cref="FixedString64Bytes"/> that matches the format <c>"FloraInstanceHandle(Index:Version)"</c>, or
        /// <c>"FloraInstanceHandle.Null"</c> if this handle is null.
        /// </returns>
        [GenerateTestsForBurstCompatibility]
        public FixedString64Bytes ToFixedString()
        {
            if (Equals(Null))
                return (FixedString64Bytes)"FloraInstanceHandle.Null";

            var fs = new FixedString64Bytes();
            fs.Append((FixedString32Bytes)"FloraInstanceHandle(");
            fs.Append(Index);
            fs.Append(':');
            fs.Append(Version);
            fs.Append(')');
            return fs;
        }
    }
}
