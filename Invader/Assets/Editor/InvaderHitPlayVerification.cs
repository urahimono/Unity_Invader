using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    // Dedicated batch process, without -quit. Uses real controller updates and virtual keyboard input.
    public static class InvaderHitPlayVerification
    {
        private const string SessionKey = "InvaderHitPlayVerification";
        private static int stage, stateIndex, shotX, initialPlayerX, pausedX, pausedY, pausedStep, heldStep;
        private static double deadline, nextCheck;
        private static PlayerController player;
        private static InvaderFormationController formation;
        private static GameStateController state;
        private static SpriteRenderer shotRenderer;
        private static Keyboard keyboard;
        private static InputSettings settings;
        private static InputSettings.BackgroundBehavior backgroundBehavior;
        private static InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private static readonly GameState[] PausedStates = {
            GameState.Attract, GameState.PlayerReady, GameState.PlayerDead, GameState.WaveClear, GameState.GameOver
        };

        public static void Verify()
        {
            InvaderHitVerification.Verify();
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            stage = stateIndex = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Invader hit Play verification timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextCheck) return;
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade") return;
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    Require(player != null && formation != null && state != null, "Arcade components missing.");
                    if (state.CurrentState != GameState.Playing) return;
                    Require(ReferenceEquals(player.Player, state.Playing.Player) && ReferenceEquals(player.Shot, state.Playing.Shot) &&
                        ReferenceEquals(formation.Formation, state.Playing.Formation), "Controllers must share one Playing simulation.");
                    Require(formation.Formation.AliveCount == 55 && state.Playing.Score == 0, "Initial logical state mismatch.");
                    Require(player.GetComponent<SpriteRenderer>().enabled, "Player must be visible.");
                    shotRenderer = player.transform.Find("PlayerShot").GetComponent<SpriteRenderer>();
                    settings = InputSystem.settings;
                    backgroundBehavior = settings.backgroundBehavior;
                    editorBehavior = settings.editorInputBehaviorInPlayMode;
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    initialPlayerX = player.Player.X;
                    Keys(Key.Space);
                    stage = 1;
                }
                else if (stage == 1)
                {
                    if (!player.Shot.Active) return;
                    shotX = player.Shot.X;
                    Require(shotX == initialPlayerX && shotRenderer.enabled, "Space must launch at current Player X.");
                    Keys(Key.Space, Key.RightArrow);
                    stage = 2;
                    nextCheck = EditorApplication.timeSinceStartup + 0.07;
                }
                else if (stage == 2)
                {
                    Require(player.Player.X > initialPlayerX && player.Shot.X == shotX, "Arrow input must move Player without moving Shot X.");
                    Keys(Key.Space);
                    stage = 3;
                }
                else if (stage == 3)
                {
                    if (state.Playing.Score == 0) return;
                    Require(state.Playing.Score == 10 && formation.Formation.AliveCount == 54 && !player.Shot.Active,
                        "First Bottom hit must add 10, kill exactly one and consume Shot.");
                    heldStep = formation.Formation.StepCount;
                    stage = 4;
                    nextCheck = EditorApplication.timeSinceStartup + 0.25;
                }
                else if (stage == 4)
                {
                    Require(!player.Shot.Active && !shotRenderer.enabled && state.Playing.Score == 10 &&
                        formation.Formation.AliveCount == 54, "Held Space must not respawn or score again after a hit.");
                    Require(formation.Formation.StepCount > heldStep, "Formation must keep moving after a kill.");
                    CheckFormationDisplay(true);
                    Keys();
                    stage = 5;
                    nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (stage == 5)
                {
                    Require(player.Shot.FireReady, "Release must re-arm Fire.");
                    Keys(Key.Space);
                    stage = 6;
                }
                else if (stage == 6)
                {
                    if (!player.Shot.Active) return;
                    Require(player.Shot.X == player.Player.X, "Re-press must launch from the new Player position.");
                    Pause();
                    Keys(Key.Space, Key.RightArrow);
                    stage = 7;
                }
                else if (stage == 7)
                {
                    Require(player.Player.X == pausedX && player.Shot.Y == pausedY && player.Shot.Active &&
                        formation.Formation.StepCount == pausedStep && formation.Formation.AliveCount == 54 && state.Playing.Score == 10,
                        "Non-Playing must stop Player/Shot/Formation/collision/score.");
                    Require(!shotRenderer.enabled && !player.GetComponent<SpriteRenderer>().enabled, "Non-Playing must hide Player and Shot.");
                    CheckFormationDisplay(false);
                    if (++stateIndex < PausedStates.Length) Pause();
                    else
                    {
                        state.ChangeState(GameState.Playing);
                        stage = 8;
                        nextCheck = EditorApplication.timeSinceStartup + 0.05;
                    }
                }
                else if (stage == 8)
                {
                    Require(player.Player.X > pausedX && player.Shot.Y > pausedY && shotRenderer.enabled,
                        "Shared simulation must resume in Playing.");
                    CheckFormationDisplay(true);
                    // Isolate the zero-alive State rule; scoring itself was verified with 55 actual hits in logic tests.
                    foreach (var slot in formation.Formation.Invaders) formation.Formation.TryKill(slot.Id);
                    player.Shot.Deactivate();
                    Keys();
                    stage = 9;
                    nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else
                {
                    Require(state.CurrentState == GameState.Playing && formation.Formation.AliveCount == 0 && state.Playing.Score == 10,
                        "Zero alive must not trigger WaveClear, reset Score or spawn another wave.");
                    CheckFormationDisplay(true);
                    Debug.Log("INVADER_HIT_PLAY_PASS: Bootstrap -> Arcade -> Playing; one shared simulation; virtual Space/arrow input through controllers; fixed Shot X; one Bottom hit/Score 10; Dead renderer hidden with all 55 objects retained; Shot hidden; held Fire/release/re-press; movement continues; all five non-Playing states stop/hide/preserve; resume; zero alive stays Playing.");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Pause()
        {
            state.ChangeState(PausedStates[stateIndex]);
            pausedX = player.Player.X;
            pausedY = player.Shot.Y;
            pausedStep = formation.Formation.StepCount;
            nextCheck = EditorApplication.timeSinceStartup + 0.1;
        }

        private static void CheckFormationDisplay(bool playing)
        {
            var renderers = formation.GetComponentsInChildren<SpriteRenderer>();
            Require(renderers.Length == 55, "All 55 display objects must remain.");
            foreach (var slot in formation.Formation.Invaders)
            {
                var renderer = renderers[slot.Id];
                Require(renderer.enabled == (playing && slot.Alive), "Renderer must reflect Playing and Alive.");
                Require(renderer.sprite.name == InvaderFormationSceneSetup.FrameNames[(int)slot.Type * 2 + formation.Formation.AnimationFrame],
                    "Surviving slots must retain synchronized animation.");
                Require(renderer.transform.position == new Vector3(formation.Formation.X + slot.Column * 16 - 112,
                    formation.Formation.Y - slot.Row * 16 - 128, 0), "Logical-to-display coordinates must remain unchanged.");
            }
        }

        private static void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        }

        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (settings != null)
            {
                settings.backgroundBehavior = backgroundBehavior;
                settings.editorInputBehaviorInPlayMode = editorBehavior;
            }
            EditorApplication.Exit(code);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
