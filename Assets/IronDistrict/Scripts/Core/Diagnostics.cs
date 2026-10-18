using UnityEngine;

namespace IronDistrict
{
    /// <summary>
    /// IMGUI overlay: proves the game is running (state, input, position, FPS) and
    /// shows fatal generation errors even if the uGUI font is missing.
    /// IMGUI uses the built-in skin, so it works when everything else fails.
    /// </summary>
    public class Diagnostics : MonoBehaviour
    {
        public static Diagnostics I;

        public string FatalError;
        public bool ShowFirstSeconds = true;
        float hideAt;
        float fpsSmoothed = 60f;

        void Awake()
        {
            I = this;
            hideAt = Time.unscaledTime + 14f;
        }

        public void ReportFatal(string message)
        {
            FatalError = message;
            Debug.LogError("[IronDistrict] FATAL: " + message);
        }

        void Update()
        {
            float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / dt, 0.05f);
        }

        void OnGUI()
        {
            var root = GameRoot.I;
            bool generating = root != null && root.State == GameRoot.GameState.Generating;

            bool visible = FatalError != null || generating ||
                           (ShowFirstSeconds && Time.unscaledTime < hideAt);
            if (!visible) return;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.UpperLeft,
                richText = false
            };
            style.normal.textColor = new Color(0.92f, 0.94f, 0.9f);

            string state = root != null ? root.State.ToString() : "no GameRoot";
            var p = root != null ? root.Player : null;
            Vector3 pos = p != null ? p.transform.position : Vector3.zero;
            var inp = InputService.I;

            string text =
                $"IRON DISTRICT · {state}\n" +
                $"FPS {fpsSmoothed:0}\n" +
                $"POS {pos.x:0.0} {pos.y:0.0} {pos.z:0.0}\n" +
                (inp != null
                    ? $"MOVE {inp.Move.x:0.0} {inp.Move.y:0.0}   LOOK {inp.Look.x:0.00} {inp.Look.y:0.00}\n" +
                      $"CURSOR {(Cursor.lockState == CursorLockMode.Locked ? "LOCKED" : "free")}\n"
                    : "") +
                (generating ? "ГЕНЕРАЦИЯ МИРА...\n" : "") +
                (root != null && root.State == GameRoot.GameState.Playing && Time.unscaledTime < hideAt + 6f
                    ? "WASD — движение · мышь/стрелки — обзор\nЛКМ — огонь · ПКМ — прицел · ESC — пауза\n"
                    : "");

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(10, 10, 430, 150), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(20, 16, 420, 140), text, style);

            if (FatalError != null)
            {
                var err = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                err.normal.textColor = new Color(1f, 0.55f, 0.45f);
                GUI.color = new Color(0.1f, 0f, 0f, 0.8f);
                GUI.DrawTexture(new Rect(10, Screen.height - 150, Screen.width - 20, 130), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(24, Screen.height - 140, Screen.width - 48, 110),
                    "ОШИБКА ГЕНЕРАЦИИ МИРА:\n" + FatalError +
                    "\n\nПришли этот текст разработчику — исправлю.", err);
            }
        }
    }
}
