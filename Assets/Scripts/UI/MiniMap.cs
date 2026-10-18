using System.Collections.Generic;
using Deprem.Bag;
using Deprem.Core;
using Deprem.Family;
using Deprem.Quake;
using Deprem.Spirit;
using Deprem.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace Deprem.UI
{
    // Sol üstte CS 1.6 radarı gibi ama 2D: evin kesiti küçük karelerden (her kare cellSize m), oyuncu ve o fazın
    // hedefleri renkli noktalar (jüri temposu: oyuncu nereye gideceğini aramasın).
    //   Ana deprem: güvenli masalar yeşil.
    //   Ruh fazı: önemli (kırmızı konturlu) eşyalar kırmızı, el feneri ve ilk yardım turuncu, çanta sarı; beden soluk.
    //   Kurtarma: aile üyeleri oklarla aynı renkte (saklanan kardeş görünmez), kabin beyaz, alınmamış malzemeler.
    // Odalar Mehmet'in RoomVisibility'sinden; o yoksa (test sahnesi) harita gizli. Oyuncunun odası açık renkli.
    public class MiniMap : MonoBehaviour
    {
        [SerializeField] int sortingOrder = 54;
        [Tooltip("Bir karenin dünyadaki boyu (m)")]
        [SerializeField] float cellSize = 0.5f;
        [Tooltip("Bir karenin referans pikseli (son pikseli karelerin arasındaki boşluk)")]
        [SerializeField] int cellPixels = 3;
        [SerializeField] float padding = 6f;

        [Header("Renkler")]
        [SerializeField] Color roomColor = new Color(0.24f, 0.27f, 0.35f);
        [SerializeField] Color wallColor = new Color(0.46f, 0.49f, 0.58f);
        [SerializeField] Color currentRoomColor = new Color(0.46f, 0.54f, 0.68f);
        [SerializeField] Color playerColor = new Color(0.96f, 0.92f, 0.84f);
        [SerializeField] Color spiritColor = new Color(0.6f, 0.86f, 1f);
        [SerializeField] Color importantColor = new Color(1f, 0.34f, 0.28f);
        [SerializeField] Color supplyColor = new Color(1f, 0.58f, 0.2f);
        [SerializeField] Color bagColor = new Color(1f, 0.86f, 0.36f);
        [SerializeField] Color safeColor = new Color(0.55f, 0.92f, 0.55f);
        [SerializeField] Color cabinColor = Color.white;
        [SerializeField] Color fatherColor = new Color(1f, 0.82f, 0.4f);
        [SerializeField] Color motherColor = new Color(1f, 0.6f, 0.52f);
        [SerializeField] Color siblingColor = new Color(0.58f, 0.88f, 0.6f);
        [SerializeField] float pulseSpeed = 5f;

        const string CabinMarker = "Marker_CabinEntrance";
        const string SpiritObjectName = "Spirit";

        /// <summary>Haritanın sol üstte kapladığı yükseklik ve en (referans pikseli; gizliyken 0): ObjectiveHud paneli
        /// ve okları bunun altında kalır</summary>
        public static float ReservedHeight { get; private set; }
        public static float ReservedWidth { get; private set; }

        CanvasGroup group;
        RectTransform panel;
        RawImage grid;
        Texture2D texture;
        readonly List<Image> dots = new List<Image>();
        int usedDots;

        RoomVisibility rooms;
        readonly List<RoomVisibility.Area> areas = new List<RoomVisibility.Area>();
        Vector2 worldMin;
        int cellsX, cellsY;
        string shownRoom = "-";

        Transform player;
        Transform spirit;
        Transform cabin;
        BagSupply[] supplies = new BagSupply[0];
        float supplyRefresh;

        void Awake()
        {
            var canvas = UiBuilder.CreateCanvas(transform, "MiniMapCanvas", sortingOrder);
            group = canvas.GetComponent<CanvasGroup>();
            float margin = UiBuilder.HudMargin;
            panel = UiBuilder.CreatePanel(canvas.transform, "MiniMap").rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(margin, -margin);
            grid = UiBuilder.CreateChild<RawImage>(panel, "Grid");
            grid.raycastTarget = false;
            var rect = grid.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.one * padding;
            group.alpha = 0f;
        }

        void OnDestroy()
        {
            if (texture) Destroy(texture);
            ReservedHeight = ReservedWidth = 0f;
        }

        void LateUpdate() => Refresh(GameState.Phase);

        void Refresh(GamePhase phase)
        {
            bool phaseShown = phase == GamePhase.Foreshock || phase == GamePhase.MainQuake || phase == GamePhase.Spirit ||
                              phase == GamePhase.Rescue;
            bool visible = phaseShown && EnsureLayout();
            group.alpha = visible ? 1f : 0f;
            ReservedHeight = visible ? panel.sizeDelta.y + 8f : 0f;
            ReservedWidth = visible ? panel.sizeDelta.x : 0f;
            if (!visible) return;

            FindActors();
            Transform viewer = phase == GamePhase.Spirit && spirit ? spirit : player;
            string room = viewer && rooms ? rooms.RoomKeyAt(viewer.position + Vector3.up * 0.8f) : null;
            if (room != shownRoom)
            {
                shownRoom = room;
                DrawRooms();
            }

            usedDots = 0;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
            switch (phase)
            {
                case GamePhase.MainQuake:
                    foreach (var spot in SafeSpot.All)
                        if (spot) Dot(spot.transform.position + Vector3.up * 0.4f, Fade(safeColor, pulse), 4f);
                    break;
                case GamePhase.Spirit:
                    foreach (var item in SecurableItem.All)
                        if (item && !item.IsSecured && SpiritHighlighter.IsImportant(item.Id))
                            Dot(CenterOf(item.gameObject), Fade(importantColor, pulse), 4f);
                    Supplies(pulse);
                    var bag = BagDropPoint.Current;
                    bool carrying = BagSystem.Carrying > BagItemId.None;
                    if (bag) Dot(bag.transform.position + Vector3.up * 0.3f, carrying ? Fade(bagColor, pulse) : bagColor,
                        carrying ? 6f : 4f);
                    if (player) Dot(player.position + Vector3.up * 0.4f, Fade(playerColor, 0.45f), 4f); // yataktaki beden
                    break;
                case GamePhase.Rescue:
                    foreach (var member in FamilyMember.All)
                        if (member && !member.Hidden && member.State != FamilyState.InCabin && !GameState.IsRescued(member.Id))
                            Dot(member.transform.position + Vector3.up * 0.8f, Fade(ColorFor(member.Id), pulse), 4f);
                    if (cabin) Dot(cabin.position + Vector3.up * 0.8f, cabinColor, 4f);
                    Supplies(pulse);
                    break;
            }

            // Oyuncu en son: noktaların üstünde, koyu konturlu
            if (viewer) Dot(viewer.position + Vector3.up * 0.8f, viewer == spirit ? spiritColor : playerColor, 5f, true);
            for (int i = usedDots; i < dots.Count; i++)
                if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
        }

        // Odalar bir kez okunur; doku evin kesitinin boyunda
        bool EnsureLayout()
        {
            if (texture) return rooms;
            rooms = RoomVisibility.Instance ? RoomVisibility.Instance : FindFirstObjectByType<RoomVisibility>();
            if (!rooms || rooms.Areas.Count == 0) return false;
            areas.Clear();
            areas.AddRange(rooms.Areas);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var area in areas)
            {
                min = Vector2.Min(min, new Vector2(area.x0, area.floorY));
                max = Vector2.Max(max, new Vector2(area.x1, area.ceilingY));
            }
            worldMin = min;
            cellsX = Mathf.CeilToInt((max.x - min.x) / cellSize - 0.01f);
            cellsY = Mathf.CeilToInt((max.y - min.y) / cellSize - 0.01f);
            texture = new Texture2D(cellsX * cellPixels, cellsY * cellPixels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MiniMap",
            };
            grid.texture = texture;
            grid.rectTransform.sizeDelta = new Vector2(texture.width, texture.height);
            panel.sizeDelta = new Vector2(texture.width, texture.height) + Vector2.one * padding * 2f;
            shownRoom = "-";
            return true;
        }

        // Her oda kareleriyle: kenar kareleri duvar, içi oda rengi (oyuncunun odası açık)
        void DrawRooms()
        {
            var pixels = new Color32[texture.width * texture.height];
            int fill = Mathf.Max(1, cellPixels - 1);
            foreach (var area in areas)
            {
                int cx0 = Mathf.FloorToInt((area.x0 - worldMin.x) / cellSize + 0.01f);
                int cx1 = Mathf.CeilToInt((area.x1 - worldMin.x) / cellSize - 0.01f) - 1;
                int cy0 = Mathf.FloorToInt((area.floorY - worldMin.y) / cellSize + 0.01f);
                int cy1 = Mathf.CeilToInt((area.ceilingY - worldMin.y) / cellSize - 0.01f) - 1;
                bool current = area.key == shownRoom;
                for (int cy = Mathf.Max(0, cy0); cy <= Mathf.Min(cellsY - 1, cy1); cy++)
                for (int cx = Mathf.Max(0, cx0); cx <= Mathf.Min(cellsX - 1, cx1); cx++)
                {
                    bool edge = cx == cx0 || cx == cx1 || cy == cy0 || cy == cy1;
                    Color32 color = edge ? wallColor : current ? currentRoomColor : roomColor;
                    for (int py = 0; py < fill; py++)
                    for (int px = 0; px < fill; px++)
                        pixels[(cy * cellPixels + py) * texture.width + cx * cellPixels + px] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        void FindActors()
        {
            if (!player)
            {
                var go = GameObject.Find("Player");
                if (go) player = go.transform;
            }
            if (!spirit && GameState.Phase == GamePhase.Spirit)
            {
                var go = GameObject.Find(SpiritObjectName);
                if (go) spirit = go.transform;
            }
            if (!cabin)
            {
                var go = GameObject.Find(CabinMarker);
                if (go) cabin = go.transform;
            }
            if (Time.unscaledTime >= supplyRefresh)
            {
                supplyRefresh = Time.unscaledTime + 1f;
                supplies = FindObjectsByType<BagSupply>(FindObjectsSortMode.None);
            }
        }

        // Evde duran (taşınmayan, çantada olmayan) el feneri ve ilk yardım
        void Supplies(float pulse)
        {
            foreach (var supply in supplies)
            {
                if (!supply || GameState.HasBagItem(supply.Id) || BagSystem.Carrying == supply.Id ||
                    !BagLessonGate.IsAvailable(supply.Id))
                    continue;
                Dot(supply.transform.position, Fade(supplyColor, pulse), 4f);
            }
        }

        static Vector3 CenterOf(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            return collider ? collider.bounds.center : go.transform.position;
        }

        Color ColorFor(FamilyMemberId id) =>
            id == FamilyMemberId.Father ? fatherColor : id == FamilyMemberId.Mother ? motherColor : siblingColor;

        static Color Fade(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        // Nokta karenin ortasına oturur (pixel ızgarası kaymasın)
        void Dot(Vector3 world, Color color, float size, bool outline = false)
        {
            Image dot;
            if (usedDots < dots.Count) dot = dots[usedDots];
            else
            {
                dot = UiBuilder.CreateImage(grid.rectTransform, "Dot", Color.white);
                var rect = dot.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                var shadow = dot.gameObject.AddComponent<Outline>();
                shadow.effectColor = new Color(0.05f, 0.04f, 0.04f, 0.9f);
                shadow.effectDistance = Vector2.one;
                dots.Add(dot);
            }
            usedDots++;
            if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            dot.color = color;
            dot.GetComponent<Outline>().enabled = outline;
            int cx = Mathf.Clamp(Mathf.FloorToInt((world.x - worldMin.x) / cellSize), 0, cellsX - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt((world.y - worldMin.y) / cellSize), 0, cellsY - 1);
            float half = (cellPixels - 1) * 0.5f;
            dot.rectTransform.sizeDelta = Vector2.one * size;
            dot.rectTransform.anchoredPosition = new Vector2(cx * cellPixels + half, cy * cellPixels + half);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ReservedHeight = ReservedWidth = 0f;
    }
}
