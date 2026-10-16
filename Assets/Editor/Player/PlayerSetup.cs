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

            // Görsel kök ayakta: ölçek (yön çevirme, eğilme) ayak hizasından uygulanır
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Placeholder";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(visual.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            quad.transform.localScale = new Vector3(0.6f, 1.7f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PlaceholderMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;

            var playerVisual = root.AddComponent<PlayerVisual>();
            var so = new SerializedObject(playerVisual);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material PlaceholderMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetColor("_BaseColor", new Color(0.95f, 0.8f, 0.35f));
            mat.SetFloat("_Smoothness", 0.1f);
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

            // Deprem sarsıntısı (GDD: Cinemachine Impulse) Quake sistemi tarafından tetiklenecek
            go.AddComponent<CinemachineImpulseListener>();
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
