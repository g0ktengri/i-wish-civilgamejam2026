using System.Collections;
using System.Collections.Generic;
using Deprem.Audio;
using Deprem.Core;
using Deprem.Player;
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
    ///       3. bedenden ruhu (ruh fazının bekleme kareleri) renkli olarak süzülüp yükselir ve kaybolur;
    ///       4. ekran kararır; karanlık keşke ekranı gelene kadar kalır, döngü yeniden başlayınca kalkar.
    ///     Zaman ölçeğine dokunmaz.
    /// QuakeDirector'ın yanında durur (QuakeSetup kurar, ruh karelerini bağlar).
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

        [Header("Ruh (ölümde bedenden yükselir)")]
        [Tooltip("Ruh fazının bekleme kareleri (ruh_bekleme_1..8); QuakeSetup bağlar. Boşsa ruh çıkmaz")]
        [SerializeField] private Texture2D[] ghostFrames;
        [SerializeField] private float ghostFps = 8f;
        [SerializeField] private float ghostDelay = 0.3f;
        [SerializeField] private float ghostTime = 1.8f;
        [Tooltip("Ruhun yükseldiği yükseklik (m)")]
        [SerializeField] private float ghostRise = 1.3f;
        [SerializeField] private Color ghostColor = new Color(0.8f, 0.92f, 1f, 0.9f);

        [Header("Kararma")]
        [SerializeField] private float blackoutStart = 1.7f;
        [SerializeField] private float blackoutTime = 0.6f;
        [Tooltip("Keşke ekranı gelmezse karanlık en fazla bu kadar kalır (sn)")]
        [SerializeField] private float maxHold = 6f;

        private Canvas canvas;
        private Image flash;
        private Image blackout;
        private Image ghost;
        private Volume deathLook;
        private Sprite[] ghostSprites;
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
            // Ruh bedenin ortasından çıkar (yere yığılmış beden: kökün biraz üstü)
            var origin = player != null ? player.transform.position + Vector3.up * 0.35f : Vector3.zero;
            float end = blackoutStart + blackoutTime;
            for (float t = 0f; t < end + maxHold; t += Time.unscaledDeltaTime)
            {
                float f = t < deathFlashHold ? 1f : 1f - Mathf.Clamp01((t - deathFlashHold) / deathFlashFade);
                flash.color = new Color(deathFlash.r, deathFlash.g, deathFlash.b, deathFlash.a * f);
                if (t < deathShakeTime)
                    QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, deathShake * (1f - t / deathShakeTime));
                // Hızlı başlar, yavaş oturur
                deathLook.weight = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / drainTime), 3f);
                UpdateGhost(t - ghostDelay, origin, canvasRect);
                blackout.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - blackoutStart) / blackoutTime)));
                yield return null;
            }
            Clear();
        }

        private void UpdateGhost(float t, Vector3 origin, RectTransform canvasRect)
        {
            if (ghostSprites == null || ghostSprites.Length == 0 || t < 0f || t > ghostTime)
            {
                if (ghost != null) ghost.enabled = false;
                return;
            }
            var cam = Camera.main;
            if (cam == null) return;
            float k = t / ghostTime;
            // Yavaşlayarak yükselir, hafifçe salınır; belirir, sonra söner
            float rise = ghostRise * (1f - Mathf.Pow(1f - k, 2f));
            var world = origin + new Vector3(Mathf.Sin(t * 2.6f) * 0.06f, rise, -0.2f);
            float alpha = Mathf.Clamp01(k / 0.2f) * Mathf.Clamp01((1f - k) / 0.45f);
            // Ruh karesi oyuncu karesi gibi 2 m: dünyadaki 2 m'nin ekrandaki boyu
            var a = cam.WorldToScreenPoint(world);
            var b = cam.WorldToScreenPoint(world + Vector3.up * 2f);
            if (a.z <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, a, null, out var localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, b, null, out var localB);
            float size = Mathf.Abs(localB.y - localA.y);
            ghost.enabled = true;
            ghost.sprite = ghostSprites[Mathf.FloorToInt(t * ghostFps) % ghostSprites.Length];
            ghost.rectTransform.anchoredPosition = localA;
            ghost.rectTransform.sizeDelta = new Vector2(size, size);
            ghost.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, ghostColor.a * alpha);
        }

        private void Clear()
        {
            if (flash != null) flash.color = Color.clear;
            if (blackout != null) blackout.color = Color.clear;
            if (ghost != null) ghost.enabled = false;
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

            // Ruh: alt orta pivot (ayakları bedende), karenin içindeki konumu oyuncu karesiyle aynı
            ghost = UiBuilder.CreateImage(canvas.transform, "Ghost", Color.clear);
            ghost.raycastTarget = false;
            ghost.preserveAspect = true;
            var rect = ghost.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            ghost.enabled = false;

            blackout = UiBuilder.CreateImage(canvas.transform, "Blackout", Color.clear);
            blackout.raycastTarget = false;
            UiBuilder.Stretch(blackout.rectTransform, Vector2.zero, Vector2.one);

            flash = UiBuilder.CreateImage(canvas.transform, "Flash", Color.clear);
            flash.raycastTarget = false;
            UiBuilder.Stretch(flash.rectTransform, Vector2.zero, Vector2.one);

            var sprites = new List<Sprite>();
            if (ghostFrames != null)
                foreach (var frame in ghostFrames)
                    if (frame != null)
                        sprites.Add(Sprite.Create(frame, new Rect(0, 0, frame.width, frame.height), new Vector2(0.5f, 0f), 64f));
            ghostSprites = sprites.ToArray();

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
