using System.Collections.Generic;
using System.Linq;
using Deprem.Core;
using UnityEditor;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Çanta malzemelerinin dünyadaki modelleri (Play testi 10: renkli küplerin ne olduğu anlaşılmıyordu).
    /// Kutu parçalarından, düz renkli, pixel stile uyan modeller; okunsun diye gerçeğinden biraz iri.
    /// Prefab: Assets/Prefabs/Mehmet/BagItems/BagItem_{BagItemId}.prefab, pivot taban ortası, ön yüz -Z.
    /// Her prefab'da model, aynı mesh'le sarı kontur (Deprem/Outline) ve Visual.PickupHighlight var.
    /// Berke'nin BagSupply'ı bu prefab'ı malzemenin işaret noktasına koyar (ENTEGRASYON, Play testi 10).
    /// </summary>
    internal static class BagItemModels
    {
        public const string PrefabFolder = "Assets/Prefabs/Mehmet/BagItems";
        private const string MaterialFolder = "Assets/Generated/BagItems";

        private struct Part
        {
            public Vector3 Min;
            public Vector3 Max;
            public string Color;

            public Part(float x0, float y0, float z0, float x1, float y1, float z1, string color)
            {
                Min = new Vector3(x0, y0, z0);
                Max = new Vector3(x1, y1, z1);
                Color = color;
            }
        }

        // Berke'nin sönük paletiyle uyumlu, ama malzemeler seçilsin diye biraz daha canlı
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "kirmizi", new Color(0.72f, 0.2f, 0.17f) },
            { "koyu_kirmizi", new Color(0.45f, 0.12f, 0.1f) },
            { "beyaz", new Color(0.92f, 0.9f, 0.85f) },
            { "metal", new Color(0.55f, 0.55f, 0.56f) },
            { "siyah", new Color(0.12f, 0.12f, 0.13f) },
            { "sari", new Color(0.93f, 0.72f, 0.18f) },
            { "lens", new Color(0.98f, 0.96f, 0.82f) },
            { "mavi", new Color(0.42f, 0.62f, 0.82f) },
            { "koyu_mavi", new Color(0.18f, 0.28f, 0.5f) },
            { "turuncu", new Color(0.9f, 0.48f, 0.15f) },
            { "koyu_turuncu", new Color(0.62f, 0.3f, 0.1f) },
            { "reflektor", new Color(0.8f, 0.82f, 0.8f) },
        };

        /// <summary>Kabinin önündeki deprem çantası (Berke'nin BagDropPoint'i kullanır, ENTEGRASYON M23)</summary>
        public const string EarthquakeBagPath = PrefabFolder + "/EarthquakeBag.prefab";

        [MenuItem("Tools/Deprem/Çanta Malzemesi Modellerini Üret")]
        public static void Generate()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            Build("BagItem_" + BagItemId.FirstAidKit, FirstAidKit(), PrefabPath(BagItemId.FirstAidKit));
            Build("BagItem_" + BagItemId.Flashlight, Flashlight(), PrefabPath(BagItemId.Flashlight));
            Build("BagItem_" + BagItemId.Water, Water(), PrefabPath(BagItemId.Water));
            Build("BagItem_" + BagItemId.Whistle, Whistle(), PrefabPath(BagItemId.Whistle));
            Build("EarthquakeBag", EarthquakeBag(), EarthquakeBagPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BagItemModels] Çanta malzemesi modelleri üretildi: " + PrefabFolder);
        }

        public static string PrefabPath(BagItemId id) => $"{PrefabFolder}/BagItem_{id}.prefab";

        // ------------------------------------------------------------------ Modeller (x sağa, y yukarı, -z ön)

        /// <summary>Kırmızı sert çanta, önünde beyaz haç, üstünde kulp, iki kilit</summary>
        private static List<Part> FirstAidKit() => new List<Part>
        {
            new Part(-0.28f, 0.04f, -0.11f, 0.28f, 0.38f, 0.11f, "kirmizi"),
            new Part(-0.29f, 0f, -0.12f, 0.29f, 0.05f, 0.12f, "koyu_kirmizi"),      // alt kapak şeridi
            new Part(-0.29f, 0.24f, -0.12f, 0.29f, 0.26f, 0.12f, "koyu_kirmizi"),   // kapak çizgisi
            new Part(-0.04f, 0.07f, -0.13f, 0.04f, 0.23f, -0.11f, "beyaz"),         // haç, dikey
            new Part(-0.12f, 0.11f, -0.13f, 0.12f, 0.19f, -0.11f, "beyaz"),         // haç, yatay
            new Part(-0.22f, 0.27f, -0.13f, -0.17f, 0.33f, -0.11f, "metal"),        // kilitler
            new Part(0.17f, 0.27f, -0.13f, 0.22f, 0.33f, -0.11f, "metal"),
            new Part(-0.13f, 0.38f, -0.02f, -0.1f, 0.45f, 0.02f, "siyah"),          // kulp
            new Part(0.1f, 0.38f, -0.02f, 0.13f, 0.45f, 0.02f, "siyah"),
            new Part(-0.13f, 0.45f, -0.025f, 0.13f, 0.48f, 0.025f, "siyah"),
        };

        /// <summary>Yatay duran sarı el feneri: kauçuk halkalı gövde, geniş baş, parlak lens, düğme</summary>
        private static List<Part> Flashlight() => new List<Part>
        {
            new Part(-0.24f, 0.02f, -0.045f, 0.1f, 0.11f, 0.045f, "sari"),         // gövde
            new Part(-0.25f, 0.025f, -0.04f, -0.24f, 0.105f, 0.04f, "siyah"),      // arka kapak
            new Part(-0.2f, 0.015f, -0.05f, -0.17f, 0.115f, 0.05f, "siyah"),       // kauçuk halkalar
            new Part(-0.12f, 0.015f, -0.05f, -0.09f, 0.115f, 0.05f, "siyah"),
            new Part(0.1f, 0.01f, -0.055f, 0.13f, 0.12f, 0.055f, "metal"),         // boyun
            new Part(0.13f, 0f, -0.07f, 0.24f, 0.13f, 0.07f, "sari"),              // baş
            new Part(0.24f, 0.015f, -0.055f, 0.255f, 0.115f, 0.055f, "lens"),      // lens
            new Part(-0.04f, 0.11f, -0.02f, 0.03f, 0.135f, 0.02f, "siyah"),        // düğme
        };

        /// <summary>Su şişesi: mavi gövde, beyaz etiket, omuz ve kapak</summary>
        private static List<Part> Water() => new List<Part>
        {
            new Part(-0.07f, 0f, -0.07f, 0.07f, 0.32f, 0.07f, "mavi"),
            new Part(-0.075f, 0.1f, -0.075f, 0.075f, 0.2f, 0.075f, "beyaz"),       // etiket
            new Part(-0.05f, 0.32f, -0.05f, 0.05f, 0.36f, 0.05f, "mavi"),          // omuz
            new Part(-0.032f, 0.36f, -0.032f, 0.032f, 0.41f, 0.032f, "koyu_mavi"), // kapak
        };

        /// <summary>Turuncu düdük: gövde, ağızlık, metal halka</summary>
        private static List<Part> Whistle() => new List<Part>
        {
            new Part(-0.08f, 0f, -0.035f, 0.06f, 0.08f, 0.035f, "turuncu"),
            new Part(0.06f, 0.02f, -0.025f, 0.12f, 0.06f, 0.025f, "turuncu"),      // ağızlık
            new Part(-0.04f, 0.08f, -0.02f, 0.02f, 0.1f, 0.02f, "turuncu"),        // üst delik çıkıntısı
            new Part(-0.12f, 0.02f, -0.006f, -0.08f, 0.065f, 0.006f, "metal"),     // halka
        };

        /// <summary>
        /// Deprem çantası (Play testi 11: "hiç çanta gibi değil"): turuncu acil durum sırt çantası; ön cep ve kapağı,
        /// fermuar, reflektör şerit, yan askılar, üst tutamak, cepte ilk yardım işareti
        /// </summary>
        private static List<Part> EarthquakeBag() => new List<Part>
        {
            new Part(-0.23f, 0f, -0.13f, 0.23f, 0.04f, 0.15f, "siyah"),            // taban
            new Part(-0.22f, 0.04f, -0.12f, 0.22f, 0.52f, 0.14f, "turuncu"),       // gövde
            new Part(-0.18f, 0.52f, -0.1f, 0.18f, 0.58f, 0.12f, "turuncu"),        // yuvarlak üst
            new Part(-0.2f, 0.47f, -0.125f, 0.2f, 0.48f, -0.12f, "koyu_turuncu"),  // fermuar
            new Part(-0.225f, 0.36f, -0.125f, 0.225f, 0.39f, 0.145f, "reflektor"), // reflektör şerit
            new Part(-0.16f, 0.08f, -0.17f, 0.16f, 0.3f, -0.12f, "turuncu"),       // ön cep
            new Part(-0.17f, 0.27f, -0.18f, 0.17f, 0.32f, -0.12f, "koyu_turuncu"), // cep kapağı
            new Part(-0.05f, 0.13f, -0.18f, 0.05f, 0.23f, -0.17f, "beyaz"),        // ilk yardım işareti
            new Part(-0.012f, 0.145f, -0.185f, 0.012f, 0.215f, -0.18f, "kirmizi"),
            new Part(-0.035f, 0.168f, -0.185f, 0.035f, 0.192f, -0.18f, "kirmizi"),
            new Part(-0.235f, 0.05f, -0.02f, -0.22f, 0.5f, 0.02f, "siyah"),        // yan askılar
            new Part(0.22f, 0.05f, -0.02f, 0.235f, 0.5f, 0.02f, "siyah"),
            new Part(-0.06f, 0.58f, -0.012f, -0.04f, 0.64f, 0.012f, "siyah"),      // tutamak
            new Part(0.04f, 0.58f, -0.012f, 0.06f, 0.64f, 0.012f, "siyah"),
            new Part(-0.06f, 0.63f, -0.015f, 0.06f, 0.66f, 0.015f, "siyah"),
        };

        // ------------------------------------------------------------------ Kurulum

        private static void Build(string name, List<Part> parts, string prefabPath)
        {
            var colors = parts.Select(p => p.Color).Distinct().ToList();
            var boxes = parts.Select(p => (p.Min, p.Max)).ToList();
            var bounds = new Bounds(boxes[0].Min, Vector3.zero);
            foreach (var (min, max) in boxes)
            {
                bounds.Encapsulate(min);
                bounds.Encapsulate(max);
            }
            var mesh = BoxMeshBuilder.BuildParts(name, Vector3.zero, boxes,
                BoxMeshBuilder.FitUv(bounds.min, bounds.max), (box, face) => colors.IndexOf(parts[box].Color), colors.Count);
            mesh = SaveMesh(mesh, $"{MaterialFolder}/{name}_mesh.asset");

            var root = new GameObject(name);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var modelRenderer = model.AddComponent<MeshRenderer>();
            modelRenderer.sharedMaterials = colors.Select(ColorMaterial).ToArray();

            var outline = new GameObject("Outline");
            outline.transform.SetParent(root.transform, false);
            outline.AddComponent<MeshFilter>().sharedMesh = mesh;
            var outlineRenderer = outline.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterials = Enumerable.Repeat(OutlineMaterial(), colors.Count).ToArray();
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.enabled = false;

            root.AddComponent<Deprem.Visual.PickupHighlight>().Configure(new Renderer[] { modelRenderer }, new Renderer[] { outlineRenderer });
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, mesh.uv);
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i);
            existing.RecalculateTangents();
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Material ColorMaterial(string key)
        {
            string path = $"{MaterialFolder}/{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Palette[key]);
            mat.SetFloat("_Smoothness", key == "lens" || key == "metal" ? 0.5f : 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Sarı nabızlı kontur; küçük eşyalar için ince</summary>
        private static Material OutlineMaterial()
        {
            string path = $"{MaterialFolder}/kontur_sari.mat";
            var shader = Shader.Find("Deprem/Outline");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_OutlineColor", new Color(1f, 0.85f, 0.3f));
            mat.SetFloat("_OutlineWidth", 0.022f);
            mat.SetFloat("_PulseAmount", 0.45f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
