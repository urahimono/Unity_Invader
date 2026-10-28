using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    [InitializeOnLoad]
    public static class InvaderDefeatedVerification
    {
        private static double _startAfter;

        private const string       PendingKey      = "UnityInvader.InvaderDefeatedVerification";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static InvaderDefeatedVerification()
        {
            _startAfter = EditorApplication.timeSinceStartup + 2d;
            EditorApplication.update += VerifyWhenPlaying;
        }

        // Batch entry: omit -quit so the domain reload and Play Mode can complete.
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            SessionState.SetBool(PendingKey, true);
            _startAfter = EditorApplication.timeSinceStartup + 2d;
        }

        private static void VerifyWhenPlaying()
        {
            // Batch startup can request another assembly reload after executeMethod returns.
            // Let imports settle before Play so readonly runtime collections are not lost to that reload.
            if (SessionState.GetBool(PendingKey, false) && EditorApplication.isPlayingOrWillChangePlaymode == false &&
                EditorApplication.isCompiling == false && EditorApplication.isUpdating == false &&
                EditorApplication.timeSinceStartup >= _startAfter)
            {
                EditorApplication.isPlaying = true;
                return;
            }
            if (SessionState.GetBool(PendingKey, false) == false || EditorApplication.isPlaying == false ||
                EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode == false)
            {
                return;
            }
            var driver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            if (driver == null || (bool)Read(driver, "_initialized") == false)
            {
                return;
            }
            SessionState.SetBool(PendingKey, false);
            try
            {
                Verify(driver);
                Debug.Log("INVADER_DEFEATED_PASS: normal/B scoring, duplicate guard, final kill, reuse, speed threshold, source and unsubscribe.");
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
            ComponentGameplayVerification.Verify();
            InvaderAnimationVerification.Verify();
            var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormation>();
            var player    = UnityEngine.Object.FindAnyObjectByType<Player>();
            var shot      = (PlayerShot)Read(player, "_shot");
            var invaders  = formation.GetComponentsInChildren<Invader>(true);
            var enemies   = new[] { "_rollingShot", "_plungerShot", "_squigglyShot" }
                .Select(inName => (EnemyShot)Read(formation, inName)).ToArray();
            var ufo         = (Ufo)Read(formation, "_ufo");
            var ufoHandlers = Read(ufo, "Defeated");
            inDriver.EditorSetState(GameState.Playing);
            VerifyUfoControl(inDriver, formation, player, shot, ufo);
            int score                        = inDriver.Score;
            int notifications                = 0;
            Component source                 = null;
            Action<Invader, Component> observe = (inInvader, inSource) =>
            {
                Require(inInvader.gameObject.activeSelf == false, "Inactive before event.");
                notifications++;
                source = inSource;
            };
            invaders[0].Defeated += observe;
            Hit(shot, invaders[0]);
            Require(Alive(formation) == 54 && inDriver.Score == score + 30 && player.NormalShotEndCount == 1,
                "Normal kill counts and scores once through Player.");
            Require(notifications == 1 && source == shot && invaders[0].TryReceiveHit(shot) == false,
                "Source identity and duplicate guard.");
            Require(notifications == 1 && Alive(formation) == 54 && inDriver.Score == score + 30, "No duplicate effects.");

            var debugShot = UnityEngine.Object.Instantiate(shot);
            Hit(debugShot, invaders[1]);
            Require(Alive(formation) == 53 && inDriver.Score == score + 60 && player.NormalShotEndCount == 1,
                "B shot scores once without normal history.");
            for (int i = 2; i < invaders.Length; i++)
            {
                Hit(shot, invaders[i]);
                int alive = 54 - i;
                Require(Alive(formation) == alive, "Exactly one alive decrement per kill.");
                Require(enemies.All(inEnemy => (bool)Read(inEnemy, "_fast") == (alive <= 8)), "Enemy speed threshold.");
                Require(inDriver.CurrentState == (alive == 0 ? GameState.WaveClear : GameState.Playing), "Final kill WaveClear order.");
            }
            Require(inDriver.Score == score + 990, "Full wave score including B shot.");

            // Exercise the existing next-wave transition without waiting for wall-clock time.
            typeof(GameplayDriver).GetField("_waveClearElapsed", PrivateInstance).SetValue(inDriver, 1f);
            Call(inDriver, "UpdateWaveClear");
            Require(inDriver.WaveNumber == 2 && Alive(formation) == 55 && player.NormalShotEndCount == 0,
                "Next wave reset.");
            VerifyPendingUfoLaunch(formation, ufo, 0);
            Call(formation, "PreparePlayerRespawn");
            InvaderAnimationVerification.VerifyFirstFrames(invaders);
            Require(invaders.SequenceEqual(formation.GetComponentsInChildren<Invader>(true)), "Reuse the same 55 instances.");
            Require(invaders.All(inInvader => ((Delegate)Read(inInvader, "Defeated")).GetInvocationList()
                .Count(inHandler => ReferenceEquals(inHandler.Target, formation)) == 1), "One Formation subscription after reuse.");
            Hit(shot, invaders[0]);
            Require(notifications == 2 && Alive(formation) == 54 && inDriver.Score == score + 1020,
                "Reused normal kill not duplicated.");
            Hit(debugShot, invaders[1]);
            for (int i = 2; i < invaders.Length; i++)
            {
                Hit(debugShot, invaders[i]);
            }
            Require(inDriver.CurrentState == GameState.WaveClear && inDriver.Score == score + 1980 &&
                player.NormalShotEndCount == 1, "B final kill and no double scoring.");
            Require(Equals(ufoHandlers, Read(ufo, "Defeated")), "UFO subscription unchanged during waves.");
            invaders[0].Defeated -= observe;
            inDriver.enabled = false;
            // Keep children alive to inspect actual OnDestroy unsubscription.
            foreach (var invader in invaders)
            {
                invader.transform.SetParent(null);
            }
            UnityEngine.Object.DestroyImmediate(formation.gameObject);
            Require(invaders.All(inInvader => Read(inInvader, "Defeated") == null), "All 55 handlers removed on destruction.");
            Require(Read(ufo, "Defeated") == null, "Existing UFO unsubscribe maintained.");
            invaders[0].gameObject.SetActive(true);
            invaders[0].GetComponent<Collider2D>().enabled = true;
            Require(invaders[0].TryReceiveHit(debugShot), "Unsubscribed individual can accept a hit.");
        }

        private static void VerifyUfoControl(GameplayDriver inDriver, InvaderFormation inFormation, Player inPlayer,
            PlayerShot inShot, Ufo inUfo)
        {
            Require(ReferenceEquals(((Delegate)Read(inFormation, "_getNormalShotEndCount")).Target, inPlayer),
                "History reader must be bound to Player, not Driver.");
            for (int count = 0; count < 4; count++)
            {
                Require(inPlayer.NormalShotEndCount == count, "Use actual Player shot-end history.");
                Call(inFormation, "PreparePlayerRespawn");
                VerifyPendingUfoLaunch(inFormation, inUfo, count);
                if (count < 3)
                {
                    Require(inShot.TryLaunch(Vector2.zero), "Launch normal shot for history update.");
                    Call(inShot, "FinishFlight", HitTargetType.None);
                    Require((int)Read(inUfo, "_direction") == (count % 2 == 1 ? 1 : -1),
                        "Shot ending during flight does not reverse the current UFO.");
                }
            }

            var position = inUfo.GetComponent<Rigidbody2D>().position;
            int ticks    = (int)Read(inFormation, "_ufoTicksRemaining");
            inDriver.EditorSetState(GameState.Attract);
            Call(inFormation, "UpdateEnemyShots");
            Require(inUfo.gameObject.activeSelf && inUfo.GetComponent<Rigidbody2D>().simulated == false &&
                inUfo.GetComponent<Rigidbody2D>().position == position &&
                (int)Read(inFormation, "_ufoTicksRemaining") == ticks && (int)Read(inFormation, "_shotPhase") == 0 &&
                (double)Read(inFormation, "_shotTickElapsed") == 0d,
                "Pause preserves UFO occupancy, position and countdown while resetting the shared clock.");
            inDriver.EditorSetState(GameState.Playing);
            Require(inUfo.GetComponent<Rigidbody2D>().simulated && (int)Read(inUfo, "_direction") == 1 &&
                inPlayer.NormalShotEndCount == 3, "Resume preserves flight direction and history.");

            Call(inFormation, "PreparePlayerRespawn");
            var squiggly = (EnemyShot)Read(inFormation, "_squigglyShot");
            Require((bool)typeof(EnemyShot).GetMethod("TryLaunch", PrivateInstance).Invoke(squiggly, new object[] { Vector2.zero }),
                "Launch Squiggly before pending UFO.");
            Set(inFormation, "_ufoPending", true);
            Set(inFormation, "_shotPhase", 2);
            Set(inFormation, "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateEnemyShots");
            Require(inUfo.gameObject.activeSelf == false && (bool)Read(inFormation, "_ufoPending") &&
                (int)Read(squiggly, "_stepCount") == 2, "Shared phase advances occupied Squiggly and leaves UFO pending.");
            Call(squiggly, "ResetFlight");
            VerifyPendingUfoLaunch(inFormation, inUfo, 3);
            Set(inFormation, "_shotPhase", 2);
            Set(inFormation, "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateEnemyShots");
            Require(squiggly.gameObject.activeSelf == false, "UFO occupies the shared phase without launching Squiggly.");

            int lives = inDriver.Lives;
            Call(inDriver, "NotifyPlayerHit");
            Set(inDriver, "_playerDeadElapsed", 0.5f);
            Call(inDriver, "UpdatePlayerDead");
            Require(inDriver.CurrentState == GameState.Playing && inPlayer.NormalShotEndCount == 3 &&
                inUfo.gameObject.activeSelf == false && (bool)Read(inFormation, "_ufoPending") == false &&
                (int)Read(inFormation, "_ufoTicksRemaining") == 0x0600, "Death/respawn resets UFO but retains Player history.");
            VerifyPendingUfoLaunch(inFormation, inUfo, 3);
            // Restore the fixture for the existing two-wave scoring checks below.
            Set(inDriver, "_lives", lives);
            Call(inPlayer, "ResetShotHistory");
            Call(inFormation, "BeginWave");
            Debug.Log("UFO_CONTROL_PASS: Player-bound live history 0..3, Formation launches, shared phase, pause/resume and death/respawn.");
        }

        private static void VerifyPendingUfoLaunch(InvaderFormation inFormation, Ufo inUfo, long inCount)
        {
            Set(inFormation, "_ufoPending", true);
            Set(inFormation, "_shotPhase", 0);
            Set(inFormation, "_shotTickElapsed", 0d);
            Call(inFormation, "UpdateEnemyShots");
            Require(inUfo.gameObject.activeSelf == false, "Phase 0 cannot launch UFO.");
            Call(inFormation, "UpdateEnemyShots");
            int direction = (inCount & 1L) != 0 ? 1 : -1;
            Require(inUfo.gameObject.activeSelf && (int)Read(inUfo, "_direction") == direction &&
                (bool)Read(inFormation, "_ufoPending") == false && (int)Read(inFormation, "_shotPhase") == 1,
                "Phase 2 launches UFO using the latest Player count and consumes pending once.");
        }

        private static void Set(object inTarget, string inName, object inValue)
        {
            inTarget.GetType().GetField(inName, PrivateInstance).SetValue(inTarget, inValue);
        }

        private static void Hit(PlayerShot inShot, Invader inInvader)
        {
            Require(inShot.TryLaunch(inInvader.transform.position), "Launch shot.");
            Call(inShot, "ResolveHit", inInvader.GetComponent<Collider2D>());
            Require(inShot.gameObject.activeSelf == false && inInvader.gameObject.activeSelf == false, "Shot and target consumed.");
        }

        private static int Alive(InvaderFormation inFormation)
        {
            return new SerializedObject(inFormation).FindProperty("_aliveCount").intValue;
        }

        private static object Read(object inTarget, string inName)
        {
            return inTarget.GetType().GetField(inName, PrivateInstance).GetValue(inTarget);
        }

        private static void Call(object inTarget, string inName, params object[] inArguments)
        {
            inTarget.GetType().GetMethod(inName, PrivateInstance).Invoke(inTarget, inArguments);
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
