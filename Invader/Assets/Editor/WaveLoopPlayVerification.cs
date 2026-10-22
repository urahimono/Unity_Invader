using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    // Dedicated batch Editor, without -quit. Fixtures position shots; runtime controller handles transitions.
    public static class WaveLoopPlayVerification
    {
        private const string SessionKey = "WaveLoopPlayVerification";
        private static int stage, clearedWave, pausedX, pausedSteps;
        private static double deadline, clearStarted, nextCheck;
        private static GameStateController state;
        private static InvaderFormationController view;
        private static SpriteRenderer[] renderers;
        private static Keyboard keyboard;
        private static InputSettings settings;
        private static InputSettings.BackgroundBehavior background;
        private static InputSettings.EditorInputBehaviorInPlayMode editorInput;

        public static void Verify()
        {
            WaveLoopVerification.Verify();
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(SessionKey, true);
            Resume();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            stage = clearedWave = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                WaveLoopVerification.Require(EditorApplication.timeSinceStartup < deadline, "Wave Play verification timeout.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
                    EditorApplication.timeSinceStartup < nextCheck) return;
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade") return;
                    state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    if (state == null || state.CurrentState != GameState.Playing) return;
                    view = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    renderers = view.GetComponentsInChildren<SpriteRenderer>();
                    settings = InputSystem.settings;
                    background = settings.backgroundBehavior;
                    editorInput = settings.editorInputBehaviorInPlayMode;
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    Keys(Key.Space);
                    stage = 1;
                }
                else if (stage == 1)
                {
                    // Keep the real InputReader holding Space during clear and into the next wave.
                    state.Playing.Shot.Deactivate();
                    WaveLoopVerification.Clear(state.Playing, PlayingSimulation.TickDuration);
                    pausedX = state.Playing.Player.X;
                    pausedSteps = state.Playing.Formation.StepCount;
                    clearedWave++;
                    stage = 2;
                }
                else if (stage == 2)
                {
                    if (state.CurrentState != GameState.WaveClear) return;
                    clearStarted = Time.timeAsDouble;
                    Keys(Key.Space, Key.RightArrow);
                    stage = 3;
                    nextCheck = EditorApplication.timeSinceStartup + 0.4;
                }
                else if (stage == 3)
                {
                    Require(state.CurrentState == GameState.WaveClear && state.Playing.Player.X == pausedX &&
                        state.Playing.Formation.StepCount == pausedSteps && state.Playing.Score == clearedWave * 990 &&
                        !state.Playing.Shot.Active && !state.Playing.Shot.FireReady, "WaveClear must freeze progress/input/Score.");
                    Keys(Key.Space);
                    stage = 4;
                }
                else if (stage == 4)
                {
                    if (state.CurrentState != GameState.Playing) return;
                    Require(Time.timeAsDouble - clearStarted >= 0.9 && Time.timeAsDouble - clearStarted < 1.3,
                        "WaveClear must last approximately one second.");
                    Require(state.Playing.WaveNumber == clearedWave + 1 && state.Playing.Formation.AliveCount == 55 &&
                        state.Playing.Score == clearedWave * 990 && state.Playing.Player.X == pausedX &&
                        !state.Playing.Shot.Active && !state.Playing.Shot.FireReady, "Next wave must retain state and block held Fire.");
                    Require(view.GetComponentsInChildren<SpriteRenderer>().SequenceEqual(renderers) && renderers.Length == 55 &&
                        renderers.All(r => r.enabled), "Reuse exactly the same 55 visible renderers.");
                    Debug.Log($"WAVE_LOOP_PLAY_WAVE_PASS: Wave={state.Playing.WaveNumber}; Score={state.Playing.Score}; renderers=55; elapsed={Time.timeAsDouble - clearStarted}");
                    Keys();
                    stage = 5;
                    nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (stage == 5)
                {
                    Require(state.Playing.Shot.FireReady, "Release must re-arm.");
                    Keys(Key.Space);
                    stage = 6;
                }
                else if (stage == 6)
                {
                    if (!state.Playing.Shot.Active) return;
                    if (clearedWave == 1) stage = 1;
                    else
                    {
                        Debug.Log("WAVE_LOOP_PLAY_PASS: Bootstrap -> Arcade -> Playing -> WaveClear -> Wave2 -> WaveClear -> Wave3; real collisions/Score; one-second wait; held/release/re-press; same 55 display objects.");
                        Finish(0);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Require(bool condition, string message) => WaveLoopVerification.Require(condition, message);
        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static void Finish(int code)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (settings != null)
            {
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
            EditorApplication.Exit(code);
        }
    }
}
