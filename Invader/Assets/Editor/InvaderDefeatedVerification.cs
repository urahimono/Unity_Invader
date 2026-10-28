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
            var ufo = (Ufo)Read(formation, "_ufo");
            inDriver.EditorSetState(GameState.Playing);
            VerifyUfoControl(inDriver, formation, player, shot, ufo);
            var bomb = inDriver.GetComponent<DebugCarpetBomb>();
            Call(bomb, "UpdateBombardment");
            Call(bomb, "CreatePool");
            var debugShots = (PlayerShot[])Read(bomb, "_shots");
            Require(debugShots.Length == 96, "Use DebugCarpetBomb's actual 96 owned shots.");
            var debugShot = debugShots[0];
            VerifyBombResults(inDriver, player, shot, bomb, debugShot, ufo);
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

            Hit(debugShot, invaders[1]);
            Call(debugShot, "ResolveHit", invaders[1].GetComponent<Collider2D>());
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
            Require(debugShot.TryLaunch(Vector2.zero), "Leave a B shot active across the Wave transition.");
            typeof(GameplayDriver).GetField("_waveClearElapsed", PrivateInstance).SetValue(inDriver, 1f);
            Call(inDriver, "UpdateWaveClear");
            Require(inDriver.WaveNumber == 2 && Alive(formation) == 55 && player.NormalShotEndCount == 0,
                "Next wave reset.");
            Call(bomb, "LateUpdate");
            Require(debugShot.gameObject.activeSelf == false, "Wave change clears B shots without scoring.");
            Call(bomb, "UpdateBombardment");
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
            Action<Invader, Component> verifyFinalOrder = (inInvader, inSource) =>
            {
                Require(Alive(formation) == 0 && inDriver.CurrentState == GameState.Playing &&
                    inDriver.Score == score + 1970 && debugShot.gameObject.activeSelf,
                    "Final B kill only updates Formation; scoring and WaveClear wait for flight end.");
            };
            invaders[invaders.Length - 1].Defeated += verifyFinalOrder;
            for (int i = 2; i < invaders.Length; i++)
            {
                Hit(debugShot, invaders[i]);
            }
            Require(inDriver.CurrentState == GameState.WaveClear && inDriver.Score == score + 1980 &&
                player.NormalShotEndCount == 1, "B final kill and no double scoring.");
            invaders[invaders.Length - 1].Defeated -= verifyFinalOrder;
            Require(debugShots.All(inShot => ((Delegate)Read(inShot, "FlightEnded")).GetInvocationList()
                .Count(inHandler => ReferenceEquals(inHandler.Target, bomb)) == 1), "One owner subscription after two Waves and reuse.");
            UnityEngine.Object.DestroyImmediate(bomb);
            Require(debugShots.All(inShot => Read(inShot, "FlightEnded") == null), "All 96 subscriptions removed when owner is destroyed.");
            invaders[0].Defeated -= observe;
            inDriver.enabled = false;
            // Keep children alive to inspect actual OnDestroy unsubscription.
            foreach (var invader in invaders)
            {
                invader.transform.SetParent(null);
            }
            UnityEngine.Object.DestroyImmediate(formation.gameObject);
            Require(invaders.All(inInvader => Read(inInvader, "Defeated") == null), "All 55 handlers removed on destruction.");
            invaders[0].gameObject.SetActive(true);
            invaders[0].GetComponent<Collider2D>().enabled = true;
            Require(invaders[0].TryReceiveHit(debugShot), "Unsubscribed individual can accept a hit.");
        }

        private static void VerifyBombResults(GameplayDriver inDriver, Player inPlayer, PlayerShot inNormalShot,
            DebugCarpetBomb inBomb, PlayerShot inDebugShot, Ufo inUfo)
        {
            int score = inDriver.Score;
            Call(inBomb, "FireBurst");
            var pool = (PlayerShot[])Read(inBomb, "_shots");
            Require(pool.Count(inShot => inShot.gameObject.activeSelf) == 3 && (int)Read(inBomb, "_laneIndex") == 3,
                "Existing three-shot burst and lanes.");
            inBomb.enabled = false;
            Require(pool.All(inShot => inShot.gameObject.activeSelf == false), "Disabling owner recalls its shots.");
            inBomb.enabled = true;
            Require(inDebugShot.TryLaunch(Vector2.up * (ArcadeViewport.ScreenHeight * 0.5f * ArcadeViewport.UnitsPerPixel)), "Launch above screen.");
            Call(inDebugShot, "CheckScreenExit");
            Require(inDebugShot.gameObject.activeSelf == false, "Screen exit ends B shot.");
            var shield = UnityEngine.Object.FindAnyObjectByType<Shield>();
            var solid  = (bool[])Read(shield, "_solid");
            int pixel  = Array.FindIndex(solid, inValue => inValue);
            var impact = (Vector2)shield.transform.position + new Vector2(pixel % Shield.Width + 0.5f,
                pixel / Shield.Width + 0.5f - PlayerShot.Height * 0.5f) * ArcadeViewport.UnitsPerPixel;
            Require(inDebugShot.TryLaunch(impact), "Launch B shot into a remaining shield pixel.");
            Physics2D.SyncTransforms();
            Require((bool)typeof(PlayerShot).GetMethod("TryHitShield", PrivateInstance).Invoke(inDebugShot, null) &&
                inDebugShot.gameObject.activeSelf == false, "Shield absorption ends B shot as None.");
            Require(inDebugShot.TryLaunch(Vector2.zero), "Launch for forced recall.");
            Call(inBomb, "ClearShots");
            Require(inDebugShot.TryLaunch(Vector2.zero), "Reuse recalled shot.");
            inDriver.EditorSetState(GameState.Attract);
            Call(inBomb, "LateUpdate");
            Require(inDebugShot.gameObject.activeSelf == false && inDriver.Score == score && inPlayer.NormalShotEndCount == 0,
                "None, ResetFlight and stopped recall never score or advance Player history.");
            inDriver.EditorSetState(GameState.Playing);

            HitUfo(inDebugShot, inUfo);
            Require(inDriver.Score == score + 100 && inPlayer.NormalShotEndCount == 0, "B UFO at history 0 scores 100 once.");
            HitUfo(inNormalShot, inUfo);
            Require(inDriver.Score == score + 150 && inPlayer.NormalShotEndCount == 1, "Normal UFO increments history before 50 points.");
            HitUfo(inDebugShot, inUfo);
            Require(inDriver.Score == score + 200 && inPlayer.NormalShotEndCount == 1, "B UFO uses latest history without increment.");
            for (int count = 1; count < 7; count++)
            {
                Require(inNormalShot.TryLaunch(Vector2.zero), "Launch normal None shot.");
                Call(inNormalShot, "FinishFlight", HitTargetType.None);
            }
            HitUfo(inNormalShot, inUfo);
            Require(inDriver.Score == score + 500 && inPlayer.NormalShotEndCount == 8, "Normal UFO at updated history 8 scores 300.");
            HitUfo(inDebugShot, inUfo);
            Require(inDriver.Score == score + 800 && inPlayer.NormalShotEndCount == 8, "B UFO at current history 8 scores 300.");
            Set(inDriver, "_score", score);
            Call(inPlayer, "ResetShotHistory");
            Call(shield, "BeginWave");
            Debug.Log("DEBUG_SHOT_RESULTS_PASS: actual pool, owner enable/disable, None/Shield/recall and normal/B UFO scores/history.");
        }

        private static void HitUfo(PlayerShot inShot, Ufo inUfo)
        {
            Require((bool)typeof(Ufo).GetMethod("TryLaunch", PrivateInstance).Invoke(inUfo, new object[] { true }), "Launch UFO.");
            Require(inShot.TryLaunch(inUfo.transform.position), "Launch shot at UFO.");
            Call(inShot, "ResolveHit", inUfo.GetComponent<Collider2D>());
            Require(inShot.gameObject.activeSelf == false && inUfo.gameObject.activeSelf == false, "UFO and shot consumed.");
            Call(inShot, "ResolveHit", inUfo.GetComponent<Collider2D>());
            Require(inUfo.TryReceiveHit(inShot) == false, "Duplicate UFO hit rejected.");
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
            var driver = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            int score  = driver.Score;
            int points = inInvader.TargetType == HitTargetType.InvaderTop ? 30 :
                inInvader.TargetType == HitTargetType.InvaderMiddle ? 20 : 10;
            Require(inShot.TryLaunch(inInvader.transform.position), "Launch shot.");
            Call(inShot, "ResolveHit", inInvader.GetComponent<Collider2D>());
            Require(inShot.gameObject.activeSelf == false && inInvader.gameObject.activeSelf == false, "Shot and target consumed.");
            Require(driver.Score == score + points, "Each Top/Middle/Bottom kill scores exactly once before WaveClear.");
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
