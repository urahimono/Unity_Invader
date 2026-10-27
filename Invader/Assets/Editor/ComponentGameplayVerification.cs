using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    public static class ComponentGameplayVerification
    {
        [MenuItem("Unity Invader/Verify Component Gameplay Setup")]
        public static void Verify()
        {
            var scene = SceneManager.GetActiveScene();
            Require(scene.path == "Assets/Scenes/Arcade.unity", "Open migrated Arcade (or let Bootstrap load it in Play Mode).");
            var roots      = scene.GetRootGameObjects();
            var drivers    = roots.SelectMany(inRoot => inRoot.GetComponentsInChildren<GameplayDriver>(true)).ToArray();
            var players    = roots.SelectMany(inRoot => inRoot.GetComponentsInChildren<Player>(true)).ToArray();
            var shots      = roots.SelectMany(inRoot => inRoot.GetComponentsInChildren<PlayerShot>(true)).ToArray();
            var formations = roots.SelectMany(inRoot => inRoot.GetComponentsInChildren<InvaderFormation>(true)).ToArray();
            Require(drivers.Length == 1 && players.Length == 1 && shots.Length == 1 && formations.Length == 1, "Expected one driver, player, shot and formation.");
            var driverData = new SerializedObject(drivers[0]);
            Require(driverData.FindProperty("_player").objectReferenceValue == players[0], "Driver player reference.");
            Require(driverData.FindProperty("_formation").objectReferenceValue == formations[0], "Driver formation reference.");
            Require(new SerializedObject(players[0]).FindProperty("_shot").objectReferenceValue == shots[0], "Player shot reference.");
            var playerData = new SerializedObject(players[0]);
            Require(playerData.FindProperty("_renderer").objectReferenceValue == players[0].GetComponent<SpriteRenderer>() &&
                playerData.FindProperty("_hitCollider").objectReferenceValue == players[0].GetComponent<BoxCollider2D>(), "Saved Player component references.");
            Require(players[0].TryGetComponent<BoxCollider2D>(out var playerCollider) && playerCollider.isTrigger, "Player hit trigger.");
            var shotBody = shots[0].GetComponent<Rigidbody2D>();
            var collider = shots[0].GetComponent<BoxCollider2D>();
            Require((EditorApplication.isPlaying || shots[0].gameObject.activeSelf == false) && new SerializedObject(shots[0]).FindProperty("_body").objectReferenceValue == shotBody,
                "PlayerShot must start inactive with its body reference saved in the prefab.");
            var shotRenderer = new SerializedObject(shots[0]).FindProperty("_renderer").objectReferenceValue as SpriteRenderer;
            Require(shotRenderer != null, "Saved PlayerShot renderer reference.");
            if (EditorApplication.isPlaying == false)
            {
                Require(shots[0].enabled && shotBody.simulated == false && shotRenderer.enabled == false,
                    "Idle PlayerShot must be ready with physics and rendering stopped.");
            }
            Require(shotBody.bodyType == RigidbodyType2D.Dynamic && shotBody.gravityScale == 0f && shotBody.linearDamping == 0f, "Shot body settings.");
            Require(collider.isTrigger && Vector2.Distance(collider.size, new Vector2(1f, 4f) * ArcadeViewport.UnitsPerPixel) < 1e-6f, "Shot trigger dimensions.");
            Require(formations[0].GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic, "Formation body must be kinematic.");
            var formationData = new SerializedObject(formations[0]);
            var ufos          = roots.SelectMany(inRoot => inRoot.GetComponentsInChildren<Ufo>(true)).ToArray();
            Require(ufos.Length == 1 && formationData.FindProperty("_ufo").objectReferenceValue == ufos[0], "Single UFO and Formation reference.");
            Require(players[0].gameObject.layer == GameplayCollisionSetup.PlayerLayer &&
                shots[0].gameObject.layer == GameplayCollisionSetup.PlayerShotLayer &&
                ufos[0].gameObject.layer == GameplayCollisionSetup.InvaderLayer &&
                ((Invader)formationData.FindProperty("_invaderPrefab").objectReferenceValue).gameObject.layer == GameplayCollisionSetup.InvaderLayer,
                "Saved player/projectile/Invader/UFO layers.");
            Require(Physics2D.GetIgnoreLayerCollision(GameplayCollisionSetup.PlayerShotLayer, GameplayCollisionSetup.PlayerLayer) &&
                Physics2D.GetIgnoreLayerCollision(GameplayCollisionSetup.EnemyShotLayer, GameplayCollisionSetup.InvaderLayer) &&
                Physics2D.GetIgnoreLayerCollision(GameplayCollisionSetup.PlayerShotLayer, GameplayCollisionSetup.InvaderLayer) == false &&
                Physics2D.GetIgnoreLayerCollision(GameplayCollisionSetup.EnemyShotLayer, GameplayCollisionSetup.PlayerLayer) == false,
                "Friendly contacts excluded; opposing contacts enabled.");
            Require(ufos[0].GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic &&
                ufos[0].GetComponent<BoxCollider2D>().isTrigger && ufos[0].GetComponent<SpriteRenderer>().sprite != null,
                "UFO physics and sprite settings.");
            var rolling       = formationData.FindProperty("_rollingShot").objectReferenceValue as EnemyShot;
            var plunger       = formationData.FindProperty("_plungerShot").objectReferenceValue as EnemyShot;
            var squiggly      = formationData.FindProperty("_squigglyShot").objectReferenceValue as EnemyShot;
            Require(rolling != null && plunger != null && squiggly != null && rolling != plunger &&
                rolling != squiggly && plunger != squiggly, "Three independent enemy shot slots.");
            foreach (var enemyShot in new[] { rolling, plunger, squiggly })
            {
                Require(enemyShot.gameObject.layer == GameplayCollisionSetup.EnemyShotLayer, "Saved enemy projectile layer.");
                Require(enemyShot.gameObject.scene == scene, "EnemyShot belongs to Arcade.");
                Require(enemyShot.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic &&
                    enemyShot.GetComponent<BoxCollider2D>().isTrigger, "EnemyShot physics settings.");
            }
            Require(Mathf.Approximately(Camera.main.orthographicSize, ArcadeViewport.ScreenHeight / 2f * ArcadeViewport.UnitsPerPixel), "Camera pixel scale.");
            foreach (var root in roots)
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Missing script: " + child.name);
                }
            }
            if (EditorApplication.isPlaying)
            {
                var invaders = formations[0].GetComponentsInChildren<Invader>(true);
                Require(invaders.Length == InvaderFormation.Rows * InvaderFormation.Columns, "Expected exactly 55 reused Invaders.");
                int aliveCount = new SerializedObject(formations[0]).FindProperty("_aliveCount").intValue;
                Require(aliveCount == invaders.Count(inInvader => inInvader.gameObject.activeSelf), "Active objects and alive aggregate differ.");
                Debug.Log($"COMPONENT_GAMEPLAY_STATE: score={driverData.FindProperty("_score").intValue}, wave={driverData.FindProperty("_waveNumber").intValue}, alive={aliveCount}");
            }
            Debug.Log("COMPONENT_GAMEPLAY_SETUP_PASS: references, physics bodies, dimensions and scripts. This is not an input/contact/Wave gameplay test.");
        }

        [MenuItem("Unity Invader/Component Gameplay/Pause (Attract)")]
        public static void Pause()
        {
            SetState(GameState.Attract);
        }

        [MenuItem("Unity Invader/Component Gameplay/Resume (Playing)")]
        public static void Resume()
        {
            SetState(GameState.Playing);
        }

        private static void SetState(GameState inState)
        {
            Require(EditorApplication.isPlaying, "Enter Play Mode first.");
            var driver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            Require(driver != null, "GameplayDriver is missing.");
            driver.EditorSetState(inState);
        }

        private static void Require(bool inCondition, string inMessage)
        {
            if (inCondition == false)
            {
                throw new InvalidOperationException(inMessage);
            }
        }
    }
}
