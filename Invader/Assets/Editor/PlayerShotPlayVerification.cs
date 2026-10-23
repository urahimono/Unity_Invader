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
        private const           string              SessionKey    = "PlayerShotPlayVerification";
        private static          int                 _stage;
        private static          int                 _stateIndex;
        private static          int                 _pausedY;
        private static          double              _deadline;
        private static          double              _nextCheck;
        private static          PlayerController    _player;
        private static          GameStateController _state;
        private static          SpriteRenderer      _renderer;
        private static readonly GameState[]         _pausedStates = {
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
            if (SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }
            _stage = 0;
            _stateIndex = 0;
            _deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < _deadline, "Play verification timed out.");
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
                    _player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    _state = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    Require(_player != null && _state != null, "Player/State missing.");
                    if (_state.CurrentState != GameState.Playing)
                    {
                        return;
                    }
                    VerifyInput();
                    var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    Require(formation != null && formation.GetComponentsInChildren<SpriteRenderer>().Length == 55,
                        "Formation must retain 55 renderers.");
                    Require(_player.GetComponent<SpriteRenderer>().enabled, "Player must be visible.");
                    Require(_player.GetComponentsInChildren<SpriteRenderer>().Length == 2, "Exactly one Shot renderer required.");
                    _renderer = _player.transform.Find("PlayerShot").GetComponent<SpriteRenderer>();
                    Require(!_renderer.enabled, "Inactive shot must be hidden.");
                    _player.Shot.ReadFire(true);
                    _player.Shot.Tick(_player.Player);
                    _stage = 1;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.04;
                }
                else if (_stage == 1)
                {
                    CheckDisplay(true);
                    Require(_renderer.sprite.rect == new Rect(0, 0, 1, 4), "Shot must use a simple 1x4 rectangle.");
                    Pause();
                    _stage = 2;
                }
                else if (_stage == 2)
                {
                    CheckDisplay(false);
                    Require(_player.Shot.Active && _player.Shot.Y == _pausedY, "Non-Playing must preserve and stop Shot.");
                    _stateIndex++;
                    if (_stateIndex < _pausedStates.Length)
                    {
                        Pause();
                    }
                    else
                    {
                        _state.ChangeState(GameState.Playing);
                        _stage = 3;
                        _nextCheck = EditorApplication.timeSinceStartup + 0.04;
                    }
                }
                else
                {
                    CheckDisplay(true);
                    Require(_player.Shot.Y > _pausedY, "Shot must resume.");
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
            _state.ChangeState(_pausedStates[_stateIndex]);
            _pausedY = _player.Shot.Y;
            _nextCheck = EditorApplication.timeSinceStartup + 0.1;
        }

        private static void CheckDisplay(bool visible)
        {
            Require(_renderer.enabled == visible, "Shot visibility mismatch.");
            Require(_renderer.transform.position == new Vector3(_player.Shot.X - 112, _player.Shot.Y - 128, 0),
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
            if (condition == false)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
