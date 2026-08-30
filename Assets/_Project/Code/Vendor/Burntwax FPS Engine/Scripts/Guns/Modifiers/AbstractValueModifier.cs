using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Burntwax.Guns.Modifiers;
using Unity.Properties;
using UnityEngine;



namespace Burntwax.Guns.Modifiers  {
    public abstract class AbstractValueModifier<T> : Modifier {
        public string AttributeName;
        public T Amount;
        public abstract void Apply(GunScriptableObject Gun);

        protected FieldType GetAttribute<FieldType>(GunScriptableObject Gun, out object TargetObject, out FieldInfo Field) {
            string[] paths = AttributeName.Split("/");
            string attribute = paths[paths.Length-1];
            Type type = Gun.GetType();
            object target = Gun;
            for(int i = 0; i < paths.Length - 1; i++) {
                FieldInfo field = type.GetField(paths[i]);
                if(field == null) {
                    Debug.LogError($"Unable to apply modifier to {AttributeName}");
                    throw new InvalidPathSpecifiedException(AttributeName);
                }
                else {
                    target = field.GetValue(target);
                    type = target.GetType();
                }
            }

            FieldInfo attributeField = type.GetField(attribute);
            if(attributeField == null) {
                Debug.LogError($"Unable to apply modifier to {AttributeName}");
                throw new InvalidPathSpecifiedException(AttributeName);
            }
            Field = attributeField;
            TargetObject = target;
            return (FieldType)attributeField.GetValue(TargetObject);
        }
    }
}
