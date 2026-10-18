using System;
using System.Collections.Generic;
using Deprem.Core;
using Deprem.Player;
using UnityEngine;

namespace Deprem.Visual
{
    /// <summary>
    /// Oda örtüsü: oyuncunun görmediği odalar evin önünde siyah bir örtüyle kapalıdır.
    /// Bir oda, bakan (beden; ruh fazında "Spirit") içine girince ya da yanındaki açık kapıdan bakınca açılır
    /// ve deneme boyunca açık kalır. Merdiven koridorla sahanlığı birbirine açar.
    /// Örtüler editörde gizli (sahne düzenlenebilsin), oyun başlayınca kapanır. HouseBuilder kurar.
    /// </summary>
    public sealed class RoomVisibility : MonoBehaviour
    {
        [Serializable]
        public struct Area
        {
            public string key;
            public float x0;
            public float x1;
            public float floorY;
            public float ceilingY;
            public Renderer cover;
        }

        /// <summary>
        /// İki oda arasında kapısız bağlantı (merdiven): bakan bu X aralığında ve minY'nin üstündeyse
        /// (basamaklarda ya da üst katta) iki oda da açılır
        /// </summary>
        [Serializable]
        public struct Link
        {
            public string a;
            public string b;
            public float x0;
            public float x1;
            public float minY;
        }

        private const string SpiritObjectName = "Spirit"; // Kararlar B2: ruh objesinin adı sabit

        [SerializeField] private Area[] areas;
        [SerializeField] private Link[] links;
        [SerializeField] private HouseDoor[] doors;
        [SerializeField] private float fadeTime = 0.4f;
        [Tooltip("Bakanın odasını bulurken tavan / zemin payı (m)")]
        [SerializeField] private float verticalMargin = 0.3f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        // Bedenin gördüğü odalar kalıcı; ruhun gördükleri sadece ruh fazında açık (Play testi 9: ruh fazında
        // gezilen oda kurtarmada kapısı kapalıyken de görünüyordu, kapalı kapının arkası siyah kalmalı)
        private bool[] revealed;
        private bool[] spiritSeen;
        private bool spiritMode;
        private float[] alpha;
        private MaterialPropertyBlock block;
        private PlayerController player;
        private Transform spirit;

        public void Configure(Area[] roomAreas, Link[] roomLinks, HouseDoor[] houseDoors)
        {
            areas = roomAreas;
            links = roomLinks;
            doors = houseDoors;
        }

        /// <summary>Sahnedeki örtü (yoksa null): başka sistemler bir noktanın görünüp görünmediğini sorar</summary>
        public static RoomVisibility Instance { get; private set; }

        /// <summary>Bu nokta açık (örtüsüz) bir odada mı; odası bulunamazsa true</summary>
        public bool IsVisibleAt(Vector3 position)
        {
            if (revealed == null) return true;
            int i = AreaAt(position);
            return i < 0 || Visible(i);
        }

        /// <summary>Oda şu an açık mı (beden gördüyse; ruh fazında ruh gördüyse de)</summary>
        public bool IsRevealed(string key)
        {
            int i = IndexOf(key);
            return i >= 0 && revealed != null && Visible(i);
        }

        private bool Visible(int i) => revealed[i] || (spiritMode && spiritSeen[i]);

        /// <summary>Odaların kesitteki dikdörtgenleri (Berke'nin sol üstteki haritası çizer; sadece okunur)</summary>
        public IReadOnlyList<Area> Areas => areas ?? System.Array.Empty<Area>();

        /// <summary>Oda örtüleri (Berke'nin ana menüsü arka plan görüntüsünü çekerken bunları bir anlığına gizler)</summary>
        public IEnumerable<Renderer> Covers
        {
            get
            {
                if (areas == null) yield break;
                foreach (var area in areas)
                    if (area.cover != null) yield return area.cover;
            }
        }

        /// <summary>Noktanın odasının anahtarı (HouseLayout: "kardes", "ebeveyn"...); oda bulunamazsa null</summary>

        public string RoomKeyAt(Vector3 position)
        {
            if (areas == null) return null;
            int i = AreaAt(position);
            return i < 0 ? null : areas[i].key;
        }

        /// <summary>İki nokta aynı odada mı (Play testi 26: oyuncu kardeşin odasına girdi mi); oda bulunamazsa false</summary>
        public bool SameRoom(Vector3 a, Vector3 b)
        {
            if (areas == null) return false;
            int i = AreaAt(a);
            return i >= 0 && i == AreaAt(b);
        }

        /// <summary>Ruh fazı açık mı: ruhun açtığı odalar ayrı tutulur, faz bitince yeniden örtülür</summary>
        public void SetSpiritMode(bool on)
        {
            if (spiritMode == on) return;
            spiritMode = on;
            if (!on) System.Array.Clear(spiritSeen, 0, spiritSeen.Length);
        }

        private void Awake()
        {
            Instance = this;
            Initialize();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Örtüleri kapatır, hiçbir oda açık değil. Awake'te çağrılır; testlerde elle çağrılabilir.</summary>
        public void Initialize()
        {
            revealed = new bool[areas.Length];
            spiritSeen = new bool[areas.Length];
            spiritMode = false;
            alpha = new float[areas.Length];
            for (int i = 0; i < areas.Length; i++)
            {
                alpha[i] = 1f;
                if (areas[i].cover != null) areas[i].cover.enabled = true;
                ApplyAlpha(i);
            }
        }

        private void Update()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            Transform viewer = player != null ? player.transform : null;
            SetSpiritMode(GameState.Phase == GamePhase.Spirit);
            if (spiritMode)
            {
                if (spirit == null)
                {
                    var go = GameObject.Find(SpiritObjectName);
                    if (go != null) spirit = go.transform;
                }
                if (spirit != null) viewer = spirit;
            }
            if (viewer != null) Tick(viewer.position, Time.deltaTime);
        }

        /// <summary>Bakanın konumuna göre odaları açar ve örtüleri soldurur. Testlerde elle çağrılabilir.</summary>
        public void Tick(Vector3 viewer, float dt)
        {
            int current = AreaAt(viewer);
            if (current >= 0)
            {
                Reveal(current);
                string key = areas[current].key;
                foreach (var door in doors)
                {
                    if (door == null || !door.IsOpen) continue;
                    if (door.LeftRoom == key) Reveal(IndexOf(door.RightRoom));
                    else if (door.RightRoom == key) Reveal(IndexOf(door.LeftRoom));
                }
            }
            foreach (var link in links)
            {
                if (viewer.x < link.x0 || viewer.x > link.x1 || viewer.y < link.minY) continue;
                int a = IndexOf(link.a), b = IndexOf(link.b);
                if (current == a || current == b)
                {
                    Reveal(a);
                    Reveal(b);
                }
            }

            // Açık odanın örtüsü solar, ruh fazı bitince ruhun açtığı oda yeniden kararır
            for (int i = 0; i < areas.Length; i++)
            {
                float target = Visible(i) ? 0f : 1f;
                if (Mathf.Approximately(alpha[i], target)) continue;
                alpha[i] = fadeTime > 0f ? Mathf.MoveTowards(alpha[i], target, dt / fadeTime) : target;
                ApplyAlpha(i);
            }
        }

        private void Reveal(int index)
        {
            if (index < 0) return;
            if (spiritMode) spiritSeen[index] = true;
            else revealed[index] = true;
        }

        private int AreaAt(Vector3 p)
        {
            // Üst kat önce: merdivenin başında iki katın payı çakışır
            int best = -1;
            for (int i = 0; i < areas.Length; i++)
            {
                var a = areas[i];
                if (p.x < a.x0 || p.x >= a.x1) continue;
                if (p.y < a.floorY - verticalMargin || p.y > a.ceilingY + verticalMargin) continue;
                if (best < 0 || a.floorY > areas[best].floorY) best = i;
            }
            return best;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < areas.Length; i++)
                if (areas[i].key == key) return i;
            return -1;
        }

        private void ApplyAlpha(int i)
        {
            var cover = areas[i].cover;
            if (cover == null) return;
            if (alpha[i] <= 0f)
            {
                cover.enabled = false;
                return;
            }
            cover.enabled = true; // yeniden kararan oda (ruh fazı bitti)
            block ??= new MaterialPropertyBlock();
            cover.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(0f, 0f, 0f, alpha[i]));
            cover.SetPropertyBlock(block);
        }
    }
}
