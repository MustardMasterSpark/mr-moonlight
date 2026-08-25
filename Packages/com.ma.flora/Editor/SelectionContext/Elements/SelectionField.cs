// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal class SelectionField : Vector3Field
    {
        public delegate bool EqualityComparer<in T>(T a, T b);

        private static readonly List<float3> Float3Buffer = new();
        private static readonly EqualityComparer<float3> ComparerX = (a, b) => a.x.Equals(b.x);
        private static readonly EqualityComparer<float3> ComparerY = (a, b) => a.y.Equals(b.y);
        private static readonly EqualityComparer<float3> ComparerZ = (a, b) => a.z.Equals(b.z);
        private static readonly string ApplyInstanceTransform = L10n.Tr("Modify Instance Transform");

        private readonly FloatField m_X;
        private readonly FloatField m_Y;
        private readonly FloatField m_Z;

        private readonly Func<InstanceSelectionGroup, int, float3> m_Get;
        private readonly Action<InstanceSelectionGroup, int, float3> m_Set;

        private List<InstanceSelectionGroup> m_SelectionGroups = new(0);

        public event Action Changed;

        public SelectionField(string label, Func<InstanceSelectionGroup, int, float3> get, Action<InstanceSelectionGroup, int, float3> set) : base(label)
        {
            m_Get = get;
            m_Set = set;

            m_X = this.Q<FloatField>("unity-x-input");
            m_Y = this.Q<FloatField>("unity-y-input");
            m_Z = this.Q<FloatField>("unity-z-input");

            m_X.RegisterValueChangedCallback(ApplyX);
            m_Y.RegisterValueChangedCallback(ApplyY);
            m_Z.RegisterValueChangedCallback(ApplyZ);
        }

        public void Update(InstanceSelectionGroup[] groups)
        {
            m_SelectionGroups.Clear();
            for (int i = 0; i < groups.Length; ++i)
            {
                if (!groups[i].IsEmpty)
                    m_SelectionGroups.Add(groups[i]);
            }

            Float3Buffer.Clear();
            for (int i = 0; i < m_SelectionGroups.Count; ++i)
            {
                foreach (int instanceIndex in m_SelectionGroups[i].SelectionIndices)
                    Float3Buffer.Add(m_Get.Invoke(m_SelectionGroups[i], instanceIndex));
            }

            float3 value = Float3Buffer.Count > 0 ? Float3Buffer[0] : 0;
            m_X.showMixedValue = HasMultipleValues(Float3Buffer, ComparerX);
            if (!m_X.showMixedValue)
                m_X.SetValueWithoutNotify(value[0]);

            m_Y.showMixedValue = HasMultipleValues(Float3Buffer, ComparerY);
            if (!m_Y.showMixedValue)
                m_Y.SetValueWithoutNotify(value[1]);

            m_Z.showMixedValue = HasMultipleValues(Float3Buffer, ComparerZ);
            if (!m_Z.showMixedValue)
                m_Z.SetValueWithoutNotify(value[2]);
        }

        private void ApplyX(ChangeEvent<float> evt)
        {
            SelectionInspector.IgnoreModificationCallbacks = true;
            for (int i = 0; i < m_SelectionGroups.Count; ++i)
            {
                m_SelectionGroups[i].RecordUndo("ApplyX");

                foreach (int instanceIndex in m_SelectionGroups[i].SelectionIndices)
                {
                    float3 value = m_Get.Invoke(m_SelectionGroups[i], instanceIndex);
                    value.x = evt.newValue;
                    m_Set.Invoke(m_SelectionGroups[i], instanceIndex, value);
                }
            }

            m_X.showMixedValue = false;
            m_X.SetValueWithoutNotify(evt.newValue);
            Changed?.Invoke();
            SelectionInspector.IgnoreModificationCallbacks = false;
        }

        private void ApplyY(ChangeEvent<float> evt)
        {
            SelectionInspector.IgnoreModificationCallbacks = true;
            for (int i = 0; i < m_SelectionGroups.Count; ++i)
            {
                m_SelectionGroups[i].RecordUndo("ApplyY");

                foreach (int instanceIndex in m_SelectionGroups[i].SelectionIndices)
                {
                    float3 value = m_Get.Invoke(m_SelectionGroups[i], instanceIndex);
                    value.y = evt.newValue;
                    m_Set.Invoke(m_SelectionGroups[i], instanceIndex, value);
                }
            }

            m_Y.showMixedValue = false;
            m_Y.SetValueWithoutNotify(evt.newValue);
            Changed?.Invoke();
            SelectionInspector.IgnoreModificationCallbacks = false;
        }

        private void ApplyZ(ChangeEvent<float> evt)
        {
            SelectionInspector.IgnoreModificationCallbacks = true;
            for (int i = 0; i < m_SelectionGroups.Count; ++i)
            {
                m_SelectionGroups[i].RecordUndo("ApplyZ");

                foreach (int instanceIndex in m_SelectionGroups[i].SelectionIndices)
                {
                    float3 value = m_Get.Invoke(m_SelectionGroups[i], instanceIndex);
                    value.z = evt.newValue;
                    m_Set.Invoke(m_SelectionGroups[i], instanceIndex, value);
                }
            }

            m_Z.showMixedValue = false;
            m_Z.SetValueWithoutNotify(evt.newValue);
            Changed?.Invoke();
            SelectionInspector.IgnoreModificationCallbacks = false;
        }

        private static bool HasMultipleValues<T>(IReadOnlyList<T> elements, EqualityComparer<T> comparer)
        {
            if (elements.Count < 2)
                return false;

            T first = elements[0];
            for (int i = 1; i < elements.Count; ++i)
                if (!comparer.Invoke(first, elements[i]))
                    return true;

            return false;
        }
    }
}
