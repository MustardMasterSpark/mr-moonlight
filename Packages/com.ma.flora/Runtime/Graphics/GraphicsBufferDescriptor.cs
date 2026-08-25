// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;

namespace MA.Flora
{
    /// <summary>
    /// A descriptor for a <see cref="GraphicsBuffer"/>.
    /// </summary>
    [DebuggerDisplay("Target = {Target}, UsageFlags = {UsageFlags}, Stride = {Stride}, Length = {Length}")]
    internal struct GraphicsBufferDescriptor : IEquatable<GraphicsBufferDescriptor>
    {
        /// <summary>
        /// The target of the buffer.
        /// </summary>
        public GraphicsBuffer.Target Target;
        /// <summary>
        /// The usage flags of the buffer.
        /// </summary>
        public GraphicsBuffer.UsageFlags UsageFlags;
        /// <summary>
        /// The stride of each element in the buffer.
        /// </summary>
        public int Stride;
        /// <summary>
        /// The number of elements in the buffer.
        /// </summary>
        public int Length;
        /// <summary>
        /// The size of the buffer in bytes.
        /// </summary>
        public long SizeInBytes => Stride * Length;

        /// <summary>
        /// Creates a new descriptor for a <see cref="GraphicsBuffer"/>.
        /// </summary>
        /// <param name="target">The target of the buffer.</param>
        /// <param name="usageFlags">The usage flags of the buffer.</param>
        /// <param name="stride">The stride of each element in the buffer.</param>
        /// <param name="length">The number of elements in the buffer.</param>
        public GraphicsBufferDescriptor(GraphicsBuffer.Target target, GraphicsBuffer.UsageFlags usageFlags, int stride, int length)
        {
            Target = target;
            UsageFlags = usageFlags;
            Stride = stride;
            Length = length;
        }

        /// <summary>
        /// Returns true if this descriptor is equal to an object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GraphicsBufferDescriptor other && Equals(other);
        }

        /// <summary>
        /// Returns true if this descriptor is equal to another descriptor
        /// </summary>
        public bool Equals(GraphicsBufferDescriptor other)
        {
            return Target == other.Target && UsageFlags == other.UsageFlags && Stride == other.Stride && Length == other.Length;
        }

        /// <summary>
        /// Returns the hash code of this descriptor.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = new xxHash3.StreamingState(true);
            hash.Update(Target);
            hash.Update(UsageFlags);
            hash.Update(Stride);
            hash.Update(Length);
            return hash.DigestHash64().GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of the descriptor.
        /// </summary>
        public override string ToString()
        {
            return $"GraphicsBufferDescriptor({Target}, {UsageFlags}, {Stride}, {Length})";
        }

        /// <summary>
        /// Compares this descriptor to another descriptor for equality.
        /// </summary>
        public static bool operator ==(GraphicsBufferDescriptor lhs, GraphicsBufferDescriptor rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Compares this descriptor to another descriptor for inequality.
        /// </summary>
        public static bool operator !=(GraphicsBufferDescriptor lhs, GraphicsBufferDescriptor rhs)
        {
            return !lhs.Equals(rhs);
        }
    }
}
