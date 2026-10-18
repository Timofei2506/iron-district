#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronDistrict.EditorTools
{
    /// <summary>
    /// Opens the project ready-to-play: ensures a bootstrap scene with GameRoot exists
    /// and registers it in Build Settings.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectAutoSetup
    {
        public const string ScenePath = "Assets/Scenes/IronDistrict.unity";
        public const string SceneFolder = "Assets/Scenes";

        static ProjectAutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += AutoSetup;
        }

        static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            ShaderGuard.EnsureAlwaysIncludedShaders();

            var active = SceneManager.GetActiveScene();
            bool activeHasRoot = Object.FindObjectOfType<GameRoot>() != null;

            if (activeHasRoot || active.isDirty)
            {
                // user is working on their own scene - only make sure build settings point somewhere sane
                if (activeHasRoot) EnsureBuildSettings(active.path);
                return;
            }

            EnsureScene(openIt: true);
        }

        [MenuItem("Iron District/Подготовить сцену запуска")]
        public static void EnsureSceneMenu()
        {
            if (SceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EnsureScene(openIt: true);
        }

        public static void EnsureScene(bool openIt)
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            if (!System.IO.File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Game");
                go.AddComponent<GameRoot>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
                Debug.Log("[IronDistrict] Создана сцена запуска: " + ScenePath);
            }

            EnsureBuildSettings(ScenePath);

            if (openIt && SceneManager.GetActiveScene().path != ScenePath &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            if (SessionState.GetBool("IrD_WelcomeShown", false) == false)
            {
                SessionState.SetBool("IrD_WelcomeShown", true);
                EditorUtility.DisplayDialog("Iron District",
                    "Проект готов!\n\nНажмите Play — игра соберётся автоматически.\n\nLinux-сборка: меню Tools → Iron District → Собрать Linux.\nWindows: File → Build Settings → Windows → Build.",
                    "Поехали");
            }
        }

        public static void EnsureBuildSettings(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) scenePath = ScenePath;
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == scenePath)) return;
            var list = scenes.ToList();
            list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[IronDistrict] Сцена добавлена в Build Settings: " + scenePath);
        }

        [MenuItem("Iron District/Сбросить сохранённый мир")]
        public static void ResetWorld()
        {
            PlayerPrefs.SetInt("IrD_Seed", 0);
            PlayerPrefs.Save();
            Debug.Log("[IronDistrict] Сид сброшен — следующий запуск сгенерирует новый мир.");
        }

        [MenuItem("Iron District/Открыть папку сборки")]
        public static void OpenBuildFolder()
        {
            System.IO.Directory.CreateDirectory("build");
            EditorUtility.RevealInFinder("build");
        }
    }
}
#endif
