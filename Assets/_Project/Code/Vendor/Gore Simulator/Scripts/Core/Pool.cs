// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Tools;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace PampelGames.GoreSimulator
{
    public static class Pool
    {
        internal static void Release(bool poolActive, List<Type> excludedComponentTypes,
            GameObject detachedObject, bool isParticle)
        {
            if (!detachedObject) return;
            if (!detachedObject.scene.isLoaded) return;

            if (detachedObject.TryGetComponent<MeshPart>(out var meshPart))
            {
                detachedObject.SetActive(false);
                return;
            }

            if (!isParticle)
            {
                var components = detachedObject.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component is not MeshFilter filter) continue;
                    Object.Destroy(filter.mesh);
                    Object.Destroy(filter.sharedMesh);
                }

                if (poolActive && Application.isPlaying)
                    foreach (var component in components)
                    {
                        switch (component)
                        {
                            case MeshFilter filter:
                            case MeshRenderer renderer:
                            case SkinnedMeshRenderer skinnedMeshRenderer:
                            case PGPoolable poolable:
                                continue;
                        }

                        if (excludedComponentTypes.Contains(component.GetType())) continue;
                        Object.Destroy(component);
                    }
            }


            if (poolActive && Application.isPlaying)
                PGPool.Release(detachedObject);
            else
                Object.Destroy(detachedObject);
        }

        internal static GameObject Get(GameObject prefab)
        {
            var newObject = PGPool.Get(prefab);
            return newObject;
        }

        internal static void ReleaseMesh(Mesh mesh)
        {
            if (Application.isPlaying) Object.Destroy(mesh);
        }


        /********************************************************************************************************************************/
        internal static GameObject[] InitializePool(GameObject prefab, int preloadAmount)
        {
            var pool = PGPool.TryGetExistingPool(prefab) ?? new ObjectPool<GameObject>(
                () => CreateSetup(prefab),
                GetSetup,
                ReleaseSetup,
                DestroySetup,
                true,
                preloadAmount);
            return PGPool.Preload(prefab, pool, preloadAmount);
        }

        /********************************************************************************************************************************/

        private static GameObject CreateSetup(GameObject prefab)
        {
            var obj = Object.Instantiate(prefab);
            return obj;
        }

        private static void GetSetup(GameObject obj)
        {
#if UNITY_EDITOR
            if (!obj) DebugHandler.EmptyPooledObject();
            if (SO_GlobalSettings.Instance.hidePooledObjects) obj.hideFlags = HideFlags.None;
#endif
            obj.SetActive(true);
        }

        private static void ReleaseSetup(GameObject obj)
        {
#if UNITY_EDITOR
            if (!obj) DebugHandler.EmptyPooledObject();
            if (SO_GlobalSettings.Instance.hidePooledObjects) obj.hideFlags = HideFlags.HideInHierarchy;
#endif

            obj.transform.SetParent(null);
            Object.DontDestroyOnLoad(obj);
            obj.transform.localScale = Vector3.one;
            obj.SetActive(false);
        }

        private static void DestroySetup(GameObject obj)
        {
            if(Application.isPlaying) Object.Destroy(obj);
        }
    }
}