namespace Burntwax {
public class GunIncrementalReloadState : GunBaseState {
   

    public GunIncrementalReloadState(GunStateMachine currentContext, GunStateFactory gunStateFactory)
    : base (currentContext, gunStateFactory) {
        IsRootState = true;

    }

    public override void EnterState(){
        Ctx.fullyEquipped = true;
        Ctx.ActiveGunScriptable.StartReloadAudio();
        Ctx.anim.SetBool(Ctx.incrementalReloadHash, true);
        Ctx.isIncrementalReloading = true;
        InitializeSubState();
    }

    public override void UpdateState(){
        CheckSwitchStates();
        
    }

    public override void ExitState(){
        Ctx.anim.SetBool(Ctx.incrementalReloadHash, false);
        Ctx.isIncrementalReloading = false; 
        
    }
    
    public override void CheckSwitchStates(){
        
        if(InputPrioritySorter.Instance.ShootIsPriority() && Ctx.ActiveGunScriptable.AmmoConfig.currentClip > 0) {
            Ctx.anim.SetTrigger(Ctx.shotWhileIncReloadHash);
            SwitchState(Factory.Rest());
        }
        else if(Ctx.ActiveGunScriptable.AmmoConfig.currentClip == Ctx.ActiveGunScriptable.AmmoConfig.clipSize) {
            SwitchState(Factory.Rest());
        }
        
    }

    public override void InitializeSubState(){
    }

}
}
