using System;
using System.Collections;
using Deprem.Core;
using Deprem.Player;
using Deprem.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// ENTEGRASYON "Outro (mezarlık)": Berke'nin final ekranının dinlediği olaylar. OutroCinematic doldurur.
    /// </summary>
    public static class Outro
    {
        /// <summary>Sahnede outro var mı (yoksa final ekranı eski akışını oynatır)</summary>
        public static bool Available { get; internal set; }
        public static bool IsPlaying { get; internal set; }
        /// <summary>Yürüyüş başladı; parametre: ilk mezara kadar kalan süre (sn). Farkındalık mesajları bu sırada.</summary>
        public static event Action<float> AwarenessStarted;
        /// <summary>Karakter mezarlara vardı: mesajlar sönsün</summary>
        public static event Action AwarenessEnded;
        /// <summary>Karakter silindi, ekran tam siyah: geçmiş depremleri anma başlasın</summary>
        public static event Action Finished;

        internal static void RaiseAwarenessStarted(float seconds) => AwarenessStarted?.Invoke(seconds);
        internal static void RaiseAwarenessEnded() => AwarenessEnded?.Invoke();
        internal static void RaiseFinished() => Finished?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Available = false;
            IsPlaying = false;
            AwarenessStarted = null;
            AwarenessEnded = null;
            Finished = null;
        }
    }

    /// <summary>
    /// Oyun sonu (Mehmet'in tasarımı): herkes kabine girince ekran kararır, mezarlık açılır. Ana karakter soldan elinde
    /// gülle yürür (bu sırada Berke'nin farkındalık mesajları), baba, anne ve kardeşin mezarına eğilip birer gül bırakır,
    /// her birine keşkesini söyler. Bitti sanılırken kamera sağa kayar: dördüncü mezar onun. Oraya da gül bırakır,
    /// "keşke kalkabilseydim" der ve silinir. Ekran kararır; geçmiş depremleri anma Berke'nin final ekranında.
    /// Mezarlık evin uzağında, HouseBuilder kurar; kamera bu sırada ortografik ve sabit (Cinemachine kapalı).
    /// </summary>
    public sealed class OutroCinematic : MonoBehaviour
    {
        [Serializable]
        public struct Grave
        {
            [Tooltip("Mezarın ortası (dünya x)")] public float x;
            [Tooltip("Bırakılan gül (başta gizli)")] public GameObject rose;
            [Tooltip("Balon (texts.csv)")] public string bubbleKey;
        }

        [Header("Sahne (HouseBuilder doldurur)")]
        [SerializeField] private Transform actor;
        [SerializeField] private Renderer actorRenderer;
        [SerializeField] private SpeechBubble bubble;
        [SerializeField] private Grave[] graves;
        [Tooltip("Mezarlık görselinin sol ve sağ kenarı (dünya x)")]
        [SerializeField] private Vector2 backdropEdges;
        [Tooltip("Görselin dikey ortası ve yarı yüksekliği (ortografik kamera boyu)")]
        [SerializeField] private float backdropCenterY;
        [SerializeField] private float backdropHalfHeight = 3.92f;
        [Tooltip("İlk kadrajın sağ kenarı (dünya x): ilk üç mezar sığar, dördüncü dışarıda")]
        [SerializeField] private float firstFrameRight;
        [Tooltip("Mehmet'in isteği: daha yakından. Kameranın yarı yüksekliği (m; görselin tamamı 3,92)")]
        [SerializeField] private float viewHalfHeight = 2.5f;
        [Tooltip("Kameranın dikey ortası (dünya y): mezarlar ve karakter kadrajda")]
        [SerializeField] private float viewCenterY;
        [Tooltip("Yürürken kamera karakteri izler; karakter kadraj ortasının bu oranda solunda kalır")]
        [SerializeField, Range(0f, 0.8f)] private float followLead = 0.25f;
        [Tooltip("Karakterin yürüdüğü çizgi (ayak, dünya y)")]
        [SerializeField] private float walkY;

        [Header("Kareler")]
        [SerializeField] private Texture2D[] walkFrames;
        [SerializeField] private Texture2D[] bendFrames;      // elinde gülle eğilir
        [SerializeField] private Texture2D[] bendEmptyFrames; // gül bırakıldı, kalkarken eli boş
        [SerializeField] private Texture2D[] idleFrames;

        [Header("Zamanlama")]
        [Tooltip("Kabin kapanma anı oynansın diye kararmadan önceki bekleme (sn)")]
        [SerializeField] private float startDelay = 2f;
        [SerializeField] private float fadeOutTime = 1.2f;
        [SerializeField] private float fadeInTime = 1.6f;
        [Tooltip("Yürüme hızı (m/sn); elde yürüme çizimi olmadığı için koşma kareleri yavaş oynar")]
        [SerializeField] private float walkSpeed = 0.75f;
        [SerializeField] private float walkFps = 6f;
        [SerializeField] private float bendFps = 7f;
        [Tooltip("Mezarın bu kadar solunda durur (m): diz çökünce el mezarın önüne gelir")]
        [SerializeField] private float standOffset = 0.6f;
        [Tooltip("Her mezardaki balonun süresi (sn)")]
        [SerializeField] private float graveTalkTime = 4.2f;
        [Tooltip("Üç mezardan sonra 'bitti' sanılan sessizlik (sn)")]
        [SerializeField] private float falseEndPause = 1.8f;
        [SerializeField] private float panTime = 3f;
        [Tooltip("Son mezarda balondan sonra karakterin silinmesi (sn)")]
        [SerializeField] private float vanishTime = 2.4f;
        [SerializeField] private float finalFadeTime = 2f;
        [Tooltip("Mezarlık gece: karakter bu renkle çarpılır")]
        [SerializeField] private Color actorTint = new Color(0.8f, 0.82f, 0.95f, 1f);

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Image fade;
        private Camera cam;
        private bool awarenessOpen;
        private bool finished;

        private void OnEnable()
        {
            Outro.Available = true;
            GameEvents.GameWon += HandleGameWon;
        }

        private void OnDisable()
        {
            Outro.Available = false;
            GameEvents.GameWon -= HandleGameWon;
        }

        private void Awake()
        {
            if (actor != null) actor.gameObject.SetActive(false);
            foreach (var grave in graves)
                if (grave.rose != null) grave.rose.SetActive(false);
        }

        // Geliştirici kısayolu (Mehmet'in isteği): oyun sürerken Ctrl+Shift+O oyunu kazanılmış sayar, outro hemen
        // başlar. Sadece editörde ve development build'de.
        private void Update()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.oKey.wasPressedThisFrame) return;
            if (!keyboard.ctrlKey.isPressed || !keyboard.shiftKey.isPressed) return;
            // Ana menüde (faz yok, zaman durmuş) ve keşke ekranında (Failed) çalışmaz
            if (Outro.IsPlaying || GameState.Phase == GamePhase.None || GameState.Phase == GamePhase.Failed ||
                GameState.Phase == GamePhase.Won) return;
            Debug.Log("[Outro] Geliştirici kısayolu: oyun kazanıldı sayıldı, outro başlıyor");
            GameState.WinLoop();
        }

        private void HandleGameWon()
        {
            StopAllCoroutines();
            StartCoroutine(Play());
        }

        // ------------------------------------------------------------------ Akış

        private IEnumerator Play()
        {
            Outro.IsPlaying = true;
            finished = false;
            yield return new WaitForSecondsRealtime(startDelay);
            yield return Fade(0f, 1f, fadeOutTime);

            Cinematic.Begin(true, Skip);
            TakeCamera();
            Audio.Sfx.PlayMusic("music.final", 2f);
            actor.gameObject.SetActive(true);
            float halfWidth = HalfWidth;
            // Görselin sol kenarının hemen dışından girer; kamera onu izleyip ilk kadraja (üç mezar) varınca durur
            float startX = backdropEdges.x - 0.8f;
            float firstFrameX = firstFrameRight - halfWidth;
            actor.position = new Vector3(startX, walkY, actor.position.z);
            SetCameraX(FollowX(firstFrameX));
            SetFrame(walkFrames[0], 1f);

            StartCoroutine(Fade(1f, 0f, fadeInTime));
            float firstStop = graves[0].x - standOffset;
            awarenessOpen = true;
            Outro.RaiseAwarenessStarted((firstStop - startX) / walkSpeed);
            yield return WalkTo(firstStop, firstFrameX);
            awarenessOpen = false;
            Outro.RaiseAwarenessEnded();

            // Baba, anne, kardeş: her mezara gül ve keşke
            int familyGraves = Mathf.Min(3, graves.Length - 1);
            for (int i = 0; i < familyGraves; i++)
            {
                yield return WalkTo(graves[i].x - standOffset);
                yield return PlaceRose(graves[i], rise: true);
            }

            // Bitti sanılır; sonra kamera sağa kayar, karakter kendi mezarına yürür
            yield return Idle(falseEndPause);
            var last = graves[graves.Length - 1];
            StartCoroutine(PanCamera(last.x - halfWidth * 0.2f, panTime)); // son mezar ortanın biraz sağında
            yield return WalkTo(last.x - standOffset);
            yield return PlaceRose(last, rise: false);
            yield return Vanish();
            yield return new WaitForSeconds(1.2f);
            yield return Fade(0f, 1f, finalFadeTime);
            Finish();
        }

        /// <summary>Cinematic.Skip: doğrudan siyaha geçilir, anma başlar</summary>
        private void Skip()
        {
            StopAllCoroutines();
            SetFadeAlpha(1f);
            if (bubble != null) bubble.gameObject.SetActive(false);
            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (awarenessOpen)
            {
                awarenessOpen = false;
                Outro.RaiseAwarenessEnded();
            }
            if (actor != null) actor.gameObject.SetActive(false);
            Cinematic.End();
            Outro.IsPlaying = false;
            Outro.RaiseFinished();
        }

        // ------------------------------------------------------------------ Karakter

        /// <param name="followUntil">NaN değilse kamera karakteri izler, bu x'te durur</param>
        private IEnumerator WalkTo(float x, float followUntil = float.NaN)
        {
            float frameTime = 0f;
            int frame = 0;
            while (actor.position.x < x - 0.01f)
            {
                var p = actor.position;
                p.x = Mathf.MoveTowards(p.x, x, walkSpeed * Time.deltaTime);
                actor.position = p;
                if (!float.IsNaN(followUntil)) SetCameraX(FollowX(followUntil));
                frameTime += Time.deltaTime * walkFps;
                frame = (int)frameTime % walkFrames.Length;
                SetFrame(walkFrames[frame], 1f);
                yield return null;
            }
        }

        private IEnumerator Idle(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (idleFrames.Length > 0) SetFrame(idleFrames[(int)(t * 2f) % idleFrames.Length], 1f);
                yield return null;
            }
        }

        /// <summary>Diz çöker, gülü mezara bırakır, keşkesini söyler; rise ise kalkar</summary>
        private IEnumerator PlaceRose(Grave grave, bool rise)
        {
            yield return Idle(0.35f);
            foreach (var frame in bendFrames) yield return Hold(frame, 1f / bendFps);
            yield return Hold(bendFrames[bendFrames.Length - 1], 0.35f);
            SetFrame(bendEmptyFrames[bendEmptyFrames.Length - 1], 1f);
            if (grave.rose != null) grave.rose.SetActive(true);
            Audio.Sfx.PlayAt("minigame.drop", grave.rose != null ? grave.rose.transform.position : actor.position, 0.35f);
            yield return new WaitForSeconds(0.5f);
            if (bubble != null && !string.IsNullOrEmpty(grave.bubbleKey)) bubble.Say(grave.bubbleKey, graveTalkTime);
            yield return new WaitForSeconds(graveTalkTime + 0.4f);
            if (!rise) yield break;
            for (int i = bendEmptyFrames.Length - 1; i >= 0; i--) yield return Hold(bendEmptyFrames[i], 1f / bendFps);
        }

        private IEnumerator Hold(Texture2D frame, float seconds)
        {
            SetFrame(frame, 1f);
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>Diz çökmüş karakter yavaşça saydamlaşıp biraz yükselerek silinir</summary>
        private IEnumerator Vanish()
        {
            var frame = bendEmptyFrames[bendEmptyFrames.Length - 1];
            var start = actor.position;
            for (float t = 0f; t < vanishTime; t += Time.deltaTime)
            {
                float k = t / vanishTime;
                SetFrame(frame, 1f - k * k);
                actor.position = start + Vector3.up * (0.25f * k);
                yield return null;
            }
            actor.gameObject.SetActive(false);
        }

        private void SetFrame(Texture2D frame, float alpha)
        {
            if (actorRenderer == null || frame == null) return;
            block ??= new MaterialPropertyBlock();
            actorRenderer.GetPropertyBlock(block);
            block.SetTexture(BaseMap, frame);
            block.SetColor(BaseColor, new Color(actorTint.r, actorTint.g, actorTint.b, actorTint.a * alpha));
            actorRenderer.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ Kamera ve kararma

        private void TakeCamera()
        {
            cam = Camera.main;
            if (cam == null) return;
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = ViewHalfHeight;
            cam.transform.rotation = Quaternion.identity;
        }

        // Yakın kadraj görselin yüksekliğini aşmasın
        private float ViewHalfHeight => Mathf.Min(viewHalfHeight, backdropHalfHeight);
        private float HalfWidth => ViewHalfHeight * (cam != null ? cam.aspect : 16f / 9f);

        /// <summary>Yürürken: karakter ortanın biraz solunda; kamera limit x'i geçmez</summary>
        private float FollowX(float limit) => Mathf.Min(actor.position.x + HalfWidth * followLead, limit);

        private void SetCameraX(float x)
        {
            if (cam == null) return;
            // Görselin dışına taşmasın
            float halfWidth = HalfWidth, halfHeight = ViewHalfHeight;
            x = Mathf.Clamp(x, backdropEdges.x + halfWidth, backdropEdges.y - halfWidth);
            float bottom = backdropCenterY - backdropHalfHeight, top = backdropCenterY + backdropHalfHeight;
            float y = Mathf.Clamp(viewCenterY, bottom + halfHeight, top - halfHeight);
            cam.transform.position = new Vector3(x, y, transform.position.z - 10f);
        }

        private IEnumerator PanCamera(float toX, float seconds)
        {
            if (cam == null) yield break;
            float from = cam.transform.position.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetCameraX(Mathf.Lerp(from, toX, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetCameraX(toX);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFadeAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetFadeAlpha(to);
        }

        private void SetFadeAlpha(float alpha)
        {
            if (fade == null)
            {
                var canvas = UiBuilder.CreateCanvas(transform, "OutroFade", 98); // bantların altında, final ekranının altında
                fade = UiBuilder.CreateImage(canvas.transform, "Black", Color.black);
                UiBuilder.Stretch(fade.rectTransform, Vector2.zero, Vector2.one);
            }
            fade.color = new Color(0f, 0f, 0f, alpha);
            fade.enabled = alpha > 0.001f;
        }
    }
}
