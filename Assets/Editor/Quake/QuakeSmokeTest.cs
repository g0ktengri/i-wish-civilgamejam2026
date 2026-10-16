using System.Collections.Generic;
using System.Linq;
using Deprem.Core;
using Deprem.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Deprem sisteminin Play'e girmeden denenebilen parçaları: çök-kapan-tutun kuralı, yatıp kalkma,
    /// eşyaların Core bağlantısı ve devrilmesi. Sahne kaydedilmez.
    /// Batchmode: -executeMethod Deprem.Quake.QuakeSmokeTest.Run  (çıkış kodu 0 = geçti)
    /// </summary>
    internal static class QuakeSmokeTest
    {
        private const float Dt = 0.02f;
        private static readonly List<string> Failures = new List<string>();

        [MenuItem("Tools/Deprem/Test: Deprem Sistemi")]
        public static void RunInEditor()
        {
            RunTests();
        }

        public static void Run()
        {
            bool passed = RunTests();
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static bool RunTests()
        {
            Failures.Clear();
            var previousMode = Physics.simulationMode;
            try
            {
                TestDuckCoverHoldRule();

                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
                Physics.simulationMode = SimulationMode.Script;

                Check(Object.FindFirstObjectByType<QuakeDirector>() != null, "Sahnede QuakeDirector var");
                var lights = Object.FindFirstObjectByType<HouseLights>();
                Check(lights != null, "Sahnede HouseLights var");

                var player = Object.FindFirstObjectByType<PlayerController>();
                player.Initialize();
                TestLieDownAndGetUp(player);
                TestItems(player);
            }
            catch (System.Exception e)
            {
                Failures.Add("Hata: " + e);
            }
            finally
            {
                Physics.simulationMode = previousMode;
            }
            return Report();
        }

        private static void TestDuckCoverHoldRule()
        {
            var check = Simulate(new DuckCoverHoldCheck(1.5f, 0.35f), 12f, t => t >= 0.5f);
            Check(!check.Failed, "Ç-K-T: 0,5 sn'de basıp sonuna kadar tutunca geçti");

            check = Simulate(new DuckCoverHoldCheck(1.5f, 0.35f), 1.4f, t => false);
            Check(!check.Failed, "Ç-K-T: başlama süresi içinde basmamak sorun değil");
            check = Simulate(new DuckCoverHoldCheck(1.5f, 0.35f), 2.5f, t => false);
            Check(check.Failed, "Ç-K-T: hiç basmayınca başarısız");

            check = Simulate(new DuckCoverHoldCheck(1.5f, 0.35f), 12f, t => t < 5f);
            Check(check.Failed, "Ç-K-T: erken bırakınca başarısız");

            check = Simulate(new DuckCoverHoldCheck(1.5f, 0.35f), 12f, t => t < 5f || t > 5.2f);
            Check(!check.Failed, "Ç-K-T: 0,2 sn'lik kısa bırakma affedildi");
        }

        private static DuckCoverHoldCheck Simulate(DuckCoverHoldCheck check, float duration, System.Func<float, bool> holding)
        {
            for (float t = 0f; t < duration; t += Dt) check.Update(Dt, holding(t));
            return check;
        }

        private static void TestLieDownAndGetUp(PlayerController player)
        {
            var bed = GameObject.Find("Marker_PlayerBed").transform.position + Vector3.up * 0.5f;
            player.LieDown(bed);
            Step(player, null, 1f);
            Check(player.IsLying && (player.transform.position - bed).sqrMagnitude < 0.0001f, "Yatakta yatıyor ve yerinden oynamadı");

            var stand = new Vector3(bed.x + 0.8f, 0f, 1f);
            bool done = false;
            player.GetUp(stand, () => done = true);
            Step(player, null, 3f);
            var p = player.transform.position;
            Check(done && !player.IsLying && !player.IsOnPath, "Yataktan kalktı");
            Check(Mathf.Abs(p.z - 1f) < 0.01f && player.IsGrounded, $"Kalkınca karakter düzleminde ve yerde (y={p.y:F2}, z={p.z:F2})");
        }

        private static void TestItems(PlayerController player)
        {
            var items = Object.FindObjectsByType<SecurableItem>(FindObjectsSortMode.None);
            Check(items.Length == 9, $"9 eşyada SecurableItem var ({items.Length})");
            foreach (var item in items)
            {
                bool idMatches = item.name.StartsWith($"Item_{(int)item.Id}_");
                Check(idMatches && item.Id != ItemId.None, $"{item.name}: ItemId = {item.Id}, minigame = {item.Minigame}");
                Check(item.GetComponent<ItemToppler>() != null, $"{item.name}: ItemToppler var");
            }

            var topplers = items.Select(i => i.GetComponent<ItemToppler>()).Where(t => t != null).ToList();
            var floorKeys = new[] { "kitaplik", "tv_unitesi", "ayakkabilik", "oyuncak_rafi", "gardirop" };
            foreach (var toppler in topplers)
            {
                toppler.Initialize();
                toppler.Topple();
            }
            Step(player, topplers, 6f);

            foreach (var toppler in topplers)
            {
                var bounds = toppler.CombinedBounds();
                float tilt = Vector3.Angle(toppler.transform.up, Vector3.up);
                Check(toppler.HasSettled, $"{toppler.name}: devrildi ve durdu (eğim {tilt:F0}°)");
                Check(bounds.min.z > -0.25f, $"{toppler.name}: evin içinde kaldı (ön z={bounds.min.z:F2})");
                if (floorKeys.Any(k => toppler.name.EndsWith(k)))
                    Check(bounds.min.z < 1.25f && tilt > 45f, $"{toppler.name}: karakterin yolunu kesiyor (ön z={bounds.min.z:F2})");
            }

            TestSafeSpots(topplers);
            TestRescueTimer(player, topplers);
        }

        private static void TestRescueTimer(PlayerController player, List<ItemToppler> toppled)
        {
            var timer = Object.FindFirstObjectByType<RescueTimer>();
            Check(timer != null, "Sahnede RescueTimer var");
            if (timer == null) return;
            timer.Initialize();

            // İki kayıptan sonra gizli bonus: +20 sn
            GameState.ResetAll();
            GameState.BeginAttempt(3);
            GameState.FailLoop(LoseReason.NoDuckCoverHold);
            GameState.BeginAttempt(3);
            GameState.FailLoop(LoseReason.NoDuckCoverHold);
            GameState.BeginAttempt(5);
            GameState.SetPhase(GamePhase.Rescue);
            timer.OnPhaseChanged(GamePhase.Rescue);
            float baseDuration = timer.Duration - 20f;
            Check(timer.IsRunning && Mathf.Abs(timer.Duration - baseDuration - 20f) < 0.01f, $"Süre başladı, 2 kayıp için +20 sn bonus ({timer.Duration:F0} sn)");

            // Oyuncu devrilmiş ayakkabılığın yanında oyalanır
            var shoeRack = toppled.First(t => t.name.EndsWith("ayakkabilik"));
            var b = shoeRack.CombinedBounds();
            player.Teleport(new Vector3(b.center.x, 0f, 1f));
            TickTimer(timer, 4f);
            player.Teleport(new Vector3(3f, 0f, 1f));
            TickTimer(timer, timer.Remaining - 1f);
            Check(GameState.Phase == GamePhase.Rescue && timer.IsRunning, "Süre bitmeden kayıp yok");

            TickTimer(timer, 1f + 5f);
            Check(GameState.Phase == GamePhase.Rescue, "Çubuk bitince gizli tolerans: büyük artçı hemen vurmadı");
            TickTimer(timer, 4f);
            Check(GameState.Phase == GamePhase.Failed && GameState.LastLoseReason == LoseReason.RescueTimeout,
                $"Tolerans da bitince büyük artçı: RescueTimeout ({GameState.LastLoseReason})");
            Check(GameState.LastLoseItem == ItemId.ShoeRack, $"Keşke cümlesi en çok yavaşlatan eşyayı söylüyor ({GameState.LastLoseItem})");

            // Kabine yetişilirse süre durur
            GameState.BeginAttempt(5);
            GameState.SetPhase(GamePhase.Rescue);
            timer.OnPhaseChanged(GamePhase.Rescue);
            TickTimer(timer, 10f);
            GameState.WinLoop();
            timer.OnPhaseChanged(GamePhase.Won);
            TickTimer(timer, timer.Duration + 20f);
            Check(GameState.Phase == GamePhase.Won && !timer.IsRunning, "Kazanınca süre durdu, kayıp yok");
            GameState.ResetAll();
        }

        private static void TickTimer(RescueTimer timer, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) timer.Tick(Dt);
        }

        private static void TestSafeSpots(List<ItemToppler> toppled)
        {
            var spots = Object.FindObjectsByType<SafeSpot>(FindObjectsSortMode.None);
            foreach (var spot in spots) spot.Register();
            Check(spots.Length == 7, $"Her odada bir güvenli nokta var ({spots.Length})");

            foreach (var spot in spots)
            {
                var p = spot.transform.position;
                var covering = toppled.Where(t =>
                {
                    var b = t.CombinedBounds();
                    bool sameFloor = b.min.y < p.y + 1.5f && b.max.y > p.y - 0.1f;
                    bool overX = b.max.x > p.x - 0.7f && b.min.x < p.x + 0.7f;
                    return sameFloor && overX && b.min.z < 1.25f && b.max.z > 0.75f;
                }).Select(t => t.name).ToList();
                Check(covering.Count == 0, $"{spot.name}: devrilen eşyaların altında kalmıyor{(covering.Count > 0 ? " (" + string.Join(", ", covering) + ")" : "")}");
            }

            var bed = GameObject.Find("Marker_PlayerBed").transform.position;
            Check(SafeSpot.AnyContains(new Vector3(bed.x + 0.8f, 0f, 1f)), "Ruh fazı sonrası ayağa kalkılan yer güvenli noktada");
            Check(!SafeSpot.AnyContains(new Vector3(12f, 0f, 1f)), "Koridorun ortası güvenli nokta değil");
        }

        private static void Step(PlayerController player, List<ItemToppler> topplers, float seconds)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (int i = 0; i < steps; i++)
            {
                player.Simulate(Dt);
                if (topplers != null) foreach (var t in topplers) t.Tick(Dt);
                Physics.Simulate(Dt);
            }
        }

        private static void Check(bool condition, string description)
        {
            Debug.Log((condition ? "[QuakeTest] GEÇTİ: " : "[QuakeTest] KALDI: ") + description);
            if (!condition) Failures.Add(description);
        }

        private static bool Report()
        {
            if (Failures.Count == 0)
            {
                Debug.Log("[QuakeTest] SONUÇ: hepsi geçti");
                return true;
            }
            Debug.LogError("[QuakeTest] SONUÇ: " + Failures.Count + " test kaldı:\n" + string.Join("\n", Failures));
            return false;
        }
    }
}
