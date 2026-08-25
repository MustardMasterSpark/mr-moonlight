// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal static class EditorElements
    {
        public const string SharedStyleSheetPath = "Packages/com.ma.flora/Editor/CommonUI/USS/EditorUI.uss";
        public const string VariablesStyleSheetPath = "Packages/com.ma.flora/Editor/CommonUI/USS/Variables.uss";
        public const string IconButtonTemplatePath = "Packages/com.ma.flora/Editor/CommonUI/UXML/IconButton.uxml";
        private static VisualTreeAsset s_IconButtonTemplate;

        public static void AddSharedStyleSheet(VisualElement root)
        {
            if (root == null)
                return;

            AddStyleSheet(root, SharedStyleSheetPath);
            AddStyleSheetWithSkinVariant(root, VariablesStyleSheetPath);
            root.AddToClassList("variables");
        }

        public static void AddStyleSheet(VisualElement root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return;

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
        }

        public static void AddStyleSheetWithSkinVariant(VisualElement root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return;

            AddStyleSheet(root, path);

            string variantPath = GetSkinVariantPath(path);
            if (!string.IsNullOrEmpty(variantPath))
                AddStyleSheet(root, variantPath);
        }

        public static Button AddIconButton(VisualElement parent, string tooltip, Action action, string iconClass)
        {
            Button button = CloneIconButton();
            button.clicked += action;
            button.tooltip = tooltip ?? string.Empty;
            if (!string.IsNullOrEmpty(iconClass))
                button.AddToClassList($"flora-editor-icon-button--{iconClass}");

            Label label = button.Q<Label>(className: "flora-editor-icon-button__label");
            if (label != null)
            {
                label.text = string.Empty;
                label.style.display = DisplayStyle.None;
            }

            parent.Add(button);
            return button;
        }

        private static Button CloneIconButton()
        {
            s_IconButtonTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(IconButtonTemplatePath);
            TemplateContainer container = s_IconButtonTemplate.CloneTree();
            Button button = container.Q<Button>(className: "flora-editor-icon-button");
            button.RemoveFromHierarchy();
            return button;
        }

        public static Button AddIconToggleButton(
            VisualElement parent,
            SerializedProperty property,
            string label,
            string tooltip,
            Action changed,
            string iconClass,
            string className)
        {
            void ToggleProperty()
            {
                property.serializedObject.Update();
                property.boolValue = !property.boolValue;
                property.serializedObject.ApplyModifiedProperties();
                changed?.Invoke();
            }

            var button = AddIconButton(
                parent,
                tooltip,
                ToggleProperty,
                iconClass);
            button.name = string.IsNullOrEmpty(label) ? property.displayName : label;
            button.userData = (Action)ToggleProperty;
            button.AddToClassList("flora-editor-icon-toggle-button");
            button.AddToClassList(className);
            button.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            button.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            return button;
        }

        public static VisualElement AddWarning(
            VisualElement parent,
            string message,
            MessageType messageType = MessageType.Warning,
            string actionText = null,
            string actionTooltip = null,
            Action action = null,
            string actionName = null,
            string actionClass = null)
        {
            if (parent == null || string.IsNullOrEmpty(message))
                return null;

            var row = new VisualElement();
            row.AddToClassList("flora-editor-warning");
            row.AddToClassList($"flora-editor-warning--{messageType.ToString().ToLowerInvariant()}");
            parent.Add(row);

            var icon = new VisualElement();
            icon.AddToClassList("flora-editor-warning__icon");
            icon.AddToClassList($"flora-editor-warning__icon--{messageType.ToString().ToLowerInvariant()}");
            row.Add(icon);

            var label = new Label(message);
            label.AddToClassList("flora-editor-warning__label");
            label.tooltip = message;
            row.Add(label);

            if (!string.IsNullOrEmpty(actionText) && action != null)
            {
                var button = new Button(action)
                {
                    text = actionText,
                    tooltip = actionTooltip ?? string.Empty,
                    name = actionName ?? string.Empty,
                    userData = action,
                };
                button.AddToClassList("flora-editor-warning__action");
                if (!string.IsNullOrEmpty(actionClass))
                    button.AddToClassList(actionClass);
                row.Add(button);
            }

            return row;
        }

        private static string GetSkinVariantPath(string path)
        {
            int extensionIndex = path.LastIndexOf('.');
            if (extensionIndex < 0)
                return null;

            return path.Insert(extensionIndex, EditorGUIUtility.isProSkin ? "Dark" : "Light");
        }
    }
}
