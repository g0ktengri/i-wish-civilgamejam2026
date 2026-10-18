using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// Kendi materyaliyle (Deprem/WorldText, derinliğe bakar) çizilen TextMesh'e fontun o anki atlasını bağlar.
    /// Dinamik fontun atlası yeni harf istendikçe büyüyüp değişir; materyalde eski doku kalınca harfler bozuk
    /// görünüyordu. Her çizimden hemen önce (edit mode önizlemesi dahil) güncel doku verilir. HouseBuilder ekler.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public sealed class WorldTextAtlas : MonoBehaviour
    {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private TextMesh text;
        private Renderer textRenderer;
        private MaterialPropertyBlock block;

        private void OnWillRenderObject()
        {
            if (text == null) text = GetComponent<TextMesh>();
            if (textRenderer == null) textRenderer = GetComponent<Renderer>();
            if (text.font == null || text.font.material == null || text.font.material.mainTexture == null) return;
            block ??= new MaterialPropertyBlock();
            textRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTex, text.font.material.mainTexture);
            textRenderer.SetPropertyBlock(block);
        }
    }
}
