using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class EnemyDefeatEffectsSetup
    {
        [MenuItem("Unity Invader/Configure Enemy Defeat Effects")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring enemy effects.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            Sprite invaderSprite = CreateSprite("InvaderBurst", new[]
            {
                "10000010000001", "00100010001000", "00010100110000", "01001111100100",
                "00011000111000", "01001111100100", "00110010011000", "10000010000001"
            });
            Sprite ufoSprite = CreateSprite("UfoBurst", new[]
            {
                "10000000100000000001", "00010000100000100000", "00000101000101000000", "01000011111100001000",
                "00011110000111100000", "01000011111100001000", "00010100001000100000", "10000001000000000001"
            });
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var driver = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include).Single();
            var formation = UnityEngine.Object.FindObjectsByType<InvaderFormation>(FindObjectsInactive.Include).Single();
            var ufo = UnityEngine.Object.FindObjectsByType<Ufo>(FindObjectsInactive.Include).Single();
            var roots = scene.GetRootGameObjects().Where(inRoot => inRoot.name == "Enemy Defeat Effects").ToArray();
            if (roots.Length > 1)
            {
                throw new InvalidOperationException("Multiple enemy effect roots.");
            }
            var root = roots.Length == 1 ? roots[0] : new GameObject("Enemy Defeat Effects", typeof(EnemyDefeatEffects));
            var effect = root.GetComponent<EnemyDefeatEffects>();
            var data = new SerializedObject(effect);
            data.FindProperty("_driver").objectReferenceValue = driver;
            data.FindProperty("_ufo").objectReferenceValue = ufo;
            data.FindProperty("_invaderSprite").objectReferenceValue = invaderSprite;
            data.FindProperty("_ufoSprite").objectReferenceValue = ufoSprite;
            var slots = data.FindProperty("_slots");
            slots.arraySize = InvaderFormation.Rows * InvaderFormation.Columns + 1;
            var source = ufo.GetComponent<SpriteRenderer>();
            for (int i = 0; i < slots.arraySize; i++)
            {
                string name = $"Burst {i:00}";
                var child = root.transform.Find(name);
                if (child == null)
                {
                    child = new GameObject(name, typeof(SpriteRenderer)).transform;
                    child.SetParent(root.transform, false);
                }
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder + 1;
                renderer.sprite = invaderSprite;
                renderer.enabled = false;
                slots.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var formationData = new SerializedObject(formation);
            formationData.FindProperty("_defeatEffects").objectReferenceValue = effect;
            formationData.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save enemy effects.");
            }
            AssetDatabase.SaveAssets();
            ComponentGameplayVerification.Verify();
            Debug.Log("ENEMY_DEFEAT_EFFECTS_SAVED: original sprites and 56 independent renderers.");
        }

        private static Sprite CreateSprite(string inName, string[] inRows)
        {
            const string directory = "Assets/Sprites/EnemyEffects";
            Directory.CreateDirectory(directory);
            string path = $"{directory}/{inName}.png";
            if (File.Exists(path) == false)
            {
                var texture = new Texture2D(inRows[0].Length, inRows.Length, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < inRows.Length; y++)
                    {
                        for (int x = 0; x < inRows[y].Length; x++)
                        {
                            texture.SetPixel(x, inRows.Length - 1 - y, inRows[y][x] == '1' ? Color.white : Color.clear);
                        }
                    }
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f / ArcadeViewport.UnitsPerPixel;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
