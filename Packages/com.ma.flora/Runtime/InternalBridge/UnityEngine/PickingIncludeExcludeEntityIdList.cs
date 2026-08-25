// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

#if !UNITY_6000_3_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

#nullable disable
namespace UnityEditor
{
    /// <summary>
    ///   <para>Represents a list of Unity Object and &lt;a href="https:docs.unity3d.comPackagescom.unity.entities@latestindex.html?subfolder=manualecs_entities.html"&gt;DOTS Entity&lt;a&gt; IDs that picking algorithms can either consider or discard.</para>
    /// </summary>
    public struct PickingIncludeExcludeEntityIdList : IDisposable
    {
        NativeArray<EntityId> m_IncludeRenderers;
        NativeArray<EntityId> m_ExcludeRenderers;
        NativeArray<EntityId> m_IncludeEntities;
        NativeArray<EntityId> m_ExcludeEntities;

        /// <summary>
        ///   <para>Represents a list of Unity Object and &lt;a href="https:docs.unity3d.comPackagescom.unity.entities@latestindex.html?subfolder=manualecs_entities.html"&gt;DOTS Entity&lt;a&gt; IDs that picking algorithms can either consider or discard.</para>
        /// </summary>
        public PickingIncludeExcludeEntityIdList(List<EntityId> includeRendererEntityIds,
            List<EntityId> excludeRendererEntityIds,
            List<EntityId> includeEntityIndices,
            List<EntityId> excludeEntityIndices,
            Allocator allocator = Allocator.Persistent)
        {
            m_IncludeRenderers = ArrayFromList(includeRendererEntityIds, allocator);
            m_ExcludeRenderers = ArrayFromList(excludeRendererEntityIds, allocator);
            m_IncludeEntities = ArrayFromList(includeEntityIndices, allocator);
            m_ExcludeEntities = ArrayFromList(excludeEntityIndices, allocator);
        }

        /// <summary>
        ///   <para>An array of GameObjects that the picking algorithm exclusively considers when it selects the nearest object. If this is null, Unity considers all GameObjects in open scenes for selection.</para>
        /// </summary>
        public NativeArray<EntityId> IncludeRenderers => m_IncludeRenderers;

        /// <summary>
        ///   <para>An array of GameObjects that the picking algorithm doesn't consider when it selects the nearest object.</para>
        /// </summary>
        public NativeArray<EntityId> ExcludeRenderers => m_ExcludeRenderers;

        /// <summary>
        ///   <para>An array of DOTS Entity IDs that the picking algorithm exclusively considers when it selects the nearest object. If this is null, Unity considers all DOTS Entities in open scenes for selection.</para>
        /// </summary>
        public NativeArray<EntityId> IncludeEntities => m_IncludeEntities;

        /// <summary>
        ///   <para>An array of DOTS Entity IDs that the picking algorithm doesn't consider when it selects the nearest object.</para>
        /// </summary>
        public NativeArray<EntityId> ExcludeEntities => m_ExcludeEntities;

        /// <summary>
        ///   <para>Dispose all the Unity.Collections.NativeArray inside the struct.</para>
        /// </summary>
        public void Dispose()
        {
            NativeArray<EntityId> nativeArray;
            if (IncludeRenderers.IsCreated)
            {
                nativeArray = IncludeRenderers;
                nativeArray.Dispose();
            }

            nativeArray = ExcludeRenderers;
            if (nativeArray.IsCreated)
            {
                nativeArray = ExcludeRenderers;
                nativeArray.Dispose();
            }

            nativeArray = IncludeEntities;
            if (nativeArray.IsCreated)
            {
                nativeArray = IncludeEntities;
                nativeArray.Dispose();
            }

            nativeArray = ExcludeEntities;
            if (nativeArray.IsCreated)
            {
                nativeArray = ExcludeEntities;
                nativeArray.Dispose();
            }
        }

        static NativeArray<EntityId> ArrayFromList(List<EntityId> list, Allocator allocator)
        {
            if (list == null || list.Count == 0)
                return new NativeArray<EntityId>();

            var dst = new NativeArray<EntityId>(list.Count, allocator, NativeArrayOptions.UninitializedMemory);
            NativeArray<EntityId>.Copy(NoAllocHelpers.ExtractArrayFromList(list), dst, list.Count);
            return dst;
        }
    }
}
#endif
