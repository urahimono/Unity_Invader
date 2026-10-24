using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayingSceneSetup
    {
        [MenuItem("Unity Invader/Configure Gameplay Driver")]
        public static void Configure()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
            if (state == null)
            {
                throw new InvalidOperationException("Arcade GameState is missing.");
            }
            // Preserve Arcade's existing enabled development start path.
            ConfigureInScene(state);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PLAYING_SCENE_SETUP_PASS: one GameplayDriver owns input/clock and development start.");
        }

        internal static void ConfigureInScene(GameStateController inState)
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers.Length > 1)
            {
                throw new InvalidOperationException("Arcade must have only one GameplayDriver.");
            }
            var controller                                                = controllers.Length == 1 ? controllers[0] : inState.gameObject.AddComponent<GameplayDriver>();
            var serialized                                                = new SerializedObject(controller);
            serialized.FindProperty("_gameState").objectReferenceValue    = inState;
            serialized.FindProperty("_developmentStartPlaying").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
