// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using PrefabUtility = UnityEditor.PrefabUtility;
#endif

namespace MA.Flora
{
    public sealed partial class FloraSystem
    {
        #region Instance Management

        /// <summary>
        /// Creates a new instance, instancing the specified source object using a <see cref="float4x4"/> for world transform data.
        /// The supplied object is used as both the identity source and the render source.
        /// </summary>
        /// <returns>A <see cref="FloraInstanceHandle"/> representing the newly created instance.</returns>
        public FloraInstanceHandle CreateInstance(GameObject prefab, GameObject owner, float4x4 localToWorld)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            return m_NativeContext.InstanceManager.ValueRW.CreateInstance(prefab, owner, localToWorld);
        }

        /// <summary>
        /// Creates a new instance, instancing the specified source object using a <see cref="FloraInstanceTransform"/> for local transform data.
        /// The supplied object is used as both the identity source and the render source.
        /// </summary>
        /// <returns>A <see cref="FloraInstanceHandle"/> representing the newly created instance.</returns>
        public FloraInstanceHandle CreateInstance(GameObject prefab, Transform parent, FloraInstanceTransform localTransform)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            return m_NativeContext.InstanceManager.ValueRW.CreateInstance(prefab, parent, localTransform);
        }

        /// <summary>
        /// Creates a new instance, instancing the specified source object under a given parent transform
        /// at the specified local position, rotation, and scale.
        /// The supplied object is used as both the identity source and the render source.
        /// </summary>
        /// <returns>A <see cref="FloraInstanceHandle"/> representing the newly created instance.</returns>
        public FloraInstanceHandle CreateInstance(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            return m_NativeContext.InstanceManager.ValueRW.CreateInstance(prefab, parent, new FloraInstanceTransform(localPosition, localRotation, localScale));
        }

        /// <summary>
        /// Instantiates multiple instances at once, using the given prefab and an array of local transforms.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="owner">The owner GameObject of the instances.</param>
        /// <param name="instanceHandles">An array (output) of newly created handles.</param>
        /// <param name="localToWorldMatrices">An array of world matrices for each instance.</param>
        public void CreateInstances(GameObject prefab, GameObject owner, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<float4x4> localToWorldMatrices)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");
            if (instanceHandles.Length != localToWorldMatrices.Length)
                throw new ArgumentException("The length of instanceHandles and matrices must be the same.");

            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, owner, instanceHandles, localToWorldMatrices.Reinterpret<FloraLocalToWorld>());
        }

        /// <summary>
        /// Instantiates multiple instances at once, using the given prefab and an array of local transforms.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="owner">The owner GameObject of the instances.</param>
        /// <param name="instanceHandles">An array (output) of newly created handles.</param>
        /// <param name="transforms">An array of world transforms for each instance.</param>
        public void CreateInstances(GameObject prefab, GameObject owner, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> transforms)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");
            if (instanceHandles.Length != transforms.Length)
                throw new ArgumentException("The length of instanceHandles and transforms must be the same.");

            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, owner, instanceHandles, transforms);
        }

        /// <summary>
        /// Instantiates multiple instances at once, using the given prefab and an array of local transforms.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="parent">The parent transform for these instances.</param>
        /// <param name="instanceHandles">An array (output) of newly created handles.</param>
        /// <param name="localTransforms">An array of local transforms for each instance.</param>
        public void CreateInstances(GameObject prefab, Transform parent, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");
            if (instanceHandles.Length != localTransforms.Length)
                throw new ArgumentException("The length of instanceHandles and localTransforms must be the same.");

            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, parent, instanceHandles, localTransforms);
        }

        /// <summary>
        /// Instantiates an array of instances at once, using the given prefab and an array of world matrices.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="owner">The owner GameObject of the instances.</param>
        /// <param name="matrices">An array of world matrices for each instance.</param>
        /// <returns>A <see cref="NativeArray{FloraInstanceHandle}"/> containing the handles of the newly created instances.</returns>
        public NativeArray<FloraInstanceHandle> CreateInstances(GameObject prefab, GameObject owner, NativeArray<float4x4> matrices)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            var instanceHandles = new NativeArray<FloraInstanceHandle>(matrices.Length, Allocator.Persistent);
            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, owner, instanceHandles, matrices.Reinterpret<FloraLocalToWorld>());
            return instanceHandles;
        }

        /// <summary>
        /// Instantiates an array of instances at once, using the given prefab and an array of world transforms.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="owner">The owner GameObject of the instances.</param>
        /// <param name="transforms">An array of world transforms for each instance.</param>
        /// <returns> A <see cref="NativeArray{FloraInstanceHandle}"/> containing the handles of the newly created instances.</returns>
        public NativeArray<FloraInstanceHandle> CreateInstances(GameObject prefab, GameObject owner, NativeArray<FloraInstanceTransform> transforms)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            var instanceHandles = new NativeArray<FloraInstanceHandle>(transforms.Length, Allocator.Persistent);
            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, owner, instanceHandles, transforms);
            return instanceHandles;
        }

        /// <summary>
        /// Instantiates an array of instances at once, using the given prefab and an array of local transforms.
        /// </summary>
        /// <param name="prefab">The prefab used for all instances.</param>
        /// <param name="parent">The parent transform for these instances.</param>
        /// <param name="localTransforms">An array of local transforms for each instance.</param>
        /// <returns> A <see cref="NativeArray{FloraInstanceHandle}"/> containing the handles of the newly created instances.</returns>
        public NativeArray<FloraInstanceHandle> CreateInstances(GameObject prefab, Transform parent, NativeArray<FloraInstanceTransform> localTransforms)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

            var instanceHandles = new NativeArray<FloraInstanceHandle>(localTransforms.Length, Allocator.Persistent);
            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, parent, instanceHandles, localTransforms);
            return instanceHandles;
        }

        /// <summary>
        /// Instantiates a standard Unity prefab in the scene, adding a <see cref="FloraInstanceRenderer"/> component if none exists, to register it with Flora.
        /// </summary>
        /// <remarks>
        /// This method physically spawns a new <see cref="GameObject"/> in the scene as a child  of <paramref name="parent"/>.
        /// It's different from <see cref="CreateInstance(UnityEngine.GameObject,UnityEngine.Transform,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)"/>
        /// in that it creates a Unity object plus the required <see cref="FloraInstanceRenderer"/>.
        /// </remarks>
        /// <returns>The newly spawned <see cref="GameObject"/> instance.</returns>
        public FloraInstanceRenderer InstantiateInstanceRenderer(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab), $"{nameof(FloraSystem)}: Prefab cannot be null.");

#if UNITY_EDITOR
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
#else
            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
#endif
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;

            if (!instance.TryGetComponent(out FloraInstanceRenderer instanceRenderer))
            {
                instanceRenderer = instance.AddComponent<FloraInstanceRenderer>();
                instanceRenderer.Prefab = prefab;
            }

            return instanceRenderer;
        }

        /// <summary>
        /// Destroys a single instance, removing it from the system's data.
        /// </summary>
        /// <param name="instance">The handle of the instance to destroy.</param>
        public void DestroyInstance(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRW.Destroy(instance);

        /// <summary>
        /// Destroys multiple instances, removing them from the system's data.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to destroy.</param>
        public void DestroyInstances(NativeArray<FloraInstanceHandle> instanceHandles)
            => m_NativeContext.InstanceManager.ValueRW.Destroy(instanceHandles);

        /// <summary>
        /// Checks if a given <see cref="FloraInstanceHandle"/> still exists in Flora's internal data.
        /// </summary>
        /// <param name="instance">The handle to check.</param>
        /// <returns>True if the instance is present; otherwise, false.</returns>
        public bool InstanceExists(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.Exists(instance);

        /// <summary>
        /// Indicates whether a given instance is currently enabled (visible/renderable).
        /// </summary>
        /// <param name="instance">The handle to check.</param>
        /// <returns>True if enabled, otherwise false.</returns>
        public bool IsInstanceEnabled(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.IsEnabled(instance);

        /// <summary>
        /// Enables or disables the specified instance.
        /// </summary>
        /// <param name="instance">The handle of the instance to modify.</param>
        /// <param name="enabled">True to enable rendering, false to disable.</param>
        public void SetInstanceEnabled(FloraInstanceHandle instance, bool enabled)
            => m_NativeContext.InstanceManager.ValueRW.SetEnabled(instance, enabled);

        /// <summary>
        /// Enables or disables all instances in the given array.
        /// </summary>
        /// <param name="instances">An array of instance handles.</param>
        /// <param name="enabled">True to enable rendering, false to disable.</param>
        public void SetInstancesEnabled(NativeArray<FloraInstanceHandle> instances, bool enabled)
            => m_NativeContext.InstanceManager.ValueRW.SetEnabled(instances, enabled);

        /// <summary>
        /// Retrieves the identity source <see cref="GameObject"/> associated with a given instance.
        /// </summary>
        /// <remarks>
        /// This is the prefab when one exists, otherwise it is the scene object used as the instance identity source.
        /// </remarks>
        public GameObject GetInstanceIdentitySource(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetIdentitySourceObject(instance);

        /// <summary>
        /// Retrieves the live render source <see cref="GameObject"/> associated with a given instance.
        /// </summary>
        public GameObject GetInstanceRenderSource(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetRenderSourceObject(instance);

        /// <summary>
        /// Retrieves the owner scene <see cref="GameObject"/> that owns or manages a given instance.
        /// </summary>
        /// <remarks>
        /// For renderer-backed instances this is the live renderer object, for container instances it is the container owner,
        /// and for terrain instances it is the terrain <see cref="GameObject"/>.
        /// </remarks>
        public GameObject GetInstanceOwnerGameObject(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetSceneGameObject(instance);

        /// <summary>
        /// Retrieves the owner <see cref="Transform"/> for a given instance.
        /// </summary>
        /// <seealso cref="GetInstanceOwnerGameObject"/>
        /// <param name="instance">The handle of the instance to retrieve.</param>
        /// <returns>The owner <see cref="Transform"/> of this instance, or null if it does not exist.</returns>
        public Transform GetInstanceOwnerTransform(FloraInstanceHandle instance)
        {
            GameObject ownerObject = GetInstanceOwnerGameObject(instance);
            if (ownerObject != null)
                return ownerObject.transform;

            return null;
        }

        /// <summary>
        /// If the instance is owned by a terrain, retrieves the owning <see cref="Terrain"/> object.
        /// </summary>
        /// <seealso cref="GetInstanceOwnerGameObject"/>
        /// <param name="instance">The handle of the instance to retrieve.</param>
        /// <returns>The owner <see cref="Terrain"/> of this instance, or null if it does not exist.</returns>
        public Terrain GetInstanceOwnerTerrain(FloraInstanceHandle instance)
        {
            GameObject ownerObject = GetInstanceOwnerGameObject(instance);
            if (ownerObject != null && ownerObject.TryGetComponent(out Terrain terrain))
                return terrain;

            return null;
        }

        #endregion

        #region Terrain Instances

        /// <summary>
        /// Retrieves the index of a instance within its terrain's tree instances array.
        /// </summary>
        /// <param name="instance">The handle of the instance to retrieve the index for.</param>
        /// <returns>
        /// The index of the instance in the terrain's tree instances array,
        /// or -1 if the instance does not belong to a terrain or is not a tree instance.
        /// </returns>
        public int GetInstanceTerrainTreeIndex(FloraInstanceHandle instance)
        {
            var treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instance);
            if (treeInTerrain.TerrainEntity.IsValid())
                return treeInTerrain.IndexInTreeInstances;
            else
                return -1;
        }

        /// <summary>
        /// Retrieves the handles of all tree instances in a given terrain, sorted by their index in the terrain's tree array.
        /// </summary>
        /// <param name="terrain">The terrain to query for tree instances.</param>
        /// <param name="allocator">The allocator to use for the returned array.</param>
        /// <returns>A <see cref="NativeArray{T}"/> of <see cref="FloraInstanceHandle"/>s representing the tree instances in the terrain.</returns>
        public NativeArray<FloraInstanceHandle> GetTreeInstanceHandles(Terrain terrain, Allocator allocator)
            => m_NativeContext.TerrainManager.ValueRO.GetTreeInstanceHandles(terrain, allocator);

        /// <summary>
        /// Retrieves a specific tree instance handle from a terrain by its index.
        /// </summary>
        /// <param name="terrain">The terrain containing the tree instance.</param>
        /// <param name="treeIndex">The index of the instance in the terrain's tree array.</param>
        /// <returns>A <see cref="FloraInstanceHandle"/> representing the tree instance.</returns>
        public FloraInstanceHandle GetTreeInstanceHandle(Terrain terrain, int treeIndex)
            => m_NativeContext.TerrainManager.ValueRO.GetTreeInstanceHandle(terrain, treeIndex);

        #endregion

        #region Per-Instance Data

        /// <summary>
        /// Retrieves the variation color associated with a given instance.
        /// This color can be used for per-instance color variation in shaders.
        /// </summary>
        /// <remarks>
        /// Requires the <see cref="FloraAdditionalPerInstanceData.VariationColor"/> flag to be set on a prefab's <see cref="FloraAdditionalRendererSettings"/>.
        /// </remarks>
        /// <param name="instance">The handle of the instance to query.</param>
        /// <returns>The instance's variation color as a float4.</returns>
        public float4 GetInstanceVariationColor(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetVariationColor(instance);

        /// <summary>
        /// Sets the variation color for a given instance.
        /// </summary>
        /// <remarks>
        /// Requires the <see cref="FloraAdditionalPerInstanceData.VariationColor"/> flag to be set on a prefab's <see cref="FloraAdditionalRendererSettings"/>.
        /// </remarks>
        /// <param name="instance">The handle of the instance to modify.</param>
        /// <param name="color">The new variation color as a float4.</param>
        public void SetInstanceVariationColor(FloraInstanceHandle instance, float4 color)
            => m_NativeContext.InstanceManager.ValueRW.SetVariationColor(instance, color);

        /// <summary>
        /// Sets the variation colors for multiple instances at once.
        /// </summary>
        /// <remarks>
        /// Requires the <see cref="FloraAdditionalPerInstanceData.VariationColor"/> flag to be set on a prefab's <see cref="FloraAdditionalRendererSettings"/>.
        /// </remarks>
        /// <param name="instances">An array of instance handles to modify.</param>
        /// <param name="colors">An array of float4 colors corresponding to each instance.</param>
        public void SetInstanceVariationColors(NativeArray<FloraInstanceHandle> instances, NativeArray<float4> colors)
            => m_NativeContext.InstanceManager.ValueRW.SetVariationColors(instances, colors);

        #endregion

        #region Instance Transforms

        /// <summary>
        /// Retrieves the world-space position of a single instance.
        /// </summary>
        /// <param name="instance">The instance handle.</param>
        /// <returns>A float3 position in world coordinates.</returns>
        public float3 GetInstancePosition(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetLocalToWorld(instance).Position;

        /// <summary>
        /// Retrieves the local-to-world <see cref="FloraLocalToWorld"/> matrix for a single instance.
        /// </summary>
        /// <param name="instance">The instance handle.</param>
        /// <returns>>A <see cref="FloraLocalToWorld"/> matrix representing the instance's transform.</returns>
        public FloraLocalToWorld GetInstanceLocalToWorld(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetLocalToWorld(instance);

        /// <summary>
        /// Retrieves the world-space transform matrix for a single instance.
        /// </summary>
        /// <param name="instance">The instance handle.</param>
        /// <returns>>A <see cref="float4x4"/> matrix representing the instance's world transform.</returns>
        public float4x4 GetInstanceLocalToWorldMatrix(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetLocalToWorld(instance);

        /// <summary>
        /// Retrieves the world-space transform of a single instance.
        /// </summary>
        /// <param name="instance">The instance handle.</param>
        /// <returns>A <see cref="FloraInstanceTransform"/> representing the instance's world transform.</returns>
        public FloraInstanceTransform GetInstanceWorldTransform(FloraInstanceHandle instance)
            => FloraInstanceTransform.FromMatrix(m_NativeContext.InstanceManager.ValueRO.GetLocalToWorld(instance));

        /// <summary>
        /// Retrieves the local-to-world <see cref="FloraLocalToWorld"/> matrices of multiple instances.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to retrieve.</param>
        /// <param name="allocator">The allocator for the returned array.</param>
        /// <returns>A <see cref="NativeArray{FloraLocalToWorld}"/> containing the world transforms of the instances.</returns>
        public NativeArray<FloraLocalToWorld> GetInstanceLocalToWorlds(NativeArray<FloraInstanceHandle> instanceHandles, Allocator allocator)
            => m_NativeContext.InstanceManager.ValueRO.GetLocalToWorlds(instanceHandles, allocator);

        /// <summary>
        /// Retrieves the local-to-world <see cref="float4x4"/> matrices of multiple instances.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to retrieve.</param>
        /// <param name="allocator">The allocator for the returned array.</param>
        /// <returns>A <see cref="NativeArray{float4x4}"/> containing the world matrices of the instances.</returns>
        public NativeArray<float4x4> GetInstanceLocalToWorldMatrices(NativeArray<FloraInstanceHandle> instanceHandles, Allocator allocator)
            => GetInstanceLocalToWorlds(instanceHandles, allocator).Reinterpret<float4x4>();

        /// <summary>
        /// Retrieves the world-space <see cref="FloraInstanceTransform"/>s of multiple instances.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to retrieve.</param>
        /// <param name="allocator">The allocator for the returned array.</param>
        /// <returns>A <see cref="NativeArray{FloraInstanceTransform}"/> containing the world transforms of the instances.</returns>
        /// <remarks>
        /// Slower than <see cref="GetInstanceLocalToWorlds"/> as it re-constructs the <see cref="FloraInstanceTransform"/> for each instance.
        /// </remarks>
        public NativeArray<FloraInstanceTransform> GetInstanceWorldTransforms(NativeArray<FloraInstanceHandle> instanceHandles, Allocator allocator)
            => m_NativeContext.InstanceManager.ValueRO.GetWorldTransforms(instanceHandles, allocator);

        /// <summary>
        /// Retrieves the world-space positions of multiple instances.
        /// </summary>
        /// <param name="instanceHandles">Handles for the instances.</param>
        /// <param name="allocator">Allocator for the returned array.</param>
        /// <returns>A <see cref="NativeArray{float3}"/> of world positions.</returns>
        public NativeArray<float3> GetInstancePositions(NativeArray<FloraInstanceHandle> instanceHandles, Allocator allocator)
            => m_NativeContext.InstanceManager.ValueRO.GetPositions(instanceHandles, allocator);

        /// <summary>
        /// Sets the <see cref="FloraLocalToWorld"/> matrix for a single instance.
        /// </summary>
        /// <param name="instance">The instance handle to update.</param>
        /// <param name="localToWorld">The new local-to-world matrix for the instance.</param>
        public void UpdateInstanceLocalToWorld(FloraInstanceHandle instance, FloraLocalToWorld localToWorld)
            => m_NativeContext.InstanceManager.ValueRW.UpdateLocalToWorld(instance, localToWorld);

        /// <summary>
        /// Sets the local-to-world <see cref="float4x4"/> matrix for a single instance.
        /// </summary>
        /// <param name="instance">The instance handle to update.</param>
        /// <param name="localToWorldMatrix">The new local-to-world matrix for the instance.</param>
        public void UpdateInstanceLocalToWorldMatrix(FloraInstanceHandle instance, float4x4 localToWorldMatrix)
            => UpdateInstanceLocalToWorld(instance, localToWorldMatrix);

        /// <summary>
        /// Sets the local transform of a single instance (relative to a parent).
        /// </summary>
        /// <param name="parent">The parent transform that provides the local-to-world reference frame.</param>
        /// <param name="instance">The instance handle to update.</param>
        /// <param name="localInstanceTransform">The new local transform for the instance.</param>
        public void UpdateInstanceLocalTransform(Transform parent, FloraInstanceHandle instance, FloraInstanceTransform localInstanceTransform)
            => UpdateInstanceLocalToWorld(instance, ((FloraLocalToWorld)parent.localToWorldMatrix).Transform(localInstanceTransform));

        /// <summary>
        /// Sets the transform of a single instance in world space.
        /// </summary>
        /// <param name="instance">The instance handle to update.</param>
        /// <param name="worldTransform">The new world transform for the instance.</param>
        public void UpdateInstanceWorldTransform(FloraInstanceHandle instance, FloraInstanceTransform worldTransform)
            => UpdateInstanceLocalToWorld(instance, worldTransform.ToLocalToWorld());

        /// <summary>
        /// Updates instances' world transforms using <see cref="FloraLocalToWorld"/> matrices.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="localToWorlds">The new local-to-world matrices for each instance.</param>
        public void UpdateInstanceLocalToWorlds(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraLocalToWorld> localToWorlds)
        {
            if (instanceHandles.Length != localToWorlds.Length)
                throw new ArgumentException("The length of instanceHandles and localToWorldMatrices must be the same.");

            m_NativeContext.InstanceManager.ValueRW.UpdateLocalToWorlds(instanceHandles, localToWorlds);
        }

        /// <summary>
        /// Schedules a job to update instances' world transforms using a <see cref="FloraLocalToWorld"/> array.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="localToWorlds">The new local-to-world matrices for each instance.</param>
        /// <param name="dependsOn">Any dependent job handles.</param>
        /// <returns>A <see cref="JobHandle"/> representing the scheduled job and internal dependencies, which will be completed before rendering.</returns>
        public JobHandle ScheduleUpdateInstanceLocalToWorlds(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraLocalToWorld> localToWorlds, JobHandle dependsOn = default)
        {
            if (instanceHandles.Length != localToWorlds.Length)
                throw new ArgumentException("The length of instanceHandles and localToWorldMatrices must be the same.");

            return m_NativeContext.InstanceManager.ValueRW.ScheduleUpdateLocalToWorlds(instanceHandles, localToWorlds, dependsOn);
        }

        /// <summary>
        /// Updates instances' local-to-world <see cref="float4x4"/> matrices.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="localToWorldMatrices">The new local-to-world matrices for each instance.</param>
        public void UpdateInstanceLocalToWorldMatrices(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<float4x4> localToWorldMatrices)
            => UpdateInstanceLocalToWorlds(instanceHandles, localToWorldMatrices.Reinterpret<FloraLocalToWorld>());

        /// <summary>
        /// Schedules a job to update instances' world transforms using a <see cref="float4x4"/> array.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="localToWorldMatrices">The new local-to-world matrices for each instance.</param>
        /// <param name="dependsOn">Any dependent job handles.</param>
        /// <returns>A <see cref="JobHandle"/> representing the scheduled job and internal dependencies, which will be completed before rendering.</returns>
        public JobHandle ScheduleUpdateInstanceLocalToWorldMatrices(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<float4x4> localToWorldMatrices, JobHandle dependsOn = default)
            => ScheduleUpdateInstanceLocalToWorlds(instanceHandles, localToWorldMatrices.Reinterpret<FloraLocalToWorld>(), dependsOn);

        /// <summary>
        /// Updates instances' world transforms.
        /// </summary>
        /// <param name="instanceHandles">Handles of the instances to update.</param>
        /// <param name="worldTransforms">The new world transforms for each instance.</param>
        public void UpdateInstanceWorldTransforms(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> worldTransforms)
        {
            if (instanceHandles.Length != worldTransforms.Length)
                throw new ArgumentException("Instance and transform arrays must have the same length.");

            m_NativeContext.InstanceManager.ValueRW.UpdateWorldTransforms(instanceHandles, worldTransforms);
        }

        /// <summary>
        /// Schedules a job to update instances' world transforms using a <see cref="FloraInstanceTransform"/> array.
        /// </summary>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="worldTransforms">The new world transforms for each instance.</param>
        /// <param name="dependsOn">Any dependent job handles.</param>
        /// <returns>A <see cref="JobHandle"/> representing the scheduled job and internal dependencies, which will be completed before rendering.</returns>
        public JobHandle ScheduleUpdateInstanceWorldTransforms(NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> worldTransforms, JobHandle dependsOn = default)
        {
            if (instanceHandles.Length != worldTransforms.Length)
                throw new ArgumentException("Instance and transform arrays must have the same length.");

            return m_NativeContext.InstanceManager.ValueRW.ScheduleUpdateWorldTransforms(instanceHandles, worldTransforms, dependsOn);
        }

        /// <summary>
        /// Updates instances' local transforms (relative to a parent).
        /// </summary>
        /// <param name="parent">The parent transform that provides the local-to-world reference frame.</param>
        /// <param name="instanceHandles">Handles of the instances to update.</param>
        /// <param name="localTransforms">The local transforms for each instance, to be applied relative to the parent.</param>
        public void UpdateInstanceLocalTransforms(Transform parent, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            if (instanceHandles.Length != localTransforms.Length)
                throw new ArgumentException("Instance and transform arrays must have the same length.");

            m_NativeContext.InstanceManager.ValueRW.UpdateLocalTransforms(parent.localToWorldMatrix, instanceHandles, localTransforms);
        }

        /// <summary>
        /// Schedules a job to update instances' local transforms (relative to a parent).
        /// </summary>
        /// <param name="parent">The parent transform that provides the local-to-world reference frame.</param>
        /// <param name="instanceHandles">The handles of the instances to update.</param>
        /// <param name="localTransforms">The local transforms for each instance, to be applied relative to the parent.</param>
        /// <param name="dependsOn">Any dependent job handles.</param>
        /// <returns>A <see cref="JobHandle"/> representing the scheduled job and internal dependencies, which will be completed before rendering.</returns>
        public JobHandle ScheduleUpdateInstanceLocalTransforms(Transform parent, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> localTransforms, JobHandle dependsOn = default)
        {
            if (instanceHandles.Length != localTransforms.Length)
                throw new ArgumentException("Instance and transform arrays must have the same length.");

            return m_NativeContext.InstanceManager.ValueRW.ScheduleUpdateLocalTransforms(parent.localToWorldMatrix, instanceHandles, localTransforms, dependsOn);
        }

        #endregion

        #region Instance Bounds

        /// <summary>
        /// Gets the world-space bounding box of a single instance.
        /// </summary>
        /// <param name="instance">The instance handle.</param>
        /// <returns>A <see cref="Bounds"/> object for the instance.</returns>
        public Bounds GetInstanceBounds(FloraInstanceHandle instance)
            => m_NativeContext.InstanceManager.ValueRO.GetBounds(instance);

        /// <summary>
        /// Gets the world-space bounding boxes of multiple instances.
        /// </summary>
        /// <param name="instanceHandles">An array of instance handles.</param>
        /// <param name="allocator">Allocator for the returned array.</param>
        /// <returns>A <see cref="NativeArray{Bounds}"/> containing one bounding box per instance.</returns>
        public NativeArray<Bounds> GetInstanceBounds(NativeArray<FloraInstanceHandle> instanceHandles, Allocator allocator)
            => m_NativeContext.InstanceManager.ValueRO.GetBounds(instanceHandles, allocator);

        /// <summary>
        /// Calculates a combined bounding box encapsulating all specified instances.
        /// </summary>
        /// <param name="instanceHandles">An array of instance handles.</param>
        /// <returns>A <see cref="Bounds"/> encompassing all those instances.</returns>
        public Bounds CalculateInstanceBounds(NativeArray<FloraInstanceHandle> instanceHandles)
            => m_NativeContext.InstanceManager.ValueRO.CalculateInstanceBounds(instanceHandles, float4x4.identity, false);

        /// <summary>
        /// Calculates a combined bounding box encapsulating all specified instances.
        /// </summary>
        /// <param name="instanceHandles">An array of instance handles.</param>
        /// <param name="inSpace">A transform matrix defining the space in which to calculate the bounds.</param>
        /// <returns>A <see cref="Bounds"/> encompassing all those instances.</returns>
        public Bounds CalculateInstanceBounds(NativeArray<FloraInstanceHandle> instanceHandles, float4x4 inSpace)
            => m_NativeContext.InstanceManager.ValueRO.CalculateInstanceBounds(instanceHandles, inSpace, true);

        #endregion

        #region Find Instances

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere.
        /// </summary>
        /// <param name="sphere">The world-space bounding sphere to check.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles whose render bounds intersect the sphere.</returns>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingSphere(BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereWithBurst(sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere.
        /// </summary>
        /// <param name="sphere">The world-space bounding sphere to check.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstancesIntersectingSphere(BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereNonAllocWithBurst(sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="sphere">The bounding sphere to check against.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles matching the criteria.</returns>
        /// <seealso cref="FloraInstanceFilter"/>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingWithBurst(filter, sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="sphere">The bounding sphere to check against.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        /// <seealso cref="FloraInstanceFilter"/>
        public void FindInstancesIntersectingSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingNonAllocWithBurst(filter, sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="sphere">The bounding sphere.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of matching instances.</returns>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingWithBurst(prefabGameObjectIDs, sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounding sphere and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="sphere">The bounding sphere.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstancesIntersectingSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingNonAllocWithBurst(prefabGameObjectIDs, sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere.
        /// </summary>
        /// <param name="sphere">The world-space bounding sphere to check.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of instance handles whose transform origins are inside the sphere.</returns>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphere(BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereWithBurst(sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere.
        /// </summary>
        /// <param name="sphere">The world-space bounding sphere to check.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstanceOriginsWithinSphere(BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereNonAllocWithBurst(sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="sphere">The bounding sphere to check against.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles matching the criteria.</returns>
        /// <seealso cref="FloraInstanceFilter"/>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereMatchingWithBurst(filter, sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="sphere">The bounding sphere to check against.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        /// <seealso cref="FloraInstanceFilter"/>
        public void FindInstanceOriginsWithinSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereMatchingNonAllocWithBurst(filter, sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="sphere">The bounding sphere.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of matching instances.</returns>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereMatchingWithBurst(prefabGameObjectIDs, sphere, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounding sphere and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="sphere">The bounding sphere.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstanceOriginsWithinSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinSphereMatchingNonAllocWithBurst(prefabGameObjectIDs, sphere, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume.
        /// </summary>
        /// <param name="bounds">The world-space bounds volume to check.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles whose render bounds intersect the bounds volume.</returns>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingBounds(Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxWithBurst(bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume.
        /// </summary>
        /// <param name="bounds">The world-space bounds volume to check.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstancesIntersectingBounds(Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxNonAllocWithBurst(bounds, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="bounds">The bounds volume to check against.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles matching the criteria.</returns>
        /// <seealso cref="FloraInstanceFilter"/>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingBoundsMatching(FloraInstanceFilter filter, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingWithBurst(filter, bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="bounds">The bounds volume to check against.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        /// <seealso cref="FloraInstanceFilter"/>
        public void FindInstancesIntersectingBoundsMatching(FloraInstanceFilter filter, Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingNonAllocWithBurst(filter, bounds, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="bounds">The bounds volume.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of matching instances.</returns>
        public NativeList<FloraInstanceHandle> FindInstancesIntersectingBoundsMatching(NativeArray<EntityId> prefabGameObjectIDs, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingWithBurst(prefabGameObjectIDs, bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space render bounds intersect a specified bounds volume and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="bounds">The bounds volume.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstancesIntersectingBoundsMatching(NativeArray<EntityId> prefabGameObjectIDs, Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingNonAllocWithBurst(prefabGameObjectIDs, bounds, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume.
        /// </summary>
        /// <param name="bounds">The world-space bounds volume to check.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of instance handles whose transform origins are inside the bounds volume.</returns>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBounds(Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxWithBurst(bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume.
        /// </summary>
        /// <param name="bounds">The world-space bounds volume to check.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstanceOriginsWithinBounds(Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxNonAllocWithBurst(bounds, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="bounds">The bounds volume to check against.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{T}"/> of instance handles matching the criteria.</returns>
        /// <seealso cref="FloraInstanceFilter"/>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBoundsMatching(FloraInstanceFilter filter, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxMatchingWithBurst(filter, bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume and match a source or type filter.
        /// </summary>
        /// <param name="filter">The instance filter to apply.</param>
        /// <param name="bounds">The bounds volume to check against.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        /// <seealso cref="FloraInstanceFilter"/>
        public void FindInstanceOriginsWithinBoundsMatching(FloraInstanceFilter filter, Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxMatchingNonAllocWithBurst(filter, bounds, ref results);
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="bounds">The bounds volume.</param>
        /// <param name="allocator">Allocator for the returned list.</param>
        /// <returns>A <see cref="NativeList{FloraInstanceHandle}"/> of matching instances.</returns>
        public NativeList<FloraInstanceHandle> FindInstanceOriginsWithinBoundsMatching(NativeArray<EntityId> prefabGameObjectIDs, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxMatchingWithBurst(prefabGameObjectIDs, bounds, allocator, out var instances);
            return instances;
        }

        /// <summary>
        /// Finds all instances whose world-space origins are inside a specified bounds volume and come from any one of the given identity sources.
        /// </summary>
        /// <param name="prefabGameObjectIDs">A collection of identity-source instance IDs to filter by.</param>
        /// <param name="bounds">The bounds volume.</param>
        /// <param name="results">Caller-owned result list. The list is cleared before matching instances are added.</param>
        public void FindInstanceOriginsWithinBoundsMatching(NativeArray<EntityId> prefabGameObjectIDs, Bounds bounds, NativeList<FloraInstanceHandle> results)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstanceOriginsWithinBoxMatchingNonAllocWithBurst(prefabGameObjectIDs, bounds, ref results);
        }

        #endregion
    }
}
