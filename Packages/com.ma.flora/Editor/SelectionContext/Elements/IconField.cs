// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal class IconField<T> : VisualElement where T : VisualElement
    {
        public Image Icon;
        public T Field;

        public static readonly string ClassName = "icon-field";
        public static readonly string IconClassName = "icon-field__icon";
        public static readonly string FieldClassName = "icon-field__field";

        public IconField(T field)
        {
            AddToClassList(ClassName);

            Icon = new Image();
            Icon.AddToClassList(IconClassName);
            Add(Icon);

            Field = field;
            Field.AddToClassList(FieldClassName);
            Add(Field);
        }
    }
}
