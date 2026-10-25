using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class DebugStatusOverlaySetup
    {
        [MenuItem("Unity Invader/Configure Debug Status Overlay")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring the debug overlay.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene   = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (drivers.Length != 1)
            {
                throw new InvalidOperationException("Arcade requires exactly one GameplayDriver.");
            }
            if (drivers[0].GetComponent<DebugStatusOverlay>() == null)
            {
                drivers[0].gameObject.AddComponent<DebugStatusOverlay>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the debug overlay in Arcade.");
            }
            Debug.Log("DEBUG_STATUS_OVERLAY_SETUP_PASS: overlay saved on GameplayDriver.");
        }
    }
}
