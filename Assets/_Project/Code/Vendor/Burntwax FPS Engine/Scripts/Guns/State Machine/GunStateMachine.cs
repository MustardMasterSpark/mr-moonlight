using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Unity.Cinemachine;

namespace Burntwax
{
    [DisallowMultipleComponent]
    public class GunStateMachine : MonoBehaviour
    {
        [Header("State Machines")]
        public GameObject player;
        [HideInInspector] public PlayerStateMachine stateMachine;
        [HideInInspector] public PlayerCrosshair crosshair;

        [Space]
        [Header("Cinemachine Required")]
        public CinemachineImpulseSource camShake;

        [Header("Animation Rigging Required")]
        public Rig handIK;

        public int currentID;

        [Header("Animation")]
        public Animator anim;
        [HideInInspector] public int stowHash, aimHash, reloadHash, proceduralShootHash, incrementalReloadHash, shotWhileIncReloadHash;

        public List<GunScriptableObject> CurrentGuns;
        public List<GameObject> OrderedGuns;

        [Space]
        [Header("Current Gun")]
        public GunScriptableObject ActiveGunScriptable;
        public GameObject CurrentGunModel;
        public ProceduralRecoil recoil;

        [Space]
        private GunBaseState currentState;
        public GunStateFactory states;

        public GunBaseState CurrentState { get { return currentState; } set { currentState = value; } }

        public bool isReloading = false;
        public bool isIncrementalReloading = false;
        public bool isShooting = false;

        public bool ableToShootInMiddleOfAnimation = false;
        public bool fullyEquipped;

        private void Awake()
        {
            states = new GunStateFactory(this);
            currentState = states.Rest();
            currentState.EnterState();

            aimHash = Animator.StringToHash("aim");
            stowHash = Animator.StringToHash("stow");
            reloadHash = Animator.StringToHash("reload");
            proceduralShootHash = Animator.StringToHash("proceduralShoot");
            incrementalReloadHash = Animator.StringToHash("incrementalReload");
            shotWhileIncReloadHash = Animator.StringToHash("shotWhileIncReload");

            stateMachine = player.GetComponent<PlayerStateMachine>();
            crosshair = player.GetComponentInChildren<PlayerCrosshair>();
        }

        private void Update()
        {
            if (ActiveGunScriptable == null)
                return;

            if (Time.timeScale == 0f)
            {
                InputManager.Instance.ConsumeWeaponSwitchInput();
                return;
            }

            if (isShooting)
            {
                InputManager.Instance.ConsumeWeaponSwitchInput();
                EmptyClip();
                return;
            }

            if (InputManager.Instance.HasWeaponSelectInput || Mathf.Abs(InputManager.Instance.mouseScroll) > 0.01f)
            {
                WeaponSwitch();
            }

            EmptyClip();
        }

        private void FixedUpdate()
        {
            if (ActiveGunScriptable)
            {
                currentState.UpdateStates();
            }
        }

        private void Despawn(GunScriptableObject activeGun)
        {
            if (CurrentGunModel != null)
                CurrentGunModel.SetActive(false);

            if (activeGun != null)
                activeGun.Despawn(this);
        }

        private void Spawn(GunScriptableObject activeGun)
        {
            if (!HasWeaponModelForID(currentID))
                return;

            CurrentGunModel = OrderedGuns[currentID];
            CurrentGunModel.SetActive(true);
            activeGun.Spawn(player, CurrentGunModel, this);
        }

        private void WeaponSwitch()
        {
            int requestedID = currentID;
            bool hasSwitchInput = false;

            if (InputManager.Instance.HasWeaponSelectInput)
            {
                requestedID = InputManager.Instance.weaponSelectID;
                hasSwitchInput = true;
            }
            else if (Mathf.Abs(InputManager.Instance.mouseScroll) > 0.01f)
            {
                requestedID = GetScrolledWeaponID(InputManager.Instance.mouseScroll);
                hasSwitchInput = true;
            }

            InputManager.Instance.ConsumeWeaponSwitchInput();

            if (!hasSwitchInput)
                return;

            if (ActiveGunScriptable != null && requestedID == ActiveGunScriptable.ID)
                return;

            GunScriptableObject gun = CurrentGuns.Find(gun => gun != null && gun.ID == requestedID);
            if (gun == null)
            {
                // This is not a real error. It can happen if the player presses a hotkey for a gun they have not picked up.
                Debug.LogWarning($"Cannot switch to weapon ID {requestedID} because it is not in CurrentGuns.");
                return;
            }

            if (!HasWeaponModelForID(requestedID))
                return;

            Despawn(ActiveGunScriptable);

            ActiveGunScriptable = gun;
            currentID = requestedID;
            SetupGun();
        }

        private int GetScrolledWeaponID(float scrollValue)
        {
            if (CurrentGuns == null || CurrentGuns.Count == 0 || ActiveGunScriptable == null)
                return currentID;

            int direction = scrollValue > 0 ? 1 : -1;
            int currentIndex = CurrentGuns.FindIndex(gun => gun != null && gun.ID == ActiveGunScriptable.ID);

            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = currentIndex;
            int attempts = 0;

            do
            {
                nextIndex = (nextIndex + direction + CurrentGuns.Count) % CurrentGuns.Count;
                attempts++;
            }
            while (CurrentGuns[nextIndex] == null && attempts <= CurrentGuns.Count);

            if (CurrentGuns[nextIndex] == null)
                return currentID;

            return CurrentGuns[nextIndex].ID;
        }

        private bool HasWeaponModelForID(int weaponID)
        {
            if (OrderedGuns == null)
            {
                Debug.LogError("OrderedGuns is null.");
                return false;
            }

            if (weaponID < 0 || weaponID >= OrderedGuns.Count)
            {
                Debug.LogError($"Weapon ID {weaponID} does not have a matching model in OrderedGuns.");
                return false;
            }

            if (OrderedGuns[weaponID] == null)
            {
                Debug.LogError($"OrderedGuns[{weaponID}] is null.");
                return false;
            }

            return true;
        }

        public void Pickup(GunScriptableObject gun)
        {
            if (!CurrentGuns.Contains(gun))
            {
                CurrentGuns.Add(gun);

                if (ActiveGunScriptable != null)
                {
                    Despawn(ActiveGunScriptable);
                }

                ActiveGunScriptable = gun;
                currentID = gun.ID;

                SetupGun();
                ActiveGunScriptable.AmmoConfig.OnEnable();
            }
        }

        private void SetupGun()
        {
            fullyEquipped = false;
            isReloading = false;

            Spawn(ActiveGunScriptable);

            crosshair.SetGunCrosshair(ActiveGunScriptable);
            currentState = states.Rest();

            if (handIK != null)
                handIK.weight = 1;

            anim.Play("equip_" + ActiveGunScriptable.Type);

            camShake = CurrentGunModel.GetComponent<CinemachineImpulseSource>();

            anim.SetBool(aimHash, false);
            anim.SetBool(stowHash, false);
            anim.SetBool(reloadHash, false);
            anim.SetBool(proceduralShootHash, false);
            anim.SetBool(incrementalReloadHash, false);
        }

        private void EmptyClip()
        {
            if (ClipEmpty()
                && InputManager.Instance.shootIsPressed
                && CurrentState.CurrentSuperstate() != states.Reload()
                && CurrentState.CurrentSuperstate() != states.Stow())
            {
                ActiveGunScriptable.AudioConfig.PlayEmptyClip(stateMachine.GetComponent<AudioSource>());
                InputManager.Instance.shootIsPressed = false;
            }
        }

        public bool CanShoot()
        {
            return ActiveGunScriptable.AmmoConfig.currentClip > 0 && !isShooting;
        }

        private bool ClipEmpty()
        {
            return ActiveGunScriptable.AmmoConfig.currentClip == 0 && ActiveGunScriptable.AmmoConfig.currentAmmo == 0;
        }

        // Animation event
        public void EndReload()
        {
            if (!ActiveGunScriptable.incrementalReload)
            {
                ActiveGunScriptable.EndReload();
            }

            isReloading = false;
        }

        // Animation event
        public void EndEquip()
        {
            fullyEquipped = true;
        }

        // Animation event
        public void EndIncrementalReload()
        {
            ActiveGunScriptable.EndIncrementalReload();
        }

        // Animation event
        public void EndShoot()
        {
            ableToShootInMiddleOfAnimation = false;
            isShooting = false;
        }

        public void AbleToShootInMiddleOfAnimation()
        {
            ableToShootInMiddleOfAnimation = true;
        }

        public bool ManualReload()
        {
            return InputManager.Instance.reloadIsPressed && ActiveGunScriptable.CanReload();
        }

        public bool IncrementalReload()
        {
            return ActiveGunScriptable.CanReload();
        }

        public bool AutoReload()
        {
            return ActiveGunScriptable.CanReload() && ActiveGunScriptable.AmmoConfig.currentClip == 0;
        }

        public void SaveData(ref GunSaveData data)
        {
            if (ActiveGunScriptable == null)
            {
                data.GunModel = null;
                data.ActiveGunScriptable = null;

                if (CurrentGuns.Count > 0)
                    data.CurrentGuns.Clear();

                data.currentState = null;
                return;
            }

            data.GunModel = CurrentGunModel;
            data.ActiveGunScriptable = ActiveGunScriptable;
            data.ActiveGunScriptable.ID = currentID;
            data.CurrentGuns = CurrentGuns;
            data.currentState = currentState;

            data.clipAmmo = new List<int>();
            data.reserveAmmo = new List<int>();

            foreach (GunScriptableObject gun in CurrentGuns)
            {
                data.clipAmmo.Add(gun.AmmoConfig.currentClip);
                data.reserveAmmo.Add(gun.AmmoConfig.currentAmmo);
            }
        }

        public void LoadData(GunSaveData data)
        {
            if (data.ActiveGunScriptable == null)
            {
                if (CurrentGunModel)
                    CurrentGunModel.gameObject.SetActive(false);

                ActiveGunScriptable = null;
                CurrentGunModel = null;
                anim.Play("empty_rest");
                CurrentGuns.Clear();
                return;
            }

            if (ActiveGunScriptable != null)
                Despawn(ActiveGunScriptable);

            currentID = data.ActiveGunScriptable.ID;
            CurrentGuns = data.CurrentGuns;
            ActiveGunScriptable = data.ActiveGunScriptable;
            CurrentGunModel = data.GunModel;

            Spawn(ActiveGunScriptable);
            SetupGun();

            currentState = data.currentState;
            currentState = states.Rest();
            currentState.EnterState();
            fullyEquipped = true;

            foreach (GunScriptableObject gun in CurrentGuns)
            {
                int index = CurrentGuns.IndexOf(gun);
                gun.AmmoConfig.currentClip = data.clipAmmo[index];
                gun.AmmoConfig.currentAmmo = data.reserveAmmo[index];
            }
        }
    }

    [System.Serializable]
    public struct GunSaveData
    {
        public List<GunScriptableObject> CurrentGuns;
        public GameObject GunModel;
        public GunScriptableObject ActiveGunScriptable;
        public List<int> clipAmmo;
        public List<int> reserveAmmo;
        public GunBaseState currentState;
    }
}
