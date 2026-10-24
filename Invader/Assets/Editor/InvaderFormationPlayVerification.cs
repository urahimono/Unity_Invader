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
        private static int                        _stage;
        private static double                     _nextCheck;
        private static double                     _deadline;
        private static InvaderFormationController _controller;
        private static GameStateController        _state;
        private static PlayerController           _player;
        private static int                        _pausedStep;
        private static int                        _pausedX;
        private static int                        _pausedY;
        private static int                        _stateIndex;

        public static void Verify()
        {
            InvaderFormationVerification.Verify();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(InvaderFormationSceneSetup.TexturePath).OfType<Sprite>().ToArray();
            Require(sprites.Length == 6, "Six imported sprites required.");
            foreach (var sprite in sprites)
            {
                int index = Array.IndexOf(InvaderFormationSceneSetup.FrameNames, sprite.name);
                Require(index >= 0, "Unexpected sprite name.");
                Require(sprite.rect == new Rect(index % 2 == 0 ? 540 : 1150,
                    1 + (2 - index / 2) * 241, 480, 241), "Sprite rect mismatch.");
                Require(sprite.pivot == new Vector2(240, 120.5f), "Centered pivot required.");
                Require(sprite.pixelsPerUnit == 30, "PPU must be 30.");
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
            if (SessionState.GetBool("InvaderFormationPlayVerification", false) == false)
            {
                return;
            }
            _stage    = 0;
            _deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < _deadline, "Play verification timed out.");
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
                    _controller = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
                    _state      = UnityEngine.Object.FindAnyObjectByType<GameStateController>();
                    _player     = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    Require(_controller != null && _player != null && _state != null, "Arcade components missing.");
                    if (_state.CurrentState != GameState.Playing)
                    {
                        return;
                    }
                    _stage     = 10;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.2;
                }
                else if (_stage == 10)
                {
                    CheckDisplay(true);
                    VerifyFrameSwitch();
                    Require(_player.GetComponent<SpriteRenderer>().enabled, "Player must remain visible in Playing.");
                    // Step the model to the opposite frame, then let the normal Update display it.
                    for (int tick = 0; tick < InvaderFormation.TicksPerStep; tick++)
                    {
                        _controller.Formation.Tick();
                    }
                    _stage     = 3;
                    _nextCheck = EditorApplication.timeSinceStartup + 0.02;
                }
                else if (_stage == 3)
                {
                    CheckDisplay(true);
                    _stateIndex = 0;
                    BeginPausedState();
                    _stage = 4;
                }
                else if (_stage == 4)
                {
                    CheckDisplay(false);
                    Require(_controller.Formation.StepCount == _pausedStep && _controller.Formation.X == _pausedX &&
                        _controller.Formation.Y == _pausedY, "Non-Playing formation must stop.");
                    _stateIndex++;
                    if (_stateIndex < 5)
                    {
                        BeginPausedState();
                    }
                    else
                    {
                        _state.ChangeState(GameState.Playing);
                        _stage     = 5;
                        _nextCheck = EditorApplication.timeSinceStartup + 0.5;
                    }
                }
                else
                {
                    CheckDisplay(true);
                    Require(_controller.Formation.StepCount > _pausedStep, "Formation must resume in Playing.");
                    Debug.Log("INVADER_FORMATION_PLAY_PASS: Bootstrap -> Arcade -> Playing; 55 renderers; six 480x241 centered sprites; PPU 30; unit scale; A/B-only switching preserves logical/display positions; synchronized display; Player present/visible; all five non-Playing states hidden/stopped; resume. Keyboard input requires GUI verification.");
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
            _state.ChangeState(states[_stateIndex]);
            _pausedStep = _controller.Formation.StepCount;
            _pausedX    = _controller.Formation.X;
            _pausedY    = _controller.Formation.Y;
            _nextCheck  = EditorApplication.timeSinceStartup + 0.4;
        }

        private static void CheckDisplay(bool inVisible)
        {
            var renderers = _controller.GetComponentsInChildren<SpriteRenderer>();
            Require(renderers.Length == 55, "Expected exactly 55 generated renderers.");
            foreach (var slot in _controller.Formation.Invaders)
            {
                var renderer = renderers[slot.Id];
                Require(renderer.enabled == inVisible, "Visibility mismatch.");
                Require(renderer.sprite.name == InvaderFormationSceneSetup.FrameNames[(int)slot.Type * 2 + _controller.Formation.AnimationFrame], "Frame/type mismatch.");
                var expected = new Vector3(_controller.Formation.X + slot.Column * 16 - 112,
                    _controller.Formation.Y - slot.Row * 16 - 128, 0);
                Require(renderer.transform.position == expected, "Logical-to-display position mismatch.");
                Require(renderer.transform.localScale == Vector3.one && renderer.transform.lossyScale == Vector3.one,
                    "Display must use unit scale without non-uniform stretching.");
                Require(Mathf.Abs(renderer.bounds.size.x - 16) < 0.001f &&
                    Mathf.Abs(renderer.bounds.size.y - 241f / 30f) < 0.001f, "Displayed rects must be 16 x 241/30.");
            }
        }

        private static void VerifyFrameSwitch()
        {
            var sprites   = AssetDatabase.LoadAllAssetsAtPath(InvaderFormationSceneSetup.TexturePath).OfType<Sprite>().ToArray();
            var formation = _controller.Formation;
            int x         = formation.X, y = formation.Y, step = formation.StepCount, direction = formation.Direction;
            var renderers = _controller.GetComponentsInChildren<SpriteRenderer>();
            foreach (var slot in formation.Invaders)
            {
                var renderer     = renderers[slot.Id];
                var original     = renderer.sprite;
                var position     = renderer.transform.position;
                var boundsCenter = renderer.bounds.center;
                foreach (int frame in new[] { 0, 1 })
                {
                    string name     = InvaderFormationSceneSetup.FrameNames[(int)slot.Type * 2 + frame];
                    renderer.sprite = sprites.Single(inSprite => inSprite.name == name);
                    Require(renderer.transform.position == position && renderer.bounds.center == boundsCenter,
                        "A/B-only switching must preserve display position and bounds center.");
                    Require(formation.X == x && formation.Y == y && formation.StepCount == step && formation.Direction == direction,
                        "A/B-only switching must not change logical formation state.");
                }
                renderer.sprite = original;
            }
        }

        private static void Finish(int inExitCode)
        {
            SessionState.SetBool("InvaderFormationPlayVerification", false);
            EditorApplication.update -= Check;
            EditorApplication.Exit(inExitCode);
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
