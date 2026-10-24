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
        private const           string                                      SessionKey    = "InvaderHitPlayVerification";
        private static          int                                         _stage, _stateIndex, _shotX, _initialPlayerX, _pausedX, _pausedY, _pausedStep, _heldStep;
        private static          double                                      _deadline, _nextCheck;
        private static          PlayerController                            _player;
        private static          GameplayDriver                           _playing;
        private static          InvaderFormationController                  _formation;
        private static          GameStateController                         _state;
        private static          SpriteRenderer                              _shotRenderer;
        private static          Keyboard                                    _keyboard;
        private static          InputSettings                               _settings;
        private static          int                                         _steppedFrames, _lastFrame, _clockStartX, _clockStartY, _clockStartStep;
        private static          double                                      _steppedTime;
        private static          PlayingClockProbe                           _clockProbe;
        private static          InputSettings.BackgroundBehavior            _backgroundBehavior;
        private static          InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
        private static readonly GameState[]                                 _pausedStates = {
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
            if (SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }
            _stage    = _stateIndex = 0;
            _deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < _deadline, "Invader hit Play verification timed out.");
                if (EditorApplication.isPlaying == false || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < _nextCheck)
                {
                    return;
                }
                if (_stage == 0)
                {
                    if (SceneManager.GetActiveScene().name != "Arcade")
                    {
                        return;
                    }
                    _player     = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    _formation  = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    _state      = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Require(drivers.Length == 1 && drivers[0].isActiveAndEnabled, "Arcade must have exactly one enabled GameplayDriver.");
                    _playing = drivers[0];
                    Require(_player != null && _formation != null && _state != null, "Arcade components missing.");
                    if (_state.CurrentState != GameState.Playing)
                    {
                        return;
                    }
                    Require(ReferenceEquals(_player.Player, _state.Playing.Player) && ReferenceEquals(_player.Shot, _state.Playing.Shot) &&
                        ReferenceEquals(_formation.Formation, _state.Playing.Formation), "Controllers must share one Playing simulation.");
                    Require(_formation.Formation.AliveCount == 55 && _state.Playing.Score == 0, "Initial logical state mismatch.");
                    Require(_player.GetComponent<SpriteRenderer>().enabled, "Player must be visible.");
                    _shotRenderer                           = _player.transform.Find("PlayerShot").GetComponent<SpriteRenderer>();
                    _settings                               = InputSystem.settings;
                    _backgroundBehavior                     = _settings.backgroundBehavior;
                    _editorBehavior                         = _settings.editorInputBehaviorInPlayMode;
                    _settings.backgroundBehavior            = InputSettings.BackgroundBehavior.IgnoreFocus;
                    _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    _keyboard                               = InputSystem.AddDevice<Keyboard>();
                    _initialPlayerX                         = _player.Player.X;
                    Keys(Key.Space);
                    _stage = 1;
                }
                else if (_stage == 1)
                {
                    if (_player.Shot.Active == false)
                    {
                        return;
                    }
                    _shotX = _player.Shot.X;
                    Require(_shotX == _initialPlayerX && _shotRenderer.enabled, "Space must launch at current Player X.");
                    Keys(Key.Space, Key.RightArrow);
                    _stage     = 2;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.07;
                }
                else if (_stage == 2)
                {
                    Require(_player.Player.X > _initialPlayerX && _player.Shot.X == _shotX, "Arrow input must move Player without moving Shot X.");
                    Keys(Key.Space);
                    _stage = 3;
                }
                else if (_stage == 3)
                {
                    if (_state.Playing.Score == 0)
                    {
                        return;
                    }
                    Require(_state.Playing.Score == 10 && _formation.Formation.AliveCount == 54 && !_player.Shot.Active,
                        "First Bottom hit must add 10, kill exactly one and consume Shot.");
                    _heldStep  = _formation.Formation.StepCount;
                    _stage     = 4;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.25;
                }
                else if (_stage == 4)
                {
                    Require(!_player.Shot.Active && !_shotRenderer.enabled && _state.Playing.Score == 10 &&
                        _formation.Formation.AliveCount == 54, "Held Space must not respawn or score again after a hit.");
                    Require(_formation.Formation.StepCount > _heldStep, "Formation must keep moving after a kill.");
                    CheckFormationDisplay(true);
                    Keys();
                    _stage     = 5;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (_stage == 5)
                {
                    Require(_player.Shot.FireReady, "Release must re-arm Fire.");
                    Keys(Key.Space);
                    _stage = 6;
                }
                else if (_stage == 6)
                {
                    if (_player.Shot.Active == false)
                    {
                        return;
                    }
                    Require(_player.Shot.X == _player.Player.X, "Re-press must launch from the new Player position.");
                    Pause();
                    Keys(Key.Space, Key.RightArrow);
                    _stage = 7;
                }
                else if (_stage == 7)
                {
                    Require(_player.Player.X == _pausedX && _player.Shot.Y == _pausedY && _player.Shot.Active &&
                        _formation.Formation.StepCount == _pausedStep && _formation.Formation.AliveCount == 54 && _state.Playing.Score == 10,
                        "Non-Playing must stop Player/Shot/Formation/collision/score.");
                    Require(!_shotRenderer.enabled && !_player.GetComponent<SpriteRenderer>().enabled, "Non-Playing must hide Player and Shot.");
                    CheckFormationDisplay(false);
                    if (++_stateIndex < _pausedStates.Length)
                    {
                        Pause();
                    }
                    else
                    {
                        _state.ChangeState(GameState.Playing);
                        _stage     = 8;
                        _nextCheck = EditorApplication.timeSinceStartup + 0.05;
                    }
                }
                else if (_stage == 8)
                {
                    Require(_player.Player.X > _pausedX && _player.Shot.Y > _pausedY && _shotRenderer.enabled,
                        "Shared simulation must resume in Playing.");
                    CheckFormationDisplay(true);
                    // Wave-clear behavior is covered by WaveLoopPlayVerification.
                    _player.Shot.Deactivate();
                    Keys();
                    _stage     = 9;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (_stage == 9)
                {
                    Require(_state.CurrentState == GameState.Playing && _formation.Formation.AliveCount == 54 && _state.Playing.Score == 10,
                        "Surviving Formation must stay Playing and preserve Score.");
                    CheckFormationDisplay(true);
                    // Editor stepping uses its own delta; compare every frame with its actual elapsed time.
                    EditorApplication.isPaused = true;
                    _playing.enabled           = false; // Discard partial accumulated time for an exact reference.
                    _playing.enabled           = true;
                    InvaderHitVerification.PrepareShot(_state.Playing, _player.Player.X, 32);
                    _player.enabled = false;
                    Require(!_player.GetComponent<SpriteRenderer>().enabled && !_shotRenderer.enabled, "Disabling the view must hide its renderers.");
                    _clockStartX    = _player.Player.X;
                    _clockStartY    = _player.Shot.Y;
                    _clockStartStep = _formation.Formation.StepCount;
                    _steppedFrames  = 0;
                    _steppedTime    = 0;
                    _clockProbe     = new PlayingClockProbe();
                    _stage          = 10;
                    StepFrame();
                }
                else if (_stage == 10 || _stage == 11)
                {
                    if (_clockProbe.Frames == _lastFrame)
                    {
                        return;
                    }
                    Require(_clockProbe.Frames == _lastFrame + 1, "Each Editor step must complete exactly one rendered frame.");
                    _steppedFrames++;
                    if (_stage == 10)
                    {
                        _steppedTime      = _clockProbe.Elapsed;
                        int expectedTicks = (int)System.Math.Floor((_steppedTime + 1e-12) / ArcadeGameplaySimulation.TickDuration);
                        Require(_clockStartY + expectedTicks * PlayerShot.SpeedPerTick < PlayerBody.ScreenHeight,
                            "Clock test requires Shot to remain within the screen.");
                        Require(_player.Shot.Active && _player.Shot.Y == _clockStartY + expectedTicks * PlayerShot.SpeedPerTick,
                            $"Frame {_steppedFrames}: exactly one Advance per frame expected; elapsed={_steppedTime}; ticks={expectedTicks}; ShotY={_player.Shot.Y}.");
                    }
                    if (_steppedFrames < 20)
                    {
                        StepFrame();
                        return;
                    }
                    if (_stage == 10)
                    {
                        Require(_formation.Formation.StepCount > _clockStartStep,
                            "Formation must advance even with PlayerController disabled.");
                        _playing.enabled = false;
                        _player.enabled  = true;
                        _clockStartX     = _player.Player.X;
                        _clockStartY     = _player.Shot.Y;
                        _clockStartStep  = _formation.Formation.StepCount;
                        _steppedFrames   = 0;
                        _stage           = 11;
                        StepFrame();
                        return;
                    }
                    Require(_player.Player.X == _clockStartX && _player.Shot.Y == _clockStartY && _formation.Formation.StepCount == _clockStartStep,
                        "Enabled views must not advance the simulation when GameplayDriver is disabled.");
                    Require(_player.GetComponent<SpriteRenderer>().enabled && _player.transform.position == new Vector3(_player.Player.X - 112, _player.Player.Y - 128, 0),
                        "Player display must still synchronize while the clock is disabled.");
                    Debug.Log("PLAYING_CONTROLLER_CLOCK_PASS: exactly one driver; each of 20 rendered frames matches one Advance of actual deltaTime with Player view disabled; 20 frames frozen with driver disabled and views enabled.");
                    Debug.Log("INVADER_HIT_PLAY_PASS: Bootstrap -> Arcade -> Playing; shared simulation; virtual keyboard; hit/Score/display; held Fire/release/re-press; all non-Playing states freeze; resume; surviving Formation stays Playing.");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void StepFrame()
        {
            _lastFrame = _clockProbe.Frames;
            EditorApplication.Step();
        }

        private static void Pause()
        {
            _state.ChangeState(_pausedStates[_stateIndex]);
            _pausedX    = _player.Player.X;
            _pausedY    = _player.Shot.Y;
            _pausedStep = _formation.Formation.StepCount;
            _nextCheck  = EditorApplication.timeSinceStartup + 0.1;
        }

        private static void CheckFormationDisplay(bool inPlaying)
        {
            var renderers = _formation.GetComponentsInChildren<SpriteRenderer>();
            Require(renderers.Length == 55, "All 55 display objects must remain.");
            foreach (var slot in _formation.Formation.Invaders)
            {
                var renderer = renderers[slot.Id];
                Require(renderer.enabled == (inPlaying && slot.Alive), "Renderer must reflect Playing and Alive.");
                Require(renderer.sprite.name == InvaderFormationSceneSetup.FrameNames[(int)slot.Type * 2 + _formation.Formation.AnimationFrame],
                    "Surviving slots must retain synchronized animation.");
                Require(renderer.transform.position == new Vector3(_formation.Formation.X + slot.Column * 16 - 112,
                    _formation.Formation.Y - slot.Row * 16 - 128, 0), "Logical-to-display coordinates must remain unchanged.");
            }
        }

        private static void Keys(params Key[] inKeys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(inKeys));
        }

        private static void Finish(int inCode)
        {
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Check;
            _clockProbe?.Dispose();
            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
            if (_settings != null)
            {
                _settings.backgroundBehavior            = _backgroundBehavior;
                _settings.editorInputBehaviorInPlayMode = _editorBehavior;
            }
            EditorApplication.isPaused = false;
            EditorApplication.Exit(inCode);
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
