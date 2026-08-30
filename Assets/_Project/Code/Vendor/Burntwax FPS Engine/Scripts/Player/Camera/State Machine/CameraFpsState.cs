namespace Burntwax
{
    public class CameraFpsState : CameraBaseState
    {

        public CameraFpsState(CamStateMachine currentContext, CameraStateFactory cameraStateFactory)
        : base(currentContext, cameraStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            Ctx.FPS();
            InitializeSubState();
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
            if (InputPrioritySorter.Instance.AimIsPriority() && Ctx.gun.CurrentState != Ctx.gun.states.Reload() && Ctx.gun.CurrentState.CurrentSubstate() != Ctx.gun.states.Shoot())
            {
                SwitchState(Factory.Aim());
            }

        }

        public override void InitializeSubState() { }

    }
}