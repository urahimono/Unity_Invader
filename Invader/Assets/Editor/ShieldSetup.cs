using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class ShieldSetup
    {
        private const string SpriteFolder = "Assets/Sprites/Shield";
        private const string SpritePath   = SpriteFolder + "/Shield.png";
        private const string PrefabPath   = ComponentGameplayMigration.PrefabPath + "/Shield.prefab";

        [MenuItem("Unity Invader/Configure Shields")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Shields.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var sprite  = CreateInitialSprite();
            var prefab  = CreatePrefab(sprite);
            var scene   = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            var shields = UnityEngine.Object.FindObjectsByType<Shield>(FindObjectsInactive.Include)
                .OrderBy(inShield => inShield.transform.position.x).ToArray();
            if (drivers.Length != 1 || (shields.Length != 0 && shields.Length != 4))
            {
                throw new InvalidOperationException("Arcade requires one Driver and zero or four Shields.");
            }
            if (shields.Length == 0)
            {
                shields = new Shield[4];
                for (int index = 0; index < shields.Length; index++)
                {
                    var instance                = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.name               = "Shield " + (index + 1);
                    instance.transform.position = new Vector3(32f + index * 46f - ArcadeViewport.ScreenWidth / 2f,
                        48f - ArcadeViewport.ScreenHeight / 2f, 0f) * ArcadeViewport.UnitsPerPixel;
                    shields[index] = instance.GetComponent<Shield>();
                }
            }
            var data       = new SerializedObject(drivers[0]);
            var references = data.FindProperty("_shields");

            references.arraySize = shields.Length;
            for (int index = 0; index < shields.Length; index++)
            {
                references.GetArrayElementAtIndex(index).objectReferenceValue = shields[index];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save Shields in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SHIELD_SETUP_PASS: four Shield prefab instances and Driver references saved; hand-authored provisional 22x16 shape.");
        }

        private static Sprite CreateInitialSprite()
        {
            if (AssetDatabase.IsValidFolder(SpriteFolder) == false)
            {
                AssetDatabase.CreateFolder("Assets/Sprites", "Shield");
            }
            if (File.Exists(SpritePath) == false)
            {
                var texture = new Texture2D(Shield.Width, Shield.Height, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[Shield.Width * Shield.Height];
                    for (int y = 0; y < Shield.Height; y++)
                    {
                        for (int x = 0; x < Shield.Width; x++)
                        {
                            // New drawing, not ROM bitmap data: clipped top corners and a lower arch.
                            int inset  = Mathf.Max(0, y - 11);
                            int notch  = y < 4 ? 6 : y == 4 ? 7 : 8;
                            bool solid = x >= inset && x < Shield.Width - inset &&
                                (y >= 6 || x < notch || x >= Shield.Width - notch);
                            pixels[y * Shield.Width + x] = solid ? new Color32(64, 240, 64, 255) : new Color32(0, 0, 0, 0);
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                AssetDatabase.ImportAsset(SpritePath);
            }
            var importer                 = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f / ArcadeViewport.UnitsPerPixel;
            importer.filterMode          = FilterMode.Point;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.mipmapEnabled       = false;
            importer.isReadable          = true;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.npotScale           = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot     = Vector2.zero;
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static GameObject CreatePrefab(Sprite inSprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (inSprite == null || material == null)
            {
                throw new InvalidOperationException("Shield sprite and existing URP unlit material are required.");
            }
            var instance = new GameObject("Shield");
            try
            {
                instance.AddComponent<Shield>();
                var renderer            = instance.GetComponent<SpriteRenderer>();
                renderer.sprite         = inSprite;
                renderer.sharedMaterial = material;
                var collider       = instance.GetComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.enabled   = false;
                collider.size      = new Vector2(Shield.Width, Shield.Height) * ArcadeViewport.UnitsPerPixel;
                collider.offset    = collider.size * 0.5f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                if (prefab == null)
                {
                    throw new IOException("Could not save Shield prefab.");
                }
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
