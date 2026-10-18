using System.Collections;
using UnityEngine;

namespace IronDistrict
{
    /// <summary>Boots every system, owns game-state transitions and the start menu flow.</summary>
    public class GameRoot : MonoBehaviour
    {
        public enum GameState { Menu, Generating, Playing, Paused, Dead }

        public static GameRoot I;

        /// <summary>Build version, shown in the menu and the diagnostics overlay.</summary>
        public const string Version = "1.2.1";

        public GameState State { get; private set; } = GameState.Menu;
        public PlayerController Player;
        public WorldGenerator World;
        public EnemySpawner Spawner;
        public HUD Hud;
        public MenuController Menu;
        public SkyController Sky;
        public Diagnostics Diag;

        bool worldReady;
        int seed = 1337;
        FpsProbe probe;

        public int Seed => seed;

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            Time.timeScale = 1f;
            Application.targetFrameRate = 240;
            QualitySettings.vSyncCount = 0;

            GameBootstrap.EnsureEventSystem();
            ApplyPerformanceSettings();

            InputService.Sensitivity = PlayerPrefs.GetFloat("IrD_Sens", 1f);
            AudioListener.volume = PlayerPrefs.GetFloat("IrD_Vol", 0.9f);
            Diag = gameObject.AddComponent<Diagnostics>();
            Sfx.Init(gameObject);

            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(transform, false);
            inputGo.AddComponent<InputService>();

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(transform, false);
            var gm = gmGo.AddComponent<GameManager>();
            gmGo.AddComponent<EnemySpawner>();
            gmGo.AddComponent<WorldGenerator>();

            Spawner = gmGo.GetComponent<EnemySpawner>();
            World = gmGo.GetComponent<WorldGenerator>();
            gm.Spawner = Spawner;
            gm.World = World;

            Sky = SkyController.Create(transform);

            Player = PlayerController.Create(new Vector3(30f, 34f, -298f));
            gm.Player = Player;

            Hud = HUD.Create(transform);
            Hud.gameObject.SetActive(false);
            gm.Hud = Hud;

            Menu = MenuController.Create(transform);
            probe = gameObject.AddComponent<FpsProbe>();
        }

        void Start()
        {
            // Меню больше не может заблокировать игру: сразу уходим в мир.
            Menu.Show();
            StartCoroutine(AutoStart());
        }

        System.Collections.IEnumerator AutoStart()
        {
            yield return null;               // дать UI отрисоваться
            yield return null;
            if (State == GameState.Menu) StartGame();
        }

        // ------------------------------------------------------------- states
        public void ShowMenu()
        {
            State = GameState.Menu;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Hud.gameObject.SetActive(false);
            Hud.ShowPause(false, 0, 0, 0);
            Hud.HideDeath();
            Menu.Show();
            GameManager.I?.ResetRun();

            Player.Weapon.SetVisible(false);
            Player.Teleport(new Vector3(30f, 34f, -298f));
            Player.Cam.SetMenuView(4f, 7f);
            Sky.MenuMood();
            Player.Health.Invulnerable = false;
            DestroyEnemies();
        }

        public void StartGame()
        {
            if (State != GameState.Menu) return;
            if (!worldReady)
            {
                StartCoroutine(GenerateWorld());
                return;
            }
            BeginRun();
        }

        float generatingSince;

        IEnumerator GenerateWorld()
        {
            State = GameState.Generating;
            generatingSince = Time.realtimeSinceStartup;
            Menu.SetStatus("ГЕНЕРАЦИЯ ГОРОДА...");
            World.UnloadWorld();
            GameManager.I.ResetRun();
            Spawner.CancelInvoke();
            yield return null;

            seed = PlayerPrefs.GetInt("IrD_Seed", 0);
            if (seed == 0)
            {
                seed = Random.Range(1000, 999999);
                PlayerPrefs.SetInt("IrD_Seed", seed);
                PlayerPrefs.Save();
            }

            yield return StartCoroutine(World.Generate(seed, s =>
            {
                Menu.SetStatus("ГЕНЕРАЦИЯ: " + s);
            }, e =>
            {
                Diag.ReportFatal(World.GenerationError ?? e.Message);
            }));

            if (World.GenerationFailed)
            {
                State = GameState.Menu;
                Menu.Show();
                Menu.SetStatus("ОШИБКА ГЕНЕРАЦИИ — см. красную плашку внизу");
                yield break;
            }

            worldReady = true;
            Menu.SetStatus("");
            BeginRun();
        }

        public void BeginRun()
        {
            DestroyEnemies();
            GameManager.I.ResetRun();
            Spawner.CancelInvoke();
            Spawner.Begin();

            Player.Teleport(WorldData.PlayerSpawn);
            Player.Cam.SetMenuView(0f, 1.5f);
            Player.Health.ResetHealth();
            Player.Weapon.ResetAmmo();
            Player.Weapon.SetVisible(true);

            Hud.HideDeath();
            Hud.gameObject.SetActive(true);
            Hud.ShowWave(1);
            Sky.PlayMood();
            Menu.Hide();
            State = GameState.Playing;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
            InputService.I?.ArmGrace(0.35f); // клик по кнопке меню не должен стрелять
            Sfx.SetAmbientVolume(0.5f);
        }

        /// <summary>Menu button: drop the current world so the next start generates a new seed.</summary>
        public void RequestNewWorld()
        {
            PlayerPrefs.SetInt("IrD_Seed", 0);
            PlayerPrefs.Save();
            if (worldReady)
            {
                World.UnloadWorld();
                worldReady = false;
            }
            DestroyEnemies();
            Menu.SetStatus("СИД СБРОШЕН — НОВЫЙ МИР БУДЕТ СОЗДАН ПРИ СТАРТЕ");
        }

        public void RestartRun()
        {
            if (!worldReady) return;
            BeginRun();
        }

        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            State = GameState.Dead;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameManager.I.SubmitBest();
            Hud.ShowDeath(GameManager.I.Kills, GameManager.I.Score, GameManager.I.Wave);
            Sfx.SetAmbientVolume(0.15f);
            Player.Weapon.SetVisible(false);
        }

        public void Pause(bool paused)
        {
            if (paused && State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Hud.ShowPause(true, GameManager.I.Kills, GameManager.I.Score, GameManager.I.Wave);
            }
            else if (!paused && State == GameState.Paused)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Hud.ShowPause(false, 0, 0, 0);
            }
        }

        public void HitFeedback(float intensity)
        {
            if (Hud != null) Hud.OnPlayerDamaged(Vector3.zero, false);
        }

        void DestroyEnemies()
        {
            Player.Health.Invulnerable = true;
#pragma warning disable 618
            var enemies = Object.FindObjectsOfType<Enemy>();
#pragma warning restore 618
            foreach (var e in enemies)
                if (e != null) Destroy(e.gameObject);
            Player.Health.Invulnerable = false;
        }

        // ------------------------------------------------------------- update
        void Update()
        {
            TracerFx.Tick(Time.deltaTime);

            if (State == GameState.Generating && Time.realtimeSinceStartup - generatingSince > 90f &&
                Diag != null && Diag.FatalError == null)
            {
                Diag.ReportFatal("Генерация мира не завершилась за 90 секунд (зависание на шаге " +
                                 (World != null && !string.IsNullOrEmpty(World.GenerationError) ? World.GenerationError : "неизвестно") + ")");
            }

            switch (State)
            {
                case GameState.Playing:
                    if (Input.GetKeyDown(KeyCode.Escape)) Pause(true);
                    break;
                case GameState.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape)) Pause(false);
                    else if (Input.GetKeyDown(KeyCode.Return)) RestartRun();
                    else if (Input.GetKeyDown(KeyCode.Q)) ShowMenu();
                    break;
                case GameState.Dead:
                    if (Input.GetKeyDown(KeyCode.R)) RestartRun();
                    else if (Input.GetKeyDown(KeyCode.Escape)) ShowMenu();
                    break;
                case GameState.Menu:
                    // Enter / Space / Numpad Enter start the run even if uGUI events fail
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                        Input.GetKeyDown(KeyCode.Space))
                    {
                        StartGame();
                    }
                    break;
            }

            if (State == GameState.Playing && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // ------------------------------------------------------------- config
        void ApplyPerformanceSettings()
        {
            Application.runInBackground = true;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            Texture.SetGlobalAnisotropicFilteringLimits(8, 16);
            QualitySettings.lodBias = 1.4f;
        }

    }
}
