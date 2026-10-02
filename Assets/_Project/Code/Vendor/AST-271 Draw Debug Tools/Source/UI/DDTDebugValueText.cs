using UnityEngine;
using UnityEngine.UI;

namespace DDT
{
    public class DDTDebugValueText : MonoBehaviour {

    	private Text m_Text;

        void Awake () {
            m_Text = transform.GetChild(0).GetComponent<Text>();
        }

        public void SetText(string InText)
        {
            m_Text.text = InText;
        }

    }
}
