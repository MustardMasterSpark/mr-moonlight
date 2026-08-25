// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Burst;

[assembly: BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]

[assembly: InternalsVisibleTo("MA.Flora.Tools.Editor")]
[assembly: InternalsVisibleTo("MA.Flora.Tests.Editor")]
