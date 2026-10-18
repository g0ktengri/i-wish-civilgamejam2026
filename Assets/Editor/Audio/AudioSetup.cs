using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Deprem.Audio.SoundSynth;

namespace Deprem.Audio
{
    /// <summary>
    /// Ses sistemi kurulumu (GDD ses tablosu, ENTEGRASYON Play testi 13):
    ///   1. Her anahtarın sesini kodla üretir: Assets/Generated/Audio/&lt;anahtar&gt;[_n].wav (sabit tohum, aynı dosya).
    ///   2. Assets/Audio/&lt;anahtar&gt;*.wav|ogg|mp3 varsa (hazır ses) o anahtar için onları kullanır.
    ///   3. SoundBank'ı ve Assets/Prefabs/Mehmet/AudioSystem.prefab'ı kurar, sahneye koyar.
    /// </summary>
    internal static class AudioSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Mehmet/AudioSystem.prefab";
        private const string GeneratedFolder = "Assets/Generated/Audio";
        private const string OverrideFolder = "Assets/Audio";
        private const string BankPath = GeneratedFolder + "/SoundBank.asset";
        private const string SystemName = "AudioSystem";

        private sealed class Recipe
        {
            public string key;
            public int variants = 1;
            public float volume = 1f;
            public Vector2 pitch = Vector2.one;
            public Func<SoundSynth, float[]> make;
        }

        [MenuItem("Tools/Deprem/Sesleri Üret ve Ses Sistemini Kur (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            SetupInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[AudioSetup] Ses sistemi kuruldu: " + scene.name);
        }

        public static GameObject SetupInScene(Scene scene)
        {
            var bank = BuildBank();
            var root = new GameObject(SystemName);
            var director = root.AddComponent<AudioDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("bank").objectReferenceValue = bank;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == SystemName).ToList())
                UnityEngine.Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = SystemName;
            return instance;
        }

        /// <summary>Sesleri üretir (değişmeyen dosyaya dokunmaz) ve bankayı kurar</summary>
        public static SoundBank BuildBank()
        {
            Directory.CreateDirectory(GeneratedFolder);
            var written = new List<string>();
            var entries = new List<SoundBank.Entry>();
            foreach (var recipe in Recipes())
            {
                var paths = new List<string>();
                for (int v = 0; v < recipe.variants; v++)
                {
                    string path = $"{GeneratedFolder}/{recipe.key}{(v == 0 ? "" : "_" + (v + 1))}.wav";
                    var synth = new SoundSynth(StableSeed(recipe.key) + v * 7919);
                    if (WriteIfChanged(path, recipe.make(synth))) written.Add(path);
                    paths.Add(path);
                }
                entries.Add(new SoundBank.Entry { key = recipe.key, volume = recipe.volume, pitch = recipe.pitch });
            }

            AssetDatabase.Refresh();
            foreach (var path in written) ConfigureImporter(path);

            // Klipler içe aktarıldıktan sonra bağlanır; Assets/Audio'daki hazır ses varsa önceliklidir
            var recipes = Recipes().ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var overrides = OverrideClips(entries[i].key);
                if (overrides.Length > 0)
                {
                    entries[i].clips = overrides;
                    continue;
                }
                entries[i].clips = Enumerable.Range(0, recipes[i].variants)
                    .Select(v => AssetDatabase.LoadAssetAtPath<AudioClip>(
                        $"{GeneratedFolder}/{recipes[i].key}{(v == 0 ? "" : "_" + (v + 1))}.wav"))
                    .Where(c => c != null).ToArray();
            }

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            bank.SetEntries(entries.ToArray());
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            foreach (var e in entries.Where(e => e.clips.Length == 0))
                Debug.LogError("[AudioSetup] Sesi yok: " + e.key);
            Debug.Log($"[AudioSetup] {entries.Count} ses anahtarı, {written.Count} dosya yeniden yazıldı");
            return bank;
        }

        private static AudioClip[] OverrideClips(string key)
        {
            if (!AssetDatabase.IsValidFolder(OverrideFolder)) return Array.Empty<AudioClip>();
            return AssetDatabase.FindAssets("t:AudioClip", new[] { OverrideFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p =>
                {
                    string name = Path.GetFileNameWithoutExtension(p);
                    return name == key || name.StartsWith(key + "_");
                })
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(c => c != null).ToArray();
        }

        private static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) return;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }

        private static bool WriteIfChanged(string path, float[] samples)
        {
            string temp = Path.Combine(Path.GetTempPath(), "deprem_audio.wav");
            WriteWav(temp, samples);
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(temp))) return false;
            File.Copy(temp, path, true);
            return true;
        }

        private static int StableSeed(string key)
        {
            int h = 17;
            foreach (char c in key) h = h * 31 + c;
            return h & 0x7fffffff;
        }

        // ================================================================== Tarifler

        private static IEnumerable<Recipe> Recipes()
        {
            // Play testi 28 (ses föyü): quake.creak, bag.collect, family.rescued ve final.check susturuldu; bankada
            // yokken çağrılar sessizce geçer.
            // Play testi 29: sesler kendi arasında dengelendi (adımlar çok yüksekti). Kaba karışım: deprem olayları ve
            // ölüm önde (0,85-1), darbe ve çarpmalar 0,6-0,8, minigame ve ruh sesleri 0,4-0,65, arayüz 0,35-0,45,
            // ortam 0,4-0,45, müzik 0,5-0,6, adım 0,35 (PlayerFootsteps ayrıca 0,45 ile çarpar).
            // --- Deprem (Mehmet, event'lerden)
            yield return new Recipe { key = "quake.rumble", volume = 0.9f, make = s => Normalize(MakeLoop(Rumble(s, 12f, 1f), 1.5f), 0.85f) };
            yield return new Recipe { key = "quake.foreshock", make = Foreshock };
            yield return new Recipe { key = "quake.impact", make = s => Normalize(Impact(s, 2.6f, 1f), 0.95f) };
            yield return new Recipe { key = "quake.aftershock", make = Aftershock };
            yield return new Recipe { key = "quake.big", make = Big };
            // Play testi 29: sarsıntıda ara ara uzaktan duvar çatlaması ve dökülen moloz (taş kırılma, çökme)
            yield return new Recipe { key = "quake.crumble", variants = 3, volume = 0.7f, pitch = new Vector2(0.9f, 1.08f), make = Crumble };
            yield return new Recipe { key = "quake.debris", variants = 4, volume = 0.45f, pitch = new Vector2(0.85f, 1.15f), make = Debris };
            yield return new Recipe { key = "item.crash", variants = 2, volume = 0.75f, pitch = new Vector2(0.9f, 1.05f), make = Crash };
            yield return new Recipe { key = "glass.break", variants = 3, volume = 0.6f, pitch = new Vector2(0.94f, 1.08f), make = GlassBreak }; // Play testi 28: Kenney "yay" gibiydi
            yield return new Recipe { key = "power.off", volume = 0.8f, make = PowerCut }; // Play testi 28: gerçek elektrik kesintisi
            yield return new Recipe { key = "ambience.night", volume = 0.4f, make = NightAmbience };
            yield return new Recipe { key = "ambience.dark", volume = 0.45f, make = DarkAmbience };
            yield return new Recipe { key = "rescue.heartbeat", volume = 0.7f, make = Heartbeat };
            yield return new Recipe { key = "player.hit", variants = 3, volume = 0.85f, pitch = new Vector2(0.92f, 1.05f), make = BodyHit }; // Play testi 28: Kenney davul gibiydi
            yield return new Recipe { key = "player.step", variants = 4, volume = 0.35f, pitch = new Vector2(0.9f, 1.1f), make = Step };
            yield return new Recipe { key = "player.land", volume = 0.55f, pitch = new Vector2(0.95f, 1.05f), make = Land };
            yield return new Recipe { key = "cabin.door", volume = 0.8f, make = CabinDoor };

            // --- Ruh fazı
            yield return new Recipe { key = "spirit.ambience", volume = 0.55f, make = SpiritAmbience };
            yield return new Recipe { key = "spirit.rise", volume = 0.65f, make = SpiritRise }; // ENTEGRASYON M49 (Berke)
            yield return new Recipe { key = "spirit.tick", volume = 0.4f, make = s => Tick(s, 3200f) };
            yield return new Recipe { key = "spirit.tock", volume = 0.4f, make = s => Tick(s, 2300f) };
            yield return new Recipe { key = "item.secured", volume = 0.7f, make = Secured };

            // --- Berke'nin çağırdıkları (M25)
            yield return new Recipe { key = "ui.move", volume = 0.35f, make = s => Blip(new[] { 1046.5f }, 0.06f) };
            yield return new Recipe { key = "ui.click", volume = 0.45f, make = s => Blip(new[] { 1046.5f, 1568f }, 0.05f) };
            yield return new Recipe { key = "ui.back", volume = 0.45f, make = s => Blip(new[] { 784f, 523.25f }, 0.05f) };
            yield return new Recipe { key = "minigame.screw", variants = 3, volume = 0.45f, pitch = new Vector2(0.92f, 1.08f), make = Ratchet };
            yield return new Recipe { key = "minigame.bracket", volume = 0.65f, make = Bracket };
            yield return new Recipe { key = "minigame.latch", volume = 0.6f, make = Latch };
            yield return new Recipe { key = "minigame.miss", volume = 0.5f, make = Miss };
            yield return new Recipe { key = "minigame.pickup", volume = 0.4f, make = Pickup };
            yield return new Recipe { key = "minigame.drop", volume = 0.5f, make = Drop };
            yield return new Recipe { key = "minigame.fail", volume = 0.6f, make = s => Arpeggio(new[] { 440f, 329.63f }, 0.18f, 1.2f, 3f) };
            yield return new Recipe { key = "wish.shatter", volume = 0.85f, make = Shatter };
            yield return new Recipe { key = "wish.note", volume = 0.7f, make = WishNote };
            yield return new Recipe { key = "wish.type", volume = 0.25f, pitch = new Vector2(0.9f, 1.1f), make = TypeClick };
            yield return new Recipe { key = "wish.rewind", volume = 0.7f, make = TapeRewind }; // Play testi 28: kaset geri sarma
            yield return new Recipe { key = "music.final", volume = 0.6f, make = MemorialMusic }; // Play testi 28: ölenleri anan, acıklı
            // Play testi 20: kurtarmada arkada gerilim, aile üyesinin durumu görülünce vurgu
            yield return new Recipe { key = "music.tension", volume = 0.55f, make = TensionMusic2 }; // Play testi 28: daha gerilimli
            // Play testi 23: balon yazı sesi (Undertale gibi), ölüm vurgusu
            yield return new Recipe { key = "bubble.blip", volume = 0.4f, pitch = new Vector2(0.97f, 1.03f), make = Blip };
            yield return new Recipe { key = "player.death", volume = 0.9f, make = DeathStinger };
            yield return new Recipe { key = "family.discover", volume = 0.75f, make = DiscoverStinger };
            yield return new Recipe { key = "family.loss", volume = 0.8f, make = LossStinger };
            // Play testi 33: ölü anne / kardeş görülünce ses yoktu, üzücü bir şey olsun
            yield return new Recipe { key = "family.grief", volume = 0.9f, make = GriefCue };
            yield return new Recipe { key = "music.menu", volume = 0.6f, make = QuakeMenuMusic }; // Play testi 28: depremi yansıtsın
        }

        // ------------------------------------------------------------------ Deprem

        /// <summary>Derin yer uğultusu: kahverengi gürültü + yavaş dalgalanan alt bas, hafif doyum</summary>
        private static float[] Rumble(SoundSynth s, float seconds, float sub)
        {
            var b = LowPass(LowPass(s.Brown(seconds), 160f), 220f);
            float seed = s.Range(0f, 100f);
            Mix(b, Sine(seconds, t => 36f + 7f * Mathf.Sin(t * 0.8f)), 0f, 0.12f * sub); // Play testi 20: ton "boru" gibi geliyordu
            Shape(b, t => 0.6f + 0.4f * Mathf.PerlinNoise(t * 2.1f, seed));
            return Saturate(Normalize(b, 1f), 1.2f);
        }

        /// <summary>Tıkırtılar: rafta titreşen eşyalar (kısa, tiz, bant geçirilmiş tıklar)</summary>
        private static float[] Rattles(SoundSynth s, float seconds, int count, Func<float, float> density)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < count; i++)
            {
                float t = s.Range(0f, seconds - 0.05f);
                if (s.Rand() > density(t)) continue;
                var click = Envelope(BandPass(s.White(0.03f), s.Range(1800f, 4200f), 5f), 0.0005f, 0f, s.Range(120f, 220f));
                Mix(b, click, t, s.Range(0.4f, 1f));
            }
            return b;
        }

        private static float[] Foreshock(SoundSynth s)
        {
            const float len = 4.5f;
            var b = Envelope(Rumble(s, len, 0.8f), 0.7f, 1.4f, 1.1f);
            Func<float, float> env = t => t < 0.7f ? t / 0.7f : Mathf.Exp(-1.1f * Mathf.Max(0f, t - 2.1f));
            Mix(b, Normalize(Rattles(s, len, 90, env), 1f), 0f, 0.1f); // Play testi 20: çıtırtı fazlaydı
            return Fade(Normalize(b, 0.9f));
        }

        /// <summary>Ana depremin vuruşu: derin bum, gürültü patlaması</summary>
        private static float[] Impact(SoundSynth s, float seconds, float gain)
        {
            var boom = Envelope(Sine(seconds, t => 30f + 30f * Mathf.Exp(-t * 3f)), 0.01f, 0.05f, 1.6f);
            var burst = Envelope(LowPass(s.White(seconds), 350f), 0.005f, 0.05f, 3.5f);
            Mix(boom, Normalize(burst, 1f), 0f, 0.7f);
            return Scale(Saturate(Normalize(boom, 1f), 2.2f), gain);
        }

        private static float[] Aftershock(SoundSynth s)
        {
            const float len = 3.6f;
            var b = Envelope(Rumble(s, len, 1f), 0.25f, 0.8f, 1.4f);
            Mix(b, Normalize(Impact(s, 1.5f, 1f), 1f), 0f, 0.45f);
            Mix(b, Normalize(Rattles(s, len, 50, t => Mathf.Exp(-t)), 1f), 0.1f, 0.08f);
            return Fade(Normalize(b, 0.9f));
        }

        private static float[] Big(SoundSynth s)
        {
            const float len = 4.5f;
            var b = Envelope(Rumble(s, len, 1.2f), 0.05f, 1.6f, 1.2f);
            Mix(b, Normalize(Impact(s, 3f, 1f), 1f), 0f, 0.9f);
            Mix(b, Normalize(Debris(s), 1f), 0.25f, 0.4f);
            Mix(b, Normalize(Crash(s), 1f), 0.6f, 0.4f);
            return Fade(Normalize(Saturate(b, 1.3f), 0.95f));
        }

        /// <summary>
        /// Taş kırılma, çökme (Play testi 29): betonun sert çatlaması (kuru "çat" ve altında tok vuruş), ardından
        /// seyrelerek dökülen moloz ve çakıl, sonunda süzülen kum; altta kısa uğultu. Sarsıntı sırasında uzaktan gelir.
        /// </summary>
        private static float[] Crumble(SoundSynth s)
        {
            const float len = 2f;
            var b = Buffer(len);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.12f), s.Range(1100f, 1800f), 1.5f), 0.0003f, 0.003f, 45f), 1f), 0f, 0.8f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.4f), 220f), 0.001f, 0.01f, 14f), 1f), 0f, 0.7f);
            // Moloz: başta sık, seyrelerek; her parça kısa, bant geçirilmiş darbe
            for (int i = 0; i < 45; i++)
            {
                float t = 0.06f - Mathf.Log(1f - s.Rand() * 0.95f) * 0.35f;
                if (t > len - 0.25f) continue;
                var grain = Envelope(BandPass(s.White(0.06f), s.Range(400f, 2600f), 2.5f), 0.0004f, 0f, s.Range(60f, 130f));
                Mix(b, Normalize(grain, 1f), t, s.Range(0.1f, 0.4f) * Mathf.Exp(-t * 1.6f));
            }
            var sand = HighPass(s.White(len), 2500f);
            Shape(sand, t => Mathf.Clamp01((t - 0.15f) / 0.2f) * Mathf.Exp(-Mathf.Max(0f, t - 0.35f) * 2.2f));
            Mix(b, Normalize(sand, 1f), 0f, 0.08f);
            var rumble = LowPass(s.Brown(1.4f), 120f);
            Shape(rumble, t => Mathf.Exp(-t * 2.5f));
            Mix(b, Normalize(rumble, 1f), 0f, 0.35f);
            return Fade(Normalize(Reverb(b, 0.9f, 0.25f, 0.7f), 0.9f));
        }

        /// <summary>Tavandan düşen taş ve sıva: birkaç küçük tok darbe ve kum hışırtısı</summary>
        private static float[] Debris(SoundSynth s)
        {
            // Play testi 23: kazma gibi çınlayan sesler kötüydü. Yumuşak, alçak tok vuruşlar ve ardından ince kum
            // ve sıva hışırtısı (tiz çınlama yok)
            var b = Buffer(0.9f);
            int hits = 1 + (int)(s.Rand() * 3);
            float t = 0f;
            for (int i = 0; i < hits; i++)
            {
                var thud = Envelope(LowPass(LowPass(s.White(0.16f), s.Range(180f, 320f)), 400f), 0.002f, 0f, s.Range(22f, 34f));
                Mix(b, Normalize(thud, 1f), t, Mathf.Lerp(1f, 0.45f, i / (float)Mathf.Max(1, hits)));
                t += s.Range(0.06f, 0.16f);
            }
            for (int k = 0; k < 22; k++)
            {
                float at = s.Range(0.02f, 0.55f);
                var grain = Envelope(BandPass(s.White(0.02f), s.Range(1200f, 2600f), 3f), 0.0005f, 0f, 220f);
                Mix(b, grain, at, 0.05f * Mathf.Exp(-at * 3f));
            }
            return Fade(Normalize(LowPass(b, 5000f), 0.6f));
        }

        /// <summary>Devrilen dolap: büyük tok darbe ve tahta takırtıları</summary>
        private static float[] Crash(SoundSynth s)
        {
            var b = Buffer(1.6f);
            Mix(b, Normalize(Envelope(Sine(0.8f, t => 42f + 40f * Mathf.Exp(-t * 9f)), 0.002f, 0.02f, 6f), 1f), 0f, 1f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.6f), 300f), 0.001f, 0.01f, 9f), 1f), 0f, 0.8f);
            for (int i = 0; i < 6; i++)
            {
                var knock = Envelope(BandPass(s.White(0.15f), s.Range(280f, 750f), 7f), 0.0005f, 0f, s.Range(25f, 40f));
                Mix(b, Normalize(knock, 1f), s.Range(0.03f, 0.7f), s.Range(0.25f, 0.6f));
            }
            Mix(b, Normalize(Debris(s), 1f), 0.15f, 0.4f);
            return Fade(Normalize(Saturate(b, 1.4f), 0.95f));
        }

        /// <summary>Cam parçalarının çınlaması (wish.shatter içinde kullanılır; o ses beğenildi, değişmesin)</summary>
        private static float[] Glass(SoundSynth s, float seconds, int shards, float wet)
        {
            var b = Buffer(seconds);
            Mix(b, Normalize(Envelope(HighPass(s.White(0.4f), 2500f), 0.001f, 0.01f, 14f), 1f), 0f, 0.8f);
            for (int i = 0; i < shards; i++)
            {
                float t = -Mathf.Log(1f - s.Rand() * 0.98f) * 0.22f; // çoğu başta, seyrelerek
                if (t > seconds - 0.2f) continue;
                var tink = Bell(0.25f, s.Range(2400f, 7200f), new[] { 1f, 2.76f, 5.4f }, new[] { 1f, 0.45f, 0.2f }, s.Range(22f, 45f));
                Mix(b, tink, t, s.Range(0.08f, 0.3f) * Mathf.Exp(-t * 1.5f));
            }
            return Fade(Normalize(Reverb(b, 0.7f, wet, 0.72f), 0.85f));
        }

        /// <summary>
        /// Cam kırılması (Play testi 28: hazır ses "yay" gibiydi): tok çarpma, keskin çatlama, kısa kırılma hışırtısı
        /// ve yere saçılan parçaların seyrelerek çınlaması; kısa oda yankısı
        /// </summary>
        private static float[] GlassBreak(SoundSynth s)
        {
            var b = Buffer(1.6f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.2f), 450f), 0.001f, 0.005f, 30f), 1f), 0f, 0.5f);
            Mix(b, Normalize(Envelope(HighPass(s.White(0.25f), 3000f), 0.0005f, 0.004f, 28f), 1f), 0f, 0.9f);
            for (int i = 0; i < 40; i++)
            {
                float t = s.Range(0f, 0.25f) * s.Rand(); // kıymıklar çoğunlukla en başta
                var grain = Envelope(HighPass(s.White(0.02f), s.Range(2500f, 6000f)), 0.0003f, 0f, s.Range(120f, 260f));
                Mix(b, grain, t, s.Range(0.15f, 0.45f));
            }
            for (int i = 0; i < 26; i++)
            {
                float t = 0.05f - Mathf.Log(1f - s.Rand() * 0.97f) * 0.25f; // saçılanlar seyrelerek
                if (t > 1.3f) continue;
                var tink = Bell(0.2f, s.Range(3000f, 8500f), new[] { 1f, 2.76f, 5.4f }, new[] { 1f, 0.4f, 0.15f }, s.Range(35f, 70f));
                Mix(b, tink, t, s.Range(0.05f, 0.2f) * Mathf.Exp(-t * 1.8f));
            }
            return Fade(Normalize(Reverb(b, 0.6f, 0.18f, 0.6f), 0.85f));
        }

        /// <summary>
        /// Elektrik kesintisi (Play testi 28): şalter atar (tok klak), kesik kesik elektrik cızırtısı, ardından
        /// buzdolabı / trafo uğultusu inerek susar; sonda sönen ampulün ince tıkırtısı
        /// </summary>
        private static float[] PowerCut(SoundSynth s)
        {
            const float len = 2.2f;
            var b = Buffer(len);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.05f), 900f, 2f), 0.0003f, 0.002f, 90f), 1f), 0.02f, 1f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.08f), 300f), 0.0005f, 0.004f, 50f), 1f), 0.02f, 0.6f);
            // Ark: 100 Hz kare dalga vızıltısı ve cızırtı, rastgele kesilip açılır, 0,45 sn'de söner
            var buzz = Buffer(0.5f);
            for (int h = 1; h <= 15; h += 2) { int k = h; Mix(buzz, Sine(0.5f, _ => 100f * k), 0f, 1f / k); }
            Mix(buzz, Normalize(HighPass(s.White(0.5f), 2000f), 1f), 0f, 0.5f);
            float seed = s.Range(0f, 50f);
            Shape(buzz, t => (Mathf.PerlinNoise(t * 45f, seed) > 0.45f ? 1f : 0.08f) * Mathf.Clamp01(1f - t / 0.45f));
            Mix(b, Normalize(LowPass(buzz, 5000f), 1f), 0.04f, 0.35f);
            // Uğultu: 100 Hz ve harmoniği inerek susar
            var hum = Sine(len - 0.1f, t => 100f * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(t / 1.6f)));
            Mix(hum, Sine(len - 0.1f, t => 200f * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(t / 1.6f))), 0f, 0.4f);
            Shape(hum, t => Mathf.Pow(Mathf.Clamp01(1f - t / 1.7f), 1.5f));
            Mix(b, hum, 0.03f, 0.3f);
            Mix(b, Bell(0.4f, 4200f, new[] { 1f, 2.4f }, new[] { 1f, 0.3f }, 25f), 0.55f, 0.06f);
            return Fade(Normalize(b, 0.8f));
        }

        /// <summary>Gece odası: çok hafif oda uğultusu ve dışarıda arada bir cırcır böceği</summary>
        private static float[] NightAmbience(SoundSynth s)
        {
            const float len = 14f;
            var b = Scale(LowPass(s.Brown(len), 250f), 0.25f);
            for (float t = s.Range(0.3f, 1f); t < len - 0.5f; t += s.Range(0.9f, 2.2f))
            {
                for (int k = 0; k < 3; k++)
                {
                    var chirp = Shape(Sine(0.018f, _ => 4300f), u => Mathf.Sin(u / 0.018f * Mathf.PI));
                    Mix(b, chirp, t + k * 0.045f, 0.035f);
                }
            }
            return Normalize(MakeLoop(b, 2f), 0.4f);
        }

        /// <summary>Elektrik kesik ev: alçak oda tonu ve seyrek ufalanan sıva</summary>
        private static float[] DarkAmbience(SoundSynth s)
        {
            const float len = 14f;
            var b = LowPass(s.Brown(len), 140f);
            for (float t = s.Range(1f, 3f); t < len - 1f; t += s.Range(2.5f, 5f))
            {
                var crumble = Envelope(HighPass(s.White(0.4f), 1800f), 0.05f, 0.1f, 10f);
                Mix(b, crumble, t, 0.012f);
            }
            return Normalize(MakeLoop(b, 2f), 0.45f);
        }

        private static float[] Heartbeat(SoundSynth s)
        {
            var b = Buffer(0.75f);
            Mix(b, Envelope(Sine(0.3f, t => 45f + 25f * Mathf.Exp(-t * 20f)), 0.006f, 0.02f, 22f), 0f, 1f);
            Mix(b, Envelope(Sine(0.3f, t => 40f + 20f * Mathf.Exp(-t * 20f)), 0.006f, 0.015f, 28f), 0.19f, 0.7f);
            return Fade(Normalize(Saturate(LowPass(b, 180f), 2f), 0.95f));
        }

        /// <summary>
        /// Oyuncuya darbe (Play testi 28: hazır ses davula vurmak gibiydi): perdesi kaymayan (davul tınısı yok),
        /// gürültüden gelen tok gövde vuruşu, boğuk kumaş şaplağı, altta kısa sabit bas
        /// </summary>
        private static float[] BodyHit(SoundSynth s)
        {
            var b = Buffer(0.5f);
            Mix(b, Normalize(Envelope(LowPass(LowPass(s.White(0.4f), 260f), 260f), 0.001f, 0.008f, 16f), 1f), 0f, 1f);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.12f), s.Range(600f, 900f), 1.2f), 0.0005f, 0f, 55f), 1f), 0f, 0.45f);
            float sub = s.Range(52f, 60f);
            Mix(b, Envelope(Sine(0.3f, _ => sub), 0.002f, 0.005f, 30f), 0f, 0.35f);
            return Fade(Normalize(Saturate(b, 1.2f), 0.95f));
        }

        private static float[] Step(SoundSynth s)
        {
            var b = Normalize(Envelope(LowPass(s.White(0.14f), s.Range(380f, 600f)), 0.002f, 0f, 45f), 1f);
            Mix(b, Envelope(HighPass(s.White(0.02f), 1500f), 0.0005f, 0f, 220f), 0f, 0.15f);
            return Fade(Normalize(b, 0.6f));
        }

        private static float[] Land(SoundSynth s)
        {
            var b = Normalize(Envelope(LowPass(s.White(0.3f), 400f), 0.002f, 0.01f, 22f), 1f);
            Mix(b, Envelope(Sine(0.25f, t => 70f - 25f * t), 0.002f, 0f, 18f), 0f, 0.5f);
            return Fade(Normalize(b, 0.8f));
        }

        private static float[] CabinDoor(SoundSynth s)
        {
            var b = Buffer(1.4f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.4f), 300f), 0.002f, 0.01f, 14f), 1f), 0f, 0.9f);
            Mix(b, Bell(1.3f, 140f, new[] { 1f, 2.13f, 3.91f, 5.27f }, new[] { 1f, 0.6f, 0.4f, 0.25f }, 4.5f), 0f, 0.45f);
            var latch = Envelope(BandPass(s.White(0.05f), 2200f, 4f), 0.0003f, 0f, 120f);
            Mix(b, Normalize(latch, 1f), 0.28f, 0.5f);
            return Fade(Normalize(Reverb(b, 0.5f, 0.2f, 0.6f), 0.9f));
        }

        // ------------------------------------------------------------------ Ruh fazı ve onaylar

        /// <summary>Donmuş dünya: suyun altından gibi boğuk gürültü, yavaş dalgalanan soğuk pad</summary>
        private static float[] SpiritAmbience(SoundSynth s)
        {
            const float len = 16f;
            var b = Scale(LowPass(LowPass(s.White(len), 320f), 320f), 2.5f);
            float[] notes = { 110f, 164.81f, 261.63f, 329.63f };
            foreach (float hz in notes)
            {
                var pad = Sine(len, t => hz * (1f + 0.002f * Mathf.Sin(t * 0.7f)));
                Mix(pad, Sine(len, t => hz * 1.004f), 0f, 0.8f);
                Shape(pad, t => 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI / len * 2f + hz));
                Mix(b, pad, 0f, 0.12f);
            }
            return Normalize(MakeLoop(Reverb(b, 2f, 0.5f, 0.85f), 3f), 0.6f);
        }

        private static float[] Tick(SoundSynth s, float hz)
        {
            var b = Envelope(BandPass(s.White(0.1f), hz, 10f), 0.0005f, 0f, 110f);
            Mix(b, Bell(0.1f, hz * 0.6f, new[] { 1f, 2.7f }, new[] { 1f, 0.3f }, 60f), 0f, 0.08f);
            return Fade(Normalize(b, 0.7f));
        }

        private static float[] Secured(SoundSynth s)
        {
            var b = Buffer(1.3f);
            Mix(b, Bell(0.6f, 420f, new[] { 1f, 2.32f, 4.25f, 6.63f }, new[] { 1f, 0.6f, 0.35f, 0.2f }, 9f), 0f, 0.35f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.1f), 500f), 0.001f, 0f, 40f), 1f), 0f, 0.3f);
            Mix(b, Note(1f, 659.25f, 0.005f, 4f, 0.35f), 0.08f, 0.35f);
            Mix(b, Note(1f, 987.77f, 0.005f, 4f, 0.35f), 0.2f, 0.35f);
            return Fade(Normalize(Reverb(b, 0.6f, 0.25f, 0.6f), 0.8f));
        }

        private static float[] Arpeggio(float[] notes, float step, float length, float decay = 5f)
        {
            var b = Buffer(length);
            for (int i = 0; i < notes.Length; i++)
                Mix(b, Note(length - i * step, notes[i], 0.004f, decay, 0.35f), i * step, 0.5f);
            return Fade(Normalize(Reverb(b, 0.6f, 0.25f, 0.6f), 0.75f));
        }

        // ------------------------------------------------------------------ Menü ve minigame (Berke)

        private static float[] Blip(float[] notes, float step)
        {
            var b = Buffer(step * notes.Length + 0.08f);
            for (int i = 0; i < notes.Length; i++)
                Mix(b, Note(0.1f, notes[i], 0.002f, 45f, 0.6f), i * step, 0.6f);
            return Fade(Normalize(b, 0.6f));
        }

        private static float[] Ratchet(SoundSynth s)
        {
            var b = Envelope(BandPass(s.White(0.07f), s.Range(2500f, 3200f), 6f), 0.0003f, 0f, 150f);
            Mix(b, Bell(0.07f, s.Range(2900f, 3400f), new[] { 1f, 2.4f }, new[] { 1f, 0.4f }, 70f), 0f, 0.05f);
            return Fade(Normalize(b, 0.7f));
        }

        private static float[] Bracket(SoundSynth s)
        {
            var b = Buffer(0.9f);
            Mix(b, Bell(0.8f, 380f, new[] { 1f, 2.32f, 4.25f, 6.63f }, new[] { 1f, 0.6f, 0.35f, 0.2f }, 8f), 0f, 0.5f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.15f), 600f), 0.001f, 0f, 30f), 1f), 0f, 0.5f);
            return Fade(Normalize(b, 0.85f));
        }

        private static float[] Latch(SoundSynth s)
        {
            var b = Buffer(0.25f);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.05f), 1600f, 5f), 0.0003f, 0f, 140f), 1f), 0f, 0.8f);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.05f), 2500f, 5f), 0.0003f, 0f, 160f), 1f), 0.05f, 1f);
            return Fade(Normalize(b, 0.75f));
        }

        private static float[] Miss(SoundSynth s)
        {
            var b = Buffer(0.3f);
            for (int h = 1; h <= 9; h += 2) Mix(b, Sine(0.3f, _ => 110f * h), 0f, 1f / h);
            return Fade(Normalize(LowPass(Envelope(b, 0.005f, 0.08f, 12f), 1200f), 0.6f));
        }

        private static float[] Pickup(SoundSynth s)
        {
            var b = BandPass(s.White(0.25f), t => 400f + 1200f * t / 0.25f, 2.5f);
            return Fade(Normalize(Shape(b, t => Mathf.Sin(t / 0.25f * Mathf.PI)), 0.5f));
        }

        private static float[] Drop(SoundSynth s)
        {
            var b = Normalize(Envelope(LowPass(s.White(0.3f), 420f), 0.001f, 0f, 25f), 1f);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.15f), 360f, 8f), 0.0005f, 0f, 30f), 1f), 0f, 0.5f);
            return Fade(Normalize(b, 0.7f));
        }

        // ------------------------------------------------------------------ Keşke ve final

        /// <summary>
        /// Keşke ekranında kırmızı çatlağın yarıldığı an (Berke M22: cam kırılması kalktı). Hızlanan keskin
        /// çıtırtılar, sonunda derin bir yarılma ve birkaç ince çınlama; uzun yankı.
        /// </summary>
        private static float[] Shatter(SoundSynth s)
        {
            const float len = 2.6f;
            var b = Buffer(len);
            // Çıtırtılar sıklaşarak 0,35 sn'de yarılmaya varır
            float t = 0f, gap = 0.07f;
            while (t < 0.35f)
            {
                var crack = Envelope(HighPass(s.White(0.05f), s.Range(1500f, 3500f)), 0.0003f, 0f, s.Range(90f, 160f));
                Mix(b, Normalize(crack, 1f), t, Mathf.Lerp(0.35f, 0.8f, t / 0.35f));
                t += gap * s.Range(0.6f, 1.2f);
                gap = Mathf.Max(0.012f, gap * 0.82f); // alt sınır: aralık sıfıra inip döngü bitmez olmasın
            }
            var split = Envelope(BandPass(s.White(0.5f), t2 => 900f - 600f * t2 / 0.5f, 1.2f), 0.001f, 0.02f, 9f);
            Mix(b, Normalize(split, 1f), 0.36f, 0.9f);
            Mix(b, Normalize(Impact(s, 1.8f, 1f), 1f), 0.36f, 0.55f);
            Mix(b, Glass(s, 1.2f, 14, 0f), 0.4f, 0.25f);
            return Fade(Normalize(Reverb(b, 1.8f, 0.35f, 0.82f), 0.95f));
        }

        /// <summary>GDD: sessizlik ve tek bir nota. Yumuşak, yankılı alçak bir piyano notası</summary>
        private static float[] WishNote(SoundSynth s)
        {
            var b = Note(5f, 220f, 0.008f, 0.8f, 0.45f);
            Mix(b, Note(5f, 110f, 0.01f, 0.7f, 0.3f), 0f, 0.35f);
            return Fade(Normalize(Reverb(b, 3f, 0.45f, 0.86f), 0.8f));
        }

        private static float[] TypeClick(SoundSynth s)
        {
            var b = Envelope(BandPass(s.White(0.04f), 2000f, 4f), 0.0003f, 0f, 260f);
            return Fade(Normalize(b, 0.5f));
        }

        /// <summary>
        /// Zaman geri sarılıyor (Play testi 28): kaset geri sarma. Perdesi yükselen ve titreşimi hızlanan "cıvıl cıvıl"
        /// ses (geri sarılan konuşma gibi), bant hışırtısı, yükselen ters zil kabarması; sonda bant durur (tık)
        /// </summary>
        private static float[] TapeRewind(SoundSynth s)
        {
            const float len = 1.8f;
            var b = Buffer(len + 0.1f);
            Func<float, float> speed = t => Mathf.Pow(Mathf.Clamp01(t / len), 1.4f);
            var warble = Sine(len, t => (260f + 900f * speed(t)) * (1f + 0.25f * Mathf.Sin(t * (20f + 140f * speed(t)))));
            Mix(warble, Sine(len, t => (390f + 1350f * speed(t)) * (1f + 0.2f * Mathf.Sin(t * (26f + 160f * speed(t)) + 1.3f))), 0f, 0.5f);
            Shape(warble, t => 0.3f + 0.7f * speed(t));
            Mix(b, LowPass(warble, 3000f), 0f, 0.3f);
            var hiss = BandPass(s.White(len), t => 1500f + 3000f * speed(t), 1f);
            Shape(hiss, t => 0.2f + 0.3f * speed(t));
            Mix(b, Normalize(hiss, 1f), 0f, 0.25f);
            var swell = HighPass(s.White(len), 4000f);
            Shape(swell, t => Mathf.Pow(t / len, 3f));
            Mix(b, Normalize(swell, 1f), 0f, 0.35f);
            Mix(b, Normalize(Envelope(BandPass(s.White(0.03f), 2000f, 3f), 0.0002f, 0f, 200f), 1f), len - 0.01f, 0.5f);
            return Fade(Normalize(b, 0.8f), 0.02f, 0.02f);
        }

        // ------------------------------------------------------------------ Gerilim

        /// <summary>
        /// Kurtarma fazının arkasındaki gerilim (Play testi 28: "daha gerilimli olsun"). Dizüstü hoparlörde de duyulsun
        /// diye her şey orta frekanslarda (Play testi 22). Dört 8 sn'lik bölüm (La, Fa, Re, Mi kökleri):
        ///   - yarım ton sürtünen uzun kümeler (kök + küçük ikili + beşli), yavaş kabarır;
        ///   - 120 vuruşta onaltılık bas ostinatosu, kök ile yarım ton üstü arasında gidip gelir, bölüm boyunca sıklaşır;
        ///   - kalp atışı, uyumsuz keman tremolosu (iki yüksek ton yarım ton arayla), bölüm ortasında madeni sürtme;
        ///   - her bölüm sonunda yükselen gürültü. Döngü 32 sn, dikişsiz.
        /// </summary>
        private static float[] TensionMusic2(SoundSynth s)
        {
            const float length = 32f, tail = 4f, section = 8f, beat = 0.5f;
            var b = Buffer(length + tail);
            float[] roots = { 220f, 174.61f, 146.83f, 164.81f };
            for (int c = 0; c < roots.Length; c++)
            {
                float root = roots[c], start = c * section;
                foreach (var (hz, gain) in new[] { (root, 0.12f), (root * 1.0595f, 0.1f), (root * 1.4983f, 0.07f), (root * 0.5f, 0.12f) })
                {
                    Mix(b, Note(section + 2f, hz, 2.5f, 0.15f, 0.55f), start, gain);
                    Mix(b, Note(section + 2f, hz * 1.005f, 2.8f, 0.15f, 0.55f), start, gain * 0.7f);
                }
                // Ostinato: onaltılık, ilk yarıda seyrek (sekizlik), ikinci yarıda sık
                for (float t = 0f; t < section; t += beat / 2f)
                {
                    int step = Mathf.RoundToInt(t / (beat / 2f));
                    bool dense = t >= section * 0.5f;
                    if (!dense && step % 2 == 1) continue;
                    float hz = step % 4 == 3 ? root * 1.0595f : root;
                    float gain = (step % 4 == 0 ? 0.2f : 0.12f) * Mathf.Lerp(0.85f, 1.15f, t / section);
                    Mix(b, Note(0.3f, hz, 0.002f, 14f, 0.7f), start + t, gain);
                }
                // Madeni sürtme: dar, rezonanslı gürültü süpürmesi
                var scrape = BandPass(s.White(1.6f), u => Mathf.Lerp(700f, 2600f, u / 1.6f), 9f);
                Shape(scrape, u => Mathf.Sin(Mathf.Clamp01(u / 1.6f) * Mathf.PI));
                Mix(b, Normalize(scrape, 1f), start + 3.5f, 0.06f);
                // Bölüm sonu: yükselen gürültü
                var riser = BandPass(s.White(1.8f), u => 400f + 3000f * Mathf.Pow(u / 1.8f, 2f), 2f);
                Shape(riser, u => Mathf.Pow(u / 1.8f, 2.5f));
                Mix(b, Normalize(riser, 1f), start + section - 1.8f, 0.09f);
            }
            // Kalp atışı: her vuruşta değil, iki vuruşta bir (dakikada 60), tok
            for (float t = 0f; t < length; t += beat * 2f)
            {
                Mix(b, Envelope(Sine(0.3f, u => 70f + 50f * Mathf.Exp(-u * 20f)), 0.004f, 0.01f, 18f), t, 0.35f);
                Mix(b, Envelope(Sine(0.3f, u => 62f + 40f * Mathf.Exp(-u * 20f)), 0.004f, 0.01f, 22f), t + 0.22f, 0.22f);
                Mix(b, Envelope(Sine(0.15f, _ => 160f), 0.002f, 0f, 35f), t, 0.07f);
            }
            // Uyumsuz keman tremolosu: 880 ve 932 Hz, bölüm sonlarına doğru kabarır
            foreach (float hz in new[] { 880f, 932.33f })
            {
                float f = hz;
                var high = Sine(length + tail, t => f * (1f + 0.003f * Mathf.Sin(t * 5.5f)));
                Shape(high, t => (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 8f)) * Mathf.Pow(Mathf.Repeat(t, section) / section, 1.6f));
                Mix(b, high, 0f, 0.03f);
            }
            var wet = Reverb(b, 2.5f, 0.3f, 0.82f);
            var loop = new float[Samples(length)];
            for (int i = 0; i < wet.Length; i++) loop[i % loop.Length] += wet[i];
            return Normalize(loop, 0.8f);
        }
        /// <summary>Konuşma balonu yazı sesi: kısa, yumuşak kare dalga "blip" (perdeyi konuşan belirler)</summary>
        private static float[] Blip(SoundSynth s)
        {
            const float len = 0.055f;
            var b = Buffer(len);
            for (int h = 1; h <= 7; h += 2) Mix(b, Sine(len, _ => 520f * h), 0f, 1f / h);
            Envelope(b, 0.002f, 0.012f, 70f);
            return Fade(Normalize(LowPass(b, 2800f), 0.5f), 0.002f, 0.008f);
        }

        /// <summary>
        /// Ruhun bedenden çıkışı (M49, ~1,1 sn): yumuşak, yükselen ve titreşen iki ses (beşli aralık), altında
        /// yükselen hava; sonda hafif bir çan. Uzun yankıyla söner, ürkütücü değil, boşlukta süzülme hissi
        /// </summary>
        private static float[] SpiritRise(SoundSynth s)
        {
            const float len = 2.2f;
            var b = Buffer(len);
            Func<float, float> glide = t => Mathf.Lerp(220f, 440f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.1f)));
            var low = Sine(1.6f, t => glide(t) * (1f + 0.012f * Mathf.Sin(t * 34f)));
            var high = Sine(1.6f, t => glide(t) * 1.5f * (1f + 0.012f * Mathf.Sin(t * 29f + 1f)));
            Func<float, float> swell = t => Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) * Mathf.Clamp01(t / 0.35f);
            Mix(b, Shape(low, swell), 0f, 0.35f);
            Mix(b, Shape(high, swell), 0f, 0.22f);
            var air = BandPass(s.White(1.5f), t => 400f + 2200f * t / 1.5f, 3f);
            Shape(air, t => Mathf.Sin(Mathf.Clamp01(t / 1.5f) * Mathf.PI));
            Mix(b, Normalize(air, 1f), 0f, 0.16f);
            Mix(b, Note(1.2f, 1318.5f, 0.01f, 2.5f, 0.3f), 1.05f, 0.12f);
            return Fade(Normalize(Reverb(b, 1.8f, 0.45f, 0.85f), 0.8f), 0.02f, 0.05f);
        }

        /// <summary>Ölüm anı: derin vuruş, altında inen uyumsuz ton, kulak çınlaması (Play testi 24), uzun yankıyla sönen</summary>
        private static float[] DeathStinger(SoundSynth s)
        {
            var b = Buffer(3.5f);
            Mix(b, Normalize(Impact(s, 2f, 1f), 1f), 0f, 0.8f);
            Mix(b, Envelope(Sine(2.5f, t => 196f * Mathf.Pow(0.5f, t / 2f)), 0.01f, 0.1f, 1.2f), 0.05f, 0.25f);
            Mix(b, Envelope(Sine(2.5f, t => 207.65f * Mathf.Pow(0.5f, t / 2f)), 0.01f, 0.1f, 1.2f), 0.05f, 0.2f);
            Mix(b, Envelope(Sine(3.2f, t => 3520f + 25f * Mathf.Sin(t * 9f)), 0.3f, 0.7f, 1.1f), 0.12f, 0.07f);
            return Fade(Normalize(Reverb(b, 2f, 0.4f, 0.85f), 0.95f));
        }

        /// <summary>Aile üyesinin durumu görülünce: tok vuruş, uyumsuz akor, kabaran hava</summary>
        private static float[] DiscoverStinger(SoundSynth s)
        {
            var b = Buffer(3.2f);
            Mix(b, Normalize(Impact(s, 1.5f, 1f), 1f), 0f, 0.5f);
            foreach (float hz in new[] { 110f, 116.54f, 164.81f, 233.08f })
                Mix(b, Note(3f, hz, 0.01f, 0.9f, 0.5f), 0f, 0.3f);
            var swell = BandPass(s.White(1.4f), t => 200f + 900f * t / 1.4f, 2f);
            Shape(swell, t => Mathf.Pow(t / 1.4f, 2f) * (t < 1.3f ? 1f : (1.4f - t) / 0.1f));
            Mix(b, Normalize(swell, 1f), 0f, 0.25f);
            return Fade(Normalize(Reverb(b, 2.2f, 0.4f, 0.85f), 0.85f));
        }

        /// <summary>Kaybedilmiş üye: derin bum ve yavaş inen minör akor, uzun yankı</summary>
        private static float[] LossStinger(SoundSynth s)
        {
            var b = Buffer(4.5f);
            Mix(b, Normalize(Impact(s, 2.5f, 1f), 1f), 0f, 0.6f);
            Mix(b, Note(4f, 146.83f, 0.02f, 0.5f, 0.4f), 0f, 0.35f);
            Mix(b, Note(4f, 174.61f, 0.02f, 0.5f, 0.4f), 0.35f, 0.3f);
            Mix(b, Note(4f, 110f, 0.02f, 0.45f, 0.35f), 0.7f, 0.35f);
            Mix(b, Note(4f, 103.83f, 0.3f, 0.4f, 0.3f), 1.4f, 0.25f);
            return Fade(Normalize(Reverb(b, 3f, 0.5f, 0.88f), 0.85f));
        }

        /// <summary>
        /// Ölü aile üyesi görülünce: La minörde yavaşça inen yaylı ezgi (Mi, Re, Do, Si, La), altta Am, F, E, Am pad
        /// ve çello kökü, uzun yankı. 8 sn.
        /// </summary>
        private static float[] GriefCue(SoundSynth s)
        {
            var b = Buffer(8f);
            (float at, float hz, float length)[] melody =
            {
                (0.3f, 659.25f, 1f), (1.1f, 587.33f, 0.9f), (1.9f, 523.25f, 0.9f), (2.7f, 493.88f, 1.2f), (3.7f, 440f, 3f),
            };
            foreach (var (at, hz, length) in melody) Mix(b, Bowed(length + 0.8f, hz), at, 0.22f);
            (float at, float[] notes)[] chords =
            {
                (0f, new[] { 220f, 261.63f, 329.63f }), (1.9f, new[] { 174.61f, 220f, 261.63f }),
                (2.7f, new[] { 164.81f, 207.65f, 246.94f }), (3.7f, new[] { 220f, 261.63f, 329.63f }),
            };
            for (int c = 0; c < chords.Length; c++)
            {
                float length = (c + 1 < chords.Length ? chords[c + 1].at - chords[c].at : 3.5f) + 1.2f;
                foreach (float hz in chords[c].notes)
                    Mix(b, Note(length, hz, 0.6f, 0.35f, 0.3f), chords[c].at, 0.07f);
                Mix(b, Note(length, chords[c].notes[0] / 2f, 0.5f, 0.3f, 0.3f), chords[c].at, 0.12f);
            }
            return Fade(Normalize(Reverb(b, 3f, 0.45f, 0.88f), 0.85f), 0.01f, 0.6f);
        }

        /// <summary>Yaylı gibi nota: yavaş atak, gittikçe açılan vibrato, birkaç harmonik, sonda sönüm</summary>
        private static float[] Bowed(float seconds, float hz)
        {
            var b = Buffer(seconds);
            for (int h = 1; h <= 4; h++)
            {
                int k = h;
                Mix(b, Sine(seconds, t => hz * k * (1f + 0.005f * Mathf.Sin(2f * Mathf.PI * 5.2f * t) * Mathf.Min(1f, t / 0.4f))),
                    0f, 1f / (0.8f * k * k + 0.2f));
            }
            Shape(b, t => Mathf.Min(1f, t / 0.25f) * Mathf.Clamp01((seconds - t) / 0.8f));
            return LowPass(b, 2400f);
        }

        // ------------------------------------------------------------------ Müzik

        /// <summary>Yankıyı uzatıp kuyruğu başa bindirir: dikişsiz döngü</summary>
        private static float[] FoldLoop(float[] b, float length, float reverbSeconds, float wet, float room, float peak)
        {
            var reverbed = Reverb(b, reverbSeconds, wet, room);
            var loop = new float[Samples(length)];
            for (int i = 0; i < reverbed.Length; i++) loop[i % loop.Length] += reverbed[i];
            return Normalize(loop, peak);
        }

        /// <summary>
        /// Ana menü (Play testi 28: "daha depremi yansıtacak bir şey"). Altta yerin derinden gelen uğultusu iki kez
        /// kabarır, kabarırken rafta tıkırdayan eşyalar duyulur; Re minörde seyrek, yankılı piyano notaları (biraz
        /// akortsuz, tedirgin), alçak yaylı pad (Re minör, Si bemol, Sol minör, La), ara ara uzak, boğuk bir vuruş.
        /// Döngü 32 sn, dikişsiz.
        /// </summary>
        private static float[] QuakeMenuMusic(SoundSynth s)
        {
            const float length = 32f, tail = 5f, section = 8f;
            var b = Buffer(length + tail);
            // Yer uğultusu: iki kabarma (16 sn'de bir), en sessizde de hafifçe duyulur
            Func<float, float> quake = t => 0.25f + 0.75f * Mathf.Pow(0.5f - 0.5f * Mathf.Cos(t / length * Mathf.PI * 4f), 2f);
            var rumble = Rumble(s, length + tail, 0.6f);
            Shape(rumble, quake);
            Mix(b, rumble, 0f, 0.3f);
            Mix(b, Normalize(Rattles(s, length, 70, t => Mathf.Pow(quake(t), 3f)), 1f), 0f, 0.05f);
            // Pad: Dm, B♭, Gm, A (8'er sn), yavaş atak
            (float root, bool minor)[] chords = { (146.83f, true), (116.54f, false), (98f, true), (110f, false) };
            for (int c = 0; c < chords.Length; c++)
            {
                var (root, minor) = chords[c];
                float third = root * (minor ? 1.1892f : 1.2599f), fifth = root * 1.4983f;
                foreach (float hz in new[] { root, third, fifth, root * 2f })
                {
                    Mix(b, Note(section + 2f, hz, 2.5f, 0.25f, 0.3f), c * section, 0.1f);
                    Mix(b, Note(section + 2f, hz * 1.004f, 2.8f, 0.25f, 0.3f), c * section, 0.06f);
                }
                Mix(b, Note(section + 1f, root / 2f, 1f, 0.3f, 0.2f), c * section, 0.14f);
            }
            // Piyano: Re minör beşlisinden seyrek, inen motifler; ikinci ses hafif akortsuz
            float[] scale = { 293.66f, 349.23f, 392f, 440f, 523.25f, 587.33f };
            int[] motif = { 5, 3, 2, 0, 4, 2, 1, 0, 3, 2, 0, 1, 5, 4, 2, 0 };
            for (int i = 0; i < motif.Length; i++)
            {
                if (i % 4 == 3 && s.Rand() < 0.5f) continue; // nefes payı
                float t = i * 2f + s.Range(-0.05f, 0.08f);
                float hz = scale[motif[i]];
                Mix(b, Note(3.5f, hz, 0.004f, 1.6f, 0.45f), t, 0.16f);
                Mix(b, Note(3.5f, hz * 1.007f, 0.004f, 1.6f, 0.45f), t + 0.01f, 0.05f);
            }
            // Uzak, boğuk vuruş (bir şey devrilmiş gibi), her bölümün ortasında
            for (int c = 0; c < chords.Length; c++)
            {
                var thump = Envelope(LowPass(s.White(0.6f), 180f), 0.003f, 0.02f, 9f);
                Mix(b, Normalize(thump, 1f), c * section + 4.3f, 0.12f);
            }
            return FoldLoop(b, length, 3f, 0.4f, 0.86f, 0.75f);
        }

        /// <summary>
        /// Final (Play testi 28: "daha acıklı olacak, ölen insanları anacağız"). Yavaş (6 sn'lik akorlar) La minörde
        /// ağıt: Am, F, C, G, Am, Dm, F, E. Yumuşak yaylı pad, altta uzun çello kökleri, üstte tek tek inen piyano
        /// ezgisi (çoğu akor notası, ara ara bir yarım ton geçiş), uzun yankı. 48 sn, dikişsiz döngü.
        /// </summary>
        private static float[] MemorialMusic(SoundSynth s)
        {
            const float chordLength = 6f, tail = 6f;
            (float root, bool minor)[] chords =
            {
                (110f, true), (87.31f, false), (130.81f, false), (98f, false),
                (110f, true), (146.83f, true), (87.31f, false), (82.41f, false),
            };
            float length = chords.Length * chordLength;
            var b = Buffer(length + tail);
            // Ezgi: her akorda 3 nota (2 sn), çoğunlukla inen
            float[][] melody =
            {
                new[] { 659.25f, 587.33f, 523.25f }, new[] { 523.25f, 440f, 349.23f },
                new[] { 392f, 440f, 523.25f }, new[] { 493.88f, 440f, 392f },
                new[] { 440f, 523.25f, 659.25f }, new[] { 698.46f, 659.25f, 587.33f },
                new[] { 523.25f, 440f, 415.3f }, new[] { 493.88f, 415.3f, 329.63f },
            };
            for (int c = 0; c < chords.Length; c++)
            {
                var (root, minor) = chords[c];
                float third = root * (minor ? 1.1892f : 1.2599f), fifth = root * 1.4983f;
                float start = c * chordLength;
                foreach (float hz in new[] { root * 2f, third * 2f, fifth * 2f })
                {
                    Mix(b, Note(chordLength + 3f, hz, 2.2f, 0.2f, 0.28f), start, 0.09f);
                    Mix(b, Note(chordLength + 3f, hz * 1.003f, 2.6f, 0.2f, 0.28f), start, 0.06f);
                }
                Mix(b, Note(chordLength + 2f, root, 1.5f, 0.25f, 0.35f), start, 0.16f);   // çello
                Mix(b, Note(chordLength + 2f, root / 2f, 1.8f, 0.25f, 0.2f), start, 0.1f);
                for (int k = 0; k < 3; k++)
                {
                    float t = start + k * 2f + (k == 0 ? 0.15f : 0f);
                    float hz = melody[c][k];
                    Mix(b, Note(4f, hz, 0.005f, 0.9f, 0.4f), t, 0.17f);
                    Mix(b, Note(4f, hz / 2f, 0.005f, 0.9f, 0.3f), t, 0.05f);
                }
            }
            return FoldLoop(b, length, 3.5f, 0.45f, 0.88f, 0.7f);
        }
    }
}
