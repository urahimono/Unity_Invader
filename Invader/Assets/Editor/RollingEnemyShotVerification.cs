using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        private static Vector2          _formationPosition;
        private static Vector2          _playerShotPosition;
        private static Keyboard         _keyboard;
        private static int              _deaths;
        private static int              _scoreBeforeDeath;
        private static int              _waveBeforeDeath;
        private static int              _directionBeforeDeath;
        private static bool             _frameBeforeDeath;
        private static bool[]           _aliveBeforeDeath;

        private const string SessionKey = "UnityInvader.RollingEnemyShotVerification";

        public static void Run()
        {
            if (Application.isBatchMode == false)
            {
                throw new InvalidOperationException("Use the explicitly approved batch process for this verification.");
            }
            PlayerHitSetup.Configure();
            RollingEnemyShotSetup.Configure();
            PlungerEnemyShotSetup.Configure();
            SquigglyEnemyShotSetup.Configure();
            UfoSetup.Configure();
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
                        _stage                      = 0;
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
            if (_stage >= 8)
            {
                RunLivesStage();
                return;
            }
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
                    _enemyShot  = (EnemyShot)new SerializedObject(_formation).FindProperty("_rollingShot").objectReferenceValue;
                    _invaders   = _formation.GetComponentsInChildren<Invader>(true)
                        .OrderByDescending(inInvader => inInvader.transform.localPosition.y)
                        .ThenBy(inInvader => inInvader.transform.localPosition.x).ToArray();
                    // Configure only an in-memory copy for the headless PlayerLoop; no project asset is changed.
                    var inputSettings                           = UnityEngine.Object.Instantiate(InputSystem.settings);
                    InputSystem.settings                        = inputSettings;
                    inputSettings.backgroundBehavior            = InputSettings.BackgroundBehavior.IgnoreFocus;
                    inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    // Isolate only this dedicated batch process from physical keyboard input.
                    foreach (var keyboard in InputSystem.devices.OfType<Keyboard>().ToArray())
                    {
                        InputSystem.DisableDevice(keyboard);
                    }
                    _keyboard = InputSystem.AddDevice<Keyboard>();
                    _deaths   = 0;
                    Require(DriverValue("_lives") == 3, "Initial lives must include the current Player and total three.");
                    Require(_invaders.Length == 55 && AliveCount() == 55, "Initial formation is not 55 living objects.");
                    var hitCollider = _player.GetComponent<BoxCollider2D>();
                    Require(hitCollider.enabled && hitCollider.isTrigger, "Playing must enable Player hit detection.");
                    Require(Vector2.Distance(hitCollider.bounds.size, _player.GetComponent<SpriteRenderer>().bounds.size) < 1e-5f, "Player hitbox must match the current sprite rectangle.");
                    Require(Vector2.Distance(_formation.GetComponent<Rigidbody2D>().position, new Vector2(-0.8f, 0.8f)) < 0.025f, "Formation initial pose.");
                    Require(TryMuzzle(out var muzzle) && Mathf.Abs(muzzle.y - (0.16f - 0.08f)) < 1e-5f, "Rolling must choose the lowest live row.");
                    Debug.Log("ROLLING_CHECK_PASS: Bootstrap -> Arcade; 55 targets; valid lowest-row source.");
                    _stage = 1;
                    break;
                case 1:
                    if (_enemyShot.gameObject.activeSelf == false)
                    {
                        return; // Wait for the real phase/step-count automatic launch.
                    }
                    _startPosition = _enemyShot.GetComponent<Rigidbody2D>().position;
                    _startTime     = Time.fixedTime;
                    // Dodge the real automatic shot, keeping Player's collider enabled.
                    _player.transform.position = new Vector3(1.04f, _player.transform.position.y, _player.transform.position.z);
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
                    Require(_player.GetComponent<BoxCollider2D>().enabled == false && ReceivePlayerHit() == false, "Non-Playing must reject Player damage.");
                    typeof(GameplayDriver).GetMethod("NotifyPlayerHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_driver, null);
                    Require(DriverValue("_currentState") == (int)GameState.Attract, "Driver must ignore a non-Playing hit notification.");
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
                    Require(DriverValue("_currentState") == (int)GameState.Playing, "Dodging a shot must allow continued play.");
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
                    Require(_playerShot.InFlight == false, "Existing PlayerShot was not consumed.");
                    Debug.Log("ROLLING_ENEMY_SHOT_PLAY_PASS: launch/normal/fast/pause/bottom/source/Wave reuse and existing PlayerShot contact.");
                    ArmPlayerDeath();
                    break;
            }
        }

        private static void RunLivesStage()
        {
            switch (_stage)
            {
                case 8:
                    if (DriverValue("_currentState") != (int)GameState.PlayerDead)
                    {
                        return; // Require an actual Physics2D trigger, not a direct damage call.
                    }
                    _deaths++;
                    Require(DriverValue("_lives") == 3 - _deaths, "A death must consume exactly one life.");
                    Require(PrivateValue<bool>(_player, "_hitAccepted"), "Player must accept the trigger hit.");
                    Require(_enemyShot.gameObject.activeSelf == false, "The successful EnemyShot must be consumed.");
                    Require(ReceivePlayerHit() == false && ReceivePlayerHit() == false, "Player must reject repeated damage.");
                    typeof(GameplayDriver).GetMethod("NotifyPlayerHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_driver, null);
                    typeof(EnemyShot).GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_enemyShot,
                        new object[] { _player.GetComponent<BoxCollider2D>() });
                    Require(DriverValue("_lives") == 3 - _deaths, "Duplicate notifications consumed another life.");
                    _playerPosition       = _player.transform.position;
                    _formationPosition    = _formation.GetComponent<Rigidbody2D>().position;
                    _playerShotPosition   = _playerShot.GetComponent<Rigidbody2D>().position;
                    _directionBeforeDeath = PrivateValue<int>(_formation, "_direction");
                    _frameBeforeDeath     = PrivateValue<bool>(_formation, "_frameB");
                    _aliveBeforeDeath     = _invaders.Select(inInvader => inInvader.gameObject.activeSelf).ToArray();
                    _startTime            = Time.time;
                    _stage                = 9;
                    Debug.Log($"LIVES_CHECK_PASS: real Trigger death {_deaths}; lives={DriverValue("_lives")}; duplicate hit/notification rejected.");
                    break;
                case 9:
                    Require(DriverValue("_currentState") != (int)GameState.PlayerReady, "A 1P respawn must not enter PlayerReady.");
                    if (DriverValue("_currentState") == (int)GameState.PlayerDead)
                    {
                        VerifyStopped();
                        return;
                    }
                    Require(Time.time - _startTime >= 0.44f, "Death wait was skipped.");
                    VerifyFormationPreserved();
                    if (_deaths == 3)
                    {
                        Require(DriverValue("_currentState") == (int)GameState.GameOver && DriverValue("_lives") == 0, "Final life must end in GameOver.");
                        _startTime = Time.time;
                        _stage     = 14;
                        return;
                    }
                    Require(DriverValue("_currentState") == (int)GameState.Playing && DriverValue("_lives") == 3 - _deaths, "Remaining lives must resume directly into Playing.");
                    Require(Vector2.Distance(_player.transform.position, new Vector2(0f, -1.04f)) < 1e-5f, "Respawn at (112,24) logical pixels.");
                    Require(_player.GetComponent<BoxCollider2D>().enabled && _player.GetComponent<SpriteRenderer>().enabled && PrivateValue<bool>(_player, "_hitAccepted") == false, "Respawn must restore Player display/hit detection.");
                    Require(_playerShot.InFlight == false && _enemyShot.gameObject.activeSelf == false, "Respawn must clear both old flights.");
                    SetKeys(Key.RightArrow, Key.Space);
                    _startTime = Time.time;
                    _stage     = 10;
                    break;
                case 10:
                    if (Time.time - _startTime < 0.12f)
                    {
                        return;
                    }
                    Require(_player.transform.position.x > 0.03f && PrivateValue<PlayerInputState>(_player, "_inputState").Fire && _playerShot.InFlight == false,
                        $"Movement must resume; held fire must not shoot. x={_player.transform.position.x}, shot={_playerShot.InFlight}, right={PrivateValue<PlayerInputState>(_player, "_inputState").Right}, fire={PrivateValue<PlayerInputState>(_player, "_inputState").Fire}");
                    Require(_enemyShot.gameObject.activeSelf == false, "No stale enemy shot may hit a new life.");
                    SetKeys();
                    _startTime = Time.time;
                    _stage     = 11;
                    break;
                case 11:
                    if (Time.time - _startTime < 0.06f)
                    {
                        return;
                    }
                    SetKeys(Key.Space);
                    _startTime = Time.time;
                    _stage     = 12;
                    break;
                case 12:
                    if (Time.time - _startTime < 0.06f)
                    {
                        return;
                    }
                    Require(_playerShot.InFlight, "Release and a fresh press must fire after respawn.");
                    SetKeys();
                    Debug.Log($"LIVES_CHECK_PASS: respawn {_deaths}; preserved rack/score/wave; position/collider/movement/fire gate restored.");
                    if (_deaths == 1)
                    {
                        foreach (var invader in _invaders.Where(inInvader => inInvader.gameObject.activeSelf))
                        {
                            Defeat(invader);
                        }
                        Require(DriverValue("_lives") == 2 && DriverValue("_currentState") == (int)GameState.WaveClear, "WaveClear must preserve lives.");
                        _stage = 13;
                        return;
                    }
                    ArmPlayerDeath();
                    break;
                case 13:
                    if (DriverValue("_waveNumber") == _waveBeforeDeath)
                    {
                        return;
                    }
                    Require(DriverValue("_waveNumber") == 3 && DriverValue("_score") == 1980 && DriverValue("_lives") == 2 && AliveCount() == 55, "Wave transition must preserve two lives; no score-based extra life yet.");
                    Debug.Log("LIVES_CHECK_PASS: WaveClear -> next Wave; lives=2 across Score 1500 (extra life out of scope).");
                    ArmPlayerDeath();
                    break;
                case 14:
                    VerifyStopped();
                    Require(DriverValue("_currentState") == (int)GameState.GameOver && DriverValue("_lives") == 0, "GameOver must not recover or consume more lives.");
                    typeof(GameplayDriver).GetMethod("NotifyPlayerHit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_driver, null);
                    if (Time.time - _startTime < 2.2f)
                    {
                        return;
                    }
                    Debug.Log("PLAYER_LIVES_PLAY_PASS: 3 real Trigger deaths; lives 3->2->1->0; two direct respawns; rack preservation; virtual input; Wave continuity; GameOver stopped for 2.2s.");
                    Finish(0);
                    break;
            }
        }

        private static void ArmPlayerDeath()
        {
            SetKeys(Key.Space); // Hold this Unity-only virtual key through the death and respawn.
            typeof(PlayerShot).GetMethod("ResetFlight", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_playerShot, null);
            Require(_playerShot.TryLaunch((Vector2)_player.transform.position + Vector2.up * 0.08f), "Launch an old-life PlayerShot fixture.");
            Require((bool)typeof(EnemyShot).GetMethod("TryLaunch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_enemyShot,
                new object[] { (Vector2)_player.transform.position + Vector2.up * 0.24f }), "Launch a descending hit fixture.");
            _scoreBeforeDeath = DriverValue("_score");
            _waveBeforeDeath  = DriverValue("_waveNumber");
            _stage            = 8;
        }

        private static void SetKeys(params Key[] inKeys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(inKeys));
            // Let the normal PlayerLoop process the virtual device; never generate Windows input.
        }

        private static T PrivateValue<T>(object inTarget, string inName)
        {
            return (T)inTarget.GetType().GetField(inName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inTarget);
        }

        private static void VerifyFormationPreserved()
        {
            Require(DriverValue("_score") == _scoreBeforeDeath && DriverValue("_waveNumber") == _waveBeforeDeath, "Death/respawn changed Score or Wave.");
            Require(Vector2.Distance(_formationPosition, _formation.GetComponent<Rigidbody2D>().position) < 1e-6f && PrivateValue<int>(_formation, "_direction") == _directionBeforeDeath && PrivateValue<bool>(_formation, "_frameB") == _frameBeforeDeath, "Death/respawn reset the Formation pose/direction/animation.");
            Require(_aliveBeforeDeath.SequenceEqual(_invaders.Select(inInvader => inInvader.gameObject.activeSelf)) && AliveCount() == _aliveBeforeDeath.Count(inAlive => inAlive), "Death/respawn reset living Invaders.");
        }

        private static void VerifyStopped()
        {
            VerifyFormationPreserved();
            Require(_player.transform.position == _playerPosition && _player.GetComponent<BoxCollider2D>().enabled == false && _player.GetComponent<SpriteRenderer>().enabled == false, "Dead Player must stay still/hidden with collider disabled.");
            Require(_playerShot.InFlight && _playerShot.GetComponent<Rigidbody2D>().simulated == false && Vector2.Distance(_playerShotPosition, _playerShot.GetComponent<Rigidbody2D>().position) < 1e-6f, "Old PlayerShot must stay paused until respawn cleanup.");
            Require(_playerShot.TryLaunch(Vector2.zero) == false && _enemyShot.gameObject.activeSelf == false && _enemyShot.GetComponent<Rigidbody2D>().simulated == false, "Death/GameOver must stop shots.");
        }

        private static bool ReceivePlayerHit()
        {
            return ((IHitReceiver)_player).TryReceiveHit();
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
            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
            if (_driver != null && _driver.enabled)
            {
                _driver.enabled = false;
            }
            EditorApplication.Exit(inExitCode);
        }
    }
}
