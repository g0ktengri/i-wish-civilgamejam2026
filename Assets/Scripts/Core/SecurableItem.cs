using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Core
{
    // 9 etkileşimli eşyanın her birinde bulunur.
    // Berke (minigame) sadece Secure() çağırır; Mehmet (deprem) sadece IsSecured okur.
    public class SecurableItem : MonoBehaviour
    {
        static readonly List<SecurableItem> all = new List<SecurableItem>();
        public static IReadOnlyList<SecurableItem> All => all;

        [SerializeField] ItemId id;
        [SerializeField] MinigameType minigame;
        [Tooltip("Sabitlenince görünen L demiri ya da dolap mandalı")]
        [SerializeField] GameObject securedVisual;

        public ItemId Id => id;
        public MinigameType Minigame => minigame;
        public bool IsSecured { get; private set; }

        public static SecurableItem Find(ItemId itemId)
        {
            foreach (var item in all)
                if (item.id == itemId) return item;
            return null;
        }

        public void Secure()
        {
            if (IsSecured) return;
            IsSecured = true;
            if (securedVisual) securedVisual.SetActive(true);
            GameState.MarkItemSecured(id);
        }

        void OnEnable()
        {
            all.Add(this);
            GameEvents.LoopStarted += HandleLoopStarted;
        }

        void OnDisable()
        {
            all.Remove(this);
            GameEvents.LoopStarted -= HandleLoopStarted;
        }

        void Start() => SetUnsecured();

        // Zaman geri sarılınca eşya sabitlenmemiş hale döner
        void HandleLoopStarted(int loop) => SetUnsecured();

        void SetUnsecured()
        {
            IsSecured = false;
            if (securedVisual) securedVisual.SetActive(false);
        }
    }
}
