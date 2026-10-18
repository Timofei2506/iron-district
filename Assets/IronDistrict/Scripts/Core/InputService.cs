using UnityEngine;

namespace IronDistrict
{
    /// <summary>Central input wrapper (legacy Input Manager) with safe fallbacks.</summary>
    public class InputService : MonoBehaviour
    {
        public static InputService I;

        public Vector2 Move;
        public Vector2 Look;
        public bool Sprint;
        public bool Jump;
        public bool Fire;
        public bool Ads;
        public bool ReloadPressed;
        public bool PausePressed;

        public static float Sensitivity = 1.0f;

        static bool axesAvailable = true;
        static bool loggedAxisWarning;
        Vector2 lastMouse;
        bool hasLastMouse;

        void Awake() { I = this; }

        static float Axis(string name, bool raw = false)
        {
            if (!axesAvailable) return 0f;
            try
            {
                return raw ? Input.GetAxisRaw(name) : Input.GetAxis(name);
            }
            catch (System.Exception)
            {
                axesAvailable = false;
                if (!loggedAxisWarning)
                {
                    loggedAxisWarning = true;
                    Debug.LogWarning("[IronDistrict] Input axes unavailable — falling back to direct keys/mouse.");
                }
                return 0f;
            }
        }

        static Vector2 KeyboardMove()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            return new Vector2(x, y);
        }

        void Update()
        {
            PausePressed = Input.GetKeyDown(KeyCode.Escape);

            bool playing = GameRoot.I != null && GameRoot.I.State == GameRoot.GameState.Playing;
            if (!playing)
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                Sprint = Jump = Fire = Ads = false;
                ReloadPressed = false;
                hasLastMouse = false;
                return;
            }

            Move = new Vector2(Axis("Horizontal", true), Axis("Vertical", true));
            if (!axesAvailable) Move = KeyboardMove();
            if (Move.sqrMagnitude > 1f) Move.Normalize();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float sens = 1.9f * Sensitivity;
                if (axesAvailable)
                {
                    Look = new Vector2(Axis("Mouse X") * sens, Axis("Mouse Y") * sens);
                }
                else
                {
                    // last-resort fallback: raw mouse delta (works if cursor isn't locked by the OS)
                    Vector2 mp = Input.mousePosition;
                    if (hasLastMouse) Look = (mp - lastMouse) * 0.12f * Sensitivity;
                    else Look = Vector2.zero;
                    lastMouse = mp;
                    hasLastMouse = true;
                }
            }
            else
            {
                Look = Vector2.zero;
                hasLastMouse = false;
            }

            Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Jump = Input.GetKeyDown(KeyCode.Space);
            Fire = Input.GetMouseButton(0);
            Ads = Input.GetMouseButton(1);
            ReloadPressed = Input.GetKeyDown(KeyCode.R);
        }
    }
}
