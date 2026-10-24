using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Prototypes.Physics2DProbe.Editor
{
    public static class Physics2DProbeVerification
    {
        [MenuItem("Unity Invader/Prototypes/Physics2D/Verify Contacts (Play Mode)")]
        public static void Verify()
        {
            if (EditorApplication.isPlaying == false)
            {
                throw new InvalidOperationException("Generate the probe assets and enter Play Mode first.");
            }
            var invaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Physics2DProbeSetup.InvaderPrefabPath);
            var shotPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(Physics2DProbeSetup.ShotPrefabPath);
            Require(invaderPrefab != null && shotPrefab != null, "Generate probe prefabs first.");
            var invader = invaderPrefab.GetComponent<Physics2DProbeInvader>();
            var shot    = shotPrefab.GetComponent<Physics2DProbeShot>();
            Require(invader != null && shot != null, "Probe prefab components are missing.");
            VerifyCase(invader, shot, new[] { Vector2.zero }, "single");
            VerifyCase(invader, shot, new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) }, "shared edge");
            VerifyCase(invader, shot, new[] { new Vector2(0.5f, 0f), new Vector2(-0.5f, 0f) }, "shared edge, reversed creation");
            Debug.Log("PHYSICS2D_PROBE_CONTACTS_PASS: single/shared/reversed; one consumption; one notification; no repeated defeat.");
        }

        private static void VerifyCase(Physics2DProbeInvader inInvader, Physics2DProbeShot inShot,
            Vector2[] inPositions, string inCase)
        {
            // Manual steps in a local physics scene leave the main scene and global settings alone.
            var scene = SceneManager.CreateScene("Physics2DProbe verification - " + inCase,
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var root  = new GameObject("Probe verification fixture");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var formation = root.AddComponent<Physics2DProbeFormation>();
                Physics2DProbeSetup.ConfigureFormation(formation, inInvader, inPositions);
                formation.ResetProbe();
                var shot = UnityEngine.Object.Instantiate(inShot, root.transform);
                // Single case approaches from below; shared cases initially overlap both equally.
                shot.transform.localPosition = inPositions.Length == 1 ? new Vector3(0f, -1.5f, 0f) : Vector3.zero;
                var physics                  = scene.GetPhysicsScene2D();
                for (int step = 0; step < 20; step++)
                {
                    Require(physics.Simulate(0.02f), "Could not simulate local Physics2D scene.");
                }
                Require(formation.AliveCount == inPositions.Length - 1, inCase + ": expected exactly one defeat.");
                Require(formation.DefeatNotifications == 1, inCase + ": expected exactly one notification.");
                Require(shot.TryConsume() == false, inCase + ": consumed shot accepted twice.");
                for (int step = 0; step < 3; step++)
                {
                    Require(physics.Simulate(0.02f), "Could not simulate follow-up step.");
                }
                Require(formation.AliveCount == inPositions.Length - 1 && formation.DefeatNotifications == 1,
                    inCase + ": repeated callbacks changed the result.");
                Debug.Log($"PHYSICS2D_PROBE_CASE_PASS: {inCase}, alive={formation.AliveCount}, notifications={formation.DefeatNotifications}");
            }
            finally
            {
                root.SetActive(false);
                SceneManager.UnloadSceneAsync(scene);
            }
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
