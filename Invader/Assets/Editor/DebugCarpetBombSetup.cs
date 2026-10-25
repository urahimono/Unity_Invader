using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class DebugCarpetBombSetup
    {
        [MenuItem("Unity Invader/Configure Debug Carpet Bomb")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Debug Carpet Bomb.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var prefab  = AssetDatabase.LoadAssetAtPath<PlayerShot>(ComponentGameplayMigration.PrefabPath + "/PlayerShot.prefab");
            var scene   = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (prefab == null || drivers.Length != 1)
            {
                throw new InvalidOperationException("Existing PlayerShot prefab and one GameplayDriver are required.");
            }
            var bomb = drivers[0].GetComponent<DebugCarpetBomb>();
            if (bomb == null)
            {
                bomb = drivers[0].gameObject.AddComponent<DebugCarpetBomb>();
            }
            var data = new SerializedObject(bomb);
            data.FindProperty("_shotPrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save Debug Carpet Bomb in Arcade.");
            }
            Debug.Log("DEBUG_CARPET_BOMB_SETUP_PASS: existing PlayerShot prefab connected in Arcade.");
        }
    }
}
