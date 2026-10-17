using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Deprem.Visual
{
    /// <summary>
    /// GDD "Post-processing": 3 Volume profili (Assets/Settings/Volumes) ve sahnede fazlara göre geçen
    /// global Volume'lar. Görünüm ayarı bu dosyadaki değerlerden yapılır; profiller her kurulumda güncellenir.
    /// </summary>
    internal static class VolumeSetup
    {
        private const string Folder = "Assets/Settings/Volumes";
        private const string RootName = "PostProcess";

        [MenuItem("Tools/Deprem/Volume Profillerini Kur (açık sahne)")]
        private static void SetupMenu()
        {
            var scene = SceneManager.GetActiveScene();
            SetupInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[VolumeSetup] Volume profilleri kuruldu: " + scene.name);
        }

        public static PhaseVolumes SetupInScene(Scene scene)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Settings", "Volumes");

            var night = Profile("Gece", p =>
            {
                Get<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                var color = Get<ColorAdjustments>(p);
                color.saturation.Override(-5f);
                color.contrast.Override(8f);
                color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
                var bloom = Get<Bloom>(p);
                bloom.intensity.Override(0.6f);
                bloom.threshold.Override(1f);
                var vignette = Get<Vignette>(p);
                vignette.intensity.Override(0.25f);
                vignette.smoothness.Override(0.5f);
                vignette.color.Override(Color.black);
                Get<FilmGrain>(p).intensity.Override(0.1f);
                Get<ChromaticAberration>(p).intensity.Override(0f);
            });

            // Ruh fazı: "Dünya donmuş görünür: soğuk mavi ton, renkler soluk"
            var spirit = Profile("RuhFazi", p =>
            {
                Get<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                var color = Get<ColorAdjustments>(p);
                color.saturation.Override(-50f);
                color.contrast.Override(-5f);
                color.postExposure.Override(0.2f);
                color.colorFilter.Override(new Color(0.6f, 0.8f, 1.25f));
                var bloom = Get<Bloom>(p);
                bloom.intensity.Override(1.1f);
                bloom.threshold.Override(0.9f);
                bloom.tint.Override(new Color(0.7f, 0.85f, 1f));
                var vignette = Get<Vignette>(p);
                vignette.intensity.Override(0.4f);
                vignette.smoothness.Override(0.6f);
                vignette.color.Override(new Color(0.05f, 0.12f, 0.25f));
                Get<FilmGrain>(p).intensity.Override(0.05f);
                Get<ChromaticAberration>(p).intensity.Override(0.2f);
            });

            // Elektrik kesik: karanlık, soğuk ve loş; kırmızı yok (Play testi 2)
            var powerOut = Profile("ElektrikKesik", p =>
            {
                Get<Tonemapping>(p).mode.Override(TonemappingMode.Neutral);
                var color = Get<ColorAdjustments>(p);
                // Gerçekten karanlık: oyuncu yakınını görüş ışığıyla (PlayerFlashlight.nightSight), uzağı el feneriyle görür
                color.saturation.Override(-30f);
                color.contrast.Override(15f);
                color.postExposure.Override(-0.2f);
                color.colorFilter.Override(new Color(0.95f, 0.95f, 0.95f)); // Play testi 27: mavi ton yok, nötr
                var bloom = Get<Bloom>(p);
                bloom.intensity.Override(0.8f);
                bloom.threshold.Override(0.9f);
                var vignette = Get<Vignette>(p);
                vignette.intensity.Override(0.45f);
                vignette.smoothness.Override(0.55f);
                vignette.color.Override(new Color(0.02f, 0.02f, 0.02f));
                Get<FilmGrain>(p).intensity.Override(0.3f);
                Get<ChromaticAberration>(p).intensity.Override(0.05f);
            });
            AssetDatabase.SaveAssets();

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToList())
                Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var phaseVolumes = root.AddComponent<PhaseVolumes>();
            phaseVolumes.Configure(
                AddVolume(root.transform, "Gece", night, 0, 1f),
                AddVolume(root.transform, "RuhFazi", spirit, 2, 0f),
                AddVolume(root.transform, "ElektrikKesik", powerOut, 1, 0f));

            EnablePostProcessing(scene);
            return phaseVolumes;
        }

        private static Volume AddVolume(Transform parent, string name, VolumeProfile profile, int priority, float weight)
        {
            var go = new GameObject("Volume_" + name);
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            volume.priority = priority;
            volume.weight = weight;
            return volume;
        }

        /// <summary>Ana kamerada post-processing açık olmalı; pixel art için kenar yumuşatma kapalı</summary>
        private static void EnablePostProcessing(Scene scene)
        {
            foreach (var cam in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            {
                if (!cam.CompareTag("MainCamera")) continue;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
            }
        }

        /// <summary>Profili bulur ya da oluşturur (GUID değişmesin diye var olanı günceller)</summary>
        private static VolumeProfile Profile(string name, System.Action<VolumeProfile> configure)
        {
            string path = $"{Folder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            configure(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Profildeki bileşeni getirir; yoksa ekler ve alt asset olarak kaydeder</summary>
        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
