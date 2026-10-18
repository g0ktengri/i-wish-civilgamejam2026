using System.IO;
using System.Linq;
using System.Reflection;
using Deprem.Bag;
using Deprem.Core;
using Deprem.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Loop.EditorTools
{
    // M24: HUD'ların görünüşünü oyunu açmadan görmek için. HUD bileşenlerini gerçek kodlarıyla kurar, örnek
    // durumla doldurur ve evin önizlemesinin (Logs/house_preview_game.png) üstüne 1920 x 1080 çizer:
    // Logs/hud_preview_rescue.png (hedef, ipucu, çanta, eklendi yazısı) ve Logs/hud_preview_spirit.png.
    // Batchmode'da -nographics OLMADAN çalışır:
    //   Unity.exe -batchmode -quit -projectPath . -executeMethod Deprem.Loop.EditorTools.HudPreview.Render
    public static class HudPreview
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Ana menünün arka plan görüntüsünü gerçek sahneden dener: Main.unity'deki kamerayı oda örtüleri açıkken ve
        // UiBackdrop.Capture gibi gizlenmişken çizer (Logs/menu_snapshot_covers.png, _clean.png). Menüde arka plan
        // siyah çıkıyordu (örtüler kapalıydı).
        public static void RenderMainSnapshot()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var cam = Camera.main ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (!cam)
            {
                Debug.LogError("[HudPreview] Main'de kamera yok");
                return;
            }
            // Edit modunda RoomVisibility.Instance yok (Awake çalışmadı): sahnedeki bileşenden
            var visibility = Object.FindFirstObjectByType<Deprem.Visual.RoomVisibility>();
            var covers = visibility ? visibility.Covers.ToList() : new System.Collections.Generic.List<Renderer>();

            Debug.Log($"[HudPreview] Oda örtüsü: {covers.Count}, kamera {cam.name} @ {cam.transform.position}");
            foreach (var (name, hide) in new[] { ("covers", false), ("clean", true) })
            {
                foreach (var r in covers) r.enabled = !hide;
                var target = new RenderTexture(384, 216, 24);
                var previous = cam.targetTexture;
                cam.targetTexture = target;
                cam.Render();
                cam.targetTexture = previous;
                RenderTexture.active = target;
                var output = new Texture2D(384, 216, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, 384, 216), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                float sum = 0f;
                foreach (var c in output.GetPixels()) sum += c.grayscale;
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes($"Logs/menu_snapshot_{name}.png", output.EncodeToPNG());
                Debug.Log($"[HudPreview] menu_snapshot_{name}: ortalama parlaklık {sum / (384 * 216):0.000}");
                Object.DestroyImmediate(output);
                target.Release();
            }
            foreach (var r in covers) r.enabled = true;
        }

        [MenuItem("Tools/Deprem/Berke: HUD Önizlemesi")]
        public static void Render()
        {
            RenderScene("rescue", root =>
            {
                // M27 okları için sahte hedefler: baba sağda ekran dışı, anne sol üstte ekran dışı, kardeş ekranda
                foreach (var (name, position) in new[] { ("Marker_Father", new Vector3(14f, 1f, 10f)),
                             ("Marker_Mother", new Vector3(-7f, 9f, 10f)), ("Marker_Sibling", new Vector3(-3f, -2f, 10f)) })
                {
                    var marker = new GameObject(name);
                    marker.transform.SetParent(root.transform, false);
                    marker.transform.position = position;
                }
                var sibling = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sibling.name = "Sibling";
                sibling.transform.SetParent(root.transform, false);
                sibling.transform.position = new Vector3(-3f, -2.4f, 10f);
                sibling.transform.localScale = new Vector3(0.6f, 1.1f, 1f);

                var prompt = root.AddComponent<PromptHud>();
                Call(prompt, "Awake");
                Call(prompt, "HandlePromptChanged", "prompt.duck_cover");

                var objective = root.AddComponent<ObjectiveHud>();
                Call(objective, "Awake");
                Call(objective, "HandlePhaseChanged", GamePhase.Rescue);

                GameState.AddBagItem(BagItemId.FirstAidKit);
                GameState.AddBagItem(BagItemId.Flashlight);
                var bag = root.AddComponent<BagHud>();
                Call(bag, "Awake"); // Awake çantadaki malzemelerle kurar
                Get<CanvasGroup>(bag, "group").alpha = 1f;
                var toast = Get<Text>(bag, "toastText");
                toast.text = Deprem.Localization.Texts.Format("bag.added",
                    $"<color={UiBuilder.Hex(UiBuilder.HudAccent)}>{Deprem.Localization.Texts.Get("bag.Flashlight")}</color>");
                ((RectTransform)toast.rectTransform.parent).sizeDelta = new Vector2(toast.preferredWidth + 32f, 36f);
                Get<CanvasGroup>(bag, "toastGroup").alpha = 1f;
            }, (root, camera) =>
            {
                var objective = root.GetComponent<ObjectiveHud>();
                var arrows = (System.Collections.IList)typeof(ObjectiveHud).GetField("arrows", Flags).GetValue(objective);
                var update = typeof(ObjectiveHud).GetMethod("UpdateArrow", Flags);
                foreach (var arrow in arrows) update.Invoke(objective, new[] { arrow, camera, (object)true, (object)0.3f });
            });

            // Kayıp anı dairesi (ışık 3D sahnede, burada yok): oyuncu odasındaki masanın yanına
            RenderScene("wish_focus", root =>
            {
                var wish = root.AddComponent<WishScreen>();
                Call(wish, "Awake");
                Get<CanvasGroup>(wish, "group").alpha = 1f;
                Get<Image>(wish, "background").color = Color.clear;
                var focus = Get<Image>(wish, "focus");
                focus.enabled = true;
                focus.color = new Color(0f, 0f, 0f, 1f);
                focus.rectTransform.anchoredPosition = new Vector2(-150f, -110f);
            });

            // Minigame'ler: eşyanın kendi kurulumuyla (MinigameHost.VariantFor), gerçek kodla. M66: mutfak dolapları
            // artık sabitlenemiyor (kilitleme önizlemesi kalktı). M61: öğretici yalnızca "_tutorial" önizlemelerinde
            foreach (Deprem.Core.MinigameType kind in System.Enum.GetValues(typeof(Deprem.Core.MinigameType)))
                PlayerPrefs.SetInt("tutorial.minigame." + kind, 1);
            foreach (var (name, type, id) in new[]
                     {
                         ("minigame_bookcase", Deprem.Core.MinigameType.ScrewTighten, ItemId.Bookcase),
                         ("minigame_mirror", Deprem.Core.MinigameType.CarryAndPlace, ItemId.StairMirror),
                         ("minigame_bedshelf", Deprem.Core.MinigameType.CarryAndPlace, ItemId.BedShelf),
                         ("minigame_tutorial_carry", Deprem.Core.MinigameType.CarryAndPlace, ItemId.BedFrame),
                         ("minigame_tutorial_screw", Deprem.Core.MinigameType.ScrewTighten, ItemId.TvUnit),
                         ("minigame_success", Deprem.Core.MinigameType.ScrewTighten, ItemId.ToyShelf),
                     })
            {
                RenderScene(name, root =>
                {
                    if (name.Contains("tutorial")) PlayerPrefs.DeleteKey("tutorial.minigame." + type);
                    else PlayerPrefs.SetInt("tutorial.minigame." + type, 1);

                    var host = root.AddComponent<Deprem.Minigames.MinigameHost>();
                    UsePrefabArt(host);
                    Call(host, "Awake");
                    var variant = typeof(Deprem.Minigames.MinigameHost).GetMethod("VariantFor", Flags).Invoke(host, new object[] { id });
                    string title = Deprem.Localization.Texts.Format("minigame.title", Deprem.Localization.Texts.Get($"item.{id}"));
                    // M37'den sonra OpenPanel'e isteğe bağlı parametreler eklendi: kalanlar varsayılan değerleriyle
                    var openPanel = typeof(Deprem.Minigames.MinigameHost).GetMethod("OpenPanel", Flags);
                    var parameters = openPanel.GetParameters();
                    var args = new object[parameters.Length];
                    var given = new object[] { type, null, variant, title, (System.Action<bool>)(_ => { }) };
                    for (int i = 0; i < args.Length; i++)
                        args[i] = i < given.Length ? given[i]
                            : parameters[i].HasDefaultValue ? parameters[i].DefaultValue
                            : parameters[i].ParameterType.IsValueType ? System.Activator.CreateInstance(parameters[i].ParameterType) : null;
                    openPanel.Invoke(host, args);
                    if (name == "minigame_success")
                    {
                        // M70: damga basıldıktan sonraki an (parlama sönmüş)
                        var stamp = Get<RectTransform>(host, "stamp");
                        Get<Text>(host, "stampText").text = Deprem.Localization.Texts.Get("minigame.stamp.secured");
                        stamp.gameObject.SetActive(true);
                        stamp.localRotation = Quaternion.Euler(0f, 0f, Get<float>(host, "stampAngle"));
                        Get<CanvasGroup>(host, "stampGroup").alpha = 1f;
                    }
                    // Önizlemede ana kamera yok: bulanık oda görüntüsünü ev önizlemesinden taklit et
                    const string house = "Logs/house_preview_game.png";
                    if (File.Exists(house))
                    {
                        var texture = new Texture2D(2, 2);
                        texture.LoadImage(File.ReadAllBytes(house));
                        var blur = new RenderTexture(96, 54, 0) { filterMode = FilterMode.Bilinear };
                        Graphics.Blit(texture, blur);
                        var snapshot = Get<RawImage>(host, "snapshot");
                        snapshot.texture = blur;
                        snapshot.color = new Color(0.34f, 0.45f, 0.68f);
                        snapshot.enabled = true;
                    }
                });
            }

            // Önizlemeler editörün PlayerPrefs'ine öğretici kaydı bırakmasın (Play'de öğretici yine görünsün)
            Deprem.Minigames.MinigameHost.ClearTutorials();

            // Çanta malzemesi: el feneri (ince uzun çizim ve çarpışma, kayan boşluk)
            RenderScene("minigame_flashlight", root =>
            {
                var host = root.AddComponent<Deprem.Minigames.MinigameHost>();
                UsePrefabArt(host);
                Call(host, "Awake");
                host.Open(Deprem.Core.MinigameType.CarryAndPlace, _ => { }, null,
                    Deprem.Localization.Texts.Get("bag.Flashlight"), "flashlight");
            });

            // Arayüz turu: ana menü ve duraklatma (bulanık arka plan ev önizlemesinden)
            RenderScene("menu_main", root =>
            {
                var menu = root.AddComponent<MainMenu>();
                typeof(MainMenu).GetField("teamLogo", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/takim_logo.png"));
                typeof(MainMenu).GetField("logoSprite", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/logo.png"));
                Call(menu, "Awake");
                var screens = typeof(MainMenu).GetNestedType("Screen", BindingFlags.NonPublic);
                Call(menu, "Show", System.Enum.Parse(screens, "Main"));
                var backdrop = Get<UiBackdrop>(menu, "backdrop");
                if (backdrop) backdrop.SetTexture(HouseBlur());
            });
            // Arayüz turu 4-5: hareketli arka planın bir anı (yakınlaşmış, yağmur)
            RenderScene("menu_motion", root =>
            {
                var menu = root.AddComponent<MainMenu>();
                typeof(MainMenu).GetField("teamLogo", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/takim_logo.png"));
                typeof(MainMenu).GetField("logoSprite", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/logo.png"));
                Call(menu, "Awake");
                var screens = typeof(MainMenu).GetNestedType("Screen", BindingFlags.NonPublic);
                Call(menu, "Show", System.Enum.Parse(screens, "Main"));
                var backdrop = Get<UiBackdrop>(menu, "backdrop");
                if (!backdrop) return;
                backdrop.SetTexture(HouseBlur());
                const float t = 30f;
                Call(backdrop, "Animate", t, 0.016f);
                // Arayüz turu 5: yağmur (elektrik oynaması bu anda kapalı)
                typeof(UiBackdrop).GetField("nextOutage", Flags).SetValue(backdrop, t + 100f);
                Call(backdrop, "AnimateMotifs", t, 0.016f);
            });

            // Arayüz turu 3: ana menünün emeği geçenler ve içerik uyarısı ekranları
            foreach (var (name, screen) in new[] { ("menu_credits", "Credits"), ("menu_warning", "Warning") })
            {
                RenderScene(name, root =>
                {
                    var menu = root.AddComponent<MainMenu>();
                    typeof(MainMenu).GetField("teamLogo", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/takim_logo.png"));
                    typeof(MainMenu).GetField("headphonesSprite", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/kulaklik.png"));
                    typeof(MainMenu).GetField("headphonesWavesSprite", Flags).SetValue(menu, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/kulaklik_dalga.png"));
                    Call(menu, "Awake");
                    var screens = typeof(MainMenu).GetNestedType("Screen", BindingFlags.NonPublic);
                    Call(menu, "Show", System.Enum.Parse(screens, screen));
                    var backdrop = Get<UiBackdrop>(menu, "backdrop");
                    if (backdrop) backdrop.SetTexture(HouseBlur());
                });
            }
            RenderScene("menu_pause", root =>
            {
                var pause = root.AddComponent<PauseMenu>();
                Call(pause, "Awake");
                Call(pause, "SetPaused", true);
                Get<UiBackdrop>(pause, "backdrop").SetTexture(HouseBlur());
            });

            // M32: ayarlar menüsü
            RenderScene("settings", root =>
            {
                var settings = root.AddComponent<SettingsMenu>();
                Call(settings, "Awake");
                settings.Open(null);
            });

            // M47: final ekranının son hali (ilk iki cümle henüz keşke, gerisi bugüne dönmüş; büyük cümle ve AFAD)
            RenderScene("final", root =>
            {
                var final = root.AddComponent<FinalScreen>();
                Call(final, "Awake");
                Get<CanvasGroup>(final, "group").alpha = 1f;
                var addLine = typeof(FinalScreen).GetMethod("AddLine", Flags);
                var said = new[]
                {
                    (LoseReason.IgnoredForeshock, ItemId.None), (LoseReason.NoDuckCoverHold, ItemId.None),
                    (LoseReason.FamilyHitByItem, ItemId.Bookcase), (LoseReason.MissingBagItem, ItemId.None),
                    (LoseReason.AftershockInjury, ItemId.None),
                };
                for (int i = 0; i < said.Length; i++)
                {
                    bool turned = i >= 2;
                    var text = turned ? WishLines.Advice(said[i].Item1, said[i].Item2) : WishLines.Wish(said[i].Item1, said[i].Item2);
                    var line = (Text)addLine.Invoke(final, new object[] { text });
                    line.color = turned ? new Color(1f, 0.82f, 0.4f) : new Color(0.62f, 0.6f, 0.58f);
                }
                Get<UiTitle>(final, "closingText").Text = Deprem.Localization.Texts.Get("final.closing");
                Get<RectTransform>(final, "closingArea").GetComponent<CanvasGroup>().alpha = 1f;
                Get<Text>(final, "subText").text = Deprem.Localization.Texts.Get("final.closing_sub");
                var site = Get<Text>(final, "siteText");
                site.text = Deprem.Localization.Texts.Get("final.site");
                site.color = new Color(0.96f, 0.92f, 0.84f, 0.6f);
                Get<Text>(final, "hintText").text = Deprem.Localization.Texts.Get("prompt.main_menu");
            });

            // Outro (Mehmet): mezarlıkta yürürken gökyüzündeki farkındalık cümlesi (arka plan OutroPreview görüntüsü)
            backgroundOverride = "Logs/outro_1_yuruyus.png";
            RenderScene("final_awareness", root =>
            {
                var final = root.AddComponent<FinalScreen>();
                Call(final, "Awake");
                Get<CanvasGroup>(final, "awarenessGroup").alpha = 1f;
                var line = Get<Text>(final, "awarenessLine");
                line.text = WishLines.Advice(LoseReason.NoDuckCoverHold, ItemId.None);
                line.color = new Color(1f, 0.82f, 0.4f);
            });
            backgroundOverride = null;

            // Outro bitince: geçmiş depremleri anma, liste kayarken
            RenderScene("final_memorial", root =>
            {
                var final = root.AddComponent<FinalScreen>();
                Call(final, "Awake");
                Get<CanvasGroup>(final, "group").alpha = 1f;
                var title = Get<Text>(final, "memorialTitle");
                title.text = Deprem.Localization.Texts.Get("memorial.title");
                title.color = new Color(0.96f, 0.92f, 0.84f);
                Call(final, "BuildMemorialList");
            }, (root, camera) =>
            {
                var list = Get<RectTransform>(root.GetComponent<FinalScreen>(), "memorialList");
                list.anchoredPosition = new Vector2(0f, 160f);
            });

            // Outrodan sonra kapanış: ekranın ortasında, büyük, sığacak boyda
            RenderScene("final_closing_outro", root =>
            {
                var final = root.AddComponent<FinalScreen>();
                Call(final, "Awake");
                Get<CanvasGroup>(final, "group").alpha = 1f;
                Call(final, "CenterClosing");
                Get<UiTitle>(final, "closingText").Text = Deprem.Localization.Texts.Get("final.closing");
                Get<RectTransform>(final, "closingArea").GetComponent<CanvasGroup>().alpha = 1f;
                var sub = Get<Text>(final, "subText");
                sub.text = Deprem.Localization.Texts.Get("final.closing_sub");
                sub.color = new Color(1f, 0.82f, 0.4f);
                var site = Get<Text>(final, "siteText");
                site.text = Deprem.Localization.Texts.Get("final.site");
                site.color = new Color(0.96f, 0.92f, 0.84f, 0.6f);
                Get<Text>(final, "hintText").text = Deprem.Localization.Texts.Get("prompt.main_menu");
            });

            // M56, M57: sinema bantları ve "geç" (Cinematic sınıfı yokken elle açılır, halka yarı dolu)
            RenderScene("cinematic", root =>
            {
                var hud = root.AddComponent<CinematicHud>();
                Call(hud, "Awake");
                Get<RectTransform>(hud, "top").gameObject.SetActive(true);
                Get<RectTransform>(hud, "top").sizeDelta = new Vector2(0f, 58f);
                Get<RectTransform>(hud, "bottom").gameObject.SetActive(true);
                Get<RectTransform>(hud, "bottom").sizeDelta = new Vector2(0f, 58f);
                Get<RectTransform>(hud, "skipRoot").gameObject.SetActive(true);
                Get<UiPrompt>(hud, "skipPrompt").Set(Deprem.Localization.Texts.Get("cinematic.skip"));
                Get<Image>(hud, "ring").fillAmount = 0.6f;
            });

            // M40: tuş değiştirme ekranı
            RenderScene("settings_controls", root =>
            {
                var settings = root.AddComponent<SettingsMenu>();
                Call(settings, "Awake");
                settings.OpenControls(null);
            });

            // Keşke çatlağı: ilk keşke (küçük) ve beşinci keşke (en büyük)
            foreach (var (name, growth) in new[] { ("wish_crack_1", 0f), ("wish_crack_5", 1f) })
            {
                RenderScene(name, root =>
                {
                    var wish = root.AddComponent<WishScreen>();
                    Call(wish, "Awake");
                    Get<CanvasGroup>(wish, "group").alpha = 1f;
                    Get<Image>(wish, "background").color = Color.black;
                    var text = Get<Text>(wish, "wishText");
                    text.text = "...ÇÖK-KAPAN-TUTUN YAPSAYDIM";
                    float textWidth = Mathf.Clamp(text.preferredWidth, 220f, 720f);
                    Call(wish, "BuildCrack", textWidth * Mathf.Lerp(0.3f, 1.15f, growth), Mathf.Lerp(2f, 5f, growth),
                        Mathf.Lerp(0.55f, 1.5f, growth), growth);
                    Call(wish, "ShowCrack", 9999);
                });
            }

            RenderScene("spirit", root =>
            {
                var spirit = root.AddComponent<SpiritHud>();
                Call(spirit, "Awake");
                Get<CanvasGroup>(spirit, "group").alpha = 1f;
                Get<Text>(spirit, "timerText").text = "0:42";
                typeof(SpiritHud).GetField("shownPrompt", Flags).SetValue(spirit, "prompt.secure");
                typeof(SpiritHud).GetField("shownCarry", Flags).SetValue(spirit, BagItemId.Flashlight);
                Call(spirit, "RefreshStaticTexts");

                // Jüri temposu: sol üstteki kareli harita. Önizlemede Main açık değil: odalar Main'deki değerlerle
                var layout = new GameObject("PreviewRooms").AddComponent<Deprem.Visual.RoomVisibility>();
                layout.transform.SetParent(root.transform, false);
                Deprem.Visual.RoomVisibility.Area Room(string key, float x0, float x1, float floor, float ceiling) =>
                    new Deprem.Visual.RoomVisibility.Area { key = key, x0 = x0, x1 = x1, floorY = floor, ceilingY = ceiling };
                layout.Configure(new[]
                {
                    Room("oyuncu", 0f, 6f, 0f, 3.5f), Room("koridor", 6f, 18f, 0f, 3.5f), Room("salon", 18f, 27f, 0f, 3.5f),
                    Room("mutfak", 27f, 32.5f, 0f, 3.5f), Room("kardes", 0f, 7f, 3.75f, 7f), Room("sahanlik", 7f, 18f, 3.75f, 7f),
                    Room("ebeveyn", 18f, 27f, 3.75f, 7f),
                }, new Deprem.Visual.RoomVisibility.Link[0], new Deprem.Player.HouseDoor[0]);
                var previewPlayer = new GameObject("Player");
                previewPlayer.transform.SetParent(root.transform, false);
                previewPlayer.transform.position = new Vector3(3.2f, 0f, 0f);
                var map = root.AddComponent<MiniMap>();
                Call(map, "Awake");
                typeof(MiniMap).GetMethod("Refresh", Flags).Invoke(map, new object[] { GamePhase.Spirit });

                GameState.AddBagItem(BagItemId.FirstAidKit);
                var bag = root.AddComponent<BagHud>();
                Call(bag, "Awake"); // Awake çantadaki malzemelerle kurar
                Get<CanvasGroup>(bag, "group").alpha = 1f;
            });
        }

        // Ruh (ruh_N) ve ölmenin son karesi (oyuncu_olme_N), oyundaki ölçek ve pivotla: Logs/character_preview.png
        [MenuItem("Tools/Deprem/Berke: Ruh ve Ölüm Önizlemesi")]
        public static void RenderCharacters()
        {
            Texture2D[] Load(string prefix) => AssetDatabase.FindAssets(prefix + "_ t:Texture2D", new[] { "Assets/Art/Characters" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(path), "^" + prefix + "_\\d+$"))
                .OrderBy(path => int.Parse(Path.GetFileNameWithoutExtension(path).Substring(prefix.Length + 1)))
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>).ToArray();

            const int Width = 1280, Height = 720;
            var root = new GameObject("CharacterPreview");
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.15f, 0.18f);
                camera.orthographic = true;
                camera.orthographicSize = 1.3f;
                camera.transform.position = new Vector3(0f, 1.1f, -5f);
                camera.targetTexture = target;

                var spiritFrames = Load("ruh");
                Debug.Log("[HudPreview] Ruh kareleri: " + string.Join(", ", spiritFrames.Select(t => t.name)));
                var spirit = Deprem.Spirit.SpiritVisual.Create(root.transform, spiritFrames);
                spirit.transform.position = new Vector3(-1.4f, 1.1f, 0f);

                var death = Load("oyuncu_olme");
                if (death.Length > 0)
                {
                    // Oyuncu kökü x = 1,2; görsel ölme kaydırmasıyla (PlayerVisual.deathOffsetX), kare 2 x 2 m
                    var last = death[death.Length - 1];
                    var sprite = Sprite.Create(last, new Rect(0, 0, last.width, last.height), new Vector2((64f - 9f) / 128f, 2f / 128f), 64f, 0, SpriteMeshType.FullRect);
                    var go = new GameObject("Death");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(1.2f - 34f / 64f, 0f, 0f);
                    go.AddComponent<SpriteRenderer>().sprite = sprite;
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = new Vector3(0f, -0.03f, 0.1f);
                floor.transform.localScale = new Vector3(8f, 0.06f, 1f);

                camera.Render();
                RenderTexture.active = target;
                var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes("Logs/character_preview.png", output.EncodeToPNG());
                Object.DestroyImmediate(output);
                Debug.Log("[HudPreview] Kaydedildi: Logs/character_preview.png");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        static void RenderScene(string name, System.Action<GameObject> build)
        {
            RenderScene(name, build, null);
        }

        // afterLayout: canvas'lar önizleme kamerasına bağlandıktan sonra (ekran konumu hesaplayanlar için)
        static void RenderScene(string name, System.Action<GameObject> build, System.Action<GameObject, Camera> afterLayout)
        {
            const int Width = 1920, Height = 1080;
            GameState.ResetAll();
            var root = new GameObject("HudPreview");
            var camera = new GameObject("HudPreviewCamera").AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24);
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
                camera.orthographic = true;
                camera.targetTexture = target;

                // Arka plan: evin önizlemesi (yoksa düz gri)
                var background = UiBuilder.CreateCanvas(root.transform, "Background", -100);
                var image = UiBuilder.CreateChild<RawImage>(background.transform, "House");
                UiBuilder.Stretch(image.rectTransform, Vector2.zero, Vector2.one);
                string house = backgroundOverride ?? "Logs/house_preview_game.png";
                if (File.Exists(house))
                {
                    var texture = new Texture2D(2, 2);
                    texture.LoadImage(File.ReadAllBytes(house));
                    image.texture = texture;
                }
                else image.color = camera.backgroundColor;

                build(root);

                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f + canvas.sortingOrder * -0.001f;
                }
                Canvas.ForceUpdateCanvases();
                if (afterLayout != null)
                {
                    afterLayout(root, camera);
                    Canvas.ForceUpdateCanvases();
                }
                camera.Render();

                RenderTexture.active = target;
                var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                output.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                string path = $"Logs/hud_preview_{name}.png";
                File.WriteAllBytes(path, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                Debug.Log($"[HudPreview] Kaydedildi: {path}");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(root);
                GameState.ResetAll();
            }
        }

        // Ev önizlemesinin bulanık hali (menü arka planı önizlemede kamera olmadan)
        static Texture HouseBlur()
        {
            const string house = "Logs/house_preview_game.png";
            if (!File.Exists(house)) return null;
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(house));
            var blur = new RenderTexture(96, 54, 0) { filterMode = FilterMode.Bilinear };
            Graphics.Blit(texture, blur);
            return blur;
        }

        // Minigame önizlemesi oyundaki gibi ekip görselleriyle (BerkeSystems prefab'ındaki art): Panel2 kutusu vb.
        static void UsePrefabArt(Deprem.Minigames.MinigameHost host)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Berke/BerkeSystems.prefab");
            var source = prefab ? prefab.GetComponent<Deprem.Minigames.MinigameHost>() : null;
            var field = typeof(Deprem.Minigames.MinigameHost).GetField("art", Flags);
            if (source && field != null) field.SetValue(host, field.GetValue(source));
        }

        static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Flags).Invoke(target, args);

        static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);

        // Mehmet: outro önizlemesi evin değil mezarlığın üstünde
        static string backgroundOverride;
    }
}
