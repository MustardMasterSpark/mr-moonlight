using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartReplacer
{
    public static class SRExtensions
    {
        /// <summary>
        /// Loop a list of transforms and get a list of all avaliable components
        /// </summary>
        /// <param name="targets">Target transforms</param>
        /// <returns>Unique components</returns>
        public static HashSet<System.Type> GetObjectArrayComponentTypes(this List<GameObject> targets)
        {
            HashSet<System.Type> avaliableComponents = new HashSet<System.Type>();
            foreach (var item in targets)
            {
                foreach (var comp in item.GetComponentTypes())
                {
                    avaliableComponents.Add(comp);
                }
            }
            return avaliableComponents;
        }

        public static List<System.Type> GetComponentTypes(this GameObject target)
        {
            if (target == null)
                return null;

            List<System.Type> avaliableComponents = new List<System.Type>();

            Component[] sourceComponents = target.GetComponents(typeof(Component));
            foreach (var comp in sourceComponents)
            {
                avaliableComponents.Add(comp.GetType());
            }  
            return avaliableComponents;
        }

        public static bool HasCorrespondingPrefab(this GameObject target, out UnityEngine.Object prefab)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target).gameObject;
                return true;
            }
            prefab = null;
            return false;
        }

        public static List<GameObject> GetSceneInstances(this List<GameObject> prefabList)
        {
            List<GameObject> result = new List<GameObject>();

            #if UNITY_2023_1_OR_NEWER
                GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            #else
                GameObject[] allObjects = (GameObject[])Object.FindObjectsOfType(typeof(GameObject));
            #endif


            foreach (var obj in allObjects)
            {
                var other = PrefabUtility.GetCorrespondingObjectFromSource(obj);
                if (prefabList.Contains(other))
                    result.Add(obj);
            }
            return result;
        }
}
}