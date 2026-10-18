using UnityEngine;

namespace IronDistrict
{
    /// <summary>
    /// Central input wrapper (legacy Input Manager) with hard fallbacks so movement
    /// works even if the project's Input axes are missing or broken.
    /// </summary>
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
        float graceUntil;

        void Awake() { I = this; }

        /// <summary>Ignores fire/jump for a moment (used right after the menu click).</summary>
        public void ArmGrace(float seconds) => graceUntil = Time.time + seconds;

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

            // keyboard is authoritative when the axes are dead/broken
            Vector2 fromAxes = new Vector2(Axis("Horizontal", true), Axis("Vertical", true));
            Vector2 fromKeys = KeyboardMove();
            Move = fromAxes.sqrMagnitude > 0.0001f ? fromAxes : fromKeys;
            if (Move.sqrMagnitude > 1f) Move.Normalize();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float sens = 1.9f * Sensitivity;
                Vector2 fromMouseAxes = new Vector2(Axis("Mouse X") * sens, Axis("Mouse Y") * sens);
                if (fromMouseAxes.sqrMagnitude > 0.000001f)
                {
                    Look = fromMouseAxes;
                    hasLastMouse = false;
                }
                else
                {
                    // last-resort: direct mouse delta
                    Vector2 mp = Input.mousePosition;
                    Look = hasLastMouse ? (mp - lastMouse) * 0.12f * Sensitivity : Vector2.zero;
                    lastMouse = mp;
                    hasLastMouse = true;
                }
            }
            else
            {
                Look = Vector2.zero;
                hasLastMouse = false;
            }

            bool grace = Time.time < graceUntil;
            Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Jump = !grace && Input.GetKeyDown(KeyCode.Space);
            Fire = !grace && Input.GetMouseButton(0);
            Ads = !grace && Input.GetMouseButton(1);
            ReloadPressed = Input.GetKeyDown(KeyCode.R);
        }
    }
}
