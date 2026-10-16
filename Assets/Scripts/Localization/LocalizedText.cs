using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Localization
{
    // Sabit bir UI metnini texts.csv anahtarından doldurur, dil değişince günceller.
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;

        Text label;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        void Awake() => label = GetComponent<Text>();

        void OnEnable()
        {
            Texts.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable() => Texts.LanguageChanged -= Refresh;

        void Refresh()
        {
            if (label && !string.IsNullOrEmpty(key)) label.text = Texts.Get(key);
        }
    }
}
