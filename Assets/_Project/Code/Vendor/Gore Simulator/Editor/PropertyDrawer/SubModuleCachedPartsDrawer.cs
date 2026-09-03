// ----------------------------------------------------
// Gore Simulator
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Tools.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PampelGames.GoreSimulator.Editor
{
    [CustomPropertyDrawer(typeof(SubModuleCachedParts))]
    public class SubModuleCachedPartsDrawer : PropertyDrawer
    {
        private SubModuleCachedParts _subModuleCachedParts;

        private ObjectField meshPartsParent;


        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var listIndex = PGInspectorEditorUtility.GetDrawingListIndex(property);
            var obj = fieldInfo.GetValue(property.serializedObject.targetObject);
            var objList = obj as List<SubModuleBase>;
            _subModuleCachedParts = (SubModuleCachedParts) objList[listIndex];

            meshPartsParent = new ObjectField("Mesh Parts Parent");


            FindAndBindProperties(property);
            VisualizeProperties();


            container.Add(meshPartsParent);


            return container;
        }

        private void FindAndBindProperties(SerializedProperty property)
        {
            meshPartsParent.BindProperty(property.FindPropertyRelative(nameof(SubModuleCachedParts.meshPartsParent)));
        }

        private void VisualizeProperties()
        {
            meshPartsParent.objectType = typeof(MeshParts);
        }
    }
}