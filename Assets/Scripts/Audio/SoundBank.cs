using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Audio
{
    /// <summary>
    /// Anahtar → ses listesi. AudioSetup (Tools > Deprem > Sesleri Üret) kurar: Assets/Audio/&lt;anahtar&gt;*.wav varsa
    /// onları, yoksa kodla üretilenleri (Assets/Generated/Audio) bağlar. Birden çok ses varsa her çalışta biri seçilir.
    /// </summary>
    [CreateAssetMenu(menuName = "Deprem/Ses Bankası")]
    public sealed class SoundBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public AudioClip[] clips;
            [Range(0f, 1.5f)] public float volume = 1f;
            [Tooltip("Her çalışta perde bu aralıkta rastgele (tekrar eden ses sırıtmasın)")]
            public Vector2 pitch = Vector2.one;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        private Dictionary<string, Entry> lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(Entry[] value)
        {
            entries = value;
            lookup = null;
        }

        public Entry Find(string key)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Entry>();
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.key) && e.clips != null && e.clips.Length > 0)
                        lookup[e.key] = e;
            }
            return key != null && lookup.TryGetValue(key, out var entry) ? entry : null;
        }
    }
}
