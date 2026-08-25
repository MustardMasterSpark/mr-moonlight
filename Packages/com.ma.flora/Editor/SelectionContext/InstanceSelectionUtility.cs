// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using MA.Flora.Editor.InternalBridge;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityObject = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal static class InstanceSelectionUtility
    {
        #region Entity Picking

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
#if UNITY_6000_3_OR_NEWER
            HandleUtility.getEntityIdsForAuthoringObject += GetEntitiesForAuthoringObject;
#else
            HandleUtility.getEntitiesForAuthoringObject += GetEntitiesForAuthoringObject;
#endif
            HandleUtility.getAuthoringObjectForEntity += GetAuthoringObjectForEntity;
#if UNITY_6000_5_OR_NEWER
            SceneHierarchyBridge.AddGetEntityIdFromIndex(GetEntityIdFromIndex);
#endif
        }

#if UNITY_6000_3_OR_NEWER
        private static IEnumerable<EntityId> GetEntitiesForAuthoringObject(UnityObject obj)
#else
        static IEnumerable<int> GetEntitiesForAuthoringObject(UnityObject obj)
#endif
        {
            if (!FloraSystem.Active)
            {
#if UNITY_6000_3_OR_NEWER
                yield return EntityId.None;
#else
                yield return 0;
#endif
            }
            else
            {
                bool isInSelectionContext = ToolManager.activeContextType == typeof(InstanceSelectionContext);
                if (!isInSelectionContext)
                {
                    if (obj is GameObject gameObject)
                    {
                        if (gameObject.TryGetComponent(out FloraInstanceRenderer instanceRenderer))
                        {
                            yield return ToEntityId(instanceRenderer.InstanceHandle);
                        }
                        else if (gameObject.TryGetComponent(out FloraInstanceContainer instanceContainer))
                        {
                            for (int i = 0; i < instanceContainer.InstanceCount; i++)
                                yield return ToEntityId(instanceContainer.GetInstanceHandle(i));
                        }
                    }
                }
                else if (obj is InstanceSelectionGroup selectionGroup)
                {
                    for (int i = 0; i < selectionGroup.Length; i++)
                        yield return ToEntityId(selectionGroup.GetInstanceHandleAt(i));
                }
            }
        }

#if UNITY_6000_3_OR_NEWER
        private static EntityId ToEntityId(FloraInstanceHandle instanceHandle)
        {
#if UNITY_6000_5_OR_NEWER
            return EntityId.FromULong(UnsafeUtility.As<FloraInstanceHandle, ulong>(ref instanceHandle));
#else
            return instanceHandle.Index;
#endif
        }
#else
        private static int ToEntityId(FloraInstanceHandle instanceHandle) => instanceHandle.Index;
#endif

#if UNITY_6000_5_OR_NEWER
        private static EntityId GetEntityIdFromIndex(int instanceHandleIndex)
        {
            FloraInstanceHandle instanceHandle = InstanceRegistry.Data.GetInstanceByIndex(instanceHandleIndex);
            return ToEntityId(instanceHandle);
        }
#endif

        private static UnityObject GetAuthoringObjectForEntity(int instanceHandleIndex)
        {
            if (!FloraSystem.Active || instanceHandleIndex < 0)
                return null;

            FloraInstanceHandle instance = InstanceRegistry.Data.GetInstanceByIndex(instanceHandleIndex);
            bool isInSelectionContext = ToolManager.activeContextType == typeof(InstanceSelectionContext);
            if (!isInSelectionContext)
                return FloraSystem.Instance.GetInstanceOwnerGameObject(instance);

            if (ScenePickerGameObject.Instance)
            {
                var picked = InstanceSelectionFactory.CreateInstance(instance);
                ScenePickerGameObject.Instance.Picked = picked;
                return ScenePickerGameObject.Instance.gameObject;
            }

            // If we did not find the container associated with this entity, try to find it in the current selection.
            // We don't want to create a new FloraSelectionGroup for an Entity that is already selected.
            // Otherwise some features like Ctrl+click to deselect an Entity won't work.
            // For example, Ctrl+click is basically checking if the newly picked object is already in the Selection.objects in list.
            // If this is the case, then it deselects it.
            if (Selection.objects != null)
            {
                foreach (UnityObject obj in Selection.objects)
                {
                    var selectionGroup = obj as InstanceSelectionGroup;
                    if (selectionGroup != null)
                    {
                        for (int i = 0; i < selectionGroup.Length; i++)
                        {
                            if (selectionGroup.GetInstanceHandleAt(i) == instance)
                            {
                                return selectionGroup;
                            }
                        }
                    }
                }
            }

            return InstanceSelectionFactory.CreateInstance(instance);
        }

        #endregion

        #region Rectangle Picking

        private static readonly ProfilerMarker PickRectInstancesMarker = new("Flora.PickRectInstances");

        public static UnityObject[] PickRectInstances(SceneView view, Rect rect)
        {
            using var _ = PickRectInstancesMarker.Auto();
            using var rectPlanes = CreateRectFrustumPlanes(view.camera, rect, Allocator.TempJob);
            if (rectPlanes.Length == 0)
                return Array.Empty<UnityObject>();

            using var instances = FloraSystem.Instance.FindInstancesInPlanes(rectPlanes, Allocator.TempJob);
            if (instances.Length < 2048)
            {
                instances.Reinterpret<ulong>().Sort();
            }
            else
            {
                instances.Reinterpret<long>().ParallelSort().Complete();
            }

            using (ListPool<InstanceSelectionGroup>.Get(out var groups))
            using (ListPool<int>.Get(out var indices))
            {
                Component currentComponent = null;

                for (int i = 0; i < instances.Length; i++)
                {
                    var instance = instances[i];
                    int instanceIndex;
                    Component instanceComponent;

                    var instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instance);
                    var treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instance);

                    if (!instanceInContainer.Equals(InstanceInContainer.None))
                    {
                        instanceComponent = instanceInContainer.Container;
                        instanceIndex = instanceInContainer.IndexInContainer;
                    }
                    else if (!treeInTerrain.Equals(TreeInTerrain.None))
                    {
                        instanceComponent = treeInTerrain.TerrainEntity.ToObject<Terrain>();
                        instanceIndex = treeInTerrain.IndexInTreeInstances;
                    }
                    else
                    {
                        continue;
                    }

                    // If we detect a change (container or terrain), finalize the previous group
                    if (instanceComponent != currentComponent)
                    {
                        // finalize previous group
                        FinalizeSelectionGroup(currentComponent, indices, groups);

                        // move on
                        currentComponent = instanceComponent;
                        indices.Clear();
                    }

                    if (currentComponent)
                        indices.Add(instanceIndex);
                }

                // Finalize the last group
                FinalizeSelectionGroup(currentComponent, indices, groups);

                return groups.ToArray();
            }
        }

        private static void FinalizeSelectionGroup(Component component, List<int> indices, List<InstanceSelectionGroup> groups)
        {
            if (component == null || indices.Count == 0)
                return;

            groups.Add(InstanceSelectionFactory.CreateInstance(component, indices.ToArray()));
        }

        public static Rect FromToRect(float2 a, float2 b)
        {
            var min = math.min(a, b);
            var max = math.max(a, b);
            return new Rect(min, max - min);
        }

        private static Rect GuiRectToPixelRect(Rect guiRect, Camera camera)
        {
            float gx0 = guiRect.xMin;
            float gy0 = guiRect.yMin; // top edge in points
            float gx1 = guiRect.xMax;
            float gy1 = guiRect.yMax; // bottom edge in points

            float dpiScale = EditorGUIUtility.pixelsPerPoint;
            float px0 = gx0 * dpiScale;
            float py0 = gy0 * dpiScale;
            float px1 = gx1 * dpiScale;
            float py1 = gy1 * dpiScale;

            float xMinPx = math.min(px0, px1);
            float xMaxPx = math.max(px0, px1);
            float yMinPx = math.min(py0, py1);
            float yMaxPx = math.max(py0, py1);

            float screenYMin = camera.pixelHeight - yMaxPx; // bottom edge in pixels

            return new Rect(
                xMinPx,
                screenYMin,
                xMaxPx - xMinPx,
                yMaxPx - yMinPx
            );
        }

        private static readonly Plane[] TempPlanes = new Plane[6];

        public static NativeArray<Plane> CreateRectFrustumPlanes(Camera camera, Rect guiRect, Allocator allocator)
        {
            Rect pixelRect = GuiRectToPixelRect(guiRect, camera);
            float3 position = camera.transform.position;
            float  nearClip = camera.nearClipPlane;

            // Determine the corners of the rectangle on the camera's near plane
            Vector3 bl = camera.ScreenToWorldPoint(new Vector3(pixelRect.xMin, pixelRect.yMin, nearClip)); // bottom-left
            Vector3 br = camera.ScreenToWorldPoint(new Vector3(pixelRect.xMax, pixelRect.yMin, nearClip)); // bottom-right
            Vector3 tl = camera.ScreenToWorldPoint(new Vector3(pixelRect.xMin, pixelRect.yMax, nearClip)); // top-left
            Vector3 tr = camera.ScreenToWorldPoint(new Vector3(pixelRect.xMax, pixelRect.yMax, nearClip)); // top-right

            // Build four “side” planes
            if (!TryCreatePlaneFromTriangle(position, tl, bl, out var leftPlane) ||
                !TryCreatePlaneFromTriangle(position, br, tr, out var rightPlane) ||
                !TryCreatePlaneFromTriangle(position, bl, br, out var bottomPlane) ||
                !TryCreatePlaneFromTriangle(position, tr, tl, out var topPlane))
            {
                return new NativeArray<Plane>(0, allocator);
            }

            // Calculate the camera's near and far planes
            GeometryUtility.CalculateFrustumPlanes(camera, TempPlanes);

            var result = new NativeArray<Plane>(5, allocator, NativeArrayOptions.UninitializedMemory);
            result[0] = leftPlane;
            result[1] = rightPlane;
            result[2] = bottomPlane;
            result[3] = topPlane;
            result[4] = TempPlanes[4];
            return result;
        }

        private static bool TryCreatePlaneFromTriangle(Vector3 a, Vector3 b, Vector3 c, out Plane plane)
        {
            plane = new Plane(a, b, c);
            return plane.normal.sqrMagnitude > 0;
        }

        #endregion
    }
}
