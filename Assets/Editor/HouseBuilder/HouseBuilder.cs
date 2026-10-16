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
            var volumes = Deprem.Visual.VolumeSetup.SetupInScene(scene);
            BerkeSystemsSetup.SetupInScene(scene);
            EditorSceneManager.SaveScene(scene);
            var whole = new Vector3(16.25f, 3.6f, -39f);
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

            ArtTemplates.Generate();
            Debug.Log("[HouseBuilder] Batch tamam");
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

            BuildLights(Child(root.transform, "Lights"));
            BuildMarkers(Child(root.transform, "Markers"));
            BuildSafeSpots(Child(root.transform, "SafeSpots"));

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
            var go = AddBox(parent, "Cabin", HouseDims.CabinMin, HouseDims.CabinMax, "kabin", new Color(0.85f, 0.55f, 0.15f), false);
            go.isStatic = true;
        }

        private static void BuildWindow(Transform parent)
        {
            var salon = HouseLayout.Room("salon");
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Decor/pencere.png");
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Decor/pencere_emission.png");
            var mat = GetMaterial("pencere", null, new Color(0.25f, 0.35f, 0.55f));
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null ? Color.white : new Color(0.25f, 0.35f, 0.55f));
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", emission != null ? Color.white : new Color(0.15f, 0.22f, 0.4f));
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
            float frontZ = item.Mount == ItemMount.BackWall ? room.Depth - item.Size.z : HouseLayout.FloorItemFront(item);
            var min = new Vector3(item.CenterX - item.Size.x * 0.5f, room.FloorY + item.BaseY, frontZ);
            var max = min + item.Size;

            var go = AddBox(parent, $"Item_{item.Number}_{item.Key}", min, max, item.Key, ItemColor(item.Number), item.PlainSides);
            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(5f, item.Size.x * item.Size.y * item.Size.z * 300f);
            body.isKinematic = true; // deprem sistemi sabitlenmemiş eşyayı serbest bırakır
            go.AddComponent<Deprem.Quake.ItemToppler>();

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
                var tvMin = new Vector3(item.CenterX - 0.5f, max.y, frontZ + 0.15f);
                AddBox(go.transform, "TV", tvMin, tvMin + new Vector3(1f, 0.625f, 0.125f), "tv", new Color(0.1f, 0.1f, 0.12f), false);
            }
            else if (item.Key == "mutfak_dolabi")
            {
                for (int i = 0; i < 2; i++)
                {
                    var doorMin = new Vector3(min.x + i * 0.75f, min.y, frontZ - 0.03f);
                    AddBox(go.transform, "Door_" + i, doorMin, doorMin + new Vector3(0.75f, 0.75f, 0.03f),
                        "mutfak_kapagi", new Color(0.75f, 0.7f, 0.6f), true);
                }
            }

            BuildOutline(go, securedVisual);
        }

        /// <summary>
        /// Ruh fazı konturu: eşyanın her gövde mesh'inin (TV, kapaklar dahil; L demiri hariç) aynı mesh'li bir kopyası,
        /// tüm submesh'lerde Deprem/Outline materyaliyle. Başta kapalı; ItemOutline açar.
        /// </summary>
        private static void BuildOutline(GameObject item, GameObject securedVisual)
        {
            var mat = OutlineMaterial();
            var renderers = new List<Renderer>();
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>(true).ToList())
            {
                if (securedVisual != null && filter.transform.IsChildOf(securedVisual.transform)) continue;
                var outline = new GameObject("Outline");
                outline.transform.SetParent(filter.transform, false);
                outline.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = outline.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Enumerable.Repeat(mat, filter.sharedMesh.subMeshCount).ToArray();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                renderers.Add(renderer);
            }
            item.AddComponent<Deprem.Visual.ItemOutline>().Configure(renderers.ToArray());
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

        /// <summary>3 materyalli kutu: ön, yan (arka ve yanlar), üst (üst ve alt)</summary>
        private static GameObject AddBox(Transform parent, string name, Vector3 min, Vector3 max, string textureKey,
            Color fallback, bool plainSides)
        {
            var mesh = BoxMeshBuilder.Build(name, min, max, BoxMeshBuilder.FitUv(min, max), FaceToItemSubmesh, 3);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * 0.5f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var side = fallback * 0.8f;
            side.a = 1f;
            var mats = new[]
            {
                GetMaterial(textureKey + "_on", $"Assets/Art/Items/{textureKey}_on.png", fallback),
                plainSides ? GetMaterial(textureKey + "_yan", null, side) : GetMaterial(textureKey + "_yan", $"Assets/Art/Items/{textureKey}_yan.png", side),
                plainSides ? GetMaterial(textureKey + "_ust", null, side) : GetMaterial(textureKey + "_ust", $"Assets/Art/Items/{textureKey}_ust.png", side),
            };
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            go.AddComponent<BoxCollider>().size = max - min;
            return go;
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
                // Elektrik kesilince yanan kırmızı acil durum ışığı (deprem sistemi açar).
                // Kardeşin odasında yok: oda karanlık kalır, kardeş ancak el feneriyle görünür (GDD).
                if (room.Key != "kardes")
                {
                    var red = new GameObject("Emergency_" + room.Key);
                    red.transform.SetParent(parent, false);
                    red.transform.position = new Vector3(room.X0 + 0.6f, room.CeilingY - 0.3f, room.Depth * 0.5f);
                    var redLight = red.AddComponent<Light>();
                    redLight.type = LightType.Point;
                    redLight.color = new Color(1f, 0.12f, 0.08f);
                    redLight.intensity = 1.6f;
                    redLight.range = room.Width * 0.9f;
                    redLight.shadows = LightShadows.Soft;
                    redLight.enabled = false;
                    emergency.Add(redLight);
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

        /// <summary>Güvenli noktalar: karakter düzleminde, yerde parlayan şerit (sarsıntıda görünür)</summary>
        private static void BuildSafeSpots(Transform parent)
        {
            var mat = GetMaterial("guvenli_nokta", "Assets/Art/Decals/guvenli_nokta.png", new Color(0.55f, 1f, 0.6f));
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.35f, 0.9f, 0.45f) * 1.5f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            foreach (var (roomKey, x) in HouseLayout.SafeSpots)
            {
                var room = HouseLayout.Room(roomKey);
                var go = new GameObject("SafeSpot_" + roomKey);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(x, room.FloorY, HouseDims.PlayerPlaneZ);

                var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glow.name = "Glow";
                Object.DestroyImmediate(glow.GetComponent<Collider>());
                glow.transform.SetParent(go.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                glow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glow.transform.localScale = new Vector3(HouseLayout.SafeSpotWidth, HouseLayout.SafeSpotDepth, 1f);
                var renderer = glow.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                glow.SetActive(false);

                go.AddComponent<Deprem.Quake.SafeSpot>().Configure(HouseLayout.SafeSpotWidth, glow);
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
