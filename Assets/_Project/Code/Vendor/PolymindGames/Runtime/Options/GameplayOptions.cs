using UnityEngine;

namespace PolymindGames.Options
{
    [CreateAssetMenu(menuName = CreateMenuPath + "Gameplay Options", fileName = nameof(GameplayOptions))]
    public sealed partial class GameplayOptions : UserOptions<GameplayOptions>
    {
        [SerializeField, Title("HUD")]
        private Option<Color> _crosshairColor = new(Color.white);

        [SerializeField, Title("Shoot")]
        private Option<bool> _infiniteMagazineAmmo = new();

        [SerializeField]
        private Option<bool> _infiniteStorageAmmo = new();

        [SerializeField]
        private Option<bool> _manualShellEjection = new();

        [SerializeField, Title("Aim")]
        private Option<bool> _canAimWhileReloading = new(); 

        [SerializeField, Title("Reload")]
        private Option<bool> _cancelReloadOnShoot = new();

        // MRM-9/ammo: default OFF - Carlos's ranged-weapon rule (2026-09-04): an empty trigger
        // pull is a dry-fire click, never an automatic reload. Reloading is a deliberate player
        // action only. This is the shared switch every Firearm already checks in
        // Firearm.StartUse(), so it governs every current and future ranged weapon uniformly -
        // no per-weapon wiring needed.
        [SerializeField]
        private Option<bool> _autoReloadOnDry = new(false);

        [SerializeField, Title("Saving")]
        private Option<bool> _autosaveEnabled = new();
        
        [SerializeField]
        private Option<float> _autosaveInterval = new(120f);
        
        public const float MaxAutoSaveInterval = 999f;

        public Option<Color> CrosshairColor => _crosshairColor;
        public Option<bool> InfiniteStorageAmmo => _infiniteStorageAmmo;
        public Option<bool> InfiniteMagazineAmmo => _infiniteMagazineAmmo;
        public Option<bool> CanAimWhileReloading => _canAimWhileReloading;
        public Option<bool> ManualShellEjection => _manualShellEjection;
        public Option<bool> CancelReloadOnShoot => _cancelReloadOnShoot;
        public Option<bool> AutoReloadOnDry => _autoReloadOnDry;
        public Option<bool> AutosaveEnabled => _autosaveEnabled;
        public Option<float> AutosaveInterval => _autosaveInterval;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                _autosaveInterval.SetValue(Mathf.Clamp(_autosaveInterval.Value, 1f, MaxAutoSaveInterval));
        }
#endif
    }
}