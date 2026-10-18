using UnityEngine;

namespace IronDistrict
{
    /// <summary>Run-wide state hub: kills, score, wave, references.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public PlayerController Player;
        public WorldGenerator World;
        public EnemySpawner Spawner;
        public HUD Hud;

        public int Kills;
        public int Score;
        public int Wave;

        public const string BestScoreKey = "IrD_BestScore";

        void Awake() { I = this; }

        public void ResetRun()
        {
            Kills = 0;
            Score = 0;
            Wave = 0;
        }

        public void RegisterKill(bool headshot, int wave)
        {
            Kills++;
            Score += headshot ? 150 : 100;
            Hud?.OnKill(headshot);
            Spawner?.NotifyKilled();
        }

        public void SubmitBest()
        {
            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            if (Score > best) PlayerPrefs.SetInt(BestScoreKey, Score);
            PlayerPrefs.Save();
        }

        public static int Best => PlayerPrefs.GetInt(BestScoreKey, 0);
    }
}
