using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Spirit
{
    // Ruhun F ile etkileşebildiği, eşya olmayan şeyler (çanta malzemesi, çanta).
    // Sabitlenebilir eşyalar SecurableItem.All üzerinden ayrıca bulunur.
    public interface ISpiritTarget
    {
        Vector3 Point { get; }
        float Range { get; }
        bool CanInteract { get; }
        string PromptKey { get; }
        void Interact();
    }

    public static class SpiritTargets
    {
        static readonly List<ISpiritTarget> all = new List<ISpiritTarget>();
        public static IReadOnlyList<ISpiritTarget> All => all;

        public static void Register(ISpiritTarget target)
        {
            if (!all.Contains(target)) all.Add(target);
        }

        public static void Unregister(ISpiritTarget target) => all.Remove(target);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => all.Clear();
    }
}
