namespace Burntwax
{
    public class PlayerGroundedState : PlayerBaseState
    {

        public PlayerGroundedState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
        : base(currentContext, playerStateFactory)
        {
            IsRootState = true;
        }

        public override void EnterState()
        {
            Ctx.MoveMultiplier = Ctx.GroundMultiplier;
            Ctx.rb.linearDamping = Ctx.GroundDrag;
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
            if (InputManager.Instance.jumpIsPressed)
            {
                SwitchState(Factory.Jump());
            }
            else if (!Ctx.playerIsGrounded && !InputManager.Instance.crouchIsPressed)
            {
                Ctx.CoyoteTime = Ctx.CoyoteTimer;
                SwitchState(Factory.Fall());
            }
            else if (Ctx.playerIsSloped)
            {
                SwitchState(Factory.Slope());
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