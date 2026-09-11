using PolymindGames.Options;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.Player
{
    /// <summary>
    /// Debug-only: toggles PolymindGames' own <see cref="GameplayOptions.InfiniteMagazineAmmo"/>
    /// option, which <c>Firearm.Shoot()</c> already checks before calling
    /// <c>IFirearmReloadableMagazine.TryUseAmmo</c> — while on, ammo is never subtracted from the
    /// magazine at all, so no reload is ever required. Because the magazine's own
    /// <see cref="PolymindGames.WieldableSystem.IFirearmReloadableMagazine.CurrentAmmoCount"/> is
    /// simply never touched while this is active, whatever count a weapon had when the cheat was
    /// turned on is exactly what it has when it's turned back off — nothing to snapshot or
    /// restore by hand. Toggle with <b>F9</b> (keyboard) or the inspector checkbox. Same category
    /// as F3/F4/F5 — see <c>Docs/debug-tools.md</c>. Owner: Carlos, magazine-size pass, 2026-09-10.
    ///
    /// <para>Only calls <c>SetValue</c>, never <see cref="PolymindGames.Options.UserOptions.Save"/>,
    /// so the toggle never writes to the options JSON file — it's a play-session-only cheat, not a
    /// persisted user setting, matching every other F-key toggle in this file.</para>
    /// </summary>
    public sealed class InfiniteAmmoDebugToggle : MonoBehaviour
    {
        [SerializeField] private bool infiniteAmmo = false;

        [Tooltip("Debug overlay font, so it doesn't read as generic default-Unity text. Carlos, 2026-09-02 (MRM-76).")]
        [SerializeField] private Font font;

        private bool _appliedState;
        private GUIStyle _style;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                infiniteAmmo = !infiniteAmmo;
            }

            if (infiniteAmmo != _appliedState)
            {
                GameplayOptions.Instance.InfiniteMagazineAmmo.SetValue(infiniteAmmo);
                _appliedState = infiniteAmmo;
            }
        }

        private void OnDisable()
        {
            if (_appliedState)
            {
                GameplayOptions.Instance.InfiniteMagazineAmmo.SetValue(false);
                _appliedState = false;
            }
        }

        private void OnGUI()
        {
            if (!_appliedState)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow }
            };

            var rect = new Rect(Screen.width / 2f - 200f, 160f, 400f, 30f);
            GUI.Label(rect, "INFINITE AMMO ON (F9)", _style);
        }
    }
}
