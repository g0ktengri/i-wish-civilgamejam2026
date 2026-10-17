using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Cam kırılması geçişinin bir parçası: ekran görüntüsünün bir çokgenini çizer (WishScreen kurar).
    // Köşeler parçanın kendi merkezine göre (referans pikseli); UV'ler ekran görüntüsündeki yeri.
    public class ShardGraphic : MaskableGraphic
    {
        Texture texture;
        Vector2[] points;
        Vector2[] uvs;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public void Set(Texture shot, Vector2[] localPoints, Vector2[] pointUvs)
        {
            texture = shot;
            points = localPoints;
            uvs = pointUvs;
            raycastTarget = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points == null || points.Length < 3) return;
            for (int i = 0; i < points.Length; i++)
                vh.AddVert(points[i], color, uvs[i]);
            for (int i = 1; i < points.Length - 1; i++)
                vh.AddTriangle(0, i, i + 1);
        }
    }
}
