using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.World
{
    /// <summary>
    /// Runtime on/off switch for a rain particle system (MRM-18 main menu ask, Cartoon Rain &amp;
    /// Blood Rain asset's "Light Rain" prefab). Turning off uses StopEmitting rather than an
    /// instant Clear, so drops already in the air finish falling and colliding naturally instead
    /// of popping out of existence; turning on resumes emission on the same system, sub-emitters
    /// (splash/ripple/bubble) included since they follow their parent automatically.
    /// </summary>
    public sealed class RainToggle : MonoBehaviour
    {
        [Tooltip("Root particle system for the rain (the 'Light Rain' system, not the prefab's outer wrapper).")]
        [SerializeField] private ParticleSystem rain;

        [SerializeField] private bool startEnabled = true;

        [Header("Debug")]
        [Tooltip("Press this key at runtime to toggle rain, for quick testing without wiring a UI control yet.")]
        [SerializeField] private Key debugToggleKey = Key.R;

        public bool IsRaining { get; private set; }

        private void Start()
        {
            SetRaining(startEnabled);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[debugToggleKey].wasPressedThisFrame)
            {
                ToggleRain();
            }
        }

        public void ToggleRain()
        {
            SetRaining(!IsRaining);
        }

        public void SetRaining(bool raining)
        {
            IsRaining = raining;
            if (rain == null) return;

            if (raining)
            {
                rain.Play(true);
            }
            else
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
