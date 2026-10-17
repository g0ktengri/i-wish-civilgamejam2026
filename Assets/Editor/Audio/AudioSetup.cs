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
            // --- Deprem (Mehmet, event'lerden)
            yield return new Recipe { key = "quake.rumble", volume = 0.9f, make = s => Normalize(MakeLoop(Rumble(s, 12f, 1f), 1.5f), 0.85f) };
            yield return new Recipe { key = "quake.foreshock", make = Foreshock };
            yield return new Recipe { key = "quake.impact", make = s => Normalize(Impact(s, 2.6f, 1f), 0.95f) };
            yield return new Recipe { key = "quake.aftershock", make = Aftershock };
            yield return new Recipe { key = "quake.big", make = Big };
            yield return new Recipe { key = "quake.creak", variants = 3, volume = 0.75f, pitch = new Vector2(0.9f, 1.1f), make = Creak };
            yield return new Recipe { key = "quake.debris", variants = 4, volume = 0.5f, pitch = new Vector2(0.85f, 1.15f), make = Debris };
            yield return new Recipe { key = "item.crash", variants = 2, pitch = new Vector2(0.9f, 1.05f), make = Crash };
            yield return new Recipe { key = "glass.break", variants = 2, volume = 0.85f, pitch = new Vector2(0.95f, 1.1f), make = s => Glass(s, 1.8f, 40, 0.25f) };
            yield return new Recipe { key = "power.off", make = PowerOff };
            yield return new Recipe { key = "ambience.night", volume = 0.5f, make = NightAmbience };
            yield return new Recipe { key = "ambience.dark", volume = 0.6f, make = DarkAmbience };
            yield return new Recipe { key = "rescue.heartbeat", make = Heartbeat };
            yield return new Recipe { key = "player.hit", make = Hit };
            yield return new Recipe { key = "player.step", variants = 4, volume = 1f, pitch = new Vector2(0.9f, 1.1f), make = Step };
            yield return new Recipe { key = "player.land", pitch = new Vector2(0.95f, 1.05f), make = Land };
            yield return new Recipe { key = "cabin.door", make = CabinDoor };

            // --- Ruh fazı
            yield return new Recipe { key = "spirit.ambience", volume = 0.7f, make = SpiritAmbience };
            yield return new Recipe { key = "spirit.rise", volume = 0.75f, make = SpiritRise }; // ENTEGRASYON M49 (Berke)
            yield return new Recipe { key = "spirit.tick", volume = 0.6f, make = s => Tick(s, 3200f) };
            yield return new Recipe { key = "spirit.tock", volume = 0.6f, make = s => Tick(s, 2300f) };
            yield return new Recipe { key = "item.secured", volume = 0.8f, make = Secured };
            yield return new Recipe { key = "bag.collect", volume = 0.7f, make = s => Arpeggio(new[] { 523.25f, 659.25f, 783.99f }, 0.07f, 0.9f) };
            yield return new Recipe { key = "family.rescued", volume = 0.7f, make = s => Arpeggio(new[] { 392f, 523.25f }, 0.16f, 1.4f) };

            // --- Berke'nin çağırdıkları (M25)
            yield return new Recipe { key = "ui.move", volume = 0.35f, make = s => Blip(new[] { 1046.5f }, 0.06f) };
            yield return new Recipe { key = "ui.click", volume = 0.45f, make = s => Blip(new[] { 1046.5f, 1568f }, 0.05f) };
            yield return new Recipe { key = "ui.back", volume = 0.45f, make = s => Blip(new[] { 784f, 523.25f }, 0.05f) };
            yield return new Recipe { key = "minigame.screw", variants = 3, volume = 0.55f, pitch = new Vector2(0.92f, 1.08f), make = Ratchet };
            yield return new Recipe { key = "minigame.bracket", volume = 0.8f, make = Bracket };
            yield return new Recipe { key = "minigame.latch", volume = 0.7f, make = Latch };
            yield return new Recipe { key = "minigame.miss", volume = 0.5f, make = Miss };
            yield return new Recipe { key = "minigame.pickup", volume = 0.5f, make = Pickup };
            yield return new Recipe { key = "minigame.drop", volume = 0.6f, make = Drop };
            yield return new Recipe { key = "minigame.fail", volume = 0.6f, make = s => Arpeggio(new[] { 440f, 329.63f }, 0.18f, 1.2f, 3f) };
            yield return new Recipe { key = "wish.shatter", make = Shatter };
            yield return new Recipe { key = "wish.note", volume = 0.8f, make = WishNote };
            yield return new Recipe { key = "wish.type", volume = 0.25f, pitch = new Vector2(0.9f, 1.1f), make = TypeClick };
            yield return new Recipe { key = "wish.rewind", volume = 0.8f, make = Rewind };
            yield return new Recipe { key = "final.check", volume = 0.6f, make = FinalCheck };
            yield return new Recipe { key = "music.final", make = s => Music(s, FinalChords, 0.5f, 0.3f) };
            // Play testi 20: kurtarmada arkada gerilim, aile üyesinin durumu görülünce vurgu
            yield return new Recipe { key = "music.tension", volume = 0.9f, make = TensionMusic };
            // Play testi 23: balon yazı sesi (Undertale gibi), ölüm vurgusu
            yield return new Recipe { key = "bubble.blip", volume = 0.6f, pitch = new Vector2(0.97f, 1.03f), make = Blip };
            yield return new Recipe { key = "player.death", make = DeathStinger };
            yield return new Recipe { key = "family.discover", volume = 0.85f, make = DiscoverStinger };
            yield return new Recipe { key = "family.loss", volume = 0.9f, make = LossStinger };
            yield return new Recipe { key = "music.menu", volume = 0.8f, make = s => Music(s, MenuChords, 1f, 0.2f) };
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

        /// <summary>Ahşap gıcırtısı: düzensiz takılıp kayma darbeleri, kayan rezonans</summary>
        private static float[] Creak(SoundSynth s)
        {
            float len = s.Range(0.9f, 1.5f);
            var b = Buffer(len);
            float rate = s.Range(70f, 140f), wobble = s.Range(2f, 6f);
            double next = 0;
            while (next < len)
            {
                int i = Samples((float)next);
                if (i < b.Length) b[i] = 1f;
                float t = (float)next;
                float hz = rate * (1f + 0.35f * Mathf.Sin(t * wobble)) * s.Range(0.85f, 1.15f);
                next += 1.0 / hz;
            }
            bool up = s.Rand() > 0.5f;
            float f0 = s.Range(450f, 650f), f1 = s.Range(800f, 1100f);
            var copy = (float[])b.Clone();
            BandPass(b, t => up ? Mathf.Lerp(f0, f1, t / len) : Mathf.Lerp(f1, f0, t / len), 9f);
            BandPass(copy, s.Range(1400f, 1900f), 7f);
            Mix(b, copy, 0f, 0.5f);
            Shape(b, t => Mathf.Sin(Mathf.Clamp01(t / len) * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 13f)));
            return Fade(Normalize(LowPass(b, 3500f), 0.8f));
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

        /// <summary>Cam kırılması: keskin patlama + dağılan cam parçalarının çınlamaları, biraz yankı</summary>
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

        private static float[] PowerOff(SoundSynth s)
        {
            var b = Buffer(1.2f);
            var click = Envelope(BandPass(s.White(0.03f), 1500f, 3f), 0.0003f, 0f, 160f);
            Mix(b, Normalize(click, 1f), 0f, 1f);
            Mix(b, Normalize(click, 1f), 0.055f, 0.6f);
            // Trafo uğultusu iner ve susar
            var hum = Sine(1f, t => 50f - 15f * t);
            Mix(hum, Sine(1f, t => 100f - 30f * t), 0f, 0.5f);
            Envelope(hum, 0.002f, 0.05f, 6f);
            Mix(b, hum, 0f, 0.35f);
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

        private static float[] Hit(SoundSynth s)
        {
            var b = Buffer(0.6f);
            Mix(b, Envelope(Sine(0.5f, t => 45f + 60f * Mathf.Exp(-t * 15f)), 0.002f, 0.01f, 10f), 0f, 1f);
            Mix(b, Normalize(Envelope(LowPass(s.White(0.3f), 700f), 0.001f, 0f, 25f), 1f), 0f, 0.7f);
            return Fade(Normalize(Saturate(b, 1.8f), 0.95f));
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

        /// <summary>Zaman geri sarılıyor: yükselen ters ses, sonunda kesilir</summary>
        private static float[] Rewind(SoundSynth s)
        {
            const float len = 1.6f;
            var air = BandPass(s.White(len), t => 300f + 3500f * Mathf.Pow(t / len, 2f), 1.5f);
            var tone = Sine(len, t => 180f + 500f * Mathf.Pow(t / len, 1.6f));
            Mix(air, tone, 0f, 0.15f);
            Shape(air, t => Mathf.Pow(t / len, 2.5f));
            return Fade(Normalize(air, 0.8f), 0.01f, 0.03f);
        }

        private static float[] FinalCheck(SoundSynth s)
        {
            var b = Bell(0.7f, 1046.5f, new[] { 1f, 2f, 3.01f }, new[] { 1f, 0.3f, 0.15f }, 5f);
            Mix(b, Note(0.7f, 1568f, 0.003f, 5f, 0.3f), 0.06f, 0.4f);
            return Fade(Normalize(Reverb(b, 0.6f, 0.25f, 0.6f), 0.7f));
        }

        // ------------------------------------------------------------------ Gerilim

        /// <summary>
        /// Kurtarma fazının arkasındaki gerilim (Play testi 22: alçak bas dizüstü hoparlörde duyulmuyordu; her şey
        /// orta frekanslarda). Yavaş kabaran minör akorlar (La minör, Fa, Re minör, Mi; 8'er sn), 100 vuruşta tıkırdayan
        /// tekrar notaları (sona doğru sıklaşır), kalp atışı nabzı ve titreşen ince yüksek ton. Döngü 32 sn, dikişsiz.
        /// </summary>
        private static float[] TensionMusic(SoundSynth s)
        {
            const float length = 32f, tail = 4f, chordLength = 8f;
            var b = Buffer(length + tail);
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f }, new[] { 174.61f, 220f, 261.63f },
                new[] { 146.83f, 174.61f, 220f }, new[] { 164.81f, 207.65f, 246.94f },
            };
            for (int c = 0; c < chords.Length; c++)
                foreach (float hz in chords[c])
                {
                    // Telli çalgı gibi: iki hafif akort farklı ses, yavaş atak, uzun sönüm
                    Mix(b, Note(chordLength + 2f, hz, 2f, 0.18f, 0.55f), c * chordLength, 0.13f);
                    Mix(b, Note(chordLength + 2f, hz * 1.004f, 2.2f, 0.18f, 0.55f), c * chordLength, 0.09f);
                }
            // Tekrar notaları: akorun kökü ve oktavı, sekizlik; her sekiz vuruşta bir yarım ton yukarı "gerilim" notası
            const float beat = 0.6f;
            for (int i = 0; i * beat < length; i++)
            {
                float t = i * beat;
                var chord = chords[(int)(t / chordLength) % chords.Length];
                float hz = i % 8 == 7 ? chord[0] * 2f * 1.0595f : (i % 2 == 0 ? chord[0] * 2f : chord[0]);
                float gain = (i % 2 == 0 ? 0.16f : 0.1f) * Mathf.Lerp(0.8f, 1.1f, t / length);
                Mix(b, Note(0.6f, hz, 0.003f, 7f, 0.6f), t, gain);
            }
            // Kalp atışı: tok vuruş, üstünde duyulur harmonik
            for (float t = 0f; t < length; t += beat * 2f)
            {
                Mix(b, Envelope(Sine(0.3f, u => 70f + 50f * Mathf.Exp(-u * 20f)), 0.004f, 0.01f, 18f), t, 0.35f);
                Mix(b, Envelope(Sine(0.15f, u => 160f), 0.002f, 0f, 35f), t, 0.08f);
            }
            // İnce, titreşen yüksek ton (keman tremolosu gibi), cümle sonlarında kabarır
            var high = Sine(length + tail, t => 880f * (1f + 0.002f * Mathf.Sin(t * 5f)));
            Shape(high, t => (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 7f)) * Mathf.Pow(Mathf.Repeat(t, chordLength) / chordLength, 2f));
            Mix(b, high, 0f, 0.035f);
            var wet = Reverb(b, 2.5f, 0.35f, 0.82f);
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

        // ------------------------------------------------------------------ Müzik

        // Akor kökleri (Hz) ve tipi; her akor 4 sn
        private static readonly (float root, bool minor)[] FinalChords =
            { (130.81f, false), (110f, true), (87.31f, false), (98f, false), (130.81f, false), (110f, true), (87.31f, false), (98f, false) };
        private static readonly (float root, bool minor)[] MenuChords =
            { (110f, true), (87.31f, false), (130.81f, false), (98f, false), (110f, true), (87.31f, false), (73.42f, true), (82.41f, false) };

        /// <summary>
        /// Sakin döngülü müzik: yumuşak pad akorları ve üstte seyrek arpej. Kuyruk başa bindirilir, döngü dikişsiz.
        /// </summary>
        private static float[] Music(SoundSynth s, (float root, bool minor)[] chords, float pluckStep, float pluckGain)
        {
            const float chordLength = 4f, tail = 5f;
            float length = chords.Length * chordLength;
            var b = Buffer(length + tail);
            for (int c = 0; c < chords.Length; c++)
            {
                var (root, minor) = chords[c];
                float third = root * (minor ? 1.1892f : 1.2599f), fifth = root * 1.4983f;
                float start = c * chordLength;
                foreach (float hz in new[] { root, third * 2f, fifth * 2f, root * 4f })
                    Mix(b, Note(chordLength + 2f, hz, 1.2f, 0.35f, 0.3f), start, 0.16f);
                Mix(b, Note(chordLength + 1f, root / 2f, 0.4f, 0.4f, 0.2f), start, 0.2f);

                float[] tones = { root * 4f, third * 4f, fifth * 4f, root * 8f, fifth * 4f, third * 4f };
                int n = Mathf.RoundToInt(chordLength / pluckStep);
                for (int k = 0; k < n; k++)
                {
                    if (s.Rand() < 0.25f) continue; // arada boşluk: mekanik durmasın
                    Mix(b, Note(2f, tones[(k + c) % tones.Length], 0.004f, 2.5f, 0.45f), start + k * pluckStep, pluckGain);
                }
            }
            var wet = Reverb(b, 2.5f, 0.35f, 0.82f);
            var loop = new float[Samples(length)];
            for (int i = 0; i < wet.Length; i++) loop[i % loop.Length] += wet[i];
            return Normalize(loop, 0.75f);
        }
    }
}
