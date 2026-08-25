// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_COMPUTE_UTILITY_INCLUDED
#define FLORA_COMPUTE_UTILITY_INCLUDED

//--------------------------------------------------------------------------------------------------
// Definitions
//--------------------------------------------------------------------------------------------------

#ifndef THREADING_BLOCK_SIZE
#error THREADING_BLOCK_SIZE must be defined as the flattened thread group size.
#define THREADING_BLOCK_SIZE 64
#endif

//--------------------------------------------------------------------------------------------------
// Includes
//--------------------------------------------------------------------------------------------------

#include "Packages/com.ma.flora/Runtime/Graphics/ComputeUtility.cs.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Threading.hlsl"

//--------------------------------------------------------------------------------------------------
// Thread Wrapping Utility Functions
//--------------------------------------------------------------------------------------------------
//
// Wrapping model:
//   • X wraps at WRAPPED_GROUP_STRIDE into Y.
//   • Y wraps at WRAPPED_GROUP_STRIDE into Z.
// Terminology:
//   • Linear group ID: 1D index of a thread *group*.
//   • Linear dispatch thread ID: 1D index of a *thread* across the whole dispatch.
//
// Notes:
//   • THREADING_BLOCK_SIZE is the flattened threads-per-group (SV_GroupIndex range).
//   • All ceil-divisions are expressed as (a + b - 1) / b.
//--------------------------------------------------------------------------------------------------

// Function: UnwrapLinearGroupID
// Summary:
//   Convert a wrapped 3D group ID (SV_GroupID) into a linear group ID.
// Details:
//   Uses the wrapping model described above.
// Parameters:
//   groupID (in): Wrapped group ID (x, y, z).
// Returns:
//   Linear group ID (0-based).
uint UnwrapLinearGroupID(uint3 groupID)
{
    return groupID.x + (groupID.z * WRAPPED_GROUP_STRIDE + groupID.y) * WRAPPED_GROUP_STRIDE;
}

// Function: UnwrapLinearGroupID
// Summary:
//   Overload that reads the wrapped group ID from a Threading::Group.
// Parameters:
//   group (in): Threading::Group containing 'groupID'.
// Returns:
//   Linear group ID (0-based).
uint UnwrapLinearGroupID(Threading::Group group)
{
    return UnwrapLinearGroupID(group.groupID);
}

// Function: UnwrapLinearDispatchThreadID
// Summary:
//   Compute the linear dispatch thread ID from a wrapped group ID and an intra-group thread index.
// Formula:
//   linearID = UnwrapLinearGroupID(groupID) * threadingBlockSize + groupThreadIndex
// Parameters:
//   groupID (in): Wrapped group ID (SV_GroupID).
//   groupThreadIndex (in): Thread index within the group (SV_GroupIndex).
//   threadingBlockSize (in): Flattened threads-per-group.
// Returns:
//   Linear dispatch thread ID (0-based).
uint UnwrapLinearDispatchThreadID(uint3 groupID, uint groupThreadIndex, uint threadingBlockSize)
{
    return UnwrapLinearGroupID(groupID) * threadingBlockSize + groupThreadIndex;
}

// Function: UnwrapLinearDispatchThreadID
// Summary:
//   Convenience overload that uses THREADING_BLOCK_SIZE.
// Parameters:
//   groupID (in): Wrapped group ID (SV_GroupID).
//   groupThreadIndex (in): Thread index within the group (SV_GroupIndex).
// Returns:
//   Linear dispatch thread ID (0-based).
uint UnwrapLinearDispatchThreadID(uint3 groupID, uint groupThreadIndex)
{
    return UnwrapLinearDispatchThreadID(groupID, groupThreadIndex, THREADING_BLOCK_SIZE);
}

// Function: UnwrapLinearDispatchThreadID
// Summary:
//   Convenience overload that reads both group ID and index from Threading::Group.
//   Uses THREADING_BLOCK_SIZE.
// Parameters:
//   group (in): Threading::Group containing 'groupID' and 'groupIndex'.
// Returns:
//   Linear dispatch thread ID (0-based).
uint UnwrapLinearDispatchThreadID(Threading::Group group)
{
    return UnwrapLinearDispatchThreadID(group.groupID, group.groupIndex);
}

// Function: WrapGroupCount
// Summary:
//   Wrap a 1D target group count into a (x, y, z) group count under per-dimension limits.
// Behavior:
//   • Start with (target, 1, 1).
//   • If x > dimensionLimitX:
//       y = ceilDiv(x, WRAPPED_GROUP_STRIDE); x = WRAPPED_GROUP_STRIDE.
//   • If y > dimensionLimitY:
//       z = ceilDiv(y, WRAPPED_GROUP_STRIDE); y = WRAPPED_GROUP_STRIDE.
// Parameters:
//   targetGroupCount (in): Desired 1D group count.
//   dimensionLimitX  (in): Max groups along X.
//   dimensionLimitY  (in): Max groups along Y.
// Returns:
//   Wrapped group count as uint3(x, y, z).
uint3 WrapGroupCount(uint targetGroupCount, uint dimensionLimitX, uint dimensionLimitY)
{
    uint3 groupCount = uint3(targetGroupCount, 1, 1);

    if (groupCount.x > dimensionLimitX)
    {
        groupCount.y = (groupCount.x + WRAPPED_GROUP_STRIDE - 1) / WRAPPED_GROUP_STRIDE;
        groupCount.x = WRAPPED_GROUP_STRIDE;
    }

    if (groupCount.y > dimensionLimitY)
    {
        groupCount.z = (groupCount.y + WRAPPED_GROUP_STRIDE - 1) / WRAPPED_GROUP_STRIDE;
        groupCount.y = WRAPPED_GROUP_STRIDE;
    }

    return groupCount;
}

// Function: WrapDispatchCount
// Summary:
//   Wrap a 1D target *thread* count into a (x, y, z) *group* (dispatch) count under per-dimension limits.
// Steps:
//   • groups = ceilDiv(targetDispatchThreadCount, threadingBlockSize)
//   • return WrapGroupCount(groups, dimensionLimitX, dimensionLimitY)
// Parameters:
//   targetDispatchThreadCount (in): Desired total thread count (1D).
//   dimensionLimitX           (in): Max groups along X.
//   dimensionLimitY           (in): Max groups along Y.
//   threadingBlockSize        (in): Flattened threads-per-group.
// Returns:
//   Wrapped dispatch (group) count as uint3(x, y, z).
uint3 WrapDispatchCount(uint targetDispatchThreadCount, uint dimensionLimitX, uint dimensionLimitY, uint threadingBlockSize)
{
    return WrapGroupCount((targetDispatchThreadCount + threadingBlockSize - 1) / threadingBlockSize, dimensionLimitX, dimensionLimitY);
}

// Function: WrapDispatchCount
// Summary:
//   Convenience overload that uses THREADING_BLOCK_SIZE.
// Parameters:
//   targetDispatchThreadCount (in): Desired total thread count (1D).
//   dimensionLimitX           (in): Max groups along X.
//   dimensionLimitY           (in): Max groups along Y.
// Returns:
//   Wrapped dispatch (group) count as uint3(x, y, z).
uint3 WrapDispatchCount(uint targetDispatchThreadCount, uint dimensionLimitX, uint dimensionLimitY)
{
    return WrapDispatchCount(targetDispatchThreadCount, dimensionLimitX, dimensionLimitY, THREADING_BLOCK_SIZE);
}

#endif // FLORA_COMPUTE_SHADER_UTILITY_INCLUDED
