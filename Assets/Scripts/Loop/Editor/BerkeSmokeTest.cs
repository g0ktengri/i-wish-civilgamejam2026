using System;
using System.Collections.Generic;
using System.Reflection;
using Deprem.Bag;
using Deprem.Core;
using Deprem.Localization;
using Deprem.Minigames;
using Deprem.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Deprem.Loop.EditorTools
{
    // Berke'nin sistemleri için hızlı test (Editor kapalıyken batchmode):
    //   Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod Deprem.Loop.EditorTools.BerkeSmokeTest.Run
    // Minigame'ler prefab'daki gerçek ayarlarla, insan gibi oynanır (vida çevrilir, eşya boşluktan geçirilir,
    // kilitte yeşil alanda tıklanır). Keşke cümleleri ve dinamik metin anahtarları iki dilde denenir.
    public static class BerkeSmokeTest
    {
        const string Tag = "[BerkeTest] ";
        const string PrefabPath = "Assets/Prefabs/Berke/BerkeSystems.prefab";
        const float Dt = 1f / 60f;
        static readonly List<string> Failures = new List<string>();

        [MenuItem("Tools/Deprem/Berke: Smoke Test")]
        public static void Run()
        {
            Failures.Clear();
            var language = Texts.Current;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Check(prefab != null, "BerkeSystems prefab'ı var");
                if (prefab != null)
                {
                    TestPrefab(prefab);
                    var host = new SerializedObject(prefab.GetComponent<MinigameHost>());
                    var panelSize = host.FindProperty("panelSize").vector2Value;
                    TestLock(host, panelSize);
                    TestScrew(host, panelSize);
                    TestCarry(host, panelSize);
                }
                foreach (var l in new[] { Language.Turkish, Language.English })
                {
                    Texts.SetLanguage(l);
                    TestTexts(l);
                    TestWishLines(l);
                }
            }
            catch (Exception e)
            {
                Check(false, "Beklenmeyen hata: " + e);
            }
            finally
            {
                Texts.SetLanguage(language);
            }

            bool passed = Failures.Count == 0;
            if (passed) Debug.Log(Tag + "SONUÇ: hepsi geçti");
            else Debug.LogError(Tag + "SONUÇ: " + Failures.Count + " test kaldı:\n" + string.Join("\n", Failures));
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }

        // ------------------------------------------------------------------ Prefab

        static void TestPrefab(GameObject prefab)
        {
            foreach (var type in new[] { typeof(LoopManager), typeof(WishScreen), typeof(MinigameHost), typeof(BagSystem),
                         typeof(FinalScreen), typeof(MainMenu), typeof(PixelScreen) })
                Check(prefab.GetComponent(type) != null, $"Prefab'da {type.Name} var");

            var bag = new SerializedObject(prefab.GetComponent<BagSystem>());
            Check(bag.FindProperty("bagModel").objectReferenceValue != null, "Deprem çantası modeli bağlı (M23)");
            var supplies = bag.FindProperty("supplies");
            for (int i = 0; i < supplies.arraySize; i++)
            {
                var def = supplies.GetArrayElementAtIndex(i);
                var id = def.FindPropertyRelative("id");
                Check(def.FindPropertyRelative("model").objectReferenceValue != null,
                    $"Çanta malzemesi modeli bağlı: {id.enumNames[id.enumValueIndex]} (M20)");
            }
        }

        // ------------------------------------------------------------------ Minigame'ler

        static RectTransform CreatePanel(Vector2 size, out GameObject root)
        {
            root = new GameObject("BerkeTestCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);
            panel.sizeDelta = size;
            return panel;
        }

        static MinigameInput Input(Vector2 pointer, bool held, bool pressed = false, bool released = false) =>
            new MinigameInput { Pointer = pointer, Held = held, Pressed = pressed, Released = released, DeltaTime = Dt };

        // Kilit: iğne yeşil alandayken tıklayan oyuncu bitirebilmeli. Play testi 8 hatası: ıskalayınca alan ve hız
        // geri gelmiyordu; isabet + ıskalama sonrası alan baştaki genişlikte olmalı.
        static void TestLock(SerializedObject host, Vector2 panelSize)
        {
            float speed = host.FindProperty("lockCyclesPerSecond").floatValue;
            float zone = host.FindProperty("lockZoneWidth").floatValue;
            int hits = host.FindProperty("lockRequiredHits").intValue;
            float shrink = host.FindProperty("lockShrinkPerHit").floatValue;
            float speedUp = host.FindProperty("lockSpeedUpPerHit").floatValue;

            // 1. Kusursuz oyuncu
            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new LockMinigame(speed, zone, hits, shrink, speedUp);
                game.Build(panel, null);
                var zoneRect = (RectTransform)panel.Find("Bar/Zone");
                var needle = (RectTransform)panel.Find("Bar/Needle");
                float startWidth = zoneRect.sizeDelta.x;

                bool done = false;
                float t = 0f;
                bool hitOnce = false, missedAfterHit = false;
                float widthAfterMiss = -1f;
                for (; t < 30f && !done; t += Dt)
                {
                    float dx = Mathf.Abs(needle.anchoredPosition.x - zoneRect.anchoredPosition.x);
                    bool inside = dx < zoneRect.sizeDelta.x * 0.35f;
                    bool farAway = dx > zoneRect.sizeDelta.x * 1.5f;
                    bool press;
                    if (!hitOnce) { press = inside; }
                    else if (!missedAfterHit) { press = farAway; }
                    else { press = inside; }
                    done = game.Tick(Input(Vector2.zero, press, press));
                    if (press && !hitOnce) hitOnce = true;
                    else if (press && hitOnce && !missedAfterHit)
                    {
                        missedAfterHit = true;
                        widthAfterMiss = zoneRect.sizeDelta.x;
                    }
                    // Bir kare sonra iğne hâlâ alandaysa ikinci tıklama sayılmasın
                    if (press) game.Tick(Input(Vector2.zero, false));
                }
                Check(Mathf.Abs(widthAfterMiss - startWidth) < 0.5f,
                    $"Kilit: isabet + ıskalama sonrası alan baştaki gibi ({widthAfterMiss:F1} / {startWidth:F1} px)");
                Check(done, $"Kilit: yeşilde tıklayan oyuncu bitiriyor ({t:F1} sn, ıskalama dahil)");

                // Son isabetin zaman penceresi insan için yeterli mi (~0,12 sn üstü)
                float barWidth = panelSize.x * 0.8f;
                float lastZone = zone * Mathf.Pow(shrink, hits - 1) * barWidth;
                float lastSpeed = speed * Mathf.Pow(speedUp, hits - 1) * barWidth; // px/sn
                float window = lastZone / lastSpeed;
                Check(window >= 0.12f, $"Kilit: son isabet penceresi {window:F2} sn (≥ 0,12)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Vida: tornavidanın ucunu vidaya getirip saat yönünde çeviren oyuncu bitirebilmeli
        static void TestScrew(SerializedObject host, Vector2 panelSize)
        {
            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new ScrewMinigame(host.FindProperty("screwTurnsPerScrew").floatValue,
                    host.FindProperty("screwReversePenalty").floatValue, host.FindProperty("screwMinRadius").floatValue,
                    host.FindProperty("screwCount").intValue);
                game.Build(panel, null);
                var screws = new List<Vector2>();
                foreach (Transform child in panel)
                    if (child.name == "Screw") screws.Add(((RectTransform)child).anchoredPosition);
                var driver = (RectTransform)panel.Find("Screwdriver");

                // Boştaki uç ile fare arasındaki fark
                game.Tick(Input(Vector2.zero, false));
                Vector2 offset = -driver.anchoredPosition;

                bool done = false;
                float t = 0f;
                foreach (var screw in screws)
                {
                    Vector2 grab = screw + offset;
                    game.Tick(Input(grab, true, true));
                    float radius = offset.magnitude;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    for (int frame = 0; frame < 900 && !done; frame++, t += Dt)
                    {
                        angle -= 9f * Mathf.Deg2Rad; // saat yönü, karede 9° (~1,5 tur/sn)
                        var pointer = screw + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        done = game.Tick(Input(pointer, true));
                    }
                    game.Tick(Input(grab, false, false, true));
                }
                Check(screws.Count > 0, $"Vida: {screws.Count} vida var");
                Check(done, $"Vida: saat yönünde çeviren oyuncu bitiriyor ({t:F1} sn)");

                // Ters yönde çevirmek ilerletmemeli
                var reverse = new ScrewMinigame(host.FindProperty("screwTurnsPerScrew").floatValue, 1f, 18f, 1);
                var panel2 = CreatePanel(panelSize, out var root2);
                try
                {
                    reverse.Build(panel2, null);
                    var driver2 = (RectTransform)panel2.Find("Screwdriver");
                    var screw = ((RectTransform)panel2.Find("Screw")).anchoredPosition;
                    reverse.Tick(Input(Vector2.zero, false));
                    Vector2 off = -driver2.anchoredPosition;
                    reverse.Tick(Input(screw + off, true, true));
                    float a = Mathf.Atan2(off.y, off.x);
                    bool reverseDone = false;
                    for (int frame = 0; frame < 600 && !reverseDone; frame++)
                    {
                        a += 9f * Mathf.Deg2Rad;
                        reverseDone = reverse.Tick(Input(screw + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * off.magnitude, true));
                    }
                    Check(!reverseDone, "Vida: ters yönde çevirmek bitirmiyor");
                }
                finally
                {
                    Object.DestroyImmediate(root2);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Taşı-yerleştir: eşyayı duvardaki boşluktan geçirip güvenli alana bırakan oyuncu bitirebilmeli.
        // Yerleşim her açılışta rastgele: 40 farklı yerleşim denenir. Duvardan geçmeye çalışan bitirememeli.
        static void TestCarry(SerializedObject host, Vector2 panelSize)
        {
            float snap = host.FindProperty("carrySnapRadius").floatValue;
            float gap = host.FindProperty("carryGapHeight").floatValue;
            int passed = 0, cheated = 0, blocked = 0;
            const int Runs = 40;
            var state = UnityEngine.Random.state;
            for (int run = 0; run < Runs; run++)
            {
                UnityEngine.Random.InitState(1000 + run);
                if (PlayCarry(panelSize, snap, gap, true, out _)) passed++;
                UnityEngine.Random.InitState(1000 + run);
                bool straightDone = PlayCarry(panelSize, snap, gap, false, out bool straightBlocked);
                if (straightBlocked)
                {
                    blocked++;
                    if (straightDone) cheated++;
                }
            }
            UnityEngine.Random.state = state;
            Check(passed == Runs, $"Taşı-yerleştir: boşluktan geçen oyuncu her yerleşimde bitiriyor ({passed}/{Runs})");
            // Düz çizgisi zaten boşluktan geçen yerleşimler sayılmaz (meşru yol)
            Check(blocked > 0 && cheated == 0, $"Taşı-yerleştir: duvarın içinden düz giden bitiremiyor ({cheated}/{blocked} duvara çarpan yerleşim)");
        }

        static bool PlayCarry(Vector2 panelSize, float snap, float gap, bool throughGap, out bool straightBlocked)
        {
            straightBlocked = false;
            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new CarryMinigame(snap, gap, null);
                game.Build(panel, null);
                var item = ((RectTransform)panel.Find("Item")).anchoredPosition;
                var target = ((RectTransform)panel.Find("SafeSpot")).anchoredPosition;
                var top = (RectTransform)panel.Find("WallTop");
                var bottom = (RectTransform)panel.Find("WallBottom");
                float gapY = ((top.anchoredPosition.y - top.sizeDelta.y * 0.5f) + (bottom.anchoredPosition.y + bottom.sizeDelta.y * 0.5f)) * 0.5f;
                float nearX = 8f + 24f + 6f;
                float crossY = Mathf.Lerp(item.y, target.y, Mathf.InverseLerp(item.x, target.x, 0f));
                straightBlocked = Mathf.Abs(crossY - gapY) > (gap - 48f) * 0.5f;

                var path = throughGap
                    ? new[] { item, new Vector2(Mathf.Sign(item.x) * nearX, gapY), new Vector2(Mathf.Sign(target.x) * nearX, gapY), target }
                    : new[] { item, target };

                Vector2 pointer = item;
                game.Tick(Input(pointer, true, true));
                bool done = false;
                for (int i = 1; i < path.Length && !done; i++)
                {
                    while (Vector2.Distance(pointer, path[i]) > 0.01f)
                    {
                        pointer = Vector2.MoveTowards(pointer, path[i], 4f);
                        done = game.Tick(Input(pointer, true));
                        if (done) break;
                    }
                }
                if (!done) done = game.Tick(Input(pointer, false, false, true));
                return done;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ Metinler

        static void TestTexts(Language language)
        {
            var missing = new List<string>();
            void Need(string key)
            {
                if (!Texts.Has(key) || Texts.Get(key).StartsWith("[")) missing.Add(key);
            }

            foreach (ItemId id in Enum.GetValues(typeof(ItemId)))
                if (id != ItemId.None) Need($"item.{id}");
            foreach (BagItemId id in Enum.GetValues(typeof(BagItemId)))
                if (id != BagItemId.None) Need($"bag.{id}");
            foreach (FamilyMemberId id in Enum.GetValues(typeof(FamilyMemberId)))
            {
                if (id == FamilyMemberId.None) continue;
                Need($"family.{id}");
                Need("bubble.family_call." + id.ToString().ToLowerInvariant());
                Need("bubble.family_hit." + id.ToString().ToLowerInvariant());
            }
            Need("bubble.family_missing.firstaidkit");
            Need("bubble.family_missing.flashlight");
            foreach (var lesson in new[] { "ignored_foreshock", "duck_cover", "secure_items", "bag", "aftershock" })
            {
                Need($"wish.{lesson}");
                Need($"final.{lesson}");
            }
            foreach (var key in new[] { "wish.prefix", "wish.rewind", "prompt.continue", "minigame.lock.hint",
                         "minigame.carry.hint", "minigame.screw.hint" })
                Need(key);
            Check(missing.Count == 0, $"Metinler ({language}): dinamik anahtarlar tam" +
                                      (missing.Count > 0 ? " — eksik: " + string.Join(", ", missing) : ""));
        }

        // Her kaybetme sebebi ve eşya için keşke cümlesi ve bilgi satırı dolu; cümle "KEŞKE..." + devamı diye bölünür
        static void TestWishLines(Language language)
        {
            var split = typeof(WishScreen).GetMethod("SplitWish", BindingFlags.NonPublic | BindingFlags.Static);
            Check(split != null, "WishScreen.SplitWish bulundu");
            var problems = new List<string>();
            foreach (LoseReason reason in Enum.GetValues(typeof(LoseReason)))
            {
                if (reason == LoseReason.None) continue;
                foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
                {
                    string wish = WishLines.Wish(reason, item);
                    string info = WishLines.Info(reason);
                    if (string.IsNullOrEmpty(wish) || wish.Contains("[")) problems.Add($"{reason}/{item}: cümle '{wish}'");
                    if (string.IsNullOrEmpty(info) || info.Contains("[")) problems.Add($"{reason}: bilgi '{info}'");
                    if (split == null) continue;
                    var args = new object[] { wish, null, null };
                    split.Invoke(null, args);
                    var head = (string)args[1];
                    var rest = (string)args[2];
                    if (string.IsNullOrEmpty(head) || !head.EndsWith("...") || string.IsNullOrEmpty(rest) || !rest.StartsWith("..."))
                        problems.Add($"{reason}/{item}: bölünmedi ('{head}' + '{rest}')");
                }
            }
            Check(problems.Count == 0, $"Keşke cümleleri ({language}): hepsi dolu ve KEŞKE... + devamı" +
                                       (problems.Count > 0 ? " — " + string.Join("; ", problems.GetRange(0, Mathf.Min(5, problems.Count))) : ""));
            Debug.Log(Tag + $"Örnek ({language}): {WishLines.Wish(LoseReason.FamilyHitByItem, ItemId.Bookcase)}");
        }

        static void Check(bool condition, string description)
        {
            Debug.Log(Tag + (condition ? "GEÇTİ: " : "KALDI: ") + description);
            if (!condition) Failures.Add(description);
        }
    }
}
