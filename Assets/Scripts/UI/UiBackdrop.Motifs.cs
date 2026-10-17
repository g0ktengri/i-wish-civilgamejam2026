using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Menü arka planının ek motifleri (arayüz turu 5, Berke: "bunun gibi birkaç fikir"). Her biri ayrı açılır; ana menü
    // hangilerinin açık olacağını kendi alanlarından seçer:
    // (Saat motifi Berke'nin isteğiyle kaldırıldı.)
    // - Elektrik oynaması: 20-30 sn'de bir lamba ve oda birkaç kez kısa kısa söner, sonra yarım saniye karanlık kalır.
    // - Camda yağmur: ince, soğuk renkli pixel çizgiler çaprazlama yağar (pencere camından bakıyormuş gibi).
    // - Kalp atışı: kenar kararması (vinyet) "tuk-tuk" ritmiyle hafifçe daralıp gevşer.
    public partial class UiBackdrop
    {
        const int RainCount = 46;

        // Elektrik
        Image blackout;
        bool powerFlicker;
        float outageStart = -100f;
        float nextOutage;
        static readonly float[] OutagePattern = { 0.08f, 0.07f, 0.12f, 0.1f, 0.06f, 0.05f, 0.55f }; // söner, yanar, ...

        /// <summary>Elektrik şu an kesik mi (lamba ışığı söner)</summary>
        public bool PowerOut { get; private set; }

        // Yağmur
        readonly List<(RectTransform rect, float speed)> rain = new List<(RectTransform, float)>();

        // Kalp atışı
        bool heartbeat;

        // Motifler arka planın içinde: kararma ve orta bandın üstünde, vinyetin altında
        void PlaceUnderVignette(Transform layer) => layer.SetSiblingIndex(vignette.transform.GetSiblingIndex());

        /// <summary>Ara ara elektrik oynar: lamba ve oda kısa kısa söner</summary>
        public void EnablePowerFlicker()
        {
            if (powerFlicker) return;
            powerFlicker = true;
            blackout = UiBuilder.CreateImage(root, "Blackout", new Color(0f, 0f, 0.02f, 0f));
            UiBuilder.Stretch(blackout.rectTransform, Vector2.zero, Vector2.one);
            PlaceUnderVignette(blackout.transform);
            nextOutage = Time.unscaledTime + Random.Range(10f, 16f);
        }

        /// <summary>Camda yağmur: ince soğuk çizgiler çaprazlama yağar</summary>
        public void EnableRain()
        {
            if (rain.Count > 0) return;
            var random = new System.Random(73);
            for (int i = 0; i < RainCount; i++)
            {
                float alpha = 0.08f + (float)random.NextDouble() * 0.16f;
                var drop = UiBuilder.CreateImage(root, "Rain", new Color(0.72f, 0.82f, 1f, alpha));
                var rect = drop.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2((float)random.NextDouble() * 1.2f, (float)random.NextDouble());
                rect.sizeDelta = new Vector2(2f, random.Next(10, 20));
                rect.localRotation = Quaternion.Euler(0f, 0f, -12f);
                drop.transform.SetSiblingIndex(dim.transform.GetSiblingIndex() + 1);
                rain.Add((rect, 420f + (float)random.NextDouble() * 260f));
            }
        }

        /// <summary>Kenar kararması kalp atışı ritmiyle daralıp gevşer</summary>
        public void EnableHeartbeat() => heartbeat = true;

        void AnimateMotifs(float t, float dt)
        {
            if (powerFlicker) AnimatePower(t);
            if (rain.Count > 0) AnimateRain(dt);
            if (heartbeat && vignette)
            {
                // Dakikada ~55 atış: "tuk" ve 0,25 sn sonra daha hafif "tuk"
                float beat = Mathf.Repeat(t, 1.1f);
                float pulse = Mathf.Exp(-beat * 14f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, beat - 0.25f) * 14f) * (beat > 0.25f ? 1f : 0f);
                vignette.rectTransform.localScale = Vector3.one * (1f - 0.035f * pulse);
            }
        }

        void AnimatePower(float t)
        {
            if (t >= nextOutage)
            {
                outageStart = t;
                float total = 0f;
                foreach (var step in OutagePattern) total += step;
                nextOutage = t + total + Random.Range(20f, 30f);
            }
            // Desende çift adımlar sönük, tekler yanık
            float e = t - outageStart;
            bool off = false;
            for (int i = 0; i < OutagePattern.Length && e >= 0f; i++)
            {
                if (e < OutagePattern[i])
                {
                    off = i % 2 == 0;
                    break;
                }
                e -= OutagePattern[i];
            }
            PowerOut = off;
            blackout.color = new Color(blackout.color.r, blackout.color.g, blackout.color.b, off ? 0.45f : 0f);
        }

        void AnimateRain(float dt)
        {
            float height = canvasRect ? Mathf.Max(1f, canvasRect.rect.height) : 540f;
            float width = canvasRect ? Mathf.Max(1f, canvasRect.rect.width) : 960f;
            foreach (var (rect, speed) in rain)
            {
                var anchor = rect.anchorMin;
                anchor.y -= speed * dt / height;
                anchor.x -= speed * 0.21f * dt / width; // -12°
                if (anchor.y < -0.05f)
                {
                    anchor.y = 1.05f;
                    anchor.x = Random.Range(0f, 1.2f);
                }
                rect.anchorMin = rect.anchorMax = anchor;
            }
        }

    }
}
