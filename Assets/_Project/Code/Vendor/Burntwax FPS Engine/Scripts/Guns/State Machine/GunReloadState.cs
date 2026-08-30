namespace Burntwax
{
    public class GunReloadState : GunBaseState
    {
        public GunReloadState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
        : base(currentContext, gunStateFactory)
        {
            IsRootState = true;

        }

        public override void EnterState()
        {
            Ctx.ActiveGunScriptable.StartReloadAudio();
            Ctx.anim.SetBool(Ctx.proceduralShootHash, false);
            Ctx.anim.SetTrigger(Ctx.reloadHash);
            Ctx.isReloading = true;
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


            if (!Ctx.ActiveGunScriptable.incrementalReload && !Ctx.isReloading && Ctx.stateMachine.currentState.CurrentSubstate() == Ctx.stateMachine.states.Sprint())
            {

                SwitchState(Factory.Stow());
            }

            else if (!Ctx.isReloading && Ctx.ActiveGunScriptable.incrementalReload)
            {


                SwitchState(Factory.IncrementalReload());
            }

            else if (!Ctx.isReloading || InputManager.Instance.mouseScroll != 0)
            {

                SwitchState(Factory.Rest());
            }

        }

        public override void InitializeSubState()
        {
        }

    }
}
