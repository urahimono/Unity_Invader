using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PlayerSceneSetup
    {
        [MenuItem("Unity Invader/Configure Player Movement")]
        public static void Configure()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }

            const string texturePath = "Assets/Sprites/Player/Player.png";
            var importer             = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Player.png is missing.");
            }
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.spritePivot         = new Vector2(0.5f, 0.5f);
            var textureSettings          = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType  = SpriteMeshType.FullRect;
            textureSettings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(textureSettings);
            importer.maxTextureSize = 2048;
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            importer.spritePixelsPerUnit = width / (float)PlayerBody.SpriteSize;
            importer.SaveAndReimport();

            var scene  = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var state  = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            var camera = Camera.main;
            if (state == null || camera == null)
            {
                throw new InvalidOperationException("Arcade foundation missing.");
            }
            camera.orthographicSize = PlayerBody.ScreenHeight / 2f;
            if (camera.GetComponent<ArcadeViewport>() == null)
            {
                camera.gameObject.AddComponent<ArcadeViewport>();
            }

            var player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
            }
            var renderer = player.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = player.AddComponent<SpriteRenderer>();
            }
            renderer.sprite           = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            renderer.sharedMaterial   = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            renderer.enabled          = false;
            player.transform.position = new Vector3(0f, 24f - PlayerBody.ScreenHeight / 2f, 0f);
            var controller            = player.GetComponent<PlayerController>();
            if (controller == null)
            {
                controller = player.AddComponent<PlayerController>();
            }
            var serialized                                                  = new SerializedObject(controller);
            serialized.FindProperty("_gameplayDriver").objectReferenceValue = state;
            serialized.FindProperty("_playerRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PlayingSceneSetup.ConfigureInScene();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER_SCENE_SETUP_PASS");
        }
    }
}
