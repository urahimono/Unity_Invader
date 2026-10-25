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
            var shotBody = shots[0].GetComponent<Rigidbody2D>();
            var collider = shots[0].GetComponent<BoxCollider2D>();
            Require(shotBody.bodyType == RigidbodyType2D.Dynamic && shotBody.gravityScale == 0f && shotBody.linearDamping == 0f, "Shot body settings.");
            Require(collider.isTrigger && Vector2.Distance(collider.size, new Vector2(1f, 4f) * ArcadeViewport.UnitsPerPixel) < 1e-6f, "Shot trigger dimensions.");
            Require(formations[0].GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic, "Formation body must be kinematic.");
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
