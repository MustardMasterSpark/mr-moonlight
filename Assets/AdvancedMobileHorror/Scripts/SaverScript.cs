using UnityEngine;

namespace AdvancedHorrorFPS
{
    public class SaverScript : MonoBehaviour
    {
        public int SavingIndex = 0;

        private void OnTriggerEnter(Collider other)
        {

            if (other.CompareTag("Player"))
            {
                if (PlayerPrefs.GetInt("SavingIndex", -1) < SavingIndex)
                {
                    PlayerPrefs.SetInt("SavingIndex", SavingIndex);
                    PlayerPrefs.Save();
                }
                Debug.Log("Inventory and Position Saved...");
                InventoryManager.Instance.SaveInventory();
            }
        }
    }
}