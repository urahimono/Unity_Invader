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
            // Preserve Arcade's existing enabled development start path.
            ConfigureInScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("GAMEPLAY_DRIVER_SETUP_PASS: one GameplayDriver owns state/input/clock and development start.");
        }

        internal static void ConfigureInScene()
        {
            var controllers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (controllers.Length > 1)
            {
                throw new InvalidOperationException("Arcade must have only one GameplayDriver.");
            }
            var controller                                                       = controllers.Length == 1 ? controllers[0] : new GameObject("Gameplay").AddComponent<GameplayDriver>();
            var serialized                                                       = new SerializedObject(controller);
            serialized.FindProperty("_developmentStartPlaying").boolValue        = true;
            serialized.FindProperty("_playerController").objectReferenceValue    = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            serialized.FindProperty("_formationController").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
