using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deprem.HouseBuilder
{
    /// <summary>
    /// Outro mezarlığı (ENTEGRASYON "Outro (mezarlık)"): evin çok solunda, kameranın oyun boyunca hiç görmediği yerde.
    /// Mezarlik.png (2006 x 784, 100 px = 1 m) arka plan; elinde gülle yürüyen karakter (128 px kare, 64 px = 1 m);
    /// dört mezara bırakılacak güller (başta gizli). Akışı OutroCinematic oynatır.
    /// </summary>
    internal static class OutroGraveyard
    {
        private const string MaterialFolder = "Assets/Generated/House/Materials";
        private const string BackdropPath = "Assets/Art/Decor/mezarlik.png";
        private const string RosePath = "Assets/Art/Decor/gul.png";
        private const string CharacterFolder = "Assets/Art/Characters";

        // Görselin ölçüleri ve mezarlar (piksel, sol üstten)
        private const float ImageWidth = 2006f, ImageHeight = 784f, PixelsPerMeter = 100f;
        private static readonly float[] GraveColumns = { 1215f, 1364f, 1513f, 1878f };
        private const float WalkRow = 600f;        // karakterin ayakları (mezarların önü)
        private const float RoseRow = 545f;        // tümseğin önü
        private const float FirstFrameRight = 1700f; // ilk kadrajın sağ kenarı: 4. mezar (1845'ten başlar) dışarıda

        private static readonly string[] BubbleKeys =
            { "outro.grave.father", "outro.grave.mother", "outro.grave.sibling", "outro.grave.self" };

        // Ev x 0..33; mezarlık görseli bunun çok solunda
        private static readonly Vector3 Origin = new Vector3(-100f, 0f, 0f);

        public static void Build(Transform parent)
        {
            var root = new GameObject("OutroGraveyard").transform;
            root.SetParent(parent, false);
            root.position = Origin;

            // Arka plan: aydınlatmasız, görsel olduğu gibi
            float width = ImageWidth / PixelsPerMeter, height = ImageHeight / PixelsPerMeter;
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(root, false);
            backdrop.transform.localPosition = new Vector3(0f, height * 0.5f, 2f);
            backdrop.transform.localScale = new Vector3(width, height, 1f);
            var backdropRenderer = backdrop.GetComponent<MeshRenderer>();
            backdropRenderer.sharedMaterial = UnlitMaterial("mezarlik", BackdropPath, transparent: false);
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;

            // Karakter: ayakları pivotta, 2 x 2 m kare (128 px, alt 2 satır boş)
            var actor = new GameObject("Actor").transform;
            actor.SetParent(root, false);
            actor.localPosition = new Vector3(X(0f), Y(WalkRow), 0f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Visual";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(actor, false);
            quad.transform.localPosition = new Vector3(0f, 1f - 2f / 64f, 0f);
            quad.transform.localScale = new Vector3(2f, 2f, 1f);
            var actorRenderer = quad.GetComponent<MeshRenderer>();
            actorRenderer.sharedMaterial = UnlitMaterial("outro_karakter", $"{CharacterFolder}/oyuncu_gul_yurume_1.png", transparent: true);
            actorRenderer.shadowCastingMode = ShadowCastingMode.Off;
            actorRenderer.receiveShadows = false;
            var bubble = actor.gameObject.AddComponent<Deprem.Player.SpeechBubble>();
            var bubbleSo = new SerializedObject(bubble);
            bubbleSo.FindProperty("font").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/DepremBalon-Regular.ttf");
            bubbleSo.FindProperty("headHeight").floatValue = 1.95f;
            bubbleSo.ApplyModifiedPropertiesWithoutUndo();

            // Güller: tümseğin önünde yatık (sap sağa)
            var roseMaterial = UnlitMaterial("outro_gul", RosePath, transparent: true);
            var rose = AssetDatabase.LoadAssetAtPath<Texture2D>(RosePath);
            float roseHeight = rose != null ? rose.height / 80f : 0.38f;
            float roseWidth = rose != null ? rose.width / 80f : 0.34f;

            var outro = root.gameObject.AddComponent<Deprem.Quake.OutroCinematic>();
            var so = new SerializedObject(outro);
            so.FindProperty("actor").objectReferenceValue = actor;
            so.FindProperty("actorRenderer").objectReferenceValue = actorRenderer;
            so.FindProperty("bubble").objectReferenceValue = bubble;
            var graves = so.FindProperty("graves");
            graves.arraySize = GraveColumns.Length;
            for (int i = 0; i < GraveColumns.Length; i++)
            {
                var roseQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                roseQuad.name = "Rose_" + i;
                Object.DestroyImmediate(roseQuad.GetComponent<Collider>());
                roseQuad.transform.SetParent(root, false);
                roseQuad.transform.localPosition = new Vector3(X(GraveColumns[i]) - 0.05f, Y(RoseRow), 1f);
                roseQuad.transform.localRotation = Quaternion.Euler(0f, 0f, 48f);
                roseQuad.transform.localScale = new Vector3(roseWidth, roseHeight, 1f);
                var roseRenderer = roseQuad.GetComponent<MeshRenderer>();
                roseRenderer.sharedMaterial = roseMaterial;
                roseRenderer.shadowCastingMode = ShadowCastingMode.Off;
                roseRenderer.receiveShadows = false;

                var grave = graves.GetArrayElementAtIndex(i);
                grave.FindPropertyRelative("x").floatValue = Origin.x + X(GraveColumns[i]);
                grave.FindPropertyRelative("rose").objectReferenceValue = roseQuad;
                grave.FindPropertyRelative("bubbleKey").stringValue = BubbleKeys[i];
            }
            so.FindProperty("backdropEdges").vector2Value = new Vector2(Origin.x - width * 0.5f, Origin.x + width * 0.5f);
            so.FindProperty("backdropCenterY").floatValue = Origin.y + height * 0.5f;
            so.FindProperty("backdropHalfHeight").floatValue = height * 0.5f;
            so.FindProperty("firstFrameRight").floatValue = Origin.x + X(FirstFrameRight);
            so.FindProperty("walkY").floatValue = Origin.y + Y(WalkRow);
            SetFrames(so.FindProperty("walkFrames"), "oyuncu_gul_yurume_", 8);
            SetFrames(so.FindProperty("bendFrames"), "oyuncu_gul_koyma_", 5);
            SetFrames(so.FindProperty("bendEmptyFrames"), "oyuncu_koyma_", 5);
            SetFrames(so.FindProperty("idleFrames"), "oyuncu_bekleme_", 2);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float X(float column) => column / PixelsPerMeter - ImageWidth / PixelsPerMeter * 0.5f;
        private static float Y(float row) => (ImageHeight - row) / PixelsPerMeter;

        private static void SetFrames(SerializedProperty list, string prefix, int count)
        {
            var frames = Enumerable.Range(1, count)
                .Select(i => AssetDatabase.LoadAssetAtPath<Texture2D>($"{CharacterFolder}/{prefix}{i}.png"))
                .Where(t => t != null).ToArray();
            list.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            if (frames.Length < count) Debug.LogWarning($"[OutroGraveyard] {prefix}: {frames.Length}/{count} kare bulundu");
        }

        /// <summary>URP Unlit; saydamsa alfa harmanlı (karakter silinirken solar)</summary>
        private static Material UnlitMaterial(string name, string texturePath, bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetColor("_BaseColor", Color.white);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Geometry;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
