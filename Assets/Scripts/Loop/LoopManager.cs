using System.Collections;
using Deprem.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deprem.Loop
{
    // Döngü yöneticisi: denemeyi başlatır, kaybedince keşke ekranından sonra
    // zamanı geri sarar (sahneyi yeniden yükler) ve doğru döngüden devam eder.
    //
    // Döngü ilerlemesi: her döngünün öğrettiği tek bir ders vardır (lessonPerLoop).
    // O dersle kaybedilirse sonraki döngüye geçilir; başka bir sebeple kaybedilirse
    // aynı döngü tekrarlanır. Döngü 5 her zaman tekrarlanır.
    public class LoopManager : MonoBehaviour
    {
        // Sahne yeniden yüklenirken hangi döngüden başlanacağını taşır
        static int pendingLoop;

        [Header("Başlangıç")]
        [SerializeField] bool startOnPlay = true;
        [SerializeField, Range(1, GameConstants.LoopCount)] int firstLoop = 1;

        [Header("Kaybedince")]
        [Tooltip("Açıksa keşke ekranı ContinueAfterWish() çağırana kadar beklenir")]
        [SerializeField] bool waitForWishScreen = false;
        [Tooltip("Keşke ekranı yokken otomatik devam süresi (sn)")]
        [SerializeField] float autoContinueDelay = 3f;
        [Tooltip("Zamanı geri sarmak için sahneyi yeniden yükle")]
        [SerializeField] bool reloadSceneOnRestart = true;

        [Header("Döngü dersleri (döngü 1-4)")]
        [SerializeField] LoseReason[] lessonPerLoop =
        {
            LoseReason.IgnoredForeshock, // 1: Keşke dikkate alsaydım
            LoseReason.NoDuckCoverHold,  // 2: Keşke çök-kapan-tutun yapsaydım
            LoseReason.FamilyHitByItem,  // 3: Keşke eşyaları sabitleseydim
            LoseReason.MissingBagItem,   // 4: Keşke deprem çantası hazırlasaydım
        };

        int nextLoop;
        bool waitingToContinue;

        public static LoopManager Instance { get; private set; }

        // Sahnede keşke ekranı varsa otomatik devam edilmez; ekran ContinueAfterWish() çağırır
        public static bool WishScreenPresent { get; set; }

        // Sahnede ana menü varsa oyun kendiliğinden başlamaz; menü StartFromLoop() çağırır
        public static bool MenuPresent { get; set; }

        // Sahne bir deneme başlatmak için yeniden yüklendiyse true (ana menü gizli kalır)
        public static bool HasPendingLoop => pendingLoop > 0;
        public bool WaitingToContinue => waitingToContinue;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable() => GameEvents.LoopFailed += HandleLoopFailed;
        void OnDisable() => GameEvents.LoopFailed -= HandleLoopFailed;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            // Bootstrap'ın yüklediği sistemler event'lere abone olsun diye bir kare bekle
            yield return null;

            if (pendingLoop > 0)
            {
                int loop = pendingLoop;
                pendingLoop = 0;
                GameState.BeginAttempt(loop);
            }
            else if (startOnPlay && !MenuPresent)
            {
                GameState.ResetAll();
                GameState.BeginAttempt(firstLoop);
            }
        }

        // Ana menüden yeni oyun ya da bölüm seçimi
        public void StartFromLoop(int loop)
        {
            GameState.ResetAll();
            Restart(loop);
        }

        // Final ekranı ya da duraklatma menüsünden ana menüye dönüş
        public void ReturnToMenu()
        {
            StopAllCoroutines();
            waitingToContinue = false;
            pendingLoop = 0;
            Time.timeScale = 1f;
            GameState.ResetAll();

            int sceneIndex = SceneManager.GetActiveScene().buildIndex;
            if (sceneIndex >= 0) SceneManager.LoadScene(sceneIndex);
            else Debug.LogWarning("[Loop] Sahne build listesinde değil, ana menüye dönülemiyor (test sahnesi).");
        }

        // Keşke ekranı bitince UI çağırır
        public void ContinueAfterWish()
        {
            if (!waitingToContinue) return;
            waitingToContinue = false;
            Restart(nextLoop);
        }

        void HandleLoopFailed(LoseReason reason, ItemId item)
        {
            nextLoop = NextLoopAfter(GameState.CurrentLoop, reason);
            waitingToContinue = true;
            if (!waitForWishScreen && !WishScreenPresent) StartCoroutine(AutoContinue());
        }

        IEnumerator AutoContinue()
        {
            yield return new WaitForSecondsRealtime(autoContinueDelay);
            ContinueAfterWish();
        }

        int NextLoopAfter(int loop, LoseReason reason)
        {
            int index = loop - 1;
            bool learnedLesson = index >= 0 && index < lessonPerLoop.Length && lessonPerLoop[index] == reason;
            return learnedLesson ? Mathf.Min(loop + 1, GameConstants.LoopCount) : loop;
        }

        void Restart(int loop)
        {
            Time.timeScale = 1f; // duraklatma menüsünden gelinmiş olabilir
            int sceneIndex = SceneManager.GetActiveScene().buildIndex;
            if (reloadSceneOnRestart && sceneIndex >= 0)
            {
                pendingLoop = loop;
                SceneManager.LoadScene(sceneIndex);
                return;
            }

            if (reloadSceneOnRestart)
                Debug.LogWarning("[Loop] Sahne build listesinde değil, yeniden yüklenmeden devam ediliyor (test sahnesi).");
            GameState.BeginAttempt(loop);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            pendingLoop = 0;
            Instance = null;
            WishScreenPresent = false;
            MenuPresent = false;
        }
    }
}
