// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEditor.Rendering;

namespace MA.Flora.Editor
{
    [CustomEditor(typeof(FloraDensitySettings))]
    internal class FloraDensitySettingsEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_GlobalDensityMode;
        private SerializedDataParameter m_GlobalDensity;
        private SerializedDataParameter m_GlobalDensityMask;
        private SerializedDataParameter m_GlobalDensitySizeThreshold;

        private SerializedDataParameter m_RangeDensityMode;
        private SerializedDataParameter m_RangeDensity;
        private SerializedDataParameter m_RangeDensityMask;
        private SerializedDataParameter m_RangeDensityFalloff;
        private SerializedDataParameter m_RangeDensityScreenPercentage;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<FloraDensitySettings>(serializedObject);

            m_GlobalDensityMode = Unpack(o.Find(x => x.GlobalDensityMode));
            m_GlobalDensity = Unpack(o.Find(x => x.GlobalDensity));
            m_GlobalDensityMask = Unpack(o.Find(x => x.GlobalDensityMask));
            m_GlobalDensitySizeThreshold = Unpack(o.Find(x => x.GlobalDensitySizeThreshold));

            m_RangeDensityMode = Unpack(o.Find(x => x.RangeDensityMode));
            m_RangeDensity = Unpack(o.Find(x => x.RangeDensity));
            m_RangeDensityMask = Unpack(o.Find(x => x.RangeDensityMask));
            m_RangeDensityFalloff = Unpack(o.Find(x => x.RangeDensityFalloff));
            m_RangeDensityScreenPercentage = Unpack(o.Find(x => x.RangeDensityScreenPercentage));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Global Density", EditorStyles.miniLabel);
            PropertyField(m_GlobalDensityMode, L10n.TextContent("Mode"));
            PropertyField(m_GlobalDensityMask, L10n.TextContent("Mask"));
            PropertyField(m_GlobalDensity, L10n.TextContent("Density"));
            PropertyField(m_GlobalDensitySizeThreshold, L10n.TextContent("Size Threshold"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Range Density", EditorStyles.miniLabel);
            PropertyField(m_RangeDensityMode, L10n.TextContent("Mode"));
            PropertyField(m_RangeDensityMask, L10n.TextContent("Mask"));
            PropertyField(m_RangeDensity, L10n.TextContent("Density"));
            PropertyField(m_RangeDensityFalloff, L10n.TextContent("Falloff"));
            PropertyField(m_RangeDensityScreenPercentage, L10n.TextContent("Screen Percentage"));
        }
    }
}
