using UnityEngine;
using Unity.Cinemachine;

namespace Burntwax
{
    public class CamStateMachine : MonoBehaviour
    {
        public Transform cam;
        CinemachinePanTilt pov;

        [Header("State Machines")]
        [HideInInspector] public PlayerStateMachine player;


        public GunStateMachine gun;
        [HideInInspector] public PlayerCrosshair crosshair;

        CinemachineCamera activeCam;

        public CinemachineCamera fpsCam;
        public CinemachineCamera aimCam;

        public CameraBaseState currentState;
        public CameraStateFactory states;

        public CameraBaseState CurrentState { get { return currentState; } set { currentState = value; } }
        int activeCameraPriorityModifier = 2000;

        public Vector3 mouseWorldPosition;


        private void Awake()
        {
            activeCam = fpsCam;
            player = GetComponent<PlayerStateMachine>();
            crosshair = GetComponent<PlayerCrosshair>();
            pov = activeCam.GetComponent<CinemachinePanTilt>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            states = new CameraStateFactory(this);
            currentState = states.Fps();
            currentState.EnterState();

        }
        void Update()
        {

            currentState.UpdateState();
            Vector2 screenCenterPoint = new Vector2(Screen.width / 2.0f, Screen.height / 2.0f);
            Ray ray = Camera.main.ScreenPointToRay(screenCenterPoint);
            if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f))
            {
                mouseWorldPosition = raycastHit.point;
            }

        }


        public void Aim()
        {
            SetCameraPriorities(aimCam);
        }

        public void FPS()
        {
            SetCameraPriorities(fpsCam);
        }


        private void SetCameraPriorities(CinemachineCamera NewCameraMode)
        {
            activeCam.Priority = activeCam.Priority.Value - activeCameraPriorityModifier;
            NewCameraMode.Priority = NewCameraMode.Priority.Value + activeCameraPriorityModifier;
            activeCam = NewCameraMode;
            pov = activeCam.GetComponent<CinemachinePanTilt>();

        }

        /// <summary>Recentres pitch. Replaces PlayerController.ResetCameraPitch() so MRM-17's
        /// DeathSequence can still level the camera before the blackout. (MRM-9 controller swap.)</summary>
        public void ResetPitch()
        {
            if (pov != null) pov.TiltAxis.Value = 0f;
        }
    }

}
