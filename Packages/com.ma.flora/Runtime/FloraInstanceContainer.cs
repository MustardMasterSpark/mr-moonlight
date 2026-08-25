// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// <summary>
    /// Manages a serialized collection of instances that do not require individual GameObjects, ideal for mass foliage or debris.
    /// </summary>
    [ExecuteAlways] [BurstCompile] [DisallowMultipleComponent]
    [AddComponentMenu("Flora/Flora Instance Container")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceContainer Icon.png")]
    [HelpURL("https://flora.magneticarcade.com/scripts/instance-container")]
    public sealed class FloraInstanceContainer : MonoBehaviour, ISerializationCallbackReceiver
    {
        private enum Version
        {
            None,
            Initial,
            GlobalDensity,
            SerializeDataAsBytes,
            RemoveCullingData,
            GlobalInstanceData,
            Latest,
        }

        [SerializeField] private Version m_Version = Version.Latest;
        [SerializeField] private GameObject m_Prefab;
        [SerializeField] private int m_SerializedTransformCount;
        [SerializeField] private byte[] m_SerializedTransformBytes = Array.Empty<byte>();

        [SerializeField, Obsolete] private FloraAdditionalRendererSettings m_Prototype;
        [SerializeField, Obsolete] private List<FloraInstanceRenderer> m_LinkedObjects = new();

        private Transform m_Transform;
        private NativeList<FloraInstanceHandle> m_InstanceHandles = new(0, Allocator.Persistent);
        private NativeList<FloraInstanceTransform> m_LocalTransforms = new(0, Allocator.Persistent);

        ~FloraInstanceContainer()
        {
            m_InstanceHandles.Dispose();
            m_LocalTransforms.Dispose();
        }

        #region Properties

        /// <summary>
        /// The prefab used to render all instances contained in this object.
        /// Changing this prefab re-initializes existing instances if they exist, invaliding their handles.
        /// </summary>
        public GameObject Prefab
        {
            get => m_Prefab;
            set
            {
                if (m_Prefab != value)
                {
                    if (m_Prefab)
                        FloraSystem.Instance?.UnregisterInstanceContainer(this);

                    m_Prefab = value;

                    if (m_Prefab && isActiveAndEnabled)
                        FloraSystem.GetOrCreate().RegisterInstanceContainer(this);
                }
            }
        }

        /// <summary>
        /// The total number of instances currently stored in the container.
        /// </summary>
        public int InstanceCount => m_LocalTransforms.IsCreated ? m_LocalTransforms.Length : 0;

        /// <summary>
        /// The runtime native array of <see cref="FloraInstanceHandle"/> for each instance in this container.
        /// </summary>
        /// <remarks>
        /// The container must be enabled for the handles to be valid.
        /// </remarks>
        public NativeArray<FloraInstanceHandle> InstanceHandles => m_InstanceHandles.AsArray();

        /// <summary>
        /// The runtime native array of local transforms for each instance in this container.
        /// </summary>
        public NativeArray<FloraInstanceTransform> LocalTransforms => m_LocalTransforms.AsArray();

        #endregion

        #region Lifecycle

        private void Awake()
        {
            m_Transform = transform;

#pragma warning disable CS0612
            if (m_Version == Version.Latest)
                return;

            while (m_Version < Version.RemoveCullingData)
            {
                m_Version++;
            }

            if (m_Version == Version.GlobalInstanceData - 1)
            {
                m_Prefab = m_Prototype ? m_Prototype.gameObject : null;

                bool hadLinkedObjects = m_LinkedObjects.Count > 0;
                if (hadLinkedObjects)
                {
                    CoreUtils.Destroy(this);
                    return;
                }

                m_Version++;
            }

#pragma warning restore CS0612

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnEnable()
        {
            m_Transform = transform;
            m_InstanceHandles.Resize(m_LocalTransforms.Length, NativeArrayOptions.ClearMemory);

            if (m_Prefab)
                FloraSystem.GetOrCreate().RegisterInstanceContainer(this);

            FloraSystem.WasCreated += OnSystemWasCreated;
            FloraSystem.WillBeDestroyed += OnSystemWillBeDestroyed;
        }

        private void OnDisable()
        {
            FloraSystem.WillBeDestroyed -= OnSystemWillBeDestroyed;
            FloraSystem.WasCreated -= OnSystemWasCreated;

            FloraSystem.Instance?.UnregisterInstanceContainer(this);
            m_InstanceHandles.Fill(FloraInstanceHandle.Null);

            m_InstanceHandles.TrimExcess();
            m_LocalTransforms.TrimExcess();
        }

        private void OnDestroy()
        {
            m_LocalTransforms.Clear();
        }

        private void OnSystemWasCreated(FloraSystem system)
        {
            if (m_Prefab)
                system.RegisterInstanceContainer(this);
        }

        private void OnSystemWillBeDestroyed(FloraSystem system)
        {
            m_InstanceHandles.Fill(FloraInstanceHandle.Null);
        }

        #endregion

        #region ISerializationCallbackReceiver

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_SerializedTransformCount =
                SerializationHelpers.SerializeTransformsToByteArray(
                    m_LocalTransforms.AsArray(),
                    ref m_SerializedTransformBytes);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            SerializationHelpers.DeserializeByteArrayToTransforms(
                ref m_SerializedTransformBytes,
                m_SerializedTransformCount,
                m_LocalTransforms);
        }

        #endregion

        #region Instance Management

        /// <summary>
        /// Retrieves the <see cref="FloraInstanceHandle"/> associated with the instance at <paramref name="instanceIndex"/>.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance within this container.</param>
        /// <returns>
        /// A globally unique handle if <paramref name="instanceIndex"/> is valid; otherwise <see cref="FloraInstanceHandle.Null"/>.
        /// </returns>
        public FloraInstanceHandle GetInstanceHandle(int instanceIndex)
        {
            return !m_InstanceHandles.IsValidIndex(instanceIndex) ? FloraInstanceHandle.Null : m_InstanceHandles[instanceIndex];
        }

        /// <summary>
        /// Checks whether the given <paramref name="instanceIndex"/> references a valid instance in this container.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance to check.</param>
        /// <returns>true if valid; otherwise false.</returns>
        public bool IsValidIndex(int instanceIndex)
        {
            return instanceIndex >= 0 && instanceIndex < m_InstanceHandles.Length;
        }

        /// <summary>
        /// Determines whether the instance at <paramref name="instanceIndex"/> is enabled.
        /// </summary>
        /// <remarks>
        /// An instance is considered enabled if it is visible and active in the <see cref="FloraSystem"/>.
        /// </remarks>
        public bool IsInstanceEnabled(int instanceIndex)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            return FloraSystem.Instance?.IsInstanceEnabled(m_InstanceHandles[instanceIndex]) ?? false;
        }

        /// <summary>
        /// Sets the visibility (enabled/disabled) of the instance at <paramref name="instanceIndex"/>.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance.</param>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetInstanceEnabled(int instanceIndex, bool enabled)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            FloraSystem.Instance?.SetInstanceEnabled(m_InstanceHandles[instanceIndex], enabled);
        }

        /// <summary>
        /// Returns the transform of the instance at <paramref name="instanceIndex"/> in either world or local space.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance.</param>
        /// <param name="space">The target space of the transform.</param>
        /// <returns>A local transform representing the instance.</returns>
        public FloraInstanceTransform GetInstanceTransform(int instanceIndex, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var localTransform = m_LocalTransforms[instanceIndex];
            return space == Space.World ? m_Transform.Transform(localTransform) : localTransform;
        }

        /// <summary>
        /// Returns an array of transforms for all instances in either world or local space.
        /// </summary>
        /// <param name="space">The target space of the transforms.</param>
        /// <param name="allocator">The allocator to use for the returned array.</param>
        /// <returns>An array of transforms representing all instances.</returns>
        public NativeArray<FloraInstanceTransform> GetInstanceTransforms(Space space, Allocator allocator)
        {
            var transforms = new NativeArray<FloraInstanceTransform>(m_LocalTransforms.Length, allocator, NativeArrayOptions.UninitializedMemory);

            if (space == Space.Self)
                transforms.CopyFrom(m_LocalTransforms.AsArray());
            else
                TransformInstancesWithBurst(m_LocalTransforms.AsArray(), ref transforms, m_Transform.localToWorldMatrix);

            return transforms;
        }

        /// <summary>
        /// Retrieves the position of the instance at <paramref name="instanceIndex"/> in either world or local space.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance.</param>
        /// <param name="space">Whether to get the position in world or local space.</param>
        /// <returns>A <see cref="float3"/> representing the instance’s position.</returns>
        public float3 GetInstancePosition(int instanceIndex, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var localPosition = m_LocalTransforms[instanceIndex].Position;
            return space == Space.World ? m_Transform.TransformPoint(localPosition) : localPosition;
        }

        /// <summary>
        /// Returns the rotation of the instance at <paramref name="instanceIndex"/> in either world or local space.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance.</param>
        /// <param name="space">The target space of the rotation.</param>
        /// <returns>The rotation of the instance in the specified space.</returns>
        public Quaternion GetInstanceRotation(int instanceIndex, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var localRotation = m_LocalTransforms[instanceIndex].Rotation;
            return space == Space.World ? m_Transform.TransformRotation(localRotation) : localRotation;
        }

        /// <summary>
        /// Retrieves the scale of the instance at <paramref name="instanceIndex"/> in either world or local space.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance.</param>
        /// <param name="space">Whether to get the scale in world or local space.</param>
        /// <returns>A <see cref="Vector3"/> representing the instance’s scale.</returns>
        public Vector3 GetInstanceScale(int instanceIndex, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var localScale = m_LocalTransforms[instanceIndex].Scale;
            return space == Space.World ? m_Transform.TransformScale(localScale) : localScale;
        }

        /// <summary>
        /// Returns the bounds of the instance at <paramref name="instanceIndex"/> in either world or local space.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance.</param>
        /// <param name="space">The target space of the bounds.</param>
        /// <returns>The bounds of the instance.</returns>
        public Bounds GetInstanceBounds(int instanceIndex, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            if (!m_Prefab)
                return AxisAlignedBox.Empty;

            var localTransform = m_LocalTransforms[instanceIndex];

            Bounds bounds;
            if (FloraSystem.Exists)
            {
                bounds = FloraSystem.Instance.GetInstanceBounds(m_InstanceHandles[instanceIndex]);
            }
            else
            {
                var prefabBounds = m_Prefab.CalculateLocalBounds();
                bounds = prefabBounds.TransformBy(localTransform.ToMatrix());
            }

            return space == Space.Self ? bounds.TransformBy(m_Transform.worldToLocalMatrix) : bounds;
        }

        /// <summary>
        /// Clears all instances from the container.
        /// </summary>
        public void ClearInstances()
        {
            if (FloraSystem.Exists)
                FloraSystem.Instance.ClearTrackedContainerInstances(this.GetEntityId());

            FloraSystem.Instance?.DestroyInstances(m_InstanceHandles.AsArray());
            m_InstanceHandles.Clear();
            m_LocalTransforms.Clear();
        }

        /// <summary>
        /// Reserves space for the specified number of instances
        /// .</summary>
        /// <param name="instanceCount">The number of instances to reserve space for.</param>
        /// <remarks>Reserving space will help reduce the number of reallocations when adding instances.</remarks>
        public void EnsureCapacity(int instanceCount)
        {
            if (m_LocalTransforms.Capacity < instanceCount)
            {
                var newCapacity = math.max(math.ceilpow2(instanceCount), 64);
                m_InstanceHandles.Capacity = newCapacity;
                m_LocalTransforms.Capacity = newCapacity;
            }
        }

        /// <summary>
        /// Reserves space for the specified number of additional instances.
        /// </summary>
        /// <param name="additionalInstanceCount">The number of additional instances to reserve space for.</param>
        /// <remarks>Reserving space will help reduce the number of reallocations when adding instances.</remarks>
        public void EnsureAdditionalCapacity(int additionalInstanceCount)
        {
            EnsureCapacity(m_LocalTransforms.Length + additionalInstanceCount);
        }

        /// <summary>
        /// Adds a new instance to the container.
        /// </summary>
        /// <param name="newInstanceTransform">The transform of the instance to add.</param>
        /// <param name="space">The space of the transform.</param>
        /// <returns>The index of the new instance.</returns>
        public int AddInstance(FloraInstanceTransform newInstanceTransform, Space space)
        {
            EnsureAdditionalCapacity(1);

            var newLocalTransform = space == Space.World ? m_Transform.InverseTransform(newInstanceTransform) : newInstanceTransform;
            var instanceIndex = m_LocalTransforms.Length;
            m_LocalTransforms.Add(newLocalTransform);
            m_InstanceHandles.Add(FloraInstanceHandle.Null);

            if (m_Prefab && FloraSystem.Exists)
            {
                EntityId containerEntity = this.GetEntityId();
                var newInstance = FloraSystem.Instance.CreateContainerInstance(m_Prefab, m_Transform, containerEntity, newLocalTransform);
                m_InstanceHandles[instanceIndex] = newInstance;
                FloraSystem.Instance.SetInstanceInContainer(newInstance, this, instanceIndex);
                FloraSystem.Instance.AppendTrackedContainerInstances(
                    containerEntity,
                    m_InstanceHandles.AsArray().GetSubArray(instanceIndex, 1),
                    m_LocalTransforms.AsArray().GetSubArray(instanceIndex, 1));
            }

            return instanceIndex;
        }

        /// <summary>
        /// Adds the specified number of instances to the container.
        /// </summary>
        /// <param name="instanceTransforms">The transforms of the instances to add.</param>
        /// <param name="space">The space of the transforms.</param>
        public void AddInstances(NativeArray<FloraInstanceTransform> instanceTransforms, Space space)
        {
            EnsureAdditionalCapacity(instanceTransforms.Length);

            int instanceStartIndex = m_LocalTransforms.Length;
            if (space == Space.Self)
            {
                m_LocalTransforms.AddRange(instanceTransforms);
            }
            else
            {
                m_LocalTransforms.Resize(instanceStartIndex + instanceTransforms.Length, NativeArrayOptions.ClearMemory);
                var newTransforms = m_LocalTransforms.AsArray().GetSubArray(instanceStartIndex, instanceTransforms.Length);
                TransformInstancesWithBurst(instanceTransforms, ref newTransforms, m_Transform.worldToLocalMatrix);
            }

            m_InstanceHandles.Resize(m_LocalTransforms.Length, NativeArrayOptions.ClearMemory);

            if (m_Prefab && FloraSystem.Exists)
            {
                EntityId containerEntity = this.GetEntityId();
                var instancesToAdd = m_InstanceHandles.AsArray().GetSubArray(instanceStartIndex, instanceTransforms.Length);
                var transformsToAdd = m_LocalTransforms.AsArray().GetSubArray(instanceStartIndex, instanceTransforms.Length);
                FloraSystem.Instance.CreateContainerInstances(m_Prefab, m_Transform, containerEntity, instancesToAdd, transformsToAdd);
                FloraSystem.Instance.SetInstanceInContainerIndices(instancesToAdd, this, instanceStartIndex);
                FloraSystem.Instance.AppendTrackedContainerInstances(containerEntity, instancesToAdd, transformsToAdd);
            }
        }

        /// <summary>
        /// Adds the specified number of instances to the container.
        /// </summary>
        /// <param name="localToWorldMatrices">An array of global matrices representing the instances to add.</param>
        public void AddInstances(NativeArray<float4x4> localToWorldMatrices)
        {
            EnsureAdditionalCapacity(localToWorldMatrices.Length);

            int instanceStartIndex = m_LocalTransforms.Length;
            m_LocalTransforms.Resize(instanceStartIndex + localToWorldMatrices.Length, NativeArrayOptions.ClearMemory);

            var newTransforms = m_LocalTransforms.AsArray().GetSubArray(instanceStartIndex, localToWorldMatrices.Length);
            TransformInstancesWithBurst(localToWorldMatrices, ref newTransforms, m_Transform.worldToLocalMatrix);
            m_InstanceHandles.Resize(m_LocalTransforms.Length, NativeArrayOptions.ClearMemory);

            if (m_Prefab && FloraSystem.Exists)
            {
                EntityId containerEntity = this.GetEntityId();
                var instancesToAdd = m_InstanceHandles.AsArray().GetSubArray(instanceStartIndex, localToWorldMatrices.Length);
                var transformsToAdd = m_LocalTransforms.AsArray().GetSubArray(instanceStartIndex, localToWorldMatrices.Length);
                FloraSystem.Instance.CreateContainerInstances(m_Prefab, m_Transform, containerEntity, instancesToAdd, transformsToAdd);
                FloraSystem.Instance.SetInstanceInContainerIndices(instancesToAdd, this, instanceStartIndex);
                FloraSystem.Instance.AppendTrackedContainerInstances(containerEntity, instancesToAdd, transformsToAdd);
            }
        }

        /// <summary>
        /// Updates the **position** of the instance at <paramref name="instanceIndex"/>
        /// to <paramref name="position"/> in the specified <paramref name="space"/>.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance to update.</param>
        /// <param name="position">The new position.</param>
        /// <param name="space">The coordinate space of <paramref name="position"/>.</param>
        public void UpdateInstancePosition(int instanceIndex, Vector3 position, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var oldLocalTransform = GetInstanceTransform(instanceIndex, Space.Self);
            var newLocalPosition = space == Space.World ? m_Transform.InverseTransformPoint(position) : position;
            var newLocalTransform = new FloraInstanceTransform(newLocalPosition, oldLocalTransform.Rotation, oldLocalTransform.Scale);
            UpdateInstanceTransform(instanceIndex, newLocalTransform, Space.Self);
        }

        /// <summary>
        /// Updates the **rotation** of the instance at <paramref name="instanceIndex"/>
        /// to <paramref name="rotation"/> in the specified <paramref name="space"/>.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance to update.</param>
        /// <param name="rotation">The new rotation.</param>
        /// <param name="space">The coordinate space of <paramref name="rotation"/>.</param>
        public void UpdateInstanceRotation(int instanceIndex, Quaternion rotation, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var oldLocalTransform = GetInstanceTransform(instanceIndex, Space.Self);
            var newLocalRotation = space == Space.World ? m_Transform.InverseTransformRotation(rotation) : rotation;
            var newLocalTransform = new FloraInstanceTransform(oldLocalTransform.Position, newLocalRotation, oldLocalTransform.Scale);
            UpdateInstanceTransform(instanceIndex, newLocalTransform, Space.Self);
        }

        /// <summary>
        /// Updates the **scale** of the instance at <paramref name="instanceIndex"/>
        /// to <paramref name="scale"/> in the specified <paramref name="space"/>.
        /// </summary>
        /// <param name="instanceIndex">Index of the instance to update.</param>
        /// <param name="scale">The new scale.</param>
        /// <param name="space">The coordinate space of <paramref name="scale"/>.</param>
        public void UpdateInstanceScale(int instanceIndex, Vector3 scale, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var oldLocalTransform = GetInstanceTransform(instanceIndex, Space.Self);
            var newLocalScale = space == Space.World ? m_Transform.InverseTransformScale(scale) : scale;
            var newLocalTransform = new FloraInstanceTransform(oldLocalTransform.Position, oldLocalTransform.Rotation, newLocalScale);
            UpdateInstanceTransform(instanceIndex, newLocalTransform, Space.Self);
        }

        /// <summary>
        /// Updates the transform of the instance at the specified index.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance to update.</param>
        /// <param name="newInstanceTransform">The new transform of the instance.</param>
        /// <param name="space">The space of the new transform.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if the instance index is out of range.</exception>
        public void UpdateInstanceTransform(int instanceIndex, FloraInstanceTransform newInstanceTransform, Space space)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var newLocalTransform = space == Space.World ? m_Transform.InverseTransform(newInstanceTransform) : newInstanceTransform;
            m_LocalTransforms[instanceIndex] = newLocalTransform;
            if (FloraSystem.Exists)
            {
                FloraSystem.Instance.UpdateInstanceLocalToWorldMatrix(m_InstanceHandles[instanceIndex], math.mul(m_Transform.localToWorldMatrix, newLocalTransform.ToMatrix()));
                FloraSystem.Instance.UpdateTrackedContainerLocalTransforms(
                    this.GetEntityId(),
                    instanceIndex,
                    m_LocalTransforms.AsArray().GetSubArray(instanceIndex, 1));
            }
        }

        /// <summary>
        /// Updates the transform of the instances starting at the specified index.
        /// </summary>
        /// <param name="startInstanceIndex">The index of the first instance to update.</param>
        /// <param name="newInstanceTransforms">The new transforms of the instances.</param>
        /// <param name="space">The space of the new transforms.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the number of instance transforms exceeds the number of instances in the container.</exception>
        public void UpdateInstanceTransforms(int startInstanceIndex, NativeArray<FloraInstanceTransform> newInstanceTransforms, Space space)
        {
            for (int i = 0; i < newInstanceTransforms.Length; i++)
                UpdateInstanceTransform(startInstanceIndex + i, newInstanceTransforms[i], space);
        }

        /// <summary>
        /// Updates the transform of the instances with the specified indices.
        /// </summary>
        /// <param name="indices">The indices of the instances to update.</param>
        /// <param name="newInstanceTransforms">The new transforms of the instances.</param>
        /// <param name="space">The space of the transforms.</param>
        /// <exception cref="ArgumentException">Thrown if the number of instance indices does not match the number of instance transforms.</exception>
        public void UpdateInstanceTransforms(NativeArray<int> indices, NativeArray<FloraInstanceTransform> newInstanceTransforms, Space space)
        {
            if (indices.Length != newInstanceTransforms.Length)
                throw new ArgumentException("The number of instance indices must match the number of instance transforms.", nameof(newInstanceTransforms));

            for (int i = 0; i < newInstanceTransforms.Length; i++)
                UpdateInstanceTransform(indices[i], newInstanceTransforms[i], space);
        }

        /// <summary>
        /// Updates the transform of the instance at the specified index using a global local-to-world matrix.
        /// </summary>
        /// <param name="instanceIndex">The index of the instance to update.</param>
        /// <param name="newLocalToWorld">The new local-to-world matrix of the instance.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown if the instance index is out of range.</exception>
        public void UpdateInstanceLocalToWorldMatrix(int instanceIndex, float4x4 newLocalToWorld)
        {
            if (!IsValidIndex(instanceIndex))
                throw new IndexOutOfRangeException("The instance index is out of range.");

            var newLocalTransform = FloraInstanceTransform.FromMatrix(math.mul(m_Transform.worldToLocalMatrix, newLocalToWorld));
            m_LocalTransforms[instanceIndex] = newLocalTransform;
            if (FloraSystem.Exists)
            {
                FloraSystem.Instance.UpdateInstanceLocalToWorldMatrix(m_InstanceHandles[instanceIndex], newLocalToWorld);
                FloraSystem.Instance.UpdateTrackedContainerLocalTransforms(
                    this.GetEntityId(),
                    instanceIndex,
                    m_LocalTransforms.AsArray().GetSubArray(instanceIndex, 1));
            }
        }

        /// <summary>
        /// Updates the transform of the instances starting at the specified index using global local-to-world matrices.
        /// </summary>
        /// <param name="startInstanceIndex">The index of the first instance to update.</param>
        /// <param name="newLocalToWorldMatrices">The new local-to-world matrices of the instances.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the number of instance transforms exceeds the number of instances in the container.</exception>
        public void UpdateInstanceLocalToWorldMatrices(int startInstanceIndex, NativeArray<float4x4> newLocalToWorldMatrices)
        {
            if (startInstanceIndex < 0 || startInstanceIndex + newLocalToWorldMatrices.Length > m_LocalTransforms.Length)
                throw new ArgumentOutOfRangeException(nameof(startInstanceIndex), "The start index is out of range for the number of instance transforms.");

            var localTransformsSlice = m_LocalTransforms.AsArray().GetSubArray(startInstanceIndex, newLocalToWorldMatrices.Length);
            TransformInstancesWithBurst(newLocalToWorldMatrices, ref localTransformsSlice, m_Transform.worldToLocalMatrix);

            var instanceHandlesSlice = m_InstanceHandles.AsArray().GetSubArray(startInstanceIndex, newLocalToWorldMatrices.Length);
            if (FloraSystem.Exists)
            {
                FloraSystem.Instance.UpdateInstanceLocalToWorldMatrices(instanceHandlesSlice, newLocalToWorldMatrices);
                FloraSystem.Instance.UpdateTrackedContainerLocalTransforms(this.GetEntityId(), startInstanceIndex, localTransformsSlice);
            }
        }

        /// <summary>
        /// Updates the transform of the instances with the specified indices using global local-to-world matrices.
        /// </summary>
        /// <param name="indices">The indices of the instances to update.</param>
        /// <param name="newLocalToWorldMatrices">The new local-to-world matrices of the instances.</param>
        /// <exception cref="ArgumentException">Thrown if the number of instance indices does not match the number of instance transforms.</exception>
        public void UpdateInstanceLocalToWorldMatrices(NativeArray<int> indices, NativeArray<float4x4> newLocalToWorldMatrices)
        {
            if (indices.Length != newLocalToWorldMatrices.Length)
                throw new ArgumentException("The number of instance indices must match the number of instance transforms.", nameof(newLocalToWorldMatrices));

            for (int i = 0; i < newLocalToWorldMatrices.Length; i++)
                UpdateInstanceLocalToWorldMatrix(indices[i], newLocalToWorldMatrices[i]);
        }

        /// <summary>
        /// Removes all instances whose indices are specified by <paramref name="instancesToRemove"/>.
        /// </summary>
        /// <remarks>
        /// Each removed instance is swapped with the last valid instance, so the array
        /// indices will shift. This might cause unexpected behavior if your logic depends
        /// on stable ordering.
        /// </remarks>
        /// <param name="instancesToRemove">Indices of the instances to remove.</param>
        public void RemoveInstances(NativeArray<int> instancesToRemove)
        {
            if (instancesToRemove.Length == 0)
                return;

            using var indexSet = new UnsafeBitSet(instancesToRemove.Length, Allocator.Temp);
            for (int i = 0; i < instancesToRemove.Length; i++)
                indexSet.Add(instancesToRemove[i]);

            foreach (var index in indexSet.GetReverseEnumerator())
                RemoveInstance(index);
        }

        /// <summary>
        /// Removes the instance at <paramref name="indexToRemove"/> from the container.
        /// </summary>
        /// <remarks>
        /// The last instance is swapped into the removed instance’s position in the array. As a result, instance indices may change.
        /// </remarks>
        /// <param name="indexToRemove">Index of the instance to remove.</param>
        public void RemoveInstance(int indexToRemove)
        {
            if (!IsValidIndex(indexToRemove))
                return;

            var handleToRemove = m_InstanceHandles[indexToRemove];
            m_LocalTransforms.RemoveAtSwapBack(indexToRemove);
            m_InstanceHandles.RemoveAtSwapBack(indexToRemove);

            if (FloraSystem.Exists)
            {
                if (indexToRemove < m_InstanceHandles.Length)
                {
                    var movedHandle = m_InstanceHandles[indexToRemove];
                    FloraSystem.Instance.SetInstanceInContainer(movedHandle, this, indexToRemove);
                }

                FloraSystem.Instance.RemoveTrackedContainerInstance(this.GetEntityId(), indexToRemove);
                FloraSystem.Instance.DestroyInstance(handleToRemove);
            }
        }

        /// <summary>
        /// Calculates and returns a bounding box that encompasses all instances in the container.
        /// </summary>
        /// <param name="space">Whether to return bounds in local or world space.</param>
        /// <returns>
        /// A <see cref="Bounds"/> that encloses every instance in the container, or an empty <see cref="Bounds"/> if there are no instances.
        /// </returns>
        public Bounds CalculateBounds(Space space)
        {
            if (!m_Prefab || m_LocalTransforms.Length == 0)
                return AxisAlignedBox.Empty;

            AxisAlignedBox result;
            if (FloraSystem.Exists && m_InstanceHandles.Length > 0)
            {
                result = FloraSystem.Instance.CalculateInstanceBounds(m_InstanceHandles.AsArray(), m_Transform.worldToLocalMatrix);
            }
            else
            {
                CalculateCombinedBoundsWithBurst(m_Prefab.CalculateLocalBounds(), m_LocalTransforms.AsArray(), out result);
            }

            return space == Space.World ? result.TransformBy(m_Transform.localToWorldMatrix) : result;
        }

        #endregion

        #region Burst Helpers

        [BurstCompile]
        private static void TransformInstancesWithBurst(
            [NoAlias] in NativeArray<FloraInstanceTransform> input,
            [NoAlias] ref NativeArray<FloraInstanceTransform> output,
            in float4x4 transform)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = FloraInstanceTransform.FromMatrix(math.mul(transform, input[i].ToMatrix()));
            }
        }

        [BurstCompile]
        private static void TransformInstancesWithBurst(
            [NoAlias] in NativeArray<FloraLocalToWorld> input,
            [NoAlias] ref NativeArray<FloraInstanceTransform> output,
            in FloraLocalToWorld transform)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = FloraInstanceTransform.FromMatrix(input[i].TransformBy(transform));
            }
        }

        [BurstCompile]
        private static void TransformInstancesWithBurst(
            [NoAlias] in NativeArray<float4x4> input,
            [NoAlias] ref NativeArray<FloraInstanceTransform> output,
            in float4x4 transform)
        {
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = FloraInstanceTransform.FromMatrix(math.mul(transform, input[i]));
            }
        }

        [BurstCompile]
        private static void CalculateCombinedBoundsWithBurst(
            in AxisAlignedBox prefabAABB,
            in NativeArray<FloraInstanceTransform> localTransforms,
            out AxisAlignedBox combinedAABB)
        {
            combinedAABB = AxisAlignedBox.Empty;

            for (int i = 0; i < localTransforms.Length; i++)
            {
                var instanceAABB = prefabAABB.TransformBy(localTransforms[i].ToMatrix());
                combinedAABB.Encapsulate(instanceAABB);
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper extension methods for <see cref="FloraInstanceContainer"/>.
    /// </summary>
    public static class FloraInstanceContainerHelpers
    {
        /// <summary>
        /// Adds a new instance to the container.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="position">The position of the instance.</param>
        /// <param name="rotation">The rotation of the instance.</param>
        /// <param name="scale">The scale of the instance.</param>
        /// <param name="space">The space of the transform.</param>
        /// <returns>The index of the new instance.</returns>
        public static int AddInstance(this FloraInstanceContainer container, Vector3 position, Quaternion rotation, Vector3 scale, Space space)
        {
            return container.AddInstance(FloraInstanceTransform.FromPositionRotationScale(position, rotation, scale), space);
        }

        /// <summary>
        /// Adds a new instance to the container.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="position">The position of the instance.</param>
        /// <param name="rotation">The rotation of the instance.</param>
        /// <param name="space">The space of the transform.</param>
        /// <returns>The index of the new instance.</returns>
        public static int AddInstance(this FloraInstanceContainer container, Vector3 position, Quaternion rotation, Space space)
        {
            return container.AddInstance(FloraInstanceTransform.FromPositionRotationScale(position, rotation, 1), space);
        }

        /// <summary>
        /// Adds a new instance to the container.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="position">The position of the instance.</param>
        /// <param name="space">The space of the transform.</param>
        /// <returns>The index of the new instance.</returns>
        public static int AddInstance(this FloraInstanceContainer container, Vector3 position, Space space)
        {
            return container.AddInstance(FloraInstanceTransform.FromPositionRotationScale(position, quaternion.identity, 1), space);
        }

        /// <summary>
        /// Updates the transform of the instance at the specified index.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="instanceIndex">The index of the instance to update.</param>
        /// <param name="position">The new position of the instance.</param>
        /// <param name="rotation">The new rotation of the instance.</param>
        /// <param name="scale">The new scale of the instance.</param>
        /// <param name="space">The space of position, rotation, and scale.</param>
        public static void UpdateInstanceTransform(this FloraInstanceContainer container, int instanceIndex, Vector3 position, Quaternion rotation, Vector3 scale, Space space)
        {
            container.UpdateInstanceTransform(instanceIndex, FloraInstanceTransform.FromPositionRotationScale(position, rotation, scale), space);
        }

        /// <summary>
        /// Updates the transform of the instance at the specified index.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="instanceIndex">The index of the instance to update.</param>
        /// <param name="position">The new position of the instance.</param>
        /// <param name="rotation">The new rotation of the instance.</param>
        /// <param name="space">The space of position and rotation.</param>
        public static void UpdateInstanceTransform(this FloraInstanceContainer container, int instanceIndex, Vector3 position, Quaternion rotation, Space space)
        {
            container.UpdateInstanceTransform(instanceIndex, FloraInstanceTransform.FromPositionRotationScale(position, rotation, 1), space);
        }

        /// <summary>
        /// Updates the transform of the instance at the specified index.
        /// </summary>
        /// <param name="container">The instance container.</param>
        /// <param name="instanceIndex">The index of the instance to update.</param>
        /// <param name="position">The new position of the instance.</param>
        /// <param name="space">The space of position.</param>
        public static void UpdateInstanceTransform(this FloraInstanceContainer container, int instanceIndex, Vector3 position, Space space)
        {
            container.UpdateInstanceTransform(instanceIndex, FloraInstanceTransform.FromPositionRotationScale(position, quaternion.identity, 1), space);
        }
    }
}
