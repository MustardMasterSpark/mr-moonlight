// Copyright © Magnetic Arcade. All Rights Reserved.

using System;

namespace MA.Flora
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class GenerateBurstMonoInteropAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class BurstMonoInteropMethodAttribute : Attribute
    {
        public bool MakePublic;
        public BurstMonoInteropMethodAttribute(bool makePublic = false) => MakePublic = makePublic;
    }
}
