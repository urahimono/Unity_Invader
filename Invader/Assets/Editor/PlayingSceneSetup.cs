using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayingSceneSetup
    {
        [MenuItem("Unity Invader/Configure Playing Controller")]
        public static void Configure()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
            if (state == null) throw new InvalidOperationException("Arcade GameState is missing.");
            // Preserve Arcade's existing enabled development start path.
            ConfigureInScene(state);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PLAYING_SCENE_SETUP_PASS: one PlayingController owns input/clock and development start.");
        }

        internal static void ConfigureInScene(GameStateController state)
        {
            var controllers = UnityEngine.Object.FindObjectsByType<PlayingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers.Length > 1) throw new InvalidOperationException("Arcade must have only one PlayingController.");
            var controller = controllers.Length == 1 ? controllers[0] : state.gameObject.AddComponent<PlayingController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameState").objectReferenceValue = state;
            serialized.FindProperty("developmentStartPlaying").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
