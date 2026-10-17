using System.Collections.Generic;
using System.Linq;
using Deprem.Core;
using Deprem.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deprem.Family
{
    /// <summary>
    /// Aile sistemini Play'e girmeden dener: döngü 3 (kitaplık babaya), döngü 4 (ilk yardım yok),
    /// döngü 5 (herkes kurtarılıp kabine girer), el feneri yok, gardırop anneye. Sahne kaydedilmez.
    /// Batchmode: -executeMethod Deprem.Family.FamilySmokeTest.Run  (çıkış kodu 0 = geçti)
    /// </summary>
    internal static class FamilySmokeTest
    {
        private const float Dt = 0.02f;
        private const float Up = 3.75f;
        private static readonly List<string> Failures = new List<string>();

        private static PlayerController player;
        private static FamilyDirector director;
        private static FamilyMember father, mother, sibling;

        [MenuItem("Tools/Deprem/Test: Aile Sistemi")]
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
            // M54: test dersleri öğretir; editörün kayıtlı durumu (Play testi) bozulmasın diye sonda geri yüklenir
            bool knewFlashlight = BagLessons.IsLearned(BagItemId.Flashlight);
            bool knewFirstAid = BagLessons.IsLearned(BagItemId.FirstAidKit);
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                BagLessons.Clear();
                FatherHitByBookcase();
                FatherWithoutFirstAid();
                Check(BagLessons.IsLearned(BagItemId.FirstAidKit), "Baba vedasından sonra ilk yardım öğrenildi (M54)");
                FullRescueAndWin();
                SiblingWithoutFlashlight();
                Check(BagLessons.IsLearned(BagItemId.Flashlight), "Kardeş bulunamayınca el feneri öğrenildi (M54)");
                MotherHitByWardrobe();
            }
            catch (System.Exception e)
            {
                Failures.Add("Hata: " + e);
            }
            finally
            {
                Physics.simulationMode = previousMode;
                GameState.ResetAll();
                BagLessons.Clear();
                if (knewFlashlight) BagLessons.Learn(BagItemId.Flashlight);
                if (knewFirstAid) BagLessons.Learn(BagItemId.FirstAidKit);
            }
            return Report();
        }

        // ------------------------------------------------------------------ Senaryolar

        private static void FatherHitByBookcase()
        {
            StartAttempt(3, new BagItemId[0], ItemId.Bookcase);
            Check(father.State == FamilyState.Hit, "Döngü 3: kitaplık devrilince baba ezildi");
            player.Teleport(new Vector3(19.5f, 0f, 1f));
            TickMembers(1f);
            Check(father.PlayerReacted && !player.InputEnabled && GameState.Phase == GamePhase.Rescue, "Babayı görünce oyuncu durup tepki veriyor, keşke ekranı hemen gelmiyor");
            TickMembers(5f); // failDelay 5,5 sn (Berke'nin play testi)
            Check(GameState.Phase == GamePhase.Failed && GameState.LastLoseReason == LoseReason.FamilyHitByItem
                  && GameState.LastLoseItem == ItemId.Bookcase, $"Döngü 3: salona girince 'Keşke kitaplığı sabitleseydim' ({GameState.LastLoseReason}, {GameState.LastLoseItem})");
        }

        // Berke'nin play testi: kaya çantasız kalkar; kan kaybı kabinde ortaya çıkar. İlk yardım yoksa baba kabin
        // girişinde yığılır, oyuncu oraya gelince MissingBagItem
        private static void FatherWithoutFirstAid()
        {
            StartAttempt(4, new BagItemId[0]);
            Check(father.State == FamilyState.Injured, "Döngü 4: baba yaralı (ayağında kaya)");
            player.Teleport(new Vector3(22.2f, 0f, 1f));
            TickMembers(2f);
            Check(GameState.Phase == GamePhase.Rescue, "Döngü 4: babanın yanında hemen kaybedilmiyor (kaya kalkabilir)");
            Check(father.CanInteract(player), "İlk yardım olmadan da kayayı kaldırmak mümkün");
            father.Interact(player);
            TickMembers(3.5f, held: true);
            Check(father.State == FamilyState.WalkingToCabin, $"Kaya kalktı, baba kabine yürüyor ({father.State})");
            for (float t = 0f; t < 30f && father.State == FamilyState.WalkingToCabin; t += Dt) director.Tick(Dt);
            // Play testi 25: kanama sona kadar belli olmaz; baba kabine girer
            Check(father.State == FamilyState.InCabin && GameState.Phase == GamePhase.Rescue,
                $"İlk yardım yok ama baba kabine girdi, henüz kayıp yok ({father.State})");
            var entrance = GameObject.Find("Marker_CabinEntrance").transform.position;
            mother.EnterCabin(entrance + Vector3.forward * 1.75f);
            sibling.EnterCabin(entrance + Vector3.forward * 1.75f);
            player.Teleport(entrance);
            StepWorld(0.2f);
            var target = Interactables.FindNearest(player);
            Check(target != null && !(target is FamilyMember), "Herkes kabindeyken girişte F");
            target?.Interact(player);
            Check(GameState.Phase == GamePhase.Rescue, $"Oyuncu girdi ama kazanılmadı: baba veda ediyor ({GameState.Phase})");
            for (float t = 0f; t < 8f && GameState.Phase == GamePhase.Rescue; t += Dt) director.Tick(Dt);
            Check(GameState.Phase == GamePhase.Failed && GameState.LastLoseReason == LoseReason.MissingBagItem,
                $"Vedadan sonra 'Keşke deprem çantası hazırlasaydım' ({GameState.LastLoseReason})");
        }

        private static void FullRescueAndWin()
        {
            StartAttempt(5, new[] { BagItemId.FirstAidKit, BagItemId.Flashlight });

            // Baba: F basılı tutularak kaya kaldırılır, baba kabine kendisi yürür (Berke'nin play testi 16)
            player.Teleport(new Vector3(22.2f, 0f, 1f));
            TickMembers(2f);
            Check(GameState.Phase == GamePhase.Rescue, "Döngü 5: ilk yardım varken babanın yanında kaybedilmedi");
            var target = Interactables.FindNearest(player);
            Check(ReferenceEquals(target, father), "Babanın yanında F ile etkileşilebilir");
            target?.Interact(player);
            Check(father.IsTreating, "F'ye basınca kaya kaldırma başladı");
            Check(father.PromptKey == "prompt.lift_rock", $"İpucu 'Kayayı kaldır' ({father.PromptKey})");
            TickMembers(1f, held: true);
            Check(father.State == FamilyState.Injured, "Kaldırma 3 sn sürüyor (1 sn'de bitmedi)");
            TickMembers(2.5f, held: true);
            Check(father.State == FamilyState.WalkingToCabin && GameState.IsRescued(FamilyMemberId.Father),
                $"Kaya kalktı, baba kabine yürüyor ({father.State})");
            director.OnMemberRescued(FamilyMemberId.Father);
            Check(director.Followers.Count == 0, "Yürüyen baba takipçi değil");

            // Baba kendi başına kabine varır ve girer (aksayarak ~1 m/sn)
            float fatherStartX = father.transform.position.x;
            float walked = 0f;
            for (; walked < 30f && father.State != FamilyState.InCabin; walked += Dt) director.Tick(Dt);
            Check(father.State == FamilyState.InCabin, $"Baba kabine kendisi girdi ({walked:F1} sn, {fatherStartX:F1} m'den)");

            // Kardeş: el feneriyle
            player.Teleport(new Vector3(4.5f, Up, 1f));
            StepWorld(0.2f);
            TickMembers(2f);
            Check(GameState.Phase == GamePhase.Rescue, "El feneri varken kardeşin odasında kaybedilmedi");
            target = Interactables.FindNearest(player);
            Check(ReferenceEquals(target, sibling), "Kardeşin yanında F ile etkileşilebilir");
            target?.Interact(player);
            Check(sibling.State == FamilyState.Following, "Kardeş takip ediyor");
            director.OnMemberRescued(FamilyMemberId.Sibling);

            // Anne
            player.Teleport(new Vector3(23.6f, Up, 1f));
            StepWorld(0.2f);
            target = Interactables.FindNearest(player);
            Check(ReferenceEquals(target, mother), "Annenin yanında F ile etkileşilebilir");
            target?.Interact(player);
            Check(mother.State == FamilyState.Following, "Anne takip ediyor");
            director.OnMemberRescued(FamilyMemberId.Mother);

            // Kabin
            var entrance = GameObject.Find("Marker_CabinEntrance").transform.position;
            player.Teleport(entrance);
            StepWorld(0.5f);
            target = Interactables.FindNearest(player);
            Check(target != null && !(target is FamilyMember), "Kabin girişinde F ile etkileşilebilir");
            target?.Interact(player);
            Check(FamilyMember.All.All(m => m.State == FamilyState.InCabin), "Herkes kabine girdi");
            Check(director.PlayerInCabin && GameState.Phase == GamePhase.Won, $"Oyuncu da girdi, oyun kazanıldı ({GameState.Phase})");
        }

        private static void SiblingWithoutFlashlight()
        {
            StartAttempt(5, new[] { BagItemId.FirstAidKit });
            Check(sibling.Hidden, "Kurtarma fazında kardeş karanlıkta saklanıyor (Play testi 24)");
            player.Teleport(new Vector3(5.5f, Up, 1f));
            TickMembers(1f);
            Check(sibling.Hidden && !sibling.CanInteract(player), "Fener yok: yanındayken bile bulunamıyor");
            TickMembers(6f);
            Check(GameState.Phase == GamePhase.Failed && GameState.LastLoseReason == LoseReason.MissingBagItem,
                $"El feneri yok, aranırken artçı: 'Keşke deprem çantası hazırlasaydım' ({GameState.LastLoseReason})");
        }

        private static void MotherHitByWardrobe()
        {
            StartAttempt(5, new[] { BagItemId.FirstAidKit, BagItemId.Flashlight }, ItemId.Wardrobe);
            player.Teleport(new Vector3(22f, Up, 1f));
            TickMembers(6f);
            Check(GameState.Phase == GamePhase.Failed && GameState.LastLoseReason == LoseReason.FamilyHitByItem
                  && GameState.LastLoseItem == ItemId.Wardrobe, $"Gardırop anneye devrildi, 'Keşke gardırobu sabitleseydim' ({GameState.LastLoseItem})");
        }

        // ------------------------------------------------------------------ Yardımcılar

        /// <summary>Sahneyi taze açar, denemeyi başlatır ve ana depremden kurtarma fazına kadar ilerletir.</summary>
        private static void StartAttempt(int loop, BagItemId[] bag, params ItemId[] toppled)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            Interactables.Clear();
            FamilyMember.ClearRegistry();

            player = Object.FindFirstObjectByType<PlayerController>();
            player.Initialize();
            player.InputEnabled = false;
            director = Object.FindFirstObjectByType<FamilyDirector>();
            director.EnsureRegistered();
            var members = Object.FindObjectsByType<FamilyMember>(FindObjectsSortMode.None);
            foreach (var m in members)
            {
                m.EnsureRegistered();
                m.PlaceAtHome();
            }
            father = FamilyMember.Find(FamilyMemberId.Father);
            mother = FamilyMember.Find(FamilyMemberId.Mother);
            sibling = FamilyMember.Find(FamilyMemberId.Sibling);

            GameState.ResetAll();
            GameState.BeginAttempt(loop);
            foreach (var item in bag) GameState.AddBagItem(item);
            SetPhase(GamePhase.MainQuake);
            foreach (var item in toppled)
                foreach (var m in members) m.OnItemToppled(item);
            SetPhase(GamePhase.Rescue);
            Physics.SyncTransforms();
        }

        private static void SetPhase(GamePhase phase)
        {
            GameState.SetPhase(phase);
            foreach (var m in FamilyMember.All) m.OnPhaseChanged(phase);
        }

        private static void TickMembers(float seconds, bool held = false)
        {
            for (float t = 0f; t < seconds; t += Dt)
                foreach (var m in FamilyMember.All) m.Tick(Dt, held);
        }

        private static void StepWorld(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                player.Simulate(Dt);
                Physics.Simulate(Dt);
                director.Tick(Dt);
            }
        }

        private static void Check(bool condition, string description)
        {
            Debug.Log((condition ? "[FamilyTest] GEÇTİ: " : "[FamilyTest] KALDI: ") + description);
            if (!condition) Failures.Add(description);
        }

        private static bool Report()
        {
            if (Failures.Count == 0)
            {
                Debug.Log("[FamilyTest] SONUÇ: hepsi geçti");
                return true;
            }
            Debug.LogError("[FamilyTest] SONUÇ: " + Failures.Count + " test kaldı:\n" + string.Join("\n", Failures));
            return false;
        }
    }
}
