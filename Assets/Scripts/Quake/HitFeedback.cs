using System.Collections;
using Deprem.Audio;
using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Play testi 23: oyuncu hit yiyip ölünce geri bildirim çok zayıftı. Darbe anında: ağır darbe sesi (ölümde ayrıca
    /// alçak bir vurgu), sert kamera sarsıntısı, kırmızı flaş ve kararan ekran kenarları, kısa ağır çekim.
    /// QuakeDirector'ın yanında durur; Play(güç) ile tetiklenir (0,4 yaralanma, 1 ölüm).
    /// </summary>
    public sealed class HitFeedback : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new Color(0.75f, 0.05f, 0.03f, 0.55f);
        [SerializeField] private float flashTime = 0.7f;
        [Tooltip("Ölümde kenar kararması ne kadar kalır (sn)")]
        [SerializeField] private float vignetteTime = 1.6f;
        [SerializeField] private float shakeTime = 0.6f;
        [Tooltip("Ağır çekim (ölümde): zaman ölçeği ve gerçek süresi")]
        [SerializeField] private float slowScale = 0.35f;
        [SerializeField] private float slowTime = 0.45f;

        private Image flash;
        private Image vignette;
        private Coroutine running;

        public void Play(float strength)
        {
            EnsureUi();
            Sfx.Play("player.hit", Mathf.Lerp(0.7f, 1f, strength));
            if (strength >= 0.8f)
            {
                Sfx.Play("item.crash", 0.9f);
                Sfx.Play("player.death", 1f);
            }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Run(strength));
        }

        private IEnumerator Run(float strength)
        {
            bool death = strength >= 0.8f;
            if (death && Time.timeScale > 0f) Time.timeScale = slowScale;
            float t = 0f;
            float total = Mathf.Max(flashTime, death ? vignetteTime : flashTime * 0.8f, shakeTime);
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(1f - t / flashTime);
                flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * strength * f * f);
                float v = death ? Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((total - t) / 0.5f) : Mathf.Clamp01(1f - t / (flashTime * 0.8f)) * 0.6f;
                vignette.color = new Color(0.04f, 0f, 0f, 0.85f * v);
                if (t < shakeTime)
                    QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, (death ? 1.4f : 0.8f) * (1f - t / shakeTime));
                if (death && t >= slowTime && Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
                yield return null;
            }
            flash.color = Color.clear;
            vignette.color = Color.clear;
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
            running = null;
        }

        private void OnDisable()
        {
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
        }

        private void EnsureUi()
        {
            if (flash != null) return;
            var canvas = UiBuilder.CreateCanvas(transform, "HitFeedback", 45);
            vignette = UiBuilder.CreateImage(canvas.transform, "Vignette", Color.clear);
            vignette.sprite = VignetteSprite();
            UiBuilder.Stretch(vignette.rectTransform, Vector2.zero, Vector2.one);
            flash = UiBuilder.CreateImage(canvas.transform, "Flash", Color.clear);
            UiBuilder.Stretch(flash.rectTransform, Vector2.zero, Vector2.one);
        }

        // Ortası saydam, kenarlara doğru koyulaşan yumuşak halka
        private static Sprite VignetteSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
