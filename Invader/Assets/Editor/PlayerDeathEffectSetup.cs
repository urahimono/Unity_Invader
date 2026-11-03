using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayerDeathEffectSetup
    {
        private const string SpritePath = "Assets/Sprites/Player/";

        [MenuItem("Unity Invader/Configure Player Death Effect")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring the Player death effect.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            // Original, deliberately simple marks; no ROM/video/source-game image data is used.
            Sprite frameA = CreateFrame("PlayerDeathA.png", new[]
            {
                "0010000100000100", "0001000100100000", "1000100101000010", "0010011110010000",
                "0001111111100100", "0010011110010000", "0100100001001000", "1001000000100010"
            });
            Sprite frameB = CreateFrame("PlayerDeathB.png", new[]
            {
                "0000100001000000", "0100010010000100", "0010001110010000", "0001110011100001",
                "1010011110010100", "0000101100100000", "0011000000011000", "0100001000000010"
            });
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsInactive.Include);
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (players.Length != 1 || drivers.Length != 1)
            {
                throw new InvalidOperationException("Arcade requires exactly one Player and Driver.");
            }
            var roots = scene.GetRootGameObjects().Where(inRoot => inRoot.name == "Player Death Effect").ToArray();
            if (roots.Length > 1)
            {
                throw new InvalidOperationException("Arcade has multiple Player Death Effect roots.");
            }
            var root = roots.Length == 1 ? roots[0] :
                new GameObject("Player Death Effect", typeof(SpriteRenderer), typeof(PlayerDeathEffect));
            var effect = root.GetComponent<PlayerDeathEffect>();
            var renderer = root.GetComponent<SpriteRenderer>();
            if (effect == null || renderer == null || root.GetComponent<Collider2D>() != null || root.GetComponent<Rigidbody2D>() != null)
            {
                throw new InvalidOperationException("Death effect must contain only presentation components, without physics.");
            }
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            var playerRenderer = players[0].GetComponent<SpriteRenderer>();
            renderer.sharedMaterial = playerRenderer.sharedMaterial;
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder + 1;
            renderer.sprite = frameA;
            renderer.color = new Color(0.2f, 1f, 0.2f, 1f);
            renderer.enabled = false;
            var data = new SerializedObject(effect);
            data.FindProperty("_player").objectReferenceValue = players[0];
            data.FindProperty("_driver").objectReferenceValue = drivers[0];
            data.FindProperty("_renderer").objectReferenceValue = renderer;
            var frames = data.FindProperty("_frames");
            frames.arraySize = 2;
            frames.GetArrayElementAtIndex(0).objectReferenceValue = frameA;
            frames.GetArrayElementAtIndex(1).objectReferenceValue = frameB;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the Player death effect in Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_DEATH_EFFECT_SAVED: two original frames and presentation-only Scene references saved.");
        }

        private static Sprite CreateFrame(string inName, string[] inRows)
        {
            string path = SpritePath + inName;
            if (File.Exists(path) == false)
            {
                var texture = new Texture2D(16, 8, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 16; x++)
                        {
                            texture.SetPixel(x, 7 - y, inRows[y][x] == '1' ? Color.white : Color.clear);
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
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"Could not import death effect frame: {path}");
            }
            return sprite;
        }
    }
}
