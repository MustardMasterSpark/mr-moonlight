using UnityEngine;

namespace Burntwax
{
    public class PlayerJumpState : PlayerBaseState
    {


        public PlayerJumpState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
        }

        public override void EnterState()
        {
            Ctx.MoveMultiplier = Ctx.AirMultiplier;
            Ctx.rb.linearDamping = Ctx.AirDrag;
            Jump();
            InitializeSubState();
            Ctx.currentAngle = 0;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState()
        {

        }

        public override void CheckSwitchStates()
        {
            if (Ctx.playerIsSloped && !Ctx.playerIsGrounded)
            {
                SwitchState(Factory.Slope());
                return;
            }
            else if (Ctx.playerIsGrounded)
            {
                SwitchState(Factory.Grounded());
            }
            else if (Ctx.rb.linearVelocity.y < 0)
            {
                SwitchState(Factory.Fall());
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

        private void Jump()
        {
            Ctx.rb.AddForce(Ctx.player.up * Ctx.JumpForce, ForceMode.Impulse);
            Ctx.YFlat();
        }


    }

}