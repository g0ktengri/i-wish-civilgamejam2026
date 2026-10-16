using Deprem.Core;
using UnityEngine;

namespace Deprem.Spirit
{
    // Ruh fazı: faz Spirit olunca ruhu yatağın yanında çıkarır ve sayacı başlatır.
    // Süre minigame sırasında da akar. Süre bitince ana depreme geçer.
    // Ruh objesini kendisi kurar; sahnede tek bir objede durması yeter.
    public class SpiritPhase : MonoBehaviour
    {
        [Header("Süre")]
        [Tooltip("GDD: 60-90 sn arası, testle ayarlanacak")]
        [SerializeField] float duration = 60f;

        [Header("Çıkış noktası")]
        [Tooltip("Boşsa aşağıdaki isimdeki işaret noktası aranır")]
        [SerializeField] Transform spawnPoint;
        [SerializeField] string spawnMarkerName = "Marker_PlayerBed";
        [SerializeField] Vector3 spawnOffset = new Vector3(0f, 1f, 0f);

        [Header("Ruh")]
        [Tooltip("Boşsa geçici bir ruh (parlayan küre) kurulur")]
        [SerializeField] SpiritController spiritPrefab;
        [SerializeField] Color glowColor = new Color(0.55f, 0.8f, 1f);
        [SerializeField] float glowIntensity = 3f;
        [SerializeField] float glowRange = 3f;

        [Header("Test")]
        [Tooltip("Minigame'ler hazır olana kadar F ile eşya anında sabitlenir")]
        [SerializeField] bool instantSecureWithoutMinigame = true;

        SpiritController spirit;
        float remaining;
        bool running;

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

            // Faz değişince HandlePhaseChanged ruhu kapatır
            if (remaining <= 0f) GameState.SetPhase(GamePhase.MainQuake);
        }

        void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Spirit) Begin();
            else if (running) End();
        }

        void Begin()
        {
            if (!spirit) spirit = CreateSpirit();
            spirit.transform.position = SpawnPosition();
            spirit.GetComponent<SpiritInteractor>().InstantSecure = instantSecureWithoutMinigame;
            spirit.gameObject.SetActive(true);

            remaining = duration;
            Duration = duration;
            TimeLeft = remaining;
            running = true;
            Running = true;
            GameEvents.RaiseSpiritTimeChanged(remaining);
        }

        void End()
        {
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

            // Geçici görünüm: oyuncu sprite'ı + ruh shader'ı gelene kadar parlayan küre
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Visual";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 0.35f;
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.intensity = glowIntensity;
            glow.range = glowRange;
            glow.shadows = LightShadows.None;

            var controller = root.AddComponent<SpiritController>();
            controller.SetVisual(body.transform);
            root.AddComponent<SpiritInteractor>();
            return controller;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Running = false;
            TimeLeft = 0f;
            Duration = 60f;
        }
    }
}
