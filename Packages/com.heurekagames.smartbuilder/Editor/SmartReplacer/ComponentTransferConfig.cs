
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartReplacer
{
    public class ComponentTransferConfig
    {
#if UNITY_2021_1_OR_NEWER
        public bool RetainChildren { get; set; } = true;
#endif
        public List<Type> TargetAssetComponents { get; private set; } = new List<Type>();
        public bool KeepLayer { get; set; } = true;
        public bool KeepTag { get; set; } = true;
        public List<GameObject> UniqueInstanceSelection
        {
            get
            {
                UnityEngine.Object prefab;
                return InstanceSelectionList.Where(x => x!=null 
                && (x.gameObject.HasCorrespondingPrefab(out prefab) && !PrefabSelectionList.Contains(prefab)
                || !x.gameObject.HasCorrespondingPrefab(out prefab))).ToList();
            }
        }

        private UnityEngine.Object newAsset;
        public UnityEngine.Object NewAsset { 
            get { return newAsset; }
            set 
            { 
                newAsset = value;
                updateNewAssetComponents();
                populateConfigGroupDict();
            }
        }

        public GameObject[] InstanceSelectionList;
        public List<GameObject> PrefabSelectionList = new List<GameObject>();
        public int prefabTypeInstanceCount = 0;
        //public IEnumerable<IGrouping<string, SB_ComponentTransferConfigElement>> groupedConfigElements;
        public Dictionary<string, IEnumerable<IGrouping<string, SB_ComponentTransferConfigElement>>> elementGroupDict = new Dictionary<string, IEnumerable<IGrouping<string, SB_ComponentTransferConfigElement>>>();

        private List<SB_ComponentTransferConfigElement> configElements = new List<SB_ComponentTransferConfigElement>();
        private List<ComponentTransfer> componentReferenceTransfers = new List<ComponentTransfer>();

        public ComponentTransferConfig(GameObject[] selected, UnityEngine.Object newAsset, bool selectByType)
        {
            this.NewAsset = newAsset;
            UpdateConfigTransforms(selected, selectByType);
        }
        public ComponentTransferConfig(Transform[] selected, UnityEngine.Object newAsset, bool selectByType) : this(UnityEditor.Selection.transforms.Select(t => t.gameObject).ToArray(), newAsset, selectByType)
        {
        }

        private void updateNewAssetComponents()
        {
            if (NewAsset != null)
                TargetAssetComponents = ((GameObject)NewAsset).GetComponentTypes();
            else
                TargetAssetComponents.Clear();
        }

        public void UpdateConfigTransforms(GameObject[] selected, bool selectByType)
        {
            this.InstanceSelectionList = selected;

            //Get components from all selected objects in scene
            var instanceComponents = selected.Select(x=>x.gameObject).ToList().GetObjectArrayComponentTypes();

            IEnumerable<Type> combinedComponents = instanceComponents;

            //Get all instances in scene of given prefabtype
            if (selectByType)
            {
                List<GameObject> instancesOfPrefabs = PrefabSelectionList.GetSceneInstances();
                prefabTypeInstanceCount = (instancesOfPrefabs != null) ? instancesOfPrefabs.Count : 0;

                //If we have instances to object we want to add to combined components
                if (instancesOfPrefabs != null)
                {
                    var prefabTypeComponents = instancesOfPrefabs.GetObjectArrayComponentTypes();
                    combinedComponents = instanceComponents.Concat(prefabTypeComponents);
                }
            }

            //Remove entries from configelements that dont exist in new selection
            if (combinedComponents != null)
            {
                configElements.RemoveAll(x => !combinedComponents.Any(y => y == x.componentType));
            }

            if (combinedComponents != null)
            {
                foreach (var componentType in combinedComponents)
                {
                    if (!configElements.Any(x => x.componentType == componentType))
                        configElements.Add(new SB_ComponentTransferConfigElement(componentType));
                }
            }

            configElements = configElements.OrderBy(x => x.componentType.Namespace).ToList();

            //Make sure Anything from UnityEngine namespace is last on list (Except for Transform)
            List<SB_ComponentTransferConfigElement> buildinComponents = configElements.Where(x => !string.IsNullOrEmpty(x.componentType.Namespace) && x.componentType.Namespace.StartsWith("UnityEngine")).ToList();
            configElements.RemoveAll(delegate (SB_ComponentTransferConfigElement c) { return buildinComponents.Contains(c); });
            configElements.AddRange(buildinComponents);

            populateConfigGroupDict();
        }

        private void populateConfigGroupDict()
        {
            var existingComponents = configElements.Where(x => TargetAssetComponents.Contains(x.componentType)).GroupBy(x => x.componentType.Namespace);
            var newComponents = configElements.Where(x => !TargetAssetComponents.Contains(x.componentType)).GroupBy(x => x.componentType.Namespace);

            elementGroupDict.Clear();

            if (existingComponents.Count() > 0)
                elementGroupDict.Add("Keep values", existingComponents);
            if (newComponents.Count() > 0)
                elementGroupDict.Add("Keep components", newComponents);
        }

        private void queueSceneReferenceTransfer(ComponentTransfer transfer)
        {
            componentReferenceTransfers.Add(transfer);
        }

        internal void TriggerReferenceTransfers()
        {
            #if UNITY_2023_1_OR_NEWER
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            #else
                var allObjects = (GameObject[])UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
            #endif
            
            for (int j = 0; j < allObjects.Length; j++)
            {
                EditorUtility.DisplayProgressBar("Re-establishing scene references", "Making sure scene references are re-established after replacing assets", ((float)j / (float)allObjects.Length));
                var go = allObjects[j];

                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    var c = components[i];
                    if (!c) continue;

                    var so = new SerializedObject(c);
                    var sp = so.GetIterator();

                    while (sp.NextVisible(true))
                        if (sp.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            foreach (var item in componentReferenceTransfers)
                            {
                                if (sp.objectReferenceValue == item.Source)
                                {
                                    sp.objectReferenceValue = item.Target;
                                    sp.serializedObject.ApplyModifiedProperties();
                                }
                            }
                        }
                }
            }
            EditorUtility.ClearProgressBar();
        }

        public void ReplaceAssets()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Undo replace objects");
            var undoID = Undo.GetCurrentGroup();

            List<GameObject> newGOs = new List<GameObject>();

            var prefabInstances = PrefabSelectionList.GetSceneInstances();

            //Create a list of the current scene selection as well as all instances of the prefabtype list that is not already selected
            List<GameObject> combinedList = InstanceSelectionList.ToList();

            if (prefabInstances!=null)
                combinedList.AddRange(prefabInstances?.Where(x=> !InstanceSelectionList.Contains(x)));

            foreach (var item in combinedList)
            {
                var newGO = PrefabUtility.InstantiatePrefab(NewAsset, item.transform.parent) as GameObject;
                if (newGO == null)
                    continue;

                //Make sure we get proper naming
                try
                {
                    GameObjectUtility.EnsureUniqueNameForSibling(newGO);
                }
                catch
                {
                    Debug.Log($"SmartReplacer failed to rename {newGO.name}");
                }

                //Make sure tags/labels etc are transfered
                if (KeepLayer)
                    newGO.layer = item.layer;
                if (KeepTag)
                    newGO.tag = item.tag;

                Undo.RegisterCreatedObjectUndo(newGO, $"SmartReplacer replace prefabs");

                //Get the components on source and target GO
                Component[] sourceComponents = item.GetComponents(typeof(Component));
                Component[] targetComponents = newGO.GetComponents(typeof(Component));

                //Transfering references to gameobject
                queueSceneReferenceTransfer(new ComponentTransfer(item.gameObject, newGO));

                //Loop all the components on selected gameobject
                foreach (var sourceComp in sourceComponents)
                {
                    if (configElements != null && configElements.Count>0)
                    {
                        //Check if this is marked for value transfer
                        if (configElements.Find(x => x.componentType.Equals(sourceComp.GetType())).copyValues)
                        {
                            //Copy component from source
                            UnityEditorInternal.ComponentUtility.CopyComponent(sourceComp);
                            //If component already exist in target
                            if (targetComponents.Any(x => x.GetType().Equals(sourceComp.GetType())))
                            {
                                var targetComp = targetComponents.First(x => x.GetType().Equals(sourceComp.GetType()));
                                UnityEditorInternal.ComponentUtility.PasteComponentValues(targetComp);
                            }
                            else
                            {
                                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(newGO);
                            }

                            //Todo investigate if this is preferable over the componentvalue copy/pase
                            //go = PrefabUtility.InstantiatePrefab(go, gameObject.transform.parent) as GameObject;
                            //PrefabUtility.SetPropertyModifications(go, PrefabUtility.GetPropertyModifications(gameObject));
                            //------------
                            //Also might be of interest
                            //https://forum.unity.com/threads/solved-duplicate-prefab-issue.778553/

                            queueSceneReferenceTransfer(new ComponentTransfer(sourceComp, newGO.GetComponent(sourceComp.GetType())));
                        }
                    }
                    newGOs.Add(newGO);
                }
#if UNITY_2021_1_OR_NEWER
                //Move "bastard" children to new parent - Waiting till last part of loop to ensure parent transform has been correctly moved
                if (RetainChildren)
                {
                    var parentPrefabHandle = PrefabUtility.GetPrefabInstanceHandle(item);
                    List<Transform> bastardChildren = new List<Transform>();
                    foreach (Transform child in item.transform)
                    {
                        var childHandle = PrefabUtility.GetPrefabInstanceHandle(child);
                        //If child is not default part of prefab, change the parent of the bastard child
                        if (childHandle != parentPrefabHandle)
                            bastardChildren.Add(child);
                    }

                    foreach (var bastard in bastardChildren)
                    {
                        Undo.SetTransformParent(bastard, newGO.transform, false, "Set new parent");
                    }
                }
#endif
            }

            //Update references to source object to new object (Sometimes other scene objects has references to the newly deleted, so we want to reestablish those references to the new object)
            TriggerReferenceTransfers();

            Debug.Log($"{ComponentTransferWindow.WINDOWNAME} replaced {combinedList.Count} objects");
            foreach (var item in combinedList)
            {
                Undo.DestroyObjectImmediate(item.gameObject);
            }
            Undo.CollapseUndoOperations(undoID);

            //Select the new prefabs
            if(newGOs.Count>0)
                Selection.objects = newGOs.ToArray();
        }

        internal void ToggleSelectionType(GameObject prefab)
        {
            if (PrefabSelectionList.Contains(prefab))
                PrefabSelectionList.Remove(prefab);
            else
                PrefabSelectionList.Add(prefab);
        }
    }

    public class SB_ComponentTransferConfigElement
    {
        public Type componentType;
        public bool copyValues = false;

        public SB_ComponentTransferConfigElement(Type type)
        {
            this.componentType = type;

            //We always want to transfer transform values
            if (this.componentType == typeof(Transform))
                copyValues = true;
        }
    }
}
