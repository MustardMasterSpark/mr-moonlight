using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AdvancedHorrorFPS
{
    public class HeroPlayerScript : MonoBehaviour
    {
        public static HeroPlayerScript Instance;
        public GameObject LadderPointInCamera;
        public FirstPersonController firstPersonController;
        public CharacterController characterController;
        public Transform DemonComingPoint;
        public int Health = 100;
        public List<int> Keys_Grabbed = new List<int>();
        [HideInInspector]
        public GameObject Carrying_Ladder = null;
        public GameObject FPSHands;
        public FlashLightScript FlashLight;
        public bool isHoldingBox = false;
        public GameObject Hand_FlashLight;
        public GameObject Hand_Pistol;
        public GameObject Hand_Baseball;
        public GameObject Hand_Key;
        public bool isHiding = false;
        public HideScript hidingPlace;
        public GameObject MainCamera;
        public bool isCaughtByEnemy = false;
        private int currentKeyIdinHands = -1;
        private bool isHeroBusy = false;
        public float lastInteractionTime = 0;
        

        void Start()
        {
            Time.timeScale = 1;
            GameCanvas.Instance.UpdateHealth();
        }

        public void SetHeroBusy(bool b)
        {
            isHeroBusy = b;
        }

        public void ToggleHandObjects()
        {
            StartCoroutine(HideHands());
        }

        IEnumerator HideHands()
        {
            yield return new WaitForSeconds(0.1f);
            FPSHands.SetActive(false);
        }

        public bool GetHeroBusy()
        {
            return isHeroBusy;
        }

        public int GetCurrentKey()
        {
            return currentKeyIdinHands;
        }

        public void Escape()
        {
            isCaughtByEnemy = false;
            ActivatePlayer();
            GameCanvas.Instance.Image_CaughtBlood.SetActive(false);
        }

        public void GetCaught()
        {
            isCaughtByEnemy = true;
            GameCanvas.Instance.Image_CaughtBlood.SetActive(true);
            GameCanvas.Instance.Panel_ClickToEscape.SetActive(true);
            StartCoroutine(Die());
        }

        public IEnumerator Die()
        {
            yield return new WaitForSeconds(AdvancedGameManager.Instance.durationForClickingTime);
            if (isCaughtByEnemy)
            {
                GetDamage(100);
            }
        }

        public void GetDamage(int Damage)
        {
            Health = Health - Damage;
            isCaughtByEnemy = false;
            GameCanvas.Instance.Show_Blood_Effect();
            GameCanvas.Instance.Panel_ClickToEscape.SetActive(false);
            if (Health < 0) Health = 0;
            GameCanvas.Instance.UpdateHealth();
            if (Health <= 0)
            {
                DeactivatePlayer();
                CameraLook.Instance.fCamShakeImpulse = 1;
                GameCanvas.Instance.Show_GameOverPanel();
                HeroPlayerScript.Instance.FPSHands.SetActive(false);
                HeroPlayerScript.Instance.Hand_FlashLight.SetActive(false);
                HeroPlayerScript.Instance.Hand_Pistol.SetActive(false);
                HeroPlayerScript.Instance.Hand_Key.SetActive(false);
                if (AdvancedGameManager.Instance.gameOverEventToInvoke != null)
                {
                    AdvancedGameManager.Instance.gameOverEventToInvoke.Invoke();
                }
            }
            else
            {
                CameraLook.Instance.fCamShakeImpulse = 0.5f;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (isCaughtByEnemy && AdvancedGameManager.Instance.canPlayerEscapeByClickEnough)
            {
                CameraLook.Instance.fCamShakeImpulse = 0.25f;
                if (AdvancedGameManager.Instance.mouseAction.WasPressedThisFrame())
                {
                    GameCanvas.Instance.Click_Escape();
                }
            }
            HandleRaycastInteraction();
        }

        public float interactDistance = 3f;
        [HideInInspector]
        public ItemScript currentItem;
        Ray ray;

        void HandleRaycastInteraction()
        {
            if (!isHoldingBox)
            {
                if (AdvancedGameManager.Instance.controllerType == ControllerType.PcAndConsole)
                {
                    ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
                }
                else // Mobil
                {
                    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    {
                        Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();
                        ray = Camera.main.ScreenPointToRay(touchPos);
                    }
                    else
                    {
                        // Dokunma yoksa merkezden ray at
                        ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2));
                    }
                }
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, interactDistance))
                {
                    ItemScript item = hit.collider.GetComponent<ItemScript>();
                    if (item != null)
                    {
                        ShowInteractionWarning(item);
                        if (AdvancedGameManager.Instance.interactAction.WasPressedThisFrame())
                        {
                            item.Interact();
                            currentItem = item;
                        }
                    }
                    else
                    {
                        GameCanvas.Instance.Hide_Warning();
                    }
                }
                else
                {
                    GameCanvas.Instance.Hide_Warning();
                }
            }
            else
            {
                if (AdvancedGameManager.Instance.interactAction.WasPressedThisFrame() && currentItem != null)
                {
                    currentItem.Interact();
                }
            }
        }

        [HideInInspector]
        public ItemScript currentLookingItem = null;

        void ShowInteractionWarning(ItemScript item)
        {
            currentLookingItem = item;
            if (AdvancedGameManager.Instance.controllerType == ControllerType.PcAndConsole)
            {
                switch (item.interactionType)
                {
                    case InteractionType.Grab:
                        GameCanvas.Instance.Show_Warning("Press E to Grab");
                        break;
                    case InteractionType.Interact:
                        GameCanvas.Instance.Show_Warning("Press E to Interact");
                        break;
                    case InteractionType.KnockDown:
                        GameCanvas.Instance.Show_Warning("Press E to Knock Down");
                        break;
                    case InteractionType.Carry:
                        GameCanvas.Instance.Show_Warning("Press E to Carry");
                        break;
                    case InteractionType.Put:
                        GameCanvas.Instance.Show_Warning("Press E to Put");
                        break;
                    case InteractionType.Hide:
                        GameCanvas.Instance.Show_Warning("Press E to Hide");
                        break;
                    case InteractionType.Open:
                        GameCanvas.Instance.Show_Warning("Press E to Open");
                        break;
                    case InteractionType.Read:
                        GameCanvas.Instance.Show_Warning("Press E to Read");
                        break;
                    case InteractionType.PressAndHold:
                        GameCanvas.Instance.Show_Warning("Press and Hold to Maintain");
                        break;
                    default:
                        GameCanvas.Instance.Show_Warning("Press E");
                        break;
                }
            }
        }

        public void Take_BaseballBat_OnHand()
        {
            FPSHands.transform.GetChild(0).GetComponent<Animation>().Play();
            StartCoroutine(ActivateBaseballBat());
        }

        IEnumerator ActivateBaseballBat()
        {
            yield return new WaitForSeconds(0.3f);
            PistolScript.Instance.enabled = false;
            BaseballScript.Instance.enabled = true;
            BaseballScript.Instance.isGrabbed = true;
            HeroPlayerScript.Instance.Hand_Baseball.SetActive(true);
            HeroPlayerScript.Instance.Hand_FlashLight.SetActive(false);
            HeroPlayerScript.Instance.Hand_Key.SetActive(false);
            HeroPlayerScript.Instance.Hand_Pistol.SetActive(false);
            GameCanvas.Instance.Deactivate_PistolPanel();
            GameCanvas.Instance.Activate_FiringButton();
            GameCanvas.Instance.Switch_HitButtonToBaseball();
        }

        public void Take_Pistol_OnHand()
        {
            FPSHands.transform.GetChild(0).GetComponent<Animation>().Play();
            StartCoroutine(ActivatePistol());
        }

        IEnumerator ActivatePistol()
        {
            yield return new WaitForSeconds(0.3f);
            PistolScript.Instance.enabled = true;
            PistolScript.Instance.isGrabbed = true;
            BaseballScript.Instance.enabled = false;
            HeroPlayerScript.Instance.Hand_Baseball.SetActive(false);
            HeroPlayerScript.Instance.Hand_FlashLight.SetActive(false);
            HeroPlayerScript.Instance.Hand_Key.SetActive(false);
            HeroPlayerScript.Instance.Hand_Pistol.SetActive(true);
            GameCanvas.Instance.Activate_PistolPanel();
            GameCanvas.Instance.Activate_FiringButton();
            GameCanvas.Instance.Switch_HitButtonToPistol();
        }

        public void Take_Key_OnHand(int keyID)
        {
            currentKeyIdinHands = keyID;
            FPSHands.transform.GetChild(0).GetComponent<Animation>().Play();
            StartCoroutine(ActivateKey());
        }

        IEnumerator ActivateKey()
        {
            yield return new WaitForSeconds(0.3f);
            PistolScript.Instance.enabled = false;
            BaseballScript.Instance.enabled = false;
            HeroPlayerScript.Instance.Hand_Baseball.SetActive(false);
            HeroPlayerScript.Instance.Hand_FlashLight.SetActive(false);
            HeroPlayerScript.Instance.Hand_Pistol.SetActive(false);
            GameCanvas.Instance.Deactivate_FiringButton();
            HeroPlayerScript.Instance.Hand_Key.SetActive(true);
            GameCanvas.Instance.Deactivate_PistolPanel();
        }

        public void Grab_Key(int ID)
        {
            Keys_Grabbed.Add(ID);
        }

        public void Get_Health()
        {
            Health = Health + 50;
            if (Health > 100) Health = 100;
            GameCanvas.Instance.UpdateHealth();
            GameCanvas.Instance.IncreaseHealth();
            AudioManager.Instance.Play_Audio_Healing();
        }

        public void ActivatePlayer()
        {
            transform.eulerAngles = Vector3.zero;
            transform.localEulerAngles = Vector3.zero;
            firstPersonController.enabled = true;
            characterController.enabled = true;
            transform.eulerAngles = Vector3.zero;
            transform.localEulerAngles = Vector3.zero;
        }

        public void DeactivatePlayer()
        {
            firstPersonController.enabled = false;
            characterController.enabled = false;
        }

        public void ChangeTag(bool hide)
        {
            if (hide)
            {
                gameObject.tag = "Untagged";
            }
            else
            {
                gameObject.tag = "Player";
            }
        }

        public void ActivatePlayerInputs()
        {
            firstPersonController.enabled = true;
            characterController.enabled = true;
        }
    }
}