using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    // 6-component representation of a (infinite length) line in 3D space
    internal struct Line
    {
        // for the line to be valid, dot(m, t) == 0
        public float3 M;
        public float3 T;

        public static Line LineOfPlaneIntersectingPlane(float4 a, float4 b)
        {
            // planes do not need to have a unit length normal
            return new Line {
                M = a.w*b.xyz - b.w*a.xyz,
                T = math.cross(a.xyz, b.xyz),
            };
        }

        public static float4 PlaneContainingLineAndPoint(Line a, float3 b)
        {
            // the resulting plane will not have a unit length normal (and the normal will be approximately zero when no plane exists)
            return new float4(a.M + math.cross(a.T, b), -math.dot(a.M, b));
        }

        public static float4 PlaneContainingLineWithNormalPerpendicularToVector(Line a, float3 b)
        {
            // the resulting plane will not have a unit length normal (and the normal will be approximately zero when no plane exists)
            return new float4(math.cross(a.T, b), -math.dot(a.M, b));
        }
    }

    internal enum FrustumIntersectResult : byte
    {
        Outside,
        Inside,
        Partial
    }

    internal struct FrustumSIMDPacket
    {
        public float4 Nx;
        public float4 Ny;
        public float4 Nz;
        public float4 D;
        public float4 AbsNx;
        public float4 AbsNy;
        public float4 AbsNz;

        public FrustumSIMDPacket(ReadOnlySpan<Plane> planes, int offset, int limit)
        {
            Plane p0 = planes[math.min(offset + 0, limit)];
            Plane p1 = planes[math.min(offset + 1, limit)];
            Plane p2 = planes[math.min(offset + 2, limit)];
            Plane p3 = planes[math.min(offset + 3, limit)];
            Nx = new float4(p0.normal.x, p1.normal.x, p2.normal.x, p3.normal.x);
            Ny = new float4(p0.normal.y, p1.normal.y, p2.normal.y, p3.normal.y);
            Nz = new float4(p0.normal.z, p1.normal.z, p2.normal.z, p3.normal.z);
            D = new float4(p0.distance, p1.distance, p2.distance, p3.distance);
            AbsNx = math.abs(Nx);
            AbsNy = math.abs(Ny);
            AbsNz = math.abs(Nz);
        }

        public FrustumSIMDPacket(NativeArray<Plane> planes, int offset, int limit)
        {
            Plane p0 = planes[Mathf.Min(offset + 0, limit)];
            Plane p1 = planes[Mathf.Min(offset + 1, limit)];
            Plane p2 = planes[Mathf.Min(offset + 2, limit)];
            Plane p3 = planes[Mathf.Min(offset + 3, limit)];
            Nx = new float4(p0.normal.x, p1.normal.x, p2.normal.x, p3.normal.x);
            Ny = new float4(p0.normal.y, p1.normal.y, p2.normal.y, p3.normal.y);
            Nz = new float4(p0.normal.z, p1.normal.z, p2.normal.z, p3.normal.z);
            D = new float4(p0.distance, p1.distance, p2.distance, p3.distance);
            AbsNx = math.abs(Nx);
            AbsNy = math.abs(Ny);
            AbsNz = math.abs(Nz);
        }

        public float4x4 AsGPUPacket()
        {
            float4x4 packet;
            packet.c0 = Nx;
            packet.c1 = Ny;
            packet.c2 = Nz;
            packet.c3 = D;
            return packet;
        }
    }

    internal struct ReceiverPlanes
    {
        public NativeList<Plane> Planes;
        public int LightFacingPlaneCount;

        private static bool IsSignBitSet(float x)
        {
            uint i = math.asuint(x);
            return (i >> 31) != 0;
        }

        public NativeArray<Plane> LightFacingFrustumPlaneSubArray()
        {
            return Planes.AsArray().GetSubArray(0, LightFacingPlaneCount);
        }

        public NativeArray<Plane> SilhouettePlaneSubArray()
        {
            return Planes.AsArray().GetSubArray(LightFacingPlaneCount, Planes.Length - LightFacingPlaneCount);
        }

        public static ReceiverPlanes CreateEmptyForTesting(Allocator allocator)
        {
            return new ReceiverPlanes
            {
                Planes = new NativeList<Plane>(allocator),
                LightFacingPlaneCount = 0,
            };
        }

        public JobHandle Dispose(JobHandle job)
        {
            return Planes.Dispose(job);
        }

        public static ReceiverPlanes Create(in BatchCullingContext cc, Allocator allocator)
        {
            ReceiverPlanes result = new ReceiverPlanes
            {
                Planes = new NativeList<Plane>(allocator),
                LightFacingPlaneCount = 0,
            };

            if (cc.viewType == BatchCullingViewType.Light && cc.receiverPlaneCount != 0)
            {
                bool isLightOrthographic = false;
                if (cc.cullingSplits.Length > 0)
                {
                    Matrix4x4 m = cc.cullingSplits[0].cullingMatrix;
                    isLightOrthographic = m[15] == 1.0f && m[11] == 0.0f && m[7] == 0.0f && m[3] == 0.0f;
                }
                if (isLightOrthographic)
                {
                    Vector3 lightDir = -cc.localToWorldMatrix.GetColumn(2);

                    // cache result for each plane, add planes facing towards the light
                    int planeSignBits = 0;
                    for (int i = 0; i < cc.receiverPlaneCount; ++i)
                    {
                        Plane plane = cc.cullingPlanes[cc.receiverPlaneOffset + i];
                        float facingTerm = Vector3.Dot(plane.normal, lightDir);
                        if (IsSignBitSet(facingTerm))
                            planeSignBits |= (1 << i);
                        else
                            result.Planes.Add(plane);
                    }
                    result.LightFacingPlaneCount = result.Planes.Length;

                    // assume ordering +/-x, +/-y, +/-z for frustum planes, test pairs for silhouette edges
                    if (cc.receiverPlaneCount == 6)
                    {
                        for (int i = 0; i < cc.receiverPlaneCount; ++i)
                        {
                            for (int j = i + 1; j < cc.receiverPlaneCount; ++j)
                            {
                                // skip pairs that are from the same frustum axis (i.e. both xs, both ys or both zs)
                                if ((i / 2) == (j / 2))
                                    continue;

                                // silhouette edges occur when the planes have opposing signs
                                int signCheck = ((planeSignBits >> i) ^ (planeSignBits >> j)) & 1;
                                if (signCheck == 0)
                                    continue;

                                // process in consistent order for consistent plane normal in the result
                                (int indexA, int indexB) = (((planeSignBits >> i) & 1) == 0) ? (i, j) : (j, i);
                                Plane planeA = cc.cullingPlanes[cc.receiverPlaneOffset + indexA];
                                Plane planeB = cc.cullingPlanes[cc.receiverPlaneOffset + indexB];

                                // construct a plane that contains the light origin and this silhouette edge
                                float4 planeEqA = new float4(planeA.normal, planeA.distance);
                                float4 planeEqB = new float4(planeB.normal, planeB.distance);
                                Line silhouetteEdge = Line.LineOfPlaneIntersectingPlane(planeEqA, planeEqB);
                                float4 silhouettePlaneEq = Line.PlaneContainingLineWithNormalPerpendicularToVector(silhouetteEdge, lightDir);

                                // try to normalize
                                silhouettePlaneEq = silhouettePlaneEq / math.length(silhouettePlaneEq.xyz);
                                if (!math.any(math.isnan(silhouettePlaneEq)))
                                     result.Planes.Add(new Plane(silhouettePlaneEq.xyz, silhouettePlaneEq.w));
                            }
                        }
                    }
                }
                else
                {
                    Vector3 lightPos = cc.localToWorldMatrix.GetPosition();

                    // cache result for each plane, add planes facing towards the light
                    int planeSignBits = 0;
                    for (int i = 0; i < cc.receiverPlaneCount; ++i)
                    {
                        Plane plane = cc.cullingPlanes[cc.receiverPlaneOffset + i];
                        float distance = plane.GetDistanceToPoint(lightPos);
                        if (IsSignBitSet(distance))
                            planeSignBits |= (1 << i);
                        else
                            result.Planes.Add(plane);
                    }
                    result.LightFacingPlaneCount = result.Planes.Length;

                    // assume ordering +/-x, +/-y, +/-z for frustum planes, test pairs for silhouette edges
                    if (cc.receiverPlaneCount == 6)
                    {
                        for (int i = 0; i < cc.receiverPlaneCount; ++i)
                        {
                            for (int j = i + 1; j < cc.receiverPlaneCount; ++j)
                            {
                                // skip pairs that are from the same frustum axis (i.e. both xs, both ys or both zs)
                                if ((i / 2) == (j / 2))
                                    continue;

                                // silhouette edges occur when the planes have opposing signs
                                int signCheck = ((planeSignBits >> i) ^ (planeSignBits >> j)) & 1;
                                if (signCheck == 0)
                                    continue;

                                // process in consistent order for consistent plane normal in the result
                                (int indexA, int indexB) = (((planeSignBits >> i) & 1) == 0) ? (i, j) : (j, i);
                                Plane planeA = cc.cullingPlanes[cc.receiverPlaneOffset + indexA];
                                Plane planeB = cc.cullingPlanes[cc.receiverPlaneOffset + indexB];

                                // construct a plane that contains the light origin and this silhouette edge
                                float4 planeEqA = new float4(planeA.normal, planeA.distance);
                                float4 planeEqB = new float4(planeB.normal, planeB.distance);
                                Line silhouetteEdge = Line.LineOfPlaneIntersectingPlane(planeEqA, planeEqB);
                                float4 silhouettePlaneEq = Line.PlaneContainingLineAndPoint(silhouetteEdge, lightPos);

                                // try to normalize
                                silhouettePlaneEq = silhouettePlaneEq / math.length(silhouettePlaneEq.xyz);
                                if (!math.any(math.isnan(silhouettePlaneEq)))
                                     result.Planes.Add(new Plane(silhouettePlaneEq.xyz, silhouettePlaneEq.w));
                            }
                        }
                    }
                }
            }

            return result;
        }
    }

    internal struct FrustumPlaneCuller
    {
        public struct SplitInfo
        {
            public int PlaneCount;
            public int PacketCount;
        }

        public NativeList<Plane> Planes;
        public NativeList<FrustumSIMDPacket> PlanePackets;
        public NativeList<SplitInfo> SplitInfos;

        public JobHandle Dispose(JobHandle job)
        {
            job = Planes.Dispose(job);
            job = PlanePackets.Dispose(job);
            job = SplitInfos.Dispose(job);
            return job;
        }

        public static FrustumPlaneCuller Create(in BatchCullingContext cc, NativeArray<Plane> receiverPlanes, in ReceiverSphereCuller receiverSphereCuller, Allocator allocator)
        {
            int splitCount = cc.cullingSplits.Length;

            int totalPacketCount = 0;
            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                int planeCount = receiverPlanes.Length + cc.cullingSplits[splitIndex].cullingPlaneCount;
                totalPacketCount += (planeCount + 3)/4;
            }

            FrustumPlaneCuller result = new FrustumPlaneCuller
            {
                Planes = new NativeList<Plane>(cc.cullingPlanes.Length + receiverPlanes.Length, allocator),
                PlanePackets = new NativeList<FrustumSIMDPacket>(totalPacketCount, allocator),
                SplitInfos = new NativeList<SplitInfo>(splitCount, allocator),
            };

            result.PlanePackets.ResizeUninitialized(totalPacketCount);
            result.SplitInfos.ResizeUninitialized(splitCount);

            int packetBase = 0;
            using NativeList<Plane> tmpPlanes = new NativeList<Plane>(cc.cullingPlanes.Length + receiverPlanes.Length, allocator);

            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                CullingSplit split = cc.cullingSplits[splitIndex];

                tmpPlanes.Clear();

                // use all culling planes
                for (int i = 0; i < split.cullingPlaneCount; ++i)
                    tmpPlanes.Add(cc.cullingPlanes[split.cullingPlaneOffset + i]);

                // conditionally use receiver planes
                if (receiverSphereCuller.UseReceiverPlanes())
                    tmpPlanes.AddRange(receiverPlanes);

                int packetCount = (tmpPlanes.Length + 3)/4;
                result.SplitInfos[splitIndex] = new SplitInfo
                {
                    PlaneCount = tmpPlanes.Length,
                    PacketCount = packetCount,
                };

                result.Planes.AddRange(tmpPlanes.AsArray());
                for (int i = 0; i < packetCount; ++i)
                    result.PlanePackets[packetBase + i] = new FrustumSIMDPacket(tmpPlanes.AsArray(), 4*i, tmpPlanes.Length - 1);

                packetBase += packetCount;
            }

            return result;
        }

        public static uint ComputeSplitVisibilityMask(NativeArray<FrustumSIMDPacket> planePackets, NativeArray<SplitInfo> splitInfos, AABB aabb)
        {
            float4 cx = aabb.Center.xxxx;
            float4 cy = aabb.Center.yyyy;
            float4 cz = aabb.Center.zzzz;

            float4 ex = aabb.Extent.xxxx;
            float4 ey = aabb.Extent.yyyy;
            float4 ez = aabb.Extent.zzzz;

            int packetBase = 0;
            int splitCount = splitInfos.Length;
            uint splitVisibilityMask = 0;

            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                SplitInfo splitInfo = splitInfos[splitIndex];
                bool4 isCulled = new bool4(false);

                for (int i = 0; i < splitInfo.PacketCount; ++i)
                {
                    FrustumSIMDPacket packet = planePackets[packetBase + i];
                    float4 distances = packet.Nx * cx + packet.Ny * cy + packet.Nz * cz + packet.D;
                    float4 radii = packet.AbsNx * ex + packet.AbsNy * ey + packet.AbsNz * ez;
                    isCulled |= (distances + radii < float4.zero);
                }

                bool overlaps = !math.any(isCulled);
                if (overlaps)
                    splitVisibilityMask |= 1u << splitIndex;

                packetBase += splitInfo.PacketCount;
            }

            return splitVisibilityMask;
        }
    }

    internal struct ReceiverSphereCuller
    {
        public struct SplitInfo
        {
            public float4 ReceiverSphereLightSpace;
            public float CascadeBlendCullingFactor;
        }

        public NativeList<SplitInfo> SplitInfos;
        public float3x3 WorldToLightSpaceRotation;

        public static ReceiverSphereCuller CreateEmptyForTesting(Allocator allocator)
        {
            return new ReceiverSphereCuller
            {
                SplitInfos = new NativeList<SplitInfo>(0, allocator),
                WorldToLightSpaceRotation = float3x3.identity,
            };
        }

        public JobHandle Dispose(JobHandle job)
        {
            return SplitInfos.Dispose(job);
        }

        public bool UseReceiverPlanes()
        {
            // only use receiver planes if there are no receiver spheres
            // (if spheres are present, then this is directional light cascades and Unity has already added receiver planes to the culling planes)
            return SplitInfos.Length == 0;
        }

        public static ReceiverSphereCuller Create(in BatchCullingContext cc, Allocator allocator)
        {
            int splitCount = cc.cullingSplits.Length;

            // only set up sphere culling when there are multiple splits and all splits have valid spheres
            bool allSpheresValid = (splitCount > 1);
            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                // ensure that NaN is not considered as valid
                if (!(cc.cullingSplits[splitIndex].sphereRadius > 0.0f))
                    allSpheresValid = false;
            }
            if (!allSpheresValid)
                splitCount = 0;

            float3x3 lightToWorldSpaceRotation = (float3x3)(float4x4)cc.localToWorldMatrix;
            ReceiverSphereCuller result = new ReceiverSphereCuller
            {
                SplitInfos = new NativeList<SplitInfo>(splitCount, allocator),
                WorldToLightSpaceRotation = math.transpose(lightToWorldSpaceRotation),
            };
            result.SplitInfos.ResizeUninitialized(splitCount);

            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                CullingSplit cullingSplit = cc.cullingSplits[splitIndex];

                float4 receiverSphereLightSpace = new float4(
                    math.mul(result.WorldToLightSpaceRotation, cullingSplit.sphereCenter),
                    cullingSplit.sphereRadius);

                result.SplitInfos[splitIndex] = new SplitInfo
                {
                    ReceiverSphereLightSpace = receiverSphereLightSpace,
                    CascadeBlendCullingFactor = cullingSplit.cascadeBlendCullingFactor,
                };
            }

            return result;
        }

        public static float DistanceUntilCylinderFullyCrossesPlane(
            float3 cylinderCenter,
            float3 cylinderDirection,
            float cylinderRadius,
            Plane plane)
        {
            const float cosEpsilon = 0.001f; // clamp the cosine of glancing angles

            // compute the distance until the center intersects the plane
            float cosTheta = math.max(math.abs(math.dot(plane.normal, cylinderDirection)), cosEpsilon);
            float heightAbovePlane = math.dot(plane.normal, cylinderCenter) + plane.distance;
            float centerDistanceToPlane = heightAbovePlane/cosTheta;

            // compute the additional distance until the edge of the cylinder intersects the plane
            float sinTheta = math.sqrt(math.max(1.0f - cosTheta*cosTheta, 0.0f));
            float edgeDistanceToPlane = cylinderRadius*sinTheta/cosTheta;

            return centerDistanceToPlane + edgeDistanceToPlane;
        }

        public static uint ComputeSplitVisibilityMask(
            NativeArray<Plane> lightFacingFrustumPlanes,
            NativeArray<SplitInfo> splitInfos,
            float3x3 worldToLightSpaceRotation,
            AABB aabb)
        {
            float3 casterCenterWorldSpace = aabb.Center.xyz;
            float3 casterCenterLightSpace = math.mul(worldToLightSpaceRotation, casterCenterWorldSpace);
            float casterRadius = math.length(aabb.Extent.xyz);

            // push the (light-facing) frustum planes back by the caster radius, then intersect with a line through the caster capsule center,
            // to compute the length of the shadow that will cover all possible receivers within the whole frustum (not just this split)
            float3 shadowDirection = math.transpose(worldToLightSpaceRotation).c2;
            float shadowLength = math.INFINITY;
            for (int i = 0; i < lightFacingFrustumPlanes.Length; ++i)
            {
                shadowLength = math.min(shadowLength, DistanceUntilCylinderFullyCrossesPlane(
                    casterCenterWorldSpace,
                    shadowDirection,
                    casterRadius,
                    lightFacingFrustumPlanes[i]));
            }
            shadowLength = math.max(shadowLength, 0.0f);

            uint splitVisibilityMask = 0;
            int splitCount = splitInfos.Length;
            for (int splitIndex = 0; splitIndex < splitCount; ++splitIndex)
            {
                SplitInfo splitInfo = splitInfos[splitIndex];
                float3 receiverCenterLightSpace = splitInfo.ReceiverSphereLightSpace.xyz;
                float receiverRadius = splitInfo.ReceiverSphereLightSpace.w;
                float3 receiverToCasterLightSpace = casterCenterLightSpace - receiverCenterLightSpace;

                // compute the light space z coordinate where the caster sphere and receiver sphere just intersect
                float sphereIntersectionMaxDistance = casterRadius + receiverRadius;
                float zSqAtSphereIntersection = math.lengthsq(sphereIntersectionMaxDistance) - math.lengthsq(receiverToCasterLightSpace.xy);

                // if this is negative, the spheres do not overlap as circles in the XY plane, so cull the caster
                if (zSqAtSphereIntersection < 0.0f)
                    continue;

                // if the caster is outside of the receiver sphere in the light direction, it cannot cast a shadow on it, so cull it
                if (receiverToCasterLightSpace.z > 0.0f && math.lengthsq(receiverToCasterLightSpace.z) > zSqAtSphereIntersection)
                    continue;

                // render the caster in this split
                splitVisibilityMask |= 1u << splitIndex;

                // culling assumes that shaders will always sample from the cascade with the lowest index,
                // so if the caster capsule is fully contained within the "core" sphere where only this split index is sampled,
                // then cull this caster from all the larger index splits (break from this loop)
                // (it is sufficient to test that only the capsule start and end spheres are within the "core" receiver sphere)
                float coreRadius = receiverRadius * splitInfo.CascadeBlendCullingFactor;
                float3 receiverToShadowEndLightSpace = receiverToCasterLightSpace + new float3(0.0f, 0.0f, shadowLength);
                float capsuleMaxDistance = coreRadius - casterRadius;
                float capsuleDistanceSq = math.max(math.lengthsq(receiverToCasterLightSpace), math.lengthsq(receiverToShadowEndLightSpace));
                if (capsuleMaxDistance > 0.0f && capsuleDistanceSq < math.lengthsq(capsuleMaxDistance))
                    break;
            }

            return splitVisibilityMask;
        }
    }
}
