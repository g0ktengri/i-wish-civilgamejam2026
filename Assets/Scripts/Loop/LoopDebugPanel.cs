using Deprem.Core;
using UnityEngine;

namespace Deprem.Loop
{
    // Geliştirici paneli: diğer sistemler hazır olmadan döngü akışını test etmek için.
    // Sadece Editor ve Development Build'de görünür; oyuncuya gösterilmez.
    public class LoopDebugPanel : MonoBehaviour
    {
        [SerializeField] bool visible = true;
        [SerializeField] Rect area = new Rect(10, 10, 260, 520);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly GamePhase[] phases =
            { GamePhase.Foreshock, GamePhase.Spirit, GamePhase.MainQuake, GamePhase.Rescue };

        void OnGUI()
        {
            if (!visible) return;

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label($"Döngü {GameState.CurrentLoop} · {GameState.Phase}");
            GUILayout.Label($"Başarısız deneme: {GameState.FailedAttempts} · Yara: {GameState.AftershockInjuries}");
            GUILayout.Label($"Sabit: {string.Join(", ", GameState.SecuredItems)}");
            GUILayout.Label($"Çanta: {string.Join(", ", GameState.BagItems)}");
            if (GameState.LastLoseReason != LoseReason.None)
                GUILayout.Label($"Son kayıp: {GameState.LastLoseReason} {GameState.LastLoseItem}");

            GUILayout.Space(6);
            GUILayout.Label("Faz");
            GUILayout.BeginHorizontal();
            foreach (var phase in phases)
                if (GUILayout.Button(phase.ToString())) GameState.SetPhase(phase);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Kaybet");
            for (var reason = LoseReason.IgnoredForeshock; reason <= LoseReason.RescueTimeout; reason++)
                if (GUILayout.Button(reason.ToString())) GameState.FailLoop(reason, ItemId.Bookcase);

            GUILayout.Space(6);
            if (GUILayout.Button("Kazan")) GameState.WinLoop();
            if (GUILayout.Button("Sabitle: Kitaplık")) GameState.MarkItemSecured(ItemId.Bookcase);
            if (GUILayout.Button("Çanta: İlk yardım")) GameState.AddBagItem(BagItemId.FirstAidKit);

            var loop = LoopManager.Instance;
            if (loop)
            {
                GUILayout.Space(6);
                if (loop.WaitingToContinue && GUILayout.Button("Keşke ekranını geç")) loop.ContinueAfterWish();
                GUILayout.Label("Bölüm seç");
                GUILayout.BeginHorizontal();
                for (int i = 1; i <= GameConstants.LoopCount; i++)
                    if (GUILayout.Button(i.ToString())) loop.StartFromLoop(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
#endif
    }
}
