using UnityEngine;

namespace IronDistrict
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        public float MaxHealth = 100f;
        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;

        float lastDamageTime = -10f;
        float regenTimer;

        public bool Invulnerable { get; set; }

        void Awake()
        {
            Health = MaxHealth;
        }

        public void ResetHealth()
        {
            Health = MaxHealth;
            Sfx.SetAmbientVolume(0.5f);
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(MaxHealth, Health + amount);
            Sfx.Play("sfx_pickup", 0.8f, 1.1f);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || Invulnerable) return;
            Health -= info.Amount;
            lastDamageTime = Time.time;
            regenTimer = 0f;

            if (GameRoot.I != null) GameRoot.I.HitFeedback(info.Amount / MaxHealth);
            if (GameManager.I != null && GameManager.I.Hud != null)
                GameManager.I.Hud.OnPlayerDamaged(info.Point, info.Headshot);
            Sfx.Play("sfx_hurt", 0.55f, 1f);

            if (Health <= 0f)
            {
                Health = 0f;
                GameRoot.I?.OnPlayerDied();
            }
        }

        void Update()
        {
            if (!IsAlive) return;
            if (GameRoot.I == null || GameRoot.I.State != GameRoot.GameState.Playing) return;
            if (Health >= MaxHealth) return;

            // classic FPS regen: wait 6s after last hit, then heal
            if (Time.time - lastDamageTime > 6f)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer > 1.2f)
                    Health = Mathf.Min(MaxHealth, Health + 13f * Time.deltaTime);
            }
        }
    }
}
