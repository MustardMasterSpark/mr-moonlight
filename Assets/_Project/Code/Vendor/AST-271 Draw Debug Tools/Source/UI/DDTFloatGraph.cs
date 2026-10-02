using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DDT
{
    public class DDTFloatGraph : MonoBehaviour
    {
        public Text m_GraphName;
        public Text m_Value;
        public Text m_MinValue;
        public Text m_MaxValue;

        private const float RangeDecayPerSecond = 2.0f;

        private GraphLineRendererUI m_GraphLineRenderer;
        private float m_Width;
        private float m_Height;
        private int m_SamplesCount;
        private float GraphPointsSpace = 1.0f;
        private bool m_IsMeasured;
        private float LastMaxValue = 0.0f, LastMinValue = 0.0f;

        private string m_LabelledName;
        private float m_LabelledValue = float.NaN;
        private float m_LabelledMin = float.NaN;
        private float m_LabelledMax = float.NaN;

        private void Awake()
        {
            m_GraphLineRenderer = GetComponentInChildren<GraphLineRendererUI>();
            m_SamplesCount = DrawDebugTools.Instance != null
                ? DrawDebugTools.Instance.GetFloatGraphSamplesCount()
                : 100;

            EnsurePointCapacity(m_SamplesCount);
        }

        /// <summary>
        /// Reads the rect size once the layout has run. Points are plotted only after this.
        /// </summary>
        private IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();

            RectTransform Rect = transform.GetComponent<RectTransform>();
            m_Width = Rect.sizeDelta.x;
            m_Height = Rect.sizeDelta.y;
            GraphPointsSpace = m_SamplesCount > 0 ? m_Width / m_SamplesCount : 1.0f;
            m_IsMeasured = true;
        }

        internal void UpdateGraphPoints(List<float> FloatValues, float ValueLength, float MinVal, float MaxVal)
        {
            if (!m_IsMeasured)
                return;

            EnsurePointCapacity(FloatValues.Count);

            float HeightModifier = 1.0f;

            // The range expands to a new extreme at once and eases back down
            if (MaxVal > LastMaxValue)
                LastMaxValue = MaxVal;
            else
                LastMaxValue = Mathf.Lerp(LastMaxValue, MaxVal, RangeDecayPerSecond * Time.unscaledDeltaTime);

            if (MinVal < LastMinValue)
                LastMinValue = MinVal;
            else
                LastMinValue = Mathf.Lerp(LastMinValue, MinVal, RangeDecayPerSecond * Time.unscaledDeltaTime);

            if ((LastMaxValue - LastMinValue) != 0.0f)
            {
                HeightModifier = m_Height / (LastMaxValue - LastMinValue);
            }

            float CenterOffset = (LastMinValue + LastMaxValue) / 2.0f;
            for (int i = 0; i < FloatValues.Count; i++)
            {
                m_GraphLineRenderer.m_Points[i] = new Vector2(i * GraphPointsSpace,
                    (FloatValues[i] - CenterOffset) * HeightModifier);
            }

            m_GraphLineRenderer.MaxPointsToDraw = FloatValues.Count;
            m_GraphLineRenderer.SetAllDirty();
        }

        /// <summary>
        /// Writes the name and value labels, skipping any whose value has not changed.
        /// </summary>
        internal void UpdateLabels(string Name, float Value, float Minimum, float Maximum)
        {
            if (!string.Equals(m_LabelledName, Name))
            {
                m_GraphName.text = Name;
                m_LabelledName = Name;
            }

            if (Value != m_LabelledValue)
            {
                m_Value.text = Value.ToString();
                m_LabelledValue = Value;
            }

            if (Minimum != m_LabelledMin)
            {
                m_MinValue.text = "MIN " + Minimum.ToString();
                m_LabelledMin = Minimum;
            }

            if (Maximum != m_LabelledMax)
            {
                m_MaxValue.text = "MAX " + Maximum.ToString();
                m_LabelledMax = Maximum;
            }
        }

        public void SetGraphColor(Color color)
        {
            m_GraphLineRenderer.color = color;
        }

        /// <summary>Grows the plotted point list so every sample has a slot.</summary>
        private void EnsurePointCapacity(int Count)
        {
            if (m_GraphLineRenderer == null)
                return;

            if (m_GraphLineRenderer.m_Points == null)
                m_GraphLineRenderer.m_Points = new List<Vector2>();

            while (m_GraphLineRenderer.m_Points.Count < Count)
            {
                m_GraphLineRenderer.m_Points.Add(Vector2.zero);
            }
        }
    }
}
