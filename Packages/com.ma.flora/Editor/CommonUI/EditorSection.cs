// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal sealed class EditorSection : Foldout
    {
        public VisualElement HeaderActions { get; }

        public EditorSection(
            string title,
            bool expanded = true,
            string iconClass = null,
            bool nested = false)
        {
            AddToClassList("flora-editor-section");
            EnableInClassList("flora-editor-section--nested", nested);
            text = title;

            Toggle toggle = this.Q<Toggle>();
            toggle?.AddToClassList("flora-editor-section__toggle");
            Label titleLabel = toggle?.Q<Label>(className: "unity-foldout__text") ?? toggle?.Q<Label>();
            if (titleLabel != null)
            {
                titleLabel.AddToClassList("flora-editor-section__label");
                titleLabel.tooltip = title;
                titleLabel.pickingMode = PickingMode.Ignore;
                if (!string.IsNullOrEmpty(iconClass))
                {
                    titleLabel.AddToClassList("flora-editor-section__label--with-icon");
                    titleLabel.AddToClassList($"flora-editor-section__label--icon-{iconClass}");
                }
            }

            if (toggle != null)
            {
                HeaderActions = new VisualElement
                {
                    name = "HeaderActions",
                    pickingMode = PickingMode.Position,
                };
                HeaderActions.AddToClassList("flora-editor-section__actions");
                toggle.Add(HeaderActions);
            }
            else
            {
                HeaderActions = new VisualElement
                {
                    name = "HeaderActions",
                };
                HeaderActions.AddToClassList("flora-editor-section__actions");
                Add(HeaderActions);
            }

            contentContainer.AddToClassList("flora-editor-section__content");
            contentContainer.AddToClassList("flora-editor-section__content--indented");

            SetValueWithoutNotify(expanded);
        }
    }

    internal sealed class EditorSubgroup : VisualElement
    {
        public VisualElement content { get; }

        public EditorSubgroup(string title)
        {
            AddToClassList("flora-editor-subgroup");

            var label = new Label(title ?? string.Empty);
            label.AddToClassList("flora-editor-subgroup__label");
            label.AddToClassList("flora-editor-subgroup__label--gutter");
            label.tooltip = label.text;
            Add(label);

            content = new VisualElement();
            content.AddToClassList("flora-editor-subgroup__content");
            content.AddToClassList("flora-editor-subgroup__content--indented");
            Add(content);
        }
    }
}
