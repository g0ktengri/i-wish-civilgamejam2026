using System.Collections.Generic;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// F ile etkileşilebilen her şey: merdiven, kabin, aile üyeleri, eşyalar.
    /// Not: Berke'nin sistemleri de kullanacaksa Core'a taşınmalı.
    /// </summary>
    public interface IInteractable
    {
        Vector3 InteractionPoint { get; }
        float InteractionRange { get; }
        bool CanInteract(PlayerController player);
        void Interact(PlayerController player);
    }

    /// <summary>İsteğe bağlı: etkileşilebilirin F ipucu (texts.csv anahtarı). Yoksa "prompt.interact".</summary>
    public interface IPromptSource
    {
        string PromptKey { get; }
    }

    /// <summary>Sahnedeki etkileşilebilirlerin listesi; collider gerektirmez.</summary>
    public static class Interactables
    {
        private static readonly List<IInteractable> All = new List<IInteractable>();

        public static void Register(IInteractable interactable)
        {
            if (!All.Contains(interactable)) All.Add(interactable);
        }

        public static void Unregister(IInteractable interactable)
        {
            All.Remove(interactable);
        }

        /// <summary>Listeyi boşaltır (Play başında; editör testlerinde sahne yeniden açılınca)</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            All.Clear();
        }

        /// <summary>X-Y düzleminde menzil içindeki en yakın etkileşilebilir (Z, 2.5D'de önemsiz)</summary>
        public static IInteractable FindNearest(PlayerController player)
        {
            var position = player.transform.position;
            IInteractable best = null;
            float bestDistance = float.MaxValue;
            foreach (var interactable in All)
            {
                var point = interactable.InteractionPoint;
                float distance = Vector2.Distance(new Vector2(position.x, position.y), new Vector2(point.x, point.y));
                if (distance > interactable.InteractionRange || distance >= bestDistance) continue;
                if (!interactable.CanInteract(player)) continue;
                best = interactable;
                bestDistance = distance;
            }
            return best;
        }
    }
}
