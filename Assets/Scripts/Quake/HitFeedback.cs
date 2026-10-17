using System.Collections;
using Deprem.Audio;
using Deprem.Core;
using Deprem.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Darbe geri bildirimi. Play(güç) ile tetiklenir (0,45 yaralanma, 1 ölüm).
    ///   - Yaralanma: anlık kırmızı flaş (hemen söner), kısa sarsıntı, darbe sesi.
    ///   - Ölüm (Play testi 25: kapanan daire zoom gibi duruyordu, güzel değildi):
    ///       1. tek karelik sert beyaz darbe, güçlü kısa sarsıntı, ağır vuruş ve kulak çınlaması (player.death);
    ///       2. dünya bir anda solar: renkler gider, kararır, kenarlar kapanır (öncelikli global Volume);
    ///       3. ekran kararır; karanlık keşke ekranı gelene kadar kalır, döngü yeniden başlayınca kalkar.
    ///     Play testi 26: bedenden yükselen ruh kalktı (ölünce ruh çıkıyormuş gibi duruyordu). Zaman ölçeğine dokunmaz.
    /// QuakeDirector'ın yanında durur (QuakeSetup kurar).
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
        [SerializeField] private float deathFlashHold = 0.05f;
        [SerializeField] private float deathFlashFade = 0.22f;
        [SerializeField] private float deathShake = 1.4f;
        [SerializeField] private float deathShakeTime = 0.45f;
        [Tooltip("Dünyanın solması (renk gider, kararır) ne kadar sürer (sn)")]
        [SerializeField] private float drainTime = 0.5f;

        [Header("Kararma")]
        [SerializeField] private float blackoutStart = 0.8f; // Play testi 27: siyah daha yavaş insin
        [SerializeField] private float blackoutTime = 1.6f;
        [Tooltip("Keşke ekranı gelmezse karanlık en fazla bu kadar kalır (sn)")]
        [SerializeField] private float maxHold = 6f;

        private Canvas canvas;
        private Image flash;
        private Image blackout;
        private Volume deathLook;
        private Coroutine running;

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
            float end = blackoutStart + blackoutTime;
            for (float t = 0f; t < end + maxHold; t += Time.unscaledDeltaTime)
            {
                float f = t < deathFlashHold ? 1f : 1f - Mathf.Clamp01((t - deathFlashHold) / deathFlashFade);
                flash.color = new Color(deathFlash.r, deathFlash.g, deathFlash.b, deathFlash.a * f);
                if (t < deathShakeTime)
                    QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, deathShake * (1f - t / deathShakeTime));
                // Hızlı başlar, yavaş oturur
                deathLook.weight = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / drainTime), 3f);
                blackout.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - blackoutStart) / blackoutTime)));
                yield return null;
            }
            Clear();
        }

        private void Clear()
        {
            if (flash != null) flash.color = Color.clear;
            if (blackout != null) blackout.color = Color.clear;
            if (deathLook != null) deathLook.weight = 0f;
            running = null;
        }

        private void OnEnable() => GameEvents.LoopStarted += OnLoopStarted;

        private void OnDisable()
        {
            GameEvents.LoopStarted -= OnLoopStarted;
            if (running != null) StopCoroutine(running);
            Clear();
        }

        // Döngü yeniden başladı (keşke ekranından sonra): karanlık ve solgunluk kalkar
        private void OnLoopStarted(int loop)
        {
            if (running != null) StopCoroutine(running);
            Clear();
        }

        private void EnsureUi()
        {
            if (canvas != null) return;
            canvas = UiBuilder.CreateCanvas(transform, "HitFeedback", 45);

            blackout = UiBuilder.CreateImage(canvas.transform, "Blackout", Color.clear);
            blackout.raycastTarget = false;
            UiBuilder.Stretch(blackout.rectTransform, Vector2.zero, Vector2.one);

            flash = UiBuilder.CreateImage(canvas.transform, "Flash", Color.clear);
            flash.raycastTarget = false;
            UiBuilder.Stretch(flash.rectTransform, Vector2.zero, Vector2.one);

            // Ölümde dünyanın solması: faz Volume'larının (öncelik 0–2) üstünde
            var lookGo = new GameObject("DeathLook");
            lookGo.transform.SetParent(transform, false);
            deathLook = lookGo.AddComponent<Volume>();
            deathLook.isGlobal = true;
            deathLook.priority = 50f;
            deathLook.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-100f);
            color.contrast.Override(25f);
            color.postExposure.Override(-0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(0.5f);
            vignette.smoothness.Override(0.55f);
            deathLook.sharedProfile = profile;
        }
    }
}
