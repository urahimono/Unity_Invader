using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class SquigglyEnemyShotSetup
    {
        public const string PrefabPath = ComponentGameplayMigration.PrefabPath + "/SquigglyEnemyShot.prefab";

        [MenuItem("Unity Invader/Configure Squiggly EnemyShot")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Squiggly EnemyShot.");
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
            ConfigureInScene(scene, formations[0]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save Squiggly references in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SQUIGGLY_SETUP_PASS: independent EnemyShot prefab and Arcade reference saved.");
        }

        internal static void ConfigureInScene(Scene inScene, InvaderFormation inFormation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Squiggly setup is an edit-time operation.");
            }
            var data    = new SerializedObject(inFormation);
            var rolling = data.FindProperty("_rollingShot").objectReferenceValue as EnemyShot;
            if (rolling == null)
            {
                throw new InvalidOperationException("Configure Rolling EnemyShot first.");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                // Reuse the existing EnemyShot component, collider and generated white sprite.
                var root = PrefabUtility.LoadPrefabContents(RollingEnemyShotSetup.PrefabPath);
                try
                {
                    root.name = "SquigglyEnemyShot";

                    root.GetComponentInChildren<SpriteRenderer>(true).color = new Color(0.85f, 0.25f, 1f);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            var shot = data.FindProperty("_squigglyShot").objectReferenceValue as EnemyShot;
            if (shot == null)
            {
                // The migration menu replaces Formation but keeps existing enemy shot roots.
                var existing = Array.FindAll(UnityEngine.Object.FindObjectsByType<EnemyShot>(FindObjectsInactive.Include),
                    inShot => inShot.gameObject.scene == inScene && inShot.gameObject.name == "SquigglyEnemyShot");
                if (existing.Length > 1)
                {
                    throw new InvalidOperationException("Expected at most one Squiggly slot in the target Scene.");
                }
                shot = existing.Length == 1 ? existing[0] : null;
            }
            var plunger = data.FindProperty("_plungerShot").objectReferenceValue as EnemyShot;
            if (shot == rolling || shot == plunger || (shot != null && shot.gameObject.scene != inScene))
            {
                throw new InvalidOperationException("Squiggly must have its own slot in the target Scene.");
            }
            if (shot == null)
            {
                shot = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, inScene)).GetComponent<EnemyShot>();
            }
            shot.gameObject.SetActive(false);
            data.FindProperty("_squigglyShot").objectReferenceValue = shot;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
