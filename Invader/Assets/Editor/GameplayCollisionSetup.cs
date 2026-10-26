using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class GameplayCollisionSetup
    {
        internal const int PlayerLayer     = 8;
        internal const int InvaderLayer    = 9;
        internal const int PlayerShotLayer = 10;
        internal const int EnemyShotLayer  = 11;

        [MenuItem("Unity Invader/Configure Gameplay Collisions")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring gameplay collisions.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            ConfigureInScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save gameplay collision layers.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("GAMEPLAY_COLLISIONS_SETUP_PASS: saved projectile/target layers and friendly contact exclusions.");
        }

        internal static void ConfigureInScene(Scene inScene)
        {
            var tags       = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers     = tags.FindProperty("layers");
            string[] names = { "Player", "Invader", "PlayerShot", "EnemyShot" };
            for (int index = 0; index < names.Length; index++)
            {
                string current = layers.GetArrayElementAtIndex(PlayerLayer + index).stringValue;
                if (string.IsNullOrEmpty(current) == false && current != names[index])
                {
                    throw new InvalidOperationException("Gameplay layer slot is already occupied: " + current);
                }
            }
            for (int index = 0; index < names.Length; index++)
            {
                layers.GetArrayElementAtIndex(PlayerLayer + index).stringValue = names[index];
            }
            tags.ApplyModifiedPropertiesWithoutUndo();
            Physics2D.IgnoreLayerCollision(PlayerShotLayer, PlayerLayer, true);
            Physics2D.IgnoreLayerCollision(EnemyShotLayer, InvaderLayer, true);
            Physics2D.IgnoreLayerCollision(PlayerShotLayer, InvaderLayer, false);
            Physics2D.IgnoreLayerCollision(EnemyShotLayer, PlayerLayer, false);
            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]);

            string[] prefabs    = { "Player", "Invader", "PlayerShot", "EnemyShot", "PlungerEnemyShot", "SquigglyEnemyShot", "Ufo" };
            int[] prefabLayers = { PlayerLayer, InvaderLayer, PlayerShotLayer, EnemyShotLayer, EnemyShotLayer, EnemyShotLayer, InvaderLayer };
            for (int index = 0; index < prefabs.Length; index++)
            {
                string path = ComponentGameplayMigration.PrefabPath + "/" + prefabs[index] + ".prefab";
                var root    = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.layer = prefabLayers[index];
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            // Refresh existing instances, including inactive projectile roots and layer overrides.
            foreach (var root in inScene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    int layer = component switch
                    {
                        Player => PlayerLayer,
                        Invader => InvaderLayer,
                        Ufo => InvaderLayer,
                        PlayerShot => PlayerShotLayer,
                        EnemyShot => EnemyShotLayer,
                        _ => -1
                    };
                    if (layer >= 0 && component.gameObject.layer != layer)
                    {
                        component.gameObject.layer = layer;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
                    }
                }
            }
        }
    }
}
