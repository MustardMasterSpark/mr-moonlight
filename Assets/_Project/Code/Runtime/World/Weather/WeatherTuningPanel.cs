using MrMoonlight.DevTools;
using MrMoonlight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MrMoonlight.World.Weather
{
    /// <summary>
    /// Lighting Test Scene tuning panel, bottom-right. <b>P</b> opens and closes it; while it is open the
    /// player's controls are suspended and the mouse is free, so the buttons (and the Inspector) can be used.
    /// "Save Lighting Values" copies the live sun and ambient light into the current weather profile; "Save
    /// Fog Values" copies the live fog. Saving is editor-only (it writes the profile asset). While the panel is
    /// closed, a status line and a key reminder stay on screen: [P] this panel, [O] enemies on/off
    /// (<see cref="EnemyVisibilityToggle"/>), [I] tree fires (<see cref="TreeFireToggle"/>). Development tool, same family as the F-key overlays.
    /// Owner: MRM-86.
    /// </summary>
    [AddComponentMenu("Mr. Moonlight/World/Weather Tuning Panel")]
    public sealed class WeatherTuningPanel : MonoBehaviour
    {
        // Reference layout is 1920x1080 (the display target); scaled to the actual screen.
        private const float ReferenceHeight = 1080f;
        private const float PanelWidth = 460f;
        private const float PanelHeight = 280f;
        private const float Margin = 24f;

        [SerializeField] private WeatherSystem weather;

        private bool _open;
        private MoonlightPlayerRig _rig;
        private EnemyVisibilityToggle _enemies;
        private TreeFireToggle _fires;
        private GUIStyle _box, _label, _small, _status, _button;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                SetOpen(!_open);
            }
        }

        private void OnDisable()
        {
            if (_open) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (_rig == null) _rig = FindAnyObjectByType<MoonlightPlayerRig>();
            if (_rig != null) _rig.SetControlSuspended(open);
        }

        private void OnGUI()
        {
            if (weather == null) return;
            EnsureStyles();

            float scale = Screen.height / ReferenceHeight;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = ReferenceHeight;

            string now = "Weather: " + weather.CurrentName;
            if (weather.IsBlending)
            {
                now += "  ->  " + weather.TargetName + "  " + Mathf.RoundToInt(weather.Blend * 100f) + "%";
            }

            if (_enemies == null) _enemies = FindAnyObjectByType<EnemyVisibilityToggle>();
            if (_fires == null) _fires = FindAnyObjectByType<TreeFireToggle>();
            string keys = "[P] tuning panel" + (_enemies != null
                ? "    [O] enemies " + (_enemies.EnemiesHidden ? "OFF (" + _enemies.HiddenCount + " hidden)" : "ON")
                : string.Empty)
                + (_fires != null ? "    [I] tree fires " + (_fires.IsOn ? "ON" : "OFF") : string.Empty);

            if (!_open)
            {
                GUI.Label(new Rect(width - 800f - Margin, height - 64f - Margin, 800f, 30f), now, _status);
                GUI.Label(new Rect(width - 800f - Margin, height - 34f - Margin, 800f, 30f), keys, _status);
                GUI.matrix = previous;
                return;
            }

            var area = new Rect(width - PanelWidth - Margin, height - PanelHeight - Margin, PanelWidth, PanelHeight);
            GUI.Box(area, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(area.x + 16f, area.y + 12f, area.width - 32f, area.height - 24f));

            GUILayout.Label(now, _label);
            bool canSave = weather.CanSave(out string reason);
            GUILayout.Label(canSave ? "Save writes to: " + weather.CurrentName : reason, _small);
            GUILayout.Space(8f);

            GUI.enabled = canSave;
            if (GUILayout.Button("Save Lighting Values", _button)) weather.SaveLighting();
            if (GUILayout.Button("Save Fog Values", _button)) weather.SaveFog();
            GUI.enabled = true;

            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(weather.LastSave)) GUILayout.Label(weather.LastSave, _small);
            GUILayout.Label(keys + "  (P closes)", _small);
            GUILayout.EndArea();

            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (_box != null) return;
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
            background.Apply();

            _box = new GUIStyle(GUI.skin.box) { normal = { background = background } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = Color.white } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
            _status = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 44f };
        }
    }
}
