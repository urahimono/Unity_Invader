using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class UfoSetup
    {
        public const string PrefabPath = ComponentGameplayMigration.PrefabPath + "/Ufo.prefab";

        private const string SpritePath = ComponentGameplayMigration.PrefabPath + "/Ufo.png";

        [MenuItem("Unity Invader/Configure UFO")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring UFO.");
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
                throw new InvalidOperationException("Could not save UFO reference in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("UFO_SETUP_PASS: generated original placeholder sprite, prefab and Arcade reference.");
        }

        internal static void ConfigureInScene(Scene inScene, InvaderFormation inFormation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("UFO setup is an edit-time operation.");
            }
            var existing = UnityEngine.Object.FindObjectsByType<Ufo>(FindObjectsInactive.Include);
            if (existing.Length > 1 || (existing.Length == 1 && existing[0].gameObject.scene != inScene))
            {
                throw new InvalidOperationException("Expected at most one UFO in the target Scene.");
            }
            var prefab = CreatePrefab();
            var ufo    = existing.Length == 1 ? existing[0] :
                ((GameObject)PrefabUtility.InstantiatePrefab(prefab, inScene)).GetComponent<Ufo>();
            ufo.gameObject.SetActive(false);
            var data                                       = new SerializedObject(inFormation);
            data.FindProperty("_ufo").objectReferenceValue = ufo;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                return prefab;
            }
            var sprite   = CreateSprite();
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (sprite == null || material == null)
            {
                throw new InvalidOperationException("UFO requires the generated sprite and existing unlit material.");
            }
            var root = new GameObject("Ufo");
            root.layer = GameplayCollisionSetup.InvaderLayer;
            try
            {
                var ufo            = root.AddComponent<Ufo>();
                var body           = root.GetComponent<Rigidbody2D>();
                body.bodyType      = RigidbodyType2D.Kinematic;
                body.gravityScale  = 0f;
                body.constraints   = RigidbodyConstraints2D.FreezeRotation;
                body.simulated     = false;
                var collider       = root.GetComponent<BoxCollider2D>();
                collider.size      = new Vector2(Ufo.Width, Ufo.Height) * ArcadeViewport.UnitsPerPixel;
                collider.isTrigger = true;
                collider.enabled   = false;

                var renderer                                        = root.AddComponent<SpriteRenderer>();
                renderer.sprite                                     = sprite;
                renderer.sharedMaterial                             = material;
                renderer.color                                      = new Color(1f, 0.15f, 0.15f);
                renderer.enabled                                    = false;
                var data                                            = new SerializedObject(ufo);
                data.FindProperty("_renderer").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Sprite CreateSprite()
        {
            if (File.Exists(SpritePath) == false)
            {
                // Hand-authored placeholder; no ROM graphics or extracted image data.
                string[] rows =
                {
                    ".........######.........",
                    "........########........",
                    ".....##############.....",
                    "..####################..",
                    "########################",
                    "..###..###....###..###..",
                    "....##............##....",
                    "........................"
                };
                var texture = new Texture2D(24, 8, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < rows.Length; y++)
                    {
                        for (int x = 0; x < rows[y].Length; x++)
                        {
                            texture.SetPixel(x, rows.Length - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
                        }
                    }
                    texture.Apply();
                    File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                AssetDatabase.ImportAsset(SpritePath);
                var importer                 = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f / ArcadeViewport.UnitsPerPixel;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }
    }
}
