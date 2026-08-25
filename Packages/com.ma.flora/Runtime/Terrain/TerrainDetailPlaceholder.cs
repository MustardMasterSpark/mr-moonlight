using UnityEngine;

namespace MA.Flora
{
    [RequireComponent(typeof(BillboardRenderer))]
    [AddComponentMenu("")]
    internal class TerrainDetailPlaceholder : MonoBehaviour
    {
        private BillboardRenderer m_BillboardRenderer;

        public BillboardRenderer BillboardRenderer
        {
            get
            {
                if (!m_BillboardRenderer && TryGetComponent(out BillboardRenderer billboardRenderer))
                    m_BillboardRenderer = billboardRenderer;

                return m_BillboardRenderer;
            }
        }
    }
}
