using UnityEngine;

namespace IronDistrict
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public CameraController Cam;
        public WeaponManager Weapon;
        public PlayerHealth Health;

        CharacterController cc;
        Vector3 vel;
        float vy;
        bool grounded;
        float stepTimer;
        int stepIndex;

        public float WalkSpeed = 6.4f;
        public float SprintSpeed = 9.8f;
        public float AdsSpeed = 4.1f;
        public float Gravity = -24f;
        public float JumpSpeed = 6.6f;

        public bool IsMoving { get; private set; }
        public bool Grounded => grounded;
        public float Speed01 { get; private set; }
        public float VerticalSpeed => vy;

        public static PlayerController Create(Vector3 pos)
        {
            var go = new GameObject("Player");
            go.layer = 2; // Ignore Raycast
            go.transform.position = pos;

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.38f;
            cc.height = 1.82f;
            cc.center = new Vector3(0, 0.94f, 0);
            cc.slopeLimit = 48f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.04f;

            var player = go.AddComponent<PlayerController>();
            var health = go.AddComponent<PlayerHealth>();
            player.Health = health;

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(go.transform, false);
            pivot.transform.localPosition = new Vector3(0, 1.64f, 0);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.045f;
            cam.farClipPlane = 1600f;
            cam.fieldOfView = 68f;
            camGo.AddComponent<AudioListener>();

            var camCtl = pivot.AddComponent<CameraController>();
            camCtl.Body = go.transform;
            camCtl.Cam = cam;
            camCtl.Player = player;
            player.Cam = camCtl;

            var weapon = camGo.AddComponent<WeaponManager>();
            weapon.Cam = cam;
            player.Weapon = weapon;

            return player;
        }

        public void Teleport(Vector3 pos)
        {
            var c = GetComponent<CharacterController>();
            bool was = c.enabled;
            c.enabled = false;
            transform.position = pos;
            c.enabled = was;
            vel = Vector3.zero;
            vy = 0f;
        }

        void Update()
        {
            var root = GameRoot.I;
            if (root == null || root.State != GameRoot.GameState.Playing) return;

            var inp = InputService.I;
            if (inp == null) return;

            bool ads = inp.Ads && !Weapon.Reloading;
            Vector3 wish = transform.forward * inp.Move.y + transform.right * inp.Move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            float targetSpeed = ads ? AdsSpeed : (inp.Sprint && inp.Move.y > 0.1f ? SprintSpeed : WalkSpeed);
            if (Weapon.Reloading) targetSpeed *= 0.92f;
            Vector3 targetVel = wish * targetSpeed;

            float accel = grounded ? 34f : 8f;
            Vector3 horiz = new Vector3(vel.x, 0, vel.z);
            horiz = Vector3.MoveTowards(horiz, targetVel, accel * Time.deltaTime);
            vel.x = horiz.x; vel.z = horiz.z;

            if (grounded && vy < 0f) vy = -2f;
            vy += Gravity * Time.deltaTime;
            if (inp.Jump && grounded && !ads)
            {
                vy = JumpSpeed;
                grounded = false;
            }

            cc.Move(new Vector3(vel.x, vy, vel.z) * Time.deltaTime);
            grounded = cc.isGrounded;

            Speed01 = Mathf.Clamp01(horiz.magnitude / SprintSpeed);
            IsMoving = horiz.magnitude > 0.7f;

            if (grounded && IsMoving)
            {
                stepTimer -= Time.deltaTime * (2.2f + Speed01 * 6.5f);
                if (stepTimer <= 0f)
                {
                    stepTimer = 1f;
                    stepIndex = 1 - stepIndex;
                    Sfx.PlayAt(stepIndex == 0 ? "sfx_step1" : "sfx_step2",
                        transform.position + Vector3.down * 1.4f, 0.32f, 0.9f + Random.value * 0.2f, 25f);
                }
            }
            else
            {
                stepTimer = Mathf.Min(stepTimer, 0.4f);
            }
        }
    }

    /// <summary>First-person look, ADS fov, recoil, head bob and shake.</summary>
    public class CameraController : MonoBehaviour
    {
        public Transform Body;
        public Camera Cam;
        public PlayerController Player;

        float yaw;
        float pitch;
        float recoilPitch;
        float recoilYaw;
        float shake;
        float bobPhase;
        float fovCurrent = 68f;
        bool ads;

        public float BaseFov = 68f;
        public float AdsFov = 47f;

        void Start()
        {
            if (Body != null) yaw = Body.eulerAngles.y;
            fovCurrent = BaseFov;
        }

        public void SetAds(bool value) => ads = value;

        public void SetMenuView(float yawDeg, float pitchDeg)
        {
            yaw = yawDeg;
            pitch = pitchDeg;
            recoilPitch = recoilYaw = 0f;
            shake = 0f;
        }

        public void AddRecoil(float pitchKickDeg, float yawKickDeg)
        {
            recoilPitch += pitchKickDeg;
            recoilYaw += Random.Range(-yawKickDeg, yawKickDeg);
        }

        public void AddShake(float amount) => shake = Mathf.Min(1.2f, shake + amount);

        public void Teleport(Vector3 pos)
        {
            var c = GetComponent<CharacterController>();
            bool was = c.enabled;
            c.enabled = false;
            transform.position = pos;
            c.enabled = was;
            vel = Vector3.zero;
            vy = 0f;
        }

        void Update()
        {
            var root = GameRoot.I;
            bool playing = root != null && root.State == GameRoot.GameState.Playing;
            var inp = InputService.I;

            if (playing && inp != null)
            {
                yaw += inp.Look.x * (ads ? 0.72f : 1f);
                pitch -= inp.Look.y * (ads ? 0.72f : 1f);
                pitch = Mathf.Clamp(pitch, -86f, 86f);
            }

            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 2.4f);

            if (Body != null)
                Body.rotation = Quaternion.Euler(0f, yaw, 0f);

            float dt = Time.deltaTime;
            if (playing && Player != null)
            {
                float moveFactor = Player.IsMoving ? 1f : 0.25f;
                bobPhase += dt * (3.2f + Player.Speed01 * 7.5f) * moveFactor;
            }
            else
            {
                bobPhase += dt * 1.2f;
            }

            float bobAmp = (Player != null ? 0.014f + Player.Speed01 * 0.02f : 0.012f) * (Player != null && Player.Grounded ? 1f : 0.3f);
            float bobX = Mathf.Sin(bobPhase) * bobAmp;
            float bobY = Mathf.Cos(bobPhase * 2f) * bobAmp * 0.8f;
            float idleY = Mathf.Sin(Time.time * 1.3f) * 0.0022f;

            float sx = (Mathf.PerlinNoise(Time.time * 17f, 0.3f) - 0.5f) * shake * 1.6f;
            float sy = (Mathf.PerlinNoise(0.7f, Time.time * 13f) - 0.5f) * shake * 1.6f;

            transform.localPosition = new Vector3(bobX, bobY + idleY, 0f);
            transform.localRotation = Quaternion.Euler(
                pitch - recoilPitch + bobY * 35f + sy,
                recoilYaw + sx,
                -bobX * 25f);

            float targetFov = ads ? AdsFov : BaseFov;
            fovCurrent = Mathf.Lerp(fovCurrent, targetFov, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            if (Cam != null) Cam.fieldOfView = fovCurrent;
        }
    }
}
