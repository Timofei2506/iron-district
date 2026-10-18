using System.Collections.Generic;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Tiny audio facade: 2D UI/one-shots + positional one-shots + ambient loop.</summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static AudioSource ui;
        static AudioSource ambient;

        public static AudioClip Get(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            cache[name] = c;
            return c;
        }

        public static void Init(GameObject root)
        {
            ui = root.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;

            ambient = root.AddComponent<AudioSource>();
            ambient.playOnAwake = false;
            ambient.loop = true;
            ambient.spatialBlend = 0f;
            ambient.volume = 0f;
            var wind = Get("amb_wind");
            if (wind != null)
            {
                ambient.clip = wind;
                ambient.Play();
            }
        }

        public static void SetMasterVolume(float v)
        {
            AudioListener.volume = Mathf.Clamp01(v);
        }

        public static void SetAmbientVolume(float v)
        {
            if (ambient) ambient.volume = Mathf.Clamp01(v);
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(name);
            if (clip == null || ui == null) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume);
        }

        public static void PlayAt(string name, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDist = 140f)
        {
            var clip = Get(name);
            if (clip == null) return;
            var go = new GameObject("sfx_" + name);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 4f;
            src.maxDistance = maxDist;
            src.dopplerLevel = 0f;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(pitch, 0.01f) + 0.15f);
        }
    }
}
