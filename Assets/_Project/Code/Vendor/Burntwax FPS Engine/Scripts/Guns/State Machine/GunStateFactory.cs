using System.Collections.Generic;
using Burntwax;
namespace Burntwax
{
    enum GunStates
    {
        rest,
        shoot,
        noShoot,
        reload,
        incrementalReload,
        aim,
        stow


    }

    public class GunStateFactory
    {

        GunStateMachine context;
        Dictionary<GunStates, GunBaseState> states = new Dictionary<GunStates, GunBaseState>();

        public GunStateFactory(GunStateMachine currentContext)
        {
            context = currentContext;
            states[GunStates.rest] = new GunRestState(context, this);
            states[GunStates.shoot] = new GunShootState(context, this);
            states[GunStates.noShoot] = new GunNoShootState(context, this);
            states[GunStates.reload] = new GunReloadState(context, this);
            states[GunStates.incrementalReload] = new GunIncrementalReloadState(context, this);
            states[GunStates.aim] = new GunAimState(context, this);
            states[GunStates.stow] = new GunStowState(context, this);

        }

        public GunBaseState Rest()
        {
            return states[GunStates.rest];
        }

        public GunBaseState Shoot()
        {
            return states[GunStates.shoot];
        }

        public GunBaseState NoShoot()
        {
            return states[GunStates.noShoot];
        }

        public GunBaseState Reload()
        {
            return states[GunStates.reload];
        }

        public GunBaseState IncrementalReload()
        {
            return states[GunStates.incrementalReload];
        }

        public GunBaseState Aim()
        {
            return states[GunStates.aim];
        }

        public GunBaseState Stow()
        {
            return states[GunStates.stow];
        }
    }

}