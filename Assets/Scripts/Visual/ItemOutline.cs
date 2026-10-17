using Deprem.Core;
using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// Sabitlenebilir eşyanın konturu (Deprem/Outline shader'ı). Kendi kendine çalışır: ruh fazında sabitlenmemiş
    /// eşyada yanar, eşya sabitlenince ya da faz bitince söner. Ruh yakınındaki eşyayı öne çıkarmak için
    /// SetEmphasis(true) (Berke'nin ruh sistemi isterse çağırır). Kontur objelerini HouseBuilder kurar.
    /// </summary>
    [RequireComponent(typeof(SecurableItem))]
    public sealed class ItemOutline : MonoBehaviour
    {
        [SerializeField] private Renderer[] outlineRenderers;
        [SerializeField] private Color normalColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color emphasisColor = new Color(1f, 0.92f, 0.55f);
        [Tooltip("M62 (Berke): ailenin ya da oyuncunun üstüne düşecek eşya: kırmızımsı kontur")]
        [SerializeField] private Color importantColor = new Color(1f, 0.42f, 0.32f);
        [Tooltip("M62 (Berke): isteğe bağlı eşya (ayna, çerçeve): soluk kontur")]
        [SerializeField] private Color optionalColor = new Color(0.55f, 0.75f, 0.95f, 0.45f);
        private Color baseColor;
        private bool hasPriority;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");

        private SecurableItem item;
        private MaterialPropertyBlock block;

        public bool IsVisible { get; private set; }

        public void Configure(Renderer[] renderers)
        {
            outlineRenderers = renderers;
        }

        private void Awake()
        {
            item = GetComponent<SecurableItem>();
            SetVisible(false);
            SetEmphasis(false);
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.ItemSecured += OnItemSecured;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.ItemSecured -= OnItemSecured;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            SetVisible(phase == GamePhase.Spirit && !item.IsSecured);
            SetEmphasis(false);
        }

        private void OnItemSecured(ItemId id)
        {
            if (id == item.Id) SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (outlineRenderers == null) return;
            foreach (var r in outlineRenderers)
                if (r != null) r.enabled = visible;
        }

        /// <summary>M62 (Berke): ruh fazında önemli eşya kırmızımsı, isteğe bağlı olan soluk konturlu</summary>
        public void SetPriority(bool important)
        {
            hasPriority = true;
            baseColor = important ? importantColor : optionalColor;
            SetEmphasis(false);
        }

        public void SetEmphasis(bool emphasized)
        {
            if (outlineRenderers == null) return;
            block ??= new MaterialPropertyBlock();
            block.SetColor(ColorId, emphasized ? emphasisColor : hasPriority ? baseColor : normalColor);

            foreach (var r in outlineRenderers)
                if (r != null) r.SetPropertyBlock(block);
        }
    }
}
