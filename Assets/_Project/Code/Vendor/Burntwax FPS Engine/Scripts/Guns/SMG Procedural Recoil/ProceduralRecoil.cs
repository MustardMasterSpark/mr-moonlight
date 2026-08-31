using UnityEngine;

namespace Burntwax
{

    public class ProceduralRecoil : MonoBehaviour
    {

        [Header("Resting Position")]


        [SerializeField] Vector3 restingPosition;

        [Header("Speed Settings")]

        public float positionalRecoilSpeed = 10f;
        public float rotationalRecoilSpeed = 10f;
        [Space(10)]

        public float positionalReturnSpeed = 20f;
        public float rotationalReturnSpeed = 40f;

        [Space()]

        [Header("Recoil Settings")]
        public Transform recoilPosition;
        public Vector3 recoilRotation = new Vector3(0f, 15f, 15f);
        public Vector3 recoilKickBack = new Vector3(0f, 0f, -0.03f);
        [Space()]
        public Vector3 recoilRotationAim = new Vector3(0f, 5f, 6f);
        public Vector3 recoilKickBackAim = new Vector3(0f, 0f, -0.01f);
        Vector3 rotationalRecoil;
        Vector3 positionalRecoil;




        [Header("State Machine References")]
        public GunStateMachine gunMachine;

        private Vector3 rot;



        void FixedUpdate()
        {
            // Debug.Log(transform.localPosition.x + " " + transform.localPosition.y + " " + transform.localPosition.z);
            if (gunMachine.ActiveGunScriptable)
            {
                if (gunMachine.ActiveGunScriptable.proceduralRecoil)
                {
                    rotationalRecoil = Vector3.Lerp(rotationalRecoil, gunMachine.ActiveGunScriptable.restingRecoilPos, rotationalReturnSpeed * Time.fixedDeltaTime);
                    positionalRecoil = Vector3.Lerp(positionalRecoil, gunMachine.ActiveGunScriptable.restingRecoilPos, positionalReturnSpeed * Time.fixedDeltaTime);

                    recoilPosition.localPosition = Vector3.Slerp(recoilPosition.localPosition, positionalRecoil, positionalRecoilSpeed * Time.fixedDeltaTime);
                    rot = Vector3.Slerp(rot, rotationalRecoil, rotationalRecoilSpeed * Time.fixedDeltaTime);
                    transform.localRotation = Quaternion.Euler(rot);

                }

            }

        }

        public void Recoil()
        {
            if (gunMachine.CurrentState == gunMachine.states.Aim())
            {
                rotationalRecoil += new Vector3(-recoilRotationAim.x,
                Random.Range(-recoilRotationAim.y, recoilRotationAim.y),
                Random.Range(0, -recoilRotationAim.z));

                positionalRecoil += new Vector3(Random.Range(-recoilKickBackAim.x, recoilKickBackAim.x),
                Random.Range(-recoilKickBackAim.y, recoilKickBackAim.y),
                recoilKickBackAim.z);
            }
            else
            {
                rotationalRecoil += new Vector3(-recoilRotation.x,
                Random.Range(-recoilRotation.y, recoilRotation.y),
                Random.Range(0, -recoilRotation.z));

                positionalRecoil += new Vector3(Random.Range(-recoilKickBack.x, recoilKickBack.x),
                Random.Range(-recoilKickBack.y, recoilKickBack.y),
                recoilKickBack.z);
            }
        }

    }
}