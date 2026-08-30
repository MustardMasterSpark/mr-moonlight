namespace Burntwax
{
    public class CameraAimState : CameraBaseState
    {

        public CameraAimState(CamStateMachine currentContext, CameraStateFactory cameraStateFactory)
        : base(currentContext, cameraStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            Ctx.crosshair.SetAimCrosshair();
            Ctx.Aim();
            InitializeSubState();
        }

        public override void UpdateState()
        {

            CheckSwitchStates();

        }

        public override void ExitState()
        {
            Ctx.crosshair.ResetAimCrosshair();
        }

        public override void CheckSwitchStates()
        {
            if (InputPrioritySorter.Instance.SprintIsPriority() || !InputManager.Instance.aimIsPressed || Ctx.gun.CurrentState == Ctx.gun.states.Reload())
            {
                SwitchState(Factory.Fps());
            }



        }


        public override void InitializeSubState() { }

    }

}