// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;

namespace MA.Flora.Editor.ShaderGraph
{
    internal sealed class FloraVersionProperty : AbstractShaderProperty<float>
    {
        public const int ShaderGraphVersion = 1;

        internal override bool isExposable => false;
        internal override bool isRenamable => false;

        public override float value
        {
            get => ShaderGraphVersion;
            set { }
        }

        public FloraVersionProperty()
        {
            overrideReferenceName = "_FloraVersion";
            displayName = "Flora Version";
        }

        internal override ShaderInput Copy()
        {
            return new FloraVersionProperty();
        }

        public override PropertyType propertyType => PropertyType.Float;

        internal override void ForeachHLSLProperty(Action<HLSLProperty> action)
        {
        }

        internal override string GetPropertyBlockString()
        {
            return $"[HideInInspector]{referenceName}(\"{displayName}\", Float) = {value}";
        }

        internal override string GetPropertyAsArgumentString(string precisionString)
        {
            return $"{precisionString} {referenceName}";
        }

        internal override AbstractMaterialNode ToConcreteNode()
        {
            return new Vector1Node();
        }

        internal override PreviewProperty GetPreviewMaterialProperty()
        {
            var previewProperty = new PreviewProperty
            {
                name = referenceName,
            };

            return previewProperty;
        }
    }
}
