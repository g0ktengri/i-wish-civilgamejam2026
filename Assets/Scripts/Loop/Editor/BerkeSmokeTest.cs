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
                    TestCinematicHud();
                    TestWorldPrompts();
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

        // ------------------------------------------------------------------ Sinematik (M56)

        // Intro siyah ekranda kalıyordu: sinematik HUD'ları gizlerken aynı objedeki bütün kanvasları (PixelScreen'in
        // oyun görüntüsü dahil) kapatıyordu. Sadece dört HUD kanvası kapanmalı, diğerleri açık kalmalı.
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static void TestCinematicHud()
        {
            var root = new GameObject("CinematicTest");
            try
            {
                var huds = new Component[]
                {
                    root.AddComponent<PromptHud>(), root.AddComponent<ObjectiveHud>(), root.AddComponent<BagHud>(),
                    root.AddComponent<SpiritHud>(),
                };
                foreach (var hud in huds) hud.GetType().GetMethod("Awake", Flags)?.Invoke(hud, null);
                var other = new GameObject("PixelScreenCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                other.transform.SetParent(root.transform, false);
                var cinematic = root.AddComponent<CinematicHud>();
                cinematic.GetType().GetMethod("Awake", Flags)?.Invoke(cinematic, null);

                typeof(CinematicHud).GetMethod("SetHudHidden", Flags).Invoke(cinematic, new object[] { true });
                int hiddenHuds = 0, visibleOthers = 0, others = 0;
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    bool hud = canvas.name == "PromptCanvas" || canvas.name == "ObjectiveCanvas" ||
                               canvas.name == "BagHudCanvas" || canvas.name == "SpiritHudCanvas";
                    if (hud && !canvas.enabled) hiddenHuds++;
                    if (!hud)
                    {
                        others++;
                        if (canvas.enabled) visibleOthers++;
                    }
                }
                Check(hiddenHuds == 4, $"Sinematik: dört HUD kanvası gizli ({hiddenHuds}/4)");
                Check(others > 0 && visibleOthers == others, $"Sinematik: diğer kanvaslar açık ({visibleOthers}/{others}; PixelScreen siyah kalmasın)");
                typeof(CinematicHud).GetMethod("SetHudHidden", Flags).Invoke(cinematic, new object[] { false });
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ------------------------------------------------------------------ İpuçları objenin üstünde (M46)

        // Konum verilince "F" ipucu alttaki yerine hedefin üstündeki küçük ipucunda çıkmalı (oyuncu ve ruh)
        static void TestWorldPrompts()
        {
            var cam = new GameObject("PromptTestCamera", typeof(Camera));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 1f, -10f);
            var root = new GameObject("PromptTest");
            var playerAnchor = typeof(Deprem.Player.PlayerInteractor).GetProperty("PromptAnchor");
            var spiritAnchor = typeof(Deprem.Spirit.SpiritInteractor).GetProperty("PromptAnchor");
            try
            {
                var hud = root.AddComponent<PromptHud>();
                hud.GetType().GetMethod("Awake", Flags).Invoke(hud, null);
                playerAnchor.SetValue(null, (Vector3?)new Vector3(1f, 2f, 0f));
                hud.GetType().GetMethod("HandlePromptChanged", Flags).Invoke(hud, new object[] { "prompt.interact" });
                var world = (UiPrompt)hud.GetType().GetField("worldPrompt", Flags).GetValue(hud);
                var bottom = (UiPrompt)hud.GetType().GetField("prompt", Flags).GetValue(hud);
                Check(world.gameObject.activeSelf && !bottom.gameObject.activeSelf, "İpucu: oyuncunun F ipucu hedefin üstünde");

                var spiritHud = root.AddComponent<SpiritHud>();
                spiritHud.GetType().GetMethod("Awake", Flags).Invoke(spiritHud, null);
                spiritAnchor.SetValue(null, (Vector3?)new Vector3(-1f, 1.5f, 0f));
                spiritHud.GetType().GetField("promptText", Flags).SetValue(spiritHud, "F: Sabitle");
                spiritHud.GetType().GetMethod("PlacePrompt", Flags).Invoke(spiritHud, null);
                var spiritWorld = (UiPrompt)spiritHud.GetType().GetField("worldPrompt", Flags).GetValue(spiritHud);
                var spiritBottom = (UiPrompt)spiritHud.GetType().GetField("prompt", Flags).GetValue(spiritHud);
                Check(spiritWorld.gameObject.activeSelf && !spiritBottom.gameObject.activeSelf, "İpucu: ruhun F ipucu hedefin üstünde");
            }
            finally
            {
                playerAnchor.SetValue(null, null);
                spiritAnchor.SetValue(null, null);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cam);
            }
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

        // Kilit: iğne yeşil alandayken tıklayan oyuncu bitirebilmeli (alan isabetten sonra kayar). Play testi 8 hatası:
        // ıskalayınca alan ve hız geri gelmiyordu; isabet + ıskalama sonrası alan baştaki genişlikte olmalı.
        static void TestLock(SerializedObject host, Vector2 panelSize)
        {
            float speed = host.FindProperty("lockCyclesPerSecond").floatValue;
            float zone = host.FindProperty("lockZoneWidth").floatValue;
            int hits = host.FindProperty("lockRequiredHits").intValue;
            float shrink = host.FindProperty("lockShrinkPerHit").floatValue;
            float speedUp = host.FindProperty("lockSpeedUpPerHit").floatValue;
            var variant = new MinigameVariant { Seed = 7, LockDrift = host.FindProperty("lockDriftPerHit").floatValue };

            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new LockMinigame(speed, zone, hits, shrink, speedUp);
                game.Build(panel, null, variant);
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
                    bool press = !hitOnce ? inside : !missedAfterHit ? farAway : inside;
                    done = game.Tick(Input(Vector2.zero, press, press));
                    if (press && !hitOnce) hitOnce = true;
                    else if (press && !missedAfterHit)
                    {
                        missedAfterHit = true;
                        widthAfterMiss = zoneRect.sizeDelta.x;
                    }
                    if (press) game.Tick(Input(Vector2.zero, false)); // aynı kareye ikinci tık sayılmasın
                }
                Check(Mathf.Abs(widthAfterMiss - startWidth) < 0.5f,
                    $"Kilit: isabet + ıskalama sonrası alan baştaki gibi ({widthAfterMiss:F1} / {startWidth:F1} px)");
                Check(done, $"Kilit: yeşilde tıklayan oyuncu kayan alanla da bitiriyor ({t:F1} sn, ıskalama dahil)");

                // Son isabetin zaman penceresi insan için yeterli mi (~0,12 sn üstü)
                float barWidth = panelSize.x * 0.78f;
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

        // Vida: her düzen insan hızında (1,5 tur/sn) bitirilebilmeli; çok hızlı çevirmek (5 tur/sn) uç kaydığı için
        // bitirmemeli; ters yönde çevirmek ilerletmemeli
        static void TestScrew(SerializedObject host, Vector2 panelSize)
        {
            float turns = host.FindProperty("screwTurnsPerScrew").floatValue;
            float reverse = host.FindProperty("screwReversePenalty").floatValue;
            float minRadius = host.FindProperty("screwMinRadius").floatValue;
            float maxSpeed = host.FindProperty("screwMaxTurnsPerSecond").floatValue;

            foreach (ScrewLayout layout in Enum.GetValues(typeof(ScrewLayout)))
            {
                var variant = new MinigameVariant { Seed = 11 + (int)layout, Screws = layout };
                bool done = PlayScrew(panelSize, turns, reverse, minRadius, maxSpeed, variant, 9f, out int count, out float seconds);
                Check(done, $"Vida ({layout}, {count} vida): saat yönünde düzgün çeviren bitiriyor ({seconds:F1} sn)");
            }
            var fast = new MinigameVariant { Seed = 3, Screws = ScrewLayout.Horizontal2 };
            bool fastDone = PlayScrew(panelSize, turns, reverse, minRadius, maxSpeed, fast, 30f, out _, out _);
            if (maxSpeed > 0f) Check(!fastDone, $"Vida: çok hızlı çevirince (5 tur/sn > {maxSpeed:F1}) uç kayıyor, bitmiyor");
            else Check(fastDone, "Vida: kayma kapalı, hızlı çevirince de uç kopmuyor (play testi 16)");
            bool reverseDone = PlayScrew(panelSize, turns, reverse, minRadius, maxSpeed, fast, -9f, out _, out _);
            Check(!reverseDone, "Vida: ters yönde çevirmek bitirmiyor");
        }

        // degreesPerFrame > 0 saat yönü. Her vida için yetecek kadar kare çevirir, bırakır, sonrakine geçer.
        static bool PlayScrew(Vector2 panelSize, float turns, float reverse, float minRadius, float maxSpeed,
            MinigameVariant variant, float degreesPerFrame, out int count, out float seconds)
        {
            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new ScrewMinigame(turns, reverse, minRadius, maxSpeed);
                game.Build(panel, null, variant);
                var screws = new List<Vector2>();
                foreach (Transform child in panel)
                    if (child.name == "Screw") screws.Add(((RectTransform)child).anchoredPosition);
                count = screws.Count;
                var driver = (RectTransform)panel.Find("Screwdriver");

                // Boştaki uç ile fare arasındaki fark
                game.Tick(Input(Vector2.zero, false));
                Vector2 offset = -driver.anchoredPosition;
                int framesPerScrew = Mathf.CeilToInt(turns * 360f / Mathf.Abs(degreesPerFrame)) + 10;

                bool done = false;
                seconds = 0f;
                foreach (var screw in screws)
                {
                    Vector2 grab = screw + offset;
                    game.Tick(Input(grab, true, true));
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    for (int frame = 0; frame < framesPerScrew && !done; frame++, seconds += Dt)
                    {
                        angle -= degreesPerFrame * Mathf.Deg2Rad;
                        done = game.Tick(Input(screw + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * offset.magnitude, true));
                    }
                    if (!done) game.Tick(Input(grab, false, false, true));
                    seconds += 0.4f; // sonraki vidaya gitme payı
                }
                return done;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Taşı-yerleştir: üç çeşit (tek duvar, kayan boşluk, iki duvar) her rastgele yerleşimde boşluklardan geçen
        // oyuncuyla bitmeli. Tek duvarda, düz çizgisi duvara çarpan yerleşimde dümdüz giden bitirememeli.
        static void TestCarry(SerializedObject host, Vector2 panelSize)
        {
            float snap = host.FindProperty("carrySnapRadius").floatValue;
            float gap = host.FindProperty("carryGapHeight").floatValue;
            const int Runs = 30;
            var state = UnityEngine.Random.state;
            foreach (CarryMode mode in Enum.GetValues(typeof(CarryMode)))
            {
                int passed = 0, cheated = 0, blocked = 0;
                float slowest = 0f;
                var kinds = new[] { "box", "shelf", "mirror", "frame", "firstaid", "flashlight", "water", "whistle" };
                for (int run = 0; run < Runs; run++)
                {
                    // Her eşya çeşidi kendi boyu ve çarpışmasıyla (M26)
                    var variant = new MinigameVariant { Seed = 1000 + run, Carry = mode, CarryKind = kinds[run % kinds.Length] };
                    if (PlayCarry(panelSize, snap, gap, variant, true, out _, out float seconds)) passed++;
                    slowest = Mathf.Max(slowest, seconds);
                    if (mode != CarryMode.Single) continue;
                    bool straightDone = PlayCarry(panelSize, snap, gap, variant, false, out bool straightBlocked, out _);
                    if (!straightBlocked) continue;
                    blocked++;
                    if (straightDone) cheated++;
                }
                Check(passed == Runs, $"Taşı-yerleştir ({mode}): boşluktan geçen her yerleşimde bitiriyor ({passed}/{Runs}, en uzun {slowest:F1} sn)");
                if (mode == CarryMode.Single)
                    Check(blocked > 0 && cheated == 0, $"Taşı-yerleştir: duvarın içinden düz giden bitiremiyor ({cheated}/{blocked} duvara çarpan yerleşim)");
            }
            UnityEngine.Random.state = state;
        }

        static bool PlayCarry(Vector2 panelSize, float snap, float gap, MinigameVariant variant, bool throughGap,
            out bool straightBlocked, out float seconds)
        {
            straightBlocked = false;
            seconds = 0f;
            var panel = CreatePanel(panelSize, out var root);
            try
            {
                var game = new CarryMinigame(snap, gap, null);
                game.Build(panel, null, variant);
                var itemRect = (RectTransform)panel.Find("Item");
                var item = itemRect.anchoredPosition;
                var itemSize = itemRect.sizeDelta;
                var target = ((RectTransform)panel.Find("SafeSpot")).anchoredPosition;
                var walls = new List<(RectTransform top, RectTransform bottom)>();
                for (int i = 0; panel.Find($"Wall{i}Top"); i++)
                    walls.Add(((RectTransform)panel.Find($"Wall{i}Top"), (RectTransform)panel.Find($"Wall{i}Bottom")));
                float GapY(int i) => ((walls[i].top.anchoredPosition.y - walls[i].top.sizeDelta.y * 0.5f) +
                                      (walls[i].bottom.anchoredPosition.y + walls[i].bottom.sizeDelta.y * 0.5f)) * 0.5f;
                float WallX(int i) => walls[i].top.anchoredPosition.x;
                float Clearance = 8f + itemSize.x * 0.5f + 6f;

                if (!throughGap)
                {
                    float crossY = Mathf.Lerp(item.y, target.y, Mathf.InverseLerp(item.x, target.x, WallX(0)));
                    straightBlocked = Mathf.Abs(crossY - GapY(0)) > (gap - itemSize.y) * 0.5f;
                }

                Vector2 pointer = item;
                float elapsed = 0f;
                bool done = game.Tick(Input(pointer, true, true));
                bool Step(Vector2 to, float speed)
                {
                    pointer = Vector2.MoveTowards(pointer, to, speed);
                    elapsed += Dt;
                    return done = game.Tick(Input(pointer, true));
                }

                if (throughGap)
                {
                    // Duvarları eşyanın tarafından hedefe doğru sırayla geç
                    var order = new List<int>();
                    for (int i = 0; i < walls.Count; i++) order.Add(i);
                    order.Sort((a, b) => (WallX(a) * Mathf.Sign(item.x)).CompareTo(WallX(b) * Mathf.Sign(item.x)) * -1);
                    foreach (int i in order)
                    {
                        float side = Mathf.Sign(pointer.x - WallX(i));
                        // Duvarın önüne gel, boşluğa hizalan (kayan boşluğu takip eder), sonra boşluktan geç
                        while (!done && Mathf.Abs(pointer.x - (WallX(i) + side * Clearance)) > 0.01f)
                            Step(new Vector2(WallX(i) + side * Clearance, pointer.y), 4f);
                        for (int guard = 0; !done && guard < 600 && Mathf.Abs(pointer.y - GapY(i)) > 1.5f; guard++)
                            Step(new Vector2(pointer.x, GapY(i)), 4f);
                        float exitX = WallX(i) - side * Clearance;
                        for (int guard = 0; !done && guard < 200 && Mathf.Abs(pointer.x - exitX) > 0.01f; guard++)
                            Step(new Vector2(exitX, GapY(i)), 8f);
                    }
                }
                for (int guard = 0; !done && guard < 600 && Vector2.Distance(pointer, target) > 0.01f; guard++)
                    Step(target, 4f);
                if (!done) done = game.Tick(Input(pointer, false, false, true));
                seconds = elapsed;
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
                Need($"advice.{lesson}"); // M47: final ekranında bugüne dönmüş hali
            }
            foreach (var key in new[] { "advice.secure_item", "final.closing", "final.closing_sub", "final.site",
                         "controls.header.spirit", "controls.reset", "controls.reset_done", "menu.continue" })
                Need(key);
            foreach (var key in new[] { "wish.prefix", "wish.rewind", "prompt.continue", "minigame.lock.hint",
                         "minigame.carry.hint", "minigame.screw.hint" })
                Need(key);
            // Play testi 32: öğretici devam ipucu (M67), bitiş damgası (M70), çantaya götür (M71)
            foreach (var key in new[] { "minigame.tutorial.continue", "minigame.stamp.secured", "minigame.stamp.taken",
                         "spirit.carry_to_bag" })
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
                    string advice = WishLines.Advice(reason, item);
                    if (string.IsNullOrEmpty(advice) || advice.Contains("[")) problems.Add($"{reason}/{item}: öğüt '{advice}'");
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
