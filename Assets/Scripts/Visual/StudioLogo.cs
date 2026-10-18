using System;
using System.Collections;
using Deprem.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deprem.Visual
{
    /// <summary>
    /// "Alter Ego Interactive" stüdyo logosu (Mehmet'in isteği): oyunun en sonunda, kapanış ekranından sonra.
    /// Ekran kararır, siyahta sadece ekibin logosu (Resources/StudioLogo/alter_ego_logo.png: ekibin orijinal yüksek
    /// çözünürlüklü logosu, siyahta görünsün diye menüdeki gibi krem; şekli birebir) sade bir şekilde belirir, bir süre
    /// durur, söner; ekran siyahken onDone (ana menü yüklenir).
    /// Gerçek zamanla akar (timeScale'den bağımsız); belirdikten sonra herhangi bir tuş ya da tık sönmeye atlar.
    /// Görünüm zamanın fonksiyonu (Evaluate); editör önizlemesi de aynı kareleri çizer.
    /// </summary>
    public sealed class StudioLogo : MonoBehaviour
    {
        const string LogoResource = "StudioLogo/alter_ego_logo";

        [Header("Zamanlama (sn)")]
        [Tooltip("Kapanış ekranının üstüne siyah iner")]
        [SerializeField] float blackFade = 0.8f;
        [SerializeField] float fadeIn = 1.2f;
        [SerializeField] float hold = 2.4f;
        [SerializeField] float fadeOut = 1.2f;
        [Tooltip("Söndükten sonra siyah ekranda bekleme")]
        [SerializeField] float afterBlack = 0.4f;

        [Header("Görünüm (referans 960x540)")]
        [Tooltip("Logonun ekrandaki yüksekliği (oranı korunur)")]
        [SerializeField] float logoHeight = 200f;

        float FadeOutStart => blackFade + fadeIn + hold;
        public float Length => FadeOutStart + fadeOut + afterBlack;
        public static bool Playing => instance != null;
        static StudioLogo instance;

        Image background;
        CanvasGroup content;

        /// <summary>Logoyu oynatır; bitince (ekran siyahken) onDone. onDone yoksa ekran açılıp logo kaldırılır.</summary>
        public static void Play(Action onDone)
        {
            if (instance) return;
            instance = new GameObject("StudioLogo").AddComponent<StudioLogo>();
            instance.Build();
            instance.StartCoroutine(instance.Run(onDone));
        }

        IEnumerator Run(Action onDone)
        {
            bool musicStopped = false;
            float t = 0f;
            while (t < Length)
            {
                if (t > blackFade + 0.3f && t < FadeOutStart && SkipPressed()) t = FadeOutStart;
                if (!musicStopped && t >= FadeOutStart)
                {
                    Audio.Sfx.StopMusic(fadeOut); // müzik logoyla birlikte söner
                    musicStopped = true;
                }
                Evaluate(t);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Evaluate(Length);
            instance = null;
            if (onDone != null)
            {
                onDone();
                yield break;
            }
            for (float k = 0f; k < 0.4f; k += Time.unscaledDeltaTime)
            {
                background.color = new Color(0f, 0f, 0f, 1f - k / 0.4f);
                yield return null;
            }
            Destroy(gameObject);
        }

        static bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        public void Build()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "StudioLogoCanvas", 1000);
            background = UiBuilder.CreateImage(canvas.transform, "Background", Color.clear);
            UiBuilder.Stretch(background.rectTransform, Vector2.zero, Vector2.one);

            var root = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.sizeDelta = Vector2.zero;
            content = root.GetComponent<CanvasGroup>();
            content.blocksRaycasts = false;

            var logo = UiBuilder.CreateImage(root, "Logo", Color.white);
            var texture = Resources.Load<Texture2D>(LogoResource);
            if (texture)
            {
                logo.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                logo.rectTransform.sizeDelta = new Vector2(logoHeight * texture.width / texture.height, logoHeight);
            }
            else
            {
                Debug.LogWarning("[StudioLogo] Logo bulunamadı: Resources/" + LogoResource);
                logo.color = Color.clear;
            }
            Evaluate(0f);
        }

        /// <summary>t anındaki kareyi çizer (saniye, logonun başından)</summary>
        public void Evaluate(float t)
        {
            background.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, t / blackFade));
            float appear = Mathf.SmoothStep(0f, 1f, (t - blackFade) / fadeIn);
            float vanish = Mathf.SmoothStep(0f, 1f, (t - FadeOutStart) / fadeOut);
            content.alpha = appear * (1f - vanish);
        }
    }
}
