using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayerHitSetup
    {
        private const string PrefabPath = ComponentGameplayMigration.PrefabPath + "/Player.prefab";

        [MenuItem("Unity Invader/Configure Player Hit")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Player Hit.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            ConfigurePrefab();
            var scene   = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include);
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (players.Length != 1 || drivers.Length != 1)
            {
                throw new InvalidOperationException("Arcade must contain one Player and GameplayDriver.");
            }
            var data                                          = new SerializedObject(players[0]);
            data.FindProperty("_driver").objectReferenceValue = drivers[0];
            data.ApplyModifiedPropertiesWithoutUndo();
            var driverData                             = new SerializedObject(drivers[0]);
            driverData.FindProperty("_lives").intValue = 3;
            driverData.ApplyModifiedPropertiesWithoutUndo();
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save Player Hit references in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_HIT_SETUP_PASS: Player collider, Arcade driver reference and three initial lives saved.");
        }

        internal static void ConfigurePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Player Hit configuration is an edit-time operation.");
            }
            var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var renderer = instance.GetComponent<SpriteRenderer>();
                if (instance.GetComponent<Player>() == null || renderer == null || renderer.sprite == null)
                {
                    throw new InvalidOperationException("The existing Player prefab and Sprite are required.");
                }
                var collider = instance.GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    collider = instance.AddComponent<BoxCollider2D>();
                    // Sprite bounds are in the same local space; preserve the existing root scale.
                    collider.size   = renderer.sprite.bounds.size;
                    collider.offset = renderer.sprite.bounds.center;
                }
                collider.isTrigger = true;
                collider.enabled   = false;
                if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not save the Player collider.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }
    }
}
