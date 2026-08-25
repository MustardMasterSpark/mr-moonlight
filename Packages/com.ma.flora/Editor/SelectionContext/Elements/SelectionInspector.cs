// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal class SelectionInspector : VisualElement, IDisposable
    {
        private static readonly string NoSelectionMessage = L10n.Tr("No element selected");

        public static bool IgnoreModificationCallbacks = false;

        private readonly Label m_ErrorMessage;

        private readonly VisualElement m_Root;
        private readonly IconField<SelectionField> m_Position;
        private readonly IconField<SelectionField> m_Rotation;
        private readonly IconField<SelectionField> m_Scale;

        public const string ClassName = "flora-instance-inspector-overlay";
        public const string PositionClassName = "flora-instance-inspector-overlay__position";
        public const string RotationClassName = "flora-instance-inspector-overlay__rotation";
        public const string ScaleClassName = "flora-instance-inspector-overlay__scale";

        public SelectionInspector()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.ma.flora/Editor/EditorResources/USS/OverlayCommon.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.ma.flora/Editor/SelectionContext/Elements/SelectionInspector.uss"));

            Add(m_Root = new VisualElement());
            m_Root.AddToClassList(ClassName);

            var positionField = new SelectionField("Position",
                (instance, index) => instance.GetInstancePosition(index),
                (instance, index, value) => instance.UpdateInstancePosition(index, value));
            m_Root.Add(m_Position = new IconField<SelectionField>(positionField));
            m_Position.AddToClassList(PositionClassName);
            m_Position.name = "Position";
            m_Position.tooltip = L10n.Tr("Instance Position");
            m_Position.style.flexDirection = FlexDirection.Row;
            m_Position.style.flexGrow = 1;

            var rotationField = new SelectionField("Rotation",
                (instance, index) => instance.GetInstanceRotation(index).eulerAngles,
                (instance, index, value) => instance.UpdateInstanceRotation(index, Quaternion.Euler(value)));
            m_Root.Add(m_Rotation = new IconField<SelectionField>(rotationField));
            m_Rotation.AddToClassList(RotationClassName);
            m_Rotation.name = "Rotation";
            m_Rotation.tooltip = L10n.Tr("Instance Rotation");
            m_Rotation.style.flexDirection = FlexDirection.Row;
            m_Rotation.style.flexGrow = 1;

            var scaleField = new SelectionField("Scale",
                (instance, index) => instance.GetInstanceScale(index),
                (instance, index, value) => instance.UpdateInstanceScale(index, value));
            m_Root.Add(m_Scale = new IconField<SelectionField>(scaleField));
            m_Scale.AddToClassList(ScaleClassName);
            m_Scale.name = "Scale";
            m_Scale.tooltip = L10n.Tr("Instance Scale");
            m_Scale.style.flexDirection = FlexDirection.Row;
            m_Scale.style.flexGrow = 1;

            Add(m_ErrorMessage = new Label { name = "ErrorMessage"});

            UpdateWithSelection();
        }

        public void Dispose()
        {
        }

        public void UpdateWithSelection()
        {
            var selectedInstances = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            if (selectedInstances.Length < 1)
            {
                ShowErrorMessage(NoSelectionMessage);
                m_Position.style.display = DisplayStyle.None;
                m_Rotation.style.display = DisplayStyle.None;
                m_Scale.style.display = DisplayStyle.None;
            }
            else
            {
                HideErrorMessage();
                m_Position.style.display = DisplayStyle.Flex;
                m_Rotation.style.display = DisplayStyle.Flex;
                m_Scale.style.display = DisplayStyle.Flex;
            }

            m_Position.Field.Update(selectedInstances);
            m_Rotation.Field.Update(selectedInstances);
            m_Scale.Field.Update(selectedInstances);
        }

        private void OnRendererModified()
        {
            if (IgnoreModificationCallbacks)
                return;

            var selectedInstances = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            m_Position.Field.Update(selectedInstances);
            m_Rotation.Field.Update(selectedInstances);
            m_Scale.Field.Update(selectedInstances);
        }

        private void ShowErrorMessage(string error)
        {
            m_ErrorMessage.style.display = DisplayStyle.Flex;
            m_ErrorMessage.text = error;
        }

        private void HideErrorMessage()
        {
            m_ErrorMessage.style.display = DisplayStyle.None;
        }
    }
}
