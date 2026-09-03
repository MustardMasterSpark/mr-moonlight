// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.GoreSimulator.Editor
{
    [CustomEditor(typeof(MeshParts))]
    public class MeshPartsInspector : UnityEditor.Editor
    {
        private VisualElement container;
        public VisualTreeAsset _visualTree;

        private MeshParts _meshParts;

        private Vector2Field seperationDirection;
        private Slider seperationSlider;

        private ToolbarButton exportMeshes;
        private ToolbarToggle showMeshParts;
        private ListView meshPartsListView;

        protected void OnEnable()
        {
            container = new VisualElement();
            _visualTree.CloneTree(container);
            _meshParts = target as MeshParts;

            FindElements();
            BindElements();
            VisualizeElements();
        }

        private void FindElements()
        {
            exportMeshes = container.Q<ToolbarButton>(nameof(exportMeshes));
            showMeshParts = container.Q<ToolbarToggle>(nameof(showMeshParts));
            meshPartsListView = container.Q<ListView>(nameof(meshPartsListView));

            seperationDirection = container.Q<Vector2Field>(nameof(seperationDirection));
            seperationSlider = container.Q<Slider>(nameof(seperationSlider));
        }

        private void BindElements()
        {
            seperationDirection.PGSetupBindProperty(serializedObject, nameof(MeshParts.seperationDirection));
            seperationSlider.PGSetupBindProperty(serializedObject, nameof(MeshParts.seperationSlider));
        }

        private void VisualizeElements()
        {
            exportMeshes.tooltip = "Exports meshes and unsaved decal materials to the project folder.\n" +
                                   "This is required to share the character across multiple scenes or to create prefabs.";
        }

        /********************************************************************************************************************************/

        public override VisualElement CreateInspectorGUI()
        {
            CreateSeperation();
            CreateExportMeshesButton();
            CreateMeshPartsListView();

            return container;
        }

        /********************************************************************************************************************************/

        private void CreateSeperation()
        {
            seperationSlider.RegisterValueChangedCallback(evt =>
            {
                _meshParts.ApplySeperation();
                SceneView.RepaintAll();
            });
        }

        private void CreateExportMeshesButton()
        {
            exportMeshes.clicked += () =>
            {
                var meshParts = _meshParts.meshParts;

                var folderPath = EditorUtility.OpenFolderPanel("Export Meshes", "Assets", "");

                // User clicked "Cancel".
                if (string.IsNullOrEmpty(folderPath)) return;

                var assetPath = "Assets" + folderPath.Substring(Application.dataPath.Length);

                // Mesh
                for (var i = 0; i < meshParts.Count; i++)
                {
                    var mesh = meshParts[i].meshFilter.sharedMesh;
                    var newMesh = Instantiate(mesh);
                    newMesh.name = meshParts[i].name;
                    var meshPath = assetPath + "/" + newMesh.name + ".asset";

                    AssetDatabase.CreateAsset(newMesh, meshPath);
                    meshParts[i].meshFilter.sharedMesh = newMesh;
                }

                // Materials
                Material decalMaterial = null;
                for (var i = 0; i < meshParts.Count; i++)
                {
                    var meshRenderer = meshParts[i].GetComponent<MeshRenderer>();
                    if (meshRenderer && meshRenderer.sharedMaterials.Length > 0)
                    {
                        var material = meshRenderer.sharedMaterials[^1];

                        // Check if material is already in the project
                        var existingPath = AssetDatabase.GetAssetPath(material);
                        if (string.IsNullOrEmpty(existingPath))
                        {
                            if (!decalMaterial)
                            {
                                decalMaterial = new Material(material)
                                {
                                    name = material.name
                                };
                                var materialPath = assetPath + "/" + decalMaterial.name + ".mat";
                                materialPath = AssetDatabase.GenerateUniqueAssetPath(materialPath);
                                AssetDatabase.CreateAsset(decalMaterial, materialPath);
                            }

                            var materials = meshRenderer.sharedMaterials;
                            materials[^1] = decalMaterial;
                            meshRenderer.sharedMaterials = materials;
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Exported {meshParts.Count} meshes to '{assetPath}'");
                if(decalMaterial) Debug.Log("Exported " + decalMaterial.name +  " to " + assetPath);
            };
        }

        private void CreateMeshPartsListView()
        {
            var skinnedChildrenProperty = serializedObject.FindProperty(nameof(_meshParts.meshParts));
            meshPartsListView.PGSetupObjectListView(skinnedChildrenProperty, _meshParts.meshParts);
            meshPartsListView.PGObjectListViewStyle();

            showMeshParts.RegisterValueChangedCallback(evt => { meshPartsListView.PGDisplayStyleFlex(showMeshParts.value); });
        }
    }
}