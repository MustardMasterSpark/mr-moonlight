using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
using HeurekaGames.Utils;

namespace HeurekaGames.SmartReplacer
{
    public class ComponentTransferWindow : EditorWindow
    {
        private ComponentTransferConfig config;
        private Vector2 scrollPosLeftArea;
        private Vector2 scrollPosRightArea;
        public const string WINDOWNAME = "SmartReplacer";

        public static void ShowConfig(ComponentTransferConfig config)
        {
            ComponentTransferWindow window = ComponentTransferWindow.GetWindow<ComponentTransferWindow>();

            window.ShowUtility();
            window.config = config;
            var tmpRect = window.position;
            tmpRect.width = 600;
            window.position = tmpRect;
            window.titleContent = new GUIContent(WINDOWNAME);
        }

        void OnSelectionChange()
        {
            updateSelection(false);
        }

        void OnGUI()
        {
            Heureka_WindowStyler.DrawGlobalHeader(Heureka_WindowStyler.clr_middleGreen, WINDOWNAME, null, drawAdditionalHeaderIcons);

            //Make sure we have a config
            if (config == null)
            {
                config = new ComponentTransferConfig(Selection.transforms, null, false);
            }

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope("box", GUILayout.ExpandHeight(false)))
                {
                    GUIContent content = new GUIContent();

                    if (config.NewAsset)
                    {
                        content = new GUIContent(AssetPreview.GetAssetPreview(config.NewAsset));
                        EditorGUILayout.LabelField(content, GUILayout.Width(64), GUILayout.Height(64));
                    }

                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.HelpBox("Select the prefab you want to replace with", MessageType.Info);
                        config.NewAsset = EditorGUILayout.ObjectField(config.NewAsset, typeof(GameObject), false);
                    }
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(false)))
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GameObject prefabTypeToToggle = null;
                        GameObject selectionToRemove = null;

                        using (var scrollarea = new EditorGUILayout.ScrollViewScope(scrollPosLeftArea, GUIStyle.none, GUI.skin.verticalScrollbar))
                        {
                            scrollPosLeftArea = scrollarea.scrollPosition;
                            using (new EditorGUILayout.VerticalScope())
                            {
                                if (config.PrefabSelectionList.Count() != 0)
                                {
                                    drawAreaHeader($"Prefab type selection ({config.PrefabSelectionList.Count()})", MessageType.Warning);
                                    foreach (var item in config.PrefabSelectionList)
                                    {
                                        using (new EditorGUILayout.HorizontalScope("box"))
                                        {
                                            if (drawDeleteBtn("Remove prefab type from selection"))
                                            {
                                                prefabTypeToToggle = item;
                                            }

                                            using (new EditorGUI.DisabledScope(true))
                                            {
                                                EditorGUILayout.ObjectField(item, typeof(GameObject), false);
                                            }
                                        }
                                    }
                                }
                                if (config.UniqueInstanceSelection.Count() == 0)
                                {
                                    drawAreaHeader(config.PrefabSelectionList.Count() > 0 ? "No additional sceneassets selected" : "No sceneassets selected", MessageType.Info);
                                }
                                else
                                {
                                    drawAreaHeader(config.PrefabSelectionList.Count()>0?"Additional Scene Selection":"Scene Selection", MessageType.Info);
                                    foreach (var selected in config.UniqueInstanceSelection)
                                    {
                                        GameObject prefab = null;
                                        if (PrefabUtility.IsPartOfPrefabInstance(selected))
                                        {
                                            prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(selected).gameObject;
                                        }

                                        if (prefab != null && config.PrefabSelectionList.Contains(prefab))
                                            continue;
                                        else
                                        {
                                            using (new EditorGUILayout.HorizontalScope())
                                            {
                                                if (drawDeleteBtn("Remove from selection"))
                                                {
                                                    selectionToRemove = selected;
                                                }
                                                Object correspondingPrefab;
                                                using (new EditorGUI.DisabledScope(!selected.HasCorrespondingPrefab(out correspondingPrefab)))
                                                {
                                                    if (drawPrefabBtn("select all instances of prefab in scene"))
                                                    {
                                                        prefabTypeToToggle = prefab;
                                                    }
                                                }
                                                using (new EditorGUI.DisabledScope(true))
                                                {
                                                    EditorGUILayout.ObjectField(selected, typeof(GameObject), true);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (config.elementGroupDict.Count() > 0 && config.NewAsset != null)
                        {
                            using (var scrollarea = new EditorGUILayout.ScrollViewScope(scrollPosRightArea, GUIStyle.none, GUI.skin.verticalScrollbar))
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    foreach (var kvPair in config.elementGroupDict)
                                {
                                        using (new EditorGUILayout.VerticalScope())
                                        {
                                            scrollPosRightArea = scrollarea.scrollPosition;
                                            drawAreaHeader(kvPair.Key, MessageType.Info);

                                            //Draw components
                                            foreach (var item in kvPair.Value)
                                            {
                                                using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandHeight(true)))
                                                {
                                                    string header = $@"{(string.IsNullOrEmpty(item.Key) ? "Custom" : item.Key)}";
                                                    EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
                                                    foreach (var configElement in item)
                                                    {
                                                        drawElement(configElement);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                GUILayout.FlexibleSpace();
                            }
                        }

                        if (selectionToRemove != null)
                        {
                            Transform[] foo1 = Selection.transforms;
                            UnityEngine.Object[] fooObj = Selection.objects;

                            Selection.objects = Selection.objects.Where(x => x != selectionToRemove).ToArray();
                            var foo2 = Selection.transforms;
                            updateSelection(true);
                            selectionToRemove = null;
                        }
                        if (prefabTypeToToggle != null)
                        {
                            config.ToggleSelectionType(prefabTypeToToggle);
                            updateSelection(true);
                        }
                    }

                    //If we want children of selected to be copied over to the new asset
                    //Also tracks layer/tags of original prefab
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        int width = 80;
                        EditorGUILayout.LabelField("Transfer from original:", EditorStyles.boldLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
#if UNITY_2021_1_OR_NEWER
                            config.RetainChildren = EditorGUILayout.ToggleLeft("Children", config.RetainChildren, GUILayout.Width(width));
#endif
                            config.KeepLayer = EditorGUILayout.ToggleLeft("Layer", config.KeepLayer, GUILayout.Width(width));
                            config.KeepTag = EditorGUILayout.ToggleLeft("Tag", config.KeepTag, GUILayout.Width(width));
                            GUILayout.FlexibleSpace();
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!(config.elementGroupDict.Count() > 0 && config.NewAsset != null)))
                        {
                            if (GUILayout.Button("Replace"))
                            {
                                config.ReplaceAssets();
                                //this.Close();
                            }
                        }

                        if (GUILayout.Button("Cancel"))
                            this.Close();
                    }
                }
            }
        }

        private void updateSelection(bool force)
        {
            var sceneSelection = Selection.transforms.Select(t => t.gameObject).ToArray();
            //If transforms[] changed, update configs!!!
            if (config != null && ( force || !config.InstanceSelectionList.Equals(sceneSelection)))
                config.UpdateConfigTransforms(sceneSelection, false);
        }

        private void drawAdditionalHeaderIcons()
        {
            GUILayout.FlexibleSpace();
        }

        private bool drawDeleteBtn(string tooltip)
        {
            return drawButton(new GUIContent(Heureka_ResourceLoader.Icons.Clear, tooltip));
        }

        private bool drawPrefabBtn(string tooltip)
        {
            return drawButton(new GUIContent(Heureka_ResourceLoader.Icons.Pick, tooltip));
        }

        private bool drawButton(GUIContent content)
        {
            return GUILayout.Button(content, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight), GUILayout.Width(EditorGUIUtility.singleLineHeight));
        }

        private void drawAreaHeader(string header, MessageType messageType)
        {
            EditorGUILayout.HelpBox(header, messageType, true);
        }

        private void drawElement(SB_ComponentTransferConfigElement element)
        {
            GUIContent content = new GUIContent($@"Copy {(config.TargetAssetComponents.Contains(element.componentType) ? "Values" : "Component")}", "Make sure this component is transfered to the new prefab");
            content = new GUIContent(element.componentType.Name, "Make sure the values of this component is copied over");
            element.copyValues = EditorGUILayout.ToggleLeft(content, element.copyValues);
        }

        void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}