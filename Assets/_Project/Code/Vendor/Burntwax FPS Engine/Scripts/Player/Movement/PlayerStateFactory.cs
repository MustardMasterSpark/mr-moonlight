using System.Collections.Generic;
using Burntwax;

namespace Burntwax
{
    enum PlayerStates
    {
        idle,
        walk,
        sprint,
        crouch,
        grounded,
        slope,
        jump,
        fall
    }

    public class PlayerStateFactory
    {

        PlayerStateMachine context;
        Dictionary<PlayerStates, PlayerBaseState> states = new Dictionary<PlayerStates, PlayerBaseState>();

        public PlayerStateFactory(PlayerStateMachine currentContext)
        {
            context = currentContext;
            states[PlayerStates.idle] = new PlayerIdleState(context, this);
            states[PlayerStates.walk] = new PlayerWalkState(context, this);
            states[PlayerStates.sprint] = new PlayerSprintState(context, this);
            states[PlayerStates.crouch] = new PlayerCrouchState(context, this);
            states[PlayerStates.slope] = new PlayerSlopeState(context, this);
            states[PlayerStates.jump] = new PlayerJumpState(context, this);
            states[PlayerStates.grounded] = new PlayerGroundedState(context, this);
            states[PlayerStates.fall] = new PlayerFallState(context, this);
        }

        public PlayerBaseState Idle()
        {
            return states[PlayerStates.idle];
        }

        public PlayerBaseState Walk()
        {
            return states[PlayerStates.walk];
        }

        public PlayerBaseState Sprint()
        {
            return states[PlayerStates.sprint];
        }

        public PlayerBaseState Crouch()
        {
            return states[PlayerStates.crouch];
        }

        public PlayerBaseState Slope()
        {
            return states[PlayerStates.slope];
        }

        public PlayerBaseState Jump()
        {
            return states[PlayerStates.jump];
        }

        public PlayerBaseState Grounded()
        {
            return states[PlayerStates.grounded];
        }

        public PlayerBaseState Fall()
        {
            return states[PlayerStates.fall];
        }


    }

}
