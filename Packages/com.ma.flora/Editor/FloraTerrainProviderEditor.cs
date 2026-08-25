using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Pool;
using Random = UnityEngine.Random;

namespace MA.Flora.Editor
{
    [CustomEditor(typeof(FloraTerrainProvider))]
    [CanEditMultipleObjects]
    internal class FloraTerrainProviderEditor : UnityEditor.Editor
    {
        private EditorPrefBool m_ShowTrees;
        private EditorPrefBool m_ShowDetails;

        private List<TreePrototype> m_TreePrototypes = new();
        private ReorderableList m_TreePrototypesList;

        private List<DetailPrototype> m_DetailPrototypes = new();
        private ReorderableList m_DetailPrototypeList;
        private bool[] m_DetailFoldouts;
        private bool m_HasRecordedTerrainDataUndo;

        private Terrain m_Terrain;
        private TerrainData m_TerrainData;

        private const int DetailFoldoutLineCount = 16;
        private const float HelperBoxHeight = 40f;

        private static class Styles
        {
            public static readonly GUIContent TreeDistance = EditorGUIUtility.TrTextContent(
                "Tree Distance",
                "The distance at which trees are loaded from the terrain, and the maximum distance at which trees are rendered.");

            public static readonly GUIContent DetailDistance = EditorGUIUtility.TrTextContent(
                "Detail Distance",
                "The distance at which details are loaded from the terrain, and the maximum distance at which details are rendered.");

            public static readonly GUIContent DetailDensityScale = EditorGUIUtility.TrTextContent(
                "Detail Density Scale",
                "Scale factor applied to the detail density, affecting how many details are rendered per unit area.");
        }

        private void OnEnable()
        {
            bool hasMultipleSelection = targets.Length > 1;
            if (!hasMultipleSelection)
            {
                m_ShowTrees = new EditorPrefBool("FloraTerrainProviderEditor.ShowTrees", true);
                m_ShowDetails = new EditorPrefBool("FloraTerrainProviderEditor.ShowDetails", true);

                m_Terrain = ((FloraTerrainProvider)target).GetComponent<Terrain>();
                m_TerrainData = m_Terrain.terrainData;
                if (m_TerrainData == null)
                    return;

                InitTreePrototypeList();
                InitDetailPrototypeList();

                FloraTerrainProvider.TerrainDataChanged += OnTerrainDataChanged;
            }
        }

        private void OnDisable()
        {
            FloraTerrainProvider.TerrainDataChanged -= OnTerrainDataChanged;
        }

        private void OnTerrainDataChanged(Terrain terrain, TerrainChangedFlags changedFlags)
        {
            InitTreePrototypes();
            InitDetailPrototypes();
        }

        public override void OnInspectorGUI()
        {
            bool hasMultipleSelection = targets.Length > 1;
            if (hasMultipleSelection)
            {
                using var _ = ListPool<Terrain>.Get(out var terrains);
                foreach (var t in targets)
                {
                    var terrain = ((FloraTerrainProvider)t).GetComponent<Terrain>();
                    if (terrain)
                        terrains.Add(terrain);
                }

                if (terrains.Count == 0)
                    return;

                // Draw only detail and tree distance fields
                const float eps = 0.001f;
                bool hasDifferentTreeDistances = false;
                bool hasDifferentDetailDistances = false;
                bool hasDifferentDetailDensities = false;
                float seedTree = terrains[0].treeDistance;
                float seedDetail = terrains[0].detailObjectDistance;
                float seedDetailDensity = terrains[0].detailObjectDensity;
                for (int i = 1; i < terrains.Count; i++)
                {
                    if (Mathf.Abs(terrains[i].treeDistance - seedTree) > eps) hasDifferentTreeDistances = true;
                    if (Mathf.Abs(terrains[i].detailObjectDistance - seedDetail) > eps) hasDifferentDetailDistances = true;
                    if (Mathf.Abs(terrains[i].detailObjectDensity - seedDetailDensity) > eps) hasDifferentDetailDensities = true;
                }


                var prevMixedValue = EditorGUI.showMixedValue;

                EditorGUI.BeginChangeCheck();

                EditorGUI.showMixedValue = hasDifferentTreeDistances;
                EditorGUI.BeginChangeCheck();
                float newTreeDistance = EditorGUILayout.DelayedFloatField(Styles.TreeDistance, seedTree);
                bool treeChanged = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = hasDifferentDetailDistances;
                EditorGUI.BeginChangeCheck();
                float newDetailDistance = EditorGUILayout.DelayedFloatField(Styles.DetailDistance, seedDetail);
                bool detailChanged = EditorGUI.EndChangeCheck();

                EditorGUI.showMixedValue = hasDifferentDetailDensities;
                EditorGUI.BeginChangeCheck();
                float newDetailDensity = EditorGUILayout.Slider(Styles.DetailDensityScale, seedDetailDensity, 0f, 1f);
                bool densityChanged = EditorGUI.EndChangeCheck();

                if (treeChanged || detailChanged || densityChanged)
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();

                    foreach (var terrain in terrains)
                    {
                        bool dirtied = false;

                        if (treeChanged)
                        {
                            float v = Mathf.Max(0f, newTreeDistance);
                            if (!Mathf.Approximately(terrain.treeDistance, v))
                            {
                                Undo.RecordObject(terrain, "Change Terrain Tree Distance");
                                terrain.treeDistance = v;
                                dirtied = true;
                            }
                        }

                        if (detailChanged)
                        {
                            float v = Mathf.Max(8f, newDetailDistance);
                            if (!Mathf.Approximately(terrain.detailObjectDistance, v))
                            {
                                Undo.RecordObject(terrain, "Change Terrain Detail Distance");
                                terrain.detailObjectDistance = v;
                                dirtied = true;
                            }
                        }

                        if (densityChanged)
                        {
                            float v = Mathf.Clamp01(newDetailDensity);
                            if (!Mathf.Approximately(terrain.detailObjectDensity, v))
                            {
                                Undo.RecordObject(terrain, "Change Terrain Detail Density");
                                terrain.detailObjectDensity = v;
                                dirtied = true;
                            }
                        }

                        if (dirtied)
                        {
                            EditorUtility.SetDirty(terrain);
                            if (terrain.terrainData)
                                EditorUtility.SetDirty(terrain.terrainData);

                            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                        }
                    }

                    Undo.CollapseUndoOperations(group);
                }

                EditorGUI.showMixedValue = prevMixedValue;
            }
            else
            {
                if (m_TerrainData == null)
                {
                    EditorGUILayout.HelpBox(L10n.Tr("TerrainData is not set. Please ensure the Terrain component is properly configured."), MessageType.Error);
                    return;
                }

                m_HasRecordedTerrainDataUndo = false;

                EditorGUILayout.Space(-5);
                m_ShowTrees.value = CoreEditorUtils.DrawHeaderFoldout(L10n.Tr("Trees"), m_ShowTrees.value);
                if (m_ShowTrees.value)
                {
                    EditorGUI.BeginChangeCheck();
                    var treeDistance = Mathf.Max(0, EditorGUILayout.FloatField(Styles.TreeDistance, m_Terrain.treeDistance));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(m_Terrain, "Change Terrain Tree Distance");
                        m_Terrain.treeDistance = treeDistance;

                        EditorUtility.SetDirty(m_Terrain);
                        if (m_Terrain.terrainData)
                            EditorUtility.SetDirty(m_Terrain.terrainData);

                        EditorSceneManager.MarkSceneDirty(m_Terrain.gameObject.scene);
                    }

                    m_TreePrototypesList.DoLayoutList();
                    EditorGUILayout.Space(4);
                }

                CoreEditorUtils.DrawSplitter();

                m_ShowDetails.value = CoreEditorUtils.DrawHeaderFoldout(L10n.Tr("Details"), m_ShowDetails.value);
                if (m_ShowDetails.value)
                {
                    EditorGUI.BeginChangeCheck();
                    var detailObjectDistance = Mathf.Max(8, EditorGUILayout.FloatField(Styles.DetailDistance, m_Terrain.detailObjectDistance));
                    var detailObjectDensity = EditorGUILayout.Slider(Styles.DetailDensityScale, m_Terrain.detailObjectDensity, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(m_Terrain, "Change Terrain Detail Settings");
                        m_Terrain.detailObjectDistance = detailObjectDistance;
                        m_Terrain.detailObjectDensity = detailObjectDensity;

                        EditorUtility.SetDirty(m_Terrain);
                        if (m_Terrain.terrainData)
                            EditorUtility.SetDirty(m_Terrain.terrainData);

                        EditorSceneManager.MarkSceneDirty(m_Terrain.gameObject.scene);
                    }

                    m_DetailPrototypeList.DoLayoutList();
                    EditorGUILayout.Space(4);
                }
                else
                {
                    EditorGUILayout.Space(-7);
                }
            }
        }

        private void RecordTerrainDataUndo(string action)
        {
            if (m_HasRecordedTerrainDataUndo)
                return;

            m_HasRecordedTerrainDataUndo = true;
            Undo.RegisterCompleteObjectUndo(m_TerrainData, action);
        }

        // ------------------------------------------------------------------------
        #region Tree Prototypes
        // ------------------------------------------------------------------------

        private void InitTreePrototypes()
        {
            var provider = (FloraTerrainProvider)target;
            var treePrototypes = provider.TerrainData.treePrototypes;

            m_TreePrototypes.Clear();
            m_TreePrototypes.AddRange(treePrototypes);
        }

        private void InitTreePrototypeList()
        {
            InitTreePrototypes();

            m_TreePrototypesList = new ReorderableList(
                m_TreePrototypes,
                typeof(TreePrototype),
                draggable: false,
                displayHeader: false,
                displayAddButton: true,
                displayRemoveButton: true
            )
            {
                multiSelect = true,
            };

            m_TreePrototypesList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, L10n.Tr("Tree Prototypes"));
            };

            // We'll define a fixed element height for tree prototypes
            m_TreePrototypesList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;

            m_TreePrototypesList.drawElementCallback = (rect, index, active, focused) =>
            {
                var treeProto = m_TreePrototypes[index];
                float lineHeight = EditorGUIUtility.singleLineHeight;
                float yOffset = (rect.height - lineHeight) * 0.5f; // center

                // Preview icon
                const float imageSize = 20f;
                Rect previewRect = new Rect(rect.x, rect.y + (rect.height - imageSize)*0.5f, imageSize, imageSize);
                Texture2D previewTex = treeProto.prefab ? AssetPreview.GetAssetPreview(treeProto.prefab) : null;
                if (previewTex)
                    GUI.DrawTexture(previewRect, previewTex, ScaleMode.ScaleToFit);

                // Object field
                float xOffset = previewRect.xMax + 5f;
                float fieldWidth = rect.width - (imageSize + 5f) - 50f; // minus Clear btn
                Rect objectFieldRect = new Rect(xOffset, rect.y + yOffset, fieldWidth, lineHeight);

                EditorGUI.BeginChangeCheck();
                GameObject newPrefab = (GameObject)EditorGUI.ObjectField(
                    objectFieldRect,
                    treeProto.prefab,
                    typeof(GameObject),
                    false
                );

                if (EditorGUI.EndChangeCheck())
                {
                    treeProto.prefab = newPrefab;
                    m_TreePrototypes[index] = treeProto;

                    // Push changes
                    RecordTerrainDataUndo("Change Tree Prototypes");
                    m_TerrainData.treePrototypes = m_TreePrototypes.ToArray();
                }

                // Clear button
                Rect clearBtnRect = new Rect(rect.x + rect.width - 50f, rect.y + yOffset, 50f, lineHeight);
                if (GUI.Button(clearBtnRect, "Clear"))
                {
                    var terrain = ((FloraTerrainProvider)target).Terrain;
                    var terrainData = terrain.terrainData;
                    Undo.RecordObject(terrainData, "Clear Tree Layer");

                    var oldInstances = terrainData.treeInstances;
                    var newInstances = new List<TreeInstance>();
                    for (int i = 0; i < oldInstances.Length; i++)
                    {
                        // Keep only those not referencing this index
                        if (oldInstances[i].prototypeIndex != index)
                            newInstances.Add(oldInstances[i]);
                    }

                    terrainData.treeInstances = newInstances.ToArray();
                }
            };

            m_TreePrototypesList.onAddCallback = list =>
            {
                var excludedItems = new List<GameObject>();
                for (int i = 0; i < m_TreePrototypes.Count; i++)
                {
                    if (m_TreePrototypes[i].prefab != null)
                        excludedItems.Add(m_TreePrototypes[i].prefab);
                }

                FloraAssetPickers.ShowModelPicker(excludedItems, true, selectedGameObjects =>
                {
                    RecordTerrainDataUndo("Add Tree Prototypes");

                    foreach (var go in selectedGameObjects)
                        m_TreePrototypes.Add(new TreePrototype { prefab = go });

                    m_TerrainData.treePrototypes = m_TreePrototypes.ToArray();
                });
            };

            m_TreePrototypesList.onRemoveCallback = list =>
            {
                RecordTerrainDataUndo("Remove Tree Prototypes");

                var sortedIndices = new List<int>(list.selectedIndices).ToArray();
                Array.Sort(sortedIndices);

                for (var i = sortedIndices.Length - 1; i >= 0; i--)
                {
                    int index = sortedIndices[i];
                    m_TreePrototypes.RemoveAt(index);
                    m_TerrainData.RemoveTreePrototype(index);
                }

                m_TerrainData.RefreshPrototypes();
            };
        }

        #endregion

        // ------------------------------------------------------------------------
        #region Detail Prototypes
        // ------------------------------------------------------------------------

        private void InitDetailPrototypes()
        {
            var provider = (FloraTerrainProvider)target;
            var terrain = provider.GetComponent<Terrain>();
            var detailPrototypes = terrain.terrainData.detailPrototypes;

            m_DetailPrototypes.Clear();
            m_DetailPrototypes.AddRange(detailPrototypes);
            Array.Resize(ref m_DetailFoldouts, m_DetailPrototypes.Count);
        }

        private void InitDetailPrototypeList()
        {
            InitDetailPrototypes();

            m_DetailPrototypeList = new ReorderableList(
                m_DetailPrototypes,
                typeof(DetailPrototype),
                draggable: false,
                displayHeader: false,
                displayAddButton: true,
                displayRemoveButton: true
            )
            {
                multiSelect = true,
            };

            m_DetailPrototypeList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, L10n.Tr("Detail Prototypes"));
            };

            // We'll compute the row height dynamically depending on whether the foldout is open
            m_DetailPrototypeList.elementHeightCallback = (index) =>
            {
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                if (index < 0 || index >= m_DetailFoldouts.Length)
                    return EditorGUIUtility.singleLineHeight + spacing;

                float lineHeight = EditorGUIUtility.singleLineHeight;
                // Enough space for the main row (which includes a 20px image)
                float imageSize = 20f;
                float baseRowHeight = Mathf.Max(lineHeight, imageSize) + spacing;

                // If folded out, add space for the extra fields.
                if (m_DetailFoldouts[index])
                {
                    // We have 18 extra lines in the foldout
                    baseRowHeight += (lineHeight + spacing) * DetailFoldoutLineCount;

                    var prototype = m_DetailPrototypes[index];
                    if (!prototype.usePrototypeMesh || prototype.prototype == null)
                        baseRowHeight += HelperBoxHeight + spacing;
                }

                return baseRowHeight;
            };

            m_DetailPrototypeList.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index < 0 || index >= m_DetailPrototypes.Count)
                    return;

                var detail = m_DetailPrototypes[index];

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float spacing = EditorGUIUtility.standardVerticalSpacing;
                const float imageSize = 20f;

                float mainRowHeight = lineHeight + spacing;
                float iconY = rect.y + (mainRowHeight - imageSize) * 0.5f + 2;
                float textY = rect.y + (mainRowHeight - lineHeight) * 0.5f + 2;

                // --- 1) Thumbnail (far left) ---
                bool hasError = (detail.usePrototypeMesh && detail.prototype == null) || (!detail.usePrototypeMesh && detail.prototypeTexture == null);
                Rect previewRect = new Rect(rect.x, iconY, imageSize, imageSize);
                if (hasError)
                {
                    // Draw icon
                    var smallIconRect = new Rect(previewRect.x + 2, previewRect.y + 2, previewRect.width - 4, previewRect.height - 4);
                    var icon = EditorGUIUtility.IconContent("console.erroricon.sml").image;
                    GUI.DrawTexture(smallIconRect, icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    var texture = detail.usePrototypeMesh ? (detail.prototype ? AssetPreview.GetAssetPreview(detail.prototype) : null) : detail.prototypeTexture;
                    GUI.DrawTexture(previewRect, texture, ScaleMode.ScaleToFit);
                }

                // --- 2) Foldout arrow (move it well to the right of the thumbnail) ---
                Rect foldoutRect = new Rect(previewRect.xMax + 16f, iconY, 4f, lineHeight);
                m_DetailFoldouts[index] = EditorGUI.Foldout(foldoutRect, m_DetailFoldouts[index], GUIContent.none);

                // --- 3) Clear button (far right) ---
                float clearBtnWidth = 50f;
                Rect clearBtnRect = new Rect(rect.x + rect.width - clearBtnWidth, textY, clearBtnWidth, lineHeight);

                // --- 4) Object field (between foldout arrow and Clear button) ---
                float fieldStartX = foldoutRect.xMax;
                float marginBetween = 5f;
                float fieldWidth = (clearBtnRect.xMin - marginBetween) - fieldStartX;

                Rect objectFieldRect = new Rect(fieldStartX, textY, fieldWidth, lineHeight);

                // DRAW the object field
                if (detail.usePrototypeMesh)
                {
                    EditorGUI.BeginChangeCheck();
                    GameObject prototypeGameObject = detail.prototype.RootGameObject();
                    prototypeGameObject = (GameObject)EditorGUI.ObjectField(objectFieldRect, prototypeGameObject, typeof(GameObject), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        detail.prototype = GetCompatibleDetailGameObject(prototypeGameObject);
                        m_DetailPrototypes[index] = detail;
                        PushDetailChangesToTerrain();
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    Texture2D detailTexture = detail.prototypeTexture;
                    detailTexture = (Texture2D)EditorGUI.ObjectField(objectFieldRect, detailTexture, typeof(Texture2D), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        detail.prototypeTexture = detailTexture;
                        m_DetailPrototypes[index] = detail;
                        PushDetailChangesToTerrain();
                    }
                }

                // DRAW the Clear button
                if (GUI.Button(clearBtnRect, L10n.Tr("Clear")))
                {
                    RecordTerrainDataUndo("Clear Detail Layer");

                    int w = m_TerrainData.detailWidth;
                    int h = m_TerrainData.detailHeight;
                    int[,] emptyMap = new int[h, w];
                    m_TerrainData.SetDetailLayer(0, 0, index, emptyMap);
                }

                rect.y += mainRowHeight + spacing;

                if (m_DetailFoldouts[index])
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();

                    Rect helpBoxRect = new Rect(rect.x, rect.y, rect.width, HelperBoxHeight);
                    if (detail.usePrototypeMesh && detail.prototype == null)
                    {
                        EditorGUI.HelpBox(helpBoxRect, L10n.Tr("Use Prototype Mesh is enabled, but Prototype mesh is not assigned."), MessageType.Error);
                        rect.y += HelperBoxHeight + spacing;
                    }
                    else if (!detail.usePrototypeMesh && detail.prototypeTexture == null)
                    {
                        EditorGUI.HelpBox(helpBoxRect, L10n.Tr("Billboard mode is enabled, but Prototype Texture is not assigned."), MessageType.Error);
                        rect.y += HelperBoxHeight + spacing;
                    }

                    // usePrototypeMesh
                    detail.usePrototypeMesh = EditorGUI.Toggle(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Use Prototype Mesh"), detail.usePrototypeMesh);
                    rect.y += lineHeight + spacing;
                    if (detail.usePrototypeMesh && detail.renderMode != DetailRenderMode.VertexLit)
                        detail.renderMode = DetailRenderMode.VertexLit;

                    // renderMode
                    using (new EditorGUI.DisabledScope(detail.usePrototypeMesh))
                    {
                        detail.renderMode = (DetailRenderMode)EditorGUI.EnumPopup(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Render Mode"), detail.renderMode);
                        if (!detail.usePrototypeMesh && detail.renderMode == DetailRenderMode.VertexLit)
                            detail.renderMode = DetailRenderMode.Grass;
                        rect.y += lineHeight + spacing;
                    }

                    // minWidth
                    detail.minWidth = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Min Width"), detail.minWidth);
                    rect.y += lineHeight + spacing;

                    // maxWidth
                    detail.maxWidth = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Max Width"), detail.maxWidth);
                    rect.y += lineHeight + spacing;

                    // minHeight
                    detail.minHeight = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Min Height"), detail.minHeight);
                    rect.y += lineHeight + spacing;

                    // maxHeight
                    detail.maxHeight = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Max Height"), detail.maxHeight);
                    rect.y += lineHeight + spacing;

                    // noiseSeed
                    detail.noiseSeed = EditorGUI.IntField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Noise Seed"), detail.noiseSeed);
                    rect.y += lineHeight + spacing;

                    // noiseSpread
                    detail.noiseSpread = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Noise Spread"), detail.noiseSpread);
                    rect.y += lineHeight + spacing;

                    // density
                    detail.density = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Density"), detail.density);
                    rect.y += lineHeight + spacing;

                    // holeEdgePadding
                    detail.holeEdgePadding = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Hole Edge Padding"), detail.holeEdgePadding);
                    rect.y += lineHeight + spacing;

                    // healthyColor
                    detail.healthyColor = EditorGUI.ColorField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Healthy Color"), detail.healthyColor);
                    rect.y += lineHeight + spacing;

                    // dryColor
                    detail.dryColor = EditorGUI.ColorField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Dry Color"), detail.dryColor);
                    rect.y += lineHeight + spacing;

                    // targetCoverage (0..1)
                    detail.targetCoverage = EditorGUI.Slider(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Target Coverage"), detail.targetCoverage, 0f, 1f);
                    rect.y += lineHeight + spacing;

                    // useDensityScaling
                    detail.useDensityScaling = EditorGUI.Toggle(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Use Density Scaling"), detail.useDensityScaling);
                    rect.y += lineHeight + spacing;

                    // alignToGround (0..1)
                    detail.alignToGround = EditorGUI.Slider(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Align To Ground"), detail.alignToGround, 0f, 1f);
                    rect.y += lineHeight + spacing;

                    // positionJitter
                    detail.positionJitter = EditorGUI.FloatField(new Rect(rect.x, rect.y, rect.width, lineHeight), L10n.Tr("Position Jitter"), detail.positionJitter);
                    rect.y += lineHeight + spacing;

                    if (EditorGUI.EndChangeCheck())
                    {
                        m_DetailPrototypes[index] = detail;
                        PushDetailChangesToTerrain();
                    }
                    EditorGUI.indentLevel--;
                }
            };

            m_DetailPrototypeList.onAddCallback = list =>
            {
                var excludedItems = new List<GameObject>();
                for (int i = 0; i < m_DetailPrototypes.Count; i++)
                {
                    if (m_DetailPrototypes[i].prototype != null)
                        excludedItems.Add(m_DetailPrototypes[i].prototype);
                }

                FloraAssetPickers.ShowModelPicker(excludedItems, true, selectedGameObjects =>
                {
                    RecordTerrainDataUndo("Add Detail Prototypes");

                    foreach (var gameObject in selectedGameObjects)
                    {
                        m_DetailPrototypes.Add(new DetailPrototype
                        {
                            prototype = GetCompatibleDetailGameObject(gameObject),
                            prototypeTexture = null,
                            minWidth = 1,
                            maxWidth = 2,
                            minHeight = 1,
                            maxHeight = 2,
                            noiseSeed = Random.Range(1, int.MaxValue),
                            noiseSpread = 0.1f,
                            density = 1f,
                            holeEdgePadding = 0f,
                            healthyColor = Color.white,
                            dryColor = Color.gray,
                            renderMode = DetailRenderMode.VertexLit,
                            usePrototypeMesh = true,
                            useInstancing = true,
                            targetCoverage = 1f,
                            useDensityScaling = true,
                            alignToGround = 1f,
                            positionJitter = 0f,
                        });
                    }

                    m_TerrainData.detailPrototypes = m_DetailPrototypes.ToArray();
                });
            };

            m_DetailPrototypeList.onRemoveCallback = list =>
            {
                RecordTerrainDataUndo("Remove Detail Prototypes");

                var sortedIndices = new List<int>(list.selectedIndices);
                sortedIndices.Sort();

                for (var i = sortedIndices.Count - 1; i >= 0; i--)
                {
                    int idx = sortedIndices[i];
                    m_DetailPrototypes.RemoveAt(idx);
                    m_TerrainData.RemoveDetailPrototype(idx);
                    m_TerrainData.RefreshPrototypes();
                }
            };
        }

        private static GameObject GetCompatibleDetailGameObject(GameObject prefab)
        {
            if (prefab == null)
                return null;

            if (prefab.TryGetComponent(out Renderer _))
                return prefab;

            if (prefab.TryGetComponent(out LODGroup lodGroup))
            {
                var lods = lodGroup.GetLODs();
                foreach (LOD lod in lods)
                {
                    foreach (Renderer renderer in lod.renderers)
                    {
                        if (renderer != null)
                            return renderer.gameObject;
                    }
                }
            }

            return null;
        }

        private void PushDetailChangesToTerrain()
        {
            RecordTerrainDataUndo("Change Detail Prototypes");
            m_TerrainData.detailPrototypes = m_DetailPrototypes.ToArray();
        }

        #endregion
    }
}
