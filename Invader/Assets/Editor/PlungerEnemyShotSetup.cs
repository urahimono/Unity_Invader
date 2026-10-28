using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class PlungerEnemyShotSetup
    {
        public const string PrefabPath = ComponentGameplayMigration.PrefabPath + "/PlungerEnemyShot.prefab";

        [MenuItem("Unity Invader/Configure Plunger EnemyShot")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Plunger EnemyShot.");
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
                throw new InvalidOperationException("Could not save Plunger references in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLUNGER_SETUP_PASS: independent EnemyShot prefab and Arcade reference saved.");
        }

        internal static void ConfigureInScene(Scene inScene, InvaderFormation inFormation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Plunger setup is an edit-time operation.");
            }
            var data    = new SerializedObject(inFormation);
            var rolling = data.FindProperty("_enemyShots").FindPropertyRelative("_rollingShot").objectReferenceValue as EnemyShot;
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
                    root.name = "PlungerEnemyShot";
                    root.GetComponentInChildren<SpriteRenderer>(true).color = new Color(0.2f, 0.85f, 1f);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            var shot = data.FindProperty("_enemyShots").FindPropertyRelative("_plungerShot").objectReferenceValue as EnemyShot;
            if (shot == null)
            {
                // The migration menu replaces Formation but keeps existing enemy shot roots.
                var existing = Array.FindAll(UnityEngine.Object.FindObjectsByType<EnemyShot>(FindObjectsInactive.Include),
                    inShot => inShot.gameObject.scene == inScene && inShot.gameObject.name == "PlungerEnemyShot");
                if (existing.Length > 1)
                {
                    throw new InvalidOperationException("Expected at most one Plunger slot in the target Scene.");
                }
                shot = existing.Length == 1 ? existing[0] : null;
            }
            if (shot == rolling || (shot != null && shot.gameObject.scene != inScene))
            {
                throw new InvalidOperationException("Plunger must have its own slot in the target Scene.");
            }
            if (shot == null)
            {
                shot = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, inScene)).GetComponent<EnemyShot>();
            }
            shot.gameObject.SetActive(false);
            data.FindProperty("_enemyShots").FindPropertyRelative("_plungerShot").objectReferenceValue = shot;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
