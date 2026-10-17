using System.Collections;
using Deprem.Audio;
using Deprem.Core;
using Deprem.Player;
using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Darbe geri bildirimi. Play(güç) ile tetiklenir (0,45 yaralanma, 1 ölüm).
    ///   - Yaralanma: anlık kırmızı flaş (hemen söner), kısa sarsıntı, darbe sesi.
    ///   - Ölüm (Play testi 24: ağır çekim kasma gibi görünüyordu, kırmızı fade in/out garipti): tek karelik sert beyaz
    ///     darbe, güçlü ve kısa sarsıntı, ağır vuruş + kulak çınlaması (player.death), sonra karanlık oyuncunun
    ///     etrafında daire şeklinde kapanır (iris). Karanlık keşke ekranı gelene kadar kalır; döngü yeniden
    ///     başlayınca kalkar. Zaman ölçeğine dokunmaz.
    /// QuakeDirector'ın yanında durur.
    /// </summary>
    public sealed class HitFeedback : MonoBehaviour
    {
        [Header("Yaralanma")]
        [SerializeField] private Color injuryFlash = new Color(0.8f, 0.06f, 0.04f, 0.45f);
        [SerializeField] private float injuryFlashTime = 0.35f;
        [SerializeField] private float injuryShake = 0.8f;
        [SerializeField] private float injuryShakeTime = 0.35f;

        [Header("Ölüm")]
        [SerializeField] private Color deathFlash = new Color(1f, 0.96f, 0.9f, 0.9f);
        [Tooltip("Beyaz darbe tam parlaklıkta kalır (sn), sonra hızla söner")]
        [SerializeField] private float deathFlashHold = 0.06f;
        [SerializeField] private float deathFlashFade = 0.2f;
        [SerializeField] private float deathShake = 1.6f;
        [SerializeField] private float deathShakeTime = 0.5f;
        [Tooltip("Karanlığın kapanmaya başladığı an ve süresi (sn)")]
        [SerializeField] private float irisDelay = 0.12f;
        [SerializeField] private float irisCloseTime = 1.3f;
        [Tooltip("Kapanınca oyuncunun çevresinde kalan aydınlık dairenin çapı (referans piksel)")]
        [SerializeField] private float irisEndSize = 230f;
        [Tooltip("Keşke ekranı gelmezse karanlık en fazla bu kadar kalır (sn)")]
        [SerializeField] private float irisMaxHold = 6f;

        private Canvas canvas;
        private Image flash;
        private RectTransform iris;
        private Coroutine running;
        private PlayerController player;

        public void Play(float strength)
        {
            EnsureUi();
            bool death = strength >= 0.8f;
            Sfx.Play("player.hit", Mathf.Lerp(0.7f, 1f, strength));
            if (death)
            {
                Sfx.Play("item.crash", 0.9f);
                Sfx.Play("player.death", 1f);
            }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(death ? Death() : Injury(strength));
        }

        private IEnumerator Injury(float strength)
        {
            for (float t = 0f; t < Mathf.Max(injuryFlashTime, injuryShakeTime); t += Time.unscaledDeltaTime)
            {
                float f = 1f - Mathf.Clamp01(t / injuryFlashTime);
                flash.color = new Color(injuryFlash.r, injuryFlash.g, injuryFlash.b, injuryFlash.a * strength * f * f);
                if (t < injuryShakeTime)
                    QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, injuryShake * (1f - t / injuryShakeTime));
                yield return null;
            }
            flash.color = Color.clear;
            running = null;
        }

        private IEnumerator Death()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            var canvasRect = (RectTransform)canvas.transform;
            float open = OpenSize(canvasRect);
            iris.gameObject.SetActive(true);
            iris.sizeDelta = Vector2.one * open;
            float closeEnd = irisDelay + irisCloseTime;
            for (float t = 0f; t < closeEnd + irisMaxHold; t += Time.unscaledDeltaTime)
            {
                // Sert beyaz darbe: yumuşak giriş yok
                float f = t < deathFlashHold ? 1f : 1f - Mathf.Clamp01((t - deathFlashHold) / deathFlashFade);
                flash.color = new Color(deathFlash.r, deathFlash.g, deathFlash.b, deathFlash.a * f);
                if (t < deathShakeTime)
                    QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, deathShake * (1f - t / deathShakeTime));

                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - irisDelay) / irisCloseTime));
                iris.sizeDelta = Vector2.one * Mathf.Lerp(open, irisEndSize, k);
                iris.anchoredPosition = PlayerOnCanvas(canvasRect);
                yield return null;
            }
            Clear();
        }

        /// <summary>Daire ekranın en uzak köşesinin de dışında: karanlık henüz görünmez</summary>
        private static float OpenSize(RectTransform canvasRect)
        {
            var size = canvasRect.rect.size;
            return size.magnitude * 2.6f;
        }

        private Vector2 PlayerOnCanvas(RectTransform canvasRect)
        {
            var cam = Camera.main;
            if (player == null || cam == null) return Vector2.zero;
            // Gövdenin ortası (ayak kökünden ~0,8 m yukarı)
            var screen = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 0.8f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            return local;
        }

        private void Clear()
        {
            if (flash != null) flash.color = Color.clear;
            if (iris != null) iris.gameObject.SetActive(false);
            running = null;
        }

        private void OnEnable() => GameEvents.LoopStarted += OnLoopStarted;

        private void OnDisable()
        {
            GameEvents.LoopStarted -= OnLoopStarted;
            if (running != null) StopCoroutine(running);
            Clear();
        }

        // Döngü yeniden başladı (keşke ekranından sonra): karanlık kalkar
        private void OnLoopStarted(int loop)
        {
            if (running != null) StopCoroutine(running);
            Clear();
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "HitFeedback", 45);

            // İris: ortası delik daire; dört yanındaki siyah bantlar ekranın geri kalanını örter
            var hole = UiBuilder.CreateImage(canvas.transform, "Iris", Color.black);
            hole.sprite = HoleSprite();
            hole.raycastTarget = false;
            iris = hole.rectTransform;
            iris.anchorMin = iris.anchorMax = iris.pivot = new Vector2(0.5f, 0.5f);
            const float far = 20000f;
            Band("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(far, far * 2f));
            Band("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(far, far * 2f));
            Band("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, far));
            Band("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, far));
            iris.gameObject.SetActive(false);

            flash = UiBuilder.CreateImage(canvas.transform, "Flash", Color.clear);
            flash.raycastTarget = false;
            UiBuilder.Stretch(flash.rectTransform, Vector2.zero, Vector2.one);
        }

        private void Band(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            var band = UiBuilder.CreateImage(iris, name, Color.black);
            band.raycastTarget = false;
            var rect = band.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        // Ortası saydam yumuşak kenarlı daire, dışı siyah (köşeler dahil)
        private static Sprite HoleSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.98f, d))));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
