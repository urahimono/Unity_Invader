using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnityInvader.Editor
{
    // Explicit batch entry point only. Importing this script never starts a verification run.
    public static class RollingEnemyShotVerification
    {
        private static GameplayDriver   _driver;
        private static InvaderFormation _formation;
        private static Player           _player;
        private static PlayerShot       _playerShot;
        private static EnemyShot        _enemyShot;
        private static Invader[]        _invaders;
        private static int              _stage;
        private static double           _deadline;
        private static double           _readyAt;
        private static float            _startTime;
        private static Vector2          _startPosition;
        private static Vector3          _playerPosition;

        private const string SessionKey = "UnityInvader.RollingEnemyShotVerification";

        public static void Run()
        {
            if (Application.isBatchMode == false)
            {
                throw new InvalidOperationException("Use the explicitly approved batch process for this verification.");
            }
            RollingEnemyShotSetup.Configure();
            ComponentGameplayVerification.Verify();
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(SessionKey, true);
            ResumeAfterReload();
        }

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (Application.isBatchMode == false || SessionState.GetBool(SessionKey, false) == false)
            {
                return;
            }
            _stage    = EditorApplication.isPlayingOrWillChangePlaymode ? 0 : -1;
            _deadline = EditorApplication.timeSinceStartup + 30d;
            _readyAt  = EditorApplication.timeSinceStartup + 2d;
            EditorApplication.update -= Check;
            EditorApplication.update += Check;
        }

        private static void Check()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < _deadline, "Rolling verification timed out.");
                if (_stage == -1)
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    {
                        _readyAt = EditorApplication.timeSinceStartup + 2d;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup >= _readyAt)
                    {
                        _stage = 0;
                        EditorApplication.isPlaying = true;
                    }
                    return;
                }
                if (EditorApplication.isPlaying == false || SceneManager.GetActiveScene().path != "Assets/Scenes/Arcade.unity")
                {
                    return;
                }
                RunStage();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void RunStage()
        {
            switch (_stage)
            {
                case 0:
                    _driver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
                    if (_driver == null || (bool)typeof(GameplayDriver).GetField("_initialized", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_driver) == false)
                    {
                        return;
                    }
                    _formation  = UnityEngine.Object.FindAnyObjectByType<InvaderFormation>();
                    _player     = UnityEngine.Object.FindAnyObjectByType<Player>();
                    _playerShot = UnityEngine.Object.FindObjectsByType<PlayerShot>(FindObjectsInactive.Include).Single();
                    _enemyShot  = UnityEngine.Object.FindObjectsByType<EnemyShot>(FindObjectsInactive.Include).Single();
                    _invaders   = _formation.GetComponentsInChildren<Invader>(true)
                        .OrderByDescending(inInvader => inInvader.transform.localPosition.y)
                        .ThenBy(inInvader => inInvader.transform.localPosition.x).ToArray();
                    // Isolate only this dedicated batch process from physical keyboard input.
                    foreach (var keyboard in InputSystem.devices.OfType<Keyboard>().ToArray())
                    {
                        InputSystem.DisableDevice(keyboard);
                    }
                    Require(_invaders.Length == 55 && AliveCount() == 55, "Initial formation is not 55 living objects.");
                    Require(Vector2.Distance(_formation.GetComponent<Rigidbody2D>().position, new Vector2(-0.8f, 0.8f)) < 0.025f, "Formation initial pose.");
                    Require(TryMuzzle(out var muzzle) && Mathf.Abs(muzzle.y - (0.16f - 0.08f)) < 1e-5f, "Rolling must choose the lowest live row.");
                    Debug.Log("ROLLING_CHECK_PASS: Bootstrap -> Arcade; 55 targets; valid lowest-row source.");
                    _stage = 1;
                    break;
                case 1:
                    if (_enemyShot.gameObject.activeSelf == false)
                    {
                        return; // Wait for the real provisional 2-second automatic firing interval.
                    }
                    _startPosition = _enemyShot.GetComponent<Rigidbody2D>().position;
                    _startTime     = Time.fixedTime;
                    _stage         = 2;
                    break;
                case 2:
                    if (Time.fixedTime - _startTime < 0.3f)
                    {
                        return;
                    }
                    float normalDistance = _startPosition.y - _enemyShot.GetComponent<Rigidbody2D>().position.y;
                    Require(normalDistance >= 0.20f && normalDistance <= 0.281f, "Expected normal 4px steps, approximately 80px/s.");
                    _driver.EditorSetState(GameState.Attract);
                    _startPosition = _enemyShot.GetComponent<Rigidbody2D>().position;
                    _startTime     = Time.fixedTime;
                    _stage         = 3;
                    Debug.Log($"ROLLING_CHECK_PASS: automatic launch and normal descent {normalDistance / ArcadeViewport.UnitsPerPixel:F1}px.");
                    break;
                case 3:
                    if (Time.fixedTime - _startTime < 0.15f)
                    {
                        return;
                    }
                    Require(_enemyShot.gameObject.activeSelf && _enemyShot.GetComponent<Rigidbody2D>().simulated == false, "Pause must preserve occupancy and stop physics.");
                    Require(Vector2.Distance(_startPosition, _enemyShot.GetComponent<Rigidbody2D>().position) < 1e-6f, "Paused shot moved.");
                    Require(_enemyShot.GetComponentInChildren<SpriteRenderer>(true).enabled == false, "Paused shot is visible.");
                    _driver.EditorSetState(GameState.Playing);
                    // Fixture uses the actual individual reaction/notification path, without new runtime test APIs.
                    foreach (var invader in _invaders.Take(47))
                    {
                        Defeat(invader);
                    }
                    Require(AliveCount() == 8 && DriverValue("_score") == 910, "Eight-survivor fixture / score aggregation.");
                    _startPosition = _enemyShot.GetComponent<Rigidbody2D>().position;
                    _startTime     = Time.fixedTime;
                    _stage         = 4;
                    break;
                case 4:
                    if (Time.fixedTime - _startTime < 0.3f)
                    {
                        return;
                    }
                    float fastDistance = _startPosition.y - _enemyShot.GetComponent<Rigidbody2D>().position.y;
                    Require(fastDistance >= 0.25f && fastDistance <= 0.351f, "Expected 5px steps at eight survivors, approximately 100px/s.");
                    _playerPosition = _player.transform.position;
                    _player.transform.position = new Vector3(_formation.transform.position.x, _playerPosition.y, _playerPosition.z);
                    Require(TryMuzzle(out _) == false, "Empty selected column must not use another column.");
                    _player.transform.position = new Vector3(-10f, _playerPosition.y, _playerPosition.z);
                    Require(TryMuzzle(out _) == false, "Out-of-range target must not clamp to another column.");
                    _player.transform.position = _playerPosition;
                    Debug.Log($"ROLLING_CHECK_PASS: paused physics; fast descent {fastDistance / ArcadeViewport.UnitsPerPixel:F1}px; empty/out-of-range rejection.");
                    _stage = 5;
                    break;
                case 5:
                    if (_enemyShot.gameObject.activeSelf)
                    {
                        return; // Actual bottom-boundary deactivation, not a forced fixture reset.
                    }
                    Debug.Log("ROLLING_CHECK_PASS: shot deactivated at bottom boundary.");
                    foreach (var invader in _invaders.Where(inInvader => inInvader.gameObject.activeSelf))
                    {
                        Defeat(invader);
                    }
                    Require(DriverValue("_score") == 990 && DriverValue("_currentState") == (int)GameState.WaveClear, "Existing score/WaveClear transition.");
                    _stage = 6;
                    break;
                case 6:
                    if (DriverValue("_waveNumber") < 2)
                    {
                        return;
                    }
                    Require(DriverValue("_score") == 990 && AliveCount() == 55, "Wave reset must preserve score and restore targets.");
                    Require(_enemyShot.gameObject.activeSelf == false, "Old Wave shot must be cleared.");
                    Require(_invaders.All(inInvader => inInvader != null && inInvader.gameObject.activeSelf), "Wave must reuse the same Invaders.");
                    Require(Vector2.Distance(_formation.GetComponent<Rigidbody2D>().position, new Vector2(-0.8f, 0.8f)) < 0.025f, "Next Wave initial pose.");
                    var target = _invaders[4 * InvaderFormation.Columns + 5];
                    Require(_playerShot.TryLaunch((Vector2)target.transform.position - Vector2.up * 0.10f), "Existing PlayerShot launch failed.");
                    _startTime = Time.fixedTime;
                    _stage     = 7;
                    break;
                case 7:
                    if (Time.fixedTime - _startTime < 0.15f)
                    {
                        return;
                    }
                    Require(AliveCount() == 54 && DriverValue("_score") == 1000, "Existing PlayerShot Physics2D hit/score regression.");
                    Require(_playerShot.gameObject.activeSelf == false, "Existing PlayerShot was not consumed.");
                    Debug.Log("ROLLING_ENEMY_SHOT_PLAY_PASS: launch/normal/fast/pause/bottom/source/Wave reuse and existing PlayerShot contact.");
                    Finish(0);
                    break;
            }
        }

        private static bool TryMuzzle(out Vector2 outPosition)
        {
            object[] arguments = { default(Vector2) };
            bool result        = (bool)typeof(InvaderFormation).GetMethod("TryGetRollingMuzzle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_formation, arguments);
            outPosition        = (Vector2)arguments[0];
            return result;
        }

        private static void Defeat(Invader inInvader)
        {
            Require((bool)typeof(Invader).GetMethod("TryReceiveHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(inInvader, null), "Fixture defeat was rejected.");
        }

        private static int AliveCount()
        {
            return new SerializedObject(_formation).FindProperty("_aliveCount").intValue;
        }

        private static int DriverValue(string inField)
        {
            return new SerializedObject(_driver).FindProperty(inField).intValue;
        }

        private static void Require(bool inCondition, string inMessage)
        {
            if (inCondition == false)
            {
                throw new InvalidOperationException(inMessage);
            }
        }

        private static void Finish(int inExitCode)
        {
            SessionState.EraseBool(SessionKey);
            EditorApplication.update -= Check;
            if (_driver != null && _driver.enabled)
            {
                _driver.enabled = false;
            }
            EditorApplication.Exit(inExitCode);
        }
    }
}
