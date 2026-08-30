namespace Burntwax
{
    public class GunNoShootState : GunBaseState
    {

        public GunNoShootState(GunStateMachine currentContext, GunStateFactory gunStateFactory) : base(currentContext, gunStateFactory) { }

        public override void EnterState()
        {
        }

        public override void UpdateState()
        {
            CheckSwitchStates();
        }

        public override void ExitState() { }

        public override void CheckSwitchStates()
        {
            if (Ctx.CurrentState.CurrentSuperstate() != Ctx.states.Reload() && Ctx.fullyEquipped
            && Ctx.CurrentState.CurrentSuperstate() != Ctx.states.Stow()
            && Ctx.CanShoot()
            && InputPrioritySorter.Instance.ShootIsPriority() && !Ctx.isShooting)
            {
                SwitchState(Factory.Shoot());
            }
        }

        public override void InitializeSubState() { }


    }
}
