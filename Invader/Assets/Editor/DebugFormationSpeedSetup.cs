using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class DebugFormationSpeedSetup
    {
        [MenuItem("Unity Invader/Configure Debug Formation Speed")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Debug Formation Speed.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene      = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var formations = UnityEngine.Object.FindObjectsByType<InvaderFormation>(FindObjectsInactive.Include);
            if (formations.Length != 1)
            {
                throw new InvalidOperationException("Arcade requires exactly one InvaderFormation.");
            }
            if (formations[0].GetComponent<DebugFormationSpeed>() == null)
            {
                formations[0].gameObject.AddComponent<DebugFormationSpeed>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save Debug Formation Speed in Arcade.");
            }
            Debug.Log("DEBUG_FORMATION_SPEED_SETUP_PASS: F input component saved on existing Formation.");
        }
    }
}
