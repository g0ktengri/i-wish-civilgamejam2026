using System.IO;
using System.Linq;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Deprem.Player
{
    /// <summary>
    /// Oyuncu prefab'ını (Assets/Prefabs/Mehmet/Player.prefab) oluşturur, sahneye Marker_PlayerStart'a koyar
    /// ve Cinemachine kamerasını oyuncuyu takip edecek şekilde kurar.
    /// </summary>
    internal static class PlayerSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Mehmet/Player.prefab";
        private const string MaterialPath = "Assets/Generated/Player/player_placeholder.mat";
        private const string PlayerName = "Player";
        private const string CameraName = "PlayerCamera";

        // GDD: perspektif, FOV 20-30, ekranda en fazla 8-10 m
        private const float FieldOfView = 25f;
        private static readonly Vector3 FollowOffset = new Vector3(0f, 1.6f, -11.5f);

        [MenuItem("Tools/Deprem/Oyuncu ve Kamerayı Kur (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            SetupInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PlayerSetup] Oyuncu ve kamera kuruldu: " + scene.name);
        }

        public static GameObject SetupInScene(Scene scene)
        {
            var prefab = CreateOrUpdatePrefab();

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == PlayerName).ToList())
                Object.DestroyImmediate(old);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = PlayerName;
            var start = FindInScene(scene, "Marker_PlayerStart");
            player.transform.position = start != null ? start.position : new Vector3(3f, 0f, 1f);

            SetupCamera(scene, player.transform);
            return player;
        }

        private static GameObject CreateOrUpdatePrefab()
        {
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));

            var root = new GameObject(PlayerName);
            root.AddComponent<Rigidbody>();
            root.AddComponent<CapsuleCollider>();
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerInteractor>();
            root.AddComponent<SpeechBubble>();

            // Görsel kök ayakta: ölçek (yön çevirme, eğilme) ayak hizasından uygulanır
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Placeholder";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(visual.transform, false);
            quad.GetComponent<MeshFilter>().sharedMesh = CharacterQuadMesh.Get(); // tavan lambalarının ışığını alsın
            var runFrames = LoadFrames("oyuncu_kosma");
            if (runFrames.Length > 0)
            {
                // 128 x 128 px kare = 2 x 2 m (64 PPU). Ayak çizimde alttan 2 px yukarıda, gövde ortası
                // pivotun ~9 px solunda: kare buna göre kaydırılır ki ayak yere, gövde pivota otursun.
                quad.transform.localPosition = new Vector3(FrameOffsetX, 1f - FrameFootPx / 64f, 0f);
                quad.transform.localScale = new Vector3(2f, 2f, 1f);
            }
            else
            {
                quad.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                quad.transform.localScale = new Vector3(0.6f, 1.7f, 1f);
            }
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PlaceholderMaterial(runFrames.FirstOrDefault());
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            // Lamba karakterin arkasında: düz görsel kendi gölgesini yüzüne düşürüp kararıyordu
            renderer.receiveShadows = false;

            var playerVisual = root.AddComponent<PlayerVisual>();
            var so = new SerializedObject(playerVisual);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.FindProperty("body").objectReferenceValue = renderer;
            SetFrames(so, "runFrames", runFrames);
            // Ekip çizimleri: bekleme (2 kare) ve çömelme (5 kare; eğilme ve çök-kapan-tutun)
            SetFrames(so, "idleFrames", LoadFrames("oyuncu_bekleme"));
            SetFrames(so, "crouchFrames", LoadFrames("oyuncu_egilme"));
            var flashlightFrames = LoadFrames("oyuncu_fenerle_kosma");
            SetFrames(so, "flashlightRunFrames", flashlightFrames); // kurtarma fazında fener yanarken
            // Huzme her karede fenerin ucundan çıksın: uç çizimden bulunur
            var tips = so.FindProperty("flashlightTips");
            tips.arraySize = flashlightFrames.Length;
            for (int i = 0; i < flashlightFrames.Length; i++)
                tips.GetArrayElementAtIndex(i).vector2Value = FindFlashlightTip(flashlightFrames[i]);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2.5D kenar ışığı: ışığa bakan kenarda 2 piksellik parlak şerit (Deprem/RimLight)
            root.AddComponent<Deprem.Visual.CharacterRimLight>().Configure(renderer, RimMaterial());

            // El feneri: çantada varsa kurtarma fazında yanar
            var spotGo = new GameObject("Flashlight");
            spotGo.transform.SetParent(root.transform, false);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.95f, 0.8f);
            spot.intensity = 14f;
            spot.range = 11f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 30f;
            spot.shadows = LightShadows.Soft;
            spot.enabled = false;
            // Elektrik kesikken oyuncunun çevresindeki loş görüş ışığı (karanlıkta sadece yakını görünür)
            var sightGo = new GameObject("NightSight");
            sightGo.transform.SetParent(root.transform, false);
            // Play testi 4: çok ışıklıydı; artık küçük ve önde, sadece karakteri hafifçe seçtirir
            sightGo.transform.localPosition = new Vector3(0f, 1.1f, -0.5f);
            var sight = sightGo.AddComponent<Light>();
            sight.type = LightType.Point;
            sight.color = new Color(0.7f, 0.78f, 1f);
            // Berke'nin play testi: hâlâ çok parlaktı
            sight.intensity = 0.22f;
            sight.range = 1.1f;
            sight.shadows = LightShadows.None;
            sight.enabled = false;

            // El fenerinin görünen huzmesi: spotun önünde saydam üçgen (spot dönünce o da döner)
            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(spotGo.transform, false);
            beamGo.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
            var beam = beamGo.AddComponent<MeshRenderer>();
            beam.sharedMaterial = BeamMaterial();
            beam.shadowCastingMode = ShadowCastingMode.Off;
            beam.receiveShadows = false;
            beam.enabled = false;

            var flashlight = root.AddComponent<PlayerFlashlight>();
            var flashSo = new SerializedObject(flashlight);
            flashSo.FindProperty("spot").objectReferenceValue = spot;
            flashSo.FindProperty("beam").objectReferenceValue = beam;
            flashSo.FindProperty("nightSight").objectReferenceValue = sight;
            flashSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Spotun yerel +Z yönünde açılan üçgen huzme (3 m, uçta 1,8 m), iki yüzlü</summary>
        private static Mesh BeamMesh()
        {
            const string path = "Assets/Generated/Player/el_feneri_huzme.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.name = "el_feneri_huzme";
            mesh.vertices = new[] { new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0.9f, 3f), new Vector3(0f, -0.9f, 3f) };
            mesh.uv = new[] { new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Karakter kenar ışığı materyali (Deprem/RimLight, eklemeli)</summary>
        private static Material RimMaterial()
        {
            const string path = "Assets/Generated/Player/kenar_isigi.mat";
            var shader = Shader.Find("Deprem/RimLight");
            if (shader == null) throw new System.InvalidOperationException("Deprem/RimLight shader bulunamadı (Assets/Shaders/RimLight.shader)");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>URP Unlit, saydam sıcak beyaz</summary>
        private static Material BeamMaterial()
        {
            const string path = "Assets/Generated/Player/el_feneri_huzme.mat";
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
            mat.SetFloat("_DstBlend", (float)BlendMode.One); // eklemeli: ışık gibi parlar
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", BeamTexture());
            mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.8f, 0.45f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Huzmenin dokusu (Play testi 12: düz koni çirkindi): U boyunca (lensten uca) solar, V'de kenarlara doğru
        /// yumuşak; lensin hemen önü en parlak. Kodla üretilir: Assets/Generated/Player/el_feneri_huzme.png.
        /// </summary>
        private static Texture2D BeamTexture()
        {
            const string path = "Assets/Generated/Player/el_feneri_huzme.png";
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float along = Mathf.Pow(1f - u, 1.4f) * Mathf.Clamp01(u * 12f);   // lensten sonra parlak, uca doğru söner
                float across = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 2f);
                across = across * across * (3f - 2f * across);                     // kenarlar yumuşak
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, along * across));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Assets/Art/Characters/{prefix}_1.png, _2.png ... numara sırasıyla</summary>
        private static void SetFrames(SerializedObject so, string property, Texture2D[] frames)
        {
            var framesProperty = so.FindProperty(property);
            framesProperty.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        /// <summary>
        /// Fenerle koşu karesinde fenerin ucu (piksel, sol üstten): elin önündeki nötr gri fener gövdesinin en sağ
        /// pikseli (gözlük de gri ama başta: üst 30 satır atlanır); y = o sütundaki fener piksellerinin ortası
        /// (lensin ortası). Bulamazsa (-1, -1).
        /// </summary>
        private static Vector2 FindFlashlightTip(Texture2D frame)
        {
            var file = AssetDatabase.GetAssetPath(frame);
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(file))) return new Vector2(-1f, -1f);
            int tipX = -1, minRow = int.MaxValue, maxRow = -1;
            for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
            {
                int row = tex.height - 1 - y; // sol üstten satır
                if (row < 30) continue;
                var c = (Color32)tex.GetPixel(x, y);
                bool grey = c.a > 0 && Mathf.Abs(c.r - c.g) <= 3 && Mathf.Abs(c.g - c.b) <= 3 && c.r >= 90 && c.r <= 230;
                if (!grey || x < tipX) continue;
                if (x > tipX)
                {
                    tipX = x;
                    minRow = maxRow = row;
                }
                else
                {
                    minRow = Mathf.Min(minRow, row);
                    maxRow = Mathf.Max(maxRow, row);
                }
            }
            Object.DestroyImmediate(tex);
            return tipX < 0 ? new Vector2(-1f, -1f) : new Vector2(tipX, (minRow + maxRow) * 0.5f);
        }

        private static Texture2D[] LoadFrames(string prefix)
        {
            const string folder = "Assets/Art/Characters";
            if (!AssetDatabase.IsValidFolder(folder)) return new Texture2D[0];
            return AssetDatabase.FindAssets(prefix + "_ t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, name: Path.GetFileNameWithoutExtension(path)))
                .Where(p => p.name.StartsWith(prefix + "_") && int.TryParse(p.name.Substring(prefix.Length + 1), out _))
                .OrderBy(p => int.Parse(p.name.Substring(prefix.Length + 1)))
                .Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p.path))
                .Where(t => t != null)
                .ToArray();
        }

        // Koşma karelerinde ayak alt kenardan 2 px yukarıda, gövde ortası x ≈ 55 px (pivot 64)
        private const float FrameFootPx = 2f;
        private const float FrameOffsetX = (64f - 55f) / 64f;
        // Karakterin taban rengi çarpanı (okunaklılık)
        private const float CharacterBoost = 1.6f;

        /// <summary>Kare varsa beyaz, saydam kısımları kesilen (alpha clip) Lit; yoksa sarı yer tutucu</summary>
        private static Material PlaceholderMaterial(Texture2D firstFrame)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            bool art = firstFrame != null;
            mat.SetTexture("_BaseMap", firstFrame);
            // Okunaklılık: koyu paletli çizim odanın ışığında arka plana karışıyordu; taban rengi 1'in üstünde
            // (ışığa yine tepki verir, karanlıkta yine kararır; sadece daha parlak yansıtır)
            mat.SetColor("_BaseColor", art ? new Color(CharacterBoost, CharacterBoost, CharacterBoost, 1f) : new Color(0.95f, 0.8f, 0.35f));
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_AlphaClip", art ? 1f : 0f);
            mat.SetFloat("_Cutoff", 0.5f);
            if (art) mat.EnableKeyword("_ALPHATEST_ON");
            else mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = art ? (int)RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupCamera(Scene scene, Transform player)
        {
            var mainCamera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>())
                .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
            if (mainCamera != null && mainCamera.GetComponent<CinemachineBrain>() == null)
                mainCamera.gameObject.AddComponent<CinemachineBrain>();

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == CameraName).ToList())
                Object.DestroyImmediate(old);

            var go = new GameObject(CameraName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = player.position + FollowOffset;

            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.3f;
            lens.FarClipPlane = 100f;
            vcam.Lens = lens;
            vcam.Follow = player;

            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = FollowOffset;
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.6f, 0.4f, 0f);
            follow.TrackerSettings = tracker;

            // Deprem sarsıntısı: şiddetini QuakeDirector ayarlar
            go.AddComponent<Deprem.Quake.QuakeCameraShake>();
            // Ruh fazında ruhu takip et
            var target = go.AddComponent<PlayerCameraTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("player").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (found != null) return found;
            }
            return null;
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
