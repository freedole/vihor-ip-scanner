using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Vihor
{
    /// <summary>
    /// Minimal localization. Translations are plain "key=value" UTF-8 files in src/Vihor/Lang/*.lang,
    /// embedded into the exe. To add a language, copy en.lang, translate the values and send a pull request.
    /// </summary>
    internal static class Lang
    {
        public sealed class Info
        {
            public string Code;
            public string NativeName;
            public override string ToString() => NativeName;
        }

        private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Dictionary<string, string> current = new Dictionary<string, string>(StringComparer.Ordinal);

        public static string Code { get; private set; } = "en";

        /// <summary>Languages compiled into the exe, in display order.</summary>
        public static List<Info> Available { get; } = new List<Info>();

        static Lang()
        {
            var order = new[] { "en", "sr", "hr", "bs" };
            var codes = new List<string>();
            foreach (var res in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (res.StartsWith("Vihor.Lang.", StringComparison.Ordinal) && res.EndsWith(".lang", StringComparison.Ordinal))
                    codes.Add(res.Substring(11, res.Length - 16));

            codes.Sort((a, b) =>
            {
                int ia = Array.IndexOf(order, a), ib = Array.IndexOf(order, b);
                if (ia < 0) ia = 100; if (ib < 0) ib = 100;
                return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a, b);
            });

            foreach (var c in codes)
            {
                var d = ReadResource(c);
                Available.Add(new Info { Code = c, NativeName = d.TryGetValue("_language", out var n) ? n : c });
            }

            foreach (var kv in ReadResource("en")) Fallback[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Maps installer language names (Inno Setup [Languages] names) and ISO codes to a lang code.
        /// Unknown languages fall back to English.
        /// </summary>
        public static string Normalize(string value)
        {
            var v = (value ?? "").Trim().ToLowerInvariant();
            switch (v)
            {
                case "english": return "en";
                case "serbianlatin": case "serbian": case "sr-latn": return "sr";
                case "croatian": return "hr";
                case "bosnian": return "bs";
                case "german": return "de";
                case "french": return "fr";
                case "spanish": return "es";
                case "italian": return "it";
                case "slovenian": return "sl";
                case "macedonian": return "mk";
                case "russian": return "ru";
                case "turkish": return "tr";
            }
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);
            return v.Length == 2 ? v : "en";
        }

        public static void Load(string code)
        {
            code = Normalize(code);
            bool exists = false;
            foreach (var a in Available) if (a.Code == code) { exists = true; break; }
            Code = exists ? code : "en";
            current = ReadResource(Code);
        }

        public static string T(string key)
        {
            if (current.TryGetValue(key, out var s)) return s;
            if (Fallback.TryGetValue(key, out s)) return s;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }

        private static Dictionary<string, string> ReadResource(string code)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Vihor.Lang." + code + ".lang");
            if (s == null) return d;
            using var r = new StreamReader(s, Encoding.UTF8);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
            }
            return d;
        }
    }
}
