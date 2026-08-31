namespace Burntwax
{
    public class PlayerIdleState : PlayerBaseState
    {

        public PlayerIdleState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory) : base(currentContext, playerStateFactory)
        {

        }

        public override void EnterState()
        {
            Ctx.moveDirection.x = 0;
            Ctx.moveDirection.y = 0;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState() { }

        public override void CheckSwitchStates()
        {
            if (InputManager.Instance.moveIsPressed && InputManager.Instance.sprintIsPressed && Ctx.gunMachine.CurrentState.CurrentSubstate() != Ctx.gunMachine.states.Shoot() && Ctx.gunMachine.CurrentState.CurrentSubstate() != Ctx.gunMachine.states.Shoot())
            {
                SwitchState(Factory.Sprint());
            }
            else if (InputManager.Instance.moveIsPressed && !InputManager.Instance.sprintIsPressed)
            {
                SwitchState(Factory.Walk());
            }
            else if (InputManager.Instance.crouchIsPressed && Ctx.currentState != Ctx.states.Fall() && Ctx.currentState != Ctx.states.Jump())
            {
                SwitchState(Factory.Crouch());
            }
        }

        public override void InitializeSubState() { }
    }

}