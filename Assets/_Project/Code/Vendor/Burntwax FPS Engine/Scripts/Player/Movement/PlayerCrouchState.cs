using UnityEngine;

namespace Burntwax
{
    public class PlayerCrouchState : PlayerBaseState
    {

        public PlayerCrouchState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
         : base(currentContext, playerStateFactory)
        {

        }

        public override void EnterState()
        {

            Ctx.PlayerVelocity = Ctx.CrouchVelocity;

            Ctx.LocalScale = new Vector3(Ctx.LocalScale.x, Ctx.CrouchYScale, Ctx.LocalScale.z);
            Ctx.rb.AddForce(Vector3.down * 8f, ForceMode.Impulse);

            if (Ctx.currentState == Ctx.states.Slope())
            {
                Ctx.RideHeight = Ctx.CrouchSlopeHeight;
                return;
            }
            Ctx.RideHeight = Ctx.CrouchHeight;

        }

        public override void UpdateState()
        {
            CheckSwitchStates();

        }

        public override void ExitState()
        {
            Ctx.LocalScale = new Vector3(Ctx.LocalScale.x, Ctx.StartYScale, Ctx.LocalScale.z);
            if (Ctx.currentState == Ctx.states.Slope())
            {
                Ctx.RideHeight = Ctx.SlopeHeight;
                return;
            }
            Ctx.RideHeight = Ctx.NormalHeight;
        }

        public override void CheckSwitchStates()
        {
            if (Physics.Raycast(Ctx.transform.position, Ctx.transform.TransformDirection(Vector3.up), out RaycastHit hit, 1f))
            {
                return;
            }
            else if (!InputManager.Instance.crouchIsPressed && InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {
                SwitchState(Factory.Walk());
            }
            else if (!InputManager.Instance.crouchIsPressed && !InputManager.Instance.moveIsPressed)
            {
                SwitchState(Factory.Idle());
            }
            else if (!InputManager.Instance.crouchIsPressed && InputManager.Instance.moveIsPressed && InputManager.Instance.sprintIsPressed)
            {
                SwitchState(Factory.Sprint());
            }
        }

        public override void InitializeSubState() { }




    }
}



