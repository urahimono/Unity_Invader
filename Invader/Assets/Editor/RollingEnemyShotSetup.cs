using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class RollingEnemyShotSetup
    {
        public const string PrefabPath = ComponentGameplayMigration.PrefabPath + "/EnemyShot.prefab";

        [MenuItem("Unity Invader/Configure Rolling EnemyShot")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Rolling EnemyShot.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene      = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var formations = UnityEngine.Object.FindObjectsByType<InvaderFormation>(FindObjectsInactive.Include);
            if (formations.Length != 1)
            {
                throw new InvalidOperationException("Arcade must contain one existing InvaderFormation.");
            }
            ConfigureInScene(scene, formations[0]);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save Rolling EnemyShot references in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ROLLING_ENEMY_SHOT_SETUP_PASS: EnemyShot prefab and Arcade references saved.");
        }

        internal static void ConfigureInScene(Scene inScene, InvaderFormation inFormation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Rolling EnemyShot setup is an edit-time operation.");
            }
            // Reuse the existing generated white pixel; do not add extracted original-game graphics.
            var sprite   = AssetDatabase.LoadAssetAtPath<Sprite>(ComponentGameplayMigration.PrefabPath + "/PlayerShot.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (sprite == null || material == null || inFormation == null)
            {
                throw new InvalidOperationException("Existing gameplay sprite/material and Formation reference are required.");
            }
            var existing = Array.FindAll(UnityEngine.Object.FindObjectsByType<EnemyShot>(FindObjectsInactive.Include),
                inShot => inShot.gameObject.name == "EnemyShot");
            if (existing.Length > 1 || (existing.Length == 1 && existing[0].gameObject.scene != inScene))
            {
                throw new InvalidOperationException("Expected at most one EnemyShot in the target scene.");
            }
            var prefab = CreatePrefab(sprite, material);
            if (prefab == null || prefab.GetComponent<EnemyShot>() == null)
            {
                throw new InvalidOperationException("EnemyShot prefab could not be created.");
            }
            var shot             = existing.Length == 1 ? existing[0] :
                ((GameObject)PrefabUtility.InstantiatePrefab(prefab, inScene)).GetComponent<EnemyShot>();
            shot.gameObject.name = "EnemyShot";
            shot.gameObject.SetActive(false);
            var data                                               = new SerializedObject(inFormation);
            data.FindProperty("_rollingShot").objectReferenceValue = shot;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrefab(Sprite inSprite, Material inMaterial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing; // Preserve Inspector adjustments on subsequent configuration runs.
            }
            var instance = new GameObject("EnemyShot");
            try
            {
                var shot           = instance.AddComponent<EnemyShot>();
                var body           = instance.GetComponent<Rigidbody2D>();
                body.bodyType      = RigidbodyType2D.Kinematic;
                body.gravityScale  = 0f;
                body.constraints   = RigidbodyConstraints2D.FreezeRotation;
                body.simulated     = false;
                var collider       = instance.GetComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size      = new Vector2(EnemyShot.Width, EnemyShot.Height) * ArcadeViewport.UnitsPerPixel;
                var visual         = new GameObject("Visual");
                visual.transform.SetParent(instance.transform, false);
                var renderer                                        = visual.AddComponent<SpriteRenderer>();
                renderer.sprite                                     = inSprite;
                renderer.sharedMaterial                             = inMaterial;
                renderer.drawMode                                   = SpriteDrawMode.Simple;
                renderer.color                                      = new Color(1f, 0.35f, 0.15f);
                renderer.enabled                                    = false;
                var size                                            = inSprite.bounds.size;
                visual.transform.localScale                         = new Vector3(collider.size.x / size.x, collider.size.y / size.y, 1f);
                var data                                            = new SerializedObject(shot);
                data.FindProperty("_renderer").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                instance.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
