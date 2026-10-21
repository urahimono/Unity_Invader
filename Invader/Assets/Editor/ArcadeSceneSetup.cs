using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class ArcadeSceneSetup
    {
        private const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        private const string ArcadePath = "Assets/Scenes/Arcade.unity";

        [MenuItem("Unity Invader/Create Initial Scenes")]
        public static void CreateScenes()
        {
            // Never overwrite scenes that may contain subsequent issue work.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath) != null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ArcadePath) != null)
            {
                throw new InvalidOperationException("Bootstrap or Arcade already exists.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            EditorSceneManager.SaveScene(bootstrap, BootstrapPath);

            var arcade = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();
            new GameObject("GameState").AddComponent<GameStateController>();
            EditorSceneManager.SaveScene(arcade, ArcadePath);

            // Preserve the sample scene entry while making Bootstrap the entry point.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(ArcadePath, true)
            };
            scenes.AddRange(EditorBuildSettings.scenes);
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(BootstrapPath);
            Debug.Log("Bootstrap and Arcade scenes created.");
        }
    }
}
