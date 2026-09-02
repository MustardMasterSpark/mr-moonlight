using System;
using System.Collections.Generic;
using System.Text;

namespace MrMoonlight.Events
{
    /// <summary>
    /// Turns an event script's text into an <see cref="EventScript"/>. Owner: MRM-11.
    ///
    /// <para><b>Why this format and not the old SLDD.</b> The SLDD
    /// (<c>Docs/SLDD (deprecated)/</c>) fixed a full parameter list per event type, forbade
    /// reordering, and forbade omitting — so every line carried a wall of <c>N/A</c> and the
    /// signal drowned in the scaffolding. Carlos's verdict was that it was unreadable and he
    /// never used it. The rule here is the opposite: <b>write only what the line actually
    /// needs</b>. Arguments are named, order-free, and every one of them optional with a
    /// documented default.</para>
    ///
    /// <para><b>The grammar, in full.</b>
    /// <code>
    /// # a comment, to end of line
    /// [sequence_name]                       a sequence header
    /// verb  "positional value"  key=value  key="value with spaces"
    /// </code>
    /// One event per line. A line never wraps. Steps written before any header belong to an
    /// implicit <c>[main]</c>, so the simplest possible script is just a list of verbs.</para>
    ///
    /// <para>The parser never throws and never stops early: it collects every problem into
    /// <see cref="EventScript.Errors"/> so one play session surfaces all of them.</para>
    /// </summary>
    public static class EventScriptParser
    {
        public static EventScript Parse(string text, string sourceName)
        {
            var script = new EventScript(sourceName);

            if (string.IsNullOrWhiteSpace(text))
            {
                script.Errors.Add($"{sourceName}: the event script is empty.");
                return script;
            }

            // Implicit main: a file with no headers at all is still a valid, runnable script.
            EventSequence current = script.GetOrCreate(EventScript.MainSequenceName, 0);
            var explicitHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                string raw = lines[i];
                string body = StripComment(raw).Trim();
                if (body.Length == 0) continue;

                if (body[0] == '[')
                {
                    current = ReadHeader(script, explicitHeaders, body, sourceName, lineNumber, current);
                    continue;
                }

                if (!Tokenize(body, out List<string> tokens, out string tokenError))
                {
                    script.Errors.Add($"{sourceName}:{lineNumber}: {tokenError}\n    {raw.Trim()}");
                    continue;
                }

                if (tokens.Count == 0) continue;

                EventStep step = BuildStep(script, tokens, sourceName, lineNumber, raw.Trim());
                if (step != null) current.Steps.Add(step);
            }

            if (script.TryGetSequence(EventScript.MainSequenceName, out EventSequence main) &&
                main.Steps.Count == 0 &&
                !explicitHeaders.Contains(EventScript.MainSequenceName))
            {
                script.Warnings.Add(
                    $"{sourceName}: no [{EventScript.MainSequenceName}] sequence and no steps before the first header — nothing will run on level start.");
            }

            return script;
        }

        private static EventSequence ReadHeader(
            EventScript script,
            HashSet<string> explicitHeaders,
            string body,
            string sourceName,
            int lineNumber,
            EventSequence current)
        {
            int close = body.IndexOf(']');
            if (close < 0)
            {
                script.Errors.Add($"{sourceName}:{lineNumber}: sequence header is missing its closing bracket.\n    {body}");
                return current;
            }

            string name = body.Substring(1, close - 1).Trim();
            if (name.Length == 0)
            {
                script.Errors.Add($"{sourceName}:{lineNumber}: sequence header has no name.");
                return current;
            }

            string trailing = body.Substring(close + 1).Trim();
            if (trailing.Length > 0)
            {
                script.Errors.Add(
                    $"{sourceName}:{lineNumber}: a sequence header must be alone on its line — found '{trailing}' after it.");
            }

            if (!explicitHeaders.Add(name))
            {
                script.Errors.Add(
                    $"{sourceName}:{lineNumber}: sequence '{name}' is declared twice. Names must be unique — the second block's " +
                    "steps would be appended to the first, which is almost never what was meant.");
            }

            return script.GetOrCreate(name, lineNumber);
        }

        private static EventStep BuildStep(EventScript script, List<string> tokens, string sourceName, int lineNumber, string sourceText)
        {
            string verb = tokens[0].ToLowerInvariant();
            if (verb.Length == 0)
            {
                script.Errors.Add($"{sourceName}:{lineNumber}: line has no verb.\n    {sourceText}");
                return null;
            }

            string value = null;
            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int t = 1; t < tokens.Count; t++)
            {
                string token = tokens[t];

                if (TrySplitNamed(token, out string key, out string keyed))
                {
                    if (args.ContainsKey(key))
                    {
                        script.Errors.Add($"{sourceName}:{lineNumber}: argument '{key}' is given twice.\n    {sourceText}");
                        continue;
                    }

                    args.Add(key, keyed);
                    continue;
                }

                // Not key=value, so it is the positional value — and there is only ever one, so a
                // stray unquoted word is caught here rather than silently swallowed.
                if (t != 1 || value != null)
                {
                    script.Errors.Add(
                        $"{sourceName}:{lineNumber}: '{token}' is neither key=value nor the first value after the verb. " +
                        "A value containing spaces must be quoted.\n    " + sourceText);
                    continue;
                }

                value = token;
            }

            return new EventStep(verb, value, args, sourceName, lineNumber, sourceText);
        }

        /// <summary>
        /// A token is named only if it starts with an identifier followed by '='. Anything else —
        /// including a quoted value that happens to contain '=' — stays positional, so
        /// <c>message "a=b"</c> does what it looks like it does.
        /// </summary>
        private static bool TrySplitNamed(string token, out string key, out string value)
        {
            key = null;
            value = null;

            int equals = token.IndexOf('=');
            if (equals <= 0) return false;
            if (!char.IsLetter(token[0]) && token[0] != '_') return false;

            for (int i = 0; i < equals; i++)
            {
                char c = token[i];
                if (!char.IsLetterOrDigit(c) && c != '_') return false;
            }

            key = token.Substring(0, equals).ToLowerInvariant();
            value = token.Substring(equals + 1);
            return true;
        }

        /// <summary>Drops everything from the first unquoted comment marker.</summary>
        private static string StripComment(string line)
        {
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == '#' && !inQuotes) return line.Substring(0, i);
            }

            return line;
        }

        /// <summary>
        /// Whitespace-separated, with double quotes protecting spaces. Quotes are stripped from
        /// the result, so <c>text="Kill 3 old timers"</c> arrives as the single token
        /// <c>text=Kill 3 old timers</c>.
        /// </summary>
        private static bool Tokenize(string line, out List<string> tokens, out string error)
        {
            tokens = new List<string>();
            error = null;

            var builder = new StringBuilder();
            bool inQuotes = false;
            bool hasContent = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasContent = true;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (hasContent)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                        hasContent = false;
                    }
                    continue;
                }

                builder.Append(c);
                hasContent = true;
            }

            if (inQuotes)
            {
                error = "unclosed double quote.";
                return false;
            }

            if (hasContent) tokens.Add(builder.ToString());
            return true;
        }
    }
}
