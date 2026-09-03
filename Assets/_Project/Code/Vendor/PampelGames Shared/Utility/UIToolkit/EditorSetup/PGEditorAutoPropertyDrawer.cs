// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.Shared.Utility
{
    // Base class for custom classes
    public abstract class PGEditorAutoClass
    {
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(PGEditorAutoClass), true)]
    public class PGEditorAutoPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var targetType = fieldInfo.FieldType;

            if (targetType.IsArray)
                targetType = targetType.GetElementType();
            else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                targetType = targetType.GetGenericArguments()[0];

            var method = typeof(PGEditorAutoSetup)
                .GetMethod(
                    "CreateAndBindClassElements",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] {typeof(SerializedProperty), typeof(VisualElement)},
                    null
                );

            if (method == null)
            {
                Debug.LogError("PGEditorAutoSetup.CreateAndBindClassElements(SerializedProperty, VisualElement) not found.");
                container.Add(new PropertyField(property));
                return container;
            }

            var genericMethod = method.MakeGenericMethod(targetType);

            try
            {
                genericMethod.Invoke(null, new object[] {property, container});
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking CreateAndBindClassElements for type {targetType.Name}: {e}");
                container.Add(new PropertyField(property));
            }

            return container;
        }
    }
#endif
}
