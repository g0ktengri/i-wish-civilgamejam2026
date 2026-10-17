using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deprem.Minigames
{
    // M37 (Mehmet'in play testi 22: minigame grafikleri oyunla hiç alakalı değildi): minigame açılırken oyundaki gerçek
    // eşya ve odası ana kamerayla yakından çekilir. Arka plan = eşyanın önünden odanın görüntüsü (oyunun ışığı,
    // renkleri, Volume'u); taşı-yerleştir'de sürüklenen şey = eşyanın kendi 3D modelinin görüntüsü (arka planı
    // saydam). İkisi de oyunun piksel ölçüsünde (düşük çözünürlük, Point filtre).
    public static class MinigameCloseUp
    {
        const int IsolationLayer = 30;
        static readonly Color KeyColor = new Color(1f, 0f, 1f, 1f);

        static bool IsOutline(Renderer r)
        {
            var m = r.sharedMaterial;
            if (!m) return false;
            string n = m.name.ToLowerInvariant();
            return n.Contains("kontur") || n.Contains("outline");
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            bool any = false;
            var bounds = new Bounds(go.transform.position, Vector3.one * 0.5f);
            foreach (var r in renderers)
            {
                if (!r.enabled || r is ParticleSystemRenderer || IsOutline(r)) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }
            return bounds;
        }

        /// <summary>Odanın, odak kutusunun önünden görüntüsü (hide: çekimde gizlenecek eşya, ör. taşınan raf)</summary>
        public static RenderTexture RenderScene(Bounds focus, Vector2 panelSize, float pixelsPerTexel, float margin,
            GameObject hide)
        {
            var cam = Camera.main;
            if (!cam) return null;
            int w = Mathf.Max(16, Mathf.RoundToInt(panelSize.x / pixelsPerTexel));
            int h = Mathf.Max(16, Mathf.RoundToInt(panelSize.y / pixelsPerTexel));
            var rt = new RenderTexture(w, h, 24) { name = "MinigameCloseUp", filterMode = FilterMode.Point };

            Renderer[] hidden = hide ? hide.GetComponentsInChildren<Renderer>() : new Renderer[0];
            var wasEnabled = new bool[hidden.Length];
            for (int i = 0; i < hidden.Length; i++)
            {
                wasEnabled[i] = hidden[i].enabled;
                hidden[i].enabled = false;
            }
            Shoot(cam, focus, w / (float)h, margin, rt, cam.cullingMask, null);
            for (int i = 0; i < hidden.Length; i++) hidden[i].enabled = wasEnabled[i];
            return rt;
        }

        /// <summary>Eşyanın yalnız kendisi, arkası saydam sprite (en fazla maxSize referans pikseli)</summary>
        public static Sprite RenderObject(GameObject go, Vector2 maxSize, float pixelsPerTexel)
        {
            var cam = Camera.main;
            if (!cam || !go) return null;
            var bounds = BoundsOf(go);
            float aspect = Mathf.Max(0.2f, bounds.size.x / Mathf.Max(0.01f, bounds.size.y));
            float boxAspect = maxSize.x / maxSize.y;
            Vector2 size = aspect > boxAspect ? new Vector2(maxSize.x, maxSize.x / aspect) : new Vector2(maxSize.y * aspect, maxSize.y);
            int w = Mathf.Max(8, Mathf.RoundToInt(size.x / pixelsPerTexel));
            int h = Mathf.Max(8, Mathf.RoundToInt(size.y / pixelsPerTexel));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;

            // Eşyanın renderer'ları geçici olarak ayrı katmanda: kamera yalnız onları görür. Ruh fazının sarı konturu
            // (kontur materyali) çekimde gizlenir
            var renderers = go.GetComponentsInChildren<Renderer>();
            var layers = new int[renderers.Length];
            var enabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                layers[i] = renderers[i].gameObject.layer;
                enabled[i] = renderers[i].enabled;
                renderers[i].gameObject.layer = IsolationLayer;
                if (IsOutline(renderers[i]) || renderers[i] is ParticleSystemRenderer) renderers[i].enabled = false;
            }
            Shoot(cam, bounds, w / (float)h, 1.05f, rt, 1 << IsolationLayer, KeyColor);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].gameObject.layer = layers[i];
                renderers[i].enabled = enabled[i];
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "MinigameItem", filterMode = FilterMode.Point };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // Mor zemini (ve kenardaki mor karışımlı pikselleri) saydam yap
            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.r > 0.55f && p.b > 0.55f && p.g < 0.4f) pixels[i] = Color.clear;
                else pixels[i].a = 1f;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        // Ana kamerayı kısa süre odağın önüne alıp çeker, her şeyi geri koyar (aynı karede; oyuncu görmez)
        static void Shoot(Camera cam, Bounds focus, float aspect, float margin, RenderTexture target, int cullingMask,
            Color? clearColor)
        {
            var t = cam.transform;
            var position = t.position;
            var rotation = t.rotation;
            var previousTarget = cam.targetTexture;
            int previousMask = cam.cullingMask;
            var previousClear = cam.clearFlags;
            var previousBackground = cam.backgroundColor;
            var data = cam.GetUniversalAdditionalCameraData();
            bool previousPost = data != null && data.renderPostProcessing;

            float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var e = focus.extents;
            float distance = Mathf.Max(e.y / Mathf.Tan(halfFov), e.x / (Mathf.Tan(halfFov) * aspect)) * margin + e.z;
            t.SetPositionAndRotation(focus.center + Vector3.back * distance, Quaternion.identity);
            cam.targetTexture = target;
            cam.cullingMask = cullingMask;
            if (clearColor.HasValue)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = clearColor.Value;
                if (data != null) data.renderPostProcessing = false; // mor zemin renk ayarından geçmesin
            }
            cam.Render();

            cam.targetTexture = previousTarget;
            cam.cullingMask = previousMask;
            cam.clearFlags = previousClear;
            cam.backgroundColor = previousBackground;
            if (data != null) data.renderPostProcessing = previousPost;
            t.SetPositionAndRotation(position, rotation);
        }
    }
}
