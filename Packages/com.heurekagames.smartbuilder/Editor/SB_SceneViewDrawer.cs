using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
 #if UNITY_2021_1_OR_NEWER

    [InitializeOnLoad]
    public static class SB_SceneViewDrawer
    {
        private static Rect sceneViewRect;
        private static Vector2 handleSize = new Vector2();
        private static MethodInfo reflectedParentObjectMethod;
        private static Texture refreshImage;
        private static Texture infoImage;
        private static Texture clearImage;
        private static SB_Preferences prefs;
        private static Rect helpBtnRect;
        private static bool allowDrawing;

        static SB_SceneViewDrawer()
        {
            SceneView.duringSceneGui += OnDuringSceneGui;

#region reflected methods
            Assembly assembly = typeof(UnityEditor.EditorWindow).Assembly;
            Type hierarchyType = assembly.GetType("UnityEditor.SceneView");

            reflectedParentObjectMethod = hierarchyType.GetMethod("GetDefaultParentObjectIfSet", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
#endregion
            refreshImage = EditorGUIUtility.IconContent("ParentConstraint Icon").image;
            infoImage = EditorGUIUtility.IconContent("_Help").image;

              #if UNITY_2023_1_OR_NEWER
                string closeBtnName = "clear";
            #else
                string closeBtnName = "winbtn_win_close_h";
            #endif

            clearImage = EditorGUIUtility.IconContent(closeBtnName).image;

            prefs = SB_Preferences.instance;
        }

        static void OnDuringSceneGui(SceneView sceneView)
        {

            if (!prefs.ShowSceneViewHelper.Value || !allowDrawing)
                return;

            var origIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(16,16));

            var areaRect = getAreaRect();

            //Have to do some weird stuff here to subtract the ribbon height from sceneview
            var style = (GUIStyle)"GV Gizmo DropDown";
            Vector2 ribbon = style.CalcSize(sceneView.titleContent);
            sceneViewRect = sceneView.position;
            sceneViewRect.height -= ribbon.y;

            if (SceneView.lastActiveSceneView != null)
            {
                Transform currentParentObject = GetCurrentDefaultParent();

                Handles.BeginGUI();

                GUIStyle areaStyle = new GUIStyle();
                GUILayout.BeginArea(new Rect(areaRect), areaStyle);

                var bgColor = EditorGUIUtility.isProSkin
                    ? (Color)new Color32(56, 56, 56, 255)
                    : (Color)new Color32(194, 194, 194, 255);

                EditorGUI.DrawRect(new Rect(0, 0, areaRect.width, areaRect.height), bgColor);
                Vector2 maxArea = new Vector2(0, 0);

                var selectedNotCurrentParent = (Selection.activeTransform?.parent?.gameObject != null && Selection.activeTransform?.parent != currentParentObject) ;

                using (new EditorGUILayout.VerticalScope())
                {
                    GUIContent content;
                    if (prefs.ShowSceneViewHelperHeader.Value)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            content = new GUIContent("Default Parent");
                            var labelSize = doAreaLabel(content, EditorStyles.boldLabel);

                            GUILayout.FlexibleSpace();

                            content = new GUIContent(infoImage, "Help");
                            var btnSize = doAreaButton(content, SB_Styles.LabelBtnStyle, () => PopupWindow.Show(helpBtnRect, new SB_HelperWindow(OnHelperWindowGUI)));

                            if (Event.current.type == EventType.Repaint) helpBtnRect = GUILayoutUtility.GetLastRect();

                            Vector2 controlSize = getHorizontalAreaSize(new Vector2[] { labelSize, btnSize });
                            appendToArea(ref maxArea, controlSize, true);
                        }
                    }

                    using (var btnScope = new EditorGUILayout.HorizontalScope())
                    {                      
                        if (currentParentObject != null)
                        {
                            content = new GUIContent(clearImage, $"Clear default parent");
                            var btnSize1 = doAreaButton(content, SB_Styles.MiniButtonReduced, () => EditorUtility.ClearDefaultParentObject());

                            content = new GUIContent($"{currentParentObject.name} (Default)", "Currently active default parent");
                            var btnSize2 = doAreaButton(content, SB_Styles.LabelBtnStyle, () => EditorGUIUtility.PingObject(currentParentObject));

                            Vector2 controlSize = getHorizontalAreaSize(new Vector2[] { btnSize1, btnSize2 });
                            appendToArea(ref maxArea, controlSize, true);
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            content = new GUIContent("No default parent");
                            Vector2 controlSize = doAreaLabel(content, SB_Styles.LabelBtnStyle);
                            appendToArea(ref maxArea, controlSize, true);
                        }
                    }

                    using (var btnScope = new EditorGUILayout.HorizontalScope())
                    {
                        if (selectedNotCurrentParent)
                        {
                            var parentName = Selection.activeTransform.parent.gameObject.name;

                            content = new GUIContent(new GUIContent(refreshImage, $"Set prefab parent to {parentName}"));
                            var btnSize1 = doAreaButton(content, SB_Styles.MiniButtonReduced, () => EditorUtility.SetDefaultParentObject(Selection.activeTransform.parent.gameObject));

                            var origColor = GUI.color;
                            if (selectedNotCurrentParent)
                                GUI.color = Color.yellow;

                            content = new GUIContent($"{parentName}", "The parent of the currently selected prefab");
                            var btnSize2 = doAreaButton(content, SB_Styles.LabelBtnStyle, () => EditorGUIUtility.PingObject(Selection.activeTransform.parent.gameObject));

                            GUI.color = origColor;

                            Vector2 controlSize = getHorizontalAreaSize(new Vector2[] { btnSize1, btnSize2 });
                            appendToArea(ref maxArea, controlSize, true);
                            GUILayout.FlexibleSpace();
                        }
                    }

                }

                //Resize handle so it matches content
                handleSize = maxArea;

                GUILayout.EndArea();
                Handles.EndGUI();
                SceneView.lastActiveSceneView.Repaint();
            }

            EditorGUIUtility.SetIconSize(origIconSize);
        }
        public static Transform GetCurrentDefaultParent()
        {
            return (Transform)reflectedParentObjectMethod.Invoke(null, null);
        }
        public static Vector2 OnHelperWindowGUI()
        {
            var fixedWidth = 200f;
            Vector2 windowSize = new Vector2(fixedWidth, 0);

            GUIContent content = new GUIContent("Info on Default Prefab Parent");
            windowSize.y = doAreaLabel(content, EditorStyles.helpBox).y;

            var text = $"You are able to assign a 'Default Parent' in the hierarchy. That means that any prefab you subsequently add will be added as a child of that parent." +
                 $"{ Environment.NewLine }{ Environment.NewLine }When you select a prefab in scene { SB_Preferences.AssetName } gives you a shortcut to use the parent of the selected prefab as the 'Default Parent'." +
                 $"{ Environment.NewLine }{ Environment.NewLine }It also allows you an easy way to clear any 'Default Parent' currently set.";

            var textFieldArea = doAreaTextfield(text, windowSize.x);
            appendToArea(ref windowSize, textFieldArea, true);

            return windowSize;
        }
        private static Vector2 getHorizontalAreaSize(Vector2[] controls)
        {
            Vector2 maxVals = new Vector2();
            for (int i = 0; i < controls.Length; i++)
            {
                maxVals.x += controls[i].x;

                if (controls[i].y > maxVals.y)
                    maxVals.y = controls[i].y;
            }
            return maxVals;
        }
        private static void appendToArea(ref Vector2 currentArea, Vector2 newArea, bool appendY)
        {
            currentArea.x = Mathf.Max(currentArea.x, newArea.x);

            if (appendY)
                currentArea.y += newArea.y;
        }
        private static Vector2 doAreaButton(GUIContent content, GUIStyle style, Action callback)
        {
            var controlRect = style.CalcSize(content);
            if (string.IsNullOrEmpty(content.text))
            {
                if (GUILayout.Button(content, style))
                    callback();
            }
            else if (GUILayout.Button(content, style))
                callback();

            //Padding/margin
            controlRect.y += (style.margin.top + style.margin.bottom);
            controlRect.x += (style.margin.left + style.margin.right);
            return controlRect;
        }
        private static Vector2 doAreaTextfield(string text, float width = 200f)
        {
            var style = new GUIStyle(EditorStyles.textArea);
            style.wordWrap = true;
            GUIContent content = new GUIContent(text);
            EditorGUILayout.TextArea(text, style);
            style.fixedHeight = 0;
            style.fixedWidth = width;
            Vector2 controlRect = new Vector2(style.CalcHeight(content, width), width);

            //Padding/margin
            controlRect.y += (style.margin.top + style.margin.bottom);
            controlRect.x += (style.margin.left + style.margin.right);
            return controlRect;
        }
        private static Vector2 doAreaLabel(GUIContent content, GUIStyle style)
        {
            var controlRect = style.CalcSize(content);
            EditorGUILayout.LabelField(content, style, GUILayout.Width(controlRect.x));

            //Padding/margin
            controlRect.y += (style.margin.top + style.margin.bottom);
            controlRect.x += (style.margin.left + style.margin.right);
            return controlRect;
        }
        internal static void AllowDraw(bool bDraw)
        {
            allowDrawing = bDraw;
        }
        private static Rect getAreaRect()
        {
            Rect newRect = new Rect(0, 0, handleSize.x, handleSize.y);
            float padding = 4;
            //Setting Y
            switch (prefs.SceneViewAnchor.Value)
            {
                case SB_Preferences.ValidAnchors.UpperLeft:
                case SB_Preferences.ValidAnchors.UpperCenter:
                case SB_Preferences.ValidAnchors.UpperRight:
                    newRect.y = padding;
                    break;
                case SB_Preferences.ValidAnchors.MiddleLeft:
                case SB_Preferences.ValidAnchors.MiddleRight:
                    newRect.y = (sceneViewRect.height * .5f) - (newRect.height * .5f);
                    break;
                case SB_Preferences.ValidAnchors.LowerLeft:
                case SB_Preferences.ValidAnchors.LowerRight:
                case SB_Preferences.ValidAnchors.LowerCenter:
                    newRect.y = (sceneViewRect.height - newRect.height) - padding;
                    break;
                default:
                    Debug.LogWarning("Switch lacking cases");
                    break;
            }

            //Setting X
            switch (prefs.SceneViewAnchor.Value)
            {
                case SB_Preferences.ValidAnchors.UpperLeft:
                case SB_Preferences.ValidAnchors.MiddleLeft:
                case SB_Preferences.ValidAnchors.LowerLeft:
                    newRect.x = padding;
                    break;
                case SB_Preferences.ValidAnchors.UpperCenter:
                case SB_Preferences.ValidAnchors.LowerCenter:
                    newRect.x = (sceneViewRect.width * .5f) - (newRect.width * .5f);
                    break;
                case SB_Preferences.ValidAnchors.UpperRight:
                case SB_Preferences.ValidAnchors.MiddleRight:
                case SB_Preferences.ValidAnchors.LowerRight:
                    newRect.x = (sceneViewRect.width - newRect.width) - padding;
                    break;

                default:
                    Debug.LogWarning("Switch lacking cases");
                    break;
            }

            return newRect;
        }
    }
#endif
}
