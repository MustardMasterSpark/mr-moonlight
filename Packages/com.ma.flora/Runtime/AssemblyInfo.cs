// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Burst;

[assembly: InternalsVisibleTo("Unity.Flora.CodeGen")]
[assembly: InternalsVisibleTo("MA.Flora.Tools")]
[assembly: InternalsVisibleTo("MA.Flora.Tools.Editor")]
[assembly: InternalsVisibleTo("MA.Flora.Editor")]
[assembly: InternalsVisibleTo("MA.Flora.Tests")]
[assembly: InternalsVisibleTo("MA.Flora.Tests.Editor")]
[assembly: InternalsVisibleTo("MA.Flora.PerformanceTests")]
[assembly: BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast)]
