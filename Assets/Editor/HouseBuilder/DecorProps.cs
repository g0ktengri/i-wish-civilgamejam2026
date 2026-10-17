using System;
using System.Collections.Generic;
using System.IO;
using Deprem.Quake;
using UnityEditor;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Odalardaki küçük düşen / yamulan dekor (Play testi 17: odalar aşırı sadeydi). Tablo, aile fotoğrafı, saat,
    /// ayna, poster, çocuk resmi duvarda; vazo, kupa, şişe masada. Her biri kameraya bakan tek pixel görsel
    /// (alpha clip) + ince çarpışma kutusu + FallingDecor. Görsel: Assets/Art/Decor/{key}.png varsa o, yoksa
    /// burada kodla çizilir (Assets/Generated/House/Props/). Camlı olanların çatlak hali ayrıca üretilir.
    /// </summary>
    internal static class DecorProps
    {
        private const string Folder = "Assets/Generated/House/Props";
        private const int Ppu = 64;

        private sealed class PropDef
        {
            public string Key;
            public string Room;
            public float X;
            /// <summary>Duvarda: görselin merkezinin zeminden yüksekliği. Masada: masanın üst yüzü.</summary>
            public float Y;
            /// <summary>Masada: görselin Z'si. Duvarda kullanılmaz (arka duvar)</summary>
            public float Z;
            public FallingDecor.MountKind Mount;
        }

        // Pencere, raflar, ayna ve çerçeve (sabitlenebilir eşyalar), kapılar ve merdivenle çakışmayacak yerler
        private static readonly PropDef[] Props =
        {
            Wall("poster_uzay", "oyuncu", 3.5f, 1.45f),
            Wall("saat", "oyuncu", 4.75f, 2.05f),
            Desk("kupa", "oyuncu", 3.45f, 0.78f, 0.45f),
            Wall("aile_fotografi", "koridor", 7.3f, 1.5f),
            Wall("tablo_manzara", "koridor", 16.2f, 1.45f),
            Desk("vazo", "koridor", 6.85f, 0.8f, 0.4f),
            Wall("tablo_soyut", "salon", 19.25f, 1.9f),
            Wall("aile_fotografi", "salon", 22f, 2.35f),
            Wall("saat", "salon", 26.3f, 1.95f),
            Desk("vazo", "salon", 25.2f, 0.78f, 0.4f),
            Wall("saat", "mutfak", 28f, 2.05f),
            Desk("kupa", "mutfak", 29.1f, 0.9f, 4.15f),
            Desk("sise", "mutfak", 31.1f, 0.9f, 4.2f),
            Wall("poster_uzay", "kardes", 1.3f, 1.35f),
            Wall("cocuk_cizimi", "kardes", 5.6f, 1.3f),
            Wall("tablo_manzara", "sahanlik", 9.9f, 1.35f),
            Wall("aile_fotografi", "sahanlik", 16.4f, 1.35f),
            Wall("ayna_kucuk", "ebeveyn", 25.3f, 1.25f),
            Wall("saat", "ebeveyn", 26.4f, 1.95f),
        };

        private static PropDef Wall(string key, string room, float x, float y) =>
            new PropDef { Key = key, Room = room, X = x, Y = y, Mount = FallingDecor.MountKind.Wall };

        private static PropDef Desk(string key, string room, float x, float top, float z) =>
            new PropDef { Key = key, Room = room, X = x, Y = top, Z = z, Mount = FallingDecor.MountKind.Surface };

        private static FallingDecor.BreakKind BreakOf(string key) => key switch
        {
            "aile_fotografi" => FallingDecor.BreakKind.Crack,
            "saat" => FallingDecor.BreakKind.Crack,
            "ayna_kucuk" => FallingDecor.BreakKind.Crack,
            "vazo" => FallingDecor.BreakKind.Shatter,
            "kupa" => FallingDecor.BreakKind.Shatter,
            "sise" => FallingDecor.BreakKind.Shatter,
            _ => FallingDecor.BreakKind.None,
        };

        /// <summary>Duvardan çıkıntı (m): çerçeveliler kalın, kağıtlar ince</summary>
        private static float DepthOf(string key) => key switch
        {
            "tablo_manzara" => 0.05f,
            "tablo_soyut" => 0.045f,
            "aile_fotografi" => 0.03f,
            "ayna_kucuk" => 0.04f,
            "saat" => 0.05f,
            _ => 0.006f, // poster, çocuk resmi
        };

        private static Color FrameColorOf(string key) => key switch
        {
            "tablo_soyut" => new Color(0.6f, 0.47f, 0.24f),
            "aile_fotografi" => new Color(0.24f, 0.16f, 0.1f),
            "saat" => new Color(0.24f, 0.16f, 0.1f),
            _ => new Color(0.4f, 0.27f, 0.16f),
        };

        // ------------------------------------------------------------------ Odaya uyan renk

        private static readonly Dictionary<string, Color> TintCache = new Dictionary<string, Color>();

        /// <summary>Odanın arka duvar dokusunun ortalama rengi, parlaklığı 1'e çekilmiş ton (doku yoksa beyaz)</summary>
        private static Color RoomTint(string roomKey)
        {
            if (TintCache.TryGetValue(roomKey, out var cached)) return cached;
            var tint = Color.white;
            string path = $"Assets/Art/Rooms/{roomKey}/arka.png";
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(File.ReadAllBytes(path)))
                {
                    var pixels = tex.GetPixels();
                    Color sum = Color.clear;
                    int n = 0;
                    for (int i = 0; i < pixels.Length; i += 7)
                    {
                        if (pixels[i].a < 0.5f) continue;
                        sum += pixels[i];
                        n++;
                    }
                    if (n > 0)
                    {
                        var avg = sum / n;
                        float luma = Mathf.Max(0.05f, avg.r * 0.3f + avg.g * 0.59f + avg.b * 0.11f);
                        tint = new Color(avg.r / luma, avg.g / luma, avg.b / luma, 1f);
                    }
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }
            TintCache[roomKey] = tint;
            return tint;
        }

        /// <summary>
        /// Rengi odanın tonuna yaklaştırır: biraz soldurur, sonra parlaklığını koruyarak oda tonuna çeker.
        /// Çizim aynı kalır, sırıtan doygun renkler (lacivert, kırmızı) odanın sıcaklığına iner.
        /// </summary>
        private static Color Grade(Color c, Color tint, float strength = 0.38f)
        {
            float luma = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            var muted = Color.Lerp(c, new Color(luma, luma, luma, c.a), 0.18f);
            var toned = new Color(luma * tint.r, luma * tint.g, luma * tint.b, c.a);
            var result = Color.Lerp(muted, toned, strength);
            result.a = c.a;
            return result;
        }

        private static void GradeCanvas(Canvas canvas, Color tint)
        {
            for (int i = 0; i < canvas.P.Length; i++)
                if (canvas.P[i].a > 0f) canvas.P[i] = Grade(canvas.P[i], tint);
        }

        private static Color ShardColorOf(string key) => key switch
        {
            "vazo" => new Color(0.3f, 0.45f, 0.7f),
            "kupa" => new Color(0.92f, 0.9f, 0.85f),
            "sise" => new Color(0.3f, 0.55f, 0.35f),
            _ => new Color(0.78f, 0.86f, 0.92f),
        };

        public static void Build(Transform root, Func<string, Color, Material> material)
        {
            var parent = new GameObject("DecorProps").transform;
            parent.SetParent(root, false);
            var shard = material("dekor_parca", Color.white);
            var cache = new Dictionary<string, (Material mat, Texture2D broken, Vector2 size)>();
            for (int i = 0; i < Props.Length; i++)
            {
                var def = Props[i];
                // Play testi 18: renkler odaya uysun; her oda için ayrı renk ayarlı doku
                string lookKey = def.Key + "_" + def.Room;
                if (!cache.TryGetValue(lookKey, out var look))
                {
                    look = Look(def.Key, def.Room);
                    cache[lookKey] = look;
                }
                var room = HouseLayout.Room(def.Room);
                var size = look.size;
                float depth = DepthOf(def.Key);
                Vector3 center = def.Mount == FallingDecor.MountKind.Wall
                    ? new Vector3(def.X, room.FloorY + def.Y, room.Depth - depth * 0.5f - 0.004f)
                    : new Vector3(def.X, room.FloorY + def.Y + size.y * 0.5f, def.Z);

                var go = new GameObject($"Prop_{i:00}_{def.Key}");
                go.transform.SetParent(parent, false);
                go.transform.position = center;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "View";
                UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localScale = new Vector3(size.x, size.y, 1f);
                if (def.Mount == FallingDecor.MountKind.Wall) quad.transform.localPosition = new Vector3(0f, 0f, -depth * 0.5f - 0.001f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = look.mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                // Play testi 18: düz kağıt gibiydi. Çerçeveli olanların arkasında çerçeve kalınlığında gövde
                // (yanları perspektifte görünür); saat yuvarlak
                if (def.Mount == FallingDecor.MountKind.Wall && depth > 0.01f)
                {
                    var bodyColor = Grade(FrameColorOf(def.Key), RoomTint(def.Room));
                    var bodyMat = material($"dekor_govde_{def.Key}_{def.Room}", bodyColor);
                    var shape = GameObject.CreatePrimitive(def.Key == "saat" ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    shape.name = "Depth";
                    UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
                    shape.transform.SetParent(go.transform, false);
                    if (def.Key == "saat")
                    {
                        shape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        shape.transform.localScale = new Vector3(size.x * 0.94f, depth * 0.5f, size.y * 0.94f);
                    }
                    else shape.transform.localScale = new Vector3(size.x * 0.97f, size.y * 0.97f, depth);
                    shape.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
                }

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(size.x * 0.9f, size.y * 0.95f, def.Mount == FallingDecor.MountKind.Wall ? Mathf.Max(0.03f, depth) : Mathf.Min(size.x, 0.12f));
                var body = go.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = 1f;
                var decor = go.AddComponent<FallingDecor>();
                var so = new SerializedObject(decor);
                so.FindProperty("mount").enumValueIndex = (int)def.Mount;
                so.FindProperty("breakKind").enumValueIndex = (int)BreakOf(def.Key);
                so.FindProperty("view").objectReferenceValue = renderer;
                so.FindProperty("brokenTexture").objectReferenceValue = look.broken;
                so.FindProperty("shardColor").colorValue = ShardColorOf(def.Key);
                so.FindProperty("shardMaterial").objectReferenceValue = shard;
                so.FindProperty("halfHeight").floatValue = size.y * 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Oyuncunun odasında yere serili halı (Play testi 24, ekip çizimi Assets/Art/Decor/hali_oyuncu.png).
        /// Zeminin hemen üstünde yatay bir quad; çarpışmasız, gölge alır.
        /// </summary>
        public static void BuildRug(Transform root, string roomKey, string artPath, float centerX, float centerZ, float width)
        {
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath);
            if (art == null) return;
            var room = HouseLayout.Room(roomKey);
            EnsureFolder();
            string matPath = $"{Folder}/{Path.GetFileNameWithoutExtension(artPath)}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", art);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(mat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Rug_" + roomKey;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            // Quad'ın üst kenarı (+Y) arka duvara (+Z) baksın: çizim kameradan bakınca düz durur
            quad.transform.SetPositionAndRotation(new Vector3(centerX, room.FloorY + 0.006f, centerZ), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(width, width * art.height / art.width, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        /// <summary>
        /// Oyuncunun odasının sol (dış) duvarında camlı pencere (Play testi 18). Yan duvarın iç yüzünde, odaya
        /// bakar; gece göğü hafifçe parlar. Assets/Art/Decor/pencere_yan.png gelirse o kullanılır.
        /// </summary>
        public static void BuildSideWindow(Transform root, Func<string, Color, Material> material)
        {
            var room = HouseLayout.Room("oyuncu");
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Decor/pencere_yan.png");
            EnsureFolder();
            var texture = art != null ? art : Save($"{Folder}/pencere_yan.png", SideWindow());
            string matPath = $"{Folder}/pencere_yan.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", new Color(0.22f, 0.24f, 0.32f)); // ay ışığı, karanlıkta seçilsin
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Window_OyuncuSol";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            float w = texture.width / (float)Ppu, h = texture.height / (float)Ppu;
            quad.transform.SetPositionAndRotation(
                new Vector3(room.X0 + HouseDims.Thickness * 0.5f + 0.01f, room.FloorY + 1.65f, 2.3f),
                Quaternion.Euler(0f, -90f, 0f)); // odaya (+X) bakar
            quad.transform.localScale = new Vector3(w, h, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (art != null) return; // sanatçı çiziminde kasa yerleri bilinmiyor

            // Play testi 18: pencere düz resim gibiydi. Kasa, kayıtlar, denizlik ve korniş duvardan çıkar; cam içeride
            // kalır. Ölçüler çizimin pikselleriyle aynı (SideWindow: 64 x 70, sol üstten)
            float wall = room.X0 + HouseDims.Thickness * 0.5f;
            var c = quad.transform.position;
            void Part(string name, int u0, int v0, int u1, int v1, float outward, Color color)
            {
                float z0 = c.z + (u0 / 64f - 0.5f) * w, z1 = c.z + ((u1 + 1) / 64f - 0.5f) * w;
                float yTop = c.y + (0.5f - v0 / 70f) * h, yBottom = c.y + (0.5f - (v1 + 1) / 70f) * h;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(quad.transform.parent, false);
                cube.transform.position = new Vector3(wall + outward * 0.5f, (yTop + yBottom) * 0.5f, (z0 + z1) * 0.5f);
                cube.transform.localScale = new Vector3(outward, yTop - yBottom, z1 - z0);
                cube.GetComponent<MeshRenderer>().sharedMaterial = material($"pencere_{name}", color);
            }
            var frame = new Color(0.86f, 0.84f, 0.78f);
            var frameShade = new Color(0.66f, 0.64f, 0.6f);
            Part("KasaUst", 8, 6, 55, 8, 0.045f, frame);
            Part("KasaAlt", 8, 59, 55, 61, 0.045f, frameShade);
            Part("KasaSol", 8, 6, 10, 61, 0.045f, frame);
            Part("KasaSag", 53, 6, 55, 61, 0.045f, frameShade);
            Part("KayitDikey", 31, 9, 32, 58, 0.03f, frame);
            Part("KayitYatay", 11, 33, 52, 34, 0.03f, frame);
            Part("Denizlik", 6, 62, 57, 64, 0.12f, new Color(0.9f, 0.88f, 0.82f));
            Part("Korniş", 0, 1, 63, 2, 0.1f, new Color(0.3f, 0.22f, 0.15f));
            Part("KornişUcSol", 0, 0, 1, 3, 0.12f, new Color(0.24f, 0.17f, 0.11f));
            Part("KornişUcSag", 62, 0, 63, 3, 0.12f, new Color(0.24f, 0.17f, 0.11f));
        }

        // Gece penceresi: beyaz ahşap kasa, artı kayıt, dört cam (gök, ay, yıldız, uzak ev ışıkları), yanlarda perde, denizlik
        private static Canvas SideWindow()
        {
            var c = new Canvas(64, 70);
            var skyTop = new Color(0.1f, 0.13f, 0.27f);
            var skyLow = new Color(0.32f, 0.3f, 0.48f);
            for (int y = 8; y < 60; y++)
                for (int x = 10; x < 54; x++) c.Set(x, y, Color.Lerp(skyTop, skyLow, (y - 8) / 52f));
            var rng = new System.Random(21);
            for (int i = 0; i < 12; i++) c.Set(12 + rng.Next(40), 10 + rng.Next(28), new Color(0.95f, 0.93f, 0.8f));
            c.Disc(42, 18, 4.5f, new Color(0.96f, 0.92f, 0.72f));
            // Uzakta evlerin silueti ve sarı pencereleri
            var town = new Color(0.12f, 0.12f, 0.2f);
            for (int x = 10; x < 54; x++)
            {
                int top = 48 - (x / 6 % 3) * 3 - (x % 11 < 3 ? 2 : 0);
                for (int y = top; y < 60; y++) c.Set(x, y, town);
            }
            for (int i = 0; i < 7; i++) c.Set(12 + rng.Next(40), 50 + rng.Next(8), new Color(0.95f, 0.8f, 0.4f));
            // Kasa ve kayıt
            var frame = new Color(0.86f, 0.84f, 0.78f);
            var frameDark = new Color(0.6f, 0.58f, 0.54f);
            c.Rect(8, 6, 55, 8, frame);
            c.Rect(8, 59, 55, 61, frameDark);
            c.Rect(8, 6, 10, 61, frame);
            c.Rect(53, 6, 55, 61, frameDark);
            c.Rect(31, 6, 32, 61, frame);
            c.Rect(8, 33, 55, 34, frame);
            // Camda parıltı
            for (int i = 0; i < 7; i++) c.Set(14 + i, 28 - i, Color.Lerp(c.Get(14 + i, 28 - i), Color.white, 0.4f));
            for (int i = 0; i < 5; i++) c.Set(37 + i, 56 - i, Color.Lerp(c.Get(37 + i, 56 - i), Color.white, 0.3f));
            // Perdeler (dalgalı kıvrımlar) ve korniş
            var curtain = new Color(0.36f, 0.48f, 0.7f);
            var curtainDark = new Color(0.25f, 0.35f, 0.55f);
            for (int y = 3; y < 66; y++)
            {
                int wl = 9 + (y > 40 ? (y - 40) / 6 : 0), wr = 9 + (y > 40 ? (y - 40) / 6 : 0);
                for (int x = 0; x < wl; x++) c.Set(x, y, x % 4 == 1 ? curtainDark : curtain);
                for (int x = 63 - wr; x < 64; x++) c.Set(x, y, x % 4 == 2 ? curtainDark : curtain);
            }
            c.Rect(0, 1, 63, 2, new Color(0.3f, 0.22f, 0.15f));
            // Denizlik
            c.Rect(6, 62, 57, 64, new Color(0.9f, 0.88f, 0.82f));
            c.Rect(6, 65, 57, 65, frameDark);
            return c;
        }

        // ------------------------------------------------------------------ Görsel

        private static (Material mat, Texture2D broken, Vector2 size) Look(string key, string roomKey)
        {
            EnsureFolder();
            // Eski, odaya göre ayarlanmamış dosyalar (Play testi 17) kaldırılır
            foreach (var stale in new[] { $"{Folder}/{key}.png", $"{Folder}/{key}_kirik.png", $"{Folder}/{key}.mat" })
                if (AssetDatabase.LoadMainAssetAtPath(stale) != null) AssetDatabase.DeleteAsset(stale);

            string name = $"{key}_{roomKey}";
            var art = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/Decor/{key}.png");
            Texture2D texture, broken = null;
            if (art != null) texture = art;
            else
            {
                var canvas = Draw(key);
                GradeCanvas(canvas, RoomTint(roomKey));
                texture = Save($"{Folder}/{name}.png", canvas);
                if (BreakOf(key) == FallingDecor.BreakKind.Crack)
                {
                    Crack(canvas, key);
                    broken = Save($"{Folder}/{name}_kirik.png", canvas);
                }
            }

            string matPath = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Smoothness", 0.15f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return (mat, broken, new Vector2(texture.width / (float)Ppu, texture.height / (float)Ppu));
        }

        /// <summary>
        /// Çizim tuvali. Tasarım koordinatlarıyla çizilir, gerçek boyut tasarım × k (Play testi 18: dekor küçüktü).
        /// Büyütme pikselleri büyütmez: aynı resim daha çok pikselle çizilir, evin 64 px/m yoğunluğu korunur.
        /// Çizgiler gerçek boyutta 1 px kalır.
        /// </summary>
        private sealed class Canvas
        {
            public readonly int W, H; // gerçek piksel
            public readonly float K;
            public readonly Color[] P;
            public Canvas(int designW, int designH, float k = 1f)
            {
                K = k;
                W = Mathf.Max(1, Mathf.RoundToInt(designW * k));
                H = Mathf.Max(1, Mathf.RoundToInt(designH * k));
                P = new Color[W * H];
            }

            private int Lo(float v) => Mathf.FloorToInt(v * K + 0.001f);
            private int Hi(float v) => Mathf.Max(Lo(v), Mathf.CeilToInt((v + 1f) * K - 0.001f) - 1);

            // Gerçek piksel, sol üstten
            public void SetPx(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                P[(H - 1 - y) * W + x] = c;
            }

            public Color GetPx(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Color.clear : P[(H - 1 - y) * W + x];

            // Tasarım koordinatı (sol üstten): bir tasarım pikselinin kapladığı gerçek pikseller
            public void Set(int x, int y, Color c) => Rect(x, y, x, y, c);
            public Color Get(int x, int y) => GetPx(Lo(x), Lo(y));

            public void Rect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = Lo(y0); y <= Hi(y1); y++)
                for (int x = Lo(x0); x <= Hi(x1); x++) SetPx(x, y, c);
            }

            public void Disc(float cx, float cy, float r, Color c)
            {
                float rx = cx * K, ry = cy * K, rr = r * K;
                for (int y = (int)(ry - rr - 1); y <= ry + rr + 1; y++)
                for (int x = (int)(rx - rr - 1); x <= rx + rr + 1; x++)
                    if ((x + 0.5f - rx) * (x + 0.5f - rx) + (y + 0.5f - ry) * (y + 0.5f - ry) <= rr * rr) SetPx(x, y, c);
            }

            public void Line(int x0, int y0, int x1, int y1, Color c)
            {
                float ax = (x0 + 0.5f) * K, ay = (y0 + 0.5f) * K, bx = (x1 + 0.5f) * K, by = (y1 + 0.5f) * K;
                int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)));
                for (int i = 0; i <= n; i++)
                {
                    float t = n == 0 ? 0f : i / (float)n;
                    SetPx(Mathf.FloorToInt(Mathf.Lerp(ax, bx, t)), Mathf.FloorToInt(Mathf.Lerp(ay, by, t)), c);
                }
            }

            /// <summary>Kabartmalı çerçeve (genişlik tasarım pikseli): dış koyu kontur, üst-sol açık, alt-sağ koyu</summary>
            public void Frame(int designWidth, Color mid)
            {
                int width = Mathf.Max(2, Mathf.RoundToInt(designWidth * K));
                var light = Color.Lerp(mid, Color.white, 0.3f);
                var dark = Color.Lerp(mid, Color.black, 0.35f);
                var outline = Color.Lerp(mid, Color.black, 0.65f);
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, W - 1 - x), Mathf.Min(y, H - 1 - y));
                    if (d >= width) continue;
                    Color c = d == 0 ? outline : mid;
                    if (d > 0 && (x == W - 1 - d || y == H - 1 - d)) c = dark;
                    if (d > 0 && (x == d || y == d)) c = light;
                    SetPx(x, y, c);
                }
            }
        }

        // Tasarım ölçeği: duvar resimleri ve aynalar büyük, masadakiler biraz
        private const float WallScale = 1.35f;
        private const float SmallScale = 1.2f;

        private static readonly Color Wood = new Color(0.5f, 0.34f, 0.2f);
        private static readonly Color DarkWood = new Color(0.3f, 0.2f, 0.13f);
        private static readonly Color Gold = new Color(0.72f, 0.58f, 0.3f);
        private static readonly Color Paper = new Color(0.93f, 0.9f, 0.82f);
        private static readonly Color Ink = new Color(0.16f, 0.13f, 0.12f);
        private static readonly Color Skin = new Color(0.86f, 0.68f, 0.54f);
        private static readonly Color Hair = new Color(0.25f, 0.17f, 0.12f);

        private static Canvas Draw(string key)
        {
            switch (key)
            {
                case "tablo_manzara": return Landscape();
                case "tablo_soyut": return Abstract();
                case "aile_fotografi": return FamilyPhoto();
                case "saat": return Clock();
                case "ayna_kucuk": return Mirror();
                case "poster_uzay": return SpacePoster();
                case "cocuk_cizimi": return ChildDrawing();
                case "vazo": return Vase();
                case "kupa": return Mug();
                case "sise": return Bottle();
                default: throw new ArgumentException(key);
            }
        }

        // Gece manzarası: dağlar, göl, ay, yıldızlar; ahşap çerçeve
        private static Canvas Landscape()
        {
            var c = new Canvas(58, 38, WallScale);
            var top = new Color(0.12f, 0.15f, 0.3f);
            var horizon = new Color(0.45f, 0.36f, 0.5f);
            for (int y = 0; y < 38; y++)
                for (int x = 0; x < 58; x++) c.Set(x, y, Color.Lerp(top, horizon, y / 26f));
            var rng = new System.Random(3);
            for (int i = 0; i < 14; i++) c.Set(4 + rng.Next(50), 4 + rng.Next(14), new Color(0.95f, 0.92f, 0.8f));
            c.Disc(42, 10, 4f, new Color(0.96f, 0.9f, 0.68f));
            c.Disc(43.5f, 9f, 3.4f, Color.Lerp(top, horizon, 0.35f)); // hilal
            var far = new Color(0.27f, 0.25f, 0.4f);
            var near = new Color(0.15f, 0.17f, 0.25f);
            for (int x = 0; x < 58; x++)
            {
                int hFar = 20 + (int)(5 * Mathf.Sin(x * 0.17f) + 3 * Mathf.Sin(x * 0.41f + 1));
                int hNear = 25 + (int)(4 * Mathf.Sin(x * 0.23f + 2) + 2 * Mathf.Sin(x * 0.6f));
                for (int y = hFar; y < 28; y++) c.Set(x, y, far);
                for (int y = hNear; y < 28; y++) c.Set(x, y, near);
                if (hFar < hNear - 1) c.Set(x, hFar, Color.Lerp(far, Color.white, 0.25f)); // kar
            }
            var lake = new Color(0.2f, 0.26f, 0.42f);
            c.Rect(0, 28, 57, 37, lake);
            for (int i = 0; i < 6; i++) c.Rect(38 + rng.Next(6), 29 + i, 41 + rng.Next(5), 29 + i, new Color(0.7f, 0.66f, 0.55f));
            c.Frame(3, Wood);
            return c;
        }

        // Soyut resim: krem zemin, kırmızı daire, hardal ve petrol mavisi bloklar, siyah çizgiler; ince altın çerçeve
        private static Canvas Abstract()
        {
            var c = new Canvas(38, 51, WallScale);
            c.Rect(0, 0, 37, 50, new Color(0.9f, 0.86f, 0.76f));
            c.Rect(5, 30, 22, 46, new Color(0.82f, 0.64f, 0.25f));
            c.Rect(22, 8, 33, 26, new Color(0.18f, 0.42f, 0.45f));
            c.Disc(15, 17, 8.5f, new Color(0.72f, 0.22f, 0.18f));
            c.Line(4, 28, 34, 28, Ink);
            c.Line(22, 4, 22, 47, Ink);
            c.Line(4, 47, 34, 40, Ink);
            c.Frame(2, Gold);
            return c;
        }

        // Aile fotoğrafı: dört kişi (baba, anne, oyuncu, kardeş), beyaz paspartu, koyu çerçeve, camda parıltı
        private static Canvas FamilyPhoto()
        {
            var c = new Canvas(30, 23, WallScale);
            c.Rect(0, 0, 29, 22, Paper);
            c.Rect(4, 4, 25, 18, new Color(0.62f, 0.72f, 0.66f));
            c.Rect(4, 14, 25, 18, new Color(0.45f, 0.55f, 0.38f));
            void Person(int x, int headY, int h, Color shirt)
            {
                c.Rect(x - 1, headY, x + 1, headY + 2, Skin);
                c.Rect(x - 1, headY - 1, x + 1, headY - 1, Hair);
                c.Rect(x - 2, headY + 3, x + 2, Mathf.Min(18, headY + 3 + h), shirt);
            }
            Person(8, 6, 9, new Color(0.25f, 0.35f, 0.6f));
            Person(13, 7, 8, new Color(0.65f, 0.25f, 0.28f));
            Person(18, 9, 6, new Color(0.45f, 0.42f, 0.4f));
            Person(22, 11, 4, new Color(0.85f, 0.7f, 0.25f));
            c.Frame(3, DarkWood);
            for (int i = 0; i < 6; i++) c.Set(6 + i, 11 - i, Color.Lerp(c.Get(6 + i, 11 - i), Color.white, 0.5f));
            for (int i = 0; i < 4; i++) c.Set(9 + i, 11 - i, Color.Lerp(c.Get(9 + i, 11 - i), Color.white, 0.35f));
            return c;
        }

        // Duvar saati: koyu çerçeve, krem kadran, işaretler, akrep ve yelkovan, kırmızı merkez
        private static Canvas Clock()
        {
            var c = new Canvas(24, 24, SmallScale);
            c.Disc(12, 12, 11.5f, Ink);
            c.Disc(12, 12, 10.5f, DarkWood);
            c.Disc(12, 12, 9f, new Color(0.95f, 0.92f, 0.84f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                int x = Mathf.RoundToInt(12 + Mathf.Sin(a) * 7.5f - 0.5f), y = Mathf.RoundToInt(12 - Mathf.Cos(a) * 7.5f - 0.5f);
                c.Set(x, y, i % 3 == 0 ? Ink : new Color(0.5f, 0.46f, 0.42f));
            }
            c.Line(12, 12, 8, 9, Ink);   // akrep (10'a)
            c.Line(12, 12, 15, 5, Ink);  // yelkovan (2'ye)
            c.Set(12, 12, new Color(0.75f, 0.2f, 0.2f));
            c.Set(8, 6, Color.Lerp(new Color(0.95f, 0.92f, 0.84f), Color.white, 0.8f));
            c.Set(9, 6, Color.white);
            return c;
        }

        // Küçük duvar aynası: altın kenarlı ahşap çerçeve, açık mavi-gri cam, iki çapraz parıltı
        private static Canvas Mirror()
        {
            var c = new Canvas(29, 42, WallScale);
            for (int y = 0; y < 42; y++)
                for (int x = 0; x < 29; x++) c.Set(x, y, Color.Lerp(new Color(0.72f, 0.8f, 0.84f), new Color(0.5f, 0.58f, 0.66f), y / 42f));
            c.Frame(3, Wood);
            c.Rect(0, 0, 2, 2, Gold);
            c.Rect(26, 0, 28, 2, Gold);
            c.Rect(0, 39, 2, 41, Gold);
            c.Rect(26, 39, 28, 41, Gold);
            for (int i = 0; i < 10; i++) c.Set(6 + i, 22 - i * 1, Color.Lerp(c.Get(6 + i, 22 - i), Color.white, 0.55f));
            for (int i = 0; i < 6; i++) c.Set(10 + i, 22 - i * 1, Color.Lerp(c.Get(10 + i, 22 - i), Color.white, 0.4f));
            return c;
        }

        // Uzay posteri: lacivert zemin, halkalı turuncu gezegen, yıldızlar, üstte başlık şeridi; köşeler bantlı
        private static Canvas SpacePoster()
        {
            var c = new Canvas(32, 45, WallScale);
            c.Rect(0, 0, 31, 44, Paper);
            c.Rect(2, 2, 29, 42, new Color(0.1f, 0.12f, 0.28f));
            var rng = new System.Random(9);
            for (int i = 0; i < 16; i++) c.Set(3 + rng.Next(26), 9 + rng.Next(32), new Color(0.95f, 0.93f, 0.8f));
            c.Disc(16, 26, 7f, new Color(0.88f, 0.52f, 0.24f));
            c.Rect(10, 25, 22, 25, new Color(0.7f, 0.38f, 0.2f));
            c.Line(5, 29, 27, 23, new Color(0.92f, 0.85f, 0.6f));
            c.Rect(4, 4, 27, 7, new Color(0.9f, 0.82f, 0.3f));
            for (int x = 6; x < 26; x += 3) c.Rect(x, 5, x + 1, 6, Ink);
            var tape = new Color(0.9f, 0.88f, 0.75f, 1f);
            c.Rect(0, 0, 3, 1, tape);
            c.Rect(28, 0, 31, 1, tape);
            return c;
        }

        // Kardeşin pastel boya resmi: ev, güneş, çimen, üç çöp adam; bantla asılı kağıt
        private static Canvas ChildDrawing()
        {
            var c = new Canvas(32, 26, WallScale);
            c.Rect(0, 0, 31, 25, new Color(0.97f, 0.96f, 0.92f));
            c.Rect(0, 21, 31, 23, new Color(0.35f, 0.7f, 0.3f));
            c.Rect(5, 12, 14, 20, new Color(0.65f, 0.42f, 0.25f));
            for (int i = 0; i < 6; i++) c.Rect(4 + i, 11 - i, 15 - i, 11 - i, new Color(0.85f, 0.25f, 0.2f));
            c.Rect(9, 16, 10, 20, new Color(0.3f, 0.2f, 0.15f));
            c.Disc(26, 5, 3f, new Color(0.98f, 0.82f, 0.2f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                c.Set(Mathf.RoundToInt(26 + Mathf.Cos(a) * 5f - 0.5f), Mathf.RoundToInt(5 + Mathf.Sin(a) * 5f - 0.5f), new Color(0.98f, 0.82f, 0.2f));
            }
            void Stick(int x, int h, Color col)
            {
                c.Set(x, 20 - h, col);
                c.Line(x, 21 - h, x, 18, col);
                c.Line(x - 1, 22 - h + 1, x + 1, 22 - h + 1, col);
                c.Set(x - 1, 20, col);
                c.Set(x + 1, 20, col);
            }
            Stick(19, 7, new Color(0.2f, 0.3f, 0.75f));
            Stick(22, 6, new Color(0.75f, 0.2f, 0.45f));
            Stick(25, 4, new Color(0.9f, 0.55f, 0.1f));
            var tape = new Color(0.86f, 0.84f, 0.72f);
            c.Rect(14, 0, 17, 1, tape);
            return c;
        }

        private static Canvas Vase()
        {
            var c = new Canvas(13, 26, SmallScale);
            var blue = new Color(0.25f, 0.4f, 0.68f);
            var blueDark = new Color(0.16f, 0.26f, 0.48f);
            for (int y = 12; y < 26; y++)
            {
                float t = (y - 12) / 13f;
                int half = Mathf.RoundToInt(2 + 3.5f * Mathf.Sin(t * Mathf.PI * 0.9f + 0.2f));
                for (int x = 6 - half; x <= 6 + half; x++) c.Set(x, y, x == 6 - half || x == 6 + half ? blueDark : blue);
                c.Set(6 - half + 1, y, Color.Lerp(blue, Color.white, 0.35f));
            }
            c.Rect(4, 25, 8, 25, blueDark);
            var stem = new Color(0.25f, 0.5f, 0.25f);
            c.Line(6, 12, 6, 4, stem);
            c.Line(6, 11, 3, 6, stem);
            c.Line(6, 11, 10, 5, stem);
            c.Disc(6, 3, 1.8f, new Color(0.85f, 0.25f, 0.3f));
            c.Disc(3, 5, 1.6f, new Color(0.95f, 0.8f, 0.25f));
            c.Disc(10, 4, 1.6f, new Color(0.9f, 0.5f, 0.6f));
            return c;
        }

        private static Canvas Mug()
        {
            var c = new Canvas(10, 9);
            var white = new Color(0.93f, 0.91f, 0.86f);
            c.Rect(1, 1, 6, 8, white);
            c.Rect(1, 3, 6, 4, new Color(0.75f, 0.2f, 0.2f));
            c.Rect(7, 3, 8, 3, white);
            c.Rect(8, 3, 8, 6, white);
            c.Rect(7, 6, 8, 6, white);
            c.Rect(1, 1, 6, 1, new Color(0.35f, 0.25f, 0.18f)); // içindeki çay
            c.Rect(6, 1, 6, 8, new Color(0.7f, 0.68f, 0.64f));
            return c;
        }

        private static Canvas Bottle()
        {
            var c = new Canvas(7, 19, SmallScale);
            var glass = new Color(0.25f, 0.5f, 0.32f);
            c.Rect(3, 0, 3, 0, new Color(0.6f, 0.45f, 0.25f));
            c.Rect(2, 1, 4, 5, glass);
            c.Rect(1, 6, 5, 18, glass);
            c.Rect(1, 10, 5, 13, Paper);
            c.Rect(2, 6, 2, 17, Color.Lerp(glass, Color.white, 0.4f));
            return c;
        }

        /// <summary>Camda çatlak: vuruş noktasından dışa kırık çizgiler (açık) ve gölgeleri (koyu)</summary>
        private static void Crack(Canvas c, string key)
        {
            var rng = new System.Random(key.GetHashCode() & 0x7fff);
            int border = Mathf.RoundToInt(3 * c.K);
            int cx = c.W / 2 + rng.Next(-3, 4), cy = c.H / 2 + rng.Next(-3, 4);
            for (int r = 0; r < 7; r++)
            {
                float a = r * Mathf.PI * 2f / 7f + (float)rng.NextDouble() * 0.6f;
                float x = cx, y = cy;
                int len = Mathf.Max(c.W, c.H);
                for (int i = 0; i < len; i++)
                {
                    a += ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a);
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    if (ix < border || iy < border || ix >= c.W - border || iy >= c.H - border) break;
                    if (c.GetPx(ix, iy).a < 0.5f) break;
                    c.SetPx(ix, iy, Color.Lerp(c.GetPx(ix, iy), Color.white, 0.75f));
                    c.SetPx(ix + 1, iy + 1, Color.Lerp(c.GetPx(ix + 1, iy + 1), Color.black, 0.3f));
                }
            }
            c.SetPx(cx, cy, Color.white);
        }

        private static Texture2D Save(string path, Canvas canvas)
        {
            var tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false);
            tex.SetPixels(canvas.P);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(png))
                File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            if (!AssetDatabase.IsValidFolder("Assets/Generated/House")) AssetDatabase.CreateFolder("Assets/Generated", "House");
            AssetDatabase.CreateFolder("Assets/Generated/House", "Props");
        }
    }
}
