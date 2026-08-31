namespace Burntwax
{
    public class GunStowState : GunBaseState
    {

        public GunStowState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
        : base(currentContext, gunStateFactory)
        {
            IsRootState = true;
        }

        public override void EnterState()
        {
            Ctx.anim.SetBool(Ctx.stowHash, true);
            Ctx.fullyEquipped = true;
            InitializeSubState();
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState()
        {
            Ctx.anim.SetBool(Ctx.stowHash, false);
        }

        public override void CheckSwitchStates()
        {
            if (Ctx.stateMachine.currentState.CurrentSubstate() != Ctx.stateMachine.states.Sprint() && !InputPrioritySorter.Instance.AimIsPriority() || InputPrioritySorter.Instance.ShootIsPriority())
            {

                SwitchState(Factory.Rest());
            }
            else if (Ctx.AutoReload() || Ctx.ManualReload())
            {
                SwitchState(Factory.Reload());
            }

            else if (!Ctx.isReloading && InputPrioritySorter.Instance.AimIsPriority())
            {
                SwitchState(Factory.Aim());
            }


        }


        public override void InitializeSubState()
        {
            SetSubState(Factory.NoShoot());

        }

    }
}
