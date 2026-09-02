using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MrMoonlight.Events
{
    /// <summary>
    /// One parsed line of an event script: a verb, an optional positional value, and any number
    /// of named arguments. Owner: MRM-11.
    ///
    /// <para>The typed accessors below are deliberately forgiving in the same way the format is:
    /// a missing argument returns the caller's default, and a <i>malformed</i> one logs an error
    /// naming the exact file and line before falling back. Authoring mistakes must be loud and
    /// locatable — Carlos writes these lines in a text editor with no compiler behind him, so the
    /// console is the only feedback channel there is.</para>
    /// </summary>
    public sealed class EventStep
    {
        private readonly Dictionary<string, string> _args;

        public EventStep(string verb, string value, Dictionary<string, string> args, string sourceName, int sourceLine, string sourceText)
        {
            Verb = verb;
            Value = value;
            _args = args ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SourceName = sourceName;
            SourceLine = sourceLine;
            SourceText = sourceText;
        }

        /// <summary>The verb, lower-cased. Custom one-off verbs keep their leading '!'.</summary>
        public string Verb { get; }

        /// <summary>The single positional argument, if the line had one. Null otherwise.</summary>
        public string Value { get; }

        /// <summary>File the step came from, for error messages.</summary>
        public string SourceName { get; }

        /// <summary>1-based line number in that file, for error messages.</summary>
        public int SourceLine { get; }

        /// <summary>The raw line as authored. Shown in the director's runtime inspector readout.</summary>
        public string SourceText { get; }

        /// <summary>"IslandEvents.txt:14" — paste-able into a text editor's go-to-line.</summary>
        public string Where => $"{SourceName}:{SourceLine}";

        public IReadOnlyDictionary<string, string> Args => _args;

        public bool Has(string key) => _args.ContainsKey(key);

        public string GetString(string key, string fallback = null) =>
            _args.TryGetValue(key, out string raw) ? raw : fallback;

        /// <summary>The positional value, or <paramref name="key"/>'s value if the line used the named form instead.</summary>
        public string GetValueOr(string key, string fallback = null) =>
            !string.IsNullOrEmpty(Value) ? Value : GetString(key, fallback);

        public float GetFloat(string key, float fallback)
        {
            if (!_args.TryGetValue(key, out string raw)) return fallback;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) return parsed;

            LogBadArg(key, raw, "a number");
            return fallback;
        }

        public int GetInt(string key, int fallback)
        {
            if (!_args.TryGetValue(key, out string raw)) return fallback;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;

            LogBadArg(key, raw, "a whole number");
            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            if (!_args.TryGetValue(key, out string raw)) return fallback;

            switch (raw.ToLowerInvariant())
            {
                case "true": case "t": case "yes": case "y": case "1": return true;
                case "false": case "f": case "no": case "n": case "0": return false;
            }

            LogBadArg(key, raw, "true or false");
            return fallback;
        }

        public T? GetEnum<T>(string key) where T : struct, Enum
        {
            if (!_args.TryGetValue(key, out string raw)) return null;
            if (Enum.TryParse(raw, ignoreCase: true, out T parsed)) return parsed;

            LogBadArg(key, raw, $"one of: {string.Join(", ", Enum.GetNames(typeof(T)))}");
            return null;
        }

        /// <summary>#RRGGBB / #RRGGBBAA, or any name Unity's own colour parser understands.</summary>
        public Color? GetColor(string key)
        {
            if (!_args.TryGetValue(key, out string raw)) return null;
            if (ColorUtility.TryParseHtmlString(raw, out Color parsed)) return parsed;

            LogBadArg(key, raw, "a colour such as #66CCFF or \"white\"");
            return null;
        }

        public override string ToString()
        {
            return SourceText;
        }

        private void LogBadArg(string key, string raw, string expected)
        {
            Debug.LogError($"[EventDirector] {Where}: '{key}={raw}' is not {expected}. Line ignored that argument:\n    {SourceText}");
        }
    }
}
