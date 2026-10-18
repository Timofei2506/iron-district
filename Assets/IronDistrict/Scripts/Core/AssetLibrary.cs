using System.Collections.Generic;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Loads generated PBR materials / textures / clips and makes runtime variants.</summary>
    public static class AssetLibrary
    {
        static readonly Dictionary<string, Material> baseCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> variantCache = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        static Shader standard;

        public static Shader Standard
        {
            get
            {
                if (standard == null) standard = Shader.Find("Standard");
                return standard;
            }
        }

        public static Texture2D Tex(string name)
        {
            if (texCache.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Textures/" + name);
            texCache[name] = t;
            return t;
        }

        // texture name, metallic, smoothness, bump scale  (built in code -> no asset GUIDs to break)
        static readonly Dictionary<string, (string tex, float metal, float gloss, float bump)> SurfaceDefs =
            new Dictionary<string, (string, float, float, float)>
            {
                { "M_Concrete",     ("Concrete",     0.05f, 0.85f, 0.08f) },
                { "M_ConcreteDark", ("ConcreteDark", 0.05f, 0.80f, 0.08f) },
                { "M_Asphalt",      ("Asphalt",      0.04f, 0.75f, 0.12f) },
                { "M_Facade",       ("Facade",       0.04f, 0.70f, 0.06f) },
                { "M_Brick",        ("Brick",        0.04f, 0.75f, 0.04f) },
                { "M_Metal",        ("MetalPanel",   0.75f, 0.45f, 0.15f) },
                { "M_Rust",         ("Rust",         0.25f, 0.50f, 0.10f) },
                { "M_Grass",        ("Grass",        0.00f, 0.25f, 0.02f) },
                { "M_Dirt",         ("Dirt",         0.00f, 0.20f, 0.02f) },
                { "M_Wood",         ("CrateWood",    0.00f, 0.30f, 0.05f) },
                { "M_Sand",         ("Sand",         0.00f, 0.25f, 0.02f) },
                { "M_Hazard",       ("Hazard",       0.35f, 0.55f, 0.05f) },
            };

        public static Material BaseMaterial(string matName)
        {
            if (baseCache.TryGetValue(matName, out var m) && m != null) return m;

            m = Resources.Load<Material>("Materials/" + matName);
            if (m == null)
            {
                string tex = matName.StartsWith("M_") ? matName.Substring(2) : matName;
                float metal = 0.05f, gloss = 0.6f, bump = 0.08f;
                if (SurfaceDefs.TryGetValue(matName, out var def))
                {
                    tex = def.tex; metal = def.metal; gloss = def.gloss; bump = def.bump;
                }
                m = new Material(Standard);
                MatUtil.SetupStandard(ref m, Color.white, metal, gloss);
                var albedo = Tex(tex);
                if (albedo != null)
                {
                    m.SetTexture("_MainTex", albedo);
                    m.SetTexture("_BaseMap", albedo);
                }
                var n = Tex(tex + "_n");
                if (n != null)
                {
                    m.SetTexture("_BumpMap", n);
                    m.SetFloat("_BumpScale", bump);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.enableInstancing = true;
            }
            else
            {
                m = new Material(m);
            }
            baseCache[matName] = m;
            return m;
        }

        /// <summary>Copy of a base material with custom tiling (and optional tint).</summary>
        public static Material Tiled(string matName, Vector2 tiling, Color? tint = null, float? bumpScale = null)
        {
            string key = matName + "|" + tiling.x + "x" + tiling.y + "|" + (tint.HasValue ? tint.Value.ToString() : "-") + "|" + bumpScale;
            if (variantCache.TryGetValue(key, out var m) && m != null) return m;
            var src = BaseMaterial(matName);
            m = new Material(src);
            m.SetTextureScale("_MainTex", tiling);
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling);
            if (tint.HasValue)
            {
                m.color = tint.Value;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint.Value);
            }
            if (bumpScale.HasValue && m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bumpScale.Value);
            variantCache[key] = m;
            return m;
        }

        public static Material Tiled(string matName, float tiling, Color? tint = null)
            => Tiled(matName, new Vector2(tiling, tiling), tint);

        /// <summary>Solid standard material (glass, foliage, painted metal, emissive...).</summary>
        public static Material Solid(string key, Color color, float metallic = 0f, float smoothness = 0.25f,
            Color? emission = null, bool receiveShadows = true)
        {
            string k = "solid|" + key + "|" + color + "|" + metallic + "|" + smoothness + "|" + (emission.HasValue ? emission.Value.ToString() : "-");
            if (variantCache.TryGetValue(k, out var m) && m != null) return m;
            m = new Material(Standard);
            MatUtil.SetupStandard(ref m, color, metallic, smoothness);
            m.enableInstancing = true;
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            variantCache[k] = m;
            return m;
        }

        public static Material Unlit(string key, Color color, Texture2D tex = null, bool additive = false)
        {
            string k = "unlit|" + key + "|" + color + "|" + (tex != null ? tex.name : "-") + "|" + additive;
            if (variantCache.TryGetValue(k, out var m) && m != null) return m;
            var sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.color = color;
            if (tex != null) m.mainTexture = tex;
            variantCache[k] = m;
            return m;
        }

        public static AudioClip Clip(string name) => Resources.Load<AudioClip>("Audio/" + name);

        // -------- frequently used material shortcuts --------
        public static Material Concrete(float t = 1f) => Tiled("M_Concrete", t);
        public static Material ConcreteDark(float t = 1f) => Tiled("M_ConcreteDark", t);
        public static Material Asphalt(float t = 1f) => Tiled("M_Asphalt", t);
        public static Material Facade(float t = 1f, Color? tint = null) => Tiled("M_Facade", t, tint);
        public static Material Brick(float t = 1f) => Tiled("M_Brick", t);
        public static Material Metal(float t = 1f) => Tiled("M_Metal", t);
        public static Material Rust(float t = 1f) => Tiled("M_Rust", t);
        public static Material Grass(float t = 1f) => Tiled("M_Grass", t);
        public static Material Dirt(float t = 1f) => Tiled("M_Dirt", t);
        public static Material Wood(float t = 1f) => Tiled("M_Wood", t);
        public static Material Sand(float t = 1f) => Tiled("M_Sand", t);
        public static Material Hazard(float t = 1f) => Tiled("M_Hazard", t);

        public static Material Glass() => Solid("glass", new Color(0.10f, 0.13f, 0.16f), 0.55f, 0.93f);
        public static Material GlassLit() => Solid("glasslit", new Color(0.95f, 0.72f, 0.38f), 0f, 0.7f,
            new Color(1.4f, 0.82f, 0.35f));
        public static Material WindowFrame() => Solid("frame", new Color(0.14f, 0.14f, 0.15f), 0.6f, 0.35f);
        public static Material Bark() => Solid("bark", new Color(0.22f, 0.16f, 0.11f), 0f, 0.12f);
        public static Material Leaf(int variant) => Solid("leaf" + variant,
            new Color(0.16f + 0.05f * variant, 0.30f + 0.07f * variant, 0.12f + 0.03f * variant), 0f, 0.08f);
        public static Material PineLeaf(int variant) => Solid("pine" + variant,
            new Color(0.10f + 0.03f * variant, 0.24f + 0.05f * variant, 0.12f + 0.02f * variant), 0f, 0.07f);
        public static Material Rock() => Solid("rock", new Color(0.40f, 0.39f, 0.37f), 0f, 0.15f);
        public static Material Painted(Color c) => Solid("paint" + c, c, 0.25f, 0.45f);
        public static Material DarkMetal() => Solid("darkmetal", new Color(0.13f, 0.13f, 0.14f), 0.75f, 0.55f);
        public static Material AccentMetal() => Solid("accentmetal", new Color(0.85f, 0.36f, 0.10f), 0.35f, 0.5f);
        public static Material RedLight() => Solid("redlight", new Color(0.9f, 0.1f, 0.05f), 0f, 0.6f,
            new Color(3f, 0.15f, 0.05f));
        public static Material LampGlow() => Solid("lampglow", new Color(1f, 0.85f, 0.55f), 0f, 0.6f,
            new Color(2.6f, 2.0f, 1.2f));
    }
}
