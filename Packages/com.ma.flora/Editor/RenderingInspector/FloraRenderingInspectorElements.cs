// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal class FloraRenderingInspectorSection : Foldout
    {
        private const string HeaderTemplatePath = "Packages/com.ma.flora/Editor/CommonUI/UXML/SectionHeader.uxml";
        private const string ToggleInputClassName = "unity-toggle__input";
        private const string FoldoutCheckmarkClassName = "unity-foldout__checkmark";
        private const string ToggleIconClassName = "flora-rendering-inspector-section__toggle-icon";
        private static VisualTreeAsset s_HeaderTemplate;

        private readonly Toggle m_Toggle;
        private readonly VisualElement m_Header;
        private readonly VisualElement m_Icon;
        private readonly Label m_TitleLabel;
        private readonly Label m_CountLabel;
        private readonly Label m_SummaryLabel;

        public FloraRenderingInspectorSection(
            string title,
            int? count = null,
            bool expanded = false,
            bool showCount = true,
            string summary = null,
            string iconClass = null,
            bool nested = false)
        {
            AddToClassList("flora-rendering-inspector__section");
            AddToClassList("flora-rendering-inspector-section");
            EnableInClassList("flora-rendering-inspector-section--nested", nested);
            Title = title;
            text = string.Empty;

            m_Toggle = this.Q<Toggle>();
            m_Toggle?.AddToClassList("flora-rendering-inspector-section__toggle");
            var toggleInput = m_Toggle?.Q(className: ToggleInputClassName) ?? m_Toggle?.Q(className: FoldoutCheckmarkClassName);
            toggleInput?.AddToClassList(ToggleIconClassName);

            contentContainer.AddToClassList("flora-rendering-inspector-section__content");

            m_Header = CloneHeaderTemplate();
            m_Header.pickingMode = PickingMode.Ignore;

            m_Icon = m_Header.Q<VisualElement>("icon");
            SetIcon(iconClass);

            m_TitleLabel = m_Header.Q<Label>("label");
            m_TitleLabel.text = title;
            m_TitleLabel.tooltip = title;
            m_TitleLabel.pickingMode = PickingMode.Ignore;

            m_SummaryLabel = m_Header.Q<Label>("summary");
            m_SummaryLabel.text = summary ?? string.Empty;
            m_SummaryLabel.style.display = string.IsNullOrEmpty(summary) ? DisplayStyle.None : DisplayStyle.Flex;
            m_SummaryLabel.tooltip = summary ?? string.Empty;
            m_SummaryLabel.pickingMode = PickingMode.Ignore;

            m_CountLabel = m_Header.Q<Label>("count");
            m_CountLabel.text = count.HasValue ? count.Value.ToString("n0") : string.Empty;
            m_CountLabel.style.display = showCount && count.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            m_CountLabel.pickingMode = PickingMode.Ignore;

            if (m_Toggle != null)
                m_Toggle.Add(m_Header);
            else
                hierarchy.Insert(0, m_Header);

            SetValueWithoutNotify(expanded);
        }

        public string Title { get; }
        public VisualElement HeaderElement => m_Toggle ?? m_Header;
        public string TitleLabelTextForTests => m_TitleLabel.text;

        private void SetIcon(string iconClass)
        {
            var hasIcon = !string.IsNullOrEmpty(iconClass);
            m_Icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasIcon)
                m_Icon.AddToClassList($"flora-rendering-inspector-section__icon--{iconClass}");
        }

        private static VisualElement CloneHeaderTemplate()
        {
            s_HeaderTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HeaderTemplatePath);
            var container = s_HeaderTemplate.CloneTree();
            var header = container.Q(className: "flora-rendering-inspector-section__header");
            header.RemoveFromHierarchy();
            return header;
        }
    }

    internal sealed class FloraRenderingInspectorRelationshipList : VisualElement
    {
        private const string ShowAllTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/ShowAllRow.uxml";
        private const string ListClassName = "flora-rendering-inspector-relationship-list";
        private const string ShowAllRowClassName = "flora-rendering-inspector__show-all-row";
        private const string ShowAllSummaryClassName = "flora-rendering-inspector__show-all-summary";
        private const string ShowAllButtonClassName = "flora-rendering-inspector__show-all-button";
        private static VisualTreeAsset s_ShowAllTemplate;

        public FloraRenderingInspectorRelationshipList(
            string key,
            IEnumerable<FloraRenderingInspectorNode> nodes,
            Action<FloraRenderingInspectorNode> onSelect,
            bool showAll,
            int maxVisible,
            Action<string> onShowAll)
        {
            AddToClassList(ListClassName);

            var nodeArray = (nodes ?? Enumerable.Empty<FloraRenderingInspectorNode>())
                .Where(node => node != null)
                .ToArray();
            var shouldShowAll = showAll || nodeArray.Length <= maxVisible;
            var visibleCount = shouldShowAll ? nodeArray.Length : maxVisible;

            for (var i = 0; i < visibleCount; i++)
                Add(new FloraRenderingInspectorRelationshipRow(nodeArray[i], onSelect));

            if (shouldShowAll || nodeArray.Length <= maxVisible)
                return;

            AddShowAllRow(key, nodeArray.Length, visibleCount, onShowAll);
        }

        private void AddShowAllRow(string key, int totalCount, int visibleCount, Action<string> onShowAll)
        {
            var row = CloneShowAllRow();
            Add(row);

            var summary = row.Q<Label>("summary") ?? row.Q<Label>(className: ShowAllSummaryClassName);
            summary.text = $"{totalCount - visibleCount:n0} more hidden";

            var button = row.Q<Button>("button") ?? row.Q<Button>(className: ShowAllButtonClassName);
            button.text = $"Show all {totalCount:n0}";
            button.clicked += () => onShowAll?.Invoke(key);
        }

        private static VisualElement CloneShowAllRow()
        {
            s_ShowAllTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShowAllTemplatePath);
            return FloraRenderingInspectorElements.CloneTemplateRoot(s_ShowAllTemplate, ShowAllRowClassName);
        }
    }

    internal sealed class FloraRenderingInspectorRelationshipRow : VisualElement
    {
        private const string RowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/RelationshipRow.uxml";
        private const string RowClassName = "flora-rendering-inspector__relationship-row";
        private const string IconClassName = "flora-rendering-inspector__relationship-icon";
        private const string IconModifierClassPrefix = "flora-rendering-inspector__relationship-icon--";
        private const string LabelsClassName = "flora-rendering-inspector__relationship-labels";
        private const string NameClassName = "flora-rendering-inspector__relationship-name";
        private const string CountClassName = "flora-rendering-inspector__relationship-count";
        private static VisualTreeAsset s_RowTemplate;

        public FloraRenderingInspectorRelationshipRow(FloraRenderingInspectorNode node, Action<FloraRenderingInspectorNode> onSelect)
        {
            var row = CloneRowTemplate();
            AddToClassList(RowClassName);
            foreach (var child in row.Children().ToList())
            {
                child.RemoveFromHierarchy();
                hierarchy.Add(child);
            }

            tooltip = node?.Tooltip ?? node?.Subtitle ?? string.Empty;
            focusable = true;

            var icon = this.Q<VisualElement>("icon") ?? this.Q(className: IconClassName);
            RemoveClassPrefix(icon, IconModifierClassPrefix);
            if (FloraRenderingInspectorIcons.TryGetThumbnail(node, EditorIconSize.Regular, out var thumbnail))
                icon.style.backgroundImage = thumbnail;
            else
            {
                icon.style.backgroundImage = StyleKeyword.Null;
                icon.AddToClassList($"{IconModifierClassPrefix}{FloraRenderingInspectorIcons.GetStyleClass(node)}");
            }

            var nameLabel = this.Q<Label>("name") ?? this.Q<Label>(className: NameClassName);
            nameLabel.text = node?.Name ?? string.Empty;
            nameLabel.tooltip = nameLabel.text;

            var countLabel = this.Q<Label>("count") ?? this.Q<Label>(className: CountClassName);
            countLabel.text = node?.CountText ?? string.Empty;
            countLabel.tooltip = countLabel.text;

            if (onSelect != null && node != null)
            {
                AddToClassList("flora-rendering-inspector__relationship-row--selectable");
                RegisterCallback<ClickEvent>(evt =>
                {
                    onSelect(node);
                    evt.StopPropagation();
                });
                RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space)
                        return;

                    onSelect(node);
                    evt.StopPropagation();
                });
            }

        }

        private static VisualElement CloneRowTemplate()
        {
            s_RowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RowTemplatePath);
            return FloraRenderingInspectorElements.CloneTemplateRoot(s_RowTemplate, RowClassName);
        }

        private static void RemoveClassPrefix(VisualElement element, string prefix)
        {
            foreach (var className in element.GetClasses().Where(className => className.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                element.RemoveFromClassList(className);
        }
    }

    internal static class FloraRenderingInspectorElements
    {
        private const string IconButtonTemplatePath = "Packages/com.ma.flora/Editor/CommonUI/UXML/IconButton.uxml";
        private const string ValueRowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/ValueRow.uxml";
        private const string ObjectRowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/ObjectRow.uxml";
        private const string WarningRowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/WarningRow.uxml";

        private static VisualTreeAsset s_IconButtonTemplate;
        private static VisualTreeAsset s_ValueRowTemplate;
        private static VisualTreeAsset s_ObjectRowTemplate;
        private static VisualTreeAsset s_WarningRowTemplate;

        public static Label AddChip(VisualElement parent, string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var chip = new Label(text);
            chip.AddToClassList("flora-rendering-inspector__chip");
            parent.Add(chip);
            return chip;
        }

        public static void AddValueRow(VisualElement parent, string label, int value) => AddValueRow(parent, label, value.ToString("n0"));
        public static void AddValueRow(VisualElement parent, string label, long value) => AddValueRow(parent, label, value.ToString("n0"));

        public static void AddValueRow(VisualElement parent, string label, string value)
        {
            var row = CloneValueRow();
            parent.Add(row);

            var labelElement = row.Q<Label>("label") ?? row.Q<Label>(className: "flora-rendering-inspector__detail-label");
            labelElement.text = label;
            labelElement.tooltip = label;

            var valueElement = row.Q<Label>("value") ?? row.Q<Label>(className: "flora-rendering-inspector__detail-value");
            valueElement.text = string.IsNullOrEmpty(value) ? "-" : value;
            valueElement.tooltip = valueElement.text;
        }

        public static void AddObjectRow(VisualElement parent, string label, Object obj)
        {
            if (!obj)
            {
                AddValueRow(parent, label, "None");
                return;
            }

            var row = CloneObjectRow();
            parent.Add(row);

            var labelElement = row.Q<Label>("label") ?? row.Q<Label>(className: "flora-rendering-inspector__detail-label");
            labelElement.text = label;
            labelElement.tooltip = label;

            var objectField = new ObjectField
            {
                value = obj,
                objectType = obj.GetType(),
                allowSceneObjects = true,
            };
            objectField.SetEnabled(false);
            objectField.AddToClassList("flora-rendering-inspector__detail-object-field");
            var fieldContainer = row.Q("object-field-container");
            fieldContainer.Add(objectField);

            AddFrameButton(row, obj);
        }

        public static void AddObjectList(VisualElement parent, string label, IReadOnlyList<Object> objects, int maxVisible = 12)
        {
            if (objects == null || objects.Count == 0)
            {
                AddValueRow(parent, label, "None");
                return;
            }

            var count = Math.Min(objects.Count, maxVisible);
            for (var i = 0; i < count; i++)
                AddObjectRow(parent, i == 0 ? label : string.Empty, objects[i]);

            if (objects.Count > count)
                AddValueRow(parent, string.Empty, $"+ {objects.Count - count:n0} more");
        }

        public static void AddRelationshipRow(VisualElement parent, FloraRenderingInspectorNode node, Action<FloraRenderingInspectorNode> onSelect)
        {
            parent.Add(new FloraRenderingInspectorRelationshipRow(node, onSelect));
        }

        internal static VisualElement CreateRelationshipRowForTests(FloraRenderingInspectorNode node, Action<FloraRenderingInspectorNode> onSelect)
            => new FloraRenderingInspectorRelationshipRow(node, onSelect);

        public static void AddWarning(VisualElement parent, string warning)
        {
            if (string.IsNullOrEmpty(warning))
                return;

            var warningElement = CloneWarningRow();
            var label = warningElement.Q<Label>("label") ?? warningElement.Q<Label>(className: "flora-rendering-inspector__warning-label");
            label.text = warning;
            label.tooltip = warning;

            parent.Add(warningElement);
        }

        public static Button AddIconButton(VisualElement parent, string tooltip, Action action, string iconClass)
            => AddIconButton(parent, tooltip, action, iconClass, null);

        public static Button AddIconButton(VisualElement parent, string tooltip, Action action, string iconClass, string label)
        {
            var button = CloneIconButton();
            button.clicked += action;
            button.tooltip = tooltip;
            button.AddToClassList("flora-rendering-inspector__icon-action");
            button.AddToClassList($"flora-rendering-inspector__object-button--{iconClass}");

            var labelElement = button.Q<Label>(className: "flora-rendering-inspector__icon-button-label")
                               ?? button.Q<Label>(className: "flora-editor-icon-button__label");
            labelElement.text = label ?? string.Empty;
            labelElement.style.display = string.IsNullOrEmpty(label) ? DisplayStyle.None : DisplayStyle.Flex;

            parent.Add(button);
            return button;
        }

        private static Button CloneIconButton()
        {
            s_IconButtonTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(IconButtonTemplatePath);
            var container = s_IconButtonTemplate.CloneTree();
            var button = container.Q<Button>(className: "flora-editor-icon-button");
            button.RemoveFromHierarchy();
            button.AddToClassList("flora-rendering-inspector__icon-button-control");

            var icon = button.Q(className: "flora-editor-icon-button__icon");
            icon.AddToClassList("flora-rendering-inspector__icon-button-icon");

            var label = button.Q<Label>(className: "flora-editor-icon-button__label");
            label.AddToClassList("flora-rendering-inspector__icon-button-label");

            return button;
        }

        internal static VisualElement CloneTemplateRoot(VisualTreeAsset template, string rootClassName)
        {
            var container = template.CloneTree();
            var root = container.Q(className: rootClassName);
            root.RemoveFromHierarchy();
            return root;
        }

        private static VisualElement CloneValueRow()
        {
            s_ValueRowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ValueRowTemplatePath);
            return CloneTemplateRoot(s_ValueRowTemplate, "flora-rendering-inspector__detail-row");
        }

        private static VisualElement CloneObjectRow()
        {
            s_ObjectRowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ObjectRowTemplatePath);
            return CloneTemplateRoot(s_ObjectRowTemplate, "flora-rendering-inspector__object-row");
        }

        private static VisualElement CloneWarningRow()
        {
            s_WarningRowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WarningRowTemplatePath);
            return CloneTemplateRoot(s_WarningRowTemplate, "flora-rendering-inspector__warning");
        }

        public static void AddFrameButton(VisualElement parent, Object obj)
        {
            if (!IsSceneObject(obj))
                return;

            var button = AddIconButton(parent, "Frame object in Scene view", () =>
            {
                Selection.activeObject = obj;
                SceneView.FrameLastActiveSceneView();
            }, "frame");
            button.AddToClassList("flora-rendering-inspector__object-button");
        }

        internal static bool TryFrameBounds(Bounds bounds)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null && SceneView.sceneViews.Count > 0)
                sceneView = SceneView.sceneViews[0] as SceneView;
            if (sceneView == null)
                return false;

            sceneView.Frame(bounds, false);
            sceneView.Repaint();
            return true;
        }

        private static bool IsSceneObject(Object obj)
        {
            return obj switch
            {
                GameObject gameObject => gameObject.scene.IsValid(),
                Component component => component.gameObject.scene.IsValid(),
                _ => false,
            };
        }
    }
}
