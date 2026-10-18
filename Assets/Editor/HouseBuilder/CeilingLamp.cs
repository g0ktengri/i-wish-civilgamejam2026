using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Tavandan sarkan avize (Mehmet'in isteği: tavandaki düz kutu lamba yerine güzel bir model, ışık da gelişsin).
    /// Pirinç tavan göbeği ve çubuk, içten aydınlanan pileli kumaş abajur (Deprem/LampShade), ampul, ampulün
    /// çevresinde halkalı parıltı ve abajurun altından inen yumuşak ışık huzmesi (Deprem/LampGlow). Işık ampulün
    /// içinde: odayı aydınlatan nokta ışığı (gölgeli) ve yere ışık havuzu düşüren aşağı bakan spot.
    /// Pivot tavanda: HouseLights sarsıntıda avizeyi buradan sallar, elektrik ve titremeyle _Glow'u ayarlar.
    /// Modeller dönen profilden (torna) kodla üretilir, Assets/Generated/House/Meshes'e kaydedilir.
    /// </summary>
    internal static class CeilingLamp
    {
        private const string MeshFolder = "Assets/Generated/House/Meshes";
        private const string MaterialFolder = "Assets/Generated/House/Materials";
        private const int Segments = 14;

        // Profil noktaları: (yarıçap, y); y tavandan aşağı (eksi). Üstten alta sırayla.
        private static readonly Vector2[] CanopyProfile = { new(0.10f, -0.005f), new(0.10f, -0.03f), new(0.045f, -0.055f), new(0f, -0.055f) };
        private static readonly Vector2[] RodProfile = { new(0.011f, -0.05f), new(0.011f, -0.37f) };
        private static readonly Vector2[] CapProfile = { new(0.02f, -0.36f), new(0.04f, -0.375f), new(0.04f, -0.42f), new(0f, -0.42f) };
        private const float ShadeTop = -0.38f, ShadeBottom = -0.67f;
        private static readonly Vector2[] ShadeProfile = { new(0.1f, ShadeTop), new(0.185f, -0.45f), new(0.275f, -0.56f), new(0.34f, ShadeBottom) };
        private static readonly Vector2[] ShaftProfile = { new(0.32f, ShadeBottom), new(1.1f, -2.5f) };
        private const float BulbY = -0.5f, BulbRadius = 0.055f;

        internal sealed class Result
        {
            public Transform Pendant;
            public Light Point;
            public Light Spot;
        }

        /// <param name="ceiling">Tavandaki asılma noktası (dünya)</param>
        /// <param name="lightShade">Açık renk (fildişi) abajur; yoksa bordo</param>
        public static Result Build(Transform parent, Vector3 ceiling, Color lightColor, float pointIntensity, float pointRange,
            float spotIntensity, float roomHeight, bool lightShade = false)
        {
            var pendant = new GameObject("Pendant").transform;
            pendant.SetParent(parent, false);
            pendant.position = ceiling;

            Part(pendant, "Canopy", Lathe("avize_gobek", CanopyProfile), Brass());
            Part(pendant, "Rod", Lathe("avize_cubuk", RodProfile), Brass());
            Part(pendant, "Cap", Lathe("avize_duy", CapProfile), Brass());
            Part(pendant, "Shade", Lathe("avize_abajur", ShadeProfile), ShadeMaterial(lightShade));
            Part(pendant, "Bulb", Lathe("avize_ampul", SphereProfile(BulbY, BulbRadius)), GlowMaterial("avize_ampul", 2f,
                new Color(1f, 0.85f, 0.6f, 1f), opaque: true));
            Part(pendant, "Shaft", Lathe("avize_huzme", ShaftProfile), GlowMaterial("avize_huzme", 1f,
                new Color(1f, 0.8f, 0.55f, 0.13f), opaque: false));
            // Hale: kameraya bakan kare, abajurun ağzında ve biraz önünde
            var halo = Part(pendant, "Halo", Quad("avize_hale", 0.9f), GlowMaterial("avize_hale", 0f,
                new Color(1f, 0.76f, 0.45f, 0.2f), opaque: false, size: 0.9f));
            halo.transform.localPosition = new Vector3(0f, ShadeBottom + 0.02f, -0.4f);

            var point = new GameObject("Light").AddComponent<Light>();
            point.transform.SetParent(pendant, false);
            point.transform.localPosition = new Vector3(0f, BulbY, 0f);
            point.type = LightType.Point;
            point.color = lightColor;
            point.intensity = pointIntensity;
            point.range = pointRange;
            point.shadows = LightShadows.Soft;

            // Abajurun ağzından aşağı: yerde sıcak ışık havuzu, karakterlerin başına üstten ışık
            var spot = new GameObject("Spot").AddComponent<Light>();
            spot.transform.SetParent(pendant, false);
            spot.transform.localPosition = new Vector3(0f, BulbY - 0.05f, 0f);
            spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spot.type = LightType.Spot;
            spot.color = lightColor;
            spot.intensity = spotIntensity;
            spot.range = roomHeight + 0.5f;
            spot.spotAngle = 115f;
            spot.innerSpotAngle = 45f;
            spot.shadows = LightShadows.None;

            return new Result { Pendant = pendant, Point = point, Spot = spot };
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // ampulün ışığını kesmesin
            renderer.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------- Modeller

        private static Vector2[] SphereProfile(float centerY, float radius)
        {
            var points = new List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = Mathf.PI * 0.5f - Mathf.PI * i / 6f;
                points.Add(new Vector2(Mathf.Max(0f, radius * Mathf.Cos(a)), centerY + radius * Mathf.Sin(a)));
            }
            return points.ToArray();
        }

        /// <summary>
        /// Profili Y ekseni çevresinde döndürür. Her profil kenarı kendi halkasını alır (köşeler keskin, çevre
        /// boyunca yumuşak). UV: x çevre (0..1), y profil boyunca (0 üst, 1 alt).
        /// </summary>
        private static Mesh Lathe(string name, Vector2[] profile)
        {
            float total = 0f;
            for (int k = 0; k + 1 < profile.Length; k++) total += Vector2.Distance(profile[k], profile[k + 1]);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float walked = 0f;
            for (int k = 0; k + 1 < profile.Length; k++)
            {
                Vector2 a = profile[k], b = profile[k + 1];
                float length = Vector2.Distance(a, b);
                // Kenarın dışa bakan normali (profil yönünü 90° döndürür)
                var edge = (b - a).normalized;
                var n2 = new Vector2(-edge.y, edge.x);
                int start = vertices.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    float angle = t * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var normal = (dir * n2.x + Vector3.up * n2.y).normalized;
                    vertices.Add(dir * a.x + Vector3.up * a.y);
                    vertices.Add(dir * b.x + Vector3.up * b.y);
                    normals.Add(normal);
                    normals.Add(normal);
                    uvs.Add(new Vector2(t, walked / total));
                    uvs.Add(new Vector2(t, (walked + length) / total));
                }
                for (int i = 0; i < Segments; i++)
                {
                    int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    // Ön yüz normal yönüne baksın (Unity: saat yönü)
                    var face = Vector3.Cross(vertices[a1] - vertices[a0], vertices[b1] - vertices[a0]);
                    if (face.sqrMagnitude < 1e-12f) face = Vector3.Cross(vertices[b1] - vertices[a0], vertices[b0] - vertices[a0]);
                    bool flip = Vector3.Dot(face, normals[a0] + normals[a1]) < 0f;
                    if (!flip) triangles.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
                    else triangles.AddRange(new[] { a0, b1, a1, a0, b0, b1 });
                }
                walked += length;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return SaveMesh(mesh, $"{MeshFolder}/{name}.asset");
        }

        /// <summary>Kameraya (-z) bakan kare, ortası pivotta</summary>
        private static Mesh Quad(string name, float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new(-h, -h, 0f), new(-h, h, 0f), new(h, h, 0f), new(h, -h, 0f) });
            mesh.SetNormals(new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetUVs(0, new List<Vector2> { new(0f, 0f), new(0f, 1f), new(1f, 1f), new(1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return SaveMesh(mesh, $"{MeshFolder}/{name}.asset");
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            EnsureFolder(MeshFolder);
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
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Materyaller

        private static Material Brass()
        {
            var mat = LoadOrCreate("avize_pirinc", Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.7f, 0.52f, 0.28f));
            mat.SetFloat("_Metallic", 0.65f);
            mat.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ShadeMaterial(bool light)
        {
            var mat = LoadOrCreate(light ? "avize_abajur_acik" : "avize_abajur", Shader.Find("Deprem/LampShade"));
            // Bordo kumaş (krem duvarda seçilsin, tavan pervazının kırmızısıyla uyumlu), pirinç şerit. Mehmet'in
            // isteği: koridor, sahanlık ve oyuncunun odasında açık renk (sıcak fildişi; beyaz duvardan ayrılsın)
            mat.SetColor("_Color", light ? new Color(0.93f, 0.84f, 0.66f) : new Color(0.62f, 0.24f, 0.18f));
            mat.SetColor("_GlowColor", new Color(1f, 0.78f, 0.5f));
            mat.SetColor("_TrimColor", new Color(0.95f, 0.72f, 0.35f));
            mat.SetFloat("_Ambient", 0.3f);
            mat.SetFloat("_Steps", 14f);
            mat.SetFloat("_Top", ShadeTop);
            mat.SetFloat("_Height", ShadeTop - ShadeBottom);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GlowMaterial(string name, float mode, Color color, bool opaque, float size = 1f)
        {
            var mat = LoadOrCreate(name, Shader.Find("Deprem/LampGlow"));
            mat.SetFloat("_Mode", mode);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Size", size);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)(opaque ? BlendMode.Zero : BlendMode.One));
            mat.SetFloat("_ZWrite", opaque ? 1f : 0f);
            mat.renderQueue = opaque ? (int)RenderQueue.Geometry : (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
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
