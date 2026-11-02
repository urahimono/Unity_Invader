using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityInvader.Editor
{
    [InitializeOnLoad]
    public static class GameStartVerification
    {
        private const string       PendingKey      = "UnityInvader.GameStartVerification";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static GameStartVerification()
        {
            EditorApplication.update += Check;
        }

        // Dedicated batch Editor, without -quit. This verifier exits after Play Mode checks.
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetFloat(PendingKey + ".start", (float)EditorApplication.timeSinceStartup);
        }

        private static void Check()
        {
            if (SessionState.GetBool(PendingKey, false) == false)
            {
                return;
            }
            double start = SessionState.GetFloat(PendingKey + ".start", 0f);
            if (EditorApplication.timeSinceStartup - start > 120d)
            {
                SessionState.EraseBool(PendingKey);
                Debug.LogError("Game start verification timed out.");
                EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode == false && EditorApplication.timeSinceStartup - start > 2d)
            {
                EditorApplication.isPlaying = true;
                return;
            }
            var driver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            if (EditorApplication.isPlaying == false || driver == null || (bool)Read(driver, "_initialized") == false)
            {
                return;
            }
            SessionState.EraseBool(PendingKey);
            try
            {
                Verify(driver);
                Debug.Log("GAME_START_PASS: saved Bootstrap -> Attract; Enter gate; Space; ignored mid-game Enter; repeated new games; owner resets; subscriptions; Disable; death; invasion; WaveClear.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Verify(GameplayDriver inDriver)
        {
            Require(inDriver.CurrentState == GameState.Attract, "Saved startup must wait, even with legacy auto-start enabled.");
            var player                             = (Player)Read(inDriver, "_player");
            var formation                          = (InvaderFormation)Read(inDriver, "_formation");
            var shot                               = (PlayerShot)Read(player, "_shot");
            var shields                            = (Shield[])Read(inDriver, "_shields");
            var invaders                           = formation.GetComponentsInChildren<Invader>(true);
            var settings                           = InputSystem.settings;
            var mode                               = settings.updateMode;
            var background                         = settings.backgroundBehavior;
            var editorInput                        = settings.editorInputBehaviorInPlayMode;
            settings.updateMode                    = InputSettings.UpdateMode.ProcessEventsManually;
            settings.backgroundBehavior            = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard                           = InputSystem.AddDevice<Keyboard>();
            try
            {
                var origin = player.Position;
                Pump(inDriver, keyboard, Key.Space, Key.RightArrow);
                Call(player, "FixedUpdate");
                Require(inDriver.CurrentState == GameState.Attract && player.Position == origin && shot.gameObject.activeSelf == false,
                    "Attract ignores movement/Space.");
                Pump(inDriver, keyboard);
                Pump(inDriver, keyboard, Key.Enter, Key.Space);
                Require(inDriver.CurrentState == GameState.Playing && shot.gameObject.activeSelf == false, "Enter starts without replaying held Fire.");
                VerifyFresh(inDriver, player, formation, shields, invaders);
                Pump(inDriver, keyboard);
                Pump(inDriver, keyboard, Key.RightArrow, Key.Space);
                Call(player, "FixedUpdate");
                Require(player.Position.x > origin.x && shot.gameObject.activeSelf, "Playing supports arrows and Space.");
                Call(inDriver, "NotifyShotResolved", HitTargetType.InvaderBottom);
                Pump(inDriver, keyboard, Key.Enter);
                Require(inDriver.Score == 10, "Enter during Playing must not reset Score.");

                int hitSubscriptions   = ((Delegate)Read(player, "HitAccepted")).GetInvocationList().Length;
                int scoreSubscriptions = ((Delegate)Read(player, "ShotResolved")).GetInvocationList().Length;
                for (int retry = 0; retry < 2; retry++)
                {
                    for (int life = 0; life < 3; life++)
                    {
                        Require(player.TryReceiveHit(null), "Actual Player hit acceptance must consume a life.");
                        Set(inDriver, "_playerDeadElapsed", 1f);
                        Pump(inDriver, keyboard, Key.Enter);
                    }
                    Require(inDriver.CurrentState == GameState.GameOver && inDriver.Lives == 0, "Three deaths reach GameOver without held-Enter retry.");
                    DirtyOwners(inDriver, player, formation, shields);
                    Pump(inDriver, keyboard, Key.Enter);
                    Require(inDriver.CurrentState == GameState.GameOver, "Held Enter cannot start again.");
                    Pump(inDriver, keyboard);
                    Pump(inDriver, keyboard, Key.Enter);
                    VerifyFresh(inDriver, player, formation, shields, invaders);
                    Require(((Delegate)Read(player, "HitAccepted")).GetInvocationList().Length == hitSubscriptions &&
                        ((Delegate)Read(player, "ShotResolved")).GetInvocationList().Length == scoreSubscriptions,
                        "Retry must not duplicate subscriptions.");
                }
                Call(inDriver, "NotifyInvaded");
                Require(inDriver.CurrentState == GameState.GameOver, "Invasion event reaches GameOver.");
                Pump(inDriver, keyboard);
                Pump(inDriver, keyboard, Key.Enter);
                VerifyFresh(inDriver, player, formation, shields, invaders);
                inDriver.enabled = false;
                Require((bool)Read(player, "_playing") == false && (bool)Read(formation, "_playing") == false, "Disable stops owners.");
                inDriver.enabled = true;
                Pump(inDriver, keyboard, Key.Enter);
                Require(inDriver.CurrentState == GameState.Playing, "Re-enable is not a new game.");
                Set(formation, "_aliveCount", 0);
                Pump(inDriver, keyboard, Key.Enter);
                Require(inDriver.CurrentState == GameState.WaveClear, "Clear retains its existing transition.");
                Set(inDriver, "_waveClearElapsed", 2f);
                Pump(inDriver, keyboard, Key.Enter);
                Require(inDriver.CurrentState == GameState.Playing && inDriver.WaveNumber == 2, "Held Enter cannot reset WaveClear; normal next-wave flow remains.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                settings.updateMode                    = mode;
                settings.backgroundBehavior            = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
            }
        }

        private static void DirtyOwners(GameplayDriver inDriver, Player inPlayer, InvaderFormation inFormation, Shield[] inShields)
        {
            Set(inDriver, "_score", 999);
            Set(inDriver, "_waveNumber", 7);
            Set(inDriver, "_waveClearElapsed", 0.9f);
            Set(inPlayer, "_normalShotEndCount", 17L);
            var controller = Read(inFormation, "_enemyShots");
            Set(controller, "_plungerColumnIndex", 4);
            Set(controller, "_squigglyColumnIndex", 3);
            Set(controller, "_shotPhase", 2);
            Set(controller, "_shotTickElapsed", 0.01d);
            var ufo = Read(inFormation, "_ufo");
            Set(ufo, "_ticksRemaining", 3);
            Set(ufo, "_pending", true);
            foreach (var shield in inShields)
            {
                Array.Clear((bool[])Read(shield, "_solid"), 0, Shield.Width * Shield.Height);
            }
            var bomb = inDriver.GetComponent<DebugCarpetBomb>();
            if (Read(bomb, "_shots") == null)
            {
                Call(bomb, "CreatePool");
            }
            var debugShots = (PlayerShot[])Read(bomb, "_shots");
            Require(debugShots[0].TryLaunch(Vector2.zero), "Create an old B flight without a LateUpdate between GameOver and retry.");
        }

        private static void VerifyFresh(GameplayDriver inDriver, Player inPlayer, InvaderFormation inFormation, Shield[] inShields, Invader[] inInvaders)
        {
            Require(inDriver.CurrentState == GameState.Playing && inDriver.Score == 0 && inDriver.Lives == 3 && inDriver.WaveNumber == 1,
                "New game resets Driver state.");
            Require(inPlayer.NormalShotEndCount == 0 && (bool)Read(inPlayer, "_hitAccepted") == false &&
                (bool)Read(inPlayer, "_shotOccupied") == false && (bool)Read(inPlayer, "_pendingFire") == false,
                "New game resets Player history, hit state and shot slot.");
            Require(((PlayerShot)Read(inPlayer, "_shot")).gameObject.activeSelf == false, "No old Player flight.");
            Require(inPlayer.Position == new Vector2(0f, -104f * ArcadeViewport.UnitsPerPixel), "Player initial position.");
            Require(inFormation.GetComponentsInChildren<Invader>(true).SequenceEqual(inInvaders) && inInvaders.Length == 55 &&
                inInvaders.All(inInvader => inInvader.gameObject.activeSelf && (int)Read(inInvader, "_frameIndex") == 0), "Reuse all 55 live Invaders with initial animation.");
            Require(inFormation.GetComponent<Rigidbody2D>().position == new Vector2(-80f, 80f) * ArcadeViewport.UnitsPerPixel,
                "Formation returns to its initial Rigidbody2D position.");
            Require((int)Read(inFormation, "_aliveCount") == 55 && (int)Read(inFormation, "_direction") == 1 && (float)Read(inFormation, "_elapsed") == 0f,
                "Formation state reset.");
            var controller = Read(inFormation, "_enemyShots");
            Require((int)Read(controller, "_plungerColumnIndex") == 0 && (int)Read(controller, "_squigglyColumnIndex") == 0 &&
                (int)Read(controller, "_shotPhase") == 0 && (double)Read(controller, "_shotTickElapsed") == 0d, "Enemy clocks/columns reset.");
            foreach (var name in new[] { "_rollingShot", "_plungerShot", "_squigglyShot" })
            {
                var enemy = (EnemyShot)Read(controller, name);
                Require(enemy.gameObject.activeSelf == false && (int)Read(enemy, "_stepCount") == 0, "No old enemy flight.");
            }
            var ufo = (Ufo)Read(inFormation, "_ufo");
            Require(ufo.gameObject.activeSelf == false && (bool)Read(ufo, "_pending") == false && (int)Read(ufo, "_ticksRemaining") == 0x0600, "No old UFO cycle.");
            Require(inShields.Length == 4, "Restore exactly four Shields.");
            foreach (var shield in inShields)
            {
                var solid   = (bool[])Read(shield, "_solid");
                var initial = (Color32[])Read(shield, "_initialPixels");
                Require(solid.Select((inSolid, inIndex) => inSolid == (initial[inIndex].a != 0)).All(inMatch => inMatch), "Shield pixels restored.");
            }
            var bomb       = inDriver.GetComponent<DebugCarpetBomb>();
            var debugShots = (PlayerShot[])Read(bomb, "_shots");
            Require(debugShots == null || debugShots.All(inShot => inShot.gameObject.activeSelf == false), "No old B flights.");
            Require((bool)Read(bomb, "_bombReady") == false, "Held B requires release in the new game.");
        }

        private static void Pump(GameplayDriver inDriver, Keyboard inKeyboard, params Key[] inKeys)
        {
            InputSystem.QueueStateEvent(inKeyboard, new KeyboardState(inKeys));
            InputSystem.Update();
            Call(inDriver, "Update");
        }

        private static void Call(object inTarget, string inName, params object[] inArguments)
        {
            inTarget.GetType().GetMethod(inName, PrivateInstance).Invoke(inTarget, inArguments);
        }

        private static object Read(object inTarget, string inName)
        {
            return inTarget.GetType().GetField(inName, PrivateInstance).GetValue(inTarget);
        }

        private static void Set(object inTarget, string inName, object inValue)
        {
            inTarget.GetType().GetField(inName, PrivateInstance).SetValue(inTarget, inValue);
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
