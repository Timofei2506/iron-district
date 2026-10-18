using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IronDistrict
{
    /// <summary>Endless waves with escalating tiers.</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public int MaxAlive = 12;
        public int BasePerWave = 6;

        readonly List<Enemy> alive = new List<Enemy>();
        int pending;
        int waveKills;
        int waveTarget;
        float nextSpawnTime;
        int tier = 1;
        bool waveEnding;

        public int AliveCount => alive.Count + pending;
        public int WaveRemaining => Mathf.Max(0, waveTarget - waveKills);

        public void Begin()
        {
            alive.Clear();
            pending = 0;
            waveKills = 0;
            tier = 1;
            waveEnding = false;
            StartWave(1);
        }

        void StartWave(int wave)
        {
            waveEnding = false;
            GameManager.I.Wave = wave;
            waveTarget = BasePerWave + wave * 2;
            waveKills = 0;
            tier = Mathf.Clamp(1 + wave / 3, 1, 3);
            pending = waveTarget;
            nextSpawnTime = Time.time + 1.5f;
            MaxAlive = Mathf.Min(18, 10 + wave);
            GameManager.I.Hud?.ShowWave(wave);
        }

        public void NotifyKilled()
        {
            waveKills++;
            if (waveKills >= waveTarget && alive.Count == 0 && pending == 0)
            {
                GameManager.I.Hud?.ShowWaveCleared();
                Invoke(nameof(NextWave), 4.5f);
            }
        }

        void NextWave()
        {
            if (GameRoot.I == null || GameRoot.I.State != GameRoot.GameState.Playing) return;
            StartWave(GameManager.I.Wave + 1);
        }

        void Update()
        {
            var root = GameRoot.I;
            if (root == null || root.State != GameRoot.GameState.Playing) return;
            if (root.Player == null) return;

            alive.RemoveAll(e => e == null);

            if (waveKills < waveTarget && pending > 0 && alive.Count < MaxAlive && Time.time >= nextSpawnTime)
            {
                nextSpawnTime = Time.time + Random.Range(0.55f, 1.7f);
                SpawnOne();
            }

            // wave ended but spawner didn't get notified (edge case: enemy despawned)
            if (!waveEnding && waveKills >= waveTarget && alive.Count == 0 && pending == 0)
            {
                waveEnding = true;
                GameManager.I.Hud?.ShowWaveCleared();
                Invoke(nameof(NextWave), 4.5f);
            }
        }

        void SpawnOne()
        {
            pending--;
            Vector3 playerPos = GameRoot.I.Player.transform.position;

            Vector3 best = playerPos;
            bool found = false;
            float bestScore = float.MinValue;

            for (int i = 0; i < 26; i++)
            {
                var candidates = WorldData.EnemySpawnPoints;
                if (candidates.Count == 0) break;
                Vector3 p = candidates[Random.Range(0, candidates.Count)];
                float d = Vector3.Distance(p, playerPos);
                if (d < 42f || d > 230f) continue;
                if (!NavMesh.SamplePosition(p, out var navHit, 5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (!HasLineOfSight(navHit.position)) continue;
                float score = -Mathf.Abs(d - 62f) + Random.value * 22f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = navHit.position;
                    found = true;
                }
            }

            if (!found)
            {
                // fallback ring around the player
                for (int i = 0; i < 14 && !found; i++)
                {
                    float ang = Random.value * Mathf.PI * 2f;
                    float d = Random.Range(60f, 110f);
                    Vector3 p = playerPos + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * d;
                    if (!WorldData.InsideWorld(p, 30f)) continue;
                    if (NavMesh.SamplePosition(p, out var navHit, 10f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        best = navHit.position;
                        found = true;
                    }
                }
            }

            if (!found) { pending++; return; }

            var enemy = Enemy.Spawn(best, tier);
            alive.Add(enemy);
        }

        bool HasLineOfSight(Vector3 point)
        {
            Vector3 player = GameRoot.I.Player.transform.position + Vector3.up * 1.5f;
            Vector3 eye = point + Vector3.up * 1.6f;
            Vector3 dir = player - eye;
            if (Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude + 1f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerHealth>() == null) return false;
            }
            return true;
        }
    }
}
