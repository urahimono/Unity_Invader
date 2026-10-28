using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class ComponentGameplayMigration
    {
        public const string PrefabPath = "Assets/Prefabs/Gameplay";

        [MenuItem("Unity Invader/Configure Invader Animation")]
        public static void ConfigureInvaderAnimation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring Invader animation.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            ConfigureInvaderPrefab();
            var scene     = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var formation = scene.GetRootGameObjects().SelectMany(inRoot => inRoot.GetComponentsInChildren<InvaderFormation>(true)).Single();
            // Reserialize this existing component to remove the obsolete Formation Sprite array.
            // Do not recreate any scene object or change its references/settings.
            EditorUtility.SetDirty(formation);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save Arcade Invader animation settings.");
            }
            Debug.Log("INVADER_ANIMATION_CONFIGURATION_PASS: existing prefab configured and Arcade saved without regeneration.");
        }

        [MenuItem("Unity Invader/Configure PlayerShot Prefab")]
        public static void ConfigurePlayerShotPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring PlayerShot.");
            }
            string path  = PrefabPath + "/PlayerShot.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var shot = contents.GetComponent<PlayerShot>();
                var body = contents.GetComponent<Rigidbody2D>();
                var data = new SerializedObject(shot);
                data.FindProperty("_body").objectReferenceValue = body;
                data.ApplyModifiedPropertiesWithoutUndo();
                body.simulated = false;
                ((SpriteRenderer)data.FindProperty("_renderer").objectReferenceValue).enabled = false;
                contents.SetActive(false);
                contents.layer = GameplayCollisionSetup.PlayerShotLayer;
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [MenuItem("Unity Invader/Migrate Arcade to Components")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before migrating Arcade.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Player/Player.png");
            var frames       = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Invader/InvaderSprites.png").OfType<Sprite>().ToArray();
            string[] names   = { "TopFrameA", "TopFrameB", "MiddleFrameA", "MiddleFrameB", "BottomFrameA", "BottomFrameB" };
            var ordered      = names.Select(inName => frames.SingleOrDefault(inSprite => inSprite.name == inName)).ToArray();
            var material     = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (playerSprite == null || ordered.Any(inFrame => inFrame == null) || material == null)
            {
                throw new InvalidOperationException("Existing Player sprite, six named Invader frames and URP unlit material are required.");
            }
            if (AssetDatabase.IsValidFolder("Assets/Prefabs") == false)
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (AssetDatabase.IsValidFolder(PrefabPath) == false)
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Gameplay");
            }
            var scene   = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (drivers.Length != 1)
            {
                throw new InvalidOperationException("Arcade must contain exactly one GameplayDriver.");
            }
            var camera = Camera.main;
            if (camera == null)
            {
                throw new InvalidOperationException("Arcade Main Camera is missing.");
            }
            // Capture only existing roots before creating replacements with the same names.
            var oldRoots = scene.GetRootGameObjects().Where(inRoot =>
                inRoot.name == "Player" || inRoot.name == "InvaderFormation" || inRoot.name == "PlayerShot").ToArray();

            var invaderPrefab = CreateInvaderPrefab(ordered, material);
            var shotPrefab    = CreateShotPrefab(CreateShotSprite(), material);
            var playerPrefab  = CreatePlayerPrefab(playerSprite, material);
            PlayerHitSetup.ConfigurePrefab();
            if (invaderPrefab == null || invaderPrefab.GetComponent<Invader>() == null ||
                shotPrefab == null || shotPrefab.GetComponent<PlayerShot>() == null ||
                playerPrefab == null || playerPrefab.GetComponent<Player>() == null)
            {
                throw new InvalidOperationException("Gameplay prefabs must be saved and contain their required components.");
            }
            var shotObject    = (GameObject)PrefabUtility.InstantiatePrefab(shotPrefab, scene);
            var playerObject  = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);

            shotObject.name                 = "PlayerShot";
            playerObject.name               = "Player";
            playerObject.transform.position = new Vector3(0f, 24f - ArcadeViewport.ScreenHeight / 2f, 0f) * ArcadeViewport.UnitsPerPixel;
            var playerData                  = new SerializedObject(playerObject.GetComponent<Player>());

            playerData.FindProperty("_shot").objectReferenceValue   = shotObject.GetComponent<PlayerShot>();
            playerData.ApplyModifiedPropertiesWithoutUndo();

            var formationObject = new GameObject("InvaderFormation");
            var body            = formationObject.AddComponent<Rigidbody2D>();
            body.bodyType       = RigidbodyType2D.Kinematic;
            body.gravityScale   = 0f;
            body.simulated      = false;
            var formation       = formationObject.AddComponent<InvaderFormation>();
            var formationData   = new SerializedObject(formation);

            formationData.FindProperty("_invaderPrefab").objectReferenceValue = invaderPrefab.GetComponent<Invader>();

            formationData.ApplyModifiedPropertiesWithoutUndo();
            var driverData = new SerializedObject(drivers[0]);

            driverData.FindProperty("_player").objectReferenceValue    = playerObject.GetComponent<Player>();
            driverData.FindProperty("_formation").objectReferenceValue = formation;
            driverData.FindProperty("_score").intValue                 = 0;
            driverData.FindProperty("_waveNumber").intValue            = 1;
            driverData.FindProperty("_lives").intValue                 = 3;
            driverData.ApplyModifiedPropertiesWithoutUndo();
            camera.orthographicSize = ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel;
            if (camera.GetComponent<ArcadeViewport>() == null)
            {
                camera.gameObject.AddComponent<ArcadeViewport>();
            }
            RollingEnemyShotSetup.ConfigureInScene(scene, formation);
            PlungerEnemyShotSetup.ConfigureInScene(scene, formation);
            SquigglyEnemyShotSetup.ConfigureInScene(scene, formation);
            UfoSetup.ConfigureInScene(scene, formation);
            GameplayCollisionSetup.ConfigureInScene(scene);
            // Finish preparation and reference wiring before destroying any existing gameplay object.
            foreach (var root in oldRoots)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new IOException("Could not save migrated Arcade.");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("COMPONENT_GAMEPLAY_MIGRATION_PASS: Arcade references and three prefabs saved. Run setup verification, then play Bootstrap.");
        }

        private static GameObject CreatePlayerPrefab(Sprite inSprite, Material inMaterial)
        {
            string path  = PrefabPath + "/Player.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }
            var instance = new GameObject("Player");
            instance.layer = GameplayCollisionSetup.PlayerLayer;
            try
            {
                instance.AddComponent<Player>();
                var renderer                  = instance.GetComponent<SpriteRenderer>();
                renderer.sprite               = inSprite;
                renderer.sharedMaterial       = inMaterial;
                renderer.enabled              = false;
                instance.transform.localScale = new Vector3(ArcadeViewport.UnitsPerPixel, ArcadeViewport.UnitsPerPixel, 1f);
                var collider                  = instance.AddComponent<BoxCollider2D>();
                collider.size                 = inSprite.bounds.size;
                collider.offset               = inSprite.bounds.center;
                collider.isTrigger            = true;
                collider.enabled              = false;
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigureInvaderPrefab()
        {
            var frames     = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Invader/InvaderSprites.png").OfType<Sprite>().ToArray();
            string[] names = { "TopFrameA", "TopFrameB", "MiddleFrameA", "MiddleFrameB", "BottomFrameA", "BottomFrameB" };
            var ordered    = names.Select(inName => frames.Single(inSprite => inSprite.name == inName)).ToArray();
            string path    = PrefabPath + "/Invader.prefab";
            var contents   = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureInvaderFrames(contents.GetComponent<Invader>(), ordered);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureInvaderFrames(Invader inInvader, Sprite[] inFrames)
        {
            var data             = new SerializedObject(inInvader);
            string[] frameFields = { "_topFrames", "_middleFrames", "_bottomFrames" };
            for (int type = 0; type < frameFields.Length; type++)
            {
                var frames = data.FindProperty(frameFields[type]);
                // Preserve configured arrays, including animations with more than two frames.
                if (frames.arraySize != 0)
                {
                    continue;
                }
                frames.arraySize = 2;
                frames.GetArrayElementAtIndex(0).objectReferenceValue = inFrames[type * 2];
                frames.GetArrayElementAtIndex(1).objectReferenceValue = inFrames[type * 2 + 1];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateInvaderPrefab(Sprite[] inFrames, Material inMaterial)
        {
            string path  = PrefabPath + "/Invader.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                ConfigureInvaderPrefab();
                return existing;
            }
            var instance = new GameObject("Invader");
            instance.layer = GameplayCollisionSetup.InvaderLayer;
            try
            {
                var invader                                         = instance.AddComponent<Invader>();
                instance.GetComponent<BoxCollider2D>().size         = new Vector2(16f, 8f) * ArcadeViewport.UnitsPerPixel;
                var sprite                                          = inFrames[0];
                var renderer                                        = CreateVisual(instance, sprite, inMaterial, new Vector2(sprite.bounds.size.x, sprite.bounds.size.y));
                var data                                            = new SerializedObject(invader);
                data.FindProperty("_renderer").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                ConfigureInvaderFrames(invader, inFrames);
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateShotPrefab(Sprite inSprite, Material inMaterial)
        {
            string path  = PrefabPath + "/PlayerShot.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                ConfigurePlayerShotPrefab();
                return existing;
            }
            var instance = new GameObject("PlayerShot");
            instance.layer = GameplayCollisionSetup.PlayerShotLayer;
            instance.SetActive(false);
            try
            {
                var shot                                            = instance.AddComponent<PlayerShot>();
                var body                                            = instance.GetComponent<Rigidbody2D>();
                body.bodyType                                       = RigidbodyType2D.Dynamic;
                body.gravityScale                                   = 0f;
                body.linearDamping                                  = 0f;
                body.constraints                                    = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode                         = CollisionDetectionMode2D.Discrete;
                body.simulated                                      = false;
                var collider                                        = instance.GetComponent<BoxCollider2D>();
                collider.isTrigger                                  = true;
                collider.size                                       = new Vector2(PlayerShot.Width, PlayerShot.Height) * ArcadeViewport.UnitsPerPixel;
                collider.offset                                     = Vector2.up * (PlayerShot.Height / 2f * ArcadeViewport.UnitsPerPixel);
                var renderer                                        = CreateVisual(instance, inSprite, inMaterial, new Vector2(PlayerShot.Width, PlayerShot.Height));
                renderer.transform.localPosition                    = collider.offset;
                var data                                            = new SerializedObject(shot);
                data.FindProperty("_renderer").objectReferenceValue = renderer;
                data.FindProperty("_body").objectReferenceValue     = body;
                data.ApplyModifiedPropertiesWithoutUndo();
                renderer.enabled = false;
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static SpriteRenderer CreateVisual(GameObject inParent, Sprite inSprite, Material inMaterial, Vector2 inPixelSize)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(inParent.transform, false);
            var renderer                = visual.AddComponent<SpriteRenderer>();
            renderer.sprite             = inSprite;
            renderer.sharedMaterial     = inMaterial;
            renderer.drawMode           = SpriteDrawMode.Simple;
            var size                    = inSprite.bounds.size;
            visual.transform.localScale = new Vector3(inPixelSize.x * ArcadeViewport.UnitsPerPixel / size.x,
                inPixelSize.y * ArcadeViewport.UnitsPerPixel / size.y, 1f);
            return renderer;
        }

        private static Sprite CreateShotSprite()
        {
            string path = PrefabPath + "/PlayerShot.png";
            if (File.Exists(path) == false)
            {
                var texture = new Texture2D(1, 1);
                try
                {
                    texture.SetPixel(0, 0, Color.white);
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
                importer.spritePixelsPerUnit = 1f;
                importer.filterMode          = FilterMode.Point;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled       = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
