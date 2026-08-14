using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AdvancedHorrorFPS
{
    public class ItemScript : MonoBehaviour
    {
        public ItemType itemType;
        public string Name = "";
        public InteractionType interactionType;
        public UnityEvent eventToInvokeWhenInteract;
        private bool isGrabbedBefore = false;

        public void Interact()
        {
            if (InventoryManager.Instance.isInventoryOpened) return;
            if (Time.time < HeroPlayerScript.Instance.lastInteractionTime + 0.5f) return;

            HeroPlayerScript.Instance.lastInteractionTime = Time.time;
            GameCanvas.Instance.Hide_Warning();
            if (itemType == ItemType.Flashlight)
            {
                AudioManager.Instance.Play_Item_Grab();
                HeroPlayerScript.Instance.FlashLight.enabled = true;
                FlashLightScript.Instance.Grabbed();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
                Destroy(gameObject);
            }
            else if (itemType == ItemType.Door)
            {
                GetComponent<DoorScript>().TryToOpen();
            }
            else if (itemType == ItemType.Key)
            {
                if (InventoryManager.Instance.Has_Inventory_Room())
                {
                    AudioManager.Instance.Play_Item_Grab();
                    GetComponent<KeyScript>().isGrabbed = true;
                    HeroPlayerScript.Instance.Grab_Key(GetComponent<KeyScript>().KeyID);
                    if (eventToInvokeWhenInteract != null)
                    {
                        eventToInvokeWhenInteract.Invoke();
                    }
                    // Add to Inventory
                    InventoryManager.Instance.Add_Item(gameObject);
                }
                else
                {
                    GameCanvas.Instance.Show_Warning("No room in Inventory");
                }
            }
            else if (itemType == ItemType.Note && !GetComponent<NoteScript>().isReading)
            {
                GetComponent<NoteScript>().Read();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Pistol)
            {
                if (InventoryManager.Instance.Has_Inventory_Room())
                {
                    PistolScript.Instance.enabled = true;
                    BaseballScript.Instance.enabled = false;
                    PistolScript.Instance.Grabbed(isGrabbedBefore);
                    isGrabbedBefore = true;
                    AudioManager.Instance.Play_Item_Grab();
                    if (eventToInvokeWhenInteract != null)
                    {
                        eventToInvokeWhenInteract.Invoke();
                    }
                    // Add to Inventory
                    InventoryManager.Instance.Add_Item(gameObject);
                }
                else
                {
                    GameCanvas.Instance.Show_Warning("No room in Inventory");
                }
            }
            else if (itemType == ItemType.Light)
            {
                GetComponent<LightScript>().Interact();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.BaseBallStick)
            {
                if (InventoryManager.Instance.Has_Inventory_Room())
                {
                    BaseballScript.Instance.enabled = true;
                    PistolScript.Instance.enabled = false;
                    BaseballScript.Instance.Grabbed();
                    AudioManager.Instance.Play_Item_Grab();
                    if (eventToInvokeWhenInteract != null)
                    {
                        eventToInvokeWhenInteract.Invoke();
                    }
                    // Add to Inventory
                    InventoryManager.Instance.Add_Item(gameObject);
                }
                else
                {
                    GameCanvas.Instance.Show_Warning("No room in Inventory");
                }
            }
            else if (itemType == ItemType.PistolAmmo)
            {
                GetComponent<PistolAmmoScript>().GrabIt();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Note && GetComponent<NoteScript>().isReading)
            {
                GameCanvas.Instance.Hide_Note();
            }
            else if (itemType == ItemType.Box)
            {
                GetComponent<BoxScript>().Interact();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.LadderPuttingArea)
            {
                if (HeroPlayerScript.Instance.Carrying_Ladder != null && !HeroPlayerScript.Instance.Carrying_Ladder.GetComponent<LadderScript>().isPut)
                {
                    GameCanvas.Instance.Drop_GrabbedLadder(transform);
                }
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Cabinet)
            {
                GetComponent<CabinetScript>().Open();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Drawer)
            {
                GetComponent<DrawerScript>().Interact();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.MedKit)
            {
                if (InventoryManager.Instance.Has_Inventory_Room())
                {
                    AudioManager.Instance.Play_Item_Grab();
                    if (eventToInvokeWhenInteract != null)
                    {
                        eventToInvokeWhenInteract.Invoke();
                    }
                    // Add to Inventory
                    InventoryManager.Instance.Add_Item(gameObject);
                }
                else
                {
                    GameCanvas.Instance.Show_Warning("No room in Inventory");
                }
            }
            else if (itemType == ItemType.WoodToBreak)
            {
                GetComponent<Rigidbody>().isKinematic = false;
                GetComponent<Rigidbody>().useGravity = true;
                GetComponent<Rigidbody>().AddExplosionForce(100, transform.position, 1);
                GetComponent<Rigidbody>().AddTorque(new Vector3(Random.Range(-90, 90), Random.Range(-90, 90), Random.Range(-90, 90)));
                AudioManager.Instance.Play_WoodBreakable();
                BlinkEffect effect = GetComponent<BlinkEffect>();
                effect.Disable();
                GetComponent<BoxCollider>().enabled = false;
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
                Destroy(gameObject, 2);
            }
            else if (itemType == ItemType.Ladder && !GetComponent<LadderScript>().isPut)
            {
                HeroPlayerScript.Instance.Carrying_Ladder = this.gameObject;
                SphereCollider[] colliders = GetComponents<SphereCollider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
                GetComponent<BoxCollider>().enabled = false;
                BlinkEffect[] blinks = this.gameObject.GetComponentsInChildren<BlinkEffect>();
                for (int i = 0; i < blinks.Length; i++)
                {
                    blinks[i].Disable();
                }
                AudioManager.Instance.Play_Item_Grab();
                transform.parent = HeroPlayerScript.Instance.LadderPointInCamera.transform.parent;
                transform.position = HeroPlayerScript.Instance.LadderPointInCamera.transform.position;
                transform.eulerAngles = HeroPlayerScript.Instance.LadderPointInCamera.transform.eulerAngles;
                transform.localScale = HeroPlayerScript.Instance.LadderPointInCamera.transform.localScale;
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Chest)
            {
                BlinkEffect[] blinks = gameObject.GetComponentsInChildren<BlinkEffect>();
                for (int i = 0; i < blinks.Length; i++)
                {
                    blinks[i].Disable();
                }

                if (GetComponent<HideScript>() != null)
                {
                    GetComponent<HideScript>().Hide();
                }
                else
                {
                    if (!GetComponent<ChestScript>().isOpened)
                    {
                        GetComponent<ChestScript>().Lock.SetActive(true);
                    }
                }
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
            else if (itemType == ItemType.Bed)
            {
                GetComponent<HideScript>().Hide();
                if (eventToInvokeWhenInteract != null)
                {
                    eventToInvokeWhenInteract.Invoke();
                }
            }
        }


        public void DeactivateCollidersAndRigidbody()
        {
            Collider[] allColliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < allColliders.Length; i++)
            {
                allColliders[i].enabled = false;
                transform.tag = "Untagged";
            }
            BlinkEffect[] allBlinks = GetComponentsInChildren<BlinkEffect>();
            for (int i = 0; i < allBlinks.Length; i++)
            {
                allBlinks[i].speed = 0;
            }
            if (GetComponent<Rigidbody>() != null)
            {
                GetComponent<Rigidbody>().isKinematic = true;
                GetComponent<Rigidbody>().useGravity = false;
            }
        }

        public void ActivateCollidersAndRigidbody()
        {
            transform.localScale = new Vector3(1, 1, 1);
            Collider[] allColliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < allColliders.Length; i++)
            {
                allColliders[i].enabled = true;
                transform.tag = "Item";
            }
            if (GetComponent<Rigidbody>() != null)
            {
                GetComponent<Rigidbody>().isKinematic = false;
                GetComponent<Rigidbody>().useGravity = true;
            }
            if (AdvancedGameManager.Instance.blinkOnInteractableObjects)
            {
                BlinkEffect[] blinks = gameObject.GetComponentsInChildren<BlinkEffect>();
                for (int i = 0; i < blinks.Length; i++)
                {
                    blinks[i].speed = 2;
                }
            }
        }


        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (itemType == ItemType.Chest && GetComponent<ChestScript>().Lock != null && GetComponent<ChestScript>().Lock.activeSelf)
                {
                    GetComponent<ChestScript>().Lock.SetActive(false);
                }
                if (AdvancedGameManager.Instance.controllerType == ControllerType.PcAndConsole)
                {
                    GameCanvas.Instance.Hide_Warning();
                    if (interactionType == InteractionType.PressAndHold && GetComponent<ItemToMaintainScript>() != null)
                    {
                        imageToFill.fillAmount = 1;
                        imageToFill.gameObject.SetActive(false);
                    }
                }
            }
        }

        public Image imageToFill;
        private float lastTime = 0;

        private void Update()
        {
            if (interactionType == InteractionType.PressAndHold)
            {
                if (HeroPlayerScript.Instance != null && HeroPlayerScript.Instance.currentLookingItem != null && HeroPlayerScript.Instance.currentLookingItem == this)
                {
                    if (AdvancedGameManager.Instance.mouseAction.inProgress)
                    {
                        if (Time.time > lastTime + 1f)
                        {
                            lastTime = Time.time;
                            imageToFill.fillAmount = imageToFill.fillAmount - (1f / GetComponent<ItemToMaintainScript>().durationForMaintain);
                            HeroPlayerScript.Instance.SetHeroBusy(true);
                            if (imageToFill.fillAmount <= 0)
                            {
                                if (itemType == ItemType.ItemToMaintain)
                                {
                                    if (eventToInvokeWhenInteract != null)
                                    {
                                        eventToInvokeWhenInteract.Invoke();
                                    }
                                    AudioManager.Instance.Play_Audio_PressAndHoldMaintainDone();
                                    Destroy(gameObject);
                                }
                            }
                        }
                        imageToFill.gameObject.SetActive(true);
                    }
                    else if (AdvancedGameManager.Instance.mouseAction.WasReleasedThisFrame())
                    {
                        imageToFill.fillAmount = 1;
                        imageToFill.gameObject.SetActive(false);
                    }
                }
            }
            else if (interactionType == InteractionType.Hide)
            {
                // Hiding currently. Let's get out:
                if (AdvancedGameManager.Instance.interactAction.WasPressedThisFrame() && HeroPlayerScript.Instance.hidingPlace == GetComponent<HideScript>() && HeroPlayerScript.Instance.isHiding)
                {
                    Interact();
                }
            }
            else if (interactionType == InteractionType.Read)
            {
                // Hiding currently. Let's get out:
                if (AdvancedGameManager.Instance.interactAction.WasPressedThisFrame() && GameCanvas.Instance.CurrentNote == GetComponent<NoteScript>() && GetComponent<NoteScript>().isReading)
                {
                    Interact();
                }
            }
        }
    }


    public enum ItemType
    {
        Door,
        Flashlight,
        Key,
        Note,
        Cabinet,
        Ladder,
        WoodToBreak,
        LadderPuttingArea,
        Chest,
        None,
        Drawer,
        Box,
        MedKit,
        Pistol,
        PistolAmmo,
        BaseBallStick,
        Bed,
        ItemToMaintain,
        Light,
    }

    public enum InteractionType
    {
        Grab,
        Hide,
        Carry,
        Read,
        Open,
        PressAndHold,
        Put,
        KnockDown,
        Interact
    }
}