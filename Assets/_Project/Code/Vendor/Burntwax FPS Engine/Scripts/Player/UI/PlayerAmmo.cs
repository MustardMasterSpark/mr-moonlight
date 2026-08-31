using TMPro;
using UnityEngine;

namespace Burntwax
{

    [DisallowMultipleComponent]

    public class PlayerAmmo : MonoBehaviour
    {


        [SerializeField] private GunStateMachine gunSelector;
        [SerializeField] private TextMeshProUGUI ammoText;



        void Update()
        {
            if (gunSelector.ActiveGunScriptable != null)
            {
                ammoText.SetText($"{gunSelector.ActiveGunScriptable.AmmoConfig.currentClip} / {gunSelector.ActiveGunScriptable.AmmoConfig.currentAmmo}");
            }
            else
            {
                ammoText.SetText("");
            }
        }
    }

}