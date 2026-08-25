// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEditor.Rendering;

namespace MA.Flora.Editor
{
    [CustomEditor(typeof(FloraRenderSettings))]
    internal class FloraRenderSettingsEditor : VolumeComponentEditor
    {
        private SerializedDataParameter m_MaxRenderDistance;
        private SerializedDataParameter m_MaxShadowDistance;
        private SerializedDataParameter m_MinScreenSizeMode;
        private SerializedDataParameter m_MinScreenSize;
        private SerializedDataParameter m_MinShadowLOD;
        private SerializedDataParameter m_RandomizeLODTransition;
        private SerializedDataParameter m_CrossFadeDuration;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<FloraRenderSettings>(serializedObject);

            m_MaxRenderDistance = Unpack(o.Find(x => x.MaxRenderDistance));
            m_MaxShadowDistance = Unpack(o.Find(x => x.MaxShadowDistance));
            m_MinScreenSizeMode = Unpack(o.Find(x => x.MinScreenSizeMode));
            m_MinScreenSize = Unpack(o.Find(x => x.MinScreenSize));
            m_MinShadowLOD = Unpack(o.Find(x => x.MinShadowLOD));
            m_RandomizeLODTransition = Unpack(o.Find(x => x.RandomizeLODTransition));
            m_CrossFadeDuration = Unpack(o.Find(x => x.CrossFadeDuration));
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Culling", EditorStyles.miniLabel);
            PropertyField(m_MaxRenderDistance, L10n.TextContent("Max Render Distance"));
            PropertyField(m_MaxShadowDistance, L10n.TextContent("Max Shadow Distance"));
            PropertyField(m_MinScreenSizeMode, L10n.TextContent("Min Screen Size Mode"));
            using (new EditorGUI.DisabledScope(m_MinScreenSizeMode.value.intValue == (int)FloraMinimumScreenSizeMode.Disabled))
                PropertyField(m_MinScreenSize, L10n.TextContent("Min Screen Size"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("LODs", EditorStyles.miniLabel);
            PropertyField(m_MinShadowLOD, L10n.TextContent("Min Shadow LOD"));
            PropertyField(m_RandomizeLODTransition, L10n.TextContent("Randomize LOD Transition"));
            PropertyField(m_CrossFadeDuration, L10n.TextContent("Crossfade Duration"));
        }
    }
}
