using System.Collections;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Family;
using Deprem.Player;
using Deprem.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.Quake
{
    /// <summary>
    /// Oyunun açılış sinematiği (Mehmet'in isteği, ENTEGRASYON "Intro sinematiği"): 1. döngünün ilk denemesinde,
    /// öncü sarsıntıdan önce. Siyahtan açılır; oyuncu okuldan gelir, mutfağın sağındaki giriş kapısından girer,
    /// salonda babasıyla laflaşır, merdivenin önünde üst kattan seslenen annesine ve kardeşine cevap verir, odasına
    /// gidip yatar. Oyuncu yönlendiremez, tuş ipucu görünmez (Cinematic.IsPlaying); "Geç" ile her an yatağa atlanır.
    /// QuakeDirector çalıştırır; bitince ekran karanlıkta kalır, yatağa yatırılınca FadeIn ile açılır.
    /// </summary>
    public sealed class IntroCinematic : MonoBehaviour
    {
        [Header("Yürüyüş")]
        [Tooltip("Okuldan yorgun gelir: normal yürüme hızının oranı")]
        [SerializeField] private float walkSpeedMultiplier = 0.55f;
        [Tooltip("Baba ile konuşurken oyuncunun durduğu X (baba salonda, 22,75)")]
        [SerializeField] private float fatherTalkX = 24.4f;
        [Tooltip("Merdivenin önünde durup üst kata seslendiği X")]
        [SerializeField] private float stairsTalkX = 13.6f;
        [Tooltip("Odasında yatağın yanında durduğu X")]
        [SerializeField] private float bedsideX = 2.9f;

        [Header("Üst kat (anne, kardeş merdiven başında)")]
        [SerializeField] private Vector3 motherSpot = new Vector3(15.4f, 3.75f, 1f);
        [SerializeField] private Vector3 siblingSpot = new Vector3(16.3f, 3.75f, 1f);
        [Tooltip("Merdivendeki konuşmada kamera iki katı birden göstersin (görüş açısı oranı; Berke'nin play testi: 1,3 → 1,6)")]
        [SerializeField] private float stairsViewFov = 1.6f;
        [Tooltip("Merdiven görüntüsünün yüksekliği: üst kattaki annenin yüksekliğinin oranı (annenin balonu bandın altında kalmasın)")]
        [SerializeField] private float stairsViewHeight = 0.7f;
        [Tooltip("Açılış: kararma açıldıktan sonra oyuncu dışarıda yürümeye başlamadan önceki bekleme (sn)")]
        [SerializeField] private float enterDelay = 0.6f;

        [Header("Dışarıdan giriş")]
        [Tooltip("Oyuncunun başladığı yer: dış duvarın dış yüzünden ne kadar sağda, karanlıkta (m)")]
        [SerializeField] private float outsideStartDistance = 2.6f;
        [Tooltip("Kapıyı açmak için durduğu yer: dış duvarın dış yüzünden ne kadar sağda (m)")]
        [SerializeField] private float doorstepDistance = 0.3f;
        [Tooltip("İçeri girince kapının eşiğinden ne kadar solda durup kapıyı kapattığı (m)")]
        [SerializeField] private float insideStopDistance = 1.1f;
        [Tooltip("Dışarıdan girerken yürüme hızı (m/sn; yorgun yürüyüşle aynı olsun)")]
        [SerializeField] private float enterWalkSpeed = 1.75f;
        [Tooltip("Dış duvarın kalınlığı (m; HouseDims.Thickness)")]
        [SerializeField] private float wallThickness = 0.25f;
        [Tooltip("Kapının önünde, açmadan önce durma (sn)")]
        [SerializeField] private float doorstepPause = 0.35f;
        [Tooltip("Dışarıda, karanlıkta oyuncunun aydınlığı (0..1); kapıdan geçerken normale açılır")]
        [SerializeField] private float outsideShade = 0.3f;

        [Header("Kararma")]
        [SerializeField] private float fadeInTime = 1.8f;
        [SerializeField] private float fadeOutTime = 1.4f;
        [SerializeField] private float nightFadeInTime = 2f;

        /// <summary>1. döngünün ilk denemesi ("Yeni oyun"): intro oynar</summary>
        public static bool ShouldPlay => GameState.CurrentLoop == 1 && GameState.FailedAttempts == 0;

        /// <summary>Bu denemede intro oynadı mı (ana depremde giriş kapısı yakın çekimi buna bakar)</summary>
        public bool PlayedThisAttempt { get; private set; }

        private bool skipped;
        private PlayerController player;
        private Image fade;

        public IEnumerator Play()
        {
            PlayedThisAttempt = true;
            skipped = false;
            player = FindFirstObjectByType<PlayerController>();
            EnsureFade();
            SetFade(1f);
            Cinematic.Begin(true, () => skipped = true);
            QuakePrompts.Clear();

            float speed = player != null ? player.SpeedMultiplier : 1f;
            if (player != null)
            {
                player.InputEnabled = false;
                player.SetScriptedInput(0f);
                player.JumpEnabled = false;
                player.SpeedMultiplier = walkSpeedMultiplier;
            }
            yield return Run(Scene());
            if (player != null) player.PathPaused = false; // "Geç" kapının önünde basıldıysa: yatağa yatırılınca yol biter
            SetShade(1f);

            // Bitiş (ya da "Geç"): karar, gece olur; aile odalarına geçer
            player?.SetScriptedInput(0f);
            PlayerCameraTarget.Release();
            yield return Fade(fade.color.a, 1f, skipped ? 0.4f : fadeOutTime);
            foreach (var member in FamilyMember.All) member.PlaceAtHome();
            if (EntranceDoor.Instance != null) EntranceDoor.Instance.SetOpenImmediate(false);
            if (player != null)
            {
                player.SpeedMultiplier = speed;
                player.JumpEnabled = true;
            }
            Cinematic.End();
        }

        /// <summary>Oyuncu yatağa yatırıldıktan sonra karanlıktan gece açılır</summary>
        public IEnumerator FadeIn()
        {
            yield return new WaitForSeconds(0.3f);
            yield return Fade(1f, 0f, nightFadeInTime);
        }

        private IEnumerator Scene()
        {
            var father = FamilyMember.Find(FamilyMemberId.Father);
            var mother = FamilyMember.Find(FamilyMemberId.Mother);
            var sibling = FamilyMember.Find(FamilyMemberId.Sibling);
            var door = EntranceDoor.Instance;

            // Kurulum, ekran siyahken: oyuncu evin dışında, karanlıkta; giriş kapısı kapalı, iç kapılar açık, aile akşam
            // yerlerinde. Play testi: kapı kapalı başlasın; oyuncu içeride belirmesin, dışarıdan yürüyüp kapıyı açıp girsin.
            // Dışarıda zemin ve ev sınırı yok: oyuncu fizikten çıkmış, yol boyunca (FollowPath) yürür.
            var start = door != null ? door.Threshold : new Vector3(31.9f, 0f, 1f);
            float outerWall = door != null ? door.transform.position.x + wallThickness : start.x + 0.7f;
            var outside = new Vector3(outerWall + outsideStartDistance, start.y, start.z);
            var doorstep = new Vector3(outerWall + doorstepDistance, start.y, start.z);
            var inside = new Vector3(start.x - insideStopDistance, start.y, start.z);
            player.Teleport(outside);
            player.FollowPath(new[] { doorstep, inside }, enterWalkSpeed);
            player.PathPaused = true;
            player.Face(-1);
            SetShade(outsideShade);
            if (door != null) door.SetOpenImmediate(false);
            var visibility = Deprem.Visual.RoomVisibility.Instance;
            if (visibility != null) visibility.Tick(start + Vector3.left * 1.5f, 1f); // mutfak açılışta görünsün
            foreach (var houseDoor in FindObjectsByType<HouseDoor>(FindObjectsSortMode.None))
                if (houseDoor.transform.position.y < 1f) houseDoor.Open(start.x); // zemin kat: yol açık
            if (father != null)
            {
                father.PlaceAtHome();
                father.Face(1);
            }
            if (mother != null)
            {
                mother.transform.position = motherSpot;
                mother.Face(-1);
            }
            if (sibling != null)
            {
                sibling.transform.position = siblingSpot;
                sibling.Face(-1);
            }

            yield return Fade(1f, 0f, fadeInTime);
            yield return Wait(enterDelay);
            // Karanlıkta kapıya yürür, önünde durup açar, içeri girer; kapı arkasından kapanır
            player.PathPaused = false;
            while (player.IsOnPath && player.transform.position.x > doorstep.x + 0.02f)
            {
                ShadeOutside(outerWall, start.x);
                yield return null;
            }
            player.PathPaused = true;
            yield return Wait(doorstepPause);
            if (door != null)
            {
                Audio.Sfx.PlayAt("cabin.door", door.transform.position, 0.4f);
                yield return door.Swing(true);
            }
            player.PathPaused = false;
            while (player.IsOnPath)
            {
                ShadeOutside(outerWall, start.x);
                yield return null;
            }
            SetShade(1f);
            if (door != null) StartCoroutine(door.Swing(false));

            // Salon: baba
            yield return WalkTo(fatherTalkX);
            if (father != null)
            {
                yield return Say(father, "bubble.intro.father_1", 2.8f);
                yield return Say(player, "bubble.intro.player_1", 3.2f);
                yield return Say(father, "bubble.intro.father_2", 3.2f);
            }

            // Merdiven: üst kattan anne ve kardeş seslenir; kamera iki katı birden gösterir
            yield return WalkTo(stairsTalkX);
            // Berke'nin play testi: kamera anneye doğru uzaklaşsın (balonu sinema bandının altında kalmasın), oyuncu
            // seslenen annesine dönsün
            var view = new GameObject("IntroStairsView").transform;
            view.position = new Vector3(Mathf.Lerp(stairsTalkX, motherSpot.x, 0.6f), motherSpot.y * stairsViewHeight, start.z);
            PlayerCameraTarget.Focus(view, stairsViewFov);
            // Sahanlık örtüsü kalksın: anne ve kardeş merdiven başında görünsün
            if (visibility != null) visibility.Tick(motherSpot, 1f);
            yield return Wait(0.9f);
            if (player != null) player.Face(motherSpot.x > player.transform.position.x ? 1 : -1);
            if (mother != null) yield return Say(mother, "bubble.intro.mother", 3f);
            if (sibling != null) yield return Say(sibling, "bubble.intro.sibling", 2.6f);
            yield return Say(player, "bubble.intro.player_2", 3.8f);
            PlayerCameraTarget.Release();
            Destroy(view.gameObject);

            // Odası: yatağın yanına kadar yürür, yatar
            yield return WalkTo(bedsideX);
            yield return Say(player, "bubble.intro.player_3", 2.2f);
        }

        // ------------------------------------------------------------------ Adımlar ("Geç" her adımı keser)

        /// <summary>İç içe adımları tek tek yürütür; "Geç" basılınca bir sonraki karede durur</summary>
        private IEnumerator Run(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0 && !skipped)
            {
                var top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return top.Current;
            }
        }

        private IEnumerator WalkTo(float x)
        {
            if (player == null) yield break;
            float dir = Mathf.Sign(x - player.transform.position.x);
            player.SetScriptedInput(dir);
            for (float t = 0f; t < 25f; t += Time.deltaTime)
            {
                float left = (x - player.transform.position.x) * dir;
                if (left <= 0.05f) break;
                // Varışa yakın yavaşla
                player.SetScriptedInput(dir * Mathf.Clamp(left / 0.6f, 0.35f, 1f));
                yield return null;
            }
            player.SetScriptedInput(0f);
        }

        private IEnumerator Say(Component speaker, string key, float seconds)
        {
            var bubble = speaker != null ? speaker.GetComponent<SpeechBubble>() : null;
            if (bubble != null) bubble.Say(key, seconds);
            yield return Wait(seconds + 0.2f);
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>Dışarıda koyu; kapıdan geçerken (dış duvar → eşik) mutfağın ışığına açılır</summary>
        private void ShadeOutside(float outerWallX, float thresholdX)
        {
            if (player == null) return;
            float k = Mathf.InverseLerp(outerWallX + 0.4f, thresholdX, player.transform.position.x);
            SetShade(Mathf.Lerp(outsideShade, 1f, k));
        }

        private void SetShade(float value)
        {
            var visual = player != null ? player.GetComponentInChildren<PlayerVisual>() : null;
            if (visual != null) visual.Shade = value;
        }

        // ------------------------------------------------------------------ Karartma

        private IEnumerator Fade(float from, float to, float seconds)
        {
            EnsureFade();
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetFade(to);
        }

        private void SetFade(float alpha)
        {
            fade.color = new Color(0f, 0f, 0f, alpha);
            fade.enabled = alpha > 0.001f;
        }

        private void EnsureFade()
        {
            if (fade != null) return;
            var canvas = UiBuilder.CreateCanvas(transform, "IntroFade", 98);
            fade = UiBuilder.CreateImage(canvas.transform, "Black", Color.black);
            fade.raycastTarget = false;
            UiBuilder.Stretch(fade.rectTransform, Vector2.zero, Vector2.one);
        }
    }
}
