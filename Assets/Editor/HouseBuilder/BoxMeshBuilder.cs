using System.Collections.Generic;
using UnityEngine;

namespace Deprem.HouseBuilder
{
    /// <summary>Kutu yüzleri: normal yönüne göre</summary>
    internal enum BoxFace
    {
        PosX,
        NegX,
        PosY,
        NegY,
        PosZ,
        NegZ,
    }

    /// <summary>
    /// Dünya koordinatında min-max verilen kutudan mesh üretir. Her yüzün UV'si ve submesh'i
    /// çağıran tarafından belirlenir; böylece oda boyutunda çizilen dokular duvara birebir oturur.
    /// </summary>
    internal static class BoxMeshBuilder
    {
        public delegate Vector2 UvFunc(Vector3 worldPos, BoxFace face);
        public delegate int SubmeshFunc(BoxFace face);

        private static readonly Vector3[] Normals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        public static Mesh Build(string name, Vector3 min, Vector3 max, UvFunc uv, SubmeshFunc submesh, int submeshCount)
        {
            return BuildParts(name, (min + max) * 0.5f, new[] { (min, max) }, uv, submesh, submeshCount);
        }

        /// <summary>Birden çok kutudan tek mesh (parçalı eşya şekilleri). Köşeler origin'e göre yazılır.</summary>
        public static Mesh BuildParts(string name, Vector3 origin, IReadOnlyList<(Vector3 min, Vector3 max)> boxes,
            UvFunc uv, SubmeshFunc submesh, int submeshCount)
        {
            var vertices = new List<Vector3>(24 * boxes.Count);
            var normals = new List<Vector3>(24 * boxes.Count);
            var uvs = new List<Vector2>(24 * boxes.Count);
            var triangles = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) triangles[i] = new List<int>();

            foreach (var (min, max) in boxes)
            for (int f = 0; f < 6; f++)
            {
                var face = (BoxFace)f;
                var normal = Normals[f];
                int axis = f / 2;
                int a = (axis + 1) % 3;
                int b = (axis + 2) % 3;
                float fixedValue = normal[axis] > 0 ? max[axis] : min[axis];

                var corners = new Vector3[4];
                for (int c = 0; c < 4; c++)
                {
                    var p = Vector3.zero;
                    p[axis] = fixedValue;
                    p[a] = (c == 1 || c == 2) ? max[a] : min[a];
                    p[b] = (c >= 2) ? max[b] : min[b];
                    corners[c] = p;
                }

                int start = vertices.Count;
                foreach (var corner in corners)
                {
                    vertices.Add(corner - origin);
                    normals.Add(normal);
                    uvs.Add(uv(corner, face));
                }

                // Unity'de ön yüz: (v1-v0) x (v2-v0) normal yönünü gösterir
                var cross = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]);
                var list = triangles[submesh(face)];
                if (Vector3.Dot(cross, normal) >= 0f)
                    list.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else
                    list.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshCount;
            for (int i = 0; i < submeshCount; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Döşenebilir doku: her yüze 1 m = 1 tekrar olacak şekilde dünya koordinatından UV</summary>
        public static Vector2 TileUv(Vector3 p, BoxFace face)
        {
            switch (face)
            {
                case BoxFace.PosX:
                case BoxFace.NegX:
                    return new Vector2(p.z, p.y);
                case BoxFace.PosY:
                case BoxFace.NegY:
                    return new Vector2(p.x, p.z);
                default:
                    return new Vector2(p.x, p.y);
            }
        }

        /// <summary>Her yüze dokunun tamamını (0-1) yayan UV: eşyalar ve kabin için</summary>
        public static UvFunc FitUv(Vector3 min, Vector3 max)
        {
            var size = max - min;
            return (p, face) =>
            {
                var t = new Vector3(
                    size.x > 0f ? (p.x - min.x) / size.x : 0f,
                    size.y > 0f ? (p.y - min.y) / size.y : 0f,
                    size.z > 0f ? (p.z - min.z) / size.z : 0f);
                switch (face)
                {
                    case BoxFace.PosX:
                    case BoxFace.NegX:
                        return new Vector2(t.z, t.y);
                    case BoxFace.PosY:
                    case BoxFace.NegY:
                        return new Vector2(t.x, t.z);
                    default:
                        return new Vector2(t.x, t.y);
                }
            };
        }
    }
}
