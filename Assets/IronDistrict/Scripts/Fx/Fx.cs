using System.Collections.Generic;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Pooled tracer bullets.</summary>
    public static class TracerFx
    {
        class Tracer
        {
            public GameObject Go;
            public LineRenderer Line;
            public float Life;
        }

        static readonly List<Tracer> pool = new List<Tracer>();
        static Material lineMat;
        static int activeCount;

        public static void Spawn(Vector3 from, Vector3 to)
        {
            if (lineMat == null)
            {
                var sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                lineMat = new Material(sh) { color = new Color(1f, 0.85f, 0.5f, 0.75f) };
            }
            if (activeCount > 90) return;
            activeCount++;

            Tracer t = null;
            foreach (var p in pool)
            {
                if (!p.Go.activeSelf) { t = p; break; }
            }
            if (t == null)
            {
                var go = new GameObject("Tracer");
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.widthMultiplier = 1f;
                lr.useWorldSpace = true;
                lr.numCapVertices = 0;
                var grad = new Gradient();
                grad.SetKeys(new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f)
                }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                lr.colorGradient = grad;
                lr.material = lineMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startWidth = 0.035f;
                lr.endWidth = 0.008f;
                t = new Tracer { Go = go, Line = lr };
                pool.Add(t);
            }

            Vector3 dir = (to - from).normalized;
            float len = Vector3.Distance(from, to);
            // tracers read better when not starting inside the camera
            Vector3 start = from + dir * 0.4f;
            t.Line.SetPosition(0, start);
            t.Line.SetPosition(1, to);
            t.Line.startWidth = 0.028f;
            t.Line.endWidth = 0.004f;
            t.Life = 0.07f;
            t.Go.SetActive(true);
        }

        public static void Tick(float dt)
        {
            foreach (var t in pool)
            {
                if (!t.Go.activeSelf) continue;
                t.Life -= dt;
                if (t.Life <= 0f)
                {
                    t.Go.SetActive(false);
                    activeCount = Mathf.Max(0, activeCount - 1);
                }
            }
        }
    }

    /// <summary>Impact decals + sparks + debris puff.</summary>
    public static class ImpactFx
    {
        static Material holeMat;
        static int decalIndex;
        static readonly Transform[] decals = new Transform[110];

        public static void Spawn(Vector3 point, Vector3 normal, Collider col = null)
        {
            if (holeMat == null)
            {
                holeMat = AssetLibrary.Unlit("hole", new Color(1f, 1f, 1f, 0.85f), AssetLibrary.Tex("BulletHole"));
            }

            bool useDecal = Random.value < 0.55f;
            if (useDecal)
            {
                Transform d = decals[decalIndex % decals.Length];
                if (d == null)
                {
                    var go = new GameObject("Decal");
                    var mf = go.AddComponent<MeshFilter>();
                    var mb = new MeshBuilder();
                    mb.AddQuad(Vector3.zero, Vector3.right, Vector3.up, 1f, 1f, 1f);
                    mf.sharedMesh = mb.Build("decal");
                    go.transform.localScale = Vector3.one * 0.22f;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = holeMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    d = go.transform;
                    decals[decalIndex % decals.Length] = d;
                }
                decalIndex++;
                d.position = point + normal * 0.012f;
                d.rotation = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                d.gameObject.SetActive(true);
            }

            // sparks via tiny pooled quads
            Vector3 camPos = Camera.main != null ? Camera.main.transform.position : point + Vector3.up * 2f;
            if (Vector3.Distance(camPos, point) < 60f)
            {
                for (int i = 0; i < 4; i++)
                {
                    Spark.Spawn(point, normal);
                }
                if (Random.value < 0.35f) Sfx.PlayAt("sfx_hit", point, 0.12f, Random.Range(0.7f, 1.3f), 18f);
            }
        }
    }

    public class Spark : MonoBehaviour
    {
        Vector3 vel;
        float life;
        static Transform poolRoot;

        public static void Spawn(Vector3 pos, Vector3 normal)
        {
            if (poolRoot == null) poolRoot = new GameObject("Sparks").transform;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(poolRoot, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.03f;
            var r = go.GetComponent<MeshRenderer>();
            r.material = AssetLibrary.Solid("spark", new Color(1f, 0.7f, 0.25f), 0f, 0.5f, new Color(3f, 1.4f, 0.3f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var s = go.AddComponent<Spark>();
            s.vel = (normal + Random.insideUnitSphere * 0.9f).normalized * Random.Range(2f, 7f);
            s.life = Random.Range(0.15f, 0.4f);
        }

        void Update()
        {
            life -= Time.deltaTime;
            vel += Vector3.down * 9.8f * Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Max(0.001f, life * 0.12f);
            if (life <= 0f) Destroy(gameObject);
        }
    }

    /// <summary>Explosions: light, sound, smoke puff.</summary>
    public static class ExplosionFx
    {
        public static void Spawn(Vector3 pos, float radius = 7f)
        {
            Sfx.PlayAt("sfx_explosion", pos, 1f, Random.Range(0.9f, 1.05f), 250f);

            var go = new GameObject("Explosion");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startColor = new Color(1f, 0.55f, 0.15f, 0.9f);
            main.maxParticles = 80;
            main.duration = 0.4f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.6f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f),
                new GradientColorKey(new Color(0.3f, 0.28f, 0.26f), 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.material = AssetLibrary.Unlit("smoke", Color.white, AssetLibrary.Tex("Smoke"));
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();

            var lightGo = new GameObject("Flash");
            lightGo.transform.SetParent(go.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = radius * 2.2f;
            l.intensity = 14f;
            l.color = new Color(1f, 0.6f, 0.25f);
            l.shadows = LightShadows.None;
            lightGo.AddComponent<LightFade>();

            Object.Destroy(go, 2.5f);
        }
    }

    public class LightFade : MonoBehaviour
    {
        Light l;
        void Awake() { l = GetComponent<Light>(); }
        void Update()
        {
            if (l == null) return;
            l.intensity = Mathf.MoveTowards(l.intensity, 0f, Time.deltaTime * 30f);
        }
    }
}
