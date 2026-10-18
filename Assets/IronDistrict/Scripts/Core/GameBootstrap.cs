using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronDistrict
{
    /// <summary>
    /// Creates the whole game at runtime, so the project works from any scene
    /// (this is what makes drag-and-drop + CI builds foolproof).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameRoot>() != null) return;

            var root = new GameObject("IronDistrict");
            root.AddComponent<GameRoot>();
            Object.DontDestroyOnLoad(root);

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(es);
            }
        }
    }

    /// <summary>Lowers heavy settings once if the machine struggles (keeps laptops playable).</summary>
    public class FpsProbe : MonoBehaviour
    {
        float windowTime;
        int frames;
        float slowTime;
        int level;

        void Update()
        {
            windowTime += Time.unscaledDeltaTime;
            frames++;
            if (windowTime < 1f) return;

            float fps = frames / windowTime;
            windowTime = 0f;
            frames = 0;

            if (fps < 24f) slowTime += 1f;
            else slowTime = Mathf.Max(0f, slowTime - 0.5f);

            if (slowTime >= 4f)
            {
                slowTime = 0f;
                level++;
                if (level == 1)
                {
                    QualitySettings.shadowDistance = Mathf.Min(110f, QualitySettings.shadowDistance);
                    QualitySettings.pixelLightCount = 4;
                    QualitySettings.antiAliasing = 2;
                    RenderSettings.fogEndDistance = 650f;
                    Debug.Log("[IronDistrict] Авто-качество: средний профиль (" + Mathf.RoundToInt(fps) + " FPS)");
                }
                else if (level >= 2)
                {
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowDistance = 70f;
                    QualitySettings.pixelLightCount = 2;
                    QualitySettings.antiAliasing = 0;
                    RenderSettings.fogEndDistance = 480f;
                    Debug.Log("[IronDistrict] Авто-качество: низкий профиль");
                    enabled = false;
                }
            }
        }
    }
}
