using System.Linq;
using Deprem.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Deprem.Family
{
    /// <summary>
    /// Aile sistemini prefab olarak (Assets/Prefabs/Mehmet/FamilySystem.prefab) sahneye koyar:
    /// FamilyDirector + baba, anne, kardeş. Üyeler kurulumda ve çalışma anında kendi Marker_* noktalarına yerleşir.
    /// </summary>
    internal static class FamilySetup
    {
        public const string PrefabPath = "Assets/Prefabs/Mehmet/FamilySystem.prefab";
        private const string MaterialFolder = "Assets/Generated/Family";
        private const string SystemName = "FamilySystem";

        private struct MemberDef
        {
            public string Name;
            public FamilyMemberId Id;
            public string Marker;
            public ItemId Danger;
            public BagItemId Bag;
            public BagItemId CabinBag;
            public bool Injured;
            public float Height;
            public Color Color;
        }

        // GDD "Artçılar ve kurtarma" ve "Sabitlenebilir eşyalar"
        private static readonly MemberDef[] Members =
        {
            new MemberDef { Name = "Father", Id = FamilyMemberId.Father, Marker = "Marker_Father", Danger = ItemId.Bookcase,
                // Berke'nin play testi: kayayı kaldırmak için çanta şart değil; kan kaybı kabinde ortaya çıkar (ilk yardım)
                Bag = BagItemId.None, CabinBag = BagItemId.FirstAidKit, Injured = true, Height = 1.8f, Color = new Color(0.35f, 0.5f, 0.85f) },
            new MemberDef { Name = "Mother", Id = FamilyMemberId.Mother, Marker = "Marker_Mother", Danger = ItemId.Wardrobe,
                Bag = BagItemId.None, Injured = false, Height = 1.65f, Color = new Color(0.85f, 0.4f, 0.45f) },
            new MemberDef { Name = "Sibling", Id = FamilyMemberId.Sibling, Marker = "Marker_Sibling", Danger = ItemId.ToyShelf,
                Bag = BagItemId.Flashlight, Injured = false, Height = 1.1f, Color = new Color(0.45f, 0.8f, 0.45f) },
        };

        [MenuItem("Tools/Deprem/Aile Sistemini Kur (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            SetupInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[FamilySetup] Aile sistemi kuruldu: " + scene.name);
        }

        public static GameObject SetupInScene(Scene scene)
        {
            var prefab = CreateOrUpdatePrefab();
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == SystemName).ToList())
                Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = SystemName;

            // Çalışma anında da yerleşirler; editörde de yerlerinde görünsünler, (0, 0, 0)'da evin solunda değil
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var def in Members)
            {
                var member = instance.transform.Find(def.Name);
                var marker = transforms.FirstOrDefault(t => t.name == def.Marker);
                if (member != null && marker != null) member.position = marker.position;
            }
            return instance;
        }

        private static GameObject CreateOrUpdatePrefab()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Generated", "Family");

            var root = new GameObject(SystemName);
            root.AddComponent<FamilyDirector>();
            foreach (var def in Members) BuildMember(def, root.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildMember(MemberDef def, Transform parent)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);

            // Görsel kök ayakta: yatarken ve çömelirken ayak hizasından döner/ölçeklenir
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Placeholder";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(visual.transform, false);
            quad.GetComponent<MeshFilter>().sharedMesh = Deprem.Player.CharacterQuadMesh.Get(); // tavan lambalarının ışığını alsın
            quad.transform.localPosition = new Vector3(0f, def.Height * 0.5f, 0f);
            quad.transform.localScale = new Vector3(0.55f * def.Height / 1.7f, def.Height, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(def.Name.ToLowerInvariant(), def.Color);
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            renderer.receiveShadows = false; // kendi gölgesi yüzüne düşmesin (lamba arkada)

            // Kurtarma fazında (karanlık) üyeyi seçtiren hafif sıcak ışık, önde ve göğüs hizasında
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, def.Height * 0.55f, -0.5f);
            var glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.65f);
            glow.intensity = 0.8f;
            glow.range = 1.8f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            // Seslenme balonu: başının biraz üstünde
            var bubble = go.AddComponent<Deprem.Player.SpeechBubble>();
            Deprem.Player.PlayerSetup.ConfigureBubble(bubble);
            var bubbleSo = new SerializedObject(bubble);
            bubbleSo.FindProperty("headHeight").floatValue = def.Height + 0.35f;
            bubbleSo.ApplyModifiedPropertiesWithoutUndo();

            var member = go.AddComponent<FamilyMember>();
            var so = new SerializedObject(member);
            so.FindProperty("glow").objectReferenceValue = glow;
            so.FindProperty("id").enumValueIndex = (int)def.Id;
            so.FindProperty("homeMarker").stringValue = def.Marker;
            so.FindProperty("dangerItem").enumValueIndex = (int)def.Danger;
            so.FindProperty("requiredBagItem").enumValueIndex = (int)def.Bag;
            so.FindProperty("cabinBagItem").enumValueIndex = (int)def.CabinBag;
            so.FindProperty("injuredAfterQuake").boolValue = def.Injured;
            // Yaralıyken ayağındaki kaya: deprem molozunun materyali (QuakeSetup üretir), doku çalışma anında
            so.FindProperty("rockMaterial").objectReferenceValue =
                def.Injured ? AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Quake/siva.mat") : null;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Material(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}_placeholder.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
