using UnityEngine;

namespace IronDistrict
{
    /// <summary>
    /// Filmic colour grading for the built-in pipeline: ACES tonemap, split-tone,
    /// contrast, vignette and grain. Turns the flat procedural look into film.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        public float Exposure = 1.06f;
        public float Saturation = 0.86f;
        public float Contrast = 1.07f;
        public float Vignette = 0.34f;
        public float Grain = 0.028f;
        public float Lift = 0.012f;

        Material mat;
        bool ok = true;

        void Awake()
        {
            var sh = Shader.Find("IronDistrict/PostFX");
            if (sh == null)
            {
                ok = false;
                return;
            }
            mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!ok || mat == null)
            {
                Graphics.Blit(src, dst);
                return;
            }
            mat.SetFloat("_Exposure", Exposure);
            mat.SetFloat("_Saturation", Saturation);
            mat.SetFloat("_Contrast", Contrast);
            mat.SetFloat("_Vignette", Vignette);
            mat.SetFloat("_Grain", Grain);
            mat.SetFloat("_Lift", Lift);
            mat.SetFloat("_Seed", Random.value * 100f);
            Graphics.Blit(src, dst, mat);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
