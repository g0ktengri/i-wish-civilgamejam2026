using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Deprem.Localization
{
    public enum Language
    {
        Turkish = 0,
        English = 1,
    }

    // Assets/Resources/texts.csv dosyasını okur. Sütunlar: key,tr,en
    // "#" ile başlayan satırlar ve boş satırlar atlanır.
    public static class Texts
    {
        const string ResourcePath = "texts";
        const string LanguagePrefKey = "language";

        static Dictionary<string, string[]> table;
        static readonly HashSet<string> warnedKeys = new HashSet<string>();

        public static Language Current { get; private set; }
        public static event Action LanguageChanged;

        public static string Get(string key)
        {
            EnsureLoaded();
            if (table.TryGetValue(key, out var row))
            {
                int column = (int)Current;
                if (column < row.Length && !string.IsNullOrEmpty(row[column])) return row[column];
                if (row.Length > 0 && !string.IsNullOrEmpty(row[0])) return row[0]; // çeviri yoksa Türkçe
            }
            if (warnedKeys.Add(key)) Debug.LogWarning($"[Texts] Anahtar yok: {key}");
            return $"[{key}]";
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        public static bool Has(string key)
        {
            EnsureLoaded();
            return table.ContainsKey(key);
        }

        public static void SetLanguage(Language language)
        {
            EnsureLoaded();
            if (Current == language) return;
            Current = language;
            PlayerPrefs.SetInt(LanguagePrefKey, (int)language);
            LanguageChanged?.Invoke();
        }

        public static void ToggleLanguage() =>
            SetLanguage(Current == Language.Turkish ? Language.English : Language.Turkish);

        static void EnsureLoaded()
        {
            if (table != null) return;
            table = new Dictionary<string, string[]>();
            Current = (Language)PlayerPrefs.GetInt(LanguagePrefKey, (int)Language.Turkish);

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (!asset)
            {
                Debug.LogError("[Texts] Assets/Resources/texts.csv bulunamadı");
                return;
            }

            bool header = true;
            foreach (var fields in ParseCsv(asset.text))
            {
                if (header) { header = false; continue; }
                if (fields.Count == 0 || string.IsNullOrWhiteSpace(fields[0])) continue;
                string key = fields[0].Trim();
                if (key.StartsWith("#")) continue;

                var values = new string[fields.Count - 1];
                for (int i = 1; i < fields.Count; i++) values[i - 1] = fields[i];
                if (!table.TryAdd(key, values)) Debug.LogWarning($"[Texts] Tekrar eden anahtar: {key}");
            }
        }

        // Tırnak içinde virgül, çift tırnak ("") ve satır sonu destekler
        static IEnumerable<List<string>> ParseCsv(string text)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return fields;
                    fields = new List<string>();
                }
                else field.Append(c);
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                yield return fields;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            table = null;
            warnedKeys.Clear();
            LanguageChanged = null;
        }
    }
}
