using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class InvaderFormationSceneSetup
    {
        public const string TexturePath = "Assets/Sprites/Invader/InvaderSprites.png";
        public const int CellWidth = 480;
        public const int CellHeight = 241;
        public static readonly string[] FrameNames =
            { "TopFrameA", "TopFrameB", "MiddleFrameA", "MiddleFrameB", "BottomFrameA", "BottomFrameB" };

        [MenuItem("Unity Invader/Configure Invader Formation")]
        public static void Configure()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("InvaderSprites.png is missing.");
            }
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 2172 || height != 724)
            {
                throw new InvalidOperationException("Unexpected source dimensions.");
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = CellWidth / (float)InvaderFormation.CellSize;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            var rects = new SpriteRect[6];
            for (int index = 0; index < rects.Length; index++)
            {
                string name = FrameNames[index];
                rects[index] = new SpriteRect
                {
                    name = name,
                    spriteID = existing.FirstOrDefault(rect => rect.name == name)?.spriteID ?? GUID.Generate(),
                    // Unity rects start at bottom-left. Exclude source bottom row y=0.
                    rect = new Rect(index % 2 == 0 ? 540 : 1150,
                        1 + (2 - index / 2) * CellHeight, CellWidth, CellHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                };
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
            if (state == null)
            {
                throw new InvalidOperationException("Arcade GameState is missing.");
            }
            var formation = GameObject.Find("InvaderFormation");
            if (formation == null)
            {
                formation = new GameObject("InvaderFormation");
            }
            var controller = formation.GetComponent<InvaderFormationController>();
            if (controller == null)
            {
                controller = formation.AddComponent<InvaderFormationController>();
            }
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_gameState").objectReferenceValue = state;
            serialized.FindProperty("_spriteMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            serialized.FindProperty("_cellDisplayScale").vector2Value = Vector2.one;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().ToArray();
            var frames = serialized.FindProperty("_frames");
            frames.arraySize = 6;
            for (int index = 0; index < 6; index++)
            {
                frames.GetArrayElementAtIndex(index).objectReferenceValue = sprites.Single(sprite => sprite.name == FrameNames[index]);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            InvaderFormationVerification.Verify();
            Debug.Log("INVADER_FORMATION_SETUP_PASS: six 480x241 centered rects; PPU 30; unit display scale; Arcade connected.");
        }
    }
}
