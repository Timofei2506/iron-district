using UnityEngine;
using UnityEngine.AI;

namespace IronDistrict
{
    /// <summary>Hostile trooper: patrols, chases, strafes, burst-fires at the player.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        public float MaxHealth = 100f;
        public float Health { get; private set; }
        public bool IsAlive => Health > 0f && !Dying;

        NavMeshAgent agent;
        Transform target;
        Transform head;
        Transform barrel;
        Material bodyMat;
        ParticleSystem muzzleSmoke;

        enum State { Spawn, Patrol, Attack, Dead }
        State state;

        Vector3 home;
        Vector3 patrolTarget;
        Vector3 lastKnown;
        float nextThink;
        float lastSeenTime = -99f;
        float nextBurst;
        int burstLeft;
        float nextShot;
        float strafeDir;
        float nextStrafeFlip;
        float stunUntil;
        bool Dying;
        float deathTime;
        float idleLookTimer;

        // vision + enemy fire must *see* the player (layer 2), so cast against everything
        static readonly int Mask = ~0;

        public static Enemy Spawn(Vector3 pos, int tier)
        {
            var go = new GameObject("Enemy");
            go.transform.position = pos;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.38f;
            agent.height = 1.85f;
            agent.baseOffset = 0.02f;
            agent.speed = 3.1f + tier * 0.35f;
            agent.acceleration = 16f;
            agent.angularSpeed = 640f;
            agent.stoppingDistance = 8f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.updateRotation = false;

            var e = go.AddComponent<Enemy>();
            e.MaxHealth = 85f + tier * 25f;
            e.Health = e.MaxHealth;
            e.BuildBody(tier);
            return e;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        void BuildBody(int tier)
        {
            var sk = AssetLibrary.Solid("skin", new Color(0.62f, 0.48f, 0.38f), 0f, 0.25f);
            var cloth = AssetLibrary.Solid("cloth" + tier,
                tier == 0 ? new Color(0.14f, 0.16f, 0.13f) : new Color(0.10f, 0.11f, 0.12f),
                0f, 0.15f);
            var armor = AssetLibrary.DarkMetal();
            var accent = tier == 0 ? AssetLibrary.Solid("enemyAccent", new Color(0.75f, 0.15f, 0.1f), 0.2f, 0.4f)
                                   : AssetLibrary.Solid("enemyAccent2", new Color(1f, 0.55f, 0.1f), 0.2f, 0.5f);
            var visorGlow = AssetLibrary.Solid("visor", new Color(1f, 0.2f, 0.08f), 0f, 0.8f, new Color(2.2f, 0.25f, 0.05f));

            var visual = Geo.Obj("Visual", transform);

            // torso (cloth) with vest (armor) -> separate meshes for two materials
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, 1.22f, 0), new Vector3(0.5f, 0.62f, 0.28f), 0f, 0.6f);
            mb.AddBox(new Vector3(0, 0.86f, 0), new Vector3(0.42f, 0.24f, 0.26f), 0f, 0.6f);   // pelvis
            mb.AddBox(new Vector3(-0.13f, 0.48f, 0), new Vector3(0.17f, 0.55f, 0.2f), 0f, 0.6f); // legs
            mb.AddBox(new Vector3(0.13f, 0.48f, 0), new Vector3(0.17f, 0.55f, 0.2f), 0f, 0.6f);
            mb.AddBox(new Vector3(-0.13f, 0.12f, 0.03f), new Vector3(0.16f, 0.16f, 0.28f), 0f, 0.6f);
            mb.AddBox(new Vector3(0.13f, 0.12f, 0.03f), new Vector3(0.16f, 0.16f, 0.28f), 0f, 0.6f);
            mb.AddBox(new Vector3(-0.33f, 1.2f, 0), new Vector3(0.15f, 0.5f, 0.18f), 0f, 0.6f);  // arms
            mb.AddBox(new Vector3(0.33f, 1.2f, 0), new Vector3(0.15f, 0.5f, 0.18f), 0f, 0.6f);
            Geo.MeshObj("Cloth", visual.transform, mb.Build("cloth"), new[] { cloth });

            var mb2 = new MeshBuilder();
            mb2.AddBox(new Vector3(0, 1.26f, 0.02f), new Vector3(0.54f, 0.44f, 0.32f), 0f, 0.6f);  // vest
            mb2.AddBox(new Vector3(0, 1.52f, 0), new Vector3(0.3f, 0.1f, 0.26f), 0f, 0.6f);       // shoulders
            mb2.AddBox(new Vector3(-0.13f, 0.24f, 0), new Vector3(0.19f, 0.2f, 0.22f), 0f, 0.6f); // shin guards
            mb2.AddBox(new Vector3(0.13f, 0.24f, 0), new Vector3(0.19f, 0.2f, 0.22f), 0f, 0.6f);
            Geo.MeshObj("Armor", visual.transform, mb2.Build("armor"), new[] { armor });

            var mb3 = new MeshBuilder();
            mb3.AddBox(new Vector3(0, 1.45f, 0.06f), new Vector3(0.3f, 0.08f, 0.1f), 0f, 0.6f); // accent patch
            mb3.AddBox(new Vector3(0.2f, 1.5f, 0.05f), new Vector3(0.09f, 0.12f, 0.12f), 0f, 0.6f); // radio
            Geo.MeshObj("Accent", visual.transform, mb3.Build("accent"), new[] { accent });

            // head
            var headGo = Geo.Obj("Head", visual.transform, new Vector3(0, 1.72f, 0));
            var hb = new MeshBuilder();
            hb.AddBox(Vector3.zero, new Vector3(0.24f, 0.26f, 0.24f), 0f, 0.6f);
            hb.AddBox(new Vector3(0, -0.06f, 0.03f), new Vector3(0.27f, 0.14f, 0.27f), 0f, 0.6f); // jaw guard
            Geo.MeshObj("HeadMesh", headGo.transform, hb.Build("head"), new[] { sk });
            var viz = new MeshBuilder();
            viz.AddBox(new Vector3(0, 0.01f, 0.125f), new Vector3(0.18f, 0.06f, 0.02f), 0f, 0.6f);
            Geo.MeshObj("Visor", headGo.transform, viz.Build("visor"), new[] { visorGlow });
            head = headGo.transform;
            var headCol = headGo.AddComponent<CapsuleCollider>();
            headCol.radius = 0.16f;
            headCol.height = 0.3f;
            var zone = headGo.AddComponent<HitZone>();
            zone.Multiplier = 2.1f;

            // body hitbox
            var bodyGo = Geo.Obj("BodyHitbox", transform, new Vector3(0, 1.05f, 0));
            var bodyCol = bodyGo.AddComponent<CapsuleCollider>();
            bodyCol.radius = 0.31f;
            bodyCol.height = 1.35f;
            var bzone = bodyGo.AddComponent<HitZone>();
            bzone.Multiplier = 1f;

            // rifle
            var rifle = Geo.Obj("Rifle", visual.transform, new Vector3(0.22f, 1.18f, 0.32f));
            var rb = new MeshBuilder();
            rb.AddBox(Vector3.zero, new Vector3(0.06f, 0.09f, 0.52f), 0f, 0.5f);
            rb.AddBox(new Vector3(0, -0.09f, -0.1f), new Vector3(0.05f, 0.14f, 0.07f), 0f, 0.5f);
            rb.AddBox(new Vector3(0, 0, -0.3f), new Vector3(0.05f, 0.08f, 0.14f), 0f, 0.5f);
            Geo.MeshObj("GunMesh", rifle.transform, rb.Build("enemyGun"), new[] { armor });
            var barrelGo = Geo.Obj("Barrel", rifle.transform, new Vector3(0, 0, 0.3f));
            barrel = barrelGo.transform;

            var smokeGo = new GameObject("MuzzleSmoke");
            smokeGo.transform.SetParent(barrel, false);
            muzzleSmoke = smokeGo.AddComponent<ParticleSystem>();
            var main = muzzleSmoke.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 1.2f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.9f, 0.85f, 0.7f, 0.5f);
            main.maxParticles = 10;
            var em = muzzleSmoke.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var rend = smokeGo.GetComponent<ParticleSystemRenderer>();
            rend.material = AssetLibrary.Unlit("muzzle", Color.white, AssetLibrary.Tex("Smoke"));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            muzzleSmoke.Stop();

            // audio source for shots
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 4f;
            src.maxDistance = 160f;
            src.dopplerLevel = 0f;

            // death shrink anchor unused; simple
            Dying = false;
        }

        void Start()
        {
            home = transform.position;
            target = GameRoot.I != null && GameRoot.I.Player != null ? GameRoot.I.Player.transform : null;
            state = State.Spawn;
            nextThink = Time.time + Random.Range(0.2f, 0.7f);
            strafeDir = Random.value < 0.5f ? -1f : 1f;
            nextStrafeFlip = Time.time + Random.Range(1.5f, 3.5f);
            PickPatrolPoint();
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas))
            {
                if (agent.isOnNavMesh) agent.Warp(hit.position);
            }
        }

        void PickPatrolPoint()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 r = Random.insideUnitCircle * 18f;
                Vector3 p = home + new Vector3(r.x, 0, r.y);
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas))
                {
                    patrolTarget = hit.position;
                    return;
                }
            }
            patrolTarget = home;
        }

        void Update()
        {
            if (target == null)
            {
                var root = GameRoot.I;
                if (root != null && root.Player != null) target = root.Player.transform;
            }

            if (Dying)
            {
                deathTime += Time.deltaTime;
                transform.position += Vector3.down * Time.deltaTime * 0.9f;
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(1f, 0.12f, 1f), 1f - Mathf.Exp(-4f * Time.deltaTime));
                if (deathTime > 3.5f) Destroy(gameObject);
                return;
            }

            if (!IsAlive) return;

            if (GameRoot.I == null || GameRoot.I.State != GameRoot.GameState.Playing)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            if (Time.time < stunUntil)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + Random.Range(0.18f, 0.35f);
                Think();
            }

            if (state == State.Attack) AttackUpdate();

            // visual yaw follows velocity/target
            Vector3 lookDir = transform.forward;
            if (state == State.Attack && target != null)
            {
                lookDir = target.position - transform.position;
                lookDir.y = 0;
            }
            else if (agent.velocity.sqrMagnitude > 0.2f)
            {
                lookDir = agent.velocity;
                lookDir.y = 0;
            }
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-9f * Time.deltaTime));
            }
        }

        bool CanSeePlayer(out float dist)
        {
            dist = 999f;
            if (target == null) return false;
            Vector3 eye = transform.position + Vector3.up * 1.65f + transform.forward * 0.45f;
            Vector3 targetPoint = target.position + Vector3.up * 1.3f;
            Vector3 dir = targetPoint - eye;
            dist = dir.magnitude;
            float viewDist = state == State.Attack ? 90f : 58f;
            if (dist > viewDist) return false;

            float angle = Vector3.Angle(transform.forward, dir);
            if (angle > 100f && state != State.Attack) return false;

            if (Physics.Raycast(eye, dir.normalized, out var hit, dist + 0.6f, Mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerHealth>() != null) return true;
                if (hit.collider.transform.IsChildOf(transform)) return true; // own body - ignore
                return false;
            }
            return true;
        }

        void Think()
        {
            if (agent == null || !agent.isOnNavMesh) return;

            bool sees = CanSeePlayer(out float dist);
            if (sees)
            {
                lastSeenTime = Time.time;
                if (state != State.Attack)
                {
                    state = State.Attack;
                    Invoke(nameof(AlertShout), Random.Range(0.1f, 0.5f));
                }
                lastKnown = target.position;

                if (dist < 26f)
                {
                    // strafe around the player
                    if (Time.time >= nextStrafeFlip)
                    {
                        nextStrafeFlip = Time.time + Random.Range(1.2f, 2.8f);
                        strafeDir = Random.value < 0.5f ? -1f : 1f;
                    }
                    Vector3 toTarget = lastKnown - transform.position;
                    toTarget.y = 0;
                    Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized) * strafeDir;
                    Vector3 back = dist < 12f ? -toTarget.normalized * 3.5f : Vector3.zero;
                    Vector3 dest = transform.position + side * 4f + back;
                    if (NavMesh.SamplePosition(dest, out var hit, 3f, NavMesh.AllAreas))
                    {
                        agent.isStopped = false;
                        agent.speed = 2.6f;
                        agent.SetDestination(hit.position);
                    }
                }
                else
                {
                    agent.isStopped = false;
                    agent.speed = 4.1f;
                    agent.SetDestination(lastKnown);
                }
                return;
            }

            // pursuit of last known position shortly after losing sight
            if (state == State.Attack && Time.time - lastSeenTime < 6f)
            {
                agent.isStopped = false;
                agent.speed = 4.1f;
                agent.SetDestination(lastKnown);
                return;
            }

            state = State.Patrol;
            agent.isStopped = false;
            agent.speed = 2.0f;
            if (!agent.hasPath || Vector3.Distance(transform.position, patrolTarget) < 2.5f ||
                (agent.pathPending == false && agent.remainingDistance < 1.5f))
            {
                PickPatrolPoint();
            }
            agent.SetDestination(patrolTarget);

            // idle scanning
            idleLookTimer -= 0.2f;
            if (idleLookTimer <= 0f)
            {
                idleLookTimer = Random.Range(1.5f, 4f);
            }
        }

        void AlertShout()
        {
            if (!IsAlive) return;
            Sfx.PlayAt("sfx_enemy_down", transform.position, 0.25f, 1.4f, 30f);
        }

        void AttackUpdate()
        {
            if (target == null) return;
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > 95f) { state = State.Patrol; return; }
            if (!CanSeePlayer(out dist)) return;

            if (Time.time >= nextBurst && burstLeft <= 0)
            {
                burstLeft = Random.Range(2, 5);
                nextBurst = Time.time + Random.Range(1.6f, 3.1f);
                nextShot = Time.time;
            }

            if (burstLeft > 0 && Time.time >= nextShot)
            {
                burstLeft--;
                nextShot = Time.time + Random.Range(0.11f, 0.16f);
                FireAtPlayer();
            }
        }

        void FireAtPlayer()
        {
            if (target == null || barrel == null) return;
            Vector3 aimPoint = target.position + Vector3.up * 1.25f;
            Vector3 origin = barrel.position;

            float dist = Vector3.Distance(origin, aimPoint);
            float hitChance = Mathf.Clamp01(0.72f - dist * 0.008f); // farther = less accurate
            Vector3 dir;

            if (Random.value < hitChance)
            {
                dir = (aimPoint - origin).normalized;
                // partial miss jitter near hit
                dir = Quaternion.Euler(Random.Range(-1.4f, 1.4f), Random.Range(-1.4f, 1.4f), 0f) * dir;
            }
            else
            {
                dir = (aimPoint - origin).normalized;
                dir = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f) * dir;
            }

            var player = GameRoot.I != null ? GameRoot.I.Player : null;
            if (player == null) return;

            // raycast to see if the shot is blocked
            if (Physics.Raycast(origin, dir, out var hit, 200f, Mask, QueryTriggerInteraction.Ignore))
            {
                var dmg = hit.collider.GetComponentInParent<IDamageable>();
                if (dmg is PlayerHealth ph)
                {
                    var zone = hit.collider.GetComponent<HitZone>();
                    float mult = zone != null ? zone.Multiplier : 1f;
                    ph.TakeDamage(new DamageInfo(9f * mult, hit.point, hit.normal, dir, false, gameObject));
                }
                else
                {
                    ImpactFx.Spawn(hit.point, hit.normal, hit.collider);
                }
            }
            TracerFx.Spawn(origin, origin + dir * 90f);

            var src = GetComponent<AudioSource>();
            if (src != null)
            {
                src.pitch = Random.Range(0.9f, 1.05f);
                src.PlayOneShot(Sfx.Get("sfx_enemy_shot"), 0.55f);
            }
            if (muzzleSmoke != null) muzzleSmoke.Emit(2);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive) return;
            Health -= info.Amount;

            if (Health <= 0f)
            {
                Die(info);
                return;
            }

            // flinch + aggro
            stunUntil = Mathf.Max(stunUntil, Time.time + 0.12f);
            if (state != State.Attack)
            {
                state = State.Attack;
                lastKnown = info.Point;
                nextThink = 0f;
            }
            Sfx.PlayAt("sfx_hit", info.Point, 0.3f, Random.Range(0.8f, 1.2f), 30f);
        }

        void Die(DamageInfo info)
        {
            Dying = true;
            deathTime = 0f;
            Health = 0f;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            if (agent != null) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Sfx.PlayAt("sfx_enemy_down", transform.position, 0.7f, Random.Range(0.9f, 1.1f), 60f);

            GameManager.I?.RegisterKill(info.Headshot, GameManager.I.Wave);
            if (Random.value < 0.42f)
                Pickup.Spawn(transform.position + Vector3.up * 0.4f,
                    Random.value < 0.6f ? PickupKind.Ammo : PickupKind.Medkit);
        }
    }
}
