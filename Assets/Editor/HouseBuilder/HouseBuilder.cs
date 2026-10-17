using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Evi ve 9 sabitlenebilir eşyayı HouseLayout'taki ölçülerden kodla kurar (GDD "Ev ve seviye tasarımı").
    ///
    /// Sanatçı dokuları bulunursa otomatik takılır, yoksa gri kutu renkleri kullanılır:
    ///   Odalar : Assets/Art/Rooms/{oda}_arka.png, _yan.png, _zemin.png, _tavan.png
    ///            oda = oyuncu, koridor, salon, mutfak, kardes, sahanlik, ebeveyn
    ///   Eşyalar: Assets/Art/Items/{esya}_on.png, _yan.png, _ust.png (kabin dahil: kabin_on ...)
    ///   Mobilya: Assets/Art/Decor/{mobilya}_on.png, _yan.png, _ust.png (şekiller: FurnitureShapes)
    ///   Ortak  : Assets/Art/Tiles/kesit.png, Assets/Art/Tiles/merdiven_basamak.png
    ///   Pencere: Assets/Art/Decor/pencere.png, pencere_emission.png
    /// Doku ekledikten sonra Tools > Deprem > Evi Yeniden Kur yeterli.
    /// </summary>
    internal static class HouseBuilder
    {
        public const string RootName = "House";
        public const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string MaterialFolder = "Assets/Generated/House/Materials";

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        [MenuItem("Tools/Deprem/Evi Yeniden Kur (açık sahne)")]
        private static void RebuildInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            var root = Build(scene);
            Undo.RegisterCreatedObjectUndo(root, "Evi Kur");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("[HouseBuilder] Ev kuruldu: " + scene.name);
        }

        [MenuItem("Tools/Deprem/Main Sahnesini Aç veya Oluştur")]
        private static void OpenOrCreateMain()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OpenOrCreateMainScene();
        }

        /// <summary>Batchmode: Main.unity'yi kurar/günceller, kaydeder ve Logs altına önizleme çizer
        /// (Temp, Unity kapanırken silinir).</summary>
        public static void BuildMainSceneBatch()
        {
            var scene = OpenOrCreateMainScene();
            Deprem.Player.PlayerSetup.SetupInScene(scene);
            // Döngüyü Berke'nin LoopManager'ı (BerkeSystems prefab'ı) başlatıyor; geliştirme başlatıcısı kapalı
            Deprem.Quake.QuakeSetup.SetupInScene(scene, 0);
            Deprem.Family.FamilySetup.SetupInScene(scene);
            Deprem.Audio.AudioSetup.SetupInScene(scene);
            var volumes = Deprem.Visual.VolumeSetup.SetupInScene(scene);
            BagItemModels.Generate(); // Berke'nin BagSupply'ı prefab'ları kullanır (ENTEGRASYON, Play testi 10)
            BerkeSystemsSetup.SetupInScene(scene);
            EditorSceneManager.SaveScene(scene);
            var whole = new Vector3(16.25f, 3.6f, -39f);
            RenderBagItemsPreview();
            HousePreview.Render("Logs/house_preview.png", null, whole, 2048, 1024);
            HousePreview.Render("Logs/house_preview_flat.png", 0.7f, whole, 2048, 1024);
            var gameCamera = Camera.main;
            if (gameCamera != null)
                HousePreview.Render("Logs/house_preview_game.png", null, gameCamera.transform.position, 1920, 1080);
            var salon = HouseLayout.Room("salon");
            var salonCamera = new Vector3((salon.X0 + salon.X1) * 0.5f, 1.9f, -12f);
            HousePreview.Render("Logs/house_preview_salon.png", null, salonCamera, 1920, 1080);

            // Elektrik kesik hali (ana depremden sonra): ışıklar ve Volume
            var houseLights = Object.FindFirstObjectByType<Deprem.Quake.HouseLights>();
            if (houseLights != null)
            {
                houseLights.SetPower(false);
                volumes.SetImmediate(volumes.PowerOut);
                HousePreview.Render("Logs/house_preview_salon_dark.png", null, salonCamera, 1920, 1080);
                volumes.SetImmediate(volumes.Night);
                houseLights.SetPower(true);
            }

            // Ruh fazı: Volume ve sabitlenmemiş eşyalarda kontur
            var outlines = Object.FindObjectsByType<Deprem.Visual.ItemOutline>(FindObjectsSortMode.None);
            foreach (var outline in outlines) outline.SetVisible(true);
            volumes.SetImmediate(volumes.Spirit);
            HousePreview.Render("Logs/house_preview_spirit.png", null, salonCamera, 1920, 1080);
            volumes.SetImmediate(volumes.Night);
            foreach (var outline in outlines) outline.SetVisible(false);

            // Oyun başındaki oda örtüsü: sadece oyuncunun odası açık, koridor kapısı açık (sahne kaydedildikten sonra)
            var visibility = Object.FindFirstObjectByType<Deprem.Visual.RoomVisibility>();
            if (visibility != null)
            {
                foreach (var door in Object.FindObjectsByType<Deprem.Player.HouseDoor>(FindObjectsSortMode.None))
                    if (door.LeftRoom == "oyuncu")
                    {
                        door.Open(0f);
                        var hinge = door.transform.Find("Hinge");
                        if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    }
                visibility.Initialize();
                visibility.Tick(new Vector3(5.5f, 0f, 1f), 1f);
                HousePreview.Render("Logs/house_preview_covers.png", null, whole, 2048, 1024);

                // Kurtarma fazı, üst kat (ENTEGRASYON B10): elektrik kesik, merdivenden çıkılmış, üst kat kapıları açık
                if (houseLights != null)
                {
                    houseLights.SetPower(false);
                    volumes.SetImmediate(volumes.PowerOut);
                    foreach (var door in Object.FindObjectsByType<Deprem.Player.HouseDoor>(FindObjectsSortMode.None))
                        if (door.LeftRoom == "kardes" || door.RightRoom == "ebeveyn") door.Open(door.transform.position.x + 1f);
                    visibility.Tick(new Vector3(12f, 1.5f, 2.6f), 1f);   // merdivende
                    visibility.Tick(new Vector3(14.6f, HouseDims.UpperFloorY, 1f), 1f); // merdiven başı
                    visibility.Tick(new Vector3(8f, HouseDims.UpperFloorY, 1f), 1f);    // kardeşin kapısında
                    visibility.Tick(new Vector3(17.5f, HouseDims.UpperFloorY, 1f), 1f); // ebeveyn kapısında
                    var upper = new Vector3(13.5f, HouseDims.UpperFloorY + 1.7f, -26f);
                    HousePreview.Render("Logs/house_preview_rescue_upper.png", null, upper, 1920, 1080);
                    Debug.Log("[HouseBuilder] B10 açık odalar: " + string.Join(", ",
                        HouseLayout.Rooms.Where(r => visibility.IsRevealed(r.Key)).Select(r => r.Key)));
                    volumes.SetImmediate(volumes.Night);
                    houseLights.SetPower(true);
                }
            }

            ArtTemplates.Generate();
            Debug.Log("[HouseBuilder] Batch tamam");
        }

        /// <summary>Çanta malzemesi modellerini salonun önünde yan yana, konturları açık çizer (sahne kaydedildikten sonra)</summary>
        private static void RenderBagItemsPreview()
        {
            var placed = new List<GameObject>();
            var ids = new[] { Deprem.Core.BagItemId.FirstAidKit, Deprem.Core.BagItemId.Flashlight, Deprem.Core.BagItemId.Water, Deprem.Core.BagItemId.Whistle };
            for (int i = 0; i < ids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagItemModels.PrefabPath(ids[i]));
                if (prefab == null) continue;
                var go = (GameObject)Object.Instantiate(prefab, new Vector3(20.2f + i * 0.75f, 0f, 0.6f), Quaternion.identity);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                placed.Add(go);
            }
            var bagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BagItemModels.EarthquakeBagPath);
            if (bagPrefab != null)
            {
                var bag = (GameObject)Object.Instantiate(bagPrefab, new Vector3(20.2f + ids.Length * 0.75f, 0f, 0.6f), Quaternion.identity);
                foreach (var r in bag.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                placed.Add(bag);
            }
            HousePreview.Render("Logs/bag_items_preview.png", null, new Vector3(21.7f, 0.6f, -3.6f), 1280, 720);
            foreach (var go in placed) Object.DestroyImmediate(go);
        }

        private static Scene OpenOrCreateMainScene()
        {
            Scene scene;
            if (File.Exists(MainScenePath))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateCamera();
                EditorSceneManager.SaveScene(scene, MainScenePath);
                AddToBuildSettings(MainScenePath);
            }

            Build(scene);
            ApplyNightLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return scene;
        }

        public static GameObject Build(Scene scene)
        {
            foreach (var existing in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList())
                Object.DestroyImmediate(existing);

            MaterialCache.Clear();
            EnsureFolder(MaterialFolder);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Deprem.Player.HouseBounds>().Configure(FloorBounds());

            var rooms = Child(root.transform, "Rooms");
            foreach (var room in HouseLayout.Rooms)
                BuildRoom(room, Child(rooms, "Room_" + room.Key));

            var stairs = Child(root.transform, "Stairs");
            BuildStairs(stairs);
            stairs.gameObject.AddComponent<Deprem.Player.StairPath>().Configure(StairRoute());
            BuildCabin(root.transform);
            BuildWindow(root.transform);
            BuildFrontBarrier(Child(root.transform, "FrontBarrier"));

            var items = Child(root.transform, "Items");
            foreach (var item in HouseLayout.Items)
                BuildItem(item, items);
            var decor = Child(root.transform, "Decor");
            foreach (var piece in HouseLayout.Decor)
                BuildDecor(piece, decor);

            var doors = BuildDoors(Child(root.transform, "Doors"));
            var covers = BuildRoomCovers(Child(root.transform, "RoomCovers"));
            var stairLink = new Deprem.Visual.RoomVisibility.Link
            {
                a = "koridor", b = "sahanlik", x0 = HouseDims.StairX0, x1 = HouseDims.StairHoleX1 + 0.4f, minY = 0.3f,
            };
            root.AddComponent<Deprem.Visual.RoomVisibility>().Configure(covers, new[] { stairLink }, doors);

            BuildLights(Child(root.transform, "Lights"));
            BuildMarkers(Child(root.transform, "Markers"));
            BuildSafeSpots(Child(root.transform, "SafeSpots"), decor);

            AssetDatabase.SaveAssets();
            return root;
        }

        // ---------------------------------------------------------------- Odalar

        private static void BuildRoom(RoomDef room, Transform parent)
        {
            const float t = HouseDims.Thickness;
            const float half = t * 0.5f;
            float x0 = room.X0, x1 = room.X1, d = room.Depth;
            float y0 = room.FloorY, ceil = room.CeilingY;

            var floorMat = RoomMaterial(room, "zemin", 0.32f);
            var ceilingMat = RoomMaterial(room, "tavan", 0.5f);
            var backMat = RoomMaterial(room, "arka", 0.72f);
            var sideMat = RoomMaterial(room, "yan", 0.6f);

            // Zemin: zemin katta tam kalınlık, üst katta döşemenin üst yarısı
            float floorBottom = room.Floor == 0 ? -t : y0 - half;
            var floorMap = Planar(0, x0, room.Width, 2, 0f, d);
            bool floorHole = room.Key == "sahanlik";
            BuildSlab(parent, "Floor", new Vector3(x0, floorBottom, 0f), new Vector3(x1, y0, d), floorHole, floorMap, floorMat);

            // Tavan: üstünde oda varsa döşemenin alt yarısı, yoksa (mutfak, üst kat) tam kalınlık
            float ceilingTop = HouseLayout.HasRoomAbove(room) ? ceil + half : ceil + t;
            bool ceilingHole = room.Key == "koridor";
            BuildSlab(parent, "Ceiling", new Vector3(x0, ceil, 0f), new Vector3(x1, ceilingTop, d), ceilingHole, floorMap, ceilingMat);

            // Arka duvar: döşeme ve tavan kalınlığını da kapatır, doku iç yüksekliğe göre hizalanır
            var backMap = Planar(0, x0, room.Width, 1, y0, room.Height);
            AddPiece(parent, "BackWall", new Vector3(x0, floorBottom, d), new Vector3(x1, ceilingTop, d + t), backMap, backMat);

            // Yan duvarlar: odalar arası duvarın kendi yarısı, dış duvarlar tam kalınlık. Doku sol kenarı = ön.
            var sideMap = Planar(2, 0f, d, 1, y0, room.Height);
            bool leftNeighbor = HouseLayout.HasNeighbor(room, false);
            bool rightNeighbor = HouseLayout.HasNeighbor(room, true);
            BuildSideWall(parent, "LeftWall", leftNeighbor ? x0 : x0 - half, x0 + half, y0, ceil, d, leftNeighbor, sideMap, sideMat);
            BuildSideWall(parent, "RightWall", x1 - half, rightNeighbor ? x1 : x1 + half, y0, ceil, d, rightNeighbor, sideMap, sideMat);
        }

        private static void BuildSlab(Transform parent, string name, Vector3 min, Vector3 max, bool stairHole,
            BoxMeshBuilder.UvFunc uv, Material mat)
        {
            if (!stairHole)
            {
                AddPiece(parent, name, min, max, uv, mat);
                return;
            }

            float hx0 = HouseDims.StairHoleX0, hx1 = HouseDims.StairHoleX1;
            float hz0 = HouseDims.StairZ0, hz1 = Mathf.Min(HouseDims.StairZ1, max.z);
            AddPiece(parent, name + "_L", min, new Vector3(hx0, max.y, max.z), uv, mat);
            AddPiece(parent, name + "_R", new Vector3(hx1, min.y, min.z), max, uv, mat);
            AddPiece(parent, name + "_Front", new Vector3(hx0, min.y, min.z), new Vector3(hx1, max.y, hz0), uv, mat);
            AddPiece(parent, name + "_Back", new Vector3(hx0, min.y, hz1), new Vector3(hx1, max.y, max.z), uv, mat);
        }

        private static void BuildSideWall(Transform parent, string name, float xa, float xb, float y0, float ceil, float d,
            bool door, BoxMeshBuilder.UvFunc uv, Material mat)
        {
            if (!door)
            {
                AddPiece(parent, name, new Vector3(xa, y0, 0f), new Vector3(xb, ceil, d), uv, mat);
                return;
            }

            float z0 = HouseDims.DoorZ0, z1 = HouseDims.DoorZ1, top = y0 + HouseDims.DoorHeight;
            AddPiece(parent, name + "_Front", new Vector3(xa, y0, 0f), new Vector3(xb, ceil, z0), uv, mat);
            AddPiece(parent, name + "_Lintel", new Vector3(xa, top, z0), new Vector3(xb, ceil, z1), uv, mat);
            AddPiece(parent, name + "_Back", new Vector3(xa, y0, z1), new Vector3(xb, ceil, d), uv, mat);
        }

        // ---------------------------------------------------------------- Kapılar ve oda örtüsü

        /// <summary>
        /// Aynı kattaki komşu odalar arasındaki kapı boşluğuna kapı: menteşe boşluğun arka kenarında,
        /// kanat öne uzanır ve karakterin yolunu keser. F ile açılır (Player.HouseDoor).
        /// </summary>
        private static Deprem.Player.HouseDoor[] BuildDoors(Transform parent)
        {
            var doors = new List<Deprem.Player.HouseDoor>();
            var color = new Color(0.55f, 0.4f, 0.28f);
            foreach (var room in HouseLayout.Rooms)
            {
                var right = HouseLayout.RightNeighbor(room);
                if (right == null) continue;

                float b = room.X1;
                var go = new GameObject($"Door_{room.Key}_{right.Key}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(b, room.FloorY, HouseDims.PlayerPlaneZ);

                var hinge = new GameObject("Hinge");
                hinge.transform.SetParent(go.transform, false);
                hinge.transform.position = new Vector3(b, room.FloorY, HouseDims.DoorZ1);

                var size = HouseLayout.DoorLeafSize;
                var min = new Vector3(b - size.x * 0.5f, room.FloorY, HouseDims.DoorZ1 - size.z);
                var leaf = AddShape(hinge.transform, "Leaf", min, min + size, "Decor", "kapi", color, false, true);

                var door = go.AddComponent<Deprem.Player.HouseDoor>();
                // M17: salon kapısının hemen arkasındaki TV ünitesi devrilirse kapı açılmaz
                var jammedBy = room.Key == "koridor" && right.Key == "salon" ? Deprem.Core.ItemId.TvUnit : Deprem.Core.ItemId.None;
                door.Configure(hinge.transform, leaf.GetComponent<Collider>(), room.Key, right.Key, jammedBy);
                doors.Add(door);
            }
            return doors.ToArray();
        }

        /// <summary>
        /// Her odanın önünde siyah örtü (evin önünden biraz önde). Editörde gizli; oyunda RoomVisibility
        /// açar ve oyuncu odayı görünce soldurur.
        /// </summary>
        private static Deprem.Visual.RoomVisibility.Area[] BuildRoomCovers(Transform parent)
        {
            const float t = HouseDims.Thickness;
            const float half = t * 0.5f;
            var mat = CoverMaterial();
            var areas = new List<Deprem.Visual.RoomVisibility.Area>();
            foreach (var room in HouseLayout.Rooms)
            {
                float x0 = room.X0 - (HouseLayout.HasNeighbor(room, false) ? 0f : half);
                float x1 = room.X1 + (HouseLayout.HasNeighbor(room, true) ? 0f : half);
                float y0 = room.FloorY - (room.Floor == 0 ? t : half);
                float y1 = room.CeilingY + (HouseLayout.HasRoomAbove(room) ? half : t);

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Cover_" + room.Key;
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(parent, false);
                quad.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, -0.03f);
                quad.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                areas.Add(new Deprem.Visual.RoomVisibility.Area
                {
                    key = room.Key, x0 = room.X0, x1 = room.X1, floorY = room.FloorY, ceilingY = room.CeilingY, cover = renderer,
                });
            }
            return areas.ToArray();
        }

        /// <summary>URP Unlit, saydam siyah; saydamlık oda başına MaterialPropertyBlock ile</summary>
        private static Material CoverMaterial()
        {
            string path = $"{MaterialFolder}/oda_ortusu.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + 100;
            mat.SetColor("_BaseColor", Color.black);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Merdiven, kabin, pencere

        private static void BuildStairs(Transform parent)
        {
            var mat = GetMaterial("merdiven_basamak", "Assets/Art/Tiles/merdiven_basamak.png", new Color(0.45f, 0.38f, 0.3f));
            float run = HouseDims.StairLength / HouseDims.StairSteps;
            float rise = HouseDims.UpperFloorY / HouseDims.StairSteps;
            for (int i = 0; i < HouseDims.StairSteps; i++)
            {
                float xa = HouseDims.StairX0 + i * run;
                float top = (i + 1) * rise;
                var min = new Vector3(xa, top - rise, HouseDims.StairZ0);
                var max = new Vector3(xa + run, top, HouseDims.StairZ1);
                AddPiece(parent, $"Step_{i:00}", min, max, BoxMeshBuilder.TileUv, mat);
            }
            BuildStairTrim(parent, run, rise);
        }

        /// <summary>
        /// Merdivenin süsü (çarpışmasız). Play testi 4–5: havada duran kutular gibiydi, basamaklı küpeşte de
        /// tırtıklıydı. Kameraya bakan tek parça eğik yan kiriş, eğik küpeşte ve dikmeler, basamak burunları;
        /// kabine kadar basamakların altı kapalı (duvar), kabin yüksek ucun altında açık kalır.
        /// Üst katta merdiven boşluğunun önünde korkuluk.
        /// </summary>
        private static void BuildStairTrim(Transform parent, float run, float rise)
        {
            var beam = GetMaterial("merdiven_kiris", "Assets/Art/Tiles/merdiven_kiris.png", new Color(0.32f, 0.24f, 0.18f));
            var rail = GetMaterial("korkuluk", "Assets/Art/Tiles/korkuluk.png", new Color(0.22f, 0.2f, 0.19f));
            var nose = GetMaterial("merdiven_burun", null, new Color(0.55f, 0.45f, 0.35f));
            var under = GetMaterial("merdiven_alti", "Assets/Art/Tiles/merdiven_alti.png", new Color(0.78f, 0.72f, 0.62f));
            var trim = Child(parent, "Trim");
            float x0 = HouseDims.StairX0;
            float z0 = HouseDims.StairZ0;
            float slope = rise / run;
            float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
            const float railHeight = 0.9f;
            // Basamak burunlarından geçen çizgi: y = rise + (x - x0) * slope
            float Nose(float x) => rise + (x - x0) * slope;

            // Eğik yan kiriş: üst kenarı burun çizgisinin hemen üstünde, 0,3 m kalınlık; alt ucu zeminin içinde
            float xs = x0 - 0.3f, xe = x0 + HouseDims.StairLength;
            float length = (xe - xs) / Mathf.Cos(angle * Mathf.Deg2Rad);
            AddSlopedBox(trim, "Stringer", new Vector3((xs + xe) * 0.5f, Nose((xs + xe) * 0.5f) - 0.12f, z0 - 0.03f),
                new Vector3(length, 0.3f, 0.06f), angle, beam);
            // Eğik küpeşte: burun çizgisinin railHeight üstünde
            AddSlopedBox(trim, "Handrail", new Vector3((x0 + xe) * 0.5f, Nose((x0 + xe) * 0.5f) + railHeight, z0 + 0.05f),
                new Vector3((xe - x0) / Mathf.Cos(angle * Mathf.Deg2Rad), 0.06f, 0.08f), angle, rail);

            for (int i = 0; i < HouseDims.StairSteps; i++)
            {
                float xa = x0 + i * run;
                float top = (i + 1) * rise;
                // Basamak burnu: önde ince, açık renkli kenar
                AddDecorBox(trim, $"Nose_{i:00}", new Vector3(xa - 0.02f, top - 0.04f, z0 - 0.08f),
                    new Vector3(xa + run, top, HouseDims.StairZ1), nose);
                // Dikme her iki basamakta bir: basamaktan küpeşteye
                if (i % 2 == 0)
                {
                    float bx = xa + run * 0.4f;
                    AddDecorBox(trim, $"Baluster_{i:00}", new Vector3(bx, top, z0 + 0.03f),
                        new Vector3(bx + 0.04f, Nose(bx + 0.02f) + railHeight, z0 + 0.07f), rail);
                }
                // Basamağın altı kabine kadar kapalı (kabin yüksek ucun altında)
                if (xa + run <= HouseDims.CabinMin.x + 0.01f && top - rise > 0.01f)
                    AddDecorBox(trim, $"Under_{i:00}", new Vector3(xa, 0f, z0 + 0.02f),
                        new Vector3(xa + run, top - rise, HouseDims.StairZ1), under);
            }

            // Üst kat: merdiven boşluğunun önünde (karakterin arkasında) korkuluk
            float up = HouseDims.UpperFloorY;
            float hx0 = HouseDims.StairHoleX0, hx1 = HouseDims.StairHoleX1;
            float hz = HouseDims.StairZ0 - 0.05f;
            AddDecorBox(trim, "LandingRail", new Vector3(hx0, up + railHeight - 0.03f, hz - 0.04f),
                new Vector3(hx1, up + railHeight + 0.03f, hz + 0.04f), rail);
            for (float x = hx0; x <= hx1 + 0.01f; x += 0.45f)
                AddDecorBox(trim, $"LandingBaluster_{x:F2}", new Vector3(Mathf.Min(x, hx1 - 0.04f), up, hz - 0.02f),
                    new Vector3(Mathf.Min(x, hx1 - 0.04f) + 0.04f, up + railHeight, hz + 0.02f), rail);
        }

        /// <summary>Her katın iç sınırları: dış duvarların iç yüzleri arası</summary>
        private static Deprem.Player.HouseBounds.Floor[] FloorBounds()
        {
            const float half = HouseDims.Thickness * 0.5f;
            return HouseLayout.Rooms.GroupBy(r => r.Floor).OrderBy(g => g.Key).Select(g => new Deprem.Player.HouseBounds.Floor
            {
                minX = g.Min(r => r.X0) + half,
                maxX = g.Max(r => r.X1) - half,
                floorY = g.First().FloorY,
                ceilingY = g.First().CeilingY,
            }).ToArray();
        }

        /// <summary>
        /// Merdiven yolu: karakter düzleminden arka şeride geç, basamak burunları boyunca çık,
        /// sahanlıkta karakter düzlemine dön. İniş aynı yolun tersi.
        /// </summary>
        private static Vector3[] StairRoute()
        {
            float run = HouseDims.StairLength / HouseDims.StairSteps;
            float rise = HouseDims.UpperFloorY / HouseDims.StairSteps;
            float x0 = HouseDims.StairX0;
            float x1 = HouseDims.StairX0 + HouseDims.StairLength;
            float lane = (HouseDims.StairZ0 + HouseDims.StairZ1) * 0.5f;
            float plane = HouseDims.PlayerPlaneZ;
            float up = HouseDims.UpperFloorY;
            return new[]
            {
                new Vector3(x0 - 0.2f, 0f, plane),
                new Vector3(x0 - 0.2f, 0f, lane),
                new Vector3(x0, rise, lane),
                new Vector3(x0 + (HouseDims.StairSteps - 1) * run, up, lane),
                new Vector3(x1 + 0.3f, up, lane),
                new Vector3(x1 + 0.3f, up, plane),
            };
        }

        /// <summary>Evin önünde görünmez duvar: devrilen eşyalar kesit çizgisinden boşluğa düşmesin</summary>
        private static void BuildFrontBarrier(Transform parent)
        {
            foreach (var room in HouseLayout.Rooms)
            {
                var go = new GameObject("Barrier_" + room.Key);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3((room.X0 + room.X1) * 0.5f, (room.FloorY + room.CeilingY) * 0.5f, -0.1f);
                go.isStatic = true;
                go.AddComponent<BoxCollider>().size = new Vector3(room.Width, room.Height, 0.2f);
            }
        }

        private static void BuildCabin(Transform parent)
        {
            var min = HouseDims.CabinMin;
            var max = HouseDims.CabinMax;
            var go = AddShape(parent, "Cabin", min, max, "Items", "kabin", new Color(0.85f, 0.55f, 0.15f), false, true);
            go.isStatic = true;

            // Kurtarma fazında ön yüz parlar (CabinBeacon emisyonu nabızla açar)
            var renderer = go.GetComponent<MeshRenderer>();
            var front = renderer.sharedMaterials[0];
            front.EnableKeyword("_EMISSION");
            front.SetColor("_EmissionColor", Color.black);
            front.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(front);

            // Tabela: kapının üstündeki levhanın önünde (FurnitureShapes "kabin": levha y 1,56-1,84)
            var signGo = new GameObject("Sign");
            signGo.transform.SetParent(go.transform, false);
            signGo.transform.position = new Vector3((min.x + max.x) * 0.5f, min.y + 1.7f, min.z - 0.005f);
            var sign = signGo.AddComponent<TextMesh>();
            sign.font = Deprem.UI.UiBuilder.DefaultFont;
            signGo.GetComponent<MeshRenderer>().sharedMaterial = sign.font.material;
            sign.text = "DEPREM KABİNİ";
            sign.fontSize = 64;
            sign.characterSize = 0.02f;
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.color = new Color(0.1f, 0.1f, 0.1f);

            var beaconGo = new GameObject("Beacon");
            beaconGo.transform.SetParent(go.transform, false);
            beaconGo.transform.position = new Vector3((min.x + max.x) * 0.5f, min.y + 1.2f, min.z - 0.6f);
            var beacon = beaconGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = new Color(0.35f, 1f, 0.5f);
            beacon.range = 3f;
            beacon.shadows = LightShadows.None;
            beacon.enabled = false;

            go.AddComponent<Deprem.Family.CabinBeacon>().Configure(sign, beacon, renderer, 0);
        }

        private static void BuildWindow(Transform parent)
        {
            var salon = HouseLayout.Room("salon");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Decor/pencere.png");
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Decor/pencere_emission.png");
            // Berke'nin play testi: doygun mavi sırıtıyordu; çizim gelene kadar sönük gece mavisi
            var mat = GetMaterial("pencere", null, new Color(0.2f, 0.23f, 0.29f));
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null ? Color.white : new Color(0.2f, 0.23f, 0.29f));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", emission != null ? Color.white : new Color(0.06f, 0.08f, 0.12f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            var window = GameObject.CreatePrimitive(PrimitiveType.Quad);
            window.name = "Window_Salon";
            Object.DestroyImmediate(window.GetComponent<Collider>());
            window.transform.SetParent(parent, false);
            window.transform.position = new Vector3(HouseDims.SalonWindowX, 1.5f, salon.Depth - 0.01f);
            window.transform.localScale = new Vector3(1.5f, 1f, 1f);
            window.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------------------------------------------------------------- Eşyalar

        private static void BuildItem(ItemDef item, Transform parent)
        {
            var room = HouseLayout.Room(item.Room);
            float toppleFrontZ = HouseLayout.FloorItemFront(item);
            float frontZ = item.Mount == ItemMount.BackWall ? room.Depth - item.Size.z
                : item.RestsAtBackWall ? room.Depth - item.Size.z - HouseLayout.BackWallGap
                : toppleFrontZ;
            var min = new Vector3(item.CenterX - item.Size.x * 0.5f, room.FloorY + item.BaseY, frontZ);
            var max = min + item.Size;

            var go = AddShape(parent, $"Item_{item.Number}_{item.Key}", min, max, "Items", item.Key, ItemColor(item.Number), item.PlainSides, true);
            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(5f, item.Size.x * item.Size.y * item.Size.z * 300f);
            body.isKinematic = true; // deprem sistemi sabitlenmemiş eşyayı serbest bırakır
            var toppler = go.AddComponent<Deprem.Quake.ItemToppler>();
            if (item.Mount == ItemMount.Floor && item.RestsAtBackWall)
            {
                // Arkada durur, devrilirken eski yerine (karakterin düzlemine düşeceği derinliğe) yürür
                var toppleSo = new SerializedObject(toppler);
                toppleSo.FindProperty("walkForward").boolValue = true;
                toppleSo.FindProperty("walkToZ").floatValue = toppleFrontZ + item.Size.z * 0.5f;
                toppleSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Core: Berke'nin minigame'i Secure() çağırır, deprem IsSecured okur
            var securedVisual = BuildSecuredVisual(item, go.transform, min, max);
            var securable = go.AddComponent<Deprem.Core.SecurableItem>();
            var so = new SerializedObject(securable);
            so.FindProperty("id").enumValueIndex = item.Number;
            so.FindProperty("minigame").enumValueIndex = (int)item.Minigame;
            so.FindProperty("securedVisual").objectReferenceValue = securedVisual;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (item.Key == "tv_unitesi")
            {
                var tvMin = new Vector3(item.CenterX - HouseLayout.TvSize.x * 0.5f, max.y, frontZ + 0.15f);
                AddShape(go.transform, "TV", tvMin, tvMin + HouseLayout.TvSize, "Items", "tv", new Color(0.1f, 0.1f, 0.12f), false, true);
            }

            BuildOutline(go, securedVisual);
        }

        /// <summary>Mobilyalar: çarpışmasız ve sabit; devrilen eşyalar içlerinden geçebilir</summary>
        private static void BuildDecor(DecorDef decor, Transform parent)
        {
            var room = HouseLayout.Room(decor.Room);
            var min = new Vector3(decor.X0, room.FloorY, decor.Z0);
            // Doku yokken mobilyalar birbirinden ayrılsın: anahtara göre hafif farklı ahşap tonu
            int hash = decor.Key.Sum(c => c);
            var color = Color.HSVToRGB(0.05f + hash % 7 * 0.012f, 0.3f, 0.5f + hash % 5 * 0.05f);
            var go = AddShape(parent, $"Decor_{decor.Key}_{decor.Room}", min, min + decor.Size, "Decor", decor.Key, color, false, false);
            go.isStatic = true;
            // Yataklar şilte hizasında çarpışır: döngü 1'de çöken tavan parçaları ve raf yatağın üstünde kalır
            // (karakter düzleminin arkasında, oyuncunun yolunu kesmez)
            if (decor.Key.EndsWith("yatak") || decor.Key.EndsWith("yatagi"))
            {
                var bed = go.AddComponent<BoxCollider>();
                bed.size = new Vector3(decor.Size.x, 0.66f, decor.Size.z); // şilte üstü (FurnitureShapes)
                bed.center = new Vector3(0f, 0.33f - decor.Size.y * 0.5f, 0f);
            }
            // Güvenli masanın tablası çarpışır ki çök-kapan sırasında düşen taşlar üstünde kalsın (Play testi 11).
            // Sadece tablanın ön kısmı (Z 0,15–0,6): karakter düzlemindeki kapsül (Z ≥ ~0,7) takılmaz.
            if (Mathf.Approximately(decor.Z0, HouseLayout.SafeTableZ))
            {
                var top = go.AddComponent<BoxCollider>();
                float depth = Mathf.Min(decor.Size.z, 0.6f - decor.Z0);
                top.size = new Vector3(decor.Size.x, 0.05f, depth);
                top.center = new Vector3(0f, decor.Size.y * 0.5f - 0.025f, -decor.Size.z * 0.5f + depth * 0.5f);
            }
        }

        /// <summary>
        /// Ruh fazı konturu: eşyanın her gövde mesh'inin (TV, kapaklar dahil; L demiri hariç) aynı mesh'li bir kopyası,
        /// tüm submesh'lerde Deprem/Outline materyaliyle. Başta kapalı; ItemOutline açar.
        /// </summary>
        private static void BuildOutline(GameObject item, GameObject securedVisual)
        {
            item.AddComponent<Deprem.Visual.ItemOutline>().Configure(BuildOutlineRenderers(item, securedVisual));
        }

        /// <summary>Objenin gövde mesh'lerinin kontur kopyaları (kapalı). Eşya konturu ve güvenli masa (M16) kullanır.</summary>
        private static Renderer[] BuildOutlineRenderers(GameObject item, GameObject skip)
        {
            var mat = OutlineMaterial();
            var renderers = new List<Renderer>();
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true).ToList())
            {
                if (skip != null && filter.transform.IsChildOf(skip.transform)) continue;
                renderers.Add(CreateOutlineRenderer(filter, mat));
            }
            return renderers.ToArray();
        }

        private static Renderer CreateOutlineRenderer(MeshFilter filter, Material mat)
        {
            var outline = new GameObject("Outline");
            outline.transform.SetParent(filter.transform, false);
            outline.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = outline.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(mat, filter.sharedMesh.subMeshCount).ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>Güvenli masa konturu: eşya konturundan ince ve daha yumuşak nabızlı (Play testi 6 sonrası)</summary>
        private static Material SafeTableOutlineMaterial()
        {
            if (MaterialCache.TryGetValue("kontur_masa", out var cached)) return cached;
            string path = $"{MaterialFolder}/kontur_masa.mat";
            var shader = Shader.Find("Deprem/Outline");
            if (shader == null) throw new System.InvalidOperationException("Deprem/Outline shader bulunamadı (Assets/Shaders/Outline.shader)");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_OutlineColor", new Color(1f, 0.85f, 0.45f));
            mat.SetFloat("_OutlineWidth", 0.015f);
            mat.SetFloat("_PulseSpeed", 4f);
            mat.SetFloat("_PulseAmount", 0.6f);
            EditorUtility.SetDirty(mat);
            MaterialCache["kontur_masa"] = mat;
            return mat;
        }

        private static Material OutlineMaterial()
        {
            if (MaterialCache.TryGetValue("kontur", out var cached)) return cached;
            string path = $"{MaterialFolder}/kontur.mat";
            var shader = Shader.Find("Deprem/Outline");
            if (shader == null) throw new System.InvalidOperationException("Deprem/Outline shader bulunamadı (Assets/Shaders/Outline.shader)");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            MaterialCache["kontur"] = mat;
            return mat;
        }

        /// <summary>
        /// Sabitlenince görünen parça: vidalı eşyalarda üst-arka köşede L demiri, dolapta mandal.
        /// Taşı-yerleştir eşyalarında yok (eşyanın kendisi güvenli yere taşınır).
        /// </summary>
        private static GameObject BuildSecuredVisual(ItemDef item, Transform parent, Vector3 min, Vector3 max)
        {
            var metal = GetMaterial("l_demiri", "Assets/Art/Items/l_demiri.png", new Color(0.55f, 0.57f, 0.6f));
            GameObject visual;
            if (item.Minigame == Deprem.Core.MinigameType.ScrewTighten)
            {
                visual = new GameObject("SecuredVisual");
                visual.transform.SetParent(parent, false);
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? min.x + 0.1f : max.x - 0.25f;
                    var bracketMin = new Vector3(x, max.y, max.z - 0.15f);
                    AddDecorBox(visual.transform, "Bracket_" + side, bracketMin, bracketMin + new Vector3(0.15f, 0.03f, 0.15f), metal);
                }
            }
            else if (item.Minigame == Deprem.Core.MinigameType.Lock)
            {
                var latchMin = new Vector3((min.x + max.x) * 0.5f - 0.06f, min.y + 0.3f, min.z - 0.06f);
                visual = AddDecorBox(parent, "SecuredVisual", latchMin, latchMin + new Vector3(0.12f, 0.15f, 0.03f), metal);
            }
            else
            {
                return null;
            }
            visual.SetActive(false);
            return visual;
        }

        /// <summary>Çarpışmasız, Z ekseninde açılı kutu (eğik kiriş, küpeşte): merkez, boyut (yerel), açı (derece)</summary>
        private static GameObject AddSlopedBox(Transform parent, string name, Vector3 center, Vector3 size, float angle, Material mat)
        {
            var go = AddDecorBox(parent, name, center - size * 0.5f, center + size * 0.5f, mat);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return go;
        }

        /// <summary>Çarpışmasız, tek materyalli küçük kutu</summary>
        private static GameObject AddDecorBox(Transform parent, string name, Vector3 min, Vector3 max, Material mat)
        {
            var mesh = BoxMeshBuilder.Build(name, min, max, BoxMeshBuilder.FitUv(min, max), face => 0, 1);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static Color ItemColor(int number)
        {
            return Color.HSVToRGB(number / 9f, 0.45f, 0.75f);
        }

        // Detay tonları (FurnitureShapes.PartTone, Body hariç): doku yokken parçalar bu renklerle seçilir
        private static readonly (string key, Color color)[] ToneMaterials =
        {
            // Berke'nin play testi: doygun kırmızı / mavi / yeşil parçalar absürt duruyordu (yeşil çekmece,
            // gri şilte). Hepsi ahşap ve bej ailesinden; renk_b şilte/örtü, renk_c çekmece ve kapak
            ("detay_metal", new Color(0.5f, 0.49f, 0.47f)),
            ("detay_kumas", new Color(0.78f, 0.74f, 0.65f)),
            ("detay_cam", new Color(0.56f, 0.6f, 0.6f)),
            ("detay_koyu", new Color(0.16f, 0.14f, 0.13f)),
            ("detay_renk_a", new Color(0.55f, 0.42f, 0.36f)),
            ("detay_renk_b", new Color(0.72f, 0.67f, 0.57f)),
            ("detay_renk_c", new Color(0.46f, 0.34f, 0.24f)),
            ("detay_kitap", new Color(0.55f, 0.45f, 0.36f)), // eşyaya özel kitap sırtı dokusu (BooksMaterial)
            ("detay_yastik", new Color(0.86f, 0.84f, 0.79f)), // yastık kılıfı (PartTone.Linen)
            // Oyuncaklar (PartTone.Toy*): pastel ama seçilir; sadece oyuncak dolabında
            ("oyuncak_kirmizi", new Color(0.78f, 0.36f, 0.3f)),
            ("oyuncak_mavi", new Color(0.36f, 0.52f, 0.75f)),
            ("oyuncak_sari", new Color(0.9f, 0.74f, 0.32f)),
            ("oyuncak_yesil", new Color(0.45f, 0.66f, 0.4f)),
        };

        /// <summary>
        /// FurnitureShapes'teki parçalardan tek mesh. Materyaller: ön, yan (arka ve yanlar), üst (üst ve alt),
        /// sonra detay tonları (kulp, yastık, cam, ekran, renkli kitaplar). Dokular dış kutuya yansıtılır
        /// (Assets/Art/{artFolder}/{textureKey}_on.png ...); doku varsa detaylar da çizimden gelir. Çarpışma dış kutu.
        /// </summary>
        private static GameObject AddShape(Transform parent, string name, Vector3 min, Vector3 max, string artFolder,
            string textureKey, Color fallback, bool plainSides, bool collider)
        {
            var parts = FurnitureShapes.For(textureKey, max - min);
            var boxes = parts.Select(p => (min + p.Min, min + p.Max)).ToList();
            string frontPath = $"Assets/Art/{artFolder}/{textureKey}_on.png";
            bool hasArt = AssetDatabase.LoadAssetAtPath<Texture2D>(frontPath) != null;
            int SubmeshFor(int box, BoxFace face)
            {
                var tone = parts[box].Tone;
                return hasArt || tone == PartTone.Body ? FaceToItemSubmesh(face) : 2 + (int)tone;
            }
            int submeshCount = 3 + ToneMaterials.Length;
            var mesh = BoxMeshBuilder.BuildParts(name, (min + max) * 0.5f, boxes, BoxMeshBuilder.FitUv(min, max), SubmeshFor, submeshCount);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var side = fallback * 0.8f;
            side.a = 1f;
            var size = max - min;
            var mats = new List<Material>
            {
                BodyMaterial(textureKey, "on", frontPath, fallback, new Vector2(size.x, size.y)),
                BodyMaterial(textureKey, "yan", plainSides ? null : $"Assets/Art/{artFolder}/{textureKey}_yan.png", side, new Vector2(size.z, size.y)),
                BodyMaterial(textureKey, "ust", plainSides ? null : $"Assets/Art/{artFolder}/{textureKey}_ust.png", side, new Vector2(size.x, size.z)),
            };
            foreach (var (key, color) in ToneMaterials)
                mats.Add(key == "detay_kitap" && !hasArt && parts.Any(p => p.Tone == PartTone.Books)
                    ? BooksMaterial(textureKey, new Vector2(size.x, size.y), color)
                    : GetMaterial(key, null, color));
            go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
            if (collider) go.AddComponent<BoxCollider>().size = max - min;
            return go;
        }

        /// <summary>Kitap sırtları: eşyanın ön yüzü boyutunda (UV dış kutuya yayıldığı için) pixel doku</summary>
        private static Material BooksMaterial(string key, Vector2 frontSize, Color fallback)
        {
            string name = key + "_kitap";
            bool cached = MaterialCache.ContainsKey(name);
            var mat = GetMaterial(name, null, fallback);
            if (!cached)
            {
                mat.SetTexture("_BaseMap", PixelFurnitureTextures.Books(key, frontSize));
                mat.SetColor("_BaseColor", Color.white);
            }
            return mat;
        }

        /// <summary>
        /// Gövde yüzü materyali: sanatçı dokusu varsa o; yoksa yüzün boyutunda kodla üretilen pixel art doku
        /// (PixelFurnitureTextures; düz renk salonun pixel art duvarlarının yanında sırıtıyordu).
        /// </summary>
        private static Material BodyMaterial(string key, string face, string artPath, Color fallback, Vector2 faceSize)
        {
            string name = key + "_" + face;
            bool hasArt = artPath != null && AssetDatabase.LoadAssetAtPath<Texture2D>(artPath) != null;
            bool cached = MaterialCache.ContainsKey(name);
            var mat = GetMaterial(name, hasArt ? artPath : null, fallback);
            if (!hasArt && !cached)
            {
                mat.SetTexture("_BaseMap", PixelFurnitureTextures.Get(key, face, faceSize, fallback));
                mat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static int FaceToItemSubmesh(BoxFace face)
        {
            switch (face)
            {
                case BoxFace.NegZ: return 0;
                case BoxFace.PosY:
                case BoxFace.NegY: return 2;
                default: return 1;
            }
        }

        // ---------------------------------------------------------------- Işık, işaretler, kamera

        private static void BuildLights(Transform parent)
        {
            var lamps = new List<Light>();
            var emergency = new List<Light>();
            var fixtures = new List<Renderer>();
            var fixtureOn = GetMaterial("tavan_lambasi", "Assets/Art/Decor/tavan_lambasi.png", new Color(1f, 0.92f, 0.75f));
            fixtureOn.EnableKeyword("_EMISSION");
            fixtureOn.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f) * 2f);
            fixtureOn.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            var fixtureOff = GetMaterial("tavan_lambasi_sonuk", "Assets/Art/Decor/tavan_lambasi.png", new Color(0.35f, 0.33f, 0.3f));

            foreach (var room in HouseLayout.Rooms)
            {
                // Elektrik kesilince odayı loş tutan soğuk ışık (sokak / ay ışığı; deprem sistemi açar).
                // Kırmızı değil: karanlık soğuk ve loş olsun (Play testi 2). Kardeşin odasında çok sönük:
                // oda açılınca fark edilir (B10) ama kardeş ancak el feneriyle seçilir (GDD).
                {
                    bool siblingRoom = room.Key == "kardes";
                    var dim = new GameObject("Emergency_" + room.Key);
                    dim.transform.SetParent(parent, false);
                    dim.transform.position = new Vector3((room.X0 + room.X1) * 0.5f, room.CeilingY - 0.4f, room.Depth * 0.6f);
                    var dimLight = dim.AddComponent<Light>();
                    dimLight.type = LightType.Point;
                    dimLight.color = new Color(0.55f, 0.65f, 0.9f);
                    // Play testi 3: elektrik gidince her yer rahat görünüyordu (0,9 -> 0,35); ENTEGRASYON B10: bu kez
                    // açılan oda kapalıyla aynı karanlıkta kalıyor, açıldığı anlaşılmıyordu. Arası.
                    dimLight.intensity = siblingRoom ? 0.15f : 0.6f;
                    dimLight.range = Mathf.Max(room.Width, room.Height) * 1.1f;
                    dimLight.shadows = LightShadows.Soft;
                    dimLight.enabled = false;
                    emergency.Add(dimLight);
                }

                // Uzun odalara (koridor, sahanlık) iki lamba
                int count = room.Width > 8f ? 2 : 1;
                float span = room.Width / count;
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject(count == 1 ? "Lamp_" + room.Key : $"Lamp_{room.Key}_{i}");
                    go.transform.SetParent(parent, false);
                    // Tavanın ortasında; kameraya bakan yüzleri önden gelen dolgu ışığı aydınlatır
                    var center = new Vector3(room.X0 + span * (i + 0.5f), room.CeilingY, room.Depth * 0.5f);
                    go.transform.position = center + Vector3.down * 0.35f;
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.85f, 0.65f);
                    light.intensity = 4f;
                    light.range = Mathf.Max(span, room.Depth) * 1.3f;
                    light.shadows = LightShadows.Soft;
                    lamps.Add(light);

                    // Yansıma ışığı: tavan lambası arka duvara zayıf ulaşır (mesafenin karesiyle söner). Gerçekte duvarı
                    // tavandan ve zeminden seken ışık aydınlatır; bunu arka duvarın önünde gölgesiz, zayıf bir ışıkla
                    // taklit ediyoruz. Lambanın parçası: elektrik kesilince söner, sarsıntıda titrer.
                    var bounce = new GameObject("Bounce");
                    bounce.transform.SetParent(go.transform, false);
                    // Duvardan uzakta ve geniş menzilli: duvarda belirgin bir ışık havuzu değil, yumuşak bir aydınlık
                    bounce.transform.position = new Vector3(center.x, room.FloorY + room.Height * 0.55f, Mathf.Max(room.Depth * 0.5f, room.Depth - 1.8f));
                    var bounceLight = bounce.AddComponent<Light>();
                    bounceLight.type = LightType.Point;
                    bounceLight.color = new Color(1f, 0.82f, 0.6f);
                    bounceLight.intensity = 2.2f;
                    bounceLight.range = Mathf.Max(span, room.Height) * 1.5f;
                    bounceLight.shadows = LightShadows.None;
                    lamps.Add(bounceLight);

                    // Tavandaki lamba gövdesi (ışığın kendi gölgesine girmesin diye gölge vermez)
                    var fixtureMin = center + new Vector3(-0.2f, -0.12f, -0.2f);
                    var fixture = AddDecorBox(go.transform, "Fixture", fixtureMin, fixtureMin + new Vector3(0.4f, 0.12f, 0.4f), fixtureOn);
                    var fixtureRenderer = fixture.GetComponent<MeshRenderer>();
                    fixtureRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    fixtures.Add(fixtureRenderer);
                }
            }
            // Önden zayıf dolgu ışığı: lambalar tavanın ortasındayken karakterlerin ve eşyaların kameraya bakan
            // yüzleri kararmasın. Gölgesiz. Rengini HouseLights yönetir: elektrik varken lambayla aynı sıcak ton
            // (yoksa kameraya bakan arka duvar yan duvarlardan soluk görünür), kesilince soğuk ay ışığı.
            var fill = new GameObject("Fill");
            fill.transform.SetParent(parent, false);
            fill.transform.rotation = Quaternion.Euler(12f, 8f, 0f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(1f, 0.88f, 0.74f);
            fillLight.intensity = 0.3f;
            fillLight.shadows = LightShadows.None;

            parent.gameObject.AddComponent<Deprem.Quake.HouseLights>()
                .Configure(lamps.ToArray(), emergency.ToArray(), fixtures.ToArray(), fixtureOn, fixtureOff, fillLight);
        }

        private static void BuildMarkers(Transform parent)
        {
            var icon = EditorGUIUtility.IconContent("sv_label_1").image as Texture2D;
            foreach (var marker in HouseLayout.Markers)
            {
                var go = new GameObject(marker.Name);
                go.transform.SetParent(parent, false);
                go.transform.position = marker.Position;
                if (icon != null) EditorGUIUtility.SetIconForObject(go, icon);
            }
        }

        /// <summary>
        /// Güvenli noktalar: karakter düzleminde, odanın önde duran sağlam masasının altı.
        /// M16: sarsıntıda yerde şerit yerine masanın konturu sarı parlar.
        /// </summary>
        private static void BuildSafeSpots(Transform parent, Transform decorRoot)
        {
            foreach (var (roomKey, x) in HouseLayout.SafeSpots)
            {
                var room = HouseLayout.Room(roomKey);
                var go = new GameObject("SafeSpot_" + roomKey);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, room.FloorY, HouseDims.PlayerPlaneZ);

                Renderer[] outline = null;
                Light glow = null;
                var table = HouseLayout.Decor.FirstOrDefault(d => d.Room == roomKey && Mathf.Approximately(d.Z0, HouseLayout.SafeTableZ));
                var tableObject = table != null ? decorRoot.Find($"Decor_{table.Key}_{table.Room}") : null;
                if (tableObject != null)
                {
                    // Sadece üst tabla ince konturlu (bütün parçalar konturlanınca çekmece ve ayaklar dolu görünüyordu)
                    var top = tableObject.GetComponentsInChildren<MeshFilter>(true)
                        .OrderByDescending(f => f.GetComponent<Renderer>() != null ? f.GetComponent<Renderer>().bounds.max.y : 0f)
                        .FirstOrDefault();
                    if (top != null) outline = new[] { CreateOutlineRenderer(top, SafeTableOutlineMaterial()) };

                    // Masanın altında yumuşak sıcak ışık: "altına çök"
                    glow = new GameObject("UnderTableGlow").AddComponent<Light>();
                    glow.transform.SetParent(go.transform, false);
                    glow.transform.position = new Vector3(x, room.FloorY + table.Size.y * 0.45f, table.Z0 + table.Size.z * 0.5f);
                    glow.type = LightType.Point;
                    glow.color = new Color(1f, 0.8f, 0.45f);
                    glow.range = 1.4f;
                    glow.intensity = 1.6f;
                    glow.shadows = LightShadows.None;
                    glow.enabled = false;
                }
                else Debug.LogWarning($"[HouseBuilder] Güvenli masa bulunamadı: {roomKey}");

                go.AddComponent<Deprem.Quake.SafeSpot>().Configure(HouseLayout.SafeSpotWidth, null, outline, glow);
            }
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 25f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.position = new Vector3(4f, 1.6f, -11.5f);
            go.AddComponent<AudioListener>();
        }

        private static void ApplyNightLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Gece; yan duvarlar (önden dolgu ışığını almayanlar) arka duvardan çok koyu kalmasın
            RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.15f);
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- Yardımcılar

        /// <summary>Oda yüzeyi parçası: ana yüzlere oda dokusu, evin önündeki kesit yüzüne döşenen kesit dokusu</summary>
        private static void AddPiece(Transform parent, string name, Vector3 min, Vector3 max, BoxMeshBuilder.UvFunc uv, Material mat)
        {
            if (max.x - min.x < 0.001f || max.y - min.y < 0.001f || max.z - min.z < 0.001f) return;

            bool atFront = min.z < 0.001f;
            BoxMeshBuilder.UvFunc pieceUv = (p, face) => atFront && face == BoxFace.NegZ ? BoxMeshBuilder.TileUv(p, face) : uv(p, face);
            var mesh = BoxMeshBuilder.Build(name, min, max, pieceUv, face => atFront && face == BoxFace.NegZ ? 1 : 0, 2);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { mat, SectionMaterial() };
            go.AddComponent<BoxCollider>().size = max - min;
        }

        /// <summary>Yüzeyin iki dünya ekseni boyunca 0-1 UV (oda boyutunda çizilmiş doku için)</summary>
        private static BoxMeshBuilder.UvFunc Planar(int axisU, float originU, float sizeU, int axisV, float originV, float sizeV)
        {
            return (p, face) => new Vector2((p[axisU] - originU) / sizeU, (p[axisV] - originV) / sizeV);
        }

        private static Material RoomMaterial(RoomDef room, string surface, float value)
        {
            int index = System.Array.IndexOf(HouseLayout.Rooms, room);
            var fallback = Color.HSVToRGB(index / (float)HouseLayout.Rooms.Length, 0.12f, value);
            return GetMaterial($"{room.Key}_{surface}", RoomTexturePath(room.Key, surface), fallback);
        }

        /// <summary>
        /// Oda dokusu, ilk bulunan: Rooms/{oda}_{yüzey}.png, Rooms/{oda}/{yüzey}.png;
        /// duvarlar için tek doku da olur: Rooms/{oda}_duvar.png ya da Rooms/{oda}/duvar.png (arka ve yan duvara).
        /// Doku yüzeye sığdırılır; en iyi sonuç GDD'deki boyutlarda (ör. salon arka 576x224).
        /// </summary>
        private static string RoomTexturePath(string room, string surface)
        {
            var candidates = new List<string>
            {
                $"Assets/Art/Rooms/{room}_{surface}.png",
                $"Assets/Art/Rooms/{room}/{surface}.png",
            };
            if (surface == "arka" || surface == "yan")
            {
                candidates.Add($"Assets/Art/Rooms/{room}_duvar.png");
                candidates.Add($"Assets/Art/Rooms/{room}/duvar.png");
            }
            foreach (var path in candidates)
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) return path;
            return candidates[0];
        }

        private static Material SectionMaterial()
        {
            return GetMaterial("kesit", "Assets/Art/Tiles/kesit.png", new Color(0.12f, 0.11f, 0.11f));
        }

        /// <summary>Materyali bulur ya da oluşturur; GUID değişmesin diye var olanı günceller.</summary>
        private static Material GetMaterial(string name, string texturePath, Color fallback)
        {
            if (MaterialCache.TryGetValue(name, out var cached)) return cached;

            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new System.InvalidOperationException("URP Lit shader bulunamadı");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = texturePath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) : null;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null ? Color.white : fallback);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            MaterialCache[name] = mat;
            return mat;
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
