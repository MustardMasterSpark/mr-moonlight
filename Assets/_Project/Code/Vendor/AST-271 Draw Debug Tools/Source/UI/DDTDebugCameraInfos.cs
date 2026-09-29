using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DDT
{
    public class DDTDebugCameraInfos : MonoBehaviour {

    	[Space(10)]
    	public DDTDebugValueText m_CamPosText;
    	public DDTDebugValueText m_CamRotText;
    	public DDTDebugValueText m_CamSpeedText;

    	[Space(10)]
    	[Header("Raycast Infos")]
    	public RectTransform m_ColliderRaycastPanel;
    	public DDTDebugValueText m_RaycastInfos_ValText_1;
    	public DDTDebugValueText m_RaycastInfos_ValText_2;
    	public DDTDebugValueText m_RaycastInfos_ValText_3;
    	public DDTDebugValueText m_RaycastInfos_ValText_4;
    	public DDTDebugValueText m_RaycastInfos_ValText_5;
    	public DDTDebugValueText m_RaycastInfos_ValText_6;
    	public DDTDebugValueText m_RaycastInfos_ValText_7;
    	public DDTDebugValueText m_RaycastInfos_ValText_8;
    	[Header("Mesh Materials Infos")]
    	public GameObject m_MaterialListEntryPrefab;
    	public RectTransform m_RaycastMeshMatsDebugInfosParent;
    	public List<DDTDebugValueText> m_MaterialsListTextsList;

    	[Space(10)]
    	public Text m_DeltaTimeText;
    	public Text m_TimeScaleText;

    	private float m_TimeControlStep = 0.1f;
    	private float m_MaxTimeControlScale = 100.0f;

    	// Instantiate runs Awake before it returns but defers Start, and the caller fills
    	// the material list in that same frame, so it is set up here
    	void Awake () {
    		m_MaterialsListTextsList = new List<DDTDebugValueText>();
    		m_ColliderRaycastPanel.gameObject.SetActive(false);
    		m_RaycastMeshMatsDebugInfosParent.gameObject.SetActive(false);

    		DDTSettings Settings = DrawDebugTools.Instance != null
    			? DrawDebugTools.Instance.m_DDTSettings : null;
    		if (Settings != null)
    			m_TimeControlStep = Settings.m_TimeControlStep;
    	}

    	/// <summary>
    	/// The hit's texture coordinate for the given UV channel, or "none" when the collider is not a
    	/// mesh collider or its mesh has no such channel.
    	/// </summary>
    	private static string TextureCoordText(RaycastHit HitInfos, int Channel)
    	{
    		MeshCollider HitMeshCollider = HitInfos.collider as MeshCollider;
    		if (HitMeshCollider == null || HitMeshCollider.sharedMesh == null
    		    || !HitMeshCollider.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0 + Channel))
    			return "none";

    		return (Channel == 0 ? HitInfos.textureCoord : HitInfos.textureCoord2).ToString();
    	}

    	public void UpdateInfos (RaycastHit HitInfos) {
    		// Cam transform and speed
    		m_CamPosText.SetText("position: " + DrawDebugTools.Instance.DebugCamera.transform.position.ToString());
    		m_CamRotText.SetText("rotation: " + DrawDebugTools.Instance.DebugCamera.transform.eulerAngles.ToString());
    		m_CamSpeedText.SetText("cam speed (mouse wheel): " + DrawDebugTools.Instance.DebugCamera.GetDebugCameraMovSpeedMultiplier().ToString());

    		// Raycast infos
    		if (HitInfos.collider != null)
    		{
    			if (!m_ColliderRaycastPanel.gameObject.activeInHierarchy)
    				m_ColliderRaycastPanel.gameObject.SetActive(true);

    			m_RaycastInfos_ValText_1.SetText("ray hit point: " + HitInfos.point.ToString());
    			m_RaycastInfos_ValText_2.SetText("ray hit normal: " + HitInfos.normal.ToString());
    			m_RaycastInfos_ValText_3.SetText("ray hit barycentricCoordinate: " + HitInfos.barycentricCoordinate.ToString());
    			m_RaycastInfos_ValText_4.SetText("ray hit distance: " + HitInfos.distance.ToString());
    			m_RaycastInfos_ValText_5.SetText("ray hit triangleIndex: " + HitInfos.triangleIndex.ToString());
    			m_RaycastInfos_ValText_6.SetText("ray hit textureCoord: " + TextureCoordText(HitInfos, 0));
    			m_RaycastInfos_ValText_7.SetText("ray hit textureCoord2: " + TextureCoordText(HitInfos, 1));
    			m_RaycastInfos_ValText_8.SetText("ray hit Object name: \"" + HitInfos.transform.name + "\"");

                if (HitInfos.transform.GetComponent<MeshRenderer>() != null || HitInfos.transform.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    if (!m_RaycastMeshMatsDebugInfosParent.gameObject.activeInHierarchy)
                        m_RaycastMeshMatsDebugInfosParent.gameObject.SetActive(true);

                    Material[] MatsArray;
                    if (HitInfos.transform.GetComponent<MeshRenderer>() != null)
                        MatsArray = HitInfos.transform.GetComponent<MeshRenderer>().sharedMaterials;
                    else
                        MatsArray = HitInfos.transform.GetComponent<SkinnedMeshRenderer>().sharedMaterials;

                    int DiffNum = Mathf.Abs(m_MaterialsListTextsList.Count - MatsArray.Length);

                    if (DiffNum != 0)
                    {
                        if (m_MaterialsListTextsList.Count > MatsArray.Length)
                        {
                            for (int i = m_MaterialsListTextsList.Count - 1; i >= m_MaterialsListTextsList.Count - DiffNum; i--)
                            {
                                if (i < m_MaterialsListTextsList.Count)
                                {
                                    GameObject TextObj = m_MaterialsListTextsList[i].gameObject;
                                    Destroy(TextObj);
                                }
                            }
                            m_MaterialsListTextsList.RemoveRange(m_MaterialsListTextsList.Count - DiffNum, DiffNum);
                        }
                        else
                        {
                            for (int i = 0; i < DiffNum; i++)
                            {
                                m_MaterialsListTextsList.Add(InstantiateMeshMatListEntry());
                            }
                        }
                    }

                    // Display the mesh material names
                    for (int i = 0; i < MatsArray.Length; i++)
                    {
                        m_MaterialsListTextsList[i].SetText("Mat (" + i + "): " + (MatsArray[i] != null ? MatsArray[i].name : "None"));
                        m_MaterialsListTextsList[i].GetComponent<RectTransform>().SetAsLastSibling();
                    }
                }
                else
                {
                    if (m_RaycastMeshMatsDebugInfosParent.gameObject.activeInHierarchy)
                        m_RaycastMeshMatsDebugInfosParent.gameObject.SetActive(false);
                }
            }
    		else
    		{
    			if (m_ColliderRaycastPanel.gameObject.activeInHierarchy)
    				m_ColliderRaycastPanel.gameObject.SetActive(false);
    			if (m_RaycastMeshMatsDebugInfosParent.gameObject.activeInHierarchy)
    				m_RaycastMeshMatsDebugInfosParent.gameObject.SetActive(false);
    		}

    		// Delta time
    		m_DeltaTimeText.text = "delta time: " + Time.deltaTime;
    		m_TimeScaleText.text = "time scale: " + Time.timeScale;
    	}

    	private DDTDebugValueText InstantiateMeshMatListEntry()
    	{
    		GameObject MeshMatNameText = GameObject.Instantiate(m_MaterialListEntryPrefab);
    		MeshMatNameText.transform.SetParent(m_RaycastMeshMatsDebugInfosParent);
    		MeshMatNameText.transform.SetAsFirstSibling();
    		MeshMatNameText.name = "MeshMat_ValText_" + MeshMatNameText.GetInstanceID();
    		return MeshMatNameText.GetComponent<DDTDebugValueText>();
    	}

    	public void PauseTime()
    	{
    		Time.timeScale = 0.0f;
    		DrawDebugTools.Instance.NotifyTimeScaleChangedByPanel();
    	}

    	/// <summary>Restores the time scale the game ran at before the debug camera froze it.</summary>
    	public void ResumeTime()
    	{
    		Time.timeScale = DrawDebugTools.Instance.TimeScaleBeforeFreeze;
    		DrawDebugTools.Instance.NotifyTimeScaleChangedByPanel();
    	}

    	public void SpeedUpTime()
    	{
    		float NewTimeScale = Time.timeScale;
    		NewTimeScale += m_TimeControlStep;
    		Time.timeScale = Mathf.Clamp(NewTimeScale, 0.0f, m_MaxTimeControlScale);
    		DrawDebugTools.Instance.NotifyTimeScaleChangedByPanel();
    	}

    	public void SlowDownTime()
    	{
    		float NewTimeScale = Time.timeScale;
    		NewTimeScale -= m_TimeControlStep;
    		Time.timeScale = Mathf.Clamp(NewTimeScale, 0.0f, m_MaxTimeControlScale);
    		DrawDebugTools.Instance.NotifyTimeScaleChangedByPanel();
    	}
    }
}
