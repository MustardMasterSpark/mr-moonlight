using System;
using System.Collections.Generic;

namespace MrMoonlight.Events
{
    /// <summary>An ordered run of steps, addressable by name from a <c>run</c> verb or a scene trigger. Owner: MRM-11.</summary>
    public sealed class EventSequence
    {
        public EventSequence(string name, int headerLine)
        {
            Name = name;
            HeaderLine = headerLine;
        }

        public string Name { get; }

        /// <summary>Line the <c>[name]</c> header sat on. 0 for the implicit <c>main</c> sequence.</summary>
        public int HeaderLine { get; }

        public List<EventStep> Steps { get; } = new List<EventStep>();
    }

    /// <summary>
    /// A whole parsed event script file: its named sequences, plus anything the parser could not
    /// make sense of. Owner: MRM-11.
    ///
    /// <para>Parse errors are collected rather than thrown. A single bad line should not take the
    /// level down with it — the director reports every problem at once, at load, so Carlos fixes
    /// them in one pass instead of one play session per typo.</para>
    /// </summary>
    public sealed class EventScript
    {
        /// <summary>The sequence that runs on level start unless the director is told otherwise.</summary>
        public const string MainSequenceName = "main";

        private readonly Dictionary<string, EventSequence> _sequences =
            new Dictionary<string, EventSequence>(StringComparer.OrdinalIgnoreCase);

        public EventScript(string sourceName)
        {
            SourceName = sourceName;
        }

        public string SourceName { get; }

        public List<string> Errors { get; } = new List<string>();

        public List<string> Warnings { get; } = new List<string>();

        public IReadOnlyCollection<EventSequence> Sequences => _sequences.Values;

        public bool TryGetSequence(string name, out EventSequence sequence) =>
            _sequences.TryGetValue(name, out sequence);

        public EventSequence GetOrCreate(string name, int headerLine)
        {
            if (_sequences.TryGetValue(name, out EventSequence existing)) return existing;

            var created = new EventSequence(name, headerLine);
            _sequences.Add(name, created);
            return created;
        }

        public bool Contains(string name) => _sequences.ContainsKey(name);
    }
}
