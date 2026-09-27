using System;
using System.Collections.Generic;
using System.Globalization;

namespace RedlineDiagnostics.Localization
{
    public enum Language { EN = 0, JA = 1, ZH = 2 }

    /// <summary>
    /// Runtime localization. Strings live in <see cref="Strings"/>; the current language can be switched at
    /// any time and every control that implements <see cref="ILocalizable"/> is refreshed by the main form.
    /// </summary>
    public static class Loc
    {
        private static Language _current = Language.EN;
        private static readonly Dictionary<string, string[]> _table = Strings.Build();

        public static event Action LanguageChanged;

        public static Language Current
        {
            get { return _current; }
            set
            {
                if (_current == value) return;
                _current = value;
                App.Theme.ResetFonts();
                LanguageChanged?.Invoke();
            }
        }

        public static CultureInfo Culture
        {
            get
            {
                switch (_current)
                {
                    case Language.JA: return CultureInfo.GetCultureInfo("ja-JP");
                    case Language.ZH: return CultureInfo.GetCultureInfo("zh-CN");
                    default: return CultureInfo.GetCultureInfo("en-US");
                }
            }
        }

        public static string Code
        {
            get
            {
                switch (_current)
                {
                    case Language.JA: return "ja";
                    case Language.ZH: return "zh";
                    default: return "en";
                }
            }
        }

        public static Language FromCode(string code)
        {
            switch ((code ?? "").ToLowerInvariant())
            {
                case "ja": return Language.JA;
                case "zh": return Language.ZH;
                default: return Language.EN;
            }
        }

        /// <summary>Translate a key. Missing keys return the key itself so problems are visible, never fatal.</summary>
        public static string T(string key)
        {
            string[] values;
            if (key != null && _table.TryGetValue(key, out values))
            {
                var v = values[(int)_current];
                if (string.IsNullOrEmpty(v)) v = values[0];
                return v;
            }
            return key ?? string.Empty;
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }

        /// <summary>Pick one of three pre-translated values (used by database rows).</summary>
        public static string Pick(string en, string ja, string zh)
        {
            switch (_current)
            {
                case Language.JA: return string.IsNullOrEmpty(ja) ? en : ja;
                case Language.ZH: return string.IsNullOrEmpty(zh) ? en : zh;
                default: return en;
            }
        }

        public static bool Has(string key) => key != null && _table.ContainsKey(key);
    }

    public interface ILocalizable
    {
        void ApplyLocalization();
    }
}
