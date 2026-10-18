using UnityEngine;
using UnityEngine.Rendering;

namespace IronDistrict
{
    /// <summary>Golden-hour lighting, fog and procedural skybox.</summary>
    public class SkyController : MonoBehaviour
    {
        public Light Sun;
        Light fill;
        Material sky;

        public static SkyController Create(Transform parent)
        {
            var go = new GameObject("Sky & Lighting");
            go.transform.SetParent(parent, false);
            return go.AddComponent<SkyController>();
        }

        void Awake()
        {
            QualitySettings.shadowDistance = 170f;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.pixelLightCount = 8;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, 148f, 0f);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.92f, 0.80f);
            Sun.intensity = 1.45f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.82f;
            Sun.shadowBias = 0.04f;
            Sun.shadowNormalBias = 0.35f;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(18f, -40f, 0f);
            fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.66f, 0.85f);
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;

            RenderSettings.sun = Sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.73f, 0.71f, 0.67f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 820f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.56f, 0.63f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.17f);
            RenderSettings.ambientIntensity = 1.15f;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null)
            {
                RenderSettings.skybox = null;
                Camera[] cams = Camera.allCameras;
                foreach (var c in cams)
                {
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = new Color(0.55f, 0.58f, 0.63f);
                }
                return;
            }
            sky = new Material(skyShader);
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.042f);
            sky.SetFloat("_SunSizeConvergence", 4f);
            sky.SetFloat("_AtmosphereThickness", 1.12f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
            sky.SetColor("_GroundColor", new Color(0.40f, 0.37f, 0.33f));
            sky.SetFloat("_Exposure", 1.32f);
            RenderSettings.skybox = sky;

            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Restores the golden-hour gameplay look (after menu mood).</summary>
        public void PlayMood()
        {
            Sun.color = new Color(1f, 0.92f, 0.80f);
            Sun.intensity = 1.45f;
            Sun.transform.rotation = Quaternion.Euler(38f, 148f, 0f);
            fill.intensity = 0.28f;
            RenderSettings.fogColor = new Color(0.73f, 0.71f, 0.67f);
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 820f;
            RenderSettings.ambientIntensity = 1.15f;
            if (sky != null)
            {
                sky.SetFloat("_AtmosphereThickness", 1.12f);
                sky.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
                sky.SetFloat("_Exposure", 1.32f);
            }
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Moodier look for the menu skyline.</summary>
        public void MenuMood()
        {
            Sun.color = new Color(1f, 0.76f, 0.55f);
            Sun.intensity = 1.25f;
            Sun.transform.rotation = Quaternion.Euler(16f, 168f, 0f);
            fill.intensity = 0.2f;
            RenderSettings.fogColor = new Color(0.62f, 0.58f, 0.56f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 520f;
            RenderSettings.ambientIntensity = 0.95f;
            if (sky != null)
            {
                sky.SetFloat("_AtmosphereThickness", 1.5f);
                sky.SetColor("_SkyTint", new Color(0.62f, 0.55f, 0.52f));
                sky.SetFloat("_Exposure", 1.1f);
            }
            DynamicGI.UpdateEnvironment();
        }
    }
}
