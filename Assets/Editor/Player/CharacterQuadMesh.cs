using UnityEditor;
using UnityEngine;

namespace Deprem.Player
{
    /// <summary>
    /// Karakter görselleri (oyuncu ve aile) için 1 x 1 quad. Normal kameraya değil yukarı-öne eğik:
    /// tavan lambaları karakter düzleminin arkasında (odanın ortasında) olduğu için düz quad'a ışık vurmuyordu.
    /// Böylece karakter odanın ışığını alır, elektrik gidince karanlığa uyar (Unlit gibi parlamaz).
    /// Unity'nin Quad'ı gibi -Z'ye (kameraya) bakar; ölçek ve konum aynı kalır.
    /// </summary>
    internal static class CharacterQuadMesh
    {
        private const string Path = "Assets/Generated/Player/karakter_quad.asset";
        // Yukarı ve kameraya doğru (yaklaşık 60° yukarı)
        private static readonly Vector3 Normal = new Vector3(0f, 0.86f, -0.5f).normalized;

        public static Mesh Get()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Path);
            if (mesh == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Generated/Player")) AssetDatabase.CreateFolder("Assets/Generated", "Player");
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, Path);
            }
            mesh.Clear();
            mesh.name = "karakter_quad";
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.normals = new[] { Normal, Normal, Normal, Normal };
            mesh.triangles = new[] { 0, 3, 1, 3, 0, 2 }; // -Z'den bakınca saat yönünde (Unity Quad ile aynı)
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
