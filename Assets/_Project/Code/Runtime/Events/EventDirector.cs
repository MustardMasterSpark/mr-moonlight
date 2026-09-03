using System;
using System.Collections;
using System.Collections.Generic;
using MrMoonlight.Enemies;
using MrMoonlight.Player;
using MrMoonlight.UI;
using MrMoonlight.VFX;
using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// Runs the level: reads an authored event script and executes it, top to bottom. Owner: MRM-11.
    ///
    /// <para><b>The one rule that makes the format readable.</b> Every line fires and the director
    /// moves straight to the next one. The only line that ever blocks is <c>wait</c>. The old SLDD
    /// gave every event type its own <c>Blocking: T/F</c> parameter and then never enforced any of
    /// it, which is precisely how a screenplay-shaped document stops being readable — you cannot
    /// tell by looking where the flow pauses. Here you can: it pauses where it says wait.</para>
    ///
    /// <para><b>Sequences.</b> A script is one or more named blocks. <c>[main]</c> runs on level
    /// start; anything else is started by a <c>run</c> line or by an <see cref="EventZone"/> in the
    /// world. Several can run at once, which is what makes an optional ambush or a background beat
    /// possible without threading it through the main line.</para>
    ///
    /// <para><b>Where the data lives.</b> The script is a <see cref="TextAsset"/> in
    /// <c>Assets/_Project/Data/Events/</c>. That is a real text file Carlos edits in any editor and
    /// commits like source, and Unity bakes it into the build automatically — so MRM-11's "no
    /// runtime file reads, keep it in version control, validated at import" holds without a
    /// separate bake step. Numbers inside the script are level content, not tunables: the
    /// no-hardcoded-values rule points at code, and this file <i>is</i> the data it points to.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Mr. Moonlight/Events/Event Director")]
    public sealed class EventDirector : MonoBehaviour
    {
        [Header("Script")]
        [Tooltip("The authored event script. A .txt file under Assets/_Project/Data/Events/.")]
        [SerializeField] private TextAsset eventScript;

        [Tooltip("Start running as soon as the level loads. Untick to drive the director manually from a test harness.")]
        [SerializeField] private bool runOnStart = true;

        [Tooltip("Sequence to run on start. Almost always 'main'.")]
        [SerializeField] private string startSequence = EventScript.MainSequenceName;

        [Header("Scene references")]
        [Tooltip("Objective state and kill counting. Found on this GameObject if left empty.")]
        [SerializeField] private ObjectiveTracker objectives;

        [Tooltip("The centre-bottom subtitle channel objectives and messages are announced through.")]
        [SerializeField] private SystemMessageUI systemMessages;

        [Tooltip("The end-of-level panel, shown for both the win and the loss.")]
        [SerializeField] private GameOverPanel endPanel;

        [Tooltip("The chase target handed to spawned enemies. Must be the object Blaze can see — the tagged collider, which on this player is the Body child, not the root. Found by tag if left empty.")]
        [SerializeField] private GameObject player;

        private readonly Dictionary<string, RunningSequence> _running =
            new Dictionary<string, RunningSequence>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<EnemyHealth>> _groups =
            new Dictionary<string, List<EnemyHealth>>(StringComparer.OrdinalIgnoreCase);

        private EventScript _script;

        /// <summary>
        /// The director in the loaded scene, so an <see cref="EventZone"/> can reach it without
        /// Carlos wiring a reference into every volume he places.
        ///
        /// <para>Not a singleton in the sense <c>Docs/csharp-conventions.md</c> forbids: nothing
        /// creates one on demand, it does not survive a scene load, and a second one in the same
        /// scene is a logged error rather than a silent overwrite. It is a lookup cache for an
        /// object that is authored into the scene by hand.</para>
        /// </summary>
        public static EventDirector Active { get; private set; }

        public ObjectiveTracker Objectives => objectives;

        public SystemMessageUI Messages => systemMessages;

        public GameOverPanel EndPanel => endPanel;

        public GameObject Player => player;

        /// <summary>Sequences executing right now. Read by the runtime inspector readout.</summary>
        public IReadOnlyCollection<RunningSequence> Running => _running.Values;

        /// <summary>True once <c>win</c> or <c>lose</c> has run. Stops a second ending from firing on top of the first.</summary>
        public bool LevelEnded { get; private set; }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError($"[EventDirector] A second Event Director ('{name}') is in the scene alongside '{Active.name}'. Only one may run a level; this one will not register.", this);
            }
            else
            {
                Active = this;
            }

            // Latched signals from a previous run of this scene would otherwise resolve waits
            // instantly on a restart. Subscribers are left alone on purpose — see ResetLatched.
            EventSignals.ResetLatched();

            if (objectives == null) objectives = GetComponent<ObjectiveTracker>();
            if (player == null) player = GameObject.FindGameObjectWithTag("Player");

            LoadScript();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            if (runOnStart) RunSequence(startSequence);
        }

        /// <summary>Starts a sequence alongside whatever else is running. Does nothing if it is already running.</summary>
        public void RunSequence(string sequenceName)
        {
            if (_script == null)
            {
                Debug.LogError("[EventDirector] No event script loaded — nothing to run.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(sequenceName)) return;
            sequenceName = sequenceName.Trim();

            if (!_script.TryGetSequence(sequenceName, out EventSequence sequence))
            {
                Debug.LogError($"[EventDirector] No sequence named '{sequenceName}' in {_script.SourceName}. Sequences: {DescribeSequenceNames()}", this);
                return;
            }

            if (_running.ContainsKey(sequenceName))
            {
                Debug.LogWarning($"[EventDirector] Sequence '{sequenceName}' is already running; the request to start it again was ignored.", this);
                return;
            }

            var state = new RunningSequence(sequence);
            _running.Add(sequenceName, state);
            state.Coroutine = StartCoroutine(RunRoutine(state));
        }

        /// <summary>Asks a running sequence to stop after its current step.</summary>
        public void StopSequence(string sequenceName)
        {
            if (_running.TryGetValue(sequenceName ?? string.Empty, out RunningSequence state)) state.Stop();
        }

        public bool IsRunning(string sequenceName) => _running.ContainsKey(sequenceName ?? string.Empty);

        /// <summary>Ends the level. The first call wins; later ones are ignored so a win cannot be overwritten by a death mid-fade.</summary>
        public void EndLevel(bool won, string message)
        {
            if (LevelEnded)
            {
                Debug.LogWarning($"[EventDirector] The level already ended; ignoring a second ending ('{message}').", this);
                return;
            }

            LevelEnded = true;

            foreach (RunningSequence state in _running.Values) state.Stop();

            if (won) ClearHealthTint();

            if (endPanel == null)
            {
                Debug.LogError($"[EventDirector] Level ended ({(won ? "win" : "loss")}: \"{message}\") but no end panel is assigned — the player is left standing there.", this);
                return;
            }

            endPanel.ShowEnding(message, won);
        }

        /// <summary>Registers enemies under a group name so <c>wait group=</c> can watch them.</summary>
        public void AddToGroup(string groupName, IEnumerable<EnemyHealth> enemies)
        {
            if (string.IsNullOrWhiteSpace(groupName) || enemies == null) return;

            if (!_groups.TryGetValue(groupName, out List<EnemyHealth> group))
            {
                group = new List<EnemyHealth>();
                _groups.Add(groupName, group);
            }

            group.AddRange(enemies);
        }

        public bool GroupExists(string groupName) => _groups.ContainsKey(groupName ?? string.Empty);

        /// <summary>True when every member of the group is dead or gone. An unknown group is not "clear" — waiting on one is a mistake worth surfacing.</summary>
        public bool IsGroupClear(string groupName)
        {
            if (!_groups.TryGetValue(groupName ?? string.Empty, out List<EnemyHealth> group)) return false;

            for (int i = 0; i < group.Count; i++)
            {
                EnemyHealth member = group[i];
                if (member != null && !member.IsDead) return false;
            }

            return true;
        }

        private IEnumerator RunRoutine(RunningSequence state)
        {
            List<EventStep> steps = state.Sequence.Steps;
            var context = new EventContext(this, state);

            state.Index = 0;
            while (state.Index < steps.Count)
            {
                EventStep step = steps[state.Index];

                if (EventVerbRegistry.TryGet(step.Verb, out EventVerb verb))
                {
                    IEnumerator routine = verb.Run(step, context);
                    if (routine != null) yield return routine;
                }
                else
                {
                    // Already reported at load; this is the belt to that braces, for a sequence
                    // started after a hot script edit.
                    Debug.LogError($"[EventDirector] {step.Where}: unknown verb '{step.Verb}'.\n    {step.SourceText}", this);
                }

                if (state.StopRequested) break;

                if (state.TryTakeJump(out int jumpTarget))
                {
                    state.Index = Mathf.Clamp(jumpTarget, 0, steps.Count);
                    continue;
                }

                state.Index++;
            }

            _running.Remove(state.Name);
        }

        private void LoadScript()
        {
            if (eventScript == null)
            {
                Debug.LogError($"[EventDirector] '{name}' has no event script assigned. Nothing will happen this level.", this);
                return;
            }

            _script = EventScriptParser.Parse(eventScript.text, eventScript.name);
            ValidateVerbs(_script);

            foreach (string warning in _script.Warnings) Debug.LogWarning($"[EventDirector] {warning}", this);
            foreach (string error in _script.Errors) Debug.LogError($"[EventDirector] {error}", this);
        }

        /// <summary>
        /// Second pass, after parsing: every verb exists, and every verb gets a look at its own
        /// arguments. Shared with the editor validator so a script can be checked without pressing
        /// play. Owner: MRM-11.
        /// </summary>
        public static void ValidateVerbs(EventScript script)
        {
            foreach (EventSequence sequence in script.Sequences)
            {
                foreach (EventStep step in sequence.Steps)
                {
                    if (!EventVerbRegistry.TryGet(step.Verb, out EventVerb verb))
                    {
                        script.Errors.Add(
                            $"{step.Where}: unknown verb '{step.Verb}'. Known verbs: {EventVerbRegistry.DescribeVerbNames()}\n    {step.SourceText}");
                        continue;
                    }

                    verb.Validate(step, script, script.Errors);
                }
            }
        }

        private string DescribeSequenceNames()
        {
            var names = new List<string>();
            foreach (EventSequence sequence in _script.Sequences) names.Add(sequence.Name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", names);
        }

        /// <summary>
        /// Takes the health damage tint off the victory screen. Owner: MRM-11.
        ///
        /// <para>A player who wins on their last sliver of health finishes the level looking at
        /// "Good boy" through a full-screen blood red, which reads as a failure state. The source
        /// re-writes its contribution every frame, so clearing the registry entry alone would not
        /// stick — the component has to go quiet. Only on a win: a scripted <c>lose</c>, and the
        /// death sequence's own red ramp, both want the screen exactly as red as it is.</para>
        /// </summary>
        private void ClearHealthTint()
        {
            HealthRedTintSource source = FindFirstObjectByType<HealthRedTintSource>(FindObjectsInactive.Include);
            if (source != null) source.enabled = false;

            ScreenTint.ClearRed(HealthRedTintSource.SourceName);
        }

        /// <summary>Shuts the player down at the end of a level, so the camera stops moving behind the end panel.</summary>
        internal void DisablePlayerControl()
        {
            // Searched from the root, not from `player`: the Player tag is on the Body collider
            // (physics reads the collider's own tag, never a parent's), while the bridge lives on
            // a sibling branch — MrMoonlight Systems. Starting at `player` finds nothing.
            MoonlightPlayerRig bridge = player != null
                ? player.transform.root.GetComponentInChildren<MoonlightPlayerRig>(true)
                : null;

            if (bridge == null) bridge = FindFirstObjectByType<MoonlightPlayerRig>(FindObjectsInactive.Include);

            if (bridge == null)
            {
                Debug.LogWarning("[EventDirector] Level ended but no MoonlightPlayerRig was found — the player keeps looking around behind the end panel.", this);
                return;
            }

            bridge.DisableControl();
        }
    }
}
