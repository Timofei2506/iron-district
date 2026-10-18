using UnityEngine;

namespace IronDistrict
{
    public enum FireMode { Auto, Semi }

    /// <summary>First-person rifle: procedural viewmodel, hitscan, recoil, reload, tracers.</summary>
    public class WeaponManager : MonoBehaviour
    {
        public Camera Cam;

        [Header("Numbers")]
        public float Damage = 24f;
        public float FireRate = 9.2f;
        public int MagSize = 30;
        public int ReserveStart = 150;
        public int ReserveMax = 240;
        public float Range = 320f;
        public float ReloadTime = 1.85f;
        public FireMode Mode = FireMode.Auto;

        [Header("Feel")]
        public float Spread = 0.65f;
        public float AdsSpreadMul = 0.42f;
        public float BloomPerShot = 0.30f;
        public float RecoilKick = 1.15f;

        public int AmmoInMag { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadProgress => Reloading ? Mathf.Clamp01((Time.time - reloadStart) / ReloadTime) : 0f;
        public bool IsAds { get; private set; }

        Transform gunRoot;
        Transform muzzle;
        GameObject muzzleFlash;
        Light flashLight;
        Transform magTransform;
        Vector3 magHome;
        AudioSource audioSource;
        float nextShotTime;
        float reloadStart;
        float bloom;
        float kickBack;
        float swayX, swayY;
        bool semiLatch;

        static readonly int RayLayerMask = Physics.DefaultRaycastLayers & ~(1 << 2); // ignore Player layer

        void Awake()
        {
            AmmoInMag = MagSize;
            Reserve = ReserveStart;
            BuildViewModel();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            audioSource.dopplerLevel = 0f;
        }

        void BuildViewModel()
        {
            gunRoot = new GameObject("ViewModel").transform;
            gunRoot.SetParent(transform, false);
            gunRoot.localPosition = new Vector3(0.24f, -0.215f, 0.34f);

            var dark = AssetLibrary.DarkMetal();
            var accent = AssetLibrary.AccentMetal();
            var bodyMat = AssetLibrary.Solid("gunBody", new Color(0.16f, 0.165f, 0.17f), 0.55f, 0.42f);

            var mb = new MeshBuilder();
            mb.Submesh = 0;
            // receiver
            mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.075f, 0.1f, 0.46f), 0f, 0.5f);
            // handguard
            mb.AddBox(new Vector3(0, 0.005f, 0.32f), new Vector3(0.066f, 0.076f, 0.24f), 0f, 0.5f);
            // barrel + muzzle brake (along +Z)
            mb.AddBox(new Vector3(0, 0.012f, 0.53f), new Vector3(0.028f, 0.028f, 0.2f), 0f, 0.5f);
            mb.AddBox(new Vector3(0, 0.012f, 0.64f), new Vector3(0.042f, 0.042f, 0.05f), 0f, 0.5f);
            // sight rail
            mb.AddBox(new Vector3(0, 0.06f, 0.02f), new Vector3(0.03f, 0.02f, 0.3f), 0f, 0.5f);
            // front sight
            mb.AddBox(new Vector3(0, 0.088f, 0.3f), new Vector3(0.012f, 0.036f, 0.03f), 0f, 0.5f);
            // stock
            mb.AddBox(new Vector3(0, -0.005f, -0.33f), new Vector3(0.062f, 0.09f, 0.22f), 0f, 0.5f);
            mb.AddBox(new Vector3(0, -0.03f, -0.45f), new Vector3(0.05f, 0.13f, 0.04f), 0f, 0.5f);
            // grip
            mb.AddBox(new Vector3(0, -0.105f, -0.14f), new Vector3(0.05f, 0.12f, 0.07f), Quaternion.Euler(-18f, 0, 0), 0.5f);
            // magazine well
            mb.AddBox(new Vector3(0, -0.085f, 0.05f), new Vector3(0.055f, 0.075f, 0.12f), 0f, 0.5f);
            // rear sight ring (two posts)
            mb.AddBox(new Vector3(-0.017f, 0.083f, -0.03f), new Vector3(0.01f, 0.03f, 0.02f), 0f, 0.5f);
            mb.AddBox(new Vector3(0.017f, 0.083f, -0.03f), new Vector3(0.01f, 0.03f, 0.02f), 0f, 0.5f);
            Geo.MeshObj("Gun", gunRoot, mb.Build("gun"), new[] { bodyMat });

            // magazine (animated on reload)
            var mag = Geo.Obj("Magazine", gunRoot, new Vector3(0, -0.145f, 0.05f), Quaternion.Euler(8f, 0, 0));
            var mbMag = new MeshBuilder();
            mbMag.AddBox(Vector3.zero, new Vector3(0.048f, 0.16f, 0.085f), 0f, 0.5f);
            mbMag.AddBox(new Vector3(0, -0.088f, 0.012f), new Vector3(0.05f, 0.03f, 0.1f), 0f, 0.5f);
            Geo.MeshObj("MagMesh", mag.transform, mbMag.Build("mag"), new[] { dark });
            magTransform = mag.transform;
            magHome = mag.transform.localPosition;

            // accent stripe
            var stripe = new MeshBuilder();
            stripe.AddBox(new Vector3(0, 0.028f, 0.32f), new Vector3(0.068f, 0.012f, 0.18f), 0f, 0.5f);
            Geo.MeshObj("Accent", gunRoot, stripe.Build("accent"), new[] { accent });

            // muzzle + flash
            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(gunRoot, false);
            muzzleGo.transform.localPosition = new Vector3(0, 0.012f, 0.655f);
            muzzle = muzzleGo.transform;

            muzzleFlash = new GameObject("MuzzleFlash");
            muzzleFlash.transform.SetParent(muzzle, false);
            var mf = muzzleFlash.AddComponent<MeshFilter>();
            var mfm = new MeshBuilder();
            mfm.AddQuad(Vector3.zero, Vector3.right, Vector3.up, 1f, 1f, 1f);
            var q2 = mfm.Build("flash");
            muzzleFlash.transform.localScale = Vector3.one * 0.2f;
            mf.sharedMesh = q2;
            var mr = muzzleFlash.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetLibrary.Unlit("flash", new Color(1f, 0.8f, 0.35f), AssetLibrary.Tex("MuzzleFlash"), true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            muzzleFlash.SetActive(false);

            var flGo = new GameObject("FlashLight");
            flGo.transform.SetParent(muzzle, false);
            flashLight = flGo.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 7f;
            flashLight.intensity = 0f;
            flashLight.color = new Color(1f, 0.78f, 0.45f);
            flashLight.shadows = LightShadows.None;
        }

        void Update()
        {
            var root = GameRoot.I;
            bool playing = root != null && root.State == GameRoot.GameState.Playing;
            if (!playing)
            {
                muzzleFlash.SetActive(false);
                if (flashLight) flashLight.intensity = 0f;
                IsAds = false;
                if (root != null && root.Player != null && root.Player.Cam != null)
                    root.Player.Cam.SetAds(false);
                return;
            }

            var inp = InputService.I;
            if (inp == null) return;

            float dt = Time.deltaTime;
            bloom = Mathf.MoveTowards(bloom, 0f, dt * 4.2f);
            flashLight.intensity = Mathf.MoveTowards(flashLight.intensity, 0f, dt * 60f);
            if (flashLight.intensity <= 0.01f && muzzleFlash.activeSelf) muzzleFlash.SetActive(false);

            // ----- reload trigger
            if (inp.ReloadPressed && !Reloading && AmmoInMag < MagSize && Reserve > 0)
                StartCoroutine(ReloadRoutine());

            // ----- fire
            if (Mode == FireMode.Semi)
            {
                if (!inp.Fire) semiLatch = false;
            }
            bool wantShot = Mode == FireMode.Auto ? inp.Fire : (inp.Fire && !semiLatch);
            if (wantShot && !Reloading && Time.time >= nextShotTime)
            {
                if (AmmoInMag > 0)
                {
                    semiLatch = true;
                    Shoot();
                }
                else
                {
                    if (Mode == FireMode.Auto) nextShotTime = Time.time + 0.22f;
                    Sfx.Play("sfx_reload", 0.25f, 1.4f);
                    StartCoroutine(ReloadRoutine());
                }
            }

            // ----- ADS / fov handled by CameraController
            IsAds = inp.Ads && !Reloading;
            if (root.Player != null && root.Player.Cam != null)
                root.Player.Cam.SetAds(IsAds);

            UpdateViewModel(dt, inp);
        }

        void UpdateViewModel(float dt, InputService inp)
        {
            // sway
            swayX = Mathf.Lerp(swayX, -inp.Look.x * 0.02f, 1f - Mathf.Exp(-8f * dt));
            swayY = Mathf.Lerp(swayY, -inp.Look.y * 0.02f, 1f - Mathf.Exp(-8f * dt));

            Vector3 hipPos = new Vector3(0.24f, -0.215f, 0.34f);
            Vector3 adsPos = new Vector3(0f, -0.152f, 0.24f);
            Vector3 targetPos = IsAds ? adsPos : hipPos;

            // sprint / bob
            var player = GameRoot.I != null ? GameRoot.I.Player : null;
            float speed01 = player != null ? player.Speed01 : 0f;
            float t = Time.time;
            float bobX = Mathf.Sin(t * 9f) * 0.012f * speed01;
            float bobY = Mathf.Abs(Mathf.Cos(t * 9f)) * 0.014f * speed01;
            if (speed01 > 0.75f && !IsAds)
            {
                targetPos += new Vector3(0.035f, -0.06f, -0.04f);
            }
            targetPos += new Vector3(bobX, bobY, 0f) + new Vector3(swayX, swayY, 0f);

            // reload animation
            float reloadDip = 0f;
            float magDrop = 0f;
            if (Reloading)
            {
                float p = ReloadProgress;
                reloadDip = Mathf.Sin(p * Mathf.PI) * 0.09f;
                magDrop = p < 0.35f ? Mathf.InverseLerp(0f, 0.35f, p) : p < 0.65f ? 1f - Mathf.InverseLerp(0.35f, 0.65f, p) : 0f;
            }

            kickBack = Mathf.Lerp(kickBack, 0f, 1f - Mathf.Exp(-14f * dt));
            targetPos += new Vector3(0.012f, -reloadDip, -kickBack);

            float lerpSpeed = IsAds ? 16f : 11f;
            gunRoot.localPosition = Vector3.Lerp(gunRoot.localPosition, targetPos, 1f - Mathf.Exp(-lerpSpeed * dt));
            Quaternion targetRot = Quaternion.Euler(reloadDip * 260f, IsAds ? 0f : 1.5f, IsAds ? 0f : -2f);
            gunRoot.localRotation = Quaternion.Slerp(gunRoot.localRotation, targetRot, 1f - Mathf.Exp(-10f * dt));

            magTransform.localPosition = Vector3.Lerp(magTransform.localPosition,
                magHome + Vector3.down * magDrop * 0.22f, 1f - Mathf.Exp(-20f * dt));
        }

        System.Collections.IEnumerator ReloadRoutine()
        {
            Reloading = true;
            reloadStart = Time.time;
            Sfx.Play("sfx_reload", 0.7f, 1f);
            yield return new WaitForSeconds(ReloadTime * 0.62f);

            int need = MagSize - AmmoInMag;
            int take = Mathf.Min(need, Reserve);
            AmmoInMag += take;
            Reserve -= take;
            yield return new WaitForSeconds(ReloadTime * 0.38f);
            Reloading = false;
        }

        public void SetVisible(bool visible)
        {
            if (gunRoot != null) gunRoot.gameObject.SetActive(visible);
        }

        public void ResetAmmo()
        {
            AmmoInMag = MagSize;
            Reserve = ReserveStart;
            bloom = 0f;
            kickBack = 0f;
            Reloading = false;
            StopAllCoroutines();
            if (magTransform != null) magTransform.localPosition = magHome;
        }

        public void AddReserve(int amount)
        {
            Reserve = Mathf.Min(ReserveMax, Reserve + amount);
        }

        void Shoot()
        {
            nextShotTime = Time.time + 1f / FireRate;
            AmmoInMag--;
            bloom = Mathf.Min(bloom + BloomPerShot, 3.2f);
            kickBack = Mathf.Min(kickBack + 0.045f, 0.09f);

            if (GameRoot.I != null && GameRoot.I.Player != null && GameRoot.I.Player.Cam != null)
                GameRoot.I.Player.Cam.AddRecoil(RecoilKick * (IsAds ? 0.65f : 1f), 0.35f);
            Sfx.Play("sfx_shot", IsAds ? 0.55f : 0.75f, 0.95f + Random.value * 0.1f);

            // muzzle flash
            muzzleFlash.SetActive(true);
            muzzleFlash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            muzzleFlash.transform.localScale = Vector3.one * Random.Range(0.8f, 1.25f);
            flashLight.intensity = 6f;

            // spread
            float spread = Spread * (IsAds ? AdsSpreadMul : 1f) + bloom * (IsAds ? 0.35f : 1f);
            Vector3 dir = Cam.transform.forward;
            if (Mathf.Approximately(GameRoot.I.Player.Speed01, 0f) == false) spread *= 1.35f;
            dir = Quaternion.Euler(
                Random.Range(-spread, spread) * 0.35f,
                Random.Range(-spread, spread) * 0.35f, 0f) * dir;
            dir += Cam.transform.right * Random.Range(-spread, spread) * 0.008f;
            dir += Cam.transform.up * Random.Range(-spread, spread) * 0.008f;
            dir.Normalize();

            Vector3 origin = Cam.transform.position;
            RaycastHit hit;

            if (Physics.Raycast(origin, dir, out hit, Range, RayLayerMask, QueryTriggerInteraction.Ignore))
            {
                float dist = hit.distance;
                // damage through hit zones
                var zone = hit.collider.GetComponent<HitZone>();
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                bool head = false;
                float mult = 1f;
                if (zone != null)
                {
                    mult = zone.Multiplier;
                    head = zone.Multiplier >= 1.9f;
                }
                if (dmg != null)
                {
                    dmg.TakeDamage(new DamageInfo(Damage * mult, hit.point, hit.normal, dir, head, gameObject));
                    if (GameManager.I != null && GameManager.I.Hud != null && head)
                        GameManager.I.Hud.ShowHitmarker(true);
                    else if (GameManager.I != null && GameManager.I.Hud != null)
                        GameManager.I.Hud.ShowHitmarker(false);
                }
                else
                {
                    ImpactFx.Spawn(hit.point, hit.normal, hit.collider);
                }
                TracerFx.Spawn(muzzle.position, hit.point);
            }
            else
            {
                Vector3 far = origin + dir * Range;
                TracerFx.Spawn(muzzle.position, far);
            }
        }
    }
}
