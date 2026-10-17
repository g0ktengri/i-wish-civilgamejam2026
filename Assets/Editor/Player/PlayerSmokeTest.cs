using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Oyuncu kontrolcüsünü Main sahnesinde Play'e girmeden dener: fizik elle adımlanır, karakter
    /// senaryolu girdiyle yürür, zıplar, eğilir, merdivenden çıkıp iner. Sahne kaydedilmez.
    /// Batchmode: -executeMethod Deprem.Player.PlayerSmokeTest.Run  (çıkış kodu 0 = geçti)
    /// </summary>
    internal static class PlayerSmokeTest
    {
        private const float Dt = 0.02f;

        private static PlayerController player;
        private static readonly List<string> Failures = new List<string>();

        [MenuItem("Tools/Deprem/Test: Oyuncu Kontrolcüsü")]
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
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
                Physics.simulationMode = SimulationMode.Script;

                player = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).FirstOrDefault();
                Check(player != null, "Sahnede PlayerController var");
                if (player == null) return Report();
                player.Initialize();
                player.InputEnabled = false;
                var bounds = Object.FindFirstObjectByType<HouseBounds>();
                Check(bounds != null, "Sahnede HouseBounds var");
                bounds?.Activate();

                var stairs = Object.FindObjectsByType<StairPath>(FindObjectsSortMode.None).FirstOrDefault();
                Check(stairs != null, "Sahnede StairPath var");
                stairs?.EnsureRegistered();

                Physics.SyncTransforms();
                Step(1f);
                var p = player.transform.position;
                Check(player.IsGrounded && Mathf.Abs(p.y) < 0.05f, $"Başlangıçta yerde (y={p.y:F2})");

                float startX = p.x;
                foreach (var d in Object.FindObjectsByType<HouseDoor>(FindObjectsSortMode.None)) d.EnsureRegistered();
                player.SetScriptedInput(1f);
                Step(2f);
                p = player.transform.position;
                Check(p.x < 6f, $"Kapalı koridor kapısı yolu kesti (x {startX:F2} -> {p.x:F2})");

                var visibility = Object.FindFirstObjectByType<Deprem.Visual.RoomVisibility>();
                Check(visibility != null, "Sahnede oda örtüsü (RoomVisibility) var");
                visibility?.Initialize();
                visibility?.Tick(p, 1f);
                Check(visibility != null && visibility.IsRevealed("oyuncu") && !visibility.IsRevealed("koridor"),
                    "Kapı kapalıyken sadece oyuncunun odası açık");

                var door = Interactables.FindNearest(player) as HouseDoor;
                Check(door != null, "Kapının önünde F ile kapı açılabilir");
                door?.Interact(player);
                visibility?.Tick(player.transform.position, 1f);
                Check(visibility != null && visibility.IsRevealed("koridor") && !visibility.IsRevealed("salon") && !visibility.IsRevealed("kardes"),
                    "Açık kapıdan koridor göründü; salon ve üst kat kapalı");
                if (visibility != null)
                {
                    visibility.SetSpiritMode(true);
                    visibility.Tick(new Vector3(22f, 1f, 1f), 1f);
                    bool spiritSees = visibility.IsRevealed("salon");
                    visibility.SetSpiritMode(false);
                    visibility.Tick(player.transform.position, 1f);
                    Check(spiritSees && !visibility.IsRevealed("salon"),
                        "Ruhun açtığı salon ruh fazı bitince yeniden kararıyor (kapısı kapalı)");
                }
                Physics.SyncTransforms();
                Step(2f);
                p = player.transform.position;
                Check(p.x > 8.5f, $"Kapıyı açıp sağa yürüdü, koridora geçti (x={p.x:F2})");
                Check(Mathf.Abs(p.z - 1f) < 0.01f, $"Karakter düzleminde kaldı (z={p.z:F2})");
                Check(player.Facing == 1, "Sağa bakıyor");

                player.SetScriptedInput(-1f);
                Step(0.5f);
                Check(player.Facing == -1, "Sola dönünce yönü değişti");

                player.SetScriptedInput(0f);
                Step(0.5f);
                float groundY = player.transform.position.y;
                player.SetScriptedInput(0f, jump: true);
                float maxY = groundY;
                for (int i = 0; i < 60; i++)
                {
                    Step(Dt);
                    maxY = Mathf.Max(maxY, player.transform.position.y);
                }
                Check(maxY - groundY > 0.9f && maxY - groundY < 1.4f, $"Zıpladı (yükseklik {maxY - groundY:F2} m)");
                Check(player.IsGrounded, "Zıpladıktan sonra yere indi");

                player.SetScriptedInput(0f, crouch: true);
                Step(0.2f);
                var capsule = player.GetComponent<CapsuleCollider>();
                Check(player.IsCrouching && Mathf.Abs(capsule.height - player.CrouchHeight) < 0.01f, "S ile eğildi");
                player.SetScriptedInput(0f);
                Step(0.2f);
                Check(!player.IsCrouching && Mathf.Abs(capsule.height - player.StandingHeight) < 0.01f, "Bırakınca kalktı");

                TestStairs(stairs);
                TestHouseEnds();
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

        private static void TestStairs(StairPath stairs)
        {
            if (stairs == null) return;
            var points = stairs.Points;
            var bottom = points[0];
            var top = points[points.Length - 1];

            player.Teleport(bottom + Vector3.right * 0.3f);
            Step(0.3f);
            var target = Interactables.FindNearest(player);
            Check(target != null, "Merdiven dibinde F ile etkileşilebilir bir şey var");
            if (target == null) return;
            target.Interact(player);
            Check(player.IsOnPath, "Merdivene çıkmaya başladı");
            Step(8f);
            var p = player.transform.position;
            Check(!player.IsOnPath, "Merdiven yolu bitti");
            Check(Mathf.Abs(p.y - top.y) < 0.05f && Mathf.Abs(p.z - 1f) < 0.01f, $"Üst kata çıktı (y={p.y:F2}, z={p.z:F2})");

            player.SetScriptedInput(1f);
            Step(0.5f);
            player.SetScriptedInput(0f);
            Step(0.5f);
            p = player.transform.position;
            Check(player.IsGrounded && Mathf.Abs(p.y - top.y) < 0.05f, $"Üst katta yürüdü, düşmedi (y={p.y:F2})");

            player.Teleport(top);
            Step(0.2f);
            target = Interactables.FindNearest(player);
            Check(target != null, "Merdiven başında F ile etkileşilebilir bir şey var");
            if (target == null) return;
            target.Interact(player);
            Step(8f);
            p = player.transform.position;
            Check(Mathf.Abs(p.y) < 0.05f && Mathf.Abs(p.x - bottom.x) < 0.05f, $"Merdivenden indi (x={p.x:F2}, y={p.y:F2})");
        }

        /// <summary>Evin dört ucuna doğru yürü (ve zıpla): karakter evin içinde kalmalı</summary>
        private static void TestHouseEnds()
        {
            const float up = 3.75f;
            var cases = new[]
            {
                ("Zemin kat, sol uç", new Vector3(3f, 0f, 1f), -1f, 0f, 32.5f),
                ("Zemin kat, sağ uç", new Vector3(30f, 0f, 1f), 1f, 0f, 32.5f),
                ("Üst kat, sol uç", new Vector3(3f, up, 1f), -1f, 0f, 27f),
                ("Üst kat, sağ uç", new Vector3(25f, up, 1f), 1f, 0f, 27f),
            };
            foreach (var (name, start, dir, minX, maxX) in cases)
            {
                player.Teleport(start);
                Step(0.3f);
                player.SetScriptedInput(dir);
                for (int i = 0; i < 6; i++)
                {
                    player.SetScriptedInput(dir, jump: true);
                    Step(0.5f);
                }
                player.SetScriptedInput(0f);
                Step(0.5f);
                var p = player.transform.position;
                Check(p.x > minX + 0.2f && p.x < maxX - 0.2f && Mathf.Abs(p.y - start.y) < 0.1f,
                    $"{name}: evin içinde kaldı (x={p.x:F2}, y={p.y:F2})");
            }
        }

        private static void Step(float seconds)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (int i = 0; i < steps; i++)
            {
                player.Simulate(Dt);
                Physics.Simulate(Dt);
            }
        }

        private static void Check(bool condition, string description)
        {
            Debug.Log((condition ? "[SmokeTest] GEÇTİ: " : "[SmokeTest] KALDI: ") + description);
            if (!condition) Failures.Add(description);
        }

        private static bool Report()
        {
            if (Failures.Count == 0)
            {
                Debug.Log("[SmokeTest] SONUÇ: hepsi geçti");
                return true;
            }
            Debug.LogError("[SmokeTest] SONUÇ: " + Failures.Count + " test kaldı:\n" + string.Join("\n", Failures));
            return false;
        }
    }
}
