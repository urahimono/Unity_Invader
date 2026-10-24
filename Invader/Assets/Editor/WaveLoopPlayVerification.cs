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
        private const  string                                      SessionKey = "WaveLoopPlayVerification";
        private static int                                         _stage, _clearedWave, _pausedX, _pausedSteps;
        private static double                                      _deadline, _clearStarted, _nextCheck;
        private static GameplayDriver                              _gameplayDriver;
        private static InvaderFormationController                  _view;
        private static SpriteRenderer[]                            _renderers;
        private static Keyboard                                    _keyboard;
        private static InputSettings                               _settings;
        private static InputSettings.BackgroundBehavior            _background;
        private static InputSettings.EditorInputBehaviorInPlayMode _editorInput;

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
            if (SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }
            _stage    = _clearedWave = 0;
            _deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                WaveLoopVerification.Require(EditorApplication.timeSinceStartup < _deadline, "Wave Play verification timeout.");
                if (EditorApplication.isPlaying == false || EditorApplication.isCompiling ||
                    EditorApplication.timeSinceStartup < _nextCheck)
                {
                    return;
                }
                if (_stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade")
                    {
                        return;
                    }
                    _gameplayDriver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
                    if (_gameplayDriver == null || _gameplayDriver.CurrentState != GameState.Playing)
                    {
                        return;
                    }
                    _view                                   = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    _renderers                              = _view.GetComponentsInChildren<SpriteRenderer>();
                    _settings                               = InputSystem.settings;
                    _background                             = _settings.backgroundBehavior;
                    _editorInput                            = _settings.editorInputBehaviorInPlayMode;
                    _settings.backgroundBehavior            = InputSettings.BackgroundBehavior.IgnoreFocus;
                    _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    _keyboard                               = InputSystem.AddDevice<Keyboard>();
                    Keys(Key.Space);
                    _stage = 1;
                }
                else if (_stage == 1)
                {
                    // Keep the real InputReader holding Space during clear and into the next wave.
                    _gameplayDriver.Simulation.Shot.Deactivate();
                    WaveLoopVerification.Clear(_gameplayDriver.Simulation, GameplaySimulation.TickDuration);
                    _pausedX     = _gameplayDriver.Simulation.Player.X;
                    _pausedSteps = _gameplayDriver.Simulation.Formation.StepCount;
                    _clearedWave++;
                    _stage = 2;
                }
                else if (_stage == 2)
                {
                    if (_gameplayDriver.CurrentState != GameState.WaveClear)
                    {
                        return;
                    }
                    _clearStarted = Time.timeAsDouble;
                    Keys(Key.Space, Key.RightArrow);
                    _stage     = 3;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.4;
                }
                else if (_stage == 3)
                {
                    Require(_gameplayDriver.CurrentState == GameState.WaveClear && _gameplayDriver.Simulation.Player.X == _pausedX &&
                        _gameplayDriver.Simulation.Formation.StepCount == _pausedSteps && _gameplayDriver.Simulation.Score == _clearedWave * 990 &&
                        !_gameplayDriver.Simulation.Shot.Active && !_gameplayDriver.Simulation.Shot.FireReady, "WaveClear must freeze progress/input/Score.");
                    Keys(Key.Space);
                    _stage = 4;
                }
                else if (_stage == 4)
                {
                    if (_gameplayDriver.CurrentState != GameState.Playing)
                    {
                        return;
                    }
                    Require(Time.timeAsDouble - _clearStarted >= 0.9 && Time.timeAsDouble - _clearStarted < 1.3,
                        "WaveClear must last approximately one second.");
                    Require(_gameplayDriver.Simulation.WaveNumber == _clearedWave + 1 && _gameplayDriver.Simulation.Formation.AliveCount == 55 &&
                        _gameplayDriver.Simulation.Score == _clearedWave * 990 && _gameplayDriver.Simulation.Player.X == _pausedX &&
                        !_gameplayDriver.Simulation.Shot.Active && !_gameplayDriver.Simulation.Shot.FireReady, "Next wave must retain state and block held Fire.");
                    Require(_view.GetComponentsInChildren<SpriteRenderer>().SequenceEqual(_renderers) && _renderers.Length == 55 &&
                        _renderers.All(inR => inR.enabled), "Reuse exactly the same 55 visible renderers.");
                    Debug.Log($"WAVE_LOOP_PLAY_WAVE_PASS: Wave={_gameplayDriver.Simulation.WaveNumber}; Score={_gameplayDriver.Simulation.Score}; renderers=55; elapsed={Time.timeAsDouble - _clearStarted}");
                    Keys();
                    _stage     = 5;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (_stage == 5)
                {
                    Require(_gameplayDriver.Simulation.Shot.FireReady, "Release must re-arm.");
                    Keys(Key.Space);
                    _stage = 6;
                }
                else if (_stage == 6)
                {
                    if (_gameplayDriver.Simulation.Shot.Active == false)
                    {
                        return;
                    }
                    if (_clearedWave == 1)
                    {
                        _stage = 1;
                    }
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

        private static void Require(bool inCondition, string inMessage) => WaveLoopVerification.Require(inCondition, inMessage);
        private static void Keys(params Key[] inKeys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(inKeys));
        private static void Finish(int inCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
            if (_settings != null)
            {
                _settings.backgroundBehavior            = _background;
                _settings.editorInputBehaviorInPlayMode = _editorInput;
            }
            EditorApplication.Exit(inCode);
        }
    }
}
