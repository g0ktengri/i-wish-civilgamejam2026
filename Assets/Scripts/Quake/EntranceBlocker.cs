using System.Collections;
using Deprem.Audio;
using Deprem.Core;
using UnityEngine;

namespace Deprem.Quake
{
    /// <summary>
    /// Giriş kapısının yanındaki uzun portmanto dolabı (intro sinematiği): her ana depremde kameraya doğru devrilip
    /// kapının önünü kapatır; eve neden kapanıldığı, kabine neden gidildiği anlaşılsın. Sabitlenemez. Pivot dolabın
    /// alt ön kenarında; dönen çarpışma kutusu devrilince karakterin yolunu keser. HouseBuilder kurar.
    /// </summary>
    public sealed class EntranceBlocker : MonoBehaviour
    {
        [Tooltip("Devrilme süresi (sn): önce yavaş yalpalar, sonra hızlanarak düşer")]
        [SerializeField] private float fallTime = 0.9f;
        [SerializeField] private float wobbleTime = 0.6f;

        public static EntranceBlocker Instance { get; private set; }
        public bool HasFallen { get; private set; }
        public bool IsFalling { get; private set; }

        private static readonly Quaternion FallenRotation = Quaternion.Euler(-90f, 0f, 0f);
        private bool warnedRestored;

        private void Awake() => Instance = this;

        private void OnEnable() => GameEvents.PhaseChanged += HandlePhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= HandlePhaseChanged;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Play testi 32: kurtarmada devrik dolap geri dikilmiş görünüyordu. Ne olursa olsun kurtarma fazında yerde kalır.
        // Play testi 33: ruh fazında (döngü 4+) dik duruyordu. Ruh fazında da devrik; ana depremde yeniden devrilmez.
        private void Start()
        {
            if (GameState.Phase == GamePhase.Spirit) SetFallen();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Rescue && phase != GamePhase.Spirit || HasFallen) return;
            if (IsFalling) return; // düşüş bitince HasFallen olur
            SetFallen();
        }

        private void LateUpdate()
        {
            if (!HasFallen || transform.localRotation == FallenRotation) return;
            if (!warnedRestored)
            {
                warnedRestored = true;
                Debug.LogWarning("[EntranceBlocker] Devrik dolabın açısını başka bir şey değiştirdi; yerine kondu.");
            }
            transform.localRotation = FallenRotation;
        }

        private void SetFallen()
        {
            StopAllCoroutines();
            IsFalling = false;
            HasFallen = true;
            transform.localRotation = FallenRotation;
        }

        public void Topple()
        {
            if (HasFallen || IsFalling) return;
            StartCoroutine(Fall());
        }

        private IEnumerator Fall()
        {
            IsFalling = true;
            // Yalpalama: ileri geri sallanır
            for (float t = 0f; t < wobbleTime; t += Time.deltaTime)
            {
                float k = t / wobbleTime;
                transform.localRotation = Quaternion.Euler(-Mathf.Sin(k * Mathf.PI * 3f) * 6f * k, 0f, 0f);
                yield return null;
            }
            // Devrilme: kameraya doğru (pivot alt ön kenarda), hızlanarak
            float start = transform.localEulerAngles.x;
            if (start > 180f) start -= 360f;
            for (float t = 0f; t < fallTime; t += Time.deltaTime)
            {
                float k = t / fallTime;
                transform.localRotation = Quaternion.Euler(Mathf.Lerp(start, -90f, k * k), 0f, 0f);
                yield return null;
            }
            transform.localRotation = FallenRotation;
            Sfx.PlayAt("item.crash", transform.position, 1f);
            Sfx.PlayAt("quake.crumble", transform.position, 0.8f);
            QuakeCameraShake.Intensity = Mathf.Max(QuakeCameraShake.Intensity, 1f);
            IsFalling = false;
            HasFallen = true;
        }
    }
}
