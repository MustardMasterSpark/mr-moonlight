using UnityEngine;

namespace Burntwax
{
    public class PlayerFallState : PlayerBaseState
    {


        public PlayerFallState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory)
        {
            IsRootState = true;

        }


        public override void EnterState()
        {
            Ctx.MoveMultiplier = Ctx.AirMultiplier;
            Ctx.rb.linearDamping = Ctx.AirDrag;

            InitializeSubState();
            Ctx.currentAngle = 0;
        }

        public override void UpdateState()
        {

            Ctx.CoyoteTime--;
            if (Ctx.CoyoteTime <= 0)
            {
                Ctx.CoyoteTime = 0;
            }
            CheckSwitchStates();

        }

        public override void ExitState()
        {
            Ctx.CoyoteTime = 0;
        }

        public override void CheckSwitchStates()
        {
            if (Ctx.playerIsSloped)
            {
                // Debug.Log("FALL TO SLOPE");
                SwitchState(Factory.Slope());
                return;
            }
            else if (Ctx.playerIsGrounded && !Ctx.playerIsSloped)
            {
                // Debug.Log("FALL TO GROUNDED");
                SwitchState(Factory.Grounded());
            }
            else if (Ctx.CoyoteTime > 0 && InputManager.Instance.jumpIsPressed)
            {
                Ctx.CoyoteTime = Ctx.CoyoteTimer;
                Ctx.YFlat();
                SwitchState(Factory.Jump());
            }

        }

        public override void InitializeSubState()
        {
            if (!InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {
                SetSubState(Factory.Idle());
            }
            else if (InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {
                SetSubState(Factory.Walk());
            }
            else
            {
                SetSubState(Factory.Sprint());
            }
        }




    }

}