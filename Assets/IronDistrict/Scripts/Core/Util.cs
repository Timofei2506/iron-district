using System.Collections.Generic;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Shared palette used across world + UI.</summary>
    public static class Palette
    {
        public static readonly Color Accent = new Color(0.95f, 0.55f, 0.22f);
        public static readonly Color Danger = new Color(0.88f, 0.22f, 0.16f);
        public static readonly Color Success = new Color(0.49f, 0.81f, 0.35f);
        public static readonly Color Text = new Color(0.94f, 0.92f, 0.88f);
        public static readonly Color TextDim = new Color(0.72f, 0.70f, 0.66f);
        public static readonly Color Panel = new Color(0.055f, 0.06f, 0.07f, 0.92f);
        public static readonly Color TowerDark = new Color(0.16f, 0.16f, 0.18f);
    }

    /// <summary>Tiny procedural mesh builder (flat shaded, supports submeshes).</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triIdx = new List<int>();
        readonly List<int> triSub = new List<int>();

        public int Submesh = 0;

        /// <summary>Bake a fake ambient occlusion into vertex colours (ground grime + dark undersides).</summary>
        public bool BakeAO = false;
        /// <summary>Height (metres) over which the ground-grime gradient fades out. 0 = no gradient.</summary>
        public float GroundFade = 0f;

        public bool IsEmpty => verts.Count == 0;

        void Quad(Vector3 bl, Vector3 br, Vector3 tl, Vector3 tr, Vector2 uvScale, Vector3 normal)
        {
            int i = verts.Count;
            verts.Add(bl); verts.Add(br); verts.Add(tl); verts.Add(tr);
            norms.Add(normal); norms.Add(normal); norms.Add(normal); norms.Add(normal);
            float u = uvScale.x, v = uvScale.y;
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(u, 0));
            uvs.Add(new Vector2(0, v)); uvs.Add(new Vector2(u, v));
            triIdx.Add(i); triIdx.Add(i + 1); triIdx.Add(i + 2);
            triIdx.Add(i + 2); triIdx.Add(i + 1); triIdx.Add(i + 3);
            triSub.Add(Submesh); triSub.Add(Submesh);
        }

        /// <summary>Quad from center oriented by right/up vectors. Faces cross(right, up).</summary>
        public void AddQuad(Vector3 center, Vector3 right, Vector3 up, float width, float height, float uvPerMeter = 0.5f)
        {
            Vector3 r = right.normalized * (width * 0.5f);
            Vector3 u = up.normalized * (height * 0.5f);
            Vector3 n = Vector3.Cross(right.normalized, up.normalized);
            Quad(center - r - u, center + r - u, center - r + u, center + r + u,
                new Vector2(width * uvPerMeter, height * uvPerMeter), n);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, float uvPerMeter = 0.5f)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = rot * Vector3.right, Y = rot * Vector3.up, Z = rot * Vector3.forward;
            void Face(Vector3 right, Vector3 up, Vector3 axis, Vector3 halfNormal, float wx, float hy)
            {
                Vector3 c = center + axis;
                Vector3 r = right * (wx * 0.5f);
                Vector3 u = up * (hy * 0.5f);
                Vector3 n = Vector3.Cross(right, up);
                Quad(c - r - u, c + r - u, c - r + u, c + r + u,
                    new Vector2(wx * uvPerMeter, hy * uvPerMeter), n);
            }
            // +X / -X
            Face(-Z, Y, X * h.x, X, size.z, size.y);
            Face(Z, Y, X * -h.x, -X, size.z, size.y);
            // +Z / -Z
            Face(X, Y, Z * h.z, Z, size.x, size.y);
            Face(-X, Y, Z * -h.z, -Z, size.x, size.y);
            // +Y / -Y
            Face(X, -Z, Y * h.y, Y, size.x, size.z);
            Face(X, Z, Y * -h.y, -Y, size.x, size.z);
        }

        public void AddBox(Vector3 center, Vector3 size, float yawDegrees = 0f, float uvPerMeter = 0.5f)
            => AddBox(center, size, Quaternion.Euler(0, yawDegrees, 0), uvPerMeter);

        /// <summary>Cylinder / cone along +Y.</summary>
        public void AddTaperedCylinder(Vector3 baseCenter, float rBottom, float rTop, float height,
            int segments = 12, bool caps = true, float uvPerMeter = 0.5f)
        {
            segments = Mathf.Max(3, segments);
            float circ = 2f * Mathf.PI * Mathf.Max(rBottom, rTop);
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * rBottom;
                Vector3 b1 = baseCenter + d1 * rBottom;
                Vector3 t0 = baseCenter + d0 * rTop + Vector3.up * height;
                Vector3 t1 = baseCenter + d1 * rTop + Vector3.up * height;
                Vector3 n = Vector3.Cross(d1 - d0, Vector3.up).normalized;
                // outward normal per segment midpoint
                Vector3 md = (d0 + d1) * 0.5f;
                Vector3 outward = new Vector3(md.x, (rTop - rBottom) / Mathf.Max(height, 0.001f), md.z).normalized;
                n = outward;
                int i0 = verts.Count;
                verts.Add(b0); verts.Add(b1); verts.Add(t0);
                verts.Add(t1);
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
                float u = circ * 0.5f; // keep moderate tiling
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(u, 0));
                uvs.Add(new Vector2(0, height * uvPerMeter)); uvs.Add(new Vector2(u, height * uvPerMeter));
                // outward winding: (b0, t0, b1) / (t0, t1, b1)
                triIdx.Add(i0); triIdx.Add(i0 + 2); triIdx.Add(i0 + 1);
                triIdx.Add(i0 + 2); triIdx.Add(i0 + 3); triIdx.Add(i0 + 1);
                triSub.Add(Submesh); triSub.Add(Submesh);
            }
            if (caps)
            {
                for (int cap = 0; cap < 2; cap++)
                {
                    float y = cap == 0 ? 0 : height;
                    float r = cap == 0 ? rBottom : rTop;
                    if (r <= 0.001f) continue;
                    int cIdx = verts.Count;
                    verts.Add(baseCenter + Vector3.up * y);
                    norms.Add(cap == 0 ? Vector3.down : Vector3.up);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    int ringStart = verts.Count;
                    for (int i = 0; i < segments; i++)
                    {
                        float a = (float)i / segments * Mathf.PI * 2f;
                        Vector3 p = baseCenter + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r + Vector3.up * y;
                        verts.Add(p);
                        norms.Add(cap == 0 ? Vector3.down : Vector3.up);
                        uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                    }
                    for (int i = 0; i < segments; i++)
                    {
                        int nxt = (i + 1) % segments;
                        if (cap == 0)
                        {
                            triIdx.Add(cIdx); triIdx.Add(ringStart + i); triIdx.Add(ringStart + nxt);
                        }
                        else
                        {
                            triIdx.Add(cIdx); triIdx.Add(ringStart + nxt); triIdx.Add(ringStart + i);
                        }
                        triSub.Add(Submesh);
                    }
                }
            }
        }

        public void AddSphere(Vector3 center, Vector3 radii, int segments = 10, int rings = 7)
        {
            segments = Mathf.Max(3, segments);
            rings = Mathf.Max(2, rings);
            int start = verts.Count;
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.PI * i / rings;
                float sinLat = Mathf.Sin(lat), cosLat = Mathf.Cos(lat);
                for (int j = 0; j <= segments; j++)
                {
                    float lon = 2f * Mathf.PI * j / segments;
                    Vector3 dir = new Vector3(sinLat * Mathf.Cos(lon), cosLat, sinLat * Mathf.Sin(lon));
                    verts.Add(center + Vector3.Scale(dir, radii));
                    norms.Add(Vector3.Scale(dir, new Vector3(1f / Mathf.Max(radii.x, .001f), 1f / Mathf.Max(radii.y, .001f), 1f / Mathf.Max(radii.z, .001f))).normalized);
                    uvs.Add(new Vector2((float)j / segments, 1f - (float)i / rings));
                }
            }
            int row = segments + 1;
            for (int i = 0; i < rings; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = start + i * row + j;
                    int b = a + 1;
                    int c = a + row;
                    int d = c + 1;
                    triIdx.Add(a); triIdx.Add(b); triIdx.Add(c);
                    triIdx.Add(b); triIdx.Add(d); triIdx.Add(c);
                    triSub.Add(Submesh); triSub.Add(Submesh);
                }
            }
        }

        Color[] BakeVertexAO()
        {
            float minY = float.MaxValue;
            for (int i = 0; i < verts.Count; i++) if (verts[i].y < minY) minY = verts[i].y;

            var cols = new Color[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                Vector3 n = norms[i];

                float ao = 1f;

                // grime gradient near the ground
                if (GroundFade > 0.01f)
                {
                    float h = Mathf.Clamp01((v.y - minY) / GroundFade);
                    ao *= Mathf.Lerp(0.52f, 1f, Mathf.Sqrt(h));
                }

                // undersides and inward faces read darker
                if (n.y < -0.35f) ao *= 0.72f;
                else if (n.y > 0.95f) ao *= 0.93f;

                // cheap per-vertex variation breaks the "plastic" look
                float jitter = 0.955f + 0.045f * Mathf.PerlinNoise(v.x * 0.9f + 13.1f, v.z * 0.9f + 7.7f);
                ao *= jitter;

                ao = Mathf.Clamp01(ao);
                cols[i] = new Color(ao, ao, ao, 1f);
            }
            return cols;
        }

        public Mesh Build(string name = "mesh")
        {
            Mesh m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            if (BakeAO) m.SetColors(BakeVertexAO());
            int subCount = 0;
            for (int i = 0; i < triSub.Count; i++) subCount = Mathf.Max(subCount, triSub[i] + 1);
            if (subCount <= 1)
            {
                m.SetTriangles(triIdx, 0, false);
            }
            else
            {
                m.subMeshCount = subCount;
                var buckets = new List<int>[subCount];
                for (int s = 0; s < subCount; s++) buckets[s] = new List<int>();
                for (int t = 0; t < triSub.Count; t++)
                {
                    int s = triSub[t];
                    buckets[s].Add(triIdx[t * 3]);
                    buckets[s].Add(triIdx[t * 3 + 1]);
                    buckets[s].Add(triIdx[t * 3 + 2]);
                }
                for (int s = 0; s < subCount; s++) m.SetTriangles(buckets[s], s, false);
            }
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Object / mesh helpers.</summary>
    public static class Geo
    {
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        public static GameObject Obj(string name, Transform parent, Vector3 localPos = default, Quaternion rot = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot == default ? Quaternion.identity : rot;
            return go;
        }

        public static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material[] mats,
            Vector3 localPos = default, Quaternion rot = default)
        {
            var go = Obj(name, parent, localPos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            Quaternion rot = default, float uvPerMetre = 0.5f)
        {
            string key = "box_" + size.x + "_" + size.y + "_" + size.z + "_" + uvPerMetre;
            if (!meshCache.TryGetValue(key, out var m))
            {
                var mb = new MeshBuilder();
                mb.AddBox(Vector3.zero, size, 0f, uvPerMetre);
                m = mb.Build("box");
                meshCache[key] = m;
            }
            return MeshObj(name, parent, m, new[] { mat }, center, rot);
        }

        public static GameObject BoxCol(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            Quaternion rot = default, float uvPerMetre = 0.5f)
        {
            var go = Box(name, parent, center, size, mat, rot, uvPerMetre);
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.center = Vector3.zero;
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 center, float radius, float height,
            Material mat, int segments = 12, float uvPerMetre = 0.5f)
        {
            string key = "cyl_" + radius + "_" + height + "_" + segments;
            if (!meshCache.TryGetValue(key, out var m))
            {
                var mb = new MeshBuilder();
                mb.AddTaperedCylinder(new Vector3(0, -height * 0.5f, 0), radius, radius, height, segments, true, uvPerMetre);
                m = mb.Build("cyl");
                meshCache[key] = m;
            }
            return MeshObj(name, parent, m, new[] { mat }, center);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 center, Vector3 radii, Material mat,
            int segments = 10, int rings = 7)
        {
            string key = "sph_" + radii.x + "_" + radii.y + "_" + radii.z + "_" + segments + "_" + rings;
            if (!meshCache.TryGetValue(key, out var m))
            {
                var mb = new MeshBuilder();
                mb.AddSphere(Vector3.zero, radii, segments, rings);
                m = mb.Build("sphere");
                meshCache[key] = m;
            }
            return MeshObj(name, parent, m, new[] { mat }, center);
        }

        /// <summary>Horizontal textured rectangle (roads, pads, ground).</summary>
        public static GameObject GroundRect(string name, Transform parent, float cx, float cz,
            float sizeX, float sizeZ, float y, Material mat, float uvPerMetre = 0.1f)
        {
            var mb = new MeshBuilder();
            mb.AddQuad(new Vector3(cx, y, cz), Vector3.forward, Vector3.right, sizeZ, sizeX, uvPerMetre);
            var go = MeshObj(name, parent, mb.Build("ground"), new[] { mat });
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static void AddBoxCollider(GameObject go, Vector3 size, Vector3 center = default)
        {
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            c.center = center;
        }

        public static void AddCapsuleCollider(GameObject go, float radius, float height, Vector3 center = default)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = height;
            c.center = center;
        }
    }

    /// <summary>Font helper (works in 2022+ and older editors).</summary>
    public static class UIFont
    {
        static Font cached;

        static readonly string[] OsFonts =
        {
            "Arial", "Segoe UI", "Tahoma", "Verdana", "Helvetica",
            "Liberation Sans", "DejaVu Sans", "Noto Sans", "FreeSans"
        };

        public static Font Get()
        {
            if (cached != null) return cached;

            // In a standalone build the OS font is the reliable path.
            try { cached = Font.CreateDynamicFontFromOSFont(OsFonts, 24); } catch { }
            if (cached == null)
            {
                try { cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            }
            if (cached == null)
            {
                try { cached = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return cached;
        }
    }

    public static class MatUtil
    {
        public static void SetupStandard(ref Material m, Color color, float metallic, float smoothness)
        {
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        }
    }
}
