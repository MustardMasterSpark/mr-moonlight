// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

namespace PampelGames.Shared.Utility
{
    public static class PGClassUtility
    {
        /// <summary>
        ///     Generates a list of all inheritors of a class type.
        /// </summary>
        /// <param name="assemblies">var assemblies = AppDomain.CurrentDomain.GetAssemblies();</param>
        /// <typeparam name="T">Type of the base class.</typeparam>
        public static List<T> CreateInstances<T>(Assembly[] assemblies)
            where T : class
        {
            var instances = new List<T>();

            foreach (var type in assemblies.SelectMany(a => a.GetTypes())
                         .Where(t => typeof(T).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
            {
                var instance = (T) FormatterServices.GetUninitializedObject(type);
                instances.Add(instance);
            }

            return instances;
        }

        /// <summary>
        ///     Creates a copy of a class instance, including all properties and fields.
        /// </summary>
        /// <param name="source">Instance of the class that should be copied.</param>
        /// <returns>Object that has to be casted to the class.</returns>
        public static object CopyClass(object source)
        {
            return CopyClassInternal(source);
        }

        /// <summary>
        ///     Copies the fields and properties from the source object to the target object.
        /// </summary>
        /// <param name="source">The source object to copy from.</param>
        /// <param name="target">The target object to copy to. Can also be a derived class.</param>
        /// <param name="fieldNames">Optional: Only copy fields and properties of these names.</param>
        public static void CopyClassValues(object source, object target, List<string> fieldNames = null)
        {
            CopyClassValuesInternal(source, target, fieldNames);
        }


        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private static object CopyClassInternal(object source)
        {
            var sourceType = source.GetType();
            var targetType = sourceType.Assembly.GetType(sourceType.FullName);

            if (targetType != null)
            {
                var targetInstance = Activator.CreateInstance(targetType);

                CopyProperties(source, targetInstance, null);
                CopyFields(source, targetInstance, null);

                return targetInstance;
            }

            return targetType;
        }

        private static void CopyClassValuesInternal(object source, object target, List<string> fieldNames)
        {
            CopyProperties(source, target, fieldNames);
            CopyFields(source, target, fieldNames);
        }

        private static void CopyProperties(object source, object target, List<string> fieldNames)
        {
            var sourceType = source.GetType();
            var targetType = target.GetType();

            var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var sourceProp in sourceProperties)
            {
                if (fieldNames is {Count: > 0} && !fieldNames.Contains(sourceProp.Name)) continue;

                var targetProp = targetType.GetProperty(sourceProp.Name, BindingFlags.Public | BindingFlags.Instance);

                if (targetProp != null && sourceProp.CanRead && targetProp.CanWrite)
                {
                    var value = sourceProp.GetValue(source);

                    if (value is AnimationCurve curve)
                        targetProp.SetValue(target, CopyAnimationCurve(curve));
                    else
                        targetProp.SetValue(target, value);
                }
            }
        }

        private static void CopyFields(object source, object target, List<string> fieldNames)
        {
            var sourceType = source.GetType();
            var targetType = target.GetType();

            var sourceFields = sourceType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in sourceFields)
            {
                if (fieldNames is {Count: > 0} && !fieldNames.Contains(field.Name)) continue;

                var targetField = targetType.GetField(field.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (targetField == null) continue;

                var value = field.GetValue(source);

                if (value == null)
                {
                    targetField.SetValue(target, null);
                    continue;
                }

                if (value is PGIDuplicable)
                {
                    targetField.SetValue(target, CopyClassInternal(value));
                }
                else if (value is AnimationCurve curve)
                {
                    targetField.SetValue(target, CopyAnimationCurve(curve));
                }
                else if (field.FieldType.IsArray)
                {
                    var array = (Array) value;
                    var elementType = field.FieldType.GetElementType();
                    if (elementType == null) continue;

                    var newArray = Array.CreateInstance(elementType, array.Length);
                    for (var i = 0; i < array.Length; i++)
                    {
                        var item = array.GetValue(i);
                        newArray.SetValue(item is PGIDuplicable ? CopyClassInternal(item) : item, i);
                    }

                    targetField.SetValue(target, newArray);
                }
                else if (value is IList list)
                {
                    var newList = (IList) Activator.CreateInstance(targetField.FieldType);
                    foreach (var item in list) newList.Add(item is PGIDuplicable ? CopyClassInternal(item) : item);
                    targetField.SetValue(target, newList);
                }
                else
                {
                    targetField.SetValue(target, value);
                }
            }
        }

        private static AnimationCurve CopyAnimationCurve(AnimationCurve curve)
        {
            var copiedCurve = new AnimationCurve(curve.keys)
            {
                postWrapMode = curve.postWrapMode,
                preWrapMode = curve.preWrapMode
            };
            return copiedCurve;
        }
    }
}