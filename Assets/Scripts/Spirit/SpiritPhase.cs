using Deprem.Core;
using UnityEngine;

namespace Deprem.Spirit
{
    // Ruh fazı: faz Spirit olunca ruhu yatağın yanında çıkarır ve sayacı başlatır.
    // M49 (Mehmet'in play testi 26: ruh birden beliriyordu): ruh yatan bedenin içinden saydamdan görünüre geçerek,
    // küçükten büyüyüp hafifçe dalgalanarak yükselir; beden kısa bir an soğuk ışıkla parlar. Bu sırada oyuncu
    // kontrol edemez, sayaç ve konuşma çıkış bitince başlar. Ses: spirit.rise (ses bankasında yoksa sessiz).
    // Süre minigame sırasında da akar. Süre bitince ruh yatakta yatan bedene uçar, küçülüp solar (M31), sonra ana
    // depreme geçilir (beden yataktan kalkma animasyonuyla kalkar; Mehmet).
    // Ruh objesini kendisi kurar; sahnede tek bir objede durması yeter.
    public class SpiritPhase : MonoBehaviour
    {
        [Header("Süre")]
        [Tooltip("GDD: 60-90 sn; Play testinde fazla zaman kaldığı için 50")]
        [SerializeField] float duration = 50f;

        [Header("Çıkış noktası")]
        [Tooltip("Boşsa aşağıdaki isimdeki işaret noktası aranır")]
        [SerializeField] Transform spawnPoint;
        [SerializeField] string spawnMarkerName = "Marker_PlayerBed";
        [Tooltip("M50: ruhun kökü (ayakları) işaret noktasından bu kadar uzakta; görsel kökün CenterHeight üstünde")]
        [SerializeField] Vector3 spawnOffset = new Vector3(0f, 0.1f, 0f);

        [Header("Ruh")]
        [Tooltip("Boşsa geçici bir ruh (parlayan küre) kurulur")]
        [SerializeField] SpiritController spiritPrefab;
        [Tooltip("Ekip çizimi ruh_N.png (prefab kurulurken Art/Characters'tan bağlanır); yoksa parlayan küre")]
        [SerializeField] Texture2D[] spiritFrames;
        [Tooltip("M30: dururken ekip çizimi ruh_bekleme_N.png (prefab kurulurken bağlanır)")]
        [SerializeField] Texture2D[] spiritIdleFrames;

        [Header("Bedenden çıkış (M49)")]
        [Tooltip("Ruhun bedenden doğrulup yükselmesi (sn)")]
        [SerializeField] float emergeSeconds = 1.1f;
        [Tooltip("Yükselirken yana dalgalanma (m)")]
        [SerializeField] float emergeSway = 0.08f;
        [Tooltip("Bedenin parlaması: en yüksek şiddet")]
        [SerializeField] float emergeGlow = 4f;

        [Header("Bedene dönüş (M31)")]
        [Tooltip("Ruhun bedene uçuşu (sn, yavaş başlar hızlanır)")]
        [SerializeField] float returnFlySeconds = 0.8f;
        [Tooltip("Bedenin üstünde küçülüp solma (sn)")]
        [SerializeField] float returnFadeSeconds = 0.35f;
        [Tooltip("Yatan bedenin ortası, oyuncunun kökünden (yatan görsel pivotun yanında uzanır)")]
        [SerializeField] Vector3 bodyCenterOffset = new Vector3(0.6f, 0.35f, 0f);
        [Tooltip("Ruhun konuşma balonunun fontu: oyuncu ve aile balonlarıyla aynı (DepremBalon), prefab kurulurken bağlanır")]
        [SerializeField] Font bubbleFont;
        [SerializeField] int bubbleFontSize = 18;
        [SerializeField] Color glowColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField] float glowIntensity = 3f;
        [SerializeField] float glowRange = 3f;

        [Header("Test")]
        [Tooltip("Sahnede MinigameHost yoksa F ile eşya anında sabitlenir")]
        [SerializeField] bool instantSecureWithoutMinigame = true;

        SpiritController spirit;
        float remaining;
        bool running;
        Coroutine returning;
        Coroutine emerging;
        Light bodyGlow;

        // Ruh fazını daha önce görmüş mü (sahne yeniden yüklense de kalır): ilk sefer uzun, sonra kısa konuşur
        static bool seenBefore;

        public static bool Running { get; private set; }
        public static float TimeLeft { get; private set; }
        public static float Duration { get; private set; } = 60f;

        void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;

        void OnDisable()
        {
            GameEvents.PhaseChanged -= HandlePhaseChanged;
            Running = false;
        }

        void Update()
        {
            if (!running) return;

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            TimeLeft = remaining;
            GameEvents.RaiseSpiritTimeChanged(remaining);

            // Süre bitti: ruh bedene döner, sonra faz değişir (HandlePhaseChanged ruhu kapatır)
            if (remaining <= 0f && returning == null) returning = StartCoroutine(ReturnToBody());
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Spirit) Begin();
            else if (running || returning != null || emerging != null) End();
        }

        // M31: sayaç durur, HUD kapanır (Running = false), açık minigame kapanır; ruh bedene doğru hızlanarak uçar,
        // bedenin üstünde küçülüp solar, sonra ana depreme geçilir
        System.Collections.IEnumerator ReturnToBody()
        {
            running = false;
            Running = false;
            spirit.Locked = true;
            var interactor = spirit.GetComponent<SpiritInteractor>();
            if (interactor) interactor.enabled = false;
            var bubble = spirit.GetComponent<SpiritBubble>();
            if (bubble) bubble.Hide();
            if (Minigames.MinigameHost.Instance) Minigames.MinigameHost.Instance.CloseIfOpen();

            var player = GameObject.Find("Player");
            var start = spirit.transform.position;
            var body = player ? player.transform.position + bodyCenterOffset : start + Vector3.up * spirit.CenterHeight;
            body.z = start.z;
            var visual = spirit.GetComponentInChildren<SpiritVisual>();
            // M50: kök ayak hizasında; görselin merkezi bedene otursun diye kök bedenin CenterHeight × ölçek altında.
            // Sınırlar (zemin) dönüşü bozmasın diye hareket kapalı.
            spirit.enabled = false;
            for (float t = 0f; t < returnFlySeconds; t += Time.deltaTime)
            {
                float k = t / returnFlySeconds;
                spirit.transform.position = Vector3.Lerp(start, body - Vector3.up * spirit.CenterHeight, k * k); // ease-in
                yield return null;
            }
            for (float t = 0f; t < returnFadeSeconds; t += Time.deltaTime)
            {
                float k = t / returnFadeSeconds;
                float scale = Mathf.Lerp(1f, 0.25f, k);
                spirit.transform.localScale = Vector3.one * scale;
                spirit.transform.position = body - Vector3.up * spirit.CenterHeight * scale;
                if (visual) visual.SetFade(1f - k);
                yield return null;
            }
            returning = null;
            GameState.SetPhase(GamePhase.MainQuake);
        }

        void Begin()
        {
            if (!spirit) spirit = CreateSpirit();
            var spiritInteractor = spirit.GetComponent<SpiritInteractor>();
            spiritInteractor.InstantSecure = instantSecureWithoutMinigame;
            remaining = duration;
            Duration = duration;
            TimeLeft = remaining;
            running = false;
            Running = false;
            if (emerging != null) StopCoroutine(emerging);
            emerging = StartCoroutine(Emerge());
        }

        // M49: ruh bedenin içinden saydam ve küçük başlar, doğrulup yükselir (yavaş biter), yükselirken hafifçe yana
        // dalgalanır; beden soğuk ışıkla parlayıp söner. Bitince kontrol, sayaç ve konuşma başlar.
        System.Collections.IEnumerator Emerge()
        {
            var target = SpawnPosition();
            var player = GameObject.Find("Player");
            // Görselin merkezi bedenin ortasından başlasın (kök ayak hizasında, ölçek 0,55)
            var start = player ? player.transform.position + bodyCenterOffset : target;
            start -= Vector3.up * spirit.CenterHeight * 0.55f;
            start.z = target.z;

            // Çıkış boyunca hareket (sınırlar, süzülme) ve etkileşim kapalı
            spirit.Locked = true;
            spirit.enabled = false;
            spirit.PlaceVisual();
            var interactor = spirit.GetComponent<SpiritInteractor>();

            interactor.enabled = false;
            var visual = spirit.GetComponentInChildren<SpiritVisual>();
            spirit.transform.position = start;
            spirit.transform.localScale = Vector3.one * 0.55f;
            if (visual) visual.SetFade(0f);
            spirit.gameObject.SetActive(true);

            if (!bodyGlow)
            {
                bodyGlow = new GameObject("SpiritBodyGlow").AddComponent<Light>();
                bodyGlow.type = LightType.Point;
                bodyGlow.shadows = LightShadows.None;
                bodyGlow.color = glowColor;
                bodyGlow.range = glowRange;
            }
            bodyGlow.transform.position = start + new Vector3(0f, 0.2f, -0.8f);
            bodyGlow.gameObject.SetActive(true);
            Audio.Sfx.Play("spirit.rise");

            float total = Mathf.Max(0.1f, emergeSeconds);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                float k = t / total;
                float ease = 1f - (1f - k) * (1f - k);
                var position = Vector3.Lerp(start, target, ease);
                position.x += Mathf.Sin(k * Mathf.PI * 3f) * emergeSway * (1f - k);
                spirit.transform.position = position;
                spirit.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, ease);
                if (visual) visual.SetFade(Mathf.Clamp01(k * 1.6f));
                bodyGlow.intensity = emergeGlow * Mathf.Sin(k * Mathf.PI);
                yield return null;
            }
            spirit.transform.position = target;
            spirit.transform.localScale = Vector3.one;
            if (visual) visual.SetFade(1f);
            bodyGlow.gameObject.SetActive(false);

            spirit.enabled = true;
            spirit.Locked = false;
            interactor.enabled = true;
            emerging = null;

            running = true;
            Running = true;
            GameEvents.RaiseSpiritTimeChanged(remaining);

            // Berke'nin play testi: ruh formuna geçince karakter bir şey demeli
            var bubble = spirit.GetComponent<SpiritBubble>();
            if (bubble == null)
            {
                bubble = spirit.gameObject.AddComponent<SpiritBubble>();
                bubble.SetFont(bubbleFont, bubbleFontSize);
            }
            if (seenBefore) bubble.Say(("spirit.again", 2.5f));
            else bubble.Say(("spirit.intro_1", 2.8f), ("spirit.intro_2", 3f));
            seenBefore = true;
        }

        void End()
        {
            if (returning != null) StopCoroutine(returning);
            returning = null;
            if (emerging != null) StopCoroutine(emerging);
            emerging = null;
            if (bodyGlow) bodyGlow.gameObject.SetActive(false);
            if (spirit) spirit.enabled = true;

            running = false;
            Running = false;
            TimeLeft = 0f;
            if (spirit) spirit.gameObject.SetActive(false);
        }

        Vector3 SpawnPosition()
        {
            if (!spawnPoint && !string.IsNullOrEmpty(spawnMarkerName))
            {
                var marker = GameObject.Find(spawnMarkerName);
                if (marker) spawnPoint = marker.transform;
            }
            return (spawnPoint ? spawnPoint.position : transform.position) + spawnOffset;
        }

        SpiritController CreateSpirit()
        {
            if (spiritPrefab)
            {
                var instance = Instantiate(spiritPrefab);
                if (!instance.GetComponent<SpiritInteractor>()) instance.gameObject.AddComponent<SpiritInteractor>();
                return instance;
            }

            var root = new GameObject("Spirit");
            root.SetActive(false);

            // Ruh çizimi (bacakları dağılan koşu); yoksa geçici parlayan küre
            Transform body;
            if (spiritFrames != null && spiritFrames.Length > 0)
            {
                body = SpiritVisual.Create(root.transform, spiritFrames, spiritIdleFrames).transform;
            }
            else
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Visual";
                Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localScale = Vector3.one * 0.35f;
                sphere.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                body = sphere.transform;
            }

            var controller = root.AddComponent<SpiritController>();
            controller.SetVisual(body);

            // Işık gövdenin hizasında (kök ayaklarda, M50)
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = Vector3.up * controller.CenterHeight;
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.intensity = glowIntensity;
            glow.range = glowRange;
            glow.shadows = LightShadows.None;

            root.AddComponent<SpiritInteractor>();
            return controller;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Running = false;
            TimeLeft = 0f;
            Duration = 60f;
            seenBefore = false;
        }
    }
}
