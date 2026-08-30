using System;
using UnityEngine;
using UnityEngine.UI;

namespace Burntwax
{
    /// <summary>
    /// Health bar renderer and player marker.
    /// <para><b>Modified for Mr. Moonlight (MRM-9).</b> Mr. Moonlight's health lives in
    /// <c>MrMoonlight.Player.PlayerStats</c> (MRM-12), which owns the Stat/StatModifier stack and
    /// raises the death event that MRM-17's DeathSequence listens to. Two competing health floats
    /// would desync immediately, so this class no longer owns the value: when
    /// <see cref="ExternalHealthAuthority"/> is set, <see cref="TakeDamage"/> and
    /// <see cref="RestoreHealth"/> only raise <see cref="OnDamaged"/>/<see cref="OnHealed"/>, and
    /// <c>BurntwaxHealthBridge</c> applies them to PlayerStats and pushes the result back through
    /// <see cref="SetDisplayedHealth"/> for the bar.</para>
    /// <para>The component itself stays because Burntwax's pickups and teleporter use it as their
    /// "is this the player?" marker via GetComponentInParent.</para>
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        /// <summary>When true, PlayerStats owns the value and this only reports and displays.</summary>
        [HideInInspector] public bool ExternalHealthAuthority;

        /// <summary>Raised with the incoming damage amount when <see cref="ExternalHealthAuthority"/> is set.</summary>
        public event Action<float> OnDamaged;

        /// <summary>Raised with the heal amount when <see cref="ExternalHealthAuthority"/> is set.</summary>
        public event Action<float> OnHealed;

        private float health;
        private float lerpTimer;
        public float maxHealth = 100;
        public float chipSpeed = 2.0f;

        public Image frontHealthBar;
        public Image backHealthBar;
        void Start()
        {
            if (!ExternalHealthAuthority) health = maxHealth;
        }
        void Update()
        {
            health = Mathf.Clamp(health, 0, maxHealth);
            UpdateHealthUI();

        }

        public void UpdateHealthUI()
        {
            float fillF = frontHealthBar.fillAmount;
            float fillB = backHealthBar.fillAmount;
            float hFraction = health / maxHealth;
            if (fillB > hFraction)
            {
                frontHealthBar.fillAmount = hFraction;
                backHealthBar.color = Color.red;
                lerpTimer += Time.deltaTime;
                float percentComplete = lerpTimer / chipSpeed;
                percentComplete = percentComplete * percentComplete;
                backHealthBar.fillAmount = Mathf.Lerp(fillB, hFraction, percentComplete * Time.deltaTime);
            }
            if (fillF < hFraction)
            {
                backHealthBar.color = Color.cyan;
                backHealthBar.fillAmount = hFraction;
                lerpTimer += Time.deltaTime;
                float percentComplete = lerpTimer / chipSpeed;
                percentComplete = percentComplete * percentComplete;
                frontHealthBar.fillAmount = Mathf.Lerp(fillF, backHealthBar.fillAmount, percentComplete * Time.deltaTime);
            }
        }

        public void TakeDamage(float damage)
        {
            lerpTimer = 0f;

            if (ExternalHealthAuthority)
            {
                OnDamaged?.Invoke(damage);
                return;
            }

            health -= damage;
        }

        public void RestoreHealth(float healAmount)
        {
            lerpTimer = 0f;

            if (ExternalHealthAuthority)
            {
                OnHealed?.Invoke(healAmount);
                return;
            }

            health += healAmount;
        }

        /// <summary>Pushes the authoritative value in for display only. Used by BurntwaxHealthBridge.</summary>
        public void SetDisplayedHealth(float current, float max)
        {
            health = current;
            maxHealth = max;
        }

        public float CurrentHealth()
        {
            return health;
        }

    }


}