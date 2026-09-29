using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DDT
{
    public class DDTCanvas : MonoBehaviour {

        // Log
    	public Transform m_LogTextsParent;
        public GameObject m_LogTextPrefab;
        public List<Text> m_LogTextsList;

        // Float graph
        public Transform m_FloatGraphParent;
        public GameObject m_FloatGraphPrefab;
        public List<DDTFloatGraph> m_FloatGraphsList;

        // Debug camera
        public GameObject m_DebugCameraInfosPrefab;
        private DDTDebugCameraInfos m_DebugCameraInfos;

        public DDTDebugCameraInfos DebugCameraInfos
        {
            get
            {
                return m_DebugCameraInfos;
            }

            set
            {
                m_DebugCameraInfos = value;
            }
        }

        // Instantiate runs Awake before it returns but defers Start, and the caller fills
        // these lists in that same frame, so they are set up here
        void Awake () {
            m_LogTextsList = new List<Text>();
            m_FloatGraphsList = new List<DDTFloatGraph>();

            // Draw this canvas above everything else
            GetComponent<Canvas>().sortingOrder = 1000;
        }

        public void UpdateLogTexts(List<DebugLogMessage> LogMessagesList)
        {
            int DiffNum = Mathf.Abs(m_LogTextsList.Count - LogMessagesList.Count);

            // Clean log text container
            if (DiffNum != 0)
            {
                if (m_LogTextsList.Count > LogMessagesList.Count)
                {
                    for (int i = m_LogTextsList.Count - 1; i >= m_LogTextsList.Count - DiffNum; i--)
                    {
                        if (i < m_LogTextsList.Count)
                        {
                            GameObject TextObj = m_LogTextsList[i].gameObject;
                            Destroy(TextObj);
                        }
                    }
                    m_LogTextsList.RemoveRange(m_LogTextsList.Count - DiffNum, DiffNum);
                }
                else
                {
                    for (int i = 0; i < DiffNum; i++)
                    {
                        m_LogTextsList.Add(InstantiateLogText());
                    }
                }
            }

            // Update text and color
            for (int i = 0; i < LogMessagesList.Count; i++)
            {
                m_LogTextsList[i].text = LogMessagesList[i].m_LogMessageText;
                m_LogTextsList[i].color = LogMessagesList[i].m_Color;
            }
        }

        public void UpdateGraphFloats(List<DebugFloatGraph> FloatGraphsList)
        {
            int DiffNum = Mathf.Abs(m_FloatGraphsList.Count - FloatGraphsList.Count);

            // Clean float graph container
            if (DiffNum != 0)
            {
                if (m_FloatGraphsList.Count > FloatGraphsList.Count)
                {
                    for (int i = m_FloatGraphsList.Count - 1; i >= m_FloatGraphsList.Count - DiffNum; i--)
                    {
                        if (i < m_FloatGraphsList.Count)
                        {
                            GameObject TextObj = m_FloatGraphsList[i].gameObject;
                            Destroy(TextObj);
                        }
                    }
                    m_FloatGraphsList.RemoveRange(m_FloatGraphsList.Count - DiffNum, DiffNum);
                }
                else
                {
                    for (int i = 0; i < DiffNum; i++)
                    {
                        m_FloatGraphsList.Add(InstantiateFloatGraph());
                    }
                }
            }

            // Update text and color
            for (int i = 0; i < FloatGraphsList.Count; i++)
            {
                if (FloatGraphsList[i].m_FloatValuesList.Count == 0)
                    continue;
                DebugFloatGraph Source = FloatGraphsList[i];
                float Minimum = Source.GetMinimumValue();
                float Maximum = Source.GetMaximumValue();
                float Latest = Source.m_FloatValuesList[Source.m_FloatValuesList.Count - 1];

                m_FloatGraphsList[i].UpdateLabels(Source.m_UniqueFloatName, Latest, Minimum, Maximum);
                m_FloatGraphsList[i].UpdateGraphPoints(Source.m_FloatValuesList, Source.m_GraphValueLength,
                    Minimum, Maximum);
                m_FloatGraphsList[i].SetGraphColor(FloatGraphsList[i].m_GraphColor);
            }
        }

        public void UpdateDebugCamera(RaycastHit HitInfos)
        {
            if (m_DebugCameraInfos == null)
                m_DebugCameraInfos = GameObject.Instantiate(m_DebugCameraInfosPrefab, transform).GetComponent<DDTDebugCameraInfos>();
            else if (!m_DebugCameraInfos.gameObject.activeInHierarchy)
                m_DebugCameraInfos.gameObject.SetActive(true);

            m_DebugCameraInfos.UpdateInfos(HitInfos);
        }

        private Text InstantiateLogText()
        {
            GameObject LogText = GameObject.Instantiate(m_LogTextPrefab);
            LogText.transform.SetParent(m_LogTextsParent);
            LogText.transform.SetAsFirstSibling();
            LogText.name = "LogText-" + LogText.GetInstanceID();
            return LogText.GetComponent<Text>();
        }

        private DDTFloatGraph InstantiateFloatGraph()
        {
            GameObject FloatGraph = GameObject.Instantiate(m_FloatGraphPrefab);
            FloatGraph.transform.SetParent(m_FloatGraphParent);
            FloatGraph.name = "FloatGraph-" + FloatGraph.GetInstanceID();
            return FloatGraph.GetComponent<DDTFloatGraph>();
        }
    }
}