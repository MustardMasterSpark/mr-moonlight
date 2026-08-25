// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using PrefabUtility = UnityEditor.PrefabUtility;

namespace MA.Flora.Editor
{
    internal static class FloraMenuItems
    {
        internal const string RenderingInspectorMenuPathForTests = "Window/Flora/Rendering Inspector";

        #region Global Settings Volume

        [MenuItem("GameObject/Flora/Global Settings Volume", priority = CoreUtils.Priorities.gameObjectMenuPriority + 1)]
        private static void CreateSceneSettingsGameObject(MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var settings = CoreEditorUtils.CreateGameObject("Flora Global Volume", parent);

            var profile = VolumeProfileFactory.CreateVolumeProfile(settings.scene, "Flora Settings Profile");
            var renderSettings = VolumeProfileFactory.CreateVolumeComponent<FloraRenderSettings>(profile, false, false);
            renderSettings.MinScreenSize.Override(0.05f);

            var densitySettings = VolumeProfileFactory.CreateVolumeComponent<FloraDensitySettings>(profile, false, false);
            densitySettings.RangeDensityMode.value = FloraDensityMode.RenderersOnly;

            var volume = settings.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        #endregion

        #region Rendering Debugger

        [MenuItem(RenderingInspectorMenuPathForTests, priority = EditorMenuPriority + 1)]
        public static void OpenRenderingInspector()
        {
            FloraRenderingInspectorWindow.OpenWindow();
        }

        [MenuItem("Window/Flora/Rendering Debugger", priority = EditorMenuPriority)]
        public static void OpenRenderingDebugger()
        {
            DebugManager.instance.displayEditorUI = true;

            Type debugWindowType = typeof(DebugState).Assembly.GetType("UnityEditor.Rendering.DebugWindow");
            EditorWindow window = EditorWindow.GetWindow(debugWindowType);
            window.titleContent = L10n.TextContent("Rendering Debugger");

            if (window)
            {
                int index = GetRenderingDebuggerPanelIndex(DebugDisplayFlora.PanelName);
                if (index != -1)
                    DebugManager.instance.RequestEditorWindowPanelIndex(index);
            }
        }

        [MenuItem("Window/Flora/Rendering Debugger", priority = EditorMenuPriority, validate = true)]
        public static bool ValidateRenderingDebugger()
        {
            if (!FloraSystem.Active)
                return false;

            DebugUI.Panel floraPanel = DebugManager.instance.GetPanel(DebugDisplayFlora.PanelName, true);
            return floraPanel != null;
        }

        private static int GetRenderingDebuggerPanelIndex([DisallowNull] string displayName)
        {
            var panels = DebugManager.instance.panels;
            for (int i = 0; i < panels.Count; ++i)
            {
                if (displayName.Equals(panels[i].displayName, StringComparison.InvariantCultureIgnoreCase))
                    return i;
            }

            return -1;
        }

        #endregion

        #region Priority Constants

        public const int EditorMenuPriority = 400;

        private const int GameObjectPrimaryPriority            = 0;
        private const int CreateContainerPriority              = 5;
        private const int ConvertToGameObjectSecondaryPriority = 100;
        private const int ConvertToInstanceSecondaryPriority   = 100;
        private const int ConvertToTreeSecondaryPriority       = 20;
        private const int CombineContainerSecondaryPriority    = 50;

        #endregion

        #region Convert To Instance Renderers

        [MenuItem("GameObject/Convert To Instance Renderer(s)", true, GameObjectPrimaryPriority, secondaryPriority = ConvertToInstanceSecondaryPriority)]
        [MenuItem("CONTEXT/LODGroup/Convert To Instance Renderer", true)]
        [MenuItem("CONTEXT/MeshRenderer/Convert To Instance Renderer", true)]
        private static bool ConvertToInstancesCommandValidate(MenuCommand menuCommand)
        {
            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return false;

            foreach (var gameObject in selectedObjects)
                if (SelectionHierarchyContainsConvertableGameObjects(gameObject, includeInstanceRenderers: false, requirePrefabInstance: false))
                    return true;

            return false;
        }

        [MenuItem("GameObject/Convert To Instance Renderer(s)", false, GameObjectPrimaryPriority, secondaryPriority = ConvertToInstanceSecondaryPriority)]
        [MenuItem("CONTEXT/LODGroup/Convert To Instance Renderer", false)]
        [MenuItem("CONTEXT/MeshRenderer/Convert To Instance Renderer", false)]
        private static void ConvertToInstancesCommand(MenuCommand menuCommand)
        {
            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return;

            var qualifyingRoots = new HashSet<GameObject>();
            foreach (var go in selectedObjects)
            {
                GetUniqueRenderableRootObjectsRecursively(go, qualifyingRoots, includeInstanceRenderers: false, requirePrefabInstance: false);
            }

            foreach (var root in qualifyingRoots)
            {
                if (!root.TryGetComponent<FloraInstanceRenderer>(out _))
                {
                    Undo.AddComponent<FloraInstanceRenderer>(root);
                }
            }
        }

        #endregion

        #region Convert To Trees

        [MenuItem("GameObject/Convert To Tree", true, GameObjectPrimaryPriority, secondaryPriority = ConvertToTreeSecondaryPriority)]
        [MenuItem("CONTEXT/LODGroup/Convert To Tree", true)]
        [MenuItem("CONTEXT/MeshRenderer/Convert To Tree", true)]
        private static bool ConvertToTreesCommandValidate(MenuCommand menuCommand)
        {
            if (Terrain.activeTerrains.Length == 0)
                return false;

            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return false;

            foreach (var gameObject in selectedObjects)
                if (SelectionHierarchyContainsConvertableGameObjects(gameObject, includeInstanceRenderers: true, requirePrefabInstance: true))
                    return true;

            return Selection.GetFiltered<FloraInstanceContainer>(SelectionMode.Deep | SelectionMode.Editable).Length > 0;
        }

        [MenuItem("GameObject/Convert To Tree", false, GameObjectPrimaryPriority, secondaryPriority = ConvertToTreeSecondaryPriority)]
        [MenuItem("CONTEXT/LODGroup/Convert To Tree", false)]
        [MenuItem("CONTEXT/MeshRenderer/Convert To Tree", false)]
        private static void ConvertToTreesCommand(MenuCommand menuCommand)
        {
            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return;

            var uniqueGameObjects = new HashSet<GameObject>();
            foreach (var gameObject in selectedObjects)
                GetUniqueRenderableRootObjectsRecursively(gameObject, uniqueGameObjects, includeInstanceRenderers: true, requirePrefabInstance: true);

            var prefabInstances = new Dictionary<GameObject, List<PrefabInstanceInfo>>(selectedObjects.Length);
            foreach (var gameObject in uniqueGameObjects)
            {
                var prefab = (GameObject)AssetDatabase.LoadMainAssetAtPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject));
                if (prefab == null)
                    continue;

                if (!prefabInstances.TryGetValue(prefab, out var instances))
                {
                    instances = new List<PrefabInstanceInfo>();
                    prefabInstances.Add(prefab, instances);
                }

                instances.Add(new PrefabInstanceInfo
                {
                    Parent = gameObject.transform.parent,
                    Prefab = prefab,
                    Bounds = prefab.CalculateLocalBounds().TransformBy(gameObject.transform.localToWorldMatrix),
                    Instance = gameObject,
                });
            }

            var instancesToAdd = new List<(int, GameObject)>();
            var treeInstances = new List<TreeInstance>();

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                var terrainPrototypes = terrain.terrainData.treePrototypes;
                var terrainData = terrain.terrainData;
                var terrainBounds = terrainData.bounds.TransformBy(terrain.transform.localToWorldMatrix);
                var terrainSize = terrainData.size;

                treeInstances.Clear();
                treeInstances.AddRange(terrainData.treeInstances);
                var originalTreeInstanceCount = treeInstances.Count;

                foreach (var type in prefabInstances.Keys)
                {
                    var instances = prefabInstances[type];
                    var instancesToAddBounds = AxisAlignedBox.Empty;

                    for (var i = instances.Count - 1; i >= 0; i--)
                    {
                        PrefabInstanceInfo prefabInstanceInfo = instances[i];
                        if (terrainBounds.Intersects(prefabInstanceInfo.Bounds))
                        {
                            instancesToAdd.Add((i, prefabInstanceInfo.Instance));
                            instancesToAddBounds += prefabInstanceInfo.Bounds;
                        }
                    }

                    if (instancesToAdd.Count > 0)
                    {
                        var indexOfPrototype = Array.FindIndex(terrainPrototypes, prototype => prototype.prefab == type);
                        if (indexOfPrototype == -1)
                        {
                            indexOfPrototype = terrainPrototypes.Length;
                            Array.Resize(ref terrainPrototypes, indexOfPrototype + 1);
                            terrainPrototypes[indexOfPrototype] = new TreePrototype { prefab = type };
                        }

                        foreach ((int index, GameObject instance) in instancesToAdd)
                        {
                            var localPosition = (float3)terrain.transform.InverseTransformPoint(instance.transform.position);
                            var localRotation = instance.transform.rotation;
                            var localScale = instance.transform.lossyScale;
                            var treeInstance = new TreeInstance
                            {
                                position = localPosition / terrainSize,
                                widthScale = localScale.x,
                                heightScale = localScale.y,
                                rotation = localRotation.eulerAngles.y * Mathf.Deg2Rad,
                                color = Color.white,
                                lightmapColor = Color.white,
                                prototypeIndex = indexOfPrototype,
                            };

                            treeInstances.Add(treeInstance);
                            instances.RemoveAtSwapBack(index);

                            Undo.DestroyObjectImmediate(instance);
                        }

                        instancesToAdd.Clear();
                    }
                }

                if (originalTreeInstanceCount != treeInstances.Count)
                {
                    Undo.RecordObject(terrain.terrainData, "Convert To Trees");
                    terrainData.treePrototypes = terrainPrototypes;
                    terrainData.SetTreeInstances(treeInstances.ToArray(), true);
                }
            }
        }

        #endregion

        #region Convert From Trees

        [MenuItem("CONTEXT/TerrainEngineTrees/Convert To GameObjects", false, 1000)]
        private static void ConvertFromTreesCommand(MenuCommand item)
        {
            var terrain = (Terrain)item.context;
            var selectedLayerIndex = item.userData;
            var terrainData = terrain.terrainData;
            var treePrototypes = new List<TreePrototype>(terrainData.treePrototypes);
            var selectedPrototype = treePrototypes[selectedLayerIndex];
            var treeInstances = new List<TreeInstance>(terrainData.treeInstances);
            if (treeInstances.Count == 0)
                return;

            var prototypeInstances = new List<TreeInstance>(treeInstances.Count);

            for (int i = treeInstances.Count - 1; i >= 0; i--)
            {
                if (treeInstances[i].prototypeIndex == selectedLayerIndex)
                {
                    prototypeInstances.Add(treeInstances[i]);
                    treeInstances.RemoveAtSwapBack(i);
                }
            }

            if (prototypeInstances.Count == 0)
                return;

            Transform parent = terrain.transform;
            Vector3 terrainPosition = terrain.transform.position;
            foreach (var tree in prototypeInstances)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(selectedPrototype.prefab, parent) as GameObject;
                if (instance)
                {
                    instance.transform.position = Vector3.Scale(tree.position, terrainData.size) + terrainPosition;
                    instance.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale);
                    instance.transform.rotation = Quaternion.AngleAxis(tree.rotation * Mathf.Rad2Deg, Vector3.up);
                    Undo.RegisterCreatedObjectUndo(instance, "Convert Trees To GameObjects");
                }
            }

            treePrototypes.RemoveAt(selectedLayerIndex);

            Undo.RecordObject(terrainData, "Convert Trees To GameObjects");
            terrainData.treeInstances = treeInstances.ToArray();
            terrainData.RemoveTreePrototype(selectedLayerIndex);
        }

        #endregion

        #region Revert To GameObjects

        [MenuItem("GameObject/Revert To GameObject(s)", true, GameObjectPrimaryPriority, secondaryPriority = ConvertToGameObjectSecondaryPriority)]
        [MenuItem("CONTEXT/FloraInstanceRenderer/Revert To GameObject", true)]
        private static bool ConvertInstancesToGameObjectsValidate(MenuCommand menuCommand)
        {
            var containers = GetDeepSelectionOrEmpty<FloraInstanceContainer>(menuCommand);
            var renderers  = GetDeepSelectionOrEmpty<FloraInstanceRenderer>(menuCommand);
            return containers.Length > 0 || renderers.Length > 0;
        }

        [MenuItem("GameObject/Revert To GameObject(s)", false, GameObjectPrimaryPriority, secondaryPriority = ConvertToGameObjectSecondaryPriority)]
        [MenuItem("CONTEXT/FloraInstanceRenderer/Revert To GameObject", false)]
        private static void ConvertInstancesToGameObjects(MenuCommand menuCommand)
        {
            var instanceContainers = GetDeepSelectionOrEmpty<FloraInstanceContainer>(menuCommand);
            var totalInstanceCount = instanceContainers.Sum(container => container.InstanceCount);
            var undoAvailable = totalInstanceCount <= 100000;
            if (!undoAvailable)
            {
                if (!EditorUtility.DisplayDialog(
                        "Revert To GameObjects",
                        "This operation will be irreversible due to the large number of instances. Are you sure you want to continue?",
                        "Continue", "Cancel"))
                {
                    return;
                }
            }

            foreach (var container in instanceContainers)
            {
                if (!container || !container.Prefab)
                    continue;

                var parentTransform = container.transform;
                if (undoAvailable)
                    Undo.RegisterCompleteObjectUndo(container, "Revert To GameObjects");

                for (int instanceIndex = container.InstanceCount - 1; instanceIndex >= 0; instanceIndex--)
                {
                    var prefab = container.Prefab;
                    if (prefab)
                    {
                        var localTransform = container.GetInstanceTransform(instanceIndex, Space.World);
                        var prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(container.Prefab);
                        prefabInstance.transform.position = localTransform.Position;
                        prefabInstance.transform.rotation = localTransform.Rotation;
                        prefabInstance.transform.localScale = localTransform.Scale;
                        prefabInstance.transform.parent = parentTransform;

                        if (undoAvailable)
                            Undo.RegisterCreatedObjectUndo(prefabInstance, "Revert To GameObjects");
                    }
                }

                if (undoAvailable)
                    Undo.DestroyObjectImmediate(container);
                else
                    Object.DestroyImmediate(container);
            }

            var instanceRenderers = GetDeepSelectionOrEmpty<FloraInstanceRenderer>(menuCommand);
            foreach (var rendererGroup in instanceRenderers)
                Undo.DestroyObjectImmediate(rendererGroup);
        }

        #endregion

        #region Convert To Instance Containers

        [MenuItem("GameObject/Create Instance Container(s)", true, GameObjectPrimaryPriority, secondaryPriority = CreateContainerPriority)]
        [MenuItem("CONTEXT/LODGroup/Create Instance Container", true)]
        [MenuItem("CONTEXT/MeshRenderer/Create Instance Container", true)]
        private static bool CreateContainerCommandValidate(MenuCommand menuCommand)
        {
            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return false;

            if (menuCommand?.context is GameObject)
                return selectedObjects.Any(go => SelectionHierarchyContainsConvertableGameObjects(go, includeInstanceRenderers: true, requirePrefabInstance: true));

            foreach (var gameObject in selectedObjects)
                if (SelectionHierarchyContainsConvertableGameObjects(gameObject, includeInstanceRenderers: true, requirePrefabInstance: true))
                    return true;

            return false;
        }

        [MenuItem("GameObject/Create Instance Container(s)", false, GameObjectPrimaryPriority, secondaryPriority = CreateContainerPriority)]
        [MenuItem("CONTEXT/LODGroup/Create Instance Container", false)]
        [MenuItem("CONTEXT/MeshRenderer/Create Instance Container", false)]
        private static void CreateContainerCommand(MenuCommand menuCommand)
        {
            var selectedObjects = GetContextOrTopLevelSelection(menuCommand);
            if (selectedObjects.Length == 0)
                return;

            foreach (var gameObject in selectedObjects)
            {
                if (SelectionHierarchyContainsConvertableGameObjects(gameObject, includeInstanceRenderers: true, requirePrefabInstance: true))
                    Undo.RegisterFullObjectHierarchyUndo(gameObject, "Convert to Instance Container");
            }

            var uniqueGameObjects = new HashSet<GameObject>();
            foreach (var topLevelGO in selectedObjects)
                GetUniqueRenderableRootObjectsRecursively(topLevelGO, uniqueGameObjects, includeInstanceRenderers: true, requirePrefabInstance: true);

            var prefabInstances = new List<PrefabInstanceInfo>(selectedObjects.Length);
            foreach (var gameObject in uniqueGameObjects)
            {
                var prefab = (GameObject)AssetDatabase.LoadMainAssetAtPath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject));
                if (prefab == null)
                    continue;

                prefabInstances.Add(new PrefabInstanceInfo
                {
                    Parent = gameObject.transform.parent,
                    Prefab = prefab,
                    Bounds = prefab.CalculateLocalBounds().TransformBy(gameObject.transform.localToWorldMatrix),
                    Instance = gameObject,
                });
            }

            prefabInstances.Sort(new PrefabInstanceComparer());

            var currentParent = default(Transform);
            var currentSourcePrefab = default(GameObject);
            var createdContainers = new List<FloraInstanceContainer>();

            var instancesToAdd = new List<GameObject>();
            var instancesToAddBounds = AxisAlignedBox.Empty;

            foreach (var prefabInstanceInfo in prefabInstances)
            {
                if (currentParent != prefabInstanceInfo.Parent ||
                    currentSourcePrefab != prefabInstanceInfo.Prefab)
                {
                    if (instancesToAdd.Count > 0)
                    {
                        if (!instancesToAddBounds.IsEmpty)
                        {
                            var container = CreateContainer(currentParent, currentSourcePrefab, instancesToAdd, instancesToAddBounds);
                            createdContainers.Add(container);
                        }

                        instancesToAdd.Clear();
                        instancesToAddBounds = AxisAlignedBox.Empty;
                    }

                    currentParent = prefabInstanceInfo.Parent;
                    currentSourcePrefab = prefabInstanceInfo.Prefab;
                }

                instancesToAdd.Add(prefabInstanceInfo.Instance);
                instancesToAddBounds += prefabInstanceInfo.Bounds;
            }

            if (instancesToAdd.Count > 0)
            {
                if (!instancesToAddBounds.IsEmpty)
                {
                    var container = CreateContainer(currentParent, currentSourcePrefab, instancesToAdd, instancesToAddBounds);
                    createdContainers.Add(container);
                }

                instancesToAdd.Clear();
            }

            var newSelection = createdContainers.ConvertAll(container => container.gameObject).ToArray();
            if (newSelection.Length > 0)
            {
                Selection.objects = newSelection;
                Selection.activeGameObject = newSelection[0];
            }
        }

        #endregion

        #region Combine Instance Containers

        [MenuItem("GameObject/Combine Instance Container(s)", true, GameObjectPrimaryPriority, secondaryPriority = CombineContainerSecondaryPriority)]
        private static bool CombineContainerCommandValidate()
        {
            if (Selection.activeGameObject == null)
                return false;

            var activeContainer = Selection.activeGameObject.GetComponent<FloraInstanceContainer>();
            if (activeContainer == null)
                return false;

            var selectedInstances = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel | SelectionMode.Editable);
            if (selectedInstances.Length < 2)
                return false;

            var activeContainerPrefab = activeContainer.Prefab;
            var instancedMeshContainers = new FloraInstanceContainer[selectedInstances.Length];
            for (int i = 0; i < selectedInstances.Length; i++)
            {
                if (!selectedInstances[i].TryGetComponent(out instancedMeshContainers[i]))
                    return false;

                if (instancedMeshContainers[i].Prefab != activeContainerPrefab)
                    return false;
            }

            return true;
        }

        [MenuItem("GameObject/Combine Instance Container(s)", false, GameObjectPrimaryPriority, secondaryPriority = CombineContainerSecondaryPriority)]
        private static void CombineContainerCommand()
        {
            if (Selection.activeGameObject == null)
                return;

            var activeContainer = Selection.activeGameObject.GetComponent<FloraInstanceContainer>();
            if (activeContainer == null)
                return;

            var selectedInstances = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel | SelectionMode.Editable);
            if (selectedInstances.Length < 2)
                return;

            var instancedMeshContainers = new FloraInstanceContainer[selectedInstances.Length];
            for (int i = 0; i < selectedInstances.Length; i++)
            {
                if (!selectedInstances[i].TryGetComponent(out instancedMeshContainers[i]))
                    return;
            }

            Undo.RegisterFullObjectHierarchyUndo(activeContainer.gameObject, "Combine Instance Containers");

            foreach (var instancedMeshContainer in instancedMeshContainers)
            {
                if (instancedMeshContainer == activeContainer)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(instancedMeshContainer.gameObject, "Combine Instance Containers");

                for (int i = 0; i < instancedMeshContainer.InstanceCount; i++)
                    MoveInstanceToContainer(instancedMeshContainer, i, activeContainer);

                Undo.DestroyObjectImmediate(instancedMeshContainer.gameObject);
            }
        }

        #endregion

        #region Helpers

        private struct PrefabInstanceInfo
        {
            public Transform Parent;
            public GameObject Prefab;
            public GameObject Instance;
            public Bounds Bounds;
            public Vector3 Scale => Instance.transform.lossyScale;
            public Quaternion Rotation => Instance.transform.rotation;
        }

        private struct PrefabInstanceComparer : IComparer<PrefabInstanceInfo>
        {
            public int Compare(PrefabInstanceInfo x, PrefabInstanceInfo y)
            {
                int prefabComparison = CompareGameObjects(x.Prefab, y.Prefab);
                if (prefabComparison != 0) return prefabComparison;

                int parentComparison = CompareParents(x.Parent, y.Parent);
                if (parentComparison != 0) return parentComparison;

                return string.Compare(x.Instance.name, y.Instance.name, StringComparison.Ordinal);
            }

            private static int CompareParents(Transform xParent, Transform yParent)
            {
                if (xParent == yParent) return 0;
                if (xParent == null) return -1;
                if (yParent == null) return 1;
                return xParent.GetEntityId().CompareTo(yParent.GetEntityId());
            }

            private static int CompareGameObjects(GameObject xObject, GameObject yObject)
            {
                if (xObject == yObject) return 0;
                if (xObject == null) return -1;
                if (yObject == null) return 1;
                return xObject.GetEntityId().CompareTo(yObject.GetEntityId());
            }
        }

        private static FloraInstanceContainer CreateContainer(Transform parent, GameObject prefab, List<GameObject> instances, AxisAlignedBox combinedBounds)
        {
            var name = GameObjectUtility.GetUniqueNameForSibling(parent, $"{prefab.name} Container");
            var containerGameObject = new GameObject(name, typeof(FloraInstanceContainer))
            {
                layer = parent != null ? parent.gameObject.layer : 0,
                isStatic = true,
                transform =
                {
                    parent = parent,
                    position = combinedBounds.Center,
                }
            };

            var container = containerGameObject.GetComponent<FloraInstanceContainer>();
            container.Prefab = prefab;
            Undo.RegisterCreatedObjectUndo(containerGameObject, "Convert to Instance Container");

            foreach (var instance in instances)
            {
                container.AddInstance(FloraInstanceTransform.FromUnityTransform(instance.transform, Space.World), Space.World);
                Undo.DestroyObjectImmediate(instance);
            }

            return container;
        }

        private static void MoveInstancesToContainer(FloraInstanceContainer source, NativeArray<int> sourceInstances, FloraInstanceContainer destination)
        {
            foreach (int instanceIndex in sourceInstances)
                MoveInstanceToContainer(source, instanceIndex, destination);
        }

        private static void MoveInstanceToContainer(FloraInstanceContainer source, int sourceInstanceIndex, FloraInstanceContainer destination)
        {
            FloraInstanceTransform localInstanceTransform = source.GetInstanceTransform(sourceInstanceIndex, Space.World);
            destination.AddInstance(localInstanceTransform, Space.World);
        }

        private static bool SelectionHierarchyContainsConvertableGameObjects(GameObject obj, bool includeInstanceRenderers, bool requirePrefabInstance)
        {
            if (obj == null)
                return false;

            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(obj);
            if ((!requirePrefabInstance || isPrefabInstance))
            {
                if (!includeInstanceRenderers && obj.TryGetComponent(out FloraInstanceRenderer _))
                    return false;

                if (HasLODGroup(obj) || IsNonLODGroupMeshRenderer(obj))
                    return true;
            }

            foreach (Transform child in obj.transform)
            {
                if (SelectionHierarchyContainsConvertableGameObjects(child.gameObject, includeInstanceRenderers, requirePrefabInstance))
                    return true;
            }

            return false;
        }

        private static void GetUniqueRenderableRootObjectsRecursively(GameObject obj, HashSet<GameObject> qualifyingRoots, bool includeInstanceRenderers, bool requirePrefabInstance)
        {
            if (obj == null)
                return;

            bool isLODGroup = false;
            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(obj);

            if (!requirePrefabInstance || isPrefabInstance)
            {
                if (!includeInstanceRenderers && obj.TryGetComponent(out FloraInstanceRenderer _))
                    return;

                bool isQualifyingRoot = !requirePrefabInstance || PrefabUtility.IsAnyPrefabInstanceRoot(obj);
                if (isQualifyingRoot)
                {
                    isLODGroup = HasLODGroup(obj);
                    if (isLODGroup || IsNonLODGroupMeshRenderer(obj))
                    {
                        qualifyingRoots.Add(obj);
                    }
                }
            }

            if (isLODGroup)
            {
                // If this is a LODGroup, we don't need to check children for renderers.
                return;
            }

            foreach (Transform child in obj.transform)
            {
                GetUniqueRenderableRootObjectsRecursively(child.gameObject, qualifyingRoots, includeInstanceRenderers, requirePrefabInstance);
            }
        }

        private static GameObject[] GetContextOrTopLevelSelection(MenuCommand menuCommand)
        {
            if (menuCommand?.context is GameObject go && go)
                return new[] { go };

            return Selection.GetFiltered<GameObject>(SelectionMode.TopLevel | SelectionMode.Editable);
        }

        private static T[] GetDeepSelectionOrEmpty<T>(MenuCommand menuCommand) where T : Component
        {
            if (menuCommand?.context is GameObject go && go)
                return go.GetComponentsInChildren<T>(true);

            return Selection.GetFiltered<T>(SelectionMode.Deep | SelectionMode.Editable);
        }

        private static bool ContainsConvertibleRenderers(GameObject root)
        {
            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                if (!HasParentLODGroup(mr.gameObject))
                    return true;
            }

            return false;
        }

        private static bool HasLODGroup(GameObject obj)
        {
            return obj.TryGetComponent<LODGroup>(out _);
        }

        private static bool HasMeshRenderer(GameObject obj)
        {
            return obj.TryGetComponent<MeshRenderer>(out _);
        }

        private static bool IsNonLODGroupMeshRenderer(GameObject obj)
        {
            return HasMeshRenderer(obj) && !HasParentLODGroup(obj);
        }

        private static bool HasParentLODGroup(GameObject obj)
        {
            Transform parent = obj.transform.parent;
            if (parent != null)
                return parent.GetComponentInParent<LODGroup>() != null;

            return false;
        }

        #endregion
    }
}
