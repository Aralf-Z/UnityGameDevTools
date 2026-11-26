using GameConsole;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDevTools.Development
{
    public static class DevelopmentSceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/Development.unity";

        /// <summary> 创建本仓库的最小调试场景, 已存在时直接打开. </summary>
        [MenuItem("GameDevTools/Open Development Scene")]
        public static void OpenScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH) != null)
            {
                EditorSceneManager.OpenScene(SCENE_PATH);
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var controller = new GameObject("Game Dev Tools");
            controller.AddComponent<GameDevToolsController>();
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE_PATH, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
