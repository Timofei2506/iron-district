#!/usr/bin/env python3
"""
Iron District - procedural asset generator.
Creates tileable PBR textures (albedo / normal / roughness), Unity .meta files
with Linux Standalone settings, and lightweight WAV sound effects.

Run:  python3 Tools/gen_assets.py
"""
import os, wave, struct, io
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources")
TEX = os.path.join(ROOT, "Textures")
AUD = os.path.join(ROOT, "Audio")
UID_DIR = os.path.join(ROOT, "Textures")  # meta written next to assets
os.makedirs(TEX, exist_ok=True)
os.makedirs(AUD, exist_ok=True)
rng = np.random.default_rng(1337)


# ---------------------------------------------------------------- helpers
def _uid_from(name: str) -> str:
    """Deterministic 32-char hex GUID from asset name."""
    import hashlib
    return hashlib.md5(("IrD_" + name).encode()).hexdigest()


def upsample(x: np.ndarray, size: int) -> np.ndarray:
    img = Image.fromarray((np.clip(x, 0, 1) * 255).astype(np.uint8), "L")
    if img.size[0] != size:
        img = img.resize((size, size), Image.BICUBIC)
    return np.asarray(img).astype(np.float32) / 255.0


def fbm(size: int, base: int = 4, octaves: int = 5, seed: int = 0) -> np.ndarray:
    """Tileable fractal value noise via periodic upsampled grids."""
    r = np.random.default_rng(seed)
    out = np.zeros((size, size), np.float32)
    amp, total, freq = 1.0, 0.0, base
    for _ in range(octaves):
        g = r.random((freq, freq)).astype(np.float32)
        g = np.tile(g, (3, 3))
        g = np.asarray(Image.fromarray((g * 255).astype(np.uint8), "L")
                       .resize((size * 3, size * 3), Image.BICUBIC)).astype(np.float32) / 255.0
        g = g[size:size * 2, size:size * 2]
        out += g * amp
        total += amp
        amp *= 0.55
        freq *= 2
    return out / total


def normal_from_height(h: np.ndarray, strength: float = 2.0) -> np.ndarray:
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.dstack((-dx, -dy, np.ones_like(h)))
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def save_tex(name: str, rgb01: np.ndarray, normal_strength: float | None = None,
             rough01: np.ndarray | None = None, alpha: np.ndarray | None = None):
    size = rgb01.shape[0]
    a = (np.clip(rgb01, 0, 1) * 255).astype(np.uint8)
    if alpha is not None:
        rgba = np.dstack([a, (np.clip(alpha, 0, 1) * 255).astype(np.uint8)])
        img = Image.fromarray(rgba, "RGBA")
        kind = 0
    else:
        img = Image.fromarray(a, "RGB")
        kind = 1
    files = []
    base = os.path.join(TEX, name)
    img.save(base + ".png")
    files.append((base + ".png", kind))

    if normal_strength is not None:
        lum = rgb01.mean(axis=2) if rgb01.ndim == 3 else rgb01
        if rough01 is not None:
            lum = rough01
        nrm = (normal_from_height(lum, normal_strength) * 255).astype(np.uint8)
        Image.fromarray(nrm, "RGB").save(base + "_n.png")
        files.append((base + "_n.png", 1))

    if rough01 is not None:
        Image.fromarray((np.clip(rough01, 0, 1) * 255).astype(np.uint8), "L").save(base + "_r.png")
        files.append((base + "_r.png", 1))

    for path, kind in files:
        ext = os.path.splitext(path)[1]
        uid = _uid_from(os.path.basename(path))
        meta = f"""fileFormatVersion: 2
guid: {uid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {1 if kind == 1 else 0}
    linearTexture: {0 if kind == 1 else 1}
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: {1 if 'Normal' in path or path.endswith('_n.png') else 0}
    heightScale: 1
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""
        with open(path + ".meta", "w") as f:
            f.write(meta)


# ---------------------------------------------------------------- textures
def concrete(name, s=512, base=(0.42, 0.42, 0.44), seed=1, moss=0.06):
    n1 = fbm(s, 4, 5, seed)
    n2 = fbm(s, 16, 3, seed + 1)
    pits = (fbm(s, 64, 2, seed + 2) > 0.72).astype(np.float32)
    pits = upsample(pits, s)
    shade = 0.75 + 0.35 * n1 - 0.25 * pits * rng.random((s, s)) * 0.3
    col = np.dstack([shade * base[0], shade * base[1], shade * base[2]])
    # moss streaks
    m = np.clip((fbm(s, 3, 4, seed + 7) - 0.62) * 6, 0, 1) * moss
    col = col * (1 - m[..., None]) + np.dstack(
        [m * 0.24, m * 0.30, m * 0.16])
    rough = np.clip(0.85 + 0.1 * n2 - 0.25 * pits, 0, 1)
    save_tex(name, col, normal_strength=1.4, rough01=rough)


def asphalt(name, s=512, seed=11):
    n1 = fbm(s, 6, 6, seed)
    n2 = fbm(s, 48, 3, seed + 1)
    stones = (n2 > 0.68).astype(np.float32) * 0.12
    base = 0.20 + 0.10 * n1 + stones
    crack = np.abs(fbm(s, 5, 3, seed + 9) - 0.5)
    crack = np.clip((0.03 - crack) * 22, 0, 1)
    base = base * (1 - crack * 0.75)
    col = np.dstack([base * 1.02, base, base * 0.98])
    rough = np.clip(0.9 + 0.08 * n2, 0, 1)
    save_tex(name, col, normal_strength=1.8, rough01=rough)


def facade(name, s=512, seed=21, brick=False):
    wall = fbm(s, 8, 5, seed) * 0.18 + 0.30
    # concrete panel seams + per-panel tone
    yy, xx = np.mgrid[0:s, 0:s]
    ph, pw = s // 4, s // 2
    panel_id = (xx // pw) + (yy // ph) * 2
    wall = wall + 0.035 * np.sin(panel_id * 2.1)
    seam = ((yy % ph) < 2) | ((xx % pw) < 2)
    wall = np.where(seam, wall * 0.62, wall)
    col = np.dstack([wall * 1.0, wall * 0.99, wall * 0.96])
    if brick:
        bw, bh = s // 8, s // 16
        yy, xx = np.mgrid[0:s, 0:s]
        row = yy // bh
        off = (row % 2) * (bw // 2)
        mortar = ((yy % bh) < 2) | (((xx + off) % bw) < 2)
        shade = 0.65 + 0.25 * fbm(s, 32, 2, seed + 3)
        r = shade * 0.55
        col = np.dstack([r, r * 0.42, r * 0.34])
        col[mortar] = 0.55
    grime = np.clip((fbm(s, 2, 5, seed + 5) - 0.5) * 1.8, 0, 1)[..., None]
    col = col * (1 - 0.45 * grime)
    streak = np.clip((fbm(s, 3, 4, seed + 11) - 0.55) * 5, 0, 1)[..., None]
    col = col * (1 - 0.25 * streak)
    lum = col.mean(axis=2)
    rough = np.clip(0.7 + 0.2 * (1 - lum), 0, 1)
    save_tex(name, col, normal_strength=1.0 if not brick else 2.2, rough01=rough)


def panel(name, s=512, seed=31, base=(0.36, 0.34, 0.31), scale=4):
    """Large metal/panel sheet with seams."""
    n1 = fbm(s, 6, 5, seed)
    col = np.dstack([(0.8 + 0.35 * n1) * base[0],
                     (0.8 + 0.35 * n1) * base[1],
                     (0.8 + 0.35 * n1) * base[2]])
    yy, xx = np.mgrid[0:s, 0:s]
    seam = ((xx % (s // scale)) < 2) | ((yy % (s // scale)) < 2)
    col[seam] *= 0.45
    rivet = ((xx % (s // scale) - 6) ** 2 + (yy % (s // scale) - 6) ** 2) < 5
    col[rivet] *= 1.35
    rough = np.clip(0.55 + 0.25 * n1, 0, 1)
    lum = col.mean(axis=2)
    save_tex(name, col, normal_strength=2.0, rough01=rough)


def rusty(name, s=512, seed=41):
    n1 = fbm(s, 5, 6, seed)
    n2 = fbm(s, 20, 4, seed + 2)
    metal = 0.34 + 0.12 * n1
    col = np.dstack([metal, metal * 0.97, metal * 0.92])
    rust_m = np.clip((fbm(s, 4, 5, seed + 6) - 0.52) * 3.2, 0, 1)
    rust = np.dstack([rust_m * 0.45 + n2 * rust_m * 0.25,
                      rust_m * 0.20 + n2 * rust_m * 0.08,
                      rust_m * 0.08])
    col = col * (1 - rust_m[..., None] * 0.9) + rust
    rough = np.clip(0.45 + 0.5 * rust_m + 0.1 * n2, 0, 1)
    save_tex(name, col, normal_strength=2.4, rough01=rough)


def grass(name, s=512, seed=51):
    n1 = fbm(s, 6, 6, seed)
    n2 = fbm(s, 64, 3, seed + 1)
    blades = (fbm(s, 96, 2, seed + 4) > 0.62).astype(np.float32)
    g = 0.20 + 0.22 * n1 + 0.10 * blades
    dirt = np.clip((fbm(s, 3, 4, seed + 8) - 0.55) * 4, 0, 1)
    col = np.dstack([g * 0.55 + dirt * 0.25, g * 0.95, g * 0.38])
    col = np.clip(col + 0.03 * n2[..., None], 0, 1)
    rough = np.clip(0.8 + 0.15 * n2, 0, 1)
    save_tex(name, col, normal_strength=1.6, rough01=rough)


def dirt(name, s=512, seed=61):
    n1 = fbm(s, 5, 6, seed)
    n2 = fbm(s, 40, 3, seed + 1)
    col = np.dstack([0.30 + 0.16 * n1, 0.24 + 0.13 * n1, 0.17 + 0.09 * n1])
    peb = (n2 > 0.66).astype(np.float32) * 0.10
    col = np.clip(col + peb[..., None], 0, 1)
    rough = np.clip(0.92 + 0.06 * n1, 0, 1)
    save_tex(name, col, normal_strength=1.8, rough01=rough)


def crate_wood(name, s=512, seed=71):
    xx, yy = np.mgrid[0:s, 0:s]
    planks = s // 6
    pid = xx // planks
    grain = fbm(s, 24, 4, seed) * 0.2
    tone = 0.34 + 0.10 * np.sin(pid * 1.7) + grain
    col = np.dstack([tone * 1.15, tone * 0.78, tone * 0.45])
    seam = (xx % planks) < 2
    col[seam] *= 0.35
    nail = (((xx % planks) - 4) ** 2 + ((yy % 96) - 48) ** 2) < 6
    col[nail] = 0.12
    rough = np.clip(0.75 + 0.2 * grain, 0, 1)
    save_tex(name, col, normal_strength=1.6, rough01=rough)


def sand(name, s=512, seed=81):
    n1 = fbm(s, 8, 5, seed)
    ripple = 0.5 + 0.5 * np.sin((np.mgrid[0:s, 0:s][0] * 0.11) + 4 * n1)
    col = np.dstack([0.62 + 0.12 * n1 + 0.05 * ripple,
                     0.55 + 0.10 * n1 + 0.04 * ripple,
                     0.38 + 0.07 * n1 + 0.03 * ripple])
    rough = np.clip(0.9 + 0.05 * n1, 0, 1)
    save_tex(name, col, normal_strength=0.9, rough01=rough)


def hazard(name, s=256):
    xx, yy = np.mgrid[0:s, 0:s]
    stripe = (((xx + yy) // 32) % 2).astype(np.float32)
    yellow = np.dstack([stripe * 0.85 + 0.12, stripe * 0.65 + 0.10, np.full_like(stripe, 0.05)])
    dark = np.dstack([y * 0.12 for y in (stripe, stripe, stripe)])
    col = np.where(stripe[..., None] > 0.5, yellow, dark)
    wear = fbm(s, 16, 4, 99)
    col = col * (0.75 + 0.3 * wear[..., None])
    save_tex(name, np.clip(col, 0, 1), normal_strength=0.8, rough01=0.7 + 0.2 * wear)


def flash(name, s=256):
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.sqrt((xx - s / 2) ** 2 + (yy - s / 2) ** 2) / (s / 2)
    a = np.clip(1 - d, 0, 1) ** 2
    a = np.clip(a + 0.35 * (fbm(s, 8, 3, 5) - 0.4), 0, 1)
    rgb = np.dstack([np.ones_like(a), 0.75 + 0.2 * a, 0.25 + 0.2 * a])
    save_tex(name, rgb, alpha=a)


def hole(name, s=256):
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.sqrt((xx - s / 2) ** 2 + (yy - s / 2) ** 2) / (s / 2)
    a = np.clip((0.55 - d) * 4, 0, 1)
    a *= 0.75 + 0.25 * fbm(s, 16, 3, 3)
    rgb = np.dstack([np.full_like(a, 0.03), np.full_like(a, 0.03), np.full_like(a, 0.03)])
    save_tex(name, rgb, alpha=a)


def smoke(name, s=256):
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.sqrt((xx - s / 2) ** 2 + (yy - s / 2) ** 2) / (s / 2)
    base = np.clip(1 - d, 0, 1)
    n = fbm(s, 6, 5, 42)
    a = np.clip((base * 1.4 - 0.35) * 0.9 + (n - 0.5) * 0.9, 0, 1) * base
    rgb = np.dstack([np.full_like(a, 0.75), np.full_like(a, 0.75), np.full_like(a, 0.78)])
    save_tex(name, rgb, alpha=a)


# ---------------------------------------------------------------- audio
def wav_write(name, samples, rate=44100):
    samples = np.clip(samples, -1, 1)
    pcm = (samples * 32767).astype(np.int16)
    path = os.path.join(AUD, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())
    uid = _uid_from(os.path.basename(path))
    with open(path + ".meta", "w") as f:
        f.write(f"""fileFormatVersion: 2
guid: {uid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""")


def env_adsr(n, a=0.005, d=0.1, s=0.4, r=0.2, sr=44100):
    t = np.arange(n) / sr
    A = int(a * sr); D = int(d * sr); R = int(r * sr)
    S = max(0, n - A - D - R)
    env = np.concatenate([np.linspace(0, 1, A, endpoint=False) if A else np.array([]),
                          np.linspace(1, s, D, endpoint=False) if D else np.array([]),
                          np.full(S, s),
                          np.linspace(s, 0, R) if R else np.array([])])
    if len(env) < n:
        env = np.pad(env, (0, n - len(env)))
    return env[:n]


def gen_audio():
    sr = 44100
    r = np.random.default_rng(7)

    # gunshot: noise burst + low thump
    n = int(0.35 * sr)
    t = np.arange(n) / sr
    noise = r.normal(0, 1, n) * np.exp(-t * 22)
    thump = np.sin(2 * np.pi * (90 - 60 * t) * t) * np.exp(-t * 30) * 0.9
    crack = np.sin(2 * np.pi * 1800 * t) * np.exp(-t * 90) * 0.3
    wav_write("sfx_shot", (noise * 0.8 + thump + crack) * 0.9)

    # enemy shot: quieter
    wav_write("sfx_enemy_shot", (noise * 0.45 + thump * 0.4) * 0.7)

    # reload click-clack
    clicks = np.zeros(int(0.7 * sr))
    for start, f, a in [(0.02, 2200, 0.7), (0.22, 1500, 0.8), (0.45, 900, 0.9)]:
        i = int(start * sr)
        m = int(0.05 * sr)
        tt = np.arange(m) / sr
        clicks[i:i + m] += (np.sin(2 * np.pi * f * tt) * np.exp(-tt * 160) * a +
                            r.normal(0, 0.4, m) * np.exp(-tt * 300) * a)
    wav_write("sfx_reload", clicks * 0.6)

    # hit marker tick
    t = np.arange(int(0.08 * sr)) / sr
    wav_write("sfx_hit", np.sin(2 * np.pi * 2400 * t) * np.exp(-t * 60) * 0.5)

    # enemy death
    n = int(0.5 * sr); t = np.arange(n) / sr
    wav_write("sfx_enemy_down", (np.sin(2 * np.pi * (300 - 200 * t) * t) * np.exp(-t * 8)) * 0.5)

    # pickup blip
    n = int(0.15 * sr); t = np.arange(n) / sr
    f = 900 + 1400 * t / 0.15
    wav_write("sfx_pickup", np.sin(2 * np.pi * f * t) * np.exp(-t * 20) * 0.5)

    # player hurt
    n = int(0.25 * sr); t = np.arange(n) / sr
    wav_write("sfx_hurt", (np.sin(2 * np.pi * 160 * t) + r.normal(0, .3, n)) * np.exp(-t * 14) * 0.6)

    # explosion
    n = int(1.2 * sr); t = np.arange(n) / sr
    boom = r.normal(0, 1, n) * np.exp(-t * 5)
    low = np.sin(2 * np.pi * (60 - 30 * t) * t) * np.exp(-t * 3)
    wav_write("sfx_explosion", (boom * 0.6 + low) * 0.9)

    # footsteps (two variants)
    for i, seed in ((1, 3), (2, 9)):
        rr = np.random.default_rng(seed)
        n = int(0.12 * sr); t = np.arange(n) / sr
        wav_write(f"sfx_step{i}", rr.normal(0, 1, n) * np.exp(-t * 55) * 0.5)

    # wind loop (16 s, crossfaded)
    n = int(16 * sr)
    w = r.normal(0, 1, n)
    k = np.ones(200) / 200
    w = np.convolve(w, k, mode="same")
    mod = 0.6 + 0.4 * np.sin(2 * np.pi * np.arange(n) / sr / 7.3)
    w = w / (np.abs(w).max() + 1e-6) * mod * 0.5
    fade = int(0.5 * sr)
    w[:fade] *= np.linspace(0, 1, fade)
    w[-fade:] *= np.linspace(1, 0, fade)
    wav_write("amb_wind", w)


# ---------------------------------------------------------------- main
def main():
    print("textures...")
    concrete("Concrete", seed=1)
    concrete("ConcreteDark", seed=5, base=(0.26, 0.26, 0.27), moss=0.12)
    asphalt("Asphalt")
    facade("Facade", seed=21)
    facade("Brick", seed=24, brick=True)
    panel("MetalPanel", seed=31)
    rusty("Rust", seed=41)
    grass("Grass")
    dirt("Dirt")
    crate_wood("CrateWood")
    sand("Sand")
    hazard("Hazard")
    flash("MuzzleFlash")
    hole("BulletHole")
    smoke("Smoke")
    print("audio...")
    gen_audio()
    print("done.")


if __name__ == "__main__":
    main()
