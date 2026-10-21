using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    // Dedicated batch process; omit -quit. This verifier exits the Editor itself.
    public static class PlayerShotPlayVerification
    {
        private const string SessionKey = "PlayerShotPlayVerification";
        private static int stage;
        private static int stateIndex;
        private static int pausedY;
        private static double deadline;
        private static double nextCheck;
        private static PlayerController player;
        private static GameStateController state;
        private static SpriteRenderer renderer;
        private static readonly GameState[] PausedStates = {
            GameState.Attract, GameState.PlayerReady, GameState.PlayerDead, GameState.WaveClear, GameState.GameOver
        };

        public static void Verify()
        {
            PlayerShotVerification.Verify();
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            stage = 0;
            stateIndex = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Play verification timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextCheck) return;
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade") return;
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    Require(player != null && state != null, "Player/State missing.");
                    if (state.CurrentState != GameState.Playing) return;
                    VerifyInput();
                    var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    Require(formation != null && formation.GetComponentsInChildren<SpriteRenderer>().Length == 55,
                        "Formation must retain 55 renderers.");
                    Require(player.GetComponent<SpriteRenderer>().enabled, "Player must be visible.");
                    Require(player.GetComponentsInChildren<SpriteRenderer>().Length == 2, "Exactly one Shot renderer required.");
                    renderer = player.transform.Find("PlayerShot").GetComponent<SpriteRenderer>();
                    Require(!renderer.enabled, "Inactive shot must be hidden.");
                    player.Shot.ReadFire(true);
                    player.Shot.Tick(player.Player);
                    stage = 1;
                    nextCheck = EditorApplication.timeSinceStartup + 0.04;
                }
                else if (stage == 1)
                {
                    CheckDisplay(true);
                    Require(renderer.sprite.rect == new Rect(0, 0, 1, 4), "Shot must use a simple 1x4 rectangle.");
                    Pause();
                    stage = 2;
                }
                else if (stage == 2)
                {
                    CheckDisplay(false);
                    Require(player.Shot.Active && player.Shot.Y == pausedY, "Non-Playing must preserve and stop Shot.");
                    stateIndex++;
                    if (stateIndex < PausedStates.Length) Pause();
                    else
                    {
                        state.ChangeState(GameState.Playing);
                        stage = 3;
                        nextCheck = EditorApplication.timeSinceStartup + 0.04;
                    }
                }
                else
                {
                    CheckDisplay(true);
                    Require(player.Shot.Y > pausedY, "Shot must resume.");
                    Debug.Log("PLAYER_SHOT_PLAY_PASS: Input System arrows/Space press/release; Bootstrap -> Arcade -> Playing; one 1x4 Shot renderer; logical/display conversion; all non-Playing states hidden/stopped/preserved; resume; Player and 55 Formation renderers present.");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void VerifyInput()
        {
            // Verify the real InputAction bindings, independently of physical keyboard focus.
            var testSettings = InputSystem.settings;
            var updateMode = testSettings.updateMode;
            var backgroundBehavior = testSettings.backgroundBehavior;
            var editorBehavior = testSettings.editorInputBehaviorInPlayMode;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                using (var reader = new PlayerInputReader())
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.RightArrow, Key.Space));
                    InputSystem.Update();
                    var input = reader.Read();
                    Require(input.Left && input.Right && input.Fire, "Input System arrow/Space bindings failed.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    input = reader.Read();
                    Require(!input.Left && !input.Right && !input.Fire, "Input System release failed.");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                testSettings.updateMode = updateMode;
                testSettings.backgroundBehavior = backgroundBehavior;
                testSettings.editorInputBehaviorInPlayMode = editorBehavior;
            }
        }

        private static void Pause()
        {
            state.ChangeState(PausedStates[stateIndex]);
            pausedY = player.Shot.Y;
            nextCheck = EditorApplication.timeSinceStartup + 0.1;
        }

        private static void CheckDisplay(bool visible)
        {
            Require(renderer.enabled == visible, "Shot visibility mismatch.");
            Require(renderer.transform.position == new Vector3(player.Shot.X - 112, player.Shot.Y - 128, 0),
                "Shot logical/display coordinate mismatch.");
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            EditorApplication.Exit(code);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
