using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    // Batch entry point: run without -quit; this verifier exits after Play Mode checks.
    public static class InvaderFormationPlayVerification
    {
        private static int stage;
        private static double nextCheck;
        private static double deadline;
        private static InvaderFormationController controller;
        private static GameStateController state;
        private static PlayerController player;
        private static int pausedStep;
        private static int pausedX;
        private static int pausedY;
        private static int stateIndex;

        public static void Verify()
        {
            InvaderFormationVerification.Verify();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(InvaderFormationSceneSetup.TexturePath).OfType<Sprite>().ToArray();
            Require(sprites.Length == 6, "Six imported sprites required.");
            foreach (var sprite in sprites)
            {
                int index = Array.IndexOf(InvaderFormationSceneSetup.FrameNames, sprite.name);
                Require(index >= 0, "Unexpected sprite name.");
                Require(sprite.rect == new Rect(index % 2 * 1086, 1 + (2 - index / 2) * 241, 1086, 241), "Sprite rect mismatch.");
                Require(sprite.pivot == new Vector2(543, 120.5f), "Centered pivot required.");
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            // Preserve the callback across the Play Mode domain reload through InitializeOnLoadMethod.
            SessionState.SetBool("InvaderFormationPlayVerification", true);
            Resume();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("InvaderFormationPlayVerification", false)) return;
            stage = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Play verification timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
                    EditorApplication.timeSinceStartup < nextCheck) return;
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade") return;
                    controller = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    Require(controller != null && player != null && state != null, "Arcade components missing.");
                    if (state.CurrentState != GameState.Playing) return;
                    stage = 10;
                    nextCheck = EditorApplication.timeSinceStartup + 0.2;
                }
                else if (stage == 10)
                {
                    CheckDisplay(true);
                    Require(player.GetComponent<SpriteRenderer>().enabled, "Player must remain visible in Playing.");
                    // Step the model to the opposite frame, then let the normal Update display it.
                    for (int tick = 0; tick < InvaderFormation.TicksPerStep; tick++) controller.Formation.Tick();
                    stage = 3;
                    nextCheck = EditorApplication.timeSinceStartup + 0.02;
                }
                else if (stage == 3)
                {
                    CheckDisplay(true);
                    stateIndex = 0;
                    BeginPausedState();
                    stage = 4;
                }
                else if (stage == 4)
                {
                    CheckDisplay(false);
                    Require(controller.Formation.StepCount == pausedStep && controller.Formation.X == pausedX &&
                        controller.Formation.Y == pausedY, "Non-Playing formation must stop.");
                    stateIndex++;
                    if (stateIndex < 5) BeginPausedState();
                    else
                    {
                        state.ChangeState(GameState.Playing);
                        stage = 5;
                        nextCheck = EditorApplication.timeSinceStartup + 0.5;
                    }
                }
                else
                {
                    CheckDisplay(true);
                    Require(controller.Formation.StepCount > pausedStep, "Formation must resume in Playing.");
                    Debug.Log("INVADER_FORMATION_PLAY_PASS: Bootstrap -> Arcade -> Playing; 55 renderers; six sprites/rects/pivots; synchronized display; Player present/visible; all five non-Playing states hidden/stopped; resume. Keyboard input requires GUI verification.");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void BeginPausedState()
        {
            var states = new[] { GameState.Attract, GameState.PlayerReady, GameState.PlayerDead, GameState.WaveClear, GameState.GameOver };
            state.ChangeState(states[stateIndex]);
            pausedStep = controller.Formation.StepCount;
            pausedX = controller.Formation.X;
            pausedY = controller.Formation.Y;
            nextCheck = EditorApplication.timeSinceStartup + 0.4;
        }

        private static void CheckDisplay(bool visible)
        {
            var renderers = controller.GetComponentsInChildren<SpriteRenderer>();
            Require(renderers.Length == 55, "Expected exactly 55 generated renderers.");
            foreach (var slot in controller.Formation.Invaders)
            {
                var renderer = renderers[slot.Id];
                Require(renderer.enabled == visible, "Visibility mismatch.");
                Require(renderer.sprite.name == InvaderFormationSceneSetup.FrameNames[(int)slot.Type * 2 + controller.Formation.AnimationFrame], "Frame/type mismatch.");
                var expected = new Vector3(controller.Formation.X + slot.Column * 16 - 112,
                    controller.Formation.Y - slot.Row * 16 - 128, 0);
                Require(renderer.transform.position == expected, "Logical-to-display position mismatch.");
                Require(Mathf.Abs(renderer.bounds.size.x - 16) < 0.001f &&
                    Mathf.Abs(renderer.bounds.size.y - 16) < 0.001f, "All displayed cells must be 16x16.");
            }
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool("InvaderFormationPlayVerification", false);
            EditorApplication.update -= Check;
            EditorApplication.Exit(exitCode);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
