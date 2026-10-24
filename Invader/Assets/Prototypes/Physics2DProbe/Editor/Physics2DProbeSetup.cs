using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Prototypes.Physics2DProbe.Editor
{
    public static class Physics2DProbeSetup
    {
        public const string GeneratedPath     = "Assets/Prototypes/Physics2DProbe/Generated";
        public const string ScenePath         = GeneratedPath + "/Physics2DProbe.unity";
        public const string InvaderPrefabPath = GeneratedPath + "/Physics2DProbeInvader.prefab";
        public const string ShotPrefabPath    = GeneratedPath + "/Physics2DProbeShot.prefab";

        [MenuItem("Unity Invader/Prototypes/Physics2D/Create or Open Probe Scene")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before generating the probe scene.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return; // Keep Inspector edits to existing scene and prefabs.
            }
            if (AssetDatabase.IsValidFolder(GeneratedPath) == false)
            {
                AssetDatabase.CreateFolder("Assets/Prototypes/Physics2DProbe", "Generated");
            }

            var scene    = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sprite   = CreateSprite();
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (material == null)
            {
                throw new InvalidOperationException("The existing URP Sprite-Unlit-Default material is missing.");
            }
            var invader     = CreateInvaderPrefab(sprite, material);
            var shot        = CreateShotPrefab(sprite, material);
            var root        = new GameObject("Physics2D Probe (not Arcade)");
            var single      = CreateFormation("LEFT - single target", root.transform, new Vector2(-3f, 2f), invader,
                new[] { Vector2.zero });
            var shared      = CreateFormation("RIGHT - shared edge", root.transform, new Vector2(3f, 2f), invader,
                new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) });
            var leftMuzzle  = CreateMuzzle("LEFT muzzle", root.transform, new Vector2(-3f, -2f));
            var rightMuzzle = CreateMuzzle("RIGHT muzzle (shared edge)", root.transform, new Vector2(3f, -2f));
            var arena       = root.AddComponent<Physics2DProbeArena>();
            var serialized  = new SerializedObject(arena);

            serialized.FindProperty("_singleFormation").objectReferenceValue = single;
            serialized.FindProperty("_sharedFormation").objectReferenceValue = shared;
            serialized.FindProperty("_shotPrefab").objectReferenceValue      = shot;
            serialized.FindProperty("_singleMuzzle").objectReferenceValue    = leftMuzzle;
            serialized.FindProperty("_sharedMuzzle").objectReferenceValue    = rightMuzzle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CreateCamera();
            if (EditorSceneManager.SaveScene(scene, ScenePath) == false)
            {
                throw new IOException("Could not save the Physics2D probe scene.");
            }
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log($"PHYSICS2D_PROBE_SETUP_PASS: {ScenePath}. Enter Play Mode and use the Game view buttons.");
        }

        internal static void ConfigureFormation(Physics2DProbeFormation inFormation,
            Physics2DProbeInvader inPrefab, Vector2[] inPositions)
        {
            var serialized = new SerializedObject(inFormation);

            serialized.FindProperty("_invaderPrefab").objectReferenceValue = inPrefab;

            var positions       = serialized.FindProperty("_positions");
            positions.arraySize = inPositions.Length;
            for (int index = 0; index < inPositions.Length; index++)
            {
                positions.GetArrayElementAtIndex(index).vector2Value = inPositions[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite CreateSprite()
        {
            string path = GeneratedPath + "/ProbeSquare.png";
            if (File.Exists(path) == false)
            {
                var texture = new Texture2D(8, 8);
                try
                {
                    var pixels = new Color[64];
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        pixels[index] = Color.white;
                    }
                    texture.SetPixels(pixels);
                    texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                AssetDatabase.ImportAsset(path);
                var importer                 = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 8f;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled       = false;
                var settings                 = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Physics2DProbeInvader CreateInvaderPrefab(Sprite inSprite, Material inMaterial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(InvaderPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<Physics2DProbeInvader>();
            }
            var instance = new GameObject("Physics2DProbeInvader");
            try
            {
                instance.AddComponent<Physics2DProbeInvader>();
                instance.GetComponent<BoxCollider2D>().size = Vector2.one;
                ConfigureRenderer(instance, inSprite, inMaterial, Vector2.one, new Color(0.25f, 0.9f, 0.5f));
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, InvaderPrefabPath);
                return prefab.GetComponent<Physics2DProbeInvader>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static Physics2DProbeShot CreateShotPrefab(Sprite inSprite, Material inMaterial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShotPrefabPath);
            if (existing != null)
            {
                return existing.GetComponent<Physics2DProbeShot>();
            }
            var instance = new GameObject("Physics2DProbeShot");
            try
            {
                instance.AddComponent<Physics2DProbeShot>();
                var body                    = instance.GetComponent<Rigidbody2D>();
                body.bodyType               = RigidbodyType2D.Dynamic;
                body.gravityScale           = 0f;
                body.linearDamping          = 0f;
                body.constraints            = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                var collider                = instance.GetComponent<BoxCollider2D>();
                collider.isTrigger          = true;
                collider.size               = new Vector2(0.35f, 0.6f);
                ConfigureRenderer(instance, inSprite, inMaterial, collider.size, new Color(1f, 0.85f, 0.2f));
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShotPrefabPath);
                return prefab.GetComponent<Physics2DProbeShot>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigureRenderer(GameObject inObject, Sprite inSprite, Material inMaterial,
            Vector2 inSize, Color inColor)
        {
            var renderer        = inObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = inObject.AddComponent<SpriteRenderer>();
            }
            renderer.sprite         = inSprite;
            renderer.sharedMaterial = inMaterial;
            renderer.drawMode       = SpriteDrawMode.Sliced;
            renderer.size           = inSize;
            renderer.color          = inColor;
        }

        private static Physics2DProbeFormation CreateFormation(string inName, Transform inParent,
            Vector2 inPosition, Physics2DProbeInvader inPrefab, Vector2[] inPositions)
        {
            var instance = new GameObject(inName);
            instance.transform.SetParent(inParent, false);
            instance.transform.localPosition = inPosition;
            var formation                    = instance.AddComponent<Physics2DProbeFormation>();
            ConfigureFormation(formation, inPrefab, inPositions);
            return formation;
        }

        private static Transform CreateMuzzle(string inName, Transform inParent, Vector2 inPosition)
        {
            var instance = new GameObject(inName);
            instance.transform.SetParent(inParent, false);
            instance.transform.localPosition = inPosition;
            return instance.transform;
        }

        private static void CreateCamera()
        {
            var instance                = new GameObject("Probe Camera");
            var camera                  = instance.AddComponent<Camera>();
            instance.transform.position = new Vector3(0f, 2f, -10f);
            camera.orthographic         = true;
            camera.orthographicSize     = 5f;
            camera.clearFlags           = CameraClearFlags.SolidColor;
            camera.backgroundColor      = new Color(0.03f, 0.04f, 0.08f);
        }
    }
}
