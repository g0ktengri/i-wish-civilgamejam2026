using Deprem.Core;
using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// Çanta malzemesi modeli (ilk yardım, el feneri, su, düdük) ve sarı nabızlı konturu. Model prefab'ları
    /// Assets/Prefabs/Mehmet/BagItems/BagItem_{BagItemId}.prefab (BagItemModels üretir; pivot: taban ortası).
    /// Kontur ruh fazında yanar (malzemeler o fazda alınır); taşınırken ya da çantadayken SetShown(false) ile
    /// model ve kontur birlikte gizlenir. Berke'nin BagSupply'ı kullanır (ENTEGRASYON, Play testi 10).
    /// </summary>
    public sealed class PickupHighlight : MonoBehaviour
    {
        [SerializeField] private Renderer[] modelRenderers;
        [SerializeField] private Renderer[] outlineRenderers;
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.3f);
        [Tooltip("Kapalıysa kontur her fazda yanar")]
        [SerializeField] private bool onlyInSpirit = true;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private MaterialPropertyBlock block;
        private bool shown = true;
        private bool highlight = true;

        public void Configure(Renderer[] model, Renderer[] outline)
        {
            modelRenderers = model;
            outlineRenderers = outline;
        }

        /// <summary>Modeli ve konturu gösterir / gizler (taşınırken, çantaya konunca)</summary>
        public void SetShown(bool value)
        {
            shown = value;
            if (modelRenderers != null)
                foreach (var r in modelRenderers)
                    if (r != null) r.enabled = value;
            Apply();
        }

        /// <summary>Kontur açık mı (varsayılan açık; örneğin malzeme alındıktan sonra kapatılabilir)</summary>
        public void SetHighlight(bool value)
        {
            highlight = value;
            Apply();
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            if (outlineRenderers != null)
                foreach (var r in outlineRenderers)
                    if (r != null) r.SetPropertyBlock(block);
            Apply();
        }

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;
        private void OnPhaseChanged(GamePhase phase) => Apply();

        private void Apply()
        {
            bool on = shown && highlight && (!onlyInSpirit || GameState.Phase == GamePhase.Spirit);
            if (outlineRenderers == null) return;
            foreach (var r in outlineRenderers)
                if (r != null) r.enabled = on;
        }
    }
}
