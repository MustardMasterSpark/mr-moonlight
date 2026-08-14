using System.Collections.Generic;
using UnityEngine;
using System;

namespace AdvancedHorrorFPS
{
    public class ItemFactory : MonoBehaviour
    {
        public static ItemFactory Instance;

        // Inspector'dan atayacaðýn prefablar
        public GameObject PistolPrefab;
        public GameObject KeyPrefab;
        public GameObject BaseballPrefab;
        public GameObject MedKitPrefab;

        private Dictionary<string, GameObject> itemPrefabs;

        private void Awake()
        {
            Instance = this;
            itemPrefabs = new Dictionary<string, GameObject>
            {
                { ItemType.Pistol.ToString(), PistolPrefab },
                { ItemType.Key.ToString(), KeyPrefab },
                { ItemType.BaseBallStick.ToString(), BaseballPrefab },
                { ItemType.MedKit.ToString(), MedKitPrefab }
            };
        }

        public static GameObject CreateItemByTypeAndID(string itemType, string Name, string Id)
        {
            // Prefab'larý önceden Inspector'dan atadýðýný varsayýyorum
            if (!Instance.itemPrefabs.TryGetValue(itemType, out GameObject prefab))
            {
                Debug.LogWarning("Prefab bulunamadý: " + itemType);
                return null;
            }

            GameObject item = GameObject.Instantiate(prefab);
            var itemScript = item.GetComponent<ItemScript>();
            itemScript.itemType = (ItemType)System.Enum.Parse(typeof(ItemType), itemType);

            // Anahtar ise KeyID'yi ata, diðerlerinde Name'i ata
            if (itemScript.itemType == ItemType.Key)
            {
                var keyScript = item.GetComponent<KeyScript>();
                keyScript.KeyID = Convert.ToInt32(Id);
                itemScript.Name = Name;
            }
            else
            {
                itemScript.Name = Name;
            }

            return item;
        }


        public static GameObject CreateItemByType(string itemType)
        {
            if (Instance.itemPrefabs.TryGetValue(itemType, out GameObject prefab))
            {
                return Instantiate(prefab);
            }
            return null;
        }
    }
}
