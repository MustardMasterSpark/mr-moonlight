namespace Burntwax
{
    public class PlayerWalkState : PlayerBaseState
    {

        public PlayerWalkState(PlayerStateMachine currentContext, PlayerStateFactory playerStateFactory)
         : base(currentContext, playerStateFactory)
        {

        }

        public override void EnterState()
        {
            Ctx.PlayerVelocity = Ctx.WalkVelocity;
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState() { }

        public override void CheckSwitchStates()
        {

            if (InputManager.Instance.moveIsPressed && InputPrioritySorter.Instance.SprintIsPriority() && !Ctx.gunMachine.isShooting && !Ctx.gunMachine.isReloading && !Ctx.gunMachine.isIncrementalReloading)
            {
                SwitchState(Factory.Sprint());
            }
            else if (!InputManager.Instance.moveIsPressed)
            {
                SwitchState(Factory.Idle());
            }
            else if (InputManager.Instance.crouchIsPressed && Ctx.currentState != Ctx.states.Fall() && Ctx.currentState != Ctx.states.Jump())
            {
                SwitchState(Factory.Crouch());
            }


        }

        public override void InitializeSubState() { }
    }
}